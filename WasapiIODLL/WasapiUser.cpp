// 日本語 UTF-8
// WASAPIの機能を使って、音を出したり録音したりするWasapiUserクラス。

#include "WasapiUser.h"
#include "WWUtil.h"
#include <avrt.h>
#include <assert.h>
#include <functiondiscoverykeys.h>
#include <strsafe.h>
#include <mmsystem.h>
#include <malloc.h>
#include <stdint.h>

#define FOOTER_SEND_FRAME_NUM                   (2)
#define PERIODS_PER_BUFFER_ON_TIMER_DRIVEN_MODE (4)

// define: レンダーバッファ上で再生データを作る
// undef : 一旦スタック上にて再生データを作ってからレンダーバッファにコピーする
#define CREATE_PLAYPCM_ON_RENDER_BUFFER

// DoPマーカーが正しく付いているかチェックする。
//#define CHECK_DOP_MARKER

WWDeviceInfo::WWDeviceInfo(int id, const wchar_t * name, const wchar_t * idStr)
{
    this->id = id;
    wcsncpy_s(this->name, name, WW_DEVICE_NAME_COUNT-1);
    wcsncpy_s(this->idStr, idStr, WW_DEVICE_IDSTR_COUNT-1);
}

static wchar_t*
WWSchedulerTaskTypeToStr(WWSchedulerTaskType t)
{
    switch (t) {
    case WWSTTNone: return L"None";
    case WWSTTAudio: return L"Audio";
    case WWSTTProAudio: return L"Pro Audio";
    case WWSTTPlayback: return L"Playback";
    default: assert(0); return L"";
    }
}

///////////////////////////////////////////////////////////////////////
// WasapiUser class

WasapiUser::WasapiUser(void)
{
    m_deviceCollection = NULL;
    m_deviceToUse      = NULL;

    m_shutdownEvent          = NULL;
    m_audioSamplesReadyEvent = NULL;
    m_audioClient            = NULL;
    m_bufferFrameNum         = 0;

    m_sampleFormat  = WWPcmDataSampleFormatUnknown;
    m_sampleRate    = 0;
    m_numChannels   = 0;
    m_dwChannelMask = 0;

    m_deviceSampleFormat  = WWPcmDataSampleFormatUnknown;
    m_deviceSampleRate    = 0;
    m_deviceNumChannels   = 0;
    m_deviceDwChannelMask = 0;
    m_deviceBytesPerFrame = 0;

    m_dataFeedMode      = WWDFMEventDriven;
    m_schedulerTaskType = WWSTTAudio;
    m_shareMode         = AUDCLNT_SHAREMODE_EXCLUSIVE;
    m_latencyMillisec   = 0;

    m_renderClient     = NULL;
    m_captureClient    = NULL;
    m_thread           = NULL;
    m_mutex            = NULL;
    m_coInitializeSuccess = false;
    m_footerNeedSendCount = 0;
    m_dataFlow         = eRender;
    m_glitchCount      = 0;
    m_footerCount      = 0;
    m_useDeviceId      = -1;
    memset(m_useDeviceName, 0, sizeof m_useDeviceName);
    memset(m_useDeviceIdStr, 0, sizeof m_useDeviceIdStr);

    m_captureCallback      = NULL;
    m_stateChangedCallback = NULL;
    m_deviceEnumerator     = NULL;
    m_pNotificationClient  = NULL;
}

WasapiUser::~WasapiUser(void)
{
    assert(!m_pNotificationClient);
    assert(!m_deviceEnumerator);
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);
    m_useDeviceId = -1;
    m_useDeviceName[0] = 0;
    m_useDeviceIdStr[0] = 0;
}

HRESULT
WasapiUser::Init(void)
{
    HRESULT hr = S_OK;
    
    dprintf("D: %s()\n", __FUNCTION__);

    assert(!m_pNotificationClient);
    assert(!m_deviceEnumerator);
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);

    hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    if (S_OK == hr) {
        m_coInitializeSuccess = true;
    } else {
        dprintf("E: WasapiUser::Init() CoInitializeEx() failed %08x\n", hr);
        hr = S_OK;
    }

    assert(!m_mutex);
    m_mutex = CreateMutex(NULL, FALSE, NULL);

    m_pNotificationClient = new WWMMNotificationClient(this);

    return hr;
}

void
WasapiUser::Term(void)
{
    dprintf("D: %s() m_deviceCollection=%p m_deviceToUse=%p m_mutex=%p\n", __FUNCTION__, m_deviceCollection, m_deviceToUse, m_mutex);

    if (m_deviceEnumerator && m_pNotificationClient) {
        m_deviceEnumerator->UnregisterEndpointNotificationCallback(m_pNotificationClient);
    }

    m_captureCallback      = NULL;
    m_stateChangedCallback = NULL;

    SafeRelease(&m_deviceCollection);
    SafeRelease(&m_deviceEnumerator);
    SAFE_DELETE(m_pNotificationClient);
    SafeRelease(&m_deviceToUse);
    m_useDeviceId = -1;
    m_useDeviceName[0] = 0;
    m_useDeviceIdStr[0] = 0;

    if (m_mutex) {
        CloseHandle(m_mutex);
        m_mutex = NULL;
    }

    if (m_coInitializeSuccess) {
        CoUninitialize();
    }
}

void
WasapiUser::SetSchedulerTaskType(WWSchedulerTaskType t)
{
    assert(0 <= t&& t <= WWSTTPlayback);

    dprintf("D: %s() t=%d\n", __FUNCTION__, (int)t);

    m_schedulerTaskType = t;
}

void
WasapiUser::SetShareMode(WWShareMode sm)
{
    dprintf("D: %s() sm=%d\n", __FUNCTION__, (int)sm);

    switch (sm) {
    case WWSMShared:
        m_shareMode = AUDCLNT_SHAREMODE_SHARED;
        break;
    case WWSMExclusive:
        m_shareMode = AUDCLNT_SHAREMODE_EXCLUSIVE;
        break;
    default:
        assert(0);
        break;
    }
}

