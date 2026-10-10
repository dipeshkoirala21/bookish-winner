using System;

namespace Ghumante.Core.Meshing.Shapes
{
    public static partial class Shapes
    {
        [ThreadStatic] private static Profile2 s_prof, s_cap;

        private static Profile2 TempProfile
        {
            get { return s_prof ?? (s_prof = new Profile2(64)); }
        }

        private static Profile2 CapProfile
        {
            get { return s_cap ?? (s_cap = new Profile2(64)); }
        }

        // ------------------------------------------------------------------ lathe

        /// <summary>
        /// Revolve a profile about the local Y axis. Profile x = radius (≥ 0), y = height; it runs bottom to top with
        /// the outside on its right (+x), and may be open (a vase, a pot, a stupa dome: start and end on the axis to
        /// close it) or closed (a ring: a torus). Creased profile points give hard rings (a lip, a step), smooth
        /// ones round shading. <paramref name="startDeg"/> and <paramref name="sweepDeg"/> are compass angles
        /// (clockwise from +Z seen from above); a partial sweep can close its two cut faces with
        /// <paramref name="endCaps"/>.
        /// </summary>
        public static int Lathe(MeshData m, in Affine3 xf, in ShapeBrush b, Profile2 profile, int radialSegments, ShapeLod lod = default,
                                double startDeg = 0, double sweepDeg = 360, bool endCaps = false)
        {
            int first = m.VertexCount;
            Expanded e = ShapeEmit.ExpA;
            int s = ShapeEmit.Expand(profile, e);
            if (s < 2) return first;
            bool full = Math.Abs(sweepDeg) >= 359.999;
            int n = lod.Radial(radialSegments);
            if (!full) n = Math.Max(1, (int)Math.Ceiling(n * Math.Abs(sweepDeg) / 360.0));
            int rows = full ? n : n + 1;
            double a0 = startDeg * Math.PI / 180.0, da = (full ? 360.0 : sweepDeg) * Math.PI / 180.0 / n;
            m.Reserve(rows * s, rows * s * 6);
            for (int r = 0; r < rows; r++)
            {
                double ang = a0 + da * r, dx = Math.Sin(ang), dz = Math.Cos(ang);
                for (int k = 0; k < s; k++)
                {
                    double x = e.X[k], nx = e.NX[k] * dx, ny = e.NY[k], nz = e.NX[k] * dz;
                    ShapeEmit.Normalize(ref nx, ref ny, ref nz);
                    ShapeEmit.Vertex(m, xf, b, x * dx, e.Y[k], x * dz, nx, ny, nz);
                }
            }
            ShapeEmit.Grid(m, first, rows, s, profile.Closed, full, e.Seam, null);
            if (!full && endCaps)
            {
                LatheCap(m, xf, b, profile, a0, sweepDeg > 0 ? -1 : 1);
                LatheCap(m, xf, b, profile, a0 + da * n, sweepDeg > 0 ? 1 : -1);
            }
            return first;
        }

        private static void LatheCap(MeshData m, in Affine3 xf, in ShapeBrush b, Profile2 profile, double ang, double dir)
        {
            Profile2 cap = CapProfile.CopyFrom(profile);
            if (!profile.Closed && cap.Count > 0)
            {
                // Close an open profile along the axis.
                if (Math.Abs(cap.X[cap.Count - 1]) > 1e-9) cap.Add(0, cap.Y[cap.Count - 1]);
                if (Math.Abs(cap.X[0]) > 1e-9) cap.Add(0, cap.Y[0]);
            }
            cap.Closed = true;
            int[] tris, idx;
            int pc;
            int tc = ShapeEmit.TriangulateProfile(cap, out tris, out idx, out pc);
            if (tc == 0) return;
            double dx = Math.Sin(ang), dz = Math.Cos(ang);
            // Tangent of increasing compass angle: (cos, 0, -sin).
            double nx = Math.Cos(ang) * dir, nz = -Math.Sin(ang) * dir;
            int v0 = m.VertexCount;
            m.Reserve(pc, tc * 3);
            for (int k = 0; k < pc; k++)
            {
                double x = cap.X[idx[k]];
                ShapeEmit.Vertex(m, xf, b, x * dx, cap.Y[idx[k]], x * dz, nx, 0, nz);
            }
            for (int t = 0; t < tc; t++) ShapeEmit.TriOriented(m, v0 + tris[3 * t], v0 + tris[3 * t + 1], v0 + tris[3 * t + 2]);
        }

        // ------------------------------------------------------------------ cylinders, cones, frusta

        /// <summary>
        /// A frustum standing on y = 0 along +Y: bottom radius <paramref name="rBottom"/>, top radius
        /// <paramref name="rTop"/> (0 = a cone). Optional flat caps; with <paramref name="rimRadius"/> &gt; 0 the
        /// rims where a cap meets the side are rounded with <paramref name="rimSegments"/> segments (a soft
        /// cartoon edge instead of a razor rim).
        /// </summary>
        public static int Frustum(MeshData m, in Affine3 xf, in ShapeBrush b, double rBottom, double rTop, double height,
                                  int radialSegments, double rimRadius = 0, int rimSegments = 0, bool capBottom = true,
                                  bool capTop = true, ShapeLod lod = default)
        {
            Profile2 p = TempProfile.Clear(false);
            if (capBottom && rBottom > 1e-9) p.Add(0, 0, true);
            p.Add(Math.Max(0, rBottom), 0, true);
            p.Add(Math.Max(0, rTop), height, true);
            if (capTop && rTop > 1e-9) p.Add(0, height, true);
            if (rimRadius > 0 && rimSegments > 0) p.FilletCorners(rimRadius, lod.Bevel(rimSegments));
            return Lathe(m, xf, b, p, radialSegments, lod);
        }

