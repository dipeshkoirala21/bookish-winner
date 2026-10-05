using System;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>W2_DESIGN 10.6 V7 (handling): turning radius per class within ±5% of 6.3, the δmax(v) law, the bus body
    /// sweep and two-wheelers that never tip.</summary>
    public class DrivingHandlingTests
    {
        private const float Dt = 1f / 60f;
        private const float Deg = (float)(Math.PI / 180.0);

        /// <summary>W2_DESIGN 6.3: (preset, wheelbase m, δ0 °, min radius at the front axle m).</summary>
        private static readonly object[] Table =
        {
            new object[] { HandlingPreset.Bicycle, 1.12f, 40f, 1.74f },
            new object[] { HandlingPreset.Scooter, 1.26f, 38f, 2.05f },
            new object[] { HandlingPreset.Motorbike, 1.30f, 35f, 2.27f },
            new object[] { HandlingPreset.Cruiser, 1.39f, 33f, 2.55f },
            new object[] { HandlingPreset.Taxi, 2.36f, 34f, 4.2f },
            new object[] { HandlingPreset.Hatchback, 2.43f, 34f, 4.35f },
            new object[] { HandlingPreset.Ev, 2.55f, 34f, 4.6f },
            new object[] { HandlingPreset.Suv, 2.68f, 33f, 4.9f },
            new object[] { HandlingPreset.Microbus, 2.57f, 34f, 4.6f },
            new object[] { HandlingPreset.Tempo, 2.10f, 38f, 3.4f },
            new object[] { HandlingPreset.Minibus, 3.8f, 38f, 6.2f },
            new object[] { HandlingPreset.Bus, 5.4f, 42f, 8.1f },
            new object[] { HandlingPreset.Coach, 5.6f, 42f, 8.4f },
            new object[] { HandlingPreset.Truck, 4.8f, 40f, 7.5f },
            new object[] { HandlingPreset.Tipper, 4.48f, 36.7f, 7.5f }, // tandem: effective wheelbase (5.2)
            new object[] { HandlingPreset.Tanker, 4.8f, 40f, 7.5f },
            new object[] { HandlingPreset.Tractor, 1.95f, 38f, 3.2f },
        };

        [Test]
        public void EveryPresetIsValid()
        {
            Assert.That(VehicleSpec.PresetCount, Is.EqualTo(21));
            for (int p = 0; p < VehicleSpec.PresetCount; p++)
            {
                VehicleSpec s = VehicleSpec.For((HandlingPreset)p);
                Assert.DoesNotThrow(s.Validate, ((HandlingPreset)p).ToString());
                Assert.That(s.Preset, Is.EqualTo((HandlingPreset)p));
                Assert.That(s.SteerInputDelayS, Is.InRange(0f, VehicleSpec.MaxSteerDelayS));
            }
            Assert.That(VehicleSpec.For(HandlingPreset.Bus).SteerInputDelayS, Is.EqualTo(0.25f));
            Assert.That(VehicleSpec.For(HandlingPreset.Motorbike).MaxSpeedKmh, Is.EqualTo(85f), "W1 values kept [R]");
            Assert.That(VehicleSpec.For(VehicleKind.Taxi).MaxSpeedKmh, Is.EqualTo(75f));
        }

        [TestCaseSource(nameof(Table))]
        public void TurningRadiusMatchesTheTable(HandlingPreset p, float wheelbase, float d0, float minR)
        {
            VehicleSpec s = VehicleSpec.For(p);
            Assert.That(s.WheelbaseM, Is.EqualTo(wheelbase).Within(0.06f), "wheelbase");
            Assert.That(s.MaxSteerDeg, Is.EqualTo(d0).Within(0.01f), "δ0");
            Assert.That(s.MinTurnRadiusFrontM, Is.EqualTo(minR).Within(minR * 0.05f), "L / sin δ0");

            // Driven: full lock at walking pace traces the front-axle circle of the table (±5%).
            var g = new PlaneGround();
            var v = new ArcadeVehicle(s);
            v.RoadAssist = false;
            v.Teleport(0, 0, 0f, g);
            double sx = 0, sz = 0;
            int n = 0;
            var xs = new double[4000];
            var zs = new double[4000];
            for (int i = 0; i < 60 * 40 && n < xs.Length; i++)
            {
                float throttle = v.SpeedMps < 1.6f ? 0.6f : 0f;
                v.Step(new DriveInput(throttle, 0f, 1f), Dt, g, 0f);
                if (i < 60 * 6 || i % 4 != 0) continue;
                double fx = v.X + Math.Sin(v.HeadingRad) * s.WheelbaseM, fz = v.Z + Math.Cos(v.HeadingRad) * s.WheelbaseM;
                xs[n] = fx;
                zs[n] = fz;
                sx += fx;
                sz += fz;
                n++;
            }
            // Least-squares circle (Kåsa): x² + z² + D x + E z + F = 0.
            double cx, cz, r;
            FitCircle(xs, zs, n, out cx, out cz, out r);
            Assert.That(r, Is.EqualTo(minR).Within(minR * 0.05), "driven front-axle radius of " + p);
        }

        private static void FitCircle(double[] xs, double[] zs, int n, out double cx, out double cz, out double r)
        {
            double mx = 0, mz = 0;
            for (int i = 0; i < n; i++)
            {
                mx += xs[i];
                mz += zs[i];
            }
            mx /= n;
            mz /= n;
            double suu = 0, svv = 0, suv = 0, suuu = 0, svvv = 0, suvv = 0, svuu = 0;
            for (int i = 0; i < n; i++)
            {
                double u = xs[i] - mx, w = zs[i] - mz;
                suu += u * u;
                svv += w * w;
                suv += u * w;
                suuu += u * u * u;
                svvv += w * w * w;
                suvv += u * w * w;
                svuu += w * u * u;
            }
            double a1 = 0.5 * (suuu + suvv), a2 = 0.5 * (svvv + svuu);
            double det = suu * svv - suv * suv;
            double uc = (a1 * svv - a2 * suv) / det, vc = (a2 * suu - a1 * suv) / det;
            cx = mx + uc;
            cz = mz + vc;
            r = Math.Sqrt(uc * uc + vc * vc + (suu + svv) / n);
        }

        [Test]
        public void SteeringFollowsTheSpeedLaw()
        {
            foreach (HandlingPreset p in new[] { HandlingPreset.Scooter, HandlingPreset.Taxi, HandlingPreset.Bus, HandlingPreset.Truck })
            {
                VehicleSpec s = VehicleSpec.For(p);
                float d0 = s.MaxSteerDeg * Deg;
                Assert.That(s.MaxSteerAtRad(0f), Is.EqualTo(d0).Within(1e-6f));
                foreach (float v in new[] { 2f, 5f, 10f, 15f, 20f, 25f })
                {
                    float law = Math.Min(d0, (float)Math.Atan(s.WheelbaseM * s.SteerLateralMps2 / (v * v)));
                    Assert.That(s.MaxSteerAtRad(v), Is.EqualTo(law).Within(1e-5f), p + " at " + v);
                }
                Assert.That(s.MaxSteerAtRad(25f), Is.LessThan(s.MaxSteerAtRad(10f)));

                // Driven at speed with full lock: the wheel angle never passes the law (lateral acceleration capped).
                var g = new PlaneGround();
                var car = new ArcadeVehicle(s);
                car.RoadAssist = false;
                car.Teleport(0, 0, 0f, g);
                for (int i = 0; i < 60 * 20; i++) car.Step(new DriveInput(1f, 0f, 0f), Dt, g, 0f);
                for (int i = 0; i < 60 * 3; i++)
                {
                    car.Step(new DriveInput(1f, 0f, 1f), Dt, g, 0f);
                    Assert.That(Math.Abs(car.SteerAngleRad), Is.LessThanOrEqualTo(s.MaxSteerAtRad(car.SpeedMps) + 0.02f),
                                p + " steer at " + car.SpeedMps);
                }
            }
        }

        [Test]
        public void BusBodySweepStaysInsideTheBusCircle()
        {
            foreach (HandlingPreset p in new[] { HandlingPreset.Bus, HandlingPreset.Coach, HandlingPreset.SchoolBus, HandlingPreset.Minibus })
            {
                VehicleSpec s = VehicleSpec.For(p);
                Assert.That(s.BodySweepRadiusM, Is.LessThanOrEqualTo(11f), p.ToString());
            }
            VehicleSpec bus = VehicleSpec.For(HandlingPreset.Bus);
            Assert.That(bus.BodySweepRadiusM, Is.GreaterThanOrEqualTo(10f), "≈ 10.5–11 m (6.3)");
        }

        [Test]
        public void TwoWheelersNeverTip()
        {
            var rng = new Random(5);
            Func<double, double, double> bumps = (x, z) => 1300.0 + 0.4 * Math.Sin(x * 0.7) * Math.Cos(z * 0.5) + 0.08 * x;
            var g = new PlaneGround { HeightAt = bumps, Surface = SurfaceGroup.Dirt };
            foreach (HandlingPreset p in new[] { HandlingPreset.Bicycle, HandlingPreset.Scooter, HandlingPreset.EScooter, HandlingPreset.Motorbike, HandlingPreset.Cruiser })
            {
                VehicleSpec s = VehicleSpec.For(p);
                Assert.That(s.TwoWheeler, Is.True, p.ToString());
                var v = new ArcadeVehicle(s);
                v.Teleport(0, 0, 0f, g);
                var input = new DriveInput(1f, 0f, 0f);
                bool footDown = false;
                for (int i = 0; i < 60 * 90; i++)
                {
                    if (i % 30 == 0)
                        input = new DriveInput((float)rng.NextDouble(), rng.NextDouble() < 0.2 ? 1f : 0f, (float)(rng.NextDouble() * 2 - 1),
                                               rng.NextDouble() < 0.3);
                    v.Step(input, Dt, g, 1f);
                    Assert.That(float.IsNaN(v.Lean) || float.IsNaN(v.Roll), Is.False);
                    Assert.That(Math.Abs(v.Lean), Is.LessThanOrEqualTo(38f * Deg + 1e-3f), p + " lean");
                    Assert.That(Math.Abs(v.Roll), Is.LessThan(30f * Deg), p + " roll");
                    footDown |= v.FootDown;
                }
                // Stopped: upright on a foot, the wobble within ±3°.
                for (int i = 0; i < 60 * 8; i++) v.Step(new DriveInput(0f, 1f, 0f), Dt, g, 0f);
                Assert.That(v.SpeedMps, Is.LessThan(0.3f));
                Assert.That(v.FootDown, Is.True, p + " foot down when stopped");
                Assert.That(Math.Abs(v.Lean), Is.LessThanOrEqualTo(3f * Deg + 1e-3f), p + " upright when stopped");
                Assert.That(footDown, Is.True);
            }
        }

        [Test]
        public void StaminaBoostsTheBicycleAndRefills()
        {
            VehicleSpec s = VehicleSpec.For(HandlingPreset.Bicycle);
            var g = new PlaneGround();
            var v = new ArcadeVehicle(s);
            v.Teleport(0, 0, 0f, g);
            for (int i = 0; i < 60 * 30; i++) v.Step(new DriveInput(1f, 0f, 0f), Dt, g, 0f);
            Assert.That(v.SpeedMps * 3.6f, Is.EqualTo(25f).Within(0.6f), "pedalling top speed");
            float before = v.Stamina;
            for (int i = 0; i < 60 * 3; i++) v.Step(new DriveInput(1f, 0f, 0f, true), Dt, g, 0f);
            Assert.That(v.SpeedMps * 3.6f, Is.GreaterThan(25.5f), "stamina boost");
            Assert.That(v.Stamina, Is.LessThan(before));
            // From full, the boost lasts ≈ 12 s; released, the stamina refills in ≈ 8 s.
            for (int i = 0; i < 60 * 10; i++) v.Step(new DriveInput(1f, 0f, 0f), Dt, g, 0f);
            Assert.That(v.Stamina, Is.EqualTo(1f));
            int frames = 0;
            while (v.Stamina > 0f && frames < 60 * 20)
            {
                v.Step(new DriveInput(1f, 0f, 0f, true), Dt, g, 0f);
                frames++;
            }
            Assert.That(frames * Dt, Is.EqualTo(12f).Within(0.1f), "drain time");
            frames = 0;
            while (v.Stamina < 1f && frames < 60 * 20)
            {
                v.Step(new DriveInput(1f, 0f, 0f), Dt, g, 0f);
                frames++;
            }
            Assert.That(frames * Dt, Is.EqualTo(8f).Within(0.1f), "refill time");

            // A 12% grade: pushed at 1.2 m/s.
            var hill = new PlaneGround { GradeZ = 0.12 };
            var b = new ArcadeVehicle(s);
            b.Teleport(0, 0, 0f, hill);
            for (int i = 0; i < 60 * 20; i++) b.Step(new DriveInput(1f, 0f, 0f), Dt, hill, 0f);
            Assert.That(b.Pushing, Is.True);
            Assert.That(b.SpeedMps, Is.EqualTo(s.PushSpeedMps).Within(0.15f));
        }

        [Test]
        public void HeavyVehiclesDelayTheSteeringAndEngineBrake()
        {
            VehicleSpec bus = VehicleSpec.For(HandlingPreset.Bus);
            var g = new PlaneGround();
            var v = new ArcadeVehicle(bus);
            v.RoadAssist = false;
            v.Teleport(0, 0, 0f, g);
            for (int i = 0; i < 60 * 5; i++) v.Step(new DriveInput(1f, 0f, 0f), Dt, g, 0f);
            for (int i = 0; i < 12; i++) v.Step(new DriveInput(1f, 0f, 1f), Dt, g, 0f); // 0.2 s
            Assert.That(Math.Abs(v.SteerAngleRad), Is.LessThan(0.01f), "0.25 s steering delay");
            for (int i = 0; i < 30; i++) v.Step(new DriveInput(1f, 0f, 1f), Dt, g, 0f);
            Assert.That(v.SteerAngleRad, Is.GreaterThan(0.01f));

            VehicleSpec truck = VehicleSpec.For(HandlingPreset.Truck);
            Assert.That(truck.EngineBrakeMps2, Is.EqualTo(1.2f));
            var t = new ArcadeVehicle(truck);
            t.Teleport(0, 0, 0f, g);
            for (int i = 0; i < 60 * 15; i++) t.Step(new DriveInput(1f, 0f, 0f), Dt, g, 0f);
            float v0 = t.SpeedMps;
            for (int i = 0; i < 60; i++) t.Step(new DriveInput(0f, 0f, 0f), Dt, g, 0f);
            Assert.That(v0 - t.SpeedMps, Is.GreaterThan(1.0f), "engine brake off throttle");
        }
    }
}
