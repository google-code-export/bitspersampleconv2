#include "WWUpsampleGpu.h"
#include "WWUtil.h"
#include <assert.h>
#include <float.h>

/// 1�X���b�h�O���[�v�ɏ�������X���b�h�̐��BTGSM�����L����B
/// 2�̏搔�B
/// ���̐��l��������������V�F�[�_�[������������K�v����B
#define GROUP_THREAD_COUNT 1024

#define PI_D 3.141592653589793238462643
#define PI_F 3.141592653589793238462643f

/// �V�F�[�_�[�ɓn���萔�B16�o�C�g�̔{���łȂ��Ƃ����Ȃ��炵���B
struct ConstShaderParams {
    unsigned int c_convOffs;
    unsigned int c_dispatchCount;
    unsigned int c_sampleToStartPos;
    unsigned int c_reserved2;
};

void
WWUpsampleGpu::Init(void)
{
    int m_convolutionN = 0;
    float * m_sampleFrom = NULL;
    int m_sampleTotalFrom = 0;
    int m_sampleRateFrom = 0;
    int m_sampleRateTo = 0;
    int m_sampleTotalTo = 0;

    m_pDCU = NULL;
    m_pCS  = NULL;

    m_pBuf0Srv = NULL;
    m_pBuf1Srv = NULL;
    m_pBuf2Srv = NULL;
    m_pBuf3Srv = NULL;
    m_pBufResultUav = NULL;
    m_pBufConst = NULL;
}

void
WWUpsampleGpu::Term(void)
{
    assert(m_pDCU == NULL);
    assert(m_pCS  == NULL);

    assert(m_pBuf0Srv == NULL);
    assert(m_pBuf1Srv == NULL);
    assert(m_pBuf2Srv == NULL);
    assert(m_pBuf3Srv == NULL);
    assert(m_pBufResultUav == NULL);
    assert(m_pBufConst == NULL);
}

static void
PrepareResamplePosArray(
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo,
        int * resamplePosArray,
        double *fractionArrayD)
{
    for (int i=0; i<sampleTotalTo; ++i) {
        double resamplePos = (double)i * sampleRateFrom / sampleRateTo;
        /* -0.5 <= fraction<+0.5�ɂȂ�悤��resamplePos��I�ԁB
         * �Ō�̂ق��Ŕ͈͊O���w���Ȃ��悤�ɂ���B
         */
        int resamplePosI = (int)(resamplePos+0.5);
        if (resamplePosI < 0) {
            resamplePosI = 0;
        }
        if (sampleTotalFrom <= resamplePosI) {
            resamplePosI = sampleTotalFrom -1;
        }
        double fraction = resamplePos - resamplePosI;

        resamplePosArray[i] = resamplePosI;
        fractionArrayD[i]   = fraction;
    }
}

static void
PrepareSinPreComputeArray(
        const double *fractionArray, int sampleTotalTo, float *sinPreComputeArray)
{
    for (int i=0; i<sampleTotalTo; ++i) {
        sinPreComputeArray[i] = (float)sin(-PI_D * fractionArray[i]);
    }
}

