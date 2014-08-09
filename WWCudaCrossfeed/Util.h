#ifndef H_Util
#define H_Util

#include <stdio.h>
#include <stdint.h>
#include <cufft.h>

#define WW_NUM_THREADS_PER_BLOCK (256)
#define WW_BLOCK_X               (32768)

#define CHECKED(x) if (!(x)) { goto END; }
#define CROSSFEED_COEF_NUM (8)

enum PcmChannelType {
    PCT_LeftLow,
    PCT_LeftHigh,
    PCT_RightLow,
    PCT_RightHigh,
    PCT_NUM
};

size_t
NextPowerOf2(size_t v);

bool
ReadOneLine(FILE *fp, char *line_return, size_t lineBytes);

void
GetBestBlockThreadSize(int count, dim3 &threads_return, dim3 &blocks_return);

struct CrossfeedParam {
    int numChannels;
    float *coeffs[CROSSFEED_COEF_NUM];
    cufftComplex *spectra[CROSSFEED_COEF_NUM];

    int sampleRate;
    int coeffSize;
    int fftSize;

    CrossfeedParam(void) {
        numChannels = 0;
        sampleRate = 0;
        coeffSize = 0;

        for (int i=0; i<CROSSFEED_COEF_NUM; ++i) {
            coeffs[i]  = NULL;
            spectra[i] = NULL;
        }
    }
    void Term(void);
};

struct PcmSamplesPerChannel {
    size_t totalSamples;
    float *inputPcm;
    float *outputPcm;
    cufftComplex *spectrum;
    int fftSize;

    void Init(void) {
        inputPcm = NULL;
        outputPcm = NULL;
        spectrum = NULL;
    }

    void Term(void);
};

extern int64_t gCudaAllocatedBytes;
extern int64_t gCudaMaxBytes;

#define CHK_CUDAMALLOC(pp, sz)                                                             \
    ercd = cudaMalloc(pp, sz);                                                             \
    if (cudaSuccess != ercd) {                                                             \
        printf("cudaMalloc(%dMBytes) failed. errorcode=%d (%s). allocated CUDA memory=%lld Mbytes\n", (int)(sz/1024/1024), ercd, cudaGetErrorString(ercd), gCudaAllocatedBytes/1024/1024); \
        return NULL;                                                                       \
    }                                                                                      \
    gCudaAllocatedBytes += sz;                                                             \
    if (gCudaMaxBytes < gCudaAllocatedBytes) {                                             \
        gCudaMaxBytes = gCudaAllocatedBytes;                                               \
    }

#define CHK_CUDAFREE(p, sz)        \
    cudaFree(p);                   \
    if (p != NULL) {               \
        p = NULL;                  \
        gCudaAllocatedBytes -= sz; \
    }

#endif /* H_Util */

