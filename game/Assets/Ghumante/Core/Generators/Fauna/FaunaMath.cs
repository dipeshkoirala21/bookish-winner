using System;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>A small float vector for the fauna meshers and the skinner (engine-free, allocation-free).</summary>
    public struct Fv3
    {
        public float X, Y, Z;

        public Fv3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Fv3 Zero = new Fv3(0f, 0f, 0f);
        public static readonly Fv3 Up = new Fv3(0f, 1f, 0f);
        public static readonly Fv3 Forward = new Fv3(0f, 0f, 1f);
        public static readonly Fv3 Right = new Fv3(1f, 0f, 0f);

        public static Fv3 operator +(Fv3 a, Fv3 b)
        {
            return new Fv3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Fv3 operator -(Fv3 a, Fv3 b)
        {
            return new Fv3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Fv3 operator -(Fv3 a)
        {
            return new Fv3(-a.X, -a.Y, -a.Z);
        }

        public static Fv3 operator *(Fv3 a, float s)
        {
            return new Fv3(a.X * s, a.Y * s, a.Z * s);
        }

        public static Fv3 operator *(float s, Fv3 a)
        {
            return new Fv3(a.X * s, a.Y * s, a.Z * s);
        }

        public float Length
        {
            get { return (float)Math.Sqrt(X * X + Y * Y + Z * Z); }
        }

        /// <summary>Unit vector (or <paramref name="fallback"/> for a zero vector).</summary>
        public Fv3 NormalizedOr(Fv3 fallback)
        {
            float l = Length;
            return l > 1e-8f ? new Fv3(X / l, Y / l, Z / l) : fallback;
        }

        public Fv3 Normalized
        {
            get { return NormalizedOr(Up); }
        }

        /// <summary>The mirror across the animal's median plane (x → −x).</summary>
        public Fv3 MirrorX
        {
            get { return new Fv3(-X, Y, Z); }
        }

        public static float Dot(Fv3 a, Fv3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Fv3 Cross(Fv3 a, Fv3 b)
        {
            return new Fv3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        public static Fv3 Lerp(Fv3 a, Fv3 b, float t)
        {
            return new Fv3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        public static float Distance(Fv3 a, Fv3 b)
        {
            return (a - b).Length;
        }

        /// <summary>Catmull-Rom point between <paramref name="p1"/> and <paramref name="p2"/>.</summary>
        public static Fv3 CatmullRom(Fv3 p0, Fv3 p1, Fv3 p2, Fv3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            float a = -0.5f * t3 + t2 - 0.5f * t;
            float b = 1.5f * t3 - 2.5f * t2 + 1f;
            float c = -1.5f * t3 + 2f * t2 + 0.5f * t;
            float d = 0.5f * t3 - 0.5f * t2;
            return new Fv3(a * p0.X + b * p1.X + c * p2.X + d * p3.X, a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y,
                           a * p0.Z + b * p1.Z + c * p2.Z + d * p3.Z);
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " +
                   Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " +
                   Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")";
        }
    }

    /// <summary>
    /// A 3×3 rotation (row-major) built from Euler angles in Unity's order: <c>R = Ry(yaw) · Rx(pitch) · Rz(roll)</c>,
    /// so <c>Quaternion.Euler(pitch, yaw, roll)</c> on the Unity side is the same rotation. Positive pitch tips +Z
    /// down, positive yaw turns +Z towards +X, positive roll lifts +X.
    /// </summary>
    public struct FRot
    {
        public float M00, M01, M02, M10, M11, M12, M20, M21, M22;

        public static readonly FRot Identity = new FRot { M00 = 1f, M11 = 1f, M22 = 1f };

        /// <summary>The rotation for Euler angles in radians (pitch about X, yaw about Y, roll about Z).</summary>
        public static FRot Euler(float pitch, float yaw, float roll)
        {
            float cx = (float)Math.Cos(pitch), sx = (float)Math.Sin(pitch);
            float cy = (float)Math.Cos(yaw), sy = (float)Math.Sin(yaw);
            float cz = (float)Math.Cos(roll), sz = (float)Math.Sin(roll);
            // Ry * Rx * Rz with Rx = [1 0 0; 0 cx -sx; 0 sx cx], Ry = [cy 0 sy; 0 1 0; -sy 0 cy], Rz = [cz -sz 0; sz cz 0; 0 0 1].
            var r = new FRot();
            r.M00 = cy * cz + sy * sx * sz;
            r.M01 = -cy * sz + sy * sx * cz;
            r.M02 = sy * cx;
            r.M10 = cx * sz;
            r.M11 = cx * cz;
            r.M12 = -sx;
            r.M20 = -sy * cz + cy * sx * sz;
            r.M21 = sy * sz + cy * sx * cz;
            r.M22 = cy * cx;
            return r;
        }

        /// <summary>Rotation by <paramref name="angle"/> radians about a unit <paramref name="axis"/> (right-hand rule
        /// in these coordinates, i.e. the same sense as <see cref="Euler"/> for the principal axes).</summary>
        public static FRot AxisAngle(Fv3 axis, float angle)
        {
            Fv3 a = axis.Normalized;
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle), t = 1f - c;
            var r = new FRot();
            r.M00 = t * a.X * a.X + c;
            r.M01 = t * a.X * a.Y - s * a.Z;
            r.M02 = t * a.X * a.Z + s * a.Y;
            r.M10 = t * a.X * a.Y + s * a.Z;
            r.M11 = t * a.Y * a.Y + c;
            r.M12 = t * a.Y * a.Z - s * a.X;
            r.M20 = t * a.X * a.Z - s * a.Y;
            r.M21 = t * a.Y * a.Z + s * a.X;
            r.M22 = t * a.Z * a.Z + c;
            return r;
        }

        public static FRot operator *(FRot a, FRot b)
        {
            var r = new FRot();
            r.M00 = a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20;
            r.M01 = a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21;
            r.M02 = a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22;
            r.M10 = a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20;
            r.M11 = a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21;
            r.M12 = a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22;
            r.M20 = a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20;
            r.M21 = a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21;
            r.M22 = a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22;
            return r;
        }

        public static Fv3 operator *(FRot r, Fv3 v)
        {
            return new Fv3(r.M00 * v.X + r.M01 * v.Y + r.M02 * v.Z, r.M10 * v.X + r.M11 * v.Y + r.M12 * v.Z,
                           r.M20 * v.X + r.M21 * v.Y + r.M22 * v.Z);
        }

        /// <summary>The image of +Z (the forward axis).</summary>
        public Fv3 Forward
        {
            get { return new Fv3(M02, M12, M22); }
        }

        /// <summary>The image of +Y.</summary>
        public Fv3 UpAxis
        {
            get { return new Fv3(M01, M11, M21); }
        }
    }

    /// <summary>Scalar helpers shared by the fauna code.</summary>
    public static class FMath
    {
        public const float Pi = 3.14159265f;
        public const float TwoPi = 6.2831853f;
        public const float Deg = 0.0174532925f;

        public static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
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

        /// <summary>Smooth 0→1 ramp between <paramref name="e0"/> and <paramref name="e1"/>.</summary>
        public static float SmoothStep(float e0, float e1, float x)
        {
            return SmoothStep((x - e0) / (e1 - e0));
        }

        /// <summary>Fractional part in [0, 1).</summary>
        public static float Frac(float x)
        {
            return x - (float)Math.Floor(x);
        }

        public static float Sin(float x)
        {
            return (float)Math.Sin(x);
        }

        public static float Cos(float x)
        {
            return (float)Math.Cos(x);
        }

        /// <summary>A smooth pulse: 0 outside [0, 1], 1 at 0.5 (sin² window).</summary>
        public static float Pulse(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            float s = (float)Math.Sin(t * Pi);
            return s * s;
        }

        /// <summary>Wraps an angle to (−π, π].</summary>
        public static float WrapPi(float a)
        {
            a %= TwoPi;
            if (a > Pi) a -= TwoPi;
            else if (a <= -Pi) a += TwoPi;
            return a;
        }

        /// <summary>Linear interpolation of two 0xRRGGBBAA colours.</summary>
        public static uint LerpColour(uint a, uint b, float t)
        {
            t = Clamp01(t);
            uint r = 0;
            for (int s = 0; s < 32; s += 8)
            {
                float ca = (a >> s) & 0xFF, cb = (b >> s) & 0xFF;
                r |= (uint)(ca + (cb - ca) * t + 0.5f) << s;
            }
            return r;
        }

        /// <summary>A 0xRRGGBB colour with an alpha byte.</summary>
        public static uint Rgba(uint rgb, byte alpha)
        {
            return (rgb << 8) | alpha;
        }

        /// <summary>Scales the RGB of a 0xRRGGBBAA colour (alpha kept).</summary>
        public static uint Shade(uint rgba, float f)
        {
            uint r = (uint)Clamp(((rgba >> 24) & 0xFF) * f, 0f, 255f);
            uint g = (uint)Clamp(((rgba >> 16) & 0xFF) * f, 0f, 255f);
            uint b = (uint)Clamp(((rgba >> 8) & 0xFF) * f, 0f, 255f);
            return (r << 24) | (g << 16) | (b << 8) | (rgba & 0xFF);
        }
    }

    /// <summary>
    /// Deterministic hashing and a small counter-based random stream for the fauna code (FNV-1a mixing, the same
    /// family as <c>Core.Data.Hashes</c>): the same inputs give the same animals, coats and flocks on every device.
    /// </summary>
    public struct FaunaRng
    {
        private uint _state;

        public FaunaRng(uint seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : seed;
        }

        /// <summary>FNV-1a over the bytes of up to four 32-bit words.</summary>
        public static uint Hash(uint a, uint b = 0u, uint c = 0u, uint d = 0u)
        {
            uint h = 0x811C9DC5u;
            h = Mix(h, a);
            h = Mix(h, b);
            h = Mix(h, c);
            h = Mix(h, d);
            // Final avalanche so neighbouring cells do not get correlated low bits.
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }

        private static uint Mix(uint h, uint w)
        {
            unchecked
            {
                h = (h ^ (w & 0xFF)) * 0x01000193u;
                h = (h ^ ((w >> 8) & 0xFF)) * 0x01000193u;
                h = (h ^ ((w >> 16) & 0xFF)) * 0x01000193u;
                h = (h ^ (w >> 24)) * 0x01000193u;
            }
            return h;
        }

        /// <summary>Uniform in [0, 1) from a hash.</summary>
        public static float Unit(uint h)
        {
            return (h >> 8) * (1f / 16777216f);
        }

        /// <summary>Next raw value (xorshift32).</summary>
        public uint NextU32()
        {
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
            return (NextU32() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform in [lo, hi).</summary>
        public float Range(float lo, float hi)
        {
            return lo + (hi - lo) * NextFloat();
        }

        /// <summary>Uniform integer in [0, n).</summary>
        public int Next(int n)
        {
            return n <= 1 ? 0 : (int)(NextU32() % (uint)n);
        }

        public bool Chance(float p)
        {
            return NextFloat() < p;
        }
    }
}
