using System;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The Gorakhnath shrine under the great pipal in the square's north-east corner (judge round 1; DANAM KAT1551 site
    /// and floor plans 2023, photos from W and SW; Pira Pati KAT1552): a stone shrine 2.5 × 1.8 m built into the roots
    /// of the Bodhi pipal (Ficus religiosa), its west face carved with a central arched niche and six relief niches,
    /// hung with a yellow cloth valance, on a low stone base; the pipal itself, about 19 m tall with an 18 m crown, on its
    /// 0.5 m brick chautara behind the shrine; north of it the long low Pira Pati rest house (a brick base, a timber front
    /// of doors, lattice windows and open bays facing west, a hipped tile roof hung with a yellow valance). The frame
    /// stands on the shrine, its door (+w) west.
    /// </summary>
    internal static class Gorakhnath
    {
        /// <summary>The pipal's crown top over the shrine's ground (m).</summary>
        public const float TopM = 19.5f;

        private static readonly ShapeBrush ShrineStone = Looks.Of(MeshColor.FromHex(0x7D7870), MaterialChannel.Stone);
        private static readonly ShapeBrush ReliefStone = Looks.Of(MeshColor.FromHex(0x9A948A), MaterialChannel.Stone);
        private static readonly ShapeBrush Cloth = Looks.Of(MeshColor.FromHex(0xE3B22E), MaterialChannel.Fabric);
        private static readonly ShapeBrush Root = Looks.Of(MeshColor.FromHex(0x8E887E), MaterialChannel.Bark);

        // The trunk's foot and the chautara round it (frame metres): 1.5 m east of the shrine's centre, behind its back.
        private const double TrunkU = -0.4, TrunkW = -1.95;

        public static double Build(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            // The chautara: a 0.5 m brick platform with a stone coping round the trunk.
            if (lod >= 3)
            {
                SacredDraw.Box(m, x.K, -2.7, 2.1, 0, 0.5, -4.4, -0.9, KdsLooks.BrickPlinth, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, x.K, -1.25, 1.25, 0, 2.5, -0.9, 0.9, ShrineStone, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, x.K, -9.2, -2.8, 0, 2.6, -2.9, 1.1, KdsLooks.Brick, BoxFaces.All & ~BoxFaces.Bottom);
                Tree(x, 3);
                return TopM;
            }
            KdsKit.StageAt(x, -0.3, -2.65, 2.4, 1.75, 0, 0.5, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.08, 0.15, GenColliders.Brick);
            Shrine(x);
            Roots(x);
            Tree(x, lod);
            Trunk(x);
            x.Cylinder(TrunkU, TrunkW, 0.5, 6.0, 1.1);
            PiraPati(x);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = 1;
                x.Stats.PlinthTopM = 0.3f;
                x.Stats.SanctumHalfU = 1.25f;
                x.Stats.SanctumHalfW = 0.9f;
            }
            return TopM;
        }

        /// <summary>The stone shrine: base slab, the block, the central niche and six relief niches on its west face, the
        /// yellow cloth valance along its top.</summary>
        private static void Shrine(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            const double hu = 1.25, hw = 0.9, b = 0.3, top = 2.5;
            SacredDraw.Box(m, x.K, -hu - 0.2, hu + 0.2, 0, b, -hw, hw + 0.25, KdsLooks.StonePlinth, BoxFaces.All & ~BoxFaces.Bottom);
            x.Floor(-hu - 0.2, hu + 0.2, -hw, hw + 0.25, 0, b, GenColliders.Stone);
            Shapes.RoundedBox(m, SacredDraw.At(x.K, 0, 0.5 * (b + top), 0), ShrineStone, 2 * hu, top - b, 2 * hw, 0.08, lod == 0 ? 1 : 0);
            x.Solid(-hu, hu, -hw, hw, b, top, GenColliders.Stone);
            Affine3 front = KdsKit.Side(x.K, 0);
            // The central arched niche (Gorakhnath's footprints in it) in a carved frame.
            KdsKit.ArchPanel(m, front, 0, b + 0.15, hw + 0.005, 0.46, 1.3, 0.04, ReliefStone, Math.Max(1, lod), false);
            KdsKit.ArchPanel(m, front, 0, b + 0.2, hw + 0.045, 0.36, 1.15, 0.01, Looks.Recess, Math.Max(1, lod), false);
            if (lod <= 1)
            {
                // Six relief niches, three a side, each a small arched frame with a figure standing proud in it.
                for (int sg = -1; sg <= 1; sg += 2)
                for (int r = 0; r < 3; r++)
                {
                    double u = sg * 0.86, v0 = b + 0.1 + 0.66 * r;
                    KdsKit.ArchPanel(m, front, u, v0, hw + 0.005, 0.2, 0.56, 0.03, ReliefStone, 2, false);
                    if (lod == 0)
                    {
                        KdsKit.ArchPanel(m, front, u, v0 + 0.05, hw + 0.04, 0.14, 0.44, 0.005, Looks.Ao(ShrineStone, 0.6f), 2, false);
                        Shapes.Ellipsoid(m, SacredDraw.At(front, u, v0 + 0.22, hw + 0.05), ReliefStone, 0.08, 0.16, 0.04, 2);
                        Shapes.Ellipsoid(m, SacredDraw.At(front, u, v0 + 0.41, hw + 0.05), ReliefStone, 0.045, 0.05, 0.035, 1);
                    }
                }
            }
            // The yellow cloth valance hung along the top of the three open faces.
            for (int s = 0; s < 4; s++)
            {
                if (s == 2) continue;
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = s % 2 == 0 ? hu : hw, d = s % 2 == 0 ? hw : hu;
                ClothBand(x, sk, -a - 0.05, a + 0.05, d + 0.06, top + 0.06, 0.36, lod);
            }
        }

        /// <summary>A pleated cloth band on a side frame's plane d from u0 to u1, its top at vTop, hanging drop m with a
        /// scalloped hem (LOD0 pleats every 0.3 m, LOD1 every 0.8 m, LOD2 one strip).</summary>
        private static void ClothBand(KdsCtx x, in Affine3 sk, double u0, double u1, double d, double vTop, double drop, int lod)
        {
            MeshData m = x.M;
            double pitch = lod == 0 ? 0.3 : lod == 1 ? 0.8 : 10;
            int n = Math.Max(1, (int)Math.Round((u1 - u0) / pitch));
            for (int i = 0; i < n; i++)
            {
                double a0 = u0 + (u1 - u0) * i / n, a1 = u0 + (u1 - u0) * (i + 1) / n, am = 0.5 * (a0 + a1);
                ShapeBrush c = i % 2 == 0 ? Cloth : Looks.Ao(Cloth, 0.86f);
                if (lod == 0)
                {
                    int p0 = SacredDraw.V(m, sk, a0, vTop, d, 0, 0, 1, c), p1 = SacredDraw.V(m, sk, a1, vTop, d, 0, 0, 1, c);
                    int q1 = SacredDraw.V(m, sk, a1, vTop - 0.75 * drop, d, 0, 0, 1, Looks.Ao(c, 0.85f));
                    int qm = SacredDraw.V(m, sk, am, vTop - drop, d + 0.02, 0, 0, 1, Looks.Ao(c, 0.85f));
                    int q0 = SacredDraw.V(m, sk, a0, vTop - 0.75 * drop, d, 0, 0, 1, Looks.Ao(c, 0.85f));
                    int pm = SacredDraw.V(m, sk, am, vTop, d + 0.02, 0, 0, 1, c);
                    SacredDraw.Quad(m, p0, pm, qm, q0);
                    SacredDraw.Quad(m, pm, p1, q1, qm);
                }
                else
                {
                    SacredDraw.Panel(m, sk, a0, vTop - 0.8 * drop, a1, vTop, d, c);
                }
            }
        }

        /// <summary>The pipal's roots spilling from the trunk over the shrine's top and down its back corners.</summary>
        private static void Roots(KdsCtx x)
        {
            if (x.Lod >= 2) return;
            MeshData m = x.M;
            for (int i = 0; i < 4; i++)
            {
                double u = -1.0 + 0.66 * i, r = 0.11 - 0.015 * i;
                double tu = TrunkU + 0.35 * (u - TrunkU);
                Shapes.Bar(m, x.K, Root, tu, 2.4, TrunkW + 0.6, u * 0.9, 2.62, -0.2, r, x.Lod == 0 ? 5 : 4);
                Shapes.Bar(m, x.K, Root, u * 0.9, 2.62, -0.2, u, 2.3 - 0.2 * (i % 2), 0.75, r * 0.8, x.Lod == 0 ? 5 : 4);
            }
            // The fluted root ridges where the trunk meets the chautara.
            for (int i = 0; i < 5; i++)
            {
                double a = 2 * Math.PI * (i + 0.3) / 5, cu = TrunkU + 1.05 * Math.Cos(a), cw = TrunkW + 1.05 * Math.Sin(a);
                Shapes.Bar(m, x.K, Root, TrunkU + 0.7 * Math.Cos(a), 1.6, TrunkW + 0.7 * Math.Sin(a), cu, 0.5, cw, 0.16, 4);
            }
        }

        /// <summary>The old pipal's massive pale fluted trunk over the kit tree's: flared at the root, about 2 m across at
        /// chest height, narrowing to the fork.</summary>
        private static void Trunk(KdsCtx x)
        {
            var p = new Profile2(8);
            p.Add(0, 0, true).Add(1.45, 0, true).Add(1.1, 0.5).Add(0.95, 1.5).Add(0.85, 3.5).Add(0.7, 5.6).Add(0, 5.6, true);
            Shapes.Lathe(x.M, SacredDraw.At(x.K, TrunkU, 0.5, TrunkW), Root, p, x.Lod == 0 ? 9 : x.Lod == 1 ? 6 : 4);
        }

        /// <summary>The pipal (the nature kit's Ficus religiosa, autumn leaves) scaled to the corner's tree: 19 m tall,
        /// an 18 m crown, standing on the chautara.</summary>
        private static void Tree(KdsCtx x, int lod)
        {
            var t = new MeshData();
            FloraMesher.Build(TreeSpecies.Pipal, Math.Min(3, lod), 10, t, true);
            Affine3 xf = Affine3.Scaling(18.0, TopM - 0.5, 18.0).Then(SacredDraw.At(x.K, TrunkU, 0.5, TrunkW));
            Shapes.CopyTransformed(x.M, t, xf);
        }

        /// <summary>
        /// Pira Pati (KAT1552): the long low rest house north of the shrine, its timber front west: a brick base course
        /// under a front of posts with doors, lattice windows and open bays, brick side and back walls, a hipped tile roof
        /// with a yellow cloth valance.
        /// </summary>
        private static void PiraPati(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            KitFrame keep = x.Shift(-6.0, -0.9);
            try
            {
                const double hu = 3.2, hw = 2.0, b = 0.35, wallTop = 2.35;
                KdsKit.Stage(x, hu + 0.1, hw + 0.1, 0, b, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.06, 0.12, GenColliders.Brick);
                // Brick back and side walls; the front a timber frame on a brick dado.
                SacredDraw.Box(m, x.K, -hu, hu, b, wallTop, -hw, -hw + 0.3, KdsLooks.Brick, BoxFaces.All & ~BoxFaces.Bottom);
                for (int sg = -1; sg <= 1; sg += 2)
                    SacredDraw.Box(m, x.K, sg * hu - 0.15, sg * hu + 0.15, b, wallTop, -hw + 0.3, hw, KdsLooks.Brick, BoxFaces.All & ~BoxFaces.Bottom);
                x.Solid(-hu, hu, -hw, -hw + 0.3, b, wallTop, GenColliders.Brick);
                x.Solid(-hu - 0.15, -hu + 0.15, -hw, hw, b, wallTop, GenColliders.Brick);
                x.Solid(hu - 0.15, hu + 0.15, -hw, hw, b, wallTop, GenColliders.Brick);
                Affine3 front = KdsKit.Side(x.K, 0);
                const int bays = 5;
                for (int i = 0; i < bays; i++)
                {
                    double u0 = -hu + 0.15 + (2 * hu - 0.3) * i / bays, u1 = -hu + 0.15 + (2 * hu - 0.3) * (i + 1) / bays;
                    SacredDraw.Box(m, x.K, u0, u1, b, b + 0.35, hw - 0.12, hw, KdsLooks.BrickRed, BoxFaces.Front | BoxFaces.Top);
                    // Bay 0 a door, 1 and 4 lattice windows, 2 and 3 open onto the fire-pit room.
                    if (i == 0)
                    {
                        SacredDraw.Panel(m, front, u0 + 0.12, b + 0.35, u1 - 0.12, 2.1, hw - 0.05, KdsLooks.TimberRed);
                    }
                    else if (i == 1 || i == 4)
                    {
                        SacredDraw.Panel(m, front, u0 + 0.1, b + 0.35, u1 - 0.1, 1.1, hw - 0.06, KdsLooks.TimberRed);
                        SacredDraw.Panel(m, front, u0 + 0.1, 1.1, u1 - 0.1, 2.05, hw - 0.06, Looks.Recess);
                        if (lod == 0)
                            for (int q = 1; q <= 3; q++)
                                SacredDraw.Panel(m, front, u0 + (u1 - u0) * q / 4 - 0.02, 1.1, u0 + (u1 - u0) * q / 4 + 0.02, 2.05, hw - 0.04, KdsLooks.Timber);
                    }
                    else
                    {
                        SacredDraw.Panel(m, front, u0 + 0.05, b + 0.35, u1 - 0.05, 2.1, -hw + 0.31, Looks.Recess);
                    }
                    if (lod <= 1) SacredDraw.Box(m, x.K, u0 - 0.06, u0 + 0.06, b, 2.15, hw - 0.14, hw, KdsLooks.Timber, BoxFaces.Wall);
                    if (i == bays - 1 && lod <= 1) SacredDraw.Box(m, x.K, u1 - 0.06, u1 + 0.06, b, 2.15, hw - 0.14, hw, KdsLooks.Timber, BoxFaces.Wall);
                }
                SacredDraw.Box(m, x.K, -hu, hu, 2.1, wallTop, hw - 0.16, hw, KdsLooks.Timber, BoxFaces.Wall | BoxFaces.Bottom);
                x.Wall(-hu, hw - 0.08, -hu + (2 * hu - 0.3) * 2 / bays, hw - 0.08, b, 2.1, 0.08);
                x.Wall(-hu + 0.15 + (2 * hu - 0.3) * 4 / bays, hw - 0.08, hu, hw - 0.08, b, 2.1, 0.08);
                RoofSpec r = KdsKit.Roof(x, hu + 0.55, hw + 0.6, 2.75, 2.2, 0.12, 3.95, hu, hw, wallTop, KdsLooks.TileOld, 0);
                KdsKit.Valance(x, r, 0.28, Cloth);
            }
            finally
            {
                x.Unshift(keep);
            }
        }
    }
}
