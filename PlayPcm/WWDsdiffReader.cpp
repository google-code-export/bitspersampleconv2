#include "WWDsdiffReader.h"
#include <stdio.h>
#include <stdint.h>
#include <string.h>

#define FORM_DSD_FORM_TYPE_FOURCC           "DSD "
#define PROPERTY_CHUNK_PROPERTY_TYPE_FOURCC "SND "
#define COMPRESSION_UNCOMPRESSED_FOURCC     "DSD "

// assumed target platform is little endian...

#define FRM8_FOURCC_LE4 0x384d5246 //< "FRM8"
#define FVER_FOURCC_LE4 0x52455646 //< "FVER"
#define PROP_FOURCC_LE4 0x504f5250 //< "PROP"
#define FS_FOURCC_LE4   0x20205346 //< "FS  "
#define SND_FOURCC_LE4  0x20444e53 //< "SND "
#define CHNL_FOURCC_LE4 0x4c4e4843 //< "CHNL"
#define CMPR_FOURCC_LE4 0x52504d43 //< "CMPR"
#define DSD_FOURCC_LE4  0x20445344 //< "DSD "

// assumed target platform is little endian...
#define FREAD(toPtr, bytes, fp)               \
    if (fread(toPtr, 1, bytes, fp) < bytes) { \
        return -1;                            \
    }

static uint16_t
Big2ToLittle2(uint16_t v)
{
    return ((v<<8)&0xff00) |
           ((v>>8)&0xff);
}

static uint32_t
Big4ToLittle4(uint32_t v)
{
    return   (v >> 24) |
            ((v & 0x00ff0000) >> 8) |
            ((v & 0x0000ff00) << 8) |
             (v << 24);
}

static uint64_t
Big8ToLittle8(uint64_t v)
{
    return (v>>56) |
           ((v&0x00ff000000000000)>>40) |
           ((v&0x0000ff0000000000)>>24) |
           ((v&0x000000ff00000000)>>8)  |
           ((v&0x00000000ff000000)<<8)  |
           ((v&0x0000000000ff0000)<<24) |
           ((v&0x000000000000ff00)<<40) |
           ((v&0x00000000000000ff)<<56);
}

#define READ_BIG2(v, fp)           \
    if (fread(&v, 1, 2, fp) < 2) { \
        return -1;                 \
    }                              \
    v = Big2ToLittle2(v);

#define READ_BIG4(v, fp)           \
    if (fread(&v, 1, 4, fp) < 4) { \
        return -1;                 \
    }                              \
    v = Big4ToLittle4(v);

#define READ_BIG8(v, fp)           \
    if (fread(&v, 1, 8, fp) < 8) { \
        return -1;                 \
    }                              \
    v = Big8ToLittle8(v);

struct DsdiffFormDsdChunk {
    uint64_t ckDataSize;
    char     formType[4];

    DsdiffFormDsdChunk(void) {
        ckDataSize = 0;
    }

    int ReadFromFile(FILE *fp) {
        READ_BIG8(ckDataSize, fp);
        FREAD(formType, 4, fp);

        if (0x7fffffff < ckDataSize) {
            printf("file too large %llu\n", ckDataSize);
            return -1;
        }

        if (0 != memcmp(formType, FORM_DSD_FORM_TYPE_FOURCC, 4)) {
            printf("DSDIFF formType != DSD %c%c%c%c\n",
                    formType[0], formType[1], formType[2], formType[3]);
            return -1;
        }

        return 0;
    }
};

struct DsdiffFormVersionChunk {
    uint64_t ckDataSize;
    uint32_t version;

    DsdiffFormVersionChunk(void) {
        ckDataSize = 0;
    }

    int ReadFromFile(FILE *fp) {
        READ_BIG8(ckDataSize, fp);
        READ_BIG4(version, fp);

        if (4 != ckDataSize) {
            printf("DSDIFF FormVersion ckDataSize!=4 %llu\n", ckDataSize);
            return -1;
        }

        if (0x01000000 != (version&0xff000000)) {
            printf("DSDIFF Format main version != 0x01 (%08x)\n", version);
            return -1;
        }

        return 0;
    }
};

struct DsdiffPropertyChunk {
    uint64_t ckDataSize;
    char     propType[4];

    DsdiffPropertyChunk(void) {
        ckDataSize = 0;
    }

    int ReadFromFile(FILE *fp) {
        READ_BIG8(ckDataSize, fp);
        FREAD(propType, 4, fp);

        if (ckDataSize < 4) {
            printf("DSDIFF PropertyChunk ckDataSize<4 %llu\n", ckDataSize);
            return -1;
        }

        if (0 != memcmp(propType, PROPERTY_CHUNK_PROPERTY_TYPE_FOURCC, 4)) {
            printf("DSDIFF propertyType != SND %c%c%c%c\n",
                    propType[0], propType[1], propType[2], propType[3]);
            return -1;
        }

        return 0;
    }
};

