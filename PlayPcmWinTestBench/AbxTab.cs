using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Wasapi;
using PcmDataLib;
using System.IO;
using WavRWLib2;
using WasapiPcmUtil;

namespace PlayPcmWinTestBench {
    public partial class MainWindow : Window {
        private BackgroundWorker m_playWorker;

        private void InitAbxTab() {
            m_playWorker = new BackgroundWorker();
            m_playWorker.WorkerReportsProgress = true;
            m_playWorker.DoWork += new DoWorkEventHandler(PlayDoWork);
            m_playWorker.ProgressChanged += new ProgressChangedEventHandler(PlayProgressChanged);
            m_playWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(PlayRunWorkerCompleted);
            m_playWorker.WorkerSupportsCancellation = true;

            ListDevices();

            m_testInfoList.Clear();
            PrepareNextTest();

            UpdateAbxTabUIStatus();
        }

        enum AB {
            Unknown = -1,
            A,
            B
        };

        class TestInfo {
            public AB x;
            public AB answer;

            public TestInfo(AB x) {
                this.x = x;
                answer = AB.Unknown;
            }

            public void SetAnswer(AB answer) {
                this.answer = answer;
            }
        }

        List<TestInfo> m_testInfoList = new List<TestInfo>();

        private AB GetX() {
            return m_testInfoList[m_testInfoList.Count - 1].x;
        }

        /// <summary>
        /// 回答する。
        /// </summary>
        /// <param name="answer"></param>
        /// <returns>true:まだ続きがある。false:終わり</returns>
        private bool Answer(AB answer) {
            m_testInfoList[m_testInfoList.Count - 1].answer = answer;
            if (10 == m_testInfoList.Count) {
                return false;
            }
            return true;
        }

        private void PrepareNextTest() {
            byte[] r = new byte[1];
            gen.GetBytes(r);

            AB x = ((r[0] & 1) == 0) ? AB.A : AB.B;
            TestInfo ti = new TestInfo(x);
            m_testInfoList.Add(ti);
        }

        private void ComboBoxDeviceInit(ComboBox comboBox) {
            int selectedIdx = comboBox.SelectedIndex;

            comboBox.Items.Clear();

            int nDevices = wasapi.GetDeviceCount();
            for (int i = 0; i < nDevices; ++i) {
                var attr = wasapi.GetDeviceAttributes(i);
                comboBox.Items.Add(attr.Name);
            }

            if (0 <= selectedIdx && selectedIdx < nDevices) {
                comboBox.SelectedIndex = selectedIdx;
            } else if (0 < nDevices) {
                comboBox.SelectedIndex = 0;
            }
        }

        private void ListDevices() {
            int hr = wasapi.EnumerateDevices(WasapiCS.DeviceType.Play);

            ComboBoxDeviceInit(comboBoxDeviceA);
            ComboBoxDeviceInit(comboBoxDeviceB);
        }

        /// <summary>
        /// 再生中。バックグラウンドスレッド。
        /// </summary>
        private void PlayDoWork(object o, DoWorkEventArgs args) {
            //Console.WriteLine("PlayDoWork started");
            BackgroundWorker bw = (BackgroundWorker)o;

            while (!wasapi.Run(100)) {
                m_playWorker.ReportProgress(0);
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
        }

        /// <summary>
        /// 再生終了。
        /// </summary>
        private void PlayRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            m_status = Status.Stop;

            wasapi.Unsetup();
            UnchooseRecreateDeviceList();
        }

        enum Status {
            Stop,
            Play,
        }

        private Status m_status = Status.Stop;

        public void UpdateAbxTabUIStatus() {
            labelGuide.Content = string.Format("テスト{0}回目", m_testInfoList.Count);

            if (m_status != Status.Play) {
                // Stop
                buttonPlayA.IsEnabled = false;
                buttonPlayB.IsEnabled = false;
                buttonPlayX.IsEnabled = false;
                buttonPlayY.IsEnabled = false;
                buttonConfirm.IsEnabled = false;
                buttonStopA.IsEnabled = false;
                buttonStopB.IsEnabled = false;
                buttonStopX.IsEnabled = false;
                buttonStopY.IsEnabled = false;

                if (0 < textBoxPathA.Text.Length && System.IO.File.Exists(textBoxPathA.Text)) {
                    buttonPlayA.IsEnabled = true;
                }
                if (0 < textBoxPathA.Text.Length && System.IO.File.Exists(textBoxPathB.Text)) {
                    buttonPlayB.IsEnabled = true;
                }
                if (buttonPlayA.IsEnabled && buttonPlayB.IsEnabled) {
                    buttonPlayX.IsEnabled = true;
                    buttonPlayY.IsEnabled = true;
                    buttonConfirm.IsEnabled = true;
                }
            } else {
                // Play
                buttonPlayA.IsEnabled = false;
                buttonPlayB.IsEnabled = false;
                buttonPlayX.IsEnabled = false;
                buttonPlayY.IsEnabled = false;
                buttonStopA.IsEnabled = true;
                buttonStopB.IsEnabled = true;
                buttonStopX.IsEnabled = true;
                buttonStopY.IsEnabled = true;
            }
        }

