#include "WasapiIOIF.h"
#include "WasapiUser.h"
#include "WWPlayPcmGroup.h"
#include "WWUtil.h"
#include "WWTimerResolution.h"
#include <assert.h>
#include <map>

struct WasapiIO {
    WasapiUser     wasapi;
    WWPlayPcmGroup playPcmGroup;
    WWTimerResolution timerResolution;
    int            instanceId;
    static int     sNextInstanceId;

    int GetInstanceId(void) const { return instanceId; }

    HRESULT Init(void);
    void Term(void);

    void UpdatePlayRepeat(bool repeat);
    bool AddPcmDataStart(void);
    HRESULT ResampleIfNeeded(int conversionQuality);
    void AddPcmDataEnd(void);

    HRESULT StartPlayback(int wavDataId);
    HRESULT StartRecording(void);

    double ScanPcmMaxAbsAmplitude(void);
    void ScalePcmAmplitude(double scale);

    bool ConnectPcmDataNext(int fromIdx, int toIdx);
};

int WasapiIO::sNextInstanceId = 0;

HRESULT
WasapiIO::Init(void)
{
    HRESULT hr;
    
    hr = wasapi.Init();
    playPcmGroup.Term();

    if (SUCCEEDED(hr)) {
        instanceId = sNextInstanceId;
        ++sNextInstanceId;
    }

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
        wasapi.PcmStream().UpdatePlayRepeat(repeat, first, last);
    }
}

bool
WasapiIO::ConnectPcmDataNext(int fromIdx, int toIdx)
{
    WWPcmData *from = playPcmGroup.FindPcmDataById(fromIdx);
    WWPcmData *to = playPcmGroup.FindPcmDataById(toIdx);

    if (NULL == from || NULL == to) {
        return false;
    }

    wasapi.MutexWait();
    from->next = to;
    wasapi.MutexRelease();

    return true;
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
WasapiIO::ResampleIfNeeded(int conversionQuality)
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
        conversionQuality);
    if (FAILED(hr)) {
        return hr;
    }

    wasapi.UpdatePcmDataFormat(wasapi.GetDeviceSampleRate(), WWPcmDataSampleFormatSfloat, wasapi.GetDeviceNumChannels(), wasapi.GetDeviceDwChannelMask());

    return hr;
}

double
WasapiIO::ScanPcmMaxAbsAmplitude(void)
{
    float minResult = FLT_MAX;
    float maxResult = FLT_MIN;

    for (int i=0; i<playPcmGroup.Count(); ++i) {
        WWPcmData *pcm = playPcmGroup.NthPcmData(i);
        assert(pcm);

        float minV, maxV;
        pcm->FindSampleValueMinMax(&minV, &maxV);

        if (minV < minResult) {
            minResult = minV;
        }
        if (maxResult < maxV) {
            maxResult = maxV;
        }
    }
    
    return max(fabsf(maxResult), fabsf(minResult));
}

void
WasapiIO::ScalePcmAmplitude(double scale)
{
    for (int i=0; i<playPcmGroup.Count(); ++i) {
        WWPcmData *pcm = playPcmGroup.NthPcmData(i);
        assert(pcm);

        pcm->ScaleSampleValue((float)scale);
    }
}

void
WasapiIO::AddPcmDataEnd(void)
{
    playPcmGroup.AddPlayPcmDataEnd();

    // リピートなしと仮定してリンクリストをつなげておく。
    UpdatePlayRepeat(false);
}

HRESULT
WasapiIO::StartPlayback(int wavDataId)
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

HRESULT
WasapiIO::StartRecording(void)
{
    return wasapi.Start();
}

static std::map<int, WasapiIO *> gSelf;

static WasapiIO *
Instance(int id)
{
    if (id < 0) {
        return NULL;
    }

    std::map<int, WasapiIO *>::iterator ite = gSelf.find(id);
    if (ite == gSelf.end()) {
        return NULL;
    }

    return ite->second;
}

////////////////////////////////////////////////////////////////////////////////

