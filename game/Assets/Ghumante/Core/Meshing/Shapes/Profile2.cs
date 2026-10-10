using System;

namespace Ghumante.Core.Meshing.Shapes
{
    /// <summary>
    /// A reusable 2D polyline (open) or polygon (closed) for lathes, sweeps, lofts and extrudes: points (X, Y), an
    /// optional explicit outward normal per point (NaN = derive it from the edges) and a crease flag per point (hard
    /// shading edge there). Orientation: the outward side is to the <b>right</b> of the direction of travel, so a
    /// closed profile goes counter-clockwise (x right, y up) and a lathe profile runs from the bottom of the axis to the
    /// top with the outside at +x; a clockwise closed profile is detected and handled. Grow-only buffers: build one
    /// per mesher and refill it with the <c>Set…</c> builders, which return <c>this</c> for chaining.
    /// </summary>
    public sealed class Profile2
    {
        public double[] X, Y, NX, NY;
        public bool[] Crease;
        public int Count;
        public bool Closed;

        public Profile2(int capacity = 32)
        {
            if (capacity < 4) capacity = 4;
            X = new double[capacity];
            Y = new double[capacity];
            NX = new double[capacity];
            NY = new double[capacity];
            Crease = new bool[capacity];
        }

        /// <summary>Forget the points (keeps the buffers).</summary>
        public Profile2 Clear(bool closed)
        {
            Count = 0;
            Closed = closed;
            return this;
        }

        /// <summary>Append a point whose normal is derived from its edges.</summary>
        public Profile2 Add(double x, double y, bool crease = false)
        {
            return Add(x, y, double.NaN, double.NaN, crease);
        }

        /// <summary>Append a point with an explicit outward normal (normalised here; ignored at a crease).</summary>
        public Profile2 Add(double x, double y, double nx, double ny, bool crease)
        {
            if (Count == X.Length) Grow(Count * 2);
            if (!double.IsNaN(nx))
            {
                double l = Math.Sqrt(nx * nx + ny * ny);
                if (l > 1e-12)
                {
                    nx /= l;
                    ny /= l;
                }
                else nx = ny = double.NaN;
            }
            X[Count] = x;
            Y[Count] = y;
            NX[Count] = nx;
            NY[Count] = ny;
            Crease[Count] = crease;
            Count++;
            return this;
        }

        /// <summary>Copy another profile into this one.</summary>
        public Profile2 CopyFrom(Profile2 src)
        {
            Clear(src.Closed);
            if (X.Length < src.Count) Grow(src.Count);
            Array.Copy(src.X, X, src.Count);
            Array.Copy(src.Y, Y, src.Count);
            Array.Copy(src.NX, NX, src.Count);
            Array.Copy(src.NY, NY, src.Count);
            Array.Copy(src.Crease, Crease, src.Count);
            Count = src.Count;
            return this;
        }

        /// <summary>Shoelace signed area (positive = counter-clockwise, x right, y up).</summary>
        public double SignedArea()
        {
            double a = 0;
            for (int i = 0, j = Count - 1; i < Count; j = i++) a += X[j] * Y[i] - X[i] * Y[j];
            return 0.5 * a;
        }

        /// <summary>Total length (including the closing edge of a closed profile).</summary>
        public double Length()
        {
            double l = 0;
            int n = Closed ? Count : Count - 1;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % Count;
                double dx = X[j] - X[i], dy = Y[j] - Y[i];
                l += Math.Sqrt(dx * dx + dy * dy);
            }
            return l;
        }

        /// <summary>Scale then offset every point (normals follow the scale).</summary>
        public Profile2 Transform(double sx, double sy, double ox, double oy)
        {
            for (int i = 0; i < Count; i++)
            {
                X[i] = X[i] * sx + ox;
                Y[i] = Y[i] * sy + oy;
                if (!double.IsNaN(NX[i]))
                {
                    double nx = NX[i] / (sx == 0 ? 1e-12 : sx), ny = NY[i] / (sy == 0 ? 1e-12 : sy);
                    double l = Math.Sqrt(nx * nx + ny * ny);
                    if (sx * sy < 0) l = -l;
                    NX[i] = nx / l;
                    NY[i] = ny / l;
                }
            }
            if (sx * sy < 0) Reverse();
            return this;
        }

        /// <summary>Reverse the point order (normals and creases travel with their points).</summary>
        public Profile2 Reverse()
        {
            for (int i = 0, j = Count - 1; i < j; i++, j--)
            {
                Swap(X, i, j);
                Swap(Y, i, j);
                Swap(NX, i, j);
                Swap(NY, i, j);
                bool c = Crease[i];
                Crease[i] = Crease[j];
                Crease[j] = c;
            }
            return this;
        }

        /// <summary>A circle of radius <paramref name="r"/> (counter-clockwise, <paramref name="n"/> points, the first
        /// at angle <paramref name="phaseDeg"/> from +x), exact normals.</summary>
        public Profile2 SetCircle(double r, int n, double cx = 0, double cy = 0, double phaseDeg = 0)
        {
            return SetEllipse(r, r, n, cx, cy, phaseDeg);
        }

