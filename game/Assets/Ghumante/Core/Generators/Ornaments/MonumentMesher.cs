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

        // Dimensions in metres, measured on the frontal photos (ref_ornaments.md §1: c_07, c_10, c_16) against the
        // people on the steps (c_16) and the 2.6 m plaque: platform top 0.9 m, a 9 m clear span, the intrados apex
        // 7.4 m above the island, a 1.6 m band, a lantern half as tall as the arch (3.3 m chamber, 6 m slab),
        // pavilions 6.6 m wide with their peaks at 4.9 m, about 22 m across; it stands on the 30.5 m round park
        // island (OSM w193705373) with lawn, beds and the railing round it.
        private const double GateY0 = 0.9, GateSpan = 4.5, GateRise = 6.5, GateDepth = 1.5, GateBand = 1.6, GateFlare = 2.0;
        private const double PavIn = 4.5, PavOut = 11.1, PavDepth = 4.0, PavEave = 2.5, PavRidge = 4.0, PavOverhang = 0.3;
        private const double PlatHalfU = 11.4, PlatHalfW = 3.2;

        /// <summary>The lantern on the crown: post height, opening half widths (bottom, top), post widths (bottom,
        /// top), slab thickness and half width, peaked cap height, mast length.</summary>
        private const double LanPostH = 3.3, LanInB = 0.85, LanInT = 1.15, LanWB = 0.9, LanWT = 1.25, LanSlab = 0.5, LanSlabHalf = 3.0,
                             LanCapH = 1.0, LanMast = 3.0;

        /// <summary>Height of the lantern's base above the island top.</summary>
        private const double LanternY = GateY0 + GateRise + 0.35;

        /// <summary>Plan half sizes of the gate's footprint (platform and pavilions with their horn tips).</summary>
        public const double GateHalfU = PavOut + 0.3, GateHalfW = PlatHalfW;

        /// <summary>Farthest reach of Shahid Gate from its centre (the corners of its footprint).</summary>
        public static readonly double GateReach = Math.Sqrt(GateHalfU * GateHalfU + GateHalfW * GateHalfW) + 0.3;

        /// <summary>
        /// Shahid Gate (Martyrs' Memorial), as the photos show it: one pointed beige ashlar arch whose legs spring out
        /// of two broad pavilions and flare outward at the foot; above the crown the legs splay into a lantern with a
        /// pointed opening holding King Tribhuvan's bust, under a slab with up-turned horns and a peaked concave cap
        /// carrying the national flag; each pavilion a stone block under a concave saddle roof with crested edges
        /// sweeping up to sharp horned tips, a martyr's bust in a tall lancet niche on each face; all on a stepped
        /// platform with a broad flight of steps under the arch. Returns its reach (<see cref="GateReach"/>).
        /// </summary>
        public static double ShahidGate(OrnCtx c)
        {
            MeshData m = c.M;
            Affine3 f = c.CentreFrame();
            int v0 = m.VertexCount, i0 = m.IndexCount;
            ShapeBrush stone = OrnamentKit.B(OrnamentPalette.GateStone, MaterialChannel.Stone);
            ShapeBrush light = OrnamentKit.B(OrnamentPalette.GateStoneLight, MaterialChannel.Stone);
            const double ly = LanternY;
            // Platform: three stepped slabs (one block far away), the flight of steps and the dark plaque.
            if (c.Lod >= 2)
            {
                OrnamentKit.Box(m, f * Affine3.Translation(0, 0.5 * GateY0 - 0.15, 0), light, 2 * PlatHalfU, GateY0 + 0.3, 2 * PlatHalfW);
            }
            else
            {
                for (int k = 0; k < 3; k++)
                {
                    double w = 2 * PlatHalfU - 0.8 * k, d = 2 * PlatHalfW - 0.8 * k;
                    Shapes.RoundedSlab(m, f * Affine3.Translation(0, 0.3 * k - (k == 0 ? 0.3 : 0), 0), light, w, d, 0.3 + (k == 0 ? 0.3 : 0), 0.08, 0.03, 1, c.L);
                    if (c.Stats != null) c.Stats.Steps++;
                }
                for (int k = 0; k < 5; k++)
                {
                    double depth = 0.42 * (5 - k);
                    OrnamentKit.Box(m, f * Affine3.Translation(0, 0.09 + 0.18 * k, PlatHalfW - 0.6 + 0.5 * depth), light, 2 * GateSpan - 1.0, 0.18, depth);
                    if (c.Stats != null) c.Stats.Steps++;
                }
                OrnamentKit.Box(m, f * Affine3.Translation(0, GateY0 + 0.55, -1.9), OrnamentKit.B(OrnamentPalette.Plaque, MaterialChannel.Stone), 2.6, 1.1, 0.16);
                OrnamentKit.Box(m, f * Affine3.Translation(0, GateY0 + 0.08, -1.9), light, 3.0, 0.16, 0.5);
            }
            int rings = c.Lod == 0 ? 24 : c.Lod == 1 ? 12 : c.Lod == 2 ? 7 : 4;
            for (int side = -1; side <= 1; side += 2) ArchLeg(c, f, stone, side, rings);
            Lantern(c, f * Affine3.Translation(0, ly, 0), stone, light);
            for (int side = -1; side <= 1; side += 2) Pavilion(c, f, side, stone, light);
            if (c.Lod < 2) ShapeColor.JitterByPosition(m, v0, m.VertexCount - v0, 0.07f, c.Seed, 0.7);
            c.Ao(v0, i0, c.CentreTopY, c.Lod < 2 ? 0.5f : 0.3f);
            if (c.Stats != null) c.Stats.TopM = (float)Math.Max(c.Stats.TopM, ly + LanPostH + LanSlab + LanCapH + (c.Lod < 3 ? 0.4 + LanMast : 0));
            c.Take(OrnamentFootprint.Box(FootprintKind.Centrepiece, c.CX, c.CZ, GateHalfU, Math.Max(GateHalfW, PavDepth * 0.5 + PavOverhang), c.FacingDeg, 0.3));
            return GateReach;
        }

        /// <summary>Solid boxes of the gate: the walkable platform and the two pavilions (the opening under the arch
        /// stays free).</summary>
        public static void GateColliders(OrnCtx c, GenColliders col)
        {
            double a = c.FacingDeg * Math.PI / 180.0, ux = Math.Cos(a), uz = -Math.Sin(a), y = c.CentreTopY;
            col.AddBox(c.CX, c.CZ, y - 0.3, y + GateY0, PlatHalfU, PlatHalfW, ux, uz, GenColliderFlags.Walkable, GenColliders.Stone);
            for (int side = -1; side <= 1; side += 2)
            {
                double x, z;
                c.Local(side * 0.5 * (PavIn + PavOut), 0, out x, out z);
                col.AddBox(x, z, y + GateY0, y + GateY0 + PavEave + 0.8, 0.5 * (PavOut - PavIn), 0.5 * PavDepth, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
            }
        }

        /// <summary>One leg of the pointed arch: the intrados is a circular arc from the platform at u = ±span to the
        /// apex, the band 1.3 m wide over most of its length and flaring outward in its lowest third, where it
        /// springs out of the pavilion.</summary>
        private static void ArchLeg(OrnCtx c, in Affine3 f, in ShapeBrush b, int side, int rings)
        {
            MeshData m = c.M;
            double cx = (GateRise * GateRise - GateSpan * GateSpan) / (2 * GateSpan), ri = GateSpan + cx, phi1 = Math.Atan2(GateRise, cx);
            Profile2 sec = c.Lod >= 2 ? c.P.Clear(true).SetRect(1, 1) : c.P.Clear(true).SetRoundedRect(1, 1, 0.12, c.Lod == 0 ? 2 : 1);
            double[] px = c.Xs, py = c.Zs, pw = c.Nx;
            for (int k = 0; k < rings; k++)
            {
                // Denser near the foot, where the flare curves.
                double t = Math.Pow((double)k / (rings - 1), 1.35);
                double phi = phi1 * t, ca = Math.Cos(phi), sa = Math.Sin(phi);
                double w = LegWidth(t);
                px[k] = side * (-cx + ri * ca + ca * 0.5 * w);
                py[k] = GateY0 - 0.2 * (1 - t) * (1 - t) + ri * sa + sa * 0.5 * w;
                pw[k] = w;
            }
            OrnamentKit.PlanarSweep(m, f, b, px, py, pw, rings, GateDepth, sec, true);
            if (c.Lod != 0) return;
            // Ashlar courses: thin joint bands across the band every few rings read as the voussoirs.
            ShapeBrush jb = OrnamentKit.B(OrnamentPalette.GateJoint, MaterialChannel.Stone);
            for (int k = 3; k < rings - 1; k += 3)
            {
                double t = Math.Pow((double)k / (rings - 1), 1.35), phi = phi1 * t, ca = Math.Cos(phi), sa = Math.Sin(phi);
                double w = LegWidth(t);
                if (w > 2.2) continue;
                double bx = -cx + ri * ca + ca * 0.5 * w, by = GateY0 + ri * sa + sa * 0.5 * w;
                Affine3 bf = f * Affine3.Translation(side * bx, by, 0) * Affine3.RotationZ(side * phi);
                OrnamentKit.Box(m, bf, jb, w + 0.06, 0.06, GateDepth + 0.06);
            }
        }

        private static double LegWidth(double t)
        {
            double flare = Math.Max(0, 1 - t / 0.38);
            return GateBand + 0.15 * (1 - t) + GateFlare * flare * flare;
        }

        /// <summary>The lantern on the crown (frame origin at its base): the legs continue as two posts splaying
        /// outward round a pointed opening with the bust, a slab with up-turned horns, a peaked concave cap, finial,
        /// mast and flag.</summary>
        private static void Lantern(OrnCtx c, in Affine3 f, in ShapeBrush stone, in ShapeBrush light)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            const double postH = LanPostH, inB = LanInB, inT = LanInT, wB = LanWB, wT = LanWT;
            if (c.Lod >= 3)
            {
                OrnamentKit.Box(m, f * Affine3.Translation(0, 0.5 * postH, 0), stone, 2 * (inT + wT), postH, GateDepth);
                OrnamentKit.Box(m, f * Affine3.Translation(0, postH + 0.5 * LanSlab, 0), light, 2 * LanSlabHalf, LanSlab, 1.9);
                Shapes.Cone(m, f * Affine3.Translation(0, postH + LanSlab, 0), stone, 1.3, LanCapH, 4, 0, 0, false, L);
                return;
            }
            // Sill merging into the crown.
            Shapes.RoundedSlab(m, f * Affine3.Translation(0, -0.5, 0), stone, 2 * (inB + wB) + 0.3, GateDepth + 0.2, 0.7, 0.08, 0.05, 1, L);
            // Posts, splaying outward as they rise (the lantern is wider at the top, a bell chamber).
            Profile2 sec = c.Lod >= 2 ? c.Q.Clear(true).SetRect(1, 1) : c.Q.Clear(true).SetRoundedRect(1, 1, 0.1, 1);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                double[] px = c.Ax, py = c.Ay, pw = c.Aw;
                px[0] = sd * (inB + 0.5 * wB);
                py[0] = -0.3;
                pw[0] = wB;
                px[1] = sd * (inT + 0.5 * wT);
                py[1] = postH;
                pw[1] = wT;
                OrnamentKit.PlanarSweep(m, f, stone, px, py, pw, 2, GateDepth, sec, true);
            }
            // Spandrel over the pointed (lancet) opening, between the posts' inner edges.
            {
                int seg = c.Lod == 0 ? 8 : 4;
                const double ys = 0.2, hr = 2.6;
                double a2 = inB + (inT - inB) * ys / postH;
                double cc = (hr * hr - a2 * a2) / (2 * a2), rr = a2 + cc, ap = Math.Atan2(hr, cc);
                double[] ex = c.Xs, ey = c.Zs;
                int n = 0;
                // Left springing up the left arc to the apex, down the right arc, up the right post, across, down the left.
                for (int k = 0; k <= seg; k++)
                {
                    double phi = ap * k / seg;
                    ex[n] = cc - rr * Math.Cos(phi);
                    ey[n++] = ys + rr * Math.Sin(phi);
                }
                for (int k = seg - 1; k >= 0; k--)
                {
                    double phi = ap * k / seg;
                    ex[n] = -cc + rr * Math.Cos(phi);
                    ey[n++] = ys + rr * Math.Sin(phi);
                }
                ex[n] = inT;
                ey[n++] = postH + 0.02;
                ex[n] = -inT;
                ey[n++] = postH + 0.02;
                // No bevel: the sliver between the arc and the post is too thin for a mitred inset.
                Elevation(m, f, stone, ex, ey, n, GateDepth - 0.08, 0, L);
            }
            // King Tribhuvan's bust on a small pedestal in the opening (dark bronze, it reads as a black bell from the road).
            OrnamentKit.Box(m, f * Affine3.Translation(0, 0.2, 0), light, 0.5, 0.4, 0.5);
            if (c.Lod < 2)
            {
                var spec = new StatueSpec { Pose = StatuePose.Bust, Attire = StatueAttire.RoyalPlumed, Finish = StatueFinish.Bronze, FigureM = 2.3f };
                ShapeLod keep = c.L;
                c.L = new ShapeLod(Math.Min(2, c.Lod + 1));
                StatueMesher.Bust(c, f * Affine3.Translation(0, 0.4, 0) * Affine3.Scaling(2.3 / 1.8), spec);
                c.L = keep;
            }
            else
            {
                Shapes.Sphere(m, f * Affine3.Translation(0, 0.9, 0) * Affine3.Scaling(1, 1.3, 1), OrnamentKit.B(OrnamentPalette.IronBlack, MaterialChannel.Metal), 0.3, 6, L);
            }
            // The flat cap slab, its ends turned up into small horns.
            {
                double[] ex = c.Xs, ey = c.Zs;
                const double h = LanSlabHalf, t = LanSlab;
                int n = 0;
                ex[n] = -h; ey[n++] = postH;
                ex[n] = h; ey[n++] = postH;
                ex[n] = h + 0.22; ey[n++] = postH + 0.12;
                ex[n] = h + 0.38; ey[n++] = postH + t + 0.3;
                ex[n] = h + 0.12; ey[n++] = postH + t + 0.08;
                ex[n] = h - 0.1; ey[n++] = postH + t;
                ex[n] = -h + 0.1; ey[n++] = postH + t;
                ex[n] = -h - 0.12; ey[n++] = postH + t + 0.08;
                ex[n] = -h - 0.38; ey[n++] = postH + t + 0.3;
                ex[n] = -h - 0.22; ey[n++] = postH + 0.12;
                Elevation(m, f, light, ex, ey, n, 2.0, c.Lod == 0 ? 0.05 : 0, L);
            }
            // Low peaked cap with concave sides, spike, crimson finial, mast and flag.
            Profile2 plan = c.P.Clear(true).SetRoundedRect(1, 0.42, 0.03, 1);
            int capRings = c.Lod == 0 ? 7 : 4;
            double[] ys2 = c.Ys, ss2 = c.Ss;
            for (int k = 0; k < capRings; k++)
            {
                double t = (double)k / (capRings - 1);
                ys2[k] = LanCapH * t;
                ss2[k] = 2.7 * (0.08 + 0.92 * Math.Pow(1 - t, 1.5));
            }
            OrnamentKit.StackLoft(m, f * Affine3.Translation(0, postH + LanSlab, 0), stone, plan, ys2, ss2, capRings, false, false);
            double top = postH + LanSlab + LanCapH;
            Shapes.Cone(m, f * Affine3.Translation(0, top - 0.05, 0), light, 0.12, 0.6, 6, 0, 0, false, L);
            Shapes.Sphere(m, f * Affine3.Translation(0, top + 0.25, 0), OrnamentKit.B(OrnamentPalette.FlagCrimson, MaterialChannel.Paint), 0.14, 8, L);
            ShapeBrush steel = OrnamentKit.B(OrnamentPalette.Steel, MaterialChannel.Metal);
            Shapes.Cylinder(m, f * Affine3.Translation(0, top + 0.4, 0), steel, 0.035, LanMast, 6, 0, 0, false, true, L);
            if (c.Lod < 3) OrnamentKit.Flag(c, f * Affine3.Translation(0.04, top + 0.4 + LanMast - 1.65, 0), 1.6);
        }

        /// <summary>A polygon in the frame's elevation plane (u right, y up) extruded through <paramref name="depth"/>
        /// centred on the frame's z = 0, with rounded (bevelled) edges.</summary>
        private static void Elevation(MeshData m, in Affine3 f, in ShapeBrush b, double[] u, double[] y, int n, double depth, double bevel, ShapeLod lod)
        {
            Affine3 xf = f * Affine3.Translation(0, 0, 0.5 * depth) * Affine3.RotationX(-0.5 * Math.PI);
            Shapes.BevelExtrude(m, xf, b, u, y, n, depth, bevel, bevel > 0 ? 1 : 0, BevelStyle.Chamfer, true, true, bevel, 35, lod);
        }

        /// <summary>One side pavilion: a stone block under a concave saddle roof (ridge front to back) whose crested
        /// edges sweep down from the ridge and up again into sharp horned tips at the four eave corners, a lancet niche
        /// with a martyr's bust on the front and back faces. The arch leg springs out of its inner half.</summary>
        private static void Pavilion(OrnCtx c, in Affine3 f, int side, in ShapeBrush stone, in ShapeBrush light)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            double hw = 0.5 * (PavOut - PavIn), uc = side * 0.5 * (PavIn + PavOut);
            Affine3 pf = f * Affine3.Translation(uc, GateY0, 0);
            double[] ex = c.Xs, ey = c.Zs;
            int gable = c.Lod == 0 ? 10 : c.Lod == 1 ? 6 : c.Lod == 2 ? 4 : 1;
            // Body with its gable ends.
            int n = 0;
            ex[n] = -hw; ey[n++] = 0;
            ex[n] = hw; ey[n++] = 0;
            for (int k = 0; k <= 2 * gable; k++)
            {
                double u = hw - hw * k / gable;
                ex[n] = u;
                ey[n++] = PavEave + (PavRidge - PavEave) * Math.Pow(1 - Math.Abs(u) / hw, 1.25);
            }
            Elevation(m, pf, stone, ex, ey, n, PavDepth, c.Lod == 0 ? 0.06 : 0, L);
            if (c.Lod >= 3) return;
            // Roof plate: top line from horn tip to horn tip, underside back.
            // A gently concave gable line from the peak down to the eave, then a short horn turned up and out (photos c_07,
            // c_10: small horns, not a Chinese sweep).
            double uMin = hw - 0.18, uTip = hw + 0.38, yMin = PavEave + 0.1, yRidge = PavRidge + 0.3, yTip = PavEave + 0.5;
            int half = c.Lod == 0 ? 9 : c.Lod == 1 ? 6 : 3;
            n = 0;
            for (int k = -half - 2; k <= half + 2; k++)
            {
                double u = RoofU(k, half, uMin, uTip);
                ex[n] = u;
                ey[n++] = RoofTop(u, uMin, uTip, yMin, yRidge, yTip);
            }
            for (int k = half + 2; k >= -half - 2; k--)
            {
                double u = RoofU(k, half, uMin, uTip);
                if (Math.Abs(k) == half + 2) continue; // the tips are sharp: no underside point there
                double th = Math.Abs(u) <= uMin ? 0.3 : 0.3 - 0.22 * (Math.Abs(u) - uMin) / (uTip - uMin);
                ex[n] = u * 0.985;
                ey[n++] = RoofTop(u, uMin, uTip, yMin, yRidge, yTip) - th;
            }
            Elevation(m, pf, light, ex, ey, n, PavDepth + 2 * PavOverhang, c.Lod == 0 ? 0.04 : 0, L);
            if (c.Lod == 0)
            {
                // Crest: teeth along both gable edges and along the ridge.
                ShapeBrush crest = light;
                double zf = 0.5 * PavDepth + PavOverhang - 0.12;
                for (int face = -1; face <= 1; face += 2)
                {
                    for (double u = -uMin + 0.3; u <= uMin - 0.29; u += 0.5)
                    {
                        double y = RoofTop(u, uMin, uTip, yMin, yRidge, yTip), dy = RoofTop(u + 0.05, uMin, uTip, yMin, yRidge, yTip) - RoofTop(u - 0.05, uMin, uTip, yMin, yRidge, yTip);
                        double ang = Math.Atan2(dy, 0.1);
                        OrnamentKit.Box(m, pf * Affine3.Translation(u, y + 0.06, face * zf) * Affine3.RotationZ(ang), crest, 0.15, 0.2, 0.18);
                    }
                }
                for (double z = -zf + 0.4; z <= zf - 0.39; z += 0.5)
                    OrnamentKit.Box(m, pf * Affine3.Translation(0, yRidge + 0.06, z), crest, 0.18, 0.2, 0.15);
            }
            // Lancet niches with the martyrs' busts on the front and back faces.
            ShapeLod saved = c.L;
            c.L = new ShapeLod(Math.Min(2, c.Lod + 1));
            for (int face = -1; face <= 1; face += 2)
            {
                Affine3 nf = pf * Affine3.Yaw(face > 0 ? 0 : 180) * Affine3.Translation(0, 0.3, 0.5 * PavDepth);
                Niche(c, nf, light, 1.9, 2.7);
                if (c.Lod == 0)
                {
                    OrnamentKit.Box(m, nf * Affine3.Translation(0, 0.3, 0.2), light, 0.6, 0.6, 0.5);
                    var spec = new StatueSpec
                    {
                        Pose = StatuePose.Bust, Attire = (side + face) % 4 == 0 ? StatueAttire.DauraTopi : StatueAttire.Coat, Finish = StatueFinish.Bronze,
                        FigureM = 2.2f, Glasses = side < 0 && face > 0,
                    };
                    StatueMesher.Bust(c, nf * Affine3.Translation(0, 0.6, 0.22) * Affine3.Scaling(2.2 / 1.8), spec);
                }
            }
            c.L = saved;
        }

        /// <summary>Station k (−half−2 … half+2) of the roof top line: half stations each side between the ridge and
        /// the low point, two more out to the horn tip.</summary>
        private static double RoofU(int k, int half, double uMin, double uTip)
        {
            int a = Math.Abs(k);
            double u = a <= half ? uMin * a / half : uMin + (uTip - uMin) * (a - half) / 2.0;
            return k < 0 ? -u : u;
        }

        /// <summary>Height of the roof's top line at u: a concave sweep down from the ridge to a low point near the
        /// eave, then up into the horn tip.</summary>
        private static double RoofTop(double u, double uMin, double uTip, double yMin, double yRidge, double yTip)
        {
            double a = Math.Abs(u);
            if (a <= uMin) return yMin + (yRidge - yMin) * Math.Pow(1 - a / uMin, 1.25);
            double t = Math.Min(1, (a - uMin) / (uTip - uMin));
            return yMin + (yTip - yMin) * t * t;
        }

        /// <summary>A tall lancet niche on the wall plane (frame XY at z = 0, front +Z): dark recess panel and a stone
        /// frame. The arch springs low and rises to a sharp point (its arcs are centred well outside the opening).</summary>
        private static void Niche(OrnCtx c, in Affine3 f, in ShapeBrush frame, double w, double h)
        {
            MeshData m = c.M;
            double[] px = c.Xs, py = c.Zs;
            int seg = c.Lod == 0 ? 7 : 3;
            double hw = 0.5 * w, spring = 0.36 * h, rise = h - spring, cc = (rise * rise - hw * hw) / (2 * hw), rr = hw + cc;
            double ap = Math.Atan2(rise, cc);
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

        // The platform outline measured on OSM w120106732 (roundabouts.md §3.1): a 21 m stepped square, each side at
        // half width 10.5 m for ±5.4 m, then two right-angle steps in to the corner (8.8, 8.8). Counter-clockwise in
        // the frame's (u, w) plane, 20 points.
        private static readonly double[] s_mandU, s_mandW;

        static MonumentMesher()
        {
            double[] qu = { 10.5, 10.5, 8.8, 8.8, 5.4 }, qw = { -5.4, 5.4, 5.4, 8.8, 8.8 };
            s_mandU = new double[20];
            s_mandW = new double[20];
            for (int q = 0; q < 4; q++)
                for (int i = 0; i < 5; i++)
                {
                    // Rotate by q quarter turns counter-clockwise: (u, w) -> (-w, u).
                    double u = qu[i], w = qw[i];
                    for (int r = 0; r < q; r++)
                    {
                        double t = u;
                        u = -w;
                        w = t;
                    }
                    s_mandU[5 * q + i] = u;
                    s_mandW[5 * q + i] = w;
                }
        }

        /// <summary>Half width of the mandala platform.</summary>
        public const double MandalaHalf = 10.5;

        /// <summary>Distance of the colonnade in front of the mandala centre.</summary>
        private const double ColonnadeW = 12.9;

        /// <summary>Farthest reach of the full-size mandala from its centre (the colonnade's end columns).</summary>
        public static readonly double MandalaReach = Math.Sqrt(9.4 * 9.4 + (ColonnadeW + 0.35) * (ColonnadeW + 0.35));

        /// <summary>The stepped outline offset inward by <paramref name="inset"/> (negative: outward), mitred at every
        /// right-angle step. Returns the point count (20).</summary>
        private static int MandalaOutline(double[] u, double[] w, double inset)
        {
            int n = s_mandU.Length;
            for (int i = 0; i < n; i++)
            {
                int ip = (i + n - 1) % n, inx = (i + 1) % n;
                double n1u = s_mandW[i] - s_mandW[ip], n1w = -(s_mandU[i] - s_mandU[ip]);
                double n2u = s_mandW[inx] - s_mandW[i], n2w = -(s_mandU[inx] - s_mandU[i]);
                double l1 = Math.Sqrt(n1u * n1u + n1w * n1w), l2 = Math.Sqrt(n2u * n2u + n2w * n2w);
                n1u /= l1;
                n1w /= l1;
                n2u /= l2;
                n2w /= l2;
                double den = Math.Max(0.125, 1 + n1u * n2u + n1w * n2w);
                u[i] = s_mandU[i] - inset * (n1u + n2u) / den;
                w[i] = s_mandW[i] - inset * (n1w + n2w) / den;
            }
            return n;
        }

        /// <summary>The Maitighar Mandala at <paramref name="scale"/> (1 = the mapped 21 m): a stepped square platform
        /// with the coloured mandala inlay (teal border with a pale edge line, coloured corner fields with Ashtamangala
        /// discs, concentric bands, 16 lotus petals, 32 gilt vajras, 32 garland beads, a small central stupa), a
        /// marigold collar following the outline and a colonnade of white columns with green capitals on the road side
        /// it faces. Placed at <see cref="OrnCtx.CX"/>, <see cref="OrnCtx.CZ"/>; registers its footprints and returns
        /// its reach.</summary>
        public static double Mandala(OrnCtx c, double scale)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            Affine3 f = c.CentreFrame() * Affine3.Scaling(scale, 1, scale);
            int v0 = m.VertexCount, i0 = m.IndexCount;
            const double pt = 0.32, ring = 8.7;
            double[] ou = c.Xs, ow = c.Zs;
            int on = MandalaOutline(ou, ow, 0);
            Shapes.BevelExtrude(m, f, OrnamentKit.B(OrnamentPalette.MandalaTeal, MaterialChannel.Paint), ou, ow, on, pt, c.Lod == 0 ? 0.06 : 0, 1,
                                BevelStyle.Chamfer, true, false, 0, 30, L);
            Affine3 top = f * Affine3.Translation(0, pt, 0);
            if (c.Lod >= 2)
            {
                int fr = c.Lod == 2 ? 16 : 10;
                for (int k = 0; k < (c.Lod == 2 ? 3 : 1); k++)
                {
                    double ro = k == 0 ? ring : k == 1 ? 7.2 : 3.3, rin = k == 0 ? 7.7 : k == 1 ? 5.5 : 1.5;
                    uint bandCol = k == 0 ? OrnamentPalette.MandalaYellow : k == 1 ? OrnamentPalette.MandalaBlue : OrnamentPalette.MandalaWhite;
                    Shapes.Lathe(m, top, OrnamentKit.B(bandCol, MaterialChannel.Paint), c.Q.Clear(false).Add(ro, 0, true).Add(ro, 0.03 + 0.01 * k, true).Add(rin, 0.03 + 0.01 * k, true), fr, L);
                }
                if (c.Lod == 2)
                {
                    Shapes.Dome(m, top, OrnamentKit.B(OrnamentPalette.MandalaWhite, MaterialChannel.Plaster), 1.25, 1.2, 8, false, L);
                    Shapes.Cone(m, top * Affine3.Translation(0, 1.15, 0), OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt), 0.22, 1.3, 5, 0, 0, false, L);
                    Profile2 col2 = c.Q.Clear(false).Add(12.3, 0, true).Add(12.1, 0.4, true).Add(10.9, 0.4, true).Add(10.7, 0, true);
                    Shapes.Lathe(m, f * Affine3.Translation(0, -0.05, 0), OrnamentKit.B(OrnamentPalette.MarigoldYellow, MaterialChannel.Foliage), col2, 16, L);
                }
                c.Ao(v0, i0, c.CentreTopY, 0.3f);
                Colonnade(c, scale, 10, 2.0);
                MandalaFootprints(c, scale);
                return MandalaReach * scale;
            }
            // Pale edge line just inside the border, following the steps.
            {
                int en = MandalaOutline(ou, ow, 0.75);
                Path3 ep = c.Path.Clear();
                for (int i = 0; i < en; i++) ep.Add(ou[i], pt + 0.004, ow[i]);
                Shapes.Sweep(m, f, OrnamentKit.B(OrnamentPalette.MandalaEdge, MaterialChannel.Paint), ep, c.Q.Clear(true).SetRect(0.28, 0.02), true, true, SweepFrames.Upright);
            }
            // Corner fields (inside the border, outside the circle), one colour per quadrant.
            int arc = c.Lod == 0 ? 10 : 5;
            for (int q = 0; q < 4; q++)
            {
                double[] px = c.Ax, pz = c.Ay;
                int n = 0;
                const double bi = 1.05;
                px[n] = 10.5 - bi; pz[n++] = 0;
                px[n] = 10.5 - bi; pz[n++] = 5.4 - bi;
                px[n] = 8.8 - bi; pz[n++] = 5.4 - bi;
                px[n] = 8.8 - bi; pz[n++] = 8.8 - bi;
                px[n] = 5.4 - bi; pz[n++] = 8.8 - bi;
                px[n] = 5.4 - bi; pz[n++] = 10.5 - bi;
                px[n] = 0; pz[n++] = 10.5 - bi;
                for (int k = 0; k <= arc; k++)
                {
                    double a = 0.5 * Math.PI * k / arc;
                    px[n] = ring * Math.Sin(a);
                    pz[n++] = ring * Math.Cos(a);
                }
                Shapes.BevelExtrude(m, top * Affine3.Yaw(90 * q), OrnamentKit.B(s_cornerCols[q], MaterialChannel.Paint), px, pz, n, 0.018, 0, 0,
                                    BevelStyle.Chamfer, true, false, 0, 30, L);
                if (c.Lod == 0)
                {
                    // Ashtamangala emblem disc in the corner field.
                    Affine3 ef = top * Affine3.Yaw(90 * q + 45) * Affine3.Translation(0, 0, 9.6);
                    Shapes.Cylinder(m, ef, OrnamentKit.B(OrnamentPalette.MandalaWhite, MaterialChannel.Paint), 0.75, 0.05, 16, 0, 0, false, true, L);
                    Shapes.Cylinder(m, ef, OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt), 0.45, 0.11, 12, 0.03, 1, false, true, L);
                }
            }
            // Concentric bands (outer to inner), each a thin raised annulus.
            int rad = c.Lod == 0 ? 40 : 18;
            for (int k = 0; k < s_bandR.Length - 1; k++)
            {
                double ro = s_bandR[k] * BandK, rin = s_bandR[k + 1] * BandK, h = 0.022 + 0.006 * k;
                Profile2 p = c.Q.Clear(false).Add(ro, 0, true).Add(ro, h, true).Add(rin, h, true);
                Shapes.Lathe(m, top, OrnamentKit.B(s_bandCol[k], MaterialChannel.Paint), p, Math.Max(16, (int)(rad * ro / 10.6)), L);
            }
            double hTop = 0.022 + 0.006 * (s_bandR.Length - 1);
            if (c.Lod == 1)
            {
                // Lotus petals as flat inlays.
                for (int k = 0; k < 16; k++)
                {
                    double[] px = c.Ax, pz = c.Ay;
                    px[0] = 0; pz[0] = 0.02;
                    px[1] = 0.48; pz[1] = 0.75;
                    px[2] = 0; pz[2] = 1.5;
                    px[3] = -0.48; pz[3] = 0.75;
                    OrnamentKit.FlatPoly(m, top * Affine3.Translation(0, hTop + 0.005, 0) * Affine3.Yaw(22.5 * k) * Affine3.Translation(0, 0, 3.85) * Affine3.RotationX(Math.PI / 2),
                                         (k & 1) == 0 ? OrnamentPalette.RosePink : OrnamentPalette.MandalaWhite, MaterialChannel.Paint, px, pz, 4, 0.004, false);
                }
            }
            if (c.Lod == 0)
            {
                // 32 gilt vajras on the outer blue band.
                for (int k = 0; k < 32; k++)
                {
                    Affine3 vf = top * Affine3.Yaw(360.0 * k / 32) * Affine3.Translation(0, 0.1, 7.95 * BandK);
                    Shapes.Sphere(m, vf * Affine3.Scaling(0.28, 0.14, 0.85), OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt), 0.5, 5, L);
                }
                // 16 lotus petals, alternating pink and white.
                for (int k = 0; k < 16; k++)
                {
                    double[] px = c.Ax, pz = c.Ay;
                    int n = 0;
                    const int ps = 6;
                    for (int q = 0; q <= ps; q++)
                    {
                        double t = (double)q / ps;
                        px[n] = 0.48 * Math.Sin(Math.PI * t);
                        pz[n++] = 3.85 + 1.5 * t;
                    }
                    for (int q = ps - 1; q > 0; q--)
                    {
                        double t = (double)q / ps;
                        px[n] = -0.48 * Math.Sin(Math.PI * t);
                        pz[n++] = 3.85 + 1.5 * t;
                    }
                    Shapes.BevelExtrude(m, top * Affine3.Translation(0, hTop - 0.02, 0) * Affine3.Yaw(22.5 * k),
                                        OrnamentKit.B((k & 1) == 0 ? OrnamentPalette.RosePink : OrnamentPalette.MandalaWhite, MaterialChannel.Paint), px, pz, n,
                                        0.06, 0, 0, BevelStyle.Chamfer, true, false, 0, 40, L);
                }
                // 32 garland beads, alternating black and blue (every other one).
                for (int k = 0; k < 32; k += 2)
                {
                    double a = 11.25 * k * Math.PI / 180;
                    Shapes.Sphere(m, top * Affine3.Translation(2.85 * Math.Sin(a), hTop + 0.06, 2.85 * Math.Cos(a)),
                                  OrnamentKit.B((k & 2) == 0 ? OrnamentPalette.MandalaBlack : OrnamentPalette.MandalaBlue, MaterialChannel.Paint), 0.15, 5, L);
                }
            }
            // Central stupa: two round steps, a white dome, harmika and gilt spire.
            ShapeBrush white = OrnamentKit.B(OrnamentPalette.MandalaWhite, MaterialChannel.Plaster);
            int sr = c.Lod == 0 ? 24 : 12;
            Shapes.Cylinder(m, top, white, 1.5, 0.25, sr, 0.04, 1, false, true, L);
            Shapes.Cylinder(m, top * Affine3.Translation(0, 0.25, 0), white, 1.28, 0.2, sr, 0.04, 1, false, true, L);
            Shapes.Dome(m, top * Affine3.Translation(0, 0.45, 0), white, 1.1, 0.9, sr, false, L);
            ShapeBrush gilt = OrnamentKit.B(OrnamentPalette.Gilt, MaterialChannel.Gilt);
            Shapes.RoundedBox(m, top * Affine3.Translation(0, 1.48, 0), gilt, 0.5, 0.32, 0.5, 0.04, 1, L);
            Shapes.Frustum(m, top * Affine3.Translation(0, 1.64, 0), gilt, 0.24, 0.06, 0.95, 12, 0, 0, false, true, L);
            if (c.Lod < 2)
            {
                for (int k = 0; k < 5; k++) Shapes.Torus(m, top * Affine3.Translation(0, 1.7 + 0.16 * k, 0), gilt, 0.22 - 0.032 * k, 0.03, 12, 4, L);
                Shapes.Cylinder(m, top * Affine3.Translation(0, 2.6, 0), gilt, 0.28, 0.05, 12, 0.02, 1, true, true, L);
                Shapes.Sphere(m, top * Affine3.Translation(0, 2.76, 0), gilt, 0.1, 8, L);
            }
            c.Ao(v0, i0, c.CentreTopY, 0.3f);
            // Marigold collar following the stepped outline 1 m out, its corners rounded.
            {
                int cn = MandalaOutline(ou, ow, -1.0);
                Path3 raw = c.Path2.Clear();
                for (int i = 0; i < cn; i++)
                {
                    double lx, lz;
                    c.Local(ou[i] * scale, ow[i] * scale, out lx, out lz);
                    raw.Add(lx, 0, lz);
                }
                Path3 col = Curves.Fillet(raw, c.Path.Clear(), 0.9 * scale, c.Lod == 0 ? 3 : 1, true);
                for (int i = 0; i < col.Count; i++) col.Y[i] = c.TopY(col.X[i], col.Z[i]);
                int bv = m.VertexCount, bi0 = m.IndexCount;
                OrnamentKit.Mound(c, col, true, 1.6 * scale, 0.42, OrnamentPalette.MarigoldYellow, OrnamentPalette.MarigoldOrange, MaterialChannel.Foliage, 0.07,
                                  c.Seed ^ 0xA11u, 0.45);
                if (c.Lod == 0)
                {
                    for (int k = 0; k < col.Count; k += 3)
                        OrnamentKit.Bloom(c, col.X[k] + 0.3 * (OrnamentSeed.Unit(c.Seed, k) - 0.5), col.Y[k] + 0.44, col.Z[k] + 0.3 * (OrnamentSeed.Unit(c.Seed, k + 999) - 0.5), 0.11,
                                          (k % 5) == 0 ? OrnamentPalette.MarigoldOrange : OrnamentPalette.MarigoldYellow);
                }
                c.Ao(bv, bi0, c.CentreTopY - 0.1, 0.4f);
            }
            Colonnade(c, scale, 10, 2.0);
            MandalaFootprints(c, scale);
            return MandalaReach * scale;
        }

        /// <summary>Scale of the inlay circle inside the 21 m platform (bands, petals, vajras) against the 10.6 m
        /// reference radius of <see cref="s_bandR"/>.</summary>
        private const double BandK = 8.7 / 10.6;

        private static void MandalaFootprints(OrnCtx c, double scale)
        {
            c.Take(OrnamentFootprint.Box(FootprintKind.Centrepiece, c.CX, c.CZ, (MandalaHalf + 1.0) * scale, (MandalaHalf + 1.0) * scale, c.FacingDeg, 0.8 * scale));
            double x, z;
            c.Local(0, ColonnadeW * scale, out x, out z);
            c.Take(OrnamentFootprint.Box(FootprintKind.Centrepiece, x, z, 9.4 * scale, 0.5, c.FacingDeg, 0.2));
        }

        /// <summary>Maitighar's round raised feature bed on the free side of the island, opposite the mandala, when
        /// the island has room for it.</summary>
        public static void MandalaFeatureBed(OrnCtx c, double ri)
        {
            if (c.Lod >= 2) return;
            RoundaboutSite s = c.Site;
            double ox = c.CX - s.X, oz = c.CZ - s.Z, o = Math.Sqrt(ox * ox + oz * oz);
            double bearing = o > 0.5 ? Math.Atan2(-ox, -oz) * 180 / Math.PI : c.FacingDeg + 180;
            const double rb = 1.6;
            double x, z, deg;
            if (!c.FindSpot(bearing, ri - rb - 1.2, rb + 0.4, 1.0, ri, 10, 50, out x, out z, out deg)) return;
            c.Take(OrnamentFootprint.Disc(FootprintKind.Bed, x, z, rb + 0.4));
            IslandMesher.RaisedBed(c, x, z, rb);
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

        /// <summary>A row of white columns with green capitals and bases carrying a light beam frame (Maitighar),
        /// <see cref="ColonnadeW"/> in front of the mandala centre on the side it faces.</summary>
        private static void Colonnade(OrnCtx c, double scale, int columns, double spacing)
        {
            if (c.Lod >= 3) return;
            MeshData m = c.M;
            ShapeLod L = c.L;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            ShapeBrush white = OrnamentKit.B(OrnamentPalette.ColumnWhite, MaterialChannel.Plaster);
            ShapeBrush green = OrnamentKit.B(OrnamentPalette.ColumnGreen, MaterialChannel.Paint);
            double sp = spacing * scale, span = (columns - 1) * sp, dist = ColonnadeW * scale;
            double maxY = 0;
            for (int k = 0; k < columns; k++)
            {
                double u = -0.5 * span + sp * k, x, z;
                c.Local(u, dist, out x, out z);
                float y = c.TopY(x, z);
                Affine3 cf = Affine3.Translation(x, y, z) * Affine3.Yaw(c.FacingDeg);
                if (c.Lod >= 2)
                {
                    Shapes.Cylinder(m, cf, white, 0.18, 3.3, 4, 0, 0, false, false, L);
                    maxY = Math.Max(maxY, y);
                    continue;
                }
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

        /// <summary>Outer radius of Jawalakhel's basin (Ø ≈ 15 m, photos c_jawa).</summary>
        public const double SpireBasinRadius = 7.6;

        /// <summary>Jawalakhel's round sunken fountain basin (cream rim, terrace, step, water, white tubular railing,
        /// globe lamps and small jets) with the stone spire and King Birendra's statue rising from it. Returns the
        /// footprint radius.</summary>
        public static double SpireBasin(OrnCtx c, in StatueSpec statue, double radius)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            Affine3 f = c.CentreFrame();
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double ro = Math.Max(3.0, radius);
            ShapeBrush rim = OrnamentKit.B(OrnamentPalette.BasinRim, MaterialChannel.Stone);
            int rad = c.Lod == 0 ? 40 : c.Lod == 1 ? 20 : c.Lod == 2 ? 12 : 8;
            Profile2 p = c.Q.Clear(false);
            if (c.Lod >= 2) p.Add(ro, -0.1, true).Add(ro, 0.48, true).Add(ro - 0.5, 0.48, true);
            else p.Add(ro, -0.1, true).Add(ro, 0.42, true).Add(ro - 0.06, 0.48).Add(ro - 0.5, 0.48, true).Add(ro - 0.5, 0.05, true)
                  .Add(ro - 1.3, 0.05, true).Add(ro - 1.3, -0.15, true).Add(ro - 1.5, -0.15, true);
            Shapes.Lathe(m, f, rim, p, rad, L);
            // Water.
            Profile2 w = c.Q.Clear(false).Add(c.Lod >= 2 ? ro - 0.5 : ro - 1.45, -0.12, true).Add(Math.Min(2.4, 0.4 * ro), -0.12, true);
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
                    double a = 2 * Math.PI * (k + 0.5) / jets, jr = 0.58 * ro;
                    Path3 jp = c.Path2.Clear();
                    for (int q = 0; q <= 5; q++)
                    {
                        double t = q / 5.0;
                        jp.Add((jr - 0.17 * ro * t) * Math.Sin(a), -0.12 + 1.6 * t * (1 - t) * 1.6, (jr - 0.17 * ro * t) * Math.Cos(a));
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

        // ------------------------------------------------------------------ fountain, flag pole, chautari tree

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

        /// <summary>Plan radius of a flag pole's round plinth.</summary>
        public static double FlagPoleBase(double height)
        {
            return Math.Max(0.6, 0.05 * height) + 0.35;
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

        /// <summary>Height of the chautari's brick platform above the island top.</summary>
        public const double ChautariHeightM = 0.75;

        /// <summary>
        /// A big old shade tree on a chautari (Lagankhel Chowk, photos cf_lagankhel_chowk 00-01): a round raised
        /// platform of red brick with a stone coping and a packed-earth top, a massive trunk with buttress roots and
        /// three main limbs, and a broad dense canopy of overlapping lobes whose underside stays above 5 m, clear of
        /// the 4.5 m overhead limit where it reaches over the ring road. Sized to the planting radius
        /// <paramref name="ri"/>; returns the platform's footprint radius.
        /// </summary>
        public static double Chautari(OrnCtx c, double ri)
        {
            MeshData m = c.M;
            ShapeLod L = c.L;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double rp = Math.Max(1.8, Math.Min(4.2, 0.55 * ri));
            float y = c.CentreTopY;
            Affine3 f = Affine3.Translation(c.CX, y, c.CZ) * Affine3.Yaw(c.FacingDeg);
            ShapeBrush brick = OrnamentKit.B(OrnamentPalette.BrickPlanter, MaterialChannel.Brick);
            // Red-painted brick with a white coping (photos cf_lagankhel_chowk 00, 01).
            ShapeBrush coping = OrnamentKit.B(OrnamentPalette.PlinthWhite, MaterialChannel.Plaster);
            ShapeBrush bark = OrnamentKit.B(OrnamentPalette.Bark, MaterialChannel.Bark);
            int rad = c.Lod == 0 ? 28 : c.Lod == 1 ? 16 : 8;
            const double ph = ChautariHeightM;
            Shapes.Cylinder(m, f * Affine3.Translation(0, -0.1, 0), brick, rp, ph + 0.02, rad, 0, 0, false, false, L);
            Shapes.Cylinder(m, f * Affine3.Translation(0, ph - 0.08, 0), coping, rp + 0.08, 0.12, rad, c.Lod == 0 ? 0.03 : 0, 1, false, true, L);
            if (c.Lod < 2)
            {
                Shapes.Cylinder(m, f * Affine3.Translation(0, ph + 0.04, 0), OrnamentKit.B(OrnamentPalette.Soil, MaterialChannel.Dirt), rp - 0.2, 0.02, rad, 0, 0, false, true, L);
                // A stone step up to the platform on the side it faces.
                OrnamentKit.Box(m, f * Affine3.Translation(0, 0.18, rp + 0.22), coping, 1.4, 0.36, 0.5);
            }
            // Trunk with buttress roots and three main limbs.
            double tr = Math.Min(1.0, 0.25 * rp + 0.3), th = 3.8;
            Affine3 tf = f * Affine3.Translation(0, ph, 0);
            Shapes.Frustum(m, tf, bark, 1.25 * tr, 0.8 * tr, th, c.Lod == 0 ? 12 : c.Lod == 1 ? 8 : 5, 0, 0, false, c.Lod >= 2, L);
            // A big old broadleaf (a pipal-like crown about 14 m across, its top near 12 m: three to four times the shrine).
            double R = Math.Max(5.0, Math.Min(8.0, 3.0 * rp));
            double yc = Math.Max(ph + th + 0.25 * R, 5.0 + 0.44 * R);
            if (c.Lod < 2)
            {
                if (c.Lod == 0)
                {
                    for (int k = 0; k < 5; k++)
                    {
                        double a = 2 * Math.PI * (k + 0.3 * OrnamentSeed.Unit(c.Seed, 70 + k)) / 5, sa = Math.Sin(a), ca = Math.Cos(a);
                        Path3 rp3 = c.Path2.Clear();
                        rp3.Add(0.55 * tr * sa, 0.9, 0.55 * tr * ca).Add(1.2 * tr * sa, 0.35, 1.2 * tr * ca).Add(1.9 * tr * sa, 0.02, 1.9 * tr * ca);
                        Shapes.Tube(m, tf, bark, rp3, 0.32 * tr, 6, true, false, L, 0.12 * tr);
                    }
                }
                for (int k = 0; k < 3; k++)
                {
                    double a = 2 * Math.PI * k / 3 + 0.4, sa = Math.Sin(a), ca = Math.Cos(a);
                    Path3 lp = c.Path2.Clear();
                    lp.Add(0, 0.75 * th, 0).Add(0.25 * R * sa, th + 0.2 * (yc - th), 0.25 * R * ca).Add(0.42 * R * sa, yc - ph - 0.3, 0.42 * R * ca);
                    Shapes.Tube(m, tf, bark, lp, 0.42 * tr, c.Lod == 0 ? 7 : 5, true, false, L, 0.18 * tr);
                }
            }
            ShapeColor.VerticalShade(m, v0, m.VertexCount - v0, y, y + th, 0.8f, 1.05f);
            // Canopy: a broad central dome and lobes round it.
            int cv = m.VertexCount;
            int lobes = c.Lod == 0 ? 7 : c.Lod == 1 ? 4 : c.Lod == 2 ? 2 : 0;
            int seg = c.Lod == 0 ? 20 : c.Lod == 1 ? 10 : 5;
            OrnamentKit.Clump(c, c.CX, y + yc + 0.08 * R, c.CZ, 0.66 * R, 0.46 * R, 0.66 * R, OrnamentPalette.TreeCanopy, MaterialChannel.Foliage, seg, 0.16, c.Seed + 11);
            for (int k = 0; k < lobes; k++)
            {
                double a = 2 * Math.PI * (k + 0.5 * OrnamentSeed.Unit(c.Seed, 80 + k)) / lobes, rr = 0.52 * R;
                double lx = c.CX + rr * Math.Sin(a), lz = c.CZ + rr * Math.Cos(a);
                double ly = y + yc - 0.08 * R + 0.12 * R * (OrnamentSeed.Unit(c.Seed, 90 + k) - 0.5);
                double sz = 0.4 * R * (0.85 + 0.3 * OrnamentSeed.Unit(c.Seed, 100 + k));
                OrnamentKit.Clump(c, lx, ly, lz, sz, 0.78 * sz, sz, (k & 1) == 0 ? OrnamentPalette.LeafGreen : OrnamentPalette.TreeCanopy, MaterialChannel.Foliage, seg - 2,
                                  0.18, c.Seed + 20 + (uint)k);
            }
            ShapeColor.VerticalShade(m, cv, m.VertexCount - cv, y + yc - 0.5 * R, y + yc + 0.5 * R, 0.72f, 1.12f);
            c.Ao(v0, i0, y, 0.35f);
            if (c.Stats != null)
            {
                c.Stats.Trees++;
                c.Stats.TopM = (float)Math.Max(c.Stats.TopM, yc + 0.54 * R);
            }
            return rp + 0.1;
        }
    }
}
