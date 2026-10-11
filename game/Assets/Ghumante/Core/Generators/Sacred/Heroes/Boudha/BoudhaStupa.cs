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
        public const double NeckTop = 37.1, DrumTop = 38.7, CanopyTop = 40.6, Tip = 44.8;
        public const int Steps = 13, Arcs = 12, Niches = 108;

        // ---- Terrace plans in the site frame (m from the stupa axis; OSM outlines turned onto the 9° axes).
        // T1: body u ±32.9, v −36.1..29.8; N/S arms ±20.7 to +38.8 / −40.8; E/W arms v −23.4..17.4 to +40.6 / −41.0.
        // T2: body ±25.6 × −25.8..25.9; arms ±15.3 / ±15.0 to ±31.3. T3: body ±20.65 × −20.8..20.7; arms ±11.2 to ±24.85.

        /// <summary>The outline of terrace <paramref name="i"/> (0 = T1) into (pu, pv), counter-clockwise; returns 20.</summary>
        public static int Terrace(int i, double[] pu, double[] pv)
        {
            switch (i)
            {
                case 0: return BoudhaKit.CrossSquare(-32.9, 32.9, -36.1, 29.8, -0.2, 20.7, 38.8, -40.8, -3.0, 20.4, 40.6, -41.0, pu, pv);
                case 1: return BoudhaKit.CrossSquare(-25.6, 25.6, -25.8, 25.9, -0.3, 15.3, 31.2, -30.8, 0.1, 15.0, 31.2, -31.6, pu, pv);
                default: return BoudhaKit.CrossSquare(-20.65, 20.65, -20.8, 20.7, -0.5, 11.2, 24.2, -24.8, 0.6, 11.2, 24.7, -25.0, pu, pv);
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
                    end = q == 0 ? 38.8 : q == 1 ? 40.6 : q == 2 ? 40.8 : 41.0;
                    centre = q == 0 || q == 2 ? -0.2 : -3.0;
                    return;
                case 1:
                    end = q == 0 ? 31.2 : q == 1 ? 31.2 : q == 2 ? 30.8 : 31.6;
                    centre = q == 0 || q == 2 ? -0.3 : 0.1;
                    return;
                default:
                    end = q == 0 ? 24.2 : q == 1 ? 24.7 : q == 2 ? 24.8 : 25.0;
                    centre = q == 0 || q == 2 ? -0.5 : 0.6;
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
                c.AddCylinder(x, z, f.Level + T3Top, f.Level + RingTop, RingR0);
                c.AddCylinder(x, z, f.Level + RingTop, f.Level + DomeApex - 2.0, DomeR);
                c.AddBox(x, z, f.Level + HarmikaBase - 4, f.Level + SpireTop, CorniceHalf, CorniceHalf, Math.Cos(BoudhaSite.AxisBearingDeg * Math.PI / 180),
                         -Math.Sin(BoudhaSite.AxisBearingDeg * Math.PI / 180), GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Stone);
            }
            SacredDraw.Bake(m, v0, i0, f.Level);
            int fv = m.VertexCount, fi = m.IndexCount;
            int lines = Flags(m, k, lod);
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
                if (i == 0)
                {
                    // T1's low parapet along its outer edge, with small chorten finials (corner/02, pair_3).
                    Mould pp = SacredDraw.M;
                    double ph = 0.5;
                    pp.Add(-0.02, y1 - 0.01, wall).Add(-0.02, y1 + ph).Add(-0.1, y1 + ph + 0.06, cope, false).Add(-0.3, y1 + ph + 0.06, false).Add(-0.38, y1 + ph)
                      .Add(-0.38, y1 - 0.01, wall);
                    if (lod <= 1) SacredDraw.Ring(m, k, pu, pv, n, pp);
                    if (lod == 0)
                    {
                        double per = BoudhaKit.Perimeter(pu, pv, n);
                        int count = (int)(per / 3.2);
                        for (int q = 0; q < count; q++)
                        {
                            double u, v, tu, tv;
                            BoudhaKit.AtLength(pu, pv, n, (q + 0.5) * per / count, out u, out v, out tu, out tv);
                            // Inward of a counter-clockwise outline: the edge turned left.
                            double iu = -tv, iv = tu;
                            BoudhaKit.FinialChorten(m, SacredDraw.At(k, u + 0.2 * iu, y1 + ph + 0.06, v + 0.2 * iv), 0.75, lod);
                        }
                    }
                }
                if (c != null)
                {
                    // Walkable body and arms (a cross is two overlapping boxes per axis plus the body).
                    AddCrossColliders(c, f, pu, pv, y0 - sink, y1);
                }
            }
        }

        private static void AddCrossColliders(GenColliders c, in BoudhaFrame f, double[] pu, double[] pv, double y0, double y1)
        {
            // Points of CrossSquare: 0 (u1, v0), 5 (u1, v1), 10 (u0, v1), 15 (u0, v0) are the body corners; 2..3 the E arm
            // end, 7..8 N, 12..13 W, 17..18 S.
            double u0 = pu[10], u1 = pu[0], v0 = pv[0], v1 = pv[5];
            Box(c, f, u0, u1, v0, v1, y0, y1);
            Box(c, f, pu[8], pu[7], v1 - 0.5, pv[7], y0, y1); // N arm
            Box(c, f, pu[17], pu[18], pv[17], v0 + 0.5, y0, y1); // S arm
            Box(c, f, u1 - 0.5, pu[2], pv[2], pv[3], y0, y1); // E arm
            Box(c, f, pu[12], u0 + 0.5, pv[13], pv[12], y0, y1); // W arm
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
            ShapeBrush tread = BoudhaKit.B(MeshColor.FromHex(0xB89484), MaterialChannel.Brick), cheek = BoudhaKit.White;
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
                    Affine3 outward = Dir(k, q, endUpper + run, yLow, q == 1 || q == 2 ? -across : across);
                    Affine3 inward = outward * Affine3.Yaw(180);
                    BoudhaKit.Flight(m, c, inward, halfW, run, yLow, yHigh, 0.35, 0.55, tread, cheek, lod);
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
                    Shapes.Frustum(m, p, clay, 0.13, 0.2, 0.3, 8, 0, 0, false, true);
                    Shapes.Ellipsoid(m, SacredDraw.At(p, 0, 0.42, 0), leaf, 0.24, 0.2, 0.24, 6, false);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ ring + dome

        private static void Ring(MeshData m, in Affine3 k, int lod)
        {
            int seg = lod == 0 ? 96 : lod == 1 ? 48 : 24;
            ShapeBrush w = BoudhaKit.White, sh = BoudhaKit.B(BoudhaKit.Weathered, MaterialChannel.Plaster);
            Profile2 p = BoudhaKit.Prof;
            p.Add(0, T3Top - 0.05).Add(RingR0, T3Top - 0.05, true).Add(RingR0, RingStep - 0.08, true).Add(RingR0 + 0.06, RingStep - 0.04, false)
             .Add(RingR0 - 0.02, RingStep, true).Add(RingR1, RingStep, true).Add(RingR1, RingTop, true).Add(DomeR, RingTop, true);
            Shapes.Lathe(m, k, w, p, seg);
            if (lod >= 2) return;
            // The 108 niches round the upper step, each a dark recess in a gilt frame with a tiny seated figure, and the red
            // festoons swagged between every second pair.
            ShapeBrush frame = BoudhaKit.GiltAged, dark = BoudhaKit.B(BoudhaKit.NicheDark, MaterialChannel.Plain, 0.6f);
            ShapeBrush red = BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Fabric), fig = BoudhaKit.B(BoudhaKit.GiltLit, MaterialChannel.Gilt);
            int count = lod == 0 ? Niches : Niches / 2;
            double nh = 0.52, nw = 0.42, yb = RingStep + 0.12;
            for (int j = 0; j < count; j++)
            {
                double a = 360.0 * (j + 0.5) / count;
                Affine3 nf = SacredDraw.At(k * Affine3.Yaw(a), 0, 0, RingR1);
                SacredDraw.Box(m, nf, -0.5 * nw - 0.07, 0.5 * nw + 0.07, yb - 0.06, yb + nh + 0.1, -0.02, 0.05, frame, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
                SacredDraw.Panel(m, nf, -0.5 * nw, yb, 0.5 * nw, yb + nh, 0.055, dark);
                if (lod == 0)
                {
                    Shapes.Ellipsoid(m, SacredDraw.At(nf, 0, yb + 0.16, 0.06), fig, 0.11, 0.13, 0.04, 5, false);
                    Shapes.Sphere(m, SacredDraw.At(nf, 0, yb + 0.34, 0.06), fig, 0.06, 5);
                    if (j % 2 == 0)
                    {
                        // A festoon hanging from the dome foot over this niche and the next.
                        double a2 = 360.0 * (j + 1.5) / count;
                        Affine3 g0 = k * Affine3.Yaw(a);
                        double ax, az, bx, bz;
                        ax = 0;
                        az = RingR1 + 0.12;
                        double rad = (a2 - a) * Math.PI / 180.0;
                        bx = (RingR1 + 0.12) * Math.Sin(rad);
                        bz = (RingR1 + 0.12) * Math.Cos(rad);
                        Shapes.Wire(m, g0, red, ax, RingTop + 0.02, az, bx, RingTop + 0.02, bz, 0.22, 0.045, 5, 4);
                    }
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

        /// <summary>The scallop line: elevation fraction of the arc's lower edge at fraction t across one arc.</summary>
        private static double ArcLine(double t)
        {
            // From just under the harmika foot (e 0.86) the arc hangs down to about half the dome height (e 0.42).
            return 0.86 - 0.44 * Math.Pow(Math.Sin(Math.PI * t), 0.8);
        }

        private static void Dome(MeshData m, in Affine3 k, int lod)
        {
            int seg = lod == 0 ? 96 : lod == 1 ? 48 : 24, rows = lod == 0 ? 24 : lod == 1 ? 12 : 6;
            double h = DomeApex - RingTop;
            ShapeBrush w = BoudhaKit.B(BoudhaKit.DomeWhite, MaterialChannel.Plaster);
            // The dome as a grid so the yellow wash can follow the scallop line vertex by vertex.
            int first = m.VertexCount;
            for (int r = 0; r <= rows; r++)
            {
                double e = (double)r / rows;
                for (int j = 0; j <= seg; j++)
                {
                    double a = 2 * Math.PI * j / seg;
                    double u, y, v, nu, ny, nv;
                    DomePoint(a, e, 0, out u, out y, out v, out nu, out ny, out nv);
                    // The pale yellow wash above the arc line (boudha.md 3.1.4); white below.
                    double t = (a * Arcs / (2 * Math.PI)) % 1.0;
                    bool wash = lod <= 1 && e > ArcLine(t) + 0.01;
                    uint col = wash ? BoudhaKit.YellowWash : BoudhaKit.DomeWhite;
                    if (!wash && lod <= 1)
                    {
                        // Soft weathering toward the foot.
                        col = MeshColor.Lerp(MeshColor.FromHex(0xE8E2D4), col, (float)Math.Min(1, e * 4));
                    }
                    SacredDraw.V(m, k, u, y, v, nu, ny, nv, BoudhaKit.B(col, MaterialChannel.Plaster));
                }
            }
            SacredDraw.Grid(m, first, rows + 1, seg + 1, false, null);
            if (lod >= 2) return;
            // The saffron arc lines: a ribbon of constant width along each scallop, lifted off the surface, and thin
            // drips under every second arc.
            ShapeBrush sb = BoudhaKit.B(BoudhaKit.SaffronArc, MaterialChannel.Paint);
            double width = 0.5;
            int segs = lod == 0 ? 20 : 10;
            for (int j = 0; j < Arcs; j++)
            {
                double a0 = 2 * Math.PI * j / Arcs, a1 = 2 * Math.PI * (j + 1) / Arcs;
                int f0 = m.VertexCount;
                for (int q = 0; q <= segs; q++)
                {
                    double t = (double)q / segs, a = a0 + (a1 - a0) * t, e = ArcLine(t);
                    double u, y, v, nu, ny, nv, ua, ya, va, ub, yb, vb, n1, n2, n3;
                    DomePoint(a, e, 0.03, out u, out y, out v, out nu, out ny, out nv);
                    double tp = Math.Max(0, t - 0.01), tn = Math.Min(1, t + 0.01);
                    DomePoint(a0 + (a1 - a0) * tp, ArcLine(tp), 0.03, out ua, out ya, out va, out n1, out n2, out n3);
                    DomePoint(a0 + (a1 - a0) * tn, ArcLine(tn), 0.03, out ub, out yb, out vb, out n1, out n2, out n3);
                    double tu = ub - ua, ty = yb - ya, tv = vb - va;
                    double cu = ny * tv - nv * ty, cy = nv * tu - nu * tv, cv = nu * ty - ny * tu, cl = Math.Sqrt(cu * cu + cy * cy + cv * cv);
                    if (cl < 1e-9) cl = 1;
                    cu *= 0.5 * width / cl;
                    cy *= 0.5 * width / cl;
                    cv *= 0.5 * width / cl;
                    SacredDraw.V(m, k, u - cu, y - cy, v - cv, nu, ny, nv, sb);
                    SacredDraw.V(m, k, u + cu, y + cy, v + cv, nu, ny, nv, sb);
                }
                SacredDraw.Grid(m, f0, segs + 1, 2, false, null);
                if (lod == 0)
                {
                    // Drips: thin tapering runs down from the arc's low point and its quarter points.
                    for (int d = 0; d < 3; d++)
                    {
                        double t = d == 0 ? 0.5 : d == 1 ? 0.3 : 0.72, ac = a0 + (a1 - a0) * t, e0 = ArcLine(t), len = d == 0 ? 0.2 : 0.09;
                        int f2 = m.VertexCount;
                        for (int s = 0; s <= 4; s++)
                        {
                            double e = e0 - len * s / 4.0, taper = 1 - 0.85 * s / 4.0, da = 0.12 * taper / Math.Max(1, DomeR * Math.Cos(0.5 * Math.PI * e));
                            for (int side = -1; side <= 1; side += 2)
                            {
                                double u, y, v, nu, ny, nv;
                                DomePoint(ac + side * da, e, 0.025, out u, out y, out v, out nu, out ny, out nv);
                                SacredDraw.V(m, k, u, y, v, nu, ny, nv, sb);
                            }
                        }
                        SacredDraw.Grid(m, f2, 5, 2, false, null);
                    }
                }
            }
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
            double ye = HarmikaBase + 0.52 * (HarmikaTop - HarmikaBase), ew = 0.85, eh = 0.3;
            ShapeBrush white = BoudhaKit.B(0xFFFFFFFFu, MaterialChannel.Paint), red = BoudhaKit.B(BoudhaKit.EyeRed, MaterialChannel.Paint);
            ShapeBrush blueD = BoudhaKit.B(BoudhaKit.EyeBlueDark, MaterialChannel.Paint), blueL = BoudhaKit.B(BoudhaKit.EyeBlueLight, MaterialChannel.Paint);
            ShapeBrush ink = BoudhaKit.B(BoudhaKit.Ink, MaterialChannel.Paint), brow = BoudhaKit.B(BoudhaKit.Brow, MaterialChannel.Paint);
            for (int s = -1; s <= 1; s += 2)
            {
                double xc = s * 1.35;
                // Red-orange contour round the almond (wider, pointed at the outer corner), then the white almond.
                Almond(m, f, xc + s * 0.06, ye - 0.02, z, ew + 0.16, eh + 0.12, eh + 0.18, s, seg, red);
                Almond(m, f, xc, ye, z + 0.004, ew, eh, eh * 0.8, s, seg, white);
                // Iris: dark blue ring, light blue, black pupil; looking slightly down.
                Disc(m, f, xc, ye - 0.04, z + 0.008, 0.26, seg, blueD);
                Disc(m, f, xc, ye - 0.04, z + 0.011, 0.19, seg, blueL);
                Disc(m, f, xc, ye - 0.04, z + 0.014, 0.1, Math.Max(6, seg - 4), ink);
                // Black upper lid line and the thick dark brow sweeping up and out, meeting the other over the nose.
                Arc(m, f, xc, ye + 0.02, z + 0.016, ew + 0.04, eh + 0.04, 0.07, s, seg, ink);
                Arc(m, f, xc + s * 0.1, ye + 0.42, z + 0.006, ew + 0.32, 0.28, 0.16, s, seg, brow);
            }
            // The nose "१": a white teardrop with a red spiral; the urna dot between the brows.
            double ny = ye - 0.72;
            Disc(m, f, 0, ny, z + 0.004, 0.3, seg, white);
            Spiral(m, f, 0, ny, z + 0.008, 0.24, lod == 0 ? 28 : 14, 0.05, red);
            Disc(m, f, 0, ye + 0.5, z + 0.006, 0.09, seg, blueD);
            Disc(m, f, 0, ye + 0.5, z + 0.009, 0.05, seg, red);
        }

        /// <summary>An almond on plane z: half width hw, the upper lid up by <paramref name="up"/>, the lower lid down
        /// by <paramref name="down"/>; the outer corner (side s) a little sharper and lower.</summary>
        private static void Almond(MeshData m, in Affine3 f, double xc, double yc, double z, double hw, double up, double down, int s, int seg, in ShapeBrush b)
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

        private static void Disc(MeshData m, in Affine3 f, double xc, double yc, double z, double rad, int seg, in ShapeBrush b)
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
        private static void Arc(MeshData m, in Affine3 f, double xc, double yc, double z, double hw, double rise, double th, int s, int seg, in ShapeBrush b)
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
        private static void Spiral(MeshData m, in Affine3 f, double xc, double yc, double z, double r, int n, double w, in ShapeBrush b)
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

        private static void Crown(MeshData m, in Affine3 k, int lod)
        {
            int seg = lod == 0 ? 32 : lod == 1 ? 16 : 8;
            ShapeBrush g = BoudhaKit.B(BoudhaKit.GiltBright, MaterialChannel.Gilt), gl = BoudhaKit.B(BoudhaKit.GiltLit, MaterialChannel.Gilt);
            // Neck on the top step.
            Profile2 p = BoudhaKit.Prof;
            p.Add(0, SpireTop).Add(1.0, SpireTop, true).Add(0.9, SpireTop + 0.12).Add(0.9, NeckTop, true).Add(0, NeckTop);
            Shapes.Lathe(m, k, g, p, seg);
            // The canopy drum (Ø 4.4) with its repoussé top band and scalloped rim.
            Profile2 d = BoudhaKit.Prof;
            d.Add(0, NeckTop).Add(2.05, NeckTop, true).Add(2.2, NeckTop + 0.12).Add(2.2, DrumTop - 0.25, true).Add(2.32, DrumTop - 0.15).Add(2.3, DrumTop, true)
             .Add(2.0, DrumTop + 0.05).Add(0, DrumTop + 0.05);
            Shapes.Lathe(m, k, gl, d, seg);
            // The parasol cap: a gilt bell dome closing up to the finial.
            Profile2 c = BoudhaKit.Prof;
            c.Add(0, DrumTop).Add(2.25, DrumTop, true).Add(2.05, DrumTop + 0.35).Add(1.6, DrumTop + 0.85).Add(0.9, DrumTop + 1.35).Add(0.55, CanopyTop - 0.2)
             .Add(0.5, CanopyTop, true).Add(0, CanopyTop);
            if (lod <= 1) c.Smooth(lod == 0 ? 2 : 1);
            Shapes.Lathe(m, k, g, c, seg);
            // The cloth: a striped band round the drum (white, blue, red, yellow) over the wide saffron skirt that hangs
            // down over the neck and the top step (general3/00, spire/06).
            uint[] bands = { BoudhaKit.ClothWhite, BoudhaKit.ClothBlue, BoudhaKit.ClothWhite, BoudhaKit.ClothRed, BoudhaKit.ClothYellow };
            double y = NeckTop + 0.95;
            for (int i = 0; i < bands.Length; i++)
            {
                Profile2 b = BoudhaKit.Prof;
                double r = 2.24 + 0.012 * i;
                b.Add(r, y - 0.12 * (i + 1)).Add(r, y - 0.12 * i);
                Shapes.Lathe(m, k, BoudhaKit.B(bands[i], MaterialChannel.Fabric), b, seg);
            }
            double skirtTop = y - 0.12 * bands.Length;
            Profile2 sk = BoudhaKit.Prof;
            sk.Add(2.62, SpireTop - 0.55).Add(2.48, SpireTop - 0.1).Add(2.34, skirtTop - 0.15).Add(2.27, skirtTop + 0.02);
            if (lod == 0)
            {
                // Folds: a lathe whose radius waves round the circumference (built as a ring grid).
                int folds = 24, cols = folds * 2, rowsN = 4, first = m.VertexCount;
                ShapeBrush sf = BoudhaKit.B(BoudhaKit.ClothSaffron, MaterialChannel.Fabric);
                for (int r = 0; r <= rowsN; r++)
                {
                    double t = (double)r / rowsN, yy = (skirtTop + 0.02) + (SpireTop - 0.55 - skirtTop - 0.02) * t, rr = 2.27 + (2.62 - 2.27) * t;
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
                Shapes.Lathe(m, k, BoudhaKit.B(BoudhaKit.ClothSaffron, MaterialChannel.Fabric), sk, seg);
            }
            // The finial: a gilt bell, stacked discs and a pinnacle with a small umbrella and spike, held in a four-leg
            // lantern frame whose legs meet at the top (spire/05, c04, general3/00).
            Profile2 fn = BoudhaKit.Prof;
            double yf = CanopyTop;
            fn.Add(0, yf).Add(0.48, yf, true).Add(0.42, yf + 0.25).Add(0.3, yf + 0.5).Add(0.36, yf + 0.62, true).Add(0.2, yf + 0.7).Add(0.32, yf + 0.86)
              .Add(0.36, yf + 1.1).Add(0.25, yf + 1.35).Add(0.12, yf + 1.5, true).Add(0.16, yf + 1.62).Add(0.1, yf + 1.75, true).Add(0.07, yf + 2.4)
              .Add(0.2, yf + 2.55, true).Add(0.05, yf + 2.65).Add(0.04, yf + 3.6).Add(0, Tip - 0.02);
            if (lod <= 1) fn.Smooth(1);
            Shapes.Lathe(m, k, g, fn, lod == 0 ? 16 : 8);
            if (lod <= 1)
            {
                double legR = 0.95, top = Tip - 0.35;
                for (int q = 0; q < 4; q++)
                {
                    double a = (45 + 90 * q) * Math.PI / 180, x0 = legR * Math.Sin(a), z0 = legR * Math.Cos(a);
                    Shapes.Bar(m, k, g, x0, yf + 0.05, z0, 0.04 * Math.Sin(a), top, 0.04 * Math.Cos(a), 0.05, lod == 0 ? 6 : 4);
                    // A ring tie half-way up.
                    if (lod == 0) Shapes.Bar(m, k, g, 0.55 * x0, yf + 1.9, 0.55 * z0, 0.55 * legR * Math.Sin(a + 0.5 * Math.PI), yf + 1.9, 0.55 * legR * Math.Cos(a + 0.5 * Math.PI), 0.03, 4);
                }
                Shapes.Sphere(m, SacredDraw.At(k, 0, top + 0.05, 0), g, 0.11, lod == 0 ? 10 : 6);
            }
        }

        // ------------------------------------------------------------------------------------------------ flags

        /// <summary>
        /// The prayer-flag lines: sixteen bundles fanning from under the canopy to the four arm ends and the four body
        /// corners of T1 and to eight darchor poles on the compound wall (boudha.md 3.1.6). LOD0 five lines a bundle with
        /// flags nearly edge to edge, LOD1 three with every other flag, LOD2 one coloured ribbon a bundle. Returns the
        /// lines.
        /// </summary>
        private static int Flags(MeshData m, in Affine3 k, int lod)
        {
            int per = lod == 0 ? 5 : lod == 1 ? 3 : 1, lines = 0;
            double y0 = NeckTop - 0.15, r0 = 1.5;
            double[] pu = BoudhaKit.U2, pv = BoudhaKit.V2;
            int n = Terrace(0, pu, pv);
            for (int b = 0; b < 16; b++)
            {
                double eu, ev, ey;
                BundleEnd(b, pu, pv, out eu, out ev, out ey);
                double dir = Math.Atan2(eu, ev);
                for (int j = 0; j < per; j++)
                {
                    // Spread the lines of a bundle a little round the crown and along the edge at their feet.
                    double spread = per > 1 ? (j - 0.5 * (per - 1)) / (per - 1) : 0;
                    double a0 = dir + 0.18 * spread, su = r0 * Math.Sin(a0), sv = r0 * Math.Cos(a0);
                    double side = 1.8 * spread, tu = Math.Cos(dir), tv = -Math.Sin(dir);
                    double fu = eu + side * tu, fv = ev + side * tv, fy = ey + 0.25 * Math.Abs(spread);
                    if (lod <= 1)
                    {
                        BoudhaKit.FlagLine(m, k, su, y0 - 0.25 * j, sv, fu, fy, fv, 0.05 + 0.012 * j, lod, 0.1);
                    }
                    else
                    {
                        Ribbon(m, k, su, y0, sv, fu, fy, fv);
                    }
                    lines++;
                }
            }
            return lines;
        }

        /// <summary>Where bundle b ends (site u, v and height): 0-3 the T1 arm ends, 4-7 the body corners, 8-15 the poles on
        /// the compound wall half-way between.</summary>
        internal static void BundleEnd(int b, double[] pu, double[] pv, out double u, out double v, out double y)
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
            double r = BoudhaWall.RadiusAt(ang) - 0.3;
            u = r * Math.Sin(ang);
            v = r * Math.Cos(ang);
            y = BoudhaWall.PoleTop;
        }

        /// <summary>A far-view flag bundle: a narrow two-sided ribbon striped in the five colours.</summary>
        private static void Ribbon(MeshData m, in Affine3 k, double u0, double y0, double v0, double u1, double y1, double v1)
        {
            int seg = 10;
            double du = u1 - u0, dv = v1 - v0, l = Math.Sqrt(du * du + dv * dv), nu = -dv / l, nv = du / l, sag = 0.06 * l;
            for (int i = 0; i < seg; i++)
            {
                double ta = (double)i / seg, tb = (i + 1.0) / seg;
                double ya = y0 + (y1 - y0) * ta - 4 * sag * ta * (1 - ta), yb = y0 + (y1 - y0) * tb - 4 * sag * tb * (1 - tb);
                ShapeBrush c = BoudhaKit.B(SacredPalette.Flags[i % 5], MaterialChannel.Fabric);
                for (int s = -1; s <= 1; s += 2)
                    SacredDraw.Quad(m, k, u0 + du * ta, ya, v0 + dv * ta, u0 + du * tb, yb, v0 + dv * tb, u0 + du * tb, yb - 0.5, v0 + dv * tb, u0 + du * ta, ya - 0.5,
                                    v0 + dv * ta, s * nu, 0, s * nv, c);
            }
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
            Shapes.Frustum(m, SacredDraw.At(k, 0, SpireTop, 0), BoudhaKit.B(BoudhaKit.ClothSaffron, MaterialChannel.Fabric), 2.4, 2.2, CanopyTop - SpireTop, 6, 0, 0,
                           false, false);
            Shapes.Cone(m, SacredDraw.At(k, 0, CanopyTop, 0), g, 0.6, Tip - CanopyTop, 4, 0, 0, false);
        }
    }
}
