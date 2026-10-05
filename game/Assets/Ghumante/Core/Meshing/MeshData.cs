using System;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// A reusable, grow-only mesh buffer the meshers append to (M1_PLAN contract). Positions are metres relative to
    /// the tile (area) south-west corner on X (east) and Z (north) with <b>absolute</b> Y (up); a caller places the
    /// mesh at <c>tile origin - floating origin</c>. Arrays are interleaved and may be longer than the counts:
    /// <see cref="Positions"/> and <see cref="Normals"/> hold xyz per vertex, <see cref="Colors"/> RGBA32 bytes
    /// (R, G, B, A) per vertex, <see cref="Uv0"/> uv per vertex (meaningful only when <see cref="HasUv0"/>), and
    /// <see cref="Indices"/> three per triangle.
    /// <para>
    /// Winding follows Unity: a front face is clockwise seen from the front, so for triangle (a, b, c) the vector
    /// <c>cross(b - a, c - a)</c> (computed component-wise on these coordinates) points to the front, the same way
    /// as the vertex normals. <see cref="Clear"/> resets the counts and keeps the buffers, so a pooled instance stops
    /// allocating once it has grown to its working size.
    /// </para>
    /// </summary>
    public sealed class MeshData
    {
        public float[] Positions;
        public float[] Normals;
        public byte[] Colors;
        public float[] Uv0;
        public int[] Indices;
        public int VertexCount;
        public int IndexCount;

        /// <summary>True once a vertex with UVs was added since the last <see cref="Clear"/>; vertices added
        /// without UVs then read (0, 0).</summary>
        public bool HasUv0;

        public MeshData(int vertexCapacity = 1024, int indexCapacity = 3072)
        {
            if (vertexCapacity < 4) vertexCapacity = 4;
            if (indexCapacity < 6) indexCapacity = 6;
            Positions = new float[vertexCapacity * 3];
            Normals = new float[vertexCapacity * 3];
            Colors = new byte[vertexCapacity * 4];
            Uv0 = new float[vertexCapacity * 2];
            Indices = new int[indexCapacity];
        }

        public int TriangleCount
        {
            get { return IndexCount / 3; }
        }

        /// <summary>Vertices the buffers can hold without growing.</summary>
        public int VertexCapacity
        {
            get { return Positions.Length / 3; }
        }

        /// <summary>Forget the contents; keeps the buffers.</summary>
        public void Clear()
        {
            VertexCount = 0;
            IndexCount = 0;
            HasUv0 = false;
        }

        /// <summary>Make room for <paramref name="extraVertices"/> more vertices and
        /// <paramref name="extraIndices"/> more indices (grows by doubling).</summary>
        public void Reserve(int extraVertices, int extraIndices)
        {
            int nv = VertexCount + extraVertices;
            if (nv * 3 > Positions.Length)
            {
                int cap = Math.Max(nv, Positions.Length / 3 * 2);
                Array.Resize(ref Positions, cap * 3);
                Array.Resize(ref Normals, cap * 3);
                Array.Resize(ref Colors, cap * 4);
                Array.Resize(ref Uv0, cap * 2);
            }
            int ni = IndexCount + extraIndices;
            if (ni > Indices.Length) Array.Resize(ref Indices, Math.Max(ni, Indices.Length * 2));
        }

        /// <summary>Append a vertex (normal expected unit length, colour as 0xRRGGBBAA) and return its index.</summary>
        public int AddVertex(float x, float y, float z, float nx, float ny, float nz, uint rgba)
        {
            int v = VertexCount;
            if ((v + 1) * 3 > Positions.Length) Reserve(1, 0);
            int p = v * 3;
            Positions[p] = x;
            Positions[p + 1] = y;
            Positions[p + 2] = z;
            Normals[p] = nx;
            Normals[p + 1] = ny;
            Normals[p + 2] = nz;
            int c = v * 4;
            Colors[c] = (byte)(rgba >> 24);
            Colors[c + 1] = (byte)(rgba >> 16);
            Colors[c + 2] = (byte)(rgba >> 8);
            Colors[c + 3] = (byte)rgba;
            if (HasUv0)
            {
                Uv0[v * 2] = 0f;
                Uv0[v * 2 + 1] = 0f;
            }
            VertexCount = v + 1;
            return v;
        }

        /// <summary>Append a vertex with UVs; the first such vertex turns <see cref="HasUv0"/> on and zero-fills
        /// the UVs of the vertices before it.</summary>
        public int AddVertex(float x, float y, float z, float nx, float ny, float nz, uint rgba, float u, float v)
        {
            if (!HasUv0)
            {
                Array.Clear(Uv0, 0, Math.Min(Uv0.Length, VertexCount * 2));
                HasUv0 = true;
            }
            int i = AddVertex(x, y, z, nx, ny, nz, rgba);
            Uv0[i * 2] = u;
            Uv0[i * 2 + 1] = v;
            return i;
        }

        /// <summary>Append a triangle (Unity winding: clockwise seen from the front).</summary>
        public void AddTriangle(int a, int b, int c)
        {
            int i = IndexCount;
            if (i + 3 > Indices.Length) Reserve(0, 3);
            Indices[i] = a;
            Indices[i + 1] = b;
            Indices[i + 2] = c;
            IndexCount = i + 3;
        }

        /// <summary>Axis-aligned bounds of the vertices (all zero when empty).</summary>
        public void GetBounds(out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ)
        {
            if (VertexCount == 0)
            {
                minX = minY = minZ = maxX = maxY = maxZ = 0f;
                return;
            }
            minX = maxX = Positions[0];
            minY = maxY = Positions[1];
            minZ = maxZ = Positions[2];
            for (int p = 3, n = VertexCount * 3; p < n; p += 3)
            {
                float x = Positions[p], y = Positions[p + 1], z = Positions[p + 2];
                if (x < minX) minX = x;
                else if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                else if (y > maxY) maxY = y;
                if (z < minZ) minZ = z;
                else if (z > maxZ) maxZ = z;
            }
        }
    }
}
