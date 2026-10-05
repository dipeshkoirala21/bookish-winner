using System;

namespace Ghumante.Core.Synth
{
    // Birds, animals and the ambience beds (W2_DESIGN 7.2–7.3; A §2.7, §2.9, §2.10, §4.3).
    public static partial class ProceduralBank
    {
        private static float[] RenderNature(BankSound s, ref BankContext c)
        {
            switch (s)
            {
                case BankSound.Crow: return Crow(ref c);
                case BankSound.PigeonCoo: return PigeonCoo(ref c);
                case BankSound.PigeonFlock: return PigeonFlock(ref c);
                case BankSound.Myna: return Myna(ref c);
                case BankSound.Sparrow: return Sparrow(ref c);
                case BankSound.Kite: return Kite(ref c);
                case BankSound.DogBark: return DogBark(ref c);
                case BankSound.CowMoo: return CowMoo(ref c);
                case BankSound.Rooster: return Rooster(ref c);
                case BankSound.Cricket: return Cricket(ref c);
                case BankSound.Koel: return Koel(ref c);
                case BankSound.Bulbul: return Bulbul(ref c);
                default: return new float[0];
            }
        }

        private static float[] Crow(ref BankContext c)
        {
            // House crow "kaa": glottal pulse 25–40 Hz, formants 700 Hz / 1.5 kHz, 0.25–0.45 s, 1–4 in a row.
            int calls = 1 + c.Variant % 3;
            float len = 0f;
            var starts = new float[calls];
            var lens = new float[calls];
            for (int i = 0; i < calls; i++)
            {
                starts[i] = len;
                lens[i] = c.Range(0.25f, 0.45f);
                len += lens[i] + c.Range(0.15f, 0.3f);
            }
            var o = new float[c.Samples(len)];
            float g0 = c.Range(25f, 40f);
            for (int i = 0; i < calls; i++)
            {
                BankKit.Call(o, starts[i], ref c, lens[i], new[] { g0 * 1.1f, g0 * 1.25f, g0 }, new[] { 700f, 1500f, 2600f }, new[] { 4f, 5f, 6f },
                             0.25f, 1f, 0.02f, 0.08f);
            }
            Dsp.Normalize(o, 0, o.Length, 0.7f);
            return o;
        }

        private static float[] PigeonCoo(ref BankContext c)
        {
            // Coo: sine 300–550 Hz with throat AM 20–30 Hz, rising–falling, 0.5–1.2 s.
            float len = c.Range(0.5f, 1.2f);
            var o = new float[c.Samples(len)];
            float f = c.Range(300f, 420f);
            float am = c.Range(20f, 30f);
            int n = o.Length;
            double ph = 0, aph = 0;
            float invSr = 1f / c.Rate;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                float contour = u < 0.4f ? Dsp.Lerp(1f, 1.3f, u / 0.4f) : Dsp.Lerp(1.3f, 0.95f, (u - 0.4f) / 0.6f);
                ph += f * contour * invSr;
                aph += am * invSr;
                float y = Dsp.Sin(ph) + 0.3f * Dsp.Sin(ph * 2);
                o[i] = y * (0.55f + 0.45f * Dsp.Sin(aph)) * Dsp.Hann(u);
            }
            Dsp.Normalize(o, 0, o.Length, 0.5f);
            return o;
        }

        private static float[] PigeonFlock(ref BankContext c)
        {
            // Flock take-off: 40–200 wing claps over 0.6–1.5 s, each a 10 ms LF thud + 2–4 kHz flutter.
            float span = c.Range(0.6f, 1.5f);
            int claps = 40 + c.Rng.Index(161);
            var o = new float[c.Samples(span + 0.3f)];
            for (int i = 0; i < claps; i++)
            {
                float u = c.Rng.Next01();
                float t = span * (float)Math.Sqrt(u) * 0.9f;
                float g = c.Range(0.3f, 1f) * (1f - 0.5f * u);
                BankKit.Tone(o, t, ref c, c.Range(70f, 110f), 60f, 0.012f, 0.001f, 0.008f, 0.4f * g, Wave.Sine);
                BankKit.NoiseBurst(o, t, ref c, 0.0005f, 0.006f, 0.03f, BiquadKind.BandPass, c.Range(2000f, 4000f), 1.2f, 0.5f * g);
            }
            BankKit.FadeOut(o, c, 0.2f);
            Dsp.Normalize(o, 0, o.Length, 0.8f);
            return o;
        }

