using System;
using System.Collections.Generic;
using System.Globalization;

namespace WWAudioFilter {
    class CicFilter : FilterBase {
        public enum CicType {
            SingleStage,
            NUM
        }
        public CicType Type { get; set; }
        public int Delay { get; set; }

        public CicFilter(CicType type, int delay)
                : base(FilterType.CicFilter) {
            Type = type;
            Delay = delay;
        }

        public override FilterBase CreateCopy() {
            return new CicFilter(Type, Delay);
        }

        public override string ToDescriptionText() {
            return string.Format(CultureInfo.CurrentCulture, Properties.Resources.FilterCicFilterDesc, Type, Delay);
        }

        public override string ToSaveText() {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}", Type, Delay);
        }

        public static FilterBase Restore(string[] tokens) {
            if (tokens.Length != 3) {
                return null;
            }

            int type;
            if (!Int32.TryParse(tokens[1], out type) || type < 0 || type <= (int)CicType.NUM) {
                return null;
            }

            int delay;
            if (!Int32.TryParse(tokens[2], out delay) || delay < 1) {
                return null;
            }

            return new CicFilter((CicType)type, delay);
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
            return inputFormat;
        }

        private Queue<double> mCombQueue = new Queue<double>();
        private double mIntegratorZ = 0.0;

        public override double[] FilterDo(double[] inPcm) {
            System.Diagnostics.Debug.Assert(inPcm.LongLength == NumOfSamplesNeeded());
            double v = inPcm[0];
            mCombQueue.Enqueue(v);
            if (Delay < mCombQueue.Count) {
                v -= mCombQueue.Dequeue();
            }

            v += mIntegratorZ;
            mIntegratorZ = v;
            return new double[] {v};
        }
    }
}
