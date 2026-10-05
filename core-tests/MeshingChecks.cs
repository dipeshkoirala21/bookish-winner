using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Structural checks shared by the meshing tests.</summary>
    internal static class MeshingChecks
    {
        /// <summary>Finite positions and unit normals, opaque colours, whole triangles, indices in range.</summary>
        public static void AssertWellFormed(MeshData m, string what)
        {
            Assert.That(m.IndexCount % 3, Is.EqualTo(0), what);
            Assert.That(m.Positions.Length, Is.GreaterThanOrEqualTo(m.VertexCount * 3), what);
            Assert.That(m.Indices.Length, Is.GreaterThanOrEqualTo(m.IndexCount), what);
            for (int v = 0; v < m.VertexCount; v++)
            {
                float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z)) Assert.Fail(what + ": vertex " + v + " not finite");
                float nx = m.Normals[3 * v], ny = m.Normals[3 * v + 1], nz = m.Normals[3 * v + 2];
                if (!IsFinite(nx) || !IsFinite(ny) || !IsFinite(nz)) Assert.Fail(what + ": normal " + v + " not finite");
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (Math.Abs(len - 1) > 1e-3) Assert.Fail(what + ": normal " + v + " has length " + len);
                if (m.Colors[4 * v + 3] != 255) Assert.Fail(what + ": vertex " + v + " not opaque");
            }
            for (int i = 0; i < m.IndexCount; i++)
                if (m.Indices[i] < 0 || m.Indices[i] >= m.VertexCount) Assert.Fail(what + ": index " + i + " out of range");
        }

        public static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        /// <summary>Unnormalised geometric normal cross(b - a, c - a) of triangle t (Unity front side).</summary>
        public static void Facet(MeshData m, int t, out double nx, out double ny, out double nz)
        {
            int a = m.Indices[3 * t], b = m.Indices[3 * t + 1], c = m.Indices[3 * t + 2];
            double ux = m.Positions[3 * b] - (double)m.Positions[3 * a], uy = m.Positions[3 * b + 1] - (double)m.Positions[3 * a + 1];
            double uz = m.Positions[3 * b + 2] - (double)m.Positions[3 * a + 2];
            double vx = m.Positions[3 * c] - (double)m.Positions[3 * a], vy = m.Positions[3 * c + 1] - (double)m.Positions[3 * a + 1];
            double vz = m.Positions[3 * c + 2] - (double)m.Positions[3 * a + 2];
            nx = uy * vz - uz * vy;
            ny = uz * vx - ux * vz;
            nz = ux * vy - uy * vx;
        }

        /// <summary>
        /// Every non-degenerate triangle in [first, last) has its front (Unity winding) on the side of its vertex
        /// normals: dot(unit facet normal, vertex normal) &gt; <paramref name="minDot"/> for all three vertices.
        /// Returns the number of degenerate triangles skipped.
        /// </summary>
        public static int AssertFrontFacesAgreeWithNormals(MeshData m, int first, int last, double minDot, string what)
        {
            int degenerate = 0;
            for (int t = first; t < last; t++)
            {
                double nx, ny, nz;
                Facet(m, t, out nx, out ny, out nz);
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-6)
                {
                    degenerate++;
                    continue;
                }
                for (int k = 0; k < 3; k++)
                {
                    int v = m.Indices[3 * t + k];
                    double d = (nx * m.Normals[3 * v] + ny * m.Normals[3 * v + 1] + nz * m.Normals[3 * v + 2]) / len;
                    if (d <= minDot) Assert.Fail(what + ": triangle " + t + " faces away from its normals (dot " + d + ")");
                }
            }
            return degenerate;
        }

        /// <summary>Height of the mesh triangle t at (px, pz) by barycentric interpolation; false when (px, pz) lies
        /// outside it (with a small tolerance) or it is degenerate in plan.</summary>
        public static bool TriangleHeight(MeshData m, int t, double px, double pz, out double h)
        {
            int a = m.Indices[3 * t], b = m.Indices[3 * t + 1], c = m.Indices[3 * t + 2];
            double ax = m.Positions[3 * a], az = m.Positions[3 * a + 2], bx = m.Positions[3 * b], bz = m.Positions[3 * b + 2];
            double cx = m.Positions[3 * c], cz = m.Positions[3 * c + 2];
            double det = (bx - ax) * (cz - az) - (cx - ax) * (bz - az);
            h = 0;
            if (Math.Abs(det) < 1e-12) return false;
            double wb = ((px - ax) * (cz - az) - (cx - ax) * (pz - az)) / det;
            double wc = ((bx - ax) * (pz - az) - (px - ax) * (bz - az)) / det;
            double wa = 1 - wb - wc;
            const double eps = -1e-7;
            if (wa < eps || wb < eps || wc < eps) return false;
            h = wa * m.Positions[3 * a + 1] + wb * m.Positions[3 * b + 1] + wc * m.Positions[3 * c + 1];
            return true;
        }

        /// <summary>A synthetic tile whose heights follow <paramref name="f"/>(game x, game z), quantised like HGHT.</summary>
        public static TileData SyntheticTile(TileId id, Func<double, double, double> f, int n = 129, Biome biome = Biome.UrbanDense)
        {
            var t = new TileData { Tile = id, HeightsN = n, HeightsQ = new ushort[n * n], BiomesN = 65, Biomes = new Biome[65 * 65] };
            double cell = id.Size / (n - 1);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    t.HeightsQ[j * n + i] = Ght.Quantize(f(id.X0 + i * cell, id.Z0 + j * cell));
            for (int k = 0; k < t.Biomes.Length; k++) t.Biomes[k] = biome;
            t.Flags = Ght.FlagHasDetail;
            return t;
        }
    }
}
