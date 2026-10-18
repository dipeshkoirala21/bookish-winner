using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The Bhagwati temple (kds-temples.md T26, shared with the palace brief §3.18): the long brick range on the palace's
    /// north-west wing (OSM w1415152800, 29.9 × 6.4 m) with souvenir and thangka shopfronts on its raised walkway, two
    /// storeys of dark carved windows and a tiled roof; rising from it near its north-east end the three-roof temple, its
    /// small dark roofs with gilt edges and a gilt finial at about 22 m.
    /// </summary>
    internal static class Bhagwati
    {
        public const float TopM = 22.0f;

        public static double Build(KdsCtx x)
        {
            double hu = x.PlanHu > 2 ? x.PlanHu - 0.1 : 14.8, hw = x.PlanHw > 1 ? Math.Max(2.4, x.PlanHw - 0.15) : 3.0;
            // The street in front (north-east) runs along the outline's slanting front edge: the range's front, with its
            // jettied upper storey (0.35 m out), gives way to the corridor and the range is drawn about the fitted centre.
            double fcu = 0, fcw = 0, ring = 0.35;
            hu += ring;
            hw += ring;
            KdsKit.FitRect(x, ref fcu, ref fcw, ref hu, ref hw, 8.0 + ring, 2.2 + ring);
            hu -= ring;
            hw -= ring;
            // The temple stands on the range near the brief's point (-79, -16), along the range's axis.
            double tu, tw;
            KdsTemplesSite.LocalToTile(x.Tile, -79.0, -16.0, out tu, out tw);
            double uc, wc;
            x.Local(tu, tw, out uc, out wc);
            uc -= fcu;
            KitFrame keepKf = x.Kf;
            Affine3 keepK = x.K;
            x.Kf = keepKf.Offset(fcu, 0, fcw);
            x.K = SacredDraw.Xf(x.Kf);
            try
            {
                return BuildAt(x, hu, hw, uc);
            }
            finally
            {
                x.Kf = keepKf;
                x.K = keepK;
            }
        }

        private static double BuildAt(KdsCtx x, double hu, double hw, double uc)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            const double p0 = 0.45, g1 = 3.2, f1 = 3.4, f2 = 5.75, eave = 8.1, ridge = 10.4;
            uc = Math.Max(-hu + 3.5, Math.Min(hu - 3.5, uc));
            double th = Math.Min(3.1, hw + 0.1);
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu, hw, p0, hu, hw, new[] { hu + 0.9 }, new[] { eave }, new double[0], ridge, KdsLooks.Brick, KdsLooks.Brick, KdsLooks.TileOld);
                KitFrame kf0 = x.Kf;
                Affine3 k0 = x.K;
                x.Kf = kf0.Offset(uc, 0, 0);
                x.K = SacredDraw.Xf(x.Kf);
                KdsKit.Lod3(x, 0, 0, 0, th, th, new[] { 4.4, 3.5, 2.6 }, new[] { 11.8, 15.4, 18.6 }, new[] { 13.9, 17.4 }, 20.4, KdsLooks.Brick, KdsLooks.Brick,
                            KdsLooks.TileOld);
                x.Kf = kf0;
                x.K = k0;
                return 20.4;
            }
            // The raised walkway and the ground storey of shops.
            KdsKit.Stage(x, hu, hw, 0, p0, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Flag, 0.1, 0.25, GenColliders.Brick);
            double bu = hu - 0.3, bw = hw - 0.6;
            SacredParts.BandedWall(m, x.K, bu, bw, p0, f1, KdsLooks.Brick, KdsLooks.Timber, lod, true, true);
            x.Solid(-bu, bu, -bw, bw, p0, f1, GenColliders.Brick);
            ShapeBrush[] goods = { KdsLooks.ClothRed, KdsLooks.ClothBlue, KdsLooks.ClothYellow, KdsLooks.ClothGreen };
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, bu, bw), d = KdsKit.Depth(s, bu, bw);
                int n = s % 2 == 0 ? Math.Max(1, (int)(2 * a / 2.4)) : 1;
                if (lod >= 2) n = Math.Min(n, 3);
                for (int i = 0; i < n; i++)
                {
                    double u = -a + 2 * a * (i + 0.5) / n;
                    SacredDraw.Panel(m, sk, u - 0.8, p0 + 0.05, u + 0.8, g1 - 0.15, d + 0.012, Looks.Dark);
                    if (lod >= 2) continue;
                    SacredDraw.Box(m, sk, u - 0.92, u + 0.92, g1 - 0.15, g1, d, d + 0.1, KdsLooks.Timber, BoxFaces.Wall | BoxFaces.Bottom);
                    SacredDraw.Box(m, sk, u - 0.92, u - 0.8, p0, g1 - 0.15, d, d + 0.1, KdsLooks.Timber, BoxFaces.Wall);
                    SacredDraw.Box(m, sk, u + 0.8, u + 0.92, p0, g1 - 0.15, d, d + 0.1, KdsLooks.Timber, BoxFaces.Wall);
                    if (lod == 0 && s == 0)
                    {
                        // Thangkas and goods hung in the shop fronts.
                        for (int q = 0; q < 3; q++)
                        {
                            double gu = u - 0.5 + 0.5 * q;
                            SacredDraw.Panel(m, sk, gu - 0.18, p0 + 0.9, gu + 0.18, p0 + 1.9, d + 0.03, goods[(i + q) % goods.Length]);
                        }
                    }
                }
            }
            // The carved window storeys (the upper one slightly jettied).
            KdsKit.Cornice(x, bu + 0.02, bw + 0.02, g1, f1 + 0.05, 0.12, KdsLooks.Timber);
            SacredParts.BandedWall(m, x.K, bu, bw, f1, f2, KdsLooks.Brick, KdsLooks.Timber, lod, false, true);
            SacredParts.BandedWall(m, x.K, bu + 0.25, bw + 0.25, f2, eave + 0.3, KdsLooks.Brick, KdsLooks.Timber, lod, true, false);
            x.Solid(-bu - 0.25, bu + 0.25, -bw - 0.25, bw + 0.25, f1, eave, GenColliders.Brick);
            int per = Math.Max(2, (int)(2 * bu / (lod == 0 ? 3.0 : 3.6)));
            Windows(x, bu, bw, f1 + 0.25, f2 - 0.25, per, 0.7, 1.15);
            Windows(x, bu + 0.25, bw + 0.25, f2 + 0.3, eave - 0.35, per, 0.75, 1.2);
            // The range roof (no valance), a few plain struts.
            RoofSpec r = KdsKit.Roof(x, hu + 0.9, hw + 1.05, eave, Math.Max(0.4, hu - hw + 0.2), 0.12, ridge, bu + 0.25, bw + 0.25, eave - 0.15, KdsLooks.TileOld, 0);
            KdsKit.Struts(x, r, f2 + 0.6, Math.Max(4, (int)(hu / 2.6)), 2, false, 0x42484731u);
            // The three-roof temple rising from the range.
            KitFrame kf = x.Kf;
            Affine3 k = x.K;
            x.Kf = kf.Offset(uc, 0, 0);
            x.K = SacredDraw.Xf(x.Kf);
            ShapeBrush tile = KdsLooks.TileOld, gilt = KdsLooks.Gilt;
            SacredParts.BandedWall(m, x.K, th, th, eave - 0.4, 11.95, KdsLooks.Brick, KdsLooks.Timber, lod, false, true);
            x.Solid(-th, th, -th, th, eave, 11.7, GenColliders.Brick);
            KdsPagoda.WindowBand(x, th, th, ridge - 0.6, 11.6, lod == 0 ? 3 : 1, 0.7, 0.7);
            RoofSpec t1 = KdsKit.Roof(x, 4.4, 4.4, 11.8, 2.4, 2.4, 13.9, th, th, 11.7, tile, 0, false, -1, gilt);
            KdsKit.Struts(x, t1, ridge, 4, 4, false, 0x42484732u);
            SacredParts.BandedWall(m, x.K, 2.3, 2.3, 13.85, 15.6, KdsLooks.Brick, KdsLooks.Timber, lod, false, true);
            x.Solid(-2.3, 2.3, -2.3, 2.3, 13.85, 15.3, GenColliders.Brick);
            KdsPagoda.WindowBand(x, 2.3, 2.3, 13.9, 15.2, lod == 0 ? 3 : 1, 0.5, 0.55);
            RoofSpec t2 = KdsKit.Roof(x, 3.5, 3.5, 15.4, 1.5, 1.5, 17.4, 2.3, 2.3, 15.3, tile, 0, false, -1, gilt);
            KdsKit.Struts(x, t2, 14.1, 3, 3, false, 0x42484733u);
            SacredParts.BandedWall(m, x.K, 1.5, 1.5, 17.35, 18.85, KdsLooks.Brick, KdsLooks.Timber, lod, false, true);
            x.Solid(-1.5, 1.5, -1.5, 1.5, 17.35, 18.6, GenColliders.Brick);
            RoofSpec t3 = KdsKit.Roof(x, 2.6, 2.6, 18.6, 0.15, 0.15, 20.4, 1.5, 1.5, 18.55, tile, 0, false, -1, gilt);
            KdsKit.Struts(x, t3, 17.6, 2, 2, false, 0x42484734u);
            if (lod == 0)
            {
                KdsKit.Bells(x, t1, 1.0, 1.0);
                KdsKit.Bells(x, t2, 1.0, 0.9);
            }
            double tip = KdsKit.GiltFinial(x, 20.3, TopM - 20.3, 0.24);
            x.Kf = kf;
            x.K = k;
            if (x.Stats != null) x.Stats.Tiers = 3;
            return tip;
        }

        /// <summary>A row of dark carved windows on each long side (and two on each end) of a storey wall.</summary>
        private static void Windows(KdsCtx x, double hu, double hw, double v0, double v1, int perLong, double ww, double wh)
        {
            double vc = 0.5 * (v0 + v1);
            wh = Math.Min(wh, 0.85 * (v1 - v0));
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, hu, hw), d = KdsKit.Depth(s, hu, hw);
                int n = s % 2 == 0 ? perLong : 2;
                if (x.Lod >= 2) n = s % 2 == 0 ? Math.Min(n, 6) : 1;
                for (int i = 0; i < n; i++)
                {
                    double u = -a + 2 * a * (i + 0.5) / n;
                    KdsKit.BlackWindow(x.M, sk, u, vc, d, ww, wh, x.Lod, 0.1, 2);
                }
            }
        }
    }
}
