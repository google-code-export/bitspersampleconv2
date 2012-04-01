using System.Text;
using System.Runtime.InteropServices;

namespace Wasapi {
    public class WasapiCS {
        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Init();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_Term();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetSchedulerTaskType(int t);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetShareMode(int sm);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetDataFeedMode(int dfm);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetLatencyMillisec(int ms);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_DoDeviceEnumeration(int deviceType);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetDeviceCount();

        [DllImport("WasapiIODLL.dll", CharSet = CharSet.Auto)]
        private extern static bool
        WasapiIO_GetDeviceName(int id, System.Text.StringBuilder name, int nameBytes);

        [DllImport("WasapiIODLL.dll", CharSet = CharSet.Auto)]
        private extern static bool
        WasapiIO_GetDeviceIdString(int id, System.Text.StringBuilder idStr, int idStrBytes);

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
        private extern static int
        WasapiIO_GetUseDeviceId();

        [DllImport("WasapiIODLL.dll", CharSet = CharSet.Auto)]
        private extern static bool
        WasapiIO_GetUseDeviceName(System.Text.StringBuilder name, int nameBytes);

        [DllImport("WasapiIODLL.dll", CharSet = CharSet.Auto)]
        private extern static bool
        WasapiIO_GetUseDeviceIdString(System.Text.StringBuilder idStr, int idStrBytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Setup(int sampleRate, int format, int numChannels);

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
        private extern static bool
        WasapiIO_AddPlayPcmDataEnd();

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
        WasapiIO_GetNowPlayingPcmDataId();

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
        WasapiIO_Start(int wavDataId);

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
        private extern static long
        WasapiIO_GetPosFrame();

        [DllImport("WasapiIODLL.dll")]
        private extern static long
        WasapiIO_GetTotalFrameNum();

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_SetPosFrame(long v);
        
        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetMixFormatSampleRate();

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetMixFormatType();

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetPcmDataSampleRate();

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetPcmDataFrameBytes();

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetPcmDataNumChannels();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetZeroFlushMillisec(int millisec);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetTimePeriodMillisec(int millisec);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Unicode)]
        public delegate void StateChangedCallback(StringBuilder idStr);

        [DllImport("WasapiIODLL.dll")]
        public static extern void WasapiIO_RegisterCallback(StateChangedCallback callback);

        public int Init() {
            return WasapiIO_Init();
        }

        public void Term() {
            WasapiIO_Term();
        }

        public void RegisterCallback(StateChangedCallback callback) {
            WasapiIO_RegisterCallback(callback);
        }

        public enum SchedulerTaskType {
            None,
            Audio,
            ProAudio,
            Playback
        };

        public void SetSchedulerTaskType(SchedulerTaskType t) {
            WasapiIO_SetSchedulerTaskType((int)t);
        }

        public enum ShareMode {
            Shared,
            Exclusive
        };

        public void SetShareMode(ShareMode t) {
            WasapiIO_SetShareMode((int)t);
        }

        public enum DataFeedMode {
            EventDriven,
            TimerDriven,
        };

        public void SetDataFeedMode(DataFeedMode t) {
            WasapiIO_SetDataFeedMode((int)t);
        }

        public void SetLatencyMillisec(int ms) {
            WasapiIO_SetLatencyMillisec(ms);
        }

        public enum DeviceType {
            Play,
            Rec
        };

        public enum BitFormatType {
            SInt,
            SFloat
        };

        public int DoDeviceEnumeration(DeviceType t) {
            return WasapiIO_DoDeviceEnumeration((int)t);
        }

        public int GetDeviceCount() {
            return WasapiIO_GetDeviceCount();
        }

        public string GetDeviceName(int id) {
            StringBuilder buf = new StringBuilder(64);
            WasapiIO_GetDeviceName(id, buf, buf.Capacity*2);
            return buf.ToString();
        }

        public string GetDeviceIdString(int id) {
            StringBuilder buf = new StringBuilder(256);
            WasapiIO_GetDeviceIdString(id, buf, buf.Capacity * 2);
            return buf.ToString();
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

        public int GetUseDeviceId() {
            return WasapiIO_GetUseDeviceId();
        }

        public string GetUseDeviceName() {
            StringBuilder buf = new StringBuilder(64);
            WasapiIO_GetUseDeviceName(buf, buf.Capacity * 2);
            return buf.ToString();
        }

        public string GetUseDeviceIdString()
        {
            StringBuilder buf = new StringBuilder(256);
            WasapiIO_GetUseDeviceIdString(buf, buf.Capacity * 2);
            return buf.ToString();
        }

        public enum SampleFormatType {
            Unknown = -1,
            Sint16,
            Sint24,
            Sint32V24,
            Sint32,
            Sfloat,
        };

        /// <summary>
        /// サンプルフォーマットタイプ→メモリ上に占めるバイト数(1サンプル1chあたり)
        /// </summary>
        /// <param name="t">サンプルフォーマットタイプ</param>
        /// <returns>メモリ上に占めるバイト数(1サンプル1chあたり)</returns>
        public static int SampleFormatTypeToUseBytesPerSample(SampleFormatType t) {
            switch (t) {
            case SampleFormatType.Sint16: return 2;
            case SampleFormatType.Sint24: return 3;
            case SampleFormatType.Sint32V24: return 4;
            case SampleFormatType.Sint32: return 4;
            case SampleFormatType.Sfloat: return 4;
            default:
                System.Diagnostics.Debug.Assert(false);
                return 0;
            }
        }

        public int Setup(int sampleRate, SampleFormatType format, int numChannels) {
            return WasapiIO_Setup(sampleRate, (int)format, numChannels);
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

        public int GetNowPlayingPcmDataId() {
            return WasapiIO_GetNowPlayingPcmDataId();
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

        public int Start(int wavDataId) {
            return WasapiIO_Start(wavDataId);
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

        public long GetPosFrame() {
            return WasapiIO_GetPosFrame();
        }

        public long GetTotalFrameNum() {
            return WasapiIO_GetTotalFrameNum();
        }

        public bool SetPosFrame(long v) {
            return WasapiIO_SetPosFrame(v);
        }

        public int GetBufferFormatSampleRate() {
            return WasapiIO_GetMixFormatSampleRate();
        }

        public int GetNumOfChannels() {
            return WasapiIO_GetPcmDataNumChannels();
        }

        public SampleFormatType GetBufferFormatType() {
            return (SampleFormatType)WasapiIO_GetMixFormatType();
        }

        public struct PcmFormat {
            public int sampleRate;
            public SampleFormatType sampleFormat;
            public int numChannels;
            public int frameBytes;
        }

        /// <summary>
        /// @todo WasapiIOから、構造体を引き揚げられるようにする
        /// </summary>
        /// <returns></returns>
        public PcmFormat GetPcmFormat() {
            PcmFormat pf = new PcmFormat();
            pf.sampleRate = WasapiIO_GetPcmDataSampleRate();
            pf.sampleFormat = (SampleFormatType)WasapiIO_GetMixFormatType();
            pf.numChannels = WasapiIO_GetPcmDataNumChannels();
            pf.frameBytes = WasapiIO_GetPcmDataFrameBytes();
            return pf;
        }

        public void SetZeroFlushMillisec(int millisec) {
            WasapiIO_SetZeroFlushMillisec(millisec);
        }

        public void SetTimePeriodMillisec(int millisec) {
            WasapiIO_SetTimePeriodMillisec(millisec);
        }
    }
}
