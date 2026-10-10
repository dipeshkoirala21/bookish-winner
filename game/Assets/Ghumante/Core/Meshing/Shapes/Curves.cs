using System;

namespace Ghumante.Core.Meshing.Shapes
{
    /// <summary>
    /// Curve helpers that write into <see cref="Path3"/> (3D) buffers: centripetal Catmull-Rom splines through
    /// control points, cubic Bézier segments, arc fillets of polyline corners, circular arcs, catenary wires and
    /// arc-length resampling. All append to the destination (call <c>Clear()</c> first for a fresh path) and never
    /// allocate beyond the buffers' growth. Use <see cref="ShapeLod.Path"/> for the segment counts.
    /// </summary>
    public static class Curves
    {
        /// <summary>
        /// Centripetal Catmull-Rom (alpha 0.5, no cusps or self-loops) through every control point, with
        /// <paramref name="segmentsPerSpan"/> segments between consecutive control points. Open curves pass through
        /// the first and last point; closed curves wrap (the first point is not repeated at the end).
        /// </summary>
        public static Path3 CatmullRom(Path3 control, Path3 dst, int segmentsPerSpan, bool closed = false)
        {
            int n = control.Count;
            if (n == 0) return dst;
            if (n == 1 || segmentsPerSpan < 1)
            {
                for (int i = 0; i < n; i++) dst.AddDistinct(control.X[i], control.Y[i], control.Z[i]);
                return dst;
            }
            int spans = closed ? n : n - 1;
            for (int s = 0; s < spans; s++)
            {
                int i1 = s, i2 = (s + 1) % n;
                int i0 = closed ? (s + n - 1) % n : Math.Max(0, s - 1);
                int i3 = closed ? (s + 2) % n : Math.Min(n - 1, s + 2);
                double p0x = control.X[i0], p0y = control.Y[i0], p0z = control.Z[i0];
                double p1x = control.X[i1], p1y = control.Y[i1], p1z = control.Z[i1];
                double p2x = control.X[i2], p2y = control.Y[i2], p2z = control.Z[i2];
                double p3x = control.X[i3], p3y = control.Y[i3], p3z = control.Z[i3];
                // Phantom end points by reflection for open ends.
                if (!closed && s == 0)
                {
                    p0x = 2 * p1x - p2x;
                    p0y = 2 * p1y - p2y;
                    p0z = 2 * p1z - p2z;
                }
                if (!closed && s == spans - 1)
                {
                    p3x = 2 * p2x - p1x;
                    p3y = 2 * p2y - p1y;
                    p3z = 2 * p2z - p1z;
                }
                double t0 = 0;
                double t1 = t0 + Knot(p0x, p0y, p0z, p1x, p1y, p1z);
                double t2 = t1 + Knot(p1x, p1y, p1z, p2x, p2y, p2z);
                double t3 = t2 + Knot(p2x, p2y, p2z, p3x, p3y, p3z);
                int k0 = s == 0 ? 0 : 1;
                int kEnd = segmentsPerSpan;
                for (int k = k0; k <= kEnd; k++)
                {
                    if (closed && s == spans - 1 && k == kEnd) break;
                    double t = t1 + (t2 - t1) * k / segmentsPerSpan;
                    double x = Cr(p0x, p1x, p2x, p3x, t0, t1, t2, t3, t);
                    double y = Cr(p0y, p1y, p2y, p3y, t0, t1, t2, t3, t);
                    double z = Cr(p0z, p1z, p2z, p3z, t0, t1, t2, t3, t);
                    dst.AddDistinct(x, y, z);
                }
            }
            return dst;
        }

