// 日本語UTF-8
#pragma warning(disable:4127)  // Disable warning C4127: conditional expression is constant

#define WINVER _WIN32_WINNT_WIN7

#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <stdio.h>
#include <mferror.h>
#include <assert.h>
#include <new>
#include <Shlwapi.h>
#include <wmcodecdsp.h>
#include <stdint.h>

#pragma comment(lib, "mfplat")
#pragma comment(lib, "mf")
#pragma comment(lib, "mfreadwrite")
#pragma comment(lib, "mfuuid")
#pragma comment(lib, "Shlwapi")
#pragma comment(lib, "wmcodecdspuuid")

// スピーカーから音が出る
// #define WW_USE_SAR

// Sample grabber sinkのテスト
#define WW_USE_SAMPLE_GRABBER

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

/** ファイルパスからメディアソースを作成する。
 * @param sURL ファイルパス
 * @param ppSource [out] 作成されたメディアソースが戻る。失敗のときはNULLが入る。
 */
static HRESULT
CreateMediaSourceFromURL(const WCHAR *sURL, IMFMediaSource **ppSource)
{
    HRESULT hr = S_OK;
    IMFSourceResolver* pSourceResolver = NULL;
    IUnknown* pSource = NULL;
    MF_OBJECT_TYPE ObjectType = MF_OBJECT_INVALID;
    assert(sURL);
    assert(ppSource);
    *ppSource = NULL;

    HRG(MFCreateSourceResolver(&pSourceResolver));
    HRG(pSourceResolver->CreateObjectFromURL(
            sURL,
            MF_RESOLUTION_MEDIASOURCE | MF_RESOLUTION_CONTENT_DOES_NOT_HAVE_TO_MATCH_EXTENSION_OR_MIME_TYPE,
            NULL, &ObjectType, &pSource));
    HRG(pSource->QueryInterface(IID_PPV_ARGS(ppSource)));

end:
    SafeRelease(&pSource);
    SafeRelease(&pSourceResolver);
    return hr;
}

/** pSourceがソースのSource Node IMFTopologyNodeを作成する。
 * @param pSource このノードのメディアソース。
 * @param ppNode [out] IMFTopologyNodeが戻る。失敗の時NULLが入る。
 */
static HRESULT
CreateTopologyNodeFromMediaSource(
        IMFMediaSource *pSource,
        IMFPresentationDescriptor *pSourcePD, 
        IMFStreamDescriptor *pSourceSD,
        IMFTopologyNode **ppNode)
{
    HRESULT hr = S_OK;
    IMFTopologyNode *pNode = NULL;
    assert(pSource);
    assert(pSourcePD);
    assert(pSourceSD);
    assert(ppNode);
    *ppNode = NULL;

    HRG(MFCreateTopologyNode(MF_TOPOLOGY_SOURCESTREAM_NODE, &pNode));
    HRG(pNode->SetUnknown(MF_TOPONODE_SOURCE, pSource));
    HRG(pNode->SetUnknown(MF_TOPONODE_PRESENTATION_DESCRIPTOR, pSourcePD));
    HRG(pNode->SetUnknown(MF_TOPONODE_STREAM_DESCRIPTOR, pSourceSD));

    *ppNode = pNode;
    pNode = NULL; //< end:でReleaseされるのを防ぐ。

end:
    SafeRelease(&pNode);
    return hr;
}

#if 1
/** Resampler MFTを作成する
 */
static HRESULT
CreateResamplerMFT(IMFMediaType *pOutputMediaType, IMFTopologyNode **ppTransformNode)
{
    HRESULT hr = S_OK;
    IMFTopologyNode *pTransformNode = NULL;
    IUnknown *pTransformUnk = NULL;
    IWMResamplerProps *pResamplerProps = NULL;
    assert(ppTransformNode);
    *ppTransformNode = NULL;

    HRG(MFCreateTopologyNode(MF_TOPOLOGY_TRANSFORM_NODE, &pTransformNode));
    HRG(CoCreateInstance(CLSID_CResamplerMediaObject, NULL, CLSCTX_INPROC_SERVER,
            IID_IUnknown, (void**)&pTransformUnk));

    // 効かない
    HRG(pTransformNode->SetOutputPrefType(0, pOutputMediaType));

    HRG(pTransformNode->SetObject(pTransformUnk));
    HRG(pTransformUnk->QueryInterface(IID_PPV_ARGS(&pResamplerProps)));
    
    *ppTransformNode = pTransformNode;
    pTransformNode = NULL; //< 消されるの防止。

end:
    SafeRelease(&pTransformUnk);
    SafeRelease(&pResamplerProps);
    SafeRelease(&pTransformNode);
    return hr;
}
#endif

