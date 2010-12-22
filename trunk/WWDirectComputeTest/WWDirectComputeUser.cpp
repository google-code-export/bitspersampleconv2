// ���{�� SJIS
// �Q�l�FBasicCompute11.cpp

#include "WWDirectComputeUser.h"
#include "WWUtil.h"
#include <d3dcompiler.h>
#include <assert.h>
#include <d3dx11.h>

WWDirectComputeUser::WWDirectComputeUser(void)
{
    m_pDevice = NULL;
    m_pContext = NULL;

    m_pBufIn = NULL;
    m_pBufOut = NULL;

    m_pBufInSRV = NULL;
    m_pBufOutUAV = NULL;
}

WWDirectComputeUser::~WWDirectComputeUser(void)
{
    assert(NULL == m_pDevice);
    assert(NULL == m_pContext);

    assert(NULL == m_pBufIn);
    assert(NULL == m_pBufOut);

    assert(NULL == m_pBufInSRV);
    assert(NULL == m_pBufOutUAV);
}

HRESULT
WWDirectComputeUser::Init(void)
{
    HRESULT hr = S_OK;

    HRG(CreateComputeDevice());

end:
    return hr;
}

void
WWDirectComputeUser::Term(void)
{
    SafeRelease( &m_pBufOutUAV );
    SafeRelease( &m_pBufInSRV );

    SafeRelease( &m_pBufOut );
    SafeRelease( &m_pBufIn );

    SafeRelease( &m_pContext );
    SafeRelease( &m_pDevice );
}

