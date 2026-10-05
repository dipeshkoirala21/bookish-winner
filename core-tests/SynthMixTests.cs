using System;
using Ghumante.Core.Synth;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Distance, Doppler, voices and ambience logic (W2_DESIGN 7.1, 7.2, 10.4).</summary>
    public class SynthMixTests
    {
        [Test]
        public void RolloffIsOneInsideMinZeroAtMaxAndMonotonic()
        {
            Assert.That(SoundSpace.Rolloff(0f, 3f, 60f), Is.EqualTo(1f));
            Assert.That(SoundSpace.Rolloff(3f, 3f, 60f), Is.EqualTo(1f));
            Assert.That(SoundSpace.Rolloff(6f, 3f, 60f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(SoundSpace.Rolloff(60f, 3f, 60f), Is.EqualTo(0f));
            Assert.That(SoundSpace.Rolloff(1000f, 3f, 60f), Is.EqualTo(0f));
            Assert.That(SoundSpace.Rolloff(float.NaN, 3f, 60f), Is.EqualTo(1f));
            float prev = 1f;
            for (float d = 0f; d <= 61f; d += 0.05f)
            {
                float g = SoundSpace.Rolloff(d, 3f, 60f);
                Assert.That(g, Is.LessThanOrEqualTo(prev + 1e-6f));
                Assert.That(prev - g, Is.LessThan(0.03f), "discontinuity at " + d);
                prev = g;
            }
            // 1/r up to 0.7 × max.
            Assert.That(SoundSpace.Rolloff(42f, 3f, 60f), Is.EqualTo(3f / 42f).Within(1e-5f));
        }

        [Test]
        public void ClassDistancesFollowTheDesign()
        {
            Assert.That(SoundClasses.Get(SoundClass.PlayerFootstep).MaxM, Is.EqualTo(15f));
            Assert.That(SoundClasses.Get(SoundClass.NpcFootstep).MaxM, Is.EqualTo(12f));
            Assert.That(SoundClasses.Get(SoundClass.Bicycle).MaxM, Is.EqualTo(25f));
            Assert.That(SoundClasses.Get(SoundClass.TwoWheeler).MaxM, Is.EqualTo(60f));
            Assert.That(SoundClasses.Get(SoundClass.Car).MaxM, Is.EqualTo(80f));
            Assert.That(SoundClasses.Get(SoundClass.Heavy).MaxM, Is.EqualTo(150f));
            Assert.That(SoundClasses.Get(SoundClass.Horn).MaxM, Is.EqualTo(200f));
            Assert.That(SoundClasses.Get(SoundClass.HornMoto).MaxM, Is.EqualTo(120f));
            Assert.That(SoundClasses.Get(SoundClass.ShrineBell).MaxM, Is.EqualTo(40f));
            Assert.That(SoundClasses.Get(SoundClass.GreatBell).MaxM, Is.EqualTo(400f));
            Assert.That(SoundClasses.Get(SoundClass.Conch).MaxM, Is.EqualTo(150f));
            Assert.That(SoundClasses.Get(SoundClass.DogBark).MaxM, Is.EqualTo(250f));
            Assert.That(SoundClasses.Get(SoundClass.Helicopter).MaxM, Is.EqualTo(4000f));
            Assert.That(SoundClasses.Get(SoundClass.Turboprop).MaxM, Is.EqualTo(6000f));
            Assert.That(SoundClasses.Get(SoundClass.Jet).MaxM, Is.EqualTo(9000f));
            for (int i = 0; i < SoundClasses.Count; i++)
            {
                SoundClassInfo c = SoundClasses.Get((SoundClass)i);
                Assert.That(c.MaxM, Is.GreaterThan(c.MinM), ((SoundClass)i).ToString());
                Assert.That(c.Priority, Is.InRange(0, 256));
            }
            Assert.That(SoundClasses.Get(SoundClass.PlayerEngine).Protected, Is.True);
            Assert.That(SoundClasses.Get(SoundClass.NpcFootstep).Priority, Is.EqualTo(192));
            Assert.That(SoundClasses.ForEngine(EngineModel.BusCity, false), Is.EqualTo(SoundClass.Heavy));
            Assert.That(SoundClasses.ForEngine(EngineModel.BusCity, true), Is.EqualTo(SoundClass.PlayerEngine));
        }

        [Test]
        public void AirAbsorptionMatchesTheTable()
        {
            Assert.That(SoundSpace.AirCutoffHz(30f), Is.EqualTo(0f));
            Assert.That(SoundSpace.AirCutoffHz(200f), Is.EqualTo(11000f).Within(1f));
            Assert.That(SoundSpace.AirCutoffHz(500f), Is.EqualTo(7000f).Within(1f));
            Assert.That(SoundSpace.AirCutoffHz(1000f), Is.EqualTo(4500f).Within(1f));
            Assert.That(SoundSpace.AirCutoffHz(4000f), Is.EqualTo(1800f).Within(1f));
            Assert.That(SoundSpace.AirCutoffHz(20000f), Is.EqualTo(1100f));
            float prev = 30000f;
            for (float d = 60f; d < 9000f; d += 37f)
            {
                float f = SoundSpace.AirCutoffHz(d);
                Assert.That(f, Is.LessThanOrEqualTo(prev));
                prev = f;
            }
        }

        [Test]
        public void DopplerUsesSimulationVelocitiesAndIsClamped()
        {
            // Source 100 m east, driving west towards a still listener at 20 m/s: pitch = 343 / (343 − 20).
            float p = SoundSpace.DopplerPitch(100, 0, 0, -20, 0, 0, 0, 0, 0, 0, 0, 0, 1f);
            Assert.That(p, Is.EqualTo(343f / 323f).Within(1e-4f));
            float away = SoundSpace.DopplerPitch(100, 0, 0, 20, 0, 0, 0, 0, 0, 0, 0, 0, 1f);
            Assert.That(away, Is.LessThan(1f));
            // Listener moving towards the source raises the pitch.
            Assert.That(SoundSpace.DopplerPitch(100, 0, 0, 0, 0, 0, 0, 0, 0, 20, 0, 0, 1f), Is.GreaterThan(1f));
            // Clamp 0.7–1.4, strength 0 = none, coincident positions = none.
            Assert.That(SoundSpace.DopplerPitch(100, 0, 0, -300, 0, 0, 0, 0, 0, 0, 0, 0, 1f), Is.EqualTo(1.4f));
            Assert.That(SoundSpace.DopplerPitch(100, 0, 0, 300, 0, 0, 0, 0, 0, 0, 0, 0, 1f), Is.EqualTo(0.7f));
            Assert.That(SoundSpace.DopplerPitch(100, 0, 0, -20, 0, 0, 0, 0, 0, 0, 0, 0, 0f), Is.EqualTo(1f));
            Assert.That(SoundSpace.DopplerPitch(5, 5, 5, -20, 0, 0, 5, 5, 5, 0, 0, 0, 1f), Is.EqualTo(1f));
            Assert.That(SoundSpace.DopplerPitch(float.NaN, 0, 0, float.NaN, 0, 0, 0, 0, 0, 0, 0, 0, 1f), Is.EqualTo(1f));
            // A floating-origin rebase moves both positions; velocities are unchanged, so the pitch is too.
            float shifted = SoundSpace.DopplerPitch(100 - 2000, 0, 0, -20, 0, 0, -2000, 0, 0, 0, 0, 0, 1f);
            Assert.That(shifted, Is.EqualTo(p).Within(1e-4f));
        }

        [Test]
        public void HelpersForReflectionsSlapbackAndListener()
        {
            Assert.That(SoundSpace.SlapbackDelayS(3f), Is.EqualTo(0.0175f).Within(5e-4f));
            Assert.That(SoundSpace.SlapbackDelayS(6f), Is.EqualTo(0.035f).Within(5e-4f));
            Assert.That(SoundSpace.ReflectionDelayS(0f, 100f), Is.EqualTo(0f));
            Assert.That(SoundSpace.ReflectionDelayS(100f, 1000f), Is.EqualTo(2f * 100f * 0.1f / 343f).Within(1e-5f));
            SoundSpace.ListenerPoint(0, 10, 0, 0, 0, 10, out float x, out float y, out float z);
            Assert.That(y, Is.EqualTo(6.5f).Within(1e-5f));
            Assert.That(z, Is.EqualTo(3.5f).Within(1e-5f));
            var s = new Smoothed();
            Assert.That(s.Step(2f, 0.01f, 0.05f), Is.EqualTo(2f), "first step snaps");
            float v = s.Step(1f, 0.05f, 0.05f);
            Assert.That(v, Is.EqualTo(2f - (1f - (float)Math.Exp(-1))).Within(1e-4f));
        }

        [Test]
        public void TierBudgetsMatchTheDesign()
        {
            VoiceBudget low = VoiceBudget.For(AudioTier.Low), mid = VoiceBudget.For(AudioTier.Mid), high = VoiceBudget.For(AudioTier.High);
            Assert.That(new[] { low.RealVoices, mid.RealVoices, high.RealVoices }, Is.EqualTo(new[] { 24, 32, 48 }));
            Assert.That(new[] { low.SynthVoices, mid.SynthVoices, high.SynthVoices }, Is.EqualTo(new[] { 4, 8, 12 }));
            Assert.That(new[] { low.VirtualVoices, mid.VirtualVoices, high.VirtualVoices }, Is.EqualTo(new[] { 128, 256, 512 }));
            Assert.That(new[] { low.MemoryCapMb, mid.MemoryCapMb, high.MemoryCapMb }, Is.EqualTo(new[] { 25, 40, 50 }));
            Assert.That(high.AircraftVoices, Is.EqualTo(2));
            Assert.That(low.LowBank && !mid.LowBank, Is.True);
            Assert.That(mid.ThermalStepDown().SynthVoices, Is.EqualTo(4));
            Assert.That(low.ClipVoices + low.SynthVoices + low.AircraftVoices + low.BedVoices, Is.LessThanOrEqualTo(low.RealVoices));
        }

        private static VoiceRequest Req(SoundClass c, float db, bool oneShot = true)
        {
            return VoiceRequest.For(c, db, oneShot);
        }

        [Test]
        public void AllocatorCullsCapsAndStealsByPriority()
        {
            var a = new VoiceAllocator(4, VoiceBudget.For(AudioTier.High));
            Assert.That(a.TryAcquire(Req(SoundClass.Crow, -50f), 0, out _, out _), Is.False);
            Assert.That(a.LastDenial, Is.EqualTo(VoiceDenial.TooQuiet));

            // Horn cap 3: the 4th horn replaces the quietest only if it is louder.
            Assert.That(a.TryAcquire(Req(SoundClass.Horn, -20f), 0.0, out int h1, out _), Is.True);
            Assert.That(a.TryAcquire(Req(SoundClass.Horn, -25f), 0.1, out int h2, out _), Is.True);
            Assert.That(a.TryAcquire(Req(SoundClass.Horn, -30f), 0.2, out int h3, out _), Is.True);
            Assert.That(a.TryAcquire(Req(SoundClass.Horn, -40f), 0.3, out _, out _), Is.False);
            Assert.That(a.LastDenial, Is.EqualTo(VoiceDenial.CapReached));
            Assert.That(a.TryAcquire(Req(SoundClass.Horn, -10f), 0.4, out int h4, out bool stole), Is.True);
            Assert.That(stole, Is.True);
            Assert.That(h4, Is.EqualTo(h3));
            Assert.That(a.CountInGroup(CapGroup.Horn), Is.EqualTo(3));

            // Fill the last slot with an NPC footstep (192), then a crow (128) steals it, not a horn (64).
            Assert.That(a.TryAcquire(Req(SoundClass.NpcFootstep, -20f), 0.5, out int fs, out _), Is.True);
            Assert.That(a.TryAcquire(Req(SoundClass.Crow, -20f), 0.6, out int crow, out bool stole2), Is.True);
            Assert.That(stole2 && crow == fs, Is.True);
            // A footstep cannot steal anything more important.
            Assert.That(a.TryAcquire(Req(SoundClass.NpcFootstep, -5f), 0.7, out _, out _), Is.False);
            Assert.That(a.LastDenial, Is.EqualTo(VoiceDenial.NoVoice));

            // Protected (player) sounds always get in, and are never stolen afterwards.
            Assert.That(a.TryAcquire(Req(SoundClass.PlayerFootstep, -60f), 0.8, out int pf, out _), Is.True);
            for (int i = 0; i < 10; i++) a.TryAcquire(Req(SoundClass.Horn, 0f), 1 + i, out _, out _);
            Assert.That(a.IsActive(pf), Is.True);
            a.Release(h1);
            Assert.That(a.IsActive(h1), Is.False);
            Assert.That(h2, Is.Not.EqualTo(h1));
        }

        [Test]
        public void AllocatorLimitsOneShotsPerSecond()
        {
            VoiceBudget low = VoiceBudget.For(AudioTier.Low);
            var a = new VoiceAllocator(64, low);
            int ok = 0;
            for (int i = 0; i < 40; i++)
            {
                if (a.TryAcquire(Req(SoundClass.Bird, -20f), i * 0.01, out int slot, out _))
                {
                    ok++;
                    a.Release(slot);
                }
            }
            Assert.That(ok, Is.EqualTo(low.MaxOneShotsPerSecond));
            Assert.That(a.TryAcquire(Req(SoundClass.Bird, -20f), 1.5, out _, out _), Is.True, "the window slides");
            // Loops are not rate limited.
            Assert.That(a.TryAcquire(Req(SoundClass.Bird, -20f, false), 1.5, out _, out _), Is.True);
        }

        [Test]
        public void NpcFootstepsAreCappedPerTier()
        {
            var a = new VoiceAllocator(32, VoiceBudget.For(AudioTier.Low));
            int ok = 0;
            for (int i = 0; i < 10; i++)
                if (a.TryAcquire(Req(SoundClass.NpcFootstep, -20f), i * 0.2, out _, out _)) ok++;
            Assert.That(ok, Is.EqualTo(2));
        }

        [Test]
        public void SynthVoicesGoToThePlayerThenTheLoudest()
        {
            var c = new SynthCandidate[12];
            for (int i = 0; i < c.Length; i++) c[i] = new SynthCandidate { Id = i, Score = -40f + i };
            c[0].Player = true;
            c[0].Score = -90f;
            c[5].Aircraft = true;
            c[6].Aircraft = true;
            c[7].Aircraft = true;
            c[2].Score = -60f; // culled
            var sel = new bool[12];
            VoiceBudget low = VoiceBudget.For(AudioTier.Low); // 4 synth + 1 aircraft
            int n = SynthAssign.Select(c, c.Length, low, sel);
            Assert.That(sel[0], Is.True, "player always");
            Assert.That(sel[2], Is.False, "below the cull");
            Assert.That(sel[7] && !sel[5] && !sel[6], Is.True, "loudest aircraft only");
            Assert.That(sel[11] && sel[10] && sel[9], Is.True, "loudest NPC engines");
            Assert.That(n, Is.EqualTo(5));
        }

        private static AmbienceInputs Urban(float hour)
        {
            var a = new AmbienceInputs { Hour = hour, Month = 10 };
            a.SetArea(2, 1f);
            return a;
        }

        [Test]
        public void ZoneWeightsSumToOneAndSacredPolygonsOverride()
        {
            Span<float> z = stackalloc float[AmbienceModel.ZoneCount];
            AmbienceModel.ZoneWeights(Urban(11f), z);
            Assert.That(Sum(z), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(z[(int)AmbienceZone.Urban], Is.EqualTo(1f).Within(1e-5f));

            var kora = Urban(7f);
            kora.SacredKind = 4;
            AmbienceModel.ZoneWeights(kora, z);
            Assert.That(Sum(z), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(z[(int)AmbienceZone.StupaKora], Is.GreaterThanOrEqualTo(0.8f));

            var empty = new AmbienceInputs { Hour = 12f, Month = 1, ElevationM = 2000f };
            AmbienceModel.ZoneWeights(empty, z);
            Assert.That(Sum(z), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(z[(int)AmbienceZone.Hilltop], Is.GreaterThan(0.5f));
        }

        [Test]
        public void TimeBandsAndTheirBlend()
        {
            Assert.That(AmbienceModel.BandOf(3f), Is.EqualTo(TimeBand.LateNight));
            Assert.That(AmbienceModel.BandOf(5f), Is.EqualTo(TimeBand.Dawn));
            Assert.That(AmbienceModel.BandOf(9f), Is.EqualTo(TimeBand.Rush));
            Assert.That(AmbienceModel.BandOf(12f), Is.EqualTo(TimeBand.Day));
            Assert.That(AmbienceModel.BandOf(17f), Is.EqualTo(TimeBand.Evening));
            Assert.That(AmbienceModel.BandOf(20f), Is.EqualTo(TimeBand.Night));
            Assert.That(AmbienceModel.BandOf(23.5f), Is.EqualTo(TimeBand.LateNight));
            Assert.That(AmbienceModel.BandOf(-1f), Is.EqualTo(TimeBand.LateNight));
            Assert.That(AmbienceModel.Multipliers(9f).Traffic, Is.EqualTo(1f));
            Assert.That(AmbienceModel.Multipliers(2f).Dogs, Is.EqualTo(1f));
            // Continuous over the day: no jump bigger than a small step per minute.
            TimeMultipliers prev = AmbienceModel.Multipliers(0f);
            for (int m = 1; m <= 24 * 60; m++)
            {
                TimeMultipliers cur = AmbienceModel.Multipliers(m / 60f);
                Assert.That(Math.Abs(cur.Traffic - prev.Traffic), Is.LessThan(0.05f), "traffic at minute " + m);
                Assert.That(Math.Abs(cur.Dogs - prev.Dogs), Is.LessThan(0.05f), "dogs at minute " + m);
                prev = cur;
            }
            Assert.That(AmbienceModel.SeasonOf(7), Is.EqualTo(Season.Monsoon));
            Assert.That(AmbienceModel.SeasonOf(10), Is.EqualTo(Season.Autumn));
            Assert.That(AmbienceModel.SeasonOf(1), Is.EqualTo(Season.Winter));
            Assert.That(AmbienceModel.SeasonOf(4), Is.EqualTo(Season.Spring));
        }

        [Test]
        public void BedsFollowZoneTimeAndWeather()
        {
            Span<float> b = stackalloc float[AmbienceModel.BedCount];
            int traffic = BankSound.BedTrafficHum - BankSound.BedBase;
            int birds = BankSound.BedBirdsPeri - BankSound.BedBase;
            int rainH = BankSound.BedRainHeavy - BankSound.BedBase;
            int crowd = BankSound.BedCrowdDense - BankSound.BedBase;
            AmbienceModel.BedGains(Urban(9f), b);
            float rush = b[traffic];
            AmbienceModel.BedGains(Urban(3f), b);
            Assert.That(b[traffic], Is.LessThan(rush * 0.3f), "late night traffic is quieter");
            foreach (float g in b) Assert.That(g, Is.InRange(0f, 1f));

            var peri = new AmbienceInputs { Hour = 6.5f, Month = 7 };
            peri.SetArea(3, 1f);
            AmbienceModel.BedGains(peri, b);
            float dry = b[birds];
            peri.Rain01 = 1f;
            AmbienceModel.BedGains(peri, b);
            Assert.That(b[birds], Is.LessThan(dry * 0.3f), "heavy rain ducks birds by ~12 dB");
            Assert.That(b[rainH], Is.EqualTo(1f));

            var core = new AmbienceInputs { Hour = 12f, Month = 10 };
            core.SetArea(1, 1f);
            AmbienceModel.BedGains(core, b);
            Assert.That(b[crowd], Is.GreaterThan(0.8f), "old core is a dense crowd at midday");
        }

        [Test]
        public void SpotsRespectTheirTimeWindows()
        {
            Span<float> r = stackalloc float[AmbienceModel.SpotCount];
            var core = new AmbienceInputs { Month = 4 };
            core.SetArea(1, 1f);
            core.Hour = 9f;
            AmbienceModel.SpotRates(core, r);
            Assert.That(r[(int)SpotKind.Shutter], Is.GreaterThan(0f), "shutters go up 08:30–10:00");
            Assert.That(r[(int)SpotKind.PressureCooker], Is.GreaterThan(0f));
            core.Hour = 13f;
            AmbienceModel.SpotRates(core, r);
            Assert.That(r[(int)SpotKind.Shutter], Is.EqualTo(0f));
            Assert.That(r[(int)SpotKind.PressureCooker], Is.EqualTo(0f));
            Assert.That(r[(int)SpotKind.Conch], Is.EqualTo(0f));

            var temple = new AmbienceInputs { Hour = 5.5f, Month = 10, SacredKind = 1 };
            temple.SetArea(1, 1f);
            AmbienceModel.SpotRates(temple, r);
            Assert.That(r[(int)SpotKind.Conch], Is.GreaterThan(0f), "conch at dawn worship");
            Assert.That(r[(int)SpotKind.ShrineBell], Is.GreaterThan(3f));
            Assert.That(r[(int)SpotKind.Koel], Is.EqualTo(0f), "no koel in October");

            var night = Urban(2f);
            AmbienceModel.SpotRates(night, r);
            Assert.That(r[(int)SpotKind.Dog], Is.GreaterThan(r[(int)SpotKind.Crow]), "night bark chains, no crows");
            for (int i = 0; i < AmbienceModel.SpotCount; i++)
            {
                Assert.That(AmbienceModel.SpotSound((SpotKind)i), Is.Not.EqualTo(BankSound.None));
                Assert.That(ProceduralBank.Info(AmbienceModel.SpotSound((SpotKind)i)).IsValid, Is.True);
            }
        }

        [Test]
        public void SnapshotsBySacredZoneLaneAndVehicle()
        {
            Span<float> w = stackalloc float[5];
            var a = Urban(12f);
            a.SacredKind = 2;
            AmbienceModel.SnapshotWeights(a, w);
            Assert.That(w[(int)MixSnapshot.Courtyard], Is.EqualTo(1f));
            SnapshotParams court = AmbienceModel.Blend(w, 0f);
            Assert.That(court.StreetDb, Is.EqualTo(-9f).Within(1e-4f));
            Assert.That(court.StreetLpfHz, Is.EqualTo(2500f).Within(1e-2f));

            a.SacredKind = 3;
            AmbienceModel.SnapshotWeights(a, w);
            Assert.That(AmbienceModel.Blend(w, 0f).ReverbRt60, Is.EqualTo(1.2f).Within(1e-4f));

            a.SacredKind = 0;
            a.LaneWidthM = 3f;
            AmbienceModel.SnapshotWeights(a, w);
            SnapshotParams galli = AmbienceModel.Blend(w, a.LaneWidthM);
            Assert.That(w[(int)MixSnapshot.Galli], Is.EqualTo(1f));
            Assert.That(galli.EchoDelayMs, Is.EqualTo(17.5f).Within(0.5f));

            a.InVehicle = true;
            AmbienceModel.SnapshotWeights(a, w);
            Assert.That(AmbienceModel.Blend(w, 0f).StreetDb, Is.EqualTo(-8f).Within(1e-4f));
        }

        private static float Sum(Span<float> s)
        {
            float t = 0f;
            foreach (float x in s) t += x;
            return t;
        }
    }
}
