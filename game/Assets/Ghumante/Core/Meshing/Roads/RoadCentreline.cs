using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>Why a <see cref="RoadCorner"/> got a smaller radius than its class wants.</summary>
    public enum CornerLimit : byte
    {
        /// <summary>The class radius fits.</summary>
        None = 0,

        /// <summary>The neighbouring segments are too short (shared with the next corner in proportion).</summary>
        Segment = 1,

        /// <summary>The arc would leave the mapped corner by more than the area allows (it would cut through the
        /// corner houses).</summary>
        Deviation = 2,

        /// <summary>A tile-border cut is too close (the curve stays straight through every cut).</summary>
        TileCut = 3,

        /// <summary>The corner lies in (or next to) a junction cap, which draws the turn itself.</summary>
        Junction = 4,
    }

    /// <summary>One corner (polyline vertex) of a <see cref="RoadCentreline"/> and the fillet drawn there.</summary>
    public struct RoadCorner
    {
        /// <summary>Raw along distance of the mapped vertex.</summary>
        public double S;

        /// <summary>The mapped vertex, tile-local metres.</summary>
        public double X, Z;

        /// <summary>Deflection between the incoming and outgoing segments, degrees (0 straight, 180 reversal).</summary>
        public float DeflectionDeg;

        /// <summary>The class and design-speed radius (<see cref="RoadCentreline.DesignRadiusM"/>).</summary>
        public float WantedRadiusM;

        /// <summary>Radius of the drawn arc; 0 when the corner is left sharp.</summary>
        public float RadiusM;

        /// <summary>Distance from the vertex to each tangent point of the arc.</summary>
        public float TangentM;

        /// <summary>The largest radius the geometry allowed (segments, deviation, cuts); the drawn radius is
        /// <c>min(WantedRadiusM, AllowedRadiusM)</c>.</summary>
        public float AllowedRadiusM;

        public CornerLimit Limit;
    }

    /// <summary>
    /// One end of a piece that continues smoothly into another piece of the same tile (a "knee": exactly two pieces end
    /// at the same point and nothing else passes through it). Both pieces draw half of one fillet arc and meet at its
    /// midpoint with the same position, tangent and section, so a bend between two OSM ways reads as one curve.
    /// </summary>
    public struct RoadKnee
    {
        /// <summary>True when this end is a knee.</summary>
        public bool Has;

        /// <summary>The other piece and whether it starts (true) or ends at the shared point.</summary>
        public int Other;

        public bool OtherStarts;

        /// <summary>Unit direction from the shared point along the other piece (away from the point).</summary>
        public double OutX, OutZ;

        /// <summary>Tangent length of the shared fillet (identical for both pieces), 0 = straight join.</summary>
        public double TangentM;

        /// <summary>Wanted tangent length of the shared corner (before the allocation), for the neighbour corner's
        /// share of the end segment.</summary>
        public double WantM;

        /// <summary>Carriageway width both ends are pinned to.</summary>
        public float WidthM;

        /// <summary>The piece whose ribbon lift both ends meet at (the higher of the two).</summary>
        public int LiftRoad;
    }

    /// <summary>
    /// The smoothed plan-view centreline of one road piece (owner feedback: no sharp corners). Built by
    /// <see cref="RoadLayout"/> from the mapped polyline: every corner gets a circular fillet whose radius comes from the
    /// road class and design speed (<see cref="DesignRadiusM"/>), limited by the room on the neighbouring segments
    /// (shared in proportion to what both corners want), by how far the arc may leave the mapped corner
    /// (<see cref="MaxDeviationM"/>), by tile-border cuts (the curve is straight through a cut, so both tiles agree on
    /// position, tangent and zero curvature there whatever they know of the way beyond the context point) and by junction
    /// caps (which draw the turn themselves). Arcs are densified at a fixed angular step and straights at the mesher's
    /// segment length, so there are no kinks or spikes.
    /// <para>
    /// Every point carries <see cref="S"/>, the raw along distance (metres along the mapped polyline from the first
    /// rendered point) it stands for, so all along-based data (width profiles, junction cuts, markings, props) keeps its
    /// meaning: <see cref="At"/> maps a raw along value to the smoothed curve. <see cref="L"/> is the true length along the
    /// smoothed curve. Tile-local metres. Immutable once built.
    /// </para>
    /// </summary>
    public sealed class RoadCentreline
    {
        /// <summary>Largest angle one arc segment turns through.</summary>
        public const double MaxArcStepDeg = 15.0;

        /// <summary>Smallest angle step of an arc (very wide roads on big radii).</summary>
        public const double MinArcStepDeg = 2.5;

        /// <summary>Largest chord error (sagitta) of an arc segment at the road's outer edge.</summary>
        public const double ArcToleranceM = 0.05;

        /// <summary>Shortest arc segment (chords below this merge into fewer, longer ones).</summary>
        public const double MinArcChordM = 0.5;

        /// <summary>Straight run kept on either side of a tile-border cut (zero curvature at the cut).</summary>
        public const double CutMarginM = 0.5;

        /// <summary>Straight run kept between a fillet and a junction cap's cut.</summary>
        public const double GapMarginM = 0.05;

        /// <summary>Deflections under this are left as they are (a mitred joint: the kink is invisible).</summary>
        public const double MinDeflectionDeg = 3.0;

        /// <summary>Smallest filleted deflection on footways, paths, steps, cycleways and bridleways (2.5 m wide trails:
        /// a mitred kink this small does not show, and their wiggly mapping would otherwise double their rows).</summary>
        public const double MinTrailDeflectionDeg = 12.0;

        /// <summary>The smallest deflection a piece of class <paramref name="c"/> fillets.</summary>
        public static double MinDeflectionDegFor(RoadClass c)
        {
            return RoadWidthModel.IsFootClass(c) ? MinTrailDeflectionDeg : MinDeflectionDeg;
        }

        public int Count;
        public double[] X, Z;

        /// <summary>Raw along distance of each point (non-decreasing).</summary>
        public double[] S;

        /// <summary>Distance along the smoothed curve from the first point.</summary>
        public double[] L;

        /// <summary>Unit tangent (travel direction in point order).</summary>
        public double[] Tx, Tz;

        /// <summary>Signed curvature at each point (1/m, positive turning left); 0 on straights.</summary>
        public float[] Curv;

        /// <summary>Every interior corner of the mapped polyline (and knee corners at the ends).</summary>
        public RoadCorner[] Corners = new RoadCorner[0];

        /// <summary>The piece's first / last point is a tile-border cut.</summary>
        public bool StartCut, EndCut;

        /// <summary>Raw rendered length of the piece (the last point's <see cref="S"/>).</summary>
        public double RawLengthM;

        public double LengthM
        {
            get { return Count > 0 ? L[Count - 1] : 0.0; }
        }

        private RoadCentreline(int capacity)
        {
            int c = Math.Max(4, capacity);
            X = new double[c];
            Z = new double[c];
            S = new double[c];
            L = new double[c];
            Tx = new double[c];
            Tz = new double[c];
            Curv = new float[c];
        }

        private void Add(double x, double z, double s, double tx, double tz, float k)
        {
            if (Count == X.Length)
            {
                int cap = Count * 2;
                Array.Resize(ref X, cap);
                Array.Resize(ref Z, cap);
                Array.Resize(ref S, cap);
                Array.Resize(ref L, cap);
                Array.Resize(ref Tx, cap);
                Array.Resize(ref Tz, cap);
                Array.Resize(ref Curv, cap);
            }
            if (Count > 0)
            {
                double dx = x - X[Count - 1], dz = z - Z[Count - 1];
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (d < 1e-6 && Math.Abs(s - S[Count - 1]) < 1e-9)
                {
                    // Same point: keep the later tangent and curvature (an arc start after a straight).
                    Tx[Count - 1] = tx;
                    Tz[Count - 1] = tz;
                    if (k != 0f) Curv[Count - 1] = k;
                    return;
                }
                L[Count] = L[Count - 1] + d;
            }
            else
            {
                L[0] = 0;
            }
            X[Count] = x;
            Z[Count] = z;
            S[Count] = Math.Max(s, Count > 0 ? S[Count - 1] : s);
            Tx[Count] = tx;
            Tz[Count] = tz;
            Curv[Count] = k;
            Count++;
        }

        // -------------------------------------------------------------------------------------------------------
        // Queries
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Index k of the segment [k, k + 1] holding raw along <paramref name="s"/> (clamped to the ends).</summary>
        public int SegmentAt(double s)
        {
            if (Count < 2) return 0;
            if (s <= S[0]) return 0;
            if (s >= S[Count - 1]) return Count - 2;
            int lo = 0, hi = Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (S[mid] <= s) lo = mid;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>Position and unit tangent of the smoothed curve at raw along <paramref name="s"/> (clamped).</summary>
        public void At(double s, out double x, out double z, out double tx, out double tz)
        {
            if (Count == 0)
            {
                x = z = 0;
                tx = 1;
                tz = 0;
                return;
            }
            if (Count == 1)
            {
                x = X[0];
                z = Z[0];
                tx = Tx[0];
                tz = Tz[0];
                return;
            }
            int k = SegmentAt(s);
            double ds = S[k + 1] - S[k];
            double f = ds > 1e-12 ? (s - S[k]) / ds : 0.0;
            f = f < 0 ? 0 : f > 1 ? 1 : f;
            x = X[k] + (X[k + 1] - X[k]) * f;
            z = Z[k] + (Z[k + 1] - Z[k]) * f;
            double ax = Tx[k] + (Tx[k + 1] - Tx[k]) * f, az = Tz[k] + (Tz[k + 1] - Tz[k]) * f;
            double l = Math.Sqrt(ax * ax + az * az);
            if (l < 1e-9)
            {
                ax = X[k + 1] - X[k];
                az = Z[k + 1] - Z[k];
                l = Math.Sqrt(ax * ax + az * az);
                if (l < 1e-12)
                {
                    ax = 1;
                    az = 0;
                    l = 1;
                }
            }
            tx = ax / l;
            tz = az / l;
        }

        /// <summary>Distance along the smoothed curve at raw along <paramref name="s"/>.</summary>
        public double ArcAt(double s)
        {
            if (Count < 2) return 0;
            int k = SegmentAt(s);
            double ds = S[k + 1] - S[k];
            double f = ds > 1e-12 ? (s - S[k]) / ds : 0.0;
            f = f < 0 ? 0 : f > 1 ? 1 : f;
            return L[k] + (L[k + 1] - L[k]) * f;
        }

        /// <summary>Raw along value at curve distance <paramref name="l"/> (inverse of <see cref="ArcAt"/>).</summary>
        public double AlongAtArc(double l)
        {
            if (Count < 2) return Count == 1 ? S[0] : 0;
            if (l <= 0) return S[0];
            if (l >= L[Count - 1]) return S[Count - 1];
            int lo = 0, hi = Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (L[mid] <= l) lo = mid;
                else hi = mid;
            }
            double dl = L[lo + 1] - L[lo];
            double f = dl > 1e-12 ? (l - L[lo]) / dl : 0.0;
            return S[lo] + (S[lo + 1] - S[lo]) * f;
        }

        /// <summary>Smallest arc radius drawn (0 when no corner was filleted).</summary>
        public float MinRadiusM
        {
            get
            {
                float best = 0f;
                foreach (RoadCorner c in Corners)
                    if (c.RadiusM > 0f && (best == 0f || c.RadiusM < best)) best = c.RadiusM;
                return best;
            }
        }

        /// <summary>
        /// Nearest point of the curve to (x, z): segment index, fraction along it, raw along value, signed lateral offset
        /// (positive left of the travel direction) and distance. False for an empty curve.
        /// </summary>
        public bool Nearest(double x, double z, out int segment, out double along, out double lateral, out double dist)
        {
            segment = 0;
            along = 0;
            lateral = 0;
            dist = double.PositiveInfinity;
            if (Count < 2) return false;
            for (int k = 0; k + 1 < Count; k++)
            {
                double ax = X[k], az = Z[k], dx = X[k + 1] - ax, dz = Z[k + 1] - az;
                double l2 = dx * dx + dz * dz;
                double f = l2 > 1e-12 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
                f = f < 0 ? 0 : f > 1 ? 1 : f;
                double px = ax + dx * f - x, pz = az + dz * f - z;
                double d = Math.Sqrt(px * px + pz * pz);
                if (d >= dist) continue;
                dist = d;
                segment = k;
                along = S[k] + (S[k + 1] - S[k]) * f;
                double l = Math.Sqrt(l2);
                lateral = l > 1e-9 ? (dx * (z - az) - dz * (x - ax)) / l : 0;
            }
            return true;
        }

        // -------------------------------------------------------------------------------------------------------
        // Design radii
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Smallest comfortable centreline radius by class and design speed (game metres): trunk 60, primary 40,
        /// secondary 30, tertiary 22, residential and unclassified 14, service, living street and track 10, pedestrian
        /// 8, footway, path and cycleway 6, steps 3; halved in old cores (at least 4 m); never under half the width + 1.5
        /// so the inner edge does not pinch.
        /// </summary>
        public static double DesignRadiusM(RoadClass c, AreaType area, double widthM)
        {
            double r;
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk: r = 60; break;
                case RoadClass.Primary: r = 40; break;
                case RoadClass.Secondary: r = 30; break;
                case RoadClass.Tertiary: r = 22; break;
                case RoadClass.Unclassified:
                case RoadClass.Residential:
                case RoadClass.Road:
                case RoadClass.Unknown: r = 14; break;
                case RoadClass.LivingStreet:
                case RoadClass.Service:
                case RoadClass.Track: r = 10; break;
                case RoadClass.Pedestrian: r = 8; break;
                case RoadClass.Steps: r = 3; break;
                default: r = 6; break;
            }
            if (area == AreaType.OldCore) r = Math.Max(4.0, 0.5 * r);
            return Math.Max(r, 0.5 * widthM + 1.5);
        }

        /// <summary>
        /// How far a fillet may leave the mapped corner (arc midpoint to vertex): old cores 2.5 m, other built-up areas
        /// 4 m, open country 8 m; trails half that. Bounds how much a corner house is trimmed (decision 1: rideability
        /// beats exact footprints).
        /// </summary>
        public static double MaxDeviationM(RoadClass c, AreaType area)
        {
            double d;
            switch (area)
            {
                case AreaType.OldCore: d = 2.5; break;
                case AreaType.Urban:
                case AreaType.PeriUrban:
                case AreaType.Unknown: d = 4.0; break;
                default: d = 8.0; break;
            }
            return RoadWidthModel.IsFootClass(c) ? 0.5 * d : d;
        }

        /// <summary>The tangent length a corner of deflection <paramref name="theta"/> (radians) wants: the design radius,
        /// capped so the arc stays within <paramref name="maxDev"/> of the vertex.</summary>
        internal static double WantedTangent(double theta, double radius, double maxDev, out bool devLimited)
        {
            devLimited = false;
            if (theta < MinDeflectionDeg * Math.PI / 180.0) return 0;
            double t = radius * Math.Tan(0.5 * Math.Min(theta, Math.PI - 1e-3));
            double q = Math.Tan(0.25 * theta);
            if (q > 1e-9)
            {
                double td = maxDev / q;
                if (td < t)
                {
                    t = td;
                    devLimited = true;
                }
            }
            return t;
        }

        // -------------------------------------------------------------------------------------------------------
        // Building
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Per-thread working arrays of <see cref="Build"/> (fetched once per call, never per element).</summary>
        private sealed class Work
        {
            public double[] Vx = new double[0], Vz = new double[0], Vs = new double[0], Dx = new double[0], Dz = new double[0], Len = new double[0];
            public double[] Want = new double[0], Tan = new double[0], Theta = new double[0], Turn = new double[0], RoomL = new double[0], RoomR = new double[0];
            public bool[] DevLim = new bool[0];
            public int[] Src = new int[0];
            public int LastSeg;
            public double EdgeM;

            public void Ensure(int n)
            {
                if (Vx.Length >= n) return;
                int c = Math.Max(n, 64);
                Vx = new double[c];
                Vz = new double[c];
                Vs = new double[c];
                Dx = new double[c];
                Dz = new double[c];
                Len = new double[c];
                Want = new double[c];
                Tan = new double[c];
                Theta = new double[c];
                Turn = new double[c];
                RoomL = new double[c];
                RoomR = new double[c];
                DevLim = new bool[c];
                Src = new int[2 * c + 4];
            }
        }

        [ThreadStatic] private static Work _work;

        /// <summary>
        /// Build the centreline of a piece. <paramref name="along"/> is the raw along value of every record point;
        /// <paramref name="gaps"/> holds junction-cap intervals (pairs in ascending S, may be null) that stay exactly on
        /// the mapped polyline; <paramref name="startKnee"/> / <paramref name="endKnee"/> continue the curve into a
        /// neighbouring piece.
        /// </summary>
        internal static RoadCentreline Build(RoadRecord r, double[] along, double radius, double maxDev, double maxSeg, RoadCut[] gaps,
                                             in RoadKnee startKnee, in RoadKnee endKnee)
        {
            return Build(r, along, radius, maxDev, maxSeg, gaps, startKnee, endKnee, 0.0);
        }

        /// <summary>As the overload above; <paramref name="edgeM"/> is the largest distance from the centreline to the
        /// road's outer edge (carriageway half width plus footpath), which sets the arc step: a chord error of at most
        /// <see cref="ArcToleranceM"/> there.</summary>
        internal static RoadCentreline Build(RoadRecord r, double[] along, double radius, double maxDev, double maxSeg, RoadCut[] gaps,
                                             in RoadKnee startKnee, in RoadKnee endKnee, double edgeM)
        {
            Work w = _work ?? (_work = new Work());
            w.EdgeM = edgeM < 0 ? 0 : edgeM;
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            w.Ensure(count + 2);
            // Distinct rendered vertices.
            int m = 0;
            for (int i = first; i <= last; i++)
            {
                double x = p[2 * i] / 100.0, z = p[2 * i + 1] / 100.0;
                if (m > 0)
                {
                    double ddx = x - w.Vx[m - 1], ddz = z - w.Vz[m - 1];
                    if (ddx * ddx + ddz * ddz < 1e-4) continue;
                }
                w.Vx[m] = x;
                w.Vz[m] = z;
                w.Vs[m] = along[i];
                w.Src[m] = i;
                m++;
            }
            m = Simplify(w, m, SimplifyToleranceM(r.RoadClass), gaps);
            if (radius > 0) m = MergeClusters(w, m, radius, maxDev, MinDeflectionDegFor(r.RoadClass) * Math.PI / 180.0, gaps, r.HasPrevContext,
                                              r.HasNextContext, startKnee.Has, endKnee.Has);
            var c = new RoadCentreline(Math.Max(8, 2 * m + 8))
            {
                StartCut = r.HasPrevContext, EndCut = r.HasNextContext, RawLengthM = last >= first ? along[last] - along[first] : 0,
            };
            w.LastSeg = m - 2;
            if (m < 2)
            {
                double x = p[2 * first] / 100.0, z = p[2 * first + 1] / 100.0;
                c.Add(x, z, 0, 1, 0, 0f);
                if (last > first) c.Add(p[2 * last] / 100.0, p[2 * last + 1] / 100.0, c.RawLengthM, 1, 0, 0f);
                return c;
            }
            for (int j = 0; j + 1 < m; j++)
            {
                double dx = w.Vx[j + 1] - w.Vx[j], dz = w.Vz[j + 1] - w.Vz[j], l = Math.Sqrt(dx * dx + dz * dz);
                w.Dx[j] = dx / l;
                w.Dz[j] = dz / l;
                w.Len[j] = l;
            }
            // Wanted tangent per vertex.
            double minDefl = MinDeflectionDegFor(r.RoadClass) * Math.PI / 180.0;
            for (int i = 0; i < m; i++)
            {
                w.Want[i] = 0;
                w.Theta[i] = 0;
                w.Turn[i] = 0;
                w.DevLim[i] = false;
                if (i == 0 || i == m - 1) continue;
                double cr = w.Dx[i - 1] * w.Dz[i] - w.Dz[i - 1] * w.Dx[i], dot = w.Dx[i - 1] * w.Dx[i] + w.Dz[i - 1] * w.Dz[i];
                double th = Math.Atan2(Math.Abs(cr), dot);
                w.Theta[i] = th;
                w.Turn[i] = cr >= 0 ? 1 : -1;
                if (InGap(gaps, w.Vs[i], 0.5) || th < minDefl) continue;
                bool dev;
                w.Want[i] = WantedTangent(th, radius, maxDev, out dev);
                w.DevLim[i] = dev;
            }
            if (startKnee.Has) w.Want[0] = startKnee.WantM;
            if (endKnee.Has) w.Want[m - 1] = endKnee.WantM;
            // Room on each side of every vertex: the whole segment, less a cut margin at tile-border cuts, and never
            // into a junction gap.
            for (int i = 0; i < m; i++)
            {
                w.RoomL[i] = i > 0 ? w.Len[i - 1] : 0;
                w.RoomR[i] = i + 1 < m ? w.Len[i] : 0;
            }
            if (r.HasPrevContext && m > 1) w.RoomL[1] = Math.Max(0, w.Len[0] - CutMarginM);
            if (r.HasNextContext && m > 1) w.RoomR[m - 2] = Math.Max(0, w.Len[m - 2] - CutMarginM);
            // A knee's fillet is fixed by the layout: the corner next to it gets the rest of the end segment.
            if (startKnee.Has && m > 2) w.RoomL[1] = Math.Min(w.RoomL[1], Math.Max(0, w.Len[0] - startKnee.TangentM));
            if (endKnee.Has && m > 2) w.RoomR[m - 2] = Math.Min(w.RoomR[m - 2], Math.Max(0, w.Len[m - 2] - endKnee.TangentM));
            if (gaps != null)
            {
                for (int i = 0; i < m; i++)
                {
                    for (int g = 0; g + 1 < gaps.Length; g += 2)
                    {
                        double a = gaps[g].S - GapMarginM, b = gaps[g + 1].S + GapMarginM;
                        if (a > w.Vs[i] && a - w.Vs[i] < w.RoomR[i]) w.RoomR[i] = a - w.Vs[i];
                        if (b < w.Vs[i] && w.Vs[i] - b < w.RoomL[i]) w.RoomL[i] = w.Vs[i] - b;
                    }
                    if (w.RoomL[i] < 0) w.RoomL[i] = 0;
                    if (w.RoomR[i] < 0) w.RoomR[i] = 0;
                }
            }
            // Allocation: on a segment where both ends want more than it holds, each gets its share in proportion.
            var corners = new RoadCorner[Math.Max(0, m - 2) + (startKnee.Has ? 1 : 0) + (endKnee.Has ? 1 : 0)];
            int nc = 0;
            for (int i = 0; i < m; i++)
            {
                w.Tan[i] = 0;
                bool knee = i == 0 && startKnee.Has || i == m - 1 && endKnee.Has;
                if (knee)
                {
                    w.Tan[i] = i == 0 ? startKnee.TangentM : endKnee.TangentM;
                    continue;
                }
                if (i == 0 || i == m - 1 || w.Want[i] <= 0) continue;
                double wi = w.Want[i];
                double shareL = Share(w.Len[i - 1], wi, w.Want[i - 1]), shareR = Share(w.Len[i], wi, w.Want[i + 1]);
                double t = Math.Min(wi, Math.Min(Math.Min(shareL, shareR), Math.Min(w.RoomL[i], w.RoomR[i])));
                w.Tan[i] = t < 1e-3 ? 0 : t;
            }
            // Corner records.
            for (int i = 0; i < m; i++)
            {
                bool knee = i == 0 && startKnee.Has || i == m - 1 && endKnee.Has;
                if (!knee && (i == 0 || i == m - 1)) continue;
                double th;
                if (knee)
                {
                    double ix, iz, ox, oz;
                    KneeLegs(w, i == 0, i == 0 ? startKnee : endKnee, out ix, out iz, out ox, out oz);
                    th = Math.Atan2(Math.Abs(ix * oz - iz * ox), ix * ox + iz * oz);
                }
                else
                {
                    th = w.Theta[i];
                }
                var rc = new RoadCorner
                {
                    S = w.Vs[i], X = w.Vx[i], Z = w.Vz[i], DeflectionDeg = (float)(th * 180.0 / Math.PI), WantedRadiusM = (float)radius,
                    TangentM = (float)w.Tan[i],
                };
                double tanHalf = Math.Tan(0.5 * Math.Min(th, Math.PI - 1e-3));
                rc.RadiusM = w.Tan[i] > 0 && tanHalf > 1e-9 ? (float)(w.Tan[i] / tanHalf) : 0f;
                double wantT = radius * tanHalf;
                rc.AllowedRadiusM = tanHalf > 1e-9 ? (float)(Math.Max(w.Tan[i], 0) / tanHalf) : float.PositiveInfinity;
                if (th < minDefl && !knee)
                {
                    rc.Limit = CornerLimit.None;
                    rc.AllowedRadiusM = float.PositiveInfinity;
                }
                else if (w.Tan[i] >= wantT - 1e-6)
                {
                    rc.Limit = CornerLimit.None;
                    rc.AllowedRadiusM = float.PositiveInfinity;
                }
                else if (!knee && InGap(gaps, w.Vs[i], 0.5)) rc.Limit = CornerLimit.Junction;
                else if (w.DevLim[i] && w.Tan[i] >= w.Want[i] - 1e-6) rc.Limit = CornerLimit.Deviation;
                else if (!knee && (i == 1 && r.HasPrevContext && w.RoomL[i] <= w.Tan[i] + 1e-6 ||
                                   i == m - 2 && r.HasNextContext && w.RoomR[i] <= w.Tan[i] + 1e-6)) rc.Limit = CornerLimit.TileCut;
                else if (!knee && (w.RoomL[i] < w.Len[i - 1] - 1e-6 && w.RoomL[i] <= w.Tan[i] + 1e-6 ||
                                   w.RoomR[i] < w.Len[i] - 1e-6 && w.RoomR[i] <= w.Tan[i] + 1e-6)) rc.Limit = CornerLimit.Junction;
                else rc.Limit = CornerLimit.Segment;
                corners[nc++] = rc;
            }
            if (nc < corners.Length) Array.Resize(ref corners, nc);
            c.Corners = corners;
            Emit(w, c, m, maxSeg, startKnee, endKnee);
            // Tile-border cut ends: the tangent both tiles agree on bit for bit, the mapped direction from the vertex
            // before the cut to the vertex after it (one is this tile's, the other its context point; the neighbour holds
            // the same two), so the border-oblique end sections of both pieces span exactly the same points.
            if (r.HasPrevContext && count >= 3) EndTangent(c, true, p[4] - p[0], p[5] - p[1]);
            if (r.HasNextContext && count >= 3)
                EndTangent(c, false, p[2 * (count - 1)] - p[2 * (count - 3)], p[2 * (count - 1) + 1] - p[2 * (count - 3) + 1]);
            return c;
        }

        private static void EndTangent(RoadCentreline c, bool start, int dxCm, int dzCm)
        {
            double dx = dxCm / 100.0, dz = dzCm / 100.0, l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-9 || c.Count < 1) return;
            int k = start ? 0 : c.Count - 1;
            c.Tx[k] = dx / l;
            c.Tz[k] = dz / l;
        }

        /// <summary>Douglas-Peucker tolerance of the mapped polyline before filleting: OSM mapping noise on footways, paths
        /// and steps (2.5 m wide) and on streets, well under what shows at their widths.</summary>
        public static double SimplifyToleranceM(RoadClass c)
        {
            return RoadWidthModel.IsFootClass(c) ? 0.25 : 0.08;
        }

        /// <summary>
        /// Drop vertices that lie within <paramref name="tol"/> of the chord of their kept neighbours (Douglas-Peucker),
        /// never the two vertices at each end (the end directions are shared with knees and with the neighbouring tile
        /// across a cut), nor the vertices around and inside junction gaps (cap cuts lie on the mapped polyline). Returns the
        /// new count; the arrays are compacted in place.
        /// </summary>
        private static int Simplify(Work w, int m, double tol, RoadCut[] gaps)
        {
            if (m <= 4 || tol <= 0) return m;
            bool[] keep = w.DevLim; // free until the wanted tangents are computed
            for (int i = 0; i < m; i++) keep[i] = i <= 1 || i >= m - 2;
            if (gaps != null)
            {
                for (int g = 0; g + 1 < gaps.Length; g += 2)
                {
                    double a = gaps[g].S, b = gaps[g + 1].S;
                    for (int i = 0; i < m; i++)
                    {
                        double s = w.Vs[i];
                        bool around = s >= a - 0.5 && s <= b + 0.5 || i + 1 < m && w.Vs[i] <= a && w.Vs[i + 1] >= a ||
                                      i > 0 && w.Vs[i - 1] <= b && w.Vs[i] >= b || i + 1 < m && w.Vs[i] <= b && w.Vs[i + 1] >= b ||
                                      i > 0 && w.Vs[i - 1] <= a && w.Vs[i] >= a;
                        if (around) keep[i] = true;
                    }
                }
            }
            // Iterative Douglas-Peucker between consecutive kept vertices.
            int[] stack = w.Src; // vertex sources are not needed after this point
            int sp = 0;
            int prev = 0;
            for (int i = 1; i < m; i++)
            {
                if (!keep[i]) continue;
                if (i - prev > 1)
                {
                    stack[sp++] = prev;
                    stack[sp++] = i;
                }
                prev = i;
            }
            while (sp > 0)
            {
                int b = stack[--sp], a = stack[--sp];
                double ax = w.Vx[a], az = w.Vz[a], dx = w.Vx[b] - ax, dz = w.Vz[b] - az, l = Math.Sqrt(dx * dx + dz * dz);
                int worst = -1;
                double worstD = tol;
                for (int i = a + 1; i < b; i++)
                {
                    double ox = w.Vx[i] - ax, oz = w.Vz[i] - az;
                    double d = l > 1e-9 ? Math.Abs(ox * dz - oz * dx) / l : Math.Sqrt(ox * ox + oz * oz);
                    if (d > worstD)
                    {
                        worstD = d;
                        worst = i;
                    }
                }
                if (worst < 0) continue;
                keep[worst] = true;
                if (worst - a > 1)
                {
                    stack[sp++] = a;
                    stack[sp++] = worst;
                }
                if (b - worst > 1)
                {
                    stack[sp++] = worst;
                    stack[sp++] = b;
                }
            }
            int n = 0;
            for (int i = 0; i < m; i++)
            {
                if (!keep[i]) continue;
                w.Vx[n] = w.Vx[i];
                w.Vz[n] = w.Vz[i];
                w.Vs[n] = w.Vs[i];
                n++;
            }
            return n;
        }

        /// <summary>A mapped vertex deflecting at least this much with a leg shorter than <see cref="SpikeLegM"/> (or four
        /// times the road's half reach) is dropped as a spike.</summary>
        public const double SpikeDeflectionDeg = 150.0;

        /// <summary>Shortest leg that makes a sharp reversal a real hairpin rather than a spike.</summary>
        public const double SpikeLegM = 10.0;

        /// <summary>Largest number of merge passes over a piece (each pass merges at least one pair or stops).</summary>
        private const int MaxMergePasses = 64;

        /// <summary>
        /// Merge corner clusters: two neighbouring corners whose wanted tangents do not fit on the short segment between
        /// them (a bend mapped as several vertices, or a jog of mapping noise inside a bend) become one corner at the
        /// intersection of the outer segments' lines, so the bend is filleted as a whole with its class radius instead of
        /// as a run of tight arcs. The outer segments keep their directions (knees and tile-border cuts see the same end
        /// directions), the merged vertex stays within half the deviation limit of the short segment, and nothing moves
        /// near a junction gap. Returns the new vertex count; the arrays are compacted in place.
        /// </summary>
        private static int MergeClusters(Work w, int m, double radius, double maxDev, double minDefl, RoadCut[] gaps, bool startCut, bool endCut,
                                         bool startKnee, bool endKnee)
        {
            for (int pass = 0; pass < MaxMergePasses && m >= 4; pass++)
            {
                bool merged = false;
                // Spikes first: a vertex where the way doubles back on a short leg is a mapping error, not a hairpin.
                for (int j = 1; j + 1 < m && m >= 4; j++)
                {
                    if (j == 1 && (startCut || startKnee) || j == m - 2 && (endCut || endKnee)) continue;
                    double ax = w.Vx[j] - w.Vx[j - 1], az = w.Vz[j] - w.Vz[j - 1], bx = w.Vx[j + 1] - w.Vx[j], bz = w.Vz[j + 1] - w.Vz[j];
                    double la = Math.Sqrt(ax * ax + az * az), lb = Math.Sqrt(bx * bx + bz * bz);
                    if (la < 1e-6 || lb < 1e-6) continue;
                    double th = Math.Atan2(Math.Abs(ax * bz - az * bx), ax * bx + az * bz);
                    if (th < SpikeDeflectionDeg * Math.PI / 180.0 || Math.Min(la, lb) > Math.Max(SpikeLegM, 4 * w.EdgeM)) continue;
                    if (NearGap(gaps, w.Vs[j - 1], w.Vs[j + 1])) continue;
                    for (int k = j; k + 1 < m; k++)
                    {
                        w.Vx[k] = w.Vx[k + 1];
                        w.Vz[k] = w.Vz[k + 1];
                        w.Vs[k] = w.Vs[k + 1];
                    }
                    m--;
                    merged = true;
                }
                for (int j = 1; j + 2 < m; j++)
                {
                    double ax = w.Vx[j] - w.Vx[j - 1], az = w.Vz[j] - w.Vz[j - 1];
                    double mx = w.Vx[j + 1] - w.Vx[j], mz = w.Vz[j + 1] - w.Vz[j];
                    double bx = w.Vx[j + 2] - w.Vx[j + 1], bz = w.Vz[j + 2] - w.Vz[j + 1];
                    double la = Math.Sqrt(ax * ax + az * az), lm = Math.Sqrt(mx * mx + mz * mz), lb = Math.Sqrt(bx * bx + bz * bz);
                    if (la < 1e-6 || lm < 1e-6 || lb < 1e-6) continue;
                    ax /= la;
                    az /= la;
                    mx /= lm;
                    mz /= lm;
                    bx /= lb;
                    bz /= lb;
                    double thA = Math.Atan2(Math.Abs(ax * mz - az * mx), ax * mx + az * mz);
                    double thB = Math.Atan2(Math.Abs(mx * bz - mz * bx), mx * bx + mz * bz);
                    if (thA < minDefl && thB < minDefl) continue;
                    bool devA, devB;
                    double tA = thA < minDefl ? 0 : WantedTangent(thA, radius, maxDev, out devA);
                    double tB = thB < minDefl ? 0 : WantedTangent(thB, radius, maxDev, out devB);
                    if (tA + tB <= lm) continue;
                    if (NearGap(gaps, w.Vs[j - 1], w.Vs[j + 2])) continue;
                    // X = V[j] + a * A = V[j + 1] - b * B.
                    double cr = ax * bz - az * bx;
                    if (Math.Abs(cr) < Math.Sin(minDefl)) continue;
                    double rx = w.Vx[j + 1] - w.Vx[j], rz = w.Vz[j + 1] - w.Vz[j];
                    double a = (rx * bz - rz * bx) / cr, b = (ax * rz - az * rx) / cr;
                    // The outer segments must keep a positive length (and their cut margin and knee tangent).
                    double minA = startCut && j == 1 ? CutMarginM + 0.05 : 0.05, minB = endCut && j + 2 == m - 1 ? CutMarginM + 0.05 : 0.05;
                    if (a < 0 && (-a > la - minA || j == 1 && startKnee)) continue;
                    if (b < 0 && (-b > lb - minB || j + 2 == m - 1 && endKnee)) continue;
                    double px = w.Vx[j] + a * ax, pz = w.Vz[j] + a * az;
                    double ox, oz;
                    if (RoadCorridor.PointSeg(px, pz, w.Vx[j], w.Vz[j], w.Vx[j + 1], w.Vz[j + 1], out ox, out oz) > 0.5 * maxDev) continue;
                    w.Vx[j] = px;
                    w.Vz[j] = pz;
                    w.Vs[j] = 0.5 * (w.Vs[j] + w.Vs[j + 1]);
                    for (int k = j + 1; k + 1 < m; k++)
                    {
                        w.Vx[k] = w.Vx[k + 1];
                        w.Vz[k] = w.Vz[k + 1];
                        w.Vs[k] = w.Vs[k + 1];
                    }
                    m--;
                    merged = true;
                }
                if (!merged) break;
            }
            return m;
        }

        /// <summary>True when a junction gap (with half a metre either side) touches raw along [s0, s1].</summary>
        private static bool NearGap(RoadCut[] gaps, double s0, double s1)
        {
            if (gaps == null) return false;
            for (int g = 0; g + 1 < gaps.Length; g += 2)
                if (gaps[g].S - 0.5 <= s1 && gaps[g + 1].S + 0.5 >= s0) return true;
            return false;
        }

        private static double Share(double len, double w, double other)
        {
            if (other <= 0 || w + other <= len) return Math.Min(w, len);
            return len * w / (w + other);
        }

        private static bool InGap(RoadCut[] gaps, double s, double margin)
        {
            if (gaps == null) return false;
            for (int g = 0; g + 1 < gaps.Length; g += 2)
                if (s >= gaps[g].S - margin && s <= gaps[g + 1].S + margin) return true;
            return false;
        }

        /// <summary>The knee's legs as (direction into the shared point, direction out of it) in curve order.</summary>
        private static void KneeLegs(Work w, bool atStart, in RoadKnee k, out double ix, out double iz, out double ox, out double oz)
        {
            if (atStart)
            {
                // The curve comes in along the other piece (reversed out-direction) and leaves along this piece.
                ix = -k.OutX;
                iz = -k.OutZ;
                ox = w.Dx[0];
                oz = w.Dz[0];
            }
            else
            {
                int m1 = w.LastSeg;
                ix = w.Dx[m1];
                iz = w.Dz[m1];
                ox = k.OutX;
                oz = k.OutZ;
            }
        }

        private static void Emit(Work w, RoadCentreline c, int m, double maxSeg, in RoadKnee startKnee, in RoadKnee endKnee)
        {
            w.LastSeg = m - 2;
            double step = MaxArcStepDeg * Math.PI / 180.0;
            double cx = w.Vx[0], cz = w.Vz[0], cs = w.Vs[0];
            if (startKnee.Has && startKnee.TangentM > 1e-3)
            {
                // Second half of the shared fillet: from its midpoint to the tangent point on the first segment.
                double ix, iz, ox, oz;
                KneeLegs(w, true, startKnee, out ix, out iz, out ox, out oz);
                Arc(w, c, w.Vx[0], w.Vz[0], w.Vs[0], ix, iz, ox, oz, startKnee.TangentM, step, 0.5, 1.0, 0.0, SpanS(w, 0, startKnee.TangentM));
                cx = c.X[c.Count - 1];
                cz = c.Z[c.Count - 1];
                cs = c.S[c.Count - 1];
            }
            else
            {
                c.Add(cx, cz, cs, w.Dx[0], w.Dz[0], 0f);
            }
            for (int i = 1; i < m; i++)
            {
                bool lastV = i == m - 1;
                double t = lastV ? (endKnee.Has ? endKnee.TangentM : 0) : w.Tan[i];
                if (t < 1e-3) t = 0;
                double ax = w.Vx[i] - w.Dx[i - 1] * t, az = w.Vz[i] - w.Dz[i - 1] * t, sa = w.Vs[i] - SpanS(w, i - 1, t);
                Straight(c, cx, cz, cs, ax, az, sa, w.Dx[i - 1], w.Dz[i - 1], maxSeg);
                if (t <= 0 && !lastV)
                {
                    // An unfilleted (nearly straight) vertex: the section is mitred along the bisector.
                    double bx = w.Dx[i - 1] + w.Dx[i], bz = w.Dz[i - 1] + w.Dz[i], bl = Math.Sqrt(bx * bx + bz * bz);
                    if (bl > 1e-9)
                    {
                        c.Tx[c.Count - 1] = bx / bl;
                        c.Tz[c.Count - 1] = bz / bl;
                    }
                }
                if (t > 0)
                {
                    if (lastV)
                    {
                        double ix, iz, ox, oz;
                        KneeLegs(w, false, endKnee, out ix, out iz, out ox, out oz);
                        Arc(w, c, w.Vx[i], w.Vz[i], w.Vs[i], ix, iz, ox, oz, t, step, 0.0, 0.5, -SpanS(w, i - 1, t), 0.0);
                    }
                    else
                    {
                        Arc(w, c, w.Vx[i], w.Vz[i], w.Vs[i], w.Dx[i - 1], w.Dz[i - 1], w.Dx[i], w.Dz[i], t, step, 0.0, 1.0, -SpanS(w, i - 1, t),
                            SpanS(w, i, t));
                    }
                }
                cx = c.X[c.Count - 1];
                cz = c.Z[c.Count - 1];
                cs = c.S[c.Count - 1];
            }
            // The last point always closes on the piece end (the knee midpoint maps to the end's raw along).
            c.S[c.Count - 1] = w.Vs[m - 1];
        }

        /// <summary>The raw along span of <paramref name="d"/> metres of segment <paramref name="seg"/> (the raw along values of
        /// its ends spread in proportion, so a merged or simplified vertex never makes the mapping run backwards).</summary>
        private static double SpanS(Work w, int seg, double d)
        {
            double len = w.Len[seg];
            return len > 1e-9 ? (w.Vs[seg + 1] - w.Vs[seg]) * d / len : 0.0;
        }

        /// <summary>Straight run from the current point (excluded) to (bx, bz) (included), at most maxSeg per step.</summary>
        private static void Straight(RoadCentreline c, double ax, double az, double sa, double bx, double bz, double sb, double tx, double tz, double maxSeg)
        {
            double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6)
            {
                c.Add(bx, bz, sb, tx, tz, 0f);
                return;
            }
            int n = Math.Max(1, (int)Math.Ceiling(len / maxSeg));
            for (int k = 1; k <= n; k++)
            {
                double f = (double)k / n;
                c.Add(ax + dx * f, az + dz * f, sa + (sb - sa) * f, tx, tz, 0f);
            }
        }

        /// <summary>
        /// The fillet at vertex (vx, vz) between legs (ix, iz) (into the vertex) and (ox, oz) (out of it) with tangent
        /// length t, emitted for the arc fraction [f0, f1] (0 = incoming tangent point, 1 = outgoing) with raw along
        /// running linearly from vs + s0 to vs + s1. The first emitted point (f0) replaces the current point when they
        /// coincide.
        /// </summary>
        private static void Arc(Work w, RoadCentreline c, double vx, double vz, double vs, double ix, double iz, double ox, double oz, double t,
                                double step, double f0, double f1, double s0, double s1)
        {
            double cr = ix * oz - iz * ox, dot = ix * ox + iz * oz;
            double th = Math.Atan2(Math.Abs(cr), dot);
            double turn = cr >= 0 ? 1 : -1;
            double tanHalf = Math.Tan(0.5 * Math.Min(th, Math.PI - 1e-3));
            double rad = tanHalf > 1e-12 ? t / tanHalf : 1e12;
            // Tangent point on the incoming leg and the centre on the inside of the turn.
            double ax = vx - ix * t, az = vz - iz * t;
            double nx = -iz * turn, nz = ix * turn; // toward the centre
            double ox0 = ax + nx * rad, oz0 = az + nz * rad;
            double sweep = th * (f1 - f0);
            double arcLen = rad * sweep;
            // Angle step: chord error at most ArcToleranceM at the outer edge, between MinArcStepDeg and step.
            double ro = rad + w.EdgeM;
            double sag = ro > ArcToleranceM ? 2.0 * Math.Acos(1.0 - ArcToleranceM / ro) : step;
            double minStep = MinArcStepDeg * Math.PI / 180.0;
            if (sag < minStep) sag = minStep;
            if (sag > step) sag = step;
            int n = Math.Max(1, (int)Math.Ceiling(sweep / sag - 1e-9));
            int byChord = Math.Max(1, (int)Math.Floor(arcLen / MinArcChordM));
            if (n > byChord) n = byChord;
            float k = (float)(turn / rad);
            for (int q = 0; q <= n; q++)
            {
                double f = f0 + (f1 - f0) * q / n;
                double ang = th * f;
                // Rotate the radius vector (point - centre) by ang in the turn direction.
                double rx = ax - ox0, rz = az - oz0;
                double ca = Math.Cos(ang * turn), sa = Math.Sin(ang * turn);
                double px = ox0 + rx * ca - rz * sa, pz = oz0 + rx * sa + rz * ca;
                double tx = ix * ca - iz * sa, tz = ix * sa + iz * ca;
                double s = vs + s0 + (s1 - s0) * (f - f0) / (f1 - f0);
                c.Add(px, pz, s, tx, tz, k);
            }
        }
    }
}
