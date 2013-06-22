using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;

namespace WWAudioFilter {
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            mBackgroundWorker = new BackgroundWorker();
            mBackgroundWorker.WorkerReportsProgress = true;
            mBackgroundWorker.DoWork += new DoWorkEventHandler(Background_DoWork);
            mBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(Background_ProgressChanged);
            mBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Background_RunWorkerCompleted);
        }

        BackgroundWorker mBackgroundWorker;

        private bool mInitialized = false;

        private enum State {
            NotReady,
            Ready,
            ReadFile,
            Converting,
            WriteFile
        }

        private State mState = State.NotReady;

        private List<FilterBase> mFilters = new List<FilterBase>();

        private const int FILTER_FILE_VERSION = 1;

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mInitialized = true;
            Update();
        }

        private void Update() {
            if (!mInitialized) {
                return;
            }

            UpdateFilterSettings();

            switch (mState) {
            case State.NotReady:
                buttonStartConversion.IsEnabled = false;
                break;
            case State.Ready:
                buttonStartConversion.IsEnabled = true;
                break;
            case State.ReadFile:
            case State.Converting:
            case State.WriteFile:
                buttonStartConversion.IsEnabled = false;
                break;
            }
        }

        private void UpdateFilterButtons() {
            switch (mState) {
            case State.NotReady:
            case State.Ready:
                groupBoxFilterSettings.IsEnabled = true;
                if (listBoxFilters.SelectedIndex < 0) {
                    buttonFilterAdd.IsEnabled = true;
                    buttonFilterDelete.IsEnabled = false;
                    buttonFilterEdit.IsEnabled = false;
                    buttonFilterLoad.IsEnabled = true;
                    buttonFilterSaveAs.IsEnabled = false;

                    buttonFilterDown.IsEnabled = false;
                    buttonFilterUp.IsEnabled = false;
                } else {
                    buttonFilterAdd.IsEnabled = true;
                    buttonFilterDelete.IsEnabled = true;
                    buttonFilterEdit.IsEnabled = true;
                    buttonFilterLoad.IsEnabled = true;
                    buttonFilterSaveAs.IsEnabled = true;

                    buttonFilterDown.IsEnabled = listBoxFilters.SelectedIndex != listBoxFilters.Items.Count - 1;
                    buttonFilterUp.IsEnabled = listBoxFilters.SelectedIndex != 0;
                }
                break;
            case State.ReadFile:
            case State.Converting:
            case State.WriteFile:
                groupBoxFilterSettings.IsEnabled = false;
                break;
            }
        }

        private void UpdateFilterSettings() {
            int selectedIdx = listBoxFilters.SelectedIndex;

            listBoxFilters.Items.Clear();
            foreach (var f in mFilters) {
                listBoxFilters.Items.Add(f.ToDescriptionText());
            }

            if (listBoxFilters.Items.Count == 1) {
                // 最初に項目が追加された
                selectedIdx = 0;
            }
            if (0 <= selectedIdx && listBoxFilters.Items.Count <= selectedIdx) {
                // 選択されていた最後の項目が削除された。
                selectedIdx = listBoxFilters.Items.Count - 1;
            }
            listBoxFilters.SelectedIndex = selectedIdx;

            UpdateFilterButtons();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////

        private void buttonFilterAdd_Click(object sender, RoutedEventArgs e) {
            var w = new FilterConfiguration(null);
            w.ShowDialog();

            if (true == w.DialogResult) {
                var f = w.GetFilter();
                mFilters.Add(f);
                Update();
                listBoxFilters.SelectedIndex = listBoxFilters.Items.Count - 1;
            }
        }

        private void buttonFilterEdit_Click(object sender, RoutedEventArgs e) {
            System.Diagnostics.Debug.Assert(0 <= listBoxFilters.SelectedIndex);
            System.Diagnostics.Debug.Assert(listBoxFilters.SelectedIndex < mFilters.Count);

            var w = new FilterConfiguration(mFilters[listBoxFilters.SelectedIndex]);
            w.ShowDialog();

            if (true == w.DialogResult) {
                var f = w.GetFilter();
                mFilters.Add(f);
                Update();
            }
        }

        private void buttonFilterUp_Click(object sender, RoutedEventArgs e) {
            int pos = listBoxFilters.SelectedIndex;
            var tmp = mFilters[pos];
            mFilters.RemoveAt(pos);
            mFilters.Insert(pos - 1, tmp);

            --listBoxFilters.SelectedIndex;

            Update();
        }

        private void buttonFilterDown_Click(object sender, RoutedEventArgs e) {
            int pos = listBoxFilters.SelectedIndex;
            var tmp = mFilters[pos];
            mFilters.RemoveAt(pos);
            mFilters.Insert(pos + 1, tmp);

            ++listBoxFilters.SelectedIndex;

            Update();
        }

        private void buttonFilterDelete_Click(object sender, RoutedEventArgs e) {
            mFilters.RemoveAt(listBoxFilters.SelectedIndex);

            Update();
        }

        private void listBoxFilters_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UpdateFilterButtons();
        }

        private void buttonFilterSaveAs_Click(object sender, RoutedEventArgs e) {
            if (mFilters.Count() == 0) {
                MessageBox.Show(Properties.Resources.NothingToStore);
                return;
            }

            System.Diagnostics.Debug.Assert(0 < mFilters.Count());

            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = Properties.Resources.FilterWWAFilterFiles;
            dlg.ValidateNames = true;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            // 保存する
            try {
                using (StreamWriter w = new StreamWriter(dlg.FileName)) {
                    w.WriteLine("{0} {1}", FILTER_FILE_VERSION, mFilters.Count());
                    foreach (var f in mFilters) {
                        w.WriteLine("{0} {1}", f.FilterType, f.ToSaveText());
                    }
                }
            } catch (IOException ex) {
                MessageBox.Show("{0}", ex.Message);
            } catch (UnauthorizedAccessException ex) {
                MessageBox.Show("{0}", ex.Message);
            }
        }

        private void buttonFilterLoad_Click(object sender, RoutedEventArgs e) {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = Properties.Resources.FilterWWAFilterFiles;
            dlg.ValidateNames = true;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            // 読み込む
            try {
                var filters = new List<FilterBase>();

                using (StreamReader r = new StreamReader(dlg.FileName)) {
                    int filterNum = 0;

                    {
                        // ヘッダ部分。バージョン番号とフィルタの個数が入っている。
                        var s = r.ReadLine();
                        s = s.Trim();
                        var tokens = s.Split(null);
                        if (tokens.Length != 2) {
                            MessageBox.Show("Read failed: " + dlg.FileName);
                            return;
                        }
                        int version;
                        if (!Int32.TryParse(tokens[0], out version) || version != FILTER_FILE_VERSION) {
                            MessageBox.Show(
                                string.Format("Filter file version mismatch. expected version={0}, file version={1}",
                                    FILTER_FILE_VERSION, tokens[0]));
                            return;
                        }

                        if (!Int32.TryParse(tokens[1], out filterNum) || filterNum < 0) {
                            MessageBox.Show(
                                string.Format("Read failed. bad filter count {0}",
                                    tokens[1]));
                            return;
                        }
                    }

                    for (int i=0; i < filterNum; ++i) {
                        var s = r.ReadLine();
                        s = s.Trim();
                        var f = FilterFactory.Create(s);
                        if (null == f) {
                            MessageBox.Show(
                                string.Format("Read failed. line={0}, {1}",
                                    i+2, s));
                        }
                        filters.Add(f);
                    }
                }

                mFilters = filters;
            } catch (IOException ex) {
                MessageBox.Show("{0}", ex.Message);
            } catch (UnauthorizedAccessException ex) {
                MessageBox.Show("{0}", ex.Message);
            }

            Update();
        }

        private void buttonBrowseInputFile_Click(object sender, RoutedEventArgs e) {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = Properties.Resources.FilterFlacFiles;
            dlg.ValidateNames = true;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            textBoxInputFile.Text = dlg.FileName;

            if (0 < textBoxInputFile.Text.Length &&
                    0 < textBoxOutputFile.Text.Length) {
                mState = State.Ready;
            } else {
                mState = State.NotReady;
            }

            Update();
        }

        private void buttonBrowseOutputFile_Click(object sender, RoutedEventArgs e) {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = Properties.Resources.FilterFlacFiles;
            dlg.ValidateNames = true;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            textBoxOutputFile.Text = dlg.FileName;

            if (0 < textBoxInputFile.Text.Length &&
                    0 < textBoxOutputFile.Text.Length) {
                mState = State.Ready;
            } else {
                mState = State.NotReady;
            }

            Update();
        }

        struct AudioDataPerChannel {
            public byte [] data;
            public long offsBytes;
            public long totalSamples;
            public int bitsPerSample;
            public bool overflow;
            public double maxMagnitude;

            public void ResetStatistics() {
                overflow = false;
                maxMagnitude = 0.0;
            }

            public double [] GetPcmInDouble(long count) {
                if (totalSamples <= offsBytes / (bitsPerSample/8) || count <= 0) {
                    return new double[count];
                }

                var result = new double[count];
                var copyCount = result.LongLength;
                if (totalSamples < offsBytes / (bitsPerSample / 8) + copyCount) {
                    copyCount = totalSamples - offsBytes / (bitsPerSample / 8);
                }

                switch (bitsPerSample) {
                case 16:
                    for (var i=0; i<copyCount; ++i) {
                        short v = (short)((data[offsBytes]) + (data[offsBytes+1]<<8));
                        result[i] = v * (1.0 / 32768.0);
                        offsBytes += 2;
                    }
                    break;

                case 24:
                    for (var i=0; i<copyCount; ++i) {
                        int v = (int)((data[offsBytes]<<8) + (data[offsBytes+1]<<16) + (data[offsBytes+2]<<24));
                        result[i] = v * (1.0 / 2147483648.0);
                        offsBytes += 3;
                    }
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    break;
                }
                return result;
            }

            public void SetPcmInDouble(double[] pcm, long writeOffs) {
                var copyCount = pcm.LongLength;
                if (totalSamples < writeOffs + copyCount) {
                    copyCount = totalSamples - writeOffs;
                }

                long writePosBytes;
                switch (bitsPerSample) {
                case 16:
                    writePosBytes = writeOffs*2;
                    for (var i=0; i < copyCount; ++i) {
                        short vS = 0;
                        double vD = pcm[i];
                        if (vD < -1.0f) {
                            vS = -32768;

                            overflow = true;
                            if (maxMagnitude < Math.Abs(vD)) {
                                maxMagnitude = Math.Abs(vD);
                            }
                        } else if (1.0f <= vD) {
                            vS = 32767;

                            overflow = true;
                            if (maxMagnitude < Math.Abs(vD)) {
                                maxMagnitude = Math.Abs(vD);
                            }
                        } else {
                            vS = (short)(32768.0 * vD);
                        }

                        data[writePosBytes + 0] = (byte)((vS     ) & 0xff);
                        data[writePosBytes + 1] = (byte)((vS >> 8) & 0xff);

                        writePosBytes += 2;
                    }
                    break;

                case 24:
                    writePosBytes = writeOffs * 3;
                    for (var i=0; i < copyCount; ++i) {
                        int vI = 0;
                        double vD = pcm[i];
                        if (vD < -1.0f) {
                            vI = Int32.MinValue;

                            overflow = true;
                            if (maxMagnitude < Math.Abs(vD)) {
                                maxMagnitude = Math.Abs(vD);
                            }
                        } else if (1.0f <= vD) {
                            vI = 0x7fffff00;

                            overflow = true;
                            if (maxMagnitude < Math.Abs(vD)) {
                                maxMagnitude = Math.Abs(vD);
                            }
                        } else {
                            vI = (int)(2147483648.0 * vD);
                        }

                        data[writePosBytes + 0] = (byte)((vI >>  8) & 0xff);
                        data[writePosBytes + 1] = (byte)((vI >> 16) & 0xff);
                        data[writePosBytes + 2] = (byte)((vI >> 24) & 0xff);

                        writePosBytes += 3;
                    }
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    break;
                }
            }
        };

        struct AudioData {
            public WWFlacRWCS.Metadata meta;
            public List<AudioDataPerChannel> pcm;
            public byte [] picture;
        };

        class RunWorkerArgs {
            public string FromPath { get; set; }
            public string ToPath { get; set; }

            public RunWorkerArgs(string fromPath, string toPath) {
                FromPath = fromPath;
                ToPath = toPath;
            }
        };

        private void buttonStartConversion_Click(object sender, RoutedEventArgs e) {
            textBoxLog.Text = string.Empty;
            textBoxLog.Text += string.Format("Reading file {0} ...\r\n", textBoxInputFile.Text);
            progressBar1.Value = 0;
            progressBar1.IsEnabled = true;

            mBackgroundWorker.RunWorkerAsync(new RunWorkerArgs(textBoxInputFile.Text, textBoxOutputFile.Text));
        }

        class ProgressArgs {
            public string Message { get; set; }
            public int Result { get; set; }

            public ProgressArgs(string message, int result) {
                Message = message;
                Result = result;
            }
        }

        private int ReadFlacFile(string path, out AudioData ad) {
            ad = new AudioData();

            var flacRW = new WWFlacRWCS.FlacRW();
            int id = flacRW.DecodeAll(path);
            if (id < 0) {
                return id;
            }

            int rv = flacRW.GetDecodedMetadata(id, out ad.meta);
            if (rv < 0) {
                return rv;
            }

            rv = flacRW.GetDecodedPicture(id, out ad.picture, ad.meta.pictureBytes);
            if (rv < 0) {
                return rv;
            }

            ad.pcm = new List<AudioDataPerChannel>();
            for (int ch=0; ch < ad.meta.channels; ++ch) {
                byte [] data;
                long lrv = flacRW.GetDecodedPcmBytes(id, ch, 0, out data, ad.meta.totalSamples * (ad.meta.bitsPerSample / 8));
                if (lrv < 0) {
                    return (int)lrv;
                }

                var adp = new AudioDataPerChannel();
                adp.data = data;
                adp.offsBytes = 0;
                adp.bitsPerSample = ad.meta.bitsPerSample;
                adp.totalSamples = ad.meta.totalSamples;
                ad.pcm.Add(adp);
            }

            return 0;
        }

        private int WriteFlacFile(ref AudioData ad, string path) {
            int rv;
            var flacRW = new WWFlacRWCS.FlacRW();
            int id = flacRW.EncodeInit(ad.meta);
            if (id < 0) {
                return id;
            }

            rv = flacRW.EncodeSetPicture(id, ad.picture);
            if (rv < 0) {
                return rv;
            }

            for (int ch=0; ch < ad.meta.channels; ++ch) {
                long lrv = flacRW.EncodeAddPcm(id, ch, ad.pcm[ch].data);
                if (lrv < 0) {
                    return (int)lrv;
                }
            }

            rv = flacRW.EncodeRun(id, path);
            if (rv < 0) {
                return rv;
            }

            return 0;
        }

        private void SetupResultPcm(AudioData from, out AudioData to) {
            to = new AudioData();

            var fmt = new PcmFormat(from.meta.channels, from.meta.sampleRate, from.meta.totalSamples);
            foreach (var f in mFilters) {
                fmt = f.Setup(fmt);
            }
            to.meta = new WWFlacRWCS.Metadata(from.meta);
            to.meta.sampleRate = fmt.SampleRate;
            to.meta.totalSamples = fmt.NumSamples;
            to.meta.channels = fmt.Channels;

            if (from.picture != null) {
                to.picture = new byte[from.picture.Length];
                System.Array.Copy(from.picture, to.picture, to.picture.Length);
            }

            // allocate to pcm data
            to.pcm = new List<AudioDataPerChannel>();
            for (int ch=0; ch < to.meta.channels; ++ch) {
                var data = new byte[to.meta.totalSamples * (to.meta.bitsPerSample / 8)];
                var adp = new AudioDataPerChannel();
                adp.data = data;
                adp.bitsPerSample = to.meta.bitsPerSample;
                adp.totalSamples = to.meta.totalSamples;
                to.pcm.Add(adp);
            }
        }

        private long CountTotalSamples(List<double[]> data) {
            long count = 0;
            foreach (var k in data) {
                count += k.LongLength;
            }
            return count;
        }

        private void AssembleSample(List<double[]> dataList, long count, out double [] gathered, out double [] remainings) {
            gathered = new double[count];
            long offs = 0;
            long remainLength = 0;
            foreach (var d in dataList) {
                long length = d.LongLength;
                remainLength = 0;
                if (count < offs + length) {
                    length = count - offs;
                    remainLength = d.LongLength - length;
                }

                Array.Copy(d, 0, gathered, offs, length);
                offs += length;
            }

            remainings = new double[remainLength];
            if (0 < remainLength) {
                long lastDataLength = dataList[dataList.Count-1].LongLength;
                Array.Copy(dataList[dataList.Count-1], lastDataLength-remainLength, remainings, 0, remainLength);
            }
        }

        private double [] FilterNth(int nth, ref AudioDataPerChannel from) {
            if (nth == -1) {
                return from.GetPcmInDouble(mFilters[0].NumOfSamplesNeeded());
            } else {
                // サンプル数が貯まるまでn-1番目のフィルターを実行する。
                // n番目のフィルターを実行する

                List<double[]> inPcmList = new List<double[]>();
                {
                    // 前回フィルタ処理で余った入力データ
                    double [] prevRemainings = mFilters[nth].Remainings;
                    if (prevRemainings != null && 0 < prevRemainings.LongLength) {
                        inPcmList.Add(prevRemainings);
                    }
                }

                while (CountTotalSamples(inPcmList) < mFilters[nth].NumOfSamplesNeeded()) {
                    inPcmList.Add(FilterNth(nth-1, ref from));
                }
                double [] inPcm;
                double [] remainings;
                AssembleSample(inPcmList, mFilters[nth].NumOfSamplesNeeded(), out inPcm, out remainings);
                double [] outPcm = mFilters[nth].FilterDo(inPcm);

                // n-1番目のフィルター後のデータの余ったデータremainingsをn番目のフィルターにセットする
                mFilters[nth].Remainings = remainings;

                return outPcm;
            }
        }

        private void FilterStart() {

        }

        private int ProcessAudioFile(int ch, ref AudioDataPerChannel from, ref AudioDataPerChannel to) {
            foreach (var f in mFilters) {
                f.FilterStart();
            }

            to.ResetStatistics();
            long pos = 0;
            while (pos < to.totalSamples) {
                var pcm = FilterNth(mFilters.Count - 1, ref from);

                to.SetPcmInDouble(pcm, pos);

                pos += pcm.LongLength;
            }

            foreach (var f in mFilters) {
                f.FilterEnd();
            }
            return 0;
        }

        const int FILE_READ_COMPLETE_PERCENTAGE = 10;
        const int FILE_PROCESS_COMPLETE_PERCENTAGE = 90;

        void Background_DoWork(object sender, DoWorkEventArgs e) {
            var args = e.Argument as RunWorkerArgs;
            int rv;
            AudioData audioDataFrom;
            AudioData audioDataTo;

            rv = ReadFlacFile(args.FromPath, out audioDataFrom);
            if (rv < 0) {
                e.Result = rv;
                return;
            }

            mBackgroundWorker.ReportProgress(FILE_READ_COMPLETE_PERCENTAGE, new ProgressArgs("Read completed. now processing...\r\n", 0));

            SetupResultPcm(audioDataFrom, out audioDataTo);

            for (int ch=0; ch < audioDataFrom.meta.channels; ++ch) {
                var from = audioDataFrom.pcm[ch];
                var to = audioDataTo.pcm[ch];
                rv = ProcessAudioFile(ch, ref from, ref to);
                if (rv < 0) {
                    e.Result = rv;
                    return;
                }
                audioDataTo.pcm[ch] = to;

                int percent = (int)((double)FILE_READ_COMPLETE_PERCENTAGE
                        + (FILE_PROCESS_COMPLETE_PERCENTAGE - FILE_READ_COMPLETE_PERCENTAGE)
                        * (ch + 1) / (audioDataFrom.meta.channels));
                string s = string.Empty;
                if (audioDataTo.pcm[ch].overflow) {
                    s = string.Format("Too large magnitude sample detected! channel={0}, magnitude={1}\r\n",
                            ch, audioDataTo.pcm[ch].maxMagnitude);
                }
                mBackgroundWorker.ReportProgress(percent, new ProgressArgs(s, 0));
            }

            mBackgroundWorker.ReportProgress(FILE_PROCESS_COMPLETE_PERCENTAGE, new ProgressArgs("Process completed. now writing...\r\n", 0));

            rv = WriteFlacFile(ref audioDataTo, args.ToPath);
            if (rv < 0) {
                e.Result = rv;
                return;
            }

            mBackgroundWorker.ReportProgress(100, new ProgressArgs("", 0));

            e.Result = rv;
        }

        void Background_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            var args = e.UserState as ProgressArgs;

            progressBar1.Value = e.ProgressPercentage;
            if (0 < args.Message.Length) {
                textBoxLog.Text += args.Message;
                textBoxLog.ScrollToEnd();
            }
        }

        void Background_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            int rv = (int)e.Result;

            progressBar1.IsEnabled = false;
            progressBar1.Value = 0;

            if (rv < 0) {
                var s = string.Format("Error {0}\r\n", rv);
                MessageBox.Show(s);
            }
            textBoxLog.Text += string.Format("Completed.\r\n");
            textBoxLog.ScrollToEnd();
        }


    }
}
