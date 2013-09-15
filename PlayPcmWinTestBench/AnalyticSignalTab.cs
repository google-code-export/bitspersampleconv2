using System;
using System.Windows;
using System.ComponentModel;
using PcmDataLib;
using WWDirectComputeCS;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Threading;

namespace PlayPcmWinTestBench {
    public partial class MainWindow : Window {
        private BackgroundWorker m_ASWorker;
        private DispatcherTimer m_ASdispatcherTimer;

        enum ASViewModeType {
            Gauss,
            AmplitudeReal,
        };
        ASViewModeType m_ASviewMode = ASViewModeType.Gauss;

        private int m_ASdrawEllipseCount = 100;

        private List<double[]> m_analyticSignalList = new List<double[]>();

        private long m_ASviewPos = 0;

        private List<Ellipse> m_ASellipseList = new List<Ellipse>();

        private bool m_ASInitComplete = false;

        private void InitAnalyticSignalTab() {
            m_ASWorker = new BackgroundWorker();
            m_ASWorker.WorkerReportsProgress = true;
            m_ASWorker.DoWork += new DoWorkEventHandler(m_AnalyticSignalWorker_DoWork);
            m_ASWorker.ProgressChanged += new ProgressChangedEventHandler(m_AnalyticSignalWorker_ProgressChanged);
            m_ASWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(m_AnalyticSignalWorker_RunWorkerCompleted);
            m_ASWorker.WorkerSupportsCancellation = true;

            m_ASdispatcherTimer = new DispatcherTimer();
            m_ASdispatcherTimer.Tick += new EventHandler(m_dispatcherTimer_Tick);
            m_ASdispatcherTimer.Interval = new TimeSpan(0, 0, 1, 1000/60);

            m_ASviewPos = 0;

            m_ASInitComplete = true;

            ASViewModeUpdate();
            ASViewUpdate();

        }

        private void radioButtonASViewGauss_Checked(object sender, RoutedEventArgs e) {
            m_ASviewMode = ASViewModeType.Gauss;
            ASViewModeUpdate();
            ASViewUpdate();
        }

        private void radioButtonASViewTime_Checked(object sender, RoutedEventArgs e) {
            m_ASviewMode = ASViewModeType.AmplitudeReal;
            ASViewModeUpdate();
            ASViewUpdate();
        }

        private void ASViewModeUpdate() {
            if (!m_ASInitComplete) {
                return;
            }

            Visibility v;

            v = (m_ASviewMode == ASViewModeType.Gauss) ? Visibility.Visible : System.Windows.Visibility.Hidden;
            textBlockASM1.Visibility = v;
            textBlockASM1i.Visibility = v;
            textBlockASOrigin.Visibility = v;
            textBlockASP1.Visibility = v;
            textBlockASP1i.Visibility = v;
            lineASH.Visibility = v;
            lineASV.Visibility = v;
            ellipseASGauss.Visibility = v;

            v = (m_ASviewMode == ASViewModeType.AmplitudeReal) ? Visibility.Visible : System.Windows.Visibility.Hidden;
            textBlockASTimeM1.Visibility = v;
            textBlockASTimeOrigin.Visibility = v;
            textBlockASTimeP1.Visibility = v;
            textBlockASTimeX.Visibility = v;
            textBlockASTimeY.Visibility = v;
            lineASTimeH.Visibility = v;
            lineASTimeV.Visibility = v;
        }

        private void buttonASInputBrowse_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile(m_filterWav);
            if (0 < fileName.Length) {
                textBoxASInputPath.Text = fileName;
            }
        }

        class AnalyticSignalWorkerArgs : FirWorkerArgs {
            public WWHilbert.HilbertFilterType hilbertFilterType;
        };

