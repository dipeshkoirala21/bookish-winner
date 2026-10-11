using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// What a plant looks like from afar or from above, measured on its mesh (no renderer): side outlines blurred to the
    /// few dozen pixels a far tree spans (<see cref="Coverage"/>, compared with <see cref="SoftIoU"/>), how solid the
    /// crown's outline is row by row (<see cref="RowSolidity"/>: a ball is solid, a star of fronds is not), and which
    /// colour wins each pixel of a depth-tested view (<see cref="ViewShare"/>: the share of a bed that reads as bloom).
    /// Used by the flora tests and the playtest reproductions of docs/research/w2/ref_assets-b.md §1-§2.
    /// </summary>
    internal static class FloraSilhouette
    {
        /// <summary>
        /// The far-off outline of a unit model: orthographic side views from four bearings (0, 45, 90, 135°), each
        /// rasterised at 104 × 96 over x ∈ [−0.65, 0.65], y ∈ [−0.05, 1.15] and averaged down 4 × 4 to 26 × 24
        /// coverage fractions (what a tree spanning a few dozen pixels shows).
        /// </summary>
        public static float[] Coverage(MeshData m)
        {
            const int W = 104, H = 96, F = 4, w = W / F, h = H / F;
            var outv = new float[4 * w * h];
            var hi = new bool[W * H];
            for (int view = 0; view < 4; view++)
            {
                Side(m, view * Math.PI / 4, hi, W, H);
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                    {
                        int n = 0;
                        for (int q = 0; q < F; q++)
                            for (int r = 0; r < F; r++)
                                if (hi[(j * F + q) * W + i * F + r]) n++;
                        outv[view * w * h + j * w + i] = n / (float)(F * F);
                    }
            }
            return outv;
        }

        /// <summary>Soft intersection over union of two coverage maps (Σ min / Σ max).</summary>
        public static float SoftIoU(float[] a, float[] b)
        {
            double lo = 0, hi = 0;
            for (int i = 0; i < a.Length; i++)
            {
                lo += Math.Min(a[i], b[i]);
                hi += Math.Max(a[i], b[i]);
            }
            return hi > 0 ? (float)(lo / hi) : 0f;
        }

        /// <summary>
        /// Row solidity of a model's side outline above <paramref name="y0"/> (unit heights), from the bearings 0° and
        /// 90°: in every raster row the filled cells over the span between the first and the last filled cell, weighted
        /// by the span. A round crown is solid (about 1); a star of fronds or a fan of leaves leaves gaps between its
        /// blades (well under 1).
        /// </summary>
        public static float RowSolidity(MeshData m, float y0)
        {
            const int W = 104, H = 96;
            const float ya = -0.05f, yb = 1.15f;
            var hi = new bool[W * H];
            long filled = 0, span = 0;
            for (int view = 0; view < 2; view++)
            {
                Side(m, view * Math.PI / 2, hi, W, H);
                int jStart = (int)Math.Ceiling((y0 - ya) / (yb - ya) * H);
                for (int j = jStart; j < H; j++)
                {
                    int first = -1, last = -1, n = 0;
                    for (int i = 0; i < W; i++)
                        if (hi[j * W + i])
                        {
                            if (first < 0) first = i;
                            last = i;
                            n++;
                        }
                    if (first < 0) continue;
                    filled += n;
                    span += last - first + 1;
                }
            }
            return span > 0 ? (float)filled / span : 0f;
        }

        /// <summary>Orthographic side view at bearing <paramref name="a"/> (radians) of a unit model into a W × H mask
        /// over x ∈ [−0.65, 0.65], y ∈ [−0.05, 1.15].</summary>
        private static void Side(MeshData m, double a, bool[] hi, int W, int H)
        {
            const float x0 = -0.65f, x1 = 0.65f, y0 = -0.05f, y1 = 1.15f;
            Array.Clear(hi, 0, hi.Length);
            float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            var u = new float[3];
            var v = new float[3];
            for (int t = 0; t < m.TriangleCount; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int vi = m.Indices[3 * t + k];
                    u[k] = (m.Positions[3 * vi] * ca + m.Positions[3 * vi + 2] * sa - x0) / (x1 - x0) * W;
                    v[k] = (m.Positions[3 * vi + 1] - y0) / (y1 - y0) * H;
                }
                float d = (v[1] - v[2]) * (u[0] - u[2]) + (u[2] - u[1]) * (v[0] - v[2]);
                if (Math.Abs(d) < 1e-9f) continue;
                int i0 = Math.Max(0, (int)Math.Floor(Math.Min(u[0], Math.Min(u[1], u[2])))), i1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(u[0], Math.Max(u[1], u[2]))));
                int j0 = Math.Max(0, (int)Math.Floor(Math.Min(v[0], Math.Min(v[1], v[2])))), j1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(v[0], Math.Max(v[1], v[2]))));
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        float px = i + 0.5f, py = j + 0.5f;
                        float l0 = ((v[1] - v[2]) * (px - u[2]) + (u[2] - u[1]) * (py - v[2])) / d;
                        float l1 = ((v[2] - v[0]) * (px - u[2]) + (u[0] - u[2]) * (py - v[2])) / d;
                        if (l0 >= 0 && l1 >= 0 && l0 + l1 <= 1) hi[j * W + i] = true;
                    }
            }
        }

        /// <summary>
        /// Depth-tested orthographic view of <paramref name="m"/> looking down at <paramref name="elevationDeg"/> from
        /// the bearing <paramref name="azimuthDeg"/> (90 = straight down), <paramref name="res"/> pixels across its
        /// bounds: the share of the pixels showing <paramref name="among"/> material channels whose nearest triangle's
        /// (average vertex) colour passes <paramref name="test"/>. A marigold bed: the bloom over the foliage and soil.
        /// </summary>
        public static double ViewShare(MeshData m, double azimuthDeg, double elevationDeg, int res, MaterialChannel[] among, Func<int, int, int, bool> test)
        {
            double az = azimuthDeg * Math.PI / 180, el = elevationDeg * Math.PI / 180;
            // View frame: forward f (towards the model), right r, up u.
            double fx = -Math.Sin(az) * Math.Cos(el), fy = -Math.Sin(el), fz = -Math.Cos(az) * Math.Cos(el);
            double rx = Math.Cos(az), rz = -Math.Sin(az);
            double ux = fy * rz, uy = fz * rx - fx * rz, uz = -fy * rx;
            if (elevationDeg >= 89.99)
            {
                ux = Math.Sin(az);
                uy = 0;
                uz = Math.Cos(az);
            }
            int nv = m.VertexCount;
            var sx = new double[nv];
            var sy = new double[nv];
            var sd = new double[nv];
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            for (int v = 0; v < nv; v++)
            {
                double x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                sx[v] = x * rx + z * rz;
                sy[v] = x * ux + y * uy + z * uz;
                sd[v] = x * fx + y * fy + z * fz;
                minX = Math.Min(minX, sx[v]);
                maxX = Math.Max(maxX, sx[v]);
                minY = Math.Min(minY, sy[v]);
                maxY = Math.Max(maxY, sy[v]);
            }
            double scale = res / Math.Max(maxX - minX, maxY - minY);
            int W = (int)Math.Ceiling((maxX - minX) * scale) + 1, H = (int)Math.Ceiling((maxY - minY) * scale) + 1;
            var depth = new double[W * H];
            var tri = new int[W * H];
            for (int i = 0; i < depth.Length; i++)
            {
                depth[i] = double.MaxValue;
                tri[i] = -1;
            }
            for (int t = 0; t < m.TriangleCount; t++)
            {
                int a = m.Indices[3 * t], b = m.Indices[3 * t + 1], c = m.Indices[3 * t + 2];
                double ax = (sx[a] - minX) * scale, ay = (sy[a] - minY) * scale, bx = (sx[b] - minX) * scale, by = (sy[b] - minY) * scale;
                double cx = (sx[c] - minX) * scale, cy = (sy[c] - minY) * scale;
                double d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
                if (Math.Abs(d) < 1e-12) continue;
                int i0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)))), i1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                int j0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)))), j1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        double px = i + 0.5, py = j + 0.5;
                        double l0 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / d;
                        double l1 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / d;
                        double l2 = 1 - l0 - l1;
                        if (l0 < 0 || l1 < 0 || l2 < 0) continue;
                        double z = l0 * sd[a] + l1 * sd[b] + l2 * sd[c];
                        int k = j * W + i;
                        if (z < depth[k])
                        {
                            depth[k] = z;
                            tri[k] = t;
                        }
                    }
            }
            int hit = 0, n = 0;
            for (int k = 0; k < tri.Length; k++)
            {
                int t = tri[k];
                if (t < 0) continue;
                int v0 = m.Indices[3 * t];
                var ch = (MaterialChannel)(byte)Math.Round(m.Uv0[2 * v0]);
                if (Array.IndexOf(among, ch) < 0) continue;
                int r = 0, g = 0, bl = 0;
                for (int q = 0; q < 3; q++)
                {
                    int v = m.Indices[3 * t + q];
                    r += m.Colors[4 * v];
                    g += m.Colors[4 * v + 1];
                    bl += m.Colors[4 * v + 2];
                }
                n++;
                if (test(r / 3, g / 3, bl / 3)) hit++;
            }
            return n > 0 ? (double)hit / n : 0;
        }

        /// <summary>Marigold bloom colours (deep orange, golden yellow, red-orange, lit or shaded): warm, saturated,
        /// red over green and almost no blue. Foliage (green over red), the orange-flecked mound tops (greyer) and
        /// soil do not pass.</summary>
        public static bool IsBloom(int r, int g, int b)
        {
            return r >= 150 && r >= g && b <= 0.2 * r;
        }
    }
}
