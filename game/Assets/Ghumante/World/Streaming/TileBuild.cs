using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Ornaments;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Instancing;
using Ghumante.World.Rendering;
using Ghumante.World.Sacred;

namespace Ghumante.World.Streaming
{
    /// <summary>
    /// The mesh layers of a tile view, in upload order. W2 adds the road-decal layer (markings and zebras, W2_DESIGN
    /// 4.5), the far building bands B2 and B3 (2.4; <see cref="Buildings"/> is B1), and the hero replicas (3.4). The
    /// detail pass adds the bridge and flyover structures (<see cref="Bridges"/>, docs/W2_DETAIL_CONTRACT.md decisions 3
    /// and 4) and the roundabout and police-chowk dressing (<see cref="Ornaments"/>, roundabouts.md). The band, hero,
    /// bridge and ornament layers are split into parts (<see cref="UploadChunk.Part"/>): B1 and B2 per 128 m block, B3
    /// per 256 m block, heroes per (hero, LOD) as <c>hero × 4 + lod</c>, bridges and ornaments per (piece, LOD) as
    /// <c>piece × 4 + lod</c> (<see cref="LodPiece"/>). The perf pass splits the roads per 128 m block twice (the full kit
    /// as <c>block</c>, the cheap far copy as <see cref="RoadBlocks.FarPart"/> + block) and the decals per 128 m block
    /// (<see cref="RoadBlocks"/>).
    /// </summary>
    public static class TileLayers
    {
        public const int Terrain = 0;

        /// <summary>Road ribbons, junctions and islands: the full kit per 128 m block (part = block) and the cheap far copy
        /// (part = <see cref="RoadBlocks.FarPart"/> + block).</summary>
        public const int Roads = 1;

        /// <summary>B1 styled extrusions, one part per 128 m block.</summary>
        public const int Buildings = 2;

        public const int Areas = 3;

        /// <summary>Road markings (decals lifted over the ribbons), one part per 128 m block.</summary>
        public const int RoadDecals = 4;

        /// <summary>B2 prisms, one part per 128 m block.</summary>
        public const int BuildingsFar = 5;

        /// <summary>B3 city blocks, one part per 256 m block.</summary>
        public const int BuildingsBlock = 6;

        /// <summary>Hero replicas, part = hero × 4 + LOD.</summary>
        public const int Heroes = 7;

        /// <summary>Bridge and flyover structures (decks, railings, piers, stairs, trench walls), part = piece × 4 + LOD
        /// (<see cref="BridgeMesher"/>, one piece per span plus one for the trench walls).</summary>
        public const int Bridges = 8;

        /// <summary>Roundabout islands, statues and police posts, part = piece × 4 + LOD (<see cref="RoundaboutDecorator"/>,
        /// one piece per decorated site).</summary>
        public const int Ornaments = 9;

        public const int Count = 10;

        private static readonly string[] Names =
        {
            "terrain", "roads", "buildings", "areas", "decals", "buildings_b2", "buildings_b3", "heroes", "bridges", "ornaments",
        };

        /// <summary>Lowercase layer name (no allocation).</summary>
        public static string Name(int layer)
        {
            return layer >= 0 && layer < Count ? Names[layer] : "layer";
        }
    }

    /// <summary>
    /// One upload unit of a <see cref="TileBuild"/>: a run of one layer's triangles and the vertex range they use, at
    /// most <see cref="TileBuild.UploadChunkVertices"/> vertices and <see cref="TileBuild.UploadChunkBytes"/> bytes, so the
    /// main thread never copies a whole dense layer (a city buildings layer reaches 290 000 vertices) in one frame. Each
    /// chunk becomes its own mesh. Its indices (<see cref="MeshData.Indices"/>, and <see cref="TileBuild.ShortIndices"/>
    /// when <see cref="UsesShortIndices"/>) are relative to <see cref="FirstVertex"/>.
    /// </summary>
    public struct UploadChunk
    {
        public int Layer;

        /// <summary>The part of the layer the chunk belongs to (<see cref="TileLayers"/>; 0 for unsplit layers).</summary>
        public int Part;

        /// <summary>First index of the chunk in the layer's index arrays, and how many (a multiple of three).</summary>
        public int FirstIndex;

        public int IndexCount;

        /// <summary>First vertex of the chunk in the layer's vertex arrays, and how many.</summary>
        public int FirstVertex;

        public int VertexCount;

        /// <summary>Bounds of the chunk's vertices (metres from the area's south-west corner).</summary>
        public float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

        /// <summary>True when the chunk's indices fit 16 bits (always, unless one triangle spans more vertices than
        /// <see cref="TileBuild.UploadChunkVertices"/>).</summary>
        public bool UsesShortIndices
        {
            get { return VertexCount <= 65535; }
        }

        /// <summary>Approximate GPU bytes: vertex streams (position, normal, colour, UV0 when present) plus indices.</summary>
        public long Bytes(bool hasUv0)
        {
            return (long)VertexCount * (12 + 12 + 4 + (hasUv0 ? 8 : 0)) + (long)IndexCount * (UsesShortIndices ? 2 : 4);
        }
    }

    /// <summary>
    /// Options every tile build shares. Treat as read-only once streaming started (worker threads read it), apart from
    /// <see cref="SetMonth"/>, which the world calls on the main thread before it asks the scheduler to rebuild the
    /// seasonal layers (<see cref="StreamingScheduler.RefreshLayers"/>). <see cref="ForTier"/> makes the game's options:
    /// every per-tier choice of the detail-pass packages in one place.
    /// </summary>
    public sealed class MeshingSettings
    {
        /// <summary>Device tier the options are for (0 Low, 1 Mid, 2 High): which bridge and ornament LODs are built.</summary>
        public int Tier = 1;

        /// <summary>Palette season of the terrain and area colours (<see cref="SetMonth"/>).</summary>
        public Season Season = BiomePalette.DefaultSeason;

        /// <summary>Terrain skirt depth (below 0 automatic, see <see cref="TerrainOptions.SkirtDepthM"/>).</summary>
        public float SkirtDepthM = -1f;

        /// <summary>Road ribbons; pass the same instance to <see cref="TileGroundQuery"/> so lift and roads agree.</summary>
        public RoadOptions Roads = new RoadOptions();

        /// <summary>B1 buildings (the <see cref="TileLayers.Buildings"/> layer).</summary>
        public BuildingOptions Buildings = new BuildingOptions();

        /// <summary>B2 prisms (<see cref="TileLayers.BuildingsFar"/>).</summary>
        public BuildingOptions FarBuildings = new BuildingOptions { Band = BuildingBand.B2Prism };

        /// <summary>B3 city blocks (<see cref="TileLayers.BuildingsBlock"/>).</summary>
        public BuildingOptions BlockBuildings = new BuildingOptions { Band = BuildingBand.B3Block };

        /// <summary>B0 detail cells (built by the streamer per 64 m cell, not by tile builds).</summary>
        public BuildingOptions DetailBuildings = new BuildingOptions { Band = BuildingBand.B0KitLite };

        public AreaOptions Areas = new AreaOptions();
        public bool DrawRoads = true;
        public bool DrawBuildings = true;
        public bool DrawAreas = true;

        /// <summary>Road markings into <see cref="TileLayers.RoadDecals"/> (W2_DESIGN 4.5), one part per 128 m block
        /// (<see cref="RoadBlocks"/>; the streamer shows them only within the B1 radius).</summary>
        public bool DrawRoadDecals = true;

        /// <summary>Also build the cheap far copy of the road layer (<see cref="RoadBlocks.FarOptions"/> of
        /// <see cref="Roads"/>, parts <see cref="RoadBlocks.FarPart"/> + block), which the streamer shows beyond the B1
        /// radius instead of the full kit. Off: only the full kit, which then shows out to the far radius.</summary>
        public bool FarRoads = true;

        /// <summary>W2 building bands: B1 split per 128 m block plus the B2 and B3 layers. Off: the W1 single B1 layer.</summary>
        public bool BuildingBands = true;

        /// <summary>Hero replicas and hide zones (null: none).</summary>
        public HeroSet Heroes;

        /// <summary>Trees, street props and parked vehicles (<see cref="TileBuild.Instances"/>).</summary>
        public bool DrawInstances = true;

        public TreePlacementOptions Trees = new TreePlacementOptions();

        /// <summary>Bridge and flyover structures into <see cref="TileLayers.Bridges"/>, one piece per span at every LOD of
        /// <see cref="BridgeLods"/> (the streamer shows one per span by distance, <see cref="BridgeOptions.LodFor"/>).</summary>
        public bool DrawBridges = true;

        /// <summary>Roundabout islands, statues and police posts into <see cref="TileLayers.Ornaments"/>, one piece per
        /// decorated site at every LOD of <see cref="OrnamentLods"/>. When on, the road mesher's own police podiums are
        /// off (<see cref="RoadOptions.PolicePodiums"/>) and the decorator leaves the island disc to the road mesher.</summary>
        public bool DrawOrnaments = true;

        /// <summary>On a pack without structure records (RSTR), derive them from the tags (<see cref="BridgeStructures.For"/>)
        /// before anything is meshed, so the road surface, the bridge decks, the ground query and traffic share one set
        /// of deck heights (<see cref="StructureFallback"/>).</summary>
        public bool DeriveStructures = true;

        /// <summary>The road corridors of a tile in game metres (the roads package's <see cref="RoadCorridorIndex.ForTile"/>);
        /// set it with <see cref="SetCorridors"/> so every consumer gets it in its own frame. Null: each package's
        /// stand-in.</summary>
        public Func<TileData, IRoadCorridorQuery> Corridors { get; private set; }

        private readonly BridgeOptions[] _bridgeLods = { new BridgeOptions { Lod = 0 }, new BridgeOptions { Lod = 1 }, new BridgeOptions { Lod = 2 } };

        /// <summary>Bridge options per LOD 0, 1, 2 (one instance each, so worker builds never share a mutable LOD).</summary>
        public BridgeOptions BridgeLod(int lod)
        {
            return _bridgeLods[lod < 0 ? 0 : lod > 2 ? 2 : lod];
        }

        /// <summary>Bit mask of the bridge LODs built on <see cref="Tier"/>: Low never draws LOD0
        /// (<see cref="BridgeOptions.Lod0RadiusM"/>), so it never builds it.</summary>
        public int BridgeLods
        {
            get { return BridgeOptions.Lod0RadiusM[Tier < 0 ? 0 : Tier > 2 ? 2 : Tier] > 0 ? 0x7 : 0x6; }
        }

        /// <summary>Bit mask of the ornament LODs built on <see cref="Tier"/> (<see cref="OrnamentLod.BuiltMask"/>).</summary>
        public int OrnamentLods
        {
            get { return OrnamentLod.BuiltMask(Tier); }
        }

        /// <summary>Hide the hero zones' buildings in every band (W2_DESIGN 3.3 D5).</summary>
        public void SetHiddenRefs(ISet<ulong> refs)
        {
            Buildings.HiddenRefs = refs;
            FarBuildings.HiddenRefs = refs;
            BlockBuildings.HiddenRefs = refs;
            DetailBuildings.HiddenRefs = refs;
        }

