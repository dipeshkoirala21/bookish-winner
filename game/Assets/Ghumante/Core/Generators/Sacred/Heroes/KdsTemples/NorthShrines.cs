using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The small temples of the north field and the north-east corner (judge round 1): Shiva Mandira 1 and 2 (DANAM
    /// KAT1550, KAT1560; KVPT elevations 2002: 7.75 m, a 2.15 m brick cella on a two-step brick plinth of about 4 m, the
    /// door west under a carved torana, a broad first roof on struts, a short upper storey, a second roof, crimson
    /// valances, a gilt finial), Kashinath (KAT1521, rebuilt 2023-25 on its 6 m plinth: a 3.8 m brick cella with its door
    /// south toward Mahavishnu, two tiled roofs on struts, about 9.6 m) and the small Nila Varahi (Varaha, KAT1522: a
    /// 3.3 m brick shrine against a house wall, a lattice front, one red-valanced hipped roof).
    /// </summary>
    internal static class NorthShrines
    {
        public const float ShivaMandira2TopM = 7.75f;
        public const float ShivaMandira1TopM = 7.6f;
        public const float KashinathTopM = 9.6f;
        public const float NilaVarahiTopM = 4.8f;

        /// <summary>The measured numbers of a small two-roof temple (frame metres).</summary>
        private struct TwoRoofSpec
        {
            public double[] Stages;
            public double PlinthTop, StairHalf, StairRun;
            public int Steps;
            public double Cella, WallTop, Ledge;
            public double Eave1, EaveV1, Top1, TopV1;
            public double Wall2, Eave2, EaveV2, TopV2;
            public int Struts1, Struts2;
            public double Valance1, Valance2, TopM, FinialR, DoorW, DoorH;
            public uint Seed;

            /// <summary>The roof tiles (the rebuilt Kashinath's new, the others' weathered).</summary>
            public ShapeBrush Tile;
        }

        /// <summary>Shiva Mandira 2 (KAT1560; OSM w907048009): the KVPT elevation's 7.75 m.</summary>
        public static double ShivaMandira2(KdsCtx x)
        {
            return TwoRoof(x, new TwoRoofSpec
            {
                Stages = new[] { 4.3, 3.6 }, PlinthTop = 0.55, StairHalf = 0.5, StairRun = 0.7, Steps = 3, Cella = 1.08, WallTop = 2.85, Ledge = 3.05, Eave1 = 2.75,
                EaveV1 = 3.55, Top1 = 0.78, TopV1 = 4.75, Wall2 = 0.72, Eave2 = 1.85, EaveV2 = 5.85, TopV2 = 6.85, Struts1 = 2, Struts2 = 1, Valance1 = 0.36,
                Valance2 = 0.3, TopM = ShivaMandira2TopM, FinialR = 0.14, DoorW = 0.7, DoorH = 1.6, Seed = 0x53484932u, Tile = KdsLooks.TileOld,
            });
        }

        /// <summary>Shiva Mandira 1 (KAT1550): its twin north-north-east of it, a little lower.</summary>
        public static double ShivaMandira1(KdsCtx x)
        {
            return TwoRoof(x, new TwoRoofSpec
            {
                Stages = new[] { 4.1, 3.4 }, PlinthTop = 0.5, StairHalf = 0.5, StairRun = 0.6, Steps = 3, Cella = 1.08, WallTop = 2.8, Ledge = 3.0, Eave1 = 2.7,
                EaveV1 = 3.5, Top1 = 0.76, TopV1 = 4.65, Wall2 = 0.7, Eave2 = 1.8, EaveV2 = 5.75, TopV2 = 6.72, Struts1 = 2, Struts2 = 1, Valance1 = 0.36,
                Valance2 = 0.3, TopM = ShivaMandira1TopM, FinialR = 0.14, DoorW = 0.7, DoorH = 1.6, Seed = 0x53484931u, Tile = KdsLooks.TileOld,
            });
        }

        /// <summary>Kashinath (KAT1521; OSM w907080024), its door south toward Mahavishnu.</summary>
        public static double Kashinath(KdsCtx x)
        {
            double st = x.PlanHu > 1 ? Math.Max(5.4, Math.Min(6.4, 2 * Math.Min(x.PlanHu, x.PlanHw))) : 6.0;
            return TwoRoof(x, new TwoRoofSpec
            {
                Stages = new[] { st, st - 0.8 }, PlinthTop = 0.9, StairHalf = 0.65, StairRun = 1.0, Steps = 5, Cella = 1.9, WallTop = 3.9, Ledge = 4.25, Eave1 = 3.6,
                EaveV1 = 4.7, Top1 = 1.2, TopV1 = 6.0, Wall2 = 1.15, Eave2 = 2.3, EaveV2 = 7.3, TopV2 = 8.55, Struts1 = 3, Struts2 = 2, Valance1 = 0.4,
                Valance2 = 0.33, TopM = KashinathTopM, FinialR = 0.18, DoorW = 0.9, DoorH = 2.0, Seed = 0x4B415349u, Tile = KdsLooks.TileNew,
            });
        }

        /// <summary>A small two-roof temple on its stepped plinth: brick cella with the door on +w under a torana and blind
        /// black windows on the other faces, a carved cornice and ledge, the first roof on struts, the short upper storey,
        /// the second roof, crimson valances, a gilt finial.</summary>
        private static double TwoRoof(KdsCtx x, TwoRoofSpec s)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            double wh = s.Cella;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, 0.5 * s.Stages[0], 0.5 * s.Stages[0], s.PlinthTop, wh, wh, new[] { s.Eave1, s.Eave2 }, new[] { s.EaveV1, s.EaveV2 }, new[] { s.TopV1 },
                            s.TopV2, KdsLooks.BrickPlinth, KdsLooks.Brick, s.Tile);
                return s.TopV2;
            }
            KdsPagoda.StageStack(x, s.Stages, s.Stages, s.PlinthTop, s.StairHalf, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.08);
            double top = 0.5 * s.Stages[s.Stages.Length - 1], run = s.StairRun;
            while (run > 0.3 && x.CorridorDistance(0, top + run) < SacredClearance.MarginM) run -= 0.1;
            KdsKit.Stair(x, s.StairHalf, top, run, 0, s.PlinthTop, 0.12, 0.1, KdsLooks.StonePlinth, KdsLooks.BrickPlinth, s.Steps);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = s.Stages.Length;
                x.Stats.PlinthTopM = (float)s.PlinthTop;
                x.Stats.SanctumHalfU = (float)wh;
                x.Stats.SanctumHalfW = (float)wh;
            }
            SacredParts.BandedWall(m, x.K, wh, wh, s.PlinthTop, s.WallTop, KdsLooks.Brick, KdsLooks.Timber, lod, true, true);
            x.Solid(-wh, wh, -wh, wh, s.PlinthTop, s.WallTop, GenColliders.Brick);
            KdsKit.Door(m, KdsKit.Side(x.K, 0), 0, s.PlinthTop + 0.08, wh, s.DoorW, s.DoorH, KdsLooks.Timber, lod, true, s.WallTop - 0.08);
            for (int f = 1; f < 4; f++) KdsKit.BlackWindow(m, KdsKit.Side(x.K, f), 0, s.PlinthTop + 0.5 * (s.WallTop - s.PlinthTop) + 0.1, wh, 0.5, 0.7, lod, 0.1, 2);
            KdsKit.Cornice(x, wh + 0.04, wh + 0.04, s.WallTop, s.WallTop + 0.22, 0.12, KdsLooks.Timber);
            KdsKit.Cornice(x, wh + 0.16, wh + 0.16, s.WallTop + 0.22, s.Ledge, 0.1, KdsLooks.Timber);
            var tiers = new[]
            {
                new KdsTier
                {
                    WallHu = wh, WallHw = wh, WallV0 = s.Ledge, EaveHu = s.Eave1, EaveHw = s.Eave1, EaveV = s.EaveV1, TopHu = s.Top1, TopHw = s.Top1, TopV = s.TopV1,
                    StrutsU = s.Struts1, StrutsW = s.Struts1, StrutFootV = s.Ledge + 0.1, ValanceH = s.Valance1, Wall = KdsLooks.Brick, Tile = s.Tile,
                },
                new KdsTier
                {
                    WallHu = s.Wall2, WallHw = s.Wall2, WallV0 = s.TopV1, EaveHu = s.Eave2, EaveHw = s.Eave2, EaveV = s.EaveV2, TopHu = 0.22, TopHw = 0.22, TopV = s.TopV2,
                    StrutsU = s.Struts2, StrutsW = s.Struts2, StrutFootV = s.TopV1 + 0.3, ValanceH = s.Valance2, Wall = KdsLooks.Brick, Tile = s.Tile,
                    Windows = 1, WinW = 0.42, WinH = 0.36,
                },
            };
            double apex = KdsPagoda.Tiers(x, tiers, s.Seed);
            KdsKit.Cornice(x, s.Wall2 + 0.08, s.Wall2 + 0.08, s.TopV1, s.TopV1 + 0.2, 0.08, KdsLooks.Timber);
            if (x.Stats != null) x.Stats.Tiers = 2;
            return KdsKit.GiltFinial(x, apex - 0.1, s.TopM - apex + 0.1, s.FinialR);
        }

        /// <summary>
        /// Nila Varahi (Varaha Mandira, KAT1522): a 3.3 m square brick shrine on a low base, its back against the house to the
        /// north; the front (+w) a dark carved lattice screen between timber posts under a carved lintel, small black
        /// windows on the sides, one hipped tile roof with a red valance and a small metal finial.
        /// </summary>
        public static double NilaVarahi(KdsCtx x)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            const double h = 1.55, b = 0.3, wallTop = 2.7;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, h + 0.1, h + 0.1, b, h, h, new[] { 2.2 }, new[] { 3.05 }, new double[0], 4.4, KdsLooks.BrickPlinth, KdsLooks.Brick, KdsLooks.TileOld);
                return 4.4;
            }
            KdsKit.Stage(x, h + 0.12, h + 0.12, 0, b, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.06, 0.15, GenColliders.Brick);
            SacredParts.BandedWall(m, x.K, h, h, b, wallTop, KdsLooks.Brick, KdsLooks.Timber, lod, true, true);
            x.Solid(-h, h, -h, h, b, wallTop, GenColliders.Brick);
            Affine3 front = KdsKit.Side(x.K, 0);
            // The lattice front: posts, the dark lattice screen and its carved lintel.
            SacredDraw.Panel(m, front, -0.75, b + 0.1, 0.75, 2.05, h + 0.01, Looks.Recess);
            if (lod <= 1)
            {
                for (int sg = -1; sg <= 1; sg += 2) SacredDraw.Box(m, front, sg * 0.75 - 0.08, sg * 0.75 + 0.08, b, 2.15, h, h + 0.1, KdsLooks.Timber, BoxFaces.Wall);
                SacredDraw.Box(m, front, -0.98, 0.98, 2.05, 2.25, h, h + 0.12, KdsLooks.Timber, BoxFaces.Wall | BoxFaces.Bottom);
                int bars = lod == 0 ? 5 : 2;
                for (int i = 1; i <= bars; i++)
                {
                    double u = -0.75 + 1.5 * i / (bars + 1), v = b + 0.1 + 1.95 * i / (bars + 1);
                    SacredDraw.Panel(m, front, u - 0.02, b + 0.1, u + 0.02, 2.05, h + 0.03, KdsLooks.WindowBlack);
                    SacredDraw.Panel(m, front, -0.75, v - 0.02, 0.75, v + 0.02, h + 0.035, KdsLooks.WindowBlack);
                }
                for (int s = 1; s <= 3; s += 2) KdsKit.BlackWindow(m, KdsKit.Side(x.K, s), 0, 2.15, h, 0.38, 0.34, lod, 0.06, 2);
            }
            RoofSpec r = KdsKit.Roof(x, 2.2, 2.2, 3.05, 0.12, 0.12, 4.35, h, h, wallTop, KdsLooks.TileOld, 0.3);
            if (lod == 0) KdsKit.Struts(x, r, 2.0, 2, 2, false, 0x56415241u);
            if (x.Stats != null)
            {
                x.Stats.Tiers = 1;
                x.Stats.SanctumHalfU = (float)h;
                x.Stats.SanctumHalfW = (float)h;
            }
            return SacredParts.Gajur(m, x.K, 4.25, NilaVarahiTopM - 4.25, 0.07, KdsLooks.Brass, Math.Min(2, lod + 1));
        }
    }
}
