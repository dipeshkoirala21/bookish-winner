using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;

namespace Ghumante.World.Buildings
{
    /// <summary>
    /// Which houses a B0 cell draws (W2_DESIGN 2.4; the detail pass band table): per building of a tile, the bounding box
    /// of its B0 geometry (the footprint's bounding box grown by <see cref="OverhangM"/>, the real reach of eaves and
    /// balconies), its centroid and whether it fronts a street; per cell of both B0 grids (64 m cells and the 32 m
    /// cells of <see cref="BuildingBandTable.NearSubdivision"/>, <see cref="BuildingDetailMesher.Cells(TileData, int)"/>)
    /// its buildings and the bounds of what they draw. A cell is built only with the houses whose geometry meets B0 and
    /// its 4 m fade into B1 (<see cref="Select"/>), never with a whole cell beyond it. Built once per tile on a worker
    /// (<see cref="Prepare"/>: a few milliseconds on the densest tile, the street fronts included) and cached with it.
    /// Engine-free and immutable once built; the queries are allocation-free (B0CellSet and the budget tests share them).
    /// </summary>
    public sealed class B0HouseIndex
    {
        /// <summary>
        /// How far B0 geometry reaches beyond a building's footprint bounding box, horizontally: eaves (1.0 to 1.2 m),
        /// sanjhya and balconies, struts and shop shutters. Measured at most 2.03 m on every building within 150 m of
        /// Asan at the richest near level (core-tests MeshingBuildingB0CellTests); the old cell margin was 24 m.
        /// </summary>
        public const float OverhangM = 2.5f;

        /// <summary>A house fronts a street when the road its front edge faces lies within this distance (the street
        /// row; BFNT front distance). The others stand behind it, mostly hidden from the street.</summary>
        public const float StreetFrontM = 8f;

        private static readonly ConditionalWeakTable<TileData, B0HouseIndex> Cache = new ConditionalWeakTable<TileData, B0HouseIndex>();

        private readonly float[] _bounds, _centroids;
        private readonly bool[] _street;
        private readonly int[][] _fine, _coarse;
        private readonly float[] _fineCells, _coarseCells;
        private readonly int _finePer, _coarsePer;
        private readonly double _fineSide, _coarseSide, _fineSpill, _coarseSpill;

        private B0HouseIndex(TileData t)
        {
            int n = t.Buildings.Count;
            _bounds = new float[4 * n];
            _centroids = new float[2 * n];
            _street = new bool[n];
            BuildingFronts fronts = BuildingFronts.For(t);
            for (int i = 0; i < n; i++)
            {
                int[] r = t.Buildings[i].Rings[0];
                int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
                long sx = 0, sz = 0;
                int np = r.Length / 2;
                for (int k = 0; k < np; k++)
                {
                    int x = r[2 * k], z = r[2 * k + 1];
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (z < minZ) minZ = z;
                    if (z > maxZ) maxZ = z;
                    sx += x;
                    sz += z;
                }
                if (np == 0) minX = maxX = minZ = maxZ = 0;
                _bounds[4 * i] = minX / 100f - OverhangM;
                _bounds[4 * i + 1] = minZ / 100f - OverhangM;
                _bounds[4 * i + 2] = maxX / 100f + OverhangM;
                _bounds[4 * i + 3] = maxZ / 100f + OverhangM;
                _centroids[2 * i] = np > 0 ? (float)(sx / (double)np / 100.0) : 0f;
                _centroids[2 * i + 1] = np > 0 ? (float)(sz / (double)np / 100.0) : 0f;
                BuildingFrontRecord f = fronts.Front(i);
                _street[i] = f.HasFront && f.FrontDistDm <= StreetFrontM * 10f;
            }
            _coarse = BuildingDetailMesher.Cells(t, 1);
            _fine = BuildingDetailMesher.Cells(t, BuildingBandTable.NearSubdivision);
            _coarsePer = BuildingDetailMesher.CellsPerSide(t, 1);
            _finePer = BuildingDetailMesher.CellsPerSide(t, BuildingBandTable.NearSubdivision);
            _coarseSide = t.Tile.Size / _coarsePer;
            _fineSide = t.Tile.Size / _finePer;
            _coarseCells = CellBounds(_coarse);
            _fineCells = CellBounds(_fine);
            _coarseSpill = Spill(_coarseCells, _coarsePer, _coarseSide);
            _fineSpill = Spill(_fineCells, _finePer, _fineSide);
        }

