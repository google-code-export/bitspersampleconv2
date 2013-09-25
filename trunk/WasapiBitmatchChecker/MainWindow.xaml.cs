﻿using System;
using System.Windows;
using Wasapi;
using System.ComponentModel;
using System.Windows.Threading;

namespace WasapiBitmatchChecker {
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        private static string AssemblyVersion {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }
        
        private WasapiCS mWasapiPlay;
        private WasapiCS mWasapiRec;

        private BackgroundWorker mPlayWorker;
        private BackgroundWorker mRecWorker;

        private Wasapi.WasapiCS.CaptureCallback mCaptureDataArrivedDelegate;

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mWasapiPlay = new WasapiCS();
            mWasapiPlay.Init();
            mWasapiPlay.EnumerateDevices(WasapiCS.DeviceType.Play);

            mWasapiRec = new WasapiCS();
            mWasapiRec.Init();
            mWasapiRec.EnumerateDevices(WasapiCS.DeviceType.Rec);
            mCaptureDataArrivedDelegate = new Wasapi.WasapiCS.CaptureCallback(CaptureDataArrived);
            mWasapiRec.RegisterCaptureCallback(mCaptureDataArrivedDelegate);

            mPlayWorker = new BackgroundWorker();
            mPlayWorker.DoWork += new DoWorkEventHandler(PlayDoWork);
            mPlayWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(PlayRunWorkerCompleted);
            mPlayWorker.WorkerSupportsCancellation = true;
            mPlayWorker.WorkerReportsProgress = true;
            mPlayWorker.ProgressChanged += new ProgressChangedEventHandler(PlayWorkerProgressChanged);

            mRecWorker = new BackgroundWorker();
            mRecWorker.DoWork += new DoWorkEventHandler(RecDoWork);
            mRecWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(RecRunWorkerCompleted);
            mRecWorker.WorkerSupportsCancellation = true;

            UpdateDeviceList();

            mSyncTimeout = new DispatcherTimer();
            mSyncTimeout.Tick += new EventHandler(SyncTimeoutTickCallback);
            mSyncTimeout.Interval = new TimeSpan(0, 0, 5);

