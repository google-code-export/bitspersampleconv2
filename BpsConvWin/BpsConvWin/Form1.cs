using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.IO;
using System.Globalization;

namespace BpsConvWin
{
    public partial class Form1 : Form
    {
        private string readFileName;
        private System.Resources.ResourceManager rm;

        public Form1()
        {
            InitializeComponent();

            rm = BpsConvWin.Properties.Resources.ResourceManager;
            textBoxOutput.Text = rm.GetString("PleasePressBrowseButton") + "\r\n";
        }

        private static string ReadFileNameToWriteFileName(string readFileName, BpsConv.ConvertParams a)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}{1}{2}_{3}.wav",
                readFileName, a.addDither ? "_Dither" : "", a.noiseShaping ? "_NS" : "",
                a.newQuantizationBitrate);
        }

        private void buttonReadFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.ReadOnlyChecked    = true;
                ofd.Multiselect        = false;
                ofd.Filter             = rm.GetString("WavFileFilter");
                ofd.CheckPathExists    = true;
                ofd.CheckFileExists    = true;
                ofd.AutoUpgradeEnabled = true;
                DialogResult dr = ofd.ShowDialog();
                if (DialogResult.OK == dr) {
                    readFileName             = ofd.FileName;
                    buttonConvStart.Enabled  = true;
                    textBoxReadFilePath.Text = readFileName;

                    textBoxOutput.Text = string.Empty;
                    textBoxOutput.Text += rm.GetString("PleasePressConvButton") + "\r\n";
                }
            }
        }

        class BackgroundWorkerArgs
        {
            public string readFileName;
            public string writeFileName;
            public BpsConv.ConvertParams convParams;
        };

        private int workerProgress;

        private void WorkerDoNext()
        {
            ++workerProgress;

            if (15 < workerProgress) {
                textBoxOutput.Text += rm.GetString("ConvertEnd");
                buttonConvStart.Enabled = true;
                return;
            }

            var cp = new BpsConv.ConvertParams();
            cp.addDither = radioButtonDither.Checked;
            cp.noiseShaping = radioButtonNoiseShaping.Checked;
            cp.newQuantizationBitrate = workerProgress;

            string writeFileName = ReadFileNameToWriteFileName(readFileName, cp);
            textBoxOutput.Text += string.Format(CultureInfo.InvariantCulture, rm.GetString("ConvertingProgressText"),
                readFileName, writeFileName, workerProgress) + "\r\n";

            BackgroundWorkerArgs args = new BackgroundWorkerArgs();
            args.readFileName     = readFileName;
            args.writeFileName    = writeFileName;
            args.convParams = cp;

            backgroundWorker1.RunWorkerAsync(args);
        }

        private void buttonConvStart_Click(object sender, EventArgs e)
        {
            textBoxOutput.Text = rm.GetString("NowConverting") + "\r\n";

            workerProgress          = 0;
            buttonConvStart.Enabled = false;

            WorkerDoNext();
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (null != e.Result) {
                textBoxOutput.Text += (string)e.Result;
                return;
            }

            WorkerDoNext();
        }
         
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorkerArgs a    = e.Argument as BackgroundWorkerArgs;
            try {
                using (BinaryReader br = new BinaryReader(File.Open(a.readFileName, FileMode.Open))) {
                    using (BinaryWriter bw = new BinaryWriter(File.Open(a.writeFileName, FileMode.CreateNew))) {
                        if (!BpsConv.Convert(br, bw, a.convParams)) {
                            e.Result = rm.GetString("ConvFailedByWrongFormat");
                            return;
                        }
                    }
                }
            } catch (Exception ex) {
                e.Result = rm.GetString("ConvFailedByException") + "\r\n\r\n" + string.Format(CultureInfo.InvariantCulture, "{0}", ex);
                return;
            }
            e.Result = null;
        }

    }
}
