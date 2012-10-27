#pragma once

// 日本語 UTF-8

#include <Windows.h>
#include <MMDeviceAPI.h>
#include <AudioClient.h>
#include <AudioPolicy.h>
#include <vector>
#include "WWPcmData.h"

#define WW_DEVICE_NAME_COUNT (256)
#define WW_DEVICE_IDSTR_COUNT (256)

typedef void (__stdcall WWStateChanged)(LPCWSTR deviceIdStr);

struct WWDeviceInfo {
    int id;
    wchar_t name[WW_DEVICE_NAME_COUNT];
    wchar_t idStr[WW_DEVICE_IDSTR_COUNT];

    WWDeviceInfo(void) {
        id = -1;
        name[0] = 0;
        idStr[0] = 0;
    }

    WWDeviceInfo(int id, const wchar_t * name, const wchar_t * idStr);
};

enum WWDataFeedMode {
    WWDFMEventDriven,
    WWDFMTimerDriven,

    WWDFMNum
};

enum WWDeviceType {
    WWDTPlay,
    WWDTRec,

    WWDTNum
};

enum WWSchedulerTaskType {
    WWSTTNone,
    WWSTTAudio,
    WWSTTProAudio,
    WWSTTPlayback,
};

enum WWShareMode {
    WWSMShared,
    WWSMExclusive,
};

enum WWBitFormatType {
    WWBitFormatUnknown = -1,
    WWBitFormatSint,
    WWBitFormatSfloat,
    WWBitFormatNUM
};

enum WWPcmDataUsageType {
    WWPDUNowPlaying,
    WWPDUPauseResumeToPlay,
    WWPDUSpliceNext,
    WWPDUCapture,
};

class WasapiUser {
public:
    WasapiUser(void);
    ~WasapiUser(void);

    HRESULT Init(void);
    void Term(void);

    // device enumeration
    HRESULT DoDeviceEnumeration(WWDeviceType t);
    int GetDeviceCount(void);
    bool GetDeviceName(int id, LPWSTR name, size_t nameBytes);
    bool GetDeviceIdString(int id, LPWSTR idStr, size_t idStrBytes);

    /// @param bitFormat 0:Int, 1:Float
    /// @return 0 this sampleFormat is supported
    int InspectDevice(int id, int sampleRate, int bitsPerSample, int validBitsPerSample, int bitFormat);

    // set use device
    HRESULT ChooseDevice(int id);
    void UnchooseDevice(void);
    int  GetUseDeviceId(void);
    bool GetUseDeviceName(LPWSTR name, size_t nameBytes);
    bool GetUseDeviceIdString(LPWSTR idStr, size_t idStrBytes);

    // wasapi configuration parameters
    // call before Setup()
    void SetSchedulerTaskType(WWSchedulerTaskType t);
    void SetShareMode(WWShareMode sm);
    void SetDataFeedMode(WWDataFeedMode mode);
    void SetLatencyMillisec(DWORD millisec);
    void SetZeroFlushMillisec(int zeroFlushMillisec);
    void SetTimePeriodMillisec(int millisec);

    /// @param sampleRate pcm data sample rate. On WASAPI shared mode, device sample rate cannot be changed so
    ///        you need to resample pcm to DeviceSampleRate
    HRESULT Setup(
            int sampleRate, WWPcmDataSampleFormatType sampleFormat, int numChannels);

    void Unsetup(void);

    // Setup後に呼ぶ(Setup()で代入するので)
    int GetPcmDataSampleRate(void) const      { return m_sampleRate; }
    int GetPcmDataNumChannels(void) const     { return m_numChannels; }
    DWORD GetPcmDataDwChannelMask(void) const { return m_dwChannelMask; }
    WWPcmDataSampleFormatType GetPcmDataSampleFormat(void) const { return m_sampleFormat; }

    bool IsResampleNeeded(void) const;

    /// if you changed sample format after Setup() call this function...
    void UpdatePcmDataFormat(int sampleRate, WWPcmDataSampleFormatType sampleFormat,
            int numChannels, DWORD dwChannelMask);

    /// デバイス(ミックスフォーマット)サンプルレート
    /// WASAPI共有の場合、Setup後にGetPcmDataSampleRateとは異なる値になることがある。
    int GetDeviceSampleRate(void) const      { return m_deviceSampleRate; }
    int GetDeviceNumChannels(void) const     { return m_deviceNumChannels; }
    DWORD GetDeviceDwChannelMask(void) const { return m_deviceDwChannelMask; }
    int GetDeviceBytesPerFrame(void) const   { return m_deviceBytesPerFrame; }
    WWPcmDataSampleFormatType GetDeviceSampleFormat(void) const { return m_deviceSampleFormat; }

    /// 再生データをpcmDataに切り替える。再生中でも停止中でも再生一時停止中でも可。
    void UpdatePlayPcmData(WWPcmData &pcmData);

