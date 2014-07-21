using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WWCrossFeed {
    public partial class MainWindow : Window {
        private const double CAMERA_DISTANCE_DEFAULT = 12.0;
        private const double SMALLEST_ROOM_LENGTH = 2.0;
        private WWRoom mRoom = new WWRoom();
        private WWRoomVisualizer mRoomVisualizer;
        private bool mInitialized = false;
        private WWCrossFeedFir mCrossFeed = new WWCrossFeedFir();

        public MainWindow() {
            InitializeComponent();
            mInitialized = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mRoomVisualizer = new WWRoomVisualizer(mCanvas);
            mRoomVisualizer.SetRoom(mRoom);
            mRoomVisualizer.ResetCamera(CAMERA_DISTANCE_DEFAULT);
            mRoomVisualizer.SetCrossFeed(mCrossFeed);

            if (!UpdateParameters()) {
                Close();
            }

            UpdateRoomCanvas();
        }

        private bool UpdateParameters() {
            bool result = true;

            // listener
            WW3DModel model;
            model = LoadModel((mRadioButtonGenerateDefaultListener.IsChecked == true) ? "" : mListenerModelPath.Text, Properties.Resources.listenerModel);
            if (model == null) {
                return false;
            }
            mRoom.ListenerModel = model;
            if (!SetupListenerPosition()) {
                return false;
            }

            // room model
            if (mRadioButtonCreateRoomFromDimension.IsChecked == true) {
                mRoom.RoomModel = GenerateRoomModelFromDimension();
            } else {
                mRoom.RoomModel = WWWaveFrontObjReader.ReadFromFile(mRoomModelPath.Text);
                result = (mRoom.RoomModel != null);
            }
            if (!result) {
                return false;
            }

            // speaker
            model = LoadModel((mRadioButtonGenerateDefaultSpeaker.IsChecked == true) ? "" : mSpeakerModelPath.Text, Properties.Resources.speakerModel2);
            if (model == null) {
                return false;
            }
            mRoom.SpeakerModel = model;
            if (!SetupSpeakerPosition()) {
                return false;
            }

            // camera
            if (!SetupCamera()) {
                return false;
            }

            return true;
        }

        private bool SetupCamera() {
            double fovH;

            if (!Double.TryParse(mTextBoxCameraFovH.Text, out fovH)) {
                MessageBox.Show("Error: Camera parameter parse error");
                return false;
            }
            mRoomVisualizer.CameraFovHDegree = fovH;
            return true;
        }

        private bool SetupListenerPosition() {
            double x, y, z;
            double headSize;

            if (!Double.TryParse(mTextBoxListenerPositionX.Text, out x) ||
                    !Double.TryParse(mTextBoxListenerPositionY.Text, out y) ||
                    !Double.TryParse(mTextBoxListenerPositionZ.Text, out z) ||
                    !Double.TryParse(mTextBoxListenerHeadSize.Text, out headSize)) {
                MessageBox.Show("Error: Listener settings parse error");
                return false;
            }
            mRoom.ListenerPos = new Point3D(x, y, z);
            mRoom.ListenerHeadRadius = headSize / 2.0;
            return true;
        }

        private bool SetupSpeakerPosition() {
            double x, y, z;

            if (!Double.TryParse(mTextBoxLeftSpeakerX.Text, out x) ||
                    !Double.TryParse(mTextBoxLeftSpeakerY.Text, out y) ||
                    !Double.TryParse(mTextBoxLeftSpeakerZ.Text, out z)) {
                MessageBox.Show("Error: Left speaker position parse error");
                return false;
            }
            mRoom.SetSpeakerPos(0, new Point3D(x, y, z));

            if (!Double.TryParse(mTextBoxRightSpeakerX.Text, out x) ||
                    !Double.TryParse(mTextBoxRightSpeakerY.Text, out y) ||
                    !Double.TryParse(mTextBoxRightSpeakerZ.Text, out z)) {
                MessageBox.Show("Error: Left speaker position parse error");
                return false;
            }
            mRoom.SetSpeakerPos(1, new Point3D(x, y, z));

            if (!Double.TryParse(mTextBoxLeftSpeakerDX.Text, out x) ||
                    !Double.TryParse(mTextBoxLeftSpeakerDY.Text, out y) ||
                    !Double.TryParse(mTextBoxLeftSpeakerDZ.Text, out z) ||
                new Vector3D(x, y, z).LengthSquared < float.Epsilon) {
                MessageBox.Show("Error: Left speaker direction parse error");
                return false;
            }
            {
                var dir = new Vector3D(x, y, z);
                dir.Normalize();
                mRoom.SetSpeakerDir(0, dir);
            }

            if (!Double.TryParse(mTextBoxRightSpeakerDX.Text, out x) ||
                    !Double.TryParse(mTextBoxRightSpeakerDY.Text, out y) ||
                    !Double.TryParse(mTextBoxRightSpeakerDZ.Text, out z)) {
                MessageBox.Show("Error: Left speaker direction parse error");
                return false;
            }
            {
                var dir = new Vector3D(x, y, z);
                dir.Normalize();
                mRoom.SetSpeakerDir(1, dir);
            }
            return true;
        }

        private WW3DModel GenerateRoomModelFromDimension() {
            double w, d, h;

            if (!Double.TryParse(mRoomW.Text, out w) || w < SMALLEST_ROOM_LENGTH ||
                    !Double.TryParse(mRoomD.Text, out d) || d < SMALLEST_ROOM_LENGTH ||
                    !Double.TryParse(mRoomH.Text, out h) || h < SMALLEST_ROOM_LENGTH) {
                MessageBox.Show("Error: Set room size larger than or equal to " + SMALLEST_ROOM_LENGTH);
                return null;
            }
            return WWModeler.GenerateCuboid(new Size3D(w, h, d), new Vector3D(0, h/2, 0), WWModeler.NormalDirection.Inward);
        }

        private WW3DModel LoadModel(string path, byte[] defaultModel) {
            WW3DModel model;

            if (path == null || path.Length == 0) {
                model = WWWaveFrontObjReader.ReadFromStream(new MemoryStream(defaultModel));
            } else {
                model = WWWaveFrontObjReader.ReadFromFile(path);
            }

            if (model == null) {
                MessageBox.Show("Error: Could not read file: " + path);
                return null;
            }
            return model;
        }

        private void UpdateRoomCanvas() {
            mRoomVisualizer.Redraw();
        }

        private void mRadioButtonCreateRoomFromDimension_Checked(object sender, RoutedEventArgs e) {
            if (!mInitialized) {
                return;
            }
            mGroupBoxRoomDimension.IsEnabled = true;
            mGroupBoxRoomFile.IsEnabled = false;
        }

        private void mRadioButtonCreateRoomFromFile_Checked(object sender, RoutedEventArgs e) {
            if (!mInitialized) {
                return;
            }
            mGroupBoxRoomDimension.IsEnabled = false;
            mGroupBoxRoomFile.IsEnabled = true;
        }

        private void mRadioButtonGenerateDefaultListener_Checked(object sender, RoutedEventArgs e) {
            if (!mInitialized) {
                return;
            }
            mGroupBoxListenerModelFile.IsEnabled = false;
        }

        private void mRadioButtonGenerateListenerFromFile_Checked(object sender, RoutedEventArgs e) {
            if (!mInitialized) {
                return;
            }
            mGroupBoxListenerModelFile.IsEnabled = true;
        }

        private string OpenFileDialogHelper(string defaultExt, string filter) {
            var dlg = new OpenFileDialog();
            dlg.CheckFileExists = true;
            dlg.CheckPathExists = true;
            dlg.ValidateNames = true;
            dlg.Multiselect = false;
            dlg.DefaultExt = defaultExt;
            dlg.Filter = filter;
            var result = dlg.ShowDialog();
            if (result != true) {
                return null;
            }
            return dlg.FileName;
        }

        private void ButtonBrowseListenerModel_Click(object sender, RoutedEventArgs e) {
            string path = OpenFileDialogHelper(Properties.Resources.WavefrontObjFileExt, Properties.Resources.WavefrontObjFilter);
            if (path == null) {
                return;
            }

            var model = WWWaveFrontObjReader.ReadFromFile(path);
            if (model == null) {
                return;
            }

            mListenerModelPath.Text = path;

            mRoom.ListenerModel = model;
            UpdateRoomCanvas();
        }

        private void mButtonUpdateView_Click(object sender, RoutedEventArgs e) {
            if (!UpdateParameters()) {
                mCanvas.Children.Clear();
                return;
            }

            UpdateRoomCanvas();
        }

        private void ButtonCameraReset_Click(object sender, RoutedEventArgs e) {
            mRoomVisualizer.ResetCamera(CAMERA_DISTANCE_DEFAULT);
            SetupCamera();

            UpdateRoomCanvas();
        }

        private void mRadioButtonGenerateDefaultSpeaker_Checked(object sender, RoutedEventArgs e) {
            if (!mInitialized) {
                return;
            }
            mGroupBoxSpeakerModelFile.IsEnabled = false;
        }

        private void mRadioButtonGenerateSpeakerFromFile_Checked(object sender, RoutedEventArgs e) {
            if (!mInitialized) {
                return;
            }
            mGroupBoxSpeakerModelFile.IsEnabled = true;
        }

        private void ButtonBrowseSpeakerModel_Click(object sender, RoutedEventArgs e) {
            string path = OpenFileDialogHelper(Properties.Resources.WavefrontObjFileExt, Properties.Resources.WavefrontObjFilter);
            if (path == null) {
                return;
            }

            var model = WWWaveFrontObjReader.ReadFromFile(path);
            if (model == null) {
                return;
            }

            mSpeakerModelPath.Text = path;

            mRoom.SpeakerModel = model;
            UpdateRoomCanvas();
        }

        private void mButtonRayTest_Click(object sender, RoutedEventArgs e) {
            mCrossFeed.Clear();
            mCrossFeed.Start(mRoom);
            for (int i = 0; i < 20; ++i) {
                mCrossFeed.Trace(mRoom, 0);
                mCrossFeed.Trace(mRoom, 1);
            }

            UpdateRoomCanvas();
        }

        private void ButtonClearRay_Click(object sender, RoutedEventArgs e) {
            mCrossFeed.Clear();
            UpdateRoomCanvas();
        }

        private void ButtonCreateFirCoefficients(object sender, RoutedEventArgs e) {
            if (!UpdateParameters()) {
                return;
            }

            var dlg = new SaveFileDialog();
            dlg.DefaultExt = Properties.Resources.FirFilterFileExt;
            dlg.Filter = Properties.Resources.FirFilterFileFilter;
            dlg.FileName = "CrossfeedFilterCoeffs" + Properties.Resources.FirFilterFileExt;
            dlg.CheckPathExists = true;
            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            int sampleRate;
            Int32.TryParse(mTextBoxSampleRate.Text, out sampleRate);

            double reflectionGain;
            Double.TryParse(mTextBoxRefrectionGain.Text, out reflectionGain);

            double wallReflectionRatio;
            Double.TryParse(mWallReflectionRatio.Text, out wallReflectionRatio);

            mCrossFeed.Clear();
            mCrossFeed.ReflectionGain = reflectionGain;
            
            // エネルギー比 to 振幅比の変換。
            mCrossFeed.WallReflectionRatio = Math.Sqrt(wallReflectionRatio); 

            mCrossFeed.Start(mRoom);
            for (int i = 0; i < 500000; ++i) {
                mCrossFeed.Trace(mRoom, 0);
                mCrossFeed.Trace(mRoom, 1);
            }
            mCrossFeed.OutputFirCoeffs(sampleRate, dlg.FileName);

            mCrossFeed.Clear();
            UpdateRoomCanvas();
        }

    }
}
