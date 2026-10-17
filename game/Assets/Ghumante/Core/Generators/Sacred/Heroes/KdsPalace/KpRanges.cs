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
        private static readonly double[] WingFloors = { 4.2, 8.2 };
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
            KpFacade.Rana(m, e, half, 0, TribhuvanFloors, 3, TribhuvanEave, KpFoot.Plain, 2.9, Kp.Shutter, true, false, false, Math.Max(1, lod));
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
            if (lod == 0)
            {
                // The court faces: rows of shuttered windows (seen from the towers and the air).
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
                if (i % 3 == 1) Kp.RanaWindow(m, k, u, 1.1, 0.8, 1.5, Kp.Shutter, true, lod);
                else Kp.BlindArch(m, k, u, 0.7, w - 0.45, 2.7, lod);
            }
            Kp.Band(m, k, -half, half, DakhaF1 - 0.62 - 0.12, DakhaF1 - 0.62 + 0.06, 0.08, Kp.WhiteTrim);
            // First-floor windows, every 3.4 m.
            int wins = Math.Max(1, (int)Math.Round(2 * half / 3.4));
            for (int i = 0; i < wins; i++) Kp.RanaWindow(m, k, -half + 2 * half * (i + 0.5) / wins, DakhaF1 - 0.6 + 0.55, 0.95, 1.7, Kp.Shutter, false, lod);
            // The black balcony on its struts, the upper windows behind it.
            double bv = DakhaBalcony - PlatformH;
            Kp.Gallery(m, k, -half - 0.05, half + 0.05, bv, 1.0, 0.95, 0.95, lod);
            for (int i = 0; i < wins; i++) Kp.RanaWindow(m, k, -half + 2 * half * (i + 0.5) / wins, bv + 0.45, 0.9, 1.75, Kp.Shutter, false, lod);
            Kp.Band(m, k, -half - 0.05, half + 0.05, DakhaEave - PlatformH - 0.6, DakhaEave - PlatformH - 0.4, 0.14, Kp.WhiteTrim);
            if (lod <= 1) Kp.Struts(m, rk, roof, DakhaEave - 1.9, 1.35, false, 501u, lod);
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
                        Kp.RanaWindow(m, k, -half + 2 * half * (i + 0.5) / n, CourtFloors[fl] + 0.7, 0.85, 1.5, Kp.Shutter, true, 1);
            }
        }

        // =============================================================================================================
        // The white wings on Basantapur
        // =============================================================================================================

        /// <summary>The white south-west wing (w906935471, run on east to the nine-storey tower) and the white range to Gaddi
        /// Baithak (w910121098).</summary>
        public const double WA0 = -11.0, WA1 = 8.6, WB0 = -74.2, WB1 = -60.2, RA0 = -33.0, RA1 = -11.0, RB0 = -75.7, RB1 = -62.8, WingEave = 12.4, WingRidge = 15.6;

        /// <summary>
        /// The white wings on Basantapur (§3.16): three storeys of white plaster, regular windows in dark frames with
        /// green-teal louvred shutters, dark lattice windows and blind arches on the ground floor, a deep eave on struts and a
        /// hipped tile roof with a small white roof pavilion.
        /// </summary>
        public static void SouthWing(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            Affine3 g = f.Grid;
            if (lod >= 3)
            {
                Kp.Box(m, g, WA0, WA1, 0, WingEave, WB0, WB1, Kp.White, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, g, 0.5 * (WA0 + WA1), 0.5 * (WB0 + WB1), 10.6, 8.0, WingEave, WingRidge, Kp.Tile);
                Kp.Box(m, g, RA0, RA1, 0, WingEave, RB0, RB1, Kp.White, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, g, 0.5 * (RA0 + RA1), 0.5 * (RB0 + RB1), 12.0, 7.4, WingEave, WingRidge, Kp.Tile);
                return;
            }
            Wing(ref f, m, WA0, WA1, WB0, WB1, KpFoot.Lattice, 2.6, true, 601u);
            Wing(ref f, m, RA0, RA1, RB0, RB1, KpFoot.BlindArches, 2.8, false, 607u);
            if (f.C != null)
            {
                f.GridBox(WA0, WA1, WB0, WB1, 0, WingRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(RA0, RA1, RB0, RB1, 0, WingRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }

        private static void Wing(ref KpFrame f, MeshData m, double a0, double a1, double b0, double b1, KpFoot foot, double pitch, bool pavilion, uint seed)
        {
            int lod = f.Lod;
            RoofSpec roof = KpFacade.Range(m, ref f, a0, a1, b0, b1, WingEave, WingRidge, 1.4, Kp.White, Kp.Tile, false, lod);
            double half;
            Affine3 s = f.GridSide(a0, a1, b0, b1, 0, 2, out half);
            KpFacade.Rana(m, s, half, 0, WingFloors, 2, WingEave, foot, pitch, Kp.Teal, false, false, true, lod);
            Affine3 rk = KpFacade.RoofFrame(ref f, a0, a1, b0, b1);
            if (lod <= 1) Kp.Struts(m, rk, roof, WingEave - 1.8, 1.4, false, seed, lod);
            if (pavilion && lod <= 1) Kp.Dormer(m, rk, 0, WingRidge - 1.0, 0, 2.0, lod);
        }
    }
}
