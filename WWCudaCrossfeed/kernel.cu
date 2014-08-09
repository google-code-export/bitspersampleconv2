// 日本語

#include "Util.h"
#include "CrossfeedF.h"

#include <stdio.h>
#include <string.h> //< memset()
#include <math.h>
#include <assert.h>

#include <vector>
#include <float.h>

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

    std::vector<PcmSamplesPerChannel> pcmSamples;

    if (argc != 4) {
        printf("Usage: %S coeffFile inputFile outputFile\n", argv[0]);
        goto END;
    }

    if (!ReadCrossfeeedParamsFromFileF(argv[1], &crossfeedParam)) {
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
        SetInputPcmSamplesF(buff, meta.bitsPerSample, &ppc);

        delete [] buff;
        buff = NULL;

        {
            // 低音域
            PcmSamplesPerChannel lowFreq;
            lowFreq.Init();
            lowFreq.totalSamples = ppc.totalSamples;
            lowFreq.inputPcm = new float[ppc.totalSamples];
            if (NULL == FirFilterF(gLpf, sizeof gLpf/sizeof gLpf[0], ppc, &lowFreq)) {
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
            if (NULL == FirFilterF(gHpf, sizeof gHpf/sizeof gHpf[0], ppc, &highFreq)) {
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
        crossfeedParam.spectra[i] = CreateSpectrumF(crossfeedParam.coeffs[i], crossfeedParam.coeffSize, nFFT);
        if (crossfeedParam.spectra[i] == NULL) {
            goto END;
        }
        crossfeedParam.fftSize = nFFT;
    }
    for (int i=0; i<pcmSamples.size(); ++i) {
        pcmSamples[i].spectrum = CreateSpectrumF(pcmSamples[i].inputPcm, pcmSamples[i].totalSamples, nFFT);
        if (pcmSamples[i].spectrum == NULL) {
            goto END;
        }
        pcmSamples[i].fftSize = nFFT;
        inPcmSpectra[i] = pcmSamples[i].spectrum;
    }

    pcmSamples[0].outputPcm = CrossfeedMixF(inPcmSpectra,
            &crossfeedParam.spectra[0], &crossfeedParam.spectra[4], nFFT, pcmSamples[0].totalSamples);
    if (pcmSamples[0].outputPcm == NULL) {
        goto END;
    }
    pcmSamples[1].outputPcm = CrossfeedMixF(inPcmSpectra,
            &crossfeedParam.spectra[2], &crossfeedParam.spectra[6], nFFT, pcmSamples[0].totalSamples);
    if (pcmSamples[1].outputPcm == NULL) {
        goto END;
    }

    NormalizeOutputPcmF(pcmSamples);

    // 出力bit depth == 24bit
    meta.bitsPerSample = 24;
    if (!WriteFlacFileF(meta, picture, pcmSamples, argv[3])) {
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
        printf("    maximum used CUDA memory: %lld Mbytes\n", gCudaMaxBytes / 1024/ 1024);
        printf("Succeeded to write %S.\n", argv[3]);
        assert(gCudaAllocatedBytes == 0);
    }

    return result;
}