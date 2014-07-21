#include <stdio.h>
#include <string.h> //< memset()
#include <math.h>
#include <assert.h>

#include <cufft.h>

#include "WWFlacRW.h"
#include <vector>
#include <float.h>

#define CROSSFEED_COEF_NUM (8)
#define NUM_THREADS_PER_BLOCK (32)
#define BLOCK_X (32768)

enum PcmChannelType {
    PCT_LeftLow,
    PCT_LeftHigh,
    PCT_RightLow,
    PCT_RightHigh,
    PCT_NUM
};

// 44.1kHz用 1kHz以下を取り出すLPF。ディレイをHPFと合わせる。
static float gLpf[] = {
    0.0, 0.0, 0.0, 0.0,
    0.0, 0.0, 0.0, 0.0,
    0.005228327, 0.003249754, 0.004192373, 0.005265026,
    0.006468574, 0.007797099, 0.009237486, 0.010779043,
    0.012417001, 0.014132141, 0.01589555, 0.017701121,
    0.019508703, 0.021304869, 0.023059883,0.024747905,
    0.02634363, 0.027823228, 0.029158971, 0.030331066,
    0.031319484, 0.032104039, 0.032676435, 0.033022636,
    0.033138738, 0.033022636, 0.032676435, 0.032104039,
    0.031319484, 0.030331066, 0.029158971, 0.027823228,
    0.02634363, 0.024747905, 0.023059883, 0.021304869,
    0.019508703, 0.017701121, 0.01589555, 0.014132141,
    0.012417001, 0.010779043, 0.009237486, 0.007797099,
    0.006468574, 0.005265026, 0.004192373, 0.003249754,
    0.005228327 };

