using System;
using System.Threading;
using Ghumante.Core.Synth;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>DSP primitives of the engine-free synth core (W2_DESIGN 7.1).</summary>
    public class SynthDspTests
    {
        [Test]
        public void SineTableMatchesMathSin()
        {
            double worst = 0;
            for (int i = 0; i < 10000; i++)
            {
                double p = i / 10000.0 * 3.0 - 1.0; // any phase, including negative
                worst = Math.Max(worst, Math.Abs(Dsp.Sin(p) - Math.Sin(2 * Math.PI * p)));
            }
            Assert.That(worst, Is.LessThan(1e-5));
            Assert.That(Dsp.Sin01(0.25f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Dsp.Cos(0.0), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void HannWindowShape()
        {
            Assert.That(Dsp.Hann(0f), Is.EqualTo(0f));
            Assert.That(Dsp.Hann(1f), Is.EqualTo(0f));
            Assert.That(Dsp.Hann(0.5f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Dsp.Hann(0.25f), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(Dsp.Hann(0.9f), Is.EqualTo(Dsp.Hann(0.1f)).Within(1e-4f));
        }

        [Test]
        public void SoftClipIsBoundedOddAndMonotonic()
        {
            float prev = -2f;
            for (int i = -1000; i <= 1000; i++)
            {
                float x = i / 100f;
                float y = Dsp.SoftClip(x);
                Assert.That(y, Is.InRange(-1f, 1f));
                Assert.That(y, Is.GreaterThanOrEqualTo(prev));
                Assert.That(Dsp.SoftClip(-x), Is.EqualTo(-y).Within(1e-6f));
                prev = y;
            }
            Assert.That(Dsp.SoftClip(0.1f), Is.EqualTo((float)Math.Tanh(0.1)).Within(1e-3f));
        }

        [Test]
        public void BandLimitedOscillatorsStayBounded()
        {
            float dt = 440f / 48000f;
            float t = 0f;
            for (int i = 0; i < 48000; i++)
            {
                Assert.That(Math.Abs(Dsp.Saw(t, dt)), Is.LessThanOrEqualTo(1.0001f));
                Assert.That(Math.Abs(Dsp.Square(t, dt)), Is.LessThanOrEqualTo(1.0001f));
                t += dt;
                if (t >= 1f) t -= 1f;
            }
        }

        [Test]
        public void NoiseIsDeterministicBoundedAndZeroMean()
        {
            var a = new Noise(1234);
            var b = new Noise(1234);
            var z = new Noise(0); // zero seed is remapped, never stuck
            double sum = 0;
            for (int i = 0; i < 100000; i++)
            {
                float w = a.White();
                Assert.That(w, Is.EqualTo(b.White()));
                Assert.That(w, Is.InRange(-1f, 1f));
                sum += w;
                Assert.That(Math.Abs(a.Pink()), Is.LessThan(2f));
                b.Pink();
                Assert.That(Math.Abs(a.Brown()), Is.LessThan(4f));
                b.Brown();
            }
            Assert.That(sum / 100000, Is.InRange(-0.02, 0.02));
            Assert.That(z.NextUInt(), Is.Not.EqualTo(0u));
        }

        [Test]
        public void BiquadLowPassPassesDcAndCutsHighs()
        {
            var lp = default(Biquad);
            lp.Set(BiquadKind.LowPass, 500f, 0.707f, 48000);
            float y = 0f;
            for (int i = 0; i < 4800; i++) y = lp.Process(1f);
            Assert.That(y, Is.EqualTo(1f).Within(1e-3f));

            lp.Reset();
            double hi = Rms(i => Dsp.Sin(8000.0 * i / 48000), lp);
            Assert.That(hi, Is.LessThan(0.02), "8 kHz through a 500 Hz low-pass");

            var bp = default(Biquad);
            bp.Set(BiquadKind.BandPass, 1000f, 2f, 48000);
            double on = Rms(i => Dsp.Sin(1000.0 * i / 48000), bp);
            bp.Reset();
            double off = Rms(i => Dsp.Sin(5000.0 * i / 48000), bp);
            Assert.That(on, Is.GreaterThan(0.6));
            Assert.That(off, Is.LessThan(on * 0.3));
        }

        [Test]
        public void BiquadRecoversFromNonFiniteInput()
        {
            var lp = default(Biquad);
            lp.Set(BiquadKind.LowPass, 1000f, 0.7f, 48000);
            lp.Process(float.NaN);
            lp.Process(float.PositiveInfinity);
            float y = 0f;
            for (int i = 0; i < 1000; i++) y = lp.Process(0.5f);
            Assert.That(float.IsNaN(y) || float.IsInfinity(y), Is.False);
            Assert.That(y, Is.EqualTo(0.5f).Within(1e-2f));
        }

        [Test]
        public void ModalBankRingsAtItsFrequencyAndDecays()
        {
            var bank = new ModalBank(4);
            Assert.That(bank.Add(440f, 0.5f, 1f, 48000), Is.True);
            Assert.That(bank.Add(30000f, 0.5f, 1f, 48000), Is.False, "above 0.45 fs is ignored");
            var buf = new float[48000];
            buf[0] = bank.Process(1f);
            for (int i = 1; i < buf.Length; i++) buf[i] = bank.Process(0f);
            double early = 0, late = 0;
            for (int i = 0; i < 4800; i++) early = Math.Max(early, Math.Abs(buf[i]));
            for (int i = 24000; i < 28800; i++) late = Math.Max(late, Math.Abs(buf[i]));
            // T60 0.5 s: after 0.5 s the level is 60 dB down.
            Assert.That(late / early, Is.LessThan(0.002));
            Assert.That(early, Is.InRange(0.5, 1.5));
            Assert.That(Goertzel(buf, 0, 4800, 440f, 48000), Is.GreaterThan(5 * Goertzel(buf, 0, 4800, 600f, 48000)));
        }

        [Test]
        public void GrainPlayerOverlapsHannGrains()
        {
            var src = new float[1000];
            for (int i = 0; i < src.Length; i++) src[i] = 1f;
            var g = new GrainPlayer(2) { Source = src };
            Assert.That(g.Trigger(0, 100, 1f, 1f), Is.True);
            Assert.That(g.Trigger(0, 100, 1f, 1f), Is.True);
            Assert.That(g.Trigger(0, 100, 1f, 1f), Is.False, "capacity reached");
            float peak = 0f;
            for (int i = 0; i < 100; i++) peak = Math.Max(peak, g.Process());
            Assert.That(peak, Is.EqualTo(2f).Within(0.05f));
            Assert.That(g.Active, Is.EqualTo(0));
        }

        [Test]
        public void DelayLineReadsWhatWasWritten()
        {
            var d = new DelayLine(16);
            for (int i = 1; i <= 10; i++) d.Write(i);
            Assert.That(d.Read(1f), Is.EqualTo(10f));
            Assert.That(d.Read(3f), Is.EqualTo(8f));
            Assert.That(d.Read(2.5f), Is.EqualTo(8.5f).Within(1e-5f));
        }

        private struct Pair
        {
            public float A, B;
            public long C;
        }

        [Test]
        public void ParamMailboxNeverHandsOutATornValue()
        {
            var box = new ParamMailbox<Pair>();
            Assert.That(box.Read(out Pair first), Is.False);
            Assert.That(first.A, Is.EqualTo(0f));
            box.Write(new Pair { A = 1, B = -1, C = 3 });
            Assert.That(box.Read(out Pair p), Is.True);
            Assert.That(p.A, Is.EqualTo(1f));
            Assert.That(box.Read(out p), Is.False, "no change since the last read");

            int torn = 0;
            var stop = false;
            var writer = new Thread(() =>
            {
                for (int i = 0; i < 200000 && !Volatile.Read(ref stop); i++) box.Write(new Pair { A = i, B = -i, C = i * 3L });
            });
            writer.Start();
            for (int i = 0; i < 200000; i++)
            {
                box.Read(out Pair v);
                if (v.B != -v.A || v.C != (long)v.A * 3L) torn++;
            }
            Volatile.Write(ref stop, true);
            writer.Join();
            Assert.That(torn, Is.EqualTo(0));
        }

        [Test]
        public void SeedMixIsDeterministicAndSpreads()
        {
            Assert.That(Dsp.Mix(1, 2, 3), Is.EqualTo(Dsp.Mix(1, 2, 3)));
            Assert.That(Dsp.Mix(1, 2, 3), Is.Not.EqualTo(Dsp.Mix(1, 2, 4)));
            Assert.That(Dsp.Mix(0, 0, 0), Is.Not.EqualTo(0u));
        }

        // ---- helpers shared by the synth tests ----

        internal static double Rms(Func<int, float> signal, Biquad f)
        {
            double sum = 0;
            int n = 9600;
            for (int i = 0; i < n; i++)
            {
                float y = f.Process(signal(i));
                if (i >= 2400) sum += y * y;
            }
            return Math.Sqrt(sum / (n - 2400)) * Math.Sqrt(2);
        }

        /// <summary>Magnitude of one frequency over buf[start..start+n) (Goertzel).</summary>
        internal static double Goertzel(float[] buf, int start, int n, float hz, int sr)
        {
            double w = 2 * Math.PI * hz / sr;
            double c = 2 * Math.Cos(w);
            double s1 = 0, s2 = 0;
            for (int i = 0; i < n; i++)
            {
                double s = buf[start + i] + c * s1 - s2;
                s2 = s1;
                s1 = s;
            }
            return Math.Sqrt(Math.Max(0, s1 * s1 + s2 * s2 - c * s1 * s2)) / n;
        }

        internal static void AssertClean(float[] buf, int n, string what)
        {
            for (int i = 0; i < n; i++)
            {
                float v = buf[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) Assert.Fail(what + ": non-finite sample at " + i);
                if (v > 1f || v < -1f) Assert.Fail(what + ": sample " + v + " out of [-1, 1] at " + i);
            }
        }

        internal static double RmsOf(float[] buf, int start, int n)
        {
            double s = 0;
            for (int i = 0; i < n; i++) s += buf[start + i] * (double)buf[start + i];
            return Math.Sqrt(s / Math.Max(1, n));
        }

        /// <summary>FNV-1a 64 over the samples quantised to 16-bit PCM (robust to last-bit float noise).</summary>
        internal static ulong Pcm16Hash(float[] buf, int n)
        {
            ulong h = 0xCBF29CE484222325;
            for (int i = 0; i < n; i++)
            {
                int q = (int)Math.Round(Math.Max(-1f, Math.Min(1f, buf[i])) * 32767f);
                ushort u = (ushort)(short)q;
                h = unchecked((h ^ (byte)u) * 0x100000001B3);
                h = unchecked((h ^ (byte)(u >> 8)) * 0x100000001B3);
            }
            return h;
        }
    }
}
