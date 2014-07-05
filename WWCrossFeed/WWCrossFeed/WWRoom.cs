using System.Windows.Media.Media3D;

namespace WWCrossFeed {
    class WWRoom {
        public const int NUM_OF_SPEAKERS = 2;
        public WW3DModel ListenerModel { get; set; }
        public WW3DModel SpeakerModel { get; set; }
        public WW3DModel RoomModel { get; set; }
        private WWPosture[] mSpeakerPos = new WWPosture[NUM_OF_SPEAKERS];
        public Vector3D ListenerPos { get; set; }

        public WWRoom() {
            for (int i=0; i<NUM_OF_SPEAKERS; ++i) {
                mSpeakerPos[i] = new WWPosture();
            }
        }

        public Point3D SpeakerPos(int idx) {
            return mSpeakerPos[idx].Pos;
        }
        public Vector3D SpeakerDir(int idx) {
            return mSpeakerPos[idx].Dir;
        }

        public void SetSpeakerPos(int idx, Point3D pos) {
            mSpeakerPos[idx].Pos = pos;
        }

        public void SetSpeakerDir(int idx, Vector3D dir) {
            mSpeakerPos[idx].Dir = dir;
        }
    }
}
