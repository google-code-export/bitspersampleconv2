﻿using System;

namespace WWAudioFilter {
    public class GainFilter : FilterBase {
        public double Amplitude { get; set; }

        public GainFilter(double amplitude)
            : base(FilterType.Gain) {
            if (amplitude < 0) {
                throw new ArgumentOutOfRangeException();
            }

            Amplitude = amplitude;
        }

        public override string ToDescriptionText() {
            return string.Format(Properties.Resources.FilterGainDesc, Amplitude, 20.0 * Math.Log10(Amplitude));
        }

        public override string ToSaveText() {
            return string.Format("{0}", Amplitude);
        }

        public static FilterBase Restore(string[] tokens) {
            if (tokens.Length != 2) {
                return null;
            }

            double amplitude;
            if (!Double.TryParse(tokens[1], out amplitude) || amplitude <= Double.Epsilon) {
                return null;
            }

            return new GainFilter(amplitude);
        }

        public override double[] FilterDo(double[] inPcm) {
            double [] outPcm = new double[inPcm.LongLength];
            for (long i=0; i < outPcm.LongLength; ++i) {
                outPcm[i] = inPcm[i] * Amplitude;
            }
            return outPcm;
        }
    }
}