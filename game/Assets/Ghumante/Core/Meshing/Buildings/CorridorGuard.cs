using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>Small 2-D helpers shared by the building guards (tile-local or game metres alike).</summary>
    internal static class Plane2
    {
        /// <summary>Distance from (x, z) to segment a-b, with the closest point.</summary>
        public static double PointSeg(double x, double z, double ax, double az, double bx, double bz, out double ox, out double oz)
        {
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            double t = l2 > 0 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            ox = ax + dx * t;
            oz = az + dz * t;
            return Math.Sqrt((x - ox) * (x - ox) + (z - oz) * (z - oz));
        }

        public static double PointSeg(double x, double z, double ax, double az, double bx, double bz)
        {
            double ox, oz;
            return PointSeg(x, z, ax, az, bx, bz, out ox, out oz);
        }

        private static double Orient(double ax, double az, double bx, double bz, double cx, double cz)
        {
            return (bx - ax) * (cz - az) - (bz - az) * (cx - ax);
        }

        /// <summary>True when segments a-b and c-d cross properly (touching ends do not count).</summary>
        public static bool Cross(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            double d1 = Orient(cx, cz, dx, dz, ax, az), d2 = Orient(cx, cz, dx, dz, bx, bz);
            double d3 = Orient(ax, az, bx, bz, cx, cz), d4 = Orient(ax, az, bx, bz, dx, dz);
            return (d1 > 0 && d2 < 0 || d1 < 0 && d2 > 0) && (d3 > 0 && d4 < 0 || d3 < 0 && d4 > 0);
        }

        /// <summary>Distance between segments a-b and c-d (0 when they cross).</summary>
        public static double SegSeg(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            if (Cross(ax, az, bx, bz, cx, cz, dx, dz)) return 0;
            double d = PointSeg(ax, az, cx, cz, dx, dz);
            d = Math.Min(d, PointSeg(bx, bz, cx, cz, dx, dz));
            d = Math.Min(d, PointSeg(cx, cz, ax, az, bx, bz));
            return Math.Min(d, PointSeg(dx, dz, ax, az, bx, bz));
        }

        /// <summary>Even-odd point in polygon.</summary>
        public static bool Inside(double[] x, double[] z, int n, double px, double pz)
        {
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
                if ((z[i] > pz) != (z[j] > pz) && px < (x[j] - x[i]) * (pz - z[i]) / (z[j] - z[i]) + x[i]) inside = !inside;
            return inside;
        }

        /// <summary>True when no two non-adjacent edges of the ring cross or touch within 1 cm.</summary>
        public static bool IsSimple(double[] x, double[] z, int n)
        {
            for (int i = 0; i < n; i++)
            {
                int i1 = i + 1 == n ? 0 : i + 1;
                for (int j = i + 2; j < n; j++)
                {
                    int j1 = j + 1 == n ? 0 : j + 1;
                    if (j1 == i) continue;
                    if (SegSeg(x[i], z[i], x[i1], z[i1], x[j], z[j], x[j1], z[j1]) < 0.01) return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// The building package's stand-in for the road corridors (<see cref="IRoadCorridorQuery"/>, docs/W2_DETAIL_CONTRACT.md
    /// §1-3) until the roads package's <c>RoadCorridorIndex</c> is wired through <see cref="BuildingOptions.Corridors"/>:
    /// every road of the tile on the ground (no tunnels, covered passages or underground layers) is a stadium around its
    /// centreline as wide as the road mesher's widest game width of the piece (<see cref="RoadWidthModel.MaxGameWidthM"/>)
    /// and never narrower than <see cref="RoadClearance.MinCorridorM"/>. Game metres in. <see cref="SignedDistance"/> is
    /// exact out to <see cref="MaxSearchM"/> (a ring search over a 16 m grid, like the roads package's index) and reports
    /// <see cref="MaxSearchM"/> beyond it, so "a road within 4 m" means a real road (a clamp to a few metres made every
    /// wall a street wall). Built once per tile and immutable, so worker threads may share it.
    /// </summary>
    internal sealed class RoadCorridorStandIn : IRoadCorridorQuery
    {
        /// <summary>Farthest a <see cref="SignedDistance"/> query looks for a corridor; beyond it the answer is this
        /// bound (the same as the roads package's <c>RoadCorridorIndex.MaxSearchM</c>).</summary>
        public const double MaxSearchM = 64.0;

        private const double CellM = 16.0;
        private static readonly ConditionalWeakTable<TileData, RoadCorridorStandIn> Cache = new ConditionalWeakTable<TileData, RoadCorridorStandIn>();

        private readonly double _x0, _z0;
        private readonly int _n;
        private readonly int[] _start, _idx;
        private readonly double[] _ax, _az, _bx, _bz, _half;

        public static RoadCorridorStandIn For(TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return Cache.GetValue(t, k => new RoadCorridorStandIn(k));
        }

        /// <summary>Segments in tile-local metres with their half widths (tests build corridors by hand).</summary>
        public RoadCorridorStandIn(double tileX0, double tileZ0, double tileSize, double[] ax, double[] az, double[] bx, double[] bz, double[] half)
        {
            _x0 = tileX0;
            _z0 = tileZ0;
            _ax = ax;
            _az = az;
            _bx = bx;
            _bz = bz;
            _half = half;
            _n = Math.Max(1, (int)Math.Ceiling(tileSize / CellM));
            Index(ax.Length, out _start, out _idx);
        }

        private RoadCorridorStandIn(TileData t)
        {
            _x0 = t.Tile.X0;
            _z0 = t.Tile.Z0;
            _n = Math.Max(1, (int)Math.Ceiling(t.Tile.Size / CellM));
            int segs = 0;
            for (int i = 0; i < t.Roads.Count; i++)
                if (Corridor(t.Roads[i])) segs += Math.Max(0, t.Roads[i].PointCount - 1);
            _ax = new double[segs];
            _az = new double[segs];
            _bx = new double[segs];
            _bz = new double[segs];
            _half = new double[segs];
            int s = 0;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!Corridor(r)) continue;
                double half = 0.5 * Math.Max(RoadClearance.MinCorridorM, RoadWidthModel.MaxGameWidthM(r, t.RoadAttrOf(i)));
                int[] p = r.Points;
                for (int k = 0; k + 1 < p.Length / 2; k++)
                {
                    _ax[s] = p[2 * k] / 100.0;
                    _az[s] = p[2 * k + 1] / 100.0;
                    _bx[s] = p[2 * k + 2] / 100.0;
                    _bz[s] = p[2 * k + 3] / 100.0;
                    _half[s] = half;
                    s++;
                }
            }
            Index(s, out _start, out _idx);
        }

        /// <summary>Roads that claim a corridor on the ground: not tunnels or covered passages, not under ground.</summary>
        private static bool Corridor(RoadRecord r)
        {
            return (r.Flags & RoadFlags.Tunnel) == 0 && r.Layer >= 0 && r.PointCount >= 2 && r.RoadClass != RoadClass.Unknown;
        }

        /// <summary>CSR grid: each segment is listed in every cell its corridor stadium's bounding box touches.</summary>
        private void Index(int segs, out int[] start, out int[] idx)
        {
            var counts = new int[_n * _n + 1];
            for (int k = 0; k < segs; k++)
            {
                int i0, i1, j0, j1;
                Range(k, out i0, out i1, out j0, out j1);
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    counts[j * _n + i + 1]++;
            }
            for (int c = 0; c < _n * _n; c++) counts[c + 1] += counts[c];
            start = counts;
            idx = new int[counts[_n * _n]];
            var fill = new int[_n * _n];
            for (int k = 0; k < segs; k++)
            {
                int i0, i1, j0, j1;
                Range(k, out i0, out i1, out j0, out j1);
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int c = j * _n + i;
                    idx[start[c] + fill[c]++] = k;
                }
            }
        }

        private void Range(int k, out int i0, out int i1, out int j0, out int j1)
        {
            double pad = _half[k];
            i0 = Clamp((int)Math.Floor((Math.Min(_ax[k], _bx[k]) - pad) / CellM));
            i1 = Clamp((int)Math.Floor((Math.Max(_ax[k], _bx[k]) + pad) / CellM));
            j0 = Clamp((int)Math.Floor((Math.Min(_az[k], _bz[k]) - pad) / CellM));
            j1 = Clamp((int)Math.Floor((Math.Max(_az[k], _bz[k]) + pad) / CellM));
        }

        private int Clamp(int v)
        {
            return v < 0 ? 0 : v >= _n ? _n - 1 : v;
        }

        /// <summary>Signed distance (m) from game point (x, z) to the nearest corridor edge, negative inside; exact up to
        /// <see cref="MaxSearchM"/>, which is returned when nothing is nearer.</summary>
        public double SignedDistance(double x, double z)
        {
            if (_idx.Length == 0) return MaxSearchM;
            double lx = x - _x0, lz = z - _z0;
            int ci = (int)Math.Floor(lx / CellM), cj = (int)Math.Floor(lz / CellM);
            double best = double.PositiveInfinity;
            int rings = (int)Math.Ceiling(MaxSearchM / CellM) + 1;
            for (int r = 0; r <= rings; r++)
            {
                for (int j = cj - r; j <= cj + r; j++)
                {
                    if (j < 0 || j >= _n) continue;
                    bool edgeRow = j == cj - r || j == cj + r;
                    for (int i = ci - r; i <= ci + r; i += edgeRow ? 1 : Math.Max(1, 2 * r))
                    {
                        if (i < 0 || i >= _n) continue;
                        int c = j * _n + i;
                        for (int k = _start[c]; k < _start[c + 1]; k++)
                        {
                            int e = _idx[k];
                            double d = Plane2.PointSeg(lx, lz, _ax[e], _az[e], _bx[e], _bz[e]) - _half[e];
                            if (d < best) best = d;
                        }
                    }
                }
                // Every cell of the next ring lies at least r cells from the point (which is in its own cell, or outside
                // the grid, where the clamped rings only reach further), and each corridor sits in the cells its stadium
                // touches: nothing unvisited can be nearer.
                if (best <= r * CellM) break;
            }
            return Math.Min(best, MaxSearchM);
        }

        /// <summary>True when the polygon (game metres) comes within a corridor; depth = the deepest intrusion.</summary>
        public bool Overlaps(double[] x, double[] z, int n, out double depthM)
        {
            depthM = 0;
            if (n < 3) return false;
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                minX = Math.Min(minX, x[i] - _x0);
                maxX = Math.Max(maxX, x[i] - _x0);
                minZ = Math.Min(minZ, z[i] - _z0);
                maxZ = Math.Max(maxZ, z[i] - _z0);
            }
            int i0 = Clamp((int)Math.Floor(minX / CellM)), i1 = Clamp((int)Math.Floor(maxX / CellM));
            int j0 = Clamp((int)Math.Floor(minZ / CellM)), j1 = Clamp((int)Math.Floor(maxZ / CellM));
            bool hit = false;
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                int c = j * _n + i;
                for (int k = _start[c]; k < _start[c + 1]; k++)
                {
                    int e = _idx[k];
                    double ax = _ax[e] + _x0, az = _az[e] + _z0, bx = _bx[e] + _x0, bz = _bz[e] + _z0;
                    double d = double.MaxValue;
                    if (Plane2.Inside(x, z, n, ax, az) || Plane2.Inside(x, z, n, bx, bz)) d = 0;
                    else
                        for (int q = 0; q < n && d > 0; q++)
                        {
                            int r = q + 1 == n ? 0 : q + 1;
                            d = Math.Min(d, Plane2.SegSeg(x[q], z[q], x[r], z[r], ax, az, bx, bz));
                        }
                    double depth = _half[e] - d;
                    if (depth > 0)
                    {
                        hit = true;
                        if (depth > depthM) depthM = depth;
                    }
                }
            }
            return hit;
        }
    }

    /// <summary>
    /// The runtime footprint guard (decision 1 of docs/W2_DETAIL_CONTRACT.md): the pipeline trims footprints back to
    /// the road corridors so every consumer agrees; packs built before that, and any footprint that still intrudes,
    /// are trimmed here. While a ring intrudes deeper than <see cref="ToleranceM"/>, it is cut by the half-plane along
    /// the corridor edge at its deepest point (<see cref="MarginM"/> outside it; where a road runs right through a
    /// building the larger side is kept), up to <see cref="MaxCuts"/> times; half-plane cuts never make a ring
    /// self-intersect. The result is simplified and validated (at least <see cref="MinKeepShare"/> of the area left, no
    /// intrusion left). A ring that fails is <b>dropped</b>: a house never stands in a road. The front and corner edges are
    /// re-found on the trimmed ring (the edge along the corridor is the new front). Built once per tile and
    /// corridor source, immutable afterwards (worker threads may share it). Deterministic.
    /// </summary>
    public sealed class BuildingFootprints
    {
        /// <summary>Intrusions up to this depth are left alone (rounding); the walls' few centimetres of relief end
        /// within the trim's own <see cref="MarginM"/>.</summary>
        public const double ToleranceM = 0.02;

        /// <summary>A trimmed edge ends this far outside the corridor.</summary>
        public const double MarginM = 0.03;

        /// <summary>Densify step of a ring before the push.</summary>
        public const double StepM = 0.75;

        /// <summary>Smallest share of the original area a trimmed ring keeps; less means the house stands in the road.</summary>
        public const double MinKeepShare = 0.3;

        /// <summary>Guards kept per tile, one per corridor source (B0 cells and the far bands may use different
        /// sources); the least recently used goes first.</summary>
        public const int SourcesPerTile = 4;

        private sealed class Holder
        {
            public readonly BuildingFootprints[] Values = new BuildingFootprints[SourcesPerTile];
            public readonly long[] Used = new long[SourcesPerTile];
            public long Clock;
        }

        private static readonly ConditionalWeakTable<TileData, Holder> Cache = new ConditionalWeakTable<TileData, Holder>();

        private readonly IRoadCorridorQuery _q;
        private readonly TileData _tile;
        private readonly BuildingRecord[] _records;
        private readonly short[] _front, _second;
        private readonly byte[] _state;
        private readonly byte[] _why;
        private BuildingBands.FootprintIndex _neighbours;

        private const byte Kept = 0, TrimmedState = 1, DroppedState = 2;

        /// <summary>Guards built so far (all tiles, all sources): tests check that alternating sources do not rebuild.</summary>
        internal static long BuildCount;

        /// <summary>The guard of a tile for a corridor source (null: footprints as they are). Cached per tile and source
        /// (up to <see cref="SourcesPerTile"/> sources per tile), so callers that alternate sources never rebuild.</summary>
        public static BuildingFootprints For(TileData t, IRoadCorridorQuery q)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            Holder h = Cache.GetValue(t, k => new Holder());
            lock (h)
            {
                h.Clock++;
                int free = -1, oldest = 0;
                for (int k = 0; k < SourcesPerTile; k++)
                {
                    BuildingFootprints v = h.Values[k];
                    if (v == null)
                    {
                        if (free < 0) free = k;
                        continue;
                    }
                    if (ReferenceEquals(v._q, q))
                    {
                        h.Used[k] = h.Clock;
                        return v;
                    }
                    if (h.Used[k] < h.Used[oldest]) oldest = k;
                }
                int slot = free >= 0 ? free : oldest;
                var built = new BuildingFootprints(t, q);
                System.Threading.Interlocked.Increment(ref BuildCount);
                h.Values[slot] = built;
                h.Used[slot] = h.Clock;
                return built;
            }
        }

        /// <summary>The footprints this guard draws as a point-in-footprint index (which walls and corners abut a
        /// neighbour): trimmed rings, houses in the road left out. Built on first use; thread-safe.</summary>
        internal BuildingBands.FootprintIndex Neighbours
        {
            get
            {
                BuildingBands.FootprintIndex n = System.Threading.Volatile.Read(ref _neighbours);
                if (n != null) return n;
                lock (_records)
                {
                    if (_neighbours == null) System.Threading.Volatile.Write(ref _neighbours, new BuildingBands.FootprintIndex(_tile, this));
                    return _neighbours;
                }
            }
        }

        /// <summary>Buildings trimmed back to a corridor.</summary>
        public int TrimmedCount { get; private set; }

        /// <summary>Buildings dropped because they stand in a road.</summary>
        public int DroppedCount { get; private set; }

        /// <summary>The record to draw: the original, or a copy with the trimmed outer ring.</summary>
        public BuildingRecord Record(int index)
        {
            return _records[index];
        }

        public bool Trimmed(int index)
        {
            return _state[index] == TrimmedState;
        }

        /// <summary>True when the building stands in a road and is not drawn.</summary>
        public bool Dropped(int index)
        {
            return _state[index] == DroppedState;
        }

        /// <summary>The plan with the front and corner edges re-found on a trimmed ring (unchanged otherwise).</summary>
        public HousePlan Adjust(int index, HousePlan plan)
        {
            if (_state[index] != TrimmedState) return plan;
            plan.FrontEdge = _front[index];
            plan.SecondEdge = _second[index];
            return plan;
        }

        private BuildingFootprints(TileData t, IRoadCorridorQuery q)
        {
            _q = q;
            _tile = t;
            int n = t.Buildings.Count;
            _records = new BuildingRecord[n];
            _front = new short[n];
            _second = new short[n];
            _state = new byte[n];
            _why = new byte[n];
            var w = new Work();
            for (int i = 0; i < n; i++)
            {
                BuildingRecord b = t.Buildings[i];
                _records[i] = b;
                if (q == null || b.Rings == null || b.Rings.Length == 0) continue;
                // Temples, stupas and shrines keep their footprints (their generators and heroes own them).
                if (BuildingGrammar.IsSacred(b.Archetype)) continue;
                byte s = Guard(t, b, q, w, out BuildingRecord trimmed, out int front, out int second, out byte why);
                _state[i] = s;
                _why[i] = why;
                if (s == TrimmedState)
                {
                    _records[i] = trimmed;
                    _front[i] = (short)front;
                    _second[i] = (short)second;
                    TrimmedCount++;
                }
                else if (s == DroppedState) DroppedCount++;
            }
        }

        private sealed class Work
        {
            public double[] X = new double[64], Z = new double[64], X2 = new double[64], Z2 = new double[64], XB = new double[64], ZB = new double[64];
            public bool[] Keep = new bool[64];

            public void Ensure(int n)
            {
                // Each cut adds at most one point net; keep room for every cut.
                n = 2 * n + 2 * MaxCuts + 4;
                if (X.Length >= n) return;
                int cap = Math.Max(n, X.Length * 2);
                X = new double[cap];
                Z = new double[cap];
                X2 = new double[cap];
                Z2 = new double[cap];
                XB = new double[cap];
                ZB = new double[cap];
                Keep = new bool[cap];
            }
        }

        /// <summary>Why a building was dropped: 1 too little left, 2 the trimmed ring self-intersects, 3 still in a
        /// road, 4 degenerate (0 = kept or trimmed).</summary>
        internal byte DropReason(int index)
        {
            return _why[index];
        }

        private static byte Guard(TileData t, BuildingRecord b, IRoadCorridorQuery q, Work w, out BuildingRecord trimmed, out int front, out int second, out byte why)
        {
            trimmed = null;
            front = second = -1;
            why = 0;
            int[] ring = b.Rings[0];
            int n = ring.Length / 2;
            if (n < 3) return Kept;
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            w.Ensure(n + 1);
            for (int i = 0; i < n; i++)
            {
                w.X[i] = x0 + ring[2 * i] / 100.0;
                w.Z[i] = z0 + ring[2 * i + 1] / 100.0;
            }
            double depth;
            if (Clear(w, n, q)) return Kept;
            for (int k = 0; k < n; k++)
            {
                w.X2[k] = w.X[k];
                w.Z2[k] = w.Z[k];
            }
            double dpx, dpz, dsd;
            bool sampled = Deepest(w, n, q, out dpx, out dpz, out dsd);
            if (sampled && dsd >= -ToleranceM && (!q.Overlaps(w.X, w.Z, n, out depth) || depth <= ToleranceM)) return Kept;
            double signed0 = Polygon.SignedArea(w.X, w.Z, n), area0 = Math.Abs(signed0);
            if (signed0 < 0)
            {
                Array.Reverse(w.X, 0, n);
                Array.Reverse(w.Z, 0, n);
            }
            // Cut the ring with half-planes along the local corridor edge at its deepest intrusion, until nothing
            // intrudes (a cut cannot make a ring self-intersect, unlike pushing points out).
            int m = n;
            for (int k = 0; k < m; k++)
            {
                w.X2[k] = w.X[k];
                w.Z2[k] = w.Z[k];
            }
            bool cut = false;
            for (int iter = 0; iter < MaxCuts; iter++)
            {
                double px, pz, sd;
                if (!Deepest(w, m, q, out px, out pz, out sd) || sd >= -ToleranceM) break;
                double gx, gz;
                if (!Gradient(q, px, pz, out gx, out gz))
                {
                    // On a corridor's axis the distance has no gradient: step off it.
                    bool found = false;
                    for (int k = 0; k < 8 && !found; k++)
                    {
                        double a = k * Math.PI / 4, ox = px + 0.15 * Math.Cos(a), oz = pz + 0.15 * Math.Sin(a);
                        if (Gradient(q, ox, oz, out gx, out gz))
                        {
                            sd = q.SignedDistance(ox, oz);
                            px = ox;
                            pz = oz;
                            found = true;
                        }
                    }
                    if (!found)
                    {
                        why = 3;
                        return DroppedState;
                    }
                }
                // Option A: keep the side away from the corridor beyond its near edge.
                double ax = px + gx * (MarginM - sd), az = pz + gz * (MarginM - sd);
                int ma = ClipHalf(w.X2, w.Z2, m, w.X, w.Z, ax, az, gx, gz);
                double areaA = ma >= 3 ? Polygon.SignedArea(w.X, w.Z, ma) : 0;
                // Option B: the road crosses the building: keep the part beyond the far edge instead, if larger.
                double bxp = px, bzp = pz, areaB = 0;
                int mb = 0;
                for (double tt = 0.25; tt < 40;)
                {
                    double qx = px - gx * tt, qz = pz - gz * tt, qs = q.SignedDistance(qx, qz);
                    if (qs <= 0)
                    {
                        // Inside: |distance| to the nearest edge is a safe step in any direction.
                        tt += Math.Max(0.2, -qs);
                        continue;
                    }
                    {
                        bxp = qx - gx * MarginM;
                        bzp = qz - gz * MarginM;
                        mb = ClipHalf(w.X2, w.Z2, m, w.XB, w.ZB, bxp, bzp, -gx, -gz);
                        areaB = mb >= 3 ? Polygon.SignedArea(w.XB, w.ZB, mb) : 0;
                        break;
                    }
                }
                if (areaB > areaA)
                {
                    Array.Copy(w.XB, w.X2, mb);
                    Array.Copy(w.ZB, w.Z2, mb);
                    m = mb;
                }
                else
                {
                    Array.Copy(w.X, w.X2, ma);
                    Array.Copy(w.Z, w.Z2, ma);
                    m = ma;
                }
                cut = true;
                if (m < 3) break;
            }
            if (!cut) return Kept;
            m = Simplify(w, m);
            why = 4;
            if (m < 3) return DroppedState;
            double area = Polygon.SignedArea(w.X2, w.Z2, m);
            why = 1;
            if (area <= 0 || area < MinKeepShare * area0 || area < 1.0) return DroppedState;
            why = 3;
            double lx, lz, lsd;
            if (Deepest(w, m, q, out lx, out lz, out lsd) && lsd < -3 * ToleranceM) return DroppedState;
            why = 0;
            if (signed0 < 0)
            {
                // Back to the record's winding.
                Array.Reverse(w.X2, 0, m);
                Array.Reverse(w.Z2, 0, m);
            }

            var r = new int[2 * m];
            for (int k = 0; k < m; k++)
            {
                r[2 * k] = (int)Math.Round((w.X2[k] - x0) * 100.0);
                r[2 * k + 1] = (int)Math.Round((w.Z2[k] - z0) * 100.0);
            }
            var rings = new int[b.Rings.Length][];
            rings[0] = r;
            for (int k = 1; k < rings.Length; k++) rings[k] = b.Rings[k];
            trimmed = new BuildingRecord
            {
                OsmRef = b.OsmRef, Archetype = b.Archetype, Use = b.Use, Levels = b.Levels, Flags = b.Flags, HeightCm = b.HeightCm,
                MinHeightCm = b.MinHeightCm, RoofShape = b.RoofShape, RoofMaterial = b.RoofMaterial, WallMaterial = b.WallMaterial,
                Seed = b.Seed, NameRef = b.NameRef, Rings = rings,
            };
            Fronts(w, m, q, out front, out second);
            return TrimmedState;
        }

        /// <summary>
        /// A cheap proof that a ring is clear of every corridor: the corridor distance is 1-Lipschitz (and the
        /// stand-in's clamp only lowers it), so an edge whose end distances sum to at least its length cannot reach a
        /// corridor. False means "maybe": the exact <see cref="IRoadCorridorQuery.Overlaps"/> decides.
        /// </summary>
        private static bool Clear(Work w, int n, IRoadCorridorQuery q)
        {
            double cx = 0, cz = 0;
            for (int i = 0; i < n; i++)
            {
                w.X2[i] = q.SignedDistance(w.X[i], w.Z[i]);
                if (w.X2[i] < 0) return false;
                cx += w.X[i];
                cz += w.Z[i];
            }
            if (q.SignedDistance(cx / n, cz / n) < 0) return false;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                double dx = w.X[j] - w.X[i], dz = w.Z[j] - w.Z[i];
                if (w.X2[i] + w.X2[j] < Math.Sqrt(dx * dx + dz * dz)) return false;
            }
            return true;
        }

        /// <summary>Cuts per footprint before giving up (a curving corridor takes several).</summary>
        public const int MaxCuts = 8;

        /// <summary>The deepest point of the ring inside a corridor: its vertices and points every
        /// <see cref="StepM"/> along its edges (in X2/Z2).</summary>
        private static bool Deepest(Work w, int m, IRoadCorridorQuery q, out double px, out double pz, out double sd)
        {
            px = pz = 0;
            sd = double.MaxValue;
            for (int i = 0; i < m; i++)
            {
                int j = i + 1 == m ? 0 : i + 1;
                double dx = w.X2[j] - w.X2[i], dz = w.Z2[j] - w.Z2[i];
                int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / (0.67 * StepM)));
                for (int k = 0; k < steps; k++)
                {
                    double x = w.X2[i] + dx * k / steps, z = w.Z2[i] + dz * k / steps;
                    double d = q.SignedDistance(x, z);
                    if (d < sd)
                    {
                        sd = d;
                        px = x;
                        pz = z;
                    }
                }
            }
            return sd < double.MaxValue;
        }

        /// <summary>Unit gradient of the corridor distance at (x, z) (pointing out of the corridor).</summary>
        private static bool Gradient(IRoadCorridorQuery q, double x, double z, out double gx, out double gz)
        {
            const double H = 0.05;
            gx = q.SignedDistance(x + H, z) - q.SignedDistance(x - H, z);
            gz = q.SignedDistance(x, z + H) - q.SignedDistance(x, z - H);
            double gl = Math.Sqrt(gx * gx + gz * gz);
            if (gl < 1e-9) return false;
            gx /= gl;
            gz /= gl;
            return true;
        }

        /// <summary>Sutherland-Hodgman: the part of ring (sx, sz) with (p − o)·n ≥ 0, into (dx, dz). Returns the
        /// point count.</summary>
        private static int ClipHalf(double[] sx, double[] sz, int m, double[] dx, double[] dz, double ox, double oz, double nx, double nz)
        {
            int k = 0;
            for (int i = 0; i < m; i++)
            {
                int j = i + 1 == m ? 0 : i + 1;
                double si = (sx[i] - ox) * nx + (sz[i] - oz) * nz, sj = (sx[j] - ox) * nx + (sz[j] - oz) * nz;
                if (si >= 0)
                {
                    dx[k] = sx[i];
                    dz[k] = sz[i];
                    k++;
                }
                if ((si >= 0) != (sj >= 0))
                {
                    double t = si / (si - sj);
                    dx[k] = sx[i] + (sx[j] - sx[i]) * t;
                    dz[k] = sz[i] + (sz[j] - sz[i]) * t;
                    k++;
                }
            }
            return k;
        }

        /// <summary>Drop points closer than 2 cm to the previous one and points within 4 cm of the line through their
        /// neighbours (repeated until stable), in place on X2/Z2.</summary>
        private static int Simplify(Work w, int m)
        {
            bool changed = true;
            while (changed && m >= 3)
            {
                changed = false;
                for (int k = 0; k < m; k++) w.Keep[k] = true;
                int kept = m;
                for (int k = 0; k < m && kept > 3; k++)
                {
                    int p = k - 1;
                    while (p >= 0 && !w.Keep[p]) p--;
                    if (p < 0) p = m - 1;
                    int nx = k + 1 == m ? 0 : k + 1;
                    double d = Plane2.PointSeg(w.X2[k], w.Z2[k], w.X2[p], w.Z2[p], w.X2[nx], w.Z2[nx]);
                    double dp = Math.Abs(w.X2[k] - w.X2[p]) + Math.Abs(w.Z2[k] - w.Z2[p]);
                    if (d < 0.04 || dp < 0.02)
                    {
                        w.Keep[k] = false;
                        kept--;
                        changed = true;
                        k++; // keep the next point this pass, so a run is thinned evenly
                    }
                }
                int o = 0;
                for (int k = 0; k < m; k++)
                {
                    if (!w.Keep[k]) continue;
                    w.X2[o] = w.X2[k];
                    w.Z2[o] = w.Z2[k];
                    o++;
                }
                m = o;
            }
            return m;
        }

        /// <summary>The front of a trimmed ring: the longest edge lying along a corridor (its middle within 0.4 m of
        /// the edge, the corridor in front of it); the corner edge: the longest other such edge turned 45° or more.</summary>
        private static void Fronts(Work w, int m, IRoadCorridorQuery q, out int front, out int second)
        {
            front = second = -1;
            double best = 0, bestSecond = 0, fnx = 0, fnz = 0, longest = 0;
            int longestEdge = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < m; i++)
                {
                    int j = i + 1 == m ? 0 : i + 1;
                    double dx = w.X2[j] - w.X2[i], dz = w.Z2[j] - w.Z2[i], len = Math.Sqrt(dx * dx + dz * dz);
                    if (len < 0.5) continue;
                    if (pass == 0 && len > longest)
                    {
                        longest = len;
                        longestEdge = i;
                    }
                    double nx = dz / len, nz = -dx / len, mx = 0.5 * (w.X2[i] + w.X2[j]), mz = 0.5 * (w.Z2[i] + w.Z2[j]);
                    double sd = q.SignedDistance(mx, mz);
                    if (sd > 0.4) continue;
                    if (q.SignedDistance(mx + 0.3 * nx, mz + 0.3 * nz) >= sd) continue; // the corridor is behind
                    if (pass == 0)
                    {
                        if (len > best)
                        {
                            best = len;
                            front = i;
                            fnx = nx;
                            fnz = nz;
                        }
                    }
                    else if (i != front && nx * fnx + nz * fnz < 0.707 && len > bestSecond && len >= 2.0)
                    {
                        bestSecond = len;
                        second = i;
                    }
                }
                if (front < 0) break;
            }
            if (front < 0) front = longestEdge;
        }
    }
}
