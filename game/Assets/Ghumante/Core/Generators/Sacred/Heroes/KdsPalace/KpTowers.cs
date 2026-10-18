using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// The four corner towers of the Lohan Chowk complex (kds-palace.md §3.11, §3.13-§3.15), each a stack of measured
    /// storeys and roofs in its own plan frame on the palace grid: the nine-storey Basantapur tower (SW, rebuilt after
    /// 2015: four broad tile roofs, the top one a short ridge with three gilt finials, 30 m), the Kirtipur tower (NW, its
    /// grey-green copper "Bangla" hood, 23 m), the octagonal Bhaktapur tower (NE, 19.5 m) and the Lalitpur tower (SE,
    /// two skirt roofs and a broad pyramid over a lattice band, 21 m, with its grey stone lion on the street). Their
    /// lowest roofs hang at the ranges' eave (<see cref="RangeEave"/>) so they run on into the court roofs.
    /// </summary>
    internal static class KpTowers
    {
        /// <summary>Roof tiers of the Basantapur tower.</summary>
        public const int BasantapurRoofs = 4;

        /// <summary>The eave of the Lohan Chowk ranges, shared by the towers' lowest roofs.</summary>
        public const double RangeEave = 10.2;

        /// <summary>The Basantapur tower's base (palace grid; OSM w906985354, 13.8 × 13.4 m).</summary>
        public const double BA0 = 8.6, BA1 = 22.2, BB0 = -71.7, BB1 = -58.2;

        /// <summary>The Kirtipur tower's centre and half size.</summary>
        public const double KA = 25.0, KB = -38.1, KHalf = 4.5;

        /// <summary>The Bhaktapur tower's centre and its square base's half size.</summary>
        public const double HA = 49.0, HB = -38.1, HHalf = 4.5;

        /// <summary>The Lalitpur tower's base.</summary>
        public const double LA0 = 46.0, LA1 = 57.0, LB0 = -69.6, LB1 = -58.6;

        private static readonly double[] LowFloors = { 3.0, 6.0 };

        // =============================================================================================================
        // Basantapur (Nautale Durbar)
        // =============================================================================================================

        /// <summary>
        /// The nine-storey tower (§3.11): a brick base of three storeys with the carved three-bay window on its south and
        /// north faces, roof 1 at the ranges' eave; storey 4 a continuous carved lattice band, roof 2 at 15 m; storeys 5-6
        /// brick with a carved balcony window centred on each face, roof 3 at 20 m; the lattice belvedere and roof 4 at
        /// 25 m, a hipped roof whose short ridge carries three gilt finials to 30 m.
        /// </summary>
        public static void Basantapur(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            double am = 0.5 * (BA0 + BA1), bm = 0.5 * (BB0 + BB1), hu = 0.5 * (BA1 - BA0), hw = 0.5 * (BB1 - BB0);
            Affine3 k = PlanAt(ref f, am, bm);
            var t = new KpTier[4];
            // Measured on the photographs (c_10 from the south-west before 2015, c_00 of the rebuilt south face): the upper
            // storeys stay nearly as wide as the base (13.5-14.6 m) and every roof overhangs it by 2.5-3 m, eaves at about
            // 11, 16.5, 20.5 and 25 m, the top roof a short ridge with the three gilt finials to 30 m.
            t[0] = new KpTier { Base = 0, WallU = hu, WallW = hw, Eave = 11.0, EaveU = hu + 2.5, EaveW = hw + 2.5, Top = 13.6, Walls = KpWalls.Plain };
            t[1] = new KpTier { Base = 13.6, WallU = 6.5, WallW = 6.4, Eave = 16.4, EaveU = 9.4, EaveW = 9.3, Top = 18.6, Walls = KpWalls.Lattice };
            t[2] = new KpTier { Base = 18.6, WallU = 6.3, WallW = 6.2, Eave = 20.6, EaveU = 8.9, EaveW = 8.8, Top = 22.4, Walls = KpWalls.Brick, Windows = 3, WinSill = 0.3, WinH = 1.2 };
            t[3] = new KpTier
            {
                Base = 22.4, WallU = 6.0, WallW = 5.9, Eave = 25.0, EaveU = 7.9, EaveW = 7.8, Top = 28.6, ApexU = 2.2, ApexW = 0.25, Walls = KpWalls.Lattice,
            };
            if (lod >= 3)
            {
                Lod3(m, k, t, 4, Kp.BrickRebuilt, Kp.TowerTile);
                return;
            }
            KpRoofLook look = KpRoofLook.TilePlain(Kp.TowerTile);
            look.StrutPitch = 1.15;
            KpStack.Build(m, k, t, 4, Kp.BrickRebuilt, look, 91u, lod);
            // The three gilt finials on the top ridge.
            for (int i = -1; i <= 1; i++) Kp.Finial(m, k, 1.7 * i, 28.55, 0, i == 0 ? 1.45 : 1.15, i == 0 ? 0.24 : 0.2, lod);
            // The base storeys: the faces that show (S onto Basantapur, N onto Nasal Chowk, E onto Lohan).
            double half;
            Affine3 s = f.GridSide(BA0, BA1, BB0, BB1, 0, 2, out half);
            BaseFace(m, s, half, false, true, lod);
            Affine3 n = f.GridSide(BA0, BA1, BB0, BB1, 0, 0, out half);
            BaseFace(m, n, half, true, true, lod);
            if (lod <= 1)
            {
                Affine3 e = f.GridSide(BA0, BA1, BB0, BB1, 0, 1, out half);
                BaseFace(m, e, half, false, false, Math.Max(1, lod));
            }
            // Storeys 5-6: a carved balcony window at the centre of each face.
            if (lod <= 1)
                for (int q = 0; q < 4; q++)
                    Kp.Bay(m, KpStack.Face(k, 90 * q, 6.25), 0, 18.9, 2.6, 1.5, false, lod);
            // On Basantapur: the stone flags, the curb and the black iron railing in front of the base (basantapur ref_08,
            // v5 c_02), and the short railings that join it to the white wing's railing west and to the Lohan front's east.
            {
                double bx, by;
                KpFrame.FromGrid(am, BB0, out bx, out by);
                Affine3 sk = f.Plan(bx, by, 0, 102);
                KpRanges.Frontage(ref f, m, sk, -hu, hu, lod);
                Joint(ref f, m, BA0, -74.45, BB0 - KpRanges.RailOut, lod);
                Joint(ref f, m, BA1, BB0 - KpRanges.RailOut, KpCourts.SouthB - 1.45, lod);
            }
            Colliders(ref f, BA0, BA1, BB0, BB1, 28.6);
        }

        /// <summary>
        /// One face of the Basantapur tower's brick base as rebuilt (photos basantapur_tower c_00, c_02, c_10): a plain brick
        /// ground storey with thin brick courses and one small window (a doorway on the Nasal Chowk side); a timber band at
        /// 4 m; the second storey's three carved windows with long-eared frames (the middle one larger, under a crest); a
        /// carved cornice; the third storey's projecting three-bay window (sah jhya) between two small windows; and under
        /// roof 1 the continuous carved lattice gallery projecting on a row of carved figure struts.
        /// </summary>
        private static void BaseFace(MeshData m, in Affine3 k, double half, bool door, bool bay, int lod)
        {
            ShapeBrush course = Kp.Tint(Kp.BrickRebuilt, 0.84f);
            Kp.Band(m, k, -half, half, 0, 0.4, 0.07, Kp.Step);
            if (lod <= 1)
                for (int i = 0; i < 2; i++)
                    Kp.Band(m, k, -half, half, 1.45 + 1.25 * i, 1.53 + 1.25 * i, 0.03, course);
            if (door)
            {
                // The Nasal Chowk side (nasal_chowk c_08): a carved door over the plinth along its foot and two small
                // carved windows.
                double top = Kp.Doorway(m, k, 0, 0.6, 1.15, 2.25, Kp.Wood, false, lod);
                if (lod <= 1) Kp.Crest(m, k, 0, top + 0.03, 0.04, 2.0, 1.05, Kp.Wood, lod);
                for (int e = -1; e <= 1; e += 2) Kp.FramedWindow(m, k, e * 3.6, 1.6, 0.8, 0.95, false, lod);
            }
            else Kp.FramedWindow(m, k, 0, 1.7, 0.85, 1.0, false, lod);
            // Timber band at the first floor.
            Kp.Band(m, k, -half, half, 3.95, 4.2, 0.1, Kp.Wood);
            if (lod == 0) Kp.Band(m, k, -half, half, 4.2, 4.28, 0.05, Kp.WoodMid);
            // Second storey: three carved windows.
            double side = Math.Min(4.4, 0.64 * half);
            Kp.FramedWindow(m, k, -side, 4.95, 1.0, 1.2, false, lod);
            Kp.FramedWindow(m, k, side, 4.95, 1.0, 1.2, false, lod);
            Kp.FramedWindow(m, k, 0, 4.85, 1.45, 1.35, true, lod);
            // Carved cornice, then the third storey: the projecting bay between two small windows.
            Kp.Cornice(m, k, -half, half, 6.95, 0.36, Kp.Wood, lod);
            if (bay) Kp.Bay(m, k, 0, 7.45, Math.Min(4.6, 0.7 * half), 1.75, false, lod);
            else Kp.FramedWindow(m, k, 0, 7.75, 1.2, 1.15, false, lod);
            Kp.FramedWindow(m, k, -side - 0.4, 7.8, 0.8, 1.0, false, lod);
            Kp.FramedWindow(m, k, side + 0.4, 7.8, 0.8, 1.0, false, lod);
            // The lattice gallery under roof 1 on its figure struts.
            const double G0 = 9.45, G1 = 10.9, Gd = 0.85;
            if (lod >= 2)
            {
                Kp.Box(m, k, -half, half, G0, G1, 0, Gd, Kp.Wood, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                Kp.Panel(m, k, -half + 0.2, G0 + 0.3, half - 0.2, G1 - 0.25, Gd + 0.01, Kp.Recess);
                return;
            }
            Kp.Box(m, k, -half - 0.1, half + 0.1, G0 - 0.18, G0, 0, Gd + 0.08, Kp.WoodBeam, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
            Kp.Box(m, k, -half - 0.1, half + 0.1, G1 - 0.14, G1, 0, Gd + 0.06, Kp.WoodBeam, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
            Kp.Box(m, k, -half, half, G0, G1 - 0.14, 0, Gd, Kp.Wood, BoxFaces.Left | BoxFaces.Right);
            Affine3 g = SacredDraw.At(k, 0, 0, Gd);
            int np = Math.Max(4, (int)Math.Round(2 * half / 1.05));
            for (int i = 0; i < np; i++)
            {
                double u = -half + 2 * half * (i + 0.5) / np;
                Kp.Lattice(m, g, u, 0.5 * (G0 + G1 - 0.14), 2 * half / np - 0.2, G1 - G0 - 0.42, lod, lod == 0 ? 3 : 0);
                if (lod == 0) Kp.Box(m, g, u - 0.5 * (2 * half / np) - 0.05, u - 0.5 * (2 * half / np) + 0.05, G0, G1 - 0.14, 0, 0.06, Kp.Wood, BoxFaces.Front);
            }
            int ns = Math.Max(4, (int)Math.Round(2 * half / 1.75));
            for (int i = 0; i <= ns; i++)
            {
                double u = -half + 0.25 + (2 * half - 0.5) * i / ns;
                if (bay && Math.Abs(u) < 0.5 * Math.Min(4.6, 0.7 * half) + 0.15) continue;
                Kp.FigureStrut(m, k, u, 7.45, 0.03, G0 - 0.22, Gd - 0.12, lod);
            }
        }

        /// <summary>A short railing on its curb along palace-grid a = <paramref name="a"/> from b0 to b1 (joining two
        /// frontage railings that stand at different distances from their walls), with its collider.</summary>
        private static void Joint(ref KpFrame f, MeshData m, double a, double b0, double b1, int lod)
        {
            if (Math.Abs(b1 - b0) < 0.2) return;
            double bm = 0.5 * (b0 + b1), hl = 0.5 * Math.Abs(b1 - b0);
            Affine3 face = f.GridFace(a, bm, 0, 102);
            Kp.Box(m, face, -hl - 0.16, hl + 0.16, -0.1, 0.2, -0.16, 0.16, Kp.Step, BoxFaces.All & ~BoxFaces.Bottom);
            Kp.Railing(m, face, -hl, hl, 0, 0.2, KpRanges.RailH, lod);
            f.GridWall(a, b0, a, b1, 0, KpRanges.RailH + 0.2, 0.06);
        }

        /// <summary>A red-framed round-headed opening (the small arcade under the Kirtipur tower on Nasal Chowk): the dark passage,
        /// red-painted jambs and archivolt.</summary>
        internal static void RedArch(MeshData m, in Affine3 k, double u, double v0, double ww, double h, int lod)
        {
            double r = 0.5 * ww, rect = h - r;
            Kp.Panel(m, k, u - r, v0, u + r, v0 + rect, 0.01, Kp.Recess);
            Kp.HalfDisc(m, k, u, v0 + rect, r, r, 0.01, Kp.Recess, lod == 0 ? 8 : 5);
            if (lod >= 2) return;
            Kp.Box(m, k, u - r - 0.16, u - r, v0, v0 + rect, 0, 0.12, Kp.Dado, BoxFaces.Wall);
            Kp.Box(m, k, u + r, u + r + 0.16, v0, v0 + rect, 0, 0.12, Kp.Dado, BoxFaces.Wall);
            ShikharaGenerator.Arch(m, k, u, v0 + rect, 0.012, ww, r, 0.16, 0.12, false, Kp.Dado, lod);
        }

        // =============================================================================================================
        // Kirtipur
        // =============================================================================================================

        /// <summary>
        /// The Kirtipur tower (§3.13): a square brick tower whose lowest roof runs on into the ranges, two brick storeys with
        /// windows, a broad hipped roof, a storey of carved lattice and the curved copper hood (grey-green, ribbed, its
        /// eaves curving down and flaring out at the corners), top 23 m.
        /// </summary>
        public static void Kirtipur(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            Affine3 k = PlanAt(ref f, KA, KB);
            var t = new KpTier[2];
            t[0] = new KpTier { Base = 0, WallU = KHalf, WallW = KHalf, Eave = RangeEave, EaveU = KHalf + 2.3, EaveW = KHalf + 2.3, Top = 11.6, Walls = KpWalls.Plain };
            t[1] = new KpTier { Base = 11.6, WallU = 3.9, WallW = 3.9, Eave = 15.4, EaveU = 6.1, EaveW = 6.1, Top = 16.7, Walls = KpWalls.Brick, Windows = 2 };
            if (lod >= 3)
            {
                Lod3(m, k, t, 2, Kp.Brick, Kp.TowerTile);
                SacredDraw.Box(m, k, -3.3, 3.3, 16.7, 19.0, -3.3, 3.3, Kp.Brick, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                KpLod3.Pyramid(m, k, 0, 0, 4.6, 4.2, 19.0, 23.0, Kp.KirtipurCopper);
                return;
            }
            KpRoofLook look = KpRoofLook.TilePlain(Kp.TowerTile);
            KpStack.Build(m, k, t, 2, Kp.Brick, look, 93u, lod);
            // The hood is big against its drum (nasal_chowk c_08, kirtipur_tower c_00-c_10): the lip about 1.4 × the drum
            // across, the rise about 0.6 × the drum's width, the lip coming down low so only a short band of the drum's
            // arched windows shows under it.
            Drum(m, k, 3.3, 16.7, 19.1, lod);
            Hood(m, k, 3.3, 1.5, 18.9, 23.0, lod);
            Kp.Finial(m, k, 0, 22.95, 0, 0.75, 0.16, lod);
            double half;
            Affine3 w = f.GridSide(KA - KHalf, KA + KHalf, KB - KHalf, KB + KHalf, 0, 3, out half);
            KpFacade.Newar(m, w, half, 0, LowFloors, 2, RangeEave + 0.35, 2.2, -1, -1, false, false, lod);
            Colliders(ref f, KA - KHalf, KA + KHalf, KB - KHalf, KB + KHalf, 23.0);
        }

        /// <summary>The drum under the Kirtipur hood (kirtipur_tower c_00-c_10): dark timber faces of cusped-arch lattice
        /// windows at about 1.2 m pitch between slim posts, a carved sill band and a beam under the hood.</summary>
        private static void Drum(MeshData m, in Affine3 k, double half, double v0, double v1, int lod)
        {
            SacredDraw.Box(m, k, -half, half, v0, v1, -half, half, Kp.Wood, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            for (int s = 0; s < 4; s++)
            {
                Affine3 face = SacredDraw.At(SacredDraw.Turn(k, 0, 0, 0, s * 0.5 * Math.PI), 0, 0, half);
                Kp.Band(m, face, -half, half, v0, v0 + 0.3, 0.1, Kp.WoodBeam);
                Kp.Band(m, face, -half, half, v1 - 0.32, v1, 0.12, Kp.WoodBeam);
                if (lod >= 2)
                {
                    Kp.Panel(m, face, -half + 0.15, v0 + 0.4, half - 0.15, v1 - 0.4, 0.02, Kp.LatticeFar);
                    continue;
                }
                int n = Math.Max(3, (int)Math.Round(2 * half / 1.2));
                double bay = 2 * half / n;
                for (int q = 0; q < n; q++)
                {
                    double u = -half + bay * (q + 0.5);
                    Kp.ArchWindow(m, face, u, v0 + 0.38, bay - 0.26, v1 - v0 - 0.85, lod, 0.72);
                    if (lod == 0 && q > 0) Kp.Box(m, face, u - 0.5 * bay - 0.06, u - 0.5 * bay + 0.06, v0 + 0.3, v1 - 0.32, 0, 0.1, Kp.WoodMid, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
            }
        }

        /// <summary>
        /// The Kirtipur hood (photos kirtipur_tower c_00-c_10, nasal_chowk c_08): a square-plan Bangla bell roof of copper
        /// sheet over the drum of half size <paramref name="wall"/>. Its profile is an S: from the short ridge (half length
        /// <paramref name="ridge"/>, at <paramref name="top"/>) it falls convex over rounded shoulders, then its lower third
        /// flares outward (concave) into a projecting lip about 1.3 m beyond the drum that curls up, a little at mid-side and
        /// into horns at the four sharp corners. Ribs run down the four hips only, standing seams between them; a dark soffit
        /// closes it back to the drum.
        /// </summary>
        private static void Hood(MeshData m, in Affine3 k, double wall, double ridge, double eave, double top, int lod)
        {
            int nt = lod == 0 ? 48 : lod == 1 ? 32 : 16;
            int np = HoodRows.Length / 2;
            double rise = top - eave, lip = wall + 1.3;
            ShapeBrush cu = Kp.KirtipurCopper;
            int first = m.VertexCount;
            for (int r = 0; r < np; r++)
            {
                if (lod >= 2 && r % 2 == 1 && r != np - 1) continue;
                for (int c = 0; c < nt; c++)
                {
                    double u, v, w, nu, nv, nw;
                    HoodPoint(lip, ridge, eave, rise, r, 2 * Math.PI * c / nt, out u, out v, out w, out nu, out nv, out nw);
                    SacredDraw.V(m, k, u, v, w, nu, nv, nw, Kp.Tint(cu, (float)(0.86 + 0.14 * HoodRows[2 * r + 1])));
                }
            }
            int rows = lod >= 2 ? (np + 1) / 2 + (np % 2 == 0 ? 1 : 0) : np;
            SacredDraw.Grid(m, first, rows, nt, true, null);
            // The soffit from the lip's underside back to the drum.
            first = m.VertexCount;
            for (int c = 0; c < nt; c++)
            {
                double th = 2 * Math.PI * c / nt, u, v, w, nu, nv, nw;
                HoodPoint(lip, ridge, eave, rise, 0, th, out u, out v, out w, out nu, out nv, out nw);
                SacredDraw.V(m, k, u, v - 0.1, w, 0, -1, 0, Kp.Ao(Kp.Wood, 0.5f));
                double su = Math.Cos(th), sw = Math.Sin(th), mx = Math.Max(Math.Abs(su), Math.Abs(sw));
                SacredDraw.V(m, k, wall * su / mx, eave - 0.2, wall * sw / mx, 0, -1, 0, Kp.Ao(Kp.Wood, 0.45f));
            }
            for (int c = 0; c < nt; c++)
            {
                int a = first + 2 * c, b = first + 2 * ((c + 1) % nt);
                SacredDraw.Quad(m, a, b, b + 1, a + 1);
            }
            if (lod >= 2) return;
            // Ribs on the four hips (heavy) and standing seams between them (LOD0), and the ridge roll.
            var p = new Path3(np);
            int seams = lod == 0 ? 3 : 0;
            for (int h = 0; h < 4; h++)
            {
                for (int j = 0; j <= seams; j++)
                {
                    double th = 2 * Math.PI * (h + 0.5) / 4 + (j == 0 ? 0 : 0.5 * Math.PI * j / (seams + 1));
                    p.Clear();
                    for (int r = 0; r < np; r++)
                    {
                        double u, v, w, nu, nv, nw;
                        HoodPoint(lip, ridge, eave, rise, r, th, out u, out v, out w, out nu, out nv, out nw);
                        double off = j == 0 ? 0.04 : 0.015;
                        p.Add(u + off * nu, v + off * nv, w + off * nw);
                    }
                    Shapes.Tube(m, k, j == 0 ? Kp.KirtipurRib : Kp.Tint(Kp.KirtipurCopper, 0.82f), p, j == 0 ? 0.06 : 0.022, j == 0 ? 5 : 3, false);
                }
            }
            p.Clear();
            p.Add(-ridge - 0.1, top + 0.04, 0).Add(ridge + 0.1, top + 0.04, 0);
            Shapes.Tube(m, k, Kp.KirtipurRib, p, 0.1, 6, true);
        }

        /// <summary>The hood's profile from the lip (row 0) to the ridge: the plan scale (1 = the lip) and the height as a
        /// fraction of the rise; rows 0-3 the up-curled lip and the concave flare, then the convex shoulders.</summary>
        private static readonly double[] HoodRows =
        {
            1.0, 0.12, 0.965, 0.05, 0.915, 0.035, 0.86, 0.08, 0.815, 0.2, 0.79, 0.36, 0.74, 0.54, 0.64, 0.71, 0.5, 0.84, 0.32, 0.94, 0.14, 0.99, 0.0, 1.0,
        };

        /// <summary>A point on the hood: profile row <paramref name="r"/> at plan angle <paramref name="th"/> on a
        /// square-cornered plan (superellipse of exponent 8), the ridge along u, the lip raised at the corners.</summary>
        private static void HoodPoint(double lip, double ridge, double eave, double rise, int r, double th, out double u, out double v, out double w,
                                      out double nu, out double nv, out double nw)
        {
            double ct = Math.Cos(th), st = Math.Sin(th);
            // Exponent 8: |x|^8 + |y|^8 = 1 -> x = sign(c)|c|^(1/4).
            double su = Math.Sign(ct) * Math.Pow(Math.Abs(ct), 0.25), sw = Math.Sign(st) * Math.Pow(Math.Abs(st), 0.25);
            double rho = HoodRows[2 * r], fh = HoodRows[2 * r + 1];
            double corner = Math.Pow(Math.Abs(Math.Sin(2 * th)), 6);
            // The flare: the lip (rows 0-2) curls up a little at mid-side and into horns at the corners.
            double curl = r == 0 ? 0.3 + 0.4 * corner : r == 1 ? 0.1 + 0.15 * corner : 0;
            double ru = ridge + (lip - ridge) * rho, rw = lip * rho;
            u = ru * su;
            w = rw * sw;
            v = eave + rise * fh + curl;
            int r0 = Math.Max(0, r - 1), r1 = Math.Min(HoodRows.Length / 2 - 1, r + 1);
            double dRho = HoodRows[2 * r1] - HoodRows[2 * r0], dH = HoodRows[2 * r1 + 1] - HoodRows[2 * r0 + 1];
            // Normal in the profile plane: perpendicular to (dRho * lip, dH * rise), outward and up.
            double tr = dRho * lip, tv = dH * rise, l = Math.Sqrt(tr * tr + tv * tv);
            double nr = l > 1e-9 ? -tv / l : 1, nvv = l > 1e-9 ? tr / l : 0;
            if (nvv < 0)
            {
                nr = -nr;
                nvv = -nvv;
            }
            nu = su * Math.Abs(nr) * (nr >= 0 ? 1 : -1);
            nw = sw * Math.Abs(nr) * (nr >= 0 ? 1 : -1);
            nv = nvv;
        }

        // =============================================================================================================
        // Bhaktapur (octagonal)
        // =============================================================================================================

        /// <summary>
        /// The Bhaktapur tower (§3.14): a square brick base whose roof runs on into the ranges, then an octagonal brick storey
        /// with small windows, a belvedere of eight carved lattice faces and the octagonal pyramid roof with a gilt finial
        /// at 19.5 m.
        /// </summary>
        public static void Bhaktapur(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            Affine3 k = PlanAt(ref f, HA, HB);
            var t = new KpTier[1];
            t[0] = new KpTier
            {
                Base = 0, WallU = HHalf, WallW = HHalf, Eave = RangeEave + 0.5, EaveU = HHalf + 2.2, EaveW = HHalf + 2.2, Top = 12.2, ApexU = 3.5, ApexW = 3.5,
                Walls = KpWalls.Plain,
            };
            const double ap = 3.3, ap2 = 3.0, v1 = 14.7, v2 = 16.6, eaveR = 5.0, apex = 18.8;
            if (lod >= 3)
            {
                Lod3(m, k, t, 1, Kp.Brick, Kp.TowerTile);
                Octagon(m, k, ap, 12.1, v2, Kp.Brick, false);
                OctRoof(m, k, ap2, eaveR, v2, apex, Kp.TowerTile, 3);
                return;
            }
            KpStack.Build(m, k, t, 1, Kp.Brick, KpRoofLook.TilePlain(Kp.TowerTile), 97u, lod);
            // The octagonal brick storey with a small window in every other face, and its wooden cornice.
            Octagon(m, k, ap, 12.1, v1, Kp.BrickNew, true);
            for (int i = 0; i < 8; i++)
            {
                Affine3 fc = KpStack.Face(k, 45 * i, ap);
                if (i % 2 == 0 && lod <= 1) Kp.Lattice(m, fc, 0, 13.4, 0.75, 0.9, lod, 2);
                if (lod <= 1) Kp.Band(m, fc, -1.36, 1.36, v1 - 0.28, v1, 0.12, Kp.Wood);
            }
            // The belvedere: eight carved lattice faces between corner posts.
            Octagon(m, k, ap2, v1, v2, Kp.Wood, false);
            for (int i = 0; i < 8; i++)
            {
                Affine3 fc = KpStack.Face(k, 45 * i, ap2);
                Kp.Lattice(m, fc, 0, 0.5 * (v1 + v2), 2.0, v2 - v1 - 0.35, lod, lod == 0 ? 3 : 0);
            }
            OctRoof(m, k, ap2, eaveR, v2, apex, Kp.TowerTile, lod);
            Kp.Finial(m, k, 0, apex - 0.05, 0, 0.8, 0.2, lod);
            if (lod == 0)
            {
                // Struts under the octagonal eave, one per corner and one per face.
                for (int i = 0; i < 16; i++)
                {
                    double a = Math.PI * i / 8, c = Math.Cos(a), s = Math.Sin(a);
                    Affine3 st = SacredDraw.Basis(k, 0, 0, 0, s, 0, -c, 0, 1, 0);
                    Kp.Strut(m, st, 0, v1 + 0.2, i % 2 == 0 ? ap2 : ap2 / Math.Cos(Math.PI / 8), v2 - 0.05, eaveR - 0.6, 0.06, Kp.Wood);
                }
            }
            Colliders(ref f, HA - HHalf, HA + HHalf, HB - HHalf, HB + HHalf, apex);
        }

        /// <summary>An octagonal prism of apothem <paramref name="ap"/> (faces normal to 0°, 45°, … from +u) from v0 to v1,
        /// with a top cap when asked.</summary>
        private static void Octagon(MeshData m, in Affine3 k, double ap, double v0, double v1, in ShapeBrush b, bool cap)
        {
            double r = ap / Math.Cos(Math.PI / 8);
            for (int i = 0; i < 8; i++)
            {
                double a = Math.PI * i / 4, a0 = a - Math.PI / 8, a1 = a + Math.PI / 8;
                SacredDraw.Quad(m, k, r * Math.Cos(a0), v0, r * Math.Sin(a0), r * Math.Cos(a1), v0, r * Math.Sin(a1), r * Math.Cos(a1), v1, r * Math.Sin(a1),
                                r * Math.Cos(a0), v1, r * Math.Sin(a0), Math.Cos(a), 0, Math.Sin(a), b);
                if (cap) SacredDraw.Tri(m, k, 0, v1, 0, r * Math.Cos(a0), v1, r * Math.Sin(a0), r * Math.Cos(a1), v1, r * Math.Sin(a1), 0, 1, 0, b);
            }
        }

        /// <summary>An octagonal flared pyramid roof: eave ring of apothem <paramref name="eave"/> at v0 rising (concave) to an
        /// apex at v1, a fascia band and a soffit back to the wall of apothem <paramref name="wall"/>.</summary>
        private static void OctRoof(MeshData m, in Affine3 k, double wall, double eave, double v0, double v1, in ShapeBrush tile, int lod)
        {
            double rc = 1 / Math.Cos(Math.PI / 8);
            // Profile (apothem, height): the eave, a knee a third of the way in, the apex.
            double e0 = eave * rc, e1 = (0.58 * eave) * rc, h1 = v0 + 0.3 * (v1 - v0), lift = lod <= 1 ? 0.15 : 0;
            for (int i = 0; i < 8; i++)
            {
                double a = Math.PI * i / 4, a0 = a - Math.PI / 8, a1 = a + Math.PI / 8, c0 = Math.Cos(a0), s0 = Math.Sin(a0), c1 = Math.Cos(a1), s1 = Math.Sin(a1);
                double nu = Math.Cos(a), nw = Math.Sin(a);
                // Lower band (shallow, the corners lifted a little) and upper band (steeper).
                SacredDraw.Quad(m, k, e0 * c0, v0 + lift, e0 * s0, e0 * c1, v0 + lift, e0 * s1, e1 * c1, h1, e1 * s1, e1 * c0, h1, e1 * s0, 0.45 * nu, 1, 0.45 * nw,
                                tile);
                SacredDraw.Tri(m, k, e1 * c0, h1, e1 * s0, e1 * c1, h1, e1 * s1, 0, v1, 0, 0.9 * nu, 1, 0.9 * nw, Kp.Tint(tile, 1.05f));
                if (lod >= 3) continue;
                // Fascia and soffit.
                SacredDraw.Quad(m, k, e0 * c0, v0 - 0.22, e0 * s0, e0 * c1, v0 - 0.22, e0 * s1, e0 * c1, v0 + lift, e0 * s1, e0 * c0, v0 + lift, e0 * s0, nu, 0, nw,
                                Kp.Wood);
                double w0 = wall * rc;
                SacredDraw.Quad(m, k, e0 * c0, v0 - 0.22, e0 * s0, e0 * c1, v0 - 0.22, e0 * s1, w0 * c1, v0 + 0.15, w0 * s1, w0 * c0, v0 + 0.15, w0 * s0, 0, -1, 0,
                                Kp.Ao(Kp.Wood, 0.5f));
                if (lod <= 1)
                {
                    // Hip ridges.
                    var p = new Path3(3);
                    p.Add(e0 * c0, v0 + lift + 0.06, e0 * s0).Add(e1 * c0, h1 + 0.06, e1 * s0).Add(0, v1 + 0.04, 0);
                    Shapes.Tube(m, k, Kp.TileRidge, p, 0.07, 4, false);
                }
            }
        }

        // =============================================================================================================
        // Lalitpur
        // =============================================================================================================

        /// <summary>
        /// The Lalitpur tower (§3.15): square red brick, two skirt roofs on struts, a brick storey with windows, the top
        /// belvedere wrapped in carved lattice under a broad square pyramid (16 m across, deep eaves) with one gilt
        /// finial at 21 m; the grey stone lion on its tall pedestal at the south-west foot on the street.
        /// </summary>
        public static void Lalitpur(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            double am = 0.5 * (LA0 + LA1), bm = 0.5 * (LB0 + LB1), h = 0.5 * (LA1 - LA0);
            Affine3 k = PlanAt(ref f, am, bm);
            var t = new KpTier[3];
            t[0] = new KpTier { Base = 0, WallU = h, WallW = h, Eave = 8.8, EaveU = h + 2.1, EaveW = h + 2.1, Top = 10.0, Walls = KpWalls.Plain };
            t[1] = new KpTier { Base = 10.0, WallU = 5.0, WallW = 5.0, Eave = 12.7, EaveU = 7.0, EaveW = 7.0, Top = 13.8, Walls = KpWalls.Brick, Windows = 3 };
            t[2] = new KpTier { Base = 15.7, WallU = 4.6, WallW = 4.6, Eave = 18.0, EaveU = 8.1, EaveW = 8.1, Top = 20.3, ApexU = 0.3, ApexW = 0.3, Walls = KpWalls.Lattice };
            if (lod >= 3)
            {
                Lod3(m, k, t, 3, Kp.Brick, Kp.TowerTile);
                return;
            }
            KpStack.Build(m, k, t, 3, Kp.BrickNew, KpRoofLook.TilePlain(Kp.TowerTile), 99u, lod);
            // The brick storey under the belvedere.
            var mid = new KpTier { Base = 13.8, WallU = 4.6, WallW = 4.6, Walls = KpWalls.Brick, Windows = 2 };
            KpStack.Storey(m, k, mid, 15.75, Kp.BrickNew, lod, 101u);
            Kp.Finial(m, k, 0, 20.25, 0, 0.75, 0.22, lod);
            // The base faces on the street (S) and the lane (E).
            double half;
            Affine3 s = f.GridSide(LA0, LA1, LB0, LB1, 0, 2, out half);
            KpFacade.Newar(m, s, half, 0, LowFloors, 2, 9.1, 2.2, 1, -1, false, true, lod);
            Affine3 e = f.GridSide(LA0, LA1, LB0, LB1, 0, 1, out half);
            KpFacade.Newar(m, e, half, 0, LowFloors, 2, 9.1, 2.2, -1, -1, false, false, lod);
            // The lion at the south-west foot, facing the street.
            Affine3 lk = f.GridFace(LA0 + 0.9, LB0 - 1.3, 0, 192);
            Kp.Lion(m, lk, 0, 0, 0, 1.5, 1.25, false, 1, lod);
            if (f.C != null)
            {
                double x, y;
                KpFrame.FromGrid(LA0 + 0.9, LB0 - 1.3, out x, out y);
                f.Cylinder(x, y, 0, 2.9, 0.6);
            }
            Colliders(ref f, LA0, LA1, LB0, LB1, 20.3);
        }

        // =============================================================================================================
        // Shared
        // =============================================================================================================

        private static Affine3 PlanAt(ref KpFrame f, double a, double b)
        {
            double x, y;
            KpFrame.FromGrid(a, b, out x, out y);
            return f.Plan(x, y, 0, 102);
        }

        /// <summary>A tower's solid: the base block to its top (no climbing).</summary>
        private static void Colliders(ref KpFrame f, double a0, double a1, double b0, double b1, double top)
        {
            if (f.C == null) return;
            f.GridBox(a0, a1, b0, b1, 0, top, GenColliderFlags.NoClimb, GenColliders.Brick);
        }

        /// <summary>LOD3: each tier's walls as a box and its roof as a pyramid.</summary>
        internal static void Lod3(MeshData m, in Affine3 k, KpTier[] t, int n, in ShapeBrush wall, in ShapeBrush roof)
        {
            for (int i = 0; i < n; i++)
            {
                double next = i + 1 < n ? t[i + 1].Base : t[i].Eave;
                SacredDraw.Box(m, k, -t[i].WallU, t[i].WallU, t[i].Base, Math.Max(t[i].Eave, next - 0.1), -t[i].WallW, t[i].WallW, wall,
                               BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                bool last = i == n - 1;
                double apex = last ? t[i].Top : t[i].Top + 0.6 * (t[i].Top - t[i].Eave);
                KpLod3.Pyramid(m, k, 0, 0, t[i].EaveU, t[i].EaveW, t[i].Eave, apex, roof);
            }
        }
    }
}
