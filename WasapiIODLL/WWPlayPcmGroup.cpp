// 日本語UTF-8

#include "WWPlayPcmGroup.h"
#include "WWMFResampler.h"
#include "WWUtil.h"
#include <assert.h>
#include <stdint.h>
#include <list>

WWPlayPcmGroup::WWPlayPcmGroup(void)
{
    m_repeat = false;
    Clear();
}

WWPlayPcmGroup::~WWPlayPcmGroup(void)
{
    assert(m_playPcmDataList.size() == 0);
}

void
WWPlayPcmGroup::Init(void)
{
}

void
WWPlayPcmGroup::Term(void)
{
    Clear();
}

bool
WWPlayPcmGroup::AddPlayPcmData(int id, BYTE *data, int64_t bytes)
{
    assert(1 <= m_numChannels);
    assert(1 <= m_sampleRate);
    assert(1 <= m_bytesPerFrame);

#ifdef _X86_
    if (0x7fffffffL < bytes) {
        // cannot alloc 2GB buffer on 32bit build
        dprintf("E: %s(%d, %p, %lld) cannot alloc 2GB buffer on 32bit build\n", __FUNCTION__, id, data, bytes);
        return false;
    }
#endif

    if (0 == bytes) {
        dprintf("E: %s(%d, %p, %lld) arg check failed\n", __FUNCTION__, id, data, bytes);
        return false;
    }

    WWPcmData pcmData;
    if (!pcmData.Init(id, m_sampleFormat, m_numChannels,
            bytes/m_bytesPerFrame,
            m_bytesPerFrame, WWPcmDataContentPcmData)) {
        dprintf("E: %s(%d, %p, %lld) malloc failed\n", __FUNCTION__, id, data, bytes);
        return false;
    }

    if (NULL != data) {
        CopyMemory(pcmData.stream, data, (bytes/m_bytesPerFrame) * m_bytesPerFrame);
    }
    m_playPcmDataList.push_back(pcmData);
    return true;
}

bool
WWPlayPcmGroup::AddPlayPcmDataStart(
        int sampleRate,
        WWPcmDataSampleFormatType sampleFormat,
        int numChannels,
        DWORD dwChannelMask,
        int bytesPerFrame)
{
    assert(m_playPcmDataList.size() == 0);
    assert(1 <= numChannels);
    assert(1 <= sampleRate);
    assert(1 <= bytesPerFrame);

    m_sampleRate    = sampleRate;
    m_sampleFormat  = sampleFormat;
    m_numChannels   = numChannels;
    m_bytesPerFrame = bytesPerFrame;
    m_dwChannelMask = dwChannelMask;

    return true;
}

void
WWPlayPcmGroup::AddPlayPcmDataEnd(void)
{
    PlayPcmDataListDebug();
}

void
WWPlayPcmGroup::RemoveAt(int id)
{
    assert(0 <= id && id < m_playPcmDataList.size());

    WWPcmData *pcmData = &m_playPcmDataList[id];
    pcmData->Term();

    m_playPcmDataList.erase(m_playPcmDataList.begin()+id);

    // 連続再生のリンクリストをつなげ直す。
    SetPlayRepeat(m_repeat);
}

void
WWPlayPcmGroup::Clear(void)
{
    for (size_t i=0; i<m_playPcmDataList.size(); ++i) {
        m_playPcmDataList[i].Term();
    }
    m_playPcmDataList.clear();

    m_sampleRate    = 0;
    m_sampleFormat  = WWPcmDataSampleFormatUnknown;
    m_numChannels   = 0;
    m_bytesPerFrame = 0;
}

void
WWPlayPcmGroup::SetPlayRepeat(bool repeat)
{
    dprintf("D: %s(%d)\n", __FUNCTION__, (int)repeat);
    m_repeat = repeat;

    if (m_playPcmDataList.size() < 1) {
        dprintf("D: %s(%d) pcmDataList.size() == %d nothing to do\n",
            __FUNCTION__, (int)repeat, m_playPcmDataList.size());
        return;
    }

    // 最初のpcmDataから、最後のpcmDataまでnextでつなげる。
    // リピートフラグが立っていたら最後のpcmDataのnextを最初のpcmDataにする。
    for (size_t i=0; i<m_playPcmDataList.size(); ++i) {
        if (i == m_playPcmDataList.size()-1) {
            // 最後→最初に接続。
            if (repeat) {
                m_playPcmDataList[i].next = 
                    &m_playPcmDataList[0];
            } else {
                // 最後→NULL
                m_playPcmDataList[i].next = NULL;
            }
        } else {
            // 最後のあたりの項目以外は、連続にnextをつなげる。
            m_playPcmDataList[i].next = 
                &m_playPcmDataList[i+1];
        }
    }
}

