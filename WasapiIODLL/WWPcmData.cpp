// 日本語 UTF-8

#include "WWPcmData.h"
#include "WWUtil.h"
#include <assert.h>
#include <malloc.h>
#include <stdint.h>

const char *
WWPcmDataContentTypeToStr(WWPcmDataContentType w)
{
    switch (w) {
    case WWPcmDataContentSilence: return "Silence";
    case WWPcmDataContentPcmData: return "PcmData";
    case WWPcmDataContentSplice:  return "Splice";
    default: return "unknown";
    }
}

const char *
WWPcmDataFormatTypeToStr(WWPcmDataFormatType w)
{
    switch (w) {
    case WWPcmDataFormatSint16: return "Sint16";
    case WWPcmDataFormatSint24: return "Sint24";
    case WWPcmDataFormatSint32V24: return "Sint32V24";
    case WWPcmDataFormatSint32: return "Sint32";
    case WWPcmDataFormatSfloat: return "Sfloat";
    default: return "unknown";
    }
}

WWPcmDataFormatType
WWPcmDataBitsPerSamplesToFormatType(int bitsPerSample, int validBitsPerSample, GUID subFormat)
{
    if (subFormat == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT) {
        if (bitsPerSample == 32 &&
            validBitsPerSample == 32) {
            return WWPcmDataFormatSfloat;
        }
        return WWPcmDataFormatUnknown;
    }
    
    if (subFormat == KSDATAFORMAT_SUBTYPE_PCM) {
        switch (bitsPerSample) {
        case 16:
            if (validBitsPerSample == 16) {
                return WWPcmDataFormatSint16;
            }
            break;
        case 24:
            if (validBitsPerSample == 24) {
                return WWPcmDataFormatSint24;
            }
            break;
        case 32:
            if (validBitsPerSample == 24) {
                return WWPcmDataFormatSint32V24;
            }
            if (validBitsPerSample == 32) {
                return WWPcmDataFormatSint32;
            }
            break;
        default:
            break;
        }
        return WWPcmDataFormatUnknown;
    }

    return WWPcmDataFormatUnknown;
}

int
WWPcmDataFormatTypeToBitsPerSample(WWPcmDataFormatType t)
{
    static const int result[WWPcmDataFormatNUM]
        = { 16, 24, 32, 32, 32 };

    if (t < 0 || WWPcmDataFormatNUM <= t) {
        assert(0);
        return -1;
    }
    return result[t];
}

int
WWPcmDataFormatTypeToValidBitsPerSample(WWPcmDataFormatType t)
{
    static const int result[WWPcmDataFormatNUM]
        = { 16, 24, 24, 32, 32 };

    if (t < 0 || WWPcmDataFormatNUM <= t) {
        assert(0);
        return -1;
    }
    return result[t];
}

bool
WWPcmDataFormatTypeIsFloat(WWPcmDataFormatType t)
{
    static const bool result[WWPcmDataFormatNUM]
        = { false, false, false, false, true };

    if (t < 0 || WWPcmDataFormatNUM <= t) {
        assert(0);
        return false;
    }
    return result[t];
}

bool
WWPcmDataFormatTypeIsInt(WWPcmDataFormatType t)
{
    static const bool result[WWPcmDataFormatNUM]
        = { true, true, true, true, false };

    if (t < 0 || WWPcmDataFormatNUM <= t) {
        assert(0);
        return false;
    }
    return result[t];
}

void
WWPcmData::Term(void)
{
    dprintf("D: %s() stream=%p\n", __FUNCTION__, stream);

    free(stream);
    stream = NULL;
}

WWPcmData::~WWPcmData(void)
{
    // ここでstreamをfreeする必要はない。
    // streamがNULLでなくても問題ない！
    // メモリリークしないように呼び出し側が気をつける。
}

void
WWPcmData::CopyFrom(WWPcmData *rhs)
{
    *this = *rhs;

    next = NULL;

    int64_t bytes = nFrames * frameBytes;
    assert(0 < bytes);

    stream = (BYTE*)malloc(bytes);
    CopyMemory(stream, rhs->stream, bytes);
}

bool
WWPcmData::Init(
        int aId, WWPcmDataFormatType aFormat, int anChannels,
        int64_t anFrames, int aframeBytes, WWPcmDataContentType dataType)
{
    assert(stream == NULL);

    id       = aId;
    format   = aFormat;
    contentType = dataType;
    next     = NULL;
    posFrame = 0;
    nChannels = anChannels;
    // メモリ確保に成功してからフレーム数をセットする。
    nFrames  = 0;
    frameBytes = aframeBytes;
    stream   = NULL;

    int64_t bytes = anFrames * aframeBytes;
    if (bytes < 0) {
        return false;
    }
#ifdef _X86_
    if (0x7fffffffL < bytes) {
        // cannot alloc 2GB buffer on 32bit build
        return false;
    }
#endif

    BYTE *p = (BYTE *)malloc(bytes);
    if (NULL == p) {
        // 失敗…
        return false;
    }

    ZeroMemory(p, bytes);
    nFrames = anFrames;
    stream = p;
    return true;
}

