using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The forecourt and north-field temples of Kathmandu Durbar Square (kds-temples.md T16, T20, T21, T23, T24; DANAM
    /// KAT1740/1780 site plans, KAT1520 section): the small west-facing Lakshmi Narayan with winged-horse corner struts; the
    /// squat Indrapur whose very broad, low first roof reaches nearly to its plinth edge under a small upper storey; the
    /// slender three-roof Gopinath (Shri Krishna Mahavishnu) on its three-step plinth; Mahavishnu, restored to its three
    /// roofs in 2023 on a 2.2 m three-stage plinth; Kageshwor, one broad tile roof over a post ambulatory out of which rise a
    /// white cubic storey with a small balustrade and a white ribbed shikhara.
    /// </summary>
    internal static class ForecourtTemples
    {
        public const float LakshmiNarayanTopM = 9.0f;
        public const float IndrapurTopM = 11.0f;
        public const float GopinathTopM = 16.0f;
        public const float MahavishnuTopM = 15.8f;
        public const float KageshworTopM = 14.5f;

        /// <summary>Plinth stages from the outline (or the plan) inward: full widths along u and w, equal rises to topV,
        /// a front stair notch and stair.</summary>
        private static void Plinth(KdsCtx x, double[] su, double[] sw, double topV, double stairHalf, double stairRun, int steps)
        {
            KdsPagoda.StageStack(x, su, sw, topV, stairHalf, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.1);
            double top = 0.5 * sw[sw.Length - 1];
            double run = stairRun;
            while (run > 0.9 * topV && x.CorridorDistance(0, top + run) < SacredClearance.MarginM) run -= 0.2;
            KdsKit.Stair(x, stairHalf, top, run, 0, topV, 0.2, 0.15, KdsLooks.StonePlinth, KdsLooks.BrickPlinth, steps);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = su.Length;
                x.Stats.PlinthTopM = (float)topV;
            }
        }

        private static double[] Scaled(double[] widths, double outline)
        {
            var r = (double[])widths.Clone();
            if (outline > 1)
            {
                double f = Math.Min(1.08, Math.Max(0.92, outline / widths[0]));
                for (int i = 0; i < r.Length; i++) r[i] *= f;
            }
            return r;
        }

        public static double LakshmiNarayan(KdsCtx x)
        {
            int lod = x.Lod;
            double[] st = Scaled(new[] { 5.8, 5.1, 4.4 }, 2 * x.PlanHu);
            if (lod >= 3)
            {
                KdsKit.Lod3(x, 0.5 * st[0], 0.5 * st[0], 1.2, 1.7, 1.7, new[] { 2.9, 1.9 }, new[] { 4.0, 6.6 }, new[] { 5.6 }, 7.9, KdsLooks.BrickPlinth, KdsLooks.Brick,
                            KdsLooks.TileOld);
                return 7.9;
            }
            Plinth(x, st, st, 1.2, 0.55, 1.4, 5);
            const double wh = 1.7, v0 = 1.2, v1 = 3.45;
            SacredParts.BandedWall(x.M, x.K, wh, wh, v0, v1, KdsLooks.Brick, KdsLooks.Timber, lod, true, true);
            x.Solid(-wh, wh, -wh, wh, v0, v1, GenColliders.Brick);
            KdsKit.Door(x.M, KdsKit.Side(x.K, 0), 0, v0 + 0.1, wh, 0.75, 1.55, KdsLooks.Timber, lod, true, v1 - 0.05);
            for (int s = 1; s < 4; s++) KdsKit.BlackWindow(x.M, KdsKit.Side(x.K, s), 0, v0 + 1.2, wh, 0.55, 0.7, lod, 0.1, 2);
            var tiers = new[]
            {
                new KdsTier
                {
                    WallHu = 1.65, WallHw = 1.65, WallV0 = v1, EaveHu = 2.9, EaveHw = 2.9, EaveV = 4.0, TopHu = 1.2, TopHw = 1.2, TopV = 5.6, StrutsU = 2, StrutsW = 2,
                    ValanceH = 0.32, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Bells = true,
                },
                new KdsTier
                {
                    WallHu = 1.2, WallHw = 1.2, WallV0 = 5.6, EaveHu = 1.9, EaveHw = 1.9, EaveV = 6.6, TopHu = 0.18, TopHw = 0.18, TopV = 7.9, StrutsU = 1, StrutsW = 1,
                    ValanceH = 0.26, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Windows = 1, WinW = 0.4, WinH = 0.35, Bells = true,
                },
            };
            double apex = KdsPagoda.Tiers(x, tiers, 0x4C4E54u);
            if (x.Stats != null) x.Stats.Tiers = 2;
            return KdsKit.GiltFinial(x, apex - 0.1, LakshmiNarayanTopM - apex + 0.1, 0.2);
        }

        public static double Indrapur(KdsCtx x)
        {
            int lod = x.Lod;
            double[] st = Scaled(new[] { 8.8, 7.8, 7.0 }, 2 * x.PlanHu);
            double edge = 0.5 * st[2];
            if (lod >= 3)
            {
                KdsKit.Lod3(x, 0.5 * st[0], 0.5 * st[0], 1.5, 2.0, 2.0, new[] { edge + 1.0, 2.4 }, new[] { 3.6, 7.6 }, new[] { 6.0 }, 9.1, KdsLooks.BrickPlinth, KdsLooks.Brick,
                            KdsLooks.TileOld);
                return 9.1;
            }
            Plinth(x, st, st, 1.5, 0.6, 1.7, 7);
            // A ring of posts at the top stage's edge round a small sanctum; the beam band over them.
            double pv = 1.5, postTop = 3.35;
            double beamTop = KdsPagoda.Ambulatory(x, edge - 0.3, edge - 0.3, pv, postTop, 3, 0.22, 1.9, 1.9, KdsLooks.Brick, 1, 0.3, false, false, KdsLooks.Timber);
            // The very broad, low first roof reaching nearly to the plinth edge, then the small upper storey.
            var tiers = new[]
            {
                new KdsTier
                {
                    WallHu = edge - 0.4, WallHw = edge - 0.4, WallV0 = beamTop, EaveHu = edge + 0.95, EaveHw = edge + 0.95, EaveV = 3.65, TopHu = 1.55, TopHw = 1.55, TopV = 6.0,
                    StrutsU = 3, StrutsW = 3, StrutFootV = beamTop - 0.05, ValanceH = 0.36, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Bells = true,
                },
                new KdsTier
                {
                    WallHu = 1.55, WallHw = 1.55, WallV0 = 6.0, EaveHu = 2.4, EaveHw = 2.4, EaveV = 7.6, TopHu = 0.2, TopHw = 0.2, TopV = 9.1, StrutsU = 2, StrutsW = 2,
                    ValanceH = 0.28, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Windows = 1, WinW = 0.6, WinH = 0.55, Bells = true,
                },
            };
            double apex = KdsPagoda.Tiers(x, tiers, 0x494E44u);
            if (x.Stats != null) x.Stats.Tiers = 2;
            return KdsKit.GiltFinial(x, apex - 0.1, IndrapurTopM - apex + 0.1, 0.24);
        }

        public static double Gopinath(KdsCtx x)
        {
            int lod = x.Lod;
            double[] st = Scaled(new[] { 10.4, 9.2, 8.0 }, 2 * x.PlanHu);
            if (lod >= 3)
            {
                KdsKit.Lod3(x, 0.5 * st[0], 0.5 * st[0], 2.2, 3.0, 3.0, new[] { 4.5, 3.3, 2.2 }, new[] { 5.6, 9.4, 12.5 }, new[] { 7.6, 11.1 }, 13.9, KdsLooks.BrickPlinth,
                            KdsLooks.Brick, KdsLooks.TileOld);
                return 13.9;
            }
            Plinth(x, st, st, 2.2, 0.65, 2.6, 11);
            double pv = 2.2;
            KdsPagoda.Base(x, 3.25, 3.25, pv, pv + 0.15, KdsLooks.StonePlinth, KdsLooks.Nosing);
            double beamTop = KdsPagoda.Ambulatory(x, 3.0, 3.0, pv + 0.15, 4.55, 4, 0.22, 2.0, 2.0, KdsLooks.Brick, 4, 0.35, false, false, KdsLooks.Timber);
            var tiers = new[]
            {
                new KdsTier
                {
                    WallHu = 2.9, WallHw = 2.9, WallV0 = beamTop, EaveHu = 4.5, EaveHw = 4.5, EaveV = 5.6, TopHu = 2.2, TopHw = 2.2, TopV = 7.6, StrutsU = 4, StrutsW = 4,
                    ValanceH = 0.36, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Windows = 3, WinW = 0.45, WinH = 0.35, Bells = true,
                },
                new KdsTier
                {
                    WallHu = 2.2, WallHw = 2.2, WallV0 = 7.6, EaveHu = 3.3, EaveHw = 3.3, EaveV = 9.4, TopHu = 1.4, TopHw = 1.4, TopV = 11.1, StrutsU = 3, StrutsW = 3,
                    ValanceH = 0.3, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Windows = 3, WinW = 0.4, WinH = 0.45, Bells = true,
                },
                new KdsTier
                {
                    WallHu = 1.4, WallHw = 1.4, WallV0 = 11.1, EaveHu = 2.2, EaveHw = 2.2, EaveV = 12.5, TopHu = 0.2, TopHw = 0.2, TopV = 13.9, StrutsU = 2, StrutsW = 2,
                    ValanceH = 0.26, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Windows = 1, WinW = 0.4, WinH = 0.4, Bells = true,
                },
            };
            double apex = KdsPagoda.Tiers(x, tiers, 0x47504Eu);
            if (x.Stats != null) x.Stats.Tiers = 3;
            return KdsKit.GiltFinial(x, apex - 0.1, GopinathTopM - apex + 0.1, 0.3);
        }

        public static double Mahavishnu(KdsCtx x)
        {
            int lod = x.Lod;
            double hu = x.PlanHu > 1 ? x.PlanHu : 6.2, hw = x.PlanHw > 1 ? x.PlanHw : 5.75;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu, hw, 2.2, 4.0, 4.0, new[] { 5.15, 3.8, 2.4 }, new[] { 6.1, 9.5, 12.4 }, new[] { 7.9, 11.0 }, 13.5, KdsLooks.BrickPlinth, KdsLooks.Brick,
                            KdsLooks.TileNew);
                return 13.5;
            }
            // Three stages: the OSM outline, then two set-backs; the stair on the south (door) face.
            if (x.OutU != null) KdsKit.OutlineLevel(x, x.OutU, x.OutW, x.OutU.Length, 0, 0.75, 0.25, KdsLooks.BrickPlinth, KdsLooks.Nosing, GenColliders.Brick);
            else KdsKit.Stage(x, hu, hw, 0, 0.75, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.1, 0.25, GenColliders.Brick);
            double s2u = Math.Min(hu, hw) - 0.55, s3u = s2u - 0.55;
            KdsKit.Stage(x, s2u, s2u, 0.75, 1.5, s3u, s3u, 0.7, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.1, 0, GenColliders.Brick, s3u);
            KdsKit.Stage(x, s3u, s3u, 1.5, 2.2, 0, 0, 0.7, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.1, 0, GenColliders.Brick, s3u);
            double run = Math.Max(1.2, hw - s3u + 0.3);
            while (run > 2.0 && x.CorridorDistance(0, s3u + run) < SacredClearance.MarginM) run -= 0.2;
            KdsKit.Stair(x, 0.7, s3u, run, 0, 2.2, 0.22, 0.15, KdsLooks.StonePlinth, KdsLooks.BrickPlinth, 11);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = 3;
                x.Stats.PlinthTopM = 2.2f;
            }
            double pv = 2.2, post = Math.Min(4.15, s3u - 0.35);
            double beamTop = KdsPagoda.Ambulatory(x, post, post, pv, 4.6, 4, 0.24, 3.15, 3.15, KdsLooks.Brick, 1, 0.38, false, false, KdsLooks.Timber);
            var tiers = new[]
            {
                new KdsTier
                {
                    WallHu = post - 0.1, WallHw = post - 0.1, WallV0 = beamTop, EaveHu = 5.15, EaveHw = 5.15, EaveV = 6.1, TopHu = 2.5, TopHw = 2.5, TopV = 7.9, StrutsU = 5,
                    StrutsW = 5, ValanceH = 0.4, Wall = KdsLooks.Brick, Tile = KdsLooks.TileNew, Windows = 3, WinW = 0.5, WinH = 0.3, Bells = true,
                },
                new KdsTier
                {
                    WallHu = 2.5, WallHw = 2.5, WallV0 = 7.9, EaveHu = 3.8, EaveHw = 3.8, EaveV = 9.5, TopHu = 1.6, TopHw = 1.6, TopV = 11.0, StrutsU = 3, StrutsW = 3,
                    ValanceH = 0.32, Wall = KdsLooks.Brick, Tile = KdsLooks.TileNew, Windows = 3, WinW = 0.42, WinH = 0.42, Bells = true,
                },
                new KdsTier
                {
                    WallHu = 1.6, WallHw = 1.6, WallV0 = 11.0, EaveHu = 2.4, EaveHw = 2.4, EaveV = 12.4, TopHu = 0.2, TopHw = 0.2, TopV = 13.5, StrutsU = 2, StrutsW = 2,
                    ValanceH = 0.26, Wall = KdsLooks.Brick, Tile = KdsLooks.TileNew, Windows = 1, WinW = 0.4, WinH = 0.4, Bells = true,
                },
            };
            double apex = KdsPagoda.Tiers(x, tiers, 0x4D5653u);
            if (x.Stats != null) x.Stats.Tiers = 3;
            return KdsKit.GiltFinial(x, apex - 0.1, MahavishnuTopM - apex + 0.1, 0.32);
        }

        public static double Kageshwor(KdsCtx x)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            double hu = x.PlanHu > 1 ? x.PlanHu : 5.5, hw = x.PlanHw > 1 ? x.PlanHw : 5.4;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu, hw, 0.8, 4.3, 4.3, new[] { 5.6 }, new[] { 4.0 }, new double[0], 6.0, KdsLooks.BrickPlinth, KdsLooks.TimberRed, KdsLooks.TileOld);
                SacredDraw.Box(m, x.K, -2.3, 2.3, 5.6, 7.6, -2.3, 2.3, KdsLooks.Plaster, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Cone(m, SacredDraw.At(x.K, 0, 7.6, 0), KdsLooks.Plaster, 2.0, 6.0, 6, 0, 0, false);
                return 13.6;
            }
            // Low brick plinth in two steps: the outline, then a set-back.
            if (x.OutU != null) KdsKit.OutlineLevel(x, x.OutU, x.OutW, x.OutU.Length, 0, 0.4, 0.25, KdsLooks.BrickPlinth, KdsLooks.Nosing, GenColliders.Brick);
            else KdsKit.Stage(x, hu, hw, 0, 0.4, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.1, 0.25, GenColliders.Brick);
            double s2 = Math.Min(hu, hw) - 0.45;
            KdsKit.Stage(x, s2, s2, 0.4, 0.8, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.1, 0, GenColliders.Brick);
            for (int s = 0; s < 4; s++) KdsKit.SideStair(x, s, 0, 0.8, s2, 0.6, 0, 0.8, 0, 0, KdsLooks.StonePlinth, KdsLooks.StonePlinth);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = 2;
                x.Stats.PlinthTopM = 0.8f;
            }
            // The post ambulatory round the brick sanctum, one broad tile roof.
            double post = s2 - 0.35;
            double beamTop = KdsPagoda.Ambulatory(x, post, post, 0.8, 3.2, 5, 0.24, 3.0, 3.0, KdsLooks.Brick, 4, 0.35, false, false, KdsLooks.TimberRed);
            var tier = new[]
            {
                new KdsTier
                {
                    WallHu = post - 0.1, WallHw = post - 0.1, WallV0 = beamTop, EaveHu = post + 1.25, EaveHw = post + 1.25, EaveV = 3.95, TopHu = 2.45, TopHw = 2.45,
                    TopV = 6.1, StrutsU = 5, StrutsW = 5, StrutFootV = 3.0, ValanceH = 0.42, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Bells = true,
                },
            };
            KdsPagoda.Tiers(x, tier, 0x4B4147u);
            // The white cubic storey with its small balustrade, the white ribbed shikhara and the gilt finial.
            ShapeBrush white = KdsLooks.Plaster;
            const double cube = 2.4, c0 = 5.9, c1 = 7.5;
            SacredParts.BandedWall(m, x.K, cube, cube, c0, c1, white, white, lod, false, true);
            SacredDraw.CapRect(m, x.K, 0, 0, cube + 0.17, cube + 0.17, c1, true, white);
            if (lod <= 1)
            {
                for (int s = 0; s < 4; s++)
                {
                    Affine3 sk = KdsKit.Side(x.K, s);
                    KdsKit.BlackWindow(m, sk, 0, 0.5 * (c0 + c1) + 0.1, cube, 0.5, 0.55, lod, 0.06, 2);
                    // Balustrade posts round the cube's top.
                    for (int i = 0; i < 7; i++)
                    {
                        double u = -cube + 2 * cube * (i + 0.5) / 7;
                        SacredDraw.Box(m, sk, u - 0.06, u + 0.06, c1, c1 + 0.45, cube - 0.05, cube + 0.07, white, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
                    }
                    SacredDraw.Box(m, sk, -cube - 0.05, cube + 0.05, c1 + 0.45, c1 + 0.55, cube - 0.08, cube + 0.09, white, BoxFaces.All & ~BoxFaces.Bottom);
                }
            }
            x.Solid(-cube, cube, -cube, cube, c0, c1, GenColliders.Brick);
            double topHalf = ShikharaGenerator.Spire(m, x.K, 1.85, c1 + 0.55, 4.6, white, KdsLooks.PlasterShade, 3, lod);
            double cap = KdsKit.Amalaka(x, topHalf, c1 + 0.55 + 4.6, white);
            double tip = KdsKit.GiltFinial(x, cap - 0.05, KageshworTopM - cap + 0.05, 0.2);
            if (x.Stats != null) x.Stats.Tiers = 1;
            return tip;
        }
    }
}
