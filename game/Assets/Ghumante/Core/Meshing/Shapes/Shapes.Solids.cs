using System;

namespace Ghumante.Core.Meshing.Shapes
{
    /// <summary>Rounding radius per box side for <see cref="Shapes.RoundedBox(MeshData, in Affine3, in ShapeBrush, double, double, double, in BoxRadii, int, ShapeLod)"/>:
    /// the extent of the rounding measured along each axis as it approaches the −/+ face. Equal radii on two axes give
    /// circular edges; unequal ones give elliptical edges (a car roof that rounds wide but drops sharply); a near-zero
    /// radius keeps that side's edges crisp (a flat-bottomed seat).</summary>
    public struct BoxRadii
    {
        public double XNeg, XPos, YNeg, YPos, ZNeg, ZPos;

        public BoxRadii(double xNeg, double xPos, double yNeg, double yPos, double zNeg, double zPos)
        {
            XNeg = xNeg;
            XPos = xPos;
            YNeg = yNeg;
            YPos = yPos;
            ZNeg = zNeg;
            ZPos = zPos;
        }

        /// <summary>The same radius everywhere.</summary>
        public static BoxRadii All(double r)
        {
            return new BoxRadii(r, r, r, r, r, r);
        }

        /// <summary>Sides (x, z) round with <paramref name="side"/>; the top and bottom faces approach with
        /// <paramref name="top"/> and <paramref name="bottom"/>.</summary>
        public static BoxRadii Vertical(double side, double top, double bottom)
        {
            return new BoxRadii(side, side, bottom, top, side, side);
        }
    }

    /// <summary>
    /// The rounded-geometry library (docs/W2_DETAIL_CONTRACT.md §4): engine-free, deterministic primitives that
    /// append to a <see cref="MeshData"/> through an <see cref="Affine3"/> with a <see cref="ShapeBrush"/> (tint,
    /// material channel and AO in UV0). Every emitter returns the index of its first vertex (its vertices run to
    /// <c>m.VertexCount</c>), writes Unity's winding (front clockwise seen from the front, checked against the
    /// normals, so mirroring transforms are safe), smooth normals unless a crease is asked for, and scales its
    /// segment counts with a <see cref="ShapeLod"/>. Nothing allocates once the per-thread scratch has grown.
    /// Conventions: boxes, spheres, superellipsoids and tori are centred on the origin; cylinders, cones, capsules,
    /// lathes and extrudes stand on y = 0 along +Y.
    /// </summary>
    public static partial class Shapes
    {
        // ------------------------------------------------------------------ rounded box

        /// <summary>A box of size sx × sy × sz centred on the origin with every edge and corner rounded by
        /// <paramref name="radius"/> (<paramref name="segments"/> segments per 90° edge arc; 1 gives a bevel).</summary>
        public static int RoundedBox(MeshData m, in Affine3 xf, in ShapeBrush b, double sx, double sy, double sz, double radius,
                                     int segments, ShapeLod lod = default)
        {
            return RoundedBox(m, xf, b, sx, sy, sz, BoxRadii.All(radius), segments, lod);
        }

