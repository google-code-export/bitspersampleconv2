using System;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;

namespace WWAudioFilter {
    /// <summary>
    /// FilterConfiguration.xaml の相互作用ロジック
    /// </summary>
    public partial class FilterConfiguration : Window {
        private TextChangedEventHandler mTextBoxGainInDbChangedEH;
        private TextChangedEventHandler mTextBoxGainInAmplitudeChangedEH;

        private FilterBase mFilter = null;

        private bool mInitialized = false;

        public FilterConfiguration(FilterBase filter) {
            InitializeComponent();

            if (filter != null) {
                mFilter = filter;
            }

            SetLocalizedTextToUI();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mTextBoxGainInDbChangedEH = new TextChangedEventHandler(textBoxGainInDB_TextChanged);
            mTextBoxGainInAmplitudeChangedEH = new TextChangedEventHandler(textBoxGainInAmplitude_TextChanged);

            if (mFilter != null) {
                // filterの設定をUIに反映する
                InitializeUIbyFilter(mFilter);
            }

            mInitialized = true;
        }

        private void SetLocalizedTextToUI() {
            groupBoxGain.Header = Properties.Resources.GroupGain;
            labelGainInAmplitude.Content = Properties.Resources.LabelGainInAmplitude;
            labelGainInDB.Content = Properties.Resources.LabelGainInDb;
            labelGainAmplitudeUnit.Content = Properties.Resources.LabelX;
            buttonUseGain.Content = Properties.Resources.ButtonUseThisFilter;

            groupBoxLPF.Header = Properties.Resources.GroupLPF;
            labelLpfCutoff.Content = Properties.Resources.LabelCutoffFreq;
            labelLpfSlope.Content = Properties.Resources.LabelGainRolloffSlopes;
            labelLpfLen.Content = Properties.Resources.LabelFilterLength;
            labelLpfLenUnit.Content = Properties.Resources.LabelSamples;
            buttonUseLpf.Content = Properties.Resources.ButtonUseThisFilter;

            groupBoxUpsampler.Header = Properties.Resources.GroupUpsampler;
            labelUpsamplerType.Content = Properties.Resources.LabelUpsamplerType;
            cbItemFftUpsampler.Content = Properties.Resources.CbItemFftUpsampler;
            cbItemZohUpsampler.Content = Properties.Resources.CbItemZohUpsampler;
            labelUpsampleFactor.Content = Properties.Resources.LabelUpsamplingFactor;
            buttonUseUpsampler.Content = Properties.Resources.ButtonUseThisFilter;
            labelUpsampleLen.Content = Properties.Resources.LabelUpsamplerLength;
            labelUpsampleLenUnit.Content = Properties.Resources.LabelSamples;

            groupBoxNoiseShaping.Header = Properties.Resources.GroupNoiseShaping;
            labelNoiseShapingTargetBit.Content = Properties.Resources.LabelNoiseShapingTargetBit;
            buttonUseNoiseShaping.Content = Properties.Resources.ButtonUseThisFilter;
        }

        public FilterBase Filter {
            get { return mFilter; }
        }

        private void InitializeUIbyFilter(FilterBase filter) {
            switch (filter.FilterType) {
            case FilterType.Gain:
                textBoxGainInDB.TextChanged        -= mTextBoxGainInDbChangedEH;
                textBoxGainInAmplitude.TextChanged -= mTextBoxGainInAmplitudeChangedEH;

                var gain = filter as GainFilter;
                textBoxGainInDB.Text = string.Format(CultureInfo.CurrentCulture, "{0}", 20.0 * Math.Log10(gain.Amplitude));
                textBoxGainInAmplitude.Text = string.Format(CultureInfo.CurrentCulture, "{0}", gain.Amplitude);

                textBoxGainInDB.TextChanged        += mTextBoxGainInDbChangedEH;
                textBoxGainInAmplitude.TextChanged += mTextBoxGainInAmplitudeChangedEH;
                break;
            case FilterType.ZohUpsampler:
                var zoh = filter as ZeroOrderHoldUpsampler;
                comboBoxUpsamplingFactor.SelectedIndex = (int)UpsamplingFactorToUpsamplingFactorType(zoh.Factor);
                comboBoxUpsamplerType.SelectedIndex = (int)UpsamplerType.ZOH;
                break;
            case FilterType.FftUpsampler:
                var fftu = filter as FftUpsampler;
                comboBoxUpsamplingFactor.SelectedIndex = (int)UpsamplingFactorToUpsamplingFactorType(fftu.Factor);
                comboBoxUpsamplerType.SelectedIndex = (int)UpsamplerType.FFT;
                comboBoxUpsampleLen.SelectedIndex = (int)UpsampleLenToUpsampleLenType(fftu.FftLength);
                break;
            case FilterType.LowPassFilter:
                var lpf = filter as LowpassFilter;
                textBoxLpfCutoff.Text = string.Format(CultureInfo.CurrentCulture, "{0}", lpf.CutoffFrequency);
                comboBoxLpfLen.SelectedIndex = (int)LpfLenToLpfLenType(lpf.FilterLength);
                textBoxLpfSlope.Text = string.Format(CultureInfo.CurrentCulture, "{0}", lpf.FilterSlopeDbOct);
                break;
            case FilterType.Mash2:
                var mash = filter as MashFilter;
                textBoxNoiseShapingTargetBit.Text = string.Format(CultureInfo.CurrentCulture, "{0}", mash.TargetBitsPerSample);
                break;
            }
        }

