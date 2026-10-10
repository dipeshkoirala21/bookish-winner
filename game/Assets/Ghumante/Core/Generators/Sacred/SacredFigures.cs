using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>Guardian figures of the temple stairs, in the real order of Nyatapola (bottom to top).</summary>
    internal enum GuardianKind : byte
    {
        Lion = 0,
        Wrestler = 1,
        Elephant = 2,
        Griffin = 3,
        Goddess = 4,
    }

    /// <summary>
    /// The carved figures and the furniture in front of temples (ref_temples 1.5, 1.6; photos refs/temples/lions,
    /// nyatapola, nyatapola_guard, pashupati_nandi, garuda), each with its own silhouette:
    /// <list type="bullet">
    /// <item>the Newar lion (simha): standing square on four column legs with anklets, chest out, a big boxy head with
    /// a wide-open jaw, teeth and fangs, bulging eyes under heavy brows, rows of curls framing the face and down the
    /// neck, a bell necklace and a tail curling up over the back; one outer forepaw raised;</item>
    /// <item>the elephant with its domed head, fanned ears, curling trunk, tusks and a gilt-bordered saddle cloth;</item>
    /// <item>the wrestlers (Jayamal and Phattu): heavy figures seated on one knee with a mace held upright, a turban,
    /// moustache and jewellery;</item>
    /// <item>the griffin (sardula): a lion's body with a hooked beak, curled horns and folded wings;</item>
    /// <item>the goddesses (Singhini and Byaghrini): seated cross-legged on a lotus with four arms, a tall crown and an
    /// arched halo;</item>
    /// <item>Pashupatinath's Nandi: a recumbent gilt bull with a hump, dewlap, folded legs, horns, a bell collar and a
    /// marigold garland; the kneeling Garuda with folded hands and spread wings; the temple bell and the lamp pillar.</item>
    /// </list>
    /// Each stands in a local frame facing +z (the frame's +w), origin on its pedestal top, scaled by its height (or
    /// length); detail 0 is the hero close-up, 1 the generic LOD0, 2 a few blocks, 3 a single blob.
    /// </summary>
    internal static class SacredFigures
    {
        [ThreadStatic] private static Path3 _p;
        [ThreadStatic] private static Profile2 _q;

        private static Profile2 Q
        {
            get { return (_q ?? (_q = new Profile2(24))).Clear(false); }
        }

        private static Path3 P
        {
            get { return (_p ?? (_p = new Path3(16))).Clear(); }
        }

        private static readonly ShapeBrush Ivory = new ShapeBrush(MeshColor.FromHex(0xF2EAD3), MaterialChannel.Paint);
        private static readonly ShapeBrush MouthRed = new ShapeBrush(MeshColor.FromHex(0x7A1E1A), MaterialChannel.Paint, 0.7f);
        private static readonly ShapeBrush Marigold = new ShapeBrush(SacredPalette.Marigold, MaterialChannel.Fabric);

        // ---------------------------------------------------------------------------------------------------------
        // Building blocks
        // ---------------------------------------------------------------------------------------------------------

        private static Affine3 Rot(double rx, double ry, double rz)
        {
            return Affine3.RotationX(rx).Then(Affine3.RotationY(ry)).Then(Affine3.RotationZ(rz));
        }

        /// <summary>A superellipsoid part at (x, y, z) of the figure frame, rotated (x, then y, then z).</summary>
        private static void SE(MeshData m, in Affine3 b, double x, double y, double z, double rx, double ry, double rz, double ev, double eh, in ShapeBrush br,
                               int seg, double ax = 0, double ay = 0, double az = 0)
        {
            Affine3 xf = Rot(ax, ay, az).Then(SacredDraw.At(b, x, y, z));
            Shapes.Superellipsoid(m, xf, br, rx, ry, rz, ev, eh, seg);
        }

        /// <summary>A small latitude/longitude ellipsoid (eyes, curls, paws: cheaper than the cube grid).</summary>
        private static void Ball(MeshData m, in Affine3 b, double x, double y, double z, double rx, double ry, double rz, in ShapeBrush br, int seg)
        {
            Shapes.Ellipsoid(m, SacredDraw.At(b, x, y, z), br, rx, ry, rz, Math.Max(4, seg), false);
        }

        /// <summary>A tapering tube through three points.</summary>
        private static void Tube3(MeshData m, in Affine3 b, in ShapeBrush br, double r0, double r1, int radial, double ax, double ay, double az, double bx, double by,
                                  double bz, double cx, double cy, double cz)
        {
            Path3 p = P;
            p.Add(ax, ay, az).Add(bx, by, bz).Add(cx, cy, cz);
            Shapes.Tube(m, b, br, p, r0, radial, true, false, default, r1);
        }

        /// <summary>A tapering tube through four points.</summary>
        private static void Tube4(MeshData m, in Affine3 b, in ShapeBrush br, double r0, double r1, int radial, double ax, double ay, double az, double bx, double by,
                                  double bz, double cx, double cy, double cz, double dx, double dy, double dz)
        {
            Path3 p = P;
            p.Add(ax, ay, az).Add(bx, by, bz).Add(cx, cy, cz).Add(dx, dy, dz);
            Shapes.Tube(m, b, br, p, r0, radial, true, false, default, r1);
        }

        /// <summary>A cone from a base at (x, y, z) pointing along (dx, dy, dz) (unit) for <paramref name="len"/>.</summary>
        private static void Spike(MeshData m, in Affine3 b, double x, double y, double z, double dx, double dy, double dz, double r, double len, in ShapeBrush br, int radial)
        {
            double l;
            Affine3 a = Affine3.Along(x, y, z, x + dx * len, y + dy * len, z + dz * len, out l).Then(b);
            if (l < 1e-6) return;
            Shapes.Cone(m, a, br, r, l, radial, 0, 0, false);
        }

        /// <summary>A ring (torus) of major radius R round the axis through (x, y, z) tilted by (ax, az).</summary>
        private static void Ring(MeshData m, in Affine3 b, double x, double y, double z, double rMajor, double rMinor, in ShapeBrush br, int major, int minor,
                                 double ax = 0, double az = 0)
        {
            Shapes.Torus(m, Rot(ax, 0, az).Then(SacredDraw.At(b, x, y, z)), br, rMajor, rMinor, major, minor);
        }

        /// <summary>A figure frame: at (u, v, w) of the kit, mirrored across its own x when <paramref name="side"/> &lt; 0.</summary>
        private static Affine3 Frame(in Affine3 k, double u, double v, double w, int side)
        {
            Affine3 at = SacredDraw.At(k, u, v, w);
            return side < 0 ? Affine3.Scaling(-1, 1, 1).Then(at) : at;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Guardians
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Figure quality: the centrepiece close-up (about 1,500 triangles a figure).</summary>
        public const int Centrepiece = 0;

        /// <summary>Figure quality: another hero (about 900).</summary>
        public const int Hero = 1;

        /// <summary>Figure quality: a generic temple (about 450).</summary>
        public const int Generic = 2;

        /// <summary>Figure quality: a small shrine (about 200).</summary>
        public const int Small = 3;

        /// <summary>
        /// A guardian of height h (above its pedestal) at local (u, v, w) of kit transform k, facing +w, at a quality
        /// (<see cref="Centrepiece"/> … <see cref="Small"/>) that each LOD lowers by one step (LOD2: a single blob).
        /// <paramref name="side"/> is the side of the stair it stands on (−1 left, +1 right, 0 alone): the figure is
        /// mirrored so its raised paw or mace is on the outer side, as on Nyatapola's stair.
        /// </summary>
        public static void Guardian(MeshData m, in Affine3 k, double u, double v, double w, double h, GuardianKind kind, bool painted, int quality, int lod, int side = 0)
        {
            Affine3 b = Frame(k, u, v, w, side);
            ShapeBrush body = painted ? Looks.Lime : Looks.Carved;
            int first = m.VertexCount;
            // LOD0 shows the quality asked for; LOD1 drops two steps (at least to the generic figure); LOD2 is a blob.
            int det = lod >= 2 ? 4 : lod == 1 ? Math.Min(3, Math.Max(0, quality) + 2) : Math.Min(3, Math.Max(0, quality));
            if (det >= 4)
            {
                // A single blob in the right proportions.
                bool wide = kind == GuardianKind.Elephant;
                Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.5 * h, 0), body, (wide ? 0.26 : 0.18) * h, 0.5 * h, (wide ? 0.4 : 0.26) * h, 6, false);
                return;
            }
            switch (kind)
            {
                case GuardianKind.Lion:
                    // All four paws planted, as the Bhaktapur and Kathmandu stair lions stand.
                    Lion(m, b, h, body, painted, det, false, false);
                    break;
                case GuardianKind.Griffin:
                    Lion(m, b, h, body, painted, det, true, false);
                    break;
                case GuardianKind.Elephant:
                    Elephant(m, b, h, body, painted, det);
                    break;
                case GuardianKind.Wrestler:
                    Wrestler(m, b, h, body, painted, det);
                    break;
                default:
                    Goddess(m, b, h, body, painted, det);
                    break;
            }
            // Weathered stone: darker and lighter patches over the carving (seam-safe, by position).
            if (!painted && det <= 1) ShapeColor.JitterByPosition(m, first, m.VertexCount - first, 0.09f, 0x51A7u, 0.18 * h);
        }

        /// <summary>
        /// The Newar lion (or, with <paramref name="griffin"/>, the beaked, horned and winged sardula), h tall to the top
        /// of its head, as the Bhaktapur stair lions stand (refs lions 00, 05, 09): a deep body on four heavy column legs
        /// with banded anklets and big clawed paws, the chest high and the neck rising steeply so the big boxy head is held
        /// up with its muzzle tilted to the sky; a wide-open jaw with teeth and fangs, bulging eyes under heavy brows, a ruff
        /// of curls framing the face and rows of hanging ringlets down the back and sides of the neck (the mane), a bell
        /// necklace, spiral reliefs on the shoulders and a tail curling up over the rump.
        /// </summary>
        private static void Lion(MeshData m, in Affine3 b, double h, in ShapeBrush body, bool painted, int det, bool griffin, bool raisePaw)
        {
            // Detail per level (about 2,600 / 1,100 / 450 / 250 triangles): spine ring, skull, limbs, small parts.
            int ring = det == 0 ? 14 : det == 1 ? 12 : 5, head = det == 0 ? 16 : det == 1 ? 12 : 8;
            int leg = det == 0 ? 8 : det == 1 ? 6 : 3, small = det == 0 ? 6 : 4;
            ShapeBrush mane = painted ? Looks.Of(MeshColor.FromHex(0x2F6E4F), MaterialChannel.Paint) : Looks.Ao(body, 0.82f);
            ShapeBrush trim = painted ? Looks.Gilt : Looks.Ao(body, 0.9f);
            ShapeBrush eyeW = painted ? Looks.Lime : Looks.Of(MeshColor.Scale(body.Color, 1.12f), body.Channel);
            ShapeBrush teeth = painted ? Ivory : Looks.Of(MeshColor.Scale(body.Color, 1.18f), body.Channel);
            Spine(m, b, body, h, LionSpine, ring, 0.85);
            // Legs: heavy front columns with banded anklets and clawed paws; haunches, shanks and paws behind.
            for (int s = -1; s <= 1; s += 2)
            {
                double x = s * 0.112 * h;
                if (raisePaw && s > 0 && det <= 1)
                {
                    Tube3(m, b, body, 0.08 * h, 0.06 * h, leg, x, 0.56 * h, 0.06 * h, x + 0.01 * h, 0.44 * h, 0.2 * h, x, 0.4 * h, 0.3 * h);
                    SE(m, b, x, 0.39 * h, 0.335 * h, 0.072 * h, 0.05 * h, 0.08 * h, 0.6, 0.8, body, 8, -1.1);
                }
                else
                {
                    Tube3(m, b, body, 0.085 * h, 0.066 * h, leg, x, 0.56 * h, 0.06 * h, x, 0.3 * h, 0.11 * h, x, 0.06 * h, 0.12 * h);
                    if (det <= 1) SE(m, b, x, 0.042 * h, 0.16 * h, 0.08 * h, 0.044 * h, 0.095 * h, 0.6, 0.7, body, 8);
                    if (det <= 1)
                    {
                        Ring(m, b, x, 0.115 * h, 0.12 * h, 0.07 * h, 0.014 * h, trim, det == 0 ? 12 : 8, 3);
                        // Claws: three rounded toes along the front of the paw.
                        for (int t = -1; t <= 1 && det == 0; t++)
                            Ball(m, b, x + t * 0.04 * h, 0.03 * h, 0.235 * h, 0.024 * h, 0.026 * h, 0.03 * h, body, 4);
                    }
                }
                double hx = s * 0.115 * h;
                if (det <= 1) SE(m, b, hx, 0.37 * h, -0.2 * h, 0.085 * h, 0.15 * h, 0.13 * h, 0.85, 0.9, body, 8);
                Tube3(m, b, body, 0.07 * h, 0.056 * h, leg, hx, 0.28 * h, -0.24 * h, hx, 0.14 * h, -0.28 * h, hx, 0.06 * h, -0.24 * h);
                if (det <= 1) SE(m, b, hx, 0.04 * h, -0.2 * h, 0.07 * h, 0.04 * h, 0.085 * h, 0.6, 0.7, body, 8);
                if (det == 0)
                {
                    // The spiral relief on the shoulder.
                    Ring(m, b, s * 0.168 * h, 0.5 * h, 0.04 * h, 0.05 * h, 0.012 * h, trim, 10, 3, 0, 0.5 * Math.PI);
                    Ball(m, b, s * 0.172 * h, 0.5 * h, 0.04 * h, 0.012 * h, 0.02 * h, 0.02 * h, trim, 4);
                }
            }
            // The necklace with three bells.
            if (det <= 1)
            {
                Ring(m, b, 0, 0.6 * h, 0.12 * h, 0.17 * h, 0.019 * h, trim, det == 0 ? 14 : 10, 3, 0.6);
                for (int q = -1; q <= 1; q++)
                {
                    if (det == 1 && q != 0) continue;
                    Profile2 bp = Q;
                    double bh = 0.06 * h, br = 0.03 * h;
                    bp.Add(0, 0).Add(br, 0, true).Add(0.85 * br, 0.4 * bh).Add(0.55 * br, 0.85 * bh).Add(0, bh);
                    Shapes.Lathe(m, SacredDraw.At(b, q * 0.1 * h, 0.5 * h - (q == 0 ? 0.01 * h : 0), 0.265 * h - Math.Abs(q) * 0.03 * h), painted ? Looks.Gilt : Looks.Bronze, bp,
                                 det == 0 ? 6 : 5);
                }
            }
            // The head, held high on the neck with the muzzle tilted up (the griffin's level).
            double py = 0.8 * h, pz = 0.15 * h, tilt = griffin ? 0 : -0.24;
            Affine3 hb = Affine3.Translation(0, -py, -pz).Then(Affine3.RotationX(tilt)).Then(Affine3.Translation(0, py, pz)).Then(b);
            SE(m, hb, 0, 0.86 * h, 0.19 * h, 0.17 * h, 0.14 * h, 0.135 * h, 0.5, 0.6, body, head);
            if (griffin)
            {
                // Hooked beak, round eyes, curled horns and the folded wings.
                // An eagle's short beak, hooked hard down (refs nyatapola_guard 01).
                Tube4(m, b, Looks.Ao(body, 0.95f), 0.075 * h, 0.012 * h, leg, 0, 0.86 * h, 0.27 * h, 0, 0.875 * h, 0.35 * h, 0, 0.83 * h, 0.405 * h, 0, 0.755 * h, 0.395 * h);
                for (int s = -1; s <= 1; s += 2)
                {
                    Ball(m, b, s * 0.075 * h, 0.9 * h, 0.3 * h, 0.038 * h, 0.038 * h, 0.03 * h, eyeW, small);
                    if (det <= 2) Ball(m, b, s * 0.075 * h, 0.9 * h, 0.326 * h, 0.018 * h, 0.018 * h, 0.012 * h, Looks.Ink, 4);
                    Tube4(m, b, mane, 0.032 * h, 0.012 * h, leg, s * 0.06 * h, 0.95 * h, 0.2 * h, s * 0.1 * h, 1.06 * h, 0.14 * h, s * 0.08 * h, 1.11 * h, 0.03 * h,
                          s * 0.04 * h, 1.05 * h, 0.0);
                    SE(m, b, s * 0.19 * h, 0.58 * h, -0.04 * h, 0.035 * h, 0.17 * h, 0.21 * h, 0.9, 0.9, mane, det == 0 ? 12 : 8, -0.5, 0, s * -0.15);
                    if (det == 0)
                    {
                        SE(m, b, s * 0.21 * h, 0.5 * h, -0.1 * h, 0.025 * h, 0.12 * h, 0.17 * h, 0.9, 0.9, trim, 8, -0.6, 0, s * -0.2);
                        SE(m, b, s * 0.22 * h, 0.43 * h, -0.16 * h, 0.02 * h, 0.08 * h, 0.12 * h, 0.9, 0.9, mane, 8, -0.7, 0, s * -0.25);
                    }
                }
                if (det <= 2) Ruff(m, b, mane, 0, 0.84 * h, 0.15 * h, 0.155 * h, 0.04 * h, 8, det == 0 ? 32 : det == 1 ? 24 : 10, det == 0 ? 5 : 3, 0.25);
            }
            else
            {
                // The wide-open jaw: a broad flat muzzle with an upturned nose, the dropped lower jaw, the dark mouth,
                // tongue, teeth and fangs.
                if (det <= 1) SE(m, hb, 0, 0.815 * h, 0.33 * h, 0.13 * h, 0.06 * h, 0.09 * h, 0.45, 0.55, body, det == 0 ? 12 : 8, -0.1);
                else Ball(m, hb, 0, 0.8 * h, 0.31 * h, 0.12 * h, 0.06 * h, 0.08 * h, body, 4);
                if (det <= 1)
                {
                    SE(m, hb, 0, 0.69 * h, 0.3 * h, 0.105 * h, 0.032 * h, 0.08 * h, 0.55, 0.6, body, 8, 0.35);
                    SE(m, hb, 0, 0.745 * h, 0.3 * h, 0.098 * h, 0.05 * h, 0.065 * h, 0.8, 0.8, painted ? Looks.PaintRed : MouthRed, 8);
                }
                else if (det == 2)
                {
                    Ball(m, hb, 0, 0.72 * h, 0.3 * h, 0.09 * h, 0.045 * h, 0.07 * h, painted ? Looks.PaintRed : MouthRed, 4);
                }
                if (det <= 1) SE(m, hb, 0, 0.85 * h, 0.405 * h, 0.065 * h, 0.035 * h, 0.035 * h, 0.7, 0.7, body, small + 2);
                if (det <= 2)
                {
                    int tr = det == 0 ? 4 : 3;
                    for (int t = -1; t <= 1; t += 2)
                    {
                        Spike(m, hb, t * 0.092 * h, 0.785 * h, 0.37 * h, 0, -1, 0.15, 0.02 * h, 0.065 * h, teeth, tr);
                        if (det <= 1) Spike(m, hb, t * 0.078 * h, 0.71 * h, 0.35 * h, 0, 1, 0.1, 0.017 * h, 0.05 * h, teeth, tr);
                        if (det == 0)
                        {
                            Spike(m, hb, t * 0.024 * h, 0.785 * h, 0.395 * h, 0, -1, 0.1, 0.012 * h, 0.03 * h, teeth, tr);
                            Spike(m, hb, t * 0.056 * h, 0.785 * h, 0.39 * h, 0, -1, 0.1, 0.012 * h, 0.03 * h, teeth, tr);
                        }
                    }
                    if (det == 0) SE(m, hb, 0, 0.72 * h, 0.345 * h, 0.05 * h, 0.012 * h, 0.05 * h, 0.8, 0.8, Looks.PaintRed, 8, 0.3);
                }
                for (int s = -1; s <= 1 && det == 2; s += 2)
                {
                    // The generic temple's lion keeps its bulging eyes and brows.
                    Ball(m, hb, s * 0.074 * h, 0.905 * h, 0.315 * h, 0.034 * h, 0.032 * h, 0.022 * h, Looks.Ink, 4);
                }
                for (int s = -1; s <= 1 && det <= 1; s += 2)
                {
                    // Bulging eyes with pupils, heavy curled brows, whisker curls on the cheeks, small ears.
                    Ball(m, hb, s * 0.074 * h, 0.905 * h, 0.305 * h, 0.046 * h, 0.042 * h, 0.034 * h, eyeW, small);
                    Ball(m, hb, s * 0.074 * h, 0.905 * h, 0.336 * h, 0.022 * h, 0.022 * h, 0.01 * h, Looks.Ink, 4);
                    Tube3(m, hb, mane, 0.022 * h, 0.014 * h, 4, s * 0.012 * h, 0.95 * h, 0.325 * h, s * 0.075 * h, 0.975 * h, 0.33 * h, s * 0.135 * h, 0.945 * h, 0.3 * h);
                    if (det == 0) Ring(m, hb, s * 0.125 * h, 0.8 * h, 0.33 * h, 0.03 * h, 0.01 * h, mane, 10, 3, 0, s * 0.5 * Math.PI);
                    Spike(m, hb, s * 0.145 * h, 0.95 * h, 0.16 * h, s * 0.6, 0.8, 0, 0.036 * h, 0.075 * h, body, 4);
                }
                // Curls framing the face.
                if (det <= 2) Ruff(m, hb, mane, 0, 0.84 * h, 0.17 * h, 0.18 * h, 0.048 * h, det <= 1 ? 9 : 3, det <= 1 ? 27 : 6, det <= 1 ? 4 : 3, 0.2);
                // The mane: rows of hanging ringlets down the back and sides of the neck to the shoulders.
                if (det <= 1)
                {
                    int rows = det == 0 ? 4 : 3, cols = det == 0 ? 5 : 4;
                    for (int r = 0; r < rows; r++)
                    {
                        double t = rows == 1 ? 0 : (double)r / (rows - 1);
                        double cy = (0.8 - 0.22 * t) * h, cz = (0.1 - 0.12 * t) * h, ra = (0.15 + 0.03 * t) * h, rb = (0.14 + 0.04 * t) * h;
                        for (int q = 0; q < cols; q++)
                        {
                            double th = (-1.9 + 3.8 * (q + 0.5 * (r % 2)) / cols);
                            if (Math.Abs(th) > 1.95) continue;
                            double cx = ra * Math.Sin(th), zz = cz - rb * Math.Cos(th);
                            Shapes.Ellipsoid(m, Rot(0.15, th, 0).Then(SacredDraw.At(b, cx, cy, zz)), mane, 0.034 * h, 0.052 * h, 0.03 * h, 6, false);
                        }
                    }
                }
            }
            // Tail curling up over the rump with a curly tuft.
            if (det <= 1) Tube4(m, b, body, 0.034 * h, 0.02 * h, det == 0 ? 6 : 4, 0, 0.49 * h, -0.32 * h, 0, 0.62 * h, -0.42 * h, 0, 0.78 * h, -0.4 * h, 0, 0.83 * h, -0.31 * h);
            if (det <= 1) Ball(m, b, 0, 0.84 * h, -0.28 * h, 0.055 * h, 0.045 * h, 0.055 * h, mane, small);
        }

        /// <summary>An elephant h tall to the top of its saddle cloth, facing +z: one smooth body from the tail to the
        /// domed forehead, pillar legs with toenail rims, fanned ears, a curling trunk, tusks and a gilt-bordered saddle
        /// cloth.</summary>
        private static void Elephant(MeshData m, in Affine3 b, double h, in ShapeBrush body, bool painted, int det)
        {
            int ring = det == 0 ? 14 : det == 1 ? 12 : det == 2 ? 8 : 6, leg = det == 0 ? 8 : det <= 2 ? 6 : 4, small = det == 0 ? 6 : 4;
            // The stone elephants' saddle cloth and ornaments are carved in the same stone (Nyatapola, refs lions 06); a
            // painted guardian gets the red cloth and gilt trim.
            ShapeBrush cloth = painted ? Looks.Of(MeshColor.FromHex(0xA8322B), MaterialChannel.Fabric) : Looks.Ao(body, 0.86f);
            ShapeBrush border = painted ? Looks.Gilt : Looks.Of(MeshColor.Scale(body.Color, 1.1f), body.Channel);
            Spine(m, b, body, h, ElephantSpine, ring, 0.95);
            if (det <= 1)
                for (int s = -1; s <= 1; s += 2)
                    Ball(m, b, s * 0.06 * h, 0.82 * h, 0.39 * h, 0.075 * h, 0.06 * h, 0.065 * h, body, small);
            for (int s = -1; s <= 1; s += 2)
            {
                if (det <= 2) SE(m, b, s * 0.17 * h, 0.64 * h, 0.28 * h, 0.028 * h, 0.17 * h, 0.13 * h, 0.9, 0.9, Looks.Ao(body, 0.9f), 8, 0, s * 0.45);
                else Ball(m, Rot(0, s * 0.45, 0).Then(SacredDraw.At(b, s * 0.17 * h, 0.64 * h, 0.28 * h)), 0, 0, 0, 0.028 * h, 0.17 * h, 0.13 * h, Looks.Ao(body, 0.9f), 4);
                // Pillar legs with toenail rims.
                double x = s * 0.14 * h;
                Shapes.Frustum(m, SacredDraw.At(b, x, 0, 0.2 * h), body, 0.092 * h, 0.085 * h, 0.48 * h, leg);
                Shapes.Frustum(m, SacredDraw.At(b, x, 0, -0.3 * h), body, 0.092 * h, 0.085 * h, 0.48 * h, leg);
                if (det <= 1)
                {
                    ShapeBrush nail = Looks.Of(MeshColor.Scale(body.Color, 1.1f), body.Channel);
                    Shapes.Cylinder(m, SacredDraw.At(b, x, 0, 0.2 * h), nail, 0.1 * h, 0.045 * h, leg, 0, 0, false, true);
                    Shapes.Cylinder(m, SacredDraw.At(b, x, 0, -0.3 * h), nail, 0.1 * h, 0.045 * h, leg, 0, 0, false, true);
                    Ball(m, b, s * 0.12 * h, 0.72 * h, 0.44 * h, 0.018 * h, 0.018 * h, 0.012 * h, Looks.Ink, 4);
                    Tube3(m, b, painted ? Ivory : border, 0.022 * h, 0.009 * h, small, s * 0.07 * h, 0.56 * h, 0.45 * h, s * 0.09 * h, 0.47 * h, 0.56 * h, s * 0.075 * h, 0.48 * h,
                          0.64 * h);
                }
            }
            // Trunk hanging and curling forward at the tip.
            Path3 t = P;
            t.Add(0, 0.64 * h, 0.47 * h).Add(0, 0.46 * h, 0.55 * h).Add(0, 0.26 * h, 0.58 * h).Add(0, 0.1 * h, 0.6 * h).Add(0, 0.05 * h, 0.67 * h);
            Shapes.Tube(m, b, body, t, 0.07 * h, det == 0 ? 10 : det <= 2 ? 6 : 4, true, false, default, 0.03 * h);
            // Saddle cloth over the back with a gilt border, a gilt headpiece and necklace, the tail.
            Shapes.RoundedBox(m, SacredDraw.At(b, 0, 0.8 * h, -0.06 * h), cloth, 0.5 * h, 0.045 * h, 0.44 * h, 0.02 * h, 1);
            for (int s = -1; s <= 1 && det <= 2; s += 2)
            {
                Shapes.RoundedBox(m, Rot(0, 0, s * 0.12).Then(SacredDraw.At(b, s * 0.245 * h, 0.64 * h, -0.06 * h)), cloth, 0.03 * h, 0.27 * h, 0.42 * h, 0.012 * h, 1);
                if (det <= 1) Shapes.RoundedBox(m, Rot(0, 0, s * 0.12).Then(SacredDraw.At(b, s * 0.26 * h, 0.51 * h, -0.06 * h)), border, 0.035 * h, 0.035 * h, 0.44 * h, 0.01 * h, 1);
            }
            if (det <= 1)
            {
                SE(m, b, 0, 0.87 * h, 0.35 * h, 0.085 * h, 0.03 * h, 0.09 * h, 0.6, 0.8, border, 8);
                Ring(m, b, 0, 0.58 * h, 0.24 * h, 0.2 * h, 0.018 * h, border, det == 0 ? 16 : 10, 3, 0.35);
                Tube3(m, b, body, 0.018 * h, 0.01 * h, 4, 0, 0.6 * h, -0.44 * h, 0, 0.42 * h, -0.48 * h, 0, 0.26 * h, -0.46 * h);
            }
        }

        /// <summary>A wrestler (Jayamal or Phattu) h tall, seated on his right knee with the left knee up, a mace held
        /// upright in the right hand, a turban, moustache, earrings, necklace and waist sash: one smooth body from the
        /// seat to the crown.</summary>
        private static void Wrestler(MeshData m, in Affine3 b, double h, in ShapeBrush body, bool painted, int det)
        {
            int ring = det == 0 ? 14 : det == 1 ? 10 : 8, limb = det == 0 ? 8 : 5, small = det == 0 ? 6 : 4;
            ShapeBrush dark = Looks.Ao(body, 0.8f), metal = painted ? Looks.Gilt : Looks.Ao(body, 0.9f);
            Spine(m, b, body, h, WrestlerSpine, ring, 0.9);
            // Turban: a wound ring and a domed top.
            Ring(m, b, 0, 0.8 * h, 0.03 * h, 0.09 * h, 0.035 * h, dark, det == 0 ? 14 : 8, det == 0 ? 6 : 4);
            SE(m, b, 0, 0.845 * h, 0.03 * h, 0.09 * h, 0.055 * h, 0.09 * h, 0.9, 0.9, dark, 8);
            if (det <= 1)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Ball(m, b, s * 0.038 * h, 0.75 * h, 0.12 * h, 0.018 * h, 0.014 * h, 0.01 * h, Looks.Ink, 4);
                    Tube3(m, b, dark, 0.013 * h, 0.006 * h, 4, s * 0.005 * h, 0.7 * h, 0.13 * h, s * 0.045 * h, 0.695 * h, 0.12 * h, s * 0.072 * h, 0.722 * h, 0.1 * h);
                    Ring(m, b, s * 0.1 * h, 0.7 * h, 0.03 * h, 0.022 * h, 0.006 * h, metal, 8, 3, 0, 0.5 * Math.PI);
                }
                Ball(m, b, 0, 0.725 * h, 0.13 * h, 0.025 * h, 0.03 * h, 0.022 * h, body, small);
                Ring(m, b, 0, 0.6 * h, 0.03 * h, 0.12 * h, 0.014 * h, metal, det == 0 ? 14 : 8, 3, 0.35);
                Ring(m, b, 0, 0.28 * h, -0.01 * h, 0.195 * h, 0.024 * h, dark, det == 0 ? 16 : 10, 3);
            }
            // Arms: the right hand holds the mace upright, the left rests on the raised knee.
            Tube3(m, b, body, 0.06 * h, 0.046 * h, limb, 0.2 * h, 0.56 * h, 0.0, 0.26 * h, 0.43 * h, 0.07 * h, 0.21 * h, 0.37 * h, 0.19 * h);
            Tube3(m, b, body, 0.06 * h, 0.046 * h, limb, -0.2 * h, 0.56 * h, 0.0, -0.25 * h, 0.42 * h, 0.06 * h, -0.17 * h, 0.36 * h, 0.17 * h);
            Ball(m, b, 0.21 * h, 0.36 * h, 0.21 * h, 0.05 * h, 0.05 * h, 0.05 * h, body, small);
            Ball(m, b, -0.16 * h, 0.35 * h, 0.2 * h, 0.05 * h, 0.04 * h, 0.05 * h, body, small);
            // Legs: left knee up, right knee on the ground.
            Tube3(m, b, body, 0.078 * h, 0.05 * h, limb, -0.1 * h, 0.18 * h, 0.0, -0.13 * h, 0.33 * h, 0.2 * h, -0.12 * h, 0.04 * h, 0.25 * h);
            Tube3(m, b, body, 0.078 * h, 0.05 * h, limb, 0.1 * h, 0.16 * h, 0.0, 0.13 * h, 0.05 * h, 0.19 * h, 0.13 * h, 0.04 * h, -0.14 * h);
            Ball(m, b, -0.12 * h, 0.025 * h, 0.3 * h, 0.05 * h, 0.025 * h, 0.08 * h, body, small);
            // The mace: a shaft from the ground to above the shoulder, a ribbed head with a point.
            Shapes.Cylinder(m, SacredDraw.At(b, 0.22 * h, 0.02 * h, 0.22 * h), metal, 0.022 * h, 0.72 * h, det == 0 ? 8 : 5);
            SE(m, b, 0.22 * h, 0.78 * h, 0.22 * h, 0.065 * h, 0.08 * h, 0.065 * h, 0.9, 0.9, metal, 8);
            if (det == 0)
            {
                Ring(m, b, 0.22 * h, 0.78 * h, 0.22 * h, 0.066 * h, 0.012 * h, Looks.Ao(metal, 0.8f), 10, 3);
                Spike(m, b, 0.22 * h, 0.85 * h, 0.22 * h, 0, 1, 0, 0.02 * h, 0.06 * h, metal, 6);
            }
        }

        /// <summary>A goddess (Singhini, Byaghrini) h tall, seated cross-legged on a lotus with four arms, a tall gilt
        /// crown and an arched halo behind: one smooth body from the seat to the crown.</summary>
        private static void Goddess(MeshData m, in Affine3 b, double h, in ShapeBrush body, bool painted, int det)
        {
            int ring = det == 0 ? 14 : det == 1 ? 10 : 8, limb = det == 0 ? 6 : 4, small = det == 0 ? 6 : 4;
            ShapeBrush gilt = painted ? Looks.Gilt : Looks.Of(MeshColor.Scale(body.Color, 1.06f), body.Channel);
            // Lotus seat: a lathe with petal lips.
            Profile2 q = Q;
            q.Add(0, 0).Add(0.27 * h, 0, true).Add(0.3 * h, 0.04 * h).Add(0.26 * h, 0.08 * h).Add(0.29 * h, 0.1 * h).Add(0.24 * h, 0.13 * h, true).Add(0, 0.13 * h);
            Shapes.Lathe(m, b, Looks.Ao(body, 0.92f), q, det == 0 ? 16 : 10);
            // Crossed legs and the body.
            SE(m, b, 0, 0.18 * h, 0.05 * h, 0.23 * h, 0.065 * h, 0.13 * h, 0.75, 0.8, body, det == 0 ? 12 : 8);
            Spine(m, b, body, h, GoddessSpine, ring, 0.95);
            // Tall crown (mukut) and the halo arch.
            q = Q;
            q.Add(0, 0).Add(0.085 * h, 0, true).Add(0.08 * h, 0.05 * h).Add(0.065 * h, 0.1 * h).Add(0.07 * h, 0.13 * h).Add(0.04 * h, 0.2 * h).Add(0.015 * h, 0.25 * h)
             .Add(0, 0.26 * h);
            Shapes.Lathe(m, SacredDraw.At(b, 0, 0.69 * h, 0.02 * h), gilt, q, det == 0 ? 10 : 6);
            if (det <= 1)
            {
                double[] x = ShapeScratch.U, z = ShapeScratch.W;
                int n = 0, seg = det == 0 ? 10 : 6;
                x[n] = -0.26 * h;
                z[n++] = 0;
                x[n] = 0.26 * h;
                z[n++] = 0;
                for (int i = 0; i <= seg; i++)
                {
                    double a = Math.PI * i / seg;
                    x[n] = 0.26 * h * Math.Cos(a);
                    z[n++] = -(0.55 * h + 0.42 * h * Math.Sin(a));
                }
                Shapes.BevelExtrude(m, SacredDraw.Basis(b, 0, 0.12 * h, -0.14 * h, 1, 0, 0, 0, 0, 1), Looks.Ao(body, 0.88f), x, z, n, 0.06 * h, 0.015 * h, 1, BevelStyle.Round,
                                    true, false);
                for (int s = -1; s <= 1; s += 2) Ball(m, b, s * 0.03 * h, 0.66 * h, 0.09 * h, 0.012 * h, 0.01 * h, 0.006 * h, Looks.Ink, 4);
            }
            // Four arms: the upper pair raised holding a sword and a lotus, the lower pair in the lap.
            for (int s = -1; s <= 1; s += 2)
            {
                Tube3(m, b, body, 0.034 * h, 0.026 * h, limb, s * 0.12 * h, 0.52 * h, 0.0, s * 0.2 * h, 0.44 * h, 0.04 * h, s * 0.13 * h, 0.3 * h, 0.12 * h);
                Ball(m, b, s * 0.12 * h, 0.29 * h, 0.14 * h, 0.03 * h, 0.03 * h, 0.03 * h, body, small);
                if (det <= 1)
                {
                    Tube3(m, b, body, 0.031 * h, 0.024 * h, limb, s * 0.12 * h, 0.53 * h, -0.02 * h, s * 0.24 * h, 0.6 * h, 0.0, s * 0.25 * h, 0.74 * h, 0.03 * h);
                    if (s > 0) Spike(m, b, 0.25 * h, 0.74 * h, 0.03 * h, 0, 1, 0, 0.018 * h, 0.22 * h, Looks.Ao(gilt, 0.9f), 4);
                    else Ball(m, b, -0.25 * h, 0.79 * h, 0.03 * h, 0.05 * h, 0.04 * h, 0.05 * h, gilt, small);
                }
            }
            if (det <= 1) Ring(m, b, 0, 0.53 * h, 0.03 * h, 0.08 * h, 0.012 * h, gilt, det == 0 ? 12 : 8, 3, 0.4);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Vahanas
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Pashupatinath's Nandi: a recumbent gilt bull of length l lying on its pedestal, facing +w: one smooth body from
        /// the rump to the muzzle (the head raised), the hump, a heavy dewlap, short horns, ears and a dark muzzle, the
        /// forelegs folded under the chest with their hooves showing, the hind legs tucked along the flanks, a red bell
        /// collar with its bell and a marigold garland, the tail along the side.
        /// </summary>
        public static void Nandi(MeshData m, in Affine3 k, double u, double v, double w, double l, in ShapeBrush b, int lod)
        {
            Affine3 f = SacredDraw.At(k, u, v, w);
            if (lod >= 2)
            {
                Spine(m, f, b, l, NandiSpine, 6, 0.9);
                return;
            }
            int ring = lod == 0 ? 16 : 10, limb = lod == 0 ? 6 : 4, small = lod == 0 ? 6 : 4;
            ShapeBrush dark = Looks.Ao(b, 0.75f), red = Looks.Of(MeshColor.FromHex(0xB0281F), MaterialChannel.Fabric);
            Spine(m, f, b, l, NandiSpine, ring, 0.9);
            // Hump, dewlap, the broad forehead and the dark muzzle.
            SE(m, f, 0, 0.42 * l, 0.16 * l, 0.11 * l, 0.1 * l, 0.12 * l, 0.85, 0.9, b, lod == 0 ? 12 : 8);
            SE(m, f, 0, 0.24 * l, 0.33 * l, 0.045 * l, 0.12 * l, 0.1 * l, 0.9, 0.9, b, 8);
            SE(m, f, 0, 0.5 * l, 0.43 * l, 0.105 * l, 0.06 * l, 0.07 * l, 0.6, 0.8, b, 8);
            SE(m, f, 0, 0.41 * l, 0.55 * l, 0.065 * l, 0.05 * l, 0.035 * l, 0.8, 0.8, dark, 8);
            for (int s = -1; s <= 1; s += 2)
            {
                // Short crescent horns curving out and up from the round poll (refs pashupati_nandi 00), ears out to the
                // sides, eyes.
                Tube4(m, f, Looks.GiltHi, 0.028 * l, 0.01 * l, limb, s * 0.055 * l, 0.535 * l, 0.415 * l, s * 0.095 * l, 0.55 * l, 0.41 * l, s * 0.11 * l, 0.585 * l, 0.405 * l,
                      s * 0.095 * l, 0.615 * l, 0.4 * l);
                SE(m, f, s * 0.13 * l, 0.5 * l, 0.4 * l, 0.06 * l, 0.02 * l, 0.035 * l, 0.8, 0.8, b, 8, 0, 0, s * -0.25);
                Ball(m, f, s * 0.08 * l, 0.5 * l, 0.48 * l, 0.014 * l, 0.014 * l, 0.01 * l, Looks.Ink, 4);
                // Folded forelegs under the chest, hooves forward; hind legs tucked along the flank.
                Tube3(m, f, b, 0.048 * l, 0.04 * l, limb, s * 0.12 * l, 0.15 * l, 0.15 * l, s * 0.1 * l, 0.06 * l, 0.2 * l, s * 0.1 * l, 0.045 * l, 0.31 * l);
                Ball(m, f, s * 0.1 * l, 0.04 * l, 0.34 * l, 0.042 * l, 0.03 * l, 0.036 * l, dark, small);
                SE(m, f, s * 0.18 * l, 0.17 * l, -0.24 * l, 0.05 * l, 0.11 * l, 0.15 * l, 0.8, 0.9, b, lod == 0 ? 12 : 8);
                Ball(m, f, s * 0.2 * l, 0.04 * l, -0.05 * l, 0.036 * l, 0.03 * l, 0.046 * l, dark, small);
            }
            // Bell collar with its bell, the marigold garland, the tail.
            // The collar and garland hug the neck and chest (bands of the body's own cross-sections, a little larger).
            Spine(m, f, red, l, NandiCollar, ring, 0.9);
            Profile2 q = Q;
            double bh = 0.07 * l, br = 0.035 * l;
            q.Add(0, 0).Add(br, 0, true).Add(0.85 * br, 0.4 * bh).Add(0.6 * br, 0.85 * bh).Add(0, bh);
            Shapes.Lathe(m, SacredDraw.At(f, 0, 0.17 * l, 0.42 * l), Looks.Bronze, q, lod == 0 ? 8 : 5);
            if (lod == 0) Spine(m, f, Marigold, l, NandiGarland, ring, 0.9);
            Tube4(m, f, b, 0.016 * l, 0.01 * l, 4, 0, 0.3 * l, -0.44 * l, 0.1 * l, 0.16 * l, -0.44 * l, 0.19 * l, 0.06 * l, -0.33 * l, 0.21 * l, 0.03 * l, -0.2 * l);
        }

        /// <summary>A kneeling Garuda of height h with hands folded, wings spread behind, a beaked face under a gilt crown
        /// and a serpent round the neck, facing +w.</summary>
        public static void Garuda(MeshData m, in Affine3 k, double u, double v, double w, double h, in ShapeBrush stone, int lod)
        {
            Affine3 f = SacredDraw.At(k, u, v, w);
            ShapeBrush b = stone.Channel == MaterialChannel.Stone ? Looks.Carved : stone;
            if (lod >= 2)
            {
                Spine(m, f, b, h, GarudaSpine, 6, 0.9);
                return;
            }
            int ring = lod == 0 ? 14 : 10, limb = lod == 0 ? 6 : 4, small = lod == 0 ? 6 : 4;
            ShapeBrush wing = Looks.Ao(b, 0.9f);
            Spine(m, f, b, h, GarudaSpine, ring, 0.9);
            for (int s = -1; s <= 1; s += 2)
            {
                // Kneeling thighs and feet.
                SE(m, f, s * 0.08 * h, 0.1 * h, 0.03 * h, 0.065 * h, 0.065 * h, 0.15 * h, 0.85, 0.9, b, 8);
                Ball(m, f, s * 0.08 * h, 0.05 * h, -0.13 * h, 0.05 * h, 0.04 * h, 0.07 * h, b, small);
            }
            Tube3(m, f, Looks.Ao(b, 0.95f), 0.032 * h, 0.008 * h, limb, 0, 0.7 * h, 0.1 * h, 0, 0.68 * h, 0.15 * h, 0, 0.63 * h, 0.16 * h);
            Profile2 q = Q;
            q.Add(0, 0).Add(0.08 * h, 0, true).Add(0.07 * h, 0.06 * h).Add(0.045 * h, 0.13 * h).Add(0.015 * h, 0.19 * h).Add(0, 0.2 * h);
            Shapes.Lathe(m, SacredDraw.At(f, 0, 0.77 * h, 0.03 * h), Looks.Gilt, q, lod == 0 ? 10 : 6);
            for (int s = -1; s <= 1; s += 2)
            {
                if (lod == 0) Ball(m, f, s * 0.035 * h, 0.72 * h, 0.105 * h, 0.014 * h, 0.014 * h, 0.01 * h, Looks.Ink, 4);
                // Folded hands (namaste) in front of the chest.
                Tube3(m, f, b, 0.036 * h, 0.028 * h, limb, s * 0.15 * h, 0.53 * h, 0.0, s * 0.13 * h, 0.4 * h, 0.07 * h, s * 0.03 * h, 0.46 * h, 0.14 * h);
                // Wings spread behind, with a second row of feathers.
                SE(m, f, s * 0.2 * h, 0.55 * h, -0.12 * h, 0.03 * h, 0.24 * h, 0.13 * h, 0.9, 0.9, wing, 8, 0, s * 0.6, s * -0.45);
                if (lod == 0) SE(m, f, s * 0.24 * h, 0.44 * h, -0.15 * h, 0.022 * h, 0.17 * h, 0.1 * h, 0.9, 0.9, Looks.Ao(b, 0.8f), 8, 0, s * 0.6, s * -0.65);
            }
            SE(m, f, 0, 0.47 * h, 0.15 * h, 0.035 * h, 0.07 * h, 0.03 * h, 0.8, 0.9, b, 8);
            if (lod == 0) Ring(m, f, 0, 0.59 * h, 0.02 * h, 0.09 * h, 0.016 * h, Looks.Of(MeshColor.FromHex(0x3E7A4A), MaterialChannel.Paint), 12, 3, 0.4);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Smooth bodies
        // ---------------------------------------------------------------------------------------------------------

        [ThreadStatic] private static double[] _grid;

        private static double[] Grid(int n)
        {
            if (_grid == null || _grid.Length < n) _grid = new double[Math.Max(n, 1024)];
            return _grid;
        }

        /// <summary>
        /// Emit a smooth closed surface from a rows × cols grid of figure-local positions (row-major x, y, z; columns wrap
        /// round), with normals from central differences, oriented away from each row's centre. Optional fan caps close
        /// the first and last rows.
        /// </summary>
        private static void EmitGrid(MeshData m, in Affine3 b, in ShapeBrush br, double[] g, int rows, int cols, bool wrapRows, bool caps)
        {
            int first = m.VertexCount;
            for (int r = 0; r < rows; r++)
            {
                double cx = 0, cy = 0, cz = 0;
                for (int c = 0; c < cols; c++)
                {
                    int q = 3 * (r * cols + c);
                    cx += g[q];
                    cy += g[q + 1];
                    cz += g[q + 2];
                }
                cx /= cols;
                cy /= cols;
                cz /= cols;
                for (int c = 0; c < cols; c++)
                {
                    int c0 = (c + cols - 1) % cols, c1 = (c + 1) % cols;
                    int r0 = wrapRows ? (r + rows - 1) % rows : Math.Max(0, r - 1), r1 = wrapRows ? (r + 1) % rows : Math.Min(rows - 1, r + 1);
                    int qa = 3 * (r * cols + c0), qb = 3 * (r * cols + c1), qc = 3 * (r0 * cols + c), qd = 3 * (r1 * cols + c), q = 3 * (r * cols + c);
                    double ax = g[qb] - g[qa], ay = g[qb + 1] - g[qa + 1], az = g[qb + 2] - g[qa + 2];
                    double dx = g[qd] - g[qc], dy = g[qd + 1] - g[qc + 1], dz = g[qd + 2] - g[qc + 2];
                    double nx = ay * dz - az * dy, ny = az * dx - ax * dz, nz = ax * dy - ay * dx;
                    if (nx * (g[q] - cx) + ny * (g[q + 1] - cy) + nz * (g[q + 2] - cz) < 0)
                    {
                        nx = -nx;
                        ny = -ny;
                        nz = -nz;
                    }
                    SacredDraw.V(m, b, g[q], g[q + 1], g[q + 2], nx, ny, nz, br);
                }
            }
            SacredDraw.Grid(m, first, rows, cols, true, null);
            if (wrapRows)
            {
                // Close the seam between the last and the first row.
                for (int c = 0; c < cols; c++)
                {
                    int c1 = (c + 1) % cols;
                    SacredDraw.Quad(m, first + (rows - 1) * cols + c, first + (rows - 1) * cols + c1, first + c1, first + c);
                }
                return;
            }
            if (!caps) return;
            for (int end = 0; end < 2; end++)
            {
                int r = end == 0 ? 0 : rows - 1, rn = end == 0 ? 1 : rows - 2;
                double cx = 0, cy = 0, cz = 0, nx = 0, ny = 0, nz = 0;
                for (int c = 0; c < cols; c++)
                {
                    int q = 3 * (r * cols + c), qn = 3 * (rn * cols + c);
                    cx += g[q] / cols;
                    cy += g[q + 1] / cols;
                    cz += g[q + 2] / cols;
                    nx += (g[q] - g[qn]) / cols;
                    ny += (g[q + 1] - g[qn + 1]) / cols;
                    nz += (g[q + 2] - g[qn + 2]) / cols;
                }
                int centre = SacredDraw.V(m, b, cx + 0.3 * nx, cy + 0.3 * ny, cz + 0.3 * nz, nx, ny, nz, br);
                for (int c = 0; c < cols; c++) SacredDraw.Tri(m, centre, first + r * cols + c, first + r * cols + (c + 1) % cols);
            }
        }

        /// <summary>
        /// A smooth organic body round a spine in the figure's yz plane: <paramref name="st"/> holds stations of (y, z,
        /// half-width a, half-depth b) in units of <paramref name="s"/>, from one end to the other; each station is a
        /// superellipse ring (<paramref name="square"/> 1 = ellipse, lower = boxier) square to the spine, so torso, chest,
        /// neck and head flow into one surface. Both ends are capped.
        /// </summary>
        private static void Spine(MeshData m, in Affine3 b, in ShapeBrush br, double s, double[] st, int n, double square, double dx = 0)
        {
            int rows = st.Length / 4;
            double[] g = Grid(rows * n * 3);
            for (int r = 0; r < rows; r++)
            {
                int ra = Math.Max(0, r - 1), rb = Math.Min(rows - 1, r + 1);
                double ty = st[4 * rb] - st[4 * ra], tz = st[4 * rb + 1] - st[4 * ra + 1], tl = Math.Sqrt(ty * ty + tz * tz);
                if (tl < 1e-9)
                {
                    ty = 0;
                    tz = 1;
                    tl = 1;
                }
                double ny = tz / tl, nz = -ty / tl;
                double y = st[4 * r] * s, z = st[4 * r + 1] * s, a = st[4 * r + 2] * s, bb = st[4 * r + 3] * s;
                for (int c = 0; c < n; c++)
                {
                    double t = 2 * Math.PI * c / n, cs = Math.Cos(t), sn = Math.Sin(t);
                    double ce = Math.Sign(cs) * Math.Pow(Math.Abs(cs), square), se = Math.Sign(sn) * Math.Pow(Math.Abs(sn), square);
                    int q = 3 * (r * n + c);
                    g[q] = dx + a * ce;
                    g[q + 1] = y + bb * se * ny;
                    g[q + 2] = z + bb * se * nz;
                }
            }
            EmitGrid(m, b, br, g, rows, n, false, true);
        }

        /// <summary>
        /// A ruff of curls: a torus of major radius R round the axis through (x, y, z) (along +z, tilted by
        /// <paramref name="tilt"/> about x) whose tube swells into <paramref name="lobes"/> curls, each with a dimple; a
        /// lion's mane frame or the rows of curls down its neck.
        /// </summary>
        private static void Ruff(MeshData m, in Affine3 b, in ShapeBrush br, double x, double y, double z, double R, double r, int lobes, int major, int minor,
                                 double tilt)
        {
            int rows = major;
            double[] g = Grid(rows * minor * 3);
            double ct = Math.Cos(tilt), stl = Math.Sin(tilt);
            for (int i = 0; i < rows; i++)
            {
                double th = 2 * Math.PI * i / major;
                double lobe = Math.Cos(lobes * th);
                double swell = 1 + 0.45 * Math.Max(0, lobe) * Math.Max(0, lobe);
                double ux = Math.Sin(th), uy = Math.Cos(th);
                for (int j = 0; j < minor; j++)
                {
                    double ph = 2 * Math.PI * j / minor;
                    double dimple = Math.Max(0, lobe) > 0.92 && Math.Cos(ph) > 0.7 ? 0.75 : 1.0;
                    double rr = r * swell * dimple, radial = R + rr * Math.Cos(ph), fz = rr * Math.Sin(ph);
                    double px = radial * ux, py = radial * uy, pz = fz;
                    int q = 3 * (i * minor + j);
                    g[q] = x + px;
                    g[q + 1] = y + py * ct - pz * stl;
                    g[q + 2] = z + py * stl + pz * ct;
                }
            }
            EmitTorus(m, b, br, g, rows, minor);
        }

        /// <summary>Emit a torus-like grid (rows round the ring, columns round the tube) with normals pointing away from the
        /// tube's centre line.</summary>
        private static void EmitTorus(MeshData m, in Affine3 b, in ShapeBrush br, double[] g, int rows, int cols)
        {
            int first = m.VertexCount;
            for (int i = 0; i < rows; i++)
            {
                // Tube centre of this row: the mean of its ring.
                double cx = 0, cy = 0, cz = 0;
                for (int j = 0; j < cols; j++)
                {
                    int q = 3 * (i * cols + j);
                    cx += g[q] / cols;
                    cy += g[q + 1] / cols;
                    cz += g[q + 2] / cols;
                }
                for (int j = 0; j < cols; j++)
                {
                    int q = 3 * (i * cols + j);
                    SacredDraw.V(m, b, g[q], g[q + 1], g[q + 2], g[q] - cx, g[q + 1] - cy, g[q + 2] - cz, br);
                }
            }
            SacredDraw.Grid(m, first, rows, cols, true, null);
            for (int j = 0; j < cols; j++)
            {
                int j1 = (j + 1) % cols;
                SacredDraw.Quad(m, first + (rows - 1) * cols + j, first + (rows - 1) * cols + j1, first + j1, first + j);
            }
        }

        // Spine stations (y, z, half-width, half-depth) per figure, in units of its height (or length).
        private static readonly double[] LionSpine =
        {
            0.47, -0.33, 0.06, 0.06, 0.48, -0.3, 0.145, 0.15, 0.49, -0.21, 0.175, 0.18, 0.5, -0.1, 0.165, 0.175, 0.53, 0.0, 0.175, 0.195,
            0.59, 0.07, 0.185, 0.21, 0.67, 0.11, 0.17, 0.19, 0.74, 0.13, 0.15, 0.15, 0.8, 0.14, 0.12, 0.11,
        };

        private static readonly double[] NandiSpine =
        {
            0.22, -0.45, 0.08, 0.08, 0.23, -0.41, 0.19, 0.18, 0.24, -0.29, 0.235, 0.225, 0.24, -0.12, 0.225, 0.215, 0.26, 0.05, 0.22, 0.22, 0.29, 0.17, 0.2, 0.23,
            0.35, 0.26, 0.15, 0.17, 0.42, 0.33, 0.12, 0.13, 0.47, 0.39, 0.105, 0.11, 0.48, 0.45, 0.095, 0.095, 0.44, 0.51, 0.08, 0.075, 0.4, 0.545, 0.06, 0.055,
        };

        // Bands round the Nandi's neck (bell collar) and chest (garland): three stations along the body axis there.
        private static readonly double[] NandiCollar =
        {
            0.343, 0.252, 0.163, 0.183, 0.355, 0.266, 0.166, 0.186, 0.367, 0.281, 0.16, 0.18,
        };

        private static readonly double[] NandiGarland =
        {
            0.276, 0.13, 0.213, 0.24, 0.287, 0.152, 0.216, 0.245, 0.298, 0.174, 0.208, 0.238,
        };

        private static readonly double[] ElephantSpine =
        {
            0.56, -0.45, 0.07, 0.07, 0.56, -0.41, 0.19, 0.2, 0.56, -0.29, 0.24, 0.24, 0.57, -0.08, 0.25, 0.25, 0.58, 0.12, 0.24, 0.24, 0.62, 0.25, 0.19, 0.22,
            0.68, 0.35, 0.17, 0.21, 0.68, 0.43, 0.14, 0.17, 0.64, 0.48, 0.08, 0.1,
        };

        private static readonly double[] WrestlerSpine =
        {
            0.1, -0.02, 0.1, 0.08, 0.15, -0.02, 0.19, 0.14, 0.24, -0.01, 0.2, 0.15, 0.36, 0.01, 0.185, 0.16, 0.47, 0.0, 0.19, 0.14, 0.56, -0.01, 0.21, 0.12,
            0.62, 0.0, 0.09, 0.08, 0.66, 0.02, 0.085, 0.085, 0.73, 0.03, 0.1, 0.1, 0.8, 0.03, 0.095, 0.095, 0.84, 0.03, 0.05, 0.05,
        };

        private static readonly double[] GoddessSpine =
        {
            0.13, 0.0, 0.08, 0.06, 0.16, 0.0, 0.14, 0.1, 0.25, 0.0, 0.105, 0.075, 0.36, 0.0, 0.115, 0.08, 0.46, 0.01, 0.135, 0.085, 0.53, 0.0, 0.13, 0.07,
            0.57, 0.01, 0.05, 0.045, 0.6, 0.02, 0.065, 0.07, 0.65, 0.02, 0.08, 0.08, 0.71, 0.02, 0.06, 0.06,
        };

        private static readonly double[] GarudaSpine =
        {
            0.14, -0.05, 0.08, 0.07, 0.18, -0.05, 0.15, 0.11, 0.3, -0.02, 0.13, 0.1, 0.42, 0.0, 0.14, 0.11, 0.53, 0.0, 0.16, 0.1, 0.59, 0.01, 0.07, 0.06,
            0.63, 0.02, 0.075, 0.08, 0.7, 0.03, 0.085, 0.09, 0.76, 0.03, 0.06, 0.06,
        };

        // ---------------------------------------------------------------------------------------------------------
        // Bells and lamps
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A bronze temple bell hung from a beam on two stone posts (and a small hipped tile roof when
        /// <paramref name="roofed"/>), the beam along local u, at (u, v, w). Bell mouth diameter d.</summary>
        public static void TempleBell(MeshData m, in Affine3 k, double u, double v, double w, double d, bool roofed, int lod)
        {
            Affine3 f = SacredDraw.At(k, u, v, w);
            double span = 1.6 * d + 0.5, hgt = 1.6 * d + 1.4, post = 0.18 + 0.06 * d;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                if (lod >= 2) SacredDraw.Box(m, f, sgn * 0.5 * span - post, sgn * 0.5 * span + post, 0, hgt, -post, post, Looks.Stone, BoxFaces.All & ~BoxFaces.Bottom);
                else Shapes.RoundedBox(m, SacredDraw.At(f, sgn * 0.5 * span, 0.5 * hgt, 0), Looks.Stone, 2 * post, hgt, 2 * post, 0.03, 1);
            }
            Shapes.RoundedBox(m, SacredDraw.At(f, 0, hgt + 0.08, 0), Looks.WoodDark, span + 4 * post, 0.16, 2.2 * post, 0.03, 1);
            Profile2 p = Q;
            double bh = 1.05 * d, r = 0.5 * d;
            p.Add(0, 0).Add(r * 1.02, 0, true).Add(r, 0.06 * bh).Add(0.86 * r, 0.25 * bh).Add(0.72 * r, 0.6 * bh).Add(0.62 * r, 0.85 * bh)
             .Add(0.4 * r, 0.97 * bh).Add(0, bh);
            Shapes.Lathe(m, SacredDraw.At(f, 0, hgt - 0.12 - bh, 0), Looks.Bronze, p, SacredParts.Seg(12, lod));
            if (lod < 2) Shapes.Cylinder(m, SacredDraw.At(f, 0, hgt - 0.14, 0), Looks.Bronze, 0.04, 0.16, 5);
            if (roofed && lod < 2)
            {
                // A small hipped tile roof square to the beam and posts (a four-sided frustum would sit on its corners).
                double ru = 0.5 * span + 2 * post + 0.2, rw = post + 0.35;
                SacredParts.HipRoof(m, f, ru - 0.25, rw - 0.25, hgt + 0.3, 0.25, 0.08, hgt + 0.3 + 0.45 + 0.2 * d, false, 0, false, lod + 1);
            }
        }

        /// <summary>A stone lamp pillar (deep stambha): moulded base, shaft and a lotus top with a ring of lamp cups,
        /// height h at (u, v, w).</summary>
        public static void LampPillar(MeshData m, in Affine3 k, double u, double v, double w, double h, int lod)
        {
            Affine3 f = SacredDraw.At(k, u, v, w);
            Profile2 p = Q;
            p.Add(0, 0).Add(0.32, 0, true).Add(0.32, 0.12, true).Add(0.24, 0.16).Add(0.24, 0.3, true).Add(0.13, 0.36).Add(0.11, 0.6 * h)
             .Add(0.15, 0.64 * h).Add(0.11, 0.68 * h).Add(0.1, 0.84 * h).Add(0.24, 0.9 * h).Add(0.3, 0.94 * h, true).Add(0.16, 0.95 * h).Add(0.06, h)
             .Add(0, h);
            Shapes.Lathe(m, f, Looks.Stone, p, SacredParts.Seg(10, lod));
            if (lod > 0) return;
            for (int i = 0; i < 6; i++)
            {
                double a = 2 * Math.PI * i / 6;
                Shapes.Cylinder(m, SacredDraw.At(f, 0.27 * Math.Sin(a), 0.94 * h, 0.27 * Math.Cos(a)), Looks.Bronze, 0.05, 0.05, 5);
            }
        }
    }
}

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>
    /// A line-up of every carved figure (the five guardian kinds, painted and stone, the Nandi and the Garuda) for the
    /// visual self-check and the figure budgets: figures stand 3 m apart along +x, facing +z, at a common height.
    /// </summary>
    public static class SacredFigureGallery
    {
        /// <summary>The figures, in line-up order.</summary>
        public static readonly string[] Names = { "lion", "wrestler", "elephant", "griffin", "goddess", "lion_painted", "nandi", "garuda" };

        /// <summary>Build figure <paramref name="index"/> (of <see cref="Names"/>) at the origin, <paramref name="h"/> tall
        /// (the Nandi 1.4 × h long), at a LOD and a guardian quality (0 centrepiece, 1 hero, 2 generic, 3 small shrine).
        /// Returns its triangles.</summary>
        public static int Build(int index, double h, int quality, int lod, MeshData m)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = Affine3.Identity;
            switch (index)
            {
                case 6:
                    SacredFigures.Nandi(m, k, 0, 0, 0, 1.4 * h, Looks.Gilt, lod);
                    break;
                case 7:
                    SacredFigures.Garuda(m, k, 0, 0, 0, h, Looks.Stone, lod);
                    break;
                default:
                    GuardianKind kind = index == 5 ? GuardianKind.Lion : (GuardianKind)(index == 0 ? 0 : index == 1 ? 1 : index == 2 ? 2 : index == 3 ? 3 : 4);
                    SacredFigures.Guardian(m, k, 0, 0, 0, h, kind, index == 5, quality, lod, 1);
                    break;
            }
            SacredDraw.Bake(m, v0, i0, 0);
            return m.TriangleCount - t0;
        }
    }
}
