using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Generators.Placement
{
    /// <summary>
    /// A tile-local raster (default 4 m cells) of where generated dressing may not stand: road corridors with their
    /// footpaths, building footprints and water areas; plus land rasters the placers choose from: forest (AREA
    /// FOREST and the forest biomes), parks and gardens, fields (farmland, orchards, cropland biomes) and river banks
    /// (cells next to water). Road cells come from the <see cref="IRoadCorridorQuery"/> when one is given (the same
    /// widened corridor the road mesher draws, docs/W2_DETAIL_CONTRACT.md §1.1), else from the
    /// <see cref="RoadLayout"/> widths, never narrower than the rideable corridor. Built once per tile; immutable after.
    /// </summary>
    public sealed class PlacementMask
    {
        public const double DefaultCellM = 4.0;

        public readonly double CellM;
        public readonly int N;
        private readonly byte[] _bits;

        public const byte Road = 1, Building = 2, Water = 4, Forest = 8, OsmTree = 16, Park = 32, Field = 64, Bank = 128;

        public PlacementMask(TileData t, double cellM = DefaultCellM) : this(t, null, cellM)
        {
        }

        /// <summary>The mask of a tile, its road cells from <paramref name="corridor"/> when given (tile-local metres).</summary>
        public PlacementMask(TileData t, IRoadCorridorQuery corridor, double cellM = DefaultCellM)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            CellM = cellM;
            N = Math.Max(1, (int)Math.Ceiling(t.Tile.Size / cellM));
            _bits = new byte[N * N];
            if (corridor != null) Corridor(corridor);
            else if (t.Roads.Count > 0) Roads(t);
            foreach (BuildingRecord b in t.Buildings) Ring(b.Rings[0], Building);
            foreach (AreaRecord a in t.Areas)
            {
                AreaFamily fam = AreaStyle.Family(a.Kind);
                if (fam == AreaFamily.Water) Triangles(a, Water);
                else if (a.Kind == AreaKind.Forest) Triangles(a, Forest);
                else if (a.Kind == AreaKind.Park || a.Kind == AreaKind.Cemetery || a.Kind == AreaKind.Courtyard) Triangles(a, Park);
                else if (a.Kind == AreaKind.Farmland || a.Kind == AreaKind.Orchard || a.Kind == AreaKind.TeaGarden) Triangles(a, Field);
            }
            if (t.Biomes != null && t.BiomesN > 1)
            {
                int bn = t.BiomesN;
                for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    double x = (i + 0.5) * cellM, z = (j + 0.5) * cellM;
                    int bi = (int)Math.Round(x / t.Tile.Size * (bn - 1)), bj = (int)Math.Round(z / t.Tile.Size * (bn - 1));
                    bi = bi < 0 ? 0 : bi >= bn ? bn - 1 : bi;
                    bj = bj < 0 ? 0 : bj >= bn ? bn - 1 : bj;
                    Biome b = t.Biomes[bj * bn + bi];
                    if (AreaTypeGrid.BiomeFamily(b) == 1) _bits[j * N + i] |= Forest;
                    else if (IsCropBiome(b)) _bits[j * N + i] |= Field;
                }
            }
            // River banks: dry cells next to water.
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                if ((_bits[j * N + i] & Water) != 0) continue;
                bool near = false;
                for (int dj = -1; dj <= 1 && !near; dj++)
                for (int di = -1; di <= 1 && !near; di++)
                {
                    int ii = i + di, jj = j + dj;
                    if (ii >= 0 && jj >= 0 && ii < N && jj < N && (_bits[jj * N + ii] & Water) != 0) near = true;
                }
                if (near) _bits[j * N + i] |= Bank;
            }
            foreach (PropRecord p in t.Props)
                if (p.Kind == ObjectKind.Tree) Disc(p.XCm / 100.0, p.ZCm / 100.0, 6.0, OsmTree);
        }

        /// <summary>Cropland biomes (paddy and terraces): where field furniture and terrace banding go.</summary>
        public static bool IsCropBiome(Biome b)
        {
            return b == Biome.ValleyCropland || b == Biome.HillTerraces || b == Biome.TeraiPaddy || b == Biome.TeraiCropland || b == Biome.TransHimalayanCropland;
        }

        /// <summary>The bits of the cell holding (x, z) in tile-local metres (<see cref="Road"/> off the tile).</summary>
        public byte At(double x, double z)
        {
            int i = (int)Math.Floor(x / CellM), j = (int)Math.Floor(z / CellM);
            if (i < 0 || j < 0 || i >= N || j >= N) return Road; // off the tile: never place
            return _bits[j * N + i];
        }

        /// <summary>True when the cell holds no road, building or water.</summary>
        public bool Free(double x, double z)
        {
            return (At(x, z) & (Road | Building | Water)) == 0;
        }

        private void Set(int i, int j, byte bit)
        {
            if (i < 0 || j < 0 || i >= N || j >= N) return;
            _bits[j * N + i] |= bit;
        }

        private void Disc(double x, double z, double r, byte bit)
        {
            int i0 = (int)Math.Floor((x - r) / CellM), i1 = (int)Math.Floor((x + r) / CellM);
            int j0 = (int)Math.Floor((z - r) / CellM), j1 = (int)Math.Floor((z + r) / CellM);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                // Every cell the disc touches (nearest point of the cell within r): conservative, so nothing
                // generated stands on the edge of a road.
                double nx = Math.Max(i * CellM, Math.Min(x, (i + 1) * CellM)) - x, nz = Math.Max(j * CellM, Math.Min(z, (j + 1) * CellM)) - z;
                if (nx * nx + nz * nz <= r * r) Set(i, j, bit);
            }
        }

        /// <summary>Road cells from the corridor query: a cell is road when any part of it may lie within the corridor
        /// (its centre closer to the corridor than its half diagonal).</summary>
        private void Corridor(IRoadCorridorQuery c)
        {
            double half = CellM * 0.7072;
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
                if (c.SignedDistance((i + 0.5) * CellM, (j + 0.5) * CellM) < half) _bits[j * N + i] |= Road;
        }

        private void Roads(TileData t)
        {
            RoadLayout layout = RoadLayout.For(t);
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                RoadRecord r = t.Roads[ri];
                if ((r.Flags & RoadFlags.Tunnel) != 0) continue;
                RoadWidthProfile prof = layout.Profiles[ri];
                double reach = 0.5 * prof.MaxWidth + Math.Max(Max(prof.FootLeft, prof.Count), Max(prof.FootRight, prof.Count)) + 0.5;
                // Never narrower than the rideable corridor (contract §1.1), whatever the layout says.
                reach = Math.Max(reach, 0.5 * RoadClearance.MinCorridorM + 0.5);
                int[] p = r.Points;
                for (int k = 0; k + 1 < p.Length / 2; k++)
                {
                    double ax = p[2 * k] / 100.0, az = p[2 * k + 1] / 100.0, bx = p[2 * k + 2] / 100.0, bz = p[2 * k + 3] / 100.0;
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    int steps = Math.Max(1, (int)Math.Ceiling(len / (0.5 * CellM)));
                    for (int q = 0; q <= steps; q++)
                    {
                        double f = (double)q / steps;
                        Disc(ax + (bx - ax) * f, az + (bz - az) * f, reach, Road);
                    }
                }
            }
        }

        private static float Max(float[] v, int n)
        {
            float m = 0;
            for (int i = 0; i < n; i++) m = Math.Max(m, v[i]);
            return m;
        }

        private void Ring(int[] ring, byte bit)
        {
            int n = ring.Length / 2;
            double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
            for (int k = 0; k < n; k++)
            {
                x0 = Math.Min(x0, ring[2 * k] / 100.0);
                z0 = Math.Min(z0, ring[2 * k + 1] / 100.0);
                x1 = Math.Max(x1, ring[2 * k] / 100.0);
                z1 = Math.Max(z1, ring[2 * k + 1] / 100.0);
            }
            int i0 = (int)Math.Floor(x0 / CellM), i1 = (int)Math.Floor(x1 / CellM), j0 = (int)Math.Floor(z0 / CellM), j1 = (int)Math.Floor(z1 / CellM);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                // A cell is blocked when its centre or any corner is inside (buildings must never get a tree).
                bool inside = false;
                for (int c = 0; c < 5 && !inside; c++)
                {
                    double px = (i + (c == 4 ? 0.5 : c % 2)) * CellM, pz = (j + (c == 4 ? 0.5 : c / 2)) * CellM;
                    inside = InRing(ring, px, pz);
                }
                if (inside) Set(i, j, bit);
            }
        }

        /// <summary>Point in polygon (even-odd) for a ring of centimetre pairs, at metres.</summary>
        internal static bool InRing(int[] ring, double x, double z)
        {
            int n = ring.Length / 2;
            bool inside = false;
            double xc = x * 100.0, zc = z * 100.0;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = ring[2 * i], zi = ring[2 * i + 1], xj = ring[2 * j], zj = ring[2 * j + 1];
                if ((zi > zc) != (zj > zc) && xc < (xj - xi) * (zc - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        private void Triangles(AreaRecord a, byte bit)
        {
            for (int t = 0; t + 2 < a.Indices.Length; t += 3)
            {
                double ax = a.Vertices[2 * a.Indices[t]] / 100.0, az = a.Vertices[2 * a.Indices[t] + 1] / 100.0;
                double bx = a.Vertices[2 * a.Indices[t + 1]] / 100.0, bz = a.Vertices[2 * a.Indices[t + 1] + 1] / 100.0;
                double cx = a.Vertices[2 * a.Indices[t + 2]] / 100.0, cz = a.Vertices[2 * a.Indices[t + 2] + 1] / 100.0;
                int i0 = (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)) / CellM), i1 = (int)Math.Floor(Math.Max(ax, Math.Max(bx, cx)) / CellM);
                int j0 = (int)Math.Floor(Math.Min(az, Math.Min(bz, cz)) / CellM), j1 = (int)Math.Floor(Math.Max(az, Math.Max(bz, cz)) / CellM);
                for (int j = Math.Max(0, j0); j <= Math.Min(N - 1, j1); j++)
                for (int i = Math.Max(0, i0); i <= Math.Min(N - 1, i1); i++)
                {
                    double px = (i + 0.5) * CellM, pz = (j + 0.5) * CellM;
                    double d1 = (bx - ax) * (pz - az) - (bz - az) * (px - ax), d2 = (cx - bx) * (pz - bz) - (cz - bz) * (px - bx), d3 = (ax - cx) * (pz - cz) - (az - cz) * (px - cx);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) Set(i, j, bit);
                }
            }
        }
    }
}
