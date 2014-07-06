using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace WWCrossFeed {
    class WWCrossFeedFir {
        List<WWRoute> mRouteList = new List<WWRoute>();

        Random mRand = new Random();

        public void Clear() {
            mRouteList = new List<WWRoute>();
        }

        public int Count() {
            return mRouteList.Count;
        }

        public WWRoute GetNth(int idx) {
            return mRouteList[idx];
        }

        /// <summary>
        ///  スピーカーから耳に届く音がたどる経路を調べる。
        /// </summary>
        /// <param name="room"></param>
        /// <param name="earCh">耳 0:左耳, 1:右耳</param>
        public void Trace(WWRoom room, int earCh) {
            var route = new WWRoute();

            // 耳の位置
            var rayPos = room.ListenerEarPos(earCh);
            var earDir = room.ListenerEarDir(earCh);

            Vector3D rayDir = RayGen(earDir);
            //耳からrayが発射して、部屋の壁に当たる

            Point3D hitPos;
            Vector3D hitSurfaceNormal;
            double rayLength;
            if (!room.RayIntersection(rayPos, rayDir, out hitPos, out hitSurfaceNormal, out rayLength)) {
                // 終わり。
                return;
            }

            route.Add(new WWLineSegment(rayPos, rayDir, rayLength, 1.0f));
            mRouteList.Add(route);

            for (int i = 0; i < 2; ++i) {
                rayPos = hitPos;
                rayDir = RayGen(hitSurfaceNormal);
                if (!room.RayIntersection(rayPos, rayDir, out hitPos, out hitSurfaceNormal, out rayLength)) {
                    // 終わり。
                    return;
                }

                route.Add(new WWLineSegment(rayPos, rayDir, rayLength, 1.0f));
                mRouteList.Add(route);
            }
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
