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

/// データフォーマット。
enum WWPcmDataFormatType {
    WWPcmDataFormatUnknown = -1,
    WWPcmDataFormatSint16,
    WWPcmDataFormatSint24,
    WWPcmDataFormatSint32V24,
    WWPcmDataFormatSint32,
    WWPcmDataFormatSfloat,

    WWPcmDataFormatNUM
};
const char *
WWPcmDataFormatTypeToStr(WWPcmDataFormatType w);
int WWPcmDataFormatTypeToBitsPerSample(WWPcmDataFormatType t);
int WWPcmDataFormatTypeToValidBitsPerSample(WWPcmDataFormatType t);
bool WWPcmDataFormatTypeIsFloat(WWPcmDataFormatType t);
bool WWPcmDataFormatTypeIsInt(WWPcmDataFormatType t);

WWPcmDataFormatType
WWPcmDataBitsPerSamplesToFormatType(int bitsPerSample, int validBitsPerSample, GUID subFormat);

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
    WWPcmDataFormatType format;
    WWPcmDataContentType contentType;
    int       nChannels;

    /// bytes per frame
    int       frameBytes;

    int64_t   nFrames;
    int64_t   posFrame;
    BYTE      *stream;

    WWPcmData(void) {
        id       = 0;
        next     = NULL;
        format   = WWPcmDataFormatUnknown;
        contentType = WWPcmDataContentPcmData;
        nChannels = 0;

        nFrames    = 0;
        frameBytes = 0;
        posFrame   = 0;
        stream     = NULL;
    }

    ~WWPcmData(void);

    /// @param frameBytes 1フレームのバイト数。
    ///     (1サンプル1チャンネルのバイト数×チャンネル数)
    bool Init(int id, WWPcmDataFormatType format, int nChannels,
        int64_t nFrames, int frameBytes, WWPcmDataContentType dataType);
    void Term(void);

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

