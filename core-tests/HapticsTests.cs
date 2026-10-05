using System;
using System.Collections.Generic;
using Ghumante.Core.Save;
using Ghumante.Core.Services;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class HapticGateTests
    {
        // Intervals and times are multiples of 1/8 s, exact in binary, so the boundaries are exact too.
        private const double MinInterval = 0.125;
        private const double SameKindInterval = 0.5;

        [Test]
        public void DefaultsAreThirtyFiveAndSeventyMilliseconds()
        {
            var gate = new HapticGate();
            Assert.That(gate.TryPass(HapticKind.Selection, 10.0), Is.True);
            Assert.That(gate.TryPass(HapticKind.LightImpact, 10.034), Is.False, "any kind: 34 ms is too soon");
            Assert.That(gate.TryPass(HapticKind.Selection, 10.069), Is.False, "same kind: 69 ms is too soon");
            Assert.That(gate.TryPass(HapticKind.Selection, 10.071), Is.True, "same kind: 71 ms is enough");
            Assert.That(gate.TryPass(HapticKind.LightImpact, 10.105), Is.False, "any kind: 34 ms after the last");
            Assert.That(gate.TryPass(HapticKind.LightImpact, 10.107), Is.True, "any kind: 36 ms is enough");
        }

        [Test]
        public void FirstRequestOfEveryKindPasses()
        {
            foreach (HapticKind kind in Enum.GetValues(typeof(HapticKind)))
            {
                var gate = new HapticGate(MinInterval, SameKindInterval);
                Assert.That(gate.TryPass(kind, 0.0), Is.True, kind.ToString());
            }
        }

        [Test]
        public void AnyTwoHapticsKeepTheMinimumInterval()
        {
            var gate = new HapticGate(MinInterval, SameKindInterval);
            Assert.That(gate.TryPass(HapticKind.Selection, 1.0), Is.True);
            Assert.That(gate.TryPass(HapticKind.Success, 1.0), Is.False, "same instant");
            Assert.That(gate.TryPass(HapticKind.Success, 1.0625), Is.False, "half the minimum interval");
            Assert.That(gate.TryPass(HapticKind.Success, 1.125), Is.True, "exactly the minimum interval passes");
            Assert.That(gate.TryPass(HapticKind.Error, 1.25), Is.True);
        }

        [Test]
        public void RepeatsOfOneKindKeepTheLongerInterval()
        {
            var gate = new HapticGate(MinInterval, SameKindInterval);
            Assert.That(gate.TryPass(HapticKind.LightImpact, 2.0), Is.True);
            Assert.That(gate.TryPass(HapticKind.LightImpact, 2.25), Is.False, "past the any-kind interval only");
            Assert.That(gate.TryPass(HapticKind.LightImpact, 2.375), Is.False);
            Assert.That(gate.TryPass(HapticKind.LightImpact, 2.5), Is.True, "exactly the same-kind interval passes");
        }

        [Test]
        public void RejectedRequestsDoNotExtendTheWindow()
        {
            var gate = new HapticGate(MinInterval, SameKindInterval);
            Assert.That(gate.TryPass(HapticKind.Selection, 0.0), Is.True);
            // A burst of rejected scroll ticks must not keep pushing the next allowed tick further out.
            for (double t = 0.0625; t < 0.5; t += 0.0625)
            {
                Assert.That(gate.TryPass(HapticKind.Selection, t), Is.False, "t=" + t);
            }
            Assert.That(gate.TryPass(HapticKind.Selection, 0.5), Is.True);

            // Same for the any-kind interval: a rejected Warning does not count as the last haptic.
            Assert.That(gate.TryPass(HapticKind.Warning, 0.5625), Is.False);
            Assert.That(gate.TryPass(HapticKind.Error, 0.625), Is.True);
        }

        [Test]
        public void KindsHaveIndependentSameKindWindows()
        {
            var gate = new HapticGate(MinInterval, SameKindInterval);
            Assert.That(gate.TryPass(HapticKind.Selection, 0.0), Is.True);
            Assert.That(gate.TryPass(HapticKind.LightImpact, 0.125), Is.True);
            Assert.That(gate.TryPass(HapticKind.Selection, 0.25), Is.False, "Selection still in its window");
            Assert.That(gate.TryPass(HapticKind.MediumImpact, 0.25), Is.True);
            Assert.That(gate.TryPass(HapticKind.Selection, 0.5), Is.True);
            Assert.That(gate.TryPass(HapticKind.LightImpact, 0.625), Is.True);
        }

        [Test]
        public void ResetForgetsHistory()
        {
            var gate = new HapticGate(MinInterval, SameKindInterval);
            Assert.That(gate.TryPass(HapticKind.HeavyImpact, 5.0), Is.True);
            Assert.That(gate.TryPass(HapticKind.HeavyImpact, 5.0), Is.False);
            gate.Reset();
            Assert.That(gate.TryPass(HapticKind.HeavyImpact, 5.0), Is.True, "same instant after Reset");
            Assert.That(gate.TryPass(HapticKind.Success, 5.0), Is.False, "Reset then a pass records again");
        }

        [Test]
        public void ResetAllowsAClockThatRestarted()
        {
            // A clock that went backwards (e.g. a new session reusing the gate) would otherwise block forever.
            var gate = new HapticGate(MinInterval, SameKindInterval);
            Assert.That(gate.TryPass(HapticKind.Success, 1000.0), Is.True);
            gate.Reset();
            Assert.That(gate.TryPass(HapticKind.Success, 0.0), Is.True);
        }

        [Test]
        public void ZeroIntervalsLetEverythingThrough()
        {
            var gate = new HapticGate(0, 0);
            for (int i = 0; i < 10; i++)
            {
                Assert.That(gate.TryPass(HapticKind.Selection, 3.0), Is.True);
            }
        }

        [Test]
        public void UndefinedKindsBeyondTheTableAreRejected()
        {
            var gate = new HapticGate(MinInterval, SameKindInterval);
            Assert.That(gate.TryPass((HapticKind)255, 0.0), Is.False);
            Assert.That(gate.TryPass(HapticKind.Selection, 0.0), Is.True, "a rejected kind records nothing");
        }

        [Test]
        public void NegativeIntervalsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticGate(-0.001, 0.07));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticGate(0.035, -1));
        }

        [Test]
        public void KindValuesAreStable()
        {
            // Native bridges map these values: append only, never renumber (Haptics.cs).
            Assert.That((int)HapticKind.Selection, Is.EqualTo(0));
            Assert.That((int)HapticKind.LightImpact, Is.EqualTo(1));
            Assert.That((int)HapticKind.MediumImpact, Is.EqualTo(2));
            Assert.That((int)HapticKind.HeavyImpact, Is.EqualTo(3));
            Assert.That((int)HapticKind.Success, Is.EqualTo(4));
            Assert.That((int)HapticKind.Warning, Is.EqualTo(5));
            Assert.That((int)HapticKind.Error, Is.EqualTo(6));
        }
    }

    public class NullHapticsTests
    {
        [Test]
        public void IsUnsupportedAndEnabledByDefault()
        {
            IHaptics haptics = new NullHaptics();
            Assert.That(haptics.IsSupported, Is.False);
            Assert.That(haptics.Enabled, Is.True);
        }

        [Test]
        public void RecordsEveryEnabledPlay()
        {
            var haptics = new NullHaptics();
            var played = new List<HapticKind>();
            haptics.Played += played.Add;
            Assert.That(haptics.LastPlayed, Is.Null);
            Assert.That(haptics.PlayCount, Is.EqualTo(0));

            haptics.Play(HapticKind.Success);
            haptics.Play(HapticKind.Success); // no rate limit in NullHaptics: it records requests as given
            haptics.Play(HapticKind.Selection);
            Assert.That(haptics.LastPlayed, Is.EqualTo(HapticKind.Selection));
            Assert.That(haptics.PlayCount, Is.EqualTo(3));
            Assert.That(played, Is.EqualTo(new[] { HapticKind.Success, HapticKind.Success, HapticKind.Selection }));
        }

        [Test]
        public void DisabledPlaysAreIgnored()
        {
            var haptics = new NullHaptics();
            int events = 0;
            haptics.Played += k => events++;
            haptics.Play(HapticKind.Warning);
            haptics.Enabled = false;
            haptics.Play(HapticKind.Error);
            Assert.That(haptics.LastPlayed, Is.EqualTo(HapticKind.Warning));
            Assert.That(haptics.PlayCount, Is.EqualTo(1));
            Assert.That(events, Is.EqualTo(1));
            haptics.Enabled = true;
            haptics.Play(HapticKind.Error);
            Assert.That(haptics.LastPlayed, Is.EqualTo(HapticKind.Error));
            Assert.That(events, Is.EqualTo(2));
        }

        [Test]
        public void PlayWithoutSubscribersDoesNotThrow()
        {
            var haptics = new NullHaptics();
            Assert.DoesNotThrow(() => haptics.Play(HapticKind.HeavyImpact));
            Assert.DoesNotThrow(() => haptics.Play((HapticKind)200));
        }
    }

    public class HapticSettingsTests
    {
        [Test]
        public void DefaultsAreHapticsOnAndFullMotion()
        {
            var s = new SaveData();
            Assert.That(s.Settings.Haptics, Is.True);
            Assert.That(s.Settings.ReduceMotion, Is.False);
        }

        [Test]
        public void RoundTripKeepsBothSettings()
        {
            foreach (bool haptics in new[] { false, true })
            foreach (bool reduceMotion in new[] { false, true })
            {
                var s = new SaveData();
                s.Settings.Haptics = haptics;
                s.Settings.ReduceMotion = reduceMotion;
                string json = SaveSerializer.ToJson(s);
                JsonObject settings = Json.Parse(json).AsObject().GetObject("settings");
                Assert.That(settings["haptics"].AsBool(!haptics), Is.EqualTo(haptics), json);
                Assert.That(settings["reduceMotion"].AsBool(!reduceMotion), Is.EqualTo(reduceMotion), json);

                SaveData back = SaveSerializer.FromJson(json);
                Assert.That(back.Settings.Haptics, Is.EqualTo(haptics));
                Assert.That(back.Settings.ReduceMotion, Is.EqualTo(reduceMotion));
                Assert.That(back.Settings.Extra.ContainsKey("haptics"), Is.False, "known keys are not kept as extra");
                Assert.That(back.Settings.Extra.ContainsKey("reduceMotion"), Is.False);
                Assert.That(SaveSerializer.ToJson(back), Is.EqualTo(json), "stable output");
            }
        }

        [Test]
        public void SavesFromBeforeTheSettingsExistedGetHapticsOn()
        {
            SaveData s = SaveSerializer.FromJson(
                "{\"schemaVersion\":1,\"settings\":{\"language\":\"ne\",\"musicVolume\":0.5}}");
            Assert.That(s.Settings.Haptics, Is.True, "missing key: haptics on");
            Assert.That(s.Settings.ReduceMotion, Is.False, "missing key: full motion");
            Assert.That(s.Settings.Language, Is.EqualTo("ne"));

            SaveData noSettings = SaveSerializer.FromJson("{\"schemaVersion\":1}");
            Assert.That(noSettings.Settings.Haptics, Is.True);
            Assert.That(noSettings.Settings.ReduceMotion, Is.False);
        }

        [Test]
        public void MistypedValuesFallBackToDefaults()
        {
            SaveData s = SaveSerializer.FromJson(
                "{\"schemaVersion\":1,\"settings\":{\"haptics\":\"off\",\"reduceMotion\":1}}");
            Assert.That(s.Settings.Haptics, Is.True);
            Assert.That(s.Settings.ReduceMotion, Is.False);
        }

        [Test]
        public void ExplicitlyDisabledHapticsStayOff()
        {
            SaveData s = SaveSerializer.FromJson(
                "{\"schemaVersion\":1,\"settings\":{\"haptics\":false,\"reduceMotion\":true}}");
            Assert.That(s.Settings.Haptics, Is.False);
            Assert.That(s.Settings.ReduceMotion, Is.True);
        }
    }
}
