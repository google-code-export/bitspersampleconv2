using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WWAudioFilter {
    /// <summary>
    /// FilterConfiguration.xaml の相互作用ロジック
    /// </summary>
    public partial class FilterConfiguration : Window {
        private TextChangedEventHandler mTextBoxGainInDbChangedEH;
        private TextChangedEventHandler mTextBoxGainInAmplitudeChangedEH;

        private FilterBase mFilter = new GainFilter(1.0);

        public FilterConfiguration(FilterBase filter) {
            InitializeComponent();

            if (filter != null) {
                mFilter = filter;
            }

            mTextBoxGainInDbChangedEH        = new TextChangedEventHandler(textBoxGainInDB_TextChanged);
            mTextBoxGainInAmplitudeChangedEH = new TextChangedEventHandler(textBoxGainInAmplitude_TextChanged);

            if (mFilter != null) {
                // filterの設定をUIに反映する
                InitializeUIbyFilter(mFilter);
            }

            SetLocalizedTextToUI();
        }

        private void SetLocalizedTextToUI() {
            groupBoxGain.Header = Properties.Resources.GroupGain;
            labelGainInAmplitude.Content = Properties.Resources.LabelGainInAmplitude;
            labelGainInDB.Content = Properties.Resources.LabelGainInDb;
            labelGainAmplitudeUnit.Content = Properties.Resources.LabelX;
            buttonUseGain.Content = Properties.Resources.ButtonUseThisFilter;

            groupBoxUpsampler.Header = Properties.Resources.GroupUpsampler;
            labelUpsampleFactor.Content = Properties.Resources.LabelUpsamplingFactor;
            buttonUseUpsampler.Content = Properties.Resources.ButtonUseThisFilter;

            groupBoxLPF.Header = Properties.Resources.GroupLPF;
            labelLpfCutoff.Content = Properties.Resources.LabelCutoffFreq;
            labelLpfSlope.Content = Properties.Resources.LabelGainRolloffSlopes;
            labelLpfLen.Content = Properties.Resources.LabelFilterLength;
            labelLpfLenUnit.Content = Properties.Resources.LabelSamples;
            buttonUseLpf.Content = Properties.Resources.ButtonUseThisFilter;
        }

        public FilterBase GetFilter() {
            return mFilter;
        }

        private void InitializeUIbyFilter(FilterBase filter) {
            switch (filter.FilterType) {
            case FilterType.Gain:
                textBoxGainInDB.TextChanged        -= mTextBoxGainInDbChangedEH;
                textBoxGainInAmplitude.TextChanged -= mTextBoxGainInAmplitudeChangedEH;

                var gain = filter as GainFilter;
                textBoxGainInDB.Text = string.Format("{0}", 20.0 * Math.Log10(gain.Amplitude));
                textBoxGainInAmplitude.Text = string.Format("{0}", gain.Amplitude);

                textBoxGainInDB.TextChanged        += mTextBoxGainInDbChangedEH;
                textBoxGainInAmplitude.TextChanged += mTextBoxGainInAmplitudeChangedEH;
                break;
            case FilterType.ZOH:
                var zoh = filter as ZeroOrderHoldUpsampler;
                comboBoxUpsamplingFactor.SelectedIndex = (int)UpsamplingFactorToUpsamplingFactorType(zoh.Factor);
                break;
            case FilterType.LPF:
                var lpf = filter as LowpassFilter;
                textBoxLpfCutoff.Text = string.Format("{0}", lpf.CutoffFrequency);
                comboBoxLpfLen.SelectedIndex = (int)LpfLenToLpfLenType(lpf.FilterLength);
                textBoxLpfSlope.Text = string.Format("{0}", lpf.FilterSlopeDbOct);
                break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
        }

        void textBoxGainInDB_TextChanged(object sender, TextChangedEventArgs e) {
            double v;
            if (!Double.TryParse(textBoxGainInDB.Text, out v)) {
                return;
            }

            textBoxGainInAmplitude.TextChanged -= mTextBoxGainInAmplitudeChangedEH;
            textBoxGainInAmplitude.Text = string.Format("{0}", Math.Pow(10.0, v/20.0));
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
            textBoxGainInDB.Text = string.Format("{0}", 20.0 * Math.Log10(v));
            textBoxGainInDB.TextChanged += mTextBoxGainInAmplitudeChangedEH;
        }

        enum UpsamplingFactorType {
            x2,
            x4,
            x8,
            x16,
        };

        private UpsamplingFactorType UpsamplingFactorToUpsamplingFactorType(int factor) {
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

        private int UpsamplingFactorTypeToUpsampingfactor(int t) {
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

        private LpfLenType LpfLenToLpfLenType(int lpfLen) {
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

        private int LpfLenTypeToLpfLen(int t) {
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

        ///////////////////////////////////////////////////////////////////////////////////

        private void buttonCancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void buttonUseGain_Click(object sender, RoutedEventArgs e) {
            double v;
            if (!Double.TryParse(textBoxGainInAmplitude.Text, out v)) {
                MessageBox.Show("Please input gain value in number");
                return;
            }
            if (v <= Double.Epsilon) {
                MessageBox.Show("Please input gain value larger than 0.0");
                return;
            }

            mFilter = new GainFilter(v);

            DialogResult = true;
            Close();
        }

        private void buttonUseZOH_Click(object sender, RoutedEventArgs e) {
            int factor = UpsamplingFactorTypeToUpsampingfactor(comboBoxUpsamplingFactor.SelectedIndex);

            mFilter = new ZeroOrderHoldUpsampler(factor);

            DialogResult = true;
            Close();
        }

        private void buttonUseLpf_Click(object sender, RoutedEventArgs e) {
            double v;
            if (!Double.TryParse(textBoxLpfCutoff.Text, out v)) {
                MessageBox.Show("Please input Lowpass filter cutoff frequency in number");
                return;
            }
            if (v <= 0.0) {
                MessageBox.Show("Please input Lowpass filter cutoff frequency larger than 0.0");
                return;
            }

            int slope;
            if (!Int32.TryParse(textBoxLpfSlope.Text, out slope)) {
                MessageBox.Show("Please input Lowpass filter slope in number");
                return;
            }

            if (slope <= 0) {
                MessageBox.Show("Please input Lowpass filter slope larger than 1");
                return;
            }

            int filterLength = LpfLenTypeToLpfLen(comboBoxLpfLen.SelectedIndex);

            mFilter = new LowpassFilter(v, filterLength, slope);
            DialogResult = true;
            Close();
        }

    }
}