extern "C" {

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_Init(int *instanceId_return)
{
    HRESULT hr = S_OK;

    WasapiIO * self = new WasapiIO();
    if (self == NULL) {
        return E_FAIL;
    }

    hr = self->Init();
    if (FAILED(hr)) {
        return hr;
    }

    *instanceId_return = self->GetInstanceId();
    gSelf[*instanceId_return] = self;
    return hr;
}

__declspec(dllexport)
void __stdcall
WasapiIO_Term(int instanceId)
{
    std::map<int, WasapiIO *>::iterator ite = gSelf.find(instanceId);
    if (ite == gSelf.end()) {
        assert(0);
        return;
    }

    ite->second->Term();
    SAFE_DELETE(ite->second);
    gSelf.erase(ite);
}

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_EnumerateDevices(int instanceId, int deviceType)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    WWDeviceType t = (WWDeviceType)deviceType;
    return self->wasapi.DoDeviceEnumeration(t);
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceCount(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->wasapi.GetDeviceCount();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceAttributes(int instanceId, int deviceId, WasapiIoDeviceAttributes &attr_return)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    attr_return.deviceId = deviceId;
    if (!self->wasapi.GetDeviceName(deviceId, attr_return.name, sizeof attr_return.name)) {
        return false;
    }
    if (!self->wasapi.GetDeviceIdString(deviceId, attr_return.deviceIdString, sizeof attr_return.deviceIdString)) {
        return false;
    }
    return true;
}

__declspec(dllexport)
int __stdcall
WasapiIO_InspectDevice(int instanceId, int deviceId, int sampleRate, int bitsPerSample, int validBitsPerSample, int bitFormat)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->wasapi.InspectDevice(deviceId, sampleRate, bitsPerSample, validBitsPerSample, bitFormat);
}

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_ChooseDevice(int instanceId, int deviceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->wasapi.ChooseDevice(deviceId);
}

__declspec(dllexport)
void __stdcall
WasapiIO_UnchooseDevice(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    self->wasapi.UnchooseDevice();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_GetUseDeviceAttributes(int instanceId, WasapiIoDeviceAttributes &attr_return)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return WasapiIO_GetDeviceAttributes(instanceId, self->wasapi.GetUseDeviceId(), attr_return);
}

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_Setup(int instanceId, const WasapiIoSetupArgs &args)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    self->wasapi.SetStreamType((WWStreamType)args.streamType);
    self->wasapi.SetShareMode((WWShareMode)args.shareMode);
    self->wasapi.SetSchedulerTaskType((WWSchedulerTaskType)args.schedulerTask);
    self->wasapi.SetDataFeedMode((WWDataFeedMode)args.dataFeedMode);
    self->wasapi.SetLatencyMillisec((DWORD)args.latencyMillisec);
    self->wasapi.PcmStream().SetZeroFlushMillisec(args.zeroFlushMillisec);
    self->wasapi.TimerResolution().SetTimePeriodHundredNanosec(args.timePeriodHandledNanosec);

    return self->wasapi.Setup(
        args.sampleRate, (WWPcmDataSampleFormatType)args.sampleFormat, args.numChannels);
}

__declspec(dllexport)
void __stdcall
WasapiIO_Unsetup(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    self->wasapi.Unsetup();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataStart(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);

    return self->AddPcmDataStart();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmData(int instanceId, int pcmId, unsigned char *data, int64_t bytes)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->playPcmGroup.AddPlayPcmData(pcmId, data, bytes);
}

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataSetPcmFragment(int instanceId, int pcmId, int64_t posBytes, unsigned char *data, int64_t bytes)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
#ifdef _X86_
    if (0x7fffffffL < posBytes + bytes) {
        // cannot alloc 2GB buffer on 32bit build
        return false;
    }
#endif

    WWPcmData *p = self->playPcmGroup.FindPcmDataById(pcmId);
    if (NULL == p) {
        return false;
    }

    assert(posBytes + bytes <= p->nFrames * p->bytesPerFrame);

    memcpy(&p->stream[posBytes], data, bytes);
    return true;
}

__declspec(dllexport)
int __stdcall
WasapiIO_ResampleIfNeeded(int instanceId, int conversionQuality)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->ResampleIfNeeded(conversionQuality);
}

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataEnd(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);

    self->AddPcmDataEnd();

    return true;
}

__declspec(dllexport)
void __stdcall
WasapiIO_RemovePlayPcmDataAt(int instanceId, int pcmId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    self->playPcmGroup.RemoveAt(pcmId);
    self->UpdatePlayRepeat(self->playPcmGroup.GetRepatFlag());
}

