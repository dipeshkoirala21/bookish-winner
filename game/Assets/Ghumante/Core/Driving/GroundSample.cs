using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// What the ground is like at one game position (M1_PLAN contracts): the rendered surface height, its
    /// normal, the physics surface group and, on a road, the road's class, surface and direction. Heights are
    /// absolute metres; the normal is a unit vector in game axes (X east, Y up, Z north).
    /// </summary>
    public struct GroundSample
    {
        /// <summary>Height the wheels stand on: the terrain, or the road deck (terrain + lift) on a road.</summary>
        public float Height;

        /// <summary>Unit surface normal (of the terrain triangle under the point).</summary>
        public float Nx, Ny, Nz;

        /// <summary>Physics surface: the road's group on a road, otherwise derived from the biome
        /// (<see cref="BiomeGround"/>). Wetness is applied by the vehicle, not here.</summary>
        public SurfaceGroup Surface;

        /// <summary>True within half the road width + <see cref="RoadSpatialIndex.OnRoadMarginM"/> of a
        /// road's centreline.</summary>
        public bool OnRoad;

        public RoadClass RoadClass;
        public Surface RoadSurface;

        // ---- additions beyond the M1_PLAN contract (all zero / default when not applicable) ----

        /// <summary>Terrain surface height without the road lift (equals <see cref="Height"/> off road).</summary>
        public float TerrainHeight;

        /// <summary>Biome class under the point (nearest BIOM sample of the finest tile that has one).</summary>
        public Biome Biome;

        /// <summary>Flags of the road under the point (bridge, ford, oneway ...), None off road.</summary>
        public RoadFlags RoadFlags;

        /// <summary>Unit tangent of the road at the nearest point, in the road's point order (game X/Z).</summary>
        public float RoadDirX, RoadDirZ;

        /// <summary>Signed distance from the road centreline, positive to the right of the point order.</summary>
        public float RoadOffsetM;

        /// <summary>Half the road's ribbon width (as the road mesh).</summary>
        public float RoadHalfWidthM;

        /// <summary>Quadtree level of the tile whose terrain answered (diagnostics).</summary>
        public int TileLevel;

        // ---- W2 additions (W2_DESIGN 10.3) ----

        /// <summary>What the foot touches: a structure's material, a paved AREA, the road surface or the biome, in
        /// that order. Wetness is not applied here (<see cref="FootSurfaces.Effective"/>).</summary>
        public FootSurface Foot;

        /// <summary>True on a raised footpath of a W2 road profile (beside the carriageway, kerb height above it).</summary>
        public bool OnFootpath;

        /// <summary>True when a walkable structure top or ramp (<see cref="StructureColliders"/>) carries the point.</summary>
        public bool OnStructure;
    }

    /// <summary>
    /// Answers "what is the ground here?" for game positions. Implementations are not required to be thread
    /// safe; the vehicle calls them from its own (main) thread. <see cref="TrySample"/> returns false where no
    /// ground is known (no tile loaded, outside the region): the vehicle treats that as a wall.
    /// </summary>
    public interface IGroundQuery
    {
        bool TrySample(double x, double z, out GroundSample s);
    }

    /// <summary>
    /// Optional companion of <see cref="IGroundQuery"/> for layered roads: <paramref name="nearY"/> (the height of
    /// whoever asks) picks between a bridge deck and what lies under it, so riding under a flyover or along a
    /// riverbed below a bridge stays below, and riding on the deck stays on it. <see cref="ArcadeVehicle"/> uses it
    /// automatically when the ground query implements it.
    /// </summary>
    public interface ILayeredGroundQuery : IGroundQuery
    {
        bool TrySample(double x, double z, float nearY, out GroundSample s);
    }

    /// <summary>
    /// Optional companion of <see cref="IGroundQuery"/>: a nearest-road lookup over a wider radius, used for
    /// stuck recovery (nudge towards the road). <see cref="TileGroundQuery"/> implements both.
    /// </summary>
    public interface IRoadQuery
    {
        /// <summary>The drawn road surface nearest to (x, z) whose edge lies within <paramref name="maxDistM"/>
        /// (see <see cref="RoadSpatialIndex.TryNearest"/>).</summary>
        bool TryNearestRoad(double x, double z, double maxDistM, out RoadHit hit);
    }

    /// <summary>
    /// Off-road physics surface per biome (ARCHITECTURE 7.6): built-up ground is PAVED, riverbeds, scree,
    /// moraine and steppe are GRAVEL, fields, grass and forest floors are DIRT, and water, wetland, paddy,
    /// glacier and snow are MUD (slippery and slow). Unknown (newer) biome values count as DIRT.
    /// </summary>
    public static class BiomeGround
    {
        private static readonly SurfaceGroup[] Table =
        {
            SurfaceGroup.Dirt, // None
            SurfaceGroup.Mud, // Water
            SurfaceGroup.Paved, // UrbanDense
            SurfaceGroup.Gravel, // UrbanGreen: gardens, yards and unpaved lanes between houses
            SurfaceGroup.Mud, // TeraiPaddy
            SurfaceGroup.Dirt, // TeraiCropland
            SurfaceGroup.Dirt, // TeraiSalForest
            SurfaceGroup.Dirt, // TeraiGrassland
            SurfaceGroup.Gravel, // RiverbedGravel
            SurfaceGroup.Dirt, // ChureForest
            SurfaceGroup.Dirt, // HillTerraces
            SurfaceGroup.Dirt, // HillForest
            SurfaceGroup.Dirt, // HillScrub
            SurfaceGroup.Dirt, // HillGrassland
            SurfaceGroup.Dirt, // SubalpineForest
            SurfaceGroup.Dirt, // AlpineMeadow
            SurfaceGroup.Dirt, // AlpineScrub
            SurfaceGroup.Gravel, // ScreeRock
            SurfaceGroup.Gravel, // Moraine
            SurfaceGroup.Mud, // Glacier
            SurfaceGroup.Mud, // Snow
            SurfaceGroup.Gravel, // TransHimalayanSteppe
            SurfaceGroup.Dirt, // TransHimalayanCropland
            SurfaceGroup.Dirt, // Orchard
            SurfaceGroup.Dirt, // TeaGarden
            SurfaceGroup.Mud, // Wetland
            SurfaceGroup.Dirt, // ValleyCropland
            SurfaceGroup.Dirt, // BareSoil
        };

        public static SurfaceGroup Of(Biome b)
        {
            int i = (int)b;
            return i < Table.Length ? Table[i] : SurfaceGroup.Dirt;
        }

        /// <summary>The nearest BIOM sample (vertex-aligned grid) of a tile at game (x, z), clamped onto the tile;
        /// None without a biome map.</summary>
        public static Biome Sample(TileData t, double x, double z)
        {
            if (t == null || t.Biomes == null || t.BiomesN < 2 || t.Biomes.Length < t.BiomesN * t.BiomesN) return Biome.None;
            int cells = t.BiomesN - 1;
            double s = t.Tile.Size / cells;
            double fi = Math.Floor((x - t.Tile.X0) / s + 0.5), fj = Math.Floor((z - t.Tile.Z0) / s + 0.5);
            int i = fi < 0 ? 0 : fi > cells ? cells : (int)fi;
            int j = fj < 0 ? 0 : fj > cells ? cells : (int)fj;
            return t.Biomes[j * t.BiomesN + i];
        }
    }
}
