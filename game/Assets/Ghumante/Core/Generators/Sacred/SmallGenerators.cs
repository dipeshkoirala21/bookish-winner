using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>A Newar votive chaitya (W2_DESIGN 3.2; temples.md 2.4): 0.8-3 m tall, 0.6-2 m square.</summary>
    public struct ChaityaParams
    {
        public float HeightM, BaseW;
        public int Steps;

        public static ChaityaParams Defaults(float height)
        {
            return new ChaityaParams { HeightM = height, BaseW = Math.Max(0.6f, Math.Min(2.0f, 0.6f * height)), Steps = 3 };
        }
    }

    /// <summary>
    /// The chaitya generator (ref_temples 2.3): a moulded stepped stone base, a drum with four arched niches holding the
    /// four directional Buddhas in their fixed compass order (E Akshobhya, S Ratnasambhava, W Amitabha, N Amoghasiddhi;
    /// Vairocana at the centre unseen) with sindoor at their feet, a smooth dome, the harmika, a cone of 13 rings and a
    /// gilt parasol knob, in grey stone. The niches face the real compass directions whatever the frame's yaw.
    /// ≤ 600 triangles at LOD0.
    /// </summary>
    public static class ChaityaGenerator
    {
        /// <summary>Niche order: east, south, west, north (W2_DESIGN 3.2).</summary>
        public static readonly string[] NicheOrder = { "Akshobhya", "Ratnasambhava", "Amitabha", "Amoghasiddhi" };

        /// <summary>Bearings (degrees clockwise from north) of the niches in <see cref="NicheOrder"/>.</summary>
        public static readonly float[] NicheBearing = { 90f, 180f, 270f, 0f };

        [ThreadStatic] private static Profile2 _prof;

        private static Profile2 Prof
        {
            get { return (_prof ?? (_prof = new Profile2(48))).Clear(false); }
        }

        public static int Build(in ChaityaParams p, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            return Build(p, f, lod, m, c, null);
        }

        public static int Build(in ChaityaParams p, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            double h = Math.Max(0.5, p.HeightM), bw = Math.Max(0.4, p.BaseW);
            int steps = Math.Max(1, Math.Min(4, p.Steps));
            var northF = KitFrame.FromYaw(f.X, f.GroundY, f.Z, 0);
            Affine3 north = SacredDraw.Xf(northF);
            ShapeBrush stone = Looks.Stone, dark = Looks.Of(MeshColor.FromHex(0x3A3630), MaterialChannel.Stone, 0.6f);
            double y = 0, stepH = 0.08 * h;
            for (int s = 0; s < steps; s++)
            {
                double half = 0.5 * bw * (1 - 0.1 * s);
                Mould mo = SacredDraw.M;
                if (lod >= 2) mo.Add(0, s == 0 ? -0.2 : y, stone).Add(0, y + stepH);
                else mo.Add(0, s == 0 ? -0.2 : y, stone).Add(0, y + 0.7 * stepH).Add(0.03, y + 0.7 * stepH, Looks.StoneLight).Add(0.03, y + stepH);
                double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
                int n = SacredDraw.Rect(0, 0, half, half, pu, pw);
                SacredDraw.Ring(m, north, pu, pw, n, mo);
                SacredDraw.CapRect(m, north, 0, 0, half + (lod >= 2 ? 0 : 0.03), half + (lod >= 2 ? 0 : 0.03), y + stepH, true, stone);
                y += stepH;
            }
            if (lod >= 3)
            {
                Shapes.Cone(m, SacredDraw.At(north, 0, y, 0), stone, 0.3 * bw, h - y, 4, 0, 0, false);
                SacredDraw.Bake(m, v0, i0, f.GroundY);
                return m.TriangleCount - t0;
            }
            double drumHalf = 0.32 * bw, drumH = 0.22 * h;
            SacredDraw.Box(m, north, -drumHalf, drumHalf, y, y + drumH, -drumHalf, drumHalf, stone, BoxFaces.All & ~BoxFaces.Bottom);
            // Four arched niches with their Buddhas, in compass order.
            for (int q = 0; q < 4; q++)
            {
                Affine3 nf = SacredDraw.Xf(KitFrame.FromYaw(f.X, f.GroundY, f.Z, NicheBearing[q]));
                double nw = 0.55 * drumHalf, nTop = y + 0.72 * drumH;
                SacredDraw.Panel(m, nf, -0.5 * nw, y + 0.12 * drumH, 0.5 * nw, nTop, drumHalf + 0.005, dark);
                ShapeBrush buddha = Looks.Of(SacredPalette.NicheBuddha[q], MaterialChannel.Paint);
                if (lod <= 1)
                {
                    ShikharaGenerator.Arch(m, nf, 0, nTop, drumHalf + 0.005, nw + 0.04, 0.25 * nw, 0.035, 0.04, false, Looks.StoneLight, lod + 1);
                    // A seated figure: lap and torso with the head.
                    Shapes.Ellipsoid(m, SacredDraw.At(nf, 0, y + 0.24 * drumH, drumHalf + 0.03), buddha, 0.3 * nw, 0.12 * drumH, 0.05 * bw, 4, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(nf, 0, y + 0.47 * drumH, drumHalf + 0.03), buddha, 0.18 * nw, 0.22 * drumH, 0.04 * bw, 4, false);
                    SacredDraw.Panel(m, nf, -0.1 * drumHalf, y + 0.02 * drumH, 0.1 * drumHalf, y + 0.1 * drumH, drumHalf + 0.012, Looks.Sindoor);
                }
                else
                {
                    SacredDraw.Panel(m, nf, -0.2 * drumHalf, y + 0.2 * drumH, 0.2 * drumHalf, y + 0.6 * drumH, drumHalf + 0.012, buddha);
                }
            }
            if (stats != null) stats.Niches = 4;
            y += drumH;
            // Dome, harmika, 13 rings, parasol knob: one lathe up from the drum top.
            double domeR = 0.3 * bw, domeH = 0.16 * h, hh = 0.07 * h, hw = 0.12 * bw;
            Profile2 pr = Prof;
            pr.Add(0, y).Add(domeR, y, true);
            int rows = lod == 0 ? 4 : 2;
            for (int q = 1; q <= rows; q++)
            {
                double a = 0.5 * Math.PI * q / rows;
                pr.Add(domeR * Math.Cos(a) + (q == rows ? hw * 0.9 : 0), y + domeH * Math.Sin(a), q == rows);
            }
            double yc = y + domeH;
            Shapes.Lathe(m, north, stone, pr, lod == 0 ? 10 : 6);
            SacredDraw.Box(m, north, -hw, hw, yc - 0.02, yc + hh, -hw, hw, stone, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, north, -hw - 0.02, hw + 0.02, yc + hh, yc + hh + 0.025, -hw - 0.02, hw + 0.02, Looks.StoneLight, BoxFaces.All & ~BoxFaces.Bottom);
            double y2 = yc + hh + 0.025, coneH = Math.Max(0.1, h - y2 - 0.06 * h), rb = 0.11 * bw, rt = 0.025 * bw;
            Profile2 rp = Prof;
            rp.Add(0, y2).Add(rb, y2, true);
            int bands = lod == 0 ? 7 : 3;
            for (int q = 0; q < bands; q++)
            {
                double ra = rb + (rt - rb) * q / bands, rbb = rb + (rt - rb) * (q + 1) / bands, yy = y2 + coneH * q / bands, st = coneH / bands;
                rp.Add(ra * 1.05, yy + 0.4 * st).Add(rbb * 0.9, yy + st, true);
            }
            rp.Add(0, y2 + coneH);
            Shapes.Lathe(m, north, stone, rp, lod == 0 ? 6 : 4);
            Shapes.Cone(m, SacredDraw.At(north, 0, h - 0.06 * h - 0.01, 0), Looks.Gilt, 0.08 * bw, 0.06 * h + 0.01, lod == 0 ? 6 : 4, 0, 0, false);
            if (c != null) c.AddBox(f.X, f.Z, f.GroundY, f.GroundY + h, 0.5 * bw, 0.5 * bw, 1, 0, GenColliderFlags.NoClimb, GenColliders.Stone);
            if (stats != null)
            {
                stats.TopM = (float)h;
                stats.Rings = 13;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }
    }

    /// <summary>Kind of a small shrine (temples.md 2.7), from the name or religion.</summary>
    public enum ShrineKind : byte
    {
        Generic = 0,
        Ganesh = 1,
        Bhairav = 2,
        Linga = 3,
        Nag = 4,
    }

    /// <summary>Form of a small shrine.</summary>
    public enum ShrineForm : byte
    {
        Niche = 0,
        MiniPagoda = 1,
        TinCanopy = 2,
    }

    /// <summary>A small shrine (corner Ganesh, Bhairav, linga, nag stone; W2_DESIGN 3.2).</summary>
    public struct ShrineParams
    {
        public ShrineKind Kind;
        public ShrineForm Form;
        public float LongSideM, ShortSideM;
        public float ImageM;
    }

    /// <summary>
    /// The shrine generator (ref_temples 4): the stone corner shrine (a platform, a stone box with a cusped arched niche
    /// and a stepped cap with a knob, two small lions), a one-tier mini pagoda over a niche, or a tin canopy on posts,
    /// each over a sindoor-smeared image (Ganesh with his trunk and a marigold garland, a linga on its yoni, a Bhairav
    /// mask, a nag stone) and a brass bell on a bracket. ≤ 1,500 triangles at LOD0.
    /// </summary>
    public static class ShrineGenerator
    {
        public static int Build(in ShrineParams p, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
            double l = Math.Max(0.8, p.LongSideM), s = Math.Max(0.8, p.ShortSideM > 0 ? p.ShortSideM : 0.8 * l);
            double img = p.ImageM > 0 ? p.ImageM : Math.Min(0.9, 0.14 * l + 0.35);
            double bellU = 0.5 * l + 0.15, bellW = 0.5 * s + 0.1, bellV = 1.7;
            switch (p.Form)
            {
                case ShrineForm.MiniPagoda:
                {
                    PagodaParams pp = PagodaParams.Defaults((float)l, (float)s, 1);
                    pp.PlinthLevels = 1;
                    pp.StepRiseM = 0.3f;
                    pp.CoreFrac = 0.6f;
                    pp.FirstEaveFrac = 1.6f;
                    pp.TotalHeightM = (float)Math.Max(2.5, Math.Min(6.0, 0.9 * l));
                    pp.Guardians = GuardianSet.None;
                    pp.BellSpacingM = 0.6f;
                    pp.Ambulatory = false;
                    PagodaGenerator.Build(pp, f, lod == 0 ? 1 : lod + 1, m, c);
                    Image(m, k, p.Kind, 0.3, 0.3 * s + 0.12, img, lod);
                    bellV = 1.5;
                    break;
                }
                case ShrineForm.TinCanopy:
                {
                    ShapeBrush post = Looks.Of(MeshColor.FromHex(0x5A5E63), MaterialChannel.Metal), tin = Looks.Of(MeshColor.FromHex(0x3D7CC9), MaterialChannel.Metal);
                    for (int q = 0; q < 4; q++)
                    {
                        double u = (q % 2 == 0 ? -1 : 1) * (0.5 * l - 0.1), w = (q < 2 ? -1 : 1) * (0.5 * s - 0.1);
                        Shapes.Cylinder(m, SacredDraw.At(k, u, 0, w), post, 0.05, 2.25, 5, 0, 0, false, false);
                    }
                    // A ribbed tin sheet sloping to the front.
                    int ribs = lod == 0 ? 8 : 3;
                    for (int q = 0; q < ribs; q++)
                    {
                        double u0 = -0.5 * l - 0.2 + (l + 0.4) * q / ribs, u1 = -0.5 * l - 0.2 + (l + 0.4) * (q + 1) / ribs, um = 0.5 * (u0 + u1);
                        SacredDraw.Quad(m, k, u0, 2.4, -0.5 * s - 0.2, um, 2.45, -0.5 * s - 0.2, um, 2.15, 0.5 * s + 0.2, u0, 2.1, 0.5 * s + 0.2, -0.3, 1, 0.2, tin);
                        SacredDraw.Quad(m, k, um, 2.45, -0.5 * s - 0.2, u1, 2.4, -0.5 * s - 0.2, u1, 2.1, 0.5 * s + 0.2, um, 2.15, 0.5 * s + 0.2, 0.3, 1, 0.2, tin);
                        SacredDraw.Quad(m, k, u0, 2.4, -0.5 * s - 0.2, u1, 2.4, -0.5 * s - 0.2, u1, 2.1, 0.5 * s + 0.2, u0, 2.1, 0.5 * s + 0.2, 0, -1, -0.2, Looks.Ao(post, 0.6f));
                    }
                    SacredParts.PlinthLevel(m, k, 0.4 * l, 0.4 * s, 0, 0.3, 0.2, Looks.Stone, Looks.StoneLight, lod);
                    Image(m, k, p.Kind, 0.3, 0, img, lod);
                    bellV = 2.0;
                    break;
                }
                default:
                    NicheShrine(m, k, l, s, img, p.Kind, lod);
                    bellU = 0.32 * l + 0.25;
                    bellW = 0.5 * s * 0.7 + 0.15;
                    break;
            }
            if (lod <= 1)
            {
                // A brass bell hung from an iron bracket by the shrine.
                Affine3 bf = SacredDraw.At(k, bellU, 0, bellW);
                Shapes.Cylinder(m, SacredDraw.At(bf, 0, bellV + 0.32, 0), Looks.Of(MeshColor.FromHex(0x2E3440), MaterialChannel.Metal), 0.015, 0.08, 4, 0, 0, false, false);
                SacredDraw.Box(m, bf, -0.25, 0.02, bellV + 0.38, bellV + 0.41, -0.015, 0.015, Looks.Of(MeshColor.FromHex(0x2E3440), MaterialChannel.Metal), BoxFaces.All);
                Profile2 bp = new Profile2(10);
                bp.Clear(false).Add(0, 0).Add(0.13, 0, true).Add(0.11, 0.06).Add(0.08, 0.22).Add(0.05, 0.3).Add(0, 0.32);
                Shapes.Lathe(m, SacredDraw.At(bf, 0, bellV, 0), Looks.Of(MeshColor.FromHex(0xC9A13A), MaterialChannel.Metal), bp, lod == 0 ? 8 : 5);
            }
            if (c != null) c.AddBox(f.X, f.Z, f.GroundY, f.GroundY + 2.0, 0.5 * l, 0.5 * s, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Stone);
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>
        /// The stone corner shrine (ref_temples 4): a low platform, a weathered stone box with a deep cusped arched niche
        /// between pilasters (the image fills most of it), a carved lintel band, an overhanging cornice and a shallow hipped
        /// stone roof with a stacked finial, and two small lions on the platform.
        /// </summary>
        private static void NicheShrine(MeshData m, in Affine3 k, double l, double s, double img, ShrineKind kind, int lod)
        {
            double bw = Math.Min(2.2, 0.62 * l + 0.3), bd = Math.Min(1.6, 0.7 * s), plat = 0.32;
            double hgt = Math.Max(1.4, Math.Min(2.4, 1.0 * bw + 0.2));
            ShapeBrush stone = Looks.Of(MeshColor.FromHex(0x7E7A71), MaterialChannel.Stone), light = Looks.Of(MeshColor.FromHex(0x9A958A), MaterialChannel.Stone);
            SacredParts.PlinthLevel(m, k, 0.5 * l, 0.5 * s, 0, plat, 0.25, stone, light, lod);
            // The box.
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(0, 0, 0.5 * bw, 0.5 * bd, pu, pw);
            Mould mo = SacredDraw.M;
            if (lod >= 2) mo.Add(0, plat, stone).Add(0, plat + hgt);
            else mo.Add(0.05, plat, light).Add(0.05, plat + 0.15).Add(0, plat + 0.15, stone).Add(0, plat + hgt - 0.3).Add(0.04, plat + hgt - 0.26, light).Add(0.04, plat + hgt);
            SacredDraw.Ring(m, k, pu, pw, n, mo);
            // The niche: a dark recess with an arched head, framed by pilasters and the cusped arch.
            double fw = 0.5 * bd + 0.008, nw = 0.58 * bw, sill = plat + 0.2, spring = plat + 0.48 * hgt, rise = 0.42 * nw;
            ShapeBrush dark = Looks.Of(MeshColor.FromHex(0x2A1E16), MaterialChannel.Plain, 0.55f);
            SacredDraw.Panel(m, k, -0.5 * nw, sill, 0.5 * nw, spring, fw, dark);
            int seg = lod == 0 ? 12 : lod == 1 ? 6 : 3;
            for (int i = 0; i < seg; i++)
            {
                double t0 = Math.PI * i / seg, t1 = Math.PI * (i + 1) / seg;
                double l0 = lod == 0 ? 1 - 0.1 * Math.Abs(Math.Sin(3 * t0)) : 1, l1 = lod == 0 ? 1 - 0.1 * Math.Abs(Math.Sin(3 * t1)) : 1;
                SacredDraw.Tri(m, k, 0, spring, fw, 0.5 * nw * Math.Cos(t0) * l0, spring + rise * Math.Sin(t0) * l0, fw,
                               0.5 * nw * Math.Cos(t1) * l1, spring + rise * Math.Sin(t1) * l1, fw, 0, 0, 1, dark);
            }
            if (lod <= 1)
            {
                ShikharaGenerator.Arch(m, k, 0, spring, fw, nw, rise, 0.08, 0.1, lod == 0, light, lod);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    SacredDraw.Box(m, k, sgn * 0.5 * nw - (sgn > 0 ? 0 : 0.1), sgn * 0.5 * nw + (sgn > 0 ? 0.1 : 0), sill, spring, fw - 0.01, fw + 0.06, light,
                                   BoxFaces.All & ~BoxFaces.Bottom & ~BoxFaces.Back);
                // The carved lintel band over the arch.
                SacredDraw.Box(m, k, -0.5 * bw + 0.08, 0.5 * bw - 0.08, plat + hgt - 0.26, plat + hgt - 0.12, fw - 0.01, fw + 0.05, stone, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            }
            double imgN = Math.Min(1.3, Math.Max(img, 0.82 * (spring + 0.5 * rise - sill)));
            Image(m, k, kind, sill, fw + 0.02 + 0.12 * imgN, imgN, lod);
            // Overhanging cornice and a shallow hipped stone roof with a stacked finial.
            double cap = plat + hgt, ou = 0.5 * bw + 0.14, ow = 0.5 * bd + 0.14, cv = cap + 0.1;
            SacredDraw.Box(m, k, -ou, ou, cap, cv, -ow, ow, light, BoxFaces.All & ~BoxFaces.Top);
            double ru = 0.12, rw = 0.12, apex = cv + Math.Max(0.35, 0.5 * Math.Min(ou, ow));
            double mu = ru + 0.42 * (ou - ru), mw = rw + 0.42 * (ow - rw), mv = cv + 0.38 * (apex - cv);
            if (lod >= 2) { mu = 0.5 * (ou + ru); mw = 0.5 * (ow + rw); mv = 0.5 * (cv + apex); }
            for (int q = 0; q < 4; q++)
            {
                // Two-segment hip (concave like the photographed roofs): eave → mid ring → top square.
                double su = q == 1 ? 1 : q == 3 ? -1 : 0, sw = q == 0 ? 1 : q == 2 ? -1 : 0;
                double au, aw, bu, bww, am, aw2, bm, bw2, tu0, tw0, tu1, tw1;
                if (sw != 0) { au = -ou; bu = ou; aw = bww = sw * ow; am = -mu; bm = mu; aw2 = bw2 = sw * mw; tu0 = -ru; tu1 = ru; tw0 = tw1 = sw * rw; }
                else { aw = -ow; bww = ow; au = bu = su * ou; am = bm = su * mu; aw2 = -mw; bw2 = mw; tu0 = tu1 = su * ru; tw0 = -rw; tw1 = rw; }
                double nlo = 0.38 * (apex - cv), dlo = sw != 0 ? ow - mw : ou - mu;
                double nhi = apex - mv, dhi = sw != 0 ? mw - rw : mu - ru;
                double l1 = Math.Sqrt(nlo * nlo + dlo * dlo), l2 = Math.Sqrt(nhi * nhi + dhi * dhi);
                SacredDraw.Quad(m, k, au, cv, aw, bu, cv, bww, bm, mv, bw2, am, mv, aw2, su * nlo / l1, dlo / l1, sw * nlo / l1, stone);
                SacredDraw.Quad(m, k, am, mv, aw2, bm, mv, bw2, tu1, apex, tw1, tu0, apex, tw0, su * nhi / l2, dhi / l2, sw * nhi / l2, stone);
            }
            SacredDraw.Box(m, k, -ru, ru, apex - 0.01, apex + 0.05, -rw, rw, light, BoxFaces.Top);
            if (lod >= 2)
            {
                Shapes.Cone(m, SacredDraw.At(k, 0, apex + 0.05, 0), light, 0.12, 0.42, 4, 0, 0, false);
                return;
            }
            Profile2 fp = new Profile2(12);
            fp.Clear(false).Add(0, 0).Add(0.16, 0, true).Add(0.16, 0.06, true).Add(0.1, 0.08).Add(0.13, 0.18).Add(0.1, 0.26, true).Add(0.06, 0.28)
              .Add(0.07, 0.34).Add(0.03, 0.42).Add(0, 0.46);
            Shapes.Lathe(m, SacredDraw.At(k, 0, apex + 0.05, 0), light, fp, lod == 0 ? 8 : 5);
            Shapes.Cone(m, SacredDraw.At(k, 0, apex + 0.47, 0), Looks.Gilt, 0.035, 0.14, 5, 0, 0, false);
            if (lod <= 1)
            {
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    SacredFigures.Guardian(m, k, sgn * (0.5 * bw + 0.24), plat, 0.5 * bd - 0.05, 0.7, GuardianKind.Lion, false, false, lod + 1);
            }
        }

        /// <summary>A sindoor-red image: Ganesh (a rounded relief with his trunk, ears, crown and a marigold garland), a
        /// linga on a yoni, a Bhairav mask, a nag stone or a generic image, standing at local (0, v, w).</summary>
        private static void Image(MeshData m, in Affine3 k, ShrineKind kind, double v, double w, double size, int lod)
        {
            ShapeBrush red = Looks.Sindoor;
            switch (kind)
            {
                case ShrineKind.Linga:
                {
                    Profile2 y = new Profile2(10);
                    y.Clear(false).Add(0, 0).Add(0.36 * size, 0, true).Add(0.36 * size, 0.08 * size, true).Add(0.3 * size, 0.14 * size, true).Add(0, 0.14 * size);
                    Shapes.Lathe(m, SacredDraw.At(k, 0, v, w), Looks.Stone, y, lod == 0 ? 10 : 6);
                    Shapes.Capsule(m, SacredDraw.At(k, 0, v + 0.12 * size, w), Looks.Of(MeshColor.FromHex(0x2A2A2E), MaterialChannel.Stone), 0.13 * size, 0.5 * size, lod == 0 ? 8 : 5);
                    SacredDraw.Box(m, k, -0.05 * size, 0.05 * size, v + 0.05 * size, v + 0.11 * size, w + 0.3 * size, w + 0.5 * size, Looks.Stone, BoxFaces.All & ~BoxFaces.Bottom);
                    break;
                }
                case ShrineKind.Nag:
                    Shapes.RoundedSlab(m, Affine3.RotationX(-0.5 * Math.PI).Then(SacredDraw.At(k, 0, v + 0.35 * size, w)), Looks.Stone, 0.4 * size, 0.7 * size, 0.1, 0.05, 0.02, 1);
                    break;
                case ShrineKind.Bhairav:
                    Shapes.Ellipsoid(m, SacredDraw.At(k, 0, v + 0.4 * size, w), Looks.Of(MeshColor.FromHex(0x2A3550), MaterialChannel.Paint), 0.26 * size, 0.32 * size, 0.12 * size, 6, false);
                    Shapes.Cone(m, SacredDraw.At(k, 0, v + 0.68 * size, w), Looks.Gilt, 0.2 * size, 0.25 * size, 6, 0, 0, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(k, 0, v + 0.3 * size, w + 0.1 * size), red, 0.12 * size, 0.04 * size, 0.03 * size, 4, false);
                    break;
                default:
                {
                    // Ganesh: body, head, ears, trunk, crown, garland.
                    Shapes.Ellipsoid(m, SacredDraw.At(k, 0, v + 0.22 * size, w), red, 0.26 * size, 0.22 * size, 0.16 * size, lod == 0 ? 6 : 4, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(k, 0, v + 0.55 * size, w + 0.02 * size), red, 0.17 * size, 0.17 * size, 0.14 * size, lod == 0 ? 6 : 4, false);
                    if (kind == ShrineKind.Ganesh && lod <= 1)
                    {
                        Shapes.Ellipsoid(m, SacredDraw.At(k, 0.17 * size, 0.55 * size + v, w), red, 0.1 * size, 0.13 * size, 0.03 * size, 4, false);
                        Shapes.Ellipsoid(m, SacredDraw.At(k, -0.17 * size, 0.55 * size + v, w), red, 0.1 * size, 0.13 * size, 0.03 * size, 4, false);
                        Path3 t = new Path3(6);
                        t.Add(0, v + 0.5 * size, w + 0.14 * size).Add(0.02 * size, v + 0.32 * size, w + 0.18 * size).Add(0.08 * size, v + 0.2 * size, w + 0.17 * size)
                         .Add(0.12 * size, v + 0.24 * size, w + 0.15 * size);
                        Shapes.Tube(m, k, red, t, 0.05 * size, 4, true, false, default, 0.025 * size);
                        Shapes.Cone(m, SacredDraw.At(k, 0, v + 0.68 * size, w), Looks.Gilt, 0.1 * size, 0.18 * size, 5, 0, 0, false);
                        Affine3 g = Affine3.RotationX(0.35 * Math.PI).Then(SacredDraw.At(k, 0, v + 0.36 * size, w + 0.04 * size));
                        Shapes.Torus(m, g, Looks.Of(SacredPalette.Marigold, MaterialChannel.Fabric), 0.2 * size, 0.035 * size, 8, 3);
                    }
                    break;
                }
            }
        }
    }
}
