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
    HRESULT ResampleIfNeeded(int conversionQuality);
    void AddPcmDataEnd(void);

    HRESULT StartPlayback(int wavDataId);
    HRESULT StartRecording(void);

    double ScanPcmMaxAbsAmplitude(void);
    void ScalePcmAmplitude(double scale);
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

static WasapiIO * self = NULL;

////////////////////////////////////////////////////////////////////////////////

extern "C" {

__declspec(dllexport)
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

__declspec(dllexport)
void __stdcall
WasapiIO_Term(void)
{
    if (self) {
        self->Term();
        delete self;
        self = NULL;
    }
}

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_DoDeviceEnumeration(int deviceType)
{
    assert(self);
    WWDeviceType t = (WWDeviceType)deviceType;
    return self->wasapi.DoDeviceEnumeration(t);
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceCount(void)
{
    assert(self);
    return self->wasapi.GetDeviceCount();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceAttributes(int id, WasapiIoDeviceAttributes &attr_return)
{
    assert(self);
    attr_return.deviceId = id;
    if (!self->wasapi.GetDeviceName(id, attr_return.name, sizeof attr_return.name)) {
        return false;
    }
    if (!self->wasapi.GetDeviceIdString(id, attr_return.deviceIdString, sizeof attr_return.deviceIdString)) {
        return false;
    }
    return true;
}

__declspec(dllexport)
int __stdcall
WasapiIO_InspectDevice(int id, int sampleRate, int bitsPerSample, int validBitsPerSample, int bitFormat)
{
    assert(self);
    return self->wasapi.InspectDevice(id, sampleRate, bitsPerSample, validBitsPerSample, bitFormat);
}

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_ChooseDevice(int id)
{
    assert(self);
    return self->wasapi.ChooseDevice(id);
}

__declspec(dllexport)
void __stdcall
WasapiIO_UnchooseDevice(void)
{
    assert(self);
    self->wasapi.UnchooseDevice();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_GetUseDeviceAttributes(WasapiIoDeviceAttributes &attr_return)
{
    assert(self);
    return WasapiIO_GetDeviceAttributes(self->wasapi.GetUseDeviceId(), attr_return);
}

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_Setup(const WasapiIoSetupArgs &args)
{
    assert(self);
    self->wasapi.SetStreamType((WWStreamType)args.streamType);
    self->wasapi.SetShareMode((WWShareMode)args.shareMode);
    self->wasapi.SetSchedulerTaskType((WWSchedulerTaskType)args.schedulerTask);
    self->wasapi.SetDataFeedMode((WWDataFeedMode)args.dataFeedMode);
    self->wasapi.SetLatencyMillisec((DWORD)args.latencyMillisec);
    self->wasapi.SetZeroFlushMillisec(args.zeroFlushMillisec);
    self->wasapi.SetTimePeriodHundredNanosec(args.timePeriodHandledNanosec);

    return self->wasapi.Setup(
        args.sampleRate, (WWPcmDataSampleFormatType)args.sampleFormat, args.numChannels);
}

__declspec(dllexport)
void __stdcall
WasapiIO_Unsetup(void)
{
    assert(self);
    self->wasapi.Unsetup();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataStart(void)
{
    assert(self);

    return self->AddPcmDataStart();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmData(int id, unsigned char *data, int64_t bytes)
{
    assert(self);
    return self->playPcmGroup.AddPlayPcmData(id, data, bytes);
}

__declspec(dllexport)
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

__declspec(dllexport)
int __stdcall
WasapiIO_ResampleIfNeeded(int conversionQuality)
{
    assert(self);
    return self->ResampleIfNeeded(conversionQuality);
}

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataEnd(void)
{
    assert(self);

    self->AddPcmDataEnd();

    return true;
}

__declspec(dllexport)
void __stdcall
WasapiIO_RemovePlayPcmDataAt(int id)
{
    assert(self);
    self->playPcmGroup.RemoveAt(id);
    self->UpdatePlayRepeat(self->playPcmGroup.GetRepatFlag());
}

__declspec(dllexport)
void __stdcall
WasapiIO_ClearPlayList(void)
{
    assert(self);
    self->playPcmGroup.Clear();
}

__declspec(dllexport)
void __stdcall
WasapiIO_SetPlayRepeat(bool b)
{
    assert(self);

    self->UpdatePlayRepeat(b);
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataId(int usageType)
{
    assert(self);
    return self->wasapi.GetPcmDataId((WWPcmDataUsageType)usageType);
}

__declspec(dllexport)
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

__declspec(dllexport)
bool __stdcall
WasapiIO_SetupCaptureBuffer(int64_t bytes)
{
    assert(self);
    return self->wasapi.SetupCaptureBuffer(bytes);
}

__declspec(dllexport)
int64_t __stdcall
WasapiIO_GetCapturedData(unsigned char *data, int64_t bytes)
{
    assert(self);
    return self->wasapi.GetCapturedData(data, bytes);
}

__declspec(dllexport)
int64_t __stdcall
WasapiIO_GetCaptureGlitchCount(void)
{
    assert(self);
    return self->wasapi.GetCaptureGlitchCount();
}

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_StartPlayback(int wavDataId)
{
    assert(self);

    return self->StartPlayback(wavDataId);
}

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_StartRecording(void)
{
    assert(self);

    return self->StartRecording();
}

__declspec(dllexport)
bool __stdcall
WasapiIO_Run(int millisec)
{
    assert(self);
    return self->wasapi.Run(millisec);
}

__declspec(dllexport)
void __stdcall
WasapiIO_Stop(void)
{
    assert(self);
    self->wasapi.Stop();
}

__declspec(dllexport)
int __stdcall
WasapiIO_Pause(void)
{
    assert(self);
    return self->wasapi.Pause();
}

__declspec(dllexport)
int __stdcall
WasapiIO_Unpause(void)
{
    assert(self);
    return self->wasapi.Unpause();
}

__declspec(dllexport)
int64_t __stdcall
WasapiIO_GetPosFrame(int usageType)
{
    assert(self);
    return self->wasapi.GetPosFrame((WWPcmDataUsageType)usageType);
}

__declspec(dllexport)
int64_t __stdcall
WasapiIO_GetTotalFrameNum(int usageType)
{
    assert(self);
    return self->wasapi.GetTotalFrameNum((WWPcmDataUsageType)usageType);
}

__declspec(dllexport)
bool __stdcall
WasapiIO_SetPosFrame(int64_t v)
{
    assert(self);
    return self->wasapi.SetPosFrame(v);
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceSampleRate(void)
{
    assert(self);
    return self->wasapi.GetDeviceSampleRate();
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceSampleFormat(void)
{
    assert(self);
    return self->wasapi.GetDeviceSampleFormat();
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataSampleRate(void)
{
    assert(self);
    return self->wasapi.GetPcmDataSampleRate();
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceBytesPerFrame(void)
{
    assert(self);
    return self->wasapi.GetDeviceBytesPerFrame();
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceNumChannels(void)
{
    assert(self);
    return self->wasapi.GetDeviceNumChannels();
}

__declspec(dllexport)
void __stdcall
WasapiIO_RegisterCallback(WWStateChanged callback)
{
    assert(self);
    self->wasapi.RegisterCallback(callback);
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetTimePeriodHundredNanosec(void)
{
    assert(self);
    return self->wasapi.GetTimePeriodHundredNanosec();
}

__declspec(dllexport)
double __stdcall
WasapiIO_ScanPcmMaxAbsAmplitude(void)
{
    assert(self);
    return self->ScanPcmMaxAbsAmplitude();
}

__declspec(dllexport)
void __stdcall
WasapiIO_ScalePcmAmplitude(double scale)
{
    assert(self);
    return self->ScalePcmAmplitude(scale);
}

__declspec(dllexport)
int __stdcall
WasapiIO_GetStreamType(void)
{
    assert(self);
    return self->wasapi.GetStreamType();
}

}; // extern "C"
