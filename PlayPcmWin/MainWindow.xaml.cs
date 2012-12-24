﻿// このファイルは、
// 行き当たりばったりで機能を追加していったため
// PlayPcmWinの中で特にイマイチなコードである

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wasapi;
using WavRWLib2;
using System.IO;
using System.ComponentModel;
using PcmDataLib;
using WasapiPcmUtil;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Controls.Primitives;
using System.Globalization;

namespace PlayPcmWin
{
    public sealed partial class MainWindow : Window
    {
        /// <summary>
        /// 再生の進捗状況を取りに行き表示を更新する時間間隔
        /// </summary>
        const int PROGRESS_REPORT_INTERVAL_MS = 500;

        private const string PLAYING_TIME_UNKNOWN = "--:--:--/--:--:--";
        private const string PLAYING_TIME_ALLZERO = "00:00:00/00:00:00";

        /// <summary>
        /// アルバムのカバーアート画像のファイル名
        /// </summary>
        private readonly string[] ALBUM_IMAGE_FILENAMES = {
            "folder.jpg",
            "cover.jpg",
        };

        /// <summary>
        /// 共有モードの音量制限。
        /// </summary>
        const double SHARED_MAX_AMPLITUDE = 0.98;

        private WasapiCS wasapi;

        private Wasapi.WasapiCS.StateChangedCallback m_wasapiStateChangedDelegate;

        private Preference m_preference = new Preference();

        class PlayListColumnInfo {
            public string Name { get; set; }
            public DataGridLength Width { get; set; }
            public PlayListColumnInfo(string name, DataGridLength width) {
                Name = name;
                Width = width;
            }
        };
        private PlayListColumnInfo[] m_playlistColumnDefaults =
        {
            new PlayListColumnInfo("Title", DataGridLength.Auto),
            new PlayListColumnInfo("Duration", DataGridLength.Auto),
            new PlayListColumnInfo("Artist", DataGridLength.Auto),
            new PlayListColumnInfo("AlbumTitle", DataGridLength.Auto),
            new PlayListColumnInfo("SampleRate", DataGridLength.Auto),

            new PlayListColumnInfo("QuantizationBitRate", DataGridLength.Auto),
            new PlayListColumnInfo("NumChannels", DataGridLength.SizeToCells),
            new PlayListColumnInfo("BitRate", DataGridLength.Auto),
            new PlayListColumnInfo("IndexNr", DataGridLength.SizeToCells),
            new PlayListColumnInfo("ReadSeparaterAfter", DataGridLength.SizeToCells)
        };



        class DeviceInfo
        {
            public int Idx { get; set; }
            public string Name { get; set; }
            public string DeviceIdStr { get; set; }

            public DeviceInfo(int idx, string name, string deviceIdStr)
            {
                Idx = idx;
                Name = name;
                DeviceIdStr = deviceIdStr;
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}: {1}", Idx, Name);
            }
        }


        /// <summary>
        /// PcmDataの表示用リスト。
        /// </summary>
        private List<PcmDataLib.PcmData> m_pcmDataListForDisp = new List<PcmDataLib.PcmData>();

        /// <summary>
        /// PcmDataの再生用リスト。(通常は表示用リストと同じ。シャッフルの時は順番が入れ替わる)
        /// </summary>
        private List<PcmDataLib.PcmData> m_pcmDataListForPlay = new List<PcmDataLib.PcmData>();

        /// <summary>
        /// プレイリスト1項目の情報。
        /// dataGridPlayList.Itemsの項目と一対一に対応する。
        /// </summary>
        class PlayListItemInfo : INotifyPropertyChanged {
            private static int mNextRowId = 1;
            private PcmDataLib.PcmData mPcmData;
            private bool mReadSeparatorAfter;
            private int mRowId;

            public static void SetNextRowId(int id) {
                mNextRowId = id;
            }

            public int RowId {
                get {
                    return mRowId;
                }
                set {
                    mRowId = value;
                }
            }

            public string Id {
                get {
                    return mPcmData.Id.ToString(CultureInfo.CurrentCulture);
                }
            }

            public string Title {
                get {
                    return mPcmData.DisplayName;
                }
                set {
                    mPcmData.DisplayName = value;
                }
            }

            public string ArtistName {
                get {
                    return mPcmData.ArtistName;
                }
                set {
                    mPcmData.ArtistName = value;
                }
            }

            public string AlbumTitle {
                get {
                    return mPcmData.AlbumTitle;
                }
                set {
                    mPcmData.AlbumTitle = value;
                }
            }

            /// <summary>
            /// 長さ表示用文字列
            /// </summary>
            public string Duration {
                get {
                    return SecondsToHMSString(mPcmData.DurationSeconds);
                }
            }

            public int NumChannels {
                get {
                    return mPcmData.NumChannels;
                }
            }

            public int IndexNr {
                get {
                    return mPcmData.CueSheetIndex;
                }
            }

            public string SampleRate {
                get {
                    return string.Format(CultureInfo.CurrentCulture, "{0}kHz", mPcmData.SampleRate * 0.001);
                }
            }

            public string QuantizationBitRate {
                get {
                    if (mPcmData.SampleValueRepresentationType == PcmDataLib.PcmData.ValueRepresentationType.SFloat) {
                        return mPcmData.BitsPerSample.ToString(CultureInfo.CurrentCulture)
                                + " bit (" + Properties.Resources.FloatingPointNumbers + ")";
                    }
                    return mPcmData.BitsPerSample.ToString(CultureInfo.CurrentCulture) + " bit";
                }
            }

            public string BitRate {
                get {
                    return ((long)mPcmData.BitsPerSample * mPcmData.SampleRate * mPcmData.NumChannels / 1000)
                            .ToString(CultureInfo.CurrentCulture) + " kbps";
                }
            }

            public PcmDataLib.PcmData PcmData() { return mPcmData; }

            public bool ReadSeparaterAfter {
                get { return mReadSeparatorAfter; }
                set {
                    mReadSeparatorAfter = value;
                    OnPropertyChanged("ReadSeparaterAfter");
                }
            }

            public PlayListItemInfo(PcmDataLib.PcmData pcmData) {
                mPcmData = pcmData;
                mRowId = mNextRowId++;
            }

            #region INotifyPropertyChanged members

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            #endregion
        }

        /// <summary>
        /// プレイリスト項目情報。
        /// </summary>
        private ObservableCollection<PlayListItemInfo> m_playListItems = new ObservableCollection<PlayListItemInfo>();

        private BackgroundWorker m_playWorker;
        private BackgroundWorker m_readFileWorker;
        private System.Diagnostics.Stopwatch m_sw = new System.Diagnostics.Stopwatch();
        private bool m_playListMouseDown = false;

        // 次にプレイリストにAddしたファイルに振られるGroupId。
        private int m_groupIdNextAdd = 0;

        /// <summary>
        /// メモリ上に読み込まれているGroupId。
        /// </summary>
        private int m_loadedGroupId = -1;
        
        /// <summary>
        /// PCMデータ読み込み中グループIDまたは読み込み完了したグループID
        /// </summary>
        private int m_loadingGroupId = -1;


        /// <summary>
        /// デバイスのセットアップ情報
        /// </summary>
        struct DeviceSetupInfo {
            bool setuped;
            int samplingRate;
            WasapiCS.SampleFormatType sampleFormat;
            int latencyMillisec;
            int zeroFlushMillisec;
            WasapiDataFeedModeType dfm;
            WasapiSharedOrExclusiveType shareMode;
            RenderThreadTaskType threadTaskType;

            public int SampleRate { get { return samplingRate; } }
            public WasapiCS.SampleFormatType SampleFormat { get { return sampleFormat; } }
            public int NumChannels { get; set; }
            public int LatencyMillisec { get { return latencyMillisec; } }
            public int ZeroFlushMillisec { get { return zeroFlushMillisec; } }
            public WasapiDataFeedModeType DataFeedMode { get { return dfm; } }
            public WasapiSharedOrExclusiveType SharedOrExclusive { get { return shareMode; } }
            public RenderThreadTaskType ThreadTaskType { get { return threadTaskType; } }
            public int ResamplerConversionQuality { get; set; }

            /// <summary>
            /// 1フレーム(1サンプル全ch)のデータがメモリ上を占める領域(バイト)
            /// </summary>
            public int UseBytesPerFrame {
                get {
                    return NumChannels * WasapiCS.SampleFormatTypeToUseBytesPerSample(sampleFormat);
                }
            }

            public bool Is(
                    int samplingRate,
                    WasapiCS.SampleFormatType fmt,
                    int numChannels,
                    int latencyMillisec,
                    int zeroFlushMillisec,
                    WasapiDataFeedModeType dfm,
                    WasapiSharedOrExclusiveType shareMode,
                    RenderThreadTaskType threadTaskType,
                    int resamplerConversionQuality) {
                return (this.setuped
                    && this.samplingRate == samplingRate
                    && this.sampleFormat == fmt
                    && this.NumChannels == numChannels
                    && this.latencyMillisec == latencyMillisec
                    && this.zeroFlushMillisec == zeroFlushMillisec
                    && this.dfm == dfm
                    && this.shareMode == shareMode
                    && this.threadTaskType == threadTaskType
                    && this.ResamplerConversionQuality == resamplerConversionQuality);
            }

            public bool CompatibleTo(
                    int samplingRate,
                    WasapiCS.SampleFormatType fmt,
                    int numChannels,
                    int latencyMillisec,
                    int zeroFlushMillisec,
                    WasapiDataFeedModeType dfm,
                    WasapiSharedOrExclusiveType shareMode,
                    RenderThreadTaskType threadTaskType,
                    int resamplerConversionQuality) {
                return (this.setuped
                    && this.samplingRate == samplingRate
                    && SampleFormatIsCompatible(this.sampleFormat, fmt)
                    && this.NumChannels == numChannels
                    && this.latencyMillisec == latencyMillisec
                    && this.ZeroFlushMillisec == zeroFlushMillisec
                    && this.dfm == dfm
                    && this.shareMode == shareMode
                    && this.threadTaskType == threadTaskType
                    && this.ResamplerConversionQuality == resamplerConversionQuality);
            }

            private static bool SampleFormatIsCompatible(
                    WasapiCS.SampleFormatType lhs,
                    WasapiCS.SampleFormatType rhs) {
                switch (lhs) {
                case WasapiCS.SampleFormatType.Sint24:
                case WasapiCS.SampleFormatType.Sint32V24:
                    return rhs == WasapiCS.SampleFormatType.Sint24 ||
                        rhs == WasapiCS.SampleFormatType.Sint32V24;
                default:
                    return lhs == rhs;
                }
            }

            public void Set(int samplingRate,
                WasapiCS.SampleFormatType fmt,
                int numChannels,
                int latencyMillisec,
                int zeroFlushMillisec,
                WasapiDataFeedModeType dfm,
                WasapiSharedOrExclusiveType shareMode,
                RenderThreadTaskType threadTaskType,
                int resamplerConversionQuality) {
                    this.setuped = true;
                this.samplingRate = samplingRate;
                this.sampleFormat = fmt;
                this.NumChannels = numChannels;
                this.latencyMillisec = latencyMillisec;
                this.zeroFlushMillisec = zeroFlushMillisec;
                this.dfm = dfm;
                this.shareMode = shareMode;
                this.threadTaskType = threadTaskType;
                this.ResamplerConversionQuality = resamplerConversionQuality;
            }

            /// <summary>
            /// wasapi.Unsetup()された場合に呼ぶ。
            /// </summary>
            public void Unsetuped() {
                setuped = false;
            }

            /// <summary>
            /// Setup状態か？
            /// </summary>
            /// <returns>true: Setup状態。false: Setupされていない。</returns>
            public bool IsSetuped() {
                return setuped;
            }
        }

        /// <summary>
        /// デバイスSetup情報。サンプリングレート、量子化ビット数…。
        /// </summary>
        DeviceSetupInfo m_deviceSetupInfo = new DeviceSetupInfo();

        // 再生停止完了後に行うタスク。
        enum TaskType {
            /// <summary>
            /// 停止する。
            /// </summary>
            None,

            /// <summary>
            /// 指定されたグループをメモリに読み込み、グループの先頭の項目を再生開始する。
            /// </summary>
            PlaySpecifiedGroup,
        }

        class Task {
            public Task() {
                Type = TaskType.None;
                GroupId = -1;
                WavDataId = -1;
            }

            public Task(TaskType type) {
                Set(type);
            }

            public Task(TaskType type, int groupId, int wavDataId) {
                Set(type, groupId, wavDataId);
            }

            public void Set(TaskType type) {
                // 現時点で、このSet()のtypeはNoneしかありえない。
                System.Diagnostics.Debug.Assert(type == TaskType.None);
                Type = type;
            }

            public void Set(TaskType type, int groupId, int wavDataId) {
                Type = type;
                GroupId = groupId;
                WavDataId = wavDataId;
            }

            public TaskType Type { get; set; }
            public int GroupId { get; set; }
            public int WavDataId { get; set; }
        };

        Task m_task = new Task();

        enum State {
            未初期化,
            初期化完了,
            プレイリストあり,

            // これ以降の状態にいる場合、再生リストに新しいファイルを追加できない。
            デバイスSetup完了,
            ファイル読み込み完了,
            再生中,
            再生一時停止中,
            再生停止開始,
            再生グループ切り替え中,
        }

        /// <summary>
        /// UIの状態。
        /// </summary>
        private State m_state = State.未初期化;

        private void ChangeState(State nowState) {
            m_state = nowState;
        }

        /// <summary>
        /// 再生グループId==groupIdの先頭のファイルのWavDataIdを取得。O(n)
        /// </summary>
        /// <param name="groupId">再生グループId</param>
        /// <returns>再生グループId==groupIdの先頭のファイルのWavDataId。見つからないときは-1</returns>
        private static int GetFirstWavDataIdOnGroup(List<PcmData> pcmDataList, int groupId) {
            for (int i = 0; i < pcmDataList.Count(); ++i) {
                if (pcmDataList[i].GroupId == groupId) {
                    return pcmDataList[i].Id;
                }
            }

            return -1;
        }

        /// <summary>
        /// 指定された再生グループIdに属するWavDataの数を数える。O(n)
        /// </summary>
        /// <param name="groupId">指定された再生グループId</param>
        /// <returns>WavDataの数。1つもないときは0</returns>
        private static int CountWaveDataOnPlayGroup(List<PcmData> pcmDataList, int groupId) {
            int count = 0;
            for (int i = 0; i < pcmDataList.Count(); ++i) {
                if (pcmDataList[i].GroupId == groupId) {
                    ++count;
                }
            }

            return count;
        }

