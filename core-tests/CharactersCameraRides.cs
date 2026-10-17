using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Routing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>One rendered frame of a recorded ride: what the explorer hands the camera (game metres).</summary>
    internal struct RideFrame
    {
        /// <summary>Frame time, seconds (what Unity's frame took).</summary>
        public float Dt;

        public double X, Z;
        public float Y, HeadingRad, SpeedMps, LeanRad;
        public bool Airborne;
    }

    /// <summary>
    /// Recorded rides over the committed sample region (shared/sample-regions/kathmandu_core) for the camera tests
    /// (owner playtest #5: "the camera constantly keeps moving, which makes the driving experience really bad"). A route
    /// from the sample's routing graph is driven by the real <see cref="ArcadeVehicle"/> on the real
    /// <see cref="TileGroundQuery"/> (terrain, drawn roads, decks, house solids, road magnet) by a simple autopilot at a
    /// constant cruise speed, stepped at 60 Hz exactly as <c>FixedStepDriver</c> does and interpolated into frames of a
    /// phone-like frame clock (about 30 fps with jitter and a few hitches). Deterministic.
    /// </summary>
    internal static class CameraRides
    {
        public const float StepS = 1f / 60f;

        private static RouteGraph _graph;
        private static TileGroundQuery _ground;

        /// <summary>Every tile of the sample pack in the ground query the world makes (WorldRoot: bridge decks, solids
        /// trimmed to the drawn road corridors, hero footprints left to the heroes; Mid tier, October), cached for the run.</summary>
        public static TileGroundQuery Ground
        {
            get
            {
                if (_ground != null) return _ground;
                TileGroundQuery g = StreamingScene.Ground(StreamingScene.Settings(1, 10));
                foreach (TileData t in DrivingData.SampleTiles().Values) g.Add(t);
                _ground = g;
                return g;
            }
        }

        public static RouteGraph Graph
        {
            get
            {
                if (_graph != null) return _graph;
                string path = Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.route.ghrg");
                if (!File.Exists(path)) Assert.Fail("sample route graph missing: " + path);
                _graph = RouteGraphReader.Read(File.ReadAllBytes(path));
                return _graph;
            }
        }

        /// <summary>The route a <paramref name="profile"/> plans between two places (lon/lat), as a game-metre polyline
        /// (x, z interleaved).</summary>
        public static double[] Route(Travel profile, double lonA, double latA, double lonB, double latB)
        {
            return Route(profile, lonA, latA, lonB, latB, 0.0);
        }

        /// <summary>As <see cref="Route(Travel, double, double, double, double)"/>, moved <paramref name="leftM"/> to the
        /// left of the centre line (Nepal drives on the left; a divided road's median runs down the middle).</summary>
        public static double[] Route(Travel profile, double lonA, double latA, double lonB, double latB, double leftM)
        {
            double[] line = CentreLine(profile, lonA, latA, lonB, latB);
            if (leftM == 0.0) return line;
            int n = line.Length / 2;
            var o = new double[line.Length];
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(n - 1, i + 1);
                double dx = line[2 * b] - line[2 * a], dz = line[2 * b + 1] - line[2 * a + 1];
                double len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-9) len = 1.0;
                // Left of travel: forward (dx, dz) turned a quarter to the left is (-dz, dx).
                o[2 * i] = line[2 * i] - dz / len * leftM;
                o[2 * i + 1] = line[2 * i + 1] + dx / len * leftM;
            }
            return o;
        }

        private static double[] CentreLine(Travel profile, double lonA, double latA, double lonB, double latB)
        {
            RouteGraph g = Graph;
            double ax, az, bx, bz;
            WorldFrame.LonLatToGame(lonA, latA, out ax, out az);
            WorldFrame.LonLatToGame(lonB, latB, out bx, out bz);
            int src = new NearestNode(g, profile, false).Find(ax, az);
            int dst = new NearestNode(g, profile, true).Find(bx, bz);
            Assert.That(src, Is.GreaterThanOrEqualTo(0), "route start");
            Assert.That(dst, Is.GreaterThanOrEqualTo(0), "route end");
            Route r = new AStar(g).FindRoute(src, dst, profile);
            Assert.That(r, Is.Not.Null, "no " + profile + " route");
            return new AStar(g).RouteGeometry(r);
        }

        /// <summary>The frame clock of a phone at about 30 fps: 33.3 ms ± 4 ms of jitter, a 50 ms hitch every 97 frames and
        /// a 16.7 ms frame every 13 (deterministic).</summary>
        public static float FrameDt(int frame)
        {
            if (frame % 97 == 96) return 0.05f;
            if (frame % 13 == 12) return 1f / 60f;
            uint h = CharMath.Hash((uint)frame, 0x5EEDu);
            return 1f / 30f + (CharMath.Unit(h) - 0.5f) * 0.008f;
        }

        /// <summary>
        /// Drives <paramref name="path"/> with a vehicle of <paramref name="preset"/> at <paramref name="cruiseMps"/> (slower
        /// only where a bend needs it) and records every frame. The autopilot steers at a point 4–14 m ahead on the path.
        /// <paramref name="startAtM"/> skips the first metres of the route. Stops at the end of the path, after
        /// <paramref name="maxSeconds"/>, or when stuck for 3 s.
        /// </summary>
        public static List<RideFrame> Drive(double[] path, HandlingPreset preset, float cruiseMps, float maxSeconds, out RideStats stats,
                                            double startAtM = 0.0, Func<int, float> clock = null)
        {
            TileGroundQuery g = Ground;
            int n = path.Length / 2;
            var cum = new double[n];
            for (int i = 1; i < n; i++)
            {
                double dx = path[2 * i] - path[2 * i - 2], dz = path[2 * i + 1] - path[2 * i - 1];
                cum[i] = cum[i - 1] + Math.Sqrt(dx * dx + dz * dz);
            }
            double total = cum[n - 1];
            stats = new RideStats { RouteM = total };

            var v = new ArcadeVehicle(VehicleSpec.For(preset));
            double sx, sz, hx, hz;
            At(path, cum, startAtM, out sx, out sz);
            At(path, cum, startAtM + 3.0, out hx, out hz);
            v.Teleport(sx, sz, (float)Math.Atan2(hx - sx, hz - sz), g);
            Pose prev = Pose.Of(v), cur = prev;
            double s = startAtM;
            float acc = 0f, stuck = 0f, time = 0f;
            var frames = new List<RideFrame>(4096);
            for (int f = 0; time < maxSeconds; f++)
            {
                float dt = clock != null ? clock(f) : FrameDt(f);
                time += dt;
                acc += Math.Min(dt, 0.25f);
                while (acc >= StepS - 1e-6f)
                {
                    acc = Math.Max(0f, acc - StepS);
                    s = Progress(path, cum, v.X, v.Z, s);
                    float speed = Math.Abs(v.SpeedMps);
                    float look = Math.Max(4f, Math.Min(14f, 2.5f + 0.7f * speed));
                    double tx, tz;
                    At(path, cum, s + look, out tx, out tz);
                    float want = (float)Math.Atan2(tx - v.X, tz - v.Z);
                    float err = CharMath.WrapRad(want - v.HeadingRad);
                    float bend = Bend(path, cum, s, 25.0);
                    float target = Math.Min(cruiseMps, Math.Max(3f, (float)Math.Sqrt(3.0 / Math.Max(1e-3, bend))));
                    float throttle = CharMath.Clamp((target - v.SpeedMps) * 0.6f + 0.15f, -1f, 1f);
                    float brake = v.SpeedMps > target + 1.5f ? 0.5f : 0f;
                    var input = new DriveInput(throttle, brake, CharMath.Clamp(err * 2.2f, -1f, 1f));
                    prev = cur;
                    StepEvents ev = v.Step(input, StepS, g, 0f);
                    if ((ev & StepEvents.HitWall) != 0) stats.WallHits++;
                    if ((ev & StepEvents.Bump) != 0) stats.Bumps++;
                    cur = Pose.Of(v);
                }
                float t = acc / StepS;
                var fr = new RideFrame
                {
                    Dt = dt,
                    X = prev.X + (cur.X - prev.X) * t,
                    Z = prev.Z + (cur.Z - prev.Z) * t,
                    Y = prev.Y + (cur.Y - prev.Y) * t,
                    HeadingRad = CharMath.WrapRad(prev.H + CharMath.WrapRad(cur.H - prev.H) * t),
                    SpeedMps = prev.V + (cur.V - prev.V) * t,
                    LeanRad = prev.Lean + (cur.Lean - prev.Lean) * t,
                    Airborne = cur.Air,
                };
                frames.Add(fr);
                stuck = Math.Abs(v.SpeedMps) < 0.5f && time > 2f ? stuck + dt : 0f;
                if (stuck > 3f)
                {
                    stats.Stuck = true;
                    stats.StuckX = v.X;
                    stats.StuckZ = v.Z;
                    break;
                }
                if (s >= total - 8.0) break;
            }
            stats.DrivenM = s - startAtM;
            stats.Seconds = time;
            return frames;
        }

        private struct Pose
        {
            public double X, Z;
            public float Y, H, V, Lean;
            public bool Air;

            public static Pose Of(ArcadeVehicle v)
            {
                return new Pose { X = v.X, Z = v.Z, Y = v.Y, H = v.HeadingRad, V = v.SpeedMps, Lean = v.Lean, Air = v.Airborne };
            }
        }

        private static void At(double[] p, double[] cum, double s, out double x, out double z)
        {
            int n = cum.Length;
            if (s <= 0)
            {
                x = p[0];
                z = p[1];
                return;
            }
            for (int i = 1; i < n; i++)
            {
                if (cum[i] < s) continue;
                double seg = cum[i] - cum[i - 1];
                double t = seg > 1e-9 ? (s - cum[i - 1]) / seg : 0.0;
                x = p[2 * i - 2] + (p[2 * i] - p[2 * i - 2]) * t;
                z = p[2 * i - 1] + (p[2 * i + 1] - p[2 * i - 1]) * t;
                return;
            }
            x = p[2 * n - 2];
            z = p[2 * n - 1];
        }

        /// <summary>The path distance nearest to (x, z), searched from <paramref name="s"/> forward a little.</summary>
        private static double Progress(double[] p, double[] cum, double x, double z, double s)
        {
            int n = cum.Length;
            double best = double.MaxValue, bestS = s;
            for (int i = 1; i < n; i++)
            {
                if (cum[i] < s - 5.0) continue;
                if (cum[i - 1] > s + 30.0) break;
                double ax = p[2 * i - 2], az = p[2 * i - 1], ex = p[2 * i] - ax, ez = p[2 * i + 1] - az;
                double len2 = ex * ex + ez * ez;
                double t = len2 > 1e-12 ? ((x - ax) * ex + (z - az) * ez) / len2 : 0.0;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                double qx = ax + ex * t - x, qz = az + ez * t - z;
                double d = qx * qx + qz * qz;
                if (d < best)
                {
                    best = d;
                    bestS = cum[i - 1] + Math.Sqrt(len2) * t;
                }
            }
            return Math.Max(s, bestS);
        }

        /// <summary>The sharpest turn per metre (radians/m) on the path within <paramref name="ahead"/> metres.</summary>
        private static float Bend(double[] p, double[] cum, double s, double ahead)
        {
            float worst = 0f;
            for (double a = 0; a < ahead; a += 4.0)
            {
                double x0, z0, x1, z1, x2, z2;
                At(p, cum, s + a, out x0, out z0);
                At(p, cum, s + a + 4.0, out x1, out z1);
                At(p, cum, s + a + 8.0, out x2, out z2);
                double h0 = Math.Atan2(x1 - x0, z1 - z0), h1 = Math.Atan2(x2 - x1, z2 - z1);
                float turn = Math.Abs(CharMath.WrapRad((float)(h1 - h0))) / 4f;
                if (turn > worst) worst = turn;
            }
            return worst;
        }
    }

    internal struct RideStats
    {
        public double RouteM, DrivenM;
        public float Seconds;
        public int WallHits, Bumps;
        public bool Stuck;
        public double StuckX, StuckZ;

        private string StuckAt()
        {
            double lon, lat;
            WorldFrame.GameToLonLat(StuckX, StuckZ, out lon, out lat);
            return lon.ToString("0.00000") + ", " + lat.ToString("0.00000");
        }

        public override string ToString()
        {
            return "route " + RouteM.ToString("0") + " m, drove " + DrivenM.ToString("0") + " m in " + Seconds.ToString("0.0") + " s, " + WallHits +
                   " wall hits, " + Bumps + " bumps" + (Stuck ? ", STUCK at " + StuckAt() : "");
        }
    }

    /// <summary>One camera frame for the motion metrics: position (game metres) and rotation.</summary>
    internal struct CamSample
    {
        public float Dt;
        public double X, Y, Z;
        public Quat Rot;
        public float VFovDeg;

        /// <summary>The boom length this frame (pivot to camera), metres.</summary>
        public float BoomM;

        /// <summary>The vehicle's heading this frame, degrees.</summary>
        public float TargetHeadingDeg;
    }

    /// <summary>Motion of a camera over a ride: position jerk, angular velocity and acceleration, boom steps.</summary>
    internal struct CamMotion
    {
        public double JerkP95, JerkP99, JerkMax;
        public double AngVelMax, AngVelStraightMax, AngVelStraightP95;

        /// <summary>Share of the straight-road frames in which the view turns faster than
        /// <see cref="StraightFastDegPerS"/>.</summary>
        public double StraightFastShare;

        public const double StraightFastDegPerS = 6.0;
        public double AngAccP99;
        public double BoomStepMax, OffsetStepMax;

        /// <summary>The fastest change of the boom's length and of the camera's offset from the vehicle as a share of the
        /// boom per 1/30 s (a frame at 30 fps; a long boom swings and moves farther for the same turn of the view, and a
        /// 50 ms hitch frame moves farther at the same speed).</summary>
        public double BoomStepRelMax, OffsetStepRelMax;

        /// <summary>Frames in which the boom's length changed by more than <see cref="BoomJumpShare"/> of it per 1/30 s
        /// (the hard limit cut it at a wall the early look did not see), and the minutes measured.</summary>
        public int BoomJumps;

        /// <summary>Runs of <see cref="BoomJumps"/> frames (one wall cutting the boom over a few frames is one cut).</summary>
        public int BoomCuts;

        public double Minutes;

        public const double BoomJumpShare = 0.03;
        public double RollMaxDeg;
        public int Frames;

        public override string ToString()
        {
            return "jerk p95 " + JerkP95.ToString("0.0") + " p99 " + JerkP99.ToString("0.0") + " max " + JerkMax.ToString("0") + " m/s³; ang vel max " +
                   AngVelMax.ToString("0.0") + " °/s, straight max " + AngVelStraightMax.ToString("0.00") + " p95 " + AngVelStraightP95.ToString("0.00") +
                   " °/s (" + (100 * StraightFastShare).ToString("0.00") + " % over 6); ang acc p99 " + AngAccP99.ToString("0") + " °/s²; boom step max " + BoomStepMax.ToString("0.00") + " m (" +
                   (100 * BoomStepRelMax).ToString("0.0") + " %, " + BoomJumps + " jumps in " + BoomCuts + " cuts), offset step max " + OffsetStepMax.ToString("0.00") + " m (" +
                   (100 * OffsetStepRelMax).ToString("0.0") + " %); roll max " + RollMaxDeg.ToString("0.0") + "°; " + Frames + " frames";
        }

        /// <summary>Shares of the boom are taken of at least this length, metres (a boom pulled in close to the rider).</summary>
        public const double MinBoomM = 4.0;

        /// <summary>Measures <paramref name="cam"/> (one sample per frame) after <paramref name="skipS"/> of warm-up. A frame
        /// counts as "straight" while the vehicle's heading moved less than 1.5° over the last 1.5 s.</summary>
        public static CamMotion Of(List<CamSample> cam, List<RideFrame> ride, float skipS = 1.5f)
        {
            int n = cam.Count;
            var t = new double[n];
            for (int i = 1; i < n; i++) t[i] = t[i - 1] + cam[i].Dt;
            var jerks = new List<double>();
            var angs = new List<double>();
            var straight = new List<double>();
            var angAcc = new List<double>();
            var m = new CamMotion();
            double prevW = double.NaN;
            int lastJump = -100;
            for (int i = 3; i < n; i++)
            {
                if (t[i - 3] < skipS) continue;
                m.Frames++;
                // Velocities between samples, accelerations at samples, jerk between accelerations (non-uniform steps).
                double v0x = (cam[i - 2].X - cam[i - 3].X) / cam[i - 2].Dt, v1x = (cam[i - 1].X - cam[i - 2].X) / cam[i - 1].Dt, v2x = (cam[i].X - cam[i - 1].X) / cam[i].Dt;
                double v0y = (cam[i - 2].Y - cam[i - 3].Y) / cam[i - 2].Dt, v1y = (cam[i - 1].Y - cam[i - 2].Y) / cam[i - 1].Dt, v2y = (cam[i].Y - cam[i - 1].Y) / cam[i].Dt;
                double v0z = (cam[i - 2].Z - cam[i - 3].Z) / cam[i - 2].Dt, v1z = (cam[i - 1].Z - cam[i - 2].Z) / cam[i - 1].Dt, v2z = (cam[i].Z - cam[i - 1].Z) / cam[i].Dt;
                double h1 = 0.5 * (cam[i - 2].Dt + cam[i - 1].Dt), h2 = 0.5 * (cam[i - 1].Dt + cam[i].Dt);
                double a1x = (v1x - v0x) / h1, a2x = (v2x - v1x) / h2, a1y = (v1y - v0y) / h1, a2y = (v2y - v1y) / h2, a1z = (v1z - v0z) / h1, a2z = (v2z - v1z) / h2;
                double hj = 0.5 * (h1 + h2);
                double jx = (a2x - a1x) / hj, jy = (a2y - a1y) / hj, jz = (a2z - a1z) / hj;
                jerks.Add(Math.Sqrt(jx * jx + jy * jy + jz * jz));

                float ang = Quat.Angle(cam[i - 1].Rot, cam[i].Rot);
                double w = ang / cam[i].Dt;
                angs.Add(w);
                if (!double.IsNaN(prevW)) angAcc.Add(Math.Abs(w - prevW) / cam[i].Dt);
                prevW = w;
                if (IsStraight(cam, t, i, 1.5, 1.5f)) straight.Add(w);

                double boomStep = Math.Abs(cam[i].BoomM - cam[i - 1].BoomM), boom = Math.Max(MinBoomM, Math.Max(cam[i].BoomM, cam[i - 1].BoomM));
                m.BoomStepMax = Math.Max(m.BoomStepMax, boomStep);
                double perFrame = (1.0 / 30.0) / Math.Max(1e-3, cam[i].Dt);
                m.BoomStepRelMax = Math.Max(m.BoomStepRelMax, boomStep / boom * perFrame);
                if (boomStep / boom * perFrame > BoomJumpShare)
                {
                    m.BoomJumps++;
                    if (i - lastJump > 3) m.BoomCuts++;
                    lastJump = i;
                }
                m.Minutes += cam[i].Dt / 60.0;
                // The camera's offset from the vehicle: a snap shows as a jump here.
                double ox = (cam[i].X - ride[i].X) - (cam[i - 1].X - ride[i - 1].X);
                double oy = (cam[i].Y - ride[i].Y) - (cam[i - 1].Y - ride[i - 1].Y);
                double oz = (cam[i].Z - ride[i].Z) - (cam[i - 1].Z - ride[i - 1].Z);
                double offsetStep = Math.Sqrt(ox * ox + oy * oy + oz * oz);
                m.OffsetStepMax = Math.Max(m.OffsetStepMax, offsetStep);
                m.OffsetStepRelMax = Math.Max(m.OffsetStepRelMax, offsetStep / boom * perFrame);
                V3 right = cam[i].Rot * V3.Right;
                m.RollMaxDeg = Math.Max(m.RollMaxDeg, Math.Abs(Math.Asin(Math.Max(-1f, Math.Min(1f, right.Y)))) * 180.0 / Math.PI);
            }
            m.JerkP95 = Pct(jerks, 0.95);
            m.JerkP99 = Pct(jerks, 0.99);
            m.JerkMax = Pct(jerks, 1.0);
            m.AngVelMax = Pct(angs, 1.0);
            m.AngVelStraightMax = Pct(straight, 1.0);
            m.AngVelStraightP95 = Pct(straight, 0.95);
            int fast = 0;
            foreach (double w in straight)
                if (w > StraightFastDegPerS) fast++;
            m.StraightFastShare = straight.Count > 0 ? (double)fast / straight.Count : 0.0;
            m.AngAccP99 = Pct(angAcc, 0.99);
            return m;
        }

        private static bool IsStraight(List<CamSample> cam, double[] t, int i, double windowS, float maxDeg)
        {
            float h = cam[i].TargetHeadingDeg;
            for (int k = i; k >= 0 && t[i] - t[k] <= windowS; k--)
            {
                float d = Math.Abs(CharMath.WrapRad((cam[k].TargetHeadingDeg - h) * Quat.Deg2Rad)) * Quat.Rad2Deg;
                if (d > maxDeg) return false;
                if (k == 0) return false;
            }
            return true;
        }

        private static double Pct(List<double> v, double q)
        {
            if (v.Count == 0) return 0.0;
            var a = v.ToArray();
            Array.Sort(a);
            int i = (int)Math.Min(a.Length - 1, Math.Floor(q * (a.Length - 1) + 0.5));
            return a[i];
        }
    }
}