WWPcmData *
WWPlayPcmGroup::FindPcmDataById(int id)
{
    for (size_t i=0; i<m_playPcmDataList.size(); ++i) {
        if (m_playPcmDataList[i].id == id) {
            return &m_playPcmDataList[i];
        }
    }

    return NULL;
}

WWPcmData *
WWPlayPcmGroup::FirstPcmData(void)
{
    if (0 == m_playPcmDataList.size()) {
        return NULL;
    }

    return &m_playPcmDataList[0];
}

WWPcmData *
WWPlayPcmGroup::LastPcmData(void)
{
    if (0 == m_playPcmDataList.size()) {
        return NULL;
    }

    return &m_playPcmDataList[m_playPcmDataList.size()-1];
}

void
WWPlayPcmGroup::PlayPcmDataListDebug(void)
{
#ifdef _DEBUG
    dprintf("D: %s() count=%u\n", __FUNCTION__, m_playPcmDataList.size());
    for (size_t i=0; i<m_playPcmDataList.size(); ++i) {
        WWPcmData *p = &m_playPcmDataList[i];

        dprintf("  %p next=%p i=%d id=%d nFrames=%d posFrame=%d contentType=%s stream=%p\n",
            p, p->next, i, p->id, p->nFrames, p->posFrame,
            WWPcmDataContentTypeToStr(p->contentType), p->stream);
    }
#endif
}