#ifdef WW_USE_SAR
/**
 * SAR(Streaming Audio Renderer) Topology Nodeを作成する。
 * @param ppSARTopologyNode [out] SAR Topology Node。作成失敗の時NULLが入る。
 */
static HRESULT
CreateSARTopologyNode(IMFTopologyNode **ppSARTopologyNode)
{
    HRESULT hr = S_OK;
    IMFActivate *pRendererActivate = NULL;
    IMFTopologyNode *pOutputNode = NULL;
    assert(ppSARTopologyNode);
    *ppSARTopologyNode = NULL;

    HRG(MFCreateTopologyNode(MF_TOPOLOGY_OUTPUT_NODE, &pOutputNode));
    HRG(MFCreateAudioRendererActivate(&pRendererActivate));
    HRG(pOutputNode->SetObject(pRendererActivate));

    *ppSARTopologyNode = pOutputNode;
    pOutputNode = NULL; //< end:で消されるのを防止。

end:
    SafeRelease(&pOutputNode);
    SafeRelease(&pRendererActivate);
    return hr;
}
#endif /* WW_USE_SAR */

#ifdef WW_USE_SAMPLE_GRABBER
class SampleGrabberCB : public IMFSampleGrabberSinkCallback 
{
    long m_cRef;
    int64_t m_accumBytes;

    SampleGrabberCB() : m_cRef(1), m_accumBytes(0) {}

public:
    static HRESULT CreateInstance(SampleGrabberCB **ppCB) {
        *ppCB = new (std::nothrow) SampleGrabberCB();
        if (ppCB == NULL) {
            return E_OUTOFMEMORY;
        }
        return S_OK;
    }

    // IUnknown methods
    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) {
        static const QITAB qit[] = 
        {
            QITABENT(SampleGrabberCB, IMFSampleGrabberSinkCallback),
            QITABENT(SampleGrabberCB, IMFClockStateSink),
            { 0 }
        };
        return QISearch(this, qit, riid, ppv);
    }
    STDMETHODIMP_(ULONG) AddRef(void) {
        return InterlockedIncrement(&m_cRef);
    }
    STDMETHODIMP_(ULONG) Release(void) {
        ULONG cRef = InterlockedDecrement(&m_cRef);
        if (cRef == 0) {
            delete this;
        }
        return cRef;
    }

    // IMFClockStateSink methods
    STDMETHODIMP OnClockStart(MFTIME hnsSystemTime, LONGLONG llClockStartOffset) {
        hnsSystemTime;
        llClockStartOffset;
        return S_OK;
    }
    STDMETHODIMP OnClockStop(MFTIME hnsSystemTime) {
        hnsSystemTime;
        return S_OK;
    }
    STDMETHODIMP OnClockPause(MFTIME hnsSystemTime) {
        hnsSystemTime;
        return S_OK;
    }
    STDMETHODIMP OnClockRestart(MFTIME hnsSystemTime) {
        hnsSystemTime;
        return S_OK;
    }
    STDMETHODIMP OnClockSetRate(MFTIME hnsSystemTime, float flRate) {
        hnsSystemTime;
        flRate;
        return S_OK;
    }

    // IMFSampleGrabberSinkCallback methods
    STDMETHODIMP OnSetPresentationClock(IMFPresentationClock* pClock) {
        pClock;
        return S_OK;
    }
    STDMETHODIMP OnProcessSample(REFGUID guidMajorMediaType, DWORD dwSampleFlags,
        LONGLONG llSampleTime, LONGLONG llSampleDuration, const BYTE * pSampleBuffer,
        DWORD dwSampleSize) {
            pSampleBuffer;
            dwSampleFlags;
            guidMajorMediaType;
        m_accumBytes += dwSampleSize;

        // Display information about the sample.
        printf("Sample: start = %I64d ms, duration = %I64d, bytes = %d acc=%d bytes\n",
            llSampleTime/10000, llSampleDuration, dwSampleSize, m_accumBytes);
        return S_OK;
    }
    STDMETHODIMP OnShutdown(void) {
        return S_OK;
    }
};

