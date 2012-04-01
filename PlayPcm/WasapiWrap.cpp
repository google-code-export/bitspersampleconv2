#include "WasapiWrap.h"
#include "WWUtil.h"
#include <assert.h>
#include <functiondiscoverykeys.h>
#include <strsafe.h>

#define FOOTER_SEND_PACKET_NUM (2)

static void
WaveFormatDebug(WAVEFORMATEX *v)
{
    printf(
        "  cbSize=%d\n"
        "  nAvgBytesPerSec=%d\n"
        "  nBlockAlign=%d\n"
        "  nChannels=%d\n"
        "  nSamplesPerSec=%d\n"
        "  wBitsPerSample=%d\n"
        "  wFormatTag=0x%x\n",
        v->cbSize,
        v->nAvgBytesPerSec,
        v->nBlockAlign,
        v->nChannels,
        v->nSamplesPerSec,
        v->wBitsPerSample,
        v->wFormatTag);
}

static void
WFEXDebug(WAVEFORMATEXTENSIBLE *v)
{
    printf(
        "  Samples.wValidBitsPerSample=%d\n"
        "  dwChannelMask=0x%x\n"
        "  SubFormat=0x%x\n",
        v->Samples.wValidBitsPerSample,
        v->dwChannelMask,
        v->SubFormat);
}

WWDeviceInfo::WWDeviceInfo(int id, const wchar_t * name)
{
    this->id = id;
    wcsncpy_s(this->name, _countof(this->name), name, _TRUNCATE);
}

void
WWPcmData::Term(void)
{
    free(stream);
    stream = NULL;
}

WWPcmData::~WWPcmData(void)
{
    assert(!stream);
}

///////////////////////////////////////////////////////////////////////
// WasapiWrap class

WasapiWrap::WasapiWrap(void)
{
    m_deviceCollection = NULL;
    m_deviceToUse      = NULL;
    m_shutdownEvent    = NULL;
    m_audioSamplesReadyEvent = NULL;
    m_audioClient      = NULL;
    m_frameBytes       = 0;
    m_bufferFrameNum    = 0;
    m_renderClient     = NULL;
    m_renderThread     = NULL;
    m_pcmData          = NULL;
    m_mutex            = NULL;
    m_footerCount      = 0;
    m_coInitializeSuccess = false;
}


WasapiWrap::~WasapiWrap(void)
{
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);
}


HRESULT
WasapiWrap::Init(void)
{
    HRESULT hr = S_OK;
    
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);

    hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    if (S_OK == hr) {
        m_coInitializeSuccess = true;
    } else {
        printf("E: WasapiWrap::Init() CoInitializeEx() failed %08x\n", hr);
        hr = S_OK;
    }

    assert(!m_mutex);
    m_mutex = CreateMutex(
        NULL, FALSE, NULL);

    return hr;
}

void
WasapiWrap::Term(void)
{
    SafeRelease(&m_deviceCollection);

    assert(!m_deviceToUse);

    if (m_mutex) {
        CloseHandle(m_mutex);
        m_mutex = NULL;
    }

    if (m_coInitializeSuccess) {
        CoUninitialize();
    }
}


static HRESULT
DeviceNameGet(
    IMMDeviceCollection *dc, UINT id, wchar_t *name, size_t nameBytes)
{
    HRESULT hr = 0;

    IMMDevice *device  = NULL;
    LPWSTR deviceId    = NULL;
    IPropertyStore *ps = NULL;
    PROPVARIANT pv;

    assert(dc);
    assert(name);

    name[0] = 0;

    assert(0 < nameBytes);

    PropVariantInit(&pv);

    HRR(dc->Item(id, &device));
    HRR(device->GetId(&deviceId));
    HRR(device->OpenPropertyStore(STGM_READ, &ps));

    HRG(ps->GetValue(PKEY_Device_FriendlyName, &pv));
    SafeRelease(&ps);

    wcsncpy_s(name, nameBytes/sizeof name[0], pv.pwszVal, _TRUNCATE);

end:
    PropVariantClear(&pv);
    CoTaskMemFree(deviceId);
    SafeRelease(&ps);
    return hr;
}

