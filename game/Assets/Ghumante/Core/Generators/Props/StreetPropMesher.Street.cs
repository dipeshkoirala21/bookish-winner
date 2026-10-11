using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Props
{
    public static partial class StreetPropMesher
    {
        /// <summary>Pictogram on a sign post.</summary>
        private enum Pictogram : byte
        {
            Bus = 0,
            Car = 1,
            Gate = 2,
        }

        // Palette (ref_props.md §4-8).
        private const uint PostWhite = 0xE8E8E4, SignBlue = 0x1F5FAF, SignWhite = 0xF4F4F0, SignYellow = 0xF2C230, SignBlack = 0x23252A;
        private const uint Walnut = 0x3A2416, WalnutLight = 0x5A3A24, Jhingati = 0xA8553A, JhingatiDark = 0x7E3C29, Valance = 0xC0282D, ValanceDark = 0x5A1416;
        private const uint PlinthStone = 0x9C968C, BenchWood = 0x8A5A32;
        private const uint SignalPole = 0x8E9497, SignalBody = 0x2A2D31, SignalRim = 0xE8E8E4, LensRed = 0xE23B2E, LensAmber = 0xF2B01E, LensGreen = 0x27B05A;
        private const uint CastIron = 0x2E4A3A, Plaster = 0xE7DCC4, PlinthRed = 0x8E3A2C, GateGreen = 0x2F5D46;
        private const uint Concrete = 0xB1ADA4, ConcreteDark = 0x8F8B83, GiPipe = 0x8C949C, Brass = 0xB8902F, Jerrycan = 0xF2C230, BrickPlaster = 0xC9B9A0;

        // ---------------------------------------------------------------------------------------------------------
        // Sign posts: bus stop (blue, bus pictogram), taxi stand (blue, car), aeroway gate (yellow, gate arrow) (§4).

        private static void SignPost(MeshData m, int lod, Pictogram pic)
        {
            const double Top = 2.62, SignY = 2.22, Size = 0.6;
            bool aero = pic == Pictogram.Gate;
            uint face = aero ? SignYellow : SignBlue, ink = aero ? SignBlack : SignWhite;
            Affine3 I = Affine3.Identity;
            int radial = P(lod, 8, 6, 4);
            ShapeBrush post = B(aero ? 0x9AA0A4u : PostWhite, aero ? MaterialChannel.Metal : MaterialChannel.Paint);
            if (lod < 2) Shapes.Cylinder(m, I, B(Footing, MaterialChannel.Concrete), 0.13, 0.1, radial, lod == 0 ? 0.02 : 0, 1, false, true, L0);
            Shapes.Cylinder(m, I, post, 0.032, Top, radial, 0, 0, false, false, L0);
            if (lod < 2) Shapes.Dome(m, Affine3.Translation(0, Top, 0), post, 0.034, 0.03, radial, false, L0);
            // The plate faces +Z, mounted on the front of the post.
            Affine3 plate = Affine3.Translation(0, SignY, 0.05);
            if (lod >= 2)
            {
                Box(m, plate, B(face, MaterialChannel.Paint), Size, Size, 0.03);
                return;
            }
            if (lod == 0) Shapes.RoundedBox(m, plate, B(ink, MaterialChannel.Paint), Size, Size, 0.024, 0.045, 1, L0);
            else Box(m, plate, B(ink, MaterialChannel.Paint), Size, Size, 0.024);
            Box(m, plate * Affine3.Translation(0, 0, 0.007), B(face, MaterialChannel.Paint), Size - 0.07, Size - 0.07, 0.016);
            // Pictogram, raised on the face.
            ShapeBrush ik = B(ink, MaterialChannel.Paint), fc = B(face, MaterialChannel.Paint);
            Affine3 g = plate * Affine3.Translation(0, 0, 0.017);
            switch (pic)
            {
                case Pictogram.Bus:
                    Box(m, g * Affine3.Translation(0, 0.03, 0), ik, 0.36, 0.24, 0.008);
                    if (lod == 0)
                    {
                        // Windscreen and two side windows in the face colour, a roof rib and two wheels.
                        Box(m, g * Affine3.Translation(-0.1, 0.075, 0.004), fc, 0.1, 0.07, 0.006);
                        Box(m, g * Affine3.Translation(0.04, 0.075, 0.004), fc, 0.1, 0.07, 0.006);
                        Box(m, g * Affine3.Translation(0.145, 0.06, 0.004), fc, 0.045, 0.1, 0.006);
                        for (int i = 0; i < 2; i++)
                            Shapes.Cylinder(m, g * Affine3.Translation(i == 0 ? -0.1 : 0.1, -0.095, 0) * Affine3.RotationX(90 * Deg), ik, 0.042, 0.01, 8, 0, 0, false, true, L0);
                    }
                    break;
                case Pictogram.Car:
                    Box(m, g * Affine3.Translation(0, -0.01, 0), ik, 0.38, 0.11, 0.008);
                    Box(m, g * Affine3.Translation(-0.02, 0.08, 0), ik, 0.22, 0.09, 0.008);
                    if (lod == 0)
                    {
                        Box(m, g * Affine3.Translation(-0.02, 0.082, 0.004), fc, 0.17, 0.05, 0.006);
                        Box(m, g * Affine3.Translation(-0.02, 0.15, 0), ik, 0.08, 0.035, 0.008); // roof sign
                        for (int i = 0; i < 2; i++)
                            Shapes.Cylinder(m, g * Affine3.Translation(i == 0 ? -0.11 : 0.11, -0.075, 0) * Affine3.RotationX(90 * Deg), ik, 0.04, 0.01, 8, 0, 0, false, true, L0);
                    }
                    break;
                default:
                    // An arrow and a blank band (gate number: no text).
                    Box(m, g * Affine3.Translation(0, 0.12, 0), ik, 0.4, 0.09, 0.008);
                    Box(m, g * Affine3.Translation(-0.04, -0.07, 0), ik, 0.22, 0.06, 0.008);
                    if (lod == 0)
                    {
                        Box(m, g * Affine3.Translation(0.1, -0.07, 0) * Affine3.RotationZ(45 * Deg), ik, 0.11, 0.06, 0.008);
                        Box(m, g * Affine3.Translation(0.1, -0.07, 0) * Affine3.RotationZ(-45 * Deg), ik, 0.11, 0.06, 0.008);
                    }
                    break;
            }
            // A small blank plate under the sign (white on blue signs, black on the yellow one).
            Box(m, Affine3.Translation(0, SignY - 0.43, 0.045), B(aero ? SignBlack : SignWhite, MaterialChannel.Paint), 0.5, 0.16, 0.02);
            if (lod == 0)
            {
                Box(m, Affine3.Translation(0, SignY - 0.43, 0.056), B(face, MaterialChannel.Paint), 0.36, 0.035, 0.004);
                // Clamp bands holding the plates.
                for (int i = 0; i < 3; i++)
                    Shapes.Cylinder(m, Affine3.Translation(0, (i == 0 ? SignY - 0.2 : i == 1 ? SignY + 0.2 : SignY - 0.43) - 0.02, 0), B(GalvanisedDark, MaterialChannel.Metal),
                                    0.04, 0.04, 6, 0, 0, false, false, L0);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Falcha bus shelter (KMC, 2024-26): stone plinth, two rows of five carved dark posts, beams and lattice
        // frieze, a hipped jhingati roof with flared eaves and a red frilled valance, benches at the back (§5).

        private static void Falcha(MeshData m, int lod)
        {
            const double Wx = 6.0, Dz = 3.6, Plinth = 0.25, Post = 2.3, BeamH = 0.22, EaveY = 2.72, Over = 0.6, RidgeY = 4.05;
            Affine3 I = Affine3.Identity;
            ShapeBrush wood = B(Walnut, MaterialChannel.WoodCarved), stone = B(PlinthStone, MaterialChannel.Stone);
            double px = 0.5 * Wx - 0.3, pz = 0.5 * Dz - 0.3; // post rows
            BoxSpan(m, I, stone, -0.5 * Wx, 0.5 * Wx, 0, Plinth, -0.5 * Dz, 0.5 * Dz);
            if (lod == 0) BoxSpan(m, I, stone, -1.4, 1.4, 0, 0.12, 0.5 * Dz, 0.5 * Dz + 0.35); // front step
            double y0 = Plinth, yTop = Plinth + Post;
            int cols = lod >= 2 ? 2 : 5;
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < cols; c++)
                {
                    double x = cols == 1 ? 0 : -px + 2 * px * c / (cols - 1), z = r == 0 ? -pz : pz;
                    Affine3 f = Affine3.Translation(x, y0, z);
                    if (lod == 0)
                    {
                        // A square Newar post: the carved shaft, then a flared capital block under the beam.
                        Prism(m, f, wood, 0.2, 0.2, 0.16, 0.16, Post - 0.3, 0);
                        Prism(m, f * Affine3.Translation(0, Post - 0.3, 0), B(WalnutLight, MaterialChannel.WoodCarved), 0.17, 0.17, 0.27, 0.27, 0.3, 0);
                    }
                    else Prism(m, f, wood, 0.2, 0.2, 0.17, 0.17, Post, 0);
                }
            // Beams on the posts (both long sides and the two ends) and the lattice frieze up to the eaves.
            double by0 = yTop, by1 = yTop + BeamH;
            if (lod < 2)
            {
                BoxSpan(m, I, wood, -px - 0.18, px + 0.18, by0, by1, -pz - 0.13, -pz + 0.13);
                BoxSpan(m, I, wood, -px - 0.18, px + 0.18, by0, by1, pz - 0.13, pz + 0.13);
            }
            if (lod == 0)
            {
                BoxSpan(m, I, wood, -px - 0.13, -px + 0.13, by0, by1, -pz + 0.13, pz - 0.13);
                BoxSpan(m, I, wood, px - 0.13, px + 0.13, by0, by1, -pz + 0.13, pz - 0.13);
            }
            ShapeBrush lattice = B(WalnutLight, MaterialChannel.WoodCarved);
            double fy0 = by1, fy1 = EaveY - 0.02, fx = px + 0.1, fz = pz + 0.1;
            if (lod == 0)
            {
                Quad(m, I, lattice, -fx, fy0, fz, fx, fy0, fz, fx, fy1, fz, -fx, fy1, fz, 0, 0, 1);
                Quad(m, I, lattice, -fx, fy0, -fz, fx, fy0, -fz, fx, fy1, -fz, -fx, fy1, -fz, 0, 0, -1);
                Quad(m, I, lattice, fx, fy0, -fz, fx, fy0, fz, fx, fy1, fz, fx, fy1, -fz, 1, 0, 0);
                Quad(m, I, lattice, -fx, fy0, -fz, -fx, fy0, fz, -fx, fy1, fz, -fx, fy1, -fz, -1, 0, 0);
            }
            // Hipped roof: eave rectangle (with a little upturn at the corners), a kink line for the flare, the ridge.
            ShapeBrush tile = B(Jhingati, MaterialChannel.RoofTile);
            double ex = 0.5 * Wx + Over - 0.3, ez = 0.5 * Dz + Over - 0.3; // eave half extents beyond the post lines
            ex = px + Over;
            ez = pz + Over;
            double kx = ex - 0.55, kz = ez - 0.55, ky = EaveY + 0.28; // flare kink
            double rx = ex - (RidgeY - EaveY) * 0.9 * ez / (RidgeY - EaveY) * 0.98; // ridge half length
            rx = Math.Max(0.6, ex - ez);
            int r0 = m.VertexCount, ri0 = m.IndexCount;
            if (lod < 2)
            {
                RoofBand(m, tile, ex, ez, EaveY, kx, kz, ky);
                RoofTop(m, tile, kx, kz, ky, rx, RidgeY);
            }
            else RoofTop(m, tile, ex, ez, EaveY, rx, RidgeY);
            ShapeColor.VerticalShade(m, r0, m.VertexCount - r0, EaveY, RidgeY, 1.05f, 0.92f);
            if (lod == 0) ShapeColor.JitterFaces(m, ri0, m.IndexCount - ri0, 0.05f, 0x46414C48u, 2);
            // Ridge cap and the gilt finials (gajur) at its ends.
            if (lod < 2)
            {
                Shapes.Bar(m, I, B(JhingatiDark, MaterialChannel.RoofTile), -rx - 0.05, RidgeY + 0.02, 0, rx + 0.05, RidgeY + 0.02, 0, 0.07, P(lod, 6, 4, 4), L0);
                if (lod == 0)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Affine3 gf = Affine3.Translation(s * (rx + 0.02), RidgeY + 0.06, 0);
                        Shapes.Cone(m, gf, B(Gold, MaterialChannel.Gilt), 0.06, 0.3, 5, 0, 0, false, L0);
                    }
            }
            // Ceiling under the roof (seen from the street), and the eave thickness band.
            ShapeBrush soffit = B(WalnutLight, MaterialChannel.Wood);
            Quad(m, I, soffit, -ex, EaveY - 0.04, -ez, ex, EaveY - 0.04, -ez, ex, EaveY - 0.04, ez, -ex, EaveY - 0.04, ez, 0, -1, 0);
            if (lod == 0)
            {
                ShapeBrush band = B(Walnut, MaterialChannel.Wood);
                Quad(m, I, band, -ex, EaveY - 0.04, ez, ex, EaveY - 0.04, ez, ex, EaveY, ez, -ex, EaveY, ez, 0, 0, 1);
                Quad(m, I, band, -ex, EaveY - 0.04, -ez, ex, EaveY - 0.04, -ez, ex, EaveY, -ez, -ex, EaveY, -ez, 0, 0, -1);
                Quad(m, I, band, ex, EaveY - 0.04, -ez, ex, EaveY - 0.04, ez, ex, EaveY, ez, ex, EaveY, -ez, 1, 0, 0);
                Quad(m, I, band, -ex, EaveY - 0.04, -ez, -ex, EaveY - 0.04, ez, -ex, EaveY, ez, -ex, EaveY, -ez, -1, 0, 0);
            }
            // Red frilled valance round the eaves.
            int frills = P(lod, 5, 0, 0);
            ValanceSide(m, -ex, ez + 0.01, ex, ez + 0.01, EaveY - 0.04, 2 * frills, lod);
            ValanceSide(m, ex, -ez - 0.01, -ex, -ez - 0.01, EaveY - 0.04, 2 * frills, lod);
            ValanceSide(m, ex + 0.01, ez, ex + 0.01, -ez, EaveY - 0.04, frills + 1, lod);
            ValanceSide(m, -ex - 0.01, -ez, -ex - 0.01, ez, EaveY - 0.04, frills + 1, lod);
            // Benches between the back posts and at the two ends.
            if (lod < 2)
            {
                ShapeBrush seat = B(BenchWood, MaterialChannel.Wood);
                BoxSpan(m, I, seat, -px + 0.15, px - 0.15, y0 + 0.4, y0 + 0.46, -pz + 0.05, -pz + 0.45);
                if (lod > 0) BoxSpan(m, I, B(Walnut, MaterialChannel.Wood), -px + 0.25, px - 0.25, y0, y0 + 0.4, -pz + 0.12, -pz + 0.38);
                else
                    for (int i = 0; i < 2; i++)
                    {
                        double bx = i == 0 ? -px + 1.0 : px - 1.0;
                        BoxSpan(m, I, B(Walnut, MaterialChannel.Wood), bx - 0.05, bx + 0.05, y0, y0 + 0.4, -pz + 0.12, -pz + 0.38);
                    }
                if (lod == 0)
                {
                    BoxSpan(m, I, seat, -px + 0.05, -px + 0.45, y0 + 0.4, y0 + 0.46, -pz + 0.5, pz - 0.5);
                    BoxSpan(m, I, seat, px - 0.45, px - 0.05, y0 + 0.4, y0 + 0.46, -pz + 0.5, pz - 0.5);
                }
            }
        }

        /// <summary>The flared lower band of the hipped roof: from the eave rectangle (ex, ez at y0) up to the kink
        /// rectangle (kx, kz at y1), four trapezoids with their own slope normals.</summary>
        private static void RoofBand(MeshData m, in ShapeBrush b, double ex, double ez, double y0, double kx, double kz, double y1)
        {
            Affine3 I = Affine3.Identity;
            double dzn = ez - kz, dyn = y1 - y0, l = Math.Sqrt(dzn * dzn + dyn * dyn);
            double ny = dzn / l, nh = dyn / l;
            double dxn = ex - kx, lx = Math.Sqrt(dxn * dxn + dyn * dyn), nyx = dxn / lx, nhx = dyn / lx;
            Quad(m, I, b, -ex, y0, ez, ex, y0, ez, kx, y1, kz, -kx, y1, kz, 0, ny, nh);
            Quad(m, I, b, ex, y0, -ez, -ex, y0, -ez, -kx, y1, -kz, kx, y1, -kz, 0, ny, -nh);
            Quad(m, I, b, ex, y0, ez, ex, y0, -ez, kx, y1, -kz, kx, y1, kz, nhx, nyx, 0);
            Quad(m, I, b, -ex, y0, -ez, -ex, y0, ez, -kx, y1, kz, -kx, y1, -kz, -nhx, nyx, 0);
        }

        /// <summary>The upper hipped roof from a rectangle (kx, kz at y0) to a ridge of half length rx at y1: two
        /// trapezoids and two triangles.</summary>
        private static void RoofTop(MeshData m, in ShapeBrush b, double kx, double kz, double y0, double rx, double y1)
        {
            Affine3 I = Affine3.Identity;
            double dy = y1 - y0, l = Math.Sqrt(kz * kz + dy * dy), ny = kz / l, nh = dy / l;
            double dx = kx - rx, lx = Math.Sqrt(dx * dx + dy * dy), nyx = dx / lx, nhx = dy / lx;
            Quad(m, I, b, -kx, y0, kz, kx, y0, kz, rx, y1, 0, -rx, y1, 0, 0, ny, nh);
            Quad(m, I, b, kx, y0, -kz, -kx, y0, -kz, -rx, y1, 0, rx, y1, 0, 0, ny, -nh);
            int v = m.VertexCount;
            ShapeEmit.Vertex(m, I, b, kx, y0, kz, nhx, nyx, 0);
            ShapeEmit.Vertex(m, I, b, kx, y0, -kz, nhx, nyx, 0);
            ShapeEmit.Vertex(m, I, b, rx, y1, 0, nhx, nyx, 0);
            ShapeEmit.TriOriented(m, v, v + 1, v + 2);
            v = m.VertexCount;
            ShapeEmit.Vertex(m, I, b, -kx, y0, -kz, -nhx, nyx, 0);
            ShapeEmit.Vertex(m, I, b, -kx, y0, kz, -nhx, nyx, 0);
            ShapeEmit.Vertex(m, I, b, -rx, y1, 0, -nhx, nyx, 0);
            ShapeEmit.TriOriented(m, v, v + 1, v + 2);
        }

        /// <summary>One side of the frilled valance hanging from (ax, az)-(bx, bz) at height y: a red band with
        /// <paramref name="n"/> scallops, darker at the hem; its outward side is to the right of travel a→b.</summary>
        private static void ValanceSide(MeshData m, double ax, double az, double bx, double bz, double y, int n, int lod)
        {
            double dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
            double nx = -dz / l, nz = dx / l; // left of travel
            // Outward = away from the shelter centre.
            double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
            if (nx * mx + nz * mz < 0)
            {
                nx = -nx;
                nz = -nz;
            }
            ShapeBrush red = B(Valance, MaterialChannel.Fabric), hem = B(ValanceDark, MaterialChannel.Fabric);
            const double Band = 0.16, Scallop = 0.12;
            Affine3 I = Affine3.Identity;
            if (lod >= 1 || n <= 0)
            {
                Quad(m, I, red, ax, y - Band - (lod >= 1 ? 0.06 : 0), az, bx, y - Band - (lod >= 1 ? 0.06 : 0), bz, bx, y, bz, ax, y, az, nx, 0, nz);
                return;
            }
            // Band (one quad), then scallops: each a triangle hanging from the band, its tip at the hem colour.
            Quad(m, I, red, ax, y - Band, az, bx, y - Band, bz, bx, y, bz, ax, y, az, nx, 0, nz);
            for (int i = 0; i < n; i++)
            {
                double t0 = (double)i / n, t1 = (double)(i + 1) / n, tm = 0.5 * (t0 + t1);
                int v = m.VertexCount;
                ShapeEmit.Vertex(m, I, red, ax + dx * t0, y - Band, az + dz * t0, nx, 0, nz);
                ShapeEmit.Vertex(m, I, red, ax + dx * t1, y - Band, az + dz * t1, nx, 0, nz);
                ShapeEmit.Vertex(m, I, hem, ax + dx * tm + nx * 0.015, y - Band - Scallop, az + dz * tm + nz * 0.015, nx, -0.2, nz);
                ShapeEmit.TriOriented(m, v, v + 1, v + 2);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Traffic signal: grey pole, hooded three-aspect head on a white-rimmed back plate facing +Z, push button and a
        // small pedestrian head (§6).

        private static void Signal(MeshData m, int lod)
        {
            const double HeadY = 3.95, PoleTop = 4.42;
            Affine3 I = Affine3.Identity;
            int radial = P(lod, 8, 6, 4);
            ShapeBrush pole = B(SignalPole, MaterialChannel.Metal), body = B(SignalBody, MaterialChannel.Metal);
            if (lod < 2) Shapes.Cylinder(m, I, B(Footing, MaterialChannel.Concrete), 0.17, 0.12, radial, 0, 0, false, true, L0);
            Shapes.Cylinder(m, I, pole, 0.062, PoleTop, radial, 0, 0, false, true, L0);
            Affine3 head = Affine3.Translation(0, HeadY, 0.19);
            if (lod >= 2)
            {
                Box(m, head, body, 0.34, 1.0, 0.24);
                Box(m, head * Affine3.Translation(0, 0, 0.125), B(LensAmber, MaterialChannel.Glass), 0.18, 0.75, 0.01);
                return;
            }
            // Back plate with a white rim, the housing in front of it.
            Box(m, head * Affine3.Translation(0, 0, -0.13), B(SignalRim, MaterialChannel.Paint), 0.54, 1.22, 0.02);
            Box(m, head * Affine3.Translation(0, 0, -0.118), B(SignalBody, MaterialChannel.Paint), 0.46, 1.14, 0.012);
            if (lod == 0) Shapes.Superellipsoid(m, head, body, 0.17, 0.5, 0.12, 0.18, 0.35, 8, L0);
            else Box(m, head, body, 0.34, 1.0, 0.24);
            Shapes.Bar(m, I, pole, 0, HeadY + 0.3, 0, 0, HeadY + 0.3, 0.1, 0.035, 3, L0);
            Shapes.Bar(m, I, pole, 0, HeadY - 0.3, 0, 0, HeadY - 0.3, 0.1, 0.035, 3, L0);
            for (int i = 0; i < 3; i++)
            {
                double y = 0.31 - 0.31 * i;
                Affine3 lf = head * Affine3.Translation(0, y, 0.105) * Affine3.RotationX(90 * Deg);
                Shapes.Lathe(m, lf, B(SignalLenses[i], MaterialChannel.Glass), Prof.Clear(false).Add(0.095, 0, true).Add(0.07, 0.022).Add(0, 0.03, true), P(lod, 8, 6, 4), L0);
                if (lod == 0)
                {
                    // Visor: a half tube shading the lens from above, open below.
                    Shapes.Lathe(m, lf * Affine3.Translation(0, 0.02, 0), body, Prof.Clear(false).Add(0.115, 0, true).Add(0.115, 0.17, true), 6, L0, -95, 190);
                    Shapes.Lathe(m, lf * Affine3.Translation(0, 0.02, 0), body, Prof.Clear(false).Add(0.105, 0.17, true).Add(0.105, 0, true), 6, L0, -95, 190);
                }
            }
            if (lod == 0)
            {
                // Pedestrian push button and a small two-lens pedestrian head facing the crossing (+X).
                Box(m, Affine3.Translation(0, 1.12, 0.1), B(SignYellow, MaterialChannel.Paint), 0.13, 0.2, 0.08);
                Shapes.Cylinder(m, Affine3.Translation(0, 1.16, 0.14) * Affine3.RotationX(90 * Deg), B(SignBlack, MaterialChannel.Rubber), 0.025, 0.012, 6, 0, 0, false, true, L0);
                Affine3 ped = Affine3.Translation(0.16, 2.65, 0);
                Box(m, ped, body, 0.2, 0.42, 0.2);
                Shapes.Bar(m, I, pole, 0.05, 2.65, 0, 0.08, 2.65, 0, 0.03, 4, L0);
                for (int i = 0; i < 2; i++)
                    Shapes.Cylinder(m, ped * Affine3.Translation(0.1, i == 0 ? 0.1 : -0.1, 0) * Affine3.RotationZ(-90 * Deg), B(i == 0 ? LensRed : LensGreen, MaterialChannel.Glass),
                                    0.07, 0.015, 6, 0, 0, false, true, L0);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Bench: cast-iron end frames with scrolled arms, four seat slats and three back slats, facing +Z (§8).

        private static void Bench(MeshData m, int lod)
        {
            const double Len = 1.6, SeatY = 0.44, Depth = 0.46;
            Affine3 I = Affine3.Identity;
            ShapeBrush iron = B(CastIron, MaterialChannel.Metal), wood = B(BenchWood, MaterialChannel.Wood);
            double hx = 0.5 * Len - 0.08;
            if (lod >= 2)
            {
                BoxSpan(m, I, wood, -0.5 * Len, 0.5 * Len, SeatY - 0.05, SeatY, -0.5 * Depth, 0.5 * Depth - 0.04);
                Box(m, Affine3.Translation(0, 0.66, -0.24) * Affine3.RotationX(-12 * Deg), wood, Len, 0.3, 0.04);
                BoxSpan(m, I, iron, -hx - 0.03, -hx + 0.03, 0, SeatY - 0.05, -0.2, 0.2);
                BoxSpan(m, I, iron, hx - 0.03, hx + 0.03, 0, SeatY - 0.05, -0.2, 0.2);
                return;
            }
            // End frames: a side profile (in YZ) extruded 5 cm along X: front leg, seat rail, scrolled arm, back post.
            for (int s = -1; s <= 1; s += 2)
            {
                double x = s * hx;
                Path3 ctrl = PathB.Clear();
                // Back leg up into the back post (leaning back), as one bent bar.
                if (lod > 0)
                {
                    ctrl.Add(x, 0.0, -0.22).Add(x, SeatY - 0.06, -0.18).Add(x, 0.86, -0.3);
                    TubeAlong(m, I, iron, ctrl, 0.025, 0.022, 3);
                    Shapes.Bar(m, I, iron, x, 0.0, 0.25, x, SeatY - 0.06, 0.2, 0.025, 3, L0);
                    ctrl.Clear();
                    ctrl.Add(x, 0.6, -0.24).Add(x, 0.65, 0.05).Add(x, 0.53, 0.2);
                    TubeAlong(m, I, iron, ctrl, 0.022, 0.02, 3);
                    BoxSpan(m, I, iron, x - 0.025, x + 0.025, SeatY - 0.08, SeatY - 0.04, -0.21, 0.22);
                    continue;
                }
                ctrl.Add(x, 0.0, -0.22).Add(x, SeatY - 0.06, -0.18).Add(x, 0.62, -0.25).Add(x, 0.86, -0.3);
                TubeAlong(m, I, iron, ctrl, 0.025, 0.022, 4);
                // Front leg curving forward at the foot.
                ctrl.Clear();
                ctrl.Add(x, 0.0, 0.26).Add(x, 0.12, 0.21).Add(x, SeatY - 0.06, 0.2);
                TubeAlong(m, I, iron, ctrl, 0.025, 0.022, 4);
                // Arm: from the back post forward over the seat, ending in a scroll.
                ctrl.Clear();
                ctrl.Add(x, 0.6, -0.24).Add(x, 0.655, -0.08).Add(x, 0.65, 0.1).Add(x, 0.61, 0.21).Add(x, 0.55, 0.23).Add(x, 0.52, 0.18);
                TubeAlong(m, I, iron, ctrl, 0.022, 0.02, 4);
                if (lod == 0) Shapes.Bar(m, I, iron, x, 0.52, 0.19, x, SeatY - 0.04, 0.2, 0.02, 3, L0); // arm support
                // Seat rail.
                BoxSpan(m, I, iron, x - 0.025, x + 0.025, SeatY - 0.08, SeatY - 0.04, -0.21, 0.22);
            }
            // Slats.
            int seat = P(lod, 4, 3, 2), back = P(lod, 3, 2, 1);
            for (int i = 0; i < seat; i++)
            {
                double z = -0.17 + 0.37 * i / Math.Max(1, seat - 1);
                Slat(m, Affine3.Translation(0, SeatY - 0.015, z), wood, Len, 0.085, 0.035, lod);
            }
            for (int i = 0; i < back; i++)
            {
                double t = (double)i / Math.Max(1, back - 1), y = 0.56 + 0.22 * t;
                Slat(m, Affine3.Translation(0, y, -0.225 - 0.075 * t) * Affine3.RotationX(-14 * Deg), wood, Len, 0.03, 0.08, lod);
            }
        }

        /// <summary>A slat along X centred on the frame.</summary>
        private static void Slat(MeshData m, in Affine3 f, in ShapeBrush b, double len, double depth, double thick, int lod)
        {
            Box(m, f, b, len, thick, depth);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Compound gate: two plastered pillars with tiled caps, steel grille leaves opened back along the road; the
        // opening (5.3 m) is wider than a road corridor and nothing spans it (§8).

        private static void CompoundGate(MeshData m, int lod)
        {
            const double Gap = 5.3, Pw = 0.55, Ph = 2.7, LeafW = 2.3, LeafH = 1.85;
            Affine3 I = Affine3.Identity;
            ShapeBrush plaster = B(Plaster, MaterialChannel.Plaster), band = B(PlinthRed, MaterialChannel.Paint);
            ShapeBrush steel = B(GateGreen, MaterialChannel.Paint), tile = B(Jhingati, MaterialChannel.RoofTile);
            for (int s = -1; s <= 1; s += 2)
            {
                double cx = s * (0.5 * Gap + 0.5 * Pw);
                Affine3 f = Affine3.Translation(cx, 0, 0);
                if (lod >= 2)
                {
                    Prism(m, f, plaster, Pw, Pw, Pw, Pw, Ph, 0, true);
                    Shapes.Cone(m, f * Affine3.Translation(0, Ph, 0), tile, 0.45, 0.4, 4, 0, 0, false, L0);
                    BoxSpan(m, I, steel, s * 0.5 * Gap - s * 0.03, s * 0.5 * Gap + s * 0.03, 0.05, LeafH, 0.1, 0.1 + LeafW);
                    continue;
                }
                Prism(m, f, band, Pw + 0.06, Pw + 0.06, Pw + 0.06, Pw + 0.06, 0.4, 0);
                Prism(m, f * Affine3.Translation(0, 0.4, 0), plaster, Pw, Pw, Pw, Pw, Ph - 0.6, lod == 0 ? 0.12 : 0);
                // Cornice: two steps out, then a small hipped tile cap and a globe lamp.
                Prism(m, f * Affine3.Translation(0, Ph - 0.2, 0), plaster, Pw + 0.08, Pw + 0.08, Pw + 0.12, Pw + 0.12, 0.12, 0, true);
                if (lod == 0) Prism(m, f * Affine3.Translation(0, Ph - 0.08, 0), band, Pw + 0.12, Pw + 0.12, Pw + 0.12, Pw + 0.12, 0.06, 0);
                Shapes.Frustum(m, f * Affine3.Translation(0, Ph - 0.02, 0) * Affine3.Yaw(45), tile, 0.5, 0.06, 0.32, 4, 0, 0, true, true, L0);
                if (lod == 0)
                {
                    Shapes.Cylinder(m, f * Affine3.Translation(0, Ph + 0.28, 0), B(IronBlack, MaterialChannel.Metal), 0.03, 0.12, 6, 0, 0, false, false, L0);
                    Shapes.Sphere(m, f * Affine3.Translation(0, Ph + 0.5, 0), B(LanternGlass, MaterialChannel.Glass), 0.12, 6, L0);
                }
                // Leaf hinged at the pillar's inner face, swung open along +Z (in the road's direction, outside the
                // corridor): frame and vertical bars.
                double lx = s * (0.5 * Gap - 0.04), z0 = 0.18, z1 = z0 + LeafW;
                if (lod == 1)
                {
                    BoxSpan(m, I, steel, lx - 0.025, lx + 0.025, 0.08, LeafH, z0, z1);
                    continue;
                }
                BoxSpan(m, I, steel, lx - 0.025, lx + 0.025, 0.08, 0.14, z0, z1);
                BoxSpan(m, I, steel, lx - 0.025, lx + 0.025, LeafH - 0.06, LeafH, z0, z1);
                BoxSpan(m, I, steel, lx - 0.03, lx + 0.03, 0.08, LeafH, z0, z0 + 0.06);
                BoxSpan(m, I, steel, lx - 0.03, lx + 0.03, 0.08, LeafH, z1 - 0.06, z1);
                int bars = P(lod, 5, 3, 0);
                for (int k = 1; k <= bars; k++)
                {
                    double z = z0 + (z1 - z0) * k / (bars + 1);
                    if (lod == 0) Prism(m, Affine3.Translation(lx, 0.14, z), steel, 0.025, 0.025, 0.025, 0.025, LeafH - 0.2, 0);
                    else BoxSpan(m, I, steel, lx - 0.012, lx + 0.012, 0.14, LeafH - 0.06, z - 0.012, z + 0.012);
                }
                if (lod == 0) BoxSpan(m, I, steel, lx - 0.02, lx + 0.02, 0.95, 1.0, z0, z1); // mid rail
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Community water tap: concrete apron, small plastered pedestal, GI pipe gooseneck with a brass tap, a
        // jerrycan waiting (§8).

        private static void Tap(MeshData m, int lod)
        {
            Affine3 I = Affine3.Identity;
            ShapeBrush conc = B(Concrete, MaterialChannel.Concrete), pipe = B(GiPipe, MaterialChannel.Metal);
            BoxSpan(m, I, conc, -0.5, 0.5, 0, 0.1, -0.35, 0.45);
            if (lod < 2)
            {
                // Drain lip along the front and a wet patch.
                BoxSpan(m, I, B(ConcreteDark, MaterialChannel.Concrete), -0.5, 0.5, 0.1, 0.14, 0.4, 0.45);
                if (lod == 0) Shapes.Cylinder(m, Affine3.Translation(0.05, 0.1, 0.18), B(0x55606A, MaterialChannel.Water), 0.2, 0.004, 10, 0, 0, false, true, L0);
            }
            BoxSpan(m, I, B(BrickPlaster, MaterialChannel.Plaster), -0.16, 0.16, 0.1, 0.55, -0.32, -0.04);
            if (lod >= 2)
            {
                Shapes.Bar(m, I, pipe, 0, 0.55, -0.18, 0, 0.98, -0.18, 0.03, 3, L0);
                Shapes.Bar(m, I, pipe, 0, 0.98, -0.18, 0, 0.92, 0.12, 0.03, 3, L0);
                return;
            }
            Path3 ctrl = PathB.Clear();
            ctrl.Add(0, 0.5, -0.18).Add(0, 0.9, -0.18).Add(0, 0.99, -0.12).Add(0, 0.99, 0.02).Add(0, 0.93, 0.1);
            TubeAlong(m, I, pipe, lod == 0 ? Curves.CatmullRom(ctrl, PathA.Clear(), 2) : ctrl, 0.027, 0.025, P(lod, 6, 4, 3));
            // Brass tap body, handle and spout.
            Affine3 tap = Affine3.Translation(0, 0.88, 0.13);
            Shapes.Capsule(m, tap * Affine3.Translation(0, -0.06, 0), B(Brass, MaterialChannel.Metal), 0.035, 0.12, P(lod, 6, 4, 3), L0);
            Shapes.Bar(m, I, B(Brass, MaterialChannel.Metal), -0.07, 0.93, 0.13, 0.07, 0.93, 0.13, 0.012, 4, L0);
            Shapes.Cylinder(m, tap * Affine3.Translation(0, -0.12, 0.0), B(Brass, MaterialChannel.Metal), 0.016, 0.05, 6, 0, 0, false, true, L0);
            if (lod == 0)
            {
                // Yellow jerrycan under the tap.
                Shapes.RoundedBox(m, Affine3.Translation(0.02, 0.27, 0.16) * Affine3.Yaw(12), B(Jerrycan, MaterialChannel.Plain), 0.26, 0.34, 0.17, 0.035, 1, L0);
                Shapes.Cylinder(m, Affine3.Translation(0.09, 0.44, 0.16), B(0x2F5DA8, MaterialChannel.Plain), 0.03, 0.04, 6, 0, 0, false, true, L0);
                Shapes.Bar(m, I, B(Jerrycan, MaterialChannel.Plain), -0.06, 0.44, 0.16, 0.04, 0.44, 0.16, 0.015, 4, L0);
            }
        }
    }
}