/**
 * Sample Grabber Sink Topology nodeを作成する。
 */
static HRESULT
CreateSampleGrabberTopologyNode(IMFMediaType *pMediaType, IMFTopologyNode **ppTopologyNode)
{
    HRESULT hr = S_OK;
    IMFTopologyNode *pOutputNode = NULL;
    SampleGrabberCB *pCallback = NULL;
    IMFActivate *pActivate = NULL;
    assert(ppTopologyNode);
    *ppTopologyNode = NULL;

    HRG(SampleGrabberCB::CreateInstance(&pCallback));
    HRG(MFCreateSampleGrabberSinkActivate(pMediaType, pCallback, &pActivate));

    HRG(MFCreateTopologyNode(MF_TOPOLOGY_OUTPUT_NODE, &pOutputNode));
    HRG(pActivate->SetUINT32(MF_SAMPLEGRABBERSINK_IGNORE_CLOCK, TRUE));
    HRG(pOutputNode->SetObject(pActivate));

    *ppTopologyNode = pOutputNode;
    pOutputNode = NULL;

end:
    SafeRelease(&pOutputNode);
    SafeRelease(&pCallback);
    SafeRelease(&pActivate);
    return hr;
}

#endif /* WW_USE_SAMPLE_GRABBER */

/** Audio Floatフォーマット
 */
static HRESULT
CreateAudioFloatMediaType(IMFMediaType **ppMediaType)
{
    HRESULT hr = S_OK;
    IMFMediaType *pMediaType = NULL;
    *ppMediaType = NULL;

    HRG(MFCreateMediaType(&pMediaType));
    HRG(pMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio));
    HRG(pMediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_Float));

    *ppMediaType = pMediaType;
    pMediaType = NULL;

end:
    SafeRelease(&pMediaType);
    return hr;
}

/** 出力フォーマットを決める
 */
static HRESULT
CreateOutputMediaType(WORD nChannels, WORD bits, int sampleRate, IMFMediaType **ppMediaType)
{
    HRESULT hr = S_OK;
    IMFMediaType *pMediaType = NULL;
    *ppMediaType = NULL;
    WAVEFORMATEX wfex;
    HRG(MFCreateMediaType(&pMediaType));
#if 1
    //HRG(pMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio));
    //HRG(pMediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM));
    memset(&wfex, 0, sizeof wfex);
    wfex.wFormatTag = WAVE_FORMAT_PCM;
    wfex.nChannels = nChannels;
    wfex.nSamplesPerSec = sampleRate;
    wfex.wBitsPerSample = bits;
    wfex.nBlockAlign = (nChannels * bits)/8;
    //wfex.nAvgBytesPerSec = (sampleRate * bits * nChannels) / 8;
    //wfex.cbSize = sizeof wfex;
    HRG(MFInitMediaTypeFromWaveFormatEx(pMediaType, &wfex, sizeof wfex));
#else
    wfex;
    nChannels;
    bits;
    sampleRate;
    HRG(pMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio));
#if 1
    HRG(pMediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM));
#else
    // 駄目だった
    HRG(pMediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_Float));
#endif

    // 駄目だった
    HRG(pMediaType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, nChannels));
    HRG(pMediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, sampleRate));
    HRG(pMediaType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, nChannels * bits/8));
    HRG(pMediaType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, sampleRate * nChannels * bits/8));
    HRG(pMediaType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, bits));
    HRG(pMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, 1));
