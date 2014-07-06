using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WWCrossFeed {
    class WWFirCoefficient {
        public double DelaySecond { get; set; }
        public double Gain { get; set; }
        public WWFirCoefficient(double delaySecond, double gain) {
            DelaySecond = delaySecond;
            Gain = gain;
        }

        public WWFirCoefficient() {

        }
    }
}
