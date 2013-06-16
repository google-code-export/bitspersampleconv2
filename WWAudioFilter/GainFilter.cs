using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            return string.Format("{0} Gain : {1}x ({2:0.00}dB)", FilterId, Amplitude, 20.0 * Math.Log10(Amplitude));
        }

        public override string ToSaveText() {
            return string.Format("{0}", Amplitude);
        }
    }
}