        /// <summary>
        /// The road corridors (game metres) every consumer keeps clear (docs/W2_DETAIL_CONTRACT.md decisions 1 and 2): the
        /// building bands (footprint guard and overhang clamp), the nature placement (its tile-local frame,
        /// <see cref="CorridorFrames.TileLocal"/>) and the roundabout dressing. Null: each package's stand-in.
        /// </summary>
        public void SetCorridors(Func<TileData, IRoadCorridorQuery> corridors)
        {
            Corridors = corridors;
            Buildings.Corridors = corridors;
            FarBuildings.Corridors = corridors;
            BlockBuildings.Corridors = corridors;
            DetailBuildings.Corridors = corridors;
            Trees.Corridors = corridors != null ? CorridorFrames.TileLocal(corridors) : null;
        }

        /// <summary>Season of the terrain and area palettes from a month (1-12, <see cref="BiomePalette.SeasonOf"/>).
        /// Returns true when the season changed (the seasonal layers then need a rebuild).</summary>
        public bool SetMonth(int month)
        {
            Season s = BiomePalette.SeasonOf(month);
            bool changed = s != Season || s != Areas.Season;
            Season = s;
            Areas.Season = s;
            return changed;
        }

        /// <summary>The layers whose look follows the season (terrain and areas), as a <see cref="TileLayers"/> bit mask.</summary>
        public const int SeasonalLayers = (1 << TileLayers.Terrain) | (1 << TileLayers.Areas);

        /// <summary>
        /// The game's options for a device tier (0 Low, 1 Mid, 2 High) and month: the area budget
        /// (<see cref="AreaOptions.ForTier"/>), the band table's B1 (styled front detail, <see cref="BuildingBandTable.B1Options"/>),
        /// the light road kit on Low (<see cref="RoadOptions.Detail"/> off: about a quarter fewer road triangles), the
        /// season of the month, the roads package's corridors for every consumer (<see cref="SetCorridors"/>), bridges and
        /// roundabout dressing with their tier's LODs, and the hero set's replicas and hide zones.
        /// </summary>
        public static MeshingSettings ForTier(int tier, int month = BiomePalette.DefaultMonth, HeroSet heroes = null)
        {
            tier = tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
            var m = new MeshingSettings { Tier = tier };
            m.Areas = AreaOptions.ForTier(tier);
            m.Buildings = BuildingBandTable.B1Options(m.Buildings);
            m.Roads.Detail = tier > 0;
            m.Roads.PolicePodiums = !m.DrawOrnaments;
            m.SetMonth(month);
            m.SetCorridors(RoadCorridorIndex.ForTile);
            m.Heroes = heroes;
            if (heroes != null) m.SetHiddenRefs(heroes.HiddenRefs);
            return m;
        }
    }

    /// <summary>
    /// A bridge span or a decorated roundabout built with a tile at several LODs (<see cref="TileLayers.Bridges"/>,
    /// <see cref="TileLayers.Ornaments"/>): its parts are <c>Index × 4 + lod</c> for every lod in <see cref="LodMask"/>, and
    /// the streamer shows one of them by the camera's distance to its footprint.
    /// </summary>
    public struct LodPiece
    {
        /// <summary><see cref="TileLayers.Bridges"/> or <see cref="TileLayers.Ornaments"/>.</summary>
        public int Layer;

        /// <summary>Index among the tile's pieces of that layer.</summary>
        public int Index;

        /// <summary>Plan centre (tile-local metres) and radius of the footprint (all LODs).</summary>
        public double CX, CZ;

        public float RadiusM;

        /// <summary>Bit per LOD built (bit 0 = LOD0).</summary>
        public int LodMask;

        /// <summary>The LOD nearest to <paramref name="want"/> that was built: the next coarser first, else the next
        /// finer (-1 when none).</summary>
        public int Nearest(int want)
        {
            for (int l = want; l < 4; l++)
                if ((LodMask & (1 << l)) != 0) return l;
            for (int l = want - 1; l >= 0; l--)
                if ((LodMask & (1 << l)) != 0) return l;
            return -1;
        }

        /// <summary>The centreline of a bridge span or trench piece (tile-local; <see cref="BridgeMesher.Distance"/>), or
        /// null (ornaments): then the distance is to the footprint circle.</summary>
        public BridgeSpan Span;

        /// <summary>How far the structure of <see cref="Span"/> reaches sideways from its centreline (m).</summary>
        public float SpanHalfM;

        /// <summary>Plan distance (m) from a tile-local point to the footprint (0 inside): to the structure along its
        /// centreline for a bridge or trench (a long span is near only where the camera is near it), else to the circle
        /// round the footprint. The circle distance is a lower bound of the other, so far pieces skip the walk along the
        /// centreline. No allocation.</summary>
        public double DistanceTo(double x, double z)
        {
            double dx = x - CX, dz = z - CZ;
            double circle = Math.Max(0.0, Math.Sqrt(dx * dx + dz * dz) - RadiusM);
            if (Span == null || circle >= FarM) return circle;
            return Math.Max(circle, BridgeMesher.Distance(Span, x, z) - SpanHalfM);
        }

        /// <summary>Beyond this circle distance every piece shows its coarsest LOD anyway, so the centreline is not walked.</summary>
        public const double FarM = 2000.0;
    }

    /// <summary>A hero replica built with a tile (W2_DESIGN 3.4): where it stands and its triangles per LOD.</summary>
    public struct HeroPiece
    {
        public string Id;

        /// <summary>Index among the tile's heroes (its parts are <c>Index × 4 + lod</c>).</summary>
        public int Index;

        /// <summary>Centre (tile-local metres), ground height and height of the top.</summary>
        public double CX, CZ;

        public float GroundY, TopY, RadiusM;
        public int Tris0, Tris1, Tris2, Tris3;
    }

    /// <summary>What a ready node hands to its view besides meshes: the decoded tile and sampler (for B0 cells), the
    /// instanced dressing, the heroes, the bridge and ornament pieces and the structure colliders (tile-local). Owned by
    /// the view.</summary>
    public sealed class TileExtras
    {
        public TileData Source;
        public TileHeightSampler Sampler;
        public TileInstances Instances;
        public HeroPiece[] Heroes = new HeroPiece[0];

        /// <summary>Bridge spans and decorated roundabouts with their LOD parts.</summary>
        public LodPiece[] Pieces = new LodPiece[0];

        /// <summary>Colliders of the heroes (tile-local), or null.</summary>
        public StructureColliders HeroColliders;

        /// <summary>Colliders of the roundabout dressing (pedestals, gates, posts, island tops) and of the instanced
        /// dressing (generated tree trunks, generated lamp and signal poles, parked vehicles that are not community
        /// fleet), tile-local, or null.</summary>
        public StructureColliders Colliders;

        /// <summary><see cref="Colliders"/> indexed by the build's worker for the ground query
        /// (<see cref="TileGroundQuery.PrepareColliders"/>, key <c>tile | TileGroundQuery.StaticSetKeyBit</c>), so
        /// registering the static set when the tile shows only links it in (perf pass); null without colliders or
        /// without a ground query.</summary>
        public TileGroundQuery.PreparedColliders PreparedColliders;
    }

    /// <summary>
    /// One tile build: the decode and meshing work for a <see cref="SelectedNode"/> that runs on a worker thread
    /// (<see cref="Execute"/>, Core types only, no Unity API), and its result, which the main thread uploads chunk by
    /// chunk (<see cref="Chunks"/>). Instances and their <see cref="MeshData"/> buffers are pooled by <see cref="StreamingScheduler"/>.
    /// <para>
    /// Per node: the terrain of the area from its source (decimation step from
    /// <see cref="StreamingConfig.TerrainStep(SelectedNode, int)"/>, skirts), the matching <see cref="TileHeightSampler"/> for the ground query, and, for exact nodes whose source has
    /// detail chunks, roads, buildings and areas draped on that same sampler plus the road index. Positions are metres
    /// from the area's south-west corner with absolute heights.
    /// </para>
    /// <para>
    /// Every layer is then split into <see cref="Chunks"/> of at most <see cref="UploadChunkVertices"/> vertices and
    /// <see cref="UploadChunkBytes"/> bytes, the unit the scheduler uploads (one or more per frame within its time and byte
    /// budgets). The road and decal layers are first regrouped per 128 m block (<see cref="RoadBlocks"/>).
    /// </para>
    /// </summary>
    public sealed class TileBuild
    {
        /// <summary>
        /// Above this many vertices a pooled layer buffer is dropped instead of kept (memory cap). Buffers grow by
        /// doubling, so this keeps the 262 144-vertex capacity that dense city buildings layers (130 000 to 200 000
        /// vertices in kathmandu_core) grow to: they are reused instead of re-grown from scratch as large-object garbage
        /// on every build. Only rare outliers beyond it are dropped.
        /// </summary>
        public const int PoolKeepVertices = 262144;

        /// <summary>Most vertices one <see cref="UploadChunk"/> spans (16-bit indices); <see cref="UploadChunkBytes"/>
        /// usually ends a chunk first.</summary>
        public const int UploadChunkVertices = 32768;

        /// <summary>
        /// Most bytes one <see cref="UploadChunk"/> uploads (<see cref="UploadChunk.Bytes"/>: 28 bytes per vertex, 36 with
        /// UV0, plus 2 per index): the scheduler's default per-tick byte budget (<see cref="StreamingScheduler.UploadBudgetBytes"/>,
        /// 1 MiB), so a chunk never makes a tick copy more than the budget. A dense city layer with UV0 (the W2 detail
        /// pass) ends its chunks at about 25 000 vertices instead of the 32 768 that 28-byte vertices allowed (1.43 MB).
        /// </summary>
        public const long UploadChunkBytes = 1L << 20;

        public SelectedNode Node;

        /// <summary>The decoded source tile: given by the dispatcher when cached, else decoded by the job.</summary>
        public TileData Source;

        /// <summary>True when <see cref="Execute"/> decoded <see cref="Source"/> (the caller caches it).</summary>
        public bool DecodedSource;

        /// <summary>Terrain decimation step used for the meshes and the sampler.</summary>
        public int Step;

        public readonly MeshData[] Layers = new MeshData[TileLayers.Count];

        /// <summary>16-bit copies of each layer's chunk-relative indices (valid for the chunks whose
        /// <see cref="UploadChunk.UsesShortIndices"/> is true; parallel to <see cref="MeshData.Indices"/>).</summary>
        public readonly ushort[][] ShortIndices = new ushort[TileLayers.Count][];

        /// <summary>Upload units of every non-empty layer, in layer order (filled by <see cref="Execute"/>).</summary>
        public readonly List<UploadChunk> Chunks = new List<UploadChunk>(16);

        /// <summary>Per layer min x, y, z then max x, y, z.</summary>
        public readonly float[] Bounds = new float[6 * TileLayers.Count];

        /// <summary>How far the curvature drop can lower this area's vertices, for renderer bounds.</summary>
        public float CurvatureMarginM;

        public TileHeightSampler Sampler;
        public RoadSpatialIndex Roads;
        public Exception Error;

        /// <summary>The runs of each layer's parts (empty for unsplit layers, which are part 0).</summary>
        public readonly List<PartRange>[] Parts = new List<PartRange>[TileLayers.Count];

        /// <summary>Trees, props and parked vehicles of a detail node (null when not built).</summary>
        public TileInstances Instances;

        /// <summary>Heroes built with this node.</summary>
        public readonly List<HeroPiece> Heroes = new List<HeroPiece>();

        /// <summary>Structure colliders of the heroes (tile-local), or null.</summary>
        public StructureColliders HeroColliders;

        /// <summary>Heroes whose build threw (skipped; the tile still loads).</summary>
        public int HeroFailures;

