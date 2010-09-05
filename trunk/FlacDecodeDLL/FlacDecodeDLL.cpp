// ���{��UTF-8

#include "stdafx.h"
#include <stdio.h>
#include <stdlib.h>
#include "FLAC/stream_decoder.h"
#include "FlacDecodeDLL.h"
#include <assert.h>

// x86 CPU�ɂ����Ή����ĂȂ��B
// x64��r�b�O�G���f�B�A���ɂ͑Ή����ĂȂ��B

#define FLACDECODE_MAXPATH (1024)

#ifdef _DEBUG
static FILE *g_fp = NULL;
static void
LogOpen(void)
{
    g_fp = fopen("log.txt", "wb");
}
static void
LogClose(void)
{
    fclose(g_fp);
    g_fp = NULL;
}
#else
#define LogOpen(x)
#define LogClose(x)
#endif

#ifdef _DEBUG
#  define dprintf1(x, ...) fprintf(g_fp, x, __VA_ARGS__); fflush(g_fp);
#  define dprintf(x, ...)
//#  define dprintf(x, ...) printf(x, __VA_ARGS__)
#else
#  define dprintf(x, ...)
#endif

#define CHK(x)                           \
{   if (!x) {                            \
        dprintf("E: %s:%d %s is NULL\n", \
            __FILE__, __LINE__, #x);     \
        return E_FAIL;                   \
    }                                    \
}

/// FlacDecode�X���b�h�ւ̃R�}���h�B
enum FlacDecodeCommand {
    /// �R�}���h�Ȃ��B(�R�}���h���s���FlacDecode���Z�b�g����)
    FDC_None,

    /// �V���b�g�_�E���C�x���g�B
    FDC_Shutdown,

    /// �t���[��(�T���v���f�[�^)�擾�B
    /// �擾����t���[����
    FDC_GetFrames,
};

/// FlacDecode�̕��u�B
struct FlacDecodeInfo {
    FLAC__uint64 totalSamples;
    int          sampleRate;
    int          channels;
    int          bitsPerSample;

    /// 1�̃u���b�N�ɉ��T���v���f�[�^�������Ă��邩�B
    int          blockSize;

    HANDLE       thread;

    FlacDecodeResultType errorCode;

    FlacDecodeCommand command;
    HANDLE            commandEvent;
    HANDLE            commandCompleteEvent;
    /// �R�}���h�𓊓����镔�����͂ރ~���[�e�b�N�X�B
    HANDLE            commandMutex;

    char              *buff;
    int               numFrames;
    int               retrievedFrames;

    char         fromFlacPath[FLACDECODE_MAXPATH];

    void Clear(void) {
        totalSamples  = 0;
        sampleRate    = 0;
        channels      = 0;
        bitsPerSample = 0;
        blockSize     = 0;

        thread        = NULL;

        errorCode     = FDRT_DataNotReady;

        command              = FDC_None;
        commandEvent         = NULL;
        commandCompleteEvent = NULL;
        commandMutex         = NULL;

        buff            = NULL;
        numFrames       = 0;
        retrievedFrames = 0;

        fromFlacPath[0] = 0;
    }

    FlacDecodeInfo(void) {
        Clear();
    }
};

#define RG(x,v)                                   \
{                                                 \
    rv = x;                                       \
    if (v != rv) {                                \
        goto end;                                 \
    }                                             \
}                                                 \

////////////////////////////////////////////////////////////////////////
// FLAC�f�R�[�_�[�R�[���o�b�N

static FLAC__StreamDecoderWriteStatus
WriteCallback1(const FLAC__StreamDecoder *decoder,
    const FLAC__Frame *frame, const FLAC__int32 * const buffer[],
    void *clientData)
{
    FlacDecodeInfo *args = (FlacDecodeInfo*)clientData;
    size_t i;

    dprintf("%s args->totalSamples=%lld errorCode=%d\n", __FUNCTION__,
        args->totalSamples, args->errorCode);

    if(args->totalSamples == 0) {
        return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
    }
    if(args->channels != 2
        || (args->bitsPerSample != 16
         && args->bitsPerSample != 24)) {
        return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
    }

    if(frame->header.number.sample_number == 0) {
        args->blockSize = frame->header.blocksize;

        // �ŏ��̃f�[�^�������B�����ł�������҂���ԂɂȂ�B
        dprintf("%s first data come. blockSize=%d. set commandCompleteEvent\n",
            __FUNCTION__, args->blockSize);
        SetEvent(args->commandCompleteEvent);
        WaitForSingleObject(args->commandEvent, INFINITE);

        // �N�����B�v�����`�F�b�N����B
        dprintf("%s event received. %d\n", __FUNCTION__, args->command);
        if (args->command == FDC_Shutdown) {
            return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
        }
    }

    if (args->errorCode != FDRT_Success) {
        // �f�R�[�h�G���[���N�����B�����ł�������҂���ԂɂȂ�B
        dprintf("%s decode error %d. set commandCompleteEvent\n",
            __FUNCTION__, args->errorCode);
        SetEvent(args->commandCompleteEvent);
        WaitForSingleObject(args->commandEvent, INFINITE);

        // �N�����B�v�����`�F�b�N����B�ǂ���ɂ��Ă����s�͂ł��Ȃ��B
        dprintf("%s event received. %d\n", __FUNCTION__, args->command);
        return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
    }

    // �f�[�^�������B�u���b�N���� frame->header.blocksize
    if (args->blockSize != frame->header.blocksize) {
        args->blockSize = frame->header.blocksize;
        if (args->blockSize < (int)frame->header.blocksize) {
            // �u���b�N�T�C�Y���r���ő��������B�т����肷��B
            // �Ȃ��A�u���b�N�T�C�Y���ŏI�t���[���ŏ������l�ɂȂ邱�Ƃ͕��ʂɂ���B
            dprintf("D: block size changed !!! %d to %d\n",
                args->blockSize, frame->header.blocksize);
            assert(0);
        }
    }

    if (args->bitsPerSample == 16) {
        assert((int)frame->header.blocksize <= args->numFrames);

        for(i = 0; i < frame->header.blocksize; i++) {
            memcpy(&args->buff[i*4+0], &buffer[0][i], 2);
            memcpy(&args->buff[i*4+2], &buffer[1][i], 2);
        }
    }

    if (args->bitsPerSample == 24) {
        assert((int)frame->header.blocksize <= args->numFrames);

        for(i = 0; i < frame->header.blocksize; i++) {
            memcpy(&args->buff[i*6+0], &buffer[0][i], 3);
            memcpy(&args->buff[i*6+3], &buffer[1][i], 3);
        }
    }

    dprintf("%s set %d frame. args->errorCode=%d set commandCompleteEvent\n",
        __FUNCTION__, frame->header.blocksize, args->errorCode);

    args->retrievedFrames = frame->header.blocksize;
    args->errorCode       = FDRT_Success;
    SetEvent(args->commandCompleteEvent);
    WaitForSingleObject(args->commandEvent, INFINITE);

    // �N�����B�v�����`�F�b�N����B
    dprintf("%s event received. %d args->errorCode=%d\n",
        __FUNCTION__, args->command, args->errorCode);
    if (args->command == FDC_Shutdown) {
        return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
    }

    return FLAC__STREAM_DECODER_WRITE_STATUS_CONTINUE;
}

static FLAC__StreamDecoderWriteStatus
WriteCallback(const FLAC__StreamDecoder *decoder,
    const FLAC__Frame *frame, const FLAC__int32 * const buffer[],
    void *clientData)
{
    FLAC__StreamDecoderWriteStatus rv =
        WriteCallback1(decoder, frame, buffer, clientData);

    if (rv == FLAC__STREAM_DECODER_WRITE_STATUS_ABORT) {
        /* �f�R�[�h�I�� */
    }
    return rv;
}

static void
MetadataCallback(const FLAC__StreamDecoder *decoder,
    const FLAC__StreamMetadata *metadata, void *clientData)
{
    FlacDecodeInfo *args = (FlacDecodeInfo*)clientData;

    dprintf("%s type=%d\n", __FUNCTION__, metadata->type);

    if(metadata->type == FLAC__METADATA_TYPE_STREAMINFO) {
        args->totalSamples  = metadata->data.stream_info.total_samples;
        args->sampleRate    = metadata->data.stream_info.sample_rate;
        args->channels      = metadata->data.stream_info.channels;
        args->bitsPerSample = metadata->data.stream_info.bits_per_sample;
    }
}

static void
ErrorCallback(const FLAC__StreamDecoder *decoder,
    FLAC__StreamDecoderErrorStatus status, void *clientData)
{
    FlacDecodeInfo *args = (FlacDecodeInfo*)clientData;

    dprintf("%s status=%d\n", __FUNCTION__, status);

    switch (status) {
    case FLAC__STREAM_DECODER_ERROR_STATUS_LOST_SYNC:
        args->errorCode = FDRT_LostSync;
        break;
    case FLAC__STREAM_DECODER_ERROR_STATUS_BAD_HEADER:
        args->errorCode = FDRT_BadHeader;
        break;
    case FLAC__STREAM_DECODER_ERROR_STATUS_FRAME_CRC_MISMATCH:
        args->errorCode = FDRT_FrameCrcMismatch;
        break;
    case FLAC__STREAM_DECODER_ERROR_STATUS_UNPARSEABLE_STREAM:
        args->errorCode = FDRT_Unparseable;
        break;
    default:
        args->errorCode = FDRT_OtherError;
        break;
    }

    if (args->errorCode != FDRT_Success) {
        /* �G���[���N�����B */
    }
};

///////////////////////////////////////////////////////////////

// �f�R�[�h�X���b�h
static int
DecodeMain(FlacDecodeInfo *args)
{
    FLAC__bool                    ok       = true;
    FLAC__StreamDecoder           *decoder = NULL;
    FLAC__StreamDecoderInitStatus init_status;

    dprintf("%s\n", __FUNCTION__);

    decoder = FLAC__stream_decoder_new();
    if(decoder == NULL) {
        args->errorCode = FDRT_FlacStreamDecoderNewFailed;
        dprintf("%s Flac decode error %d. set complete event.\n",
            __FUNCTION__, args->errorCode);
        goto end;
    }

    FLAC__stream_decoder_set_md5_checking(decoder, true);

    init_status = FLAC__stream_decoder_init_file(
        decoder, args->fromFlacPath,
        WriteCallback, MetadataCallback, ErrorCallback, args);
    if(init_status != FLAC__STREAM_DECODER_INIT_STATUS_OK) {
        args->errorCode = FDRT_FlacStreamDecoderInitFailed;
        dprintf("%s Flac decode error %d. set complete event.\n",
            __FUNCTION__, args->errorCode);
        goto end;
    }

    ok = FLAC__stream_decoder_process_until_end_of_stream(decoder);
    if (!ok) {
        // result == args->errorCode;
        dprintf("%s Flac decode error %d. set complete event.\n",
            __FUNCTION__, args->errorCode);
        goto end;
    }

    args->errorCode = FDRT_Completed;
end:
    if (NULL != decoder) {
        FLAC__stream_decoder_delete(decoder);
        decoder = NULL;
    }

    SetEvent(args->commandCompleteEvent);

    dprintf("%s end\n", __FUNCTION__);
    return args->errorCode;
}

static DWORD WINAPI
DecodeEntry(LPVOID param)
{
    dprintf("%s\n", __FUNCTION__);

    FlacDecodeInfo *args = (FlacDecodeInfo*)param;
    DecodeMain(args);

    dprintf("%s end\n", __FUNCTION__);
    return 0;
}

///////////////////////////////////////////////////////////////

/// ���u�̎��́B�O���[�o���ϐ��B
static FlacDecodeInfo g_flacDecodeInfo;

/// �`�����l�����B
/// DecodeStart������ɌĂԂ��Ƃ��ł���B
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetNumOfChannels(void)
{
    if (g_flacDecodeInfo.errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return g_flacDecodeInfo.channels;
}

/// �ʎq���r�b�g���B
/// DecodeStart������ɌĂԂ��Ƃ��ł���B
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetBitsPerSample(void)
{
    if (g_flacDecodeInfo.errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return g_flacDecodeInfo.bitsPerSample;
}

/// �T���v�����[�g�B
/// DecodeStart������ɌĂԂ��Ƃ��ł���B
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetSampleRate(void)
{
    if (g_flacDecodeInfo.errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return g_flacDecodeInfo.sampleRate;
}

/// �T���v��(==frame)�����B
/// DecodeStart������ɌĂԂ��Ƃ��ł���B
extern "C" __declspec(dllexport)
int64_t __stdcall
FlacDecodeDLL_GetNumSamples(void)
{
    if (g_flacDecodeInfo.errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return g_flacDecodeInfo.totalSamples;
}

extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetLastResult(void)
{
    return g_flacDecodeInfo.errorCode;
}

extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetBlockSize(void)
{
    return g_flacDecodeInfo.blockSize;
}

/// FLAC�w�b�_�[��ǂݍ���ŁA�t�H�[�}�b�g�����擾����B
/// ���̃O���[�o���ϐ��ɒ��߂�BAPI�̐݌v���X���b�h�Z�[�t�ɂȂ��ĂȂ��̂Œ��ӁB
/// @return 0 �����B1�ȏ�: �G���[�BFlacDecodeResultType�Q�ƁB
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_DecodeStart(const char *fromFlacPath)
{
    LogOpen();

    dprintf1("%s started\n", __FUNCTION__);
    dprintf1("%s path=\"%s\"\n", __FUNCTION__, fromFlacPath);

    assert(NULL == g_flacDecodeInfo.commandMutex);
    g_flacDecodeInfo.commandMutex = CreateMutex(NULL, FALSE, NULL);
    CHK(g_flacDecodeInfo.commandMutex);

    assert(NULL == g_flacDecodeInfo.commandEvent);
    g_flacDecodeInfo.commandEvent = CreateEventEx(NULL, NULL, 0,
        EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(g_flacDecodeInfo.commandEvent);

    assert(NULL == g_flacDecodeInfo.commandCompleteEvent);
    g_flacDecodeInfo.commandCompleteEvent = CreateEventEx(NULL, NULL, 0,
        EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(g_flacDecodeInfo.commandCompleteEvent);

    g_flacDecodeInfo.errorCode = FDRT_Success;
    strncpy_s(g_flacDecodeInfo.fromFlacPath, fromFlacPath,
        sizeof g_flacDecodeInfo.fromFlacPath-1);

    g_flacDecodeInfo.thread
        = CreateThread(NULL, 0, DecodeEntry, &g_flacDecodeInfo, 0, NULL);
    assert(g_flacDecodeInfo.thread);

    dprintf("%s createThread\n", __FUNCTION__);

    // FlacDecode�X���������n�߂�BcommandCompleteEvent��҂B
    // FlacDecode�X���́A�r���ŃG���[���N���邩�A
    // �f�[�^�̏������ł�����commandCompleteEvent�𔭍s���AcommandEvent��Wait����B
    WaitForSingleObject(g_flacDecodeInfo.commandCompleteEvent, INFINITE);
    
    dprintf1("%s commandCompleteEvent. ercd=%d\n",
        __FUNCTION__, g_flacDecodeInfo.errorCode);
    return g_flacDecodeInfo.errorCode;
}

#define CLOSE_SET_NULL(p) \
if (NULL != p) {          \
    CloseHandle(p);       \
    p = NULL;             \
}

/// FlacDecode���I������B(DecodeStart�ŗ��Ă��X�����~�߂��肷��)
extern "C" __declspec(dllexport)
void __stdcall
FlacDecodeDLL_DecodeEnd(void)
{
    dprintf1("%s started.\n", __FUNCTION__);

    if (g_flacDecodeInfo.thread) {
        assert(g_flacDecodeInfo.commandMutex);
        assert(g_flacDecodeInfo.commandEvent);
        assert(g_flacDecodeInfo.commandCompleteEvent);

        WaitForSingleObject(g_flacDecodeInfo.commandMutex, INFINITE);
        g_flacDecodeInfo.command = FDC_Shutdown;

        dprintf("%s SetEvent and wait to complete FlacDecodeThead\n",
            __FUNCTION__);

        SetEvent(g_flacDecodeInfo.commandEvent);
        ReleaseMutex(g_flacDecodeInfo.commandMutex);

        // �X���b�h���I���͂��B
        WaitForSingleObject(g_flacDecodeInfo.thread, INFINITE);

        dprintf("%s thread stopped. delete FlacDecodeThead\n",
            __FUNCTION__);
        CLOSE_SET_NULL(g_flacDecodeInfo.thread);
    }

    CLOSE_SET_NULL(g_flacDecodeInfo.commandEvent);
    CLOSE_SET_NULL(g_flacDecodeInfo.commandCompleteEvent);
    CLOSE_SET_NULL(g_flacDecodeInfo.commandMutex);

    g_flacDecodeInfo.Clear();

    dprintf1("%s done.\n", __FUNCTION__);
    LogClose();
}

/// ����PCM�f�[�^��numFrame�T���v������buff_return�ɋl�߂�
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetNextPcmData(int numFrame, char *buff_return)
{
    if (NULL == g_flacDecodeInfo.thread) {
        dprintf("%s FlacDecodeThread is not ready.\n",
            __FUNCTION__);
        return E_FAIL;
    }

    assert(g_flacDecodeInfo.commandMutex);
    assert(g_flacDecodeInfo.commandEvent);
    assert(g_flacDecodeInfo.commandCompleteEvent);

    const int bytesPerFrame
        = g_flacDecodeInfo.channels * g_flacDecodeInfo.bitsPerSample/8;

    int pos = 0;

    while (pos < numFrame) {
        dprintf("%s pos=%d numFrame=%d\n",
            __FUNCTION__, pos, numFrame);

        {   // FlacDecodeThread��GetFrames�R�}���h��`����
            WaitForSingleObject(g_flacDecodeInfo.commandMutex, INFINITE);

            g_flacDecodeInfo.errorCode    = FDRT_Success;
            g_flacDecodeInfo.command      = FDC_GetFrames;
            g_flacDecodeInfo.buff         = &buff_return[bytesPerFrame * pos];
            g_flacDecodeInfo.numFrames    = numFrame;
            g_flacDecodeInfo.retrievedFrames = 0;

            dprintf("%s set command.\n", __FUNCTION__);
            SetEvent(g_flacDecodeInfo.commandEvent);

            ReleaseMutex(g_flacDecodeInfo.commandMutex);
        }

        dprintf("%s wait for commandCompleteEvent.\n", __FUNCTION__);
        WaitForSingleObject(g_flacDecodeInfo.commandCompleteEvent, INFINITE);

        dprintf("%s command completed. ercd=%d retrievedFrames=%d\n",
            __FUNCTION__, g_flacDecodeInfo.errorCode,
            g_flacDecodeInfo.retrievedFrames);

        pos += g_flacDecodeInfo.retrievedFrames;

        if (g_flacDecodeInfo.errorCode != FDRT_Success) {
            break;
        }
    }

    dprintf1("%s numFrame=%d retrieved=%d ercd=%d\n",
            __FUNCTION__, numFrame, pos, g_flacDecodeInfo.errorCode);

    if (FDRT_Success   != g_flacDecodeInfo.errorCode &&
        FDRT_Completed != g_flacDecodeInfo.errorCode) {
        // �G���[�I���B
        return -1;
    }
    return pos;
}

