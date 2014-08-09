#include "Util.h"
#include <stdio.h>

int64_t gCudaAllocatedBytes = 0;
int64_t gCudaMaxBytes = 0;



// 44.1kHz用 1kHz以下を取り出すLPF。
float gLpf[WW_CROSSOVER_COEFF_LENGTH] = {
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
float gHpf[WW_CROSSOVER_COEFF_LENGTH] = {
        -0.005228327,-0.003249754,-0.004192373,-0.005265026,
        -0.006468574,-0.007797099,-0.009237486,-0.010779043,
        -0.012417001,-0.014132141,-0.01589555,-0.017701121,
        -0.019508703,-0.021304869,-0.023059883,-0.024747905,
        -0.02634363,-0.027823228,-0.029158971,-0.030331066,
        -0.031319484,-0.032104039,-0.032676435,-0.033022636,
        0.966861262,-0.033022636,-0.032676435,-0.032104039,
        -0.031319484,-0.030331066,-0.029158971,-0.027823228,
        -0.02634363,-0.024747905,-0.023059883,-0.021304869,
        -0.019508703,-0.017701121,-0.01589555,-0.014132141,
        -0.012417001,-0.010779043,-0.009237486,-0.007797099,
        -0.006468574,-0.005265026,-0.004192373,-0.003249754,
        -0.005228327};

size_t
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

bool
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

void
GetBestBlockThreadSize(int count, dim3 &threads_return, dim3 &blocks_return)
{
    if ((count / WW_NUM_THREADS_PER_BLOCK) <= 1) {
        threads_return.x = count;
    } else {
        threads_return.x = WW_NUM_THREADS_PER_BLOCK;
        threads_return.y = 1;
        threads_return.z = 1;
        int countRemain = count / WW_NUM_THREADS_PER_BLOCK;
        if ((countRemain / WW_BLOCK_X) <= 1) {
            blocks_return.x = countRemain;
            blocks_return.y = 1;
            blocks_return.z = 1;
        } else {
            blocks_return.x = WW_BLOCK_X;
            countRemain /= WW_BLOCK_X;
            blocks_return.y = countRemain;
            blocks_return.z = 1;
        }
    }
}

void
CrossfeedParam::Term(void) {
    for (int i=0; i<CROSSFEED_COEF_NUM; ++i) {
        delete [] coeffs[i];
        coeffs[i] = NULL;

        CHK_CUDAFREE(spectra[i], fftSize * sizeof(cufftComplex));
    }
}

void
PcmSamplesPerChannel::Term(void)
{
    delete [] inputPcm;
    inputPcm = NULL;

    delete [] outputPcm;
    outputPcm = NULL;

    CHK_CUDAFREE(spectrum, fftSize * sizeof(cufftComplex));
}

const char *
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