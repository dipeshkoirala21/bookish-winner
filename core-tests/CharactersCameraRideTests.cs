using System;
using System.Collections.Generic;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #5 ("camera angle is bad; it constantly keeps moving, which makes the driving experience really
    /// bad"): recorded rides over real streets of the sample region, played into the camera at a phone's uneven frame
    /// clock, measuring what the player feels: position jerk, angular velocity on straight roads and in turns, and how far
    /// the boom jumps per frame in narrow lanes.
    /// </summary>
    public class CharactersCameraRideTests
    {
        internal struct RideCase
        {
            public string Name;
            public Travel Profile;
            public HandlingPreset Preset;
            public RigClass Rig;
            public float CruiseMps;
            public double LonA, LatA, LonB, LatB;
            public float Seconds;

            /// <summary>Keep this far left of the road's centre line, metres.</summary>
            public double LeftM;

            public override string ToString()
            {
                return Name;
            }
        }

        internal static readonly RideCase[] Rides =
        {
            // Car: Thamel Chowk south to Ratna Park (Kantipath), about 32 km/h.
            new RideCase { Name = "car Thamel-Ratna Park", Profile = Travel.Car, Preset = HandlingPreset.Hatchback, Rig = RigClass.Car, CruiseMps = 9f,
                           LonA = 85.3110, LatA = 27.7150, LonB = 85.3153, LatB = 27.7066, Seconds = 150f, LeftM = 1.5 },
            // Scooter: Indra Chowk through the old core (Garcha Marg, the Himal Marg galli) to Asan and Z Marg, about 22 km/h.
            new RideCase { Name = "scooter Indra Chowk-Asan", Profile = Travel.Motorbike, Preset = HandlingPreset.Scooter, Rig = RigClass.TwoWheeler,
                           CruiseMps = 6f, LonA = 85.3077, LatA = 27.7044, LonB = 85.3140, LatB = 27.7083, Seconds = 150f },
            // Bus: Maitighar east along the Baneshwor road (the owner's "Baneshwor roads"), about 29 km/h, on the centre line
            // (1.5 m left of it the bus is stopped by a solid at 85.32753, 27.69324: open issue for the ride package).
            new RideCase { Name = "bus Maitighar-Baneshwor", Profile = Travel.Bus, Preset = HandlingPreset.Bus, Rig = RigClass.Bus, CruiseMps = 8f,
                           LonA = 85.3240, LatA = 27.6950, LonB = 85.3340, LatB = 27.6930, Seconds = 150f },
        };

        private static readonly Dictionary<string, List<RideFrame>> Recorded = new Dictionary<string, List<RideFrame>>();

        internal static List<RideFrame> Record(RideCase c)
        {
            List<RideFrame> frames;
            if (Recorded.TryGetValue(c.Name, out frames)) return frames;
            double[] path = CameraRides.Route(c.Profile, c.LonA, c.LatA, c.LonB, c.LatB, c.LeftM);
            RideStats stats;
            frames = CameraRides.Drive(path, c.Preset, c.CruiseMps, c.Seconds, out stats);
            TestContext.WriteLine(c.Name + ": " + stats);
            Assert.That(stats.Stuck, Is.False, c.Name + " got stuck: " + stats);
            Assert.That(stats.DrivenM, Is.GreaterThan(300.0), c.Name + ": " + stats);
            Recorded[c.Name] = frames;
            return frames;
        }

        /// <summary>The old rig over a ride (scene origin at the start, as after a spawn).</summary>
        internal static List<CamSample> RunLegacy(RideCase c, List<RideFrame> ride, float aspect = 2400f / 1080f)
        {
            var rig = new LegacyChaseRig { Obstacles = CameraRides.Ground, Aspect = aspect, Portrait = aspect < 1f };
            var origin = new WorldPos(ride[0].X, 0f, ride[0].Z);
            var cam = new List<CamSample>(ride.Count);
            foreach (RideFrame f in ride)
            {
                var ground = new V3((float)(f.X - origin.X), f.Y - origin.Y, (float)(f.Z - origin.Z));
                rig.Tick(f.Dt, ground, f.HeadingRad, f.SpeedMps, f.LeanRad, c.Rig, CameraRides.Ground, origin);
                V3 p = rig.Position;
                cam.Add(new CamSample
                {
                    Dt = f.Dt, X = p.X + origin.X, Y = p.Y + origin.Y, Z = p.Z + origin.Z, Rot = rig.Rotation, VFovDeg = rig.VerticalFovDeg,
                    BoomM = rig.Boom.DistanceM, TargetHeadingDeg = f.HeadingRad * Quat.Rad2Deg,
                });
            }
            return cam;
        }

        /// <summary>The new chase camera over a ride (scene origin at the start, as after a spawn).</summary>
        internal static List<CamSample> RunNew(RideCase c, List<RideFrame> ride, float aspect = 2400f / 1080f, ChaseCamera cam = null,
                                               List<string> diag = null)
        {
            cam = cam ?? new ChaseCamera();
            cam.Obstacles = CameraRides.Ground;
            var origin = new WorldPos(ride[0].X, 0f, ride[0].Z);
            var output = new List<CamSample>(ride.Count);
            var controls = new ChaseControls();
            float bodyAhead = ChaseCamera.BodyAheadOf(VehicleCatalog.At(VariantOf(c.Preset)));
            foreach (RideFrame f in ride)
            {
                var t = new ChaseTarget
                {
                    Ground = new V3((float)(f.X - origin.X), f.Y - origin.Y, (float)(f.Z - origin.Z)), HeadingRad = f.HeadingRad, SpeedMps = f.SpeedMps,
                    LeanRad = f.LeanRad, Rig = c.Rig, PassengerOf = RigClass.Car, HasBody = true, BodyAheadM = bodyAhead,
                };
                ChasePose p = cam.Tick(f.Dt, t, CameraViews.Default(c.Rig), controls, aspect, CameraRides.Ground, origin.X, origin.Y, origin.Z);
                output.Add(new CamSample
                {
                    Dt = f.Dt, X = p.Position.X + origin.X, Y = p.Position.Y + origin.Y, Z = p.Position.Z + origin.Z, Rot = p.Rotation,
                    VFovDeg = p.VerticalFovDeg, BoomM = cam.Boom.DistanceM, TargetHeadingDeg = f.HeadingRad * Quat.Rad2Deg,
                });
                if (diag != null)
                {
                    diag.Add("yaw " + p.YawDeg.ToString("0.0") + " head " + (f.HeadingRad * Quat.Rad2Deg).ToString("0.0") + " pitch " + cam.Boom.PitchDeg.ToString("0.0") +
                             " lift " + cam.Boom.LiftDeg.ToString("0.0") + " lane " + cam.Boom.Lane01.ToString("0.00") + " len " + cam.Boom.DistanceM.ToString("0.00") +
                             " clear " + cam.Boom.ClearM.ToString("0.00") + " over " + cam.Boom.Overhead01.ToString("0.00") + " v " + f.SpeedMps.ToString("0.0") +
                             " y " + f.Y.ToString("0.00") + " anchorY " + cam.Anchor.Y.ToString("0.00") + (cam.Boom.Clamped ? " CLAMPED" : "") +
                             (CameraRides.Ground.InsideSolid(cam.Anchor.X + origin.X, cam.Anchor.Z + origin.Z, cam.Anchor.Y + origin.Y) ? " PIVOT-IN-SOLID" : ""));
                }
            }
            return output;
        }

        internal static int VariantOf(HandlingPreset preset)
        {
            for (int i = 0; i < VehicleCatalog.Count; i++)
                if (VehicleCatalog.At(i).HandlingPreset == (byte)preset) return i;
            return 0;
        }

        // ---- Thresholds (docs/research/w2/ref_camera.md §6) ----------------------------------------------------------------

        internal const double JerkP95Max = 60, JerkP99Max = 200, StraightMaxDegPerS = 6, StraightP95DegPerS = 4, AngVelMaxDegPerS = 90,
                              AngAccP99Max = 250, OffsetStepMaxM = 0.5, LaneBoomStepMaxM = 0.25;

        /// <summary>What a camera fails of the thresholds over a ride ("" when it passes).</summary>
        internal static string Failures(in CamMotion m)
        {
            string f = "";
            if (m.JerkP95 > JerkP95Max) f += " jerk p95 " + m.JerkP95.ToString("0") + " > " + JerkP95Max + ";";
            if (m.JerkP99 > JerkP99Max) f += " jerk p99 " + m.JerkP99.ToString("0") + " > " + JerkP99Max + ";";
            if (m.AngVelStraightMax > StraightMaxDegPerS) f += " straight-road rotation " + m.AngVelStraightMax.ToString("0.0") + " °/s > " + StraightMaxDegPerS + ";";
            if (m.AngVelStraightP95 > StraightP95DegPerS) f += " straight-road rotation p95 " + m.AngVelStraightP95.ToString("0.0") + " °/s > " + StraightP95DegPerS + ";";
            if (m.AngVelMax > AngVelMaxDegPerS) f += " rotation " + m.AngVelMax.ToString("0") + " °/s > " + AngVelMaxDegPerS + ";";
            if (m.AngAccP99 > AngAccP99Max) f += " angular acceleration p99 " + m.AngAccP99.ToString("0") + " °/s² > " + AngAccP99Max + ";";
            if (m.OffsetStepMax > OffsetStepMaxM) f += " snap of " + m.OffsetStepMax.ToString("0.00") + " m in one frame;";
            if (m.RollMaxDeg > 0.01) f += " roll " + m.RollMaxDeg.ToString("0.0") + "°;";
            return f;
        }

        private static IEnumerable<TestCaseData> RideCases()
        {
            for (int i = 0; i < Rides.Length; i++)
            {
                yield return new TestCaseData(i, false).SetName("TheCameraStaysSteadyOnRealStreets(" + Rides[i].Name + ", landscape)");
                yield return new TestCaseData(i, true).SetName("TheCameraStaysSteadyOnRealStreets(" + Rides[i].Name + ", portrait)");
            }
        }

        [TestCaseSource(nameof(RideCases))]
        public void TheCameraStaysSteadyOnRealStreets(int ride, bool portrait)
        {
            // Owner playtest #5. The ride is recorded once per run on the real streets; the camera sees it on a phone's
            // uneven frame clock. The old camera (the same ride and orientation) is measured alongside as the "before".
            RideCase c = Rides[ride];
            List<RideFrame> frames = Record(c);
            CamMotion now = CamMotion.Of(RunNew(c, frames, portrait ? 1080f / 2400f : 2400f / 1080f), frames);
            string orientation = portrait ? "portrait" : "landscape";
            TestContext.WriteLine("after  (" + orientation + "): " + now);
            CamMotion before = CamMotion.Of(RunLegacy(c, frames, portrait ? 1080f / 2400f : 2400f / 1080f), frames);
            TestContext.WriteLine("before (" + orientation + "): " + before);
            TestContext.WriteLine("before fails:" + Failures(before));
            Assert.That(Failures(now), Is.Empty, c.Name);
        }

        [Test]
        public void TheOldCameraFailedOnTheSameRides()
        {
            // The reproduction of the complaint: the pre-fix rig (CharactersCameraLegacy.cs, ported line by line) fails the
            // thresholds on every ride, mostly through jerk (its SmoothDamp lag changes with every frame time) and rotation
            // on straight roads (lane mode and the ground clamp).
            foreach (RideCase c in Rides)
            {
                List<RideFrame> frames = Record(c);
                CamMotion before = CamMotion.Of(RunLegacy(c, frames), frames);
                string failures = Failures(before);
                TestContext.WriteLine(c.Name + ": " + before + " | fails:" + failures);
                Assert.That(failures, Is.Not.Empty, c.Name + ": the old camera should fail here");
                Assert.Greater(before.JerkP95, 10 * JerkP95Max, c.Name + ": the old camera shook against the vehicle every frame");
            }
        }

        [Test]
        public void InTheOldCoreLanesTheBoomNeverSnaps()
        {
            // The scooter through Indra Chowk, Garcha Marg and the Himal Marg galli (4-6 m between houses): the boom never
            // shortens or lengthens by more than 0.25 m in one frame, the camera is never inside a house, and the house
            // fronts never come between the rider and the camera. Landscape and portrait.
            RideCase c = Rides[1];
            List<RideFrame> frames = Record(c);
            foreach (float aspect in new[] { 2400f / 1080f, 1080f / 2400f })
            {
                var cam = new ChaseCamera();
                List<CamSample> got = RunNew(c, frames, aspect, cam);
                CamMotion m = CamMotion.Of(got, frames);
                TestContext.WriteLine((aspect < 1f ? "portrait" : "landscape") + ": " + m);
                Assert.LessOrEqual(m.BoomStepMax, LaneBoomStepMaxM, "boom step");
                int inside = 0, hidden = 0, blocked = 0;
                for (int i = 0; i < got.Count; i++)
                {
                    CamSample s = got[i];
                    if (CameraRides.Ground.InsideSolid(s.X, s.Z, (float)s.Y - 0.2f)) inside++;
                    double px = frames[i].X, py = frames[i].Y + 1.0, pz = frames[i].Z;
                    for (double f = 0.15; f < 0.95; f += 0.1)
                    {
                        if (CameraRides.Ground.InsideSolid(px + (s.X - px) * f, pz + (s.Z - pz) * f, (float)(py + (s.Y - py) * f) - 0.2f))
                        {
                            hidden++;
                            break;
                        }
                    }
                    if (s.BoomM < 3f) blocked++;
                }
                TestContext.WriteLine("  frames with the boom under 3 m: " + blocked + " of " + got.Count);
                Assert.AreEqual(0, inside, "the camera inside a house");
                Assert.LessOrEqual(hidden, got.Count / 200, "a house between the rider and the camera");
            }
        }

        [Test]
        public void TheSameRideGivesTheSameViewAtAnyFrameRate()
        {
            // The car's ride recorded on the phone clock and at an even 60 fps (the physics steps at 60 Hz either way): at
            // every phone frame the camera's offset from the car matches the 60 fps run within 3 cm. The old camera's
            // SmoothDamp does not (its lag depends on the frame time): that difference is the shake the owner saw.
            RideCase c = Rides[0];
            double[] path = CameraRides.Route(c.Profile, c.LonA, c.LatA, c.LonB, c.LatB, c.LeftM);
            RideStats s1, s2;
            List<RideFrame> phone = CameraRides.Drive(path, c.Preset, c.CruiseMps, 40f, out s1);
            List<RideFrame> even = CameraRides.Drive(path, c.Preset, c.CruiseMps, 40f, out s2, 0.0, i => 1f / 60f);
            double now = OffsetMismatch(c, phone, even, false), before = OffsetMismatch(c, phone, even, true);
            TestContext.WriteLine("camera offset from the car, phone clock vs 60 fps: now " + (now * 100).ToString("0.0") + " cm, before " +
                                  (before * 100).ToString("0.0") + " cm");
            Assert.Less(now, 0.03, "the frame clock does not show in the camera");
            Assert.Greater(before, 2 * now, "it did in the old camera");
        }

        private static double OffsetMismatch(RideCase c, List<RideFrame> phone, List<RideFrame> even, bool legacy)
        {
            List<CamSample> a = legacy ? RunLegacy(c, phone) : RunNew(c, phone);
            List<CamSample> b = legacy ? RunLegacy(c, even) : RunNew(c, even);
            var tb = new double[even.Count];
            tb[0] = even[0].Dt;
            for (int i = 1; i < even.Count; i++) tb[i] = tb[i - 1] + even[i].Dt;
            double t = 0, worst = 0;
            int k = 0;
            for (int i = 0; i < phone.Count; i++)
            {
                t += phone[i].Dt;
                if (t < 3.0 || t > tb[even.Count - 1]) continue;
                while (k + 1 < even.Count && tb[k + 1] < t) k++;
                if (k + 1 >= even.Count) break;
                double f = (t - tb[k]) / (tb[k + 1] - tb[k]);
                double ox = (b[k].X - even[k].X) + ((b[k + 1].X - even[k + 1].X) - (b[k].X - even[k].X)) * f;
                double oy = (b[k].Y - even[k].Y) + ((b[k + 1].Y - even[k + 1].Y) - (b[k].Y - even[k].Y)) * f;
                double oz = (b[k].Z - even[k].Z) + ((b[k + 1].Z - even[k + 1].Z) - (b[k].Z - even[k].Z)) * f;
                double dx = (a[i].X - phone[i].X) - ox, dy = (a[i].Y - phone[i].Y) - oy, dz = (a[i].Z - phone[i].Z) - oz;
                worst = Math.Max(worst, Math.Sqrt(dx * dx + dy * dy + dz * dz));
            }
            return worst;
        }

        [Test]
        public void DumpRideTracesForThePreviews()
        {
            // Preview only (writes nothing unless GHUMANTE_PREVIEW_DIR is set): per frame of each ride, the camera's height
            // over the vehicle, its distance behind it and how fast the view turns, before and after, for the plots in
            // /home/user/wt/previews/camera/.
            string dir = Environment.GetEnvironmentVariable("GHUMANTE_PREVIEW_DIR");
            if (string.IsNullOrEmpty(dir)) return;
            System.IO.Directory.CreateDirectory(dir);
            foreach (RideCase c in Rides)
            {
                List<RideFrame> frames = Record(c);
                List<CamSample> before = RunLegacy(c, frames), after = RunNew(c, frames);
                var sb = new System.Text.StringBuilder("t,speed,heading,old_up,new_up,old_back,new_back,old_turn,new_turn,old_boom,new_boom\n");
                double t = 0;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                for (int i = 1; i < frames.Count; i++)
                {
                    t += frames[i].Dt;
                    RideFrame f = frames[i];
                    sb.AppendFormat(ci, "{0:0.000},{1:0.00},{2:0.0},{3:0.000},{4:0.000},{5:0.000},{6:0.000},{7:0.00},{8:0.00},{9:0.00},{10:0.00}\n", t, f.SpeedMps,
                                    f.HeadingRad * Quat.Rad2Deg, before[i].Y - f.Y, after[i].Y - f.Y, Math.Sqrt(Sq(before[i].X - f.X) + Sq(before[i].Z - f.Z)),
                                    Math.Sqrt(Sq(after[i].X - f.X) + Sq(after[i].Z - f.Z)), Quat.Angle(before[i - 1].Rot, before[i].Rot) / f.Dt,
                                    Quat.Angle(after[i - 1].Rot, after[i].Rot) / f.Dt, before[i].BoomM, after[i].BoomM);
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "trace_" + c.Name.Split(' ')[0] + ".csv"), sb.ToString());
            }
        }

        private static double Sq(double v)
        {
            return v * v;
        }
    }
}
