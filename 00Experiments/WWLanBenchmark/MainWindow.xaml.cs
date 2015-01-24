using System;
using System.ComponentModel;
using System.Windows;

namespace WWLanBenchmark {
    public partial class MainWindow : Window {
        private const int PORT = 9880;
        private bool mWindowLoaded = false;
        private BackgroundWorker mBackgroundWorker;

        public MainWindow() {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mWindowLoaded = true;
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e) {
            if (true == radioButtonClient.IsChecked) {
                StartClient();
                return;
            }

            if (true == radioButtonServer.IsChecked) {  
                StartServer();
                return;
            }
        }

        struct ClientArgs {
            public string serverIP;
            public int continuousSendGB;
            public int testIterationCount;
        };

        private void StartClient() {
            mBackgroundWorker = new BackgroundWorker();
            mBackgroundWorker.DoWork += Client_DoWork;
            mBackgroundWorker.WorkerReportsProgress = true;
            mBackgroundWorker.ProgressChanged += Client_ProgressChanged;
            mBackgroundWorker.RunWorkerCompleted += Client_RunWorkerCompleted;

            var args = new ClientArgs();
            args.serverIP = textBoxServerIP.Text;

            if (!Int32.TryParse(textBoxContinuousSendSizeGB.Text, out args.continuousSendGB)) {
                MessageBox.Show("Parse error of Contiunous send size");
                return;
            }
            if (args.continuousSendGB < 1) {
                MessageBox.Show("Contiunous send size must be integer value greater than 0");
                return;
            }

            if (!Int32.TryParse(textBoxIterationCount.Text, out args.testIterationCount)) {
                MessageBox.Show("Parse error of Test iteration count");
                return;
            }
            if (args.testIterationCount < 1) {
                MessageBox.Show("Test iteration count must be integer value greater than 0");
                return;
            }

            textBoxLog.Clear();
            buttonStart.IsEnabled = false;

            mBackgroundWorker.RunWorkerAsync(args);
        }

        private void StartServer() {
            mBackgroundWorker = new BackgroundWorker();
            mBackgroundWorker.DoWork += Server_DoWork;
            mBackgroundWorker.WorkerReportsProgress = true;
            mBackgroundWorker.ProgressChanged += Server_ProgressChanged;
            mBackgroundWorker.RunWorkerCompleted += Server_RunWorkerCompleted;

            textBoxLog.Clear();
            buttonStart.IsEnabled = false;

            mBackgroundWorker.RunWorkerAsync();
        }

        private void Server_DoWork(object sender, DoWorkEventArgs e) {
            var server = new Server();
            server.Run(mBackgroundWorker, PORT);
        }

        private void Client_DoWork(object sender, DoWorkEventArgs e) {
            var args = (ClientArgs)e.Argument;

            var client = new Client();
            client.Run(mBackgroundWorker, args.serverIP, PORT, args.continuousSendGB, args.testIterationCount);
        }

        void Client_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            buttonStart.IsEnabled = true;
        }

        private void Server_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            buttonStart.IsEnabled = true;
        }

        void Client_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            string s = e.UserState as string;
            textBoxLog.AppendText(s);
            textBoxLog.ScrollToEnd();
        }

        void Server_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            string s = e.UserState as string;
            textBoxLog.AppendText(s);
            textBoxLog.ScrollToEnd();
        }

        private void radioButtonClient_Checked(object sender, RoutedEventArgs e) {
            if (!mWindowLoaded) {
                return;
            }

            groupBoxClientSettings.IsEnabled = true;
            groupBoxServerSettings.IsEnabled = false;
        }

        private void radioButtonServer_Checked(object sender, RoutedEventArgs e) {
            if (!mWindowLoaded) {
                return;
            }

            groupBoxClientSettings.IsEnabled = false;
            groupBoxServerSettings.IsEnabled = true;
        }
    }
}
