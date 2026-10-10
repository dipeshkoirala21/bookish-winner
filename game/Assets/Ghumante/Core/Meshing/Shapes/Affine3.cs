using System;

namespace Ghumante.Core.Meshing.Shapes
{
    /// <summary>
    /// An affine transform of 3D points (a 3×4 row-major matrix, doubles): <c>p' = L·p + t</c>. Composition reads
    /// like matrices: <c>a * b</c> applies <c>b</c> first, then <c>a</c>; <see cref="Then"/> is the same with the
    /// order spelled out (<c>a.Then(b)</c> applies <c>a</c>, then <c>b</c>). Rotations follow the right-hand rule on
    /// the mesh coordinates (x east, y up, z north), so <see cref="Yaw"/>(90) turns +Z (north) to +X (east), the
    /// compass convention of <see cref="KitFrame.FromYaw"/>. A transform with a negative determinant mirrors; the
    /// <see cref="Shapes"/> emitters notice and keep the Unity winding of <see cref="MeshData"/> correct.
    /// </summary>
    public struct Affine3
    {
        public double M00, M01, M02, M03;
        public double M10, M11, M12, M13;
        public double M20, M21, M22, M23;

        public Affine3(double m00, double m01, double m02, double m03, double m10, double m11, double m12, double m13,
                       double m20, double m21, double m22, double m23)
        {
            M00 = m00;
            M01 = m01;
            M02 = m02;
            M03 = m03;
            M10 = m10;
            M11 = m11;
            M12 = m12;
            M13 = m13;
            M20 = m20;
            M21 = m21;
            M22 = m22;
            M23 = m23;
        }

        /// <summary>The identity.</summary>
        public static Affine3 Identity
        {
            get { return new Affine3(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0); }
        }

        public static Affine3 Translation(double x, double y, double z)
        {
            return new Affine3(1, 0, 0, x, 0, 1, 0, y, 0, 0, 1, z);
        }

        public static Affine3 Scaling(double s)
        {
            return new Affine3(s, 0, 0, 0, 0, s, 0, 0, 0, 0, s, 0);
        }

        public static Affine3 Scaling(double sx, double sy, double sz)
        {
            return new Affine3(sx, 0, 0, 0, 0, sy, 0, 0, 0, 0, sz, 0);
        }

        /// <summary>Rotation about +X by <paramref name="rad"/> radians (right-hand rule: +Y turns towards +Z).</summary>
        public static Affine3 RotationX(double rad)
        {
            double c = Math.Cos(rad), s = Math.Sin(rad);
            return new Affine3(1, 0, 0, 0, 0, c, -s, 0, 0, s, c, 0);
        }

        /// <summary>Rotation about +Y by <paramref name="rad"/> radians (right-hand rule: +Z turns towards +X).</summary>
        public static Affine3 RotationY(double rad)
        {
            double c = Math.Cos(rad), s = Math.Sin(rad);
            return new Affine3(c, 0, s, 0, 0, 1, 0, 0, -s, 0, c, 0);
        }

        /// <summary>Rotation about +Z by <paramref name="rad"/> radians (right-hand rule: +X turns towards +Y).</summary>
        public static Affine3 RotationZ(double rad)
        {
            double c = Math.Cos(rad), s = Math.Sin(rad);
            return new Affine3(c, -s, 0, 0, s, c, 0, 0, 0, 0, 1, 0);
        }

        /// <summary>Compass yaw: +Z (north, "forward") turns to the bearing <paramref name="deg"/> degrees clockwise
        /// seen from above (90 = east).</summary>
        public static Affine3 Yaw(double deg)
        {
            return RotationY(deg * Math.PI / 180.0);
        }

        /// <summary>Rotation about an arbitrary axis (need not be unit) by <paramref name="rad"/> radians.</summary>
        public static Affine3 RotationAxis(double ax, double ay, double az, double rad)
        {
            double l = Math.Sqrt(ax * ax + ay * ay + az * az);
            if (l < 1e-12) return Identity;
            ax /= l;
            ay /= l;
            az /= l;
            double c = Math.Cos(rad), s = Math.Sin(rad), t = 1 - c;
            return new Affine3(t * ax * ax + c, t * ax * ay - s * az, t * ax * az + s * ay, 0,
                               t * ax * ay + s * az, t * ay * ay + c, t * ay * az - s * ax, 0,
                               t * ax * az - s * ay, t * ay * az + s * ax, t * az * az + c, 0);
        }

        /// <summary>The transform whose columns are the images of local X (u), Y (v) and Z (w) and whose origin is
        /// (ox, oy, oz).</summary>
        public static Affine3 FromBasis(double ux, double uy, double uz, double vx, double vy, double vz,
                                        double wx, double wy, double wz, double ox, double oy, double oz)
        {
            return new Affine3(ux, vx, wx, ox, uy, vy, wy, oy, uz, vz, wz, oz);
        }

        /// <summary>The building frame as a transform: local (u, v, w) of <see cref="KitFrame.ToWorld"/> maps to the
        /// same world point (this basis mirrors; the emitters handle the winding).</summary>
        public static Affine3 FromKitFrame(in KitFrame f)
        {
            return FromBasis(f.UX, 0, f.UZ, 0, 1, 0, f.UZ, 0, -f.UX, f.OX, f.OY, f.OZ);
        }

