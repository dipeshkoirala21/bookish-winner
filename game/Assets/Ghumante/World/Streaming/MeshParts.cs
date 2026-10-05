using System;
using System.Collections.Generic;
using Ghumante.Core.Meshing;

namespace Ghumante.World.Streaming
{
    /// <summary>A run of a layer's triangles that belongs to one part (a band block, a hero LOD): indices
    /// [<see cref="FirstIndex"/>, <see cref="FirstIndex"/> + <see cref="IndexCount"/>).</summary>
    public struct PartRange
    {
        public int Part;
        public int FirstIndex;
        public int IndexCount;

        public PartRange(int part, int firstIndex, int indexCount)
        {
            Part = part;
            FirstIndex = firstIndex;
            IndexCount = indexCount;
        }
    }

    /// <summary>
    /// Spatial regrouping of a layer's triangles (W2_DESIGN 2.4 band blocks): every triangle goes to the square block
    /// of side <c>blockM</c> holding its centroid, and the blocks are written one after the other with their own
    /// compact vertex runs, so each block uploads as its own chunks and the band controller can switch it on and off.
    /// A block's renderer is enabled whenever its bounds touch the band's distance range (a conservative test) and the
    /// band shader dithers the exact edge per fragment, so cutting a building between two blocks is never visible.
    /// Deterministic; engine-free; allocation-free once the scratch buffers have grown.
    /// </summary>
    public static class MeshParts
    {
        /// <summary>Blocks per side for a tile of <paramref name="tileSizeM"/>.</summary>
        public static int BlocksPerSide(double tileSizeM, double blockM)
        {
            if (!(blockM > 0)) throw new ArgumentOutOfRangeException(nameof(blockM));
            return Math.Max(1, (int)Math.Ceiling(tileSizeM / blockM - 1e-9));
        }

        /// <summary>Block (column, row) of a tile-local point, clamped into the tile.</summary>
        public static int BlockOf(double x, double z, double blockM, int perSide)
        {
            int bx = (int)Math.Floor(x / blockM), bz = (int)Math.Floor(z / blockM);
            if (bx < 0) bx = 0;
            else if (bx >= perSide) bx = perSide - 1;
            if (bz < 0) bz = 0;
            else if (bz >= perSide) bz = perSide - 1;
            return bz * perSide + bx;
        }

        /// <summary>Scratch buffers of <see cref="ByBlocks"/> (one per worker; reuse across calls).</summary>
        public sealed class Scratch
        {
            internal int[] TriBlock = new int[0];
            internal int[] Order = new int[0];
            internal int[] Counts = new int[0];
            internal int[] Map = new int[0];
            internal int[] Stamp = new int[0];
            internal int StampValue;
        }