void
WasapiUser::SetDataFeedMode(WWDataFeedMode mode)
{
    assert(0 <= mode && mode < WWDFMNum);

    dprintf("D: %s() mode=%d\n", __FUNCTION__, (int)mode);

    m_dataFeedMode = mode;
}

void
WasapiUser::SetLatencyMillisec(DWORD millisec)
{
    dprintf("D: %s() latencyMillisec=%u\n", __FUNCTION__, millisec);

    m_latencyMillisec = millisec;
}

void
WasapiUser::SetStreamType(WWStreamType t)
{
    m_pcmStream.SetStreamType(t);
}

WWStreamType WasapiUser::StreamType(void) const
{
    return m_pcmStream.StreamType();
}

static HRESULT
DeviceNameGet(IMMDeviceCollection *dc, UINT id, wchar_t *name, size_t nameBytes)
{
    HRESULT hr = 0;

    IMMDevice *device  = NULL;
    IPropertyStore *ps = NULL;
    PROPVARIANT pv;

    assert(dc);
    assert(name);
    assert(0 < nameBytes);

    name[0] = 0;

    PropVariantInit(&pv);

    HRG(dc->Item(id, &device));
    HRG(device->OpenPropertyStore(STGM_READ, &ps));
    HRG(ps->GetValue(PKEY_Device_FriendlyName, &pv));

    wcsncpy_s(name, nameBytes/2, pv.pwszVal, nameBytes/2 -1);

end:
    PropVariantClear(&pv);
    SafeRelease(&ps);
    SafeRelease(&device);
    return hr;
}

static HRESULT
DeviceIdStringGet(IMMDeviceCollection *dc, UINT id, wchar_t *deviceIdStr, size_t deviceIdStrBytes)
{
    HRESULT hr = 0;

    IMMDevice *device  = NULL;
    LPWSTR    s        = NULL;

    assert(dc);
    assert(deviceIdStr);
    assert(0 < deviceIdStrBytes);

    deviceIdStr[0] = 0;

    HRG(dc->Item(id, &device));
    HRG(device->GetId(&s));

    wcsncpy_s(deviceIdStr, deviceIdStrBytes/2, s, deviceIdStrBytes/2 -1);

end:
    CoTaskMemFree(s);
    s = NULL;
    SafeRelease(&device);
    return hr;
}

HRESULT
WasapiUser::DoDeviceEnumeration(WWDeviceType t)
{
    HRESULT hr = 0;

    dprintf("D: %s() t=%d\n", __FUNCTION__, (int)t);

    assert(m_pNotificationClient);

    switch (t) {
    case WWDTPlay:
        if (m_dataFlow != eRender) {
            m_dataFlow = eRender;
        }
        break;
    case WWDTRec:
        if (m_dataFlow != eCapture) {
            m_dataFlow = eCapture;
        }
        break;
    default:
        assert(0);
        return E_FAIL;
    }

    m_deviceInfo.clear();
    SafeRelease(&m_deviceCollection);

    if (m_deviceEnumerator) {
        m_deviceEnumerator->UnregisterEndpointNotificationCallback(m_pNotificationClient);
    }
    SafeRelease(&m_deviceEnumerator);

    HRR(CoCreateInstance(__uuidof(MMDeviceEnumerator), NULL, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&m_deviceEnumerator)));

    m_deviceEnumerator->RegisterEndpointNotificationCallback(m_pNotificationClient);

    HRR(m_deviceEnumerator->EnumAudioEndpoints(m_dataFlow, DEVICE_STATE_ACTIVE, &m_deviceCollection));

    UINT nDevices = 0;
    HRG(m_deviceCollection->GetCount(&nDevices));

    for (UINT i=0; i<nDevices; ++i) {
        wchar_t name[WW_DEVICE_NAME_COUNT];
        wchar_t idStr[WW_DEVICE_IDSTR_COUNT];
        HRG(DeviceNameGet(m_deviceCollection, i, name, sizeof name));
        HRG(DeviceIdStringGet(m_deviceCollection, i, idStr, sizeof idStr));
        m_deviceInfo.push_back(WWDeviceInfo(i, name, idStr));
    }

end:
    return hr;
}

int
WasapiUser::GetDeviceCount(void)
{
    assert(m_deviceCollection);
    return (int)m_deviceInfo.size();
}

bool
WasapiUser::GetDeviceName(int id, LPWSTR name, size_t nameBytes)
{
    if(id < 0 || (int)m_deviceInfo.size() <= id) {
        return false;
    }

    wcsncpy_s(name, nameBytes/2, m_deviceInfo[id].name, nameBytes/2 -1);
    return true;
}

bool
WasapiUser::GetDeviceIdString(int id, LPWSTR idStr, size_t idStrBytes)
{
    if(id < 0 || (int)m_deviceInfo.size() <= id) {
        return false;
    }

    wcsncpy_s(idStr, idStrBytes/2, m_deviceInfo[id].idStr, idStrBytes/2 -1);
    return true;
}

