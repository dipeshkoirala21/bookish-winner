using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// A fauna mesh in its bind pose plus what the skinner and the coat painter need: two bones per vertex
    /// (<see cref="Bone0"/> with <see cref="Weight0"/>, <see cref="Bone1"/> with 1 − Weight0), the body part of each
    /// vertex, and the bone pivots of this species (<see cref="Pivot"/>, model space). <see cref="Mesh"/> carries the
    /// tint colours (alpha 255 = coat, tinted per instance; 0 = fixed colour) and UV0 = (material channel, baked AO)
    /// (W2_DETAIL_CONTRACT §5). Grow-only; <see cref="Clear"/> keeps the buffers.
    /// </summary>
    public sealed class FaunaMesh
    {
        /// <summary>Geometry in the bind pose (model space: +Z forward, +Y up, root on the ground).</summary>
        public readonly MeshData Mesh;

        public byte[] Bone0;
        public byte[] Bone1;
        public float[] Weight0;
        public byte[] Part;

        /// <summary>Bind position of each bone's pivot (<see cref="FaunaRig.BoneCount"/> entries).</summary>
        public readonly Fv3[] Pivot = new Fv3[FaunaRig.BoneCount];

        /// <summary>Bones that carry vertices (bit per <see cref="FaunaBone"/>).</summary>
        public uint UsedBones;

        public FaunaSpecies Species;
        public int Lod;
        public CoatPattern Pattern;

        /// <summary>Height of the eyes above the ground in the bind pose (for look-at and camera framing).</summary>
        public float EyeHeightM;

        /// <summary>Radius of a sphere round the root that holds every pose (for culling bounds).</summary>
        public float BoundsRadiusM;

        /// <summary>Lowest point of the body under the chest and under the loins (bind pose, model space; y and z),
        /// for lying poses that rest the belly on the ground.</summary>
        public float BellyFrontY, BellyFrontZ, BellyRearY, BellyRearZ;

        /// <summary>Half the width of the barrel (largest |x| of the body), for the poses lying on one side.</summary>
        public float BodyHalfWidthM;

        public FaunaMesh(int vertexCapacity = 1024, int indexCapacity = 3072)
        {
            Mesh = new MeshData(vertexCapacity, indexCapacity);
            Bone0 = new byte[vertexCapacity];
            Bone1 = new byte[vertexCapacity];
            Weight0 = new float[vertexCapacity];
            Part = new byte[vertexCapacity];
        }

        public int VertexCount
        {
            get { return Mesh.VertexCount; }
        }

        public int TriangleCount
        {
            get { return Mesh.TriangleCount; }
        }

        /// <summary>Forget the contents; keeps the buffers.</summary>
        public void Clear()
        {
            Mesh.Clear();
            UsedBones = 0;
            for (int i = 0; i < Pivot.Length; i++) Pivot[i] = Fv3.Zero;
            EyeHeightM = 0f;
            BoundsRadiusM = 0f;
            BellyFrontY = BellyFrontZ = BellyRearY = BellyRearZ = 0f;
            BodyHalfWidthM = 0f;
        }

        /// <summary>Makes the side arrays hold at least <paramref name="vertices"/> entries.</summary>
        internal void EnsureSide(int vertices)
        {
            if (Bone0.Length >= vertices) return;
            int cap = Math.Max(vertices, Bone0.Length * 2);
            Array.Resize(ref Bone0, cap);
            Array.Resize(ref Bone1, cap);
            Array.Resize(ref Weight0, cap);
            Array.Resize(ref Part, cap);
        }

        /// <summary>True when bone <paramref name="b"/> carries vertices.</summary>
        public bool Uses(FaunaBone b)
        {
            return (UsedBones & (1u << (int)b)) != 0;
        }

        /// <summary>Copies another mesh (geometry, weights, pivots) into this one.</summary>
        public void CopyFrom(FaunaMesh src)
        {
            Clear();
            MeshData s = src.Mesh, d = Mesh;
            d.Reserve(s.VertexCount, s.IndexCount);
            Array.Copy(s.Positions, d.Positions, s.VertexCount * 3);
            Array.Copy(s.Normals, d.Normals, s.VertexCount * 3);
            Array.Copy(s.Colors, d.Colors, s.VertexCount * 4);
            Array.Copy(s.Uv0, d.Uv0, s.VertexCount * 2);
            Array.Copy(s.Indices, d.Indices, s.IndexCount);
            d.VertexCount = s.VertexCount;
            d.IndexCount = s.IndexCount;
            d.HasUv0 = s.HasUv0;
            EnsureSide(s.VertexCount);
            Array.Copy(src.Bone0, Bone0, s.VertexCount);
            Array.Copy(src.Bone1, Bone1, s.VertexCount);
            Array.Copy(src.Weight0, Weight0, s.VertexCount);
            Array.Copy(src.Part, Part, s.VertexCount);
            Array.Copy(src.Pivot, Pivot, Pivot.Length);
            UsedBones = src.UsedBones;
            Species = src.Species;
            Lod = src.Lod;
            Pattern = src.Pattern;
            EyeHeightM = src.EyeHeightM;
            BoundsRadiusM = src.BoundsRadiusM;
            BellyFrontY = src.BellyFrontY;
            BellyFrontZ = src.BellyFrontZ;
            BellyRearY = src.BellyRearY;
            BellyRearZ = src.BellyRearZ;
            BodyHalfWidthM = src.BodyHalfWidthM;
        }
    }
}
