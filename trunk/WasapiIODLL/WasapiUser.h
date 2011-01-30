#pragma once

// 日本語 UTF-8

#include <Windows.h>
#include <MMDeviceAPI.h>
#include <AudioClient.h>
#include <AudioPolicy.h>
#include <vector>
#include "WWPcmData.h"

#define WW_DEVICE_NAME_COUNT (256)

typedef void (__stdcall WWStateChanged)();

struct WWDeviceInfo {
    int id;
    wchar_t name[WW_DEVICE_NAME_COUNT];

    WWDeviceInfo(void) {
        id = -1;
        name[0] = 0;
    }

    WWDeviceInfo(int id, const wchar_t * name);
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
    WWSTTPlayback
};

enum WWShareMode {
    WWSMShared,
    WWSMExclusive,
};

enum WWBitFormatType {
    WWSInt,
    WWSFloat
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
    bool InspectDevice(int id, LPWSTR result, size_t resultBytes);

    // set use device
    HRESULT ChooseDevice(int id);
    void UnchooseDevice(void);
    int  GetUseDeviceId(void);
    bool GetUseDeviceName(LPWSTR name, size_t nameBytes);

    // wasapi configuration parameters
    void SetSchedulerTaskType(WWSchedulerTaskType t);
    void SetShareMode(WWShareMode sm);
    void SetDataFeedMode(WWDataFeedMode mode);
    void SetLatencyMillisec(DWORD millisec);

    HRESULT Setup(
        int sampleRate, WWPcmDataFormatType format, int numChannels);

    void Unsetup(void);

    // Setup後に呼ぶ(Setup()で決定するので)
    int GetPcmDataSampleRate(void);
    int GetPcmDataNumChannels(void);
    int GetPcmDataFrameBytes(void);

    /// エンドポイントバッファーのデータフォーマット。
    WWPcmDataFormatType GetMixFormatType(void);

    /// ミックスフォーマットサンプルレート
    /// (WASAPI共有の場合、PcmDataSampleRateとは異なる値になることがある)
    int GetMixFormatSampleRate(void);

    /// 再生データをpcmDataに切り替える。
    void UpdatePlayPcmData(WWPcmData &pcmData);

    /// -1: not playing
    int GetNowPlayingPcmDataId(void);

    // recording buffer setup
    void SetupCaptureBuffer(int bytes);
    int GetCapturedData(BYTE *data, int bytes);
    int GetCaptureGlitchCount(void);

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
    int GetPosFrame(void);

    /// return total frames without pregap frame num
    int GetTotalFrameNum(void);

    /// v must be 0 or greater number
    bool SetPosFrame(int v);

    EDataFlow GetDataFlow(void) const {
        return m_dataFlow;
    }

    void RegisterCallback(WWStateChanged callback) {
        m_stateChangedCallback = callback;
    }

    void DeviceStateChanged(void);

private:
    std::vector<WWDeviceInfo> m_deviceInfo;
    IMMDeviceCollection       *m_deviceCollection;
    IMMDevice                 *m_deviceToUse;

    HANDLE       m_shutdownEvent;
    HANDLE       m_audioSamplesReadyEvent;

    IAudioClient *m_audioClient;
    int          m_frameBytes;
    UINT32       m_bufferFrameNum;

    int          m_sampleRate;

    /// has different value from m_sampleRate on wasapi shared mode
    int          m_deviceSampleRate;

    WWPcmDataFormatType m_format;
    int          m_numChannels;

    WWDataFeedMode m_dataFeedMode;
    WWSchedulerTaskType m_schedulerTaskType;
    AUDCLNT_SHAREMODE m_shareMode;
    DWORD        m_latencyMillisec;

    IAudioRenderClient  *m_renderClient;
    IAudioCaptureClient *m_captureClient;
    IAudioClockAdjustment *m_audioClockAdjustment;
    HANDLE       m_thread;
    HANDLE       m_mutex;
    bool         m_coInitializeSuccess;
    int          m_footerNeedSendCount;

    EDataFlow    m_dataFlow;
    int          m_glitchCount;
    int          m_footerCount;

    int          m_useDeviceId;
    wchar_t      m_useDeviceName[WW_DEVICE_NAME_COUNT];

    WWPcmData    *m_capturedPcmData;
    WWPcmData    *m_nowPlayingPcmData;
    WWPcmData    *m_pauseResumePcmData;
    WWPcmData    m_spliceBuffer;
    WWPcmData    m_startSilenceBuffer;
    WWPcmData    m_endSilenceBuffer;
    WWPcmData    m_pauseBuffer;

    WWStateChanged * m_stateChangedCallback;
    IMMDeviceEnumerator *m_deviceEnumerator;
    IMMNotificationClient *m_pNotificationClient;

    static DWORD WINAPI RenderEntry(LPVOID lpThreadParameter);
    static DWORD WINAPI CaptureEntry(LPVOID lpThreadParameter);

    DWORD RenderMain(void);
    DWORD CaptureMain(void);

    bool AudioSamplesSendProc(void);
    bool AudioSamplesRecvProc(void);

    void ClearCapturedPcmData(void);
    void ClearPlayPcmData(void);

    int CreateWritableFrames(BYTE *pData_return, int wantFrames);

    /// 再生リンクリストをつなげる。
    void SetupPlayPcmDataLinklist(
        bool repeat, WWPcmData *startPcmData, WWPcmData *endPcmData);

    /// 停止中に再生するpcmDataをセットする。
    /// 1フレームの無音の後にpcmDataを再生する。
    void UpdatePlayPcmDataWhenNotPlaying(WWPcmData &playPcmData);

    /// 再生中に再生するPcmDataをセットする。
    /// サンプル値をなめらかに補間する。
    void UpdatePlayPcmDataWhenPlaying(WWPcmData &playPcmData);
};

