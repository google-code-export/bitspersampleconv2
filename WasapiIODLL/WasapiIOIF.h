#pragma once

#include <Windows.h>
#include "WasapiUser.h"
#include <stdint.h>

extern "C" {

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_Init(void);

__declspec(dllexport)
void __stdcall
WasapiIO_Term(void);

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_DoDeviceEnumeration(int deviceType);

__declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceCount(void);

#define WASAPI_IO_DEVICE_STR_COUNT (256)

#pragma pack(push, 4)
struct WasapiIoDeviceAttributes {
    int   deviceId;
    WCHAR name[WASAPI_IO_DEVICE_STR_COUNT];
    WCHAR deviceIdString[WASAPI_IO_DEVICE_STR_COUNT];
};
#pragma pack(pop)

__declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceAttributes(int id, WasapiIoDeviceAttributes &attr_return);

__declspec(dllexport)
int __stdcall
WasapiIO_InspectDevice(int id, int sampleRate, int bitsPerSample, int validBitsPerSample, int bitFormat);

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_ChooseDevice(int id);

__declspec(dllexport)
void __stdcall
WasapiIO_UnchooseDevice(void);

__declspec(dllexport)
bool __stdcall
WasapiIO_GetUseDeviceAttributes(WasapiIoDeviceAttributes &attr_return);

#pragma pack(push, 4)
struct WasapiIoSetupArgs {
    int streamType;
    int sampleRate;
    int sampleFormat;    ///< WWPcmDataSampleFormatType
    int numChannels;
    int shareMode;
    int schedulerTask;
    int dataFeedMode;
    int latencyMillisec;
    int timePeriodHandledNanosec;
    int zeroFlushMillisec;
};
#pragma pack(pop)

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_Setup(const WasapiIoSetupArgs &args);

__declspec(dllexport)
void __stdcall
WasapiIO_Unsetup(void);

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataStart(void);

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmData(int id, unsigned char *data, int64_t bytes);

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataSetPcmFragment(int id, int64_t posBytes, unsigned char *data, int64_t bytes);

/// @return HRESULT
__declspec(dllexport)
int __stdcall
WasapiIO_ResampleIfNeeded(int conversionQuality);

__declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataEnd(void);

__declspec(dllexport)
void __stdcall
WasapiIO_ClearPlayList(void);

__declspec(dllexport)
void __stdcall
WasapiIO_SetPlayRepeat(bool b);

__declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataId(int usageType);

__declspec(dllexport)
void __stdcall
WasapiIO_SetNowPlayingPcmDataId(int id);

__declspec(dllexport)
bool __stdcall
WasapiIO_SetupCaptureBuffer(int64_t bytes);

__declspec(dllexport)
int64_t __stdcall
WasapiIO_GetCapturedData(unsigned char *data, int64_t bytes);

__declspec(dllexport)
int64_t __stdcall
WasapiIO_GetCaptureGlitchCount(void);

__declspec(dllexport)
HRESULT __stdcall
WasapiIO_Start(int wavDataId);

__declspec(dllexport)
bool __stdcall
WasapiIO_Run(int millisec);

__declspec(dllexport)
void __stdcall
WasapiIO_Stop(void);

__declspec(dllexport)
int __stdcall
WasapiIO_Pause(void);

__declspec(dllexport)
int __stdcall
WasapiIO_Unpause(void);

__declspec(dllexport)
int64_t __stdcall
WasapiIO_GetPosFrame(int usageType);

__declspec(dllexport)
int64_t __stdcall
WasapiIO_GetTotalFrameNum(int usageType);

__declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceSampleRate(void);

__declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceSampleFormat(void);

__declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataSampleRate(void);

__declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataFrameBytes(void);

__declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceNumChannels(void);

__declspec(dllexport)
void __stdcall
WasapiIO_RegisterCallback(WWStateChanged callback);

__declspec(dllexport)
int __stdcall
WasapiIO_GetTimePeriodHundredNanosec(void);

__declspec(dllexport)
double __stdcall
WasapiIO_ScanPcmMaxAbsAmplitude(void);

__declspec(dllexport)
void __stdcall
WasapiIO_ScalePcmAmplitude(double scale);

__declspec(dllexport)
int __stdcall
WasapiIO_GetStreamType(void);

}; // extern "C"
