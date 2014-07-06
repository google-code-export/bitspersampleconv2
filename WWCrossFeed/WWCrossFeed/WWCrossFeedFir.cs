using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace WWCrossFeed {
    class WWCrossFeedFir {
        List<WWRoute> mRouteList = new List<WWRoute>();
        public double ReflectionRatio { get; set; }
        public double SoundSpeed { get; set; }

        private const double SMALL_GAIN_THRESHOLD = 0.01;

        List<WWFirCoefficient> mLeftSpeakerToLeftEar   = new List<WWFirCoefficient>();
        List<WWFirCoefficient> mLeftSpeakerToRightEar  = new List<WWFirCoefficient>();
        List<WWFirCoefficient> mRightSpeakerToLeftEar  = new List<WWFirCoefficient>();
        List<WWFirCoefficient> mRightSpeakerToRightEar = new List<WWFirCoefficient>();

        Random mRand = new Random();

        public WWCrossFeedFir() {
            ReflectionRatio = 0.8f;
            SoundSpeed = 330;
        }

        public void Clear() {
            mRouteList.Clear();
            mLeftSpeakerToLeftEar.Clear();
            mLeftSpeakerToRightEar.Clear();
            mRightSpeakerToLeftEar.Clear();
            mRightSpeakerToRightEar.Clear();
        }

        public int Count() {
            return mRouteList.Count;
        }

        public WWRoute GetNth(int idx) {
            return mRouteList[idx];
        }

        /// <summary>
        /// 初期設定する
        /// </summary>
        /// <param name="room"></param>
        public void Start(WWRoom room) {
            var leftEarPos  = room.ListenerEarPos(0);
            var rightEarPos = room.ListenerEarPos(1);

            var leftSpeakerPos = room.SpeakerPos(0);
            var rightSpeakerPos = room.SpeakerPos(1);

            // 左スピーカーから左の耳に音が届く
            var ll = leftEarPos - leftSpeakerPos;

            // エネルギーは、距離の2乗に反比例する
            // 振幅は、距離の1乗に反比例
            // ということにする。

            mLeftSpeakerToLeftEar.Add(new WWFirCoefficient(ll.Length / SoundSpeed, 1.0 / ll.Length));

            // 右スピーカーから右の耳に音が届く
            var rr = rightEarPos - rightSpeakerPos;
            mRightSpeakerToRightEar.Add(new WWFirCoefficient(rr.Length / SoundSpeed, 1.0 / rr.Length));

            // 左スピーカーから右の耳に音が届く。
            // 振幅が半分くらいになる。

            var lr = rightEarPos - leftSpeakerPos;
            mLeftSpeakerToRightEar.Add(new WWFirCoefficient(lr.Length / SoundSpeed, 0.5 / lr.Length));

            var rl = leftEarPos - rightSpeakerPos;
            mRightSpeakerToLeftEar.Add(new WWFirCoefficient(rl.Length / SoundSpeed, 0.5 / rl.Length));
        }

        private double CalcRouteDistance(WWRoom room, int speakerCh, WWRoute route, WWLineSegment lastSegment, Point3D hitPos) {
            var speakerToHitPos = hitPos - room.SpeakerPos(speakerCh);
            double distance = speakerToHitPos.Length;
            for (int i = 0; i < route.Count(); ++i) {
                var lineSegment = route.GetNth(i);
                distance += lineSegment.Length;
            }
            distance += lastSegment.Length;
            return distance;
        }

        private void StoreCoeff(int earCh, int speakerCh, WWFirCoefficient coeff) {
            int n = earCh + speakerCh * 2;

            switch (n) {
            case 0:
                mLeftSpeakerToLeftEar.Add(coeff);
                break;
            case 1:
                mLeftSpeakerToRightEar.Add(coeff);
                break;
            case 2:
                mRightSpeakerToLeftEar.Add(coeff);
                break;
            case 3:
                mRightSpeakerToRightEar.Add(coeff);
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
            return;
        }

        /// <summary>
        ///  スピーカーから耳に届く音がたどる経路を調べる。
        /// </summary>
        /// <param name="room"></param>
        /// <param name="earCh">耳 0:左耳, 1:右耳</param>
        public void Trace(WWRoom room, int earCh) {
            var route = new WWRoute(earCh);

            // 耳の位置
            var rayPos = room.ListenerEarPos(earCh);
            var earDir = room.ListenerEarDir(earCh);

            Vector3D rayDir = RayGen(earDir);
            //耳からrayが発射して、部屋の壁に当たる

            for (int i=0; i<100; ++i) {
                Point3D hitPos;
                Vector3D hitSurfaceNormal;
                double rayLength;
                if (!room.RayIntersection(rayPos, rayDir, out hitPos, out hitSurfaceNormal, out rayLength)) {
                    // 終わり。
                    break;
                }

                // スピーカーからの道のりを計算する。
                var lineSegment = new WWLineSegment(rayPos, rayDir, rayLength, 1.0f);

                int speakerCh = earCh;
                var distanceSame = CalcRouteDistance(room, speakerCh, route, lineSegment, hitPos);
                var coeffS = new WWFirCoefficient(distanceSame / SoundSpeed, 1.0f / distanceSame * Math.Pow(ReflectionRatio, i+1));
                lineSegment.Intensity = coeffS.Gain;

                if (coeffS.Gain < SMALL_GAIN_THRESHOLD) {
                    break;
                }

                StoreCoeff(earCh, earCh, coeffS);
                
                speakerCh = (earCh==0)?1:0;
                var distanceDifferent = CalcRouteDistance(room, speakerCh, route, lineSegment, hitPos);
                var coeffD = new WWFirCoefficient(distanceDifferent / SoundSpeed, 1.0f / distanceDifferent * Math.Pow(ReflectionRatio, i + 1));

                if (SMALL_GAIN_THRESHOLD <= coeffD.Gain) {
                    StoreCoeff(earCh, speakerCh, coeffD);
                }

                route.Add(lineSegment);
                rayPos = hitPos;
                rayDir = RayGen(hitSurfaceNormal);
            }

            if (route.Count() <= 0) {
                return;
            }

            mRouteList.Add(route);
        }

        private Vector3D RayGen(Vector3D dir) {
            while (true) {
                Vector3D d = new Vector3D(mRand.NextDouble() * 2.0 - 1.0, mRand.NextDouble() * 2.0 - 1.0, mRand.NextDouble() * 2.0 - 1.0);
                if (d.LengthSquared < float.Epsilon) {
                    continue;
                }

                d.Normalize();
                if (Vector3D.DotProduct(dir, d) < float.Epsilon) {
                    continue;
                }

                return d;
            }
        }
    }
}