        /// <summary>
        /// 指定されたWavDataIdの、プレイリスト位置番号(プレイリスト内のindex)を戻す。
        /// </summary>
        /// <param name="wavDataId">プレイリスト位置番号を知りたいWaveDataのId</param>
        /// <returns>プレイリスト位置番号(プレイリスト内のindex)。見つからないときは-1</returns>
        private int GetPlayListIndexOfWaveDataId(int wavDataId) {
            for (int i = 0; i < m_playListItems.Count(); ++i) {
                if (m_playListItems[i].PcmData() != null
                    && m_playListItems[i].PcmData().Id == wavDataId) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 保存してあった再生リストを読んでm_pcmDataListとm_playListItemsに足す。
        /// UpdateUIは行わない。
        /// </summary>
        private int ReadPpwPlaylist(string path) {
            int count = 0;

            PlaylistSave pl;
            if (path.Length == 0) {
                pl = PpwPlaylistRW.Load();
            } else {
                pl = PpwPlaylistRW.LoadFrom(path);
            }
            foreach (var p in pl.Items) {
                int rv = ReadFileHeader(p.PathName, ReadHeaderMode.OnlyConcreteFile, null);
                if (1 == rv) {
                    // 読み込み成功。読み込んだPcmDataの曲名、アーティスト名、アルバム名、startTick等を上書きする。

                    // pcmDataのメンバ。
                    var pcmData = m_pcmDataListForDisp.Last();
                    pcmData.DisplayName = p.Title;
                    pcmData.AlbumTitle = p.AlbumName;
                    pcmData.ArtistName = p.ArtistName;
                    pcmData.StartTick = p.StartTick;
                    pcmData.EndTick = p.EndTick;
                    pcmData.CueSheetIndex = p.CueSheetIndex;

                    // playList表のメンバ。
                    var playListItem = m_playListItems[count];
                    playListItem.ReadSeparaterAfter = p.ReadSeparaterAfter;
                }
                count += rv;
            }

            return count;
        }

        private bool SavePpwPlaylist(string path) {
            var s = new PlaylistSave();

            for (int i=0; i<m_pcmDataListForDisp.Count; ++i) {
                var p = m_pcmDataListForDisp[i];
                var playListItem = m_playListItems[i];

                s.Add(new PlaylistItemSave().Set(
                    p.DisplayName, p.AlbumTitle, p.ArtistName, p.FullPath,
                    p.CueSheetIndex, p.StartTick, p.EndTick, playListItem.ReadSeparaterAfter));
            }

            if (path.Length == 0) {
                return PpwPlaylistRW.Save(s);
            } else {
                return PpwPlaylistRW.SaveAs(s, path);
            }
        }

        /// <summary>
        /// true: slider is dragging
        /// </summary>
        private bool mSliderSliding = false;

        /// ///////////////////////////////////////////////////////////////////////////////////////////////////

        public MainWindow()
        {
            InitializeComponent();

            SetLocalizedTextToUI();

            this.AddHandler(Slider.MouseLeftButtonDownEvent,
                new MouseButtonEventHandler(slider1_MouseLeftButtonDown), true);
            this.AddHandler(Slider.MouseLeftButtonUpEvent,
                new MouseButtonEventHandler(slider1_MouseLeftButtonUp), true);

            // InitializeComponent()によって、チェックボックスのチェックイベントが発生し
            // m_preferenceの内容が変わるので、InitializeComponent()の後にロードする。

            m_preference = PreferenceStore.Load();

            if (m_preference.ManuallySetMainWindowDimension) {
                // 記録されているウィンドウ形状が、一部分でも画面に入っていたらOKとする
                var windowRect = new System.Drawing.Rectangle(
                    (int)m_preference.MainWindowLeft,
                    (int)m_preference.MainWindowTop,
                    (int)m_preference.MainWindowWidth,
                    (int)m_preference.MainWindowHeight);

                bool inScreen = false;
                foreach (var screen in System.Windows.Forms.Screen.AllScreens) {
                    if (!System.Drawing.Rectangle.Intersect(windowRect, screen.Bounds).IsEmpty) {
                        inScreen = true;
                        break;
                    }
                }
                if (inScreen) {
                    Left   = m_preference.MainWindowLeft;
                    Top    = m_preference.MainWindowTop;
                    if (100 <= m_preference.MainWindowWidth) {
                        Width = m_preference.MainWindowWidth;
                    }
                    if (100 <= m_preference.MainWindowHeight) {
                        Height = m_preference.MainWindowHeight;
                    }
                }
            }

            if (!m_preference.SettingsIsExpanded) {
                expanderSettings.IsExpanded = false;
            }

            // 再生リスト読み出し

            PlayListItemInfo.SetNextRowId(1);
            m_groupIdNextAdd = 0;

            if (m_preference.StorePlaylistContent) {
                // display error MessageBox on Window_Loaded()
                m_loadErrorMessages = new StringBuilder();
                ReadPpwPlaylist(string.Empty);
            }

            RestorePlaylistColumnOrderFromPreference();

            AddLogText(string.Format(CultureInfo.InvariantCulture, "PlayPcmWin {0} {1}\r\n",
                    AssemblyVersion,
                    IntPtr.Size == 8 ? "64bit" : "32bit"));

            dataGridPlayList.ItemsSource = m_playListItems;

            if (0 <= m_preference.LastPlayItemIndex &&
                    m_preference.LastPlayItemIndex < dataGridPlayList.Items.Count) {
                dataGridPlayList.SelectedIndex = m_preference.LastPlayItemIndex;
                dataGridPlayList.ScrollIntoView(dataGridPlayList.SelectedItem);
            }

            int hr = 0;
            wasapi = new WasapiCS();
            hr = wasapi.Init();
            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.Init() {0:X8}\r\n", hr));

            m_wasapiStateChangedDelegate = new Wasapi.WasapiCS.StateChangedCallback(WasapiStatusChanged);
            wasapi.RegisterCallback(m_wasapiStateChangedDelegate);

            textBoxLatency.Text = string.Format(CultureInfo.InvariantCulture, "{0}", m_preference.LatencyMillisec);

            switch (m_preference.WasapiSharedOrExclusive) {
            case WasapiSharedOrExclusiveType.Exclusive:
                radioButtonExclusive.IsChecked = true;
                break;
            case WasapiSharedOrExclusiveType.Shared:
                radioButtonShared.IsChecked = true;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            switch (m_preference.WasapiDataFeedMode) {
            case WasapiDataFeedModeType.EventDriven:
                radioButtonEventDriven.IsChecked = true;
                break;
            case WasapiDataFeedModeType.TimerDriven:
                radioButtonTimerDriven.IsChecked = true;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            UpdatePlaymodeComboBoxFromPreference();

            SetupBackgroundWorkers();

            UpdateDeviceList();
            UpdateWindowSettings();

            {
                // slider1のTrackをクリックしてThumbがクリック位置に移動した時Thumbがつままれた状態になるようにする
                slider1.ApplyTemplate();
                (slider1.Template.FindName("PART_Track", slider1) as Track).Thumb.MouseEnter += new MouseEventHandler((sender, e) => {
                    if (e.LeftButton == MouseButtonState.Pressed && e.MouseDevice.Captured == null) {
                        var args = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left);
                        args.RoutedEvent = MouseLeftButtonDownEvent;
                        (sender as Thumb).RaiseEvent(args);
                    }
                });
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            // Showing error MessageBox must be delayed until Window Loaded state because SplashScreen closes all MessageBoxes whose owner is DesktopWindow
            if (null != m_loadErrorMessages && 0 < m_loadErrorMessages.Length) {
                MessageBox.Show(m_loadErrorMessages.ToString(), Properties.Resources.RestoreFailedFiles, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            m_loadErrorMessages = null;
        }

        /// <summary>
        /// 再生モードコンボボックスの項目
        /// </summary>
        enum ComboBoxPlayModeType {
            AllTracks,
            AllTracksRepeat,
            OneTrack,
            OneTrackRepeat,
            Shuffle,
            ShuffleRepeat,
            NUM
        };

        private void SetLocalizedTextToUI() {
            comboBoxPlayMode.Items.Clear();
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeAllTracks);
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeAllTracksRepeat);
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeOneTrack);
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeOneTrackRepeat);
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeShuffle);
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeShuffleRepeat);

            cmenuPlayListClear.Header = Properties.Resources.MainCMenuPlaylistClear;
            cmenuPlayListEditMode.Header = Properties.Resources.MainCMenuPlayListEditMode;

            menuFile.Header = Properties.Resources.MenuFile;
            menuItemFileNew.Header = Properties.Resources.MenuItemFileNew;
            menuItemFileOpen.Header = Properties.Resources.MenuItemFileOpen;
            menuItemFileSaveAs.Header = Properties.Resources.MenuItemFileSaveAs;
            menuItemFileSaveCueAs.Header = Properties.Resources.MenuItemFileSaveCueAs;
            menuItemFileExit.Header = Properties.Resources.MenuItemFileExit;

            menuTool.Header = Properties.Resources.MenuTool;
            menuItemToolSettings.Header = Properties.Resources.MenuItemToolSettings;

            menuPlayList.Header = Properties.Resources.MenuPlayList;
            menuItemPlayListClear.Header = Properties.Resources.MenuItemPlayListClear;
            menuItemPlayListItemEditMode.Header = Properties.Resources.MenuItemPlayListItemEditMode;

            menuHelp.Header = Properties.Resources.MenuHelp;
            menuItemHelpAbout.Header = Properties.Resources.MenuItemHelpAbout;
            menuItemHelpWeb.Header = Properties.Resources.MenuItemHelpWeb;

            groupBoxLog.Header = Properties.Resources.MainGroupBoxLog;
            groupBoxOutputDevices.Header = Properties.Resources.MainGroupBoxOutputDevices;
            groupBoxPlaybackControl.Header = Properties.Resources.MainGroupBoxPlaybackControl;
            groupBoxPlaylist.Header = Properties.Resources.MainGroupBoxPlaylist;
            groupBoxWasapiDataFeedMode.Header = Properties.Resources.MainGroupBoxWasapiDataFeedMode;

            groupBoxWasapiOperationMode.Header = Properties.Resources.MainGroupBoxWasapiOperationMode;
            groupBoxWasapiOutputLatency.Header = Properties.Resources.MainGroupBoxWasapiOutputLatency;
            groupBoxWasapiSettings.Header = Properties.Resources.MainGroupBoxWasapiSettings;

            buttonClearPlayList.Content = Properties.Resources.MainButtonClearPlayList;
            buttonDelistSelected.Content = Properties.Resources.MainButtonDelistSelected;
            buttonInspectDevice.Content = Properties.Resources.MainButtonInspectDevice;
            buttonNext.Content = Properties.Resources.MainButtonNext;
            buttonPause.Content = Properties.Resources.MainButtonPause;

            buttonPlay.Content = Properties.Resources.MainButtonPlay;
            buttonPrev.Content = Properties.Resources.MainButtonPrev;
            buttonSettings.Content = Properties.Resources.MainButtonSettings;
            buttonStop.Content = Properties.Resources.MainButtonStop;

            radioButtonEventDriven.Content = Properties.Resources.MainRadioButtonEventDriven;
            radioButtonExclusive.Content = Properties.Resources.MainRadioButtonExclusive;
            radioButtonShared.Content = Properties.Resources.MainRadioButtonShared;
            radioButtonTimerDriven.Content = Properties.Resources.MainRadioButtonTimerDriven;

            expanderSettings.Header = Properties.Resources.MainExpanderSettings;

            dataGridColumnAlbumTitle.Header = Properties.Resources.MainDataGridColumnAlbumTitle;
            dataGridColumnArtist.Header = Properties.Resources.MainDataGridColumnArtist;
            dataGridColumnBitRate.Header = Properties.Resources.MainDataGridColumnBitRate;
            dataGridColumnDuration.Header = Properties.Resources.MainDataGridColumnDuration;
            dataGridColumnIndexNr.Header = Properties.Resources.MainDataGridColumnIndexNr;

            dataGridColumnNumChannels.Header = Properties.Resources.MainDataGridColumnNumChannels;
            dataGridColumnQuantizationBitRate.Header = Properties.Resources.MainDataGridColumnQuantizationBitRate;
            dataGridColumnReadSeparaterAfter.Header = Properties.Resources.MainDataGridColumnReadSeparaterAfter;
            dataGridColumnSampleRate.Header = Properties.Resources.MainDataGridColumnSampleRate;
            dataGridColumnTitle.Header = Properties.Resources.MainDataGridColumnTitle;

        }

        private bool IsPlayModeAllTracks() {
            ComboBoxPlayModeType t = (ComboBoxPlayModeType)comboBoxPlayMode.SelectedIndex;
            return t == ComboBoxPlayModeType.AllTracks
                || t == ComboBoxPlayModeType.AllTracksRepeat;
        }

        private bool IsPlayModeOneTrack() {
            ComboBoxPlayModeType t = (ComboBoxPlayModeType)comboBoxPlayMode.SelectedIndex;
            return t == ComboBoxPlayModeType.OneTrack
                || t == ComboBoxPlayModeType.OneTrackRepeat;
        }

        private bool IsPlayModeShuffle() {
            ComboBoxPlayModeType t = (ComboBoxPlayModeType)comboBoxPlayMode.SelectedIndex;
            return t == ComboBoxPlayModeType.Shuffle
                || t == ComboBoxPlayModeType.ShuffleRepeat;
        }

        private bool IsPlayModeRepeat() {
            ComboBoxPlayModeType t = (ComboBoxPlayModeType)comboBoxPlayMode.SelectedIndex;
            return t == ComboBoxPlayModeType.AllTracksRepeat
                || t == ComboBoxPlayModeType.OneTrackRepeat
                || t == ComboBoxPlayModeType.ShuffleRepeat;
        }

        private void UpdatePlaymodeComboBoxFromPreference() {
            if (m_preference.Shuffle) {
                 if (m_preference.PlayRepeat) {
                     comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.ShuffleRepeat;
                 } else {
                     comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.Shuffle;
                 }
            } else if (m_preference.PlayAllTracks) {
                if (m_preference.PlayRepeat) {
                    comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.AllTracksRepeat;
                } else {
                    comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.AllTracks;
                }
            } else {
                if (m_preference.PlayRepeat) {
                    comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.OneTrackRepeat;
                } else {
                    comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.OneTrack;
                }
            }
        }

        private void SetPreferencePlaymodeFromComboBox() {
            m_preference.PlayAllTracks = IsPlayModeAllTracks();
            m_preference.PlayRepeat    = IsPlayModeRepeat();
            m_preference.Shuffle       = IsPlayModeShuffle();
        }

        // 再生リストの列の順番設定の保存
        private void SavePlaylistColumnOrderToPreference() {
            var idxNameTable = new Dictionary<int, string>();
            int i=0;
            foreach (var item in dataGridPlayList.Columns) {
                idxNameTable.Add(item.DisplayIndex, m_playlistColumnDefaults[i].Name);
                ++i;
            }

            m_preference.PlayListColumnsOrder.Clear();
            foreach (var item in idxNameTable.OrderBy(x => x.Key)) {
                m_preference.PlayListColumnsOrder.Add(item.Value);
            }
        }

        // 再生リストの列の順番を設定から読み出し適用する
        private bool RestorePlaylistColumnOrderFromPreference() {
            var nameIdxTable = new Dictionary<string, int>();
            {
                int i=0;
                foreach (var item in m_preference.PlayListColumnsOrder) {
                    nameIdxTable.Add(item, i);
                    ++i;
                }
            }
            var columnIdxList = new List<int>();
            foreach (var item in m_playlistColumnDefaults) {
                int idx;
                if (!nameIdxTable.TryGetValue(item.Name, out idx)) {
                    Console.WriteLine("E: unknown playlist column name {0}", item);
                    return false;
                }
                columnIdxList.Add(idx);
            }

            if (columnIdxList.Count != dataGridPlayList.Columns.Count) {
                Console.WriteLine("E: playlist column count mismatch {0}", columnIdxList.Count);
                return false;
            }

            {
                int i=0;
                foreach (var item in dataGridPlayList.Columns) {
                    item.DisplayIndex = columnIdxList[i];
                    ++i;
                }
            }
            return true;
        }

        private void SetupBackgroundWorkers() {
            m_readFileWorker = new BackgroundWorker();
            m_readFileWorker.DoWork += new DoWorkEventHandler(ReadFileDoWork);
            m_readFileWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ReadFileRunWorkerCompleted);
            m_readFileWorker.WorkerReportsProgress = true;
            m_readFileWorker.ProgressChanged += new ProgressChangedEventHandler(ReadFileWorkerProgressChanged);
            m_readFileWorker.WorkerSupportsCancellation = true;

