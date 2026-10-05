using System;
using System.Collections.Generic;
using Ghumante.Core.Services;
using Ghumante.Platform;
using Ghumante.Platform.Haptics;
using NUnit.Framework;
using UnityEngine;

namespace Ghumante.Tests.EditMode
{
    public class HapticsEditModeTests
    {
        private readonly List<KeyValuePair<HapticKind, bool>> _requests = new List<KeyValuePair<HapticKind, bool>>();

        [SetUp]
        public void Subscribe()
        {
            _requests.Clear();
            MobileHaptics.Requested += OnRequested;
        }

        [TearDown]
        public void Unsubscribe()
        {
            MobileHaptics.Requested -= OnRequested;
        }

        private void OnRequested(HapticKind kind, bool deviceWillPlay)
        {
            _requests.Add(new KeyValuePair<HapticKind, bool>(kind, deviceWillPlay));
        }

        [Test]
        public void EditorGetsSilentHapticsThatStillReportRequests()
        {
            IHaptics haptics = MobileHaptics.Create(true);
            try
            {
                Assert.IsInstanceOf<SilentHaptics>(haptics);
                Assert.IsFalse(haptics.IsSupported);
                Assert.IsTrue(haptics.Enabled);

                Assert.DoesNotThrow(() => haptics.Play(HapticKind.Success));
                Assert.AreEqual(1, _requests.Count);
                Assert.AreEqual(HapticKind.Success, _requests[0].Key);
                Assert.IsFalse(_requests[0].Value, "nothing plays in the editor");
                Assert.AreEqual(HapticKind.Success, ((SilentHaptics)haptics).Recorder.LastPlayed);
            }
            finally
            {
                ((IDisposable)haptics).Dispose();
            }
        }

        [Test]
        public void PlayNeverThrows()
        {
            IHaptics haptics = MobileHaptics.Create(true);
            foreach (HapticKind kind in Enum.GetValues(typeof(HapticKind)))
            {
                Assert.DoesNotThrow(() => haptics.Play(kind), kind.ToString());
            }
            Assert.DoesNotThrow(() => haptics.Play((HapticKind)200));
            ((IDisposable)haptics).Dispose();
            Assert.DoesNotThrow(() => haptics.Play(HapticKind.Error), "after Dispose");
            Assert.DoesNotThrow(() => MobileHaptics.Prepare(haptics));
            Assert.DoesNotThrow(() => MobileHaptics.Prepare(null));
        }

        [Test]
        public void DisabledHapticsReportNothing()
        {
            IHaptics haptics = MobileHaptics.Create(false);
            Assert.IsFalse(haptics.Enabled);
            haptics.Play(HapticKind.HeavyImpact);
            Assert.AreEqual(0, _requests.Count);

            haptics.Enabled = true;
            haptics.Play(HapticKind.HeavyImpact);
            Assert.AreEqual(1, _requests.Count);
        }

        [Test]
        public void RequestsAreRateLimited()
        {
            IHaptics haptics = MobileHaptics.Create(true);
            double start = Time.realtimeSinceStartupAsDouble;
            haptics.Play(HapticKind.Selection);
            haptics.Play(HapticKind.Selection);
            haptics.Play(HapticKind.LightImpact);
            double elapsed = Time.realtimeSinceStartupAsDouble - start;
            if (elapsed < 0.035)
            {
                // Back-to-back requests: only the first passes the gate (any-kind interval 35 ms).
                Assert.AreEqual(1, _requests.Count);
            }
            else
            {
                Assert.Inconclusive("the test thread stalled for " + elapsed + " s; gate timing not observable");
            }
        }

        [Test]
        public void OutcomeRightAfterThePressTickIsDeferredNotDropped()
        {
            // Regression: PressFeel plays LightImpact on PointerDown and the click plays Success; a quick tap
            // delivers both in one frame (or 33 ms apart at 30 fps) and the rate limiter dropped the Success.
            var haptics = (SilentHaptics)MobileHaptics.Create(true);
            try
            {
                haptics.PlayAt(HapticKind.LightImpact, 10.0);
                haptics.PlayAt(HapticKind.Success, 10.0);
                Assert.AreEqual(1, _requests.Count, "the reward cannot play on top of the press tick");
                Assert.IsTrue(haptics.HasDeferred, "but it waits");

                haptics.PlayDeferredIfDue(10.02);
                Assert.AreEqual(1, _requests.Count, "still inside the 35 ms interval");

                haptics.PlayDeferredIfDue(10.04);
                Assert.AreEqual(2, _requests.Count);
                Assert.AreEqual(HapticKind.LightImpact, _requests[0].Key);
                Assert.AreEqual(HapticKind.Success, _requests[1].Key);
                Assert.AreEqual(HapticKind.Success, haptics.Recorder.LastPlayed);
                Assert.AreEqual(2, haptics.Recorder.PlayCount);
                Assert.IsFalse(haptics.HasDeferred);
            }
            finally
            {
                haptics.Dispose();
            }
        }

        [Test]
        public void AWaitingOutcomePlaysBeforeTheNextRequest()
        {
            var haptics = (SilentHaptics)MobileHaptics.Create(true);
            try
            {
                haptics.PlayAt(HapticKind.MediumImpact, 20.0);
                haptics.PlayAt(HapticKind.Warning, 20.0 + 1.0 / 30.0);
                Assert.AreEqual(1, _requests.Count);

                // The next tap's press arrives before the per-frame poll: the warning goes first.
                haptics.PlayAt(HapticKind.LightImpact, 20.1);
                Assert.AreEqual(2, _requests.Count);
                Assert.AreEqual(HapticKind.Warning, _requests[1].Key);
                Assert.IsFalse(haptics.HasDeferred);
            }
            finally
            {
                haptics.Dispose();
            }
        }

        [Test]
        public void SwitchingHapticsOffDropsAWaitingOutcome()
        {
            var haptics = (SilentHaptics)MobileHaptics.Create(true);
            try
            {
                haptics.PlayAt(HapticKind.MediumImpact, 30.0);
                haptics.PlayAt(HapticKind.Warning, 30.0);
                Assert.IsTrue(haptics.HasDeferred);

                haptics.Enabled = false;
                haptics.PlayDeferredIfDue(30.1);
                Assert.IsFalse(haptics.HasDeferred);
                haptics.Enabled = true;
                haptics.PlayDeferredIfDue(30.2);
                Assert.AreEqual(1, _requests.Count, "the warning was dropped, not delayed");
            }
            finally
            {
                haptics.Dispose();
            }
        }

        [Test]
        public void ReducedMotionIsOffInTheEditor()
        {
            Assert.IsFalse(DeviceAccessibility.PrefersReducedMotion());
            DeviceAccessibility.Invalidate();
            Assert.IsFalse(DeviceAccessibility.PrefersReducedMotion());
        }
    }
}
