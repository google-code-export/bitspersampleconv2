using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FftTest {
    struct WWComplex {
        public double real;
        public double imaginary;

        public WWComplex(double real, double imaginary) {
            this.real      = real;
            this.imaginary = imaginary;
        }

        public WWComplex(WWComplex rhs) {
            this.real      = rhs.real;
            this.imaginary = rhs.imaginary;
        }

        public void Set(double real, double imaginary) {
            this.real = real;
            this.imaginary = imaginary;
        }

        public double Magnitude() {
            return Math.Sqrt(real * real + imaginary * imaginary);
        }

        /// <summary>
        /// Phase in radians
        /// </summary>
        /// <returns>radians, -π to +π</returns>
        public double Phase() {
            if (Magnitude() < Double.Epsilon) {
                return 0;
            }

            return Math.Atan2(imaginary, real);
        }

        public WWComplex Add(WWComplex rhs) {
            real      += rhs.real;
            imaginary += rhs.imaginary;
            return this;
        }

        public WWComplex Mul(double v) {
            real      *= v;
            imaginary *= v;
            return this;
        }

        public WWComplex Mul(WWComplex rhs) {
            double tR = real * rhs.real      - imaginary * rhs.imaginary;
            double tI = real * rhs.imaginary + imaginary * rhs.real;
            real      = tR;
            imaginary = tI;
            return this;
        }

        public void CopyFrom(WWComplex rhs) {
            real      = rhs.real;
            imaginary = rhs.imaginary;
        }
    }
}
