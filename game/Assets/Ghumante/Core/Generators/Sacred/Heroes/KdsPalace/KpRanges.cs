using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// The white Rana ranges of the palace (kds-palace.md §3.3, §3.7, §3.16) on the palace grid: the Dakha range whose
    /// long north facade faces the forecourt (blind cusped arches over a red dado and bench, shuttered windows, the
    /// continuous black balcony on its diagonal struts, the upper windows, a deep eave on struts, white roof pavilions on
    /// the ridge; the stepped stone platform along its foot), the courts behind it (Dakha and Lam Chowk), the four-storey
    /// Tribhuvan block that closes Nasal Chowk on the west, and on Basantapur the white south-west wing and the white range
    /// running on to Gaddi Baithak.
    /// </summary>
    internal static class KpRanges
    {
        /// <summary>The Dakha range on the forecourt (palace grid) with its eave and ridge.</summary>
        public const double DA0 = -38.6, DA1 = -11.6, DB0 = -27.0, DB1 = -10.0, DakhaEave = 12.0, DakhaRidge = 15.0;

        /// <summary>The storeys of the Dakha front: first floor, balcony floor.</summary>
        public const double DakhaF1 = 4.0, DakhaBalcony = 7.7;

        /// <summary>The stepped platform along the Dakha front: its depth, height and the run of its four steps.</summary>
        public const double PlatformDepth = 1.6, PlatformH = 0.6, StepRun = 0.35;

        /// <summary>The Tribhuvan block (the Nasal court's west side).</summary>
        public const double TA0 = -11.6, TA1 = 0.9, TB0 = -56.0, TB1 = -9.6, TribhuvanEave = 13.5, TribhuvanRidge = 16.2;

        private static readonly double[] TribhuvanFloors = { 3.6, 7.0, 10.3 };
        private static readonly double[] CourtFloors = { 4.0, 8.0 };

        public static void Dakha(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            Affine3 g = f.Grid;
            if (lod >= 3)
            {
                Kp.Box(m, g, DA0, DA1, 0, DakhaEave, DB0, DB1, Kp.White, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, g, 0.5 * (DA0 + DA1), 0.5 * (DB0 + DB1), 14.5, 9.5, DakhaEave, DakhaRidge, Kp.Tile);
                Kp.Box(m, g, TA0, TA1, 0, TribhuvanEave, TB0, TB1, Kp.White, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, g, 0.5 * (TA0 + TA1), 0.5 * (TB0 + TB1), 7.2, 24.2, TribhuvanEave, TribhuvanRidge, Kp.Tile);
                Kp.Box(m, g, -55.7, TA0, 0, 11.0, -64.2, DB0, Kp.White, BoxFaces.All & ~BoxFaces.Bottom);
                return;
            }
            DakhaFront(ref f, m);
            // The Tribhuvan block on Nasal Chowk's west side: white, four storeys, regular windows.
            KpFacade.Range(m, ref f, TA0, TA1, TB0, TB1, TribhuvanEave, TribhuvanRidge, 1.0, Kp.White, Kp.Tile, false, lod);
            double half;
            Affine3 e = f.GridSide(TA0, TA1, TB0, TB1, 0, 1, out half);
            KpFacade.Rana(m, e, half, 0, TribhuvanFloors, 3, TribhuvanEave, KpFoot.Plain, 2.9, Kp.ShutterWhite, true, false, false, Math.Max(1, lod));
            if (lod <= 1) Kp.Dormer(m, KpFacade.RoofFrame(ref f, TA0, TA1, TB0, TB1), 0, TribhuvanRidge - 0.9, -6.0, 1.8, lod);
            // The ranges round Dakha Chowk and Lam Chowk, and the block west of Lam Chowk (w910121099).
            const double ce = 12.0, cr = 14.5;
            KpFacade.Range(m, ref f, -24.0, TA0, -38.0, DB0, ce, cr, 1.0, Kp.White, Kp.Tile, false, lod);
            KpFacade.Range(m, ref f, DA0, -35.0, -38.0, DB0, ce, cr, 1.0, Kp.White, Kp.Tile, false, lod);
            KpFacade.Range(m, ref f, DA0, TA0, -52.5, -38.0, ce, cr, 1.0, Kp.White, Kp.Tile, false, lod);
            KpFacade.Range(m, ref f, DA0, -33.0, -62.8, -52.5, ce, cr, 1.0, Kp.White, Kp.Tile, false, lod);
            KpFacade.Range(m, ref f, -22.5, TA0, -62.8, -52.5, ce, cr, 1.0, Kp.White, Kp.Tile, false, lod);
            KpFacade.Range(m, ref f, -55.7, DA0, -64.2, -42.3, 11.0, 13.6, 1.0, Kp.White, Kp.Tile, false, lod);
            KpFacade.Court(m, ref f, -35.0, -24.0, -38.0, DB0, Kp.Flags);
            KpFacade.Court(m, ref f, -33.0, -22.5, -62.8, -52.5, Kp.Flags);
            DakhaWest(ref f, m);
            if (lod == 0)
            {
                // The court faces: rows of shuttered windows as flat dark panes (seen only from the towers and the air).
                CourtFaces(ref f, m, -35.0, -24.0, -38.0, DB0);
                CourtFaces(ref f, m, -33.0, -22.5, -62.8, -52.5);
            }
            if (f.C != null)
            {
                f.GridBox(DA0, DA1, DB0, DB1, 0, DakhaRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(TA0, TA1, TB0, TB1, 0, TribhuvanRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(-55.7, TA0, -64.2, DB0, 0, cr, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }

        /// <summary>
        /// The Dakha front on the forecourt (§3.3a): red dado and bench, blind cusped arches with a few dark windows, the
        /// first-floor shuttered windows, the continuous black balcony on diagonal struts, the upper windows behind it, a
        /// white cornice, the deep eave on struts, two white roof pavilions; the stepped stone platform along the foot.
        /// </summary>
        private static void DakhaFront(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            Affine3 g = f.Grid;
            Kp.Box(m, g, DA0, DA1, -0.2, DakhaEave + 0.2, DB0, DB1, Kp.White, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            RoofSpec roof = KpFacade.Roof(m, ref f, DA0, DA1, DB0, DB1, DakhaEave, DakhaRidge, 1.3, Kp.TileMoss, false, lod);
            Affine3 rk = KpFacade.RoofFrame(ref f, DA0, DA1, DB0, DB1);
            double half;
            Affine3 k = f.GridSide(DA0, DA1, DB0, DB1, PlatformH, 0, out half);
            // Dado and bench on the platform.
            Kp.Band(m, k, -half, half, 0, 0.55, 0.05, Kp.Dado);
            if (lod <= 1) Kp.Box(m, k, -half + 0.4, half - 0.4, 0, 0.42, 0, 0.36, Kp.Dado, BoxFaces.Wall);
            // Blind cusped arches on the ground floor, every third bay a dark window.
            int n = Math.Max(1, (int)Math.Round(2 * half / 2.35));
            double w = 2 * half / n;
            for (int i = 0; i < n; i++)
            {
                double u = -half + w * (i + 0.5);
                if (i % 3 == 1) Kp.RanaWindow(m, k, u, 1.1, 0.8, 1.5, Kp.ShutterWhite, true, lod);
                else Kp.BlindArch(m, k, u, 0.7, w - 0.45, 2.7, lod);
            }
            Kp.Band(m, k, -half, half, DakhaF1 - 0.62 - 0.12, DakhaF1 - 0.62 + 0.06, 0.08, Kp.WhiteTrim);
            // First-floor windows every 3.4 m (dakha_north c_07): dark lattice, the white louvred leaves folded open.
            int wins = Math.Max(1, (int)Math.Round(2 * half / 3.4));
            for (int i = 0; i < wins; i++) Kp.RanaWindow(m, k, -half + 2 * half * (i + 0.5) / wins, DakhaF1 - 0.6 + 0.55, 0.95, 1.7, Kp.ShutterWhite, false, lod);
            // The black balcony on its struts, the upper windows behind it closed by white louvres.
            double bv = DakhaBalcony - PlatformH;
            Kp.Gallery(m, k, -half - 0.05, half + 0.05, bv, 1.0, 0.95, 0.95, lod);
            for (int i = 0; i < wins; i++) Kp.RanaWindow(m, k, -half + 2 * half * (i + 0.5) / wins, bv + 0.45, 0.9, 1.75, Kp.ShutterWhite, true, lod == 0 ? 0 : 2);
            Kp.Band(m, k, -half - 0.05, half + 0.05, DakhaEave - PlatformH - 0.6, DakhaEave - PlatformH - 0.4, 0.14, Kp.WhiteTrim);
            if (lod <= 1) Kp.Struts(m, rk, roof, DakhaEave - 1.9, 1.0, false, 501u, lod);
            if (lod <= 1)
                for (int i = 0; i < 2; i++)
                    Kp.Dormer(m, rk, -6.5 + 13.0 * i, DakhaRidge - 1.0, 0, 1.9, lod);
            // The stepped platform: a stone deck along the facade with four steps down to the forecourt.
            Affine3 p = f.GridSide(DA0, DA1 - 2.0, DB0, DB1, 0, 0, out half);
            Kp.Box(m, p, -half, half, -0.2, PlatformH, 0, PlatformDepth, Kp.Step, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            for (int s = 0; s < 4; s++)
            {
                double top = PlatformH - 0.15 * (s + 1), w0 = PlatformDepth + StepRun * s, w1 = w0 + StepRun;
                if (top <= 0.01) break;
                Kp.Box(m, p, -half, half, -0.2, top, w0, w1, Kp.Ao(Kp.Step, 0.94f), BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            }
            if (f.C != null)
            {
                double am = 0.5 * (DA0 + DA1 - 2.0);
                f.GridBox(DA0, DA1 - 2.0, DB1, DB1 + PlatformDepth, 0, PlatformH, GenColliderFlags.Walkable, GenColliders.Stone);
                f.GridRamp(am, DB1 + PlatformDepth + 3 * StepRun, 0, am, DB1 + PlatformDepth, PlatformH, half, GenColliders.Stone);
            }
        }

        /// <summary>The four inner faces of a court: white walls with two rows of closed shutters (LOD0).</summary>
        private static void CourtFaces(ref KpFrame f, MeshData m, double a0, double a1, double b0, double b1)
        {
            for (int s = 0; s < 4; s++)
            {
                double half;
                Affine3 k = f.GridSide(a0, a1, b0, b1, 0, s, out half);
                k = SacredDraw.Basis(k, 0, 0, 0, -1, 0, 0, 0, 1, 0);
                int n = Math.Max(1, (int)Math.Round(2 * half / 3.0));
                for (int fl = 0; fl < 2; fl++)
                    for (int i = 0; i < n; i++)
                        Kp.RanaWindow(m, k, -half + 2 * half * (i + 0.5) / n, CourtFloors[fl] + 0.7, 0.85, 1.5, Kp.Shutter, true, 2);
            }
        }

        // =============================================================================================================
        // The white wings on Basantapur
        // =============================================================================================================

        /// <summary>The white south-west wing (w906935471, run on east to the nine-storey tower) and the white range to Gaddi
        /// Baithak (w910121098), kept for the catalogue and the tests (palace grid; the model builds them in their own
        /// frame along the real front line, see <see cref="WingFrame"/>).</summary>
        public const double WA0 = -11.0, WA1 = 8.6, WB0 = -74.2, WB1 = -60.2, RA0 = -33.0, RA1 = -11.0, RB0 = -75.7, RB1 = -62.8, WingEave = 9.6, WingRidge = 12.6;

        /// <summary>The white wing's front line on Basantapur (OSM w910121098 / w906935471: from grid (−32.9, −75.7) to (6.1,
        /// −72.7), about 4.4° off the palace grid): its middle, length and bearing; the west block (RA) and the east block
        /// (WA, run on to the tower at grid a 8.6) along it, their depths, the attic of the east block.</summary>
        public const double FrontA0 = -32.9, FrontB0 = -75.7, FrontA1 = 6.1, FrontB1 = -72.7;

        public const double WestX0 = -19.55, SeamX = 2.7, EastX1 = 22.05, WestDepth = 11.4, EastDepth = 12.7;

        /// <summary>The east block's attic: its set-back from the front, its half width, eave and ridge.</summary>
        public const double AtticBack = 1.75, AtticHalf = 7.2, AtticEave = 13.2, AtticRidge = 15.6;

        /// <summary>The railing and the stone curb in front of the wing and the tower on Basantapur (gaddi ref_01): its
        /// distance out from the front, its height.</summary>
        public const double RailOut = 1.9, RailH = 1.2;

        /// <summary>The plan frame of the white wing: origin at the middle of its front line on the square, x along the front
        /// to the east, z north into the building.</summary>
        public static Affine3 WingFrame(ref KpFrame f, out double bearing)
        {
            double am = 0.5 * (FrontA0 + FrontA1), bmid = 0.5 * (FrontB0 + FrontB1), x, y;
            KpFrame.FromGrid(am, bmid, out x, out y);
            bearing = 102 - Math.Atan2(FrontB1 - FrontB0, FrontA1 - FrontA0) * 180 / Math.PI;
            return f.Plan(x, y, 0, bearing);
        }

        /// <summary>
        /// The white wing on Basantapur (§3.16; photos gaddi ref_01, ref_06, basantapur_tower c_02, c_10), one front of white
        /// plaster along the real front line: the west block (RA) three storeys under one deep tile roof whose eave sits on
        /// struts right above the second row of teal-shuttered windows (9.6 m), no row above; the east block (WA) the same
        /// front with, set back behind the main roof's slope, an attic storey of three windows under its own hipped roof and
        /// the small white roof pavilion. The plinth band and the eave fascia run unbroken across both blocks. In front: the
        /// grey stone flags, a low stone curb and the black iron railing.
        /// </summary>
        public static void SouthWing(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            double bearing;
            Affine3 k = WingFrame(ref f, out bearing);
            double wm = 0.5 * (WestX0 + SeamX), em = 0.5 * (SeamX + EastX1), wh = 0.5 * (SeamX - WestX0), eh = 0.5 * (EastX1 - SeamX);
            if (lod >= 3)
            {
                Kp.Box(m, k, WestX0, SeamX, 0, WingEave, 0, WestDepth, Kp.White, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, k, wm, 0.5 * WestDepth, wh + 1.2, 0.5 * WestDepth + 1.2, WingEave, WingRidge, Kp.Tile);
                Kp.Box(m, k, SeamX, EastX1, 0, WingEave, 0, EastDepth, Kp.White, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, k, em, 0.5 * EastDepth, eh + 1.2, 0.5 * EastDepth + 1.2, WingEave, WingRidge, Kp.Tile);
                Kp.Box(m, k, em - AtticHalf, em + AtticHalf, WingEave, AtticEave, AtticBack, EastDepth - 1.5, Kp.White, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                return;
            }
            // The blocks' walls and their deep tile roofs (eave 9.6 m), hipped, ridges along the front.
            Kp.Box(m, k, WestX0, SeamX, -0.2, WingEave + 0.2, 0, WestDepth, Kp.White, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Kp.Box(m, k, SeamX, EastX1, -0.2, WingEave + 0.2, 0, EastDepth, Kp.White, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Affine3 rw = SacredDraw.At(k, wm, 0, 0.5 * WestDepth), re = SacredDraw.At(k, em, 0, 0.5 * EastDepth);
            RoofSpec roofW = Kp.Ridge(m, rw, wh, 0.5 * WestDepth, WingEave, WingRidge, 1.4, Kp.Tile, false, lod);
            RoofSpec roofE = Kp.Ridge(m, re, eh, 0.5 * EastDepth, WingEave, WingRidge - 0.2, 1.4, Kp.Tile, false, lod);
            // The attic of the east block, set back from the front, under its own hipped roof with the pavilion.
            double az0 = AtticBack, az1 = EastDepth - 1.5;
            Kp.Box(m, k, em - AtticHalf, em + AtticHalf, WingEave - 0.5, AtticEave + 0.2, az0, az1, Kp.White, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Affine3 ra = SacredDraw.At(k, em, 0, 0.5 * (az0 + az1));
            RoofSpec roofA = Kp.Ridge(m, ra, AtticHalf, 0.5 * (az1 - az0), AtticEave, AtticRidge, 1.0, Kp.Tile, false, lod);
            // The front faces (face u runs along −x: the viewer's right is east).
            Affine3 front = KpStack.Face(k, 270, 0);
            WingFace(m, front, -SeamX, -WestX0, lod);
            WingFace(m, front, -EastX1, -SeamX, lod);
            // The unbroken plinth band and eave fascia across both blocks.
            Kp.Band(m, front, -EastX1, -WestX0, 0, 0.35, 0.06, Kp.Step);
            Kp.Band(m, front, -EastX1 - 0.05, -WestX0 + 0.05, WingEave - 0.5, WingEave - 0.32, 0.12, Kp.WhiteTrim);
            // The attic's three windows.
            Affine3 af = SacredDraw.At(KpStack.Face(k, 270, 0), 0, 0, -az0);
            // A full storey clear of the main roof's slope (gaddi ref_01: the three windows stand wholly above it).
            for (int i = -1; i <= 1; i++) Kp.RanaWindow(m, af, -em + i * 3.6, AtticEave - 2.05, 0.95, 1.3, Kp.Teal, false, lod);
            if (lod <= 1)
            {
                Kp.Struts(m, rw, roofW, WingEave - 1.6, 1.0, false, 607u, lod);
                Kp.Struts(m, re, roofE, WingEave - 1.6, 1.0, false, 601u, lod);
                Kp.Struts(m, ra, roofA, AtticEave - 1.2, 1.0, false, 603u, lod);
                Kp.Dormer(m, ra, 0, AtticRidge - 0.8, 0, 1.8, lod);
            }
            Frontage(ref f, m, k, WestX0, EastX1, lod);
            if (f.C != null)
            {
                double x, y;
                Local(ref f, wm, 0.5 * WestDepth, out x, out y);
                f.Box(x, y, wh, 0.5 * WestDepth, bearing, 0, WingRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                Local(ref f, em, 0.5 * EastDepth, out x, out y);
                f.Box(x, y, eh, 0.5 * EastDepth, bearing, 0, AtticRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }

        /// <summary>Site-local (x, y) of a point (px, pz) of the wing frame.</summary>
        private static void Local(ref KpFrame f, double px, double pz, out double x, out double y)
        {
            double bearing;
            WingFrame(ref f, out bearing);
            double am = 0.5 * (FrontA0 + FrontA1), bmid = 0.5 * (FrontB0 + FrontB1), cx, cy;
            KpFrame.FromGrid(am, bmid, out cx, out cy);
            double r = bearing * Math.PI / 180, r2 = (bearing - 90) * Math.PI / 180;
            x = cx + px * Math.Sin(r) + pz * Math.Sin(r2);
            y = cy + px * Math.Cos(r) + pz * Math.Cos(r2);
        }

        /// <summary>
        /// The white wing's front from face u0 to u1 (photo gaddi ref_01): a grey plinth; round-headed windows with dark
        /// lattice on the ground floor; two rows of windows (about every 4.4 m) with open teal shutters, recessed plaster
        /// panels between them; the eave right above the second row.
        /// </summary>
        private static void WingFace(MeshData m, in Affine3 k, double u0, double u1, int lod)
        {
            const double Pitch = 4.4;
            double len = u1 - u0;
            int bays = Math.Max(1, (int)Math.Round(len / Pitch));
            double bay = len / bays;
            for (int i = 0; i < bays; i++)
            {
                double u = u0 + bay * (i + 0.5);
                Kp.ArchedLatticeWindow(m, k, u, 0.55, 0.95, 1.85, lod);
                Kp.RanaWindow(m, k, u, 3.6, 0.95, 1.65, Kp.Teal, false, lod);
                Kp.RanaWindow(m, k, u, 6.55, 0.95, 1.65, Kp.Teal, false, lod);
                if (i == bays - 1) continue;
                double um = u + 0.5 * bay;
                Kp.BlindPanel(m, k, um, 1.0, 0.9, 1.4, lod);
                Kp.BlindPanel(m, k, um, 3.7, 0.9, 1.4, lod);
                Kp.BlindPanel(m, k, um, 6.65, 0.9, 1.4, lod);
            }
            Kp.Band(m, k, u0, u1, 3.0, 3.15, 0.07, Kp.WhiteTrim);
            Kp.Band(m, k, u0, u1, 5.95, 6.1, 0.07, Kp.WhiteTrim);
        }

        /// <summary>
        /// The frontage along Basantapur in front of a front line (gaddi ref_01, basantapur ref_08): the grey stone flags
        /// from the wall out past the railing, a low stone curb and the black iron railing on it (posts every 2 m, bars every
        /// 0.15 m), with its colliders (the camera passes over the railing; the flags are walkable). Frame <paramref name="k"/>
        /// has x along the front and z into the building; the front runs from x0 to x1.
        /// </summary>
        internal static void Frontage(ref KpFrame f, MeshData m, in Affine3 k, double x0, double x1, int lod)
        {
            Affine3 face = KpStack.Face(k, 270, 0);
            double u0 = -x1, u1 = -x0;
            // The flags (a hair over the square), the curb under the railing.
            Kp.Floor(m, face, u0, u1, 0, RailOut + 0.45, 0.02, Kp.ForecourtFlags);
            Kp.Box(m, face, u0, u1, -0.1, 0.2, RailOut - 0.16, RailOut + 0.16, Kp.Step, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            Kp.Railing(m, face, u0 + 0.3, u1 - 0.3, RailOut, 0.2, RailH, lod);
            if (f.C == null) return;
            double ax, ay, bx, by;
            FrontLocal(ref f, k, x0 + 0.3, -RailOut, out ax, out ay);
            FrontLocal(ref f, k, x1 - 0.3, -RailOut, out bx, out by);
            f.Wall(ax, ay, bx, by, 0, RailH + 0.2, 0.06);
        }

        /// <summary>Site-local (x, y) of a point (px, pz) of a plan frame <paramref name="k"/> (any frame on the square):
        /// inverted through the tile frame.</summary>
        private static void FrontLocal(ref KpFrame f, in Affine3 k, double px, double pz, out double x, out double y)
        {
            double tx, ty, tz;
            k.Point(px, 0, pz, out tx, out ty, out tz);
            f.FromTile(tx, tz, out x, out y);
        }

        // =============================================================================================================
        // The white ranges west of the Dakha front on the forecourt
        // =============================================================================================================

        /// <summary>
        /// The white ranges that continue the Dakha front west along the forecourt (OSM w907080042, w1415152801 with
        /// w1029965548, and w1117943799; photo dakha_north c_07, 2026 imagery): the square block at the forecourt's west end
        /// under a grey-green metal pyramidal roof with dormers on two slopes (eave 12 m), a white range in the Dakha front's
        /// language beyond it (red dado and bench, blind cusped arches, white-shuttered windows about every 3.5 m, the black
        /// gallery on diagonal struts on the top floor, a tile roof with white dormer lanterns, eave 12 m, ridge 15 m), and the
        /// long white range behind them to the south.
        /// </summary>
        private static void DakhaWest(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            const double ce = 12.0, cr = 15.0;
            // The square west block (w907080042), clipped to the Dakha range's west end.
            const double qa0 = -52.9, qa1 = DA0, qb0 = -20.9, qb1 = -6.1;
            KpFacade.Walls(m, ref f, qa0, qa1, qb0, qb1, 0, ce + 0.2, Kp.White);
            RoofSpec qr = KpFacade.Roof(m, ref f, qa0, qa1, qb0, qb1, ce, cr + 0.6, 1.3, Kp.MetalRoof, false, lod);
            Affine3 qk = KpFacade.RoofFrame(ref f, qa0, qa1, qb0, qb1);
            if (lod <= 1)
            {
                // Standing seams on the sheet metal and the two rows of dormers (north and south slopes).
                if (lod == 0) Kp.Seams(m, qk, qr, 1.6);
                double hw = 0.5 * (qb1 - qb0) + 1.3, mid = ce + 0.45 * (cr + 0.6 - ce);
                for (int r = -1; r <= 1; r += 2)
                    for (int i = -1; i <= 1; i++)
                        Kp.Dormer(m, qk, i * 4.2, mid, r * 0.5 * hw, 1.5, lod);
                if (lod == 0) Kp.Struts(m, qk, qr, ce - 1.8, 1.0, false, 511u, lod);
            }
            double half;
            Affine3 qn = f.GridSide(qa0, qa1, qb0, qb1, 0, 0, out half);
            DakhaLanguage(m, qn, half, lod, true);
            Affine3 qw = f.GridSide(qa0, qa1, qb0, qb1, 0, 3, out half);
            // The faces off the forecourt (seen across the roofs or from the lanes) drop to the far facade from LOD1.
            int far = lod == 0 ? 1 : Math.Max(2, lod);
            DakhaLanguage(m, qw, half, far, false);
            // The range beyond it (w1415152801 + w1029965548) and its west annex. The annex's OSM outline runs on west to a
            // slanted face at grid a -70.7..-68.5, but the street drawn along it covers that end (0.8 m deep at b -18.5, 4 m
            // at b -11.8), so the annex stops at a -66.2, clear of what the road mesher draws.
            const double ra0 = -63.7, ra1 = qa0, rb0 = -17.0, rb1 = -8.5, wa0 = -66.2, wb0 = -17.0, wb1 = -12.1;
            KpFacade.Range(m, ref f, ra0, ra1, rb0, rb1, ce, cr, 1.3, Kp.White, Kp.TileMoss, false, lod);
            KpFacade.Range(m, ref f, wa0, ra0, wb0, wb1, ce - 0.6, cr - 1.6, 0.8, Kp.White, Kp.TileMoss, false, lod);
            Affine3 rn = f.GridSide(ra0, ra1, rb0, rb1, 0, 0, out half);
            DakhaLanguage(m, rn, half, lod, true);
            Affine3 rwn = f.GridSide(wa0, ra0, wb0, wb1, 0, 0, out half);
            DakhaLanguage(m, rwn, half, far, true);
            if (lod <= 1)
            {
                Affine3 rk = KpFacade.RoofFrame(ref f, ra0, ra1, rb0, rb1);
                for (int i = 0; i < 2; i++) Kp.Dormer(m, rk, -2.6 + 5.2 * i, cr - 1.0, 0, 1.7, lod);
            }
            // The long range behind them to the south (w1117943799).
            const double la0 = -47.1, la1 = -40.0, lb0 = -42.8, lb1 = qb0;
            KpFacade.Range(m, ref f, la0, la1, lb0, lb1, ce, cr, 1.3, Kp.White, Kp.TileMoss, false, lod);
            Affine3 lw = f.GridSide(la0, la1, lb0, lb1, 0, 3, out half);
            KpFacade.Rana(m, lw, half, 0, CourtFloors, 2, ce, KpFoot.BlindArches, 3.5, Kp.ShutterWhite, false, true, true, far);
            if (lod <= 1)
            {
                Affine3 lk = KpFacade.RoofFrame(ref f, la0, la1, lb0, lb1);
                for (int i = -1; i <= 1; i++) Kp.Dormer(m, lk, 0, cr - 1.0, i * 6.5, 1.6, lod);
            }
            if (f.C == null) return;
            f.GridBox(qa0, qa1, qb0, qb1, 0, cr, GenColliderFlags.NoClimb, GenColliders.Brick);
            f.GridBox(ra0, ra1, rb0, rb1, 0, cr, GenColliderFlags.NoClimb, GenColliders.Brick);
            f.GridBox(wa0, ra0, wb0, wb1, 0, cr - 1.6, GenColliderFlags.NoClimb, GenColliders.Brick);
            f.GridBox(la0, la1, lb0, lb1, 0, cr, GenColliderFlags.NoClimb, GenColliders.Brick);
        }

        /// <summary>The Dakha front's language on a white range face (dakha_north c_07): red dado and bench, blind cusped
        /// arches (a dark colonnade on the forecourt fronts when <paramref name="colonnade"/>), white-shuttered windows about
        /// every 3.5 m, the black gallery on diagonal struts on the top floor.</summary>
        private static void DakhaLanguage(MeshData m, in Affine3 k, double half, int lod, bool colonnade)
        {
            if (colonnade && lod <= 1)
            {
                // The ground-floor veranda of dark posts before a shaded wall (c_07).
                Kp.Panel(m, k, -half + 0.4, 0.3, half - 0.4, DakhaF1 - 0.5, 0.01, Kp.Ao(Kp.Recess, 0.85f));
                int n = Math.Max(2, (int)Math.Round(2 * half / 2.4));
                for (int i = 0; i <= n; i++)
                {
                    double u = -half + 0.4 + (2 * half - 0.8) * i / n;
                    SacredParts.Post(m, SacredDraw.At(k, 0, 0, 0.14), u, 0, 0.3, DakhaF1 - 0.6, 0.24, Kp.Wood, lod);
                }
                Kp.Band(m, k, -half, half, DakhaF1 - 0.6, DakhaF1 - 0.35, 0.3, Kp.WoodBeam);
                Kp.Band(m, k, -half, half, 0, 0.3, 0.3, Kp.Step);
                KpFacade.Rana(m, k, half, 0, CourtFloors, 2, 12.0, KpFoot.Plain, 3.5, Kp.ShutterWhite, false, true, false, lod);
                return;
            }
            KpFacade.Rana(m, k, half, 0, CourtFloors, 2, 12.0, KpFoot.BlindArches, 3.5, Kp.ShutterWhite, false, true, true, lod);
        }
    }
}