int
WWPcmData::GetSampleValueInt(int ch, int64_t posFrame)
{
    assert(format != WWPcmDataFormatSfloat);
    assert(0 <= ch && ch < nChannels);

    if (posFrame < 0 ||
        nFrames <= posFrame) {
        return 0;
    }

    int result = 0;
    switch (format) {
    case WWPcmDataFormatSint16:
        {
            short *p = (short*)(&stream[2 * (nChannels * posFrame + ch)]);
            result = *p;
        }
        break;
    case WWPcmDataFormatSint24:
        {
            // bus error回避。x86にはbus error無いけど一応。
            unsigned char *p =
                (unsigned char*)(&stream[3 * (nChannels * posFrame + ch)]);

            result =
                (((unsigned int)p[0])<<8) +
                (((unsigned int)p[1])<<16) +
                (((unsigned int)p[2])<<24);
            result /= 256;
        }
        break;
    case WWPcmDataFormatSint32V24:
        {
            int *p = (int*)(&stream[4 * (nChannels * posFrame + ch)]);
            result = ((*p)/256);
        }
        break;
    case WWPcmDataFormatSint32:
        {
            // mallocで確保したバッファーなので、bus errorは起きない。
            int *p = (int*)(&stream[4 * (nChannels * posFrame + ch)]);
            result = *p;
        }
        break;
    default:
        assert(0);
        break;
    }

    return result;
}

float
WWPcmData::GetSampleValueFloat(int ch, int64_t posFrame)
{
    assert(format == WWPcmDataFormatSfloat);
    assert(0 <= ch && ch < nChannels);

    if (posFrame < 0 ||
        nFrames <= posFrame) {
        return 0;
    }

    float *p = (float *)(&stream[4 * (nChannels * posFrame + ch)]);
    return *p;
}

float
WWPcmData::GetSampleValueAsFloat(int ch, int64_t posFrame)
{
    float result = 0.0f;

    switch (format) {
    case WWPcmDataFormatSint16:
        result = GetSampleValueInt(ch, posFrame) * (1.0f / 32768.0f);
        break;
    case WWPcmDataFormatSint24:
    case WWPcmDataFormatSint32V24:
        result = GetSampleValueInt(ch, posFrame) * (1.0f / 8388608.0f);
        break;
    case WWPcmDataFormatSint32:
        result = GetSampleValueInt(ch, posFrame) * (1.0f / 2147483648.0f);
        break;
    case WWPcmDataFormatSfloat:
        result = GetSampleValueFloat(ch, posFrame);
        break;
    default:
        assert(0);
        break;
    }
    return result;
}

bool
WWPcmData::SetSampleValueInt(int ch, int64_t posFrame, int value)
{
    assert(format != WWPcmDataFormatSfloat);
    assert(0 <= ch && ch < nChannels);

    if (posFrame < 0 ||
        nFrames <= posFrame) {
        return false;
    }

    switch (format) {
    case WWPcmDataFormatSint16:
        {
            short *p =
                (short*)(&stream[2 * (nChannels * posFrame + ch)]);
            *p = (short)value;
        }
        break;
    case WWPcmDataFormatSint24:
        {
            // bus error回避。x86にはbus error無いけど一応。
            unsigned char *p =
                (unsigned char*)(&stream[3 * (nChannels * posFrame + ch)]);
            p[0] = (unsigned char)(value & 0xff);
            p[1] = (unsigned char)((value>>8) & 0xff);
            p[2] = (unsigned char)((value>>16) & 0xff);
        }
        break;
    case WWPcmDataFormatSint32V24:
        {
            unsigned char *p =
                (unsigned char*)(&stream[4 * (nChannels * posFrame + ch)]);
            p[0] = 0;
            p[1] = (unsigned char)(value & 0xff);
            p[2] = (unsigned char)((value>>8) & 0xff);
            p[3] = (unsigned char)((value>>16) & 0xff);
        }
        break;
    case WWPcmDataFormatSint32:
        {
            // mallocで確保したバッファーなので、bus errorは起きない。
            int *p = (int*)(&stream[4 * (nChannels * posFrame + ch)]);
            *p = value;
        }
        break;
    default:
        assert(0);
        break;
    }

    return true;
}

bool
WWPcmData::SetSampleValueFloat(int ch, int64_t posFrame, float value)
{
    assert(format == WWPcmDataFormatSfloat);
    assert(0 <= ch && ch < nChannels);

    if (posFrame < 0 ||
        nFrames <= posFrame) {
        return false;
    }

    float *p = (float *)(&stream[4 * (nChannels * posFrame + ch)]);
    *p = value;
    return true;
}

