using System;

namespace Ghumante.Core.Motion
{
    /// <summary>
    /// Easing curves by name, so a tween can be described as plain data (ARCHITECTURE.md 8: bouncy UI tweens).
    /// Values are stable; append only.
    /// </summary>
    public enum Ease : byte
    {
        /// <summary>Constant speed.</summary>
        Linear = 0,

        /// <summary>Fast start, gentle stop. The default for slides and fades.</summary>
        OutCubic = 1,

        /// <summary>Gentle start, fast end. For things leaving the screen.</summary>
        InCubic = 2,

        /// <summary>Gentle at both ends. For loops and sweeps.</summary>
        InOutSine = 3,

        /// <summary>Overshoots by about 10 % and settles (ASSET_MANIFEST.md 12: "overshoot 1.1"). Pops.</summary>
        OutBack = 4,

        /// <summary>Springy overshoot with a few decaying wiggles. Swings.</summary>
        OutElastic = 5,

        /// <summary>Falls and bounces on the end value, never past it. Drops.</summary>
        OutBounce = 6,

        /// <summary>Pulls back a little before leaving. Exits.</summary>
        InBack = 7,
    }

    /// <summary>
    /// Easing functions on normalised time. Every function maps 0 to exactly 0 and 1 to exactly 1; inputs
    /// outside [0, 1] are clamped and NaN counts as 0. Pure, allocation-free and deterministic
    /// (single-precision MathF only).
    /// </summary>
    public static class Easing
    {
        /// <summary>
        /// Back-ease constant: <see cref="OutBack"/> peaks at about 1.0998 (t = 0.6), the 10 % overshoot the
        /// asset manifest asks for.
        /// </summary>
        public const float DefaultOvershoot = 1.70158f;

        private const float TwoPi = 6.28318530718f;

        /// <summary>Evaluates <paramref name="ease"/> at <paramref name="t"/> (clamped to [0, 1]).</summary>
        public static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.OutCubic: return OutCubic(t);
                case Ease.InCubic: return InCubic(t);
                case Ease.InOutSine: return InOutSine(t);
                case Ease.OutBack: return OutBack(t);
                case Ease.OutElastic: return OutElastic(t);
                case Ease.OutBounce: return OutBounce(t);
                case Ease.InBack: return InBack(t);
                default: return Linear(t);
            }
        }

        public static float Linear(float t)
        {
            return Clamp01(t);
        }

        public static float OutCubic(float t)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float InCubic(float t)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            return t * t * t;
        }

        public static float InOutSine(float t)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            return 0.5f - 0.5f * MathF.Cos(MathF.PI * t);
        }

        /// <summary>Back ease out: overshoots the end value and settles.</summary>
        /// <param name="t">Normalised time.</param>
        /// <param name="overshoot">Larger is bouncier; <see cref="DefaultOvershoot"/> gives a 10 % overshoot.</param>
        public static float OutBack(float t, float overshoot = DefaultOvershoot)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            float u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }

        /// <summary>Back ease in: dips below the start value before leaving.</summary>
        public static float InBack(float t, float overshoot = DefaultOvershoot)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            return (overshoot + 1f) * t * t * t - overshoot * t * t;
        }

        /// <summary>Elastic ease out: overshoots by about a third and rings down within the duration.</summary>
        public static float OutElastic(float t)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * (TwoPi / 3f)) + 1f;
        }

        /// <summary>Bounce ease out: reaches the end value at t = 0.36 and bounces on it, never past it.</summary>
        public static float OutBounce(float t)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            const float n = 7.5625f;
            const float d = 2.75f;
            float r;
            if (t < 1f / d)
            {
                r = n * t * t;
            }
            else if (t < 2f / d)
            {
                t -= 1.5f / d;
                r = n * t * t + 0.75f;
            }
            else if (t < 2.5f / d)
            {
                t -= 2.25f / d;
                r = n * t * t + 0.9375f;
            }
            else
            {
                t -= 2.625f / d;
                r = n * t * t + 0.984375f;
            }
            return r > 1f ? 1f : r;
        }

        public static float Clamp01(float t)
        {
            return !(t > 0f) ? 0f : (t >= 1f ? 1f : t);
        }

        /// <summary>Unclamped linear interpolation (eased values above 1 overshoot past <paramref name="b"/>).</summary>
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
