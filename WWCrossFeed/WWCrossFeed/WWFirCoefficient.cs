using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WWCrossFeed {
    class WWFirCoefficient {
        public double DelaySecond { get; set; }
        public double Gain { get; set; }
        public bool IsDirect { get; set; }

        public WWFirCoefficient(double delaySecond, double gain, bool isDirect) {
            DelaySecond = delaySecond;
            Gain = gain;
            IsDirect = isDirect;
        }
    }
}
