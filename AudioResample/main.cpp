#pragma warning(disable:4127)  // Disable warning C4127: conditional expression is constant

#define WINVER _WIN32_WINNT_WIN7

#include <windows.h>
#include <atlbase.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mferror.h>
#include <wmcodecdsp.h>
#include <stdio.h>
#include <assert.h>
#include <stdint.h>
#include <math.h>

#pragma comment(lib, "mfplat")
#pragma comment(lib, "mf")
#pragma comment(lib, "mfuuid")
#pragma comment(lib, "wmcodecdspuuid")

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
        dprintf("E: %s:%d %s failed (%08x)\n",    \
            __FILE__, __LINE__, #x, hr);          \
        goto end;                                 \
    }                                             \
}                                                 \

enum WWAvailableType {
    WWAvailableInput,
    WWAvailableOutput,
};

enum WWSampleFormatType {
    WWSampleFormatInt,
    WWSampleFormatFloat,
};

struct WWMediaFormat {
    WWSampleFormatType sampleFormat;
    int nChannels;
    int bits;
    int sampleRate;
    int dwChannelMask;
    int validBitsPerSample;

    WWMediaFormat(WWSampleFormatType aSampleFormat, WORD aNChannels, WORD aBits, int aSampleRate, int aDwChannelMask, int aValidBitsPerSample) {
        sampleFormat       = aSampleFormat;
        nChannels          = aNChannels;
        bits               = aBits;
        sampleRate         = aSampleRate;
        dwChannelMask      = aDwChannelMask;
        validBitsPerSample = aValidBitsPerSample;
    }
};

static HRESULT
CreateAudioMediaType(WWMediaFormat &fmt, IMFMediaType** ppMediaType)
{
    HRESULT hr;
    IMFMediaType *pMediaType = NULL;
    *ppMediaType = NULL;

    HRG(MFCreateMediaType(&pMediaType) );
    HRG(pMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio));
    HRG(pMediaType->SetGUID(MF_MT_SUBTYPE, (fmt.sampleFormat==WWSampleFormatInt)?MFAudioFormat_PCM:MFAudioFormat_Float));
    HRG(pMediaType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, fmt.nChannels));
    HRG(pMediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, fmt.sampleRate));
    HRG(pMediaType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, fmt.nChannels * fmt.bits/8));
    HRG(pMediaType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, fmt.nChannels * fmt.sampleRate * fmt.bits / 8));
    HRG(pMediaType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, fmt.bits));
    HRG(pMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE));
    if (0 != fmt.dwChannelMask) {
        HRG(pMediaType->SetUINT32(MF_MT_AUDIO_CHANNEL_MASK, fmt.dwChannelMask));
    }
    if (fmt.bits != fmt.validBitsPerSample) {
        HRG(pMediaType->SetUINT32(MF_MT_AUDIO_VALID_BITS_PER_SAMPLE, fmt.validBitsPerSample));
    }

    *ppMediaType = pMediaType;
    pMediaType = NULL;

end:
    SafeRelease(&pMediaType);
    return hr;
}

static HRESULT
CreateResamplerMFT(
        WWMediaFormat &fmtIn,
        WWMediaFormat &fmtOut,
        IMFTransform **ppTransform)
{
    HRESULT hr = S_OK;
    CComPtr<IMFMediaType> spInputType;
    CComPtr<IMFMediaType> spOutputType;
    CComPtr<IUnknown> spTransformUnk;
    IMFTransform *pTransform = NULL;
    IWMResamplerProps *pResamplerProps = NULL;
    assert(ppTransform);
    *ppTransform = NULL;

    HRG(CoCreateInstance(CLSID_CResamplerMediaObject, NULL, CLSCTX_INPROC_SERVER,
            IID_IUnknown, (void**)&spTransformUnk));

    HRG(spTransformUnk->QueryInterface(IID_PPV_ARGS(&pTransform)));

    HRG(CreateAudioMediaType(fmtIn, &spInputType));
    HRG(pTransform->SetInputType(0, spInputType, 0));

    HRG(CreateAudioMediaType(fmtOut, &spOutputType));
    HRG(pTransform->SetOutputType(0, spOutputType, 0));

    HRG(spTransformUnk->QueryInterface(IID_PPV_ARGS(&pResamplerProps)));
    // Resampler max conversion quality == 60
    HRG(pResamplerProps->SetHalfFilterLength(60));

    *ppTransform = pTransform;
    pTransform = NULL; //< prevent release

end:
    SafeRelease(&pResamplerProps);
    SafeRelease(&pTransform);
    return hr;
}

static HRESULT
GenerateFloatSourceSamples(LONGLONG hnsSampleTime, int sampleRate, int nChannels, int cbBytes, IMFSample **ppSample)
{
    HRESULT hr = S_OK;
    IMFSample *pSample = NULL;
    IMFMediaBuffer *pBuffer = NULL;
    BYTE  *pByteBuffer = NULL;
    float *pFloat = NULL;
    float v, step;
    LONGLONG hnsSampleDuration;

    assert(ppSample);
    *ppSample = NULL;
        
    HRG(MFCreateMemoryBuffer(cbBytes, &pBuffer));
    HRG(pBuffer->Lock(&pByteBuffer, NULL, NULL));
    pFloat = (float *)pByteBuffer;

    v = 0;
    step = 1000 * 2.0f * WW_PIF / sampleRate;
    for (int i=0; i<cbBytes/4/nChannels; ++i) {
        for (int ch=0; ch<nChannels; ++ch) {
            pFloat[i*nChannels + ch] = sinf(v);
        }
        v += step;
        while (2.0f * WW_PIF <= v) {
            v -= 2.0f * WW_PIF;
        }
    }
    
    pByteBuffer = NULL;
    pFloat = NULL;
    HRG(pBuffer->Unlock());
    HRG(pBuffer->SetCurrentLength(cbBytes));

    HRG(MFCreateSample(&pSample));
    HRG(pSample->AddBuffer(pBuffer));

    hnsSampleDuration = (10LL * 1000 * 1000 / nChannels / sizeof(float)) * cbBytes / sampleRate;

    HRG(pSample->SetSampleDuration(hnsSampleDuration));
    HRG(pSample->SetSampleTime(hnsSampleTime));

    *ppSample = pSample;
    pSample = NULL;

end:
    SafeRelease(&pBuffer);
    SafeRelease(&pSample);
    return hr;
}