int
WasapiUser::InspectDevice(int id, int sampleRate, int bitsPerSample, int validBitsPerSample, int bitFormat)
{
    HRESULT hr;
    WAVEFORMATEX *waveFormat = NULL;

    assert(0 <= id && id < (int)m_deviceInfo.size());

    assert(m_deviceCollection);
    assert(!m_deviceToUse);
    assert(0 <= bitFormat && bitFormat <= 1);

    HRG(m_deviceCollection->Item(id, &m_deviceToUse));

    HRG(m_deviceToUse->Activate(__uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL, (void**)&m_audioClient));

    assert(!waveFormat);
    HRG(m_audioClient->GetMixFormat(&waveFormat));
    assert(waveFormat);

    WAVEFORMATEXTENSIBLE * wfex = (WAVEFORMATEXTENSIBLE*)waveFormat;

    dprintf("original Mix Format:\n");
    WWWaveFormatDebug(waveFormat);
    WWWFEXDebug(wfex);

    if (waveFormat->wFormatTag != WAVE_FORMAT_EXTENSIBLE) {
        dprintf("E: unsupported device ! mixformat == 0x%08x\n", waveFormat->wFormatTag);
        hr = E_FAIL;
        goto end;
    }

    if (bitFormat == 0) {
        wfex->SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
    } else {
        wfex->SubFormat = KSDATAFORMAT_SUBTYPE_IEEE_FLOAT;
    }

    wfex->Format.wBitsPerSample       = (WORD)bitsPerSample;
    wfex->Format.nSamplesPerSec       = sampleRate;
    wfex->Format.nBlockAlign          = (WORD)((bitsPerSample / 8) * waveFormat->nChannels);
    wfex->Format.nAvgBytesPerSec      = wfex->Format.nSamplesPerSec*wfex->Format.nBlockAlign;
    wfex->Samples.wValidBitsPerSample = (WORD)validBitsPerSample;

    dprintf("preferred Format:\n");
    WWWaveFormatDebug(waveFormat);
    WWWFEXDebug(wfex);

    hr = m_audioClient->IsFormatSupported(m_shareMode,waveFormat,NULL);
    dprintf("IsFormatSupported=%08x\n", hr);

end:
    SafeRelease(&m_deviceToUse);
    SafeRelease(&m_audioClient);

    if (waveFormat) {
        CoTaskMemFree(waveFormat);
        waveFormat = NULL;
    }

    return hr;
}

HRESULT
WasapiUser::ChooseDevice(int id)
{
    HRESULT hr = 0;

    dprintf("D: %s(%d)\n", __FUNCTION__, id);

    if (id < 0) {
        goto end;
    }

    assert(m_deviceCollection);
    assert(!m_deviceToUse);

    HRG(m_deviceCollection->Item(id, &m_deviceToUse));
    m_useDeviceId = id;
    wcscpy_s(m_useDeviceName, m_deviceInfo[id].name);
    wcscpy_s(m_useDeviceIdStr, m_deviceInfo[id].idStr);

end:
    return hr;
}

void
WasapiUser::UnchooseDevice(void)
{
    dprintf("D: %s()\n", __FUNCTION__);

    SafeRelease(&m_deviceToUse);
    m_useDeviceId = -1;
    m_useDeviceName[0] = 0;
    m_useDeviceIdStr[0] = 0;
}

int
WasapiUser::GetUseDeviceId(void)
{
    dprintf("D: %s() %d\n", __FUNCTION__, m_useDeviceId);
    return m_useDeviceId;
}

bool
WasapiUser::GetUseDeviceName(LPWSTR name, size_t nameBytes)
{
    wcsncpy_s(name, nameBytes/2, m_useDeviceName, nameBytes/2 -1);
    return true;
}

bool
WasapiUser::GetUseDeviceIdString(LPWSTR idStr, size_t idStrBytes)
{
    wcsncpy_s(idStr, idStrBytes/2, m_useDeviceIdStr, idStrBytes/2 -1);
    return true;
}

/// numChannels to channelMask
static DWORD
GetChannelMask(int numChannels)
{
    // maskbit32 is reserved therefore allowable numChannels is smaller than 32
    assert(numChannels < 32);
    DWORD result = 0;

    switch (numChannels) {
    case 1:
        result = 0; // mono (unspecified)
        break;
    case 2:
        result = 3; // 2ch stereo (FL FR)
        break;
    case 4:
        result = 0x33; // 4ch matrix (FL FR BL BR)
        break;
    case 6:
        result = 0x3f; // 5.1 surround (FL FR FC LFE BL BR)
        break;
    case 8:
        result = 0x63f; // 7.1 surround (FL FR FC LFE BL BR SL SR)
        break;
    default:
        // ? unknown sampleFormat
        result = (DWORD)((1LL << numChannels)-1);
        break;
    }

    return result;
}

