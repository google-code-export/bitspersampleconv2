#include "WWDirectComputeIOIF.h"
#include "WWDirectComputeUser.h"
#include "WWUtil.h"
#include <assert.h>

/// 1�X���b�h�O���[�v�ɏ�������X���b�h�̐��BTGSM�����L����B
/// 2�̏搔�B
/// ���̐��l��������������V�F�[�_�[������������K�v���邩���B
/// 1024�ɂ���ƁA�}256�T���v���̌v�Z���ł��Ȃ��B
#define GROUP_THREAD_COUNT 1024

#define PI_D 3.141592653589793238462643
#define PI_F 3.141592653589793238462643f

enum WWGpuPrecisionType {
    WWGpuPrecision_Float,
    WWGpuPrecision_Double,
    WWGpuPrecision_NUM
};

/// �V�F�[�_�[�ɓn���萔�B16�o�C�g�̔{���łȂ��Ƃ����Ȃ��炵���B
struct ConstShaderParams {
    unsigned int c_convOffs;
    unsigned int c_dispatchCount;
    unsigned int c_reserved1;
    unsigned int c_reserved2;
};

static double
moduloD(double left, double right)
{
    if (right < 0) {
        right = -right;
    }

    if (0 < left) {
        while (0 <= left - right) {
            left -= right;
        }
    } else if (left < 0) {
        do{
            left += right;
        } while (left < 0);
    }

    return left;
}

static float
moduloF(float left, float right)
{
    if (right < 0) {
        right = -right;
    }

    if (0 < left) {
        while (0 <= left - right) {
            left -= right;
        }
    } else if (left < 0) {
        do{
            left += right;
        } while (left < 0);
    }

    return left;
}

