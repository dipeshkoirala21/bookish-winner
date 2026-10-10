using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>
    /// A moulding profile for <see cref="SacredDraw.Ring"/>: per point the outward offset from the base outline (metres),
    /// the height, the look of the band that starts at this point and whether the point is a hard edge (a crease
    /// duplicates it so both sides shade flat). Grow-only, so a cached instance stops allocating.
    /// </summary>
    internal sealed class Mould
    {
        public double[] O = new double[16], H = new double[16];
        public ShapeBrush[] B = new ShapeBrush[16];
        public bool[] Hard = new bool[16];
        public int Count;

        public Mould Clear()
        {
            Count = 0;
            return this;
        }

        /// <summary>Add a point; <paramref name="hard"/> makes it a crease.</summary>
        public Mould Add(double offset, double height, in ShapeBrush b, bool hard = true)
        {
            if (Count == O.Length)
            {
                int n = 2 * O.Length;
                Array.Resize(ref O, n);
                Array.Resize(ref H, n);
                Array.Resize(ref B, n);
                Array.Resize(ref Hard, n);
            }
            O[Count] = offset;
            H[Count] = height;
            B[Count] = b;
            Hard[Count] = hard;
            Count++;
            return this;
        }

        /// <summary>Add a point with the previous look.</summary>
        public Mould Add(double offset, double height, bool hard = true)
        {
            return Add(offset, height, B[Count - 1], hard);
        }
    }

    /// <summary>
    /// The emission core of the sacred generators (W2 detail pass): vertices carry UV0 = (<see cref="MaterialChannel"/>,
    /// baked AO) per docs/W2_DETAIL_CONTRACT.md §5; positions and normals go through an <see cref="Affine3"/> (usually a
    /// <see cref="KitFrame"/>: +W through the main door), so local coordinates are (u, v, w) = (across, up, out).
    /// Triangles are wound to agree with their vertex normals, which keeps them right under the mirroring kit frame.
    /// Helpers: flat quads and boxes, surface grids, mouldings swept round a plan outline (plinths, copings, cornices,
    /// terraces), caps and the final AO bake. Allocation-free after warm-up.
    /// </summary>
    internal static class SacredDraw
    {
        [ThreadStatic] private static double[] _mu, _mw, _nu, _nw;
        [ThreadStatic] private static Mould _mould;

        /// <summary>A per-thread moulding scratch (cleared).</summary>
        public static Mould M
        {
            get { return (_mould ?? (_mould = new Mould())).Clear(); }
        }

        public static ShapeBrush Br(uint col, MaterialChannel ch, float ao = 1f)
        {
            return new ShapeBrush(col, ch, ao);
        }

        /// <summary>The kit frame as a transform.</summary>
        public static Affine3 Xf(in KitFrame k)
        {
            return Affine3.FromKitFrame(k);
        }

        /// <summary>A local translation followed by <paramref name="k"/>.</summary>
        public static Affine3 At(in Affine3 k, double u, double v, double w)
        {
            return Affine3.Translation(u, v, w).Then(k);
        }

        /// <summary>A local frame at (u, v, w) whose axes are x = (xu, xv, xw), y = (yu, yv, yw) and z = x × y, then
        /// <paramref name="k"/>. x and y must be orthonormal.</summary>
        public static Affine3 Basis(in Affine3 k, double u, double v, double w, double xu, double xv, double xw, double yu, double yv, double yw)
        {
            double zu = xv * yw - xw * yv, zv = xw * yu - xu * yw, zw = xu * yv - xv * yu;
            return Affine3.FromBasis(xu, xv, xw, yu, yv, yw, zu, zv, zw, u, v, w).Then(k);
        }

        /// <summary>A local frame at (u, v, w) turned about the vertical by <paramref name="rad"/> (x toward z), then
        /// <paramref name="k"/>.</summary>
        public static Affine3 Turn(in Affine3 k, double u, double v, double w, double rad)
        {
            double c = Math.Cos(rad), s = Math.Sin(rad);
            return Basis(k, u, v, w, c, 0, s, 0, 1, 0);
        }

        public static int V(MeshData m, in Affine3 xf, double x, double y, double z, double nx, double ny, double nz, in ShapeBrush b)
        {
            double wx, wy, wz, vx, vy, vz;
            xf.Point(x, y, z, out wx, out wy, out wz);
            xf.Normal(nx, ny, nz, out vx, out vy, out vz);
            double l = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            if (!(l > 1e-12))
            {
                vx = 0;
                vy = 1;
                vz = 0;
                l = 1;
            }
            return m.AddVertex((float)wx, (float)wy, (float)wz, (float)(vx / l), (float)(vy / l), (float)(vz / l), b.Color, (float)b.Channel, b.Ao);
        }

        private static bool SamePos(MeshData m, int a, int b)
        {
            float[] p = m.Positions;
            return p[3 * a] == p[3 * b] && p[3 * a + 1] == p[3 * b + 1] && p[3 * a + 2] == p[3 * b + 2];
        }

        private static double Dot(MeshData m, int a, int b, int c)
        {
            float[] p = m.Positions, n = m.Normals;
            double ux = p[3 * b] - (double)p[3 * a], uy = p[3 * b + 1] - (double)p[3 * a + 1], uz = p[3 * b + 2] - (double)p[3 * a + 2];
            double vx = p[3 * c] - (double)p[3 * a], vy = p[3 * c + 1] - (double)p[3 * a + 1], vz = p[3 * c + 2] - (double)p[3 * a + 2];
            double fx = uy * vz - uz * vy, fy = uz * vx - ux * vz, fz = ux * vy - uy * vx;
            return fx * (n[3 * a] + n[3 * b] + (double)n[3 * c]) + fy * (n[3 * a + 1] + n[3 * b + 1] + (double)n[3 * c + 1]) +
                   fz * (n[3 * a + 2] + n[3 * b + 2] + (double)n[3 * c + 2]);
        }

        /// <summary>Triangle (a, b, c) wound to front its vertex normals; skipped when degenerate.</summary>
        public static void Tri(MeshData m, int a, int b, int c)
        {
            if (SamePos(m, a, b) || SamePos(m, b, c) || SamePos(m, a, c)) return;
            if (Dot(m, a, b, c) >= 0) m.AddTriangle(a, b, c);
            else m.AddTriangle(a, c, b);
        }

        /// <summary>Quad a-b-c-d (around its perimeter) wound to front its vertex normals.</summary>
        public static void Quad(MeshData m, int a, int b, int c, int d)
        {
            double s = Dot(m, a, b, c) + Dot(m, a, c, d);
            bool flip = s < 0;
            if (!(SamePos(m, a, b) || SamePos(m, b, c) || SamePos(m, a, c)))
            {
                if (flip) m.AddTriangle(a, c, b);
                else m.AddTriangle(a, b, c);
            }
            if (!(SamePos(m, a, c) || SamePos(m, c, d) || SamePos(m, a, d)))
            {
                if (flip) m.AddTriangle(a, d, c);
                else m.AddTriangle(a, c, d);
            }
        }

        /// <summary>A flat quad from four local points with one normal.</summary>
        public static void Quad(MeshData m, in Affine3 xf, double u0, double v0, double w0, double u1, double v1, double w1, double u2, double v2,
                                double w2, double u3, double v3, double w3, double nu, double nv, double nw, in ShapeBrush b)
        {
            int a = V(m, xf, u0, v0, w0, nu, nv, nw, b), bb = V(m, xf, u1, v1, w1, nu, nv, nw, b);
            int c = V(m, xf, u2, v2, w2, nu, nv, nw, b), d = V(m, xf, u3, v3, w3, nu, nv, nw, b);
            Quad(m, a, bb, c, d);
        }

        /// <summary>A flat triangle from three local points with one normal.</summary>
        public static void Tri(MeshData m, in Affine3 xf, double u0, double v0, double w0, double u1, double v1, double w1, double u2, double v2,
                               double w2, double nu, double nv, double nw, in ShapeBrush b)
        {
            int a = V(m, xf, u0, v0, w0, nu, nv, nw, b), bb = V(m, xf, u1, v1, w1, nu, nv, nw, b), c = V(m, xf, u2, v2, w2, nu, nv, nw, b);
            Tri(m, a, bb, c);
        }

        /// <summary>A vertical panel in the plane w facing +w, from (u0, v0) to (u1, v1).</summary>
        public static void Panel(MeshData m, in Affine3 xf, double u0, double v0, double u1, double v1, double w, in ShapeBrush b)
        {
            Quad(m, xf, u0, v0, w, u1, v0, w, u1, v1, w, u0, v1, w, 0, 0, 1, b);
        }

        /// <summary>An axis-aligned local box with flat faces.</summary>
        public static void Box(MeshData m, in Affine3 xf, double u0, double u1, double v0, double v1, double w0, double w1, in ShapeBrush b,
                               BoxFaces faces = BoxFaces.All)
        {
            if ((faces & BoxFaces.Front) != 0) Quad(m, xf, u0, v0, w1, u1, v0, w1, u1, v1, w1, u0, v1, w1, 0, 0, 1, b);
            if ((faces & BoxFaces.Back) != 0) Quad(m, xf, u0, v0, w0, u1, v0, w0, u1, v1, w0, u0, v1, w0, 0, 0, -1, b);
            if ((faces & BoxFaces.Right) != 0) Quad(m, xf, u1, v0, w0, u1, v0, w1, u1, v1, w1, u1, v1, w0, 1, 0, 0, b);
            if ((faces & BoxFaces.Left) != 0) Quad(m, xf, u0, v0, w0, u0, v0, w1, u0, v1, w1, u0, v1, w0, -1, 0, 0, b);
            if ((faces & BoxFaces.Top) != 0) Quad(m, xf, u0, v1, w0, u1, v1, w0, u1, v1, w1, u0, v1, w1, 0, 1, 0, b);
            if ((faces & BoxFaces.Bottom) != 0) Quad(m, xf, u0, v0, w0, u1, v0, w0, u1, v0, w1, u0, v0, w1, 0, -1, 0, b);
        }

        /// <summary>
        /// Connect a rows × cols block of vertices (row-major from <paramref name="first"/>) with quads wound by a vote of
        /// all quads against their normals; <paramref name="rowSeam"/>[r] skips the band between rows r and r + 1.
        /// </summary>
        public static void Grid(MeshData m, int first, int rows, int cols, bool wrapCols, bool[] rowSeam)
        {
            int qc = wrapCols ? cols : cols - 1;
            double vote = 0;
            for (int r = 0; r + 1 < rows; r++)
            {
                if (rowSeam != null && rowSeam[r]) continue;
                for (int c = 0; c < qc; c++)
                {
                    int c1 = (c + 1) % cols;
                    int a = first + r * cols + c, b = first + r * cols + c1, cc = first + (r + 1) * cols + c1, d = first + (r + 1) * cols + c;
                    double s = Dot(m, a, b, cc) + Dot(m, a, cc, d);
                    vote += s > 0 ? 1 : s < 0 ? -1 : 0;
                }
            }
            bool flip = vote < 0;
            for (int r = 0; r + 1 < rows; r++)
            {
                if (rowSeam != null && rowSeam[r]) continue;
                for (int c = 0; c < qc; c++)
                {
                    int c1 = (c + 1) % cols;
                    int a = first + r * cols + c, b = first + r * cols + c1, cc = first + (r + 1) * cols + c1, d = first + (r + 1) * cols + c;
                    if (!(SamePos(m, a, b) || SamePos(m, b, cc) || SamePos(m, a, cc)))
                    {
                        if (flip) m.AddTriangle(a, cc, b);
                        else m.AddTriangle(a, b, cc);
                    }
                    if (!(SamePos(m, a, cc) || SamePos(m, cc, d) || SamePos(m, a, d)))
                    {
                        if (flip) m.AddTriangle(a, d, cc);
                        else m.AddTriangle(a, cc, d);
                    }
                }
            }
        }

        private static void Scratch(int n)
        {
            if (_mu == null || _mu.Length < n)
            {
                int c = Math.Max(64, n);
                _mu = new double[c];
                _mw = new double[c];
                _nu = new double[c];
                _nw = new double[c];
            }
        }

        /// <summary>Signed area of a local (u, w) polygon (positive counter-clockwise with u right and w up).</summary>
        public static double Area(double[] pu, double[] pw, int n)
        {
            double a = 0;
            for (int i = 0, j = n - 1; i < n; j = i++) a += pu[j] * pw[i] - pu[i] * pw[j];
            return 0.5 * a;
        }

        /// <summary>A rectangle outline of half sizes (hu, hw) centred on (cu, cw), counter-clockwise; returns 4.</summary>
        public static int Rect(double cu, double cw, double hu, double hw, double[] pu, double[] pw)
        {
            pu[0] = cu + hu;
            pw[0] = cw - hw;
            pu[1] = cu + hu;
            pw[1] = cw + hw;
            pu[2] = cu - hu;
            pw[2] = cw + hw;
            pu[3] = cu - hu;
            pw[3] = cw - hw;
            return 4;
        }

        /// <summary>
        /// Sweep a moulding profile round a closed plan outline (local u, w; either orientation; convex or with re-entrant
        /// corners such as the 20-cornered stupa terraces): each profile point offsets the outline outward by its
        /// <see cref="Mould.O"/> (mitred at corners) at its height. Faces are flat per outline edge and shade smoothly
        /// along the profile except at hard points. Returns the triangles.
        /// </summary>
        public static int Ring(MeshData m, in Affine3 xf, double[] pu, double[] pw, int n, Mould p)
        {
            if (n < 3 || p.Count < 2) return 0;
            int t0 = m.TriangleCount;
            Scratch(n);
            double sign = Area(pu, pw, n) >= 0 ? 1 : -1;
            // Outward edge normals and mitre vectors.
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double du = pu[j] - pu[i], dw = pw[j] - pw[i], l = Math.Sqrt(du * du + dw * dw);
                if (l < 1e-9) l = 1e-9;
                _nu[i] = sign * dw / l;
                _nw[i] = -sign * du / l;
            }
            for (int i = 0; i < n; i++)
            {
                int h = (i + n - 1) % n;
                double mu = _nu[h] + _nu[i], mw = _nw[h] + _nw[i], l = Math.Sqrt(mu * mu + mw * mw);
                if (l < 1e-9)
                {
                    mu = _nu[i];
                    mw = _nw[i];
                    l = 1;
                }
                mu /= l;
                mw /= l;
                double c = mu * _nu[i] + mw * _nw[i];
                double s = 1.0 / Math.Max(0.35, c);
                _mu[i] = mu * s;
                _mw[i] = mw * s;
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double eu = _nu[i], ew = _nw[i];
                for (int k = 0; k + 1 < p.Count; k++)
                {
                    double o0 = p.O[k], h0 = p.H[k], o1 = p.O[k + 1], h1 = p.H[k + 1];
                    if (Math.Abs(o0 - o1) < 1e-9 && Math.Abs(h0 - h1) < 1e-9) continue;
                    double nr0, nh0, nr1, nh1;
                    Normal(p, k, true, out nr0, out nh0);
                    Normal(p, k + 1, false, out nr1, out nh1);
                    ShapeBrush b = p.B[k];
                    int a = V(m, xf, pu[i] + o0 * _mu[i], h0, pw[i] + o0 * _mw[i], eu * nr0, nh0, ew * nr0, b);
                    int bb = V(m, xf, pu[j] + o0 * _mu[j], h0, pw[j] + o0 * _mw[j], eu * nr0, nh0, ew * nr0, b);
                    int c = V(m, xf, pu[j] + o1 * _mu[j], h1, pw[j] + o1 * _mw[j], eu * nr1, nh1, ew * nr1, b);
                    int d = V(m, xf, pu[i] + o1 * _mu[i], h1, pw[i] + o1 * _mw[i], eu * nr1, nh1, ew * nr1, b);
                    Quad(m, a, bb, c, d);
                }
            }
            return m.TriangleCount - t0;
        }

        /// <summary>Profile normal (outward, up) at point k for the band starting (<paramref name="start"/>) or ending there.</summary>
        private static void Normal(Mould p, int k, bool start, out double nr, out double nh)
        {
            double tr = 0, th = 0;
            // The band's own direction.
            int a = start ? k : k - 1, b = start ? k + 1 : k;
            double dr = p.O[b] - p.O[a], dh = p.H[b] - p.H[a], l = Math.Sqrt(dr * dr + dh * dh);
            if (l > 1e-12)
            {
                tr += dr / l;
                th += dh / l;
            }
            // Smooth points blend with the neighbouring band.
            if (!p.Hard[k])
            {
                int a2 = start ? k - 1 : k, b2 = start ? k : k + 1;
                if (a2 >= 0 && b2 < p.Count)
                {
                    double dr2 = p.O[b2] - p.O[a2], dh2 = p.H[b2] - p.H[a2], l2 = Math.Sqrt(dr2 * dr2 + dh2 * dh2);
                    if (l2 > 1e-12)
                    {
                        tr += dr2 / l2;
                        th += dh2 / l2;
                    }
                }
            }
            double tl = Math.Sqrt(tr * tr + th * th);
            if (tl < 1e-12)
            {
                nr = 1;
                nh = 0;
                return;
            }
            // The profile runs bottom to top with the outside on the right: normal = tangent turned clockwise.
            nr = th / tl;
            nh = -tr / tl;
        }

        /// <summary>A flat cap over a star-shaped outline at height v (fan from the centroid), facing up or down.</summary>
        public static int Cap(MeshData m, in Affine3 xf, double[] pu, double[] pw, int n, double offset, double v, bool up, in ShapeBrush b)
        {
            if (n < 3) return 0;
            int t0 = m.TriangleCount;
            double cu = 0, cw = 0;
            for (int i = 0; i < n; i++)
            {
                cu += pu[i];
                cw += pw[i];
            }
            cu /= n;
            cw /= n;
            double ny = up ? 1 : -1;
            if (Math.Abs(offset) > 1e-9)
            {
                // Offset outline: reuse the mitres of the last Ring call is unsafe; recompute simply by scaling about the centroid.
                Scratch(n);
            }
            int c = V(m, xf, cu, v, cw, 0, ny, 0, b);
            int first = m.VertexCount;
            for (int i = 0; i < n; i++)
            {
                double ou = pu[i], ow = pw[i];
                if (Math.Abs(offset) > 1e-9)
                {
                    double du = ou - cu, dw = ow - cw, l = Math.Sqrt(du * du + dw * dw);
                    if (l > 1e-9)
                    {
                        ou += du / l * offset;
                        ow += dw / l * offset;
                    }
                }
                V(m, xf, ou, v, ow, 0, ny, 0, b);
            }
            for (int i = 0; i < n; i++) Tri(m, c, first + i, first + (i + 1) % n);
            return m.TriangleCount - t0;
        }

        /// <summary>A rectangular moulded block (plinth level, coping, cornice band): the profile swept round a
        /// rectangle of half sizes (hu, hw) centred on (cu, cw), with an optional top cap at the last profile point.</summary>
        public static int RectRing(MeshData m, in Affine3 xf, double cu, double cw, double hu, double hw, Mould p, bool capTop, in ShapeBrush top)
        {
            double[] pu = ShapeScratch.U, pw = ShapeScratch.W;
            int n = Rect(cu, cw, hu, hw, pu, pw);
            int t = Ring(m, xf, pu, pw, n, p);
            if (capTop)
            {
                double o = p.O[p.Count - 1], h = p.H[p.Count - 1];
                double cu0 = cu, cw0 = cw;
                t += CapRect(m, xf, cu0, cw0, hu + o, hw + o, h, true, top);
            }
            return t;
        }

        /// <summary>A flat rectangle at height v facing up or down.</summary>
        public static int CapRect(MeshData m, in Affine3 xf, double cu, double cw, double hu, double hw, double v, bool up, in ShapeBrush b)
        {
            double ny = up ? 1 : -1;
            Quad(m, xf, cu - hu, v, cw - hw, cu + hu, v, cw - hw, cu + hu, v, cw + hw, cu - hu, v, cw + hw, 0, ny, 0, b);
            return 2;
        }

        /// <summary>Bake the cheap AO of the shapes library into the vertices added since (v0, i0), standing on
        /// <paramref name="groundY"/>; it multiplies the brush AO already written.</summary>
        public static void Bake(MeshData m, int v0, int i0, double groundY)
        {
            if (m.VertexCount <= v0) return;
            AoSettings s = ShapeAo.Defaults(groundY);
            s.GroundFade = 0.6;
            s.GroundAo = 0.72f;
            s.Underside = 0.3f;
            s.Concavity = 0.5f;
            ShapeAo.Bake(m, v0, m.VertexCount - v0, i0, m.IndexCount - i0, s);
        }
    }

    /// <summary>Per-thread outline scratch for the drawing helpers.</summary>
    internal static class ShapeScratch
    {
        [ThreadStatic] private static double[] _u, _w, _u2, _w2;

        public static double[] U
        {
            get { return _u ?? (_u = new double[64]); }
        }

        public static double[] W
        {
            get { return _w ?? (_w = new double[64]); }
        }

        public static double[] U2
        {
            get { return _u2 ?? (_u2 = new double[64]); }
        }

        public static double[] W2
        {
            get { return _w2 ?? (_w2 = new double[64]); }
        }
    }
}
