using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WWAudioFilter {
    class FilterFactory {
        private FilterFactory() {
        }

        public static FilterBase Create(string s) {
            var tokens = s.Split(null);
            if (tokens == null || tokens.Length < 1) {
                return null;
            }

            switch (tokens[0]) {
            case "Gain":
                return GainFilter.Restore(tokens);
            case "ZohUpsampler":
                return ZeroOrderHoldUpsampler.Restore(tokens);
            case "LowPassFilter":
                return LowpassFilter.Restore(tokens);
            case "FftUpsampler":
                return FftUpsampler.Restore(tokens);
            default:
                return null;
            }
        }
    }
}
