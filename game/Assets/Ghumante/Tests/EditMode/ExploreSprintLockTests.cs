using System;
using Ghumante.Characters;
using Ghumante.Core.Driving;
using Ghumante.UI.Hud;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The sprint lock (owner, first playtest #10: "Add locking mechanism while user is sprinting, so that the player do
    /// not have to keep pushing it forward"): double-tap sprint, or drag the touch stick up into the lock, and the explorer
    /// keeps sprinting with no key held until pulled back, tapped off, an Action press or a wall. Driven through Core's
    /// walker exactly as ExplorerController drives it (ControlMapper.WalkInput on the rewritten frame). Engine-free.
    /// </summary>
    public class ExploreSprintLockTests
    {
        private const float Dt = 1f / 30f;

        /// <summary>A walker on flat ground and the lock, stepped like ExplorerController.StepWalk.</summary>
        private sealed class Runner
        {
            public readonly ArcadeVehicle Walker = new ArcadeVehicle(VehicleSpec.Walker());
            public readonly SprintLock Lock = new SprintLock();
            public readonly FlatGround Ground = new FlatGround();
            public int Changes;

            public Runner()
            {
                Walker.Teleport(0, 0, 0f, Ground);
            }

            public void Step(ControlFrame c, float cameraYawRad = 0f)
            {
                Lock.Step(Dt, ref c, Walker.SpeedMps);
                if (Lock.Changed) Changes++;
                DriveInput input = ControlMapper.WalkInput(Walker, c, cameraYawRad, Dt);
                Walker.Step(input, Dt, Ground, 0f);
            }

            public void Hold(ControlFrame c, float seconds, float cameraYawRad = 0f)
            {
                for (int i = 0; i < (int)Math.Round(seconds / Dt); i++) Step(c, cameraYawRad);
            }

            /// <summary>Two sprint taps 0.2 s apart, nothing else held.</summary>
            public void DoubleTap()
            {
                Hold(new ControlFrame { Boost = true }, 0.1f);
                Hold(new ControlFrame(), 0.1f);
                Hold(new ControlFrame { Boost = true }, 0.1f);
                Hold(new ControlFrame(), Dt);
            }
        }

        [Test]
        public void DoubleTapSprintKeepsTheExplorerSprintingHandsFree()
        {
            var r = new Runner();
            // Plain sprinting for reference: stick forward with Shift held.
            var held = new Runner();
            held.Hold(new ControlFrame { MoveY = 1f, Boost = true }, 6f);
            float sprint = held.Walker.SpeedMps;
            Assert.That(sprint, Is.GreaterThan(3f), "holding sprint runs");

            r.DoubleTap();
            Assert.IsTrue(r.Lock.Locked, "a double tap locks the sprint");
            Assert.AreEqual(1, r.Changes, "one change: locked");
            // Nothing held for ten seconds: still sprinting forward, as fast as holding it.
            r.Hold(new ControlFrame(), 10f);
            Assert.IsTrue(r.Lock.Locked);
            Assert.AreEqual(sprint, r.Walker.SpeedMps, 0.05f * sprint, "the full sprint, hands free");
            Assert.That(r.Walker.Z, Is.GreaterThan(30.0), "and it went forward (north, away from the camera)");
        }

        [Test]
        public void ALockedSprintSteersWithTheCameraAndTheStick()
        {
            var r = new Runner();
            r.DoubleTap();
            // The camera turned to the east: the run follows it.
            r.Hold(new ControlFrame(), 4f, (float)(Math.PI / 2));
            Assert.That(Math.Abs(r.Walker.HeadingRad - Math.PI / 2), Is.LessThan(0.15), "runs where the camera looks");
            // A sideways push steers without dropping the lock.
            r.Hold(new ControlFrame { MoveX = -1f }, 1f, (float)(Math.PI / 2));
            Assert.IsTrue(r.Lock.Locked, "steering keeps the lock");
        }

        [TestCase("tap")]
        [TestCase("back")]
        [TestCase("action")]
        public void TheLockStopsWhenThePlayerStopsIt(string how)
        {
            var r = new Runner();
            r.DoubleTap();
            r.Hold(new ControlFrame(), 3f);
            Assert.IsTrue(r.Lock.Locked);
            switch (how)
            {
                case "tap":
                    r.Hold(new ControlFrame { Boost = true }, 0.1f);
                    break;
                case "back":
                    r.Hold(new ControlFrame { MoveY = -1f }, 0.1f);
                    break;
                default:
                    r.Hold(new ControlFrame { ToggleMode = true }, Dt);
                    break;
            }
            Assert.IsFalse(r.Lock.Locked, how + " unlocks");
            r.Hold(new ControlFrame(), 3f);
            Assert.That(r.Walker.SpeedMps, Is.LessThan(0.3f), "and the explorer comes to a stop");
        }

        [Test]
        public void HoldingSprintAloneNeverLocks()
        {
            var r = new Runner();
            r.Hold(new ControlFrame { MoveY = 1f, Boost = true }, 5f);
            r.Hold(new ControlFrame(), 2f);
            Assert.IsFalse(r.Lock.Locked, "a long hold is just sprinting");
            Assert.That(r.Walker.SpeedMps, Is.LessThan(0.3f));
            // Two presses far apart are not a double tap either.
            r.Hold(new ControlFrame { Boost = true }, 0.1f);
            r.Hold(new ControlFrame(), 0.6f);
            r.Hold(new ControlFrame { Boost = true }, 0.1f);
            Assert.IsFalse(r.Lock.Locked);
        }

        [Test]
        public void AWallReleasesTheLock()
        {
            var r = new Runner();
            r.DoubleTap();
            r.Hold(new ControlFrame(), 2f);
            Assert.IsTrue(r.Lock.Locked);
            // Held at a standstill (a wall) for longer than SprintLock.StuckS: released.
            var c = new ControlFrame();
            for (int i = 0; i < (int)((SprintLock.StuckS + 0.2f) / Dt); i++) r.Lock.Step(Dt, ref c, 0f);
            Assert.IsFalse(r.Lock.Locked);
        }

        [Test]
        public void TouchDragIntoTheLockLocksAndTheRimMathsHoldsItsCone()
        {
            Assert.IsFalse(TouchMath.InSprintLock(0f, -TouchMath.StickRadius), "a full stick is not the lock");
            Assert.IsTrue(TouchMath.InSprintLock(0f, -TouchMath.SprintLockReach - 1f), "past the rim, straight up");
            Assert.IsTrue(TouchMath.InSprintLock(60f, -TouchMath.SprintLockReach - 40f), "within the cone");
            Assert.IsFalse(TouchMath.InSprintLock(TouchMath.SprintLockReach, -20f), "sideways is not the lock");
            Assert.IsFalse(TouchMath.InSprintLock(0f, TouchMath.SprintLockReach + 10f), "down is not the lock");

            var r = new Runner();
            r.Hold(new ControlFrame { MoveY = 1f, SprintLockDrag = true }, SprintLock.DragHoldS + 0.1f);
            Assert.IsTrue(r.Lock.Locked, "held in the lock");
            r.Hold(new ControlFrame(), 5f);
            Assert.IsTrue(r.Lock.Locked, "the thumb is off the screen: still sprinting");
            Assert.That(r.Walker.SpeedMps, Is.GreaterThan(3f));
        }

        [Test]
        public void ReleaseDropsTheLock()
        {
            var r = new Runner();
            r.DoubleTap();
            Assert.IsTrue(r.Lock.Locked);
            r.Lock.Release();
            Assert.IsFalse(r.Lock.Locked, "getting on a vehicle, an emote or a calm zone release it");
        }
    }
}
