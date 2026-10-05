using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Streaming;
using Ghumante.World.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Ghumante.World.Streaming
{
    /// <summary>
    /// The Unity side of world streaming (ARCHITECTURE.md 7.2): owns a <see cref="StreamingScheduler"/> (selection,
    /// worker builds, budgets, swap rule, ground query) and implements its <see cref="ITileSink"/> with pooled
    /// <see cref="TileView"/> GameObjects. Uploads go chunk by chunk (<see cref="UploadChunk"/>, at most
    /// <see cref="TileBuild.UploadChunkVertices"/> vertices, so a dense city layer spreads over several frames) through
    /// the advanced Mesh API straight from the builds' arrays (no per-frame garbage; 16-bit indices) and the CPU copies
    /// are released after upload. Tile views sit at <c>area south-west corner - floating origin</c>; meshes are never
    /// rebuilt when the origin moves. Owned by <see cref="WorldRoot"/>; main thread only.
    /// </summary>
    public sealed class WorldStreamer : ITileSink, IDisposable
    {
        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        private readonly StreamingScheduler _scheduler;
        private readonly Transform _root;
        private readonly WorldMaterialSet _materials;
        private readonly Dictionary<SelectedNode, TileView> _views = new Dictionary<SelectedNode, TileView>(512);
        private readonly Stack<TileView> _pool = new Stack<TileView>(64);
        private WorldPos _origin;
        private bool _disposed;

        /// <summary>Starts streaming <paramref name="pack"/> (the streamer owns it from now on and closes it after the
        /// last running build when disposed).</summary>
        public WorldStreamer(Transform parent, WorldMaterialSet materials, PackReader pack, StreamingConfig config, TileSelector selector,
                             TileGroundQuery ground, MeshingSettings meshing)
        {
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            _materials = materials;
            var go = new GameObject("Tiles");
            _root = go.transform;
            if (parent != null) _root.SetParent(parent, false);
            _scheduler = new StreamingScheduler(pack, config, selector, ground, this, new ThreadPoolJobRunner(), NowMs, meshing, true)
            {
                Warning = Debug.LogWarning,
            };
        }

        public StreamingScheduler Scheduler
        {
            get { return _scheduler; }
        }

        /// <summary>Tile views alive (visible, hidden or uploading).</summary>
        public int ViewCount
        {
            get { return _views.Count; }
        }

        /// <summary>One frame: stream around <paramref name="focus"/>. The upload budget follows the frame rate target
        /// (1.5 ms at 60 fps, 2 ms at 30 fps).</summary>
        public void Tick(WorldPos focus)
        {
            if (_disposed) return;
            _scheduler.UploadBudgetMs = Application.targetFrameRate >= 50 ? 1.5 : 2.0;
            _scheduler.Tick(focus.X, focus.Z);
        }

        /// <summary>Re-place every view for a new floating origin (exact: computed from the doubles each time).</summary>
        public void Rebase(WorldPos origin)
        {
            _origin = origin;
            foreach (KeyValuePair<SelectedNode, TileView> kv in _views) kv.Value.Place(_origin);
        }

        // ---------------------------------------------------------------------------------------------------------
        // ITileSink

        public void Upload(TileBuild build, int chunk)
        {
            TileView view;
            if (!_views.TryGetValue(build.Node, out view))
            {
                view = _pool.Count > 0 ? _pool.Pop() : new TileView(_root, _materials);
                view.Bind(build.Node, _origin);
                _views.Add(build.Node, view);
            }
            view.AddChunk(build.Chunks[chunk].Layer, MeshUpload.Create(build, chunk));
        }

        public void SetVisible(SelectedNode node, bool visible)
        {
            TileView view;
            if (_views.TryGetValue(node, out view)) view.SetVisible(visible);
        }

        public void Release(SelectedNode node)
        {
            TileView view;
            if (!_views.TryGetValue(node, out view)) return;
            _views.Remove(node);
            view.Clear();
            _pool.Push(view);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scheduler.Dispose(); // releases every view through Release
            foreach (KeyValuePair<SelectedNode, TileView> kv in _views) kv.Value.Clear();
            _views.Clear();
            _pool.Clear();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        }

        private static double NowMs()
        {
            return Stopwatch.GetTimestamp() * TicksToMs;
        }
    }

    /// <summary>
    /// A pooled GameObject drawing one selected node: a child per layer (terrain, roads, buildings, areas) holding one
    /// renderer per upload chunk (<see cref="UploadChunk"/>), with the layer's material. Terrain and buildings cast
    /// shadows; road and area overlays do not. Light probes are off, so the renderers get the scene's ambient probe (the
    /// SH the toon shader samples). The chunk GameObjects stay with the view when it goes back to the pool; meshes are
    /// GPU-only (not readable, so they cannot be refilled) and are destroyed with <see cref="Clear"/>.
    /// </summary>
    public sealed class TileView
    {
        private readonly GameObject _root;
        private readonly Transform _transform;
        private readonly WorldMaterialSet _materials;
        private readonly GameObject[] _layers = new GameObject[TileLayers.Count];
        private readonly List<MeshFilter>[] _parts = new List<MeshFilter>[TileLayers.Count];
        private readonly int[] _used = new int[TileLayers.Count];
        private SelectedNode _node;

        public TileView(Transform parent, WorldMaterialSet materials)
        {
            _materials = materials;
            _root = new GameObject("tile");
            _root.SetActive(false);
            _transform = _root.transform;
            _transform.SetParent(parent, false);
            for (int i = 0; i < TileLayers.Count; i++)
            {
                var go = new GameObject(TileLayers.Name(i));
                go.transform.SetParent(_transform, false);
                go.SetActive(false);
                _layers[i] = go;
                _parts[i] = new List<MeshFilter>(1);
            }
        }

        public SelectedNode Node
        {
            get { return _node; }
        }

        internal void Bind(SelectedNode node, WorldPos origin)
        {
            _node = node;
#if UNITY_EDITOR
            _root.name = node.ToString();
#endif
            Place(origin);
        }

        /// <summary>Put the view at its area's south-west corner relative to the floating origin.</summary>
        public void Place(WorldPos origin)
        {
            _transform.localPosition = new Vector3((float)(_node.Area.X0 - origin.X), 0f, (float)(_node.Area.Z0 - origin.Z));
        }

        /// <summary>Show one more chunk mesh of <paramref name="layer"/> (the view takes ownership of the mesh).</summary>
        internal void AddChunk(int layer, Mesh mesh)
        {
            List<MeshFilter> parts = _parts[layer];
            MeshFilter filter;
            if (_used[layer] < parts.Count)
            {
                filter = parts[_used[layer]];
            }
            else
            {
                var go = new GameObject(TileLayers.Name(layer));
                go.transform.SetParent(_layers[layer].transform, false);
                filter = go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _materials.ForLayer(layer);
                bool overlay = layer == TileLayers.Roads || layer == TileLayers.Areas;
                r.shadowCastingMode = overlay ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                r.allowOcclusionWhenDynamic = false;
                parts.Add(filter);
            }
            _used[layer]++;
            filter.sharedMesh = mesh;
            filter.gameObject.SetActive(true);
            _layers[layer].SetActive(true);
        }

        internal void SetVisible(bool visible)
        {
            _root.SetActive(visible);
        }

        /// <summary>Destroy the meshes and hide (back to the pool; the chunk GameObjects are kept for reuse).</summary>
        internal void Clear()
        {
            for (int i = 0; i < TileLayers.Count; i++)
            {
                List<MeshFilter> parts = _parts[i];
                for (int k = 0; k < _used[i]; k++)
                {
                    MeshFilter f = parts[k];
                    if (f == null) continue;
                    Mesh mesh = f.sharedMesh;
                    f.sharedMesh = null;
                    if (mesh != null) UnityEngine.Object.Destroy(mesh);
                    f.gameObject.SetActive(false);
                }
                _used[i] = 0;
                if (_layers[i] != null) _layers[i].SetActive(false);
            }
            if (_root != null) _root.SetActive(false);
        }
    }

    /// <summary>One <see cref="UploadChunk"/> to a Unity Mesh through the advanced Mesh API (multi-stream, straight from
    /// the build's arrays, no intermediate copies).</summary>
    internal static class MeshUpload
    {
        // Stream 0 positions, 1 normals, 2 colours (RGBA8, sRGB values: the shader linearises them), 3 UV0 (roads).
        private static readonly VertexAttributeDescriptor[] Plain =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
        };

        private static readonly VertexAttributeDescriptor[] WithUv =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3),
        };

        private const MeshUpdateFlags Flags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds |
                                              MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds;

        public static Mesh Create(TileBuild b, int chunk)
        {
            UploadChunk c = b.Chunks[chunk];
            Core.Meshing.MeshData m = b.Layers[c.Layer];
            int vc = c.VertexCount, ic = c.IndexCount, v0 = c.FirstVertex;
            var mesh = new Mesh();
            mesh.name = TileLayers.Name(c.Layer);
            mesh.SetVertexBufferParams(vc, m.HasUv0 ? WithUv : Plain);
            mesh.SetVertexBufferData(m.Positions, v0 * 3, 0, vc * 3, 0, Flags);
            mesh.SetVertexBufferData(m.Normals, v0 * 3, 0, vc * 3, 1, Flags);
            mesh.SetVertexBufferData(m.Colors, v0 * 4, 0, vc * 4, 2, Flags);
            if (m.HasUv0) mesh.SetVertexBufferData(m.Uv0, v0 * 2, 0, vc * 2, 3, Flags);
            bool shortIndices = c.UsesShortIndices;
            mesh.SetIndexBufferParams(ic, shortIndices ? IndexFormat.UInt16 : IndexFormat.UInt32);
            if (shortIndices) mesh.SetIndexBufferData(b.ShortIndices[c.Layer], c.FirstIndex, 0, ic, Flags);
            else mesh.SetIndexBufferData(m.Indices, c.FirstIndex, 0, ic, Flags);

            // Earth curvature lowers far vertices in the shader; keep them inside the culling bounds.
            float y0 = c.MinY - b.CurvatureMarginM;
            var bounds = new Bounds(new Vector3((c.MinX + c.MaxX) * 0.5f, (y0 + c.MaxY) * 0.5f, (c.MinZ + c.MaxZ) * 0.5f),
                                    new Vector3(c.MaxX - c.MinX, c.MaxY - y0, c.MaxZ - c.MinZ));
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, ic, MeshTopology.Triangles)
            {
                firstVertex = 0,
                vertexCount = vc,
                bounds = bounds,
            }, Flags);
            mesh.bounds = bounds;
            mesh.UploadMeshData(true); // frees the CPU copy (ARCHITECTURE 10: meshes are GPU-only)
            return mesh;
        }
    }
}
