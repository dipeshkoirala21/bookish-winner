using System;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// A second-order follower with personality (W2_DESIGN 6.1, P §5.3; t3ssel8r 2022): <c>f</c> the natural frequency
    /// (Hz), <c>ζ</c> the damping (below 1 overshoots), <c>r</c> the initial response (above 1 overshoots on the way in,
    /// below 0 anticipates). Constants k1 = ζ/(πf), k2 = 1/(2πf)², k3 = r·ζ/(2πf); semi-implicit Euler with k2 raised
    /// for stability at large steps, so it never blows up at dt = 1/20 s. A mutable struct: keep it in a field.
    /// Presets for the character's secondary motion are the static fields (topi 3.5/0.45/0, braid 2.4/0.35/0, backpack
    /// 2.0/0.5/0, hem 2.2/0.5/0, head look-at 2.5/0.9/−0.2, body lean 2.0/0.8/0).
    /// </summary>
    public struct SecondOrder
    {
        private float _k1, _k2, _k3;
        private float _xp;
        private float _y, _yd;

        public SecondOrder(float f, float zeta, float r, float x0)
        {
            if (!(f > 0f) || float.IsInfinity(f)) throw new ArgumentOutOfRangeException(nameof(f), "frequency must be positive");
            if (!(zeta >= 0f)) throw new ArgumentOutOfRangeException(nameof(zeta), "damping must be >= 0");
            _k1 = zeta / (MathF.PI * f);
            float w = 2f * MathF.PI * f;
            _k2 = 1f / (w * w);
            _k3 = r * zeta / w;
            _xp = x0;
            _y = x0;
            _yd = 0f;
        }

        /// <summary>The current output.</summary>
        public float Value
        {
            get { return _y; }
        }

        /// <summary>The output's rate of change.</summary>
        public float Velocity
        {
            get { return _yd; }
        }

        /// <summary>Jumps to <paramref name="x"/> at rest.</summary>
        public void Reset(float x)
        {
            _xp = x;
            _y = x;
            _yd = 0f;
        }

        /// <summary>Adds an instant velocity (a kick: landings, bumps).</summary>
        public void Kick(float velocity)
        {
            if (!float.IsNaN(velocity) && !float.IsInfinity(velocity)) _yd += velocity;
        }

        /// <summary>Follows input <paramref name="x"/> for <paramref name="dt"/> seconds; the input's velocity is estimated
        /// from the last call.</summary>
        public float Update(float dt, float x)
        {
            if (!(dt > 0f) || float.IsInfinity(dt) || float.IsNaN(x) || float.IsInfinity(x)) return _y;
            if (dt > 0.25f) dt = 0.25f;
            float xd = (x - _xp) / dt;
            _xp = x;
            // Stability: k2 at least 1.1 × (T²/4 + T·k1/2) (the semi-implicit Euler bound).
            float k2 = Math.Max(_k2, 1.1f * Math.Max(dt * dt / 4f + dt * _k1 / 2f, dt * _k1));
            _y += dt * _yd;
            _yd += dt * (x + _k3 * xd - _y - _k1 * _yd) / k2;
            if (float.IsNaN(_y) || float.IsInfinity(_y)) Reset(x);
            return _y;
        }

        public static SecondOrder Topi(float x0 = 0f)
        {
            return new SecondOrder(3.5f, 0.45f, 0f, x0);
        }

        public static SecondOrder Helmet(float x0 = 0f)
        {
            return new SecondOrder(6.0f, 0.7f, 0f, x0);
        }

        public static SecondOrder Braid(float x0 = 0f)
        {
            return new SecondOrder(2.4f, 0.35f, 0f, x0);
        }

        public static SecondOrder Backpack(float x0 = 0f)
        {
            return new SecondOrder(2.0f, 0.5f, 0f, x0);
        }

        public static SecondOrder Hem(float x0 = 0f)
        {
            return new SecondOrder(2.2f, 0.5f, 0f, x0);
        }

        public static SecondOrder LookAt(float x0 = 0f)
        {
            return new SecondOrder(2.5f, 0.9f, -0.2f, x0);
        }

        public static SecondOrder BodyLean(float x0 = 0f)
        {
            return new SecondOrder(2.0f, 0.8f, 0f, x0);
        }
    }

    /// <summary>
    /// Analytic two-bone IK (law of cosines) with a pole vector (P §5.6): knees bend forward, elbows out and back.
    /// Engine-free and allocation-free.
    /// </summary>
    public static class TwoBoneIk
    {
        /// <summary>
        /// Places the middle joint for a chain rooted at <paramref name="root"/> with segment lengths
        /// <paramref name="upper"/> and <paramref name="lower"/> reaching for <paramref name="target"/>, bending towards
        /// <paramref name="pole"/> (a direction). The reach is clamped to what the chain can do; the remaining error is
        /// returned (0 when the target is reachable).
        /// </summary>
        public static float Solve(V3 root, float upper, float lower, V3 target, V3 pole, out V3 joint, out V3 end)
        {
            V3 to = target - root;
            float d = to.Length;
            float minD = Math.Abs(upper - lower) + 1e-4f, maxD = upper + lower - 1e-4f;
            V3 dir = d > 1e-6f ? to * (1f / d) : V3.Down;
            float dc = d < minD ? minD : d > maxD ? maxD : d;
            float cosA = (upper * upper + dc * dc - lower * lower) / (2f * upper * dc);
            cosA = cosA < -1f ? -1f : cosA > 1f ? 1f : cosA;
            float sinA = MathF.Sqrt(Math.Max(0f, 1f - cosA * cosA));
            V3 bend = pole - dir * V3.Dot(pole, dir);
            if (bend.LengthSq < 1e-8f)
            {
                bend = V3.Cross(dir, V3.Right);
                if (bend.LengthSq < 1e-8f) bend = V3.Cross(dir, V3.Forward);
            }
            bend = bend.Normalized;
            joint = root + dir * (upper * cosA) + bend * (upper * sinA);
            end = root + dir * dc;
            return V3.Distance(end, target);
        }

        /// <summary>The world rotation that turns a bone pointing along <paramref name="bindDir"/> (in its parent's world
        /// rotation <paramref name="parentWorld"/>) to point along <paramref name="worldDir"/>, as a local rotation relative
        /// to the parent.</summary>
        public static Quat Aim(Quat parentWorld, V3 bindDir, V3 worldDir)
        {
            V3 current = parentWorld * bindDir;
            Quat delta = Quat.FromTo(current, worldDir);
            Quat world = delta * parentWorld;
            return (parentWorld.Inverse * world).Normalized;
        }
    }
}
