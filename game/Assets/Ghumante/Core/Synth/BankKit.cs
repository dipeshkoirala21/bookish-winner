using System;

namespace Ghumante.Core.Synth
{
    /// <summary>Oscillator shapes for <see cref="BankKit.Tone"/>.</summary>
    internal enum Wave : byte
    {
        Sine = 0,
        Saw = 1,
        Square = 2,
        /// <summary>30% square + 70% saw (A §4.3 horns).</summary>
        HornMix = 3,
        Triangle = 4,
    }

    /// <summary>
    /// Building blocks for the baked recipes: filtered noise bursts, gliding tones with envelopes and vibrato,
    /// modal strikes (with doublets for beating), two-operator FM bells, source-filter animal calls and the
    /// seamless-loop crossfade. Every function adds into the target buffer and clips the write range.
    /// Load-time code: small allocations are allowed here (never on the audio thread).
    /// </summary>
    internal static class BankKit
    {
        public static int At(in BankContext c, float seconds)
        {
            return (int)(Math.Max(0f, seconds) * c.Rate);
        }

        /// <summary>Adds noise shaped by an attack and an exponential decay (time constant <paramref name="tauS"/>),
        /// through a biquad.</summary>
        public static void NoiseBurst(float[] o, float startS, ref BankContext c, float attackS, float tauS, float lenS,
                                      BiquadKind kind, float hz, float q, float gain, bool pink = false)
        {
            int at = At(c, startS);
            int n = c.Samples(lenS);
            var f = default(Biquad);
            f.Set(kind, hz, q, c.Rate);
            int atk = Math.Max(1, (int)(attackS * c.Rate));
            float dec = (float)Math.Exp(-1.0 / Math.Max(1e-4, tauS * c.Rate));
            float env = 1f;
            for (int i = 0; i < n; i++)
            {
                int k = at + i;
                if (k >= o.Length) break;
                float a = i < atk ? (float)i / atk : 1f;
                float x = pink ? c.Rng.Pink() * 3f : c.Rng.White();
                o[k] += f.Process(x) * a * env * gain;
                if (i >= atk) env *= dec;
            }
        }

        /// <summary>Adds a tone gliding exponentially from <paramref name="f0"/> to <paramref name="f1"/> with a
        /// linear attack, sustain and release, optional vibrato (Hz, depth as a frequency fraction) and extra
        /// harmonics (relative amplitudes of 2f, 3f, …; sine wave only).</summary>
        public static void Tone(float[] o, float startS, ref BankContext c, float f0, float f1, float lenS, float attackS, float releaseS,
                                float gain, Wave wave, float[] harmonics = null, float vibHz = 0f, float vibDepth = 0f, float decayTauS = 0f)
        {
            int at = At(c, startS);
            int n = c.Samples(lenS);
            int atk = Math.Max(1, (int)(attackS * c.Rate));
            int rel = Math.Max(1, (int)(releaseS * c.Rate));
            double phase = c.Rng.Next01();
            double vib = 0;
            float invSr = 1f / c.Rate;
            float ratio = f1 > 0f && f0 > 0f ? f1 / f0 : 1f;
            float dec = decayTauS > 0f ? (float)Math.Exp(-1.0 / (decayTauS * c.Rate)) : 1f;
            float decEnv = 1f;
            float nyq = 0.45f * c.Rate;
            for (int i = 0; i < n; i++)
            {
                int k = at + i;
                if (k >= o.Length) break;
                float u = n > 1 ? (float)i / (n - 1) : 0f;
                float f = f0 * (float)Math.Pow(ratio, u);
                if (vibHz > 0f)
                {
                    vib += vibHz * invSr;
                    f *= 1f + vibDepth * Dsp.Sin(vib);
                }
                float dt = f * invSr;
                phase += dt;
                if (phase >= 1.0) phase -= Math.Floor(phase);
                float p = (float)phase;
                float s;
                switch (wave)
                {
                    case Wave.Saw: s = Dsp.Saw(p, dt); break;
                    case Wave.Square: s = Dsp.Square(p, dt); break;
                    case Wave.HornMix: s = 0.3f * Dsp.Square(p, dt) + 0.7f * Dsp.Saw(p, dt); break;
                    case Wave.Triangle: s = 1f - 4f * Math.Abs(p - 0.5f); break;
                    default:
                        s = Dsp.Sin01(p);
                        if (harmonics != null)
                        {
                            for (int h = 0; h < harmonics.Length; h++)
                            {
                                if (f * (h + 2) >= nyq) break;
                                float x = p * (h + 2);
                                x -= (int)x;
                                s += harmonics[h] * Dsp.Sin01(x);
                            }
                        }
                        break;
                }
                float env = i < atk ? (float)i / atk : 1f;
                if (i > n - rel) env *= (float)(n - i) / rel;
                o[k] += s * env * decEnv * gain;
                decEnv *= dec;
            }
        }