#endif
    *ppMediaType = pMediaType;
    pMediaType = NULL;

end:
    SafeRelease(&pMediaType);
    return hr;
}

static HRESULT
AddSinkToPartialTopology(
        IMFTopology *pTopology,
        IMFMediaSource *pSource,
        IMFTopologyNode *pTransformNode,
        IMFTopologyNode *pSinkNode,
        IMFPresentationDescriptor *pSourcePD, 
        DWORD idx)
{
    HRESULT hr = S_OK;
    IMFStreamDescriptor* pSourceSD = NULL;
    IMFTopologyNode* pSourceNode = NULL;
    BOOL fSelected = FALSE;
    assert(pTopology);
    assert(pSource);
    assert(pSinkNode);
    assert(pSourcePD);

    HRG(pSourcePD->GetStreamDescriptorByIndex(idx, &fSelected, &pSourceSD));

    if (!fSelected) {
        // We don't want to render this stream. Exit now.
        dprintf("GetStreamDescriptorByIndex fSelected==false.\n");
        pSourceSD->Release();
        return S_OK;
    }

    HRG(CreateTopologyNodeFromMediaSource(pSource, pSourcePD, pSourceSD, &pSourceNode));
    HRG(pTopology->AddNode(pSourceNode));

    HRG(pTopology->AddNode(pSinkNode));
    HRG(pSinkNode->SetUINT32(MF_TOPONODE_STREAMID, idx));
    HRG(pSinkNode->SetUINT32(MF_TOPONODE_NOSHUTDOWN_ON_REMOVE, FALSE));

    if (NULL == pTransformNode) {
        hr = pSourceNode->ConnectOutput(0, pSinkNode, 0);
        goto end;
    }

    HRG(pTopology->AddNode(pTransformNode));
    HRG(pSourceNode->ConnectOutput(0, pTransformNode, 0));
    HRG(pTransformNode->ConnectOutput(0, pSinkNode, 0));

end:
    SafeRelease(&pSourceSD);
    SafeRelease(&pSourceNode);
    return hr;
}

/** pSource、pSinkNodeからIMFTopologyを作成する。
 * @param ppTopology [out]作成したTopologyが戻る。失敗のときはNULLが入る。
 */
static HRESULT
CreateTopology(
        IMFMediaSource *pSource,
        IMFTopologyNode *pTransformNode,
        IMFTopologyNode *pSinkNode,
        IMFTopology **ppTopology)
{
    HRESULT hr = S_OK;
    IMFTopology *pTopology = NULL;
    IMFPresentationDescriptor* pSourcePD = NULL;
    DWORD cSourceStreams = 0;
    *ppTopology = NULL;

    HRG(MFCreateTopology(&pTopology));
    HRG(pSource->CreatePresentationDescriptor(&pSourcePD));
    HRG(pSourcePD->GetStreamDescriptorCount(&cSourceStreams));
    if (cSourceStreams != 1) {
        dprintf("source stream is not 1 !\n");
        hr = E_FAIL;
        goto end;
    }
    HRG(AddSinkToPartialTopology(pTopology, pSource, pTransformNode, pSinkNode, pSourcePD, 0));

    *ppTopology = pTopology;
    pTopology = NULL; //< end:で消されるのを防止。

end:
    SafeRelease(&pSourcePD);
    SafeRelease(&pTopology);
    return hr;
}

/** Topology status eventの処理。
 */
static HRESULT
OnTopoStatus(IMFMediaSession *pSession, IMFMediaEvent *pMediaEvent)
{
    HRESULT hr = S_OK;
    MF_TOPOSTATUS topoStatus = MF_TOPOSTATUS_INVALID;
    PROPVARIANT varStart;

    HRG(pMediaEvent->GetUINT32(MF_EVENT_TOPOLOGY_STATUS, (UINT32*)&topoStatus));
    switch (topoStatus) {
    case MF_TOPOSTATUS_READY:
        // ここで再生を開始する。
        PropVariantInit(&varStart);
        HRG(pSession->Start(&GUID_NULL, &varStart));
        PropVariantClear(&varStart);
        break;
    case MF_TOPOSTATUS_ENDED:
        dprintf("playback completed.\n");
        break;
    default:
        dprintf("topology status=%d\n", topoStatus);
        break;
    }

end:
    return hr;
}

