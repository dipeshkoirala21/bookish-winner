using System;

namespace Ghumante.Core.Synth.Textures
{
    /// <summary>
    /// Periodic noise for the material textures (<see cref="MaterialTextures"/>): an integer lattice hash, value noise,
    /// fractal value noise and cellular (Worley) noise, all periodic over the unit square with integer periods, so
    /// every texture made from them tiles seamlessly. Pure functions of their arguments and a seed (deterministic,
    /// thread-safe, allocation-free).
    /// </summary>
    internal static class TextureNoise
    {
        /// <summary>A well-mixed 32-bit hash of a lattice point and a seed.</summary>
        public static uint Hash(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = seed ^ ((uint)x * 0x27D4EB2Du) ^ ((uint)y * 0x165667B1u);
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>Hash of a lattice point as a float in [0, 1).</summary>
        public static float Hash01(int x, int y, uint seed)
        {
            return (Hash(x, y, seed) >> 8) * (1f / 16777216f);
        }

        /// <summary>Euclidean modulo (always in [0, m)).</summary>
        public static int Wrap(int i, int m)
        {
            int r = i % m;
            return r < 0 ? r + m : r;
        }

        public static int Floor(float x)
        {
            int i = (int)x;
            return x < i ? i - 1 : i;
        }

        public static float Frac(float x)
        {
            return x - Floor(x);
        }

        public static float Clamp01(float x)
        {
            return x < 0f ? 0f : x > 1f ? 1f : x;
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        public static float SmoothStep(float e0, float e1, float x)
        {
            float t = Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float Quintic(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        /// <summary>Value noise in [0, 1] at texture coordinates (u, v), periodic over the unit square with
        /// <paramref name="px"/> × <paramref name="py"/> lattice cells.</summary>
        public static float Value(float u, float v, int px, int py, uint seed)
        {
            float x = u * px, y = v * py;
            int xi = Floor(x), yi = Floor(y);
            float fx = Quintic(x - xi), fy = Quintic(y - yi);
            int x0 = Wrap(xi, px), x1 = Wrap(xi + 1, px), y0 = Wrap(yi, py), y1 = Wrap(yi + 1, py);
            float a = Hash01(x0, y0, seed), b = Hash01(x1, y0, seed), c = Hash01(x0, y1, seed), d = Hash01(x1, y1, seed);
            return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
        }

        /// <summary>Fractal value noise in [0, 1]: <paramref name="octaves"/> octaves of <see cref="Value"/>, the period
        /// doubling and the amplitude halving each octave (stays periodic).</summary>
        public static float Fbm(float u, float v, int px, int py, int octaves, uint seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Value(u, v, px << o, py << o, seed + (uint)o * 0x9E3779B9u);
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>
        /// Periodic cellular noise over <paramref name="px"/> × <paramref name="py"/> cells, one jittered feature point
        /// per cell. Distances are in cell units (anisotropic cells stay round in cell space). Returns F1, F2 (distances to
        /// the nearest and second-nearest point) and the hash of the nearest point's cell.
        /// </summary>
        public static void Worley(float u, float v, int px, int py, uint seed, float jitter, out float f1, out float f2, out uint id)
        {
            float x = u * px, y = v * py;
            int xi = Floor(x), yi = Floor(y);
            f1 = 9f;
            f2 = 9f;
            id = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = xi + dx, cy = yi + dy;
                    int wx = Wrap(cx, px), wy = Wrap(cy, py);
                    uint h = Hash(wx, wy, seed);
                    float jx = ((h & 0xFFFF) * (1f / 65536f) - 0.5f) * jitter + 0.5f;
                    float jy = ((h >> 16) * (1f / 65536f) - 0.5f) * jitter + 0.5f;
                    float ddx = cx + jx - x, ddy = cy + jy - y;
                    float d = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
                    if (d < f1)
                    {
                        f2 = f1;
                        f1 = d;
                        id = h;
                    }
                    else if (d < f2)
                    {
                        f2 = d;
                    }
                }
            }
        }
    }
}