static HRESULT
CreateDeviceInternal(
        IDXGIAdapter* pAdapter,
        D3D_DRIVER_TYPE DriverType,
        HMODULE Software,
        UINT32 Flags,
        CONST D3D_FEATURE_LEVEL* pFeatureLevels,
        UINT FeatureLevels,
        UINT32 SDKVersion,
        ID3D11Device** ppDevice,
        D3D_FEATURE_LEVEL* pFeatureLevel,
        ID3D11DeviceContext** ppImmediateContext )
{
    HRESULT hr;

    *ppDevice           = NULL;
    *ppImmediateContext = NULL;

    // ������f�o�C�X���쐬�����ꍇ��D3D11������܂���Ƃ����G���[���P�񂾂��o���悤�ɂ���t���O�B
    static bool bMessageAlreadyShown = false;

    HMODULE hModD3D11 = LoadLibrary( L"d3d11.dll" );
    if ( hModD3D11 == NULL ) {
        // D3D11���Ȃ��B

        if ( !bMessageAlreadyShown ) {
            OSVERSIONINFOEX osv;
            memset( &osv, 0, sizeof(osv) );
            osv.dwOSVersionInfoSize = sizeof(osv);
            GetVersionEx( (LPOSVERSIONINFO)&osv );

            if ( ( osv.dwMajorVersion > 6 )
                || ( osv.dwMajorVersion == 6 && osv.dwMinorVersion >= 1 ) 
                || ( osv.dwMajorVersion == 6 && osv.dwMinorVersion == 0 && osv.dwBuildNumber > 6002 ) ) {
                MessageBox(0,
                    L"�G���[: Direct3D 11 �R���|�[�l���g��������܂���ł����B",
                    L"Error",
                    MB_ICONEXCLAMATION );
                // This should not happen, but is here for completeness as the system could be
                // corrupted or some future OS version could pull D3D11.DLL for some reason
            } else if ( osv.dwMajorVersion == 6 && osv.dwMinorVersion == 0 && osv.dwBuildNumber == 6002 ) {
                MessageBox(0,
                    L"�G���[: Direct3D 11 �R���|�[�l���g��������܂���ł������A"
                    L"����Windows�p��Direct3D 11 �R���|�[�l���g�͓���\�ł��B\n"
                    L"�}�C�N���\�t�gKB #971644���������������B\n"
                    L" http://support.microsoft.com/default.aspx/kb/971644/",
                    L"Error", MB_ICONEXCLAMATION );
            } else if ( osv.dwMajorVersion == 6 && osv.dwMinorVersion == 0 ) {
                MessageBox(0,
                    L"�G���[: Direct3D 11 �R���|�[�l���g��������܂���ł����B"
                    L"�ŐV�̃T�[�r�X�p�b�N��K�p���Ă��������B\n"
                    L"�ڂ����̓}�C�N���\�t�gKB #935791���������������B\n"
                    L" http://support.microsoft.com/default.aspx/kb/935791",
                    L"Error", MB_ICONEXCLAMATION );
            } else {
                MessageBox(0,
                    L"�G���[: ���̃o�[�W������Windows������Direct3D 11 �͂���܂���B",
                    L"Error", MB_ICONEXCLAMATION);
            }

            bMessageAlreadyShown = true;
        }

        hr = E_FAIL;
        goto end;
    }

    // D3D11�f�o�C�X�����݂���ꍇ�B

    typedef HRESULT (WINAPI * LPD3D11CREATEDEVICE)(
        IDXGIAdapter*, D3D_DRIVER_TYPE, HMODULE, UINT32,
        CONST D3D_FEATURE_LEVEL*, UINT, UINT32, ID3D11Device**,
        D3D_FEATURE_LEVEL*, ID3D11DeviceContext** );

    LPD3D11CREATEDEVICE pDynamicD3D11CreateDevice =
        (LPD3D11CREATEDEVICE)GetProcAddress( hModD3D11, "D3D11CreateDevice" );

    HRG(pDynamicD3D11CreateDevice(
        pAdapter, DriverType, Software, Flags, pFeatureLevels, FeatureLevels,
        SDKVersion, ppDevice, pFeatureLevel, ppImmediateContext));

    assert(*ppDevice);
    assert(*ppImmediateContext);

    // A hardware accelerated device has been created, so check for Compute Shader support.
    // If we have a device >= D3D_FEATURE_LEVEL_11_0 created,
    // full CS5.0 support is guaranteed, no need for further checks.

    // Double-precision support is an optional feature of CS 5.0.
    D3D11_FEATURE_DATA_DOUBLES hwopts;
    (*ppDevice)->CheckFeatureSupport( D3D11_FEATURE_DOUBLES, &hwopts, sizeof(hwopts) );
    if ( !hwopts.DoublePrecisionFloatShaderOps ) {
        if ( !bMessageAlreadyShown ) {
            MessageBox(0,
                L"�G���[: ����GPU��ComputeShader5.0�̔{���x���������_���I�v�V����"
                L"(double-precision support)���g�p�ł��܂���B",
                L"Error", MB_ICONEXCLAMATION);
            bMessageAlreadyShown = true;
        }
        hr = E_FAIL;
        goto end;
    }

end:
    return hr;
}

HRESULT
WWDirectComputeUser::CreateComputeDevice(void)
{
    HRESULT hr = S_OK;

    assert(NULL == m_pDevice);
    assert(NULL == m_pContext);
    
    UINT uCreationFlags = D3D11_CREATE_DEVICE_SINGLETHREADED;
#if defined(DEBUG) || defined(_DEBUG)
    uCreationFlags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

    D3D_FEATURE_LEVEL flOut;
    static const D3D_FEATURE_LEVEL flvl[] = { D3D_FEATURE_LEVEL_11_0 };
    
    HRG(CreateDeviceInternal(
        NULL,                        // Use default graphics card
        D3D_DRIVER_TYPE_HARDWARE,    // Try to create a hardware accelerated device
        NULL,                        // Do not use external software rasterizer module
        uCreationFlags,              // Device creation flags
        flvl,
        sizeof(flvl) / sizeof(D3D_FEATURE_LEVEL),
        D3D11_SDK_VERSION,           // SDK version
        &m_pDevice,                  // Device out
        &flOut,                      // Actual feature level created
        &m_pContext));

    assert(flOut == D3D_FEATURE_LEVEL_11_0);
end:
    return hr;
}

