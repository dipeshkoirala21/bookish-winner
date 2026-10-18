using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// Kal Bhairav (kds-temples.md T17; DANAM KAT1770, KVPT north elevation 8.12 m) on its true footprint (OSM w908861416),
    /// facing north: the grey stone shrine wall 4.8 m wide, blackened by oil and soot, with its side wings holding bronze
    /// bells; the ogee-arched niche (3.9 × 5.2 m) whose carved rim flames round the painted six-armed Bhairav (blue-black
    /// body, white eyes, red mouth, gilt-yellow crown of small skulls, a flame aureole; a general depiction) standing on a
    /// reclining figure; sun and moon discs; the small valanced eave at 5.3 m, the cornice platform at 6.1 m and the three
    /// ribbed bell domes (8.12 m in the middle, 7.6 m at the sides); the low front platform with the white-and-red lions,
    /// pennant staffs and the iron railing. Also the Sweta (Seto) Bhairav screen (T27): the closed carved lattice screen
    /// with its brass lamps on the palace front.
    /// </summary>
    internal static class KalBhairav
    {
        public const float TopM = 8.12f;
        public const float SwetaTopM = 4.5f;

        public static double Build(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double hu = 2.4, hw = x.PlanHw > 0.5 ? Math.Max(1.05, Math.Min(1.6, x.PlanHw)) : 1.25;
            const double b0 = 0.3, wallTop = 6.0;
            // The domes are weathered grey stone (DANAM KAT1770 13), paler than the soot-blackened wall.
            ShapeBrush stone = KdsLooks.StoneGrey, dark = Looks.Of(MeshColor.FromHex(0x8A877F), MaterialChannel.Stone);
            if (lod >= 3)
            {
                SacredDraw.Box(m, x.K, -hu, hu, 0, wallTop, -hw, hw, stone, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Panel(m, KdsKit.Side(x.K, 0), -1.6, 0.6, 1.6, 4.8, hw + 0.01, KdsLooks.BhairavBlue);
                for (int q = -1; q <= 1; q++) Shapes.Cone(m, SacredDraw.At(x.K, q * 1.55, wallTop, 0), dark, q == 0 ? 0.6 : 0.45, q == 0 ? 2.1 : 1.6, 4, 0, 0, false);
                return TopM;
            }
            // The low front platform (shortened out of the lane's corridor).
            double front = 2.0;
            while (front > 0.8 && Math.Min(x.CorridorDistance(-3.6, hw + front), x.CorridorDistance(3.6, hw + front)) < SacredClearance.MarginM) front -= 0.2;
            KdsKit.StageAt(x, 0, hw + 0.5 * (front - 0.3), 3.6, 0.5 * (front + 0.3), 0, b0, KdsLooks.StonePlinth, KdsLooks.Flag, 0.08, 0.2, GenColliders.Stone);
            // The stone block (from below the ground at its plain back), soot-blackened toward the foot.
            int first = m.VertexCount;
            if (lod <= 1) Shapes.RoundedBox(m, SacredDraw.At(x.K, 0, 0.5 * (wallTop - 0.2), 0), stone, 2 * hu, wallTop + 0.2, 2 * hw, 0.04, 1);
            else SacredDraw.Box(m, x.K, -hu, hu, -0.2, wallTop, -hw, hw, stone, BoxFaces.All & ~BoxFaces.Bottom);
            // The side wings.
            for (int sg = -1; sg <= 1; sg += 2)
            {
                double u0 = sg * hu, u1 = sg * (hu + 1.0);
                SacredDraw.Box(m, x.K, Math.Min(u0, u1), Math.Max(u0, u1), -0.2, 2.2, -hw + 0.25, hw - 0.25, stone, BoxFaces.All & ~BoxFaces.Bottom);
                x.Solid(Math.Min(u0, u1), Math.Max(u0, u1), -hw + 0.25, hw - 0.25, 0, 2.2, GenColliders.Stone);
            }
            ShapeColor.VerticalGradient(m, first, m.VertexCount - first, F0(x), F0(x) + wallTop, MeshColor.FromHex(0x45423E), MeshColor.FromHex(0x8C877D));
            if (lod == 0) ShapeColor.JitterByPosition(m, first, m.VertexCount - first, 0.08f, 0x4B414Cu, 0.45);
            x.Solid(-hu, hu, -hw, hw, 0, wallTop, GenColliders.Stone);
            Affine3 nf = KdsKit.Side(x.K, 0);
            if (lod == 0)
            {
                // Stone courses on the plain back and sides.
                for (int s = 1; s < 4; s++)
                {
                    Affine3 sk = KdsKit.Side(x.K, s);
                    double a = KdsKit.Along(s, hu, hw), d = KdsKit.Depth(s, hu, hw);
                    for (double v = b0 + 0.45; v < wallTop - 0.3; v += 0.45)
                        SacredDraw.Box(m, sk, -a + 0.04, a - 0.04, v - 0.015, v + 0.015, d, d + 0.006, KdsLooks.StoneSoot, BoxFaces.Front);
                }
                // Small carved steles on the wing fronts.
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    for (int q = 0; q < 2; q++)
                    {
                        double u = sg * (hu + 0.27 + 0.47 * q);
                        KdsKit.FlameArch(m, nf, u, b0 + 0.35, hw - 0.25, 0.16, 0.85, 0.04, 0.05, KdsLooks.StoneSoot, lod, false, false);
                        KdsKit.ArchPanel(m, nf, u, b0 + 0.35, hw - 0.25, 0.16, 0.85, 0.03, Looks.Of(MeshColor.FromHex(0x7A3A2A), MaterialChannel.Paint), lod, false);
                    }
                }
            }
            // The niche: the flame-carved stone rim, the painted flame field and the figure.
            const double nw = 1.75, nv0 = b0 + 0.15, nh = 5.2;
            // The rim is black carved stone (serpents and the monster face); the field behind the figure is painted sky
            // blue with an orange-red aureole round the body (KAT1770 14).
            ShapeBrush rim = Looks.Of(MeshColor.FromHex(0x3E3B38), MaterialChannel.Stone);
            KdsKit.FlameArch(m, nf, 0, nv0, hw, nw, nh, 0.26, 0.12, rim, lod, true, true);
            KdsKit.ArchPanel(m, nf, 0, nv0, hw, nw, nh, 0.02, Looks.Of(MeshColor.FromHex(0x86B4D6), MaterialChannel.Paint), lod, true);
            if (lod <= 1) KdsKit.ArchPanel(m, nf, 0, nv0 + 0.2, hw + 0.025, 1.2, 3.4, 0.01, KdsLooks.FlameOrange, lod, false);
            if (lod <= 1)
            {
                // A gilt inner border and the monster face at the crown.
                KdsKit.FlameArch(m, nf, 0, nv0, hw + 0.02, nw - 0.12, nh - 0.1, 0.1, 0.03, KdsLooks.Gilt, lod, false, true);
                Shapes.Ellipsoid(m, SacredDraw.At(nf, 0, nv0 + nh + 0.05, hw + 0.08), rim, 0.36, 0.3, 0.14, 8, false);
                if (lod == 0)
                {
                    for (int sg = -1; sg <= 1; sg += 2)
                        Shapes.Sphere(m, SacredDraw.At(nf, sg * 0.11, nv0 + nh + 0.1, hw + 0.2), KdsLooks.PaintWhite, 0.06, 5);
                    Shapes.Ellipsoid(m, SacredDraw.At(nf, 0, nv0 + nh - 0.05, hw + 0.2), KdsLooks.PaintRed, 0.14, 0.05, 0.05, 6, false);
                }
                // Sun and moon: carved stone faces in discs above the shoulders.
                ShapeBrush disc = Looks.Of(MeshColor.FromHex(0x6E6A63), MaterialChannel.Stone);
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    Shapes.Cylinder(m, SacredDraw.Basis(nf, sg * 2.0, 5.3, hw, 1, 0, 0, 0, 0, 1), disc, 0.3, 0.06, lod == 0 ? 14 : 8);
                    if (lod == 0) Shapes.Ellipsoid(m, SacredDraw.At(nf, sg * 2.0, 5.3, hw + 0.07), Looks.Ao(disc, 0.85f), 0.17, 0.2, 0.06, 6, false);
                }
            }
            BhairavFigure(m, nf, 0, nv0 + 0.1, hw + 0.05, 3.7, lod);
            // The valanced eave, the cornice platform and the three ribbed domes.
            KdsKit.Roof(x, hu + 0.4, hw + 0.42, 5.25, hu + 0.03, hw + 0.03, 5.72, hu, hw, 5.45, KdsLooks.TileOld, 0.32);
            SacredDraw.Box(m, x.K, -hu - 0.12, hu + 0.12, 5.95, 6.2, -hw - 0.12, hw + 0.12, KdsLooks.StonePale, BoxFaces.All);
            // A second red valance hangs from the cornice slab under the domes, in front of the roof's upper slope
            // (KAT1770 05, 08).
            if (lod <= 1)
            {
                var slab = new RoofSpec
                {
                    EaveU = hu + 0.12, EaveW = hw + 0.12, EaveV = 5.95, TopU = hu + 0.12, TopW = hw + 0.12, TopV = 5.95, Clear = x.Clear, GroundY = x.F.GroundY,
                };
                KdsKit.Valance(x, slab, 0.3);
            }
            KdsKit.RibbedDome(x, 0, 0, 6.2, TopM - 6.2, 0.62, dark);
            int was = x.Coarser();
            for (int sg = -1; sg <= 1; sg += 2) KdsKit.RibbedDome(x, sg * 1.6, 0, 6.2, 1.4, 0.48, dark);
            x.Restore(was);
            // Lions at the north-west and north-east corners of the platform, bells on the wings, pennants and the railing.
            // The lions stand on the platform's front corners, stepped back toward the wall (or left out) where the
            // square's walkway corridor runs close: their heads lean forward over their pedestals.
            for (int sg = -1; sg <= 1; sg += 2)
            {
                double lu = sg * 3.0, lw = hw + Math.Min(1.35, front - 0.45);
                while (lw > hw + 0.45 && LionRoom(x, lu, lw) < SacredClearance.MarginM + 0.1) lw -= 0.15;
                if (LionRoom(x, lu, lw) >= SacredClearance.MarginM + 0.05) KdsKit.Lion(x, lu, lw, 0.5, 1.6, sg, true, 0.32);
            }
            if (lod <= 1)
            {
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    // The bell frames in front of the wings, nudged toward the wall where the walkway runs close.
                    double bu = sg * (hu + 0.5);
                    while (Math.Abs(bu) > hu + 0.15 && x.CorridorDistance(bu + sg * 0.5, hw + 0.3) < SacredClearance.MarginM + 0.1) bu -= sg * 0.1;
                    if (x.CorridorDistance(bu + sg * 0.5, hw + 0.3) >= SacredClearance.MarginM) KdsKit.BellFrame(x, bu, hw + 0.3, b0, 1.75, 0.42);
                    // A pennant staff with its red pennant, where it stands clear of the walkway.
                    double pu = sg * 2.2, pw = hw + front - 0.35;
                    if (Math.Min(x.CorridorDistance(pu, pw), x.CorridorDistance(pu + sg * 0.7, pw)) < SacredClearance.MarginM + 0.1) continue;
                    Shapes.Cylinder(m, SacredDraw.At(x.K, pu, b0, pw), KdsLooks.Timber, 0.03, 3.9, 5);
                    SacredDraw.Tri(m, x.K, pu, b0 + 3.85, pw, pu + sg * 0.7, b0 + 3.6, pw, pu, b0 + 3.3, pw, 0, 0, 1, KdsLooks.ClothRed);
                    SacredDraw.Tri(m, x.K, pu, b0 + 3.85, pw, pu, b0 + 3.3, pw, pu + sg * 0.7, b0 + 3.6, pw, 0, 0, -1, KdsLooks.ClothRed);
                }
                ShapeBrush iron = Looks.Of(MeshColor.FromHex(0x2E3440), MaterialChannel.Metal);
                double rw = hw + front - 0.1;
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    double u0 = sg * 0.8, u1 = sg * 3.4;
                    SacredDraw.Box(m, x.K, Math.Min(u0, u1), Math.Max(u0, u1), b0 + 0.85, b0 + 0.9, rw - 0.02, rw + 0.02, iron, BoxFaces.All);
                    for (int q = 0; q <= 5; q++)
                    {
                        double u = u0 + (u1 - u0) * q / 5;
                        SacredDraw.Box(m, x.K, u - 0.02, u + 0.02, b0, b0 + 0.85, rw - 0.02, rw + 0.02, iron, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                    }
                    x.Wall(u0, rw, u1, rw, b0, b0 + 0.9, 0.03);
                }
            }
            return TopM;
        }

        private static double F0(KdsCtx x)
        {
            return x.F.GroundY;
        }

        /// <summary>The least corridor distance over a platform lion's footprint and its forward-leaning head.</summary>
        static double LionRoom(KdsCtx x, double lu, double lw)
        {
            double d = double.MaxValue;
            for (int a = -1; a <= 1; a++)
                for (int b = -1; b <= 1; b++) d = Math.Min(d, x.CorridorDistance(lu + 0.5 * a, lw + 0.55 * b));
            return d;
        }

        /// <summary>The six-armed Bhairav, h tall, against the niche field at (u, v, w) of the wall frame k, facing +w: a
        /// wide stance on a reclining figure, blue-black body, tiger-skin hip cloth, a garland, a fierce red-mouthed face with
        /// round white eyes, a tall gilt crown with small white skulls and a flame aureole behind the head; the upper arms
        /// hold a sword and a disc, the middle a trident, a skull cup at the chest (general depiction).</summary>
        private static void BhairavFigure(MeshData m, in Affine3 k, double u, double v, double w, double h, int lod)
        {
            ShapeBrush body = KdsLooks.BhairavBlue, gold = KdsLooks.PaintYellow, red = KdsLooks.PaintRed, white = KdsLooks.PaintWhite;
            if (lod >= 2)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(k, u, v + 0.45 * h, w + 0.1), body, 0.36 * h, 0.45 * h, 0.12, 6, false);
                Shapes.Cone(m, SacredDraw.At(k, u, v + 0.86 * h, w + 0.12), gold, 0.12 * h, 0.2 * h, 5, 0, 0, false);
                return;
            }
            int seg = lod == 0 ? 10 : 6, limb = lod == 0 ? 6 : 4;
            // The figure fills its arch as the real relief does: a broad, heavy body (the frame widened 1.3 times).
            Affine3 b = Affine3.Scaling(1.3, 1, 1).Then(SacredDraw.At(k, u, v, w));
            ShapeBrush skin = Looks.Of(MeshColor.FromHex(0xE8B07A), MaterialChannel.Paint);
            // The flame aureole behind the head.
            KdsKit.FlameArch(m, k, u, v + 0.7 * h, w - 0.03, 0.3 * h, 0.38 * h, 0.07 * h, 0.03, KdsLooks.FlameOrange, lod, true, false);
            // The reclining figure across the base.
            Shapes.Capsule(m, Affine3.RotationZ(0.5 * Math.PI).Then(SacredDraw.At(b, 0.42 * h, 0.07 * h, 0.05)), skin, 0.06 * h, 0.84 * h, limb);
            Shapes.Sphere(m, SacredDraw.At(b, -0.46 * h, 0.08 * h, 0.06), skin, 0.065 * h, limb);
            for (int s = -1; s <= 1; s += 2)
            {
                var p = new Path3(4);
                p.Add(s * 0.08 * h, 0.42 * h, 0.05).Add(s * 0.22 * h, 0.3 * h, 0.1).Add(s * 0.24 * h, 0.12 * h, 0.08);
                Shapes.Tube(m, b, body, p, 0.075 * h, limb, true, false, default, 0.055 * h);
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.25 * h, 0.12 * h, 0.14), body, 0.06 * h, 0.035 * h, 0.08 * h, limb, false);
            }
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.44 * h, 0.08), Looks.Of(MeshColor.FromHex(0xD98B2B), MaterialChannel.Fabric), 0.2 * h, 0.08 * h, 0.1 * h, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.55 * h, 0.08), body, 0.2 * h, 0.13 * h, 0.11 * h, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.68 * h, 0.06), body, 0.23 * h, 0.1 * h, 0.1 * h, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.81 * h, 0.1), body, 0.12 * h, 0.115 * h, 0.1 * h, seg, false);
            double face = 0.1 + 0.085 * h;
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.775 * h, face), red, 0.05 * h, 0.022 * h, 0.03 * h, 6, false);
            for (int s = -1; s <= 1; s += 2)
            {
                Shapes.Sphere(m, SacredDraw.At(b, s * 0.042 * h, 0.83 * h, face - 0.01 * h), white, 0.026 * h, 6);
                Shapes.Sphere(m, SacredDraw.At(b, s * 0.042 * h, 0.83 * h, face + 0.012 * h), Looks.Ink, 0.012 * h, 4);
                Shapes.Ellipsoid(m, Affine3.RotationZ(s * 0.35).Then(SacredDraw.At(b, s * 0.045 * h, 0.865 * h, face - 0.01 * h)), gold, 0.035 * h, 0.008 * h, 0.015 * h, 4,
                                 false);
                if (lod == 0) Shapes.Cone(m, Affine3.RotationX(Math.PI).Then(SacredDraw.At(b, s * 0.03 * h, 0.775 * h, face + 0.02 * h)), white, 0.008 * h, 0.03 * h, 4, 0, 0, false);
            }
            if (lod == 0) Shapes.Sphere(m, SacredDraw.At(b, 0, 0.875 * h, face - 0.015 * h), white, 0.012 * h, 4);
            // The tall gilt crown with its row of small skulls.
            var cp = new Profile2(8);
            cp.Add(0, 0).Add(0.11 * h, 0, true).Add(0.13 * h, 0.05 * h).Add(0.1 * h, 0.1 * h).Add(0.06 * h, 0.15 * h).Add(0.02 * h, 0.18 * h).Add(0, 0.18 * h);
            Shapes.Lathe(m, SacredDraw.At(b, 0, 0.89 * h, 0.08), gold, cp, seg);
            for (int q = -2; q <= 2; q++) Shapes.Sphere(m, SacredDraw.At(b, q * 0.05 * h, 0.93 * h - 0.01 * h * q * q, 0.08 + 0.115 * h), white, 0.022 * h, 5);
            // A marigold garland across the chest.
            var gl = new Path3(8);
            gl.Add(-0.18 * h, 0.72 * h, 0.12).Add(-0.08 * h, 0.55 * h, 0.2).Add(0.08 * h, 0.55 * h, 0.2).Add(0.18 * h, 0.72 * h, 0.12);
            Path3 gs = Curves.CatmullRom(gl, new Path3(16), lod == 0 ? 4 : 2, false);
            if (lod == 0) Shapes.Tube(m, b, Looks.Of(SacredPalette.Marigold, MaterialChannel.Fabric), gs, 0.02 * h, limb, true);
            // Six arms.
            double[] ax = { 0.42, 0.44, 0.36 }, ay = { 0.98, 0.78, 0.58 };
            for (int s = -1; s <= 1; s += 2)
            {
                for (int a = 0; a < 3; a++)
                {
                    var p = new Path3(4);
                    p.Add(s * 0.17 * h, 0.7 * h, 0.08).Add(s * (0.17 + 0.6 * (ax[a] - 0.17)) * h, (0.7 + 0.5 * (ay[a] - 0.7)) * h - 0.03 * h, 0.1).Add(s * ax[a] * h, ay[a] * h, 0.12);
                    Shapes.Tube(m, b, body, p, 0.055 * h, limb, true, false, default, 0.04 * h);
                    Shapes.Sphere(m, SacredDraw.At(b, s * ax[a] * h, ay[a] * h, 0.12), gold, 0.025 * h, 4);
                }
            }
            // Sword (upper right), disc (upper left), trident (middle right) and the skull cup at the chest.
            Shapes.RoundedBox(m, Affine3.RotationZ(-0.35).Then(SacredDraw.At(b, 0.48 * h, 1.12 * h, 0.14)), white, 0.08 * h, 0.3 * h, 0.02, 0.01, 1);
            Shapes.Cylinder(m, SacredDraw.Basis(b, -0.44 * h, 1.02 * h, 0.12, 1, 0, 0, 0, 0, 1), white, 0.1 * h, 0.02, seg);
            Shapes.Cylinder(m, SacredDraw.At(b, 0.44 * h, 0.58 * h, 0.12), gold, 0.012 * h, 0.4 * h, 4);
            for (int q = -1; q <= 1; q++)
                Shapes.Cone(m, SacredDraw.At(b, 0.44 * h + q * 0.04 * h, 0.98 * h - Math.Abs(q) * 0.03 * h, 0.12), gold, 0.012 * h, 0.08 * h, 4, 0, 0, false);
            Shapes.Dome(m, Affine3.RotationX(Math.PI).Then(SacredDraw.At(b, 0, 0.62 * h, 0.24)), white, 0.06 * h, 0.04 * h, seg, true);
        }

        // -------------------------------------------------------------------------------------------------------------
        // T27 Sweta Bhairav
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>OSM way of the Degu Taleju base building whose west wall carries the Sweta Bhairav screen.</summary>
        public const string SwetaWall = "w1415152806";

        /// <summary>
        /// The Sweta (Seto) Bhairav screen on the west wall of the Degu Taleju base building (closed, as all year but Indra
        /// Jatra): a carved dark timber frame with lintel ears round three latticed panels (about 4.2 × 3 m), a carved hood
        /// over it, two brass hanging lamps on brackets. Placed on the building's wall plane when it is in the tile.
        /// </summary>
        public static double Sweta(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double d = WallPlane(x);
            const double hw = 2.1, v0 = 1.0, v1 = 4.0;
            Affine3 k = SacredDraw.At(x.K, 0, 0, d);
            ShapeBrush wood = KdsLooks.Timber;
            if (lod >= 2)
            {
                SacredDraw.Box(m, k, -hw - 0.25, hw + 0.25, v0 - 0.2, v1 + 0.3, 0, 0.12, wood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
                SacredDraw.Panel(m, k, -hw, v0, hw, v1, 0.13, KdsLooks.WindowBlack);
                return v1 + 0.3;
            }
            // The frame: jambs, sill and a lintel whose ears run past the jambs, and the two mullions.
            BoxFaces wf = BoxFaces.Wall;
            SacredDraw.Box(m, k, -hw - 0.22, -hw, v0, v1, 0, 0.22, wood, wf);
            SacredDraw.Box(m, k, hw, hw + 0.22, v0, v1, 0, 0.22, wood, wf);
            SacredDraw.Box(m, k, -hw - 0.6, hw + 0.6, v1, v1 + 0.24, 0, 0.26, wood, wf | BoxFaces.Bottom);
            SacredDraw.Box(m, k, -hw - 0.5, hw + 0.5, v0 - 0.2, v0, 0, 0.26, wood, wf);
            for (int q = -1; q <= 1; q += 2) SacredDraw.Box(m, k, q * hw / 3 - 0.08, q * hw / 3 + 0.08, v0, v1, 0, 0.18, wood, wf);
            // Three latticed panels.
            for (int p = -1; p <= 1; p++)
            {
                double c = p * 2 * hw / 3, pw = 2 * hw / 3 - 0.16;
                SacredDraw.Panel(m, k, c - 0.5 * pw, v0, c + 0.5 * pw, v1, 0.04, Looks.Recess);
                int bars = lod == 0 ? 7 : 3;
                for (int i = 1; i <= bars; i++)
                {
                    double uu = c - 0.5 * pw + pw * i / (bars + 1);
                    SacredDraw.Box(m, k, uu - 0.022, uu + 0.022, v0, v1, 0.04, 0.1, KdsLooks.WindowBlack, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
                int rows = lod == 0 ? 10 : 4;
                for (int i = 1; i <= rows; i++)
                {
                    double vv = v0 + (v1 - v0) * i / (rows + 1);
                    SacredDraw.Box(m, k, c - 0.5 * pw, c + 0.5 * pw, vv - 0.022, vv + 0.022, 0.04, 0.1, KdsLooks.WindowBlack, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                }
            }
            // The carved hood over the screen and a gilt crest.
            SacredDraw.Box(m, k, -hw - 0.7, hw + 0.7, v1 + 0.24, v1 + 0.42, 0, 0.55, Looks.Ao(wood, 0.9f), BoxFaces.All);
            if (lod == 0) KdsKit.FlameArch(m, k, 0, v1 + 0.42, 0.2, 0.35, 0.5, 0.08, 0.04, KdsLooks.Gilt, lod, true, true);
            // The two brass lamps hanging from iron brackets.
            for (int sg = -1; sg <= 1; sg += 2)
            {
                double lu = sg * (hw + 0.75);
                Shapes.Bar(m, k, Looks.Of(MeshColor.FromHex(0x2E2B28), MaterialChannel.Metal), lu, v1 - 0.1, 0.02, lu, v1 - 0.1, 0.5, 0.025, 4);
                var p = new Profile2(8);
                p.Add(0, 0, true).Add(0.12, 0.05).Add(0.2, 0.18, true).Add(0.12, 0.28).Add(0.05, 0.45).Add(0, 0.5, true);
                Shapes.Lathe(m, SacredDraw.At(k, lu, v1 - 1.0, 0.5), KdsLooks.Brass, p, KdsKit.Seg(10, lod));
                Shapes.Cylinder(m, SacredDraw.At(k, lu, v1 - 0.5, 0.5), KdsLooks.BronzeDark, 0.01, 0.4, 4);
            }
            if (x.C != null) x.Box(-hw - 0.25, hw + 0.25, d, d + 0.25, v0 - 0.2, v1 + 0.42, GenColliderFlags.NoClimb, GenColliders.Wood);
            return SwetaTopM;
        }

        /// <summary>The frame w of the building's wall the screen hangs on (the edge of OSM w1415152806 crossing u = 0 most
        /// nearly along u, nearest the frame origin); 0 when the building is not in the tile.</summary>
        private static double WallPlane(KdsCtx x)
        {
            ulong wr = HeritageRecord.WayRelationRef(SwetaWall);
            double best = double.MaxValue, d = 0;
            for (int i = 0; i < x.Tile.Buildings.Count; i++)
            {
                if (x.Tile.Buildings[i].OsmRef != wr) continue;
                int[] ring = x.Tile.Buildings[i].Rings[0];
                int n = ring.Length / 2;
                for (int e = 0; e < n; e++)
                {
                    int f = (e + 1) % n;
                    double u0, w0, u1, w1;
                    x.Local(ring[2 * e] / 100.0, ring[2 * e + 1] / 100.0, out u0, out w0);
                    x.Local(ring[2 * f] / 100.0, ring[2 * f + 1] / 100.0, out u1, out w1);
                    double du = u1 - u0, dw = w1 - w0;
                    if (Math.Abs(du) < 2 * Math.Abs(dw) || Math.Min(u0, u1) > 1.0 || Math.Max(u0, u1) < -1.0) continue;
                    double wl = w0 + (0 - u0) * dw / du;
                    if (Math.Abs(wl) < best)
                    {
                        best = Math.Abs(wl);
                        d = wl;
                    }
                }
            }
            return best < 3.0 ? d + 0.02 : 0;
        }
    }
}