    /// -1: specified buffer is not used
    int GetPcmDataId(WWPcmDataUsageType t);

    // recording buffer setup
    bool SetupCaptureBuffer(int64_t bytes);
    int64_t GetCapturedData(BYTE *data, int64_t bytes);
    int64_t GetCaptureGlitchCount(void);

    /// 再生リピートの更新。
    void UpdatePlayRepeat(
        bool repeat, WWPcmData *startPcmData, WWPcmData *endPcmData);

    HRESULT Start(void);

    /// 再生スレッドが終了したかどうかを調べる。
    bool Run(int millisec);

    /// 停止。
    void Stop(void);

    /// ポーズ。
    HRESULT Pause(void);

    /// ポーズ解除。
    HRESULT Unpause(void);

    /// negative number returns when playing pregap
    int64_t GetPosFrame(WWPcmDataUsageType t);

    /// return total frames without pregap frame num
    int64_t GetTotalFrameNum(WWPcmDataUsageType t);

    /// v must be 0 or greater number
    bool SetPosFrame(int64_t v);

    EDataFlow GetDataFlow(void) const {
        return m_dataFlow;
    }

    void RegisterCallback(WWStateChanged callback) {
        m_stateChangedCallback = callback;
    }

    void DeviceStateChanged(LPCWSTR deviceIdStr);

private:
    std::vector<WWDeviceInfo> m_deviceInfo;
    IMMDeviceCollection       *m_deviceCollection;
    IMMDevice                 *m_deviceToUse;

    HANDLE       m_shutdownEvent;
    HANDLE       m_audioSamplesReadyEvent;

    IAudioClient *m_audioClient;

    /// wasapi audio buffer frame size
    UINT32       m_bufferFrameNum;

    /// source data format
    WWPcmDataSampleFormatType m_sampleFormat;
    int          m_sampleRate;
    int          m_numChannels;
    DWORD        m_dwChannelMask;

    /// may have different value from m_sampleRate on wasapi shared mode
    WWPcmDataSampleFormatType m_deviceSampleFormat;
    int          m_deviceSampleRate;
    int          m_deviceNumChannels;
    DWORD        m_deviceDwChannelMask;
    int          m_deviceBytesPerFrame;

    WWDataFeedMode m_dataFeedMode;
    WWSchedulerTaskType m_schedulerTaskType;
    AUDCLNT_SHAREMODE m_shareMode;
    DWORD        m_latencyMillisec;

    IAudioRenderClient  *m_renderClient;
    IAudioCaptureClient *m_captureClient;
    HANDLE       m_thread;
    HANDLE       m_mutex;
    bool         m_coInitializeSuccess;
    int          m_footerNeedSendCount;

    EDataFlow    m_dataFlow;
    int64_t      m_glitchCount;
    int          m_footerCount;

    int          m_useDeviceId;
    wchar_t      m_useDeviceName[WW_DEVICE_NAME_COUNT];
    wchar_t      m_useDeviceIdStr[WW_DEVICE_IDSTR_COUNT];

    WWPcmData    *m_capturedPcmData;
    WWPcmData    *m_nowPlayingPcmData;
    WWPcmData    *m_pauseResumePcmData;
    WWPcmData    m_spliceBuffer;
    WWPcmData    m_startSilenceBuffer0;
    WWPcmData    m_startSilenceBuffer1;
    WWPcmData    m_endSilenceBuffer;
    WWPcmData    m_pauseBuffer;

    WWStateChanged * m_stateChangedCallback;
    IMMDeviceEnumerator *m_deviceEnumerator;
    IMMNotificationClient *m_pNotificationClient;
    int          m_timePeriodMillisec;
    int          m_zeroFlushMillisec;

    static DWORD WINAPI RenderEntry(LPVOID lpThreadParameter);
    static DWORD WINAPI CaptureEntry(LPVOID lpThreadParameter);

    DWORD RenderMain(void);
    DWORD CaptureMain(void);

    bool AudioSamplesSendProc(void);
    bool AudioSamplesRecvProc(void);

    void ClearCapturedPcmData(void);
    void ClearPlayPcmData(void);

    /// WASAPIレンダーバッファに詰めるデータを作る。
    int CreateWritableFrames(BYTE *pData_return, int wantFrames);

    /// 再生リンクリストをつなげる。
    void SetupPlayPcmDataLinklist(
        bool repeat, WWPcmData *startPcmData, WWPcmData *endPcmData);

    /// 再生スレッド停止中に再生するpcmDataをセットする。
    /// 1フレームの無音の後にpcmDataを再生する。
    void UpdatePlayPcmDataWhenNotPlaying(WWPcmData &playPcmData);

    /// 再生中(か一時停止中)に再生するPcmDataをセットする。
    /// サンプル値をなめらかに補間する。
    void UpdatePlayPcmDataWhenPlaying(WWPcmData &playPcmData);

    WWPcmData * GetPcmDataByUsageType(WWPcmDataUsageType t);
};

