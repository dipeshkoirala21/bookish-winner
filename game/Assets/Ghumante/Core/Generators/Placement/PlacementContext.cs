using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Generators.Placement
{
    /// <summary>
    /// Shared state of one tile's nature placement: the tile, its ground heights, the <see cref="PlacementMask"/>,
    /// the road corridors (when known), an occupancy grid so two plants never share a spot, the tile-local area
    /// types and biomes, and the output list with its plant budget. Tile-local metres with absolute Y. One per
    /// <see cref="TreePlacement.Place(TileData, IHeightSampler, TreePlacementOptions, IRoadCorridorQuery, List{TreeInstance})"/>
    /// call; not thread-safe (each worker builds its own).
    /// </summary>
    internal sealed class PlacementContext
    {
        public const int AreaCells = 8;
        private const double OccCellM = 2.0;

        public readonly TileData T;
        public readonly IHeightSampler H;
        public readonly PlacementMask Mask;
        public readonly IRoadCorridorQuery Corridor;
        public readonly TreePlacementOptions O;
        public readonly List<TreeInstance> Out;
        public readonly uint Seed;
        public readonly double Size;
        public readonly AreaType[] Areas;
        private readonly byte[] _occ;
        private readonly int _occN;

        /// <summary>Plants (non-trees) added so far; capped at <see cref="TreePlacementOptions.MaxPlants"/>.</summary>
        public int Plants;

        /// <summary>OSM trees moved out of a road corridor, and those dropped because they stood deep inside one.</summary>
        public int OsmMoved, OsmDropped;

        public PlacementContext(TileData t, IHeightSampler h, TreePlacementOptions o, IRoadCorridorQuery corridor, List<TreeInstance> output)
        {
            T = t;
            H = h;
            O = o;
            Corridor = corridor;
            Out = output;
            Size = t.Tile.Size;
            Seed = t.HasSeed ? (uint)(t.TileSeed ^ (t.TileSeed >> 32)) : (uint)(t.Tile.Key ^ (t.Tile.Key >> 32));
            Mask = new PlacementMask(t, corridor);
            Areas = AreaTypeGrid.ClassifyTile(t, AreaCells);
            _occN = Math.Max(1, (int)Math.Ceiling(Size / OccCellM));
            _occ = new byte[_occN * _occN];
        }

        public bool PlantsFull
        {
            get { return Plants >= O.MaxPlants; }
        }

        /// <summary>Ground height (absolute) at tile-local metres, clamped onto the tile.</summary>
        public float Height(double x, double z)
        {
            float y;
            if (H != null && H.TryHeight(T.Tile.X0 + x, T.Tile.Z0 + z, out y)) return y;
            double cx = x < 0 ? 0 : x > Size ? Size : x, cz = z < 0 ? 0 : z > Size ? Size : z;
            if (H != null && H.TryHeight(T.Tile.X0 + cx, T.Tile.Z0 + cz, out y)) return y;
            return 0f;
        }

        /// <summary>Slope (rise over run) and aspect (bearing of the downhill direction, degrees clockwise from north).</summary>
        public void Slope(double x, double z, out float slope, out float aspectDeg)
        {
            const double e = 4.0;
            double gx = (Height(x + e, z) - Height(x - e, z)) / (2 * e), gz = (Height(x, z + e) - Height(x, z - e)) / (2 * e);
            slope = (float)Math.Sqrt(gx * gx + gz * gz);
            double a = Math.Atan2(-gx, -gz) * 180 / Math.PI;
            aspectDeg = (float)(a < 0 ? a + 360 : a);
        }

        /// <summary>Nearest BIOM sample at tile-local metres (<see cref="Biome.None"/> without a biome map).</summary>
        public Biome BiomeAt(double x, double z)
        {
            if (T.Biomes == null || T.BiomesN < 2) return Biome.None;
            int bn = T.BiomesN;
            int i = (int)Math.Round(x / Size * (bn - 1)), j = (int)Math.Round(z / Size * (bn - 1));
            i = i < 0 ? 0 : i >= bn ? bn - 1 : i;
            j = j < 0 ? 0 : j >= bn ? bn - 1 : j;
            return T.Biomes[j * bn + i];
        }

        /// <summary>Area type of the tile-local cell (8 × 8 per tile) a point lies in.</summary>
        public AreaType AreaAt(double x, double z)
        {
            int i = (int)(x / Size * AreaCells), j = (int)(z / Size * AreaCells);
            i = i < 0 ? 0 : i >= AreaCells ? AreaCells - 1 : i;
            j = j < 0 ? 0 : j >= AreaCells ? AreaCells - 1 : j;
            return Areas[j * AreaCells + i];
        }

        /// <summary>Signed distance to the nearest road corridor edge (positive outside); +∞ without a corridor query.</summary>
        public double RoadDistance(double x, double z)
        {
            return Corridor != null ? Corridor.SignedDistance(x, z) : double.PositiveInfinity;
        }

        /// <summary>
        /// True when a plant of footprint radius <paramref name="radius"/> may stand at (x, z): on the tile, not on a
        /// road, building or water cell, at least <paramref name="roadMargin"/> outside every road corridor, and not on
        /// a spot already taken.
        /// </summary>
        public bool Clear(double x, double z, double radius, double roadMargin)
        {
            if (x < 0.5 || z < 0.5 || x > Size - 0.5 || z > Size - 0.5) return false;
            // The instance stores float positions: test the rounded spot too (it may fall into the next cell).
            if (!Mask.Free(x, z) || !Mask.Free((float)x, (float)z)) return false;
            if (Corridor != null && Corridor.SignedDistance(x, z) < roadMargin) return false;
            return !Taken(x, z, radius);
        }

        /// <summary>
        /// True when a plant may stand right beside a way it lines (a park path's hedge): on the tile, not on a building
        /// or water cell and not on a spot already taken. The road cells of the mask and the corridor margin are not
        /// checked: the caller keeps its own clearance from the way and from every other way.
        /// </summary>
        public bool ClearBesideWay(double x, double z, double radius)
        {
            if (x < 0.5 || z < 0.5 || x > Size - 0.5 || z > Size - 0.5) return false;
            if ((Mask.At(x, z) & (PlacementMask.Building | PlacementMask.Water)) != 0) return false;
            if (Corridor != null && Corridor.SignedDistance(x, z) < 0.3) return false;
            return !Taken(x, z, radius);
        }

        private bool Taken(double x, double z, double r)
        {
            int i0 = (int)Math.Floor((x - r) / OccCellM), i1 = (int)Math.Floor((x + r) / OccCellM);
            int j0 = (int)Math.Floor((z - r) / OccCellM), j1 = (int)Math.Floor((z + r) / OccCellM);
            for (int j = Math.Max(0, j0); j <= Math.Min(_occN - 1, j1); j++)
            for (int i = Math.Max(0, i0); i <= Math.Min(_occN - 1, i1); i++)
                if (_occ[j * _occN + i] != 0) return true;
            return false;
        }

        private void Occupy(double x, double z, double r)
        {
            int i0 = (int)Math.Floor((x - r) / OccCellM), i1 = (int)Math.Floor((x + r) / OccCellM);
            int j0 = (int)Math.Floor((z - r) / OccCellM), j1 = (int)Math.Floor((z + r) / OccCellM);
            for (int j = Math.Max(0, j0); j <= Math.Min(_occN - 1, j1); j++)
            for (int i = Math.Max(0, i0); i <= Math.Min(_occN - 1, i1); i++)
                _occ[j * _occN + i] = 1;
        }

        /// <summary>
        /// The distance a plant must keep from a road corridor (docs/W2_DETAIL_CONTRACT.md §1.1-1.2): a big tree whose
        /// crown starts above the 4.5 m overhead clearance may overhang the road, so only its trunk keeps clear
        /// (1.2 m); a smaller plant keeps its whole crown out.
        /// </summary>
        public static double RoadMarginFor(TreeSpecies s, float heightM, float crownM)
        {
            FloraInfo info = FloraCatalog.Info(s);
            float crownBase = heightM * (info.Family == TreeShape.Umbrella ? 0.4f : info.Family == TreeShape.Cone || info.Family == TreeShape.Column ? 0.3f : 0.35f);
            if (info.Class == FloraClass.Tree && crownBase >= RoadClearance.MinOverheadClearanceM) return 1.2;
            return 0.5 * crownM + 0.3;
        }

        /// <summary>Add a plant (remembering its footprint) and return true; false when the plant budget is spent.</summary>
        public bool Add(TreeSpecies s, double x, double z, float heightM, float crownM, float yawDeg, TreeOrigin origin, int clump = 0, ulong osmRef = 0,
                        bool chautari = false, float footprint = -1f)
        {
            bool tree = FloraCatalog.IsTree(s);
            if (!tree && PlantsFull) return false;
            float y = Height(x, z);
            Out.Add(new TreeInstance
            {
                X = (float)x, Y = y, Z = (float)z, HeightM = heightM, CrownM = crownM, YawDeg = yawDeg < 0 ? yawDeg + 360f : yawDeg, Species = s,
                Shape = FloraCatalog.Info(s).Family, SizeClass = (byte)(heightM < 6 ? 0 : heightM < 12 ? 1 : heightM < 20 ? 2 : 3), Origin = origin,
                Chautari = chautari, ClumpId = clump, OsmRef = osmRef,
            });
            if (!tree) Plants++;
            Occupy(x, z, footprint >= 0f ? footprint : tree ? Math.Min(2.5f, 0.15f * crownM + 0.6f) : 0.35f * crownM);
            return true;
        }

        /// <summary>A size for a kind: height from the catalogue range (scaled by <paramref name="scale"/>), crown from
        /// the model's aspect with ±10% variation.</summary>
        public static void Size01(TreeSpecies s, ref FloraRng rng, out float heightM, out float crownM, float scale = 1f)
        {
            FloraInfo info = FloraCatalog.Info(s);
            heightM = rng.Range(info.MinHeightM, info.MaxHeightM) * scale;
            crownM = heightM * info.Aspect * rng.Range(0.9f, 1.1f);
        }
    }
}
