using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The square's stone-flag paving (kds-temples.md §2.3; judge round 1): one continuous, nearly level plaza of grey stone
    /// flags (#A39E94, joints #7E786E; the flag courses are the material's) over the pedestrian square (OSM r21291455), the
    /// temple field, Maru and the bell lane, on the levelled ground (<see cref="KdsGround"/>) a few centimetres over the
    /// terrain, in cells whose tones vary a little like worn flags. Where a mapped footway or path is drawn as a raised
    /// road ribbon across the square, the flags rise over it (the roads package draws those ribbons; the square's footways
    /// should be flush flags, an open issue), so the plaza reads as one surface; the vehicle lanes (the pedestrian streets:
    /// the bell lane, Maru to Jhochhen) stay the roads'. The Basantapur platform has its own brick
    /// (<see cref="Basantapur.Square"/>). Cells wholly inside a building outline are left out.
    /// </summary>
    internal static class SquarePaving
    {
        /// <summary>The paving's top over its frame (the temple field's level): the plaza rises to Maru and over the footway ribbons.</summary>
        public const float TopM = 1.0f;

        private static readonly ShapeBrush Flag = Looks.Of(MeshColor.FromHex(0xA39E94), MaterialChannel.Flagstone);

        /// <summary>The paving's brief bounding box (m): the pedestrian square and the open zones.</summary>
        private const double X0 = -200.0, X1 = 150.0, Y0 = -126.0, Y1 = 90.0;

        public static double Build(KdsCtx x)
        {
            MeshData m = x.M;
            TileData t = x.Tile;
            int lod = x.Lod;
            double pitch = lod == 0 ? 5.0 : lod == 1 ? 8.0 : lod == 2 ? 16.0 : 24.0;
            KdsGround.BriefMap map = KdsGround.BriefMap.For(t);
            // The tile-local box of the paving.
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int q = 0; q < 4; q++)
            {
                double tx, tz;
                KdsTemplesSite.LocalToTile(t, (q & 1) == 0 ? X0 : X1, (q & 2) == 0 ? Y0 : Y1, out tx, out tz);
                minX = Math.Min(minX, tx);
                maxX = Math.Max(maxX, tx);
                minZ = Math.Min(minZ, tz);
                maxZ = Math.Max(maxZ, tz);
            }
            minX = Math.Max(0, minX);
            minZ = Math.Max(0, minZ);
            maxX = Math.Min(t.Tile.Size, maxX);
            maxZ = Math.Min(t.Tile.Size, maxZ);
            if (maxX <= minX || maxZ <= minZ) return 0;
            var outlines = new Outlines(t, minX, minZ, maxX, maxZ);
            RoadSurfaceQuery roads = lod <= 1 && x.Heights != null && t.Roads.Count > 0 ? RoadSurfaceQuery.For(t, x.Heights) : null;
            int nx = (int)Math.Ceiling((maxX - minX) / pitch), nz = (int)Math.Ceiling((maxZ - minZ) / pitch);
            // Vertex heights on the grid (levelled ground plus the paving lift, risen over the footway ribbons).
            var ys = new double[(nx + 1) * (nz + 1)];
            var paved = new bool[(nx + 1) * (nz + 1)];
            for (int j = 0; j <= nz; j++)
            {
                for (int i = 0; i <= nx; i++)
                {
                    double tx = minX + i * pitch, tz = minZ + j * pitch, bx, by;
                    map.Map(tx, tz, out bx, out by);
                    double level = KdsGround.LevelAt(bx, by);
                    if (double.IsNaN(level)) level = KdsGround.Snap(KdsGround.Ramp(bx));
                    double y = level + KdsGround.PavingLiftM;
                    RoadSurfaceHit hit;
                    if (roads != null && roads.TrySample(t.Tile.X0 + tx, t.Tile.Z0 + tz, out hit) && hit.Road >= 0 &&
                        Raised(t.Roads[hit.Road].RoadClass) && Math.Abs(hit.Y - level) < 0.6)
                        y = Math.Max(y, hit.Y + 0.015);
                    ys[j * (nx + 1) + i] = y;
                    paved[j * (nx + 1) + i] = KdsGround.InPlaza(bx, by) && !Inside(Basantapur.MarketX, Basantapur.MarketY, bx, by);
                }
            }
            uint seed = 0x464C4147u;
            int cells = 0;
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int k00 = j * (nx + 1) + i, k10 = k00 + 1, k01 = k00 + nx + 1, k11 = k01 + 1;
                    double tx0 = minX + i * pitch, tz0 = minZ + j * pitch, tx1 = tx0 + pitch, tz1 = tz0 + pitch;
                    double bx, by;
                    map.Map(tx0 + 0.5 * pitch, tz0 + 0.5 * pitch, out bx, out by);
                    // A cell is paved when its centre lies in the plaza, or most of its corners do (the plaza's edge).
                    int corners = (paved[k00] ? 1 : 0) + (paved[k10] ? 1 : 0) + (paved[k01] ? 1 : 0) + (paved[k11] ? 1 : 0);
                    bool centre = KdsGround.InPlaza(bx, by) && !Inside(Basantapur.MarketX, Basantapur.MarketY, bx, by);
                    if (!centre && corners < 3) continue;
                    if (outlines.Covers(tx0, tz0) && outlines.Covers(tx1, tz0) && outlines.Covers(tx0, tz1) && outlines.Covers(tx1, tz1)) continue;
                    float tone = 0.92f + 0.13f * (float)((seed = seed * 1664525u + 1013904223u) >> 8) / 16777216f;
                    ShapeBrush b = Looks.Of(MeshColor.Scale(Flag.Color, tone), MaterialChannel.Flagstone);
                    int a = SacredDraw.V(m, Affine3.Identity, tx0, ys[k00], tz0, 0, 1, 0, b), c = SacredDraw.V(m, Affine3.Identity, tx1, ys[k10], tz0, 0, 1, 0, b);
                    int d = SacredDraw.V(m, Affine3.Identity, tx1, ys[k11], tz1, 0, 1, 0, b), e = SacredDraw.V(m, Affine3.Identity, tx0, ys[k01], tz1, 0, 1, 0, b);
                    SacredDraw.Quad(m, a, c, d, e);
                    cells++;
                }
            }
            if (x.Stats != null) x.Stats.PlinthTopM = (float)KdsGround.PavingLiftM;
            return cells > 0 ? TopM : 0;
        }

        /// <summary>The road classes the flags cover where the roads package draws them as raised ribbons across the square
        /// (footways, paths, steps, cycleways); the pedestrian streets (the bell lane and Maru to Jhochhen, where vehicles
        /// pass) stay the roads'.</summary>
        private static bool Raised(RoadClass c)
        {
            return c == RoadClass.Footway || c == RoadClass.Path || c == RoadClass.Steps || c == RoadClass.Cycleway || c == RoadClass.Bridleway;
        }

        private static bool Inside(double[] xs, double[] ys, double x, double y)
        {
            return KdsGround.Inside(xs, ys, x, y);
        }

        /// <summary>The tile's building outlines over the paving's box, bucketed for point tests (tile-local metres).</summary>
        private sealed class Outlines
        {
            private const double Cell = 16.0;
            private readonly double _x0, _z0;
            private readonly int _nx, _nz;
            private readonly List<int>[] _cells;
            private readonly List<double[]> _rings = new List<double[]>();

            public Outlines(TileData t, double x0, double z0, double x1, double z1)
            {
                _x0 = x0;
                _z0 = z0;
                _nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / Cell));
                _nz = Math.Max(1, (int)Math.Ceiling((z1 - z0) / Cell));
                _cells = new List<int>[_nx * _nz];
                foreach (BuildingRecord b in t.Buildings)
                {
                    int[] r = b.Rings[0];
                    double bx0 = double.MaxValue, bz0 = double.MaxValue, bx1 = double.MinValue, bz1 = double.MinValue;
                    var p = new double[r.Length];
                    for (int i = 0; i < r.Length / 2; i++)
                    {
                        p[2 * i] = r[2 * i] / 100.0;
                        p[2 * i + 1] = r[2 * i + 1] / 100.0;
                        bx0 = Math.Min(bx0, p[2 * i]);
                        bx1 = Math.Max(bx1, p[2 * i]);
                        bz0 = Math.Min(bz0, p[2 * i + 1]);
                        bz1 = Math.Max(bz1, p[2 * i + 1]);
                    }
                    if (bx1 < x0 || bz1 < z0 || bx0 > x1 || bz0 > z1) continue;
                    int id = _rings.Count;
                    _rings.Add(p);
                    for (int cj = Clamp((int)Math.Floor((bz0 - z0) / Cell), _nz); cj <= Clamp((int)Math.Floor((bz1 - z0) / Cell), _nz); cj++)
                    for (int ci = Clamp((int)Math.Floor((bx0 - x0) / Cell), _nx); ci <= Clamp((int)Math.Floor((bx1 - x0) / Cell), _nx); ci++)
                        (_cells[cj * _nx + ci] ?? (_cells[cj * _nx + ci] = new List<int>())).Add(id);
                }
            }

            private static int Clamp(int i, int n)
            {
                return i < 0 ? 0 : i >= n ? n - 1 : i;
            }

            /// <summary>True when the tile-local point lies inside a building outline.</summary>
            public bool Covers(double x, double z)
            {
                int ci = Clamp((int)Math.Floor((x - _x0) / Cell), _nx), cj = Clamp((int)Math.Floor((z - _z0) / Cell), _nz);
                List<int> l = _cells[cj * _nx + ci];
                if (l == null) return false;
                foreach (int id in l)
                {
                    double[] p = _rings[id];
                    int n = p.Length / 2;
                    bool inside = false;
                    for (int i = 0, k = n - 1; i < n; k = i++)
                    {
                        double zi = p[2 * i + 1], zk = p[2 * k + 1];
                        if ((zi > z) != (zk > z) && x < (p[2 * k] - p[2 * i]) * (z - zi) / (zk - zi) + p[2 * i]) inside = !inside;
                    }
                    if (inside) return true;
                }
                return false;
            }
        }
    }
}
