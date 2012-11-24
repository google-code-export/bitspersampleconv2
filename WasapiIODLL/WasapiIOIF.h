#pragma once

#include <Windows.h>
#include "WasapiUser.h"
#include <stdint.h>

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Init(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Term(void);

/// @param type WWSchedulerTaskType
extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetSchedulerTaskType(int type);

/// @param sm WWShareMode
extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetShareMode(int sm);

/// @param dfm WWDataFeedMode
extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetDataFeedMode(int dfm);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetLatencyMillisec(int ms);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_DoDeviceEnumeration(int deviceType);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceCount(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceName(int id, LPWSTR name, int nameBytes);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceIdString(int id, LPWSTR idStr, int idStrBytes);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_InspectDevice(int id, int sampleRate, int bitsPerSample, int validBitsPerSample, int bitFormat);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_ChooseDevice(int id);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_UnchooseDevice(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetUseDeviceId(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetUseDeviceName(LPWSTR name, int nameBytes);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetUseDeviceIdString(LPWSTR idStr, int idStrBytes);

/// @param sampleFormat WWPcmDataSampleFormatType
extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Setup(int sampleRate, int sampleFormat, int numChannels);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Unsetup(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataStart(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmData(int id, unsigned char *data, int64_t bytes);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataSetPcmFragment(int id, int64_t posBytes, unsigned char *data, int64_t bytes);

/// @return HRESULT
extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_ResampleIfNeeded(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataEnd(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_ClearPlayList(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetPlayRepeat(bool b);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataId(int usageType);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetNowPlayingPcmDataId(int id);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_SetupCaptureBuffer(int64_t bytes);

extern "C" __declspec(dllexport)
int64_t __stdcall
WasapiIO_GetCapturedData(unsigned char *data, int64_t bytes);

extern "C" __declspec(dllexport)
int64_t __stdcall
WasapiIO_GetCaptureGlitchCount(void);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Start(int wavDataId);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_Run(int millisec);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Stop(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_Pause(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_Unpause(void);

extern "C" __declspec(dllexport)
int64_t __stdcall
WasapiIO_GetPosFrame(int usageType);

extern "C" __declspec(dllexport)
int64_t __stdcall
WasapiIO_GetTotalFrameNum(int usageType);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceSampleRate(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceSampleFormat(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataSampleRate(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetPcmDataFrameBytes(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceNumChannels(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_RegisterCallback(WWStateChanged callback);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetZeroFlushMillisec(int zeroFlushMillisec);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetTimePeriodHundredNanosec(int hnanosec);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetTimePeriodHundredNanosec(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetResamplerConversionQuality(int quality);
