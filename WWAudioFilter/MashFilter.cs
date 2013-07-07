// under construction

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace WWAudioFilter {
    class MashFilter : FilterBase {
        public int TargetBitsPerSample { get; set; }
        private NoiseShaperMash mNoiseShaper;
        private NoiseShaperMash1bit mNoiseShaper1bit;

        public MashFilter(int targetBitsPerSample)
                : base(FilterType.Mash2) {
            if (targetBitsPerSample <= 0 || 24 < targetBitsPerSample) {
                throw new ArgumentOutOfRangeException("targetBitsPerSample");
            }
            TargetBitsPerSample = targetBitsPerSample;
        }

        public override FilterBase CreateCopy() {
            return new MashFilter(TargetBitsPerSample);
        }

        public override string ToDescriptionText() {
            return string.Format(CultureInfo.CurrentCulture, Properties.Resources.FilterMashDesc, TargetBitsPerSample);
        }

        public override string ToSaveText() {
            return string.Format(CultureInfo.InvariantCulture, "{0}", TargetBitsPerSample);
        }

        public static FilterBase Restore(string[] tokens) {
            if (tokens.Length != 2) {
                return null;
            }

            int tbps;
            if (!Int32.TryParse(tokens[1], out tbps) || tbps < 1 || 23 < tbps) {
                return null;
            }

            return new MashFilter(tbps);
        }

        public override PcmFormat Setup(PcmFormat inputFormat) {
            return new PcmFormat(inputFormat);
        }

        public override void FilterStart() {
            if (1 == TargetBitsPerSample) {
                mNoiseShaper1bit = new NoiseShaperMash1bit();
            } else {
                mNoiseShaper = new NoiseShaperMash(TargetBitsPerSample);
            }
        }

        public override void FilterEnd() {
            mNoiseShaper = null;
        }

        public override double[] FilterDo(double[] inPcm) {
            double [] outPcm = new double[inPcm.LongLength];

            if (1 == TargetBitsPerSample) {
                for (long i=0; i < outPcm.LongLength; ++i) {
                    double sampleD = inPcm[i];

                    int sampleI24;
                    if (1.0 <= sampleD) {
                        sampleI24 = 8388607;
                    } else if (sampleD < -1.0) {
                        sampleI24 = -8388608;
                    } else {
                        sampleI24 = (int)(8388608 * sampleD);
                    }

                    sampleI24 = mNoiseShaper1bit.Filter24(sampleI24);

                    outPcm[i] = (double)sampleI24 * (1.0 / 8388608);
                }
            } else {
                for (long i=0; i < outPcm.LongLength; ++i) {
                    double sampleD = inPcm[i];

                    int sampleI24;
                    if (1.0 <= sampleD) {
                        sampleI24 = 8388607;
                    } else if (sampleD < -1.0) {
                        sampleI24 = -8388608;
                    } else {
                        sampleI24 = (int)(8388608 * sampleD);
                    }

                    sampleI24 = mNoiseShaper.Filter24(sampleI24);

                    outPcm[i] = (double)sampleI24 * (1.0 / 8388608);
                }
            }

            return outPcm;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        // 2 or more bit version

        class SigmaDelta {
            private int mQuantizedBit;
            private uint mMask;
            private double mDelayX;
            private double mDelayY;
            private double mQuantizationError;

            /// <summary>
            /// SigmaDelta noise shaping system
            /// </summary>
            /// <param name="quantizedBit">quantizer parameter. 1 to 23</param>
            public SigmaDelta(int quantizedBit) {
                if (quantizedBit < 1 || 23 < quantizedBit) {
                    throw new System.ArgumentException();
                }
                mQuantizedBit = quantizedBit;

                mMask = 0xffffff00U << (24 - mQuantizedBit);
            }

            public double QuantizationError() {
                return mQuantizationError;
            }

            /// <summary>
            /// input sampleFrom, returns quantized sample value
            /// </summary>
            /// <param name="sampleFrom">input data. 24bit signed (-2^23 to +2^23-1)</param>
            /// <returns>filtered value. 24bit signed</returns>
            public int Filter24(double sampleFrom) {
                // convert quantized bit rate to 32bit integer
                sampleFrom *= 256;

                double x = sampleFrom + mDelayX - mDelayY;
                mDelayX = x;

                double y1q = x;
                if (y1q > Int32.MaxValue) {
                    y1q = Int32.MaxValue;
                }
                if (y1q < Int32.MinValue) {
                    y1q = Int32.MinValue;
                }

                int sampleY = (int)(((int)y1q) & mMask);
                mDelayY = sampleY;

                mQuantizationError = (sampleY - x) / 256;

                return sampleY / 256;
            }
        }

        class NoiseShaperMash {
            private SigmaDelta [] mSds;
            private double mDelayY1;
            private double mDelayY2;

            public NoiseShaperMash(int quantizedBit) {
                mSds = new SigmaDelta[2];

                for (int i=0; i < 2; ++i) {
                    mSds[i] = new SigmaDelta(quantizedBit);
                }
            }

            public int Filter24(int sampleFrom) {
                double y1 = mSds[0].Filter24(sampleFrom);
                double y2 = mSds[1].Filter24(mSds[0].QuantizationError());

                double r = mDelayY1 - (y2 - mDelayY2);

                mDelayY1 = y1;
                mDelayY2 = y2;

                r *= 256;

                if (r > Int32.MaxValue) {
                    r = Int32.MaxValue;
                }
                if (r < Int32.MinValue) {
                    r = Int32.MinValue;
                }

                r /= 256;
                return (int)r;
            }
        }

        ///////////////////////////////////////////////////////////////////////
        // 1bit version

        class SigmaDelta1bit {
            private double mDelayX;
            private double mDelayY;
            private double mQuantizationError;

            /// <summary>
            /// SigmaDelta 1bit noise shaping system
            /// </summary>
            public SigmaDelta1bit() {
            }

            public double QuantizationError() {
                return mQuantizationError;
            }

            /// <summary>
            /// input sampleFrom, returns quantized sample value
            /// </summary>
            /// <param name="sampleFrom">input data. 24bit signed (-2^23 to +2^23-1)</param>
            /// <returns>filtered value. 24bit signed</returns>
            public int Filter24(double sampleFrom) {
                // convert quantized bit rate to 32bit integer
                sampleFrom *= 256;

                double x = sampleFrom + mDelayX - mDelayY;
                mDelayX = x;

                double y1q = x;

                int sampleY = (0 <= y1q) ? Int32.MaxValue : Int32.MinValue;
                mDelayY = sampleY;

                mQuantizationError = (sampleY - x) / 256;

                return sampleY / 256;
            }
        }

        class NoiseShaperMash1bit {
            private SigmaDelta1bit [] mSds;
            private double mDelayY1;
            private double mDelayY2;

            public NoiseShaperMash1bit() {
                mSds = new SigmaDelta1bit[2];

                for (int i=0; i < 2; ++i) {
                    mSds[i] = new SigmaDelta1bit();
                }
            }

            public int Filter24(int sampleFrom) {
                double y1 = mSds[0].Filter24(sampleFrom);
                double y2 = mSds[1].Filter24(mSds[0].QuantizationError());

                double r = mDelayY1 - (y2 - mDelayY2);

                mDelayY1 = y1;
                mDelayY2 = y2;

                return (0 <= r) ? 8388607 : -8388608;
            }
        }
    }
}
