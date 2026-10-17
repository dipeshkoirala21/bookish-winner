using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Props
{
    public static partial class StreetPropMesher
    {
        // Palette (ref_props.md §7-8; street_life.md §5).
        private const uint PoleConcrete = 0xB3AFA6, PoleFoot = 0x7D7A73, Porcelain = 0x7A4E33, CableBlack = 0x1E1F22, IspBox = 0xC4C7C2;
        private const uint TransformerGrey = 0x7E8A8C, Danger = 0xC8282E, LatticeGrey = 0x9EA3A8, MastRed = 0xC9433A, MastWhite = 0xEDEBE6;
        private const uint TankBase = 0xF2F2F2, KilnBrick = 0x9C4A33, Soot = 0x2E2A28, WellBrick = 0xA4553A, WellStone = 0x9A948A, WaterDark = 0x2B3B4A;

        private static readonly uint[] SignalLenses = { 0xE23B2E, 0xF2B01E, 0x27B05A };
        private static readonly double[] PylonLevels = { 0, 7.5, 15, 22 };

        // ---------------------------------------------------------------------------------------------------------
        // NEA utility pole: square-tapered PSC concrete, 11 kV cross-arm with pin insulators, an LT rack, and the
        // tangle: coils of slack telecom cable, a bundle clamped down the pole, a hanging loop, ISP boxes (§7).

        private static void UtilityPole(MeshData m, int lod)
        {
            const double H = 9.0;
            Affine3 I = Affine3.Identity;
            ShapeBrush conc = B(PoleConcrete, MaterialChannel.Concrete), foot = B(PoleFoot, MaterialChannel.Concrete);
            PolePrism(m, I, H, lod);
            if (lod >= 2)
            {
                Box(m, Affine3.Translation(0, H - 0.35, 0), B(GalvanisedDark, MaterialChannel.Metal), 1.5, 0.08, 0.08);
                Shapes.Torus(m, Affine3.Translation(0, 5.4, 0.12) * Affine3.RotationX(90 * Deg), B(CableBlack, MaterialChannel.Rubber), 0.32, 0.07, 6, 3, L0);
                return;
            }
            CrossArm(m, Affine3.Translation(0, H - 0.35, 0.0), lod);
            // LT rack: a vertical strap on the road side with shackle insulators.
            Box(m, Affine3.Translation(0, 7.25, 0.12), B(GalvanisedDark, MaterialChannel.Metal), 0.06, 0.75, 0.03);
            int shackles = P(lod, 2, 2, 0);
            for (int i = 0; i < shackles; i++)
                Shapes.Cylinder(m, Affine3.Translation(0, 6.98 + 0.26 * i, 0.19) * Affine3.RotationX(90 * Deg) * Affine3.Translation(0, -0.05, 0),
                                B(Porcelain, MaterialChannel.Plain), 0.045, 0.1, 4, 0, 0, false, true, L0);
            Tangle(m, I, 0.0, lod);
        }

        /// <summary>The concrete pole: a foot band darker with grime, the shaft, a white/red number band.</summary>
        private static void PolePrism(MeshData m, in Affine3 f, double h, int lod)
        {
            ShapeBrush conc = B(PoleConcrete, MaterialChannel.Concrete), foot = B(PoleFoot, MaterialChannel.Concrete);
            const double W0 = 0.24, W1 = 0.12;
            double chamfer = lod == 0 ? 0.18 : 0;
            if (lod >= 1)
            {
                Prism(m, f, conc, W0, W0 * 0.85, W1, W1 * 0.85, h, 0, true);
                return;
            }
            double Wd(double y) => W0 + (W1 - W0) * y / h;
            Prism(m, f, foot, W0, W0 * 0.85, Wd(0.7), Wd(0.7) * 0.85, 0.7, chamfer);
            Prism(m, f * Affine3.Translation(0, 0.7, 0), conc, Wd(0.7), Wd(0.7) * 0.85, Wd(2.4), Wd(2.4) * 0.85, 1.7, chamfer);
            Prism(m, f * Affine3.Translation(0, 2.4, 0), B(Danger, MaterialChannel.Paint), Wd(2.4) + 0.004, Wd(2.4) * 0.85 + 0.004, Wd(2.75) + 0.004,
                  Wd(2.75) * 0.85 + 0.004, 0.35, chamfer);
            Prism(m, f * Affine3.Translation(0, 2.75, 0), conc, Wd(2.75), Wd(2.75) * 0.85, W1, W1 * 0.85, h - 2.75, chamfer, true);
        }

        /// <summary>A galvanised channel cross-arm (along X) with three brown porcelain pin insulators on it.</summary>
        private static void CrossArm(MeshData m, in Affine3 f, int lod)
        {
            ShapeBrush steel = B(GalvanisedDark, MaterialChannel.Metal), por = B(Porcelain, MaterialChannel.Plain);
            Box(m, f, steel, 1.5, 0.08, 0.08);
            if (lod >= 2) return;
            if (lod == 0)
            {
                Shapes.Bar(m, f, steel, -0.55, -0.02, 0, -0.05, -0.5, 0, 0.018, 3, L0);
                Shapes.Bar(m, f, steel, 0.55, -0.02, 0, 0.05, -0.5, 0, 0.018, 3, L0);
            }
            for (int i = -1; i <= 1; i++)
            {
                Affine3 pf = f * Affine3.Translation(0.62 * i, 0.04, 0);
                if (lod == 0)
                {
                    Profile2 pr = Prof.Clear(false);
                    pr.Add(0.025, 0, true).Add(0.07, 0.06).Add(0.045, 0.11).Add(0, 0.18);
                    Shapes.Lathe(m, pf, por, pr, 5, L0);
                }
                else Shapes.Frustum(m, pf, por, 0.06, 0.03, 0.17, 4, 0, 0, false, true, L0);
            }
        }

        /// <summary>The Kathmandu cable tangle round a pole at x: two coils of slack cable, a bundle clamped down the
        /// pole, a slack loop hanging in front, ISP junction boxes.</summary>
        private static void Tangle(MeshData m, in Affine3 f, double x, int lod, bool full = true)
        {
            ShapeBrush cable = B(CableBlack, MaterialChannel.Rubber);
            // Coils hang flat against the pole sides, tilted a little.
            Shapes.Torus(m, f * Affine3.Translation(x + 0.1, 5.55, 0.12) * Affine3.RotationZ(80 * Deg) * Affine3.RotationX(12 * Deg), cable, 0.36, 0.055, P(lod, 9, 6, 5), 3, L0);
            if (lod == 0 && full)
                Shapes.Torus(m, f * Affine3.Translation(x - 0.12, 4.95, -0.04) * Affine3.RotationZ(-75 * Deg) * Affine3.RotationX(-20 * Deg), cable, 0.28, 0.045, 8, 3, L0);
            // Bundle down the pole (front face), out to a box at 2.9 m.
            Path3 ctrl = PathB.Clear();
            ctrl.Add(x + 0.02, 6.6, 0.1).Add(x + 0.03, 5.2, 0.1).Add(x + 0.05, 3.6, 0.1).Add(x + 0.12, 3.15, 0.14);
            TubeAlong(m, f, cable, ctrl, 0.035, 0.03, P(lod, 4, 3, 3), false);
            Box(m, f * Affine3.Translation(x + 0.16, 2.95, 0.16), B(IspBox, MaterialChannel.Plain), 0.18, 0.26, 0.09);
            if (lod == 0 && full)
            {
                Box(m, f * Affine3.Translation(x - 0.13, 6.2, 0.04), B(IspBox, MaterialChannel.Plain), 0.12, 0.18, 0.08);
                // A slack loop hanging in front of the pole from both sides of the bundle.
                ctrl.Clear();
                ctrl.Add(x + 0.12, 6.35, 0.08).Add(x + 0.28, 5.85, 0.38).Add(x + 0.16, 5.38, 0.52).Add(x - 0.1, 5.32, 0.5).Add(x - 0.26, 5.75, 0.38)
                    .Add(x - 0.13, 6.3, 0.08);
                TubeAlong(m, f, cable, ctrl, 0.024, 0.024, 3, false);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // H-frame transformer: two poles 2.4 m apart, a channel platform at 4 m with the tank (radiator fins, HT and LT
        // bushings), drop-out fuses on the top arm, LT cables down to a distribution box, a danger plate (§7).

        private static void Transformer(MeshData m, int lod)
        {
            const double H = 9.0, Span = 2.4, PlatY = 4.0;
            Affine3 I = Affine3.Identity;
            ShapeBrush steel = B(GalvanisedDark, MaterialChannel.Metal), tank = B(TransformerGrey, MaterialChannel.Metal);
            for (int s = -1; s <= 1; s += 2) PolePrism(m, Affine3.Translation(s * 0.5 * Span, 0, 0), H, Math.Max(lod, 1));
            // Platform: two channels and a grating.
            BoxSpan(m, I, steel, -0.5 * Span - 0.25, 0.5 * Span + 0.25, PlatY - 0.12, PlatY, 0.1, 0.2);
            BoxSpan(m, I, steel, -0.5 * Span - 0.25, 0.5 * Span + 0.25, PlatY - 0.12, PlatY, -0.2, -0.1);
            if (lod < 2) BoxSpan(m, I, B(0x6E7477, MaterialChannel.Metal), -0.75, 0.75, PlatY, PlatY + 0.04, -0.35, 0.45);
            // Tank, lid, radiator fins.
            Affine3 t = Affine3.Translation(0, PlatY + 0.04, 0.05);
            Box(m, t * Affine3.Translation(0, 0.55, 0), tank, 0.95, 1.1, 0.62);
            if (lod < 2) Box(m, t * Affine3.Translation(0, 1.13, 0), B(0x6B7578, MaterialChannel.Metal), 1.02, 0.06, 0.7);
            if (lod == 0)
                for (int s = -1; s <= 1; s += 2)
                    Box(m, t * Affine3.Translation(0, 0.5, s * 0.38), B(0x748083, MaterialChannel.Metal), 0.8, 0.85, 0.14);
            if (lod == 0)
            {
                // Three HT bushings on the lid.
                for (int i = -1; i <= 1; i++)
                {
                    Profile2 pr = Prof.Clear(false);
                    pr.Add(0.055, 0, true).Add(0.04, 0.14).Add(0, 0.26);
                    Shapes.Lathe(m, t * Affine3.Translation(0.3 * i, 1.16, 0), B(Porcelain, MaterialChannel.Plain), pr, 4, L0);
                }
                // Danger plate on the front, LT cables down the right pole into the distribution box.
                Box(m, t * Affine3.Translation(0, 0.55, 0.316), B(Danger, MaterialChannel.Paint), 0.28, 0.2, 0.01);
                Path3 ctrl = PathB.Clear();
                ctrl.Add(0.2, PlatY + 0.95, 0.36).Add(0.55, PlatY - 0.25, 0.3).Add(0.5 * Span - 0.08, PlatY - 1.0, 0.16).Add(0.5 * Span - 0.08, 1.9, 0.16);
                TubeAlong(m, I, B(CableBlack, MaterialChannel.Rubber), ctrl, 0.04, 0.04, 4, false);
            }
            if (lod < 2)
            {
                BoxSpan(m, I, B(0x9AA3A6, MaterialChannel.Metal), 0.5 * Span - 0.32, 0.5 * Span + 0.22, 1.25, 1.95, 0.06, 0.36); // distribution box
                // Top arm with three drop-out fuses; HT jumpers down to the bushings.
                // Top beam across both poles carrying the 11 kV insulators.
                BoxSpan(m, I, steel, -0.5 * Span - 0.25, 0.5 * Span + 0.25, H - 0.44, H - 0.36, -0.04, 0.04);
                CrossArm(m, Affine3.Translation(0, H - 0.4, 0.0), lod == 0 ? 1 : 2);
                for (int i = -1; i <= 1 && lod == 0; i++)
                {
                    // Drop-out fuse hanging under the top beam, its jumper down to the bushing.
                    Box(m, Affine3.Translation(0.45 * i, H - 0.62, 0.08) * Affine3.RotationX(18 * Deg), B(Porcelain, MaterialChannel.Plain), 0.05, 0.28, 0.05);
                    Shapes.Bar(m, I, B(0x3A3C3F, MaterialChannel.Metal), 0.45 * i, H - 0.78, 0.1, 0.3 * i, PlatY + 1.45, 0.05, 0.008, 3, L0);
                }
            }
            if (lod == 0) Tangle(m, I, -0.5 * Span, lod, false);
            else if (lod == 1) Shapes.Torus(m, Affine3.Translation(-0.5 * Span + 0.1, 5.55, 0.12) * Affine3.RotationZ(80 * Deg), B(CableBlack, MaterialChannel.Rubber), 0.36, 0.06, 6, 3, L0);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Lattice transmission tower (30 m): four tapering legs, horizontal girts and X bracing in three panels, two
        // cross-arms with insulator strings.

        private static void Pylon(MeshData m, int lod)
        {
            const double H = 30, B0 = 3.4, B1 = 0.8, Waist = 22;
            Affine3 I = Affine3.Identity;
            ShapeBrush steel = B(LatticeGrey, MaterialChannel.Metal);
            int rr = P(lod, 4, 3, 3);
            double Half(double y) => y <= Waist ? B0 + (B1 - B0) * y / Waist : B1;
            for (int c = 0; c < 4; c++)
            {
                double sx = c % 2 == 0 ? -1 : 1, sz = c < 2 ? -1 : 1;
                Path3 p = PathA.Clear();
                p.Add(sx * Half(0.03), 0.03, sz * Half(0.03)).Add(sx * B1, Waist, sz * B1).Add(sx * B1, H, sz * B1);
                TubeAlong(m, I, steel, p, 0.12, 0.07, rr, false);
            }
            if (lod >= 2)
            {
                Box(m, Affine3.Translation(0, 24.2, 0), steel, 12, 0.35, 0.35);
                Box(m, Affine3.Translation(0, 27.6, 0), steel, 9, 0.35, 0.35);
                return;
            }
            int panels = lod == 0 ? 3 : 2;
            for (int k = 1; k <= panels; k++)
            {
                double y = PylonLevels[k], h = Half(y);
                double y0 = PylonLevels[k - 1], h0 = Half(y0);
                for (int f = 0; f < 4; f++)
                {
                    // Face corners a (left) and b (right) at the panel's bottom and top.
                    double ax, az, bx, bz;
                    switch (f)
                    {
                        case 0: ax = -1; az = -1; bx = 1; bz = -1; break;
                        case 1: ax = 1; az = -1; bx = 1; bz = 1; break;
                        case 2: ax = 1; az = 1; bx = -1; bz = 1; break;
                        default: ax = -1; az = 1; bx = -1; bz = -1; break;
                    }
                    Strut(m, steel, ax * h, y, az * h, bx * h, y, bz * h, 0.05);
                    Strut(m, steel, ax * h0, Math.Max(0.3, y0), az * h0, bx * h, y, bz * h, 0.045);
                    if (lod == 0) Strut(m, steel, bx * h0, Math.Max(0.3, y0), bz * h0, ax * h, y, az * h, 0.045);
                }
            }
            // Cross-arms (tapered beams) and insulator strings.
            for (int a = 0; a < 2; a++)
            {
                double y = a == 0 ? 24.2 : 27.6, half = a == 0 ? 6 : 4.5;
                Box(m, Affine3.Translation(0, y, 0), steel, 2 * half, 0.4, 0.5);
                if (lod == 0)
                    for (int s = -1; s <= 1; s += 2)
                        Shapes.Cylinder(m, Affine3.Translation(s * (half - 0.3), y - 1.8, 0), B(0x7C8B96, MaterialChannel.Glass), 0.11, 1.6, 5, 0, 0, true, false, L0);
            }
            if (lod == 0) Shapes.Cone(m, Affine3.Translation(0, H, 0), steel, 1.0, 1.6, 4, 0, 0, false, L0);
        }

        /// <summary>An open-ended triangular-section strut between two points (lattice members).</summary>
        private static void Strut(MeshData m, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz, double r)
        {
            Path3 p = PathA.Clear().Add(ax, ay, az).Add(bx, by, bz);
            TubeAlong(m, Affine3.Identity, b, p, r, r, 3, false);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Telecom mast: a tapered red-and-white monopole, a platform with three antenna panels and a dish.

        private static void Mast(MeshData m, int lod)
        {
            const double H = 25;
            int radial = P(lod, 10, 6, 4);
            int bands = lod >= 2 ? 1 : 5;
            for (int i = 0; i < bands; i++)
            {
                double y0 = H * i / bands, y1 = H * (i + 1) / bands;
                double r0 = 0.6 + (0.18 - 0.6) * y0 / H, r1 = 0.6 + (0.18 - 0.6) * y1 / H;
                Shapes.Frustum(m, Affine3.Translation(0, y0, 0), B(i % 2 == 0 ? MastRed : MastWhite, MaterialChannel.Paint), r0, r1, y1 - y0, radial, 0, 0, false, i == bands - 1, L0);
            }
            if (lod >= 2) return;
            ShapeBrush grey = B(0xC8CCCE, MaterialChannel.Metal);
            Shapes.Cylinder(m, Affine3.Translation(0, H - 2.2, 0), grey, 0.9, 0.15, 6, 0, 0, true, true, L0);
            for (int i = 0; i < 3; i++)
            {
                Affine3 a = Affine3.Yaw(120 * i) * Affine3.Translation(0, H - 1.0, 0.75);
                Box(m, a, B(0xE6E6E2, MaterialChannel.Plain), 0.3, 1.8, 0.12);
            }
            if (lod == 0)
            {
                Affine3 d = Affine3.Translation(0.35, H - 4.0, 0.0) * Affine3.RotationZ(-90 * Deg);
                Shapes.Dome(m, d, B(0xE6E6E2, MaterialChannel.Plain), 0.45, 0.15, 10, true, L0);
                Shapes.Bar(m, Affine3.Identity, grey, 0, H - 2.05, 0, 0, H + 1.2, 0, 0.05, 4, L0); // lightning rod
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Brick-kiln chimney: round tapered brick with steel bands and a sooty top.

        private static void Chimney(MeshData m, int lod)
        {
            const double H = 18, R0 = 1.4, R1 = 0.85;
            int radial = P(lod, 14, 8, 6);
            ShapeBrush brick = B(KilnBrick, MaterialChannel.Brick), band = B(0x4A4644, MaterialChannel.Metal);
            double Rad(double y) => R0 + (R1 - R0) * y / H;
            BoxSpan(m, Affine3.Identity, B(0x8A7A6A, MaterialChannel.Brick), -1.7, 1.7, 0, 0.6, -1.7, 1.7);
            int r0 = m.VertexCount;
            Shapes.Frustum(m, Affine3.Translation(0, 0.6, 0), brick, Rad(0.6), Rad(H - 1.2), H - 1.8, radial, 0, 0, false, false, L0);
            ShapeColor.JitterByPosition(m, r0, m.VertexCount - r0, 0.06f, 0x4B494C4Eu, 1.5);
            // Sooty crown with an open, dark flue.
            Shapes.Frustum(m, Affine3.Translation(0, H - 1.2, 0), B(Soot, MaterialChannel.Brick), Rad(H - 1.2), R1 + 0.05, 1.2, radial, 0, 0, false, false, L0);
            Shapes.Lathe(m, Affine3.Translation(0, H, 0), B(0x151414, MaterialChannel.Plain), Prof.Clear(false).Add(R1 + 0.05, 0, true).Add(R1 - 0.15, 0, true).Add(R1 - 0.15, -1.0, true), radial, L0);
            if (lod >= 2) return;
            int bands = lod == 0 ? 4 : 2;
            for (int i = 1; i <= bands; i++)
            {
                double y = 0.6 + (H - 2.0) * i / (bands + 1);
                Shapes.Cylinder(m, Affine3.Translation(0, y, 0), band, Rad(y) + 0.04, 0.22, radial, 0, 0, false, false, L0);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Polyethylene water tank (1,000 L, ribbed) on a steel angle stand; the tank body takes the instance tint.

        private static void Tank(MeshData m, int lod)
        {
            const double StandH = 1.25, R = 0.56, TankH = 1.2;
            Affine3 I = Affine3.Identity;
            ShapeBrush steel = B(0x3C4046, MaterialChannel.Metal);
            double l = 0.47;
            if (lod >= 2)
            {
                Prism(m, I, steel, 1.0, 1.0, 1.0, 1.0, StandH, 0, false);
                Shapes.Lathe(m, Affine3.Translation(0, StandH, 0), Tinted(TankBase, MaterialChannel.Plain),
                             Prof.Clear(false).Add(0, 0, true).Add(R, 0, true).Add(R, TankH, true).Add(0, TankH + 0.12, true), 6, L0);
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                double x = i % 2 == 0 ? -l : l, z = i < 2 ? -l : l;
                Prism(m, Affine3.Translation(x, 0, z), steel, 0.05, 0.05, 0.05, 0.05, StandH, 0);
            }
            BoxSpan(m, I, steel, -l - 0.04, l + 0.04, StandH - 0.06, StandH, -l - 0.04, l + 0.04);
            if (lod == 0)
            {
                BoxSpan(m, I, steel, -l, l, 0.45, 0.5, -l - 0.02, -l + 0.02);
                BoxSpan(m, I, steel, -l, l, 0.45, 0.5, l - 0.02, l + 0.02);
            }
            ShapeBrush body = Tinted(TankBase, MaterialChannel.Plain);
            Profile2 pr = Prof.Clear(false);
            double y0 = StandH;
            pr.Add(0, 0, true).Add(R - 0.05, 0, true).Add(R, 0.06);
            int ribs = P(lod, 3, 1, 0);
            for (int i = 0; i < ribs; i++)
            {
                double y = 0.2 + (TankH - 0.45) * (i + 0.5) / ribs;
                pr.Add(R + 0.025, y).Add(R, y + 0.08);
            }
            pr.Add(R - 0.02, TankH - 0.12).Add(R * 0.75, TankH + 0.05).Add(R * 0.3, TankH + 0.13, true).Add(0, TankH + 0.13, true);
            Shapes.Lathe(m, Affine3.Translation(0, y0, 0), body, pr, P(lod, 10, 8, 6), L0);
            Shapes.Cylinder(m, Affine3.Translation(0, y0 + TankH + 0.12, 0), body, 0.17, 0.07, P(lod, 8, 6, 4), lod == 0 ? 0.02 : 0, 1, false, true, L0);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Brick well (inar): round brick parapet with a stone coping, dark water, a wooden pulley frame.

        private static void Well(MeshData m, int lod)
        {
            const double R = 0.8, H = 0.8, Wall = 0.22;
            Affine3 I = Affine3.Identity;
            int radial = P(lod, 12, 6, 5);
            Profile2 pr = Prof.Clear(true);
            if (lod >= 2) pr.Add(R - Wall, 0, true).Add(R, 0, true).Add(R, H, true).Add(R - Wall, H, true);
            // Closed ring section (outside on the right of travel = counter-clockwise).
            else pr.Add(R - Wall, 0, true).Add(R, 0, true).Add(R, H - 0.1, true).Add(R + 0.03, H - 0.08, true).Add(R + 0.03, H, true).Add(R - Wall - 0.03, H, true)
              .Add(R - Wall - 0.03, H - 0.08, true).Add(R - Wall, H - 0.1, true);
            int r0 = m.VertexCount;
            Shapes.Lathe(m, I, B(WellBrick, MaterialChannel.Brick), pr, radial, L0);
            // Coping in stone colour (the top ring of vertices).
            for (int v = r0; v < m.VertexCount; v++)
                if (m.Positions[3 * v + 1] > H - 0.09)
                {
                    uint c = WellStone << 8;
                    m.Colors[4 * v] = (byte)(c >> 24);
                    m.Colors[4 * v + 1] = (byte)(c >> 16);
                    m.Colors[4 * v + 2] = (byte)(c >> 8);
                    m.Uv0[2 * v] = (float)MaterialChannel.Stone;
                }
            if (lod < 2) Shapes.Cylinder(m, Affine3.Translation(0, 0.25, 0), B(WaterDark, MaterialChannel.Water), R - Wall + 0.01, 0.01, radial, 0, 0, false, true, L0);
            ShapeBrush wood = B(0x6B4129, MaterialChannel.Wood);
            for (int s = -1; s <= 1; s += 2) Prism(m, Affine3.Translation(s * (R + 0.08), 0, 0), wood, 0.1, 0.1, 0.08, 0.08, 1.85, 0, true);
            Shapes.Bar(m, I, wood, -R - 0.1, 1.8, 0, R + 0.1, 1.8, 0, 0.045, P(lod, 6, 4, 3), L0);
            if (lod == 0)
            {
                Shapes.Cylinder(m, Affine3.Translation(-0.05, 1.8, 0) * Affine3.RotationZ(-90 * Deg), B(0x4A4644, MaterialChannel.Metal), 0.11, 0.1, 8, 0.02, 1, true, true, L0);
                Shapes.Bar(m, I, B(0xB9A27A, MaterialChannel.Fabric), 0, 1.69, 0.02, 0, 0.9, 0.02, 0.008, 3, L0); // rope
                Shapes.Frustum(m, Affine3.Translation(0, 0.72, 0.02), B(0x8A8F93, MaterialChannel.Metal), 0.09, 0.12, 0.18, 8, 0, 0, true, false, L0); // bucket
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Solar panel array: two panels side by side tilted south on an aluminium frame.

        private static void Solar(MeshData m, int lod)
        {
            Affine3 I = Affine3.Identity;
            ShapeBrush frame = B(PanelFrame, MaterialChannel.Metal), cells = B(PanelCells, MaterialChannel.Glass);
            Affine3 pf = Affine3.Translation(0, 1.05, 0) * Affine3.RotationX(-30 * Deg);
            Box(m, pf, frame, 2.1, 0.05, 1.25);
            Box(m, pf * Affine3.Translation(0, 0.028, 0), cells, 2.04, 0.01, 1.19);
            if (lod >= 2)
                for (int sx = -1; sx <= 1; sx += 2) Shapes.Bar(m, I, frame, sx * 0.9, 0, 0, sx * 0.9, 1.05, 0, 0.035, 3, L0);
            if (lod < 2)
            {
                Box(m, pf * Affine3.Translation(0, 0.034, 0), frame, 0.03, 0.006, 1.19);
                for (int s = -1; s <= 1; s += 2)
                {
                    // The panel's high edge is at +Z (tilted towards the sun behind it): tall legs there.
                    Shapes.Bar(m, I, frame, s * 0.9, 0, -0.5, s * 0.9, 0.76, -0.5, 0.03, 4, L0);
                    Shapes.Bar(m, I, frame, s * 0.9, 0, 0.5, s * 0.9, 1.33, 0.5, 0.03, 4, L0);
                    if (lod == 0) Shapes.Bar(m, I, frame, s * 0.9, 0.05, -0.5, s * 0.9, 1.0, 0.45, 0.02, 4, L0);
                }
            }
            if (lod == 0)
                for (int k = -2; k <= 2; k++)
                    Box(m, pf * Affine3.Translation(0.41 * k, 0.035, 0), frame, 0.008, 0.004, 1.19);
        }
    }
}