        /// <summary>
        /// A rigid frame at (ax, ay, az) whose local +Y points at (bx, by, bz) (for cylinders, capsules and cones
        /// between two points); local +Z is kept as horizontal as possible. <paramref name="length"/> is the distance.
        /// </summary>
        public static Affine3 Along(double ax, double ay, double az, double bx, double by, double bz, out double length)
        {
            double dx = bx - ax, dy = by - ay, dz = bz - az;
            length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length < 1e-12) return Translation(ax, ay, az);
            dx /= length;
            dy /= length;
            dz /= length;
            // Reference: world up unless nearly parallel, then world north.
            double rx = 0, ry = 1, rz = 0;
            if (Math.Abs(dy) > 0.95)
            {
                ry = 0;
                rz = 1;
            }
            // u = normalize(r × d); w = u × d (right-handed u, d, w).
            double ux = ry * dz - rz * dy, uy = rz * dx - rx * dz, uz = rx * dy - ry * dx;
            double ul = Math.Sqrt(ux * ux + uy * uy + uz * uz);
            ux /= ul;
            uy /= ul;
            uz /= ul;
            double wx = uy * dz - uz * dy, wy = uz * dx - ux * dz, wz = ux * dy - uy * dx;
            return FromBasis(ux, uy, uz, dx, dy, dz, wx, wy, wz, ax, ay, az);
        }

        /// <summary><c>a * b</c>: apply <paramref name="b"/> first, then <paramref name="a"/>.</summary>
        public static Affine3 operator *(in Affine3 a, in Affine3 b)
        {
            return new Affine3(
                a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20, a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21,
                a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22, a.M00 * b.M03 + a.M01 * b.M13 + a.M02 * b.M23 + a.M03,
                a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20, a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21,
                a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22, a.M10 * b.M03 + a.M11 * b.M13 + a.M12 * b.M23 + a.M13,
                a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20, a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21,
                a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22, a.M20 * b.M03 + a.M21 * b.M13 + a.M22 * b.M23 + a.M23);
        }

        /// <summary>This transform followed by <paramref name="next"/> (<c>next * this</c>).</summary>
        public Affine3 Then(in Affine3 next)
        {
            return next * this;
        }

        /// <summary>Shorthand for <c>Then(Translation(x, y, z))</c>.</summary>
        public Affine3 ThenTranslate(double x, double y, double z)
        {
            Affine3 r = this;
            r.M03 += x;
            r.M13 += y;
            r.M23 += z;
            return r;
        }

        public double Determinant
        {
            get
            {
                return M00 * (M11 * M22 - M12 * M21) - M01 * (M10 * M22 - M12 * M20) + M02 * (M10 * M21 - M11 * M20);
            }
        }

        /// <summary>True when the transform mirrors (negative determinant).</summary>
        public bool Mirrors
        {
            get { return Determinant < 0; }
        }

        /// <summary>The inverse (identity for a singular transform).</summary>
        public Affine3 Inverse()
        {
            double det = Determinant;
            if (Math.Abs(det) < 1e-300) return Identity;
            double i = 1.0 / det;
            double a00 = (M11 * M22 - M12 * M21) * i, a01 = (M02 * M21 - M01 * M22) * i, a02 = (M01 * M12 - M02 * M11) * i;
            double a10 = (M12 * M20 - M10 * M22) * i, a11 = (M00 * M22 - M02 * M20) * i, a12 = (M02 * M10 - M00 * M12) * i;
            double a20 = (M10 * M21 - M11 * M20) * i, a21 = (M01 * M20 - M00 * M21) * i, a22 = (M00 * M11 - M01 * M10) * i;
            return new Affine3(a00, a01, a02, -(a00 * M03 + a01 * M13 + a02 * M23),
                               a10, a11, a12, -(a10 * M03 + a11 * M13 + a12 * M23),
                               a20, a21, a22, -(a20 * M03 + a21 * M13 + a22 * M23));
        }

        public void Point(double x, double y, double z, out double ox, out double oy, out double oz)
        {
            ox = M00 * x + M01 * y + M02 * z + M03;
            oy = M10 * x + M11 * y + M12 * z + M13;
            oz = M20 * x + M21 * y + M22 * z + M23;
        }

        /// <summary>A direction (no translation, not normalised).</summary>
        public void Vector(double x, double y, double z, out double ox, out double oy, out double oz)
        {
            ox = M00 * x + M01 * y + M02 * z;
            oy = M10 * x + M11 * y + M12 * z;
            oz = M20 * x + M21 * y + M22 * z;
        }

        /// <summary>A surface normal (inverse transpose, normalised; points to the same side under mirroring).</summary>
        public void Normal(double x, double y, double z, out double ox, out double oy, out double oz)
        {
            // Cofactor matrix = det · inverse-transpose; the sign of det restores the direction.
            double c00 = M11 * M22 - M12 * M21, c01 = M12 * M20 - M10 * M22, c02 = M10 * M21 - M11 * M20;
            double c10 = M02 * M21 - M01 * M22, c11 = M00 * M22 - M02 * M20, c12 = M01 * M20 - M00 * M21;
            double c20 = M01 * M12 - M02 * M11, c21 = M02 * M10 - M00 * M12, c22 = M00 * M11 - M01 * M10;
            ox = c00 * x + c01 * y + c02 * z;
            oy = c10 * x + c11 * y + c12 * z;
            oz = c20 * x + c21 * y + c22 * z;
            double l = Math.Sqrt(ox * ox + oy * oy + oz * oz);
            if (l < 1e-300)
            {
                ox = 0;
                oy = 1;
                oz = 0;
                return;
            }
            if (Determinant < 0) l = -l;
            ox /= l;
            oy /= l;
            oz /= l;
        }
    }
}
