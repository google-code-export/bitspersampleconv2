English | [日本語](http://code.google.com/p/bitspersampleconv2/wiki/PlayPcmWinChangelog)

# [PlayPcmWin](http://code.google.com/p/bitspersampleconv2/wiki/PlayPcmWinEn) Changelog #

### PlayPcmWin 4.0.78 ###
  * Simplified-Chinese translation updated

### PlayPcmWin 4.0.77 ###
  * [Issue 60](https://code.google.com/p/bitspersampleconv2/issues/detail?id=60) Polarity invert function created
  * [Issue 148](https://code.google.com/p/bitspersampleconv2/issues/detail?id=148) Monaural playback function created

### PlayPcmWin 4.0.75 ###
  * [Issue 144](https://code.google.com/p/bitspersampleconv2/issues/detail?id=144) fixed to capture mediakeys event when application does not have a keyboard focus
  * [Issue 143](https://code.google.com/p/bitspersampleconv2/issues/detail?id=143) remember last selected file filter on file open dialog

### PlayPcmWin 4.0.74 ###
  * Enable NextTrack/PrevTrack buttons on pause state and stop state.
  * When stop button is pressed while playing, play fade-out sound and stop decently.

### PlayPcmWin 4.0.73 ###
  * libflac updated to version 1.3.1
  * [Issue 144](https://code.google.com/p/bitspersampleconv2/issues/detail?id=144) Catch media keys event (play/pause, stop, next track, prev track)

### PlayPcmWin 4.0.72 ###
  * Bugfix: FLAC MD5 signature calculation is performed even when it is disabled on detailed settings of PPW.
  * Bugfix: Read error on unsupported ID3tag. Now PPW skips through it and continue to read the music file

### PlayPcmWin 4.0.70 ###
  * [Issue 138](https://code.google.com/p/bitspersampleconv2/issues/detail?id=138) Fix crash when selecting File Open from PlayPcmWin menubar on Windows Simplified Chinese version. This bug is introduced on PlayPcmWin 4.0.66

### PlayPcmWin 4.0.69 ###
  * [Issue 71](https://code.google.com/p/bitspersampleconv2/issues/detail?id=71) Fixed WAV file read bug
  * Read WAV "id3 " tag
  * Enable "Inspect device" button when playlist is not empty

### PlayPcmWin 4.0.67 ###
  * Test build for [Issue 138](https://code.google.com/p/bitspersampleconv2/issues/detail?id=138) inspection

### PlayPcmWin 4.0.66 ###
  * Updated Simplified Chinese translation.
  * When outputting 32bit quantization bit rate, 24bit ValidBitsPerSample PCM to 24-bit device, PPW now does not perform noise shaping using lower 8bit.

### PlayPcmWin 4.0.64 ###
  * Fix [Issue 136](https://code.google.com/p/bitspersampleconv2/issues/detail?id=136) PPW 4.0.63 does not play 32bit quantization bit rate and 24bit ValidBitsPerSample WAV file

### PlayPcmWin 4.0.63 ###
  * [Issue 135](https://code.google.com/p/bitspersampleconv2/issues/detail?id=135) Add dither when bits per sample is reduced

![http://bitspersampleconv2.googlecode.com/files/SettingsNoiseShapingEn.png](http://bitspersampleconv2.googlecode.com/files/SettingsNoiseShapingEn.png)

### PlayPcmWin 4.0.62 ###
  * Fixed read bug of WAV files which fmt chunk size is 40 bytes and extensible size is 0 bytes. (Certain version of Pro Tools produces  this type of WAV files) ([revision 3253](https://code.google.com/p/bitspersampleconv2/source/detail?r=3253))

### PlayPcmWin 4.0.61 ###
  * 1channel monaural DSF playback
  * Read DSF files larger than 2GB (on 64-bit version)
  * (Bug fix) When UTF-8 text appears in ID3 name frame, first 2 bytes of text string are truncated

### PlayPcmWin 4.0.60 ###
  * (Bug fix) When UTF-8 text appears in ID3 name frame, first 2 bytes of text string are truncated

### PlayPcmWin 4.0.59 ###
  * Fixed [Issue 131](https://code.google.com/p/bitspersampleconv2/issues/detail?id=131) "Zero flush milliseconds setting does not work". This bug is introduced on PlayPcmWin 4.0.52, on the course of refactoring WasapiCS API

### PlayPcmWin 4.0.58 ###
  * Fixed [Issue 130](https://code.google.com/p/bitspersampleconv2/issues/detail?id=130) PPW does not update Playlist on D&D (but play button is enabled and PPW crashes on play button press) when "Restore the playlist on program startup" is unchecked.

### PlayPcmWin 4.0.57 ###
  * Refrain DwmEnableMMCSS call. Now this is default of DwmEnableMMCSS call setting.

### PlayPcmWin 4.0.56 ###
  * Test build for [Issue 130](https://code.google.com/p/bitspersampleconv2/issues/detail?id=130) inspection

### PlayPcmWin 4.0.54 ###
  * Added application icon size 96x96 and 256x256. (96x96 icon is used by 150% text sizing)

![http://bitspersampleconv2.googlecode.com/files/ppw150percenticon.png](http://bitspersampleconv2.googlecode.com/files/ppw150percenticon.png)

### PlayPcmWin 4.0.53 ###
  * libFLAC.lib optimization option tweak

### PlayPcmWin 4.0.52 ###
  * Display endpoint buffer frame number in log window before playback starts (for debugging purpose). This value is output parameter of IAudioClient::GetBufferSize()  http://msdn.microsoft.com/en-us/library/windows/desktop/dd370866%28v=vs.85%29.aspx

### PlayPcmWin 4.0.50 ###
  * Fix program crash when the reading file is disappeared while loading

### PlayPcmWin 4.0.49 ###
  * On program startup, perform playlist file loading after window creation.
  * (New feature) Invoke DwmEnableMMCSS(FALSE)

### PlayPcmWin 4.0.47 (unstable) ###
  * (New feature) DoP playback (05/FA method) of DSD 11.2MHz, DSD 22.5MHz, DSD 3.0MHz, DSD 6.1MHz, DSD 12.2MHz and DSD 24.5MHz.
  * Perform playlist file loading after window creation on program startup. Blank window appears while playlist loading and it looks not so good.

### PlayPcmWin 4.0.46 ###
  * (New feature) Enable/disable GPU rendering ([Revision 3023](https://code.google.com/p/bitspersampleconv2/source/detail?r=3023))

### PlayPcmWin 4.0.45 ###
  * Fix [Issue 120](https://code.google.com/p/bitspersampleconv2/issues/detail?id=120) Application crashes when latency value text is selected and dropped to PPW window.

### PlayPcmWin 4.0.43 ###
  * Settings window layout updated to accommodate new checkboxes. ([Revision 2960](https://code.google.com/p/bitspersampleconv2/source/detail?r=2960))
  * (New feature) Sort dropped files ordered by file name ([Issue 123](https://code.google.com/p/bitspersampleconv2/issues/detail?id=123))
  * (New feature) Set Batch read endpoint on file drop. This feature enables to read one track onto main memory and play when multiple tracks exists on playlist and play mode is all-tracks. ([Issue 127](https://code.google.com/p/bitspersampleconv2/issues/detail?id=127))
  * (New feature) Calculate FLAC MD5sum on file read and compare with metadata MD5sum. ([Issue 126](https://code.google.com/p/bitspersampleconv2/issues/detail?id=126))

![http://bitspersampleconv2.googlecode.com/files/ppw4043ss2.png](http://bitspersampleconv2.googlecode.com/files/ppw4043ss2.png)

MD5 mismatch dialog

![http://bitspersampleconv2.googlecode.com/files/flacmd5mismatch2.png](http://bitspersampleconv2.googlecode.com/files/flacmd5mismatch2.png)

### PlayPcmWin 4.0.42 ###
  * Settings window layout updated. ([Revision 2957](https://code.google.com/p/bitspersampleconv2/source/detail?r=2957))
  * DSD DoP playback seek data generation algorithm is slightly optimized but still not very good ([Revision 2950](https://code.google.com/p/bitspersampleconv2/source/detail?r=2950))

### PlayPcmWin 4.0.37 ###
  * Fixed DSD DoP playback glich on file seek and track change
  * Support DoP playback of DSD5.6MHz 2ch stereo DSD data(0x05/0xfa method) I hope it works :) (not tested on real DSD files)

### PlayPcmWin 4.0.32 ###
  * DoP playback of DSDIFF files(2.8MHz 2ch stereo“not compressed”DSD).
  * DoP playback glitch still not fixed

  * DoP playback test program http://bitspersampleconv2.googlecode.com/files/PlayPcm106.zip
  * DoP playback test program source code: http://code.google.com/p/bitspersampleconv2/source/browse/#svn%2Ftrunk%2FPlayPcm

### PlayPcmWin 4.0.31 ###
  * DoP playback of DSF files(2.8MHz 2ch stereo DSD)
  * DoP playback glitch exists

### PlayPcmWin 4.0.29 ###
  * WASAPI exclusive PCM quantization bit rate selection algorithm improved ([Revision 2869](https://code.google.com/p/bitspersampleconv2/source/detail?r=2869)) ([Issue 121](https://code.google.com/p/bitspersampleconv2/issues/detail?id=121))

### PlayPcmWin 4.0.28 ###
  * [Issue 121](https://code.google.com/p/bitspersampleconv2/issues/detail?id=121) WASAPI initialize error message updated. (about DSPeaker Anti-Mode 2.0 Dual Core)
  * [Issue 123](https://code.google.com/p/bitspersampleconv2/issues/detail?id=123) (New feature) Sort folder by filename when dropped

### PlayPcmWin 4.0.27 ###
  * [Revision 2858](https://code.google.com/p/bitspersampleconv2/source/detail?r=2858) Fixed bug: “Noise shaping...” message is always displayed even when noise shaping is not performed

### PlayPcmWin 4.0.26 ###
  * (New feature) Perform noise shaping when quantization bit rate is reduced on WASAPI exclusive mode playback

### PlayPcmWin 4.0.25 ###
  * Remember last played track number using IsolatedStorage ([Revision 2830](https://code.google.com/p/bitspersampleconv2/source/detail?r=2830))

### PlayPcmWin 4.0.24 ###
  * (Bugfix) There seems timer resolution=0.5ms settings is not stable so dialog text updated to add (unstable) ([Issue 118](https://code.google.com/p/bitspersampleconv2/issues/detail?id=118))。(Timer resolution monitor program: http://bitspersampleconv2.googlecode.com/files/TimerResolutionMonitor101.zip )

### PlayPcmWin 4.0.23 ###
  * (New feature) On WASAPI shared mode, Limits maximum quantized value magnitude to 0.98 to prevent limiter APO artifact

### PlayPcmWin 4.0.22 ###
  * (New feature) Setup timer resolution to 0.5ms

### PlayPcmWin 4.0.20 ###
  * PlayPcmWinTestBench updates

### PlayPcmWin 4.0.19 ###
  * Message text displayed on WASAPI Setup failure is updated ([Issue 41](https://code.google.com/p/bitspersampleconv2/issues/detail?id=41))
  * PlayPcmWinTestBench updates.

### PlayPcmWin 4.0.18 ###
  * PlayPcmWinTestBench updates.

### PlayPcmWin 4.0.17 ###
  * PlayPcmWinTestBench updates.

### PlayPcmWin 4.0.16 ###
  * [Issue 116](https://code.google.com/p/bitspersampleconv2/issues/detail?id=116) (BugFix) album name is not displayed when album name is updated using Mp3tag
  * PlayPcmWinTestBench updates.

### PlayPcmWin 4.0.14 ###
  * [Issue 115](https://code.google.com/p/bitspersampleconv2/issues/detail?id=115) (BugFix) Application crashes when performer is missing on CUE sheets
  * (New feature) CUE sheets Character encoding selection combo box
![http://bitspersampleconv2.googlecode.com/files/ppwsettings414.png](http://bitspersampleconv2.googlecode.com/files/ppwsettings414.png)

### PlayPcmWin 4.0.12 ###
  * [Issue 115](https://code.google.com/p/bitspersampleconv2/issues/detail?id=115) display title name from playlist file when playlist file and music metadata both contains title name

### PlayPcmWin 4.0.11 ###
  * [Issue 115](https://code.google.com/p/bitspersampleconv2/issues/detail?id=115) (BugFix) Display title name from music metadata when playlist file does not contain music title name

### PlayPcmWin 4.0.10 ###
  * [Issue 115](https://code.google.com/p/bitspersampleconv2/issues/detail?id=115) (BugFix) Display album name from playlist file when music file does not contain album name

### PlayPcmWin 4.0.9 ###
  * [Issue 115](https://code.google.com/p/bitspersampleconv2/issues/detail?id=115) (New feature) Supports M3U8 playlist file.
  * Application icon updated.
![http://bitspersampleconv2.googlecode.com/files/ppwlogodiff.png](http://bitspersampleconv2.googlecode.com/files/ppwlogodiff.png)

### PlayPcmWin 4.0.8 ###
  * Simplified Chinese translation

### PlayPcmWin 4.0.7 ###
  * (BugFix) Missing application icon for uninstaller on English 64bit version

### PlayPcmWin 4.0.6 ###
  * Application icon updated.

### PlayPcmWin 4.0.4 ###
  * Settings window Japanese translation updated

### PlayPcmWin 4.0.3 ###
  * Fixed most of FxCop warnings
  * Traditional Chinese translation
  * Settings IsolatedStorage format changed due to typo correction of XML tag names found by FxCop

### PlayPcmWin 4.0.1 ###
  * [Issue 114](https://code.google.com/p/bitspersampleconv2/issues/detail?id=114) (BugFix) Fixed GPF bug on WASAPI shared mode. This bug is introduced on PlayPcmWin 3.0.98

### PlayPcmWin 4.0.0 ###
  * [Issue 113](https://code.google.com/p/bitspersampleconv2/issues/detail?id=113) (BugFix) Seek thumb does not move. This bug is introduced on PlayPcmWin 3.0.99