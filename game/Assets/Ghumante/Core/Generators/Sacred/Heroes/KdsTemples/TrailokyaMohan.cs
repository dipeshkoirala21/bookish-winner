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

        /// <summary>The Garuda's red-brick pedestal and its chamfered grey stone cap (judge round 1, KAT2020 03).</summary>
        private static readonly ShapeBrush PedestalBrick = KdsLooks.Brick, CapStone = Looks.Of(MeshColor.FromHex(0x6E6A63), MaterialChannel.Stone);

        /// <summary>
        /// The Garuda before the temple: a red-brick pedestal (1.0 m) with a chamfered grey stone cap carrying the black stone
        /// Garuda (1.6 m) kneeling with joined hands and wings fanning down, facing the door, on the stair axis at w. It
        /// stands at its surveyed place even where a mapped path's corridor covers it (judge round 1: the path's corridor
        /// should be trimmed at the statue, an open issue for the roads package); it only steps off a drawn path's surface.
        /// </summary>
        public static void Garuda(KdsCtx x, double w)
        {
            double ww = double.NaN, uu = 0;
            for (double dw = 0; dw <= 3.0 && double.IsNaN(ww); dw += 0.25)
            {
                if (GroupClear(x, 0, w + dw)) ww = w + dw;
                else if (w - dw >= 8.6 && GroupClear(x, 0, w - dw)) ww = w - dw;
            }
            if (double.IsNaN(ww)) return;
            MeshData m = x.M;
            int lod = x.Lod;
            double vg = x.HasGround ? Math.Max(0, x.GroundV(uu, ww)) : 0;
            Affine3 face = SacredDraw.Turn(x.K, uu, vg, ww, Math.PI);
            if (lod <= 1)
            {
                SacredParts.PlinthLevel(m, face, 0.62, 0.48, 0, 1.0, 0.15, PedestalBrick, PedestalBrick, lod);
                // The cap: a thick slab with chamfered edges.
                Shapes.RoundedBox(m, SacredDraw.At(face, 0, 1.1, 0), CapStone, 1.4, 0.2, 1.12, 0.05, lod == 0 ? 1 : 0);
            }
            else
            {
                SacredDraw.Box(m, face, -0.62, 0.62, 0, 1.0, -0.48, 0.48, PedestalBrick, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, face, -0.7, 0.7, 1.0, 1.2, -0.56, 0.56, CapStone, BoxFaces.All & ~BoxFaces.Bottom);
            }
            KneelingGaruda(m, face, 1.2, lod);
            if (x.CorridorDistance(uu, ww) < 1.0)
            {
                x.StatuesInCorridors.Add(uu);
                x.StatuesInCorridors.Add(ww);
                x.StatuesInCorridors.Add(1.2);
            }
            x.Solid(uu - 0.7, uu + 0.7, ww - 0.56, ww + 0.56, 0, vg + 2.8, GenColliders.Stone);
        }

        // The group's outline in its own frame (it faces the temple, so its back and wings lie toward +w of the temple's
        // frame): the pedestal's corners and the wing tips.
        private static readonly double[] GroupU = { -0.72, 0.72, -0.72, 0.72, -0.85, 0.85 };
        private static readonly double[] GroupW = { -0.58, -0.58, 0.58, 0.58, 0.45, 0.45 };

        /// <summary>True when the Garuda group centred at frame (cu, cw) stands off every drawn road surface.</summary>
        private static bool GroupClear(KdsCtx x, double cu, double cw)
        {
            for (int i = 0; i < GroupU.Length; i++)
                if (x.DrawnDistance(cu + GroupU[i], cw + GroupW[i]) < 0.05) return false;
            return true;
        }

        /// <summary>
        /// The dark stone Garuda of Trailokya Mohan (KAT2020 18, 19), facing +w of frame k on its block from v0: a man-bird
        /// kneeling on the left knee with the right knee raised, hands joined in anjali before the chest, a broad crown
        /// with a front medallion, earrings and a serpent necklace; behind the arms a wing on each side spreads out and sweeps
        /// down to the block in tiers of long feathers. About 1.55 m over the block.
        /// </summary>
        private static void KneelingGaruda(MeshData m, in Affine3 k, double v0, int lod)
        {
            ShapeBrush st = KdsLooks.StoneBlack, lit = Looks.Of(MeshColor.Scale(KdsLooks.StoneBlack.Color, 1.12f), MaterialChannel.Stone);
            // The statue's block.
            SacredDraw.Box(m, k, -0.46, 0.46, v0, v0 + 0.3, -0.3, 0.36, st, BoxFaces.All & ~BoxFaces.Bottom);
            double b = v0 + 0.3;
            int rs = 4, es = lod == 0 ? 6 : 5;
            if (lod >= 2)
            {
                // Far off: a body, a head and two wing plates.
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b + 0.5, 0.02), st, 0.28, 0.5, 0.24, 4, false);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b + 1.12, 0), st, 0.13, 0.2, 0.13, 4, false);
            }
            else
            {
                // Legs: the left knee down on the block pointing forward, the right knee raised, its foot planted.
                Limb(m, k, -0.13, b + 0.3, -0.02, -0.2, b + 0.11, 0.36, 0.1, st, rs);
                Limb(m, k, -0.2, b + 0.09, 0.36, -0.18, b + 0.07, -0.12, 0.075, st, rs);
                Limb(m, k, 0.13, b + 0.3, 0.0, 0.25, b + 0.6, 0.3, 0.1, st, rs);
                Limb(m, k, 0.25, b + 0.6, 0.3, 0.26, b + 0.08, 0.35, 0.075, st, rs);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0.26, b + 0.04, 0.42), st, 0.06, 0.04, 0.11, es, false);
                // Hips, torso and shoulders.
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b + 0.33, -0.02), st, 0.2, 0.13, 0.16, es, false);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b + 0.66, -0.02), lit, 0.25, 0.3, 0.16, es, false);
                // Arms: upper arms down to the elbows, forearms up to the joined hands before the chest.
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    Limb(m, k, sg * 0.23, b + 0.87, -0.02, sg * 0.25, b + 0.62, 0.07, 0.065, st, rs);
                    Limb(m, k, sg * 0.25, b + 0.62, 0.07, sg * 0.04, b + 0.8, 0.17, 0.055, st, rs);
                }
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b + 0.86, 0.18), lit, 0.05, 0.1, 0.05, es, false);
                // Neck and a human face (judge round 1, KAT2020 03): the head with a brow ridge, a nose and full cheeks, earrings,
                // the serpent necklace, and a three-pointed crown (three lobes on a band, the middle one tallest).
                Shapes.Cylinder(m, SacredDraw.At(k, 0, b + 0.9, -0.01), st, 0.07, 0.12, rs, 0, 0, false, false);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b + 1.1, 0.01), lit, 0.135, 0.165, 0.14, es, false);
                if (lod == 0)
                {
                    Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b + 1.16, 0.125), st, 0.11, 0.022, 0.03, 6, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b + 1.1, 0.15), lit, 0.024, 0.045, 0.03, 5, false);
                    for (int sg = -1; sg <= 1; sg += 2) Shapes.Ellipsoid(m, SacredDraw.At(k, sg * 0.07, b + 1.06, 0.115), lit, 0.045, 0.04, 0.03, 5, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(k, 0, b + 1.02, 0.13), st, 0.04, 0.012, 0.015, 4, false);
                }
                Shapes.Frustum(m, SacredDraw.At(k, 0, b + 1.2, 0.0), st, 0.15, 0.14, 0.06, lod == 0 ? 10 : 6, 0.01, 1, false, true);
                for (int q = -1; q <= 1; q++)
                {
                    double lh = q == 0 ? 0.2 : 0.14;
                    Shapes.Ellipsoid(m, SacredDraw.At(k, q * 0.085, b + 1.26 + 0.5 * lh, 0.06 - 0.02 * Math.Abs(q)), lit, 0.055, 0.5 * lh, 0.03, lod == 0 ? 6 : 4, false);
                }
                if (lod == 0)
                {
                    Shapes.Sphere(m, SacredDraw.At(k, -0.14, b + 1.02, 0.0), st, 0.035, 4);
                    Shapes.Sphere(m, SacredDraw.At(k, 0.14, b + 1.02, 0.0), st, 0.035, 4);
                    Shapes.Torus(m, SacredDraw.Basis(k, 0, b + 0.86, 0.02, 1, 0, 0, 0, 0.94, 0.342), st, 0.14, 0.025, 10, 3);
                }
            }
            // The wings fan down from the shoulders like a feathered cape (judge round 1, KAT2020 03, 18): curved back about
            // 30° about the vertical, 0.07 m thick, their upper edge never above the shoulder, the long feathers reaching the
            // block; three tiers whose round-ended feather tips curl outward at the pedestal, the knee and the hip.
            int tiers = lod == 0 ? 3 : lod == 1 ? 2 : 1;
            for (int sg = -1; sg <= 1; sg += 2)
            {
                for (int t = 0; t < tiers; t++)
                {
                    double sc = 1.0 - 0.22 * t, a = 0.52 + 0.08 * t;
                    // In the wing's plane: x out from the shoulder root and back by a, y up (the extrusion runs forward).
                    double ex = sg * Math.Cos(a), ez = -Math.Sin(a), yx = sg * Math.Sin(a), yz = Math.Cos(a);
                    Affine3 wf = SacredDraw.Basis(k, sg * 0.15, b + 0.86, -0.1 + 0.05 * t, ex, 0, ez, yx, 0, yz);
                    int n = WingOutline(sg, sc, lod);
                    Shapes.BevelExtrude(m, wf, t % 2 == 0 ? st : lit, ShapeScratch.U, ShapeScratch.W, n, 0.07, lod == 0 ? 0.02 : 0, lod == 0 ? 1 : 0,
                                        BevelStyle.Round, true, true);
                }
            }
        }

        /// <summary>The outline of one wing tier into the scratch arrays (x outward, the second coordinate the extrude's own
        /// z, which points down in the frame of the right wing and up in the mirrored left one), scaled by sc about the
        /// shoulder root: the upper edge level with the shoulder out to the wing's elbow, the outer edge down to the tip at
        /// the block (1.05 m below the root), then the round ends of five long feathers back up to the root. Returns the
        /// point count.</summary>
        private static int WingOutline(int sg, double sc, int lod)
        {
            double[] px = ShapeScratch.U, pz = ShapeScratch.W;
            int n = 0;
            double flip = sg > 0 ? -1 : 1;
            void P(double x, double y)
            {
                px[n] = x * sc;
                pz[n] = flip * y * sc;
                n++;
            }
            P(0.0, 0.05);
            P(0.3, 0.04);
            P(0.55, -0.02);
            P(0.74, -0.2);
            P(0.86, -0.5);
            P(0.9, -0.8);
            // Five feathers from the tip back toward the root, each ending in a round lobe curling outward.
            const int feathers = 5;
            int arc = lod == 0 ? 3 : 1;
            for (int f = 0; f < feathers; f++)
            {
                double t0 = (double)f / feathers, t1 = (f + 1.0) / feathers;
                double xa = 0.9 - 0.82 * t0, xb = 0.9 - 0.82 * t1;
                double ya = -1.05 + 0.5 * t0 * t0, yb = -1.05 + 0.5 * t1 * t1;
                double cx = 0.5 * (xa + xb), cy = 0.5 * (ya + yb), r = 0.5 * Math.Sqrt((xa - xb) * (xa - xb) + (ya - yb) * (ya - yb));
                for (int q = 0; q <= arc; q++)
                {
                    // The lobe: a half circle below the chord between the feather's two ends.
                    double ang = Math.PI * (q + 0.5) / (arc + 1);
                    P(cx + r * Math.Cos(ang), cy - 0.7 * r * Math.Sin(ang) - 0.03);
                }
                P(xb, yb + 0.06);
            }
            P(0.04, -0.45);
            return n;
        }

        /// <summary>A rounded limb (capsule) from point a to point b of radius r in frame k.</summary>
        private static void Limb(MeshData m, in Affine3 k, double au, double av, double aw, double bu, double bv, double bw, double r, in ShapeBrush br, int seg)
        {
            double len;
            Affine3 along = Affine3.Along(au, av, aw, bu, bv, bw, out len);
            Shapes.Capsule(m, (along * Affine3.Translation(0, -r, 0)).Then(k), br, r, len + 2 * r, seg);
        }
    }
}