/** Topology session end eventの処理。
 */
static HRESULT
OnSessionEnd(IMFMediaSession *pSession)
{
    dprintf("D: session close!\n");
    return pSession->Close();
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WWResampler_test(void)
{
    HRESULT hr = S_OK;
    IMFMediaSource * pSource = NULL;
    IMFMediaSession *pSession = NULL;
    IMFTopology     *pTopology = NULL;
    IMFMediaEvent   *pMediaEvent = NULL;
    IMFTopologyNode *pTransformNode = NULL;
    IMFTopologyNode *pSinkNode = NULL;
    IMFMediaType    *pAudioMediaType = NULL;
    IMFMediaType    *pOutputMediaType = NULL;
    HRESULT         hrStatus = S_OK;
    MediaEventType  meType;
    bool            bDone = false;

    HRG(MFStartup(MF_VERSION, MFSTARTUP_NOSOCKET));
    HRG(MFCreateMediaSession(NULL, &pSession));
    HRG(CreateMediaSourceFromURL(L"C:/tmp/a.wav", &pSource));

#ifdef WW_USE_SAR
    HRG(CreateSARTopologyNode(&pSinkNode));
#endif /* WW_USE_SAR */
#ifdef WW_USE_SAMPLE_GRABBER
    HRG(CreateAudioFloatMediaType(&pAudioMediaType));
    HRG(CreateOutputMediaType(2, 16, 96000, &pOutputMediaType));
    HRG(CreateSampleGrabberTopologyNode(pAudioMediaType, &pSinkNode));
#endif /* WW_USE_SAMPLE_GRABBER */
    HRG(CreateResamplerMFT(pOutputMediaType, &pTransformNode));
    //HRG(pSinkNode->SetInputPrefType(0, pOutputMediaType));
    HRG(CreateTopology(pSource, pTransformNode, pSinkNode, &pTopology));
    HRG(pSession->SetTopology(0, pTopology));
    while (!bDone) {
        HRG(pSession->GetEvent(0, &pMediaEvent));
        HRG(pMediaEvent->GetStatus(&hrStatus));
        if (FAILED(hrStatus)) {
            dprintf("Session error %08x\n", hrStatus);
            hr = hrStatus;
            goto end;
        }

        HRG(pMediaEvent->GetType(&meType));
        dprintf("%d\n", meType);
        switch (meType) {
        case MESessionTopologyStatus:
            // TopologyがReadyになったら再生開始する。
            HRG(OnTopoStatus(pSession, pMediaEvent));
            break;
        case MESessionStarted:
            dprintf("session started.\n");
            break;
        case MEEndOfPresentation:
            dprintf("end of presentation.\n");
            break;
        case MESessionEnded:
            dprintf("session ended.\n");
            // Sessionは自動的に停止状態になり、ここに来る。
            HRG(OnSessionEnd(pSession));
            break;
        case MESessionClosed:
            dprintf("session closed.\n");
            bDone = true;
            break;
        default:
            dprintf("session event %d. do nothing.\n", meType);
            break;
        }

        SafeRelease(&pMediaEvent);
    }

end:
    // pAudioPcmMediaType
    // pOutputMediaType
    SafeRelease(&pSinkNode);
    SafeRelease(&pTransformNode);
    SafeRelease(&pMediaEvent);
    SafeRelease(&pTopology);
    SafeRelease(&pSource);
    SafeRelease(&pSession);
    MFShutdown();
    return hr;
}

int wmain(int argc, wchar_t* argv[])
{
    HRESULT hr = S_OK;
    bool bCoInitialize = false;
    argc;
    argv;
    
    HRG(CoInitializeEx(NULL, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE));
    bCoInitialize = true;

    HRG(WWResampler_test());

end:
    if (bCoInitialize) {
        CoUninitialize();
        bCoInitialize = false;
    }
    return SUCCEEDED(hr) ? 0 : 1;
};

