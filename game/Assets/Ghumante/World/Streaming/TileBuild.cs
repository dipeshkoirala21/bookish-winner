using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing;
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
    /// band and hero layers are split into parts (<see cref="UploadChunk.Part"/>): B1 and B2 per 128 m block, B3 per
    /// 256 m block, heroes per (hero, LOD) as <c>hero × 4 + lod</c>.
    /// </summary>
    public static class TileLayers
    {
        public const int Terrain = 0;
        public const int Roads = 1;

        /// <summary>B1 styled extrusions, one part per 128 m block.</summary>
        public const int Buildings = 2;

        public const int Areas = 3;

        /// <summary>Road markings (decals lifted over the ribbons).</summary>
        public const int RoadDecals = 4;

        /// <summary>B2 prisms, one part per 128 m block.</summary>
        public const int BuildingsFar = 5;

        /// <summary>B3 city blocks, one part per 256 m block.</summary>
        public const int BuildingsBlock = 6;

        /// <summary>Hero replicas, part = hero × 4 + LOD.</summary>
        public const int Heroes = 7;

        public const int Count = 8;

        private static readonly string[] Names = { "terrain", "roads", "buildings", "areas", "decals", "buildings_b2", "buildings_b3", "heroes" };

        /// <summary>Lowercase layer name (no allocation).</summary>
        public static string Name(int layer)
        {
            return layer >= 0 && layer < Count ? Names[layer] : "layer";
        }
    }

    /// <summary>
    /// One upload unit of a <see cref="TileBuild"/>: a run of one layer's triangles and the vertex range they use, at
    /// most <see cref="TileBuild.UploadChunkVertices"/> vertices, so the main thread never copies a whole dense layer
    /// (a city buildings layer reaches 290 000 vertices) in one frame. Each chunk becomes its own mesh. Its indices
    /// (<see cref="MeshData.Indices"/>, and <see cref="TileBuild.ShortIndices"/> when <see cref="UsesShortIndices"/>)
    /// are relative to <see cref="FirstVertex"/>.
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
    /// Options every tile build shares. Treat as read-only once streaming started: worker threads read it.
    /// </summary>
    public sealed class MeshingSettings
    {
        /// <summary>Palette season of the terrain colours.</summary>
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

        /// <summary>Road markings into <see cref="TileLayers.RoadDecals"/> (W2_DESIGN 4.5).</summary>
        public bool DrawRoadDecals = true;

        /// <summary>W2 building bands: B1 split per 128 m block plus the B2 and B3 layers. Off: the W1 single B1 layer.</summary>
        public bool BuildingBands = true;

        /// <summary>Hero replicas and hide zones (null: none).</summary>
        public HeroSet Heroes;

        /// <summary>Trees, street props and parked vehicles (<see cref="TileBuild.Instances"/>).</summary>
        public bool DrawInstances = true;

        public TreePlacementOptions Trees = new TreePlacementOptions();

        /// <summary>Hide the hero zones' buildings in every band (W2_DESIGN 3.3 D5).</summary>
        public void SetHiddenRefs(ISet<ulong> refs)
        {
            Buildings.HiddenRefs = refs;
            FarBuildings.HiddenRefs = refs;
            BlockBuildings.HiddenRefs = refs;
            DetailBuildings.HiddenRefs = refs;
        }
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
    /// instanced dressing, the heroes and their structure colliders (tile-local). Owned by the view.</summary>
    public sealed class TileExtras
    {
        public TileData Source;
        public TileHeightSampler Sampler;
        public TileInstances Instances;
        public HeroPiece[] Heroes = new HeroPiece[0];
        public StructureColliders HeroColliders;
    }

    /// <summary>
    /// One tile build: the decode and meshing work for a <see cref="SelectedNode"/> that runs on a worker thread
    /// (<see cref="Execute"/>, Core types only, no Unity API), and its result, which the main thread uploads chunk by
    /// chunk (<see cref="Chunks"/>). Instances and their <see cref="MeshData"/> buffers are pooled by <see cref="StreamingScheduler"/>.
    /// <para>
    /// Per node: the terrain of the area from its source (decimation step from <see cref="StreamingConfig.TerrainStep"/>,
    /// skirts), the matching <see cref="TileHeightSampler"/> for the ground query, and, for exact nodes whose source has
    /// detail chunks, roads, buildings and areas draped on that same sampler plus the road index. Positions are metres
    /// from the area's south-west corner with absolute heights.
    /// </para>
    /// <para>
    /// Every layer is then split into <see cref="Chunks"/> of at most <see cref="UploadChunkVertices"/> vertices, the
    /// unit the scheduler uploads (one or more per frame within its time and byte budgets).
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

        /// <summary>Most vertices one <see cref="UploadChunk"/> spans (about 1 MB of vertex data, 16-bit indices).</summary>
        public const int UploadChunkVertices = 32768;

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

        private readonly MeshData _scratch = new MeshData();
        private readonly MeshParts.Scratch _partScratch = new MeshParts.Scratch();
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
            };
            Instances = null;
            HeroColliders = null;
            Heroes.Clear();
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
        /// index. Thread-safe for distinct builds (the meshers keep per-thread scratch); touches no Unity API.
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
            TileData src = b.Source;
            TileId area = n.Area;
            int step = config.TerrainStep(area, n.Source, src.HeightsN);
            b.Step = step;
            for (int i = 0; i < TileLayers.Count; i++) b.Layers[i].Clear();
            b.Chunks.Clear();

            var terrain = new TerrainOptions { Step = step, SkirtDepthM = settings.SkirtDepthM, Season = settings.Season };
            TerrainMesher.Build(src, area, terrain, b.Layers[TileLayers.Terrain]);
            b.Sampler = TileHeightSampler.ForArea(src, area, step);

            if (n.DrawsDetail && src.HasDetail && b.Sampler.HasHeights)
            {
                if (settings.DrawRoads) RoadMesher.Build(src, b.Sampler, settings.Roads, b.Layers[TileLayers.Roads]);
                if (settings.DrawRoads && settings.DrawRoadDecals) MarkingMesher.Build(src, b.Sampler, settings.Roads, b.Layers[TileLayers.RoadDecals]);
                if (settings.DrawBuildings) BuildBuildings(b, src, settings);
                if (settings.DrawAreas) AreaMesher.Build(src, b.Sampler, settings.Areas, b.Layers[TileLayers.Areas]);
                if (settings.Heroes != null) BuildHeroes(b, src, settings.Heroes);
                if (settings.DrawInstances) BuildInstances(b, src, settings);
                b.Roads = ground != null ? ground.BuildRoadIndex(src) : null;
            }

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
                if (b.Parts[i].Count > 0) SplitLayer(m, i, b.Parts[i], UploadChunkVertices, b.Chunks, ref b.ShortIndices[i]);
                else SplitLayer(m, i, UploadChunkVertices, b.Chunks, ref b.ShortIndices[i]);
            }

            // Vertices drop with the square of their distance to the camera; the camera can be anywhere inside this
            // area's ring, so allow for the ring radius plus the area's diagonal.
            double reach = config.RingFor(area.Level).RadiusM + area.Size * 1.5;
            b.CurvatureMarginM = (float)EarthCurvature.DropM(reach);
            b.WorkMs = sw.Elapsed.TotalMilliseconds;
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

        /// <summary>Every hero anchored in this tile at LOD 0 to 3 (parts hero × 4 + lod) and its LOD0 colliders.</summary>
        private static void BuildHeroes(TileBuild b, TileData src, HeroSet heroes)
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
                    for (int lod = 0; lod < HeroLodBudget.LodCount; lod++)
                    {
                        b._scratch.Clear();
                        int tris = HeroBuilder.Build(r, src, lod, b._scratch, lod == 0 ? b._gen : null, lod == 0 ? stats : null);
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
                    if (b._gen.Boxes.Count > 0 || b._gen.Ramps.Count > 0)
                    {
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

        /// <summary>Trees (OSM, avenues, forest clumps), street props and parked vehicles, indexed by 64 m cell.</summary>
        private static void BuildInstances(TileBuild b, TileData src, MeshingSettings settings)
        {
            var inst = new TileInstances();
            TreePlacement.Place(src, b.Sampler, settings.Trees, inst.Trees);
            PropPlacement.Place(src, b.Sampler, inst.Props);
            ParkedPlacement.Place(src, b.Sampler, inst.Parked);
            inst.Index(src.Tile.Size);
            b.Instances = inst;
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
            if (m == null) throw new ArgumentNullException(nameof(m));
            int n = m.IndexCount - m.IndexCount % 3;
            if (n <= 0) return;
            SplitLayer(m, layer, new[] { new PartRange(0, 0, n) }, maxVertices, chunks, ref shortIndices);
        }

        /// <summary>
        /// <see cref="SplitLayer(MeshData, int, int, List{UploadChunk}, ref ushort[])"/> over the layer's parts: a chunk
        /// never spans two parts, and each chunk carries its part.
        /// </summary>
        public static void SplitLayer(MeshData m, int layer, IReadOnlyList<PartRange> parts, int maxVertices, List<UploadChunk> chunks,
                                      ref ushort[] shortIndices)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));
            int total = m.IndexCount - m.IndexCount % 3;
            if (total <= 0) return;
            if (maxVertices < 3) maxVertices = 3;
            int[] idx = m.Indices;
            int firstChunk = chunks.Count;
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
                    if (t > first && nmax - nmin >= maxVertices)
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
}
