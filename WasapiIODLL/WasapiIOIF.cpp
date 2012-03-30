#include "WasapiIOIF.h"
#include "WasapiUser.h"
#include "WWPlayPcmGroup.h"
#include "WWUtil.h"
#include <assert.h>

struct WasapiIO {
    WasapiUser     wasapi;
    WWPlayPcmGroup playPcmGroup;

    HRESULT Init(void);
    void Term(void);

    void UpdatePlayRepeat(bool repeat);
    bool AddPcmDataStart(void);

    HRESULT Start(int wavDataId);
};

HRESULT
WasapiIO::Init(void)
{
    HRESULT hr;
    
    hr = wasapi.Init();
    playPcmGroup.Term();

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
    int sampleRate  = wasapi.GetPcmDataSampleRate();
    int numChannels = wasapi.GetPcmDataNumChannels();
    int frameBytes  = wasapi.GetPcmDataFrameBytes();
    WWPcmDataFormatType format = wasapi.GetMixFormatType();

    return playPcmGroup.AddPlayPcmDataStart(
        sampleRate, format, numChannels, frameBytes);
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
bool __stdcall
WasapiIO_InspectDevice(int id, LPWSTR result, int resultBytes)
{
    assert(self);
    return self->wasapi.InspectDevice(id, result, resultBytes);
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
WasapiIO_Setup(int sampleRate, int format, int numChannels)
{
    assert(self);
    return self->wasapi.Setup(
        sampleRate, (WWPcmDataFormatType)format, numChannels);
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

    assert(posBytes + bytes <= p->nFrames * p->frameBytes);

    memcpy(&p->stream[posBytes], data, bytes);
    return true;
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataEnd(void)
{
    assert(self);

    bool result = self->playPcmGroup.AddPlayPcmDataEnd();

    // リピートなしと仮定してリンクリストをつなげておく。
    self->UpdatePlayRepeat(false);

    return result;
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
WasapiIO_GetNowPlayingPcmDataId(void)
{
    assert(self);
    return self->wasapi.GetNowPlayingPcmDataId();
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
WasapiIO_GetPosFrame(void)
{
    assert(self);
    return self->wasapi.GetPosFrame();
}

extern "C" __declspec(dllexport)
int64_t __stdcall
WasapiIO_GetTotalFrameNum(void)
{
    assert(self);
    return self->wasapi.GetTotalFrameNum();
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
WasapiIO_GetMixFormatSampleRate(void)
{
    assert(self);
    return self->wasapi.GetMixFormatSampleRate();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetMixFormatType(void)
{
    assert(self);
    return self->wasapi.GetMixFormatType();
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
WasapiIO_GetPcmDataFrameBytes(void)
{
    assert(self);
    return self->wasapi.GetPcmDataFrameBytes();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataNumChannels(void)
{
    assert(self);
    return self->wasapi.GetPcmDataNumChannels();
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
WasapiIO_SetZeroFlushMillisec(int zeroFlushMillisec)
{
    assert(self);
    self->wasapi.SetZeroFlushMillisec(zeroFlushMillisec);
}