HRESULT
WWPlayPcmGroup::DoResample(
        int sampleRate, WWPcmDataSampleFormatType sampleFormat, int numChannels, DWORD dwChannelMask, int conversionQuality)
{
    HRESULT hr = S_OK;
    WWMFResampler resampler;
    size_t n = m_playPcmDataList.size();
    const int PROCESS_FRAMES = 128 * 1024;
    BYTE *buff = new BYTE[PROCESS_FRAMES * m_bytesPerFrame];
    std::list<size_t> toPcmDataIdxList;
    size_t numConvertedPcmData = 0;
    assert(1 <= conversionQuality && conversionQuality <= 60);

    if (NULL == buff) {
        hr = E_OUTOFMEMORY;
        goto end;
    }

    // 共有モードのサンプルレート変更。
    HRG(resampler.Initialize(
        WWMFPcmFormat(
            (WWMFBitFormatType)WWPcmDataSampleFormatTypeIsFloat(m_sampleFormat),
            (WORD)m_numChannels,
            (WORD)WWPcmDataSampleFormatTypeToBitsPerSample(m_sampleFormat),
            m_sampleRate,
            0, //< TODO: target dwChannelMask
            (WORD)WWPcmDataSampleFormatTypeToValidBitsPerSample(m_sampleFormat)),
        WWMFPcmFormat(
            WWMFBitFormatFloat,
            (WORD)numChannels,
            32,
            sampleRate,
            0, //< TODO: target dwChannelMask
            32),
        conversionQuality));

    for (size_t i=0; i<n; ++i) {
        WWPcmData *pFrom = &m_playPcmDataList[i];
        WWPcmData pcmDataTo;

        if (!pcmDataTo.Init(pFrom->id, sampleFormat, numChannels,
                (int64_t)(((double)sampleRate / m_sampleRate) * pFrom->nFrames),
                numChannels * WWPcmDataSampleFormatTypeToBitsPerSample(sampleFormat)/8, WWPcmDataContentPcmData)) {
            dprintf("E: %s malloc failed. pcm id=%d\n", __FUNCTION__, pFrom->id);
            hr = E_OUTOFMEMORY;
            goto end;
        }
        m_playPcmDataList.push_back(pcmDataTo);
        pFrom = &m_playPcmDataList[i];

        toPcmDataIdxList.push_back(n+i);

        dprintf("D: pFrom stream=%p nFrames=%lld\n", pFrom->stream, pFrom->nFrames);

        for (size_t posFrames=0; ; posFrames += PROCESS_FRAMES) {
            WWMFSampleData mfSampleData;
            DWORD consumedBytes = 0;

            int buffBytes = pFrom->GetBufferData(posFrames * m_bytesPerFrame, PROCESS_FRAMES * m_bytesPerFrame, buff);
            dprintf("D: pFrom->GetBufferData posBytes=%lld bytes=%d rv=%d\n",
                    posFrames * m_bytesPerFrame, PROCESS_FRAMES * m_bytesPerFrame, buffBytes);
            if (0 == buffBytes) {
                break;
            }

            HRG(resampler.Resample(buff, buffBytes, &mfSampleData));
            dprintf("D: resampler.Resample mfSampleData.bytes=%u\n",
                    mfSampleData.bytes);
            consumedBytes = 0;
            while (0 < toPcmDataIdxList.size() && consumedBytes < mfSampleData.bytes) {
                size_t toIdx = toPcmDataIdxList.front();
                WWPcmData *pTo = &m_playPcmDataList[toIdx];
                assert(pTo);
                int rv = pTo->FillBufferAddData(&mfSampleData.data[consumedBytes], mfSampleData.bytes - consumedBytes);
                dprintf("D: consumedBytes=%d/%d FillBufferAddData() pTo->stream=%p pTo->nFrames=%lld rv=%d\n",
                        consumedBytes, mfSampleData.bytes, pTo->stream, pTo->nFrames, rv);
                consumedBytes += rv;
                if (0 == rv) {
                    pTo->FillBufferEnd();
                    ++numConvertedPcmData;
                    toPcmDataIdxList.pop_front();
                }
            }
            mfSampleData.Release();
        }
        pFrom->Term();
    }

    {
        WWMFSampleData mfSampleData;
        DWORD consumedBytes = 0;

        HRG(resampler.Drain(PROCESS_FRAMES * m_bytesPerFrame, &mfSampleData));
        consumedBytes = 0;
        while (0 < toPcmDataIdxList.size() && consumedBytes < mfSampleData.bytes) {
            size_t toIdx = toPcmDataIdxList.front();
            WWPcmData *pTo = &m_playPcmDataList[toIdx];
            assert(pTo);
            int rv = pTo->FillBufferAddData(&mfSampleData.data[consumedBytes], mfSampleData.bytes - consumedBytes);
            consumedBytes += rv;
            if (0 == rv) {
                pTo->FillBufferEnd();
                ++numConvertedPcmData;
                toPcmDataIdxList.pop_front();
            }
        }
        mfSampleData.Release();
    }

    while (0 < toPcmDataIdxList.size()) {
        size_t toIdx = toPcmDataIdxList.front();
        WWPcmData *pTo = &m_playPcmDataList[toIdx];
        assert(pTo);

        pTo->FillBufferEnd();
        if (0 == pTo->nFrames) {
            hr = E_FAIL;
            goto end;
        }
        ++numConvertedPcmData;
        toPcmDataIdxList.pop_front();
    }

    assert(n == numConvertedPcmData);

    for (size_t i=0; i<n; ++i) {
        m_playPcmDataList[i] = m_playPcmDataList[n+i];
        m_playPcmDataList[n+i].Forget();
    }

    m_playPcmDataList.resize(numConvertedPcmData);

    // update pcm format info
    m_sampleFormat  = sampleFormat;
    m_sampleRate    = sampleRate;
    m_numChannels   = numChannels;
    m_dwChannelMask = dwChannelMask;
    m_bytesPerFrame = numChannels * WWPcmDataSampleFormatTypeToBitsPerSample(sampleFormat)/8;

    // reduce volume level when out of range sample value is found
    {
        float maxV = 0.0f;
        float minV = 0.0f;
        const float  SAMPLE_VALUE_MAX_FLOAT  =  1.0f;
        const float  SAMPLE_VALUE_MIN_FLOAT  = -1.0f;

        for (size_t i=0; i<n; ++i) {
            float currentMax = 0.0f;
            float currentMin = 0.0f;
            m_playPcmDataList[i].FindSampleValueMinMax(&currentMin, &currentMax);
            if (currentMin < minV) {
                minV = currentMin;
            }
            if (maxV < currentMax) {
                maxV = currentMax;
            }
        }

        float scale = 1.0f;
        if (SAMPLE_VALUE_MAX_FLOAT < maxV) {
            scale = SAMPLE_VALUE_MAX_FLOAT / maxV;
        }
        if (minV < SAMPLE_VALUE_MIN_FLOAT && SAMPLE_VALUE_MIN_FLOAT / minV < scale) {
            scale = SAMPLE_VALUE_MIN_FLOAT / minV;
        }
        if (scale < 1.0f) {
            for (size_t i=0; i<n; ++i) {
                m_playPcmDataList[i].ScaleSampleValue(scale);
            }
        }
    }

end:
    resampler.Finalize();
    delete [] buff;
    buff = NULL;
    return hr;
}