static HRESULT
DumpOutputSamples(IMFSample *pSample)
{
    HRESULT hr = S_OK;
    IMFMediaBuffer *pBuffer = NULL;
    BYTE  *pByteBuffer = NULL;
    float *pFloat = NULL;
    assert(pSample);
    DWORD cbBytes = 0;

    HRG(pSample->ConvertToContiguousBuffer(&pBuffer));
    HRG(pBuffer->GetCurrentLength(&cbBytes));
    HRG(pBuffer->Lock(&pByteBuffer, NULL, NULL));
    pFloat = (float *)pByteBuffer;
    for (DWORD i=0; i<cbBytes/4; ++i) {
        dprintf("%u %f\n", i, pFloat[i]);
    }
    
    pByteBuffer = NULL;
    pFloat = NULL;
    HRG(pBuffer->Unlock());

end:
    SafeRelease(&pBuffer);
    return hr;
}

static HRESULT
ApplyTransform(IMFTransform *pTransform, IMFSample *pInputSample,
        DWORD cbOutSamples, bool bEnd)
{
    HRESULT hr = S_OK;
    IMFMediaBuffer *pBuffer = NULL;
    MFT_OUTPUT_STREAM_INFO StreamInfo;
    MFT_OUTPUT_DATA_BUFFER outputDataBuffer;
    DWORD dwStatus;
    bool bDrained = false;
    memset(&outputDataBuffer, 0, sizeof outputDataBuffer);

    HRG(pTransform->GetInputStatus(0, &dwStatus));
    if ( MFT_INPUT_STATUS_ACCEPT_DATA != dwStatus) {
        dprintf("E: ApplyTransform() pTransform->GetInputStatus() not accept data.\n");
        hr = E_FAIL;
        goto end;
    }
    HRG(pTransform->ProcessMessage(MFT_MESSAGE_COMMAND_FLUSH, NULL));
    HRG(pTransform->ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, NULL));
    HRG(pTransform->ProcessMessage(MFT_MESSAGE_NOTIFY_START_OF_STREAM, NULL));

    HRG(pTransform->ProcessInput(0, pInputSample, 0));

    HRG(pTransform->GetOutputStreamInfo(0, &StreamInfo));
    if (cbOutSamples < StreamInfo.cbSize) {
        cbOutSamples = StreamInfo.cbSize;
    }

    do {
        HRG(MFCreateSample(&(outputDataBuffer.pSample)));
        HRG(MFCreateMemoryBuffer(cbOutSamples, &pBuffer));
        HRG(outputDataBuffer.pSample->AddBuffer(pBuffer));
        outputDataBuffer.dwStreamID = 0;
        outputDataBuffer.dwStatus = 0;
        outputDataBuffer.pEvents = NULL;

        hr = pTransform->ProcessOutput(0, 1, &outputDataBuffer, &dwStatus);
        if (MF_E_TRANSFORM_NEED_MORE_INPUT == hr) {
            if (bEnd) {
                HRG(pTransform->ProcessMessage(MFT_MESSAGE_NOTIFY_END_OF_STREAM, NULL));
                HRG(pTransform->ProcessMessage(MFT_MESSAGE_NOTIFY_END_STREAMING, NULL));
            }
            hr = S_OK;
            break;
        }
        if (FAILED(hr)) {
            goto end;
        }
        DumpOutputSamples(outputDataBuffer.pSample);
        SafeRelease(&pBuffer);
        SafeRelease(&outputDataBuffer.pSample);
        if (bEnd && !bDrained) {
            HRG(pTransform->ProcessMessage(MFT_MESSAGE_COMMAND_DRAIN, NULL));
            bDrained = true;
        }

    } while (true);

end:
    SafeRelease(&pBuffer);
    SafeRelease(&outputDataBuffer.pSample);
    return hr;
}

static HRESULT
Test(void)
{
    HRESULT hr = S_OK;
    IMFTransform *pTransform = NULL;
    IMFSample    *pInputSample = NULL;
    WWMediaFormat fmtIn( WWSampleFormatFloat, 1, 32, 44100, 0, 32);
    WWMediaFormat fmtOut(WWSampleFormatFloat, 1, 32, 48000, 0, 32);

    HRG(MFStartup(MF_VERSION, MFSTARTUP_NOSOCKET));
    HRG(CreateResamplerMFT(fmtIn, fmtOut, &pTransform));
    HRG(GenerateFloatSourceSamples(0LL, 44100, 1, 1 * 44100*4*1, &pInputSample));
    HRG(ApplyTransform(pTransform, pInputSample, 1 * 48000*4*1, true));

end:
    SafeRelease(&pInputSample);
    SafeRelease(&pTransform);
    MFShutdown();
    return hr;
}

int wmain(void)
{
    HRESULT hr = S_OK;
    bool bCoInitialize = false;

    HRG(CoInitializeEx(NULL, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE));
    bCoInitialize = true;

    HRG(Test());

end:
    if (bCoInitialize) {
        CoUninitialize();
        bCoInitialize = false;
    }
    return SUCCEEDED(hr) ? 0 : 1;
}

