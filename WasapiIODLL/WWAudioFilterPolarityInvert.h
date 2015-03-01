#pragma once

// “ú–{Œê UTF-8

#include "IWWAudioFilter.h"

class WWAudioFilterPolarityInvert : public IWWAudioFilter {
public:
    virtual ~WWAudioFilterPolarityInvert(void) {}
    virtual void UpdateSampleFormat(WWPcmDataSampleFormatType &format);
    virtual void Filter(unsigned char *buff, int bytes);

private:
    WWPcmDataSampleFormatType mFormat;
};

