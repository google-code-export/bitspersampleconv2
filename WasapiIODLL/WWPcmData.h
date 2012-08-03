#pragma once

// 日本語 UTF-8

#include <Windows.h>
#include <mmsystem.h>
#include <MMReg.h>
#include <stdint.h>

/// PCMデータの用途。
enum WWPcmDataContentType {
    WWPcmDataContentPcmData,
    WWPcmDataContentSilence,
    WWPcmDataContentSplice,
};
const char *
WWPcmDataContentTypeToStr(WWPcmDataContentType w);

/// サンプルフォーマット。
enum WWPcmDataSampleFormatType {
    WWPcmDataSampleFormatUnknown = -1,
    WWPcmDataSampleFormatSint16,
    WWPcmDataSampleFormatSint24,
    WWPcmDataSampleFormatSint32V24,
    WWPcmDataSampleFormatSint32,
    WWPcmDataSampleFormatSfloat,

    WWPcmDataSampleFormatNUM
};
const char *
WWPcmDataSampleFormatTypeToStr(WWPcmDataSampleFormatType w);
int WWPcmDataSampleFormatTypeToBitsPerSample(WWPcmDataSampleFormatType t);
int WWPcmDataSampleFormatTypeToValidBitsPerSample(WWPcmDataSampleFormatType t);
bool WWPcmDataSampleFormatTypeIsFloat(WWPcmDataSampleFormatType t);
bool WWPcmDataSampleFormatTypeIsInt(WWPcmDataSampleFormatType t);

WWPcmDataSampleFormatType
WWPcmDataSampleFormatTypeGenerate(int bitsPerSample, int validBitsPerSample, GUID subFormat);

/*
 * play
 *   pcmData->posFrame: playing position
 *   pcmData->nFrames: total frames to play (frame == sample point)
 * record
 *   pcmData->posFrame: available recorded frame num
 *   pcmData->nFrames: recording buffer size
 */
struct WWPcmData {
    int       id;
    WWPcmData *next;
    WWPcmDataSampleFormatType sampleFormat;
    WWPcmDataContentType contentType;
    int       nChannels;

    int       bytesPerFrame;

    /// used by FillBufferAddData()
    int64_t   filledFrames;

    int64_t   nFrames;
    int64_t   posFrame;
    BYTE      *stream;

    WWPcmData(void) {
        id       = 0;
        next     = NULL;
        sampleFormat   = WWPcmDataSampleFormatUnknown;
        contentType = WWPcmDataContentPcmData;
        nChannels = 0;

        nFrames       = 0;
        bytesPerFrame = 0;
        filledFrames  = 0;
        posFrame      = 0;
        stream        = NULL;
    }

    ~WWPcmData(void);

    /// @param bytesPerFrame 1フレームのバイト数。
    ///     (1サンプル1チャンネルのバイト数×チャンネル数)
    bool Init(int id, WWPcmDataSampleFormatType sampleFormat, int nChannels,
        int64_t nFrames, int bytesPerFrame, WWPcmDataContentType dataType);
    void Term(void);

    void Forget(void) {
        stream = NULL;
    }

    void CopyFrom(WWPcmData *rhs);

    /** create splice data from the two adjacent sample data */
    int UpdateSpliceDataWithStraightLine(
        WWPcmData *fromPcmData, int64_t fromPosFrame,
        WWPcmData *toPcmData,   int64_t toPosFrame);

    /** @return sample count need to advance */
    int CreateCrossfadeData(
        WWPcmData *fromPcmData, int64_t fromPosFrame,
        WWPcmData *toPcmData,   int64_t toPosFrame);

    int64_t AvailableFrames(void) const {
        return nFrames - posFrame;
    }

    /// @return retrieved data bytes
    int GetBufferData(int64_t fromBytes, int wantBytes, BYTE *data_return);

    /// FillBuffer api.
    /// FillBufferStart() and FillBufferAddSampleData() several times and FillBufferEnd()
    void FillBufferStart(void) { filledFrames = 0; }

    /// @param data sampleData
    /// @param bytes data bytes
    /// @return added sample bytes. 0 if satistifed and no sample data is consumed
    int FillBufferAddData(const BYTE *data, int bytes);

    void FillBufferEnd(void) { nFrames = filledFrames; }

    /// get float sample min/max for volume correction
    void FindSampleValueMinMax(float *minValue_return, float *maxValue_return);
    void ScaleSampleValue(float scale);

private:
    /** get sample value on posFrame.
     * 24 bit signed int value is returned when Sint32V24
     */
    int GetSampleValueInt(int ch, int64_t posFrame);
    float GetSampleValueFloat(int ch, int64_t posFrame);

    bool SetSampleValueInt(int ch, int64_t posFrame, int value);
    bool SetSampleValueFloat(int ch, int64_t posFrame, float value);

    float GetSampleValueAsFloat(int ch, int64_t posFrame);
    bool SetSampleValueAsFloat(int ch, int64_t posFrame, float value);
};