bool
WWPcmData::SetSampleValueAsFloat(int ch, int64_t posFrame, float value)
{
    bool result = false;

    switch (format) {
    case WWPcmDataFormatSint16:
        result = SetSampleValueInt(ch, posFrame, (int)(value * 32768.0f));
        break;
    case WWPcmDataFormatSint24:
    case WWPcmDataFormatSint32V24:
        result = SetSampleValueInt(ch, posFrame, (int)(value * 8388608.0f));
        break;
    case WWPcmDataFormatSint32:
        result = SetSampleValueInt(ch, posFrame, (int)(value * 2147483648.0f));
        break;
    case WWPcmDataFormatSfloat:
        result = SetSampleValueFloat(ch, posFrame, value);
        break;
    default:
        assert(0);
        break;
    }
    return result;
}

struct PcmSpliceInfoFloat {
    float dydx;
    float y;
};

struct PcmSpliceInfoInt {
    int deltaX;
    int error;
    int ystep;
    int deltaError;
    int deltaErrorDirection;
    int y;
};

int
WWPcmData::UpdateSpliceDataWithStraightLine(
        WWPcmData *fromPcmData, int64_t fromPosFrame,
        WWPcmData *toPcmData,   int64_t toPosFrame)
{
    assert(0 < nFrames && nFrames <= 0x7fffffff);

    switch (fromPcmData->format) {
    case WWPcmDataFormatSfloat:
        {
            // floatは、簡単。
            PcmSpliceInfoFloat *p =
                (PcmSpliceInfoFloat*)_malloca(nChannels * sizeof(PcmSpliceInfoFloat));
            assert(p);

            for (int ch=0; ch<nChannels; ++ch) {
                float y0 = fromPcmData->GetSampleValueFloat(ch, fromPosFrame);
                float y1 = toPcmData->GetSampleValueFloat(ch, toPosFrame);
                p[ch].dydx = (y1 - y0)/(nFrames);
                p[ch].y = y0;
            }

            for (int x=0; x<nFrames; ++x) {
                for (int ch=0; ch<nChannels; ++ch) {
                    SetSampleValueFloat(ch, x, p[ch].y);
                    p[ch].y += p[ch].dydx;
                }
            }

            _freea(p);
            p = NULL;
        }
        break;
    default:
        {
            // Bresenham's line algorithm的な物
            PcmSpliceInfoInt *p =
                (PcmSpliceInfoInt*)_malloca(nChannels * sizeof(PcmSpliceInfoInt));
            assert(p);

            for (int ch=0; ch<nChannels; ++ch) {
                int y0 = fromPcmData->GetSampleValueInt(ch, fromPosFrame);
                int y1 = toPcmData->GetSampleValueInt(ch, toPosFrame);
                p[ch].deltaX = (int)nFrames;
                p[ch].error  = p[ch].deltaX/2;
                p[ch].ystep  = ((int64_t)y1 - y0)/p[ch].deltaX;
                p[ch].deltaError = abs(y1 - y0) - abs(p[ch].ystep * p[ch].deltaX);
                p[ch].deltaErrorDirection = (y1-y0) >= 0 ? 1 : -1;
                p[ch].y = y0;
            }

            for (int x=0; x<(int)nFrames; ++x) {
                for (int ch=0; ch<nChannels; ++ch) {
                    SetSampleValueInt(ch, x, p[ch].y);
                    // printf("(%d %d)", x, y);
                    p[ch].y += p[ch].ystep;
                    p[ch].error -= p[ch].deltaError;
                    if (p[ch].error < 0) {
                        p[ch].y += p[ch].deltaErrorDirection;
                        p[ch].error += p[ch].deltaX;
                    }
                }
            }
            // printf("\n");

            _freea(p);
            p = NULL;
        }
        break;
    }

    return 0;
}

int
WWPcmData::CreateCrossfadeData(
        WWPcmData *fromPcmData, int64_t fromPosFrame,
        WWPcmData *toPcmData,   int64_t toPosFrame)
{
    assert(0 < nFrames && nFrames <= 0x7fffffff);

    for (int ch=0; ch<nChannels; ++ch) {
        WWPcmData *from = fromPcmData;
        int64_t fromPos = fromPosFrame;

        WWPcmData *to = toPcmData;
        int64_t toPos = toPosFrame;

        for (int x=0; x<nFrames; ++x) {
            float ratio = (float)x / nFrames;

            float y0 = from->GetSampleValueAsFloat(ch, fromPos);
            float y1 = to->GetSampleValueAsFloat(ch, toPos);

            SetSampleValueAsFloat(ch, x, y0 * (1-ratio) + y1 * ratio);

            ++fromPos;
            if (from->nFrames <= fromPos && NULL != from->next) {
                from = from->next;
            }

            ++toPos;
            if (to->nFrames <= toPos && NULL != to->next) {
                to = to->next;
            }
        }
    }

    // クロスフェードのPCMデータは2GBもない(assertでチェックしている)。intにキャストする。
    return (int)nFrames; 
}
