using System;

namespace WWAudioFilter {
    class LowpassFilter : FilterBase {
        // フィルターの長さ-1。2のべき乗の値である必要がある
        private const int FILTER_LENGTHM1 = 65536;

        // 2のべき乗の値である必要がある
        private const int FFT_LENGTH    = 262144;

        public int SampleRate { get; set; }
        public double CutoffFrequency { get; set; }
        public WWComplex [] mFilterFreq;
        public double [] mIfftAddBuffer;

        private static bool IsPowerOfTwo(int x) {
            return (x != 0) && ((x & (x - 1)) == 0);
        }

        public LowpassFilter(double cutoffFrequency)
                : base(FilterType.LPF) {
            if (cutoffFrequency < 0.0) {
                throw new ArgumentOutOfRangeException();
            }
            CutoffFrequency = cutoffFrequency;

            System.Diagnostics.Debug.Assert(!IsPowerOfTwo(FILTER_LENGTHM1) && !IsPowerOfTwo(FFT_LENGTH) && FILTER_LENGTHM1 < FFT_LENGTH);
        }

        public override PcmFormat Setup(PcmFormat inputFormat) {
            SampleRate = inputFormat.SampleRate;
            DesignCutoffFilter();
            mIfftAddBuffer = null;

            return new PcmFormat(inputFormat);
        }

        public override string ToDescriptionText() {
            return string.Format("LPF : Cutoff={0}Hz", CutoffFrequency);
        }

        public override string ToSaveText() {
            return string.Format("{0}", CutoffFrequency);
        }

        public static FilterBase Restore(string[] tokens) {
            if (tokens.Length != 2) {
                return null;
            }

            double cutoffFrequency;
            if (!Double.TryParse(tokens[1], out cutoffFrequency) || cutoffFrequency <= 0) {
                return null;
            }

            return new LowpassFilter(cutoffFrequency);
        }

        public override long NumOfSamplesNeeded() {
            return FFT_LENGTH - FILTER_LENGTHM1;
        }

        private void DesignCutoffFilter() {
            // 100次のバターワースフィルター
            double orderX2 = 2.0 * 100;

            // フィルタのF特
            var filterFreq64k = new WWComplex[FILTER_LENGTHM1];
            filterFreq64k[0].real = 1.0f;
            for (int i=1; i <= FILTER_LENGTHM1 / 2; ++i) {
                double omegaRatio = i * (1.0 / (FILTER_LENGTHM1 / 2));
                filterFreq64k[i].real = Math.Sqrt(1.0 / 1.0 + Math.Pow(omegaRatio / CutoffFrequency, orderX2));
            }
            for (int i=1; i < FILTER_LENGTHM1 / 2; ++i) {
                filterFreq64k[FILTER_LENGTHM1 - i].real = filterFreq64k[i].real;
            }

            // 逆FFTしてHsmall(jω)を作る
            var hSmall = new WWComplex[FILTER_LENGTHM1];
            {
                var fft = new WWRadix2Fft(FILTER_LENGTHM1);
                fft.Fft(filterFreq64k, hSmall);

                double compensate = 1.0 / FILTER_LENGTHM1;
                for (int i=0; i < hSmall.Length; ++i) {
                    hSmall[i] = new WWComplex(
                            hSmall[i].imaginary * compensate,
                            hSmall[i].real * compensate);
                }
            }

            // FFT_LENGTHの長さのHlarge(jω)にする
            var hLarge = new WWComplex[FFT_LENGTH];
            for (int i=0; i < hSmall.Length; ++i) {
                hLarge[i].CopyFrom(hSmall[i]);
            }

            // hLargeをFFTしてmFilterFreqを作成する
            mFilterFreq = new WWComplex[FFT_LENGTH];
            {
                var fft = new WWRadix2Fft(FFT_LENGTH);
                fft.Fft(hLarge, mFilterFreq);

                // TODO: フィルターの値を最大値1.0になるようにする
                double compensate = 1.0;
                for (int i=0; i < mFilterFreq.Length; ++i) {
                    mFilterFreq[i] = new WWComplex(
                            mFilterFreq[i].real * compensate,
                            mFilterFreq[i].imaginary * compensate);
                }
            }
        }

        public override double[] FilterDo(double[] inPcm) {
            System.Diagnostics.Debug.Assert(inPcm.LongLength <= NumOfSamplesNeeded());

            // Overlap and add continuous FFT

            var inTime = new WWComplex[FFT_LENGTH];
            for (int i=0; i < inPcm.LongLength; ++i) {
                inTime[i] = new WWComplex(inPcm[i], 0.0);
            }

            var inFreq = new WWComplex[FFT_LENGTH];
            {
                var fft = new WWRadix2Fft(FFT_LENGTH);
                fft.Fft(inTime, inFreq);
            }
            inTime = null;

            // FFT後、フィルターHの周波数ドメインデータを掛ける
            for (int i=0; i < FFT_LENGTH; ++i) {
                inFreq[i].Mul(mFilterFreq[i]);
            }

            // IFFTする
            var outTime = new WWComplex[FFT_LENGTH];
            {
                var fft = new WWRadix2Fft(FFT_LENGTH);
                fft.Fft(inFreq, outTime);

                double compensate = 1.0 / FFT_LENGTH;
                for (int i=0; i < outTime.Length; ++i) {
                    outTime[i] = new WWComplex(
                            outTime[i].imaginary * compensate,
                            outTime[i].real * compensate);
                }
            }
            inFreq = null;

            var outReal = new double[NumOfSamplesNeeded()];
            for (int i=0; i < outReal.Length; ++i) {
                outReal[i] = outTime[i].real;
            }

            // 前回のIFFT結果の最後のFILTER_LENGTH-1サンプルを先頭に加算する
            if (null != mIfftAddBuffer) {
                for (int i=0; i < outReal.Length; ++i) {
                    outReal[i] += mIfftAddBuffer[i];
                }
            }

            // 今回のIFFT結果の最後のFILTER_LENGTH-1サンプルをmIfftAddBufferとして保存する
            mIfftAddBuffer = new double[FILTER_LENGTHM1];
            for (int i=0; i < mIfftAddBuffer.Length; ++i) {
                mIfftAddBuffer[i] = outTime[outTime.Length - mIfftAddBuffer.Length + i].real;
            }
            outTime = null;

            return outReal;
        }
    }
}
