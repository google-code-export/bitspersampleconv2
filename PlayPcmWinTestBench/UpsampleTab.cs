using System;
using System.Windows;
using PcmDataLib;
using WWDirectComputeCS;
using System.ComponentModel;
using System.Security.Cryptography;
using System.IO;
using Wasapi;

namespace PlayPcmWinTestBench {
    public partial class MainWindow : Window {
        /// /////////////////////////////////////////////////////////////////
        /// アップサンプル
        RNGCryptoServiceProvider gen = new RNGCryptoServiceProvider();
        
        private BackgroundWorker m_USAQworker;

        void InitUpsampleTab() {
            m_USAQworker = new BackgroundWorker();
            m_USAQworker.WorkerReportsProgress = true;
            m_USAQworker.DoWork += new DoWorkEventHandler(m_USAQworker_DoWork);
            m_USAQworker.ProgressChanged += new ProgressChangedEventHandler(m_USAQworker_ProgressChanged);
            m_USAQworker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(m_USAQworker_RunWorkerCompleted);
            m_USAQworker.WorkerSupportsCancellation = true;

            string s = "※GPU計算機能を使用するためには以下の3つの準備が要ります:\r\n"
                + "・GPUはGeForce GTX 570以上を用意して下さい。\r\n"
                + "・最新のNVIDIAディスプレイドライバ をインストールして下さい(バージョン260以降が必要)。\r\n"
                + "・最新のDirectXエンドユーザーランタイムをインストールする必要があります(August 2009以降が必要)。"
                + " http://www.microsoft.com/downloads/details.aspx?FamilyID=2da43d38-db71-4c1b-bc6a-9b6652cd92a3&displayLang=ja\r\n";

            textBoxUSResult.Text = s;
            textBoxAQResult.Text = s;

        }

        enum ProcessDevice {
            Cpu,
            Gpu
        };

        struct USWorkerArgs {
            public string inputPath;
            public string outputPath;
            public int resampleFrequency;
            public int convolutionN;
            public ProcessDevice device;
            public int sampleSlice;

            public bool addJitter;
            public double sequentialJitterFrequency;
            public double sequentialJitterPicoseconds;
            public double tpdfJitterPicoseconds;
            public double rpdfJitterPicoseconds;

            public int outputBitsPerSample;
            public PcmDataLib.PcmData.ValueRepresentationType outputVRT;

            // --------------------------------------------------------
            // 以降、物置(DoWork()の中で使用する)
            public double thetaCoefficientSeqJitter;
            public double ampSeqJitter;
            public double ampTpdfJitter;
            public double ampRpdfJitter;

            public int[] resamplePosArray;
            public double[] fractionArray;
        };

        private void buttonUSBrowseOpen_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile(m_filterWav);
            if (0 < fileName.Length) {
                textBoxUSInputFilePath.Text = fileName;
            }
        }