HRESULT
WWDirectComputeUser::CreateComputeShader(
        LPCWSTR path, LPCSTR entryPoint, ID3D11ComputeShader **ppCS)
{
    HRESULT hr;
    ID3DBlob * pErrorBlob = NULL;
    ID3DBlob * pBlob      = NULL;

    assert(m_pDevice);

    DWORD dwShaderFlags = D3DCOMPILE_ENABLE_STRICTNESS;
#if defined( DEBUG ) || defined( _DEBUG )
    // D3DCOMPILE_DEBUG�t���O���w�肷��ƁA�V�F�[�_�[�Ƀf�o�b�O���𖄂ߍ��ނ�
    // �œK���͂���邵�ARELEASE�Ɠ����̓�������A���\�������Ȃ��c�炵���B
    dwShaderFlags |= D3DCOMPILE_DEBUG;
#endif

    // ��������������HLSL���Ŏg�p����萔��#define���ł���
    const D3D_SHADER_MACRO defines[] = {
        "USE_STRUCTURED_BUFFERS", "1",
        "TEST_DOUBLE", "1",
        NULL, NULL
    };

    // CS�V�F�[�_�[�v���t�@�C��5.0���w��B
    LPCSTR pProfile = "cs_5_0";

    hr = D3DX11CompileFromFile(path, defines, NULL, entryPoint, pProfile,
        dwShaderFlags, NULL, NULL, &pBlob, &pErrorBlob, NULL );
    if (FAILED(hr)) {
        WCHAR erStr[256];
        ZeroMemory(erStr, sizeof erStr);

        if (pErrorBlob) {
            const char *s = (const char *)pErrorBlob->GetBufferPointer();
            MultiByteToWideChar(CP_ACP, 0, s, -1,
                erStr, sizeof erStr/sizeof erStr[0]-1);
        }
        MessageBox(0, erStr, L"D3DX11CompileFromFile���s", MB_ICONEXCLAMATION);
        goto end;
    }

    assert(pBlob);

    hr = m_pDevice->CreateComputeShader( pBlob->GetBufferPointer(), pBlob->GetBufferSize(), NULL, ppCS);

#if defined(DEBUG) || defined(PROFILE)
    if (*ppCS) {
        (*ppCS)->SetPrivateData( WKPDID_D3DDebugObjectName, lstrlenA(pFunctionName), pFunctionName );
    }
#endif

end:
    SafeRelease(&pErrorBlob);
    SafeRelease(&pBlob);
    return hr;
}

void
WWDirectComputeUser::DestroyComputeShader(ID3D11ComputeShader *pCS)
{
    SAFE_RELEASE(pCS);
}

HRESULT
WWDirectComputeUser::CreateStructuredBuffer(
        unsigned int uElementSize,
        unsigned int uCount,
        void * pInitData,
        ID3D11Buffer ** ppBufOut)
{
    HRESULT hr = S_OK;

    assert(m_pDevice);
    assert(uElementSize);
    assert(0 < uCount);

    *ppBufOut = NULL;

    D3D11_BUFFER_DESC desc;
    ZeroMemory(&desc, sizeof desc);
    desc.BindFlags = D3D11_BIND_UNORDERED_ACCESS | D3D11_BIND_SHADER_RESOURCE;
    desc.ByteWidth = uElementSize * uCount;
    desc.MiscFlags = D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;
    desc.StructureByteStride = uElementSize;

    if (pInitData) {
        D3D11_SUBRESOURCE_DATA initData;
        ZeroMemory(&initData, sizeof initData);
        initData.pSysMem = pInitData;

        hr = m_pDevice->CreateBuffer(&desc, &initData, ppBufOut);
    } else {
        hr = m_pDevice->CreateBuffer(&desc, NULL, ppBufOut);
    }

    return hr;
}

void
WWDirectComputeUser::DestroyStructuredBuffer(ID3D11Buffer * pBuf)
{
    SAFE_RELEASE(pBuf);
}