HRESULT
WasapiUser::Setup(
        int sampleRate,
        WWPcmDataSampleFormatType sampleFormat,
        int numChannels)
{
    HRESULT      hr          = 0;
    WAVEFORMATEX *waveFormat = NULL;

    dprintf("D: %s(%d %s %d)\n", __FUNCTION__, sampleRate, WWPcmDataSampleFormatTypeToStr(sampleFormat), numChannels);

    m_sampleRate          = sampleRate;
    m_sampleFormat        = sampleFormat;
    m_numChannels         = numChannels;
    m_dwChannelMask = 0; //< TODO: pass from argument

    m_audioSamplesReadyEvent = CreateEventEx(NULL, NULL, 0, EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_audioSamplesReadyEvent);

    assert(m_deviceToUse);
    assert(!m_audioClient);
    HRG(m_deviceToUse->Activate(__uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL, (void**)&m_audioClient));

    assert(!waveFormat);
    HRG(m_audioClient->GetMixFormat(&waveFormat));
    assert(waveFormat);

    WAVEFORMATEXTENSIBLE * wfex = (WAVEFORMATEXTENSIBLE*)waveFormat;

    dprintf("original Mix Format:\n");
    WWWaveFormatDebug(waveFormat);
    WWWFEXDebug(wfex);

    if (waveFormat->wFormatTag != WAVE_FORMAT_EXTENSIBLE) {
        dprintf("E: unsupported device ! mixformat == 0x%08x\n", waveFormat->wFormatTag);
        hr = E_FAIL;
        goto end;
    }

    // exclusive/shared common task
    wfex->Format.nChannels = (WORD)m_numChannels;

    if (WWSMExclusive == m_shareMode) {
        // exclusive mode specific task

        if (WWPcmDataSampleFormatTypeIsInt(m_sampleFormat)) {
            wfex->SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
        }
        wfex->Format.wBitsPerSample       = (WORD)WWPcmDataSampleFormatTypeToBitsPerSample(m_sampleFormat);
        wfex->Format.nSamplesPerSec       = sampleRate;
        wfex->Format.nBlockAlign          = (WORD)((wfex->Format.wBitsPerSample / 8) * wfex->Format.nChannels);
        wfex->Format.nAvgBytesPerSec      = wfex->Format.nSamplesPerSec * wfex->Format.nBlockAlign;
        wfex->Samples.wValidBitsPerSample = (WORD)WWPcmDataSampleFormatTypeToValidBitsPerSample(m_sampleFormat);
        wfex->dwChannelMask               = GetChannelMask(m_numChannels);

        dprintf("preferred Format:\n");
        WWWaveFormatDebug(waveFormat);
        WWWFEXDebug(wfex);
    
        HRG(m_audioClient->IsFormatSupported(m_shareMode,waveFormat,NULL));
    } else {
        // shared mode specific task
        // wBitsPerSample, nSamplesPerSec, wValidBitsPerSample are fixed

        // FIXME: This code snippet does not work properly!
        if (2 != m_numChannels) {
            wfex->Format.nBlockAlign     = (WORD)((wfex->Format.wBitsPerSample / 8) * wfex->Format.nChannels);
            wfex->Format.nAvgBytesPerSec = wfex->Format.nSamplesPerSec*wfex->Format.nBlockAlign;
            wfex->dwChannelMask          = GetChannelMask(m_numChannels);
        }
    }

    m_deviceBytesPerFrame = waveFormat->nBlockAlign;
    
    DWORD streamFlags      = 0;
    int   periodsPerBuffer = 1;
    switch (m_dataFeedMode) {
    case WWDFMTimerDriven:
        streamFlags      = AUDCLNT_STREAMFLAGS_NOPERSIST;
        periodsPerBuffer = PERIODS_PER_BUFFER_ON_TIMER_DRIVEN_MODE;
        break;
    case WWDFMEventDriven:
        streamFlags      = AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_NOPERSIST;
        periodsPerBuffer = 1;
        break;
    default:
        assert(0);
        break;
    }

    REFERENCE_TIME bufferPeriodicity = m_latencyMillisec * 10000;
    REFERENCE_TIME bufferDuration    = bufferPeriodicity * periodsPerBuffer;

    m_deviceSampleRate    = waveFormat->nSamplesPerSec;
    m_deviceNumChannels   = waveFormat->nChannels;
    m_deviceDwChannelMask = wfex->dwChannelMask;
    m_deviceSampleFormat  = m_sampleFormat;

    // TODO: delete!
    m_dwChannelMask = m_deviceDwChannelMask;

    if (WWSMShared == m_shareMode) {
        // 共有モードでデバイスサンプルレートとWAVファイルのサンプルレートが異なる場合、
        // 誰かが別のところでリサンプリングを行ってデバイスサンプルレートにする必要がある。
        // デバイスサンプルレートはWasapiUser::GetDeviceSampleRate()
        // WAVファイルのサンプルレートはWasapiUser::GetPcmDataSampleRate()で取得できる。
        // この後誰かが別のところでリサンプリングを行った結果
        // WAVファイルのサンプルレートが変わったらWasapiUser::UpdatePcmDataFormat()で更新する。
        //
        // 共有モード イベント駆動の場合、bufferPeriodicityに0をセットする。

        m_deviceSampleFormat = WWPcmDataSampleFormatSfloat;

        if (WWDFMEventDriven == m_dataFeedMode) {
            bufferPeriodicity = 0;
        }
    }

    hr = m_audioClient->Initialize(m_shareMode, streamFlags, bufferDuration, bufferPeriodicity, waveFormat, NULL);
    if (hr == AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED) {
        HRG(m_audioClient->GetBufferSize(&m_bufferFrameNum));

        SafeRelease(&m_audioClient);

        bufferPeriodicity = (REFERENCE_TIME)(
            10000.0 *                         // (REFERENCE_TIME(100ns) / ms) *
            1000 *                            // (ms / s) *
            m_bufferFrameNum /                // frames /
            waveFormat->nSamplesPerSec +      // (frames / s)
            0.5);
        bufferDuration = bufferPeriodicity * periodsPerBuffer;

        HRG(m_deviceToUse->Activate(__uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL, (void**)&m_audioClient));

        hr = m_audioClient->Initialize(m_shareMode, streamFlags, bufferDuration, bufferPeriodicity, waveFormat, NULL);
    }
    if (FAILED(hr)) {
        dprintf("E: audioClient->Initialize failed 0x%08x\n", hr);
        goto end;
    }

    HRG(m_audioClient->GetBufferSize(&m_bufferFrameNum));
    dprintf("m_audioClient->GetBufferSize() rv=%u\n", m_bufferFrameNum);

    if (WWDFMEventDriven == m_dataFeedMode) {
        HRG(m_audioClient->SetEventHandle(m_audioSamplesReadyEvent));
    }

    switch (m_dataFlow) {
    case eRender:
        HRG(m_audioClient->GetService(IID_PPV_ARGS(&m_renderClient)));
        m_pcmStream.PrepareSilenceBuffers(m_latencyMillisec, m_deviceSampleFormat, m_deviceSampleRate, m_deviceNumChannels, m_deviceBytesPerFrame);
        break;
    case eCapture:
        HRG(m_audioClient->GetService(IID_PPV_ARGS(&m_captureClient)));
        break;
    default:
        assert(0);
        break;
    }

end:

    if (waveFormat) {
        CoTaskMemFree(waveFormat);
        waveFormat = NULL;
    }

    return hr;
}