        void textBoxGainInDB_TextChanged(object sender, TextChangedEventArgs e) {
            double v;
            if (!Double.TryParse(textBoxGainInDB.Text, out v)) {
                return;
            }

            textBoxGainInAmplitude.TextChanged -= mTextBoxGainInAmplitudeChangedEH;
            textBoxGainInAmplitude.Text = string.Format(CultureInfo.CurrentCulture, "{0}", Math.Pow(10.0, v / 20.0));
            textBoxGainInAmplitude.TextChanged += mTextBoxGainInAmplitudeChangedEH;
        }

        void textBoxGainInAmplitude_TextChanged(object sender, TextChangedEventArgs e) {
            double v;
            if (!Double.TryParse(textBoxGainInAmplitude.Text, out v)) {
                return;
            }
            if (v <= Double.Epsilon) {
                return;
            }

            textBoxGainInDB.TextChanged -= mTextBoxGainInAmplitudeChangedEH;
            textBoxGainInDB.Text = string.Format(CultureInfo.CurrentCulture, "{0}", 20.0 * Math.Log10(v));
            textBoxGainInDB.TextChanged += mTextBoxGainInAmplitudeChangedEH;
        }

        enum UpsamplingFactorType {
            x2,
            x4,
            x8,
            x16,
        };

        private static UpsamplingFactorType UpsamplingFactorToUpsamplingFactorType(int factor) {
            switch (factor) {
            case 2:
                return UpsamplingFactorType.x2;
            case 4:
                return UpsamplingFactorType.x4;
            case 8:
                return UpsamplingFactorType.x8;
            case 16:
                return UpsamplingFactorType.x16;
            default:
                System.Diagnostics.Debug.Assert(false);
                return UpsamplingFactorType.x2;
            }
        }

        private static int UpsamplingFactorTypeToUpsampingfactor(int t) {
            switch (t) {
            case (int)UpsamplingFactorType.x2:
                return 2;
            case (int)UpsamplingFactorType.x4:
                return 4;
            case (int)UpsamplingFactorType.x8:
                return 8;
            case (int)UpsamplingFactorType.x16:
                return 16;
            default:
                System.Diagnostics.Debug.Assert(false);
                return 2;
            }
        }

        enum LpfLenType {
            L255,
            L1023,
            L4095,
            L16383,
            L65535,
        };

        private static LpfLenType LpfLenToLpfLenType(int lpfLen) {
            switch (lpfLen) {
            case 255:
                return LpfLenType.L255;
            case 1023:
                return LpfLenType.L1023;
            case 4095:
                return LpfLenType.L4095;
            case 16383:
                return LpfLenType.L16383;
            case 65535:
            default:
                return LpfLenType.L65535;
            }
        }

        private static int LpfLenTypeToLpfLen(int t) {
            switch (t) {
            case (int)LpfLenType.L255:
                return 255;
            case (int)LpfLenType.L1023:
                return 1023;
            case (int)LpfLenType.L4095:
                return 4095;
            case (int)LpfLenType.L16383:
                return 16383;
            case (int)LpfLenType.L65535:
            default:
                return 65535;
            }
        }

        enum UpsamplerType {
            FFT,
            ZOH
        };

