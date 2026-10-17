using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Props
{
    public static partial class StreetPropMesher
    {
        // Palette (0xRRGGBB, ref_props.md §1-3).
        private const uint Galvanised = 0x9DA3A6, GalvanisedDark = 0x7F878B, Footing = 0xA9A59C, HeadGrey = 0x5F666C, LensWarm = 0xFFF1C9;
        private const uint SolarPole = 0xC9CCCB, PanelFrame = 0xB8BCBE, PanelCells = 0x1F3A6B, BatteryGrey = 0xAEB3B5;
        private const uint IronBlack = 0x1C1D1F, Gold = 0xC9A23A, LanternGlass = 0xF4EBD0;

        // ---------------------------------------------------------------------------------------------------------
        // Grid lamp: galvanised tapered pole bending over in one swan neck into the arm, flat LED head (§1).

        private static void GridLamp(MeshData m, int lod)
        {
            const double H = 10.0, R = 1.6, Tilt = 7 * Deg, ArmLen = 0.45;
            double yb = H - R * Math.Sin(90 * Deg - Tilt) - ArmLen * Math.Sin(Tilt) - 0.04;
            ShapeBrush steel = B(Galvanised, MaterialChannel.Metal);
            Affine3 I = Affine3.Identity;
            if (lod >= 2)
            {
                Shapes.Frustum(m, I, steel, 0.1, 0.06, yb + 0.6, 4, 0, 0, false, false, L0);
                double ex = R + ArmLen, ey = yb + R + 0.1;
                Shapes.Bar(m, I, steel, 0, yb + 0.5, 0, 0, ey, ex - 0.25, 0.05, 3, L0);
                Box(m, Affine3.Translation(0, ey, ex), B(HeadGrey, MaterialChannel.Metal), 0.26, 0.08, 0.62);
                return;
            }
            int radial = P(lod, 10, 6, 4);
            // Footing, flange, shaft.
            Shapes.Cylinder(m, I, B(Footing, MaterialChannel.Concrete), 0.24, 0.18, P(lod, 8, 6, 4), lod == 0 ? 0.03 : 0, 1, false, true, L0);
            if (lod == 0)
            {
                Shapes.Frustum(m, Affine3.Translation(0, 0.18, 0), B(GalvanisedDark, MaterialChannel.Metal), 0.145, 0.12, 0.1, radial, 0, 0, false, true, L0);
                // Access door on the back, a little proud of the shaft.
                Box(m, Affine3.Translation(0, 0.72, -0.1) * Affine3.RotationX(-0.6 * Deg), B(GalvanisedDark, MaterialChannel.Metal), 0.075, 0.26, 0.03);
            }
            Shapes.Frustum(m, Affine3.Translation(0, 0.17, 0), steel, 0.105, 0.062, yb - 0.17, radial, 0, 0, false, false, L0);
            // Swan neck: an arc of radius R in the YZ plane from vertical to Tilt above horizontal, then a short straight arm.
            Path3 p = PathA.Clear();
            int arc = P(lod, 6, 4, 2);
            double thetaEnd = 90 * Deg - Tilt;
            p.Add(0, yb - 0.3, 0);
            for (int i = 0; i <= arc; i++)
            {
                double th = thetaEnd * i / arc;
                p.Add(0, yb + R * Math.Sin(th), R - R * Math.Cos(th));
            }
            double tx = Math.Cos(thetaEnd), tz = Math.Sin(thetaEnd); // tangent (y, z)
            double ay = yb + R * Math.Sin(thetaEnd), az = R - R * Math.Cos(thetaEnd);
            double ey2 = ay + ArmLen * tx, ez2 = az + ArmLen * tz;
            p.Add(0, ey2, ez2);
            TubeAlong(m, I, steel, p, 0.062, 0.045, P(lod, 6, 5, 4));
            // LED head: a slim rounded lozenge hung on the arm end, tilted with it; its underside is the lens.
            int h0 = m.VertexCount;
            Affine3 head = Affine3.Translation(0, ey2 - 0.015, ez2 + 0.27) * Affine3.RotationX(-Tilt);
            Shapes.Superellipsoid(m, head, B(HeadGrey, MaterialChannel.Metal), 0.14, 0.05, 0.37, 0.45, 0.55, P(lod, 12, 8, 8), L0);
            Underside(m, h0, LensWarm, MaterialChannel.Glass);
            if (lod == 0)
            {
                // Clamp collar where the arm enters the head, and a heat-sink ridge on top.
                Shapes.Cylinder(m, Affine3.Translation(0, ey2 - 0.005, ez2 - 0.1) * Affine3.RotationX(90 * Deg - Tilt), B(GalvanisedDark, MaterialChannel.Metal),
                                0.058, 0.09, 6, 0, 0, false, true, L0);
                Box(m, head * Affine3.Translation(0, 0.045, 0.02), B(GalvanisedDark, MaterialChannel.Metal), 0.05, 0.03, 0.42);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Solar lamp: lighter pole, tilted panel on top, short inclined arm with a slim head, battery box (§2).

        private static void SolarLamp(MeshData m, int lod)
        {
            const double H = 7.75, Arm = 1.3, Lift = 15 * Deg, ArmY = 7.15;
            ShapeBrush pole = B(SolarPole, MaterialChannel.Paint), frame = B(PanelFrame, MaterialChannel.Metal);
            Affine3 I = Affine3.Identity;
            int radial = P(lod, 10, 6, 4);
            if (lod < 2) Shapes.Cylinder(m, I, B(Footing, MaterialChannel.Concrete), 0.2, 0.16, radial, lod == 0 ? 0.025 : 0, 1, false, true, L0);
            Shapes.Frustum(m, Affine3.Translation(0, lod < 2 ? 0.15 : 0, 0), pole, 0.078, 0.048, H - (lod < 2 ? 0.15 : 0), radial, 0, 0, false, true, L0);
            double ex = ArmY + Arm * Math.Sin(Lift), ez = Arm * Math.Cos(Lift);
            // Panel: aluminium frame with the cell sheet, tilted 25 degrees back (away from the road), on a stub.
            Affine3 pf = Affine3.Translation(0, H + 0.24, -0.08) * Affine3.RotationX(-25 * Deg);
            if (lod >= 2)
            {
                Shapes.Bar(m, I, pole, 0, ArmY, 0, 0, ex, ez, 0.03, 3, L0);
                Box(m, Affine3.Translation(0, ex - 0.01, ez + 0.2) * Affine3.RotationX(-Lift), B(HeadGrey, MaterialChannel.Metal), 0.2, 0.06, 0.5);
                Box(m, pf, B(PanelCells, MaterialChannel.Glass), 1.25, 0.05, 0.8);
                return;
            }
            Shapes.Bar(m, I, pole, 0, ArmY - 0.04, 0, 0, ex, ez, 0.032, P(lod, 6, 4, 3), L0);
            int h0 = m.VertexCount;
            Affine3 head = Affine3.Translation(0, ex - 0.005, ez + 0.2) * Affine3.RotationX(-Lift);
            Shapes.Superellipsoid(m, head, B(HeadGrey, MaterialChannel.Metal), 0.11, 0.036, 0.27, 0.45, 0.55, P(lod, 12, 8, 8), L0);
            Underside(m, h0, LensWarm, MaterialChannel.Glass);
            Shapes.Bar(m, I, frame, 0, H - 0.05, 0, 0, H + 0.2, -0.06, 0.035, P(lod, 6, 4, 3), L0);
            Box(m, pf, frame, 1.25, 0.045, 0.8);
            Box(m, pf * Affine3.Translation(0, 0.026, 0), B(PanelCells, MaterialChannel.Glass), 1.19, 0.012, 0.74);
            if (lod == 0)
            {
                // Cell divisions (two silver bus bars) and the battery box on the back of the pole.
                Box(m, pf * Affine3.Translation(0, 0.033, 0.12), frame, 1.19, 0.006, 0.018);
                Box(m, pf * Affine3.Translation(0, 0.033, -0.12), frame, 1.19, 0.006, 0.018);
                Shapes.RoundedBox(m, Affine3.Translation(0, 3.3, -0.17), B(BatteryGrey, MaterialChannel.Metal), 0.32, 0.46, 0.16, 0.025, 1, L0);
                Shapes.Bar(m, I, B(HeadGrey, MaterialChannel.Rubber), 0, 3.56, -0.12, 0, 7.0, -0.045, 0.012, 4, L0); // cable to the panel
            }
            else Box(m, Affine3.Translation(0, 3.3, -0.17), B(BatteryGrey, MaterialChannel.Metal), 0.3, 0.45, 0.15);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Heritage lantern lamp: black pole with gold rings and base, scroll arm with a hanging bell lantern towards
        // the road and a second lantern on a crook at the top towards the footpath (§3).

        private static void HeritageLamp(MeshData m, int lod)
        {
            const double H = 5.75, ArmY = 4.3;
            ShapeBrush iron = B(IronBlack, MaterialChannel.Metal), gold = B(Gold, MaterialChannel.Gilt);
            Affine3 I = Affine3.Identity;
            int radial = P(lod, 8, 6, 4);
            if (lod >= 2)
            {
                Shapes.Frustum(m, I, gold, 0.13, 0.08, 0.3, 4, 0, 0, false, true, L0);
                Shapes.Cylinder(m, I, iron, 0.055, H + 0.1, 4, 0, 0, false, true, L0);
                Shapes.Bar(m, I, iron, 0, ArmY, 0.03, 0, ArmY + 0.3, 0.8, 0.025, 3, L0);
                Lantern(m, Affine3.Translation(0, ArmY + 0.25, 0.85), lod);
                Lantern(m, Affine3.Translation(0, H + 0.2, -0.35), lod);
                return;
            }
            // Flared gold base collar.
            Profile2 pr = Prof.Clear(false);
            pr.Add(0.135, 0, true).Add(0.135, 0.045, true).Add(0.09, 0.14).Add(0.06, 0.32, true);
            Shapes.Lathe(m, I, gold, pr, radial, L0);
            Shapes.Cylinder(m, Affine3.Translation(0, 0.3, 0), iron, 0.052, H - 0.3, radial, 0, 0, false, lod > 0, L0);
            if (lod > 0)
            {
                Shapes.Bar(m, I, iron, 0, ArmY, 0.03, 0, ArmY + 0.29, 0.62, 0.024, 3, L0);
                Shapes.Bar(m, I, iron, 0, ArmY + 0.29, 0.62, 0, ArmY + 0.04, 0.89, 0.022, 3, L0);
                Lantern(m, Affine3.Translation(0, ArmY + 0.02, 0.89), lod);
                Shapes.Bar(m, I, iron, 0, H - 0.25, -0.03, 0, H - 0.12, -0.38, 0.02, 3, L0);
                Lantern(m, Affine3.Translation(0, H - 0.2, -0.38), lod);
                return;
            }
            // Vase knop at 1.2 m and gold rings at 3.0 m, under the arm and at the top.
            pr = Prof.Clear(false);
            pr.Add(0.058, 0, true).Add(0.084, 0.09).Add(0.058, 0.24, true);
            Shapes.Lathe(m, Affine3.Translation(0, 1.1, 0), gold, pr, 4, L0);
            for (int i = 0; i < 2; i++)
                Shapes.Cylinder(m, Affine3.Translation(0, i == 0 ? 3.0 : ArmY - 0.12, 0), gold, 0.066, 0.07, 5, 0, 0, false, false, L0);
            // Top finial on a gold collar.
            Shapes.Cone(m, Affine3.Translation(0, H - 0.05, 0), gold, 0.066, 0.21, 4, 0, 0, true, L0);
            // Scroll arm towards +Z: out, up and over, down to the hook.
            Path3 ctrl = PathB.Clear();
            ctrl.Add(0, ArmY, 0.03).Add(0, ArmY + 0.17, 0.17).Add(0, ArmY + 0.35, 0.38).Add(0, ArmY + 0.41, 0.6).Add(0, ArmY + 0.33, 0.79).Add(0, ArmY + 0.17, 0.85)
                .Add(0, ArmY + 0.02, 0.85);
            TubeAlong(m, I, iron, ctrl, 0.024, 0.02, 4);
            if (lod == 0)
            {
                // The curl under the arm (a spiral of about one and a quarter turns) with a gold rosette at its eye.
                ctrl.Clear();
                const double cy = ArmY + 0.08, cz = 0.36;
                ctrl.Add(0, ArmY - 0.3, 0.03);
                for (int i = 0; i <= 7; i++)
                {
                    double a = -110 * Deg + i * 58 * Deg, r = 0.17 - 0.016 * i;
                    ctrl.Add(0, cy + r * Math.Sin(a), cz + r * Math.Cos(a));
                }
                TubeAlong(m, I, iron, ctrl, 0.013, 0.009, 3);
                Shapes.Ellipsoid(m, Affine3.Translation(0.0, cy + 0.0, cz - 0.0), gold, 0.03, 0.05, 0.05, 4, false, L0);
            }
            Lantern(m, Affine3.Translation(0, ArmY + 0.02, 0.85), lod);
            // Top crook towards -Z with the second lantern.
            ctrl.Clear();
            ctrl.Add(0, H - 0.25, -0.03).Add(0, H - 0.1, -0.15).Add(0, H - 0.07, -0.3).Add(0, H - 0.17, -0.38);
            TubeAlong(m, I, iron, ctrl, 0.02, 0.018, 4);
            Lantern(m, Affine3.Translation(0, H - 0.2, -0.38), lod);
        }

        /// <summary>A hanging bell lantern whose hook sits at the frame origin: gold collar, black bell hood with a
        /// wide brim, clear ribbed glass bowl below.</summary>
        private static void Lantern(MeshData m, in Affine3 f, int lod)
        {
            ShapeBrush iron = B(IronBlack, MaterialChannel.Metal), gold = B(Gold, MaterialChannel.Gilt), glass = B(LanternGlass, MaterialChannel.Glass);
            int radial = P(lod, 6, 5, 4);
            if (lod >= 2)
            {
                Shapes.Frustum(m, f * Affine3.Translation(0, -0.3, 0), iron, 0.2, 0.05, 0.16, 4, 0, 0, true, false, L0);
                return;
            }
            if (lod == 1)
            {
                Shapes.Frustum(m, f * Affine3.Translation(0, -0.3, 0), iron, 0.2, 0.04, 0.2, radial, 0, 0, true, false, L0);
                Shapes.Frustum(m, f * Affine3.Translation(0, -0.52, 0), glass, 0.05, 0.085, 0.22, radial, 0, 0, true, false, L0);
                return;
            }
            Shapes.Cylinder(m, f * Affine3.Translation(0, -0.1, 0), gold, 0.022, 0.1, 4, 0, 0, false, false, L0);
            Profile2 pr = Prof.Clear(false);
            pr.Add(0, -0.3, true).Add(0.21, -0.3, true).Add(0.11, -0.245).Add(0.045, -0.12, true).Add(0, -0.1, true);
            Shapes.Lathe(m, f, iron, pr, radial, L0);
            pr = Prof.Clear(false);
            pr.Add(0, -0.5, true).Add(0.075, -0.47).Add(0.1, -0.38).Add(0.085, -0.3, true);
            Shapes.Lathe(m, f, glass, pr, radial, L0);
        }
    }
}
