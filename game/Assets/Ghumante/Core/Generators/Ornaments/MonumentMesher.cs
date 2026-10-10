using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Ornaments
{
    /// <summary>
    /// The non-statue centrepieces (docs/research/w2/ref_ornaments.md): Shahid Gate's stone arch with its lantern
    /// and pavilions, the Maitighar Mandala, Jawalakhel's fountain basin, clock towers, fountains and flag poles.
    /// Every builder takes the centrepiece frame (origin on the island top, +Z towards the main arm) and returns the
    /// footprint radius it occupies, so the garden keeps clear of it.
    /// </summary>
    internal static class MonumentMesher
    {
        // ------------------------------------------------------------------ Shahid Gate

        /// <summary>Shahid Gate (Martyrs' Memorial): one pointed ashlar arch with splayed legs, a lantern on its crown
        /// holding King Tribhuvan's bust under a flared cap with the national flag, two side pavilions with flared
        /// crested roofs and a martyr's bust in a pointed niche on each face, on a stepped platform with a broad
        /// flight of steps under the arch. Returns the half length of its footprint.</summary>
        public static double ShahidGate(OrnCtx c)
        {
            MeshData m = c.M;
            Affine3 f = c.CentreFrame();
            int v0 = m.VertexCount, i0 = m.IndexCount;
            ShapeBrush stone = OrnamentKit.B(OrnamentPalette.GateStone, MaterialChannel.Stone);
            ShapeBrush light = OrnamentKit.B(OrnamentPalette.GateStoneLight, MaterialChannel.Stone);
            const double y0 = 0.9, a = 5.0, hc = 9.0, pavX = 10.3, pavW = 4.6, pavD = 4.0, pavH = 2.3, wFoot = 4.4, wCrown = 1.4;
            double cx = (hc * hc - a * a) / (2 * a), ri = a + cx, phi1 = Math.Atan2(hc, cx);
            if (c.Lod >= 2)
            {
                // Far: platform, the two legs as plain ribs, lantern and pavilion blocks (LOD 2 adds the roofs, niches
                // and the flag).
                OrnamentKit.Box(m, f * Affine3.Translation(0, 0.3, 0), light, 26.4, 1.2, 7.2);
                int fr = c.Lod == 2 ? 6 : 4;
                for (int side = -1; side <= 1; side += 2) ArchLeg(c, f, stone, side, y0, cx, ri, phi1, fr, wFoot, wCrown, 1.5);
                double ly2 = y0 + hc + 0.25;
                OrnamentKit.Box(m, f * Affine3.Translation(0, ly2 + 1.55, 0), stone, 3.4, 3.4, 1.5);
                OrnamentKit.Box(m, f * Affine3.Translation(0, ly2 + 3.4, 0), light, 4.6, 0.3, 2.2);
                for (int side = -1; side <= 1; side += 2)
                {
                    Affine3 pf = f * Affine3.Translation(side * pavX, y0, 0);
                    OrnamentKit.Box(m, pf * Affine3.Translation(0, 0.5 * pavH, 0), stone, pavW, pavH, pavD);
                    if (c.Lod == 2)
                    {
                        Profile2 plan = c.P.Clear(true).SetRect(1, 1);
                        double[] ys2 = c.Ys, ss2 = c.Ss;
                        ys2[0] = 0;
                        ss2[0] = 1;
                        ys2[1] = 1.7;
                        ss2[1] = 0.12;
                        OrnamentKit.StackLoft(m, pf * Affine3.Translation(0, pavH, 0) * Affine3.Scaling(pavW + 0.5, 1, pavD + 0.5), stone, plan, ys2, ss2, 2, false, true);
                        for (int face = -1; face <= 1; face += 2)
                            Niche(c, pf * Affine3.Yaw(face > 0 ? 0 : 180) * Affine3.Translation(0, 0.35, 0.5 * pavD), light, 1.5, 2.0);
                    }
                }
                if (c.Lod == 2)
                {
                    Shapes.Cylinder(m, f * Affine3.Translation(0, ly2 + 3.55, 0), OrnamentKit.B(OrnamentPalette.Steel, MaterialChannel.Metal), 0.04, 3.0, 4, 0, 0, false, false, c.L);
                    OrnamentKit.Flag(c, f * Affine3.Translation(0.04, ly2 + 5.1, 0), 1.45);
                }
                c.Ao(v0, i0, c.CentreTopY, 0.3f);
                if (c.Stats != null) c.Stats.TopM = (float)Math.Max(c.Stats.TopM, ly2 + 6.6);
                return 13.2;
            }
            // Platform: three stepped slabs, the front flight of steps under the arch.
            for (int k = 0; k < 3; k++)
            {
                double w = 26.4 - 0.8 * k, d = 7.2 - 0.8 * k;
                Shapes.RoundedSlab(m, f * Affine3.Translation(0, 0.3 * k - (k == 0 ? 0.3 : 0), 0), light, w, d, 0.3 + (k == 0 ? 0.3 : 0), 0.08, 0.03, 1, c.L);
                if (c.Stats != null) c.Stats.Steps++;
            }
            if (c.Lod < 2)
            {
                for (int k = 0; k < 5; k++)
                {
                    double depth = 0.42 * (5 - k);
                    OrnamentKit.Box(m, f * Affine3.Translation(0, 0.09 + 0.18 * k, 3.0 + 0.5 * depth), light, 6.0, 0.18, depth);
                    if (c.Stats != null) c.Stats.Steps++;
                }
                // The blank dark plaque at the back of the platform.
                OrnamentKit.Box(m, f * Affine3.Translation(0, y0 + 0.55, -1.9), OrnamentKit.B(OrnamentPalette.Plaque, MaterialChannel.Stone), 2.6, 1.1, 0.16);
                OrnamentKit.Box(m, f * Affine3.Translation(0, y0 + 0.08, -1.9), light, 3.0, 0.16, 0.5);
            }
            // The arch: two pointed legs (intrados arcs of radius ri centred at ±cx on the platform), each a rounded
            // section that narrows from 2.6 m at the foot to 1.5 m at the crown.
            int rings = c.Lod == 0 ? 22 : 10;
            for (int side = -1; side <= 1; side += 2) ArchLeg(c, f, stone, side, y0, cx, ri, phi1, rings, wFoot, wCrown, 1.5);
            // Lantern on the crown.
            double ly = y0 + hc + 0.25;
            Lantern(c, f * Affine3.Translation(0, ly, 0), stone, light);
            // Side pavilions where the legs land.
            for (int side = -1; side <= 1; side += 2) Pavilion(c, f * Affine3.Translation(side * pavX, y0, 0), stone, light, pavW, pavD, pavH, side);
            int vs = m.VertexCount;
            ShapeColor.JitterByPosition(m, v0, vs - v0, 0.07f, c.Seed, 0.7);
            c.Ao(v0, i0, c.CentreTopY, 0.5f);
            if (c.Stats != null) c.Stats.TopM = (float)Math.Max(c.Stats.TopM, ly + 3.8 + 0.6 + 3.2);
            return 13.2;
        }

        private static void ArchLeg(OrnCtx c, in Affine3 f, in ShapeBrush b, int side, double y0, double cx, double ri, double phi1, int rings,
                                    double wFoot, double wCrown, double depth)
        {
            MeshData m = c.M;
            Profile2 sec = c.Lod >= 2 ? c.P.Clear(true).SetRect(1, 1) : c.P.Clear(true).SetRoundedRect(1, 1, 0.12, c.Lod == 0 ? 2 : 1);
            double[] px = c.Xs, py = c.Zs, pw = c.Nx;
            for (int k = 0; k < rings; k++)
            {
                double t = (double)k / (rings - 1);
                double phi = phi1 * t;
                double nxp = Math.Cos(phi), nyp = Math.Sin(phi);
                double w = wCrown + (wFoot - wCrown) * Math.Pow(1 - t, 2.4);
                px[k] = side * (-cx + ri * nxp + nxp * 0.5 * w);
                py[k] = y0 + ri * nyp + nyp * 0.5 * w;
                pw[k] = w;
            }
            OrnamentKit.PlanarSweep(m, f, b, px, py, pw, rings, depth, sec, false);
            // Block courses: thin projecting bands every few rings read as the ashlar voussoirs at a distance.
            if (c.Lod == 0)
            {
                ShapeBrush jb = OrnamentKit.B(OrnamentPalette.GateJoint, MaterialChannel.Stone);
                for (int k = 2; k < rings - 1; k += 3)
                {
                    double t = (double)k / (rings - 1), phi = phi1 * t;
                    double nxp = Math.Cos(phi), nyp = Math.Sin(phi);
                    double w = wCrown + (wFoot - wCrown) * Math.Pow(1 - t, 2.4);
                    double bx = -cx + ri * nxp + nxp * 0.5 * w, by = y0 + ri * nyp + nyp * 0.5 * w;
                    Affine3 bf = f * Affine3.Translation(side * bx, by, 0) * Affine3.RotationZ(side * phi);
                    OrnamentKit.Box(m, bf, jb, w + 0.06, 0.06, depth + 0.06);
                }
            }
        }

        private static void Lantern(OrnCtx c, in Affine3 f, in ShapeBrush stone, in ShapeBrush light)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            // Sill, two posts, the pointed-arch rib of the opening, lintel.
            Shapes.RoundedSlab(m, f * Affine3.Translation(0, -0.4, 0), stone, 3.8, 1.6, 0.75, 0.08, 0.05, 1, L);
            // Posts lean in: the lantern is a little bell tower, wider at its foot.
            Profile2 post = c.P.Clear(true).SetRoundedRect(1, 1, 0.08, 1);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                double[] py0 = c.Ys, ps0 = c.Ss;
                py0[0] = 0;
                ps0[0] = 1;
                py0[1] = 3.05;
                ps0[1] = 0.72;
                OrnamentKit.StackLoft(m, f * Affine3.Translation(sd * 1.5, 0, 0) * Affine3.RotationZ(-sd * 0.06) * Affine3.Scaling(0.75, 1, 1.5), stone, post, py0, ps0, 2, false, false);
            }
            // Lancet opening between the posts' inner faces: a rib along two arcs meeting at the apex, carried down to
            // the sill; the spandrels above it are filled.
            int seg = c.Lod == 0 ? 8 : 4;
            const double aw = 1.12, ys = 0.75, hr = 1.75, rib = 0.24;
            double cc = (hr * hr - aw * aw) / (2 * aw), rr = aw + cc, ap = Math.Atan2(hr, cc);
            double[] px = c.Xs, py = c.Zs, pw = c.Nx;
            int n = 0;
            px[n] = aw - 0.5 * rib; py[n] = -0.05; pw[n++] = rib;
            for (int k = 0; k <= seg; k++)
            {
                double phi = ap * k / seg;
                px[n] = -cc + (rr - 0.5 * rib) * Math.Cos(phi);
                py[n] = ys + (rr - 0.5 * rib) * Math.Sin(phi);
                pw[n++] = rib;
            }
            for (int k = seg - 1; k >= 0; k--)
            {
                double phi = ap * k / seg;
                px[n] = cc - (rr - 0.5 * rib) * Math.Cos(phi);
                py[n] = ys + (rr - 0.5 * rib) * Math.Sin(phi);
                pw[n++] = rib;
            }
            px[n] = -aw + 0.5 * rib; py[n] = -0.05; pw[n++] = rib;
            OrnamentKit.PlanarSweep(m, f, stone, px, py, pw, n, 1.5, c.Q.Clear(true).SetRoundedRect(1, 1, 0.1, 1), true);
            // Spandrels: solid stone between the arch and the lintel on both faces' plane.
            for (int sd = -1; sd <= 1; sd += 2)
            {
                double[] sx = c.Ax, sy = c.Ay;
                int q = 0;
                sx[q] = sd * aw; sy[q++] = ys;
                sx[q] = sd * aw; sy[q++] = 3.0;
                sx[q] = 0; sy[q++] = 3.0;
                for (int k = seg; k >= 0; k--)
                {
                    double phi = ap * k / seg;
                    sx[q] = sd * (-cc + rr * Math.Cos(phi));
                    sy[q++] = ys + rr * Math.Sin(phi);
                }
                for (int face = -1; face <= 1; face += 2)
                    OrnamentKit.FlatPoly(m, f * Affine3.Translation(0, 0, face * 0.7) * Affine3.Yaw(face > 0 ? 0 : 180) * Affine3.Scaling(face > 0 ? 1 : -1, 1, 1),
                                         OrnamentPalette.GateStone, MaterialChannel.Stone, sx, sy, q, 0.01, false);
            }
            Shapes.RoundedBox(m, f * Affine3.Translation(0, 3.25, 0), stone, 3.6, 0.5, 1.6, 0.08, 1, L);
            // Bust of King Tribhuvan in the opening on a small pedestal.
            OrnamentKit.Box(m, f * Affine3.Translation(0, 0.2, 0), light, 0.55, 0.45, 0.55);
            var spec = new StatueSpec { Pose = StatuePose.Bust, Attire = StatueAttire.RoyalPlumed, Finish = StatueFinish.Bronze, FigureM = 2.9f };
            ShapeLod keep = c.L;
            c.L = new ShapeLod(Math.Min(2, c.Lod + 1));
            StatueMesher.Bust(c, f * Affine3.Translation(0, 0.42, 0) * Affine3.Scaling(2.9 / 1.8), spec);
            c.L = keep;
            // Flared cap with up-turned corners, a stepped pyramid, finial and the flag on its mast.
            Shapes.RoundedSlab(m, f * Affine3.Translation(0, 3.5, 0), light, 4.6, 2.2, 0.32, 0.12, 0.08, 2, L);
            if (c.Lod < 2)
            {
                for (int k = 0; k < 4; k++)
                {
                    double sx = (k & 1) == 0 ? 1 : -1, sz = (k & 2) == 0 ? 1 : -1;
                    Affine3 hf = f * Affine3.Translation(sx * 2.2, 3.7, sz * 1.0) * Affine3.RotationZ(-sx * 0.7) * Affine3.RotationX(sz * 0.5);
                    Shapes.Cone(m, hf, light, 0.16, 0.42, 6, 0, 0, true, L);
                }
            }
            Profile2 sq = c.P.Clear(true).SetRoundedRect(1, 1, 0.08, 1);
            double[] hy = c.Ys, hs = c.Ss;
            hy[0] = 0; hs[0] = 1.7;
            hy[1] = 0.25; hs[1] = 1.2;
            hy[2] = 0.55; hs[2] = 0.45;
            OrnamentKit.StackLoft(m, f * Affine3.Translation(0, 3.82, 0), stone, sq, hy, hs, 3, false, true, 0.55);
            Shapes.Sphere(m, f * Affine3.Translation(0, 4.55, 0), stone, 0.2, 10, L);
            ShapeBrush steel = OrnamentKit.B(OrnamentPalette.Steel, MaterialChannel.Metal);
            Shapes.Cylinder(m, f * Affine3.Translation(0, 4.6, 0), steel, 0.035, 3.0, 6, 0, 0, false, true, L);
            OrnamentKit.Flag(c, f * Affine3.Translation(0.04, 6.1, 0), 1.45);
        }

        private static void Pavilion(OrnCtx c, in Affine3 f, in ShapeBrush stone, in ShapeBrush light, double w, double d, double h, int side)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            Shapes.RoundedBox(m, f * Affine3.Translation(0, 0.5 * h, 0), stone, w, h, d, 0.08, 1, L);
            Shapes.RoundedSlab(m, f * Affine3.Translation(0, h, 0), light, w + 1.4, d + 1.4, 0.14, 0.2, 0.06, 1, L);
            // Broad flared hipped roof: concave sides from the wide eave up to a short ridge, then a finial ball.
            Profile2 plan = c.P.Clear(true).SetRoundedRect(1, 1, 0.06, 1);
            int rings = c.Lod == 0 ? 7 : 4;
            double[] ys = c.Ys, ss = c.Ss;
            for (int k = 0; k < rings; k++)
            {
                double t = (double)k / (rings - 1);
                ys[k] = 1.2 * t;
                ss[k] = 0.1 + 0.9 * Math.Pow(1 - t, 2.6);
            }
            OrnamentKit.StackLoft(m, f * Affine3.Translation(0, h + 0.14, 0) * Affine3.Scaling(w + 1.3, 1, d + 1.3), stone, plan, ys, ss, rings, false, true);
            if (c.Lod == 0)
            {
                // Crested hips: beads from each eave corner to the ridge.
                ShapeBrush crest = light;
                for (int k = 0; k < 4; k++)
                {
                    double sx = (k & 1) == 0 ? 1 : -1, sz = (k & 2) == 0 ? 1 : -1;
                    Path3 hp = c.Path2.Clear();
                    for (int q = 0; q < rings; q++)
                    {
                        double sc = ss[q];
                        hp.Add(sx * 0.5 * sc * (w + 1.3), h + 0.2 + ys[q], sz * 0.5 * sc * (d + 1.3));
                    }
                    Shapes.Tube(m, f, crest, hp, 0.07, 6, true, false, L);
                }
                Shapes.Sphere(m, f * Affine3.Translation(0, h + 1.42, 0), light, 0.22, 10, L);
            }
            // Pointed niches with a martyr's bust on the front and back faces.
            ShapeLod saved = c.L;
            c.L = new ShapeLod(Math.Min(2, c.Lod + 1));
            for (int face = -1; face <= 1; face += 2)
            {
                Affine3 nf = f * Affine3.Yaw(face > 0 ? 0 : 180) * Affine3.Translation(0, 0.35, 0.5 * d);
                Niche(c, nf, light, 1.7, 2.1);
                if (c.Lod == 0)
                {
                    OrnamentKit.Box(m, nf * Affine3.Translation(0, 0.3, 0.2), light, 0.6, 0.6, 0.5);
                    var spec = new StatueSpec
                    {
                        Pose = StatuePose.Bust, Attire = (side + face) % 4 == 0 ? StatueAttire.DauraTopi : StatueAttire.Coat, Finish = StatueFinish.Bronze, FigureM = 2.2f,
                        Glasses = side < 0 && face > 0,
                    };
                    StatueMesher.Bust(c, nf * Affine3.Translation(0, 0.6, 0.22) * Affine3.Scaling(2.2 / 1.8), spec);
                }
            }
            c.L = saved;
        }

        /// <summary>A pointed-arch niche on the wall plane (frame XY at z = 0, front +Z): dark recess panel and a
        /// stone frame.</summary>
        private static void Niche(OrnCtx c, in Affine3 f, in ShapeBrush frame, double w, double h)
        {
            MeshData m = c.M;
            double[] px = c.Xs, py = c.Zs;
            int seg = c.Lod == 0 ? 6 : 3;
            double hw = 0.5 * w, spring = h - 1.1 * hw, cc = (Math.Pow(h - spring, 2) - hw * hw) / (2 * hw), rr = hw + cc;
            double ap = Math.Atan2(h - spring, cc);
            int n = 0;
            px[n] = -hw; py[n++] = 0;
            px[n] = hw; py[n++] = 0;
            for (int k = 0; k <= seg; k++)
            {
                double phi = ap * k / seg;
                px[n] = -cc + rr * Math.Cos(phi);
                py[n++] = spring + rr * Math.Sin(phi);
            }
            for (int k = seg - 1; k >= 0; k--)
            {
                double phi = ap * k / seg;
                px[n] = cc - rr * Math.Cos(phi);
                py[n++] = spring + rr * Math.Sin(phi);
            }
            OrnamentKit.FlatPoly(m, f * Affine3.Translation(0, 0, 0.015), OrnamentPalette.NicheDark, MaterialChannel.Stone, px, py, n, 0.01, false);
            if (c.Lod == 0)
            {
                // Frame moulding: up the right jamb, round the pointed head, down the left jamb.
                double[] fx = c.Ax, fy = c.Ay, fw = c.Aw;
                int q = 0;
                for (int k = 1; k < n; k++)
                {
                    fx[q] = px[k];
                    fy[q] = py[k];
                    fw[q++] = 0.16;
                }
                fx[q] = px[0];
                fy[q] = py[0];
                fw[q++] = 0.16;
                OrnamentKit.PlanarSweep(m, f * Affine3.Translation(0, 0, 0.05), frame, fx, fy, fw, q, 0.12, c.Q.Clear(true).SetRoundedRect(1, 1, 0.15, 1), true);
            }
        }

        // ------------------------------------------------------------------ Maitighar Mandala

        /// <summary>The Maitighar Mandala: a 24 m square platform with the coloured mandala inlay (teal border, coloured
        /// corner fields, concentric bands, 16 lotus petals, 32 gilt vajras, 32 garland beads, a small central stupa),
        /// a marigold collar round the square and a colonnade of white columns with green capitals on the main-arm
        /// side. Returns the footprint radius.</summary>
        public static double Mandala(OrnCtx c)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            Affine3 f = c.CentreFrame();
            int v0 = m.VertexCount, i0 = m.IndexCount;
            const double half = 12.0, pt = 0.32;
            if (c.Lod >= 2)
            {
                // Far: the platform, the main coloured bands, the stupa, the marigold ring and the colonnade line.
                OrnamentKit.Box(m, f * Affine3.Translation(0, 0.5 * pt - 0.1, 0), OrnamentKit.B(OrnamentPalette.MandalaTeal, MaterialChannel.Paint), 2 * half, pt + 0.2, 2 * half);
                int fr = c.Lod == 2 ? 16 : 10;
                Affine3 tf = f * Affine3.Translation(0, pt, 0);
                for (int k = 0; k < (c.Lod == 2 ? 3 : 1); k++)
                {
                    double ro = k == 0 ? 10.6 : k == 1 ? 8.75 : 4.05, rin = k == 0 ? 9.35 : k == 1 ? 6.65 : 1.8;
                    uint bandCol = k == 0 ? OrnamentPalette.MandalaYellow : k == 1 ? OrnamentPalette.MandalaBlue : OrnamentPalette.MandalaWhite;
                    Shapes.Lathe(m, tf, OrnamentKit.B(bandCol, MaterialChannel.Paint), c.Q.Clear(false).Add(ro, 0, true).Add(ro, 0.03 + 0.01 * k, true).Add(rin, 0.03 + 0.01 * k, true), fr, L);
                }
                if (c.Lod == 2)
                {
                    Shapes.Dome(m, tf, OrnamentKit.B(OrnamentPalette.MandalaWhite, MaterialChannel.Plaster), 1.4, 1.3, 8, false, L);
                    Shapes.Cone(m, tf * Affine3.Translation(0, 1.25, 0), OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt), 0.25, 1.4, 5, 0, 0, false, L);
                    Profile2 ring = c.Q.Clear(false).Add(15.0, 0, true).Add(14.8, 0.4, true).Add(12.6, 0.4, true).Add(12.4, 0, true);
                    Shapes.Lathe(m, f * Affine3.Translation(0, -0.05, 0), OrnamentKit.B(OrnamentPalette.MarigoldYellow, MaterialChannel.Foliage), ring, 16, L);
                    ShapeBrush cw = OrnamentKit.B(OrnamentPalette.ColumnWhite, MaterialChannel.Plaster);
                    for (int k = 0; k < 10; k++)
                    {
                        double u = -9.45 + 2.1 * k, x, z;
                        c.Local(u, 16.0, out x, out z);
                        Shapes.Cylinder(m, Affine3.Translation(x, c.TopY(x, z), z), cw, 0.18, 3.3, 4, 0, 0, false, false, L);
                    }
                    double bx, bz;
                    c.Local(0, 16.0, out bx, out bz);
                    OrnamentKit.Box(m, Affine3.Translation(bx, c.TopY(bx, bz) + 3.4, bz) * Affine3.Yaw(c.FacingDeg), cw, 19.7, 0.3, 0.35);
                }
                c.Ao(v0, i0, c.CentreTopY, 0.3f);
                return 16.8;
            }
            Shapes.RoundedSlab(m, f, OrnamentKit.B(OrnamentPalette.MandalaTeal, MaterialChannel.Paint), 2 * half, 2 * half, pt, 0.3, 0.05, 1, L);
            Affine3 top = f * Affine3.Translation(0, pt, 0);
            // Pale edge line just inside the border.
            ShapeBrush edge = OrnamentKit.B(OrnamentPalette.MandalaEdge, MaterialChannel.Paint);
            for (int k = 0; k < 4; k++)
            {
                Affine3 ef = top * Affine3.Yaw(90 * k) * Affine3.Translation(0, 0.005, half - 0.9);
                OrnamentKit.Box(m, ef, edge, 2 * (half - 0.9) + 0.3, 0.012, 0.3);
            }
            // Corner fields (square minus circle), one colour per quadrant.
            double rc = 10.6, inner = half - 1.05;
            uint[] cornerCols = s_cornerCols;
            int arc = c.Lod == 0 ? 10 : 5;
            for (int q = 0; q < 4; q++)
            {
                double[] px = c.Xs, pz = c.Zs;
                int n = 0;
                px[n] = inner; pz[n++] = 0;
                px[n] = inner; pz[n++] = inner;
                px[n] = 0; pz[n++] = inner;
                for (int k = 0; k <= arc; k++)
                {
                    double a = 0.5 * Math.PI * k / arc;
                    px[n] = rc * Math.Sin(a);
                    pz[n++] = rc * Math.Cos(a);
                }
                Shapes.BevelExtrude(m, top * Affine3.Yaw(90 * q), OrnamentKit.B(cornerCols[q], MaterialChannel.Paint), px, pz, n, 0.018, 0, 0,
                                    BevelStyle.Chamfer, true, false, 0, 30, L);
                // Ashtamangala emblem disc in the corner field.
                if (c.Lod == 0)
                {
                    Affine3 ef = top * Affine3.Yaw(90 * q + 45) * Affine3.Translation(0, 0, 13.2);
                    Shapes.Cylinder(m, ef, OrnamentKit.B(OrnamentPalette.MandalaWhite, MaterialChannel.Paint), 0.9, 0.05, 16, 0, 0, false, true, L);
                    Shapes.Cylinder(m, ef, OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt), 0.55, 0.11, 12, 0.03, 1, false, true, L);
                }
            }
            // Concentric bands (outer to inner), each a thin raised annulus.
            int rad = c.Lod == 0 ? 40 : 18;
            for (int k = 0; k < s_bandR.Length - 1; k++)
            {
                double ro = s_bandR[k], rin = s_bandR[k + 1], h = 0.022 + 0.006 * k;
                Profile2 p = c.Q.Clear(false).Add(ro, 0, true).Add(ro, h, true).Add(rin, h, true);
                Shapes.Lathe(m, top, OrnamentKit.B(s_bandCol[k], MaterialChannel.Paint), p, Math.Max(16, (int)(rad * ro / 10.6)), L);
            }
            double hTop = 0.022 + 0.006 * (s_bandR.Length - 1);
            if (c.Lod == 1)
            {
                // Lotus petals as flat inlays.
                for (int k = 0; k < 16; k++)
                {
                    double[] px = c.Xs, pz = c.Zs;
                    px[0] = 0; pz[0] = 0.02;
                    px[1] = 0.55; pz[1] = 0.9;
                    px[2] = 0; pz[2] = 1.8;
                    px[3] = -0.55; pz[3] = 0.9;
                    OrnamentKit.FlatPoly(m, top * Affine3.Translation(0, hTop + 0.005, 0) * Affine3.Yaw(22.5 * k) * Affine3.Translation(0, 0, 4.7) * Affine3.RotationX(Math.PI / 2),
                                         (k & 1) == 0 ? OrnamentPalette.RosePink : OrnamentPalette.MandalaWhite, MaterialChannel.Paint, px, pz, 4, 0.004, false);
                }
            }
            if (c.Lod == 0)
            {
                // 32 gilt vajras on the outer blue band.
                int nv = 32;
                for (int k = 0; k < nv; k++)
                {
                    double a = 360.0 * k / nv;
                    Affine3 vf = top * Affine3.Yaw(a) * Affine3.Translation(0, 0.1, 7.95);
                    Shapes.Sphere(m, vf * Affine3.Scaling(0.32, 0.16, 1.0), OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt), 0.5, 5, L);
                }
                // 16 lotus petals, alternating pink and white.
                for (int k = 0; k < 16; k++)
                {
                    double a = 22.5 * k;
                    double[] px = c.Xs, pz = c.Zs;
                    int n = 0;
                    const int ps = 6;
                    for (int q = 0; q <= ps; q++)
                    {
                        double t = (double)q / ps;
                        px[n] = 0.55 * Math.Sin(Math.PI * t);
                        pz[n++] = 4.7 + 1.8 * t;
                    }
                    for (int q = ps - 1; q > 0; q--)
                    {
                        double t = (double)q / ps;
                        px[n] = -0.55 * Math.Sin(Math.PI * t);
                        pz[n++] = 4.7 + 1.8 * t;
                    }
                    Shapes.BevelExtrude(m, top * Affine3.Translation(0, hTop - 0.02, 0) * Affine3.Yaw(a),
                                        OrnamentKit.B((k & 1) == 0 ? OrnamentPalette.RosePink : OrnamentPalette.MandalaWhite, MaterialChannel.Paint), px, pz, n,
                                        0.06, 0, 0, BevelStyle.Chamfer, true, false, 0, 40, L);
                }
                // 32 garland beads, alternating black and blue.
                if (c.Lod == 0)
                {
                    for (int k = 0; k < 32; k += 2)
                    {
                        double a = 11.25 * k * Math.PI / 180;
                        Shapes.Sphere(m, top * Affine3.Translation(3.45 * Math.Sin(a), hTop + 0.06, 3.45 * Math.Cos(a)),
                                      OrnamentKit.B((k & 1) == 0 ? OrnamentPalette.MandalaBlack : OrnamentPalette.MandalaBlue, MaterialChannel.Paint), 0.17, 5, L);
                    }
                }
            }
            // Central stupa: two round steps, a white dome, harmika and gilt spire.
            ShapeBrush white = OrnamentKit.B(OrnamentPalette.MandalaWhite, MaterialChannel.Plaster);
            int sr = c.Lod == 0 ? 24 : 12;
            Shapes.Cylinder(m, top, white, 1.7, 0.25, sr, 0.04, 1, false, true, L);
            Shapes.Cylinder(m, top * Affine3.Translation(0, 0.25, 0), white, 1.45, 0.2, sr, 0.04, 1, false, true, L);
            Shapes.Dome(m, top * Affine3.Translation(0, 0.45, 0), white, 1.25, 1.0, sr, false, L);
            ShapeBrush gilt = OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt);
            Shapes.RoundedBox(m, top * Affine3.Translation(0, 1.6, 0), gilt, 0.55, 0.35, 0.55, 0.04, 1, L);
            Shapes.Frustum(m, top * Affine3.Translation(0, 1.78, 0), gilt, 0.26, 0.06, 1.0, 12, 0, 0, false, true, L);
            if (c.Lod < 2)
            {
                for (int k = 0; k < 5; k++) Shapes.Torus(m, top * Affine3.Translation(0, 1.85 + 0.17 * k, 0), gilt, 0.24 - 0.035 * k, 0.03, 12, 4, L);
                Shapes.Cylinder(m, top * Affine3.Translation(0, 2.78, 0), gilt, 0.3, 0.05, 12, 0.02, 1, true, true, L);
                Shapes.Sphere(m, top * Affine3.Translation(0, 2.95, 0), gilt, 0.1, 8, L);
            }
            c.Ao(v0, i0, c.CentreTopY, 0.3f);
            // Marigold collar round the square (a squircle band, mostly yellow).
            Path3 col = c.Path.Clear();
            int segs = c.Lod == 0 ? 96 : 36;
            for (int k = 0; k < segs; k++)
            {
                double a = 2 * Math.PI * k / segs, sa = Math.Sin(a), ca = Math.Cos(a);
                double r = 13.4 / Math.Pow(Math.Pow(Math.Abs(sa), 5) + Math.Pow(Math.Abs(ca), 5), 0.2);
                double lx, lz;
                c.Local(r * sa, r * ca, out lx, out lz);
                col.Add(lx, c.TopY(lx, lz), lz);
            }
            OrnamentKit.Mound(c, col, true, 1.9, 0.42, OrnamentPalette.MarigoldYellow, OrnamentPalette.MarigoldOrange, MaterialChannel.Foliage, 0.07,
                              c.Seed ^ 0xA11u, 0.45);
            if (c.Lod == 0)
            {
                for (int k = 0; k < segs; k += 3)
                {
                    double x = col.X[k], z = col.Z[k];
                    OrnamentKit.Bloom(c, x + 0.4 * (OrnamentSeed.Unit(c.Seed, k) - 0.5), col.Y[k] + 0.44, z + 0.4 * (OrnamentSeed.Unit(c.Seed, k + 999) - 0.5), 0.11,
                                      (k % 5) == 0 ? OrnamentPalette.MarigoldOrange : OrnamentPalette.MarigoldYellow);
                }
            }
            Colonnade(c, f, 16.0, 10, 2.1);
            return 16.8;
        }

        private static readonly uint[] s_cornerCols =
        {
            OrnamentPalette.MandalaRed, OrnamentPalette.MandalaBlue, OrnamentPalette.MandalaYellow, OrnamentPalette.MandalaGreen,
        };

        private static readonly double[] s_bandR = { 10.6, 10.0, 9.35, 8.75, 7.2, 6.65, 4.6, 4.05, 2.9, 1.8 };

        private static readonly uint[] s_bandCol =
        {
            OrnamentPalette.MandalaYellow, OrnamentPalette.MandalaOrange, OrnamentPalette.MandalaRed, OrnamentPalette.MandalaBlue,
            OrnamentPalette.MandalaWhite, OrnamentPalette.MandalaBlue, OrnamentPalette.MandalaOrange, OrnamentPalette.MandalaWhite,
            OrnamentPalette.MandalaYellow,
        };

        /// <summary>A row of white columns with green capitals and bases carrying a light beam frame (Maitighar), at
        /// distance <paramref name="dist"/> in front of the centre.</summary>
        private static void Colonnade(OrnCtx c, in Affine3 f, double dist, int columns, double spacing)
        {
            if (c.Lod >= 3) return;
            MeshData m = c.M;
            ShapeLod L = c.L;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            ShapeBrush white = OrnamentKit.B(OrnamentPalette.ColumnWhite, MaterialChannel.Plaster);
            ShapeBrush green = OrnamentKit.B(OrnamentPalette.ColumnGreen, MaterialChannel.Paint);
            double span = (columns - 1) * spacing;
            double maxY = 0;
            for (int k = 0; k < columns; k++)
            {
                double u = -0.5 * span + spacing * k, x, z;
                c.Local(u, dist, out x, out z);
                float y = c.TopY(x, z);
                Affine3 cf = Affine3.Translation(x, y, z) * Affine3.Yaw(c.FacingDeg);
                OrnamentKit.Box(m, cf * Affine3.Translation(0, 0.15, 0), green, 0.5, 0.3, 0.5);
                Shapes.Cylinder(m, cf * Affine3.Translation(0, 0.3, 0), white, 0.16, 2.75, c.Lod == 0 ? 8 : 6, 0, 0, false, false, L);
                Shapes.Frustum(m, cf * Affine3.Translation(0, 3.05, 0), green, 0.17, 0.3, 0.25, c.Lod == 0 ? 8 : 6, 0, 0, false, true, L);
                if (c.Lod == 0) OrnamentKit.Box(m, cf * Affine3.Translation(0, 3.42, 0), green, 0.2, 0.2, 1.5);
                maxY = Math.Max(maxY, y);
            }
            double cxp, czp;
            c.Local(0, dist, out cxp, out czp);
            Affine3 bf = Affine3.Translation(cxp, maxY + 3.4, czp) * Affine3.Yaw(c.FacingDeg);
            OrnamentKit.Box(m, bf, white, span + 0.8, 0.26, 0.34);
            if (c.Lod == 0)
            {
                OrnamentKit.Box(m, bf * Affine3.Translation(0, 0.18, 0.65), white, span + 0.8, 0.16, 0.16);
                OrnamentKit.Box(m, bf * Affine3.Translation(0, 0.18, -0.65), white, span + 0.8, 0.16, 0.16);
            }
            c.Ao(v0, i0, c.CentreTopY, 0.4f);
        }

        // ------------------------------------------------------------------ Jawalakhel basin

        /// <summary>Jawalakhel's round sunken fountain basin (cream rim, terrace, step, water, white tubular railing,
        /// globe lamps and small jets) with the stone spire and King Birendra's statue rising from it. Returns the
        /// footprint radius.</summary>
        public static double SpireBasin(OrnCtx c, in StatueSpec statue)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            Affine3 f = c.CentreFrame();
            int v0 = m.VertexCount, i0 = m.IndexCount;
            const double ro = 7.6;
            ShapeBrush rim = OrnamentKit.B(OrnamentPalette.BasinRim, MaterialChannel.Stone);
            int rad = c.Lod == 0 ? 40 : c.Lod == 1 ? 20 : c.Lod == 2 ? 12 : 8;
            Profile2 p = c.Q.Clear(false);
            if (c.Lod >= 2) p.Add(ro, -0.1, true).Add(ro, 0.48, true).Add(ro - 0.5, 0.48, true);
            else p.Add(ro, -0.1, true).Add(ro, 0.42, true).Add(ro - 0.06, 0.48).Add(ro - 0.5, 0.48, true).Add(ro - 0.5, 0.05, true)
                  .Add(ro - 1.3, 0.05, true).Add(ro - 1.3, -0.15, true).Add(ro - 1.5, -0.15, true);
            Shapes.Lathe(m, f, rim, p, rad, L);
            // Water.
            Profile2 w = c.Q.Clear(false).Add(c.Lod >= 2 ? ro - 0.5 : ro - 1.45, -0.12, true).Add(2.4, -0.12, true);
            Shapes.Lathe(m, f, OrnamentKit.B(OrnamentPalette.Water, MaterialChannel.Water), w, rad, L);
            if (c.Lod == 1)
            {
                Shapes.Torus(m, f * Affine3.Translation(0, 0.95, 0), OrnamentKit.B(OrnamentPalette.PaintWhite, MaterialChannel.Metal), ro - 1.35, 0.04, rad, 3, L);
            }
            if (c.Lod == 0)
            {
                // White tubular railing on the terrace edge.
                ShapeBrush rail = OrnamentKit.B(OrnamentPalette.PaintWhite, MaterialChannel.Metal);
                double rr = ro - 1.35;
                Shapes.Torus(m, f * Affine3.Translation(0, 0.95, 0), rail, rr, 0.03, rad, 4, L);
                if (c.Lod == 0) Shapes.Torus(m, f * Affine3.Translation(0, 0.5, 0), rail, rr, 0.022, rad, 3, L);
                int posts = c.Lod == 0 ? 14 : 8;
                for (int k = 0; k < posts; k++)
                {
                    double a = 2 * Math.PI * k / posts;
                    Shapes.Bar(m, f, rail, rr * Math.Sin(a), 0.05, rr * Math.Cos(a), rr * Math.Sin(a), 0.95, rr * Math.Cos(a), 0.025, 4, L);
                    if (c.Stats != null) c.Stats.RailingPosts++;
                }
                // Globe lamps on the rim.
                int lamps = 8;
                for (int k = 0; k < lamps; k++)
                {
                    double a = 2 * Math.PI * (k + 0.5) / lamps, ox, oy, oz;
                    f.Point((ro - 0.25) * Math.Sin(a), 0.48, (ro - 0.25) * Math.Cos(a), out ox, out oy, out oz);
                    OrnamentKit.GlobeLamp(c, ox, oy, oz, 0.55);
                }
                // Small water jets round the spire.
                ShapeBrush jet = OrnamentKit.B(OrnamentPalette.WaterJet, MaterialChannel.Water);
                int jets = c.Lod == 0 ? 12 : 6;
                for (int k = 0; k < jets; k++)
                {
                    double a = 2 * Math.PI * (k + 0.5) / jets, jr = 4.4;
                    Path3 jp = c.Path2.Clear();
                    for (int q = 0; q <= 5; q++)
                    {
                        double t = q / 5.0;
                        jp.Add((jr - 1.3 * t) * Math.Sin(a), -0.12 + 1.6 * t * (1 - t) * 1.6, (jr - 1.3 * t) * Math.Cos(a));
                    }
                    Shapes.Tube(m, f, jet, jp, 0.05, 5, true, false, L, 0.02);
                    if (c.Stats != null) c.Stats.Jets++;
                }
            }
            c.Ao(v0, i0, c.CentreTopY - 0.2, 0.4f);
            // Spire and statue standing on the basin floor.
            double crown = StatueMesher.Build(c, f * Affine3.Translation(0, -0.15, 0), statue, c.CentreTopY - 0.15);
            if (c.Stats != null) c.Stats.TopM = (float)Math.Max(c.Stats.TopM, crown - 0.15);
            return ro + 0.3;
        }

        // ------------------------------------------------------------------ clock tower, fountain, flag pole

        /// <summary>A slim square clock tower: stone base, cream shaft with brick trim bands, the clock stage with four
        /// faces, cornice, small dome and finial. Returns the footprint radius.</summary>
        public static double ClockTower(OrnCtx c, double height)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            Affine3 f = c.CentreFrame();
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double w = Math.Max(1.6, 0.18 * height);
            ShapeBrush wall = OrnamentKit.B(OrnamentPalette.TowerWall, MaterialChannel.Plaster);
            ShapeBrush trim = OrnamentKit.B(OrnamentPalette.TowerTrim, MaterialChannel.Brick);
            ShapeBrush stone = OrnamentKit.B(OrnamentPalette.StepGrey, MaterialChannel.Stone);
            double shaftH = height - 3.6;
            if (c.Lod >= 1)
            {
                OrnamentKit.Box(m, f * Affine3.Translation(0, 0.4, 0), stone, w + 1.0, 0.8, w + 1.0);
                OrnamentKit.Box(m, f * Affine3.Translation(0, 0.8 + 0.5 * shaftH, 0), wall, w, shaftH, w);
                OrnamentKit.Box(m, f * Affine3.Translation(0, 0.8 + shaftH + 1.0, 0), wall, w + 0.4, 2.0, w + 0.4);
                if (c.Lod <= 2)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        if (c.Lod == 1)
                        {
                            Affine3 cf1 = f * Affine3.Yaw(90 * k) * Affine3.Translation(0, 0.8 + shaftH + 0.95, 0.5 * (w + 0.4)) * Affine3.RotationX(Math.PI / 2);
                            Shapes.Cylinder(m, cf1, OrnamentKit.B(OrnamentPalette.ClockFace, MaterialChannel.Paint), 0.62, 0.05, 12, 0, 0, false, true, L);
                        }
                        else OrnamentKit.Panel(m, f * Affine3.Yaw(90 * k), OrnamentPalette.ClockFace, MaterialChannel.Paint, -0.55, 0.8 + shaftH + 0.4, 0.55, 0.8 + shaftH + 1.5, 0.5 * (w + 0.4) + 0.01);
                    }
                    if (c.Lod == 1)
                        for (double y = 2.4; y < shaftH; y += 2.6)
                            OrnamentKit.Box(m, f * Affine3.Translation(0, 0.88 + y, 0), trim, w + 0.14, 0.16, w + 0.14);
                    Shapes.Dome(m, f * Affine3.Translation(0, 0.8 + shaftH + 2.0, 0), wall, 0.5 * w + 0.15, 0.9, c.Lod == 1 ? 10 : 8, false, L);
                    if (c.Lod == 1) Shapes.Cone(m, f * Affine3.Translation(0, 0.8 + shaftH + 2.85, 0), OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt), 0.1, 0.6, 5, 0, 0, false, L);
                }
                c.Ao(v0, i0, c.CentreTopY, 0.3f);
                if (c.Stats != null) c.Stats.TopM = (float)Math.Max(c.Stats.TopM, 0.8 + shaftH + 2.9);
                return 0.5 * (w + 1.4) * 1.42;
            }
            Shapes.RoundedSlab(m, f, stone, w + 1.4, w + 1.4, 0.45, 0.08, 0.04, 1, L);
            Shapes.RoundedSlab(m, f * Affine3.Translation(0, 0.45, 0), stone, w + 0.7, w + 0.7, 0.35, 0.06, 0.04, 1, L);
            Shapes.RoundedBox(m, f * Affine3.Translation(0, 0.8 + 0.5 * shaftH, 0), wall, w, shaftH, w, 0.06, 1, L);
            if (c.Lod < 3)
            {
                for (double y = 2.4; y < shaftH; y += 2.6)
                    OrnamentKit.Box(m, f * Affine3.Translation(0, 0.88 + y, 0), trim, w + 0.14, 0.16, w + 0.14);
            }
            double sy = 0.8 + shaftH;
            Shapes.RoundedBox(m, f * Affine3.Translation(0, sy + 0.95, 0), wall, w + 0.4, 1.9, w + 0.4, 0.08, 1, L);
            Shapes.RoundedSlab(m, f * Affine3.Translation(0, sy + 1.9, 0), trim, w + 0.7, w + 0.7, 0.22, 0.06, 0.04, 1, L);
            if (c.Lod < 3)
            {
                for (int k = 0; k < 4; k++)
                {
                    Affine3 cf = f * Affine3.Yaw(90 * k) * Affine3.Translation(0, sy + 0.95, 0.5 * (w + 0.4)) * Affine3.RotationX(Math.PI / 2);
                    Shapes.Cylinder(m, cf, OrnamentKit.B(OrnamentPalette.ClockFace, MaterialChannel.Paint), 0.62, 0.05, 16, 0, 0, false, true, L);
                    if (c.Lod < 2)
                    {
                        Shapes.Torus(m, cf * Affine3.Translation(0, 0.05, 0), trim, 0.62, 0.05, 16, 3, L);
                        // Hands at ten past ten.
                        Affine3 hf = f * Affine3.Yaw(90 * k) * Affine3.Translation(0, sy + 0.95, 0.5 * (w + 0.4) + 0.08);
                        OrnamentKit.Box(m, hf * Affine3.RotationZ(-1.0) * Affine3.Translation(0, 0.2, 0), OrnamentKit.B(OrnamentPalette.IronBlack, MaterialChannel.Metal), 0.05, 0.4, 0.02);
                        OrnamentKit.Box(m, hf * Affine3.RotationZ(1.05) * Affine3.Translation(0, 0.13, 0), OrnamentKit.B(OrnamentPalette.IronBlack, MaterialChannel.Metal), 0.06, 0.28, 0.02);
                        // A dark slit window below the clock.
                        Affine3 wf = f * Affine3.Yaw(90 * k);
                        OrnamentKit.Panel(m, wf, OrnamentPalette.NicheDark, MaterialChannel.Glass, -0.18, sy - 2.4, 0.18, sy - 0.8, 0.5 * w + 0.005);
                    }
                }
            }
            Shapes.Dome(m, f * Affine3.Translation(0, sy + 2.12, 0), wall, 0.5 * w + 0.15, 0.9, 12, true, L);
            ShapeBrush gilt = OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt);
            Shapes.Sphere(m, f * Affine3.Translation(0, sy + 3.15, 0), gilt, 0.14, 10, L);
            Shapes.Cone(m, f * Affine3.Translation(0, sy + 3.2, 0), gilt, 0.05, 0.55, 6, 0, 0, false, L);
            c.Ao(v0, i0, c.CentreTopY, 0.4f);
            if (c.Stats != null) c.Stats.TopM = (float)Math.Max(c.Stats.TopM, sy + 3.75);
            return 0.5 * (w + 1.4) * 1.42;
        }

        /// <summary>A round fountain of basin radius <paramref name="r"/>: rim, water, a central column with a bowl,
        /// a central jet, the falling curtain from the bowl and rim jets arching inward. Returns the footprint radius.</summary>
        public static double Fountain(OrnCtx c, double r)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            Affine3 f = c.CentreFrame();
            int v0 = m.VertexCount, i0 = m.IndexCount;
            ShapeBrush rim = OrnamentKit.B(OrnamentPalette.BasinRim, MaterialChannel.Stone);
            ShapeBrush water = OrnamentKit.B(OrnamentPalette.Water, MaterialChannel.Water);
            ShapeBrush jet = OrnamentKit.B(OrnamentPalette.WaterJet, MaterialChannel.Water);
            int rad = c.Lod == 0 ? 28 : c.Lod == 1 ? 16 : c.Lod == 2 ? 10 : 8;
            Profile2 p = c.Q.Clear(false);
            if (c.Lod >= 2) p.Add(r, -0.05, true).Add(r, 0.5, true).Add(r - 0.4, 0.5, true);
            else p.Add(r, -0.05, true).Add(r, 0.42, true).Add(r - 0.05, 0.5).Add(r - 0.4, 0.5, true).Add(r - 0.4, 0.1, true);
            Shapes.Lathe(m, f, rim, p, rad, L);
            Shapes.Lathe(m, f, water, c.Q.Clear(false).Add(r - 0.38, 0.3, true).Add(0, 0.3, true), rad, L);
            if (c.Lod >= 3) return r;
            if (c.Lod == 2)
            {
                Shapes.Cylinder(m, f, rim, 0.35, 1.3, 5, 0, 0, false, true, L);
                Shapes.Cone(m, f * Affine3.Translation(0, 1.3, 0), jet, 0.25, 2.0, 5, 0, 0, false, L);
                return r + 0.2;
            }
            // Column and bowl.
            Profile2 col = c.Q.Clear(false);
            col.Add(0.45, 0.2, true).Add(0.3, 0.5).Add(0.22, 1.1).Add(0.3, 1.25, true).Add(1.25, 1.35).Add(1.35, 1.48, true).Add(1.2, 1.5, true)
               .Add(0.2, 1.45).Add(0, 1.45, true);
            Shapes.Lathe(m, f, rim, col, rad / 2 + 4, L);
            Shapes.Lathe(m, f, water, c.Q.Clear(false).Add(1.22, 1.47, true).Add(0, 1.47, true), rad / 2 + 4, L);
            Shapes.Frustum(m, f * Affine3.Translation(0, 1.45, 0), jet, 0.12, 0.03, 1.9, 8, 0, 0, false, true, L);
            Shapes.Sphere(m, f * Affine3.Translation(0, 3.3, 0), jet, 0.12, 8, L);
            if (c.Stats != null) c.Stats.Jets++;
            if (c.Lod < 2)
            {
                // Falling curtain from the bowl lip.
                Profile2 cur = c.Q.Clear(false).Add(1.35, 1.46, false).Add(1.45, 1.0).Add(1.52, 0.2, false);
                Shapes.Lathe(m, f, jet, cur, rad / 2 + 4, L);
                int jets = c.Lod == 0 ? 10 : 4;
                for (int k = 0; k < jets; k++)
                {
                    double a = 2 * Math.PI * (k + 0.5) / jets;
                    Path3 jp = c.Path2.Clear();
                    double r0 = r - 0.45, r1 = 1.9;
                    for (int q = 0; q <= 6; q++)
                    {
                        double t = q / 6.0, rr = r0 + (r1 - r0) * t;
                        jp.Add(rr * Math.Sin(a), 0.5 + 2.2 * t * (1 - t) * (r0 - r1) / 3.0 * 1.2, rr * Math.Cos(a));
                    }
                    Shapes.Tube(m, f, jet, jp, 0.045, 4, true, false, L, 0.02);
                    if (c.Stats != null) c.Stats.Jets++;
                }
            }
            c.Ao(v0, i0, c.CentreTopY, 0.4f);
            if (c.Stats != null) c.Stats.TopM = (float)Math.Max(c.Stats.TopM, 3.45);
            return r + 0.2;
        }

        /// <summary>A national flag pole of <paramref name="height"/> at tile-local (x, z) on a round plinth, the flag
        /// flying towards <paramref name="windDeg"/>.</summary>
        public static void FlagPole(OrnCtx c, double x, double z, double height, double windDeg)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            float y = c.TopY(x, z);
            Affine3 f = Affine3.Translation(x, y, z);
            ShapeBrush stone = OrnamentKit.B(OrnamentPalette.PlinthWhite, MaterialChannel.Stone);
            ShapeBrush steel = OrnamentKit.B(OrnamentPalette.Steel, MaterialChannel.Metal);
            double baseR = Math.Max(0.6, 0.05 * height);
            double r0 = Math.Max(0.08, 0.009 * height);
            if (c.Lod >= 2)
            {
                if (c.Lod == 2) Shapes.Cylinder(m, f, stone, baseR + 0.2, 0.8, 6, 0, 0, false, true, L);
                Shapes.Cylinder(m, f, steel, r0, height, 4, 0, 0, false, false, L);
            }
            else
            {
                Shapes.Cylinder(m, f, stone, baseR + 0.35, 0.3, c.Lod == 0 ? 24 : 12, 0.04, 1, false, true, L);
                Shapes.Cylinder(m, f * Affine3.Translation(0, 0.3, 0), stone, baseR, 0.5, c.Lod == 0 ? 24 : 12, 0.04, 1, false, true, L);
                Shapes.Frustum(m, f * Affine3.Translation(0, 0.8, 0), steel, r0, 0.45 * r0, height - 0.8, c.Lod == 0 ? 10 : 6, 0, 0, false, true, L);
                Shapes.Sphere(m, f * Affine3.Translation(0, height + 0.1, 0), OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt), 1.6 * r0, 8, L);
            }
            double fh = Math.Min(9.0, 0.3 * height);
            OrnamentKit.Flag(c, f * Affine3.Translation(0, height - fh - 0.15, 0) * Affine3.Yaw(windDeg - 90), fh);
            c.Ao(v0, i0, y);
            if (c.Stats != null) c.Stats.TopM = (float)Math.Max(c.Stats.TopM, y + height + 0.25 - c.CentreTopY);
        }
    }
}
