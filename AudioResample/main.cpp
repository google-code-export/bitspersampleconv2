#pragma warning(disable:4127)  // Disable warning C4127: conditional expression is constant

#define WINVER _WIN32_WINNT_WIN7

#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <stdio.h>
#include <mferror.h>

template <class T> void SafeRelease(T **ppT)
{
    if (*ppT)
    {
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
    IMFTopologyNode *pNode = NULL;
    HRESULT hr = S_OK;
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

static HRESULT
AddSinkToPartialTopology(
        IMFTopology *pTopology,
        IMFMediaSource *pSource,
        IMFPresentationDescriptor *pSourcePD, 
        DWORD idx)
{
    IMFStreamDescriptor* pSourceSD = NULL;
    IMFTopologyNode* pSourceNode = NULL;
    IMFTopologyNode* pSinkNode = NULL;
    BOOL fSelected = FALSE;
    HRESULT hr = S_OK;

    HRG(pSourcePD->GetStreamDescriptorByIndex(idx, &fSelected, &pSourceSD));

    if (!fSelected) {
        // We don't want to render this stream. Exit now.
        pSourceSD->Release();
        return S_OK;
    }

    HRG(CreateTopologyNodeFromMediaSource(pSource, pSourcePD, pSourceSD, &pSourceNode));
    HRG(pTopology->AddNode(pSourceNode));

    HRG(CreateSARTopologyNode(&pSinkNode));
    HRG(pTopology->AddNode(pSinkNode));

    HRG(ConnectSourceToOutput(pTopology, pSourceNode, pSinkNode, GUID_NULL));

end:
    SafeRelease(&pSourceSD);
    SafeRelease(&pSourceNode);
    SafeRelease(&pSinkNode);
    return hr;
}

/** IMFMediaSourceからIMFTopologyを作成する。
 * @param ppTopology [out]作成したTopologyが戻る。失敗のときはNULLが入る。
 */
static HRESULT
CreateTopologyFromSource(IMFMediaSource *pSource, IMFTopology **ppTopology)
{
    HRESULT hr = S_OK;
    IMFTopology *pTopology = NULL;
    IMFPresentationDescriptor* pSourcePD = NULL;
    DWORD cSourceStreams = 0;
    *ppTopology = NULL;

    HRG(MFCreateTopology(&pTopology));
    HRG(pSource->CreatePresentationDescriptor(&pSourcePD));
    HRG(pSourcePD->GetStreamDescriptorCount(&cSourceStreams));

    for (DWORD i = 0; i < cSourceStreams; ++i) {
        HRG(AddSinkToPartialTopology(pTopology, pSource, pSourcePD, i));
    }

    *ppTopology = pTopology;
    pTopology = NULL; //< end:で消されるのを防止。

end:
    SafeRelease(&pSourcePD);
    SafeRelease(&pTopology);
    return hr;
}

static HRESULT
OnTopoStatus(IMFMediaSession *pSession, IMFMediaEvent *pMediaEvent)
{
    HRESULT hr = S_OK;
    MF_TOPOSTATUS topoStatus = MF_TOPOSTATUS_INVALID;
    PROPVARIANT varStart;

    PropVariantInit(&varStart);

    HRG(pMediaEvent->GetUINT32(MF_EVENT_TOPOLOGY_STATUS, (UINT32*)&topoStatus));
    switch (topoStatus) {
    case MF_TOPOSTATUS_READY:
        // ここで再生を開始する。
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

static HRESULT
OnEndOfPresentation(IMFMediaSession *pSession)
{
    dprintf("D: session close!\n");
    return pSession->Close();
}

static HRESULT
Test(void)
{
    HRESULT hr;
    IMFMediaSource * pSource = NULL;
    IMFMediaSession *pSession = NULL;
    IMFTopology     *pTopology = NULL;
    IMFMediaEvent   *pMediaEvent = NULL;
    HRESULT         hrStatus = S_OK;
    MediaEventType  meType;
    bool            bDone = false;

    HRG(MFCreateMediaSession(NULL, &pSession));
    HRG(CreateMediaSourceFromURL(L"C:/tmp/a.wav", &pSource));
    HRG(CreateTopologyFromSource(pSource, &pTopology));
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
    SafeRelease(&pMediaEvent);
    SafeRelease(&pTopology);
    SafeRelease(&pSource);
    SafeRelease(&pSession);
    return hr;
}

extern "C" __declspec(dllexport)
int __stdcall
WWResampler_test(void)
{
    HRESULT hr;
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
    }

    SafeRelease(&pReader);
    MFShutdown();

    return hr;
}

int wmain(int argc, wchar_t* argv[])
{
    argc;
    argv;

    // Initialize the COM library.
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);

    WWResampler_test();

    CoUninitialize();

    return SUCCEEDED(hr) ? 0 : 1;
};

