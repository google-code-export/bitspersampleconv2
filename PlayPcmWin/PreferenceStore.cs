using WasapiPcmUtil;

namespace PlayPcmWin {
    public class Preference : WWXmlRW.SaveLoadContents {
        // SaveLoadContents IF
        public int GetCurrentVersion() { return CurrentVersion; }
        public int GetVersion() { return Version; }

        public const int DefaultLatencyMilliseconds = 170;
        public const int CurrentVersion = 3;

        public int Version { get; set; }

        public int LatencyMillisec { get; set; }
        public WasapiSharedOrExclusive wasapiSharedOrExclusive { get; set; }
        public WasapiDataFeedMode wasapiDataFeedMode { get; set; }
        public RenderThreadTaskType renderThreadTaskType { get; set; }
        public BitsPerSampleFixType bitsPerSampleFixType { get; set; }

        public bool ReplaceGapWithKokomade { get; set; }

        public string PreferredDeviceName { get; set; }
        public string PreferredDeviceIdString { get; set; }

        public double MainWindowLeft { get; set; }
        public double MainWindowTop { get; set; }
        public double MainWindowWidth { get; set; }
        public double MainWindowHeight { get; set; }

        public bool ManuallySetMainWindowDimension { get; set; }
        public bool ParallelRead { get; set; }
        public bool PlayRepeat { get; set; }
        public bool PlayAllTracks { get; set; }
        public int PlayingTimeSize { get; set; }
        public bool PlayingTimeFontBold { get; set; }
        public string PlayingTimeFontName { get; set; }
        public double WindowScale { get; set; }
        public bool SettingsIsExpanded { get; set; }
        public bool StorePlaylistContent { get; set; }
        public bool DispCoverart { get; set; }
        public bool RefrainRedraw { get; set; }
        public bool Shuffle { get; set; }
        public int ZeroFlushMillisec { get; set; }
        public int TimePeriodMillisec { get; set; }

        public enum PlayListDispModeType {
            /// <summary>
            /// 選択モード
            /// </summary>
            Select,
            /// <summary>
            /// 項目編集モード
            /// </summary>
            EditItem,
        }

        public Preference() {
            Reset();
        }

        /// <summary>
        /// デフォルト設定値。
        /// </summary>
        public void Reset() {
            Version = CurrentVersion;
            LatencyMillisec = DefaultLatencyMilliseconds;
            wasapiSharedOrExclusive = WasapiSharedOrExclusive.Exclusive;
            wasapiDataFeedMode = WasapiDataFeedMode.EventDriven;
            renderThreadTaskType = RenderThreadTaskType.ProAudio;
            bitsPerSampleFixType = BitsPerSampleFixType.AutoSelect;
            PreferredDeviceName = "";
            PreferredDeviceIdString = "";
            ReplaceGapWithKokomade = false;
            ManuallySetMainWindowDimension = true;
            ParallelRead = false;
            PlayRepeat = true;
            PlayAllTracks = true;
            Shuffle = false;
            PlayingTimeSize = 16;
            PlayingTimeFontBold = true;
            PlayingTimeFontName = "Courier New";
            WindowScale = 1.0f;
            SettingsIsExpanded = true;
            StorePlaylistContent = true;
            DispCoverart = true;
            RefrainRedraw = false;
            ZeroFlushMillisec = 500;
            TimePeriodMillisec = 1;

            MainWindowLeft = -1;
            MainWindowTop = -1;
            MainWindowWidth = 1000;
            MainWindowHeight = 640;
        }

        /// <summary>
        /// ウィンドウサイズセット。
        /// </summary>
        public void SetMainWindowLeftTopWidthHeight(
                double left, double top,
                double width, double height) {
            MainWindowLeft   = left;
            MainWindowTop    = top;
            MainWindowWidth  = width;
            MainWindowHeight = height;
        }
    }

    sealed class PreferenceStore {
        private static readonly string m_fileName = "PlayPcmWinPreference.xml";

        private PreferenceStore() {
        }

        public static Preference Load() {
            var xmlRW = new WWXmlRW.XmlRW<Preference>(m_fileName);

            Preference p = xmlRW.Load();

            // (読み込んだ値が都合によりサポートされていない場合、このタイミングでロード後に強制的に上書き出来る)

            return p;
        }

        public static bool Save(Preference p) {
            var xmlRW = new WWXmlRW.XmlRW<Preference>(m_fileName);
            return xmlRW.Save(p);
        }
    }
}
