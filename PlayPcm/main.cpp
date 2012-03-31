#include "WasapiWrap.h"
#include "WWUtil.h"
#include "WWPcmData.h"

#include <stdio.h>
#include <Windows.h>
#include <stdlib.h>
#include <crtdbg.h>

#define LATENCY_MILLISEC_DEFAULT (100)

static HRESULT
GetIntValueFromConsole(const char *prompt, int from, int to, int *value_return)
{
    *value_return = 0;

    printf("%s (%d to %d) ? ", prompt, from, to);
    fflush(stdout);

    char s[32];
    char *result = fgets(s, 31, stdin);
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

    return NOERROR;
}

static HRESULT
Run(int deviceId, int latencyMillisec, WWPcmData &pcm)
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

    ww.ChooseDevice(deviceId);
    if (deviceId < 0) {
        goto end;
    }

    WWSetupArg setupArg;
    setupArg.bitsPerSample     = pcm.bitsPerSample;
    setupArg.latencyInMillisec = latencyMillisec;
    setupArg.nChannels         = pcm.nChannels;
    setupArg.nSamplesPerSec    = pcm.nSamplesPerSec;
    HRG(ww.Setup(setupArg));
    ww.SetOutputData(pcm);
    ww.Start();

    while (!ww.Run(1000)) {
        printf("%d / %d\n", ww.GetPosFrame(), ww.GetTotalFrameNum());
    }

end:
    ww.Stop();
    ww.Unsetup();
    ww.Term();
    return NOERROR;
}

static void
PrintUsage(void)
{
    printf(
        "Usage:\n"
        "    PlayPcm\n"
        "        Enumerate all available devices\n"
        "    PlayPcm -d deviceId [-l latencyInMillisec] input_wave_file_name\n"
        "        Play wav file on deviceId device\n"
        "        Example:\n"
        "            PlayPcm -d 1 C:\\audio\\music.wav\n\n"
        );
}

int
main(int argc, char *argv[])
{
    WWPcmData *pcmData = NULL;
    int deviceId = -1;
    int latencyInMillisec = LATENCY_MILLISEC_DEFAULT;

    if (argc == 4 || argc == 6) {
        char *filePath = 0;

        if (0 != strcmp("-d", argv[1])) {
            PrintUsage();
            return 1;
        }
        deviceId = atoi(argv[2]);

        if (argc == 6) {
            if (0 != strcmp("-l", argv[3])) {
                PrintUsage();
                return 1;
            }
            latencyInMillisec = atoi(argv[4]);
        }

        filePath = argv[argc-1];
        pcmData = WWPcmDataWavFileLoad(filePath);
        if (NULL == pcmData) {
            printf("E: WWPcmDataWavFileLoad failed %s\n", argv[3]);
            return 1;
        }
    } else {
        PrintUsage();
        // continue and display device list
    }

    HRESULT hr = Run(deviceId, latencyInMillisec, *pcmData);
    if (FAILED(hr)) {
        printf("Run failed (%08x)\n", hr);
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

