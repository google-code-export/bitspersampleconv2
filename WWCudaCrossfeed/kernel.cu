#include <stdio.h>
#include <string.h> //< memset()
#include <math.h>
#include <assert.h>

#include <cufft.h>

#include "WWFlacRW.h"
#include <vector>
#include <float.h>

#define CROSSFEED_COEF_NUM (4)
#define NUM_THREADS_PER_BLOCK (32)
#define BLOCK_X (32768)

struct CrossfeedParam {
    int numChannels;
    float *coeffs[CROSSFEED_COEF_NUM];
    cufftComplex *spectra[CROSSFEED_COEF_NUM];

    int sampleRate;
    int coeffSize;

    CrossfeedParam(void) {
        numChannels = 0;
        sampleRate = 0;
        coeffSize = 0;

        for (int i=0; i<CROSSFEED_COEF_NUM; ++i) {
            coeffs[i]  = NULL;
            spectra[i] = NULL;
        }
    }

    void Term(void) {
        for (int i=0; i<CROSSFEED_COEF_NUM; ++i) {
            delete [] coeffs[i];
            coeffs[i] = NULL;

            cudaFree(spectra[i]);
            spectra[i] = NULL;
        }
    }
};

struct PcmSamplesPerChannel {
    size_t totalSamples;
    float *inputSamples;
    float *outputSamples;
    cufftComplex *spectrum;

    void Init(void) {
        inputSamples = NULL;
        outputSamples = NULL;
        spectrum = NULL;
    }

    void Term(void) {
        delete [] inputSamples;
        inputSamples = NULL;

        delete [] outputSamples;
        outputSamples = NULL;

        cudaFree(spectrum);
        spectrum = NULL;
    }
};

static bool
ReadOneLine(FILE *fp, char *line_return, size_t lineBytes)
{
    line_return[0] = 0;
    int c;
    int pos = 0;

    do {
        c = fgetc(fp);
        if (c == EOF || c == '\n') {
            break;
        }

        if (c != '\r') {
            line_return[pos] = (char)c;
            line_return[pos+1] = 0;
            ++pos;
        }
    } while (c != EOF && pos < (int)lineBytes -1);

    return c != EOF;
}

#define CHECKED(x) if (!(x)) { goto END; }

static bool
ReadCrossfeeedParamsFromFile(const wchar_t *path, CrossfeedParam *param_return)
{
    assert(param_return);

    char buff[256];
    bool result = false;
    FILE *fp;
    errno_t ercd = _wfopen_s(&fp, path, L"rb");
    if (NULL == fp || 0 != ercd) {
        return false;
    }

    CHECKED(ReadOneLine(fp, buff, sizeof buff));
    CHECKED(0 == strncmp(buff, "CFD1", 4));

    param_return->numChannels = 2;

    CHECKED(ReadOneLine(fp, buff, sizeof buff));
    sscanf(buff, "%d", &param_return->sampleRate);

    CHECKED(ReadOneLine(fp, buff, sizeof buff));
    sscanf(buff, "%d", &param_return->coeffSize);

    CHECKED(0 < param_return->coeffSize);

    for (int ch=0; ch<CROSSFEED_COEF_NUM; ++ch) {
        param_return->coeffs[ch] = new float[param_return->coeffSize];
    }

    for (int i=0; i<param_return->coeffSize; ++i) {
#if CROSSFEED_COEF_NUM != 4
#  error
#endif
        double v[CROSSFEED_COEF_NUM];

        CHECKED(ReadOneLine(fp, buff, sizeof buff));
        sscanf(buff, "%lf, %lf, %lf, %lf", &v[0], &v[1], &v[2], &v[3]);

        for (int ch=0; ch<CROSSFEED_COEF_NUM; ++ch) {
            param_return->coeffs[ch][i] = (float)v[ch];
        }
    }

    result = true;

END:
    fclose(fp);
    fp = NULL;
    return result;
}

