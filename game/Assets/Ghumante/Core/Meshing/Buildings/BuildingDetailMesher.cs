using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// B0 "kit-lite" buildings per detail cell (W2_DESIGN 2.4, 10.3): every building whose outer-ring centroid lies in
    /// the cell gets the house grammar (<see cref="HouseBuilder"/>), the generic sacred generators
    /// (<see cref="SacredSelector"/>, LOD0) for temples, stupas and shrines, and the styled B1 extrusion for the other
    /// archetypes. Houses are held to <see cref="BuildingOptions.B0CapTris"/> per plot from
    /// <see cref="BuildingOptions.B0BaseDrop"/> (the band table's near ring), or drawn at the tier's lite level (its outer
    /// ring: the flat level on Low; <see cref="BuildingBandTable.LiteOptions(int, BuildingOptions)"/>).
    /// Cells are 64 m, or 32 m for the near ring (<see cref="BuildingBandTable.NearSubdivision"/>). Colliders (pikha
    /// aprons, flat roofs, plinths, temple steps) go to the collider set when given. Deterministic; positions relative to
    /// the tile's south-west corner. Thread-safe for distinct meshes.
    /// </summary>
    public static class BuildingDetailMesher
    {
        /// <summary>Side of a detail cell.</summary>
        public const double CellM = 64.0;

        /// <summary>Detail cells per tile side.</summary>
        public static int CellsPerSide(TileData t)
        {
            return Math.Max(1, (int)Math.Round(t.Tile.Size / CellM));
        }

        /// <summary>Cells per tile side when every detail cell is split <paramref name="subdivision"/> times per side (the
        /// near B0 ring uses <see cref="BuildingBandTable.NearSubdivision"/>; 1 = the 64 m cells).</summary>
        public static int CellsPerSide(TileData t, int subdivision)
        {
            return CellsPerSide(t) * Math.Max(1, subdivision);
        }

        /// <summary>The detail cell (column, row) holding a building's outer-ring centroid.</summary>
        public static void CellOf(TileData t, int buildingIndex, out int cellX, out int cellZ)
        {
            CellOf(t, buildingIndex, 1, out cellX, out cellZ);
        }

        /// <summary>The cell (column, row) of a subdivided grid holding a building's outer-ring centroid.</summary>
        public static void CellOf(TileData t, int buildingIndex, int subdivision, out int cellX, out int cellZ)
        {
            int[] r = t.Buildings[buildingIndex].Rings[0];
            int n = r.Length / 2;
            double cx = 0, cz = 0;
            for (int k = 0; k < n; k++)
            {
                cx += r[2 * k];
                cz += r[2 * k + 1];
            }
            int cells = CellsPerSide(t, subdivision);
            double side = t.Tile.Size / cells;
            cellX = Math.Max(0, Math.Min(cells - 1, (int)Math.Floor(cx / n / 100.0 / side)));
            cellZ = Math.Max(0, Math.Min(cells - 1, (int)Math.Floor(cz / n / 100.0 / side)));
        }

        private sealed class CellLists
        {
            public int[][] One, Near;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TileData, CellLists> CellIndex =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TileData, CellLists>();

        /// <summary>The building indices of each detail cell (row-major, row 0 south), built once per tile.</summary>
        public static int[][] Cells(TileData t)
        {
            return Cells(t, 1);
        }

        /// <summary>The building indices of each cell of a subdivided grid (row-major, row 0 south), built once per tile
        /// and subdivision (1 and <see cref="BuildingBandTable.NearSubdivision"/> are cached).</summary>
        public static int[][] Cells(TileData t, int subdivision)
        {
            subdivision = Math.Max(1, subdivision);
            CellLists l = CellIndex.GetValue(t, k => new CellLists());
            lock (l)
            {
                if (subdivision == 1) return l.One ?? (l.One = Index(t, 1));
                if (subdivision == BuildingBandTable.NearSubdivision) return l.Near ?? (l.Near = Index(t, subdivision));
            }
            return Index(t, subdivision);
        }

        private static int[][] Index(TileData t, int subdivision)
        {
            int n = CellsPerSide(t, subdivision);
            var lists = new System.Collections.Generic.List<int>[n * n];
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                int cx, cz;
                CellOf(t, i, subdivision, out cx, out cz);
                int c = cz * n + cx;
                if (lists[c] == null) lists[c] = new System.Collections.Generic.List<int>();
                lists[c].Add(i);
            }
            var a = new int[n * n][];
            for (int c = 0; c < a.Length; c++) a[c] = lists[c] == null ? new int[0] : lists[c].ToArray();
            return a;
        }

        /// <summary>Build the B0 meshes of one detail cell; returns the number of buildings drawn.</summary>
        public static int BuildCell(TileData t, IHeightSampler h, int cellX, int cellZ, BuildingOptions o, MeshData m, GenColliders c)
        {
            return BuildCell(t, h, cellX, cellZ, 1, o, m, c);
        }

        /// <summary>Build the B0 meshes of one cell of a subdivided grid (the near ring's 32 m cells, or the 64 m cells for
        /// a subdivision of 1); returns the number of buildings drawn.</summary>
        public static int BuildCell(TileData t, IHeightSampler h, int cellX, int cellZ, int subdivision, BuildingOptions o, MeshData m, GenColliders c)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (o == null) o = new BuildingOptions();
            int n = CellsPerSide(t, subdivision);
            if (cellX < 0 || cellZ < 0 || cellX >= n || cellZ >= n) return 0;
            int drawn = 0;
            foreach (int i in Cells(t, subdivision)[cellZ * n + cellX])
                if (One(t, i, h, o, m, c)) drawn++;
            return drawn;
        }

        /// <summary>B0 for the whole tile (tests and previews; streaming builds cells).</summary>
        public static int BuildTile(TileData t, IHeightSampler h, BuildingOptions o, MeshData m, GenColliders c)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (o == null) o = new BuildingOptions();
            int drawn = 0;
            for (int i = 0; i < t.Buildings.Count; i++)
                if (One(t, i, h, o, m, c)) drawn++;
            return drawn;
        }

        /// <summary>One building at B0. Footprints in a road corridor are trimmed (or the building is skipped when it
        /// stands in the road, <see cref="BuildingFootprints"/>); houses are held to <see cref="BuildingOptions.B0CapTris"/>
        /// <b>per plot</b> (<see cref="HouseBudget"/>): each plot of a row starts at the drop level its size predicts and
        /// only a plot that still overflows is rebuilt lighter; the lightest level is always kept, so a house inside the
        /// B0 band is never a bare box. Archetypes without a facade grammar use the styled B1 extrusion.</summary>
        public static bool One(TileData t, int i, IHeightSampler h, BuildingOptions o, MeshData m, GenColliders c)
        {
            if (o == null) o = new BuildingOptions();
            IRoadCorridorQuery q = o.CorridorsFor(t);
            BuildingFootprints guard = BuildingFootprints.For(t, q);
            if (guard.Dropped(i)) return false;
            BuildingRecord b = guard.Record(i);
            if (o.SkipLandmarks && (b.Flags & BuildingFlags.Landmark) != 0) return false;
            if (o.HiddenRefs != null && o.HiddenRefs.Contains(b.OsmRef)) return false;
            if (SacredSelector.HostOf(t, i) >= 0) return false; // a part of a generic sacred outline: the host draws it
            if ((b.Flags & BuildingFlags.HasParts) != 0 && !SacredSelector.DrawsGeneric(b)) return false; // its parts are drawn instead
            HousePlan plan = guard.Adjust(i, BuildingGrammar.Plan(t, i));
            if ((b.Flags & BuildingFlags.Part) != 0) return BuildingMesher.Styled(t, b, h, o, plan, m);
            if (plan.Sacred)
            {
                int vs = m.VertexCount;
                if (SacredSelector.BuildGeneric(t, i, h, 0, m, c))
                {
                    KitPaint.FillUnset(m, vs);
                    return true;
                }
                return BuildingMesher.Styled(t, b, h, o, plan, m);
            }
            if (!BuildingGrammar.IsHouse(plan.Archetype)) return BuildingMesher.Styled(t, b, h, o, plan, m);
            var env = new HouseEnv { Clear = new Clearance(t, q), Neighbours = guard.Neighbours, Index = i };
            var g = new BuildingGround(t, h);
            return HouseBuilder.Build(b, plan, ref g, env, o.SinkM, new HouseBudget(o.B0CapTris, o.B0BaseDrop), m, c);
        }
    }
}
