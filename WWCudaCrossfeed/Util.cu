#include "Util.h"
#include <stdio.h>

int64_t gCudaAllocatedBytes = 0;
int64_t gCudaMaxBytes = 0;

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

