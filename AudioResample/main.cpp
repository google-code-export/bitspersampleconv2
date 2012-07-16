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

#pragma comment(lib, "mfplat")
#pragma comment(lib, "mf")
#pragma comment(lib, "mfreadwrite")
#pragma comment(lib, "mfuuid")
#pragma comment(lib, "Shlwapi")

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
            sURL, MF_RESOLUTION_MEDIASOURCE, NULL, &ObjectType, &pSource));
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

/**
 * clsidTransformのTransformを作成し、
 * SourceNode → TransformNode → OutputNode という接続を作る。
 */
static HRESULT
ConnectSourceToOutput(
        IMFTopology     *pTopology,
        IMFTopologyNode *pSourceNode,
        IMFTopologyNode *pOutputNode,
        GUID            clsidTransform)
{
    HRESULT hr = S_OK;
    IMFTopologyNode *pTransformNode = NULL;
    IUnknown *pTransformUnk = NULL;
    assert(pTopology);
    assert(pSourceNode);
    assert(pOutputNode);

    if (clsidTransform == GUID_NULL) {
        hr = pSourceNode->ConnectOutput(0, pOutputNode, 0);
        goto end;
    }

    HRG(MFCreateTopologyNode(MF_TOPOLOGY_TRANSFORM_NODE, &pTransformNode));

    HRG(CoCreateInstance(clsidTransform, NULL, CLSCTX_INPROC_SERVER,
            IID_IUnknown, (void**)&pTransformUnk));
    HRG(pTransformNode->SetObject(pTransformUnk));
    HRG(pTopology->AddNode(pTransformNode));
    HRG(pSourceNode->ConnectOutput(0, pTransformNode, 0));
    HRG(pTransformNode->ConnectOutput(0, pOutputNode, 0));

end:
    SafeRelease(&pTransformNode);
    SafeRelease(&pTransformUnk);
    return hr;
}

class SampleGrabberCB : public IMFSampleGrabberSinkCallback 
{
    long m_cRef;

    SampleGrabberCB() : m_cRef(1) {}

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
        // Display information about the sample.
        printf("Sample: start = %I64d, duration = %I64d, bytes = %d\n", llSampleTime, llSampleDuration, dwSampleSize); 
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
CreateSampleGrabberTopologyNode(IMFTopologyNode **ppTopologyNode)
{
    HRESULT hr = S_OK;
    IMFMediaType *pMediaType = NULL;
    IMFTopologyNode *pOutputNode = NULL;
    SampleGrabberCB *pCallback = NULL;
    IMFActivate *pActivate = NULL;
    assert(ppTopologyNode);
    *ppTopologyNode = NULL;

    HRG(MFCreateMediaType(&pMediaType));
    HRG(pMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio));
    HRG(pMediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM));
    HRG(SampleGrabberCB::CreateInstance(&pCallback));
    HRG(MFCreateSampleGrabberSinkActivate(pMediaType, pCallback, &pActivate));

    HRG(MFCreateTopologyNode(MF_TOPOLOGY_OUTPUT_NODE, &pOutputNode));
    HRG(pActivate->SetUINT32(MF_SAMPLEGRABBERSINK_IGNORE_CLOCK, TRUE));
    HRG(MFCreateTopologyNode(MF_TOPOLOGY_OUTPUT_NODE, &pOutputNode));
    HRG(pOutputNode->SetObject(pActivate));

    *ppTopologyNode = pOutputNode;
    pOutputNode = NULL;

end:
    SafeRelease(&pMediaType);
    SafeRelease(&pOutputNode);
    SafeRelease(&pCallback);
    SafeRelease(&pActivate);
    return hr;
}

#if 0
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
#endif /* 0 */

static HRESULT
AddSinkToPartialTopology(
        IMFTopology *pTopology,
        IMFMediaSource *pSource,
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

    HRG(ConnectSourceToOutput(pTopology, pSourceNode, pSinkNode, GUID_NULL));

end:
    SafeRelease(&pSourceSD);
    SafeRelease(&pSourceNode);
    return hr;
}

/** pSource、pSinkNodeからIMFTopologyを作成する。
 * @param ppTopology [out]作成したTopologyが戻る。失敗のときはNULLが入る。
 */
static HRESULT
CreateTopology(IMFMediaSource *pSource, IMFTopologyNode *pSinkNode, IMFTopology **ppTopology)
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
    HRG(AddSinkToPartialTopology(pTopology, pSource, pSinkNode, pSourcePD, 0));

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

/** Topology end of presentation eventの処理。
 */
static HRESULT
OnEndOfPresentation(IMFMediaSession *pSession)
{
    dprintf("D: session close!\n");
    return pSession->Close();
}

static HRESULT
Test(void)
{
    HRESULT hr = S_OK;
    IMFMediaSource * pSource = NULL;
    IMFMediaSession *pSession = NULL;
    IMFTopology     *pTopology = NULL;
    IMFMediaEvent   *pMediaEvent = NULL;
    IMFTopologyNode *pSinkNode = NULL;
    HRESULT         hrStatus = S_OK;
    MediaEventType  meType;
    bool            bDone = false;

    HRG(MFCreateMediaSession(NULL, &pSession));
    HRG(CreateMediaSourceFromURL(L"C:/tmp/a.wav", &pSource));
    //HRG(CreateSARTopologyNode(&pSinkNode));
    HRG(CreateSampleGrabberTopologyNode(&pSinkNode));
    HRG(CreateTopology(pSource, pSinkNode, &pTopology));
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
            // Sessionは自動的に停止状態になり、ここに来る。
            HRG(OnEndOfPresentation(pSession));
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
    SafeRelease(&pSinkNode);
    SafeRelease(&pMediaEvent);
    SafeRelease(&pTopology);
    SafeRelease(&pSource);
    SafeRelease(&pSession);
    return hr;
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WWResampler_test(void)
{
    HRESULT hr = S_OK;
    IMFSourceReader *pReader = NULL;
    HANDLE hFile = INVALID_HANDLE_VALUE;

    HRG(MFStartup(MF_VERSION, MFSTARTUP_NOSOCKET));
    HRG(MFCreateSourceReaderFromURL(L"C:/tmp/test.wav", NULL, &pReader));
    hFile = CreateFile(L"C:/tmp/output.wav", GENERIC_WRITE, FILE_SHARE_READ, NULL, CREATE_ALWAYS, 0, NULL);
    if (hFile == INVALID_HANDLE_VALUE) {
        printf("could not open output file");
        goto end;
    }

    HRG(Test());

end:
    if (hFile != INVALID_HANDLE_VALUE) {
        CloseHandle(hFile);
        hFile = INVALID_HANDLE_VALUE;
    }
    SafeRelease(&pReader);
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

