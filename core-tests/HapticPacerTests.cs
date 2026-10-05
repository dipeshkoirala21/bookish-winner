using System;
using Ghumante.Core.Services;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class HapticPacerTests
    {
        // Intervals and times are multiples of 1/32 s, exact in binary, so the boundaries are exact too.
        private const double MinInterval = 0.125;
        private const double SameKindInterval = 0.25;
        private const double MaxDelay = 0.5;

        private static HapticPacer NewPacer()
        {
            return new HapticPacer(MinInterval, SameKindInterval, MaxDelay);
        }

        [Test]
        public void DefaultsMatchTheGate()
        {
            Assert.That(HapticPacer.DefaultMinIntervalSeconds, Is.EqualTo(0.035));
            Assert.That(HapticPacer.DefaultSameKindIntervalSeconds, Is.EqualTo(0.07));
            Assert.That(HapticPacer.DefaultMaxDelaySeconds, Is.EqualTo(0.25));

            // The same behaviour as HapticGate's defaults for requests that pass or are dropped.
            var pacer = new HapticPacer();
            Assert.That(pacer.TryPass(HapticKind.Selection, 10.0), Is.True);
            Assert.That(pacer.TryPass(HapticKind.LightImpact, 10.034), Is.False, "any kind: 34 ms is too soon");
            Assert.That(pacer.TryPass(HapticKind.Selection, 10.069), Is.False, "same kind: 69 ms is too soon");
            Assert.That(pacer.TryPass(HapticKind.Selection, 10.071), Is.True, "same kind: 71 ms is enough");
            Assert.That(pacer.HasDeferred, Is.False, "press ticks are never deferred");
        }

        [Test]
        public void OutcomeInTheSameFrameAsThePressTickIsDeferredNotDropped()
        {
            // Regression: a quick tap delivers PointerDown and the click in the same frame; the press tick
            // passed and the gate dropped the reward.
            var pacer = new HapticPacer();
            Assert.That(pacer.TryPass(HapticKind.LightImpact, 5.0), Is.True, "press-down tick");
            Assert.That(pacer.TryPass(HapticKind.Success, 5.0), Is.False, "the reward cannot play on top of it");
            Assert.That(pacer.HasDeferred, Is.True, "but it waits");

            HapticKind due;
            Assert.That(pacer.TryTakeDue(5.034, out due), Is.False, "still inside the any-kind interval");
            Assert.That(pacer.HasDeferred, Is.True);
            Assert.That(pacer.TryTakeDue(5.036, out due), Is.True, "the next poll after 35 ms plays it");
            Assert.That(due, Is.EqualTo(HapticKind.Success));
            Assert.That(pacer.HasDeferred, Is.False);
            Assert.That(pacer.TryTakeDue(5.5, out due), Is.False, "taken once");
        }

        [Test]
        public void OutcomeOneThirtyFpsFrameAfterThePressTickIsDeferred()
        {
            // Low/Mid tier: 30 fps, so PointerDown and the click are 33.3 ms apart, under the 35 ms interval.
            var pacer = new HapticPacer();
            Assert.That(pacer.TryPass(HapticKind.MediumImpact, 1.0), Is.True, "Explore's press");
            Assert.That(pacer.TryPass(HapticKind.Warning, 1.0 + 1.0 / 30.0), Is.False);
            HapticKind due;
            Assert.That(pacer.TryTakeDue(1.0 + 2.0 / 30.0, out due), Is.True, "the next frame");
            Assert.That(due, Is.EqualTo(HapticKind.Warning));
        }

        [Test]
        public void EveryOutcomeKindIsDeferredAndNoTickIs()
        {
            foreach (HapticKind kind in Enum.GetValues(typeof(HapticKind)))
            {
                var pacer = NewPacer();
                HapticKind other = kind == HapticKind.Selection ? HapticKind.LightImpact : HapticKind.Selection;
                Assert.That(pacer.TryPass(other, 0.0), Is.True);
                Assert.That(pacer.TryPass(kind, 0.0), Is.False, kind.ToString());
                Assert.That(pacer.HasDeferred, Is.EqualTo(HapticPacer.IsOutcome(kind)), kind.ToString());
            }
            Assert.That(HapticPacer.IsOutcome(HapticKind.HeavyImpact), Is.True);
            Assert.That(HapticPacer.IsOutcome(HapticKind.Success), Is.True);
            Assert.That(HapticPacer.IsOutcome(HapticKind.Warning), Is.True);
            Assert.That(HapticPacer.IsOutcome(HapticKind.Error), Is.True);
            Assert.That(HapticPacer.IsOutcome(HapticKind.Selection), Is.False);
            Assert.That(HapticPacer.IsOutcome(HapticKind.LightImpact), Is.False);
            Assert.That(HapticPacer.IsOutcome(HapticKind.MediumImpact), Is.False);
            Assert.That(HapticPacer.IsOutcome((HapticKind)200), Is.False);
        }

        [Test]
        public void RepeatedOutcomesWithinTheSameKindIntervalAreStillDropped()
        {
            var pacer = NewPacer();
            Assert.That(pacer.TryPass(HapticKind.Success, 0.0), Is.True);
            Assert.That(pacer.TryPass(HapticKind.Success, 0.0625), Is.False);
            Assert.That(pacer.HasDeferred, Is.False, "a burst of rewards must not queue up into a buzz");
            Assert.That(pacer.TryPass(HapticKind.Success, 0.25), Is.True, "exactly the same-kind interval");
        }

        [Test]
        public void ANewerOutcomeReplacesTheDeferredOne()
        {
            var pacer = NewPacer();
            Assert.That(pacer.TryPass(HapticKind.LightImpact, 0.0), Is.True);
            Assert.That(pacer.TryPass(HapticKind.Success, 0.03125), Is.False);
            Assert.That(pacer.TryPass(HapticKind.Warning, 0.0625), Is.False);
            HapticKind due;
            Assert.That(pacer.TryTakeDue(0.125, out due), Is.True);
            Assert.That(due, Is.EqualTo(HapticKind.Warning), "one slot: the latest outcome wins");
            Assert.That(pacer.HasDeferred, Is.False);
        }

        [Test]
        public void ADeferredOutcomeTooOldIsDropped()
        {
            var pacer = NewPacer();
            Assert.That(pacer.TryPass(HapticKind.LightImpact, 0.0), Is.True);
            Assert.That(pacer.TryPass(HapticKind.Error, 0.0), Is.False);
            HapticKind due;
            Assert.That(pacer.TryTakeDue(0.53125, out due), Is.False, "more than the maximum delay later");
            Assert.That(pacer.HasDeferred, Is.False, "dropped as stale");
            Assert.That(pacer.TryPass(HapticKind.Error, 0.53125), Is.True, "and it recorded nothing");
        }

        [Test]
        public void TheDeferredOutcomeStillCountsForTheGate()
        {
            var pacer = NewPacer();
            Assert.That(pacer.TryPass(HapticKind.LightImpact, 0.0), Is.True);
            Assert.That(pacer.TryPass(HapticKind.Success, 0.0), Is.False);
            HapticKind due;
            Assert.That(pacer.TryTakeDue(0.125, out due), Is.True);
            Assert.That(pacer.TryPass(HapticKind.Selection, 0.1875), Is.False, "any-kind interval after it");
            Assert.That(pacer.TryPass(HapticKind.Success, 0.3125), Is.False, "same-kind interval after it");
            Assert.That(pacer.HasDeferred, Is.False, "the same-kind interval drops, it does not defer");
            Assert.That(pacer.TryPass(HapticKind.Success, 0.375), Is.True);
        }

        [Test]
        public void ADeferredOutcomeWaitsWhileOtherHapticsKeepTheGateBusy()
        {
            var pacer = NewPacer();
            Assert.That(pacer.TryPass(HapticKind.LightImpact, 0.0), Is.True);
            Assert.That(pacer.TryPass(HapticKind.Success, 0.0), Is.False);
            HapticKind due;
            // The caller polls first, but a tick that slipped in (e.g. from another screen) delays it.
            Assert.That(pacer.TryTakeDue(0.0625, out due), Is.False);
            Assert.That(pacer.TryPass(HapticKind.Selection, 0.125), Is.True);
            Assert.That(pacer.TryTakeDue(0.1875, out due), Is.False, "the tick reset the any-kind interval");
            Assert.That(pacer.TryTakeDue(0.25, out due), Is.True);
            Assert.That(due, Is.EqualTo(HapticKind.Success));
        }

        [Test]
        public void CancelAndResetDropTheDeferredOutcome()
        {
            var pacer = NewPacer();
            HapticKind due;
            Assert.That(pacer.TryPass(HapticKind.LightImpact, 0.0), Is.True);
            Assert.That(pacer.TryPass(HapticKind.Success, 0.0), Is.False);
            pacer.CancelDeferred();
            Assert.That(pacer.HasDeferred, Is.False);
            Assert.That(pacer.TryTakeDue(0.125, out due), Is.False);

            Assert.That(pacer.TryPass(HapticKind.Warning, 0.125), Is.True);
            Assert.That(pacer.TryPass(HapticKind.Error, 0.125), Is.False);
            Assert.That(pacer.HasDeferred, Is.True);
            pacer.Reset();
            Assert.That(pacer.HasDeferred, Is.False);
            Assert.That(pacer.TryPass(HapticKind.Warning, 0.125), Is.True, "history forgotten too");
        }

        [Test]
        public void UndefinedKindsAreRejectedAndNeverDeferred()
        {
            var pacer = NewPacer();
            Assert.That(pacer.TryPass((HapticKind)255, 0.0), Is.False);
            Assert.That(pacer.HasDeferred, Is.False);
            Assert.That(pacer.TryPass(HapticKind.Selection, 0.0), Is.True, "a rejected kind records nothing");
        }

        [Test]
        public void NegativeIntervalsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticPacer(-0.001, 0.07, 0.25));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticPacer(0.035, -1, 0.25));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticPacer(0.035, 0.07, -0.5));
        }
    }
}
