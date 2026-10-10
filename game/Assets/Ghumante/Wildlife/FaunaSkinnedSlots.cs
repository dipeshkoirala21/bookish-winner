using System;
using Ghumante.Core.Generators.Fauna;
using Ghumante.Core.Meshing;
using Ghumante.World.Instancing;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.Wildlife
{
    /// <summary>
    /// The near animals (LOD0 within 10 m, LOD1 within 30 m; at most 2 + 6 on High, W2_DESIGN 5.5) are skinned on the
    /// CPU every frame for smooth, continuous procedural animation: a small pool of dynamic meshes, each bound to one
    /// animal while it stays near (so colours, UV0 and indices are uploaded once), whose positions and normals are
    /// rewritten from <see cref="FaunaSkinner"/> each frame (two vertex streams, no allocation) and drawn as a single
    /// tinted instance. Main thread only.
    /// </summary>
    public sealed class FaunaSkinnedSlots : IDisposable
    {
        private sealed class Slot
        {
            public Mesh Mesh;
            public InstanceBatch Batch;
            public FaunaMesh Source;
            public int Owner = int.MinValue;
            public bool Used;
            public float[] Pos = new float[3];
            public float[] Nrm = new float[3];
            public int Vertices;
        }

        private static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3),
        };

        private const MeshUpdateFlags Flags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds |
                                              MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds;

        private readonly Slot[] _slots;
        private readonly Material _material;
        private readonly FaunaSkinner _skinner = new FaunaSkinner();
        private ushort[] _shorts = new ushort[4096];
        private Bounds _bounds;

        public FaunaSkinnedSlots(Material instancedTint, int capacity)
        {
            _material = instancedTint;
            _slots = new Slot[Math.Max(1, capacity)];
            for (int i = 0; i < _slots.Length; i++) _slots[i] = new Slot();
        }

        public int Tris { get; private set; }

        public int Draws { get; private set; }

        /// <summary>Skinning work of the last frame (vertices), for the debug counters.</summary>
        public int SkinnedVertices { get; private set; }

        public void Begin(Vector3 cameraScene)
        {
            _bounds = new Bounds(cameraScene, new Vector3(400f, 300f, 400f));
            for (int i = 0; i < _slots.Length; i++) _slots[i].Used = false;
            Tris = Draws = SkinnedVertices = 0;
        }

        /// <summary>
        /// Draws <paramref name="source"/> posed by <paramref name="pose"/> at <paramref name="model"/> with the coat
        /// <paramref name="tint"/> for animal <paramref name="owner"/>. False when every slot is taken this frame (the
        /// caller then falls back to a keyframe).
        /// </summary>
        public bool Draw(int owner, FaunaMesh source, FaunaPose pose, in Matrix4x4 model, Vector4 tint, bool shadows)
        {
            Slot s = Find(owner, source);
            if (s == null) return false;
            s.Used = true;
            if (s.Source != source || s.Mesh == null) Bind(s, source);
            s.Owner = owner;
            _skinner.Solve(source, pose);
            _skinner.Skin(source, s.Pos, s.Nrm);
            int vc = s.Vertices;
            s.Mesh.SetVertexBufferData(s.Pos, 0, 0, vc * 3, 0, Flags);
            s.Mesh.SetVertexBufferData(s.Nrm, 0, 0, vc * 3, 1, Flags);
            s.Batch.Shadows = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            s.Batch.ResetCounters();
            s.Batch.WorldBounds = _bounds;
            s.Batch.Add(model, tint);
            s.Batch.Flush();
            Tris += s.Batch.Tris;
            Draws += s.Batch.Draws;
            SkinnedVertices += vc;
            return true;
        }

        private Slot Find(int owner, FaunaMesh source)
        {
            Slot free = null;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (s.Used) continue;
                if (s.Owner == owner && s.Source == source) return s;
                if (free == null || free.Owner != int.MinValue && s.Owner == int.MinValue) free = s;
            }
            return free;
        }

        /// <summary>Uploads a new source's static streams (colours, UV0) and indices into the slot's mesh.</summary>
        private void Bind(Slot s, FaunaMesh source)
        {
            MeshData m = source.Mesh;
            int vc = m.VertexCount, ic = m.IndexCount;
            if (s.Mesh == null)
            {
                s.Mesh = new Mesh { name = "fauna_skinned" };
                s.Mesh.MarkDynamic();
                s.Batch = new InstanceBatch(s.Mesh, _material, 0, true);
            }
            if (s.Pos.Length < vc * 3)
            {
                s.Pos = new float[vc * 3];
                s.Nrm = new float[vc * 3];
            }
            s.Vertices = vc;
            s.Source = source;
            Mesh mesh = s.Mesh;
            mesh.Clear();
            mesh.SetVertexBufferParams(vc, Layout);
            mesh.SetVertexBufferData(m.Positions, 0, 0, vc * 3, 0, Flags);
            mesh.SetVertexBufferData(m.Normals, 0, 0, vc * 3, 1, Flags);
            mesh.SetVertexBufferData(m.Colors, 0, 0, vc * 4, 2, Flags);
            mesh.SetVertexBufferData(m.Uv0, 0, 0, vc * 2, 3, Flags);
            if (_shorts.Length < ic) _shorts = new ushort[Math.Max(ic, _shorts.Length * 2)];
            for (int i = 0; i < ic; i++) _shorts[i] = (ushort)m.Indices[i];
            mesh.SetIndexBufferParams(ic, IndexFormat.UInt16);
            mesh.SetIndexBufferData(_shorts, 0, 0, ic, Flags);
            float r = Math.Max(0.5f, source.BoundsRadiusM);
            var bounds = new Bounds(new Vector3(0f, 0.5f * r, 0f), new Vector3(2f * r, 2f * r, 2f * r));
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, ic, MeshTopology.Triangles) { firstVertex = 0, vertexCount = vc, bounds = bounds }, Flags);
            mesh.bounds = bounds;
            s.Batch.TrisPerInstance = ic / 3;
        }

        public void Dispose()
        {
            foreach (Slot s in _slots)
            {
                if (s.Mesh != null) UnityEngine.Object.Destroy(s.Mesh);
                s.Mesh = null;
                s.Batch = null;
                s.Source = null;
            }
        }
    }
}
