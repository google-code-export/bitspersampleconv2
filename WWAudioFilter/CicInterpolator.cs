using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WWAudioFilter {
    class CicInterpolator : FilterBase {
        public enum CicType {
            Interpolation1stOrder4x,
            NUM
        }
        public CicType Type { get; set; }
        public int Factor { get; set; }

        public CicInterpolator(CicType type)
                : base(FilterType.CicInterpolator) {
            Type = type;
            Factor = 4;
        }

        public override FilterBase CreateCopy() {
            return new CicInterpolator(Type);
        }

        public override string ToDescriptionText() {
            return string.Format(CultureInfo.CurrentCulture, Properties.Resources.FilterCicInterpolatorDesc, Type, Factor);
        }

        public override string ToSaveText() {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}", Type, Factor);
        }

        public static FilterBase Restore(string[] tokens) {
            if (tokens.Length != 3) {
                return null;
            }

            int type;
            if (!Int32.TryParse(tokens[1], out type) || type < 0 || type <= (int)CicType.NUM) {
                return null;
            }

            // factorは未使用。
            int factor;
            if (!Int32.TryParse(tokens[2], out factor) || factor <= 1 || !IsPowerOfTwo(factor)) {
                return null;
            }

            return new CicInterpolator((CicType)type);
        }

        public override long NumOfSamplesNeeded() {
            return 1;
        }

        public override void FilterStart() {
            base.FilterStart();
            mCombQueue.Clear();
            mIntegratorZ = 0.0;
        }

        public override void FilterEnd() {
            base.FilterEnd();
        }
        
        public override PcmFormat Setup(PcmFormat inputFormat) {
            var r = new PcmFormat(inputFormat);
            r.SampleRate *= Factor;
            r.NumSamples *= Factor;
            return r;
        }

        private Queue<double> mCombQueue = new Queue<double>();
        private double mIntegratorZ = 0.0;

        private double [] Cic4xInterpolator(double inPcm) {
            double[] interpolated = new double[Factor];
            interpolated[0] = inPcm;

            double[] result = new double[Factor];
            for (int i = 0; i < Factor; ++i) {
                mCombQueue.Enqueue(interpolated[i]);
                double v = interpolated[i];
                if (Factor*2 < mCombQueue.Count) {
                    v -= mCombQueue.Dequeue();
                }

                v += mIntegratorZ;
                mIntegratorZ = v;

                result[i] = v;
            }
            return result;
        }

        public override double[] FilterDo(double[] inPcm) {
            System.Diagnostics.Debug.Assert(inPcm.LongLength == NumOfSamplesNeeded());

            return Cic4xInterpolator(inPcm[0]);
        }
    }
}