        /// <summary>
        /// Writes the triangles of <paramref name="src"/> into <paramref name="dst"/> (cleared first) grouped by block,
        /// row-major from the south-west, and appends one <see cref="PartRange"/> per non-empty block (part = block
        /// index) to <paramref name="parts"/>. Vertices used by triangles of two blocks are duplicated. Returns the
        /// number of parts added.
        /// </summary>
        public static int ByBlocks(MeshData src, double tileSizeM, double blockM, MeshData dst, List<PartRange> parts, Scratch s)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (s == null) throw new ArgumentNullException(nameof(s));
            dst.Clear();
            int triCount = src.IndexCount / 3;
            if (triCount == 0) return 0;
            int perSide = BlocksPerSide(tileSizeM, blockM);
            int blocks = perSide * perSide;
            if (s.TriBlock.Length < triCount)
            {
                s.TriBlock = new int[Math.Max(triCount, s.TriBlock.Length * 2)];
                s.Order = new int[s.TriBlock.Length];
            }
            if (s.Counts.Length < blocks + 1) s.Counts = new int[blocks + 1];
            if (s.Map.Length < src.VertexCount)
            {
                int cap = Math.Max(src.VertexCount, s.Map.Length * 2);
                s.Map = new int[cap];
                s.Stamp = new int[cap];
                s.StampValue = 0;
            }
            Array.Clear(s.Counts, 0, blocks + 1);
            int[] idx = src.Indices;
            float[] p = src.Positions;
            for (int t = 0; t < triCount; t++)
            {
                int a = idx[3 * t] * 3, b = idx[3 * t + 1] * 3, c = idx[3 * t + 2] * 3;
                double cx = (p[a] + p[b] + p[c]) / 3.0, cz = (p[a + 2] + p[b + 2] + p[c + 2]) / 3.0;
                int k = BlockOf(cx, cz, blockM, perSide);
                s.TriBlock[t] = k;
                s.Counts[k + 1]++;
            }
            for (int k = 0; k < blocks; k++) s.Counts[k + 1] += s.Counts[k];
            // Counting sort (stable: triangles keep their order inside a block).
            for (int t = 0; t < triCount; t++) s.Order[s.Counts[s.TriBlock[t]]++] = t;
            // Counts[k] now holds the end of block k; recover the starts by walking back.
            dst.HasUv0 = src.HasUv0;
            dst.Reserve(src.VertexCount + src.VertexCount / 8, src.IndexCount);
            int added = 0, start = 0;
            for (int k = 0; k < blocks; k++)
            {
                int end = s.Counts[k];
                if (end <= start) continue;
                if (++s.StampValue == int.MaxValue)
                {
                    Array.Clear(s.Stamp, 0, s.Stamp.Length);
                    s.StampValue = 1;
                }
                int stamp = s.StampValue;
                int firstIndex = dst.IndexCount;
                for (int o = start; o < end; o++)
                {
                    int t = s.Order[o];
                    int ia = Copy(src, dst, idx[3 * t], s, stamp);
                    int ib = Copy(src, dst, idx[3 * t + 1], s, stamp);
                    int ic = Copy(src, dst, idx[3 * t + 2], s, stamp);
                    dst.AddTriangle(ia, ib, ic);
                }
                parts.Add(new PartRange(k, firstIndex, dst.IndexCount - firstIndex));
                added++;
                start = end;
            }
            return added;
        }

        private static int Copy(MeshData src, MeshData dst, int v, Scratch s, int stamp)
        {
            if (s.Stamp[v] == stamp) return s.Map[v];
            int n = dst.VertexCount;
            if ((n + 1) * 3 > dst.Positions.Length) dst.Reserve(Math.Max(64, n / 2), 0);
            Array.Copy(src.Positions, v * 3, dst.Positions, n * 3, 3);
            Array.Copy(src.Normals, v * 3, dst.Normals, n * 3, 3);
            Array.Copy(src.Colors, v * 4, dst.Colors, n * 4, 4);
            if (src.HasUv0) Array.Copy(src.Uv0, v * 2, dst.Uv0, n * 2, 2);
            dst.VertexCount = n + 1;
            s.Stamp[v] = stamp;
            s.Map[v] = n;
            return n;
        }

        /// <summary>Appends <paramref name="src"/> to <paramref name="dst"/> as one part (hero LODs, cells).</summary>
        public static void AppendPart(MeshData src, MeshData dst, int part, List<PartRange> parts)
        {
            if (src.IndexCount == 0) return;
            int v0 = dst.VertexCount, i0 = dst.IndexCount;
            if (src.HasUv0) dst.HasUv0 = true;
            dst.Reserve(src.VertexCount, src.IndexCount);
            Array.Copy(src.Positions, 0, dst.Positions, v0 * 3, src.VertexCount * 3);
            Array.Copy(src.Normals, 0, dst.Normals, v0 * 3, src.VertexCount * 3);
            Array.Copy(src.Colors, 0, dst.Colors, v0 * 4, src.VertexCount * 4);
            if (dst.HasUv0)
            {
                if (src.HasUv0) Array.Copy(src.Uv0, 0, dst.Uv0, v0 * 2, src.VertexCount * 2);
                else Array.Clear(dst.Uv0, v0 * 2, src.VertexCount * 2);
            }
            dst.VertexCount = v0 + src.VertexCount;
            for (int i = 0; i < src.IndexCount; i++) dst.Indices[i0 + i] = src.Indices[i] + v0;
            dst.IndexCount = i0 + src.IndexCount;
            parts.Add(new PartRange(part, i0, src.IndexCount));
        }
    }
}
