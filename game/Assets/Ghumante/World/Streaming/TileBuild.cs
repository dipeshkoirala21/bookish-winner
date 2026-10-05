using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Rendering;

namespace Ghumante.World.Streaming
{
    /// <summary>The mesh layers of a tile view, in upload order.</summary>
    public static class TileLayers
    {
        public const int Terrain = 0;
        public const int Roads = 1;
        public const int Buildings = 2;
        public const int Areas = 3;
        public const int Count = 4;

        private static readonly string[] Names = { "terrain", "roads", "buildings", "areas" };

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

        public BuildingOptions Buildings = new BuildingOptions();
        public AreaOptions Areas = new AreaOptions();
        public bool DrawRoads = true;
        public bool DrawBuildings = true;
        public bool DrawAreas = true;
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

        /// <summary>Worker milliseconds spent on this build.</summary>
        public double WorkMs;

        internal StreamingScheduler Owner;
        internal long Generation;
        internal int NextChunk;
        internal readonly Action Work;

        public TileBuild()
        {
            for (int i = 0; i < TileLayers.Count; i++) Layers[i] = new MeshData();
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
            for (int i = 0; i < TileLayers.Count; i++) Layers[i].Clear();
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
            for (int i = 0; i < TileLayers.Count; i++)
            {
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
                if (settings.DrawBuildings) BuildingMesher.Build(src, b.Sampler, settings.Buildings, b.Layers[TileLayers.Buildings]);
                if (settings.DrawAreas) AreaMesher.Build(src, b.Sampler, settings.Areas, b.Layers[TileLayers.Areas]);
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
                SplitLayer(m, i, UploadChunkVertices, b.Chunks, ref b.ShortIndices[i]);
            }

            // Vertices drop with the square of their distance to the camera; the camera can be anywhere inside this
            // area's ring, so allow for the ring radius plus the area's diagonal.
            double reach = config.RingFor(area.Level).RadiusM + area.Size * 1.5;
            b.CurvatureMarginM = (float)EarthCurvature.DropM(reach);
            b.WorkMs = sw.Elapsed.TotalMilliseconds;
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
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));
            int n = m.IndexCount - m.IndexCount % 3;
            if (n <= 0) return;
            if (maxVertices < 3) maxVertices = 3;
            int[] idx = m.Indices;
            int firstChunk = chunks.Count;
            int first = 0, vmin = int.MaxValue, vmax = -1;
            for (int t = 0; t < n; t += 3)
            {
                int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                int tmin = Math.Min(a, Math.Min(b, c)), tmax = Math.Max(a, Math.Max(b, c));
                int nmin = Math.Min(vmin, tmin), nmax = Math.Max(vmax, tmax);
                if (t > first && nmax - nmin >= maxVertices)
                {
                    chunks.Add(MakeChunk(m, layer, first, t - first, vmin, vmax));
                    first = t;
                    nmin = tmin;
                    nmax = tmax;
                }
                vmin = nmin;
                vmax = nmax;
            }
            chunks.Add(MakeChunk(m, layer, first, n - first, vmin, vmax));

            if (shortIndices == null || shortIndices.Length < n)
                shortIndices = new ushort[Math.Max(n, shortIndices == null ? 3072 : shortIndices.Length * 2)];
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

        private static UploadChunk MakeChunk(MeshData m, int layer, int firstIndex, int indexCount, int vmin, int vmax)
        {
            var c = new UploadChunk
            {
                Layer = layer,
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
