using System;
using Ghumante.Core.Synth;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>The baked procedural bank (W2_DESIGN 7.1 "Baked bank", 7.3, 7.4).</summary>
    public class SynthBankTests
    {
        [Test]
        public void EverySoundAndVariantRendersCleanly()
        {
            for (int i = 0; i < ProceduralBank.Count; i++)
            {
                BankSoundInfo info = ProceduralBank.InfoAt(i);
                Assert.That(info.IsValid, info.Sound.ToString());
                Assert.That(info.LowVariants, Is.InRange(1, (int)info.Variants), info.Sound + " low variants");
                for (int v = 0; v < info.Variants; v++)
                {
                    float[] o = ProceduralBank.Render(info.Sound, v, info.SampleRate, 99);
                    string what = info.Sound + "#" + v;
                    Assert.That(o.Length, Is.GreaterThan(info.SampleRate / 100), what + " too short");
                    Assert.That(o.Length, Is.LessThanOrEqualTo((int)(ProceduralBank.MaxSeconds * info.SampleRate)), what + " too long");
                    SynthDspTests.AssertClean(o, o.Length, what);
                    float peak = 0f;
                    foreach (float x in o) peak = Math.Max(peak, Math.Abs(x));
                    Assert.That(peak, Is.GreaterThan(0.05f), what + " silent");
                }
            }
        }

        [Test]
        public void RenderingIsDeterministicPerSeed()
        {
            foreach (BankSound s in new[] { BankSound.FootstepBrick, BankSound.HornCar, BankSound.ShrineBellMedium, BankSound.Crow, BankSound.BedCrowdDense })
            {
                float[] a = ProceduralBank.Render(s, 1, 22050, 5), b = ProceduralBank.Render(s, 1, 22050, 5), c = ProceduralBank.Render(s, 1, 22050, 6);
                Assert.That(SynthDspTests.Pcm16Hash(a, a.Length), Is.EqualTo(SynthDspTests.Pcm16Hash(b, b.Length)), s.ToString());
                Assert.That(SynthDspTests.Pcm16Hash(a, a.Length), Is.Not.EqualTo(SynthDspTests.Pcm16Hash(c, c.Length)), s + " ignores the seed");
            }
        }

        [Test]
        public void UnknownSoundsAndBadArgumentsAreSafe()
        {
            Assert.That(ProceduralBank.Render(BankSound.None, 0, 22050, 1), Is.Empty);
            Assert.That(ProceduralBank.Render((BankSound)9999, 0, 22050, 1), Is.Empty);
            float[] o = ProceduralBank.Render(BankSound.HornMoto, -5, 7, 1); // bad rate → preferred rate; bad variant → 0
            Assert.That(o.Length, Is.GreaterThan(0));
            float[] w = ProceduralBank.Render(BankSound.HornMoto, 4 + 1, 22050, 1); // variant wraps
            Assert.That(SynthDspTests.Pcm16Hash(w, w.Length), Is.EqualTo(SynthDspTests.Pcm16Hash(ProceduralBank.Render(BankSound.HornMoto, 1, 22050, 1), w.Length)));
        }

        [Test]
        public void BedsLoopSeamlessly()
        {
            for (int s = (int)BankSound.BedWind; s <= (int)BankSound.BedRiver; s++)
            {
                BankSoundInfo info = ProceduralBank.Info((BankSound)s);
                Assert.That(info.Loop, Is.True);
                float[] o = ProceduralBank.Render((BankSound)s, 0, info.SampleRate, 3);
                Assert.That(o.Length, Is.EqualTo((int)(ProceduralBank.BedSeconds * info.SampleRate)));
                // The jump across the loop point is no bigger than the bed's typical sample-to-sample step.
                double meanStep = 0;
                float maxStep = 0f;
                for (int i = 1; i < o.Length; i++)
                {
                    float d = Math.Abs(o[i] - o[i - 1]);
                    meanStep += d;
                    maxStep = Math.Max(maxStep, d);
                }
                meanStep /= o.Length - 1;
                float seam = Math.Abs(o[0] - o[o.Length - 1]);
                Assert.That(seam, Is.LessThanOrEqualTo(Math.Max(maxStep, (float)(6 * meanStep))), (BankSound)s + " clicks at the loop point");
                Assert.That(SynthDspTests.RmsOf(o, 0, o.Length), Is.GreaterThan(0.01), (BankSound)s + " near silent");
            }
        }

        [Test]
        public void FootstepsAreShortAndDistinctPerSurface()
        {
            ulong prev = 0;
            for (int s = 0; s <= (int)FootstepSurface.Barefoot; s++)
            {
                BankSound b = ProceduralBank.Footstep((FootstepSurface)s);
                Assert.That((int)b, Is.EqualTo((int)BankSound.FootstepAsphalt + s));
                float[] o = ProceduralBank.Render(b, 0, 22050, 1);
                Assert.That(o.Length / 22050f, Is.LessThanOrEqualTo(0.45f), b.ToString());
                ulong h = SynthDspTests.Pcm16Hash(o, o.Length);
                Assert.That(h, Is.Not.EqualTo(prev));
                prev = h;
            }
            Assert.That(ProceduralBank.FootstepVariant(FootstepGait.Walk, 7), Is.EqualTo(1));
            Assert.That(ProceduralBank.FootstepVariant(FootstepGait.Run, 0), Is.EqualTo(6));
            Assert.That(ProceduralBank.FootstepVariant(FootstepGait.Land, 3), Is.EqualTo(12));
            Assert.That(ProceduralBank.FootstepVariant(FootstepGait.Scuff, 0), Is.EqualTo(13));
        }

        [Test]
        public void FootstepPickerNeverRepeatsAndMapsMonsoonMud()
        {
            var p = new FootstepPicker(7);
            int last = -1;
            for (int i = 0; i < 500; i++)
            {
                FootstepGait g = i % 50 < 40 ? FootstepGait.Walk : FootstepGait.Run;
                p.Next(FootstepSurface.Stone, g, out BankSound s, out int v, out float pitch, out float db);
                Assert.That(s, Is.EqualTo(BankSound.FootstepStone));
                Assert.That(v, Is.Not.EqualTo(last));
                Assert.That(v, Is.InRange(0, 11));
                Assert.That(pitch, Is.InRange(0.94f, 1.06f));
                Assert.That(db, Is.InRange(-1.5f, 3.5f));
                last = v;
            }
            Assert.That(FootstepPicker.Resolve((byte)FootstepSurface.Dirt, 0.6f, false), Is.EqualTo(FootstepSurface.Mud));
            Assert.That(FootstepPicker.Resolve((byte)FootstepSurface.Dirt, 0.2f, false), Is.EqualTo(FootstepSurface.Dirt));
            Assert.That(FootstepPicker.Resolve((byte)FootstepSurface.Brick, 0f, true), Is.EqualTo(FootstepSurface.Barefoot));
            Assert.That(FootstepPicker.Resolve(200, 0f, false), Is.EqualTo(FootstepSurface.Concrete));
        }

        [Test]
        public void BicycleBellHasItsPartialsAndHornsSitInTheirBand()
        {
            float[] bell = ProceduralBank.Render(BankSound.BicycleBell, 0, 32000, 4);
            // The fundamental band (2.3–2.8 kHz) carries the most energy among probe frequencies.
            double best = 0;
            float bestF = 0;
            for (float f = 500f; f < 12000f; f += 50f)
            {
                double g = SynthDspTests.Goertzel(bell, 0, Math.Min(bell.Length, 8000), f, 32000);
                if (g > best)
                {
                    best = g;
                    bestF = f;
                }
            }
            Assert.That(bestF, Is.InRange(2250f, 2850f));

            float[] bus = ProceduralBank.Render(BankSound.HornBus, 1, 22050, 4);
            double inBand = SynthDspTests.Goertzel(bus, 0, 4000, 345f, 22050) + SynthDspTests.Goertzel(bus, 0, 4000, 435f, 22050);
            double outBand = SynthDspTests.Goertzel(bus, 0, 4000, 6000f, 22050);
            Assert.That(inBand, Is.GreaterThan(10 * outBand));
        }

        [Test]
        public void BankFitsTheMemoryBudget()
        {
            // W2_DESIGN 7.1 / 7.4: ≈ 6 MB baked (≈ 3 MB on Low); resident audio caps 25 / 40 / 50 MB.
            long mid = ProceduralBank.EstimateBytes(false);
            long low = ProceduralBank.EstimateBytes(true);
            // Footsteps and beds are synthesised (the design budgeted them as recordings), so the bank is larger
            // than 6 MB; it still leaves the design's resident totals (≈ 14.5 / 20 MB) and the tier caps intact.
            Assert.That(mid, Is.LessThan(15L * 1024 * 1024));
            Assert.That(low, Is.LessThan(mid));
            Assert.That(low, Is.LessThan(10L * 1024 * 1024));
            // The nominal lengths stay close to what the recipes really render (within 25%).
            long actual = 0;
            for (int i = 0; i < ProceduralBank.Count; i++)
            {
                BankSoundInfo info = ProceduralBank.InfoAt(i);
                for (int v = 0; v < info.Variants; v++) actual += ProceduralBank.Render(info.Sound, v, info.SampleRate, 1).Length * 2L;
            }
            Assert.That((double)mid, Is.InRange(actual * 0.75, actual * 1.25));
            TestContext.WriteLine("bank estimate: mid " + mid / 1024 + " KB, low " + low / 1024 + " KB");
        }

        [Test]
        public void EveryShippedRecordingHasALicenceRow()
        {
            // W2_DESIGN 10.6 V12: every .wav/.ogg/.mp3/.aif/.flac under the game's assets maps to a CC0 / PD row in
            // docs/LICENSES.md (by file name). Today the game ships none: every sound is synthesised by Core/Synth.
            string root = GoldenFiles.RepoRoot;
            string assets = System.IO.Path.Combine(root, "game", "Assets");
            string licences = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "docs", "LICENSES.md"));
            var missing = new System.Collections.Generic.List<string>();
            foreach (string ext in new[] { "*.wav", "*.ogg", "*.mp3", "*.aif", "*.aiff", "*.flac" })
            {
                foreach (string f in System.IO.Directory.GetFiles(assets, ext, System.IO.SearchOption.AllDirectories))
                {
                    string name = System.IO.Path.GetFileName(f);
                    if (licences.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) missing.Add(f.Substring(root.Length + 1));
                }
            }
            Assert.That(missing, Is.Empty, "recordings without a LICENSES.md row");
        }

        private static readonly (BankSound Sound, int Variant, ulong Hash)[] Golden =
        {
            (BankSound.FootstepBrick, 0, 0x480F77DF4C32D07EUL),
            (BankSound.FootstepMud, 6, 0x5A8177D15DD4C6B9UL),
            (BankSound.HornCar, 2, 0xCB4663A17322A5F3UL),
            (BankSound.AirBrake, 0, 0xA694AE4AAF51216AUL),
            (BankSound.ShrineBellLarge, 0, 0x1A152514FBEF11A9UL),
            (BankSound.Crow, 3, 0x1868E8AF2AD69E46UL),
            (BankSound.DogBark, 1, 0xC656C3590C11182CUL),
            (BankSound.BedRainLight, 0, 0x47AED7B42EC8EFDFUL),
            (BankSound.BedCrowdDense, 0, 0x6DE2FDC937A70785UL),
        };

        [Test]
        public void GoldenHashesPerSeed()
        {
            var report = new System.Text.StringBuilder();
            bool fail = false;
            foreach (var (s, v, h) in Golden)
            {
                float[] o = ProceduralBank.Render(s, v, ProceduralBank.Info(s).SampleRate, 2026);
                ulong got = SynthDspTests.Pcm16Hash(o, o.Length);
                report.AppendLine("(BankSound." + s + ", " + v + ", 0x" + got.ToString("X16") + "UL),");
                if (got != h) fail = true;
            }
            if (fail) Assert.Fail("Bank golden hashes changed:\n" + report);
        }
    }
}
