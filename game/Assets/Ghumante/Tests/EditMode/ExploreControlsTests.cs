using System;
using Ghumante.App.Explore;
using Ghumante.Characters;
using Ghumante.Characters.Cameras;
using Ghumante.Core.Driving;
using Ghumante.Core.Services;
using Ghumante.UI.Hud;
using Ghumante.Vehicles;
using Ghumante.World.Cameras;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// Input mapping of Explore (M1 track D): keyboard, gamepad and touch controls into Core's DriveInput, the touch
    /// stick and drive-zone maths, when touch controls show, which haptics driving plays, and the chase rig's framing
    /// per orientation. Engine-free.
    /// </summary>
    public class ExploreControlsTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void DeadZoneSwallowsRestingThumbsAndRescalesTheRest()
        {
            float x, y;
            ControlMapper.ApplyDeadZone(0.1f, 0.05f, ControlMapper.StickDeadZone, out x, out y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
            ControlMapper.ApplyDeadZone(0.575f, 0f, ControlMapper.StickDeadZone, out x, out y);
            Assert.AreEqual(0.5f, x, Eps, "halfway between the dead zone and the rim reads half");
            ControlMapper.ApplyDeadZone(-3f, 0f, ControlMapper.StickDeadZone, out x, out y);
            Assert.AreEqual(-1f, x, Eps, "clamped to the unit circle");
            ControlMapper.ApplyDeadZone(float.NaN, float.PositiveInfinity, ControlMapper.StickDeadZone, out x, out y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
        }

        [Test]
        public void RidingMapsPedalsToThrottleAndTheStickToSteering()
        {
            DriveInput go = ControlMapper.Ride(new ControlFrame { Throttle = 1f, MoveX = 1f });
            Assert.AreEqual(1f, go.Throttle, Eps);
            Assert.AreEqual(1f, go.Steer, Eps);
            Assert.AreEqual(0f, go.Brake, Eps);

            DriveInput back = ControlMapper.Ride(new ControlFrame { Reverse = 1f, MoveX = -0.575f });
            Assert.AreEqual(-1f, back.Throttle, Eps, "S / left trigger / the brake pedal brake, then reverse");
            Assert.AreEqual(-0.5f, back.Steer, Eps);

            DriveInput brake = ControlMapper.Ride(new ControlFrame { Throttle = 0.8f, Brake = 1f, Boost = true });
            Assert.AreEqual(0.8f, brake.Throttle, Eps);
            Assert.AreEqual(1f, brake.Brake, Eps, "Space / B brake without reversing");
            Assert.IsTrue(brake.Boost);

            // Regression: Go and Brake held together (RT + LT, W + S, the portrait drive zone + the Brake pedal) used to
            // net out to a coast; it brakes.
            DriveInput both = ControlMapper.Ride(new ControlFrame { Throttle = 1f, Reverse = 1f });
            Assert.AreEqual(0f, both.Throttle, Eps, "the brake cuts the throttle");
            Assert.AreEqual(1f, both.Brake, Eps, "full brake, not a coast");
            DriveInput squeeze = ControlMapper.Ride(new ControlFrame { Throttle = 1f, Reverse = 0.4f, Brake = 0.2f });
            Assert.AreEqual(0f, squeeze.Throttle, Eps);
            Assert.AreEqual(0.4f, squeeze.Brake, Eps, "a squeezed trigger brakes as hard as it is pressed");

            DriveInput stickForward = ControlMapper.Ride(new ControlFrame { MoveY = 1f });
            Assert.AreEqual(0f, stickForward.Throttle, Eps, "on the scooter the stick only steers; pedals and W drive");
        }

        [Test]
        public void WalkingIsRelativeToTheCamera()
        {
            float heading, throttle;
            float east = (float)(Math.PI / 2);
            Assert.IsTrue(ControlMapper.Walk(new ControlFrame { MoveY = 1f }, east, out heading, out throttle));
            Assert.AreEqual(east, heading, Eps, "stick up walks away from a camera looking east");
            Assert.AreEqual(1f, throttle, Eps, "full stick runs");

            Assert.IsTrue(ControlMapper.Walk(new ControlFrame { MoveX = 1f }, east, out heading, out throttle));
            Assert.AreEqual((float)Math.PI, Math.Abs(heading), Eps, "stick right walks to the camera's right: south");

            Assert.IsTrue(ControlMapper.Walk(new ControlFrame { MoveY = -0.575f }, 0f, out heading, out throttle));
            Assert.AreEqual((float)Math.PI, Math.Abs(heading), Eps, "stick down walks towards the camera");
            Assert.AreEqual(0.5f, throttle, Eps, "half stick walks");

            Assert.IsFalse(ControlMapper.Walk(new ControlFrame { MoveX = 0.05f }, 0f, out heading, out throttle));
            Assert.AreEqual(0f, throttle);
        }

        [Test]
        public void TheWalkerTurnsTowardsTheStick()
        {
            var walker = new ArcadeVehicle(VehicleTuning.Walker());
            walker.Teleport(0, 0, 0f, new FlatGround());
            DriveInput input = ControlMapper.WalkInput(walker, new ControlFrame { MoveX = 1f }, 0f, 1f / 60f);
            Assert.AreEqual(1f, input.Throttle, Eps);
            Assert.Greater(input.Steer, 0.99f, "east of north: a full right turn this frame");
            DriveInput rest = ControlMapper.WalkInput(walker, new ControlFrame(), 0f, 1f / 60f);
            Assert.AreEqual(0f, rest.Throttle);
            Assert.AreEqual(0f, rest.Steer);
        }

        [Test]
        public void MergingKeepsTheStrongerSourceAndRecordsTouch()
        {
            var keys = new ControlFrame { MoveX = 1f, Throttle = 1f, Device = ControlDevice.KeyboardMouse, ZoomSteps = 1f };
            var touch = new ControlFrame { MoveX = -0.3f, Reverse = 1f, ZoomSteps = 0.5f, Looking = true, Device = ControlDevice.Touch };
            keys.Merge(touch);
            Assert.AreEqual(1f, keys.MoveX, Eps);
            Assert.AreEqual(1f, keys.Throttle, Eps);
            Assert.AreEqual(1f, keys.Reverse, Eps);
            Assert.AreEqual(1.5f, keys.ZoomSteps, Eps);
            Assert.IsTrue(keys.Looking);
            Assert.AreEqual(ControlDevice.Touch, keys.Device);
            var idle = new ControlFrame { Device = ControlDevice.Gamepad };
            idle.Merge(new ControlFrame());
            Assert.AreEqual(ControlDevice.Gamepad, idle.Device, "an idle touch frame does not claim the device");
        }

        [Test]
        public void TouchStickHasADeadZoneAndKeepsTheKnobOnTheRim()
        {
            float x, y, kx, ky;
            TouchMath.Stick(6f, 0f, out x, out y, out kx, out ky);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(6f, kx, Eps, "the knob still follows the finger");
            TouchMath.Stick(300f, 0f, out x, out y, out kx, out ky);
            Assert.AreEqual(1f, x, Eps);
            Assert.AreEqual(TouchMath.StickRadius, kx, Eps);
            TouchMath.Stick(0f, -TouchMath.StickRadius, out x, out y, out kx, out ky);
            Assert.AreEqual(1f, y, Eps, "dragging up (panel y down is positive) is forward");
        }

        [Test]
        public void PortraitDriveZoneSteersWithASidewaysSlide()
        {
            Assert.AreEqual(0f, TouchMath.DriveSteer(5f));
            Assert.AreEqual(1f, TouchMath.DriveSteer(TouchMath.DriveSteerRange), Eps);
            Assert.AreEqual(1f, TouchMath.DriveSteer(1000f), Eps);
            float half = TouchMath.DriveDeadZone + 0.5f * (TouchMath.DriveSteerRange - TouchMath.DriveDeadZone);
            Assert.AreEqual(-0.5f, TouchMath.DriveSteer(-half), Eps);
            Assert.AreEqual(0f, TouchMath.DriveSteer(float.NaN));
        }

        [Test]
        public void PinchingApartZoomsIn()
        {
            Assert.AreEqual(1f, TouchMath.PinchNotches(100f, 100f + TouchMath.PinchPerNotch), Eps);
            Assert.Less(TouchMath.PinchNotches(300f, 200f), 0f);
            Assert.AreEqual(0f, TouchMath.PinchNotches(0f, 100f), "no previous span: no jump");
        }

        [Test]
        public void TouchControlsHideForKeysAndPadsAndComeBackOnATouch()
        {
            var desktop = new TouchControlsVisibility(false);
            Assert.IsFalse(desktop.Visible, "a Mac without a touchscreen starts without touch controls");
            Assert.IsFalse(desktop.Update(TouchControlsVisibility.Source.None));
            Assert.IsTrue(desktop.Update(TouchControlsVisibility.Source.Touch));
            Assert.IsTrue(desktop.Visible);

            var phone = new TouchControlsVisibility(true);
            Assert.IsTrue(phone.Visible);
            Assert.IsTrue(phone.Update(TouchControlsVisibility.Source.KeysOrPad), "a paired gamepad hides them");
            Assert.IsFalse(phone.Visible);
            Assert.IsFalse(phone.Update(TouchControlsVisibility.Source.KeysOrPad));
            Assert.IsTrue(phone.Update(TouchControlsVisibility.Source.Touch));
        }

        [Test]
        public void DrivingEventsPlayTheAgreedHaptics()
        {
            HapticKind kind;
            Assert.IsTrue(ExploreFeedback.HapticFor(StepEvents.SurfaceChanged, true, 0f, out kind));
            Assert.AreEqual(HapticKind.Selection, kind);
            Assert.IsFalse(ExploreFeedback.HapticFor(StepEvents.SurfaceChanged, false, 0f, out kind), "not on foot");
            Assert.IsTrue(ExploreFeedback.HapticFor(StepEvents.Bump | StepEvents.SurfaceChanged, true, 0f, out kind));
            Assert.AreEqual(HapticKind.LightImpact, kind, "the stronger one wins");
            Assert.IsTrue(ExploreFeedback.HapticFor(StepEvents.StuckRecovered | StepEvents.Bump, true, 0f, out kind));
            Assert.AreEqual(HapticKind.MediumImpact, kind);
            Assert.IsFalse(ExploreFeedback.HapticFor(StepEvents.Landed, true, 1f, out kind), "a soft landing is not felt");
            Assert.IsTrue(ExploreFeedback.HapticFor(StepEvents.Landed, true, 4f, out kind));
            Assert.AreEqual(HapticKind.LightImpact, kind);
            Assert.IsFalse(ExploreFeedback.HapticFor(StepEvents.None, true, 0f, out kind));
            Assert.IsFalse(ExploreFeedback.HapticFor(StepEvents.OffRoad, true, 0f, out kind));
        }

        [Test]
        public void PortraitRigRisesAndPullsBack()
        {
            foreach (float ride in new[] { 0f, 1f })
            {
                ChaseRigProfile landscape = ChaseRigProfile.Blend(ride, 0f);
                ChaseRigProfile portrait = ChaseRigProfile.Blend(ride, 1f);
                Assert.Greater(portrait.DistanceM, landscape.DistanceM);
                Assert.Greater(portrait.CameraHeightM, landscape.CameraHeightM);
                Assert.Greater(portrait.PitchDeg, landscape.PitchDeg);
                Assert.Greater(portrait.LookAhead(20f), landscape.LookAhead(20f), "more road ahead on a tall screen");
            }
            Assert.AreEqual(62f, ChaseRigProfile.Blend(1f, 0.3f).MinHorizontalFovDeg, Eps, "driving rig");
            Assert.AreEqual(55f, ChaseRigProfile.Blend(0f, 0.7f).MinHorizontalFovDeg, Eps, "walking rig");
            Assert.AreEqual(ChaseRigProfile.RideLandscape.LookAheadMaxM, ChaseRigProfile.RideLandscape.LookAhead(1000f), Eps);
            Assert.AreEqual(0f, ChaseRigProfile.RideLandscape.LookAhead(-5f), "no look-ahead reversing");
        }

        [Test]
        public void TheExplorerStaysInTheLowerFrameAtAnySpeedAndAspect()
        {
            // Regression: the look-ahead used to slide the whole camera forward, so at riding speed the scooter left the
            // bottom of the screen and from about 14 m/s the camera was ahead of it. Projects the explorer's feet and a
            // 1.7 m head into the view the rig builds (default zoom and pitch, flat ground).
            var aspects = new[] { 9f / 19.5f, 9f / 16f, 3f / 4f, 4f / 3f, 16f / 9f, 20f / 9f };
            foreach (float ride in new[] { 0f, 1f })
            {
                foreach (float aspect in aspects)
                {
                    ChaseRigProfile rig = ChaseRigProfile.Blend(ride, aspect < 1f ? 1f : 0f);
                    float minHFov = ride > 0f ? CameraFov.DrivingMinHorizontalFov : CameraFov.WalkingMinHorizontalFov;
                    double p = rig.PitchDeg * Math.PI / 180.0;
                    float behind = (float)(rig.DistanceM * Math.Cos(p));
                    float above = rig.AimHeightM + (float)(rig.DistanceM * Math.Sin(p));
                    Assert.Greater(behind, 1f, "the camera stays behind the explorer");
                    for (float speed = 0f; speed <= 30f; speed += 1f)
                    {
                        float kick = ride > 0f ? 5f * Math.Min(1f, speed / 25f) : 0f;
                        float vfov = CameraFov.VerticalFromHorizontal(minHFov + kick, aspect);
                        float pitch = ChaseRigProfile.ViewPitchDeg(above, behind, rig.FootScreenY(speed), vfov);
                        float feet = ChaseRigProfile.ScreenY(above, behind, pitch, vfov);
                        float head = ChaseRigProfile.ScreenY(above - 1.7f, behind, pitch, vfov);
                        string at = "ride " + ride + ", aspect " + aspect + ", " + speed + " m/s";
                        Assert.AreEqual(rig.FootScreenY(speed), feet, 1e-3f, at);
                        Assert.GreaterOrEqual(feet, -0.9f, "feet on screen: " + at);
                        Assert.LessOrEqual(feet, -0.4f, "feet in the lower part: " + at);
                        Assert.Greater(head, feet, at);
                        Assert.LessOrEqual(head, 0.3f, "head below the upper third: " + at);
                    }
                }
            }
        }
    }
}
