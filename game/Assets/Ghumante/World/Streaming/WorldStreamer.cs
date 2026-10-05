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
    /// <see cref="TileView"/> GameObjects. Uploads go through the advanced Mesh API straight from the builds' arrays
    /// (no per-frame garbage; 16-bit indices unless a layer has more than 65 535 vertices) and the CPU copies are
    /// released after upload. Tile views sit at <c>area south-west corner - floating origin</c>; meshes are never
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

        public void Upload(TileBuild build, int layer)
        {
            TileView view;
            if (!_views.TryGetValue(build.Node, out view))
            {
                view = _pool.Count > 0 ? _pool.Pop() : new TileView(_root, _materials);
                view.Bind(build.Node, _origin);
                _views.Add(build.Node, view);
            }
            view.SetLayer(layer, MeshUpload.Create(build, layer));
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
    /// A pooled GameObject drawing one selected node: a child per layer (terrain, roads, buildings, areas) with its own
    /// renderer and material. Terrain and buildings cast shadows; road and area overlays do not. Light probes are off,
    /// so the renderers get the scene's ambient probe (the SH the toon shader samples).
    /// </summary>
    public sealed class TileView
    {
        private readonly GameObject _root;
        private readonly Transform _transform;
        private readonly GameObject[] _layers = new GameObject[TileLayers.Count];
        private readonly MeshFilter[] _filters = new MeshFilter[TileLayers.Count];
        private readonly Mesh[] _meshes = new Mesh[TileLayers.Count];
        private SelectedNode _node;

        public TileView(Transform parent, WorldMaterialSet materials)
        {
            _root = new GameObject("tile");
            _root.SetActive(false);
            _transform = _root.transform;
            _transform.SetParent(parent, false);
            for (int i = 0; i < TileLayers.Count; i++)
            {
                var go = new GameObject(TileLayers.Name(i));
                go.transform.SetParent(_transform, false);
                _filters[i] = go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = materials.ForLayer(i);
                bool overlay = i == TileLayers.Roads || i == TileLayers.Areas;
                r.shadowCastingMode = overlay ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                r.allowOcclusionWhenDynamic = false;
                go.SetActive(false);
                _layers[i] = go;
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

        internal void SetLayer(int layer, Mesh mesh)
        {
            if (_meshes[layer] != null) UnityEngine.Object.Destroy(_meshes[layer]);
            _meshes[layer] = mesh;
            _filters[layer].sharedMesh = mesh;
            _layers[layer].SetActive(mesh != null);
        }

        internal void SetVisible(bool visible)
        {
            _root.SetActive(visible);
        }

        /// <summary>Destroy the meshes and hide (back to the pool).</summary>
        internal void Clear()
        {
            for (int i = 0; i < TileLayers.Count; i++)
            {
                if (_meshes[i] != null) UnityEngine.Object.Destroy(_meshes[i]);
                _meshes[i] = null;
                if (_filters[i] != null) _filters[i].sharedMesh = null;
                if (_layers[i] != null) _layers[i].SetActive(false);
            }
            if (_root != null) _root.SetActive(false);
        }
    }

    /// <summary>MeshData to Unity Mesh through the advanced Mesh API (multi-stream, no intermediate arrays).</summary>
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

        public static Mesh Create(TileBuild b, int layer)
        {
            Core.Meshing.MeshData m = b.Layers[layer];
            int vc = m.VertexCount, ic = m.IndexCount;
            var mesh = new Mesh();
            mesh.name = TileLayers.Name(layer);
            mesh.SetVertexBufferParams(vc, m.HasUv0 ? WithUv : Plain);
            mesh.SetVertexBufferData(m.Positions, 0, 0, vc * 3, 0, Flags);
            mesh.SetVertexBufferData(m.Normals, 0, 0, vc * 3, 1, Flags);
            mesh.SetVertexBufferData(m.Colors, 0, 0, vc * 4, 2, Flags);
            if (m.HasUv0) mesh.SetVertexBufferData(m.Uv0, 0, 0, vc * 2, 3, Flags);
            bool shortIndices = b.UsesShortIndices(layer);
            mesh.SetIndexBufferParams(ic, shortIndices ? IndexFormat.UInt16 : IndexFormat.UInt32);
            if (shortIndices) mesh.SetIndexBufferData(b.ShortIndices[layer], 0, 0, ic, Flags);
            else mesh.SetIndexBufferData(m.Indices, 0, 0, ic, Flags);

            float x0, y0, z0, x1, y1, z1;
            b.GetBounds(layer, out x0, out y0, out z0, out x1, out y1, out z1);
            // Earth curvature lowers far vertices in the shader; keep them inside the culling bounds.
            y0 -= b.CurvatureMarginM;
            var bounds = new Bounds(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                                    new Vector3(x1 - x0, y1 - y0, z1 - z0));
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