        public Profile2 SetEllipse(double rx, double ry, int n, double cx = 0, double cy = 0, double phaseDeg = 0)
        {
            if (n < 3) n = 3;
            Clear(true);
            double p = phaseDeg * Math.PI / 180.0;
            for (int i = 0; i < n; i++)
            {
                double a = p + 2 * Math.PI * i / n, c = Math.Cos(a), s = Math.Sin(a);
                Add(cx + rx * c, cy + ry * s, c / Math.Max(rx, 1e-9), s / Math.Max(ry, 1e-9), false);
            }
            return this;
        }

        /// <summary>A centred w × h rectangle with hard corners (counter-clockwise).</summary>
        public Profile2 SetRect(double w, double h)
        {
            double a = w * 0.5, b = h * 0.5;
            return Clear(true).Add(-a, -b, true).Add(a, -b, true).Add(a, b, true).Add(-a, b, true);
        }

        /// <summary>A centred w × h rectangle whose corners are quarter circles of radius <paramref name="r"/> with
        /// <paramref name="segments"/> segments each (0 = hard corners).</summary>
        public Profile2 SetRoundedRect(double w, double h, double r, int segments)
        {
            SetRect(w, h);
            return segments > 0 && r > 0 ? FilletCorners(r, segments) : this;
        }

        /// <summary>A regular polygon with hard corners (n sides, circumradius r).</summary>
        public Profile2 SetRegular(double r, int n, double phaseDeg = 90)
        {
            if (n < 3) n = 3;
            Clear(true);
            double p = phaseDeg * Math.PI / 180.0;
            for (int i = 0; i < n; i++)
            {
                double a = p + 2 * Math.PI * i / n;
                Add(r * Math.Cos(a), r * Math.Sin(a), true);
            }
            return this;
        }

        /// <summary>Points from parallel arrays; <paramref name="crease"/> marks every point hard.</summary>
        public Profile2 SetPoints(double[] x, double[] y, int n, bool closed, bool crease)
        {
            Clear(closed);
            for (int i = 0; i < n; i++) Add(x[i], y[i], crease);
            return this;
        }

        /// <summary>
        /// Round every creased corner (and every corner when <paramref name="all"/>) with a circular arc of radius
        /// <paramref name="radius"/> (shrunk to fit half of each adjacent edge) and <paramref name="segments"/>
        /// segments; the arc points are smooth. Open ends are kept. In place (uses a per-thread scratch copy).
        /// </summary>
        public Profile2 FilletCorners(double radius, int segments, bool all = false)
        {
            if (segments <= 0 || radius <= 0 || Count < 3) return this;
            Profile2 src = Scratch;
            src.CopyFrom(this);
            Clear(src.Closed);
            int n = src.Count;
            // Explicit normals are true outward normals; the right of travel is the inside of a clockwise polygon.
            double side = src.Closed && src.SignedArea() < 0 ? -1 : 1;
            for (int i = 0; i < n; i++)
            {
                bool end = !src.Closed && (i == 0 || i == n - 1);
                if (end || !(all || src.Crease[i]))
                {
                    Add(src.X[i], src.Y[i], src.NX[i], src.NY[i], src.Crease[i]);
                    continue;
                }
                int ip = (i + n - 1) % n, inx = (i + 1) % n;
                double r = radius, ax, ay, bx, by, cx, cy, turn;
                if (!Curves.FilletCorner(src.X[ip], src.Y[ip], src.X[i], src.Y[i], src.X[inx], src.Y[inx], ref r,
                                         out ax, out ay, out bx, out by, out cx, out cy, out turn))
                {
                    Add(src.X[i], src.Y[i], src.NX[i], src.NY[i], src.Crease[i]);
                    continue;
                }
                double a0 = Math.Atan2(ay - cy, ax - cx), a1 = Math.Atan2(by - cy, bx - cx);
                double sweep = a1 - a0;
                if (turn > 0)
                {
                    while (sweep < 0) sweep += 2 * Math.PI;
                }
                else
                {
                    while (sweep > 0) sweep -= 2 * Math.PI;
                }
                for (int k = 0; k <= segments; k++)
                {
                    double t = a0 + sweep * k / segments, c = Math.Cos(t), s = Math.Sin(t);
                    // The right of travel is away from the centre on a left turn and towards it on a right turn.
                    double f = (turn > 0 ? 1 : -1) * side;
                    Add(cx + r * c, cy + r * s, f * c, f * s, false);
                }
            }
            return this;
        }