            textBoxLog.Text = string.Format("WasapiBitmatchChecker version {0}\r\n", AssemblyVersion);

        }

        private void UpdateDeviceList() {
            int prevPlayDeviceIdx = listBoxPlayDevices.SelectedIndex;
            int prevRecDeviceIdx = listBoxRecDevices.SelectedIndex;

            listBoxPlayDevices.Items.Clear();
            for (int i=0; i < mWasapiPlay.GetDeviceCount(); ++i) {
                var attr = mWasapiPlay.GetDeviceAttributes(i);
                listBoxPlayDevices.Items.Add(attr.Name);
            }
            if (0 < listBoxPlayDevices.Items.Count) {
                if (0 <= prevPlayDeviceIdx && prevPlayDeviceIdx < listBoxPlayDevices.Items.Count) {
                    listBoxPlayDevices.SelectedIndex = prevPlayDeviceIdx;
                } else {
                    listBoxPlayDevices.SelectedIndex = 0;
                }
            }

            listBoxRecDevices.Items.Clear();
            for (int i=0; i < mWasapiPlay.GetDeviceCount(); ++i) {
                var attr = mWasapiRec.GetDeviceAttributes(i);
                listBoxRecDevices.Items.Add(attr.Name);
            }
            if (0 < listBoxRecDevices.Items.Count) {
                if (0 <= prevRecDeviceIdx && prevRecDeviceIdx < listBoxRecDevices.Items.Count) {
                    listBoxRecDevices.SelectedIndex = prevRecDeviceIdx;
                } else {
                    listBoxRecDevices.SelectedIndex = 0;
                }
            }

            if (0 < listBoxPlayDevices.Items.Count &&
                    0 < listBoxRecDevices.Items.Count) {
                buttonStart.IsEnabled = true;
            } else {
                buttonStart.IsEnabled = false;
            }

        }

        private void Term() {
            // バックグラウンドスレッドにjoinして、完全に止まるまで待ち合わせするブロッキング版のStopを呼ぶ。
            // そうしないと、バックグラウンドスレッドによって使用中のオブジェクトが
            // この後のUnsetupの呼出によって開放されてしまい問題が起きる。
            StopBlocking();

            if (mWasapiRec != null) {
                mWasapiRec.Unsetup();
                mWasapiRec.UnchooseDevice();
                mWasapiRec.Term();
                mWasapiRec = null;
            }

            if (mWasapiPlay != null) {
                mWasapiPlay.Unsetup();
                mWasapiPlay.UnchooseDevice();
                mWasapiPlay.Term();
                mWasapiPlay = null;
            }
        }

        private void StopBlocking() {
            if (mRecWorker.IsBusy) {
                mRecWorker.CancelAsync();
            }
            while (mRecWorker.IsBusy) {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));

                System.Threading.Thread.Sleep(100);
            }

            if (mPlayWorker.IsBusy) {
                mPlayWorker.CancelAsync();
            }
            while (mPlayWorker.IsBusy) {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));

                System.Threading.Thread.Sleep(100);
            }
        }

        private void Exit() {
            Term();
            // Application.Current.Shutdown();
            Close();
        }

        private void Window_Closed(object sender, EventArgs e) {
            Term();
        }

        enum State {
            Init,
            Syncing,
            Running,
            RecCompleted,
        };

        private static int NUM_PROLOGUE_FRAMES = 262144;
        private int mNumTestFrames = 1024 * 1024;
        private static int NUM_CHANNELS = 2;
        private int mSampleRate;
        private WasapiCS.SampleFormatType mPlaySampleFormat;
        private WasapiCS.SampleFormatType mRecSampleFormat;
        private WasapiCS.DataFeedMode mPlayDataFeedMode;
        private WasapiCS.DataFeedMode mRecDataFeedMode;
        private int mPlayBufferMillisec;
        private int mRecBufferMillisec;

        private DispatcherTimer mSyncTimeout;

        private State mState = State.Init;

        private PcmDataLib.PcmData mPcmSync;
        private PcmDataLib.PcmData mPcmReady;
        private PcmDataLib.PcmData mPcmTest;

        private bool UpdateTestParamsFromUI() {

            if (!Int32.TryParse(textBoxTestFrames.Text, out mNumTestFrames) || mNumTestFrames <= 0) {
                MessageBox.Show("PCM size must be greater than or equals to 1");
                return false;
            }
            mNumTestFrames *= 1024 * 1024;

            if (radioButton44100.IsChecked == true) {
                mSampleRate = 44100;
            }
            if (radioButton48000.IsChecked == true) {
                mSampleRate = 48000;
            }
            if (radioButton88200.IsChecked == true) {
                mSampleRate = 88200;
            }
            if (radioButton96000.IsChecked == true) {
                mSampleRate = 96000;
            }
            if (radioButton176400.IsChecked == true) {
                mSampleRate = 176400;
            }
            if (radioButton192000.IsChecked == true) {
                mSampleRate = 192000;
            }

            if (radioButtonPlayPcm16.IsChecked == true) {
                mPlaySampleFormat = WasapiCS.SampleFormatType.Sint16;
            }
            if (radioButtonPlayPcm24.IsChecked == true) {
                mPlaySampleFormat = WasapiCS.SampleFormatType.Sint24;
            }
            if (radioButtonPlayPcm32v24.IsChecked == true) {
                mPlaySampleFormat = WasapiCS.SampleFormatType.Sint32V24;
            }

            if (radioButtonRecPcm16.IsChecked == true) {
                mRecSampleFormat = WasapiCS.SampleFormatType.Sint16;
            }
            if (radioButtonRecPcm24.IsChecked == true) {
                mRecSampleFormat = WasapiCS.SampleFormatType.Sint24;
            }
            if (radioButtonRecPcm32v24.IsChecked == true) {
                mRecSampleFormat = WasapiCS.SampleFormatType.Sint32V24;
            }

            if (radioButtonPlayEvent.IsChecked == true) {
                mPlayDataFeedMode = WasapiCS.DataFeedMode.EventDriven;
            }
            if (radioButtonPlayTimer.IsChecked == true) {
                mPlayDataFeedMode = WasapiCS.DataFeedMode.TimerDriven;
            }

            if (radioButtonRecEvent.IsChecked == true) {
                mRecDataFeedMode = WasapiCS.DataFeedMode.EventDriven;
            }
            if (radioButtonRecTimer.IsChecked == true) {
                mRecDataFeedMode = WasapiCS.DataFeedMode.TimerDriven;
            }

            if (!Int32.TryParse(textBoxPlayBufferSize.Text, out mPlayBufferMillisec)) {
                MessageBox.Show("Playback buffer size parse error");
                return false;
            }
            if (!Int32.TryParse(textBoxRecBufferSize.Text, out mRecBufferMillisec)) {
                MessageBox.Show("Recording buffer size parse error");
                return false;
            }
            return true;
        }

        Random mRand = new Random();

        private void PreparePcmData() {
            var ss = mWasapiPlay.GetSessionStatus();
            
            mPcmSync  = new PcmDataLib.PcmData();
            mPcmReady = new PcmDataLib.PcmData();
            mPcmTest = new PcmDataLib.PcmData();

            switch (mPlaySampleFormat) {
            case WasapiCS.SampleFormatType.Sint16: {
                    mPcmSync.SetFormat(2, 16, 16, mSampleRate, PcmDataLib.PcmData.ValueRepresentationType.SInt, ss.EndpointBufferFrameNum);
                    var data = new byte[2 * 2 * mPcmSync.NumFrames];
                    data[0] = 4;
                    mPcmSync.SetSampleArray(data);
                    
                    mPcmReady.CopyFrom(mPcmSync);
                    data = new byte[2 * 2 * mPcmSync.NumFrames];
                    data[0] = 3;
                    mPcmReady.SetSampleArray(data);

                    mPcmTest.CopyFrom(mPcmSync);
                    data = new byte[2 * 2 * mNumTestFrames];
                    mRand.NextBytes(data);
                    mPcmTest.SetSampleArray(mNumTestFrames, data);

                    mCapturedPcmData = new byte[2 * 2 * (mNumTestFrames + NUM_PROLOGUE_FRAMES)];
                }
                break;
            case WasapiCS.SampleFormatType.Sint24: {
                    mPcmSync.SetFormat(2, 24, 24, mSampleRate, PcmDataLib.PcmData.ValueRepresentationType.SInt, ss.EndpointBufferFrameNum);
                    var data = new byte[2 * 3 * mPcmSync.NumFrames];
                    data[0] = 4;
                    mPcmSync.SetSampleArray(data);

                    mPcmReady.CopyFrom(mPcmSync);
                    data = new byte[2 * 3 * mPcmSync.NumFrames];
                    data[0] = 3;
                    mPcmReady.SetSampleArray(data);

                    mPcmTest.CopyFrom(mPcmSync);
                    data = new byte[2 * 3 * mNumTestFrames];
                    mRand.NextBytes(data);
                    mPcmTest.SetSampleArray(mNumTestFrames, data);

                    mCapturedPcmData = new byte[2 * 3 * (mNumTestFrames + NUM_PROLOGUE_FRAMES)];
                }
                break;
            case WasapiCS.SampleFormatType.Sint32V24: {
                    mPcmSync.SetFormat(2, 32, 24, mSampleRate, PcmDataLib.PcmData.ValueRepresentationType.SInt, ss.EndpointBufferFrameNum);
                    var data = new byte[2 * 4 * mPcmSync.NumFrames];
                    data[1] = 4;
                    mPcmSync.SetSampleArray(data);

                    mPcmReady.CopyFrom(mPcmSync);
                    data = new byte[2 * 4 * mPcmSync.NumFrames];
                    data[1] = 3;
                    mPcmReady.SetSampleArray(data);

                    mPcmTest.CopyFrom(mPcmSync);
                    data = new byte[2 * 4 * mNumTestFrames];
                    mRand.NextBytes(data);
                    mPcmTest.SetSampleArray(mNumTestFrames, data);

                    mCapturedPcmData = new byte[2 * 4 * (mNumTestFrames + NUM_PROLOGUE_FRAMES)];
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

        }

        /// <summary>
        /// 再生中。バックグラウンドスレッド。
        /// </summary>
        private void PlayDoWork(object o, DoWorkEventArgs args) {
            //Console.WriteLine("PlayDoWork started");
            BackgroundWorker bw = (BackgroundWorker)o;

            while (!mWasapiPlay.Run(100)) {
                System.Threading.Thread.Sleep(1);
                if (bw.CancellationPending) {
                    Console.WriteLine("PlayDoWork() CANCELED");
                    mWasapiPlay.Stop();
                    args.Cancel = true;
                }
                
                var playPosition = mWasapiPlay.GetPlayCursorPosition(WasapiCS.PcmDataUsageType.NowPlaying);
                if (playPosition.TotalFrameNum == mNumTestFrames) {
                    // 本編を再生している時だけプログレスバーを動かす
                    mPlayWorker.ReportProgress((int)(playPosition.PosFrame * 95 / playPosition.TotalFrameNum));
                }
            }

            // 正常に最後まで再生が終わった場合、ここでStopを呼んで、後始末する。
            // キャンセルの場合は、2回Stopが呼ばれることになるが、問題ない!!!
            mWasapiPlay.Stop();

            // 停止完了後タスクの処理は、ここではなく、PlayRunWorkerCompletedで行う。
        }

        private void PlayWorkerProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBar1.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// 再生終了。
        /// </summary>
        private void PlayRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            mWasapiPlay.Unsetup();
            mWasapiPlay.UnchooseDevice();
            // FIXME: この仕様はどうかと思う UnchooseDeviceを呼ぶとデバイス一覧が破壊されるので一覧を再取得する
            mWasapiPlay.EnumerateDevices(WasapiCS.DeviceType.Play);
        }

        private void RecDoWork(object o, DoWorkEventArgs args) {
            BackgroundWorker bw = (BackgroundWorker)o;

            while (!mWasapiRec.Run(100) && mState != State.RecCompleted) {
                System.Threading.Thread.Sleep(1);
                if (bw.CancellationPending) {
                    Console.WriteLine("RecDoWork() CANCELED");
                    mWasapiRec.Stop();
                    args.Cancel = true;
                }
            }

            // キャンセルの場合は、2回Stopが呼ばれることになるが、問題ない!!!
            mWasapiRec.Stop();

            // 停止完了後タスクの処理は、ここではなく、RecRunWorkerCompletedで行う。
        }

        private void RecRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            mWasapiRec.Unsetup();
            mWasapiRec.UnchooseDevice();
            mWasapiRec.EnumerateDevices(WasapiCS.DeviceType.Rec);

            CompareRecordedData();
            textBoxLog.ScrollToEnd();

            // 完了。UIの状態を戻す。
            buttonStart.IsEnabled = false;
            buttonStop.IsEnabled = false;

            groupBoxPcmDataSettings.IsEnabled = true;
            groupBoxPlayback.IsEnabled = true;
            groupBoxRecording.IsEnabled = true;

            progressBar1.Value = 0;

            mState = State.Init;

            UpdateDeviceList(); //< この中でbuttonStart.IsEnabledの状態が適切に更新される
        }

        //=========================================================================================================

        private void buttonStart_Click(object sender, RoutedEventArgs e) {
            if (!UpdateTestParamsFromUI()) {
                return;
            }

            int hr = 0;

            hr = mWasapiPlay.ChooseDevice(listBoxPlayDevices.SelectedIndex);
            if (hr < 0) {
                MessageBox.Show("Playback device select error");
                return;
            }

            hr = mWasapiPlay.Setup(WasapiCS.StreamType.PCM, mSampleRate, mPlaySampleFormat,
                NUM_CHANNELS, WasapiCS.SchedulerTaskType.ProAudio, WasapiCS.ShareMode.Exclusive,
                mPlayDataFeedMode, mPlayBufferMillisec, 1000, 10000);
            if (hr < 0) {
                MessageBox.Show(string.Format("Playback Setup error. {0}Hz {1} {2}ch ProAudio Exclusive {3} {4}ms",
                        mSampleRate, mPlaySampleFormat, NUM_CHANNELS, mPlayDataFeedMode, mPlayBufferMillisec));
                mWasapiPlay.Unsetup();
                mWasapiPlay.UnchooseDevice();
                mWasapiPlay.EnumerateDevices(WasapiCS.DeviceType.Play);
                UpdateDeviceList();
                return;
            }

            PreparePcmData();

            mWasapiPlay.ClearPlayList();
            mWasapiPlay.AddPlayPcmDataStart();
            mWasapiPlay.AddPlayPcmData(0, mPcmSync.GetSampleArray());
            mWasapiPlay.AddPlayPcmData(1, mPcmReady.GetSampleArray());
            mWasapiPlay.AddPlayPcmData(2, mPcmTest.GetSampleArray());
            mWasapiPlay.AddPlayPcmDataEnd();

            mWasapiPlay.SetPlayRepeat(false);
            mWasapiPlay.ConnectPcmDataNext(0, 0);

            hr = mWasapiPlay.StartPlayback(0);
            mPlayWorker.RunWorkerAsync();

            // 録音
            mCapturedBytes = 0;

            hr = mWasapiRec.ChooseDevice(listBoxRecDevices.SelectedIndex);
            if (hr < 0) {
                MessageBox.Show("Error. recording device select failed");
                StopUnsetup();
                return;
            }

            hr = mWasapiRec.Setup(WasapiCS.StreamType.PCM, mSampleRate, mRecSampleFormat,
                NUM_CHANNELS, WasapiCS.SchedulerTaskType.ProAudio, WasapiCS.ShareMode.Exclusive,
                mRecDataFeedMode, mRecBufferMillisec, 1000, 10000);
            if (hr < 0) {
                MessageBox.Show(string.Format("Recording Setup error. {0}Hz {1} {2}ch ProAudio Exclusive {3} {4}ms",
                        mSampleRate, mRecSampleFormat, NUM_CHANNELS, mRecDataFeedMode, mRecBufferMillisec));
                StopUnsetup();
                return;
            }

            textBoxLog.Text += string.Format("Test started. SampleRate={0}Hz, PCM data duration={1} seconds.\r\n", mSampleRate, mNumTestFrames / mSampleRate);
            textBoxLog.Text += string.Format("  Playback:  {0}, buffer size={1}ms, {2}, {3}\r\n",
                    mPlaySampleFormat, mPlayBufferMillisec, mPlayDataFeedMode, listBoxPlayDevices.SelectedItem);
            textBoxLog.Text += string.Format("  Recording: {0}, buffer size={1}ms, {2}, {3}\r\n",
                    mRecSampleFormat, mRecBufferMillisec, mRecDataFeedMode, listBoxRecDevices.SelectedItem);
            textBoxLog.ScrollToEnd();

            groupBoxPcmDataSettings.IsEnabled = false;
            groupBoxPlayback.IsEnabled = false;
            groupBoxRecording.IsEnabled = false;

            buttonStart.IsEnabled = false;
            buttonStop.IsEnabled = true;

            // SYNC失敗タイマーのセット
            mSyncTimeout.Start();

            hr = mWasapiRec.StartRecording();
            mRecWorker.RunWorkerAsync();

            mState = State.Syncing;
        }

        void SyncTimeoutTickCallback(object sender, EventArgs e) {
            mSyncTimeout.Stop();
            textBoxLog.Text += "Error. could not receive Sync signal. Check your S/PDIF cabling.\r\n";
            textBoxLog.ScrollToEnd();
            AbortTest();
        }

        private void StopUnsetup() {
            StopBlocking();
            mWasapiPlay.Unsetup();
            mWasapiPlay.UnchooseDevice();
            mWasapiPlay.EnumerateDevices(WasapiCS.DeviceType.Play);
            mWasapiRec.Unsetup();
            mWasapiRec.UnchooseDevice();
            mWasapiRec.EnumerateDevices(WasapiCS.DeviceType.Rec);
            UpdateDeviceList();
        }

        private void AbortTest() {
            buttonStart.IsEnabled = false;
            buttonStop.IsEnabled = false;

            groupBoxPcmDataSettings.IsEnabled = true;
            groupBoxPlayback.IsEnabled = true;
            groupBoxRecording.IsEnabled = true;

            progressBar1.Value = 0;

            StopUnsetup(); //< この中でbuttonStart.IsEnabledの状態が適切に更新される
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            AbortTest();
        }

        private byte[] mCapturedPcmData;
        private int mCapturedBytes;

        private void CaptureSync(byte[] data) {
            switch (mRecSampleFormat) {
            case WasapiCS.SampleFormatType.Sint16: {
                    int nFrames = (int)(data.Length / 2 / NUM_CHANNELS);
                    int mRecSyncPosInBytes = -1;
                    int zeroSamples = 0;
                    int syncSamples = 0;
                    for (int pos=0; pos < data.Length; pos += 2) {
                        if (data[pos] == 0 && data[pos + 1] == 0) {
                            ++zeroSamples;
                        }
                        if (data[pos] == 4 && data[pos + 1] == 0) {
                            ++syncSamples;
                            mRecSyncPosInBytes = pos;
                        }
                    }
                    if (0 <= mRecSyncPosInBytes && zeroSamples + syncSamples == nFrames * NUM_CHANNELS) {
                        // sync frame arrived
                        mSyncTimeout.Stop();

                        //System.Console.WriteLine("Sync Frame arrived. offset={0}", mRecSyncPosInBytes);

                        Array.Copy(data, mRecSyncPosInBytes, mCapturedPcmData, 0, data.Length - mRecSyncPosInBytes);
                        mCapturedBytes = data.Length - mRecSyncPosInBytes;

                        mWasapiPlay.ConnectPcmDataNext(0, 1);
                        mState = State.Running;
                    }
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        private void CaptureRunning(byte[] data) {
            switch (mRecSampleFormat) {
            case WasapiCS.SampleFormatType.Sint16:
                if (mCapturedBytes + data.Length <= mCapturedPcmData.Length) {
                    Array.Copy(data, 0, mCapturedPcmData, mCapturedBytes, data.Length);
                    mCapturedBytes += data.Length;

                    int capturedFrames = mCapturedBytes / NUM_CHANNELS / (WasapiCS.SampleFormatTypeToUseBitsPerSample(mRecSampleFormat) / 8);

                    //System.Console.WriteLine("Captured {0} frames", capturedFrames);
                } else {
                    // キャプチャー終了. データの整合性チェックはRecRunWorkerCompletedで行う。
                    mState = State.RecCompleted;
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        private void CompareRecordedData() {
            int numTestBytes = mNumTestFrames * NUM_CHANNELS * (WasapiCS.SampleFormatTypeToUseBitsPerSample(mRecSampleFormat) / 8);

            if (mState == State.RecCompleted) {
                // 開始合図位置をサーチ
                int dataStartBytes = -1;

                switch (mRecSampleFormat) {
                case WasapiCS.SampleFormatType.Sint16:
                    for (int pos=0; pos < mCapturedBytes / 2; pos += 2) {
                        if (mCapturedPcmData[pos] == 3 && mCapturedPcmData[pos + 1] == 0) {
                            dataStartBytes = pos;
                            break;
                        }
                    }
                    if (dataStartBytes < 0) {
                        textBoxLog.Text += "Error. Start marker is not found in recorded PCM\r\n";
                        return;
                    }

                    dataStartBytes += mPcmReady.GetSampleArray().Length;

                    if (mCapturedBytes - dataStartBytes < numTestBytes) {
                        textBoxLog.Text += "Error. Recorded data insufficient to analyze.\r\n";
                        return;
                    }

                    var original = mPcmTest.GetSampleArray();
                    for (int i=0; i < numTestBytes; ++i) {
                        if (original[i] != mCapturedPcmData[dataStartBytes + i]) {
                            textBoxLog.Text += string.Format("Test Completed. Received data is different from transmitted data!\r\n  PCM size played = {0} MB ({1} Mbits). Tested PCM Duration = {2} seconds\r\n",
                                    numTestBytes / 1024 / 1024, numTestBytes * 8L / 1024 / 1024, mNumTestFrames / mSampleRate);
                            return;
                        }
                    }

                    textBoxLog.Text += string.Format("Test Completed. Bitmatch transfer succeeded.\r\n  PCM size played = {0} MB ({1} Mbits). Tested PCM Duration = {2} seconds\r\n",
                            numTestBytes / 1024 / 1024, numTestBytes * 8L / 1024 / 1024, mNumTestFrames / mSampleRate);
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    break;
                }
            } else {
                textBoxLog.Text += "Error. Recorded data is insufficient to analyze.\r\n";
            }
        }

        private void CaptureDataArrived(byte[] data) {
            // System.Console.WriteLine("CaptureDataArrived {0} bytes", data.Length);

            switch (mState) {
            case State.Syncing:
                CaptureSync(data);
                break;
            case State.Running:
                CaptureRunning(data);
                break;
            default:
                break;
            }
        }

    }
}
