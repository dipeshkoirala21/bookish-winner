using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// Kavindrapur Mandira (Kabindrapur, Dhansa Dega; 1657, music-god Nasadyo temple; DANAM KAT2000; OSM w906786393; judge
    /// round 1): the long three-storey temple-house south-east of Kasthamandap facing west onto Maru. From the KAT2000
    /// floor plan (15.35 m with its arcade, the brick block 13.8 × 4.65 m) and comparative section (14.88 m from the street
    /// to the pinnacle tip; heights read on the 2006 section and checked on photo 04 by perspective from the car beside
    /// the arcade), the 2023 photos from W, SW and NW: a low brick plinth (0.7 m) carrying a west arcade of slender posts
    /// closed by dark lattice screens on a red-brick dado, under a red-valanced lean-to roof wrapping round the ends
    /// (eave 2.7 m, meeting the wall at 4.6 m); the brick window storey with a row of black carved windows; the balcony
    /// storey with its projecting lattice balcony on the west over a row of small struts; the broad hipped main roof
    /// (eave 8.45 m) with its red valance; the short top storey (about 5.2 m of the block's length) with its own
    /// near-gabled roof (eave 11.6 m, ridge 13.35 m) and three small gilt pinnacles close together at the ridge's middle.
    /// The north neighbour (OSM w1416684423, a modern white house in every photo) is not part of the temple and stays.
    /// </summary>
    internal static class Kavindrapur
    {
        public const float TopM = 14.85f;

        /// <summary>The top storey's half length and half depth (m; photo 04, the 2006 section).</summary>
        private const double TopHu = 2.6, TopHw = 1.75;

        /// <summary>The arcade posts' top (m over the street; the beam sits on them under the lean-to roof).</summary>
        private const double ArcadeTop = 2.8;

        public static double Build(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            // The outline holds the brick block (its east part) and the west platform with the arcade.
            double half = x.PlanHu > 1 ? Math.Max(6.8, Math.Min(8.0, x.PlanHu)) : 7.7;
            double depth = x.PlanHw > 1 ? Math.Max(3.0, Math.Min(4.0, x.PlanHw)) : 3.5;
            const double hb = 2.35, plinth = 0.7;
            double hl = half - 0.75, bc = -depth + hb, wa = depth - bc;
            KitFrame keep = x.Shift(0, bc);
            try
            {
                if (lod >= 3)
                {
                    KdsKit.Lod3(x, half, hb + 0.3, plinth, hl, hb, new[] { hl + 0.85, TopHu + 0.9 }, new[] { 8.45, 11.6 }, new[] { 9.75 }, 13.35,
                                KdsLooks.BrickPlinth, KdsLooks.Brick, KdsLooks.TileOld);
                    SacredDraw.Box(m, x.K, -half, half, 0, 2.7, hb, wa, KdsLooks.WindowBlack, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    return 13.35;
                }
                // The plinth under the block and the arcade.
                double pw0 = -hb - 0.15;
                KdsKit.StageAt(x, 0, 0.5 * (wa + pw0), half, 0.5 * (wa - pw0), 0, plinth, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.08, 0.2, GenColliders.Brick);
                if (x.Stats != null)
                {
                    x.Stats.PlinthLevels = 1;
                    x.Stats.PlinthTopM = (float)plinth;
                }
                // The brick block up to the main roof's soffit, its timber bands.
                SacredParts.BandedWall(m, x.K, hl, hb, plinth, 8.6, KdsLooks.Brick, KdsLooks.Timber, lod, true, false);
                x.Solid(-hl, hl, -hb, hb, plinth, 8.6, GenColliders.Brick);
                KdsKit.Cornice(x, hl + 0.02, hb + 0.02, 4.6, 4.85, 0.12, KdsLooks.Timber);
                KdsKit.Cornice(x, hl + 0.02, hb + 0.02, 6.4, 6.6, 0.1, KdsLooks.Timber);
                if (x.Stats != null)
                {
                    x.Stats.SanctumHalfU = (float)hl;
                    x.Stats.SanctumHalfW = (float)hb;
                }
                Affine3 front = KdsKit.Side(x.K, 0);
                // The doors on the block's west face inside the arcade: the main door with its torana, two side doors.
                KdsKit.Door(m, front, 0, plinth, hb, 1.1, 2.1, KdsLooks.Timber, lod, true, 3.8);
                if (lod <= 1)
                {
                    for (int sg = -1; sg <= 1; sg += 2) KdsKit.Door(m, front, sg * 0.66 * hl, plinth, hb, 0.85, 1.85, KdsLooks.Timber, lod, false);
                }
                Arcade(x, half, hb, wa, plinth);
                // The lean-to roof over the arcade, hipped round the ends: drawn as a hip roof about the arcade's centre
                // whose back slopes and back eave stay inside the brick block (no valance on the back wall).
                double e0 = -hb + 0.05, e1 = wa + 0.5, ce = 0.5 * (e0 + e1);
                KitFrame k2 = x.Shift(0, ce);
                KdsKit.Roof(x, half + 0.35, 0.5 * (e1 - e0), 2.7, hl, hb - ce, 4.6, half - 0.3, wa - 0.3 - ce, ArcadeTop + 0.18, KdsLooks.TileOld, 0.25);
                x.Unshift(k2);
                // The window storey: black carved windows round the block.
                if (lod <= 1)
                {
                    int n = lod == 0 ? 9 : 5;
                    for (int i = 0; i < n; i++)
                    {
                        double u = -hl + 2 * hl * (i + 0.5) / n;
                        KdsKit.BlackWindow(m, front, u, 5.55, hb, 0.72, 0.95, lod, 0.12, 3);
                    }
                    for (int s = 1; s <= 3; s += 2) KdsKit.BlackWindow(m, KdsKit.Side(x.K, s), 0, 5.55, hl, 0.7, 0.95, lod, 0.12, 2);
                    if (lod == 0)
                    {
                        for (int i = 0; i < 5; i++) KdsKit.BlackWindow(m, KdsKit.Side(x.K, 2), -hl + 2 * hl * (i + 0.5) / 5, 5.55, hb, 0.55, 0.75, lod, 0.08, 2);
                    }
                }
                else
                {
                    SacredDraw.Panel(m, front, -hl + 0.4, 5.05, hl - 0.4, 6.05, hb + 0.02, Looks.Recess);
                }
                Balcony(x, hl, hb);
                // The broad hipped main roof with its red valance; winged struts on the ends.
                RoofSpec main = KdsKit.Roof(x, hl + 0.85, hb + 1.6, 8.45, TopHu, TopHw, 9.75, hl, hb, 8.6, KdsLooks.TileOld, 0.32);
                if (lod == 0) KdsKit.Struts(x, main, 7.0, 0, 2, false, 0x4B415649u);
                // The short top storey and its near-gabled roof with the three small gilt pinnacles at the ridge's middle.
                SacredParts.BandedWall(m, x.K, TopHu, TopHw, 9.6, 11.45, KdsLooks.Brick, KdsLooks.Timber, lod, true, true);
                x.Solid(-TopHu, TopHu, -TopHw, TopHw, 9.6, 11.45, GenColliders.Brick);
                if (lod <= 1)
                {
                    int n = lod == 0 ? 5 : 3;
                    for (int s = 0; s <= 2; s += 2)
                    for (int i = 0; i < n; i++)
                        KdsKit.BlackWindow(m, KdsKit.Side(x.K, s), -(TopHu - 0.5) + 2 * (TopHu - 0.5) * (i + 0.5) / n, 10.5, TopHw, 0.6, 0.65, lod, 0.08, 2);
                }
                RoofSpec top = KdsKit.Roof(x, TopHu + 0.9, TopHw + 1.25, 11.6, TopHu, 0.15, 13.35, TopHu, TopHw, 11.45, KdsLooks.TileOld, 0.28);
                if (lod <= 1) KdsKit.Struts(x, top, 10.3, 4, 1, false, 0x4B415650u);
                if (lod == 0) KdsKit.Bells(x, main, 1.3, 0.9);
                double tip = 0;
                for (int i = -1; i <= 1; i++)
                {
                    KitFrame k3 = x.Shift(i * 0.75, 0);
                    tip = Math.Max(tip, KdsKit.GiltFinial(x, 13.25, i == 0 ? TopM - 13.25 : 1.05, i == 0 ? 0.16 : 0.13));
                    x.Unshift(k3);
                }
                if (x.Stats != null) x.Stats.Tiers = 3;
                return tip;
            }
            finally
            {
                x.Unshift(keep);
            }
        }

        /// <summary>The west arcade: slender posts along the platform's edge (and round the ends), dark lattice screens on a
        /// red-brick dado between them, open at the middle bay before the main door, and the beam over the posts.</summary>
        private static void Arcade(KdsCtx x, double half, double hb, double wa, double plinth)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double pw = wa - 0.3, pu = half - 0.3, top = ArcadeTop;
            const int bays = 11;
            int step = lod >= 2 ? 2 : 1;
            for (int i = 0; i <= bays; i += step)
            {
                double u = -pu + 2 * pu * i / bays;
                KdsKit.Post(x, u, pw, plinth, top, 0.2, KdsLooks.Timber);
            }
            // The posts round the ends, back to the block.
            for (int sg = -1; sg <= 1; sg += 2)
                if (pw - hb > 1.2) KdsKit.Post(x, sg * pu, 0.5 * (pw + hb), plinth, top, 0.2, KdsLooks.Timber);
            Affine3 front = KdsKit.Side(x.K, 0);
            ShapeBrush dado = KdsLooks.BrickRed, lat = KdsLooks.WindowBlack;
            for (int i = 0; i < bays; i++)
            {
                double u0 = -pu + 2 * pu * i / bays + 0.1, u1 = -pu + 2 * pu * (i + 1) / bays - 0.1;
                if (i == bays / 2) continue;
                SacredDraw.Box(m, x.K, u0, u1, plinth, plinth + 0.4, pw - 0.06, pw + 0.06, dado, BoxFaces.Front | BoxFaces.Top);
                SacredDraw.Panel(m, front, u0, plinth + 0.4, u1, top - 0.1, pw, Looks.Recess);
                if (lod == 0)
                {
                    // The lattice: thin bars both ways standing off the dark panel.
                    for (int q = 1; q <= 3; q++)
                    {
                        double uu = u0 + (u1 - u0) * q / 4;
                        SacredDraw.Panel(m, front, uu - 0.025, plinth + 0.4, uu + 0.025, top - 0.1, pw + 0.03, lat);
                    }
                    for (int q = 1; q <= 5; q++)
                    {
                        double vv = plinth + 0.4 + (top - 0.5 - plinth) * q / 6;
                        SacredDraw.Panel(m, front, u0, vv - 0.025, u1, vv + 0.025, pw + 0.035, lat);
                    }
                }
                else if (lod == 1)
                {
                    SacredDraw.Panel(m, front, u0, 0.5 * (plinth + top), u1, 0.5 * (plinth + top) + 0.06, pw + 0.03, lat);
                }
            }
            // The beam over the posts along the front and round the ends.
            SacredDraw.Box(m, x.K, -pu - 0.12, pu + 0.12, top, top + 0.2, pw - 0.12, pw + 0.12, KdsLooks.Timber, BoxFaces.All);
            if (lod <= 1)
            {
                for (int sg = -1; sg <= 1; sg += 2)
                    SacredDraw.Box(m, x.K, sg * pu - 0.12, sg * pu + 0.12, top, top + 0.2, hb, pw - 0.12, KdsLooks.Timber, BoxFaces.All & ~BoxFaces.Back);
            }
            // The screens close the arcade but for the middle bay.
            double mid0 = -pu + 2 * pu * (bays / 2) / bays, mid1 = -pu + 2 * pu * (bays / 2 + 1) / bays;
            x.Wall(-pu, pw, mid0, pw, plinth, top, 0.08);
            x.Wall(mid1, pw, pu, pw, plinth, top, 0.08);
            x.Floor(-pu, pu, hb, pw, 0, plinth, GenColliders.Brick);
        }

        /// <summary>The balcony storey's projecting west balcony: a floor on a row of small struts, a dark lattice railing,
        /// slender posts up to a beam under the main roof's soffit, the dark inner wall behind.</summary>
        private static void Balcony(KdsCtx x, double hl, double hb)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double bf = hb + 0.75, bl = hl - 0.25;
            Affine3 front = KdsKit.Side(x.K, 0);
            SacredDraw.Panel(m, front, -bl, 6.76, bl, 8.3, hb + 0.01, Looks.Of(MeshColor.FromHex(0x3A2A20), MaterialChannel.WoodCarved));
            SacredDraw.Box(m, x.K, -bl, bl, 6.6, 6.76, hb, bf, KdsLooks.Timber, BoxFaces.All & ~BoxFaces.Back);
            if (lod >= 2)
            {
                SacredDraw.Box(m, x.K, -bl, bl, 6.76, 8.4, bf - 0.05, bf, KdsLooks.WindowBlack, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                return;
            }
            // The lattice railing and its top rail.
            SacredDraw.Panel(m, front, -bl, 6.76, bl, 7.3, bf, KdsLooks.WindowBlack);
            SacredDraw.Box(m, x.K, -bl, bl, 7.3, 7.38, bf - 0.06, bf + 0.04, KdsLooks.Timber, BoxFaces.All);
            int posts = lod == 0 ? 10 : 5;
            for (int i = 0; i <= posts; i++)
            {
                double u = -bl + 2 * bl * i / posts;
                SacredDraw.Box(m, x.K, u - 0.06, u + 0.06, 6.76, 8.25, bf - 0.1, bf + 0.02, KdsLooks.Timber, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            }
            SacredDraw.Box(m, x.K, -bl - 0.1, bl + 0.1, 8.25, 8.4, hb, bf + 0.05, KdsLooks.Timber, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
            if (lod == 0)
            {
                // The lattice bars and the small struts carrying the balcony from the window storey's cornice.
                for (int i = 0; i < 24; i++)
                {
                    double u = -bl + 2 * bl * (i + 0.5) / 24;
                    SacredDraw.Panel(m, front, u - 0.02, 6.8, u + 0.02, 7.28, bf + 0.02, KdsLooks.Timber);
                }
                for (int i = 0; i < 8; i++)
                {
                    double u = -bl + 2 * bl * (i + 0.5) / 8;
                    Shapes.Bar(m, x.K, KdsLooks.Strut, u, 4.95, hb + 0.08, u, 6.58, bf - 0.12, 0.055, 4);
                }
            }
        }
    }
}
