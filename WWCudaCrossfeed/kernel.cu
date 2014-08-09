// 日本語

#include "Util.h"
#include "CrossfeedF.h"
#include <assert.h>

int wmain(int argc, wchar_t *argv[])
{
    if (argc != 4) {
        printf("Usage: %S coeffFile inputFile outputFile\n", argv[0]);
        return 1;
    }

    const wchar_t *coeffPath = argv[1];
    const wchar_t *fromPath = argv[2];
    const wchar_t *toPath = argv[3];

    return WWRunCrossfeedF(coeffPath, fromPath, toPath);
}