// 44.1kHz用 1kHz以上を取り出すHPF。LPFとコンプリメンタリーになっている。
static float gHpf[] = {
            0.18284994666072, 0.135236586884136, 0.122587729966973, 0.0194160387180315,
            -0.018554433938607, -0.147953150443285, -0.151175453402614, -0.349031089410704,
            0.402427310594025, -0.0290473999611028, -0.158700363343139, -0.0615684548751206,
            -0.0757049588858041, -0.00276365388846877, -0.0170037945344803, 0.024517130588635,

            -0.00946482719827604, 0.0131620130045816, -0.0290320503133136, 0.00288266370987212,
            -0.0316496185674791, 0.0377990630692439, 0.00347129302294323, 0.190514306269757,
            -0.0464892033979637, -0.0815022375716482, 0.0915280717216996, 0.0379499306403254,
            0.0878060905218899, 0.0380322544842594, 0.0621210160273231, 0.0199502643669319,
            
            0.0479758154442058, 0.0199502643669309, 0.062121016027322, 0.0380322544842602,
            0.0878060905218885, 0.0379499306403286, 0.0915280717216994, -0.0815022375716453,
            -0.0464892033979671, 0.190514306269758, 0.00347129302294125, 0.0377990630692462,
            -0.0316496185674813, 0.0028826637098741, -0.029032050313314, 0.0131620130045817,

            -0.00946482719827875, 0.024517130588635, -0.0170037945344796, -0.00276365388846837,
            -0.0757049588858039, -0.061568454875121, -0.158700363343138, -0.0290473999611048,
            0.402427310594025, -0.349031089410705, -0.151175453402611, -0.147953150443284,
            -0.0185544339386063, 0.0194160387180304, 0.122587729966972, 0.135236586884135,
            0.18284994666072, };

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
    float *inputPcm;
    float *outputPcm;
    cufftComplex *spectrum;

    void Init(void) {
        inputPcm = NULL;
        outputPcm = NULL;
        spectrum = NULL;
    }

    void Term(void) {
        delete [] inputPcm;
        inputPcm = NULL;

        delete [] outputPcm;
        outputPcm = NULL;

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
    CHECKED(0 == strncmp(buff, "CFD2", 4));

    param_return->numChannels = 2;

    CHECKED(ReadOneLine(fp, buff, sizeof buff));
    sscanf(buff, "%d", &param_return->sampleRate);

    CHECKED(ReadOneLine(fp, buff, sizeof buff));
    sscanf(buff, "%d", &param_return->coeffSize);

    CHECKED(0 < param_return->coeffSize);

    // コメント行。スキップする。
    CHECKED(ReadOneLine(fp, buff, sizeof buff));

    for (int ch=0; ch<CROSSFEED_COEF_NUM; ++ch) {
        param_return->coeffs[ch] = new float[param_return->coeffSize];
    }

    for (int i=0; i<param_return->coeffSize; ++i) {
#if CROSSFEED_COEF_NUM != 8
#  error
#endif
        double v[CROSSFEED_COEF_NUM];

        CHECKED(ReadOneLine(fp, buff, sizeof buff));
        CHECKED(8 == sscanf(buff, "%lf, %lf, %lf, %lf, %lf, %lf, %lf, %lf", &v[0], &v[1], &v[2], &v[3], &v[4], &v[5], &v[6], &v[7]));

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
            ppc_return->inputPcm[samplePos] = float(v) * (1.0f / 32768.0f);
        }
        break;
    case 24:
        for (size_t samplePos=0; samplePos<ppc_return->totalSamples; ++samplePos) {
            int v = (int)((buff[samplePos*3]<<8) + (buff[samplePos*3+1]<<16) + (buff[samplePos*3+2]<<24));
            ppc_return->inputPcm[samplePos] = float(v) * (1.0f / 2147483648.0f);
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

__global__ void
ElementWiseMulCuda(cufftComplex *C, cufftComplex *A, cufftComplex *B)
{
    int offs = threadIdx.x + NUM_THREADS_PER_BLOCK * (blockIdx.x + BLOCK_X * blockIdx.y);
    C[offs].x = A[offs].x * B[offs].x - A[offs].y * B[offs].y;
    C[offs].y = A[offs].x * B[offs].y + A[offs].y * B[offs].x;
}

__global__ void
ElementWiseAddCuda(cufftReal *C, cufftReal *A, cufftReal *B)
{
    int offs = threadIdx.x + NUM_THREADS_PER_BLOCK * (blockIdx.x + BLOCK_X * blockIdx.y);
    C[offs] = A[offs] + B[offs];
}

static void
CudaElementWiseMul(int count, cufftComplex *dest, cufftComplex *from0, cufftComplex *from1)
{
    dim3 threads(1);
    dim3 blocks(1);

    if ((count / NUM_THREADS_PER_BLOCK) <= 1) {
        threads.x = count;
    } else {
        threads.x = NUM_THREADS_PER_BLOCK;
        threads.y = 1;
        threads.z = 1;
        int countRemain = count / NUM_THREADS_PER_BLOCK;
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

    ElementWiseMulCuda<<<blocks, threads>>>(dest, from0, from1);
    cudaDeviceSynchronize();
}

static void
CudaElementWiseAdd(int count, cufftReal *dest, cufftReal *from0, cufftReal *from1)
{
    dim3 threads(1);
    dim3 blocks(1);

    if ((count / NUM_THREADS_PER_BLOCK) <= 1) {
        threads.x = count;
    } else {
        threads.x = NUM_THREADS_PER_BLOCK;
        threads.y = 1;
        threads.z = 1;
        int countRemain = count / NUM_THREADS_PER_BLOCK;
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

    ElementWiseAddCuda<<<blocks, threads>>>(dest, from0, from1);
    cudaDeviceSynchronize();
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

    cufftDestroy(plan);
    plan = 0;

    cudaFree(cuFromT);
    cuFromT = NULL;

    return spectrum;
}

static float *
FirFilter(float *firCoeff, size_t firCoeffNum, PcmSamplesPerChannel &input, PcmSamplesPerChannel *pOutput)
{
    size_t fftSize = (firCoeffNum < input.totalSamples) ? input.totalSamples: firCoeffNum;
    fftSize = NextPowerOf2(fftSize);
    if (fftSize == 0) {
        return NULL;
    }

    cudaError_t ercd;
    cufftResult fftResult;
    cufftReal *coefTime = NULL;
    cufftReal *pcmTime = NULL;
    cufftReal *resultTime = NULL;
    cufftComplex *coefFreq = NULL;
    cufftComplex *pcmFreq = NULL;
    cufftComplex *resultFreq = NULL;
    cufftHandle plan = 0;

    CHK_CUDAERROR(cudaMalloc((void**)&coefTime, sizeof(cufftReal)*fftSize));
    CHK_CUDAERROR(cudaMemset((void*)coefTime, 0, sizeof(cufftReal)*fftSize));
    CHK_CUDAERROR(cudaMemcpy(coefTime, firCoeff, firCoeffNum * sizeof(float), cudaMemcpyHostToDevice));
    CHK_CUDAERROR(cudaMalloc((void**)&coefFreq, sizeof(cufftComplex)*fftSize));

    CHK_CUFFT(cufftPlan1d(&plan, fftSize, CUFFT_R2C, 1));
    CHK_CUFFT(cufftExecR2C(plan, coefTime, coefFreq));

    cudaDeviceSynchronize();

    cufftDestroy(plan);
    plan = 0;

    cudaFree(coefTime);
    coefTime = NULL;

    CHK_CUDAERROR(cudaMalloc((void**)&pcmTime, sizeof(cufftReal)*fftSize));
    CHK_CUDAERROR(cudaMemset((void*)pcmTime, 0, sizeof(cufftReal)*fftSize));
    CHK_CUDAERROR(cudaMemcpy(pcmTime, input.inputPcm, input.totalSamples * sizeof(float), cudaMemcpyHostToDevice));
    CHK_CUDAERROR(cudaMalloc((void**)&pcmFreq, sizeof(cufftComplex)*fftSize));

    cudaDeviceSynchronize();

    CHK_CUFFT(cufftPlan1d(&plan, fftSize, CUFFT_R2C, 1));
    CHK_CUFFT(cufftExecR2C(plan, pcmTime, pcmFreq));

    cudaDeviceSynchronize();

    cufftDestroy(plan);
    plan = 0;

    cudaFree(pcmTime);
    pcmTime = NULL;

    CHK_CUDAERROR(cudaMalloc((void**)&resultFreq, sizeof(cufftComplex)*fftSize));
    CudaElementWiseMul(fftSize, resultFreq, coefFreq, pcmFreq);

    cudaDeviceSynchronize();

    cudaFree(coefFreq);
    coefFreq = NULL;
    cudaFree(pcmFreq);
    pcmFreq = NULL;

    CHK_CUDAERROR(cudaMalloc((void**)&resultTime, sizeof(cufftReal)*fftSize));

    cudaDeviceSynchronize();

    CHK_CUFFT(cufftPlan1d(&plan, fftSize, CUFFT_C2R, 1));
    CHK_CUFFT(cufftExecC2R(plan, resultFreq, resultTime));

    cudaDeviceSynchronize();

    cufftDestroy(plan);
    plan = 0;

    cudaFree(resultFreq);
    resultFreq = NULL;

    CHK_CUDAERROR(cudaMemcpy(pOutput->inputPcm, resultTime, input.totalSamples * sizeof(float), cudaMemcpyDeviceToHost));
    
    cudaDeviceSynchronize();

    cudaFree(resultTime);
    resultTime = NULL;

    return pOutput->inputPcm;
}

static float *
CrossfeedMix(cufftComplex *inPcmSpectra[PCT_NUM], cufftComplex *coeffLo[2], cufftComplex *coeffHi[2], int nFFT, int pcmSamples)
{
    cudaError_t ercd;
    cufftResult fftResult;
    cufftHandle plan = 0;
    cufftComplex *cuFreq = NULL;
    cufftReal *cuTime[PCT_NUM] = {NULL, NULL, NULL, NULL};
    cufftReal *cuTimeMixedLo = NULL;
    cufftReal *cuTimeMixedHi = NULL;
    cufftReal *cuTimeMixed = NULL;

    CHK_CUDAERROR(cudaMalloc((void**)&cuFreq,      sizeof(cufftComplex)*nFFT));

    cudaDeviceSynchronize();

    CHK_CUFFT(cufftPlan1d(&plan, nFFT, CUFFT_C2R, 1));

    for (int ch=0; ch<2; ++ch) {
        CudaElementWiseMul(nFFT, cuFreq, inPcmSpectra[ch*2], coeffLo[ch]);
    
        cudaDeviceSynchronize();

        CHK_CUDAERROR(cudaMalloc((void**)&cuTime[ch*2], sizeof(cufftReal)*nFFT));
        CHK_CUFFT(cufftExecC2R(plan, cuFreq, cuTime[ch*2]));

        cudaDeviceSynchronize();

        CudaElementWiseMul(nFFT, cuFreq, inPcmSpectra[ch*2+1], coeffHi[ch]);
    
        cudaDeviceSynchronize();

        CHK_CUDAERROR(cudaMalloc((void**)&cuTime[ch*2+1], sizeof(cufftReal)*nFFT));
        CHK_CUFFT(cufftExecC2R(plan, cuFreq, cuTime[ch*2+1]));

        cudaDeviceSynchronize();
    }

    cufftDestroy(plan);
    plan = 0;

    cudaFree(cuFreq);
    cuFreq = NULL;

    CHK_CUDAERROR(cudaMalloc((void**)&cuTimeMixedLo, sizeof(cufftReal)*nFFT));
    CHK_CUDAERROR(cudaMalloc((void**)&cuTimeMixedHi, sizeof(cufftReal)*nFFT));
    CHK_CUDAERROR(cudaMalloc((void**)&cuTimeMixed, sizeof(cufftReal)*nFFT));

    cudaDeviceSynchronize();

    CudaElementWiseAdd(nFFT, cuTimeMixedLo, cuTime[0], cuTime[2]);
    CudaElementWiseAdd(nFFT, cuTimeMixedHi, cuTime[1], cuTime[3]);
    CudaElementWiseAdd(nFFT, cuTimeMixed, cuTimeMixedLo, cuTimeMixedHi);

    for (int i=0; i<PCT_NUM; ++i) {
        cudaFree(cuTime[i]);
        cuTime[i] = NULL;
    }
    cudaFree(cuTimeMixedLo);
    cuTimeMixedLo = NULL;
    cudaFree(cuTimeMixedHi);
    cuTimeMixedHi = NULL;

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
        if (pcmSamples[ch].outputPcm == NULL) {
            continue;
        }

        for (size_t i=0; i<pcmSamples[ch].totalSamples; ++i) {
            if (maxV < pcmSamples[ch].outputPcm[i]) {
                maxV = pcmSamples[ch].outputPcm[i];
            }
            if (pcmSamples[ch].outputPcm[i] < minV) {
                minV = pcmSamples[ch].outputPcm[i];
            }
        }
    }

    float absMax = (fabsf(minV) < fabsf(maxV)) ? fabsf(maxV) : fabsf(minV);
    float scale = 1.0f;
    if ((8388607.0f / 8388608.0f) < absMax) {
        scale = (8388607.0f / 8388608.0f) / absMax;
    }

    for (size_t ch=0; ch<pcmSamples.size(); ++ch) {
        if (pcmSamples[ch].outputPcm == NULL) {
            continue;
        }
        for (size_t i=0; i<pcmSamples[ch].totalSamples; ++i) {
            pcmSamples[ch].outputPcm[i] *= scale;
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
            int v = (int)(8388608.0f * pcmSamples[ch].outputPcm[i]);
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
    cufftComplex * inPcmSpectra[PCT_NUM];
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
        ppc.inputPcm = new float[(size_t)(meta.totalSamples * sizeof(float))];
        SetInputPcmSamples(buff, meta.bitsPerSample, &ppc);

        delete [] buff;
        buff = NULL;

        {
            // 低音域
            PcmSamplesPerChannel lowFreq;
            lowFreq.Init();
            lowFreq.totalSamples = ppc.totalSamples;
            lowFreq.inputPcm = new float[ppc.totalSamples];
            if (NULL == FirFilter(gLpf, sizeof gLpf/sizeof gLpf[0], ppc, &lowFreq)) {
                goto END;
            }
            pcmSamples.push_back(lowFreq);
        }

        {
            // 高音域
            PcmSamplesPerChannel highFreq;
            highFreq.Init();
            highFreq.totalSamples = ppc.totalSamples;
            highFreq.inputPcm = new float[ppc.totalSamples];
            if (NULL == FirFilter(gHpf, sizeof gHpf/sizeof gHpf[0], ppc, &highFreq)) {
                goto END;
            }
            pcmSamples.push_back(highFreq);
        }
        ppc.Term();
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
    for (int i=0; i<pcmSamples.size(); ++i) {
        pcmSamples[i].spectrum = CreateSpectrum(pcmSamples[i].inputPcm, pcmSamples[i].totalSamples, nFFT);
        if (pcmSamples[i].spectrum == NULL) {
            goto END;
        }
        usedGpuMemoryBytes += nFFT * sizeof(cufftComplex);
        inPcmSpectra[i] = pcmSamples[i].spectrum;
    }

    pcmSamples[0].outputPcm = CrossfeedMix(inPcmSpectra, &crossfeedParam.spectra[0], &crossfeedParam.spectra[4], nFFT, pcmSamples[0].totalSamples);
    if (pcmSamples[0].outputPcm == NULL) {
        usedGpuMemoryBytes += nFFT * sizeof(cufftReal);
        goto END;
    }
    pcmSamples[1].outputPcm = CrossfeedMix(inPcmSpectra, &crossfeedParam.spectra[2], &crossfeedParam.spectra[6], nFFT, pcmSamples[0].totalSamples);
    if (pcmSamples[1].outputPcm == NULL) {
        usedGpuMemoryBytes += nFFT * sizeof(cufftReal);
        goto END;
    }

    NormalizeOutputPcm(pcmSamples);

    // 出力bit depth == 24bit
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