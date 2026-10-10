using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing.Shapes
{
    /// <summary>
    /// Deterministic noise for organic shapes (rocks, foliage clumps, tree canopies, mud, bark): an FNV-1a lattice
    /// hash (<see cref="Hashes"/> constants, with an avalanche finaliser), 3D value noise, 3D gradient (Perlin-style)
    /// noise and fractal sums, all in [-1, 1] and identical on every platform for the same seed. Plus mesh helpers:
    /// <see cref="Displace"/> pushes vertices along their normals and <see cref="RecomputeNormals"/> rebuilds smooth
    /// normals afterwards (welding coincident seam vertices so cube-sphere seams stay invisible).
    /// </summary>
    public static class ShapeNoise
    {
        /// <summary>FNV-1a 32 over the little-endian bytes of (x, y, z, seed), then a murmur-style avalanche.</summary>
        public static uint Hash(int x, int y, int z, uint seed)
        {
            uint h = Hashes.Fnv32Offset;
            h = Mix(h, (uint)x);
            h = Mix(h, (uint)y);
            h = Mix(h, (uint)z);
            h = Mix(h, seed);
            h ^= h >> 16;
            h = unchecked(h * 0x85EBCA6Bu);
            h ^= h >> 13;
            h = unchecked(h * 0xC2B2AE35u);
            h ^= h >> 16;
            return h;
        }

        private static uint Mix(uint h, uint v)
        {
            unchecked
            {
                h = (h ^ (v & 0xFF)) * Hashes.Fnv32Prime;
                h = (h ^ ((v >> 8) & 0xFF)) * Hashes.Fnv32Prime;
                h = (h ^ ((v >> 16) & 0xFF)) * Hashes.Fnv32Prime;
                h = (h ^ (v >> 24)) * Hashes.Fnv32Prime;
            }
            return h;
        }

        /// <summary>A hash mapped to [0, 1).</summary>
        public static double Unit(int x, int y, int z, uint seed)
        {
            return (Hash(x, y, z, seed) >> 8) * (1.0 / 16777216.0);
        }

        /// <summary>3D value noise in [-1, 1] (smooth-step interpolation of lattice values).</summary>
        public static double Value3(double x, double y, double z, uint seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y), iz = (int)Math.Floor(z);
            double fx = x - ix, fy = y - iy, fz = z - iz;
            double ux = Fade(fx), uy = Fade(fy), uz = Fade(fz);
            double c000 = Lat(ix, iy, iz, seed), c100 = Lat(ix + 1, iy, iz, seed);
            double c010 = Lat(ix, iy + 1, iz, seed), c110 = Lat(ix + 1, iy + 1, iz, seed);
            double c001 = Lat(ix, iy, iz + 1, seed), c101 = Lat(ix + 1, iy, iz + 1, seed);
            double c011 = Lat(ix, iy + 1, iz + 1, seed), c111 = Lat(ix + 1, iy + 1, iz + 1, seed);
            double x00 = c000 + (c100 - c000) * ux, x10 = c010 + (c110 - c010) * ux;
            double x01 = c001 + (c101 - c001) * ux, x11 = c011 + (c111 - c011) * ux;
            double y0 = x00 + (x10 - x00) * uy, y1 = x01 + (x11 - x01) * uy;
            return y0 + (y1 - y0) * uz;
        }

        private static double Lat(int x, int y, int z, uint seed)
        {
            return Unit(x, y, z, seed) * 2 - 1;
        }

        /// <summary>3D gradient (Perlin-style) noise, roughly in [-1, 1], zero at lattice points.</summary>
        public static double Gradient3(double x, double y, double z, uint seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y), iz = (int)Math.Floor(z);
            double fx = x - ix, fy = y - iy, fz = z - iz;
            double ux = Fade(fx), uy = Fade(fy), uz = Fade(fz);
            double g000 = Grad(ix, iy, iz, seed, fx, fy, fz), g100 = Grad(ix + 1, iy, iz, seed, fx - 1, fy, fz);
            double g010 = Grad(ix, iy + 1, iz, seed, fx, fy - 1, fz), g110 = Grad(ix + 1, iy + 1, iz, seed, fx - 1, fy - 1, fz);
            double g001 = Grad(ix, iy, iz + 1, seed, fx, fy, fz - 1), g101 = Grad(ix + 1, iy, iz + 1, seed, fx - 1, fy, fz - 1);
            double g011 = Grad(ix, iy + 1, iz + 1, seed, fx, fy - 1, fz - 1), g111 = Grad(ix + 1, iy + 1, iz + 1, seed, fx - 1, fy - 1, fz - 1);
            double x00 = g000 + (g100 - g000) * ux, x10 = g010 + (g110 - g010) * ux;
            double x01 = g001 + (g101 - g001) * ux, x11 = g011 + (g111 - g011) * ux;
            double y0 = x00 + (x10 - x00) * uy, y1 = x01 + (x11 - x01) * uy;
            double v = (y0 + (y1 - y0) * uz) * 1.15;
            return v < -1 ? -1 : v > 1 ? 1 : v;
        }

        private static double Grad(int x, int y, int z, uint seed, double dx, double dy, double dz)
        {
            // The 12 edge directions of a cube (Perlin's improved noise).
            switch (Hash(x, y, z, seed) % 12)
            {
                case 0: return dx + dy;
                case 1: return -dx + dy;
                case 2: return dx - dy;
                case 3: return -dx - dy;
                case 4: return dx + dz;
                case 5: return -dx + dz;
                case 6: return dx - dz;
                case 7: return -dx - dz;
                case 8: return dy + dz;
                case 9: return -dy + dz;
                case 10: return dy - dz;
                default: return -dy - dz;
            }
        }

        private static double Fade(double t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        /// <summary>Fractal sum of <see cref="Gradient3"/> octaves, normalised to about [-1, 1].</summary>
        public static double Fbm3(double x, double y, double z, uint seed, int octaves = 3, double lacunarity = 2.0, double gain = 0.5)
        {
            double sum = 0, amp = 1, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Gradient3(x, y, z, seed + (uint)o * 0x9E3779B9u);
                norm += amp;
                amp *= gain;
                x *= lacunarity;
                y *= lacunarity;
                z *= lacunarity;
            }
            return norm > 0 ? sum / norm : 0;
        }

        /// <summary>
        /// Push vertices [first, first + count) along their normals by <paramref name="amplitude"/> ·
        /// fbm(position · <paramref name="frequency"/>). Coincident vertices move identically (the noise is keyed
        /// by position) as long as their normals agree, which holds for the smooth seams of every <see cref="Shapes"/>
        /// primitive; a creased (hard) edge opens up, so displace smooth shapes. Call <see cref="RecomputeNormals"/>
        /// afterwards.
        /// </summary>
        public static void Displace(MeshData m, int first, int count, double amplitude, double frequency, uint seed, int octaves = 3)
        {
            float[] p = m.Positions, n = m.Normals;
            for (int v = first; v < first + count; v++)
            {
                double x = p[3 * v], y = p[3 * v + 1], z = p[3 * v + 2];
                double d = amplitude * Fbm3(x * frequency, y * frequency, z * frequency, seed, octaves);
                p[3 * v] = (float)(x + n[3 * v] * d);
                p[3 * v + 1] = (float)(y + n[3 * v + 1] * d);
                p[3 * v + 2] = (float)(z + n[3 * v + 2] * d);
            }
        }

        [ThreadStatic] private static Dictionary<long, int> s_weld;
        [ThreadStatic] private static int[] s_rep;
        [ThreadStatic] private static double[] s_acc;

        /// <summary>
        /// Rebuild smooth, area-weighted normals for vertices [firstVertex, firstVertex + vertexCount) from the
        /// triangles [firstIndex, firstIndex + indexCount). With <paramref name="weld"/>, vertices at the same
        /// position (within 0.1 mm) share one normal, which hides UV and cube-face seams; hard creases made of
        /// duplicated vertices are smoothed too, so leave it off for creased parts.
        /// </summary>
        public static void RecomputeNormals(MeshData m, int firstVertex, int vertexCount, int firstIndex, int indexCount, bool weld = true)
        {
            if (vertexCount <= 0) return;
            if (s_rep == null || s_rep.Length < vertexCount) s_rep = new int[Math.Max(vertexCount, 256)];
            if (s_acc == null || s_acc.Length < 3 * vertexCount) s_acc = new double[Math.Max(3 * vertexCount, 768)];
            int[] rep = s_rep;
            double[] acc = s_acc;
            float[] p = m.Positions;
            if (weld)
            {
                if (s_weld == null) s_weld = new Dictionary<long, int>(1024);
                s_weld.Clear();
                for (int i = 0; i < vertexCount; i++)
                {
                    int v = firstVertex + i;
                    long key = Quant(p[3 * v]) * 73856093L ^ Quant(p[3 * v + 1]) * 19349663L ^ Quant(p[3 * v + 2]) * 83492791L;
                    int r;
                    if (s_weld.TryGetValue(key, out r) && SameSpot(p, firstVertex + r, v)) rep[i] = r;
                    else
                    {
                        if (!s_weld.ContainsKey(key)) s_weld.Add(key, i);
                        rep[i] = i;
                    }
                }
            }
            else
            {
                for (int i = 0; i < vertexCount; i++) rep[i] = i;
            }
            Array.Clear(acc, 0, 3 * vertexCount);
            for (int t = firstIndex; t + 2 < firstIndex + indexCount; t += 3)
            {
                int a = m.Indices[t], b = m.Indices[t + 1], c = m.Indices[t + 2];
                if (a < firstVertex || b < firstVertex || c < firstVertex) continue;
                if (a >= firstVertex + vertexCount || b >= firstVertex + vertexCount || c >= firstVertex + vertexCount) continue;
                double ux = p[3 * b] - (double)p[3 * a], uy = p[3 * b + 1] - (double)p[3 * a + 1], uz = p[3 * b + 2] - (double)p[3 * a + 2];
                double vx = p[3 * c] - (double)p[3 * a], vy = p[3 * c + 1] - (double)p[3 * a + 1], vz = p[3 * c + 2] - (double)p[3 * a + 2];
                double fx = uy * vz - uz * vy, fy = uz * vx - ux * vz, fz = ux * vy - uy * vx;
                int ra = rep[a - firstVertex], rb = rep[b - firstVertex], rc = rep[c - firstVertex];
                acc[3 * ra] += fx;
                acc[3 * ra + 1] += fy;
                acc[3 * ra + 2] += fz;
                acc[3 * rb] += fx;
                acc[3 * rb + 1] += fy;
                acc[3 * rb + 2] += fz;
                acc[3 * rc] += fx;
                acc[3 * rc + 1] += fy;
                acc[3 * rc + 2] += fz;
            }
            for (int i = 0; i < vertexCount; i++)
            {
                int r = rep[i], v = firstVertex + i;
                double nx = acc[3 * r], ny = acc[3 * r + 1], nz = acc[3 * r + 2];
                double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (l < 1e-20) continue; // keep the old normal
                m.Normals[3 * v] = (float)(nx / l);
                m.Normals[3 * v + 1] = (float)(ny / l);
                m.Normals[3 * v + 2] = (float)(nz / l);
            }
        }

        private static long Quant(float f)
        {
            return (long)Math.Round(f * 1e4);
        }

        private static bool SameSpot(float[] p, int a, int b)
        {
            return Math.Abs(p[3 * a] - p[3 * b]) < 2e-4f && Math.Abs(p[3 * a + 1] - p[3 * b + 1]) < 2e-4f &&
                   Math.Abs(p[3 * a + 2] - p[3 * b + 2]) < 2e-4f;
        }
    }
}
