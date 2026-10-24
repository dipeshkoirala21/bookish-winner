using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Pashupati
{
    /// <summary>The flat carved silhouettes of the Pashupatinath kit.</summary>
    internal enum FigureKind : byte
    {
        /// <summary>The kirtimukha (face of glory) mask at the apex of a torana: a horned, round-eyed face.</summary>
        Kirtimukha = 4,
    }

    /// <summary>
    /// Figures for the Pashupatinath site. The deities, guardians and attendants are modelled round forms (ellipsoids,
    /// tapered limbs, lathed skirts and crowns) half sunk in their ground, so they catch light as relief does: the
    /// blackened bronze deities of the temple's wall panels with their gilt crowns and aureoles, the small seated figures
    /// of the band under the heads frieze, and the gilt guardians in the door niches in the round. The kirtimukha mask is
    /// a carved silhouette (an outline in the face plane, the right half in height units, mirrored) extruded a few
    /// centimetres proud with its two bulging eyes. Frames are face frames (x along the face, y up, z out).
    /// Allocation-free after warm-up.
    /// </summary>
    internal static class PashupatiFigures
    {
        private static readonly double[] KirtimukhaHalf =
        {
            0, 0.78, 0.12, 0.88, 0.22, 1, 0.26, 0.86, 0.4, 0.84, 0.48, 0.72, 0.46, 0.56, 0.54, 0.46, 0.42, 0.36, 0.3, 0.16, 0.16, 0.06, 0, 0,
        };

        [ThreadStatic] private static double[] _x, _z;

        /// <summary>The widest point of an outline (its x in height units).</summary>
        private static double MaxX(double[] half)
        {
            double m = 1e-6;
            for (int i = 0; i < half.Length; i += 2) m = Math.Max(m, half[i]);
            return m;
        }

        /// <summary>
        /// A carved silhouette on face frame <paramref name="x"/>: its foot at (u, v), <paramref name="h"/> tall and
        /// <paramref name="halfW"/> wide, standing <paramref name="depth"/> proud of the plane z = <paramref name="z"/>; the
        /// kirtimukha's eyes bulge at LOD0. LOD2 and beyond nothing. Returns the first vertex.
        /// </summary>
        public static int Relief(MeshData m, in Affine3 x, FigureKind kind, double u, double v, double z, double h, double halfW, double depth,
                                 in ShapeBrush b, int lod)
        {
            int first = m.VertexCount;
            if (lod >= 2) return first;
            double[] half = KirtimukhaHalf;
            int nh = half.Length / 2;
            if (_x == null)
            {
                _x = new double[64];
                _z = new double[64];
            }
            double sx = halfW / MaxX(half);
            // Right half top to bottom, then the left half bottom to top (skipping the two centre points).
            int n = 0;
            for (int i = 0; i < nh; i++)
            {
                _x[n] = half[2 * i] * sx;
                _z[n] = -half[2 * i + 1] * h;
                n++;
            }
            int right = n;
            for (int i = right - 2; i >= 1; i--)
            {
                _x[n] = -_x[i];
                _z[n] = _z[i];
                n++;
            }
            // Basis: x along the face, extrusion y out of the face (+z), the polygon's z = −height.
            Affine3 f = SacredDraw.Basis(x, u, v, z, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, f, b, _x, _z, n, depth, 0, 0, BevelStyle.Chamfer, true, false);
            if (lod != 0) return first;
            // The mask's two round bulging eyes.
            for (int s = -1; s <= 1; s += 2)
                Shapes.Ellipsoid(m, SacredDraw.At(x, u + s * 0.22 * halfW, v + 0.6 * h, z + depth), b, 0.12 * halfW, 0.1 * h, 0.5 * depth + 0.01, 6, false);
            return first;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Modelled relief figures (round forms half sunk in their ground)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A standing four-armed deity in modelled relief on face frame <paramref name="x"/> (the ground storey's blackened
        /// bronze repoussé panels, c_inner/c_01, c_gje/c_03, night/ref_05): feet on a lotus at (u, v), <paramref name="h"/>
        /// tall, standing 6-10 cm proud of the plane z: the lotus, the legs in the dhoti with its folds, the slim torso and
        /// broad shoulders, the four arms (the upper pair raised beside the head with their attributes, the lower pair out
        /// to the hips, one in blessing), the head with its face and a tall gilt crown, and the gilt aureole ring behind
        /// it. The body in <paramref name="bronze"/>, crown, attributes, necklace and aureole in <paramref name="gilt"/>.
        /// LOD0 about 330 triangles; LOD1 a few masses (torso, head, crown, the dhoti); LOD2 nothing.
        /// </summary>
        public static void Deity(MeshData m, in Affine3 x, double u, double v, double z, double h, in ShapeBrush bronze, in ShapeBrush gilt, int lod)
        {
            if (lod >= 2) return;
            const double D = 0.6; // depth scale of the round forms (relief, not in the round)
            Affine3 k = SacredDraw.At(x, u, v, z);
            if (lod == 1)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.36 * h, 0.02), bronze, 0.12 * h, 0.3 * h, 0.05, 5, false);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.66 * h, 0.03), bronze, 0.13 * h, 0.12 * h, 0.06, 5, false);
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.8 * h, 0.04), bronze, 0.055 * h, 0.065 * h, 0.05, 5, false);
                Shapes.Cone(m, SacredDraw.At(k, 0, 0.84 * h, 0.04), gilt, 0.06 * h, 0.13 * h, 4, 0, 0, false);
                return;
            }
            int seg = 5;
            // The lotus pedestal and the aureole ring (gilt) behind the head.
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.02 * h, 0.01), gilt, 0.16 * h, 0.035 * h, D * 0.08 * h, seg, false);
            Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(k, 0, 0.81 * h, 0.005)), gilt, 0.12 * h, 0.012 * h, 10, 3);
            // Legs and feet under the dhoti.
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Shapes.Bar(m, k, bronze, sg * 0.045 * h, 0.05 * h, 0.03, sg * 0.05 * h, 0.3 * h, 0.035, 0.028 * h, 4);
                Shapes.Ellipsoid(m, SacredDraw.At(k, sg * 0.055 * h, 0.05 * h, 0.05), bronze, 0.035 * h, 0.018 * h, 0.03, 5, false);
            }
            // The dhoti: a flared skirt (flattened lathe) from the waist to the shins, three folds down its front.
            PashupatiKit.LatheOf(m, Affine3.Scaling(1, 1, D * 0.5).Then(SacredDraw.At(k, 0, 0.22 * h, 0.01)), bronze,
                                 new[] { 0.0, 0.0, 0.13 * h, 0.0, 0.12 * h, 0.12 * h, 0.095 * h, 0.27 * h, 0.08 * h, 0.32 * h, 0.0, 0.32 * h }, 6, 0, 0b10_0011);
            for (int q = -1; q <= 1; q++)
                Shapes.Bar(m, k, bronze, q * 0.05 * h, 0.24 * h, D * 0.13 * h * 0.5 + 0.015, q * 0.035 * h, 0.5 * h, D * 0.1 * h * 0.5 + 0.015, 0.008 * h, 3);
            // Torso, shoulders and the gilt necklace.
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.6 * h, 0.02), bronze, 0.085 * h, 0.12 * h, D * 0.07 * h, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.7 * h, 0.02), bronze, 0.13 * h, 0.045 * h, D * 0.06 * h, seg, false);
            Shapes.Torus(m, Affine3.Scaling(1, 1, 0.5).Then(Affine3.RotationX(0.35)).Then(SacredDraw.At(k, 0, 0.66 * h, 0.03 + D * 0.05 * h)), gilt, 0.05 * h,
                         0.008 * h, 8, 3);
            // The four arms: the lower pair out to the hips (one hand raised in blessing), the upper pair up beside the head
            // holding the trident (left) and the lotus or disc (right).
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Shapes.Bar(m, k, bronze, sg * 0.11 * h, 0.69 * h, 0.03, sg * 0.17 * h, 0.52 * h, 0.04, 0.024 * h, 4);
                Shapes.Bar(m, k, bronze, sg * 0.17 * h, 0.52 * h, 0.04, sg * 0.15 * h, sg < 0 ? 0.42 * h : 0.6 * h, 0.05, 0.02 * h, 4);
                Shapes.Bar(m, k, bronze, sg * 0.1 * h, 0.71 * h, 0.02, sg * 0.21 * h, 0.76 * h, 0.03, 0.02 * h, 4);
                Shapes.Bar(m, k, bronze, sg * 0.21 * h, 0.76 * h, 0.03, sg * 0.23 * h, 0.88 * h, 0.035, 0.017 * h, 4);
            }
            Shapes.Bar(m, k, gilt, -0.24 * h, 0.66 * h, 0.05, -0.24 * h, 1.02 * h, 0.05, 0.007 * h, 3);
            for (int sg = -1; sg <= 1; sg += 2)
                Shapes.Bar(m, k, gilt, -0.24 * h, 0.97 * h, 0.05, -0.24 * h + sg * 0.035 * h, 1.02 * h, 0.05, 0.006 * h, 3);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0.235 * h, 0.91 * h, 0.05), gilt, 0.035 * h, 0.035 * h, 0.02, 5, false);
            // Head with its face (the nose catches the light) and the tall tiered gilt crown with its side flaps.
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.79 * h, 0.03), bronze, 0.05 * h, 0.06 * h, D * 0.05 * h, seg, false);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.78 * h, 0.03 + D * 0.05 * h), bronze, 0.01 * h, 0.018 * h, 0.012, 4, false);
            Shapes.Frustum(m, SacredDraw.At(k, 0, 0.835 * h, 0.03), gilt, 0.055 * h, 0.035 * h, 0.06 * h, 6, 0, 0, false, false);
            Shapes.Cone(m, SacredDraw.At(k, 0, 0.895 * h, 0.03), gilt, 0.035 * h, 0.07 * h, 6, 0, 0, false);
            for (int sg = -1; sg <= 1; sg += 2)
                Shapes.Ellipsoid(m, SacredDraw.At(k, sg * 0.06 * h, 0.86 * h, 0.03), gilt, 0.02 * h, 0.03 * h, 0.012, 4, false);
        }

        /// <summary>A small seated deity in modelled relief (the gilt-framed band of seated figures under the heads frieze,
        /// c_inner/c_01): folded legs, torso, two arms to the knees, head and gilt crown, about 60 triangles; LOD1 the
        /// body and crown only.</summary>
        public static void SeatedSmall(MeshData m, in Affine3 x, double u, double v, double z, double h, in ShapeBrush bronze, in ShapeBrush gilt, int lod)
        {
            if (lod >= 2) return;
            Affine3 k = SacredDraw.At(x, u, v, z);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.14 * h, 0.01), bronze, 0.3 * h, 0.12 * h, 0.04, 4, false);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.45 * h, 0.02), bronze, 0.15 * h, 0.22 * h, 0.04, 4, false);
            if (lod == 0)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 0.76 * h, 0.03), bronze, 0.09 * h, 0.1 * h, 0.035, 4, false);
                for (int sg = -1; sg <= 1; sg += 2) Shapes.Bar(m, k, bronze, sg * 0.13 * h, 0.58 * h, 0.03, sg * 0.2 * h, 0.22 * h, 0.04, 0.035 * h, 3);
            }
            Shapes.Cone(m, SacredDraw.At(k, 0, 0.83 * h, 0.03), gilt, 0.08 * h, 0.2 * h, 4, 0, 0, false);
        }

        /// <summary>
        /// A gilt guardian deity in the round (the niche images either side of each door, c_gje/c_03, nandi/ref_03), about
        /// 1.5 m on a lotus at (u, v) of face frame <paramref name="x"/>, its front toward +z: the lotus and the stepped
        /// base, the legs in the flared dhoti, the torso with its sash and necklace, broad shoulders, four arms (the outer
        /// upper hand raising the mace on the outer side <paramref name="side"/>, the inner upper one a lotus, the lower pair
        /// at the hips), the head with its face, ears and earrings, the tall tiered crown with side flaps and the aureole
        /// ring. About 520 triangles; LOD1 the masses.
        /// </summary>
        public static void Guardian(MeshData m, in Affine3 x, double u, double v, double z, double h, int side, in ShapeBrush gold, in ShapeBrush shade, int lod)
        {
            Affine3 g = SacredDraw.At(x, u, v, z);
            if (lod >= 2) return;
            if (lod == 1)
            {
                Shapes.Frustum(m, g, shade, 0.2 * h, 0.16 * h, 0.08 * h, 6, 0, 0, false, true);
                Shapes.Frustum(m, SacredDraw.At(g, 0, 0.08 * h, 0), gold, 0.15 * h, 0.12 * h, 0.4 * h, 6, 0, 0, false, false);
                Shapes.Ellipsoid(m, SacredDraw.At(g, 0, 0.62 * h, 0), gold, 0.15 * h, 0.17 * h, 0.08, 6, false);
                Shapes.Sphere(m, SacredDraw.At(g, 0, 0.85 * h, 0.01), gold, 0.07 * h, 6);
                Shapes.Cone(m, SacredDraw.At(g, 0, 0.89 * h, 0.01), gold, 0.06 * h, 0.13 * h, 5, 0, 0, false);
                return;
            }
            // Stepped base and lotus.
            Shapes.Frustum(m, g, shade, 0.22 * h, 0.2 * h, 0.04 * h, 6, 0, 0, false, true);
            Shapes.Ellipsoid(m, SacredDraw.At(g, 0, 0.06 * h, 0), gold, 0.19 * h, 0.035 * h, 0.1, 6, false);
            // Legs and feet, the flared dhoti with its hem and a front pleat.
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Shapes.Bar(m, g, gold, sg * 0.05 * h, 0.08 * h, 0.01, sg * 0.055 * h, 0.3 * h, 0.01, 0.032 * h, 5);
                Shapes.Ellipsoid(m, SacredDraw.At(g, sg * 0.06 * h, 0.085 * h, 0.04), gold, 0.035 * h, 0.02 * h, 0.05, 5, false);
            }
            PashupatiKit.LatheOf(m, Affine3.Scaling(1, 1, 0.7).Then(SacredDraw.At(g, 0, 0.22 * h, 0)), shade,
                                 new[] { 0.0, 0.0, 0.15 * h, 0.0, 0.14 * h, 0.1 * h, 0.11 * h, 0.25 * h, 0.095 * h, 0.3 * h, 0.0, 0.3 * h }, 8, 0, 0b10_0011);
            Shapes.Bar(m, g, gold, 0, 0.24 * h, 0.11 * h * 0.7, 0, 0.5 * h, 0.08 * h * 0.7, 0.012 * h, 4);
            // Torso, chest, shoulders, sash and necklace.
            Shapes.Ellipsoid(m, SacredDraw.At(g, 0, 0.6 * h, 0), gold, 0.095 * h, 0.12 * h, 0.07 * h, 6, false);
            Shapes.Ellipsoid(m, SacredDraw.At(g, 0, 0.7 * h, 0), gold, 0.14 * h, 0.05 * h, 0.065 * h, 6, false);
            Shapes.Torus(m, Affine3.RotationX(0.25).Then(SacredDraw.At(g, 0, 0.69 * h, 0.01)), shade, 0.07 * h, 0.01 * h, 10, 3);
            Shapes.Bar(m, g, shade, -0.09 * h, 0.72 * h, 0.06 * h, 0.08 * h, 0.52 * h, 0.07 * h, 0.012 * h, 4);
            // Four arms: lower pair at the hips, upper pair raised; the mace on the outer side, a lotus on the inner.
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Shapes.Bar(m, g, gold, sg * 0.13 * h, 0.7 * h, 0.01, sg * 0.18 * h, 0.54 * h, 0.03, 0.026 * h, 5);
                Shapes.Bar(m, g, gold, sg * 0.18 * h, 0.54 * h, 0.03, sg * 0.14 * h, 0.44 * h, 0.07, 0.022 * h, 5);
                Shapes.Bar(m, g, gold, sg * 0.12 * h, 0.72 * h, -0.01, sg * 0.24 * h, 0.78 * h, 0.0, 0.022 * h, 5);
                Shapes.Bar(m, g, gold, sg * 0.24 * h, 0.78 * h, 0.0, sg * 0.25 * h, 0.9 * h, 0.02, 0.019 * h, 5);
            }
            double mx = side * 0.25 * h;
            Shapes.Bar(m, g, shade, mx, 0.5 * h, 0.03, mx, 1.0 * h, 0.03, 0.012 * h, 4);
            Shapes.Sphere(m, SacredDraw.At(g, mx, 1.02 * h, 0.03), gold, 0.04 * h, 6);
            Shapes.Ellipsoid(m, SacredDraw.At(g, -side * 0.255 * h, 0.94 * h, 0.03), gold, 0.04 * h, 0.03 * h, 0.04 * h, 6, false);
            // Head, face, ears with earrings, and the tall tiered crown with its side flaps.
            Shapes.Ellipsoid(m, SacredDraw.At(g, 0, 0.8 * h, 0.01), gold, 0.055 * h, 0.065 * h, 0.06 * h, 6, false);
            Shapes.Ellipsoid(m, SacredDraw.At(g, 0, 0.79 * h, 0.01 + 0.06 * h), shade, 0.012 * h, 0.02 * h, 0.012 * h, 4, false);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Shapes.Ellipsoid(m, SacredDraw.At(g, sg * 0.024 * h, 0.81 * h, 0.01 + 0.052 * h), PashupatiKit.Ink, 0.01 * h, 0.006 * h, 0.006 * h, 4, false);
                Shapes.Sphere(m, SacredDraw.At(g, sg * 0.058 * h, 0.77 * h, 0.01), shade, 0.014 * h, 4);
                Shapes.Ellipsoid(m, SacredDraw.At(g, sg * 0.065 * h, 0.87 * h, 0.0), gold, 0.025 * h, 0.04 * h, 0.012 * h, 5, false);
            }
            Shapes.Frustum(m, SacredDraw.At(g, 0, 0.845 * h, 0.01), gold, 0.06 * h, 0.045 * h, 0.06 * h, 6, 0, 0, false, false);
            Shapes.Frustum(m, SacredDraw.At(g, 0, 0.905 * h, 0.01), shade, 0.048 * h, 0.03 * h, 0.04 * h, 6, 0, 0, false, false);
            Shapes.Cone(m, SacredDraw.At(g, 0, 0.945 * h, 0.01), gold, 0.03 * h, 0.07 * h, 6, 0, 0, false);
            // The aureole ring behind the head.
            Shapes.Torus(m, Affine3.RotationX(0.5 * Math.PI).Then(SacredDraw.At(g, 0, 0.84 * h, -0.07 * h)), gold, 0.13 * h, 0.012 * h, 12, 3);
        }
    }
}
