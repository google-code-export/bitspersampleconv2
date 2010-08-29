#pragma once

enum Flac2WavResultType {
    F2WRT_Success = 0,
    F2WRT_WriteOpenFailed,
    F2WRT_FlacStreamDecoderNewFailed,
    F2WRT_FlacStreamDecoderInitFailed,
    F2WRT_LostSync,
    F2WRT_BadHeader,
    F2WRT_FrameCrcMismatch,
    F2WRT_Unparseable,
    F2WRT_OtherError
};

/// FLAC�t�@�C����ǂݍ���ŁAWAV�t�@�C�����o�͂���B
/// @return 0 �����B1�ȏ�: �G���[�BFlac2WavResultType�Q�ƁB
extern "C" __declspec(dllexport)
int __stdcall
Flac2Wav(const char *fromFlacPath, const char *toWavPath);