        /// <summary>Adds a modal strike: a 3 ms noise burst into resonators at <paramref name="freqs"/> with
        /// per-mode T60 and amplitude; <paramref name="doublet"/> splits every mode into two at ±split for beating.</summary>
        public static void Modal(float[] o, float startS, ref BankContext c, float[] freqs, float[] t60s, float[] amps, float gain,
                                 float lenS, float doublet = 0f, float strikeS = 0.003f)
        {
            int at = At(c, startS);
            int n = c.Samples(lenS);
            var bank = new ModalBank(freqs.Length * 2);
            for (int i = 0; i < freqs.Length; i++)
            {
                if (doublet > 0f)
                {
                    bank.Add(freqs[i] * (1f - doublet), t60s[i], amps[i] * 0.5f, c.Rate);
                    bank.Add(freqs[i] * (1f + doublet), t60s[i], amps[i] * 0.5f, c.Rate);
                }
                else
                {
                    bank.Add(freqs[i], t60s[i], amps[i], c.Rate);
                }
            }
            int strike = Math.Max(1, (int)(strikeS * c.Rate));
            for (int i = 0; i < n; i++)
            {
                int k = at + i;
                if (k >= o.Length) break;
                float x = i < strike ? c.Rng.White() * (1f - (float)i / strike) : 0f;
                o[k] += bank.Process(x) * gain;
            }
        }

        /// <summary>Two-operator FM bell (A §4.3 alternative): carrier f0, modulator ratio × f0, modulation index
        /// decaying from <paramref name="index0"/> to <paramref name="index1"/>, amplitude falling 60 dB over T60.</summary>
        public static void FmBell(float[] o, float startS, ref BankContext c, float f0, float ratio, float index0, float index1,
                                  float t60S, float lenS, float gain)
        {
            int at = At(c, startS);
            int n = c.Samples(lenS);
            float invSr = 1f / c.Rate;
            double pc = 0, pm = 0;
            float amp = 1f;
            float dec = Dsp.T60Decay(t60S, c.Rate);
            float idxDec = (float)Math.Pow(Math.Max(1e-3f, index1 / Math.Max(1e-3f, index0)), 1.0 / Math.Max(1, t60S * c.Rate));
            float idx = index0;
            float fm = f0 * ratio;
            int atk = Math.Max(1, (int)(0.002f * c.Rate));
            for (int i = 0; i < n; i++)
            {
                int k = at + i;
                if (k >= o.Length) break;
                pm += fm * invSr;
                if (pm >= 1.0) pm -= Math.Floor(pm);
                float mod = Dsp.Sin01((float)pm) * idx;
                pc += f0 * invSr;
                if (pc >= 1.0) pc -= Math.Floor(pc);
                float a = i < atk ? (float)i / atk : 1f;
                o[k] += Dsp.Sin(pc + mod / Dsp.TwoPi * 1f) * amp * a * gain;
                amp *= dec;
                idx *= idxDec;
            }
        }

