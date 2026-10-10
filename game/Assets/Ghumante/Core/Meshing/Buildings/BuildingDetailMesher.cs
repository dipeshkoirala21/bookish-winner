using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// B0 "kit-lite" buildings per 64 m detail cell (W2_DESIGN 2.4, 10.3): every building whose outer-ring centroid
    /// lies in the cell gets the full grammar (<see cref="HouseBuilder"/>) for houses, the generic sacred generators
    /// (<see cref="SacredSelector"/>, LOD0) for temples, stupas and shrines, and the styled B1 extrusion for the other
    /// archetypes. Each house is held to <see cref="BuildingOptions.B0CapTris"/> by dropping detail in the §2.4 order.
    /// Colliders (pikha aprons, flat roofs, plinths, temple steps) go to <paramref name="c"/> when given.
    /// Deterministic; positions relative to the tile's south-west corner. Thread-safe for distinct meshes.
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

        /// <summary>The detail cell (column, row) holding a building's outer-ring centroid.</summary>
        public static void CellOf(TileData t, int buildingIndex, out int cellX, out int cellZ)
        {
            int[] r = t.Buildings[buildingIndex].Rings[0];
            int n = r.Length / 2;
            double cx = 0, cz = 0;
            for (int k = 0; k < n; k++)
            {
                cx += r[2 * k];
                cz += r[2 * k + 1];
            }
            int cells = CellsPerSide(t);
            cellX = Math.Max(0, Math.Min(cells - 1, (int)Math.Floor(cx / n / 100.0 / CellM)));
            cellZ = Math.Max(0, Math.Min(cells - 1, (int)Math.Floor(cz / n / 100.0 / CellM)));
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TileData, int[][]> CellIndex =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TileData, int[][]>();

        /// <summary>The building indices of each detail cell (row-major, row 0 south), built once per tile.</summary>
        public static int[][] Cells(TileData t)
        {
            return CellIndex.GetValue(t, k =>
            {
                int n = CellsPerSide(k);
                var lists = new System.Collections.Generic.List<int>[n * n];
                for (int i = 0; i < k.Buildings.Count; i++)
                {
                    int cx, cz;
                    CellOf(k, i, out cx, out cz);
                    int c = cz * n + cx;
                    if (lists[c] == null) lists[c] = new System.Collections.Generic.List<int>();
                    lists[c].Add(i);
                }
                var a = new int[n * n][];
                for (int c = 0; c < a.Length; c++) a[c] = lists[c] == null ? new int[0] : lists[c].ToArray();
                return a;
            });
        }

        /// <summary>Build the B0 meshes of one detail cell; returns the number of buildings drawn.</summary>
        public static int BuildCell(TileData t, IHeightSampler h, int cellX, int cellZ, BuildingOptions o, MeshData m, GenColliders c)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (o == null) o = new BuildingOptions();
            int n = CellsPerSide(t);
            if (cellX < 0 || cellZ < 0 || cellX >= n || cellZ >= n) return 0;
            int drawn = 0;
            foreach (int i in Cells(t)[cellZ * n + cellX])
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

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TileData, BuildingBands.FootprintIndex> Neighbours =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TileData, BuildingBands.FootprintIndex>();

        /// <summary>The tile's footprint index (built once per tile): which corners abut a neighbour.</summary>
        internal static BuildingBands.FootprintIndex NeighbourIndex(TileData t)
        {
            return Neighbours.GetValue(t, k => new BuildingBands.FootprintIndex(k));
        }

        /// <summary>One building at B0. Footprints in a road corridor are trimmed (or the building is skipped when it
        /// stands in the road, <see cref="BuildingFootprints"/>); houses over the cap drop detail level by level; at
        /// the last level the house stays as built (the sanjhya is never dropped) unless it is still over the cap,
        /// when the B1 extrusion is used.</summary>
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
            var env = new HouseEnv { Clear = new Clearance(t, q), Neighbours = NeighbourIndex(t), Index = i };
            int v0 = m.VertexCount, i0 = m.IndexCount;
            bool uv0 = m.HasUv0;
            int boxes0 = c == null ? 0 : c.Boxes.Count, ramps0 = c == null ? 0 : c.Ramps.Count;
            for (int drop = 0; drop <= HouseBuilder.MaxDrop; drop++)
            {
                var g = new BuildingGround(t, h);
                if (!HouseBuilder.Build(b, plan, ref g, env, o.SinkM, drop, m, c)) return false;
                if ((m.IndexCount - i0) / 3 <= o.B0CapTris) return true;
                m.VertexCount = v0;
                m.IndexCount = i0;
                if (c != null)
                {
                    c.Boxes.RemoveRange(boxes0, c.Boxes.Count - boxes0);
                    c.Ramps.RemoveRange(ramps0, c.Ramps.Count - ramps0);
                }
            }
            return BuildingMesher.Styled(t, b, h, o, plan, m);
        }
    }
}
