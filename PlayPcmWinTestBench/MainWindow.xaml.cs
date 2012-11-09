// 日本語UTF-8

using System;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Windows;
using Wasapi;
using System.Runtime.InteropServices;

namespace PlayPcmWinTestBench {
    public partial class MainWindow : Window, IDisposable {
        Wasapi.WasapiCS wasapi;

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                m_playWorker.Dispose();
                gen.Dispose();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public MainWindow() {
            InitializeComponent();

            wasapi = new WasapiCS();
            wasapi.Init();

            Prepare();
        }

        private void Prepare() {
            InitUpsampleTab();
            InitAbxTab();
            InitFirTab();
            InitHilbTab();
            InitAnalyticSignalTab();
            InitItUpsample();
        }

        private void Window_Closed(object sender, EventArgs e) {
            if (wasapi != null) {
                Stop();

                // バックグラウンドスレッドにjoinして、完全に止まるまで待ち合わせする。
                // そうしないと、バックグラウンドスレッドによって使用中のオブジェクトが
                // この後のTermの呼出によって開放されてしまい問題が起きる。

                while (m_playWorker.IsBusy) {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));

                    System.Threading.Thread.Sleep(100);
                }

                wasapi.UnchooseDevice();
                wasapi.Term();
                wasapi = null;
            }

        }


        //////////////////////////////////////////////////////////////////////////////////////////
        // 互換性実験

        

        private void buttonCompatibility_Click(object sender, RoutedEventArgs e) {
            int hr = NativeMethods.WWDirectDrawTest_Test();
            textBoxCompatibility.Text += string.Format("DirectDraw PrimarySurface Lock {0:X8} {1}\r\n",
                hr, hr==0 ? "成功" : "失敗");
        }

        private void buttonTest1_Click(object sender, RoutedEventArgs e) {
            {
                double[] v = { 0, 0, 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0, 7, 0};

                var dft = new WWDirectComputeCS.WWDftCpu();
                double [] rvD;
                dft.Dft1d(v, out rvD);

                var rvF = WWDirectComputeCS.WWFFTCpu.ComplexFFT(v);
                var rvIF = WWDirectComputeCS.WWFFTCpu.ComplexIFFT(rvF);

            }
            {
                double[] v = { 1, 1, 1, 1 };

                var dft = new WWDirectComputeCS.WWDftCpu();
                double [] rvD;
                dft.Dft1d(v, out rvD);

                var rvF = WWDirectComputeCS.WWFFTCpu.ComplexFFT(v);

            }
            {
                double[] v = { 0, 1, 2, 3, 4, 5, 6, 7 };

                var dft = new WWDirectComputeCS.WWDftCpu();
                double [] rvD;
                dft.Dft1d(v, out rvD);

                var rvF = WWDirectComputeCS.WWFFTCpu.ComplexFFT(v);

            }
            {
                double[] v = { 1, 0, 1, 0, 1, 0, 1, 0 };
                var rv = WWDirectComputeCS.WWFFTCpu.ComplexFFT(v);
            }
            {
                double[] v = { 0, 1, 0, 1, 0, 1, 0, 1 };
                var rv = WWDirectComputeCS.WWFFTCpu.ComplexFFT(v);
            }

            {
                double[] v = { 0, 0, 1, 0, 0, 0, -1, 0 };
                var rv = WWDirectComputeCS.WWFFTCpu.ComplexFFT(v);
            }

            {
                double[] v = { 1, 0, 0, 0, -1, 0, 0, 0 };
                var rv = WWDirectComputeCS.WWFFTCpu.ComplexFFT(v);
            }
        }
    }

    internal static class NativeMethods {
        [DllImport("WWDirectDrawTest.dll")]
        internal extern static int
        WWDirectDrawTest_Test();
    }
}
