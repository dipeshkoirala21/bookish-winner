using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// The Boudhanath site palette (docs/research/heritage/boudha.md, colours as measured on the 2016-2024 photos) and the
    /// shared drawing helpers of the bespoke Boudha models: cross-on-square terrace outlines, polygon offsets, the white
    /// bell chortens and wall finials, prayer-flag runs, striped cloth valances, flights of steps with their walkable
    /// ramps, gilt grid lines and the Tibetan window. Every helper draws in a local frame given as an
    /// <see cref="Affine3"/> (x across, y up, z out) through <see cref="SacredDraw"/>, so the vertices carry the material
    /// channel and AO of the detail pass. Engine-free; per-thread scratch only.
    /// </summary>
    internal static class BoudhaKit
    {
        // ------------------------------------------------------------------------------------------------ palette

        public static readonly uint LimeWhite = MeshColor.FromHex(0xEEEAE0);
        public static readonly uint DomeWhite = MeshColor.FromHex(0xF4F0E6);
        public static readonly uint Weathered = MeshColor.FromHex(0xD9D4C8);
        public static readonly uint TerraceFloor = MeshColor.FromHex(0xC39A86);
        public static readonly uint SaffronArc = MeshColor.FromHex(0xE3A73A);
        public static readonly uint YellowWash = MeshColor.FromHex(0xF2D27A);
        public static readonly uint GiltPanel = MeshColor.FromHex(0xC9A050);
        public static readonly uint GiltLit = MeshColor.FromHex(0xE0BC63);
        public static readonly uint GiltBright = MeshColor.FromHex(0xE2B54A);
        public static readonly uint GiltShade = MeshColor.FromHex(0xA87C2E);
        public static readonly uint ClothRed = MeshColor.FromHex(0xC8202A);
        public static readonly uint ClothYellow = MeshColor.FromHex(0xF2C230);
        public static readonly uint ClothBlue = MeshColor.FromHex(0x2E5DA8);
        public static readonly uint ClothGreen = MeshColor.FromHex(0x2F6A3A);
        public static readonly uint ClothWhite = MeshColor.FromHex(0xF4F2EC);
        public static readonly uint ClothSaffron = MeshColor.FromHex(0xF0A01E);
        public static readonly uint NicheValance = MeshColor.FromHex(0xE8B530);
        public static readonly uint Recess = MeshColor.FromHex(0x2B2620);
        public static readonly uint NicheDark = MeshColor.FromHex(0x3A2E28);
        public static readonly uint Copper = MeshColor.FromHex(0xB0743A);
        public static readonly uint Brass = MeshColor.FromHex(0xC9A24A);
        public static readonly uint StoneFlag = MeshColor.FromHex(0x8C8A86);
        public static readonly uint StoneKerb = MeshColor.FromHex(0x6E6A64);
        public static readonly uint PlazaBrick = MeshColor.FromHex(0x9C5A40);
        public static readonly uint Terracotta = MeshColor.FromHex(0xB5603E);
        public static readonly uint Leaf = MeshColor.FromHex(0x3F7A34);
        public static readonly uint LeafLight = MeshColor.FromHex(0x5E9A3E);
        public static readonly uint Maroon = MeshColor.FromHex(0x7A1F1F);
        public static readonly uint MonkMaroon = MeshColor.FromHex(0x9E2430);
        public static readonly uint Ochre = MeshColor.FromHex(0xE3B84E);
        public static readonly uint OchreDeep = MeshColor.FromHex(0xE2B73F);
        public static readonly uint LatticeDark = MeshColor.FromHex(0x2A2018);
        public static readonly uint Teal = MeshColor.FromHex(0x2E4A4E);
        public static readonly uint TibetanYellow = MeshColor.FromHex(0xE3CC3A);
        public static readonly uint EyeBlueDark = MeshColor.FromHex(0x1F4E9A);
        public static readonly uint EyeBlueLight = MeshColor.FromHex(0x73B9E6);
        public static readonly uint EyeRed = MeshColor.FromHex(0xD8402A);
        public static readonly uint Brow = MeshColor.FromHex(0x1B2140);
        public static readonly uint Ink = MeshColor.FromHex(0x111111);

        public static ShapeBrush B(uint c, MaterialChannel ch, float ao = 1f)
        {
            return new ShapeBrush(c, ch, ao);
        }

        public static readonly ShapeBrush White = new ShapeBrush(MeshColor.FromHex(0xF2EEE6), MaterialChannel.Plaster);
        public static readonly ShapeBrush WhiteShade = new ShapeBrush(MeshColor.FromHex(0xE4DED2), MaterialChannel.Plaster);
        public static readonly ShapeBrush Gilt = new ShapeBrush(MeshColor.FromHex(0xE2B54A), MaterialChannel.Gilt);
        public static readonly ShapeBrush GiltAged = new ShapeBrush(MeshColor.FromHex(0xC9A050), MaterialChannel.Gilt);
        public static readonly ShapeBrush GiltDark = new ShapeBrush(MeshColor.FromHex(0x8A6420), MaterialChannel.Gilt, 0.8f);
        public static readonly ShapeBrush DarkRecess = new ShapeBrush(MeshColor.FromHex(0x2B2620), MaterialChannel.Plain, 0.55f);

        // ------------------------------------------------------------------------------------------------ scratch

        [ThreadStatic] private static double[] _u, _v, _u2, _v2, _nu, _nv;
        [ThreadStatic] private static Profile2 _prof;

        /// <summary>Per-thread outline scratch (64 points each), cleared by the caller's use.</summary>
        public static double[] U
        {
            get { return _u ?? (_u = new double[256]); }
        }

        public static double[] V
        {
            get { return _v ?? (_v = new double[256]); }
        }

        public static double[] U2
        {
            get { return _u2 ?? (_u2 = new double[256]); }
        }

        public static double[] V2
        {
            get { return _v2 ?? (_v2 = new double[256]); }
        }

        /// <summary>A per-thread lathe profile (cleared, open).</summary>
        public static Profile2 Prof
        {
            get { return (_prof ?? (_prof = new Profile2(64))).Clear(false); }
        }

        // ------------------------------------------------------------------------------------------------ outlines

        /// <summary>
        /// A cross on a square (the mandala plan of the Boudha terraces, 20 corners, counter-clockwise in local (u, v)):
        /// the body spans u0..u1 × v0..v1; the north and south arms are <paramref name="nsHalf"/> wide either side of
        /// <paramref name="nsCu"/> and end at <paramref name="vN"/> and <paramref name="vS"/>; the east and west arms are
        /// <paramref name="ewHalf"/> either side of <paramref name="ewCv"/> and end at <paramref name="uE"/> and
        /// <paramref name="uW"/>. Returns 20.
        /// </summary>
        public static int CrossSquare(double u0, double u1, double v0, double v1, double nsCu, double nsHalf, double vN, double vS, double ewCv,
                                      double ewHalf, double uE, double uW, double[] pu, double[] pv)
        {
            int n = 0;
            // Start at the south-east body corner and go counter-clockwise (east side upward).
            Put(pu, pv, ref n, u1, v0);
            Put(pu, pv, ref n, u1, ewCv - ewHalf);
            Put(pu, pv, ref n, uE, ewCv - ewHalf);
            Put(pu, pv, ref n, uE, ewCv + ewHalf);
            Put(pu, pv, ref n, u1, ewCv + ewHalf);
            Put(pu, pv, ref n, u1, v1);
            Put(pu, pv, ref n, nsCu + nsHalf, v1);
            Put(pu, pv, ref n, nsCu + nsHalf, vN);
            Put(pu, pv, ref n, nsCu - nsHalf, vN);
            Put(pu, pv, ref n, nsCu - nsHalf, v1);
            Put(pu, pv, ref n, u0, v1);
            Put(pu, pv, ref n, u0, ewCv + ewHalf);
            Put(pu, pv, ref n, uW, ewCv + ewHalf);
            Put(pu, pv, ref n, uW, ewCv - ewHalf);
            Put(pu, pv, ref n, u0, ewCv - ewHalf);
            Put(pu, pv, ref n, u0, v0);
            Put(pu, pv, ref n, nsCu - nsHalf, v0);
            Put(pu, pv, ref n, nsCu - nsHalf, vS);
            Put(pu, pv, ref n, nsCu + nsHalf, vS);
            Put(pu, pv, ref n, nsCu + nsHalf, v0);
            return n;
        }

        private static void Put(double[] pu, double[] pv, ref int n, double u, double v)
        {
            pu[n] = u;
            pv[n] = v;
            n++;
        }

        /// <summary>Signed area (positive counter-clockwise, u right and v up).</summary>
        public static double Area(double[] pu, double[] pv, int n)
        {
            double a = 0;
            for (int i = 0, j = n - 1; i < n; j = i++) a += pu[j] * pv[i] - pu[i] * pv[j];
            return 0.5 * a;
        }

        /// <summary>Make a polygon counter-clockwise in place.</summary>
        public static void Ccw(double[] pu, double[] pv, int n)
        {
            if (Area(pu, pv, n) >= 0) return;
            Array.Reverse(pu, 0, n);
            Array.Reverse(pv, 0, n);
        }

        /// <summary>
        /// Offset a counter-clockwise polygon outward by <paramref name="d"/> (negative: inward) with mitred corners
        /// (limited to 3 × d at very sharp corners) into (ou, ov). Returns n.
        /// </summary>
        public static int Offset(double[] pu, double[] pv, int n, double d, double[] ou, double[] ov)
        {
            if (_nu == null || _nu.Length < n)
            {
                _nu = new double[Math.Max(256, n)];
                _nv = new double[Math.Max(256, n)];
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double du = pu[j] - pu[i], dv = pv[j] - pv[i], l = Math.Sqrt(du * du + dv * dv);
                if (l < 1e-9) l = 1e-9;
                // Outward of a counter-clockwise polygon: the edge turned clockwise.
                _nu[i] = dv / l;
                _nv[i] = -du / l;
            }
            for (int i = 0; i < n; i++)
            {
                int h = (i + n - 1) % n;
                double mu = _nu[h] + _nu[i], mv = _nv[h] + _nv[i], l = Math.Sqrt(mu * mu + mv * mv);
                if (l < 1e-9)
                {
                    mu = _nu[i];
                    mv = _nv[i];
                    l = 1;
                }
                mu /= l;
                mv /= l;
                double c = mu * _nu[i] + mv * _nv[i];
                double s = 1.0 / Math.Max(0.33, c);
                ou[i] = pu[i] + mu * d * s;
                ov[i] = pv[i] + mv * d * s;
            }
            return n;
        }

        /// <summary>True when (u, v) lies inside the polygon (even-odd rule).</summary>
        public static bool Inside(double[] pu, double[] pv, int n, double u, double v)
        {
            bool c = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if ((pv[i] > v) != (pv[j] > v) && u < (pu[j] - pu[i]) * (v - pv[i]) / (pv[j] - pv[i]) + pu[i]) c = !c;
            }
            return c;
        }

        /// <summary>Distance from (u, v) to the polygon's boundary.</summary>
        public static double EdgeDistance(double[] pu, double[] pv, int n, double u, double v)
        {
            double best = double.MaxValue;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double ex = pu[i] - pu[j], ey = pv[i] - pv[j], l2 = ex * ex + ey * ey;
                double t = l2 > 1e-12 ? Math.Max(0, Math.Min(1, ((u - pu[j]) * ex + (v - pv[j]) * ey) / l2)) : 0;
                double qx = pu[j] + t * ex - u, qy = pv[j] + t * ey - v, d = qx * qx + qy * qy;
                if (d < best) best = d;
            }
            return Math.Sqrt(best);
        }

        /// <summary>Perimeter of a closed polygon.</summary>
        public static double Perimeter(double[] pu, double[] pv, int n)
        {
            double p = 0;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double du = pu[i] - pu[j], dv = pv[i] - pv[j];
                p += Math.Sqrt(du * du + dv * dv);
            }
            return p;
        }

        /// <summary>The point at arc length <paramref name="s"/> along a closed polygon (from vertex 0) and the unit
        /// direction of the edge there.</summary>
        public static void AtLength(double[] pu, double[] pv, int n, double s, out double u, out double v, out double tu, out double tv)
        {
            double per = Perimeter(pu, pv, n);
            s %= per;
            if (s < 0) s += per;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double du = pu[j] - pu[i], dv = pv[j] - pv[i], l = Math.Sqrt(du * du + dv * dv);
                if (s <= l || i == n - 1)
                {
                    double t = l > 1e-9 ? Math.Min(1, s / l) : 0;
                    u = pu[i] + du * t;
                    v = pv[i] + dv * t;
                    tu = l > 1e-9 ? du / l : 1;
                    tv = l > 1e-9 ? dv / l : 0;
                    return;
                }
                s -= l;
            }
            u = pu[0];
            v = pv[0];
            tu = 1;
            tv = 0;
        }

        /// <summary>A local frame at (u, y, v) of <paramref name="k"/> whose +z points along (du, dv) on the ground.</summary>
        public static Affine3 Facing(in Affine3 k, double u, double y, double v, double du, double dv)
        {
            double l = Math.Sqrt(du * du + dv * dv);
            if (l < 1e-12)
            {
                du = 0;
                dv = 1;
                l = 1;
            }
            du /= l;
            dv /= l;
            // x = z turned clockwise seen from above (right-hand of a viewer looking along -z at the object).
            return SacredDraw.Basis(k, u, y, v, dv, 0, -du, 0, 1, 0);
        }

        // ------------------------------------------------------------------------------------------------ chortens

        /// <summary>
        /// A white bell chorten (Tibetan style, the big ones on the Boudha terraces): a three-step square base, a round
        /// lotus collar, the bell-shaped dome (bumpa), a small square harmika with a cornice, a short gilt-tipped spire
        /// of rings and a finial; <paramref name="h"/> is the total height. Standing on y = 0 of <paramref name="f"/>.
        /// </summary>
        public static void BellChorten(MeshData m, in Affine3 f, double h, int lod, bool giltSpire)
        {
            double s = h / 6.0; // a 6 m chorten: base 3.6 m wide
            ShapeBrush w = White, sh = WhiteShade;
            double y = 0;
            double[] steps = { 1.8, 1.55, 1.32 };
            double[] rises = { 0.55, 0.45, 0.4 };
            for (int i = 0; i < 3; i++)
            {
                double hw = steps[i] * s, rh = rises[i] * s;
                Mould mo = SacredDraw.M;
                if (lod >= 2) mo.Add(0, y - (i == 0 ? 0.2 : 0), i == 0 ? sh : w).Add(0, y + rh, w);
                else mo.Add(0, y - (i == 0 ? 0.2 : 0), i == 0 ? sh : w).Add(0, y + rh - 0.08 * s).Add(0.05 * s, y + rh - 0.06 * s, w).Add(0.05 * s, y + rh);
                SacredDraw.RectRing(m, f, 0, 0, hw, hw, mo, true, w);
                y += rh;
            }
            int seg = lod == 0 ? 12 : lod == 1 ? 8 : 6;
            Profile2 p = Prof;
            double r0 = 1.12 * s;
            // Lotus collar, then the bell body flaring out and closing up into the shoulder (fewer stations far off).
            if (lod >= 2)
                p.Add(0, y).Add(r0, y, true).Add(r0, y + 0.3 * s, true).Add(0.86 * r0, y + 1.1 * s).Add(0.55 * r0, y + 1.74 * s).Add(0, y + 1.9 * s);
            else if (lod == 1)
                p.Add(0, y).Add(r0, y, true).Add(r0, y + 0.22 * s, true).Add(0.98 * r0, y + 0.42 * s).Add(0.86 * r0, y + 1.1 * s).Add(0.62 * r0, y + 1.7 * s)
                 .Add(0, y + 1.9 * s);
            else
                p.Add(0, y).Add(r0, y, true).Add(r0, y + 0.18 * s).Add(0.98 * r0, y + 0.22 * s, true).Add(1.04 * r0, y + 0.3 * s)
                 .Add(0.98 * r0, y + 0.42 * s).Add(0.9 * r0, y + 0.7 * s).Add(0.86 * r0, y + 1.1 * s).Add(0.8 * r0, y + 1.42 * s).Add(0.62 * r0, y + 1.7 * s)
                 .Add(0.36 * r0, y + 1.86 * s).Add(0, y + 1.9 * s);
                        Shapes.Lathe(m, f, w, p, seg);
            y += 1.84 * s;
            // Harmika: a small square box with a cornice.
            double hh = 0.42 * s, hwid = 0.42 * s;
            Mould hm = SacredDraw.M;
            if (lod >= 2) hm.Add(0, y, w).Add(0, y + hh + 0.1 * s);
            else hm.Add(0, y, w).Add(0, y + hh).Add(0.08 * s, y + hh).Add(0.08 * s, y + hh + 0.1 * s);
            SacredDraw.RectRing(m, f, 0, 0, hwid, hwid, hm, true, w);
            y += hh + 0.1 * s;
            // Spire of 13 rings (a tapering, ringed cone) and the finial.
            ShapeBrush sp = giltSpire ? Gilt : w;
            double sh0 = 1.35 * s;
            if (lod == 0)
            {
                Profile2 q = Prof;
                int rings = 4;
                double rb = 0.32 * s, rt = 0.14 * s;
                q.Add(0, y);
                for (int i = 0; i < rings; i++)
                {
                    double t0 = (double)i / rings, t1 = (i + 0.85) / rings, rr = rb + (rt - rb) * t0, rr1 = rb + (rt - rb) * t1;
                    q.Add(rr, y + sh0 * t0, true).Add(rr1 * 1.08, y + sh0 * (t0 + 0.5 / rings)).Add(rr1 * 0.9, y + sh0 * t1, true);
                }
                q.Add(0.0, y + sh0);
                Shapes.Lathe(m, f, sp, q, 6);
            }
            else
            {
                Shapes.Cone(m, SacredDraw.At(f, 0, y, 0), sp, 0.32 * s, sh0, lod == 1 ? 6 : 5, 0, 0, false);
            }
            y += sh0;
            // Parasol disc, a moon and sun and the gilt tip.
            if (lod <= 1) Shapes.Cylinder(m, SacredDraw.At(f, 0, y, 0), Gilt, 0.26 * s, 0.07 * s, lod == 0 ? 10 : 6, 0, 0, true, true);
            if (lod == 0) Shapes.Sphere(m, SacredDraw.At(f, 0, y + 0.22 * s, 0), Gilt, 0.1 * s, 6);
            Shapes.Cone(m, SacredDraw.At(f, 0, y + 0.3 * s, 0), Gilt, 0.06 * s, 0.35 * s, 5, 0, 0, false);
        }

        /// <summary>A small white chorten finial (0.6-0.8 m) on a wall or parapet top: a stepped base, a bulb and a
        /// spike; <paramref name="h"/> is the total height.</summary>
        public static void FinialChorten(MeshData m, in Affine3 f, double h, int lod)
        {
            // Square in plan (a four-sided lathe turned 45°): a plinth block, the bulb and the spike; about 40 triangles,
            // since there are hundreds of them on the terrace parapet and the compound wall.
            ShapeBrush w = White;
            Profile2 p = Prof;
            double r = 0.3 * h;
            // A step, the round bulb and a thin spike (corner/02: small white chortens, not cones).
            if (lod >= 1)
                p.Add(0, 0).Add(r, 0, true).Add(r, 0.2 * h, true).Add(0.92 * r, 0.42 * h).Add(0.3 * r, 0.64 * h, true).Add(0.05 * r, h, true).Add(0, h);
            else
                p.Add(0, 0).Add(r, 0, true).Add(r, 0.17 * h, true).Add(0.95 * r, 0.4 * h).Add(0.62 * r, 0.58 * h).Add(0.12 * r, 0.66 * h, true)
                 .Add(0.05 * r, h, true).Add(0, h);
            Shapes.Lathe(m, f, w, p, 4, default, 45);
        }

        // ------------------------------------------------------------------------------------------------ cloth

        /// <summary>
        /// A hanging striped cloth valance along the local x axis from x0 to x1 in the plane z = <paramref name="z"/>
        /// (facing +z), from <paramref name="yTop"/> down: thin stripes of <paramref name="stripes"/> (each
        /// <paramref name="stripeH"/> tall), then a deep drape of <paramref name="drape"/> colour
        /// <paramref name="drapeH"/> tall, pleated (a soft wave every <paramref name="pleat"/> metres) with a scalloped
        /// hem. Returns the y of the hem.
        /// </summary>
        public static double Valance(MeshData m, in Affine3 f, double x0, double x1, double z, double yTop, uint[] stripes, double stripeH, uint drape,
                                     double drapeH, double pleat, int lod)
        {
            double y = yTop;
            for (int i = 0; i < stripes.Length; i++)
            {
                SacredDraw.Quad(m, f, x0, y - stripeH, z + 0.01 * i, x1, y - stripeH, z + 0.01 * i, x1, y, z + 0.01 * i, x0, y, z + 0.01 * i, 0, 0, 1,
                                B(stripes[i], MaterialChannel.Fabric));
                y -= stripeH;
            }
            if (drapeH <= 0) return y;
            ShapeBrush b = B(drape, MaterialChannel.Fabric);
            double len = x1 - x0;
            int cols = lod == 0 ? Math.Max(4, (int)(len / (0.5 * pleat))) : Math.Max(2, (int)(len / (2 * pleat)));
            int rows = lod == 0 ? 3 : 2;
            int first = m.VertexCount;
            double amp = lod == 0 ? 0.06 : 0.0;
            for (int r = 0; r <= rows; r++)
            {
                double t = (double)r / rows;
                for (int q = 0; q <= cols; q++)
                {
                    double xq = x0 + len * q / cols;
                    // Pleats: alternate columns stand out a little more the lower they hang; the hem scallops.
                    double depth = (q % 2 == 0 ? 1 : -1) * amp * t;
                    double hem = r == rows ? (q % 2 == 0 ? 0 : 0.12 * drapeH) : 0;
                    SacredDraw.V(m, f, xq, y - drapeH * t + hem, z + 0.03 + depth + 0.04 * t, 0, -0.1 * t, 1, b);
                }
            }
            SacredDraw.Grid(m, first, rows + 1, cols + 1, false, null);
            return y - drapeH;
        }

        // ------------------------------------------------------------------------------------------------ flags

        /// <summary>A prayer-flag line in the local frame (see <see cref="StupaGenerator.FlagLine"/>), with Boudha's
        /// flag size and pitch; returns the flags.</summary>
        public static int FlagLine(MeshData m, in Affine3 k, double u0, double y0, double v0, double u1, double y1, double v1, double sagFrac, int lod,
                                   double start)
        {
            double du = u1 - u0, dv = v1 - v0, span = Math.Sqrt(du * du + dv * dv);
            double fw = lod == 0 ? 0.6 : 1.0, fh = 0.45, pitch = lod == 0 ? 1.05 : 2.2;
            return StupaGenerator.FlagLine(m, k, u0, y0, v0, u1, y1, v1, sagFrac * span, fw, fh, pitch, true, start, lod == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------------------------------------ steps

        /// <summary>
        /// A straight flight of steps in local frame <paramref name="f"/> (x across, z along the climb): its foot at z = 0
        /// on y = <paramref name="y0"/>, its head at z = <paramref name="run"/> on y = <paramref name="y1"/>,
        /// <paramref name="halfW"/> either side of x = 0, with white cheek walls of <paramref name="cheekW"/> (0 = none)
        /// and a walkable ramp collider (tile-local, absolute heights through <paramref name="site"/> on
        /// <paramref name="ground"/>). Returns the steps.
        /// </summary>
        public static int Flight(MeshData m, GenColliders c, in Affine3 f, double halfW, double run, double y0, double y1, double cheekW, double cheekH,
                                 in ShapeBrush tread, in ShapeBrush cheek, int lod)
        {
            double rise = y1 - y0;
            if (rise <= 0.01 || run <= 0.01) return 0;
            int steps = Math.Max(1, (int)Math.Ceiling(rise / 0.18));
            double tr = run / steps, sr = rise / steps;
            if (lod >= 2)
            {
                SacredDraw.Quad(m, f, -halfW, y0, 0, halfW, y0, 0, halfW, y1, run, -halfW, y1, run, 0, run, -rise, tread);
            }
            else
            {
                ShapeBrush riser = Looks.Ao(tread, 0.9f);
                for (int i = 0; i < steps; i++)
                {
                    double top = y0 + (i + 1) * sr, za = i * tr, zb = (i + 1) * tr;
                    SacredDraw.Box(m, f, -halfW, halfW, top - sr, top, za, zb, riser, BoxFaces.Back);
                    SacredDraw.Box(m, f, -halfW, halfW, top - sr, top, za, zb, tread, BoxFaces.Top);
                    if (cheekW <= 0) SacredDraw.Box(m, f, -halfW, halfW, y0, top, za, zb, tread, BoxFaces.Left | BoxFaces.Right);
                }
            }
            if (cheekW > 0)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    double xi = s * halfW, xo = s * (halfW + cheekW), hb = y0 + cheekH, ht = y1 + cheekH;
                    SacredDraw.Quad(m, f, xo, y0 - 0.2, 0, xo, hb, 0, xo, ht, run, xo, y0 - 0.2, run, s, 0, 0, cheek);
                    SacredDraw.Quad(m, f, xi, y0, 0, xi, hb, 0, xi, ht, run, xi, y0, run, -s, 0, 0, Looks.Ao(cheek, 0.85f));
                    SacredDraw.Quad(m, f, xi, hb, 0, xo, hb, 0, xo, ht, run, xi, ht, run, 0, run, -rise, cheek);
                    SacredDraw.Quad(m, f, xi, y0 - 0.2, 0, xo, y0 - 0.2, 0, xo, hb, 0, xi, hb, 0, 0, 0, -1, cheek);
                }
            }
            if (c != null)
            {
                double x0, yy0, z0, x1, yy1, z1;
                f.Point(0, y0, 0, out x0, out yy0, out z0);
                f.Point(0, y1, run, out x1, out yy1, out z1);
                c.AddRamp(new GenRamp { X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Y0 = (float)yy0, Y1 = (float)yy1, HalfWidth = (float)halfW, Material = GenColliders.Stone });
            }
            return steps;
        }

        /// <summary>A handrail of round tube on posts along a sloping line in frame <paramref name="f"/>: from (x, y0 + h,
        /// 0) to (x, y1 + h, run).</summary>
        public static void Handrail(MeshData m, in Affine3 f, double x, double run, double y0, double y1, double h, in ShapeBrush b, int lod)
        {
            int seg = lod == 0 ? 6 : 4;
            Shapes.Bar(m, f, b, x, y0 + h, -0.1, x, y1 + h, run + 0.1, 0.035, seg);
            int posts = Math.Max(2, (int)(run / 1.6) + 1);
            for (int i = 0; i < posts; i++)
            {
                double t = (double)i / (posts - 1), z = run * t, y = y0 + (y1 - y0) * t;
                Shapes.Bar(m, f, b, x, y, z, x, y + h, z, 0.03, seg);
            }
        }

        // ------------------------------------------------------------------------------------------------ surfaces

        /// <summary>Thin raised lines (a grid of plates) on a face in plane z = <paramref name="z"/> of frame
        /// <paramref name="f"/>: verticals every <paramref name="pitchX"/> from x0 to x1 and horizontals every
        /// <paramref name="pitchY"/> from y0 to y1, each <paramref name="w"/> wide.</summary>
        public static void GridLines(MeshData m, in Affine3 f, double x0, double x1, double y0, double y1, double z, double pitchX, double pitchY, double w,
                                     in ShapeBrush b)
        {
            int nx = Math.Max(1, (int)Math.Round((x1 - x0) / pitchX));
            for (int i = 1; i < nx; i++)
            {
                double x = x0 + (x1 - x0) * i / nx;
                SacredDraw.Quad(m, f, x - 0.5 * w, y0, z, x + 0.5 * w, y0, z, x + 0.5 * w, y1, z, x - 0.5 * w, y1, z, 0, 0, 1, b);
            }
            if (pitchY <= 0) return;
            int ny = Math.Max(1, (int)Math.Round((y1 - y0) / pitchY));
            for (int i = 1; i < ny; i++)
            {
                double y = y0 + (y1 - y0) * i / ny;
                SacredDraw.Quad(m, f, x0, y - 0.5 * w, z, x1, y - 0.5 * w, z, x1, y + 0.5 * w, z, x0, y + 0.5 * w, z, 0, 0, 1, b);
            }
        }

        /// <summary>A flat polygon (local u, v; counter-clockwise; ear-clipped by the shapes library) at height y facing up,
        /// slightly bevelled (a 0.04 m slab).</summary>
        public static void Floor(MeshData m, in Affine3 k, double[] pu, double[] pv, int n, double y, double thick, in ShapeBrush b)
        {
            Shapes.BevelExtrude(m, SacredDraw.At(k, 0, y - thick, 0), b, pu, pv, n, thick, 0, 1, BevelStyle.Chamfer, true, false, 0, 30);
        }

        /// <summary>A Tibetan window on a wall plane z = <paramref name="z"/> (facing +z): a black trapezoid surround
        /// wider at the bottom, a red frame with a cross of muntins, a dark pane and a small frilled awning (cloth valance)
        /// above. Centre (x, y) is the window centre; w × h the opening.</summary>
        public static void TibetanWindow(MeshData m, in Affine3 f, double x, double y, double z, double w, double h, uint frame, int lod)
        {
            ShapeBrush black = B(MeshColor.FromHex(0x1A1714), MaterialChannel.Paint), fr = B(frame, MaterialChannel.Wood), pane = B(MeshColor.FromHex(0x23282E), MaterialChannel.Glass, 0.8f);
            double sw = 0.18 * w;
            // Trapezoid surround (wider at the bottom).
            SacredDraw.Quad(m, f, x - 0.5 * w - 1.4 * sw, y - 0.5 * h - 0.12, z + 0.005, x + 0.5 * w + 1.4 * sw, y - 0.5 * h - 0.12, z + 0.005,
                            x + 0.5 * w + 0.7 * sw, y + 0.5 * h + 0.06, z + 0.005, x - 0.5 * w - 0.7 * sw, y + 0.5 * h + 0.06, z + 0.005, 0, 0, 1, black);
            SacredDraw.Box(m, f, x - 0.5 * w - 0.06, x + 0.5 * w + 0.06, y - 0.5 * h - 0.06, y + 0.5 * h + 0.06, z + 0.005, z + 0.06, fr, BoxFaces.Front);
            SacredDraw.Quad(m, f, x - 0.5 * w, y - 0.5 * h, z + 0.065, x + 0.5 * w, y - 0.5 * h, z + 0.065, x + 0.5 * w, y + 0.5 * h, z + 0.065, x - 0.5 * w,
                            y + 0.5 * h, z + 0.065, 0, 0, 1, pane);
            if (lod == 0)
            {
                SacredDraw.Box(m, f, x - 0.025, x + 0.025, y - 0.5 * h, y + 0.5 * h, z + 0.065, z + 0.09, fr, BoxFaces.Front);
                SacredDraw.Box(m, f, x - 0.5 * w, x + 0.5 * w, y + 0.12 * h - 0.025, y + 0.12 * h + 0.025, z + 0.065, z + 0.09, fr, BoxFaces.Front);
            }
            // The frilled awning (shembu): a short cloth over the window top, tilted out.
            SacredDraw.Quad(m, f, x - 0.5 * w - 0.15, y + 0.5 * h + 0.1, z + 0.32, x + 0.5 * w + 0.15, y + 0.5 * h + 0.1, z + 0.32,
                            x + 0.5 * w + 0.15, y + 0.5 * h + 0.32, z + 0.02, x - 0.5 * w - 0.15, y + 0.5 * h + 0.32, z + 0.02, 0, 0.8, 0.6,
                            B(lod == 0 ? ClothWhite : ClothRed, MaterialChannel.Fabric));
            if (lod == 0)
            {
                SacredDraw.Quad(m, f, x - 0.5 * w - 0.15, y + 0.5 * h - 0.02, z + 0.33, x + 0.5 * w + 0.15, y + 0.5 * h - 0.02, z + 0.33,
                                x + 0.5 * w + 0.15, y + 0.5 * h + 0.1, z + 0.33, x - 0.5 * w - 0.15, y + 0.5 * h + 0.1, z + 0.33, 0, 0, 1, B(ClothRed, MaterialChannel.Fabric));
            }
        }
    }
}
