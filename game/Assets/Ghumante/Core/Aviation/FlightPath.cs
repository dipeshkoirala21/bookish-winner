using System;
using System.Collections.Generic;

namespace Ghumante.Core.Aviation
{
    /// <summary>
    /// A flight path through procedure points (W2_DESIGN 8.3): a centripetal Catmull-Rom spline (α = 0.5, no cusps or
    /// self-intersections) over (x, z, altitude), sampled every ~25 m with the arc length, and the ground under it
    /// interpolated linearly between the points' <c>ground_m</c>. Terrain floor: outside 5 km of the runway every sample
    /// stays at least 300 m above the ground (the imported tables dip below that on a few approach points; the floor
    /// lifts the path, never lowers it). Immutable; positions are game metres, heights metres above sea level.
    /// </summary>
    public sealed class FlightPath
    {
        public const float SampleM = 25f;
        public const float ClearanceM = 300f, ClearanceOutsideM = 5000f;

        public readonly double[] X, Z;
        public readonly float[] Y, Ground, S;
        public readonly int N;
        public readonly float Length;

        private FlightPath(List<double> x, List<double> z, List<float> y, List<float> g)
        {
            N = x.Count;
            X = x.ToArray();
            Z = z.ToArray();
            Y = y.ToArray();
            Ground = g.ToArray();
            S = new float[N];
            double s = 0;
            for (int i = 1; i < N; i++)
            {
                s += Math.Sqrt((X[i] - X[i - 1]) * (X[i] - X[i - 1]) + (Z[i] - Z[i - 1]) * (Z[i] - Z[i - 1]));
                S[i] = (float)s;
            }
            Length = (float)s;
        }

