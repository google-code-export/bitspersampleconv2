using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WWAudioFilter {
    class ZeroOrderHoldUpsampler : FilterBase {
        public int Factor { get; set; }

        public ZeroOrderHoldUpsampler(int factor)
                : base(FilterType.ZOH) {
            if (factor <= 1 || !IsPowerOfTwo(factor)) {
                throw new ArgumentException();
            }

            Factor = factor;
        }

        public override string ToDescriptionText() {
            return string.Format("Zero order hold upsample: {0}x", Factor);
        }

        public override string ToSaveText() {
            return string.Format("{0}", Factor);
        }

        public static FilterBase Restore(string[] tokens) {
            if (tokens.Length != 2) {
                return null;
            }

            int factor;
            if (!Int32.TryParse(tokens[1], out factor) || factor <= 1 || !IsPowerOfTwo(factor)) {
                return null;
            }

            return new ZeroOrderHoldUpsampler(factor);
        }

        public override long NumOfSamplesNeeded() {
            return 8192;
        }

        public override PcmFormat Setup(PcmFormat inputFormat) {
            var r = new PcmFormat(inputFormat);
            r.SampleRate *= Factor;
            r.NumSamples *= Factor;
            return r;
        }

        public override double[] FilterDo(double[] inPcm) {

            double [] outPcm = new double[inPcm.LongLength * Factor];
            long pos=0;
            for (long i=0; i < inPcm.LongLength; ++i) {
                for (int r=0; r < Factor; ++r) {
                    outPcm[pos] = inPcm[i];
                    ++pos;
                }
            }
            return outPcm;
        }

        private static bool IsPowerOfTwo(int x) {
            return (x != 0) && ((x & (x - 1)) == 0);
        }
    }
}
