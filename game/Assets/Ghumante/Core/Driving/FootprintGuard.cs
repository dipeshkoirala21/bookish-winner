using System;
using System.Collections.Generic;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// The runtime guard of decision 1 (docs/W2_DETAIL_CONTRACT.md §1) for footprint solids: a building outline that
    /// reaches into a drawn carriageway (or the road corridor, when the roads package's <see cref="IRoadCorridorQuery"/>
    /// is given) is trimmed back to that road's edge on the building's side, so walls stand beside the roads, never
    /// across them. Built once per tile on the build worker; one instance per tile build (not thread safe).
    /// <list type="bullet">
    /// <item>Only the roads whose segments come near the outline are looked at, and only edges that come within a
    /// road's widest half width of its centreline; those are halved until each piece that still may reach the road is
    /// at most <see cref="StepM"/> long. An outline away from every road costs one grid lookup.</item>
    /// <item>Each run of consecutive points inside a road is moved to the edge of the road segment each point lies in,
    /// on the side where the outline's points just before and after the run lie; points outside every road never move
    /// (except a short gap of them between two runs in the wedge the per-segment strips leave on the outer side of a
    /// bend, which goes round the bend with the run). Where the run's ends disagree on the side (a dead end poking into
    /// the footprint) every point keeps its own side.</item>
    /// <item>A road that runs right through the footprint (two runs crossing it) splits the outline into one per
    /// side, each trimmed again for the other roads.</item>
    /// <item>The result is checked: no wall may lie more than <see cref="MaxInsideM"/> inside any carriageway. When one
    /// does (a dead end inside the footprint, a tangle of roads), the original ring minus the offending road segment's
    /// strip is used instead, repeated up to <see cref="MaxClips"/> strips: exact, as several outlines.</item>
    /// </list>
    /// Deterministic: the same ring, roads and corridors give the same outlines.
    /// </summary>
    internal sealed class FootprintGuard
    {
        /// <summary>Longest piece of an intruding edge (finer pieces follow a curved road edge).</summary>
        public const double StepM = 1.0;

        /// <summary>A trimmed outline lands this far beyond the road edge.</summary>
        public const double ClearM = 0.05;

        /// <summary>Deepest a trimmed wall may still lie inside a carriageway (checked every half metre).</summary>
        public const double MaxInsideM = 0.3;

        /// <summary>Most road strips the fallback subtracts from one outline.</summary>
        public const int MaxClips = 12;

        private const double CheckStepM = 0.5;
        private const double CorridorStepM = 0.25;
        private const int MaxCorridorSteps = 48;
        private const int MaxDepth = 2;
        private const int MaxFixes = 3;
        private const int MaxGapPoints = 3;
        private const double GapNearM = 1.0;

        private readonly RoadSpatialIndex _roads;
        private readonly IRoadCorridorQuery _corridors;
        private readonly double _extra;
        private readonly int[] _stamp;
        private int _stampValue;
        private readonly List<int> _gathered = new List<int>();
        private readonly List<int> _cand = new List<int>();
        private double[] _candReach = new double[16]; // widest half width (with dual shift) of each candidate's road
        private double[] _candEnds = new double[64]; // centreline ends (ax, az, bx, bz) of each candidate
        private double _minX, _minZ, _maxX, _maxZ;

        // Per recursion depth: the densified ring and the split side being built.
        private sealed class Buf
        {
            public double[] X = new double[64], Z = new double[64];
            public bool[] In = new bool[64];
            public int[] Seg = new int[64];
            public double[] SX = new double[64], SZ = new double[64];

            public void Ensure(int n)
            {
                if (X.Length >= n) return;
                int m = Math.Max(n, 2 * X.Length);
                Array.Resize(ref X, m);
                Array.Resize(ref Z, m);
                Array.Resize(ref In, m);
                Array.Resize(ref Seg, m);
                SX = new double[m];
                SZ = new double[m];
            }
        }

        private readonly Buf[] _bufs = { new Buf(), new Buf(), new Buf() };
        private double[] _ox = new double[64], _oz = new double[64], _clipX = new double[64], _clipZ = new double[64];
        private List<double[]> _fbX = new List<double[]>(), _fbZ = new List<double[]>(), _nextX = new List<double[]>(), _nextZ = new List<double[]>();
        private readonly double[] _hpX = new double[4], _hpZ = new double[4], _hpC = new double[4];
        private bool[] _hot = new bool[64];
        private readonly List<int> _runStart = new List<int>(), _runEnd = new List<int>();
        private readonly List<int> _runRoads = new List<int>();

        // The trimmed outlines of the last Trim: piece p is points [_pieceStart[p], _pieceStart[p + 1]).
        private readonly List<double> _outX = new List<double>(), _outZ = new List<double>();
        private readonly List<int> _pieceStart = new List<int>();

        /// <summary>A guard for the footprints of one tile: <paramref name="roads"/> is its road index (the drawn
        /// carriageways), <paramref name="corridors"/> the roads package's corridors (may be null).</summary>
        public FootprintGuard(RoadSpatialIndex roads, IRoadCorridorQuery corridors)
        {
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _corridors = corridors;
            _extra = corridors != null ? RoadSpatialIndex.MaxFootpathM + 0.5 * RoadClearance.MinCorridorM : 0.0;
            _stamp = new int[Math.Max(1, roads.SegmentCount)];
        }

        /// <summary>Number of outlines the last <see cref="Trim"/> produced (0: the footprint lay inside the road).</summary>
        public int PieceCount
        {
            get { return Math.Max(0, _pieceStart.Count - 1); }
        }

        /// <summary>Copies outline <paramref name="piece"/> of the last <see cref="Trim"/> into (x, z), growing them as
        /// needed; returns its point count.</summary>
        public int CopyPiece(int piece, ref double[] x, ref double[] z)
        {
            int a = _pieceStart[piece], n = _pieceStart[piece + 1] - a;
            if (x.Length < n) x = new double[Math.Max(n, 2 * x.Length)];
            if (z.Length < x.Length) z = new double[x.Length];
            for (int k = 0; k < n; k++)
            {
                x[k] = _outX[a + k];
                z[k] = _outZ[a + k];
            }
            return n;
        }

        /// <summary>
        /// Trims the closed ring (x, z), <paramref name="n"/> points in game metres. False: nothing reaches a road and the
        /// ring stands as it is. True: the trimmed outlines are in <see cref="PieceCount"/> / <see cref="CopyPiece"/>.
        /// </summary>
        public bool Trim(double[] x, double[] z, int n)
        {
            _outX.Clear();
            _outZ.Clear();
            _pieceStart.Clear();
            _pieceStart.Add(0);
            if (n < 3 || !Prepare(x, z, n) || !HotEdges(x, z, n)) return false;
            TrimRing(x, z, n, 0, true);
            return true;
        }

        /// <summary>Marks the edges that may reach a road in <see cref="_hot"/>; false when none does.</summary>
        private bool HotEdges(double[] x, double[] z, int n)
        {
            if (_hot.Length < n) _hot = new bool[Math.Max(n, 2 * _hot.Length)];
            bool any = false;
            for (int k = 0; k < n; k++)
            {
                int j = k + 1 == n ? 0 : k + 1;
                _hot[k] = MayIntrude(x[k], z[k], x[j], z[j]);
                any |= _hot[k];
            }
            return any;
        }

        /// <summary>True when some wall of the closed ring (x, z) lies in a road (sampled every half metre along the
        /// edges that can reach one). A courtyard that opens onto a road is filled rather than left as walls in it.</summary>
        public bool Intrudes(double[] x, double[] z, int n)
        {
            if (n < 3 || !Prepare(x, z, n)) return false;
            for (int k = 0; k < n; k++)
            {
                int j = k + 1 == n ? 0 : k + 1;
                if (!MayIntrude(x[k], z[k], x[j], z[j])) continue;
                double dx = x[j] - x[k], dz = z[j] - z[k];
                int pieces = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / CheckStepM));
                for (int q = 0; q <= pieces; q++)
                {
                    int seg;
                    if (Inside(x[k] + dx * q / pieces, z[k] + dz * q / pieces, out seg)) return true;
                }
            }
            return false;
        }

        /// <summary>True when (x, z) lies inside a carriageway (or corridor) near the ring last given to
        /// <see cref="Trim"/> (canopy posts are kept out of roads with it).</summary>
        public bool InRoad(double x, double z)
        {
            if (_cand.Count == 0) return false;
            int seg;
            if (Carriageway(x, z, out seg) < 0) return true;
            return _corridors != null && _corridors.SignedDistance(x, z) < 0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Candidates and distances

        /// <summary>Gathers the ground road segments that can reach the ring's box; false when there are none.</summary>
        private bool Prepare(double[] x, double[] z, int n)
        {
            _cand.Clear();
            _gathered.Clear();
            _minX = _minZ = double.PositiveInfinity;
            _maxX = _maxZ = double.NegativeInfinity;
            for (int k = 0; k < n; k++)
            {
                _minX = Math.Min(_minX, x[k]);
                _maxX = Math.Max(_maxX, x[k]);
                _minZ = Math.Min(_minZ, z[k]);
                _maxZ = Math.Max(_maxZ, z[k]);
            }
            if (++_stampValue == int.MaxValue)
            {
                Array.Clear(_stamp, 0, _stamp.Length);
                _stampValue = 1;
            }
            _roads.GatherSegments(_minX, _minZ, _maxX, _maxZ, _extra, RoadLayer.Ground, _stamp, _stampValue, _gathered);
            for (int g = 0; g < _gathered.Count; g++)
            {
                int i = _gathered[g];
                double reach = _roads.HalfWidthM(_roads.SegmentRoad(i)) + _extra + 0.1;
                double ax, az, bx, bz;
                _roads.SegmentBounds(i, out ax, out az, out bx, out bz);
                if (bx + reach < _minX || ax - reach > _maxX || bz + reach < _minZ || az - reach > _maxZ) continue;
                // The centreline must come within reach of the ring's box (its centre, plus half its diagonal).
                double cx = 0.5 * (_minX + _maxX), cz = 0.5 * (_minZ + _maxZ);
                double diag = 0.5 * Math.Sqrt((_maxX - _minX) * (_maxX - _minX) + (_maxZ - _minZ) * (_maxZ - _minZ));
                if (_roads.SegmentDistance(i, cx, cz) > diag + reach) continue;
                _cand.Add(i);
            }
            // Ascending segment order: ties between candidates always resolve the same way.
            _cand.Sort();
            if (_candReach.Length < _cand.Count)
            {
                _candReach = new double[Math.Max(_cand.Count, 2 * _candReach.Length)];
                _candEnds = new double[4 * _candReach.Length];
            }
            for (int c = 0; c < _cand.Count; c++)
            {
                _candReach[c] = _roads.HalfWidthM(_roads.SegmentRoad(_cand[c]));
                _roads.SegmentEnds(_cand[c], out _candEnds[4 * c], out _candEnds[4 * c + 1], out _candEnds[4 * c + 2], out _candEnds[4 * c + 3]);
            }
            return _cand.Count > 0;
        }

        /// <summary>Signed distance to the nearest candidate carriageway (negative inside) and that segment; candidates
        /// whose centreline lies farther than the best answer plus their widest half width are skipped.</summary>
        private double Carriageway(double x, double z, out int seg)
        {
            double best = double.PositiveInfinity;
            seg = -1;
            for (int c = 0; c < _cand.Count; c++)
            {
                int i = _cand[c];
                if (_roads.SegmentDistance(i, x, z) - _candReach[c] >= best) continue;
                double lat, t, fx, fz, cd;
                float half, shift;
                double e = _roads.SegmentEdge(i, x, z, out lat, out half, out shift, out t, out fx, out fz, out cd);
                if (e < best)
                {
                    best = e;
                    seg = i;
                }
            }
            return best;
        }

        /// <summary>True when some point of the edge A–B may lie in a road: it comes within a candidate road's widest
        /// half width of that road's centreline (or, with corridors, the corridor distance at its middle is within half
        /// its length).</summary>
        private bool MayIntrude(double ax, double az, double bx, double bz)
        {
            for (int c = 0; c < _cand.Count; c++)
            {
                int o = 4 * c;
                if (SegmentSegment(ax, az, bx, bz, _candEnds[o], _candEnds[o + 1], _candEnds[o + 2], _candEnds[o + 3]) <= _candReach[c] + 0.02)
                    return true;
            }
            if (_corridors == null) return false;
            double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
            double half = 0.5 * Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            return _corridors.SignedDistance(mx, mz) <= half * 1.02 + 0.02;
        }

        /// <summary>Distance between the segments P0–P1 and Q0–Q1 (0 when they cross).</summary>
        private static double SegmentSegment(double p0x, double p0z, double p1x, double p1z, double q0x, double q0z, double q1x, double q1z)
        {
            double ux = p1x - p0x, uz = p1z - p0z, vx = q1x - q0x, vz = q1z - q0z;
            double d1 = Cross(vx, vz, p0x - q0x, p0z - q0z), d2 = Cross(vx, vz, p1x - q0x, p1z - q0z);
            double d3 = Cross(ux, uz, q0x - p0x, q0z - p0z), d4 = Cross(ux, uz, q1x - p0x, q1z - p0z);
            if ((d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0)) return 0;
            double best = PointSegment(p0x, p0z, q0x, q0z, q1x, q1z);
            best = Math.Min(best, PointSegment(p1x, p1z, q0x, q0z, q1x, q1z));
            best = Math.Min(best, PointSegment(q0x, q0z, p0x, p0z, p1x, p1z));
            return Math.Min(best, PointSegment(q1x, q1z, p0x, p0z, p1x, p1z));
        }

        private static double Cross(double ax, double az, double bx, double bz)
        {
            return ax * bz - az * bx;
        }

        private static double PointSegment(double px, double pz, double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            double t = l2 > 0 ? ((px - ax) * dx + (pz - az) * dz) / l2 : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            double qx = ax + t * dx - px, qz = az + t * dz - pz;
            return Math.Sqrt(qx * qx + qz * qz);
        }

        /// <summary>Inside a road: a carriageway, or the corridor when one is given. <paramref name="seg"/> is the
        /// nearest carriageway segment either way.</summary>
        private bool Inside(double x, double z, out int seg)
        {
            if (Carriageway(x, z, out seg) < 0) return true;
            return _corridors != null && _corridors.SignedDistance(x, z) < 0;
        }

        /// <summary>Side (-1 left, +1 right of its point order) of (x, z) relative to <paramref name="road"/>: its nearest
        /// candidate segment's carriageway centre; 0 when on it or the road has no candidate.</summary>
        private int SideOf(double x, double z, int road)
        {
            double bestD = double.PositiveInfinity, side = 0;
            for (int c = 0; c < _cand.Count; c++)
            {
                int i = _cand[c];
                if (_roads.SegmentRoad(i) != road) continue;
                double lat, t, fx, fz, cd;
                float half, shift;
                _roads.SegmentEdge(i, x, z, out lat, out half, out shift, out t, out fx, out fz, out cd);
                if (cd < bestD)
                {
                    bestD = cd;
                    side = lat - shift;
                }
            }
            return side > 1e-6 ? 1 : side < -1e-6 ? -1 : 0;
        }

        /// <summary>Own side of (x, z) relative to segment <paramref name="seg"/> (+1 on its centre).</summary>
        private int OwnSide(double x, double z, int seg)
        {
            double lat, t, fx, fz, cd;
            float half, shift;
            _roads.SegmentEdge(seg, x, z, out lat, out half, out shift, out t, out fx, out fz, out cd);
            return lat - shift < 0 ? -1 : 1;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Trimming

        /// <summary>Densifies the ring into buffer <paramref name="b"/>: an edge that may reach a road is halved until each
        /// piece that still may is at most <see cref="StepM"/> long, and only the starts of those pieces are classified
        /// (a piece that cannot reach a road lies outside); returns the point count.</summary>
        private int Densify(double[] x, double[] z, int n, Buf b, bool hotKnown)
        {
            if (!hotKnown) HotEdges(x, z, n);
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                int j = k + 1 == n ? 0 : k + 1;
                Piece(x[k], z[k], x[j], z[j], _hot[k], b, ref m);
            }
            return m;
        }

        private void Piece(double ax, double az, double bx, double bz, bool hot, Buf b, ref int m)
        {
            double dx = bx - ax, dz = bz - az;
            if (hot && dx * dx + dz * dz > StepM * StepM)
            {
                double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
                Piece(ax, az, mx, mz, MayIntrude(ax, az, mx, mz), b, ref m);
                Piece(mx, mz, bx, bz, MayIntrude(mx, mz, bx, bz), b, ref m);
                return;
            }
            b.Ensure(m + 1);
            b.X[m] = ax;
            b.Z[m] = az;
            int seg = -1;
            b.In[m] = hot && Inside(ax, az, out seg);
            b.Seg[m] = seg;
            m++;
        }

        private void TrimRing(double[] x, double[] z, int n, int depth, bool hotKnown)
        {
            Buf b = _bufs[depth];
            int m = Densify(x, z, n, b, hotKnown);
            int inside = 0;
            for (int k = 0; k < m; k++)
                if (b.In[k]) inside++;
            if (inside == 0)
            {
                // No sampled point lies in a road, but a wall between two may still cross a narrow one.
                if (Clean(x, z, n)) Emit(x, z, n);
                else SubtractFallback(x, z, n);
                return;
            }
            if (inside == m) return; // the whole outline lies in the road: nothing solid stands there

            // A short gap of outside points close to the road between two runs lies in the wedge the per-segment strips
            // leave on the outer side of a bend: it belongs to the run (moved with it, round the bend).
            CloseGaps(b, m);

            // Runs of inside points (cyclic), each with the outside points just before and after it.
            _runStart.Clear();
            _runEnd.Clear();
            for (int k = 0; k < m; k++)
            {
                if (!b.In[k] || b.In[(k + m - 1) % m]) continue;
                int end = k;
                while (b.In[(end + 1) % m]) end = (end + 1) % m;
                _runStart.Add(k);
                _runEnd.Add(end);
            }

            // A road with two or more runs whose ends lie on opposite sides of it runs right through the footprint.
            int split = -1;
            if (depth < MaxDepth)
            {
                for (int r = 0; r < _runStart.Count && split < 0; r++)
                {
                    RoadsOfRun(b, m, r);
                    for (int q = 0; q < _runRoads.Count; q++)
                    {
                        int road = _runRoads[q];
                        if (!Straddles(b, m, r, road)) continue;
                        int count = 0;
                        for (int r2 = 0; r2 < _runStart.Count; r2++)
                            if (RunHasRoad(b, m, r2, road) && Straddles(b, m, r2, road)) count++;
                        if (count >= 2 && (split < 0 || road < split)) split = road;
                    }
                }
            }
            if (split >= 0)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int sn = SplitSide(b, m, split, side);
                    if (sn >= 3) TrimRing(b.SX, b.SZ, sn, depth + 1, false);
                }
                return;
            }

            // Move every run to the edge of the roads it lies in, on the outline's side.
            if (_ox.Length < m)
            {
                _ox = new double[Math.Max(m, 2 * _ox.Length)];
                _oz = new double[_ox.Length];
            }
            for (int k = 0; k < m; k++)
            {
                _ox[k] = b.X[k];
                _oz[k] = b.Z[k];
            }
            for (int r = 0; r < _runStart.Count; r++)
            {
                int a = (_runStart[r] + m - 1) % m, e = (_runEnd[r] + 1) % m;
                for (int k = _runStart[r]; ; k = (k + 1) % m)
                {
                    double px = _ox[k], pz = _oz[k];
                    int seg = b.Seg[k];
                    for (int fix = 0; fix <= MaxFixes && seg >= 0; fix++)
                    {
                        int side = RunSide(b, a, e, _roads.SegmentRoad(seg));
                        if (side == 0) side = OwnSide(px, pz, seg);
                        Push(ref px, ref pz, seg, side);
                        // Pushed into another road (a junction corner): out of that one too.
                        if (Carriageway(px, pz, out seg) >= -1e-4) break;
                    }
                    _ox[k] = px;
                    _oz[k] = pz;
                    if (k == _runEnd[r]) break;
                }
            }
            int mo = Simplify(_ox, _oz, m);
            if (mo >= 3 && Clean(_ox, _oz, mo))
            {
                Emit(_ox, _oz, mo);
                return;
            }
            SubtractFallback(x, z, n);
        }

        /// <summary>Marks as inside every gap of at most <see cref="MaxGapPoints"/> outside points between inside ones
        /// whose points all lie within <see cref="GapNearM"/> of a carriageway, unless it is the outline's only stretch
        /// outside the road.</summary>
        private void CloseGaps(Buf b, int m)
        {
            int start = -1;
            for (int k = 0; k < m; k++)
            {
                if (b.In[k] && !b.In[(k + 1) % m])
                {
                    start = (k + 1) % m;
                    break;
                }
            }
            if (start < 0) return;
            int gaps = 0;
            for (int k = 0; k < m; k++)
                if (!b.In[k] && b.In[(k + m - 1) % m]) gaps++;
            if (gaps < 2) return;
            // Walk the gaps once round the ring, starting at one.
            for (int done = 0, k = start; done < m;)
            {
                if (b.In[k])
                {
                    k = (k + 1) % m;
                    done++;
                    continue;
                }
                int len = 0;
                bool near = true;
                int j = k;
                while (!b.In[j] && len <= m)
                {
                    int seg;
                    if (len < MaxGapPoints && (b.Seg[j] < 0 || Carriageway(b.X[j], b.Z[j], out seg) > GapNearM)) near = false;
                    len++;
                    j = (j + 1) % m;
                }
                if (near && len <= MaxGapPoints && gaps >= 2)
                {
                    for (int q = 0, i = k; q < len; q++, i = (i + 1) % m) b.In[i] = true;
                    gaps--;
                }
                done += len;
                k = j;
            }
        }

        /// <summary>The distinct roads the inside points of run <paramref name="r"/> lie in, into <see cref="_runRoads"/>.</summary>
        private void RoadsOfRun(Buf b, int m, int r)
        {
            _runRoads.Clear();
            for (int k = _runStart[r]; ; k = (k + 1) % m)
            {
                if (b.Seg[k] >= 0)
                {
                    int road = _roads.SegmentRoad(b.Seg[k]);
                    if (!_runRoads.Contains(road)) _runRoads.Add(road);
                }
                if (k == _runEnd[r]) break;
            }
        }

        private bool RunHasRoad(Buf b, int m, int r, int road)
        {
            for (int k = _runStart[r]; ; k = (k + 1) % m)
            {
                if (b.Seg[k] >= 0 && _roads.SegmentRoad(b.Seg[k]) == road) return true;
                if (k == _runEnd[r]) return false;
            }
        }

        /// <summary>True when the outside points before and after run <paramref name="r"/> lie on opposite sides of the road.</summary>
        private bool Straddles(Buf b, int m, int r, int road)
        {
            int a = (_runStart[r] + m - 1) % m, e = (_runEnd[r] + 1) % m;
            return SideOf(b.X[a], b.Z[a], road) * SideOf(b.X[e], b.Z[e], road) < 0;
        }

        /// <summary>The side of <paramref name="road"/> the run's neighbours (points a and e) agree on, or 0.</summary>
        private int RunSide(Buf b, int a, int e, int road)
        {
            int sa = SideOf(b.X[a], b.Z[a], road), se = SideOf(b.X[e], b.Z[e], road);
            if (sa == se) return sa;
            if (sa == 0) return se;
            if (se == 0) return sa;
            return 0;
        }

        /// <summary>
        /// Moves (x, z) out of segment <paramref name="seg"/>'s carriageway to its edge on <paramref name="side"/>
        /// (never back towards the road when it already lies beyond that edge), or radially out of a round end; then,
        /// with corridors, on outward until clear of the corridor.
        /// </summary>
        private void Push(ref double x, ref double z, int seg, int side)
        {
            double lat, tRaw, fx, fz, cd;
            float half, shift;
            double e = _roads.SegmentEdge(seg, x, z, out lat, out half, out shift, out tRaw, out fx, out fz, out cd);
            double ux, uz;
            _roads.SegmentDirection(seg, out ux, out uz);
            double vx = uz * side, vz = -ux * side; // outward: the right normal (uz, -ux) times the side
            if (tRaw < 0 || tRaw > 1)
            {
                // Past a segment end, where a carriageway with round ends (W1 widths) caps it: out along the radius.
                double ox = x - fx, oz = z - fz, l = Math.Sqrt(ox * ox + oz * oz);
                if (l > 1e-6)
                {
                    vx = ox / l;
                    vz = oz / l;
                }
                if (e < ClearM)
                {
                    double rad = half + Math.Abs(shift) + ClearM;
                    x = fx + vx * rad;
                    z = fz + vz * rad;
                }
            }
            else
            {
                double target = shift + side * (half + ClearM);
                double move = target - lat;
                if (move * side > 0)
                {
                    // lateral grows along the right normal (uz, -ux).
                    x += uz * move;
                    z += -ux * move;
                }
            }
            if (_corridors == null) return;
            for (int it = 0; it < MaxCorridorSteps && _corridors.SignedDistance(x, z) < 0; it++)
            {
                x += vx * CorridorStepM;
                z += vz * CorridorStepM;
            }
        }

        /// <summary>
        /// The outline on <paramref name="side"/> of a road that runs through the footprint, into the buffer's split
        /// arrays: points outside and on that side stay, every other point goes onto that side's edge (the corridor
        /// edge with corridors). Returns the point count after simplification.
        /// </summary>
        private int SplitSide(Buf b, int m, int road, int side)
        {
            for (int k = 0; k < m; k++)
            {
                double px = b.X[k], pz = b.Z[k];
                if (b.In[k] || SideOf(px, pz, road) != side)
                {
                    int seg = NearestOfRoad(px, pz, road);
                    if (seg >= 0) OntoEdge(ref px, ref pz, seg, side);
                }
                b.SX[k] = px;
                b.SZ[k] = pz;
            }
            return Simplify(b.SX, b.SZ, m);
        }

        private int NearestOfRoad(double x, double z, int road)
        {
            double bestD = double.PositiveInfinity;
            int best = -1;
            for (int c = 0; c < _cand.Count; c++)
            {
                int i = _cand[c];
                if (_roads.SegmentRoad(i) != road) continue;
                double lat, t, fx, fz, cd;
                float half, shift;
                _roads.SegmentEdge(i, x, z, out lat, out half, out shift, out t, out fx, out fz, out cd);
                if (cd < bestD)
                {
                    bestD = cd;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Puts (x, z) on the edge of segment <paramref name="seg"/> on <paramref name="side"/> (beyond its
        /// carriageway, and beyond the corridor when given), wherever it lay across the line.</summary>
        private void OntoEdge(ref double x, ref double z, int seg, int side)
        {
            double lat, tRaw, fx, fz, cd;
            float half, shift;
            _roads.SegmentEdge(seg, x, z, out lat, out half, out shift, out tRaw, out fx, out fz, out cd);
            double ux, uz;
            _roads.SegmentDirection(seg, out ux, out uz);
            double off = shift + side * (half + ClearM);
            x = fx + uz * off;
            z = fz - ux * off;
            if (_corridors == null) return;
            for (int it = 0; it < MaxCorridorSteps && _corridors.SignedDistance(x, z) < 0; it++)
            {
                x += uz * side * CorridorStepM;
                z += -ux * side * CorridorStepM;
            }
        }

        /// <summary>Drops points closer than 5 cm to the previous one and points within 2 cm of the chord between their
        /// neighbours (the pushed runs along a straight edge collapse to their ends); returns the new count.</summary>
        private static int Simplify(double[] x, double[] z, int n)
        {
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                if (m > 0 && Math.Abs(x[k] - x[m - 1]) < 0.05 && Math.Abs(z[k] - z[m - 1]) < 0.05) continue;
                x[m] = x[k];
                z[m] = z[k];
                m++;
            }
            while (m > 1 && Math.Abs(x[m - 1] - x[0]) < 0.05 && Math.Abs(z[m - 1] - z[0]) < 0.05) m--;
            for (int pass = 0; pass < 2 && m > 3; pass++)
            {
                // In place: x[0 .. w) are the points kept so far, x[k ..] the ones still to look at.
                int w = 0;
                for (int k = 0; k < m; k++)
                {
                    double px = w > 0 ? x[w - 1] : x[m - 1], pz = w > 0 ? z[w - 1] : z[m - 1];
                    double qx = k + 1 < m ? x[k + 1] : x[0], qz = k + 1 < m ? z[k + 1] : z[0];
                    double ex = qx - px, ez = qz - pz, l2 = ex * ex + ez * ez;
                    if (l2 > 1e-12 && w + (m - k - 1) >= 3)
                    {
                        double dev = Math.Abs((x[k] - px) * ez - (z[k] - pz) * ex) / Math.Sqrt(l2);
                        double along = ((x[k] - px) * ex + (z[k] - pz) * ez) / l2;
                        if (dev < 0.02 && along > 0 && along < 1) continue; // on the chord: drop it
                    }
                    x[w] = x[k];
                    z[w] = z[k];
                    w++;
                }
                m = w;
            }
            return m;
        }

        /// <summary>True when no wall of the outline lies more than <see cref="MaxInsideM"/> inside a carriageway.</summary>
        private bool Clean(double[] x, double[] z, int n)
        {
            for (int k = 0; k < n; k++)
            {
                int j = k + 1 == n ? 0 : k + 1;
                if (!MayIntrude(x[k], z[k], x[j], z[j])) continue;
                double dx = x[j] - x[k], dz = z[j] - z[k];
                int pieces = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / CheckStepM));
                for (int q = 0; q < pieces; q++)
                {
                    double f = q / (double)pieces;
                    int seg;
                    if (Carriageway(x[k] + dx * f, z[k] + dz * f, out seg) < -MaxInsideM) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The fallback: the ring minus the strip of the road segment its deepest wall point lies in (grown by
        /// <see cref="ClearM"/>), repeated for whatever still lies in a road, up to <see cref="MaxClips"/> strips. A strip
        /// is a convex quad, so the difference is exact: the parts beyond each of its sides in turn (beyond the right
        /// edge; else beyond the left; else before its start; else past its end), each its own outline.
        /// </summary>
        private void SubtractFallback(double[] x, double[] z, int n)
        {
            _fbX.Clear();
            _fbZ.Clear();
            _fbX.Add(Copy(x, n));
            _fbZ.Add(Copy(z, n));
            for (int clip = 0; clip < MaxClips && _fbX.Count > 0; clip++)
            {
                int seg = -1;
                double best = -MaxInsideM, qx, qz;
                for (int p = 0; p < _fbX.Count; p++)
                {
                    int s;
                    if (Deepest(_fbX[p], _fbZ[p], _fbX[p].Length, best, out s, out qx, out qz, out double e))
                    {
                        seg = s;
                        best = e;
                    }
                }
                if (seg < 0) break;
                double ax, az, bx, bz, ux, uz, lo, hi;
                _roads.SegmentEnds(seg, out ax, out az, out bx, out bz);
                _roads.SegmentDirection(seg, out ux, out uz);
                _roads.SegmentStrip(seg, out lo, out hi);
                double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                double cap = _roads.RoundEnds ? hi : 0.0; // a capsule's round end, boxed
                lo -= ClearM;
                hi += ClearM;
                // Outward half-planes (n·(p − A) ≥ c is outside that side): right edge, left edge, start, end.
                double rx = uz, rz = -ux;
                _hpX[0] = rx; _hpZ[0] = rz; _hpC[0] = hi;
                _hpX[1] = -rx; _hpZ[1] = -rz; _hpC[1] = -lo;
                _hpX[2] = -ux; _hpZ[2] = -uz; _hpC[2] = cap + ClearM;
                _hpX[3] = ux; _hpZ[3] = uz; _hpC[3] = len + cap + ClearM;
                _nextX.Clear();
                _nextZ.Clear();
                for (int p = 0; p < _fbX.Count; p++)
                {
                    for (int side = 0; side < 4; side++)
                    {
                        double[] px = _fbX[p], pz = _fbZ[p];
                        int pn = px.Length;
                        for (int k = 0; k < side && pn >= 3; k++)
                        {
                            // Inside that side's half-plane (the strip lies on this side of it).
                            pn = Clip(px, pz, pn, ax, az, -_hpX[k], -_hpZ[k], -_hpC[k]);
                            px = Copy(_clipX, pn);
                            pz = Copy(_clipZ, pn);
                        }
                        if (pn < 3) continue;
                        pn = Clip(px, pz, pn, ax, az, _hpX[side], _hpZ[side], _hpC[side]);
                        if (pn < 3) continue;
                        pn = Simplify(_clipX, _clipZ, pn);
                        if (pn < 3 || Math.Abs(Area(_clipX, _clipZ, pn)) < 0.05) continue;
                        _nextX.Add(Copy(_clipX, pn));
                        _nextZ.Add(Copy(_clipZ, pn));
                    }
                }
                List<double[]> tx = _fbX, tz = _fbZ;
                _fbX = _nextX;
                _fbZ = _nextZ;
                _nextX = tx;
                _nextZ = tz;
            }
            for (int p = 0; p < _fbX.Count; p++) Emit(_fbX[p], _fbZ[p], _fbX[p].Length);
        }

        private static double[] Copy(double[] a, int n)
        {
            var c = new double[n];
            Array.Copy(a, c, n);
            return c;
        }

        /// <summary>Signed area of a polygon (counter-clockwise positive).</summary>
        private static double Area(double[] x, double[] z, int n)
        {
            double a = 0;
            for (int k = 0, j = n - 1; k < n; j = k++) a += x[j] * z[k] - x[k] * z[j];
            return 0.5 * a;
        }

        /// <summary>Sutherland–Hodgman: the part of the polygon (x, z) where (p − o)·(nx, nz) ≥ <paramref name="c"/>, into
        /// <see cref="_clipX"/>/<see cref="_clipZ"/>; returns its point count.</summary>
        private int Clip(double[] x, double[] z, int n, double ox, double oz, double nx, double nz, double c)
        {
            if (_clipX.Length < 2 * n + 2)
            {
                _clipX = new double[2 * n + 2];
                _clipZ = new double[_clipX.Length];
            }
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                int j = k + 1 == n ? 0 : k + 1;
                double fk = (x[k] - ox) * nx + (z[k] - oz) * nz - c, fj = (x[j] - ox) * nx + (z[j] - oz) * nz - c;
                if (fk >= 0)
                {
                    _clipX[m] = x[k];
                    _clipZ[m] = z[k];
                    m++;
                }
                if (fk >= 0 != fj >= 0)
                {
                    double t = fk / (fk - fj);
                    _clipX[m] = x[k] + (x[j] - x[k]) * t;
                    _clipZ[m] = z[k] + (z[j] - z[k]) * t;
                    m++;
                }
            }
            return m;
        }

        /// <summary>The wall point deepest inside a carriageway (deeper than <paramref name="than"/>), its segment and
        /// depth.</summary>
        private bool Deepest(double[] x, double[] z, int n, double than, out int seg, out double qx, out double qz, out double depth)
        {
            depth = than;
            seg = -1;
            qx = qz = 0;
            for (int k = 0; k < n; k++)
            {
                int j = k + 1 == n ? 0 : k + 1;
                if (!MayIntrude(x[k], z[k], x[j], z[j])) continue;
                double dx = x[j] - x[k], dz = z[j] - z[k];
                int pieces = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / CheckStepM));
                for (int q = 0; q <= pieces; q++)
                {
                    double f = q / (double)pieces;
                    double px = x[k] + dx * f, pz = z[k] + dz * f;
                    int s;
                    double e = Carriageway(px, pz, out s);
                    if (e < depth)
                    {
                        depth = e;
                        seg = s;
                        qx = px;
                        qz = pz;
                    }
                }
            }
            return seg >= 0;
        }

        private void Emit(double[] x, double[] z, int n)
        {
            for (int k = 0; k < n; k++)
            {
                _outX.Add(x[k]);
                _outZ.Add(z[k]);
            }
            _pieceStart.Add(_outX.Count);
        }
    }
}
