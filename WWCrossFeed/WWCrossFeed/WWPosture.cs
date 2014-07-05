using System.Windows.Media.Media3D;

namespace WWCrossFeed {
    class WWPosture {
        public Point3D Pos { get; set; }
        public Vector3D Dir { get; set; }

        public WWPosture() {
            Pos = new Point3D();
            Dir = new Vector3D(0.0f, 0.0f, 1.0f);
        }

        public WWPosture(Point3D pos, Vector3D dir) {
            Pos = pos;
            Dir = dir;
        }
    }
}
