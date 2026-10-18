using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// The dressing of the stupa's terraces (boudha.md 3.1.2; around/03 zoom, corner/04-06, spire/06, tamang/08,
    /// earthquake/07):
    /// <list type="bullet">
    /// <item>the stairs from the compound up to the lowest terrace: on the north axis through the west gate pavilion of
    /// the north entrance and on the south side by the lamp houses, white-cheeked with gilt-yellow handrails, with their
    /// walkable ramps;</item>
    /// <item>the big white bell chortens on the lowest terrace, a pair at each arm (the north pair flanking the second
    /// terrace's stair);</item>
    /// <item>the four mini-Boudha chortens at the lowest terrace's corners: a white stepped base and dome with a small gilt
    /// niche shrine under a torana on its front, a white harmika with the painted eyes, a gilt thirteen-step spire, a
    /// gilt parasol and finial;</item>
    /// <item>the pairs of white elephants with riders and dotted harness at the foot of the north and south stairs to
    /// the second terrace, the two white Dhyani Buddhas on their pedestals at the head of the north stair;</item>
    /// <item>the grey carved stone shrine with its makara pediment and chaitya finial beside the foot of the south dome
    /// stair.</item>
    /// </list>
    /// </summary>
    internal static class BoudhaTerraces
    {
        /// <summary>The compound stairs to the lowest terrace: centre across the axis (site u), half width.</summary>
        public const double NorthStairU = -BoudhaNorth.PavilionU, SouthStairU = 4.5, StairHalfW = 1.25;

        /// <summary>Gaps the lowest terrace's parapet leaves for the compound stairs (site u ranges on the north and
        /// south arm ends).</summary>
        public static bool ParapetGap(int arm, double u)
        {
            if (arm == 0) return Math.Abs(u - NorthStairU) < StairHalfW + 0.45;
            if (arm == 2) return Math.Abs(u - SouthStairU) < StairHalfW + 0.45;
            return false;
        }

        public static void Build(MeshData m, GenColliders c, in BoudhaFrame f, int lod)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = f.Site;
            double t1 = BoudhaStupa.T1Top;
            CompoundStairs(m, c, f, lod);
            // Bell chortens: a pair per arm on the lowest terrace.
            double[] bell = { 18.1, 34.6, -18.1, 34.6, 18.2, -34.2, -18.2, -34.2, 36.0, 10.5, 36.0, -16.5, -36.0, 10.0, -36.0, -16.0 };
            for (int i = 0; i < bell.Length; i += 2)
            {
                BoudhaKit.BellChorten(m, SacredDraw.At(k, bell[i], t1, bell[i + 1]), 5.6, lod, false);
                Solid(c, f, bell[i], bell[i + 1], t1, t1 + 5.6, 1.6);
            }
            // Mini-Boudha chortens at the four body corners.
            double[] mini = { 29.6, 26.4, 29.6, -32.6, -29.2, 26.4, -29.2, -32.6 };
            for (int i = 0; i < mini.Length; i += 2)
            {
                double u = mini[i], v = mini[i + 1];
                MiniBoudha(m, BoudhaKit.Facing(k, u, t1, v, Math.Sign(u), Math.Sign(v)), lod);
                Solid(c, f, u, v, t1, t1 + MiniH, MiniDomeR + 0.1);
            }
            if (lod <= 1)
            {
                // Elephants with riders at the foot of the north and south stairs to the second terrace.
                for (int s = -1; s <= 1; s += 2)
                {
                    Elephant(m, c, f, BoudhaKit.Facing(k, s * 3.6, t1, 37.4, 0, 1), s, lod);
                    Elephant(m, c, f, BoudhaKit.Facing(k, s * 3.6, t1, -37.6, 0, -1), -s, lod);
                    // Dhyani Buddhas at the head of the north stair, on the second terrace.
                    Buddha(m, c, f, BoudhaKit.Facing(k, -0.5 + s * 3.1, BoudhaStupa.T2Top, 29.6, 0, 1), lod);
                }
                StoneShrine(m, c, f, BoudhaKit.Facing(k, 3.0, BoudhaStupa.T3Top, -21.8, 0, -1), lod);
            }
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        private static void Solid(GenColliders c, in BoudhaFrame f, double u, double v, double y0, double y1, double r)
        {
            if (c == null) return;
            double x, z;
            f.ToTile(u, v, out x, out z);
            c.AddCylinder(x, z, f.Level + y0, f.Level + y1, r);
        }

        // ------------------------------------------------------------------------------------------------ stairs

        /// <summary>The two stairs from the compound ground to the lowest terrace (north through the west gate pavilion,
        /// south by the lamp houses), with cheek walls, yellow handrails, clay pots and their ramps.</summary>
        private static void CompoundStairs(MeshData m, GenColliders c, in BoudhaFrame f, int lod)
        {
            Affine3 k = f.Site;
            ShapeBrush tread = BoudhaKit.B(MeshColor.FromHex(0xB0A79C), MaterialChannel.Stone), cheek = BoudhaKit.White;
            ShapeBrush rail = BoudhaKit.B(MeshColor.FromHex(0xE8C23A), MaterialChannel.Metal);
            double end, centre, y0 = BoudhaQuarter.CompoundTop, y1 = BoudhaStupa.T1Top;
            for (int q = 0; q < 2; q++)
            {
                int arm = q == 0 ? 0 : 2;
                BoudhaStupa.ArmEnd(0, arm, out end, out centre);
                double u = q == 0 ? NorthStairU : SouthStairU, dir = q == 0 ? 1 : -1;
                double wallR = BoudhaWall.RadiusAt(f, q == 0 ? 0 : Math.PI);
                double foot = wallR - (q == 0 ? 1.35 : 1.6), run = foot - end;
                if (run < 3.0) continue;
                // Frame at the foot, +z climbing toward the terrace.
                Affine3 st = BoudhaKit.Facing(k, u, 0, dir * foot, 0, -dir);
                BoudhaKit.Flight(m, c, st, StairHalfW, run, y0, y1, 0.32, 0.55, tread, cheek, lod >= 1 ? 2 : 0);
                if (lod <= 1)
                {
                    BoudhaKit.Handrail(m, st, -StairHalfW + 0.12, run, y0, y1, 0.9, rail, lod);
                    BoudhaKit.Handrail(m, st, StairHalfW - 0.12, run, y0, y1, 0.9, rail, lod);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ chortens

        /// <summary>Height of a mini-Boudha chorten, half its square base and its dome's radius (spire/06: the dome
        /// nearly fills the base, the harmika about 0.4 of the dome's width).</summary>
        public const double MiniH = 7.5, MiniBase = 2.2, MiniDomeR = 1.95;

        /// <summary>
        /// A mini-Boudha chorten (spire/06, Bibek KC): a white two-step square base, a white dome with a small gilt niche
        /// shrine under a torana on its front (frame +z), a white harmika with the Buddha's eyes on its four faces, a tall
        /// gilt square spire of thirteen steps, a gilt parasol and the finial. Standing on y = 0 of <paramref name="fr"/>.
        /// </summary>
        private static void MiniBoudha(MeshData m, in Affine3 fr, int lod)
        {
            ShapeBrush w = BoudhaKit.White, sh = BoudhaKit.WhiteShade, gilt = BoudhaKit.Gilt, giltS = BoudhaKit.B(BoudhaKit.GiltShade, MaterialChannel.Gilt);
            // Base: two white steps with copings (spire/06: the dome nearly fills the upper step).
            Mould mo = SacredDraw.M;
            if (lod >= 2) mo.Add(0, -0.2, sh).Add(0, 0.55, w).Add(-0.25, 0.55).Add(-0.25, 1.05);
            else mo.Add(0, -0.2, sh).Add(0, 0.47, w).Add(0.06, 0.49).Add(0.06, 0.55).Add(-0.25, 0.55).Add(-0.25, 0.97).Add(-0.2, 0.99).Add(-0.2, 1.05);
            SacredDraw.RectRing(m, fr, 0, 0, MiniBase, MiniBase, mo, true, w);
            // Dome: a full, slightly raised hemisphere on a round foot band.
            int seg = lod == 0 ? 18 : lod == 1 ? 12 : 8, arc = lod == 0 ? 8 : lod == 1 ? 5 : 3;
            Profile2 p = BoudhaKit.Prof;
            double r = MiniDomeR, y = 1.05, rise = 1.85;
            p.Add(0, y).Add(r + 0.08, y, true).Add(r + 0.08, y + 0.18, true).Add(r, y + 0.2, true);
            for (int i = 1; i <= arc; i++)
            {
                double a = 0.5 * Math.PI * i / arc;
                p.Add(r * Math.Cos(a), y + 0.2 + rise * Math.Sin(a));
            }
            Shapes.Lathe(m, fr, w, p, seg);
            double top = y + 0.2 + rise;
            // The gilt niche shrine with its torana low on the dome's front.
            if (lod <= 1)
            {
                double ny = y + 0.3, nz = r - 0.2;
                SacredDraw.Box(m, fr, -0.45, 0.45, ny, ny + 0.85, nz - 0.1, nz + 0.3, gilt, BoxFaces.All & ~BoxFaces.Bottom & ~BoxFaces.Back);
                SacredDraw.Panel(m, fr, -0.29, ny + 0.1, 0.29, ny + 0.64, nz + 0.305, BoudhaKit.B(MeshColor.FromHex(0x5A3A1C), MaterialChannel.Plain, 0.6f));
                Shapes.Frustum(m, SacredDraw.At(fr, 0, ny + 0.85, nz + 0.1) * Affine3.Yaw(45), gilt, 0.55, 0.07, 0.38, 4, 0, 0, true, false);
                if (lod == 0) SacredParts.Torana(m, fr, 0, ny + 0.65, nz + 0.31, 0.54, gilt, lod);
            }
            // Harmika with the eyes: tall and broad (about 0.42 of the dome's width and 0.68 of its own width tall,
            // spire/06).
            double hh = 1.1, hw = 0.82;
            Mould hm = SacredDraw.M;
            if (lod >= 2) hm.Add(0, top - 0.15, w).Add(0, top + hh + 0.12);
            else hm.Add(0, top - 0.15, w).Add(0, top + hh).Add(0.08, top + hh + 0.02).Add(0.08, top + hh + 0.12);
            SacredDraw.RectRing(m, fr, 0, 0, hw, hw, hm, true, w);
            if (lod <= 1)
            {
                for (int s = 0; s < 4; s++)
                {
                    Affine3 face = fr * Affine3.Yaw(90 * s);
                    MiniEyes(m, face, top + 0.62, hw + 0.006, 2 * hw, lod);
                }
            }
            // The gilt square spire: thirteen steps from almost the harmika's width to about half of it, then the
            // parasol and the finial.
            double sy = top + hh + 0.12, sh0 = 2.3, b0 = 0.7, b1 = 0.36;
            if (lod == 0)
            {
                int steps = 13;
                for (int q = 0; q < steps; q++)
                {
                    double t0 = (double)q / steps, half = b0 + (b1 - b0) * t0, ya = sy + sh0 * t0, yb = sy + sh0 * (q + 1.0) / steps;
                    Mould sm = SacredDraw.M;
                    sm.Add(0, ya, gilt).Add(0, yb - 0.04).Add(-0.02, yb - 0.02, giltS).Add(-(b0 - b1) / steps, yb);
                    SacredDraw.RectRing(m, fr, 0, 0, half, half, sm, q == steps - 1, gilt);
                }
            }
            else
            {
                Shapes.Frustum(m, SacredDraw.At(fr * Affine3.Yaw(45), 0, sy, 0), gilt, b0 * 1.41, b1 * 1.41, sh0, 4, 0, 0, false, true);
            }
            double py = sy + sh0;
            // The parasol: a gilt square cap wider than the spire's top, a drum and the bulb under the spike.
            if (lod <= 1)
            {
                Shapes.Frustum(m, SacredDraw.At(fr * Affine3.Yaw(45), 0, py, 0), gilt, (b1 + 0.16) * 1.41, (b1 + 0.04) * 1.41, 0.16, 4, 0, 0, true, true);
                Shapes.Cylinder(m, SacredDraw.At(fr, 0, py + 0.16, 0), giltS, 0.2, 0.2, lod == 0 ? 10 : 6, 0, 0, false, false);
            }
            Shapes.Cone(m, SacredDraw.At(fr, 0, py + 0.36, 0), gilt, 0.08, MiniH - py - 0.36, lod == 0 ? 8 : 4, 0, 0, false);
            if (lod == 0) Shapes.Sphere(m, SacredDraw.At(fr, 0, py + 0.5, 0), gilt, 0.13, 8);
        }

        /// <summary>The painted eyes on a mini chorten's harmika face <paramref name="w"/> wide (spire/06): white almonds
        /// with dark blue irises looking a little down, the dark brows sweeping up and out, the red "१" nose below between
        /// them; the eye centre line at <paramref name="y"/> on the face plane z.</summary>
        private static void MiniEyes(MeshData m, in Affine3 f, double y, double z, double w, int lod)
        {
            ShapeBrush white = BoudhaKit.B(0xFFFFFFFFu, MaterialChannel.Paint), blue = BoudhaKit.B(BoudhaKit.EyeBlueDark, MaterialChannel.Paint);
            ShapeBrush brow = BoudhaKit.B(BoudhaKit.Brow, MaterialChannel.Paint), red = BoudhaKit.B(BoudhaKit.EyeRed, MaterialChannel.Paint);
            int seg = lod == 0 ? 5 : 4;
            for (int s = -1; s <= 1; s += 2)
            {
                double xc = s * 0.25 * w;
                // The dark lid line round the white almond (the harmika is white too), then the almond and the iris.
                BoudhaStupa.Almond(m, f, xc, y, z, 0.16 * w, 0.072 * w, 0.056 * w, s, seg, brow);
                BoudhaStupa.Almond(m, f, xc, y, z + 0.002, 0.145 * w, 0.058 * w, 0.044 * w, s, seg, white);
                BoudhaStupa.Disc(m, f, xc, y - 0.006 * w, z + 0.004, 0.05 * w, 6, blue);
                BoudhaStupa.Arc(m, f, xc + s * 0.02 * w, y + 0.1 * w, z + 0.002, 0.18 * w, 0.05 * w, 0.035 * w, s, 4, brow);
            }
            if (lod == 0) BoudhaStupa.Spiral(m, f, 0, y - 0.17 * w, z + 0.002, 0.05 * w, 8, 0.02 * w, red);
            else SacredDraw.Panel(m, f, -0.015 * w, y - 0.22 * w, 0.015 * w, y - 0.1 * w, z + 0.002, red);
        }

        // ------------------------------------------------------------------------------------------------ statues

        /// <summary>A white elephant with a rider and its dotted harness (tamang/08): the temple guardian figure painted
        /// white, a red saddle cloth, rows of coloured dots on the harness, the rider seated with a sword and shield.</summary>
        private static void Elephant(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 ef, int side, int lod)
        {
            double h = 1.55;
            // A white plinth step.
            SacredDraw.Box(m, ef, -0.55, 0.55, -0.05, 0.35, -0.95, 0.95, BoudhaKit.White, BoxFaces.All & ~BoxFaces.Bottom);
            SacredFigures.Guardian(m, ef, 0, 0.35, 0, h, GuardianKind.Elephant, true, SacredFigures.Generic, lod, side);
            if (lod == 0)
            {
                // Dotted harness: rows of red, blue, yellow and green beads round the body.
                uint[] dots = { 0xC8282E, 0x2E5DA8, 0xF2C230, 0x2F8F4E };
                for (int s = -1; s <= 1; s += 2)
                    for (int q = 0; q < 5; q++)
                    {
                        double t = q / 4.0, z = -0.42 * h + 0.84 * h * t, y = 0.35 + 0.48 * h + 0.06 * Math.Sin(Math.PI * t);
                        Shapes.Sphere(m, SacredDraw.At(ef, s * 0.27 * h, y, z), BoudhaKit.B(MeshColor.FromHex(dots[q % 4]), MaterialChannel.Paint), 0.04, 4);
                    }
            }
            // The rider astride the back.
            double ry = 0.35 + 0.78 * h;
            ShapeBrush robe = BoudhaKit.B(MeshColor.FromHex(0xE07A2A), MaterialChannel.Fabric), skin = BoudhaKit.B(MeshColor.FromHex(0xF0D2B4), MaterialChannel.Skin);
            ShapeBrush hair = BoudhaKit.B(MeshColor.FromHex(0x2A2420), MaterialChannel.Hair);
            Shapes.Ellipsoid(m, SacredDraw.At(ef, 0, ry + 0.05, -0.05), BoudhaKit.B(MeshColor.FromHex(0x2E5DA8), MaterialChannel.Fabric), 0.3, 0.12, 0.22, lod == 0 ? 6 : 5, false);
            Shapes.Ellipsoid(m, SacredDraw.At(ef, 0, ry + 0.35, -0.05), robe, 0.17, 0.26, 0.13, lod == 0 ? 6 : 5, false);
            Shapes.Sphere(m, SacredDraw.At(ef, 0, ry + 0.72, -0.03), skin, 0.12, lod == 0 ? 8 : 5);
            Shapes.Sphere(m, SacredDraw.At(ef, 0, ry + 0.86, -0.06), hair, 0.07, 5);
            if (lod == 0)
            {
                Shapes.Bar(m, ef, skin, 0.15, ry + 0.48, -0.05, 0.22, ry + 0.25, 0.12, 0.04, 4);
                Shapes.Bar(m, ef, skin, -0.15, ry + 0.48, -0.05, -0.22, ry + 0.3, 0.1, 0.04, 4);
                Shapes.Cylinder(m, SacredDraw.Basis(ef, -0.3, ry + 0.32, 0.12, 0, 0, 1, 1, 0, 0), BoudhaKit.Gilt, 0.14, 0.03, 10, 0, 0, true, true);
                Shapes.Bar(m, ef, BoudhaKit.B(MeshColor.FromHex(0xC9CED6), MaterialChannel.Metal), 0.22, ry + 0.25, 0.12, 0.22, ry + 0.75, 0.2, 0.02, 3);
            }
            if (c != null)
            {
                double x, y, z;
                ef.Point(0, 0, 0, out x, out y, out z);
                c.AddCylinder(x, z, y, y + 0.35 + h + 0.9, 0.7);
            }
        }

        /// <summary>A white Dhyani Buddha seated in meditation on a lotus atop a white pedestal (around/03 zoom).</summary>
        private static void Buddha(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 bf, int lod)
        {
            ShapeBrush w = BoudhaKit.White, robe = BoudhaKit.B(MeshColor.FromHex(0xF6F2EA), MaterialChannel.Paint), gilt = BoudhaKit.Gilt;
            Mould mo = SacredDraw.M;
            if (lod >= 2) mo.Add(0.05, -0.05, w).Add(0.05, 1.0);
            else mo.Add(0.05, -0.05, w).Add(0.05, 0.15).Add(0, 0.18).Add(0, 0.85).Add(0.07, 0.88).Add(0.07, 1.0);
            SacredDraw.RectRing(m, bf, 0, 0, 0.42, 0.42, mo, true, w);
            int seg = lod == 0 ? 12 : lod == 1 ? 7 : 5;
            Profile2 lotus = BoudhaKit.Prof;
            lotus.Add(0, 1.0).Add(0.42, 1.0, true).Add(0.5, 1.1).Add(0.46, 1.2).Add(0, 1.2);
            Shapes.Lathe(m, bf, gilt, lotus, seg);
            // Crossed legs, torso, head with the ushnisha, hands in the lap.
            Shapes.Ellipsoid(m, SacredDraw.At(bf, 0, 1.32, 0.02), robe, 0.42, 0.13, 0.28, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(bf, 0, 1.72, -0.04), robe, 0.25, 0.36, 0.17, seg, false);
            Shapes.Sphere(m, SacredDraw.At(bf, 0, 2.18, -0.02), w, 0.15, seg);
            if (lod <= 1) Shapes.Sphere(m, SacredDraw.At(bf, 0, 2.34, -0.03), BoudhaKit.B(MeshColor.FromHex(0x3A5BA0), MaterialChannel.Paint), 0.07, 6);
            if (lod == 0)
            {
                for (int s = -1; s <= 1; s += 2) Shapes.Bar(m, bf, robe, s * 0.24, 1.88, -0.04, s * 0.12, 1.47, 0.16, 0.06, 5);
                Shapes.Ellipsoid(m, SacredDraw.At(bf, 0, 1.46, 0.17), robe, 0.12, 0.05, 0.07, 6, false);
                Shapes.Torus(m, SacredDraw.Basis(bf, 0, 2.18, -0.12, 1, 0, 0, 0, 0, 1), gilt, 0.24, 0.02, 14, 3);
            }
            if (c != null)
            {
                double x, y, z;
                bf.Point(0, 0, 0, out x, out y, out z);
                c.AddCylinder(x, z, y, y + 2.4, 0.5);
            }
        }

        /// <summary>The grey carved stone shrine (wheels/04-05, earthquake/07): a plinth, a body with an arched niche, a
        /// makara-scroll pediment and a small chaitya finial.</summary>
        private static void StoneShrine(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 sf, int lod)
        {
            ShapeBrush stone = BoudhaKit.B(MeshColor.FromHex(0x8E8A82), MaterialChannel.Stone), dark = BoudhaKit.B(MeshColor.FromHex(0x2A2622), MaterialChannel.Plain, 0.6f);
            Mould mo = SacredDraw.M;
            mo.Add(0.08, -0.05, stone).Add(0.08, 0.3).Add(0, 0.34).Add(0, 1.55).Add(0.08, 1.6).Add(0.08, 1.7);
            SacredDraw.RectRing(m, sf, 0, 0, 0.6, 0.4, mo, true, stone);
            SacredDraw.Panel(m, sf, -0.28, 0.45, 0.28, 1.2, 0.41, dark);
            if (lod <= 1)
            {
                SacredParts.Torana(m, sf, 0, 1.2, 0.41, 0.7, stone, lod);
                // Makara scrolls either side of the pediment.
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Torus(m, SacredDraw.Basis(sf, s * 0.48, 1.85, 0, 1, 0, 0, 0, 0, 1), stone, 0.13, 0.05, lod == 0 ? 8 : 6, lod == 0 ? 4 : 3);
            }
            // The chaitya on top.
            Profile2 p = BoudhaKit.Prof;
            p.Add(0, 1.7).Add(0.28, 1.7, true).Add(0.3, 1.82).Add(0.26, 1.98).Add(0.12, 2.06).Add(0.1, 2.15, true).Add(0.03, 2.4).Add(0, 2.42);
            Shapes.Lathe(m, sf, stone, p, lod == 0 ? 10 : lod == 1 ? 6 : 4);
            if (c != null)
            {
                double x, y, z;
                sf.Point(0, 0, 0, out x, out y, out z);
                c.AddCylinder(x, z, y, y + 2.4, 0.65);
            }
        }
    }
}
