#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include <assert.h>
#include <stdlib.h>
#include <math.h>

#define MATH_PI (3.14159265358979f)

#define OUTPUT_FILENAME "output.dff"

#define SIGNAL_FREQUENCY_HZ (882)

#define SAMPLE_RATE (2822400)
#define NUM_CHANNELS (2)
#define OUTPUT_SECONDS (60)

struct SmallDsdStreamInfo {
    uint64_t dsdStream;
    int      availableBits;

    SmallDsdStreamInfo(void) {
        dsdStream = 0;
        availableBits = 0;
    }
};

static const unsigned char gBitsSetTable256[256] = 
{
#   define B2(n) n,     n+1,     n+1,     n+2
#   define B4(n) B2(n), B2(n+1), B2(n+1), B2(n+2)
#   define B6(n) B4(n), B4(n+1), B4(n+1), B4(n+2)
    B6(0), B6(1), B6(1), B6(2)
};
#undef B6
#undef B4
#undef B2

/// @return -1.0 to 1.0f
static float
DsdStreamToAmplitudeFloat(uint64_t v, int availableBits)
{
    if (availableBits <= 0) {
        return 0.0f;
    }

    int bitCount = 0;

    for (int i=0; i<availableBits/8; ++i) {
        bitCount += gBitsSetTable256[v&0xff];
        v >>= 8;
    }

    int remainBits = availableBits&7;
    for (int i=0; i<remainBits; ++i) {
        bitCount += v & 1;
        v >>= 1;
    }

    return (bitCount-availableBits*0.5f)/(availableBits*0.5f);
}

/// @param nFrames �S�`�����l����8�T���v�����̏���1�Ƃ���P�ʁB
static void
GenerateDffData(int nFrames, int nChannels, FILE *fp)
{
    int64_t pos = 0;
    SmallDsdStreamInfo *dsdStreams = new SmallDsdStreamInfo[nChannels];
    if (NULL == dsdStreams) {
        printf("memory exhausted\n");
        exit(1);
    }

    for (int64_t i=0; i<nFrames; ++i) {
        // 0 <= phase < SAMPLE_RATE/SIGNAL_FREQUENCY_HZ
        int fraction = SAMPLE_RATE/8/SIGNAL_FREQUENCY_HZ;
        int phase = i % fraction;
        float targetV = 0.5f * sinf(2.0f * MATH_PI * phase/fraction);

        for (int ch=0; ch<nChannels; ++ch) {
            SmallDsdStreamInfo *p = &dsdStreams[ch];

            for (int c=0; c<8; ++c) {
                int ampBits = p->availableBits;
                if (64 == p->availableBits) {
                    // ������Ă���8�r�b�g��DSD�f�[�^��p->dsdStream�ɋl�߂��
                    // 64�r�b�g�̃f�[�^�̂����Â��f�[�^8�r�b�g�������o����ď�����̂�Amplitude�̌v�Z���珜�O����B
                    ampBits = 56+c;
                }

                float currentV = DsdStreamToAmplitudeFloat(p->dsdStream, ampBits);
                p->dsdStream <<= 1;
                if (currentV < targetV) {
                    p->dsdStream += 1;
                }

                if (p->availableBits < 64) {
                    ++p->availableBits;
                }
            }
            if (fputc(p->dsdStream&0xff, fp)<0) {
                printf("fputc error\n");
                exit(1);
            }
        }
    }

    delete [] dsdStreams;
    dsdStreams = NULL;
}

#define FORM_DSD_FORM_TYPE           "DSD "
#define PROPERTY_CHUNK_PROPERTY_TYPE "SND "
#define COMPRESSION_UNCOMPRESSED     "DSD "

// assumed target platform is little endian...

static const int FOURCC_FRM8 = 0x384d5246; //< "FRM8"
static const int FOURCC_FVER = 0x52455646; //< "FVER"
static const int FOURCC_PROP = 0x504f5250; //< "PROP"
static const int FOURCC_FS   = 0x20205346; //< "FS  "
static const int FOURCC_SND  = 0x20444e53; //< "SND "
static const int FOURCC_CHNL = 0x4c4e4843; //< "CHNL"
static const int FOURCC_CMPR = 0x52504d43; //< "CMPR"
static const int FOURCC_DSD  = 0x20445344; //< "DSD "
static const int FOURCC_ABSS = 0x53534241; //< "ABSS"

// assumed target platform is little endian...
#define FWRITE(ptr, bytes, fp)               \
    if (fwrite(ptr, 1, bytes, fp) < bytes) { \
        return -1;                           \
    }

static uint16_t
Little2ToBig2(uint16_t v)
{
    return (v<<8) |
           (v>>8);
}

static uint32_t
Little4ToBig4(uint32_t v)
{
    return (v >> 24) |
           ((v & 0x00ff0000) >> 8) |
           ((v & 0x0000ff00) << 8) |
           (v << 24);
}

static uint64_t
Little8ToBig8(uint64_t v)
{
    return (v>>56) |
           ((v&0x00ff000000000000)>>40) |
           ((v&0x0000ff0000000000)>>24) |
           ((v&0x000000ff00000000)>>8)  |
           ((v&0x00000000ff000000)<<8)  |
           ((v&0x0000000000ff0000)<<24) |
           ((v&0x000000000000ff00)<<40) |
           (v<<56);
}

static int
WriteBig2(uint16_t v, FILE *fp)
{
    uint16_t tmp = Little2ToBig2(v);
    return fwrite(&tmp, 1, 2, fp);
}

