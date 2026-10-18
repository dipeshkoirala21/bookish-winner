using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// Trailokya Mohan Narayan (1680, Vishnu; rebuilt after 2015; kds-temples.md T8 and T9; DANAM KAT2020, OSM parts
    /// w1414593331..34 and roofs w1415349762, w1416278296, w1416686188): five brick stages with stone nosings to 2.8 m
    /// (13.1 → 9.9 m, the OSM outlines), a stone stair in the middle of the west face; a 0.2 m base; an ambulatory of twenty
    /// carved dark posts (six a side) round a white plastered sanctum with a carved lattice door on each side flanked by
    /// black keyhole-shaped blind niches; the carved beam band, the white strut-zone wall with black windows; three roofs
    /// (12.6 / 9.9 / 6.3 m at 8.0 / 11.0 / 14.3 m) with crimson valances; the white stepped pinnacle with a gilt tip to
    /// 18.5 m; the black stone Garuda kneeling with joined hands and spread wings on its brick pedestal, on the stair axis
    /// 10.7 m west, facing the door.
    /// </summary>
    internal static class TrailokyaMohan
    {
        public const float TopM = 18.5f;

        public static double Build(KdsCtx x)
        {
            int lod = x.Lod;
            // Stage sizes from the OSM parts (across the door axis u, along it w).
            double[] su = { 12.5, 12.0, 11.1, 10.2, 9.4 }, sw = { 13.1, 12.6, 11.7, 10.8, 9.9 };
            if (x.PlanHu > 1)
            {
                double fu = 2 * x.PlanHu / su[0], fw = 2 * x.PlanHw / sw[0];
                for (int i = 0; i < su.Length; i++)
                {
                    su[i] *= Math.Min(1.06, Math.Max(0.94, fu));
                    sw[i] *= Math.Min(1.06, Math.Max(0.94, fw));
                }
            }
            const double plinth = 2.8;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, 0.5 * su[0], 0.5 * sw[0], plinth, 2.8, 2.8, new[] { 6.3, 4.95, 3.15 }, new[] { 8.0, 11.0, 14.3 }, new[] { 10.2, 13.2 }, 16.3,
                            KdsLooks.BrickPlinth, KdsLooks.Plaster, KdsLooks.TileNew);
                return 16.3;
            }
            const double stairHalf = 0.75;
            KdsPagoda.StageStack(x, su, sw, plinth, stairHalf, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.12);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = su.Length;
                x.Stats.PlinthTopM = (float)plinth;
            }
            // The west stair: from the top edge down past the lowest stage, kept out of the forecourt path's corridor.
            double top = 0.5 * sw[su.Length - 1], want = 0.5 * sw[0] - top + 2.2, run = want;
            while (run > 2.2 && x.CorridorDistance(0, top + run) < SacredClearance.MarginM) run -= 0.25;
            KdsKit.Stair(x, stairHalf, top, run, 0, plinth, 0, 0, KdsLooks.StonePlinth, KdsLooks.StonePlinth, 13);

            // Base, ambulatory and sanctum.
            KdsPagoda.Base(x, 3.55, 3.55, plinth, plinth + 0.2, KdsLooks.StonePlinth, KdsLooks.Nosing);
            double gv = plinth + 0.2, postTop = 5.5;
            double beamTop = KdsPagoda.Ambulatory(x, 3.35, 3.35, gv, postTop, 5, 0.24, 2.35, 2.35, KdsLooks.Plaster, 4, 0.55, true, true, KdsLooks.Timber);
            if (x.Stats != null)
            {
                x.Stats.SanctumHalfU = 2.35f;
                x.Stats.SanctumHalfW = 2.35f;
                x.Stats.SanctumV0 = (float)gv;
                x.Stats.SanctumV1 = (float)postTop;
            }
            var tiers = new[]
            {
                new KdsTier
                {
                    WallHu = 3.25, WallHw = 3.25, WallV0 = beamTop, EaveHu = 6.3, EaveHw = 6.0, EaveV = 8.0, TopHu = 2.5, TopHw = 2.5, TopV = 10.2, StrutsU = 5, StrutsW = 5,
                    ValanceH = 0.4, Wall = KdsLooks.Plaster, Tile = KdsLooks.TileNew, Windows = 3, WinW = 0.75, WinH = 0.7, Bells = true,
                },
                new KdsTier
                {
                    WallHu = 2.5, WallHw = 2.5, WallV0 = 10.2, EaveHu = 4.95, EaveHw = 4.7, EaveV = 11.0, TopHu = 1.7, TopHw = 1.7, TopV = 13.2, StrutsU = 4, StrutsW = 4,
                    ValanceH = 0.34, Wall = KdsLooks.Plaster, Tile = KdsLooks.TileNew, Windows = 3, WinW = 0.5, WinH = 0.5, Bells = true,
                },
                new KdsTier
                {
                    WallHu = 1.7, WallHw = 1.7, WallV0 = 13.2, EaveHu = 3.15, EaveHw = 3.1, EaveV = 14.3, TopHu = 0.35, TopHw = 0.35, TopV = 16.3, StrutsU = 2, StrutsW = 2,
                    ValanceH = 0.28, Wall = KdsLooks.Plaster, Tile = KdsLooks.TileNew, Windows = 1, WinW = 0.45, WinH = 0.45, Bells = true,
                },
            };
            double apex = KdsPagoda.Tiers(x, tiers, 0x54524C4Bu);
            KdsKit.Cornice(x, 2.5, 2.5, 10.05, 10.3, 0.08, KdsLooks.Timber);
            KdsKit.Cornice(x, 1.7, 1.7, 13.05, 13.3, 0.07, KdsLooks.Timber);
            double tip = KdsKit.WhitePinnacle(x, apex - 0.15, TopM - apex + 0.15, 0.55);
            if (x.Stats != null) x.Stats.Tiers = 3;

            Garuda(x, 10.7);
            return tip;
        }

        /// <summary>The Garuda before the temple: a red-brick pedestal (1.2 m) with a stone cap slab carrying the black stone
        /// Garuda (1.6 m) kneeling on one knee with joined hands and spread wings, facing the door, on the stair axis at w.</summary>
        public static void Garuda(KdsCtx x, double w)
        {
            // Keep the pedestal out of the corridors: if a mapped path runs over the spot, the Garuda moves along the stair
            // axis to the nearest clear place (toward the temple down to the stair foot, or a little further out).
            double ww = double.NaN, uu = 0;
            // The whole group (pedestal corners and wing tips, about 0.95 m from its centre) stays out of the corridor
            // margin: along the axis first, then up to half a metre to the side of it; failing that, the centre alone keeps
            // 0.75 m.
            const double need = 0.95 + SacredClearance.MarginM, needCentre = 0.75 + SacredClearance.MarginM;
            for (double dw = 0; dw <= 5.0 && double.IsNaN(ww); dw += 0.25)
            {
                for (int q = 0; q <= 4 && double.IsNaN(ww); q++)
                {
                    double du = (q + 1) / 2 * 0.25 * (q % 2 == 0 ? 1 : -1);
                    if (w - dw >= 8.6 && x.CorridorDistance(du, w - dw) >= need)
                    {
                        ww = w - dw;
                        uu = du;
                    }
                    else if (x.CorridorDistance(du, w + dw) >= need)
                    {
                        ww = w + dw;
                        uu = du;
                    }
                }
            }
            for (double dw = 0; dw <= 5.0 && double.IsNaN(ww); dw += 0.25)
            {
                if (w - dw >= 8.6 && x.CorridorDistance(0, w - dw) >= needCentre) ww = w - dw;
                else if (x.CorridorDistance(0, w + dw) >= needCentre) ww = w + dw;
            }
            if (double.IsNaN(ww)) return;
            MeshData m = x.M;
            int lod = x.Lod;
            // The pedestal stands on the square's ground at its own spot (10 m from the temple's lowest corner the flags
            // lie higher), never buried.
            double vg = x.HasGround ? Math.Max(0, x.GroundV(uu, ww)) : 0;
            Affine3 face = SacredDraw.Turn(x.K, uu, vg, ww, Math.PI);
            ShapeBrush brick = KdsLooks.BrickPlinth;
            if (lod <= 1)
            {
                SacredParts.PlinthLevel(m, face, 0.62, 0.48, 0, 1.08, 0.15, brick, brick, lod);
                SacredDraw.Box(m, face, -0.7, 0.7, 1.08, 1.22, -0.55, 0.55, KdsLooks.StoneBlack, BoxFaces.All & ~BoxFaces.Bottom);
            }
            else
            {
                SacredDraw.Box(m, face, -0.62, 0.62, 0, 1.22, -0.48, 0.48, brick, BoxFaces.All & ~BoxFaces.Bottom);
            }
            SacredFigures.Garuda(m, face, 0, 1.22, 0, 1.6, KdsLooks.StoneBlack, lod);
            x.Solid(uu - 0.7, uu + 0.7, ww - 0.55, ww + 0.55, 0, vg + 2.8, GenColliders.Stone);
        }
    }
}
