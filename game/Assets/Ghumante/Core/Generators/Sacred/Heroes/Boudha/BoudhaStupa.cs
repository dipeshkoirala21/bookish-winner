using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// S1, the Great Stupa (Jarung Khashor) as rebuilt in 2016 (boudha.md 3.1), in the site frame (u east', y up from the
    /// kora, v north'; origin on the stupa axis):
    /// <list type="bullet">
    /// <item>three cross-on-square terraces to their OSM outlines (T1 w56688295, T2 w235084185, T3 w421288398) with tops
    /// at 4.0, 8.0 and 10.5 m, white lime walls with a base course and a rounded coping, salmon brick floors, a low
    /// parapet with small chorten finials along T1's edge, stairs on every arm (the main one on the north axis with its
    /// gilt-yellow handrails and clay flower pots), and the walkable colliders;</item>
    /// <item>the two-step base ring (Ø 38 → 35.5) with the band of 108 Amitabha niches and red festoons; the white dome
    /// (Ø 33.5, apex 23.7 m) with twelve saffron scallop arcs under the pale yellow cap and their drips;</item>
    /// <item>the gilt harmika (7.0 m) in square plates with the Buddha's eyes on its four faces (white almond, two blue
    /// iris rings, black pupil and lid line, red-orange contour, dark brows meeting over the nose), the white "१" nose
    /// with its red spiral and the urna; the gilt cornice with the striped cloth valance and the red pleated drape;</item>
    /// <item>the thirteen square gilt steps (9.1 m, 6.6 → 2.4 m) in two rows of plates each; the neck, the canopy drum
    /// with its striped band and saffron skirt, the parasol cap and the gilt lantern-frame finial to 44.8 m;</item>
    /// <item>sixteen bundles of prayer-flag lines from under the canopy to the arm ends, the corners and the darchor poles
    /// on the compound wall (blue, white, red, green, yellow).</item>
    /// </list>
    /// The terrace dressing (chortens, statues, shrine, pots) is <see cref="BoudhaTerraces"/>.
    /// </summary>
    internal static class BoudhaStupa
    {
        // ---- Section (boudha.md 3.1.1), metres above the kora.
        public const double T1Top = 4.0, T2Top = 8.0, T3Top = 10.5;
        public const double RingR0 = 19.0, RingR1 = 17.75, RingStep = 11.25, RingTop = 12.0;
        public const double DomeR = 16.75, DomeApex = 23.7;
        public const double HarmikaBase = 23.0, HarmikaTop = 26.1, CorniceTop = 27.6, HarmikaHalf = 3.5, CorniceHalf = 3.85;
        public const double SpireTop = 36.7, SpireHalf0 = 3.3, SpireHalf1 = 1.2;
        public const double NeckTop = 37.1, DrumMid = 38.55, DrumTop = 40.0, CanopyTop = 40.6, FrameTop = 44.2, Tip = 44.8;
        public const int Steps = 13, Arcs = 12, Niches = 108;

        // ---- Terrace plans in the site frame (m from the stupa axis; the OSM outlines turned onto the 9° axes, every
        // vertex within 0.5 m). T1 (w56688295): body u −32.7..33.1, v −36.1..29.9; N/S arms u −20.9..20.5 to v +38.75 /
        // −40.7; E/W arms v −23.4..17.4 to u +40.65 / −40.45. T2 (w235084185): body ±25.75; arms 31 m wide to ±31.0-31.45.
        // T3 (w421288398): body ±20.6; arms 22.5 m wide to ±24.1-25.0 (the north arm's stair between two cheeks).

        /// <summary>The outline of terrace <paramref name="i"/> (0 = T1) into (pu, pv), counter-clockwise; returns 20.</summary>
        public static int Terrace(int i, double[] pu, double[] pv)
        {
            switch (i)
            {
                case 0: return BoudhaKit.CrossSquare(-32.7, 33.1, -36.1, 29.9, -0.2, 20.7, 38.75, -40.7, -3.0, 20.4, 40.65, -40.45, pu, pv);
                case 1: return BoudhaKit.CrossSquare(-25.7, 25.8, -25.7, 25.8, -0.35, 15.5, 31.0, -30.85, -0.1, 15.25, 31.1, -31.45, pu, pv);
                default: return BoudhaKit.CrossSquare(-20.55, 20.6, -20.6, 20.6, -1.0, 11.25, 24.1, -24.75, 0.2, 11.25, 24.8, -25.0, pu, pv);
            }
        }

        /// <summary>Top of terrace i above the kora.</summary>
        public static double TerraceTop(int i)
        {
            return i == 0 ? T1Top : i == 1 ? T2Top : T3Top;
        }

        /// <summary>The arm end of terrace i in direction q (0 N, 1 E, 2 S, 3 W): its distance from the axis along the
        /// direction and the arm's centre line offset across it.</summary>
        public static void ArmEnd(int i, int q, out double end, out double centre)
        {
            switch (i)
            {
                case 0:
                    end = q == 0 ? 38.75 : q == 1 ? 40.65 : q == 2 ? 40.7 : 40.45;
                    centre = q == 0 || q == 2 ? -0.2 : -3.0;
                    return;
                case 1:
                    end = q == 0 ? 31.0 : q == 1 ? 31.1 : q == 2 ? 30.85 : 31.45;
                    centre = q == 0 || q == 2 ? -0.35 : -0.1;
                    return;
                default:
                    end = q == 0 ? 24.1 : q == 1 ? 24.8 : q == 2 ? 24.75 : 25.0;
                    centre = q == 0 || q == 2 ? -1.0 : 0.2;
                    return;
            }
        }

        /// <summary>A frame on the site at (u, y, v) whose +z points outward in direction q (0 N, 1 E, 2 S, 3 W).</summary>
        public static Affine3 Dir(in Affine3 site, int q, double along, double y, double across)
        {
            // Direction q: N = +v, E = +u, S = −v, W = −u; "across" runs to the right of a viewer facing outward.
            double du = q == 1 ? 1 : q == 3 ? -1 : 0, dv = q == 0 ? 1 : q == 2 ? -1 : 0;
            double u = du * along + dv * across, v = dv * along - du * across;
            return BoudhaKit.Facing(site, u, y, v, du, dv);
        }

        // ------------------------------------------------------------------------------------------------ build

        public static void Build(MeshData m, GenColliders c, in BoudhaFrame f, int lod, SacredStats stats)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = f.Site;
            Terraces(m, c, f, lod);
            Stairs(m, c, f, lod);
            Ring(m, k, lod);
            Dome(m, k, lod);
            Harmika(m, k, lod);
            Spire(m, k, lod);
            Crown(m, k, lod);
            if (c != null)
            {
                // The ring, dome and tower: solid, not climbable.
                double x, y, z;
                k.Point(0, 0, 0, out x, out y, out z);
                c.AddCylinder(x, z, f.Level + T3Top, f.Level + RingStep, RingR0);
                c.AddCylinder(x, z, f.Level + RingStep, f.Level + RingTop, RingR1);
                c.AddCylinder(x, z, f.Level + RingTop, f.Level + DomeApex - 2.0, DomeR);
                c.AddBox(x, z, f.Level + HarmikaBase - 4, f.Level + SpireTop, CorniceHalf, CorniceHalf, Math.Cos(BoudhaSite.AxisBearingDeg * Math.PI / 180),
                         -Math.Sin(BoudhaSite.AxisBearingDeg * Math.PI / 180), GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Stone);
            }
            SacredDraw.Bake(m, v0, i0, f.Level);
            int fv = m.VertexCount, fi = m.IndexCount;
            int lines = Flags(m, f, lod);
            if (stats != null) stats.FlagLines = lines;
            SacredDraw.Bake(m, fv, fi, f.Level);
        }

        // ------------------------------------------------------------------------------------------------ terraces

        private static void Terraces(MeshData m, GenColliders c, in BoudhaFrame f, int lod)
        {
            Affine3 k = f.Site;
            double[] pu = BoudhaKit.U, pv = BoudhaKit.V;
            ShapeBrush wall = BoudhaKit.White, foot = BoudhaKit.B(BoudhaKit.Weathered, MaterialChannel.Plaster), cope = BoudhaKit.B(MeshColor.FromHex(0xF6F3EC), MaterialChannel.Plaster);
            ShapeBrush floor = BoudhaKit.B(BoudhaKit.TerraceFloor, MaterialChannel.Brick);
            for (int i = 0; i < 3; i++)
            {
                int n = Terrace(i, pu, pv);
                double y0 = i == 0 ? 0 : TerraceTop(i - 1), y1 = TerraceTop(i);
                Mould mo = SacredDraw.M;
                double sink = i == 0 ? 0.6 : 0.05;
                if (lod >= 2)
                {
                    mo.Add(0, y0 - sink, wall).Add(0, y1).Add(0.12, y1, cope);
                }
                else
                {
                    // Weathered base course, the white wall face, a rounded coping band.
                    mo.Add(0.07, y0 - sink, foot).Add(0.07, y0 + 0.32).Add(0.0, y0 + 0.36, wall, false).Add(0, y1 - 0.34).Add(0.1, y1 - 0.3, cope)
                      .Add(0.16, y1 - 0.18, false).Add(0.14, y1 - 0.06, false).Add(0.05, y1).Add(0, y1);
                }
                SacredDraw.Ring(m, k, pu, pv, n, mo);
                SacredDraw.Cap(m, k, pu, pv, n, 0, y1, true, floor);
                if (i == 0 && lod <= 1) Parapet(m, k, pu, pv, n, y1, lod);
                if (c != null)
                {
                    // Walkable body and arms (a cross is two overlapping boxes per axis plus the body).
                    AddCrossColliders(c, f, pu, pv, y0 - sink, y1);
                }
            }
        }

        /// <summary>
        /// T1's low parapet along its outer edge with the small chorten finials on it (corner/02, pair_3), edge by edge, left
        /// open where the compound stairs arrive on the north and south arm ends (<see cref="BoudhaTerraces.ParapetGap"/>).
        /// </summary>
        private static void Parapet(MeshData m, in Affine3 k, double[] pu, double[] pv, int n, double y1, int lod)
        {
            ShapeBrush wall = BoudhaKit.White, cope = BoudhaKit.B(MeshColor.FromHex(0xF6F3EC), MaterialChannel.Plaster);
            const double ph = 0.5, th = 0.36, step = 0.25;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double du = pu[j] - pu[i], dv = pv[j] - pv[i], l = Math.Sqrt(du * du + dv * dv);
                if (l < 0.05) continue;
                // The arm-end edges: north (points 7 → 8) and south (17 → 18) of the cross outline.
                int arm = i == 7 ? 0 : i == 17 ? 2 : -1;
                // Frame on the edge: x along it, +z inward (the parapet stands just inside the outer face).
                Affine3 e = SacredDraw.Basis(k, pu[i], y1, pv[i], du / l, 0, dv / l, 0, 1, 0);
                double a = 0;
                int pieces = (int)Math.Ceiling(l / step);
                bool open = false;
                for (int q = 0; q <= pieces; q++)
                {
                    double x = Math.Min(l, q * step);
                    double u = pu[i] + du * x / l, v = pv[i] + dv * x / l;
                    bool gap = arm >= 0 && BoudhaTerraces.ParapetGap(arm, u);
                    if (q == pieces || gap != open)
                    {
                        if (!open && x - a > 0.05)
                        {
                            SacredDraw.Box(m, e, a, x, -0.01, ph, 0.02, th, wall, BoxFaces.Front | BoxFaces.Back);
                            SacredDraw.Box(m, e, a - 0.04, x + 0.04, ph, ph + 0.06, -0.02, th + 0.04, cope, BoxFaces.All & ~BoxFaces.Bottom);
                            if (lod == 0)
                            {
                                int count = (int)((x - a) / 4.0);
                                for (int f = 0; f < count; f++)
                                {
                                    double fx = a + (f + 0.5) * (x - a) / count;
                                    BoudhaKit.FinialChorten(m, SacredDraw.At(e, fx, ph + 0.06, 0.5 * th), 0.75, lod);
                                }
                            }
                        }
                        a = x;
                        open = gap;
                    }
                }
            }
        }

        private static void AddCrossColliders(GenColliders c, in BoudhaFrame f, double[] pu, double[] pv, double y0, double y1)
        {
            // Points of CrossSquare: 0 (u1, v0), 5 (u1, v1), 10 (u0, v1), 15 (u0, v0) are the body corners; 2..3 the E arm
            // end, 7..8 N, 12..13 W, 17..18 S. The body as a true square on its centre (the measured body is square to
            // 0.3 %: the shorter half both ways, so the box never overhangs the edge), the cross as its two arms end to
            // end (N-S and E-W, each one strip from arm end to arm end; opposite arms share their centre line and width).
            double u0 = pu[10], u1 = pu[0], v0 = pv[0], v1 = pv[5], half = 0.5 * Math.Min(u1 - u0, v1 - v0), cu = 0.5 * (u0 + u1), cv = 0.5 * (v0 + v1);
            Box(c, f, cu - half, cu + half, cv - half, cv + half, y0, y1);
            Box(c, f, Math.Max(pu[8], pu[17]), Math.Min(pu[7], pu[18]), pv[17], pv[7], y0, y1); // N-S arms
            Box(c, f, pu[12], pu[2], Math.Max(pv[2], pv[13]), Math.Min(pv[3], pv[12]), y0, y1); // E-W arms
        }

        /// <summary>A walkable site-aligned box over site-local u0..u1 × v0..v1 (absolute y0..y1 above the kora).</summary>
        internal static void Box(GenColliders c, in BoudhaFrame f, double u0, double u1, double v0, double v1, double y0, double y1)
        {
            if (u1 < u0)
            {
                double t = u0;
                u0 = u1;
                u1 = t;
            }
            if (v1 < v0)
            {
                double t = v0;
                v0 = v1;
                v1 = t;
            }
            double x, z;
            f.ToTile(0.5 * (u0 + u1), 0.5 * (v0 + v1), out x, out z);
            double a = BoudhaSite.AxisBearingDeg * Math.PI / 180.0;
            c.AddBox(x, z, f.Level + y0, f.Level + y1, 0.5 * (u1 - u0), 0.5 * (v1 - v0), Math.Cos(a), -Math.Sin(a), GenColliderFlags.Walkable, GenColliders.Brick);
        }

        // ------------------------------------------------------------------------------------------------ stairs

        private static void Stairs(MeshData m, GenColliders c, in BoudhaFrame f, int lod)
        {
            Affine3 k = f.Site;
            ShapeBrush tread = BoudhaKit.B(MeshColor.FromHex(0xE4DCD3), MaterialChannel.Stone), cheek = BoudhaKit.White;
            ShapeBrush rail = BoudhaKit.B(MeshColor.FromHex(0xE8C23A), MaterialChannel.Metal);
            for (int q = 0; q < 4; q++)
            {
                // T1 → T2 on every arm; T2 → T3; T3 → the base ring.
                for (int i = 0; i < 3; i++)
                {
                    double endUpper, cUpper, endLower, cLower;
                    ArmEnd(i == 0 ? 1 : 2, q, out endUpper, out cUpper);
                    double yLow = TerraceTop(i), yHigh = i == 0 ? T2Top : i == 1 ? T3Top : RingTop;
                    if (i == 2)
                    {
                        endUpper = RingR0 - 0.05;
                        cUpper = 0;
                    }
                    ArmEnd(i, q, out endLower, out cLower);
                    double rise = yHigh - yLow, run = Math.Min(rise / Math.Tan(37 * Math.PI / 180), endLower - endUpper - 0.9);
                    double halfW = q == 0 ? 1.95 : 1.5;
                    double across = q == 0 ? -0.5 : (i == 2 ? 0 : cUpper);
                    // Frame at the foot, +z running inward (up the flight).
                    Affine3 outward = Dir(k, q, endUpper + run, 0, q == 1 || q == 2 ? -across : across);
                    Affine3 inward = outward * Affine3.Yaw(180);
                    BoudhaKit.Flight(m, c, inward, halfW, run, yLow, yHigh, 0.35, 0.55, tread, cheek, lod >= 1 ? 2 : 0);
                    if (q == 0 && lod <= 1)
                    {
                        BoudhaKit.Handrail(m, inward, -halfW + 0.15, run, yLow, yHigh, 0.9, rail, lod);
                        BoudhaKit.Handrail(m, inward, halfW - 0.15, run, yLow, yHigh, 0.9, rail, lod);
                    }
                    if (q == 0 && lod == 0) Pots(m, inward, halfW + 0.35, run, yLow, yHigh);
                }
            }
        }

        /// <summary>Clay flower pots with green plants up both cheeks of a flight (earthquake/06, around/03).</summary>
        private static void Pots(MeshData m, in Affine3 f, double x, double run, double y0, double y1)
        {
            ShapeBrush clay = BoudhaKit.B(BoudhaKit.Terracotta, MaterialChannel.Dirt), leaf = BoudhaKit.B(BoudhaKit.Leaf, MaterialChannel.Foliage);
            int n = Math.Max(2, (int)(run / 1.1));
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < n; i++)
                {
                    double t = (i + 0.5) / n, z = run * t, y = y0 + (y1 - y0) * t + 0.55;
                    Affine3 p = SacredDraw.At(f, s * (x + 0.2), y, z);
                    Shapes.Frustum(m, p, clay, 0.13, 0.2, 0.3, 6, 0, 0, false, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(p, 0, 0.42, 0), leaf, 0.24, 0.2, 0.24, 5, false);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ ring + dome

        private static void Ring(MeshData m, in Affine3 k, int lod)
        {
            int seg = lod == 0 ? 72 : lod == 1 ? 32 : 24;
            ShapeBrush w = BoudhaKit.White, sh = BoudhaKit.B(BoudhaKit.Weathered, MaterialChannel.Plaster);
            Profile2 p = BoudhaKit.Prof;
            p.Add(0, T3Top - 0.05).Add(RingR0, T3Top - 0.05, true).Add(RingR0, RingStep - 0.08, true).Add(RingR0 + 0.06, RingStep - 0.04, false)
             .Add(RingR0 - 0.02, RingStep, true).Add(RingR1, RingStep, true).Add(RingR1, RingTop, true).Add(DomeR, RingTop, true);
            Shapes.Lathe(m, k, w, p, seg);
            if (lod >= 2) return;
            // The 108 niches round the upper step (spire/06, dome/05): small dark square recesses in the white band, each with
            // a tiny gilt Amitabha, a slightly greyer white frame, and between them small cloth pennants in the five colours.
            ShapeBrush frame = BoudhaKit.WhiteShade, dark = BoudhaKit.B(BoudhaKit.NicheDark, MaterialChannel.Plain, 0.55f);
            ShapeBrush fig = BoudhaKit.B(BoudhaKit.GiltShade, MaterialChannel.Gilt, 0.8f);
            int count = lod == 0 ? Niches : Niches / 2;
            double nh = 0.5, nw = 0.42, yb = RingStep + 0.14;
            for (int j = 0; j < count; j++)
            {
                double a = 360.0 * (j + 0.5) / count;
                Affine3 nf = SacredDraw.At(k * Affine3.Yaw(a), 0, 0, RingR1);
                SacredDraw.Box(m, nf, -0.5 * nw - 0.06, 0.5 * nw + 0.06, yb - 0.06, yb + nh + 0.06, -0.02, 0.04, frame, BoxFaces.Front | BoxFaces.Top);
                SacredDraw.Panel(m, nf, -0.5 * nw, yb, 0.5 * nw, yb + nh, 0.045, dark);
                if (lod == 0)
                {
                    // The seated Amitabha in the niche: a lap and body and the head, as flat gilt reliefs.
                    SacredDraw.Quad(m, nf, -0.13, yb + 0.04, 0.05, 0.13, yb + 0.04, 0.05, 0.07, yb + 0.26, 0.05, -0.07, yb + 0.26, 0.05, 0, 0, 1, fig);
                    SacredDraw.Quad(m, nf, -0.045, yb + 0.27, 0.052, 0.045, yb + 0.27, 0.052, 0.045, yb + 0.36, 0.052, -0.045, yb + 0.36, 0.052, 0, 0, 1, fig);
                    // A pennant hanging from the dome foot over the pier after this niche (both faces).
                    Affine3 pf = SacredDraw.At(k * Affine3.Yaw(360.0 * (j + 1.0) / count), 0, 0, RingR1 + 0.06);
                    ShapeBrush cl = BoudhaKit.B(SacredPalette.Flags[j % 5], MaterialChannel.Fabric);
                    for (int s = -1; s <= 1; s += 2)
                        SacredDraw.Tri(m, pf, -0.13, RingTop - 0.02, 0.0, 0.13, RingTop - 0.02, 0.0, 0.0, RingTop - 0.42, 0.04, 0, 0, s, cl);
                }
            }
        }

        /// <summary>A point on the dome (a flattened half ellipse of radius r and rise h on y = y0) at compass angle a
        /// (radians, from +v toward +u) and elevation fraction e (0 foot, 1 apex), lifted <paramref name="lift"/> along
        /// its normal.</summary>
        internal static void DomePoint(double a, double e, double lift, out double u, out double y, out double v, out double nu, out double ny, out double nv)
        {
            double r = DomeR, h = DomeApex - RingTop, phi = 0.5 * Math.PI * e, cr = Math.Cos(phi), sr = Math.Sin(phi);
            double su = Math.Sin(a), sv = Math.Cos(a);
            double gr = cr / r, gy = sr / h, gl = Math.Sqrt(gr * gr + gy * gy);
            nu = gr / gl * su;
            nv = gr / gl * sv;
            ny = gy / gl;
            u = r * cr * su + nu * lift;
            v = r * cr * sv + nv * lift;
            y = RingTop + h * sr + ny * lift;
        }

        // The saffron "lotus" painting (boudha.md 3.1.4; spire/06, dome/05, corner/02): a ring of twelve round-topped white
        // lobes outlined in saffron, like an arcade round the dome. Each lobe's arch springs at about 36 % of the dome's
        // height and crowns at about 64 %; between two lobes the saffron line runs straight down the dome as a leg to just
        // above the 108-niche band. Above the arcade the dome is washed a pale yellow, deepening to a yellow cap under the
        // harmika whose lower edge scallops in a second, smaller row of lobes (offset by half a lobe). The constants are
        // elevation fractions e of the dome's ellipse (height = rise · sin(e · 90°)).
        private const double ArcSpring = 0.234, ArcCrown = 0.442, LegFoot = 0.051, CapSpring = 0.659, CapCrown = 0.76;

        /// <summary>The arcade line: elevation fraction of a lobe's arch at fraction t across it (0, 1 at the legs).</summary>
        internal static double ArcLine(double t)
        {
            return ArcSpring + (ArcCrown - ArcSpring) * Math.Pow(Math.Sin(Math.PI * Math.Max(0, Math.Min(1, t))), 0.45);
        }

        /// <summary>The scalloped lower edge of the yellow cap at fraction t across a lobe (offset half a lobe from the
        /// arcade).</summary>
        private static double CapLine(double t)
        {
            return CapSpring + (CapCrown - CapSpring) * Math.Pow(Math.Sin(Math.PI * Math.Max(0, Math.Min(1, t))), 0.6);
        }

        /// <summary>Dome rows (elevation fractions): dense through the arcade and the cap edge so the wash follows them.</summary>
        private static readonly double[] RowsLod0 =
            { 0, 0.03, 0.08, 0.14, 0.2, 0.24, 0.28, 0.32, 0.36, 0.4, 0.44, 0.48, 0.54, 0.6, 0.64, 0.68, 0.72, 0.76, 0.8, 0.86, 0.93, 1.0 };

        private static readonly double[] RowsLod1 = { 0, 0.12, 0.24, 0.32, 0.4, 0.48, 0.6, 0.68, 0.78, 1.0 };

        private static readonly double[] RowsLod2 = { 0, 0.15, 0.3, 0.45, 0.66, 0.8, 1.0 };

        private static readonly uint PaleWash = MeshColor.FromHex(0xF0D894), CapWash = MeshColor.FromHex(0xEDC862);

        private static void Dome(MeshData m, in Affine3 k, int lod)
        {
            int seg = lod == 0 ? 72 : lod == 1 ? 32 : 24;
            double[] rows = lod == 0 ? RowsLod0 : lod == 1 ? RowsLod1 : RowsLod2;
            // The dome as a grid so the washes can follow the arcade vertex by vertex.
            int first = m.VertexCount;
            for (int r = 0; r < rows.Length; r++)
            {
                double e = rows[r];
                for (int j = 0; j <= seg; j++)
                {
                    double a = 2 * Math.PI * j / seg;
                    double u, y, v, nu, ny, nv;
                    DomePoint(a, e, 0, out u, out y, out v, out nu, out ny, out nv);
                    double t = (a * Arcs / (2 * Math.PI)) % 1.0, tc = (t + 0.5) % 1.0;
                    uint col = BoudhaKit.DomeWhite;
                    if (e > CapLine(tc) - 0.005) col = CapWash;
                    else if (e > ArcLine(t) + 0.005) col = lod >= 2 ? CapWash : PaleWash;
                    else col = MeshColor.Lerp(MeshColor.FromHex(0xE8E2D4), col, (float)Math.Min(1, e * 4));
                    SacredDraw.V(m, k, u, y, v, nu, ny, nv, BoudhaKit.B(col, MaterialChannel.Plaster));
                }
            }
            SacredDraw.Grid(m, first, rows.Length, seg + 1, false, null);
            if (lod >= 2) return;
            ShapeBrush sb = BoudhaKit.B(BoudhaKit.SaffronArc, MaterialChannel.Paint);
            ShapeBrush soft = BoudhaKit.B(MeshColor.FromHex(0xEACB86), MaterialChannel.Paint);
            int segs = lod == 0 ? 16 : 8;
            for (int j = 0; j < Arcs; j++)
            {
                double a0 = 2 * Math.PI * j / Arcs, a1 = 2 * Math.PI * (j + 1) / Arcs;
                // The arch over lobe j (a ribbon of constant width along the arcade line, lifted off the surface).
                DomeRibbon(m, k, a0, a1, segs, 0.45, 0.03, false, sb);
                // The leg at a0 down the dome from the springing to the niche band, wider where the two arches meet.
                Leg(m, k, a0, ArcSpring + 0.02, LegFoot, 0.62, 0.4, lod == 0 ? 6 : 2, 0.03, sb);
                if (lod == 0)
                {
                    // The softer saffron line of the cap's scallops, and two pale drips down each lobe from its crown.
                    DomeRibbon(m, k, a0 - 0.5 * (a1 - a0), a0 + 0.5 * (a1 - a0), 10, 0.35, 0.025, true, soft);
                    for (int d = -1; d <= 1; d += 2)
                        Leg(m, k, 0.5 * (a0 + a1) + d * 0.12 * (a1 - a0), ArcCrown - 0.02, ArcCrown - 0.12, 0.16, 0.05, 3, 0.02, soft);
                }
            }
        }

        /// <summary>A painted ribbon along the arcade line (or the cap's scallop line when <paramref name="cap"/>) from
        /// compass angle a0 to a1, <paramref name="width"/> wide, lifted <paramref name="lift"/> off the dome.</summary>
        private static void DomeRibbon(MeshData m, in Affine3 k, double a0, double a1, int segs, double width, double lift, bool cap, in ShapeBrush b)
        {
            int f0 = m.VertexCount;
            for (int q = 0; q <= segs; q++)
            {
                double t = (double)q / segs, a = a0 + (a1 - a0) * t, e = cap ? CapLine(t) : ArcLine(t);
                double u, y, v, nu, ny, nv, ua, ya, va, ub, yb, vb, n1, n2, n3;
                DomePoint(a, e, lift, out u, out y, out v, out nu, out ny, out nv);
                double tp = Math.Max(0, t - 0.01), tn = Math.Min(1, t + 0.01);
                DomePoint(a0 + (a1 - a0) * tp, cap ? CapLine(tp) : ArcLine(tp), lift, out ua, out ya, out va, out n1, out n2, out n3);
                DomePoint(a0 + (a1 - a0) * tn, cap ? CapLine(tn) : ArcLine(tn), lift, out ub, out yb, out vb, out n1, out n2, out n3);
                double tu = ub - ua, ty = yb - ya, tv = vb - va;
                double cu = ny * tv - nv * ty, cy = nv * tu - nu * tv, cv = nu * ty - ny * tu, cl = Math.Sqrt(cu * cu + cy * cy + cv * cv);
                if (cl < 1e-9) cl = 1;
                cu *= 0.5 * width / cl;
                cy *= 0.5 * width / cl;
                cv *= 0.5 * width / cl;
                SacredDraw.V(m, k, u - cu, y - cy, v - cv, nu, ny, nv, b);
                SacredDraw.V(m, k, u + cu, y + cy, v + cv, nu, ny, nv, b);
            }
            SacredDraw.Grid(m, f0, segs + 1, 2, false, null);
        }

        /// <summary>A painted stripe down the dome's meridian at compass angle <paramref name="a"/> from elevation e0 down
        /// to e1, <paramref name="w0"/> wide at the top narrowing to <paramref name="w1"/>.</summary>
        private static void Leg(MeshData m, in Affine3 k, double a, double e0, double e1, double w0, double w1, int segs, double lift, in ShapeBrush b)
        {
            int f0 = m.VertexCount;
            for (int q = 0; q <= segs; q++)
            {
                double t = (double)q / segs, e = e0 + (e1 - e0) * t, w = w0 + (w1 - w0) * Math.Min(1, 3 * t);
                double r = Math.Max(0.5, DomeR * Math.Cos(0.5 * Math.PI * e)), da = 0.5 * w / r;
                for (int s = -1; s <= 1; s += 2)
                {
                    double u, y, v, nu, ny, nv;
                    DomePoint(a + s * da, e, lift, out u, out y, out v, out nu, out ny, out nv);
                    SacredDraw.V(m, k, u, y, v, nu, ny, nv, b);
                }
            }
            SacredDraw.Grid(m, f0, segs + 1, 2, false, null);
        }

        // ------------------------------------------------------------------------------------------------ harmika

        private static void Harmika(MeshData m, in Affine3 k, int lod)
        {
            double hb = HarmikaBase - 0.6, hw = HarmikaHalf;
            ShapeBrush g = BoudhaKit.B(BoudhaKit.GiltPanel, MaterialChannel.Gilt), gl = BoudhaKit.B(BoudhaKit.GiltLit, MaterialChannel.Gilt);
            ShapeBrush line = BoudhaKit.GiltDark;
            // The eye block, its gilt cornice (projecting 0.35 m) and the frieze above it that carries the cloth.
            Mould mo = SacredDraw.M;
            if (lod >= 2)
            {
                mo.Add(0, hb, g).Add(0, HarmikaTop).Add(CorniceHalf - hw, HarmikaTop, gl).Add(CorniceHalf - hw, CorniceTop);
            }
            else
            {
                mo.Add(0, hb, g).Add(0, HarmikaTop - 0.05).Add(0.12, HarmikaTop, gl).Add(0.38, HarmikaTop + 0.12).Add(0.38, HarmikaTop + 0.3)
                  .Add(CorniceHalf - hw, HarmikaTop + 0.34, g).Add(CorniceHalf - hw, CorniceTop - 0.2).Add(CorniceHalf - hw + 0.12, CorniceTop - 0.1, gl)
                  .Add(CorniceHalf - hw + 0.12, CorniceTop);
            }
            SacredDraw.RectRing(m, k, 0, 0, hw, hw, mo, true, gl);
            if (lod >= 2)
            {
                for (int s = 0; s < 4; s++)
                {
                    Affine3 face = k * Affine3.Yaw(90 * s);
                    SacredDraw.Panel(m, face, -0.72 * hw, 24.35, -0.12 * hw, 24.85, hw + 0.02, BoudhaKit.B(0xFFFFFFFFu, MaterialChannel.Paint));
                    SacredDraw.Panel(m, face, 0.12 * hw, 24.35, 0.72 * hw, 24.85, hw + 0.02, BoudhaKit.B(0xFFFFFFFFu, MaterialChannel.Paint));
                    SacredDraw.Panel(m, face, -hw - 0.4, CorniceTop - 1.4, hw + 0.4, CorniceTop - 0.4, hw + 0.42, BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Fabric));
                }
                return;
            }
            for (int s = 0; s < 4; s++)
            {
                Affine3 face = k * Affine3.Yaw(90 * s);
                // Square gilt plates (about 0.45 m): the joints as darker lines.
                if (lod == 0) BoudhaKit.GridLines(m, face, -hw, hw, HarmikaBase, HarmikaTop - 0.05, hw + 0.004, 0.45, 0.45, 0.035, line);
                Eyes(m, face, hw + 0.01, lod);
                // The cloth on the frieze: thin stripes (blue, yellow, green over white) and the red pleated drape with its
                // green frill hanging over the top of the eye block (eyes/00, general3/00).
                double zc = CorniceHalf + 0.02, x0 = -CorniceHalf - 0.02, x1 = CorniceHalf + 0.02;
                uint[] stripes = { BoudhaKit.ClothBlue, BoudhaKit.ClothYellow, BoudhaKit.ClothRed, BoudhaKit.ClothGreen };
                double hem = BoudhaKit.Valance(m, face, x0, x1, zc, CorniceTop - 0.12, stripes, 0.11, BoudhaKit.ClothRed, 0.85, 0.45, lod);
                // Green ruffled frill at the hem.
                BoudhaKit.Valance(m, face, x0, x1, zc + 0.06, hem + 0.08, new uint[0], 0, BoudhaKit.ClothGreen, 0.22, 0.25, lod);
            }
        }

        /// <summary>The eyes on one harmika face (the frame's +z face at z = <paramref name="z"/>; boudha.md 3.1.5).</summary>
        private static void Eyes(MeshData m, in Affine3 f, double z, int lod)
        {
            int seg = lod == 0 ? 12 : 6;
            // Big, wide-open eyes filling the face between the dome and the valance (spire/06, eyes/00): each about 0.27
            // of the face wide, the brows close under the drape's frill.
            double ye = HarmikaBase + 0.57 * (HarmikaTop - HarmikaBase), ew = 0.95, eh = 0.36;
            ShapeBrush white = BoudhaKit.B(0xFFFFFFFFu, MaterialChannel.Paint), red = BoudhaKit.B(BoudhaKit.EyeRed, MaterialChannel.Paint);
            ShapeBrush blueD = BoudhaKit.B(BoudhaKit.EyeBlueDark, MaterialChannel.Paint), blueL = BoudhaKit.B(BoudhaKit.EyeBlueLight, MaterialChannel.Paint);
            ShapeBrush ink = BoudhaKit.B(BoudhaKit.Ink, MaterialChannel.Paint), brow = BoudhaKit.B(BoudhaKit.Brow, MaterialChannel.Paint);
            for (int s = -1; s <= 1; s += 2)
            {
                double xc = s * 1.35;
                // Red-orange contour round the almond (wider, pointed at the outer corner), then the white almond.
                Almond(m, f, xc + s * 0.06, ye - 0.02, z, ew + 0.16, eh + 0.12, 0.85 * eh + 0.16, s, seg, red);
                Almond(m, f, xc, ye, z + 0.004, ew, eh, eh * 0.85, s, seg, white);
                // Iris: dark blue ring, light blue, black pupil; looking slightly down.
                Disc(m, f, xc, ye - 0.05, z + 0.008, 0.3, seg, blueD);
                Disc(m, f, xc, ye - 0.05, z + 0.011, 0.22, seg, blueL);
                Disc(m, f, xc, ye - 0.05, z + 0.014, 0.12, Math.Max(6, seg - 4), ink);
                // Black upper lid line and the thick dark brow sweeping up and out, meeting the other over the nose.
                Arc(m, f, xc, ye + 0.02, z + 0.016, ew + 0.04, eh + 0.04, 0.08, s, seg, ink);
                Arc(m, f, xc + s * 0.1, ye + 0.5, z + 0.006, ew + 0.36, 0.3, 0.18, s, seg, brow);
            }
            // The nose "१": a white teardrop with a red spiral; the urna dot between the brows.
            double ny = ye - 0.8;
            Disc(m, f, 0, ny, z + 0.004, 0.34, seg, white);
            Spiral(m, f, 0, ny, z + 0.008, 0.27, lod == 0 ? 28 : 14, 0.055, red);
            Disc(m, f, 0, ye + 0.6, z + 0.006, 0.1, seg, blueD);
            Disc(m, f, 0, ye + 0.6, z + 0.009, 0.055, seg, red);
        }

        /// <summary>An almond on plane z: half width hw, the upper lid up by <paramref name="up"/>, the lower lid down
        /// by <paramref name="down"/>; the outer corner (side s) a little sharper and lower.</summary>
        internal static void Almond(MeshData m, in Affine3 f, double xc, double yc, double z, double hw, double up, double down, int s, int seg, in ShapeBrush b)
        {
            int c = SacredDraw.V(m, f, xc, yc, z, 0, 0, 1, b), first = m.VertexCount;
            for (int i = 0; i < 2 * seg; i++)
            {
                double t = (double)i / seg, x, y;
                if (t <= 1)
                {
                    x = -hw + 2 * hw * t;
                    y = up * Math.Pow(Math.Sin(Math.PI * t), 0.85);
                }
                else
                {
                    x = hw - 2 * hw * (t - 1);
                    y = -down * Math.Pow(Math.Sin(Math.PI * (t - 1)), 1.1);
                }
                // Outer corner drawn down and out a touch.
                double outer = s * x > 0 ? s * x / hw : 0;
                y -= 0.05 * outer * outer;
                SacredDraw.V(m, f, xc + x, yc + y, z, 0, 0, 1, b);
            }
            for (int i = 0; i < 2 * seg; i++) SacredDraw.Tri(m, c, first + i, first + (i + 1) % (2 * seg));
        }

        internal static void Disc(MeshData m, in Affine3 f, double xc, double yc, double z, double rad, int seg, in ShapeBrush b)
        {
            int c = SacredDraw.V(m, f, xc, yc, z, 0, 0, 1, b), first = m.VertexCount;
            for (int i = 0; i < seg; i++)
            {
                double a = 2 * Math.PI * i / seg;
                SacredDraw.V(m, f, xc + rad * Math.Cos(a), yc + rad * Math.Sin(a), z, 0, 0, 1, b);
            }
            for (int i = 0; i < seg; i++) SacredDraw.Tri(m, c, first + i, first + (i + 1) % seg);
        }

        /// <summary>A crescent stroke over an eye: half width hw, rising <paramref name="rise"/>, thickness th thinning
        /// toward the inner end and sweeping up at the outer end (side s).</summary>
        internal static void Arc(MeshData m, in Affine3 f, double xc, double yc, double z, double hw, double rise, double th, int s, int seg, in ShapeBrush b)
        {
            int first = m.VertexCount;
            for (int i = 0; i <= seg; i++)
            {
                double t = (double)i / seg, x = -hw + 2 * hw * t, y = rise * Math.Sin(Math.PI * t);
                double outer = s * x > 0 ? s * x / hw : 0, tt = th * (0.45 + 0.55 * Math.Sin(Math.PI * Math.Min(1, t * 1.1)));
                y += 0.06 * outer * outer;
                SacredDraw.V(m, f, xc + x, yc + y, z, 0, 0, 1, b);
                SacredDraw.V(m, f, xc + x, yc + y + tt, z, 0, 0, 1, b);
            }
            SacredDraw.Grid(m, first, seg + 1, 2, false, null);
        }

        /// <summary>A flat spiral stroke (the red spiral in the nose).</summary>
        internal static void Spiral(MeshData m, in Affine3 f, double xc, double yc, double z, double r, int n, double w, in ShapeBrush b)
        {
            int first = m.VertexCount;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / (n - 1), a = 2.6 * Math.PI * t, rr = r * (0.12 + 0.88 * t);
                double x = xc + rr * Math.Cos(a), y = yc + rr * Math.Sin(a);
                double ox = Math.Cos(a) * 0.5 * w, oy = Math.Sin(a) * 0.5 * w;
                SacredDraw.V(m, f, x - ox, y - oy, z, 0, 0, 1, b);
                SacredDraw.V(m, f, x + ox, y + oy, z, 0, 0, 1, b);
            }
            SacredDraw.Grid(m, first, n, 2, false, null);
        }

        // ------------------------------------------------------------------------------------------------ spire

        private static void Spire(MeshData m, in Affine3 k, int lod)
        {
            double step = (SpireTop - CorniceTop) / Steps;
            ShapeBrush g = BoudhaKit.B(BoudhaKit.GiltBright, MaterialChannel.Gilt), gs = BoudhaKit.B(BoudhaKit.GiltShade, MaterialChannel.Gilt);
            ShapeBrush line = BoudhaKit.B(BoudhaKit.GiltShade, MaterialChannel.Gilt, 0.85f);
            if (lod >= 2)
            {
                Shapes.Frustum(m, SacredDraw.At(k * Affine3.Yaw(45), 0, CorniceTop, 0), g, SpireHalf0 * Math.Sqrt(2), SpireHalf1 * Math.Sqrt(2), SpireTop - CorniceTop, 4, 0,
                               0, false, true);
                return;
            }
            for (int q = 0; q < Steps; q++)
            {
                double half = SpireHalf0 + (SpireHalf1 - SpireHalf0) * q / Steps, y = CorniceTop + q * step, set = (SpireHalf0 - SpireHalf1) / Steps;
                Mould mo = SacredDraw.M;
                mo.Add(0, y, g).Add(0, y + step - 0.08).Add(-0.03, y + step - 0.04, gs).Add(-set, y + step - 0.02, g).Add(-set, y + step);
                SacredDraw.RectRing(m, k, 0, 0, half, half, mo, q == Steps - 1, g);
                if (lod == 0)
                {
                    // Two rows of square plates on every riser.
                    for (int s = 0; s < 4; s++)
                    {
                        Affine3 face = k * Affine3.Yaw(90 * s);
                        BoudhaKit.GridLines(m, face, -half, half, y + 0.02, y + step - 0.1, half + 0.004, 0.36, 0.5 * (step - 0.12), 0.03, line);
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ crown

        /// <summary>
        /// The crown over the spire (crowns of spire/06, dome/05, general3/00): a short gilt neck; the canopy (chhatra), a
        /// broad gilt barrel drum Ø 4.4 with a repoussé fringe band and rounded top rim, wrapped low by a striped cloth band
        /// (white, blue, red) over the saffron skirt that hangs down over the neck; on the drum's low domed top the gilt
        /// finial bulb (a bell with rings and a pinnacle), all held in the tall four-legged gilt lantern frame whose legs
        /// rise from the drum's rim to a small parasol and the spike at 44.8 m.
        /// </summary>
        private static void Crown(MeshData m, in Affine3 k, int lod)
        {
            int seg = lod == 0 ? 32 : lod == 1 ? 12 : 8;
            ShapeBrush g = BoudhaKit.B(BoudhaKit.GiltBright, MaterialChannel.Gilt), gl = BoudhaKit.B(BoudhaKit.GiltLit, MaterialChannel.Gilt);
            ShapeBrush gs = BoudhaKit.B(BoudhaKit.GiltShade, MaterialChannel.Gilt, 0.85f);
            // Neck on the top step.
            Profile2 p = BoudhaKit.Prof;
            p.Add(0, SpireTop).Add(1.0, SpireTop, true).Add(0.9, SpireTop + 0.12).Add(0.9, NeckTop, true).Add(0, NeckTop);
            Shapes.Lathe(m, k, g, p, seg);
            // The drum: a gilt barrel with a rounded top rim and a low domed top.
            Profile2 d = BoudhaKit.Prof;
            if (lod >= 2) d.Add(0, NeckTop).Add(2.1, NeckTop, true).Add(2.3, DrumMid).Add(2.15, DrumTop, true).Add(1.0, DrumTop + 0.35).Add(0, DrumTop + 0.4);
            else d.Add(0, NeckTop).Add(2.02, NeckTop, true).Add(2.18, NeckTop + 0.25).Add(2.28, DrumMid).Add(2.26, DrumTop - 0.45).Add(2.18, DrumTop - 0.12)
                  .Add(2.0, DrumTop + 0.02).Add(1.55, DrumTop + 0.18).Add(1.0, DrumTop + 0.33).Add(0.5, DrumTop + 0.4).Add(0, DrumTop + 0.42);
            if (lod <= 1) d.Smooth(1);
            Shapes.Lathe(m, k, gl, d, seg);
            if (lod <= 1)
            {
                // Repoussé mouldings: two raised rings and, between them, the fringe of small pendant scallops.
                Shapes.Torus(m, SacredDraw.At(k, 0, DrumMid + 0.15, 0), g, 2.29, 0.06, seg, 4);
                Shapes.Torus(m, SacredDraw.At(k, 0, DrumTop - 0.3, 0), g, 2.24, 0.05, seg, 4);
                if (lod == 0)
                {
                    for (int q = 0; q < 28; q++)
                    {
                        double a = 2 * Math.PI * (q + 0.5) / 28;
                        // A pendant leaf facing out, tangent to the barrel (a diamond pointing down).
                        Affine3 pf = SacredDraw.Basis(k, 2.31 * Math.Sin(a), DrumMid + 0.05, 2.31 * Math.Cos(a), Math.Cos(a), 0, -Math.Sin(a), 0, 1, 0);
                        SacredDraw.Quad(m, pf, 0, 0.08, 0, 0.13, -0.08, 0, 0, -0.32, 0, -0.13, -0.08, 0, 0, 0, 1, gs);
                    }
                }
            }
            // The cloth: a striped band low round the drum (white, blue, white, red) over the saffron skirt that hangs down
            // over the neck and the top step.
            uint[] bands = { BoudhaKit.ClothWhite, BoudhaKit.ClothBlue, BoudhaKit.ClothWhite, BoudhaKit.ClothRed };
            double y = NeckTop + 1.2, bandH = 0.13;
            for (int i = 0; i < bands.Length; i++)
            {
                // Far off the stripes merge into one blue band.
                if (lod >= 2 && i != 1) continue;
                Profile2 bp = BoudhaKit.Prof;
                double r = 2.3 + 0.012 * i;
                if (lod >= 2) bp.Add(r, y - bandH * bands.Length).Add(r, y);
                else bp.Add(r, y - bandH * (i + 1)).Add(r, y - bandH * i);
                Shapes.Lathe(m, k, BoudhaKit.B(bands[i], MaterialChannel.Fabric), bp, seg);
            }
            double skirtTop = y - bandH * bands.Length, skirtBot = SpireTop - 0.35;
            if (lod == 0)
            {
                // Folds: a ring grid whose radius waves round the circumference, the hem scalloped.
                int folds = 24, cols = folds * 2, rowsN = 4, first = m.VertexCount;
                ShapeBrush sf = BoudhaKit.B(BoudhaKit.ClothSaffron, MaterialChannel.Fabric);
                for (int r = 0; r <= rowsN; r++)
                {
                    double t = (double)r / rowsN, yy = skirtTop + (skirtBot - skirtTop) * t, rr = 2.33 + 0.3 * t;
                    for (int j = 0; j <= cols; j++)
                    {
                        double a = 2 * Math.PI * j / cols, wob = (j % 2 == 0 ? 1 : -1) * 0.07 * t;
                        double hem = r == rowsN && j % 2 == 1 ? 0.12 : 0;
                        double nu = Math.Sin(a), nv = Math.Cos(a);
                        SacredDraw.V(m, k, (rr + wob) * nu, yy + hem, (rr + wob) * nv, nu, 0.25, nv, sf);
                    }
                }
                SacredDraw.Grid(m, first, rowsN + 1, cols + 1, false, null);
            }
            else
            {
                Profile2 sk = BoudhaKit.Prof;
                sk.Add(2.63, skirtBot).Add(2.33, skirtTop + 0.02);
                Shapes.Lathe(m, k, BoudhaKit.B(BoudhaKit.ClothSaffron, MaterialChannel.Fabric), sk, seg);
            }
            // The finial bulb on the drum top: a lotus foot, the bell, two rings and the pinnacle.
            Profile2 fn = BoudhaKit.Prof;
            double yf = DrumTop + 0.38;
            if (lod >= 2) fn.Add(0, yf).Add(0.75, yf, true).Add(0.55, yf + 0.8).Add(0.25, yf + 1.4).Add(0.1, yf + 2.2).Add(0, FrameTop);
            else fn.Add(0, yf).Add(0.78, yf, true).Add(0.8, yf + 0.12).Add(0.62, yf + 0.22, true).Add(0.66, yf + 0.55).Add(0.56, yf + 0.9).Add(0.36, yf + 1.12)
                  .Add(0.4, yf + 1.2, true).Add(0.26, yf + 1.3).Add(0.3, yf + 1.4, true).Add(0.18, yf + 1.5).Add(0.2, yf + 1.75).Add(0.1, yf + 2.0).Add(0.07, yf + 2.6)
                  .Add(0, yf + 2.9);
            if (lod == 0) fn.Smooth(1);
            Shapes.Lathe(m, k, g, fn, lod == 0 ? 16 : lod == 1 ? 8 : 6);
            // The lantern frame: four legs from the drum's rim to the apex, a crossbar ring near the top, the parasol and
            // the spike.
            double legR = 1.7, foot = DrumTop + 0.12;
            int legs = lod >= 2 ? 2 : 4;
            for (int q = 0; q < legs; q++)
            {
                double a = (45 + (lod >= 2 ? 180 : 90) * q) * Math.PI / 180, x0 = legR * Math.Sin(a), z0 = legR * Math.Cos(a);
                Shapes.Bar(m, k, g, x0, foot, z0, 0.06 * Math.Sin(a), FrameTop, 0.06 * Math.Cos(a), lod == 0 ? 0.075 : 0.1, lod == 0 ? 6 : 4);
                if (lod == 0)
                {
                    // The crossbar pieces between neighbouring legs, two thirds of the way up.
                    double t = 0.7, b0 = legR * (1 - t) + 0.06 * t, y1 = foot + (FrameTop - foot) * t;
                    double a2 = a + 0.5 * Math.PI;
                    Shapes.Bar(m, k, g, b0 * Math.Sin(a), y1, b0 * Math.Cos(a), b0 * Math.Sin(a2), y1, b0 * Math.Cos(a2), 0.045, 4);
                    // A small scroll bracket at each leg's foot.
                    Shapes.Sphere(m, SacredDraw.At(k, x0, foot + 0.06, z0), g, 0.12, 6);
                }
            }
            // The apex: a small gilt parasol, a bell and the spike.
            Shapes.Cylinder(m, SacredDraw.At(k, 0, FrameTop - 0.05, 0), g, 0.34, 0.1, lod == 0 ? 12 : 6, 0, 0, true, true);
            Profile2 tp = BoudhaKit.Prof;
            tp.Add(0, FrameTop + 0.05).Add(0.18, FrameTop + 0.05, true).Add(0.2, FrameTop + 0.2).Add(0.1, FrameTop + 0.36).Add(0.04, FrameTop + 0.45).Add(0, Tip);
            Shapes.Lathe(m, k, g, tp, lod == 0 ? 10 : 5);
        }

        // ------------------------------------------------------------------------------------------------ flags

        /// <summary>
        /// The prayer-flag lines: sixteen bundles fanning from under the canopy to the four arm ends and the four body
        /// corners of T1 and to eight darchor poles on the compound wall (boudha.md 3.1.6). LOD0 four lines a bundle on
        /// their cords, small flags (0.5 × 0.42 m) every metre swung alternately in the breeze; LOD1 two lines a bundle as
        /// continuous ribbons of colour; LOD2 one coarser ribbon a bundle. Returns the lines.
        /// </summary>
        private static int Flags(MeshData m, in BoudhaFrame f, int lod)
        {
            Affine3 k = f.Site;
            int per = lod == 0 ? 4 : lod == 1 ? 2 : 1, lines = 0;
            double y0 = NeckTop - 0.15, r0 = 1.5;
            double[] pu = BoudhaKit.U2, pv = BoudhaKit.V2;
            int n = Terrace(0, pu, pv);
            for (int b = 0; b < 16; b++)
            {
                double eu, ev, ey;
                BundleEnd(f, b, pu, pv, out eu, out ev, out ey);
                double dir = Math.Atan2(eu, ev);
                for (int j = 0; j < per; j++)
                {
                    // Spread the lines of a bundle a little round the crown and along the edge at their feet.
                    double spread = per > 1 ? (j - 0.5 * (per - 1)) / (per - 1) : 0;
                    double a0 = dir + 0.18 * spread, su = r0 * Math.Sin(a0), sv = r0 * Math.Cos(a0);
                    double side = 1.8 * spread, tu = Math.Cos(dir), tv = -Math.Sin(dir);
                    double fu = eu + side * tu, fv = ev + side * tv, fy = ey + 0.25 * Math.Abs(spread);
                    double span = Math.Sqrt((fu - su) * (fu - su) + (fv - sv) * (fv - sv)), sag = (0.05 + 0.012 * j) * span;
                    if (lod == 0)
                        BoudhaKit.FlagRun(m, k, su, y0 - 0.25 * j, sv, fu, fy, fv, sag, 0.5, 0.42, 1.0, 25, true, 0.08, b + j);
                    else if (lod == 1)
                        BoudhaKit.FlagRun(m, k, su, y0 - 0.25 * j, sv, fu, fy, fv, sag, 1.45, 0.4, 1.45, 18, false, 0.08, b + j);
                    else
                        BoudhaKit.FlagRun(m, k, su, y0, sv, fu, fy, fv, 0.06 * span, 3.0, 0.5, 3.0, 0, false, 0.05, b);
                    lines++;
                }
            }
            return lines;
        }

        /// <summary>Where bundle b ends (site u, v and height): 0-3 the T1 arm ends, 4-7 the body corners, 8-15 the poles on
        /// the compound wall half-way between.</summary>
        internal static void BundleEnd(in BoudhaFrame f, int b, double[] pu, double[] pv, out double u, out double v, out double y)
        {
            if (b < 4)
            {
                // Arm ends: N (points 7-8), E (2-3), S (17-18), W (12-13).
                int a = b == 0 ? 7 : b == 1 ? 2 : b == 2 ? 17 : 12;
                u = 0.5 * (pu[a] + pu[a + 1]);
                v = 0.5 * (pv[a] + pv[a + 1]);
                y = T1Top + 1.2;
                return;
            }
            if (b < 8)
            {
                int a = b == 4 ? 5 : b == 5 ? 0 : b == 6 ? 15 : 10;
                u = pu[a];
                v = pv[a];
                y = T1Top + 1.2;
                return;
            }
            // The poles stand on the wall at bearings 22.5° + 45° k from the site axis.
            double ang = (22.5 + 45 * (b - 8)) * Math.PI / 180;
            double r = BoudhaWall.RadiusAt(f, ang) - 0.3;
            u = r * Math.Sin(ang);
            v = r * Math.Cos(ang);
            y = BoudhaWall.PoleTop;
        }

        // ------------------------------------------------------------------------------------------------ LOD3

        /// <summary>The far shape (≤ 200 triangles): the three terraces as boxes, the dome, the gilt tower with its red
        /// valance band and the crown.</summary>
        public static void Far(MeshData m, in BoudhaFrame f)
        {
            Affine3 k = f.Site;
            ShapeBrush w = BoudhaKit.White, g = BoudhaKit.Gilt;
            double[] tw = { 40.0, 31.0, 24.0 };
            for (int i = 0; i < 3; i++)
            {
                double y0 = i == 0 ? -0.3 : TerraceTop(i - 1);
                SacredDraw.Box(m, k, -tw[i], tw[i], y0, TerraceTop(i), -tw[i], tw[i], w, BoxFaces.All & ~BoxFaces.Bottom);
            }
            Shapes.Dome(m, SacredDraw.At(k, 0, RingTop - 1.5, 0), BoudhaKit.B(BoudhaKit.DomeWhite, MaterialChannel.Plaster), DomeR + 1, DomeApex - RingTop + 1.5, 8);
            SacredDraw.Box(m, k, -HarmikaHalf, HarmikaHalf, HarmikaBase, HarmikaTop, -HarmikaHalf, HarmikaHalf, BoudhaKit.GiltAged, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, k, -CorniceHalf, CorniceHalf, HarmikaTop, CorniceTop, -CorniceHalf, CorniceHalf, BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Fabric),
                           BoxFaces.All & ~BoxFaces.Bottom);
            Shapes.Frustum(m, SacredDraw.At(k * Affine3.Yaw(45), 0, CorniceTop, 0), g, SpireHalf0 * 1.41, SpireHalf1 * 1.41, SpireTop - CorniceTop, 4, 0, 0, false, false);
            Shapes.Frustum(m, SacredDraw.At(k, 0, SpireTop - 0.3, 0), BoudhaKit.B(BoudhaKit.ClothSaffron, MaterialChannel.Fabric), 2.6, 2.3, NeckTop + 0.7 - SpireTop + 0.3, 6, 0, 0,
                           false, false);
            Shapes.Frustum(m, SacredDraw.At(k, 0, NeckTop + 0.7, 0), g, 2.3, 2.15, DrumTop - NeckTop - 0.7, 6, 0, 0, false, true);
            Shapes.Cone(m, SacredDraw.At(k, 0, DrumTop, 0), g, 1.2, Tip - DrumTop, 4, 0, 0, false);
        }
    }
}
