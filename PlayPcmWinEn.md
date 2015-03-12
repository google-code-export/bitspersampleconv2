English | [日本語](http://code.google.com/p/bitspersampleconv2/wiki/PlayPcmWin)

# PlayPcmWin #

PlayPcmWin is yet another opensource audio player for audiophiles.

# Features #

  * Supports WASAPI exclusive mode playback. Bit-perfect capable.
  * Memory play. Load all PCM data onto the main memory before the playback starts.
  * **Native C++ optimized code for the playback thread.** C# .NET 4.0 WPF GUI for easy use.
  * Supports WAV(16, 24, 32bit), FLAC(16, 24bit), AIFF(16, 24bit)  and AIFC-sowt formats.
  * DoP playback of DFF and DSF files.
  * Supports CUE sheets and M3U8 playlists.
  * Gapless playback.
  * Source code available.

# Supported Platforms #
  * Windows Vista (SP1 or later), Windows 7. ( **Windows XP is not supported**, because XP does not have WASAPI. )
  * WASAPI exclusive mode playback of PlayPcmWin works on Windows Vista and Windows 7.
  * PlayPcmWin uses Resampler MFT that is introduced on Windows 7 therefore **WASAPI shared mode playback of PlayPcmWin doesn't work on Windows Vista**.

# Downloads 下载 #

Stable version (PlayPcmWin 4.0.78)
  * 64-bit English  http://sourceforge.net/projects/playpcmwin/files/PlayPcmWin/PlayPcmWin478x64en.zip/download
  * 32-bit English  http://sourceforge.net/projects/playpcmwin/files/PlayPcmWin/PlayPcmWin478en.zip/download
  * 64位 简体中文版 http://sourceforge.net/projects/playpcmwin/files/PlayPcmWin/PlayPcmWin478x64cn.zip/download
  * 32位 简体中文版 http://sourceforge.net/projects/playpcmwin/files/PlayPcmWin/PlayPcmWin478cn.zip/download

Other versions
  * http://sourceforge.net/projects/playpcmwin/files/PlayPcmWin/

More older versions
  * http://code.google.com/p/bitspersampleconv2/downloads/list

# Changelog #

  * http://code.google.com/p/bitspersampleconv2/wiki/PlayPcmWinChangelogEn

# License #

  * PlayPcmWin: MIT License http://code.google.com/p/bitspersampleconv2/source/browse/trunk/PlayPcmWin/PlayPcmWinLicense.txt
  * libFLAC: New BSD License http://code.google.com/p/bitspersampleconv2/source/browse/trunk/PlayPcmWin/libFlacLicense.txt

![http://bitspersampleconv2.googlecode.com/files/ppw3070ss2_en.png](http://bitspersampleconv2.googlecode.com/files/ppw3070ss2_en.png)

# 64-bit version or 32-bit version, Which is better? #

These two versions are created from the same source code. 64-bit version is better than 32-bit version for lower CPU usage and its capability to use larger virtual memory space. (faster processing, more space for music data)

But 64-bit version doesn't work if your computer has 32-bit operating system.

# Additional setting is necessary with RME FireFace 400/800/UC/UFX, M-AUDIO ProFire series, Echo AudioFire series etc #

These professional audio equipments do not support sample rate change via WASAPI. Please set sample rate manually prior to play using "FireFace Settings", "M-Audio Profire Control Panel" or "AudioFire Console" application on your system tray.

# What's the data feed mode? Which is better for the sound quality ? #

  * Event driven data feed mode: Playback thread wakes up from sleep state by WASAPI buffer request event. Wake up interval of playback thread is specified by output latency time. Buffer refill sample size is latency time x sample rate (samples).
  * Timer driven data feed mode: Playback thread wakes up by timer event. Timer alarm interval is specified by output latency time / 2.

Theoretically, Event driven mode is more sophisticated method than timer driven mode. It minimizes CPU load and elongate sleep interval of the playback thread.

Generally speaking, Event driven mode is recommended for lower CPU load and less sound glitch.

In the real world, Several devices prefer event driven mode (On those devices, Timer driven mode lead to frequent click noise), Other a few devices prefer timer driven mode (Cannot use event mode at all). Most devices do work well on both mode.

# About the render thread task type #

First, Render means playback, Capture means recording :)

If you want to set 10 ms or fewer output latency,
**Pro Audio** is preferred option. If you choose Pro Audio option,
The render thread runs on the highest priority. It reduces the probability of output buffer underflow accidents but from the power consumption standpoint, It causes negative impact.

Render thread task type settings ultimately chooses
the first parameter of
AvSetMmThreadCharacteristics() function call of the playback thread.

If you choose **None** , the playback thread does not call AvSetMmThreadCharacteristics() at all.

**Playback** has lower priority than **Pro Audio** but higher priority than **Audio**. These difference is subtle, I think **Playback** is suitable for high CPU load environment such as background video transcoding or CGI rendering or other background number crunching tasks.

Very detailed description is available on these website:
  * http://msdn.microsoft.com/en-us/library/ms684247%28v=VS.85%29.aspx
  * http://msdn.microsoft.com/en-us/library/bb614507.aspx

# Exclusive or Shared #

I strongly recommend to choose exclusive mode for optimal sound quality. Exclusive mode bypasses windows mixer and numerous PCM data altering effects such as poor quality windows built-in software sample rate conversion.

# About wasapi shared resampler quality value on settings window #

PlayPcmWin uses Resampler MFT(Audio Resampler DSP) to resample PCM data on wasapi shared mode.
http://msdn.microsoft.com/en-us/library/windows/desktop/ff819070%28v=vs.85%29.aspx

Wasapi shared resampler quality value is used as
IWMResamplerProps::SetHalfFilterLength() argument.
http://msdn.microsoft.com/en-us/library/windows/desktop/ff819250%28v=vs.85%29.aspx

Resampler MFT is introduced on Windows 7. PlayPcmWin wasapi shared mode playback does not run on Vista.

On wasapi exclusive mode, PlayPcmWin does not perform resampling (sample rate conversion) so this parameter is not used.

# If you experience sound stuttering on playback #

Windows is complicated system and has wide variety of hardware and software. Many factors are involved this type of problem.

The following page provides very useful troubleshooting regarding to playback glitch on Windows. I recommend to check your system according to this guide.
http://www.native-instruments.com/knowledge/questions/847/Windows+7+Tuning+Tips+for+audio+processing

# How to build #

PlayPcmWinHowToBuild

# What is Int16, Int24, Int32 and Int32v24 ? #

  * Int16: 16-bit PCM
  * Int24: 24-bit PCM
  * Int32: 32-bit PCM
  * Int32v24: 32-bit PCM data. valid bits=24bits

For more details, please refer following document :
PlayPcmWinPcmSerializationFormat