using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace WWCrossFeed {
    class WWIntersection {
        public static bool TriangleRayIntersect(Vector3D v0, Vector3D v1, Vector3D v2, Vector3D rayOrig, Vector3D rayDir) {
            Vector3D edge0 = v1 - v0;
            Vector3D edge1 = v2 - v1;
            Vector3D edge2 = v0 - v2;

            // plane normal. no need to normalize 
            Vector3D n = Vector3D.CrossProduct(edge0, -edge2);

            double nDotRay = Vector3D.DotProduct(n, rayDir);
            if (-float.Epsilon < nDotRay) {
                // レイとトライアングルが平行、またはトライアングルの裏面にレイが入射。
                return false;
            }

            double d = Vector3D.DotProduct(n, v0);
            double t = -(Vector3D.DotProduct(n, rayOrig) + d) / nDotRay;

            if (t < 0) {
                // rayの始点よりも後方に当たりがある。
                return false;
            }

            // レイとトライアングル面との交点p
            Vector3D p = rayOrig + t * rayDir;

            Vector3D c;

            Vector3D vp0 = p - v0;
            c = Vector3D.CrossProduct(edge0, vp0);
            if (Vector3D.DotProduct(n, c) < 0) {
                return false;
            }

            Vector3D vp1 = p - v1;
            c = Vector3D.CrossProduct(edge1, vp1);
            if (Vector3D.DotProduct(n, c) < 0) {
                return false;
            }

            Vector3D vp2 = p - v2;
            c = Vector3D.CrossProduct(edge2, vp2);
            if (Vector3D.DotProduct(n, c) < 0) {
                return false;
            }

            return true;
        }
    }
}
