using System;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>
    /// A cross-section for <see cref="BridgeKit.Sweep"/> in the (u, y) plane of a path frame: u across (left of the
    /// travel direction positive), y up. Points are smooth by default (the normal is the average of the two
    /// neighbouring segments); <see cref="Hard"/> duplicates the last point so the two sides of that corner get their
    /// own normals. Outward faces need the points in clockwise order with u drawn to the right and y up (the front of
    /// segment (du, dy) faces (−dy, du)). Each point carries an AO factor (1 = open). Reusable: <see cref="Clear"/>
    /// keeps the buffers.
    /// </summary>
    public sealed class BridgeProfile
    {
        public double[] U = new double[32], Y = new double[32], NU = new double[32], NY = new double[32];
        public float[] Ao = new float[32];

        /// <summary>Per-point colour (0 = the sweep's colour) and channel (255 = the sweep's channel).</summary>
        public uint[] C = new uint[32];
        public byte[] Ch = new byte[32];
        public int Count;

        /// <summary>True when the last point joins the first (pipes): the normals wrap around.</summary>
        public bool Closed;

        public BridgeProfile Clear()
        {
            Count = 0;
            Closed = false;
            return this;
        }

        /// <summary>Append a smooth point (optionally with its own colour and material channel).</summary>
        public BridgeProfile Add(double u, double y, float ao = 1f, uint colour = 0, int channel = 255)
        {
            if (Count == U.Length)
            {
                int cap = Count * 2;
                Array.Resize(ref U, cap);
                Array.Resize(ref Y, cap);
                Array.Resize(ref NU, cap);
                Array.Resize(ref NY, cap);
                Array.Resize(ref Ao, cap);
                Array.Resize(ref C, cap);
                Array.Resize(ref Ch, cap);
            }
            U[Count] = u;
            Y[Count] = y;
            Ao[Count] = ao;
            C[Count] = colour;
            Ch[Count] = (byte)channel;
            Count++;
            return this;
        }

        /// <summary>Set the colour and channel of the points from <paramref name="from"/> to the end.</summary>
        public BridgeProfile Paint(int from, uint colour, int channel = 255)
        {
            for (int i = Math.Max(0, from); i < Count; i++)
            {
                C[i] = colour;
                Ch[i] = (byte)channel;
            }
            return this;
        }

        /// <summary>A closed rounded rectangle centred on (cu, cy) with half sizes hu × hy and corner radius r
        /// (<paramref name="seg"/> pieces per corner; 0 = sharp), clockwise (outward).</summary>
        public BridgeProfile RoundRect(double cu, double cy, double hu, double hy, double r, int seg, float ao = 1f)
        {
            Clear();
            r = Math.Max(0, Math.Min(r, Math.Min(hu, hy)));
            if (seg < 1 || r < 1e-4)
            {
                // Start mid-edge so every corner has both of its sides.
                Add(cu, cy + hy, ao).Add(cu + hu, cy + hy, ao).Hard().Add(cu + hu, cy - hy, ao).Hard().Add(cu - hu, cy - hy, ao).Hard()
                    .Add(cu - hu, cy + hy, ao).Hard().Add(cu, cy + hy, ao);
                Closed = false;
                return Finish();
            }
            // Clockwise: top-left corner, top-right, bottom-right, bottom-left (angles decreasing).
            Arc(cu - hu + r, cy + hy - r, r, Math.PI, 0.5 * Math.PI, seg, ao);
            Arc(cu + hu - r, cy + hy - r, r, 0.5 * Math.PI, 0, seg, ao);
            Arc(cu + hu - r, cy - hy + r, r, 0, -0.5 * Math.PI, seg, ao);
            Arc(cu - hu + r, cy - hy + r, r, -0.5 * Math.PI, -Math.PI, seg, ao);
            Add(U[0], Y[0], ao);
            Closed = true;
            return Finish();
        }

        /// <summary>Make the last point a hard corner (duplicated, each copy shaded by its own side).</summary>
        public BridgeProfile Hard()
        {
            if (Count > 0) Add(U[Count - 1], Y[Count - 1], Ao[Count - 1], C[Count - 1], Ch[Count - 1]);
            return this;
        }

        /// <summary>A rounded corner: an arc about (cu, cy) of radius r from angle a0 to a1 (radians, measured from
        /// +u toward +y), <paramref name="segments"/> pieces (the end points included).</summary>
        public BridgeProfile Arc(double cu, double cy, double r, double a0, double a1, int segments, float ao = 1f)
        {
            if (segments < 1) segments = 1;
            for (int i = 0; i <= segments; i++)
            {
                double a = a0 + (a1 - a0) * i / segments;
                Add(cu + r * Math.Cos(a), cy + r * Math.Sin(a), ao);
            }
            return this;
        }

        /// <summary>A full circle of radius r about (cu, cy), clockwise (outward), <paramref name="sides"/> sides; closed.</summary>
        public BridgeProfile Circle(double cu, double cy, double r, int sides, float ao = 1f)
        {
            Clear();
            if (sides < 3) sides = 3;
            for (int i = 0; i <= sides; i++)
            {
                double a = -2.0 * Math.PI * i / sides;
                Add(cu + r * Math.Cos(a), cy + r * Math.Sin(a), ao);
            }
            Closed = true;
            return Finish();
        }

        /// <summary>The same profile mirrored to −u (order reversed so it still faces outward).</summary>
        public BridgeProfile MirrorInto(BridgeProfile dst)
        {
            dst.Clear();
            for (int i = Count - 1; i >= 0; i--) dst.Add(-U[i], Y[i], Ao[i], C[i], Ch[i]);
            dst.Closed = Closed;
            return dst.Finish();
        }

        /// <summary>Compute the point normals (call after the last point).</summary>
        public BridgeProfile Finish()
        {
            for (int i = 0; i < Count; i++)
            {
                double inU = 0, inY = 0, outU = 0, outY = 0;
                bool hasIn = SegmentNormal(i - 1, out inU, out inY);
                bool hasOut = SegmentNormal(i, out outU, out outY);
                bool dupNext = i + 1 < Count && Same(i, i + 1);
                bool dupPrev = i > 0 && Same(i, i - 1);
                double nu, ny;
                if (dupNext && hasIn) { nu = inU; ny = inY; }
                else if (dupPrev && hasOut) { nu = outU; ny = outY; }
                else if (hasIn && hasOut) { nu = inU + outU; ny = inY + outY; }
                else if (hasIn) { nu = inU; ny = inY; }
                else if (hasOut) { nu = outU; ny = outY; }
                else { nu = 0; ny = 1; }
                double l = Math.Sqrt(nu * nu + ny * ny);
                if (l < 1e-9)
                {
                    nu = hasOut ? outU : 0;
                    ny = hasOut ? outY : 1;
                    l = Math.Sqrt(nu * nu + ny * ny);
                    if (l < 1e-9) { nu = 0; ny = 1; l = 1; }
                }
                NU[i] = nu / l;
                NY[i] = ny / l;
            }
            return this;
        }

        private bool Same(int a, int b)
        {
            return Math.Abs(U[a] - U[b]) < 1e-9 && Math.Abs(Y[a] - Y[b]) < 1e-9;
        }

        /// <summary>Unit outward normal of segment (i, i + 1) (wrapping when closed); false when it is degenerate or
        /// does not exist.</summary>
        private bool SegmentNormal(int i, out double nu, out double ny)
        {
            nu = ny = 0;
            int n = Count;
            if (Closed)
            {
                // The last point repeats the first: segment indices run 0 .. n - 2.
                int m = n - 1;
                if (m < 2) return false;
                i = ((i % m) + m) % m;
            }
            else if (i < 0 || i + 1 >= n)
            {
                return false;
            }
            double du = U[i + 1] - U[i], dy = Y[i + 1] - Y[i];
            double l = Math.Sqrt(du * du + dy * dy);
            if (l < 1e-9) return false;
            nu = -dy / l;
            ny = du / l;
            return true;
        }
    }

    /// <summary>
    /// A deck centreline for <see cref="BridgeKit.Sweep"/>: stations with tile-local plan positions, the offset
    /// vector U per metre (unit left normal × miter at a joint, or the border direction at a cut end, so a section
    /// lies on the tile border there), the unit shading normal N (left), the along distance S and the deck height Y.
    /// Between stations the frame is interpolated linearly with the segment's own unit left normal. Key stations
    /// (road points, zone edges) carry every change of the deck; the others only subdivide long segments, so the
    /// sweeps run through the key stations alone. <see cref="Near"/>, <see cref="Mid"/> and <see cref="Far"/> thin the
    /// key stations per LOD (a Douglas-Peucker pass by the layout: the stations whose removal moves the deck, its widths
    /// or depth by more than millimetres near, a few centimetres mid, or a decimetre or two far away, stay).
    /// </summary>
    public sealed class BridgePath
    {
        public double[] X = new double[32], Z = new double[32], Ux = new double[32], Uz = new double[32];
        public double[] Nx = new double[32], Nz = new double[32], S = new double[32];
        public float[] Y = new float[32];
        public bool[] Key = new bool[32];

        /// <summary>The key stations the LOD0 (<see cref="Near"/>: collinear ones dropped), LOD1 (<see cref="Mid"/>)
        /// and LOD2 (<see cref="Far"/>) sweeps keep (every key station until the layout thins them).</summary>
        public bool[] Near = new bool[32], Mid = new bool[32], Far = new bool[32];

        public int Count;

        /// <summary>The station mask of a level of detail (0: <see cref="Near"/>, 1: <see cref="Mid"/>, 2:
        /// <see cref="Far"/>).</summary>
        public bool[] KeysFor(int lod)
        {
            return lod >= 2 ? Far : lod == 1 ? Mid : Near;
        }

        public void Clear()
        {
            Count = 0;
        }

        public void Add(double x, double z, double ux, double uz, double nx, double nz, double s, float y, bool key = true)
        {
            if (Count == X.Length)
            {
                int cap = Count * 2;
                Array.Resize(ref Key, cap);
                Array.Resize(ref Near, cap);
                Array.Resize(ref Mid, cap);
                Array.Resize(ref Far, cap);
                Array.Resize(ref X, cap);
                Array.Resize(ref Z, cap);
                Array.Resize(ref Ux, cap);
                Array.Resize(ref Uz, cap);
                Array.Resize(ref Nx, cap);
                Array.Resize(ref Nz, cap);
                Array.Resize(ref S, cap);
                Array.Resize(ref Y, cap);
            }
            X[Count] = x;
            Z[Count] = z;
            Ux[Count] = ux;
            Uz[Count] = uz;
            Nx[Count] = nx;
            Nz[Count] = nz;
            S[Count] = s;
            Y[Count] = y;
            Key[Count] = key;
            Near[Count] = key;
            Mid[Count] = key;
            Far[Count] = key;
            Count++;
        }

        public double Start
        {
            get { return Count == 0 ? 0 : S[0]; }
        }

        public double End
        {
            get { return Count == 0 ? 0 : S[Count - 1]; }
        }

        /// <summary>Index k of the segment (k, k + 1) holding <paramref name="s"/> (clamped).</summary>
        public int Segment(double s)
        {
            int lo = 0, hi = Count - 2;
            if (hi <= 0) return 0;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (S[mid] <= s) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>The frame at along <paramref name="s"/>: centre (x, z), deck y, offset vector (ux, uz), unit
        /// left normal (nx, nz) and unit tangent (tx, tz). Exactly a station's own vectors when s hits it.</summary>
        public void Frame(double s, out double x, out double z, out float y, out double ux, out double uz, out double nx, out double nz,
                          out double tx, out double tz)
        {
            int k = Segment(s);
            int j = Math.Min(k + 1, Count - 1);
            double len = S[j] - S[k];
            double f = len > 1e-9 ? (s - S[k]) / len : 0;
            if (f < 0) f = 0;
            else if (f > 1) f = 1;
            x = X[k] + (X[j] - X[k]) * f;
            z = Z[k] + (Z[j] - Z[k]) * f;
            y = (float)(Y[k] + (Y[j] - Y[k]) * f);
            double dx = X[j] - X[k], dz = Z[j] - Z[k], dl = Math.Sqrt(dx * dx + dz * dz);
            if (dl > 1e-9)
            {
                tx = dx / dl;
                tz = dz / dl;
            }
            else
            {
                tx = -Nz[k];
                tz = Nx[k];
            }
            if (f <= 1e-9)
            {
                ux = Ux[k];
                uz = Uz[k];
                nx = Nx[k];
                nz = Nz[k];
            }
            else if (f >= 1 - 1e-9)
            {
                ux = Ux[j];
                uz = Uz[j];
                nx = Nx[j];
                nz = Nz[j];
            }
            else
            {
                ux = nx = -tz;
                uz = nz = tx;
            }
        }

        /// <summary>Deck height at along <paramref name="s"/>.</summary>
        public float YAt(double s)
        {
            int k = Segment(s);
            int j = Math.Min(k + 1, Count - 1);
            double len = S[j] - S[k];
            double f = len > 1e-9 ? (s - S[k]) / len : 0;
            if (f < 0) f = 0;
            else if (f > 1) f = 1;
            return (float)(Y[k] + (Y[j] - Y[k]) * f);
        }
    }

    /// <summary>
    /// Rounded geometry for the bridge meshers (until the shared shapes library lands): profile sweeps along a
    /// <see cref="BridgePath"/>, vertical lofts of rounded rectangles and circles (columns, posts, caps, lamp poles,
    /// balusters), free tubes (lamp arms, stair rails) and flat quads. Every vertex gets UV0 =
    /// (<see cref="MaterialChannel"/>, baked AO). Triangles are wound so their front faces the side of their vertex
    /// normals (Unity: clockwise seen from the front), whatever the input order. No allocation beyond
    /// <see cref="MeshData"/> growth and the thread-static scratch.
    /// </summary>
    public static class BridgeKit
    {
        [ThreadStatic] private static double[] _sList;

        /// <summary>The along positions written by the last <see cref="BuildStations"/> on this thread.</summary>
        public static double[] Stations
        {
            get { return _sList; }
        }
        [ThreadStatic] private static double[] _rx, _rz, _rnx, _rnz;

        /// <summary>Add a vertex with the material channel and AO in UV0.</summary>
        public static int V(MeshData m, double x, double y, double z, double nx, double ny, double nz, uint c, MaterialChannel ch, float ao)
        {
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l < 1e-12)
            {
                nx = 0;
                ny = 1;
                nz = 0;
                l = 1;
            }
            if (ao < 0f) ao = 0f;
            else if (ao > 1f) ao = 1f;
            return m.AddVertex((float)x, (float)y, (float)z, (float)(nx / l), (float)(ny / l), (float)(nz / l), c, (float)ch, ao);
        }

        /// <summary>A triangle wound so its front faces the side of its vertex normals; degenerate ones are skipped.</summary>
        public static void Tri(MeshData m, int a, int b, int c)
        {
            float[] p = m.Positions, n = m.Normals;
            double ux = p[3 * b] - (double)p[3 * a], uy = p[3 * b + 1] - (double)p[3 * a + 1], uz = p[3 * b + 2] - (double)p[3 * a + 2];
            double vx = p[3 * c] - (double)p[3 * a], vy = p[3 * c + 1] - (double)p[3 * a + 1], vz = p[3 * c + 2] - (double)p[3 * a + 2];
            double fx = uy * vz - uz * vy, fy = uz * vx - ux * vz, fz = ux * vy - uy * vx;
            double area2 = fx * fx + fy * fy + fz * fz;
            if (area2 < 1e-14) return;
            double sx = n[3 * a] + n[3 * b] + n[3 * c], sy = n[3 * a + 1] + n[3 * b + 1] + n[3 * c + 1], sz = n[3 * a + 2] + n[3 * b + 2] + n[3 * c + 2];
            if (fx * sx + fy * sy + fz * sz >= 0) m.AddTriangle(a, b, c);
            else m.AddTriangle(a, c, b);
        }

        /// <summary>Quad a, b, c, d (around the perimeter) as two triangles facing their normals.</summary>
        public static void QuadIdx(MeshData m, int a, int b, int c, int d)
        {
            Tri(m, a, b, c);
            Tri(m, a, c, d);
        }

        /// <summary>A flat quad p0..p3 (around the perimeter) facing (hx, hy, hz).</summary>
        public static void Quad(MeshData m, double x0, double y0, double z0, double x1, double y1, double z1, double x2, double y2, double z2,
                                double x3, double y3, double z3, double hx, double hy, double hz, uint c, MaterialChannel ch, float ao0, float ao1)
        {
            int a = V(m, x0, y0, z0, hx, hy, hz, c, ch, ao0);
            int b = V(m, x1, y1, z1, hx, hy, hz, c, ch, ao0);
            int cc = V(m, x2, y2, z2, hx, hy, hz, c, ch, ao1);
            int d = V(m, x3, y3, z3, hx, hy, hz, c, ch, ao1);
            QuadIdx(m, a, b, cc, d);
        }

        /// <summary>
        /// Sweep <paramref name="p"/> along <paramref name="path"/> from <paramref name="s0"/> to <paramref name="s1"/>,
        /// offset by (<paramref name="du"/>, <paramref name="dy"/>) in the frame. Rings sit at every path station inside
        /// the range and every <paramref name="step"/> metres (0 = stations only). With <paramref name="stripe"/> &gt; 0
        /// the rings also split at world-anchored stripe boundaries (<see cref="StripeIndex"/>) and alternate stripes
        /// take <paramref name="c2"/> (separate vertices per stripe). <paramref name="capStart"/> and
        /// <paramref name="capEnd"/> close the ends with a fan (convex profiles only). Returns the triangles added.
        /// </summary>
        public static int Sweep(MeshData m, BridgePath path, double s0, double s1, BridgeProfile p, double du, double dy, uint c,
                                MaterialChannel ch, float ao, double step = 0, double stripe = 0, uint c2 = 0, double worldX0 = 0,
                                double worldZ0 = 0, bool capStart = false, bool capEnd = false)
        {
            if (path.Count < 2 || p.Count < 2 || s1 - s0 < 1e-4) return 0;
            int tris0 = m.IndexCount;
            int ns = BuildStations(path, s0, s1, step, stripe, worldX0, worldZ0);
            double[] sl = _sList;
            int n = p.Count;
            m.Reserve(ns * n * (stripe > 0 ? 2 : 1), (ns - 1) * (n - 1) * 6);
            int prevRing = -1;
            uint prevColour = c;
            for (int k = 0; k < ns; k++)
            {
                double s = sl[k];
                uint segColour = prevColour;
                if (k < ns - 1)
                {
                    segColour = c;
                    if (stripe > 0 && (StripeIndex(path, 0.5 * (s + sl[k + 1]), stripe, worldX0, worldZ0) & 1) != 0) segColour = c2;
                }
                if (prevRing >= 0 && segColour != prevColour)
                {
                    // A stripe boundary: close the previous stripe with its own colour, start the next one.
                    int endRing = Ring(m, path, s, p, du, dy, prevColour, ch, ao);
                    Band(m, prevRing, endRing, n);
                    prevRing = Ring(m, path, s, p, du, dy, segColour, ch, ao);
                }
                else
                {
                    int ring = Ring(m, path, s, p, du, dy, segColour, ch, ao);
                    if (prevRing >= 0) Band(m, prevRing, ring, n);
                    prevRing = ring;
                }
                if (k == 0 && capStart) Cap(m, path, s, p, du, dy, segColour, ch, ao, -1);
                if (k == ns - 1 && capEnd) Cap(m, path, s, p, du, dy, segColour, ch, ao, +1);
                prevColour = segColour;
            }
            return (m.IndexCount - tris0) / 3;
        }

        /// <summary>The world-anchored stripe index at along <paramref name="s"/>: the position along the segment's
        /// dominant world axis divided by the stripe length (× the axis component), so two tiles cutting the same
        /// segment agree.</summary>
        public static long StripeIndex(BridgePath path, double s, double stripe, double worldX0, double worldZ0)
        {
            double x, z, ux, uz, nx, nz, tx, tz;
            float y;
            path.Frame(s, out x, out z, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            double a = Math.Abs(tx) >= Math.Abs(tz) ? (worldX0 + x) / Math.Max(Math.Abs(tx), 1e-6) : (worldZ0 + z) / Math.Max(Math.Abs(tz), 1e-6);
            return (long)Math.Floor(a / stripe);
        }

        /// <summary>The along positions of a sweep (see <see cref="Sweep"/>) in <see cref="Stations"/>; returns the count.
        /// With <paramref name="keyOnly"/> only the key path stations (and the range ends) are used, thinned for
        /// <paramref name="lod"/> (<see cref="BridgePath.KeysFor"/>).</summary>
        public static int BuildStations(BridgePath path, double s0, double s1, double step, double stripe, double wx0, double wz0, bool keyOnly = false,
                                        int lod = 0)
        {
            bool[] keys = path.KeysFor(lod);
            int cap = path.Count + 4 + (step > 0 ? (int)((s1 - s0) / step) + 2 : 0) + (stripe > 0 ? (int)((s1 - s0) / stripe) + path.Count + 4 : 0);
            if (_sList == null || _sList.Length < cap) _sList = new double[Math.Max(cap, 256)];
            double[] sl = _sList;
            int n = 0;
            sl[n++] = s0;
            for (int k = 0; k < path.Count; k++)
                if (path.S[k] > s0 + 1e-4 && path.S[k] < s1 - 1e-4 && (!keyOnly || keys[k])) sl[n++] = path.S[k];
            if (step > 0)
                for (double t = s0 + step; t < s1 - 1e-4; t += step) sl[n++] = t;
            if (stripe > 0)
            {
                // Stripe boundaries per path segment: the world coordinate along the dominant axis, divided by the
                // axis component, grows by exactly 1 m per metre along the segment.
                for (int k = 0; k + 1 < path.Count; k++)
                {
                    double a0 = Math.Max(path.S[k], s0), a1 = Math.Min(path.S[k + 1], s1);
                    if (a1 - a0 < 1e-4) continue;
                    double dx = path.X[k + 1] - path.X[k], dz = path.Z[k + 1] - path.Z[k], dl = Math.Sqrt(dx * dx + dz * dz);
                    if (dl < 1e-9) continue;
                    double tx = dx / dl, tz = dz / dl;
                    bool useX = Math.Abs(tx) >= Math.Abs(tz);
                    double t = useX ? tx : tz, at = Math.Max(Math.Abs(t), 1e-6);
                    double w = (useX ? wx0 + path.X[k] : wz0 + path.Z[k]) / at; // stripe coordinate at S[k]
                    double sign = t >= 0 ? 1 : -1;
                    // coordinate(s) = w + sign·(s − S[k]); boundaries where coordinate = j·stripe.
                    double c0 = w + sign * (a0 - path.S[k]), c1 = w + sign * (a1 - path.S[k]);
                    long j0 = (long)Math.Ceiling(Math.Min(c0, c1) / stripe), j1 = (long)Math.Floor(Math.Max(c0, c1) / stripe);
                    for (long j = j0; j <= j1 && n < sl.Length - 2; j++)
                    {
                        double sb = path.S[k] + (j * stripe - w) * sign;
                        if (sb > s0 + 1e-4 && sb < s1 - 1e-4) sl[n++] = sb;
                    }
                }
            }
            sl[n++] = s1;
            Array.Sort(sl, 0, n);
            int u = 1;
            for (int i = 1; i < n; i++)
                if (sl[i] - sl[u - 1] > 1e-4) sl[u++] = sl[i];
            sl[u - 1] = s1;
            return u;
        }

        /// <summary>One ring of <paramref name="p"/> at along <paramref name="s"/> (returns the first vertex).</summary>
        public static int Ring(MeshData m, BridgePath path, double s, BridgeProfile p, double du, double dy, uint c, MaterialChannel ch, float ao)
        {
            double x, z, ux, uz, nx, nz, tx, tz;
            float y;
            path.Frame(s, out x, out z, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            int first = m.VertexCount;
            for (int i = 0; i < p.Count; i++)
            {
                double u = du + p.U[i];
                V(m, x + ux * u, y + dy + p.Y[i], z + uz * u, nx * p.NU[i], p.NY[i], nz * p.NU[i], p.C[i] != 0 ? p.C[i] : c,
                  p.Ch[i] != 255 ? (MaterialChannel)p.Ch[i] : ch, ao * p.Ao[i]);
            }
            return first;
        }

        /// <summary>Quads between two rings of n points.</summary>
        public static void Band(MeshData m, int a, int b, int n)
        {
            for (int i = 0; i + 1 < n; i++) QuadIdx(m, a + i, a + i + 1, b + i + 1, b + i);
        }

        private static void Cap(MeshData m, BridgePath path, double s, BridgeProfile p, double du, double dy, uint c, MaterialChannel ch, float ao, int dir)
        {
            double x, z, ux, uz, nx, nz, tx, tz;
            float y;
            path.Frame(s, out x, out z, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            int n = p.Count - (p.Closed ? 1 : 0);
            if (n < 3) return;
            double cu = 0, cy = 0;
            for (int i = 0; i < n; i++)
            {
                cu += p.U[i];
                cy += p.Y[i];
            }
            cu /= n;
            cy /= n;
            int centre = V(m, x + ux * (du + cu), y + dy + cy, z + uz * (du + cu), tx * dir, 0, tz * dir, c, ch, ao);
            int first = m.VertexCount;
            for (int i = 0; i < n; i++)
            {
                double u = du + p.U[i];
                V(m, x + ux * u, y + dy + p.Y[i], z + uz * u, tx * dir, 0, tz * dir, c, ch, ao * p.Ao[i]);
            }
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? (p.Closed ? 0 : -1) : i + 1;
                if (j < 0) break;
                Tri(m, centre, first + i, first + j);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Vertical lofts
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A vertical loft of rounded rectangles about (cx, cz): local axis A at <paramref name="yaw"/> (radians from +X
        /// toward +Z) with half extent <paramref name="halfA"/>, axis B (A turned +90°) with <paramref name="halfB"/>,
        /// corner radius <paramref name="r"/> (≥ 1 cm; r = halfA = halfB gives a circle). Level i sits at height
        /// <paramref name="ly"/>[i] with every edge moved in by <paramref name="inset"/>[i] (negative flares out) and AO
        /// <paramref name="lao"/>[i]. Smooth across levels unless <paramref name="hard"/>[i] (may be null) splits the
        /// shading there. Caps close the bottom and top. <paramref name="arcSeg"/> segments per corner.
        /// </summary>
        public static int Loft(MeshData m, double cx, double cz, double yaw, double halfA, double halfB, double r, int arcSeg,
                               double[] ly, double[] inset, float[] lao, bool[] hard, int levels, bool capBottom, bool capTop, uint c,
                               MaterialChannel ch)
        {
            if (levels < 2) return 0;
            int tris0 = m.IndexCount;
            if (arcSeg < 1) arcSeg = 1;
            r = Math.Max(0.01, Math.Min(r, Math.Min(halfA, halfB)));
            int rn = 4 * (arcSeg + 1);
            EnsureRing(rn);
            double ca = Math.Cos(yaw), sa = Math.Sin(yaw);
            m.Reserve(levels * rn * 2 + 2 * (rn + 1), levels * rn * 6 + rn * 6);
            int prev = -1;
            for (int i = 0; i < levels; i++)
            {
                // Profile direction below and above the level for the normals.
                double gBelowY = 0, gBelowI = 0, gAboveY = 0, gAboveI = 0;
                bool below = i > 0, above = i + 1 < levels;
                if (below)
                {
                    gBelowY = ly[i] - ly[i - 1];
                    gBelowI = inset[i] - inset[i - 1];
                }
                if (above)
                {
                    gAboveY = ly[i + 1] - ly[i];
                    gAboveI = inset[i + 1] - inset[i];
                }
                bool split = hard != null && hard[i] && below && above;
                RingShape(halfA - inset[i], halfB - inset[i], Math.Max(0.005, r - inset[i]), arcSeg);
                if (split)
                {
                    int top = LoftRing(m, cx, cz, ca, sa, ly[i], rn, gBelowY, gBelowI, 0, 0, c, ch, lao[i]);
                    if (prev >= 0) LoftBand(m, prev, top, rn);
                    prev = LoftRing(m, cx, cz, ca, sa, ly[i], rn, 0, 0, gAboveY, gAboveI, c, ch, lao[i]);
                }
                else
                {
                    int ring = LoftRing(m, cx, cz, ca, sa, ly[i], rn, gBelowY, gBelowI, gAboveY, gAboveI, c, ch, lao[i]);
                    if (prev >= 0) LoftBand(m, prev, ring, rn);
                    prev = ring;
                }
                if (i == 0 && capBottom) LoftCap(m, cx, cz, ca, sa, ly[i], rn, -1, c, ch, lao[i]);
                if (i == levels - 1 && capTop) LoftCap(m, cx, cz, ca, sa, ly[i], rn, +1, c, ch, lao[i]);
            }
            return (m.IndexCount - tris0) / 3;
        }

        /// <summary>A vertical cylinder-like column: circle of radius r from y0 to y1 with a capped top.</summary>
        public static int Column(MeshData m, double cx, double cz, double r, double y0, double y1, int sides, uint c, MaterialChannel ch,
                                 float ao0, float ao1)
        {
            Levels(2);
            _ly[0] = y0;
            _ly[1] = y1;
            _li[0] = _li[1] = 0;
            _la[0] = ao0;
            _la[1] = ao1;
            int seg = Math.Max(1, sides / 4 - 1);
            return Loft(m, cx, cz, 0, r, r, r, seg, _ly, _li, _la, null, 2, false, true, c, ch);
        }

        private static void EnsureRing(int n)
        {
            if (_rx != null && _rx.Length >= n) return;
            int cap = Math.Max(n, 64);
            _rx = new double[cap];
            _rz = new double[cap];
            _rnx = new double[cap];
            _rnz = new double[cap];
        }

        /// <summary>Local rounded-rectangle ring (A, B coordinates) counter-clockwise from above, with outward normals.</summary>
        private static void RingShape(double a, double b, double r, int seg)
        {
            if (a < 0.005) a = 0.005;
            if (b < 0.005) b = 0.005;
            if (r > a) r = a;
            if (r > b) r = b;
            int k = 0;
            for (int q = 0; q < 4; q++)
            {
                double sx = q == 0 || q == 3 ? 1 : -1, sz = q < 2 ? 1 : -1;
                double ccx = sx * (a - r), ccz = sz * (b - r);
                double a0 = q * 0.5 * Math.PI;
                for (int i = 0; i <= seg; i++)
                {
                    double t = a0 + 0.5 * Math.PI * i / seg;
                    double nx = Math.Cos(t), nz = Math.Sin(t);
                    _rx[k] = ccx + r * nx;
                    _rz[k] = ccz + r * nz;
                    _rnx[k] = nx;
                    _rnz[k] = nz;
                    k++;
                }
            }
        }

        private static int LoftRing(MeshData m, double cx, double cz, double ca, double sa, double y, int rn, double bY, double bI, double aY,
                                    double aI, uint c, MaterialChannel ch, float ao)
        {
            int first = m.VertexCount;
            for (int j = 0; j < rn; j++)
            {
                double lx = _rx[j], lz = _rz[j];
                double px = _rnx[j], pz = _rnz[j];
                // Normal in the (outward, up) plane from the profile tangents: (dy, dInset) per band.
                double h = 0, v = 0;
                double l1 = Math.Sqrt(bY * bY + bI * bI), l2 = Math.Sqrt(aY * aY + aI * aI);
                if (l1 > 1e-9)
                {
                    h += bY / l1;
                    v += bI / l1;
                }
                if (l2 > 1e-9)
                {
                    h += aY / l2;
                    v += aI / l2;
                }
                if (Math.Abs(h) < 1e-9 && Math.Abs(v) < 1e-9) h = 1;
                double wx = cx + lx * ca - lz * sa, wz = cz + lx * sa + lz * ca;
                double nwx = px * ca - pz * sa, nwz = px * sa + pz * ca;
                V(m, wx, y, wz, nwx * h, v, nwz * h, c, ch, ao);
            }
            return first;
        }

        private static void LoftBand(MeshData m, int a, int b, int rn)
        {
            for (int j = 0; j < rn; j++)
            {
                int j1 = j + 1 == rn ? 0 : j + 1;
                QuadIdx(m, a + j, a + j1, b + j1, b + j);
            }
        }

        private static void LoftCap(MeshData m, double cx, double cz, double ca, double sa, double y, int rn, int dir, uint c, MaterialChannel ch, float ao)
        {
            int centre = V(m, cx, y, cz, 0, dir, 0, c, ch, ao);
            int first = m.VertexCount;
            for (int j = 0; j < rn; j++)
            {
                double lx = _rx[j], lz = _rz[j];
                V(m, cx + lx * ca - lz * sa, y, cz + lx * sa + lz * ca, 0, dir, 0, c, ch, ao);
            }
            for (int j = 0; j < rn; j++) Tri(m, centre, first + j, first + (j + 1 == rn ? 0 : j + 1));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Tubes and boxes
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A round tube of radius <paramref name="r"/> through n points (xs, ys, zs), smooth along and around,
        /// with flat end caps when <paramref name="caps"/>. Frames use parallel transport from a horizontal side
        /// vector, so the tube never twists.</summary>
        public static int Tube(MeshData m, double[] xs, double[] ys, double[] zs, int n, double r, int sides, bool caps, uint c,
                               MaterialChannel ch, float ao)
        {
            if (n < 2) return 0;
            int tris0 = m.IndexCount;
            if (sides < 3) sides = 3;
            double sx = 0, sy = 0, sz = 0;
            int prev = -1;
            for (int i = 0; i < n; i++)
            {
                // Tangent: average of the adjacent segment directions.
                double tx = 0, ty = 0, tz = 0;
                if (i > 0) AddDir(xs[i] - xs[i - 1], ys[i] - ys[i - 1], zs[i] - zs[i - 1], ref tx, ref ty, ref tz);
                if (i + 1 < n) AddDir(xs[i + 1] - xs[i], ys[i + 1] - ys[i], zs[i + 1] - zs[i], ref tx, ref ty, ref tz);
                double tl = Math.Sqrt(tx * tx + ty * ty + tz * tz);
                if (tl < 1e-9) tl = 1;
                tx /= tl;
                ty /= tl;
                tz /= tl;
                if (i == 0)
                {
                    // Side vector: horizontal, perpendicular to the tangent (or +X for a vertical tube).
                    sx = -tz;
                    sy = 0;
                    sz = tx;
                    if (sx * sx + sz * sz < 1e-6)
                    {
                        sx = 1;
                        sz = 0;
                    }
                }
                // Re-orthogonalise the side vector against this tangent (parallel transport).
                double d = sx * tx + sy * ty + sz * tz;
                sx -= d * tx;
                sy -= d * ty;
                sz -= d * tz;
                double sl = Math.Sqrt(sx * sx + sy * sy + sz * sz);
                if (sl < 1e-9)
                {
                    sx = 1;
                    sy = 0;
                    sz = 0;
                    sl = 1;
                }
                sx /= sl;
                sy /= sl;
                sz /= sl;
                double bx = ty * sz - tz * sy, by = tz * sx - tx * sz, bz = tx * sy - ty * sx;
                int ring = m.VertexCount;
                for (int j = 0; j < sides; j++)
                {
                    double a = 2 * Math.PI * j / sides, ca = Math.Cos(a), sa = Math.Sin(a);
                    double nx = sx * ca + bx * sa, ny = sy * ca + by * sa, nz = sz * ca + bz * sa;
                    V(m, xs[i] + nx * r, ys[i] + ny * r, zs[i] + nz * r, nx, ny, nz, c, ch, ao);
                }
                if (prev >= 0)
                    for (int j = 0; j < sides; j++)
                    {
                        int j1 = j + 1 == sides ? 0 : j + 1;
                        QuadIdx(m, prev + j, prev + j1, ring + j1, ring + j);
                    }
                if (caps && (i == 0 || i == n - 1))
                {
                    double dir = i == 0 ? -1 : 1;
                    int centre = V(m, xs[i], ys[i], zs[i], tx * dir, ty * dir, tz * dir, c, ch, ao);
                    int f = m.VertexCount;
                    for (int j = 0; j < sides; j++)
                    {
                        double a = 2 * Math.PI * j / sides, ca = Math.Cos(a), sa = Math.Sin(a);
                        double nx = sx * ca + bx * sa, ny = sy * ca + by * sa, nz = sz * ca + bz * sa;
                        V(m, xs[i] + nx * r, ys[i] + ny * r, zs[i] + nz * r, tx * dir, ty * dir, tz * dir, c, ch, ao);
                    }
                    for (int j = 0; j < sides; j++) Tri(m, centre, f + j, f + (j + 1 == sides ? 0 : j + 1));
                }
                prev = ring;
            }
            return (m.IndexCount - tris0) / 3;
        }

        private static void AddDir(double dx, double dy, double dz, ref double tx, ref double ty, ref double tz)
        {
            double l = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (l < 1e-9) return;
            tx += dx / l;
            ty += dy / l;
            tz += dz / l;
        }

        /// <summary>
        /// A bevelled box (the shading rounds over the bevel) in a horizontal frame: centre (cx, cz), axis A at
        /// <paramref name="yaw"/>, half extents halfA × halfB in plan, from y0 to y1, with every edge chamfered by
        /// <paramref name="bevel"/> (top edges rounded in two steps). Built as a loft, so it is closed.
        /// </summary>
        public static int RoundedBox(MeshData m, double cx, double cz, double yaw, double halfA, double halfB, double y0, double y1, double bevel,
                                     int arcSeg, uint c, MaterialChannel ch, float aoBottom, float aoTop)
        {
            double h = y1 - y0;
            double b = Math.Max(0.005, Math.Min(bevel, Math.Min(Math.Min(halfA, halfB) * 0.9, h * 0.45)));
            Levels(5);
            _ly[0] = y0;
            _ly[1] = y0 + b * 0.3;
            _ly[2] = y1 - b;
            _ly[3] = y1 - b * 0.3;
            _ly[4] = y1;
            _li[0] = b * 0.3;
            _li[1] = _li[2] = 0;
            _li[3] = b * 0.3;
            _li[4] = b;
            _la[0] = _la[1] = aoBottom;
            _la[2] = _la[3] = _la[4] = aoTop;
            return Loft(m, cx, cz, yaw, halfA, halfB, b, arcSeg, _ly, _li, _la, null, 5, true, true, c, ch);
        }

        /// <summary>A plain oriented box (the far LODs' posts, blocks and caps): centre (cx, cz), axis A at
        /// <paramref name="yaw"/>, half extents halfA × halfB, from y0 to y1; four sides and the top (10 triangles),
        /// the bottom too when <paramref name="bottom"/>.</summary>
        public static void Box(MeshData m, double cx, double cz, double yaw, double halfA, double halfB, double y0, double y1, uint c, MaterialChannel ch,
                               float aoBottom, float aoTop, bool bottom = false)
        {
            double ca = Math.Cos(yaw), sa = Math.Sin(yaw);
            double ax = ca * halfA, az = sa * halfA, bx = -sa * halfB, bz = ca * halfB;
            // Corners counter-clockwise from above: (−A −B), (+A −B), (+A +B), (−A +B).
            double x0 = cx - ax - bx, z0 = cz - az - bz, x1 = cx + ax - bx, z1 = cz + az - bz;
            double x2 = cx + ax + bx, z2 = cz + az + bz, x3 = cx - ax + bx, z3 = cz - az + bz;
            Quad(m, x0, y0, z0, x1, y0, z1, x1, y1, z1, x0, y1, z0, sa, 0, -ca, c, ch, aoBottom, aoTop);
            Quad(m, x1, y0, z1, x2, y0, z2, x2, y1, z2, x1, y1, z1, ca, 0, sa, c, ch, aoBottom, aoTop);
            Quad(m, x2, y0, z2, x3, y0, z3, x3, y1, z3, x2, y1, z2, -sa, 0, ca, c, ch, aoBottom, aoTop);
            Quad(m, x3, y0, z3, x0, y0, z0, x0, y1, z0, x3, y1, z3, -ca, 0, -sa, c, ch, aoBottom, aoTop);
            Quad(m, x0, y1, z0, x1, y1, z1, x2, y1, z2, x3, y1, z3, 0, 1, 0, c, ch, aoTop, aoTop);
            if (bottom) Quad(m, x0, y0, z0, x3, y0, z3, x2, y0, z2, x1, y0, z1, 0, -1, 0, c, ch, aoBottom, aoBottom);
        }

        /// <summary>A square post with a chamfered cap (the near LOD's railing posts): a shaft of half width
        /// <paramref name="half"/> from y0 to y1, then a cap rising <paramref name="capH"/> to a top inset by
        /// <paramref name="inset"/> (a pyramid when the inset reaches the half width): 12 to 18 triangles.</summary>
        public static void CappedPost(MeshData m, double cx, double cz, double yaw, double half, double y0, double y1, double capH, double inset, uint c,
                                      MaterialChannel ch, float aoBottom, float aoTop)
        {
            CappedBlock(m, cx, cz, yaw, half, half, y0, y1, capH, inset, c, ch, aoBottom, aoTop);
        }

        /// <summary>A rectangular block (axis A at <paramref name="yaw"/>, half extents halfA × halfB) from y0 to y1
        /// with a chamfered cap rising <paramref name="capH"/> to a top inset by <paramref name="inset"/> on every side.</summary>
        public static void CappedBlock(MeshData m, double cx, double cz, double yaw, double halfA, double halfB, double y0, double y1, double capH,
                                       double inset, uint c, MaterialChannel ch, float aoBottom, float aoTop)
        {
            double ca = Math.Cos(yaw), sa = Math.Sin(yaw);
            double ia = Math.Max(0.0, halfA - inset), ib = Math.Max(0.0, halfB - inset), yc = y1 + capH;
            bool pyramid = ia < 1e-4 && ib < 1e-4;
            for (int f = 0; f < 4; f++)
            {
                // Face f: outward along +A, +B, −A, −B.
                double nx = f == 0 ? ca : f == 1 ? -sa : f == 2 ? -ca : sa, nz = f == 0 ? sa : f == 1 ? ca : f == 2 ? -sa : -ca;
                double tx = -nz, tz = nx; // along the face
                double hn = f % 2 == 0 ? halfA : halfB, ht = f % 2 == 0 ? halfB : halfA;
                double ihn = f % 2 == 0 ? ia : ib, iht = f % 2 == 0 ? ib : ia;
                double ox = cx + nx * hn, oz = cz + nz * hn, ix = cx + nx * ihn, iz = cz + nz * ihn;
                Quad(m, ox - tx * ht, y0, oz - tz * ht, ox + tx * ht, y0, oz + tz * ht, ox + tx * ht, y1, oz + tz * ht, ox - tx * ht, y1, oz - tz * ht, nx, 0,
                     nz, c, ch, aoBottom, aoTop);
                if (capH <= 1e-6) continue;
                double sl = (hn - ihn) / capH;
                if (pyramid)
                {
                    int a = V(m, ox - tx * ht, y1, oz - tz * ht, nx, sl, nz, c, ch, aoTop);
                    int b = V(m, ox + tx * ht, y1, oz + tz * ht, nx, sl, nz, c, ch, aoTop);
                    int t = V(m, cx, yc, cz, nx, sl, nz, c, ch, aoTop);
                    Tri(m, a, b, t);
                }
                else
                {
                    Quad(m, ox - tx * ht, y1, oz - tz * ht, ox + tx * ht, y1, oz + tz * ht, ix + tx * iht, yc, iz + tz * iht, ix - tx * iht, yc, iz - tz * iht, nx,
                         sl, nz, c, ch, aoTop, aoTop);
                }
            }
            double topY = capH > 1e-6 ? yc : y1;
            if (pyramid && capH > 1e-6) return;
            double qa = capH > 1e-6 ? ia : halfA, qb = capH > 1e-6 ? ib : halfB;
            double ax = ca * qa, az = sa * qa, bx = -sa * qb, bz = ca * qb;
            Quad(m, cx - ax - bx, topY, cz - az - bz, cx + ax - bx, topY, cz + az - bz, cx + ax + bx, topY, cz + az + bz, cx - ax + bx, topY, cz - az + bz, 0, 1,
                 0, c, ch, aoTop, aoTop);
        }

        [ThreadStatic] private static double[] _ly, _li;
        [ThreadStatic] private static float[] _la;

        /// <summary>Thread-static level buffers (heights, insets, AO) for callers composing their own lofts; valid until
        /// the next kit call on this thread.</summary>
        public static void Levels(int n, out double[] ly, out double[] inset, out float[] ao)
        {
            Levels(n);
            ly = _ly;
            inset = _li;
            ao = _la;
        }

        private static void Levels(int n)
        {
            if (_ly != null && _ly.Length >= n) return;
            int cap = Math.Max(n, 32);
            _ly = new double[cap];
            _li = new double[cap];
            _la = new float[cap];
        }
    }
}
