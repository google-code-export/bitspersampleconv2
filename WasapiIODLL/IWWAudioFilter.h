#pragma once

// 日本語 UTF-8

#include "WWPcmData.h"

class IWWAudioFilter {
public:
    virtual ~IWWAudioFilter(void) {}
    virtual void UpdateSampleFormat(WWPcmDataSampleFormatType format, WWStreamType streamType, int numChannels) = 0;
    virtual void Filter(unsigned char *buff, int bytes) = 0;
};