static void
SetInputPcmSamples(uint8_t *buff, int bitsPerSample, PcmSamplesPerChannel *ppc_return)
{
    assert(ppc_return);

    switch (bitsPerSample) {
    case 16:
        for (size_t samplePos=0; samplePos<ppc_return->totalSamples; ++samplePos) {
            short v = (short)(buff[samplePos*2] + (buff[samplePos*2+1]<<8));
            ppc_return->inputSamples[samplePos] = float(v) * (1.0f / 32768.0f);
        }
        break;
    case 24:
        for (size_t samplePos=0; samplePos<ppc_return->totalSamples; ++samplePos) {
            int v = (int)((buff[samplePos*3]<<8) + (buff[samplePos*3+1]<<16) + (buff[samplePos*3+2]<<24));
            ppc_return->inputSamples[samplePos] = float(v) * (1.0f / 2147483648.0f);
        }
        break;
    default:
        assert(!"not supported");
        break;
    }
}

static size_t
NextPowerOf2(size_t v)
{
    size_t result = 1;
    if (INT_MAX+1U < v) {
        printf("Error: NextPowerOf2(%d) too large!\n", v);
        return 0;
    }
    while (result < v) {
        result *= 2;
    }
    return result;
}
static const char *
CudaFftGetErrorString(cufftResult error)
{
    switch (error) {
        case CUFFT_SUCCESS:       return "CUFFT_SUCCESS";
        case CUFFT_INVALID_PLAN:  return "CUFFT_INVALID_PLAN";
        case CUFFT_ALLOC_FAILED:  return "CUFFT_ALLOC_FAILED";
        case CUFFT_INVALID_TYPE:  return "CUFFT_INVALID_TYPE";
        case CUFFT_INVALID_VALUE: return "CUFFT_INVALID_VALUE";

        case CUFFT_INTERNAL_ERROR: return "CUFFT_INTERNAL_ERROR";
        case CUFFT_EXEC_FAILED:    return "CUFFT_EXEC_FAILED";
        case CUFFT_SETUP_FAILED:   return "CUFFT_SETUP_FAILED";
        case CUFFT_INVALID_SIZE:   return "CUFFT_INVALID_SIZE";
        case CUFFT_UNALIGNED_DATA: return "CUFFT_UNALIGNED_DATA";

        case CUFFT_INCOMPLETE_PARAMETER_LIST: return "CUFFT_INCOMPLETE_PARAMETER_LIST";
        case CUFFT_INVALID_DEVICE:            return "CUFFT_INVALID_DEVICE";
        case CUFFT_PARSE_ERROR:               return "CUFFT_PARSE_ERROR";
        case CUFFT_NO_WORKSPACE:              return "CUFFT_NO_WORKSPACE";
        default: return "unknown";
    }
}


