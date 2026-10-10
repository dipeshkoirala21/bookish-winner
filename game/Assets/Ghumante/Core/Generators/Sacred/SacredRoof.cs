using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>
    /// One hipped roof tier of a Newar pagoda (ref_temples 1.3) in kit-local metres: the eave ring (half sizes and the
    /// height of the eave edge), the ring where the slope meets the next storey (or closes on the apex block), the wall
    /// the soffit returns to, the corner lift and slope sag, the fascia, the finish (tile courses or gilt standing seams)
    /// and the looks.
    /// </summary>
    internal struct RoofSpec
    {
        public double EaveU, EaveW, EaveV;
        public double TopU, TopW, TopV;
        public double WallU, WallW, WallTopV;
        public double Lift, Sag, FasciaH;
        public double CoursePitch;
        public bool Gilt, Fringe, Horns;
        public double FringeH;

        /// <summary>Width of one fringe scallop at LOD0 (0 = 0.7 m).</summary>
        public double FringePitch;

        /// <summary>Road corridors the eave bells and fringe keep their overhead clearance over (null = none), and the
        /// frame's ground height they stand on.</summary>
        public SacredClearance Clear;

        public double GroundY;
        public ShapeBrush Roof, Ridge, Fascia, Soffit, Horn;
    }

    internal static partial class SacredParts
    {
        private static readonly double[] Cols0 = { -1, -0.82, -0.62, -0.45, 0.45, 0.62, 0.82, 1 };
        private static readonly double[] Cols1 = { -1, -0.72, -0.45, 0.45, 0.72, 1 };
        private static readonly double[] Cols2 = { -1, -0.55, 0.55, 1 };
        private static readonly double[] SoffitCols = { -1, -0.6, 0.6, 1 };

        [ThreadStatic] private static MeshData _proto;
        [ThreadStatic] private static Profile2 _prof;
        [ThreadStatic] private static Path3 _path;
        [ThreadStatic] private static bool[] _seams;

        private static MeshData Proto
        {
            get
            {
                if (_proto == null) _proto = new MeshData(256, 768);
                _proto.Clear();
                return _proto;
            }
        }

        private static Profile2 Prof
        {
            get { return (_prof ?? (_prof = new Profile2(48))).Clear(false); }
        }

        private static Path3 PathS
        {
            get { return (_path ?? (_path = new Path3(32))).Clear(); }
        }

        private static bool[] Seams(int n)
        {
            if (_seams == null || _seams.Length < n) _seams = new bool[Math.Max(64, n)];
            Array.Clear(_seams, 0, n);
            return _seams;
        }

        private static double Sq(double x)
        {
            return x * x;
        }

        /// <summary>Outward unit direction of a side in local (u, w): 0 = +w (door), 1 = −u, 2 = −w, 3 = +u.</summary>
        public static void Out(int side, out double du, out double dw)
        {
            du = side == 1 ? -1 : side == 3 ? 1 : 0;
            dw = side == 0 ? 1 : side == 2 ? -1 : 0;
        }

        /// <summary>Local point on side <paramref name="side"/> of a rectangle of half sizes (hu, hw): a in [−1, 1] runs
        /// along the side (the corners of neighbouring sides coincide).</summary>
        public static void OnRect(int side, double a, double hu, double hw, out double u, out double w)
        {
            switch (side)
            {
                case 0:
                    u = a * hu;
                    w = hw;
                    break;
                case 1:
                    u = -hu;
                    w = a * hw;
                    break;
                case 2:
                    u = -a * hu;
                    w = -hw;
                    break;
                default:
                    u = hu;
                    w = -a * hw;
                    break;
            }
        }

        /// <summary>The roof surface at (side, a, t): t = 0 the eave edge, 1 the top ring; corners lifted, slope sagging.</summary>
        public static void RoofPoint(in RoofSpec r, int side, double a, double t, out double u, out double v, out double w)
        {
            double hu = r.EaveU + (r.TopU - r.EaveU) * t, hw = r.EaveW + (r.TopW - r.EaveW) * t;
            OnRect(side, a, hu, hw, out u, out w);
            double aa = Math.Abs(a), f = aa > 0.45 ? Sq((aa - 0.45) / 0.55) : 0;
            v = r.EaveV + (r.TopV - r.EaveV) * t + r.Lift * f * Sq(1 - t) - r.Sag * Math.Sin(Math.PI * t);
        }

        /// <summary>Unit normal of the roof surface (up and outward).</summary>
        public static void RoofNormal(in RoofSpec r, int side, double a, double t, out double nu, out double nv, out double nw)
        {
            const double e = 1e-3;
            double a0 = Math.Max(-1, a - e), a1 = Math.Min(1, a + e), t0 = Math.Max(0, t - e), t1 = Math.Min(1, t + e);
            double u0, v0, w0, u1, v1, w1, u2, v2, w2, u3, v3, w3;
            RoofPoint(r, side, a0, t, out u0, out v0, out w0);
            RoofPoint(r, side, a1, t, out u1, out v1, out w1);
            RoofPoint(r, side, a, t0, out u2, out v2, out w2);
            RoofPoint(r, side, a, t1, out u3, out v3, out w3);
            double au = u1 - u0, av = v1 - v0, aw = w1 - w0, tu = u3 - u2, tv = v3 - v2, tw = w3 - w2;
            nu = av * tw - aw * tv;
            nv = aw * tu - au * tw;
            nw = au * tv - av * tu;
            double ou, ow;
            Out(side, out ou, out ow);
            if (nu * ou + nv + nw * ow < 0)
            {
                nu = -nu;
                nv = -nv;
                nw = -nw;
            }
            double l = Math.Sqrt(nu * nu + nv * nv + nw * nw);
            if (l < 1e-12)
            {
                nu = 0;
                nv = 1;
                nw = 0;
                return;
            }
            nu /= l;
            nv /= l;
            nw /= l;
        }

        /// <summary>
        /// Draw a roof tier: the slope (tile courses as stepped rows at LOD0, gilt roofs with standing seams), the fascia,
        /// the soffit with purlin steps back to the wall, rounded hip rolls with upturned corner horns and an optional red
        /// cloth fringe (Kathmandu Durbar Square temples). Returns the triangles.
        /// </summary>
        public static int Roof(MeshData m, in Affine3 k, in RoofSpec r, int lod)
        {
            int t0 = m.TriangleCount;
            double[] cols = lod <= 0 ? (Math.Max(r.EaveU, r.EaveW) >= 5.5 ? Cols0 : Cols1) : lod == 1 ? Cols1 : Cols2;
            int nc = cols.Length;
            double slopeLen = Math.Sqrt(Sq(r.EaveW - r.TopW) + Sq(r.TopV - r.EaveV));
            int courses = lod == 0 && !r.Gilt && r.CoursePitch > 0 ? Math.Max(2, Math.Min(9, (int)Math.Round(slopeLen / r.CoursePitch))) : 0;
            const double Lip = 0.055;
            for (int s = 0; s < 4; s++)
            {
                // 1. The slope.
                int first = m.VertexCount, rows = 0;
                bool[] seam = Seams(64);
                if (courses > 0)
                {
                    for (int q = 0; q < courses; q++)
                    {
                        double ta = (double)q / courses, tb = (double)(q + 1) / courses;
                        RoofRow(m, k, r, s, cols, ta, Lip, 0, ref rows);
                        RoofRow(m, k, r, s, cols, tb, 0, 0, ref rows);
                        if (q + 1 < courses)
                        {
                            seam[rows - 1] = true;
                            RoofRow(m, k, r, s, cols, tb, 0, 1, ref rows);
                            RoofRow(m, k, r, s, cols, tb, Lip, 1, ref rows);
                            seam[rows - 1] = true;
                        }
                    }
                }
                else
                {
                    int bands = lod <= 0 ? 3 : lod == 1 ? 2 : 1;
                    for (int q = 0; q <= bands; q++) RoofRow(m, k, r, s, cols, (double)q / bands, 0, 0, ref rows);
                }
                SacredDraw.Grid(m, first, rows, nc, false, seam);
                if (r.Gilt && lod == 0) Seams(m, k, r, s);

                // 2. Fascia: a vertical board under the eave edge.
                Out(s, out double ou, out double ow);
                int ff = m.VertexCount;
                for (int row = 0; row < 2; row++)
                {
                    for (int c = 0; c < nc; c++)
                    {
                        double u, v, w;
                        RoofPoint(r, s, cols[c], 0, out u, out v, out w);
                        SacredDraw.V(m, k, u + 0.01 * ou, v - row * r.FasciaH, w + 0.01 * ow, ou, -0.08, ow, row == 0 ? r.Fascia : Looks.Ao(r.Fascia, 0.8f));
                    }
                }
                SacredDraw.Grid(m, ff, 2, nc, false, null);

                // 3. Soffit back to the wall, with two purlin steps at LOD0.
                Soffit(m, k, r, s, lod);
            }
            if (lod == 0 || lod == 1 && r.Horns && Math.Max(r.EaveU, r.EaveW) > 6) Hips(m, k, r, lod);
            if (r.Fringe && lod <= 1) Fringe(m, k, r, lod);
            return m.TriangleCount - t0;
        }

        /// <summary>One row of roof vertices at t, raised along the normal by <paramref name="off"/>; kind 1 rows belong
        /// to a course lip (normals face down the slope).</summary>
        private static void RoofRow(MeshData m, in Affine3 k, in RoofSpec r, int s, double[] cols, double t, double off, int kind, ref int rows)
        {
            for (int c = 0; c < cols.Length; c++)
            {
                double u, v, w, nu, nv, nw;
                RoofPoint(r, s, cols[c], t, out u, out v, out w);
                RoofNormal(r, s, cols[c], t, out nu, out nv, out nw);
                double px = u + nu * off, py = v + nv * off, pz = w + nw * off;
                if (kind == 1)
                {
                    // Down-slope direction: the lip faces the eave.
                    double u2, v2, w2;
                    RoofPoint(r, s, cols[c], Math.Max(0, t - 0.02), out u2, out v2, out w2);
                    double du = u2 - u, dv = v2 - v, dw = w2 - w, l = Math.Sqrt(du * du + dv * dv + dw * dw);
                    if (l > 1e-9)
                    {
                        nu = 0.75 * du / l + 0.25 * nu;
                        nv = 0.75 * dv / l + 0.25 * nv;
                        nw = 0.75 * dw / l + 0.25 * nw;
                    }
                }
                float ao = kind == 1 ? 0.75f : 1f;
                SacredDraw.V(m, k, px, py, pz, nu, nv, nw, Looks.Ao(r.Roof, ao * r.Roof.Ao));
            }
            rows++;
        }

        /// <summary>Standing seams of a gilt copper roof: low ridges running down the slope about every 0.9 m.</summary>
        private static void Seams(MeshData m, in Affine3 k, in RoofSpec r, int s)
        {
            double len = 2 * (s % 2 == 0 ? r.EaveU : r.EaveW);
            int n = Math.Max(3, (int)Math.Round(len / 0.9));
            double da = 0.05 / Math.Max(0.5, 0.5 * len);
            ShapeBrush b = Looks.Ao(r.Ridge, 1f);
            for (int j = 0; j < n; j++)
            {
                double a = -0.94 + 1.88 * (j + 0.5) / n;
                int f = m.VertexCount;
                for (int q = 0; q <= 2; q++)
                {
                    double t = 0.03 + 0.94 * q / 2;
                    for (int side = -1; side <= 1; side++)
                    {
                        double u, v, w, nu, nv, nw;
                        RoofPoint(r, s, a + side * da, t, out u, out v, out w);
                        RoofNormal(r, s, a, t, out nu, out nv, out nw);
                        double h = side == 0 ? 0.05 : 0.0;
                        // Side faces lean outward.
                        double su, sw;
                        AlongDir(s, out su, out sw);
                        double tn = side * 0.6;
                        SacredDraw.V(m, k, u + nu * h, v + nv * h, w + nw * h, nu + tn * su, nv, nw + tn * sw, b);
                    }
                }
                SacredDraw.Grid(m, f, 3, 3, false, null);
            }
        }

        /// <summary>Direction of increasing a along a side, in local (u, w).</summary>
        private static void AlongDir(int side, out double su, out double sw)
        {
            su = side == 0 ? 1 : side == 2 ? -1 : 0;
            sw = side == 1 ? 1 : side == 3 ? -1 : 0;
        }

        private static void Soffit(MeshData m, in Affine3 k, in RoofSpec r, int s, int lod)
        {
            int nc = SoffitCols.Length;
            int first = m.VertexCount, rows = 0;
            bool[] seam = Seams(16);
            ShapeBrush sb = r.Soffit;
            // Fractions from the eave (0) to the wall (1) and drops of the purlin beams.
            int stations = lod == 0 ? (Math.Abs(r.EaveW - r.WallW) > 2.2 ? 2 : 1) : 0;
            double ou, ow;
            Out(s, out ou, out ow);
            for (int st = -1; st <= stations; st++)
            {
                // st = -1: the eave; 0..stations-1: purlins (four rows each); stations: the wall.
                if (st == -1 || st == stations)
                {
                    double f = st == -1 ? 0 : 1;
                    SoffitRow(m, k, r, s, f, 0, sb, 0, -1, 0, ref rows);
                    if (st == -1 && stations > 0) seam[rows - 1] = false;
                    continue;
                }
                double fc = (st + 1.0) / (stations + 1), half = 0.06 / Math.Max(0.5, Math.Abs(r.EaveW - r.WallW));
                const double Drop = 0.13;
                SoffitRow(m, k, r, s, fc - half, 0, sb, 0, -1, 0, ref rows);
                seam[rows - 1] = true;
                SoffitRow(m, k, r, s, fc - half, 0, Looks.WoodBeam, 1, 0, 0, ref rows);
                SoffitRow(m, k, r, s, fc - half, Drop, Looks.WoodBeam, 1, 0, 0, ref rows);
                seam[rows - 1] = true;
                SoffitRow(m, k, r, s, fc - half, Drop, Looks.WoodBeam, 0, -1, 0, ref rows);
                SoffitRow(m, k, r, s, fc + half, Drop, Looks.WoodBeam, 0, -1, 0, ref rows);
                seam[rows - 1] = true;
                SoffitRow(m, k, r, s, fc + half, Drop, Looks.WoodBeam, -1, 0, 0, ref rows);
                SoffitRow(m, k, r, s, fc + half, 0, Looks.WoodBeam, -1, 0, 0, ref rows);
                seam[rows - 1] = true;
                SoffitRow(m, k, r, s, fc + half, 0, sb, 0, -1, 0, ref rows);
            }
            SacredDraw.Grid(m, first, rows, nc, false, seam);
        }

        /// <summary>A soffit row at fraction f from the eave (fascia bottom) to the wall top, dropped by
        /// <paramref name="drop"/>, normal (out, up) in the side's vertical plane.</summary>
        private static void SoffitRow(MeshData m, in Affine3 k, in RoofSpec r, int s, double f, double drop, in ShapeBrush b, double nOut, double nUp,
                                      int unused, ref int rows)
        {
            double ou, ow;
            Out(s, out ou, out ow);
            for (int c = 0; c < SoffitCols.Length; c++)
            {
                double a = SoffitCols[c];
                double eu, ev, ew;
                RoofPoint(r, s, a, 0, out eu, out ev, out ew);
                ev -= r.FasciaH;
                eu -= 0.02 * ou;
                ew -= 0.02 * ow;
                double iu, iw;
                OnRect(s, a, r.WallU, r.WallW, out iu, out iw);
                double u = eu + (iu - eu) * f, w = ew + (iw - ew) * f, v = ev + (r.WallTopV - ev) * f - drop;
                SacredDraw.V(m, k, u, v, w, nOut * ou, nUp, nOut * ow, b);
            }
            rows++;
        }

        /// <summary>Rounded hip rolls along the four hips and the upturned corner horns (kunsala).</summary>
        private static void Hips(MeshData m, in Affine3 k, in RoofSpec r, int lod)
        {
            double size = Math.Max(0.6, Math.Min(1.5, Math.Max(r.EaveU, r.EaveW) / 7.0));
            double rad = 0.075 * size;
            int segs = lod == 0 ? 4 : 3;
            for (int s = 0; s < 4; s++)
            {
                Path3 p = PathS;
                for (int q = 0; q <= 3; q++)
                {
                    double t = q / 3.0;
                    double u, v, w, nu, nv, nw, mu, mv, mw;
                    RoofPoint(r, s, 1, t, out u, out v, out w);
                    RoofNormal(r, s, 0.999, t, out nu, out nv, out nw);
                    RoofNormal(r, (s + 3) % 4, -0.999, t, out mu, out mv, out mw);
                    double h = 0.06 + 0.5 * rad;
                    p.Add(u + 0.5 * (nu + mu) * h, v + 0.5 * (nv + mv) * h, w + 0.5 * (nw + mw) * h);
                }
                Shapes.Tube(m, k, r.Ridge, p, rad, segs, true, false, default, 0.8 * rad);
                if (!r.Horns || lod > 0) continue;
                // The corner horn: out along the diagonal, curling up.
                double cu, cv, cw;
                RoofPoint(r, s, 1, 0, out cu, out cv, out cw);
                double dl = Math.Sqrt(cu * cu + cw * cw), du = cu / dl, dw = cw / dl;
                Path3 h2 = PathS;
                cv += 0.03;
                h2.Add(cu - 0.15 * du * size, cv - 0.04 * size, cw - 0.15 * dw * size);
                h2.Add(cu + 0.18 * du * size, cv + 0.02 * size, cw + 0.18 * dw * size);
                h2.Add(cu + 0.36 * du * size, cv + 0.18 * size, cw + 0.36 * dw * size);
                h2.Add(cu + 0.38 * du * size, cv + 0.42 * size, cw + 0.38 * dw * size);
                h2.Add(cu + 0.28 * du * size, cv + 0.55 * size, cw + 0.28 * dw * size);
                Shapes.Tube(m, k, r.Horn, h2, 0.1 * size, 4, true, false, default, 0.035 * size);
            }
        }

        /// <summary>The red cloth valance with a white top band hung under the fascia, its lower edge scalloped.</summary>
        private static void Fringe(MeshData m, in Affine3 k, in RoofSpec r, int lod)
        {
            for (int s = 0; s < 4; s++)
            {
                double len = 2 * (s % 2 == 0 ? r.EaveU : r.EaveW);
                double pitch = r.FringePitch > 0.2 ? r.FringePitch : 0.7;
                int n = Math.Max(4, (int)Math.Round(len / (lod == 0 ? pitch : Math.Max(1.2, pitch))));
                double ou, ow;
                Out(s, out ou, out ow);
                int first = m.VertexCount;
                for (int row = 0; row < 4; row++)
                {
                    for (int c = 0; c <= n; c++)
                    {
                        double a = -1 + 2.0 * c / n;
                        double u, v, w;
                        RoofPoint(r, s, a, 0, out u, out v, out w);
                        v -= r.FasciaH;
                        double drop = row == 0 ? 0 : row <= 2 ? 0.09 : (c % 2 == 0 ? r.FringeH : 0.72 * r.FringeH);
                        if (r.Clear != null && row > 0)
                        {
                            // Over a road corridor the valance stops at the overhead clearance (contract §1).
                            double x, y, z;
                            k.Point(u + 0.035 * ou, v, w + 0.035 * ow, out x, out y, out z);
                            double minY = r.Clear.MinHangY(x, z, r.GroundY);
                            drop = Math.Max(0, Math.Min(drop, y - minY));
                        }
                        ShapeBrush b = row <= 1 ? Looks.FringeWhite : Looks.FringeRed;
                        SacredDraw.V(m, k, u + 0.035 * ou, v - drop, w + 0.035 * ow, ou, -0.1, ow, b);
                    }
                }
                bool[] seam = Seams(4);
                seam[1] = true;
                SacredDraw.Grid(m, first, 4, n + 1, false, seam);
            }
        }

        /// <summary>
        /// A hipped (or, on a long plan, ridged) tile or gilt roof over a wall of half sizes (wu, ww) whose top is at
        /// <paramref name="wallTop"/>: the eave edge sits <paramref name="drop"/> under the wall top with
        /// <paramref name="overhang"/> metres of eave, the slope rises to <paramref name="apexV"/> (a ridge along the long
        /// side, or a small square apex block). Returns the spec it drew (for struts and bells).
        /// </summary>
        public static RoofSpec HipRoof(MeshData m, in Affine3 k, double wu, double ww, double wallTop, double overhang, double drop, double apexV, bool gilt,
                                       uint tile, bool fringe, int lod)
        {
            double eu = wu + overhang, ewh = ww + overhang, ev = wallTop - drop;
            double tu = Math.Max(0.12, eu - ewh), tw = Math.Max(0.12, ewh - eu);
            if (tu > 0.12 && tw > 0.12) tu = tw = 0.12;
            double t = (eu - wu) / Math.Max(1e-6, eu - tu);
            var r = new RoofSpec
            {
                EaveU = eu, EaveW = ewh, EaveV = ev, TopU = tu, TopW = tw, TopV = apexV, WallU = wu, WallW = ww,
                WallTopV = Math.Max(ev + 0.05, ev + t * (apexV - ev) - 0.25), Lift = 0.05 * Math.Max(eu, ewh), Sag = 0.02 * Math.Min(eu, ewh),
                FasciaH = Math.Min(0.24, 0.1 + 0.01 * Math.Max(eu, ewh)), CoursePitch = 0.62, Gilt = gilt, Fringe = fringe, FringeH = 0.3, Horns = true,
                Roof = gilt ? Looks.Gilt : Looks.Of(tile != 0 ? tile : SacredPalette.Tile, MaterialChannel.RoofTile),
                Ridge = gilt ? Looks.GiltHi : Looks.Of(MeshColor.Scale(tile != 0 ? tile : SacredPalette.Tile, 0.72f), MaterialChannel.RoofTile),
                Fascia = gilt ? Looks.GiltAged : Looks.WoodDark, Soffit = Looks.Ao(Looks.WoodDark, 0.55f), Horn = gilt ? Looks.Gilt : Looks.WoodDark,
            };
            Roof(m, k, r, lod);
            if (tu > 0.12 && lod <= 1)
            {
                // The ridge roll between the two hips.
                Path3 p = PathS;
                p.Add(-tu, apexV + 0.06, 0).Add(tu, apexV + 0.06, 0);
                Shapes.Tube(m, k, r.Ridge, p, 0.09, 4, true);
            }
            return r;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Struts and bells
        // ---------------------------------------------------------------------------------------------------------

        private static readonly double[] StrutRich = { 0.0, 0.12, 0.13, 0.17, 0.085, 0.07, 0.3, 0.11, 0.075, 0.55, 0.095, 0.07, 0.68, 0.125, 0.08, 0.73, 0.065, 0.06, 0.81, 0.085, 0.085, 1.0, 0.14, 0.09 };
        private static readonly double[] StrutPlain = { 0.0, 0.11, 0.07, 0.16, 0.09, 0.065, 1.0, 0.14, 0.09 };
        private static readonly double[] StrutLow = { 0.0, 0.1, 0.07, 1.0, 0.12, 0.08 };

        /// <summary>
        /// A carved strut (tunāla) prototype of length L in its own frame: y along the strut from the foot, x across
        /// (along the wall), z out of its carved face. Rounded board sections widen and narrow for the foot figure, the
        /// standing deity (painted), its head and the scroll capital; corner struts get griffin wings. Built into a
        /// scratch mesh to be copied with <see cref="Shapes.CopyTransformed"/>.
        /// </summary>
        public static MeshData StrutProto(double len, double size, bool rich, bool corner, in ShapeBrush wood, in ShapeBrush paint, int lod)
        {
            MeshData p = Proto;
            double[] st = lod >= 1 ? StrutLow : rich ? StrutRich : StrutPlain;
            int n = st.Length / 3;
            Affine3 id = Affine3.Identity;
            int first = p.VertexCount;
            for (int i = 0; i < n; i++)
            {
                double y = st[3 * i] * len, hx = st[3 * i + 1] * size, hz = st[3 * i + 2] * size;
                bool painted = lod == 0 && (rich ? i >= 3 && i < n - 1 && i != 5 : i == 1);
                ShapeBrush b = painted ? paint : wood;
                for (int c = 0; c < 4; c++)
                {
                    double sx = c == 0 || c == 3 ? 1 : -1, sz = c < 2 ? 1 : -1;
                    SacredDraw.V(p, id, sx * hx, y, sz * hz, sx * 0.7, 0, sz * 0.7, b);
                }
            }
            SacredDraw.Grid(p, first, n, 4, true, null);
            if (corner && lod == 0)
            {
                // Griffin wings: two-sided fans from the shoulders.
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    double x0 = sgn * 0.1 * size, x1 = sgn * 0.42 * size;
                    for (int face = -1; face <= 1; face += 2)
                    {
                        int a = SacredDraw.V(p, id, x0, 0.55 * len, 0.03 * face, 0, 0, face, paint);
                        int b2 = SacredDraw.V(p, id, x1, 0.92 * len, 0.03 * face, 0, 0, face, paint);
                        int c2 = SacredDraw.V(p, id, x0, 0.88 * len, 0.03 * face, 0, 0, face, paint);
                        int d2 = SacredDraw.V(p, id, 0.7 * x1, 0.7 * len, 0.03 * face, 0, 0, face, paint);
                        SacredDraw.Tri(p, a, d2, b2);
                        SacredDraw.Tri(p, a, b2, c2);
                    }
                }
            }
            return p;
        }

        /// <summary>
        /// Place the struts of one tier: <paramref name="perU"/> struts on the sides along u and <paramref name="perW"/> on the others, leaning from the foot ledge on the
        /// wall (height <paramref name="footV"/>) up to the soffit near the eave, plus a winged corner strut at each
        /// corner (all four at LOD0). Returns the struts drawn.
        /// </summary>
        public static int Struts(MeshData m, in Affine3 k, in RoofSpec r, double footV, int perU, int perW, bool rich, in ShapeBrush wood, uint seed, int lod)
        {
            if (lod >= 2) return 0;
            int drawn = 0;
            double depth = Math.Abs(r.EaveW - r.WallW), size = Math.Max(0.8, Math.Min(1.6, depth / 2.2));
            double headF = 0.78;
            // Head height: on the soffit at headF.
            double hv = r.WallTopV + ((r.EaveV - r.FasciaH) - r.WallTopV) * headF - 0.08;
            double hw = depth * headF;
            double footOut = 0.1;
            double dv = hv - footV, dwl = hw - footOut, len = Math.Sqrt(dv * dv + dwl * dwl);
            if (len < 0.4) return 0;
            double cy = dv / len, sy = dwl / len;
            for (int corner = 0; corner <= 1; corner++)
            {
                if (corner == 1 && lod > 0) break;
                ShapeBrush paint = PaintFor(seed + (uint)corner * 7);
                MeshData proto = corner == 0 ? StrutProto(len, size, rich, false, wood, paint, lod) : StrutProto(len * 1.18, size * 1.2, rich, true, wood, paint, lod);
                for (int s = 0; s < 4; s++)
                {
                    double along = s % 2 == 0 ? r.WallU : r.WallW, wall = s % 2 == 0 ? r.WallW : r.WallU;
                    double ou, ow, su, sw;
                    Out(s, out ou, out ow);
                    AlongDir(s, out su, out sw);
                    if (corner == 0)
                    {
                        int per = s % 2 == 0 ? perU : perW, draw = lod == 0 ? per : Math.Max(2, per / 2);
                        for (int q = 0; q < draw; q++)
                        {
                            double x = -along + 2 * along * (q + 0.5) / draw;
                            double fu = su * x + ou * (wall + footOut), fw = sw * x + ow * (wall + footOut);
                            Affine3 side = SacredDraw.Turn(k, fu, footV, fw, s * 0.5 * Math.PI);
                            Affine3 xf = Affine3.FromBasis(1, 0, 0, 0, cy, sy, 0, -sy, cy, 0, 0, 0).Then(side);
                            Shapes.CopyTransformed(m, proto, xf, Tint(seed, s, q));
                            drawn++;
                        }
                    }
                    else
                    {
                        // Diagonal at the corner where this side meets the next one (a = +1 end).
                        double cu = su * along + ou * (wall + footOut), cw = sw * along + ow * (wall + footOut);
                        double ang = s * 0.5 * Math.PI - 0.25 * Math.PI;
                        Affine3 side = SacredDraw.Turn(k, cu, footV, cw, ang);
                        double dlen = Math.Sqrt(dv * dv + 2 * dwl * dwl) * 1.0, c2 = dv / dlen, s2 = Math.Sqrt(2) * dwl / dlen;
                        Affine3 xf = Affine3.FromBasis(1, 0, 0, 0, c2, s2, 0, -s2, c2, 0, 0, 0).Then(side);
                        Shapes.CopyTransformed(m, proto, xf, 0xFFFFFFFFu);
                        drawn++;
                    }
                }
            }
            return drawn;
        }

        private static ShapeBrush PaintFor(uint seed)
        {
            switch (seed % 4)
            {
                case 0: return Looks.Of(MeshColor.FromHex(0x8E5B37), MaterialChannel.WoodCarved);
                case 1: return Looks.Of(MeshColor.FromHex(0x7A4A2E), MaterialChannel.WoodCarved);
                case 2: return Looks.Of(MeshColor.FromHex(0x6B4129), MaterialChannel.WoodCarved);
                default: return Looks.Of(MeshColor.FromHex(0x845236), MaterialChannel.WoodCarved);
            }
        }

        /// <summary>A small deterministic tint so neighbouring struts are not identical.</summary>
        private static uint Tint(uint seed, int side, int q)
        {
            uint h = ShapeNoise.Hash(side, q, 17, seed);
            uint d = 230u + (h % 26u);
            return (d << 24) | (d << 16) | (d << 8) | 0xFFu;
        }

        /// <summary>A bell prototype facing +z: a bronze bell (five-sided with a leaf clapper plate when rich, else a
        /// four-sided bell with a small leaf), on a hanger when <paramref name="drop"/> &gt; 0.</summary>
        public static MeshData BellProto(double size, double drop, bool rich, int lod)
        {
            MeshData p = Proto;
            Affine3 id = Affine3.Identity;
            ShapeBrush b = Looks.Bronze, leaf = Looks.GiltAged;
            double top = -drop, h = 0.21 * size, rr = 0.09 * size;
            if (drop > 0.01)
            {
                int a0 = SacredDraw.V(p, id, -0.01, 0, 0.0, 0, 0, 1, b), a1 = SacredDraw.V(p, id, 0.01, 0, 0, 0, 0, 1, b);
                int a2 = SacredDraw.V(p, id, 0.01, top, 0, 0, 0, 1, b), a3 = SacredDraw.V(p, id, -0.01, top, 0, 0, 0, 1, b);
                SacredDraw.Quad(p, a0, a1, a2, a3);
            }
            int sides = lod == 0 && rich ? 5 : 4;
            int apex = SacredDraw.V(p, id, 0, top, 0, 0, 1, 0, b);
            int ring = p.VertexCount;
            for (int i = 0; i < sides; i++)
            {
                double ang = 2 * Math.PI * (i + 0.5) / sides;
                double x = Math.Sin(ang), z = Math.Cos(ang);
                SacredDraw.V(p, id, rr * x, top - h, rr * z, x * 0.85, 0.4, z * 0.85, b);
            }
            for (int i = 0; i < sides; i++) SacredDraw.Tri(p, apex, ring + i, ring + (i + 1) % sides);
            if (lod == 0 && rich)
            {
                double l0 = top - h - 0.01 * size, l1 = l0 - 0.11 * size, lw = 0.04 * size;
                int c0 = SacredDraw.V(p, id, 0, l0, 0.0, 0, 0, 1, leaf), c1 = SacredDraw.V(p, id, lw, 0.5 * (l0 + l1), 0, 0, 0, 1, leaf);
                int c2 = SacredDraw.V(p, id, 0, l1, 0, 0, 0, 1, leaf);
                if (rich)
                {
                    int c3 = SacredDraw.V(p, id, -lw, 0.5 * (l0 + l1), 0, 0, 0, 1, leaf);
                    SacredDraw.Quad(p, c0, c1, c2, c3);
                }
                else
                {
                    SacredDraw.Tri(p, c0, c1, c2);
                }
            }
            return p;
        }

        /// <summary>Hang bells along each eave every <paramref name="spacing"/> metres (floor(eave / spacing) per side) from
        /// the fascia bottom. Returns the bells drawn.</summary>
        public static int Bells(MeshData m, in Affine3 k, in RoofSpec r, double spacing, double size, bool rich, int lod)
        {
            if (lod >= 1) return 0;
            double drop = r.Fringe ? r.FringeH + 0.04 : 0.02;
            MeshData proto = BellProto(size, drop, rich, lod);
            int drawn = 0;
            for (int s = 0; s < 4; s++)
            {
                double len = 2 * (s % 2 == 0 ? r.EaveU : r.EaveW);
                int n = Math.Max(1, (int)Math.Floor(len / Math.Max(0.1, spacing)));
                int step = lod == 0 ? 1 : 3;
                double ou, ow;
                Out(s, out ou, out ow);
                for (int q = 0; q < n; q += step)
                {
                    double a = -1 + 2.0 * (q + 0.5) / n;
                    double u, v, w;
                    RoofPoint(r, s, a, 0, out u, out v, out w);
                    if (r.Clear != null)
                    {
                        // No bell hangs into a road corridor's overhead clearance.
                        double x, y, z;
                        k.Point(u - 0.07 * ou, v - r.FasciaH - drop - 0.36 * size, w - 0.07 * ow, out x, out y, out z);
                        if (y < r.Clear.MinHangY(x, z, r.GroundY)) continue;
                    }
                    Affine3 xf = SacredDraw.Turn(k, u - 0.07 * ou, v - r.FasciaH, w - 0.07 * ow, s * 0.5 * Math.PI);
                    Shapes.CopyTransformed(m, proto, xf);
                    drawn++;
                }
            }
            return drawn;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Gajur, pataka
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The gilt gajur on a square stepped base at (0, v0, 0): pot (kalasha), stacked lotus and bell rings, a small
        /// parasol and the point (ref_temples 1.4); <paramref name="taleju"/> builds the big bell form with four small
        /// finials at the base corners. Total height h, base radius rr. Returns the tip height.
        /// </summary>
        public static double Gajur(MeshData m, in Affine3 k, double v0, double h, double rr, in ShapeBrush gilt, int lod, bool taleju = false)
        {
            if (lod >= 2)
            {
                Shapes.Cone(m, SacredDraw.At(k, 0, v0, 0), gilt, 1.1 * rr, h, 5, 0, 0, false);
                return v0 + h;
            }
            double bh = 0.12 * h;
            SacredDraw.Box(m, k, -1.3 * rr, 1.3 * rr, v0, v0 + bh, -1.3 * rr, 1.3 * rr, gilt, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod == 0) SacredDraw.Box(m, k, -1.15 * rr, 1.15 * rr, v0 + bh, v0 + 1.4 * bh, -1.15 * rr, 1.15 * rr, Looks.GiltAged, BoxFaces.All & ~BoxFaces.Bottom);
            double b0 = v0 + bh, hh = h - bh;
            Profile2 p = Prof;
            if (taleju)
            {
                p.Add(0, 0).Add(0.9 * rr, 0, true).Add(1.15 * rr, 0.06 * hh).Add(1.2 * rr, 0.12 * hh).Add(1.0 * rr, 0.24 * hh).Add(0.7 * rr, 0.38 * hh)
                 .Add(0.5 * rr, 0.46 * hh).Add(0.62 * rr, 0.52 * hh).Add(0.45 * rr, 0.58 * hh).Add(0.55 * rr, 0.64 * hh).Add(0.3 * rr, 0.7 * hh)
                 .Add(0.42 * rr, 0.74 * hh).Add(0.18 * rr, 0.84 * hh).Add(0.08 * rr, 0.93 * hh).Add(0, hh);
            }
            else
            {
                p.Add(0, 0).Add(0.85 * rr, 0, true).Add(0.85 * rr, 0.05 * hh, true).Add(0.6 * rr, 0.08 * hh).Add(0.88 * rr, 0.18 * hh).Add(0.95 * rr, 0.27 * hh)
                 .Add(0.75 * rr, 0.36 * hh).Add(0.38 * rr, 0.42 * hh).Add(0.62 * rr, 0.46 * hh, true).Add(0.36 * rr, 0.5 * hh).Add(0.52 * rr, 0.58 * hh)
                 .Add(0.42 * rr, 0.65 * hh).Add(0.24 * rr, 0.69 * hh).Add(0.8 * rr, 0.73 * hh, true).Add(0.2 * rr, 0.79 * hh).Add(0.26 * rr, 0.84 * hh)
                 .Add(0.14 * rr, 0.9 * hh).Add(0, hh);
            }
            Shapes.Lathe(m, SacredDraw.At(k, 0, b0, 0), gilt, p, Seg(12, lod));
            if (taleju && lod <= 1)
            {
                for (int c = 0; c < 4; c++)
                {
                    double cu = (c % 2 == 0 ? 1 : -1) * 1.15 * rr, cw = (c < 2 ? 1 : -1) * 1.15 * rr;
                    Profile2 q = Prof;
                    double sh = 0.4 * hh, sr = 0.32 * rr;
                    q.Add(0, 0).Add(0.6 * sr, 0, true).Add(sr, 0.3 * sh).Add(0.7 * sr, 0.55 * sh).Add(0.3 * sr, 0.7 * sh).Add(0.08 * sr, 0.9 * sh).Add(0, sh);
                    Shapes.Lathe(m, SacredDraw.At(k, cu, b0, cw), gilt, q, Seg(8, lod));
                }
            }
            return v0 + h;
        }

        /// <summary>A gilt pataka banner: a ribbon of <paramref name="width"/> along a local polyline (draped over the
        /// front roofs and hanging between them), facing outward, with small round plates.</summary>
        public static void Pataka(MeshData m, in Affine3 k, double[] pu, double[] pv, double[] pw, int n, double width, int lod)
        {
            if (n < 2) return;
            int first = m.VertexCount;
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(n - 1, i + 1);
                double tv = pv[b] - pv[a], tw = pw[b] - pw[a], tl = Math.Sqrt(tv * tv + tw * tw);
                if (tl < 1e-9) tl = 1;
                // Normal in the (v, w) plane, outward (+w side).
                double nv = tw / tl, nw = -tv / tl;
                if (nw < 0 || (Math.Abs(nw) < 1e-6 && nv < 0))
                {
                    nv = -nv;
                    nw = -nw;
                }
                for (int side = -1; side <= 1; side += 2)
                    SacredDraw.V(m, k, pu[i] + side * 0.5 * width, pv[i], pw[i], 0, nv, nw, i % 2 == 0 ? Looks.GiltHi : Looks.Gilt);
            }
            SacredDraw.Grid(m, first, n, 2, false, null);
            if (lod > 0) return;
            for (int i = 1; i < n - 1; i += 2)
            {
                Affine3 x = SacredDraw.Basis(k, pu[i], pv[i], pw[i] + 0.02, 1, 0, 0, 0, 0, 1);
                Shapes.Cylinder(m, x, Looks.GiltHi, 0.6 * width, 0.03, 8);
            }
        }
    }
}
