using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Tests
{
    /// <summary>One named group of an OBJ file (<see cref="ObjDump.Write(string, ObjPart[])"/>).</summary>
    public readonly struct ObjPart
    {
        public readonly string Name;
        public readonly MeshData Mesh;

        public ObjPart(string name, MeshData mesh)
        {
            Name = name;
            Mesh = mesh;
        }
    }

    /// <summary>
    /// Writes <see cref="MeshData"/> as Wavefront OBJ for the visual self-check with tools/mesh-preview/render.py
    /// (docs/W2_DETAIL_CONTRACT.md §3 and §6; tools/mesh-preview/README.md).
    /// <para>
    /// Gated: <see cref="Write(MeshData, string)"/> writes only when the environment variable
    /// <see cref="EnvVar"/> (<c>GHUMANTE_PREVIEW_DIR</c>) names a folder, so ordinary test runs write nothing.
    /// Relative paths resolve under that folder (sub-folders are created).
    /// </para>
    /// <para>
    /// Format: <c>v x y z r g b</c> (colour 0..1 from the RGBA32 albedo tint; alpha is dropped), <c>vt u v</c> when
    /// <see cref="MeshData.HasUv0"/> (u = <see cref="MaterialChannel"/>, v = baked AO), <c>vn</c> per vertex, one
    /// <c>o</c> group per part and triangles <c>f a/a/a b/b/b c/c/c</c> (or <c>a//a</c> without UVs). Unity is
    /// left-handed (X east, Y up, Z north, clockwise front faces); OBJ is right-handed, so Z (positions and normals)
    /// is negated and every triangle's winding is reversed. In the OBJ, north is -Z; the preview tool's
    /// <c>--unity</c> flag takes eye, look and target points in Unity coordinates instead.
    /// </para>
    /// </summary>
    public static class ObjDump
    {
        /// <summary>The environment variable naming the output folder; unset or empty disables writing.</summary>
        public const string EnvVar = "GHUMANTE_PREVIEW_DIR";

        /// <summary>The output folder, or null when <see cref="EnvVar"/> is unset (dumps are then skipped).</summary>
        public static string Dir
        {
            get
            {
                string d = Environment.GetEnvironmentVariable(EnvVar);
                return string.IsNullOrWhiteSpace(d) ? null : d;
            }
        }

        /// <summary>True when <see cref="EnvVar"/> is set.</summary>
        public static bool Enabled
        {
            get { return Dir != null; }
        }

        /// <summary>Write <paramref name="m"/> to <paramref name="path"/> (relative to <see cref="Dir"/> unless
        /// rooted) as one group named after the file. Returns the full path written, or null (nothing written) when
        /// <see cref="Enabled"/> is false.</summary>
        public static string Write(MeshData m, string path)
        {
            return Write(path, null, new ObjPart(Path.GetFileNameWithoutExtension(path), m));
        }

        /// <summary>As <see cref="Write(MeshData, string)"/> with header comment lines; a line
        /// <c>meshpreview: key=value ...</c> sets render defaults (views, eye, look, grid, fog, ...).</summary>
        public static string Write(MeshData m, string path, IList<string> comments)
        {
            return Write(path, comments, new ObjPart(Path.GetFileNameWithoutExtension(path), m));
        }

        /// <summary>Write several meshes into one file, one OBJ group per part (render with
        /// <c>--exploded</c> or <c>--group</c>). Returns the full path, or null when disabled.</summary>
        public static string Write(string path, params ObjPart[] parts)
        {
            return Write(path, null, parts);
        }

        /// <summary>Write several meshes with header comment lines into one file; null when disabled.</summary>
        public static string Write(string path, IList<string> comments, params ObjPart[] parts)
        {
            string dir = Dir;
            if (dir == null) return null;
            string full = Path.IsPathRooted(path) ? path : Path.Combine(dir, path);
            string parent = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            using (var w = new StreamWriter(full, false, new UTF8Encoding(false), 1 << 16))
            {
                w.NewLine = "\n";
                Format(w, comments, parts);
            }
            return full;
        }

        /// <summary>The OBJ text of <paramref name="parts"/> (no file access; for tests of the format).</summary>
        public static string Format(IList<string> comments, params ObjPart[] parts)
        {
            using (var w = new StringWriter(CultureInfo.InvariantCulture))
            {
                w.NewLine = "\n";
                Format(w, comments, parts);
                return w.ToString();
            }
        }

        /// <summary>Write the OBJ text of <paramref name="parts"/> to <paramref name="w"/>.</summary>
        public static void Format(TextWriter w, IList<string> comments, params ObjPart[] parts)
        {
            if (w == null) throw new ArgumentNullException(nameof(w));
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            CultureInfo c = CultureInfo.InvariantCulture;
            w.WriteLine("# Ghumante ObjDump (core-tests/Support/ObjDump.cs): Unity Z negated, winding reversed; north is -Z");
            if (comments != null)
                foreach (string line in comments)
                    w.WriteLine("# " + line);
            bool anyUv = false;
            foreach (ObjPart p in parts)
                if (p.Mesh != null && p.Mesh.HasUv0 && p.Mesh.VertexCount > 0) anyUv = true;

            var sb = new StringBuilder(256);
            foreach (ObjPart p in parts)
            {
                MeshData m = p.Mesh;
                if (m == null) continue;
                for (int v = 0; v < m.VertexCount; v++)
                {
                    int i = v * 3, k = v * 4;
                    sb.Clear();
                    sb.Append("v ");
                    F(sb, m.Positions[i], "0.####", c).Append(' ');
                    F(sb, m.Positions[i + 1], "0.####", c).Append(' ');
                    F(sb, -m.Positions[i + 2], "0.####", c).Append(' ');
                    F(sb, m.Colors[k] / 255f, "0.###", c).Append(' ');
                    F(sb, m.Colors[k + 1] / 255f, "0.###", c).Append(' ');
                    F(sb, m.Colors[k + 2] / 255f, "0.###", c);
                    w.WriteLine(sb.ToString());
                }
            }
            foreach (ObjPart p in parts)
            {
                MeshData m = p.Mesh;
                if (m == null) continue;
                for (int v = 0; v < m.VertexCount; v++)
                {
                    int i = v * 3;
                    sb.Clear();
                    sb.Append("vn ");
                    F(sb, m.Normals[i], "0.####", c).Append(' ');
                    F(sb, m.Normals[i + 1], "0.####", c).Append(' ');
                    F(sb, -m.Normals[i + 2], "0.####", c);
                    w.WriteLine(sb.ToString());
                }
            }
            if (anyUv)
            {
                foreach (ObjPart p in parts)
                {
                    MeshData m = p.Mesh;
                    if (m == null) continue;
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        sb.Clear();
                        sb.Append("vt ");
                        if (m.HasUv0)
                        {
                            F(sb, m.Uv0[v * 2], "0.###", c).Append(' ');
                            F(sb, m.Uv0[v * 2 + 1], "0.###", c);
                        }
                        else
                        {
                            sb.Append("0 1"); // Plain, open: renders like a mesh without UV0
                        }
                        w.WriteLine(sb.ToString());
                    }
                }
            }

            int baseVertex = 0;
            foreach (ObjPart p in parts)
            {
                MeshData m = p.Mesh;
                if (m == null) continue;
                w.WriteLine("o " + (string.IsNullOrWhiteSpace(p.Name) ? "mesh" : p.Name.Replace(' ', '_')));
                for (int t = 0; t + 2 < m.IndexCount; t += 3)
                {
                    // Unity (a, b, c) clockwise in a left-handed frame -> (a, c, b) counter-clockwise in OBJ.
                    int a = m.Indices[t] + 1 + baseVertex, b = m.Indices[t + 2] + 1 + baseVertex, d = m.Indices[t + 1] + 1 + baseVertex;
                    sb.Clear();
                    sb.Append("f ");
                    Corner(sb, a, anyUv).Append(' ');
                    Corner(sb, b, anyUv).Append(' ');
                    Corner(sb, d, anyUv);
                    w.WriteLine(sb.ToString());
                }
                baseVertex += m.VertexCount;
            }
        }

        /// <summary>Append <paramref name="src"/> to <paramref name="dst"/> moved by (dx, dy, dz) (for assembling
        /// a preview from pieces, e.g. a vehicle body and its wheels).</summary>
        public static void Append(MeshData dst, MeshData src, float dx, float dy, float dz)
        {
            int baseV = dst.VertexCount;
            for (int v = 0; v < src.VertexCount; v++)
            {
                int i = v * 3, k = v * 4;
                uint rgba = ((uint)src.Colors[k] << 24) | ((uint)src.Colors[k + 1] << 16) | ((uint)src.Colors[k + 2] << 8) | src.Colors[k + 3];
                if (src.HasUv0)
                    dst.AddVertex(src.Positions[i] + dx, src.Positions[i + 1] + dy, src.Positions[i + 2] + dz, src.Normals[i], src.Normals[i + 1], src.Normals[i + 2],
                        rgba, src.Uv0[v * 2], src.Uv0[v * 2 + 1]);
                else
                    dst.AddVertex(src.Positions[i] + dx, src.Positions[i + 1] + dy, src.Positions[i + 2] + dz, src.Normals[i], src.Normals[i + 1], src.Normals[i + 2], rgba);
            }
            for (int t = 0; t + 2 < src.IndexCount; t += 3)
                dst.AddTriangle(src.Indices[t] + baseV, src.Indices[t + 1] + baseV, src.Indices[t + 2] + baseV);
        }

        /// <summary>The triangles of <paramref name="src"/> whose centroid lies inside (or, with
        /// <paramref name="inside"/> false, outside) the X/Z rectangle, as a new mesh (unused vertices dropped).</summary>
        public static MeshData Crop(MeshData src, float x0, float z0, float x1, float z1, bool inside)
        {
            var dst = new MeshData();
            var map = new int[src.VertexCount];
            for (int i = 0; i < map.Length; i++) map[i] = -1;
            for (int t = 0; t + 2 < src.IndexCount; t += 3)
            {
                int a = src.Indices[t], b = src.Indices[t + 1], c = src.Indices[t + 2];
                float cx = (src.Positions[a * 3] + src.Positions[b * 3] + src.Positions[c * 3]) / 3f;
                float cz = (src.Positions[a * 3 + 2] + src.Positions[b * 3 + 2] + src.Positions[c * 3 + 2]) / 3f;
                bool isIn = cx >= x0 && cx <= x1 && cz >= z0 && cz <= z1;
                if (isIn != inside) continue;
                dst.AddTriangle(Copy(dst, src, a, map), Copy(dst, src, b, map), Copy(dst, src, c, map));
            }
            return dst;
        }

        private static int Copy(MeshData dst, MeshData src, int v, int[] map)
        {
            if (map[v] >= 0) return map[v];
            int i = v * 3, k = v * 4;
            uint rgba = ((uint)src.Colors[k] << 24) | ((uint)src.Colors[k + 1] << 16) | ((uint)src.Colors[k + 2] << 8) | src.Colors[k + 3];
            map[v] = src.HasUv0
                ? dst.AddVertex(src.Positions[i], src.Positions[i + 1], src.Positions[i + 2], src.Normals[i], src.Normals[i + 1], src.Normals[i + 2], rgba, src.Uv0[v * 2], src.Uv0[v * 2 + 1])
                : dst.AddVertex(src.Positions[i], src.Positions[i + 1], src.Positions[i + 2], src.Normals[i], src.Normals[i + 1], src.Normals[i + 2], rgba);
            return map[v];
        }

        private static StringBuilder F(StringBuilder sb, float v, string format, CultureInfo c)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
            string s = v.ToString(format, c);
            return sb.Append(s == "-0" ? "0" : s);
        }

        private static StringBuilder Corner(StringBuilder sb, int i, bool uv)
        {
            sb.Append(i);
            if (uv) sb.Append('/').Append(i).Append('/').Append(i);
            else sb.Append("//").Append(i);
            return sb;
        }
    }
}
