using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Placement
{
    /// <summary>
    /// A tile-local raster (default 4 m cells) of where generated dressing may not stand: road ribbons with their
    /// footpaths (from <see cref="RoadLayout"/>, so the masks match the drawn widths), building footprints and water
    /// areas; plus a forest raster from AREA FOREST and the forest biomes. Built once per tile; immutable after.
    /// </summary>
    public sealed class PlacementMask
    {
        public const double DefaultCellM = 4.0;

        public readonly double CellM;
        public readonly int N;
        private readonly byte[] _bits;

        public const byte Road = 1, Building = 2, Water = 4, Forest = 8, OsmTree = 16;

        public PlacementMask(TileData t, double cellM = DefaultCellM)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            CellM = cellM;
            N = Math.Max(1, (int)Math.Ceiling(t.Tile.Size / cellM));
            _bits = new byte[N * N];
            if (t.Roads.Count > 0) Roads(t);
            foreach (BuildingRecord b in t.Buildings) Ring(b.Rings[0], Building);
            foreach (AreaRecord a in t.Areas)
            {
                AreaFamily fam = AreaStyle.Family(a.Kind);
                if (fam == AreaFamily.Water) Triangles(a, Water);
                else if (a.Kind == AreaKind.Forest) Triangles(a, Forest);
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
                    if (AreaTypeGrid.BiomeFamily(t.Biomes[bj * bn + bi]) == 1) _bits[j * N + i] |= Forest;
                }
            }
            foreach (PropRecord p in t.Props)
                if (p.Kind == ObjectKind.Tree) Disc(p.XCm / 100.0, p.ZCm / 100.0, 6.0, OsmTree);
        }

        public byte At(double x, double z)
        {
            int i = (int)Math.Floor(x / CellM), j = (int)Math.Floor(z / CellM);
            if (i < 0 || j < 0 || i >= N || j >= N) return Road; // off the tile: never place
            return _bits[j * N + i];
        }

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

        private void Roads(TileData t)
        {
            RoadLayout layout = RoadLayout.For(t);
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                RoadRecord r = t.Roads[ri];
                if ((r.Flags & RoadFlags.Tunnel) != 0) continue;
                RoadWidthProfile prof = layout.Profiles[ri];
                double reach = 0.5 * prof.MaxWidth + Math.Max(Max(prof.FootLeft, prof.Count), Max(prof.FootRight, prof.Count)) + 0.5;
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
                    inside = Meshing.BuildingBands.FootprintIndex.InRing(ring, px, pz);
                }
                if (inside) Set(i, j, bit);
            }
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
