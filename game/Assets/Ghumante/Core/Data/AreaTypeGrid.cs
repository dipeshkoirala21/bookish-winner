using System;
using System.Collections.Generic;

namespace Ghumante.Core.Data
{
    /// <summary>
    /// Area type at any game point on the 250 m grid of street_life S 1.1 (W2_DESIGN 10.3): a majority vote of the
    /// BFNT and RATR area types inside each cell. Tiles without those chunks (W1 packs) contribute land statistics
    /// instead (building count, built cover, Newar share, biome), classified by <see cref="Classify"/>. Ambience,
    /// spawns and pedestrians use it in stage 1; the D21 LIFE raster replaces it in stage 2. Cells straddling tiles
    /// sum the contributions of every resident tile, so a result can change while neighbours stream in. Thread-safe
    /// (one lock); <see cref="AddTile"/> does the work, the queries are dictionary lookups.
    /// </summary>
    public sealed class AreaTypeGrid
    {
        public const double CellM = 250.0;
        public const int TypeCount = 7;

        private sealed class Cell
        {
            public readonly int[] Votes = new int[TypeCount];
            public double BuiltM2;
            public double CoveredM2;
            public int Buildings;
            public int Newar;
            public int BiomeForest, BiomeHill, BiomeOpen;
        }

        private struct Contribution
        {
            public long Key;
            public int[] Votes;
            public double BuiltM2, CoveredM2;
            public int Buildings, Newar, BiomeForest, BiomeHill, BiomeOpen;
        }

        private readonly object _lock = new object();
        private readonly Dictionary<long, Cell> _cells = new Dictionary<long, Cell>();
        private readonly Dictionary<ulong, List<Contribution>> _byTile = new Dictionary<ulong, List<Contribution>>();

        private static long Key(long cx, long cz)
        {
            return cx << 32 ^ (cz & 0xFFFFFFFFL);
        }

        public int CellCount
        {
            get
            {
                lock (_lock) return _cells.Count;
            }
        }

