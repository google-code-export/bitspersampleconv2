using System.Text;
using System.Runtime.InteropServices;
using System;

namespace Wasapi {
    public class WasapiCS {
        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Init();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_Term();

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_DoDeviceEnumeration(int deviceType);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetDeviceCount();

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet=CharSet.Unicode)]
        internal struct WasapiIoDeviceAttributes {
            public int    deviceId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String deviceIdString;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_GetDeviceAttributes(int id, out WasapiIoDeviceAttributes attr);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_InspectDevice(int id, int sampleRate, int bitsPerSample, int validBitsPerSample, int bitFormat);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_ChooseDevice(int id);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_UnchooseDevice();

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_GetUseDeviceAttributes(out WasapiIoDeviceAttributes attr);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct SetupArgs {
            public int streamType;
            public int sampleRate;
            public int sampleFormat;
            public int numChannels;
            public int shareMode;
            public int schedulerTask;
            public int dataFeedMode;
            public int latencyMillisec;
            public int timePeriodHandledNanosec;
            public int zeroFlushMillisec;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Setup(ref SetupArgs args);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_Unsetup();

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_AddPlayPcmDataStart();

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_AddPlayPcmData(int id, byte[] data, long bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_AddPlayPcmDataSetPcmFragment(int id, long posBytes, byte[] data, long bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_ResampleIfNeeded(int conversionQuality);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_AddPlayPcmDataEnd();

        [DllImport("WasapiIODLL.dll")]
        private extern static double
        WasapiIO_ScanPcmMaxAbsAmplitude();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_ScalePcmAmplitude(double scale);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_ClearPlayList();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_RemovePlayPcmDataAt(int id);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetPlayRepeat(bool repeat);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetPcmDataId(int usageType);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetNowPlayingPcmDataId(int id);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_SetupCaptureBuffer(long bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static long
        WasapiIO_GetCapturedData(byte[] data, long bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static long
        WasapiIO_GetCaptureGlitchCount();

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_StartPlayback(int wavDataId);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_StartRecording();

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_Run(int millisec);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_Stop();

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Pause();

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Unpause();

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_SetPosFrame(long v);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct WasapiIoSessionStatus {
            public int streamType;
            public int pcmDataSampleRate;
            public int deviceSampleRate;
            public int deviceSampleFormat;
            public int deviceBytesPerFrame;
            public int deviceNumChannels;
            public int timePeriodHandledNanosec;
            public int bufferFrameNum;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_GetSessionStatus(out WasapiIoSessionStatus a);

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct WasapiIoCursorLocation {
            public long posFrame;
            public long totalFrameNum;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_GetPlayCursorPosition(int usageType, out WasapiIoCursorLocation a);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate void StateChangedCallback(StringBuilder idStr);

        [DllImport("WasapiIODLL.dll")]
        public static extern void WasapiIO_RegisterCallback(StateChangedCallback callback);

        public enum SchedulerTaskType {
            None,
            Audio,
            ProAudio,
            Playback
        };

        public enum ShareMode {
            Shared,
            Exclusive
        };

        public enum DataFeedMode {
            EventDriven,
            TimerDriven,
        };

        public enum DeviceType {
            Play,
            Rec
        };

        /// <summary>
        /// enum項目はPcmData.ValueRepresentationTypeと同じ順番で並べる。
        /// WasapiPcmUtilのVrtToBftも参照。
        /// </summary>
        public enum BitFormatType {
            SInt,
            SFloat
        };

        public enum SampleFormatType {
            Unknown = -1,
            Sint16,
            Sint24,
            Sint32V24,
            Sint32,
            Sfloat,
            Sdouble, //< WASAPIはサポートしないが便宜上用意する
        };

        public enum StreamType {
            PCM,
            DoP,
        };

        /// <summary>
        /// サンプルフォーマットタイプ→メモリ上に占めるビット数(1サンプル1chあたり)
        /// </summary>
        /// <param name="t">サンプルフォーマットタイプ</param>
        /// <returns>メモリ上に占めるビット数(1サンプル1chあたり)</returns>
        public static int SampleFormatTypeToUseBitsPerSample(SampleFormatType t) {
            switch (t) {
            case SampleFormatType.Sint16:
                return 16;
            case SampleFormatType.Sint24:
                return 24;
            case SampleFormatType.Sint32V24:
                return 32;
            case SampleFormatType.Sint32:
                return 32;
            case SampleFormatType.Sfloat:
                return 32;
            case SampleFormatType.Sdouble:
                return 64;
            default:
                System.Diagnostics.Debug.Assert(false);
                return 0;
            }
        }

        public static SampleFormatType BitAndFormatToSampleFormatType(int bitsPerSample, int validBitsPerSample, BitFormatType bitFormat) {
            if (bitFormat == BitFormatType.SInt) {
                // int
                switch (bitsPerSample) {
                case 16:
                    return SampleFormatType.Sint16;
                case 24:
                    return SampleFormatType.Sint24;
                case 32:
                    switch (validBitsPerSample) {
                    case 24:
                        return SampleFormatType.Sint32V24;
                    case 32:
                        return SampleFormatType.Sint32;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        return SampleFormatType.Unknown;
                    }
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return SampleFormatType.Unknown;
                }
            } else {
                // float
                switch (bitsPerSample) {
                case 32:
                    return SampleFormatType.Sfloat;
                case 64:
                    return SampleFormatType.Sdouble;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return SampleFormatType.Unknown;
                }
            }
        }

        /// <summary>
        ///  サンプルフォーマットタイプ→有効ビット数(1サンプル1chあたり。バイト数ではなくビット数)
        /// </summary>
        /// <param name="t">サンプルフォーマットタイプ</param>
        /// <returns>有効ビット数(1サンプル1chあたり。バイト数ではなくビット数)</returns>
        public static int SampleFormatTypeToValidBitsPerSample(SampleFormatType t) {
            switch (t) {
            case SampleFormatType.Sint16:
                return 16;
            case SampleFormatType.Sint24:
                return 24;
            case SampleFormatType.Sint32V24:
                return 24;
            case SampleFormatType.Sint32:
                return 32;
            case SampleFormatType.Sfloat:
                return 32;
            case SampleFormatType.Sdouble:
                return 64;
            default:
                System.Diagnostics.Debug.Assert(false);
                return 0;
            }
        }

        public int Init() {
            return WasapiIO_Init();
        }

        public void Term() {
            WasapiIO_Term();
        }

        public void RegisterCallback(StateChangedCallback callback) {
            WasapiIO_RegisterCallback(callback);
        }

        public int DoDeviceEnumeration(DeviceType t) {
            return WasapiIO_DoDeviceEnumeration((int)t);
        }

        public int GetDeviceCount() {
            return WasapiIO_GetDeviceCount();
        }

        public class DeviceAttributes {
            public int Id { get; set; }
            public string Name { get; set; }
            public string DeviceIdString { get; set; }

            public DeviceAttributes(int id, string name, string deviceIdString) {
                Id = id;
                Name = name;
                DeviceIdString = deviceIdString;
            }
        };

        public DeviceAttributes GetDeviceAttributes(int id) {
            var a = new WasapiIoDeviceAttributes();
            if (!WasapiIO_GetDeviceAttributes(id, out a)) {
                return null;
            }
            return new DeviceAttributes(a.deviceId, a.name, a.deviceIdString);
        }

        public int InspectDevice(int id, int sampleRate, int bitsPerSample, int validBitsPerSample, int bitFormat) {
            return WasapiIO_InspectDevice(id, sampleRate, bitsPerSample, validBitsPerSample, bitFormat);
        }

        public int ChooseDevice(int id) {
            return WasapiIO_ChooseDevice(id);
        }

        public void UnchooseDevice() {
            WasapiIO_UnchooseDevice();
        }

        public DeviceAttributes GetUseDeviceAttributes() {
            var a = new WasapiIoDeviceAttributes();
            if (!WasapiIO_GetUseDeviceAttributes(out a)) {
                return null;
            }
            return new DeviceAttributes(a.deviceId, a.name, a.deviceIdString);
        }

        public int Setup(StreamType streamType, int sampleRate, SampleFormatType format, int numChannels,
                SchedulerTaskType schedulerTask, ShareMode shareMode, DataFeedMode dataFeedMode,
                int latencyMillisec, int zeroFlushMillisec, int timePeriodHandredNanosec) {
            var args = new SetupArgs();
            args.streamType = (int)streamType;
            args.sampleRate = sampleRate;
            args.sampleFormat = (int)format;
            args.numChannels = numChannels;
            args.schedulerTask = (int)schedulerTask;
            args.shareMode = (int)shareMode;
            args.dataFeedMode = (int)dataFeedMode;
            args.latencyMillisec = latencyMillisec;
            args.timePeriodHandledNanosec = timePeriodHandredNanosec;
            args.zeroFlushMillisec = zeroFlushMillisec;
            return WasapiIO_Setup(ref args);
        }

        public void Unsetup() {
            WasapiIO_Unsetup();
        }

        public bool AddPlayPcmDataStart() {
            return WasapiIO_AddPlayPcmDataStart();
        }

        public bool AddPlayPcmData(int id, byte[] data) {
            return WasapiIO_AddPlayPcmData(id, data, data.LongLength);
        }

        public bool AddPlayPcmDataAllocateMemory(int id, long bytes) {
            return WasapiIO_AddPlayPcmData(id, null, bytes);
        }

        public bool AddPlayPcmDataSetPcmFragment(int id, long posBytes, byte[] data) {
            return WasapiIO_AddPlayPcmDataSetPcmFragment(id, posBytes, data, data.Length);
        }

        /// <summary>
        /// perform resample on shared mode. blocking call.
        /// </summary>
        /// <param name="conversionQuality">1(minimum quality) to 60(maximum quality)</param>
        /// <returns>HRESULT</returns>
        public int ResampleIfNeeded(int conversionQuality) {
            return WasapiIO_ResampleIfNeeded(conversionQuality);
        }

        public double ScanPcmMaxAbsAmplitude() {
            return WasapiIO_ScanPcmMaxAbsAmplitude();
        }

        public void ScalePcmAmplitude(double scale) {
            WasapiIO_ScalePcmAmplitude(scale);
        }

        public bool AddPlayPcmDataEnd() {
            return WasapiIO_AddPlayPcmDataEnd();
        }

        public void RemovePlayPcmDataAt(int id) {
            WasapiIO_RemovePlayPcmDataAt(id);
        }

        public void ClearPlayList() {
            WasapiIO_ClearPlayList();
        }

        public void SetPlayRepeat(bool repeat) {
            WasapiIO_SetPlayRepeat(repeat);
        }

        public enum PcmDataUsageType {
            NowPlaying,
            PauseResumeToPlay,
            SpliceNext,
            Capture,
        };

        public int GetPcmDataId(PcmDataUsageType t) {
            return WasapiIO_GetPcmDataId((int)t);
        }

        /// <summary>
        /// 再生中の曲変更。
        /// idのグループが読み込まれている必要がある。
        /// 再生中に呼ぶ必要がある。再生中でない場合、空振りする。
        /// </summary>
        /// <param name="id">曲番号</param>
        public void UpdatePlayPcmDataById(int id) {
            WasapiIO_SetNowPlayingPcmDataId(id);
        }

        public bool SetupCaptureBuffer(long bytes) {
            return WasapiIO_SetupCaptureBuffer(bytes);
        }

        public long GetCapturedData(byte[] data) {
            return WasapiIO_GetCapturedData(data, data.LongLength);
        }

        public long GetCaptureGlitchCount() {
            return WasapiIO_GetCaptureGlitchCount();
        }

        public int StartPlayback(int wavDataId) {
            return WasapiIO_StartPlayback(wavDataId);
        }

        public int StartRecording() {
            return WasapiIO_StartRecording();
        }

        public bool Run(int millisec) {
            return WasapiIO_Run(millisec);
        }

        public void Stop() {
            WasapiIO_Stop();
        }

        public int Pause() {
            return WasapiIO_Pause();
        }

        public int Unpause() {
            return WasapiIO_Unpause();
        }

        public bool SetPosFrame(long v) {
            return WasapiIO_SetPosFrame(v);
        }

        public class SessionStatus {
            public StreamType StreamType { get; set; }
            public int PcmDataSampleRate { get; set; }
            public int DeviceSampleRate { get; set; }
            public SampleFormatType DeviceSampleFormat { get; set; }
            public int DeviceBytesPerFrame { get; set; }
            public int DeviceNumChannels { get; set; }
            public int TimePeriodHandledNanosec { get; set; }
            public int EndpointBufferFrameNum { get; set; }

            public SessionStatus(StreamType streamType, int pcmDataSampleRate, int deviceSampleRate, SampleFormatType deviceSampleFormat,
                    int deviceBytesPerFrame, int deviceNumChannels, int timePeriodHandledNanosec, int bufferFrameNum) {
                StreamType = streamType;
                PcmDataSampleRate = pcmDataSampleRate;
                DeviceSampleRate = deviceSampleRate;
                DeviceSampleFormat = deviceSampleFormat;
                DeviceBytesPerFrame = deviceBytesPerFrame;
                DeviceNumChannels = deviceNumChannels;
                TimePeriodHandledNanosec = timePeriodHandledNanosec;
                EndpointBufferFrameNum = bufferFrameNum;
            }
        };

        public SessionStatus GetSessionStatus() {
            var s = new WasapiIoSessionStatus();
            if (!WasapiIO_GetSessionStatus(out s)) {
                return null;
            }
            return new SessionStatus((StreamType)s.streamType, s.pcmDataSampleRate, s.deviceSampleRate, (SampleFormatType)s.deviceSampleFormat,
                    s.deviceBytesPerFrame, s.deviceNumChannels, s.timePeriodHandledNanosec, s.bufferFrameNum);
        }

        public class CursorLocation {
            public long PosFrame { get; set; }
            public long TotalFrameNum { get; set; }
            public CursorLocation(long posFrame, long totalFrameNum) {
                PosFrame = posFrame;
                TotalFrameNum = totalFrameNum;
            }
        };

        public CursorLocation GetPlayCursorPosition(PcmDataUsageType usageType) {
            var p = new WasapiIoCursorLocation();
            if (!WasapiIO_GetPlayCursorPosition((int)usageType, out p)) {
                return null;
            }
            return new CursorLocation(p.posFrame, p.totalFrameNum);
        }
    }
}
