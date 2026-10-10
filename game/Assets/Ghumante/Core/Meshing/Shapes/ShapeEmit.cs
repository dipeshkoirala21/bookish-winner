using System;

namespace Ghumante.Core.Meshing.Shapes
{
    /// <summary>A profile expanded into shading samples: crease points split in two (one normal per side) with a
    /// seam between them, duplicate points dropped. Internal scratch of the emitters.</summary>
    internal sealed class Expanded
    {
        public double[] X = new double[64], Y = new double[64], NX = new double[64], NY = new double[64];
        public bool[] Seam = new bool[64];
        public int Count;

        public void Clear()
        {
            Count = 0;
        }

        public void Add(double x, double y, double nx, double ny, bool seamAfter)
        {
            if (Count == X.Length)
            {
                int c = Count * 2;
                Array.Resize(ref X, c);
                Array.Resize(ref Y, c);
                Array.Resize(ref NX, c);
                Array.Resize(ref NY, c);
                Array.Resize(ref Seam, c);
            }
            X[Count] = x;
            Y[Count] = y;
            NX[Count] = nx;
            NY[Count] = ny;
            Seam[Count] = seamAfter;
            Count++;
        }
    }

    /// <summary>A horizontal ring path for revolve-style emitters: point, offset direction (radial unit vector for a
    /// lathe, miter vector for an extrude) and shading normal per sample.</summary>
    internal sealed class RingPath
    {
        public double[] X = new double[64], Z = new double[64], MX = new double[64], MZ = new double[64];
        public double[] NX = new double[64], NZ = new double[64];
        public bool[] Seam = new bool[64];
        public int Count;

        public void Clear()
        {
            Count = 0;
        }

        public void Add(double x, double z, double mx, double mz, double nx, double nz, bool seamAfter)
        {
            if (Count == X.Length)
            {
                int c = Count * 2;
                Array.Resize(ref X, c);
                Array.Resize(ref Z, c);
                Array.Resize(ref MX, c);
                Array.Resize(ref MZ, c);
                Array.Resize(ref NX, c);
                Array.Resize(ref NZ, c);
                Array.Resize(ref Seam, c);
            }
            X[Count] = x;
            Z[Count] = z;
            MX[Count] = mx;
            MZ[Count] = mz;
            NX[Count] = nx;
            NZ[Count] = nz;
            Seam[Count] = seamAfter;
            Count++;
        }
    }

    /// <summary>Shared low-level writers: transformed vertices with the material channel and AO in UV0, grids of
    /// quads with automatic (majority) orientation, oriented triangles and polygon caps. Per-thread scratch.</summary>
    internal static class ShapeEmit
    {
        [ThreadStatic] private static Expanded s_ea, s_eb;
        [ThreadStatic] private static RingPath s_ring;
        [ThreadStatic] private static double[] s_px, s_pz;
        [ThreadStatic] private static int[] s_tris, s_next, s_prev, s_map;

        [ThreadStatic] private static double[] s_tiny;

        /// <summary>A per-thread scratch of 32 doubles for small fixed-size working sets.</summary>
        public static double[] Tiny
        {
            get { return s_tiny ?? (s_tiny = new double[32]); }
        }

        public static Expanded ExpA
        {
            get { return s_ea ?? (s_ea = new Expanded()); }
        }

        public static Expanded ExpB
        {
            get { return s_eb ?? (s_eb = new Expanded()); }
        }

        public static RingPath Ring
        {
            get { return s_ring ?? (s_ring = new RingPath()); }
        }

        [ThreadStatic] private static double[][] s_slots;

        /// <summary>Per-thread double scratch number <paramref name="slot"/> (0..15) of at least n entries.</summary>
        public static double[] Scratch(int slot, int n)
        {
            if (s_slots == null) s_slots = new double[16][];
            return Ensure(ref s_slots[slot], n);
        }

        private static double[] Ensure(ref double[] a, int n)
        {
            if (a == null || a.Length < n) a = new double[Math.Max(n, a == null ? 64 : a.Length * 2)];
            return a;
        }

        private static int[] Ensure(ref int[] a, int n)
        {
            if (a == null || a.Length < n) a = new int[Math.Max(n, a == null ? 64 : a.Length * 2)];
            return a;
        }

