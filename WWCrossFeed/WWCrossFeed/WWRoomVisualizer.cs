using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace WWCrossFeed {
    class WWRoomVisualizer {
        private Canvas mCanvas;
        private WWRoom mRoom;
        private Matrix3D mWorldProjectionMatrix;
        private double mCameraNear = 1.0;
        public double CameraFovHDegree { get; set; }
        private double mCameraDistanceCurrent;

        private WWVirtualTrackball mVirtualTrackball = new WWVirtualTrackball();

        public WWRoomVisualizer(Canvas canvas) {
            mCanvas = canvas;
            mCanvas.MouseDown += mCanvas_MouseDown;
            mCanvas.MouseMove += mCanvas_MouseMove;
            mCanvas.MouseUp += mCanvas_MouseUp;
            mCanvas.MouseWheel += mCanvas_MouseWheel;

            mVirtualTrackball.ScreenWH = new Size(mCanvas.Width, mCanvas.Height);
            mVirtualTrackball.SphereRadius = mCanvas.Width/2;
        }

        void mCanvas_MouseWheel(object sender, MouseWheelEventArgs e) {
            mCameraDistanceCurrent += -e.Delta * 0.01f;
            if (mCameraDistanceCurrent < mCameraNear * 2) {
                mCameraDistanceCurrent = mCameraNear * 2;
            }
            Redraw();
        }

        void mCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            mVirtualTrackball.Down(e.GetPosition(mCanvas));
        }

        void mCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) {
                return;
            }

            mVirtualTrackball.Move(e.GetPosition(mCanvas));
            Redraw();
        }

        void mCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            mVirtualTrackball.Up();
        }

        public void ResetCamera(double cameraDistance) {
            mCameraDistanceCurrent = cameraDistance;
            mVirtualTrackball.Reset();
        }

        private void UpdateCameraMatrix() {
            // right-handed coordinates

#if false
            // front view
            var eye = new Vector3D(0.0, 0.0, -CameraDistance);
            var at = new Vector3D(0.0, 0.0, 0.0);
            var up = new Vector3D(0.0, 1.0, 0.0);
            var lookAt = WWMatrixUtil.CalculateLookAt(eye, at, up);
#else
            // virtual trackball
            var cameraRot = mVirtualTrackball.RotationMatrix();
            var cameraTranslate = new Matrix3D();
            cameraTranslate.Translate(new Vector3D(0.0, 0.0, -mCameraDistanceCurrent));
            var cameraMat = cameraTranslate * cameraRot;
            var lookAt = cameraMat;
            lookAt.Invert();
#endif

            var viewProjectionMatrix = WWMatrixUtil.CreatePerspectiveProjectionMatrix(mCameraNear, mCameraDistanceCurrent * 2.0, CameraFovHDegree, 1.0);

            mWorldProjectionMatrix = lookAt * viewProjectionMatrix;
        }

        public void SetRoom(WWRoom room) {
            mRoom = room;
        }

        public void Redraw() {
            mCanvas.Children.Clear();

            UpdateCameraMatrix();

            RedrawRoom();
        }

        private void RedrawRoom() {
            DrawModel(mRoom.RoomModel, Matrix3D.Identity, new SolidColorBrush(Colors.White));

            Matrix3D listenerMatrix = new Matrix3D();
            listenerMatrix.Translate(mRoom.ListenerPos);
            DrawModel(mRoom.ListenerModel, listenerMatrix, new SolidColorBrush(Colors.Gray));

            for (int i = 0; i < WWRoom.NUM_OF_SPEAKERS; ++i) {
                var pos = mRoom.SpeakerPos(i);
                var dir = mRoom.SpeakerDir(i);
                Vector3D posV = new Vector3D(pos.X, pos.Y, pos.Z);
                Vector3D at = new Vector3D(pos.X +dir.X, pos.Y + dir.Y, pos.Z + dir.Z);
                Vector3D up = new Vector3D(0.0, 1.0, 0.0);

                Matrix3D speakerMatrix = WWMatrixUtil.CalculatePostureMatrix(posV, at, up);

                DrawModel(mRoom.SpeakerModel, speakerMatrix, new SolidColorBrush(Colors.Gray));
            }
        }

        private void DrawModel(WW3DModel model, Matrix3D modelWorldMatrix, Brush brush) {
            var modelProjectionMatrix = modelWorldMatrix * mWorldProjectionMatrix;

            var pointArray = model.TriangleList();
            var indexArray = model.IndexList();
            for (int i = 0; i < indexArray.Length / 3; ++i) {
                Point3D p0 = Point3D.Multiply(pointArray[indexArray[i * 3 + 0]], modelProjectionMatrix);
                Point3D p1 = Point3D.Multiply(pointArray[indexArray[i * 3 + 1]], modelProjectionMatrix);
                Point3D p2 = Point3D.Multiply(pointArray[indexArray[i * 3 + 2]], modelProjectionMatrix);
                AddNewLine(p0, p1, brush);
                AddNewLine(p1, p2, brush);
                AddNewLine(p2, p0, brush);
            }
        }

        private Vector ScaleToCanvas(Point3D v) {
            return new Vector(mCanvas.Width/2 * (v.X+1.0), mCanvas.Height/2 * (v.Y+1.0));
        }

        private void AddNewLine(Point3D p0, Point3D p1, Brush brush) {
            var from = ScaleToCanvas(p0);
            var to = ScaleToCanvas(p1);

            var line = new Line();
            line.X1 = from.X;
            line.Y1 = from.Y;
            line.X2 = to.X;
            line.Y2 = to.Y;
            line.Stroke = brush;

            mCanvas.Children.Add(line);
        }
    }
}
