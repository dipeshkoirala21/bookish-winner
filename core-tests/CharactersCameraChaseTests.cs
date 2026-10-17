using System;
using System.Collections.Generic;
using Ghumante.Core.Characters;
using Ghumante.Core.Driving;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The engine-free chase camera (<see cref="ChaseCamera"/>, docs/research/w2/ref_camera.md) on synthetic rides: the
    /// framing of every class in both orientations, what it must ignore (bumps, kerbs, the lean, physics steps, parking),
    /// what it must follow (hills, turns, the flight path), look-around, rebases and teleports.
    /// </summary>
    public class CharactersCameraChaseTests
    {
        private const float Landscape = 2400f / 1080f, Wide = 16f / 9f, Portrait = 1080f / 2340f;

        /// <summary>Open flat ground at 1300 m.</summary>
        private static readonly PlaneGround Flat = new PlaneGround { H0 = 1300.0 };

        private struct Frame
        {
            public ChasePose Pose;
            public V3 Ground;
            public float Heading;
        }

        /// <summary>Drives the camera along <paramref name="path"/> (time → ground point, heading in radians, speed) for
        /// <paramref name="seconds"/> on the phone frame clock; origin at the world origin.</summary>
        private static List<Frame> Run(ChaseCamera cam, RigClass rig, float aspect, float seconds,
                                       Func<double, (V3 ground, float heading, float speed)> path, CameraView view = CameraView.Near,
                                       float bodyAhead = -1f, IGroundQuery ground = null, Func<int, ChaseControls> controls = null)
        {
            var frames = new List<Frame>();
            double t = 0;
            for (int i = 0; t < seconds; i++)
            {
                float dt = CameraRides.FrameDt(i);
                t += dt;
                var p = path(t);
                var target = new ChaseTarget
                {
                    Ground = p.ground, HeadingRad = p.heading, SpeedMps = p.speed, Rig = rig, PassengerOf = RigClass.Car, HasBody = bodyAhead >= 0f,
                    BodyAheadM = Math.Max(0f, bodyAhead),
                };
                ChaseControls c = controls != null ? controls(i) : new ChaseControls();
                ChasePose pose = cam.Tick(dt, target, view, c, aspect, ground ?? Flat, 0, 0, 0);
                frames.Add(new Frame { Pose = pose, Ground = p.ground, Heading = p.heading });
            }
            return frames;
        }

        /// <summary>Screen height (NDC) of a world point seen by a pose.</summary>
        private static float NdcY(in ChasePose pose, V3 point)
        {
            V3 rel = point - pose.Position;
            V3 local = pose.Rotation.Inverse * rel;
            float tanHalf = MathF.Tan(pose.VerticalFovDeg * 0.5f * Quat.Deg2Rad);
            return local.Y / (local.Z * tanHalf);
        }

        private static float PitchDeg(Quat q)
        {
            V3 f = q * V3.Forward;
            return MathF.Asin(CharMath.Clamp(-f.Y, -1f, 1f)) * Quat.Rad2Deg;
        }

        private static float RollDeg(Quat q)
        {
            V3 r = q * V3.Right;
            return MathF.Asin(CharMath.Clamp(r.Y, -1f, 1f)) * Quat.Rad2Deg;
        }

        // ---- Framing -------------------------------------------------------------------------------------------------

        internal struct Body
        {
            public RigClass Rig;
            public float LengthM, HeightM, RearOverhangM, TailNdc, Share, Horizon, TailNdcP, ShareP, HorizonP;
        }

        private static readonly Body[] Bodies =
        {
            new Body { Rig = RigClass.Walk, LengthM = 0.4f, HeightM = 1.75f, RearOverhangM = 0.2f, TailNdc = -0.58f, Share = 0.33f, Horizon = 0.30f, TailNdcP = -0.46f, ShareP = 0.14f, HorizonP = 0.15f },
            new Body { Rig = RigClass.TwoWheeler, LengthM = 1.85f, HeightM = 1.72f, RearOverhangM = 0.32f, TailNdc = -0.56f, Share = 0.29f, Horizon = 0.30f, TailNdcP = -0.48f, ShareP = 0.115f, HorizonP = 0.15f },
            new Body { Rig = RigClass.Car, LengthM = 3.8f, HeightM = 1.5f, RearOverhangM = 0.6f, TailNdc = -0.55f, Share = 0.26f, Horizon = 0.28f, TailNdcP = -0.48f, ShareP = 0.095f, HorizonP = 0.15f },
            new Body { Rig = RigClass.Van, LengthM = 4.6f, HeightM = 1.95f, RearOverhangM = 0.8f, TailNdc = -0.56f, Share = 0.28f, Horizon = 0.28f, TailNdcP = -0.48f, ShareP = 0.10f, HorizonP = 0.15f },
            new Body { Rig = RigClass.Bus, LengthM = 10.5f, HeightM = 3.2f, RearOverhangM = 2.6f, TailNdc = -0.58f, Share = 0.30f, Horizon = 0.26f, TailNdcP = -0.50f, ShareP = 0.12f, HorizonP = 0.13f },
        };

        [Test]
        public void EveryClassFramesItsBodyAsTheBriefSays()
        {
            // At rest on flat ground: where the tail stands on screen, how much of the screen height the body takes and
            // where the horizon runs (ref_camera.md §4), on a 20:9 and a 16:9 landscape phone and a 9:19.5 portrait one.
            foreach (Body b in Bodies)
            {
                foreach (float aspect in new[] { Landscape, Wide, Portrait })
                {
                    var cam = new ChaseCamera();
                    float ahead = Math.Max(0f, 0.5f * b.LengthM - b.RearOverhangM);
                    List<Frame> f = Run(cam, b.Rig, aspect, 1f, t => (new V3(0f, 1300f, 0f), 0f, 0f), CameraView.Near, b.Rig == RigClass.Walk ? 0f : ahead);
                    ChasePose p = f[f.Count - 1].Pose;
                    float tailZ = b.Rig == RigClass.Walk ? -0.2f : -b.RearOverhangM;
                    float tail = NdcY(p, new V3(0f, 1300f, tailZ)), top = NdcY(p, new V3(0f, 1300f + b.HeightM, tailZ));
                    float horizon = NdcY(p, new V3(p.Position.X, p.Position.Y, p.Position.Z + 1e5f));
                    bool portrait = aspect < 1f;
                    string what = b.Rig + (portrait ? " portrait" : " landscape " + aspect.ToString("0.00")) + ": tail " + tail.ToString("0.00") + ", share " +
                                  ((top - tail) / 2f).ToString("0.000") + ", horizon " + horizon.ToString("0.00") + ", camera " + p.Position.Y.ToString("0.0") + " m up";
                    TestContext.WriteLine(what);
                    Assert.AreEqual(portrait ? b.TailNdcP : b.TailNdc, tail, 0.06f, what);
                    Assert.AreEqual(portrait ? b.ShareP : b.Share, (top - tail) / 2f, portrait ? 0.025f : 0.05f, what);
                    Assert.AreEqual(portrait ? b.HorizonP : b.Horizon, horizon, 0.07f, what);
                    Assert.AreEqual(0f, RollDeg(p.Rotation), 1e-3f, "the horizon is level");
                }
            }
        }

        [Test]
        public void LandscapeWidensSidewaysAndPortraitKeepsTheMinimumWidth()
        {
            foreach (RigClass rig in new[] { RigClass.Walk, RigClass.TwoWheeler, RigClass.Car, RigClass.Bus })
            {
                RigParams l = CameraRigTable.For(rig, false), p = CameraRigTable.For(rig, true);
                float vWide = CameraRigTable.VerticalFovDeg(l, 16f / 9f, 0f), vPhone = CameraRigTable.VerticalFovDeg(l, 20f / 9f, 0f);
                Assert.AreEqual(vWide, vPhone, 0.01f, rig + ": a wider landscape phone keeps the vertical FOV (Hor+)");
                Assert.That(vPhone, Is.InRange(45f, 49f), rig + " landscape vertical FOV");
                float vTall = CameraRigTable.VerticalFovDeg(p, 9f / 19.5f, 0f);
                double h = 2 * Math.Atan(Math.Tan(vTall * Math.PI / 360) * (9 / 19.5)) * 180 / Math.PI;
                Assert.IsTrue(vTall >= 99.99f || h >= p.MinHFovDeg - 0.1, rig + " portrait keeps its minimum horizontal FOV");
                Assert.Greater(CameraRigTable.VerticalFovDeg(l, 20f / 9f, 5f), vPhone, "the speed kick widens it");
            }
        }

        // ---- Steady ----------------------------------------------------------------------------------------------------

        [Test]
        public void BumpsSeamsAndTheLeanNeverShakeTheView()
        {
            // A scooter at 8 m/s on a straight road whose surface jumps ±4 cm every few centimetres (seams, patched
            // asphalt), leaning ±20° (weaving): the view's pitch and roll stay put and the camera barely moves up and down.
            var cam = new ChaseCamera();
            List<Frame> f = Run(cam, RigClass.TwoWheeler, Landscape, 8f, t =>
            {
                float bump = 0.04f * (CharMath.Unit(CharMath.Hash((uint)(t * 8 / 0.07))) * 2f - 1f);
                return (new V3(0f, 1300f + bump, (float)(8 * t)), 0f, 8f);
            });
            var ys = new List<float>();
            float pitchMin = 999f, pitchMax = -999f, rollMax = 0f;
            for (int i = 60; i < f.Count; i++)
            {
                ChasePose p = f[i].Pose;
                ys.Add(p.Position.Y);
                pitchMin = Math.Min(pitchMin, PitchDeg(p.Rotation));
                pitchMax = Math.Max(pitchMax, PitchDeg(p.Rotation));
                rollMax = Math.Max(rollMax, Math.Abs(RollDeg(p.Rotation)));
            }
            float yMin = float.MaxValue, yMax = float.MinValue;
            foreach (float y in ys)
            {
                yMin = Math.Min(yMin, y);
                yMax = Math.Max(yMax, y);
            }
            TestContext.WriteLine("camera height range " + ((yMax - yMin) * 100).ToString("0.0") + " cm, pitch range " + (pitchMax - pitchMin).ToString("0.00") + "°");
            Assert.Less(yMax - yMin, 0.03f, "8 cm of surface noise moves the camera less than 3 cm");
            Assert.Less(pitchMax - pitchMin, 0.5f, "and tilts the view less than half a degree");
            Assert.AreEqual(0f, rollMax, 1e-3f, "no roll");

            // The lean (the target's LeanRad) is not used at all by the chase view.
            var a = new ChaseCamera();
            var b = new ChaseCamera();
            for (int i = 0; i < 200; i++)
            {
                var ta = new ChaseTarget { Ground = new V3(0f, 1300f, i * 0.2f), SpeedMps = 6f, Rig = RigClass.TwoWheeler, LeanRad = 0.5f * MathF.Sin(i * 0.2f) };
                var tb = ta;
                tb.LeanRad = 0f;
                ChasePose pa = a.Tick(1f / 30f, ta, CameraView.Near, new ChaseControls(), Landscape, Flat, 0, 0, 0);
                ChasePose pb = b.Tick(1f / 30f, tb, CameraView.Near, new ChaseControls(), Landscape, Flat, 0, 0, 0);
                Assert.AreEqual(0f, Quat.Angle(pa.Rotation, pb.Rotation), 1e-3f);
            }
        }

        [Test]
        public void AKerbIsTakenInSmoothly()
        {
            // A car rides up a 15 cm kerb ramp in one frame: the camera follows over about a third of a second, never by
            // more than a couple of centimetres a frame, and the view does not jolt.
            var cam = new ChaseCamera();
            List<Frame> f = Run(cam, RigClass.Car, Landscape, 4f, t => (new V3(0f, t > 2 ? 1300.15f : 1300f, (float)(6 * t)), 0f, 6f));
            float worstStep = 0f, worstTurn = 0f;
            for (int i = 1; i < f.Count; i++)
            {
                worstStep = Math.Max(worstStep, Math.Abs(f[i].Pose.Position.Y - f[i - 1].Pose.Position.Y));
                worstTurn = Math.Max(worstTurn, Quat.Angle(f[i].Pose.Rotation, f[i - 1].Pose.Rotation));
            }
            Assert.Less(worstStep, 0.025f, "no vertical jump");
            Assert.Less(worstTurn, 0.15f, "no jolt of the view");
            Assert.AreEqual(1300.15f + 2.36f, f[f.Count - 1].Pose.Position.Y, 0.12f, "settled on the new level");
        }

        [Test]
        public void HillsAreFollowedWithoutLag()
        {
            // A 12 % hill at 7 m/s: the camera keeps its height over the road (the climb is fed forward), so the rider stays
            // put on screen while climbing.
            var hill = new PlaneGround { H0 = 1300.0, GradeZ = 0.12 };
            var cam = new ChaseCamera();
            List<Frame> f = Run(cam, RigClass.TwoWheeler, Landscape, 6f, t => (new V3(0f, (float)(1300 + 0.12 * 7 * t), (float)(7 * t)), 0f, 7f), CameraView.Near,
                                -1f, hill);
            V3 anchor = cam.Anchor;
            Frame last = f[f.Count - 1];
            Assert.AreEqual(last.Ground.Y, anchor.Y, 0.03f, "the followed point is on the road, not trailing below it");
            float foot = NdcY(last.Pose, last.Ground + new V3(0f, 0f, 0.6f));
            Assert.AreEqual(CameraRigTable.FootNdc(CameraRigTable.For(RigClass.TwoWheeler, false), 7f), foot, 0.06f, "the rider stays at the framing height");
        }

        [Test]
        public void TheYawLagsATurnAndRecentres()
        {
            // A car turning at 30°/s for 3 s, then straight: the camera trails the turn by about rate × follow time (shows
            // the inside of the bend), then comes back behind the car without overshooting.
            var cam = new ChaseCamera();
            const float rate = 30f;
            List<Frame> f = Run(cam, RigClass.Car, Landscape, 7f, t =>
            {
                double turn = t < 1 ? 0 : t < 4 ? rate * (t - 1) : rate * 3;
                return (new V3(0f, 1300f, 0f), (float)(turn * Math.PI / 180), 8f);
            });
            float lagMid = 0f, overshoot = 0f;
            double t2 = 0;
            for (int i = 0; i < f.Count; i++)
            {
                t2 += CameraRides.FrameDt(i);
                float heading = f[i].Heading * Quat.Rad2Deg;
                float lag = CameraAngleSpring.Delta(f[i].Pose.YawDeg, heading);
                if (t2 > 3.5 && t2 < 3.9) lagMid = Math.Max(lagMid, lag);
                if (t2 > 4.2) overshoot = Math.Min(overshoot, lag);
            }
            float follow = CameraRigTable.For(RigClass.Car, false).FollowS;
            Assert.AreEqual(rate * follow, lagMid, rate * follow * 0.3f, "trails the turn by rate × follow time");
            Assert.Greater(overshoot, -0.5f, "never swings past the car");
            Assert.AreEqual(f[f.Count - 1].Heading * Quat.Rad2Deg, f[f.Count - 1].Pose.YawDeg, 0.5f, "back behind it");
        }

        [Test]
        public void ParkingDoesNotSwingTheCamera()
        {
            // Creeping at 0.4 m/s while turning 90° (parking): the camera stays where it is instead of swinging round.
            var cam = new ChaseCamera();
            List<Frame> f = Run(cam, RigClass.Car, Landscape, 4f, t => (new V3(0f, 1300f, 0f), (float)(Math.Min(1.0, t / 3) * Math.PI / 2), 0.4f));
            Assert.Less(Math.Abs(CameraAngleSpring.Delta(0f, f[f.Count - 1].Pose.YawDeg)), 5f);
        }

        [Test]
        public void LookAroundSpringsBackAfterAPause()
        {
            var cam = new ChaseCamera();
            List<Frame> f = Run(cam, RigClass.TwoWheeler, Landscape, 5f, t => (new V3(0f, 1300f, (float)(6 * t)), 0f, 6f), CameraView.Near, -1f, null,
                                i => new ChaseControls { LookYawDeg = i >= 30 && i < 40 ? 6f : 0f, Looking = i >= 30 && i < 40 });
            double t2 = 0;
            for (int i = 0; i < f.Count; i++)
            {
                t2 += CameraRides.FrameDt(i);
                if (i == 41) Assert.AreEqual(60f, f[i].Pose.YawDeg, 1f, "the glance");
                if (t2 > 1.4 + 1.3 && t2 < 2.6) Assert.Greater(f[i].Pose.YawDeg, 40f, "held while the player might look again");
            }
            Assert.AreEqual(0f, f[f.Count - 1].Pose.YawDeg, 1f, "back behind the rider");
        }

        [Test]
        public void ARebaseChangesNothingAndATeleportSnaps()
        {
            // The same straight ride, once with the floating origin moved by 6 km halfway: the camera in game metres is the
            // same within a centimetre.
            var a = new ChaseCamera();
            var b = new ChaseCamera();
            double ox = 0, oz = 0;
            for (int i = 0; i < 240; i++)
            {
                float dt = CameraRides.FrameDt(i);
                double gz = i * 0.25;
                if (i == 120)
                {
                    b.ShiftOrigin(6000.0, 0.0, -4000.0);
                    ox = 6000.0;
                    oz = -4000.0;
                }
                var ta = new ChaseTarget { Ground = new V3(0f, 1300f, (float)gz), SpeedMps = 7f, Rig = RigClass.Car };
                var tb = new ChaseTarget { Ground = new V3((float)(0 - ox), 1300f, (float)(gz - oz)), SpeedMps = 7f, Rig = RigClass.Car };
                ChasePose pa = a.Tick(dt, ta, CameraView.Near, new ChaseControls(), Landscape, Flat, 0, 0, 0);
                ChasePose pb = b.Tick(dt, tb, CameraView.Near, new ChaseControls(), Landscape, Flat, ox, 0, oz);
                Assert.AreEqual(pa.Position.X, pb.Position.X + ox, 0.01, "frame " + i);
                Assert.AreEqual(pa.Position.Z, pb.Position.Z + oz, 0.01, "frame " + i);
                Assert.AreEqual(pa.Position.Y, pb.Position.Y, 0.01, "frame " + i);
            }

            // A teleport of 200 m: the camera is there at once, behind the car, not swooping across the city.
            var c = new ChaseCamera();
            ChasePose p = default(ChasePose);
            for (int i = 0; i < 40; i++)
            {
                float z = i < 20 ? i * 0.2f : 200f + (i - 20) * 0.2f;
                p = c.Tick(1f / 30f, new ChaseTarget { Ground = new V3(0f, 1300f, z), SpeedMps = 6f, Rig = RigClass.Car }, CameraView.Near, new ChaseControls(),
                           Landscape, Flat, 0, 0, 0);
                if (i == 20) Assert.Greater(p.Position.Z, 185f, "at the new place on the first frame");
            }
        }

        [Test]
        public void AnAircraftsViewTiltsWithTheClimbAndBanksOnlyALittle()
        {
            // A Cessna-sized aircraft (8.3 m) climbing at 10 % at 40 m/s and banked 30°: the boom looks along the climb
            // (lower behind the tail than in level flight) and the view rolls 20 % of the bank; with Reduce motion not at all.
            ChasePose Fly(bool climb, float bankDeg, bool reduced)
            {
                var cam = new ChaseCamera { ReducedMotion = reduced };
                ChasePose pose = default(ChasePose);
                for (int i = 0; i < 150; i++)
                {
                    float t = i / 30f;
                    var target = new ChaseTarget
                    {
                        Ground = new V3(0f, 1400f + (climb ? 4f * t : 0f), 40f * t), SpeedMps = 40f, Rig = RigClass.Car, Aircraft = true,
                        AircraftLengthM = 8.3f, BankRad = bankDeg * Quat.Deg2Rad,
                    };
                    pose = cam.Tick(1f / 30f, target, CameraView.Near, new ChaseControls(), Landscape, null, 0, 0, 0);
                }
                return pose;
            }
            ChasePose level = Fly(false, 0f, false), climbing = Fly(true, 0f, false);
            float levelAbove = level.Position.Y - 1400f, climbAbove = climbing.Position.Y - (1400f + 4f * 149f / 30f);
            Assert.Less(climbAbove, levelAbove - 0.5f, "climbing, the camera drops behind the tail to look along the climb");
            RigParams r = CameraRigTable.ForAircraft(8.3f, false);
            Assert.That(r.DistanceM, Is.InRange(15f, 18f), "about 7 m + 1.15 × the length behind");
            Assert.AreEqual(-30f * CameraRigTable.AircraftBankShare, RollDeg(Fly(false, 30f, false).Rotation), 0.5f, "a little of the bank");
            Assert.AreEqual(0f, RollDeg(Fly(false, 30f, true).Rotation), 1e-3f, "Reduce motion: level");
            Assert.Greater(CameraRigTable.ForAircraft(37f, false).DistanceM, 45f, "an airliner sits farther back");
        }

        [Test]
        public void TheChaseCameraDoesNotAllocatePerFrame()
        {
            var cam = new ChaseCamera { Obstacles = new CharactersCameraTests.BoxWorld().Add(-12, 0, -60, -2.4, 14, 60).Add(2.4, 0, -60, 12, 14, 60) };
            var target = new ChaseTarget { Ground = new V3(0f, 0f, 0f), SpeedMps = 6f, Rig = RigClass.TwoWheeler };
            var ground = new PlaneGround { H0 = 0.0 };
            // Rides down the lane and back warm up every path (lane mode, the lift, the hold) and let the runtime finish its
            // one-off work (tiered compilation); the last ride must not allocate at all. Frames that do are listed.
            var allocating = new List<int>();
            long total = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = 0; i < 300; i++)
                {
                    float z = pass % 2 == 0 ? i * 0.2f - 30f : 30f - i * 0.2f;
                    target.Ground = new V3(0f, 0f, z);
                    target.HeadingRad = pass % 2 == 0 ? 0f : MathF.PI;
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    cam.Tick(1f / 30f, target, CameraView.Near, new ChaseControls(), Landscape, ground, 0, 0, 0);
                    long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                    if (pass == 3 && bytes != 0)
                    {
                        total += bytes;
                        allocating.Add(i);
                    }
                }
            }
            Assert.AreEqual(0, total, "no garbage per frame (frames " + string.Join(", ", allocating) + ")");
        }
    }
}
