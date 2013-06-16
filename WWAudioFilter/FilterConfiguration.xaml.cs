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

    }
}
