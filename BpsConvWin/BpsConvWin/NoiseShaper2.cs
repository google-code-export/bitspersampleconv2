using System;

namespace BpsConvWin {
    class NoiseShaper2 {
        private int  mOrder;
        private int  mQuantizedBit;
        private uint mMask;
        private double [] mDelay;
        private double [] mCoeffs;

        /// <summary>
        /// noise shaping filter
        /// </summary>
        /// <param name="order">filter order</param>
        /// <param name="coefficients">filter coefficients. element count == filter order+1</param>
        /// <param name="quantizedBit">target quantized bit. 1 to 23</param>
        public NoiseShaper2(int order, double[] coefficients, int quantizedBit) {
            if (order < 1) {
                throw new System.ArgumentException();
            }
            mOrder = order;

            if (coefficients.Length != mOrder+1) {
                throw new System.ArgumentException();
            }
            mCoeffs = coefficients;

            mDelay = new double[mOrder];

            if (quantizedBit < 1 || 23 < quantizedBit) {
                throw new System.ArgumentException();
            }
            mQuantizedBit = quantizedBit;
            mMask = 0xffffff00U << (24 - mQuantizedBit);
        }

        /// <summary>
        /// returns sample value its quantization bit is reduced to quantizedBit
        /// </summary>
        /// <param name="sampleFrom">input sample value. 24bit signed (-2^23 to +2^23-1)</param>
        /// <returns>noise shaping filter output sample. 24bit signed</returns>
        public int Filter24(int sampleFrom) {
            // convert quantized bit rate to 32bit
            sampleFrom <<= 8;

            double v = mCoeffs[0] * sampleFrom;

            for (int i=0; i < mOrder; ++i) {
                v += mCoeffs[i + 1] * mDelay[i];
            }

            if (v > Int32.MaxValue) {
                v = Int32.MaxValue;
            }
            if (v < Int32.MinValue) {
                v = Int32.MinValue;
            }

            int sampleY = (int)(((int)v) & mMask);

            // todo: コピーしないようにする
            for (int i=mOrder - 1; 0 < i; --i) {
                mDelay[i] = mDelay[i - 1];
            }
            mDelay[0] = sampleY - v;

            return sampleY /= 256;
        }
    }
}