        /// <summary>Builds a path through <paramref name="pts"/>; <paramref name="floorUntilEndM"/> exempts the last metres
        /// before the end from the terrain floor (an arrival's final approach), <paramref name="floorFromStartM"/> the
        /// first metres (a departure's initial climb inside the 5 km zone is exempt anyway).</summary>
        public static FlightPath Build(IList<ProcedurePoint> pts, AviationConfig c, float floorUntilEndM = 0f)
        {
            if (pts == null || pts.Count < 2) throw new ArgumentException("a path needs two points");
            int n = pts.Count;
            var x = new List<double>();
            var z = new List<double>();
            var y = new List<float>();
            var g = new List<float>();
            for (int i = 0; i + 1 < n; i++)
            {
                // Phantom end points (reflections) keep the end segments well conditioned.
                ProcedurePoint p1 = pts[i], p2 = pts[i + 1];
                ProcedurePoint p0 = i > 0 ? pts[i - 1] : Reflect(p2, p1), p3 = i + 2 < n ? pts[i + 2] : Reflect(p1, p2);
                double seg = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Z - p1.Z) * (p2.Z - p1.Z));
                int steps = Math.Max(1, (int)Math.Ceiling(seg / SampleM));
                for (int k = 0; k < steps; k++)
                {
                    double t = k / (double)steps;
                    double sx, sz, sy;
                    Centripetal(p0, p1, p2, p3, t, out sx, out sz, out sy);
                    x.Add(sx);
                    z.Add(sz);
                    y.Add((float)sy);
                    g.Add(p1.GroundM + (p2.GroundM - p1.GroundM) * (float)t);
                }
            }
            ProcedurePoint last = pts[n - 1];
            x.Add(last.X);
            z.Add(last.Z);
            y.Add(last.AltM);
            g.Add(last.GroundM);
            // On the runway strip the ground is the runway surface (the tables' ground there is a DSM height).
            if (c != null)
                for (int i = 0; i < g.Count; i++)
                    if (c.DistanceToRunway(x[i], z[i]) < 0.5 * c.RunwayWidthM + 30.0)
                    {
                        g[i] = Math.Min(g[i], c.RunwayElevationAt(x[i], z[i]));
                        // The tables' runway points are a few metres off the sloped surface: never below it.
                        if (y[i] < g[i]) y[i] = g[i];
                    }
            var path = new FlightPath(x, z, y, g);
            path.ApplyFloor(c, floorUntilEndM);
            return path;
        }

        /// <summary>Raises samples outside 5 km of the runway to at least 300 m above the ground, blending the lift in and
        /// out over 2 km so the profile stays smooth.</summary>
        private void ApplyFloor(AviationConfig c, float exemptEndM)
        {
            var lift = new float[N];
            for (int i = 0; i < N; i++)
            {
                if (exemptEndM > 0f && Length - S[i] < exemptEndM) continue;
                if (c != null && c.DistanceToRunway(X[i], Z[i]) < ClearanceOutsideM) continue;
                float need = Ground[i] + ClearanceM - Y[i];
                if (need > 0f) lift[i] = need;
            }
            // Spread each lift over ±2 km (taking the maximum), then apply.
            var spread = new float[N];
            for (int i = 0; i < N; i++)
            {
                if (lift[i] <= 0f) continue;
                for (int j = 0; j < N; j++)
                {
                    float d = Math.Abs(S[j] - S[i]);
                    if (d > 2000f) continue;
                    float v = lift[i] * (1f - d / 2000f);
                    if (j == i) v = lift[i];
                    if (v > spread[j]) spread[j] = v;
                }
            }
            for (int i = 0; i < N; i++) Y[i] += spread[i];
        }

        /// <summary>The point mirrored through <paramref name="about"/>: 2·about − p.</summary>
        private static ProcedurePoint Reflect(ProcedurePoint p, ProcedurePoint about)
        {
            return new ProcedurePoint
            {
                X = 2 * about.X - p.X, Z = 2 * about.Z - p.Z, AltM = 2 * about.AltM - p.AltM, GroundM = about.GroundM,
            };
        }

        private static void Centripetal(ProcedurePoint p0, ProcedurePoint p1, ProcedurePoint p2, ProcedurePoint p3, double u,
                                        out double x, out double z, out double y)
        {
            // Altitude: linear along the segment (the tables give the profile; a spline would overshoot the glide slope).
            y = p1.AltM + (p2.AltM - p1.AltM) * u;
            double d01 = Knot(p0, p1), d12 = Knot(p1, p2), d23 = Knot(p2, p3);
            if (d12 < 1e-3)
            {
                x = p1.X;
                z = p1.Z;
                return;
            }
            // Degenerate neighbours (repeated points): fall back to the chord on that side.
            if (d01 < 1e-3) d01 = d12;
            if (d23 < 1e-3) d23 = d12;
            double t0 = 0, t1 = d01, t2 = t1 + d12, t3 = t2 + d23;
            double t = t1 + (t2 - t1) * u;
            x = Barry(p0.X, p1.X, p2.X, p3.X, t0, t1, t2, t3, t);
            z = Barry(p0.Z, p1.Z, p2.Z, p3.Z, t0, t1, t2, t3, t);
        }

        private static double Knot(ProcedurePoint a, ProcedurePoint b)
        {
            double d = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
            return Math.Sqrt(Math.Max(d, 1e-6));
        }

        private static double Barry(double p0, double p1, double p2, double p3, double t0, double t1, double t2, double t3, double t)
        {
            double a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            double a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            double a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            double b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            double b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        /// <summary>Position, height, heading (0 north, clockwise) and climb gradient (rise per metre) at arc length s.</summary>
        public void At(float s, out double x, out double z, out float y, out float heading, out float grade)
        {
            int i;
            if (s <= 0f) i = 0;
            else if (s >= Length) i = N - 2;
            else
            {
                int lo = 0, hi = N - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (S[mid] <= s) lo = mid;
                    else hi = mid;
                }
                i = lo;
            }
            float seg = S[i + 1] - S[i];
            float t = seg > 1e-6f ? Math.Max(0f, Math.Min(1f, (s - S[i]) / seg)) : 0f;
            double dx = X[i + 1] - X[i], dz = Z[i + 1] - Z[i];
            x = X[i] + dx * t;
            z = Z[i] + dz * t;
            y = Y[i] + (Y[i + 1] - Y[i]) * t;
            heading = (float)Math.Atan2(dx, dz);
            grade = seg > 1e-6f ? (Y[i + 1] - Y[i]) / seg : 0f;
        }

        /// <summary>Ground under arc length s (interpolated from the procedure table).</summary>
        public float GroundAt(float s)
        {
            int i = 0;
            while (i < N - 2 && S[i + 1] < s) i++;
            float seg = S[i + 1] - S[i];
            float t = seg > 1e-6f ? Math.Max(0f, Math.Min(1f, (s - S[i]) / seg)) : 0f;
            return Ground[i] + (Ground[i + 1] - Ground[i]) * t;
        }

        /// <summary>The reversed path (helicopter arrivals fly a departure corridor backwards).</summary>
        public static ProcedurePoint[] Reversed(ProcedurePoint[] pts)
        {
            var r = (ProcedurePoint[])pts.Clone();
            Array.Reverse(r);
            return r;
        }
    }
}