HRESULT
WasapiWrap::DoDeviceEnumeration(void)
{
    HRESULT hr = 0;
    IMMDeviceEnumerator *deviceEnumerator = NULL;

    m_deviceInfo.clear();

    HRR(CoCreateInstance(__uuidof(MMDeviceEnumerator),
        NULL, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&deviceEnumerator)));
    
    HRR(deviceEnumerator->EnumAudioEndpoints(
        eRender, DEVICE_STATE_ACTIVE, &m_deviceCollection));

    UINT nDevices = 0;
    HRG(m_deviceCollection->GetCount(&nDevices));

    for (UINT i=0; i<nDevices; ++i) {
        wchar_t name[WW_DEVICE_NAME_COUNT];
        HRG(DeviceNameGet(m_deviceCollection, i, name, sizeof name));
        m_deviceInfo.push_back(WWDeviceInfo(i, name));
    }

end:
    SafeRelease(&deviceEnumerator);
    return hr;
}

int
WasapiWrap::GetDeviceCount(void)
{
    assert(m_deviceCollection);
    return (int)m_deviceInfo.size();
}

bool
WasapiWrap::GetDeviceName(int id, LPWSTR name, size_t nameBytes)
{
    assert(name);
    memset(name, 0, nameBytes);
    if (id < 0 || m_deviceInfo.size() <= (unsigned int)id) {
        assert(0);
        return false;
    }
    wcsncpy_s(name, nameBytes/sizeof name[0], m_deviceInfo[id].name, _TRUNCATE);
    return true;
}

HRESULT
WasapiWrap::ChooseDevice(int id)
{
    HRESULT hr = 0;

    if (id < 0) {
        goto end;
    }

    assert(m_deviceCollection);
    assert(!m_deviceToUse);

    HRG(m_deviceCollection->Item(id, &m_deviceToUse));

end:
    SafeRelease(&m_deviceCollection);
    return hr;
}

HRESULT
WasapiWrap::Setup(const WWSetupArg & arg)
{
    HRESULT hr = 0;
    WAVEFORMATEX *waveFormat = NULL;

    m_sampleRate = arg.nSamplesPerSec;
    m_dataBitsPerSample = arg.bitsPerSample;
    m_deviceBitsPerSample = m_dataBitsPerSample;
    if (24 == m_deviceBitsPerSample) {
        m_deviceBitsPerSample = 32;
    }

    m_audioSamplesReadyEvent =
        CreateEventEx(NULL, NULL, 0, EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_audioSamplesReadyEvent);

    assert(m_deviceToUse);
    assert(!m_audioClient);
    HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL, (void**)&m_audioClient));

    assert(!waveFormat);
    HRG(m_audioClient->GetMixFormat(&waveFormat));
    assert(waveFormat);

    WAVEFORMATEXTENSIBLE * wfex = (WAVEFORMATEXTENSIBLE*)waveFormat;

    printf("original Mix Format:\n");
    WaveFormatDebug(waveFormat);
    WFEXDebug(wfex);

    if (waveFormat->wFormatTag != WAVE_FORMAT_EXTENSIBLE) {
        printf("E: unsupported device ! mixformat == 0x%08x\n", waveFormat->wFormatTag);
        hr = E_FAIL;
        goto end;
    }

    wfex->SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
    wfex->Format.wBitsPerSample = m_deviceBitsPerSample;
    wfex->Format.nSamplesPerSec = arg.nSamplesPerSec;

    wfex->Format.nBlockAlign = (m_deviceBitsPerSample / 8) * waveFormat->nChannels;
    wfex->Format.nAvgBytesPerSec = wfex->Format.nSamplesPerSec*wfex->Format.nBlockAlign;
    wfex->Samples.wValidBitsPerSample = m_deviceBitsPerSample;

    printf("preferred Format:\n");
    WaveFormatDebug(waveFormat);
    WFEXDebug(wfex);
    
    HRG(m_audioClient->IsFormatSupported(AUDCLNT_SHAREMODE_EXCLUSIVE,waveFormat,NULL));

    m_frameBytes = waveFormat->nBlockAlign;
    
    REFERENCE_TIME bufferDuration = arg.latencyInMillisec*10000;

    hr = m_audioClient->Initialize(
        AUDCLNT_SHAREMODE_EXCLUSIVE,
        AUDCLNT_STREAMFLAGS_EVENTCALLBACK , 
        bufferDuration, bufferDuration, waveFormat, NULL);
    if (hr == AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED) {
        HRG(m_audioClient->GetBufferSize(&m_bufferFrameNum));

        SafeRelease(&m_audioClient);

        bufferDuration = (REFERENCE_TIME)(
            10000.0 *                         // (REFERENCE_TIME(100ns) / ms) *
            1000 *                            // (ms / s) *
            m_bufferFrameNum /                 // frames /
            waveFormat->nSamplesPerSec +     // (frames / s)
            0.5);

        HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL, (void**)&m_audioClient));

        hr = m_audioClient->Initialize(
            AUDCLNT_SHAREMODE_EXCLUSIVE, 
            AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_NOPERSIST | AUDCLNT_STREAMFLAGS_RATEADJUST, 
            bufferDuration, 
            bufferDuration, 
            waveFormat, 
            NULL);
    }
    if (FAILED(hr)) {
        printf("E: audioClient->Initialize failed 0x%08x\n", hr);
        goto end;
    }

    HRG(m_audioClient->GetBufferSize(&m_bufferFrameNum));
    HRG(m_audioClient->SetEventHandle(m_audioSamplesReadyEvent));
    HRG(m_audioClient->GetService(IID_PPV_ARGS(&m_renderClient)));

