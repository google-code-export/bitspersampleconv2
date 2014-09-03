#pragma once
// 日本語 UTF-8

#include <Windows.h>
#include <mmsystem.h>

enum WWSchedulerTaskType {
    WWSTTNone,
    WWSTTAudio,
    WWSTTProAudio,
    WWSTTPlayback,
};

class WWThreadCharacteristics {
public:
    WWThreadCharacteristics(void) : m_schedulerTaskType(WWSTTAudio), m_mmcssHandle(NULL), m_mmcssTaskIndex(0) { }

    void Set(WWSchedulerTaskType t);
    bool Setup(void);
    void Unsetup(void);

private:
    WWSchedulerTaskType m_schedulerTaskType;
    HANDLE  m_mmcssHandle;
    DWORD   m_mmcssTaskIndex;
};
