#include "WasapiWrap.h"
#include "WWUtil.h"
#include "WWWavReader.h"
#include "WWDsfReader.h"

#include <stdio.h>
#include <Windows.h>
#include <stdlib.h>
#include <crtdbg.h>

#define LATENCY_MILLISEC_DEFAULT (100)
#define READ_LINE_BYTES          (256)

static HRESULT
GetIntValueFromConsole(const char *prompt, int from, int to, int *value_return)
{
    *value_return = 0;

    printf("%s (%d to %d) ? ", prompt, from, to);
    fflush(stdout);

    char s[READ_LINE_BYTES];
    char *result = fgets(s, READ_LINE_BYTES-1, stdin);
    if (NULL == result) {
        return E_INVALIDARG;
    }

    char *p = NULL;
    errno = 0;
    int v = (int)strtol(s, &p, 10);
    if (errno != 0 || p == s) {
        printf("E: malformed input...\n");
        return E_INVALIDARG;
    }
    if (v < from || to < v) {
        printf("E: value is out of range.\n");
        return E_INVALIDARG;
    }

    *value_return = v;

    return S_OK;
}

static HRESULT
PrintDeviceList(void)
{
    HRESULT hr;
    WasapiWrap ww;

    HRR(ww.Init());
    HRG(ww.DoDeviceEnumeration());

    printf("Device list:\n");
    for (int i=0; i<ww.GetDeviceCount(); ++i) {
        wchar_t namew[WW_DEVICE_NAME_COUNT];
        ww.GetDeviceName(i, namew, sizeof namew);

        char    namec[WW_DEVICE_NAME_COUNT];
        memset(namec, 0, sizeof namec);
        WideCharToMultiByte(CP_ACP, 0, namew, -1, namec, sizeof namec-1, NULL, NULL);
        printf("    deviceId=%d: %s\n", i, namec);
    }
    printf("\n");

end:
    ww.Term();
    return hr;
}

static HRESULT
Run(int deviceId, int latencyMillisec, WWPcmData &pcm)
{
    HRESULT hr;
    WasapiWrap ww;

    HRR(ww.Init());
    HRG(ww.DoDeviceEnumeration());
    HRG(ww.ChooseDevice(deviceId));

    WWSetupArg setupArg;
    setupArg.Set(pcm.bitsPerSample,pcm.validBitsPerSample, pcm.nSamplesPerSec, pcm.nChannels, latencyMillisec);
    HRG(ww.Setup(setupArg));
    ww.SetOutputData(pcm);
    ww.Start();

    while (!ww.Run(1000)) {
        printf("%d / %d\n", ww.GetPosFrame(), ww.GetTotalFrameNum());
    }
    hr = S_OK;

end:
    ww.Stop();
    ww.Unsetup();
    ww.Term();
    return hr;
}

static HRESULT
Test(int deviceId)
{
    HRESULT hr;
    WasapiWrap ww;

    HRR(ww.Init());
    HRG(ww.DoDeviceEnumeration());
    HRG(ww.ChooseDevice(deviceId));

    ww.PrintMixFormat();
    WWInspectArg inspectArg;

    inspectArg.Set(16, 16, 44100, 2);
    ww.Inspect(inspectArg);

    inspectArg.Set(16, 16, 48000, 2);
    ww.Inspect(inspectArg);

    inspectArg.Set(24, 24, 176400, 2);
    ww.Inspect(inspectArg);

    inspectArg.Set(32, 24, 176400, 2);
    ww.Inspect(inspectArg);

end:
    ww.Unsetup();
    ww.Term();
    return hr;
}


static void
PrintUsage(void)
{
    printf(
        "PlayPcm device inspect version 1.1\n"
        "Usage:\n"
        "    PlayPcm\n"
        "        Print this message and enumerate all available devices\n"
        "\n"
        "    PlayPcm -d deviceId\n"
        "        Test specified device\n"
        "\n"
        "    PlayPcm -d deviceId [-l latencyInMillisec] input_pcm_file_name\n"
        "        Play pcm file on deviceId device\n"
        "        Example:\n"
        "            PlayPcm -d 1 C:\\audio\\music.wav\n\n"
        "            PlayPcm -d 1 C:\\audio\\music.dsf\n\n"
        );
}

int
main(int argc, char *argv[])
{
    WWPcmData *pcmData = NULL;
    int deviceId = -1;
    int latencyInMillisec = LATENCY_MILLISEC_DEFAULT;
    char *filePath = 0;

    if (argc != 3 && argc != 4 && argc != 6) {
        PrintUsage();
        PrintDeviceList();
        return 0;
    }

    if (0 != strcmp("-d", argv[1])) {
        PrintUsage();
        return 1;
    }
    deviceId = atoi(argv[2]);

    if (argc == 3) {
        Test(deviceId);
        return 0;
    }

    if (argc == 6) {
        if (0 != strcmp("-l", argv[3])) {
            PrintUsage();
            return 1;
        }
        latencyInMillisec = atoi(argv[4]);
    }

    filePath = argv[argc-1];
    pcmData = WWReadWavFile(filePath);
    if (NULL == pcmData) {
        pcmData = WWReadDsfFile(filePath);
        if (NULL == pcmData) {
            printf("E: read file failed %s\n", argv[3]);
            return 1;
        }
    }

    HRESULT hr = Run(deviceId, latencyInMillisec, *pcmData);
    if (FAILED(hr)) {
        printf("E: Run failed (%08x)\n", hr);
    }

    if (NULL != pcmData) {
        pcmData->Term();
        delete pcmData;
        pcmData = NULL;
    }

#ifdef _DEBUG
    _CrtDumpMemoryLeaks();
#endif
    return 0;
}

