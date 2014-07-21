﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace WWCrossFeed {
    class WWCrossFeedFir {
        List<WWRoute> mRouteList = new List<WWRoute>();
        /// <summary>
        /// 壁の反射率
        /// </summary>
        public double WallReflectionRatio { get; set; }

        /// <summary>
        /// 速さ m/s
        /// </summary>
        public double SoundSpeed { get; set; }

        /// <summary>
        /// 反射成分のゲイン 4程度
        /// </summary>
        public double ReflectionGain { get; set; }

        private const double SMALL_GAIN_THRESHOLD = 0.01;
        private const int FILE_VERSION = 2;

        List<WWFirCoefficient> mLeftSpeakerToLeftEar   = new List<WWFirCoefficient>();
        List<WWFirCoefficient> mLeftSpeakerToRightEar  = new List<WWFirCoefficient>();
        List<WWFirCoefficient> mRightSpeakerToLeftEar  = new List<WWFirCoefficient>();
        List<WWFirCoefficient> mRightSpeakerToRightEar = new List<WWFirCoefficient>();

        int[] mRouteCount = new int[2];

        Random mRand = new Random();

        public enum ReflectionType {
            Diffuse,
            Specular
        };

        public WWCrossFeedFir() {
            WallReflectionType = ReflectionType.Diffuse;
            WallReflectionRatio = 0.9f;
            SoundSpeed = 330;
        }

        public void Clear() {
            mRouteList.Clear();
            mLeftSpeakerToLeftEar.Clear();
            mLeftSpeakerToRightEar.Clear();
            mRightSpeakerToLeftEar.Clear();
            mRightSpeakerToRightEar.Clear();
            for (int i = 0; i < mRouteCount.Length; ++i) {
                mRouteCount[i] = 0;
            }
        }

        public int Count() {
            return mRouteList.Count;
        }

        public WWRoute GetNth(int idx) {
            return mRouteList[idx];
        }

        public ReflectionType WallReflectionType { get; set; }

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
            var llN = ll;
            llN.Normalize();

            // エネルギーは、距離の2乗に反比例する
            // 振幅は、距離の1乗に反比例
            // ということにする。

            mLeftSpeakerToLeftEar.Add(new WWFirCoefficient(ll.Length / SoundSpeed, llN, 1.0 / ll.Length, true));

            // 右スピーカーから右の耳に音が届く
            var rr = rightEarPos - rightSpeakerPos;
            var rrN = rr;
            rrN.Normalize();
            mRightSpeakerToRightEar.Add(new WWFirCoefficient(rr.Length / SoundSpeed, rrN, 1.0 / rr.Length, true));

            double gain = 1.0;
            if (WallReflectionType == ReflectionType.Specular) {
                // なんとなく、高音は逆の耳に届きにくい感じ。
                gain = 1.0/4.0;
            }

            // 左スピーカーから右の耳に音が届く。
            // 振幅が-4.5dBくらいになる。
            double attenuationDecibel = -4.5;
            double attenuationMagnitude = Math.Pow(10.0, attenuationDecibel / 20.0);

            var lr = rightEarPos - leftSpeakerPos;
            var lrN = lr;
            lrN.Normalize();
            mLeftSpeakerToRightEar.Add(new WWFirCoefficient(lr.Length / SoundSpeed, lrN, gain * attenuationMagnitude / lr.Length, true));

            var rl = leftEarPos - rightSpeakerPos;
            var rlN = rl;
            rlN.Normalize();
            mRightSpeakerToLeftEar.Add(new WWFirCoefficient(rl.Length / SoundSpeed, rlN, gain * attenuationMagnitude / rl.Length, true));

            // 1本のレイがそれぞれのスピーカーリスナー組に入る。
            mRouteCount[0] = 1;
            mRouteCount[1] = 1;
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

        public void TraceAll(WWRoom room) {
            for (int i = 0; i < 500000; ++i) {
                Trace(room, WallReflectionType, 0);
                Trace(room, WallReflectionType, 1);
            }
        }

        private static Vector3D SpecularReflection(Vector3D inDir, Vector3D surfaceNormal) {
            double dot = Vector3D.DotProduct(-inDir, surfaceNormal);
            return 2 * dot * surfaceNormal + inDir;
        }

        private double CalcReflectionGain(ReflectionType type, WWRoom room, int speakerCh, Point3D hitPos, Vector3D rayDir, Vector3D hitSurfaceNormal) {
            if (type == ReflectionType.Diffuse) {
                return 1.0;
            }

            // specular
            var reflectionDir = SpecularReflection(rayDir, hitSurfaceNormal);
            var speakerDir = room.SpeakerPos(speakerCh) - hitPos;
            speakerDir.Normalize();
            var dot = Vector3D.DotProduct(reflectionDir, speakerDir);
            return (dot + 1.0) / 2.0;
        }

        /// <summary>
        ///  スピーカーから耳に届く音がたどる経路を調べる。
        /// </summary>
        /// <param name="room"></param>
        /// <param name="earCh">耳 0:左耳, 1:右耳</param>
        public void Trace(WWRoom room, ReflectionType reflectionType, int earCh) {
            var route = new WWRoute(earCh);

            // 耳の位置
            var rayPos = room.ListenerEarPos(earCh);
            var earDir = room.ListenerEarDir(earCh);

            Vector3D rayDir = RayGen(earDir);
            //耳からrayが発射して、部屋の壁に当たる

            // 音が耳に向かう方向。
            Vector3D soundDir = -rayDir;
            var accumReflectionGain = new double[] {1.0, 1.0};

            for (int i = 0; i < 100; ++i) {
                Point3D hitPos;
                Vector3D hitSurfaceNormal;
                double rayLength;
                if (!room.RayIntersection(rayPos, rayDir, out hitPos, out hitSurfaceNormal, out rayLength)) {
                    // 終わり。
                    break;
                }

                // 1.0 - 反射率の確率で、計算を打ち切る。
                // たとえば反射率0.8の壁にRayが10本入射すると、8本のRayが強度を100%保ったまま反射する。
                if (WallReflectionRatio < mRand.NextDouble()) {
                    break;
                }

                // スピーカーからの道のりを計算する。
                var lineSegment = new WWLineSegment(rayPos, rayDir, rayLength, 1.0f /* 仮 Intensity */ );

                {
                    int speakerCh = earCh;
                    var distance = CalcRouteDistance(room, speakerCh, route, lineSegment, hitPos);
                    double gain = CalcReflectionGain(reflectionType, room, speakerCh, hitPos, rayDir, hitSurfaceNormal);
                    accumReflectionGain[0] *= gain;
                    var coeffS = new WWFirCoefficient(distance / SoundSpeed, soundDir, accumReflectionGain[0] / distance, false);
                    lineSegment.Intensity = coeffS.Gain;

                    if (1.0 / distance < SMALL_GAIN_THRESHOLD) {
                        break;
                    }

                    if (SMALL_GAIN_THRESHOLD <= coeffS.Gain) {
                        StoreCoeff(earCh, earCh, coeffS);
                    }
                }

                {
                    int speakerCh = (earCh == 0) ? 1 : 0;
                    var distance = CalcRouteDistance(room, speakerCh, route, lineSegment, hitPos);
                    double gain = CalcReflectionGain(reflectionType, room, speakerCh, hitPos, rayDir, hitSurfaceNormal);
                    accumReflectionGain[1] *= gain;
                    var coeffD = new WWFirCoefficient(distance / SoundSpeed, soundDir, accumReflectionGain[1] / distance, false);

                    if (SMALL_GAIN_THRESHOLD <= coeffD.Gain) {
                        StoreCoeff(earCh, speakerCh, coeffD);
                    }
                }

                route.Add(lineSegment);
                rayPos = hitPos;

                // 反射後の出射方向rayDir
                switch (reflectionType) {
                case ReflectionType.Diffuse:
                    rayDir = RayGen(hitSurfaceNormal);
                    break;
                case ReflectionType.Specular:
                    rayDir = SpecularReflection(rayDir, hitSurfaceNormal);
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    break;
                }
            }

            if (route.Count() <= 0) {
                return;
            }

            mRouteList.Add(route);
            ++mRouteCount[earCh];
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

        public Dictionary<int, double>[] OutputFirCoeffs(int sampleRate) {
            var ll = CreateFirCoeff(sampleRate, mLeftSpeakerToLeftEar);
            var lr = CreateFirCoeff(sampleRate, mLeftSpeakerToRightEar);
            var rl = CreateFirCoeff(sampleRate, mRightSpeakerToLeftEar);
            var rr = CreateFirCoeff(sampleRate, mRightSpeakerToRightEar);

            return new Dictionary<int, double>[] { ll, lr, rl, rr };
        }

        private Dictionary<int, double> CreateFirCoeff(int sampleRate, List<WWFirCoefficient> coeffList) {
            var table = new Dictionary<int, Vector3D>();
            double ratio = ReflectionGain / mRouteCount[0];

            foreach (var coeff in coeffList) {
                int delaySample = (int)(coeff.DelaySecond * sampleRate);
                Vector3D v = coeff.SoundDirection * coeff.Gain;

                if (table.ContainsKey(delaySample)) {

                    table[delaySample] += v;
                } else {
                    table[delaySample] = v;
                }
            }

            var items = from pair in table
                    orderby pair.Key
                    select pair;

            bool bFirst = true;

            var result = new Dictionary<int,double>();

            foreach (var entry in items) {
                double gain = entry.Value.Length;
                if (bFirst) {
                    bFirst = false;
                } else {
                    gain *= ratio;
                }
                result.Add(entry.Key, gain);
            }

            return result;
        }

        public static void OutputFile(int sampleRate, Dictionary<int, double>[] coeffs, string path) {
            int smallestTime = int.MaxValue;
            int largestTime = 0;

            foreach (var c in coeffs) {
                if (c.First().Key < smallestTime) {
                    smallestTime = c.First().Key;
                }
                if (largestTime < c.Last().Key) {
                    largestTime = c.Last().Key;
                }
            }

            using (StreamWriter sw = new StreamWriter(path)) {
                sw.WriteLine("CFD{0}", FILE_VERSION);
                sw.WriteLine("{0}", sampleRate);
                sw.WriteLine("{0}", largestTime - smallestTime+1);
                sw.WriteLine("# LeftSpeakerToLeftEar LowFreq, LeftSpeakerToRightEar LowFreq, RightSpeakerToLeftEar LowFreq, RightSpeakerToRightEar LowFreq, LeftSpeakerToLeftEar HighFreq, LeftSpeakerToRightEar HighFreq, RightSpeakerToLeftEar HighFreq, RightSpeakerToRightEar HighFreq");

                for (int t = 0; t <= largestTime - smallestTime; ++t) {
                    var v = new double[coeffs.Length];

                    for (int i = 0; i < coeffs.Length; ++i) {
                        if (coeffs[i].ContainsKey(t + smallestTime)) {
                            v[i] = coeffs[i][t+smallestTime];
                        }

                        sw.Write("{0}", v[i]);
                        if (i != coeffs.Length - 1) {
                            sw.Write(", ");
                        }
                    }
                    sw.WriteLine("");
                }
            }
        }
    }
}