        private static float[] Myna(ref BankContext c)
        {
            // Phrase of 4–10 FM chirps 1–4 kHz, 50–200 ms each.
            int notes = 4 + c.Rng.Index(7);
            var o = new float[c.Samples(notes * 0.22f + 0.1f)];
            float t = 0f;
            for (int i = 0; i < notes; i++)
            {
                float len = c.Range(0.05f, 0.2f);
                float f0 = c.Range(1000f, 4000f);
                float f1 = f0 * c.Range(0.6f, 1.6f);
                BankKit.Tone(o, t, ref c, f0, f1, len, 0.008f, 0.02f, c.Range(0.4f, 1f), Wave.Sine, new[] { 0.15f }, c.Range(0f, 40f), 0.03f);
                t += len + c.Range(0.01f, 0.06f);
            }
            Dsp.Normalize(o, 0, o.Length, 0.45f);
            return o;
        }

        private static float[] Sparrow(ref BankContext c)
        {
            // "Chirp" 3–6 kHz, 60–120 ms, rapid series of 3–6.
            int notes = 3 + c.Rng.Index(4);
            var o = new float[c.Samples(notes * 0.2f + 0.05f)];
            float t = 0f;
            for (int i = 0; i < notes; i++)
            {
                float len = c.Range(0.06f, 0.12f);
                float f0 = c.Range(3000f, 6000f);
                BankKit.Tone(o, t, ref c, f0, f0 * c.Range(0.7f, 0.9f), len, 0.005f, 0.03f, 0.8f, Wave.Sine, null, 60f, 0.05f);
                t += len + c.Range(0.03f, 0.08f);
            }
            Dsp.Normalize(o, 0, o.Length, 0.35f);
            return o;
        }

        private static float[] Kite(ref BankContext c)
        {
            // Black kite: whinnying whistle 4.5 → 2.5 kHz with an 8–12 Hz trill, 1–2 s.
            float len = c.Range(1f, 1.8f);
            var o = new float[c.Samples(len)];
            BankKit.Tone(o, 0f, ref c, c.Range(4300f, 4700f), c.Range(2400f, 2700f), len, 0.05f, 0.2f, 0.8f, Wave.Sine, new[] { 0.1f },
                         c.Range(8f, 12f), 0.04f);
            Dsp.Normalize(o, 0, o.Length, 0.4f);
            return o;
        }

        private static float[] DogBark(ref BankContext c)
        {
            // Bark: pulse f0 200–400 Hz through formants 500–900 Hz and 1.5–2.5 kHz, 0.15–0.3 s; 1–3 barks.
            int barks = 1 + c.Variant % 3;
            float len = 0f;
            var o = new float[c.Samples(barks * 0.55f)];
            float f0 = c.Range(200f, 400f);
            float f1 = c.Range(500f, 900f), f2 = c.Range(1500f, 2500f);
            for (int i = 0; i < barks; i++)
            {
                float l = c.Range(0.15f, 0.3f);
                BankKit.Call(o, len, ref c, l, new[] { f0 * 0.9f, f0 * 1.2f, f0 * 0.8f }, new[] { f1, f2 }, new[] { 3f, 4f }, 0.35f, 1f, 0.005f, 0.06f);
                len += l + c.Range(0.12f, 0.25f);
            }
            Dsp.Normalize(o, 0, o.Length, 0.8f);
            return o;
        }

        private static float[] CowMoo(ref BankContext c)
        {
            // Moo: 120–200 Hz saw, formant glide, 1–2 s.
            float len = c.Range(1f, 1.8f);
            var o = new float[c.Samples(len + 0.1f)];
            float f0 = c.Range(120f, 200f);
            BankKit.Call(o, 0f, ref c, len, new[] { f0 * 0.9f, f0, f0 * 1.05f, f0 * 0.85f }, new[] { 400f, 1100f }, new[] { 3f, 4f }, 0.1f, 1f,
                         0.15f, 0.3f, 2f);
            Dsp.Normalize(o, 0, o.Length, 0.7f);
            return o;
        }

