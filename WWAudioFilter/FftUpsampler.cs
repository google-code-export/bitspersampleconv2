using System;
using System.Globalization;

namespace WWAudioFilter {
    class FftUpsampler : FilterBase {
        private const int FFT_LENGTH = 262144;
        private const int OVERLAP_LENGTH = 65536;

        public int Factor { get; set; }

        private bool mFirst;
        private double [] mOverlapSamples;

        public FftUpsampler(int factor)
                : base(FilterType.FftUpsampler) {
            if (factor <= 1 || !IsPowerOfTwo(factor)) {
                throw new ArgumentException("factor must be power of two integer and larger than 1");
            }
            Factor = factor;

            System.Diagnostics.Debug.Assert(
                    IsPowerOfTwo(FFT_LENGTH) && IsPowerOfTwo(OVERLAP_LENGTH)
                    && OVERLAP_LENGTH * 2 < FFT_LENGTH);
        }

        public override FilterBase CreateCopy() {
            return new FftUpsampler(Factor);
        }

        public override string ToDescriptionText() {
            return string.Format(CultureInfo.CurrentCulture, Properties.Resources.FilterFftUpsampleDesc, Factor);
        }

        public override string ToSaveText() {
            return string.Format(CultureInfo.InvariantCulture, "{0}", Factor);
        }

        public static FilterBase Restore(string[] tokens) {
            if (tokens.Length != 2) {
                return null;
            }

            int factor;
            if (!Int32.TryParse(tokens[1], out factor) || factor <= 1 || !IsPowerOfTwo(factor)) {
                return null;
            }

            return new FftUpsampler(factor);
        }

        public override long NumOfSamplesNeeded() {
            if (mFirst) {
                return FFT_LENGTH - OVERLAP_LENGTH;
            } else {
                return FFT_LENGTH - OVERLAP_LENGTH * 2;
            }
        }

        public override void FilterStart() {
            base.FilterStart();

            mOverlapSamples = null;
            mFirst = true;
        }

        public override void FilterEnd() {
            base.FilterEnd();

            mOverlapSamples = null;
            mFirst = true;
        }
        
        public override PcmFormat Setup(PcmFormat inputFormat) {
            var r = new PcmFormat(inputFormat);
            r.SampleRate *= Factor;
            r.NumSamples *= Factor;
            return r;
        }

        public override double[] FilterDo(double[] inPcm) {
            System.Diagnostics.Debug.Assert(inPcm.LongLength == NumOfSamplesNeeded());

            var inPcmR = new double[FFT_LENGTH];
            if (mFirst) {
                Array.Copy(inPcm, 0, inPcmR, OVERLAP_LENGTH, inPcm.LongLength);
                
                mFirst = false;
            } else {
                System.Diagnostics.Debug.Assert(mOverlapSamples != null
                        && mOverlapSamples.LongLength == OVERLAP_LENGTH*2);

                Array.Copy(mOverlapSamples, 0, inPcmR, 0, OVERLAP_LENGTH * 2);
                mOverlapSamples = null;
                Array.Copy(inPcm, 0, inPcmR, OVERLAP_LENGTH * 2, inPcm.LongLength);
            }

            // inPcmTをFFTしてinPcmFを得る。
            var inPcmT = new WWComplex[FFT_LENGTH];
            for (int i=0; i < inPcmT.Length; ++i) {
                inPcmT[i] = new WWComplex(inPcmR[i], 0);
            }

            var inPcmF = new WWComplex[FFT_LENGTH];
            {
                var fft = new WWRadix2Fft(FFT_LENGTH);
                fft.ForwardFft(inPcmT, inPcmF);
            }
            inPcmT = null;

            // inPcmFを0で水増ししたデータoutPcmFを作って逆FFTしoutPcmTを得る。

            var UPSAMPLE_FFT_LENGTH = Factor * FFT_LENGTH;

            var outPcmF = new WWComplex[UPSAMPLE_FFT_LENGTH];
            for (int i=0; i < outPcmF.Length; ++i) {
                if (i <= FFT_LENGTH / 2) {
                    outPcmF[i].CopyFrom(inPcmF[i]);
                    if (i == FFT_LENGTH / 2) {
                        outPcmF[i].Mul(0.5);
                    }
                } else if (UPSAMPLE_FFT_LENGTH - FFT_LENGTH / 2 <= i) {
                    int pos = i + FFT_LENGTH - UPSAMPLE_FFT_LENGTH;
                    outPcmF[i].CopyFrom(inPcmF[pos]);
                    if (outPcmF.Length - FFT_LENGTH / 2 == i) {
                        outPcmF[i].Mul(0.5);
                    }
                } else {
                    // do nothing
                }
            }
            inPcmF = null;
            var outPcmT = new WWComplex[UPSAMPLE_FFT_LENGTH];
            {
                var fft = new WWRadix2Fft(UPSAMPLE_FFT_LENGTH);
                fft.InverseFft(outPcmF, outPcmT, 1.0 / (UPSAMPLE_FFT_LENGTH / 2));
            }
            outPcmF = null;

            // outPcmTの実数成分を戻り値とする。
            var outPcm = new double[Factor * (FFT_LENGTH - OVERLAP_LENGTH*2)];
            for (int i=0; i < outPcm.Length; ++i) {
                outPcm[i] = outPcmT[i + Factor * OVERLAP_LENGTH].real;
            }
            outPcmT = null;

            // 次回計算に使用するオーバーラップ部分のデータをmOverlapSamplesに保存。
            mOverlapSamples = new double[OVERLAP_LENGTH * 2];
            Array.Copy(inPcm, inPcm.LongLength - OVERLAP_LENGTH * 2, mOverlapSamples, 0, OVERLAP_LENGTH * 2);

            return outPcm;
        }
    }
}