#define CHK_CUDAERROR(x)                                                              \
    ercd = x;                                                                         \
    if (cudaSuccess != ercd) {                                                        \
        printf("%s failed. errorcode=%d (%s)\n", #x, ercd, cudaGetErrorString(ercd)); \
        return NULL;                                                                  \
    }

#define CHK_CUFFT(x)                                                                               \
    fftResult = x;                                                                                 \
    if (cudaSuccess != fftResult) {                                                                \
        printf("%s failed. errorcode=%d (%s)\n", #x, fftResult, CudaFftGetErrorString(fftResult)); \
        return NULL;                                                                               \
    }

static cufftComplex *
CreateSpectrum(float *timeDomainData, int numSamples, int fftSize)
{
    cufftReal *cuFromT = NULL;
    cudaError_t ercd;
    cufftResult fftResult;
    cufftComplex *spectrum;
    cufftHandle plan = 0;

    CHK_CUDAERROR(cudaMalloc((void**)&cuFromT, sizeof(cufftReal)*fftSize));
    CHK_CUDAERROR(cudaMemset((void*)cuFromT, 0, sizeof(cufftReal)*fftSize));
    CHK_CUDAERROR(cudaMemcpy(cuFromT, timeDomainData, numSamples * sizeof(float), cudaMemcpyHostToDevice));
    CHK_CUDAERROR(cudaMalloc((void**)&spectrum, sizeof(cufftComplex)*fftSize));

    CHK_CUFFT(cufftPlan1d(&plan, fftSize, CUFFT_R2C, 1));
    CHK_CUFFT(cufftExecR2C(plan, cuFromT, spectrum));

    cudaDeviceSynchronize();

    if (plan != 0) {
        cufftDestroy(plan);
        plan = 0;
    }

    cudaFree(cuFromT);
    cuFromT = NULL;

    return spectrum;
}

__global__ void
ElementWiseMulCuda(cufftComplex *C, cufftComplex *A, cufftComplex *B)
{
    int offs = threadIdx.x + (blockDim.x * blockDim.y) * (blockIdx.x + gridDim.x * blockIdx.y);
    C[offs].x = A[offs].x * B[offs].x - A[offs].y * B[offs].y;
    C[offs].y = A[offs].x * B[offs].y + A[offs].y * B[offs].x;
}

__global__ void
ElementWiseAddCuda(cufftReal *C, cufftReal *A, cufftReal *B)
{
    int offs = threadIdx.x + NUM_THREADS_PER_BLOCK * (blockIdx.x + BLOCK_X * blockIdx.y);
    C[offs] = A[offs] + B[offs];
}

static float *
CrossfeedMix(cufftComplex *inPcm[2], cufftComplex *coeff[2], int nFFT, int pcmSamples)
{
    dim3 threads(1);
    dim3 blocks(1);
    cudaError_t ercd;
    cufftResult fftResult;
    cufftHandle plan = 0;
    cufftComplex *cuFreq = NULL;
    cufftReal *cuTime[2] = {NULL, NULL};
    cufftReal *cuTimeMixed = NULL;

    if ((nFFT / NUM_THREADS_PER_BLOCK) <= 1) {
        threads.x = nFFT;
    } else {
        threads.x = NUM_THREADS_PER_BLOCK;
        threads.y = 1;
        threads.z = 1;
        int countRemain = nFFT / NUM_THREADS_PER_BLOCK;
        if ((countRemain / BLOCK_X) <= 1) {
            blocks.x = countRemain;
            blocks.y = 1;
            blocks.z = 1;
        } else {
            blocks.x = BLOCK_X;
            countRemain /= BLOCK_X;
            blocks.y = countRemain;
            blocks.z = 1;
        }
    }

    CHK_CUDAERROR(cudaMalloc((void**)&cuFreq,      sizeof(cufftComplex)*nFFT));
    CHK_CUDAERROR(cudaMalloc((void**)&cuTime[0],   sizeof(cufftReal)*nFFT));
    CHK_CUDAERROR(cudaMalloc((void**)&cuTime[1],   sizeof(cufftReal)*nFFT));

    cudaDeviceSynchronize();

    for (int ch=0; ch<2; ++ch) {
        ElementWiseMulCuda<<<blocks, threads>>>(cuFreq, inPcm[ch], coeff[ch]);
    
        cudaDeviceSynchronize();

        CHK_CUFFT(cufftPlan1d(&plan, nFFT, CUFFT_C2R, 1));
        CHK_CUFFT(cufftExecC2R(plan, cuFreq, cuTime[ch]));

        cudaDeviceSynchronize();

        cufftDestroy(plan);
        plan = 0;
    }

    cudaFree(cuFreq);
    cuFreq = NULL;

    CHK_CUDAERROR(cudaMalloc((void**)&cuTimeMixed, sizeof(cufftReal)*nFFT));

    cudaDeviceSynchronize();

    ElementWiseAddCuda<<<blocks, threads>>>(cuTimeMixed, cuTime[0], cuTime[1]);

    cudaDeviceSynchronize();

    for (int ch=0; ch<2; ++ch) {
        cudaFree(cuTime[ch]);
        cuTime[ch] = NULL;
    }

    cudaDeviceSynchronize();

    float *result = new float[pcmSamples];
    CHK_CUDAERROR(cudaMemcpy(result, cuTimeMixed, pcmSamples * sizeof(float), cudaMemcpyDeviceToHost));

    cudaDeviceSynchronize();

    cudaFree(cuTimeMixed);
    cuTimeMixed = NULL;

    cudaDeviceSynchronize();

    return result;
}

static void
NormalizeOutputPcm(std::vector<PcmSamplesPerChannel> & pcmSamples)
{
    float minV = FLT_MAX;
    float maxV = FLT_MIN;

    for (size_t ch=0; ch<pcmSamples.size(); ++ch) {
        for (size_t i=0; i<pcmSamples[ch].totalSamples; ++i) {
            if (maxV < pcmSamples[ch].outputSamples[i]) {
                maxV = pcmSamples[ch].outputSamples[i];
            }
            if (pcmSamples[ch].outputSamples[i] < minV) {
                minV = pcmSamples[ch].outputSamples[i];
            }
        }
    }

    float absMax = (fabsf(minV) < fabsf(maxV)) ? fabsf(maxV) : fabsf(minV);
    float scale = 1.0f;
    if ((8388607.0f / 8388608.0f) < absMax) {
        scale = (8388607.0f / 8388608.0f) / absMax;
    }

    for (size_t ch=0; ch<pcmSamples.size(); ++ch) {
        for (size_t i=0; i<pcmSamples[ch].totalSamples; ++i) {
            pcmSamples[ch].outputSamples[i] *= scale;
        }
    }
}

static bool
WriteFlacFile(const WWFlacMetadata &meta, const uint8_t *picture, std::vector<PcmSamplesPerChannel> &pcmSamples, const wchar_t *path)
{
    bool result = false;
    int rv;
    int pictureBytes = meta.pictureBytes;

    int id = WWFlacRW_EncodeInit(meta);
    if (id < 0) {
        return false;
    }

    if (0 < pictureBytes) {
        rv = WWFlacRW_EncodeSetPicture(id, picture, pictureBytes);
        if (rv < 0) {
            goto END;
        }
    }

    for (int ch=0; ch<meta.channels; ++ch) {
        uint8_t *pcmDataUint8 = new uint8_t[(size_t)(meta.totalSamples * 3)];
        for (int i=0; i<meta.totalSamples; ++i) {
            int v = (int)(8388608.0f * pcmSamples[ch].outputSamples[i]);
            pcmDataUint8[i*3+0] = v&0xff;
            pcmDataUint8[i*3+1] = (v>>8)&0xff;
            pcmDataUint8[i*3+2] = (v>>16)&0xff;
        }

        rv = WWFlacRW_EncodeAddPcm(id, ch, pcmDataUint8, meta.totalSamples*3);
        if (rv < 0) {
            goto END;
        }
        delete [] pcmDataUint8;
        pcmDataUint8 = NULL;
    }

    rv = WWFlacRW_EncodeRun(id, path);
    if (rv < 0) {
        goto END;
    }

    result = true;
END:

    WWFlacRW_EncodeEnd(id);

    return result;
}

int wmain(int argc, wchar_t *argv[])
{
    int result = 1;
    int ercd;
    int id = -1;
    size_t nFFT;
    CrossfeedParam crossfeedParam;
    WWFlacMetadata meta;
    uint8_t * picture = NULL;
    cufftComplex * inPcmSpectra[2];
    int64_t usedGpuMemoryBytes = 0;

    std::vector<PcmSamplesPerChannel> pcmSamples;

    if (argc != 4) {
        printf("Usage: %S coeffFile inputFile outputFile\n", argv[0]);
        goto END;
    }

    if (!ReadCrossfeeedParamsFromFile(argv[1], &crossfeedParam)) {
        printf("Error: could not read crossfeed param file %S\n", argv[1]);
        goto END;
    }

    id = WWFlacRW_DecodeAll(argv[2]);
    if (id < 0) {
        printf("Error: Read failed %S\n", argv[2]);
        goto END;
    }

    ercd = WWFlacRW_GetDecodedMetadata(id, meta);
    if (ercd < 0) {
        printf("Error: Read meta failed %S\n", argv[2]);
        goto END;
    }

    if (0 < meta.pictureBytes) {
        picture = new uint8_t[meta.pictureBytes];
        ercd = WWFlacRW_GetDecodedPicture(id, picture, meta.pictureBytes);
        if (ercd < 0) {
            printf("Error: Read meta failed %S\n", argv[2]);
            goto END;
        }
    }

    if (meta.channels != crossfeedParam.numChannels) {
        printf("Error: channel count mismatch. FLAC ch=%d, crossfeed ch=%d\n", meta.channels, crossfeedParam.numChannels);
        goto END;
    }

    if (meta.channels != crossfeedParam.numChannels) {
        printf("Error: samplerate mismatch. FLAC=%d, crossfeed=%d\n", meta.sampleRate, crossfeedParam.sampleRate);
        goto END;
    }

    for (int ch=0; ch<meta.channels; ++ch) {
        size_t bytes = (size_t)(meta.totalSamples * (meta.bitsPerSample/8));
        uint8_t *buff = new uint8_t[bytes];
        WWFlacRW_GetDecodedPcmBytes(id, ch, 0, buff, bytes);

        PcmSamplesPerChannel ppc;
        ppc.Init();
        ppc.totalSamples = (size_t)meta.totalSamples;
        ppc.inputSamples = new float[(size_t)(meta.totalSamples * sizeof(float))];
        ppc.outputSamples = NULL;
        ppc.spectrum = NULL;

        SetInputPcmSamples(buff, meta.bitsPerSample, &ppc);

        delete [] buff;
        buff = NULL;

        pcmSamples.push_back(ppc);
    }

    WWFlacRW_DecodeEnd(id);
    id = -1;

    nFFT = (size_t)((crossfeedParam.coeffSize < meta.totalSamples) ? meta.totalSamples : crossfeedParam.coeffSize);
    nFFT = NextPowerOf2(nFFT);

    for (int i=0; i<CROSSFEED_COEF_NUM; ++i) {
        crossfeedParam.spectra[i] = CreateSpectrum(crossfeedParam.coeffs[i], crossfeedParam.coeffSize, nFFT);
        if (crossfeedParam.spectra[i] == NULL) {
            goto END;
        }
        usedGpuMemoryBytes += nFFT * sizeof(cufftComplex);
    }
    for (int ch=0; ch<meta.channels; ++ch) {
        pcmSamples[ch].spectrum = CreateSpectrum(pcmSamples[ch].inputSamples, pcmSamples[ch].totalSamples, nFFT);
        if (pcmSamples[ch].spectrum == NULL) {
            goto END;
        }
        usedGpuMemoryBytes += nFFT * sizeof(cufftComplex);
    }

    inPcmSpectra[0] = pcmSamples[0].spectrum;
    inPcmSpectra[1] = pcmSamples[1].spectrum;
    pcmSamples[0].outputSamples = CrossfeedMix(inPcmSpectra, &crossfeedParam.spectra[0], nFFT, pcmSamples[0].totalSamples);
    if (pcmSamples[0].outputSamples == NULL) {
        usedGpuMemoryBytes += nFFT * sizeof(cufftReal);
        goto END;
    }
    pcmSamples[1].outputSamples = CrossfeedMix(inPcmSpectra, &crossfeedParam.spectra[2], nFFT, pcmSamples[0].totalSamples);
    if (pcmSamples[1].outputSamples == NULL) {
        usedGpuMemoryBytes += nFFT * sizeof(cufftReal);
        goto END;
    }

    NormalizeOutputPcm(pcmSamples);

    // o—Íbit depth == 24bit
    meta.bitsPerSample = 24;
    if (!WriteFlacFile(meta, picture, pcmSamples, argv[3])) {
        printf("Error: WriteFlac(%S) failed\n", argv[3]);
        goto END;
    }

    result = 0;

END:
    delete [] picture;
    picture = NULL;

    for (size_t i=0; i<pcmSamples.size(); ++i) {
        pcmSamples[i].Term();
    }
    pcmSamples.clear();

    crossfeedParam.Term();

    if (result != 0) {
        printf("Failed!\n");
    } else {
        printf("Used GPU memory: %lld Mbytes.\n", usedGpuMemoryBytes/1024/1024);
        printf("Succeeded to write %S.\n", argv[3]);
    }

    return result;
}