using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class DrivingVehicleTests
    {
        private const float Dt60 = 1f / 60f;
        private const float Deg = (float)(Math.PI / 180.0);

        private static ArcadeVehicle At(VehicleSpec spec, IGroundQuery g, double x = 0, double z = 0, float heading = 0f)
        {
            var v = new ArcadeVehicle(spec);
            v.Teleport(x, z, heading, g);
            return v;
        }

        private static StepEvents Run(ArcadeVehicle v, IGroundQuery g, DriveInput input, float seconds, float dt = Dt60, float wet = 0f)
        {
            StepEvents all = StepEvents.None;
            int frames = (int)Math.Round(seconds / dt);
            for (int i = 0; i < frames; i++) all |= v.Step(input, dt, g, wet);
            return all;
        }

        private static readonly DriveInput Full = new DriveInput(1f, 0f, 0f);
        private static readonly DriveInput Idle = new DriveInput(0f, 0f, 0f);

        [Test]
        public void PresetsAreValidAndMatchTheBrief()
        {
            VehicleSpec bike = VehicleSpec.Motorbike(), taxi = VehicleSpec.Taxi(), walker = VehicleSpec.Walker();
            foreach (VehicleSpec s in new[] { bike, taxi, walker }) Assert.DoesNotThrow(s.Validate);
            Assert.That(bike.MaxSpeedKmh, Is.EqualTo(85f));
            Assert.That(taxi.MaxSpeedKmh, Is.EqualTo(75f));
            Assert.That(walker.MaxSpeedMps, Is.EqualTo(4.5f).Within(1e-5));
            Assert.That(walker.WalkSpeedMps, Is.EqualTo(1.6f));
            Assert.That(walker.TurnInPlace, Is.True);
            Assert.That(bike.Grip, Is.EqualTo(new[] { 1.0f, 0.8f, 0.7f, 0.45f }));
            Assert.That(bike.TopSpeedFactor, Is.EqualTo(new[] { 1.0f, 0.75f, 0.6f, 0.35f }));
            Assert.That(taxi.Grip, Is.EqualTo(bike.Grip));
            Assert.That(taxi.WheelbaseM, Is.GreaterThan(bike.WheelbaseM));

            VehicleSpec c = bike.Clone();
            c.Grip[0] = 0.5f;
            Assert.That(bike.Grip[0], Is.EqualTo(1f), "clones do not share tables");
            c.MaxSpeedKmh = -1f;
            Assert.Throws<ArgumentException>(c.Validate);
            Assert.Throws<ArgumentException>(() => new ArcadeVehicle(c));
            Assert.Throws<ArgumentNullException>(() => new ArcadeVehicle(null));
        }

        [Test]
        public void WetnessTurnsDirtIntoMud()
        {
            VehicleSpec s = VehicleSpec.Motorbike();
            Assert.That(VehicleSpec.Effective(SurfaceGroup.Dirt, 0.49f), Is.EqualTo(SurfaceGroup.Dirt));
            Assert.That(VehicleSpec.Effective(SurfaceGroup.Dirt, 0.5f), Is.EqualTo(SurfaceGroup.Mud));
            Assert.That(VehicleSpec.Effective(SurfaceGroup.Paved, 1f), Is.EqualTo(SurfaceGroup.Paved));
            Assert.That(s.GripOf(SurfaceGroup.Dirt, 1f), Is.EqualTo(s.GripOf(SurfaceGroup.Mud, 0f)));
            Assert.That(s.TopSpeedFactorOf(SurfaceGroup.Dirt, 1f), Is.EqualTo(0.35f));
            Assert.That(s.TopSpeedFactorOf(SurfaceGroup.Dirt, 0.5f), Is.EqualTo(0.475f).Within(1e-6));
            Assert.That(s.GripOf(SurfaceGroup.Paved, 1f), Is.LessThan(s.GripOf(SurfaceGroup.Paved, 0f)));
            Assert.That(s.TopSpeedFactorOf(SurfaceGroup.Paved, 1f), Is.EqualTo(1f));

            // Driving: wet dirt tops out at the mud speed, and the reported surface flips with an event.
            var dirt = new PlaneGround { Surface = SurfaceGroup.Dirt };
            ArcadeVehicle dry = At(s, dirt), wet = At(s, dirt);
            Run(dry, dirt, Full, 20f, Dt60, 0f);
            StepEvents ev = Run(wet, dirt, Full, 20f, Dt60, 1f);
            Assert.That(dry.SpeedMps, Is.EqualTo(s.MaxSpeedMps * 0.6f).Within(0.05));
            Assert.That(wet.SpeedMps, Is.EqualTo(s.MaxSpeedMps * 0.35f).Within(0.05));
            Assert.That(dry.Surface, Is.EqualTo(SurfaceGroup.Dirt));
            Assert.That(wet.Surface, Is.EqualTo(SurfaceGroup.Mud));
            Assert.That(ev & StepEvents.SurfaceChanged, Is.EqualTo(StepEvents.SurfaceChanged));
        }

        [TestCase(SurfaceGroup.Paved)]
        [TestCase(SurfaceGroup.Gravel)]
        [TestCase(SurfaceGroup.Dirt)]
        [TestCase(SurfaceGroup.Mud)]
        public void AcceleratesToTopSpeedPerSurface(SurfaceGroup surface)
        {
            foreach (VehicleSpec spec in new[] { VehicleSpec.Motorbike(), VehicleSpec.Taxi() })
            {
                var g = new PlaneGround { Surface = surface };
                ArcadeVehicle v = At(spec, g);
                Assert.That(v.Surface, Is.EqualTo(surface));
                float top = spec.TopSpeedMps(surface, 0f);
                float t95 = -1f, t = 0f, prev = 0f;
                for (int i = 0; i < 60 * 30; i++)
                {
                    v.Step(Full, Dt60, g, 0f);
                    t += Dt60;
                    Assert.That(v.SpeedMps, Is.GreaterThanOrEqualTo(prev - 1e-4f), "monotonic on the flat");
                    Assert.That(v.SpeedMps, Is.LessThanOrEqualTo(top + 1e-3f), "never above the surface's top speed");
                    prev = v.SpeedMps;
                    if (t95 < 0 && v.SpeedMps >= 0.95f * top) t95 = t;
                }
                Assert.That(v.SpeedMps, Is.EqualTo(top).Within(top * 0.01f), spec.Name + " on " + surface);
                Assert.That(t95, Is.InRange(1.0f, 15f), spec.Name + " reaches 95% of top speed on " + surface);
                Assert.That(v.Z, Is.GreaterThan(100.0), "drove north");
                Assert.That(Math.Abs(v.X), Is.LessThan(1e-6), "straight");
                Assert.That(v.Y, Is.EqualTo(1300f));
            }
        }

        [Test]
        public void BikeIsQuickTaxiIsHeavier()
        {
            var g = new PlaneGround();
            ArcadeVehicle bike = At(VehicleSpec.Motorbike(), g), taxi = At(VehicleSpec.Taxi(), g);
            float tb = TimeTo(bike, g, 60f / 3.6f), tt = TimeTo(taxi, g, 60f / 3.6f);
            Assert.That(tb, Is.InRange(2.5f, 4.5f), "bike 0-60 km/h");
            Assert.That(tt, Is.GreaterThan(tb + 1f), "taxi 0-60 km/h");
            Assert.That(tt, Is.LessThan(9f));
        }

        private static float TimeTo(ArcadeVehicle v, IGroundQuery g, float speed)
        {
            float t = 0f;
            while (v.SpeedMps < speed && t < 60f)
            {
                v.Step(Full, Dt60, g, 0f);
                t += Dt60;
            }
            return t;
        }

        [Test]
        public void BrakingDistanceIsSane()
        {
            double paved = BrakeFrom(VehicleSpec.Motorbike(), SurfaceGroup.Paved, 60f / 3.6f);
            double mud = BrakeFrom(VehicleSpec.Motorbike(), SurfaceGroup.Mud, 30f / 3.6f);
            double pavedSlow = BrakeFrom(VehicleSpec.Motorbike(), SurfaceGroup.Paved, 30f / 3.6f);
            double taxi = BrakeFrom(VehicleSpec.Taxi(), SurfaceGroup.Paved, 60f / 3.6f);
            // Real-world dry stopping distance from 60 km/h (no reaction time) is about 15-25 m.
            Assert.That(paved, Is.InRange(12.0, 25.0), "bike from 60 km/h");
            Assert.That(taxi, Is.InRange(12.0, 28.0), "taxi from 60 km/h");
            Assert.That(taxi, Is.GreaterThan(paved));
            Assert.That(mud, Is.GreaterThan(1.5 * pavedSlow), "mud brakes worse");
        }

        private static double BrakeFrom(VehicleSpec spec, SurfaceGroup surface, float speed)
        {
            var g = new PlaneGround { Surface = surface };
            ArcadeVehicle v = At(spec, g);
            v.SpeedMps = speed;
            double z0 = v.Z;
            var brake = new DriveInput(0f, 1f, 0f);
            for (int i = 0; i < 600 && v.SpeedMps > 0f; i++) v.Step(brake, Dt60, g, 0f);
            Assert.That(v.SpeedMps, Is.EqualTo(0f), "stopped");
            Run(v, g, brake, 1f);
            Assert.That(v.SpeedMps, Is.EqualTo(0f), "and does not roll backwards");
            return v.Z - z0;
        }

        [Test]
        public void ThrottleBackwardsBrakesThenReverses()
        {
            var g = new PlaneGround();
            ArcadeVehicle v = At(VehicleSpec.Taxi(), g);
            v.SpeedMps = 10f;
            var back = new DriveInput(-1f, 0f, 0f);
            float minSeen = float.MaxValue;
            for (int i = 0; i < 600; i++)
            {
                v.Step(back, Dt60, g, 0f);
                minSeen = Math.Min(minSeen, v.SpeedMps);
            }
            Assert.That(v.SpeedMps, Is.EqualTo(-VehicleSpec.Taxi().ReverseSpeedMps).Within(0.05));
            Assert.That(v.HeadingRad, Is.EqualTo(0f).Within(1e-6), "reversing does not turn");
            // Forward throttle while reversing brakes first.
            v.Step(Full, Dt60, g, 0f);
            Assert.That(v.SpeedMps, Is.GreaterThan(-VehicleSpec.Taxi().ReverseSpeedMps));
        }

        [Test]
        public void WalkerGaitsAndTurnInPlace()
        {
            var g = new PlaneGround();
            VehicleSpec spec = VehicleSpec.Walker();
            ArcadeVehicle w = At(spec, g);
            Run(w, g, new DriveInput(0.5f, 0f, 0f), 2f);
            Assert.That(w.SpeedMps, Is.EqualTo(1.6f).Within(1e-4), "walk");
            Run(w, g, new DriveInput(1f, 0f, 0f), 2f);
            Assert.That(w.SpeedMps, Is.EqualTo(4.5f).Within(1e-4), "run");
            Run(w, g, new DriveInput(1f, 0f, 0f, true), 2f);
            Assert.That(w.SpeedMps, Is.EqualTo(6.0f).Within(1e-4), "sprint");
            Run(w, g, Idle, 1f);
            Assert.That(w.SpeedMps, Is.EqualTo(0f), "stops quickly");
            Assert.That(w.SlipRad, Is.EqualTo(0f), "never drifts");

            // Turning in place: heading changes, position does not.
            double x = w.X, z = w.Z;
            Run(w, g, new DriveInput(0f, 0f, 1f), 0.3f);
            Assert.That(w.HeadingRad, Is.EqualTo(90f * Deg).Within(0.02));
            Assert.That(w.X, Is.EqualTo(x));
            Assert.That(w.Z, Is.EqualTo(z));
            Assert.That(w.Pitch, Is.EqualTo(0f));
            Assert.That(w.Roll, Is.EqualTo(0f));

            // Mud slows the pace; backwards is a slow backpedal.
            var mud = new PlaneGround { Surface = SurfaceGroup.Mud };
            ArcadeVehicle m = At(spec, mud);
            Run(m, mud, Full, 2f);
            Assert.That(m.SpeedMps, Is.EqualTo(4.5f * 0.7f).Within(1e-3));
            Run(m, mud, new DriveInput(-1f, 0f, 0f), 2f);
            Assert.That(m.SpeedMps, Is.EqualTo(-1.2f * 0.7f).Within(1e-3));

            // SteerToward reaches a camera-relative heading within a frame.
            float target = -1.0f;
            for (int i = 0; i < 30; i++) w.Step(new DriveInput(0f, 0f, w.SteerToward(target, Dt60)), Dt60, g, 0f);
            Assert.That(w.HeadingRad, Is.EqualTo(target).Within(1e-4));
        }

        [Test]
        public void SpeedDependentSteeringAndDrift()
        {
            VehicleSpec spec = VehicleSpec.Motorbike();
            var g = new PlaneGround();
            // Full lock: the yaw rate falls with speed (the turn radius grows).
            float slowYaw = SteadyYaw(spec, g, 6f), fastYaw = SteadyYaw(spec, g, 22f);
            Assert.That(Math.Abs(fastYaw), Is.LessThan(Math.Abs(slowYaw)));
            Assert.That(slowYaw, Is.GreaterThan(0f), "positive steer turns right (heading grows)");

            // Low speed on asphalt: no drift. Fast on mud at full lock: a clear slide.
            ArcadeVehicle a = At(spec, g);
            a.SpeedMps = 6f;
            Run(a, g, new DriveInput(0.3f, 0f, 1f), 1f);
            Assert.That(Math.Abs(a.SlipRad), Is.LessThan(1f * Deg));

            var mud = new PlaneGround { Surface = SurfaceGroup.Mud };
            ArcadeVehicle b = At(spec, mud);
            b.SpeedMps = 8f;
            float maxSlip = 0f;
            for (int i = 0; i < 60; i++)
            {
                b.Step(new DriveInput(1f, 0f, 1f), Dt60, mud, 0f);
                maxSlip = Math.Max(maxSlip, Math.Abs(b.SlipRad));
            }
            Assert.That(maxSlip, Is.GreaterThan(5f * Deg), "drifts on mud");
            Assert.That(maxSlip, Is.LessThanOrEqualTo(spec.MaxSlipDeg * Deg + 1e-4f), "never past the slip cap");
            Assert.That(b.Lean, Is.GreaterThan(0f), "leans right into a right turn");
        }

        private static float SteadyYaw(VehicleSpec spec, IGroundQuery g, float speed)
        {
            ArcadeVehicle v = At(spec, g);
            v.SpeedMps = speed;
            var input = new DriveInput(speed / spec.MaxSpeedMps, 0f, 1f);
            Run(v, g, input, 0.5f);
            return v.YawRateRadPerS;
        }

        [Test]
        public void SlopesSlowClimbsAndBlockSteepOnes()
        {
            VehicleSpec bike = VehicleSpec.Motorbike();
            // 10 % up to the north: climbs, a little slower than on the flat.
            var hill = new PlaneGround { GradeZ = 0.10 };
            ArcadeVehicle v = At(bike, hill);
            Run(v, hill, Full, 20f);
            Assert.That(v.SpeedMps, Is.InRange(0.8f * bike.MaxSpeedMps, 0.99f * bike.MaxSpeedMps));
            Assert.That(v.Y, Is.EqualTo((float)(1300.0 + 0.10 * v.Z)).Within(0.01), "on the ground");
            Assert.That(v.Pitch, Is.EqualTo((float)Math.Atan(0.10)).Within(0.01), "nose up");
            Assert.That(v.Roll, Is.EqualTo(0f).Within(1e-3));

            // 35 degrees (70 %): steeper than the bike's 28 degree limit; it cannot climb.
            var wall = new PlaneGround { GradeZ = Math.Tan(35 * Math.PI / 180) };
            ArcadeVehicle w = At(bike, wall);
            double maxZ = 0;
            for (int i = 0; i < 120; i++)
            {
                w.Step(Full, Dt60, wall, 0f);
                maxZ = Math.Max(maxZ, w.Z);
            }
            Assert.That(maxZ, Is.LessThan(0.5), "no progress up a 35 degree slope");

            // 20 degrees: fine on asphalt, too steep in mud (traction-limited climbing).
            var steepPaved = new PlaneGround { GradeZ = Math.Tan(20 * Math.PI / 180) };
            var steepMud = new PlaneGround { GradeZ = Math.Tan(20 * Math.PI / 180), Surface = SurfaceGroup.Mud };
            ArcadeVehicle p = At(bike, steepPaved), m = At(bike, steepMud);
            Run(p, steepPaved, Full, 3f);
            Run(m, steepMud, Full, 2f);
            Assert.That(p.Z, Is.GreaterThan(8.0));
            Assert.That(p.SpeedMps, Is.GreaterThan(4f));
            Assert.That(m.Z, Is.LessThan(0.5));

            // Parked on 10 % with the brake on: stays. Released in neutral on 30 %: rolls downhill.
            ArcadeVehicle parked = At(bike, hill, 0, 100);
            Run(parked, hill, new DriveInput(0f, 1f, 0f), 3f);
            Assert.That(parked.Z, Is.EqualTo(100.0).Within(1e-9));
            var steep = new PlaneGround { GradeZ = 0.30 };
            ArcadeVehicle rolling = At(bike, steep, 0, 100);
            Run(rolling, steep, Idle, 2f);
            Assert.That(rolling.Z, Is.LessThan(98.0));
            Assert.That(rolling.SpeedMps, Is.LessThan(0f));

            // Downhill at full throttle: engine braking holds about the top speed.
            var down = new PlaneGround { GradeZ = -0.15 };
            ArcadeVehicle d = At(bike, down);
            Run(d, down, Full, 25f);
            Assert.That(d.SpeedMps, Is.InRange(bike.MaxSpeedMps * 0.98f, bike.MaxSpeedMps * 1.05f));

            // On foot: slower uphill, blocked above 40 degrees.
            VehicleSpec walker = VehicleSpec.Walker();
            ArcadeVehicle up = At(walker, hill);
            Run(up, hill, Full, 2f);
            Assert.That(up.SpeedMps, Is.InRange(3.0f, 4.4f));
            var cliff = new PlaneGround { GradeZ = Math.Tan(45 * Math.PI / 180) };
            ArcadeVehicle c = At(walker, cliff);
            Run(c, cliff, Full, 2f);
            Assert.That(c.Z, Is.EqualTo(0.0), "blocked");
            c.HeadingRad = (float)Math.PI; // turn around: downhill is fine
            Run(c, cliff, Full, 1f);
            Assert.That(c.Z, Is.LessThan(-2.0));
        }

        [Test]
        public void PitchAndRollFollowTheNormal()
        {
            // The plane rises to the north. Facing east, the left (north) side is higher: roll right-side-down.
            var g = new PlaneGround { GradeZ = 0.2 };
            ArcadeVehicle v = At(VehicleSpec.Taxi(), g, 0, 0, 90f * Deg);
            Assert.That(v.Roll, Is.EqualTo((float)Math.Asin(0.2 / Math.Sqrt(1.04))).Within(1e-3), "teleport snaps the pose");
            Assert.That(v.Pitch, Is.EqualTo(0f).Within(1e-3));
            v.HeadingRad = 180f * Deg; // facing south: downhill, nose down
            Run(v, g, Idle, 2f);
            Assert.That(v.Pitch, Is.EqualTo((float)-Math.Atan(0.2)).Within(1e-3));
            Assert.That(v.Roll, Is.EqualTo(0f).Within(1e-3));
        }

        [Test]
        public void EventsForSurfacesRoadsAndBumps()
        {
            VehicleSpec spec = VehicleSpec.Motorbike();
            // A road along z = 50 (half width 3); heading north from z = 40 crosses it at speed.
            var g = new PlaneGround { HasRoad = true, RoadZ = 50, Surface = SurfaceGroup.Dirt };
            ArcadeVehicle v = At(spec, g, 0, 35);
            v.SpeedMps = 15f;
            var log = new List<StepEvents>();
            for (int i = 0; i < 150; i++) log.Add(v.Step(Full, Dt60, g, 0f));
            StepEvents all = StepEvents.None;
            foreach (StepEvents e in log) all |= e;
            Assert.That(all & StepEvents.SurfaceChanged, Is.EqualTo(StepEvents.SurfaceChanged), "dirt -> paved -> dirt");
            Assert.That(all & StepEvents.OffRoad, Is.EqualTo(StepEvents.OffRoad));
            Assert.That(all & StepEvents.Bump, Is.EqualTo(StepEvents.Bump), "a sudden 0.25 m kerb at 15 m/s");
            Assert.That(v.LastBumpStrength, Is.GreaterThan(spec.BumpThresholdMps));
            Assert.That(all & (StepEvents.Landed | StepEvents.StuckRecovered), Is.EqualTo(StepEvents.None));
            Assert.That(v.OnRoad, Is.False);
            Assert.That(v.Surface, Is.EqualTo(SurfaceGroup.Dirt));

            // Slowly onto a kerb ramp: no bump.
            var ramp = new PlaneGround
            {
                HeightAt = (x, z) => 1300.0 + (z < 50 ? 0.0 : z > 50.5 ? 0.25 : (z - 50) * 0.5),
            };
            ArcadeVehicle slow = At(spec, ramp, 0, 45);
            slow.SpeedMps = 2f;
            Assert.That(Run(slow, ramp, new DriveInput(0.08f, 0f, 0f), 4f) & StepEvents.Bump, Is.EqualTo(StepEvents.None));
            Assert.That(slow.Z, Is.GreaterThan(51.0));
        }

        [Test]
        public void CrestLaunchesAndLands()
        {
            // A 25 % ramp ending in a flat plateau: at speed the bike leaves the ground and lands.
            var g = new PlaneGround { HeightAt = (x, z) => 1300.0 + (z < 0 ? 0 : z < 40 ? 0.25 * z : 10.0) };
            ArcadeVehicle v = At(VehicleSpec.Motorbike(), g, 0, -10);
            v.SpeedMps = 20f;
            bool wasAirborne = false;
            StepEvents all = StepEvents.None;
            for (int i = 0; i < 300; i++)
            {
                all |= v.Step(Full, Dt60, g, 0f);
                if (v.Airborne)
                {
                    wasAirborne = true;
                    GroundSample s;
                    g.TrySample(v.X, v.Z, out s);
                    Assert.That(v.Y, Is.GreaterThanOrEqualTo(s.Height - 1e-3f), "never below the ground");
                }
            }
            Assert.That(wasAirborne, Is.True);
            Assert.That(all & StepEvents.Landed, Is.EqualTo(StepEvents.Landed));
            Assert.That(v.Airborne, Is.False);
            Assert.That(v.LastLandingSpeedMps, Is.GreaterThan(0f));
            Assert.That(v.Y, Is.EqualTo(1310f).Within(1e-3));

            // Driving off a 5 m drop falls instead of snapping down.
            var drop = new PlaneGround { HeightAt = (x, z) => z < 10 ? 1305.0 : 1300.0 };
            ArcadeVehicle d = At(VehicleSpec.Taxi(), drop, 0, 0);
            d.SpeedMps = 10f;
            StepEvents ev = StepEvents.None;
            bool fell = false;
            for (int i = 0; i < 120; i++)
            {
                ev |= d.Step(Full, Dt60, drop, 0f);
                fell |= d.Airborne;
            }
            Assert.That(fell, Is.True);
            Assert.That(ev & StepEvents.Landed, Is.EqualTo(StepEvents.Landed));
            Assert.That(d.LastLandingSpeedMps, Is.EqualTo(Math.Sqrt(2 * 9.81 * 5)).Within(0.6));
        }

        [Test]
        public void StuckRecoveryHopsAndNudgesAfterThreeSeconds()
        {
            VehicleSpec spec = VehicleSpec.Motorbike();
            // Facing a 40 degree wall, throttle held: no progress, so after ~3 s it hops and nudges.
            var g = new PlaneGround { HeightAt = (x, z) => 1300.0 + (z > 2 ? (z - 2) * Math.Tan(40 * Math.PI / 180) : 0.0) };
            ArcadeVehicle v = At(spec, g, 0, 0);
            float t = 0f, firedAt = -1f, landedAt = -1f;
            for (int i = 0; i < 60 * 6; i++)
            {
                StepEvents e = v.Step(Full, Dt60, g, 0f);
                t += Dt60;
                if ((e & StepEvents.StuckRecovered) != 0 && firedAt < 0)
                {
                    firedAt = t;
                    Assert.That(v.Airborne, Is.True, "the hop");
                }
                if ((e & StepEvents.Landed) != 0 && firedAt > 0 && landedAt < 0) landedAt = t;
            }
            Assert.That(firedAt, Is.InRange(3.0f, 4.5f));
            Assert.That(landedAt, Is.GreaterThan(firedAt));
            Assert.That(landedAt - firedAt, Is.LessThan(1.2f));
            Assert.That(v.Airborne, Is.False);
            // No road nearby: it turned to the first climbable direction (not up the wall) and nudged.
            Assert.That(Math.Abs(Math.Cos(v.HeadingRad)), Is.LessThan(0.75), "turned away from the wall");

            // With a road in reach it hops onto the road, facing along it.
            var withRoad = new PlaneGround
            {
                HeightAt = (x, z) => 1300.0 + (z > 2 ? (z - 2) * Math.Tan(40 * Math.PI / 180) : 0.0),
                HasRoad = true, RoadZ = -12, RoadHalfWidth = 2.5f,
            };
            ArcadeVehicle r = At(spec, withRoad, 0, 0);
            StepEvents all = Run(r, withRoad, Full, 4.5f);
            Assert.That(all & StepEvents.StuckRecovered, Is.EqualTo(StepEvents.StuckRecovered));
            Run(r, withRoad, Idle, 1.5f);
            Assert.That(r.Z, Is.EqualTo(-12.0).Within(0.6), "landed on the road");
            Assert.That(r.OnRoad, Is.True);
            Assert.That(Math.Abs(Math.Sin(r.HeadingRad)), Is.EqualTo(1.0).Within(1e-3), "facing along the road");

            // The walker has no automatic recovery; a manual Recover() still works.
            VehicleSpec walker = VehicleSpec.Walker();
            ArcadeVehicle w = At(walker, g, 0, 0);
            Assert.That(Run(w, g, Full, 5f) & StepEvents.StuckRecovered, Is.EqualTo(StepEvents.None));
            Assert.That(w.Recover(g), Is.True);
            Assert.That(w.Airborne, Is.True);
        }

        [Test]
        public void MudIsNeverASoftLock()
        {
            // Deep mud in a bowl: whatever the player holds, the bike keeps moving or gets recovered.
            var g = new PlaneGround
            {
                Surface = SurfaceGroup.Mud,
                HeightAt = (x, z) => 1300.0 + 0.004 * (x * x + z * z),
            };
            ArcadeVehicle v = At(VehicleSpec.Motorbike(), g, 0, 0);
            double far = 0;
            for (int i = 0; i < 60 * 20; i++)
            {
                v.Step(Full, Dt60, g, 1f);
                far = Math.Max(far, Math.Sqrt(v.X * v.X + v.Z * v.Z));
            }
            Assert.That(far, Is.GreaterThan(10.0));
        }

        [Test]
        public void RoadAssistAlignsGentleSteering()
        {
            VehicleSpec spec = VehicleSpec.Motorbike();
            var g = new PlaneGround { HasRoad = true, RoadZ = 0, RoadHalfWidth = 6f };
            // Riding east along an east-west road but 10 degrees off.
            float start = (90f - 10f) * Deg;
            ArcadeVehicle on = At(spec, g, 0, 0, start), off = At(spec, g, 0, 0, start);
            off.RoadAssist = false;
            on.SpeedMps = off.SpeedMps = 12f;
            var gentle = new DriveInput(0.6f, 0f, 0f);
            Run(on, g, gentle, 0.8f);
            Run(off, g, gentle, 0.8f);
            Assert.That(Math.Abs(on.HeadingRad - 90f * Deg), Is.LessThan(5f * Deg), "pulled towards the road direction");
            Assert.That(Math.Abs(off.HeadingRad - start), Is.LessThan(1e-4f), "no assist: heading kept");
            // Hard steering overrides the magnet.
            ArcadeVehicle hard = At(spec, g, 0, 0, 90f * Deg);
            hard.SpeedMps = 12f;
            Run(hard, g, new DriveInput(0.6f, 0f, -1f), 0.5f);
            Assert.That(hard.HeadingRad, Is.LessThan(75f * Deg));
            // The walker has no magnet.
            Assert.That(VehicleSpec.Walker().RoadAssistPerS, Is.EqualTo(0f));
        }

        /// <summary>A scripted ride whose inputs change every 0.1 s (so 30, 60 and 120 fps see the same inputs).</summary>
        private static DriveInput Scripted(double t)
        {
            t = Math.Floor(t * 10.0 + 1e-6) / 10.0;
            return new DriveInput(
                (float)(t < 8 ? 1.0 : t < 10 ? -0.5 : 0.8),
                (float)(t > 14 && t < 15 ? 1.0 : 0.0),
                (float)Math.Sin(t * 0.9) * (t > 3 ? 1f : 0.2f),
                t > 16);
        }

        private static ArcadeVehicle Drive(VehicleSpec spec, IGroundQuery g, float dt, double seconds, double x = 0, double z = 0, float wet = 0f)
        {
            ArcadeVehicle v = At(spec, g, x, z);
            int frames = (int)Math.Round(seconds / dt);
            double fps = Math.Round(1.0 / dt);
            for (int i = 0; i < frames; i++) v.Step(Scripted(i / fps), dt, g, wet);
            return v;
        }

        [Test]
        public void SameInputsSameTrajectoryAt30And60Fps()
        {
            var g = new PlaneGround
            {
                HeightAt = (x, z) => 1300.0 + 3.0 * Math.Sin(x * 0.02) + 2.0 * Math.Cos(z * 0.03),
                SurfaceAt = (x, z) => (Math.Floor(x / 40.0) + Math.Floor(z / 40.0)) % 2 == 0 ? SurfaceGroup.Paved : SurfaceGroup.Dirt,
            };
            foreach (VehicleSpec spec in new[] { VehicleSpec.Motorbike(), VehicleSpec.Taxi(), VehicleSpec.Walker() })
            {
                ArcadeVehicle a = Drive(spec, g, 1f / 30f, 20.0), b = Drive(spec, g, 1f / 60f, 20.0);
                double d = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
                double travelled = Math.Sqrt(a.X * a.X + a.Z * a.Z);
                Assert.That(travelled, Is.GreaterThan(spec.TurnInPlace ? 5.0 : 50.0), spec.Name);
                Assert.That(d, Is.LessThan(0.05), spec.Name + " position at 30 vs 60 fps");
                Assert.That(Math.Abs(ArcadeVehicle.WrapAngle(a.HeadingRad - b.HeadingRad)), Is.LessThan(0.005f), spec.Name + " heading");
                Assert.That(a.SpeedMps, Is.EqualTo(b.SpeedMps).Within(0.02), spec.Name + " speed");

                // Bit-for-bit repeatable at the same frame rate.
                ArcadeVehicle c = Drive(spec, g, 1f / 60f, 20.0);
                Assert.That(c.X, Is.EqualTo(b.X));
                Assert.That(c.Z, Is.EqualTo(b.Z));
                Assert.That(c.HeadingRad, Is.EqualTo(b.HeadingRad));
                Assert.That(c.Y, Is.EqualTo(b.Y));
            }
            // An uneven frame rate (inputs then change mid-frame) also stays close.
            ArcadeVehicle smooth = Drive(VehicleSpec.Motorbike(), g, 1f / 120f, 20.0);
            ArcadeVehicle jittery = At(VehicleSpec.Motorbike(), g);
            double t = 0;
            var rng = new Random(5);
            while (t < 20.0 - 1e-9)
            {
                float dt = (float)Math.Min(20.0 - t, 1.0 / 120 * (1 + rng.Next(8)));
                jittery.Step(Scripted(t), dt, g, 0f);
                t += dt;
            }
            double dj = Math.Sqrt((smooth.X - jittery.X) * (smooth.X - jittery.X) + (smooth.Z - jittery.Z) * (smooth.Z - jittery.Z));
            Assert.That(dj, Is.LessThan(3.0), "inputs are sampled per frame, so only input timing differs");
        }

        [Test]
        public void NeverNaN()
        {
            var g = new PlaneGround { HeightAt = (x, z) => 1300.0 + 30.0 * Math.Sin(x * 0.05) * Math.Cos(z * 0.05) };
            var rng = new Random(99);
            float[] odd = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1e30f, -1e30f, 0f, 1f, -1f };
            foreach (VehicleSpec spec in new[] { VehicleSpec.Motorbike(), VehicleSpec.Taxi(), VehicleSpec.Walker() })
            {
                ArcadeVehicle v = At(spec, g);
                for (int i = 0; i < 3000; i++)
                {
                    var input = new DriveInput(
                        i % 7 == 0 ? odd[rng.Next(odd.Length)] : (float)(rng.NextDouble() * 2 - 1),
                        i % 11 == 0 ? odd[rng.Next(odd.Length)] : (float)rng.NextDouble(),
                        i % 13 == 0 ? odd[rng.Next(odd.Length)] : (float)(rng.NextDouble() * 2 - 1),
                        rng.Next(2) == 0);
                    float dt = i % 17 == 0 ? odd[rng.Next(odd.Length)] : (float)(rng.NextDouble() * 0.1);
                    float wet = i % 19 == 0 ? float.NaN : (float)rng.NextDouble();
                    v.Step(input, dt, g, wet);
                    Assert.That(Finite(v.X) && Finite(v.Z) && Finite(v.Y) && Finite(v.HeadingRad) && Finite(v.SpeedMps)
                                && Finite(v.Pitch) && Finite(v.Roll) && Finite(v.Lean), spec.Name + " step " + i);
                    Assert.That(v.HeadingRad, Is.InRange(-Math.PI, Math.PI));
                }
            }
            Assert.That(ArcadeVehicle.WrapAngle(float.NaN), Is.EqualTo(0f));
            Assert.That(ArcadeVehicle.WrapAngle(7f), Is.EqualTo(7f - 2f * (float)Math.PI).Within(1e-5));
            Assert.Throws<ArgumentException>(() => new ArcadeVehicle(VehicleSpec.Taxi()).Teleport(double.NaN, 0, 0, g));
            Assert.Throws<ArgumentNullException>(() => new ArcadeVehicle(VehicleSpec.Taxi()).Step(Full, Dt60, null, 0f));
        }

        private static bool Finite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        [Test]
        public void NoGroundIsAWall()
        {
            var g = new PlaneGround { MaxZ = 30 };
            ArcadeVehicle v = At(VehicleSpec.Motorbike(), g);
            Run(v, g, Full, 5f);
            Assert.That(v.Z, Is.LessThanOrEqualTo(30.0));
            Assert.That(v.Z, Is.GreaterThan(29.0));

            // Teleported into the void: it waits, then stands on the ground once it appears.
            var late = new PlaneGround { MinX = 1000 };
            var w = new ArcadeVehicle(VehicleSpec.Walker());
            w.Teleport(0, 0, 0f, late);
            Assert.That(w.HasGround, Is.False);
            Run(w, late, Full, 1f);
            Assert.That(w.Z, Is.EqualTo(0.0));
            late.MinX = double.NegativeInfinity;
            Run(w, late, Full, 1f);
            Assert.That(w.HasGround, Is.True);
            Assert.That(w.Y, Is.EqualTo(1300f));
            Assert.That(w.Z, Is.GreaterThan(1.0));
        }

        [Test]
        public void BridgesCarryAndShelter()
        {
            TileData t;
            TileGroundQuery g = DrivingGroundTests.BridgeValley(out t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            // Across the bridge eastwards: stays on the deck over the valley, never falls in.
            ArcadeVehicle over = At(VehicleSpec.Motorbike(), g, x0 + 360, z0 + 500, 90f * Deg);
            over.SpeedMps = 15f;
            float minY = float.MaxValue;
            StepEvents ev = StepEvents.None;
            for (int i = 0; i < 60 * 30 && over.X < x0 + 640; i++)
            {
                ev |= over.Step(new DriveInput(0.7f, 0f, 0f), Dt60, g, 0f);
                minY = Math.Min(minY, over.Y);
                Assert.That(over.Ground.Ny, Is.GreaterThan(0.99f), "the deck is level, whatever the valley below");
            }
            Assert.That(over.X, Is.GreaterThanOrEqualTo(x0 + 640));
            Assert.That(minY, Is.GreaterThan(1299.5f));
            Assert.That(over.Airborne, Is.False);
            Assert.That(ev & (StepEvents.Bump | StepEvents.Landed | StepEvents.OffRoad), Is.EqualTo(StepEvents.None));

            // Along the riverside track northwards: passes under the deck without popping up onto it.
            ArcadeVehicle under = At(VehicleSpec.Motorbike(), g, x0 + 500, z0 + 420, 0f);
            Assert.That(under.Y, Is.LessThan(1281f));
            under.SpeedMps = 8f;
            float maxY = float.MinValue;
            StepEvents underEv = StepEvents.None;
            for (int i = 0; i < 60 * 30 && under.Z < z0 + 580; i++)
            {
                underEv |= under.Step(new DriveInput(0.6f, 0f, 0f), Dt60, g, 0f);
                maxY = Math.Max(maxY, under.Y);
                Assert.That(under.Airborne, Is.False);
            }
            Assert.That(under.Z, Is.GreaterThan(z0 + 560));
            Assert.That(maxY, Is.LessThan(1281f), "stayed under the bridge");
            Assert.That(underEv, Is.EqualTo(StepEvents.None), "no hops, bumps or surface flips under the deck");
        }

        // ---- the real sample region ----

        [Test]
        public void SteppingDoesNotAllocate()
        {
            TileGroundQuery g = DrivingData.SampleGround();
            double x, z;
            WorldFrame.LonLatToGame(85.31172094019205, 27.716693189023914, out x, out z); // Thamel Marg
            var bike = new ArcadeVehicle(VehicleSpec.Motorbike());
            var walker = new ArcadeVehicle(VehicleSpec.Walker());
            bike.Teleport(x, z, 1.2f, g);
            walker.Teleport(x, z, -0.4f, g);
            var input = new DriveInput(1f, 0f, 0.3f);
            for (int i = 0; i < 120; i++)
            {
                bike.Step(input, Dt60, g, 0.3f);
                walker.Step(input, Dt60, g, 0.3f);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            GroundSample s;
            RoadHit hit;
            for (int i = 0; i < 600; i++)
            {
                input.Steer = (float)Math.Sin(i * 0.05);
                bike.Step(input, Dt60, g, 0.3f);
                walker.Step(input, Dt60, g, 0.3f);
                g.TrySample(x + i, z - i, out s);
                g.TryNearestRoad(x + i, z + i, 30, out hit);
            }
            bike.Recover(g);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0), "no per-frame allocations");
        }

        [Test]
        public void RidesThroughRealKathmandu()
        {
            TileGroundQuery g = DrivingData.SampleGround();
            double x, z, bx, bz;
            WorldFrame.LonLatToGame(85.31172094019205, 27.716693189023914, out x, out z); // on Thamel Marg
            WorldFrame.LonLatToGame(85.362, 27.7215, out bx, out bz); // Boudhanath
            var v = new ArcadeVehicle(VehicleSpec.Motorbike());
            float heading = (float)Math.Atan2(bx - x, bz - z);
            v.Teleport(x, z, heading, g);
            Assert.That(v.HasGround, Is.True);
            Assert.That(v.OnRoad, Is.True);
            Assert.That(v.Surface, Is.EqualTo(SurfaceGroup.Paved));

            // Head for Boudhanath in a straight line (no buildings in Core), at most 10 minutes.
            int groundedSteps = 0, onRoadSteps = 0;
            StepEvents all = StepEvents.None;
            for (int i = 0; i < 60 * 600; i++)
            {
                float want = (float)Math.Atan2(bx - v.X, bz - v.Z);
                var input = new DriveInput(1f, 0f, v.SteerToward(want, Dt60));
                all |= v.Step(input, Dt60, g, 0f);
                if (!v.Airborne)
                {
                    groundedSteps++;
                    GroundSample s;
                    Assert.That(g.TrySample(v.X, v.Z, out s), Is.True);
                    Assert.That(v.Y, Is.EqualTo(s.Height).Within(1e-3), "on the rendered ground");
                    Assert.That(v.Y, Is.InRange(1250f, 1450f));
                    if (s.OnRoad) onRoadSteps++;
                }
                if ((bx - v.X) * (bx - v.X) + (bz - v.Z) * (bz - v.Z) < 30 * 30) break;
            }
            double left = Math.Sqrt((bx - v.X) * (bx - v.X) + (bz - v.Z) * (bz - v.Z));
            Assert.That(left, Is.LessThan(30.0), "reached Boudhanath");
            Assert.That(onRoadSteps, Is.GreaterThan(groundedSteps / 50), "crossed plenty of streets");
            Assert.That(all & StepEvents.SurfaceChanged, Is.EqualTo(StepEvents.SurfaceChanged));
        }
    }
}
