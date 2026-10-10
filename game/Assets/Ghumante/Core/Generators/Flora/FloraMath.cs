using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>A small float vector for the flora generators (X east, Y up, Z north; metres).</summary>
    internal readonly struct Vec3
    {
        public readonly float X, Y, Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vec3(double x, double y, double z)
        {
            X = (float)x;
            Y = (float)y;
            Z = (float)z;
        }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        public static Vec3 operator +(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vec3 operator -(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vec3 operator -(Vec3 a)
        {
            return new Vec3(-a.X, -a.Y, -a.Z);
        }

        public static Vec3 operator *(Vec3 a, float s)
        {
            return new Vec3(a.X * s, a.Y * s, a.Z * s);
        }

        public static Vec3 operator *(float s, Vec3 a)
        {
            return new Vec3(a.X * s, a.Y * s, a.Z * s);
        }

        public float Length
        {
            get { return (float)Math.Sqrt(X * X + Y * Y + Z * Z); }
        }

        /// <summary>Unit vector (up when the vector is about zero).</summary>
        public Vec3 Normalized
        {
            get
            {
                float l = Length;
                return l < 1e-8f ? Up : new Vec3(X / l, Y / l, Z / l);
            }
        }

        public static float Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        public static Vec3 Lerp(Vec3 a, Vec3 b, float t)
        {
            return new Vec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        /// <summary>Horizontal direction at a bearing (radians from +X toward +Z) tilted up by <paramref name="pitch"/>.</summary>
        public static Vec3 Dir(double bearing, double pitch)
        {
            double c = Math.Cos(pitch);
            return new Vec3(Math.Cos(bearing) * c, Math.Sin(pitch), Math.Sin(bearing) * c);
        }

        /// <summary>Any unit vector perpendicular to this (unit) vector.</summary>
        public Vec3 AnyPerpendicular()
        {
            Vec3 a = Math.Abs(Y) < 0.9f ? Up : new Vec3(1f, 0f, 0f);
            return Cross(a, this).Normalized;
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ", " + Z + ")";
        }
    }

    /// <summary>
    /// Deterministic random stream of the flora generators and placement (W2_DESIGN 10.2: FNV-1a seeding through
    /// <see cref="Hashes"/>, xorshift steps, no <c>System.Random</c>), so every device builds the same plants.
    /// </summary>
    public struct FloraRng
    {
        private uint _s;

        public FloraRng(uint seed, uint purpose)
        {
            _s = Mix(seed, purpose);
            if (_s == 0) _s = 0x9E3779B9;
        }

        /// <summary>FNV-1a 32 over the 8 little-endian bytes of (a, b).</summary>
        public static uint Mix(uint a, uint b)
        {
            unchecked
            {
                uint h = Hashes.Fnv32Offset;
                for (int i = 0; i < 4; i++) h = (h ^ (byte)(a >> (8 * i))) * Hashes.Fnv32Prime;
                for (int i = 0; i < 4; i++) h = (h ^ (byte)(b >> (8 * i))) * Hashes.Fnv32Prime;
                return h;
            }
        }

        /// <summary>Next 32-bit value (xorshift32).</summary>
        public uint NextUInt()
        {
            uint x = _s;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _s = x;
            return x;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Next()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform in [lo, hi).</summary>
        public float Range(float lo, float hi)
        {
            return lo + (hi - lo) * Next();
        }

        /// <summary>Uniform integer in [lo, hi] (inclusive).</summary>
        public int Int(int lo, int hi)
        {
            if (hi <= lo) return lo;
            return lo + (int)(NextUInt() % (uint)(hi - lo + 1));
        }

        /// <summary>True with probability <paramref name="p"/>.</summary>
        public bool Chance(float p)
        {
            return Next() < p;
        }

        /// <summary>An index drawn from relative weights (any positive scale).</summary>
        public int Pick(float[] weights)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++) sum += weights[i];
            float r = Next() * sum;
            for (int i = 0; i < weights.Length; i++)
            {
                r -= weights[i];
                if (r < 0f) return i;
            }
            return weights.Length - 1;
        }

        /// <summary>Symmetric jitter in [-a, a].</summary>
        public float Jitter(float a)
        {
            return (Next() * 2f - 1f) * a;
        }
    }

    /// <summary>
    /// Deterministic lattice value noise for lumpy canopies, rocks and ground colour (W2_DESIGN 10.2: hashed with
    /// FNV-1a through <see cref="FloraRng.Mix"/>, no <c>System.Random</c>). Results are in [-1, 1]; the same inputs
    /// give the same value on every device. Allocation-free and thread-safe.
    /// </summary>
    public static class FloraNoise
    {
        /// <summary>Hash of an integer lattice point to [-1, 1].</summary>
        public static float Lattice(int x, int y, int z, uint seed)
        {
            uint h = FloraRng.Mix(FloraRng.Mix((uint)x, (uint)y), FloraRng.Mix((uint)z, seed));
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            return (h & 0xFFFFFF) * (2f / 16777215f) - 1f;
        }

        /// <summary>Hash of a 2-D lattice point to [-1, 1].</summary>
        public static float Lattice(int x, int z, uint seed)
        {
            return Lattice(x, 0x5bd1e995, z, seed);
        }

        /// <summary>Smooth 3-D value noise at (x, y, z) (unit lattice), in [-1, 1].</summary>
        public static float Value(double x, double y, double z, uint seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y), iz = (int)Math.Floor(z);
            float fx = Fade((float)(x - ix)), fy = Fade((float)(y - iy)), fz = Fade((float)(z - iz));
            float a = L(Lattice(ix, iy, iz, seed), Lattice(ix + 1, iy, iz, seed), fx);
            float b = L(Lattice(ix, iy + 1, iz, seed), Lattice(ix + 1, iy + 1, iz, seed), fx);
            float c = L(Lattice(ix, iy, iz + 1, seed), Lattice(ix + 1, iy, iz + 1, seed), fx);
            float d = L(Lattice(ix, iy + 1, iz + 1, seed), Lattice(ix + 1, iy + 1, iz + 1, seed), fx);
            return L(L(a, b, fy), L(c, d, fy), fz);
        }

        /// <summary>Smooth 2-D value noise at (x, z) (unit lattice), in [-1, 1].</summary>
        public static float Value(double x, double z, uint seed)
        {
            int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
            float fx = Fade((float)(x - ix)), fz = Fade((float)(z - iz));
            float a = L(Lattice(ix, iz, seed), Lattice(ix + 1, iz, seed), fx);
            float b = L(Lattice(ix, iz + 1, seed), Lattice(ix + 1, iz + 1, seed), fx);
            return L(a, b, fz);
        }

        /// <summary>Fractal 2-D value noise: <paramref name="octaves"/> octaves from <paramref name="scaleM"/>
        /// (metres per lattice cell) down, each half the size and half the amplitude; result in about [-1, 1].</summary>
        public static float Fbm(double x, double z, double scaleM, int octaves, uint seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            double f = 1.0 / scaleM;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Value(x * f, z * f, seed + (uint)o * 0x9E3779B9u);
                norm += amp;
                amp *= 0.5f;
                f *= 2.0;
            }
            return sum / norm;
        }

        /// <summary>
        /// Gradient (d/dx, d/dz) of <see cref="Fbm"/> by central differences over <paramref name="epsM"/>: the
        /// micro-relief the terrain mesher bends its normals with.
        /// </summary>
        public static void FbmGradient(double x, double z, double scaleM, int octaves, uint seed, double epsM, out float gx, out float gz)
        {
            gx = (float)((Fbm(x + epsM, z, scaleM, octaves, seed) - Fbm(x - epsM, z, scaleM, octaves, seed)) / (2 * epsM));
            gz = (float)((Fbm(x, z + epsM, scaleM, octaves, seed) - Fbm(x, z - epsM, scaleM, octaves, seed)) / (2 * epsM));
        }

        private static float Fade(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float L(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
