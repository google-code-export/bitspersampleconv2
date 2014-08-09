#ifndef H_CrossfeedF
#define H_CrossfeedF

#include "Util.h"
#include <vector>
#include "WWFlacRW.h"
#include <cufft.h>

bool
ReadCrossfeeedParamsFromFileF(const wchar_t *path, CrossfeedParam *param_return);

void
SetInputPcmSamplesF(uint8_t *buff, int bitsPerSample, PcmSamplesPerChannel *ppc_return);

cufftComplex *
CreateSpectrumF(float *timeDomainData, int numSamples, int fftSize);

float *
CrossfeedMixF(cufftComplex *inPcmSpectra[PCT_NUM], cufftComplex *coeffLo[2],
        cufftComplex *coeffHi[2], int nFFT, int pcmSamples);

void
NormalizeOutputPcmF(std::vector<PcmSamplesPerChannel> & pcmSamples);

bool
WriteFlacFileF(const WWFlacMetadata &meta, const uint8_t *picture,
        std::vector<PcmSamplesPerChannel> &pcmSamples, const wchar_t *path);

float *
FirFilterF(float *firCoeff, size_t firCoeffNum, PcmSamplesPerChannel &input, PcmSamplesPerChannel *pOutput);

#endif /* H_CrossfeedF */
