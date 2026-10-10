using System;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// OBJ dumps of the nature kit for the visual self-check (docs/W2_DETAIL_CONTRACT.md §6), in the format of the
    /// mesh-preview tool (tools/mesh-preview: <c>v x y z r g b</c>, <c>vn</c>, <c>vt</c> = channel and AO; Unity Z
    /// negated and the winding reversed, so north is -Z). Writes only when <c>GHUMANTE_PREVIEW_DIR</c> is set.
    /// </summary>
    internal static class FloraObj
    {
        public static string Dir
        {
            get
            {
                string d = Environment.GetEnvironmentVariable("GHUMANTE_PREVIEW_DIR");
                return string.IsNullOrWhiteSpace(d) ? null : d;
            }
        }

        public static string Write(MeshData m, string path, string directive = null)
        {
            string dir = Dir;
            if (dir == null || m == null) return null;
            string full = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            CultureInfo c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1 << 20);
            sb.Append("# Ghumante nature kit dump: Unity Z negated, winding reversed; north is -Z\n");
            if (directive != null) sb.Append("# meshpreview: ").Append(directive).Append('\n');
            for (int v = 0; v < m.VertexCount; v++)
            {
                sb.Append("v ").Append(m.Positions[3 * v].ToString("0.####", c)).Append(' ').Append(m.Positions[3 * v + 1].ToString("0.####", c)).Append(' ')
                  .Append((-m.Positions[3 * v + 2]).ToString("0.####", c)).Append(' ').Append((m.Colors[4 * v] / 255f).ToString("0.###", c)).Append(' ')
                  .Append((m.Colors[4 * v + 1] / 255f).ToString("0.###", c)).Append(' ').Append((m.Colors[4 * v + 2] / 255f).ToString("0.###", c)).Append('\n');
            }
            for (int v = 0; v < m.VertexCount; v++)
                sb.Append("vn ").Append(m.Normals[3 * v].ToString("0.####", c)).Append(' ').Append(m.Normals[3 * v + 1].ToString("0.####", c)).Append(' ')
                  .Append((-m.Normals[3 * v + 2]).ToString("0.####", c)).Append('\n');
            if (m.HasUv0)
                for (int v = 0; v < m.VertexCount; v++)
                    sb.Append("vt ").Append(m.Uv0[2 * v].ToString("0.###", c)).Append(' ').Append(m.Uv0[2 * v + 1].ToString("0.###", c)).Append('\n');
            sb.Append("o ").Append(Path.GetFileNameWithoutExtension(path)).Append('\n');
            for (int t = 0; t < m.TriangleCount; t++)
            {
                int a = m.Indices[3 * t] + 1, b = m.Indices[3 * t + 2] + 1, d = m.Indices[3 * t + 1] + 1;
                if (m.HasUv0) sb.Append("f ").Append(a).Append('/').Append(a).Append('/').Append(a).Append(' ').Append(b).Append('/').Append(b).Append('/').Append(b).Append(' ')
                                .Append(d).Append('/').Append(d).Append('/').Append(d).Append('\n');
                else sb.Append("f ").Append(a).Append("//").Append(a).Append(' ').Append(b).Append("//").Append(b).Append(' ').Append(d).Append("//").Append(d).Append('\n');
            }
            File.WriteAllText(full, sb.ToString());
            return full;
        }

        /// <summary>Append <paramref name="src"/> to <paramref name="dst"/> moved by (dx, dy, dz), optionally turned by
        /// <paramref name="yawDeg"/> (clockwise from north) and scaled (sx, sy, sz).</summary>
        public static void Append(MeshData dst, MeshData src, float dx, float dy, float dz, float yawDeg = 0f, float sx = 1f, float sy = 1f, float sz = 1f)
        {
            int baseV = dst.VertexCount;
            double a = yawDeg * Math.PI / 180;
            float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            for (int v = 0; v < src.VertexCount; v++)
            {
                float x = src.Positions[3 * v] * sx, y = src.Positions[3 * v + 1] * sy, z = src.Positions[3 * v + 2] * sz;
                // Clockwise from north (+Z) seen from above: x' = x cos + z sin, z' = -x sin + z cos.
                float rx = x * ca + z * sa, rz = -x * sa + z * ca;
                double nx = src.Normals[3 * v] / sx, ny = src.Normals[3 * v + 1] / sy, nz = src.Normals[3 * v + 2] / sz;
                double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (l < 1e-12) l = 1;
                float nrx = (float)((nx * ca + nz * sa) / l), nry = (float)(ny / l), nrz = (float)((-nx * sa + nz * ca) / l);
                int c = 4 * v;
                uint rgba = (uint)(src.Colors[c] << 24 | src.Colors[c + 1] << 16 | src.Colors[c + 2] << 8 | src.Colors[c + 3]);
                if (src.HasUv0) dst.AddVertex(rx + dx, y + dy, rz + dz, nrx, nry, nrz, rgba, src.Uv0[2 * v], src.Uv0[2 * v + 1]);
                else dst.AddVertex(rx + dx, y + dy, rz + dz, nrx, nry, nrz, rgba);
            }
            for (int i = 0; i + 2 < src.IndexCount; i += 3) dst.AddTriangle(src.Indices[i] + baseV, src.Indices[i + 1] + baseV, src.Indices[i + 2] + baseV);
        }
    }
}
