using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// The temples that ride on the palace (kds-palace.md §3.4, §3.8): Degu Taleju, three dark copper roofs with bells on
    /// struts over a gilt-panelled body, standing on a massive brick tower that ends at 13 m in a tile skirt and a brick
    /// parapet (finial 26 m), with the two-storey brick range on its south-west whose red-valanced eave runs above the Seto
    /// Bhairav screen (modelled here on its host wall: the arched lattice with its red fringe, lamps and stone base); and
    /// Panch Mukhi Hanuman, a white drum at Nasal Chowk's north-east corner carrying five stacked round roofs with
    /// red-and-white valances (finial 25 m).
    /// </summary>
    internal static class KpTemples
    {
        /// <summary>Degu Taleju's brick tower (palace grid) and its height.</summary>
        public const double GA0 = -9.45, GA1 = 1.55, GB0 = 12.6, GB1 = 23.6, TowerTop = 12.4;

        /// <summary>The Seto Bhairav range (palace grid) south-west of the tower, its eave and ridge.</summary>
        public const double SA0 = -11.4, SA1 = -3.5, SB0 = 6.0, SB1 = 12.6, SetoEave = 7.0, SetoRidge = 9.2;

        /// <summary>The bay of the Seto Bhairav screen on the range's west face (palace grid b) and its height.</summary>
        public const double ScreenB0 = 7.4, ScreenB1 = 11.4, ScreenTop = 5.0;

        /// <summary>Panch Mukhi's centre (palace grid) and its drum's radius.</summary>
        public const double PA = 20.6, PB = -3.8, DrumR = 3.5;

        private static readonly double[] TowerFloors = { 3.4, 6.6, 9.6 };
        private static readonly double[] SetoFloors = { 3.4 };

        // =============================================================================================================
        // Degu Taleju
        // =============================================================================================================

        public static void DeguTaleju(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            double am = 0.5 * (GA0 + GA1), bm = 0.5 * (GB0 + GB1), h = 0.5 * (GA1 - GA0);
            double x, y;
            KpFrame.FromGrid(am, bm, out x, out y);
            Affine3 k = f.Plan(x, y, 0, 102);
            var t = new KpTier[3];
            t[0] = new KpTier { Base = 13.9, WallU = 3.4, WallW = 3.4, Eave = 17.4, EaveU = 5.5, EaveW = 5.5, Top = 18.6, Walls = KpWalls.GiltPanels, Windows = 3 };
            t[1] = new KpTier { Base = 18.6, WallU = 2.6, WallW = 2.6, Eave = 21.0, EaveU = 4.3, EaveW = 4.3, Top = 22.0, Walls = KpWalls.GiltPanels, Windows = 1 };
            t[2] = new KpTier { Base = 22.0, WallU = 1.9, WallW = 1.9, Eave = 23.9, EaveU = 3.15, EaveW = 3.15, Top = 25.2, ApexU = 0.3, ApexW = 0.3, Walls = KpWalls.Brick, Windows = 1 };
            if (lod >= 3)
            {
                Kp.Box(m, k, -h, h, 0, TowerTop, -h, h, Kp.Brick, BoxFaces.All & ~BoxFaces.Bottom);
                KpTowers.Lod3(m, k, t, 3, Kp.Brick, Kp.DeguCopper);
                Kp.Box(m, f.Grid, SA0, SA1, 0, SetoEave, SB0, SB1, Kp.Brick, BoxFaces.All & ~BoxFaces.Bottom);
                return;
            }
            // The brick tower: pilastered faces with stepped cornices and small lattice windows.
            Kp.Box(m, k, -h, h, -0.2, TowerTop + 0.2, -h, h, Kp.Brick, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            for (int s = 0; s < 4; s++)
            {
                // Only the west face (on the forecourt) and the north face (to the Kot) show; the others meet the palace.
                if (s != 1 && s != 2) continue;
                Affine3 fc = KpStack.Face(k, 90 * s, h);
                KpFacade.Newar(m, fc, h, 0, TowerFloors, 3, TowerTop + 0.1, 2.7, -1, -1, false, false, Math.Max(lod, s == 2 ? 0 : 1));
                if (lod <= 1)
                    for (int q = -1; q <= 1; q += 2) Kp.Box(m, fc, q * (h - 0.3) - 0.3, q * (h - 0.3) + 0.3, 0, TowerTop, 0, 0.18, Kp.BrickNew, BoxFaces.Wall);
            }
            // The tile skirt with its carved sawtooth fascia, and the brick parapet the temple stands in.
            RoofSpec skirt = Kp.Tier(m, k, h + 1.3, h + 1.3, TowerTop - 0.4, h - 0.6, h - 0.6, TowerTop + 0.9, h, h, Kp.TileMoss, Kp.Wood, false, false, lod, 0.02);
            if (lod <= 1) Kp.Struts(m, k, skirt, TowerTop - 2.2, 1.3, false, 801u, lod);
            if (lod == 0)
            {
                // The sawtooth fascia: small triangles hanging along each eave.
                for (int s = 0; s < 4; s++)
                {
                    Affine3 fc = KpStack.Face(k, 90 * s, h + 1.32);
                    int n = (int)((2 * h + 2.6) / 0.4);
                    for (int i = 0; i < n; i++)
                    {
                        double u = -h - 1.3 + (2 * h + 2.6) * (i + 0.5) / n;
                        SacredDraw.Tri(m, fc, u - 0.17, TowerTop - 0.42, 0.01, u + 0.17, TowerTop - 0.42, 0.01, u, TowerTop - 0.72, 0.01, 0, 0, 1, Kp.Wood);
                    }
                }
            }
            KpStack.Stage(m, k, 0, 0, h - 0.6, h - 0.6, TowerTop + 0.85, 13.9, Kp.Brick, Kp.Step, lod);
            // The temple: three copper roofs with bells on struts, gilt panels, the finial and the banners.
            KpRoofLook look = KpRoofLook.CopperBells(Kp.DeguCopper);
            look.Horns = true;
            KpStack.Build(m, k, t, 3, Kp.BrickGlazed, look, 811u, lod);
            Kp.Finial(m, k, 0, 25.15, 0, 0.85, 0.24, lod);
            if (lod == 0)
            {
                for (int s = 0; s < 4; s++)
                {
                    Affine3 fc = KpStack.Face(k, 90 * s, 0);
                    double[] pu = { 0, 0, 0 }, pv = { 25.0, 23.7, 21.9 }, pw = { 0.4, 3.0, 4.2 };
                    SacredParts.Pataka(m, fc, pu, pv, pw, 3, 0.26, lod);
                }
            }
            SetoRange(ref f, m);
            if (f.C != null)
            {
                f.GridBox(GA0, GA1, GB0, GB1, 0, TowerTop + 1.5, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.Box(x, y, 3.6, 3.6, 102, TowerTop + 1.5, 25.0, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }

        /// <summary>
        /// The Seto (Sweta) Bhairav screen (§3.4; photos sweta_bhairav c_01, c_06-c_08): a tall round-topped arch about
        /// 3.2 m wide and 4.6 m high in a dark carved frame, filled with a square timber lattice divided into 3 × 4 panels,
        /// the red fringed valance following the arch, two brass lamps hanging on chains in front, and the carved stone
        /// base panel with a guardian figure at each end. Closed, as all year but Indra Jatra (the gilt mask is not shown).
        /// </summary>
        private static void Screen(MeshData m, in Affine3 w, double uc, int lod)
        {
            const double hw = 1.6, b0 = 0.9, spring = 2.95, wood = 0.2;
            // The carved stone base panel and its two guardians.
            Kp.Box(m, w, uc - hw - 0.25, uc + hw + 0.25, -0.1, b0, 0, 0.3, Kp.StoneGrey, BoxFaces.Wall | BoxFaces.Top);
            if (lod <= 1)
                for (int s = -1; s <= 1; s += 2)
                {
                    double gu = uc + s * (hw - 0.05);
                    Shapes.Ellipsoid(m, SacredDraw.At(w, gu, 0.42, 0.42), Kp.Carved, 0.2, 0.34, 0.14, lod == 0 ? 8 : 5, false);
                    Shapes.Sphere(m, SacredDraw.At(w, gu, 0.86, 0.42), Kp.Carved, 0.13, lod == 0 ? 7 : 4);
                }
            if (lod == 0) Kp.Box(m, w, uc - 0.9, uc + 0.9, 0.25, 0.7, 0.3, 0.34, Kp.Tint(Kp.StoneGrey, 0.85f), BoxFaces.Front);
            // The dark opening behind the lattice: the rectangle and the half disc of the arch.
            Kp.Panel(m, w, uc - hw, b0, uc + hw, spring, 0.012, Kp.Recess);
            int seg = lod == 0 ? 12 : lod == 1 ? 8 : 4;
            for (int i = 0; i < seg; i++)
            {
                double a0 = Math.PI * i / seg, a1 = Math.PI * (i + 1) / seg;
                SacredDraw.Tri(m, w, uc, spring, 0.012, uc + hw * Math.Cos(a0), spring + hw * Math.Sin(a0), 0.012, uc + hw * Math.Cos(a1), spring + hw * Math.Sin(a1), 0.012,
                               0, 0, 1, Kp.Recess);
            }
            if (lod >= 2)
            {
                Kp.Box(m, w, uc - hw - wood, uc - hw, b0, spring, 0, 0.15, Kp.Wood, BoxFaces.Front);
                Kp.Box(m, w, uc + hw, uc + hw + wood, b0, spring, 0, 0.15, Kp.Wood, BoxFaces.Front);
                return;
            }
            // The frame: jambs, sill and the arch band.
            Kp.Box(m, w, uc - hw - wood, uc - hw, b0, spring, 0, 0.18, Kp.Wood, BoxFaces.Wall);
            Kp.Box(m, w, uc + hw, uc + hw + wood, b0, spring, 0, 0.18, Kp.Wood, BoxFaces.Wall);
            Kp.Box(m, w, uc - hw - 0.35, uc + hw + 0.35, b0 - 0.05, b0 + 0.12, 0, 0.22, Kp.Wood, BoxFaces.Wall);
            ShikharaGenerator.Arch(m, w, uc, spring, 0.0, 2 * hw, hw, wood, 0.18, false, Kp.Wood, lod);
            // The lattice: square bars clipped to the arch, heavier mullions and transoms between the 3 × 4 panels.
            double step = lod == 0 ? 0.24 : 0.48, top = spring + hw;
            for (double u = -hw + step; u < hw - 0.01; u += step)
            {
                double h = spring + Math.Sqrt(Math.Max(0, hw * hw - u * u)) - 0.04;
                bool heavy = Math.Abs(Math.Abs(u) - hw / 3) < 0.5 * step;
                double t = heavy ? 0.045 : 0.018;
                Kp.Box(m, w, uc + u - t, uc + u + t, b0, h, 0.02, heavy ? 0.1 : 0.06, Kp.Wood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            }
            for (double v = b0 + step; v < top - 0.05; v += step)
            {
                double half = v <= spring ? hw : Math.Sqrt(Math.Max(0, hw * hw - (v - spring) * (v - spring))) - 0.03;
                if (half < 0.1) continue;
                bool heavy = Math.Abs(v - (b0 + (spring - b0) / 3)) < 0.5 * step || Math.Abs(v - (b0 + 2 * (spring - b0) / 3)) < 0.5 * step || Math.Abs(v - spring) < 0.5 * step;
                double t = heavy ? 0.045 : 0.018;
                Kp.Box(m, w, uc - half, uc + half, v - t, v + t, 0.02, heavy ? 0.1 : 0.06, Kp.Wood, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            }
            // The red fringe following the arch, a white band at its top.
            int n = lod == 0 ? 22 : 10, first = m.VertexCount;
            double rr = hw + wood + 0.08;
            for (int i = 0; i <= n; i++)
            {
                double a = Math.PI * i / n, c = Math.Cos(a), sa = Math.Sin(a), d = i % 2 == 0 ? 0.36 : 0.27;
                double pu = uc + rr * c, pv = spring + rr * sa;
                SacredDraw.V(m, w, pu, pv + 0.08, 0.26, 0, 0.2, 1, Kp.ValanceWhite);
                SacredDraw.V(m, w, pu, pv - 0.02, 0.26, 0, 0.2, 1, Kp.Valance);
                SacredDraw.V(m, w, pu, pv - d, 0.28, 0, 0.1, 1, Kp.Valance);
            }
            for (int i = 0; i < n; i++)
            {
                int a = first + 3 * i, b = a + 3;
                SacredDraw.Quad(m, a, b, b + 1, a + 1);
                SacredDraw.Quad(m, a + 1, b + 1, b + 2, a + 2);
            }
            // The two brass lamps on chains from iron brackets.
            for (int s = -1; s <= 1; s += 2)
            {
                double lu = uc + s * (hw - 0.35);
                Shapes.Bar(m, w, Kp.Iron, lu, spring + 1.2, 0.0, lu, spring + 1.2, 0.55, 0.025, 4);
                Shapes.Cylinder(m, SacredDraw.At(w, lu, spring - 0.15, 0.55), Kp.Iron, 0.012, 1.35, 4, 0, 0, false, false);
                var p = new Profile2(8);
                p.Add(0, 0, true).Add(0.1, 0.04).Add(0.2, 0.16, true).Add(0.17, 0.24).Add(0.07, 0.36).Add(0, 0.42, true);
                Shapes.Lathe(m, SacredDraw.At(w, lu, spring - 0.6, 0.55), Kp.GiltDeep, p, lod == 0 ? 10 : 6);
            }
        }

        /// <summary>
        /// The two-storey brick range of the Seto Bhairav screen (§3.4): carved lattice windows and round oculus windows,
        /// a carved bay window, a red fringed valance along its eave and the plain bay where the screen stands.
        /// </summary>
        private static void SetoRange(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            RoofSpec roof = KpFacade.Range(m, ref f, SA0, SA1, SB0, SB1, SetoEave, SetoRidge, 1.3, Kp.Brick, Kp.Tile, true, lod);
            double half;
            Affine3 w = f.GridSide(SA0, SA1, SB0, SB1, 0, 3, out half);
            double bm = 0.5 * (SB0 + SB1), u0 = -(ScreenB1 - bm), u1 = -(ScreenB0 - bm);
            // Plinth course, wooden string course, cornice.
            Kp.Band(m, w, -half, half, 0, 0.35, 0.07, Kp.Step);
            Kp.Band(m, w, -half, u0 - 0.1, SetoFloors[0] - 0.1, SetoFloors[0] + 0.12, 0.1, Kp.Wood);
            Kp.Band(m, w, u1 + 0.1, half, SetoFloors[0] - 0.1, SetoFloors[0] + 0.12, 0.1, Kp.Wood);
            Kp.Cornice(m, w, -half, half, SetoEave - 0.5, 0.4, Kp.Wood, lod);
            // The Seto Bhairav screen in its bay.
            Screen(m, w, 0.5 * (u0 + u1), lod);
            // Windows either side: a carved bay window to the south, oculus and lattice windows to the north.
            if (lod <= 1)
            {
                double us = 0.5 * (u1 + half), un = 0.5 * (-half + u0);
                Kp.Lattice(m, w, us, 1.9, 0.75, 1.0, lod, 2);
                Kp.Bay(m, w, us, SetoFloors[0] + 0.45, 1.7, 1.6, false, lod);
                Kp.Lattice(m, w, un, 1.9, 0.75, 1.0, lod, 2);
                Shapes.Torus(m, SacredDraw.Basis(w, un, SetoFloors[0] + 1.3, 0.04, 1, 0, 0, 0, 0, 1), Kp.Wood, 0.42, 0.08, lod == 0 ? 14 : 8, 4);
                Kp.Panel(m, w, un - 0.3, SetoFloors[0] + 1.0, un + 0.3, SetoFloors[0] + 1.6, 0.01, Kp.Recess);
            }
            // The north face on the Kot side.
            Affine3 n = f.GridSide(SA0, SA1, SB0, SB1, 0, 0, out half);
            KpFacade.Newar(m, n, half, 0, SetoFloors, 1, SetoEave + 0.3, 2.2, -1, -1, false, false, Math.Max(1, lod));
            if (lod <= 1) Kp.Struts(m, KpFacade.RoofFrame(ref f, SA0, SA1, SB0, SB1), roof, SetoFloors[0] + 0.9, 1.3, true, 821u, lod);
            if (f.C != null) f.GridBox(SA0, SA1, SB0, SB1, 0, SetoRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
        }

        // =============================================================================================================
        // Panch Mukhi Hanuman
        // =============================================================================================================

        /// <summary>Panch Mukhi's five roofs: eave radius, eave height, top radius (the drum above), top height.</summary>
        private static readonly double[] Roofs =
        {
            5.6, 13.4, 3.1, 14.9,
            4.8, 16.2, 2.6, 17.4,
            4.0, 18.6, 2.1, 19.6,
            3.2, 20.7, 1.6, 21.5,
            2.4, 22.4, 0.14, 24.0,
        };

        /// <summary>
        /// Panch Mukhi Hanuman (§3.8): the white drum with a red-framed arched door to Nasal Chowk, then five round roofs
        /// shrinking upward, each a dark ribbed cone on struts with a red-and-white fringed valance, a round dark timber
        /// gallery storey between them, and the gilt finial at 25 m.
        /// </summary>
        public static void PanchMukhi(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            double x, y;
            KpFrame.FromGrid(PA, PB, out x, out y);
            Affine3 k = f.Plan(x, y, 0, 102);
            int seg = lod == 0 ? 28 : lod == 1 ? 18 : lod == 2 ? 12 : 8;
            ShapeLod sl = f.SL;
            ShapeBrush roofB = Kp.RoundRoof;
            if (lod >= 3)
            {
                Shapes.Cylinder(m, k, Kp.White, DrumR, Roofs[1], seg, 0, 0, false, false);
                for (int i = 0; i < 5; i++)
                    Shapes.Frustum(m, SacredDraw.At(k, 0, Roofs[4 * i + 1], 0), roofB, Roofs[4 * i], Math.Max(0.05, Roofs[4 * i + 2]), Roofs[4 * i + 3] - Roofs[4 * i + 1], seg, 0, 0, false, false);
                return;
            }
            // The white drum, its plinth band and the door to the court.
            Shapes.Cylinder(m, SacredDraw.At(k, 0, -0.2, 0), Kp.White, DrumR, Roofs[1] + 0.4, seg, 0, 0, false, false, sl);
            Shapes.Cylinder(m, SacredDraw.At(k, 0, -0.1, 0), Kp.Step, DrumR + 0.12, 0.45, seg, 0, 0, false, true, sl);
            if (lod <= 1)
            {
                for (int i = 0; i < 2; i++)
                    Shapes.Torus(m, SacredDraw.At(k, 0, i == 0 ? 10.6 : 12.4, 0), Kp.WhiteTrim, DrumR + 0.03, 0.09, seg, 4, sl);
                Affine3 door = KpStack.Face(k, -100, DrumR - 0.05);
                Kp.Panel(m, door, -0.6, 0.45, 0.6, 2.4, 0.02, Kp.Recess);
                Kp.Box(m, door, -0.75, -0.6, 0.45, 2.4, 0, 0.12, Kp.Dado, BoxFaces.Wall);
                Kp.Box(m, door, 0.6, 0.75, 0.45, 2.4, 0, 0.12, Kp.Dado, BoxFaces.Wall);
                ShikharaGenerator.Arch(m, door, 0, 2.4, 0.03, 1.2, 0.45, 0.14, 0.12, true, Kp.Dado, lod);
                Affine3 win = KpStack.Face(k, -160, DrumR - 0.05);
                Kp.Lattice(m, win, 0, 6.2, 0.8, 1.1, lod, 2);
            }
            for (int i = 0; i < 5; i++)
            {
                double er = Roofs[4 * i], ev = Roofs[4 * i + 1], tr = Roofs[4 * i + 2], tv = Roofs[4 * i + 3];
                RoundRoof(m, k, er, ev, tr, tv, i == 0 ? DrumR : Roofs[4 * i - 2], roofB, seg, lod, sl);
                if (i < 4)
                {
                    // The gallery storey between this roof and the next: a dark timber drum with posts.
                    double nv = Roofs[4 * i + 5];
                    Shapes.Cylinder(m, SacredDraw.At(k, 0, tv - 0.1, 0), Kp.Ao(Kp.Wood, 0.8f), tr, nv - tv + 0.3, seg, 0, 0, false, false, sl);
                    if (lod == 0)
                    {
                        int posts = Math.Max(8, (int)(2 * Math.PI * tr / 0.7));
                        for (int p = 0; p < posts; p++)
                        {
                            double a = 2 * Math.PI * p / posts;
                            Shapes.Bar(m, k, Kp.WoodMid, (tr + 0.04) * Math.Cos(a), tv, (tr + 0.04) * Math.Sin(a), (tr + 0.04) * Math.Cos(a), nv - 0.1,
                                       (tr + 0.04) * Math.Sin(a), 0.045, 4);
                        }
                        Shapes.Torus(m, SacredDraw.At(k, 0, tv + 0.55, 0), Kp.WoodBeam, tr + 0.05, 0.05, seg, 3);
                    }
                }
                // Struts from the storey below up under the eave.
                if (lod <= 1)
                {
                    double wr = i == 0 ? DrumR : Roofs[4 * i - 2];
                    int n = lod == 0 ? 12 : 6;
                    for (int s = 0; s < n; s++)
                    {
                        double a = 2 * Math.PI * (s + 0.5) / n, c = Math.Cos(a), sn = Math.Sin(a);
                        Affine3 st = SacredDraw.Basis(k, 0, 0, 0, sn, 0, -c, 0, 1, 0);
                        Kp.Strut(m, st, 0, ev - 1.5, wr, ev - 0.1, er - 0.45, 0.06, Kp.Wood);
                    }
                }
            }
            Kp.Finial(m, k, 0, Roofs[19] - 0.1, 0, 25.0 - Roofs[19] + 0.1, 0.22, lod);
            if (f.C != null) f.Cylinder(x, y, 0, 24.0, DrumR + 0.1);
        }

        /// <summary>One round roof: a dark ribbed cone from the eave ring (radius er at ev) rising concave to the ring where the
        /// next storey stands (tr at tv), a dark fascia, a soffit back to the wall (radius wr) and the red-and-white fringed
        /// valance hanging from the eave.</summary>
        private static void RoundRoof(MeshData m, in Affine3 k, double er, double ev, double tr, double tv, double wr, in ShapeBrush b, int seg, int lod,
                                      ShapeLod sl)
        {
            var p = new Profile2(8);
            double mr = tr + 0.45 * (er - tr), mv = ev + 0.3 * (tv - ev);
            p.Add(wr, ev - 0.25, true).Add(er, ev - 0.25, true).Add(er + 0.04, ev + 0.02, true).Add(mr, mv).Add(tr, tv, true).Add(0, tv, true);
            Shapes.Lathe(m, k, b, p, seg, sl);
            if (lod <= 1)
            {
                int ribs = lod == 0 ? 24 : 12;
                var path = new Path3(3);
                for (int i = 0; i < ribs; i++)
                {
                    double a = 2 * Math.PI * i / ribs, c = Math.Cos(a), s = Math.Sin(a);
                    path.Clear();
                    path.Add((er - 0.05) * c, ev + 0.08, (er - 0.05) * s).Add((mr + 0.02) * c, mv + 0.06, (mr + 0.02) * s).Add((tr + 0.03) * c, tv + 0.02, (tr + 0.03) * s);
                    Shapes.Tube(m, k, Kp.Tint(b, 0.72f), path, 0.035, 3, false);
                }
            }
            // The valance: a white band and red drops round the eave.
            int n = lod == 0 ? 2 * seg : seg, first = m.VertexCount;
            double drop = 0.22 + 0.025 * er;
            for (int i = 0; i <= n; i++)
            {
                double a = 2 * Math.PI * i / n, c = Math.Cos(a), s = Math.Sin(a), d = i % 2 == 0 ? drop : 0.75 * drop, r = er + 0.06;
                SacredDraw.V(m, k, r * c, ev - 0.2, r * s, c, 0, s, Kp.ValanceWhite);
                SacredDraw.V(m, k, r * c, ev - 0.32, r * s, c, 0, s, Kp.Valance);
                SacredDraw.V(m, k, (r + 0.03) * c, ev - 0.2 - d, (r + 0.03) * s, c, -0.1, s, Kp.Valance);
            }
            for (int i = 0; i < n; i++)
            {
                int a = first + 3 * i, bb = a + 3;
                SacredDraw.Quad(m, a, bb, bb + 1, a + 1);
                SacredDraw.Quad(m, a + 1, bb + 1, bb + 2, a + 2);
            }
        }
    }
}
