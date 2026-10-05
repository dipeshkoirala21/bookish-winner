using System;
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
    /// (<see cref="Execute"/>, Core types only, no Unity API), and its result, which the main thread uploads layer by
    /// layer. Instances and their <see cref="MeshData"/> buffers are pooled by <see cref="StreamingScheduler"/>.
    /// <para>
    /// Per node: the terrain of the area from its source (decimation step from <see cref="StreamingConfig.TerrainStep"/>,
    /// skirts), the matching <see cref="TileHeightSampler"/> for the ground query, and, for exact nodes whose source has
    /// detail chunks, roads, buildings and areas draped on that same sampler plus the road index. Positions are metres
    /// from the area's south-west corner with absolute heights.
    /// </para>
    /// </summary>
    public sealed class TileBuild
    {
        /// <summary>Above this many vertices a pooled layer buffer is dropped instead of kept (memory cap).</summary>
        public const int PoolKeepVertices = 131072;

        public SelectedNode Node;

        /// <summary>The decoded source tile: given by the dispatcher when cached, else decoded by the job.</summary>
        public TileData Source;

        /// <summary>True when <see cref="Execute"/> decoded <see cref="Source"/> (the caller caches it).</summary>
        public bool DecodedSource;

        /// <summary>Terrain decimation step used for the meshes and the sampler.</summary>
        public int Step;

        public readonly MeshData[] Layers = new MeshData[TileLayers.Count];

        /// <summary>16-bit copies of the indices of layers with at most 65 535 vertices (see <see cref="UsesShortIndices"/>).</summary>
        public readonly ushort[][] ShortIndices = new ushort[TileLayers.Count][];

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
        internal int NextLayer;
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

        /// <summary>True when the layer fits 16-bit indices (<see cref="ShortIndices"/> holds them).</summary>
        public bool UsesShortIndices(int layer)
        {
            return Layers[layer].VertexCount <= 65535;
        }

        /// <summary>Approximate GPU bytes of a layer: vertex streams plus indices.</summary>
        public long LayerBytes(int layer)
        {
            MeshData m = Layers[layer];
            long v = (long)m.VertexCount * (12 + 12 + 4 + (m.HasUv0 ? 8 : 0));
            return v + (long)m.IndexCount * (UsesShortIndices(layer) ? 2 : 4);
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
            NextLayer = 0;
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
            for (int i = 0; i < TileLayers.Count; i++)
            {
                if (Layers[i].VertexCapacity > PoolKeepVertices) Layers[i] = new MeshData();
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

            var terrain = new TerrainOptions { Step = step, SkirtDepthM = settings.SkirtDepthM, Season = settings.Season };
            TerrainMesher.Build(src, area, terrain, b.Layers[TileLayers.Terrain]);
            b.Sampler = TileHeightSampler.ForArea(src, area, step);

            if (n.IsExact && src.HasDetail && b.Sampler.HasHeights)
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
                if (m.IndexCount > 0 && m.VertexCount <= 65535)
                {
                    ushort[] s = b.ShortIndices[i];
                    if (s == null || s.Length < m.IndexCount) b.ShortIndices[i] = s = new ushort[Math.Max(m.IndexCount, 3072)];
                    int[] idx = m.Indices;
                    for (int k = 0; k < m.IndexCount; k++) s[k] = (ushort)idx[k];
                }
            }

            // Vertices drop with the square of their distance to the camera; the camera can be anywhere inside this
            // area's ring, so allow for the ring radius plus the area's diagonal.
            double reach = config.RingFor(area.Level).RadiusM + area.Size * 1.5;
            b.CurvatureMarginM = (float)EarthCurvature.DropM(reach);
            b.WorkMs = sw.Elapsed.TotalMilliseconds;
        }
    }
}