static int
WriteBig4(uint32_t v, FILE *fp)
{
    uint32_t tmp = Little4ToBig4(v);
    return fwrite(&tmp, 1, 4, fp);
}

static int
WriteBig8(uint64_t v, FILE *fp)
{
    uint64_t tmp = Little8ToBig8(v);
    return fwrite(&tmp, 1, 8, fp);
}

#define WRITE_BIG2(v, fp)       \
    if (WriteBig2(v, fp) < 2) { \
        return -1;              \
    }

#define WRITE_BIG4(v, fp)       \
    if (WriteBig4(v, fp) < 4) { \
        return -1;              \
    }

#define WRITE_BIG8(v, fp)       \
    if (WriteBig8(v, fp) < 8) { \
        return -1;              \
    }

struct DsdiffFormDsdChunk {
    uint64_t ckDataSize;

    DsdiffFormDsdChunk(void) {
        ckDataSize = 0;
    }

    int WriteToFile(FILE *fp) {
        FWRITE(&FOURCC_FRM8, 4, fp);
        WRITE_BIG8(ckDataSize, fp);
        fprintf(fp, "DSD ");
        return 0;
    }
};

struct DsdiffFormVersionChunk {
    uint64_t ckDataSize;
    uint32_t version;

    DsdiffFormVersionChunk(void) {
        ckDataSize = 4;
        version    = 0x01050000;
    }

    int WriteToFile(FILE *fp) {
        FWRITE(&FOURCC_FVER, 4, fp);
        WRITE_BIG8(ckDataSize, fp);
        WRITE_BIG4(version, fp);
        return 0;
    }
};

struct DsdiffPropertyChunk {
    uint64_t ckDataSize;

    DsdiffPropertyChunk(void) {
        ckDataSize = 0;
    }

    int WriteToFile(FILE *fp) {
        FWRITE(&FOURCC_PROP, 4, fp);
        WRITE_BIG8(ckDataSize, fp);
        fprintf(fp, "SND ");
        return 0;
    }
};

struct DsdiffSampleRateChunk {
    uint64_t ckDataSize;
    uint32_t sampleRate;

    DsdiffSampleRateChunk(void) {
        ckDataSize = 4;
    }

    int WriteToFile(FILE *fp) {
        FWRITE(&FOURCC_FS, 4, fp);
        WRITE_BIG8(ckDataSize, fp);
        WRITE_BIG4(sampleRate, fp);
        return 0;
    }
};

struct DsdiffChannelsChunk {
    uint64_t ckDataSize;
    uint16_t numChannels;

    DsdiffChannelsChunk(void) {
        ckDataSize = 0xa;
        numChannels = 2;
    }

    int WriteToFile(FILE *fp) {
        FWRITE(&FOURCC_CHNL, 4, fp);
        WRITE_BIG8(ckDataSize, fp);
        WRITE_BIG2(numChannels, fp);
        fprintf(fp, "SLFTSRGT");
        return 0;
    }
};

struct DsdiffCompressionTypeChunk {
    uint64_t ckDataSize;

    DsdiffCompressionTypeChunk(void) {
        ckDataSize = 0x14;
    }

    int WriteToFile(FILE *fp) {
        FWRITE(&FOURCC_CMPR, 4, fp);
        WRITE_BIG8(ckDataSize, fp);
        fprintf(fp, "DSD ");
        const unsigned char count = 0xe;
        FWRITE(&count, 1, fp);
        fprintf(fp, "not compressed");
        const unsigned char term = 0;
        FWRITE(&term, 1, fp);
        return 0;
    }
};

struct DsdiffSoundDataChunk {
    uint64_t ckDataSize;

    DsdiffSoundDataChunk(void) {
        ckDataSize = 0;
    }

    int WriteToFile(FILE *fp) {
        FWRITE(&FOURCC_DSD, 4, fp);
        WRITE_BIG8(ckDataSize, fp);
        return 0;
    }
};

static void CreateDsdiffFile(FILE *fp)
{
    DsdiffFormDsdChunk     frm8Chunk;
    DsdiffFormVersionChunk fverChunk;
    DsdiffPropertyChunk    propChunk;
    DsdiffSampleRateChunk  fsChunk;
    DsdiffChannelsChunk    chnlChunk;
    DsdiffCompressionTypeChunk cmprChunk;
    DsdiffSoundDataChunk   dataChunk;

    fsChunk.sampleRate = SAMPLE_RATE;
    dataChunk.ckDataSize = NUM_CHANNELS * (SAMPLE_RATE/8) * OUTPUT_SECONDS;
    propChunk.ckDataSize = 4 + (fsChunk.ckDataSize + 12) + (chnlChunk.ckDataSize + 12) + (cmprChunk.ckDataSize + 12);
    frm8Chunk.ckDataSize = 4 + (fverChunk.ckDataSize + 12) + (propChunk.ckDataSize + 12) + (dataChunk.ckDataSize + 12);

    frm8Chunk.WriteToFile(fp);
    fverChunk.WriteToFile(fp);
    propChunk.WriteToFile(fp);
    fsChunk.WriteToFile(fp);
    chnlChunk.WriteToFile(fp);
    cmprChunk.WriteToFile(fp);
    dataChunk.WriteToFile(fp);

    GenerateDffData((SAMPLE_RATE/8) * OUTPUT_SECONDS, NUM_CHANNELS, fp);
}

int main(int argc, char* argv[])
{
    FILE *fp = fopen(OUTPUT_FILENAME, "wb");
    if (NULL == fp) {
        printf("could not open %s\n", OUTPUT_FILENAME);
        return 1;
    }

    CreateDsdiffFile(fp);

    fclose(fp);

    return 0;
}