struct DsdiffSampleRateChunk {
    uint64_t ckDataSize;
    uint32_t sampleRate;

    DsdiffSampleRateChunk(void) {
        ckDataSize = 0;
    }

    int ReadFromFile(FILE *fp) {
        READ_BIG8(ckDataSize, fp);
        READ_BIG4(sampleRate, fp);

        if (ckDataSize != 4) {
            printf("DSDIFF SampleRateChunk ckDataSize!=4 %llu\n", ckDataSize);
            return -1;
        }

        if (2822400 != sampleRate) {
            printf("DSDIFF SampleRateChunk sampleRate != 2822400 %u\n",
                    sampleRate);
            return -1;
        }

        return 0;
    }
};

struct DsdiffChannelsChunk {
    uint64_t ckDataSize;
    uint16_t numChannels;

    DsdiffChannelsChunk(void) {
        ckDataSize = 0;
    }

    int ReadFromFile(FILE *fp) {
        READ_BIG8(ckDataSize, fp);
        READ_BIG2(numChannels, fp);

        if (ckDataSize < 6) {
            printf("DSDIFF ChannelsChunk ckDataSize<6 %llu\n", ckDataSize);
            return -1;
        }

        if (numChannels != 2) {
            printf("DSDIFF ChannelsChunk numChannels!=2 %u\n", numChannels);
            return -1;
        }

        // skip channel ID's
        if (0 != fseek(fp, (long)(ckDataSize-2), SEEK_CUR)) {
            printf("DSDIFF ChannelsChunk error in skipping channels chunk\n");
            return -1;
        }

        return 0;
    }
};

struct DsdiffCompressionTypeChunk {
    uint64_t ckDataSize;
    char     cmprType[4];

    DsdiffCompressionTypeChunk(void) {
        ckDataSize = 0;
    }

    int ReadFromFile(FILE *fp) {
        READ_BIG8(ckDataSize, fp);
        FREAD(cmprType, 4, fp);

        if (ckDataSize < 5) {
            printf("DSDIFF CompressionTypeChunk ckDataSize<5 %llu\n", ckDataSize);
            return -1;
        }

        if (0 != memcmp(cmprType, COMPRESSION_UNCOMPRESSED_FOURCC, 4)) {
            printf("DSDIFF unsupported compression type %c%c%c%c\n",
                    cmprType[0], cmprType[1], cmprType[2], cmprType[3]);
            return -1;
        }

        // skip compressionName
        if (0 != fseek(fp, (ckDataSize-4)&(~1), SEEK_CUR)) {
            printf("DSDIFF compressionName skip failed\n");
            return -1;
        }

        return 0;
    }
};

struct DsdiffSoundDataChunk {
    uint64_t ckDataSize;
    unsigned char *data;

    DsdiffSoundDataChunk(void) {
        ckDataSize = 0;
        data = NULL;
    }

    ~DsdiffSoundDataChunk(void) {
        delete [] data;
        data = NULL;
    }

    int ReadFromFile(FILE *fp) {
        READ_BIG8(ckDataSize, fp);

        if (ckDataSize == 0 || 0x7fffffff < ckDataSize) {
            printf("DSDIFF SoundDataChunk ckDataSize too large %llu\n", ckDataSize);
            return -1;
        }

        data = new unsigned char[(size_t)ckDataSize];
        if (NULL == data) {
            printf("DSDIFF SoundDataChunk memory exhausted\n");
            return -1;
        }
        if (ckDataSize != fread(data, 1, (size_t)ckDataSize, fp)) {
            printf("DSDIFF SoundDataChunk read error\n");
            return -1;
        }

        // read pad
        if (ckDataSize & 1) {
            fgetc(fp);
        }

        return 0;
    }
};

struct DsdiffUnknownChunk {
    uint64_t ckDataSize;

    DsdiffUnknownChunk(void) {
        ckDataSize = 0;
    }

    int ReadFromFile(FILE *fp) {
        READ_BIG8(ckDataSize, fp);

        if (ckDataSize == 0 || 0x7fffffff < ckDataSize) {
            printf("DSDIFF UnknownChunk ckDataSize too large %llu\n", ckDataSize);
            return -1;
        }

        // skip unknown chunk
        if (0 != fseek(fp, (ckDataSize+1)&(~1), SEEK_CUR)) {
            printf("DSDIFF UnknownChunk skip failed\n");
            return -1;
        }

        return 0;
    }
};

