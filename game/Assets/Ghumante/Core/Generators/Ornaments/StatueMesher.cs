using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Ornaments
{
    /// <summary>
    /// Statues on roundabouts (docs/research/w2/ref_ornaments.md §1-7): respectful cartoon figures built from smooth
    /// solids with the recognisable pose, attire and finish of the real statue (never a portrait, never a caricature;
    /// a statue is one colour, so faces carry modelled features only), their pedestals and stepped platforms.
    /// Figure frame: feet at y = 0, facing +Z, the figure's right hand on +X. All lengths are for a 1.8 m figure and
    /// scale with <see cref="StatueSpec.FigureM"/>.
    /// </summary>
    internal static class StatueMesher
    {
        // Joint heights of the 1.8 m reference figure.
        private const double Shoulder = 1.43, ShoulderX = 0.215, Waist = 1.02, Hip = 0.93, HeadY = 1.68;

        public static uint FinishColour(StatueFinish f)
        {
            switch (f)
            {
                case StatueFinish.Gilt: return OrnamentPalette.GiltStatue;
                case StatueFinish.BlackStone: return OrnamentPalette.BlackStone;
                case StatueFinish.WhiteMarble: return OrnamentPalette.Marble;
                case StatueFinish.Verdigris: return OrnamentPalette.Verdigris;
                default: return OrnamentPalette.Bronze;
            }
        }

        public static MaterialChannel FinishChannel(StatueFinish f)
        {
            switch (f)
            {
                case StatueFinish.Gilt: return MaterialChannel.Gilt;
                case StatueFinish.BlackStone: return MaterialChannel.Stone;
                case StatueFinish.WhiteMarble: return MaterialChannel.Plaster;
                default: return MaterialChannel.Metal;
            }
        }

        // ------------------------------------------------------------------ the whole statue

        /// <summary>
        /// Platform, pedestal and figure standing on <paramref name="f"/> (origin on the island top, +Z facing).
        /// Returns the height of the figure's crown above the frame origin.
        /// </summary>
        public static double Build(OrnCtx c, in Affine3 f, in StatueSpec s, double groundY)
        {
            MeshData m = c.M;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double k3 = Math.Max(0.5, s.FigureM) / 1.8;
            if (c.Lod >= 3)
            {
                // Silhouette: platform, pedestal, a robed figure.
                ShapeBrush st = OrnamentKit.B(s.PlinthColour != 0 ? s.PlinthColour : OrnamentPalette.PlinthCream, MaterialChannel.Stone);
                double py = s.Steps > 0 ? 0.28 * s.Steps : 0;
                if (s.Steps > 0) OrnamentKit.Box(m, f * Affine3.Translation(0, 0.5 * py - 0.12, 0), st, s.PlatformW, py + 0.24, s.PlatformW);
                if (s.Plinth == PlinthShape.Spire) Shapes.Cone(m, f * Affine3.Translation(0, py, 0), st, 0.5 * s.PlinthW, s.PlinthM, 4, 0, 0, false, c.L);
                else OrnamentKit.Box(m, f * Affine3.Translation(0, py + 0.5 * s.PlinthM, 0), st, s.PlinthW, s.PlinthM, s.PlinthW);
                ShapeBrush fb = OrnamentKit.B(FinishColour(s.Finish), FinishChannel(s.Finish));
                double fy = py + s.PlinthM;
                Shapes.Frustum(m, f * Affine3.Translation(0, fy, 0), fb, 0.3 * k3, 0.2 * k3, 1.55 * k3, 5, 0, 0, false, true, c.L);
                Shapes.Sphere(m, f * Affine3.Translation(0, fy + 1.68 * k3, 0), fb, 0.13 * k3, 5, c.L);
                if (c.Stats != null) c.Stats.Figures++;
                return fy + 1.85 * k3;
            }
            double y = Platform(c, f, s);
            y = Pedestal(c, f * Affine3.Translation(0, y, 0), s, y);
            c.Ao(v0, i0, groundY, 0.5f);
            int v1 = m.VertexCount, i1 = m.IndexCount;
            double k = Math.Max(0.5, s.FigureM) / 1.8;
            Affine3 ff = f * Affine3.Translation(0, y, 0) * Affine3.Scaling(k);
            if (s.Pose == StatuePose.Rider) Equestrian(c, ff, s);
            else if (s.Pose == StatuePose.Bust) Bust(c, ff, s);
            else Figure(c, ff, s);
            c.Ao(v1, i1, groundY + y, 0.7f);
            if (c.Stats != null) c.Stats.Figures++;
            double top = y + (s.Pose == StatuePose.Rider ? 2.5 : 1.85) * k;
            if (s.Attire == StatueAttire.RoyalPlumed || s.Attire == StatueAttire.RanaPlumed) top += 0.3 * k;
            return top;
        }

        // ------------------------------------------------------------------ platform and pedestal

        /// <summary>The stepped platform; returns its top height.</summary>
        public static double Platform(OrnCtx c, in Affine3 f, in StatueSpec s)
        {
            MeshData m = c.M;
            int steps = Math.Min(5, (int)s.Steps);
            if (steps <= 0) return 0;
            uint col = s.PlatformColour != 0 ? s.PlatformColour : OrnamentPalette.StepGrey;
            ShapeBrush b = OrnamentKit.B(col, MaterialChannel.Stone);
            double w0 = Math.Max(1.2, s.PlatformW), rise = 0.28, tread = 0.42;
            double top = 0;
            for (int k = 0; k < steps; k++)
            {
                double w = w0 - 2 * tread * k;
                if (w < 0.8) break;
                double y0 = k * rise - (k == 0 ? 0.25 : 0), h = rise + (k == 0 ? 0.25 : 0);
                Affine3 sf = f * Affine3.Translation(0, y0, 0);
                if (c.Lod >= 2)
                {
                    if (s.Platform == PlatformShape.Round) Shapes.Cylinder(m, sf, b, 0.5 * w, h, 12, 0, 0, false, true, c.L);
                    else OrnamentKit.Box(m, sf * Affine3.Translation(0, 0.5 * h, 0), b, w, h, w);
                }
                else if (s.Platform == PlatformShape.Round)
                {
                    Shapes.Cylinder(m, sf, b, 0.5 * w, h, 32, 0.03, 1, false, true, c.L);
                }
                else
                {
                    Shapes.RoundedSlab(m, sf, b, w, w, h, 0.06, 0.03, 1, c.L);
                }
                top = (k + 1) * rise;
                if (c.Stats != null) c.Stats.Steps++;
            }
            if (s.Pots && c.Lod < 2)
            {
                // Big terracotta pots at the corners of the second step (Tripureshwor) or potted plants round the top.
                double wt = w0 - 2 * tread * Math.Max(0, steps - 1);
                int n = s.Platform == PlatformShape.Round ? 6 : 4;
                for (int k = 0; k < n; k++)
                {
                    double a = (s.Platform == PlatformShape.Round ? 2 * Math.PI * (k + 0.5) / n : Math.PI / 4 + Math.PI / 2 * k);
                    double rr = s.Platform == PlatformShape.Round ? 0.5 * wt - 0.35 : 0.5 * wt * Math.Sqrt(2) - 0.55;
                    double ox, oy, oz;
                    f.Point(rr * Math.Sin(a), top, rr * Math.Cos(a), out ox, out oy, out oz);
                    OrnamentKit.Pot(c, ox, oy, oz, s.Platform == PlatformShape.Round ? 0.26 : 0.42, c.Seed + (uint)k);
                }
            }
            return top;
        }

        /// <summary>The pedestal standing on <paramref name="f"/>; returns the height of its top above the statue
        /// frame (<paramref name="baseY"/> + pedestal height).</summary>
        public static double Pedestal(OrnCtx c, in Affine3 f, in StatueSpec s, double baseY)
        {
            MeshData m = c.M;
            double h = Math.Max(0.4, s.PlinthM), w = Math.Max(0.6, s.PlinthW);
            uint col = s.PlinthColour != 0 ? s.PlinthColour : OrnamentPalette.PlinthCream;
            ShapeBrush b = OrnamentKit.B(col, MaterialChannel.Stone);
            ShapeBrush dark = OrnamentKit.B(OrnamentPalette.Plaque, MaterialChannel.Stone);
            if (c.Lod >= 2 && s.Plinth != PlinthShape.Spire)
            {
                if (s.Plinth == PlinthShape.Round || s.Plinth == PlinthShape.Octagonal)
                {
                    Shapes.Cylinder(m, f, b, 0.5 * w + 0.12, 0.3, 8, 0, 0, false, true, c.L);
                    Shapes.Cylinder(m, f * Affine3.Translation(0, 0.3, 0), b, 0.5 * w, h - 0.3, 8, 0, 0, false, true, c.L);
                }
                else if (s.Plinth == PlinthShape.Tapered)
                {
                    double bh = Math.Min(1.0, 0.25 * h);
                    OrnamentKit.Box(m, f * Affine3.Translation(0, 0.5 * bh, 0), b, w + 0.2, bh, w + 0.2);
                    Profile2 sq4 = c.P.Clear(true).SetRect(1, 1);
                    double[] ys4 = c.Ys, ss4 = c.Ss;
                    ys4[0] = 0;
                    ss4[0] = 0.56 * w;
                    ys4[1] = h - bh;
                    ss4[1] = 0.44 * w;
                    OrnamentKit.StackLoft(m, f * Affine3.Translation(0, bh, 0), b, sq4, ys4, ss4, 2, false, true);
                }
                else
                {
                    OrnamentKit.Box(m, f * Affine3.Translation(0, 0.15, 0), b, w + 0.3, 0.3, w + 0.3);
                    OrnamentKit.Box(m, f * Affine3.Translation(0, 0.5 * h, 0), b, w, h - 0.6, w);
                    OrnamentKit.Box(m, f * Affine3.Translation(0, h - 0.15, 0), b, w + 0.2, 0.3, w + 0.2);
                }
                return baseY + h;
            }
            switch (s.Plinth)
            {
                case PlinthShape.Round:
                case PlinthShape.Octagonal:
                {
                    int sides = s.Plinth == PlinthShape.Octagonal ? 8 : 24;
                    double r = 0.5 * w;
                    Profile2 p = c.Q.Clear(false);
                    p.Add(0, 0, true).Add(r + 0.14, 0, true).Add(r + 0.14, 0.18).Add(r + 0.04, 0.26, true).Add(r, 0.32, true)
                     .Add(r, h - 0.3, true).Add(r + 0.06, h - 0.24).Add(r + 0.13, h - 0.12, true).Add(r + 0.13, h, true).Add(0, h, true);
                    Shapes.Lathe(m, f, b, p, sides, c.L);
                    Shapes.Cylinder(m, f * Affine3.Translation(0, 0.45 * h, 0), dark, r + 0.012, 0.18 * h, sides, 0, 0, false, false, c.L);
                    break;
                }
                case PlinthShape.Tapered:
                {
                    // Wide square base with cornice and a black plaque in a gilt border, then the tapered die and cap.
                    double bw = w, bh = Math.Min(1.0, 0.25 * h);
                    Shapes.RoundedSlab(m, f, b, bw, bw, bh - 0.18, 0.03, 0.02, 1, c.L);
                    Shapes.RoundedSlab(m, f * Affine3.Translation(0, bh - 0.18, 0), b, bw + 0.24, bw + 0.24, 0.18, 0.07, 0.05, 2, c.L);
                    if (c.Lod < 2)
                    {
                        OrnamentKit.Box(m, f * Affine3.Translation(0, 0.45 * bh, 0.5 * bw), OrnamentKit.B(OrnamentPalette.PlaqueGilt, MaterialChannel.Gilt), 0.82 * bw, 0.62 * bh, 0.04);
                        OrnamentKit.Box(m, f * Affine3.Translation(0, 0.45 * bh, 0.5 * bw + 0.012), dark, 0.76 * bw, 0.54 * bh, 0.04);
                    }
                    double dh = h - bh - 0.3;
                    Profile2 sq = c.P.Clear(true).SetRoundedRect(1, 1, 0.05, 2);
                    double[] ys = c.Ys, ss = c.Ss;
                    ys[0] = 0;
                    ss[0] = 0.56 * bw;
                    ys[1] = dh;
                    ss[1] = 0.44 * bw;
                    OrnamentKit.StackLoft(m, f * Affine3.Translation(0, bh, 0), b, sq, ys, ss, 2, false, false);
                    Shapes.RoundedSlab(m, f * Affine3.Translation(0, bh + dh, 0), b, 0.56 * bw, 0.56 * bw, 0.16, 0.06, 0.04, 1, c.L);
                    Shapes.RoundedSlab(m, f * Affine3.Translation(0, bh + dh + 0.16, 0), b, 0.5 * bw, 0.5 * bw, 0.14, 0.05, 0.03, 1, c.L);
                    if (c.Lod < 2)
                    {
                        double py = bh + 0.62 * dh, pz = 0.5 * (0.56 * bw + (0.44 * bw - 0.56 * bw) * 0.62) + 0.012;
                        Affine3 pf = f * Affine3.Translation(0, py, pz) * Affine3.RotationX(Math.Atan2(0.06 * bw, dh));
                        OrnamentKit.Box(m, pf, OrnamentKit.B(OrnamentPalette.BronzeDark, MaterialChannel.Metal), 0.32 * bw, 0.42, 0.03);
                    }
                    break;
                }
                case PlinthShape.Spire:
                {
                    // Concave-sided stone spire (Jawalakhel), red and yellow collar, small top disc.
                    Profile2 sq = c.Lod >= 2 ? c.P.Clear(true).SetRoundedRect(1, 1, 0.12, 1) : c.P.Clear(true).SetRoundedRect(1, 1, 0.12, c.Lod == 0 ? 3 : 2);
                    int rings = c.Lod == 0 ? 12 : c.Lod == 1 ? 7 : 4;
                    double[] ys = c.Ys, ss = c.Ss;
                    double top = 0.085 * w;
                    for (int k = 0; k < rings; k++)
                    {
                        double t = (double)k / (rings - 1);
                        ys[k] = (h - 0.55) * t;
                        ss[k] = top + (w - top) * Math.Pow(1 - t, 3.2);
                    }
                    int sv = m.VertexCount;
                    OrnamentKit.StackLoft(m, f, b, sq, ys, ss, rings, false, true);
                    ShapeColor.JitterByPosition(m, sv, m.VertexCount - sv, 0.05f, c.Seed, 0.6);
                    double cy = h - 0.55;
                    int cr = c.Lod >= 2 ? 8 : 16;
                    Shapes.Cylinder(m, f * Affine3.Translation(0, cy, 0), OrnamentKit.B(OrnamentPalette.CollarRed, MaterialChannel.Paint), 0.5 * top + 0.12, 0.28, cr, 0, 0, false, true, c.L);
                    Shapes.Cylinder(m, f * Affine3.Translation(0, cy + 0.28, 0), OrnamentKit.B(OrnamentPalette.CollarYellow, MaterialChannel.Paint), 0.5 * top + 0.2, 0.2, cr, c.Lod >= 2 ? 0 : 0.04, 1, false, true, c.L);
                    if (c.Lod < 2) Shapes.Cylinder(m, f * Affine3.Translation(0, cy + 0.48, 0), OrnamentKit.B(OrnamentPalette.CollarRed, MaterialChannel.Paint), 0.5 * top + 0.16, 0.07, cr, 0.02, 1, false, true, c.L);
                    break;
                }
                default:
                {
                    // Square die: moulded base, die with a front panel, cornice and cap; optional crest medallion.
                    Shapes.RoundedSlab(m, f, b, w + 0.32, w + 0.32, 0.22, 0.06, 0.04, 1, c.L);
                    Shapes.RoundedSlab(m, f * Affine3.Translation(0, 0.22, 0), b, w + 0.16, w + 0.16, 0.1, 0.06, 0.04, 1, c.L);
                    double dh = h - 0.32 - 0.3;
                    Shapes.RoundedBox(m, f * Affine3.Translation(0, 0.32 + 0.5 * dh, 0), b, w, dh, w, 0.03, 1, c.L);
                    Shapes.RoundedSlab(m, f * Affine3.Translation(0, h - 0.3, 0), b, w + 0.18, w + 0.18, 0.16, 0.07, 0.05, 2, c.L);
                    Shapes.RoundedSlab(m, f * Affine3.Translation(0, h - 0.14, 0), b, w + 0.06, w + 0.06, 0.14, 0.05, 0.03, 1, c.L);
                    if (c.Lod < 2)
                    {
                        // Inscription panels (blank) on the front and the sides.
                        uint panel = MeshColor.Scale(col, 0.88f);
                        ShapeBrush pb = OrnamentKit.B(panel, MaterialChannel.Stone);
                        OrnamentKit.Box(m, f * Affine3.Translation(0, 0.32 + 0.48 * dh, 0.5 * w), pb, 0.72 * w, 0.62 * dh, 0.04);
                        OrnamentKit.Box(m, f * Affine3.Translation(0.5 * w, 0.32 + 0.48 * dh, 0), pb, 0.04, 0.62 * dh, 0.72 * w);
                        OrnamentKit.Box(m, f * Affine3.Translation(-0.5 * w, 0.32 + 0.48 * dh, 0), pb, 0.04, 0.62 * dh, 0.72 * w);
                    }
                    if (s.Medallion && c.Lod < 2)
                    {
                        Affine3 md = f * Affine3.Translation(0, h + 0.18, 0.5 * w - 0.02) * Affine3.RotationX(Math.PI / 2);
                        Shapes.Cylinder(m, md, b, 0.3, 0.12, 20, 0.03, 1, true, true, c.L);
                        Shapes.Torus(m, md * Affine3.Translation(0, 0.12, 0), b, 0.24, 0.035, 20, 6, c.L);
                        Shapes.RoundedSlab(m, f * Affine3.Translation(0, h - 0.02, 0.5 * w - 0.05), b, 0.7, 0.2, 0.2, 0.04, 0.03, 1, c.L);
                    }
                    break;
                }
            }
            return baseY + h;
        }

        // ------------------------------------------------------------------ the figure

        private struct Arms
        {
            public double LEx, LEy, LEz, LWx, LWy, LWz, REx, REy, REz, RWx, RWy, RWz;
        }

        private static Arms Pose(StatuePose p)
        {
            var a = new Arms();
            // Defaults: arms down at the sides (figure's right on +X).
            a.REx = 0.255; a.REy = 1.14; a.REz = 0.0; a.RWx = 0.26; a.RWy = 0.88; a.RWz = 0.05;
            a.LEx = -0.255; a.LEy = 1.14; a.LEz = 0.0; a.LWx = -0.26; a.LWy = 0.88; a.LWz = 0.05;
            switch (p)
            {
                case StatuePose.Wave:
                    a.REx = 0.36; a.REy = 1.45; a.REz = 0.06; a.RWx = 0.38; a.RWy = 1.74; a.RWz = 0.1;
                    break;
                case StatuePose.HandOnChest:
                    a.REx = 0.23; a.REy = 1.17; a.REz = 0.14; a.RWx = 0.06; a.RWy = 1.31; a.RWz = 0.17;
                    break;
                case StatuePose.Book:
                    a.LEx = -0.25; a.LEy = 1.15; a.LEz = 0.05; a.LWx = -0.12; a.LWy = 1.08; a.LWz = 0.22;
                    break;
                case StatuePose.Namaste:
                    a.REx = 0.2; a.REy = 1.19; a.REz = 0.14; a.RWx = 0.03; a.RWy = 1.31; a.RWz = 0.22;
                    a.LEx = -0.2; a.LEy = 1.19; a.LEz = 0.14; a.LWx = -0.03; a.LWy = 1.31; a.LWz = 0.22;
                    break;
                case StatuePose.PointUp:
                    a.REx = 0.3; a.REy = 1.52; a.REz = 0.12; a.RWx = 0.32; a.RWy = 1.86; a.RWz = 0.18;
                    break;
                case StatuePose.CloakHands:
                    a.LEx = -0.28; a.LEy = 1.15; a.LEz = 0.0; a.LWx = -0.17; a.LWy = 1.01; a.LWz = 0.14;
                    a.REx = 0.25; a.REy = 1.17; a.REz = 0.09; a.RWx = 0.1; a.RWy = 1.29; a.RWz = 0.17;
                    break;
                case StatuePose.HandOnHilt:
                    a.LEx = -0.31; a.LEy = 1.17; a.LEz = -0.03; a.LWx = -0.21; a.LWy = 0.99; a.LWz = 0.08;
                    break;
                case StatuePose.Sceptre:
                    a.REx = 0.27; a.REy = 1.15; a.REz = 0.06; a.RWx = 0.2; a.RWy = 1.24; a.RWz = 0.24;
                    break;
            }
            return a;
        }

        /// <summary>A standing figure in frame <paramref name="f"/> (already scaled to the figure height).</summary>
        public static void Figure(OrnCtx c, in Affine3 f, in StatueSpec s)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            uint col = FinishColour(s.Finish);
            MaterialChannel ch = FinishChannel(s.Finish);
            ShapeBrush b = OrnamentKit.B(col, ch);
            // Slightly darker recesses (trousers, under the coat) keep the forms readable in one material.
            ShapeBrush bd = OrnamentKit.B(MeshColor.Scale(col, 0.86f), ch);
            if (c.Lod >= 3)
            {
                Shapes.Capsule(m, f, b, 0.24, 1.62, 6, L);
                Shapes.Sphere(m, f * Affine3.Translation(0, HeadY, 0), b, 0.12, 6, L);
                return;
            }
            bool royal = s.Attire == StatueAttire.RoyalPlumed || s.Attire == StatueAttire.RanaPlumed;
            bool topi = s.Attire == StatueAttire.DauraTopi || s.Attire == StatueAttire.CoatTopi;
            Arms a = Pose(s.Pose);
            bool lod2 = c.Lod >= 2;
            // Shoes and legs (trousers).
            if (!lod2)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Shapes.Superellipsoid(m, f * Affine3.Translation(0.095 * side, 0.045, 0.045), b, 0.06, 0.045, 0.125, 0.6, 0.8, 8, L);
                    Shapes.Capsule(m, f * Affine3.Translation(0.095 * side, 0.05, 0) * Affine3.Scaling(1, 1, 0.92), bd, 0.078, 0.95, 8, L);
                }
            }
            // Coat skirt / daura below the waist: hem height by attire (long royal coats reach the knee).
            double hem = royal ? 0.44 : s.Attire == StatueAttire.DauraTopi ? 0.6 : 0.5;
            {
                Profile2 p = c.Q.Clear(false);
                p.Add(0.25, hem, true).Add(0.235, hem + 0.12).Add(0.2, Hip).Add(0.17, Waist + 0.02, true).Add(0, Waist + 0.02, true);
                if (!lod2) p.Smooth(2);
                Shapes.Lathe(m, f * Affine3.Scaling(1, 1, 0.74), b, p, lod2 ? 8 : 16, L);
                if (lod2) Shapes.Cylinder(m, f * Affine3.Scaling(1, 1, 0.7), bd, 0.16, hem + 0.02, 8, 0, 0, false, false, L);
            }
            // Torso and shoulders.
            {
                Profile2 p = c.Q.Clear(false);
                p.Add(0, Waist - 0.02, true).Add(0.172, Waist - 0.02, true).Add(0.195, 1.16).Add(0.208, 1.31).Add(0.2, 1.41).Add(0.15, 1.49)
                 .Add(0.065, 1.53).Add(0, 1.535, true);
                if (!lod2) p.Smooth(2);
                Shapes.Lathe(m, f * Affine3.Scaling(1, 1, 0.64), b, p, lod2 ? 8 : 16, L);
                if (!lod2)
                {
                    Shapes.Sphere(m, f * Affine3.Translation(ShoulderX - 0.02, Shoulder - 0.01, 0) * Affine3.Scaling(1.1, 0.8, 0.95), b, 0.066, 10, L);
                    Shapes.Sphere(m, f * Affine3.Translation(-ShoulderX + 0.02, Shoulder - 0.01, 0) * Affine3.Scaling(1.1, 0.8, 0.95), b, 0.066, 10, L);
                }
            }
            if (!lod2)
            {
                // Belt and front opening of the coat; sash and medals for royal dress; waistcoat for daura.
                Shapes.Torus(m, f * Affine3.Translation(0, Waist + 0.01, 0) * Affine3.Scaling(1, 1, 0.74), bd, 0.172, 0.022, 16, 4, L);
                Shapes.Bar(m, f, bd, 0, Waist + 0.02, 0.137, 0, 1.38, 0.124, 0.012, 4, L);
                if (c.Lod == 0)
                {
                    for (int k = 0; k < 4; k++) Shapes.Sphere(m, f * Affine3.Translation(0.03, 1.08 + 0.085 * k, 0.13 - 0.002 * k), b, 0.014, 6, L);
                }
                if (royal)
                {
                    Path3 sp = c.Path2.Clear();
                    sp.Add(-0.16, 1.43, 0.08).Add(-0.07, 1.33, 0.135).Add(0.05, 1.19, 0.138).Add(0.16, 1.05, 0.12);
                    Profile2 sec = c.P.Clear(true).SetRoundedRect(0.075, 0.016, 0.006, 1);
                    Shapes.Sweep(m, f, b, sp, sec, false, true, SweepFrames.ParallelTransport);
                    if (c.Lod == 0)
                    {
                        // Medals: a row of small discs on the left breast.
                        for (int k = 0; k < 3; k++)
                            Shapes.Cylinder(m, f * Affine3.Translation(-0.09 + 0.035 * k, 1.27, 0.128) * Affine3.RotationX(Math.PI / 2), b, 0.016, 0.01, 8, 0, 0, false, true, L);
                    }
                    // Collar (high Rana / royal collar).
                    Shapes.Torus(m, f * Affine3.Translation(0, 1.5, 0) * Affine3.Scaling(1, 1.6, 0.85), b, 0.07, 0.022, 12, 4, L);
                }
                else if (s.Attire == StatueAttire.DauraTopi)
                {
                    // Waistcoat edge.
                    Shapes.Torus(m, f * Affine3.Translation(0, 1.12, 0) * Affine3.Scaling(1, 1, 0.68), bd, 0.2, 0.014, 16, 3, L);
                }
            }
            // Cloak from the shoulders to the ground behind the figure.
            if (s.Cloak)
            {
                Profile2 p = c.Q.Clear(true);
                double t = 0.035;
                p.Add(0.42, 0.02, true).Add(0.37, 0.5).Add(0.3, 1.05).Add(0.25, 1.4).Add(0.18, 1.5, true)
                 .Add(0.18 - t, 1.48, true).Add(0.25 - t, 1.38).Add(0.3 - t, 1.04).Add(0.37 - t, 0.5).Add(0.42 - t, 0.02, true);
                if (!lod2) p.Smooth(1);
                Shapes.Lathe(m, f * Affine3.Scaling(1, 1, 0.72), b, p, lod2 ? 8 : 16, L, 96, 168, true);
            }
            // Arms and hands.
            ArmPair(c, f, b, a, lod2);
            // Neck and head.
            Shapes.Cylinder(m, f * Affine3.Translation(0, 1.49, 0.005), b, 0.056, 0.12, lod2 ? 6 : 12, 0, 0, false, false, L);
            Shapes.Superellipsoid(m, f * Affine3.Translation(0, HeadY, 0.012), b, 0.093, 0.118, 0.104, 0.9, 1.0, lod2 ? 8 : 16, L);
            if (c.Lod == 0) Face(c, f, b, s, royal);
            else if (!lod2) Shapes.Ellipsoid(m, f * Affine3.Translation(0, HeadY - 0.005, 0.112), b, 0.018, 0.032, 0.024, 6, false, L);
            Headgear(c, f, b, s, royal, topi, lod2);
            // Props: sword, sceptre, book.
            if (!lod2)
            {
                if (s.Pose == StatuePose.HandOnHilt || s.Pose == StatuePose.CloakHands || s.Attire == StatueAttire.RanaPlumed)
                {
                    // Scabbard down and back from the left hip, hilt forward under the hand.
                    Shapes.Bar(m, f, b, -0.2, 0.97, 0.02, -0.27, 0.22, -0.16, 0.026, 6, L);
                    Shapes.Bar(m, f, b, -0.2, 0.97, 0.02, -0.2, 1.06, 0.1, 0.018, 6, L);
                    Shapes.Bar(m, f, b, -0.25, 1.0, 0.06, -0.15, 1.0, 0.06, 0.012, 4, L);
                }
                if (s.Pose == StatuePose.Sceptre)
                {
                    Shapes.Bar(m, f, b, a.RWx, a.RWy - 0.22, a.RWz + 0.02, a.RWx + 0.01, a.RWy + 0.36, a.RWz + 0.04, 0.016, 6, L);
                    Shapes.Sphere(m, f * Affine3.Translation(a.RWx + 0.01, a.RWy + 0.4, a.RWz + 0.04), b, 0.035, 8, L);
                }
                if (s.Pose == StatuePose.Book)
                    OrnamentKit.Box(m, f * Affine3.Translation(a.LWx + 0.02, a.LWy + 0.02, a.LWz + 0.03) * Affine3.RotationY(0.4), b, 0.05, 0.22, 0.16);
                if (s.Pose == StatuePose.PointUp)
                {
                    double fl;
                    Affine3 fa = f * Affine3.Along(a.RWx, a.RWy + 0.05, a.RWz, a.RWx + 0.005, a.RWy + 0.17, a.RWz + 0.012, out fl);
                    Shapes.Capsule(m, fa, b, 0.013, fl + 0.026, 6, L);
                }
            }
            if (s.Garland && c.Lod < 2) Garland(c, f);
        }

        private static void ArmPair(OrnCtx c, in Affine3 f, in ShapeBrush b, in Arms a, bool lod2)
        {
            Arm(c, f, b, ShoulderX, a.REx, a.REy, a.REz, a.RWx, a.RWy, a.RWz, lod2);
            Arm(c, f, b, -ShoulderX, a.LEx, a.LEy, a.LEz, a.LWx, a.LWy, a.LWz, lod2);
        }

        private static void Arm(OrnCtx c, in Affine3 f, in ShapeBrush b, double sx, double ex, double ey, double ez, double wx, double wy, double wz,
                                bool lod2)
        {
            MeshData m = c.M;
            double sy = Shoulder - 0.02, sz = 0;
            double l1, l2;
            int rad = lod2 ? 5 : 8;
            Shapes.Capsule(m, f * Affine3.Along(sx, sy, sz, ex, ey, ez, out l1), b, 0.062, l1 + 0.124, rad, c.L);
            Shapes.Capsule(m, f * Affine3.Along(ex, ey, ez, wx, wy, wz, out l2), b, 0.052, l2 + 0.1, rad, c.L);
            if (!lod2)
            {
                // Sleeve cuff and a mitten-like hand continuing the forearm.
                double dx = wx - ex, dy = wy - ey, dz = wz - ez, l = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (l < 1e-6) l = 1;
                dx /= l;
                dy /= l;
                dz /= l;
                Shapes.Torus(m, f * Affine3.Along(wx - 0.02 * dx, wy - 0.02 * dy, wz - 0.02 * dz, wx, wy, wz, out _), b, 0.05, 0.014, 8, 3, c.L);
                double hx = wx + 0.07 * dx, hy = wy + 0.07 * dy, hz = wz + 0.07 * dz;
                Shapes.Superellipsoid(m, f * Affine3.Along(hx - 0.05 * dx, hy - 0.05 * dy, hz - 0.05 * dz, hx + 0.05 * dx, hy + 0.05 * dy, hz + 0.05 * dz, out _)
                                         * Affine3.Translation(0, 0.05, 0), b, 0.042, 0.06, 0.028, 0.7, 0.8, 8, c.L);
            }
        }

        private static void Face(OrnCtx c, in Affine3 f, in ShapeBrush b, in StatueSpec s, bool royal)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            // Nose, brow, ears, chin: modelled, never painted.
            Shapes.Ellipsoid(m, f * Affine3.Translation(0, HeadY - 0.005, 0.112) * Affine3.RotationX(0.25), b, 0.016, 0.032, 0.022, 8, false, L);
            Shapes.Ellipsoid(m, f * Affine3.Translation(0, HeadY + 0.035, 0.095), b, 0.07, 0.016, 0.02, 8, false, L);
            Shapes.Ellipsoid(m, f * Affine3.Translation(0.092, HeadY, 0.0), b, 0.016, 0.03, 0.022, 6, false, L);
            Shapes.Ellipsoid(m, f * Affine3.Translation(-0.092, HeadY, 0.0), b, 0.016, 0.03, 0.022, 6, false, L);
            if (royal) Shapes.Ellipsoid(m, f * Affine3.Translation(0, HeadY - 0.05, 0.103), b, 0.045, 0.011, 0.016, 8, false, L);
            if (s.Glasses)
            {
                bool dark = s.Attire == StatueAttire.RoyalPlumed;
                ShapeBrush gb = dark ? OrnamentKit.B(MeshColor.Scale(b.Color, 0.6f), b.Channel) : b;
                for (int side = -1; side <= 1; side += 2)
                {
                    Affine3 lens = f * Affine3.Translation(0.04 * side, HeadY + 0.012, 0.1) * Affine3.RotationX(Math.PI / 2);
                    if (dark) Shapes.Ellipsoid(m, lens, gb, 0.032, 0.008, 0.026, 8, false, L);
                    else Shapes.Torus(m, lens, gb, 0.026, 0.005, 12, 4, L);
                    Shapes.Bar(m, f, gb, 0.066 * side, HeadY + 0.014, 0.095, 0.094 * side, HeadY + 0.016, 0.0, 0.004, 3, L);
                }
                Shapes.Bar(m, f, gb, -0.014, HeadY + 0.016, 0.108, 0.014, HeadY + 0.016, 0.108, 0.004, 3, L);
            }
        }

        private static void Headgear(OrnCtx c, in Affine3 f, in ShapeBrush b, in StatueSpec s, bool royal, bool topi, bool lod2)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            if (topi)
            {
                // Dhaka topi: a soft frustum whose top slopes (taller at the front left), sitting low on the brow.
                Profile2 p = c.Q.Clear(false);
                p.Add(0.099, 0, true).Add(0.103, 0.06).Add(0.098, 0.12, true).Add(0.06, 0.128).Add(0, 0.13, true);
                Affine3 shear = new Affine3(1, 0, 0, 0, 0.18, 1, 0.22, 0, 0, 0, 1, 0);
                Shapes.Lathe(m, f * Affine3.Translation(0, HeadY + 0.045, 0.004) * shear * Affine3.Scaling(1, 1, 1.08), b, p, lod2 ? 8 : 20, L);
                return;
            }
            if (!royal) return;
            bool rana = s.Attire == StatueAttire.RanaPlumed;
            // Shirpech: a fitted crown cap with a jewelled band; Rana helmets sit taller.
            Shapes.Dome(m, f * Affine3.Translation(0, HeadY + 0.04, 0.0), b, 0.108, rana ? 0.15 : 0.11, lod2 ? 8 : 16, true, L);
            if (lod2)
            {
                Shapes.Bar(m, f, b, 0, HeadY + 0.15, 0.03, 0, HeadY + 0.42, -0.12, 0.03, 4, L);
                return;
            }
            Shapes.Torus(m, f * Affine3.Translation(0, HeadY + 0.05, 0), b, 0.106, 0.016, 20, 6, L);
            Shapes.Sphere(m, f * Affine3.Translation(0, HeadY + 0.1, 0.106), b, 0.022, 8, L);
            // The plume: a quill rising from the front of the crown and curving back, with bird-of-paradise feathers
            // cascading from its tip down behind the head (the shirpech of the royal statues).
            double plumeH = s.PlumeM > 0 ? s.PlumeM : rana ? 0.5 : 0.42;
            double tipY = HeadY + 0.12 + plumeH, tipZ = -0.06;
            Path3 pp = c.Path2.Clear();
            Curves.Bezier(pp, 0, HeadY + 0.12, 0.08, 0, HeadY + 0.12 + 0.6 * plumeH, 0.11, 0, tipY + 0.02, 0.04, 0, tipY, tipZ, 8);
            Shapes.Tube(m, f, b, pp, 0.04, 8, true, false, L, 0.02);
            int feathers = c.Lod == 0 ? 5 : 2;
            Profile2 fs = c.P.Clear(true).SetEllipse(0.05, 0.012, 6);
            for (int k = 0; k < feathers; k++)
            {
                double spread = feathers > 1 ? (k / (double)(feathers - 1) - 0.5) : 0;
                Path3 fp = c.Path2.Clear();
                double fall = Math.Min(0.3, 0.75 * plumeH);
                Curves.Bezier(fp, 0, tipY, tipZ, 0.03 * spread, tipY + 0.05, tipZ - 0.14, 0.06 * spread, tipY - 0.4 * fall, tipZ - 0.26,
                              0.08 * spread, tipY - fall - 0.08 * (1 - Math.Abs(spread)), tipZ - 0.22, 7);
                Shapes.Sweep(m, f, b, fp, fs, false, true, SweepFrames.ParallelTransport, 1, 0.35);
            }
        }

        private static void Garland(OrnCtx c, in Affine3 f)
        {
            MeshData m = c.M;
            uint[] cols = s_garlandCols;
            int loops = c.Lod == 0 ? 3 : 1;
            for (int g = 0; g < loops; g++)
            {
                double low = 1.16 - 0.17 * g, spread = 0.09 + 0.015 * g;
                // A smooth closed loop: behind the neck, over the shoulders, down the front in a U.
                Path3 ctrl = c.Path.Clear();
                ctrl.Add(0, 1.52, -0.13).Add(-spread - 0.03, 1.5, -0.06).Add(-spread - 0.06, 1.43, 0.1).Add(-0.08, low + 0.1, 0.165).Add(0, low, 0.17)
                    .Add(0.08, low + 0.1, 0.165).Add(spread + 0.06, 1.43, 0.1).Add(spread + 0.03, 1.5, -0.06);
                Path3 p = Curves.CatmullRom(ctrl, c.Path2.Clear(), c.Lod == 0 ? 4 : 2, true);
                int v0 = m.VertexCount;
                Shapes.Tube(m, f, OrnamentKit.B(cols[g % cols.Length], MaterialChannel.Foliage), p, 0.03, 5, false, true, c.L);
                // Flower-by-flower shading instead of a smooth rope.
                if (c.Lod == 0) ShapeColor.JitterByPosition(m, v0, m.VertexCount - v0, 0.18f, c.Seed + (uint)g, 0.03);
            }
        }

        private static readonly uint[] s_garlandCols = { OrnamentPalette.MarigoldYellow, OrnamentPalette.GarlandRed, OrnamentPalette.FlowerWhite };

        // ------------------------------------------------------------------ bust and equestrian

        /// <summary>A bust on a small pedestal block in frame <paramref name="f"/> (scaled to a 1.8 m figure: the bust
        /// is about 0.75 of that).</summary>
        public static void Bust(OrnCtx c, in Affine3 f, in StatueSpec s)
        {
            MeshData m = c.M;
            ShapeBrush b = OrnamentKit.B(FinishColour(s.Finish), FinishChannel(s.Finish));
            // Shift so the bust's chest sits on the frame origin.
            Affine3 bf = f * Affine3.Translation(0, -1.12, 0);
            Profile2 p = c.Q.Clear(false);
            p.Add(0, 1.12, true).Add(0.2, 1.12, true).Add(0.215, 1.3).Add(0.2, 1.41).Add(0.15, 1.49).Add(0.065, 1.53).Add(0, 1.535, true);
            if (c.Lod < 2) p.Smooth(2);
            Shapes.Lathe(m, bf * Affine3.Scaling(1, 1, 0.62), b, p, c.Lod < 2 ? 14 : 8, c.L);
            Shapes.Cylinder(m, bf * Affine3.Translation(0, 1.49, 0.005), b, 0.056, 0.12, 10, 0, 0, false, false, c.L);
            Shapes.Superellipsoid(m, bf * Affine3.Translation(0, HeadY, 0.012), b, 0.093, 0.118, 0.104, 0.9, 1.0, c.Lod < 2 ? 14 : 8, c.L);
            bool royal = s.Attire == StatueAttire.RoyalPlumed || s.Attire == StatueAttire.RanaPlumed;
            if (c.Lod < 2) Face(c, bf, b, s, royal);
            Headgear(c, bf, b, s, royal, s.Attire == StatueAttire.DauraTopi || s.Attire == StatueAttire.CoatTopi, c.Lod >= 2);
            if (c.Stats != null) c.Stats.Busts++;
        }

        /// <summary>A rider on a standing horse (one foreleg raised) in frame <paramref name="f"/>.</summary>
        public static void Equestrian(OrnCtx c, in Affine3 f, in StatueSpec s)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            ShapeBrush b = OrnamentKit.B(FinishColour(s.Finish), FinishChannel(s.Finish));
            int seg = c.Lod >= 2 ? 10 : 20;
            // Horse: barrel, chest, rump, neck, head, legs, tail.
            Shapes.Superellipsoid(m, f * Affine3.Translation(0, 1.25, -0.05), b, 0.3, 0.34, 0.78, 0.8, 0.9, seg, L);
            Shapes.Capsule(m, f * Affine3.Along(0, 1.35, 0.55, 0, 1.95, 0.92, out double nl), b, 0.17, nl + 0.2, seg / 2 + 2, L);
            Shapes.Superellipsoid(m, f * Affine3.Translation(0, 1.92, 1.12) * Affine3.RotationX(0.9), b, 0.11, 0.3, 0.13, 0.8, 0.9, seg, L);
            if (c.Lod < 2)
            {
                Shapes.Cone(m, f * Affine3.Translation(0.06, 2.12, 0.98), b, 0.03, 0.1, 5, 0, 0, true, L);
                Shapes.Cone(m, f * Affine3.Translation(-0.06, 2.12, 0.98), b, 0.03, 0.1, 5, 0, 0, true, L);
                Path3 tp = c.Path2.Clear();
                Curves.Bezier(tp, 0, 1.45, -0.8, 0, 1.4, -1.0, 0, 1.0, -1.02, 0, 0.75, -0.95, 6);
                Shapes.Tube(m, f, b, tp, 0.07, 6, true, false, L, 0.03);
            }
            double[] lx = { 0.17, -0.17, 0.17, -0.17 }, lz = { 0.5, 0.5, -0.55, -0.55 };
            for (int k = 0; k < 4; k++)
            {
                double l;
                if (k == 0)
                {
                    // Raised foreleg.
                    Shapes.Capsule(m, f * Affine3.Along(lx[k], 1.05, lz[k], lx[k], 0.75, lz[k] + 0.3, out l), b, 0.065, l + 0.13, 8, L);
                    Shapes.Capsule(m, f * Affine3.Along(lx[k], 0.75, lz[k] + 0.3, lx[k], 0.55, lz[k] + 0.12, out l), b, 0.055, l + 0.11, 8, L);
                    continue;
                }
                Shapes.Capsule(m, f * Affine3.Along(lx[k], 1.05, lz[k], lx[k], 0.45, lz[k] + 0.02, out l), b, 0.068, l + 0.13, 8, L);
                Shapes.Capsule(m, f * Affine3.Along(lx[k], 0.45, lz[k] + 0.02, lx[k], 0.05, lz[k], out l), b, 0.055, l + 0.11, 8, L);
            }
            // Rider seated on the saddle: thighs along the barrel, torso, arms holding the reins, head and headgear.
            Affine3 r = f * Affine3.Translation(0, 0.62, -0.05);
            for (int side = -1; side <= 1; side += 2)
            {
                Shapes.Capsule(m, r * Affine3.Along(0.15 * side, Hip, -0.05, 0.27 * side, Hip - 0.05, 0.35, out double tl), b, 0.08, tl + 0.16, 8, L);
                Shapes.Capsule(m, r * Affine3.Along(0.27 * side, Hip - 0.05, 0.35, 0.3 * side, Hip - 0.5, 0.3, out tl), b, 0.065, tl + 0.13, 8, L);
            }
            Profile2 p = c.Q.Clear(false);
            p.Add(0, Hip, true).Add(0.17, Hip, true).Add(0.2, 1.16).Add(0.208, 1.31).Add(0.2, 1.41).Add(0.15, 1.49).Add(0.065, 1.53).Add(0, 1.535, true);
            if (c.Lod < 2) p.Smooth(2);
            Shapes.Lathe(m, r * Affine3.Scaling(1, 1, 0.64), b, p, c.Lod < 2 ? 20 : 8, L);
            var a = new Arms
            {
                REx = 0.25, REy = 1.15, REz = 0.1, RWx = 0.12, RWy = 1.08, RWz = 0.32,
                LEx = -0.25, LEy = 1.15, LEz = 0.1, LWx = -0.12, LWy = 1.08, LWz = 0.32,
            };
            ArmPair(c, r, b, a, c.Lod >= 2);
            Shapes.Cylinder(m, r * Affine3.Translation(0, 1.49, 0.005), b, 0.056, 0.12, 10, 0, 0, false, false, L);
            Shapes.Superellipsoid(m, r * Affine3.Translation(0, HeadY, 0.012), b, 0.093, 0.118, 0.104, 0.9, 1.0, c.Lod >= 2 ? 10 : 18, L);
            bool royal = s.Attire == StatueAttire.RoyalPlumed || s.Attire == StatueAttire.RanaPlumed;
            if (c.Lod < 2) Face(c, r, b, s, royal);
            Headgear(c, r, b, s, royal, false, c.Lod >= 2);
        }
    }
}