        private float[] CellBounds(int[][] cells)
        {
            var b = new float[4 * cells.Length];
            for (int c = 0; c < cells.Length; c++)
            {
                float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
                foreach (int i in cells[c])
                {
                    x0 = Math.Min(x0, _bounds[4 * i]);
                    z0 = Math.Min(z0, _bounds[4 * i + 1]);
                    x1 = Math.Max(x1, _bounds[4 * i + 2]);
                    z1 = Math.Max(z1, _bounds[4 * i + 3]);
                }
                b[4 * c] = x0;
                b[4 * c + 1] = z0;
                b[4 * c + 2] = x1;
                b[4 * c + 3] = z1;
            }
            return b;
        }

        private static double Spill(float[] cells, int per, double side)
        {
            double spill = 0;
            for (int c = 0; c < per * per; c++)
            {
                if (cells[4 * c] > cells[4 * c + 2]) continue;
                double x0 = (c % per) * side, z0 = (c / per) * side;
                spill = Math.Max(spill, Math.Max(Math.Max(x0 - cells[4 * c], cells[4 * c + 2] - (x0 + side)),
                                                 Math.Max(z0 - cells[4 * c + 1], cells[4 * c + 3] - (z0 + side))));
            }
            return spill;
        }

        /// <summary>The tile's index, built on the calling thread when it is not cached yet (worker threads).</summary>
        public static B0HouseIndex Prepare(TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return Cache.GetValue(t, k => new B0HouseIndex(k));
        }

        /// <summary>The tile's index when it is cached (never builds it).</summary>
        public static bool TryGet(TileData t, out B0HouseIndex index)
        {
            if (t == null)
            {
                index = null;
                return false;
            }
            return Cache.TryGetValue(t, out index);
        }

        /// <summary>Cells per tile side of the fine (32 m) or coarse (64 m) grid.</summary>
        public int PerSide(bool fine)
        {
            return fine ? _finePer : _coarsePer;
        }

        /// <summary>Side of a fine or coarse cell (m).</summary>
        public double CellSide(bool fine)
        {
            return fine ? _fineSide : _coarseSide;
        }

        /// <summary>How far the content of a cell reaches beyond the cell (m; a long building whose centroid lies in the
        /// cell, plus <see cref="OverhangM"/>): the cells within a radius plus this can draw inside it.</summary>
        public double Spill(bool fine)
        {
            return fine ? _fineSpill : _coarseSpill;
        }

        /// <summary>The building indices of a cell (their outer-ring centroids lie in it), in a fixed order: the positions
        /// <see cref="Select"/> returns index this array.</summary>
        public int[] Houses(bool fine, int ix, int iz)
        {
            int per = fine ? _finePer : _coarsePer;
            return (fine ? _fine : _coarse)[iz * per + ix];
        }

        /// <summary>Horizontal bounds (tile-local m) of building <paramref name="i"/>'s B0 geometry.</summary>
        public void HouseBounds(int i, out float minX, out float minZ, out float maxX, out float maxZ)
        {
            minX = _bounds[4 * i];
            minZ = _bounds[4 * i + 1];
            maxX = _bounds[4 * i + 2];
            maxZ = _bounds[4 * i + 3];
        }