        private static float[] Rooster(ref BankContext c)
        {
            // Crow of 4 syllables, 1.5–2.5 s, f0 500–700 Hz with harmonics.
            float f0 = c.Range(500f, 700f);
            var o = new float[c.Samples(2.4f)];
            float[] lens = { 0.18f, 0.22f, 0.3f, c.Range(0.7f, 1.1f) };
            float[] pitch = { 0.9f, 1.05f, 1.15f, 1.0f };
            float t = 0.02f;
            for (int i = 0; i < 4; i++)
            {
                float p = f0 * pitch[i];
                BankKit.Call(o, t, ref c, lens[i], new[] { p, p * 1.08f, p * 0.92f }, new[] { 1100f, 2400f, 3500f }, new[] { 3f, 4f, 5f }, 0.15f, 1f,
                             0.02f, 0.06f);
                t += lens[i] + 0.03f;
            }
            BankKit.FadeOut(o, c, 0.1f);
            Dsp.Normalize(o, 0, o.Length, 0.7f);
            return o;
        }

        private static float[] Cricket(ref BankContext c)
        {
            // 4.5 kHz sine pulses: 3–5 pulses of 15 ms per chirp, 2–4 chirps.
            var o = new float[c.Samples(1.4f)];
            float f = c.Range(4300f, 4800f);
            float t = 0f;
            int chirps = 2 + c.Rng.Index(3);
            for (int k = 0; k < chirps; k++)
            {
                int pulses = 3 + c.Rng.Index(3);
                for (int p = 0; p < pulses; p++) BankKit.Tone(o, t + p * 0.025f, ref c, f, f, 0.015f, 0.002f, 0.005f, 0.6f, Wave.Sine);
                t += c.Range(0.3f, 0.45f);
            }
            Dsp.Normalize(o, 0, o.Length, 0.3f);
            return o;
        }

        private static float[] Koel(ref BankContext c)
        {
            // Rising "ko-el" repeated, each repeat higher, f0 ≈ 1–2 kHz.
            int repeats = 3 + c.Variant % 2;
            var o = new float[c.Samples(repeats * 0.7f + 0.2f)];
            float f = c.Range(1000f, 1250f);
            float t = 0f;
            for (int r = 0; r < repeats; r++)
            {
                float k = 1f + 0.08f * r;
                BankKit.Tone(o, t, ref c, f * k, f * k * 1.05f, 0.18f, 0.01f, 0.05f, 0.8f, Wave.Sine, new[] { 0.15f });
                BankKit.Tone(o, t + 0.22f, ref c, f * k * 1.25f, f * k * 1.45f, 0.3f, 0.01f, 0.08f, 0.9f, Wave.Sine, new[] { 0.15f });
                t += c.Range(0.6f, 0.7f);
            }
            Dsp.Normalize(o, 0, o.Length, 0.4f);
            return o;
        }

        private static float[] Bulbul(ref BankContext c)
        {
            // Cheerful 3–5 note whistles, 1.5–3.5 kHz.
            int notes = 3 + c.Rng.Index(3);
            var o = new float[c.Samples(notes * 0.2f + 0.1f)];
            float t = 0f;
            for (int i = 0; i < notes; i++)
            {
                float len = c.Range(0.08f, 0.16f);
                float f0 = c.Range(1500f, 3500f);
                BankKit.Tone(o, t, ref c, f0, f0 * c.Range(0.85f, 1.2f), len, 0.01f, 0.03f, 0.8f, Wave.Sine, new[] { 0.1f });
                t += len + c.Range(0.02f, 0.05f);
            }
            Dsp.Normalize(o, 0, o.Length, 0.4f);
            return o;
        }

        // ---------------------------------------------------------------------------------------------------
        // Ambience beds: seamless loops of BedSeconds.
        // ---------------------------------------------------------------------------------------------------

        private const float BedOverlapS = 0.5f;

        private static float[] RenderBed(BankSound s, ref BankContext c)
        {
            int loop = c.Samples(BedSeconds);
            var full = new float[loop + c.Samples(BedOverlapS)];
            switch (s)
            {
                case BankSound.BedWind: Wind(full, ref c, false); break;
                case BankSound.BedWindLeaves: Wind(full, ref c, true); break;
                case BankSound.BedRainLight: Rain(full, ref c, 700f); break;
                case BankSound.BedRainHeavy: Rain(full, ref c, 4000f); break;
                case BankSound.BedCrowdDense: Walla(full, ref c, 40, 0.9f); break;
                case BankSound.BedCrowdLight: Walla(full, ref c, 12, 0.6f); break;
                case BankSound.BedCrowdKora: Walla(full, ref c, 20, 0.4f); break;
                case BankSound.BedTrafficHum: TrafficHum(full, ref c, false); break;
                case BankSound.BedTrafficHeavy: TrafficHum(full, ref c, true); break;
                case BankSound.BedCityHum: CityHum(full, ref c); break;
                case BankSound.BedCourtyard: Courtyard(full, ref c); break;
                case BankSound.BedBirdsPeri: BirdsBed(full, ref c); break;
                case BankSound.BedInsectsNight: InsectsBed(full, ref c); break;
                case BankSound.BedRiver: River(full, ref c); break;
            }
            float[] o = BankKit.Loopify(full, loop);
            Dsp.Normalize(o, 0, o.Length, 0.7f);
            return o;
        }

