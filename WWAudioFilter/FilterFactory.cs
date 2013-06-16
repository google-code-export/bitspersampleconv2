using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WWAudioFilter {
    class FilterFactory {
        public static FilterBase Create(string s) {
            var tokens = s.Split(null);
            if (tokens == null || tokens.Length < 1) {
                return null;
            }

            switch (tokens[0]) {
            case "Gain": {
                    if (tokens.Length != 2) {
                        return null;
                    }

                    double amplitude;
                    if (!Double.TryParse(tokens[1], out amplitude) || amplitude <= Double.Epsilon) {
                        return null;
                    }

                    return new GainFilter(amplitude);
                }
            default:
                return null;
            }
        }
    }
}
