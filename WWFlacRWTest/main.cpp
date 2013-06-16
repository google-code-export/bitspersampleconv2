#include "WWFlacRW.h"
#include <stdio.h>
#include <stdint.h>

#include <vector>

#define COPY_FRAMES (4096)

static WWFlacMetadata gMeta;
static uint8_t **gPcmByChannel;
static uint8_t *gPictureData;

static bool
ReadTest(const wchar_t *path)
{
    int id = WWFlacRW_DecodeAll(path);

    if (id < 0) {
        printf("failed to read file\n");
        return false;
    }

    
    int rv = WWFlacRW_GetDecodedMetadata(id, &gMeta);
    if (rv < 0) {
        printf("get metadata failed");
        return false;
    }

    if (16 < gMeta.channels) {
        printf("too many channels %d > 16\n", gMeta.channels);
        return false;
    }

    if (0 < gMeta.pictureBytes) {
        gPictureData = new uint8_t[gMeta.pictureBytes];
        if (NULL == gPictureData) {
            printf("memory exhausted!\n");
            return false;
        }
        if (WWFlacRW_GetDecodedPicture(id, gPictureData, gMeta.pictureBytes) < 0) {
            printf("WWFlacRW_GetDecodedPictureData failed\n");
            return false;
        }
    }

    int bytesPerSample = gMeta.bitsPerSample/8;
    int64_t bytesPerChannel = gMeta.totalSamples * bytesPerSample;

    gPcmByChannel = new uint8_t*[gMeta.channels];
    memset(gPcmByChannel, 0, sizeof(uint8_t*)*gMeta.channels);
    for (int ch=0; ch<gMeta.channels; ++ch) {
        gPcmByChannel[ch] = new uint8_t[bytesPerChannel];
        if (!gPcmByChannel[ch]) {
            printf("memory exhausted!\n");
            return false;
        }
    }

    for (int ch=0; ch<gMeta.channels; ++ch) {
        WWFlacRW_GetDecodedPcmBytes(id, ch, 0, &gPcmByChannel[ch][0], bytesPerChannel);
    }
    WWFlacRW_DecodeEnd(id);

    return true;
}

static bool
WriteTest(const wchar_t *path)
{
    int result;

    int id = WWFlacRW_EncodeInit(&gMeta);
    if (id < 0) {
        printf("failed EncodeInit\n");
        return false;
    }

    if (0 < gMeta.pictureBytes) {
        result = WWFlacRW_EncodeSetPicture(id, gPictureData, gMeta.pictureBytes);
        if (result < 0) {
            printf("failed WWFlacRW_EncodeAddPicture\n");
            return false;
        }
    }

    for (int ch=0; ch<gMeta.channels; ++ch) {
        result = WWFlacRW_EncodeAddPcm(id, ch, gPcmByChannel[ch], gMeta.totalSamples * gMeta.bitsPerSample/8);
        if (result < 0) {
            printf("failed WWFlacRW_EncodeAddPcm\n");
            return false;
        }
    }

    result = WWFlacRW_EncodeRun(id, path);
    if (result < 0) {
        printf("failed EncodeRun\n");
        return false;
    }
    
    WWFlacRW_EncodeEnd(id);
    return true;
}

int main(void)
{
    int result = 1;

    if (!ReadTest(L"C:\\audio\\test.flac")) {
        printf("ReadTest failed\n");
        goto end;
    }

    if (!WriteTest(L"C:\\audio\\testW.flac")) {
        printf("WriteTest failed\n");
        goto end;
    }

    result = 0;
end:

    if (gPcmByChannel) {
        for (int ch=0; ch<gMeta.channels; ++ch) {
            delete [] gPcmByChannel[ch];
            gPcmByChannel[ch] = NULL;
        }
        delete [] gPcmByChannel;
        gPcmByChannel = NULL;
    }

    return result;
}
