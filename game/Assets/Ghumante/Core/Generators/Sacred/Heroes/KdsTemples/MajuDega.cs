using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// Maju Dega (Maju Deval, Shiva, 1690; kds-temples.md T4; DANAM KAT1910 west elevation by W. Korn, 43.2 px/m): nine
    /// tall, narrow brick stages (25.0 → 9.9 m, each 0.73 m, buff stone nosings) to a 6.6 m top with one east stair
    /// between white sloping parapets; a small temple on top: a 0.6 m base, an ambulatory of six carved posts a side
    /// (8.4 m post line) round a white lime-plaster sanctum with lattice doors on all sides and black windows, a carved beam
    /// band, the white strut-zone wall with its strict pattern of black windows; three roofs (14.1 / 9.6 / 6.7 m at 11.8 /
    /// 16.6 / 20.8 m) with crimson valances; the white plastered bell-dome finial with a gilt tip to 23.5 m; a stone Nandi
    /// on the lowest stage at the west (rear); the 0.5 m stone platform at the foot of the stair (OSM way 908861401).
    /// </summary>
    internal static class MajuDega
    {
        public const float TopM = 23.5f;

        private static readonly double[] Stages = { 25.0, 22.6, 20.5, 18.5, 16.5, 14.7, 13.2, 11.5, 9.9 };
        public const double PlinthTop = 6.6;

        public static double Build(KdsCtx x)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            // The OSM outline (stage 2, 22.6 m) fixes the scale: the stages keep their measured steps round it.
            double scale = x.PlanHu > 1 ? Math.Min(1.06, Math.Max(0.94, 2 * Math.Max(x.PlanHu, x.PlanHw) / 22.6)) : 1.0;
            int n = Stages.Length;
            var wu = new double[n];
            for (int i = 0; i < n; i++) wu[i] = Stages[i] * scale;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, 0.5 * wu[0], 0.5 * wu[0], PlinthTop, 3.2, 3.2, new[] { 7.05, 4.8, 3.35 }, new[] { 11.8, 16.6, 20.8 }, new[] { 14.6, 19.0 }, 22.5,
                            KdsLooks.BrickPlinth, KdsLooks.Plaster, KdsLooks.TileOld);
                return 22.5;
            }
            // The stair is about 2.1 m wide between thick white parapets with half-round copings (KAT1910 10, 12).
            const double stairHalf = 1.05, parapet = 0.44;
            double notch = stairHalf + parapet;
            KdsPagoda.StageStack(x, wu, wu, PlinthTop, notch, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.11);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = n;
                x.Stats.PlinthTopM = (float)PlinthTop;
            }
            // East stair: 33 steps from the top edge down past the lowest stage, white sloping parapets.
            // The stair lands on the 0.5 m stone platform at its foot (OSM way 908861401) when that is clear of the roads.
            double topHalf = 0.5 * wu[n - 1], run = 0.5 * wu[0] - topHalf + 0.75;
            bool platform = Platform(x, 0.5 * wu[0] + 0.4);
            double foot = platform ? 0.5 : 0;
            int steps = KdsKit.Stair(x, stairHalf, topHalf, run, foot, PlinthTop, parapet, 0.45, KdsLooks.Nosing, KdsLooks.Plaster, platform ? 31 : 33);
            if (x.Stats != null) x.Stats.Steps = steps;
            for (int sgn = -1; sgn <= 1; sgn += 2)
                KdsKit.StairCoping(x, sgn * (stairHalf + 0.5 * parapet), topHalf, topHalf + run, foot, PlinthTop, parapet + 0.04, 0.72, KdsLooks.Plaster);

            // The temple base and ground storey.
            double v0 = PlinthTop;
            KdsPagoda.Base(x, 4.45, 4.45, v0, v0 + 0.55, KdsLooks.BrickPlinth, KdsLooks.Nosing);
            double gv = v0 + 0.55, postTop = 9.8, beam = 0.42;
            double beamTop = KdsPagoda.Ambulatory(x, 4.15, 4.15, gv, postTop, 5, 0.26, 2.55, 2.55, KdsLooks.Plaster, 4, beam, true, false, KdsLooks.Timber);
            if (x.Stats != null)
            {
                x.Stats.SanctumHalfU = 2.55f;
                x.Stats.SanctumHalfW = 2.55f;
                x.Stats.SanctumV0 = (float)gv;
                x.Stats.SanctumV1 = (float)postTop;
            }

            // Three roofs on the measured eaves; white storeys with the black window pattern.
            var tiers = new[]
            {
                new KdsTier
                {
                    WallHu = 4.0, WallHw = 4.0, WallV0 = beamTop, EaveHu = 7.05, EaveHw = 7.05, EaveV = 11.8, TopHu = 2.95, TopHw = 2.95, TopV = 14.6, StrutsU = 6,
                    StrutsW = 6, ValanceH = 0.42, Wall = KdsLooks.Plaster, Tile = KdsLooks.TileOld, Windows = 3, WinW = 0.9, WinH = 0.75, Bells = true, RichStruts = true,
                },
                new KdsTier
                {
                    WallHu = 2.95, WallHw = 2.95, WallV0 = 14.6, EaveHu = 4.8, EaveHw = 4.8, EaveV = 16.6, TopHu = 1.75, TopHw = 1.75, TopV = 19.0, StrutsU = 4, StrutsW = 4,
                    ValanceH = 0.36, Wall = KdsLooks.Plaster, Tile = KdsLooks.TileOld, Windows = 3, WinW = 0.6, WinH = 0.6, Bells = true,
                },
                new KdsTier
                {
                    WallHu = 1.75, WallHw = 1.75, WallV0 = 19.0, EaveHu = 3.35, EaveHw = 3.35, EaveV = 20.8, TopHu = 0.3, TopHw = 0.3, TopV = 22.5, StrutsU = 2, StrutsW = 2,
                    ValanceH = 0.3, Wall = KdsLooks.Plaster, Tile = KdsLooks.TileOld, Windows = 1, WinW = 0.55, WinH = 0.55, Bells = true, RichStruts = false,
                },
            };
            double apex = KdsPagoda.Tiers(x, tiers, 0x4D414A55u);
            // Ledges where the upper storeys meet the roofs below.
            KdsKit.Cornice(x, 2.95, 2.95, 14.45, 14.7, 0.1, KdsLooks.Timber);
            KdsKit.Cornice(x, 1.75, 1.75, 18.85, 19.1, 0.08, KdsLooks.Timber);
            double top = KdsKit.BellDomeFinial(x, apex - 0.2, TopM - apex + 0.2, 0.42);
            if (x.Stats != null) x.Stats.Tiers = 3;

            // Nandi at the west (rear), facing the temple: on the second stage's tread, inside the OSM outline (the lane
            // along the west face runs over the lowest stage's edge).
            double sv = 2 * PlinthTop / n, tread0 = 0.5 * wu[2], tread1 = 0.5 * wu[1], rear = -0.5 * (tread0 + tread1), len = 0.92 * (tread1 - tread0);
            if (lod <= 1)
            {
                Affine3 back = SacredDraw.Turn(x.K, 0, sv, rear, Math.PI);
                SacredFigures.Nandi(m, back, 0, 0, 0, len, Looks.Carved, Math.Min(2, lod + 1));
            }
            x.Solid(-0.35, 0.35, rear - 0.5 * len, rear + 0.5 * len, sv, sv + 0.8, GenColliders.Stone);
            return top;
        }

        /// <summary>The 0.5 m stone platform east of the stair foot (6.6 × 5.0 m in OSM), drawn in the temple frame from
        /// <paramref name="w0"/>; false when it would stand in a road corridor.</summary>
        private static bool Platform(KdsCtx x, double w0)
        {
            double w1 = w0 + 5.0;
            double[] pu = ShapeScratch.U, pw = ShapeScratch.W;
            pu[0] = -3.3;
            pw[0] = w0;
            pu[1] = 3.3;
            pw[1] = w0;
            pu[2] = 3.3;
            pw[2] = w1;
            pu[3] = -3.3;
            pw[3] = w1;
            // Keep it out of the road corridors.
            double clear = Math.Min(x.CorridorDistance(0, w1), Math.Min(x.CorridorDistance(-3.3, w1), x.CorridorDistance(3.3, w1)));
            if (clear < SacredClearance.MarginM) return false;
            KdsKit.OutlineLevel(x, pu, pw, 4, 0, 0.5, 0.2, KdsLooks.StoneGrey, KdsLooks.StonePlinth, GenColliders.Stone);
            return true;
        }
    }
}
