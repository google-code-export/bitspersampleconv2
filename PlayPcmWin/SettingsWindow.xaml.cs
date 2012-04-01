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
using WasapiPcmUtil;

namespace PlayPcmWin {
    /// <summary>
    /// SettingsWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingsWindow : Window {
        public SettingsWindow() {
            InitializeComponent();
        }

        Preference m_preference = null;
        public void SetPreference(Preference preference) {
            m_preference = preference;
        }

        private void UpdateUIFromPreference(Preference preference) {
            switch (preference.bitsPerSampleFixType) {
            case BitsPerSampleFixType.Variable:
                radioButtonBpsVariable.IsChecked = true;
                break;
            case BitsPerSampleFixType.VariableSint16Sint24:
                radioButtonBpsVariableSint16Sint24.IsChecked = true;
                break;
            case BitsPerSampleFixType.VariableSint16Sint32V24:
                radioButtonBpsVariableSint16Sint32V24.IsChecked = true;
                break;
            case BitsPerSampleFixType.Sint16:
                radioButtonBpsSint16.IsChecked = true;
                break;
            case BitsPerSampleFixType.Sint24:
                radioButtonBpsSint24.IsChecked = true;
                break;
            case BitsPerSampleFixType.Sint32:
                radioButtonBpsSint32.IsChecked = true;
                break;
            case BitsPerSampleFixType.Sfloat32:
                radioButtonBpsSfloat32.IsChecked = true;
                break;
            case BitsPerSampleFixType.Sint32V24:
                radioButtonSint32V24.IsChecked = true;
                break;
            case BitsPerSampleFixType.AutoSelect:
                radioButtonBpsAutoSelect.IsChecked = true;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            checkBoxPlaceKokomadeAfterIndex00.IsChecked =
                preference.ReplaceGapWithKokomade;

            checkBoxManuallySetMainWindowDimension.IsChecked =
                preference.ManuallySetMainWindowDimension;

            checkBoxStorePlaylistContent.IsChecked =
                preference.StorePlaylistContent;

            checkBoxCoverart.IsChecked =
                preference.DispCoverart;

            checkBoxRefrainRedraw.IsChecked =
                preference.RefrainRedraw;

            checkBoxParallelRead.IsChecked =
                preference.ParallelRead;

            checkBoxTimePeriod1.IsChecked =
                preference.TimePeriodMillisec == 0 ? false : true;

            textBoxPlayingTimeSize.Text =
                preference.PlayingTimeSize.ToString();

            textBoxZeroFlushSeconds.Text =
                string.Format("{0}", preference.ZeroFlushMillisec * 0.001);

            sliderWindowScaling.Value =
                preference.WindowScale;

            checkBoxPlayingTimeBold.IsChecked =
                preference.PlayingTimeFontBold;

            var fontFamilies = new Dictionary<string, FontFamily>();

            foreach (FontFamily fontFamily in Fonts.SystemFontFamilies) {
                if (!fontFamilies.ContainsKey(fontFamily.ToString())) {
                    fontFamilies.Add(fontFamily.ToString(), fontFamily);
                }
            }

            foreach (var kvp in fontFamilies) {
                var item = new ComboBoxItem();
                item.Content = kvp.Value;
                //item.FontFamily = fontFamily;
                comboBoxPlayingTimeFontNames.Items.Add(item);
                if (kvp.Key.Equals(preference.PlayingTimeFontName)) {
                    comboBoxPlayingTimeFontNames.SelectedItem = item;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            System.Diagnostics.Debug.Assert(null != m_preference);

            UpdateUIFromPreference(m_preference);
        }

        private void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            if (true == radioButtonBpsVariable.IsChecked)
            {
                m_preference.bitsPerSampleFixType = BitsPerSampleFixType.Variable;
            }
            if (true == radioButtonBpsVariableSint16Sint24.IsChecked)
            {
                m_preference.bitsPerSampleFixType = BitsPerSampleFixType.VariableSint16Sint24;
            }
            if (true == radioButtonBpsVariableSint16Sint32V24.IsChecked)
            {
                m_preference.bitsPerSampleFixType = BitsPerSampleFixType.VariableSint16Sint32V24;
            }
            if (true == radioButtonBpsSint16.IsChecked)
            {
                m_preference.bitsPerSampleFixType = BitsPerSampleFixType.Sint16;
            }
            if (true == radioButtonBpsSint24.IsChecked)
            {
                m_preference.bitsPerSampleFixType = BitsPerSampleFixType.Sint24;
            }
            if (true == radioButtonBpsSint32.IsChecked)
            {
                m_preference.bitsPerSampleFixType = BitsPerSampleFixType.Sint32;
            }
            if (true == radioButtonBpsSfloat32.IsChecked)
            {
                m_preference.bitsPerSampleFixType = BitsPerSampleFixType.Sfloat32;
            }
            if (true == radioButtonSint32V24.IsChecked)
            {
                m_preference.bitsPerSampleFixType = BitsPerSampleFixType.Sint32V24;
            }
            if (true == radioButtonBpsAutoSelect.IsChecked)
            {
                m_preference.bitsPerSampleFixType = BitsPerSampleFixType.AutoSelect;
            }

            m_preference.ReplaceGapWithKokomade
                = checkBoxPlaceKokomadeAfterIndex00.IsChecked == true;

            m_preference.ManuallySetMainWindowDimension
                = checkBoxManuallySetMainWindowDimension.IsChecked == true;

            m_preference.StorePlaylistContent
                = checkBoxStorePlaylistContent.IsChecked == true;

            m_preference.DispCoverart
                = checkBoxCoverart.IsChecked == true;

            m_preference.RefrainRedraw
                = checkBoxRefrainRedraw.IsChecked == true;

            m_preference.ParallelRead
                = checkBoxParallelRead.IsChecked == true;

            m_preference.TimePeriodMillisec
                = checkBoxTimePeriod1.IsChecked == true ? 1 : 0;

            m_preference.WindowScale = sliderWindowScaling.Value;

            {
                int playingTimeSize;
                if (Int32.TryParse(textBoxPlayingTimeSize.Text, out playingTimeSize)) {
                    if (playingTimeSize <= 0 || 100 < playingTimeSize) {
                        MessageBox.Show("再生時間表示文字の大きさは 1～100の範囲の数字を入力してください。");
                        return;
                    }
                    m_preference.PlayingTimeSize = playingTimeSize;
                } else {
                    MessageBox.Show("再生時間表示文字の大きさは 1～100の範囲の数字を入力してください。");
                }
            }
            {
                double zeroFlushSeconds;
                if (Double.TryParse(textBoxZeroFlushSeconds.Text, out zeroFlushSeconds)) {
                    if (zeroFlushSeconds <= 0 || 1000 < zeroFlushSeconds) {
                        MessageBox.Show("再生前無音送信時間の大きさは 0.0～1000.0の範囲の数字を入力してください。");
                        return;
                    }
                    m_preference.ZeroFlushMillisec = (int)(zeroFlushSeconds * 1000);
                } else {
                    MessageBox.Show("再生前無音送信時間の大きさは 0.0～1000.0の範囲の数字を入力してください。");
                }
            }

            m_preference.PlayingTimeFontBold = (checkBoxPlayingTimeBold.IsChecked == true);

            if (null != comboBoxPlayingTimeFontNames.SelectedItem)
            {
                ComboBoxItem item = (ComboBoxItem)comboBoxPlayingTimeFontNames.SelectedItem;
                FontFamily ff = (FontFamily)item.Content;
                m_preference.PlayingTimeFontName = ff.ToString();
            }

            Close();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void buttonScale1X_Click(object sender, RoutedEventArgs e) {
            sliderWindowScaling.Value = 1.0;
        }

        private void sliderWindowScaling_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (null != labelWindowScaling) {
                labelWindowScaling.Content = string.Format("{0:0.00}", e.NewValue);
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                Keyboard.IsKeyDown(Key.RightCtrl)) {
                // CTRL + マウスホイールで画面のスケーリング

                double scaling = sliderWindowScaling.Value;
                if (e.Delta < 0) {
                    // 1.25の128乗根
                    scaling /= 1.001744829441175331741294013303;
                } else {
                    scaling *= 1.001744829441175331741294013303;
                }
                sliderWindowScaling.Value = scaling;
            }
        }

        private void comboBoxPlayingTimeFontNames_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (null == labelPlayingTime) {
                return;
            }

            ComboBox cb = sender as ComboBox;
            if (null == cb) {
                return;
            }
            ComboBoxItem item = cb.SelectedItem as ComboBoxItem;
            if (null == item) {
                return;
            }

            labelPlayingTime.FontFamily = (FontFamily)item.Content;
        }

        private void checkBoxPlayingTimeBold_Checked(object sender, RoutedEventArgs e) {
            if (null != labelPlayingTime) {
                labelPlayingTime.FontWeight = FontWeights.Bold;
            }
        }

        private void checkBoxPlayingTimeBold_Unchecked(object sender, RoutedEventArgs e) {
            if (null != labelPlayingTime) {
                labelPlayingTime.FontWeight = FontWeights.Normal;
            }
        }

        private void textBoxPlayingTimeSize_TextChanged(object sender, TextChangedEventArgs e) {
            if (null == labelPlayingTime) {
                return;
            }

            int fontSize;
            if (!Int32.TryParse(textBoxPlayingTimeSize.Text, out fontSize)) {
                return;
            }
            if (0 < fontSize && fontSize <= 100) {
                labelPlayingTime.FontSize = fontSize;
            }
        }

        private void buttonReset_Click(object sender, RoutedEventArgs e) {
            var preference = new Preference();
            UpdateUIFromPreference(preference);
        }
    }
}