        /// <summary>Bridge spans and decorated roundabouts built with this node (their parts are <c>Index × 4 + lod</c>).</summary>
        public readonly List<LodPiece> Pieces = new List<LodPiece>();

        /// <summary>Colliders of the ornaments and the instanced dressing (tile-local), or null.</summary>
        public StructureColliders Colliders;

        /// <summary><see cref="Colliders"/> prepared for the ground query on the worker (<see cref="TileExtras.PreparedColliders"/>).</summary>
        public TileGroundQuery.PreparedColliders PreparedColliders;

        /// <summary>Ornament sites whose build threw (skipped; the tile still loads).</summary>
        public int OrnamentFailures;

        /// <summary>
        /// Non-zero for a refresh build (<see cref="StreamingScheduler.RefreshLayers"/>): only the layers of this
        /// <see cref="TileLayers"/> bit mask are built (with the sampler), for a node whose view is already up; nothing
        /// else (no instances, heroes, colliders or road index).
        /// </summary>
        public int RefreshMask;

        /// <summary>The scheduler's settings version when the build was dispatched (a build older than the current
        /// version is refreshed once it is up).</summary>
        public int SettingsVersion;

        private readonly MeshData _scratch = new MeshData();
        private readonly MeshParts.Scratch _partScratch = new MeshParts.Scratch();
        private readonly RoadBlocks.Scratch _roadScratch = new RoadBlocks.Scratch();
        private readonly GenColliders _gen = new GenColliders();

        /// <summary>Worker milliseconds spent on this build.</summary>
        public double WorkMs;

        internal StreamingScheduler Owner;
        internal long Generation;
        internal int NextChunk;
        internal readonly Action Work;

        public TileBuild()
        {
            for (int i = 0; i < TileLayers.Count; i++)
            {
                Layers[i] = new MeshData();
                Parts[i] = new List<PartRange>();
            }
            Work = RunOnWorker;
        }

        public bool HasLayer(int layer)
        {
            return Layers[layer].IndexCount > 0;
        }

        public bool HasAnyLayer
        {
            get
            {
                for (int i = 0; i < TileLayers.Count; i++)
                    if (HasLayer(i)) return true;
                return false;
            }
        }

        /// <summary>True when every chunk of the layer uses 16-bit indices (<see cref="ShortIndices"/> holds them).</summary>
        public bool UsesShortIndices(int layer)
        {
            for (int i = 0; i < Chunks.Count; i++)
                if (Chunks[i].Layer == layer && !Chunks[i].UsesShortIndices) return false;
            return true;
        }

        /// <summary>Approximate GPU bytes of one chunk.</summary>
        public long ChunkBytes(int chunk)
        {
            UploadChunk c = Chunks[chunk];
            return c.Bytes(Layers[c.Layer].HasUv0);
        }

        /// <summary>Approximate GPU bytes of a layer: the sum of its chunks.</summary>
        public long LayerBytes(int layer)
        {
            long bytes = 0;
            for (int i = 0; i < Chunks.Count; i++)
                if (Chunks[i].Layer == layer) bytes += ChunkBytes(i);
            return bytes;
        }

        public void GetBounds(int layer, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ)
        {
            int b = layer * 6;
            minX = Bounds[b];
            minY = Bounds[b + 1];
            minZ = Bounds[b + 2];
            maxX = Bounds[b + 3];
            maxY = Bounds[b + 4];
            maxZ = Bounds[b + 5];
        }

        /// <summary>Prepare a pooled build for a new node.</summary>
        internal void Reset(StreamingScheduler owner, SelectedNode node, TileData cachedSource, long generation)
        {
            Owner = owner;
            Node = node;
            Source = cachedSource;
            DecodedSource = false;
            Step = 1;
            Sampler = null;
            Roads = null;
            Error = null;
            WorkMs = 0;
            CurvatureMarginM = 0f;
            Generation = generation;
            NextChunk = 0;
            Chunks.Clear();
            Instances = null;
            Heroes.Clear();
            HeroColliders = null;
            HeroFailures = 0;
            Pieces.Clear();
            Colliders = null;
            PreparedColliders = null;
            OrnamentFailures = 0;
            RefreshMask = 0;
            SettingsVersion = 0;
            for (int i = 0; i < TileLayers.Count; i++)
            {
                Layers[i].Clear();
                Parts[i].Clear();
            }
        }

        /// <summary>Hand the non-mesh results to the view (the build forgets them).</summary>
        public TileExtras TakeExtras()
        {
            var e = new TileExtras
            {
                Source = Source, Sampler = Sampler, Instances = Instances, Heroes = Heroes.ToArray(), HeroColliders = HeroColliders,
                Pieces = Pieces.ToArray(), Colliders = Colliders, PreparedColliders = PreparedColliders,
            };
            Instances = null;
            HeroColliders = null;
            Colliders = null;
            PreparedColliders = null;
            Heroes.Clear();
            Pieces.Clear();
            return e;
        }

        /// <summary>Drop references and oversized buffers before the build goes back to the pool.</summary>
        internal void Recycle()
        {
            Owner = null;
            Source = null;
            Sampler = null;
            Roads = null;
            Error = null;
            Chunks.Clear();
            NextChunk = 0;
            Instances = null;
            Heroes.Clear();
            HeroColliders = null;
            Pieces.Clear();
            Colliders = null;
            PreparedColliders = null;
            RefreshMask = 0;
            if (_scratch.VertexCapacity > PoolKeepVertices) _scratch.Clear();
            for (int i = 0; i < TileLayers.Count; i++)
            {
                Parts[i].Clear();
                if (Layers[i].VertexCapacity > PoolKeepVertices || Layers[i].Indices.Length > PoolKeepVertices * 6) Layers[i] = new MeshData();
                else Layers[i].Clear();
                if (ShortIndices[i] != null && ShortIndices[i].Length > PoolKeepVertices * 6) ShortIndices[i] = null;
            }
        }

        private void RunOnWorker()
        {
            StreamingScheduler owner = Owner;
            try
            {
                Execute(this, owner.Pack, owner.Config, owner.Meshing, owner.GroundQuery);
            }
            catch (Exception e)
            {
                Error = e;
            }
            finally
            {
                owner.OnWorkerDone(this);
            }
        }

        /// <summary>
        /// The worker-thread part: decode the source when needed, mesh every layer and build the sampler and road
        /// index. Thread-safe for distinct builds (the meshers keep per-thread scratch); touches no Unity API. A refresh
        /// build (<see cref="RefreshMask"/>) builds only those layers and the sampler.
        /// </summary>
        public static void Execute(TileBuild b, PackReader pack, StreamingConfig config, MeshingSettings settings, TileGroundQuery ground)
        {
            var sw = Stopwatch.StartNew();
            SelectedNode n = b.Node;
            if (b.Source == null)
            {
                if (pack == null) throw new InvalidOperationException("no pack to decode " + n.Source + " from");
                b.Source = pack.ReadTile(n.Source);
                if (b.Source == null) throw new InvalidOperationException("tile " + n.Source + " is not in the pack");
                b.DecodedSource = true;
            }
            if (settings == null) settings = new MeshingSettings();
            TileData src = b.Source;
            TileId area = n.Area;
            int step = config.TerrainStep(n, src.HeightsN);
            b.Step = step;
            for (int i = 0; i < TileLayers.Count; i++) b.Layers[i].Clear();
            b.Chunks.Clear();
            int only = b.RefreshMask;
            bool detail = n.DrawsDetail && src.HasDetail;
            // Before anything reads the tile's roads: one set of deck heights for roads, bridges, ground and traffic.
            if (detail && settings.DeriveStructures) StructureFallback.Apply(src);

            if (Wants(only, TileLayers.Terrain))
            {
                var terrain = new TerrainOptions { Step = step, SkirtDepthM = settings.SkirtDepthM, Season = settings.Season };
                TerrainMesher.Build(src, area, terrain, b.Layers[TileLayers.Terrain]);
            }
            b.Sampler = TileHeightSampler.ForArea(src, area, step);
            // The paved areas the ground query indexes when an exact area joins it, indexed here rather than on the main
            // thread in the frame the tile shows (perf pass).
            if (ground != null && only == 0 && n.IsExact && b.Sampler.HasHeights) TileGroundQuery.PreparePaving(src);

            if (detail && b.Sampler.HasHeights)
            {
                if (only != 0)
                {
                    if (settings.DrawAreas && Wants(only, TileLayers.Areas)) AreaMesher.Build(src, b.Sampler, settings.Areas, b.Layers[TileLayers.Areas]);
                }
                else
                {
                    if (settings.DrawRoads) BuildRoads(b, src, settings);
                    if (settings.DrawRoads && settings.DrawBridges) BuildBridges(b, src, settings);
                    if (settings.DrawBuildings) BuildBuildings(b, src, settings);
                    if (settings.DrawAreas) AreaMesher.Build(src, b.Sampler, settings.Areas, b.Layers[TileLayers.Areas]);
                    if (settings.Heroes != null) BuildHeroes(b, src, settings.Heroes, BandConfig.HeroLod0AllowedFor(settings.Tier));
                    if (settings.DrawRoads && settings.DrawOrnaments) BuildOrnaments(b, src, settings);
                    if (settings.DrawInstances) BuildInstances(b, src, settings);
                    b.Roads = ground != null ? ground.BuildRoadIndex(src) : null;
                }
            }

            // The static dressing set indexed here rather than on the main thread when the tile shows (perf pass).
            if (ground != null && b.Colliders != null && b.Colliders.Count > 0)
                b.PreparedColliders = TileGroundQuery.PrepareColliders(area.Key | TileGroundQuery.StaticSetKeyBit, b.Colliders);

            for (int i = 0; i < TileLayers.Count; i++)
            {
                MeshData m = b.Layers[i];
                float x0, y0, z0, x1, y1, z1;
                m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                int o = i * 6;
                b.Bounds[o] = x0;
                b.Bounds[o + 1] = y0;
                b.Bounds[o + 2] = z0;
                b.Bounds[o + 3] = x1;
                b.Bounds[o + 4] = y1;
                b.Bounds[o + 5] = z1;
                if (b.Parts[i].Count > 0) SplitLayer(m, i, b.Parts[i], UploadChunkVertices, UploadChunkBytes, b.Chunks, ref b.ShortIndices[i]);
                else SplitLayer(m, i, UploadChunkVertices, UploadChunkBytes, b.Chunks, ref b.ShortIndices[i]);
            }

            // Vertices drop with the square of their distance to the camera; the camera can be anywhere inside this
            // area's ring, so allow for the ring radius plus the area's diagonal.
            double reach = config.RingFor(area.Level).RadiusM + area.Size * 1.5;
            b.CurvatureMarginM = (float)EarthCurvature.DropM(reach);
            b.WorkMs = sw.Elapsed.TotalMilliseconds;
        }

        private static bool Wants(int only, int layer)
        {
            return only == 0 || (only & (1 << layer)) != 0;
        }