HRESULT
WWUpsampleGpu::Setup(
        int convolutionN,
        float * sampleFrom,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo,
        int * resamplePosArray,
        double *fractionArrayD)
{
    HRESULT hr = S_OK;
    float * sinPreComputeArray = NULL;

    assert(0 < convolutionN);
    assert(sampleFrom);
    assert(0 < sampleTotalFrom);
    assert(sampleRateFrom <= sampleRateTo);
    assert(0 < sampleTotalTo);

    m_convolutionN    = convolutionN;
    m_sampleTotalFrom = sampleTotalFrom;
    m_sampleRateFrom  = sampleRateFrom;
    m_sampleRateTo    = sampleRateTo;
    m_sampleTotalTo   = sampleTotalTo;

    // sinPreComputeArray�̐��x�����߂邽��doubleprec��fractionArrayD����v�Z����B
    // �������邱�ƂŘc������Ɨǂ��Ȃ��B
    sinPreComputeArray = new float[sampleTotalTo];
    assert(sinPreComputeArray);
    PrepareSinPreComputeArray(fractionArrayD, sampleTotalTo, sinPreComputeArray);

    // ������singleprec��fractionArrayF�쐬�B
    float *fractionArrayF = new float[sampleTotalTo];
    assert(fractionArrayF);
    for (int i=0; i<sampleTotalTo; ++i) {
        fractionArrayF[i] = (float)fractionArrayD[i];
    }

    /*
    for (int i=0; i<sampleTotalTo; ++i) {
        printf("i=%6d rPos=%6d fraction=%+f\n",
            i, resamplePosArray[i], fractionArray[i]);
    }
    printf("sampleTotal=%d\n", i);
    */

    // HLSL��#define�����B
    char      convStartStr[32];
    sprintf_s(convStartStr, "%d", -convolutionN);
    char      convEndStr[32];
    sprintf_s(convEndStr,   "%d", convolutionN);
    char      convCountStr[32];
    sprintf_s(convCountStr, "%d", convolutionN*2);
    char      sampleTotalFromStr[32];
    sprintf_s(sampleTotalFromStr,   "%d", sampleTotalFrom);
    char      sampleTotalToStr[32];
    sprintf_s(sampleTotalToStr,   "%d", sampleTotalTo);

    char      sampleRateFromStr[32];
    sprintf_s(sampleRateFromStr,   "%d", sampleRateFrom);
    char      sampleRateToStr[32];
    sprintf_s(sampleRateToStr,   "%d", sampleRateTo);
    char      iterateNStr[32];
    sprintf_s(iterateNStr,  "%d", convolutionN*2/GROUP_THREAD_COUNT);
    char      groupThreadCountStr[32];
    sprintf_s(groupThreadCountStr, "%d", GROUP_THREAD_COUNT);

    // doubleprec
    const D3D_SHADER_MACRO defines[] = {
            "CONV_START", convStartStr,
            "CONV_END", convEndStr,
            "CONV_COUNT", convCountStr,
            "SAMPLE_TOTAL_FROM", sampleTotalFromStr,
            "SAMPLE_TOTAL_TO", sampleTotalToStr,

            "SAMPLE_RATE_FROM", sampleRateFromStr,
            "SAMPLE_RATE_TO", sampleRateToStr,
            "ITERATE_N", iterateNStr,
            "GROUP_THREAD_COUNT", groupThreadCountStr,
            NULL, NULL
        };

    m_pDCU = new WWDirectComputeUser();
    assert(m_pDCU);

    HRG(m_pDCU->Init());

    // HLSL ComputeShader���R���p�C������GPU�ɑ���B
    HRG(m_pDCU->CreateComputeShader(L"SincConvolution2.hlsl", "CSMain", defines, &m_pCS));
    assert(m_pCS);

    // ���̓f�[�^��GPU�������[�ɑ���
    HRG(m_pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof sampleFrom[0], sampleTotalFrom, sampleFrom, "SampleFromBuffer", &m_pBuf0Srv));
    assert(m_pBuf0Srv);

    HRG(m_pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof resamplePosArray[0], sampleTotalTo, resamplePosArray, "ResamplePosBuffer", &m_pBuf1Srv));
    assert(m_pBuf1Srv);

    HRG(m_pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof fractionArrayF[0], sampleTotalTo, fractionArrayF, "FractionBuffer", &m_pBuf2Srv));
    assert(m_pBuf2Srv);

    HRG(m_pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof sinPreComputeArray[0], sampleTotalTo, sinPreComputeArray, "SinPreComputeBuffer", &m_pBuf3Srv));
    assert(m_pBuf3Srv);
    
    // ���ʏo�͗̈��GPU�ɍ쐬�B
    HRG(m_pDCU->CreateBufferAndUnorderedAccessView(
        sizeof(float), sampleTotalTo, NULL, "OutputBuffer", &m_pBufResultUav));
    assert(m_pBufResultUav);

    // �萔�u�����GPU�ɍ쐬�B
    HRG(m_pDCU->CreateConstantBuffer(sizeof(ConstShaderParams), 1, "ConstShaderParams", &m_pBufConst));

end:
    SAFE_DELETE(fractionArrayF);
    SAFE_DELETE(sinPreComputeArray);

    return hr;
}

