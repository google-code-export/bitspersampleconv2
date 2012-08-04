#include "WasapiIOIF.h"
#include "WasapiUser.h"
#include "WWPlayPcmGroup.h"
#include "WWUtil.h"
#include <assert.h>

struct WasapiIO {
    WasapiUser     wasapi;
    WWPlayPcmGroup playPcmGroup;
    int mResamplerConversionQuality;

    HRESULT Init(void);
    void Term(void);

    void UpdatePlayRepeat(bool repeat);
    bool AddPcmDataStart(void);
    HRESULT ResampleIfNeeded(void);
    void AddPcmDataEnd(void);

    HRESULT Start(int wavDataId);

    void SetResamplerConversionQuality(int quality) {
        mResamplerConversionQuality = quality;
    }
};

HRESULT
WasapiIO::Init(void)
{
    HRESULT hr;
    
    hr = wasapi.Init();
    playPcmGroup.Term();
    mResamplerConversionQuality = 60;

    return hr;
}

void
WasapiIO::Term(void)
{
    wasapi.Term();
    playPcmGroup.Term();
}

void
WasapiIO::UpdatePlayRepeat(bool repeat)
{
    WWPcmData *first = playPcmGroup.FirstPcmData();
    WWPcmData *last  = playPcmGroup.LastPcmData();

    if (NULL != first && NULL != last) {
        playPcmGroup.SetPlayRepeat(repeat);
        wasapi.UpdatePlayRepeat(repeat, first, last);
    }
}

bool
WasapiIO::AddPcmDataStart(void)
{
    WWPcmDataSampleFormatType sampleFormat = wasapi.GetPcmDataSampleFormat();
    int sampleRate      = wasapi.GetPcmDataSampleRate();
    int numChannels     = wasapi.GetPcmDataNumChannels();
    DWORD dwChannelMask = wasapi.GetPcmDataDwChannelMask();
    int bytesPerFrame   = numChannels * WWPcmDataSampleFormatTypeToBitsPerSample(sampleFormat) / 8;

    return playPcmGroup.AddPlayPcmDataStart(
        sampleRate, sampleFormat, numChannels, dwChannelMask, bytesPerFrame);
}

HRESULT
WasapiIO::ResampleIfNeeded(void)
{
    HRESULT hr;

    if (!wasapi.IsResampleNeeded()) {
        return S_OK;
    }

    hr = playPcmGroup.DoResample(
        wasapi.GetDeviceSampleRate(),
        WWPcmDataSampleFormatSfloat,
        wasapi.GetDeviceNumChannels(),
        wasapi.GetDeviceDwChannelMask(),
        mResamplerConversionQuality);
    if (FAILED(hr)) {
        return hr;
    }

    wasapi.UpdatePcmDataFormat(wasapi.GetDeviceSampleRate(), WWPcmDataSampleFormatSfloat, wasapi.GetDeviceNumChannels(), wasapi.GetDeviceDwChannelMask());

    return hr;
}

void
WasapiIO::AddPcmDataEnd(void)
{
    playPcmGroup.AddPlayPcmDataEnd();

    // リピートなしと仮定してリンクリストをつなげておく。
    UpdatePlayRepeat(false);
}

HRESULT
WasapiIO::Start(int wavDataId)
{
    WWPcmData *p = playPcmGroup.FindPcmDataById(wavDataId);
    if (NULL == p) {
        dprintf("%s(%d) PcmData is not found\n",
            __FUNCTION__, wavDataId);
        return E_FAIL;
    }

    wasapi.UpdatePlayPcmData(*p);
    return wasapi.Start();
}

static WasapiIO * self = NULL;