        /// <summary>Horizontal distance (m) from (cx, cz) to building <paramref name="i"/>'s outer-ring centroid: what
        /// decides the level a house is drawn at.</summary>
        public double CentroidDistance(int i, double cx, double cz)
        {
            double dx = _centroids[2 * i] - cx, dz = _centroids[2 * i + 1] - cz;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Horizontal distance (m) from (cx, cz) to the nearest point of building <paramref name="i"/>'s B0 geometry
        /// (0 inside it).</summary>
        public double Distance(int i, double cx, double cz)
        {
            return BandConfig.NearestDistance(_bounds[4 * i], _bounds[4 * i + 1], _bounds[4 * i + 2], _bounds[4 * i + 3], cx, cz);
        }

        /// <summary>
        /// The B1 blocks (<see cref="BandConfig.B1BlockM"/>, <paramref name="perSide"/> per tile side, clamped at the tile's
        /// edge as MeshParts.BlockOf clamps) that horizontal bounds [x0, x1] × [z0, z1] (tile-local) touch: every block a B1
        /// triangle of a house within them can fall in (MeshParts.ByBlocks assigns triangles by their centroid).
        /// </summary>
        public static void Blocks(double x0, double z0, double x1, double z1, int perSide, out int kx0, out int kz0, out int kx1, out int kz1)
        {
            kx0 = ClampBlock(Math.Floor(x0 / BandConfig.B1BlockM), perSide);
            kz0 = ClampBlock(Math.Floor(z0 / BandConfig.B1BlockM), perSide);
            kx1 = ClampBlock(Math.Floor(x1 / BandConfig.B1BlockM), perSide);
            kz1 = ClampBlock(Math.Floor(z1 / BandConfig.B1BlockM), perSide);
        }

        private static int ClampBlock(double k, int perSide)
        {
            return k < 0 ? 0 : k >= perSide ? perSide - 1 : (int)k;
        }

        /// <summary>The B1 blocks building <paramref name="i"/>'s B0 geometry touches (<see cref="Blocks"/>).</summary>
        public void HouseBlocks(int i, int perSide, out int kx0, out int kz0, out int kx1, out int kz1)
        {
            Blocks(_bounds[4 * i], _bounds[4 * i + 1], _bounds[4 * i + 2], _bounds[4 * i + 3], perSide, out kx0, out kz0, out kx1, out kz1);
        }

        /// <summary>True when building <paramref name="i"/> fronts a street (<see cref="StreetFrontM"/>).</summary>
        public bool Street(int i)
        {
            return _street[i];
        }

        /// <summary>Bounds of everything a cell can draw; false for a cell without buildings.</summary>
        public bool CellBounds(bool fine, int ix, int iz, out float minX, out float minZ, out float maxX, out float maxZ)
        {
            int per = fine ? _finePer : _coarsePer;
            float[] b = fine ? _fineCells : _coarseCells;
            int c = 4 * (iz * per + ix);
            minX = b[c];
            minZ = b[c + 1];
            maxX = b[c + 2];
            maxZ = b[c + 3];
            return minX <= maxX;
        }

        /// <summary>
        /// True when house <paramref name="i"/>'s geometry comes within <paramref name="outer"/> + <paramref name="padM"/>
        /// plus half the 4 m fade of (cx, cz), tile-local (<see cref="BandConfig.Touches"/>): with a pad of 0 the house
        /// has fragments B0 draws; with a pad of -<see cref="BandConfig.FadeM"/> it has fragments that only B0 draws
        /// (nearer than the fade into B1).
        /// </summary>
        public bool Meets(int i, float outer, double cx, double cz, double padM)
        {
            return BandConfig.Touches(_bounds[4 * i], _bounds[4 * i + 1], _bounds[4 * i + 2], _bounds[4 * i + 3], cx, cz, 0f, outer + (float)padM);
        }

        /// <summary>True when the content of a cell can come within <paramref name="outer"/> + pad (the quick reject).</summary>
        public bool CellMeets(bool fine, int ix, int iz, float outer, double cx, double cz, double padM)
        {
            float x0, z0, x1, z1;
            if (!CellBounds(fine, ix, iz, out x0, out z0, out x1, out z1)) return false;
            return BandConfig.Touches(x0, z0, x1, z1, cx, cz, 0f, outer + (float)padM);
        }

        /// <summary>
        /// The houses of a cell that <see cref="Meets"/> B0 (radius <paramref name="outer"/>, pad <paramref name="padM"/>)
        /// for a camera at (cx, cz), tile-local: their positions in <see cref="Houses"/> appended to <paramref name="into"/>
        /// (null: only counted). Returns the count. Allocation-free.
        /// </summary>
        public int Select(bool fine, int ix, int iz, float outer, double cx, double cz, double padM, List<int> into)
        {
            if (!CellMeets(fine, ix, iz, outer, cx, cz, padM)) return 0;
            int[] houses = Houses(fine, ix, iz);
            int n = 0;
            for (int p = 0; p < houses.Length; p++)
            {
                if (!Meets(houses[p], outer, cx, cz, padM)) continue;
                into?.Add(p);
                n++;
            }
            return n;
        }

        /// <summary>True when every house of the cell that <see cref="Meets"/> B0 with <paramref name="padM"/> is set in
        /// <paramref name="built"/> (a bit per position in <see cref="Houses"/>).</summary>
        public bool Covers(bool fine, int ix, int iz, float outer, double cx, double cz, double padM, ulong[] built)
        {
            if (!CellMeets(fine, ix, iz, outer, cx, cz, padM)) return true;
            int[] houses = Houses(fine, ix, iz);
            for (int p = 0; p < houses.Length; p++)
            {
                if (built != null && p >> 6 < built.Length && (built[p >> 6] >> (p & 63) & 1UL) != 0) continue;
                if (Meets(houses[p], outer, cx, cz, padM)) return false;
            }
            return true;
        }

        /// <summary>Houses set in <paramref name="built"/> that no longer meet B0 with <paramref name="padM"/>.</summary>
        public int Unneeded(bool fine, int ix, int iz, float outer, double cx, double cz, double padM, ulong[] built)
        {
            if (built == null) return 0;
            int[] houses = Houses(fine, ix, iz);
            int n = 0;
            for (int p = 0; p < houses.Length; p++)
                if (p >> 6 < built.Length && (built[p >> 6] >> (p & 63) & 1UL) != 0 && !Meets(houses[p], outer, cx, cz, padM)) n++;
            return n;
        }
    }
}