        /// <summary>A box of size sx × sy × sz centred on the origin with per-side rounding radii (see
        /// <see cref="BoxRadii"/>). Flat faces are single quads; only the rounded bands carry segments.</summary>
        public static int RoundedBox(MeshData m, in Affine3 xf, in ShapeBrush b, double sx, double sy, double sz, in BoxRadii radii,
                                     int segments, ShapeLod lod = default)
        {
            int first = m.VertexCount;
            int seg = lod.Bevel(Math.Max(1, segments));
            int n = Math.Max(1, (seg + 1) / 2); // steps per half arc (each face owns half of every edge arc)
            int cnt = 2 * n + 2;
            // Tiny scratch: [0..2] half sizes, [3..5] radii towards −, [6..8] towards +, [9..11] e, [12..14] q, [15..17] r.
            double[] w = ShapeEmit.Tiny;
            w[0] = sx * 0.5;
            w[1] = sy * 0.5;
            w[2] = sz * 0.5;
            w[3] = radii.XNeg;
            w[4] = radii.YNeg;
            w[5] = radii.ZNeg;
            w[6] = radii.XPos;
            w[7] = radii.YPos;
            w[8] = radii.ZPos;
            double minR = 1e-4 * Math.Max(Math.Max(sx, sy), Math.Max(sz, 1e-3));
            for (int a = 0; a < 3; a++)
            {
                if (w[3 + a] < minR) w[3 + a] = minR;
                if (w[6 + a] < minR) w[6 + a] = minR;
                double sum = w[3 + a] + w[6 + a], cap = 2 * w[a] * 0.999;
                if (sum > cap)
                {
                    w[3 + a] *= cap / sum;
                    w[6 + a] *= cap / sum;
                }
            }
            // Per-axis samples: inner-box coordinate q, unit-free offset e (tan of the arc angle) and radius r.
            for (int a = 0; a < 3; a++)
            {
                double[] q = ShapeEmit.Scratch(3 * a, cnt), e = ShapeEmit.Scratch(3 * a + 1, cnt), r = ShapeEmit.Scratch(3 * a + 2, cnt);
                for (int k = 0; k <= n; k++)
                {
                    double t = k == 0 ? -1 : k == n ? 0 : -Math.Tan(Math.PI * 0.25 * (n - k) / n);
                    q[k] = -w[a] + w[3 + a];
                    e[k] = t;
                    r[k] = w[3 + a];
                    double t2 = k == 0 ? 0 : k == n ? 1 : Math.Tan(Math.PI * 0.25 * k / n);
                    q[n + 1 + k] = w[a] - w[6 + a];
                    e[n + 1 + k] = t2;
                    r[n + 1 + k] = w[6 + a];
                }
            }
            for (int face = 0; face < 6; face++)
            {
                int a = face >> 1;
                int sgn = (face & 1) == 0 ? -1 : 1;
                int u = (a + 1) % 3, v = (a + 2) % 3;
                int fv = m.VertexCount;
                m.Reserve(cnt * cnt, 0);
                double[] qu = ShapeEmit.Scratch(3 * u, cnt), eu = ShapeEmit.Scratch(3 * u + 1, cnt), ru = ShapeEmit.Scratch(3 * u + 2, cnt);
                double[] qv = ShapeEmit.Scratch(3 * v, cnt), ev = ShapeEmit.Scratch(3 * v + 1, cnt), rv = ShapeEmit.Scratch(3 * v + 2, cnt);
                w[9 + a] = sgn;
                w[12 + a] = sgn > 0 ? w[a] - w[6 + a] : -w[a] + w[3 + a];
                w[15 + a] = sgn > 0 ? w[6 + a] : w[3 + a];
                for (int i = 0; i < cnt; i++)
                {
                    w[9 + u] = eu[i];
                    w[12 + u] = qu[i];
                    w[15 + u] = ru[i];
                    for (int j = 0; j < cnt; j++)
                    {
                        w[9 + v] = ev[j];
                        w[12 + v] = qv[j];
                        w[15 + v] = rv[j];
                        // Canonical x, y, z order so the shared border vertices of two faces match bit for bit.
                        double l = Math.Sqrt(w[9] * w[9] + w[10] * w[10] + w[11] * w[11]);
                        double dx = w[9] / l, dy = w[10] / l, dz = w[11] / l;
                        double nx = dx / w[15], ny = dy / w[16], nz = dz / w[17];
                        ShapeEmit.Normalize(ref nx, ref ny, ref nz);
                        ShapeEmit.Vertex(m, xf, b, w[12] + w[15] * dx, w[13] + w[16] * dy, w[14] + w[17] * dz, nx, ny, nz);
                    }
                }
                ShapeEmit.Grid(m, fv, cnt, cnt, false, false, null, null);
            }
            return first;
        }

        private static void Put(int axis, double p, double n, ref double px, ref double py, ref double pz, ref double nx, ref double ny, ref double nz)
        {
            if (axis == 0)
            {
                px = p;
                nx = n;
            }
            else if (axis == 1)
            {
                py = p;
                ny = n;
            }
            else
            {
                pz = p;
                nz = n;
            }
        }

        // ------------------------------------------------------------------ superellipsoid family

