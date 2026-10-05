using System;

namespace Ghumante.Core.Motion
{
    /// <summary>
    /// How a <see cref="Spring"/> moves: its natural frequency and its damping ratio (1 = critically damped,
    /// no overshoot; below 1 = bouncy; above 1 = sluggish).
    /// </summary>
    public readonly struct SpringParams
    {
        /// <summary>Natural frequency in hertz (oscillations per second when undamped). Must be &gt; 0.</summary>
        public readonly float FrequencyHz;

        /// <summary>Damping ratio, &gt;= 0.</summary>
        public readonly float DampingRatio;

        public SpringParams(float frequencyHz, float dampingRatio)
        {
            if (!(frequencyHz > 0f) || float.IsInfinity(frequencyHz))
                throw new ArgumentOutOfRangeException(nameof(frequencyHz), "frequency must be positive and finite");
            if (!(dampingRatio >= 0f) || float.IsInfinity(dampingRatio))
                throw new ArgumentOutOfRangeException(nameof(dampingRatio), "damping ratio must be >= 0 and finite");
            FrequencyHz = frequencyHz;
            DampingRatio = dampingRatio;
        }

        /// <summary>Button squash: quick, with one visible overshoot on release.</summary>
        public static SpringParams Press
        {
            get { return new SpringParams(7f, 0.42f); }
        }

        /// <summary>Rotational wobble: slow and ringing (a tapped sign on a nail).</summary>
        public static SpringParams Wobble
        {
            get { return new SpringParams(3.2f, 0.18f); }
        }

        /// <summary>A small hop that lands with a couple of bounces.</summary>
        public static SpringParams Hop
        {
            get { return new SpringParams(3.4f, 0.3f); }
        }

        /// <summary>Settles fast without overshoot (toggle knobs, sheets).</summary>
        public static SpringParams Snappy
        {
            get { return new SpringParams(5f, 0.75f); }
        }
    }

    /// <summary>
    /// A damped spring pulling <see cref="Value"/> towards <see cref="Target"/>, for squash-and-stretch,
    /// wobbles and hops. <see cref="Step"/> uses the closed-form solution of the damped harmonic oscillator, so
    /// the motion is the same whatever the frame rate (one 0.1 s step equals ten 0.01 s steps, up to
    /// rounding) and it never blows up on a long frame. A mutable struct: keep it in a field and call methods
    /// on the field. No allocations.
    /// </summary>
    public struct Spring
    {
        private const float TwoPi = 6.28318530718f;

        public float Value;
        public float Velocity;
        public float Target;
        public SpringParams Params;

        public Spring(SpringParams parameters, float value = 0f)
        {
            Params = parameters;
            Value = value;
            Target = value;
            Velocity = 0f;
        }

        /// <summary>True when value and velocity are both within <paramref name="epsilon"/> of rest.</summary>
        public bool IsSettled(float epsilon = 1e-3f)
        {
            return MathF.Abs(Value - Target) <= epsilon && MathF.Abs(Velocity) <= epsilon * 10f;
        }

        /// <summary>Jumps to <paramref name="value"/> and stops there.</summary>
        public void Snap(float value)
        {
            Value = value;
            Target = value;
            Velocity = 0f;
        }

        /// <summary>Adds an instantaneous change of velocity (units per second), e.g. a flick or a tap.</summary>
        public void Kick(float velocity)
        {
            Velocity += velocity;
        }

        /// <summary>Advances the spring by <paramref name="dt"/> seconds (non-positive dt is ignored).</summary>
        public void Step(float dt)
        {
            if (!(dt > 0f)) return;
            float freq = Params.FrequencyHz;
            if (!(freq > 0f))
            {
                // default(SpringParams): no spring at all; just drift.
                Value += Velocity * dt;
                return;
            }

            float omega = TwoPi * freq;
            float zeta = Params.DampingRatio;
            float x0 = Value - Target;
            float v0 = Velocity;
            float x, v;

            if (zeta < 0.9999f)
            {
                // Under-damped: x(t) = e^(-z w t) (x0 cos(wd t) + b sin(wd t)), b = (v0 + z w x0) / wd.
                float a = zeta * omega;
                float wd = omega * MathF.Sqrt(1f - zeta * zeta);
                float e = MathF.Exp(-a * dt);
                float c = MathF.Cos(wd * dt);
                float s = MathF.Sin(wd * dt);
                float b = (v0 + a * x0) / wd;
                x = e * (x0 * c + b * s);
                v = e * (v0 * c - (a * b + x0 * wd) * s);
            }
            else if (zeta <= 1.0001f)
            {
                // Critically damped: x(t) = e^(-w t) (x0 + (v0 + w x0) t).
                float e = MathF.Exp(-omega * dt);
                float k = v0 + omega * x0;
                x = e * (x0 + k * dt);
                v = e * (v0 - omega * k * dt);
            }
            else
            {
                // Over-damped: two real roots r1 > r2 (both negative).
                float root = MathF.Sqrt(zeta * zeta - 1f);
                float r1 = -omega * (zeta - root);
                float r2 = -omega * (zeta + root);
                float c2 = (r1 * x0 - v0) / (r1 - r2);
                float c1 = x0 - c2;
                float e1 = MathF.Exp(r1 * dt);
                float e2 = MathF.Exp(r2 * dt);
                x = c1 * e1 + c2 * e2;
                v = c1 * r1 * e1 + c2 * r2 * e2;
            }

            Value = Target + x;
            Velocity = v;
        }
    }
}