        /// <summary>Write one vertex: position and normal through <paramref name="xf"/>, colour and UV0 from the brush.</summary>
        public static int Vertex(MeshData m, in Affine3 xf, in ShapeBrush b, double x, double y, double z, double nx, double ny, double nz)
        {
            double wx, wy, wz, vx, vy, vz;
            xf.Point(x, y, z, out wx, out wy, out wz);
            xf.Normal(nx, ny, nz, out vx, out vy, out vz);
            return m.AddVertex((float)wx, (float)wy, (float)wz, (float)vx, (float)vy, (float)vz, b.Color, (float)b.Channel, b.Ao);
        }

        /// <summary>Normalise (nx, ny, nz) in place; falls back to +Y for a zero vector.</summary>
        public static void Normalize(ref double nx, ref double ny, ref double nz)
        {
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l < 1e-300)
            {
                nx = 0;
                ny = 1;
                nz = 0;
                return;
            }
            nx /= l;
            ny /= l;
            nz /= l;
        }

        public static bool SamePos(MeshData m, int a, int b)
        {
            float[] p = m.Positions;
            return p[3 * a] == p[3 * b] && p[3 * a + 1] == p[3 * b + 1] && p[3 * a + 2] == p[3 * b + 2];
        }

        /// <summary>Add triangle (a, b, c), or (a, c, b) when <paramref name="flip"/>; skipped when two corners coincide.</summary>
        public static void Tri(MeshData m, int a, int b, int c, bool flip)
        {
            if (SamePos(m, a, b) || SamePos(m, b, c) || SamePos(m, a, c)) return;
            if (flip) m.AddTriangle(a, c, b);
            else m.AddTriangle(a, b, c);
        }

        /// <summary>Add triangle (a, b, c) wound so its front faces the side of its vertex normals.</summary>
        public static void TriOriented(MeshData m, int a, int b, int c)
        {
            Tri(m, a, b, c, FacetDot(m, a, b, c) < 0);
        }

        /// <summary>dot(cross(b - a, c - a), na + nb + nc): positive when (a, b, c) fronts the vertex normals.</summary>
        public static double FacetDot(MeshData m, int a, int b, int c)
        {
            float[] p = m.Positions, n = m.Normals;
            double ux = p[3 * b] - (double)p[3 * a], uy = p[3 * b + 1] - (double)p[3 * a + 1], uz = p[3 * b + 2] - (double)p[3 * a + 2];
            double vx = p[3 * c] - (double)p[3 * a], vy = p[3 * c + 1] - (double)p[3 * a + 1], vz = p[3 * c + 2] - (double)p[3 * a + 2];
            double fx = uy * vz - uz * vy, fy = uz * vx - ux * vz, fz = ux * vy - uy * vx;
            double sx = n[3 * a] + n[3 * b] + (double)n[3 * c], sy = n[3 * a + 1] + n[3 * b + 1] + (double)n[3 * c + 1];
            double sz = n[3 * a + 2] + n[3 * b + 2] + (double)n[3 * c + 2];
            return fx * sx + fy * sy + fz * sz;
        }

