using System;

namespace Ghumante.Core.Meshing.Shapes
{
    /// <summary>
    /// Vertex-colour helpers over a vertex range of a <see cref="MeshData"/> (typically from a shape's returned
    /// first vertex to <c>m.VertexCount</c>): vertical gradients, brightness ramps, tints and deterministic jitter
    /// (per vertex keyed by position so shared seams agree, or per triangle for flat-shaded parts). Alpha (the
    /// instance tint weight) is always kept.
    /// </summary>
    public static class ShapeColor
    {
        /// <summary>Replace the RGB of vertices [first, first + count) by a gradient from <paramref name="bottom"/>
        /// at y0 to <paramref name="top"/> at y1 (mesh coordinates, clamped).</summary>
        public static void VerticalGradient(MeshData m, int first, int count, double y0, double y1, uint bottom, uint top)
        {
            double inv = Math.Abs(y1 - y0) < 1e-12 ? 0 : 1.0 / (y1 - y0);
            for (int v = first; v < first + count; v++)
            {
                double t = (m.Positions[3 * v + 1] - y0) * inv;
                uint c = MeshColor.Lerp(bottom, top, (float)t);
                Set(m, v, c, m.Colors[4 * v + 3]);
            }
        }

        /// <summary>Multiply RGB by a factor ramping from <paramref name="bottomFactor"/> at y0 to
        /// <paramref name="topFactor"/> at y1: darker trunk bases, sun-bleached tops, grimy plinths.</summary>
        public static void VerticalShade(MeshData m, int first, int count, double y0, double y1, float bottomFactor, float topFactor)
        {
            double inv = Math.Abs(y1 - y0) < 1e-12 ? 0 : 1.0 / (y1 - y0);
            for (int v = first; v < first + count; v++)
            {
                double t = (m.Positions[3 * v + 1] - y0) * inv;
                if (t < 0) t = 0;
                else if (t > 1) t = 1;
                Scale(m, v, (float)(bottomFactor + (topFactor - bottomFactor) * t));
            }
        }

        /// <summary>Multiply RGB of the range by <paramref name="tint"/> (0xRRGGBBAA, alpha ignored).</summary>
        public static void Tint(MeshData m, int first, int count, uint tint)
        {
            int tr = MeshColor.R(tint), tg = MeshColor.G(tint), tb = MeshColor.B(tint);
            for (int v = first; v < first + count; v++)
            {
                int c = 4 * v;
                m.Colors[c] = (byte)(m.Colors[c] * tr / 255);
                m.Colors[c + 1] = (byte)(m.Colors[c + 1] * tg / 255);
                m.Colors[c + 2] = (byte)(m.Colors[c + 2] * tb / 255);
            }
        }

        /// <summary>
        /// Deterministic per-vertex variation: brightness ±<paramref name="amount"/> and a small hue drift, keyed
        /// by the vertex position quantised to <paramref name="cell"/> metres (so coincident seam vertices agree and
        /// neighbouring vertices within a cell move together: soft patches on foliage, plaster, rock).
        /// </summary>
        public static void JitterByPosition(MeshData m, int first, int count, float amount, uint seed, double cell = 0.25)
        {
            double inv = cell > 1e-9 ? 1.0 / cell : 1e4;
            for (int v = first; v < first + count; v++)
            {
                int qx = (int)Math.Floor(m.Positions[3 * v] * inv), qy = (int)Math.Floor(m.Positions[3 * v + 1] * inv);
                int qz = (int)Math.Floor(m.Positions[3 * v + 2] * inv);
                Jitter(m, v, ShapeNoise.Hash(qx, qy, qz, seed), amount);
            }
        }

        /// <summary>
        /// Deterministic per-face variation for flat-shaded parts (each face owning its vertices): every group of
        /// <paramref name="trianglesPerFace"/> triangles in [firstIndex, firstIndex + indexCount) gets one
        /// brightness/hue offset (±<paramref name="amount"/>): tiles, planks, bricks, scales. A vertex shared
        /// between faces is changed once, by the first face that uses it.
        /// </summary>
        public static void JitterFaces(MeshData m, int firstIndex, int indexCount, float amount, uint seed, int trianglesPerFace = 2)
        {
            if (trianglesPerFace < 1) trianglesPerFace = 1;
            if (s_stamp == null || s_stamp.Length < m.VertexCount) s_stamp = new int[Math.Max(m.VertexCount, 1024)];
            if (++s_generation == int.MaxValue)
            {
                Array.Clear(s_stamp, 0, s_stamp.Length);
                s_generation = 1;
            }
            int tris = indexCount / 3;
            for (int t = 0; t < tris; t++)
            {
                uint h = ShapeNoise.Hash(t / trianglesPerFace, 0x5EED, 0, seed);
                for (int k = 0; k < 3; k++)
                {
                    int v = m.Indices[firstIndex + 3 * t + k];
                    if (s_stamp[v] == s_generation) continue;
                    s_stamp[v] = s_generation;
                    Jitter(m, v, h, amount);
                }
            }
        }

        [ThreadStatic] private static int[] s_stamp;
        [ThreadStatic] private static int s_generation;

        private static void Jitter(MeshData m, int v, uint h, float amount)
        {
            float f = 1 + amount * ((h & 0xFFFF) / 32767.5f - 1);
            float hue = amount * 0.35f * (((h >> 16) & 0xFF) / 127.5f - 1);
            int c = 4 * v;
            m.Colors[c] = Clamp(m.Colors[c] * (f + hue));
            m.Colors[c + 1] = Clamp(m.Colors[c + 1] * f);
            m.Colors[c + 2] = Clamp(m.Colors[c + 2] * (f - hue));
        }

        private static void Set(MeshData m, int v, uint rgb, byte alpha)
        {
            int c = 4 * v;
            m.Colors[c] = (byte)(rgb >> 24);
            m.Colors[c + 1] = (byte)(rgb >> 16);
            m.Colors[c + 2] = (byte)(rgb >> 8);
            m.Colors[c + 3] = alpha;
        }

        private static void Scale(MeshData m, int v, float f)
        {
            int c = 4 * v;
            m.Colors[c] = Clamp(m.Colors[c] * f);
            m.Colors[c + 1] = Clamp(m.Colors[c + 1] * f);
            m.Colors[c + 2] = Clamp(m.Colors[c + 2] * f);
        }

        private static byte Clamp(float v)
        {
            return v <= 0 ? (byte)0 : v >= 255 ? (byte)255 : (byte)(v + 0.5f);
        }
    }
}
