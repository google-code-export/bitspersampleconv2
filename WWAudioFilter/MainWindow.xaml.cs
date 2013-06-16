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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.IO;

namespace WWAudioFilter {
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        private bool mInitialized = false;

        private enum State {
            NotReady,
            Ready,
            Converting,
        }

        private State mState = State.NotReady;

        public List<FilterBase> mFilters = new List<FilterBase>();

        private const int FILTER_FILE_VERSION = 1;

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mInitialized = true;
            Update();
        }

        private void Update() {
            if (!mInitialized) {
                return;
            }

            UpdateFilterSettings();

            switch (mState) {
            case State.NotReady:
                buttonStartConversion.IsEnabled = false;
                break;
            case State.Ready:
                buttonStartConversion.IsEnabled = true;
                break;
            case State.Converting:
                buttonStartConversion.IsEnabled = false;
                break;
            }
        }

        private void UpdateFilterButtons() {
            switch (mState) {
            case State.NotReady:
            case State.Ready:
                groupBoxFilterSettings.IsEnabled = true;
                if (listBoxFilters.SelectedIndex < 0) {
                    buttonFilterAdd.IsEnabled = true;
                    buttonFilterDelete.IsEnabled = false;
                    buttonFilterEdit.IsEnabled = false;
                    buttonFilterLoad.IsEnabled = true;
                    buttonFilterSaveAs.IsEnabled = false;

                    buttonFilterDown.IsEnabled = false;
                    buttonFilterUp.IsEnabled = false;
                } else {
                    buttonFilterAdd.IsEnabled = true;
                    buttonFilterDelete.IsEnabled = true;
                    buttonFilterEdit.IsEnabled = true;
                    buttonFilterLoad.IsEnabled = true;
                    buttonFilterSaveAs.IsEnabled = true;

                    buttonFilterDown.IsEnabled = listBoxFilters.SelectedIndex != listBoxFilters.Items.Count - 1;
                    buttonFilterUp.IsEnabled = listBoxFilters.SelectedIndex != 0;
                }
                break;
            case State.Converting:
                groupBoxFilterSettings.IsEnabled = false;
                break;
            }
        }

        private void UpdateFilterSettings() {
            int selectedIdx = listBoxFilters.SelectedIndex;

            listBoxFilters.Items.Clear();
            foreach (var f in mFilters) {
                listBoxFilters.Items.Add(f.ToDescriptionText());
            }

            if (listBoxFilters.Items.Count == 1) {
                // 最初に項目が追加された
                selectedIdx = 0;
            }
            if (0 <= selectedIdx && listBoxFilters.Items.Count <= selectedIdx) {
                // 選択されていた最後の項目が削除された。
                selectedIdx = listBoxFilters.Items.Count - 1;
            }
            listBoxFilters.SelectedIndex = selectedIdx;

            UpdateFilterButtons();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////

        private void buttonFilterAdd_Click(object sender, RoutedEventArgs e) {
            var w = new FilterConfiguration(null);
            w.ShowDialog();

            if (true == w.DialogResult) {
                var f = w.GetFilter();
                mFilters.Add(f);
                Update();
                listBoxFilters.SelectedIndex = listBoxFilters.Items.Count - 1;
            }
        }

        private void buttonFilterEdit_Click(object sender, RoutedEventArgs e) {
            System.Diagnostics.Debug.Assert(0 <= listBoxFilters.SelectedIndex);
            System.Diagnostics.Debug.Assert(listBoxFilters.SelectedIndex < mFilters.Count);

            var w = new FilterConfiguration(mFilters[listBoxFilters.SelectedIndex]);
            w.ShowDialog();

            if (true == w.DialogResult) {
                var f = w.GetFilter();
                mFilters.Add(f);
                Update();
            }
        }

        private void buttonFilterUp_Click(object sender, RoutedEventArgs e) {
            int pos = listBoxFilters.SelectedIndex;
            var tmp = mFilters[pos];
            mFilters.RemoveAt(pos);
            mFilters.Insert(pos - 1, tmp);

            --listBoxFilters.SelectedIndex;

            Update();
        }

        private void buttonFilterDown_Click(object sender, RoutedEventArgs e) {
            int pos = listBoxFilters.SelectedIndex;
            var tmp = mFilters[pos];
            mFilters.RemoveAt(pos);
            mFilters.Insert(pos + 1, tmp);

            ++listBoxFilters.SelectedIndex;

            Update();
        }

        private void buttonFilterDelete_Click(object sender, RoutedEventArgs e) {
            mFilters.RemoveAt(listBoxFilters.SelectedIndex);

            Update();
        }

        private void listBoxFilters_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UpdateFilterButtons();
        }

        private void buttonFilterSaveAs_Click(object sender, RoutedEventArgs e) {
            if (mFilters.Count() == 0) {
                MessageBox.Show(Properties.Resources.NothingToStore);
                return;
            }

            System.Diagnostics.Debug.Assert(0 < mFilters.Count());

            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = Properties.Resources.FilterWWAFilterFiles;
            dlg.ValidateNames = true;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            // 保存する
            try {
                using (StreamWriter w = new StreamWriter(dlg.FileName)) {
                    w.WriteLine("{0} {1}", FILTER_FILE_VERSION, mFilters.Count());
                    foreach (var f in mFilters) {
                        w.WriteLine("{0} {1}", f.FilterType, f.ToSaveText());
                    }
                }
            } catch (IOException ex) {
                MessageBox.Show("{0}", ex.Message);
            } catch (UnauthorizedAccessException ex) {
                MessageBox.Show("{0}", ex.Message);
            }
        }

        private void buttonFilterLoad_Click(object sender, RoutedEventArgs e) {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = Properties.Resources.FilterWWAFilterFiles;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            // 読み込む
            try {
                var filters = new List<FilterBase>();

                using (StreamReader r = new StreamReader(dlg.FileName)) {
                    int filterNum = 0;

                    {
                        // ヘッダ部分。バージョン番号とフィルタの個数が入っている。
                        var s = r.ReadLine();
                        s = s.Trim();
                        var tokens = s.Split(null);
                        if (tokens.Length != 2) {
                            MessageBox.Show("Read failed: " + dlg.FileName);
                            return;
                        }
                        int version;
                        if (!Int32.TryParse(tokens[0], out version) || version != FILTER_FILE_VERSION) {
                            MessageBox.Show(
                                string.Format("Filter file version mismatch. expected version={0}, file version={1}",
                                    FILTER_FILE_VERSION, tokens[0]));
                            return;
                        }

                        if (!Int32.TryParse(tokens[1], out filterNum) || filterNum < 0) {
                            MessageBox.Show(
                                string.Format("Read failed. bad filter count {0}",
                                    tokens[1]));
                            return;
                        }
                    }

                    for (int i=0; i < filterNum; ++i) {
                        var s = r.ReadLine();
                        s = s.Trim();
                        var f = FilterFactory.Create(s);
                        if (null == f) {
                            MessageBox.Show(
                                string.Format("Read failed. line={0}, {1}",
                                    i+2, s));
                        }
                        filters.Add(f);
                    }
                }

                mFilters = filters;
            } catch (IOException ex) {
                MessageBox.Show("{0}", ex.Message);
            } catch (UnauthorizedAccessException ex) {
                MessageBox.Show("{0}", ex.Message);
            }

            Update();
        }
    }
}