end:
    if (waveFormat) {
        CoTaskMemFree(waveFormat);
        waveFormat = NULL;
    }

    return hr;
}

void
WasapiWrap::Unsetup(void)
{
    if (m_audioSamplesReadyEvent) {
        CloseHandle(m_audioSamplesReadyEvent);
        m_audioSamplesReadyEvent = NULL;
    }

    SafeRelease(&m_deviceToUse);
    SafeRelease(&m_audioClient);
    SafeRelease(&m_renderClient);
}

void
WasapiWrap::SetOutputData(WWPcmData &pcmData)
{
    m_pcmData = &pcmData;
}

HRESULT
WasapiWrap::Start(void)
{
    BYTE *pData = NULL;
    HRESULT hr = 0;

    assert(m_pcmData);

    assert(!m_shutdownEvent);
    m_shutdownEvent = CreateEventEx(NULL, NULL, 0, EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_shutdownEvent);

    m_renderThread = CreateThread(NULL, 0, RenderEntry, this, 0, NULL);
    assert(m_renderThread);

    assert(m_renderClient);
    HRG(m_renderClient->GetBuffer(m_bufferFrameNum, &pData));

    memset(pData, 0, m_bufferFrameNum * m_frameBytes);

    HRG(m_renderClient->ReleaseBuffer(m_bufferFrameNum, 0));

    m_footerCount = 0;

    assert(m_audioClient);
    HRG(m_audioClient->Start());

end:
    return hr;
}

void
WasapiWrap::Stop(void)
{
    HRESULT hr = 0;

    if (m_shutdownEvent) {
        SetEvent(m_shutdownEvent);
    }

    if (m_renderThread) {
        SetEvent(m_renderThread);
        WaitForSingleObject(m_renderThread, INFINITE);
        CloseHandle(m_renderThread);
        m_renderThread = NULL;
    }

    if (m_shutdownEvent) {
        CloseHandle(m_shutdownEvent);
        m_shutdownEvent = NULL;
    }

    if (m_audioClient) {
        m_audioClient->Stop();
    }

    if (m_renderThread) {
        WaitForSingleObject(m_renderThread, INFINITE);

        CloseHandle(m_renderThread);
        m_renderThread = NULL;
    }
}