static HRESULT
JitterAddGpu(
        WWGpuPrecisionType precision,
        int sampleN,
        int convolutionN,
        float *sampleData,
        float *jitterX,
        float *outF)
{
    bool result = true;
    HRESULT             hr    = S_OK;
    WWDirectComputeUser *pDCU = NULL;
    ID3D11ComputeShader *pCS  = NULL;

    ID3D11ShaderResourceView*   pBuf0Srv = NULL;
    ID3D11ShaderResourceView*   pBuf1Srv = NULL;
    ID3D11ShaderResourceView*   pBuf2Srv = NULL;
    ID3D11UnorderedAccessView*  pBufResultUav = NULL;
    ID3D11Buffer * pBufConst = NULL;

    assert(0 < sampleN);
    assert(0 < convolutionN);
    assert(sampleData);
    assert(jitterX);
    assert(outF);

    // �f�[�^����
    const int fromCount = convolutionN + sampleN + convolutionN;
    float *from = new float[fromCount];
    assert(from);
    ZeroMemory(from, sizeof(float)* fromCount);
    for (int i=0; i<sampleN; ++i) {
        from[i+convolutionN] = sampleData[i];
    }

    // HLSL��#define�����B
    char convStartStr[32];
    char convEndStr[32];
    char convCountStr[32];
    char sampleNStr[32];
    char iterateNStr[32];
    char groupThreadCountStr[32];
    sprintf_s(convStartStr, "%d", -convolutionN);
    sprintf_s(convEndStr,   "%d", convolutionN);
    sprintf_s(convCountStr, "%d", convolutionN*2);
    sprintf_s(sampleNStr,   "%d", sampleN);
    sprintf_s(iterateNStr,  "%d", convolutionN*2/GROUP_THREAD_COUNT);
    sprintf_s(groupThreadCountStr, "%d", GROUP_THREAD_COUNT);

    void *sinx = NULL;
    const D3D_SHADER_MACRO *defines = NULL;
    int sinxBufferElemBytes = 0;
    if (precision == WWGpuPrecision_Double) {
        // doubleprec

        const D3D_SHADER_MACRO definesD[] = {
            "CONV_START", convStartStr,
            "CONV_END", convEndStr,
            "CONV_COUNT", convCountStr,
            "SAMPLE_N", sampleNStr,
            "ITERATE_N", iterateNStr,
            "GROUP_THREAD_COUNT", groupThreadCountStr,
            "HIGH_PRECISION", "1",
            NULL, NULL
        };
        defines = definesD;

        double *sinxD = new double[sampleN];
        assert(sinxD);
        for (int i=0; i<sampleN; ++i) {
            sinxD[i] = sin(jitterX[i]);
        }
        sinx = sinxD;

        sinxBufferElemBytes = 8;
    } else {
        // singleprec

        const D3D_SHADER_MACRO definesF[] = {
            "CONV_START", convStartStr,
            "CONV_END", convEndStr,
            "CONV_COUNT", convCountStr,
            "SAMPLE_N", sampleNStr,
            "ITERATE_N", iterateNStr,
            "GROUP_THREAD_COUNT", groupThreadCountStr,
            // "HIGH_PRECISION", "1",
            NULL, NULL
        };
        defines = definesF;

        float *sinxF = new float[sampleN];
        assert(sinxF);
        for (int i=0; i<sampleN; ++i) {
            sinxF[i] = sinf(jitterX[i]);
        }
        sinx = sinxF;

        sinxBufferElemBytes = 4;
    }

    pDCU = new WWDirectComputeUser();
    assert(pDCU);

    HRG(pDCU->Init());

    // HLSL ComputeShader���R���p�C������GPU�ɑ���B
    HRG(pDCU->CreateComputeShader(L"SincConvolution.hlsl", "CSMain", defines, &pCS));
    assert(pCS);

    // ���̓f�[�^��GPU�������[�ɑ���
    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), fromCount, from, "SampleDataBuffer", &pBuf0Srv));
    assert(pBuf0Srv);

    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sinxBufferElemBytes, sampleN, sinx, "SinxBuffer", &pBuf1Srv));
    assert(pBuf1Srv);

    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), sampleN, jitterX, "XBuffer", &pBuf2Srv));
    assert(pBuf1Srv);

    // ���ʏo�͗̈��GPU�ɍ쐬�B
    HRG(pDCU->CreateBufferAndUnorderedAccessView(
        sizeof(float), sampleN, NULL, "OutputBuffer", &pBufResultUav));
    assert(pBufResultUav);

    // �萔�u�����GPU�ɍ쐬�B
    ConstShaderParams shaderParams;
    ZeroMemory(&shaderParams, sizeof shaderParams);
    HRG(pDCU->CreateConstantBuffer(sizeof shaderParams, 1, "ConstShaderParams", &pBufConst));

    // GPU���ComputeShader���s�B
    ID3D11ShaderResourceView* aRViews[] = { pBuf0Srv, pBuf1Srv, pBuf2Srv };
    DWORD t0 = GetTickCount();
#if 1
    // ���������������B���Ń��[�v����悤�ɂ����B
    shaderParams.c_convOffs = 0;
    shaderParams.c_dispatchCount = convolutionN*2/GROUP_THREAD_COUNT;
    HRGR(pDCU->Run(pCS, sizeof aRViews/sizeof aRViews[0], aRViews, pBufResultUav,
        pBufConst, &shaderParams, sizeof shaderParams, sampleN, 1, 1));
#else
    // �x���B������ɐ؂�ւ���ɂ̓V�F�[�_�[������������K�v����B
    for (int i=0; i<convolutionN*2/GROUP_THREAD_COUNT; ++i) {
        shaderParams.c_convOffs = i * GROUP_THREAD_COUNT;
        shaderParams.c_dispatchCount = convolutionN*2/GROUP_THREAD_COUNT;
        HRGR(pDCU->Run(pCS, sizeof aRViews/sizeof aRViews[0], aRViews, pBufResultUav,
            pBufConst, &shaderParams, sizeof shaderParams, sampleN, 1, 1));
    }
#endif

    // �v�Z���ʂ�CPU�������[�Ɏ����Ă���B
    HRG(pDCU->RecvResultToCpuMemory(pBufResultUav, outF, sampleN * sizeof(float)));