        /// <summary>Add a tile's votes (replacing an earlier add of the same tile). Tiles without buildings, roads or
        /// biomes add nothing.</summary>
        public void AddTile(TileId id, TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var contrib = new Dictionary<long, Contribution>();
            double x0 = id.X0, z0 = id.Z0;
            bool votes = t.HasBuildingFronts || t.HasRoadAttrs;
            if (t.HasBuildingFronts)
            {
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    AreaType a = t.BuildingFronts[i].Area;
                    if (a == AreaType.Unknown) continue;
                    double cx, cz;
                    RingCentre(t.Buildings[i].Rings[0], out cx, out cz);
                    Vote(contrib, x0 + cx, z0 + cz, a, 1);
                }
            }
            if (t.HasRoadAttrs)
            {
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    AreaType a = t.RoadAttrs[i].Area;
                    if (a == AreaType.Unknown) continue;
                    int[] p = t.Roads[i].Points;
                    int mid = p.Length / 4;
                    Vote(contrib, x0 + p[2 * mid] / 100.0, z0 + p[2 * mid + 1] / 100.0, a, 1);
                }
            }
            if (!votes) AddStatistics(t, contrib);
            var list = new List<Contribution>(contrib.Values);
            lock (_lock)
            {
                RemoveLocked(id.Key);
                _byTile[id.Key] = list;
                foreach (Contribution c in list) Apply(c, +1);
            }
        }

        public void RemoveTile(TileId id)
        {
            lock (_lock) RemoveLocked(id.Key);
        }

        private void RemoveLocked(ulong key)
        {
            List<Contribution> old;
            if (!_byTile.TryGetValue(key, out old)) return;
            foreach (Contribution c in old) Apply(c, -1);
            _byTile.Remove(key);
        }

        private void Apply(Contribution c, int sign)
        {
            Cell cell;
            if (!_cells.TryGetValue(c.Key, out cell))
            {
                if (sign < 0) return;
                cell = new Cell();
                _cells[c.Key] = cell;
            }
            if (c.Votes != null)
                for (int k = 0; k < TypeCount; k++) cell.Votes[k] += sign * c.Votes[k];
            cell.BuiltM2 += sign * c.BuiltM2;
            cell.CoveredM2 += sign * c.CoveredM2;
            cell.Buildings += sign * c.Buildings;
            cell.Newar += sign * c.Newar;
            cell.BiomeForest += sign * c.BiomeForest;
            cell.BiomeHill += sign * c.BiomeHill;
            cell.BiomeOpen += sign * c.BiomeOpen;
            if (sign < 0 && cell.CoveredM2 <= 1e-6 && Total(cell.Votes) == 0) _cells.Remove(c.Key);
        }

        private static int Total(int[] v)
        {
            int s = 0;
            for (int k = 0; k < v.Length; k++) s += v[k];
            return s;
        }

        private static void Vote(Dictionary<long, Contribution> contrib, double x, double z, AreaType a, int n)
        {
            long key = Key((long)Math.Floor(x / CellM), (long)Math.Floor(z / CellM));
            Contribution c;
            if (!contrib.TryGetValue(key, out c)) c = new Contribution { Key = key };
            if (c.Votes == null) c.Votes = new int[TypeCount];
            c.Votes[(int)a] += n;
            contrib[key] = c;
        }

        private static void RingCentre(int[] ring, out double cx, out double cz)
        {
            double sx = 0, sz = 0;
            int n = ring.Length / 2;
            for (int i = 0; i < n; i++)
            {
                sx += ring[2 * i];
                sz += ring[2 * i + 1];
            }
            cx = sx / n / 100.0;
            cz = sz / n / 100.0;
        }

        /// <summary>Signed area of a ring in m² (positive counter-clockwise).</summary>
        internal static double RingArea(int[] ring)
        {
            double a = 0;
            int n = ring.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
                a += (double)ring[2 * j] * ring[2 * i + 1] - (double)ring[2 * i] * ring[2 * j + 1];
            return a * 0.5 / 10000.0;
        }

        /// <summary>W1 fallback: building and biome statistics per 250 m cell (cells clipped to the tile).</summary>
        private static void AddStatistics(TileData t, Dictionary<long, Contribution> contrib)
        {
            if (!t.HasDetail) return; // only leaf tiles carry buildings; coarser levels would double-count
            double x0 = t.Tile.X0, z0 = t.Tile.Z0, size = t.Tile.Size;
            long cx0 = (long)Math.Floor(x0 / CellM), cz0 = (long)Math.Floor(z0 / CellM);
            long cx1 = (long)Math.Floor((x0 + size - 1e-6) / CellM), cz1 = (long)Math.Floor((z0 + size - 1e-6) / CellM);
            for (long cz = cz0; cz <= cz1; cz++)
            for (long cx = cx0; cx <= cx1; cx++)
            {
                double ax = Math.Max(x0, cx * CellM), bx = Math.Min(x0 + size, (cx + 1) * CellM);
                double az = Math.Max(z0, cz * CellM), bz = Math.Min(z0 + size, (cz + 1) * CellM);
                var c = new Contribution { Key = Key(cx, cz), CoveredM2 = Math.Max(0, bx - ax) * Math.Max(0, bz - az) };
                if (t.Biomes != null && t.BiomesN > 1)
                {
                    // 3 × 3 biome samples per clipped cell.
                    for (int j = 0; j < 3; j++)
                    for (int i = 0; i < 3; i++)
                    {
                        double x = ax + (bx - ax) * (i + 0.5) / 3, z = az + (bz - az) * (j + 0.5) / 3;
                        switch (BiomeFamily(BiomeAt(t, x, z)))
                        {
                            case 1: c.BiomeForest++; break;
                            case 2: c.BiomeHill++; break;
                            default: c.BiomeOpen++; break;
                        }
                    }
                }
                contrib[c.Key] = c;
            }
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                BuildingRecord b = t.Buildings[i];
                double cx, cz;
                RingCentre(b.Rings[0], out cx, out cz);
                long key = Key((long)Math.Floor((x0 + cx) / CellM), (long)Math.Floor((z0 + cz) / CellM));
                Contribution c;
                if (!contrib.TryGetValue(key, out c)) continue;
                c.Buildings++;
                if (b.Archetype == BuildingArchetype.Newar) c.Newar++;
                c.BuiltM2 += Math.Abs(RingArea(b.Rings[0]));
                contrib[key] = c;
            }
        }

        private static Biome BiomeAt(TileData t, double x, double z)
        {
            int n = t.BiomesN;
            double fx = (x - t.Tile.X0) / t.Tile.Size * (n - 1), fz = (z - t.Tile.Z0) / t.Tile.Size * (n - 1);
            int i = (int)Math.Round(fx), j = (int)Math.Round(fz);
            i = i < 0 ? 0 : i >= n ? n - 1 : i;
            j = j < 0 ? 0 : j >= n ? n - 1 : j;
            return t.Biomes[j * n + i];
        }

        /// <summary>0 open land, 1 forest, 2 hill country.</summary>
        internal static int BiomeFamily(Biome b)
        {
            switch (b)
            {
                case Biome.HillForest:
                case Biome.SubalpineForest:
                case Biome.TeraiSalForest:
                case Biome.ChureForest: return 1;
                case Biome.HillTerraces:
                case Biome.HillScrub:
                case Biome.HillGrassland:
                case Biome.AlpineMeadow:
                case Biome.AlpineScrub:
                case Biome.ScreeRock: return 2;
                default: return 0;
            }
        }

        /// <summary>
        /// The classification of one patch of land when no BFNT/RATR votes exist, with the pipeline's thresholds
        /// (DATA_FORMATS 1.11, areatype.py): OLD_CORE at building cover ≥ 0.45 with ≥ 180 buildings per 250 m cell
        /// (28.8 per hectare), or inside a curated core (stood in for by a Newar majority at cover ≥ 0.30, since W1
        /// packs mark the curated cores' buildings NEWAR); URBAN at cover ≥ 0.22; PERI_URBAN at ≥ 0.06; otherwise
        /// FOREST, HILL or RURAL by the biome majority.
        /// </summary>
        public static AreaType Classify(double areaM2, double builtM2, int buildings, int newar, int forest, int hill, int open)
        {
            if (areaM2 <= 0) return AreaType.Unknown;
            double cover = builtM2 / areaM2, perHa = buildings / (areaM2 / 10000.0);
            if (cover >= 0.45 && perHa >= 28.8) return AreaType.OldCore;
            if (cover >= 0.30 && buildings >= 8 && newar * 2 >= buildings) return AreaType.OldCore;
            if (cover >= 0.22) return AreaType.Urban;
            if (cover >= 0.06) return AreaType.PeriUrban;
            if (forest + hill + open == 0) return AreaType.Rural;
            if (forest >= hill && forest >= open) return AreaType.Forest;
            return hill > open ? AreaType.Hill : AreaType.Rural;
        }

        private static AreaType Resolve(Cell c)
        {
            int best = 0, bestN = 0;
            for (int k = 1; k < TypeCount; k++)
                if (c.Votes[k] > bestN)
                {
                    best = k;
                    bestN = c.Votes[k];
                }
            if (bestN > 0) return (AreaType)best;
            return Classify(c.CoveredM2, c.BuiltM2, c.Buildings, c.Newar, c.BiomeForest, c.BiomeHill, c.BiomeOpen);
        }

        /// <summary>The area type of the cell containing (x, z); Unknown where no resident tile covers it.</summary>
        public AreaType At(double x, double z)
        {
            long key = Key((long)Math.Floor(x / CellM), (long)Math.Floor(z / CellM));
            lock (_lock)
            {
                Cell c;
                return _cells.TryGetValue(key, out c) ? Resolve(c) : AreaType.Unknown;
            }
        }

        /// <summary>Bilinear weights of the four nearest cell centres, accumulated per area type (index =
        /// <see cref="AreaType"/> value; <paramref name="perAreaType"/> needs <see cref="TypeCount"/> entries). The
        /// weights sum to 1 where data exists; cells without data add to Unknown.</summary>
        public void Weights(double x, double z, Span<float> perAreaType)
        {
            if (perAreaType.Length < TypeCount) throw new ArgumentException("needs " + TypeCount + " entries");
            for (int k = 0; k < TypeCount; k++) perAreaType[k] = 0f;
            double fx = x / CellM - 0.5, fz = z / CellM - 0.5;
            long ix = (long)Math.Floor(fx), iz = (long)Math.Floor(fz);
            float tx = (float)(fx - ix), tz = (float)(fz - iz);
            lock (_lock)
            {
                for (int dz = 0; dz < 2; dz++)
                for (int dx = 0; dx < 2; dx++)
                {
                    float w = (dx == 0 ? 1 - tx : tx) * (dz == 0 ? 1 - tz : tz);
                    Cell c;
                    AreaType a = _cells.TryGetValue(Key(ix + dx, iz + dz), out c) ? Resolve(c) : AreaType.Unknown;
                    perAreaType[(int)a] += w;
                }
            }
        }

        /// <summary>
        /// Tile-local classification on a <paramref name="cells"/> × <paramref name="cells"/> grid (row-major, row 0
        /// south) from the tile's own BFNT votes, else its building and biome statistics. Pure, so meshers can use it
        /// on worker threads: <see cref="Ghumante.Core.Meshing.RoadMesher"/> takes the area type of pieces without
        /// RATR from it.
        /// </summary>
        public static AreaType[] ClassifyTile(TileData t, int cells)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var result = new AreaType[cells * cells];
            double size = t.Tile.Size, cell = size / cells, cellArea = cell * cell;
            var votes = new int[cells * cells * TypeCount];
            var built = new double[cells * cells];
            var count = new int[cells * cells];
            var newar = new int[cells * cells];
            bool fronts = t.HasBuildingFronts;
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                BuildingRecord b = t.Buildings[i];
                double cx, cz;
                RingCentre(b.Rings[0], out cx, out cz);
                int ci = (int)(cx / cell), cj = (int)(cz / cell);
                if (ci < 0 || cj < 0 || ci >= cells || cj >= cells) continue;
                int k = cj * cells + ci;
                count[k]++;
                if (b.Archetype == BuildingArchetype.Newar) newar[k]++;
                built[k] += Math.Abs(RingArea(b.Rings[0]));
                if (fronts && t.BuildingFronts[i].Area != AreaType.Unknown) votes[k * TypeCount + (int)t.BuildingFronts[i].Area]++;
            }
            for (int j = 0; j < cells; j++)
            for (int i = 0; i < cells; i++)
            {
                int k = j * cells + i;
                int best = 0, bestN = 0;
                for (int a = 1; a < TypeCount; a++)
                    if (votes[k * TypeCount + a] > bestN)
                    {
                        best = a;
                        bestN = votes[k * TypeCount + a];
                    }
                if (bestN > 0)
                {
                    result[k] = (AreaType)best;
                    continue;
                }
                int forest = 0, hill = 0, open = 0;
                if (t.Biomes != null && t.BiomesN > 1)
                {
                    double x = t.Tile.X0 + (i + 0.5) * cell, z = t.Tile.Z0 + (j + 0.5) * cell;
                    switch (BiomeFamily(BiomeAt(t, x, z)))
                    {
                        case 1: forest = 1; break;
                        case 2: hill = 1; break;
                        default: open = 1; break;
                    }
                }
                result[k] = Classify(cellArea, built[k], count[k], newar[k], forest, hill, open);
            }
            return result;
        }
    }
}
