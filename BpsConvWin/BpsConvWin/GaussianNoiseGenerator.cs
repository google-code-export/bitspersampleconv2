using System;
using System.Security.Cryptography;

namespace BpsConvWin {
    class GaussianNoiseGenerator {
        private RNGCryptoServiceProvider mRng = new RNGCryptoServiceProvider();

        /// <summary>
        /// returns white gaussian noise σ_e^2 in the range [-1 1)
        /// </summary>
        private float NextFloatBoxMuller() {
            double rD = 0.0;

            do {
                const double dDiv   = 1.0 / ((double)UInt32.MaxValue+1.0);
                byte[] b4 = new byte[4];

                mRng.GetNonZeroBytes(b4);
                uint   v = BitConverter.ToUInt32(b4, 0);
                double d1 = ((double)v) * dDiv;

                mRng.GetBytes(b4);
                v = BitConverter.ToUInt32(b4, 0);
                double d2 = ((double)v) * dDiv;

                rD = Math.Sqrt(-2.0 * Math.Log(d1)) * Math.Cos(2.0 * Math.PI * d2);
                rD /= Math.E;
            } while ((float)rD < -1.0f || 1.0f <= (float)rD);

            return (float)(rD);
        }

        public float NextFloat() {
            return NextFloatBoxMuller();
        }
    }
}
