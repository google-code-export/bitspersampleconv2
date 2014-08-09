#include "CrossfeedF.h"

#include <assert.h>
#include "WWFlacRW.h"

bool
ReadCrossfeeedParamsFromFileF(const wchar_t *path, CrossfeedParam *param_return)
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
        CHECKED(8 == sscanf(buff, "%lf, %lf, %lf, %lf, %lf, %lf, %lf, %lf",
                &v[0], &v[1], &v[2], &v[3], &v[4], &v[5], &v[6], &v[7]));

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

void
SetInputPcmSamplesF(uint8_t *buff, int bitsPerSample, PcmSamplesPerChannel *ppc_return)
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

__global__ void
ElementWiseMulCudaF(cufftComplex *C, cufftComplex *A, cufftComplex *B)
{
    int offs = threadIdx.x + WW_NUM_THREADS_PER_BLOCK * (blockIdx.x + WW_BLOCK_X * blockIdx.y);
    C[offs].x = A[offs].x * B[offs].x - A[offs].y * B[offs].y;
    C[offs].y = A[offs].x * B[offs].y + A[offs].y * B[offs].x;
}

__global__ void
ElementWiseAddCudaF(cufftReal *C, cufftReal *A, cufftReal *B)
{
    int offs = threadIdx.x + WW_NUM_THREADS_PER_BLOCK * (blockIdx.x + WW_BLOCK_X * blockIdx.y);
    C[offs] = A[offs] + B[offs];
}

static void
CudaElementWiseMulF(int count, cufftComplex *dest, cufftComplex *from0, cufftComplex *from1)
{
    dim3 threads(1);
    dim3 blocks(1);

    GetBestBlockThreadSize(count, threads, blocks);
    cudaDeviceSynchronize();
    ElementWiseMulCudaF<<<blocks, threads>>>(dest, from0, from1);
    cudaDeviceSynchronize();
}

static void
CudaElementWiseAddF(int count, cufftReal *dest, cufftReal *from0, cufftReal *from1)
{
    dim3 threads(1);
    dim3 blocks(1);

    GetBestBlockThreadSize(count, threads, blocks);
    cudaDeviceSynchronize();
    ElementWiseAddCudaF<<<blocks, threads>>>(dest, from0, from1);
    cudaDeviceSynchronize();
}

cufftComplex *
CreateSpectrumF(float *timeDomainData, int numSamples, int fftSize)
{
    cufftReal *cuFromT = NULL;
    cudaError_t ercd;
    cufftResult fftResult;
    cufftComplex *spectrum;
    cufftHandle plan = 0;

    CHK_CUDAMALLOC((void**)&cuFromT, sizeof(cufftReal)*fftSize);
    CHK_CUDAERROR(cudaMemset((void*)cuFromT, 0, sizeof(cufftReal)*fftSize));
    CHK_CUDAERROR(cudaMemcpy(cuFromT, timeDomainData, numSamples * sizeof(float), cudaMemcpyHostToDevice));
    CHK_CUDAMALLOC((void**)&spectrum, sizeof(cufftComplex)*fftSize);

    CHK_CUFFT(cufftPlan1d(&plan, fftSize, CUFFT_R2C, 1));
    CHK_CUFFT(cufftExecR2C(plan, cuFromT, spectrum));

    cufftDestroy(plan);
    plan = 0;

    CHK_CUDAFREE(cuFromT, sizeof(cufftReal)*fftSize);
    return spectrum;
}

float *
FirFilterF(float *firCoeff, size_t firCoeffNum, PcmSamplesPerChannel &input, PcmSamplesPerChannel *pOutput)
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

    CHK_CUDAMALLOC((void**)&coefTime, sizeof(cufftReal)*fftSize);
    CHK_CUDAERROR(cudaMemset((void*)coefTime, 0, sizeof(cufftReal)*fftSize));
    CHK_CUDAERROR(cudaMemcpy(coefTime, firCoeff, firCoeffNum * sizeof(float), cudaMemcpyHostToDevice));
    CHK_CUDAMALLOC((void**)&coefFreq, sizeof(cufftComplex)*fftSize);

    CHK_CUFFT(cufftPlan1d(&plan, fftSize, CUFFT_R2C, 1));
    CHK_CUFFT(cufftExecR2C(plan, coefTime, coefFreq));

    CHK_CUDAFREE(coefTime, sizeof(cufftReal)*fftSize);

    CHK_CUDAMALLOC((void**)&pcmTime, sizeof(cufftReal)*fftSize);
    CHK_CUDAERROR(cudaMemset((void*)pcmTime, 0, sizeof(cufftReal)*fftSize));
    CHK_CUDAERROR(cudaMemcpy(pcmTime, input.inputPcm, input.totalSamples * sizeof(float), cudaMemcpyHostToDevice));
    CHK_CUDAMALLOC((void**)&pcmFreq, sizeof(cufftComplex)*fftSize);

    CHK_CUFFT(cufftExecR2C(plan, pcmTime, pcmFreq));

    cufftDestroy(plan);
    plan = 0;

    CHK_CUDAFREE(pcmTime, sizeof(cufftReal)*fftSize);

    CHK_CUDAMALLOC((void**)&resultFreq, sizeof(cufftComplex)*fftSize);
    CudaElementWiseMulF(fftSize, resultFreq, coefFreq, pcmFreq);

    CHK_CUDAFREE(coefFreq, sizeof(cufftComplex)*fftSize);
    CHK_CUDAFREE(pcmFreq, sizeof(cufftComplex)*fftSize);

    CHK_CUDAMALLOC((void**)&resultTime, sizeof(cufftReal)*fftSize);

    CHK_CUFFT(cufftPlan1d(&plan, fftSize, CUFFT_C2R, 1));
    CHK_CUFFT(cufftExecC2R(plan, resultFreq, resultTime));

    cufftDestroy(plan);
    plan = 0;

    CHK_CUDAFREE(resultFreq, sizeof(cufftComplex)*fftSize);

    CHK_CUDAERROR(cudaMemcpy(pOutput->inputPcm, resultTime, input.totalSamples * sizeof(float), cudaMemcpyDeviceToHost));
    CHK_CUDAFREE(resultTime, sizeof(cufftReal)*fftSize);

    return pOutput->inputPcm;
}

