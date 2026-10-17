using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// Kasthamandap (Maru Satah), rebuilt 2021 (kds-temples.md T1; DANAM KAT1970 elevation by W. Korn 2023, 42.8 px/m, and
    /// floor plan by A. Basukala, 19.16 m post square): a squat, very wide timber hall. A two-step stone plinth only 0.6 m
    /// high on the OSM outline (21.8 × 21.4 m, then 21.1 m) with the east entrance stair between two pale stone lions and
    /// small stairs north, west and south; an open forest of dark posts 4.5 m tall (ten on each outer side on a 19.2 m
    /// square, a grid of heavier posts inside, four round the centre); four corner bays walled in brick behind the posts with
    /// the projecting carved mezzanine balconies; the small Gorakhnath shrine at the centre; the very broad, low first roof
    /// (eave 22.7 m at 4.5 m, deep crimson valance, short struts from the posts); the brick band storey (11.8 m) with its row
    /// of small carved windows and the continuous carved balcony gallery (14.7 m) on slender posts; the second roof (17.4 m
    /// at 10.6 m); the small top pavilion with its rail; the third roof (10.1 m at 15.5 m) and the dark bronze finial to
    /// 18.8 m; the low pointed-picket fence round the plinth, open at the stairs.
    /// </summary>
    internal static class Kasthamandap
    {
        public const float TopM = 18.8f;

        private static readonly double[] OuterPosts = { -9.16, -8.07, -5.79, -3.49, -1.19, 1.19, 3.49, 5.79, 8.07, 9.16 };
        private static readonly double[] BayGrid = { -8.07, -5.79, -3.49 };

        public static double Build(KdsCtx x)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, 10.9, 10.7, 0.6, 9.3, 9.3, new[] { 11.35, 8.7, 5.05 }, new[] { 4.6, 10.6, 15.5 }, new[] { 7.6, 13.7 }, 17.7, KdsLooks.StonePlinth,
                            KdsLooks.TimberRed, KdsLooks.TileNew);
                return 17.7;
            }
            // Plinth: the OSM outline to 0.3 m, then the 21.1 m step to 0.6 m.
            if (x.OutU != null) KdsKit.OutlineLevel(x, x.OutU, x.OutW, x.OutU.Length, 0, 0.3, 0.2, KdsLooks.StonePlinth, KdsLooks.StonePlinth, GenColliders.Stone);
            else KdsKit.Stage(x, 10.9, 10.7, 0, 0.3, 0, 0, 0, KdsLooks.StonePlinth, KdsLooks.StonePlinth, 0.08, 0.2, GenColliders.Stone);
            KdsKit.Stage(x, 10.55, 10.55, 0.3, 0.6, 0, 0, 0, KdsLooks.StonePlinth, KdsLooks.Nosing, 0.1, 0, GenColliders.Stone);
            const double pv = 0.6;
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = 2;
                x.Stats.PlinthTopM = (float)pv;
            }
            // Stairs: the east entrance (three steps between the lions) and the small north, west and south stairs.
            KdsKit.Stair(x, 1.3, 10.55, 0.95, 0, pv, 0, 0, KdsLooks.StonePlinth, KdsLooks.StonePlinth, 3);
            for (int s = 1; s < 4; s++) KdsKit.SideStair(x, s, 0, 0.65, 10.55, 0.7, 0, pv, 0, 0, KdsLooks.StonePlinth, KdsLooks.StonePlinth);
            KdsKit.Lion(x, -1.95, 11.15, 0.55, 1.3, -1, false, 0.32);
            KdsKit.Lion(x, 1.95, 11.15, 0.55, 1.3, 1, false, 0.32);

            // Posts: the outer ring, the corner-bay grids and the cross-shaped centre.
            const double postTop = 4.45;
            ShapeBrush wood = KdsLooks.TimberRed;
            for (int s = 0; s < 4; s++)
            {
                for (int i = 0; i < OuterPosts.Length; i++)
                {
                    if (i == OuterPosts.Length - 1) continue; // the corner post belongs to the next side
                    double u, w;
                    SacredParts.OnRect(s, OuterPosts[i] / 9.16, 9.16, 9.16, out u, out w);
                    KdsKit.Post(x, u, w, pv, postTop, 0.26, wood);
                }
            }
            if (lod <= 1)
            {
                for (int qu = -1; qu <= 1; qu += 2)
                for (int qw = -1; qw <= 1; qw += 2)
                {
                    for (int a = 0; a < BayGrid.Length; a++)
                    for (int b = 0; b < BayGrid.Length; b++)
                    {
                        if (a == 0 && b == 0) continue; // on the outer ring
                        KdsKit.Post(x, qu * -BayGrid[a], qw * -BayGrid[b], pv + 0.35, postTop, 0.32, wood);
                    }
                    KdsKit.Post(x, qu * 2.15, qw * 2.15, pv, postTop, 0.32, wood);
                    KdsKit.Post(x, qu * 1.19, qw * 5.79, pv, postTop, 0.32, wood);
                    KdsKit.Post(x, qu * 5.79, qw * 1.19, pv, postTop, 0.32, wood);
                }
            }
            // Corner bays: raised floors walled in brick on their outer faces, the carved mezzanine balconies over them.
            for (int qu = -1; qu <= 1; qu += 2)
            for (int qw = -1; qw <= 1; qw += 2)
                CornerBay(x, qu, qw, pv);
            // The beam ring over the posts and the dark ceiling of the hall.
            KdsKit.Cornice(x, 9.3, 9.3, postTop, postTop + 0.32, 0.1, KdsLooks.Timber);
            SacredDraw.Quad(m, x.K, -9.2, postTop, -9.2, 9.2, postTop, -9.2, 9.2, postTop, 9.2, -9.2, postTop, 9.2, 0, -1, 0, Looks.Ao(KdsLooks.Beam, 0.35f));
            x.Floor(-9.3, 9.3, -9.3, 9.3, postTop, postTop + 0.3, GenColliders.Wood);
            if (lod <= 1) Gorakhnath(x, pv);

            // First roof: very broad and low; its soffit returns to the beam over the outer posts; short struts.
            const double brick = 5.9;
            RoofSpec r1 = KdsKit.Roof(x, 11.35, 11.35, 4.85, brick + 0.05, brick + 0.05, 7.6, 9.3, 9.3, postTop + 0.3, KdsLooks.TileNew, 0.48);
            KdsKit.Struts(x, r1, 3.3, 9, 9, false, 0x4B41u);
            if (lod == 0 && x.Stats != null) x.Stats.Bells += KdsKit.Bells(x, r1, 1.6, 1.1);

            // The brick band storey with its small carved windows, the continuous balcony gallery.
            double bandTop = 8.9;
            SacredParts.BandedWall(m, x.K, brick, brick, 7.3, 11.2, KdsLooks.Brick, KdsLooks.Timber, lod, false, false);
            x.Solid(-brick, brick, -brick, brick, 7.3, 10.6, GenColliders.Brick);
            if (lod <= 1) BandWindows(x, brick, 7.6, bandTop);
            Gallery(x, 7.35, brick, bandTop, 10.5, 0.92, 9);

            // Second roof, third storey, third roof and the dark bronze finial.
            const double top3 = 3.25;
            RoofSpec r2 = KdsKit.Roof(x, 8.7, 8.7, 10.6, top3 + 0.05, top3 + 0.05, 13.7, 7.3, 7.3, 10.55, KdsLooks.TileNew, 0.4);
            if (lod == 0 && x.Stats != null) x.Stats.Bells += KdsKit.Bells(x, r2, 1.4, 1.0);
            SacredParts.BandedWall(m, x.K, 2.8, 2.8, 13.6, 16.6, KdsLooks.Brick, KdsLooks.Timber, lod, false, false);
            Gallery(x, top3, 2.8, 13.7, 15.4, 0.6, 3);
            RoofSpec r3 = KdsKit.Roof(x, 5.05, 5.05, 15.5, 0.32, 0.32, 17.7, 3.3, 3.3, 15.45, KdsLooks.TileNew, 0.32);
            if (lod == 0 && x.Stats != null) x.Stats.Bells += KdsKit.Bells(x, r3, 1.2, 0.9);
            double tip = KdsKit.BronzeFinial(x, 17.55, TopM - 17.55, 0.42);
            if (x.Stats != null) x.Stats.Tiers = 3;

            // The low picket fence on the plinth edge, open at the four stairs.
            const double fh = 1.0, fe = 10.35;
            for (int s = 0; s < 4; s++)
            {
                double gap = s == 0 ? 2.9 : 1.2;
                double au, aw, bu, bw, cu, cw, du, dw;
                SacredParts.OnRect(s, -1, fe, fe, out au, out aw);
                SacredParts.OnRect(s, -gap / fe, fe, fe, out bu, out bw);
                SacredParts.OnRect(s, gap / fe, fe, fe, out cu, out cw);
                SacredParts.OnRect(s, 1, fe, fe, out du, out dw);
                PicketOnPlinth(x, au, aw, bu, bw, fh, pv);
                PicketOnPlinth(x, cu, cw, du, dw, fh, pv);
            }
            return tip;
        }

        /// <summary>A picket run standing on the plinth top (frame raised by the plinth height).</summary>
        private static void PicketOnPlinth(KdsCtx x, double u0, double w0, double u1, double w1, double h, double pv)
        {
            Affine3 k = x.K;
            KitFrame kf = x.Kf;
            GenFrame f = x.F;
            x.K = SacredDraw.At(k, 0, pv, 0);
            x.Kf = kf.Offset(0, pv, 0);
            x.F = new GenFrame(f.X, f.Z, (float)(f.GroundY + pv), f.YawDeg);
            KdsKit.Picket(x, u0, w0, u1, w1, h);
            x.K = k;
            x.Kf = kf;
            x.F = f;
        }

        /// <summary>One corner bay (quadrant qu, qw): its raised floor, brick walls behind the outer posts and the carved
        /// mezzanine balcony fronts on its two outer faces.</summary>
        private static void CornerBay(KdsCtx x, int qu, int qw, double pv)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double a0 = 3.07, a1 = 9.0, fl = pv + 0.35;
            double u0 = qu < 0 ? -a1 : a0, u1 = qu < 0 ? -a0 : a1, w0 = qw < 0 ? -a1 : a0, w1 = qw < 0 ? -a0 : a1;
            SacredDraw.Box(m, x.K, u0, u1, pv, fl, w0, w1, KdsLooks.StonePlinth, BoxFaces.All & ~BoxFaces.Bottom);
            x.Floor(u0, u1, w0, w1, pv, fl, GenColliders.Stone);
            // Brick walls along the two outer faces, 2.1 m high, set behind the post line.
            const double wt = 0.32, wh = 2.15;
            double ou = qu * 8.75, ow = qw * 8.75;
            SacredDraw.Box(m, x.K, Math.Min(ou, ou - qu * wt), Math.Max(ou, ou - qu * wt), fl, fl + wh, w0, w1, KdsLooks.Brick, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, x.K, u0, u1, fl, fl + wh, Math.Min(ow, ow - qw * wt), Math.Max(ow, ow - qw * wt), KdsLooks.Brick, BoxFaces.All & ~BoxFaces.Bottom);
            x.Solid(Math.Min(ou, ou - qu * wt), Math.Max(ou, ou - qu * wt), w0, w1, fl, fl + wh, GenColliders.Brick);
            x.Solid(u0, u1, Math.Min(ow, ow - qw * wt), Math.Max(ow, ow - qw * wt), fl, fl + wh, GenColliders.Brick);
            // The carved balcony fronts on both outer faces of the bay.
            int sideU = qu < 0 ? 1 : 3, sideW = qw < 0 ? 2 : 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int s = pass == 0 ? sideU : sideW;
                // Along the side frame's own u axis: side 1 runs along +w, side 3 along −w, side 0 along +u, side 2 along −u.
                double c = pass == 0 ? 0.5 * (w0 + w1) : 0.5 * (u0 + u1);
                if (s == 3 || s == 2) c = -c;
                Balcony(x, KdsKit.Side(x.K, s), c - 2.95, c + 2.95, 9.3, 2.35, lod);
            }
        }

        /// <summary>A projecting carved balcony front on side frame sk from along0 to along1, at distance d, from v: a sill beam,
        /// the outward-leaning carved panel band (lattice and figure panels) and a top rail.</summary>
        private static void Balcony(KdsCtx x, in Affine3 sk, double a0, double a1, double d, double v, int lod)
        {
            MeshData m = x.M;
            ShapeBrush wood = KdsLooks.TimberRed, dark = KdsLooks.Timber;
            SacredDraw.Box(m, sk, a0, a1, v, v + 0.16, d - 0.3, d + 0.42, dark, BoxFaces.All);
            // The leaning panel band.
            int panels = lod == 0 ? 10 : lod == 1 ? 4 : 1;
            for (int i = 0; i < panels; i++)
            {
                double p0 = a0 + (a1 - a0) * i / panels, p1 = a0 + (a1 - a0) * (i + 1) / panels;
                ShapeBrush b = lod == 0 && i % 2 == 1 ? KdsLooks.Timber : wood;
                SacredDraw.Quad(m, sk, p0, v + 0.16, d + 0.38, p1, v + 0.16, d + 0.38, p1, v + 0.78, d + 0.62, p0, v + 0.78, d + 0.62, 0, 0.36, 0.93, b);
                if (lod == 0)
                {
                    // A carved figure boss on each panel.
                    Shapes.Ellipsoid(m, SacredDraw.At(sk, 0.5 * (p0 + p1), v + 0.47, d + 0.55), Looks.Ao(wood, 0.9f), 0.09, 0.2, 0.06, 6, false);
                }
            }
            SacredDraw.Box(m, sk, a0, a1, v + 0.78, v + 0.88, d - 0.1, d + 0.66, dark, BoxFaces.All & ~BoxFaces.Bottom);
            // Ends.
            SacredDraw.Quad(m, sk, a0, v + 0.16, d - 0.3, a0, v + 0.16, d + 0.38, a0, v + 0.78, d + 0.62, a0, v + 0.78, d - 0.3, -1, 0, 0, dark);
            SacredDraw.Quad(m, sk, a1, v + 0.16, d - 0.3, a1, v + 0.16, d + 0.38, a1, v + 0.78, d + 0.62, a1, v + 0.78, d - 0.3, 1, 0, 0, dark);
        }

        /// <summary>The row of small carved window and vent panels in the brick band (small, double, centre, double, small).</summary>
        private static void BandWindows(KdsCtx x, double half, double v0, double v1)
        {
            double vc = 0.5 * (v0 + v1) + 0.05;
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                KdsKit.BlackWindow(x.M, sk, 0, vc, half, 0.55, 0.55, x.Lod, 0.1, 2);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    KdsKit.BlackWindow(x.M, sk, sgn * 2.1, vc, half, 0.9, 0.42, x.Lod, 0.16, 3);
                    KdsKit.BlackWindow(x.M, sk, sgn * 4.4, vc + 0.05, half, 0.32, 0.36, x.Lod, 0.06, 1);
                }
            }
        }

        /// <summary>
        /// A continuous projecting balcony gallery round a storey: a floor slab out to half size g at v0, a carved rail with
        /// balusters, slender posts every ~1.6 m at the edge up to v1 (under the next eave), and the storey wall behind (half
        /// size wall).
        /// </summary>
        private static void Gallery(KdsCtx x, double g, double wall, double v0, double v1, double railH, int postsPerSide)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            ShapeBrush dark = KdsLooks.Timber, red = KdsLooks.TimberRed;
            // Floor slab (with a moulded fascia).
            KdsKit.Cornice(x, g, g, v0 - 0.25, v0, 0.12, dark);
            SacredDraw.CapRect(m, x.K, 0, 0, g + 0.1, g + 0.1, v0, true, KdsLooks.Beam);
            x.Floor(-g, g, -g, g, v0 - 0.25, v0, GenColliders.Wood);
            // Rail and balusters.
            double rv = v0 + railH;
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                SacredDraw.Box(m, sk, -g, g, rv - 0.09, rv, g - 0.06, g + 0.06, dark, BoxFaces.All);
                SacredDraw.Box(m, sk, -g, g, v0, v0 + 0.12, g - 0.05, g + 0.05, dark, BoxFaces.Front | BoxFaces.Top);
                if (lod == 0)
                {
                    int n = (int)(2 * g / 0.2);
                    for (int i = 0; i < n; i++)
                    {
                        double u = -g + 2 * g * (i + 0.5) / n;
                        SacredDraw.Box(m, sk, u - 0.025, u + 0.025, v0 + 0.12, rv - 0.09, g - 0.025, g + 0.025, red, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    }
                }
                else
                {
                    SacredDraw.Panel(m, sk, -g, v0 + 0.12, g, rv - 0.09, g, Looks.Ao(red, 0.7f));
                }
                for (int i = 0; i < postsPerSide; i++)
                {
                    double u = -g + 2 * g * i / postsPerSide;
                    if (lod >= 2 && i % 3 != 0) continue;
                    SacredDraw.Box(m, sk, u - 0.07, u + 0.07, v0, v1, g - 0.07, g + 0.07, dark, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                }
                x.Wall(-g, g, g, g, v0, rv, 0.06);
            }
            // A lintel ring at the post tops.
            KdsKit.BeamRing(x, g + 0.08, g + 0.08, v1 - 0.18, v1, 0.25, dark);
        }

        /// <summary>The Gorakhnath shrine at the centre: a small carved wooden aedicule on a stone base with a tile roof.</summary>
        private static void Gorakhnath(KdsCtx x, double pv)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            SacredDraw.Box(m, x.K, -1.3, 1.3, pv, pv + 0.4, -0.95, 0.95, KdsLooks.StonePlinth, BoxFaces.All & ~BoxFaces.Bottom);
            SacredParts.BandedWall(m, x.K, 1.05, 0.75, pv + 0.4, pv + 2.3, KdsLooks.TimberRed, KdsLooks.Timber, lod, true, true);
            Affine3 front = KdsKit.Side(x.K, 0);
            KdsKit.LatticeDoor(m, front, 0, pv + 0.45, 0.75, 0.8, 1.5, lod);
            var r = new RoofSpec
            {
                EaveU = 1.5, EaveW = 1.2, EaveV = pv + 2.3, TopU = 0.4, TopW = 0.1, TopV = pv + 3.1, WallU = 1.05, WallW = 0.75, WallTopV = pv + 2.35, Lift = 0.06,
                Sag = 0.02, FasciaH = 0.1, CoursePitch = 0.45, Horns = true, Roof = KdsLooks.TileNew, Ridge = KdsLooks.TileOld, Fascia = KdsLooks.Timber,
                Soffit = Looks.Ao(KdsLooks.Timber, 0.55f), Horn = KdsLooks.Timber,
            };
            SacredParts.Roof(m, x.K, r, lod);
            x.Solid(-1.3, 1.3, -0.95, 0.95, pv, pv + 3.1, GenColliders.Wood);
        }
    }
}