        private void buttonASCreate_Click(object sender, RoutedEventArgs e) {
            var args = new AnalyticSignalWorkerArgs();
            if (!Int32.TryParse(textBoxASFilterLength.Text, out args.firLength) ||
                (args.firLength & 1) == 0 || args.firLength <= 0) {
                MessageBox.Show("FIRフィルタ長は正の奇数の数値を半角数字で入力してください。処理中断。");
                return;
            }

            args.inputPath = textBoxASInputPath.Text;

            if (!Double.TryParse(textBoxASKaiserAlpha.Text, out args.kaiserAlpha) ||
                args.kaiserAlpha <= 4.0 || 9.0 <= args.kaiserAlpha) {
                MessageBox.Show("カイザー窓α値は4.0<α<9.0の範囲の数値を半角数字で入力してください。処理中断。");
                return;
            }

            if (radioButtonASBlackman.IsChecked == true) {
                args.windowFunc = WindowFuncType.Blackman;
            } else {
                args.windowFunc = WindowFuncType.Kaiser;
            }

            args.hilbertFilterType = WWHilbert.HilbertFilterType.HighPass;
            if (radioButtonASBandlimited.IsChecked == true) {
                args.hilbertFilterType = WWHilbert.HilbertFilterType.Bandlimited;
            }

            m_analyticSignalList.Clear();
            ASViewUpdate();

            buttonASCreate.IsEnabled = false;
            m_ASWorker.RunWorkerAsync(args);
        }

        private void m_AnalyticSignalWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            progressBarAS.Value = 0;

            buttonASCreate.IsEnabled = true;

            string s = (string)e.Result;
            if (0 < s.Length) {
                MessageBox.Show(s);
            }

