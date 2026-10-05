using System;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// A local building frame: origin <c>O</c> (tile-local metres, absolute Y), a horizontal unit axis <c>U</c> (along
    /// a facade) and its outward horizontal normal <c>W = (U.z, -U.x)</c> (to the right of U seen from above, so a
    /// counter-clockwise footprint edge's W points out of the building). A local point (u, v, w) is
    /// <c>O + u·U + v·up + w·W</c>.
    /// </summary>
    public struct KitFrame
    {
        public double OX, OY, OZ;
        public double UX, UZ;

        public KitFrame(double ox, double oy, double oz, double ux, double uz)
        {
            double l = Math.Sqrt(ux * ux + uz * uz);
            if (l < 1e-12)
            {
                ux = 1;
                uz = 0;
                l = 1;
            }
            OX = ox;
            OY = oy;
            OZ = oz;
            UX = ux / l;
            UZ = uz / l;
        }

        /// <summary>A frame whose W axis points along the compass bearing <paramref name="yawDeg"/> (clockwise from
        /// north = +Z): a door with this yaw faces the frame's +W.</summary>
        public static KitFrame FromYaw(double ox, double oy, double oz, double yawDeg)
        {
            double r = yawDeg * Math.PI / 180.0;
            double wx = Math.Sin(r), wz = Math.Cos(r); // facing direction
            // W = (U.z, -U.x)  =>  U = (-W.z, W.x)
            return new KitFrame(ox, oy, oz, -wz, wx);
        }

        public double WX
        {
            get { return UZ; }
        }

        public double WZ
        {
            get { return -UX; }
        }

        public void ToWorld(double u, double v, double w, out double x, out double y, out double z)
        {
            x = OX + u * UX + w * UZ;
            y = OY + v;
            z = OZ + u * UZ - w * UX;
        }

        /// <summary>The same frame turned 90° clockwise seen from above (U becomes the old W).</summary>
        public KitFrame TurnedRight()
        {
            return new KitFrame(OX, OY, OZ, WX, WZ);
        }

        public KitFrame Offset(double u, double v, double w)
        {
            double x, y, z;
            ToWorld(u, v, w, out x, out y, out z);
            return new KitFrame(x, y, z, UX, UZ);
        }
    }

    /// <summary>Faces of a kit box to draw (bit mask).</summary>
    [Flags]
    public enum BoxFaces : byte
    {
        None = 0,

        /// <summary>The −W face (towards the wall a facade element is fixed to).</summary>
        Back = 1,

        Front = 2,
        Left = 4,
        Right = 8,
        Top = 16,
        Bottom = 32,
        All = 63,

        /// <summary>Everything but the back and bottom: an element fixed to a wall.</summary>
        Wall = Front | Left | Right | Top,
    }

    /// <summary>
    /// Flat-shaded primitives on <see cref="MeshData"/> for the building grammar and the sacred generators: quads,
    /// boxes in a <see cref="KitFrame"/>, prisms, lofts between scaled rings, cylinders, cones and domes. Every face
    /// gets its own vertices (flat normals) and Unity's winding (front clockwise seen from the front). Nothing
    /// allocates beyond <see cref="MeshData"/> growth.
    /// </summary>
    public static class MeshKit
    {
        /// <summary>A planar quad (p0..p3 around its perimeter) facing the side of (hx, hy, hz). Degenerate quads are
        /// skipped. Returns the triangles added.</summary>
        public static int Quad(MeshData m, double x0, double y0, double z0, double x1, double y1, double z1,
                               double x2, double y2, double z2, double x3, double y3, double z3,
                               double hx, double hy, double hz, uint c)
        {
            // Newell normal.
            double nx = (y0 - y1) * (z0 + z1) + (y1 - y2) * (z1 + z2) + (y2 - y3) * (z2 + z3) + (y3 - y0) * (z3 + z0);
            double ny = (z0 - z1) * (x0 + x1) + (z1 - z2) * (x1 + x2) + (z2 - z3) * (x2 + x3) + (z3 - z0) * (x3 + x0);
            double nz = (x0 - x1) * (y0 + y1) + (x1 - x2) * (y1 + y2) + (x2 - x3) * (y2 + y3) + (x3 - x0) * (y3 + y0);
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 1e-9) return 0;
            bool flip = nx * hx + ny * hy + nz * hz < 0;
            if (flip)
            {
                nx = -nx;
                ny = -ny;
                nz = -nz;
            }
            float fx = (float)(nx / len), fy = (float)(ny / len), fz = (float)(nz / len);
            m.Reserve(4, 6);
            int v = m.AddVertex((float)x0, (float)y0, (float)z0, fx, fy, fz, c);
            m.AddVertex((float)x1, (float)y1, (float)z1, fx, fy, fz, c);
            m.AddVertex((float)x2, (float)y2, (float)z2, fx, fy, fz, c);
            m.AddVertex((float)x3, (float)y3, (float)z3, fx, fy, fz, c);
            // The Newell normal of 0-1-2-3 points along cross(1-0, 2-0), which is Unity's front for (0, 1, 2).
            if (!flip)
            {
                m.AddTriangle(v, v + 1, v + 2);
                m.AddTriangle(v, v + 2, v + 3);
            }
            else
            {
                m.AddTriangle(v, v + 2, v + 1);
                m.AddTriangle(v, v + 3, v + 2);
            }
            return 2;
        }

        /// <summary>A triangle facing the side of (hx, hy, hz).</summary>
        public static int Tri(MeshData m, double x0, double y0, double z0, double x1, double y1, double z1,
                              double x2, double y2, double z2, double hx, double hy, double hz, uint c)
        {
            double ux = x1 - x0, uy = y1 - y0, uz = z1 - z0, vx = x2 - x0, vy = y2 - y0, vz = z2 - z0;
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 1e-9) return 0;
            bool flip = nx * hx + ny * hy + nz * hz < 0;
            if (flip)
            {
                nx = -nx;
                ny = -ny;
                nz = -nz;
            }
            float fx = (float)(nx / len), fy = (float)(ny / len), fz = (float)(nz / len);
            m.Reserve(3, 3);
            int v = m.AddVertex((float)x0, (float)y0, (float)z0, fx, fy, fz, c);
            m.AddVertex((float)x1, (float)y1, (float)z1, fx, fy, fz, c);
            m.AddVertex((float)x2, (float)y2, (float)z2, fx, fy, fz, c);
            // cross(1-0, 2-0) = n: Unity front is where cross(b-a, c-a) points, so (0, 1, 2) when not flipped.
            if (!flip) m.AddTriangle(v, v + 1, v + 2);
            else m.AddTriangle(v, v + 2, v + 1);
            return 1;
        }

        /// <summary>A quad in a frame from local corners (u, v, w), facing local direction (hu, hv, hw).</summary>
        public static int QuadLocal(MeshData m, in KitFrame f, double u0, double v0, double w0, double u1, double v1, double w1,
                                    double u2, double v2, double w2, double u3, double v3, double w3,
                                    double hu, double hv, double hw, uint c)
        {
            double ax, ay, az, bx, by, bz, cx, cy, cz, dx, dy, dz;
            f.ToWorld(u0, v0, w0, out ax, out ay, out az);
            f.ToWorld(u1, v1, w1, out bx, out by, out bz);
            f.ToWorld(u2, v2, w2, out cx, out cy, out cz);
            f.ToWorld(u3, v3, w3, out dx, out dy, out dz);
            double hx = hu * f.UX + hw * f.UZ, hz = hu * f.UZ - hw * f.UX;
            return Quad(m, ax, ay, az, bx, by, bz, cx, cy, cz, dx, dy, dz, hx, hv, hz, c);
        }

        public static int TriLocal(MeshData m, in KitFrame f, double u0, double v0, double w0, double u1, double v1, double w1,
                                   double u2, double v2, double w2, double hu, double hv, double hw, uint c)
        {
            double ax, ay, az, bx, by, bz, cx, cy, cz;
            f.ToWorld(u0, v0, w0, out ax, out ay, out az);
            f.ToWorld(u1, v1, w1, out bx, out by, out bz);
            f.ToWorld(u2, v2, w2, out cx, out cy, out cz);
            double hx = hu * f.UX + hw * f.UZ, hz = hu * f.UZ - hw * f.UX;
            return Tri(m, ax, ay, az, bx, by, bz, cx, cy, cz, hx, hv, hz, c);
        }

        /// <summary>A flat rectangle on the facade plane w (facing +W) from (u0, v0) to (u1, v1).</summary>
        public static int Panel(MeshData m, in KitFrame f, double u0, double v0, double u1, double v1, double w, uint c)
        {
            return QuadLocal(m, f, u0, v0, w, u1, v0, w, u1, v1, w, u0, v1, w, 0, 0, 1, c);
        }

        /// <summary>An axis-aligned box in the frame: [u0, u1] × [v0, v1] × [w0, w1], drawing the faces in
        /// <paramref name="faces"/>. Returns the triangles added.</summary>
        public static int Box(MeshData m, in KitFrame f, double u0, double u1, double v0, double v1, double w0, double w1,
                              uint c, BoxFaces faces = BoxFaces.All)
        {
            if (u1 <= u0 || v1 <= v0 || w1 <= w0) return 0;
            int t = 0;
            if ((faces & BoxFaces.Front) != 0) t += QuadLocal(m, f, u0, v0, w1, u1, v0, w1, u1, v1, w1, u0, v1, w1, 0, 0, 1, c);
            if ((faces & BoxFaces.Back) != 0) t += QuadLocal(m, f, u0, v0, w0, u1, v0, w0, u1, v1, w0, u0, v1, w0, 0, 0, -1, c);
            if ((faces & BoxFaces.Left) != 0) t += QuadLocal(m, f, u0, v0, w0, u0, v0, w1, u0, v1, w1, u0, v1, w0, -1, 0, 0, c);
            if ((faces & BoxFaces.Right) != 0) t += QuadLocal(m, f, u1, v0, w0, u1, v0, w1, u1, v1, w1, u1, v1, w0, 1, 0, 0, c);
            if ((faces & BoxFaces.Top) != 0) t += QuadLocal(m, f, u0, v1, w0, u1, v1, w0, u1, v1, w1, u0, v1, w1, 0, 1, 0, c);
            if ((faces & BoxFaces.Bottom) != 0) t += QuadLocal(m, f, u0, v0, w0, u1, v0, w0, u1, v0, w1, u0, v0, w1, 0, -1, 0, c);
            return t;
        }

        /// <summary>A box centred on (cx, cz) with yaw (radians, rotating +X toward −Z... i.e. the frame U axis at
        /// angle yaw from +X counter-clockwise seen from above), half extents hx (along U), hz (along W), from y0 to
        /// y1.</summary>
        public static int OrientedBox(MeshData m, double cx, double cz, double y0, double y1, double halfU, double halfW,
                                      double ux, double uz, uint c, BoxFaces faces = BoxFaces.All)
        {
            var f = new KitFrame(cx, y0, cz, ux, uz);
            return Box(m, f, -halfU, halfU, 0, y1 - y0, -halfW, halfW, c, faces);
        }

        /// <summary>A strut or beam: a square-section bar of side <paramref name="t"/> from point a to point b (4 side
        /// faces, no end caps: 8 triangles).</summary>
        public static int Bar(MeshData m, double ax, double ay, double az, double bx, double by, double bz, double t, uint c)
        {
            double dx = bx - ax, dy = by - ay, dz = bz - az;
            double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-6) return 0;
            dx /= len;
            dy /= len;
            dz /= len;
            // Two perpendicular axes p, q.
            double px, py, pz;
            if (Math.Abs(dy) < 0.9)
            {
                // p = d × up
                px = -dz;
                py = 0;
                pz = dx;
            }
            else
            {
                px = 1;
                py = 0;
                pz = 0;
            }
            double pl = Math.Sqrt(px * px + py * py + pz * pz);
            px /= pl;
            py /= pl;
            pz /= pl;
            double qx = dy * pz - dz * py, qy = dz * px - dx * pz, qz = dx * py - dy * px;
            double h = 0.5 * t;
            int tris = 0;
            for (int k = 0; k < 4; k++)
            {
                // Section corners in order: (+p+q), (-p+q), (-p-q), (+p-q); side k joins corner k and k + 1.
                double c0p = k == 0 || k == 3 ? 1 : -1, c0q = k < 2 ? 1 : -1;
                int k1 = (k + 1) & 3;
                double c1p = k1 == 0 || k1 == 3 ? 1 : -1, c1q = k1 < 2 ? 1 : -1;
                double ox0 = h * (c0p * px + c0q * qx), oy0 = h * (c0p * py + c0q * qy), oz0 = h * (c0p * pz + c0q * qz);
                double ox1 = h * (c1p * px + c1q * qx), oy1 = h * (c1p * py + c1q * qy), oz1 = h * (c1p * pz + c1q * qz);
                tris += Quad(m, ax + ox0, ay + oy0, az + oz0, ax + ox1, ay + oy1, az + oz1, bx + ox1, by + oy1, bz + oz1,
                             bx + ox0, by + oy0, bz + oz0, ox0 + ox1, oy0 + oy1, oz0 + oz1, c);
            }
            return tris;
        }

        /// <summary>Walls of a convex or concave ring from y0 to y1 facing out (ring counter-clockwise from above).</summary>
        public static int RingWalls(MeshData m, double[] x, double[] z, int n, double y0, double y1, uint c)
        {
            if (!(y1 > y0)) return 0;
            int t = 0;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                double dx = x[j] - x[i], dz = z[j] - z[i];
                t += Quad(m, x[i], y0, z[i], x[j], y0, z[j], x[j], y1, z[j], x[i], y1, z[i], dz, 0, -dx, c);
            }
            return t;
        }

        /// <summary>A horizontal cap over a convex counter-clockwise ring at height y, facing up (or down).</summary>
        public static int ConvexCap(MeshData m, double[] x, double[] z, int n, double y, bool up, uint c)
        {
            if (n < 3) return 0;
            float ny = up ? 1f : -1f;
            m.Reserve(n, 3 * (n - 2));
            int v0 = m.VertexCount;
            for (int i = 0; i < n; i++) m.AddVertex((float)x[i], (float)y, (float)z[i], 0f, ny, 0f, c);
            for (int i = 1; i < n - 1; i++)
            {
                // CCW from above; Unity up-facing = clockwise from above.
                if (up) m.AddTriangle(v0, v0 + i + 1, v0 + i);
                else m.AddTriangle(v0, v0 + i, v0 + i + 1);
            }
            return n - 2;
        }

        /// <summary>One frustum band between a ring scaled by s0 at y0 and by s1 at y1 about (cx, cz), facing away from
        /// the axis; s1 = 0 closes to an apex. Returns the triangles added.</summary>
        public static int Loft(MeshData m, double[] x, double[] z, int n, double cx, double cz, double y0, double s0, double y1, double s1, uint c)
        {
            int t = 0;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                double ax = cx + (x[i] - cx) * s0, az = cz + (z[i] - cz) * s0, bx = cx + (x[j] - cx) * s0, bz = cz + (z[j] - cz) * s0;
                double hx = 0.5 * (x[i] + x[j]) - cx, hz = 0.5 * (z[i] + z[j]) - cz;
                double hy = s1 < s0 ? 1e-3 : s1 > s0 ? -1e-3 : 0;
                if (s1 <= 0)
                {
                    t += Tri(m, ax, y0, az, bx, y0, bz, cx, y1, cz, hx, hy, hz, c);
                    continue;
                }
                double a1x = cx + (x[i] - cx) * s1, a1z = cz + (z[i] - cz) * s1, b1x = cx + (x[j] - cx) * s1, b1z = cz + (z[j] - cz) * s1;
                t += Quad(m, ax, y0, az, bx, y0, bz, b1x, y1, b1z, a1x, y1, a1z, hx, hy, hz, c);
            }
            return t;
        }

        /// <summary>A regular n-gon ring of radius r about (cx, cz), counter-clockwise from above, starting at angle
        /// <paramref name="phase"/> (radians from +X).</summary>
        public static void RegularRing(double cx, double cz, double r, int n, double phase, double[] x, double[] z)
        {
            for (int i = 0; i < n; i++)
            {
                double a = phase + 2 * Math.PI * i / n;
                x[i] = cx + r * Math.Cos(a);
                z[i] = cz + r * Math.Sin(a);
            }
        }

        [ThreadStatic] private static double[] _rx, _rz;

        private static void RingScratch(int n)
        {
            if (_rx == null || _rx.Length < n)
            {
                _rx = new double[Math.Max(n, 32)];
                _rz = new double[Math.Max(n, 32)];
            }
        }

        /// <summary>A vertical cylinder (n sides) from y0 to y1, optional top cap. Returns the triangles added.</summary>
        public static int Cylinder(MeshData m, double cx, double cz, double r, double y0, double y1, int n, bool top, uint c)
        {
            RingScratch(n);
            RegularRing(cx, cz, r, n, 0, _rx, _rz);
            int t = RingWalls(m, _rx, _rz, n, y0, y1, c);
            if (top) t += ConvexCap(m, _rx, _rz, n, y1, true, c);
            return t;
        }

        /// <summary>A frustum (n sides) from radius r0 at y0 to r1 at y1 (r1 = 0: a cone).</summary>
        public static int Frustum(MeshData m, double cx, double cz, double r0, double y0, double r1, double y1, int n, bool top, uint c)
        {
            if (r0 <= 0) return 0;
            RingScratch(n);
            RegularRing(cx, cz, r0, n, Math.PI / n, _rx, _rz);
            int t = Loft(m, _rx, _rz, n, cx, cz, y0, 1.0, y1, r1 / r0, c);
            if (top && r1 > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    _rx[i] = cx + (_rx[i] - cx) * (r1 / r0);
                    _rz[i] = cz + (_rz[i] - cz) * (r1 / r0);
                }
                t += ConvexCap(m, _rx, _rz, n, y1, true, c);
            }
            return t;
        }

        /// <summary>A dome of radius r and height h on y0 (n sides, <paramref name="bands"/> latitude bands).</summary>
        public static int Dome(MeshData m, double cx, double cz, double r, double y0, double h, int n, int bands, uint c)
        {
            RingScratch(n);
            RegularRing(cx, cz, r, n, Math.PI / n, _rx, _rz);
            int t = 0;
            for (int k = 0; k < bands; k++)
            {
                double a0 = k * Math.PI / (2 * bands), a1 = (k + 1) * Math.PI / (2 * bands);
                double s1 = k + 1 == bands ? 0 : Math.Cos(a1);
                t += Loft(m, _rx, _rz, n, cx, cz, y0 + h * Math.Sin(a0), Math.Cos(a0), y0 + h * Math.Sin(a1), s1, c);
            }
            return t;
        }

        /// <summary>A sphere-ish blob (an octahedron squashed to radii rx, ry, rz) for small ornaments: 8 triangles.</summary>
        public static int Blob(MeshData m, double cx, double cy, double cz, double rx, double ry, double rz, uint c)
        {
            int t = 0;
            for (int k = 0; k < 4; k++)
            {
                double ax = k == 0 ? rx : k == 2 ? -rx : 0, az = k == 1 ? rz : k == 3 ? -rz : 0;
                int k1 = (k + 1) & 3;
                double bx = k1 == 0 ? rx : k1 == 2 ? -rx : 0, bz = k1 == 1 ? rz : k1 == 3 ? -rz : 0;
                double hx = ax + bx, hz = az + bz;
                t += Tri(m, cx + ax, cy, cz + az, cx + bx, cy, cz + bz, cx, cy + ry, cz, hx, 1, hz, c);
                t += Tri(m, cx + ax, cy, cz + az, cx + bx, cy, cz + bz, cx, cy - ry, cz, hx, -1, hz, c);
            }
            return t;
        }
    }
}
