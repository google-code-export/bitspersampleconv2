#pragma once

struct WWPcmData {
    int bitsPerSample;
    int validBitsPerSample;
    int nSamplesPerSec;
    int nChannels;
    int  nFrames;
    int  posFrame;

    unsigned char *stream;

    void Init(void);
    void Term(void);

    WWPcmData(void) {
        stream = 0;
    }
    ~WWPcmData(void);
};