        private static double Knot(double ax, double ay, double az, double bx, double by, double bz)
        {
            double d = Math.Sqrt(Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay) + (bz - az) * (bz - az)));
            return d < 1e-6 ? 1e-6 : d;
        }

        private static double Cr(double p0, double p1, double p2, double p3, double t0, double t1, double t2, double t3, double t)
        {
            double a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            double a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            double a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            double b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            double b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        /// <summary>A cubic Bézier from p0 to p3 with handles p1, p2, as <paramref name="segments"/> segments
        /// (p0 is skipped when it repeats the path's last point).</summary>
        public static Path3 Bezier(Path3 dst, double p0x, double p0y, double p0z, double p1x, double p1y, double p1z,
                                   double p2x, double p2y, double p2z, double p3x, double p3y, double p3z, int segments)
        {
            if (segments < 1) segments = 1;
            for (int k = 0; k <= segments; k++)
            {
                double t = (double)k / segments, u = 1 - t;
                double b0 = u * u * u, b1 = 3 * u * u * t, b2 = 3 * u * t * t, b3 = t * t * t;
                dst.AddDistinct(b0 * p0x + b1 * p1x + b2 * p2x + b3 * p3x, b0 * p0y + b1 * p1y + b2 * p2y + b3 * p3y,
                                b0 * p0z + b1 * p1z + b2 * p2z + b3 * p3z);
            }
            return dst;
        }

        /// <summary>A circular arc in the horizontal plane at height <paramref name="y"/>: centre (cx, cz), from
        /// compass bearing <paramref name="startDeg"/> (clockwise from +Z) over <paramref name="sweepDeg"/>.</summary>
        public static Path3 ArcXZ(Path3 dst, double cx, double y, double cz, double radius, double startDeg, double sweepDeg, int segments)
        {
            if (segments < 1) segments = 1;
            for (int k = 0; k <= segments; k++)
            {
                double a = (startDeg + sweepDeg * k / segments) * Math.PI / 180.0;
                dst.AddDistinct(cx + radius * Math.Sin(a), y, cz + radius * Math.Cos(a));
            }
            return dst;
        }

        /// <summary>
        /// Round the corners of a polyline with circular arcs of <paramref name="radius"/> (shrunk per corner so the
        /// tangent points stay within half of each adjacent segment) and <paramref name="segments"/> segments per
        /// arc. Open paths keep their end points. Writes to <paramref name="dst"/> (must differ from src).
        /// </summary>
        public static Path3 Fillet(Path3 src, Path3 dst, double radius, int segments, bool closed = false)
        {
            int n = src.Count;
            for (int i = 0; i < n; i++)
            {
                bool end = !closed && (i == 0 || i == n - 1);
                if (end || segments < 1 || radius <= 0 || n < 3)
                {
                    dst.AddDistinct(src.X[i], src.Y[i], src.Z[i]);
                    continue;
                }
                int ip = (i + n - 1) % n, inx = (i + 1) % n;
                double px = src.X[i], py = src.Y[i], pz = src.Z[i];
                double ux = src.X[ip] - px, uy = src.Y[ip] - py, uz = src.Z[ip] - pz;
                double vx = src.X[inx] - px, vy = src.Y[inx] - py, vz = src.Z[inx] - pz;
                double lu = Math.Sqrt(ux * ux + uy * uy + uz * uz), lv = Math.Sqrt(vx * vx + vy * vy + vz * vz);
                if (lu < 1e-9 || lv < 1e-9)
                {
                    dst.AddDistinct(px, py, pz);
                    continue;
                }
                ux /= lu;
                uy /= lu;
                uz /= lu;
                vx /= lv;
                vy /= lv;
                vz /= lv;
                double cos = ux * vx + uy * vy + uz * vz;
                if (cos < -0.9999 || cos > 0.9999)
                {
                    dst.AddDistinct(px, py, pz);
                    continue;
                }
                double theta = Math.Acos(cos), half = theta * 0.5;
                double t = radius / Math.Tan(half);
                double tMax = 0.5 * Math.Min(lu, lv);
                double r = radius;
                if (t > tMax)
                {
                    t = tMax;
                    r = t * Math.Tan(half);
                }
                double ax = px + ux * t, ay = py + uy * t, az = pz + uz * t;
                double bx = px + vx * t, by = py + vy * t, bz = pz + vz * t;
                double hx = ux + vx, hy = uy + vy, hz = uz + vz;
                double hl = Math.Sqrt(hx * hx + hy * hy + hz * hz);
                double dc = r / Math.Sin(half);
                double cx = px + hx / hl * dc, cy = py + hy / hl * dc, cz = pz + hz / hl * dc;
                // Slerp from (a - c) to (b - c).
                double e0x = ax - cx, e0y = ay - cy, e0z = az - cz, e1x = bx - cx, e1y = by - cy, e1z = bz - cz;
                double phi = Math.PI - theta;
                double sp = Math.Sin(phi);
                for (int k = 0; k <= segments; k++)
                {
                    double f = (double)k / segments;
                    double w0 = Math.Sin((1 - f) * phi) / sp, w1 = Math.Sin(f * phi) / sp;
                    dst.AddDistinct(cx + w0 * e0x + w1 * e1x, cy + w0 * e0y + w1 * e1y, cz + w0 * e0z + w1 * e1z);
                }
            }
            return dst;
        }

        /// <summary>
        /// The 2D arc fillet of corner p1 between p0 and p2: tangent points a (on p1→p0) and b (on p1→p2), centre c
        /// and the turn sign (+1 for a left / counter-clockwise turn). <paramref name="radius"/> shrinks so the
        /// tangent points stay within half of each edge. False for a straight, folded or degenerate corner.
        /// </summary>
        public static bool FilletCorner(double p0x, double p0y, double p1x, double p1y, double p2x, double p2y, ref double radius,
                                        out double ax, out double ay, out double bx, out double by, out double cx, out double cy,
                                        out double turn)
        {
            ax = ay = bx = by = cx = cy = turn = 0;
            double ux = p0x - p1x, uy = p0y - p1y, vx = p2x - p1x, vy = p2y - p1y;
            double lu = Math.Sqrt(ux * ux + uy * uy), lv = Math.Sqrt(vx * vx + vy * vy);
            if (lu < 1e-9 || lv < 1e-9) return false;
            ux /= lu;
            uy /= lu;
            vx /= lv;
            vy /= lv;
            double cos = ux * vx + uy * vy;
            if (cos < -0.9999 || cos > 0.9999) return false;
            double half = 0.5 * Math.Acos(cos);
            double t = radius / Math.Tan(half), tMax = 0.5 * Math.Min(lu, lv);
            if (t > tMax)
            {
                t = tMax;
                radius = t * Math.Tan(half);
            }
            ax = p1x + ux * t;
            ay = p1y + uy * t;
            bx = p1x + vx * t;
            by = p1y + vy * t;
            double hx = ux + vx, hy = uy + vy, hl = Math.Sqrt(hx * hx + hy * hy), dc = radius / Math.Sin(half);
            cx = p1x + hx / hl * dc;
            cy = p1y + hy / hl * dc;
            // Turn sign of p0 → p1 → p2: cross(p1 - p0, p2 - p1) = cross(-u, v).
            turn = (-ux) * vy - (-uy) * vx > 0 ? 1 : -1;
            return true;
        }

        /// <summary>
        /// A hanging wire from a to b with a catenary shape that sags <paramref name="sag"/> metres below the
        /// straight chord at mid-span (good for electric wires, prayer-flag lines and chains), as
        /// <paramref name="segments"/> segments.
        /// </summary>
        public static Path3 Catenary(Path3 dst, double ax, double ay, double az, double bx, double by, double bz, double sag, int segments)
        {
            if (segments < 2) segments = 2;
            double span = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            if (span < 1e-6) span = 1e-6;
            // Solve a·(cosh(L/2a) - 1) = sag for the catenary parameter a (bisection on log a).
            double half = span * 0.5;
            double shapeDen = 0, a = 0;
            if (sag > 1e-9)
            {
                double lo = Math.Log(half * 1e-3 + 1e-9), hi = Math.Log(half * 1e4 + 1);
                for (int it = 0; it < 80; it++)
                {
                    double mid = 0.5 * (lo + hi);
                    double am = Math.Exp(mid);
                    double s = am * (Math.Cosh(half / am) - 1);
                    if (s > sag) lo = mid;
                    else hi = mid;
                }
                a = Math.Exp(0.5 * (lo + hi));
                shapeDen = Math.Cosh(half / a) - 1;
            }
            for (int k = 0; k <= segments; k++)
            {
                double t = (double)k / segments;
                double drop = 0;
                if (shapeDen > 1e-12) drop = sag * (Math.Cosh(half / a) - Math.Cosh((t - 0.5) * span / a)) / shapeDen;
                dst.AddDistinct(ax + (bx - ax) * t, ay + (by - ay) * t - drop, az + (bz - az) * t);
            }
            return dst;
        }

        /// <summary>Resample a polyline at (about) equal arc-length <paramref name="spacing"/>, keeping both ends.
        /// Writes to <paramref name="dst"/> (must differ from src).</summary>
        public static Path3 Resample(Path3 src, Path3 dst, double spacing)
        {
            if (src.Count < 2 || spacing <= 0)
            {
                for (int i = 0; i < src.Count; i++) dst.AddDistinct(src.X[i], src.Y[i], src.Z[i]);
                return dst;
            }
            double total = src.Length();
            int n = Math.Max(1, (int)Math.Round(total / spacing));
            double step = total / n;
            dst.AddDistinct(src.X[0], src.Y[0], src.Z[0]);
            int seg = 0;
            double segStart = 0;
            for (int k = 1; k < n; k++)
            {
                double target = k * step;
                while (seg < src.Count - 2)
                {
                    double l = SegLen(src, seg);
                    if (segStart + l >= target) break;
                    segStart += l;
                    seg++;
                }
                double sl = SegLen(src, seg);
                double f = sl < 1e-12 ? 0 : (target - segStart) / sl;
                if (f > 1) f = 1;
                dst.AddDistinct(src.X[seg] + (src.X[seg + 1] - src.X[seg]) * f, src.Y[seg] + (src.Y[seg + 1] - src.Y[seg]) * f,
                                src.Z[seg] + (src.Z[seg + 1] - src.Z[seg]) * f);
            }
            int last = src.Count - 1;
            dst.AddDistinct(src.X[last], src.Y[last], src.Z[last]);
            return dst;
        }

        private static double SegLen(Path3 p, int i)
        {
            double dx = p.X[i + 1] - p.X[i], dy = p.Y[i + 1] - p.Y[i], dz = p.Z[i + 1] - p.Z[i];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
