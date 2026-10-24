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

        /// <summary>
        /// The shell (the curated hero): the base ring with its niches, the dome with the saffron arcade and the north
        /// stair, the harmika with the eyes, the thirteen steps and the crown. Its LOD0 build also registers the walkable
        /// terraces, the stairs and the compound stairs that the four quadrant parts draw (<see cref="Quadrant"/>), and the
        /// solid drum, dome and tower.
        /// </summary>
        public static void Build(MeshData m, GenColliders c, in BoudhaFrame f, int lod, SacredStats stats)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = f.Site;
            // The three terrace walls all round: the white stepped mandala the stupa stands on, from the kora up (the four
            // dressing parts draw the floors, the parapet, the stairs and what stands on the terraces).
            for (int i = 0; i < 3; i++) TerraceWall(m, k, BoudhaKit.U, BoudhaKit.V, Terrace(i, BoudhaKit.U, BoudhaKit.V), i, BoudhaPart.Stupa, lod, 0);
            Ring(m, k, lod);
            Dome(m, k, lod);
            DomeStair(m, k, lod);
            Harmika(m, k, lod);
            Spire(m, k, lod);
            Crown(m, k, lod);
            if (c != null)
            {
                double[] pu = BoudhaKit.U, pv = BoudhaKit.V;
                for (int i = 0; i < 3; i++)
                {
                    // Walkable body and arms (a cross is two overlapping boxes per axis plus the body).
                    int n = Terrace(i, pu, pv);
                    double y0 = i == 0 ? 0 : TerraceTop(i - 1), y1 = TerraceTop(i);
                    AddCrossColliders(c, f, pu, pv, y0 - (i == 0 ? 0.6 : 0.05), y1);
                }
                for (int q = 0; q < 4; q++) Stairs(null, c, f, q, 0);
                BoudhaTerraces.CompoundStairs(null, c, f, 0, 0);
                BoudhaTerraces.CompoundStairs(null, c, f, 2, 0);
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
        }

        // ------------------------------------------------------------------------------------------------ terraces

        /// <summary>
        /// The terraces of one dressing quadrant (<paramref name="quadrant"/>; each outline edge by its midpoint's bearing):
        /// the white walls with their base course and rounded coping, the rust-brown brick floors streaked white by the
        /// lime-wash runoff from the dome (c_aerial/c04), T1's parapet with its finials, the three flights on the quadrant's
        /// arm and, north and south, the compound stair (colliders: <see cref="Build"/>).
        /// </summary>
        public static void Quadrant(MeshData m, in BoudhaFrame f, BoudhaPart quadrant, int lod)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = f.Site;
            double[] pu = BoudhaKit.U, pv = BoudhaKit.V, iu = BoudhaKit.U2, iv = BoudhaKit.V2;
            for (int i = 0; i < 3; i++)
            {
                int n = Terrace(i, pu, pv);
                double y1 = TerraceTop(i);
                // The walls belong to the shell (<see cref="Build"/>) but for the far form, which the shell's own far form
                // (<see cref="Far"/>) leaves to the quadrants: drawn a hand's breadth inside the shell's faces.
                if (lod >= 3) TerraceWall(m, k, pu, pv, n, i, quadrant, lod, -0.06);
                // The floor between the terrace above (or the base ring) and this terrace's edge.
                int ni = i < 2 ? Terrace(i + 1, iu, iv) : 0;
                Floor(m, k, pu, pv, n, iu, iv, ni, y1, i, quadrant, lod);
                if (i == 0 && lod <= 1) Parapet(m, k, pu, pv, n, y1, quadrant, lod);
            }
            if (lod <= 2)
            {
                Stairs(m, null, f, (int)(quadrant - BoudhaPart.DressNorth), lod);
                if (quadrant == BoudhaPart.DressNorth) BoudhaTerraces.CompoundStairs(m, null, f, 0, lod);
                if (quadrant == BoudhaPart.DressSouth) BoudhaTerraces.CompoundStairs(m, null, f, 2, lod);
            }
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        /// <summary>
        /// The wall of terrace <paramref name="i"/> along the edges of <paramref name="quadrant"/> (<see cref="BoudhaPart.Stupa"/>:
        /// all round): a weathered base course, the white face and a rounded coping band (LOD2: face and lip; LOD3: the face
        /// alone, <paramref name="off"/> out from the outline).
        /// </summary>
        private static void TerraceWall(MeshData m, in Affine3 k, double[] pu, double[] pv, int n, int i, BoudhaPart quadrant, int lod, double off)
        {
            ShapeBrush wall = BoudhaKit.White, foot = BoudhaKit.B(BoudhaKit.Weathered, MaterialChannel.Plaster), cope = BoudhaKit.B(MeshColor.FromHex(0xF6F3EC), MaterialChannel.Plaster);
            double y0 = i == 0 ? 0 : TerraceTop(i - 1), y1 = TerraceTop(i);
            Mould mo = SacredDraw.M;
            double sink = i == 0 ? 0.6 : 0.05;
            if (lod >= 3)
            {
                mo.Add(off, y0 - sink, wall).Add(off, y1);
            }
            else if (lod >= 2)
            {
                mo.Add(0, y0 - sink, wall).Add(0, y1).Add(0.12, y1, cope);
            }
            else
            {
                mo.Add(0.07, y0 - sink, foot).Add(0.07, y0 + 0.32).Add(0.0, y0 + 0.36, wall, false).Add(0, y1 - 0.34).Add(0.1, y1 - 0.3, cope)
                  .Add(0.16, y1 - 0.18, false).Add(0.14, y1 - 0.06, false).Add(0.05, y1).Add(0, y1);
            }
            BoudhaKit.RingQuadrant(m, k, pu, pv, n, mo, quadrant);
        }

        /// <summary>The terrace floor colour (rust-brown brick, c_aerial/c04) and the lime-wash white of its streaks.</summary>
        private static readonly uint FloorBrick = MeshColor.FromHex(0x9A6A52), LimeStreak = MeshColor.FromHex(0xE8E2D6);

        /// <summary>
        /// One terrace floor in a quadrant: a grid between this terrace's outline (vertices 0..n) and the inner boundary (the
        /// outline of the terrace above, same vertex order, or the base ring's circle for T3), each outline edge cut along
        /// its length and across into rows, its vertex colours the rust-brown brick with a little variation and the white
        /// streaks of the lime-wash running off the dome, strongest at the inner edge and fading out within about 18 m of
        /// the dome (no extra triangles: the streaks are vertex colours). LOD2 and beyond draw the edge's fan triangle in the
        /// streaked brick tone.
        /// </summary>
        private static void Floor(MeshData m, in Affine3 k, double[] pu, double[] pv, int n, double[] iu, double[] iv, int ni, double y, int terrace, BoudhaPart quadrant,
                                  int lod)
        {
            int rows = lod == 0 ? 3 : 1;
            double seg = lod == 0 ? 1.6 : lod == 1 ? 4.0 : 1e9;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (BoudhaSite.QuadrantAt(BoudhaSite.BearingOf(0.5 * (pu[i] + pu[j]), 0.5 * (pv[i] + pv[j]))) != quadrant) continue;
                double el = Math.Sqrt((pu[j] - pu[i]) * (pu[j] - pu[i]) + (pv[j] - pv[i]) * (pv[j] - pv[i]));
                int cols = Math.Max(1, (int)Math.Ceiling(el / seg));
                int first = m.VertexCount;
                for (int r = 0; r <= rows; r++)
                {
                    double t = (double)r / rows;
                    for (int q = 0; q <= cols; q++)
                    {
                        double s = (double)q / cols;
                        double ou = pu[i] + (pu[j] - pu[i]) * s, ov = pv[i] + (pv[j] - pv[i]) * s, au, av;
                        Inner(pu, pv, iu, iv, ni, i, j, s, ou, ov, out au, out av);
                        double u = au + (ou - au) * t, v = av + (ov - av) * t;
                        SacredDraw.V(m, k, u, y + 0.005, v, 0, 1, 0, BoudhaKit.B(FloorTone(u, v, terrace, rows == 1), MaterialChannel.Brick));
                    }
                }
                SacredDraw.Grid(m, first, rows + 1, cols + 1, false, null);
            }
        }

        /// <summary>The inner boundary point matching outline point (ou, ov) on edge i → j at fraction s: the same point of
        /// the terrace above (same vertex order), or for T3 the base ring's circle along the same bearing.</summary>
        private static void Inner(double[] pu, double[] pv, double[] iu, double[] iv, int ni, int i, int j, double s, double ou, double ov, out double au, out double av)
        {
            if (ni == 0)
            {
                double r = Math.Sqrt(ou * ou + ov * ov);
                au = ou / Math.Max(1e-6, r) * (RingR0 - 0.05);
                av = ov / Math.Max(1e-6, r) * (RingR0 - 0.05);
                return;
            }
            au = iu[i] + (iu[j] - iu[i]) * s;
            av = iv[i] + (iv[j] - iv[i]) * s;
        }

        /// <summary>The floor colour at site (u, v) of terrace <paramref name="terrace"/>: rust-brown brick with a soft
        /// variation and the white lime-wash streaks radiating from the dome (wider and paler near it; on T1 only near its
        /// inner edge). <paramref name="far"/> gives the averaged tone.</summary>
        private static uint FloorTone(double u, double v, int terrace, bool far)
        {
            double r = Math.Sqrt(u * u + v * v), a = Math.Atan2(u, v);
            float jitter = (float)(0.06 * ShapeNoise.Value3(u * 0.35, 0, v * 0.35, 7101));
            uint brick = MeshColor.Scale(FloorBrick, 1f + jitter);
            // Streaks: a comb of radial runs (about every 2.2 m round the ring), uneven in width and length, fading with the
            // distance from the dome's foot.
            double fade = 1.0 - Math.Max(0, Math.Min(1, (r - RingR0) / 18.0));
            if (far) return MeshColor.Lerp(brick, LimeStreak, (float)(0.22 * fade));
            double comb = 0.5 + 0.5 * Math.Cos(a * 54.0 + 2.5 * ShapeNoise.Value3(Math.Cos(a) * 3, 0, Math.Sin(a) * 3, 7102));
            double streak = Math.Pow(comb, 6.0) * fade * (0.75 + 0.25 * ShapeNoise.Value3(u * 0.2, 1, v * 0.2, 7103));
            return MeshColor.Lerp(brick, LimeStreak, (float)Math.Max(0, Math.Min(0.85, 1.2 * streak + 0.12 * fade)));
        }

        /// <summary>
        /// T1's low parapet along its outer edge with the small chorten finials on it (corner/02, pair_3), edge by edge, left
        /// open where the compound stairs arrive on the north and south arm ends (<see cref="BoudhaTerraces.ParapetGap"/>).
        /// </summary>
        private static void Parapet(MeshData m, in Affine3 k, double[] pu, double[] pv, int n, double y1, BoudhaPart quadrant, int lod)
        {
            ShapeBrush wall = BoudhaKit.White, cope = BoudhaKit.B(MeshColor.FromHex(0xF6F3EC), MaterialChannel.Plaster);
            const double ph = 0.5, th = 0.36, step = 0.25;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double du = pu[j] - pu[i], dv = pv[j] - pv[i], l = Math.Sqrt(du * du + dv * dv);
                if (l < 0.05) continue;
                if (BoudhaSite.QuadrantAt(BoudhaSite.BearingOf(0.5 * (pu[i] + pu[j]), 0.5 * (pv[i] + pv[j]))) != quadrant) continue;
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
                                int count = (int)((x - a) / 5.0);
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

        /// <summary>The three flights on arm <paramref name="q"/> (0 N, 1 E, 2 S, 3 W): T1 → T2, T2 → T3 and T3 → the base
        /// ring, the north ones wider with yellow handrails and clay pots; with no mesh only their walkable ramps.</summary>
        private static void Stairs(MeshData m, GenColliders c, in BoudhaFrame f, int q, int lod)
        {
            Affine3 k = f.Site;
            ShapeBrush tread = BoudhaKit.B(MeshColor.FromHex(0xE4DCD3), MaterialChannel.Stone), cheek = BoudhaKit.White;
            ShapeBrush rail = BoudhaKit.B(MeshColor.FromHex(0xE8C23A), MaterialChannel.Metal);
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
                if (m == null) continue;
                if (q == 0 && lod == 0)
                {
                    BoudhaKit.Handrail(m, inward, -halfW + 0.15, run, yLow, yHigh, 0.9, rail, lod);
                    BoudhaKit.Handrail(m, inward, halfW - 0.15, run, yLow, yHigh, 0.9, rail, lod);
                }
                if (q == 0 && lod == 0) Pots(m, inward, halfW + 0.35, run, yLow, yHigh);
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
            int seg = lod == 0 ? 60 : lod == 1 ? 32 : 24;
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
            int count = lod == 0 ? Niches : Niches / 3;
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
                    // A pennant hanging from the dome foot over the pier after this niche (its outer face: it hangs against
                    // the band).
                    Affine3 pf = SacredDraw.At(k * Affine3.Yaw(360.0 * (j + 1.0) / count), 0, 0, RingR1 + 0.06);
                    ShapeBrush cl = BoudhaKit.B(SacredPalette.Flags[j % 5], MaterialChannel.Fabric);
                    SacredDraw.Tri(m, pf, -0.13, RingTop - 0.02, 0.0, 0.13, RingTop - 0.02, 0.0, 0.0, RingTop - 0.42, 0.04, 0, 0, 1, cl);
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

        private static readonly double[] RowsLod2 = { 0, 0.15, 0.3, 0.45, 0.62, 0.7, 0.8, 1.0 };

        private static readonly uint PaleWash = MeshColor.FromHex(0xF0D894), CapWash = MeshColor.FromHex(0xEDC862);

        private static void Dome(MeshData m, in Affine3 k, int lod)
        {
            int seg = lod == 0 ? 60 : lod == 1 ? 32 : 24;
            double[] rows = lod == 0 ? RowsLod0 : lod == 1 ? RowsLod1 : RowsLod2;
            // The dome as a grid so the washes can follow the arcade vertex by vertex: lime white up to the arcade, the pale
            // yellow wash above it, the cap wash above the cap's scalloped edge (far off: white with the cap wash only).
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
                    else if (lod <= 1 && e > ArcLine(t) + 0.005) col = PaleWash;
                    else col = MeshColor.Lerp(MeshColor.FromHex(0xE8E2D4), col, (float)Math.Min(1, e * 4));
                    SacredDraw.V(m, k, u, y, v, nu, ny, nv, BoudhaKit.B(col, MaterialChannel.Plaster));
                }
            }
            SacredDraw.Grid(m, first, rows.Length, seg + 1, false, null);
            ShapeBrush sb = BoudhaKit.B(BoudhaKit.SaffronArc, MaterialChannel.Paint);
            ShapeBrush soft = BoudhaKit.B(MeshColor.FromHex(0xEACB86), MaterialChannel.Paint);
            int segs = lod == 0 ? 16 : lod == 1 ? 8 : 4;
            for (int j = 0; j < Arcs; j++)
            {
                double a0 = 2 * Math.PI * j / Arcs, a1 = 2 * Math.PI * (j + 1) / Arcs;
                // The arch over lobe j (a ribbon of constant width along the arcade line, lifted off the surface; far off a
                // little wider so it still reads).
                DomeRibbon(m, k, a0, a1, segs, lod >= 2 ? 0.6 : 0.45, lod >= 2 ? 0.06 : 0.03, false, sb);
                // The leg at a0 down the dome from the springing to the niche band, wider where the two arches meet.
                Leg(m, k, a0, ArcSpring + 0.02, LegFoot, 0.62, lod >= 2 ? 0.5 : 0.4, lod == 0 ? 6 : lod == 1 ? 2 : 1, lod >= 2 ? 0.06 : 0.03, sb);
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

        // ------------------------------------------------------------------------------------------------ dome stair

        /// <summary>The lowest elevation fraction of the dome stair and its half width (m): it climbs the north face from
        /// the arcade's crown to the harmika's foot (motp/12-13, kora/07).</summary>
        private const double StairFoot = 0.42, DomeStairHalf = 0.65;

        /// <summary>The dome's radius at height y above the kora (its flattened half ellipse).</summary>
        private static double DomeRadiusAt(double y)
        {
            double sn = Math.Max(0, Math.Min(1, (y - RingTop) / (DomeApex - RingTop)));
            return DomeR * Math.Sqrt(1 - sn * sn);
        }

        /// <summary>
        /// The narrow stair up the dome's north face (site bearing 0; motp/12-13: the eyes look down it): white treads and
        /// greyer risers 1.3 m wide stepping up the curve from the arcade's crown to the foot of the harmika between two
        /// low white cheeks. Far off it is a stepped strip of a few treads.
        /// </summary>
        private static void DomeStair(MeshData m, in Affine3 k, int lod)
        {
            if (lod >= 3) return;
            Affine3 f = BoudhaKit.Facing(k, 0, 0, 0, 0, 1);
            double rh = (HarmikaHalf + 0.1) / DomeR;
            double y0 = RingTop + (DomeApex - RingTop) * Math.Sin(0.5 * Math.PI * StairFoot), y1 = RingTop + (DomeApex - RingTop) * Math.Sqrt(1 - rh * rh);
            int steps = lod == 0 ? 22 : lod == 1 ? 9 : 4;
            double rise = (y1 - y0) / steps;
            // Grey stone treads and darker risers in white cheeks (motp/13): the stair reads as a grey ribbed band on the dome.
            ShapeBrush tread = BoudhaKit.B(MeshColor.FromHex(0xC9C1B3), MaterialChannel.Stone), riser = BoudhaKit.B(MeshColor.FromHex(0x8E877C), MaterialChannel.Stone, 0.75f);
            ShapeBrush cheek = BoudhaKit.B(BoudhaKit.LimeWhite, MaterialChannel.Plaster);
            for (int i = 0; i < steps; i++)
            {
                double yb = y0 + i * rise, yt = yb + rise;
                double rOut = DomeRadiusAt(yb) + 0.05, rIn = Math.Max(HarmikaHalf - 0.2, DomeRadiusAt(yt) - 0.3);
                SacredDraw.Box(m, f, -DomeStairHalf, DomeStairHalf, yb - 0.25, yt, rIn, rOut, riser, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                SacredDraw.Box(m, f, -DomeStairHalf, DomeStairHalf, yb - 0.25, yt, rIn, rOut, tread, BoxFaces.Top);
                if (lod == 0)
                {
                    // The cheeks: a low white kerb either side, 0.12 m above the treads.
                    for (int s = -1; s <= 1; s += 2)
                        SacredDraw.Box(m, f, s * DomeStairHalf - (s > 0 ? 0 : 0.14), s * DomeStairHalf + (s > 0 ? 0.14 : 0), yb - 0.2, yt + 0.12, rIn, rOut, cheek,
                                       BoxFaces.Front | BoxFaces.Top | (s > 0 ? BoxFaces.Right : BoxFaces.Left));
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ harmika

        /// <summary>The eyes' centre line as a fraction of the harmika's eye block (photos: the brows meet the drape's green
        /// frill and the "१" nose hangs between the eyes toward the dome), the drape's hem above the kora and the drape's
        /// depth below its stripes.</summary>
        public const double EyeLine = 0.68, DrapeH = 1.1;

        private static void Harmika(MeshData m, in Affine3 k, int lod)
        {
            double hb = HarmikaBase - 0.6, hw = HarmikaHalf;
            ShapeBrush g = BoudhaKit.B(BoudhaKit.GiltPanel, MaterialChannel.Gilt), gl = BoudhaKit.B(BoudhaKit.GiltLit, MaterialChannel.Gilt);
            ShapeBrush line = BoudhaKit.B(BoudhaKit.GiltJoint, MaterialChannel.Gilt, 0.8f);
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
            double zc = CorniceHalf + 0.02, x0 = -CorniceHalf - 0.02, x1 = CorniceHalf + 0.02;
            for (int s = 0; s < 4; s++)
            {
                Affine3 face = k * Affine3.Yaw(90 * s);
                // Square gilt plates (about 0.45 m): the joints as darker lines.
                if (lod == 0) BoudhaKit.GridLines(m, face, -hw, hw, HarmikaBase, HarmikaTop - 0.05, hw + 0.004, 0.45, 0.45, 0.035, line);
                Eyes(m, face, hw + 0.01, lod);
                if (lod >= 2)
                {
                    // Far: the red drape over the eye block's top and its green frill as bands.
                    SacredDraw.Panel(m, face, x0, HemY + 0.16, x1, CorniceTop - 0.1, zc + 0.04, BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Fabric));
                    SacredDraw.Panel(m, face, x0, HemY - 0.06, x1, HemY + 0.16, zc + 0.06, BoudhaKit.B(BoudhaKit.ClothGreen, MaterialChannel.Fabric));
                    continue;
                }
                // The cloth on the frieze: thin stripes (blue, yellow, red, green) and the red pleated drape hanging low over
                // the top of the eye block, its green ruffled frill just over the brows (eyes/00, general3/00, motp/13).
                uint[] stripes = { BoudhaKit.ClothBlue, BoudhaKit.ClothYellow, BoudhaKit.ClothRed, BoudhaKit.ClothGreen };
                double hem = BoudhaKit.Valance(m, face, x0, x1, zc, CorniceTop - 0.12, stripes, 0.11, BoudhaKit.ClothRed, DrapeH, 0.45, lod);
                BoudhaKit.Valance(m, face, x0, x1, zc + 0.06, hem + 0.08, new uint[0], 0, BoudhaKit.ClothGreen, 0.22, 0.25, lod);
            }
        }

        /// <summary>The drape's hem above the kora (the frill hangs 0.14 m below it).</summary>
        private const double HemY = CorniceTop - 0.12 - 4 * 0.11 - DrapeH;

        /// <summary>
        /// The eyes on one harmika face (the frame's +z face at z = <paramref name="z"/>; boudha.md 3.1.5, motp/13,
        /// eyes/00): each eye 1.9 m wide (0.27 of the face) and about 0.44 as tall, its centre at
        /// <see cref="EyeLine"/> of the block, a red-orange contour round the white almond and a red shade on the arched upper
        /// lid, the large iris (0.38 m) in a dark blue and a light blue ring round the black pupil, cut by the lids, a black
        /// lid line and the heavy dark brow just under the drape's frill; the white "१" nose (0.55 m) with its red
        /// three-turn spiral between and below the eyes and the third eye between the brows. LOD2 keeps all of it as small
        /// fans (about 90 triangles a face).
        /// </summary>
        private static void Eyes(MeshData m, in Affine3 f, double z, int lod)
        {
            int seg = lod == 0 ? 12 : lod == 1 ? 6 : 3, disc = lod == 0 ? 16 : lod == 1 ? 8 : 6;
            double ye = HarmikaBase + EyeLine * (HarmikaTop - HarmikaBase), ew = 0.95, eh = 0.42, ed = 0.85 * eh;
            ShapeBrush white = BoudhaKit.B(0xFFFFFFFFu, MaterialChannel.Paint), red = BoudhaKit.B(BoudhaKit.EyeRed, MaterialChannel.Paint);
            ShapeBrush contour = BoudhaKit.B(MeshColor.FromHex(0xA8302A), MaterialChannel.Paint), lid = BoudhaKit.B(MeshColor.FromHex(0xC8402A), MaterialChannel.Paint);
            ShapeBrush blueD = BoudhaKit.B(BoudhaKit.EyeBlueDark, MaterialChannel.Paint), blueL = BoudhaKit.B(BoudhaKit.EyeBlueLight, MaterialChannel.Paint);
            ShapeBrush ink = BoudhaKit.B(BoudhaKit.Ink, MaterialChannel.Paint), brow = BoudhaKit.B(BoudhaKit.Brow, MaterialChannel.Paint);
            for (int s = -1; s <= 1; s += 2)
            {
                double xc = s * 1.35;
                // Red-orange contour round the almond (wider, pointed at the outer corner), the red lid shade over the upper lid,
                // then the white almond.
                Almond(m, f, xc + s * 0.05, ye - 0.01, z, ew + 0.11, eh + 0.08, ed + 0.09, s, seg, contour);
                if (lod <= 1) Arc(m, f, xc, ye + 0.06, z + 0.002, ew + 0.08, eh, 0.16, s, seg, lid);
                Almond(m, f, xc, ye, z + 0.004, ew, eh, ed, s, seg, white);
                // The iris, cut by the lids: a dark blue ring, light blue, the black pupil; looking slightly down.
                Iris(m, f, xc, ye, z + 0.008, ew, eh, ed, xc, ye - 0.04, 0.38, disc, blueD);
                Iris(m, f, xc, ye, z + 0.011, ew, eh, ed, xc, ye - 0.04, 0.27, disc, blueL);
                Disc(m, f, xc, ye - 0.04, z + 0.014, 0.13, Math.Max(6, disc - 4), ink);
                // Black upper lid line, and the thick dark brow sweeping up and out, its crest under the frill.
                if (lod <= 1) Arc(m, f, xc, ye + 0.02, z + 0.016, ew + 0.04, eh + 0.02, 0.07, s, seg, ink);
                Arc(m, f, xc + s * 0.06, ye + eh - 0.02, z + 0.006, ew + 0.3, 0.16, 0.24, s, lod >= 2 ? 3 : seg, brow);
            }
            // The nose "१": a white disc with a red three-turn spiral between the eyes, its top at their lower lids; the urna
            // between the brows.
            double ny = ye - 0.78;
            Disc(m, f, 0, ny, z + 0.004, 0.275, disc, white);
            Spiral(m, f, 0, ny, z + 0.008, 0.23, lod == 0 ? 40 : lod == 1 ? 18 : 4, 0.05, red);
            Disc(m, f, 0, ye + 0.62, z + 0.006, 0.1, lod >= 2 ? 6 : disc, blueD);
            if (lod <= 1) Disc(m, f, 0, ye + 0.62, z + 0.009, 0.055, disc, red);
        }

        /// <summary>A disc of radius <paramref name="rad"/> at (cx, cy) cut by the lids of the almond at (xc, yc) (half width
        /// hw, upper lid <paramref name="up"/>, lower lid <paramref name="down"/>), as the painted iris shows.</summary>
        private static void Iris(MeshData m, in Affine3 f, double xc, double yc, double z, double hw, double up, double down, double cx, double cy, double rad, int seg,
                                 in ShapeBrush b)
        {
            int c = SacredDraw.V(m, f, cx, cy, z, 0, 0, 1, b), first = m.VertexCount;
            for (int i = 0; i < seg; i++)
            {
                double a = 2 * Math.PI * i / seg, x = cx + rad * Math.Cos(a), y = cy + rad * Math.Sin(a);
                double t = Math.Max(0, Math.Min(1, (x - (xc - hw)) / (2 * hw)));
                double hi = yc + up * Math.Pow(Math.Sin(Math.PI * t), 0.85) - 0.03, lo = yc - down * Math.Pow(Math.Sin(Math.PI * t), 1.1) + 0.03;
                y = Math.Max(lo, Math.Min(hi, y));
                SacredDraw.V(m, f, x, y, z, 0, 0, 1, b);
            }
            for (int i = 0; i < seg; i++) SacredDraw.Tri(m, c, first + i, first + (i + 1) % seg);
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

        /// <summary>A flat spiral stroke of three turns (the red spiral in the nose).</summary>
        internal static void Spiral(MeshData m, in Affine3 f, double xc, double yc, double z, double r, int n, double w, in ShapeBrush b)
        {
            int first = m.VertexCount;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / (n - 1), a = 6.0 * Math.PI * t, rr = r * (0.12 + 0.88 * t);
                double x = xc + rr * Math.Cos(a), y = yc + rr * Math.Sin(a);
                double ox = Math.Cos(a) * 0.5 * w, oy = Math.Sin(a) * 0.5 * w;
                SacredDraw.V(m, f, x - ox, y - oy, z, 0, 0, 1, b);
                SacredDraw.V(m, f, x + ox, y + oy, z, 0, 0, 1, b);
            }
            SacredDraw.Grid(m, first, n, 2, false, null);
        }

        // ------------------------------------------------------------------------------------------------ spire

        /// <summary>
        /// The thirteen square gilt steps (9.1 m, 6.6 → 2.4 m): each riser of antique brass with a dark band (#5E4724) at its
        /// top where the step's shadow falls (the spire's ribbed look from afar), a ledge, and at LOD0 two rows of square plates
        /// with dark joints. LOD2 keeps the thirteen steps and their dark bands.
        /// </summary>
        private static void Spire(MeshData m, in Affine3 k, int lod)
        {
            double step = (SpireTop - CorniceTop) / Steps;
            ShapeBrush g = BoudhaKit.B(BoudhaKit.GiltBright, MaterialChannel.Gilt), gs = BoudhaKit.B(BoudhaKit.GiltShade, MaterialChannel.Gilt);
            ShapeBrush band = BoudhaKit.B(BoudhaKit.GiltJoint, MaterialChannel.Gilt, 0.75f), line = BoudhaKit.B(BoudhaKit.GiltJoint, MaterialChannel.Gilt, 0.85f);
            double set = (SpireHalf0 - SpireHalf1) / Steps;
            for (int q = 0; q < Steps; q++)
            {
                double half = SpireHalf0 + (SpireHalf1 - SpireHalf0) * q / Steps, y = CorniceTop + q * step;
                Mould mo = SacredDraw.M;
                if (lod >= 2) mo.Add(0, y, g).Add(0, y + step - 0.12).Add(0, y + step - 0.12, band).Add(0, y + step).Add(-set, y + step);
                else mo.Add(0, y, g).Add(0, y + step - 0.14).Add(0, y + step - 0.14, band).Add(0, y + step - 0.04).Add(-0.03, y + step - 0.02, gs).Add(-set, y + step - 0.01, g)
                       .Add(-set, y + step);
                SacredDraw.RectRing(m, k, 0, 0, half, half, mo, q == Steps - 1, g);
                if (lod == 0)
                {
                    // Two rows of square plates on every riser, their joints dark.
                    for (int s = 0; s < 4; s++)
                    {
                        Affine3 face = k * Affine3.Yaw(90 * s);
                        BoudhaKit.GridLines(m, face, -half, half, y + 0.02, y + step - 0.16, half + 0.004, 0.36, 0.5 * (step - 0.16), 0.03, line);
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
            int seg = lod == 0 ? 24 : lod == 1 ? 12 : 8;
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
            // The cloth (motp/04, n_Boudhanath_spire/ref_04, the telephoto): the yellow cloth hangs straight round the canopy's
            // posts as a near cylinder the drum's width, from the top step up to thin white, blue, white and red bands; above
            // them the gilt drum (cloth about 35 %, bands 18 %, drum 47 % of the crown's height).
            uint[] bands = { BoudhaKit.ClothWhite, BoudhaKit.ClothBlue, BoudhaKit.ClothWhite, BoudhaKit.ClothRed };
            double crown0 = SpireTop + 0.05, crownH = DrumTop + 0.42 - crown0, bandH = 0.18 * crownH / bands.Length;
            double skirtBot = crown0, skirtTop = crown0 + 0.35 * crownH, y = skirtTop + bandH * bands.Length;
            for (int i = 0; i < bands.Length; i++)
            {
                // Far off the white bands merge into the blue and the red.
                if (lod >= 2 && i == 0) continue;
                Profile2 bp = BoudhaKit.Prof;
                double r = 2.32 + 0.012 * i, ya = y - bandH * (i + 1), yb = y - bandH * i;
                if (lod >= 2 && i == 1) yb = y;
                bp.Add(r, ya).Add(r, yb);
                Shapes.Lathe(m, k, BoudhaKit.B(bands[i], MaterialChannel.Fabric), bp, seg);
            }
            ShapeBrush cloth = BoudhaKit.B(BoudhaKit.ClothYellow, MaterialChannel.Fabric);
            if (lod == 0)
            {
                // Vertical pleats: a ring grid whose radius waves round the circumference, a straight hem a little gathered.
                int folds = 24, cols = folds * 2, rowsN = 3, first = m.VertexCount;
                for (int r = 0; r <= rowsN; r++)
                {
                    double t = (double)r / rowsN, yy = skirtTop + (skirtBot - skirtTop) * t, rr = 2.30 - 0.05 * t;
                    for (int j = 0; j <= cols; j++)
                    {
                        double a = 2 * Math.PI * j / cols, wob = (j % 2 == 0 ? 1 : -1) * 0.05;
                        double nu = Math.Sin(a), nv = Math.Cos(a);
                        uint col = j % 2 == 0 ? BoudhaKit.ClothYellow : MeshColor.Scale(BoudhaKit.ClothYellow, 0.9f);
                        SacredDraw.V(m, k, (rr + wob) * nu, yy, (rr + wob) * nv, nu, 0, nv, BoudhaKit.B(col, MaterialChannel.Fabric));
                    }
                }
                SacredDraw.Grid(m, first, rowsN + 1, cols + 1, false, null);
            }
            else
            {
                Profile2 sk = BoudhaKit.Prof;
                sk.Add(2.25, skirtBot).Add(2.3, skirtTop);
                Shapes.Lathe(m, k, cloth, sk, seg);
            }
            // The finial bulb on the drum top: a lotus foot, the bell, two rings and the pinnacle.
            Profile2 fn = BoudhaKit.Prof;
            double yf = DrumTop + 0.38;
            if (lod >= 2) fn.Add(0, yf).Add(0.75, yf, true).Add(0.55, yf + 0.8).Add(0.25, yf + 1.4).Add(0.1, yf + 2.2).Add(0, FrameTop);
            else fn.Add(0, yf).Add(0.78, yf, true).Add(0.8, yf + 0.12).Add(0.62, yf + 0.22, true).Add(0.66, yf + 0.55).Add(0.56, yf + 0.9).Add(0.36, yf + 1.12)
                  .Add(0.4, yf + 1.2, true).Add(0.26, yf + 1.3).Add(0.3, yf + 1.4, true).Add(0.18, yf + 1.5).Add(0.2, yf + 1.75).Add(0.1, yf + 2.0).Add(0.07, yf + 2.6)
                  .Add(0, yf + 2.9);
            if (lod == 0) fn.Smooth(1);
            Shapes.Lathe(m, k, g, fn, lod == 0 ? 12 : lod == 1 ? 6 : 6);
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

        /// <summary>The flag bundles, the lines a bundle at LOD0, all the site's lines at LOD0, and the top of the flag
        /// fans (under the canopy) above the kora.</summary>
        public const int Bundles = 16, FlagLinesPerBundle = 4, FlagLinesLod0 = Bundles * FlagLinesPerBundle;

        public const double FlagTop = NeckTop + 0.1;

        /// <summary>
        /// The prayer-flag lines of one dressing quadrant (<paramref name="quadrant"/>: the bundles whose feet lie in it) of
        /// the sixteen bundles fanning from under the canopy to the four arm ends and the four body corners of T1 and to
        /// eight darchor poles on the compound wall (boudha.md 3.1.6). LOD0 four lines a bundle on their cords, small flags
        /// (0.5 × 0.42 m) every metre swung alternately in the breeze; LOD1 two lines a bundle as continuous ribbons of
        /// colour; LOD2 one ribbon of 3 m blocks a bundle, LOD3 one of 5 m blocks. Returns the lines.
        /// </summary>
        internal static int Flags(MeshData m, in BoudhaFrame f, BoudhaPart quadrant, int lod)
        {
            Affine3 k = f.Site;
            int per = lod == 0 ? FlagLinesPerBundle : lod == 1 ? 2 : 1, lines = 0;
            double y0 = NeckTop - 0.15, r0 = 1.5;
            double[] pu = BoudhaKit.U2, pv = BoudhaKit.V2;
            int n = Terrace(0, pu, pv);
            for (int b = 0; b < Bundles; b++)
            {
                double eu, ev, ey;
                BundleEnd(f, b, pu, pv, out eu, out ev, out ey);
                if (BundleQuadrant(b) != quadrant) continue;
                double dir = Math.Atan2(eu, ev);
                for (int j = 0; j < per; j++)
                {
                    // Spread the lines of a bundle a little round the crown and along the edge at their feet.
                    double spread = per > 1 ? (j - 0.5 * (per - 1)) / (per - 1) : 0;
                    double a0 = dir + 0.18 * spread, su = r0 * Math.Sin(a0), sv = r0 * Math.Cos(a0);
                    double side = 1.8 * spread, tu = Math.Cos(dir), tv = -Math.Sin(dir);
                    if (b < 4)
                    {
                        // The axis bundles part either side of the nose (motp/13: the face stays clear): every line 0.9 m or
                        // more off the axis, at the canopy and at its foot.
                        double lo = (spread >= 0 ? 1 : -1) * (0.9 + 0.6 * Math.Abs(spread));
                        su = r0 * Math.Sin(dir) + lo * tu;
                        sv = r0 * Math.Cos(dir) + lo * tv;
                        side = lo;
                    }
                    double fu = eu + side * tu, fv = ev + side * tv, fy = ey + 0.25 * Math.Abs(spread);
                    double span = Math.Sqrt((fu - su) * (fu - su) + (fv - sv) * (fv - sv)), sag = (0.05 + 0.012 * j) * span;
                    if (lod == 0)
                        BoudhaKit.FlagRun(m, k, su, y0 - 0.25 * j, sv, fu, fy, fv, sag, 0.62, 0.42, 1.35, 25, true, 0.08, b + j);
                    else if (lod == 1)
                        BoudhaKit.FlagRun(m, k, su, y0 - 0.25 * j, sv, fu, fy, fv, sag, 1.9, 0.42, 1.9, 18, false, 0.08, b + j);
                    else if (lod == 2)
                        BoudhaKit.FlagRun(m, k, su, y0, sv, fu, fy, fv, 0.06 * span, 3.0, 0.5, 3.0, 0, false, 0.05, b);
                    else
                        BoudhaKit.FlagRun(m, k, su, y0, sv, fu, fy, fv, 0.06 * span, 5.0, 0.6, 5.0, 0, false, 0.05, b);
                    lines++;
                }
            }
            return lines;
        }

        /// <summary>The dressing quadrant that draws bundle b: its arm end, the body corner clockwise after the arm (the NE
        /// corner with the north arm, and so on) and its two poles, so each quadrant holds four bundles.</summary>
        internal static BoudhaPart BundleQuadrant(int b)
        {
            if (b < 8) return (BoudhaPart)((int)BoudhaPart.DressNorth + (b & 3));
            return BoudhaSite.QuadrantAt(22.5 + 45 * (b - 8));
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

        /// <summary>
        /// The shell's far shape (≤ 240 triangles; the air, the rim hills, more than about a kilometre off; the terraces are
        /// the quadrant parts' far shapes): the base ring with its step and the white dome as a twelve-sided lathe with three
        /// latitude rings, the gilt harmika with a dark blue brow and eye strip on each face under the red valance band, the
        /// spire stepped in three square tiers, the saffron cloth band and the gilt drum and finial.
        /// </summary>
        public static void Far(MeshData m, in BoudhaFrame f)
        {
            Affine3 k = f.Site;
            ShapeBrush w = BoudhaKit.B(BoudhaKit.DomeWhite, MaterialChannel.Plaster), g = BoudhaKit.B(BoudhaKit.GiltLit, MaterialChannel.Gilt);
            // Ring (its step as one sloped band) and the dome.
            Profile2 p = BoudhaKit.Prof;
            p.Add(RingR0, T3Top - 0.1, true).Add(RingR0, RingStep, true).Add(RingR1, RingTop, true);
            double[] lat = { 0.38, 0.72, 0.93 };
            for (int i = 0; i < lat.Length; i++)
            {
                double u, y, v, nu, ny, nv;
                DomePoint(0, lat[i], 0, out u, out y, out v, out nu, out ny, out nv);
                p.Add(v, y, i == 0);
            }
            p.Add(0, DomeApex);
            Shapes.Lathe(m, k, w, p, 12);
            // The harmika, its eye strips and the red valance band with the cornice.
            SacredDraw.Box(m, k, -HarmikaHalf, HarmikaHalf, HarmikaBase, HarmikaTop, -HarmikaHalf, HarmikaHalf, BoudhaKit.B(BoudhaKit.GiltPanel, MaterialChannel.Gilt),
                           BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            ShapeBrush eye = BoudhaKit.B(BoudhaKit.EyeBlueDark, MaterialChannel.Paint);
            double ey = HarmikaBase + EyeLine * (HarmikaTop - HarmikaBase);
            for (int s = 0; s < 4; s++)
            {
                Affine3 face = k * Affine3.Yaw(90 * s);
                SacredDraw.Panel(m, face, -0.86 * HarmikaHalf, ey - 0.28, 0.86 * HarmikaHalf, ey + 0.5, HarmikaHalf + 0.03, eye);
            }
            SacredDraw.Box(m, k, -CorniceHalf, CorniceHalf, HarmikaTop, CorniceTop, -CorniceHalf, CorniceHalf, BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Fabric),
                           BoxFaces.All & ~BoxFaces.Bottom);
            // The spire in three square tiers (the thirteen steps' silhouette), the saffron cloth, the gilt drum and finial.
            double h3 = (SpireTop - CorniceTop) / 3;
            for (int t = 0; t < 3; t++)
            {
                double a0 = SpireHalf0 + (SpireHalf1 - SpireHalf0) * t / 3.0, a1 = SpireHalf0 + (SpireHalf1 - SpireHalf0) * (t + 0.85) / 3.0;
                Shapes.Frustum(m, SacredDraw.At(k * Affine3.Yaw(45), 0, CorniceTop + t * h3, 0), g, a0 * 1.414, a1 * 1.414, h3, 4, 0, 0, false, true);
            }
            Shapes.Frustum(m, SacredDraw.At(k, 0, SpireTop - 0.05, 0), BoudhaKit.B(BoudhaKit.ClothYellow, MaterialChannel.Fabric), 2.3, 2.3, NeckTop + 1.25 - SpireTop, 6, 0, 0,
                           false, false);
            Shapes.Frustum(m, SacredDraw.At(k, 0, NeckTop + 1.2, 0), g, 2.3, 2.1, DrumTop + 0.4 - NeckTop - 1.2, 6, 0, 0, false, true);
            Shapes.Cone(m, SacredDraw.At(k, 0, DrumTop + 0.4, 0), g, 0.8, Tip - DrumTop - 0.4, 4, 0, 0, false);
        }
    }
}
