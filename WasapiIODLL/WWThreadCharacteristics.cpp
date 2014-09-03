// 日本語 UTF-8

#include "WWThreadCharacteristics.h"
#include "WWUtil.h"
#include <avrt.h>
#include <assert.h>

static wchar_t*
WWSchedulerTaskTypeToStr(WWSchedulerTaskType t)
{
    switch (t) {
    case WWSTTNone: return L"None";
    case WWSTTAudio: return L"Audio";
    case WWSTTProAudio: return L"Pro Audio";
    case WWSTTPlayback: return L"Playback";
    default: assert(0); return L"";
    }
}

void
WWThreadCharacteristics::Set(WWSchedulerTaskType t)
{
    assert(0 <= t&& t <= WWSTTPlayback);
    dprintf("D: %s() t=%d\n", __FUNCTION__, (int)t);
    m_schedulerTaskType = t;
}

bool
WWThreadCharacteristics::Setup(void)
{
    // マルチメディアクラススケジューラーサービスのスレッド優先度設定。
    if (WWSTTNone != m_schedulerTaskType) {
        dprintf("D: %s() AvSetMmThreadCharacteristics(%S)\n", __FUNCTION__, WWSchedulerTaskTypeToStr(m_schedulerTaskType));

        m_mmcssHandle = AvSetMmThreadCharacteristics(WWSchedulerTaskTypeToStr(m_schedulerTaskType), &m_mmcssTaskIndex);
        if (NULL == m_mmcssHandle) {
            dprintf("Unable to enable MMCSS on render thread: 0x%08x\n", GetLastError());
            m_mmcssTaskIndex = 0;
        }
    }
    return true;
}

void
WWThreadCharacteristics::Unsetup(void)
{
    if (NULL != m_mmcssHandle) {
        AvRevertMmThreadCharacteristics(m_mmcssHandle);
        m_mmcssHandle = NULL;
        m_mmcssTaskIndex = 0;
    }
}