        /// <summary>
        /// Connect a rows × cols block of vertices (row-major from <paramref name="first"/>) with quads. Columns wrap
        /// when <paramref name="wrapCols"/>, rows when <paramref name="wrapRows"/>; <paramref name="colSeam"/>[c]
        /// (or <paramref name="rowSeam"/>[r]) suppresses the quads between column c and c + 1 (row r and r + 1).
        /// The winding is chosen once for the whole grid by a vote of every quad against its vertex normals, so it
        /// is consistent and correct under any transform; quads collapsed at poles become triangles.
        /// </summary>
        public static void Grid(MeshData m, int first, int rows, int cols, bool wrapCols, bool wrapRows, bool[] colSeam, bool[] rowSeam)
        {
            int qr = wrapRows ? rows : rows - 1, qc = wrapCols ? cols : cols - 1;
            if (qr <= 0 || qc <= 0) return;
            double vote = 0;
            for (int r = 0; r < qr; r++)
            {
                if (rowSeam != null && rowSeam[r]) continue;
                int r1 = (r + 1) % rows;
                for (int c = 0; c < qc; c++)
                {
                    if (colSeam != null && colSeam[c]) continue;
                    int c1 = (c + 1) % cols;
                    int a = first + r * cols + c, b = first + r * cols + c1, cc = first + r1 * cols + c1, d = first + r1 * cols + c;
                    double s = FacetDot(m, a, b, cc) + FacetDot(m, a, cc, d);
                    vote += s > 0 ? 1 : s < 0 ? -1 : 0;
                }
            }
            bool flip = vote < 0;
            m.Reserve(0, qr * qc * 6);
            for (int r = 0; r < qr; r++)
            {
                if (rowSeam != null && rowSeam[r]) continue;
                int r1 = (r + 1) % rows;
                for (int c = 0; c < qc; c++)
                {
                    if (colSeam != null && colSeam[c]) continue;
                    int c1 = (c + 1) % cols;
                    int a = first + r * cols + c, b = first + r * cols + c1, cc = first + r1 * cols + c1, d = first + r1 * cols + c;
                    Tri(m, a, b, cc, flip);
                    Tri(m, a, cc, d, flip);
                }
            }
        }

        /// <summary>
        /// Expand a profile into shading samples: creased points (and the ends of an open profile) take the normals
        /// of their edges, smooth points the explicit normal or the mean of their edge normals; a crease becomes two
        /// samples with a seam between them. Returns the expanded count.
        /// </summary>
        public static int Expand(Profile2 p, Expanded e)
        {
            e.Clear();
            int n = p.Count;
            if (n < 2) return 0;
            double side = p.Closed && p.SignedArea() < 0 ? -1 : 1;
            for (int i = 0; i < n; i++)
            {
                double x = p.X[i], y = p.Y[i];
                // Previous and next distinct points.
                int ip = -1, inx = -1;
                if (p.Closed || i > 0) ip = Distinct(p, i, -1);
                if (p.Closed || i < n - 1) inx = Distinct(p, i, +1);
                if (!p.Closed && i > 0 && ip < 0) continue; // duplicate of the previous point on an open profile
                if (i > 0 && SameXY(p, i, i - 1)) continue;
                if (p.Closed && i == n - 1 && SameXY(p, i, 0)) continue;
                double pnx = 0, pny = 0, nnx = 0, nny = 0;
                bool hasP = ip >= 0 && EdgeNormal(p, ip, i, side, out pnx, out pny);
                bool hasN = inx >= 0 && EdgeNormal(p, i, inx, side, out nnx, out nny);
                if (!hasP && !hasN) continue;
                bool crease = p.Crease[i] && hasP && hasN;
                if (crease)
                {
                    e.Add(x, y, pnx, pny, true);
                    e.Add(x, y, nnx, nny, false);
                    continue;
                }
                double nx, ny;
                if (!double.IsNaN(p.NX[i]))
                {
                    nx = p.NX[i];
                    ny = p.NY[i];
                }
                else if (hasP && hasN)
                {
                    nx = pnx + nnx;
                    ny = pny + nny;
                    double l = Math.Sqrt(nx * nx + ny * ny);
                    if (l < 1e-9)
                    {
                        nx = nnx;
                        ny = nny;
                    }
                    else
                    {
                        nx /= l;
                        ny /= l;
                    }
                }
                else if (hasP)
                {
                    nx = pnx;
                    ny = pny;
                }
                else
                {
                    nx = nnx;
                    ny = nny;
                }
                e.Add(x, y, nx, ny, false);
            }
            return e.Count;
        }

        private static bool SameXY(Profile2 p, int i, int j)
        {
            double dx = p.X[i] - p.X[j], dy = p.Y[i] - p.Y[j];
            return dx * dx + dy * dy < 1e-18;
        }

        private static int Distinct(Profile2 p, int i, int dir)
        {
            int n = p.Count;
            for (int k = 1; k < n; k++)
            {
                int j = i + dir * k;
                if (p.Closed) j = ((j % n) + n) % n;
                else if (j < 0 || j >= n) return -1;
                if (!SameXY(p, i, j)) return j;
            }
            return -1;
        }

