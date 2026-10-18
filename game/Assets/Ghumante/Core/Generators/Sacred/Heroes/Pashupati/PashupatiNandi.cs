using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Pashupati
{
    /// <summary>
    /// PT-02, the great gilt Nandi before the west door (pashupati-temple.md PT-02; refs c_westgate/c_26, nandi/ref_00,
    /// ref_02, ref_03, c_inner/c_00): a recumbent bull 3.6 m long, 1.9 m wide, about 2.3 m from the slab to the horn tips,
    /// on the door axis (b = 0) with its centre 15.5 m west of the temple centre, facing the west door, so the famous view
    /// through the West Gate is its rump. One smooth lofted body (the huge rounded rump with the haunch bulges, the high
    /// hump, the chest), the neck with its dewlap, a long head with a broad flat brow, white-grey eyes with dark irises,
    /// crescent horns curving up and in from scaled caps, ears straight out with round bosses, the collar strap down the
    /// neck with its round bells, the folded legs and hooves and the tail laid down the rump to its tuft; a gilt slab on a
    /// vermilion-smeared stone pedestal with heavy mouldings on a two-step grey base; the brass queue rail along its south
    /// side to the door.
    /// </summary>
    internal static class PashupatiNandi
    {
        /// <summary>Centre of the pedestal on the door axis (site a), and its top (the bull's slab).</summary>
        public const double CentreA = -15.5, SlabTopM = 1.75;

        private static readonly ShapeBrush Gold = PashupatiKit.Br(0xC9A23A, MaterialChannel.Gilt);
        private static readonly ShapeBrush GoldLight = PashupatiKit.Br(0xD7B048, MaterialChannel.Gilt);
        private static readonly ShapeBrush GoldDark = PashupatiKit.Br(0x9A7A2C, MaterialChannel.Gilt);
        private static readonly ShapeBrush EyeWhite = PashupatiKit.Br(0xD8D8D0, MaterialChannel.Paint);
        private static readonly ShapeBrush EyeIris = PashupatiKit.Br(0x2B2B2B, MaterialChannel.Paint);

        /// <summary>The pedestal's smooth vermilion-orange paint, darker where it is worn (c_westgate/c_26).</summary>
        private static readonly ShapeBrush PedestalRed = PashupatiKit.Br(0xC0502C, MaterialChannel.Paint);
        private static readonly ShapeBrush PedestalDark = PashupatiKit.Br(0x9A4A36, MaterialChannel.Paint);

        /// <summary>Body stations rump to chest: z, centre height, half width, top and bottom half heights.</summary>
        private static readonly double[] BodyStations =
        {
            -1.78, 0.62, 0.12, 0.12, 0.12,
            -1.72, 0.66, 0.52, 0.56, 0.52,
            -1.6, 0.72, 0.78, 0.8, 0.68,
            -1.38, 0.77, 0.92, 0.86, 0.76,
            -1.08, 0.78, 0.95, 0.86, 0.78,
            -0.7, 0.78, 0.9, 0.82, 0.78,
            -0.3, 0.8, 0.86, 0.84, 0.8,
            0.1, 0.82, 0.86, 0.92, 0.82,
            0.45, 0.86, 0.84, 1.0, 0.86,
            0.75, 0.84, 0.78, 0.92, 0.84,
            1.0, 0.8, 0.62, 0.78, 0.8,
            1.15, 0.78, 0.36, 0.52, 0.62,
            1.22, 0.78, 0.08, 0.1, 0.1,
        };

        /// <summary>Head stations poll to muzzle: fraction of the head axis, half width, half height.</summary>
        private static readonly double[] HeadStations =
        {
            0.0, 0.06, 0.06,
            0.07, 0.27, 0.24,
            0.2, 0.33, 0.28,
            0.45, 0.3, 0.27,
            0.7, 0.25, 0.23,
            0.88, 0.245, 0.21,
            0.97, 0.22, 0.17,
            1.0, 0.07, 0.06,
        };

        public static void Build(ref PashupatiFrame f, MeshData m)
        {
            int lod = f.Lod;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            // Nandi frame: z along +a (toward the door), x = −b, y up from the courtyard.
            Affine3 k = f.Facing(CentreA, 0, 0, 1, 0);
            Pedestal(m, k, lod);
            Affine3 b = SacredDraw.At(k, 0, SlabTopM, 0);
            if (lod >= 3)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.8, -0.3), Gold, 0.9, 0.85, 1.55, 4, false);
                SacredDraw.Bake(m, v0, i0, f.Level);
                return;
            }
            Body(m, b, lod);
            Head(m, b, lod);
            if (lod <= 1) QueueRail(ref f, m, lod);
            SacredDraw.Bake(m, v0, i0, f.Level);
            // Pedestal and bull: solid, never climbed (W2-O1: the Nandi is worshipped, not mounted).
            f.Box(CentreA - 2.8, CentreA + 2.8, -1.6, 1.6, 0, SlabTopM, GenColliderFlags.NoClimb, GenColliders.Stone);
            f.Box(CentreA - 1.8, CentreA + 1.9, -0.95, 0.95, SlabTopM, SlabTopM + 2.0, GenColliderFlags.NoClimb, GenColliders.Metal);
        }

        /// <summary>Two grey steps, the vermilion pedestal with its mouldings and the gilt slab.</summary>
        private static void Pedestal(MeshData m, in Affine3 k, int lod)
        {
            ShapeBrush step = PashupatiKit.Br(0x8A857B, MaterialChannel.Stone);
            if (lod >= 2)
            {
                PashupatiKit.Block(m, k, -1.45, 1.45, 0, 0.36, -2.65, 2.65, step);
                PashupatiKit.Block(m, k, -1.3, 1.3, 0.36, 1.55, -2.5, 2.5, PedestalRed);
                PashupatiKit.Block(m, k, -1.05, 1.05, 1.55, SlabTopM, -1.9, 1.9, Gold);
                return;
            }
            PashupatiKit.Band(m, k, 0, 0, 1.6, 2.8, new[] { 0.0, -0.1, 0.0, 0.18 }, step, true, step);
            PashupatiKit.Band(m, k, 0, 0, 1.45, 2.65, new[] { 0.0, 0.18, 0.0, 0.36 }, step, true, step);
            // The pedestal: base course, a recessed die, the heavy cap moulding; vermilion, darker where it is worn.
            Mould p = SacredDraw.M;
            p.Add(0.1, 0.36, PedestalDark).Add(0.1, 0.48).Add(0.04, 0.54, PedestalRed).Add(0, 0.6).Add(0, 1.18).Add(0.06, 1.24, PedestalDark).Add(0.06, 1.3)
             .Add(0.14, 1.4).Add(0.14, 1.55);
            SacredDraw.RectRing(m, k, 0, 0, 1.3, 2.5, p, true, PedestalDark);
            // The gilt slab with a rounded edge.
            Shapes.RoundedSlab(m, SacredDraw.At(k, 0, 1.55, 0), Gold, 2.1, 3.8, SlabTopM - 1.55, 0.12, 0.05, 2);
            if (lod == 0)
            {
                // Brass oil lamps on the upper step at the four corners and in the middle of the long sides.
                for (int q = 0; q < 6; q++)
                {
                    double x = (q % 2 == 0 ? 1 : -1) * 1.38, z = (q / 2 - 1) * 2.3;
                    PashupatiKit.LatheOf(m, SacredDraw.At(k, x, 0.36, z), PashupatiKit.Brass, new[] { 0.0, 0.0, 0.06, 0.0, 0.03, 0.05, 0.03, 0.12, 0.08, 0.16, 0.06, 0.2, 0.0, 0.2 },
                                         8, lod, 0b10);
                }
            }
        }

        /// <summary>Superellipse ring point: angle t, half sizes (rx; top and bottom half heights), exponent e (1 = ellipse,
        /// below 1 boxier).</summary>
        private static void RingPoint(double t, double rx, double ryT, double ryB, double e, out double x, out double y)
        {
            double c = Math.Cos(t), s = Math.Sin(t);
            x = rx * Math.Sign(c) * Math.Pow(Math.Abs(c), e);
            y = (s > 0 ? ryT : ryB) * Math.Sign(s) * Math.Pow(Math.Abs(s), e);
        }

        /// <summary>The body: one loft rump to chest with the haunches, the folded legs and hooves, the hump's crest and
        /// the tail.</summary>
        private static void Body(MeshData m, in Affine3 b, int lod)
        {
            int seg = lod == 0 ? 22 : lod == 1 ? 12 : 8;
            int ns = BodyStations.Length / 5;
            int first = m.VertexCount, fi = m.IndexCount;
            for (int i = 0; i < ns; i++)
            {
                double z = BodyStations[5 * i], cy = BodyStations[5 * i + 1], rx = BodyStations[5 * i + 2], ryT = BodyStations[5 * i + 3], ryB = BodyStations[5 * i + 4];
                for (int j = 0; j < seg; j++)
                {
                    double t = 2 * Math.PI * j / seg, x, y;
                    // The rump is round seen from behind and a little broader low down (c_westgate/c_26); the barrel boxier.
                    RingPoint(t, rx, ryT, ryB, z < -0.9 ? 0.92 : 0.8, out x, out y);
                    if (y < 0) x *= 1 + 0.08 * Math.Min(1, -y / Math.Max(0.1, ryB));
                    double nz = i == 0 ? -0.9 : i == ns - 1 ? 0.9 : 0;
                    SacredDraw.V(m, b, x, cy + y, z, x, y, nz, Gold);
                }
            }
            SacredDraw.Grid(m, first, ns, seg, true, null);
            Close(m, b, first, seg, 0, -1.79, BodyStations[1]);
            Close(m, b, first + (ns - 1) * seg, seg, 1, 1.23, BodyStations[5 * (ns - 1) + 1]);
            ShapeNoise.RecomputeNormals(m, first, m.VertexCount - first, fi, m.IndexCount - fi, true);
            if (lod >= 2) return;
            int limb = lod == 0 ? 8 : 5, small = lod == 0 ? 8 : 5;
            for (int s = -1; s <= 1; s += 2)
            {
                // The haunches: the folded hind legs bulging low on the rump.
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.6, 0.5, -1.12), Gold, 0.42, 0.5, 0.62, small, false);
                // Hind shank along the flank and its hoof; folded forelegs under the chest, hooves forward.
                Tube(m, b, Gold, 0.15, 0.1, limb, s * 0.86, 0.26, -1.15, s * 0.92, 0.18, -0.7, s * 0.86, 0.13, -0.3);
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.82, 0.11, -0.18), GoldDark, 0.11, 0.1, 0.14, small, false);
                Tube(m, b, Gold, 0.17, 0.12, limb, s * 0.5, 0.42, 0.75, s * 0.46, 0.2, 1.1, s * 0.4, 0.13, 1.32);
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.38, 0.11, 1.42), GoldDark, 0.11, 0.1, 0.13, small, false);
            }
            // The crest of the hump.
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 1.7, 0.48), Gold, 0.56, 0.36, 0.52, lod == 0 ? 10 : 6, false);
            // The tail from the top of the rump down its left side to the tuft.
            Path3 p = PashupatiKit.PathS;
            // The tail: from the top of the rump down its left half and hooked back under to the tuft (c_westgate/c_26).
            p.Add(-0.02, 1.46, -1.64).Add(-0.16, 1.18, -1.79).Add(-0.27, 0.84, -1.82).Add(-0.26, 0.52, -1.8).Add(-0.12, 0.34, -1.77).Add(0.06, 0.3, -1.74);
            Shapes.Tube(m, b, GoldLight, p, 0.07, lod == 0 ? 6 : 4, true, false, default, 0.055);
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0.14, 0.3, -1.72), GoldLight, 0.15, 0.12, 0.1, small, false);
        }

        /// <summary>Close a ring of the loft with a fan to an apex at (0, cy, z) (direction −1 or +1 along z).</summary>
        private static void Close(MeshData m, in Affine3 b, int ring, int seg, int dir, double z, double cy)
        {
            int c = SacredDraw.V(m, b, 0, cy, z, 0, 0, dir == 0 ? -1 : 1, Gold);
            for (int j = 0; j < seg; j++) SacredDraw.Tri(m, c, ring + j, ring + (j + 1) % seg);
        }

        /// <summary>The neck and dewlap, the head loft, eyes, nostrils, horns with their caps, ears with their bosses and
        /// the collar strap with its bells.</summary>
        private static void Head(MeshData m, in Affine3 b, int lod)
        {
            int small = lod == 0 ? 8 : 5;
            // Neck and dewlap.
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 1.28, 1.08), Gold, 0.44, 0.48, 0.46, lod == 0 ? 12 : 8, false);
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.82, 1.24), Gold, 0.17, 0.42, 0.3, small, false);
            // The head: a loft along the axis from the poll (0, 1.74, 1.24) down and forward to the muzzle.
            double p0y = 1.76, p0z = 1.22, p1y = 1.24, p1z = 2.16;
            double dy = p1y - p0y, dz = p1z - p0z, len = Math.Sqrt(dy * dy + dz * dz);
            dy /= len;
            dz /= len;
            // Head "up" (perpendicular to the axis in the vertical plane, toward the brow).
            double uy = -dz, uz = dy;
            if (uy < 0)
            {
                uy = -uy;
                uz = -uz;
            }
            int seg = lod == 0 ? 16 : lod == 1 ? 10 : 6;
            int ns = HeadStations.Length / 3, first = m.VertexCount, fi = m.IndexCount;
            for (int i = 0; i < ns; i++)
            {
                double s = HeadStations[3 * i] * len, rx = HeadStations[3 * i + 1], ry = HeadStations[3 * i + 2];
                double cy = p0y + dy * s, cz = p0z + dz * s;
                for (int j = 0; j < seg; j++)
                {
                    double t = 2 * Math.PI * j / seg, x, y;
                    // The brow is broad and flat (boxier on top), the jaw rounder.
                    RingPoint(t, rx, ry, 0.92 * ry, Math.Sin(t) > 0 ? 0.6 : 0.85, out x, out y);
                    double nn = i == 0 ? -1 : i == ns - 1 ? 1 : 0;
                    SacredDraw.V(m, b, x, cy + y * uy, cz + y * uz, x, y * uy + nn * dy, y * uz + nn * dz, Gold);
                }
            }
            SacredDraw.Grid(m, first, ns, seg, true, null);
            int c0 = SacredDraw.V(m, b, 0, p0y - 0.02 * dy, p0z - 0.02 * dz, 0, -dy, -dz, Gold);
            for (int j = 0; j < seg; j++) SacredDraw.Tri(m, c0, first + j, first + (j + 1) % seg);
            int last = first + (ns - 1) * seg;
            int c1 = SacredDraw.V(m, b, 0, p1y + 0.02 * dy, p1z + 0.02 * dz, 0, dy, dz, Gold);
            for (int j = 0; j < seg; j++) SacredDraw.Tri(m, c1, last + j, last + (j + 1) % seg);
            ShapeNoise.RecomputeNormals(m, first, m.VertexCount - first, fi, m.IndexCount - fi, true);
            if (lod >= 2)
            {
                for (int s = -1; s <= 1; s += 2) Horn(m, b, s, lod);
                return;
            }
            for (int s = -1; s <= 1; s += 2)
            {
                // Eyes (a third of the way down the head, on the sides): white-grey ball, dark iris, a gilt lid ring.
                double es = 0.42 * len, ey = p0y + dy * es + 0.06 * uy, ez = p0z + dz * es + 0.06 * uz;
                Shapes.Sphere(m, SacredDraw.At(b, s * 0.27, ey, ez), EyeWhite, 0.085, lod == 0 ? 10 : 6);
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.335, ey, ez + 0.01), EyeIris, 0.025, 0.05, 0.05, 6, false);
                if (lod == 0) Shapes.Torus(m, Affine3.RotationZ(0.5 * Math.PI).Then(SacredDraw.At(b, s * 0.29, ey, ez)), GoldLight, 0.09, 0.022, 10, 4);
                // Nostrils at the muzzle.
                double ns2 = 0.95 * len;
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.09, p0y + dy * ns2 + 0.03, p0z + dz * ns2 + 0.06), GoldDark, 0.05, 0.04, 0.04, 6, false);
                Horn(m, b, s, lod);
                // Ears straight out to the side behind the horns, each with a round scaled boss at its root.
                Affine3 ear = Affine3.RotationZ(s * -0.18).Then(SacredDraw.At(b, s * 0.55, 1.78, 1.2));
                Shapes.Ellipsoid(m, ear, GoldLight, 0.24, 0.06, 0.12, small, false);
                Shapes.Sphere(m, SacredDraw.At(b, s * 0.34, 1.8, 1.22), GoldDark, 0.1, small);
                // The collar strap down the side of the neck to the throat, with its round bells.
                Path3 p = PashupatiKit.PathS;
                p.Add(s * 0.36, 1.66, 1.02).Add(s * 0.44, 1.38, 1.06).Add(s * 0.42, 1.08, 1.16).Add(s * 0.28, 0.86, 1.3).Add(0, 0.76, 1.38);
                Shapes.Tube(m, b, GoldDark, p, 0.035, 4, false);
                if (lod == 0)
                {
                    // A row of round bells (ghanti) down the strap, each with its slit (c_inner/c_00).
                    for (int q = 0; q < 7; q++)
                    {
                        double t = (q + 0.5) / 7;
                        double bx, by, bz;
                        Bez(p, t, out bx, out by, out bz);
                        Shapes.Sphere(m, SacredDraw.At(b, bx * 1.08, by, bz + 0.04), GoldLight, 0.085, 8);
                        Shapes.Torus(m, Affine3.RotationZ(0.5 * Math.PI).Then(SacredDraw.At(b, bx * 1.08 + s * 0.02, by, bz + 0.04)), GoldDark, 0.07, 0.012, 8, 3);
                    }
                }
            }
        }

        /// <summary>A crescent horn from its scaled cap on the poll: out, up and curving in at the tip.</summary>
        private static void Horn(MeshData m, in Affine3 b, int s, int lod)
        {
            Path3 p = PashupatiKit.PathS;
            // Thick at the root, out and up, then curving forward and in (the short crescents seen over the rump from the
            // West Gate, the longer sweep of c_inner/c_00 close up).
            // c_inner/c_00: thick at the root, out and up in a long crescent (about 0.9 m along the curve), the tips turning in
            // toward each other above the poll.
            p.Add(s * 0.19, 1.86, 1.26).Add(s * 0.34, 1.96, 1.24).Add(s * 0.45, 2.12, 1.25).Add(s * 0.47, 2.32, 1.29).Add(s * 0.41, 2.5, 1.34)
             .Add(s * 0.29, 2.62, 1.39);
            Shapes.Tube(m, b, GoldLight, p, 0.13, lod == 0 ? 8 : lod == 1 ? 5 : 4, true, false, default, 0.022);
            if (lod == 0)
            {
                // The scaled (beaded) cap at the root.
                Shapes.Torus(m, Affine3.RotationZ(s * 0.9).Then(SacredDraw.At(b, s * 0.24, 1.9, 1.26)), GoldDark, 0.11, 0.045, 10, 5);
                Shapes.Torus(m, Affine3.RotationZ(s * 0.75).Then(SacredDraw.At(b, s * 0.29, 1.95, 1.26)), GoldDark, 0.1, 0.035, 10, 4);
            }
        }

        /// <summary>A point at fraction t along a path (by segment count).</summary>
        private static void Bez(Path3 p, double t, out double x, out double y, out double z)
        {
            double f = t * (p.Count - 1);
            int i = Math.Min(p.Count - 2, (int)Math.Floor(f));
            double u = f - i;
            x = p.X[i] + (p.X[i + 1] - p.X[i]) * u;
            y = p.Y[i] + (p.Y[i + 1] - p.Y[i]) * u;
            z = p.Z[i] + (p.Z[i + 1] - p.Z[i]) * u;
        }

        /// <summary>A tapering tube through three points.</summary>
        private static void Tube(MeshData m, in Affine3 b, in ShapeBrush br, double r0, double r1, int radial, double ax, double ay, double az, double bx, double by,
                                 double bz, double cx, double cy, double cz)
        {
            Path3 p = PashupatiKit.PathS;
            p.Add(ax, ay, az).Add(bx, by, bz).Add(cx, cy, cz);
            Shapes.Tube(m, b, br, p, r0, radial, true, false, default, r1);
        }

        /// <summary>The brass queue rail along the Nandi's south side from the gate passage to the west door's flight:
        /// posts every 1.6 m and two rails.</summary>
        private static void QueueRail(ref PashupatiFrame f, MeshData m, int lod)
        {
            const double B = -2.25, A0 = -32.0, A1 = -10.9;
            int posts = (int)Math.Round((A1 - A0) / 1.6);
            for (int i = 0; i <= posts; i++)
            {
                double a = A0 + (A1 - A0) * i / posts;
                Shapes.Cylinder(m, f.At(a, 0, B), PashupatiKit.Brass, 0.03, 1.0, lod == 0 ? 6 : 4, 0, 0, false, true);
            }
            for (int r = 0; r < 2; r++)
            {
                double y = r == 0 ? 0.5 : 0.98;
                Shapes.Bar(m, f.Site, PashupatiKit.Brass, A0, y, B, A1, y, B, 0.025, lod == 0 ? 6 : 4);
            }
            f.Wall(A0, B, A1, B, 0, 1.0, 0.04);
        }
    }
}
