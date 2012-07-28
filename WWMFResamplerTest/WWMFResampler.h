// 日本語UTF-8
#pragma once

#define WINVER _WIN32_WINNT_WIN7

#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <assert.h>

enum WWSampleFormatType {
    WWSampleFormatUnknown = -1,
    WWSampleFormatInt,
    WWSampleFormatFloat,
    WWSampleFormatNUM
};

struct WWMediaFormat {
    WWSampleFormatType sampleFormat;
    int nChannels;
    int bits;
    int sampleRate;
    int dwChannelMask;
    int validBitsPerSample;

    WWMediaFormat(void) {
        sampleFormat       = WWSampleFormatUnknown;
        nChannels          = 0;
        bits               = 0;
        sampleRate         = 0;
        dwChannelMask      = 0;
        validBitsPerSample = 0;
    }

    WWMediaFormat(WWSampleFormatType aSampleFormat, int aNChannels, int aBits,
            int aSampleRate, int aDwChannelMask, int aValidBitsPerSample) {
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

struct WWSampleData {
    BYTE  *data;
    DWORD  bytes;

    WWSampleData(void) : data(NULL), bytes(0) {}
    WWSampleData(BYTE *aData, int aBytes) {
        data  = aData;
        bytes = aBytes;
    }

    ~WWSampleData(void) {
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
};

class WWMFResampler {
public:
    WWMFResampler(void) : m_pTransform(NULL), m_isMFStartuped(false) { }
    ~WWMFResampler(void);

    /// @param halfFilterLength: conversion quality. 1(min) to 60 (max)
    HRESULT Initialize(WWMediaFormat &inputFormat, WWMediaFormat &outputFormat, int halfFilterLength);

    HRESULT Resample(const BYTE *buff, int bytes, WWSampleData *sampleData_return);
    HRESULT Drain(WWSampleData *sampleData_return);

    void Finalize(void);

private:
    IMFTransform *m_pTransform;
    WWMediaFormat m_inputFormat;
    WWMediaFormat m_outputFormat;
    LONGLONG      m_inputFrameTotal;
    LONGLONG      m_outputFrameTotal;
    bool          m_isMFStartuped;

    HRESULT CreateMFSampleFromByteBuffer(WWSampleData &sampleData, IMFSample **ppSample);
    HRESULT ConvertMFSampleToWWSampleData(IMFSample *pSample, WWSampleData *sampleData_return);
    HRESULT GetOutputFromTransform(WWSampleData *sampleData_return);
};