////////////////////////////////////////////////////////////////////////////////

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Init(void)
{
    HRESULT hr = S_OK;

    if(!self) {
        self = new WasapiIO();
        hr = self->Init();
    }

    return hr;
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Term(void)
{
    if (self) {
        self->Term();
        delete self;
        self = NULL;
    }
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetSchedulerTaskType(int t)
{
    assert(self);
    self->wasapi.SetSchedulerTaskType((WWSchedulerTaskType)t);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetShareMode(int sm)
{
    assert(self);
    self->wasapi.SetShareMode((WWShareMode)sm);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetDataFeedMode(int dfm)
{
    assert(self);
    self->wasapi.SetDataFeedMode((WWDataFeedMode)dfm);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetLatencyMillisec(int ms)
{
    assert(self);
    self->wasapi.SetLatencyMillisec((DWORD)ms);
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_DoDeviceEnumeration(int deviceType)
{
    assert(self);
    WWDeviceType t = (WWDeviceType)deviceType;
    return self->wasapi.DoDeviceEnumeration(t);
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceCount(void)
{
    assert(self);
    return self->wasapi.GetDeviceCount();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceName(int id, LPWSTR name, int nameBytes)
{
    assert(self);
    return self->wasapi.GetDeviceName(id, name, nameBytes);
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceIdString(int id, LPWSTR idStr, int idStrBytes)
{
    assert(self);
    return self->wasapi.GetDeviceIdString(id, idStr, idStrBytes);
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_InspectDevice(int id, int sampleRate, int bitsPerSample, int validBitsPerSample, int bitFormat)
{
    assert(self);
    return self->wasapi.InspectDevice(id, sampleRate, bitsPerSample, validBitsPerSample, bitFormat);
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_ChooseDevice(int id)
{
    assert(self);
    return self->wasapi.ChooseDevice(id);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_UnchooseDevice(void)
{
    assert(self);
    self->wasapi.UnchooseDevice();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetUseDeviceId(void)
{
    assert(self);
    return self->wasapi.GetUseDeviceId();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetUseDeviceName(LPWSTR name, int nameBytes)
{
    assert(self);
    return self->wasapi.GetUseDeviceName(name, nameBytes);
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetUseDeviceIdString(LPWSTR idStr, int idStrBytes)
{
    assert(self);
    return self->wasapi.GetUseDeviceIdString(idStr, idStrBytes);
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Setup(int sampleRate, int sampleFormat, int numChannels)
{
    assert(self);
    return self->wasapi.Setup(
        sampleRate, (WWPcmDataSampleFormatType)sampleFormat, numChannels);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Unsetup(void)
{
    assert(self);
    self->wasapi.Unsetup();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataStart(void)
{
    assert(self);

    return self->AddPcmDataStart();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmData(int id, unsigned char *data, int64_t bytes)
{
    assert(self);
    return self->playPcmGroup.AddPlayPcmData(id, data, bytes);
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataSetPcmFragment(int id, int64_t posBytes, unsigned char *data, int64_t bytes)
{
    assert(self);
#ifdef _X86_
    if (0x7fffffffL < posBytes + bytes) {
        // cannot alloc 2GB buffer on 32bit build
        return false;
    }
#endif

    WWPcmData *p = self->playPcmGroup.FindPcmDataById(id);
    if (NULL == p) {
        return false;
    }

    assert(posBytes + bytes <= p->nFrames * p->bytesPerFrame);

    memcpy(&p->stream[posBytes], data, bytes);
    return true;
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_ResampleIfNeeded(void)
{
    assert(self);
    return self->ResampleIfNeeded();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataEnd(void)
{
    assert(self);

    self->AddPcmDataEnd();

    return true;
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_RemovePlayPcmDataAt(int id)
{
    assert(self);
    self->playPcmGroup.RemoveAt(id);
    self->UpdatePlayRepeat(self->playPcmGroup.GetRepatFlag());
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_ClearPlayList(void)
{
    assert(self);
    self->playPcmGroup.Clear();
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetPlayRepeat(bool b)
{
    assert(self);

    self->UpdatePlayRepeat(b);
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataId(int usageType)
{
    assert(self);
    return self->wasapi.GetPcmDataId((WWPcmDataUsageType)usageType);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetNowPlayingPcmDataId(int id)
{
    assert(self);

    WWPcmData *p = self->playPcmGroup.FindPcmDataById(id);
    if (NULL == p) {
        dprintf("%s(%d) PcmData not found\n",
            __FUNCTION__, id);
        return;
    }

    self->wasapi.UpdatePlayPcmData(*p);
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_SetupCaptureBuffer(int64_t bytes)
{
    assert(self);
    return self->wasapi.SetupCaptureBuffer(bytes);
}

extern "C" __declspec(dllexport)
int64_t __stdcall
WasapiIO_GetCapturedData(unsigned char *data, int64_t bytes)
{
    assert(self);
    return self->wasapi.GetCapturedData(data, bytes);
}

extern "C" __declspec(dllexport)
int64_t __stdcall
WasapiIO_GetCaptureGlitchCount(void)
{
    assert(self);
    return self->wasapi.GetCaptureGlitchCount();
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Start(int wavDataId)
{
    assert(self);

    return self->Start(wavDataId);
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_Run(int millisec)
{
    assert(self);
    return self->wasapi.Run(millisec);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Stop(void)
{
    assert(self);
    self->wasapi.Stop();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_Pause(void)
{
    assert(self);
    return self->wasapi.Pause();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_Unpause(void)
{
    assert(self);
    return self->wasapi.Unpause();
}

extern "C" __declspec(dllexport)
int64_t __stdcall
WasapiIO_GetPosFrame(int usageType)
{
    assert(self);
    return self->wasapi.GetPosFrame((WWPcmDataUsageType)usageType);
}

extern "C" __declspec(dllexport)
int64_t __stdcall
WasapiIO_GetTotalFrameNum(int usageType)
{
    assert(self);
    return self->wasapi.GetTotalFrameNum((WWPcmDataUsageType)usageType);
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_SetPosFrame(int64_t v)
{
    assert(self);
    return self->wasapi.SetPosFrame(v);
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceSampleRate(void)
{
    assert(self);
    return self->wasapi.GetDeviceSampleRate();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceSampleFormat(void)
{
    assert(self);
    return self->wasapi.GetDeviceSampleFormat();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataSampleRate(void)
{
    assert(self);
    return self->wasapi.GetPcmDataSampleRate();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceBytesPerFrame(void)
{
    assert(self);
    return self->wasapi.GetDeviceBytesPerFrame();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceNumChannels(void)
{
    assert(self);
    return self->wasapi.GetDeviceNumChannels();
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_RegisterCallback(WWStateChanged callback)
{
    assert(self);
    self->wasapi.RegisterCallback(callback);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetZeroFlushMillisec(int millisec)
{
    assert(self);
    self->wasapi.SetZeroFlushMillisec(millisec);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetTimePeriodMillisec(int millisec)
{
    assert(self);
    self->wasapi.SetTimePeriodMillisec(millisec);
}
extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetResamplerConversionQuality(int quality)
{
    assert(self);
    self->SetResamplerConversionQuality(quality);
}