        enum UpsampleLenType {
            L1024,
            L4096,
            L16384,
            L65536,
            L262144,
        };

        private static UpsampleLenType UpsampleLenToUpsampleLenType(int len) {
            switch (len) {
            case 1024:
                return UpsampleLenType.L1024;
            case 4096:
                return UpsampleLenType.L4096;
            case 16384:
                return UpsampleLenType.L16384;
            case 65536:
                return UpsampleLenType.L65536;
            case 262144:
                return UpsampleLenType.L262144;
            default:
                return UpsampleLenType.L262144;
            }
        }

        private static int UpsampleLenTypeToLpfLen(int t) {
            switch (t) {
            case (int)UpsampleLenType.L1024:
                return 1024;
            case (int)UpsampleLenType.L4096:
                return 4096;
            case (int)UpsampleLenType.L16384:
                return 16384;
            case (int)UpsampleLenType.L65536:
                return 65536;
            case (int)UpsampleLenType.L262144:
                return 262144;
            default:
                return 262144;
            }
        }
        ///////////////////////////////////////////////////////////////////////////////////

        private void buttonCancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void buttonUseGain_Click(object sender, RoutedEventArgs e) {
            double v;
            if (!Double.TryParse(textBoxGainInAmplitude.Text, out v)) {
                MessageBox.Show(Properties.Resources.ErrorGainValueIsNan);
                return;
            }
            if (v <= Double.Epsilon) {
                MessageBox.Show(Properties.Resources.ErrorGainValueIsTooSmall);
                return;
            }

            mFilter = new GainFilter(v);

            DialogResult = true;
            Close();
        }

        private void buttonUseUpsampler_Click(object sender, RoutedEventArgs e) {
            int factor = UpsamplingFactorTypeToUpsampingfactor(comboBoxUpsamplingFactor.SelectedIndex);
            int len = UpsampleLenTypeToLpfLen(comboBoxUpsampleLen.SelectedIndex);

            switch (comboBoxUpsamplerType.SelectedIndex) {
            case (int)UpsamplerType.ZOH:
                mFilter = new ZeroOrderHoldUpsampler(factor);
                break;
            case (int)UpsamplerType.FFT:
                mFilter = new FftUpsampler(factor, len);
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                mFilter = new FftUpsampler(factor, len);
                break;
            }

            DialogResult = true;
            Close();
        }

        private void buttonUseLpf_Click(object sender, RoutedEventArgs e) {
            double v;
            if (!Double.TryParse(textBoxLpfCutoff.Text, out v)) {
                MessageBox.Show(Properties.Resources.ErrorLpfCutoffFreqIsNan);
                return;
            }
            if (v <= 0.0) {
                MessageBox.Show(Properties.Resources.ErrorLpfCutoffFreqIsNegative);
                return;
            }

            int slope;
            if (!Int32.TryParse(textBoxLpfSlope.Text, out slope)) {
                MessageBox.Show(Properties.Resources.ErrorLpfSlopeIsNan);
                return;
            }

            if (slope <= 0) {
                MessageBox.Show(Properties.Resources.ErrorLpfSlopeIsTooSmall);
                return;
            }

            int filterLength = LpfLenTypeToLpfLen(comboBoxLpfLen.SelectedIndex);

            mFilter = new LowpassFilter(v, filterLength, slope);
            DialogResult = true;
            Close();
        }

        private void buttonUseNoiseShaping_Click(object sender, RoutedEventArgs e) {
            int nBit;
            if (!Int32.TryParse(textBoxNoiseShapingTargetBit.Text, out nBit)) {
                MessageBox.Show(Properties.Resources.ErrorNoiseShapingBitIsNan);
                return;
            }
            if (nBit < 1 || 23 < nBit) {
                MessageBox.Show(Properties.Resources.ErrorNoiseShapingBitIsOutOfRange);
                return;
            }

            mFilter = new NoiseShapingFilter(nBit, 2);
            DialogResult = true;
            Close();
        }

        private void comboBoxUpsamplerType_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!mInitialized) {
                return;
            }

            if (comboBoxUpsamplerType.SelectedIndex == (int)UpsamplerType.FFT) {
                comboBoxUpsampleLen.IsEnabled = true;
            } else {
                comboBoxUpsampleLen.IsEnabled = false;
            }
        }

    }
}