// without resamplePosArray
HRESULT
WWUpsampleGpu::Setup(
        int convolutionN,
        float * sampleFrom,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo)
{
    bool    result = true;
    HRESULT hr     = S_OK;

    assert(0 < convolutionN);
    assert(sampleFrom);
    assert(0 < sampleTotalFrom);
    assert(sampleRateFrom <= sampleRateTo);
    assert(0 < sampleTotalTo);

    int * resamplePosArray = new int[sampleTotalTo];
    assert(resamplePosArray);

    double * fractionArrayD = new double[sampleTotalTo];
    assert(fractionArrayD);

    PrepareResamplePosArray(
          sampleTotalFrom,
          sampleRateFrom,
          sampleRateTo,
          sampleTotalTo,
          resamplePosArray,
          fractionArrayD);

    HRG(Setup(
        convolutionN,
        sampleFrom,
        sampleTotalFrom,
        sampleRateFrom,
        sampleRateTo,
        sampleTotalTo,
        resamplePosArray,
        fractionArrayD));

end:
    SAFE_DELETE(fractionArrayD);
    SAFE_DELETE(resamplePosArray);

    return hr;
}

HRESULT
WWUpsampleGpu::Dispatch(
        int startPos,
        int count)
{
    HRESULT hr = S_OK;
    bool result = true;

    // GPU���ComputeShader���s�B
    ID3D11ShaderResourceView* aRViews[]
        = { m_pBuf0Srv, m_pBuf1Srv, m_pBuf2Srv, m_pBuf3Srv };
    ConstShaderParams shaderParams;
    ZeroMemory(&shaderParams, sizeof shaderParams);
#if 1
    // ���������������B���Ń��[�v����悤�ɂ����B
    shaderParams.c_convOffs = 0;
    shaderParams.c_dispatchCount = m_convolutionN*2/GROUP_THREAD_COUNT;
    shaderParams.c_sampleToStartPos = startPos;
    HRGR(m_pDCU->Run(m_pCS, sizeof aRViews/sizeof aRViews[0], aRViews,
        m_pBufResultUav,
        m_pBufConst, &shaderParams, sizeof shaderParams, count, 1, 1));
#else
    // �x��
    for (int i=0; i<convolutionN*2/GROUP_THREAD_COUNT; ++i) {
        shaderParams.c_convOffs = i * GROUP_THREAD_COUNT;
        shaderParams.c_dispatchCount = convolutionN*2/GROUP_THREAD_COUNT;
        shaderParams.c_sampleToStartPos = startPos;
        HRGR(m_pDCU->Run(m_pCS, sizeof aRViews/sizeof aRViews[0], aRViews,
            m_pBufResultUav,
            m_pBufConst, &shaderParams, sizeof shaderParams, count, 1, 1));
    }
#endif

end:
    if (hr == DXGI_ERROR_DEVICE_REMOVED) {
        dprintf("DXGI_ERROR_DEVICE_REMOVED reason=%08x\n",
            m_pDCU->GetDevice()->GetDeviceRemovedReason());
    }

    return hr;
}

HRESULT
WWUpsampleGpu::GetResultFromGpuMemory(
        float *outputTo,
        int outputToElemNum)
{
    HRESULT hr = S_OK;

    assert(m_pDCU);
    assert(m_pBufResultUav);

    assert(outputTo);
    assert(outputToElemNum <= m_sampleTotalTo);

    // �v�Z���ʂ�CPU�������[�Ɏ����Ă���B
    HRG(m_pDCU->RecvResultToCpuMemory(m_pBufResultUav, outputTo,
        outputToElemNum * sizeof(float)));
end:
    if (hr == DXGI_ERROR_DEVICE_REMOVED) {
        dprintf("DXGI_ERROR_DEVICE_REMOVED reason=%08x\n",
            m_pDCU->GetDevice()->GetDeviceRemovedReason());
    }

    return hr;
}

void
WWUpsampleGpu::Unsetup(void)
{
    if (m_pDCU) {
        m_pDCU->DestroyConstantBuffer(m_pBufConst);
        m_pBufConst = NULL;

        m_pDCU->DestroyDataAndUnorderedAccessView(m_pBufResultUav);
        m_pBufResultUav = NULL;

        m_pDCU->DestroyDataAndShaderResourceView(m_pBuf3Srv);
        m_pBuf3Srv = NULL;

        m_pDCU->DestroyDataAndShaderResourceView(m_pBuf2Srv);
        m_pBuf2Srv = NULL;

        m_pDCU->DestroyDataAndShaderResourceView(m_pBuf1Srv);
        m_pBuf1Srv = NULL;

        m_pDCU->DestroyDataAndShaderResourceView(m_pBuf0Srv);
        m_pBuf0Srv = NULL;

        if (m_pCS) {
            m_pDCU->DestroyComputeShader(m_pCS);
            m_pCS = NULL;
        }

        m_pDCU->Term();
    }

    SAFE_DELETE(m_pDCU);
}