        /// <summary>
        /// Source-filter call (crow, dog, cow, rooster, goat): a glottal pulse train whose f0 follows a
        /// piecewise-linear contour, through two or three formant band-passes, plus aspiration noise.
        /// </summary>
        public static void Call(float[] o, float startS, ref BankContext c, float lenS, float[] f0Contour, float[] formants, float[] formantQ,
                                float aspiration, float gain, float attackS = 0.01f, float releaseS = 0.04f, float formantGlide = 1f)
        {
            int at = At(c, startS);
            int n = c.Samples(lenS);
            int nf = formants.Length;
            var bp = new Biquad[nf];
            for (int j = 0; j < nf; j++) bp[j].Set(BiquadKind.BandPass, formants[j], formantQ[j], c.Rate);
            var asp = default(Biquad);
            asp.Set(BiquadKind.HighPass, 1500f, 0.7f, c.Rate);
            double phase = 0;
            float invSr = 1f / c.Rate;
            int atk = Math.Max(1, (int)(attackS * c.Rate));
            int rel = Math.Max(1, (int)(releaseS * c.Rate));
            int segs = f0Contour.Length - 1;
            for (int i = 0; i < n; i++)
            {
                int k = at + i;
                if (k >= o.Length) break;
                float u = n > 1 ? (float)i / (n - 1) : 0f;
                float f0;
                if (segs <= 0) f0 = f0Contour[0];
                else
                {
                    float x = u * segs;
                    int s = Math.Min(segs - 1, (int)x);
                    f0 = Dsp.Lerp(f0Contour[s], f0Contour[s + 1], x - s);
                }
                if (formantGlide != 1f && (i & 31) == 0)
                {
                    float g = (float)Math.Pow(formantGlide, u);
                    for (int j = 0; j < nf; j++) bp[j].Set(BiquadKind.BandPass, formants[j] * g, formantQ[j], c.Rate, 0f, 0.003f);
                }
                float dt = f0 * invSr;
                phase += dt;
                if (phase >= 1.0) phase -= Math.Floor(phase);
                float src = Dsp.Saw((float)phase, dt) + c.Rng.White() * 0.15f;
                float y = 0f;
                for (int j = 0; j < nf; j++) y += bp[j].Process(src) * (j == 0 ? 1f : 0.7f);
                y += asp.Process(c.Rng.White()) * aspiration;
                float env = i < atk ? (float)i / atk : 1f;
                if (i > n - rel) env *= (float)(n - i) / rel;
                o[k] += y * env * gain;
            }
        }

        /// <summary>Linear fade to silence over the last <paramref name="fadeS"/> seconds.</summary>
        public static void FadeOut(float[] o, in BankContext c, float fadeS)
        {
            int n = Math.Min(o.Length, Math.Max(1, (int)(fadeS * c.Rate)));
            for (int i = 0; i < n; i++) o[o.Length - 1 - i] *= (float)i / n;
        }

        /// <summary>
        /// Makes a seamless loop of <paramref name="loopLen"/> samples from a render of loopLen + overlap samples:
        /// the tail beyond loopLen is crossfaded (equal power) into the head.
        /// </summary>
        public static float[] Loopify(float[] full, int loopLen)
        {
            int overlap = full.Length - loopLen;
            if (overlap <= 0) return full;
            var o = new float[loopLen];
            Array.Copy(full, o, loopLen);
            for (int i = 0; i < overlap && i < loopLen; i++)
            {
                float t = (float)i / overlap;
                float fin = Dsp.Sin01(t * 0.25f);            // sin(πt/2)
                float fout = Dsp.Sin01(0.25f + t * 0.25f);   // cos(πt/2)
                o[i] = full[i] * fin + full[loopLen + i] * fout;
            }
            return o;
        }
    }
}
