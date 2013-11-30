#include <stdio.h>
#include <string.h>
#include <stdlib.h> //< atoi()
#include <assert.h>

static void PrintUsage(const char *programName)
{
    printf("Usage\n"
        "%s -generate32 writeFilePath\n"
        "    Generate file that contains 32bit float values\n"
        "%s -convert32 readFilePath numerator denominator writeFilePath\n"
        "    read float values from readFilePath and scale sample value by numerator/deniminator and write to writeFilePath\n",
        programName, programName);
}

static int Generate32(int argc, char *argv[])
{
    FILE *fpw = NULL;
    int rv = 1;
    float *buff = NULL;
    const int floatNum = 16777216;
    const int buffBytes = floatNum * sizeof(float);
    size_t sz = 0;
    int i;
    errno_t ercd;

    if (0 != strcmp(argv[1], "-generate32")) {
        PrintUsage(argv[0]);
        return 1;
    }

    buff = (float *)malloc(floatNum * sizeof(float));
    if (buff == NULL) {
        printf("E: could not allocate memory\n");
        return 1;
    }

    ercd = fopen_s(&fpw, argv[2], "wb");
    if (ercd != 0 || NULL == fpw) {
        printf("E: file open error %d %s\n", ercd, argv[2]);
        return 1;
    }

    for (i=0; i<floatNum; ++i) {
        buff[i] = ((float)(i - 8388608)) / 8388608.0f;
    }

    sz = fwrite(buff, 1, buffBytes, fpw);
    if (sz != buffBytes) {
        printf("E: fwrite() failed\n");
        buff = NULL;
        return 1;
    }

    free(buff);
    buff = NULL;

    fclose(fpw);
    fpw = NULL;

    return rv;
}

static int Convert32(FILE *fpr, float multiplier, FILE *fpw)
{
    int i;
    long fileBytes = 0;
    long buffCount = 0;
    long buffBytes = 0;
    float *buff = NULL;
    size_t sz = 0;

    assert(fpr);
    assert(fpw);

    fseek(fpr, 0, SEEK_END);
    fileBytes = ftell(fpr);
    fseek(fpr, 0, SEEK_SET);
    if (fileBytes <= 0) {
        printf("E: read file size is too small\n");
        return 1;
    }

    buffCount = fileBytes/sizeof(float);
    // buffBytesはファイルサイズを4の倍数に切り捨てた値になる。
    buffBytes = buffCount * sizeof(float);
    buff = (float *)malloc(buffBytes);
    if (buff == NULL) {
        printf("E: could not allocate memory\n");
        return 1;
    }

    sz = fread(buff, 1, buffBytes, fpr);
    if (sz != buffBytes) {
        printf("E: fread() failed\n");
        buff = NULL;
        return 1;
    }

    for (i=0; i<buffCount; ++i) {
        buff[i] *= multiplier;
    }

    sz = fwrite(buff, 1, buffBytes, fpw);
    if (sz != buffBytes) {
        printf("E: fwrite() failed\n");
        buff = NULL;
        return 1;
    }

    free(buff);
    buff = NULL;

    return 0;
}

static int ReadConvertWrite32(int argc, char *argv[])
{
    FILE *fpw = NULL;
    FILE *fpr = NULL;
    errno_t ercd;
    int numerator;
    int denominator;
    float multiplier;
    int rv = 1;

    if (0 != strcmp(argv[1], "-convert32")) {
        PrintUsage(argv[0]);
        return 1;
    }

    numerator   = atoi(argv[3]);
    denominator = atoi(argv[4]);
    if (numerator == 0 || denominator == 0) {
        printf("E: numerator and denominator must not be zero\n");
        return 1;
    }

    multiplier = (float)numerator / denominator;

    ercd = fopen_s(&fpr, argv[2], "rb");
    if (ercd != 0 || NULL == fpr) {
        printf("E: file open error %d %s\n", ercd, argv[2]);
        return 1;
    }

    ercd = fopen_s(&fpw, argv[5], "wb");
    if (ercd != 0 || NULL == fpw) {
        printf("E: file open error %d %s\n", ercd, argv[5]);

        fclose(fpr);
        fpr = NULL;
        return 1;
    }

    rv = Convert32(fpr, multiplier, fpw);

    fclose(fpw);
    fpw = NULL;
    fclose(fpr);
    fpr = NULL;

    return rv;
}

int main(int argc, char *argv[])
{
    int rv = 1;
    switch (argc) {
    case 3:
        return Generate32(argc, argv);
        break;
    case 6:
        return ReadConvertWrite32(argc, argv);
    default:
        PrintUsage(argv[0]);
        break;
    }
    return rv;
}