bool
WasapiUser::IsResampleNeeded(void) const
{
    if (WWSMExclusive == m_shareMode) {
        return false;
    }

    if (m_deviceSampleRate != m_sampleRate ||
            m_deviceNumChannels != m_numChannels ||
            m_deviceDwChannelMask != m_dwChannelMask ||
            WWPcmDataSampleFormatSfloat != m_sampleFormat) {
        return true;
    }
    return false;
}

void
WasapiUser::UpdatePcmDataFormat(int sampleRate, WWPcmDataSampleFormatType sampleFormat, int numChannels, DWORD dwChannelMask)
{
    assert(WWSMShared == m_shareMode);

    m_sampleRate    = sampleRate;
    m_sampleFormat  = sampleFormat;
    m_numChannels   = numChannels;
    m_dwChannelMask = dwChannelMask;
}

void
WasapiUser::Unsetup(void)
{
    dprintf("D: %s() ASRE=%p CC=%p RC=%p AC=%p\n", __FUNCTION__, m_audioSamplesReadyEvent, m_captureClient, m_renderClient, m_audioClient);

    if (m_audioSamplesReadyEvent) {
        CloseHandle(m_audioSamplesReadyEvent);
        m_audioSamplesReadyEvent = NULL;
    }

    m_pcmStream.ReleaseBuffers();

    SafeRelease(&m_captureClient);
    SafeRelease(&m_renderClient);
    SafeRelease(&m_audioClient);
}

HRESULT
WasapiUser::Start(void)
{
    HRESULT hr      = 0;
    BYTE    *pData  = NULL;
    UINT32  nFrames = 0;
    DWORD   flags   = 0;

    dprintf("D: %s()\n", __FUNCTION__);

    HRG(m_audioClient->Reset());

    assert(!m_shutdownEvent);
    m_shutdownEvent = CreateEventEx(NULL, NULL, 0, EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_shutdownEvent);

    switch (m_dataFlow) {
    case eRender:
        assert(m_pcmStream.GetPcm(WWPDUNowPlaying));

        assert(NULL == m_thread);
        m_thread = CreateThread(NULL, 0, RenderEntry, this, 0, NULL);
        assert(m_thread);

        nFrames = m_bufferFrameNum;
        if (WWDFMTimerDriven == m_dataFeedMode || WWSMShared == m_shareMode) {
            // 排他タイマー駆動の場合、パッド計算必要。
            // 共有モードの場合タイマー駆動でもイベント駆動でもパッドが必要。
            // RenderSharedEventDrivenのWASAPIRenderer.cpp参照。

            UINT32 padding = 0; //< frame now using
            HRG(m_audioClient->GetCurrentPadding(&padding));
            nFrames = m_bufferFrameNum - padding;
        }

        if (0 <= nFrames) {
            assert(m_renderClient);
            HRG(m_renderClient->GetBuffer(nFrames, &pData));
            memset(pData, 0, nFrames * m_deviceBytesPerFrame);
            HRG(m_renderClient->ReleaseBuffer(nFrames, 0));
        }

        m_footerCount = 0;

        break;

    case eCapture:
        assert(m_captureCallback);
        m_thread = CreateThread(NULL, 0, CaptureEntry, this, 0, NULL);
        assert(m_thread);

        hr = m_captureClient->GetBuffer(&pData, &nFrames, &flags, NULL, NULL);
        if (SUCCEEDED(hr)) {
            // if succeeded, release buffer pData
            m_captureClient->ReleaseBuffer(nFrames);
            pData = NULL;
        }

        hr = S_OK;
        m_glitchCount = 0;
        break;

    default:
        assert(0);
        break;
    }

    assert(m_audioClient);
    HRG(m_audioClient->Start());

end:
    return hr;
}

void
WasapiUser::Stop(void)
{
    HRESULT hr;

    dprintf("D: %s() AC=%p SE=%p T=%p\n", __FUNCTION__, m_audioClient, m_shutdownEvent, m_thread);

    // ポーズ中の場合、ポーズを解除。
    m_pcmStream.SetPauseResumePcmData(NULL);

    if (NULL != m_audioClient) {
        hr = m_audioClient->Stop();
        if (FAILED(hr)) {
            dprintf("E: %s m_audioClient->Stop() failed 0x%x\n", __FUNCTION__, hr);
        }
    }

    if (NULL != m_shutdownEvent) {
        SetEvent(m_shutdownEvent);
    }
    if (NULL != m_thread) {
        WaitForSingleObject(m_thread, INFINITE);
        dprintf("D: %s:%d CloseHandle(%p)\n", __FILE__, __LINE__, m_thread);
        if (m_thread) {
            CloseHandle(m_thread);
        }
        m_thread = NULL;
    }

    if (NULL != m_shutdownEvent) {
        dprintf("D: %s:%d CloseHandle(%p)\n", __FILE__, __LINE__, m_shutdownEvent);
        CloseHandle(m_shutdownEvent);
        m_shutdownEvent = NULL;
    }
}

HRESULT
WasapiUser::Pause(void)
{
    // HRESULT hr = S_OK;
    bool pauseDataSetSucceeded = false;

    assert(m_mutex);
    WaitForSingleObject(m_mutex, INFINITE);
    {
        WWPcmData *nowPlaying = m_pcmStream.GetPcm(WWPDUNowPlaying);
        if (nowPlaying && nowPlaying->contentType == WWPcmDataContentMusicData) {
            // 通常データを再生中の場合ポーズが可能。
            // m_nowPlayingPcmDataをpauseBuffer(フェードアウトするPCMデータ)に差し替え、
            // 再生が終わるまでブロッキングで待つ。
            pauseDataSetSucceeded = true;
            m_pcmStream.Paused(nowPlaying);
        }
    }
    ReleaseMutex(m_mutex);

    if (pauseDataSetSucceeded) {
        // ここで再生一時停止までブロックする。
        WWPcmData *nowPlayingPcmData = NULL;
        do {
            assert(m_mutex);
            WaitForSingleObject(m_mutex, INFINITE);
            {
                nowPlayingPcmData = m_pcmStream.GetPcm(WWPDUNowPlaying);
            }
            ReleaseMutex(m_mutex);

            Sleep(100);
        } while (nowPlayingPcmData != NULL);
        //再生一時停止状態はnowPlayingPcmData==NULLで、再生スレッドは無音を送出し続ける。
    } else {
        dprintf("%s pauseDataSet failed\n", __FUNCTION__);
    }

//end:
    return (pauseDataSetSucceeded) ? S_OK : E_FAIL;
}