        private static void Wind(float[] o, ref BankContext c, bool leaves)
        {
            // Pink noise → body band-pass 300–800 Hz and whistle band 1–3 kHz (Q 8), gust LFOs 0.03 / 0.11 / 0.27 Hz.
            var body = default(Biquad);
            body.Set(BiquadKind.BandPass, c.Range(350f, 600f), 0.7f, c.Rate);
            var whistle = default(Biquad);
            whistle.Set(BiquadKind.BandPass, c.Range(1200f, 2500f), 8f, c.Rate);
            var rustle = default(Biquad);
            rustle.Set(BiquadKind.BandPass, 4500f, 0.5f, c.Rate);
            float p1 = c.Rng.Next01(), p2 = c.Rng.Next01(), p3 = c.Rng.Next01();
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / c.Rate;
                float gust = 0.55f + 0.2f * Dsp.Sin(0.03 * t + p1) + 0.15f * Dsp.Sin(0.11 * t + p2) + 0.1f * Dsp.Sin(0.27 * t + p3);
                float pink = c.Rng.Pink();
                float y = body.Process(pink) * 2.5f * gust + whistle.Process(pink) * 0.6f * gust * gust;
                if (leaves) y += rustle.Process(c.Rng.White()) * 0.5f * gust * gust * gust;
                o[i] = y;
            }
        }

        private static void Rain(float[] o, ref BankContext c, float dropsPerS)
        {
            // Poisson drops (1–4 ms bursts through random band-passes) + a filtered pink bed.
            var bed = default(Biquad);
            bed.Set(BiquadKind.LowPass, dropsPerS > 2000f ? 5000f : 3500f, 0.7f, c.Rate);
            var drop = new Biquad[3];
            drop[0].Set(BiquadKind.BandPass, 2500f, 1.5f, c.Rate);
            drop[1].Set(BiquadKind.BandPass, 4500f, 1.5f, c.Rate);
            drop[2].Set(BiquadKind.BandPass, 1500f, 1.5f, c.Rate);
            float p = dropsPerS / c.Rate;
            var env = new float[3];
            float bedLevel = dropsPerS > 2000f ? 0.8f : 0.35f;
            for (int i = 0; i < o.Length; i++)
            {
                if (c.Rng.Next01() < p) env[c.Rng.Index(3)] = c.Range(0.3f, 1f);
                float y = bed.Process(c.Rng.Pink()) * bedLevel;
                for (int k = 0; k < 3; k++)
                {
                    if (env[k] < 1e-3f) continue;
                    y += drop[k].Process(c.Rng.White() * env[k]) * 0.6f;
                    env[k] *= 0.93f;
                }
                o[i] = y;
            }
        }

        private static void Walla(float[] o, ref BankContext c, int talkers, float footsteps)
        {
            // Unintelligible babble (A §2.7): per talker, syllables at 3–6/s; each syllable is a Hann-windowed
            // grain of noise + a 100–250 Hz pulse through two random vowel formants (F1 300–800, F2 900–2,400 Hz).
            // No words in any language. Plus a granular crowd-footstep stream.
            int n = o.Length;
            var f1 = default(Biquad);
            var f2 = default(Biquad);
            for (int t = 0; t < talkers; t++)
            {
                float pitch = c.Range(100f, 250f);
                float rate = c.Range(3f, 6f);
                float gain = c.Range(0.4f, 1f);
                float pos = c.Range(0f, 0.3f);
                double ph = 0;
                while (pos < (float)n / c.Rate)
                {
                    float syl = c.Range(0.08f, 0.22f);
                    int at = (int)(pos * c.Rate);
                    int len = (int)(syl * c.Rate);
                    f1.Set(BiquadKind.BandPass, c.Range(300f, 800f), 4f, c.Rate);
                    f2.Set(BiquadKind.BandPass, c.Range(900f, 2400f), 5f, c.Rate);
                    f1.Snap();
                    f2.Snap();
                    float sp = pitch * c.Range(0.9f, 1.15f);
                    for (int i = 0; i < len && at + i < n; i++)
                    {
                        ph += sp / c.Rate;
                        if (ph >= 1.0) ph -= 1.0;
                        float src = Dsp.Saw((float)ph, sp / c.Rate) * 0.6f + c.Rng.White() * 0.4f;
                        float y = f1.Process(src) + 0.6f * f2.Process(src);
                        o[at + i] += y * Dsp.Hann((float)i / len) * gain;
                    }
                    pos += syl + c.Range(0.02f, 1f / rate);
                    if (c.Rng.Next01() < 0.15f) pos += c.Range(0.3f, 1.2f); // pauses
                }
            }
            // Low-pass the babble so it sits back and never reads as speech.
            var lp = default(Biquad);
            lp.Set(BiquadKind.LowPass, 2200f, 0.7f, c.Rate);
            for (int i = 0; i < n; i++) o[i] = lp.Process(o[i]) / (float)Math.Sqrt(talkers);

            if (footsteps > 0f)
            {
                // Granular footstep stream at about 1.9 steps/s per walker, capped at 40/s.
                float[] step = RenderFootstep(FootstepSurface.Brick, ref c);
                var grains = new GrainPlayer(16) { Source = step };
                float steps = Math.Min(40f, talkers * 0.8f * footsteps);
                float p = steps / c.Rate;
                float peak = 0f;
                for (int i = 0; i < n; i++) peak = Math.Max(peak, Math.Abs(o[i]));
                float g = Math.Max(0.05f, peak) * 0.35f;
                for (int i = 0; i < n; i++)
                {
                    if (c.Rng.Next01() < p) grains.Trigger(0, Math.Min(step.Length - 2, (int)(0.25f * c.Rate)), c.Range(0.9f, 1.1f), c.Range(0.3f, 1f));
                    o[i] += grains.Process() * g;
                }
            }
        }

        private static void TrafficHum(float[] o, ref BankContext c, bool heavy)
        {
            // Distant traffic: brown-noise rumble, a mid-band tyre hiss and a few detuned firing drones that drift.
            var lp = default(Biquad);
            lp.Set(BiquadKind.LowPass, heavy ? 350f : 250f, 0.7f, c.Rate);
            var hiss = default(Biquad);
            hiss.Set(BiquadKind.BandPass, heavy ? 900f : 700f, 0.6f, c.Rate);
            int drones = heavy ? 6 : 4;
            var f = new float[drones];
            var ph = new double[drones];
            var drift = new float[drones];
            for (int k = 0; k < drones; k++)
            {
                f[k] = c.Range(25f, 90f);
                drift[k] = c.Range(0.05f, 0.2f);
            }
            float invSr = 1f / c.Rate;
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / c.Rate;
                float y = lp.Process(c.Rng.Brown()) * 1.2f + hiss.Process(c.Rng.Pink()) * (heavy ? 0.9f : 0.5f);
                for (int k = 0; k < drones; k++)
                {
                    float fk = f[k] * (1f + 0.15f * Dsp.Sin(drift[k] * t + k * 0.37));
                    ph[k] += fk * invSr;
                    if (ph[k] >= 1.0) ph[k] -= 1.0;
                    float p = (float)ph[k];
                    float x2 = p * 2f; x2 -= (int)x2;
                    float x3 = p * 3f; x3 -= (int)x3;
                    float x4 = p * 4f; x4 -= (int)x4;
                    // Harmonics 2–4 carry the drone on phone speakers (missing fundamental).
                    y += (0.3f * Dsp.Sin01(p) + 0.4f * Dsp.Sin01(x2) + 0.3f * Dsp.Sin01(x3) + 0.2f * Dsp.Sin01(x4)) * 0.08f *
                         (0.6f + 0.4f * Dsp.Sin(drift[k] * 2.0 * t + k));
                }
                o[i] = y;
            }
        }

        private static void CityHum(float[] o, ref BankContext c)
        {
            // Valley-scale bed: brown noise band-limited to 40–400 Hz.
            var lp = default(Biquad);
            lp.Set(BiquadKind.LowPass, 400f, 0.7f, c.Rate);
            var hp = default(Biquad);
            hp.Set(BiquadKind.HighPass, 40f, 0.7f, c.Rate);
            var mid = default(Biquad);
            mid.Set(BiquadKind.BandPass, 250f, 0.8f, c.Rate);
            for (int i = 0; i < o.Length; i++)
            {
                float b = c.Rng.Brown();
                o[i] = hp.Process(lp.Process(b)) + mid.Process(c.Rng.Pink()) * 0.3f;
            }
        }

        private static void Courtyard(float[] o, ref BankContext c)
        {
            // Soft murmur: a light walla, darker, plus distant pigeon coos.
            Walla(o, ref c, 8, 0.3f);
            var lp = default(Biquad);
            lp.Set(BiquadKind.LowPass, 1500f, 0.7f, c.Rate);
            for (int i = 0; i < o.Length; i++) o[i] = lp.Process(o[i]) * 0.8f;
            float peak = 0f;
            for (int i = 0; i < o.Length; i++) peak = Math.Max(peak, Math.Abs(o[i]));
            float t = c.Range(0.5f, 1.5f);
            float len = (float)o.Length / c.Rate;
            while (t < len - 1.3f)
            {
                var coo = PigeonCoo(ref c);
                int at = (int)(t * c.Rate);
                for (int i = 0; i < coo.Length && at + i < o.Length; i++) o[at + i] += coo[i] * peak * 0.35f;
                t += c.Range(1.5f, 3.5f);
            }
        }

        private static void BirdsBed(float[] o, ref BankContext c)
        {
            // Light wind under scattered sparrow, myna and bulbul phrases at low level.
            Wind(o, ref c, false);
            for (int i = 0; i < o.Length; i++) o[i] *= 0.15f;
            float len = (float)o.Length / c.Rate;
            float t = c.Range(0.1f, 0.6f);
            while (t < len - 0.8f)
            {
                int pick = c.Rng.Index(3);
                float[] call = pick == 0 ? Sparrow(ref c) : pick == 1 ? Myna(ref c) : Bulbul(ref c);
                float g = c.Range(0.15f, 0.4f);
                int at = (int)(t * c.Rate);
                for (int i = 0; i < call.Length && at + i < o.Length; i++) o[at + i] += call[i] * g;
                t += c.Range(0.4f, 1.3f);
            }
        }

        private static void InsectsBed(float[] o, ref BankContext c)
        {
            // 5–12 crickets at 4.3–4.8 kHz, each chirping at 1–3 Hz with 3–5 pulses of 15 ms.
            int crickets = 5 + c.Rng.Index(8);
            for (int k = 0; k < crickets; k++)
            {
                float f = c.Range(4300f, 4800f);
                float rate = c.Range(1f, 3f);
                float g = c.Range(0.2f, 0.6f);
                int pulses = 3 + c.Rng.Index(3);
                float t = c.Range(0f, 1f / rate);
                float len = (float)o.Length / c.Rate;
                while (t < len - 0.2f)
                {
                    for (int p = 0; p < pulses; p++) BankKit.Tone(o, t + p * 0.025f, ref c, f, f, 0.015f, 0.002f, 0.005f, g, Wave.Sine);
                    t += 1f / rate * c.Range(0.9f, 1.1f);
                }
            }
        }

        private static void River(float[] o, ref BankContext c)
        {
            // Pink + white noise through a broad band 500 Hz–3 kHz with bubble chirps.
            var bp = default(Biquad);
            bp.Set(BiquadKind.BandPass, 1200f, 0.5f, c.Rate);
            var lp = default(Biquad);
            lp.Set(BiquadKind.LowPass, 500f, 0.7f, c.Rate);
            for (int i = 0; i < o.Length; i++) o[i] = bp.Process(c.Rng.Pink()) * 1.2f + lp.Process(c.Rng.Brown()) * 0.5f;
            float len = (float)o.Length / c.Rate;
            int bubbles = (int)(len * 12f);
            for (int b = 0; b < bubbles; b++)
            {
                float f = c.Range(600f, 2000f);
                BankKit.Tone(o, c.Range(0f, len - 0.05f), ref c, f, f * c.Range(1.1f, 1.2f), c.Range(0.02f, 0.04f), 0.002f, 0.01f, 0.15f, Wave.Sine);
            }
        }
    }
}