        /// <summary>
        /// Replace the polyline by centripetal Catmull-Rom curves through its points with
        /// <paramref name="segmentsPerSpan"/> segments per span; creased points (and the ends of an open profile)
        /// stay sharp corners and split the curve into separately smoothed runs. Turns a handful of control points
        /// into a smooth vase, kalash or finial profile for <see cref="Shapes.Lathe"/>. Explicit normals are
        /// dropped (derived from the dense curve instead). In place.
        /// </summary>
        public Profile2 Smooth(int segmentsPerSpan)
        {
            if (segmentsPerSpan < 2 || Count < 3) return this;
            Profile2 src = Scratch;
            src.CopyFrom(this);
            Clear(src.Closed);
            Path3 ctrl = s_ctrl ?? (s_ctrl = new Path3(32)), outp = s_out ?? (s_out = new Path3(128));
            int n = src.Count;
            int firstCrease = -1;
            for (int i = 0; i < n; i++)
                if (src.Crease[i])
                {
                    firstCrease = i;
                    break;
                }
            if (src.Closed && firstCrease < 0)
            {
                ctrl.Clear();
                for (int i = 0; i < n; i++) ctrl.Add(src.X[i], src.Y[i], 0);
                Curves.CatmullRom(ctrl, outp.Clear(), segmentsPerSpan, true);
                for (int k = 0; k < outp.Count; k++) Add(outp.X[k], outp.Y[k], false);
                return this;
            }
            // Runs between sharp points: open profiles start at 0, closed ones at their first crease.
            int start = src.Closed ? firstCrease : 0;
            int total = src.Closed ? n : n - 1;
            int i0 = start;
            Add(src.X[i0], src.Y[i0], true);
            int walked = 0;
            while (walked < total)
            {
                ctrl.Clear();
                ctrl.Add(src.X[i0], src.Y[i0], 0);
                int j = i0;
                do
                {
                    j = (j + 1) % n;
                    walked++;
                    ctrl.Add(src.X[j], src.Y[j], 0);
                }
                while (walked < total && !src.Crease[j]);
                outp.Clear();
                if (ctrl.Count == 2) outp.Add(ctrl.X[0], ctrl.Y[0], 0).Add(ctrl.X[1], ctrl.Y[1], 0); // straight edge
                else Curves.CatmullRom(ctrl, outp, segmentsPerSpan);
                bool endSharp = src.Crease[j] || (!src.Closed && j == n - 1);
                for (int k = 1; k < outp.Count; k++)
                {
                    bool last = k == outp.Count - 1;
                    if (last && src.Closed && walked >= total) break; // back at the start point
                    Add(outp.X[k], outp.Y[k], last && endSharp);
                }
                i0 = j;
            }
            return this;
        }

        [ThreadStatic] private static Path3 s_ctrl, s_out;
        [ThreadStatic] private static Profile2 s_scratch;

        private static Profile2 Scratch
        {
            get { return s_scratch ?? (s_scratch = new Profile2(64)); }
        }

        private void Grow(int cap)
        {
            Array.Resize(ref X, cap);
            Array.Resize(ref Y, cap);
            Array.Resize(ref NX, cap);
            Array.Resize(ref NY, cap);
            Array.Resize(ref Crease, cap);
        }

        private static void Swap(double[] a, int i, int j)
        {
            double t = a[i];
            a[i] = a[j];
            a[j] = t;
        }
    }

    /// <summary>A reusable 3D polyline (paths for sweeps, tubes, railings, wires). Grow-only buffers.</summary>
    public sealed class Path3
    {
        public double[] X, Y, Z;
        public int Count;

        public Path3(int capacity = 32)
        {
            if (capacity < 4) capacity = 4;
            X = new double[capacity];
            Y = new double[capacity];
            Z = new double[capacity];
        }

        public Path3 Clear()
        {
            Count = 0;
            return this;
        }

        public Path3 Add(double x, double y, double z)
        {
            if (Count == X.Length)
            {
                Array.Resize(ref X, Count * 2);
                Array.Resize(ref Y, Count * 2);
                Array.Resize(ref Z, Count * 2);
            }
            X[Count] = x;
            Y[Count] = y;
            Z[Count] = z;
            Count++;
            return this;
        }

        /// <summary>Append unless it repeats the last point (within 1e-9 m).</summary>
        public Path3 AddDistinct(double x, double y, double z)
        {
            if (Count > 0)
            {
                double dx = x - X[Count - 1], dy = y - Y[Count - 1], dz = z - Z[Count - 1];
                if (dx * dx + dy * dy + dz * dz < 1e-18) return this;
            }
            return Add(x, y, z);
        }

        public Path3 CopyFrom(Path3 src)
        {
            Clear();
            for (int i = 0; i < src.Count; i++) Add(src.X[i], src.Y[i], src.Z[i]);
            return this;
        }

        /// <summary>Polyline length (with the closing edge when <paramref name="closed"/>).</summary>
        public double Length(bool closed = false)
        {
            double l = 0;
            int n = closed ? Count : Count - 1;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % Count;
                double dx = X[j] - X[i], dy = Y[j] - Y[i], dz = Z[j] - Z[i];
                l += Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            return l;
        }
    }
}
