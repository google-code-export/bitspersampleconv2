# WWAudioFilter #

WWAudioFilter is my personal experiment program of several digital audio filtering techniques.

# Functions Available #

  * 16bit/24bit FLAC file read
  * 24bit FLAC file write or 1bit DSF write
  * linear-phase FIR low pass filter
  * Zero-order hold upsampling
  * FFT upsampling
  * Gain adjustment
  * Noise shaping

All filters are processed using 64-bit floating point format.

![http://bitspersampleconv2.googlecode.com/files/WWAudioFilter102SS.png](http://bitspersampleconv2.googlecode.com/files/WWAudioFilter102SS.png)

# System Requirements #

  * Windows 7 64bit or Windows 8 64bit
  * 8GB ram

# Download #

  * Windows x64 version http://bitspersampleconv2.googlecode.com/files/WWAudioFilter105.zip

# Source Code #

http://code.google.com/p/bitspersampleconv2/source/browse/#svn%2Ftrunk%2FWWAudioFilter

# How to Build #

[HowToBuildWWAudioFilter](HowToBuildWWAudioFilter.md)

# Changelog #

WWAudioFilter 1.0.5
  * Tag edit function

WWAudioFilter 1.0.4
  * 4th order noise shaping
  * Accepts input file by file drop
  * Display error dialog when output file size exceeds 2GB

WWAudioFilter 1.0.3
  * FFT upsample FFT size setting

WWAudioFilter 1.0.2
  * DSF output
  * 2nd order Noise shaping
  * (BugFix) Gain is reduced when FFT upsample 4x 8x or 16x is used

WWAudioFilter 1.0.1
  * Initial release
