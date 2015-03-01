#pragma once

// “ú–{Œê UTF-8

#include "WWPcmData.h"

class WWPcmSampleManipulator {
public:
    bool GetFloatSample(WWPcmDataSampleFormatType format, int numChannels, const unsigned char *buff, int64_t buffBytes, int64_t frameIdx, int ch, float &value_return);
    bool SetFloatSample(WWPcmDataSampleFormatType format, int numChannels, unsigned char *buff, int64_t buffBytes, int64_t frameIdx, int ch, float value);
};