        private static bool EdgeNormal(Profile2 p, int a, int b, double side, out double nx, out double ny)
        {
            double dx = p.X[b] - p.X[a], dy = p.Y[b] - p.Y[a];
            double l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-12)
            {
                nx = ny = 0;
                return false;
            }
            // Right of travel.
            nx = side * dy / l;
            ny = side * -dx / l;
            return true;
        }

        /// <summary>
        /// Triangulate the distinct points of a profile (taken as a closed polygon) and write the triangles as
        /// indices into the deduplicated point list returned in <paramref name="pointIndex"/> (profile indices).
        /// Returns the triangle count; <paramref name="tris"/> holds 3 entries per triangle.
        /// </summary>
        public static int TriangulateProfile(Profile2 p, out int[] tris, out int[] pointIndex, out int pointCount)
        {
            int n = p.Count;
            pointIndex = Ensure(ref s_map, n + 2);
            double[] x = Ensure(ref s_px, n + 2), z = Ensure(ref s_pz, n + 2);
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                if (k > 0 && Math.Abs(p.X[i] - x[k - 1]) < 1e-9 && Math.Abs(p.Y[i] - z[k - 1]) < 1e-9) continue;
                x[k] = p.X[i];
                z[k] = p.Y[i];
                pointIndex[k] = i;
                k++;
            }
            if (k > 1 && Math.Abs(x[0] - x[k - 1]) < 1e-9 && Math.Abs(z[0] - z[k - 1]) < 1e-9) k--;
            pointCount = k;
            tris = Ensure(ref s_tris, 3 * Math.Max(1, k));
            if (k < 3) return 0;
            double area = Polygon.SignedArea(x, z, k);
            if (area < 0)
            {
                for (int i = 0, j = k - 1; i < j; i++, j--)
                {
                    double t = x[i];
                    x[i] = x[j];
                    x[j] = t;
                    t = z[i];
                    z[i] = z[j];
                    z[j] = t;
                    int ti = pointIndex[i];
                    pointIndex[i] = pointIndex[j];
                    pointIndex[j] = ti;
                }
            }
            int[] next = Ensure(ref s_next, k), prev = Ensure(ref s_prev, k);
            return Polygon.Triangulate(x, z, k, tris, next, prev, Polygon.IsConvex(x, z, k, 0.5));
        }

        /// <summary>Triangulate a planar polygon given by parallel arrays (any orientation); same outputs as
        /// <see cref="TriangulateProfile"/> with <paramref name="pointIndex"/> mapping into the input arrays.</summary>
        public static int TriangulateArrays(double[] px, double[] pz, int n, out int[] tris, out int[] pointIndex, out int pointCount)
        {
            pointIndex = Ensure(ref s_map, n + 2);
            double[] x = Ensure(ref s_px, n + 2), z = Ensure(ref s_pz, n + 2);
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                if (k > 0 && Math.Abs(px[i] - x[k - 1]) < 1e-9 && Math.Abs(pz[i] - z[k - 1]) < 1e-9) continue;
                x[k] = px[i];
                z[k] = pz[i];
                pointIndex[k] = i;
                k++;
            }
            if (k > 1 && Math.Abs(x[0] - x[k - 1]) < 1e-9 && Math.Abs(z[0] - z[k - 1]) < 1e-9) k--;
            pointCount = k;
            tris = Ensure(ref s_tris, 3 * Math.Max(1, k));
            if (k < 3) return 0;
            if (Polygon.SignedArea(x, z, k) < 0)
            {
                for (int i = 0, j = k - 1; i < j; i++, j--)
                {
                    double t = x[i];
                    x[i] = x[j];
                    x[j] = t;
                    t = z[i];
                    z[i] = z[j];
                    z[j] = t;
                    int ti = pointIndex[i];
                    pointIndex[i] = pointIndex[j];
                    pointIndex[j] = ti;
                }
            }
            int[] next = Ensure(ref s_next, k), prev = Ensure(ref s_prev, k);
            return Polygon.Triangulate(x, z, k, tris, next, prev, Polygon.IsConvex(x, z, k, 0.5));
        }
    }
}