            m_playWorker = new BackgroundWorker();
            m_playWorker.WorkerReportsProgress = true;
            m_playWorker.DoWork += new DoWorkEventHandler(PlayDoWork);
            m_playWorker.ProgressChanged += new ProgressChangedEventHandler(PlayProgressChanged);
            m_playWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(PlayRunWorkerCompleted);
            m_playWorker.WorkerSupportsCancellation = true;
        }

        private void Window_Closed(object sender, EventArgs e) {
            Term();
        }

        private void MenuItemFileExit_Click(object sender, RoutedEventArgs e) {
            Exit();
        }

        private static string AssemblyVersion {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        private static string SampleFormatTypeToStr(WasapiCS.SampleFormatType t) {
            switch (t) {
            case WasapiCS.SampleFormatType.Sfloat:
                return "32bit"+Properties.Resources.FloatingPointNumbers;
            case WasapiCS.SampleFormatType.Sint16:
                return "16bit";
            case WasapiCS.SampleFormatType.Sint24:
                return "24bit";
            case WasapiCS.SampleFormatType.Sint32:
                return "32bit";
            case WasapiCS.SampleFormatType.Sint32V24:
                return "32bit("+Properties.Resources.ValidBits + "=24)";
            default:
                System.Diagnostics.Debug.Assert(false);
                return "unknown";
            }
        }

        private void DispCoverart(byte[] pictureData) {

            if (null == pictureData || pictureData.Length <= 0) {
                imageCoverArt.Source = null;
                // imageCoverArt.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            try {
                using (var stream = new MemoryStream(pictureData)) {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = null;
                    bi.StreamSource = stream;
                    bi.EndInit();

                    imageCoverArt.Source = bi;
                    // imageCoverArt.Visibility = System.Windows.Visibility.Visible;
                }
            } catch (IOException ex) {
                Console.WriteLine("D: DispCoverart {0}", ex);
                imageCoverArt.Source = null;
            } catch (System.IO.FileFormatException ex) {
                Console.WriteLine("D: DispCoverart {0}", ex);
                imageCoverArt.Source = null;
            }
        }

        private void UpdateCoverart() {
            if (!m_preference.DispCoverart) {
                // do not display coverart
                imageCoverArt.Source = null;
                imageCoverArt.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            // display coverart

            imageCoverArt.Visibility = System.Windows.Visibility.Visible;

            if (dataGridPlayList.SelectedIndex < 0) {
                DispCoverart(null);
                return;
            }
            PcmDataLib.PcmData w = m_playListItems[dataGridPlayList.SelectedIndex].PcmData();
            if (null != w && 0 < w.PictureBytes) {
                DispCoverart(w.PictureData);
            } else {
                DispCoverart(null);
            }
        }

        // 初期状態。再生リストなし。
        private void UpdateUIToInitialState() {
            cmenuPlayListClear.IsEnabled     = false;
            cmenuPlayListEditMode.IsEnabled  = true;
            menuItemFileNew.IsEnabled        = false;
            menuItemFileOpen.IsEnabled       = true;
            menuItemFileSaveAs.IsEnabled     = false;
            menuItemFileSaveCueAs.IsEnabled  = false;
            menuItemPlayListClear.IsEnabled = false;
            menuItemPlayListItemEditMode.IsEnabled = true;
            
            buttonPlay.IsEnabled             = false;
            buttonStop.IsEnabled             = false;
            buttonPause.IsEnabled            = false;
            comboBoxPlayMode.IsEnabled       = true;

            buttonNext.IsEnabled             = false;
            buttonPrev.IsEnabled             = false;
            groupBoxWasapiSettings.IsEnabled = true;

            buttonClearPlayList.IsEnabled    = false;
            buttonDelistSelected.IsEnabled = false;
            buttonInspectDevice.IsEnabled    = true;

            buttonSettings.IsEnabled = true;
            menuItemToolSettings.IsEnabled = true;
        }

        // 再生リストあり。再生していない状態。
        private void UpdateUIToEditableState() {
            cmenuPlayListClear.IsEnabled = true;
            cmenuPlayListEditMode.IsEnabled = true;
            menuItemFileNew.IsEnabled = true;
            menuItemFileOpen.IsEnabled = true;
            menuItemFileSaveAs.IsEnabled = true;
            menuItemFileSaveCueAs.IsEnabled = true;
            menuItemPlayListClear.IsEnabled = true;
            menuItemPlayListItemEditMode.IsEnabled = true;
            buttonPlay.IsEnabled = true;
            buttonStop.IsEnabled = false;
            buttonPause.IsEnabled = false;
            comboBoxPlayMode.IsEnabled = true;

            buttonNext.IsEnabled = false;
            buttonPrev.IsEnabled = false;
            groupBoxWasapiSettings.IsEnabled = true;

            buttonClearPlayList.IsEnabled = true;
            buttonDelistSelected.IsEnabled = (dataGridPlayList.SelectedIndex >= 0);
            buttonInspectDevice.IsEnabled = false;

            buttonSettings.IsEnabled = true;
            menuItemToolSettings.IsEnabled = true;
        }

        // 再生リストあり。再生開始処理中。
        private void UpdateUIToNonEditableState() {
            cmenuPlayListClear.IsEnabled = false;
            cmenuPlayListEditMode.IsEnabled = false;
            menuItemFileNew.IsEnabled = false;
            menuItemFileOpen.IsEnabled = false;
            menuItemFileSaveAs.IsEnabled = false;
            menuItemFileSaveCueAs.IsEnabled = false;
            menuItemPlayListClear.IsEnabled = false;
            menuItemPlayListItemEditMode.IsEnabled = false;
            buttonPlay.IsEnabled = false;
            buttonStop.IsEnabled = false;
            buttonPause.IsEnabled = false;
            comboBoxPlayMode.IsEnabled = false;

            buttonNext.IsEnabled = false;
            buttonPrev.IsEnabled = false;
            groupBoxWasapiSettings.IsEnabled = false;

            buttonClearPlayList.IsEnabled = false;
            buttonDelistSelected.IsEnabled = false;
            buttonInspectDevice.IsEnabled = false;

            buttonSettings.IsEnabled = false;
            menuItemToolSettings.IsEnabled = false;
        }

        // 再生中。
        private void UpdateUIToPlayingState() {
            cmenuPlayListClear.IsEnabled = false;
            cmenuPlayListEditMode.IsEnabled = false;
            menuItemFileNew.IsEnabled = false;
            menuItemFileOpen.IsEnabled = false;
            menuItemFileSaveAs.IsEnabled = false;
            menuItemFileSaveCueAs.IsEnabled = false;
            menuItemPlayListClear.IsEnabled = false;
            menuItemPlayListItemEditMode.IsEnabled = false;
            buttonPlay.IsEnabled = false;
            buttonStop.IsEnabled = true;
            buttonPause.IsEnabled = true;
            buttonPause.Content = Properties.Resources.MainButtonPause;
            comboBoxPlayMode.IsEnabled = false;

            buttonNext.IsEnabled = true;
            buttonPrev.IsEnabled = true;
            groupBoxWasapiSettings.IsEnabled = false;

            buttonClearPlayList.IsEnabled = false;
            buttonDelistSelected.IsEnabled = false;
            buttonInspectDevice.IsEnabled = false;

            buttonSettings.IsEnabled = false;
            menuItemToolSettings.IsEnabled = false;
        }

        private void UpdateUIStatus() {
            dataGridPlayList.UpdateLayout();
            UpdateCoverart();

            if (m_preference.RefrainRedraw) {
                // 再描画抑制モード
                slider1.IsEnabled = false;
                labelPlayingTime.Content = PLAYING_TIME_UNKNOWN;
            } else {
                slider1.IsEnabled = true;
                labelPlayingTime.Content = PLAYING_TIME_ALLZERO;
            }

            switch (m_state) {
            case State.初期化完了:
                UpdateUIToInitialState();
                statusBarText.Content = Properties.Resources.MainStatusPleaseCreatePlaylist;
                break;
            case State.プレイリストあり:
                if (0 < dataGridPlayList.Items.Count &&
                        dataGridPlayList.SelectedIndex < 0) {
                    // プレイリストに項目があり、選択されている曲が存在しない時、最初の曲を選択状態にする
                    dataGridPlayList.SelectedIndex = 0;
                }

                UpdateUIToEditableState();
                statusBarText.Content = Properties.Resources.MainStatusPressPlayButton;
                break;
            case State.デバイスSetup完了:
                // 一覧のクリアーとデバイスの選択、再生リストの作成関連を押せなくする。
                UpdateUIToNonEditableState();
                statusBarText.Content = Properties.Resources.MainStatusReadingFiles;
                break;
            case State.ファイル読み込み完了:
                UpdateUIToNonEditableState();
                statusBarText.Content = Properties.Resources.MainStatusReadCompleted;

                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                slider1.Value = 0;
                labelPlayingTime.Content = PLAYING_TIME_UNKNOWN;
                break;
            case State.再生中:
                UpdateUIToPlayingState();
                statusBarText.Content =
                    string.Format(CultureInfo.InvariantCulture, "{0} WASAPI{1} {2}kHz {3} {4}ch",
                        Properties.Resources.MainStatusPlaying,
                        radioButtonShared.IsChecked == true ?
                            Properties.Resources.Shared : Properties.Resources.Exclusive,
                        wasapi.GetDeviceSampleRate()*0.001,
                        SampleFormatTypeToStr(wasapi.GetDeviceSampleFormat()),
                        wasapi.GetDeviceNumChannels());

                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                break;
            case State.再生一時停止中:
                UpdateUIToPlayingState();
                buttonPause.Content = Properties.Resources.MainButtonResume;
                buttonNext.IsEnabled             = false;
                buttonPrev.IsEnabled             = false;
                statusBarText.Content = Properties.Resources.MainStatusPaused;

                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                break;
            case State.再生停止開始:
                UpdateUIToNonEditableState();
                statusBarText.Content = Properties.Resources.MainStatusStopping;
                break;
            case State.再生グループ切り替え中:
                UpdateUIToNonEditableState();
                statusBarText.Content = Properties.Resources.MainStatusChangingPlayGroup;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            // 再生リストモード更新
            if (dataGridPlayList.IsReadOnly) {
                // 再生モード
                menuItemPlayListItemEditMode.IsChecked = false;
                cmenuPlayListEditMode.IsChecked = false;
            } else {
                // 編集モード
                menuItemPlayListItemEditMode.IsChecked = true;
                cmenuPlayListEditMode.IsChecked = true;
            }

            if (m_preference.AlternatingRowBackground) {
                dataGridPlayList.AlternatingRowBackground
                    = new SolidColorBrush(Util.ColorFromArgb(
                        m_preference.AlternatingRowBackgroundArgb));
            } else {
                dataGridPlayList.AlternatingRowBackground = null;
            }
        }

        /// <summary>
        /// デバイス一覧を取得し、デバイス一覧リストを更新する。
        /// 同一デバイスのデバイス番号がずれるので注意。
        /// </summary>
        private void UpdateDeviceList() {
            int hr;

            int selectedIndex = -1;

            listBoxDevices.Items.Clear();

            hr = wasapi.DoDeviceEnumeration(WasapiCS.DeviceType.Play);
            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.DoDeviceEnumeration(Play) {0:X8}\r\n", hr));

            int nDevices = wasapi.GetDeviceCount();
            for (int i = 0; i < nDevices; ++i) {
                string deviceName = wasapi.GetDeviceName(i);
                string deviceIdStr = wasapi.GetDeviceIdString(i);

                listBoxDevices.Items.Add(new DeviceInfo(i, deviceName, deviceIdStr));

                if (0 < m_preference.PreferredDeviceName.Length
                        && 0 == string.CompareOrdinal(m_preference.PreferredDeviceName, deviceName)) {
                    // PreferredDeviceIdStringは3.0.60で追加されたので、存在しないことがある
                    // 存在するときだけチェックする
                    if (0 < m_preference.PreferredDeviceIdString.Length
                            && 0 != string.CompareOrdinal(m_preference.PreferredDeviceIdString, deviceIdStr)) {
                        continue;
                    }

                    // お気に入りデバイスを選択状態にする。
                    selectedIndex = i;
                }
            }

            if (0 < nDevices) {
                if (0 <= selectedIndex && selectedIndex < listBoxDevices.Items.Count) {
                    listBoxDevices.SelectedIndex = selectedIndex;
                } else {
                    listBoxDevices.SelectedIndex = 0;
                }

                buttonInspectDevice.IsEnabled = true;
            }

            if (0 < m_pcmDataListForDisp.Count) {
                ChangeState(State.プレイリストあり);
            } else {
                ChangeState(State.初期化完了);
            }

            UpdateUIStatus();
        }

        /// <summary>
        /// 再生中の場合は、停止を開始する。
        /// (ブロックしないのでこの関数から抜けたときに停止完了していないことがある)
        /// 
        /// 再生中でない場合は、再生停止後イベントtaskAfterStopをここで実行する。
        /// 再生中の場合は、停止完了後にtaskAfterStopを実行する。
        /// </summary>
        /// <param name="taskAfterStop"></param>
        void StopAsync(Task taskAfterStop) {
            m_task = taskAfterStop;

            if (m_playWorker.IsBusy) {
                m_playWorker.CancelAsync();
                // 再生停止したらPlayRunWorkerCompletedでイベントを開始する。
            } else {
                // 再生停止後イベントをここで、いますぐ開始。
                PerformPlayCompletedTask();
            }
        }

        void StopBlocking()
        {
            StopAsync(new Task(TaskType.None));
            m_readFileWorker.CancelAsync();

            // バックグラウンドスレッドにjoinして、完全に止まるまで待ち合わせする。
            while (m_playWorker.IsBusy)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new System.Threading.ThreadStart(delegate { }));

                System.Threading.Thread.Sleep(100);
            }

            while (m_readFileWorker.IsBusy)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new System.Threading.ThreadStart(delegate { }));
                System.Threading.Thread.Sleep(100);
            }
        }

        /// <summary>
        /// デバイス選択を解除する。再生停止中に呼ぶ必要あり。
        /// この関数を呼ぶと、デバイスリストが消えるため要注意。
        /// ふたたびCreateDeviceList()する必要あり。
        /// </summary>
        private void DeviceDeselect() {
            System.Diagnostics.Debug.Assert(!m_playWorker.IsBusy);

            UnsetupDevice();

            if (wasapi != null) {
                wasapi.UnchooseDevice();
                AddLogText("wasapi.UnchooseDevice()\r\n");
            }

            m_loadedGroupId = -1;
            m_loadingGroupId = -1;
        }

        private void Term() {
            if (wasapi != null) {
                // バックグラウンドスレッドにjoinして、完全に止まるまで待ち合わせするブロッキング版のStopを呼ぶ。
                // そうしないと、バックグラウンドスレッドによって使用中のオブジェクトが
                // この後のUnsetupの呼出によって開放されてしまい問題が起きる。
                StopBlocking();
                UnsetupDevice();
                wasapi.Term();
                wasapi = null;

                // ウィンドウの位置とサイズを保存
                m_preference.SetMainWindowLeftTopWidthHeight(
                    Left,
                    Top,
                    Width,
                    Height);

                // 再生リピート設定を保存
                SetPreferencePlaymodeFromComboBox();

                // 設定画面の表示状態を保存
                m_preference.SettingsIsExpanded = expanderSettings.IsExpanded;

                // 再生リストの列の並び順を覚える
                SavePlaylistColumnOrderToPreference();

                // 最後に再生していた曲の番号
                m_preference.LastPlayItemIndex = dataGridPlayList.SelectedIndex;

                // 設定ファイルを書き出す。
                PreferenceStore.Save(m_preference);

                // 再生リストをIsolatedStorageに保存。
                SavePpwPlaylist(string.Empty);
            }
        }

        private void Exit() {
            Term();
            // Application.Current.Shutdown();
            Close();
        }

        /// <summary>
        /// wasapi.Unsetupを行う。
        /// 既にUnsetup状態の場合は、空振りする。
        /// </summary>
        private void UnsetupDevice() {
            if (!m_deviceSetupInfo.IsSetuped()) {
                return;
            }

            wasapi.Unsetup();
            AddLogText("wasapi.Unsetup()\r\n");
            m_deviceSetupInfo.Unsetuped();
        }

        /// <summary>
        /// m_deviceSetupInfoにしたがってWasapiをSetupする。
        /// </summary>
        /// <returns>WasapiのSetup HRESULT。</returns>
        private int WasapiSetup1() {
            wasapi.SetShareMode(
                PreferenceShareModeToWasapiCSShareMode(
                    m_deviceSetupInfo.SharedOrExclusive));
            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.SetShareMode({0})\r\n",
                m_deviceSetupInfo.SharedOrExclusive));

            wasapi.SetSchedulerTaskType(
                PreferenceSchedulerTaskTypeToWasapiCSSchedulerTaskType(
                    m_deviceSetupInfo.ThreadTaskType));
            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.SetSchedulerTaskType({0})\r\n",
                m_deviceSetupInfo.ThreadTaskType));

            wasapi.SetDataFeedMode(PreferenceDataFeedModeToWasapiCS(m_deviceSetupInfo.DataFeedMode));
            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.SetDataFeedMode({0})\r\n",
                PreferenceDataFeedModeToWasapiCS(m_deviceSetupInfo.DataFeedMode)));

            wasapi.SetLatencyMillisec(m_deviceSetupInfo.LatencyMillisec);
            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.SetLatencyMillisec({0})\r\n",
                m_deviceSetupInfo.LatencyMillisec));

            wasapi.SetZeroFlushMillisec(m_deviceSetupInfo.ZeroFlushMillisec);
            wasapi.SetTimePeriodHundredNanosec(m_preference.TimePeriodHundredNanosec);

            wasapi.SetResamplerConversionQuality(m_preference.ResamplerConversionQuality);

            int hr = wasapi.Setup(
                m_deviceSetupInfo.SampleRate,
                m_deviceSetupInfo.SampleFormat,
                m_deviceSetupInfo.NumChannels);
            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.Setup({0} {1} {2}) {3:X8}\r\n",
                m_deviceSetupInfo.SampleRate,
                m_deviceSetupInfo.SampleFormat,
                m_deviceSetupInfo.NumChannels, hr));
            if (hr < 0) {
                UnsetupDevice();

                string s = string.Format(CultureInfo.InvariantCulture, "wasapi.Setup({0} {1} {2} {3} {4} {5}) failed {6:X8}\r\n",
                    m_deviceSetupInfo.SampleRate,
                    m_deviceSetupInfo.SampleFormat,
                    m_deviceSetupInfo.NumChannels,
                    m_deviceSetupInfo.LatencyMillisec,
                    m_deviceSetupInfo.DataFeedMode,
                    ShareModeToStr(m_preference.WasapiSharedOrExclusive), hr);
                AddLogText(s);
                return hr;
            }
            return hr;
        }

        private static int PcmChannelsToSetupChannels(int numChannels) {
            switch (numChannels) {
            case 1:
                return 2; //< モノラル1chのPCMデータはMonoToStereo()によってステレオ2chに変換してから再生する。
            default:
                return numChannels;
            }
        }

        /// <summary>
        /// デバイスSetupを行う。
        /// すでに同一フォーマットのSetupがなされている場合は空振りする。
        /// </summary>
        /// <param name="loadGroupId">再生するグループ番号。この番号のWAVファイルのフォーマットでSetupする。</param>
        /// <returns>false: デバイスSetup失敗。よく起こる。</returns>
        private bool SetupDevice(int loadGroupId) {
            
            int latencyMillisec = 0;
            if (!Int32.TryParse(textBoxLatency.Text, NumberStyles.Number,
                    CultureInfo.CurrentCulture, out latencyMillisec) || latencyMillisec <= 0) {
                latencyMillisec = Preference.DefaultLatencyMilliseconds;
                textBoxLatency.Text = string.Format(CultureInfo.InvariantCulture, "{0}", latencyMillisec);
            }
            m_preference.LatencyMillisec = latencyMillisec;

            int startWavDataId = GetFirstWavDataIdOnGroup(m_pcmDataListForPlay, loadGroupId);
            System.Diagnostics.Debug.Assert(0 <= startWavDataId);

            PcmDataLib.PcmData startPcmData = FindPcmDataById(m_pcmDataListForPlay, startWavDataId);

            // 1つのフォーマットに対して複数のSetup()設定選択肢がありうる。

            int candidateNum = SampleFormatInfo.GetSetupSampleFormatCandidateNum(
                m_preference.WasapiSharedOrExclusive,
                m_preference.BitsPerSampleFixType,
                startPcmData.ValidBitsPerSample,
                startPcmData.SampleValueRepresentationType);
            for (int i = 0; i < candidateNum; ++i) {
                SampleFormatInfo sf = SampleFormatInfo.CreateSetupSampleFormat(
                    m_preference.WasapiSharedOrExclusive,
                    m_preference.BitsPerSampleFixType,
                    startPcmData.BitsPerSample,
                    startPcmData.ValidBitsPerSample,
                    startPcmData.SampleValueRepresentationType,
                    i);

                if (m_deviceSetupInfo.Is(
                    startPcmData.SampleRate,
                    sf.GetSampleFormatType(),
                    PcmChannelsToSetupChannels(startPcmData.NumChannels),
                    latencyMillisec,
                    m_preference.ZeroFlushMillisec,
                    m_preference.WasapiDataFeedMode,
                    m_preference.WasapiSharedOrExclusive,
                    m_preference.RenderThreadTaskType,
                    m_preference.ResamplerConversionQuality)) {
                    // すでにこのフォーマットでSetup完了している。
                    return true;
                }
            }

            for (int i = 0; i < candidateNum; ++i) {
                SampleFormatInfo sf = SampleFormatInfo.CreateSetupSampleFormat(
                    m_preference.WasapiSharedOrExclusive,
                    m_preference.BitsPerSampleFixType,
                    startPcmData.BitsPerSample,
                    startPcmData.ValidBitsPerSample,
                    startPcmData.SampleValueRepresentationType, i);

                m_deviceSetupInfo.Set(
                    startPcmData.SampleRate,
                    sf.GetSampleFormatType(),
                    PcmChannelsToSetupChannels(startPcmData.NumChannels),
                    latencyMillisec,
                    m_preference.ZeroFlushMillisec,
                    m_preference.WasapiDataFeedMode,
                    m_preference.WasapiSharedOrExclusive,
                    m_preference.RenderThreadTaskType,
                    m_preference.ResamplerConversionQuality);

                int hr = WasapiSetup1();
                if (0 <= hr) {
                    // 成功
                    break;
                }

                // 失敗
                if (i == (candidateNum-1)) {
                    string s = string.Format(CultureInfo.InvariantCulture, "{0}: wasapi.Setup({1}Hz {2} {3}ch {4} {5}ms {6} {7}) {8} {9:X8}\n\n{10}",
                        Properties.Resources.Error,
                        startPcmData.SampleRate,
                        sf.GetSampleFormatType(),
                        PcmChannelsToSetupChannels(startPcmData.NumChannels),
                        Properties.Resources.Latency,

                        latencyMillisec,
                        DfmToStr(m_preference.WasapiDataFeedMode),
                        ShareModeToStr(m_preference.WasapiSharedOrExclusive),
                        Properties.Resources.Failed,
                        hr,
                        Properties.Resources.SetupFailAdvice);
                    MessageBox.Show(s);
                    return false;
                }
            }

            ChangeState(State.デバイスSetup完了);
            UpdateUIStatus();
            return true;
        }

        enum PlayListClearMode {
            // プレイリストをクリアーし、UI状態も更新する。(通常はこちらを使用。)
            ClearWithUpdateUI,

            // ワーカースレッドから呼ぶためUIを操作しない。UIは内部状態とは矛盾した状態になるため
            // この後UIスレッドであらためてClearPlayList(ClearWithUpdateUI)する必要あり。
            ClearWithoutUpdateUI,
        }

        private StringBuilder m_loadErrorMessages;

        private void LoadErrorMessageAdd(string s) {
            s = "*" + s.TrimEnd('\n').TrimEnd('\r') + ". ";
            m_loadErrorMessages.Append(s);
        }

        private void ClearPlayList(PlayListClearMode mode) {
            m_pcmDataListForDisp.Clear();
            m_pcmDataListForPlay.Clear();
            m_playListItems.Clear();
            PlayListItemInfo.SetNextRowId(1);

            wasapi.ClearPlayList();

            m_groupIdNextAdd = 0;
            m_loadedGroupId = -1;
            m_loadingGroupId = -1;

            GC.Collect();

            ChangeState(State.初期化完了);

            if (mode == PlayListClearMode.ClearWithUpdateUI) {
                //m_playListView.RefreshCollection();

                progressBar1.Value = 0;
                UpdateUIStatus();

                // 再生リスト列幅を初期値にリセットする。
                {
                    int i=0;
                    foreach (var item in dataGridPlayList.Columns) {
                        item.Width = DataGridLength.SizeToCells;
                        item.Width = m_playlistColumnDefaults[i].Width;
                        ++i;
                    }
                }
            }
        }

        /// <summary>
        /// ファイルを最初から最後まで全部読む。
        /// </summary>
        private static byte[] ReadWholeFile(string path) {
            byte[] result = new byte[0];

            if (System.IO.File.Exists(path)) {
                // ファイルが存在する。
                using (var br = new BinaryReader(
                        File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))) {
                    if (br.BaseStream.Length <= 0x7fffffff) {
                        result = br.ReadBytes((int)br.BaseStream.Length);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// カバーアート画像を追加する。
        /// </summary>
        /// <returns>true: カバーアート画像が付いている。false: カバーアート画像がついていない。</returns>
        private bool AddCoverart(string path, PcmDataLib.PcmData pcmData) {
            if (0 < pcmData.PictureBytes) {
                // 既に追加されている。
                return true;
            }

            var dirPath = System.IO.Path.GetDirectoryName(path);

            var pictureData = ReadWholeFile(string.Format(CultureInfo.InvariantCulture, "{0}\\{1}.jpg", dirPath,
                System.IO.Path.GetFileNameWithoutExtension(path)));
            if (0 < pictureData.Length) {
                // ファイル名.jpgが存在。
                pcmData.SetPicture(pictureData.Length, pictureData);
                return true;
            }

            foreach (string albumImageFilename in ALBUM_IMAGE_FILENAMES) {
                pictureData = ReadWholeFile(string.Format(CultureInfo.InvariantCulture, "{0}\\{1}", dirPath, albumImageFilename));
                if (0 < pictureData.Length) {
                    // アルバムのカバーアート画像(folder.jpg等)が存在。
                    pcmData.SetPicture(pictureData.Length, pictureData);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// サブルーチン
        /// PcmData読み込み成功後に行う処理。
        /// FLACとWAVとAIFFで共通。
        /// </summary>
        private bool CheckAddPcmData(PlaylistTrackInfo plti, string path, PcmDataLib.PcmData pcmData) {
            if (31 < pcmData.NumChannels) {
                string s = string.Format(CultureInfo.InvariantCulture, "{0}: {1} {2}ch\r\n",
                    Properties.Resources.TooManyChannels,
                    path, pcmData.NumChannels);
                AddLogText(s);
                LoadErrorMessageAdd(s);
                return false;
            }

            if (pcmData.BitsPerSample != 16
             && pcmData.BitsPerSample != 24
             && pcmData.BitsPerSample != 32) {
                string s = string.Format(CultureInfo.InvariantCulture, "{0}: {1} {2}bit\r\n",
                    Properties.Resources.NotSupportedQuantizationBitRate,
                    path, pcmData.BitsPerSample);
                AddLogText(s);
                LoadErrorMessageAdd(s);
                return false;
            }

            if (0 < m_pcmDataListForDisp.Count
                && !m_pcmDataListForDisp[m_pcmDataListForDisp.Count - 1].IsSameFormat(pcmData)) {
                /* データフォーマットが変わった。
                 * Setupのやり直しになるのでファイルグループ番号を変える。
                 */
                ++m_groupIdNextAdd;
            }

            pcmData.FullPath = path;
            pcmData.FileName = System.IO.Path.GetFileName(path);
            pcmData.Id = m_pcmDataListForDisp.Count();
            pcmData.Ordinal = pcmData.Id;
            pcmData.GroupId = m_groupIdNextAdd;
            // PCMファイルにタイトル名が埋め込まれていない時、ファイル名をタイトル名にする。
            if (pcmData.DisplayName == null || pcmData.DisplayName.Length == 0) {
                pcmData.DisplayName = pcmData.FileName;
            }

            // CUEシートの情報をセットする。
            if (null == plti) {
                // startTickとendTickは、既にセットされていることもあるので、ここではセットしない。
                // pcmData.StartTick = 0;
                // pcmData.EndTick = -1;
                // pcmData.CueSheetIndex = 1;
            } else {
                pcmData.StartTick = plti.startTick;
                pcmData.EndTick = plti.endTick;
                pcmData.CueSheetIndex = plti.indexId;

                // 再生リストにタイトル名情報がある時は、再生リストのタイトル名をタイトル名にする。
                if (plti.title != null && 0 < plti.title.Length) {
                    pcmData.DisplayName = plti.title;
                }

                if (plti.performer != null && 0 < plti.performer.Length) {
                    pcmData.ArtistName = plti.performer;
                }
                if (plti.albumTitle != null && 0 < plti.albumTitle.Length) {
                    pcmData.AlbumTitle = plti.albumTitle;
                }
            }

            // カバーアート画像を追加する
            AddCoverart(path, pcmData);

            var pli = new PlayListItemInfo(pcmData);
            
            if (plti != null) {
                pli.ReadSeparaterAfter = plti.readSeparatorAfter;
            }
            pli.PropertyChanged += new PropertyChangedEventHandler(PlayListItemInfoPropertyChanged);
            m_pcmDataListForDisp.Add(pcmData);
            m_playListItems.Add(pli);

            //m_playListView.RefreshCollection();

            // 状態の更新。再生リストにファイル有り。
            ChangeState(State.プレイリストあり);
            return true;
        }

        private void HandleFileReadException(string path, Exception ex) {
            string s = string.Format(CultureInfo.InvariantCulture, Properties.Resources.ReadFileFailed + "\r\n{0}\r\n\r\n{1}", "WAV", path, ex);
            AddLogText(s);
            LoadErrorMessageAdd(string.Format(CultureInfo.InvariantCulture, Properties.Resources.ReadFileFailed + ": {1}", "WAV", path));
        }

        /// <summary>
        /// WAVファイルのヘッダ部分を読み込む。
        /// </summary>
        /// <returns>読めたらtrue</returns>
        private bool ReadWavFileHeader(string path, PlaylistTrackInfo plti)
        {
            bool result = false;

            WavData wavData = new WavData();
            try {
                using (BinaryReader br = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))) {
                    if (wavData.ReadHeader(br)) {
                        // WAVヘッダ読み込み成功。PcmDataを作って再生リストに足す。

                        PcmDataLib.PcmData pd = new PcmDataLib.PcmData();
                        pd.SetFormat(wavData.NumChannels, wavData.BitsPerSample, wavData.ValidBitsPerSample,
                            wavData.SampleRate, wavData.SampleValueRepresentationType, wavData.NumFrames);
                        if ("RIFFINFO_INAM".Equals(wavData.Title) &&
                            "RIFFINFO_IART".Equals(wavData.ArtistName)) {
                            // Issue 79 workaround
                        } else {
                            if (wavData.Title != null) {
                                pd.DisplayName = wavData.Title;
                            }
                            if (wavData.AlbumName != null) {
                                pd.AlbumTitle = wavData.AlbumName;
                            }
                            if (wavData.ArtistName != null) {
                                pd.ArtistName = wavData.ArtistName;
                            }
                        }
                        result = CheckAddPcmData(plti, path, pd);
                    } else {
                        string s = string.Format(CultureInfo.InvariantCulture, Properties.Resources.ReadFileFailed + ": {1}\r\n",
                            "WAV", path);
                        AddLogText(s);
                        LoadErrorMessageAdd(s);
                    }
                }
            } catch (IOException ex) {
                HandleFileReadException(path, ex);
            } catch (ArgumentException ex) {
                HandleFileReadException(path, ex);
            } catch (UnauthorizedAccessException ex) {
                HandleFileReadException(path, ex);
            }

            return result;
        }

        /// <summary>
        /// AIFFファイルのヘッダ部分を読み込む。
        /// </summary>
        /// <returns>読めたらtrue</returns>
        private bool ReadAiffFileHeader(string path, PlaylistTrackInfo plti) {
            bool result = false;

            AiffReader ar = new AiffReader();
            try {
                using (BinaryReader br = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))) {
                    PcmDataLib.PcmData pd;
                    AiffReader.ResultType aiffResult = ar.ReadHeader(br, out pd);
                    if (aiffResult == AiffReader.ResultType.Success) {
                        if (CheckAddPcmData(plti, path, pd)) {
                            result = true;
                        }
                    } else {
                        string s = string.Format(CultureInfo.InvariantCulture, Properties.Resources.ReadFileFailed + " {1}: {2}\r\n", "AIFF", aiffResult, path);
                        AddLogText(s);
                        LoadErrorMessageAdd(s);
                    }
                }
            } catch (IOException ex) {
                HandleFileReadException(path, ex);
            } catch (ArgumentException ex) {
                HandleFileReadException(path, ex);
            } catch (UnauthorizedAccessException ex) {
                HandleFileReadException(path, ex);
            }

            return result;
        }

        /// <summary>
        /// FLACファイルのヘッダ部分を読み込む。
        /// </summary>
        /// <returns>読めたらtrue</returns>
        private bool ReadFlacFileHeader(string path, ReadHeaderMode mode, PlaylistTrackInfo plti) {
            bool readResult = false;
            PcmDataLib.PcmData pcmData;
            List<FlacDecodeIF.FlacCuesheetTrackInfo> ctiList;

            FlacDecodeIF fdif = new FlacDecodeIF();
            int flacErcd = 0;
            flacErcd = fdif.ReadHeader(path, out pcmData, out ctiList);
            if (flacErcd == 0) {
                // FLACヘッダ部分読み込み成功。

                if (ctiList.Count == 0 || mode == ReadHeaderMode.OnlyConcreteFile) {
                    // FLAC埋め込みCUEシート情報を読まない。

                    CheckAddPcmData(plti, path, pcmData);
                } else {
                    // FLAC埋め込みCUEシート情報を読む。

                    PcmData pcmTrack = null;
                    for (int trackId=0; trackId < ctiList.Count; ++trackId) {
                        var cti = ctiList[trackId];

                        if (cti.indices.Count == 0) {
                            // インデックスが1つもないトラック。lead-outトラックの場合等。
                            if (pcmTrack != null) {
                                pcmTrack.EndTick = (int)((cti.offsetSamples * 75) / pcmTrack.SampleRate);
                                CheckAddPcmData(null, path, pcmTrack);
                                pcmTrack = null;
                            }
                        } else {
                            for (int indexId=0; indexId < cti.indices.Count; ++indexId) {
                                var indexInfo = cti.indices[indexId];

                                if (pcmTrack != null) {
                                    pcmTrack.EndTick = (int)(((cti.offsetSamples + indexInfo.offsetSamples) * 75) / pcmTrack.SampleRate);
                                    CheckAddPcmData(null, path, pcmTrack);
                                    pcmTrack = null;
                                }

                                pcmTrack = new PcmData();
                                pcmTrack.CopyFrom(pcmData);
                                if (pcmTrack.DisplayName.Length == 0) {
                                    pcmTrack.DisplayName = System.IO.Path.GetFileName(path);
                                }
                                pcmTrack.DisplayName = string.Format(CultureInfo.InvariantCulture,
                                        "{0} (Track {1}, Index {2})", pcmTrack.DisplayName, cti.trackNr, indexInfo.indexNr);
                                pcmTrack.CueSheetIndex = indexInfo.indexNr;
                                pcmTrack.StartTick = (int)(((cti.offsetSamples + indexInfo.offsetSamples) * 75) / pcmTrack.SampleRate);
                            }
                        }
                    }
                }
                readResult = true;
            } else {
                // FLACヘッダ部分読み込み失敗。
                string s = string.Format(CultureInfo.InvariantCulture, Properties.Resources.ReadFileFailed + " {2}: {1}\r\n",
                    "FLAC",
                    path,
                    FlacDecodeIF.ErrorCodeToStr(flacErcd));
                AddLogText(s);
                LoadErrorMessageAdd(string.Format(CultureInfo.InvariantCulture, Properties.Resources.ReadFileFailed + " {2}: {1}",
                    "FLAC",
                    path,
                    FlacDecodeIF.ErrorCodeToStr(flacErcd)));
            }

            return readResult;
        }

        /// <summary>
        /// CUEシートを読み込む。
        /// </summary>
        /// <returns>読めたファイルの数を戻す</returns>
        private int ReadPlaylist(string path) {
            PlaylistReader plr = null;
            switch (Path.GetExtension(path).ToUpperInvariant()) {
            case ".CUE":
                plr = new CueSheetReader(Encoding.GetEncoding(m_preference.CueEncodingCodePage));
                break;
            case ".M3U":
            case ".M3U8":
                plr = new M3uReader();
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            bool result = plr.ReadFromFile(path);
            if (!result) {
                string s = string.Format(CultureInfo.InvariantCulture, Properties.Resources.ReadFileFailed + ": {1}\r\n",
                        Path.GetExtension(path), path);
                AddLogText(s);
                LoadErrorMessageAdd(s);
                return 0;
            }

            int readCount = 0;
            for (int i = 0; i < plr.GetTrackInfoCount(); ++i) {
                var plti = plr.GetTrackInfo(i);
                readCount += ReadFileHeader1(plti.path, ReadHeaderMode.OnlyConcreteFile, plti);

                if ((plti.indexId == 0 &&
                    m_preference.ReplaceGapWithKokomade) ||
                    plti.readSeparatorAfter) {
                    // INDEX 00 == gap しかも gapのかわりに[ここまで読みこみ]を追加するの場合
                    // または、CUEシートにREM KOKOMADEが書いてある場合。
                    AddKokomade();
                }
            }
            return readCount;
        }

        enum ReadHeaderMode {
            ReadAll,
            OnlyConcreteFile,
            OnlyMetaFile,
        }

        /// <summary>
        /// N.B. PcmReader.StreamBeginも参照(へぼい)。
        /// MenuItemFileOpen_Clickも参照。
        /// </summary>
        /// <returns>読めたファイルの数を戻す</returns>
        private int ReadFileHeader1(string path, ReadHeaderMode mode, PlaylistTrackInfo plti) {
            int result = 0;
            string ext = System.IO.Path.GetExtension(path);

            try {
                switch (ext.ToUpperInvariant()) {
                case ".PPWPL":
                    if (mode != ReadHeaderMode.OnlyConcreteFile) {
                        // PPWプレイリストを読み込み
                        result += ReadPpwPlaylist(path);
                    }
                    break;
                case ".CUE":
                case ".M3U":
                case ".M3U8":
                    if (mode != ReadHeaderMode.OnlyConcreteFile) {
                        // CUEシートかM3U8再生リストを読み込み。
                        result += ReadPlaylist(path);
                    }
                    break;
                case ".FLAC":
                    if (mode != ReadHeaderMode.OnlyMetaFile) {
                        result += ReadFlacFileHeader(path, mode, plti) ? 1 : 0;
                    }
                    break;
                case ".AIF":
                case ".AIFF":
                case ".AIFC":
                case ".AIFFC":
                    if (mode != ReadHeaderMode.OnlyMetaFile) {
                        result += ReadAiffFileHeader(path, plti) ? 1 : 0;
                    }
                    break;
                case ".WAV":
                case ".WAVE":
                    if (mode != ReadHeaderMode.OnlyMetaFile) {
                        result += ReadWavFileHeader(path, plti) ? 1 : 0;
                    }
                    break;
                case ".JPG":
                case ".JPEG":
                case ".PNG":
                case ".BMP":
                    // 読まないで無視する。
                    break;
                default: {
                        string s = string.Format(CultureInfo.InvariantCulture, "{0}: {1}\r\n",
                                Properties.Resources.NotSupportedFileFormat, path);
                        AddLogText(s);
                        LoadErrorMessageAdd(s);
                    }
                    break;
                }
            } catch (IOException ex) {
                HandleFileReadException(path, ex);
            } catch (ArgumentException ex) {
                HandleFileReadException(path, ex);
            } catch (UnauthorizedAccessException ex) {
                HandleFileReadException(path, ex);
            }

            return result;
        }

        private int ReadFileHeader(string path, ReadHeaderMode mode, PlaylistTrackInfo plti) {
            int result = 0;
            if (System.IO.Directory.Exists(path)) {
                // pathはディレクトリである。直下のファイル一覧を作って足す。再帰的にはたぐらない。
                var files = System.IO.Directory.GetFiles(path);
                foreach (var file in files) {
                    result += ReadFileHeader1(file, mode, plti);
                }
            } else {
                // pathはファイル。
                result += ReadFileHeader1(path, mode, plti);
            }
            return result;
        }

        //////////////////////////////////////////////////////////////////////////

        private void MainWindowDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effects = DragDropEffects.Copy;
            } else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MainWindowDragDrop(object sender, DragEventArgs e)
        {
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            /*
            Console.WriteLine("D: Form1_DragDrop() {0}", paths.Length);
            for (int i = 0; i < paths.Length; ++i) {
                Console.WriteLine("   {0}", paths[i]);
            }
            */

            if (State.デバイスSetup完了 <= m_state) {
                // 追加不可。
                MessageBox.Show(Properties.Resources.CannotAddFile);
                return;
            }

            // エラーメッセージを貯めて出す。作りがいまいちだが。
            m_loadErrorMessages = new StringBuilder();

            for (int i = 0; i < paths.Length; ++i) {
                ReadFileHeader(paths[i], ReadHeaderMode.ReadAll, null);
            }

            if (0 < m_loadErrorMessages.Length) {
                MessageBox.Show(m_loadErrorMessages.ToString(), Properties.Resources.ReadFailedFiles, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            
            m_loadErrorMessages = null;

            UpdateUIStatus();
        }

        private void MenuItemFileSaveCueAs_Click(object sender, RoutedEventArgs e) {
            if (m_pcmDataListForDisp.Count() == 0) {
                MessageBox.Show(Properties.Resources.NothingToStore);
                return;
            }

            System.Diagnostics.Debug.Assert(0 < m_pcmDataListForDisp.Count());
            var pcmData0 = m_pcmDataListForDisp[0];

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.InitialDirectory = System.IO.Path.GetDirectoryName(pcmData0.FullPath);
            dlg.Filter = Properties.Resources.FilterCueFiles;
            Nullable<bool> result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            CueSheetWriter csw = new CueSheetWriter();

            csw.SetAlbumTitle(m_playListItems[0].AlbumTitle);
            csw.SetAlbumPerformer(m_playListItems[0].PcmData().ArtistName);

            int i = 0;
            foreach (var pli in m_playListItems) {
                var pcmData = m_pcmDataListForDisp[i];

                CueSheetTrackInfo cst = new CueSheetTrackInfo();
                cst.title = pli.Title;
                cst.albumTitle = pli.AlbumTitle;
                cst.indexId = pcmData.CueSheetIndex;
                cst.performer = pli.ArtistName;
                cst.readSeparatorAfter = pli.ReadSeparaterAfter;
                cst.startTick = pcmData.StartTick;
                cst.endTick = pcmData.EndTick;
                cst.path = pcmData.FullPath;
                csw.AddTrackInfo(cst);
                ++i;
            }

            result = false;
            try {
                result = csw.WriteToFile(dlg.FileName);
            } catch (IOException ex) {
                Console.WriteLine("E: MenuItemFileSaveCueAs_Click {0}", ex);
            } catch (ArgumentException ex) {
                Console.WriteLine("E: MenuItemFileSaveCueAs_Click {0}", ex);
            } catch (UnauthorizedAccessException ex) {
                Console.WriteLine("E: MenuItemFileSaveCueAs_Click {0}", ex);
            }

            if (result != true) {
                MessageBox.Show(
                    string.Format(CultureInfo.InvariantCulture, "{0}: {1}", Properties.Resources.SaveFileFailed, dlg.FileName));
            }
        }

        private void MenuItemFileSaveAs_Click(object sender, RoutedEventArgs e) {
            if (m_pcmDataListForDisp.Count() == 0) {
                MessageBox.Show(Properties.Resources.NothingToStore);
                return;
            }

            System.Diagnostics.Debug.Assert(0 < m_pcmDataListForDisp.Count());
            var pcmData0 = m_pcmDataListForDisp[0];

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.InitialDirectory = System.IO.Path.GetDirectoryName(pcmData0.FullPath);
            dlg.Filter = Properties.Resources.FilterPpwplFiles;
            Nullable<bool> result = dlg.ShowDialog();

            if (result == true) {
                if (!SavePpwPlaylist(dlg.FileName)) {
                    MessageBox.Show(
                        string.Format(CultureInfo.InvariantCulture, "{0}: {1}", Properties.Resources.SaveFileFailed, dlg.FileName));
                }
            }
        }
        
        private void MenuItemFileNew_Click(object sender, RoutedEventArgs e) {
            ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
        }

        private void MenuItemFileOpen_Click(object sender, RoutedEventArgs e)
        {
            if (State.デバイスSetup完了 <= m_state) {
                // 追加不可。
                MessageBox.Show(Properties.Resources.CannotAddFile);
                return;
            }

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = Properties.Resources.FilterSupportedFiles;
            dlg.Multiselect = true;

            Nullable<bool> result = dlg.ShowDialog();


            if (result == true) {
                // エラーメッセージを貯めて出す。
                m_loadErrorMessages = new StringBuilder();

                for (int i = 0; i < dlg.FileNames.Length; ++i) {
                    ReadFileHeader(dlg.FileNames[i], ReadHeaderMode.ReadAll, null);
                }

                if (0 < m_loadErrorMessages.Length) {
                    MessageBox.Show(m_loadErrorMessages.ToString(),
                        Properties.Resources.ReadFailedFiles,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                m_loadErrorMessages = null;
                UpdateUIStatus();
            }

        }

        private void MenuItemHelpAbout_Click(object sender, RoutedEventArgs e) {
            MessageBox.Show(
                string.Format(CultureInfo.InvariantCulture, "PlayPcmWin {0} {1}\r\n\r\n{2}",
                    Properties.Resources.Version,
                    AssemblyVersion,
                    Properties.Resources.LicenseText));
        }

        private void MenuItemHelpWeb_Click(object sender, RoutedEventArgs e) {
            try {
                System.Diagnostics.Process.Start("http://code.google.com/p/bitspersampleconv2/wiki/PlayPcmWin");
            } catch (System.ComponentModel.Win32Exception) {
            }
        }

        private static string DfmToStr(WasapiDataFeedModeType dfm) {
            switch (dfm) {
            case WasapiDataFeedModeType.EventDriven:
                return Properties.Resources.EventDriven;
            case WasapiDataFeedModeType.TimerDriven:
                return Properties.Resources.TimerDriven;
            default:
                System.Diagnostics.Debug.Assert(false);
                return "unknown";
            }
        }

        private static string ShareModeToStr(WasapiSharedOrExclusiveType t) {
            switch (t) {
            case WasapiSharedOrExclusiveType.Exclusive:
                return "WASAPI " + Properties.Resources.Exclusive;
            case WasapiSharedOrExclusiveType.Shared:
                return "WASAPI " + Properties.Resources.Shared;
            default:
                System.Diagnostics.Debug.Assert(false);
                return "unknown";
            }
        }

        class ReadFileResultInfo {
            public int pcmDataId;
            public long clippedCount;
            public ReadFileResultInfo(int pcmDataId, long clippedCount) {
                this.pcmDataId = pcmDataId;
                this.clippedCount = clippedCount;
            }
        }

        class ReadFileRunWorkerCompletedArgs {
            public string message;
            public int hr;
            public List<ReadFileResultInfo> individualResultList = new List<ReadFileResultInfo>();

            public ReadFileRunWorkerCompletedArgs Update(string msg, int resultCode) {
                message = msg;
                hr = resultCode;
                return this;
            }
        }

        struct ReadProgressInfo {
            public int pcmDataId;
            public long startFrame;
            public long endFrame;
            public int trackCount;
            public int trackNum;

            public long readFrames;

            public long WantFramesTotal {
                get {
                    return endFrame - startFrame;
                }
            }

            public ReadProgressInfo(int pcmDataId, long startFrame, long endFrame, int trackCount, int trackNum) {
                this.pcmDataId  = pcmDataId;
                this.startFrame = startFrame;
                this.endFrame   = endFrame;
                this.trackCount = trackCount;
                this.trackNum   = trackNum;
                this.readFrames = 0;
            }

            public void FileReadStart(int pcmDataId, long startFrame, long endFrame) {
                this.pcmDataId  = pcmDataId;
                this.startFrame = startFrame;
                this.endFrame   = endFrame;
                this.readFrames = 0;
            }
        };

        /// <summary>
        /// ファイル読み出しの進捗状況
        /// </summary>
        ReadProgressInfo m_readProgressInfo;

        private static ReadFileRunWorkerCompletedArgs CreateRunWorkerCompletedArgsWhenException(
                ReadFileRunWorkerCompletedArgs r, Exception ex) {
            r.message = ex.ToString();
            r.hr = -1;
            return r;
        }

        /// <summary>
        ///  バックグラウンド読み込み。
        ///  m_readFileWorker.RunWorkerAsync(読み込むgroupId)で開始する。
        ///  完了するとReadFileRunWorkerCompletedが呼ばれる。
        /// </summary>
        private void ReadFileDoWork(object o, DoWorkEventArgs args) {
            BackgroundWorker bw = (BackgroundWorker)o;
            int readGroupId = (int)args.Argument;
            // Console.WriteLine("D: ReadFileSingleDoWork({0}) started", readGroupId);

            ReadFileRunWorkerCompletedArgs r = new ReadFileRunWorkerCompletedArgs();
            try {
                r.hr = -1;
                r.message = string.Empty;

                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();

                m_readProgressInfo = new ReadProgressInfo(
                    0, 0, 0, 0, CountWaveDataOnPlayGroup(m_pcmDataListForPlay, readGroupId));

                wasapi.ClearPlayList();
                wasapi.AddPlayPcmDataStart();
                for (int i = 0; i < m_pcmDataListForPlay.Count; ++i) {
                    PcmDataLib.PcmData pd = m_pcmDataListForPlay[i];
                    if (pd.GroupId != readGroupId) {
                        continue;
                    }

                    // どーなのよ、という感じがするが。
                    // 効果絶大である。
                    GC.Collect();

                    PcmData.ClearClippedCounter();

                    long startFrame = (long)(pd.StartTick) * pd.SampleRate / 75;
                    long endFrame   = (long)(pd.EndTick) * pd.SampleRate / 75;

                    bool rv = ReadOnePcmFile(bw, pd, startFrame, endFrame, ref r);
                    if (bw.CancellationPending) {
                        r.hr = -1;
                        r.message = string.Empty;
                        args.Result = r;
                        args.Cancel = true;
                        return;
                    }

                    {
                        long clippedCount = PcmData.ReadClippedCounter();
                        if (0 < clippedCount) {
                            r.individualResultList.Add(new ReadFileResultInfo(pd.Id, clippedCount));
                        }
                    }

                    if (!rv) {
                        args.Result = r;
                        return;
                    }

                    ++m_readProgressInfo.trackCount;
                }

                // ダメ押し。
                GC.Collect();

                if (m_preference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Shared) {
                    m_readFileWorker.ReportProgress(90, string.Format(CultureInfo.InvariantCulture, "Resampling...\r\n"));
                }
                r.hr = wasapi.ResampleIfNeeded();
                if (r.hr < 0) {
                    r.message = "Resample failed! " + string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", r.hr);
                    args.Result = r;
                    return;
                }

                if (m_preference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Shared
                        && m_preference.SootheLimiterApo) {
                    // Limiter APO対策の音量制限。
                    double maxAmplitude = wasapi.ScanPcmMaxAbsAmplitude();
                    if (SHARED_MAX_AMPLITUDE < maxAmplitude) {
                        m_readFileWorker.ReportProgress(95, string.Format(CultureInfo.InvariantCulture, "Scaling amplitude by {0:0.000}dB ({1:0.000}x) to soothe Limiter APO...\r\n",
                                20.0 * Math.Log10(SHARED_MAX_AMPLITUDE / maxAmplitude), SHARED_MAX_AMPLITUDE / maxAmplitude));
                        wasapi.ScalePcmAmplitude(SHARED_MAX_AMPLITUDE / maxAmplitude);
                    }
                }

                wasapi.AddPlayPcmDataEnd();

                // 成功。
                sw.Stop();
                r.message = string.Format(CultureInfo.InvariantCulture, Properties.Resources.ReadPlayGroupNCompleted + "\r\n", readGroupId, sw.ElapsedMilliseconds);
                r.hr = 0;
                args.Result = r;

                m_loadedGroupId = readGroupId;

                // Console.WriteLine("D: ReadFileSingleDoWork({0}) done", readGroupId);
            } catch (IOException ex) {
                args.Result = r.Update(ex.ToString(), -1);
            } catch (ArgumentException ex) {
                args.Result = r.Update(ex.ToString(), -1);
            } catch (UnauthorizedAccessException ex) {
                args.Result = r.Update(ex.ToString(), -1);
            }
        }

        private class ReadPcmTaskInfo : IDisposable {
            MainWindow mw;
            BackgroundWorker bw;
            PcmDataLib.PcmData pd;
            public long readStartFrame;
            public long readFrames;
            public long writeOffsFrame;
            public ManualResetEvent doneEvent;
            public bool result;
            public string message;

            protected virtual void Dispose(bool disposing) {
                if (disposing) {
                    doneEvent.Close();
                }
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            public ReadPcmTaskInfo(MainWindow mw, BackgroundWorker bw, PcmDataLib.PcmData pd, long readStartFrame, long readFrames, long writeOffsFrame) {
                this.mw = mw;
                this.bw = bw;

                // PcmDataのSampleArrayメンバを各スレッドが物置のように使うので実体をコピーする。
                this.pd = new PcmData();
                this.pd.CopyFrom(pd);

                this.readStartFrame = readStartFrame;
                this.readFrames     = readFrames;
                this.writeOffsFrame = writeOffsFrame;

                this.message = string.Empty;

                doneEvent = new ManualResetEvent(false);
                result = true;
            }

            public void ThreadPoolCallback(Object threadContext) {
                int threadIndex = (int)threadContext;
                result = mw.ReadOnePcmFileFragment(bw, pd, readStartFrame, readFrames, writeOffsFrame, ref message);
                doneEvent.Set();
            }

            /// <summary>
            /// このインスタンスの使用を終了する。再利用はできない。
            /// </summary>
            public void End() {
                mw = null;
                bw = null;
                pd = null;
                readStartFrame = 0;
                readFrames = 0;
                writeOffsFrame = 0;
                doneEvent = null;
                result = true;
            }
        };

        /// <summary>
        /// 分割読み込みのそれぞれのスレッドの読み込み開始位置と読み込みバイト数を計算する。
        /// </summary>
        private List<ReadPcmTaskInfo> SetupReadPcmTasks(BackgroundWorker bw, PcmDataLib.PcmData pd, long startFrame, long endFrame, int fragmentCount) {
            var result = new List<ReadPcmTaskInfo>();

            long readFrames = (endFrame - startFrame) / fragmentCount;
            // すくなくとも4Mフレームずつ読む。その結果fragmentCountよりも少ない場合がある。
            if (readFrames < 4 * 1024 * 1024) {
                readFrames = 4 * 1024 * 1024;
            }

            long readStartFrame = startFrame;
            long writeOffsFrame = 0;
            do {
                if (endFrame < readStartFrame + readFrames) {
                    readFrames = endFrame - readStartFrame;
                }
                var rri = new ReadPcmTaskInfo(this, bw, pd, readStartFrame, readFrames, writeOffsFrame);
                result.Add(rri);
                readStartFrame += readFrames;
                writeOffsFrame += readFrames;
            } while (readStartFrame < endFrame);
            return result;
        }

        private void ReadFileReportProgress(long readFrames, bool noiseShaping) {
            lock (m_readFileWorker) {
                m_readProgressInfo.readFrames += readFrames;
                var rpi = m_readProgressInfo;

                double loadCompletedPercent = 100.0;
                if (m_preference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Shared) {
                    loadCompletedPercent = 90.0;
                }

                double progressPercentage = loadCompletedPercent * (rpi.trackCount + (double)rpi.readFrames / rpi.WantFramesTotal) / rpi.trackNum;
                m_readFileWorker.ReportProgress((int)progressPercentage,
                    string.Format(CultureInfo.InvariantCulture, "wasapi.AddPlayPcmData(id={0}, frames={1})\r\n", rpi.pcmDataId, rpi.readFrames));
                if (noiseShaping) {
                    m_readFileWorker.ReportProgress((int)progressPercentage, "Noise shaping...\r\n");
                }
            }
        }

        private bool ReadOnePcmFile(BackgroundWorker bw, PcmDataLib.PcmData pd, long startFrame, long endFrame, ref ReadFileRunWorkerCompletedArgs r) {
            {
                // endFrameの位置を確定する。
                // すると、rpi.ReadFramesも確定する。
                PcmReader pr = new PcmReader();
                int ercd = pr.StreamBegin(pd.FullPath, 0, 0);
                pr.StreamEnd();
                if (ercd < 0) {
                    r.hr = ercd;
                    r.message = string.Format(CultureInfo.InvariantCulture, "{0}. {1}\r\n{2} {3}(0x{3:X8})。{4}",
                        Properties.Resources.ReadError,
                        Properties.Resources.ErrorCode,
                        pd.FullPath, ercd, FlacDecodeIF.ErrorCodeToStr(ercd));
                    Console.WriteLine("D: ReadFileSingleDoWork() !readSuccess");
                    return false;
                }
                if (endFrame < 0 || pr.NumFrames < endFrame) {
                    endFrame = pr.NumFrames;
                }
            }

            // endFrameが確定したので、総フレーム数をPcmDataにセット。
            long wantFramesTotal = endFrame - startFrame;
            pd.SetNumFrames(wantFramesTotal);
            m_readProgressInfo.FileReadStart(pd.Id, startFrame, endFrame);
            ReadFileReportProgress(0, false);

            {
                // このトラックのWasapi PCMデータ領域を確保する。
                long allocBytes = wantFramesTotal * m_deviceSetupInfo.UseBytesPerFrame;
                if (!wasapi.AddPlayPcmDataAllocateMemory(pd.Id, allocBytes)) {
                    //ClearPlayList(PlayListClearMode.ClearWithoutUpdateUI); //< メモリを空ける：効果があるか怪しい
                    r.message = string.Format(CultureInfo.InvariantCulture, Properties.Resources.MemoryExhausted);
                    Console.WriteLine("D: ReadFileSingleDoWork() lowmemory");
                    return false;
                }
            }

            bool result = true;
            if (m_preference.ParallelRead && PcmReader.IsTheFormatCompressed(PcmReader.GuessFileFormatFromFilePath(pd.FullPath))) {
                // ファイルのstartFrameからendFrameまでを読みだす。(並列化)
                int fragmentCount = Environment.ProcessorCount;
                var rri = SetupReadPcmTasks(bw, pd, startFrame, endFrame, fragmentCount);
                var doneEventArray = new ManualResetEvent[rri.Count];
                for (int i=0; i < rri.Count; ++i) {
                    doneEventArray[i] = rri[i].doneEvent;
                }

                for (int i=0; i < rri.Count; ++i) {
                    ThreadPool.QueueUserWorkItem(rri[i].ThreadPoolCallback, i);
                }
                WaitHandle.WaitAll(doneEventArray);

                for (int i=0; i < rri.Count; ++i) {
                    if (!rri[i].result) {
                        r.message += rri[i].message + "\r\n";
                        result = false;
                    }
                    rri[i].End();
                }
                rri.Clear();
                doneEventArray = null;
            } else {
                // ファイルのstartFrameからendFrameまでを読み出す。(1スレッド)
                string message = string.Empty;
                result = ReadOnePcmFileFragment(bw, pd, startFrame, wantFramesTotal, 0, ref message);
                if (!result) {
                    r.message = message;
                }
            }

            return result;
        }

        private bool ReadOnePcmFileFragment(BackgroundWorker bw, PcmDataLib.PcmData pd, long readStartFrame, long wantFramesTotal, long writeOffsFrame, ref string message) {
            PcmReader pr = new PcmReader();
            int ercd = pr.StreamBegin(pd.FullPath, readStartFrame, wantFramesTotal);
            if (ercd < 0) {
                Console.WriteLine("D: ReadOnePcmFileFragment() StreamBegin failed");
                message = FlacDecodeIF.ErrorCodeToStr(ercd);
                return false;
            }

            long frameCount = 0;
            do {
                // 読み出したいフレーム数wantFrames。
                int wantFrames = 4 * 1024 * 1024;
                if (wantFramesTotal < frameCount + wantFrames) {
                    wantFrames = (int)(wantFramesTotal - frameCount);
                }

                byte[] part = pr.StreamReadOne(wantFrames);
                if (null == part) {
                    pr.StreamEnd();
                    Console.WriteLine("D: ReadOnePcmFileFragment() lowmemory");
                    message = "Low memory";
                    return false;
                }

                // 実際に読み出されたフレーム数readFrames。
                int readFrames = part.Length / (pd.BitsPerFrame / 8);

                pd.SetSampleArray(part);
                part = null;

                // 必要に応じてpartの量子化ビット数の変更処理を行い、pdAfterに新しく確保したPCMデータ配列をセット。

                var bpsConvArgs = new PcmData.BitsPerSampleConvArgs(m_preference.EnableNoiseShaping);
                PcmData pdAfter = null;
                if (m_preference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Exclusive) {
                    pdAfter = PcmUtil.BitsPerSampleConvAsNeeded(pd, m_deviceSetupInfo.SampleFormat, bpsConvArgs);
                    pd.ForgetDataPart();
                } else {
                    pdAfter = pd;
                }

                if (pdAfter.GetSampleArray() == null ||
                    0 == pdAfter.GetSampleArray().Length) {
                    // サンプルが存在しないのでWasapiにAddしない。
                    break;
                }

                if (pdAfter.NumChannels == 1) {
                    // モノラル1ch→ステレオ2ch変換。
                    pdAfter = pdAfter.MonoToStereo();
                }

                long posBytes = (writeOffsFrame + frameCount) * pdAfter.BitsPerFrame / 8;

                bool result = false;
                lock (pd) {
                    result = wasapi.AddPlayPcmDataSetPcmFragment(pd.Id, posBytes, pdAfter.GetSampleArray());
                }
                System.Diagnostics.Debug.Assert(result);

                pdAfter.ForgetDataPart();

                // frameCountを進める
                frameCount += readFrames;

                ReadFileReportProgress(readFrames, bpsConvArgs.noiseShapingPerformed);

                if (bw.CancellationPending) {
                    pr.StreamAbort();
                    message = string.Empty;
                    return false;
                }
            } while (frameCount < wantFramesTotal);

            ercd = pr.StreamEnd();
            if (ercd < 0) {
                message = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", FlacDecodeIF.ErrorCodeToStr(ercd), pd.FullPath);
            }
            return 0 <= ercd;
        }

        private void ReadFileWorkerProgressChanged(object sender, ProgressChangedEventArgs e) {
            string s = (string)e.UserState;
            if (0 < s.Length) {
                AddLogText(s);
            }
            progressBar1.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// リピート設定。
        /// </summary>
        private void UpdatePlayRepeat() {
            bool repeat = false;
            // 1曲リピートか(全曲リピート再生で、GroupIdが0しかない場合)、WASAPI再生スレッドのリピート設定が可能。
            ComboBoxPlayModeType playMode = (ComboBoxPlayModeType)comboBoxPlayMode.SelectedIndex;
            if (playMode == ComboBoxPlayModeType.OneTrackRepeat
                || (playMode == ComboBoxPlayModeType.AllTracksRepeat
                    && 0 == CountWaveDataOnPlayGroup(m_pcmDataListForPlay, 1))) {
                repeat = true;
            }
            wasapi.SetPlayRepeat(repeat);
        }

        /// <summary>
        /// バックグラウンドファイル読み込みが完了した。
        /// </summary>
        private void ReadFileRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            if (args.Cancelled) {
                // キャンセル時は何もしないで直ちに終わる。
                return;
            }

            ReadFileRunWorkerCompletedArgs r = (ReadFileRunWorkerCompletedArgs)args.Result;

            AddLogText(r.message);

            if (r.hr < 0) {
                MessageBox.Show(r.message);
                Exit();
                return;
            }

            if (0 < r.individualResultList.Count) {
                foreach (var fileResult in r.individualResultList) {
                    AddLogText(string.Format(CultureInfo.InvariantCulture, Properties.Resources.ClippedSampleDetected,
                        FindPcmDataById(m_pcmDataListForPlay, fileResult.pcmDataId).FileName, fileResult.clippedCount));
                }
            }

            // WasapiCSのリピート設定。
            UpdatePlayRepeat();

            if (m_task.Type == TaskType.PlaySpecifiedGroup) {
                // ファイル読み込み完了後、再生を開始する。
                // 再生するファイルは、タスクで指定されたファイル。
                // このwavDataIdは、再生開始ボタンが押された時点で選択されていたファイル。
                int wavDataId = m_task.WavDataId;

                if (null != m_pliUpdatedByUserSelectWhileLoading) {
                    // (Issue 6)再生リストで選択されている曲が違う曲の場合、
                    // 選択されている曲を再生する。
                    wavDataId = m_pliUpdatedByUserSelectWhileLoading.PcmData().Id;

                    // 使い終わったのでクリアーする。
                    m_pliUpdatedByUserSelectWhileLoading = null;
                }

                ReadStartPlayByWavDataId(wavDataId);
                return;
            }

            // ファイル読み込み完了後、何もすることはない。
            ChangeState(State.ファイル読み込み完了);
            UpdateUIStatus();
        }

        /// <summary>
        /// デバイスを選択。
        /// 既に使用中の場合、空振りする。
        /// 別のデバイスを使用中の場合、そのデバイスを未使用にして、新しいデバイスを使用状態にする。
        /// </summary>
        private bool UseDevice()
        {
            var di = listBoxDevices.SelectedItem as DeviceInfo;
            int    chosenDeviceId    = wasapi.GetUseDeviceId();
            string chosenDeviceIdStr = wasapi.GetUseDeviceIdString();

            if (0 == string.CompareOrdinal(di.DeviceIdStr, chosenDeviceIdStr)) {
                // このデバイスが既に指定されている場合は、空振りする。
                return true;
            }

            if (0 <= chosenDeviceId) {
                // 別のデバイスが選択されている場合、Unchooseする。
                wasapi.UnchooseDevice();
                AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.UnchooseDevice()\r\n"));
            }

            // このデバイスを選択。
            int hr = wasapi.ChooseDevice(listBoxDevices.SelectedIndex);
            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.ChooseDevice({0}) {1:X8}\r\n", di.Name, hr));
            if (hr < 0) {
                return false;
            }

            // 通常使用するデバイスとする。
            m_preference.PreferredDeviceName     = di.Name;
            m_preference.PreferredDeviceIdString = di.DeviceIdStr;
            return true;
        }

        /// <summary>
        /// loadGroupIdのファイル読み込みを開始する。
        /// 読み込みが完了したらReadFileRunWorkerCompletedが呼ばれる。
        /// </summary>
        private void StartReadFiles(int loadGroupId) {
            progressBar1.Visibility = System.Windows.Visibility.Visible;
            progressBar1.Value = 0;

            m_loadingGroupId = loadGroupId;
            
            m_readFileWorker.RunWorkerAsync(loadGroupId);
        }

        /// <summary>
        /// 0 <= r < nMaxPlus1の範囲の整数値rをランダムに戻す。
        /// </summary>
        private static int GetRandomNumber(RNGCryptoServiceProvider gen, int nMaxPlus1) {
            byte[] v = new byte[4];
            gen.GetBytes(v);
            return (BitConverter.ToInt32(v, 0) & 0x7fffffff) % nMaxPlus1;
        }

        /// <summary>
        /// シャッフルした再生リストm_pcmDataListForPlayを作成する
        /// </summary>
        private void CreateShuffledPlayList() {
            // 適当にシャッフルされた番号が入っている配列pcmDataIdxArrayを作成。
            var pcmDataIdxArray = new int[m_pcmDataListForDisp.Count];
            for (int i=0; i < pcmDataIdxArray.Length; ++i) {
                pcmDataIdxArray[i] = i;
            }
            
            var gen = new RNGCryptoServiceProvider();
            int N = pcmDataIdxArray.Length;
            for (int i=0; i < N * 100; ++i) {
                var a = GetRandomNumber(gen, N);
                var b = GetRandomNumber(gen, N);
                if (a == b) {
                    // 入れ替え元と入れ替え先が同じ。あんまり意味ないのでスキップする。
                    continue;
                }

                // a番目とb番目を入れ替える
                var tmp = pcmDataIdxArray[a];
                pcmDataIdxArray[a] = pcmDataIdxArray[b];
                pcmDataIdxArray[b] = tmp;
            }

            // m_pcmDataListForPlayを作成。
            m_pcmDataListForPlay = new List<PcmData>();
            for (int i=0; i < pcmDataIdxArray.Length; ++i) {
                var idx = pcmDataIdxArray[i];

                // 再生順番号Ordinalを付け直す
                // GroupIdをバラバラの番号にする(1曲ずつ読み込む)
                var pcmData = new PcmData();
                pcmData.CopyFrom(m_pcmDataListForDisp[idx]);
                pcmData.Ordinal = i;
                pcmData.GroupId = i;

                m_pcmDataListForPlay.Add(pcmData);
            }
        }

        /// <summary>
        /// 全曲が表示順に並んでいるプレイリストm_pcmDataListForPlayを作成。
        /// </summary>
        private void CreateAllTracksPlayList() {
            m_pcmDataListForPlay = new List<PcmData>();
            for (int i=0; i < m_pcmDataListForDisp.Count; ++i) {
                var pcmData = new PcmData();
                pcmData.CopyFrom(m_pcmDataListForDisp[i]);
                m_pcmDataListForPlay.Add(pcmData);
            }
        }

        /// <summary>
        /// 1曲再生のプレイリストをm_pcmDataListForPlayに作成。
        /// </summary>
        private void CreateOneTrackPlayList(int wavDataId) {
            var pcmData = new PcmData();
            pcmData.CopyFrom(FindPcmDataById(m_pcmDataListForDisp, wavDataId));
            pcmData.GroupId = 0;

            m_pcmDataListForPlay = new List<PcmData>();
            m_pcmDataListForPlay.Add(pcmData);
        }

        private void buttonPlay_Click(object sender, RoutedEventArgs e) {
            var di = listBoxDevices.SelectedItem as DeviceInfo;
            if (!UseDevice()) {
                return;
            }

            if (IsPlayModeShuffle()) {
                // シャッフル再生する
                CreateShuffledPlayList();
                ReadStartPlayByWavDataId(m_pcmDataListForPlay[0].Id);
                return;
            }

            // 選択されている曲から順番に再生する。
            // 再生する曲のwavDataIdをdataGridの選択セルから取得する
            int wavDataId = 0;
            var selectedCells = dataGridPlayList.SelectedCells;
            if (0 < selectedCells.Count) {
                var cell = selectedCells[0];
                System.Diagnostics.Debug.Assert(cell != null);
                PlayListItemInfo pli = cell.Item as PlayListItemInfo;
                System.Diagnostics.Debug.Assert(pli != null);
                var pcmData = pli.PcmData();

                if (null != pcmData) {
                    wavDataId = pcmData.Id;
                } else {
                    // ココまで読んだ的な行は、pcmDataを持っていない
                }
            }

            if (IsPlayModeOneTrack()) {
                // 1曲再生。1曲だけ読み込んで再生する。
                CreateOneTrackPlayList(wavDataId);
                ReadStartPlayByWavDataId(wavDataId);
                return;
            }

            // 全曲再生
            CreateAllTracksPlayList();
            ReadStartPlayByWavDataId(wavDataId);
        }

        private void buttonPause_Click(object sender, RoutedEventArgs e) {
            int hr = 0;

            switch (m_state) {
            case State.再生中:
                hr = wasapi.Pause();
                AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.Pause() {0:X8}\r\n", hr));
                if (0 <= hr) {
                    ChangeState(State.再生一時停止中);
                    UpdateUIStatus();
                } else {
                    // Pause失敗＝すでに再生していない または再生一時停止ができない状況。ここで状態遷移する必要はない。
                }
                break;
            case State.再生一時停止中:
                hr = wasapi.Unpause();
                AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.Unpause() {0:X8}\r\n", hr));
                if (0 <= hr) {
                    ChangeState(State.再生中);
                    UpdateUIStatus();
                } else {
                    // Unpause失敗＝すでに再生していない。ここで状態遷移する必要はない。
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        private static PcmData FindPcmDataById(List<PcmData> pcmDataList, int wavDataId) {
            for (int i=0; i < pcmDataList.Count; ++i) {
                PcmData pcmData = pcmDataList[i];
                if (pcmData.Id == wavDataId) {
                    return pcmData;
                }
            }
            return null;
        }

        /// <summary>
        /// wavDataIdのGroupがロードされていたら直ちに再生開始する。
        /// 読み込まれていない場合、直ちに再生を開始できないので、ロードしてから再生する。
        /// </summary>
        private bool ReadStartPlayByWavDataId(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);


            PcmDataLib.PcmData pcmData = FindPcmDataById(m_pcmDataListForPlay, wavDataId);
            if (null == pcmData) {
                // 1曲再生モードの時。再生リストを作りなおす。
                CreateOneTrackPlayList(wavDataId);
                m_loadedGroupId = -1;
                pcmData = FindPcmDataById(m_pcmDataListForPlay, wavDataId);
            }

            if (pcmData.GroupId != m_loadedGroupId) {
                // m_LoadedGroupIdと、wavData.GroupIdが異なる場合。
                // 再生するためには、ロードする必要がある。
                UnsetupDevice();

                if (!SetupDevice(pcmData.GroupId)) {
                    //dataGridPlayList.SelectedIndex = 0;
                    ChangeState(State.ファイル読み込み完了);

                    DeviceDeselect();
                    UpdateDeviceList();
                    return false;
                }

                m_task.Set(TaskType.PlaySpecifiedGroup, pcmData.GroupId, pcmData.Id);
                StartReadPlayGroupOnTask();
                return true;
            }

            // wavDataIdのグループがm_LoadedGroupIdである。ロードされている。
            // 連続再生フラグの設定と、現在のグループが最後のグループかどうかによって
            // m_LoadedGroupIdの再生が自然に完了したら、行うタスクを決定する。
            UpdateNextTask();

            if (!SetupDevice(pcmData.GroupId)) {
                //dataGridPlayList.SelectedIndex = 0;
                ChangeState(State.ファイル読み込み完了);

                DeviceDeselect();
                UpdateDeviceList();
                return false;
            }
            StartPlay(wavDataId);
            return true;
        }

        /// <summary>
        /// 現在のグループの最後のファイルの再生が終わった後に行うタスクを判定し、
        /// m_taskにセットする。
        /// </summary>
        private void UpdateNextTask() {
            if (0 == CountWaveDataOnPlayGroup(m_pcmDataListForPlay, 1)) {
                // ファイルグループが1個しかない場合、
                // wasapiUserの中で自発的にループ再生する。
                // ファイルの再生が終わった=停止。
                m_task.Set(TaskType.None);
                return;
            }

            // 順当に行ったら次に再生するグループ番号は(m_loadedGroupId+1)。
            // ①(m_loadedGroupId+1)の再生グループが存在する場合
            //     (m_loadedGroupId+1)の再生グループを再生開始する。
            // ②(m_loadedGroupId+1)の再生グループが存在しない場合
            //     ②-①連続再生(checkBoxContinuous.IsChecked==true)の場合
            //         GroupId==0、wavDataId=0を再生開始する。
            //     ②-②連続再生ではない場合
            //         停止する。先頭の曲を選択状態にする。
            int nextGroupId = m_loadedGroupId + 1;

            if (0 < CountWaveDataOnPlayGroup(m_pcmDataListForPlay, nextGroupId)) {
                m_task.Set(TaskType.PlaySpecifiedGroup, 
                    nextGroupId,
                    GetFirstWavDataIdOnGroup(m_pcmDataListForPlay, nextGroupId));
                return;
            }

            if (IsPlayModeRepeat()) {
                m_task.Set(TaskType.PlaySpecifiedGroup, 0, 0);
                return;
            }

            m_task.Set(TaskType.None);
        }

        /// <summary>
        /// ただちに再生を開始する。
        /// wavDataIdのGroupが、ロードされている必要がある。
        /// </summary>
        /// <returns>false: 再生開始できなかった。</returns>
        private bool StartPlay(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);
            var playPcmData = FindPcmDataById(m_pcmDataListForPlay, wavDataId);
            if (playPcmData.GroupId != m_loadedGroupId) {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }

            ChangeState(State.再生中);
            UpdateUIStatus();

            m_sw.Reset();
            m_sw.Start();

            int hr = wasapi.StartPlayback(wavDataId);
            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.StartPlayback({0}) {1:X8}\r\n",
                wavDataId, hr));
            if (hr < 0) {
                MessageBox.Show(string.Format(CultureInfo.InvariantCulture, Properties.Resources.PlayStartFailed + "！{0:X8}", hr));
                Exit();
                return false;
            }

            // 再生バックグラウンドタスク開始。PlayDoWorkが実行される。
            // 再生バックグラウンドタスクを止めるには、Stop()を呼ぶ。
            // 再生バックグラウンドタスクが止まったらPlayRunWorkerCompletedが呼ばれる。
            m_playWorker.RunWorkerAsync();
            return true;
        }

        /// <summary>
        /// 再生中。バックグラウンドスレッド。
        /// </summary>
        private void PlayDoWork(object o, DoWorkEventArgs args) {
            //Console.WriteLine("PlayDoWork started");
            BackgroundWorker bw = (BackgroundWorker)o;

            while (!wasapi.Run(PROGRESS_REPORT_INTERVAL_MS)) {
                if (!m_preference.RefrainRedraw) {
                    m_playWorker.ReportProgress(0);
                }
                System.Threading.Thread.Sleep(1);
                if (bw.CancellationPending) {
                    Console.WriteLine("PlayDoWork() CANCELED");
                    wasapi.Stop();
                    args.Cancel = true;
                }
            }

            // 正常に最後まで再生が終わった場合、ここでStopを呼んで、後始末する。
            // キャンセルの場合は、2回Stopが呼ばれることになるが、問題ない!!!
            wasapi.Stop();

            // 停止完了後タスクの処理は、ここではなく、PlayRunWorkerCompletedで行う。

            //Console.WriteLine("PlayDoWork end");
        }

        private static string SecondsToHMSString(int seconds) {
            int h = seconds / 3600;
            int m = seconds / 60 - h * 60;
            int s = seconds - h * 3600 - m * 60;
            return string.Format(CultureInfo.CurrentCulture, "{0:D2}:{1:D2}:{2:D2}", h, m, s);
        }

        /// <summary>
        /// 再生の進行状況をUIに反映する。
        /// </summary>
        private void PlayProgressChanged(object o, ProgressChangedEventArgs args) {
            BackgroundWorker bw = (BackgroundWorker)o;

            if (null == wasapi) {
                return;
            }

            if (bw.CancellationPending) {
                // ワーカースレッドがキャンセルされているので、何もしない。
                return;
            }

            /* {
                long hnano = wasapi.GetTimePeriodHundredNanosec();
                AddLogText(string.Format(CultureInfo.InvariantCulture, " timer resolution = {0} microsec\r\n",
                    hnano * 0.1));
            } */


            // 再生中PCMデータ(または一時停止再開時再生予定PCMデータ等)の再生位置情報を画面に表示する。
            WasapiCS.PcmDataUsageType usageType = WasapiCS.PcmDataUsageType.NowPlaying;
            int pcmDataId = wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.NowPlaying);
            if (pcmDataId < 0) {
                pcmDataId = wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.PauseResumeToPlay);
                usageType = WasapiCS.PcmDataUsageType.PauseResumeToPlay;
            }
            if (pcmDataId < 0) {
                pcmDataId = wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.SpliceNext);
                usageType = WasapiCS.PcmDataUsageType.SpliceNext;
            }

            if (pcmDataId < 0) {
                labelPlayingTime.Content = PLAYING_TIME_UNKNOWN;
            } else {
                if (dataGridPlayList.SelectedIndex != GetPlayListIndexOfWaveDataId(pcmDataId)) {
                    dataGridPlayList.SelectedIndex = GetPlayListIndexOfWaveDataId(pcmDataId);
                    dataGridPlayList.ScrollIntoView(dataGridPlayList.SelectedItem);
                }

                PcmDataLib.PcmData pcmData = FindPcmDataById(m_pcmDataListForPlay, pcmDataId);

                long totalFrameNum = wasapi.GetTotalFrameNum(usageType);
                long playingFrame  = wasapi.GetPosFrame(usageType);

                slider1.Maximum = totalFrameNum;
                if (!mSliderSliding || totalFrameNum <= slider1.Value) {
                    slider1.Value = playingFrame;
                }

                int sampleRate = wasapi.GetDeviceSampleRate();

                labelPlayingTime.Content = string.Format(CultureInfo.InvariantCulture, "{0}/{1}",
                    SecondsToHMSString((int)(slider1.Value / sampleRate)),
                    SecondsToHMSString((int)(totalFrameNum / sampleRate)));
            }
        }

        /// <summary>
        /// m_taskに指定されているグループをロードし、ロード完了したら指定ファイルを再生開始する。
        /// ファイル読み込み完了状態にいるときに呼ぶ。
        /// </summary>
        private void StartReadPlayGroupOnTask() {
            m_loadedGroupId = -1;

            System.Diagnostics.Debug.Assert(m_task.Type == TaskType.PlaySpecifiedGroup);

            // 再生状態→再生グループ切り替え中状態に遷移。
            ChangeState(State.再生グループ切り替え中);
            UpdateUIStatus();

            StartReadFiles(m_task.GroupId);
        }

        /// <summary>
        /// 再生終了後タスクを実行する。
        /// </summary>
        private void PerformPlayCompletedTask() {
            // 再生終了後に行うタスクがある場合、ここで実行する。
            if (m_task.Type == TaskType.PlaySpecifiedGroup) {
                UnsetupDevice();

                if (IsPlayModeOneTrack()) {
                    // 1曲再生モードの時、再生リストを作りなおす。
                    CreateOneTrackPlayList(m_task.WavDataId);
                }

                if (SetupDevice(m_task.GroupId)) {
                    StartReadPlayGroupOnTask();
                    return;
                }

                // デバイスの設定を試みたら、失敗した。
                // FALL_THROUGHする。
            }

            // 再生終了後に行うタスクがない。停止する。
            // 再生状態→ファイル読み込み完了状態。

            // 先頭の曲を選択状態にする。
            //dataGridPlayList.SelectedIndex = 0;
            
            ChangeState(State.ファイル読み込み完了);

            // さらに、デバイスを選択解除し、デバイス一覧を更新する。
            // 停止後に再生リストの追加ができて便利。
            DeviceDeselect();
            UpdateDeviceList();
        }

        /// <summary>
        /// 再生終了。
        /// </summary>
        private void PlayRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            m_sw.Stop();
            AddLogText(string.Format(CultureInfo.InvariantCulture, Properties.Resources.PlayCompletedElapsedTimeIs + " {0}\r\n",
                m_sw.Elapsed));

            PerformPlayCompletedTask();
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            ChangeState(State.再生停止開始);
            UpdateUIStatus();

            // 停止ボタンで停止した場合は、停止後何もしない。
            StopAsync(new Task(TaskType.None));
            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.Stop()\r\n"));
        }

        private long mLastSliderValue = 0;

        private void slider1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.Source != slider1) {
                return;
            }

            mLastSliderValue = (long)slider1.Value;
            mSliderSliding = true;
        }

        private void slider1_MouseMove(object sender, MouseEventArgs e) {
            if (e.Source != slider1) {
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed) {
                mLastSliderValue = (long)slider1.Value;
                if (!buttonPlay.IsEnabled) {
                    wasapi.SetPosFrame((long)slider1.Value);
                }
            }
        }
        private void slider1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (e.Source != slider1) {
                return;
            }

            if (!buttonPlay.IsEnabled &&
                    mLastSliderValue != (long)slider1.Value) {
                wasapi.SetPosFrame((long)slider1.Value);
            }

            mLastSliderValue = 0;
            mSliderSliding = false;
        }


        struct InspectFormat {
            public int sampleRate;
            public int bitsPerSample;
            public int validBitsPerSample;
            public int bitFormat; // 0:Int, 1:Float
            public InspectFormat(int sr, int bps, int vbps, int bf) {
                sampleRate = sr;
                bitsPerSample = bps;
                validBitsPerSample = vbps;
                bitFormat = bf;
            }
        };

        const int TEST_SAMPLE_RATE_NUM = 8;
        const int TEST_BIT_REPRESENTATION_NUM = 5;

        static readonly InspectFormat [] gInspectFormats = new InspectFormat [] {
                new InspectFormat(44100, 16, 16, 0),
                new InspectFormat(48000, 16, 16, 0),
                new InspectFormat(88200, 16, 16, 0),
                new InspectFormat(96000, 16, 16, 0),
                new InspectFormat(176400, 16, 16, 0),
                new InspectFormat(192000, 16, 16, 0),
                new InspectFormat(352800, 16, 16, 0),
                new InspectFormat(384000, 16, 16, 0),

                new InspectFormat(44100, 24, 24, 0),
                new InspectFormat(48000, 24, 24, 0),
                new InspectFormat(88200, 24, 24, 0),
                new InspectFormat(96000, 24, 24, 0),
                new InspectFormat(176400, 24, 24, 0),
                new InspectFormat(192000, 24, 24, 0),
                new InspectFormat(352800, 24, 24, 0),
                new InspectFormat(384000, 24, 24, 0),

                new InspectFormat(44100, 32, 24, 0),
                new InspectFormat(48000, 32, 24, 0),
                new InspectFormat(88200, 32, 24, 0),
                new InspectFormat(96000, 32, 24, 0),
                new InspectFormat(176400, 32, 24, 0),
                new InspectFormat(192000, 32, 24, 0),
                new InspectFormat(352800, 32, 24, 0),
                new InspectFormat(384000, 32, 24, 0),

                new InspectFormat(44100, 32, 32, 0),
                new InspectFormat(48000, 32, 32, 0),
                new InspectFormat(88200, 32, 32, 0),
                new InspectFormat(96000, 32, 32, 0),
                new InspectFormat(176400, 32, 32, 0),
                new InspectFormat(192000, 32, 32, 0),
                new InspectFormat(352800, 32, 32, 0),
                new InspectFormat(384000, 32, 32, 0),

                new InspectFormat(44100, 32, 32, 1),
                new InspectFormat(48000, 32, 32, 1),
                new InspectFormat(88200, 32, 32, 1),
                new InspectFormat(96000, 32, 32, 1),
                new InspectFormat(176400, 32, 32, 1),
                new InspectFormat(192000, 32, 32, 1),
                new InspectFormat(352800, 32, 32, 1),
                new InspectFormat(384000, 32, 32, 1),
            };

        private void buttonInspectDevice_Click(object sender, RoutedEventArgs e) {
            string dn = wasapi.GetDeviceName(listBoxDevices.SelectedIndex);
            string did = wasapi.GetDeviceIdString(listBoxDevices.SelectedIndex);

            AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.InspectDevice()\r\nDeviceFriendlyName={0}\r\nDeviceIdString={1}\r\n", dn, did));
            AddLogText("++-------------++-------------++-------------++-------------++-------------++-------------++-------------++-------------++\r\n");
            for (int fmt = 0; fmt < TEST_BIT_REPRESENTATION_NUM; ++fmt) {
                var sb = new StringBuilder();
                for (int sr =0; sr < TEST_SAMPLE_RATE_NUM; ++sr) {
                    int idx = sr + fmt * TEST_SAMPLE_RATE_NUM;
                    System.Diagnostics.Debug.Assert(idx < gInspectFormats.Length);
                    InspectFormat ifmt = gInspectFormats[idx];
                    sb.Append(string.Format(CultureInfo.InvariantCulture, "||{0,3}kHz {1}{2}V{3}",
                        ifmt.sampleRate / 1000, ifmt.bitFormat == 0 ? "i" : "f",
                        ifmt.bitsPerSample, ifmt.validBitsPerSample));
                }
                sb.Append("||\r\n");
                AddLogText(sb.ToString());

                sb.Clear();
                for (int sr =0; sr < TEST_SAMPLE_RATE_NUM; ++sr) {
                    int idx = sr + fmt * TEST_SAMPLE_RATE_NUM;
                    System.Diagnostics.Debug.Assert(idx < gInspectFormats.Length);
                    InspectFormat ifmt = gInspectFormats[idx];
                    int hr = wasapi.InspectDevice(listBoxDevices.SelectedIndex, ifmt.sampleRate, ifmt.bitsPerSample, ifmt.validBitsPerSample, ifmt.bitFormat);
                    sb.Append(string.Format(CultureInfo.InvariantCulture, "|| {0} {1:X8} ", hr==0 ? "OK" : "NA", hr));
                }
                sb.Append("||\r\n");
                AddLogText(sb.ToString());
                AddLogText("++-------------++-------------++-------------++-------------++-------------++-------------++-------------++-------------++\r\n");
            }
        }

        /// <summary>
        /// SettingsWindowによって変更された表示情報をUIに反映する。
        /// </summary>
        void UpdateWindowSettings() {
            FontFamilyConverter ffc = new FontFamilyConverter();
            FontFamily ff = ffc.ConvertFromString(m_preference.PlayingTimeFontName) as FontFamily;
            if (null != ff) {
                labelPlayingTime.FontFamily = ff;
            }
            labelPlayingTime.FontSize = m_preference.PlayingTimeSize;
            labelPlayingTime.FontWeight = m_preference.PlayingTimeFontBold
                ? FontWeights.Bold
                : FontWeights.Normal;

            sliderWindowScaling.Value = m_preference.WindowScale;

            UpdateUIStatus();
        }

        List<string> m_logList = new List<string>();
        int m_logLineNum = 100;

        /// <summary>
        /// ログを追加する。
        /// </summary>
        /// <param name="s">追加するログ。行末に\r\nを入れる必要あり。</param>
        private void AddLogText(string s) {
            Console.Write(s);

            // ログを適当なエントリ数で流れるようにする。
            // sは複数行の文字列が入っていたり、改行が入っていなかったりするので、行数制限にはなっていない。
            m_logList.Add(s);
            while (m_logLineNum < m_logList.Count) {
                m_logList.RemoveAt(0);
            }

            StringBuilder sb = new StringBuilder();
            foreach (var item in m_logList) {
                sb.Append(item);
            }

            textBoxLog.Text = sb.ToString();
            textBoxLog.ScrollToEnd();
        }

        /// <summary>
        /// ロード中に選択曲が変更された場合、ロード後に再生曲変更処理を行う。
        /// ChangePlayWavDataById()でセットし
        /// ReadFileRunWorkerCompleted()で参照する。
        /// </summary>
        private PlayListItemInfo m_pliUpdatedByUserSelectWhileLoading = null;

        /// <summary>
        /// 再生中に、再生曲をwavDataIdの曲に切り替える。
        /// wavDataIdの曲がロードされていたら、直ちに再生曲切り替え。
        /// ロードされていなければ、グループをロードしてから再生。
        /// 
        /// 再生中でない場合は、最初に再生する曲をwavDataIdの曲に変更する。
        /// </summary>
        /// <param name="wavDataId">再生曲</param>
        private void ChangePlayWavDataById(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);

            int playingId = wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.NowPlaying);
            int pauseResumeId = wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.PauseResumeToPlay);
            if (playingId < 0 && pauseResumeId < 0 && 0 <= m_loadingGroupId) {
                // 再生中でなく、ロード中の場合。
                // ロード完了後ReadFileRunWorkerCompleted()で再生する曲を切り替えるための
                // 情報をセットする。
                m_pliUpdatedByUserSelectWhileLoading
                    = m_playListItems[dataGridPlayList.SelectedIndex];
                return;
            }

            if (0 <= pauseResumeId) {
                // 再生一時停止中の場合。
                wasapi.UpdatePlayPcmDataById(wavDataId);
            }
            if (playingId < 0) {
                // 再生中でなく、ロード中でもない場合。
                wasapi.UpdatePlayPcmDataById(wavDataId);
                return;
            }

            // 再生中である。
            var pcmData = FindPcmDataById(m_pcmDataListForPlay, wavDataId);
            if (null == pcmData) {
                // 1曲再生の時起きる。
                StopAsync(new Task(TaskType.PlaySpecifiedGroup, 0, wavDataId));
                return;
            }

            int groupId = pcmData.GroupId;

            var playPcmData = FindPcmDataById(m_pcmDataListForPlay, playingId);
            if (playPcmData.GroupId == groupId) {
                // 同一ファイルグループのファイルの場合、すぐにこの曲が再生可能。
                wasapi.UpdatePlayPcmDataById(wavDataId);
                AddLogText(string.Format(CultureInfo.InvariantCulture, "wasapi.UpdatePlayPcmDataById({0})\r\n",
                    wavDataId));
            } else {
                // ファイルグループが違う場合、再生を停止し、グループを読み直し、再生を再開する。
                StopAsync(new Task(TaskType.PlaySpecifiedGroup, groupId, wavDataId));
            }
        }

        /// <summary>
        /// [ここまで一括読み込み]を追加できたら追加する。
        /// 
        /// [ここまで一括読み込み]ボタン押下以外にも、
        /// ギャップ→ここまで変換からも呼び出される。
        /// </summary>
        private void AddKokomade() {
            if (0 == m_playListItems.Count) {
                return;
            }

            m_playListItems[m_playListItems.Count() - 1].ReadSeparaterAfter = true;

            //m_playListView.RefreshCollection();
            ++m_groupIdNextAdd;
        }

        #region しょーもない関数群

        private static WasapiCS.SchedulerTaskType
        PreferenceSchedulerTaskTypeToWasapiCSSchedulerTaskType(
            RenderThreadTaskType t) {
            switch (t) {
            case RenderThreadTaskType.None:
                return WasapiCS.SchedulerTaskType.None;
            case RenderThreadTaskType.Audio:
                return WasapiCS.SchedulerTaskType.Audio;
            case RenderThreadTaskType.ProAudio:
                return WasapiCS.SchedulerTaskType.ProAudio;
            case RenderThreadTaskType.Playback:
                return WasapiCS.SchedulerTaskType.Playback;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.SchedulerTaskType.None; ;
            }
        }

        private static WasapiCS.ShareMode
        PreferenceShareModeToWasapiCSShareMode(WasapiSharedOrExclusiveType t) {
            switch (t) {
            case WasapiSharedOrExclusiveType.Shared:
                return WasapiCS.ShareMode.Shared;
            case WasapiSharedOrExclusiveType.Exclusive:
                return WasapiCS.ShareMode.Exclusive;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.ShareMode.Exclusive;
            }
        }

        private static WasapiCS.DataFeedMode
        PreferenceDataFeedModeToWasapiCS(WasapiDataFeedModeType t) {
            switch (t) {
            case WasapiDataFeedModeType.EventDriven:
                return WasapiCS.DataFeedMode.EventDriven;
            case WasapiDataFeedModeType.TimerDriven:
                return WasapiCS.DataFeedMode.TimerDriven;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.DataFeedMode.EventDriven;
            }
        }

        private static WasapiCS.BitFormatType
        VrtToBft(PcmDataLib.PcmData.ValueRepresentationType vrt) {
            switch (vrt) {
            case PcmDataLib.PcmData.ValueRepresentationType.SInt:
                return WasapiCS.BitFormatType.SInt;
            case PcmDataLib.PcmData.ValueRepresentationType.SFloat:
                return WasapiCS.BitFormatType.SFloat;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.BitFormatType.SInt;
            }
        }
        #endregion

        // イベント処理 /////////////////////////////////////////////////////

        private void buttonSettings_Click(object sender, RoutedEventArgs e) {
            SettingsWindow sw = new SettingsWindow();
            sw.SetPreference(m_preference);
            sw.ShowDialog();

            UpdateWindowSettings();
        }

        private void radioButtonExclusive_Checked(object sender, RoutedEventArgs e) {
            m_preference.WasapiSharedOrExclusive = WasapiSharedOrExclusiveType.Exclusive;
        }

        private void radioButtonShared_Checked(object sender, RoutedEventArgs e) {
            m_preference.WasapiSharedOrExclusive = WasapiSharedOrExclusiveType.Shared;
        }

        private void radioButtonEventDriven_Checked(object sender, RoutedEventArgs e) {
            m_preference.WasapiDataFeedMode = WasapiDataFeedModeType.EventDriven;
        }

        private void radioButtonTimerDriven_Checked(object sender, RoutedEventArgs e) {
            m_preference.WasapiDataFeedMode = WasapiDataFeedModeType.TimerDriven;
        }

        private void buttonRemovePlayList_Click(object sender, RoutedEventArgs e) {
            var selectedItemCount = dataGridPlayList.SelectedItems.Count;
            if (0 == selectedItemCount) {
                return;
            }

            if (selectedItemCount == m_playListItems.Count) {
                // すべて消える。再生開始などが出来なくなるので別処理。
                ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
                return;
            }

            {
                // 再生リストの一部項目が消える。
                // PcmDataのIDが飛び飛びになるので番号を振り直す。
                // PcmDataのGroupIdも飛び飛びになるが、特に問題にならないようなので付け直さない。
                int idx;
                while (0 <= (idx = dataGridPlayList.SelectedIndex)) {
                    m_pcmDataListForDisp.RemoveAt(idx);
                    m_playListItems.RemoveAt(idx);
                    dataGridPlayList.UpdateLayout();
                }
                GC.Collect();

                for (int i = 0; i < m_pcmDataListForDisp.Count(); ++i) {
                    m_pcmDataListForDisp[i].Id = i;
                    m_pcmDataListForDisp[i].Ordinal = i;
                }
                dataGridPlayList.UpdateLayout();

                UpdateUIStatus();
            }
        }

        private delegate int UpdateOrdinal(int v);

        private void buttonNextOrPrevClicked(UpdateOrdinal updateOrdinal) {
            int wavDataId = wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.NowPlaying);
            var playingPcmData = FindPcmDataById(m_pcmDataListForPlay, wavDataId);
            if (null == playingPcmData) {
                return;
            }

            var ordinal = playingPcmData.Ordinal;
            ordinal = updateOrdinal(ordinal);
            if (ordinal < 0) {
                ordinal = 0;
            }

            if (IsPlayModeOneTrack()) {
                // 1曲再生モードの時
                if (m_pcmDataListForDisp.Count <= ordinal) {
                    ordinal = 0;
                }
                ChangePlayWavDataById(m_pcmDataListForDisp[ordinal].Id);
            } else {
                // 全曲再生またはシャッフル再生モードの時。
                if (m_pcmDataListForPlay.Count <= ordinal) {
                    ordinal = 0;
                }
                ChangePlayWavDataById(m_pcmDataListForPlay[ordinal].Id);
            }
        }

        private void buttonNext_Click(object sender, RoutedEventArgs e) {
            buttonNextOrPrevClicked((x) => { return ++x; });
        }

        private void buttonPrev_Click(object sender, RoutedEventArgs e) {
            buttonNextOrPrevClicked((x) => { return --x; });
        }

        private void checkBoxContinuous_CheckedChanged(object sender, RoutedEventArgs e) {
            if (buttonStop.IsEnabled) {
                // 再生中に連続再生かどうかが変更された。
                UpdatePlayRepeat();
            }
        }

        private void dataGrid1_LoadingRow(object sender, DataGridRowEventArgs e) {
            e.Row.MouseDoubleClick += new MouseButtonEventHandler(dataGridPlayList_RowMouseDoubleClick);
        }

        private void dataGridPlayList_RowMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (m_state == State.プレイリストあり && e.ChangedButton == MouseButton.Left && dataGridPlayList.IsReadOnly) {
                // 再生されていない状態で、再生リスト再生モードで項目左ボタンダブルクリックされたら再生開始する
                buttonPlay_Click(sender, e);
            }
        }

        private void dataGridPlayList_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            m_playListMouseDown = true;

        }

        private void dataGridPlayList_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            m_playListMouseDown = false;
        }

        private void dataGridPlayList_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e) {
            /*
                if (m_state == State.プレイリストあり && 0 <= dataGridPlayList.SelectedIndex) {
                    buttonRemovePlayList.IsEnabled = true;
                } else {
                    buttonRemovePlayList.IsEnabled = false;
                }
            */
        }

        private void dataGridPlayList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UpdateCoverart();

            if (m_state == State.プレイリストあり && 0 <= dataGridPlayList.SelectedIndex) {
                buttonDelistSelected.IsEnabled = true;
            } else {
                buttonDelistSelected.IsEnabled = false;
            }

            if (null == wasapi) {
                return;
            }

            if (!m_playListMouseDown ||
                dataGridPlayList.SelectedIndex < 0 ||
                m_playListItems.Count() <= dataGridPlayList.SelectedIndex) {
                return;
            }

            PlayListItemInfo pli = m_playListItems[dataGridPlayList.SelectedIndex];
            if (pli.PcmData() == null) {
                // 曲じゃない部分を選択したら無視。
                return;
            }

            if (m_state != State.再生中) {
                ChangePlayWavDataById(pli.PcmData().Id);
                return;
            }

            // 再生中の場合。

            int playingId = wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.NowPlaying);
            if (playingId < 0) {
                return;
            }

            // 再生中で、しかも、マウス押下中にこのイベントが来た場合で、
            // しかも、この曲を再生していない場合、この曲を再生する。
            if (null != pli.PcmData() &&
                playingId != pli.PcmData().Id) {
                ChangePlayWavDataById(pli.PcmData().Id);
            }
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e) {
            Exit();
        }

        private void buttonMinimize_Click(object sender, RoutedEventArgs e) {
            WindowState = System.Windows.WindowState.Minimized;
        }

        private Point mPrevPos;

        bool IsWindowMoveMode(MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) {
                return false;
            }

            foreach (MenuItem mi in menu1.Items) {
                if (mi.IsMouseOver) {
                    return false;
                }
            }
            return true;
        }

        private void menu1_MouseMove(object sender, MouseEventArgs e) {
            if (IsWindowMoveMode(e)) {
                Point pos = e.GetPosition(this);
                Point delta = new Point(pos.X - mPrevPos.X, pos.Y - mPrevPos.Y);

                Left += delta.X;
                Top += delta.Y;
            } else {
                mPrevPos = e.GetPosition(this);
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                Keyboard.IsKeyDown(Key.RightCtrl)) {
                // CTRL + マウスホイールで画面のスケーリング

                double scaling = sliderWindowScaling.Value;
                if (e.Delta < 0) {
                    // 1.25の128乗根 = 1.001744829441175331741294013303
                    scaling /= 1.001744829441175331741294013303;
                } else {
                    scaling *= 1.001744829441175331741294013303;
                }
                sliderWindowScaling.Value = scaling;
                m_preference.WindowScale = scaling;
            }
        }

        /// <summary>
        /// デバイスが突然消えたとか、突然増えたとかのイベント。
        /// </summary>
        private void WasapiStatusChanged(StringBuilder idStr) {
            Console.WriteLine("WasapiStatusChanged {0}", idStr);
            Dispatcher.BeginInvoke(new Action(delegate() {
                // AddLogText(string.Format(CultureInfo.InvariantCulture, Properties.Resources.DeviceStateChanged + "\r\n", idStr));
                switch (m_state)
                {
                    case State.未初期化:
                        return;
                    case State.初期化完了:
                    case State.プレイリストあり:
                        // 再生中ではない場合、デバイス一覧を更新する。
                        // DeviceDeselect();
                        UpdateDeviceList();
                        break;
                    case State.デバイスSetup完了:
                    case State.ファイル読み込み完了:
                    case State.再生グループ切り替え中:
                    case State.再生一時停止中:
                    case State.再生中:
                    case State.再生停止開始:
                        // 再生に使用しているデバイスの状態が変化した場合、再生停止する。
                        // そうではない場合、ここでは何もしない。再生停止時に兎に角デバイス一覧が更新される。
                        var useDeviceIdStr = wasapi.GetUseDeviceIdString();
                        var useDeviceName  = wasapi.GetUseDeviceName();
                        if (0 == string.Compare(useDeviceIdStr, idStr.ToString(), StringComparison.Ordinal)) {
                            AddLogText(string.Format(CultureInfo.InvariantCulture, Properties.Resources.UsingDeviceStateChanged + "\r\n", useDeviceName, useDeviceIdStr));
                            StopBlocking();
                            DeviceDeselect();
                            UpdateDeviceList();
                        }
                        break;
                }
            }));
        }
        
        #region ドラッグアンドドロップ

        private void dataGridPlayList_CheckDropTarget(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                // ファイルのドラッグアンドドロップ。
                // ここでハンドルせず、MainWindowのMainWindowDragDropに任せる。
                e.Handled = false;
                return;
            }

            e.Handled = true;
            DataGridRow row = FindVisualParent<DataGridRow>(e.OriginalSource as UIElement);
            if (row == null || !(row.Item is PlayListItemInfo)) {
                // 行がドラッグされていない。
                e.Effects = DragDropEffects.None;
            } else {
                // 行がドラッグされている。
                // Id列を選択している場合のみドラッグアンドドロップ可能。
                //if (0 != "Id".CompareTo(dataGridPlayList.CurrentCell.Column.Header)) {
                //    e.Effects = DragDropEffects.None;
                //}
                // e.Effects = DragDropEffects.Move;
            }
        }

        private void dataGridPlayList_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                // ファイルのドラッグアンドドロップ。
                // ここでハンドルせず、MainWindowのMainWindowDragDropに任せる。
                e.Handled = false;
                return;
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
            DataGridRow row = FindVisualParent<DataGridRow>(e.OriginalSource as UIElement);
            if (row == null || !(row.Item is PlayListItemInfo)) {
                // 行がドラッグされていない。(セルがドラッグされている)
            } else {
                // 再生リスト項目のドロップ。
                m_dropTargetPlayListItem = row.Item as PlayListItemInfo;
                if (m_dropTargetPlayListItem != null) {
                    e.Effects = DragDropEffects.Move;
                }
            }
        }

        private void dataGridPlayList_MouseMove(object sender, MouseEventArgs e) {
            if (m_state == State.再生中 ||
                m_state == State.再生一時停止中) {
                // 再生中は再生リスト項目入れ替え不可能。
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed) {
                // 左マウスボタンが押されていない。
                return;
            }

            DataGridRow row = FindVisualParent<DataGridRow>(e.OriginalSource as FrameworkElement);
            if ((row == null) || !row.IsSelected) {
                Console.WriteLine("MouseMove row==null || !row.IsSelected");
                return;
            }

            PlayListItemInfo pli = row.Item as PlayListItemInfo;

            // MainWindow.Drop()イベントを発生させる(ブロック)。
            DragDropEffects finalDropEffect = DragDrop.DoDragDrop(row, pli, DragDropEffects.Move);
            if (finalDropEffect == DragDropEffects.Move && m_dropTargetPlayListItem != null) {
                // ドロップ操作実行。
                // Console.WriteLine("MouseMove do move");

                int oldIndex = m_playListItems.IndexOf(pli);
                int newIndex = m_playListItems.IndexOf(m_dropTargetPlayListItem);
                if (oldIndex != newIndex) {
                    // 項目が挿入された。PcmDataも挿入処理する。
                    m_playListItems.Move(oldIndex, newIndex);
                    PcmDataListItemsMove(oldIndex, newIndex);
                    // m_playListView.RefreshCollection();
                    dataGridPlayList.UpdateLayout();
                }
                m_dropTargetPlayListItem = null;
            }
        }

        private static T FindVisualParent<T>(UIElement element) where T : UIElement {
            UIElement parent = element;
            while (parent != null) {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null) {
                    return correctlyTyped;
                }

                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
            return null;
        }

        private PlayListItemInfo m_dropTargetPlayListItem = null;

        #endregion

        /// <summary>
        /// m_pcmDataListForDispのIdとGroupIdをリナンバーする。
        /// </summary>
        private void PcmDataListForDispItemsRenumber() {
            m_groupIdNextAdd = 0;
            for (int i = 0; i < m_pcmDataListForDisp.Count(); ++i) {
                PcmData pcmData = m_pcmDataListForDisp[i];
                PlayListItemInfo pli = m_playListItems[i];

                if (0 < i) {
                    PcmData prevPcmData = m_pcmDataListForDisp[i - 1];
                    PlayListItemInfo prevPli = m_playListItems[i - 1];

                    if (prevPli.ReadSeparaterAfter || !pcmData.IsSameFormat(prevPcmData)) {
                        /* 1つ前の項目にReadSeparatorAfterフラグが立っている、または
                         * 1つ前の項目とPCMフォーマットが異なる。
                         * ファイルグループ番号を更新する。
                         */
                        ++m_groupIdNextAdd;
                    }
                }

                pcmData.Id = i;
                pcmData.Ordinal = i;
                pcmData.GroupId = m_groupIdNextAdd;
            }
        }

        /// <summary>
        /// oldIdxの項目をnewIdxの項目の後に挿入する。
        /// </summary>
        private void PcmDataListItemsMove(int oldIdx, int newIdx) {
            System.Diagnostics.Debug.Assert(oldIdx != newIdx);

            /* oldIdx==0, newIdx==1, Count==2の場合
             * remove(0)
             * insert(1)
             * 
             * oldIdx==1, newIdx==0, Count==2の場合
             * remove(1)
             * insert(0)
             */

            PcmData old = m_pcmDataListForDisp[oldIdx];
            m_pcmDataListForDisp.RemoveAt(oldIdx);
            m_pcmDataListForDisp.Insert(newIdx, old);

            // Idをリナンバーする。
            PcmDataListForDispItemsRenumber();
        }

        void PlayListItemInfoPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "ReadSeparaterAfter") {
                // グループ番号をリナンバーする。
                PcmDataListForDispItemsRenumber();
            }
        }

        private void buttonClearPlayList_Click(object sender, RoutedEventArgs e) {
            ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
        }

        private void buttonPlayListItemEditMode_Click(object sender, RoutedEventArgs e) {
            dataGridPlayList.IsReadOnly = !dataGridPlayList.IsReadOnly;

            // dataGridPlayList.IsReadOnlyを見て、他の関連メニュー項目状態が更新される
            UpdateUIStatus();
        }
    }
}