        /// <summary>A cylinder of radius r standing on y = 0 (see <see cref="Frustum"/>).</summary>
        public static int Cylinder(MeshData m, in Affine3 xf, in ShapeBrush b, double radius, double height, int radialSegments,
                                   double rimRadius = 0, int rimSegments = 0, bool capBottom = true, bool capTop = true,
                                   ShapeLod lod = default)
        {
            return Frustum(m, xf, b, radius, radius, height, radialSegments, rimRadius, rimSegments, capBottom, capTop, lod);
        }

        /// <summary>A cone of base radius r standing on y = 0 with its apex at y = height (see <see cref="Frustum"/>).</summary>
        public static int Cone(MeshData m, in Affine3 xf, in ShapeBrush b, double radius, double height, int radialSegments,
                               double rimRadius = 0, int rimSegments = 0, bool capBottom = true, ShapeLod lod = default)
        {
            return Frustum(m, xf, b, radius, 0, height, radialSegments, rimRadius, rimSegments, capBottom, false, lod);
        }

        /// <summary>
        /// A capsule standing on y = 0 along +Y: total height <paramref name="height"/> (at least 2r) with
        /// hemispherical ends (limbs, bollards, handlebar grips, sausage-shaped cartoon parts). Use
        /// <see cref="Affine3.Along"/> to run one between two points.
        /// </summary>
        public static int Capsule(MeshData m, in Affine3 xf, in ShapeBrush b, double radius, double height, int radialSegments,
                                  ShapeLod lod = default)
        {
            if (height < 2 * radius) height = 2 * radius;
            int seg = lod.Radial(radialSegments);
            int q = Math.Max(2, seg / 4);
            Profile2 p = TempProfile.Clear(false);
            for (int k = 0; k <= q; k++)
            {
                double phi = -0.5 * Math.PI + 0.5 * Math.PI * k / q, c = k == 0 ? 0 : Math.Cos(phi), s = Math.Sin(phi);
                p.Add(radius * c, radius + radius * s, c, s, false);
            }
            for (int k = 0; k <= q; k++)
            {
                double phi = 0.5 * Math.PI * k / q, c = k == q ? 0 : Math.Cos(phi), s = Math.Sin(phi);
                p.Add(radius * c, height - radius + radius * s, c, s, false);
            }
            return Lathe(m, xf, b, p, radialSegments, lod);
        }

        /// <summary>A latitude/longitude sphere centred on the origin (<paramref name="segments"/> around, half as
        /// many rings). Prefer <see cref="CubeSphere"/> for even triangles; this one shades bands cleanly (domes).</summary>
        public static int Sphere(MeshData m, in Affine3 xf, in ShapeBrush b, double radius, int segments, ShapeLod lod = default)
        {
            int seg = lod.Radial(segments);
            int rings = Math.Max(2, seg / 2);
            Profile2 p = TempProfile.Clear(false);
            for (int k = 0; k <= rings; k++)
            {
                double phi = -0.5 * Math.PI + Math.PI * k / rings;
                double c = k == 0 || k == rings ? 0 : Math.Cos(phi), s = k == 0 ? -1 : k == rings ? 1 : Math.Sin(phi);
                p.Add(radius * c, radius * s, c, s, false);
            }
            return Lathe(m, xf, b, p, segments, lod);
        }

        /// <summary>
        /// A dome standing on y = 0: a half ellipsoid of base radius <paramref name="radius"/> and height
        /// <paramref name="height"/>, optionally closed underneath (stupa domes, helmets, bells, mushroom caps).
        /// </summary>
        public static int Dome(MeshData m, in Affine3 xf, in ShapeBrush b, double radius, double height, int segments,
                               bool capBottom = false, ShapeLod lod = default)
        {
            int seg = lod.Radial(segments);
            int rings = Math.Max(2, seg / 4);
            Profile2 p = TempProfile.Clear(false);
            if (capBottom) p.Add(0, 0, true);
            for (int k = 0; k <= rings; k++)
            {
                double phi = 0.5 * Math.PI * k / rings;
                double c = k == rings ? 0 : Math.Cos(phi), s = k == rings ? 1 : Math.Sin(phi);
                p.Add(radius * c, height * s, c / Math.Max(radius, 1e-9), s / Math.Max(height, 1e-9), k == 0 && capBottom);
            }
            return Lathe(m, xf, b, p, segments, lod);
        }

        /// <summary>
        /// A torus centred on the origin around the Y axis: ring radius <paramref name="majorRadius"/>, tube radius
        /// <paramref name="minorRadius"/>. A partial torus (<paramref name="sweepDeg"/> &lt; 360, compass angles
        /// from <paramref name="startDeg"/>) can cap its cut ends: arches, horseshoe magnets, garlands, mudguards.
        /// </summary>
        public static int Torus(MeshData m, in Affine3 xf, in ShapeBrush b, double majorRadius, double minorRadius, int majorSegments,
                                int minorSegments, ShapeLod lod = default, double startDeg = 0, double sweepDeg = 360, bool endCaps = true)
        {
            Profile2 p = TempProfile.SetCircle(minorRadius, lod.Radial(minorSegments), majorRadius, 0);
            return Lathe(m, xf, b, p, majorSegments, lod, startDeg, sweepDeg, endCaps);
        }
    }
}