bool
WasapiWrap::Run(int millisec)
{
    //printf("%s WaitForSingleObject(%p, %d)\n", __FUNCTION__, m_renderThread, millisec);
    DWORD rv = WaitForSingleObject(m_renderThread, millisec);
    if (rv == WAIT_TIMEOUT) {
        Sleep(10);
        //printf(".\n");
        return false;
    }
    printf("%s WaitForSingleObject rv=%08x (ends successfully)\n", __FUNCTION__, rv);
    return true;
}

/////////////////////////////////////////////////////////////////////////////////
// callbacks

DWORD
WasapiWrap::RenderEntry(LPVOID lpThreadParameter)
{
    WasapiWrap* self = (WasapiWrap*)lpThreadParameter;
    return self->RenderMain();
}

int
WasapiWrap::GetTotalFrameNum(void)
{
    if (!m_pcmData) {
        return 0;
    }

    return m_pcmData->nFrames;
}

int
WasapiWrap::GetPosFrame(void)
{
    int result = 0;

    assert(m_mutex);

    WaitForSingleObject(m_mutex, INFINITE);
    if (m_pcmData) {
        result = m_pcmData->posFrame;
    }
    ReleaseMutex(m_mutex);

    return result;
}

bool
WasapiWrap::SetPosFrame(int v)
{
    if (v < 0 || GetTotalFrameNum() <= v) {
        return false;
    }

    assert(m_mutex);

    WaitForSingleObject(m_mutex, INFINITE);
    if (m_pcmData) {
        m_pcmData->posFrame = v;
    }
    ReleaseMutex(m_mutex);

    return true;
}

bool
WasapiWrap::AudioSamplesReadyProc(void)
{
    bool    result     = true;
    BYTE    *pFrames   = NULL;
    BYTE    *pData     = NULL;
    HRESULT hr         = 0;
    int     copyFrames = 0;

    WaitForSingleObject(m_mutex, INFINITE);

    copyFrames = m_bufferFrameNum;
    if (m_pcmData->nFrames < m_pcmData->posFrame + copyFrames) {
        copyFrames = m_pcmData->nFrames - m_pcmData->posFrame;
    }

    if (copyFrames <= 0) {
        copyFrames = 0;
    } else {
        pFrames = (BYTE *)m_pcmData->stream;
        pFrames += m_pcmData->posFrame * m_frameBytes;
    }

    assert(m_renderClient);
    hr = m_renderClient->GetBuffer(m_bufferFrameNum, &pData);
    if (FAILED(hr)) {
        result = false;
        goto end;
    }

    if (0 < copyFrames) {
        CopyMemory(pData, pFrames, copyFrames * m_frameBytes);
    }
    if (0 < m_bufferFrameNum - copyFrames) {
        memset(&pData[copyFrames*m_frameBytes], 0,
            (m_bufferFrameNum - copyFrames)*m_frameBytes);
    }

    hr = m_renderClient->ReleaseBuffer(m_bufferFrameNum, 0);
    if (FAILED(hr)) {
        result = false;
        goto end;
    }

    m_pcmData->posFrame += copyFrames;
    if (m_pcmData->nFrames <= m_pcmData->posFrame) {
        ++m_footerCount;
        if (FOOTER_SEND_PACKET_NUM < m_footerCount) {
            result = false;
        }
    }

end:
    ReleaseMutex(m_mutex);
    return result;
}

DWORD
WasapiWrap::RenderMain(void)
{
    bool stillPlaying = true;
    HANDLE waitArray[2] = {m_shutdownEvent, m_audioSamplesReadyEvent};
    DWORD waitResult;
    HRESULT hr = 0;
    
    HRG(CoInitializeEx(NULL, COINIT_MULTITHREADED));

    while (stillPlaying) {
        waitResult = WaitForMultipleObjects(2, waitArray, FALSE, INFINITE);
        switch (waitResult) {
        case WAIT_OBJECT_0 + 0:     // m_shutdownEvent
            stillPlaying = false;
            break;
        case WAIT_OBJECT_0 + 1:     // m_audioSamplesReadyEvent
            stillPlaying = AudioSamplesReadyProc();
            break;
        }
    }

end:
    CoUninitialize();
    return hr;
}