        private void buttonUSBrowseSaveAs_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseSaveFile(m_filterSaveWav);
            if (0 < fileName.Length) {
                textBoxUSOutputFilePath.Text = fileName;
            }
        }

        private void buttonUSOutputStart_Click(object sender, RoutedEventArgs e) {
            USWorkerArgs args = new USWorkerArgs();
            args.inputPath = textBoxUSInputFilePath.Text;
            args.outputPath = textBoxUSOutputFilePath.Text;
            if (!System.IO.File.Exists(args.inputPath)) {
                MessageBox.Show("エラー。入力ファイルが存在しません");
                return;
            }
            if (!Int32.TryParse(textBoxUSFrequency.Text, out args.resampleFrequency) ||
                    args.resampleFrequency < 0.0) {
                MessageBox.Show("エラー。リサンプル周波数に0以上の数値を入力してください");
                return;
            }

            args.convolutionN = 256;
            args.device = ProcessDevice.Cpu;
            args.sampleSlice = 1;
            if (radioButtonUSCpu16.IsChecked == true) {
                args.convolutionN = 65536;
            }
            if (radioButtonUSGpu16.IsChecked == true) {
                args.convolutionN = 65536;
                args.device = ProcessDevice.Gpu;
                args.sampleSlice = 256;
            }
            if (radioButtonUSGpu20.IsChecked == true) {
                args.convolutionN = 1048576;
                args.device = ProcessDevice.Gpu;

                // 重いので減らす。
                args.sampleSlice = 16;
            }
            if (radioButtonUSGpu24.IsChecked == true) {
                args.convolutionN = 16777216;
                args.device = ProcessDevice.Gpu;

                // この条件では一度に32768個処理できない。
                // 16384以下の値をセットする。
                args.sampleSlice = 1;
            }

            args.addJitter = false;

            // 出力フォーマット
            args.outputBitsPerSample = 16;
            args.outputVRT = PcmData.ValueRepresentationType.SInt;
            if (radioButtonOutputSint24.IsChecked == true) {
                args.outputBitsPerSample = 24;
            }
            if (radioButtonOutputFloat32.IsChecked == true) {
                args.outputBitsPerSample = 32;
                args.outputVRT = PcmData.ValueRepresentationType.SFloat;
            }

            buttonUSOutputStart.IsEnabled = false;
            buttonUSBrowseOpen.IsEnabled = false;
            buttonUSBrowseSaveAs.IsEnabled = false;
            buttonUSAbort.IsEnabled = true;
            progressBarUS.Value = 0;

            textBoxUSResult.Text += string.Format("処理中 {0}⇒{1}……処理中はPCの動作が重くなります!\r\n",
                args.inputPath, args.outputPath);
            textBoxUSResult.ScrollToEnd();

            m_USAQworker.RunWorkerAsync(args);
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        /// 音質劣化

        private void buttonAQBrowseOpen_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile(m_filterWav);
            if (0 < fileName.Length) {
                textBoxAQInputFilePath.Text = fileName;
            }
        }

        private void buttonAQBrowseSaveAs_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseSaveFile(m_filterSaveWav);
            if (0 < fileName.Length) {
                textBoxAQOutputFilePath.Text = fileName;
            }
        }

        private void buttonAQOutputStart_Click(object sender, RoutedEventArgs e) {
            USWorkerArgs args = new USWorkerArgs();
            args.inputPath = textBoxAQInputFilePath.Text;
            args.outputPath = textBoxAQOutputFilePath.Text;
            if (!System.IO.File.Exists(args.inputPath)) {
                MessageBox.Show("エラー。入力ファイルが存在しません");
                return;
            }
            if (!Double.TryParse(textBoxSequentialJitterFrequency.Text, out args.sequentialJitterFrequency) ||
                    args.sequentialJitterFrequency < 0.0) {
                MessageBox.Show("エラー。周期ジッター周波数に0以上の数値を入力してください");
                return;
            }
            if (!Double.TryParse(textBoxSequentialJitterPicoseconds.Text, out args.sequentialJitterPicoseconds) ||
                    args.sequentialJitterPicoseconds < 0.0) {
                MessageBox.Show("エラー。周期ジッター最大ずれ量に0以上の数値を入力してください");
                return;
            }
            // sequential jitter RMS⇒peak 正弦波なので√2倍する
            args.sequentialJitterPicoseconds *= Math.Sqrt(2.0);

            if (!Double.TryParse(textBoxTpdfJitterPicoseconds.Text, out args.tpdfJitterPicoseconds) ||
                    args.tpdfJitterPicoseconds < 0.0) {
                MessageBox.Show("エラー。三角分布ジッター最大ずれ量に0以上の数値を入力してください");
                return;
            }
            if (!Double.TryParse(textBoxRpdfJitterPicoseconds.Text, out args.rpdfJitterPicoseconds) ||
                    args.rpdfJitterPicoseconds < 0.0) {
                MessageBox.Show("エラー。一様分布ジッター最大ずれ量に0以上の数値を入力してください");
                return;
            }

            args.convolutionN = 256;
            args.device = ProcessDevice.Cpu;
            args.sampleSlice = 32768;
            if (radioButtonAQCpu16.IsChecked == true) {
                args.convolutionN = 65536;
            }
            if (radioButtonAQGpu16.IsChecked == true) {
                args.convolutionN = 65536;
                args.device = ProcessDevice.Gpu;
            }
            if (radioButtonAQGpu20.IsChecked == true) {
                args.convolutionN = 1048576;
                args.device = ProcessDevice.Gpu;

                // 重いので減らす。
                args.sampleSlice = 256;
            }
            if (radioButtonAQGpu24.IsChecked == true) {
                args.convolutionN = 16777216;
                args.device = ProcessDevice.Gpu;

                // この条件では一度に32768個処理できない。
                // 16384以下の値をセットする。
                args.sampleSlice = 16;
            }

            args.addJitter = true;

            // 出力フォーマット
            args.outputBitsPerSample = 32;
            args.outputVRT = PcmData.ValueRepresentationType.SFloat;

            buttonAQOutputStart.IsEnabled = false;
            buttonAQBrowseOpen.IsEnabled = false;
            buttonAQBrowseSaveAs.IsEnabled = false;
            buttonAQAbort.IsEnabled = true;
            progressBarAQ.Value = 0;

            textBoxAQResult.Text += string.Format("処理中 {0}⇒{1}……処理中はPCの動作が重くなります!\r\n",
                args.inputPath, args.outputPath);
            textBoxAQResult.ScrollToEnd();

            m_USAQworker.RunWorkerAsync(args);
        }

        private void buttonAQAbort_Click(object sender, RoutedEventArgs e) {
            m_USAQworker.CancelAsync();
            buttonAQAbort.IsEnabled = false;
        }

        ////////////////////////////////////////////////////////////////////
        // US AQ共用ワーカースレッド

        private int CpuUpsample(USWorkerArgs args, PcmData pcmDataIn, PcmData pcmDataOut) {
            int hr = 0;

            int sampleTotalTo = (int)pcmDataOut.NumFrames;

            float[] sampleData = new float[pcmDataIn.NumFrames];
            for (int ch = 0; ch < pcmDataIn.NumChannels; ++ch) {
                for (int i = 0; i < pcmDataIn.NumFrames; ++i) {
                    sampleData[i] = pcmDataIn.GetSampleValueInFloat(ch, i);
                }

                WWUpsampleCpu us = new WWUpsampleCpu();
                if (args.addJitter) {
                    hr = us.Setup(args.convolutionN, sampleData, sampleData.Length,
                        pcmDataIn.SampleRate, pcmDataOut.SampleRate, sampleTotalTo,
                        args.resamplePosArray, args.fractionArray);
                } else {
                    hr = us.Setup(args.convolutionN, sampleData, sampleData.Length,
                        pcmDataIn.SampleRate, pcmDataOut.SampleRate, sampleTotalTo);
                }
                if (hr < 0) {
                    break;
                }

                for (int offs = 0; offs < sampleTotalTo; offs += args.sampleSlice) {
                    int sample1 = args.sampleSlice;
                    if (sampleTotalTo - offs < sample1) {
                        sample1 = sampleTotalTo - offs;
                    }
                    if (sample1 < 1) {
                        break;
                    }

                    float[] outFragment = new float[sample1];
                    hr = us.Do(offs, sample1, outFragment);
                    if (hr < 0) {
                        // ここからbreakしても外のfor文までしか行かない
                        us.Unsetup();
                        sampleData = null;
                        break;
                    }
                    if (m_USAQworker.CancellationPending) {
                        // ここからbreakしても外のfor文までしか行かない
                        us.Unsetup();
                        sampleData = null;
                        hr = -1;
                        break;
                    }
                    if (0 <= hr) {
                        // 成功。出てきたデータをpcmDataOutに詰める。
                        for (int j = 0; j < sample1; ++j) {
                            pcmDataOut.SetSampleValueInFloat(ch, offs + j, outFragment[j]);
                        }
                    }
                    outFragment = null;

                    // 10%～99%
                    m_USAQworker.ReportProgress(
                        10 + (int)(89L * offs / sampleTotalTo + 89L * ch) / pcmDataIn.NumChannels);
                }

                if (m_USAQworker.CancellationPending) {
                    break;
                }
                if (hr < 0) {
                    break;
                }

                us.Unsetup();
            }

            sampleData = null;

            return hr;
        }

        private int GpuUpsample(USWorkerArgs args, PcmData pcmDataIn, PcmData pcmDataOut) {
            int hr = 0;

            int sampleTotalTo = (int)pcmDataOut.NumFrames;

            float[] sampleData = new float[pcmDataIn.NumFrames];
            for (int ch = 0; ch < pcmDataIn.NumChannels; ++ch) {
                for (int i = 0; i < pcmDataIn.NumFrames; ++i) {
                    sampleData[i] = pcmDataIn.GetSampleValueInFloat(ch, i);
                }

                WWUpsampleGpu us = new WWUpsampleGpu();
                if (args.addJitter) {
                    hr = us.Init(args.convolutionN, sampleData, (int)pcmDataIn.NumFrames, pcmDataIn.SampleRate,
                        args.resampleFrequency, sampleTotalTo, args.resamplePosArray, args.fractionArray);
                } else {
                    hr = us.Init(args.convolutionN, sampleData, (int)pcmDataIn.NumFrames, pcmDataIn.SampleRate,
                        args.resampleFrequency, sampleTotalTo);
                }
                if (hr < 0) {
                    us.Term();
                    return hr;
                }

                int sampleRemain = sampleTotalTo;
                int offs = 0;
                while (0 < sampleRemain) {
                    int sample1 = args.sampleSlice;
                    if (sampleRemain < sample1) {
                        sample1 = sampleRemain;
                    }
                    hr = us.ProcessPortion(offs, sample1);
                    if (hr < 0) {
                        break;
                    }
                    if (m_USAQworker.CancellationPending) {
                        us.Term();
                        return -1;
                    }

                    sampleRemain -= sample1;
                    offs += sample1;

                    // 10%～99%
                    m_USAQworker.ReportProgress(
                        10 + (int)(89L * offs / sampleTotalTo + 89L * ch) / pcmDataIn.NumChannels);
                }

                if (0 <= hr) {
                    float[] output = new float[sampleTotalTo];
                    hr = us.GetResultFromGpuMemory(ref output, sampleTotalTo);
                    if (0 <= hr) {
                        // すべて成功。
                        for (int i = 0; i < pcmDataOut.NumFrames; ++i) {
                            pcmDataOut.SetSampleValueInFloat(ch, i, output[i]);
                        }
                    }
                    output = null;
                }
                us.Term();

                if (hr < 0) {
                    break;
                }
            }
            sampleData = null;

            return hr;
        }

        private void m_USAQworker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBarUS.Value = e.ProgressPercentage;
            progressBarAQ.Value = e.ProgressPercentage;
        }

        /// <summary>
        ///  仮数部が32bitぐらいまで値が埋まっているランダムの0～1
        /// </summary>
        /// <returns></returns>
        private static double GenRandom0to1(RNGCryptoServiceProvider gen) {
            byte[] bytes = new byte[4];
            gen.GetBytes(bytes);
            uint u = BitConverter.ToUInt32(bytes, 0);
            double d = (double)u / uint.MaxValue;
            return d;
        }

        /// <summary>
        /// ジッター発生。
        /// </summary>
        private double GenerateJitter(USWorkerArgs args, int offs) {
            double seqJitter = args.ampSeqJitter
                * Math.Sin((args.thetaCoefficientSeqJitter * offs) % (2.0 * Math.PI));
            double tpdfJitter = 0.0;
            double rpdfJitter = 0.0;
            if (0.0 < args.tpdfJitterPicoseconds) {
                double r = GenRandom0to1(gen) + GenRandom0to1(gen) - 1.0;
                tpdfJitter = args.ampTpdfJitter * r;
            }
            if (0.0 < args.rpdfJitterPicoseconds) {
                rpdfJitter = args.ampRpdfJitter * (GenRandom0to1(gen) * 2.0 - 1.0);
            }
            double jitter = seqJitter + tpdfJitter + rpdfJitter;
            return jitter;
        }

        private void PrepareResamplePosArray(
                USWorkerArgs args,
                int sampleRateFrom,
                int sampleRateTo,
                int sampleTotalFrom,
                int sampleTotalTo,
                int[] resamplePosArray,
                double[] fractionArray) {

            // resamplePosArrayとfractionArrayにジッターを付加する

            for (int i = 0; i < sampleTotalTo; ++i) {
                double resamplePos = (double)i * sampleRateFrom / sampleRateTo +
                    GenerateJitter(args, i);

                /* -0.5 <= fraction<+0.5になるようにresamplePosを選ぶ。
                 * 最後のほうで範囲外を指さないようにする。
                 */
                int resamplePosI = (int)(resamplePos + 0.5);

                if (resamplePosI < 0) {
                    resamplePosI = 0;
                }

                if (sampleTotalFrom <= resamplePosI) {
                    resamplePosI = sampleTotalFrom - 1;
                }
                double fraction = resamplePos - resamplePosI;

                resamplePosArray[i] = resamplePosI;
                fractionArray[i] = fraction;
            }
        }

        private void m_USAQworker_DoWork(object sender, DoWorkEventArgs e) {
            // System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Lowest;

            USWorkerArgs args = (USWorkerArgs)e.Argument;

            PcmData pcmDataIn = ReadWavFile(args.inputPath);
            if (null == pcmDataIn) {
                e.Result = string.Format("WAVファイル 読み込み失敗: {0}", args.inputPath);
                return;
            }

            // ファイル読み込み完了。
            if (args.addJitter) {
                // ジッター負荷の場合、サンプリング周波数は変更しない。
                args.resampleFrequency = pcmDataIn.SampleRate;
            }

            if (args.resampleFrequency < pcmDataIn.SampleRate) {
                e.Result = string.Format("エラー: ダウンサンプルは対応していません {0} from={1} to={2}",
                    args.inputPath, pcmDataIn.SampleRate, args.resampleFrequency);
                return;
            }
            if (0x7fff0000L < pcmDataIn.NumFrames * 4 * pcmDataIn.NumChannels * args.resampleFrequency / pcmDataIn.SampleRate) {
                e.Result = string.Format("エラー: リサンプル後のファイルサイズが2GBを超えそうなので中断しました {0}",
                    args.inputPath);
                return;
            }

            m_USAQworker.ReportProgress(1);

            var conv = new WasapiPcmUtil.PcmFormatConverter(pcmDataIn.NumChannels);
            pcmDataIn = conv.Convert(pcmDataIn, WasapiCS.BitAndFormatToSampleFormatType(32, 32, WasapiCS.BitFormatType.SFloat), null);
            PcmData pcmDataOut = new PcmData();
            pcmDataOut.CopyFrom(pcmDataIn);
            int sampleTotalTo = (int)(args.resampleFrequency * pcmDataIn.NumFrames / pcmDataIn.SampleRate);
            {   // PcmDataOutのサンプルレートとサンプル数を更新する。
                byte[] outSampleArray = new byte[(long)sampleTotalTo * pcmDataOut.NumChannels * 4];
                pcmDataOut.SetSampleArray(sampleTotalTo, outSampleArray);
                pcmDataOut.SampleRate = args.resampleFrequency;
                outSampleArray = null;
            }

            // 再サンプルテーブル作成
            args.resamplePosArray = null;
            args.fractionArray = null;
            if (args.addJitter) {
                // ジッター付加の場合、サンプルレートは変更しない。
                args.resamplePosArray = new int[pcmDataIn.NumFrames];
                args.fractionArray = new double[pcmDataIn.NumFrames];
                /*
                 sampleRate        == 96000 Hz
                 jitterFrequency   == 50 Hz
                 jitterPicoseconds == 1 ps の場合

                 サンプル位置posのθ= 2 * PI * pos * 50 / 96000 (ラジアン)

                 サンプル間隔= 1/96000秒 = 10.4 μs
             
                 1ms = 10^-3秒
                 1μs= 10^-6秒
                 1ns = 10^-9秒
                 1ps = 10^-12秒

                  1psのずれ                     x サンプルのずれ
                 ───────────── ＝ ─────────
                  10.4 μs(1/96000)sのずれ      1 サンプルのずれ

                 1psのサンプルずれA ＝ 10^-12 ÷ (1/96000) (サンプルのずれ)
             
                 サンプルを採取する位置= pos + Asin(θ)
             
                 */

                args.thetaCoefficientSeqJitter = 2.0 * Math.PI * args.sequentialJitterFrequency / pcmDataIn.SampleRate;
                args.ampSeqJitter = 1.0e-12 * pcmDataIn.SampleRate * args.sequentialJitterPicoseconds;
                args.ampTpdfJitter = 1.0e-12 * pcmDataIn.SampleRate * args.tpdfJitterPicoseconds;
                args.ampRpdfJitter = 1.0e-12 * pcmDataIn.SampleRate * args.rpdfJitterPicoseconds;

                PrepareResamplePosArray(
                    args, pcmDataIn.SampleRate, pcmDataOut.SampleRate,
                    (int)pcmDataIn.NumFrames, sampleTotalTo,
                    args.resamplePosArray, args.fractionArray);
            }

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            int hr = 0;
            if (args.device == ProcessDevice.Gpu) {
                hr = GpuUpsample(args, pcmDataIn, pcmDataOut);
            } else {
                hr = CpuUpsample(args, pcmDataIn, pcmDataOut);
            }

            // args.resamplePosArrayは中でコピーされるのでここで不要になる。
            args.resamplePosArray = null;
            args.fractionArray = null;

            if (m_USAQworker.CancellationPending) {
                e.Result = string.Format("キャンセル完了。");
                e.Cancel = true;
                return;
            }
            if (hr < 0) {
                e.Result = string.Format("Upsample エラー 0x{0:X8}", hr);
                return;
            }
            sw.Stop();

            // 成功した。レベル制限する。
            float scale = pcmDataOut.LimitLevelOnFloatRange();

            if (args.outputVRT != PcmData.ValueRepresentationType.SFloat) {
                // ビットフォーマット変更。
                var formatConv = new WasapiPcmUtil.PcmFormatConverter(pcmDataOut.NumChannels);
                pcmDataOut = formatConv.Convert(pcmDataOut,
                        WasapiCS.BitAndFormatToSampleFormatType(args.outputBitsPerSample, args.outputBitsPerSample, (WasapiCS.BitFormatType)args.outputVRT), null);
            }

            try {
                WriteWavFile(pcmDataOut, args.outputPath);
            } catch (IOException ex) {
                // 書き込みエラー。
                e.Result = ex.ToString();
                return;
            }

            e.Result = string.Format("書き込み成功。処理時間 {0}秒\r\n",
                sw.ElapsedMilliseconds * 0.001);
            if (scale < 1.0f) {
                e.Result = string.Format("書き込み成功。処理時間 {0}秒。" +
                    "レベルオーバーのため音量調整{1}dB({2}倍)しました。\r\n",
                    sw.ElapsedMilliseconds * 0.001,
                    20.0 * Math.Log10(scale), scale);
            }
            m_USAQworker.ReportProgress(100);
        }

        private void buttonUSAbort_Click(object sender, RoutedEventArgs e) {
            m_USAQworker.CancelAsync();
            buttonUSAbort.IsEnabled = false;
        }

        private void m_USAQworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            progressBarUS.Value = 0;
            buttonUSAbort.IsEnabled = false;
            buttonUSOutputStart.IsEnabled = true;
            buttonUSBrowseOpen.IsEnabled = true;
            buttonUSBrowseSaveAs.IsEnabled = true;

            progressBarAQ.Value = 0;
            buttonAQOutputStart.IsEnabled = true;
            buttonAQBrowseOpen.IsEnabled = true;
            buttonAQBrowseSaveAs.IsEnabled = true;
            buttonAQAbort.IsEnabled = false;

            if (e.Cancelled) {
                textBoxUSResult.Text += string.Format("処理中断。\r\n");
                textBoxAQResult.Text += string.Format("処理中断。\r\n");
            } else {
                string result = (string)e.Result;
                textBoxUSResult.Text += string.Format("結果: {0}\r\n", result);
                textBoxAQResult.Text += string.Format("結果: {0}\r\n", result);
            }
            textBoxUSResult.ScrollToEnd();
            textBoxAQResult.ScrollToEnd();
        }
    }
}