end:

    DWORD t1 = GetTickCount();
    printf("RunGpu=%dms ###################################\n", t1-t0);

    if (pDCU) {
        if (hr == DXGI_ERROR_DEVICE_REMOVED) {
            dprintf("DXGI_ERROR_DEVICE_REMOVED reason=%08x\n",
                pDCU->GetDevice()->GetDeviceRemovedReason());
        }

        pDCU->DestroyConstantBuffer(pBufConst);
        pBufConst = NULL;

        pDCU->DestroyDataAndUnorderedAccessView(pBufResultUav);
        pBufResultUav = NULL;

        pDCU->DestroyDataAndShaderResourceView(pBuf2Srv);
        pBuf2Srv = NULL;

        pDCU->DestroyDataAndShaderResourceView(pBuf1Srv);
        pBuf1Srv = NULL;

        pDCU->DestroyDataAndShaderResourceView(pBuf0Srv);
        pBuf0Srv = NULL;

        if (pCS) {
            pDCU->DestroyComputeShader(pCS);
            pCS = NULL;
        }

        pDCU->Term();
    }

    SAFE_DELETE(pDCU);

    delete[] sinx;
    sinx = NULL;

    delete[] from;
    from = NULL;

    return hr;
}

static float
SincF(float sinx, float x)
{
    if (-0.000000001f < x && x < 0.000000001f) {
        return 1.0f;
    } else {
        return sinx / x;
    }
}

static double
SincD(double sinx, double x)
{
    if (-0.000000001 < x && x < 0.000000001) {
        return 1.0;
    } else {
        return sinx / x;
    }
}

static void
JitterAddCpuF(int sampleN, int convolutionN, float *sampleData, float *jitterX, float *outF)
{
    const int fromCount = convolutionN + sampleN + convolutionN;
    float *from = new float[fromCount];
    assert(from);

    ZeroMemory(from, sizeof(float) * fromCount);
    for (int i=0; i<sampleN; ++i) {
        from[i+convolutionN] = sampleData[i];
    }

    for (int pos=0; pos<sampleN; ++pos) {
        float xOffs = jitterX[pos];
        float sinx  = sinf(xOffs);
        float r = 0.0f;

        for (int i=-convolutionN; i<convolutionN; ++i) {
            float x = PI_F * i + xOffs;
            int posS = pos + i + convolutionN;
            float sinc =  SincF(sinx, x);

            r += from[posS] * sinc;
        }

        outF[pos] = r;
    }

    delete[] from;
    from = NULL;
}

static void
JitterAddCpuD(int sampleN, int convolutionN, float *sampleData, float *jitterX, float *outF)
{
    const int fromCount = convolutionN + sampleN + convolutionN;
    float *from = new float[fromCount];
    assert(from);

    ZeroMemory(from, sizeof(float) * fromCount);
    for (int i=0; i<sampleN; ++i) {
        from[i+convolutionN] = sampleData[i];
    }

    for (int pos=0; pos<sampleN; ++pos) {
        float xOffs = jitterX[pos];
        double sinx  = sin((double)xOffs);
        double r = 0.0f;

        for (int i=-convolutionN; i<convolutionN; ++i) {
            double x = PI_D * i + xOffs;
            int posS = pos + i + convolutionN;
            double sinc =  SincD(sinx, x);

            r += from[posS] * sinc;
        }

        outF[pos] = (float)r;
    }

    delete[] from;
    from = NULL;
}

class WWDCIOInfo {
    WWDirectComputeUser dcu;
    float *outputBuffer;
};

/////////////////////////////////////////////////////////////////////////////

extern "C" __declspec(dllexport)
int __stdcall
WWDCIO_Init(void)
{
    return S_OK;
}

extern "C" __declspec(dllexport)
void __stdcall
WWDCIO_Term(void)
{
}

extern "C" __declspec(dllexport)
int __stdcall
WWDCIO_JitterAddGpu(
        int precision,
        int sampleN,
        int convolutionN,
        float *sampleData,
        float *jitterX,
        float *outF)
{
    assert(0 <= precision && precision < WWGpuPrecision_NUM);
    assert(0 < sampleN);
    assert(65536 <= convolutionN);
    assert(sampleData);
    assert(jitterX);
    assert(outF);

    return JitterAddGpu(
        (WWGpuPrecisionType)precision,
        sampleN,
        convolutionN,
        sampleData,
        jitterX,
        outF);
}