HRESULT
WasapiUser::Unpause(void)
{
    WWPcmData *restartBuffer = m_pcmStream.UnpausePrepare();

    assert(m_mutex);
    WaitForSingleObject(m_mutex, INFINITE);
    {
        m_pcmStream.UpdateNowPlaying(restartBuffer);
    }
    ReleaseMutex(m_mutex);

    m_pcmStream.UnpauseDone();
    return S_OK;
}

bool
WasapiUser::Run(int millisec)
{
    DWORD rv = WaitForSingleObject(m_thread, millisec);
    if (rv == WAIT_TIMEOUT) {
        Sleep(10);
        return false;
    }

    return true;
}

void
WasapiUser::UpdatePlayPcmData(WWPcmData &pcmData)
{
    if (m_thread != NULL) {
        UpdatePlayPcmDataWhenPlaying(pcmData);
    } else {
        m_pcmStream.UpdateStartPcm(&pcmData);
    }
}

void
WasapiUser::UpdatePlayPcmDataWhenPlaying(WWPcmData &pcmData)
{
    dprintf("D: %s(%d)\n", __FUNCTION__, pcmData.id);

    assert(m_mutex);
    WaitForSingleObject(m_mutex, INFINITE);
    {
        WWPcmData *nowPlaying = m_pcmStream.GetPcm(WWPDUNowPlaying);
        if (nowPlaying) {
            WWPcmData *splice = m_pcmStream.GetPcm(WWPDUSplice);
            // m_nowPlayingPcmDataをpcmDataに移動する。
            // Issue3: いきなり移動するとブチッと言うのでsplice bufferを経由してなめらかにつなげる。
            int advance = splice->CreateCrossfadeData(*nowPlaying, nowPlaying->posFrame, pcmData, pcmData.posFrame);

            if (nowPlaying != &pcmData) {
                // 別の再生曲に移動した場合、それまで再生していた曲は頭出ししておく。
                nowPlaying->posFrame = 0;
            }

            splice->next = WWPcmData::AdvanceFrames(&pcmData, advance);
            m_pcmStream.UpdateNowPlaying(splice);
        } else {
            // 一時停止中。
            WWPcmData *pauseResumePcm = m_pcmStream.GetPcm(WWPDUPauseResumeToPlay);
            if (pauseResumePcm != &pcmData) {
                // 別の再生曲に移動した場合、それまで再生していた曲は頭出ししておく。
                pauseResumePcm->posFrame = 0;
                m_pcmStream.UpdatePauseResume(&pcmData);

                // 再生シークをしたあと再生一時停止し再生曲を変更し再生再開すると
                // 一瞬再生曲表示が再生シークした曲になる問題の修正ｗ
                m_pcmStream.GetPcm(WWPDUSplice)->next = NULL;
            }
        }
    }
    ReleaseMutex(m_mutex);
}

bool
WasapiUser::SetPosFrame(int64_t v)
{
    if (m_dataFlow != eRender) {
        assert(0);
        return false;
    }

    if (v < 0) {
        return false;
    }

    if (WWStreamDop == m_pcmStream.StreamType()) {
        // 必ず2の倍数の位置にジャンプする。
        v &= ~(1LL);
    }

    bool result = false;

    assert(m_mutex);
    WaitForSingleObject(m_mutex, INFINITE);
    {
        WWPcmData *nowPlaying = m_pcmStream.GetPcm(WWPDUNowPlaying);
        if (nowPlaying &&
                nowPlaying->contentType == WWPcmDataContentMusicData && v < nowPlaying->nFrames) {
            WWPcmData *splice = m_pcmStream.GetPcm(WWPDUSplice);
            // 再生中。
            // nowPlaying->posFrameをvに移動する。
            // Issue3: いきなり移動するとブチッと言うのでsplice bufferを経由してなめらかにつなげる。
            int advance = splice->CreateCrossfadeData(*nowPlaying, nowPlaying->posFrame, *nowPlaying, v);

            // 移動先は、nowPlaying上の位置v + クロスフェードのためにadvanceフレーム進んだ位置となる。
            nowPlaying->posFrame = v;
            WWPcmData *toPcm = WWPcmData::AdvanceFrames(nowPlaying, advance);
            splice->next = toPcm;

            m_pcmStream.UpdateNowPlaying(splice);

#ifdef CHECK_DOP_MARKER
            splice->CheckDopMarker();
#endif // CHECK_DOP_MARKER

            result = true;
        } else {
            WWPcmData *pauseResumePcm = m_pcmStream.GetPcm(WWPDUPauseResumeToPlay);
            if (pauseResumePcm && v < pauseResumePcm->nFrames) {
                // pause中。Pause再開後に再生されるPCMの再生位置を更新する。
                pauseResumePcm->posFrame = v;
                result = true;
            }
        }
    }
    ReleaseMutex(m_mutex);

    return result;
}

int64_t
WasapiUser::GetCaptureGlitchCount(void)
{
    return m_glitchCount;
}

/////////////////////////////////////////////////////////////////////////////////
// 再生スレッド