__declspec(dllexport)
void __stdcall
WasapiIO_ClearPlayList(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    self->playPcmGroup.Clear();
}

__declspec(dllexport)
void __stdcall
WasapiIO_SetPlayRepeat(int instanceId, bool b)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);

    self->UpdatePlayRepeat(b);
}

__declspec(dllexport)
bool __stdcall
WasapiIO_ConnectPcmDataNext(int instanceId, int fromIdx, int toIdx)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);

    return self->ConnectPcmDataNext(fromIdx, toIdx);
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataId(int instanceId, int usageType)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->wasapi.PcmStream().GetPcmDataId((WWPcmDataUsageType)usageType);
}

__declspec(dllexport)
void __stdcall
WasapiIO_SetNowPlayingPcmDataId(int instanceId, int pcmId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);

    WWPcmData *p = self->playPcmGroup.FindPcmDataById(pcmId);
    if (NULL == p) {
        dprintf("%s(%d) PcmData not found\n",
            __FUNCTION__, pcmId);
        return;
    }

    self->wasapi.UpdatePlayPcmData(*p);
}

__declspec(dllexport)
int64_t __stdcall
WasapiIO_GetCaptureGlitchCount(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->wasapi.GetCaptureGlitchCount();
}

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_StartPlayback(int instanceId, int wavDataId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);

    return self->StartPlayback(wavDataId);
}

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_StartRecording(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);

    return self->StartRecording();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_Run(int instanceId, int millisec)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->wasapi.Run(millisec);
}

__declspec(dllexport)
void __stdcall
WasapiIO_Stop(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    self->wasapi.Stop();
}

__declspec(dllexport)
int __stdcall
WasapiIO_Pause(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->wasapi.Pause();
}

__declspec(dllexport)
int __stdcall
WasapiIO_Unpause(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->wasapi.Unpause();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_GetPlayCursorPosition(int instanceId, int usageType, WasapiIoCursorLocation &pos_return)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    pos_return.posFrame      = self->wasapi.PcmStream().PosFrame(     (WWPcmDataUsageType)usageType);
    pos_return.totalFrameNum = self->wasapi.PcmStream().TotalFrameNum((WWPcmDataUsageType)usageType);
    return true;
}

__declspec(dllexport)
bool __stdcall
WasapiIO_SetPosFrame(int instanceId, int64_t v)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->wasapi.SetPosFrame(v);
}

__declspec(dllexport)
bool __stdcall
WasapiIO_GetSessionStatus(int instanceId, WasapiIoSessionStatus &stat_return)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);

    stat_return.streamType          = self->wasapi.StreamType();
    stat_return.pcmDataSampleRate   = self->wasapi.GetPcmDataSampleRate();
    stat_return.deviceSampleRate    = self->wasapi.GetDeviceSampleRate();
    stat_return.deviceSampleFormat  = self->wasapi.GetDeviceSampleFormat();
    stat_return.deviceBytesPerFrame = self->wasapi.GetDeviceBytesPerFrame();

    stat_return.deviceNumChannels        = self->wasapi.GetDeviceNumChannels();
    stat_return.timePeriodHandledNanosec = self->wasapi.TimerResolution().GetTimePeriodHundredNanosec();
    stat_return.bufferFrameNum           = self->wasapi.GetEndpointBufferFrameNum();

    return true;
}

__declspec(dllexport)
void __stdcall
WasapiIO_RegisterStateChangedCallback(int instanceId, WWStateChanged callback)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    self->wasapi.RegisterStateChangedCallback(callback);
}

__declspec(dllexport)
double __stdcall
WasapiIO_ScanPcmMaxAbsAmplitude(int instanceId)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->ScanPcmMaxAbsAmplitude();
}

__declspec(dllexport)
void __stdcall
WasapiIO_ScalePcmAmplitude(int instanceId, double scale)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    return self->ScalePcmAmplitude(scale);
}

__declspec(dllexport)
void __stdcall
WasapiIO_RegisterCaptureCallback(int instanceId, WWCaptureCallback callback)
{
    WasapiIO *self = Instance(instanceId);
    assert(self);
    self->wasapi.RegisterCaptureCallback(callback);
}

}; // extern "C"
