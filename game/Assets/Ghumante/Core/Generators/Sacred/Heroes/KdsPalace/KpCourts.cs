using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// The palace courts and the ranges round them (kds-palace.md §3.6, §3.7, §3.9, §3.12, §3.16), on the palace grid:
    /// Mohan and Sundari Chowk (closed, seen from above: tiled ranges, four corner turrets, the Sun Dhara pit), Nasal Chowk
    /// (the stone court, the coronation platform, the white north range with its cream arcade, green shutters and white
    /// gallery, the red-post east veranda, the white shrine and the low range at the tower's foot), Mul Chowk (two-storey
    /// brick court with the gilt roof of Taleju's shrine) and Lohan Chowk (four-storey brick ranges with colonnades and
    /// dense carved windows, the brick south facade on Basantapur with its railing and two grey lions, the east shop
    /// range). Court faces carry their dressing at LOD0-1; LOD2 keeps masses, roofs and the coloured bands.
    /// </summary>
    internal static class KpCourts
    {
        private static readonly double[] Floors3 = { 3.6, 7.0 };
        private static readonly double[] Floors4 = { 2.8, 5.3, 7.7 };
        private static readonly double[] Floors2 = { 3.4 };

        // =============================================================================================================
        // Mohan and Sundari Chowk
        // =============================================================================================================

        /// <summary>Mohan court (palace grid) and its ranges' eave and ridge.</summary>
        public const double MA0 = 4.5, MA1 = 13.5, MB0 = 2.0, MB1 = 15.5, MohanEave = 10.5, MohanRidge = 13.2;

        /// <summary>
        /// Mohan Chowk (1649) and Sundari Chowk north of it: three-storey ranges round the courts (brick and plaster), small
        /// two-roof turrets at the Mohan court's four corners, the Sun Dhara pit's white horseshoe parapet in the middle of
        /// the court, Sundari's white west front on the forecourt side.
        /// </summary>
        public static void Mohan(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            ShapeBrush brick = Kp.Brick, tile = Kp.Tile;
            if (lod >= 3)
            {
                Kp.Box(m, f.Grid, -3.5, 17.0, 0, MohanEave, -3.0, 23.6, brick, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, f.Grid, 6.75, 10.3, 11.6, 14.6, MohanEave, MohanRidge + 1, tile);
                Kp.Box(m, f.Grid, -9.5, 10.5, 0, 9.5, 23.6, 36.5, brick, BoxFaces.All & ~BoxFaces.Bottom);
                return;
            }
            // Mohan: west, south, east and north ranges round the court.
            KpFacade.Range(m, ref f, -3.5, MA0, -3.0, 12.6, MohanEave, MohanRidge, 1.0, brick, tile, false, lod);
            KpFacade.Range(m, ref f, 1.6, MA0, 12.6, 23.6, MohanEave, MohanRidge, 1.0, brick, tile, false, lod);
            KpFacade.Range(m, ref f, MA0, 17.0, -3.0, MB0, MohanEave, MohanRidge, 1.0, brick, tile, false, lod);
            KpFacade.Range(m, ref f, MA1, 17.0, MB0, 23.6, MohanEave, MohanRidge, 1.0, brick, tile, false, lod);
            KpFacade.Range(m, ref f, MA0, MA1, MB1, 23.6, MohanEave, MohanRidge, 1.0, brick, tile, false, lod);
            KpFacade.Court(m, ref f, MA0, MA1, MB0, MB1, Kp.Flags);
            if (lod <= 1)
            {
                // Court faces: carved windows on three floors (one level coarser than the outer faces: the court is only
                // seen from inside it or from above).
                for (int s = 0; s < 4; s++)
                {
                    double half;
                    Affine3 k = f.GridSide(MA0, MA1, MB0, MB1, 0, s, out half);
                    KpFacade.Newar(m, Flip(k), half, 0, Floors3, 2, MohanEave + 0.3, 2.6, -1, -1, false, s == 0, lod + 1);
                }
                // The east face on the lane to Taleju.
                double h;
                Affine3 e = f.GridSide(MA1, 17.0, -3.0, 23.6, 0, 1, out h);
                KpFacade.Newar(m, e, h, 0, Floors3, 2, MohanEave + 0.3, 2.6, -1, -1, false, false, Math.Max(1, lod));
            }
            // The corner turrets.
            for (int i = 0; i < 4; i++)
            {
                double a = i % 2 == 0 ? MA0 : MA1, b = i < 2 ? MB0 : MB1;
                Turret(ref f, m, a, b, 1.5, MohanEave + 0.3, false, lod);
            }
            // The Sun Dhara pit: dark depth inside a white horseshoe parapet, open to the south.
            double px, py;
            KpFrame.FromGrid(9.3, 9.3, out px, out py);
            Affine3 pit = f.Plan(px, py, 0, 102);
            Shapes.Cylinder(m, SacredDraw.At(pit, 0, 0.02, 0), Kp.Ao(Kp.Recess, 0.7f), 2.6, 0.03, SacredParts.Seg(20, lod), 0, 0, false, true);
            Shapes.Torus(m, SacredDraw.At(pit, 0, 0.55, 0), Kp.White, 2.85, 0.22, SacredParts.Seg(24, lod), 4, f.SL, 120, 300, true);
            Shapes.Torus(m, SacredDraw.At(pit, 0, 0.25, 0), Kp.White, 2.85, 0.3, SacredParts.Seg(24, lod), 4, f.SL, 120, 300, true);
            // Sundari Chowk: four ranges round a smaller court, the west front white.
            KpFacade.Range(m, ref f, -9.5, -3.0, 23.6, 36.5, 9.5, 12.0, 1.0, Kp.White, tile, false, lod);
            KpFacade.Range(m, ref f, 5.0, 10.5, 23.6, 36.5, 9.5, 12.0, 1.0, brick, tile, false, lod);
            KpFacade.Range(m, ref f, -3.0, 5.0, 23.6, 26.5, 9.5, 12.0, 1.0, brick, tile, false, lod);
            KpFacade.Range(m, ref f, -3.0, 5.0, 33.5, 36.5, 9.5, 12.0, 1.0, brick, tile, false, lod);
            KpFacade.Court(m, ref f, -3.0, 5.0, 26.5, 33.5, Kp.Flags);
            if (lod <= 1)
            {
                double half;
                Affine3 w = f.GridSide(-9.5, -3.0, 23.6, 36.5, 0, 3, out half);
                KpFacade.Rana(m, w, half, 0, Floors3, 2, 9.5, KpFoot.Plain, 2.6, Kp.Shutter, true, false, true, lod);
            }
            if (f.C != null)
            {
                f.GridBox(-3.5, 17.0, -3.0, 23.6, 0, MohanRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(-9.5, 10.5, 23.6, 36.5, 0, 12.0, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }

        /// <summary>A face frame turned to look the other way (u mirrored, w reversed): the court side of a range's wall seen
        /// from inside the court.</summary>
        private static Affine3 Flip(in Affine3 k)
        {
            return SacredDraw.Basis(k, 0, 0, 0, -1, 0, 0, 0, 1, 0);
        }

        /// <summary>A small square roof turret at a court corner: a brick box rising out of the range roofs with two small tile
        /// roofs (red valances when <paramref name="red"/>) and a finial.</summary>
        private static void Turret(ref KpFrame f, MeshData m, double a, double b, double half, double from, bool red, int lod)
        {
            double x, y;
            KpFrame.FromGrid(a, b, out x, out y);
            Affine3 k = f.Plan(x, y, 0, 102);
            if (lod >= 2)
            {
                // LOD2: the turret's box and one roof.
                Kp.Box(m, k, -half, half, from - 0.5, from + 2.6, -half, half, Kp.Brick, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                KpLod3.Pyramid(m, k, 0, 0, half + 1.0, half + 1.0, from + 2.6, from + 5.4, Kp.Tile);
                return;
            }
            var t = new KpTier[2];
            t[0] = new KpTier { Base = from, WallU = half, WallW = half, Eave = from + 2.6, EaveU = half + 1.0, EaveW = half + 1.0, Top = from + 3.3, Walls = KpWalls.Brick, Windows = 1 };
            t[1] = new KpTier { Base = from + 3.3, WallU = 0.85 * half, WallW = 0.85 * half, Eave = from + 4.4, EaveU = half + 0.6, EaveW = half + 0.6, Top = from + 5.4, ApexU = 0.15, ApexW = 0.15, Walls = KpWalls.Lattice };
            KpRoofLook look = KpRoofLook.TilePlain(Kp.Tile);
            look.Fringe = red;
            look.Struts = lod == 0;
            KpStack.Build(m, k, t, 2, Kp.Brick, look, 7u + (uint)((int)Math.Round(a * 13 + b * 7) & 0xFFFF), Math.Max(1, lod));
            Kp.Finial(m, k, 0, from + 5.35, 0, 0.55, 0.12, lod);
        }

        // =============================================================================================================
        // Nasal Chowk
        // =============================================================================================================

        /// <summary>Nasal court (palace grid; §3.7: 20 × 47 m, long axis 12°).</summary>
        public const double NA0 = 0.9, NA1 = 21.0, NB0 = -56.0, NB1 = -8.9;

        /// <summary>The north range's storeys: arcade top, gallery floor, eave and ridge.</summary>
        public const double NorthF1 = 3.6, NorthF2 = 6.9, NorthEave = 10.5, NorthRidge = 13.1;

        /// <summary>The east range's eave and ridge (a long low brick range with an open veranda).</summary>
        public const double EastEave = 6.8, EastRidge = 9.4;

        /// <summary>The coronation platform (palace grid) and its height.</summary>
        public const double PA0 = 9.2, PA1 = 14.2, PB0 = -41.9, PB1 = -34.9, PlatformH = 0.6;

        public static void Nasal(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            Affine3 g = f.Grid;
            if (lod >= 3)
            {
                KpFacade.Court(m, ref f, NA0, NA1, NB0, NB1, Kp.Flags);
                Kp.Box(m, g, -3.5, 15.6, 0, NorthEave, -8.9, -3.0, Kp.White, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, g, 6.05, -5.95, 10.6, 4.0, NorthEave, NorthRidge, Kp.Tile);
                Kp.Box(m, g, NA1, 25.0, 0, EastEave, NB0, NB1, Kp.Brick, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, g, 23.0, -32.45, 3.2, 24.6, EastEave, EastRidge, Kp.Tile);
                Kp.Box(m, g, PA0, PA1, 0, PlatformH, PB0, PB1, Kp.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
                return;
            }
            KpFacade.Court(m, ref f, NA0, NA1, NB0, NB1, Kp.Flags);
            NorthRange(ref f, m);
            EastRange(ref f, m, NB1, -33.6);
            EastRange(ref f, m, -42.6, NB0);
            Platform(ref f, m);
            WhiteShrine(ref f, m, 18.6, -50.5);
            // The south-west range (white, three storeys) between the court and the white wing.
            KpFacade.Range(m, ref f, NA0, 8.6, -60.2, NB0, 11.5, 14.0, 1.0, Kp.White, Kp.Tile, false, lod);
            double half;
            Affine3 sw = f.GridSide(NA0, 8.6, -60.2, NB0, 0, 0, out half);
            KpFacade.Rana(m, sw, half, 0, Floors3, 2, 11.5, KpFoot.Lattice, 2.4, Kp.Shutter, false, false, false, lod);
            // A low brick lean-to along the Basantapur tower's north foot, its tile roof sloping to the court.
            Kp.Box(m, g, 8.6, 22.2, -0.2, 3.2, -58.2, NB0 - 0.2, Kp.Brick, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Quad(m, g, 8.4, 4.4, -58.2, 22.4, 4.4, -58.2, 22.4, 3.1, NB0 + 0.6, 8.4, 3.1, NB0 + 0.6, 0, 1, 0.55, Kp.Tile);
            SacredDraw.Quad(m, g, 8.4, 3.1, NB0 + 0.6, 22.4, 3.1, NB0 + 0.6, 22.4, 2.9, NB0 + 0.6, 8.4, 2.9, NB0 + 0.6, 0, 0, 1, Kp.Wood);
            if (lod <= 1)
            {
                Affine3 lean = f.GridSide(8.6, 22.2, -58.2, NB0 - 0.2, 0, 0, out half);
                for (int i = 0; i < 4; i++) Kp.Doorway(m, lean, -half + 1.7 + i * (2 * half - 3.4) / 3, 0.15, 1.0, 2.1, Kp.Wood, false, lod);
            }
            if (f.C != null)
            {
                f.GridBox(-3.5, 15.6, -7.3, -3.0, 0, NorthRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(-3.5, 15.6, -8.9, -7.3, NorthF1, NorthRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(NA0, 8.6, -60.2, NB0, 0, 14.0, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(8.6, 22.2, -58.2, NB0 - 0.2, 0, 4.4, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }

        /// <summary>
        /// The white north range (§3.7): the ground floor an arcade of cream-yellow columns and arches before a shaded
        /// loggia with potted plants; the first floor a continuous row of green louvred shutters; the second floor an open
        /// gallery of white arches with a dark balustrade; the deep tile roof and a small red-valanced turret at the west end.
        /// </summary>
        private static void NorthRange(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            const double a0 = -3.5, a1 = 15.6, b0 = -8.9, b1 = -3.0, loggia = 1.6;
            Affine3 g = f.Grid;
            // Masses: the ground floor set back behind the arcade, the upper floors full depth.
            Kp.Box(m, g, a0, a1, -0.2, NorthF1, b0 + loggia, b1, Kp.White, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Kp.Box(m, g, a0, a1, NorthF1, NorthEave + 0.2, b0, b1, Kp.White, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right | BoxFaces.Bottom);
            RoofSpec roof = KpFacade.Roof(m, ref f, a0, a1, b0, b1, NorthEave, NorthRidge, 1.1, Kp.Tile, false, lod);
            double half;
            Affine3 k = f.GridSide(a0, a1, b0, b1, 0, 2, out half);
            int bays = 8;
            double bay = 2 * half / bays;
            // The loggia's back wall in shade and the arcade.
            Kp.Panel(m, k, -half, 0, half, NorthF1, -loggia + 0.01, Kp.Ao(Kp.Cream, 0.62f));
            for (int i = 0; i <= bays; i++)
            {
                double u = -half + 0.35 + (2 * half - 0.7) * i / bays;
                if (lod >= 2) Kp.Box(m, k, u - 0.2, u + 0.2, 0, NorthF1, -0.4, 0, Kp.Cream, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                else
                {
                    Shapes.Cylinder(m, SacredDraw.At(k, u, 0.3, -0.25), Kp.Cream, 0.17, NorthF1 - 1.15, SacredParts.Seg(10, lod), 0, 0, false, false, f.SL);
                    Shapes.RoundedBox(m, SacredDraw.At(k, u, 0.15, -0.25), Kp.Cream, 0.46, 0.3, 0.46, 0.03, 1);
                    Shapes.RoundedBox(m, SacredDraw.At(k, u, NorthF1 - 0.78, -0.25), Kp.Cream, 0.44, 0.14, 0.44, 0.03, 1);
                }
                if (i < bays && lod <= 1)
                {
                    double uc = u + 0.5 * (2 * half - 0.7) / bays;
                    ShikharaGenerator.Arch(m, k, uc, NorthF1 - 0.72, -0.05, bay - 0.45, 0.42, 0.1, 0.12, lod == 0, Kp.Cream, lod);
                    if (lod == 0) Kp.PottedPlant(m, k, uc, 0, -0.9, 1.1, i % 3 == 1, lod);
                }
            }
            // The arcade's entablature (cream band) under the first floor.
            Kp.Box(m, k, -half, half, NorthF1 - 0.72, NorthF1 + 0.05, -0.12, 0.04, Kp.Cream, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            // First floor: the continuous row of green shutters.
            int wins = 16;
            for (int i = 0; i < wins; i++)
            {
                double u = -half + 2 * half * (i + 0.5) / wins;
                Kp.RanaWindow(m, k, u, NorthF1 + 0.85, 0.72, 1.75, Kp.Green, true, lod);
            }
            Kp.Band(m, k, -half, half, NorthF2 - 0.1, NorthF2 + 0.08, 0.1, Kp.WhiteTrim);
            // Second floor: the open gallery of white arches with a dark balustrade.
            double g0 = NorthF2 + 0.1, g1 = NorthEave - 0.55;
            Kp.Panel(m, k, -half + 0.1, g0, half - 0.1, g1, 0.01, Kp.Ao(Kp.Recess, 0.85f));
            int arches = 14;
            double aw = (2 * half - 0.2) / arches;
            for (int i = 0; i <= arches; i++)
            {
                double u = -half + 0.1 + aw * i;
                Kp.Box(m, k, u - 0.1, u + 0.1, g0, g1, 0, 0.14, Kp.White, lod >= 2 ? BoxFaces.Front : BoxFaces.Wall);
                if (i < arches && lod <= 1)
                    ShikharaGenerator.Arch(m, k, u + 0.5 * aw, g1 - 0.62, 0.0, aw - 0.2, 0.42, 0.12, 0.14, false, Kp.White, lod + 1);
            }
            Kp.Box(m, k, -half, half, g1 - 0.2, g1 + 0.05, 0, 0.16, Kp.White, BoxFaces.Wall | BoxFaces.Bottom);
            Kp.Box(m, k, -half + 0.1, half - 0.1, g0, g0 + 0.95, 0.04, 0.1, Kp.GalleryWood, BoxFaces.Front | BoxFaces.Top);
            Kp.Band(m, k, -half - 0.05, half + 0.05, NorthEave - 0.45, NorthEave - 0.25, 0.14, Kp.WhiteTrim);
            if (lod <= 1) Kp.Struts(m, KpFacade.RoofFrame(ref f, a0, a1, b0, b1), roof, NorthF2 + 0.4, 1.4, false, 211u, lod);
            // The small red-valanced turret at the west end of the roof.
            Turret(ref f, m, -1.4, -6.0, 1.3, NorthEave + 0.4, true, lod);
        }

        /// <summary>
        /// A stretch of the east range (§3.7) from b0 to b1: brick, its ground floor an open veranda of red-painted posts
        /// under the first floor, a row of small carved windows above and a broad tile roof.
        /// </summary>
        private static void EastRange(ref KpFrame f, MeshData m, double b1, double b0)
        {
            int lod = f.Lod;
            const double a0 = NA1, a1 = 25.0, ver = 1.7, f1 = 3.2;
            Affine3 g = f.Grid;
            Kp.Box(m, g, a0 + ver, a1, -0.2, f1, b0, b1, Kp.Brick, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Kp.Box(m, g, a0, a1, f1, EastEave + 0.2, b0, b1, Kp.Brick, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right | BoxFaces.Bottom);
            RoofSpec roof = KpFacade.Roof(m, ref f, a0, a1, b0, b1, EastEave, EastRidge, 1.4, Kp.Tile, false, lod);
            double half;
            Affine3 k = f.GridSide(a0, a1, b0, b1, 0, 3, out half);
            // The veranda: a raised brick floor, the shaded back wall with doors, the red posts.
            Kp.Box(m, k, -half, half, 0, 0.35, -ver, 0, Kp.Step, BoxFaces.Front | BoxFaces.Top);
            Kp.Panel(m, k, -half, 0.35, half, f1, -ver + 0.01, Kp.Ao(Kp.Brick, 0.6f));
            int posts = Math.Max(2, (int)Math.Round(2 * half / 2.3));
            for (int i = 0; i <= posts; i++)
            {
                double u = -half + 0.2 + (2 * half - 0.4) * i / posts;
                if (lod >= 2) Kp.Box(m, k, u - 0.11, u + 0.11, 0.35, f1, -0.32, -0.1, Kp.RedPost, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                else SacredParts.Post(m, SacredDraw.At(k, 0, 0, -0.2), u, 0, 0.35, f1 - 0.05, 0.2, Kp.RedPost, lod);
                if (i < posts && lod == 0)
                {
                    double uc = u + 0.5 * (2 * half - 0.4) / posts;
                    Kp.Doorway(m, SacredDraw.At(k, 0, 0, -ver), uc, 0.35, 1.0, 2.1, Kp.Wood, false, 1);
                }
            }
            Kp.Box(m, k, -half, half, f1 - 0.3, f1 + 0.05, -0.4, 0.05, Kp.RedPost, BoxFaces.Front | BoxFaces.Bottom);
            // The first floor's row of small carved windows.
            if (lod <= 1)
            {
                int n = Math.Max(2, (int)Math.Round(2 * half / 1.6));
                for (int i = 0; i < n; i++) Kp.Lattice(m, k, -half + 2 * half * (i + 0.5) / n, f1 + 1.5, 0.7, 1.0, lod, 2);
                Kp.Cornice(m, k, -half, half, EastEave - 0.5, 0.36, Kp.Wood, lod);
                Kp.Struts(m, KpFacade.RoofFrame(ref f, a0, a1, b0, b1), roof, f1 + 0.6, 1.6, false, 223u + (uint)((int)Math.Round(-b0) & 0xFFFF), lod);
            }
            if (f.C != null)
            {
                f.GridBox(a0 + ver, a1, b0, b1, 0, EastRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(a0, a0 + ver, b0, b1, 0, 0.35, GenColliderFlags.Walkable, GenColliders.Stone);
                f.GridBox(a0, a1, b0, b1, f1, EastRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }

        /// <summary>The coronation platform: a low stone block with a moulded edge and potted plants along it, walkable.</summary>
        private static void Platform(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            double cx, cy;
            KpFrame.FromGrid(0.5 * (PA0 + PA1), 0.5 * (PB0 + PB1), out cx, out cy);
            Affine3 k = f.Plan(cx, cy, 0, 102);
            double hu = 0.5 * (PA1 - PA0), hw = 0.5 * (PB1 - PB0);
            KpStack.Stage(m, k, 0, 0, hu, hw, 0, PlatformH, Kp.StoneGrey, Kp.Flags, lod);
            if (lod == 0)
                for (int i = 0; i < 6; i++)
                {
                    double t = (i + 0.5) / 6.0;
                    Kp.PottedPlant(m, k, -hu + 0.35, PlatformH, -hw + 2 * hw * t, 1.0, i % 2 == 0, lod);
                    Kp.PottedPlant(m, k, hu - 0.35, PlatformH, -hw + 2 * hw * t, 1.0, i % 2 == 1, lod);
                }
            f.Box(cx, cy, hu, hw, 102, 0, PlatformH, GenColliderFlags.Walkable, GenColliders.Stone);
        }

        /// <summary>The small whitewashed shrine before the east range: a square cell with an arched door to the west and a
        /// bell-shaped top with a gilt finial.</summary>
        private static void WhiteShrine(ref KpFrame f, MeshData m, double a, double b)
        {
            int lod = f.Lod;
            double cx, cy;
            KpFrame.FromGrid(a, b, out cx, out cy);
            Affine3 k = f.Plan(cx, cy, 0, 102);
            KpStack.Stage(m, k, 0, 0, 1.45, 1.45, 0, 0.3, Kp.White, Kp.White, lod);
            Kp.Box(m, k, -1.2, 1.2, 0.3, 2.5, -1.2, 1.2, Kp.WhiteTrim, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Kp.Box(m, k, -1.32, 1.32, 2.5, 2.72, -1.32, 1.32, Kp.WhiteTrim, BoxFaces.All & ~BoxFaces.Bottom);
            var p = new Profile2(12);
            p.Add(0, 0, true).Add(1.2, 0, true).Add(1.12, 0.25).Add(0.95, 0.6).Add(0.62, 0.95).Add(0.36, 1.2).Add(0.2, 1.35).Add(0, 1.4, true);
            Shapes.Lathe(m, SacredDraw.At(k, 0, 2.72, 0), Kp.White, p.Smooth(lod == 0 ? 2 : 0), lod == 0 ? 16 : 8, f.SL);
            Kp.Finial(m, k, 0, 4.1, 0, 0.5, 0.1, lod);
            // The arched door to the court (west, −u).
            Affine3 d = KpStack.Face(k, 180, 1.2);
            Kp.Panel(m, d, -0.42, 0.3, 0.42, 1.75, 0.01, Kp.Recess);
            if (lod <= 1) ShikharaGenerator.Arch(m, d, 0, 1.75, 0.0, 0.84, 0.35, 0.09, 0.06, false, Kp.WhiteRelief, lod);
            f.Box(cx, cy, 1.45, 1.45, 102, 0, 4.2, GenColliderFlags.NoClimb, GenColliders.Stone);
        }

        // =============================================================================================================
        // Mul Chowk
        // =============================================================================================================

        /// <summary>Mul court (palace grid) and the ranges' eave and ridge (two storeys, lower than the neighbours).</summary>
        public const double UA0 = 33.0, UA1 = 46.0, UB0 = -25.5, UB1 = -11.0, MulEave = 7.5, MulRidge = 9.9;

        /// <summary>
        /// Mul Chowk (§3.9): two-storey brick ranges round a closed court with a ground-floor colonnade, the gilt two-tier
        /// roof of Taleju's shrine on the south range, the ceremonial post, and the tall range on the east lane.
        /// </summary>
        public static void Mul(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            ShapeBrush brick = Kp.Brick, tile = Kp.Tile;
            if (lod >= 3)
            {
                Kp.Box(m, f.Grid, 24.5, 50.9, 0, MulEave, -31.9, 1.1, brick, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, f.Grid, 37.7, -15.4, 13.6, 17.0, MulEave, MulRidge, tile);
                Kp.Box(m, f.Grid, 51.2, 57.0, 0, 11.5, -28.7, -20.3, brick, BoxFaces.All & ~BoxFaces.Bottom);
                return;
            }
            KpFacade.Range(m, ref f, 24.5, 50.9, -31.9, UB0, MulEave, MulRidge, 1.0, brick, tile, false, lod);
            KpFacade.Range(m, ref f, 24.5, 50.9, UB1, 1.1, MulEave, MulRidge + 0.6, 1.0, brick, tile, false, lod);
            KpFacade.Range(m, ref f, 24.5, UA0, UB0, UB1, MulEave, MulRidge, 1.0, brick, tile, false, lod);
            KpFacade.Range(m, ref f, UA1, 50.9, UB0, UB1, MulEave, MulRidge, 1.0, brick, tile, false, lod);
            KpFacade.Court(m, ref f, UA0, UA1, UB0, UB1, Kp.Flags);
            if (lod <= 1)
                for (int s = 0; s < 4; s++)
                {
                    double half;
                    Affine3 k = f.GridSide(UA0, UA1, UB0, UB1, 0, s, out half);
                    KpFacade.Newar(m, Flip(k), half, 0, Floors2, 1, MulEave + 0.3, 2.4, -1, -1, true, false, lod + 1);
                }
            // Taleju's shrine on the south range: a gilt torana door and its small two-tier gilt roof over the ranges.
            double sx, sy;
            KpFrame.FromGrid(39.5, UB0 - 1.8, out sx, out sy);
            Affine3 sk = f.Plan(sx, sy, 0, 102);
            var t = new KpTier[2];
            t[0] = new KpTier { Base = MulEave - 0.5, WallU = 1.6, WallW = 1.6, Eave = MulRidge - 0.6, EaveU = 2.5, EaveW = 2.5, Top = MulRidge + 0.5, Walls = KpWalls.GiltPanels, Windows = 1 };
            t[1] = new KpTier { Base = MulRidge + 0.5, WallU = 1.2, WallW = 1.2, Eave = 10.9, EaveU = 1.9, EaveW = 1.9, Top = 11.7, ApexU = 0.15, ApexW = 0.15, Walls = KpWalls.GiltPanels, Windows = 1 };
            KpRoofLook gl = KpRoofLook.CopperBells(Kp.Gilt);
            gl.Fascia = Kp.GiltDeep;
            KpStack.Build(m, sk, t, 2, Kp.Brick, gl, 307u, lod == 0 ? 1 : 2);
            Kp.Finial(m, sk, 0, 11.65, 0, 0.55, 0.14, lod);
            if (lod <= 1)
            {
                double half;
                Affine3 door = f.GridSide(UA0, UA1, UB0, UB1, 0, 2, out half);
                door = Flip(door);
                Kp.Doorway(m, door, -(39.5 - 0.5 * (UA0 + UA1)), 0.2, 1.1, 2.2, Kp.Gilt, true, lod);
                // The ceremonial post near the court's centre.
                double qx, qy;
                KpFrame.FromGrid(40.0, -19.5, out qx, out qy);
                Affine3 q = f.Plan(qx, qy, 0, 102);
                Shapes.Cylinder(m, q, Kp.Wood, 0.14, 5.5, 8, 0, 0, false, true, f.SL);
                Shapes.Cylinder(m, q, Kp.StoneGrey, 0.4, 0.3, 8, 0, 0, false, true, f.SL);
            }
            // The tall range on the east lane (w1414849434) and Mul's outer east face.
            KpFacade.Range(m, ref f, 51.2, 57.0, -28.7, -20.3, 11.5, 14.6, 1.0, brick, tile, false, lod);
            if (lod <= 1)
            {
                double half;
                Affine3 e = f.GridSide(51.2, 57.0, -28.7, -20.3, 0, 1, out half);
                KpFacade.Newar(m, e, half, 0, Floors3, 2, 11.8, 2.2, 1, -1, false, true, lod);
                Affine3 e2 = f.GridSide(24.5, 50.9, -20.3, 1.1, 0, 1, out half);
                KpFacade.Newar(m, e2, half, 0, Floors2, 1, MulEave + 0.3, 2.4, -1, -1, false, false, lod + 1);
            }
            if (f.C != null)
            {
                f.GridBox(24.5, 50.9, -31.9, 1.1, 0, MulRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(51.2, 57.0, -28.7, -20.3, 0, 14.6, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }

        // =============================================================================================================
        // Lohan Chowk
        // =============================================================================================================

        /// <summary>Lohan court (palace grid; §3.12: 11 × 17 m) and the line of the south facade on Basantapur.</summary>
        public const double HA0 = 35.0, HA1 = 46.0, HB0 = -58.5, HB1 = -41.5, SouthB = -70.5, LohanRidge = 14.4;

        /// <summary>
        /// Lohan Chowk (§3.12, §3.16): four-storey brick ranges round the court with colonnades, a three-bay window centred
        /// on each and a carved lattice band on the third floor; the brick plinth with steps round the court and the stone
        /// slab in the middle; the outer south facade on Basantapur with its black railing on a low plinth and two grey
        /// lions on tall pedestals; the two-storey east shop range on the street with carved posts and potted plants.
        /// </summary>
        public static void Lohan(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            double eave = KpTowers.RangeEave;
            ShapeBrush brick = Kp.Brick, tile = Kp.Tile;
            if (lod >= 3)
            {
                Kp.Box(m, f.Grid, 22.2, 46.0, 0, eave, SouthB, HB0, brick, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, f.Grid, 34.1, -64.5, 12.9, 7.0, eave, LohanRidge, tile);
                Kp.Box(m, f.Grid, 25.0, 55.0, 0, eave, HB0, -33.6, brick, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, f.Grid, 40.0, -46.0, 16.0, 13.4, eave, LohanRidge, tile);
                Kp.Box(m, f.Grid, 57.0, 80.7, 0, 8.0, -66.6, -56.0, brick, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, f.Grid, 68.85, -61.3, 13.4, 6.8, 8.0, 10.6, tile);
                return;
            }
            // The ranges: south (to the street), west, north, east.
            RoofSpec south = KpFacade.Range(m, ref f, 22.2, 46.0, SouthB, HB0, eave, LohanRidge, 1.6, brick, tile, false, lod);
            KpFacade.Range(m, ref f, 25.0, HA0, HB0, -42.6, eave, LohanRidge, 1.4, brick, tile, false, lod);
            KpFacade.Range(m, ref f, 29.5, 44.5, HB1, -33.6, eave, LohanRidge, 1.4, brick, tile, false, lod);
            KpFacade.Range(m, ref f, HA1, 55.0, -58.6, -42.6, eave, LohanRidge, 1.4, brick, tile, false, lod);
            KpFacade.Court(m, ref f, HA0, HA1, HB0, HB1, Kp.Flags);
            if (lod <= 1)
            {
                for (int s = 0; s < 4; s++)
                {
                    double half;
                    Affine3 k = f.GridSide(HA0, HA1, HB0, HB1, 0, s, out half);
                    // The court faces one level coarser than the street front (seen from inside the court or above).
                    KpFacade.Newar(m, Flip(k), half, 0.6, Floors4, 3, eave + 0.35, 2.0, 1, 2, true, false, lod + 1);
                }
            }
            // The brick plinth round the court's edges with steps down to the floor, and the stone slab in the middle.
            CourtPlinth(ref f, m, HA0, HA1, HB0, HB1, 1.3, 0.6);
            double cx, cy;
            KpFrame.FromGrid(0.5 * (HA0 + HA1), 0.5 * (HB0 + HB1), out cx, out cy);
            Kp.Box(m, f.Plan(cx, cy, 0, 102), -1.1, 1.1, -0.1, 0.2, -0.7, 0.7, Kp.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            // The south facade on Basantapur.
            {
                double half;
                Affine3 k = f.GridSide(22.2, 46.0, SouthB, HB0, 0, 2, out half);
                KpFacade.Newar(m, k, half, 0.35, Floors4, 3, eave + 0.35, 2.1, 1, -1, false, false, lod);
                Kp.Box(m, k, -half, half, 0, 0.35, 0, 1.6, Kp.Step, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
                Kp.Railing(m, k, -half + 0.3, half - 0.3, 1.45, 0.35, 0.95, lod);
                if (lod <= 1) Kp.Struts(m, KpFacade.RoofFrame(ref f, 22.2, 46.0, SouthB, HB0), south, 8.5, 1.5, false, 401u, lod);
            }
            // The two grey stone lions on tall pedestals: at the nine-storey tower's south-west corner and further east.
            LionAt(ref f, m, KpTowers.BA0 + 0.9, KpTowers.BB0 - 1.4, lod);
            LionAt(ref f, m, 33.5, SouthB - 2.0, lod);
            ShopRange(ref f, m);
            if (f.C != null)
            {
                f.GridBox(22.2, 46.0, SouthB, HB0, 0, LohanRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(25.0, HA0 - 1.3, HB0, -42.6, 0, LohanRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(29.5, 44.5, HB1 + 1.3, -33.6, 0, LohanRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                f.GridBox(HA1 + 1.3, 55.0, -58.6, -42.6, 0, LohanRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
                // The railing (camera passes over it).
                f.GridWall(22.5, SouthB - 1.45, 45.7, SouthB - 1.45, 0, 1.3, 0.04);
                f.GridBox(22.2, 46.0, SouthB - 1.6, SouthB, 0, 0.35, GenColliderFlags.Walkable, GenColliders.Stone);
            }
        }

        /// <summary>A walkable brick plinth (width <paramref name="w"/>, height <paramref name="h"/>) round the inside of a court
        /// with steps down at the middle of each side.</summary>
        private static void CourtPlinth(ref KpFrame f, MeshData m, double a0, double a1, double b0, double b1, double w, double h)
        {
            int lod = f.Lod;
            Affine3 g = f.Grid;
            BoxFaces faces = BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top;
            Kp.Box(m, g, a0, a1, -0.1, h, b0, b0 + w, Kp.BrickNew, faces);
            Kp.Box(m, g, a0, a1, -0.1, h, b1 - w, b1, Kp.BrickNew, faces);
            Kp.Box(m, g, a0, a0 + w, -0.1, h, b0 + w, b1 - w, Kp.BrickNew, faces);
            Kp.Box(m, g, a1 - w, a1, -0.1, h, b0 + w, b1 - w, Kp.BrickNew, faces);
            if (lod <= 1)
            {
                double am = 0.5 * (a0 + a1), bm = 0.5 * (b0 + b1);
                Kp.Box(m, g, am - 0.9, am + 0.9, -0.1, 0.5 * h, b0 + w, b0 + w + 0.4, Kp.Step, faces);
                Kp.Box(m, g, am - 0.9, am + 0.9, -0.1, 0.5 * h, b1 - w - 0.4, b1 - w, Kp.Step, faces);
                Kp.Box(m, g, a0 + w, a0 + w + 0.4, -0.1, 0.5 * h, bm - 0.9, bm + 0.9, Kp.Step, faces);
                Kp.Box(m, g, a1 - w - 0.4, a1 - w, -0.1, 0.5 * h, bm - 0.9, bm + 0.9, Kp.Step, faces);
            }
            if (f.C == null) return;
            f.GridBox(a0, a1, b0, b0 + w, 0, h, GenColliderFlags.Walkable, GenColliders.Brick);
            f.GridBox(a0, a1, b1 - w, b1, 0, h, GenColliderFlags.Walkable, GenColliders.Brick);
            f.GridBox(a0, a0 + w, b0 + w, b1 - w, 0, h, GenColliderFlags.Walkable, GenColliders.Brick);
            f.GridBox(a1 - w, a1, b0 + w, b1 - w, 0, h, GenColliderFlags.Walkable, GenColliders.Brick);
        }

        /// <summary>A grey stone lion on a tall pedestal at palace-grid (a, b), facing Basantapur (192°).</summary>
        private static void LionAt(ref KpFrame f, MeshData m, double a, double b, int lod)
        {
            Kp.Lion(m, f.GridFace(a, b, 0, 192), 0, 0, 0, 1.55, 1.25, false, 1, lod);
            if (f.C == null) return;
            double x, y;
            KpFrame.FromGrid(a, b, out x, out y);
            f.Cylinder(x, y, 0, 2.9, 0.6);
        }

        /// <summary>
        /// The east shop range (§3.16; OSM w906985353): two storeys under a deep hipped tile roof on struts, the ground
        /// floor a row of shopfronts between carved posts with potted plants, facing the street.
        /// </summary>
        private static void ShopRange(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            const double a0 = 57.0, a1 = 80.7, b0 = -66.6, b1 = -56.0, eave = 8.0, ridge = 10.8, f1 = 3.4;
            RoofSpec roof = KpFacade.Range(m, ref f, a0, a1, b0, b1, eave, ridge, 1.9, Kp.Brick, Kp.Tile, false, lod);
            double half;
            Affine3 k = f.GridSide(a0, a1, b0, b1, 0, 2, out half);
            int shops = 8;
            double bay = 2 * half / shops;
            for (int i = 0; i < shops; i++)
            {
                double u = -half + bay * (i + 0.5);
                // Shop opening: a dark interior, a bright stall front and an awning edge.
                Kp.Panel(m, k, u - 0.5 * bay + 0.2, 0.15, u + 0.5 * bay - 0.2, f1 - 0.35, 0.01, Kp.Ao(Kp.Recess, 0.9f));
                if (lod <= 1)
                {
                    Kp.Box(m, k, u - 0.5 * bay + 0.25, u + 0.5 * bay - 0.25, 0, 0.75, 0.0, 0.5, i % 2 == 0 ? Kp.Cloak : Kp.Cream, BoxFaces.Front | BoxFaces.Top);
                    if (lod == 0) Kp.PottedPlant(m, k, u - 0.5 * bay + 0.25, 0, 0.8, 1.0, i % 2 == 0, lod);
                }
            }
            for (int i = 0; i <= shops; i++)
            {
                double u = -half + bay * i;
                if (lod >= 2) Kp.Box(m, k, u - 0.12, u + 0.12, 0, f1, 0, 0.2, Kp.Wood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                else SacredParts.Post(m, SacredDraw.At(k, 0, 0, 0.12), u, 0, 0, f1 - 0.1, 0.22, Kp.Wood, lod);
            }
            Kp.Band(m, k, -half, half, f1 - 0.25, f1 + 0.1, 0.18, Kp.WoodBeam);
            if (lod <= 1)
            {
                int n = 10;
                for (int i = 0; i < n; i++) Kp.Lattice(m, k, -half + 2 * half * (i + 0.5) / n, f1 + 1.9, 0.85, 1.25, lod, 2);
                Kp.Cornice(m, k, -half, half, eave - 0.5, 0.4, Kp.Wood, lod);
                Kp.Struts(m, KpFacade.RoofFrame(ref f, a0, a1, b0, b1), roof, f1 + 1.0, 1.5, false, 431u, lod);
            }
            if (f.C != null) f.GridBox(a0, a1, b0, b1, 0, ridge, GenColliderFlags.NoClimb, GenColliders.Brick);
        }
    }
}
