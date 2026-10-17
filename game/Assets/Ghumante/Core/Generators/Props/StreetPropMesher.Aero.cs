using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Props
{
    public static partial class StreetPropMesher
    {
        private const uint SockOrange = 0xF26A1B, SockWhite = 0xF2F0EA, PadConcrete = 0x8C8F92, MarkWhite = 0xF2F0E8, MarkYellow = 0xF2C230;
        private const uint Bronze = 0x5E4E34, PedestalStone = 0xBDB5A6, PedestalDark = 0x8C857A, TopiPurple = 0x7A3A5C;

        // ---------------------------------------------------------------------------------------------------------
        // Windsock: white mast with red bands, a swivel, a hoop and an orange-and-white striped sock drooping a
        // little towards +Z.

        private static void Windsock(MeshData m, int lod)
        {
            const double H = 5.7;
            Affine3 I = Affine3.Identity;
            int radial = P(lod, 8, 6, 4);
            int bands = lod >= 2 ? 1 : 4;
            for (int i = 0; i < bands; i++)
            {
                double y0 = H * i / bands, y1 = H * (i + 1) / bands;
                Shapes.Frustum(m, Affine3.Translation(0, y0, 0), B(i % 2 == 0 ? SockWhite : MastRed, MaterialChannel.Paint),
                               0.07 - 0.025 * y0 / H, 0.07 - 0.025 * y1 / H, y1 - y0, radial, 0, 0, false, i == bands - 1, L0);
            }
            if (lod < 2) Shapes.Cylinder(m, Affine3.Translation(0, H, 0), B(GalvanisedDark, MaterialChannel.Metal), 0.06, 0.12, radial, 0, 0, true, true, L0);
            // Sock axis: from the hoop at (0, H + 0.06, 0.1) towards +Z, sagging 0.35 m over its 2.4 m.
            Affine3 sock = Affine3.Translation(0, H + 0.06, 0.12) * Affine3.RotationX(90 * Deg - 8 * Deg);
            if (lod == 0) Shapes.Torus(m, sock * Affine3.RotationX(90 * Deg), B(GalvanisedDark, MaterialChannel.Metal), 0.36, 0.018, 12, 3, L0);
            int stripes = lod >= 2 ? 1 : 5;
            for (int i = 0; i < stripes; i++)
            {
                double z0 = 2.4 * i / stripes, z1 = 2.4 * (i + 1) / stripes;
                double r0 = 0.36 - 0.18 * z0 / 2.4, r1 = 0.36 - 0.18 * z1 / 2.4;
                Shapes.Frustum(m, sock * Affine3.Translation(0, z0, 0), B(i % 2 == 0 ? SockOrange : SockWhite, MaterialChannel.Fabric), r0, r1, z1 - z0, P(lod, 10, 6, 4),
                               0, 0, false, false, L0);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Helipad: a 15 m concrete pad with the white circle and the H.

        private static void Helipad(MeshData m, int lod)
        {
            const double R = 7.5, T = 0.04;
            int radial = P(lod, 24, 16, 12);
            Shapes.Cylinder(m, Affine3.Identity, B(PadConcrete, MaterialChannel.Concrete), R, T, radial, lod == 0 ? 0.03 : 0, 1, false, true, L0);
            ShapeBrush paint = B(MarkWhite, MaterialChannel.Marking);
            Affine3 h = Affine3.Translation(0, T + 0.006, 0);
            if (lod >= 2)
            {
                Box(m, h, paint, 3.2, 0.008, 3.6);
                return;
            }
            Shapes.Lathe(m, Affine3.Translation(0, T + 0.003, 0), paint, Prof.Clear(false).Add(5.6, 0, true).Add(5.0, 0, true), radial, L0);
            Box(m, h * Affine3.Translation(-1.3, 0, 0), paint, 0.6, 0.008, 4.2);
            Box(m, h * Affine3.Translation(1.3, 0, 0), paint, 0.6, 0.008, 4.2);
            Box(m, h, paint, 2.0, 0.008, 0.6);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Aircraft stand marking: a yellow lead-in line along +Z with a stop bar and a side bar.

        private static void StandMarking(MeshData m, int lod)
        {
            ShapeBrush paint = B(MarkYellow, MaterialChannel.Marking);
            Box(m, Affine3.Translation(0, 0.015, 0), paint, 0.3, 0.03, 12);
            Box(m, Affine3.Translation(0, 0.015, 6.0), paint, 3.0, 0.03, 0.3);
            if (lod < 2) Box(m, Affine3.Translation(1.4, 0.015, 4.5), paint, 0.2, 0.03, 3.0);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Public artwork: a bronze standing figure (long daura coat, topi, hands at the sides) on a moulded stone
        // pedestal; most public art in the valley is a statue.

        private static void Statue(MeshData m, int lod)
        {
            Affine3 I = Affine3.Identity;
            ShapeBrush stone = B(PedestalStone, MaterialChannel.Stone), dark = B(PedestalDark, MaterialChannel.Stone), bronze = B(Bronze, MaterialChannel.Metal);
            if (lod >= 2)
            {
                Prism(m, I, stone, 1.1, 1.1, 0.9, 0.9, 1.0, 0, true);
                Shapes.Lathe(m, Affine3.Translation(0, 1.0, 0) * Affine3.Scaling(1, 1, 0.72), bronze,
                             Prof.Clear(false).Add(0, 0, true).Add(0.2, 0, true).Add(0.21, 1.2).Add(0, 1.36, true), 4, L0);
                Shapes.Sphere(m, Affine3.Translation(0, 2.47, 0), bronze, 0.12, 4, L0);
                return;
            }
            Prism(m, I, dark, 1.3, 1.3, 1.3, 1.3, 0.15, 0, true);
            Prism(m, Affine3.Translation(0, 0.15, 0), stone, 0.9, 0.9, 0.86, 0.86, 0.75, lod == 0 ? 0.08 : 0);
            Prism(m, Affine3.Translation(0, 0.9, 0), dark, 1.05, 1.05, 1.05, 1.05, 0.1, 0, true);
            if (lod == 0) Box(m, Affine3.Translation(0, 0.55, 0.445), B(0x3A3A36, MaterialChannel.Stone), 0.5, 0.3, 0.02); // blank plaque
            Affine3 f = Affine3.Translation(0, 1.0, 0);
            int seg = P(lod, 8, 5, 4);
            // Legs and coat as one lathe: trousers, the daura flaring to the knees, chest, shoulders.
            Profile2 pr = Prof.Clear(false);
            pr.Add(0.0, 0, true).Add(0.17, 0, true).Add(0.15, 0.55).Add(0.22, 0.72).Add(0.2, 1.05).Add(0.21, 1.2).Add(0.13, 1.32).Add(0.06, 1.36, true).Add(0, 1.36, true);
            int b0 = m.VertexCount;
            Shapes.Lathe(m, f * Affine3.Scaling(1.0, 1.0, 0.72), bronze, pr, seg, L0);
            // Head, topi (a little forward-tilted), arms.
            if (lod == 0) Shapes.Cylinder(m, f * Affine3.Translation(0, 1.32, 0), bronze, 0.05, 0.08, 6, 0, 0, false, false, L0);
            if (lod == 0) Shapes.Ellipsoid(m, f * Affine3.Translation(0, 1.49, 0.01), bronze, 0.1, 0.12, 0.11, 8, true, L0);
            else Shapes.Sphere(m, f * Affine3.Translation(0, 1.49, 0.01), bronze, 0.11, 6, L0);
            Shapes.Frustum(m, f * Affine3.Translation(0, 1.56, -0.005) * Affine3.RotationX(-6 * Deg), bronze, 0.105, 0.085, 0.13, P(lod, 8, 6, 4), 0, 0, false, true, L0);
            for (int s = -1; s <= 1; s += 2)
            {
                Path3 p = PathA.Clear();
                p.Add(s * 0.2, 1.27, 0).Add(s * 0.25, 1.0, 0.02).Add(s * 0.24, 0.75, 0.05);
                TubeAlong(m, f, bronze, p, 0.055, 0.045, P(lod, 6, 3, 3));
                if (lod == 0) Shapes.Sphere(m, f * Affine3.Translation(s * 0.24, 0.72, 0.05), bronze, 0.045, 4, L0);
            }
            ShapeColor.VerticalShade(m, b0, m.VertexCount - b0, 1.0, 2.6, 0.9f, 1.08f);
        }
    }
}