float *
CrossfeedMixF(cufftComplex *inPcmSpectra[PCT_NUM], cufftComplex *coeffLo[2],
        cufftComplex *coeffHi[2], int nFFT, int pcmSamples)
{
    cudaError_t ercd;
    cufftResult fftResult;
    cufftHandle plan = 0;
    cufftComplex *cuFreq = NULL;
    cufftReal *cuTime[PCT_NUM] = {NULL, NULL, NULL, NULL};
    cufftReal *cuTimeMixedLo = NULL;
    cufftReal *cuTimeMixedHi = NULL;
    cufftReal *cuTimeMixed = NULL;

    CHK_CUDAMALLOC((void**)&cuFreq, sizeof(cufftComplex)*nFFT);
    CHK_CUFFT(cufftPlan1d(&plan, nFFT, CUFFT_C2R, 1));

    for (int ch=0; ch<2; ++ch) {
        CudaElementWiseMulF(nFFT, cuFreq, inPcmSpectra[ch*2], coeffLo[ch]);

        CHK_CUDAMALLOC((void**)&cuTime[ch*2], sizeof(cufftReal)*nFFT);
        CHK_CUFFT(cufftExecC2R(plan, cuFreq, cuTime[ch*2]));

        CudaElementWiseMulF(nFFT, cuFreq, inPcmSpectra[ch*2+1], coeffHi[ch]);

        CHK_CUDAMALLOC((void**)&cuTime[ch*2+1], sizeof(cufftReal)*nFFT);
        CHK_CUFFT(cufftExecC2R(plan, cuFreq, cuTime[ch*2+1]));
    }

    cufftDestroy(plan);
    plan = 0;

    CHK_CUDAFREE(cuFreq, sizeof(cufftComplex)*nFFT);

    CHK_CUDAMALLOC((void**)&cuTimeMixedLo, sizeof(cufftReal)*nFFT);
    CHK_CUDAMALLOC((void**)&cuTimeMixedHi, sizeof(cufftReal)*nFFT);
    CHK_CUDAMALLOC((void**)&cuTimeMixed, sizeof(cufftReal)*nFFT);

    CudaElementWiseAddF(nFFT, cuTimeMixedLo, cuTime[0], cuTime[2]);
    CudaElementWiseAddF(nFFT, cuTimeMixedHi, cuTime[1], cuTime[3]);
    CudaElementWiseAddF(nFFT, cuTimeMixed, cuTimeMixedLo, cuTimeMixedHi);

    for (int i=0; i<PCT_NUM; ++i) {
        CHK_CUDAFREE(cuTime[i], sizeof(cufftReal)*nFFT);
    }
    CHK_CUDAFREE(cuTimeMixedLo, sizeof(cufftReal)*nFFT);
    CHK_CUDAFREE(cuTimeMixedHi, sizeof(cufftReal)*nFFT);

    float *result = new float[pcmSamples];
    CHK_CUDAERROR(cudaMemcpy(result, cuTimeMixed, pcmSamples * sizeof(float), cudaMemcpyDeviceToHost));

    CHK_CUDAFREE(cuTimeMixed, sizeof(cufftReal)*nFFT);

    return result;
}

void
NormalizeOutputPcmF(std::vector<PcmSamplesPerChannel> & pcmSamples)
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

bool
WriteFlacFileF(const WWFlacMetadata &meta, const uint8_t *picture,
        std::vector<PcmSamplesPerChannel> &pcmSamples, const wchar_t *path)
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