        /// <summary>
        /// A superellipsoid (superquadric) with radii rx, ry, rz centred on the origin: <paramref name="eVertical"/>
        /// shapes the silhouette seen from the side and <paramref name="eHorizontal"/> the cross-section seen from
        /// above (1 = round, towards 0.1 = boxy with rounded edges, 2 = diamond). Built on a cube-sphere grid
        /// (<paramref name="segments"/> around the equator), so there are no pole pinches: ideal for cartoon car
        /// bodies, heads, animal bodies, cushions and fruit.
        /// </summary>
        public static int Superellipsoid(MeshData m, in Affine3 xf, in ShapeBrush b, double rx, double ry, double rz,
                                         double eVertical, double eHorizontal, int segments, ShapeLod lod = default)
        {
            int first = m.VertexCount;
            double e1 = Clamp(eVertical, 0.1, 2.0), e2 = Clamp(eHorizontal, 0.1, 2.0);
            int seg = lod.Radial(Math.Max(8, segments));
            int n = Math.Max(2, (seg + 3) / 4);
            int cnt = n + 1;
            double[] t = ShapeEmit.Scratch(0, cnt);
            // Equal-angle cube-sphere samples; the signed-power map below packs them towards the rounded edges of
            // boxy shapes by itself.
            for (int k = 0; k <= n / 2; k++)
            {
                double w = k == 0 ? -1 : Math.Tan(Math.PI * 0.25 * (-1 + 2.0 * k / n));
                t[k] = w;
                t[n - k] = -w;
            }
            if (n % 2 == 0) t[n / 2] = 0;
            rx = Math.Max(rx, 1e-6);
            ry = Math.Max(ry, 1e-6);
            rz = Math.Max(rz, 1e-6);
            for (int face = 0; face < 6; face++)
            {
                int a = face >> 1;
                int sgn = (face & 1) == 0 ? -1 : 1;
                int u = (a + 1) % 3, v = (a + 2) % 3;
                int fv = m.VertexCount;
                m.Reserve(cnt * cnt, 0);
                for (int i = 0; i < cnt; i++)
                {
                    for (int j = 0; j < cnt; j++)
                    {
                        double dx = 0, dy = 0, dz = 0, ignore = 0;
                        Put(a, sgn, 0, ref dx, ref dy, ref dz, ref ignore, ref ignore, ref ignore);
                        Put(u, t[i], 0, ref dx, ref dy, ref dz, ref ignore, ref ignore, ref ignore);
                        Put(v, t[j], 0, ref dx, ref dy, ref dz, ref ignore, ref ignore, ref ignore);
                        double l = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        dx /= l;
                        dy /= l;
                        dz /= l;
                        double px, py, pz, nx, ny, nz;
                        SuperPoint(dx, dy, dz, rx, ry, rz, e1, e2, out px, out py, out pz, out nx, out ny, out nz);
                        ShapeEmit.Vertex(m, xf, b, px, py, pz, nx, ny, nz);
                    }
                }
                ShapeEmit.Grid(m, fv, cnt, cnt, false, false, null, null);
            }
            return first;
        }

        /// <summary>
        /// Map the unit-sphere point d onto the superellipsoid by signed powers (the classic superquadric
        /// parameterisation with the sphere point standing in for the two angles, so there are no poles) and return
        /// the outward normal (the gradient of the inside-outside function).
        /// </summary>
        private static void SuperPoint(double dx, double dy, double dz, double rx, double ry, double rz, double e1, double e2,
                                       out double px, out double py, out double pz, out double nx, out double ny, out double nz)
        {
            double h = Math.Sqrt(dx * dx + dz * dz);
            double hv = Math.Pow(h, e1);
            px = h > 1e-12 ? rx * hv * SignedPow(dx / h, e2) : 0;
            pz = h > 1e-12 ? rz * hv * SignedPow(dz / h, e2) : 0;
            py = ry * SignedPow(dy, e1);
            // Gradient of F = (|x/rx|^(2/e2) + |z/rz|^(2/e2))^(e2/e1) + |y/ry|^(2/e1), common factor 2/e1 dropped.
            double qx = Math.Abs(px / rx), qy = Math.Abs(py / ry), qz = Math.Abs(pz / rz);
            double g = Math.Pow(qx, 2 / e2) + Math.Pow(qz, 2 / e2);
            double k = g > 1e-12 ? Math.Pow(g, e2 / e1 - 1) : 0;
            nx = k * Math.Pow(qx, 2 / e2 - 1) * Math.Sign(px) / rx;
            nz = k * Math.Pow(qz, 2 / e2 - 1) * Math.Sign(pz) / rz;
            ny = Math.Pow(qy, 2 / e1 - 1) * Math.Sign(py) / ry;
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (double.IsNaN(l) || double.IsInfinity(l) || l < 1e-300)
            {
                nx = dx / rx;
                ny = dy / ry;
                nz = dz / rz;
                l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            }
            nx /= l;
            ny /= l;
            nz /= l;
        }

        private static double SignedPow(double v, double e)
        {
            return v < 0 ? -Math.Pow(-v, e) : Math.Pow(v, e);
        }

        /// <summary>A sphere on a cube-sphere grid (even triangles, no poles), centred on the origin.</summary>
        public static int CubeSphere(MeshData m, in Affine3 xf, in ShapeBrush b, double radius, int segments, ShapeLod lod = default)
        {
            return Superellipsoid(m, xf, b, radius, radius, radius, 1, 1, segments, lod);
        }

        /// <summary>An ellipsoid with radii rx, ry, rz centred on the origin; <paramref name="cubeSphere"/> picks the
        /// pole-free grid, otherwise latitude/longitude rings (<see cref="Sphere"/>).</summary>
        public static int Ellipsoid(MeshData m, in Affine3 xf, in ShapeBrush b, double rx, double ry, double rz, int segments,
                                    bool cubeSphere = true, ShapeLod lod = default)
        {
            if (cubeSphere) return Superellipsoid(m, xf, b, rx, ry, rz, 1, 1, segments, lod);
            Affine3 s = xf * Affine3.Scaling(rx, ry, rz);
            return Sphere(m, s, b, 1, segments, lod);
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }
    }
}
