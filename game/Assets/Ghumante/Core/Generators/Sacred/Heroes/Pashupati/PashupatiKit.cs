using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Pashupati
{
    /// <summary>
    /// One hipped roof of a Newar temple as built at Pashupati (pashupati-temple.md PT-01, PT-06, PT-07) in a structure's
    /// local frame (u along the site's a axis, v up, w along b): the eave rectangle (half sizes, height), the top
    /// rectangle where the slope meets the storey above (or the flat top), the corner lift, the fascia, the standing seams
    /// (pitch, 0 = none) and the curled corner tips.
    /// </summary>
    internal struct PRoof
    {
        public double EaveU, EaveW, EaveV, TopU, TopW, TopV;
        public double Lift, FasciaH, SeamPitch, CurlSize;
        public ShapeBrush Roof, Seam, Fascia, Curl;
    }

    /// <summary>
    /// The drawing kit of the Pashupatinath site: the colour palette of the brief, the gilt (or verdigris, or grey metal)
    /// standing-seam roofs with their curled corner tips, the slanting black lattice skirts under the eaves (the
    /// strongest single cue of Pashupati's look), the carved struts with their standing deities, bell finials, and the
    /// lathe and loft helpers the structures share. Local frames come as <see cref="Affine3"/> (proper rotations of the
    /// site frame). Allocation-free after warm-up (per-thread scratch).
    /// </summary>
    internal static class PashupatiKit
    {
        // ---------------------------------------------------------------------------------------------------------
        // Palette (pashupati-temple.md §4, colours as hex)
        // ---------------------------------------------------------------------------------------------------------

        public static readonly ShapeBrush GiltRoof = Br(0xA88C52, MaterialChannel.Gilt);
        public static readonly ShapeBrush GiltRib = Br(0xC4A462, MaterialChannel.Gilt);
        public static readonly ShapeBrush GiltEave = Br(0x8E7238, MaterialChannel.Gilt);
        public static readonly ShapeBrush GiltBright = Br(0xE9CB62, MaterialChannel.Gilt);
        public static readonly ShapeBrush GiltShade = Br(0xA3832B, MaterialChannel.Gilt);
        public static readonly ShapeBrush Lattice = Br(0x1E1A18, MaterialChannel.WoodCarved, 0.75f);
        public static readonly ShapeBrush LatticeBar = Br(0x433D38, MaterialChannel.WoodCarved);
        public static readonly ShapeBrush StrutWood = Br(0x3A2518, MaterialChannel.WoodCarved);
        public static readonly ShapeBrush PaintRed = Br(0x5E3428, MaterialChannel.Paint);
        public static readonly ShapeBrush PaintGreen = Br(0x3A4A3C, MaterialChannel.Paint);
        public static readonly ShapeBrush PaintGilt = Br(0xB08A3A, MaterialChannel.Gilt);
        public static readonly ShapeBrush Silver = Br(0xA9ACB0, MaterialChannel.Metal);
        public static readonly ShapeBrush SilverDark = Br(0x5E5B57, MaterialChannel.Metal);
        public static readonly ShapeBrush StonePlinth = Br(0x8E8A80, MaterialChannel.Stone);
        public static readonly ShapeBrush StoneEdge = Br(0xA39E92, MaterialChannel.Stone);
        public static readonly ShapeBrush StoneWall = Br(0x4E4842, MaterialChannel.Stone);
        public static readonly ShapeBrush StoneDark = Br(0x5C5A55, MaterialChannel.Stone);
        public static readonly ShapeBrush WoodDark = Br(0x2A2220, MaterialChannel.WoodCarved);
        public static readonly ShapeBrush WoodDoor = Br(0x4A2A1C, MaterialChannel.Wood);
        public static readonly ShapeBrush Verdigris = Br(0x7FA595, MaterialChannel.Metal);
        public static readonly ShapeBrush VerdigrisRib = Br(0x94AC95, MaterialChannel.Metal);
        public static readonly ShapeBrush SlateMetal = Br(0x5E6466, MaterialChannel.Metal);
        public static readonly ShapeBrush SlateRib = Br(0x7D8486, MaterialChannel.Metal);
        public static readonly ShapeBrush GreyMetal = Br(0xA4ADB4, MaterialChannel.Metal);
        public static readonly ShapeBrush GreyRib = Br(0xBFC6CC, MaterialChannel.Metal);
        public static readonly ShapeBrush Cream = Br(0xEDE4C6, MaterialChannel.Plaster);
        public static readonly ShapeBrush White = Br(0xF1EEE6, MaterialChannel.Plaster);
        public static readonly ShapeBrush RedTrim = Br(0xB3262A, MaterialChannel.Paint);
        public static readonly ShapeBrush RustRoof = Br(0x9C4A3A, MaterialChannel.Metal);
        public static readonly ShapeBrush RustRib = Br(0xB86C5A, MaterialChannel.Metal);
        public static readonly ShapeBrush Vermilion = Br(0xC0502C, MaterialChannel.Stone);
        public static readonly ShapeBrush VermilionDark = Br(0x9A4A36, MaterialChannel.Stone);
        public static readonly ShapeBrush Brass = Br(0xC8A24A, MaterialChannel.Metal);
        public static readonly ShapeBrush Ink = Br(0x161210, MaterialChannel.Plain);

        public static ShapeBrush Br(uint rgb, MaterialChannel ch, float ao = 1f)
        {
            return new ShapeBrush(MeshColor.FromHex(rgb), ch, ao);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Scratch
        // ---------------------------------------------------------------------------------------------------------

        [ThreadStatic] private static Profile2 _prof, _prof2;
        [ThreadStatic] private static Path3 _path;
        [ThreadStatic] private static MeshData _strut, _strutCorner;
        [ThreadStatic] private static double _strutLen, _strutCornerLen;

        public static Profile2 Prof
        {
            get { return (_prof ?? (_prof = new Profile2(64))).Clear(false); }
        }

        public static Profile2 Prof2
        {
            get { return (_prof2 ?? (_prof2 = new Profile2(64))).Clear(false); }
        }

        public static Path3 PathS
        {
            get { return (_path ?? (_path = new Path3(32))).Clear(); }
        }

        /// <summary>Radial segments of a round part at a LOD (LOD0 full, LOD1 half, LOD2 a quarter; at least 4).</summary>
        public static int Seg(int lod0, int lod)
        {
            int s = lod <= 0 ? lod0 : lod == 1 ? lod0 / 2 : lod0 / 4;
            return Math.Max(4, s);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Roofs
        // ---------------------------------------------------------------------------------------------------------

        private static readonly double[] Cols0 = { -1, -0.86, -0.72, -0.55, 0, 0.55, 0.72, 0.86, 1 };
        private static readonly double[] Cols1 = { -1, -0.7, 0, 0.7, 1 };
        private static readonly double[] Cols2 = { -1, 1 };
        private static readonly double[] Rows0 = { 0, 0.12, 0.35, 0.65, 1 };
        private static readonly double[] Rows1 = { 0, 0.3, 1 };
        private static readonly double[] Rows2 = { 0, 1 };

        /// <summary>Local point on side s of a rectangle of half sizes (hu, hw): a in [−1, 1] along the side; the corners
        /// of neighbouring sides coincide. Sides: 0 = +w, 1 = −u, 2 = −w, 3 = +u.</summary>
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

        /// <summary>Outward unit direction of a side in local (u, w).</summary>
        public static void Out(int side, out double du, out double dw)
        {
            du = side == 1 ? -1 : side == 3 ? 1 : 0;
            dw = side == 0 ? 1 : side == 2 ? -1 : 0;
        }

        /// <summary>The roof surface at (side, a, t): t = 0 the eave edge, 1 the top ring; the corners lift.</summary>
        public static void RoofPoint(in PRoof r, int side, double a, double t, out double u, out double v, out double w)
        {
            double hu = r.EaveU + (r.TopU - r.EaveU) * t, hw = r.EaveW + (r.TopW - r.EaveW) * t;
            OnRect(side, a, hu, hw, out u, out w);
            double aa = Math.Abs(a), lf = aa > 0.55 ? (aa - 0.55) / 0.45 : 0;
            v = r.EaveV + (r.TopV - r.EaveV) * t + r.Lift * lf * lf * (1 - t) * (1 - t);
        }

        private static void RoofNormal(in PRoof r, int side, double a, double t, out double nu, out double nv, out double nw)
        {
            const double e = 1e-3;
            double u0, v0, w0, u1, v1, w1, u2, v2, w2, u3, v3, w3;
            RoofPoint(r, side, Math.Max(-1, a - e), t, out u0, out v0, out w0);
            RoofPoint(r, side, Math.Min(1, a + e), t, out u1, out v1, out w1);
            RoofPoint(r, side, a, Math.Max(0, t - e), out u2, out v2, out w2);
            RoofPoint(r, side, a, Math.Min(1, t + e), out u3, out v3, out w3);
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
        /// A hipped temple roof (straight slopes, the eave line straight with its corners lifted): the four slopes, the
        /// gilt eave board with its drip roll, the rounded hip rolls, the standing-seam ribs running down each slope's fall
        /// line every <see cref="PRoof.SeamPitch"/> metres (cut by the hips; LOD0 every rib, LOD1 every third) and the
        /// curled corner tips (spiral hooks, LOD0-1). Returns the triangles.
        /// </summary>
        public static int Roof(MeshData m, in Affine3 k, in PRoof r, int lod)
        {
            int t0 = m.TriangleCount;
            double[] cols = lod <= 0 ? Cols0 : lod == 1 ? Cols1 : Cols2;
            double[] rows = lod <= 0 ? Rows0 : lod == 1 ? Rows1 : Rows2;
            int nc = cols.Length, nr = rows.Length;
            for (int s = 0; s < 4; s++)
            {
                int first = m.VertexCount;
                for (int i = 0; i < nr; i++)
                {
                    for (int c = 0; c < nc; c++)
                    {
                        double u, v, w, nu, nv, nw;
                        RoofPoint(r, s, cols[c], rows[i], out u, out v, out w);
                        RoofNormal(r, s, cols[c], rows[i], out nu, out nv, out nw);
                        SacredDraw.V(m, k, u, v, w, nu, nv, nw, r.Roof);
                    }
                }
                SacredDraw.Grid(m, first, nr, nc, false, null);

                // The eave board: a vertical band under the eave edge, outward, following the corner lift.
                double ou, ow;
                Out(s, out ou, out ow);
                int ff = m.VertexCount;
                for (int row = 0; row < 2; row++)
                {
                    for (int c = 0; c < nc; c++)
                    {
                        double u, v, w;
                        RoofPoint(r, s, cols[c], 0, out u, out v, out w);
                        SacredDraw.V(m, k, u + 0.01 * ou, v - row * r.FasciaH, w + 0.01 * ow, ou, -0.05, ow, row == 0 ? r.Fascia : Ao(r.Fascia, 0.8f));
                    }
                }
                SacredDraw.Grid(m, ff, 2, nc, false, null);
                if (lod == 0)
                {
                    // The drip roll along the eave edge.
                    Path3 p = PathS;
                    for (int c = 0; c < nc; c++)
                    {
                        double u, v, w;
                        RoofPoint(r, s, cols[c], 0, out u, out v, out w);
                        p.Add(u + 0.03 * ou, v - 0.02, w + 0.03 * ow);
                    }
                    Shapes.Tube(m, k, r.Seam, p, 0.045, 5, true);
                }
                if (r.SeamPitch > 0 && lod <= 1) Ribs(m, k, r, s, lod);
            }
            if (lod <= 1) Hips(m, k, r, lod);
            if (r.CurlSize > 0 && lod <= 1) Curls(m, k, r, lod);
            return m.TriangleCount - t0;
        }

        /// <summary>The standing seams of one slope: low ridges down the fall line, cut where they meet the hips.</summary>
        private static void Ribs(MeshData m, in Affine3 k, in PRoof r, int s, int lod)
        {
            double eHalf = s % 2 == 0 ? r.EaveU : r.EaveW, tHalf = s % 2 == 0 ? r.TopU : r.TopW;
            int n = Math.Max(2, (int)Math.Round(2 * eHalf / r.SeamPitch));
            int step = lod == 0 ? 1 : 3;
            const double Hw = 0.05, H = 0.085;
            double su, sw;
            Along(s, out su, out sw);
            for (int j = step / 2; j < n; j += step)
            {
                double x = -eHalf + 2 * eHalf * (j + 0.5) / n, ax = Math.Abs(x);
                double tEnd = ax <= tHalf ? 1.0 : Math.Max(0, (eHalf - ax) / Math.Max(1e-6, eHalf - tHalf));
                if (tEnd < 0.04) continue;
                int first = m.VertexCount;
                const int St = 3;
                for (int q = 0; q < St; q++)
                {
                    double t = 0.01 + (tEnd - 0.02) * q / (St - 1);
                    double hu = r.EaveU + (r.TopU - r.EaveU) * t, hw = r.EaveW + (r.TopW - r.EaveW) * t;
                    double a = x / (s % 2 == 0 ? hu : hw);
                    double u, v, w, nu, nv, nw;
                    RoofPoint(r, s, a, t, out u, out v, out w);
                    RoofNormal(r, s, a, t, out nu, out nv, out nw);
                    for (int side = -1; side <= 1; side++)
                    {
                        double h = side == 0 ? H : 0.002;
                        SacredDraw.V(m, k, u + side * Hw * su + nu * h, v + nv * h, w + side * Hw * sw + nw * h, nu + 0.7 * side * su, nv, nw + 0.7 * side * sw,
                                     r.Seam);
                    }
                }
                SacredDraw.Grid(m, first, St, 3, false, null);
            }
        }

        /// <summary>Direction of increasing a along a side, in local (u, w).</summary>
        public static void Along(int side, out double su, out double sw)
        {
            su = side == 0 ? 1 : side == 2 ? -1 : 0;
            sw = side == 1 ? 1 : side == 3 ? -1 : 0;
        }

        private static void Hips(MeshData m, in Affine3 k, in PRoof r, int lod)
        {
            double rad = 0.06 + 0.006 * Math.Max(r.EaveU, r.EaveW);
            for (int s = 0; s < 4; s++)
            {
                Path3 p = PathS;
                for (int q = 0; q <= 3; q++)
                {
                    double t = q / 3.0, u, v, w;
                    RoofPoint(r, s, 1, t, out u, out v, out w);
                    p.Add(u, v + 0.6 * rad, w);
                }
                Shapes.Tube(m, k, r.Seam, p, rad, lod == 0 ? 6 : 4, true, false, default, 0.75 * rad);
            }
        }

        /// <summary>The curled gilt tips at the four eave corners: a spiral hook out along the diagonal, up and back.</summary>
        private static void Curls(MeshData m, in Affine3 k, in PRoof r, int lod)
        {
            double sz = r.CurlSize;
            for (int s = 0; s < 4; s++)
            {
                double cu, cv, cw;
                RoofPoint(r, s, 1, 0, out cu, out cv, out cw);
                double dl = Math.Sqrt(cu * cu + cw * cw), du = cu / dl, dw = cw / dl;
                Path3 p = PathS;
                // Out along the diagonal, then a curl up and back over itself (about one and a half turns of a shrinking
                // radius, in the vertical plane of the diagonal).
                p.Add(cu - 0.3 * sz * du, cv - 0.05 * sz, cw - 0.3 * sz * dw);
                int n = lod == 0 ? 9 : 5;
                for (int i = 0; i <= n; i++)
                {
                    double ang = -0.5 * Math.PI + 1.6 * Math.PI * i / n, rr = sz * (0.42 - 0.22 * i / n);
                    double ox = 0.3 * sz + rr * Math.Cos(ang), oy = 0.42 * sz + rr * Math.Sin(ang);
                    p.Add(cu + ox * du, cv + oy - 0.05 * sz, cw + ox * dw);
                }
                Shapes.Tube(m, k, r.Curl, p, 0.11 * sz, lod == 0 ? 6 : 4, true, false, default, 0.05 * sz);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Lattice skirts and struts
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The slanting black lattice skirt under an eave (pashupati-temple.md PT-01 3, 5): a band on all four sides from
        /// the wall line (half sizes wu, ww at height wv) out to the underside of the eave board (half sizes eu, ew at
        /// height ev), facing outward and down; at LOD0 a grid of lighter bars every <paramref name="cell"/> metres over
        /// it. Returns the triangles.
        /// </summary>
        public static int Skirt(MeshData m, in Affine3 k, double wu, double ww, double wv, double eu, double ew, double ev, double cell, int lod)
        {
            int t0 = m.TriangleCount;
            for (int s = 0; s < 4; s++)
            {
                double ou, ow, su, sw;
                Out(s, out ou, out ow);
                Along(s, out su, out sw);
                double wHalf = s % 2 == 0 ? wu : ww, eHalf = s % 2 == 0 ? eu : ew, wOff = s % 2 == 0 ? ww : wu, eOff = s % 2 == 0 ? ew : eu;
                // Band normal: outward and down (perpendicular to the band in the side's vertical plane).
                double dOut = eOff - wOff, dUp = ev - wv, l = Math.Sqrt(dOut * dOut + dUp * dUp);
                double nOut = dUp / l, nUp = -dOut / l;
                int cols = lod == 0 ? 6 : lod == 1 ? 3 : 1, rowsN = lod == 0 ? 2 : 1;
                int first = m.VertexCount;
                for (int i = 0; i <= rowsN; i++)
                {
                    double t = (double)i / rowsN, half = wHalf + (eHalf - wHalf) * t, off = wOff + (eOff - wOff) * t, v = wv + (ev - wv) * t;
                    for (int c = 0; c <= cols; c++)
                    {
                        double a = -1 + 2.0 * c / cols;
                        SacredDraw.V(m, k, su * a * half + ou * off, v, sw * a * half + ow * off, ou * nOut, nUp, ow * nOut, Lattice);
                    }
                }
                SacredDraw.Grid(m, first, rowsN + 1, cols + 1, false, null);
                if (lod > 0) continue;
                // Bars across the slope (parallel to the eave) and down it (radial, so they meet the hips cleanly).
                const double Bw = 0.022, Lift = 0.015;
                int across = Math.Max(2, (int)Math.Round(l / cell));
                for (int i = 1; i < across; i++)
                {
                    double t = (double)i / across, half = wHalf + (eHalf - wHalf) * t, off = wOff + (eOff - wOff) * t, v = wv + (ev - wv) * t;
                    double dt = Bw / l;
                    double h0 = half - (eHalf - wHalf) * dt, o0 = off - dOut * dt, v0 = v - dUp * dt;
                    double h1 = half + (eHalf - wHalf) * dt, o1 = off + dOut * dt, v1 = v + dUp * dt;
                    BarQuad(m, k, -h0, h0, o0, v0, -h1, h1, o1, v1, su, sw, ou, ow, nOut, nUp, Lift);
                }
                int down = Math.Max(2, (int)Math.Round(2 * eHalf / cell));
                for (int j = 1; j < down; j++)
                {
                    double a = -1 + 2.0 * j / down, bwA = Bw / Math.Max(0.5, eHalf);
                    SacredDraw.Quad(m, k,
                                    su * (a - bwA) * wHalf + ou * wOff + ou * nOut * Lift, wv + nUp * Lift, sw * (a - bwA) * wHalf + ow * wOff + ow * nOut * Lift,
                                    su * (a + bwA) * wHalf + ou * wOff + ou * nOut * Lift, wv + nUp * Lift, sw * (a + bwA) * wHalf + ow * wOff + ow * nOut * Lift,
                                    su * (a + bwA) * eHalf + ou * eOff + ou * nOut * Lift, ev + nUp * Lift, sw * (a + bwA) * eHalf + ow * eOff + ow * nOut * Lift,
                                    su * (a - bwA) * eHalf + ou * eOff + ou * nOut * Lift, ev + nUp * Lift, sw * (a - bwA) * eHalf + ow * eOff + ow * nOut * Lift,
                                    ou * nOut, nUp, ow * nOut, LatticeBar);
                }
            }
            return m.TriangleCount - t0;
        }

        private static void BarQuad(MeshData m, in Affine3 k, double a0, double b0, double off0, double v0, double a1, double b1, double off1, double v1, double su,
                                    double sw, double ou, double ow, double nOut, double nUp, double lift)
        {
            double lo = nOut * lift, lv = nUp * lift;
            SacredDraw.Quad(m, k, su * a0 + ou * (off0 + lo), v0 + lv, sw * a0 + ow * (off0 + lo), su * b0 + ou * (off0 + lo), v0 + lv, sw * b0 + ow * (off0 + lo),
                            su * b1 + ou * (off1 + lo), v1 + lv, sw * b1 + ow * (off1 + lo), su * a1 + ou * (off1 + lo), v1 + lv, sw * a1 + ow * (off1 + lo), ou * nOut,
                            nUp, ow * nOut, LatticeBar);
        }

        /// <summary>
        /// A carved strut (tunāla) prototype of length <paramref name="len"/> in its own frame (y along the strut from the
        /// foot, x across, z out of the carved face): the dark board with its bracket head, and on its face a standing
        /// deity (painted skirt, torso and four arms, head and gilt crown) over a crouching attendant at the foot; corner
        /// struts carry the winged griffin. LOD1: the board and the head. Cached per thread and LOD.
        /// </summary>
        public static MeshData StrutProto(double len, bool corner, int lod)
        {
            // Cached per thread by (corner, LOD, length); the key holds LOD + 1 so a fresh thread's zero never matches.
            double key = (lod + 1) * 1000.0 + len;
            MeshData p;
            if (corner)
            {
                if (_strutCorner != null && _strutCornerLen == key) return _strutCorner;
                p = _strutCorner ?? (_strutCorner = new MeshData(512, 1536));
                _strutCornerLen = key;
            }
            else
            {
                if (_strut != null && _strutLen == key) return _strut;
                p = _strut ?? (_strut = new MeshData(512, 1536));
                _strutLen = key;
            }
            p.Clear();
            ShapeAo.EnsureUv(p);
            p.HasUv0 = true;
            Affine3 id = Affine3.Identity;
            double w0 = corner ? 0.2 : 0.17, w1 = corner ? 0.25 : 0.21, th = corner ? 0.11 : 0.09;
            // The board, slightly widening upward.
            SacredDraw.Quad(p, id, -w0, 0, th, w0, 0, th, w1, len, th, -w1, len, th, 0, 0, 1, StrutWood);
            SacredDraw.Quad(p, id, -w0, 0, -th, w0, 0, -th, w1, len, -th, -w1, len, -th, 0, 0, -1, StrutWood);
            SacredDraw.Quad(p, id, w0, 0, -th, w0, 0, th, w1, len, th, w1, len, -th, 1, 0, 0, StrutWood);
            SacredDraw.Quad(p, id, -w0, 0, -th, -w0, 0, th, -w1, len, th, -w1, len, -th, -1, 0, 0, StrutWood);
            // The bracket head.
            SacredDraw.Box(p, id, -w1 - 0.05, w1 + 0.05, len - 0.16, len, -th - 0.02, th + 0.04, StrutWood, BoxFaces.All & ~BoxFaces.Top);
            double z = th;
            if (lod >= 1)
            {
                Shapes.Sphere(p, Affine3.Translation(0, 0.74 * len, z + 0.03), StrutWood, 0.08, 4);
            }
            else
            {
                double s = corner ? 1.2 : 1.0;
                // The crouching attendant at the foot.
                Shapes.Ellipsoid(p, Affine3.Translation(0, 0.12 * len, z + 0.04), StrutWood, 0.12 * s, 0.1 * s, 0.07, 5, false);
                // Skirt (painted red), torso (green and gilt), four arms, head and crown.
                Shapes.Frustum(p, Affine3.Translation(0, 0.24 * len, z + 0.02), PaintRed, 0.1 * s, 0.13 * s, 0.26 * len, 5, 0, 0, false, true);
                Shapes.Ellipsoid(p, Affine3.Translation(0, 0.6 * len, z + 0.05), PaintGreen, 0.11 * s, 0.12 * s, 0.06, 5, false);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    Shapes.Bar(p, Affine3.Identity, StrutWood, sgn * 0.08 * s, 0.66 * len, z + 0.06, sgn * 0.17 * s, 0.55 * len, z + 0.1, 0.025, 4);
                    Shapes.Bar(p, Affine3.Identity, PaintGilt, sgn * 0.07 * s, 0.62 * len, z + 0.07, sgn * 0.2 * s, 0.69 * len, z + 0.08, 0.022, 4);
                }
                Shapes.Sphere(p, Affine3.Translation(0, 0.73 * len, z + 0.05), StrutWood, 0.075 * s, 6);
                Shapes.Cone(p, Affine3.Translation(0, 0.77 * len, z + 0.05), PaintGilt, 0.06 * s, 0.11 * len, 5, 0, 0, false);
                // The aureole behind the head.
                Shapes.Cylinder(p, Affine3.RotationX(0.5 * Math.PI).Then(Affine3.Translation(0, 0.75 * len, z + 0.012)), PaintGilt, 0.13 * s, 0.02, 8, 0, 0, false, true);
                if (corner)
                {
                    // Griffin wings fanning from the shoulders.
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        for (int face = -1; face <= 1; face += 2)
                        {
                            int a = SacredDraw.V(p, id, sgn * 0.12, 0.5 * len, face * 0.03, 0, 0, face, PaintRed);
                            int b2 = SacredDraw.V(p, id, sgn * 0.5, 0.9 * len, face * 0.03, 0, 0, face, PaintRed);
                            int c2 = SacredDraw.V(p, id, sgn * 0.14, 0.86 * len, face * 0.03, 0, 0, face, PaintGilt);
                            int d2 = SacredDraw.V(p, id, sgn * 0.38, 0.64 * len, face * 0.03, 0, 0, face, PaintGilt);
                            SacredDraw.Tri(p, a, d2, b2);
                            SacredDraw.Tri(p, a, b2, c2);
                        }
                    }
                }
            }
            return p;
        }

        /// <summary>
        /// The struts of one tier: <paramref name="per"/> struts on each side (spread along the wall of half sizes wu, ww),
        /// leaning from their foot on the cornice (height <paramref name="footV"/>, a little outside the wall) to the
        /// skirt at <paramref name="headFrac"/> of the way to the eave (eu, ew at height ev), plus a winged corner strut
        /// at each corner. The strut lies just under the skirt with its carved face down and out. Returns the struts.
        /// </summary>
        public static int Struts(MeshData m, in Affine3 k, double wu, double ww, double footV, double eu, double ew, double ev, int perU, int perW,
                                 double headFrac, uint seed, int lod)
        {
            if (lod >= 2) return 0;
            int drawn = 0;
            for (int s = 0; s < 4; s++)
            {
                double ou, ow, su, sw;
                Out(s, out ou, out ow);
                Along(s, out su, out sw);
                double wHalf = s % 2 == 0 ? wu : ww, eHalf = s % 2 == 0 ? eu : ew, wOff = s % 2 == 0 ? ww : wu, eOff = s % 2 == 0 ? ew : eu;
                int per = s % 2 == 0 ? perU : perW, draw = lod == 0 ? per : Math.Max(2, per / 2);
                // Foot just outside the wall; head under the skirt.
                double fo = wOff + 0.12, ho = wOff + (eOff - wOff) * headFrac, hv = footV + (ev - footV) * headFrac - 0.18;
                for (int q = 0; q < draw; q++)
                {
                    double a = -1 + 2.0 * (q + 0.5) / draw;
                    double fx = a * wHalf * 0.94, hx = a * (wHalf + (eHalf - wHalf) * headFrac) * 0.94;
                    drawn += Strut(m, k, su * fx + ou * fo, footV, sw * fx + ow * fo, su * hx + ou * ho, hv, sw * hx + ow * ho, su, sw, false, seed + (uint)(s * 31 + q), lod);
                }
                if (lod == 0)
                {
                    // The corner strut on the diagonal at the a = +1 end of this side.
                    double cu = su * wHalf + ou * wOff, cw = sw * wHalf + ow * wOff;
                    double hu = su * (wHalf + (eHalf - wHalf) * headFrac) + ou * ho, hw = sw * (wHalf + (eHalf - wHalf) * headFrac) + ow * ho;
                    double dl = Math.Sqrt(cu * cu + cw * cw);
                    double xu = -cw / dl, xw = cu / dl;
                    drawn += Strut(m, k, cu * (1 + 0.12 / dl), footV, cw * (1 + 0.12 / dl), hu, hv, hw, xu, xw, true, seed + (uint)s * 7, lod);
                }
            }
            return drawn;
        }

        /// <summary>One strut from foot (fu, fv, fw) to head (hu, hv, hw), its board across the local direction (xu, xw).</summary>
        private static int Strut(MeshData m, in Affine3 k, double fu, double fv, double fw, double hu, double hv, double hw, double xu, double xw, bool corner, uint seed,
                                 int lod)
        {
            double yu = hu - fu, yv = hv - fv, yw = hw - fw, len = Math.Sqrt(yu * yu + yv * yv + yw * yw);
            if (len < 0.3) return 0;
            yu /= len;
            yv /= len;
            yw /= len;
            // x across (horizontal, orthogonalised), z = x × y (out of the carved face).
            double d = xu * yu + xw * yw;
            double ax = xu - d * yu, ay = -d * yv, az = xw - d * yw, al = Math.Sqrt(ax * ax + ay * ay + az * az);
            ax /= al;
            ay /= al;
            az /= al;
            double zx = ay * yw - az * yv, zy = az * yu - ax * yw, zz = ax * yv - ay * yu;
            // The carved face must look down and out: flip the board (x and z) if it faces up.
            if (zy > 0)
            {
                ax = -ax;
                ay = -ay;
                az = -az;
                zx = -zx;
                zy = -zy;
                zz = -zz;
            }
            MeshData proto = StrutProto(Math.Round(len * 20) / 20, corner, lod);
            Affine3 xf = Affine3.FromBasis(ax, ay, az, yu, yv, yw, zx, zy, zz, fu, fv, fw).Then(k);
            uint h = ShapeNoise.Hash((int)seed, 3, 9, 77u);
            uint g = 224u + h % 32u;
            Shapes.CopyTransformed(m, proto, xf, (g << 24) | (g << 16) | (g << 8) | 0xFFu);
            return 1;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Finials, mouldings, small parts
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A gilt bell finial (kalasha) of height h and base radius r standing at local (u, v, w): a lotus
        /// collar, the bell, two knobs and a spike (the small gajurs of the shrines and gates).</summary>
        public static void BellFinial(MeshData m, in Affine3 k, double u, double v, double w, double h, double r, in ShapeBrush b, int lod)
        {
            Affine3 at = SacredDraw.At(k, u, v, w);
            if (lod >= 2)
            {
                Shapes.Cone(m, at, b, r, h, 5, 0, 0, false);
                return;
            }
            Profile2 p = Prof;
            p.Add(0, 0).Add(1.0 * r, 0, true).Add(1.05 * r, 0.06 * h).Add(0.8 * r, 0.12 * h, true).Add(0.95 * r, 0.16 * h).Add(0.92 * r, 0.24 * h)
             .Add(0.7 * r, 0.4 * h).Add(0.45 * r, 0.52 * h).Add(0.28 * r, 0.56 * h, true).Add(0.36 * r, 0.62 * h).Add(0.2 * r, 0.7 * h, true)
             .Add(0.28 * r, 0.75 * h).Add(0.14 * r, 0.82 * h, true).Add(0.06 * r, 0.9 * h).Add(0, h);
            Shapes.Lathe(m, at, b, p, Seg(12, lod));
        }

        /// <summary>A lathe from a profile given as (radius, height) pairs, smooth except where <paramref name="crease"/>
        /// marks a point.</summary>
        public static void LatheOf(MeshData m, in Affine3 xf, in ShapeBrush b, double[] rh, int radial, int lod, ulong creaseMask = 0)
        {
            Profile2 p = Prof;
            for (int i = 0; i < rh.Length / 2; i++) p.Add(rh[2 * i], rh[2 * i + 1], (creaseMask >> i & 1UL) != 0);
            Shapes.Lathe(m, xf, b, p, Seg(radial, lod));
        }

        /// <summary>
        /// A smooth lofted limb along a path in the local y-z plane (x = <paramref name="x"/>): stations as (y, z, half
        /// width across x, half depth in the plane), each an ellipse square to the path, the ends closed with fans (the
        /// naga hoods of the kings' pillars, the Nandi's horns and similar tapering curved bodies). Returns its first
        /// vertex.
        /// </summary>
        public static int LoftYZ(MeshData m, in Affine3 k, in ShapeBrush b, double[] st, int seg, double x = 0)
        {
            int ns = st.Length / 4, first = m.VertexCount, fi = m.IndexCount;
            for (int i = 0; i < ns; i++)
            {
                int a = Math.Max(0, i - 1), c = Math.Min(ns - 1, i + 1);
                double ty = st[4 * c] - st[4 * a], tz = st[4 * c + 1] - st[4 * a + 1], tl = Math.Sqrt(ty * ty + tz * tz);
                // The in-plane normal of the path (tangent turned 90° in y-z).
                double ny = tl > 1e-9 ? -tz / tl : 0, nz = tl > 1e-9 ? ty / tl : 1;
                double y = st[4 * i], z = st[4 * i + 1], rx = st[4 * i + 2], rn = st[4 * i + 3];
                for (int j = 0; j < seg; j++)
                {
                    double t = 2 * Math.PI * j / seg, cx = Math.Cos(t), sn = Math.Sin(t);
                    SacredDraw.V(m, k, x + rx * cx, y + rn * sn * ny, z + rn * sn * nz, cx / Math.Max(1e-3, rx), sn * ny / Math.Max(1e-3, rn),
                                 sn * nz / Math.Max(1e-3, rn), b);
                }
            }
            SacredDraw.Grid(m, first, ns, seg, true, null);
            for (int e = 0; e < 2; e++)
            {
                int i = e == 0 ? 0 : ns - 1, j2 = e == 0 ? 1 : ns - 2;
                double dy = st[4 * i] - st[4 * j2], dz = st[4 * i + 1] - st[4 * j2 + 1], dl = Math.Sqrt(dy * dy + dz * dz);
                if (dl < 1e-9) dl = 1;
                int cv = SacredDraw.V(m, k, x, st[4 * i], st[4 * i + 1], 0, dy / dl, dz / dl, b);
                int ring = first + i * seg;
                for (int j = 0; j < seg; j++) SacredDraw.Tri(m, cv, ring + j, ring + (j + 1) % seg);
            }
            ShapeNoise.RecomputeNormals(m, first, m.VertexCount - first, fi, m.IndexCount - fi, true);
            return first;
        }

        /// <summary>A brush with its AO scaled.</summary>
        public static ShapeBrush Ao(in ShapeBrush b, float ao)
        {
            return new ShapeBrush(b.Color, b.Channel, b.Ao * ao);
        }

        /// <summary>A moulded rectangular band (plinth, cornice, coping) round the local rectangle of half sizes (hu, hw)
        /// centred on (cu, cw): the profile's offsets and heights as pairs, the top capped when asked.</summary>
        public static void Band(MeshData m, in Affine3 k, double cu, double cw, double hu, double hw, double[] offH, in ShapeBrush b, bool capTop,
                                in ShapeBrush top)
        {
            Mould p = SacredDraw.M;
            for (int i = 0; i < offH.Length / 2; i++) p.Add(offH[2 * i], offH[2 * i + 1], b);
            SacredDraw.RectRing(m, k, cu, cw, hu, hw, p, capTop, top);
        }

        /// <summary>A rectangular box with flat faces (no bottom) in local coordinates.</summary>
        public static void Block(MeshData m, in Affine3 k, double u0, double u1, double v0, double v1, double w0, double w1, in ShapeBrush b)
        {
            SacredDraw.Box(m, k, u0, u1, v0, v1, w0, w1, b, BoxFaces.All & ~BoxFaces.Bottom);
        }
    }
}
