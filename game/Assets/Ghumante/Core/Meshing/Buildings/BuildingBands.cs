using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The far building bands (W2_DESIGN 2.4). <b>B2 prism</b>: each footprint simplified to its minimum-area oriented
    /// box (or its convex hull when that has at most 6 points), walls hidden by a neighbour culled, a flat top at the
    /// plan's mid-roof height: about 10 triangles. <b>B3 city block</b>: footprints rasterised to 16 m cells (max height,
    /// built cover ≥ 30%), greedily merged into boxes of equal height step (3 m), a top and the walls not hidden by an
    /// equally tall neighbour cell: about 10 triangles per box. Both take heights and colours from
    /// <see cref="BuildingGrammar.Plan(TileData, int)"/>, so they match the near bands.
    /// </summary>
    internal static class BuildingBands
    {
        public const double BlockCellM = 16.0;
        public const double MinCover = 0.30;
        public const double HeightStepM = 3.0;

        // ------------------------------------------------------------------------------------------------------------
        // B2
        // ------------------------------------------------------------------------------------------------------------

        public static int Prisms(TileData t, IHeightSampler h, BuildingOptions o, MeshData m)
        {
            var g = new RoadSurface(t, h);
            var index = new FootprintIndex(t);
            var hx = new double[64];
            var hz = new double[64];
            var bx = new double[8];
            var bz = new double[8];
            int drawn = 0;
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                BuildingRecord b = t.Buildings[i];
                if (o.SkipLandmarks && (b.Flags & BuildingFlags.Landmark) != 0) continue;
                if (o.HiddenRefs != null && o.HiddenRefs.Contains(b.OsmRef)) continue;
                if ((b.Flags & BuildingFlags.HasParts) != 0) continue;
                int[] ring = b.Rings[0];
                int n = ring.Length / 2;
                if (n < 3) continue;
                if (hx.Length < n + 1)
                {
                    hx = new double[n * 2];
                    hz = new double[n * 2];
                }
                int hn = Hull(ring, hx, hz);
                if (hn < 3) continue;
                int k;
                if (hn <= 6)
                {
                    for (int q = 0; q < hn; q++)
                    {
                        bx[q] = hx[q];
                        bz[q] = hz[q];
                    }
                    k = hn;
                }
                else
                {
                    MinAreaBox(hx, hz, hn, bx, bz);
                    k = 4;
                }
                double area = Math.Abs(Polygon.SignedArea(bx, bz, k));
                if (area < o.MinAreaM2) continue;
                HousePlan plan = BuildingGrammar.Plan(t, i);
                double cx = 0, cz = 0, ground = double.MaxValue;
                for (int q = 0; q < k; q++)
                {
                    cx += bx[q] / k;
                    cz += bz[q] / k;
                    ground = Math.Min(ground, g.Height(bx[q], bz[q]));
                }
                ground = Math.Min(ground, g.Height(cx, cz));
                double y0 = plan.MinHeightM > 0 ? ground + plan.MinHeightM : ground - o.SinkM;
                double y1 = ground + plan.WallTopM + 0.5 * plan.RoofRiseM;
                for (int q = 0; q < k; q++)
                {
                    int r = q + 1 == k ? 0 : q + 1;
                    double ex = bx[r] - bx[q], ez = bz[r] - bz[q], len = Math.Sqrt(ex * ex + ez * ez);
                    if (len < 1e-3) continue;
                    // Shared wall: the point 0.4 m outside the wall's middle lies in another footprint.
                    double px = 0.5 * (bx[q] + bx[r]) + ez / len * 0.4, pz = 0.5 * (bz[q] + bz[r]) - ex / len * 0.4;
                    if (index.Inside(px, pz, i)) continue;
                    uint c = q == 0 ? plan.Front : plan.Wall;
                    MeshKit.Quad(m, bx[q], y0, bz[q], bx[r], y0, bz[r], bx[r], y1, bz[r], bx[q], y1, bz[q], ez, 0, -ex, c);
                }
                MeshKit.ConvexCap(m, bx, bz, k, y1, true, plan.RoofColour);
                drawn++;
            }
            return drawn;
        }

        /// <summary>Convex hull (monotone chain, counter-clockwise) of a ring in tile-local metres.</summary>
        internal static int Hull(int[] ring, double[] hx, double[] hz)
        {
            int n = ring.Length / 2;
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, (a, b) => ring[2 * a] != ring[2 * b] ? ring[2 * a].CompareTo(ring[2 * b]) : ring[2 * a + 1].CompareTo(ring[2 * b + 1]));
            int k = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int start = k;
                for (int q = 0; q < n; q++)
                {
                    int i = pass == 0 ? idx[q] : idx[n - 1 - q];
                    double x = ring[2 * i] / 100.0, z = ring[2 * i + 1] / 100.0;
                    while (k >= start + 2 && (hx[k - 1] - hx[k - 2]) * (z - hz[k - 2]) - (hz[k - 1] - hz[k - 2]) * (x - hx[k - 2]) <= 0) k--;
                    hx[k] = x;
                    hz[k] = z;
                    k++;
                }
                k--; // the last point repeats the first of the other chain
            }
            return k < 3 ? 0 : k;
        }

        /// <summary>Minimum-area oriented box of a convex hull (rotating the hull edges), counter-clockwise.</summary>
        internal static void MinAreaBox(double[] hx, double[] hz, int n, double[] bx, double[] bz)
        {
            double best = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                double ux = hx[j] - hx[i], uz = hz[j] - hz[i], l = Math.Sqrt(ux * ux + uz * uz);
                if (l < 1e-9) continue;
                ux /= l;
                uz /= l;
                double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
                for (int q = 0; q < n; q++)
                {
                    double u = hx[q] * ux + hz[q] * uz, v = -hx[q] * uz + hz[q] * ux;
                    minU = Math.Min(minU, u);
                    maxU = Math.Max(maxU, u);
                    minV = Math.Min(minV, v);
                    maxV = Math.Max(maxV, v);
                }
                double area = (maxU - minU) * (maxV - minV);
                if (area >= best) continue;
                best = area;
                // Corners (u, v) back to x, z: x = u·ux − v·uz, z = u·uz + v·ux.
                double[] us = { minU, maxU, maxU, minU }, vs = { minV, minV, maxV, maxV };
                for (int c = 0; c < 4; c++)
                {
                    bx[c] = us[c] * ux - vs[c] * uz;
                    bz[c] = us[c] * uz + vs[c] * ux;
                }
            }
        }

        /// <summary>Point-in-footprint lookups over a tile's outer rings (grid of bounding boxes).</summary>
        internal sealed class FootprintIndex
        {
            private const double CellM = 32.0;
            private readonly TileData _t;
            private readonly int _n;
            private readonly List<int>[] _cells;
            private readonly double[] _minX, _minZ, _maxX, _maxZ;

            public FootprintIndex(TileData t)
            {
                _t = t;
                _n = Math.Max(1, (int)Math.Ceiling(t.Tile.Size / CellM));
                _cells = new List<int>[_n * _n];
                int count = t.Buildings.Count;
                _minX = new double[count];
                _minZ = new double[count];
                _maxX = new double[count];
                _maxZ = new double[count];
                for (int i = 0; i < count; i++)
                {
                    int[] r = t.Buildings[i].Rings[0];
                    double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                    for (int k = 0; k < r.Length / 2; k++)
                    {
                        double x = r[2 * k] / 100.0, z = r[2 * k + 1] / 100.0;
                        x0 = Math.Min(x0, x);
                        z0 = Math.Min(z0, z);
                        x1 = Math.Max(x1, x);
                        z1 = Math.Max(z1, z);
                    }
                    _minX[i] = x0;
                    _minZ[i] = z0;
                    _maxX[i] = x1;
                    _maxZ[i] = z1;
                    for (int cj = Clamp(z0); cj <= Clamp(z1); cj++)
                    for (int ci = Clamp(x0); ci <= Clamp(x1); ci++)
                    {
                        int c = cj * _n + ci;
                        if (_cells[c] == null) _cells[c] = new List<int>(4);
                        _cells[c].Add(i);
                    }
                }
            }

            private int Clamp(double v)
            {
                int c = (int)Math.Floor(v / CellM);
                return c < 0 ? 0 : c >= _n ? _n - 1 : c;
            }

            /// <summary>True when (x, z) lies inside a footprint other than <paramref name="except"/>.</summary>
            public bool Inside(double x, double z, int except)
            {
                List<int> l = _cells[Clamp(z) * _n + Clamp(x)];
                if (l == null) return false;
                foreach (int i in l)
                {
                    if (i == except || x < _minX[i] || x > _maxX[i] || z < _minZ[i] || z > _maxZ[i]) continue;
                    if (InRing(_t.Buildings[i].Rings[0], x, z)) return true;
                }
                return false;
            }

            public static bool InRing(int[] r, double x, double z)
            {
                bool inside = false;
                int n = r.Length / 2;
                double xc = x * 100.0, zc = z * 100.0;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double zi = r[2 * i + 1], zj = r[2 * j + 1], xi = r[2 * i], xj = r[2 * j];
                    if ((zi > zc) != (zj > zc) && xc < (xj - xi) * (zc - zi) / (zj - zi) + xi) inside = !inside;
                }
                return inside;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // B3
        // ------------------------------------------------------------------------------------------------------------

        public static int Blocks(TileData t, IHeightSampler h, BuildingOptions o, MeshData m)
        {
            int n = Math.Max(1, (int)Math.Round(t.Tile.Size / BlockCellM));
            double cell = t.Tile.Size / n;
            var built = new double[n * n];
            var maxH = new double[n * n];
            var colour = new uint[n * n];
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                BuildingRecord b = t.Buildings[i];
                if (o.SkipLandmarks && (b.Flags & BuildingFlags.Landmark) != 0) continue;
                if (o.HiddenRefs != null && o.HiddenRefs.Contains(b.OsmRef)) continue;
                int[] r = b.Rings[0];
                int pn = r.Length / 2;
                double cx = 0, cz = 0;
                for (int k = 0; k < pn; k++)
                {
                    cx += r[2 * k] / 100.0;
                    cz += r[2 * k + 1] / 100.0;
                }
                cx /= pn;
                cz /= pn;
                int ci = (int)(cx / cell), cj = (int)(cz / cell);
                if (ci < 0 || cj < 0 || ci >= n || cj >= n) continue;
                int c = cj * n + ci;
                built[c] += Math.Abs(AreaTypeGrid.RingArea(r));
                HousePlan plan = BuildingGrammar.Plan(t, i);
                double hgt = plan.WallTopM + 0.5 * plan.RoofRiseM;
                if (hgt > maxH[c])
                {
                    maxH[c] = hgt;
                    colour[c] = plan.Front;
                }
            }
            // Height steps per cell (0 = empty).
            var step = new int[n * n];
            for (int c = 0; c < n * n; c++)
                if (built[c] >= MinCover * cell * cell && maxH[c] > 0) step[c] = Math.Max(1, (int)Math.Round(maxH[c] / HeightStepM));
            var used = new bool[n * n];
            var g = new RoadSurface(t, h);
            int boxes = 0;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int c = j * n + i;
                if (used[c] || step[c] == 0) continue;
                int s = step[c];
                int w = 1;
                while (i + w < n && !used[j * n + i + w] && step[j * n + i + w] == s) w++;
                int d = 1;
                while (j + d < n)
                {
                    bool ok = true;
                    for (int q = 0; q < w && ok; q++) ok = !used[(j + d) * n + i + q] && step[(j + d) * n + i + q] == s;
                    if (!ok) break;
                    d++;
                }
                for (int dz = 0; dz < d; dz++)
                for (int dx = 0; dx < w; dx++)
                    used[(j + dz) * n + i + dx] = true;
                Box(t, ref g, i, j, w, d, n, cell, s, step, colour[c], o, m);
                boxes++;
            }
            return boxes;
        }

        private static void Box(TileData t, ref RoadSurface g, int i, int j, int w, int d, int n, double cell, int s, int[] step, uint c,
                                BuildingOptions o, MeshData m)
        {
            double x0 = i * cell, z0 = j * cell, x1 = (i + w) * cell, z1 = (j + d) * cell;
            double ground = Math.Min(Math.Min(g.Height(x0, z0), g.Height(x1, z0)), Math.Min(g.Height(x1, z1), g.Height(x0, z1)));
            ground = Math.Min(ground, g.Height(0.5 * (x0 + x1), 0.5 * (z0 + z1)));
            double y0 = ground - o.SinkM, y1 = ground + s * HeightStepM;
            uint roof = MeshColor.Scale(c, 0.85f);
            // Walls: skip a side whose whole neighbour row is at least as tall.
            if (!Covered(step, n, i, j - 1, w, 1, s)) MeshKit.Quad(m, x0, y0, z0, x1, y0, z0, x1, y1, z0, x0, y1, z0, 0, 0, -1, c);
            if (!Covered(step, n, i, j + d, w, 1, s)) MeshKit.Quad(m, x0, y0, z1, x1, y0, z1, x1, y1, z1, x0, y1, z1, 0, 0, 1, c);
            if (!Covered(step, n, i - 1, j, 1, d, s)) MeshKit.Quad(m, x0, y0, z0, x0, y0, z1, x0, y1, z1, x0, y1, z0, -1, 0, 0, c);
            if (!Covered(step, n, i + w, j, 1, d, s)) MeshKit.Quad(m, x1, y0, z0, x1, y0, z1, x1, y1, z1, x1, y1, z0, 1, 0, 0, c);
            MeshKit.Quad(m, x0, y1, z0, x1, y1, z0, x1, y1, z1, x0, y1, z1, 0, 1, 0, roof);
        }

        private static bool Covered(int[] step, int n, int i, int j, int w, int d, int s)
        {
            if (i < 0 || j < 0 || i + w > n || j + d > n) return false;
            for (int dz = 0; dz < d; dz++)
            for (int dx = 0; dx < w; dx++)
                if (step[(j + dz) * n + i + dx] < s) return false;
            return true;
        }
    }
}
