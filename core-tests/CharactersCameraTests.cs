using System;
using System.Collections.Generic;
using Ghumante.Core.Characters;
using Ghumante.Core.Driving;
using Ghumante.Core.Save;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The W2 detail-pass camera (docs/W2_DETAIL_CONTRACT.md, package camera): views per vehicle class and their cycle,
    /// the per-class memory in the save, the reversing frame, and the collision-aware boom (pull in, ease out, lift over
    /// low walls, lane mode) tested against a fake <see cref="IViewObstacleQuery"/> made of boxes, including a full
    /// reversing manoeuvre in a 4.8 m lane.
    /// </summary>
    public class CharactersCameraTests
    {
        /// <summary>Axis-aligned boxes with an exact sphere sweep (sphere tracing on the box distance field).</summary>
        internal sealed class BoxWorld : IViewObstacleQuery
        {
            private readonly List<double[]> _boxes = new List<double[]>();

            public int Calls;

            public BoxWorld Add(double x0, double y0, double z0, double x1, double y1, double z1)
            {
                _boxes.Add(new[] { Math.Min(x0, x1), Math.Min(y0, y1), Math.Min(z0, z1), Math.Max(x0, x1), Math.Max(y0, y1), Math.Max(z0, z1) });
                return this;
            }

            /// <summary>Distance from a point to the nearest box (0 inside one).</summary>
            public double Distance(double x, double y, double z)
            {
                double best = double.PositiveInfinity;
                foreach (double[] b in _boxes)
                {
                    double dx = Math.Max(Math.Max(b[0] - x, 0.0), x - b[3]);
                    double dy = Math.Max(Math.Max(b[1] - y, 0.0), y - b[4]);
                    double dz = Math.Max(Math.Max(b[2] - z, 0.0), z - b[5]);
                    best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy + dz * dz));
                }
                return best;
            }

            public bool SphereCast(double ox, double oy, double oz, double dx, double dy, double dz, double radius, double maxDist,
                                   out double hitDist)
            {
                Calls++;
                double t = 0.0;
                while (t <= maxDist)
                {
                    double d = Distance(ox + dx * t, oy + dy * t, oz + dz * t);
                    if (d <= radius + 1e-6)
                    {
                        hitDist = t;
                        return true;
                    }
                    t += Math.Max(0.002, d - radius);
                }
                hitDist = 0.0;
                return false;
            }

            /// <summary>True when the segment from a to b passes through a box (sampled every 5 cm).</summary>
            public bool Blocks(double ax, double ay, double az, double bx, double by, double bz)
            {
                double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay) + (bz - az) * (bz - az));
                int n = Math.Max(1, (int)Math.Ceiling(len / 0.05));
                for (int i = 0; i <= n; i++)
                {
                    double t = (double)i / n;
                    if (Distance(ax + (bx - ax) * t, ay + (by - ay) * t, az + (bz - az) * t) <= 0.0) return true;
                }
                return false;
            }
        }

        private static void Camera(ChaseBoom boom, double px, double py, double pz, float yawDeg, out double cx, out double cy, out double cz)
        {
            double dx, dy, dz;
            ChaseBoom.Direction(yawDeg, boom.PitchDeg, out dx, out dy, out dz);
            cx = px + dx * boom.DistanceM;
            cy = py + dy * boom.DistanceM;
            cz = pz + dz * boom.DistanceM;
        }

        // ----- Views per class --------------------------------------------------------------------------------------------

        [Test]
        public void EveryClassOffersItsOwnAngles()
        {
            CollectionAssert.AreEqual(new[] { CameraView.Near, CameraView.Far, CameraView.OverShoulder }, Views(RigClass.Walk));
            foreach (RigClass two in new[] { RigClass.Bicycle, RigClass.TwoWheeler })
            {
                CollectionAssert.AreEqual(new[] { CameraView.Near, CameraView.Far, CameraView.LowCinematic, CameraView.Handlebar }, Views(two),
                                          two.ToString());
            }
            foreach (RigClass car in new[] { RigClass.Car, RigClass.Van })
                CollectionAssert.AreEqual(new[] { CameraView.Near, CameraView.Far, CameraView.Hood, CameraView.Interior }, Views(car), car.ToString());
            foreach (RigClass heavy in new[] { RigClass.Bus, RigClass.Truck, RigClass.Tractor })
            {
                CollectionAssert.AreEqual(new[] { CameraView.HighChase, CameraView.Far, CameraView.DriverSeat }, Views(heavy),
                                          heavy.ToString());
            }
            CollectionAssert.AreEqual(new[] { CameraView.Near, CameraView.Far, CameraView.PassengerSeat }, Views(RigClass.Passenger));
        }

        private static CameraView[] Views(RigClass rig)
        {
            var v = new CameraView[CameraViews.Count(rig)];
            for (int i = 0; i < v.Length; i++) v[i] = CameraViews.At(rig, i);
            return v;
        }

        [Test]
        public void CyclingWrapsAndRecoversFromForeignViews()
        {
            CameraView v = CameraViews.Default(RigClass.TwoWheeler);
            var seen = new List<CameraView>();
            for (int i = 0; i < 8; i++)
            {
                seen.Add(v);
                v = CameraViews.Next(RigClass.TwoWheeler, v);
            }
            CollectionAssert.AreEqual(new[]
            {
                CameraView.Near, CameraView.Far, CameraView.LowCinematic, CameraView.Handlebar,
                CameraView.Near, CameraView.Far, CameraView.LowCinematic, CameraView.Handlebar,
            }, seen);
            Assert.AreEqual(CameraView.HighChase, CameraViews.Next(RigClass.Bus, CameraView.Handlebar), "a view the bus lacks restarts its cycle");
            Assert.AreEqual(CameraView.HighChase, CameraViews.Valid(RigClass.Bus, CameraView.Hood));
            Assert.AreEqual(CameraView.Hood, CameraViews.Valid(RigClass.Van, CameraView.Hood));
            Assert.AreEqual(CameraView.Handlebar, CameraViews.At(RigClass.Bicycle, -1), "wraps backwards too");
        }

        [Test]
        public void NearIsTheClassRigAndTheOthersChangeItAsNamed()
        {
            foreach (bool portrait in new[] { false, true })
            {
                for (int c = 0; c < CameraViews.RigCount; c++)
                {
                    var rig = (RigClass)c;
                    RigParams table = CameraRigTable.For(rig, portrait, RigClass.Bus);
                    RigParams near = CameraViews.Chase(rig, CameraViews.Default(rig), portrait, RigClass.Bus);
                    Assert.AreEqual(table.DistanceM, near.DistanceM, rig + " default view = the W2_DESIGN 6.4 rig");
                    Assert.AreEqual(table.PitchDeg, near.PitchDeg);
                    Assert.AreEqual(table.PivotM, near.PivotM);
                    Assert.AreEqual(table.MinHFovDeg, near.MinHFovDeg);
                    Assert.AreEqual(0f, near.ShoulderM);

                    RigParams far = CameraViews.Chase(rig, CameraView.Far, portrait, RigClass.Bus);
                    Assert.Greater(far.DistanceM, near.DistanceM * 1.3f, rig + " far is farther");
                    Assert.Greater(far.PitchDeg, near.PitchDeg, rig + " and higher");
                    Assert.LessOrEqual(far.MinDistanceM, far.DistanceM);
                }

                RigParams walk = CameraRigTable.For(RigClass.Walk, portrait);
                RigParams shoulder = CameraViews.Chase(RigClass.Walk, CameraView.OverShoulder, portrait, RigClass.Car);
                Assert.Less(shoulder.DistanceM, 0.6f * walk.DistanceM, "over the shoulder is close");
                Assert.Greater(shoulder.ShoulderM, 0.3f, "and beside the head");
                Assert.Greater(shoulder.PivotM, 1.3f, "at shoulder height");
                Assert.IsTrue(shoulder.AimAtPivot);
                Assert.LessOrEqual(shoulder.MinDistanceM, shoulder.DistanceM);

                RigParams bike = CameraRigTable.For(RigClass.TwoWheeler, portrait);
                RigParams low = CameraViews.Chase(RigClass.TwoWheeler, CameraView.LowCinematic, portrait, RigClass.Car);
                Assert.Less(low.PivotM, bike.PivotM);
                Assert.Less(low.PitchDeg, bike.PitchDeg);
                Assert.Less(low.DistanceM, bike.DistanceM);
                Assert.Greater(low.PitchDeg, 0f, "still above the road");
            }
        }

        [Test]
        public void MountedViewsCarryTheirOwnLensAndClip()
        {
            foreach (CameraView v in new[] { CameraView.Handlebar, CameraView.Hood, CameraView.Interior, CameraView.DriverSeat, CameraView.PassengerSeat })
            {
                CameraViewSpec s = CameraViews.Spec(v);
                Assert.IsTrue(s.Mounted, v.ToString());
                Assert.IsTrue(CameraViews.IsMounted(v));
                Assert.That(s.MinHFovDeg, Is.InRange(62f, 80f), v + " keeps the CameraFov minimum");
                Assert.That(s.NearClipM, Is.LessThanOrEqualTo(0.15f), v + " sees the dashboard");
                Assert.That(s.LookDownDeg, Is.InRange(0f, 15f));
                Assert.That(s.RollShare, Is.InRange(0f, 1f));
            }
            Assert.AreEqual(CameraViewKind.Hood, CameraViews.Spec(CameraView.Hood).Kind);
            Assert.AreEqual(CameraViewKind.Eye, CameraViews.Spec(CameraView.Handlebar).Kind);
            Assert.Less(CameraViews.Spec(CameraView.Handlebar).RollShare, 1f, "the handlebar view leans only partly");
            foreach (CameraView v in new[] { CameraView.Near, CameraView.Far, CameraView.OverShoulder, CameraView.LowCinematic, CameraView.HighChase })
            {
                Assert.IsFalse(CameraViews.IsMounted(v), v.ToString());
                Assert.AreEqual(CameraViews.ChaseNearClipM, CameraViews.Spec(v).NearClipM);
            }
            Assert.Greater(CameraViews.EyeForwardM, 0.19f + CameraViews.MountedNearClipM, "the eye sits in front of the 0.38 m deep head");

            float y, z;
            CameraViews.HoodEye(2.5f, 0.9f, 1.5f, out y, out z);
            Assert.That(z, Is.InRange(2.5f, 3.4f), "on the bonnet, ahead of the front axle");
            Assert.That(y, Is.InRange(0.9f, 1.5f), "just above the bonnet line, below the roof");
        }

        [Test]
        public void ViewNamesAndKeysAreDistinct()
        {
            var names = new HashSet<string>();
            var keys = new HashSet<string>();
            foreach (CameraView v in Enum.GetValues(typeof(CameraView)))
            {
                Assert.IsTrue(names.Add(CameraViews.Name(v)), v.ToString());
                Assert.IsTrue(keys.Add(CameraViews.Key(v)), v.ToString());
                StringAssert.StartsWith("hud.camera.", CameraViews.Key(v));
                CameraView back;
                Assert.IsTrue(CameraViews.TryParse(CameraViews.Name(v), out back));
                Assert.AreEqual(v, back);
            }
            CameraView none;
            Assert.IsFalse(CameraViews.TryParse("drone", out none));
            Assert.IsFalse(CameraViews.TryParse(null, out none));
        }

        // ----- Memory in the save ------------------------------------------------------------------------------------------

        [Test]
        public void EachClassRemembersItsViewInTheSave()
        {
            var save = new SaveData();
            CameraViewMemory m = CameraViewMemory.Load(save);
            for (int c = 0; c < CameraViews.RigCount; c++) Assert.AreEqual(CameraViews.Default((RigClass)c), m.Get((RigClass)c));

            Assert.AreEqual(CameraView.Far, m.Cycle(RigClass.TwoWheeler));
            Assert.AreEqual(CameraView.LowCinematic, m.Cycle(RigClass.TwoWheeler));
            Assert.AreEqual(CameraView.Handlebar, m.Cycle(RigClass.TwoWheeler));
            Assert.IsTrue(m.Set(RigClass.Car, CameraView.Interior));
            Assert.IsFalse(m.Set(RigClass.Car, CameraView.Handlebar), "a car has no handlebar");
            Assert.IsTrue(m.Set(RigClass.Bus, CameraView.DriverSeat));
            Assert.AreEqual(CameraView.Near, m.Get(RigClass.Walk), "classes are independent");
            m.Store(save);

            // Through the save file and back (SaveData JSON round trip).
            SaveData loaded = SaveData.FromJson(Json.Parse(Json.Write(save.ToJson())).AsObject());
            CameraViewMemory back = CameraViewMemory.Load(loaded);
            Assert.AreEqual(CameraView.Handlebar, back.Get(RigClass.TwoWheeler));
            Assert.AreEqual(CameraView.Interior, back.Get(RigClass.Car));
            Assert.AreEqual(CameraView.DriverSeat, back.Get(RigClass.Bus));
            Assert.AreEqual(CameraView.Near, back.Get(RigClass.Bicycle));
            Assert.AreEqual("handlebar", loaded.Settings.Extra.GetObject(CameraViewMemory.SaveKey).GetString("two_wheeler"));
        }

        [Test]
        public void UnknownOrForeignSavedViewsFallBack()
        {
            var o = new JsonObject().Set("car", "drone").Set("bus", "handlebar").Set("walk", "over_shoulder").Set("unknown_rig", "far");
            CameraViewMemory m = CameraViewMemory.FromJson(o);
            Assert.AreEqual(CameraView.Near, m.Get(RigClass.Car));
            Assert.AreEqual(CameraView.HighChase, m.Get(RigClass.Bus));
            Assert.AreEqual(CameraView.OverShoulder, m.Get(RigClass.Walk));
            Assert.AreEqual(CameraView.Near, CameraViewMemory.FromJson(null).Get(RigClass.Passenger));
            Assert.AreEqual(CameraView.Near, CameraViewMemory.Load(null).Get(RigClass.TwoWheeler));
        }

        // ----- Reversing frame ---------------------------------------------------------------------------------------------

        [Test]
        public void ABriefRollBackDoesNotReframe()
        {
            var r = new ReverseFraming();
            for (int i = 0; i < 5; i++) r.Update(0.05f, -1.5f, true); // 0.25 s
            Assert.IsFalse(r.Reversing);
            Assert.AreEqual(0f, r.Raise01);
            Assert.AreEqual(1f, r.DistanceScale);
        }

        [Test]
        public void ReversingRaisesShortensThenSwingsToLookAlongTheTravel()
        {
            var r = new ReverseFraming();
            float t = 0f;
            while (t < 0.9f)
            {
                r.Update(1f / 30f, -1.5f, true);
                t += 1f / 30f;
            }
            Assert.IsTrue(r.Reversing);
            Assert.Greater(r.Raise01, 0.9f);
            Assert.AreEqual(ReverseFraming.RaisePitchDeg, r.PitchAddDeg, 1.6f);
            Assert.AreEqual(ReverseFraming.ShortenTo, r.DistanceScale, 0.03f);
            Assert.Greater(r.FootRaise, 0.25f, "the lane behind shows under the vehicle");
            Assert.AreEqual(0f, r.SwingYawDeg, "no swing before 1 s");
            while (t < 3.0f)
            {
                r.Update(1f / 30f, -1.5f, true);
                t += 1f / 30f;
            }
            Assert.AreEqual(180f, r.SwingYawDeg, 1e-3f, "now looking along the direction of travel");

            // Forward again: the swing undoes at once and the frame eases back.
            for (int i = 0; i < 60; i++) r.Update(1f / 30f, 3f, true);
            Assert.IsFalse(r.Reversing);
            Assert.AreEqual(0f, r.SwingYawDeg);
            Assert.Less(r.Raise01, 0.01f);
        }

        [Test]
        public void LookingAroundOrCreepingCancelsTheSwing()
        {
            var r = new ReverseFraming();
            for (int i = 0; i < 90; i++) r.Update(1f / 30f, -1.5f, false);
            Assert.IsTrue(r.Reversing, "still raised");
            Assert.AreEqual(0f, r.SwingYawDeg, "the player looks themselves");

            var slow = new ReverseFraming();
            for (int i = 0; i < 90; i++) slow.Update(1f / 30f, -0.8f, true);
            Assert.IsTrue(slow.Reversing);
            Assert.AreEqual(0f, slow.SwingYawDeg, "inching back keeps the camera behind");

            // Standing still keeps the reversing frame for a moment (manoeuvring), then lets go.
            var stop = new ReverseFraming();
            for (int i = 0; i < 30; i++) stop.Update(1f / 30f, -1.5f, true);
            for (int i = 0; i < 30; i++) stop.Update(1f / 30f, 0f, true);
            Assert.IsTrue(stop.Reversing, "1 s stopped while manoeuvring");
            for (int i = 0; i < 40; i++) stop.Update(1f / 30f, 0f, true);
            Assert.IsFalse(stop.Reversing, "released after 2 s standing");
            stop.Update(float.NaN, float.NaN, true);
            stop.Update(1f / 30f, float.NaN, true);
            Assert.IsFalse(float.IsNaN(stop.Raise01));
        }

        // ----- The boom ----------------------------------------------------------------------------------------------------

        [Test]
        public void OpenGroundKeepsTheWantedBoom()
        {
            var world = new BoxWorld().Add(-100, -1, -100, 100, 0, 100); // the ground slab
            var boom = new ChaseBoom();
            boom.Solve(world, 0, 1, 0, 0f, 13f, 8f, 4f, 0.016f);
            Assert.AreEqual(8f, boom.DistanceM, 1e-4f);
            Assert.AreEqual(13f, boom.PitchDeg, 1e-4f);
            Assert.IsFalse(boom.Blocked);
            Assert.AreEqual(0f, boom.LiftDeg);
            Assert.That(boom.Casts, Is.InRange(1, 3));
        }

        [Test]
        public void AWallBehindPullsInAtOnceAndEasesBackOut()
        {
            var world = new BoxWorld().Add(-10, 0, -3.5, 10, 12, -3.0); // a house wall 3 m behind the pivot (camera sits at -z)
            var open = new BoxWorld();
            var boom = new ChaseBoom();
            boom.Solve(open, 0, 1, 0, 0f, 13f, 8f, 1f, 0.016f);
            Assert.AreEqual(8f, boom.DistanceM, 1e-4f);
            boom.Solve(world, 0, 1, 0, 0f, 13f, 8f, 1f, 0.016f); // the house appears behind (riding past a corner)
            double cx, cy, cz;
            Camera(boom, 0, 1, 0, 0f, out cx, out cy, out cz);
            Assert.IsTrue(boom.Blocked);
            Assert.Less(boom.DistanceM, 3.1f, "in front of the wall");
            Assert.GreaterOrEqual(world.Distance(cx, cy, cz), ChaseBoom.ProbeRadiusM, "never inside it");
            Assert.IsFalse(world.Blocks(0, 1, 0, cx, cy, cz), "and never behind it");

            // The wall goes (the explorer rode past it): out again, smoothly.
            float before = boom.DistanceM;
            boom.Solve(open, 0, 1, 0, 0f, 13f, 8f, 1f, 0.1f);
            Assert.AreEqual(before, boom.DistanceM, 1e-4f, "a short hold first, so a passing pole does not pump the camera");
            float last = boom.DistanceM;
            int frames = 0;
            while (boom.DistanceM < 7.9f && frames < 200)
            {
                boom.Solve(open, 0, 1, 0, 0f, 13f, 8f, 1f, 1f / 60f);
                Assert.GreaterOrEqual(boom.DistanceM, last - 1e-5f, "eases out monotonically");
                Assert.Less(boom.DistanceM - last, 0.6f, "no jump");
                last = boom.DistanceM;
                frames++;
            }
            Assert.That(frames / 60f, Is.InRange(0.3f, 1.5f), "back out over about 0.6 s");
        }

        [Test]
        public void ALowWallJustBehindIsClearedByLiftingTheBoom()
        {
            // A 1.6 m garden wall 1.5 m behind a walker; the low cinematic boom would hit it.
            var world = new BoxWorld().Add(-10, 0, -1.9, 10, 1.6, -1.5);
            var boom = new ChaseBoom();
            boom.Solve(world, 0, 0.9, 0, 0f, 4f, 6f, 3f, 0.016f); // snap
            for (int i = 0; i < 60; i++) boom.Solve(world, 0, 0.9, 0, 0f, 4f, 6f, 3f, 1f / 60f);
            Assert.Greater(boom.LiftDeg, 10f, "lifted over the wall");
            Assert.GreaterOrEqual(boom.DistanceM, 3f, "keeping the minimum distance");
            double cx, cy, cz;
            Camera(boom, 0, 0.9, 0, 0f, out cx, out cy, out cz);
            Assert.Greater(cy, 1.6, "above the wall");
            Assert.IsFalse(world.Blocks(0, 0.9, 0, cx, cy, cz));

            // Away from the wall the lift settles back.
            var open = new BoxWorld();
            for (int i = 0; i < 180; i++) boom.Solve(open, 0, 0.9, 0, 0f, 4f, 6f, 3f, 1f / 60f);
            Assert.Less(boom.LiftDeg, 0.5f);
            Assert.AreEqual(4f, boom.PitchDeg, 0.5f);
        }

        [Test]
        public void ANarrowLaneRaisesThePitchAndShortensTheBoom()
        {
            // A 4.8 m lane along z between tall houses.
            var world = new BoxWorld().Add(-12, 0, -60, -2.4, 14, 60).Add(2.4, 0, -60, 12, 14, 60);
            var boom = new ChaseBoom();
            boom.Solve(world, 0, 1, 0, 0f, 13f, 8f, 4f, 0.016f);
            for (int i = 0; i < 60; i++) boom.Solve(world, 0, 1, 0, 0f, 13f, 8f, 4f, 1f / 60f);
            Assert.AreEqual(1f, boom.Lane01, 1e-4f);
            Assert.AreEqual(13f + ChaseBoom.LanePitchDeg, boom.PitchDeg, 0.01f);
            Assert.AreEqual(8f * ChaseBoom.LaneDistanceScale, boom.DistanceM, 0.01f, "the lane behind is straight and clear");

            // One wall only (a street along a single facade) is not a lane.
            var side = new BoxWorld().Add(-12, 0, -60, -2.4, 14, 60);
            var b2 = new ChaseBoom();
            for (int i = 0; i < 60; i++) b2.Solve(side, 0, 1, 0, 0f, 13f, 8f, 4f, 1f / 60f);
            Assert.AreEqual(0f, b2.Lane01);
            Assert.AreEqual(13f, b2.PitchDeg, 0.01f);
        }

        [Test]
        public void WithoutAQueryTheBoomOnlyEases()
        {
            var boom = new ChaseBoom();
            boom.Solve(null, 0, 1, 0, 30f, 13f, 8f, 4f, 0.016f);
            Assert.AreEqual(8f, boom.DistanceM);
            Assert.AreEqual(0, boom.Casts);
            boom.Solve(null, 0, 1, 0, 30f, 13f, 12f, 4f, 0.016f);
            Assert.Greater(boom.DistanceM, 8f);
            Assert.Less(boom.DistanceM, 12f, "zooming out eases");
            boom.Solve(null, 0, 1, 0, 30f, 13f, 6f, 4f, 0.016f);
            Assert.AreEqual(6f, boom.DistanceM, "zooming in follows at once");
            boom.Solve(null, 0, 1, 0, 30f, 13f, float.NaN, 4f, float.NaN);
            Assert.IsFalse(float.IsNaN(boom.DistanceM));
        }

        [Test]
        public void ASweepThatStartsInsideIsIgnored()
        {
            // The pivot sits inside a balcony slab: a hit at 0 m means "started inside", not "a wall at the pivot".
            var world = new BoxWorld().Add(-5, 0.5, -0.5, 5, 1.5, 0.5);
            var boom = new ChaseBoom();
            boom.Solve(world, 0, 1, 0, 0f, 13f, 8f, 4f, 0.016f);
            Assert.AreEqual(8f, boom.DistanceM, 1e-4f);
        }

        [Test]
        public void ReversingAMotorbikeInANarrowLaneKeepsAClearView()
        {
            // Owner: "Camera view gets blocked if I try to back up my motorbike in the narrower streets." A 4.8 m lane
            // (RoadClearance.MinCorridorM) between 10 m houses that turns a corner 7 m behind the bike: a wall closes the
            // lane behind and the lane goes on to the east. The bike rides in from the south-east corner, stops, then
            // reverses 6 m towards the corner. The camera, run like ChaseCameraRig runs it (class rig, reverse frame,
            // lane mode, boom), must never sit inside a house or behind one, and must keep the bike in view.
            const double half = 2.4;
            var world = new BoxWorld()
                        .Add(-14, 0, -60, -half, 10, 60) // west row
                        .Add(half, 0, -2.0, 14, 10, 60) // east row, north of the side lane
                        .Add(-14, 0, -20, 30, 10, -6.8); // the houses closing the lane to the south (behind the bike); the
                                                         // side lane runs east between z = -6.8 and -2.0
            var reverse = new ReverseFraming();
            var boom = new ChaseBoom();
            RigParams rig = CameraRigTable.For(RigClass.TwoWheeler, false);
            double bx = 0, bz = 0;
            const float heading = 0f; // facing north, reversing south
            float dt = 1f / 30f;
            bool snapped = false;
            int blockedFrames = 0, frames = 0;
            float maxLane = 0f;
            for (float t = 0f; t < 5.5f; t += dt)
            {
                float speed = t < 1f ? 0f : bz > -4.3 ? -1.5f : 0f;
                bz += speed * dt * Math.Cos(heading);
                reverse.Update(dt, speed, true);
                float yaw = heading * 57.29578f + reverse.SwingYawDeg;
                float pitch = rig.PitchDeg + reverse.PitchAddDeg;
                float distance = rig.DistanceM * reverse.DistanceScale;
                double px = bx, py = rig.PivotM, pz = bz;
                if (!snapped)
                {
                    boom.Snap();
                    snapped = true;
                }
                boom.Solve(world, px, py, pz, yaw, pitch, distance, rig.MinDistanceM, dt);
                double cx, cy, cz;
                Camera(boom, px, py, pz, yaw, out cx, out cy, out cz);
                frames++;
                Assert.GreaterOrEqual(world.Distance(cx, cy, cz), ChaseBoom.ProbeRadiusM - 0.01,
                                      "t = " + t.ToString("0.00") + ": the camera is inside a house");
                Assert.IsFalse(world.Blocks(px, py, pz, cx, cy, cz), "t = " + t.ToString("0.00") + ": a house hides the bike");
                Assert.Greater(cy, 0.8, "above the road");
                if (boom.Blocked) blockedFrames++;
                maxLane = Math.Max(maxLane, boom.Lane01);
            }
            Assert.IsTrue(reverse.Reversing, "the reverse frame is on at the end");
            Assert.Greater(maxLane, 0.9f, "lane mode in the 4.8 m lane");
            Assert.Greater(blockedFrames, 0, "the corner did cut the boom: the test exercises the pull-in");
            Assert.Greater(boom.DistanceM, 1.5f, "still a usable distance, not inside the rider's head");
        }

        [Test]
        public void SolvingIsDeterministic()
        {
            var world = new BoxWorld().Add(-10, 0, -3.5, 10, 2, -3.0).Add(-12, 0, -60, -2.4, 14, 60).Add(2.4, 0, -60, 12, 14, 60);
            var a = new ChaseBoom();
            var b = new ChaseBoom();
            for (int i = 0; i < 100; i++)
            {
                float yaw = 20f * (float)Math.Sin(i * 0.1);
                a.Solve(world, 0, 1, 0, yaw, 13f, 8f, 4f, 1f / 60f);
                b.Solve(world, 0, 1, 0, yaw, 13f, 8f, 4f, 1f / 60f);
                Assert.AreEqual(a.DistanceM, b.DistanceM);
                Assert.AreEqual(a.PitchDeg, b.PitchDeg);
            }
        }
    }
}
