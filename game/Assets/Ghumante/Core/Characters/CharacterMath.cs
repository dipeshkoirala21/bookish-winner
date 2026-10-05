using System;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// A small float 3-vector for the engine-free character code (Core has no engine types). Model axes follow Unity:
    /// +X right, +Y up, +Z forward.
    /// </summary>
    public struct V3
    {
        public float X, Y, Z;

        public V3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly V3 Zero = new V3(0f, 0f, 0f);
        public static readonly V3 Up = new V3(0f, 1f, 0f);
        public static readonly V3 Down = new V3(0f, -1f, 0f);
        public static readonly V3 Forward = new V3(0f, 0f, 1f);
        public static readonly V3 Right = new V3(1f, 0f, 0f);

        public static V3 operator +(V3 a, V3 b)
        {
            return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static V3 operator -(V3 a, V3 b)
        {
            return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static V3 operator -(V3 a)
        {
            return new V3(-a.X, -a.Y, -a.Z);
        }

        public static V3 operator *(V3 a, float s)
        {
            return new V3(a.X * s, a.Y * s, a.Z * s);
        }

        public static V3 operator *(float s, V3 a)
        {
            return new V3(a.X * s, a.Y * s, a.Z * s);
        }

        public float Length
        {
            get { return MathF.Sqrt(X * X + Y * Y + Z * Z); }
        }

        public float LengthSq
        {
            get { return X * X + Y * Y + Z * Z; }
        }

        public V3 Normalized
        {
            get
            {
                float l = Length;
                return l > 1e-8f ? new V3(X / l, Y / l, Z / l) : Zero;
            }
        }

        public static float Dot(V3 a, V3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static V3 Cross(V3 a, V3 b)
        {
            return new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        public static V3 Lerp(V3 a, V3 b, float t)
        {
            return new V3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        public static float Distance(V3 a, V3 b)
        {
            return (a - b).Length;
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.###") + ", " + Y.ToString("0.###") + ", " + Z.ToString("0.###") + ")";
        }
    }

    /// <summary>
    /// A unit quaternion (x, y, z, w) with Unity's conventions: <see cref="Euler"/> applies Z, then X, then Y (degrees),
    /// and <c>a * b</c> rotates by b first, then a. Engine-free; the Unity side copies the four floats into a
    /// <c>Quaternion</c>.
    /// </summary>
    public struct Quat
    {
        public float X, Y, Z, W;

        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static readonly Quat Identity = new Quat(0f, 0f, 0f, 1f);

        public const float Deg2Rad = 0.0174532925f;
        public const float Rad2Deg = 57.2957795f;

        /// <summary>Rotation of <paramref name="degrees"/> about the unit <paramref name="axis"/>.</summary>
        public static Quat AxisAngle(V3 axis, float degrees)
        {
            V3 a = axis.Normalized;
            float h = degrees * Deg2Rad * 0.5f;
            float s = MathF.Sin(h);
            return new Quat(a.X * s, a.Y * s, a.Z * s, MathF.Cos(h));
        }

        /// <summary>Unity's Euler order: rotate about Z, then X, then Y (degrees).</summary>
        public static Quat Euler(float xDeg, float yDeg, float zDeg)
        {
            Quat qx = AxisAngle(V3.Right, xDeg), qy = AxisAngle(V3.Up, yDeg), qz = AxisAngle(V3.Forward, zDeg);
            return qy * (qx * qz);
        }

        public static Quat operator *(Quat a, Quat b)
        {
            return new Quat(
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
                a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
        }

        /// <summary>Rotates <paramref name="v"/>.</summary>
        public static V3 operator *(Quat q, V3 v)
        {
            // v' = v + 2w(q × v) + 2 q × (q × v)
            var u = new V3(q.X, q.Y, q.Z);
            V3 t = 2f * V3.Cross(u, v);
            return v + q.W * t + V3.Cross(u, t);
        }

        public Quat Inverse
        {
            get { return new Quat(-X, -Y, -Z, W); }
        }

        public Quat Normalized
        {
            get
            {
                float l = MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);
                return l > 1e-8f ? new Quat(X / l, Y / l, Z / l, W / l) : Identity;
            }
        }

        /// <summary>The shortest rotation taking direction <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static Quat FromTo(V3 from, V3 to)
        {
            V3 a = from.Normalized, b = to.Normalized;
            float d = V3.Dot(a, b);
            if (d > 0.999999f) return Identity;
            if (d < -0.999999f)
            {
                V3 axis = V3.Cross(V3.Right, a);
                if (axis.LengthSq < 1e-6f) axis = V3.Cross(V3.Up, a);
                return AxisAngle(axis, 180f);
            }
            V3 c = V3.Cross(a, b);
            return new Quat(c.X, c.Y, c.Z, 1f + d).Normalized;
        }

        /// <summary>Spherical interpolation along the shorter arc; <paramref name="t"/> is clamped to [0, 1].</summary>
        public static Quat Slerp(Quat a, Quat b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float d = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
            if (d < 0f)
            {
                b = new Quat(-b.X, -b.Y, -b.Z, -b.W);
                d = -d;
            }
            if (d > 0.9995f)
            {
                return new Quat(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t).Normalized;
            }
            float theta = MathF.Acos(d);
            float s = MathF.Sin(theta);
            float wa = MathF.Sin((1f - t) * theta) / s, wb = MathF.Sin(t * theta) / s;
            return new Quat(a.X * wa + b.X * wb, a.Y * wa + b.Y * wb, a.Z * wa + b.Z * wb, a.W * wa + b.W * wb);
        }

        /// <summary>The angle between two rotations, degrees.</summary>
        public static float Angle(Quat a, Quat b)
        {
            float d = Math.Abs(a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W);
            return d >= 1f ? 0f : 2f * MathF.Acos(d) * Rad2Deg;
        }

        public bool IsFinite
        {
            get { return Fin(X) && Fin(Y) && Fin(Z) && Fin(W); }
        }

        private static bool Fin(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }

    /// <summary>Scalar helpers shared by the character code.</summary>
    public static class CharMath
    {
        public static float Clamp(float v, float min, float max)
        {
            return v < min ? min : v > max ? max : v;
        }

        public static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        public static float SmoothStep(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Exponential approach of <paramref name="current"/> to <paramref name="target"/> at
        /// <paramref name="rate"/> per second (frame-rate independent).</summary>
        public static float Approach(float current, float target, float rate, float dt)
        {
            if (!(dt > 0f)) return current;
            return target + (current - target) * MathF.Exp(-rate * dt);
        }

        public static float Finite(float v, float fallback = 0f)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? fallback : v;
        }

        /// <summary>Wraps an angle in radians to (−π, π].</summary>
        public static float WrapRad(float a)
        {
            const float twoPi = 6.28318530718f;
            if (float.IsNaN(a) || float.IsInfinity(a)) return 0f;
            a %= twoPi;
            if (a > MathF.PI) a -= twoPi;
            else if (a <= -MathF.PI) a += twoPi;
            return a;
        }

        /// <summary>A deterministic 32-bit hash (FNV-1a over the four bytes of each value), for per-character variation
        /// without allocation.</summary>
        public static uint Hash(uint a, uint b = 0u, uint c = 0u)
        {
            uint h = Data.Hashes.Fnv32Offset;
            h = Mix(h, a);
            h = Mix(h, b);
            h = Mix(h, c);
            return h;
        }

        private static uint Mix(uint h, uint v)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    h = (h ^ (v & 0xFF)) * Data.Hashes.Fnv32Prime;
                    v >>= 8;
                }
                return h;
            }
        }

        /// <summary>A uniform value in [0, 1) from a hash.</summary>
        public static float Unit(uint h)
        {
            return (h >> 8) * (1f / 16777216f);
        }
    }
}
