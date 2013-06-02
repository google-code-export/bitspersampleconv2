using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.IO;
using System.Globalization;

namespace BpsConvWin
{
    public partial class Form1 : Form
    {
        private string mReadFileName;
        private System.Resources.ResourceManager rm;

        private static string AssemblyVersion {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        public Form1()
        {
            InitializeComponent();

            rm = BpsConvWin.Properties.Resources.ResourceManager;
            textBoxOutput.Text = rm.GetString("PleasePressBrowseButton") + "\r\n";
            Text = string.Format(CultureInfo.CurrentCulture, "BpsConvWin {0}", AssemblyVersion);
        }

        private static string DitherTypeToString(BpsConv.ConvertParams.DitherType dt) {
            switch (dt) {
            case BpsConv.ConvertParams.DitherType.Truncate:
                return "";
            case BpsConv.ConvertParams.DitherType.RpdfDither:
                return "_Dither";
            case BpsConv.ConvertParams.DitherType.NoiseShaping:
                return "_NS";
            case BpsConv.ConvertParams.DitherType.GaussianDither:
                return "_Gaussian";
            case BpsConv.ConvertParams.DitherType.NoiseShaping2:
                return "_NS2";
            case BpsConv.ConvertParams.DitherType.NoiseShapingMash2:
                return "_MASH2";
            default:
                System.Diagnostics.Debug.Assert(false);
                return "";
            }
        }

        private static string ReadFileNameToWriteFileName(string readFileName, BpsConv.ConvertParams a)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}{1}_{2}.wav",
                readFileName, DitherTypeToString(a.ditherType),
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
                    mReadFileName             = ofd.FileName;
                    buttonConvStart.Enabled  = true;
                    textBoxReadFilePath.Text = mReadFileName;

                    textBoxOutput.Text = string.Empty;
                    textBoxOutput.Text += rm.GetString("PleasePressConvButton") + "\r\n";
                }
            }
        }

        class BackgroundWorkerArgs
        {
            public string readFileName;
        };

        private void buttonConvStart_Click(object sender, EventArgs e)
        {
            textBoxOutput.Text = rm.GetString("NowConverting") + "\r\n";

            buttonConvStart.Enabled = false;

            BackgroundWorkerArgs args = new BackgroundWorkerArgs();
            args.readFileName = mReadFileName;
            backgroundWorker1.RunWorkerAsync(args);
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            textBoxOutput.Text += rm.GetString("ConvertEnd") + "\r\n";
            buttonConvStart.Enabled = true;
        }
         
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorkerArgs a    = e.Argument as BackgroundWorkerArgs;
            var bpsConv = new BpsConv();

            try {
                using (BinaryReader br = new BinaryReader(File.Open(a.readFileName, FileMode.Open, FileAccess.Read, FileShare.Read))) {
                        if (!bpsConv.ReadFromFile(br)) {
                            e.Result = rm.GetString("ConvFailedByWrongFormat");
                            return;
                        }
                }

                for (int i=bpsConv.BitsPerSample-1; 1 <= i; --i) {
                    var cp = new BpsConv.ConvertParams();
                    cp.ditherType = BpsConv.ConvertParams.DitherType.Truncate;

                    if (radioButtonDither.Checked) {
                        cp.ditherType = BpsConv.ConvertParams.DitherType.RpdfDither;
                    }
                    if (radioButtonNoiseShaping.Checked) {
                        cp.ditherType = BpsConv.ConvertParams.DitherType.NoiseShaping;
                    }
                    if (radioButtonGaussianDither.Checked) {
                        cp.ditherType = BpsConv.ConvertParams.DitherType.GaussianDither;
                    }
                    if (radioButton2ndOrderNS.Checked) {
                        cp.ditherType = BpsConv.ConvertParams.DitherType.NoiseShaping2;
                    }
                    if (radioButtonMash2.Checked) {
                        cp.ditherType = BpsConv.ConvertParams.DitherType.NoiseShapingMash2;
                    }
                    cp.newQuantizationBitrate = i;

                    var writeFileName = ReadFileNameToWriteFileName(mReadFileName, cp);

                    backgroundWorker1.ReportProgress(i, cp);
                    using (BinaryWriter bw = new BinaryWriter(File.Open(writeFileName, FileMode.CreateNew))) {
                        bpsConv.Convert(cp, bw);
                    }
                }
            } catch (Exception ex) {
                e.Result = rm.GetString("ConvFailedByException") + "\r\n\r\n" + string.Format(CultureInfo.InvariantCulture, "{0}", ex);
                return;
            }
            e.Result = null;
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            var cp = e.UserState as BpsConv.ConvertParams;

            textBoxOutput.Text += string.Format(CultureInfo.InvariantCulture, rm.GetString("ConvertingProgressText"),
                    mReadFileName, ReadFileNameToWriteFileName(mReadFileName, cp), cp.newQuantizationBitrate) + "\r\n";
        }

    }
}
