#pragma once
// 日本語 UTF-8

#include <Windows.h>
#include <mmsystem.h>

enum WWSchedulerTaskType {
    WWSTTNone,
    WWSTTAudio,
    WWSTTProAudio,
    WWSTTPlayback,

    WWSTTNUM
};

enum WWMMCSSCallType {
    WWMMCSSDisable,
    WWMMCSSEnable,
    WWMMCSSDoNotCall,

    WWMMCSSNUM
};

class WWThreadCharacteristics {
public:
    WWThreadCharacteristics(void) : m_mmcssCallType(WWMMCSSEnable), m_schedulerTaskType(WWSTTAudio), m_mmcssHandle(NULL), m_mmcssTaskIndex(0) { }

    void Set(WWMMCSSCallType ct, WWSchedulerTaskType stt);
    HRESULT Setup(void);
    void Unsetup(void);

private:
    WWMMCSSCallType m_mmcssCallType;
    WWSchedulerTaskType m_schedulerTaskType;
    HANDLE  m_mmcssHandle;
    DWORD   m_mmcssTaskIndex;
};
