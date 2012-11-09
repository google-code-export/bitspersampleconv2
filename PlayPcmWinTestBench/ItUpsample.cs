using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.ComponentModel;
using PcmDataLib;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PlayPcmWinTestBench {
    public partial class MainWindow : Window {
        private BackgroundWorker mItUpsampleWorker;

        private void buttonItUpsampleBrowseOpen_Click(object sender, RoutedEventArgs e) {
            var fileName = BrowseOpenFile(m_filterWav);
            if (0 < fileName.Length) {
                textBoxItUpsampleInputPath.Text = fileName;
            }
        }

        private void buttonItUpsampleBrowseSaveAs_Click(object sender, RoutedEventArgs e) {
            var fileName = BrowseSaveFile(m_filterSaveWav);
            if (0 < fileName.Length) {
                textBoxItUpsampleOutputPath.Text = fileName;
            }
        }

        private void InitItUpsample() {
            mItUpsampleWorker = new BackgroundWorker();
            mItUpsampleWorker.WorkerReportsProgress = true;
            mItUpsampleWorker.DoWork += new DoWorkEventHandler(mItUpsampleWorker_DoWork);
            mItUpsampleWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mItUpsampleWorker_RunWorkerCompleted);
        }

        private int mFreqMagnitude;
        enum ItUpsampleType {
            Unknown,

            ImpulseTrain,
            SampleHold,
            LinearInterpolation,
        };

        private ItUpsampleType mItUpsampleType = ItUpsampleType.Unknown;

        private void buttonItUpsampleStart_Click(object sender, RoutedEventArgs e) {
            var args = new FirWorkerArgs();
            args.inputPath = textBoxItUpsampleInputPath.Text;
            args.outputPath = textBoxItUpsampleOutputPath.Text;

            if (true == radioButtonItUpsampleSint16.IsChecked) {
                args.outputBitsPerSample = 16;
                args.valueRepresentationType = PcmData.ValueRepresentationType.SInt;
            }
            if (true == radioButtonItUpsampleSint24.IsChecked) {
                args.outputBitsPerSample = 24;
                args.valueRepresentationType = PcmData.ValueRepresentationType.SInt;
            }
            if (true == radioButtonItUpsampleSfloat32.IsChecked) {
                args.outputBitsPerSample = 32;
                args.valueRepresentationType = PcmData.ValueRepresentationType.SFloat;
            }

            mFreqMagnitude = 0;
            if (!Int32.TryParse(textBoxItUpsampleFreqMagnitude.Text, out mFreqMagnitude)
                    || mFreqMagnitude <= 1) {
                MessageBox.Show("アップサンプル倍率は2以上の整数を入力して下さい");
                return;
            }

            mItUpsampleType = ItUpsampleType.Unknown;
            if (true == radioButtonItUpsampleImpulse.IsChecked) {
                mItUpsampleType = ItUpsampleType.ImpulseTrain;
            }
            if (true == radioButtonItUpsampleSampleHold.IsChecked) {
                mItUpsampleType = ItUpsampleType.SampleHold;
            }
            if (true == radioButtonItUpsampleLinear.IsChecked) {
                mItUpsampleType = ItUpsampleType.LinearInterpolation;
            }
            System.Diagnostics.Debug.Assert(mItUpsampleType != ItUpsampleType.Unknown);

            textBoxItUpsampleLog.Text += string.Format("開始。{0} ==> {1} {2}x\r\n",
                args.inputPath, args.outputPath, mFreqMagnitude);
            textBoxItUpsampleLog.ScrollToEnd();

            buttonItUpsampleDo.IsEnabled = false;
            mItUpsampleWorker.RunWorkerAsync(args);
        }

        void mItUpsampleWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            string s = (string)e.Result;
            textBoxItUpsampleLog.Text += s + "\r\n";
            textBoxItUpsampleLog.ScrollToEnd();

            buttonItUpsampleDo.IsEnabled = true;
        }

        void mItUpsampleWorker_DoWork(object sender, DoWorkEventArgs e) {
            FirWorkerArgs args = (FirWorkerArgs)e.Argument;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            string result;
            if (!FirDoLoadConvertSave(args, ItUpsampleDo, out result)) {
                e.Result = result;
                return;
            }

            sw.Stop();
            e.Result = string.Format("{0}\r\n所要時間 {1}.{2:D3}秒",
                result, sw.ElapsedMilliseconds / 1000, sw.ElapsedMilliseconds % 1000);
        }

        private bool ItUpsampleDo(FirWorkerArgs args, PcmData pcmDataIn, out PcmData pcmDataOut) {
            pcmDataOut = new PcmData();
            pcmDataOut.SetFormat(pcmDataIn.NumChannels, 64, 64,
                pcmDataIn.SampleRate * mFreqMagnitude,
                PcmData.ValueRepresentationType.SFloat, pcmDataIn.NumFrames * mFreqMagnitude);
            pcmDataOut.SetSampleArray(new byte[pcmDataOut.NumFrames * pcmDataOut.BitsPerFrame / 8]);
            
            var pcm = pcmDataOut;

            switch (mItUpsampleType) {
            case ItUpsampleType.ImpulseTrain:
                Parallel.For(0, pcmDataIn.NumFrames, (pos) => {
                    for (int ch=0; ch < pcmDataIn.NumChannels; ++ch) {
                        var v = pcmDataIn.GetSampleValueInDouble(ch, pos);
                        pcm.SetSampleValueInDouble(ch, pos * mFreqMagnitude, v);
                    }
                });
                break;

            case ItUpsampleType.SampleHold:
                Parallel.For(0, pcmDataIn.NumFrames, (pos) => {
                    for (int ch=0; ch < pcmDataIn.NumChannels; ++ch) {
                        var v = pcmDataIn.GetSampleValueInDouble(ch, pos);
                        for (int i=0; i < mFreqMagnitude; ++i) {
                            pcm.SetSampleValueInDouble(ch, pos * mFreqMagnitude+i, v);
                        }
                    }
                });
                break;

            case ItUpsampleType.LinearInterpolation:
                Parallel.For(0, pcmDataIn.NumFrames - 1, (pos) => {
                    // 0 <= pos <= NumFrames-2まで実行する
                    for (int ch=0; ch < pcmDataIn.NumChannels; ++ch) {
                        var v0 = pcmDataIn.GetSampleValueInDouble(ch, pos);
                        var v1 = pcmDataIn.GetSampleValueInDouble(ch, pos + 1);
                        for (int i=0; i < mFreqMagnitude; ++i) {
                            var ratio = (double)i / mFreqMagnitude;
                            var v = v0 * (1 - ratio) + v1 * ratio;
                            pcm.SetSampleValueInDouble(ch, pos * mFreqMagnitude + i, v);
                        }
                    }
                });

                // 最後の1区間はサンプルホールドする
                for (int ch=0; ch < pcmDataIn.NumChannels; ++ch) {
                    var pos = pcmDataIn.NumFrames-1;
                    var v = pcmDataIn.GetSampleValueInDouble(ch, pos);
                    for (int i=0; i < mFreqMagnitude; ++i) {
                        pcm.SetSampleValueInDouble(ch, pos * mFreqMagnitude + i, v);
                    }
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            return true;
        }
    }
}
