using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// S3 and S4, the north entrance of the compound on the stupa's north axis (boudha.md 3.3-3.4; around/03 zoom,
    /// commons_late/c05 "Ajima Temple", y2019/06-08):
    /// <list type="bullet">
    /// <item>the Ajima (Hariti) shrine in the wall line, projecting 3 m into the kora: a red-brick plinth with two brick
    /// steps, dark carved lattice screens and a door under a gilt torana, one broad shallow gilt-copper hipped roof with
    /// standing-seam ribs, a red valance band and a yellow ruffled frill under its 1 m eaves, a gilt pendant at the eave
    /// centre and the gilt double vajra (vishvavajra) on a stepped pedestal as its finial; two tall brass prayer wheels in
    /// lantern housings flank the steps, a table of butter lamps in front;</item>
    /// <item>the two white gate pavilions either side of it (arched passages under gilt toranas and yellow striped
    /// valances, chorten finials; the west one leads to the stair up to the lowest terrace, <see cref="BoudhaTerraces"/>),
    /// joined to the shrine by short stretches of wall;</item>
    /// <item>the NE compound house east of the gate inside the wall: a white block with a maroon frieze set with white
    /// dentils, small windows, a door to the terraces and a little red pagoda-roofed shrine on its roof.</item>
    /// </list>
    /// </summary>
    internal static class BoudhaNorth
    {
        /// <summary>Ajima shrine: plinth top, eave and finial tip above the kora; its half width across the axis and its
        /// depth from the wall line back (into the compound) and forward (into the kora).</summary>
        public const double PlinthTop = 0.9, EaveY = 3.9, RoofTop = 5.5, Tip = 7.1, HalfW = 2.8, Back = 2.2, Front = 3.0;

        /// <summary>The gate pavilions' centres either side of the axis (m) and width.</summary>
        public const double PavilionU = 5.5, PavilionW = 4.2;

        public static void Build(MeshData m, GenColliders c, in BoudhaFrame f, int lod)
        {
            Affine3 k = f.Site;
            double vw = BoudhaWall.RadiusAt(f, 0);
            // A frame on the wall line at the axis, +z out to the kora (north), +x along the wall toward the east.
            Affine3 a = BoudhaKit.Facing(k, 0, BoudhaQuarter.CompoundTop, vw, 0, 1);
            Ajima(m, c, f, a, lod);
            for (int s = -1; s <= 1; s += 2)
            {
                Affine3 p = BoudhaKit.Facing(k, s * PavilionU, 0, vw, 0, 1);
                BoudhaWall.Pavilion(m, c, f, p, PavilionW, 2.6, 3.5, true, lod);
                // The short stretch of wall between the shrine and the pavilion.
                double x0 = s > 0 ? HalfW - 0.1 : -(PavilionU - 0.5 * PavilionW), x1 = s > 0 ? PavilionU - 0.5 * PavilionW : -(HalfW - 0.1);
                SacredDraw.Box(m, a, x0, x1, -0.4, BoudhaWall.Height, -BoudhaWall.HalfThick, BoudhaWall.HalfThick, BoudhaKit.B(BoudhaKit.LimeWhite, MaterialChannel.Plaster),
                               BoxFaces.Front | BoxFaces.Back | BoxFaces.Top);
            }
            if (lod <= 1) NoticeBoard(m, a, -(PavilionU + 0.5 * PavilionW) + 0.2, lod);
            NeHouse(m, c, f, lod);
        }

        // ------------------------------------------------------------------------------------------------ Ajima

        private static void Ajima(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 a, int lod)
        {
            ShapeBrush brick = BoudhaKit.B(MeshColor.FromHex(0xA64B32), MaterialChannel.Brick), brickTop = BoudhaKit.B(MeshColor.FromHex(0xB5603E), MaterialChannel.Brick);
            ShapeBrush lattice = BoudhaKit.B(BoudhaKit.LatticeDark, MaterialChannel.WoodCarved), wood = BoudhaKit.B(MeshColor.FromHex(0x3A2418), MaterialChannel.Wood);
            ShapeBrush gilt = BoudhaKit.B(MeshColor.FromHex(0xC9A24A), MaterialChannel.Gilt), giltLit = BoudhaKit.Gilt;
            double y0 = -BoudhaQuarter.CompoundTop - 0.3;
            // Plinth with a brick coping, and the two steps in front.
            Mould mo = SacredDraw.M;
            if (lod >= 2) mo.Add(0, y0, brick).Add(0, PlinthTop);
            else mo.Add(0.04, y0, brick).Add(0.04, PlinthTop - 0.12).Add(0.1, PlinthTop - 0.1, brickTop).Add(0.1, PlinthTop);
            double zc = 0.5 * (Front - Back), hd = 0.5 * (Front + Back);
            SacredDraw.RectRing(m, a, 0, zc, HalfW, hd, mo, true, brickTop);
            for (int s = 0; s < 2; s++)
            {
                // The lower step reaches 0.9 m out, the upper 0.45 m.
                double h = PlinthTop * (s + 1) / 3.0, z1 = Front + (2 - s) * 0.45;
                SacredDraw.Box(m, a, -1.6, 1.6, y0, h, Front - 0.02, z1, brickTop, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            }
            // The shrine body: corner posts, lattice screens on three sides (a white wall behind, in the compound).
            double bw = HalfW - 0.5, bz0 = -Back + 0.4, bz1 = Front - 0.5, by0 = PlinthTop, by1 = EaveY;
            SacredDraw.Box(m, a, -bw, bw, by0, by1, bz0, bz0 + 0.3, BoudhaKit.White, BoxFaces.Back | BoxFaces.Top);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                // Side screen.
                SacredDraw.Panel(m, SacredDraw.Turn(a, sx * bw, 0, 0.5 * (bz0 + bz1), sx * 0.5 * Math.PI), -0.5 * (bz1 - bz0), by0, 0.5 * (bz1 - bz0), by1, 0, lattice);
            }
            // Front: two screens either side of the door, the door opening, gilt frame and torana.
            ShapeBrush dark = BoudhaKit.B(MeshColor.FromHex(0x1C1712), MaterialChannel.Plain, 0.5f);
            SacredDraw.Panel(m, a, -bw, by0, -0.75, by1, bz1, lattice);
            SacredDraw.Panel(m, a, 0.75, by0, bw, by1, bz1, lattice);
            SacredDraw.Panel(m, a, -0.75, by0, 0.75, by0 + 2.0, bz1 - 0.25, dark);
            SacredDraw.Panel(m, a, -0.75, by0 + 2.0, 0.75, by1, bz1, lattice);
            if (lod <= 1)
            {
                // Lattice grid on the front screens: a mesh of thin bars (LOD0), the door frame and its torana.
                if (lod == 0)
                {
                    for (int sx = -1; sx <= 1; sx += 2)
                    {
                        double xa = sx < 0 ? -bw : 0.75, xb = sx < 0 ? -0.75 : bw;
                        BoudhaKit.GridLines(m, a, xa, xb, by0, by1, bz1 + 0.005, 0.22, 0.22, 0.03, BoudhaKit.B(MeshColor.FromHex(0x4A3424), MaterialChannel.WoodCarved));
                    }
                }
                SacredDraw.Box(m, a, -0.9, -0.75, by0, by0 + 2.15, bz1 - 0.05, bz1 + 0.08, gilt, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                SacredDraw.Box(m, a, 0.75, 0.9, by0, by0 + 2.15, bz1 - 0.05, bz1 + 0.08, gilt, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                SacredDraw.Box(m, a, -0.9, 0.9, by0 + 2.0, by0 + 2.15, bz1 - 0.05, bz1 + 0.08, gilt, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                SacredParts.Torana(m, a, 0, by0 + 2.17, bz1 + 0.02, 1.5, giltLit, lod);
                // Corner posts.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = 0; sz < 2; sz++)
                        SacredDraw.Box(m, a, sx * bw - 0.12, sx * bw + 0.12, by0, by1, (sz == 0 ? bz0 : bz1) - 0.12, (sz == 0 ? bz0 : bz1) + 0.12, wood, BoxFaces.All & ~BoxFaces.Bottom);
            }
            // The roof: a broad shallow gilt hip, eaves 1 m out, a short ridge.
            double ex = HalfW + 0.5, ez0 = -Back - 0.4, ez1 = Front + 0.55, rh = 0.45;
            double rz = 0.5 * (ez0 + ez1), rise = RoofTop - EaveY, hzd = 0.5 * (ez1 - ez0);
            for (int s = -1; s <= 1; s += 2)
            {
                double l = Math.Sqrt(rise * rise + hzd * hzd);
                SacredDraw.Quad(m, a, -ex, EaveY, rz + s * hzd, ex, EaveY, rz + s * hzd, rh, RoofTop, rz, -rh, RoofTop, rz, 0, hzd / l, s * rise / l, gilt);
                double lx = Math.Sqrt(rise * rise + (ex - rh) * (ex - rh));
                SacredDraw.Tri(m, a, s * ex, EaveY, ez0, s * ex, EaveY, ez1, s * rh, RoofTop, rz, s * rise / lx, (ex - rh) / lx, 0, gilt);
            }
            SacredDraw.Quad(m, a, -ex, EaveY - 0.03, ez0, ex, EaveY - 0.03, ez0, ex, EaveY - 0.03, ez1, -ex, EaveY - 0.03, ez1, 0, -1, 0, BoudhaKit.B(MeshColor.FromHex(0x5A3A22), MaterialChannel.Wood, 0.7f));
            if (lod == 0)
            {
                // Standing-seam ribs down the front and back slopes.
                ShapeBrush rib = BoudhaKit.B(BoudhaKit.GiltLit, MaterialChannel.Gilt);
                for (int s = -1; s <= 1; s += 2)
                    for (int q = -6; q <= 6; q++)
                    {
                        double xe = ex * q / 6.5, xr = rh * q / 6.5;
                        Shapes.Bar(m, a, rib, xe, EaveY + 0.03, rz + s * hzd, xr, RoofTop + 0.02, rz, 0.025, 3);
                    }
            }
            if (lod <= 1)
            {
                // Red valance band and the yellow ruffled frill under the eaves, front and sides.
                uint[] stripe = { BoudhaKit.ClothRed };
                Affine3 front = a;
                BoudhaKit.Valance(m, front, -ex + 0.1, ex - 0.1, ez1 - 0.15, EaveY - 0.02, stripe, 0.22, BoudhaKit.NicheValance, 0.38, 0.22, lod);
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    Affine3 side = SacredDraw.Turn(a, sx * (ex - 0.15), 0, rz, sx * 0.5 * Math.PI);
                    BoudhaKit.Valance(m, side, -hzd + 0.1, hzd - 0.1, 0, EaveY - 0.02, stripe, 0.22, BoudhaKit.NicheValance, 0.38, 0.3, lod);
                }
                // The gilt pendant (a torana drop) at the eave's centre.
                Profile2 pd = BoudhaKit.Prof;
                pd.Add(0, -0.75).Add(0.06, -0.72).Add(0.16, -0.45).Add(0.12, -0.2).Add(0.08, 0, true).Add(0, 0);
                if (lod == 0) pd.Smooth(1);
                Shapes.Lathe(m, SacredDraw.At(a, 0, EaveY - 0.25, ez1 - 0.1), giltLit, pd, lod == 0 ? 8 : 5);
            }
            // The finial: a stepped gilt pedestal, a lotus knob and the double vajra.
            Mould pm = SacredDraw.M;
            pm.Add(0.22, RoofTop - 0.1, gilt).Add(0.22, RoofTop + 0.12).Add(0.1, RoofTop + 0.14).Add(0.1, RoofTop + 0.3).Add(0.0, RoofTop + 0.32).Add(0.0, RoofTop + 0.45);
            SacredDraw.RectRing(m, a, 0, rz, 0.2, 0.2, pm, true, gilt);
            double vy = RoofTop + 0.45 + 0.55;
            Shapes.Sphere(m, SacredDraw.At(a, 0, vy - 0.55, rz), giltLit, 0.16, lod == 0 ? 8 : 5);
            Shapes.Sphere(m, SacredDraw.At(a, 0, vy, rz), giltLit, 0.14, lod == 0 ? 8 : 5);
            int arms = lod >= 2 ? 2 : 4;
            for (int q = 0; q < arms; q++)
            {
                // Arms up, down, left and right in the front plane (and the vajra seen end-on as the hub).
                double ang = q * 0.5 * Math.PI;
                double dx = Math.Sin(ang), dy = Math.Cos(ang);
                if (lod >= 2 && q == 1) continue;
                VajraArm(m, a, 0, vy, rz, dx, dy, giltLit, lod);
            }
            if (lod <= 1)
            {
                // Two brass prayer wheels in lantern housings flanking the steps, and the butter-lamp table.
                for (int s = -1; s <= 1; s += 2) WheelLantern(m, a, s * 2.15, Front + 0.6, lod);
                ShapeBrush table = BoudhaKit.B(MeshColor.FromHex(0x2A2622), MaterialChannel.Metal);
                SacredDraw.Box(m, a, -1.1, 1.1, BoudhaQuarter.PaveTop - BoudhaQuarter.CompoundTop, BoudhaQuarter.PaveTop - BoudhaQuarter.CompoundTop + 0.75, Front + 1.6,
                               Front + 2.1, table, BoxFaces.All & ~BoxFaces.Bottom);
                if (lod == 0)
                    for (int q = 0; q < 7; q++)
                        Shapes.Frustum(m, SacredDraw.At(a, -0.9 + q * 0.3, BoudhaQuarter.PaveTop - BoudhaQuarter.CompoundTop + 0.75, Front + 1.85),
                                       BoudhaKit.B(BoudhaKit.Brass, MaterialChannel.Metal), 0.05, 0.08, 0.08, 4, 0, 0, false, true);
            }
            if (c != null)
            {
                double x, y, z, ux, uy, uz;
                a.Point(0, 0, zc, out x, out y, out z);
                a.Vector(1, 0, 0, out ux, out uy, out uz);
                c.AddBox(x, z, f.Level - 0.3, f.Level + PlinthTop, HalfW, hd, ux, uz, GenColliderFlags.Walkable, GenColliders.Brick);
                c.AddBox(x, z, f.Level + PlinthTop, f.Level + EaveY, bw, 0.5 * (bz1 - bz0), ux, uz, GenColliderFlags.NoClimb, GenColliders.Wood);
            }
        }

        /// <summary>One arm of the double vajra: a neck, a lotus bulb and the three curled prongs meeting in a point.</summary>
        private static void VajraArm(MeshData m, in Affine3 a, double x, double y, double z, double dx, double dy, in ShapeBrush b, int lod)
        {
            Affine3 arm = Affine3.Along(0, 0, 0, dx, dy, 0, out double len);
            Affine3 af = SacredDraw.At(a, x, y, z) * arm;
            Profile2 p = BoudhaKit.Prof;
            p.Add(0, 0.1).Add(0.06, 0.1, true).Add(0.06, 0.18).Add(0.13, 0.26).Add(0.11, 0.36).Add(0.05, 0.42, true).Add(0.03, 0.5).Add(0, 0.55);
            if (lod == 0) p.Smooth(1);
            Shapes.Lathe(m, af, b, p, lod == 0 ? 8 : 5);
            if (lod == 0)
            {
                // The two side prongs curling in to the tip.
                for (int s = -1; s <= 1; s += 2) Shapes.Bar(m, af, b, s * 0.13, 0.3, 0, s * 0.03, 0.52, 0, 0.025, 4);
            }
        }

        /// <summary>A tall brass prayer wheel in its lantern housing (c05: either side of the Ajima steps).</summary>
        private static void WheelLantern(MeshData m, in Affine3 a, double x, double z, int lod)
        {
            ShapeBrush brass = BoudhaKit.B(BoudhaKit.Brass, MaterialChannel.Metal), dark = BoudhaKit.B(MeshColor.FromHex(0x5A4A2A), MaterialChannel.Metal);
            double y0 = BoudhaQuarter.PaveTop - BoudhaQuarter.CompoundTop;
            Affine3 l = SacredDraw.At(a, x, y0, z);
            SacredDraw.Box(m, l, -0.38, 0.38, 0, 0.35, -0.38, 0.38, dark, BoxFaces.All & ~BoxFaces.Bottom);
            int seg = lod == 0 ? 10 : 6;
            Shapes.Cylinder(m, SacredDraw.At(l, 0, 0.35, 0), brass, 0.27, 1.2, seg, 0.03, 1, false, true);
            if (lod == 0)
            {
                for (int q = 0; q < 4; q++) Shapes.Cylinder(m, SacredDraw.At(l, 0, 0.43 + 0.33 * q, 0), dark, 0.285, 0.05, seg, 0, 0, false, false);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Shapes.Bar(m, l, dark, sx * 0.34, 0.35, sz * 0.34, sx * 0.34, 1.65, sz * 0.34, 0.03, 4);
            }
            // The lantern's little roof and knob.
            Shapes.Frustum(m, SacredDraw.At(l, 0, 1.65, 0), brass, 0.5, 0.12, 0.35, lod == 0 ? 4 : 4, 0, 0, true, true);
            Shapes.Sphere(m, SacredDraw.At(l, 0, 2.05, 0), brass, 0.08, 5);
        }

        /// <summary>The "Important Notice" board on the west pavilion (y2019/06): a yellow panel in a dark frame.</summary>
        private static void NoticeBoard(MeshData m, in Affine3 a, double x, int lod)
        {
            SacredDraw.Box(m, a, x, x + 0.9, 1.3, 2.4, 1.32, 1.36, BoudhaKit.B(MeshColor.FromHex(0x3A3028), MaterialChannel.Wood), BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
            SacredDraw.Panel(m, a, x + 0.06, 1.36, x + 0.84, 2.34, 1.365, BoudhaKit.B(MeshColor.FromHex(0xE8D24A), MaterialChannel.Paint));
            if (lod == 0)
                for (int q = 0; q < 6; q++)
                    SacredDraw.Panel(m, a, x + 0.14, 2.15 - q * 0.13, x + 0.76, 2.18 - q * 0.13, 1.37, BoudhaKit.B(MeshColor.FromHex(0x7A2A1A), MaterialChannel.Paint));
        }

        // ------------------------------------------------------------------------------------------------ NE house

        /// <summary>The NE compound house (around/03 zoom): white, a maroon frieze with white dentils under a flat roof, small
        /// windows, a door toward the stupa, a little red pagoda-roofed shrine on the roof; in the compound east of the
        /// gate between the lowest terrace's north arm and the wall.</summary>
        private static void NeHouse(MeshData m, GenColliders c, in BoudhaFrame f, int lod)
        {
            Affine3 k = f.Site;
            double u0 = 8.6, u1 = 17.6, v0 = 39.4, v1 = 43.2, h = 4.6, fh = 0.75;
            Affine3 hf = BoudhaKit.Facing(k, 0.5 * (u0 + u1), BoudhaQuarter.CompoundTop, v0, 0, -1);
            // In hf: x across (west → east mirrored), z out toward the stupa (south), the house behind (z < 0).
            double hw = 0.5 * (u1 - u0), d = v1 - v0;
            ShapeBrush white = BoudhaKit.White, maroon = BoudhaKit.B(BoudhaKit.Maroon, MaterialChannel.Paint), dentil = BoudhaKit.B(MeshColor.FromHex(0xF4F1EA), MaterialChannel.Plaster);
            Mould mo = SacredDraw.M;
            mo.Add(0.03, -0.35, BoudhaKit.B(BoudhaKit.Weathered, MaterialChannel.Plaster)).Add(0.03, 0.3).Add(0, 0.32, white).Add(0, h - fh - 0.05).Add(0.06, h - fh, maroon)
              .Add(0.06, h).Add(0.16, h + 0.06, white).Add(0.16, h + 0.22).Add(0.02, h + 0.24).Add(0.02, h + 0.55).Add(-0.15, h + 0.55).Add(-0.15, h + 0.25);
            SacredDraw.RectRing(m, hf, 0, -0.5 * d, hw, 0.5 * d, mo, false, white);
            SacredDraw.CapRect(m, hf, 0, -0.5 * d, hw - 0.15, 0.5 * d - 0.15, h + 0.25, true, BoudhaKit.B(MeshColor.FromHex(0xB9B2A6), MaterialChannel.Concrete));
            if (lod <= 1)
            {
                // White dentils across the maroon frieze, windows with red frames, the door.
                if (lod == 0)
                {
                    int dn = (int)(2 * hw / 0.45);
                    for (int q = 0; q < dn; q++)
                    {
                        double x = -hw + (q + 0.5) * 2 * hw / dn;
                        SacredDraw.Box(m, hf, x - 0.1, x + 0.1, h - 0.32, h - 0.12, 0.06, 0.14, dentil, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                    }
                }
                for (int q = -2; q <= 2; q++)
                {
                    if (q == 0)
                    {
                        SacredDraw.Panel(m, hf, -0.6, 0.3, 0.6, 2.45, 0.01, BoudhaKit.B(MeshColor.FromHex(0x5A3422), MaterialChannel.Wood));
                        continue;
                    }
                    BoudhaKit.TibetanWindow(m, hf, q * 1.8, 2.2, 0.0, 0.7, 0.9, BoudhaKit.ClothRed, lod);
                }
                // The little red pagoda-roofed shrine on the roof (back corner).
                Affine3 sh = SacredDraw.At(hf, hw - 1.3, h + 0.25, -d + 1.2);
                SacredDraw.Box(m, sh, -0.6, 0.6, 0, 1.0, -0.6, 0.6, white, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Frustum(m, SacredDraw.At(sh * Affine3.Yaw(45), 0, 1.0, 0), BoudhaKit.B(MeshColor.FromHex(0xB3261E), MaterialChannel.RoofTile), 1.15, 0.12, 0.7, 4, 0, 0, true, false);
                Shapes.Cone(m, SacredDraw.At(sh, 0, 1.68, 0), BoudhaKit.Gilt, 0.1, 0.4, 5, 0, 0, true);
            }
            if (c != null)
            {
                double x, y, z, ux, uy, uz;
                hf.Point(0, 0, -0.5 * d, out x, out y, out z);
                hf.Vector(1, 0, 0, out ux, out uy, out uz);
                c.AddBox(x, z, f.Level - 0.3, f.Level + h + 0.5, hw, 0.5 * d, ux, uz, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }
    }
}
