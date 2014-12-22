#include <Windows.h>
#include <stdio.h>
#include <tchar.h>

static bool
LaunchAsAdministratorTest(void)
{
    SHELLEXECUTEINFO shExInfo = {0};
    shExInfo.cbSize = sizeof shExInfo;
    shExInfo.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_UNICODE;
    shExInfo.hwnd = 0;
    shExInfo.lpVerb = _T("runas");
    shExInfo.lpFile = _T("C:/work/BpsConvWin2/PlayPcm/x64/Debug/PlayPcm.exe");
    shExInfo.lpParameters = _T("-d 7 -uselargememory C:/audio/test.wav");
    shExInfo.lpDirectory = 0;
    shExInfo.nShow = SW_SHOWNORMAL ;
    shExInfo.hInstApp = 0;

    if (ShellExecuteEx(&shExInfo)) {
        WaitForSingleObject(shExInfo.hProcess, INFINITE);
        CloseHandle(shExInfo.hProcess);
        return true;
    } else {
        return false;
    }
}

int main(void)
{
    CoInitializeEx(NULL, COINIT_MULTITHREADED);
    bool result = LaunchAsAdministratorTest();
    printf("LaunchAsAdministratorTest result=%d\n", (int)result);

    return 0;
}