        private void buttonFinish_Click(object sender, RoutedEventArgs e) {
            DispResult();
            DialogResult = true;
            Close();
        }

        private string BrowseOpenFile(string filter) {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = filter;
            dlg.Multiselect = false;

            Nullable<bool> result = dlg.ShowDialog();
            if (result != true) {
                return "";
            }
            return dlg.FileName;
        }

        private string BrowseSaveFile(string filter) {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = filter;

            Nullable<bool> result = dlg.ShowDialog();
            if (result != true) {
                return "";
            }
            return dlg.FileName;
        }

        private string m_filterWav = "WAVEファイル|*.wav;*.wave";
        private string m_filterSaveWav = "WAVEファイル|*.wav";

        private void buttonBrowseA_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile(m_filterWav);
            if (0 < fileName.Length) {
                textBoxPathA.Text = fileName;
                UpdateAbxTabUIStatus();
            }
        }

        private void buttonBrowseB_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile(m_filterWav);
            if (0 < fileName.Length) {
                textBoxPathB.Text = fileName;
                UpdateAbxTabUIStatus();
            }
        }

        private void Stop() {
            if (m_status == Status.Play) {
                buttonStopA.IsEnabled = false;
                buttonStopB.IsEnabled = false;
                buttonStopX.IsEnabled = false;
                buttonStopY.IsEnabled = false;
                wasapi.Stop();
            }
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            Stop();
        }

        private PcmData ReadWavFile(string path) {
            PcmData pcmData = new PcmData();

            using (BinaryReader br = new BinaryReader(
                    File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))) {
                WavData wavData = new WavData();
                bool readSuccess = wavData.ReadHeaderAndSamples(br, 0, -1);
                if (!readSuccess) {
                    return null;
                }
                pcmData.SetFormat(wavData.NumChannels, wavData.BitsPerSample, wavData.BitsPerSample,
                    wavData.SampleRate, wavData.SampleValueRepresentationType, wavData.NumFrames);
                pcmData.SetSampleArray(wavData.GetSampleArray());
            }

            return pcmData;
        }

        private bool WriteWavFile(PcmData pcmData, string path) {
            using (BinaryWriter bw = new BinaryWriter(
                    File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Write))) {
                WavData wavData = new WavData();
                wavData.Set(pcmData.NumChannels, pcmData.BitsPerSample, pcmData.ValidBitsPerSample, pcmData.SampleRate,
                    pcmData.SampleValueRepresentationType, pcmData.NumFrames, pcmData.GetSampleArray());
                wavData.Write(bw);
            }

            return true;
        }

        private SampleFormatInfo m_sampleFormat;

        private int WasapiSetup(
                bool isExclusive,
                bool isEventDriven,
                int sampleRate,
                int pcmDataBitsPerSample,
                int pcmDataValidBitsPerSample,
                PcmDataLib.PcmData.ValueRepresentationType vrt,
                int latencyMillisec) {
            int num = SampleFormatInfo.GetSetupSampleFormatCandidateNum(
                isExclusive ? WasapiSharedOrExclusiveType.Exclusive : WasapiSharedOrExclusiveType.Shared,
                BitsPerSampleFixType.AutoSelect,
                pcmDataValidBitsPerSample, vrt);

            int hr = -1;
            for (int i = 0; i < num; ++i) {
                SampleFormatInfo sf = SampleFormatInfo.CreateSetupSampleFormat(
                    isExclusive ? WasapiSharedOrExclusiveType.Exclusive : WasapiSharedOrExclusiveType.Shared,
                    BitsPerSampleFixType.AutoSelect,
                    pcmDataBitsPerSample,
                    pcmDataValidBitsPerSample, vrt, i);

                hr = wasapi.Setup(WasapiCS.StreamType.PCM, sampleRate, sf.GetSampleFormatType(), 2,
                        WasapiCS.SchedulerTaskType.ProAudio, WasapiCS.ShareMode.Exclusive,
                        isEventDriven ? WasapiCS.DataFeedMode.EventDriven : WasapiCS.DataFeedMode.TimerDriven,
                        latencyMillisec, 500, 10000);
                if (0 <= hr) {
                    m_sampleFormat = sf;
                    return hr;
                }
            }
            wasapi.Unsetup();
            return hr;
        }


        private void UnchooseRecreateDeviceList() {
            wasapi.UnchooseDevice();
            ListDevices();
            UpdateAbxTabUIStatus();
        }

        private void buttonPlayA_Click(object sender, RoutedEventArgs e) {
            PcmData pcmData = ReadWavFile(textBoxPathA.Text);
            if (null == pcmData) {
                MessageBox.Show(
                    string.Format("WAVファイル A 読み込み失敗: {0}", textBoxPathA.Text));
                return;
            }

            int hr = wasapi.ChooseDevice(comboBoxDeviceA.SelectedIndex);
            if (hr < 0) {
                MessageBox.Show(string.Format("Wasapi.ChooseDevice()失敗 {0:X8}", hr));
                UnchooseRecreateDeviceList();
                return;
            }

            hr = WasapiSetup(
                radioButtonExclusiveA.IsChecked == true,
                radioButtonEventDrivenA.IsChecked == true,
                pcmData.SampleRate,
                pcmData.BitsPerSample,
                pcmData.ValidBitsPerSample,
                pcmData.SampleValueRepresentationType,
                Int32.Parse(textBoxLatencyA.Text));
            if (hr < 0) {
                MessageBox.Show(string.Format("Wasapi.Setup失敗 {0:X8}", hr));
                UnchooseRecreateDeviceList();
                return;
            }

            var pcmUtil = new PcmUtil(pcmData.NumChannels);
            pcmData = pcmUtil.BitsPerSampleConvAsNeeded(pcmData, m_sampleFormat.GetSampleFormatType(), null);

            wasapi.ClearPlayList();
            wasapi.AddPlayPcmDataStart();
            wasapi.AddPlayPcmData(0, pcmData.GetSampleArray());
            wasapi.AddPlayPcmDataEnd();

            hr = wasapi.StartPlayback(0);
            m_playWorker.RunWorkerAsync();
            m_status = Status.Play;
            UpdateAbxTabUIStatus();
        }

        private void buttonPlayB_Click(object sender, RoutedEventArgs e) {
            PcmData pcmData = ReadWavFile(textBoxPathB.Text);
            if (null == pcmData) {
                MessageBox.Show(
                    string.Format("WAVファイル B 読み込み失敗: {0}", textBoxPathB.Text));
                return;
            }

            int hr = wasapi.ChooseDevice(comboBoxDeviceB.SelectedIndex);
            if (hr < 0) {
                MessageBox.Show(string.Format("Wasapi.ChooseDevice()失敗 {0:X8}", hr));
                UnchooseRecreateDeviceList();
                return;
            }

            hr = WasapiSetup(
                radioButtonExclusiveB.IsChecked == true,
                radioButtonEventDrivenB.IsChecked == true,
                pcmData.SampleRate,
                pcmData.BitsPerSample,
                pcmData.ValidBitsPerSample,
                pcmData.SampleValueRepresentationType,
                Int32.Parse(textBoxLatencyB.Text));
            if (hr < 0) {
                MessageBox.Show(string.Format("Wasapi.Setup失敗 {0:X8}", hr));
                UnchooseRecreateDeviceList();
                return;
            }

            var pcmUtil = new PcmUtil(pcmData.NumChannels);
            pcmData = pcmUtil.BitsPerSampleConvAsNeeded(pcmData, m_sampleFormat.GetSampleFormatType(), null);

            wasapi.ClearPlayList();
            wasapi.AddPlayPcmDataStart();
            wasapi.AddPlayPcmData(0, pcmData.GetSampleArray());
            wasapi.AddPlayPcmDataEnd();

            hr = wasapi.StartPlayback(0);
            m_playWorker.RunWorkerAsync();
            m_status = Status.Play;
            UpdateAbxTabUIStatus();
        }

        private void buttonPlayX_Click(object sender, RoutedEventArgs e) {
            AB x = GetX();
            if (x == AB.A) {
                buttonPlayA_Click(sender, e);
            } else {
                buttonPlayB_Click(sender, e);
            }
        }

        private void buttonPlayY_Click(object sender, RoutedEventArgs e) {
            AB x = GetX();
            if (x == AB.A) {
                buttonPlayB_Click(sender, e);
            } else {
                buttonPlayA_Click(sender, e);
            }
        }

        private void buttonConfirm_Click(object sender, RoutedEventArgs e) {
            if (!Answer(radioButtonAXBY.IsChecked == true ? AB.A : AB.B)) {
                // 終わり
                DispResult();
                DialogResult = true;
                Close();
                return;
            }
            PrepareNextTest();
            UpdateAbxTabUIStatus();
        }

        private void DispResult() {
            if (m_testInfoList.Count == 1 && m_testInfoList[0].answer == AB.Unknown) {
                return;
            }
            string s = "結果発表\r\n\r\n";

            int answered = 0;
            int correct = 0;
            for (int i=0; i < m_testInfoList.Count; ++i) {
                TestInfo ti = m_testInfoList[i];
                if (ti.answer == AB.Unknown) {
                    break;
                }

                ++answered;
                s += string.Format("テスト{0}回目 {1} 正解は X={2},Y={3}\r\n",
                    i, ti.x == ti.answer ? "○" : "×",
                    ti.x == AB.A ? "A" : "B",
                    ti.x == AB.A ? "B" : "A");
                if (ti.x == ti.answer) {
                    ++correct;
                }
            }
            s += string.Format("\r\n{0}回中{1}回正解", answered, correct);
            MessageBox.Show(s);
        }


    }
}
