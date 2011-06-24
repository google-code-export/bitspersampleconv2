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

#define FOOTER_SEND_FRAME_NUM (2)
#define PERIODS_PER_BUFFER_ON_TIMER_DRIVEN_MODE (4)

// define: レンダーバッファ上で再生データを作る
// undef : 一旦スタック上にて再生データを作ってからレンダーバッファにコピーする
#define CREATE_PLAYPCM_ON_RENDER_BUFFER

WWDeviceInfo::WWDeviceInfo(int id, const wchar_t * name)
{
    this->id = id;
    wcsncpy_s(this->name, name, WW_DEVICE_NAME_COUNT-1);
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
// event handler

class CMMNotificationClient : public IMMNotificationClient
{
public:
    CMMNotificationClient(WasapiUser *pWU):
            m_cRef(1)
    {
        m_pWasapiUser = pWU;
    }

    ~CMMNotificationClient()
    {
        m_pWasapiUser = NULL;
    }

    ULONG STDMETHODCALLTYPE
    AddRef()
    {
        return InterlockedIncrement(&m_cRef);
    }

    ULONG STDMETHODCALLTYPE
    Release()
    {
        ULONG ulRef = InterlockedDecrement(&m_cRef);
        if (0 == ulRef) {
            delete this;
        }
        return ulRef;
    }

    HRESULT STDMETHODCALLTYPE
    QueryInterface(
            REFIID riid, VOID **ppvInterface)
    {
        if (IID_IUnknown == riid) {
            AddRef();
            *ppvInterface = (IUnknown*)this;
        }
        else if (__uuidof(IMMNotificationClient) == riid) {
            AddRef();
            *ppvInterface = (IMMNotificationClient*)this;
        } else {
            *ppvInterface = NULL;
            return E_NOINTERFACE;
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE
    OnDefaultDeviceChanged(
            EDataFlow flow,
            ERole role,
            LPCWSTR pwstrDeviceId)
    {
        dprintf("%s %d %d %S\n",
            __FUNCTION__, flow, role, pwstrDeviceId);

        (void)flow;
        (void)role;
        (void)pwstrDeviceId;

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE
    OnDeviceAdded(LPCWSTR pwstrDeviceId)
    {
        dprintf("%s %S\n", __FUNCTION__,
            pwstrDeviceId);

        (void)pwstrDeviceId;

        return S_OK;
    };

    HRESULT STDMETHODCALLTYPE
    OnDeviceRemoved(LPCWSTR pwstrDeviceId)
    {
        dprintf("%s %S\n", __FUNCTION__,
            pwstrDeviceId);

        (void)pwstrDeviceId;

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE
    OnDeviceStateChanged(
            LPCWSTR pwstrDeviceId,
            DWORD dwNewState)
    {
        dprintf("%s %S %08x\n", __FUNCTION__,
            pwstrDeviceId, dwNewState);

        m_pWasapiUser->DeviceStateChanged();

        (void)pwstrDeviceId;
        (void)dwNewState;

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE
    OnPropertyValueChanged(
            LPCWSTR pwstrDeviceId,
            const PROPERTYKEY key)
    {
        /*
        dprintf("%s %S %08x:%08x:%08x:%08x = %08x\n", __FUNCTION__,
            pwstrDeviceId, key.fmtid.Data1, key.fmtid.Data2, key.fmtid.Data3, key.fmtid.Data4, key.pid);
        */

        (void)pwstrDeviceId;
        (void)key;

        return S_OK;
    }

private:
    LONG m_cRef;
    WasapiUser *m_pWasapiUser;
};



///////////////////////////////////////////////////////////////////////
// WasapiUser class

WasapiUser::WasapiUser(void)
{
    m_deviceCollection = NULL;
    m_deviceToUse      = NULL;

    m_shutdownEvent    = NULL;
    m_audioSamplesReadyEvent = NULL;

    m_audioClient      = NULL;

    m_renderClient     = NULL;
    m_captureClient    = NULL;

    m_thread           = NULL;
    m_capturedPcmData  = NULL;
    m_mutex            = NULL;
    m_coInitializeSuccess = false;
    m_glitchCount      = 0;
    m_schedulerTaskType = WWSTTAudio;
    m_shareMode         = AUDCLNT_SHAREMODE_EXCLUSIVE;

    m_audioClockAdjustment = NULL;
    m_nowPlayingPcmData    = NULL;
    m_pauseResumePcmData   = NULL;
    m_useDeviceId          = -1;
    m_deviceSampleRate     = 0;

    memset(m_useDeviceName, 0, sizeof m_useDeviceName);

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

    return hr;
}

void
WasapiUser::Term(void)
{
    dprintf("D: %s() m_deviceCollection=%p m_deviceToUse=%p m_mutex=%p\n",
        __FUNCTION__, m_deviceCollection, m_deviceToUse, m_mutex);

    if (m_deviceEnumerator && m_pNotificationClient) {
        m_deviceEnumerator->UnregisterEndpointNotificationCallback(
            m_pNotificationClient);
    }

    SafeRelease(&m_deviceCollection);
    SafeRelease(&m_deviceEnumerator);
    SAFE_DELETE(m_pNotificationClient);
    SafeRelease(&m_deviceToUse);
    m_useDeviceId = -1;
    m_useDeviceName[0] = 0;

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

    wcsncpy_s(name, nameBytes/2, pv.pwszVal, nameBytes/sizeof name[0] -1);

end:
    PropVariantClear(&pv);
    CoTaskMemFree(deviceId);
    SafeRelease(&ps);
    return hr;
}

HRESULT
WasapiUser::DoDeviceEnumeration(WWDeviceType t)
{
    HRESULT hr = 0;

    dprintf("D: %s() t=%d\n", __FUNCTION__, (int)t);

    bool needCreate = false;
    if (NULL == m_deviceEnumerator) {
        needCreate = true;
    }

    switch (t) {
    case WWDTPlay:
        if (m_dataFlow != eRender) {
            m_dataFlow = eRender;
            needCreate = true;
        }
        break;
    case WWDTRec:
        if (m_dataFlow != eCapture) {
            m_dataFlow = eCapture;
            needCreate = true;
        }
        break;
    default:
        assert(0);
        return E_FAIL;
    }

    m_deviceInfo.clear();

    if (needCreate) {
        if (m_deviceEnumerator && m_pNotificationClient) {
            m_deviceEnumerator->UnregisterEndpointNotificationCallback(
                m_pNotificationClient);
        }
        SafeRelease(&m_deviceEnumerator);
        SAFE_DELETE(m_pNotificationClient);

        HRR(CoCreateInstance(__uuidof(MMDeviceEnumerator),
            NULL, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&m_deviceEnumerator)));
    }

    if (NULL == m_pNotificationClient) {
        m_pNotificationClient = new CMMNotificationClient(this);
        m_deviceEnumerator->RegisterEndpointNotificationCallback(
            m_pNotificationClient);
    }

    HRR(m_deviceEnumerator->EnumAudioEndpoints(
        m_dataFlow, DEVICE_STATE_ACTIVE, &m_deviceCollection));

    UINT nDevices = 0;
    HRG(m_deviceCollection->GetCount(&nDevices));

    for (UINT i=0; i<nDevices; ++i) {
        wchar_t name[WW_DEVICE_NAME_COUNT];
        HRG(DeviceNameGet(m_deviceCollection, i, name, sizeof name));

        /* CMDコンソールに出力する場合、文字化けして表示が乱れるので?に置換する。
        for (int j=0; j<wcslen(name); ++j) {
            if (name[j] < 0x20 || 127 <= name[j]) {
                name[j] = L'?';
            }
        }
        */

        m_deviceInfo.push_back(WWDeviceInfo(i, name));
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
    assert(0 <= id && id < (int)m_deviceInfo.size());

    wcsncpy_s(name, nameBytes/2, m_deviceInfo[id].name, nameBytes/sizeof name[0] -1);
    return true;
}

struct TestCaseInfo {
    int sampleRate;
    int bitsPerSample;
    int validBitsPerSample;
    int bitFormat; // 0:Int, 1:Float
};

const int TEST_SAMPLE_RATE_NUM = 8;
const int TEST_BIT_REPRESENTATION_NUM = 5;

const static TestCaseInfo g_testCases[] = {
    {44100, 16, 16, 0},
    {48000, 16, 16, 0},
    {88200, 16, 16, 0},
    {96000, 16, 16, 0},
    {176400, 16, 16, 0},
    {192000, 16, 16, 0},
    {352800, 16, 16, 0},
    {384000, 16, 16, 0},

    {44100, 24, 24, 0},
    {48000, 24, 24, 0},
    {88200, 24, 24, 0},
    {96000, 24, 24, 0},
    {176400, 24, 24, 0},
    {192000, 24, 24, 0},
    {352800, 24, 24, 0},
    {384000, 24, 24, 0},

    {44100, 32, 24, 0},
    {48000, 32, 24, 0},
    {88200, 32, 24, 0},
    {96000, 32, 24, 0},
    {176400, 32, 24, 0},
    {192000, 32, 24, 0},
    {352800, 32, 24, 0},
    {384000, 32, 24, 0},

    {44100, 32, 32, 0},
    {48000, 32, 32, 0},
    {88200, 32, 32, 0},
    {96000, 32, 32, 0},
    {176400, 32, 32, 0},
    {192000, 32, 32, 0},
    {352800, 32, 32, 0},
    {384000, 32, 32, 0},

    {44100, 32, 32, 1},
    {48000, 32, 32, 1},
    {88200, 32, 32, 1},
    {96000, 32, 32, 1},
    {176400, 32, 32, 1},
    {192000, 32, 32, 1},
    {352800, 32, 32, 1},
    {384000, 32, 32, 1},
};

bool
WasapiUser::InspectDevice(int id, LPWSTR result, size_t resultBytes)
{
    HRESULT hr;
    WAVEFORMATEX *waveFormat = NULL;
    REFERENCE_TIME hnsDefaultDevicePeriod;
    REFERENCE_TIME hnsMinimumDevicePeriod;

    assert(0 <= id && id < (int)m_deviceInfo.size());

    assert(m_deviceCollection);
    assert(!m_deviceToUse);

    result[0] = 0;

    const wchar_t *bitFormatNameShortList[] = { L"i", L"f"};

    HRESULT resultList[sizeof g_testCases/sizeof g_testCases[0]];
    memset(resultList, 0xff, sizeof resultList);

    HRG(m_deviceCollection->Item(id, &m_deviceToUse));

    HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL, (void**)&m_audioClient));

    for (int i=0; i<sizeof g_testCases/sizeof g_testCases[0]; ++i) {
        const TestCaseInfo *tci = &g_testCases[i];

        assert(!waveFormat);
        HRG(m_audioClient->GetMixFormat(&waveFormat));
        assert(waveFormat);

        WAVEFORMATEXTENSIBLE * wfex = (WAVEFORMATEXTENSIBLE*)waveFormat;

        dprintf("original Mix Format:\n");
        WWWaveFormatDebug(waveFormat);
        WWWFEXDebug(wfex);

        if (waveFormat->wFormatTag != WAVE_FORMAT_EXTENSIBLE) {
            dprintf("E: unsupported device ! mixformat == 0x%08x\n",
                waveFormat->wFormatTag);
            hr = E_FAIL;
            goto end;
        }

        if (tci->bitFormat == 0) {
            wfex->SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
        } else {
            wfex->SubFormat = KSDATAFORMAT_SUBTYPE_IEEE_FLOAT;
        }

        wfex->Format.wBitsPerSample = (WORD)tci->bitsPerSample;
        wfex->Format.nSamplesPerSec = tci->sampleRate;
        wfex->Format.nBlockAlign =
            (WORD)((tci->bitsPerSample / 8) * waveFormat->nChannels);
        wfex->Format.nAvgBytesPerSec =
            wfex->Format.nSamplesPerSec*wfex->Format.nBlockAlign;
        wfex->Samples.wValidBitsPerSample = (WORD)tci->validBitsPerSample;

        dprintf("preferred Format:\n");
        WWWaveFormatDebug(waveFormat);
        WWWFEXDebug(wfex);

        hr = m_audioClient->IsFormatSupported(
            m_shareMode,waveFormat,NULL);
        dprintf("IsFormatSupported=%08x\n", hr);

        resultList[i] = hr;

        if (waveFormat) {
            CoTaskMemFree(waveFormat);
            waveFormat = NULL;
        }
    }

    int count = 0;
    for (int j=0; j<TEST_BIT_REPRESENTATION_NUM; ++j) {
        wcsncat_s(result, resultBytes/2,
            L"++-------------++-------------++-------------++-------------"
            L"++-------------++-------------++-------------++-------------++\r\n",
            resultBytes/2 - wcslen(result) -1);
        for (int i=0; i<TEST_SAMPLE_RATE_NUM; ++i) {
            const TestCaseInfo *tci = &g_testCases[count +i];
            wchar_t s[256];
            StringCbPrintfW(s, sizeof s-1,
                L"||%3dkHz %s%dV%d",
                tci->sampleRate/1000, bitFormatNameShortList[tci->bitFormat],
                tci->bitsPerSample, tci->validBitsPerSample);
            wcsncat_s(result, resultBytes/2, s, resultBytes/2 - wcslen(result) -1);
        }
        wcsncat_s(result, resultBytes/2, L"||\r\n", resultBytes/2 - wcslen(result) -1);
        for (int i=0; i<TEST_SAMPLE_RATE_NUM; ++i) {
            wchar_t s[256];
            StringCbPrintfW(s, sizeof s-1,
                L"|| %s %8x ",
                (S_OK==resultList[count + i]) ? L"OK" : L"NA",
                resultList[count + i]);
            wcsncat_s(result, resultBytes/2, s, resultBytes/2 - wcslen(result) -1);
        }
        wcsncat_s(result, resultBytes/2, L"||\r\n", resultBytes/2 - wcslen(result) -1);

        count += TEST_SAMPLE_RATE_NUM;
    }
    wcsncat_s(result, resultBytes/2,
        L"++-------------++-------------++-------------++-------------"
        L"++-------------++-------------++-------------++-------------++\r\n",
        resultBytes/2 - wcslen(result) -1);

    {
        wchar_t s[256];

        HRG(m_audioClient->GetDevicePeriod(
            &hnsDefaultDevicePeriod, &hnsMinimumDevicePeriod));
        StringCbPrintfW(s, sizeof s-1,
            L"  Default scheduling period for a shared-mode stream:    %f ms\r\n"
            L"  Minimum scheduling period for a exclusive-mode stream: %f ms\r\n",
            ((double)hnsDefaultDevicePeriod)*0.0001,
            ((double)hnsMinimumDevicePeriod)*0.0001);
        wcsncat_s(result, resultBytes/2, s, resultBytes/2 - wcslen(result) -1);
    }

end:
    SafeRelease(&m_deviceToUse);
    SafeRelease(&m_audioClient);

    if (waveFormat) {
        CoTaskMemFree(waveFormat);
        waveFormat = NULL;
    }

    return true;
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

end:
    SafeRelease(&m_deviceCollection);
    return hr;
}

void
WasapiUser::UnchooseDevice(void)
{
    dprintf("D: %s()\n", __FUNCTION__);

    SafeRelease(&m_deviceToUse);
    m_useDeviceId = -1;
    m_useDeviceName[0] = 0;
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

HRESULT
WasapiUser::Setup(
        int sampleRate,
        WWPcmDataFormatType format,
        int numChannels)
{
    HRESULT      hr          = 0;
    WAVEFORMATEX *waveFormat = NULL;

    dprintf("D: %s(%d %s %d)\n", __FUNCTION__,
        sampleRate, WWPcmDataFormatTypeToStr(format), numChannels);

    m_sampleRate          = sampleRate;
    m_format              = format;
    m_numChannels         = numChannels;

    // WasapiUserクラスが備えていたサンプルフォーマット変換機能は、廃止した。
    // 上のレイヤーでPCMデータを適切な形式に変換してから渡してください。
    if (WWSMShared == m_shareMode) {
        assert(WWPcmDataFormatTypeToBitsPerSample(m_format) == 32);
        assert(WWPcmDataFormatTypeIsFloat(m_format));
    }

    m_audioSamplesReadyEvent =
        CreateEventEx(NULL, NULL, 0, EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_audioSamplesReadyEvent);

    assert(m_deviceToUse);
    assert(!m_audioClient);
    HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL,
        (void**)&m_audioClient));

    assert(!waveFormat);
    HRG(m_audioClient->GetMixFormat(&waveFormat));
    assert(waveFormat);

    WAVEFORMATEXTENSIBLE * wfex = (WAVEFORMATEXTENSIBLE*)waveFormat;

    dprintf("original Mix Format:\n");
    WWWaveFormatDebug(waveFormat);
    WWWFEXDebug(wfex);

    if (waveFormat->nChannels != m_numChannels) {
        dprintf("E: waveFormat->nChannels(%d) != %d\n",
            waveFormat->nChannels, m_numChannels);
        hr = E_FAIL;
        goto end;
    }

    if (waveFormat->wFormatTag != WAVE_FORMAT_EXTENSIBLE) {
        dprintf("E: unsupported device ! mixformat == 0x%08x\n",
            waveFormat->wFormatTag);
        hr = E_FAIL;
        goto end;
    }

    if (WWSMExclusive == m_shareMode) {
        if (WWPcmDataFormatTypeIsInt(m_format)) {
            wfex->SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
        }
        wfex->Format.wBitsPerSample
            = (WORD)WWPcmDataFormatTypeToBitsPerSample(m_format);
        wfex->Format.nSamplesPerSec = sampleRate;

        wfex->Format.nBlockAlign
            = (WORD)((WWPcmDataFormatTypeToBitsPerSample(m_format) / 8)
                * waveFormat->nChannels);
        wfex->Format.nAvgBytesPerSec
            = wfex->Format.nSamplesPerSec*wfex->Format.nBlockAlign;
        wfex->Samples.wValidBitsPerSample
            = (WORD)WWPcmDataFormatTypeToValidBitsPerSample(m_format);

        dprintf("preferred Format:\n");
        WWWaveFormatDebug(waveFormat);
        WWWFEXDebug(wfex);
    
        HRG(m_audioClient->IsFormatSupported(
            m_shareMode,waveFormat,NULL));
    }

    m_frameBytes = waveFormat->nBlockAlign;
    
    DWORD streamFlags = 0;
    int periodsPerBuffer = 1;
    switch (m_dataFeedMode) {
    case WWDFMTimerDriven:
        streamFlags      = AUDCLNT_STREAMFLAGS_NOPERSIST;
        periodsPerBuffer = PERIODS_PER_BUFFER_ON_TIMER_DRIVEN_MODE;
        break;
    case WWDFMEventDriven:
        streamFlags      =
            AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_NOPERSIST;
        periodsPerBuffer = 1;
        break;
    default:
        assert(0);
        break;
    }

    REFERENCE_TIME bufferPeriodicity = m_latencyMillisec * 10000;
    REFERENCE_TIME bufferDuration    = bufferPeriodicity * periodsPerBuffer;

    bool needClockAdjustmentOnSharedMode = false;

    m_deviceSampleRate = waveFormat->nSamplesPerSec;

    if (WWSMShared == m_shareMode) {
        // 共有モードでデバイスサンプルレートと
        // WAVファイルのサンプルレートが異なる場合、
        // 入力サンプリング周波数調整(リサンプリング)を行う。
        // 共有モード イベント駆動の場合、bufferPeriodicityに0をセットする。

        if (waveFormat->nSamplesPerSec != (DWORD)sampleRate) {
            // 共有モードのサンプルレート変更。
            needClockAdjustmentOnSharedMode = true;
            streamFlags |= AUDCLNT_STREAMFLAGS_RATEADJUST;
        }

        if (WWDFMEventDriven == m_dataFeedMode) {
            bufferPeriodicity = 0;
        }
    }

    hr = m_audioClient->Initialize(
        m_shareMode, streamFlags, 
        bufferDuration, bufferPeriodicity, waveFormat, NULL);
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

        HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL,
        (void**)&m_audioClient));

        hr = m_audioClient->Initialize(
            m_shareMode, streamFlags, 
            bufferDuration, bufferPeriodicity, waveFormat, NULL);
    }
    if (FAILED(hr)) {
        dprintf("E: audioClient->Initialize failed 0x%08x\n", hr);
        goto end;
    }

    if (needClockAdjustmentOnSharedMode) {
        assert(!m_audioClockAdjustment);
        HRG(m_audioClient->GetService(IID_PPV_ARGS(&m_audioClockAdjustment)));

        assert(m_audioClockAdjustment);
        HRG(m_audioClockAdjustment->SetSampleRate((float)sampleRate));
        dprintf("IAudioClockAdjustment::SetSampleRate(%d) %08x\n", sampleRate, hr);
    }

    // サンプルレート変更後にGetBufferSizeする。なんとなく。なお
    // サンプルレート変更前にGetBufferSizeしても、もどってくる値は同じだった。

    HRG(m_audioClient->GetBufferSize(&m_bufferFrameNum));
    dprintf("m_audioClient->GetBufferSize() rv=%u\n", m_bufferFrameNum);

    if (WWDFMEventDriven == m_dataFeedMode) {
        HRG(m_audioClient->SetEventHandle(m_audioSamplesReadyEvent));
    }

    switch (m_dataFlow) {
    case eRender:
        HRG(m_audioClient->GetService(IID_PPV_ARGS(&m_renderClient)));
        // 再生前無音、再生後無音の準備。
        m_startSilenceBuffer.Init(-1, m_format, m_numChannels,
            1 * (int)((int64_t)m_sampleRate * m_latencyMillisec / 1000),
            m_frameBytes, WWPcmDataContentSilence);
        m_endSilenceBuffer.Init(-1, m_format, m_numChannels,
            4 * (int)((int64_t)m_sampleRate * m_latencyMillisec / 1000),
            m_frameBytes, WWPcmDataContentSilence);

        // spliceバッファー。サイズは100分の1秒=10ms 適当に選んだ。
        m_spliceBuffer.Init(-1, m_format, m_numChannels,
            m_sampleRate / 100, m_frameBytes, WWPcmDataContentSplice);
        // pauseバッファー。ポーズ時の波形つなぎに使われる。spliceバッファーと同様。
        m_pauseBuffer.Init(-1, m_format, m_numChannels,
            m_sampleRate / 100, m_frameBytes, WWPcmDataContentSplice);
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

void
WasapiUser::Unsetup(void)
{
    dprintf("D: %s() ASRE=%p ACA=%p CC=%p RC=%p AC=%p\n", __FUNCTION__,
        m_audioSamplesReadyEvent, m_audioClockAdjustment, m_captureClient,
        m_renderClient, m_audioClient);

    if (m_audioSamplesReadyEvent) {
        CloseHandle(m_audioSamplesReadyEvent);
        m_audioSamplesReadyEvent = NULL;
    }

    ClearPlayPcmData();

    SafeRelease(&m_audioClockAdjustment);
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

    assert(m_nowPlayingPcmData);

    HRG(m_audioClient->Reset());

    assert(!m_shutdownEvent);
    m_shutdownEvent = CreateEventEx(NULL, NULL, 0,
        EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_shutdownEvent);

    switch (m_dataFlow) {
    case eRender:
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

            memset(pData, 0, nFrames * m_frameBytes);

            HRG(m_renderClient->ReleaseBuffer(nFrames, 0));
        }

        m_footerCount = 0;

        break;

    case eCapture:
        m_thread = CreateThread(NULL, 0, CaptureEntry, this, 0, NULL);
        assert(m_thread);

        hr = m_captureClient->GetBuffer(
            &pData, &nFrames, &flags, NULL, NULL);
        if (SUCCEEDED(hr)) {
            m_captureClient->ReleaseBuffer(nFrames);
        } else {
            hr = S_OK;
        }
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

    dprintf("D: %s() AC=%p SE=%p T=%p\n", __FUNCTION__,
        m_audioClient, m_shutdownEvent, m_thread);

    // ポーズ中の場合、ポーズを解除。
    m_pauseResumePcmData = NULL;

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
        WWPcmData *nowPlaying = m_nowPlayingPcmData;

        dprintf("%s nowPlaying=%p posFrame=%d splice=%p startSilence=%p endSilence=%p pause=%p\n", __FUNCTION__,
            nowPlaying, (nowPlaying) ? nowPlaying->posFrame : -1, &m_spliceBuffer, &m_startSilenceBuffer, &m_endSilenceBuffer, &m_pauseBuffer);

        if (nowPlaying && nowPlaying != &m_startSilenceBuffer && nowPlaying != &m_endSilenceBuffer && nowPlaying != &m_pauseBuffer &&
            nowPlaying != &m_spliceBuffer) {
            /* 通常データを再生中の場合ポーズが可能。
             * m_nowPlayingPcmDataを
             * pauseBuffer(フェードアウトするPCMデータ)に差し替え、
             * 再生が終わるまでブロッキングで待つ。
             */

            pauseDataSetSucceeded = true;

            m_pauseResumePcmData = nowPlaying;

            m_pauseBuffer.posFrame = 0;
            m_pauseBuffer.next = &m_endSilenceBuffer;

            m_endSilenceBuffer.posFrame = 0;
            m_endSilenceBuffer.next = NULL;

            m_pauseBuffer.UpdateSpliceDataWithStraightLine(
                m_nowPlayingPcmData, m_nowPlayingPcmData->posFrame,
                &m_endSilenceBuffer, m_endSilenceBuffer.posFrame);

            m_nowPlayingPcmData = &m_pauseBuffer;
        }
    }
    ReleaseMutex(m_mutex);

    if (pauseDataSetSucceeded) {
        // ここで再生停止までブロックする。

        WWPcmData *nowPlayingPcmData = NULL;

        do {
            assert(m_mutex);
            WaitForSingleObject(m_mutex, INFINITE);
            nowPlayingPcmData = m_nowPlayingPcmData;
            ReleaseMutex(m_mutex);

            Sleep(100);
        } while (nowPlayingPcmData != NULL);

        /* 再生停止。これは、呼ばなくても良い。
        assert(m_audioClient);
        HRG(m_audioClient->Stop());
        */
    } else {
        dprintf("%s pauseDataSet failed\n", __FUNCTION__);
    }

//end:
    return (pauseDataSetSucceeded) ? S_OK : E_FAIL;
}

HRESULT
WasapiUser::Unpause(void)
{
    //HRESULT hr = S_OK;

    /* 再生するPCMデータへフェードインするPCMデータをpauseBufferにセットして
     * 再生開始する。
     */
    assert(m_pauseResumePcmData);

    dprintf("%s resume=%p posFrame=%d\n", __FUNCTION__,
        m_pauseResumePcmData, m_pauseResumePcmData->posFrame);

    m_startSilenceBuffer.posFrame = 0;
    m_startSilenceBuffer.next = &m_pauseBuffer;

    m_pauseBuffer.posFrame = 0;
    m_pauseBuffer.next = m_pauseResumePcmData;

    m_pauseBuffer.UpdateSpliceDataWithStraightLine(
            &m_startSilenceBuffer, m_startSilenceBuffer.posFrame,
            m_pauseResumePcmData, m_pauseResumePcmData->posFrame);

    assert(m_mutex);
    WaitForSingleObject(m_mutex, INFINITE);
    {
        m_nowPlayingPcmData = &m_startSilenceBuffer;
    }
    ReleaseMutex(m_mutex);

    /* 再生再開。これは、呼ばなくても良い。
    assert(m_audioClient);
    HRG(m_audioClient->Start());
     */

//end:
    m_pauseResumePcmData = NULL;
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
WasapiUser::ClearPlayPcmData(void)
{
    m_spliceBuffer.Term();
    m_pauseBuffer.Term();
    m_startSilenceBuffer.Term();
    m_endSilenceBuffer.Term();

    m_nowPlayingPcmData = NULL;
}

void
WasapiUser::ClearCapturedPcmData(void)
{
    if (m_capturedPcmData) {
        m_capturedPcmData->Term();
        delete m_capturedPcmData;
        m_capturedPcmData = NULL;
    }
}

/// 再生開始直後は、Start無音を再生する。
/// その後startPcmDataを再生する。
/// endPcmDataの次に、End無音を再生する。
/// リピート再生の場合はendPcmData==NULLを渡す。
void
WasapiUser::SetupPlayPcmDataLinklist(
        bool repeat, WWPcmData *startPcmData, WWPcmData *endPcmData)
{
    UpdatePlayRepeat(repeat, startPcmData, endPcmData);

    m_nowPlayingPcmData = &m_startSilenceBuffer;
    m_nowPlayingPcmData->next = startPcmData;
}

void
WasapiUser::UpdatePlayRepeat(bool repeat,
        WWPcmData *startPcmData, WWPcmData *endPcmData)
{
    assert(startPcmData != &m_startSilenceBuffer);
    assert(startPcmData != &m_endSilenceBuffer);
    assert(endPcmData != &m_startSilenceBuffer);
    assert(endPcmData != &m_endSilenceBuffer);

    if (!repeat) {
        // リピートなし。endPcmData→endSilence→NULL
        endPcmData->next = &m_endSilenceBuffer;
    } else {
        // リピートあり。endPcmData→startPcmData
        endPcmData->next = startPcmData;
    }

    m_endSilenceBuffer.next = NULL;
}

int
WasapiUser::GetNowPlayingPcmDataId(void)
{
    WWPcmData *nowPlaying = m_nowPlayingPcmData;

    if (!nowPlaying) {
        return -1;
    }
    return nowPlaying->id;
}

void
WasapiUser::UpdatePlayPcmData(WWPcmData &pcmData)
{
    if (m_thread != NULL) {
        UpdatePlayPcmDataWhenPlaying(pcmData);
    } else {
        UpdatePlayPcmDataWhenNotPlaying(pcmData);
    }
}

void
WasapiUser::UpdatePlayPcmDataWhenNotPlaying(WWPcmData &playPcmData)
{
    m_nowPlayingPcmData = &m_startSilenceBuffer;
    m_nowPlayingPcmData->next = &playPcmData;
}

void
WasapiUser::UpdatePlayPcmDataWhenPlaying(WWPcmData &pcmData)
{
    dprintf("D: %s(%d)\n", __FUNCTION__, pcmData.id);

    assert(m_mutex);
    WaitForSingleObject(m_mutex, INFINITE);
    {
        WWPcmData *nowPlaying = m_nowPlayingPcmData;

        if (nowPlaying) {
            // m_nowPlayingPcmDataをpcmDataに移動する。
#if 1
            // Issue3: いきなり移動するとブチッと言うので
            // splice bufferを経由してなめらかにつなげる。
            m_spliceBuffer.UpdateSpliceDataWithStraightLine(
                m_nowPlayingPcmData, m_nowPlayingPcmData->posFrame,
                &pcmData, pcmData.posFrame);

            if (m_nowPlayingPcmData != &pcmData) {
                // 別の再生曲に移動した場合、
                // それまで再生していた曲は頭出ししておく。
                m_nowPlayingPcmData->posFrame = 0;
            }

            m_spliceBuffer.posFrame = 0;
            m_spliceBuffer.next = &pcmData;

            m_nowPlayingPcmData = &m_spliceBuffer;
#else
            m_nowPlayingPcmData->posFrame = 0;
            m_nowPlayingPcmData = &pcmData;
#endif
        }
    }

    ReleaseMutex(m_mutex);
}

int64_t
WasapiUser::GetTotalFrameNum(void)
{
    WWPcmData *nowPlaying = m_nowPlayingPcmData;

    if (!nowPlaying) {
        return 0;
    }
    return nowPlaying->nFrames;
}

int64_t
WasapiUser::GetPosFrame(void)
{
    int64_t result = 0;

    WWPcmData *nowPlaying = m_nowPlayingPcmData;

    // assert(m_mutex);
    // WaitForSingleObject(m_mutex, INFINITE);
    if (nowPlaying) {
        result = nowPlaying->posFrame;
    }
    //ReleaseMutex(m_mutex);

    return result;
}

bool
WasapiUser::SetPosFrame(int64_t v)
{
    if (m_dataFlow != eRender) {
        assert(0);
        return false;
    }

    if (v < 0 || GetTotalFrameNum() <= v) {
        return false;
    }

    assert(m_mutex);
    WaitForSingleObject(m_mutex, INFINITE);
    {
        if (m_nowPlayingPcmData &&
            m_nowPlayingPcmData->contentType == WWPcmDataContentPcmData) {
            /* nowPlaying->posFrameをvに移動する。
             * Issue3: いきなり移動するとブチッと言うのでsplice bufferを経由してなめらかにつなげる。
             */
            m_spliceBuffer.UpdateSpliceDataWithStraightLine(
                m_nowPlayingPcmData, m_nowPlayingPcmData->posFrame, m_nowPlayingPcmData, v);
            m_spliceBuffer.posFrame = 0;
            m_spliceBuffer.next = m_nowPlayingPcmData;
            //m_spliceBuffer.id = m_nowPlayingPcmData->id;

            m_nowPlayingPcmData->posFrame = v;
            m_nowPlayingPcmData = &m_spliceBuffer;
        }
    }
    ReleaseMutex(m_mutex);

    return true;
}

bool
WasapiUser::SetupCaptureBuffer(int64_t bytes)
{
    if (m_dataFlow != eCapture) {
        assert(0);
        return false;
    }
#ifdef _X86_
    if (0x7fffffffL < bytes) {
        // cannot alloc 2GB buffer on 32bit build
        return false;
    }
#endif

    ClearCapturedPcmData();

    // 録音時は
    //   pcmData->posFrame: 有効な録音データのフレーム数
    //   pcmData->nFrames: 録音可能総フレーム数
    m_capturedPcmData = new WWPcmData();
    m_capturedPcmData->posFrame = 0;
    m_capturedPcmData->nFrames = bytes/m_frameBytes;
    m_capturedPcmData->stream = (BYTE*)malloc(bytes);

    return  m_capturedPcmData->stream != NULL;
}

int64_t
WasapiUser::GetCapturedData(BYTE *data, int64_t bytes)
{
    if (m_dataFlow != eCapture) {
        assert(0);
        return 0;
    }

    assert(m_capturedPcmData);

    if (m_capturedPcmData->posFrame * m_frameBytes < bytes) {
        bytes = m_capturedPcmData->posFrame * m_frameBytes;
    }
    memcpy(data, m_capturedPcmData->stream, bytes);

    return bytes;
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
    WWPcmData *pcmData = m_nowPlayingPcmData;

    while (NULL != pcmData && 0 < wantFrames) {
        int copyFrames = wantFrames;
        if (pcmData->nFrames <= pcmData->posFrame + wantFrames) {
            // pcmDataが持っているフレーム数よりも要求フレーム数が多い。
            copyFrames = (int)(pcmData->nFrames - pcmData->posFrame);
        }

        dprintf("pcmData=%p next=%p posFrame=%lld copyFrames=%d nFrames=%lld\n",
            pcmData, pcmData->next, pcmData->posFrame, copyFrames, pcmData->nFrames);

        CopyMemory(&pData_return[pos*m_frameBytes],
            &pcmData->stream[pcmData->posFrame * m_frameBytes],
            copyFrames * m_frameBytes);

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

    m_nowPlayingPcmData = pcmData;

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
        memset(&to[copyFrames*m_frameBytes], 0,
            (writableFrames - copyFrames)*m_frameBytes);
        /* dprintf("fc=%d bs=%d cb=%d memset %d bytes\n",
            m_footerCount, m_bufferFrameNum, copyFrames,
            (m_bufferFrameNum - copyFrames)*m_frameBytes);
        */
    }

    HRGR(m_renderClient->ReleaseBuffer(writableFrames, 0));
    to = NULL;

    if (NULL == m_nowPlayingPcmData) {
        ++m_footerCount;
        if (m_footerNeedSendCount < m_footerCount) {
            if (NULL != m_pauseResumePcmData) {
                // ポーズ中。スレッドを回し続ける。
            } else {
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

    timeBeginPeriod(1);

    // マルチメディアクラススケジューラーサービスのスレッド優先度設定。
    if (WWSTTNone != m_schedulerTaskType) {
        dprintf("D: %s() AvSetMmThreadCharacteristics(%S)\n",
            __FUNCTION__, WWSchedulerTaskTypeToStr(m_schedulerTaskType));

        mmcssHandle = AvSetMmThreadCharacteristics(
            WWSchedulerTaskTypeToStr(m_schedulerTaskType), &mmcssTaskIndex);
        if (NULL == mmcssHandle) {
            dprintf("Unable to enable MMCSS on render thread: 0x%08x\n",
                GetLastError());
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

    timeEndPeriod(1);

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

    dprintf("D: %s AvSetMmThreadCharacteristics(%S)\n",
        __FUNCTION__,
        WWSchedulerTaskTypeToStr(m_schedulerTaskType));

    mmcssHandle = AvSetMmThreadCharacteristics(
        WWSchedulerTaskTypeToStr(m_schedulerTaskType),
        &mmcssTaskIndex);
    if (NULL == mmcssHandle) {
        dprintf("Unable to enable MMCSS on render thread: 0x%08x\n",
            GetLastError());
    }

    if (m_dataFeedMode == WWDFMTimerDriven) {
        waitArrayCount  = 1;
        timeoutMillisec = m_latencyMillisec / 2;
    } else {
        waitArrayCount  = 2;
        timeoutMillisec = INFINITE;
    }

    while (stillRecording) {
        waitResult = WaitForMultipleObjects(
            waitArrayCount, waitArray, FALSE, timeoutMillisec);
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
    int     writeFrames = 0;

    WaitForSingleObject(m_mutex, INFINITE);

    HRG(m_captureClient->GetNextPacketSize(&packetLength));

    if (packetLength == 0) {
        goto end;
    }
        
    numFramesAvailable = packetLength;
    flags = 0;

    HRG(m_captureClient->GetBuffer(&pData,
        &numFramesAvailable, &flags, &devicePosition, NULL));

    if ((m_capturedPcmData->nFrames - m_capturedPcmData->posFrame)
        < (int)numFramesAvailable) {
        HRG(m_captureClient->ReleaseBuffer(numFramesAvailable));
        result = false;
        goto end;
    }

    if (flags & AUDCLNT_BUFFERFLAGS_DATA_DISCONTINUITY) {
        ++m_glitchCount;
    }

    writeFrames = (int)(numFramesAvailable);

    if (flags & AUDCLNT_BUFFERFLAGS_SILENT) {
        // 無音を録音した。
        dprintf("flags & AUDCLNT_BUFFERFLAGS_SILENT\n");
        memset(&m_capturedPcmData->stream[m_capturedPcmData->posFrame * m_frameBytes],
            0, writeFrames * m_frameBytes);
    } else {
        dprintf("numFramesAvailable=%u fb=%d pos=%lld devPos=%llu nextPos=%lld te=%d\n",
            numFramesAvailable, m_frameBytes,
            m_capturedPcmData->posFrame,
            devicePosition,
            (m_capturedPcmData->posFrame + numFramesAvailable),
            !!(flags & AUDCLNT_BUFFERFLAGS_TIMESTAMP_ERROR));

        memcpy(&m_capturedPcmData->stream[m_capturedPcmData->posFrame * m_frameBytes],
            pData, writeFrames * m_frameBytes);
    }
    m_capturedPcmData->posFrame += writeFrames;

    HRG(m_captureClient->ReleaseBuffer(numFramesAvailable));

end:
    ReleaseMutex(m_mutex);
    return result;
}

/////////////////////////////////////////////////////////////////////////////
// 設定取得

int
WasapiUser::GetPcmDataSampleRate(void)
{
    return m_sampleRate;
}

int
WasapiUser::GetMixFormatSampleRate(void)
{
#if 1
    return m_deviceSampleRate;
#else
    HRESULT      hr          = 0;
    WAVEFORMATEX *waveFormat = NULL;
    int          sampleRate  = 0;

    if (NULL == m_audioClient) {
        return 0;
    }

    HRG(m_audioClient->GetMixFormat(&waveFormat));
    assert(waveFormat);

    sampleRate = (int)waveFormat->nSamplesPerSec;

end:
    if (waveFormat) {
        CoTaskMemFree(waveFormat);
        waveFormat = NULL;
    }
    return sampleRate;
#endif
}

WWPcmDataFormatType
WasapiUser::GetMixFormatType(void)
{
    return m_format;
}

int
WasapiUser::GetPcmDataNumChannels(void)
{
    return m_numChannels;
}

int
WasapiUser::GetPcmDataFrameBytes(void)
{
    return m_frameBytes;
}

void
WasapiUser::DeviceStateChanged(void)
{
    if (m_stateChangedCallback) {
        m_stateChangedCallback();
    }
}
