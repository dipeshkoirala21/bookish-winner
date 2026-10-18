using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// Kumari Ghar (1757; kds-palace.md §3.19) in its own plan frame (u to bearing 100°, w to 10°, the OSM centroid of
    /// w429890388 at the origin): four three-storey brick ranges round Kumari Chowk on a brick plinth, 21.1 m (E-W) by
    /// 25.3 m (N-S). The north facade on the square (photos c_kumari 00, 14): arched dark openings either side of the
    /// carved door under its gilt torana, the two white lions on the steps, the first-floor row of carved windows under big
    /// and small carved crests, the top row of carved windows with the gilt one over the door, the red-fringed valance
    /// under the deep eave, the low hipped tile roof with moss, and the cluster of five gilt pinnacles on the ridge with
    /// its gilt banner. The east side on Basantapur has the same rows and the narrow white corner pier with the yellow door
    /// (c_kumari 12, 13); the court (LOD0 and LOD1) has carved windows on every face and the Kumari's gilt window with its
    /// red valance on the south range. Built as restored, without the post-2015 shores.
    /// </summary>
    internal static class KpKumari
    {
        public const double CX = -102.3, CY = -84.0, Bearing = 100;

        /// <summary>Half sizes of the outer walls, the depth of the north and south ranges and of the east and west ranges.</summary>
        public const double HU = 10.55, HW = 12.65, DepthNS = 5.9, DepthEW = 4.6;

        /// <summary>Levels above the square: plinth top, first and second floor, eave, ridge.</summary>
        public const double Plinth = 0.45, Floor1 = 4.2, Floor2 = 7.8, Eave = 10.9, Ridge = 13.6;

        public static void Build(ref KpFrame f, MeshData m)
        {
            Affine3 k = f.Plan(CX, CY, 0, Bearing);
            int lod = f.Lod;
            if (lod >= 3)
            {
                Kp.Box(m, k, -HU, HU, 0, Eave, -HW, HW, Kp.BrickKumari, BoxFaces.All & ~BoxFaces.Bottom);
                KpLod3.Pyramid(m, k, 0, 0, HU + 1.3, HW + 1.3, Eave, Ridge, Kp.TileMoss);
                Kp.Box(m, k, -0.9, 0.9, Ridge - 0.3, Ridge + 1.6, HW - DepthNS * 0.5 - 0.3, HW - DepthNS * 0.5 + 0.3, Kp.Gilt, BoxFaces.All & ~BoxFaces.Bottom);
                return;
            }
            // The plinth: a brick platform with a stone edge a hand's breadth out from the walls, a broader stepped front.
            KpStack.Stage(m, k, 0, 0, HU + 0.35, HW + 0.35, -0.25, Plinth, Kp.Tint(Kp.BrickKumari, 0.92f), Kp.Step, lod);
            Steps(m, k, lod);
            Ranges(m, k, lod);
            North(ref f, m, k, lod);
            East(m, k, lod);
            Plain(m, k, lod);
            if (lod <= 1) Court(m, k, lod);
            Roofs(m, k, lod);
            Colliders(ref f);
        }

        /// <summary>The two broad steps across the middle of the north front, on which the lions stand.</summary>
        private static void Steps(MeshData m, in Affine3 k, int lod)
        {
            for (int s = 0; s < 2; s++)
            {
                double hu = 4.2 - 0.4 * s, w0 = HW + 0.35, w1 = HW + 1.55 - 0.6 * s, top = Plinth * (s + 1) / 2;
                if (s == 1) top = Plinth;
                Kp.Box(m, k, -hu, hu, -0.2, top, w0 - 0.4, w1, Kp.Ao(Kp.Step, s == 0 ? 0.92f : 1f), BoxFaces.All & ~BoxFaces.Bottom & ~BoxFaces.Back);
            }
        }

        /// <summary>The four ranges' outer and court walls (no tops: the roofs cover them).</summary>
        private static void Ranges(MeshData m, in Affine3 k, int lod)
        {
            ShapeBrush b = Kp.BrickKumari;
            Kp.Box(m, k, -HU, HU, Plinth - 0.1, Eave + 0.2, -HW, HW, b, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            if (lod >= 2)
            {
                // The court as a dark hole is never seen from the street at LOD2: close the top under the roofs.
                return;
            }
            double cu = HU - DepthEW, cw = HW - DepthNS;
            Kp.Floor(m, k, -cu, cu, -cw, cw, Plinth - 0.12, Kp.BrickPave);
        }

        /// <summary>
        /// The north facade (its face frame centred on the wall; face u runs across it): ground floor arched openings and
        /// the door with its torana and lions, the first-floor carved windows with alternating big and small crests, the top
        /// row of carved windows with the gilt one in the middle, string courses of carved timber.
        /// </summary>
        private static void North(ref KpFrame f, MeshData m, in Affine3 k, int lod)
        {
            Affine3 face = KpStack.Face(k, 90, HW);
            Bands(m, face, HU, lod);
            // Ground floor: the door at the centre, two arched openings either side.
            double top = Kp.Doorway(m, face, 0, Plinth, 1.25, 2.35, Kp.Wood, false, lod);
            // The door's crest: carved red-and-gilt wood (c_kumari 14), with a gilt boss.
            Kp.Crest(m, face, 0, top + 0.03, 0.04, 2.3, 1.45, Kp.Tint(Kp.Dado, 0.6f), lod);
            if (lod <= 1) Shapes.Sphere(m, SacredDraw.At(face, 0, top + 0.75, 0.16), Kp.Gilt, 0.16, lod == 0 ? 8 : 5);
            double[] arches = { -8.4, -5.0, 5.0, 8.4 };
            foreach (double u in arches) Kp.ArchWindow(m, face, u, Plinth + 0.95, 1.25, 2.3, lod);
            // The lions on the upper step either side of the door.
            for (int s = -1; s <= 1; s += 2) Kp.Lion(m, face, s * 2.25, Plinth, 0.95, 0.55, 1.55, true, s, lod, SacredFigures.Generic, true);
            // First floor: carved windows, big crests and small ones alternating.
            double[] big = { -8.4, -4.6, 0, 4.6, 8.4 }, small = { -6.5, -2.3, 2.3, 6.5 };
            foreach (double u in big) CrownWindow(m, face, u, Floor1 + 0.75, 1.15, 1.6, true, lod);
            foreach (double u in small) CrownWindow(m, face, u, Floor1 + 1.0, 0.75, 1.15, false, lod);
            // Second floor: the row of carved windows close under the eave, the gilt one over the door.
            TopRow(m, face, HU, lod, true);
        }

        /// <summary>The east side on Basantapur: the same rows, and the white corner pier with its yellow door at the north end.</summary>
        private static void East(MeshData m, in Affine3 k, int lod)
        {
            Affine3 face = KpStack.Face(k, 0, HU);
            Bands(m, face, HW, lod);
            double pier = HW - 0.9;
            // The face's u runs along +w here (the render shows it); the north end is at u = +pier.
            Kp.Box(m, face, pier - 0.85, pier + 0.85, Plinth - 0.1, Eave - 0.4, 0, 0.12, Kp.White, BoxFaces.Wall);
            Kp.Box(m, face, pier - 0.55, pier + 0.55, Plinth, Plinth + 2.3, 0.12, 0.14, Kp.Cream, BoxFaces.Front);
            if (lod == 0) Kp.Box(m, face, pier - 0.6, pier + 0.6, Plinth + 2.3, Plinth + 2.42, 0.12, 0.2, Kp.WhiteTrim, BoxFaces.Wall | BoxFaces.Bottom);
            double[] g = { -8.6, -4.4, -0.6, 4.0, 7.4 };
            foreach (double u in g) Kp.ArchWindow(m, face, u, Plinth + 1.0, 1.1, 2.1, lod);
            double[] big = { -7.0, -2.2, 2.8, 7.6 }, small = { -9.6, -4.6, 0.3, 5.2 };
            foreach (double u in big) CrownWindow(m, face, u, Floor1 + 0.75, 1.1, 1.55, true, lod);
            foreach (double u in small) CrownWindow(m, face, u, Floor1 + 1.0, 0.72, 1.1, false, lod);
            TopRow(m, face, HW, lod, false);
        }

        /// <summary>The south and west sides on the lanes: string courses and plain rows of small carved windows.</summary>
        private static void Plain(MeshData m, in Affine3 k, int lod)
        {
            for (int s = 0; s < 2; s++)
            {
                double half = s == 0 ? HU : HW;
                Affine3 face = KpStack.Face(k, s == 0 ? 270 : 180, s == 0 ? HW : HU);
                Bands(m, face, half, lod);
                if (lod >= 2) continue;
                int wl = Math.Max(1, lod);
                for (double u = -half + 2.0; u < half - 1.0; u += 3.1)
                {
                    Kp.Lattice(m, face, u, Plinth + 2.0, 0.8, 1.2, wl, 2);
                    Kp.Lattice(m, face, u, Floor1 + 1.5, 0.9, 1.3, wl, 3);
                    Kp.Lattice(m, face, u, Floor2 + 1.4, 0.9, 1.1, wl, 3);
                }
            }
        }

        /// <summary>The carved timber string courses at the floor levels and the cornice band under the eave.</summary>
        private static void Bands(MeshData m, in Affine3 face, double half, int lod)
        {
            Kp.Band(m, face, -half, half, Plinth - 0.05, Plinth + 0.2, 0.06, Kp.Step);
            Kp.Band(m, face, -half, half, Floor1 - 0.12, Floor1 + 0.12, 0.1, Kp.Wood);
            Kp.Band(m, face, -half, half, Floor2 - 0.12, Floor2 + 0.12, 0.1, Kp.Wood);
            if (lod == 0)
            {
                Kp.Band(m, face, -half, half, Floor1 + 0.12, Floor1 + 0.2, 0.05, Kp.WoodMid);
                Kp.Band(m, face, -half, half, Floor2 + 0.12, Floor2 + 0.2, 0.05, Kp.WoodMid);
            }
            Kp.Cornice(m, face, -half, half, Eave - 0.55, 0.45, Kp.Wood, lod);
        }

        /// <summary>The top row of carved windows close under the eave (a carved frame band with dark lattices between posts),
        /// the gilt window over the door on the north front.</summary>
        private static void TopRow(MeshData m, in Affine3 face, double half, int lod, bool giltCentre)
        {
            double v0 = Floor2 + 0.55, h = Eave - 0.65 - v0;
            if (lod >= 2)
            {
                Kp.Panel(m, face, -half + 0.6, v0, half - 0.6, v0 + h, 0.03, Kp.Recess);
                if (giltCentre) Kp.Panel(m, face, -1.1, v0, 1.1, v0 + h, 0.05, Kp.Gilt);
                return;
            }
            int n = Math.Max(3, (int)Math.Round((2 * half - 1.2) / 2.1));
            double bay = (2 * half - 1.2) / n;
            for (int i = 0; i < n; i++)
            {
                double u = -half + 0.6 + bay * (i + 0.5);
                if (giltCentre && Math.Abs(u) < 0.5 * bay)
                {
                    Kp.Bay(m, face, u, v0 - 0.05, 2.2, h + 0.1, true, lod);
                    continue;
                }
                Kp.ArchWindow(m, face, u, v0 + 0.05, Math.Min(1.25, bay - 0.55), h - 0.1, lod);
            }
            // A carved sill band along the whole row.
            Kp.Band(m, face, -half + 0.3, half - 0.3, v0 - 0.18, v0, 0.14, Kp.WoodMid);
        }

        /// <summary>
        /// A carved window of the first floor: a dark frame with ears round the lattice and above it a carved crest (a
        /// pointed tympanum with a small finial) — big over the main windows, small over the narrow ones.
        /// </summary>
        private static void CrownWindow(MeshData m, in Affine3 face, double u, double v0, double ww, double wh, bool big, int lod)
        {
            Kp.Lattice(m, face, u, v0 + 0.5 * wh, ww, wh, lod, 3);
            if (lod >= 2)
            {
                Kp.Panel(m, face, u - 0.5 * ww - 0.1, v0 + wh + 0.05, u + 0.5 * ww + 0.1, v0 + wh + (big ? 0.7 : 0.4), 0.03, Kp.Wood);
                return;
            }
            // The lintel with ears and the crest on it.
            Kp.Box(m, face, u - 0.5 * ww - 0.28, u + 0.5 * ww + 0.28, v0 + wh + 0.06, v0 + wh + 0.2, 0, 0.14, Kp.Wood, BoxFaces.Wall | BoxFaces.Bottom);
            double cw = big ? ww + 0.75 : ww + 0.3;
            Kp.Crest(m, face, u, v0 + wh + 0.2, 0.02, cw, big ? 0.85 * cw : 0.6 * cw, Kp.Wood, lod);
            // The sill with its carved band.
            Kp.Box(m, face, u - 0.5 * ww - 0.2, u + 0.5 * ww + 0.2, v0 - 0.22, v0 - 0.06, 0, 0.13, Kp.Wood, BoxFaces.Wall | BoxFaces.Bottom);
        }

        /// <summary>Kumari Chowk: carved windows on all four court faces, the gilt window on the south range under a red
        /// valance, a small shrine (chaitya) in the middle.</summary>
        private static void Court(MeshData m, in Affine3 k, int lod)
        {
            double cu = HU - DepthEW, cw = HW - DepthNS;
            for (int s = 0; s < 4; s++)
            {
                // Inward faces: the court wall at distance d from the centre, facing the centre.
                double half = s % 2 == 0 ? cw : cu, d = s % 2 == 0 ? cu : cw;
                double alpha = s * 90;
                Affine3 face = KpStack.Face(k, alpha + 180, -d);
                Kp.Panel(m, face, -half, Plinth - 0.15, half, Eave + 0.2, 0, Kp.Tint(Kp.BrickKumari, 0.95f));
                Kp.Band(m, face, -half, half, Floor1 - 0.1, Floor1 + 0.1, 0.08, Kp.Wood);
                Kp.Band(m, face, -half, half, Floor2 - 0.1, Floor2 + 0.1, 0.08, Kp.Wood);
                // The court's doors and windows one level coarser than the street fronts (seen from inside or above);
                // the Kumari's gilt window keeps the part's level.
                int n = Math.Max(2, (int)Math.Round(2 * half / 2.4));
                for (int i = 0; i < n; i++)
                {
                    double u = -half + 2 * half * (i + 0.5) / n;
                    bool kumari = s == 3 && i == n / 2;
                    Kp.Doorway(m, face, u, Plinth, 0.9, 2.0, Kp.Wood, false, lod + 1);
                    if (kumari)
                    {
                        Kp.Bay(m, face, u, Floor2 + 0.4, 2.6, 1.9, true, lod);
                        Kp.ValanceLine(m, face, u - 1.6, u + 1.6, Eave - 0.55, 0.45, 0.45, lod);
                        continue;
                    }
                    CrownWindow(m, face, u, Floor1 + 0.8, 0.9, 1.3, i % 2 == 0, lod + 1);
                    Kp.Lattice(m, face, u, Floor2 + 1.3, 0.9, 1.2, lod + 1, 3);
                }
                Kp.Cornice(m, face, -half, half, Eave - 0.55, 0.42, Kp.Wood, lod);
            }
            // The small stone chaitya in the middle of the court.
            if (lod == 0)
            {
                Shapes.RoundedSlab(m, SacredDraw.At(k, 0, Plinth - 0.1, 0), Kp.StoneGrey, 1.6, 1.6, 0.35, 0.06, 0.03, 2);
                Shapes.Dome(m, SacredDraw.At(k, 0, Plinth + 0.25, 0), Kp.StoneGrey, 0.55, 0.6, 12, true);
                Shapes.Cone(m, SacredDraw.At(k, 0, Plinth + 0.8, 0), Kp.StoneGrey, 0.2, 0.6, 8, 0, 0, false);
            }
        }

        /// <summary>
        /// The low hipped tile roof with moss on the four ranges (one roof ring round the court: the outer slopes from the
        /// eave with its red fringe up to the ridges over the range middles, the court slopes back down), the cluster of
        /// five gilt pinnacles on the north ridge with its gilt banner draped down to the front eave.
        /// </summary>
        private static void Roofs(MeshData m, in Affine3 k, int lod)
        {
            const double Over = 1.35;
            ShapeBrush tile = Kp.TileMoss;
            // North and south ranges run the full width; east and west ranges fit between them.
            double nw = HW - 0.5 * DepthNS, ew = HU - 0.5 * DepthEW;
            Kp.Ridge(m, SacredDraw.At(k, 0, 0, nw), HU, 0.5 * DepthNS, Eave, Ridge, Over, tile, true, lod);
            Kp.Ridge(m, SacredDraw.At(k, 0, 0, -nw), HU, 0.5 * DepthNS, Eave, Ridge, Over, tile, true, lod);
            double inner = HW - DepthNS;
            Kp.Ridge(m, SacredDraw.At(k, ew, 0, 0), 0.5 * DepthEW, inner + 0.6, Eave, Ridge - 0.6, Over, tile, true, lod);
            Kp.Ridge(m, SacredDraw.At(k, -ew, 0, 0), 0.5 * DepthEW, inner + 0.6, Eave, Ridge - 0.6, Over, tile, true, lod);
            if (lod >= 2 && lod < 3)
            {
                // Close the court from above at LOD2.
                Kp.Floor(m, k, -(HU - DepthEW), HU - DepthEW, -inner, inner, Eave - 0.4, Kp.Tint(tile, 0.8f));
            }
            // The pinnacle cluster on the north ridge.
            double pv = Ridge + 0.05;
            Kp.PinnacleCluster(m, SacredDraw.At(k, 0, 0, nw), 0, pv, 0, 1.6, lod);
            if (lod == 0)
            {
                double[] pu = { 0, 0, 0, 0 }, pvv = { pv + 0.2, pv - 0.4, Eave + 0.9, Eave - 0.2 }, pw = { nw + 0.2, nw + 0.9, HW + Over - 0.6, HW + Over + 0.05 };
                SacredParts.Pataka(m, k, pu, pvv, pw, 4, 0.32, lod);
            }
        }

        private static void Colliders(ref KpFrame f)
        {
            if (f.C == null) return;
            double s = Math.Sin(Bearing * Math.PI / 180), c = Math.Cos(Bearing * Math.PI / 180);
            double s2 = Math.Sin((Bearing - 90) * Math.PI / 180), c2 = Math.Cos((Bearing - 90) * Math.PI / 180);
            void At(double u, double w, out double x, out double y)
            {
                x = CX + u * s + w * s2;
                y = CY + u * c + w * c2;
            }
            double x0, y0;
            // The plinth (walkable) and the four ranges.
            At(0, 0, out x0, out y0);
            f.Box(x0, y0, HU + 0.35, HW + 0.35, Bearing, 0, Plinth, GenColliderFlags.Walkable, GenColliders.Brick);
            At(0, HW - 0.5 * DepthNS, out x0, out y0);
            f.Box(x0, y0, HU, 0.5 * DepthNS, Bearing, Plinth, Eave, GenColliderFlags.NoClimb, GenColliders.Brick);
            At(0, -HW + 0.5 * DepthNS, out x0, out y0);
            f.Box(x0, y0, HU, 0.5 * DepthNS, Bearing, Plinth, Eave, GenColliderFlags.NoClimb, GenColliders.Brick);
            At(HU - 0.5 * DepthEW, 0, out x0, out y0);
            f.Box(x0, y0, 0.5 * DepthEW, HW - DepthNS, Bearing, Plinth, Eave, GenColliderFlags.NoClimb, GenColliders.Brick);
            At(-HU + 0.5 * DepthEW, 0, out x0, out y0);
            f.Box(x0, y0, 0.5 * DepthEW, HW - DepthNS, Bearing, Plinth, Eave, GenColliderFlags.NoClimb, GenColliders.Brick);
            // The front steps and the lions.
            At(0, HW + 0.95, out x0, out y0);
            f.Box(x0, y0, 4.2, 0.6, Bearing, 0, 0.5 * Plinth, GenColliderFlags.Walkable, GenColliders.Stone);
            for (int q = -1; q <= 1; q += 2)
            {
                At(q * 2.25, HW + 0.95, out x0, out y0);
                f.Cylinder(x0, y0, Plinth, Plinth + 0.7 + 1.55, 0.5);
            }
        }
    }
}
