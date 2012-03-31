#pragma once

struct WWPcmData {
    int bitsPerSample;
    int nSamplesPerSec;
    int nChannels;
    int  nFrames;
    int  posFrame;

    unsigned char *stream;

    void Init(int samples);
    void Term(void);

    ~WWPcmData(void);
};

WWPcmData * WWPcmDataWavFileLoad(const char *path);

