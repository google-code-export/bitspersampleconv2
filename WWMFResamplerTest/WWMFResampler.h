// 日本語UTF-8
#pragma once

#define WINVER _WIN32_WINNT_WIN7

#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <assert.h>

enum WWMFSampleFormatType {
    WWMFSampleFormatUnknown = -1,
    WWMFSampleFormatInt,
    WWMFSampleFormatFloat,
    WWMFSampleFormatNUM
};

struct WWMFPcmFormat {
    WWMFSampleFormatType sampleFormat;
    short nChannels;
    short bits;
    int   sampleRate;
    DWORD   dwChannelMask;
    int   validBitsPerSample;

    WWMFPcmFormat(void) {
        sampleFormat       = WWMFSampleFormatUnknown;
        nChannels          = 0;
        bits               = 0;
        sampleRate         = 0;
        dwChannelMask      = 0;
        validBitsPerSample = 0;
    }

    WWMFPcmFormat(WWMFSampleFormatType aSampleFormat, short aNChannels, short aBits,
            int aSampleRate, DWORD aDwChannelMask, int aValidBitsPerSample) {
        sampleFormat       = aSampleFormat;
        nChannels          = aNChannels;
        bits               = aBits;
        sampleRate         = aSampleRate;
        dwChannelMask      = aDwChannelMask;
        validBitsPerSample = aValidBitsPerSample;
    }

    int Bitrate(void) {
        return sampleRate * nChannels * (bits/8);
    }

    int FrameBytes(void) {
        return nChannels * bits /8;
    }
};

struct WWMFSampleData {
    BYTE  *data;
    DWORD  bytes;

    WWMFSampleData(void) : data(NULL), bytes(0) {}
    WWMFSampleData(BYTE *aData, int aBytes) {
        data  = aData;
        bytes = aBytes;
    }

    ~WWMFSampleData(void) {
        assert(data == NULL);
    }

    void Release(void) {
        delete[] data;
        data = NULL;
    }

    void Forget(void) {
        data  = NULL;
        bytes = 0;
    }

    HRESULT Add(WWMFSampleData &rhs) {
        BYTE *buff = new BYTE[bytes + rhs.bytes];
        if (NULL == buff) {
            return E_FAIL;
        }

        memcpy(buff, data, bytes);
        memcpy(&buff[bytes], rhs.data, rhs.bytes);

        delete[] data;
        data = buff;
        bytes += rhs.bytes;
        return S_OK;
    }
};

class WWMFResampler {
public:
    WWMFResampler(void) : m_pTransform(NULL), m_isMFStartuped(false) { }
    ~WWMFResampler(void);

    /// @param halfFilterLength conversion quality. 1(min) to 60 (max)
    HRESULT Initialize(WWMFPcmFormat &inputFormat, WWMFPcmFormat &outputFormat, int halfFilterLength);

    HRESULT Resample(const BYTE *buff, int bytes, WWMFSampleData *sampleData_return);

    /// @param resampleInputBytes input buffer bytes of Resample(). this param is used to calculate expected output buffer size
    HRESULT Drain(int resampleInputBytes, WWMFSampleData *sampleData_return);

    /// Finalize must be called even when Initialize() is failed
    void Finalize(void);

private:
    IMFTransform *m_pTransform;
    WWMFPcmFormat m_inputFormat;
    WWMFPcmFormat m_outputFormat;
    bool          m_isMFStartuped;

    HRESULT CreateMFSampleFromByteBuffer(WWMFSampleData &sampleData, IMFSample **ppSample);
    HRESULT ConvertMFSampleToWWSampleData(IMFSample *pSample, WWMFSampleData *sampleData_return);
    HRESULT GetOutputFromTransform(WWMFSampleData *sampleData_return);
};
