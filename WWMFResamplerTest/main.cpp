// 日本語UTF-8
#pragma warning(disable:4127)  // Disable warning C4127: conditional expression is constant

#define WINVER _WIN32_WINNT_WIN7

#include "WWMFResampler.h"
#include <crtdbg.h>
#include <math.h>
#include <stdio.h>
#include <stdint.h>

#define WW_PIF       3.14159265358979323846f

template <class T> void SafeRelease(T **ppT) {
    if (*ppT) {
        (*ppT)->Release();
        *ppT = NULL;
    }
}

#ifdef _DEBUG
#  include <stdio.h>
#  define dprintf(x, ...) printf(x, __VA_ARGS__)
#else
#  define dprintf(x, ...)
#endif

#define HRG(x)                                    \
{                                                 \
    dprintf("D: %s\n", #x);                       \
    hr = x;                                       \
    if (FAILED(hr)) {                             \
        printf("E: %s:%d %s failed (%08x)\n",     \
            __FILE__, __LINE__, #x, hr);          \
        goto end;                                 \
    }                                             \
}                                                 \

static HRESULT
ReadInt16(FILE *fpr, short *value_return)
{
    HRESULT hr = E_FAIL;
    int readBytes = 0;
    assert(fpr);
    assert(value_return);

    readBytes = fread(value_return, 1, 2, fpr);
    if (readBytes != 2) {
        printf("read error\n");
        goto end;
    }

    hr = S_OK;

end:
    return hr;
}

static HRESULT
ReadInt32(FILE *fpr, int *value_return)
{
    HRESULT hr = E_FAIL;
    int readBytes = 0;
    assert(fpr);
    assert(value_return);

    readBytes = fread(value_return, 1, 4, fpr);
    if (readBytes != 4) {
        printf("read error\n");
        goto end;
    }

    hr = S_OK;

end:
    return hr;
}

static HRESULT
WriteInt32(FILE *fpw, int value)
{
    HRESULT hr = E_FAIL;
    int writeBytes = 0;

    writeBytes = fwrite(&value, 1, 4, fpw);
    if (4 == writeBytes) {
        hr = S_OK;
    }

    return hr;
}

static HRESULT
WriteInt16(FILE *fpw, short value)
{
    HRESULT hr = E_FAIL;
    int writeBytes = 0;

    writeBytes = fwrite(&value, 1, 2, fpw);
    if (2 == writeBytes) {
        hr = S_OK;
    }

    return hr;
}

static HRESULT
WriteStr(FILE *fpw, const char *s, int bytes)
{
    HRESULT hr = E_FAIL;
    int writeBytes = 0;

    writeBytes = fwrite(s, 1, bytes, fpw);
    if (bytes == writeBytes) {
        hr = S_OK;
    }

    return hr;
}

static HRESULT
ReadWavHeader(FILE *fpr, WWMFPcmFormat *format_return, int *dataBytes_return)
{
    HRESULT hr = E_FAIL;
    BYTE buff[16];
    int readBytes = 0;
    int chunkBytes = 0;
    int fmtChunkSize = 0;
    short shortValue;
    int intValue;

    readBytes = fread(buff, 1, 12, fpr);
    if (readBytes != 12) { goto end; }

    if (0 != memcmp(buff, "RIFF", 4)) {
        printf("file is not riff wave file\n");
        goto end;
    }
    if (0 != memcmp(&buff[8], "WAVE", 4)) {
        printf("file is not riff wave file\n");
        goto end;
    }

    for (;;) {
        readBytes = fread(buff, 1, 4, fpr);
        if (readBytes != 4) {
            printf("read error");
            goto end;
        }

        if (0 == memcmp(buff, "fmt ", 4)) {
            // fmt chunk

            // chunkSize size==4
            HRG(ReadInt32(fpr, &fmtChunkSize));
            if (16 != fmtChunkSize && 18 != fmtChunkSize && 40 != fmtChunkSize) {
                printf("unrecognized format");
                goto end;
            }
            // audioFormat size==2
            HRG(ReadInt16(fpr, &shortValue));
            if (1 == shortValue) {
                format_return->sampleFormat = WWMFSampleFormatInt;
            } else if (3 == shortValue) {
                format_return->sampleFormat = WWMFSampleFormatFloat;
            } else if (0xfffe == (unsigned short)shortValue) {
                // WAVEFORMATEXTENSIBLEに書いてある。
                format_return->sampleFormat = WWMFSampleFormatUnknown;
            } else {
                printf("unrecognized format");
                goto end;
            }

            // numChannels size==2
            HRG(ReadInt16(fpr, &shortValue));
            format_return->nChannels = shortValue;

            // sampleRate size==4
            HRG(ReadInt32(fpr, &intValue));
            format_return->sampleRate = intValue;

            // byteRate size==4
            HRG(ReadInt32(fpr, &intValue));

            // blockAlign size==2
            HRG(ReadInt16(fpr, &shortValue));

            // bitspersample size==2
            HRG(ReadInt16(fpr, &shortValue));
            format_return->bits = shortValue;
            format_return->validBitsPerSample = shortValue;

            if (16 < fmtChunkSize) {
                // subchunksize
                HRG(ReadInt16(fpr, &shortValue));
                if (0 == shortValue) {
                    hr = S_OK;
                    goto end;
                } else if (22 == shortValue) {
                    // validbitspersample
                    HRG(ReadInt16(fpr, &shortValue));
                    format_return->validBitsPerSample = shortValue;

                    // dwChannelMask
                    HRG(ReadInt32(fpr, (int*)&format_return->dwChannelMask));

                    // format GUID
                    readBytes = fread(buff, 1, 16, fpr);
                    if (readBytes != 16) {
                        printf("read error");
                        goto end;
                    }

                    if (0 == memcmp(buff, &MFAudioFormat_Float, 16)) {
                        format_return->sampleFormat = WWMFSampleFormatFloat;
                    } else if (0 == memcmp(buff, &MFAudioFormat_PCM, 16)) {
                        format_return->sampleFormat = WWMFSampleFormatInt;
                    } else {
                        printf("unrecognized format guid");
                        goto end;
                    }

                    hr = S_OK;
                    goto end;
                } else {
                    printf("unrecognized format");
                    goto end;
                }
            }

        } else if (0 == memcmp(buff, "data", 4)) {
            // data chunk
            HRG(ReadInt32(fpr, dataBytes_return));
            break;
        } else {
            // skip this chunk
            HRG(ReadInt32(fpr, &chunkBytes));
            if (chunkBytes < 4) {
                printf("E: chunk bytes == %d\n", chunkBytes);
                goto end;
            }
            fseek(fpr, chunkBytes-4, SEEK_CUR);
        }

    }
end:
    if (S_OK == hr && format_return->sampleFormat == WWMFSampleFormatUnknown) {
        printf("unrecognized format");
        hr = E_FAIL;
    }

    return hr;
}

static HRESULT
WriteWavHeader(FILE *fpw, WWMFPcmFormat &format, int dataBytes)
{
    HRESULT hr = E_FAIL;
    int dataChunkSize = (dataBytes + 4 + 1) & (~1);

    HRG(WriteStr(fpw, "RIFF", 4));
    HRG(WriteInt32(fpw, dataChunkSize + 0x24));
    HRG(WriteStr(fpw, "WAVE", 4));

    HRG(WriteStr(fpw, "fmt ", 4));
    HRG(WriteInt32(fpw, 16));

    // fmt audioFormat size==2 1==int 3==float
    switch (format.sampleFormat) {
    case WWMFSampleFormatInt:
        HRG(WriteInt16(fpw, 1));
        break;
    case WWMFSampleFormatFloat:
        HRG(WriteInt16(fpw, 3));
        break;
    default:
        goto end;
    }

    // fmt numChannels size==2
    HRG(WriteInt16(fpw, format.nChannels));

    // fmt sampleRate size==4
    HRG(WriteInt32(fpw, format.sampleRate));

    // fmt byteRate size==4
    HRG(WriteInt32(fpw, format.sampleRate * format.nChannels * format.bits / 8));

    // fmt blockAlign size==2
    HRG(WriteInt16(fpw, (short)format.FrameBytes()));

    // fmt bitspersample size==2
    HRG(WriteInt16(fpw, format.bits));

    HRG(WriteStr(fpw, "data", 4));
    HRG(WriteInt32(fpw, dataChunkSize));

end:
    return hr;
}

static HRESULT
FixWavHeader(FILE *fpw, int writeDataTotalBytes)
{
    HRESULT hr = E_FAIL;
    int dataChunkSize = (writeDataTotalBytes + 4 + 1) & (~1);

    fseek(fpw, 4, SEEK_SET);
    HRG(WriteInt32(fpw, dataChunkSize + 0x24));

    fseek(fpw, 0x28, SEEK_SET);
    HRG(WriteInt32(fpw, dataChunkSize));

end:
    return hr;
}

static void
PrintUsage(const wchar_t *name)
{
    printf(
            "Usage: %S inputWavFile outputWavFile outputSampleRate outputBitdepth conversionQuality\n"
            "outputBitDepth: 16, 24 or 32. If 32 is specified, output format becomes float, otherwise int.\n"
            "conversionQuality: 1 to 60. 1 is worst quality. 60 is best quality.", name);
}

int wmain(int argc, wchar_t *argv[])
{
    // _CrtSetBreakAlloc(35);
    // COM leak cannot be detected by debug heap manager ...
    _CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);

    HRESULT hr = S_OK;
    bool bCoInitialize = false;
    FILE *fpr = NULL;
    FILE *fpw = NULL;
    errno_t ercd;
    BYTE *buff = NULL;
    int buffBytes = 0;
    int readBytes = 0;
    int remainBytes = 0;
    int outputDataBytes = 0;
    int result = 0;
    DWORD writeBytes = 0;
    int writeDataTotalBytes = 0;
    int conversionQuality = 60;
    WWMFResampler resampler;
    WWMFPcmFormat inputFormat;
    WWMFPcmFormat outputFormat;
    WWMFSampleData sampleData;

    if (argc != 6) {
        PrintUsage(argv[0]);
        return 1;
    }

    HRG(CoInitializeEx(NULL, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE));
    bCoInitialize = true;

    ercd = _wfopen_s(&fpr, argv[1], L"rb");
    if (0 != ercd) {
        PrintUsage(argv[0]);
        hr = E_FAIL;
        goto end;
    }

    ercd = _wfopen_s(&fpw, argv[2], L"wb");
    if (0 != ercd) {
        PrintUsage(argv[0]);
        hr = E_FAIL;
        goto end;
    }

    HRG(ReadWavHeader(fpr, &inputFormat, &remainBytes));

    outputFormat = inputFormat;
    outputFormat.sampleRate = _wtoi(argv[3]);
    outputFormat.bits = (short)_wtoi(argv[4]);

    conversionQuality = _wtoi(argv[5]);

    if (0 == outputFormat.sampleRate ||
        0 == conversionQuality) {
        PrintUsage(argv[0]);
        hr = E_FAIL;
        goto end;
    }

    outputFormat.validBitsPerSample = outputFormat.bits;

    switch (outputFormat.bits) {
    case 16:
    case 24:
        outputFormat.sampleFormat = WWMFSampleFormatInt;
        break;
    case 32:
        outputFormat.sampleFormat = WWMFSampleFormatFloat;
        break;
    default:
        PrintUsage(argv[0]);
        hr = E_FAIL;
        goto end;
    }

    outputDataBytes = (int64_t)remainBytes
        * (outputFormat.Bitrate()/8)
        / (inputFormat .Bitrate()/8);

    HRG(WriteWavHeader(fpw, outputFormat, outputDataBytes));

    HRG(resampler.Initialize(inputFormat, outputFormat, conversionQuality));

    buffBytes = 1024 * 1024 * inputFormat.nChannels * inputFormat.bits / 8;
    buff = new BYTE[buffBytes];

    for (;;) {
        // ファイルからPCMデータを読み込む。
        readBytes = buffBytes;
        if (remainBytes < readBytes) {
            readBytes = remainBytes;
        }
        remainBytes -= readBytes;

        result = fread(buff, 1, readBytes, fpr);
        if (result != readBytes) {
            printf("file read error\n");
            hr = E_FAIL;
            goto end;
        }

        // 変換する
        HRG(resampler.Resample(buff, readBytes, &sampleData));

        // 書き込む。
        writeBytes = fwrite(sampleData.data, 1, sampleData.bytes, fpw);
        if (writeBytes != sampleData.bytes) {
            printf("file write error\n");
            hr = E_FAIL;
            goto end;
        }
        writeDataTotalBytes += sampleData.bytes;
        sampleData.Release();

        if (remainBytes == 0) {
            // 最後。
            HRG(resampler.Drain(buffBytes, &sampleData));

            // 書き込む。
            writeBytes = fwrite(sampleData.data, 1, sampleData.bytes, fpw);
            if (writeBytes != sampleData.bytes) {
                printf("file write error\n");
                hr = E_FAIL;
                goto end;
            }
            writeDataTotalBytes += sampleData.bytes;
            sampleData.Release();
            break;
        }
    }

    // data chunk align is 2 bytes
    if (writeDataTotalBytes & 1) {
        if (0 != fputc(0, fpw)) {
            printf("file write error\n");
            hr = E_FAIL;
            goto end;
        }
        ++writeDataTotalBytes;
    }
    HRG(FixWavHeader(fpw, writeDataTotalBytes));

    hr = S_OK;

end:
    resampler.Finalize();

    if (bCoInitialize) {
        CoUninitialize();
        bCoInitialize = false;
    }

    delete[] buff;
    buff = NULL;

    if (fpw != NULL) {
        fclose(fpw);
        fpw = NULL;
    }
    if (fpr != NULL) {
        fclose(fpr);
        fpr = NULL;
    }

    return SUCCEEDED(hr) ? 0 : 1;
}

