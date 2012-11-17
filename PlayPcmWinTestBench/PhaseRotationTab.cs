using System;
using System.Windows;
using System.ComponentModel;
using PcmDataLib;
using WWDirectComputeCS;
using System.Diagnostics;

namespace PlayPcmWinTestBench {
    public partial class MainWindow : Window {
        private BackgroundWorker mPRWorker;

        class PhaseRotationWorkerArgs : FirWorkerArgs {
            public WWHilbert.HilbertFilterType hilbertFilterType;
            public double phaseRadian;
        }

        private void InitPhaseRotationTab() {
            mPRWorker = new BackgroundWorker();
            mPRWorker.WorkerReportsProgress = true;
            mPRWorker.DoWork += new DoWorkEventHandler(mPhaseRotationWorker_DoWork);
            mPRWorker.ProgressChanged += new ProgressChangedEventHandler(mPhaseRotationWorker_ProgressChanged);
            mPRWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mPhaseRotationWorker_RunWorkerCompleted);
            mPRWorker.WorkerSupportsCancellation = true;
        }

        private void buttonPhaseInputBrowse_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile(m_filterWav);
            if (0 < fileName.Length) {
                textBoxPhaseInputPath.Text = fileName;
            }
        }

        private void buttonPhaseOutputBrowse_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseSaveFile(m_filterSaveWav);
            if (0 < fileName.Length) {
                textBoxPhaseOutputPath.Text = fileName;
            }
        }

        private void buttonPhaseOutputStart_Click(object sender, RoutedEventArgs e) {
            var args = new PhaseRotationWorkerArgs();
            if (!Int32.TryParse(textBoxPhaseFilterN.Text, out args.firLength) ||
                (args.firLength & 1) == 0 || args.firLength <= 0) {
                MessageBox.Show("ヒルベルト変換フィルタ長は正の奇数の数値を半角数字で入力してください。処理中断。");
                return;
            }

            args.inputPath = textBoxPhaseInputPath.Text;
            args.outputPath = textBoxPhaseOutputPath.Text;

            if (!Double.TryParse(textBoxPhaseKaiserAlpha.Text, out args.kaiserAlpha) ||
                args.kaiserAlpha <= 4.0 || 9.0 <= args.kaiserAlpha) {
                MessageBox.Show("カイザー窓α値は4.0<α<9.0の範囲の数値を半角数字で入力してください。処理中断。");
                return;
            }

            if (!Double.TryParse(textBoxPhaseRotationDegree.Text, out args.phaseRadian) ||
                args.phaseRadian < -360.0 || 360.0 < args.phaseRadian) {
                MessageBox.Show("位相回転量は-360.0～360.0の範囲の数値を半角数字で入力してください。処理中断。");
                return;
            }
            args.phaseRadian *= System.Math.PI / 180.0;

            if (radioButtonPhaseSint16.IsChecked == true) {
                args.outputBitsPerSample = 16;
                args.valueRepresentationType = PcmData.ValueRepresentationType.SInt;
            }
            if (radioButtonPhaseSint24.IsChecked == true) {
                args.outputBitsPerSample = 24;
                args.valueRepresentationType = PcmData.ValueRepresentationType.SInt;
            }
            if (radioButtonPhaseFloat32.IsChecked == true) {
                args.outputBitsPerSample = 32;
                args.valueRepresentationType = PcmData.ValueRepresentationType.SFloat;
            }

            if (radioButtonPhaseBlackman.IsChecked == true) {
                args.windowFunc = WindowFuncType.Blackman;
            } else {
                args.windowFunc = WindowFuncType.Kaiser;
            }

            args.hilbertFilterType = WWHilbert.HilbertFilterType.HighPass;
            if (radioButtonPhaseBandlimited.IsChecked == true) {
                args.hilbertFilterType = WWHilbert.HilbertFilterType.Bandlimited;
            }

            buttonPhaseOutputStart.IsEnabled = false;
            mPRWorker.RunWorkerAsync(args);
        }

        private void mPhaseRotationWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            progressBarPhase.Value = 0;

            buttonPhaseOutputStart.IsEnabled = true;

            string s = e.Result as string;
            if (s != null && 0 < s.Length) {
                MessageBox.Show(s);
            }
        }

        private void mPhaseRotationWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBarPhase.Value = e.ProgressPercentage;
        }

        private void mPhaseRotationWorker_DoWork(object sender, DoWorkEventArgs e) {
            PhaseRotationWorkerArgs args = e.Argument as PhaseRotationWorkerArgs;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            string result;
            if (!FirDoLoadConvertSave(args, PhaseRotationDo, out result)) {
                e.Result = result;
                return;
            }

            sw.Stop();
            e.Result = string.Format("{0}\r\n所要時間 {1}秒",
                result, sw.ElapsedMilliseconds / 1000);
        }

        // この関数は音量制限を行わない。呼び出し側で必要に応じて音量を制限する。
        private bool PhaseRotationDo(FirWorkerArgs argsFWA, PcmData pcmDataIn, out PcmData pcmDataOutput) {
            PhaseRotationWorkerArgs args = argsFWA as PhaseRotationWorkerArgs;

            PcmData pcmDataReal = pcmDataIn.BitsPerSampleConvertTo(64, PcmData.ValueRepresentationType.SFloat);

            pcmDataOutput = new PcmData();
            pcmDataOutput.CopyFrom(pcmDataIn);

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
                    mPRWorker.ReportProgress(percentage);
                }
                fir.Unsetup();

            }

            // 音の位相を回転。
            for (int ch=0; ch < pcmDataReal.NumChannels; ++ch) {
                for (long pos=0; pos < pcmDataReal.NumFrames; ++pos) {

                    // 解析信号の各サンプル値を極座標表現に変換。オリジナルの長さと位相を得る。
                    // 長さをそのままに位相を回転し、回転後の実数成分を出力する。

                    double x = pcmDataReal.GetSampleValueInDouble(ch, pos);
                    double y = pcmDataImaginary.GetSampleValueInDouble(ch, pos);

                    double norm = Math.Sqrt(x * x + y * y);
                    double theta = Math.Atan2(y, x);

                    double re = norm * Math.Cos(theta + args.phaseRadian);
                    pcmDataOutput.SetSampleValueInDouble(ch, pos, re);
                }
            }
            return true;
        }

    }
}
