using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WWAudioFilter;

namespace PolynomialVisualize {
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        enum PoleZeroType {
            Zero,
            Other,
            Pole
        };

        private void Update()
        {
            canvas1.Children.Clear();

            var im = new Image();

            var bm = new WriteableBitmap(
                512,
                512,
                96, 
                96, 
                PixelFormats.Rgba128Float, 
                null);
            im.Source = bm;
            im.Stretch = Stretch.None;
            im.HorizontalAlignment = HorizontalAlignment.Left;
            im.VerticalAlignment   = VerticalAlignment.Top;
            
            var px = new float[bm.PixelHeight*bm.PixelWidth * 4];

            int pos = 0;
            for (int yI = 0; yI < bm.PixelHeight; yI++) {
                for (int xI = 0; xI < bm.PixelWidth; xI++) {
                    double y = 2.666666666 * (bm.PixelHeight / 2 - yI) / bm.PixelHeight;
                    double x = 2.666666666 * (xI - bm.PixelWidth / 2) / bm.PixelHeight;
                    var z = new WWComplex(x, y);

#if true
                    var zRecip = new WWComplex(z).Reciprocal();

                    var zRecip2 = new WWComplex(zRecip).Mul(zRecip);
                    var zRecip3 = new WWComplex(zRecip2).Mul(zRecip);
                    var zRecip4 = new WWComplex(zRecip3).Mul(zRecip);

                    var hDenom0 = new WWComplex(1.0, 0.0);
                    var hDenom1 = new WWComplex(Double.Parse(textBoxD1.Text), 0.0).Mul(zRecip);
                    var hDenom2 = new WWComplex(Double.Parse(textBoxD2.Text), 0.0).Mul(zRecip2);
                    var hDenom3 = new WWComplex(Double.Parse(textBoxD3.Text), 0.0).Mul(zRecip3);
                    var hDenom4 = new WWComplex(Double.Parse(textBoxD4.Text), 0.0).Mul(zRecip4);
                    var hDenom = new WWComplex(hDenom0).Add(hDenom1).Add(hDenom2).Add(hDenom3).Add(hDenom4).Reciprocal();

                    var hNumer0 = new WWComplex(Double.Parse(textBoxN0.Text), 0.0);
                    var hNumer1 = new WWComplex(Double.Parse(textBoxN1.Text), 0.0).Mul(zRecip);
                    var hNumer2 = new WWComplex(Double.Parse(textBoxN2.Text), 0.0).Mul(zRecip2);
                    var hNumer3 = new WWComplex(Double.Parse(textBoxN3.Text), 0.0).Mul(zRecip3);
                    var hNumer4 = new WWComplex(Double.Parse(textBoxN4.Text), 0.0).Mul(zRecip4);
                    var hNumer = new WWComplex(hNumer0).Add(hNumer1).Add(hNumer2).Add(hNumer3).Add(hNumer4);
#endif
                    var h = new WWComplex(hNumer).Mul(hDenom);
                    var hM = h.Magnitude();
                    


                    var poleZero = PoleZeroType.Other;

                    if (hM < 0.1) {
                        hM = 0.1;
                        poleZero = PoleZeroType.Zero;
                    }
                    var hL = (Math.Log10(hM) + 1.0)/5.0;

                    switch (poleZero) {
                    case PoleZeroType.Other:
                        px[pos + 0] = (float)hL;
                        px[pos + 1] = (float)hL;
                        px[pos + 2] = (float)hL;
                        px[pos + 3] = 1.0f;
                        break;
                    case PoleZeroType.Pole:
                        px[pos + 0] = 1.0f;
                        px[pos + 1] = 1.0f;
                        px[pos + 2] = 1.0f;
                        px[pos + 3] = 1.0f;
                        break;
                    case PoleZeroType.Zero:
                        px[pos + 0] = 0.0f;
                        px[pos + 1] = 0.0f;
                        px[pos + 2] = 0.0f;
                        px[pos + 3] = 1.0f;
                        break;
                    }

                    pos += 4;
                }
            }

            bm.WritePixels(new Int32Rect(0, 0, bm.PixelWidth, bm.PixelHeight), px, bm.BackBufferStride, 0);

            canvas1.Children.Add(im);
        }

        private void buttonUpdate_Click(object sender, RoutedEventArgs e) {
            Update();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            Update();
        }
    }
}
