using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// Jagannath (1563, Mahendra Malla; kds-temples.md T18 and T19; DANAM KAT1740 elevation, plan and site plan): a broad
    /// 1.4 m brick-and-stone platform on the OSM outline with a miniature two-roof temple on each corner; the temple on two
    /// more plinth steps (to 2.3 m) with four identical exposed-brick facades, each a tripartite portal: the carved central
    /// door under a gilt torana with Garuda flanked by two blind doors shown as pink-plastered cusped arches in carved frames,
    /// small carved windows and hanging lamps; the carved cornice band and the struts (deities above, figures at the foot);
    /// two roofs (14 / 8.5 m at 7.4 / 12.3 m) with crimson valances; the gilt finial with Vishnu's wheel to 16.4 m. Pratap
    /// Malla's column (1670) stands 7.8 m east of the centre: a stone shaft on a stepped base, a dark bronze lotus capital
    /// and the gilt seated royal group under a rearing serpent hood, facing the palace.
    /// </summary>
    internal static class Jagannath
    {
        public const float TopM = 16.4f;
        public const float ColumnTopM = 9.0f;

        public static double Build(KdsCtx x)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            double ph = x.PlanHu > 1 ? x.PlanHu : 7.35, pw = x.PlanHw > 1 ? x.PlanHw : 7.2;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, ph, pw, 2.3, 4.3, 4.3, new[] { 7.0, 4.25 }, new[] { 7.4, 12.3 }, new[] { 10.1 }, 14.1, KdsLooks.BrickPlinth, KdsLooks.Brick,
                            KdsLooks.TileOld);
                return 14.1;
            }
            // The platform on the OSM outline (1.4 m) with a centre stair on every side, then two temple steps.
            const double plat = 1.4;
            if (x.OutU != null) KdsKit.OutlineLevel(x, x.OutU, x.OutW, x.OutU.Length, 0, plat, 0.25, KdsLooks.BrickPlinth, KdsLooks.StonePlinth, GenColliders.Brick);
            else KdsKit.Stage(x, ph, pw, 0, plat, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.StonePlinth, 0.14, 0.25, GenColliders.Brick);
            for (int s = 0; s < 4; s++)
            {
                double d = KdsKit.Depth(s, ph, pw);
                KdsKit.SideStair(x, s, 0, 1.0, d, 1.5, 0, plat, 0.3, 0.3, KdsLooks.StonePlinth, KdsLooks.BrickPlinth);
            }
            KdsKit.Stage(x, 5.2, 5.2, plat, 1.85, 4.8, 4.8, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.1, 0, GenColliders.Brick);
            KdsKit.Stage(x, 4.8, 4.8, 1.85, 2.3, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.1, 0, GenColliders.Brick);
            for (int s = 0; s < 4; s++) KdsKit.SideStair(x, s, 0, 0.7, 4.8, 0.7, plat, 2.3, 0, 0, KdsLooks.StonePlinth, KdsLooks.StonePlinth);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = 3;
                x.Stats.PlinthTopM = 2.3f;
            }

            // Walls with the four tripartite portals (KAT1740 elevation east: brick walls 2.3-4.75 m, the carved cornice with
            // its row of small carved beast heads to 5.45 m, the strut zone with three carved windows a side under the first
            // eave at 7.4 m).
            const double wh = 4.3, v0 = 2.3, v1 = 4.75, cTop = 5.45;
            SacredParts.BandedWall(m, x.K, wh, wh, v0, v1, KdsLooks.Brick, KdsLooks.Timber, lod, true, false);
            x.Solid(-wh, wh, -wh, wh, v0, v1, GenColliders.Brick);
            for (int s = 0; s < 4; s++) Portal(x, KdsKit.Side(x.K, s), wh, v0, v1);
            if (x.Stats != null)
            {
                x.Stats.SanctumHalfU = (float)wh;
                x.Stats.SanctumHalfW = (float)wh;
                x.Stats.SanctumV0 = (float)v0;
                x.Stats.SanctumV1 = (float)v1;
            }
            KdsKit.Cornice(x, wh, wh, v1, cTop, 0.3, KdsLooks.Timber);
            if (lod == 0) BeastHeads(x, wh + 0.12, v1 + 0.08, 0.17);

            var tiers = new[]
            {
                new KdsTier
                {
                    WallHu = 4.15, WallHw = 4.15, WallV0 = cTop, EaveHu = 7.0, EaveHw = 7.0, EaveV = 7.4, TopHu = 2.9, TopHw = 2.9, TopV = 10.1, StrutsU = 4,
                    StrutsW = 4, StrutFootV = cTop + 0.1, ValanceH = 0.45, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Windows = 3, WinW = 0.72, WinH = 0.8,
                    Bells = true, RichStruts = true,
                },
                new KdsTier
                {
                    WallHu = 2.9, WallHw = 2.9, WallV0 = 10.1, EaveHu = 4.25, EaveHw = 4.25, EaveV = 12.3, TopHu = 0.3, TopHw = 0.3, TopV = 14.1, StrutsU = 4, StrutsW = 4,
                    ValanceH = 0.38, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Windows = 1, WinW = 0.6, WinH = 0.7, Bells = true,
                },
            };
            double apex = KdsPagoda.Tiers(x, tiers, 0x4A41474Eu);
            KdsKit.Cornice(x, 2.9, 2.9, 9.95, 10.2, 0.08, KdsLooks.Timber);
            double tip = ChakraFinial(x, apex - 0.1, TopM - apex + 0.1);
            if (x.Stats != null) x.Stats.Tiers = 2;

            // The four miniature temples on the platform corners, doors facing out of their own side.
            for (int qu = -1; qu <= 1; qu += 2)
            for (int qw = -1; qw <= 1; qw += 2)
                CornerShrine(x, qu * (ph - 0.2), qw * (pw - 0.2), qw > 0 ? 0 : Math.PI);
            return tip;
        }

        /// <summary>One side's tripartite portal on the wall plane d: the central door with its gilt Garuda torana, two
        /// pink-plastered cusped blind arches in carved frames, carved windows near the corners and two hanging lamps.</summary>
        private static void Portal(KdsCtx x, in Affine3 sk, double d, double v0, double v1)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            // The dark carved surround that holds the three openings as one group, with its stepped outer frame (KAT1740 05,
            // elevation east).
            SacredDraw.Panel(m, sk, -2.75, v0 + 0.05, 2.75, v1 - 0.12, d + 0.005, Looks.Ao(KdsLooks.Timber, 0.85f));
            if (lod <= 1)
            {
                ShapeBrush fr = KdsLooks.Timber;
                SacredDraw.Box(m, sk, -2.95, -2.75, v0 + 0.05, v1 - 0.12, d, d + 0.08, fr, BoxFaces.Wall);
                SacredDraw.Box(m, sk, 2.75, 2.95, v0 + 0.05, v1 - 0.12, d, d + 0.08, fr, BoxFaces.Wall);
                SacredDraw.Box(m, sk, -2.95, 2.95, v1 - 0.32, v1 - 0.12, d, d + 0.08, fr, BoxFaces.Wall | BoxFaces.Bottom);
            }
            KdsKit.Door(m, sk, 0, v0 + 0.15, d, 1.1, 1.85, KdsLooks.Timber, lod, true, v1 - 0.34);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                BlindArch(m, sk, sgn * 1.75, v0 + 0.2, d, 0.95, 1.9, lod);
                KdsKit.BlackWindow(m, sk, sgn * 3.55, v0 + 1.55, d, 0.42, 0.5, lod, 0.08, 2);
                if (lod == 0)
                {
                    // A carved wooden lantern hanging on its bracket beside the portal.
                    double lu = sgn * 3.55;
                    Shapes.Bar(m, sk, KdsLooks.Timber, lu, v0 + 2.25, d, lu, v0 + 2.25, d + 0.32, 0.025, 4);
                    SacredDraw.Box(m, sk, lu - 0.16, lu + 0.16, v0 + 1.8, v0 + 2.2, d + 0.2, d + 0.48, KdsLooks.Timber, BoxFaces.All);
                    SacredDraw.Panel(m, sk, lu - 0.1, v0 + 1.86, lu + 0.1, v0 + 2.14, d + 0.485, Looks.Glow);
                }
            }
        }

        /// <summary>The row of small carved beast heads that hang under the cornice on every side (KAT1740 05, elevation east).</summary>
        private static void BeastHeads(KdsCtx x, double a, double v, double h)
        {
            ShapeBrush b = Looks.Of(MeshColor.FromHex(0x4A3326), MaterialChannel.WoodCarved);
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                int n = (int)(2 * a / 0.36);
                for (int i = 0; i < n; i++)
                {
                    double u = -a + 2 * a * (i + 0.5) / n;
                    SacredDraw.Box(x.M, sk, u - 0.07, u + 0.07, v, v + h, a - 0.1, a + 0.06, b, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                }
            }
        }

        /// <summary>A blind door: a pink plastered panel under a cusped (multi-lobed) arch, in a carved dark frame with ears.</summary>
        public static void BlindArch(MeshData m, in Affine3 sk, double u, double v, double d, double w, double h, int lod)
        {
            double hw = 0.5 * w, archH = 0.42 * w, body = h - archH;
            SacredDraw.Panel(m, sk, u - hw, v, u + hw, v + body, d + 0.02, KdsLooks.PinkArch);
            // The arch: a cusped half-ellipse polygon in the wall plane (x = u, z = −v), pink, framed.
            int lobes = lod == 0 ? 5 : 3, seg = lod == 0 ? 20 : 8;
            double[] px = ShapeScratch.U, pz = ShapeScratch.W;
            int n = 0;
            px[n] = -hw;
            pz[n++] = 0;
            for (int i = 0; i <= seg; i++)
            {
                double a = Math.PI * i / seg, r = 1 - 0.1 * Math.Abs(Math.Sin(lobes * a));
                px[n] = -hw * Math.Cos(a) * r;
                pz[n++] = -archH * Math.Sin(a) * r - 0.1 * archH * Math.Pow(Math.Sin(a), 10);
            }
            px[n] = hw;
            pz[n++] = 0;
            Affine3 f = SacredDraw.Basis(sk, u, v + body, d + 0.02, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, f, KdsLooks.PinkArch, px, pz, n, 0.01, 0, 0, BevelStyle.Chamfer, true, false);
            if (lod >= 2) return;
            // The carved frame: jambs, a lintel with ears over the arch, a sill.
            ShapeBrush fr = KdsLooks.Timber;
            SacredDraw.Box(m, sk, u - hw - 0.12, u - hw, v, v + h, d, d + 0.09, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u + hw, u + hw + 0.12, v, v + h, d, d + 0.09, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u - hw - 0.3, u + hw + 0.3, v + h, v + h + 0.14, d, d + 0.11, fr, BoxFaces.Wall | BoxFaces.Bottom);
            SacredDraw.Box(m, sk, u - hw - 0.22, u + hw + 0.22, v - 0.06, v + 0.06, d, d + 0.11, fr, BoxFaces.Wall);
        }

        /// <summary>The gilt finial with Vishnu's wheel: a kalasha finial with a gilt disc (chakra) on a short staff.</summary>
        private static double ChakraFinial(KdsCtx x, double v0, double h)
        {
            double tip = KdsKit.GiltFinial(x, v0, 0.78 * h, 0.38);
            if (x.Lod >= 2) return v0 + h;
            double cv = v0 + 0.78 * h + 0.28 * h * 0.5;
            Shapes.Bar(x.M, x.K, KdsLooks.Gilt, 0, tip - 0.05, 0, 0, v0 + h - 0.3, 0, 0.03, 5);
            Affine3 disc = SacredDraw.Basis(x.K, 0, v0 + h - 0.26, 0, 1, 0, 0, 0, 0, 1);
            Shapes.Cylinder(x.M, disc, KdsLooks.Gilt, 0.26, 0.04, x.Lod == 0 ? 14 : 8, 0.01, 1, true, true);
            if (x.Lod == 0) Shapes.Torus(x.M, disc * Affine3.Translation(0, 0.02, 0), Looks.GiltHi, 0.2, 0.025, 12, 4);
            return Math.Max(cv, v0 + h);
        }

        /// <summary>A miniature two-roof temple on a platform corner at frame (u, w) (its shrine window facing yaw rad in the
        /// temple frame), after the KAT1740 east elevation: the 1.4 m corner block of the platform, a 1.4 m brick pedestal
        /// with a carved cornice, a small cella with a carved window on each face, a first roof broader than the pedestal
        /// (3.2 m), a small upper storey and roof (2.0 m), both with deep red valances, and a gilt finial (top 6.3 m).</summary>
        private static void CornerShrine(KdsCtx x, double u, double w, double rad)
        {
            Affine3 k = x.K;
            KitFrame kf = x.Kf;
            x.K = SacredDraw.Turn(k, u, 0, w, rad);
            x.Kf = new KitFrame(0, 0, 0, 1, 0);
            double ox, oy, oz;
            kf.ToWorld(u, 0, w, out ox, out oy, out oz);
            x.Kf = KitFrame.FromYaw(ox, kf.OY, oz, x.F.YawDeg + rad * 180 / Math.PI);
            MeshData m = x.M;
            int was = x.Coarser(), lod = x.Lod;
            KdsKit.Stage(x, 1.5, 1.5, 0, 1.45, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.StonePlinth, 0.12, 0.2, GenColliders.Brick);
            SacredParts.BandedWall(m, x.K, 0.75, 0.75, 1.45, 2.85, KdsLooks.Brick, KdsLooks.Timber, lod, true, false);
            x.Solid(-0.75, 0.75, -0.75, 0.75, 1.45, 3.8, GenColliders.Brick);
            KdsKit.Cornice(x, 0.75, 0.75, 2.85, 3.08, 0.12, KdsLooks.Timber);
            SacredParts.BandedWall(m, x.K, 0.56, 0.56, 3.08, 3.85, KdsLooks.Brick, KdsLooks.Timber, lod, false, false);
            for (int s = 0; s < 4; s++)
                KdsKit.BlackWindow(m, KdsKit.Side(x.K, s), 0, 3.45, 0.56, 0.34, 0.42, lod, 0.06, 1);
            KdsKit.Roof(x, 1.6, 1.6, 3.85, 0.5, 0.5, 4.6, 0.56, 0.56, 3.9, KdsLooks.TileOld, 0.36);
            SacredParts.BandedWall(m, x.K, 0.46, 0.46, 4.5, 5.05, KdsLooks.Brick, KdsLooks.Timber, lod, false, false);
            KdsKit.Roof(x, 1.0, 1.0, 5.05, 0.12, 0.12, 5.75, 0.46, 0.46, 5.1, KdsLooks.TileOld, 0.3);
            KdsKit.GiltFinial(x, 5.7, 0.6, 0.14);
            x.Restore(was);
            x.K = k;
            x.Kf = kf;
        }

        /// <summary>
        /// Pratap Malla's column (1670): a stepped stone base, a tall stone shaft square below and octagonal above, a dark
        /// bronze lotus capital and the gilt group of the king seated with his queens and sons, hands joined, under a rearing
        /// serpent hood on a curved stem; it faces the palace (the frame's +w).
        /// </summary>
        public static double Column(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            ShapeBrush stone = Looks.Of(MeshColor.FromHex(0xB7B0A2), MaterialChannel.Stone);
            if (lod >= 3)
            {
                SacredDraw.Box(m, x.K, -0.8, 0.8, 0, 0.6, -0.8, 0.8, stone, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, x.K, -0.3, 0.3, 0.6, 7.4, -0.3, 0.3, stone, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                SacredDraw.Box(m, x.K, -0.55, 0.55, 7.4, ColumnTopM, -0.5, 0.5, KdsLooks.Gilt, BoxFaces.All & ~BoxFaces.Bottom);
                return ColumnTopM;
            }
            KdsKit.Stage(x, 1.0, 1.0, 0, 0.35, 0.75, 0.75, 0, stone, stone, 0.08, 0.15, GenColliders.Stone);
            KdsKit.Stage(x, 0.75, 0.75, 0.35, 0.7, 0.45, 0.45, 0, stone, stone, 0.08, 0, GenColliders.Stone);
            SacredDraw.Box(m, x.K, -0.45, 0.45, 0.7, 1.05, -0.45, 0.45, stone, BoxFaces.All & ~BoxFaces.Bottom);
            // The shaft: square to 2.6 m, then octagonal (a lathe with eight sides) to 7.0 m.
            SacredDraw.Box(m, x.K, -0.3, 0.3, 1.05, 2.6, -0.3, 0.3, stone, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            var shaft = new Profile2(6);
            shaft.Add(0, 0, true).Add(0.32, 0, true).Add(0.29, 4.4, true).Add(0, 4.4, true);
            Shapes.Lathe(m, SacredDraw.At(x.K, 0, 2.6, 0) * Affine3.RotationY(Math.PI / 8), stone, shaft, 8);
            // Dark bronze lotus capital.
            var lotus = new Profile2(10);
            lotus.Add(0, 0, true).Add(0.28, 0, true).Add(0.48, 0.12).Add(0.55, 0.3).Add(0.45, 0.42).Add(0.5, 0.5, true).Add(0, 0.5, true);
            Shapes.Lathe(m, SacredDraw.At(x.K, 0, 7.0, 0), KdsLooks.BronzeDark, lotus, KdsKit.Seg(14, lod));
            double v = 7.5;
            // The gilt royal group: the king seated at the centre, two queens and the sons, on a gilt seat slab.
            ShapeBrush g = KdsLooks.Gilt;
            SacredDraw.Box(m, x.K, -0.55, 0.55, v, v + 0.12, -0.42, 0.42, g, BoxFaces.All & ~BoxFaces.Bottom);
            // The king sits larger than his family (KAT1791 00).
            Seated(m, x.K, 0, v + 0.12, 0.05, 1.0, lod);
            if (lod <= 1)
            {
                Seated(m, x.K, -0.36, v + 0.12, 0.0, 0.6, lod);
                Seated(m, x.K, 0.36, v + 0.12, 0.0, 0.6, lod);
                Seated(m, x.K, -0.18, v + 0.12, 0.3, 0.38, lod);
                Seated(m, x.K, 0.18, v + 0.12, 0.3, 0.38, lod);
            }
            // The serpent hood: a curved stem rising behind and a fan of hooded heads over the group.
            var stem = new Path3(4);
            stem.Add(0, v + 0.1, -0.38).Add(0, v + 0.8, -0.42).Add(0, v + 1.25, -0.25);
            Shapes.Tube(m, x.K, g, stem, 0.06, lod == 0 ? 6 : 4, true, false, default, 0.05);
            int heads = lod == 0 ? 5 : 3;
            for (int i = 0; i < heads; i++)
            {
                double a = (i - 0.5 * (heads - 1)) * 0.3;
                Affine3 hx = SacredDraw.At(x.K, 0.42 * Math.Sin(a), v + 1.25 + 0.12 * Math.Cos(a), -0.2 + 0.1 * Math.Cos(a));
                Shapes.Ellipsoid(m, hx, g, 0.13, 0.2, 0.06, lod == 0 ? 6 : 5, false);
            }
            x.Cylinder(0, 0, 0, ColumnTopM, 0.45);
            return ColumnTopM;
        }

        /// <summary>A small gilt seated figure (folded legs, body, joined hands, head and crown) of height h at (u, v, w), facing +w.</summary>
        private static void Seated(MeshData m, in Affine3 k, double u, double v, double w, double h, int lod)
        {
            ShapeBrush g = KdsLooks.Gilt;
            // Small and 7.5 m up: six sides read round from the square.
            int seg = lod == 0 ? 6 : 5;
            Shapes.Ellipsoid(m, SacredDraw.At(k, u, v + 0.1 * h, w), g, 0.26 * h, 0.1 * h, 0.2 * h, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(k, u, v + 0.38 * h, w - 0.02 * h), g, 0.16 * h, 0.23 * h, 0.12 * h, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(k, u, v + 0.42 * h, w + 0.12 * h), Looks.GiltHi, 0.05 * h, 0.08 * h, 0.05 * h, 5, false);
            Shapes.Sphere(m, SacredDraw.At(k, u, v + 0.7 * h, w), g, 0.1 * h, seg);
            Shapes.Cone(m, SacredDraw.At(k, u, v + 0.78 * h, w), Looks.GiltHi, 0.08 * h, 0.22 * h, 6, 0, 0, false);
        }
    }
}
