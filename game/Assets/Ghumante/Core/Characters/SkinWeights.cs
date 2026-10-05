using System;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// Per-vertex skin weights that run alongside a <see cref="Meshing.MeshData"/>: up to two bones per vertex
    /// (<see cref="Bone0"/> with <see cref="Weight0"/>, <see cref="Bone1"/> with 1 − Weight0), which the Unity side writes
    /// as a <c>BoneWeight</c> (ASSET_MANIFEST 9.1 allows 4 on the player and 2 on NPCs; two blend every joint of these
    /// primitives smoothly). Grow-only like MeshData; <see cref="Clear"/> keeps the buffers.
    /// </summary>
    public sealed class SkinWeights
    {
        public byte[] Bone0;
        public byte[] Bone1;
        public float[] Weight0;
        public int Count;

        public SkinWeights(int capacity = 1024)
        {
            if (capacity < 4) capacity = 4;
            Bone0 = new byte[capacity];
            Bone1 = new byte[capacity];
            Weight0 = new float[capacity];
        }

        public void Clear()
        {
            Count = 0;
        }

        /// <summary>Appends one vertex's weights; <paramref name="w0"/> is clamped to [0, 1].</summary>
        public void Add(Bone b0, Bone b1, float w0)
        {
            if (Count == Bone0.Length)
            {
                int cap = Bone0.Length * 2;
                Array.Resize(ref Bone0, cap);
                Array.Resize(ref Bone1, cap);
                Array.Resize(ref Weight0, cap);
            }
            if (float.IsNaN(w0)) w0 = 1f;
            w0 = w0 < 0f ? 0f : w0 > 1f ? 1f : w0;
            if (b0 == b1) w0 = 1f;
            // Keep the heavier bone first (Unity sorts by weight; so does the crowd baker).
            if (w0 < 0.5f)
            {
                Bone t = b0;
                b0 = b1;
                b1 = t;
                w0 = 1f - w0;
            }
            Bone0[Count] = (byte)b0;
            Bone1[Count] = (byte)b1;
            Weight0[Count] = w0;
            Count++;
        }
    }
}