        /// <summary>
        /// The road ribbons and their markings per 128 m block (<see cref="RoadBlocks"/>): the full kit of
        /// <see cref="MeshingSettings.Roads"/> as parts = block, the cheap far copy (<see cref="RoadBlocks.FarOptions"/>,
        /// when <see cref="MeshingSettings.FarRoads"/>) as parts = <see cref="RoadBlocks.FarPart"/> + block, and the
        /// markings (<see cref="MeshingSettings.DrawRoadDecals"/>) per block of the decal layer, every triangle clipped at
        /// the block lines.
        /// </summary>
        private static void BuildRoads(TileBuild b, TileData src, MeshingSettings settings)
        {
            double size = src.Tile.Size;
            MeshData roads = b.Layers[TileLayers.Roads];
            List<PartRange> parts = b.Parts[TileLayers.Roads];
            b._scratch.Clear();
            RoadMesher.Build(src, b.Sampler, settings.Roads, b._scratch);
            RoadBlocks.Append(b._scratch, size, RoadBlocks.BlockM, 0, roads, parts, b._roadScratch);
            if (settings.FarRoads && b._scratch.IndexCount > 0)
            {
                b._scratch.Clear();
                RoadMesher.Build(src, b.Sampler, RoadBlocks.FarOptions(settings.Roads), b._scratch);
                RoadBlocks.Append(b._scratch, size, RoadBlocks.BlockM, RoadBlocks.FarPart, roads, parts, b._roadScratch);
            }
            if (!settings.DrawRoadDecals) return;
            b._scratch.Clear();
            MarkingMesher.Build(src, b.Sampler, settings.Roads, b._scratch);
            RoadBlocks.Append(b._scratch, size, RoadBlocks.BlockM, 0, b.Layers[TileLayers.RoadDecals], b.Parts[TileLayers.RoadDecals], b._roadScratch);
        }

        /// <summary>
        /// Every bridge and flyover span of the tile as its own piece at each LOD of <see cref="MeshingSettings.BridgeLods"/>
        /// (parts <c>piece × 4 + lod</c>), then every underpass trench as a piece of its own. Spans follow the tile's
        /// structure records (or the derived ones, <see cref="StructureFallback"/>), the same heights the road mesher
        /// lays the carriageway at. Each piece keeps its centreline (<see cref="LodPiece.Span"/>), so the streamer picks
        /// its LOD by the camera's distance to the structure itself, not to a circle round a 300-500 m span.
        /// </summary>
        private static void BuildBridges(TileBuild b, TileData src, MeshingSettings settings)
        {
            if (src.Roads.Count == 0) return;
            BridgeLayout layout = BridgeLayout.For(src);
            if (layout.Spans.Count == 0 && layout.Trenches.Count == 0) return;
            MeshData layer = b.Layers[TileLayers.Bridges];
            List<PartRange> parts = b.Parts[TileLayers.Bridges];
            int mask = settings.BridgeLods;
            int index = 0;
            int spans = layout.Spans.Count, total = spans + layout.Trenches.Count;
            for (int si = 0; si < total; si++)
            {
                bool trench = si >= spans;
                BridgeSpan span = trench ? layout.Trenches[si - spans] : layout.Spans[si];
                // A stair span's flights stand beside its path and are small: the footprint circle measures them.
                var piece = new LodPiece { Layer = TileLayers.Bridges, Index = index };
                if (!span.Stair)
                {
                    piece.Span = span;
                    piece.SpanHalfM = SpanHalfWidth(span);
                }
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                for (int lod = 0; lod < 3; lod++)
                {
                    if ((mask & (1 << lod)) == 0) continue;
                    b._scratch.Clear();
                    BridgeOptions o = settings.BridgeLod(lod);
                    bool built = trench ? BridgeMesher.BuildTrench(layout, si - spans, b.Sampler, o, b._scratch)
                                        : BridgeMesher.BuildSpan(layout, si, b.Sampler, o, b._scratch);
                    if (!built || b._scratch.IndexCount == 0) continue;
                    Grow(b._scratch, ref minX, ref minZ, ref maxX, ref maxZ);
                    MeshParts.AppendPart(b._scratch, layer, index * 4 + lod, parts);
                    piece.LodMask |= 1 << lod;
                }
                if (piece.LodMask == 0) continue;
                SetFootprint(ref piece, minX, minZ, maxX, maxZ);
                b.Pieces.Add(piece);
                index++;
            }
        }

        /// <summary>Most a span's structure reaches sideways from its centreline (m): the widest deck half plus walkway,
        /// and a margin for railings, piers, stairs and wing walls.</summary>
        internal static float SpanHalfWidth(BridgeSpan sp)
        {
            double half = 0;
            for (int k = 0; k < sp.Count; k++)
            {
                double l = (sp.HalfL != null && k < sp.HalfL.Length ? sp.HalfL[k] : 0) + (sp.WalkL != null && k < sp.WalkL.Length ? sp.WalkL[k] : 0);
                double r = (sp.HalfR != null && k < sp.HalfR.Length ? sp.HalfR[k] : 0) + (sp.WalkR != null && k < sp.WalkR.Length ? sp.WalkR[k] : 0);
                half = Math.Max(half, Math.Max(l, r));
            }
            return (float)(half + 2.0);
        }

        /// <summary>
        /// The roundabout and police-chowk dressing (<see cref="RoundaboutDecorator"/>): every raised island of the road
        /// layout (the road mesher draws the island disc and kerb, the decorator its garden, statue, railing, lamps and
        /// police post), the police posts of chowks without an island and the standalone curated sites (Shahid Gate,
        /// Singha Durbar, Kalimati, with their own island base, fitted between the road corridors). One piece per site at
        /// each LOD of <see cref="MeshingSettings.OrnamentLods"/>; the finest LOD's colliders (pedestals, gates, posts, and
        /// the walkable top of a standalone island) join <see cref="Colliders"/>.
        /// </summary>
        private static void BuildOrnaments(TileBuild b, TileData src, MeshingSettings settings)
        {
            IRoadCorridorQuery corridors = settings.Corridors != null ? settings.Corridors(src) : null;
            var opt = new RoundaboutDecorator.DecorOptions
            {
                DrawBase = false, DrawPolice = true, Corridors = corridors, Roads = settings.Roads,
            };
            if (src.Roads.Count > 0)
            {
                foreach (RoadIsland isl in RoadLayout.For(src).Islands)
                {
                    if (isl.Kind == IslandKind.Mini || isl.RadiusM < 0.5f) continue;
                    RoadIsland island = isl;
                    BuildOrnament(b, settings, (m, lod, o) => RoundaboutDecorator.Build(island, src, b.Sampler, m, lod, null, o), opt, false, 0, 0, 0, 0);
                }
            }
            opt.DrawBase = true;
            foreach (JunctionRecord j in src.Junctions)
            {
                RoadIsland unused;
                if (RoundaboutDecorator.TryLayoutIsland(src, j.XCm / 100.0, j.ZCm / 100.0, out unused)) continue;
                JunctionRecord rec = j;
                BuildOrnament(b, settings, (m, lod, o) => RoundaboutDecorator.Build(rec, src, b.Sampler, m, lod, -1f, float.NaN, null, o), opt, false, 0, 0, 0, 0);
            }
            var standalone = new List<RoundaboutEntry>(2);
            RoundaboutCatalog.StandaloneIn(src, standalone);
            foreach (RoundaboutEntry e in standalone)
            {
                RoundaboutSite site = RoundaboutDecorator.StandaloneSite(e, src);
                RoundaboutDesign design = e.Design;
                BuildOrnament(b, settings, (m, lod, o) => RoundaboutDecorator.Build(site, design, b.Sampler, m, lod, null, o), opt, true,
                              site.X, site.Z, site.RadiusM, site.RoadLiftM);
            }
        }

        private delegate int OrnamentBuild(MeshData m, int lod, RoundaboutDecorator.DecorOptions options);

        /// <summary>One decorated site at every built LOD; a standalone island also gets a walkable top (its kerb is
        /// lower than a step, so riders mount it like a pavement).</summary>
        private static void BuildOrnament(TileBuild b, MeshingSettings settings, OrnamentBuild build, RoundaboutDecorator.DecorOptions opt,
                                          bool standalone, double sx, double sz, float radius, float roadLift)
        {
            MeshData layer = b.Layers[TileLayers.Ornaments];
            List<PartRange> parts = b.Parts[TileLayers.Ornaments];
            int index = b.PieceCount(TileLayers.Ornaments);
            int mask = settings.OrnamentLods;
            int partsBefore = parts.Count, v0 = layer.VertexCount, i0 = layer.IndexCount;
            var piece = new LodPiece { Layer = TileLayers.Ornaments, Index = index };
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            int finest = -1;
            for (int lod = 0; lod < 4 && finest < 0; lod++)
                if ((mask & (1 << lod)) != 0) finest = lod;
            try
            {
                b._gen.Clear();
                for (int lod = 0; lod < 4; lod++)
                {
                    if ((mask & (1 << lod)) == 0) continue;
                    b._scratch.Clear();
                    RoundaboutDecorator.DecorOptions o = opt;
                    o.Colliders = lod == finest ? b._gen : null;
                    if (build(b._scratch, lod, o) <= 0 || b._scratch.IndexCount == 0) continue;
                    Grow(b._scratch, ref minX, ref minZ, ref maxX, ref maxZ);
                    MeshParts.AppendPart(b._scratch, layer, index * 4 + lod, parts);
                    piece.LodMask |= 1 << lod;
                }
                if (piece.LodMask == 0) return;
                // The island top (road lift + kerb, about 0.5 m over the terrain) of a standalone island this decorator
                // draws itself, walkable: riders step onto it like a pavement instead of passing through the lawn.
                double r = Math.Min(radius, 0.5 * Math.Min(maxX - minX, maxZ - minZ));
                float g;
                if (standalone && r >= 0.5 && b.Sampler.TryHeightClamped(b.Source.Tile.X0 + sx, b.Source.Tile.Z0 + sz, out g))
                    AddIslandTop(b._gen, opt.Corridors, b.Source.Tile.X0, b.Source.Tile.Z0, sx, sz, r, g - 0.5, g + roadLift + 0.22);
                if (b._gen.Boxes.Count + b._gen.Ramps.Count + b._gen.Cylinders.Count + b._gen.Walls.Count > 0)
                {
                    if (b.Colliders == null) b.Colliders = new StructureColliders();
                    b.Colliders.AddFrom(b._gen);
                }
                SetFootprint(ref piece, minX, minZ, maxX, maxZ);
                b.Pieces.Add(piece);
            }
            catch (Exception)
            {
                // A broken design must not take the tile down: drop what it added.
                b.OrnamentFailures++;
                parts.RemoveRange(partsBefore, parts.Count - partsBefore);
                layer.VertexCount = v0;
                layer.IndexCount = i0;
            }
        }

        /// <summary>Squares (rotated <see cref="IslandTopStepDeg"/> apart) whose union approximates a standalone island's
        /// round walkable top.</summary>
        public const int IslandTopSquares = 3;

        /// <summary>Turn (degrees) between the island-top squares: a quarter turn shared evenly.</summary>
        public const double IslandTopStepDeg = 90.0 / IslandTopSquares;

        /// <summary>
        /// The walkable top of a round island of radius <paramref name="r"/> centred at (cx, cz) tile-local: squares
        /// inscribed in the disc (corners on the rim), turned <see cref="IslandTopStepDeg"/> apart, so their union covers
        /// the disc to about 0.82 r and never leaves it. Each square is shrunk until every corner and edge midpoint lies
        /// outside the drawn road corridors (<paramref name="corridors"/>, game metres; <c>SignedDistance ≥ 0</c>), so the
        /// top never becomes a kerb on the carriageway. Returns the boxes added.
        /// </summary>
        public static int AddIslandTop(GenColliders gen, IRoadCorridorQuery corridors, double x0, double z0, double cx, double cz,
                                       double r, double y0, double y1)
        {
            int added = 0;
            for (int k = 0; k < IslandTopSquares; k++)
            {
                double a = k * IslandTopStepDeg * Math.PI / 180.0;
                double ux = Math.Cos(a), uz = Math.Sin(a);
                double half = r / Math.Sqrt(2.0);
                while (half >= 0.5 && !SquareClear(corridors, x0 + cx, z0 + cz, ux, uz, half)) half *= 0.92;
                if (half < 0.5) continue;
                gen.AddBox(cx, cz, y0, y1, half, half, ux, uz, GenColliderFlags.None, GenColliders.Grass);
                added++;
            }
            return added;
        }