WWPcmData *
WWReadDsdiffFile(const char *path, WWBitsPerSampleType bitsPerSampleType)
{
    WWPcmData *pcmData = NULL;
    uint32_t fourCC;
    DsdiffFormDsdChunk         formDsdChunk;
    DsdiffFormVersionChunk     formVersionChunk;
    DsdiffPropertyChunk        propChunk;
    DsdiffSampleRateChunk      sampleRateChunk;
    DsdiffChannelsChunk        channelsChunk;
    DsdiffCompressionTypeChunk cmprChunk;
    DsdiffSoundDataChunk       dataChunk;
    DsdiffUnknownChunk         unkChunk;
    uint32_t streamBytes;
    uint32_t writePos;
    uint32_t readPos;
    int result = -1;

    if (bitsPerSampleType == WWBpsNone) {
        printf("E: device does not support DoP\n");
        return NULL;
    }

    FILE *fp = NULL;
    fopen_s(&fp, path, "rb");
    if (NULL == fp) {
        return NULL;
    }

    while (1) {
        if (fread(&fourCC, 1, 4, fp) < 4) {
            break;
        }

        switch(fourCC) {
        case FRM8_FOURCC_LE4:
            if (formDsdChunk.ReadFromFile(fp) < 0) {
                goto end;
            }
            break;
        case FVER_FOURCC_LE4:
            if (formVersionChunk.ReadFromFile(fp) < 0) {
                goto end;
            }
            break;
        case PROP_FOURCC_LE4:
            if (propChunk.ReadFromFile(fp) < 0) {
                goto end;
            }
            break;
        case FS_FOURCC_LE4:
            if (sampleRateChunk.ReadFromFile(fp) < 0) {
                goto end;
            }
            break;
        case CHNL_FOURCC_LE4:
            if (channelsChunk.ReadFromFile(fp) < 0) {
                goto end;
            }
            break;
        case CMPR_FOURCC_LE4:
            if (cmprChunk.ReadFromFile(fp) < 0) {
                goto end;
            }
            break;
        case DSD_FOURCC_LE4:
            if (dataChunk.ReadFromFile(fp) < 0) {
                goto end;
            }
            break;
        default:
            if (unkChunk.ReadFromFile(fp) < 0) {
                goto end;
            }
            break;
        }
    }

    if (formDsdChunk.ckDataSize == 0 ||
            formVersionChunk.ckDataSize == 0 ||
            propChunk.ckDataSize == 0 ||
            sampleRateChunk.ckDataSize == 0 ||
            channelsChunk.ckDataSize == 0 ||
            cmprChunk.ckDataSize == 0 ||
            dataChunk.ckDataSize == 0) {
        printf("not supported format\n");
        goto end;
    }

    pcmData = new WWPcmData();
    if (NULL == pcmData) {
        goto end;
    }
    pcmData->Init();

    pcmData->bitsPerSample      = bitsPerSampleType == WWBps32v24 ? 32 : 24;
    pcmData->validBitsPerSample = 24;
    pcmData->nChannels          = channelsChunk.numChannels;

    // DSD 16bit == 1 frame
    pcmData->nFrames        = (int)(dataChunk.ckDataSize/2/channelsChunk.numChannels);
    pcmData->nSamplesPerSec = 176400;
    pcmData->posFrame       = 0;

    streamBytes = (pcmData->bitsPerSample/8) * pcmData->nFrames * pcmData->nChannels;
    pcmData->stream = new unsigned char[streamBytes];
    if (NULL == pcmData->stream) {
        goto end;
    }
    memset(pcmData->stream, 0, streamBytes);

    // data is stored in following order:
    // L channel byte, R channel byte, L channel byte ...
    // Most significant bit is the oldest bit in time.

    writePos = 0;
    readPos = 0;
    switch (bitsPerSampleType) {
    case WWBps32v24:
        for (int i=0; i<pcmData->nFrames; ++i) {
            for (int ch=0; ch<pcmData->nChannels; ++ch) {
                pcmData->stream[writePos+0] = 0;
                pcmData->stream[writePos+1] = dataChunk.data[readPos+2+ch];
                pcmData->stream[writePos+2] = dataChunk.data[readPos+0+ch];
                pcmData->stream[writePos+3] = i & 1 ? 0xfa : 0x05;

                writePos += 4;
            }
            readPos  += pcmData->nChannels*2;
        }
        break;
    case WWBps24:
        for (int i=0; i<pcmData->nFrames; ++i) {
            for (int ch=0; ch<pcmData->nChannels; ++ch) {
                pcmData->stream[writePos+0] = dataChunk.data[readPos+2+ch];
                pcmData->stream[writePos+1] = dataChunk.data[readPos+0+ch];
                pcmData->stream[writePos+2] = i & 1 ? 0xfa : 0x05;
                writePos += 3;
            }
            readPos  += pcmData->nChannels*2;
        }
        break;
    }

    result = 0;
end:
    if (result < 0) {
        if (pcmData) {
            pcmData->Term();
            delete pcmData;
            pcmData = NULL;
        }
    }

    fclose(fp);
    return pcmData;
}

