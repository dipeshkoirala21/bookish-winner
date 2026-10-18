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

        /// <summary>Polished brassy bronze (c_inner/c_00, j_nandi/ref_01): smooth, specular, no hammered texture, so the
        /// Metal channel; the body shades from the shadow tone low down to the highlight on the back.</summary>
        private static readonly ShapeBrush Gold = PashupatiKit.Br(0xB8923C, MaterialChannel.Metal);
        private static readonly ShapeBrush GoldLight = PashupatiKit.Br(0xD9B860, MaterialChannel.Metal);
        private static readonly ShapeBrush GoldDark = PashupatiKit.Br(0x7A5E26, MaterialChannel.Metal);
        private static readonly ShapeBrush EyeWhite = PashupatiKit.Br(0xD8D8D0, MaterialChannel.Paint);
        private static readonly ShapeBrush EyeIris = PashupatiKit.Br(0x2B2B2B, MaterialChannel.Paint);

        /// <summary>The pedestal's smooth vermilion-orange paint, darker where it is worn (c_westgate/c_26).</summary>
        private static readonly ShapeBrush PedestalRed = PashupatiKit.Br(0xC0502C, MaterialChannel.Paint);
        private static readonly ShapeBrush PedestalDark = PashupatiKit.Br(0x9A4A36, MaterialChannel.Paint);

        /// <summary>
        /// Body stations rump to chest (c_westgate/c_26, nandi/ref_00, ref_01): one smooth recumbent mass about 3.1 m long,
        /// the round rump as wide as it is tall (1.9 m by 1.7 m), the long back rising to the hump over the shoulders (2.0 m
        /// above the slab), the deep chest; the folded legs flatten its underside onto the slab. Per station: z, centre
        /// height, half width, top and bottom half heights.
        /// </summary>
        private static readonly double[] BodyStations =
        {
            -1.66, 0.9, 0.22, 0.26, 0.26,
            -1.6, 0.9, 0.54, 0.64, 0.62,
            -1.48, 0.92, 0.76, 0.86, 0.86,
            -1.28, 0.93, 0.9, 0.94, 0.91,
            -1.0, 0.93, 0.94, 0.94, 0.92,
            -0.65, 0.92, 0.9, 0.9, 0.92,
            -0.3, 0.92, 0.86, 0.9, 0.91,
            0.05, 0.94, 0.86, 0.96, 0.93,
            0.38, 0.96, 0.84, 1.04, 0.95,
            0.64, 0.96, 0.8, 1.06, 0.95,
            0.88, 0.95, 0.72, 1.0, 0.94,
            1.08, 0.93, 0.6, 0.86, 0.92,
            1.24, 0.92, 0.44, 0.66, 0.84,
            1.36, 0.92, 0.24, 0.4, 0.64,
            1.42, 0.92, 0.06, 0.1, 0.16,
        };

        /// <summary>
        /// Head stations poll to muzzle tip (c_inner/c_00, nandi/ref_01: a long head held high and forward, the broad flat
        /// brow, the cheeks narrowing below the eyes and the broad rounded muzzle, 0.55 m across): fraction of the head
        /// axis, half width, half height above the axis and below it.
        /// </summary>
        private static readonly double[] HeadStations =
        {
            0.00, 0.15, 0.13, 0.13,
            0.05, 0.32, 0.24, 0.28,
            0.16, 0.4, 0.27, 0.37,
            0.32, 0.39, 0.26, 0.38,
            0.5, 0.34, 0.24, 0.34,
            0.66, 0.3, 0.23, 0.3,
            0.8, 0.31, 0.22, 0.28,
            0.9, 0.32, 0.21, 0.26,
            0.96, 0.31, 0.17, 0.21,
            1.00, 0.2, 0.08, 0.1,
        };

        /// <summary>The head's axis from the poll to the muzzle tip (Nandi frame), 1.22 m, falling 28° from the poll.</summary>
        private const double PollY = 2.24, PollZ = 1.04, MuzzleY = 1.7, MuzzleZ = 2.22;

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
            int bull = m.VertexCount;
            Body(m, b, lod);
            Head(m, b, lod);
            // The gilt shades from its dark underside to the lit back and head (c_inner/c_00, nandi/ref_00).
            ShapeColor.VerticalShade(m, bull, m.VertexCount - bull, f.Level + SlabTopM, f.Level + SlabTopM + 2.3, 0.72f, 1.14f);
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
                // Brass oil lamps on the upper step at its four corners.
                for (int q = 0; q < 4; q++)
                {
                    double x = (q % 2 == 0 ? 1 : -1) * 1.38, z = (q / 2 == 0 ? -1 : 1) * 2.3;
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

        /// <summary>Radial segments of the bull's round parts: at least 16 at LOD0 and 8 at LOD1 (smooth normals).</summary>
        private static int Round(int lod0, int lod)
        {
            return lod <= 0 ? lod0 : lod == 1 ? Math.Max(8, lod0 / 2) : Math.Max(6, lod0 / 4);
        }

        /// <summary>
        /// The body: one smooth loft rump to chest (20 sides at LOD0, 12 at LOD1, smoothed between the stations), the two
        /// great haunches that make the round rump seen from the West Gate (a groove down the middle where the tail runs),
        /// the shoulders, the folded forelegs and hind shanks with their hooves, and the tail from the top of the rump
        /// down the groove and curling round the right haunch to its tuft (c_westgate/c_26).
        /// </summary>
        private static void Body(MeshData m, in Affine3 b, int lod)
        {
            int seg = lod == 0 ? 20 : lod == 1 ? 12 : 8;
            int ns = BodyStations.Length / 5, sub = lod == 0 ? 2 : 1;
            int first = m.VertexCount, fi = m.IndexCount, rows = 0;
            for (int i = 0; i < ns - 1; i++)
            {
                for (int k = 0; k < sub || (i == ns - 2 && k == sub); k++)
                {
                    double t = (double)k / sub;
                    int a0 = Math.Max(0, i - 1), a1 = i, a2 = i + 1, a3 = Math.Min(ns - 1, i + 2);
                    double z = Cr(BodyStations, 5, 0, a0, a1, a2, a3, t), cy = Cr(BodyStations, 5, 1, a0, a1, a2, a3, t);
                    double rx = Cr(BodyStations, 5, 2, a0, a1, a2, a3, t), ryT = Cr(BodyStations, 5, 3, a0, a1, a2, a3, t);
                    double ryB = Cr(BodyStations, 5, 4, a0, a1, a2, a3, t);
                    for (int j = 0; j < seg; j++)
                    {
                        double ang = 2 * Math.PI * j / seg, x, y;
                        // Round on top, flattening onto the slab underneath (the folded legs).
                        RingPoint(ang, rx, ryT, ryB, Math.Sin(ang) > 0 ? 0.95 : 0.7, out x, out y);
                        if (y < 0) x *= 1 + 0.06 * Math.Min(1, -y / Math.Max(0.1, ryB));
                        SacredDraw.V(m, b, x, cy + y, z, x, y, 0, Gold);
                    }
                    rows++;
                }
            }
            SacredDraw.Grid(m, first, rows, seg, true, null);
            Close(m, b, first, seg, -1, BodyStations[0] - 0.02, BodyStations[1]);
            Close(m, b, first + (rows - 1) * seg, seg, 1, BodyStations[5 * (ns - 1)] + 0.02, BodyStations[5 * (ns - 1) + 1]);
            ShapeNoise.RecomputeNormals(m, first, m.VertexCount - first, fi, m.IndexCount - fi, true);
            if (lod >= 2) return;
            int r16 = Round(16, lod), r12 = Round(12, lod), r8 = Round(8, lod);
            for (int s = -1; s <= 1; s += 2)
            {
                // The haunches: two big round lobes making the rump, the groove between them on the axis.
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.46, 0.93, -1.2), Gold, 0.56, 0.92, 0.7, r16, false);
                // The shoulder over the folded foreleg.
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.5, 0.82, 0.9), Gold, 0.36, 0.66, 0.5, r12, false);
                // Hind shank folded forward along the flank to its hoof; the foreleg folded under the chest, hoof forward.
                Limb(m, b, lod, 0.17, 0.11, s * 0.84, 0.32, -1.22, s * 0.94, 0.2, -0.78, s * 0.9, 0.14, -0.36);
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.86, 0.12, -0.24), GoldDark, 0.12, 0.11, 0.15, r8, false);
                Limb(m, b, lod, 0.18, 0.12, s * 0.5, 0.4, 0.95, s * 0.52, 0.2, 1.3, s * 0.42, 0.14, 1.56);
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.4, 0.12, 1.66), GoldDark, 0.12, 0.11, 0.14, r8, false);
            }
            // The tail: down the groove from the top of the rump, then round the right haunch (+x seen from behind) to the
            // tuft resting on the slab.
            Path3 c = PashupatiKit.PathS;
            c.Add(0, 1.8, -1.42).Add(0.02, 1.5, -1.72).Add(0.03, 1.1, -1.82).Add(0.1, 0.66, -1.8).Add(0.38, 0.34, -1.7).Add(0.66, 0.24, -1.4)
             .Add(0.86, 0.26, -1.1);
            Path3 smooth = Tail;
            Curves.CatmullRom(c, smooth.Clear(), lod == 0 ? 5 : 3);
            Shapes.Tube(m, b, GoldLight, smooth, 0.075, r12, true, false, default, 0.05);
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0.9, 0.24, -0.98), GoldLight, 0.13, 0.11, 0.2, r12, false);
        }

        [ThreadStatic] private static Path3 _tail;

        private static Path3 Tail
        {
            get { return _tail ?? (_tail = new Path3(64)); }
        }

        /// <summary>Centripetal-free Catmull-Rom of column c of a station table (stride n) between rows a1 and a2.</summary>
        private static double Cr(double[] st, int n, int c, int a0, int a1, int a2, int a3, double t)
        {
            double p0 = st[n * a0 + c], p1 = st[n * a1 + c], p2 = st[n * a2 + c], p3 = st[n * a3 + c];
            double t2 = t * t, t3 = t2 * t;
            return 0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
        }

        /// <summary>Close a ring of the loft with a fan to an apex at (0, cy, z) (direction −1 or +1 along z).</summary>
        private static void Close(MeshData m, in Affine3 b, int ring, int seg, int dir, double z, double cy)
        {
            int c = SacredDraw.V(m, b, 0, cy, z, 0, 0, dir, Gold);
            for (int j = 0; j < seg; j++) SacredDraw.Tri(m, c, ring + j, ring + (j + 1) % seg);
        }

        /// <summary>
        /// The neck with its dewlap, the head as a smooth loft (the broad brow, big white-grey eyes with dark irises in gilt
        /// lids, the broad rounded muzzle with its nostrils and the lip crease), the long crescent horns rising from their
        /// ridged caps and curving in at the tips, the ears held straight out with their round bosses, and the raised strap
        /// down each side of the neck carrying seven round bells (c_inner/c_00).
        /// </summary>
        private static void Head(MeshData m, in Affine3 b, int lod)
        {
            int r16 = Round(16, lod), r12 = Round(12, lod), r8 = Round(8, lod);
            // The neck rising from the chest to the head, and the deep dewlap under it.
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 1.66, 1.14), Gold, 0.48, 0.62, 0.52, r16, false);
            Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 1.16, 1.52), Gold, 0.2, 0.56, 0.36, r12, false);
            // The head loft along the axis poll → muzzle.
            double dy = MuzzleY - PollY, dz = MuzzleZ - PollZ, len = Math.Sqrt(dy * dy + dz * dz);
            dy /= len;
            dz /= len;
            // "Up" in the head: perpendicular to the axis in the vertical plane, toward the brow.
            double uy = dz, uz = -dy;
            int seg = lod == 0 ? 20 : lod == 1 ? 10 : 8;
            int ns = HeadStations.Length / 4, h0 = m.VertexCount, hi = m.IndexCount;
            for (int i = 0; i < ns; i++)
            {
                double t = HeadStations[4 * i] * len, rx = HeadStations[4 * i + 1], ru = HeadStations[4 * i + 2], rd = HeadStations[4 * i + 3];
                double cy = PollY + dy * t, cz = PollZ + dz * t;
                for (int j = 0; j < seg; j++)
                {
                    double ang = 2 * Math.PI * j / seg, x, y;
                    // The brow and the bridge of the nose are broad and flattish, the jaw and the muzzle rounder.
                    RingPoint(ang, rx, ru, rd, Math.Sin(ang) > 0 ? 0.72 : 0.9, out x, out y);
                    SacredDraw.V(m, b, x, cy + y * uy, cz + y * uz, x, y * uy, y * uz, Gold);
                }
            }
            SacredDraw.Grid(m, h0, ns, seg, true, null);
            int c0 = SacredDraw.V(m, b, 0, PollY - 0.03 * dy, PollZ - 0.03 * dz, 0, -dy, -dz, Gold);
            for (int j = 0; j < seg; j++) SacredDraw.Tri(m, c0, h0 + j, h0 + (j + 1) % seg);
            int last = h0 + (ns - 1) * seg;
            int c1 = SacredDraw.V(m, b, 0, MuzzleY + 0.02 * dy, MuzzleZ + 0.02 * dz, 0, dy, dz, Gold);
            for (int j = 0; j < seg; j++) SacredDraw.Tri(m, c1, last + j, last + (j + 1) % seg);
            ShapeNoise.RecomputeNormals(m, h0, m.VertexCount - h0, hi, m.IndexCount - hi, true);
            for (int s = -1; s <= 1; s += 2)
            {
                Horn(m, b, s, lod);
                if (lod >= 2) continue;
                // The eye: a big white-grey ball with a dark round iris, in a gilt lid (c_inner/c_00), on the side of the
                // head a fifth of the way down it, looking out and a little forward.
                double es = 0.24 * len, ey = PollY + dy * es + 0.06 * uy, ez = PollZ + dz * es + 0.06 * uz, ex = s * 0.37;
                Affine3 eye = Affine3.RotationY(s * 0.35).Then(SacredDraw.At(b, ex, ey, ez));
                Shapes.Ellipsoid(m, eye, EyeWhite, 0.05, 0.085, 0.12, r12, false);
                Shapes.Ellipsoid(m, SacredDraw.At(eye, s * 0.035, 0, 0.012), EyeIris, 0.025, 0.055, 0.055, r8, false);
                if (lod == 0) Shapes.Torus(m, Affine3.RotationZ(0.5 * Math.PI).Then(Affine3.Scaling(1, 0.75, 1.05)).Then(eye), GoldLight, 0.115, 0.022, 14, 4);
                // The nostrils on the front of the muzzle, the lip crease along its side.
                double ns2 = 0.97 * len;
                Shapes.Ellipsoid(m, SacredDraw.At(b, s * 0.14, PollY + dy * ns2 + 0.05 * uy, PollZ + dz * ns2 + 0.05 * uz + 0.04), GoldDark, 0.07, 0.045, 0.03, r8, false);
                if (lod == 0)
                {
                    Path3 lip = PashupatiKit.PathS;
                    for (int q = 0; q <= 6; q++)
                    {
                        double t = (0.62 + 0.38 * q / 6.0) * len, w = q < 6 ? 0.31 + 0.01 * q / 6.0 : 0.24;
                        double ly = PollY + dy * t - 0.07 * uy, lz = PollZ + dz * t - 0.07 * uz;
                        lip.Add(s * (q == 6 ? 0.16 : w + 0.005), ly, lz + (q == 6 ? 0.02 : 0));
                    }
                    Shapes.Tube(m, b, GoldDark, lip, 0.016, 6, true);
                }
                // The ear, held out sideways behind the horn: a long leaf with its hollow, and the round ridged boss at its
                // root.
                Affine3 ear = Affine3.RotationY(s * 0.25).Then(Affine3.RotationZ(s * -0.12)).Then(SacredDraw.At(b, s * 0.68, 2.06, 1.04));
                Shapes.Ellipsoid(m, ear, GoldLight, 0.3, 0.07, 0.13, r12, false);
                if (lod == 0)
                {
                    Shapes.Ellipsoid(m, SacredDraw.At(ear, 0, 0.035, 0), GoldDark, 0.22, 0.03, 0.08, 10, false);
                    for (int q = 0; q < 2; q++)
                        Shapes.Torus(m, Affine3.RotationZ(0.5 * Math.PI).Then(SacredDraw.At(b, s * (0.4 + 0.035 * q), 2.08, 1.08)), GoldDark, 0.13 - 0.03 * q, 0.03, 10, 3);
                }
                Shapes.Sphere(m, SacredDraw.At(b, s * 0.41, 2.08, 1.08), GoldLight, 0.11, r8);
                // The raised strap down the side of the neck with its seven round bells, each with its slot.
                Path3 sc = PashupatiKit.PathS;
                sc.Add(s * 0.36, 2.02, 0.94).Add(s * 0.46, 1.8, 0.98).Add(s * 0.49, 1.56, 1.06).Add(s * 0.42, 1.22, 1.22).Add(s * 0.25, 0.96, 1.44)
                  .Add(0, 0.86, 1.58);
                Path3 strap = Tail;
                Curves.CatmullRom(sc, strap.Clear(), 4);
                Shapes.Tube(m, b, GoldDark, strap, 0.04, lod == 0 ? 8 : 5, true);
                if (lod == 0)
                {
                    for (int q = 0; q < 7; q++)
                    {
                        double bx, by, bz;
                        At(strap, (q + 0.5) / 7, out bx, out by, out bz);
                        double ox = bx * 1.12, oz = bz + 0.03;
                        Shapes.Sphere(m, SacredDraw.At(b, ox, by, oz), GoldLight, 0.085, 7);
                        // The slot round the lower half of the bell.
                        Shapes.Torus(m, Affine3.RotationZ(0.5 * Math.PI).Then(SacredDraw.At(b, ox + s * 0.012, by - 0.025, oz)), GoldDark, 0.074, 0.011, 6, 3);
                    }
                }
            }
        }

        /// <summary>A crescent horn about 0.9 m along its curve from its ridged cap on the poll: out and up, the tip curving
        /// in toward the other's (the lyre seen over the rump from the West Gate), tapering from 0.11 m to 0.02 m.</summary>
        private static void Horn(MeshData m, in Affine3 b, int s, int lod)
        {
            Path3 c = PashupatiKit.PathS;
            c.Add(s * 0.24, 2.24, 1.04).Add(s * 0.42, 2.34, 1.02).Add(s * 0.53, 2.5, 1.08).Add(s * 0.53, 2.68, 1.2).Add(s * 0.44, 2.82, 1.34)
             .Add(s * 0.31, 2.88, 1.43);
            Path3 p = Tail;
            Curves.CatmullRom(c, p.Clear(), lod == 0 ? 3 : 2);
            Shapes.Tube(m, b, GoldLight, p, 0.12, lod == 0 ? 12 : lod == 1 ? 8 : 5, true, false, default, 0.02);
            if (lod >= 2) return;
            // The ridged (scaled) cap at the root: three stacked rings.
            for (int q = 0; q < 2; q++)
            {
                double t = 0.05 + 0.07 * q;
                double x, y, z, x1, y1, z1;
                At(p, t, out x, out y, out z);
                At(p, Math.Min(1, t + 0.04), out x1, out y1, out z1);
                Affine3 ring = Affine3.Along(x, y, z, x1, y1, z1, out double _).Then(b);
                Shapes.Torus(m, ring, GoldDark, 0.13 - 0.015 * q, 0.035, lod == 0 ? 12 : 8, 4);
            }
        }

        /// <summary>A point at fraction t along a path (by segment count).</summary>
        private static void At(Path3 p, double t, out double x, out double y, out double z)
        {
            double f = t * (p.Count - 1);
            int i = Math.Min(p.Count - 2, (int)Math.Floor(f));
            double u = f - i;
            x = p.X[i] + (p.X[i + 1] - p.X[i]) * u;
            y = p.Y[i] + (p.Y[i + 1] - p.Y[i]) * u;
            z = p.Z[i] + (p.Z[i + 1] - p.Z[i]) * u;
        }

        /// <summary>A folded limb: a smooth tapering tube through three points.</summary>
        private static void Limb(MeshData m, in Affine3 b, int lod, double r0, double r1, double ax, double ay, double az, double bx, double by, double bz, double cx,
                                 double cy, double cz)
        {
            Path3 c = PashupatiKit.PathS;
            c.Add(ax, ay, az).Add(bx, by, bz).Add(cx, cy, cz);
            Path3 p = Tail;
            Curves.CatmullRom(c, p.Clear(), lod == 0 ? 4 : 2);
            Shapes.Tube(m, b, Gold, p, r0, Round(12, lod), true, false, default, r1);
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
