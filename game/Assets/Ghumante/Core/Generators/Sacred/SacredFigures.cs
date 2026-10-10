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
    /// Rounded cartoon figures and the furniture in front of temples (ref_temples 1.5, 1.6): the Newar lion (seated,
    /// chest out, curly mane, open mouth), the elephant with its saddle cloth, the kneeling wrestler with a mace, the
    /// winged griffin, the seated goddess, the Nandi bull, the kneeling Garuda, the hanging temple bell and the lamp
    /// pillar. Each stands in a local frame facing +z (the frame's +w), origin on its pedestal top, scaled by its height.
    /// </summary>
    internal static class SacredFigures
    {
        /// <summary>An ellipsoid part: detail 8 is a smooth cube-sphere (48 triangles), 6 and 4 latitude/longitude
        /// bodies of 24 and 8 triangles.</summary>
        private static void E(MeshData m, in Affine3 b, double x, double y, double z, double rx, double ry, double rz, in ShapeBrush br, int seg)
        {
            bool cube = seg >= 8;
            Shapes.Ellipsoid(m, SacredDraw.At(b, x, y, z), br, rx, ry, rz, cube ? 8 : seg, cube);
        }

        /// <summary>The detail of small parts (paws, eyes, ears) for a body detail.</summary>
        private static int Small(int s)
        {
            return s >= 8 ? 6 : 4;
        }

        /// <summary>A capsule (limb) between two local points.</summary>
        private static void Limb(MeshData m, in Affine3 b, double ax, double ay, double az, double bx, double by, double bz, double r, in ShapeBrush br, int seg)
        {
            double len;
            Affine3 a = Affine3.Along(ax, ay, az, bx, by, bz, out len).Then(b);
            if (len < 1e-6) return;
            a = Affine3.Translation(0, -r, 0).Then(a);
            Shapes.Capsule(m, a, br, r, len + 2 * r, seg >= 8 ? 6 : 4);
        }

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

        /// <summary>A guardian of height h (above its pedestal) at local (u, v, w) of kit transform k, facing +w.</summary>
        public static void Guardian(MeshData m, in Affine3 k, double u, double v, double w, double h, GuardianKind kind, bool painted, bool rich, int lod)
        {
            Affine3 b = SacredDraw.At(k, u, v, w);
            ShapeBrush body = painted ? Looks.Lime : Looks.Stone;
            if (lod >= 2)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(b, 0, 0.45 * h, 0), body, 0.22 * h, 0.45 * h, 0.25 * h, 4);
                return;
            }
            int s = lod == 0 ? (rich ? 8 : 6) : 4;
            switch (kind)
            {
                case GuardianKind.Lion:
                    Lion(m, b, h, body, painted, s, false);
                    break;
                case GuardianKind.Griffin:
                    Lion(m, b, h, body, painted, s, true);
                    break;
                case GuardianKind.Elephant:
                    Elephant(m, b, h, body, s);
                    break;
                case GuardianKind.Wrestler:
                    Wrestler(m, b, h, body, s);
                    break;
                default:
                    Goddess(m, b, h, body, s);
                    break;
            }
        }

        private static void Lion(MeshData m, in Affine3 b, double h, in ShapeBrush body, bool painted, int s, bool griffin)
        {
            ShapeBrush mane = painted ? Looks.PaintGreen : Looks.Ao(body, 0.85f), mouth = Looks.PaintRed;
            // Haunches, upright chest, forelegs.
            E(m, b, 0, 0.19 * h, -0.1 * h, 0.21 * h, 0.18 * h, 0.23 * h, body, s);
            E(m, b, 0, 0.47 * h, 0.02 * h, 0.17 * h, 0.25 * h, 0.16 * h, body, s);
            Limb(m, b, 0.1 * h, 0.05 * h, 0.13 * h, 0.1 * h, 0.42 * h, 0.07 * h, 0.05 * h, body, s);
            Limb(m, b, -0.1 * h, 0.05 * h, 0.13 * h, -0.1 * h, 0.42 * h, 0.07 * h, 0.05 * h, body, s);
            E(m, b, 0.11 * h, 0.035 * h, 0.17 * h, 0.06 * h, 0.035 * h, 0.07 * h, body, Small(s));
            E(m, b, -0.11 * h, 0.035 * h, 0.17 * h, 0.06 * h, 0.035 * h, 0.07 * h, body, Small(s));
            E(m, b, 0.17 * h, 0.05 * h, 0.0, 0.06 * h, 0.05 * h, 0.09 * h, body, Small(s));
            E(m, b, -0.17 * h, 0.05 * h, 0.0, 0.06 * h, 0.05 * h, 0.09 * h, body, Small(s));
            // Head with the curly mane framing the face.
            E(m, b, 0, 0.8 * h, 0.06 * h, 0.15 * h, 0.15 * h, 0.14 * h, body, s);
            Affine3 ring = Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(b, 0, 0.8 * h, 0.04 * h));
            Shapes.Torus(m, ring, mane, 0.155 * h, 0.06 * h, s >= 8 ? 8 : 6, s >= 8 ? 4 : 3);
            if (griffin)
            {
                // Beak, horns and wings.
                Shapes.Cone(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(b, 0, 0.77 * h, 0.17 * h)), body, 0.06 * h, 0.16 * h, 5);
                Limb(m, b, 0.06 * h, 0.92 * h, 0.04 * h, 0.1 * h, 1.04 * h, -0.04 * h, 0.022 * h, mane, 4);
                Limb(m, b, -0.06 * h, 0.92 * h, 0.04 * h, -0.1 * h, 1.04 * h, -0.04 * h, 0.022 * h, mane, 4);
                E(m, b, 0.17 * h, 0.55 * h, -0.12 * h, 0.04 * h, 0.2 * h, 0.12 * h, Looks.Ao(body, 0.9f), s);
                E(m, b, -0.17 * h, 0.55 * h, -0.12 * h, 0.04 * h, 0.2 * h, 0.12 * h, Looks.Ao(body, 0.9f), s);
            }
            else
            {
                E(m, b, 0, 0.75 * h, 0.17 * h, 0.09 * h, 0.065 * h, 0.07 * h, body, s);
                E(m, b, 0, 0.7 * h, 0.2 * h, 0.065 * h, 0.028 * h, 0.04 * h, mouth, Small(s));
            }
            if (s >= 8)
            {
                E(m, b, 0.06 * h, 0.85 * h, 0.17 * h, 0.025 * h, 0.025 * h, 0.015 * h, Looks.Ink, Small(s));
                E(m, b, -0.06 * h, 0.85 * h, 0.17 * h, 0.025 * h, 0.025 * h, 0.015 * h, Looks.Ink, Small(s));
                Path3 t = P;
                t.Add(0, 0.12 * h, -0.3 * h).Add(0, 0.3 * h, -0.4 * h).Add(0, 0.48 * h, -0.34 * h).Add(0, 0.55 * h, -0.24 * h);
                Shapes.Tube(m, b, body, t, 0.03 * h, 4, true, false, default, 0.05 * h);
            }
        }

        private static void Elephant(MeshData m, in Affine3 b, double h, in ShapeBrush body, int s)
        {
            E(m, b, 0, 0.56 * h, -0.06 * h, 0.27 * h, 0.25 * h, 0.4 * h, body, s);
            E(m, b, 0, 0.72 * h, 0.36 * h, 0.18 * h, 0.21 * h, 0.17 * h, body, s);
            E(m, b, 0.19 * h, 0.72 * h, 0.3 * h, 0.035 * h, 0.17 * h, 0.13 * h, Looks.Ao(body, 0.9f), Small(s));
            E(m, b, -0.19 * h, 0.72 * h, 0.3 * h, 0.035 * h, 0.17 * h, 0.13 * h, Looks.Ao(body, 0.9f), Small(s));
            Path3 t = P;
            t.Add(0, 0.62 * h, 0.5 * h).Add(0, 0.4 * h, 0.58 * h).Add(0, 0.16 * h, 0.6 * h).Add(0, 0.1 * h, 0.68 * h);
            Shapes.Tube(m, b, body, t, 0.07 * h, 6, true, false, default, 0.035 * h);
            Limb(m, b, 0.08 * h, 0.56 * h, 0.46 * h, 0.13 * h, 0.44 * h, 0.62 * h, 0.022 * h, Looks.Lime, 4);
            Limb(m, b, -0.08 * h, 0.56 * h, 0.46 * h, -0.13 * h, 0.44 * h, 0.62 * h, 0.022 * h, Looks.Lime, 4);
            for (int q = 0; q < 4; q++)
            {
                double x = (q % 2 == 0 ? 1 : -1) * 0.15 * h, z = (q < 2 ? 1 : -1) * 0.22 * h;
                Shapes.Cylinder(m, SacredDraw.At(b, x, 0, z), body, 0.08 * h, 0.42 * h, 6, 0.02 * h, 1);
            }
            // Saddle cloth with a gilt edge and a small rider's seat.
            Shapes.RoundedBox(m, SacredDraw.At(b, 0, 0.8 * h, -0.06 * h), Looks.PaintRed, 0.58 * h, 0.05 * h, 0.42 * h, 0.02 * h, 1);
            Shapes.RoundedBox(m, SacredDraw.At(b, 0, 0.86 * h, -0.06 * h), Looks.Gilt, 0.24 * h, 0.09 * h, 0.22 * h, 0.03 * h, 1);
            if (s >= 8) E(m, b, 0, 0.7 * h, 0.5 * h, 0.03 * h, 0.03 * h, 0.02 * h, Looks.Ink, Small(s));
        }

        private static void Wrestler(MeshData m, in Affine3 b, double h, in ShapeBrush body, int s)
        {
            // Half-kneeling heavy figure: belly, chest, head with a crown, a mace held upright.
            E(m, b, 0, 0.2 * h, 0, 0.2 * h, 0.17 * h, 0.17 * h, body, s);
            E(m, b, 0, 0.45 * h, 0.02 * h, 0.22 * h, 0.2 * h, 0.17 * h, body, s);
            E(m, b, 0, 0.71 * h, 0.04 * h, 0.12 * h, 0.13 * h, 0.12 * h, body, s);
            Shapes.Frustum(m, SacredDraw.At(b, 0, 0.8 * h, 0.03 * h), Looks.Ao(body, 0.9f), 0.11 * h, 0.06 * h, 0.14 * h, 6);
            Limb(m, b, 0.12 * h, 0.12 * h, 0.12 * h, 0.13 * h, 0.03 * h, 0.26 * h, 0.065 * h, body, s);
            Limb(m, b, -0.13 * h, 0.12 * h, 0.04 * h, -0.14 * h, 0.03 * h, -0.12 * h, 0.065 * h, body, s);
            Limb(m, b, 0.22 * h, 0.55 * h, 0.02 * h, 0.2 * h, 0.35 * h, 0.16 * h, 0.055 * h, body, s);
            Limb(m, b, -0.22 * h, 0.55 * h, 0.02 * h, -0.2 * h, 0.35 * h, 0.16 * h, 0.055 * h, body, s);
            Shapes.Cylinder(m, SacredDraw.At(b, 0.2 * h, 0.08 * h, 0.2 * h), Looks.Ao(body, 0.85f), 0.025 * h, 0.62 * h, 5);
            E(m, b, 0.2 * h, 0.72 * h, 0.2 * h, 0.06 * h, 0.08 * h, 0.06 * h, Looks.Ao(body, 0.85f), Small(s));
            if (s >= 8)
            {
                E(m, b, 0.045 * h, 0.73 * h, 0.15 * h, 0.02 * h, 0.02 * h, 0.012 * h, Looks.Ink, Small(s));
                E(m, b, -0.045 * h, 0.73 * h, 0.15 * h, 0.02 * h, 0.02 * h, 0.012 * h, Looks.Ink, Small(s));
            }
        }

        private static void Goddess(MeshData m, in Affine3 b, double h, in ShapeBrush body, int s)
        {
            // Seated on a lion-throne: crossed legs, slim torso, a tall gilt crown, one arm raised.
            E(m, b, 0, 0.11 * h, 0.02 * h, 0.24 * h, 0.1 * h, 0.17 * h, body, s);
            E(m, b, 0, 0.36 * h, 0, 0.13 * h, 0.2 * h, 0.1 * h, body, s);
            E(m, b, 0, 0.64 * h, 0.02 * h, 0.09 * h, 0.1 * h, 0.09 * h, body, s);
            Shapes.Cone(m, SacredDraw.At(b, 0, 0.71 * h, 0.01 * h), Looks.Gilt, 0.085 * h, 0.22 * h, 6);
            Limb(m, b, 0.14 * h, 0.48 * h, 0, 0.22 * h, 0.66 * h, 0.06 * h, 0.035 * h, body, s);
            Limb(m, b, -0.14 * h, 0.48 * h, 0, -0.16 * h, 0.25 * h, 0.1 * h, 0.035 * h, body, s);
            Shapes.Cylinder(m, SacredDraw.At(b, 0.22 * h, 0.55 * h, 0.06 * h), Looks.Ao(body, 0.85f), 0.018 * h, 0.4 * h, 4);
            Shapes.RoundedSlab(m, SacredDraw.At(b, 0, 0.82 * h, -0.07 * h), Looks.Ao(Looks.Gilt, 0.9f), 0.32 * h, 0.03 * h, 0.04 * h, 0.01, 0.005, 1);
        }

        /// <summary>A gilt Nandi bull lying on a plinth, facing +w, length l.</summary>
        public static void Nandi(MeshData m, in Affine3 k, double u, double v, double w, double l, in ShapeBrush b, int lod)
        {
            Affine3 f = SacredDraw.At(k, u, v, w);
            int s = lod == 0 ? 8 : 4;
            E(m, f, 0, 0.26 * l, -0.05 * l, 0.22 * l, 0.22 * l, 0.42 * l, b, s);
            E(m, f, 0, 0.47 * l, 0.12 * l, 0.12 * l, 0.1 * l, 0.12 * l, b, Small(s));
            E(m, f, 0, 0.5 * l, 0.42 * l, 0.11 * l, 0.12 * l, 0.15 * l, b, s);
            E(m, f, 0, 0.42 * l, 0.55 * l, 0.075 * l, 0.06 * l, 0.07 * l, Looks.Ao(b, 0.9f), Small(s));
            Limb(m, f, 0.07 * l, 0.6 * l, 0.42 * l, 0.15 * l, 0.72 * l, 0.36 * l, 0.022 * l, b, 4);
            Limb(m, f, -0.07 * l, 0.6 * l, 0.42 * l, -0.15 * l, 0.72 * l, 0.36 * l, 0.022 * l, b, 4);
            Limb(m, f, 0.14 * l, 0.06 * l, 0.25 * l, 0.12 * l, 0.06 * l, 0.48 * l, 0.05 * l, b, 4);
            Limb(m, f, -0.14 * l, 0.06 * l, 0.25 * l, -0.12 * l, 0.06 * l, 0.48 * l, 0.05 * l, b, 4);
        }

        /// <summary>A kneeling Garuda (folded hands, wings, beaked face) of height h, facing +w.</summary>
        public static void Garuda(MeshData m, in Affine3 k, double u, double v, double w, double h, in ShapeBrush b, int lod)
        {
            Affine3 f = SacredDraw.At(k, u, v, w);
            int s = lod == 0 ? 8 : 4;
            E(m, f, 0, 0.18 * h, 0, 0.17 * h, 0.15 * h, 0.2 * h, b, s);
            E(m, f, 0, 0.45 * h, 0.02 * h, 0.15 * h, 0.2 * h, 0.13 * h, b, s);
            E(m, f, 0, 0.72 * h, 0.05 * h, 0.1 * h, 0.11 * h, 0.1 * h, b, s);
            Shapes.Cone(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(f, 0, 0.69 * h, 0.13 * h)), b, 0.04 * h, 0.12 * h, 5);
            Shapes.Cone(m, SacredDraw.At(f, 0, 0.8 * h, 0.04 * h), Looks.Gilt, 0.08 * h, 0.16 * h, 6);
            E(m, f, 0.16 * h, 0.55 * h, -0.12 * h, 0.035 * h, 0.26 * h, 0.15 * h, Looks.Ao(b, 0.9f), s);
            E(m, f, -0.16 * h, 0.55 * h, -0.12 * h, 0.035 * h, 0.26 * h, 0.15 * h, Looks.Ao(b, 0.9f), s);
            E(m, f, 0, 0.5 * h, 0.17 * h, 0.05 * h, 0.08 * h, 0.04 * h, b, Small(s));
        }

        /// <summary>A bronze temple bell hung from a beam on two stone posts (and a small tile roof when
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
                Shapes.Frustum(m, SacredDraw.At(f, 0, hgt + 0.16, 0), Looks.Tile, 0.75 * span + 0.4, 0.15, 0.55 + 0.2 * d, 4);
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