        /// <summary>True when the corners and edge midpoints of a square (centre game metres, U axis, half side) are all
        /// outside the road corridors.</summary>
        public static bool SquareClear(IRoadCorridorQuery corridors, double gx, double gz, double ux, double uz, double half)
        {
            if (corridors == null) return true;
            double wx = -uz, wz = ux;
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    double px = gx + (i * ux + j * wx) * half, pz = gz + (i * uz + j * wz) * half;
                    if (corridors.SignedDistance(px, pz) < 0.0) return false;
                }
            }
            return true;
        }

        /// <summary>Pieces of a layer built so far.</summary>
        private int PieceCount(int layer)
        {
            int c = 0;
            for (int i = 0; i < Pieces.Count; i++)
                if (Pieces[i].Layer == layer) c++;
            return c;
        }

        private static void Grow(MeshData m, ref float minX, ref float minZ, ref float maxX, ref float maxZ)
        {
            float x0, y0, z0, x1, y1, z1;
            m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
            minX = Math.Min(minX, x0);
            minZ = Math.Min(minZ, z0);
            maxX = Math.Max(maxX, x1);
            maxZ = Math.Max(maxZ, z1);
        }

        private static void SetFootprint(ref LodPiece p, float minX, float minZ, float maxX, float maxZ)
        {
            p.CX = 0.5 * (minX + maxX);
            p.CZ = 0.5 * (minZ + maxZ);
            p.RadiusM = 0.5f * (float)Math.Sqrt((maxX - minX) * (maxX - minX) + (maxZ - minZ) * (maxZ - minZ));
        }

        /// <summary>
        /// The building bands (W2_DESIGN 2.4): B1 and B2 regrouped per 128 m block, B3 per 256 m block.
        /// Without <see cref="MeshingSettings.BuildingBands"/> only the W1 B1 layer is built (one part).
        /// </summary>
        private static void BuildBuildings(TileBuild b, TileData src, MeshingSettings settings)
        {
            if (!settings.BuildingBands)
            {
                BuildingMesher.Build(src, b.Sampler, settings.Buildings, b.Layers[TileLayers.Buildings]);
                return;
            }
            double size = src.Tile.Size;
            b._scratch.Clear();
            BuildingMesher.Build(src, b.Sampler, settings.Buildings, b._scratch);
            MeshParts.ByBlocks(b._scratch, size, BandConfig.B1BlockM, b.Layers[TileLayers.Buildings], b.Parts[TileLayers.Buildings], b._partScratch);
            b._scratch.Clear();
            BuildingMesher.Build(src, b.Sampler, settings.FarBuildings, b._scratch);
            MeshParts.ByBlocks(b._scratch, size, BandConfig.B2BlockM, b.Layers[TileLayers.BuildingsFar], b.Parts[TileLayers.BuildingsFar], b._partScratch);
            b._scratch.Clear();
            BuildingMesher.Build(src, b.Sampler, settings.BlockBuildings, b._scratch);
            MeshParts.ByBlocks(b._scratch, size, BandConfig.B3BlockM, b.Layers[TileLayers.BuildingsBlock], b.Parts[TileLayers.BuildingsBlock], b._partScratch);
        }

        /// <summary>Every hero anchored in this tile at LOD 0 to 3 (parts hero × 4 + lod) and its colliders. Without
        /// <paramref name="lod0"/> (Low, whose hero budget never shows LOD0: <see cref="BandConfig.HeroLod0Allowed"/>) LOD0 is
        /// neither built nor uploaded (about 167 k triangles, 8 MB and three quarters of the hero build time on the Asan
        /// tile) and the colliders come from LOD1, whose generators emit the same set (core-tests MeshingBuildingB0CellTests).</summary>
        private static void BuildHeroes(TileBuild b, TileData src, HeroSet heroes, bool lod0)
        {
            IReadOnlyList<HeritageRecord> list = heroes.InTile(src.Tile.Key);
            if (list.Count == 0) return;
            MeshData layer = b.Layers[TileLayers.Heroes];
            List<PartRange> parts = b.Parts[TileLayers.Heroes];
            var stats = new SacredStats();
            for (int h = 0; h < list.Count; h++)
            {
                HeritageRecord r = list[h];
                int index = b.Heroes.Count;
                int partsBefore = parts.Count, v0 = layer.VertexCount, i0 = layer.IndexCount;
                var piece = new HeroPiece { Id = r.Id, Index = index };
                try
                {
                    b._gen.Clear();
                    bool any = false;
                    float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                    float minY = float.MaxValue, maxY = float.MinValue;
                    int first = lod0 ? 0 : 1;
                    for (int lod = first; lod < HeroLodBudget.LodCount; lod++)
                    {
                        b._scratch.Clear();
                        int tris = HeroBuilder.Build(r, src, lod, b._scratch, lod == first ? b._gen : null, lod == first ? stats : null);
                        if (tris <= 0) continue;
                        any = true;
                        float x0, y0, z0, x1, y1, z1;
                        b._scratch.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                        minX = Math.Min(minX, x0);
                        minY = Math.Min(minY, y0);
                        minZ = Math.Min(minZ, z0);
                        maxX = Math.Max(maxX, x1);
                        maxY = Math.Max(maxY, y1);
                        maxZ = Math.Max(maxZ, z1);
                        MeshParts.AppendPart(b._scratch, layer, index * HeroLodBudget.LodCount + lod, parts);
                        switch (lod)
                        {
                            case 0: piece.Tris0 = tris; break;
                            case 1: piece.Tris1 = tris; break;
                            case 2: piece.Tris2 = tris; break;
                            default: piece.Tris3 = tris; break;
                        }
                    }
                    if (!any) continue;
                    piece.CX = 0.5 * (minX + maxX);
                    piece.CZ = 0.5 * (minZ + maxZ);
                    piece.GroundY = minY;
                    piece.TopY = maxY;
                    piece.RadiusM = 0.5f * (float)Math.Sqrt((maxX - minX) * (maxX - minX) + (maxZ - minZ) * (maxZ - minZ));
                    if (b._gen.Boxes.Count + b._gen.Ramps.Count + b._gen.Cylinders.Count + b._gen.Walls.Count > 0)
                    {
                        // Every generator collider: boxes and ramps, and the cylinders and walls of statues, piers and parapets.
                        if (b.HeroColliders == null) b.HeroColliders = new StructureColliders();
                        b.HeroColliders.AddFrom(b._gen);
                    }
                    b.Heroes.Add(piece);
                }
                catch (Exception)
                {
                    // A broken recipe must not take the tile down: drop what it added.
                    b.HeroFailures++;
                    parts.RemoveRange(partsBefore, parts.Count - partsBefore);
                    layer.VertexCount = v0;
                    layer.IndexCount = i0;
                }
            }
        }

        /// <summary>Trees (OSM, avenues, forest clumps), street props and parked vehicles, indexed by 64 m cell, and the
        /// colliders of what is solid among them (<see cref="TileInstances.AppendColliders"/>).</summary>
        private static void BuildInstances(TileBuild b, TileData src, MeshingSettings settings)
        {
            var inst = new TileInstances();
            TreePlacement.Place(src, b.Sampler, settings.Trees, inst.Trees);
            if (b.HeroColliders != null)
            {
                IRoadCorridorQuery corridors = settings.Trees != null && settings.Trees.Corridors != null ? settings.Trees.Corridors(src) : null;
                KeepTreesOffHeroes(inst.Trees, b.HeroColliders, src, b.Sampler, corridors);
            }
            PropPlacement.Place(src, b.Sampler, inst.Props);
            ParkedPlacement.Place(src, b.Sampler, inst.Parked, settings.Roads, inst.Trees, inst.Props);
            inst.Index(src.Tile.Size);
            b.Instances = inst;
            if (b.Colliders == null) b.Colliders = new StructureColliders();
            inst.AppendColliders(b.Colliders);
            if (b.Colliders.Count == 0) b.Colliders = null;
        }

        /// <summary>Clearance (m) a tree trunk keeps from a hero's plinth, walls and statues.</summary>
        public const float HeroTreeMarginM = 1.0f;

        /// <summary>Farthest (m) a mapped tree standing on a hero is moved to stand beside it; farther, it is dropped.</summary>
        public const float HeroTreeNudgeM = 8.0f;

        /// <summary>
        /// Keeps trees off the hero replicas (the heroes' LOD0 colliders, tile-local: plinth stages, walls, stairs and
        /// statues standing above the tree's ground): temple plinths and the paved squares round them carry no trees in
        /// the real squares (Taleju's twelve-stage plinth in Hanuman Dhoka, Basantapur), and a mapped tree whose point
        /// falls on a plinth stands beside it. A mapped (OSM) tree is moved just outside the structure it stands on
        /// (at most <see cref="HeroTreeNudgeM"/>, never into another hero structure or a road corridor, given in
        /// tile-local metres) and set on the terrain there; a generated plant, or a mapped tree that cannot be moved, is
        /// dropped. Low paving flush with the ground keeps its trees. Returns the trees moved or dropped.
        /// </summary>
        public static int KeepTreesOffHeroes(List<TreeInstance> trees, StructureColliders heroes, TileData src, TileHeightSampler h,
                                             IRoadCorridorQuery corridors)
        {
            if (trees == null || heroes == null || heroes.Count == 0) return 0;
            int changed = 0, w = 0;
            for (int i = 0; i < trees.Count; i++)
            {
                TreeInstance t = trees[i];
                double x = t.X, z = t.Z;
                if (OnHero(heroes, x, z, t.Y, HeroTreeMarginM))
                {
                    changed++;
                    if (t.Origin != TreeOrigin.Osm || !NudgeOffHero(heroes, ref x, ref z, t.Y) ||
                        corridors != null && corridors.SignedDistance(x, z) < 1.2)
                        continue; // dropped
                    float y;
                    if (h != null && h.TryHeightClamped(src.Tile.X0 + x, src.Tile.Z0 + z, out y)) t.Y = y;
                    t.X = (float)x;
                    t.Z = (float)z;
                }
                trees[w++] = t;
            }
            if (w < trees.Count) trees.RemoveRange(w, trees.Count - w);
            return changed;
        }

        /// <summary>True when a trunk at (x, z) with its foot at <paramref name="footY"/> stands within
        /// <paramref name="margin"/> of a hero box, ramp or cylinder rising more than 0.3 m above its foot.</summary>
        private static bool OnHero(StructureColliders c, double x, double z, float footY, double margin)
        {
            float above = footY + 0.3f;
            for (int i = 0; i < c.Boxes.Count; i++)
            {
                OrientedBox b = c.Boxes[i];
                if (b.Top <= above) continue;
                double dx = x - b.CX, dz = z - b.CZ, cs = Math.Cos(b.YawRad), sn = Math.Sin(b.YawRad);
                double lx = dx * cs + dz * sn, lz = -dx * sn + dz * cs;
                if (Math.Abs(lx) < b.HalfX + margin && Math.Abs(lz) < b.HalfZ + margin) return true;
            }
            for (int i = 0; i < c.Cylinders.Count; i++)
            {
                ColliderCylinder y = c.Cylinders[i];
                if (y.Top <= above) continue;
                double dx = x - y.CX, dz = z - y.CZ, r = y.Radius + margin;
                if (dx * dx + dz * dz < r * r) return true;
            }
            for (int i = 0; i < c.Ramps.Count; i++)
            {
                StepRamp r = c.Ramps[i];
                if (Math.Max(r.Y0, r.Y1) <= above) continue;
                double ax = r.X1 - r.X0, az = r.Z1 - r.Z0, l2 = ax * ax + az * az;
                double f = l2 > 1e-9 ? ((x - r.X0) * ax + (z - r.Z0) * az) / l2 : 0.0;
                if (f < 0 || f > 1) continue;
                double qx = r.X0 + ax * f - x, qz = r.Z0 + az * f - z, hw = r.HalfWidth + margin;
                if (qx * qx + qz * qz < hw * hw) return true;
            }
            return false;
        }

        /// <summary>Moves (x, z) out of the hero box or cylinder it stands in, through the nearest side, to
        /// <see cref="HeroTreeMarginM"/> beyond it; false when that is farther than <see cref="HeroTreeNudgeM"/> or the
        /// spot is on another hero structure.</summary>
        private static bool NudgeOffHero(StructureColliders c, ref double x, ref double z, float footY)
        {
            float above = footY + 0.3f;
            double margin = HeroTreeMarginM + 0.05;
            for (int pass = 0; pass < 3; pass++)
            {
                bool moved = false;
                for (int i = 0; i < c.Boxes.Count && !moved; i++)
                {
                    OrientedBox b = c.Boxes[i];
                    if (b.Top <= above) continue;
                    double dx = x - b.CX, dz = z - b.CZ, cs = Math.Cos(b.YawRad), sn = Math.Sin(b.YawRad);
                    double lx = dx * cs + dz * sn, lz = -dx * sn + dz * cs;
                    double ox = b.HalfX + margin - Math.Abs(lx), oz = b.HalfZ + margin - Math.Abs(lz);
                    if (ox <= 0 || oz <= 0) continue;
                    if (ox < oz) lx = (lx < 0 ? -1 : 1) * (b.HalfX + margin);
                    else lz = (lz < 0 ? -1 : 1) * (b.HalfZ + margin);
                    double nx = b.CX + lx * cs - lz * sn, nz = b.CZ + lx * sn + lz * cs;
                    if (Math.Min(ox, oz) > HeroTreeNudgeM) return false;
                    x = nx;
                    z = nz;
                    moved = true;
                }
                for (int i = 0; i < c.Cylinders.Count && !moved; i++)
                {
                    ColliderCylinder y = c.Cylinders[i];
                    if (y.Top <= above) continue;
                    double dx = x - y.CX, dz = z - y.CZ, d = Math.Sqrt(dx * dx + dz * dz), r = y.Radius + margin;
                    if (d >= r) continue;
                    if (r - d > HeroTreeNudgeM) return false;
                    if (d < 1e-6)
                    {
                        dx = 1;
                        dz = 0;
                        d = 1;
                    }
                    x = y.CX + dx / d * r;
                    z = y.CZ + dz / d * r;
                    moved = true;
                }
                if (!moved) return !OnHero(c, x, z, footY, HeroTreeMarginM);
            }
            return !OnHero(c, x, z, footY, HeroTreeMarginM);
        }

        /// <summary>
        /// Split one layer into upload chunks: consecutive runs of its triangles whose vertices span at most
        /// <paramref name="maxVertices"/> (the meshers emit each feature's vertices together, so runs stay compact).
        /// Rebases the layer's <see cref="MeshData.Indices"/> in place to their chunk's <see cref="UploadChunk.FirstVertex"/>
        /// and writes the 16-bit copies into <paramref name="shortIndices"/> (grown when too small). A triangle spanning
        /// more vertices than that gets a chunk of its own (32-bit indices when it spans more than 65 535). Appends to
        /// <paramref name="chunks"/>; an empty layer adds nothing. Allocation-free once the buffers have grown.
        /// </summary>
        public static void SplitLayer(MeshData m, int layer, int maxVertices, List<UploadChunk> chunks, ref ushort[] shortIndices)
        {
            SplitLayer(m, layer, maxVertices, long.MaxValue, chunks, ref shortIndices);
        }

        /// <summary>
        /// <see cref="SplitLayer(MeshData, int, int, List{UploadChunk}, ref ushort[])"/> that also ends a chunk before its
        /// <see cref="UploadChunk.Bytes"/> would exceed <paramref name="maxBytes"/> (vertex streams of the layer's format plus
        /// 16-bit indices), so the upload size per chunk is bounded whatever the vertex format.
        /// </summary>
        public static void SplitLayer(MeshData m, int layer, int maxVertices, long maxBytes, List<UploadChunk> chunks, ref ushort[] shortIndices)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int n = m.IndexCount - m.IndexCount % 3;
            if (n <= 0) return;
            SplitLayer(m, layer, new[] { new PartRange(0, 0, n) }, maxVertices, maxBytes, chunks, ref shortIndices);
        }

        /// <summary>
        /// <see cref="SplitLayer(MeshData, int, int, List{UploadChunk}, ref ushort[])"/> over the layer's parts: a chunk
        /// never spans two parts, and each chunk carries its part.
        /// </summary>
        public static void SplitLayer(MeshData m, int layer, IReadOnlyList<PartRange> parts, int maxVertices, List<UploadChunk> chunks,
                                      ref ushort[] shortIndices)
        {
            SplitLayer(m, layer, parts, maxVertices, long.MaxValue, chunks, ref shortIndices);
        }

        /// <summary>
        /// <see cref="SplitLayer(MeshData, int, IReadOnlyList{PartRange}, int, List{UploadChunk}, ref ushort[])"/> with a
        /// byte cap per chunk (<see cref="SplitLayer(MeshData, int, int, long, List{UploadChunk}, ref ushort[])"/>).
        /// </summary>
        public static void SplitLayer(MeshData m, int layer, IReadOnlyList<PartRange> parts, int maxVertices, long maxBytes,
                                      List<UploadChunk> chunks, ref ushort[] shortIndices)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));
            int total = m.IndexCount - m.IndexCount % 3;
            if (total <= 0) return;
            if (maxVertices < 3) maxVertices = 3;
            int[] idx = m.Indices;
            int firstChunk = chunks.Count;
            long vertexBytes = 12 + 12 + 4 + (m.HasUv0 ? 8 : 0);
            for (int pi = 0; pi < parts.Count; pi++)
            {
                PartRange pr = parts[pi];
                int end = Math.Min(total, pr.FirstIndex + pr.IndexCount - pr.IndexCount % 3);
                if (end <= pr.FirstIndex) continue;
                int first = pr.FirstIndex, vmin = int.MaxValue, vmax = -1;
                for (int t = pr.FirstIndex; t < end; t += 3)
                {
                    int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                    int tmin = Math.Min(a, Math.Min(b, c)), tmax = Math.Max(a, Math.Max(b, c));
                    int nmin = Math.Min(vmin, tmin), nmax = Math.Max(vmax, tmax);
                    if (t > first && (nmax - nmin >= maxVertices || ChunkBytes(nmax - nmin + 1, t + 3 - first, vertexBytes) > maxBytes))
                    {
                        chunks.Add(MakeChunk(m, layer, pr.Part, first, t - first, vmin, vmax));
                        first = t;
                        nmin = tmin;
                        nmax = tmax;
                    }
                    vmin = nmin;
                    vmax = nmax;
                }
                chunks.Add(MakeChunk(m, layer, pr.Part, first, end - first, vmin, vmax));
            }

            if (shortIndices == null || shortIndices.Length < total)
                shortIndices = new ushort[Math.Max(total, shortIndices == null ? 3072 : shortIndices.Length * 2)];
            ushort[] s = shortIndices;
            for (int k = firstChunk; k < chunks.Count; k++)
            {
                UploadChunk ch = chunks[k];
                int v0 = ch.FirstVertex;
                bool small = ch.UsesShortIndices;
                for (int i = ch.FirstIndex, end = ch.FirstIndex + ch.IndexCount; i < end; i++)
                {
                    int r = idx[i] - v0;
                    idx[i] = r;
                    if (small) s[i] = (ushort)r;
                }
            }
        }

        /// <summary>Bytes of a chunk spanning <paramref name="vertices"/> with <paramref name="indices"/> (as
        /// <see cref="UploadChunk.Bytes"/>).</summary>
        private static long ChunkBytes(int vertices, int indices, long vertexBytes)
        {
            return vertices * vertexBytes + (long)indices * (vertices <= 65535 ? 2 : 4);
        }

        private static UploadChunk MakeChunk(MeshData m, int layer, int part, int firstIndex, int indexCount, int vmin, int vmax)
        {
            var c = new UploadChunk
            {
                Layer = layer,
                Part = part,
                FirstIndex = firstIndex,
                IndexCount = indexCount,
                FirstVertex = vmin,
                VertexCount = vmax - vmin + 1,
            };
            float[] p = m.Positions;
            int q = vmin * 3;
            c.MinX = c.MaxX = p[q];
            c.MinY = c.MaxY = p[q + 1];
            c.MinZ = c.MaxZ = p[q + 2];
            for (q += 3; q <= vmax * 3; q += 3)
            {
                float x = p[q], y = p[q + 1], z = p[q + 2];
                if (x < c.MinX) c.MinX = x;
                else if (x > c.MaxX) c.MaxX = x;
                if (y < c.MinY) c.MinY = y;
                else if (y > c.MaxY) c.MaxY = y;
                if (z < c.MinZ) c.MinZ = z;
                else if (z > c.MaxZ) c.MaxZ = z;
            }
            return c;
        }
    }

    /// <summary>
    /// The road and road-decal layers per block (perf pass; W2_DESIGN 10.4 gives roads and decals a slice of 8 k / 20 k /
    /// 30 k triangles in view). Both layers are regrouped per <see cref="BlockM"/> block of the tile, like the B1 and B2
    /// band layers, so every block uploads as its own chunks and the camera frustum culls the blocks behind it. The road
    /// layer holds two copies of every block: the full kit (part = block) and a cheap ribbon copy (part =
    /// <see cref="FarPart"/> + block, meshed with <see cref="FarOptions"/>: the light kit, no trails, no footpaths). The streamer
    /// (<c>WorldStreamer.UpdateBands</c>) shows a block's full copy and its markings while the block lies within the B1
    /// radius (<see cref="Radii"/>: 120 / 200 / 250 m), the cheap copy beyond it out to 250 m on Low and the B2 radius
    /// on Mid and High (500 / 700 m), and no road beyond that, where the B2 and B3 blocks stand over the streets. Roads therefore stay smooth, wide and
    /// fully detailed around the player, and only what is far or off-screen is cut.
    /// <para>
    /// Triangles crossing a block line are clipped at the line (<see cref="Append"/>), so the full copy of one block and
    /// the cheap copy of its neighbour meet exactly on the line: no gap and no doubled, z-fighting sliver where a street
    /// crosses the switch. Engine-free and deterministic; allocation-free once the scratch buffers have grown.
    /// </para>
    /// </summary>
    public static class RoadBlocks
    {
        /// <summary>Block side (m): two by two B0 cells, the B1 and B2 block.</summary>
        public const double BlockM = 128.0;

        /// <summary>Part offset of the cheap far copy in <see cref="TileLayers.Roads"/> (part = FarPart + block).</summary>
        public const int FarPart = 4096;

        /// <summary>A block keeps its state until the camera is this much (m) beyond the radius that switched it, so a
        /// player standing on the line never makes it flicker.</summary>
        public const float HysteresisM = 8f;

        /// <summary>True for a part of the cheap far copy.</summary>
        public static bool IsFar(int part)
        {
            return part >= FarPart;
        }

        /// <summary>The block of a road or decal part.</summary>
        public static int BlockOfPart(int part)
        {
            return part >= FarPart ? part - FarPart : part;
        }

        /// <summary>The tier's radii (m, horizontal from the camera to the nearest point of a block): the full kit and the
        /// markings within <paramref name="nearM"/> (B1's outer radius), the cheap copy out to <paramref name="farM"/>
        /// (B2's outer radius, or the config's <see cref="StreamingConfig.RoadFarRadiusM"/> when that is nearer: Low's
        /// 250 m; never inside the near radius).</summary>
        public static void Radii(BandConfig bands, StreamingConfig config, out float nearM, out float farM)
        {
            if (bands == null) throw new ArgumentNullException(nameof(bands));
            nearM = bands.B1OuterM;
            farM = bands.B2OuterM;
            if (config != null && config.RoadFarRadiusM > 0) farM = Math.Max(nearM, Math.Min(farM, config.RoadFarRadiusM));
        }

        /// <summary>The square of a block (tile-local metres).</summary>
        public static void Square(int block, double tileSizeM, out double minX, out double minZ, out double maxX, out double maxZ)
        {
            int per = MeshParts.BlocksPerSide(tileSizeM, BlockM);
            int bx = block % per, bz = block / per;
            minX = bx * BlockM;
            minZ = bz * BlockM;
            maxX = minX + BlockM;
            maxZ = minZ + BlockM;
        }

        /// <summary>Whether a block at <paramref name="distanceM"/> is inside a radius, with <see cref="HysteresisM"/>:
        /// a block that was inside stays inside until it is that much beyond.</summary>
        public static bool Inside(bool wasInside, double distanceM, float radiusM)
        {
            return distanceM <= (wasInside ? radiusM + HysteresisM : radiusM);
        }

        /// <summary>Blocks a view tracks in its masks (<see cref="UpdateMasks"/>): a 1 km tile has 8 × 8.</summary>
        public const int MaxTrackedBlocks = 64;

        /// <summary>
        /// One frame's road-block rule for a detail view (the streamer's per-frame decision, shared with the ride harness
        /// so both answer the same): bit k of <paramref name="near"/> is set while block k is within
        /// <paramref name="nearM"/> of the camera at tile-local (<paramref name="cx"/>, <paramref name="cz"/>) (its full
        /// road kit and markings show), bit k of <paramref name="far"/> while it is within <paramref name="farM"/> (some
        /// road shows), each with <see cref="HysteresisM"/> against the masks passed in. Blocks past the 64th, which no
        /// 1 km tile has, are not tracked (<see cref="BlockIn"/> measures them each time). No allocation.
        /// </summary>
        public static void UpdateMasks(double tileSizeM, double cx, double cz, float nearM, float farM, ref ulong near, ref ulong far)
        {
            int per = MeshParts.BlocksPerSide(tileSizeM, BlockM);
            int blocks = Math.Min(per * per, MaxTrackedBlocks);
            ulong n = 0, f = 0;
            for (int k = 0; k < blocks; k++)
            {
                double x0, z0, x1, z1;
                Square(k, tileSizeM, out x0, out z0, out x1, out z1);
                double d = BandConfig.NearestDistance(x0, z0, x1, z1, cx, cz);
                ulong bit = 1UL << k;
                if (Inside((near & bit) != 0, d, nearM)) n |= bit;
                if (Inside((far & bit) != 0, d, farM)) f |= bit;
            }
            near = n;
            far = f;
        }

        /// <summary>Whether block <paramref name="block"/> is within a radius by the mask <see cref="UpdateMasks"/> keeps
        /// for it (blocks past <see cref="MaxTrackedBlocks"/> by their distance, without hysteresis).</summary>
        public static bool BlockIn(ulong mask, int block, double tileSizeM, double cx, double cz, float radiusM)
        {
            if (block < MaxTrackedBlocks) return (mask & (1UL << block)) != 0;
            double x0, z0, x1, z1;
            Square(block, tileSizeM, out x0, out z0, out x1, out z1);
            return BandConfig.NearestDistance(x0, z0, x1, z1, cx, cz) <= radiusM;
        }

        /// <summary>Whether the road chunk of part <paramref name="part"/> shows: the full kit's within the near radius,
        /// the cheap copy's between the near and the far radius, or the full kit's out to the far radius when the view
        /// has no cheap copy (<paramref name="hasFarCopy"/> false). Road decals show with their block's full kit
        /// (<see cref="BlockIn"/> with the near mask).</summary>
        public static bool PartShown(int part, ulong near, ulong far, bool hasFarCopy, double tileSizeM, double cx, double cz, float nearM, float farM)
        {
            int block = BlockOfPart(part);
            bool isNear = BlockIn(near, block, tileSizeM, cx, cz, nearM);
            if (IsFar(part)) return !isNear && BlockIn(far, block, tileSizeM, cx, cz, farM);
            return isNear || !hasFarCopy && BlockIn(far, block, tileSizeM, cx, cz, farM);
        }

        /// <summary>The options of the cheap far copy, a ribbon-only copy of <paramref name="near"/>: the light kit
        /// (<see cref="RoadOptions.Detail"/> off), no trails (footways, paths, steps and tracks are thinner than a pixel
        /// that far) and no cross-section kit (<see cref="RoadOptions.CrossSections"/> off: the raised footpaths, medians
        /// and shoulders give way to the plain rounded skirt). The carriageway widths, curves, lifts, vertical profile and
        /// junction caps stay the same, so the two copies' carriageways meet at a block line and no junction opens a
        /// hole.</summary>
        public static RoadOptions FarOptions(RoadOptions near)
        {
            if (near == null) near = new RoadOptions();
            return new RoadOptions
            {
                LiftM = near.LiftM, ClassLiftStepM = near.ClassLiftStepM, PieceLiftStepM = near.PieceLiftStepM, PieceLiftLevels = near.PieceLiftLevels,
                MaxSegmentM = near.MaxSegmentM, Drape = near.Drape, IncludeTrails = false, CentreLighten = near.CentreLighten,
                MaxMiter = near.MaxMiter, WidthModel = near.WidthModel, CrossSections = false, JunctionCaps = near.JunctionCaps,
                KerbHeightM = near.KerbHeightM, MedianHeightM = near.MedianHeightM, Detail = false, SmoothProfile = near.SmoothProfile,
                PolicePodiums = near.PolicePodiums,
            };
        }

        /// <summary>Scratch buffers of <see cref="Append"/> (one per worker; reuse across calls).</summary>
        public sealed class Scratch
        {
            internal int[] TriBlock = new int[0];
            internal int[] Order = new int[0];
            internal int[] Counts = new int[0];
            internal int[] CrossCounts = new int[0];
            internal int[] Cross = new int[0];
            internal int[] Map = new int[0];
            internal int[] Stamp = new int[0];
            internal int StampValue;

            // Clipping: polygons of at most 3 + 4 points, Stride floats each (position, normal, colour, uv), and the
            // source vertex of each point (-1 for a point on a block line).
            internal readonly float[] PolyA = new float[16 * Stride];
            internal readonly float[] PolyB = new float[16 * Stride];
            internal readonly int[] SrcA = new int[16];
            internal readonly int[] SrcB = new int[16];
            internal readonly int[] Out = new int[16];
        }

        private const int Stride = 12;

        /// <summary>
        /// Appends the triangles of <paramref name="src"/> to <paramref name="dst"/> grouped by block (row-major from the
        /// south-west), one <see cref="PartRange"/> per non-empty block with part = <paramref name="partBase"/> + block.
        /// A triangle inside one block is copied (its vertices shared within the block); a triangle crossing a block line
        /// is clipped to every block it touches (half-open blocks [k × blockM, (k + 1) × blockM), the tile's outer blocks
        /// open towards the outside) and each piece fanned with interpolated normals, colours and UV0. The pieces cover
        /// the triangle exactly once. Returns the parts added.
        /// </summary>
        public static int Append(MeshData src, double tileSizeM, double blockM, int partBase, MeshData dst, List<PartRange> parts, Scratch s)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (s == null) throw new ArgumentNullException(nameof(s));
            int triCount = src.IndexCount / 3;
            if (triCount == 0) return 0;
            int per = MeshParts.BlocksPerSide(tileSizeM, blockM);
            int blocks = per * per;
            if (s.TriBlock.Length < triCount)
            {
                s.TriBlock = new int[Math.Max(triCount, s.TriBlock.Length * 2)];
                s.Order = new int[s.TriBlock.Length];
            }
            if (s.Counts.Length < blocks + 1)
            {
                s.Counts = new int[blocks + 1];
                s.CrossCounts = new int[blocks + 1];
            }
            if (s.Map.Length < src.VertexCount)
            {
                int cap = Math.Max(src.VertexCount, s.Map.Length * 2);
                s.Map = new int[cap];
                s.Stamp = new int[cap];
                s.StampValue = 0;
            }
            Array.Clear(s.Counts, 0, blocks + 1);
            Array.Clear(s.CrossCounts, 0, blocks + 1);
            int[] idx = src.Indices;
            float[] p = src.Positions;
            // Pass 1: whole triangles per block; crossing triangles counted once per block their bounds touch.
            int pairs = 0;
            for (int t = 0; t < triCount; t++)
            {
                int a = idx[3 * t] * 3, b = idx[3 * t + 1] * 3, c = idx[3 * t + 2] * 3;
                // Inside the closed square of its centroid's block (a vertex on a block line counts for both sides, so
                // triangles of the terrain-grid drape that end on a line stay whole).
                int kx = Cell((p[a] + p[b] + p[c]) / 3f, blockM, per), kz = Cell((p[a + 2] + p[b + 2] + p[c + 2]) / 3f, blockM, per);
                if (InSquare(p, a, kx, kz, per, blockM) && InSquare(p, b, kx, kz, per, blockM) && InSquare(p, c, kx, kz, per, blockM))
                {
                    int k = kz * per + kx;
                    s.TriBlock[t] = k;
                    s.Counts[k + 1]++;
                    continue;
                }
                int ax = Cell(p[a], blockM, per), az = Cell(p[a + 2], blockM, per);
                int bx = Cell(p[b], blockM, per), bz = Cell(p[b + 2], blockM, per);
                int cx = Cell(p[c], blockM, per), cz = Cell(p[c + 2], blockM, per);
                s.TriBlock[t] = -1;
                int x0 = Math.Min(ax, Math.Min(bx, cx)), x1 = Math.Max(ax, Math.Max(bx, cx));
                int z0 = Math.Min(az, Math.Min(bz, cz)), z1 = Math.Max(az, Math.Max(bz, cz));
                for (int zz = z0; zz <= z1; zz++)
                for (int xx = x0; xx <= x1; xx++)
                {
                    s.CrossCounts[zz * per + xx + 1]++;
                    pairs++;
                }
            }
            for (int k = 0; k < blocks; k++)
            {
                s.Counts[k + 1] += s.Counts[k];
                s.CrossCounts[k + 1] += s.CrossCounts[k];
            }
            if (s.Cross.Length < pairs) s.Cross = new int[Math.Max(pairs, s.Cross.Length * 2)];
            // Pass 2: counting sorts (stable: triangles keep their order inside a block).
            for (int t = 0; t < triCount; t++)
            {
                int k = s.TriBlock[t];
                if (k >= 0)
                {
                    s.Order[s.Counts[k]++] = t;
                    continue;
                }
                int a = idx[3 * t] * 3, b = idx[3 * t + 1] * 3, c = idx[3 * t + 2] * 3;
                int x0 = Math.Min(Cell(p[a], blockM, per), Math.Min(Cell(p[b], blockM, per), Cell(p[c], blockM, per)));
                int x1 = Math.Max(Cell(p[a], blockM, per), Math.Max(Cell(p[b], blockM, per), Cell(p[c], blockM, per)));
                int z0 = Math.Min(Cell(p[a + 2], blockM, per), Math.Min(Cell(p[b + 2], blockM, per), Cell(p[c + 2], blockM, per)));
                int z1 = Math.Max(Cell(p[a + 2], blockM, per), Math.Max(Cell(p[b + 2], blockM, per), Cell(p[c + 2], blockM, per)));
                for (int zz = z0; zz <= z1; zz++)
                for (int xx = x0; xx <= x1; xx++)
                    s.Cross[s.CrossCounts[zz * per + xx]++] = t;
            }
            // Counts[k] and CrossCounts[k] now hold the ends of block k; the starts are the previous ends.
            if (src.HasUv0 && !dst.HasUv0)
            {
                Array.Clear(dst.Uv0, 0, Math.Min(dst.Uv0.Length, dst.VertexCount * 2));
                dst.HasUv0 = true;
            }
            dst.Reserve(src.VertexCount + src.VertexCount / 8, src.IndexCount + src.IndexCount / 8);
            int added = 0, start = 0, crossStart = 0;
            for (int k = 0; k < blocks; k++)
            {
                int end = s.Counts[k], crossEnd = s.CrossCounts[k];
                if (end <= start && crossEnd <= crossStart)
                {
                    start = end;
                    crossStart = crossEnd;
                    continue;
                }
                if (++s.StampValue == int.MaxValue)
                {
                    Array.Clear(s.Stamp, 0, s.Stamp.Length);
                    s.StampValue = 1;
                }
                int stamp = s.StampValue;
                int firstIndex = dst.IndexCount;
                for (int o = start; o < end; o++)
                {
                    int t = s.Order[o];
                    int ia = Copy(src, dst, idx[3 * t], s, stamp);
                    int ib = Copy(src, dst, idx[3 * t + 1], s, stamp);
                    int ic = Copy(src, dst, idx[3 * t + 2], s, stamp);
                    dst.AddTriangle(ia, ib, ic);
                }
                int bx = k % per, bz = k / per;
                for (int o = crossStart; o < crossEnd; o++) Clip(src, s.Cross[o], bx, bz, per, blockM, dst, s, stamp);
                if (dst.IndexCount > firstIndex)
                {
                    parts.Add(new PartRange(partBase + k, firstIndex, dst.IndexCount - firstIndex));
                    added++;
                }
                start = end;
                crossStart = crossEnd;
            }
            return added;
        }

        /// <summary>Block column (or row) of a coordinate, clamped into the tile (half-open cells).</summary>
        private static int Cell(float v, double blockM, int per)
        {
            int c = (int)Math.Floor(v / blockM);
            return c < 0 ? 0 : c >= per ? per - 1 : c;
        }

        /// <summary>True when vertex <paramref name="q"/> (offset into the positions) lies in the closed square of block
        /// (kx, kz), the tile's outer blocks open towards the outside.</summary>
        private static bool InSquare(float[] p, int q, int kx, int kz, int per, double blockM)
        {
            double x = p[q], z = p[q + 2];
            if (kx > 0 && x < kx * blockM || kx < per - 1 && x > (kx + 1) * blockM) return false;
            if (kz > 0 && z < kz * blockM || kz < per - 1 && z > (kz + 1) * blockM) return false;
            return true;
        }

        private static int Copy(MeshData src, MeshData dst, int v, Scratch s, int stamp)
        {
            if (s.Stamp[v] == stamp) return s.Map[v];
            int n = dst.VertexCount;
            if ((n + 1) * 3 > dst.Positions.Length) dst.Reserve(Math.Max(64, n / 2), 0);
            Array.Copy(src.Positions, v * 3, dst.Positions, n * 3, 3);
            Array.Copy(src.Normals, v * 3, dst.Normals, n * 3, 3);
            Array.Copy(src.Colors, v * 4, dst.Colors, n * 4, 4);
            if (dst.HasUv0)
            {
                if (src.HasUv0) Array.Copy(src.Uv0, v * 2, dst.Uv0, n * 2, 2);
                else dst.Uv0[n * 2] = dst.Uv0[n * 2 + 1] = 0f;
            }
            dst.VertexCount = n + 1;
            s.Stamp[v] = stamp;
            s.Map[v] = n;
            return n;
        }

        /// <summary>Clips triangle <paramref name="t"/> to block (bx, bz) and fans the piece into <paramref name="dst"/>
        /// (corners that are vertices of the source share the block's copy of that vertex).</summary>
        private static void Clip(MeshData src, int t, int bx, int bz, int per, double blockM, MeshData dst, Scratch s, int stamp)
        {
            float[] a = s.PolyA, b = s.PolyB;
            int[] sa = s.SrcA, sb = s.SrcB;
            int n = 0;
            for (int q = 0; q < 3; q++)
            {
                sa[n] = src.Indices[3 * t + q];
                Load(src, sa[n], a, n++);
            }
            // Half-open block: x >= x0 (not for the first column), x < x1 (not for the last); the same along z.
            if (bx > 0) Swap(ref a, ref b, ref sa, ref sb, ref n, ClipPlane(a, sa, n, b, sb, 0, (float)(bx * blockM), false));
            if (n >= 3 && bx < per - 1) Swap(ref a, ref b, ref sa, ref sb, ref n, ClipPlane(a, sa, n, b, sb, 0, (float)((bx + 1) * blockM), true));
            if (n >= 3 && bz > 0) Swap(ref a, ref b, ref sa, ref sb, ref n, ClipPlane(a, sa, n, b, sb, 2, (float)(bz * blockM), false));
            if (n >= 3 && bz < per - 1) Swap(ref a, ref b, ref sa, ref sb, ref n, ClipPlane(a, sa, n, b, sb, 2, (float)((bz + 1) * blockM), true));
            if (n < 3) return;
            bool any = false;
            for (int i = 1; i + 1 < n && !any; i++) any = !Degenerate(a, 0, i, i + 1);
            if (!any) return;
            int[] o = s.Out;
            dst.Reserve(n, 3 * (n - 2));
            for (int i = 0; i < n; i++) o[i] = sa[i] >= 0 ? Copy(src, dst, sa[i], s, stamp) : Store(a, i, dst);
            for (int i = 1; i + 1 < n; i++)
            {
                if (Degenerate(a, 0, i, i + 1)) continue;
                dst.AddTriangle(o[0], o[i], o[i + 1]);
            }
        }

        private static void Swap(ref float[] a, ref float[] b, ref int[] sa, ref int[] sb, ref int n, int count)
        {
            float[] t = a;
            a = b;
            b = t;
            int[] u = sa;
            sa = sb;
            sb = u;
            n = count;
        }

        /// <summary>Sutherland-Hodgman against one axis plane: keeps <c>v &lt; c</c> (<paramref name="keepBelow"/>) or
        /// <c>v ≥ c</c>; returns the points written to <paramref name="dst"/> (their source vertex in
        /// <paramref name="dstSrc"/>, -1 for points made on the plane).</summary>
        private static int ClipPlane(float[] src, int[] srcSrc, int n, float[] dst, int[] dstSrc, int axis, float c, bool keepBelow)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float vi = src[i * Stride + axis], vj = src[j * Stride + axis];
                bool inI = keepBelow ? vi < c : vi >= c, inJ = keepBelow ? vj < c : vj >= c;
                if (inI)
                {
                    Array.Copy(src, i * Stride, dst, m * Stride, Stride);
                    dstSrc[m++] = srcSrc[i];
                }
                if (inI != inJ)
                {
                    float f = (c - vi) / (vj - vi);
                    int o = m * Stride, pi = i * Stride, pj = j * Stride;
                    for (int k = 0; k < Stride; k++) dst[o + k] = src[pi + k] + (src[pj + k] - src[pi + k]) * f;
                    dst[o + axis] = c;
                    dstSrc[m++] = -1;
                }
            }
            return m;
        }

        private static void Load(MeshData m, int v, float[] poly, int i)
        {
            int o = i * Stride;
            poly[o] = m.Positions[v * 3];
            poly[o + 1] = m.Positions[v * 3 + 1];
            poly[o + 2] = m.Positions[v * 3 + 2];
            poly[o + 3] = m.Normals[v * 3];
            poly[o + 4] = m.Normals[v * 3 + 1];
            poly[o + 5] = m.Normals[v * 3 + 2];
            poly[o + 6] = m.Colors[v * 4];
            poly[o + 7] = m.Colors[v * 4 + 1];
            poly[o + 8] = m.Colors[v * 4 + 2];
            poly[o + 9] = m.Colors[v * 4 + 3];
            poly[o + 10] = m.HasUv0 ? m.Uv0[v * 2] : 0f;
            poly[o + 11] = m.HasUv0 ? m.Uv0[v * 2 + 1] : 0f;
        }

        private static int Store(float[] poly, int i, MeshData m)
        {
            int o = i * Stride, v = m.VertexCount;
            if ((v + 1) * 3 > m.Positions.Length) m.Reserve(Math.Max(64, v / 2), 0);
            float nx = poly[o + 3], ny = poly[o + 4], nz = poly[o + 5];
            float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len > 1e-6f)
            {
                nx /= len;
                ny /= len;
                nz /= len;
            }
            m.Positions[v * 3] = poly[o];
            m.Positions[v * 3 + 1] = poly[o + 1];
            m.Positions[v * 3 + 2] = poly[o + 2];
            m.Normals[v * 3] = nx;
            m.Normals[v * 3 + 1] = ny;
            m.Normals[v * 3 + 2] = nz;
            for (int k = 0; k < 4; k++)
            {
                float c = poly[o + 6 + k] + 0.5f;
                m.Colors[v * 4 + k] = (byte)(c <= 0f ? 0 : c >= 255f ? 255 : (int)c);
            }
            if (m.HasUv0)
            {
                m.Uv0[v * 2] = poly[o + 10];
                m.Uv0[v * 2 + 1] = poly[o + 11];
            }
            m.VertexCount = v + 1;
            return v;
        }

        /// <summary>True when the triangle (i, j, k) of a polygon has (almost) no area.</summary>
        private static bool Degenerate(float[] poly, int i, int j, int k)
        {
            int a = i * Stride, b = j * Stride, c = k * Stride;
            double ux = poly[b] - poly[a], uy = poly[b + 1] - poly[a + 1], uz = poly[b + 2] - poly[a + 2];
            double vx = poly[c] - poly[a], vy = poly[c + 1] - poly[a + 1], vz = poly[c + 2] - poly[a + 2];
            double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
            return cx * cx + cy * cy + cz * cz < 1e-10;
        }
    }
}
