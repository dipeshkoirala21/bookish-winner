using System;
using System.Collections.Generic;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.World.Rendering;
using Ghumante.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.World.Buildings
{
    /// <summary>
    /// The Unity side of the B0 "kit-lite" band (W2_DESIGN 2.4; the detail pass band table): <see cref="B0CellSet"/>
    /// decides which cells draw which houses at which level, builds them on a worker in 4 ms slices, keeps them within
    /// <see cref="BandConfig.CellCacheBytes"/> and registers their colliders per cell; this class maps the streamer's
    /// detail tile views to its tiles and puts the cells on screen. Each upload chunk of a cell (at most
    /// <see cref="B0CellSet.ChunkVertices"/> vertices with 16-bit indices, copied straight from the worker's arrays
    /// through the advanced Mesh API) gets a pooled GameObject under its tile view with the streamer's B0 band material,
    /// whose band range dithers the 4 m cross-fade into B1. Uploads take what the streamer leaves of its per-frame upload
    /// cap (<see cref="Upload"/>). Main thread only, apart from the cell slices.
    /// </summary>
    internal sealed class DetailCells : IB0CellSink, IDisposable
    {
        private sealed class Chunk
        {
            public GameObject Go;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
        }

        // Streams as MeshUpload: 0 positions, 1 normals, 2 colours (RGBA8), 3 UV0 (material channel, baked AO).
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

        private readonly B0CellSet _set;
        private readonly Material _material;
        private readonly Dictionary<TileView, B0Tile> _tiles = new Dictionary<TileView, B0Tile>();
        private readonly Stack<Chunk> _pool = new Stack<Chunk>(64);
        private readonly GameObject _poolRoot;
        private bool _disposed;

        /// <summary>The band set, the B0 options and the ground of the streamer; <paramref name="material"/> is its B0 band
        /// material (owned by the streamer). <paramref name="parent"/> holds the pooled chunk objects.</summary>
        public DetailCells(BandConfig bands, BuildingOptions options, Material material, IStructureGround ground, Transform parent)
        {
            _material = material;
            _set = new B0CellSet(bands, options ?? new BuildingOptions { Band = BuildingBand.B0KitLite }, this, ground, new ThreadPoolJobRunner(),
                                 WorldStreamer.NowMs)
            {
                Warning = Debug.LogWarning,
            };
            _poolRoot = new GameObject("b0 pool");
            _poolRoot.SetActive(false);
            if (parent != null) _poolRoot.transform.SetParent(parent, false);
        }

        /// <summary>Triangles of the cells shown this frame.</summary>
        public int ShownTris
        {
            get { return _set.ShownTris; }
        }

        /// <summary>Cells shown this frame.</summary>
        public int ShownCount
        {
            get { return _set.ShownCount; }
        }

        /// <summary>Chunk renderers (draw calls) shown this frame.</summary>
        public int ShownChunks
        {
            get { return _set.ShownChunks; }
        }

        /// <summary>Cells alive (resident or building).</summary>
        public int Count
        {
            get { return _set.CellCount; }
        }

        /// <summary>GPU bytes of the resident cells (within <see cref="BandConfig.CellCacheBytes"/>).</summary>
        public long ResidentBytes
        {
            get { return _set.ResidentBytes; }
        }

        /// <summary>Per frame, with the visible detail views and the camera (game metres). <paramref name="nowMs"/> is not
        /// used: the cell set keeps its own clock.</summary>
        public void Update(List<TileView> views, double camX, double camZ, double nowMs)
        {
            if (_disposed) return;
            for (int v = 0; v < views.Count; v++)
            {
                TileView view = views[v];
                TileExtras e = view.Extras;
                if (e == null || e.Source == null || e.Sampler == null || !e.Sampler.HasHeights) continue;
                B0Tile t;
                if (_tiles.TryGetValue(view, out t) && t.Source == e.Source)
                {
                    if (!t.Visible) _set.SetVisible(t, true);
                    continue;
                }
                if (t != null) _set.Remove(t); // new extras for the view: start again
                _tiles[view] = _set.Add(view.Node.Area, e.Source, e.Sampler, view);
            }
            _set.Update(camX, camZ);
        }

        /// <summary>True when every B0 cell the camera wants in B1 block <paramref name="block"/> of the view draws all of
        /// its ring's houses, so the block may hand its inner range to B0 (decided by this frame's <see cref="Update"/>;
        /// the tile-local camera is not needed).</summary>
        public bool BlockReady(TileView view, int block, double cx, double cz)
        {
            B0Tile t;
            return _tiles.TryGetValue(view, out t) && _set.BlockReady(t, block);
        }

        /// <summary>GPU bytes of the next cell chunk waiting for upload (0 when none).</summary>
        public long NextUploadBytes
        {
            get { return _set.NextUploadBytes; }
        }

        /// <summary>Uploads finished cell chunks within <paramref name="budgetBytes"/> (one at least with
        /// <paramref name="atLeastOne"/>); returns the bytes uploaded.</summary>
        public long Upload(long budgetBytes, bool atLeastOne)
        {
            return _disposed ? 0 : _set.Upload(budgetBytes, atLeastOne);
        }

        /// <summary>The view was hidden: hide its cells (they stay cached) and drop their colliders.</summary>
        public void Hide(TileView view)
        {
            B0Tile t;
            if (_tiles.TryGetValue(view, out t)) _set.SetVisible(t, false);
        }

        /// <summary>The view is released: destroy its cells.</summary>
        public void Release(TileView view)
        {
            B0Tile t;
            if (!_tiles.TryGetValue(view, out t)) return;
            _set.Remove(t);
            _tiles.Remove(view);
        }

        // ---------------------------------------------------------------------------------------------------------
        // IB0CellSink

        object IB0CellSink.Upload(B0Tile tile, MeshData data, in UploadChunk chunk, ushort[] shortIndices)
        {
            var view = (TileView)tile.Owner;
            Chunk c = _pool.Count > 0 ? _pool.Pop() : NewChunk();
            c.Go.transform.SetParent(view.Transform, false);
            c.Filter.sharedMesh = CreateMesh(data, chunk, shortIndices, "b0");
            c.Renderer.sharedMaterial = _material;
            c.Renderer.enabled = false;
            c.Go.SetActive(true);
            return c;
        }

        void IB0CellSink.SetShown(object handle, bool shown)
        {
            var c = (Chunk)handle;
            if (c.Renderer.enabled != shown) c.Renderer.enabled = shown;
        }

        void IB0CellSink.Destroy(object handle)
        {
            var c = (Chunk)handle;
            Mesh mesh = c.Filter.sharedMesh;
            c.Filter.sharedMesh = null;
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
            c.Renderer.enabled = false;
            if (_disposed || _poolRoot == null)
            {
                UnityEngine.Object.Destroy(c.Go);
                return;
            }
            c.Go.SetActive(false);
            c.Go.transform.SetParent(_poolRoot.transform, false);
            _pool.Push(c);
        }

        private static Chunk NewChunk()
        {
            var go = new GameObject("b0 cell");
            var c = new Chunk { Go = go, Filter = go.AddComponent<MeshFilter>(), Renderer = go.AddComponent<MeshRenderer>() };
            MeshRenderer r = c.Renderer;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
            r.allowOcclusionWhenDynamic = false;
            r.enabled = false;
            return c;
        }

        /// <summary>One chunk of a cell to a GPU-only mesh straight from the worker's arrays (16-bit indices, which a B0
        /// chunk always fits).</summary>
        private static Mesh CreateMesh(MeshData m, in UploadChunk c, ushort[] shorts, string name)
        {
            int vc = c.VertexCount, ic = c.IndexCount, v0 = c.FirstVertex;
            var mesh = new Mesh { name = name };
            mesh.SetVertexBufferParams(vc, m.HasUv0 ? WithUv : Plain);
            mesh.SetVertexBufferData(m.Positions, v0 * 3, 0, vc * 3, 0, Flags);
            mesh.SetVertexBufferData(m.Normals, v0 * 3, 0, vc * 3, 1, Flags);
            mesh.SetVertexBufferData(m.Colors, v0 * 4, 0, vc * 4, 2, Flags);
            if (m.HasUv0) mesh.SetVertexBufferData(m.Uv0, v0 * 2, 0, vc * 2, 3, Flags);
            if (c.UsesShortIndices && shorts != null)
            {
                mesh.SetIndexBufferParams(ic, IndexFormat.UInt16);
                mesh.SetIndexBufferData(shorts, c.FirstIndex, 0, ic, Flags);
            }
            else
            {
                mesh.SetIndexBufferParams(ic, IndexFormat.UInt32);
                mesh.SetIndexBufferData(m.Indices, c.FirstIndex, 0, ic, Flags);
            }
            var bounds = new Bounds(new Vector3((c.MinX + c.MaxX) * 0.5f, (c.MinY + c.MaxY) * 0.5f, (c.MinZ + c.MaxZ) * 0.5f),
                                    new Vector3(c.MaxX - c.MinX, c.MaxY - c.MinY, c.MaxZ - c.MinZ));
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, ic, MeshTopology.Triangles) { firstVertex = 0, vertexCount = vc, bounds = bounds }, Flags);
            mesh.bounds = bounds;
            mesh.UploadMeshData(true); // GPU-only (ARCHITECTURE 10)
            return mesh;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _set.Dispose(); // every chunk back to the pool
            _disposed = true;
            _tiles.Clear();
            while (_pool.Count > 0)
            {
                Chunk c = _pool.Pop();
                if (c.Go != null) UnityEngine.Object.Destroy(c.Go);
            }
            if (_poolRoot != null) UnityEngine.Object.Destroy(_poolRoot);
        }
    }
}