            ASViewUpdate();
        }

        private void m_AnalyticSignalWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBarAS.Value = e.ProgressPercentage;
        }

        private void m_AnalyticSignalWorker_DoWork(object sender, DoWorkEventArgs e) {
            AnalyticSignalWorkerArgs args = e.Argument as AnalyticSignalWorkerArgs;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            // pcmファイルを読み込んでサンプル配列pcm1chを作成。
            PcmData pcmDataIn = null;
            try {
                pcmDataIn = ReadWavFile(args.inputPath);
            } catch (IOException ex) {
                e.Result = string.Format("WAVファイル {0} 読み込み失敗\r\n{1}", args.inputPath, ex);
            }
            if (null == pcmDataIn) {
                e.Result = string.Format("WAVファイル {0} 読み込み失敗", args.inputPath);
            }

            var formatConv = new WasapiPcmUtil.PcmFormatConverter(pcmDataIn.NumChannels);
            PcmData pcmDataReal = formatConv.Convert(pcmDataIn, Wasapi.WasapiCS.SampleFormatType.Sdouble, null);

            PcmData pcmDataImaginary = new PcmData();
            pcmDataImaginary.CopyFrom(pcmDataReal);

            var dft = new WWDirectComputeCS.WWDftCpu();

            var hilb = WWHilbert.HilbertFirCoeff(args.hilbertFilterType, args.firLength);
            System.Diagnostics.Debug.Assert(hilb.Length == args.firLength);

            // 窓関数
            double [] window;
            if (args.windowFunc == WindowFuncType.Blackman) {
                WWWindowFunc.BlackmanWindow(args.firLength, out window);
            } else {
                WWWindowFunc.KaiserWindow(args.firLength, args.kaiserAlpha, out window);
            }

            // FIR coeffの個数は、window.Length個。
            // ヒルベルト変換パラメータは未来から過去の方向に並んでいるので左右をひっくり返す。
            double [] coeff = new double[args.firLength];
            for (int i=0; i < coeff.Length; ++i) {
                int pos = coeff.Length - i - 1;
                coeff[i] = hilb[pos] * window[i];
            }

            for (int ch=0; ch < pcmDataImaginary.NumChannels; ++ch) {
                var pcm1ch = new double[pcmDataImaginary.NumFrames];
                for (long i=0; i < pcm1ch.Length; ++i) {
                    pcm1ch[i] = pcmDataImaginary.GetSampleValueInDouble(ch, i);
                }

                // 少しずつFIRする。
                var fir = new WWFirCpu();
                fir.Setup(coeff, pcm1ch);

                const int FIR_SAMPLE = 65536;
                for (int offs=0; offs < pcm1ch.Length; offs += FIR_SAMPLE) {
                    int nSample = FIR_SAMPLE;
                    if (pcm1ch.Length < offs + nSample) {
                        nSample = pcm1ch.Length - offs;
                    }

                    var pcmFir = new double[nSample];
                    fir.Do(offs - window.Length / 2, nSample, pcmFir);

                    // 結果を出力に書き込む。
                    for (long i=0; i < pcmFir.Length; ++i) {
                        var re = pcmFir[i];
                        pcmDataImaginary.SetSampleValueInDouble(ch, i + offs,
                            (float)(re));
                    }

                    // 進捗Update。
                    int percentage = (int)(
                        (100L * ch / pcmDataImaginary.NumChannels) +
                        (100L * (offs + 1) / pcm1ch.Length / pcmDataImaginary.NumChannels));
                    m_ASWorker.ReportProgress(percentage);
                }
                fir.Unsetup();
            }

            // 解析信号を出力。
            m_analyticSignalList.Clear();
            for (int ch=0; ch < pcmDataReal.NumChannels; ++ch) {
                double [] signal = new double[pcmDataImaginary.NumFrames * 2];
                for (long pos=0; pos < pcmDataReal.NumFrames; ++pos) {
                    signal[pos * 2 + 0] = pcmDataReal.GetSampleValueInDouble(ch, pos);
                    signal[pos * 2 + 1] = pcmDataImaginary.GetSampleValueInDouble(ch, pos);
                }
                m_analyticSignalList.Add(signal);
            }

            sw.Stop();
            e.Result = "";
        }

        private void ASViewUpdate() {
            if (!m_ASInitComplete) {
                return;
            }

            if (m_analyticSignalList.Count == 0) {
                // 空である
                buttonASScrollStart.IsEnabled = false;
                buttonASScrollStop.IsEnabled = false;
                buttonASAdvanceStep.IsEnabled = false;
                sliderASScrollPos.IsEnabled = false;
                labelASTime.Content = "0/0";

                foreach (Ellipse e in m_ASellipseList) {
                    canvasAS.Children.Remove(e);
                }
                m_ASellipseList.Clear();

                return;
            }

            buttonASScrollStart.IsEnabled = !m_ASdispatcherTimer.IsEnabled;
            buttonASScrollStop.IsEnabled = m_ASdispatcherTimer.IsEnabled;
            buttonASAdvanceStep.IsEnabled = true;
            sliderASScrollPos.IsEnabled = true;
            labelASTime.Content = string.Format("{0}/{1}", m_ASviewPos, m_analyticSignalList[0].Length/2);
            sliderASScrollPos.Maximum = m_analyticSignalList[0].Length / 2 - m_ASdrawEllipseCount;

            int ch = 0;
            if (listBoxItemAS2ch.IsSelected && 2 <= m_analyticSignalList.Count) {
                ch = 1;
            }

            foreach (Ellipse e in m_ASellipseList) {
                canvasAS.Children.Remove(e);
            }
            m_ASellipseList.Clear();

            switch (m_ASviewMode) {
            case ASViewModeType.Gauss:
                for (int i=0; i < m_ASdrawEllipseCount; ++i) {
                    long pos=m_ASviewPos + i;
                    if (m_analyticSignalList[ch].Length / 2 <= pos) {
                        break;
                    }

                    double x = m_analyticSignalList[ch][pos * 2];
                    double y = m_analyticSignalList[ch][pos * 2 + 1];

                    double scale = ((canvasAS.Width < canvasAS.Height) ? canvasAS.Width : canvasAS.Height) / 2;
                    double offsX = canvasAS.Width / 2;
                    double offsY = canvasAS.Height / 2;

                    var e = new Ellipse();

                    byte gradation = (byte)( 255.0f * i / (m_ASdrawEllipseCount-1));
                    SolidColorBrush b = new SolidColorBrush();
                    b.Color = Color.FromRgb((byte)(255-gradation), 0, gradation);
                    e.Fill = b;

                    e.StrokeThickness = 0;
                    e.Width = 4;
                    e.Height = 4;
                    e.Margin = new Thickness(x * scale + offsX - e.Width / 2, -y * scale + offsY - e.Height / 2, 0, 0);
                    m_ASellipseList.Add(e);
                    canvasAS.Children.Add(e);
                }
                break;
            case ASViewModeType.AmplitudeReal:
                textBlockASTimeOrigin.Text = string.Format("{0}", m_ASviewPos);

                for (int i=0; i < m_ASdrawEllipseCount; ++i) {
                    long pos=m_ASviewPos + i;
                    if (m_analyticSignalList[ch].Length / 2 <= pos) {
                        break;
                    }

                    double x = m_analyticSignalList[ch][pos * 2];
                    // double y = m_analyticSignalList[ch][pos * 2 + 1];

                    double xscale = (512.0 - 128.0)/m_ASdrawEllipseCount;
                    double yscale = canvasAS.Height / 2;
                    double offsX = 128;
                    double offsY = canvasAS.Height / 2;

                    var e = new Ellipse();

                    byte gradation = (byte)(255.0f * i / (m_ASdrawEllipseCount - 1));
                    SolidColorBrush b = new SolidColorBrush();
                    b.Color = Color.FromRgb((byte)(255 - gradation), 0, gradation);
                    e.Fill = b;

                    e.StrokeThickness = 0;
                    e.Width = 4;
                    e.Height = 4;
                    e.Margin = new Thickness(i * xscale + offsX - e.Width / 2, -x * yscale + offsY - e.Height / 2, 0, 0);
                    m_ASellipseList.Add(e);
                    canvasAS.Children.Add(e);
                }
                break;
            }
        }

        private void sliderASScrollPos_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            m_ASviewPos = (long)e.NewValue;
            ASViewUpdate();
        }

        private void buttonASAdvanceStep_Click(object sender, RoutedEventArgs e) {
            if (m_analyticSignalList.Count == 0) {
                return;
            }

            m_ASviewPos += 10;
            if (sliderASScrollPos.Maximum <= m_ASviewPos) {
                m_ASviewPos = (long)sliderASScrollPos.Maximum;
            }

            ASViewUpdate();
        }

        private void buttonASScrollStart_Click(object sender, RoutedEventArgs e) {
            if (m_ASdispatcherTimer.IsEnabled) {
                return;
            }

            int tickPerSecond;
            if (!Int32.TryParse(textBoxASScrollSpeed.Text, out tickPerSecond) || tickPerSecond <= 0 || 10000 < tickPerSecond) {
                MessageBox.Show("時間軸進行速度は1以上10000以下の数値を指定してください");
                return;
            }

            m_ASdispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, (int)(1000.0 / tickPerSecond));
            m_ASdispatcherTimer.Start();
            buttonASScrollStart.IsEnabled = false;
            buttonASScrollStop.IsEnabled = true;
        }

        private void buttonASScrollStop_Click(object sender, RoutedEventArgs e) {
            if (m_ASdispatcherTimer.IsEnabled) {
                m_ASdispatcherTimer.Stop();
                buttonASScrollStart.IsEnabled = true;
                buttonASScrollStop.IsEnabled = false;
            }
        }

        void m_dispatcherTimer_Tick(object sender, EventArgs e) {
            if (sliderASScrollPos.Maximum <= m_ASviewPos + 1) {
                // 止まった
                buttonASScrollStart.Content = "時間を進める(_S)";
                m_ASdispatcherTimer.Stop();
                return;
            }

            sliderASScrollPos.Value = m_ASviewPos + 1;
        }

        private void textBoxASDispSamples_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
            if (!Int32.TryParse(textBoxASDispSamples.Text, out m_ASdrawEllipseCount) || m_ASdrawEllipseCount <= 0 || 10000 < m_ASdrawEllipseCount) {
                MessageBox.Show("表示サンプル数は1以上10000以下の数値を指定してください");
                m_ASdrawEllipseCount = 100;
                return;
            }
            ASViewUpdate();
        }

        private void listBoxASChannel_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            ASViewUpdate();
        }



    }
}
