#pragma once

// 日本語 UTF-8

#include <Windows.h>
#include <AudioClient.h>
#include <AudioPolicy.h>
#include <MMDeviceAPI.h>
#include "WWPcmData.h"
#include "WWPcmStream.h"
#include "WWTimerResolution.h"
#include "WWThreadCharacteristics.h"

/// @param data captured data
/// @param dataBytes captured data size in bytes
typedef void (__stdcall WWCaptureCallback)(unsigned char *data, int dataBytes);

enum WWDataFeedMode {
    WWDFMEventDriven,
    WWDFMTimerDriven,

    WWDFMNum
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

class WasapiUser {
public:
    WasapiUser(void);
    ~WasapiUser(void);

    HRESULT Init(void);
    void Term(void);

    /// @param bitFormat 0:Int, 1:Float
    /// @return 0 this sampleFormat is supported
    int InspectDevice(IMMDevice *device, int sampleRate, int bitsPerSample, int validBitsPerSample, int bitFormat);

    // wasapi configuration parameters
    // call before Setup()
    void SetShareMode(WWShareMode sm);
    void SetDataFeedMode(WWDataFeedMode mode);
    void SetLatencyMillisec(DWORD millisec);
    void SetStreamType(WWStreamType t);
    WWStreamType StreamType(void) const;

    /// @param sampleRate pcm data sample rate. On WASAPI shared mode, device sample rate cannot be changed so
    ///        you need to resample pcm to DeviceSampleRate
    HRESULT Setup(IMMDevice *device, int sampleRate, WWPcmDataSampleFormatType sampleFormat, int numChannels);

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

    int GetEndpointBufferFrameNum(void) const { return m_bufferFrameNum; }

    /// 再生データをpcmDataに切り替える。再生中でも停止中でも再生一時停止中でも可。
    void UpdatePlayPcmData(WWPcmData &pcmData);

    // recording buffer setup
    void RegisterCaptureCallback(WWCaptureCallback cb) {
        m_captureCallback = cb;
    }
    int64_t GetCaptureGlitchCount(void);

    HRESULT Start(void);

    /// 再生スレッドが終了したかどうかを調べる。
    bool Run(int millisec);

    /// 停止。
    void Stop(void);

    /// ポーズ。
    HRESULT Pause(void);

    /// ポーズ解除。
    HRESULT Unpause(void);

    /// v must be 0 or greater number
    bool SetPosFrame(int64_t v);

    EDataFlow GetDataFlow(void) const {
        return m_dataFlow;
    }

    void MutexWait(void);

    void MutexRelease(void);

    WWPcmStream &PcmStream(void) { return m_pcmStream; }
    WWTimerResolution &TimerResolution(void) { return m_timerResolution; }
    WWThreadCharacteristics &ThreadCharacteristics(void) { return m_threadCharacteristics; }

private:
    HANDLE       m_shutdownEvent;
    HANDLE       m_audioSamplesReadyEvent;

    IMMDevice    *m_deviceToUse;
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

    WWPcmStream m_pcmStream;
    WWTimerResolution m_timerResolution;
    WWThreadCharacteristics m_threadCharacteristics;

    WWCaptureCallback *m_captureCallback;

    static DWORD WINAPI RenderEntry(LPVOID lpThreadParameter);
    static DWORD WINAPI CaptureEntry(LPVOID lpThreadParameter);

    DWORD RenderMain(void);
    DWORD CaptureMain(void);

    bool AudioSamplesSendProc(void);
    bool AudioSamplesRecvProc(void);

    /// WASAPIレンダーバッファに詰めるデータを作る。
    int CreateWritableFrames(BYTE *pData_return, int wantFrames);

    /// 再生中(か一時停止中)に再生するPcmDataをセットする。
    /// サンプル値をなめらかに補間する。
    void UpdatePlayPcmDataWhenPlaying(WWPcmData &playPcmData);

    void PrepareBuffers(void);
};

