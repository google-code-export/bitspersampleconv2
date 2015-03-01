#pragma once

// “ú–{Œê UTF-8

#include "WWPcmData.h"

class IWWAudioFilter {
public:
    virtual ~IWWAudioFilter(void) {}
    virtual void UpdateSampleFormat(WWPcmDataSampleFormatType &format) = 0;
    virtual void Filter(unsigned char *buff, int bytes) = 0;
};