/// 再生スレッドの入り口。
/// @param lpThreadParameter WasapiUserインスタンスのポインタが渡ってくる。
DWORD
WasapiUser::RenderEntry(LPVOID lpThreadParameter)
{
    WasapiUser* self = (WasapiUser*)lpThreadParameter;

    return self->RenderMain();
}

/// PCMデータをwantFramesフレームだけpData_returnに戻す。
/// @return 実際にpData_returnに書き込んだフレーム数。
int
WasapiUser::CreateWritableFrames(BYTE *pData_return, int wantFrames)
{
    int       pos      = 0;
    WWPcmData *pcmData = m_pcmStream.GetPcm(WWPDUNowPlaying);

    while (NULL != pcmData && 0 < wantFrames) {
        int copyFrames = wantFrames;
        if (pcmData->nFrames <= pcmData->posFrame + wantFrames) {
            // pcmDataが持っているフレーム数よりも要求フレーム数が多い。
            copyFrames = (int)(pcmData->nFrames - pcmData->posFrame);
        }

        dprintf("pcmData=%p next=%p posFrame/nframes=%lld/%lld copyFrames=%d\n", pcmData, pcmData->next, pcmData->posFrame, pcmData->nFrames, copyFrames);

        CopyMemory(&pData_return[pos*m_deviceBytesPerFrame], &pcmData->stream[pcmData->posFrame * m_deviceBytesPerFrame], copyFrames * m_deviceBytesPerFrame);

        pos               += copyFrames;
        pcmData->posFrame += copyFrames;
        wantFrames        -= copyFrames;

        if (pcmData->nFrames <= pcmData->posFrame) {
            // pcmDataの最後まで来た。
            // このpcmDataの再生位置は巻き戻して、次のpcmDataの先頭をポイントする。
            pcmData->posFrame = 0;
            pcmData           = pcmData->next;
        }
    }

    m_pcmStream.UpdateNowPlaying(pcmData);

    return pos;
}

/// WASAPIデバイスにPCMデータを送れるだけ送る。
bool
WasapiUser::AudioSamplesSendProc(void)
{
    bool    result     = true;
    BYTE    *to        = NULL;
    HRESULT hr         = 0;
    int     copyFrames = 0;
    int     writableFrames = 0;

    WaitForSingleObject(m_mutex, INFINITE);

    writableFrames = m_bufferFrameNum;
    if (WWDFMTimerDriven == m_dataFeedMode || WWSMShared == m_shareMode) {
        // 共有モードの場合イベント駆動でもパッドが必要になる。
        // RenderSharedEventDrivenのWASAPIRenderer.cpp参照。

        UINT32 padding = 0; //< frame num now using

        assert(m_audioClient);
        HRGR(m_audioClient->GetCurrentPadding(&padding));

        writableFrames = m_bufferFrameNum - padding;

        // dprintf("m_bufferFrameNum=%d padding=%d writableFrames=%d\n",
        //     m_bufferFrameNum, padding, writableFrames);
        if (writableFrames <= 0) {
            goto end;
        }
    }

    assert(m_renderClient);
    HRGR(m_renderClient->GetBuffer(writableFrames, &to));
    assert(to);

    copyFrames = CreateWritableFrames(to, writableFrames);

    if (0 < writableFrames - copyFrames) {
        memset(&to[copyFrames*m_deviceBytesPerFrame], 0,
            (writableFrames - copyFrames)*m_deviceBytesPerFrame);
        /* dprintf("fc=%d bs=%d cb=%d memset %d bytes\n",
            m_footerCount, m_bufferFrameNum, copyFrames,
            (m_bufferFrameNum - copyFrames)*m_deviceBytesPerFrame);
        */
    }

    HRGR(m_renderClient->ReleaseBuffer(writableFrames, 0));
    to = NULL;

    if (NULL == m_pcmStream.GetPcm(WWPDUNowPlaying)) {
        ++m_footerCount;
        if (m_footerNeedSendCount < m_footerCount) {
            // PCMを全て送信完了。
            if (NULL != m_pcmStream.GetPcm(WWPDUPauseResumeToPlay)) {
                // ポーズ中。スレッドを回し続ける。
            } else {
                // スレッドを停止する。
                result = false;
            }
        }
    }

end:
    ReleaseMutex(m_mutex);
    return result;
}



/// 再生スレッド メイン。
/// イベントやタイマーによって起き、PCMデータを送って、寝る。
/// というのを繰り返す。
DWORD
WasapiUser::RenderMain(void)
{
    bool    stillPlaying   = true;
    HANDLE  waitArray[2]   = {m_shutdownEvent, m_audioSamplesReadyEvent};
    int     waitArrayCount;
    DWORD   timeoutMillisec;
    HANDLE  mmcssHandle    = NULL;
    DWORD   mmcssTaskIndex = 0;
    DWORD   waitResult;
    HRESULT hr             = 0;
    
    HRG(CoInitializeEx(NULL, COINIT_MULTITHREADED));

    HRG(m_timerResolution.Setup());

    // マルチメディアクラススケジューラーサービスのスレッド優先度設定。
    if (WWSTTNone != m_schedulerTaskType) {
        dprintf("D: %s() AvSetMmThreadCharacteristics(%S)\n", __FUNCTION__, WWSchedulerTaskTypeToStr(m_schedulerTaskType));

        mmcssHandle = AvSetMmThreadCharacteristics(WWSchedulerTaskTypeToStr(m_schedulerTaskType), &mmcssTaskIndex);
        if (NULL == mmcssHandle) {
            dprintf("Unable to enable MMCSS on render thread: 0x%08x\n", GetLastError());
        }
    }

    if (m_dataFeedMode == WWDFMTimerDriven) {
        waitArrayCount        = 1;
        m_footerNeedSendCount = FOOTER_SEND_FRAME_NUM * 2;
        timeoutMillisec       = m_latencyMillisec     / 2;
    } else {
        waitArrayCount        = 2;
        m_footerNeedSendCount = FOOTER_SEND_FRAME_NUM;
        timeoutMillisec       = INFINITE;
    }

    // dprintf("D: %s() waitArrayCount=%d m_shutdownEvent=%p m_audioSamplesReadyEvent=%p\n",
    //    __FUNCTION__, waitArrayCount, m_shutdownEvent, m_audioSamplesReadyEvent);

    while (stillPlaying) {
        waitResult = WaitForMultipleObjects(
            waitArrayCount, waitArray, FALSE, timeoutMillisec);
        switch (waitResult) {
        case WAIT_OBJECT_0 + 0:     // m_shutdownEvent
            // シャットダウン要求によって起きた場合。
            dprintf("D: %s() shutdown event flagged\n", __FUNCTION__);
            stillPlaying = false;
            break;
        case WAIT_OBJECT_0 + 1:     // m_audioSamplesReadyEvent
            // イベント駆動モードの時だけ起こる。
            stillPlaying = AudioSamplesSendProc();
            break;
        case WAIT_TIMEOUT:
            // タイマー駆動モードの時だけ起こる。
            stillPlaying = AudioSamplesSendProc();
            break;
        default:
            break;
        }
    }

end:
    if (NULL != mmcssHandle) {
        AvRevertMmThreadCharacteristics(mmcssHandle);
        mmcssHandle = NULL;
    }

    m_timerResolution.Unsetup();

    CoUninitialize();
    return hr;
}

