using System;
using Ghumante.Core.Driving;
using Ghumante.Vehicles;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The explorer's simulation (M1 track D): Core's ArcadeVehicle at a fixed step with interpolation, the same at 30
    /// and 60 fps, hitches clamped, wheels rolling with the distance, and Explore's scooter tuning. Engine-free.
    /// </summary>
    public class ExploreDriverTests
    {
        private static FixedStepDriver Scooter(IGroundQuery ground)
        {
            var driver = new FixedStepDriver(new ArcadeVehicle(VehicleTuning.Motorbike()));
            driver.Teleport(0, 0, 0f, ground);
            return driver;
        }

        [Test]
        public void ThirtyAndSixtyFramesPerSecondRideTheSameRoad()
        {
            var ground = new FlatGround();
            FixedStepDriver at60 = Scooter(ground), at30 = Scooter(ground);
            var input = new DriveInput(1f, 0f, 0.3f);
            for (int i = 0; i < 120; i++) at60.Advance(1f / 60f, input, ground, 0f);
            for (int i = 0; i < 60; i++) at30.Advance(1f / 30f, input, ground, 0f);
            Assert.AreEqual(at60.Steps, at30.Steps);
            Assert.AreEqual(at60.Current.X, at30.Current.X, 1e-6);
            Assert.AreEqual(at60.Current.Z, at30.Current.Z, 1e-6);
            Assert.AreEqual(at60.Current.HeadingRad, at30.Current.HeadingRad, 1e-6f);
            Assert.Greater(at60.Current.SpeedMps, 5f, "it got going");
        }

        [Test]
        public void DrawsBetweenTheLastTwoSteps()
        {
            var ground = new FlatGround();
            FixedStepDriver d = Scooter(ground);
            var input = new DriveInput(1f, 0f, 0f);
            for (int i = 0; i < 30; i++) d.Advance(1f / 60f, input, ground, 0f);
            d.Advance(0.5f / 60f, input, ground, 0f);
            Assert.AreEqual(0.5f, d.Alpha, 1e-3f);
            VehiclePose mid = d.Interpolated;
            Assert.AreEqual((d.Previous.Z + d.Current.Z) / 2, mid.Z, 1e-4);
            Assert.Greater(d.Current.Z, d.Previous.Z, "heading north");
        }

        [Test]
        public void TeleportDropsTheHistory()
        {
            var ground = new FlatGround();
            FixedStepDriver d = Scooter(ground);
            for (int i = 0; i < 20; i++) d.Advance(1f / 60f, new DriveInput(1f, 0f, 0f), ground, 0f);
            d.Teleport(500, 700, 1f, ground);
            Assert.AreEqual(500, d.Previous.X, 1e-9);
            Assert.AreEqual(500, d.Interpolated.X, 1e-9);
            Assert.AreEqual(0f, d.Accumulator);
            Assert.AreEqual(0f, d.Current.SpeedMps);
            Assert.IsTrue(d.Current.HasGround);
            Assert.AreEqual(1300f, d.Current.Y, 1e-3f);
        }

        [Test]
        public void AHitchIsClampedNotSkippedAhead()
        {
            var ground = new FlatGround();
            FixedStepDriver d = Scooter(ground);
            d.Advance(1f, new DriveInput(1f, 0f, 0f), ground, 0f);
            Assert.AreEqual((long)Math.Round(FixedStepDriver.MaxFrameS / FixedStepDriver.DefaultStepS), d.Steps, "a one-second hitch runs 0.25 s");
            Assert.AreEqual(StepEvents.None, d.Advance(float.NaN, new DriveInput(1f, 0f, 0f), ground, 0f));
            Assert.AreEqual(StepEvents.None, d.Advance(-1f, new DriveInput(1f, 0f, 0f), ground, 0f));
        }

        [Test]
        public void WheelsRollWithTheDistanceAndTheOdometerCounts()
        {
            var ground = new FlatGround();
            FixedStepDriver d = Scooter(ground);
            float before = d.Current.WheelAngleRad;
            d.Advance(1f / 60f, new DriveInput(1f, 0f, 0f), ground, 0f);
            for (int i = 0; i < 60; i++) d.Advance(1f / 60f, new DriveInput(1f, 0f, 0f), ground, 0f);
            Assert.AreNotEqual(before, d.Current.WheelAngleRad);
            Assert.AreEqual(d.Current.Z, d.Current.OdometerM, 1e-3, "straight north: the odometer is the distance");
            float step = d.Current.SpeedMps * d.StepSeconds / VehicleTuning.ScooterWheelRadiusM;
            Assert.AreEqual(ArcadeVehicle.WrapAngle(d.Previous.WheelAngleRad + step), d.Current.WheelAngleRad, 1e-4f);
        }

        [Test]
        public void NoGroundYetMeansWaitingInPlace()
        {
            var ground = new FlatGround { HalfSize = 10 };
            var d = new FixedStepDriver(new ArcadeVehicle(VehicleTuning.Motorbike()));
            d.Teleport(5000, 5000, 0f, ground);
            Assert.IsFalse(d.Current.HasGround);
            for (int i = 0; i < 10; i++) d.Advance(1f / 60f, new DriveInput(1f, 0f, 0f), ground, 0f);
            Assert.AreEqual(5000, d.Current.Z, 1e-9, "the world streams in first");
        }

        [Test]
        public void TheExplorersScooterHasABoost()
        {
            VehicleSpec bike = VehicleTuning.Motorbike();
            Assert.AreEqual(VehicleTuning.MotorbikeBoostFactor, bike.BoostSpeedFactor);
            Assert.AreEqual(VehicleSpec.Motorbike().MaxSpeedKmh, bike.MaxSpeedKmh);
            Assert.AreNotSame(VehicleTuning.Motorbike(), VehicleTuning.Motorbike(), "fresh specs every time");
            Assert.IsTrue(VehicleTuning.Walker().TurnInPlace);
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepDriver(new ArcadeVehicle(bike), 0f));
        }
    }
}
