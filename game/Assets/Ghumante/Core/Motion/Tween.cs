using System;

namespace Ghumante.Core.Motion
{
    /// <summary>
    /// One animated value as data: from, to, delay, duration and ease. Sampling is a pure function of the
    /// elapsed time since the tween was started, so a timeline is deterministic and costs nothing to rewind.
    /// </summary>
    public readonly struct Tween
    {
        public readonly float From;
        public readonly float To;

        /// <summary>Seconds before the value starts moving (it holds <see cref="From"/> until then).</summary>
        public readonly float Delay;

        /// <summary>Seconds from leaving <see cref="From"/> to arriving at <see cref="To"/>; 0 jumps.</summary>
        public readonly float Duration;

        public readonly Ease Ease;

        public Tween(float from, float to, float duration, Ease ease = Ease.OutCubic, float delay = 0f)
        {
            if (!(duration >= 0f)) throw new ArgumentOutOfRangeException(nameof(duration), "duration must be >= 0");
            if (!(delay >= 0f)) throw new ArgumentOutOfRangeException(nameof(delay), "delay must be >= 0");
            From = from;
            To = to;
            Duration = duration;
            Ease = ease;
            Delay = delay;
        }

        /// <summary>Seconds from the start until the tween is complete.</summary>
        public float EndTime
        {
            get { return Delay + Duration; }
        }

        /// <summary>Linear progress in [0, 1] at <paramref name="elapsed"/> seconds after the start.</summary>
        public float Progress(float elapsed)
        {
            float t = elapsed - Delay;
            if (t <= 0f) return Duration <= 0f && t >= 0f ? 1f : 0f;
            if (Duration <= 0f || t >= Duration) return 1f;
            return t / Duration;
        }

        /// <summary>The value at <paramref name="elapsed"/> seconds after the start. Overshooting eases go
        /// past <see cref="To"/> on the way; the value is exactly <see cref="To"/> once complete.</summary>
        public float Sample(float elapsed)
        {
            float p = Progress(elapsed);
            if (p >= 1f) return To;
            return Easing.Lerp(From, To, Easing.Evaluate(Ease, p));
        }

        public bool IsComplete(float elapsed)
        {
            return elapsed >= EndTime;
        }

        /// <summary>The same tween starting <paramref name="extraDelay"/> seconds later.</summary>
        public Tween Delayed(float extraDelay)
        {
            return new Tween(From, To, Duration, Ease, Delay + extraDelay);
        }
    }

    /// <summary>
    /// A staggered sequence: item <c>i</c> starts at <c>Start + i * Step</c> and runs for <see cref="Duration"/>
    /// with <see cref="Ease"/> (menu pills popping in one after another, counters dropping in).
    /// </summary>
    public readonly struct Stagger
    {
        public readonly float Start;
        public readonly float Step;
        public readonly float Duration;
        public readonly Ease Ease;

        public Stagger(float start, float step, float duration, Ease ease)
        {
            if (!(start >= 0f)) throw new ArgumentOutOfRangeException(nameof(start));
            if (!(step >= 0f)) throw new ArgumentOutOfRangeException(nameof(step));
            if (!(duration >= 0f)) throw new ArgumentOutOfRangeException(nameof(duration));
            Start = start;
            Step = step;
            Duration = duration;
            Ease = ease;
        }

        /// <summary>Start time of item <paramref name="index"/>.</summary>
        public float DelayOf(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            return Start + Step * index;
        }

        /// <summary>The tween of item <paramref name="index"/> between two values.</summary>
        public Tween At(int index, float from, float to)
        {
            return new Tween(from, to, Duration, Ease, DelayOf(index));
        }

        /// <summary>When the last of <paramref name="count"/> items finishes.</summary>
        public float EndTime(int count)
        {
            return count <= 0 ? Start : DelayOf(count - 1) + Duration;
        }
    }

    /// <summary>Periodic motion helpers for idle loops (breathing, swaying, sweeps). Pure functions of time.</summary>
    public static class Wave
    {
        private const float TwoPi = 6.28318530718f;

        /// <summary>A sine in [-1, 1] with the given period; <paramref name="phase"/> is in cycles (0..1).</summary>
        public static float Sine(float time, float periodSeconds, float phase = 0f)
        {
            if (!(periodSeconds > 0f)) return 0f;
            return MathF.Sin(TwoPi * (time / periodSeconds + phase));
        }

        /// <summary>
        /// For an effect that runs for <paramref name="activeSeconds"/> at the start of every
        /// <paramref name="periodSeconds"/> (a shine sweep every few seconds): the progress in [0, 1] while it
        /// runs, or -1 while it waits.
        /// </summary>
        public static float Pulse(float time, float periodSeconds, float activeSeconds)
        {
            if (!(periodSeconds > 0f) || !(activeSeconds > 0f) || time < 0f) return -1f;
            float local = time % periodSeconds;
            return local < activeSeconds ? local / activeSeconds : -1f;
        }

        /// <summary>
        /// A wiggle that dies away: <paramref name="cycles"/> oscillations in [-1, 1] over
        /// <paramref name="durationSeconds"/>, fading quadratically to exactly 0 at the end (a "not yet" shake).
        /// </summary>
        public static float DampedWiggle(float time, float durationSeconds, float cycles)
        {
            if (!(durationSeconds > 0f) || time <= 0f || time >= durationSeconds) return 0f;
            float t = time / durationSeconds;
            float fade = 1f - t;
            return MathF.Sin(TwoPi * cycles * t) * fade * fade;
        }
    }

    /// <summary>Squash-and-stretch: turns one "amount" into an x/y scale pair.</summary>
    public static class Squash
    {
        /// <summary>
        /// Positive <paramref name="amount"/> squashes (wider, shorter), negative stretches (narrower, taller).
        /// <paramref name="widthGain"/> is how much of the height change goes into width; 0.75 gives the
        /// 1.06 x 0.92 press squash at amount 0.08.
        /// </summary>
        public static void Scale(float amount, float widthGain, out float scaleX, out float scaleY)
        {
            scaleY = 1f - amount;
            scaleX = 1f + amount * widthGain;
            if (scaleY < 0.05f) scaleY = 0.05f;
            if (scaleX < 0.05f) scaleX = 0.05f;
        }
    }

    /// <summary>Counting animations (a counter rolling up to its new value).</summary>
    public static class CountUp
    {
        /// <summary>The integer shown at eased progress <paramref name="progress"/> (clamped to [0, 1]).</summary>
        public static long Value(long from, long to, float progress)
        {
            if (progress <= 0f) return from;
            if (progress >= 1f) return to;
            double v = from + (double)(to - from) * progress;
            return (long)Math.Round(v, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>
    /// Small deterministic random generator (xorshift32) for particles and idle variety, so effects are
    /// reproducible in tests and allocate nothing. Not for anything security- or fairness-related.
    /// </summary>
    public struct MotionRandom
    {
        private uint _state;

        public MotionRandom(uint seed)
        {
            _state = seed == 0u ? 0x9E3779B9u : seed;
        }

        public uint NextUInt()
        {
            if (_state == 0u) _state = 0x9E3779B9u;
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform in [min, max).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform integer in [0, count); 0 when count &lt;= 0.</summary>
        public int NextInt(int count)
        {
            if (count <= 0) return 0;
            return (int)(NextUInt() % (uint)count);
        }
    }
}