//////////////////////////////////////////////////////////////////////////////
// 録音スレッド

DWORD
WasapiUser::CaptureEntry(LPVOID lpThreadParameter)
{
    WasapiUser* self = (WasapiUser*)lpThreadParameter;
    return self->CaptureMain();
}

DWORD
WasapiUser::CaptureMain(void)
{
    bool    stillRecording   = true;
    HANDLE  waitArray[2]   = {m_shutdownEvent, m_audioSamplesReadyEvent};
    int     waitArrayCount;
    DWORD   timeoutMillisec;
    HANDLE  mmcssHandle    = NULL;
    DWORD   mmcssTaskIndex = 0;
    DWORD   waitResult;
    HRESULT hr             = 0;
    
    HRG(CoInitializeEx(NULL, COINIT_MULTITHREADED));

    timeBeginPeriod(1);

    dprintf("D: %s AvSetMmThreadCharacteristics(%S)\n", __FUNCTION__, WWSchedulerTaskTypeToStr(m_schedulerTaskType));

    mmcssHandle = AvSetMmThreadCharacteristics(WWSchedulerTaskTypeToStr(m_schedulerTaskType), &mmcssTaskIndex);
    if (NULL == mmcssHandle) {
        dprintf("Unable to enable MMCSS on render thread: 0x%08x\n", GetLastError());
    }

    if (m_dataFeedMode == WWDFMTimerDriven) {
        waitArrayCount  = 1;
        timeoutMillisec = m_latencyMillisec / 2;
    } else {
        waitArrayCount  = 2;
        timeoutMillisec = INFINITE;
    }

    while (stillRecording) {
        waitResult = WaitForMultipleObjects(waitArrayCount, waitArray, FALSE, timeoutMillisec);
        switch (waitResult) {
        case WAIT_OBJECT_0 + 0:     // m_shutdownEvent
            stillRecording = false;
            break;
        case WAIT_OBJECT_0 + 1:     // m_audioSamplesReadyEvent
            // only in EventDriven mode
            stillRecording = AudioSamplesRecvProc();
            break;
        case WAIT_TIMEOUT:
            // only in TimerDriven mode
            stillRecording = AudioSamplesRecvProc();
            break;
        default:
            break;
        }
    }

end:
    if (NULL != mmcssHandle) {
        AvRevertMmThreadCharacteristics(mmcssHandle);
        mmcssHandle = NULL;
    }

    timeEndPeriod(1);

    CoUninitialize();
    return hr;
}

bool
WasapiUser::AudioSamplesRecvProc(void)
{
    bool    result     = true;
    UINT32  packetLength = 0;
    UINT32  numFramesAvailable = 0;
    DWORD   flags      = 0;
    BYTE    *pData     = NULL;
    HRESULT hr         = 0;
    UINT64  devicePosition = 0;

    WaitForSingleObject(m_mutex, INFINITE);

    HRG(m_captureClient->GetNextPacketSize(&packetLength));

    if (packetLength == 0) {
        goto end;
    }
        
    numFramesAvailable = packetLength;
    flags = 0;

    HRG(m_captureClient->GetBuffer(&pData, &numFramesAvailable, &flags, &devicePosition, NULL));

    if (flags & AUDCLNT_BUFFERFLAGS_DATA_DISCONTINUITY) {
        ++m_glitchCount;
    }

    if (m_captureCallback != NULL) {
        // 都度コールバックを呼ぶ
        m_captureCallback(pData, numFramesAvailable * m_deviceBytesPerFrame);
        HRG(m_captureClient->ReleaseBuffer(numFramesAvailable));
        goto end;
    }

end:
    ReleaseMutex(m_mutex);
    return result;
}

HRESULT
WasapiUser::OnDeviceStateChanged(LPCWSTR pwstrDeviceId, DWORD dwNewState)
{
    (void)dwNewState;
    // 再生中で、再生しているデバイスの状態が変わったときは
    // DeviceStateChanged()は再生を停止しなければならない
    if (m_stateChangedCallback) {
        m_stateChangedCallback(pwstrDeviceId);
    }

    return S_OK;
}

void
WasapiUser::MutexWait(void) {
    WaitForSingleObject(m_mutex, INFINITE);
}

void
WasapiUser::MutexRelease(void) {
    ReleaseMutex(m_mutex);
}
