#include <Windows.h>
#include <stdio.h>

/* ntdll.dll is included in Windows Driver Kit.
 * You may need to update VS user library path to
 * i386:  $(LibraryPath);C:\WinDDK\7600.16385.1\lib\win7\i386
 * amd64: $(LibraryPath);C:\WinDDK\7600.16385.1\lib\win7\amd64
 * respectively.
 */
#pragma comment(lib, "ntdll")

extern "C" {
extern NTSYSAPI NTSTATUS NTAPI
NtSetTimerResolution(
        IN  ULONG   desiredResolution,
        IN  BOOLEAN setResolution,
        OUT PULONG  currentResolution);

extern NTSYSAPI NTSTATUS NTAPI
NtQueryTimerResolution(
        OUT PULONG minimumResolution,
        OUT PULONG maximumResolution,
        OUT PULONG currentResolution);
};

int main(void)
{
    ULONG minResolution = 0U;
    ULONG maxResolution = 0U;
    ULONG curResolution = 0U;

    NtQueryTimerResolution(&minResolution, &maxResolution, &curResolution);

    printf("NtQueryTimerResolution min=%u max=%u cur=%u\n",
            minResolution, maxResolution, curResolution);

    NtSetTimerResolution(maxResolution, TRUE, &curResolution);

    printf("NtSetTimerResolution %u cur=%u\n",
            maxResolution, curResolution);
}
