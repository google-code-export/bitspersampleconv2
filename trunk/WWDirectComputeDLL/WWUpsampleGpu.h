#pragma once

#include "WWDirectComputeUser.h"

class WWUpsampleGpu {
public:
    void Init(void);
    void Term(void);

    HRESULT Setup(
        int convolutionN,
        float * sampleFrom,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo);

    HRESULT Dispatch(
        int startPos,
        int count);

    HRESULT GetResultFromGpuMemory(
        float * outputTo,
        int outputToElemNum);

    void Unsetup(void);

    // limit level to fit to the audio sampledata range
    static float LimitSampleData(
        float * sampleData,
        int sampleDataCount);

    HRESULT UpsampleCpuSetup(
        int convolutionN,
        float * sampleFrom,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo);

    // output[0]�`output[count-1]�ɏ�������
    HRESULT UpsampleCpuDo(
        int startPos,
        int count,
        float *output);

    void UpsampleCpuUnsetup(void);

private:
    int m_convolutionN;
    int m_sampleTotalFrom;
    int m_sampleRateFrom;
    int m_sampleRateTo;
    int m_sampleTotalTo;

    //CPU�����p
    float        * m_sampleFrom;
    unsigned int * m_resamplePosArray;
    float        * m_fractionArray;
    double       * m_sinPreComputeArray;

    WWDirectComputeUser *m_pDCU;
    ID3D11ComputeShader *m_pCS;

    ID3D11ShaderResourceView*   m_pBuf0Srv;
    ID3D11ShaderResourceView*   m_pBuf1Srv;
    ID3D11ShaderResourceView*   m_pBuf2Srv;
    ID3D11ShaderResourceView*   m_pBuf3Srv;
    ID3D11UnorderedAccessView*  m_pBufResultUav;
    ID3D11Buffer * m_pBufConst;
    
};

