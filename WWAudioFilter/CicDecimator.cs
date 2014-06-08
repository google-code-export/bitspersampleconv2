// Still does not work properly !!!

using System;
using System.Collections.Generic;
using System.Globalization;

namespace WWAudioFilter {
    class CicDecimator : FilterBase {
        public enum CicType {
            Decimation1stOrder8x,
            Decimation1stOrder8xWithCompensation,
            NUM
        }
        public CicType Type { get; set; }
        public int Factor { get; set; }

        public CicDecimator(CicType type)
                : base(FilterType.CicDecimator) {
            Type = type;
            Factor = 8;
        }

        public override FilterBase CreateCopy() {
            return new CicDecimator(Type);
        }

        public override string ToDescriptionText() {
            return string.Format(CultureInfo.CurrentCulture, Properties.Resources.FilterCicDecimatorDesc, Type, Factor);
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

            return new CicDecimator((CicType)type);
        }

        public override long NumOfSamplesNeeded() {
            return Factor;
        }

        public override void FilterStart() {
            base.FilterStart();
            mIntegratorZ = 0;
            mCombQueue.Clear();
            mCompensatorQueue.Clear();
        }

        public override void FilterEnd() {
            base.FilterEnd();
        }
        
        public override PcmFormat Setup(PcmFormat inputFormat) {
            var r = new PcmFormat(inputFormat);
            r.SampleRate /= Factor;
            r.NumSamples /= Factor;
            return r;
        }

        private double mIntegratorZ;
        private Queue<double> mCombQueue = new Queue<double>();

        private double Cic8xDecimator(double[] inPcm) {
            System.Diagnostics.Debug.Assert(inPcm.LongLength == NumOfSamplesNeeded());

            double integratedSample = inPcm[0] + mIntegratorZ;
            mIntegratorZ = inPcm[Factor - 1];

            mCombQueue.Enqueue(mIntegratorZ);

            double result = integratedSample;

            if (Factor < mCombQueue.Count) {
                result -= mCombQueue.Dequeue();
            }
            return result;
        }

        private Queue<double> mCompensatorQueue = new Queue<double>();

        /// <summary>
        /// この係数の並び順は慣用的なFIR係数の並び順とは逆で、古いサンプルに掛ける係数から始め、新しいサンプルに掛ける係数へと順番に並べる。
        /// </summary>
        private static readonly double [] mCompensatorCoeffs = new double [3] { -1.0/16, 9.0/8, -1.0/16 };

        private double Compensator(double input) {
            mCompensatorQueue.Enqueue(input);
            while (mCompensatorCoeffs.Length < mCompensatorQueue.Count) {
                mCompensatorQueue.Dequeue();
            }

            double result = 0.0;
            int i=0;
            foreach (double v in mCompensatorQueue) {
                // vは、古いサンプルから新しいサンプルの順で取り出される。
                result += v * mCompensatorCoeffs[i];
                ++i;
            }

            return result;
        }

        public override double[] FilterDo(double[] inPcm) {
            System.Diagnostics.Debug.Assert(inPcm.LongLength == NumOfSamplesNeeded());

            double result = Cic8xDecimator(inPcm);

            switch (Type) {
            case CicType.Decimation1stOrder8x:
                break;
            case CicType.Decimation1stOrder8xWithCompensation:
                result = Compensator(result);
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            return new double[] { result };
        }
    }
}
