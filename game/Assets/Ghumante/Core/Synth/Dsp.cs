using System;

namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Shared DSP helpers for the engine-free synth core (W2_DESIGN 7.1): a 2,048-entry sine table with linear
    /// interpolation, PolyBLEP correction, a rational tanh soft clip, decibel conversion and sample sanitising.
    /// Everything is allocation-free and deterministic (the table is built once from <see cref="Math.Sin"/>).
    /// </summary>
    public static class Dsp
    {
        /// <summary>Entries in the sine table (one full turn).</summary>
        public const int SineSize = 2048;

        public const float TwoPi = 6.28318530717958647692f;

        /// <summary>Speed of sound used by Doppler, slapback and reflection delays (m/s).</summary>
        public const float SpeedOfSound = 343f;

        // One guard entry so the interpolation never wraps.
        private static readonly float[] Sine = BuildSine();

        private static float[] BuildSine()
        {
            var t = new float[SineSize + 1];
            for (int i = 0; i <= SineSize; i++) t[i] = (float)Math.Sin(2.0 * Math.PI * i / SineSize);
            return t;
        }

        /// <summary>sin(2π·<paramref name="phase"/>) from the table; any finite phase (turns) is accepted.</summary>
        public static float Sin(double phase)
        {
            double p = phase - Math.Floor(phase);
            double x = p * SineSize;
            int i = (int)x;
            if (i >= SineSize) i = SineSize - 1;
            float f = (float)(x - i);
            return Sine[i] + (Sine[i + 1] - Sine[i]) * f;
        }

        /// <summary>sin(2π·<paramref name="phase"/>) for a phase already in [0, 1) (the fast path).</summary>
        public static float Sin01(float phase)
        {
            float x = phase * SineSize;
            int i = (int)x;
            if (i < 0) i = 0;
            else if (i >= SineSize) i = SineSize - 1;
            float f = x - i;
            return Sine[i] + (Sine[i + 1] - Sine[i]) * f;
        }

        /// <summary>cos(2π·<paramref name="phase"/>), phase in turns.</summary>
        public static float Cos(double phase)
        {
            return Sin(phase + 0.25);
        }

        /// <summary>Hann window value at <paramref name="t"/> in [0, 1] (0 at both ends, 1 in the middle).</summary>
        public static float Hann(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            return 0.5f - 0.5f * Sin01(t + 0.25f >= 1f ? t - 0.75f : t + 0.25f);
        }

        /// <summary>PolyBLEP residual for a discontinuity at phase 0 (t = phase in [0,1), dt = increment).</summary>
        public static float PolyBlep(float t, float dt)
        {
            if (dt <= 0f) return 0f;
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }
            if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }
            return 0f;
        }

        /// <summary>Band-limited sawtooth (−1..1) at phase <paramref name="t"/> with increment <paramref name="dt"/>.</summary>
        public static float Saw(float t, float dt)
        {
            return 2f * t - 1f - PolyBlep(t, dt);
        }

        /// <summary>Band-limited square (−1..1) with duty 0.5.</summary>
        public static float Square(float t, float dt)
        {
            float v = t < 0.5f ? 1f : -1f;
            v += PolyBlep(t, dt);
            float t2 = t + 0.5f;
            if (t2 >= 1f) t2 -= 1f;
            v -= PolyBlep(t2, dt);
            return v;
        }

        /// <summary>Soft clip close to tanh (rational Padé form), exact ±1 beyond |x| = 3.</summary>
        public static float SoftClip(float x)
        {
            if (x >= 3f) return 1f;
            if (x <= -3f) return -1f;
            float x2 = x * x;
            float y = x * (27f + x2) / (27f + 9f * x2);
            return y > 1f ? 1f : y < -1f ? -1f : y;
        }

        public static float DbToGain(float db)
        {
            return (float)Math.Pow(10.0, db / 20.0);
        }

        /// <summary>Gain in dB; −200 for silence.</summary>
        public static float GainToDb(float gain)
        {
            if (!(gain > 1e-10f)) return -200f;
            return (float)(20.0 * Math.Log10(gain));
        }

        /// <summary>One-pole smoothing coefficient for a time constant (s) at a sample rate (per-sample step).</summary>
        public static float SmoothCoef(float timeS, int sampleRate)
        {
            if (!(timeS > 0f) || sampleRate <= 0) return 1f;
            return 1f - (float)Math.Exp(-1.0 / (timeS * sampleRate));
        }

        /// <summary>Per-sample decay multiplier that falls by 60 dB in <paramref name="t60S"/>.</summary>
        public static float T60Decay(float t60S, int sampleRate)
        {
            if (!(t60S > 0f) || sampleRate <= 0) return 0f;
            return (float)Math.Exp(-6.907755278982137 / (t60S * sampleRate));
        }

        /// <summary>Replaces NaN and infinities by 0.</summary>
        public static float Sanitize(float x)
        {
            return float.IsNaN(x) || float.IsInfinity(x) ? 0f : x;
        }

        /// <summary>Sanitised and clamped to [lo, hi].</summary>
        public static float Clamp(float x, float lo, float hi)
        {
            if (float.IsNaN(x)) return lo;
            return x < lo ? lo : x > hi ? hi : x;
        }

        public static float Clamp01(float x)
        {
            return Clamp(x, 0f, 1f);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>Clamps every sample to [−1, 1] and zeroes non-finite ones; returns the peak magnitude.</summary>
        public static float Finish(float[] buffer, int offset, int count)
        {
            float peak = 0f;
            int end = offset + count;
            for (int i = offset; i < end; i++)
            {
                float v = buffer[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
                else if (v > 1f) v = 1f;
                else if (v < -1f) v = -1f;
                buffer[i] = v;
                float a = v < 0f ? -v : v;
                if (a > peak) peak = a;
            }
            return peak;
        }

        /// <summary>Scales <c>buffer[offset..offset+count)</c> so its peak is <paramref name="peak"/> (no-op on silence).</summary>
        public static void Normalize(float[] buffer, int offset, int count, float peak)
        {
            float max = 0f;
            int end = offset + count;
            for (int i = offset; i < end; i++)
            {
                float a = Math.Abs(buffer[i]);
                if (a > max) max = a;
            }
            if (!(max > 1e-9f)) return;
            float g = peak / max;
            for (int i = offset; i < end; i++) buffer[i] *= g;
        }

        /// <summary>Deterministic 32-bit seed mix of a base seed and two purpose values (FNV-1a over 12 bytes).</summary>
        public static uint Mix(uint seed, uint a, uint b = 0)
        {
            uint h = 0x811C9DC5;
            h = Step(h, seed);
            h = Step(h, a);
            h = Step(h, b);
            return h == 0 ? 0x9E3779B9u : h;
        }

        private static uint Step(uint h, uint v)
        {
            unchecked
            {
                h = (h ^ (v & 0xFF)) * 0x01000193;
                h = (h ^ ((v >> 8) & 0xFF)) * 0x01000193;
                h = (h ^ ((v >> 16) & 0xFF)) * 0x01000193;
                h = (h ^ (v >> 24)) * 0x01000193;
            }
            return h;
        }
    }
}
