using System;

namespace WWDirectComputeCS {
    public class WWHilbert {

        public static double[] HilbertFirCoeff(int filterLength) {
            System.Diagnostics.Debug.Assert(0 < filterLength && ((filterLength & 1) == 1));

            double [] rv = new double[filterLength];

            for (int i=0; i < filterLength; ++i) {
                int m = i - filterLength / 2;
                if ((m & 1) == 0) {
                    rv[i] = 0;
                } else {
                    rv[i] = 2.0 / m / Math.PI;
                }
            }

            return rv;
        }
    }
}
