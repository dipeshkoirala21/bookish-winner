using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing.Roads
{
    /// <summary>
    /// The clear road corridors of one tile (docs/W2_DETAIL_CONTRACT.md decision 1, §3), in game metres: exactly the
    /// corridor the road mesher draws. Every drawn piece (footways and paths included; tunnels and building passages
    /// excluded, and the stretches of mapped sidewalks absorbed into their street) contributes its smoothed centreline
    /// (<see cref="RoadLayout.Centres"/>), split at its width profile's samples, as a chain of segments offset to the
    /// carriageway centre (<see cref="RoadWidthProfile.ShiftAt"/>: the dual-carriageway or corridor shift), half as wide as
    /// the wider side of the drawn extent (<see cref="RoadLayout.CorridorSides"/>: carriageway, footpath, shoulder) at each
    /// end (linear between) and never less than half of
    /// <see cref="RoadClearance.MinCorridorM"/>, so 4.8 m stays clear everywhere. Junction caps and roundabouts (ring,
    /// entries and island) contribute their outlines. A uniform grid of <see cref="CellM"/> cells makes
    /// both queries local. Built once per decoded tile and cached (<see cref="ForTile"/>); immutable, so any thread may
    /// query it. Deterministic.
    /// </summary>
    public sealed class RoadCorridorIndex : IRoadCorridorQuery
    {
        /// <summary>Grid cell size.</summary>
        public const double CellM = 16.0;

        /// <summary>Farthest a <see cref="SignedDistance"/> query looks for a corridor; beyond it the answer is this bound.</summary>
        public const double MaxSearchM = 64.0;

        private static readonly ConditionalWeakTable<TileData, RoadCorridorIndex> Cache = new ConditionalWeakTable<TileData, RoadCorridorIndex>();

        private readonly double _x0, _z0, _size;
        private readonly int _cells;

        // Segment primitives (tile-local metres): A, B, half widths at A and B, road index.
        private readonly double[] _ax, _az, _bx, _bz;
        private readonly float[] _ha, _hb;
        private readonly int[] _road;
        private readonly int _segCount;

        // Polygon primitives: start and count into the point arrays.
        private readonly double[] _px, _pz;
        private readonly int[] _polyStart, _polyCount;
        private readonly int _polyCount2;

        // Grid (CSR): items are segment indices (>= 0) or ~polygon index (< 0).
        private readonly int[] _cellStart, _items;

        /// <summary>Largest corridor half width in the tile.</summary>
        public readonly double MaxHalfWidthM;

        /// <summary>Number of corridor segments (pieces' centreline steps).</summary>
        public int SegmentCount
        {
            get { return _segCount; }
        }

        /// <summary>Number of corridor polygons (junction caps and roundabouts).</summary>
        public int PolygonCount
        {
            get { return _polyCount2; }
        }

        /// <summary>The corridor index of a tile (built on first use, then cached for the tile's lifetime).</summary>
        public static RoadCorridorIndex ForTile(TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return Cache.GetValue(t, k => new RoadCorridorIndex(k));
        }

        private RoadCorridorIndex(TileData t)
        {
            _x0 = t.Tile.X0;
            _z0 = t.Tile.Z0;
            _size = t.Tile.Size;
            _cells = Math.Max(1, (int)Math.Ceiling(_size / CellM));
            var ax = new List<double>();
            var az = new List<double>();
            var bx = new List<double>();
            var bz = new List<double>();
            var ha = new List<float>();
            var hb = new List<float>();
            var road = new List<int>();
            var px = new List<double>();
            var pz = new List<double>();
            var ps = new List<int>();
            var pc = new List<int>();
            double maxHalf = 0.5 * RoadClearance.MinCorridorM;
            if (t.Roads.Count > 0)
            {
                RoadLayout layout = RoadLayout.For(t);
                var o = new RoadOptions();
                var knots = new List<double>();
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    // Building passages keep the house over them whole (RSTR Passage): no clear corridor.
                    if (!RoadMesher.IsDrawn(t, i, o) || layout.IsPassage(i)) continue;
                    RoadCentreline c = layout.Centres[i];
                    if (c == null || c.Count < 2) continue;
                    RoadWidthProfile prof = layout.Profiles[i];
                    // Split the centreline steps at the width profile's samples too, so the linear band of every segment
                    // holds the profile (piecewise linear between its samples) as well as the ribbon (straight between its
                    // rows at the centreline points), and where the shoulders switch to the border column (the wider side
                    // there). Each segment is offset to the carriageway centre (a dual carriageway's or a shifted corridor's
                    // carriageway is not centred on the centreline).
                    Knots(layout, t.Roads[i], i, c, knots);
                    int kn = 0;
                    float lPrev, rPrev;
                    layout.CorridorSides(i, c.S[0], out lPrev, out rPrev);
                    double sPrev = c.S[0], txPrev = c.Tx[0], tzPrev = c.Tz[0], px0 = c.X[0], pz0 = c.Z[0];
                    for (int k = 0; k + 1 < c.Count; k++)
                    {
                        double s0 = c.S[k], s1 = c.S[k + 1];
                        float lK, rK, lK1, rK1;
                        layout.CorridorSides(i, s0, out lK, out rK);
                        layout.CorridorSides(i, s1, out lK1, out rK1);
                        while (kn < knots.Count && knots[kn] <= s0 + 1e-6) kn++;
                        while (true)
                        {
                            bool knot = kn < knots.Count && knots[kn] < s1 - 1e-6;
                            double nx, nz, ntx, ntz, sNext;
                            float lNext, rNext;
                            if (knot)
                            {
                                double sj = knots[kn++];
                                double f = s1 > s0 ? (sj - s0) / (s1 - s0) : 0.0;
                                nx = c.X[k] + (c.X[k + 1] - c.X[k]) * f;
                                nz = c.Z[k] + (c.Z[k + 1] - c.Z[k]) * f;
                                ntx = c.Tx[k] + (c.Tx[k + 1] - c.Tx[k]) * f;
                                ntz = c.Tz[k] + (c.Tz[k + 1] - c.Tz[k]) * f;
                                sNext = sj;
                                // The ribbon draws straight between its rows at the centreline points: never less than the
                                // chord between the step's ends either; at a step in the shoulders, the wider side.
                                float la, ra, lb, rb;
                                layout.CorridorSides(i, sj - KnotSideM, out la, out ra);
                                layout.CorridorSides(i, sj + KnotSideM, out lb, out rb);
                                lNext = Math.Max(lK + (lK1 - lK) * (float)f, Math.Max(la, lb));
                                rNext = Math.Max(rK + (rK1 - rK) * (float)f, Math.Max(ra, rb));
                            }
                            else
                            {
                                nx = c.X[k + 1];
                                nz = c.Z[k + 1];
                                ntx = c.Tx[k + 1];
                                ntz = c.Tz[k + 1];
                                sNext = s1;
                                lNext = lK1;
                                rNext = rK1;
                            }
                            // Stretches absorbed into a neighbouring street (mapped sidewalks, footway crossings) lie inside
                            // that street's corridor already.
                            if (!layout.InAbsorbed(i, 0.5 * (sPrev + sNext)))
                            {
                                float offA, hA, offB, hB;
                                Band(lPrev, rPrev, prof.ShiftAt(sPrev), out offA, out hA);
                                Band(lNext, rNext, prof.ShiftAt(sNext), out offB, out hB);
                                double ua, va, ub, vb;
                                Normal(txPrev, tzPrev, out ua, out va);
                                Normal(ntx, ntz, out ub, out vb);
                                ax.Add(px0 + ua * offA);
                                az.Add(pz0 + va * offA);
                                bx.Add(nx + ub * offB);
                                bz.Add(nz + vb * offB);
                                ha.Add(hA);
                                hb.Add(hB);
                                road.Add(i);
                                maxHalf = Math.Max(maxHalf, Math.Max(hA + Math.Abs(offA), hB + Math.Abs(offB)));
                            }
                            lPrev = lNext;
                            rPrev = rNext;
                            sPrev = sNext;
                            txPrev = ntx;
                            tzPrev = ntz;
                            px0 = nx;
                            pz0 = nz;
                            if (!knot) break;
                        }
                    }
                }
                foreach (JunctionCap cap in layout.Caps)
                {
                    ps.Add(px.Count);
                    pc.Add(cap.Count);
                    for (int k = 0; k < cap.Count; k++)
                    {
                        px.Add(cap.PolyX[k]);
                        pz.Add(cap.PolyZ[k]);
                    }
                }
                var xs = new List<double>();
                var zs = new List<double>();
                foreach (RoadRing ring in layout.Rings)
                {
                    // The whole circle (carriageway and island), then every entry patch.
                    int segs = ring.CircleSegments;
                    ps.Add(px.Count);
                    pc.Add(segs);
                    for (int k = 0; k < segs; k++)
                    {
                        double a = 2 * Math.PI * k / segs;
                        px.Add(ring.X + ring.OuterRadiusM * Math.Cos(a));
                        pz.Add(ring.Z + ring.OuterRadiusM * Math.Sin(a));
                    }
                    for (int a = 0; a < ring.Arms.Length; a++)
                    {
                        xs.Clear();
                        zs.Clear();
                        ring.Patch(a, xs, zs);
                        ps.Add(px.Count);
                        pc.Add(xs.Count);
                        px.AddRange(xs);
                        pz.AddRange(zs);
                    }
                }
            }
            _ax = ax.ToArray();
            _az = az.ToArray();
            _bx = bx.ToArray();
            _bz = bz.ToArray();
            _ha = ha.ToArray();
            _hb = hb.ToArray();
            _road = road.ToArray();
            _segCount = _ax.Length;
            _px = px.ToArray();
            _pz = pz.ToArray();
            _polyStart = ps.ToArray();
            _polyCount = pc.ToArray();
            _polyCount2 = _polyStart.Length;
            MaxHalfWidthM = maxHalf;
            // Grid: count, then fill.
            int nc = _cells * _cells;
            var count = new int[nc + 1];
            ForEachCell(true, count, null);
            _cellStart = new int[nc + 1];
            for (int k = 0; k < nc; k++) _cellStart[k + 1] = _cellStart[k] + count[k];
            _items = new int[_cellStart[nc]];
            var fill = new int[nc];
            Array.Copy(_cellStart, fill, nc);
            ForEachCell(false, null, fill);
        }

        /// <summary>The corridor band from the drawn extents left and right of the centreline and the carriageway's shift:
        /// centred on the carriageway (offset <paramref name="shift"/>, positive = left), half wide enough for the wider
        /// side and at least half of <see cref="RoadClearance.MinCorridorM"/>. Centring on the carriageway (not on the
        /// middle of the extents) keeps the offset as smooth as the shift, so a segment never skews where a footpath
        /// starts on one side.</summary>
        internal static void Band(float left, float right, float shift, out float off, out float half)
        {
            off = shift;
            half = Math.Max(0.5f * RoadClearance.MinCorridorM, Math.Max(left - shift, right + shift));
        }

        /// <summary>Unit left normal of a (not necessarily unit) tangent.</summary>
        private static void Normal(double tx, double tz, out double ux, out double uz)
        {
            double l = Math.Sqrt(tx * tx + tz * tz);
            if (l < 1e-12)
            {
                ux = 0;
                uz = 1;
                return;
            }
            ux = -tz / l;
            uz = tx / l;
        }

        /// <summary>Half the gap either side of a knot over which its half width is the larger of the two sides.</summary>
        private const double KnotSideM = 1e-3;

        /// <summary>The raw along values (ascending, inside the piece) where a piece's corridor segments must break: the
        /// width profile's samples, the shoulders' border-blend boundaries next to tile-border cuts and the ends of absorbed
        /// stretches.</summary>
        internal static void Knots(RoadLayout layout, RoadRecord r, int road, RoadCentreline c, List<double> knots)
        {
            knots.Clear();
            double len = c.S[c.Count - 1], step = layout.Profiles[road].StepM;
            if (step > 0)
                for (int j = 1; j * step < len; j++) knots.Add(j * step);
            if (r.HasPrevContext && RoadLayout.BorderBlendM < len) knots.Add(RoadLayout.BorderBlendM);
            double endBlend = layout.Profiles[road].LengthM - RoadLayout.BorderBlendM;
            if (r.HasNextContext && endBlend > 0) knots.Add(endBlend);
            // The ends of absorbed stretches, so every segment is wholly in or out of one.
            double[] ab = layout.Absorbed[road];
            if (ab != null)
                foreach (double v in ab)
                    if (v > 1e-6 && v < len - 1e-6) knots.Add(v);
            knots.Sort();
        }

        private void ForEachCell(bool counting, int[] count, int[] fill)
        {
            for (int s = 0; s < _segCount; s++)
            {
                double h = Math.Max(_ha[s], _hb[s]);
                Cover(Math.Min(_ax[s], _bx[s]) - h, Math.Min(_az[s], _bz[s]) - h, Math.Max(_ax[s], _bx[s]) + h, Math.Max(_az[s], _bz[s]) + h, s, counting,
                      count, fill);
            }
            for (int p = 0; p < _polyCount2; p++)
            {
                double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                for (int k = 0; k < _polyCount[p]; k++)
                {
                    int q = _polyStart[p] + k;
                    x0 = Math.Min(x0, _px[q]);
                    z0 = Math.Min(z0, _pz[q]);
                    x1 = Math.Max(x1, _px[q]);
                    z1 = Math.Max(z1, _pz[q]);
                }
                Cover(x0, z0, x1, z1, ~p, counting, count, fill);
            }
        }

        private void Cover(double x0, double z0, double x1, double z1, int item, bool counting, int[] count, int[] fill)
        {
            int i0 = CellOf(x0), i1 = CellOf(x1), j0 = CellOf(z0), j1 = CellOf(z1);
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    int c = j * _cells + i;
                    if (counting) count[c]++;
                    else _items[fill[c]++] = item;
                }
            }
        }

        private int CellOf(double v)
        {
            int c = (int)Math.Floor(v / CellM);
            return c < 0 ? 0 : c >= _cells ? _cells - 1 : c;
        }

        /// <summary>Signed distance (m) from game (x, z) to the nearest corridor edge: negative inside a corridor. Points
        /// farther than <see cref="MaxSearchM"/> from every corridor of the tile get <see cref="MaxSearchM"/>.</summary>
        public double SignedDistance(double x, double z)
        {
            double lx = x - _x0, lz = z - _z0;
            if (_segCount == 0 && _polyCount2 == 0) return MaxSearchM;
            int ci = (int)Math.Floor(lx / CellM), cj = (int)Math.Floor(lz / CellM);
            double best = double.PositiveInfinity;
            int rings = (int)Math.Ceiling(MaxSearchM / CellM);
            for (int r = 0; r <= rings; r++)
            {
                for (int j = cj - r; j <= cj + r; j++)
                {
                    if (j < 0 || j >= _cells) continue;
                    bool edgeRow = j == cj - r || j == cj + r;
                    for (int i = ci - r; i <= ci + r; i += edgeRow ? 1 : Math.Max(1, 2 * r))
                    {
                        if (i < 0 || i >= _cells) continue;
                        int c = j * _cells + i;
                        for (int q = _cellStart[c]; q < _cellStart[c + 1]; q++)
                        {
                            double d = ItemDistance(_items[q], lx, lz);
                            if (d < best) best = d;
                        }
                    }
                }
                // Cells of the next ring are at least r cells away (the point lies in its own cell).
                if (best <= r * CellM) break;
            }
            return Math.Min(best, MaxSearchM);
        }

        private double ItemDistance(int item, double x, double z)
        {
            if (item >= 0)
            {
                double t;
                double d = SegDistance(x, z, _ax[item], _az[item], _bx[item], _bz[item], out t);
                return d - (_ha[item] + (_hb[item] - _ha[item]) * t);
            }
            int p = ~item;
            return PolygonSignedDistance(_px, _pz, _polyStart[p], _polyCount[p], x, z);
        }

        /// <summary>
        /// The road whose corridor is nearest to game (x, z) (an index into <see cref="TileData.Roads"/>; −1 for a junction cap
        /// or roundabout, or when nothing lies within <see cref="MaxSearchM"/>), with the signed distance to it.
        /// </summary>
        public int NearestRoad(double x, double z, out double signedDistance)
        {
            double lx = x - _x0, lz = z - _z0;
            signedDistance = MaxSearchM;
            int bestRoad = -1;
            if (_segCount == 0 && _polyCount2 == 0) return -1;
            int ci = (int)Math.Floor(lx / CellM), cj = (int)Math.Floor(lz / CellM);
            double best = double.PositiveInfinity;
            int rings = (int)Math.Ceiling(MaxSearchM / CellM);
            for (int r = 0; r <= rings; r++)
            {
                for (int j = cj - r; j <= cj + r; j++)
                {
                    if (j < 0 || j >= _cells) continue;
                    bool edgeRow = j == cj - r || j == cj + r;
                    for (int i = ci - r; i <= ci + r; i += edgeRow ? 1 : Math.Max(1, 2 * r))
                    {
                        if (i < 0 || i >= _cells) continue;
                        int c = j * _cells + i;
                        for (int q = _cellStart[c]; q < _cellStart[c + 1]; q++)
                        {
                            int item = _items[q];
                            double d = ItemDistance(item, lx, lz);
                            if (d < best)
                            {
                                best = d;
                                bestRoad = item >= 0 ? _road[item] : -1;
                            }
                        }
                    }
                }
                if (best <= r * CellM) break;
            }
            if (best > MaxSearchM) return -1;
            signedDistance = best;
            return bestRoad;
        }

        /// <summary>True when the polygon (n points, game metres) intrudes into any corridor; depth = deepest intrusion
        /// (how far the corridor reaches into it, metres; 0 when it does not).</summary>
        public bool Overlaps(double[] x, double[] z, int n, out double depthM)
        {
            depthM = 0;
            if (x == null || z == null || n < 1) return false;
            double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
            for (int k = 0; k < n; k++)
            {
                double lx = x[k] - _x0, lz = z[k] - _z0;
                x0 = Math.Min(x0, lx);
                z0 = Math.Min(z0, lz);
                x1 = Math.Max(x1, lx);
                z1 = Math.Max(z1, lz);
            }
            if (x1 < -MaxHalfWidthM || z1 < -MaxHalfWidthM || x0 > _size + MaxHalfWidthM || z0 > _size + MaxHalfWidthM) return false;
            int i0 = CellOf(x0), i1 = CellOf(x1), j0 = CellOf(z0), j1 = CellOf(z1);
            double best = 0;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    int c = j * _cells + i;
                    for (int q = _cellStart[c]; q < _cellStart[c + 1]; q++)
                    {
                        int item = _items[q];
                        double d = item >= 0 ? SegmentIntrusion(item, x, z, n) : PolygonIntrusion(~item, x, z, n);
                        if (d > best) best = d;
                    }
                }
            }
            depthM = best;
            return best > 1e-4;
        }

        /// <summary>How deep a segment corridor reaches into the polygon: the half width where its centreline lies inside
        /// the polygon (or crosses its outline), else the half width minus the gap between centreline and outline.</summary>
        private double SegmentIntrusion(int s, double[] x, double[] z, int n)
        {
            double ax = _ax[s] + _x0, az = _az[s] + _z0, bx = _bx[s] + _x0, bz = _bz[s] + _z0;
            double ha = _ha[s], hb = _hb[s];
            double best = 0;
            if (n >= 3)
            {
                if (Inside(x, z, n, ax, az)) best = Math.Max(best, ha);
                if (Inside(x, z, n, bx, bz)) best = Math.Max(best, hb);
            }
            double gap = double.PositiveInfinity, gapT = 0;
            for (int k = 0; k < n; k++)
            {
                int q = n == 1 ? k : (k + 1) % n;
                double cx = x[k], cz = z[k], dx = x[q], dz = z[q];
                double t, u;
                double d = SegSeg(ax, az, bx, bz, cx, cz, dx, dz, out t, out u);
                if (d < 1e-9)
                {
                    // The centreline crosses the outline: the corridor reaches its full half width in.
                    best = Math.Max(best, ha + (hb - ha) * t);
                }
                if (d < gap)
                {
                    gap = d;
                    gapT = t;
                }
                if (n == 1) break;
            }
            double reach = ha + (hb - ha) * gapT - gap;
            return Math.Max(best, reach);
        }

        private double PolygonIntrusion(int p, double[] x, double[] z, int n)
        {
            int st = _polyStart[p], cnt = _polyCount[p];
            double best = 0;
            for (int k = 0; k < n; k++)
            {
                double d = PolygonSignedDistance(_px, _pz, st, cnt, x[k] - _x0, z[k] - _z0);
                if (-d > best) best = -d;
            }
            if (n >= 3)
            {
                for (int k = 0; k < cnt; k++)
                {
                    double gx = _px[st + k] + _x0, gz = _pz[st + k] + _z0;
                    if (!Inside(x, z, n, gx, gz)) continue;
                    double d = double.PositiveInfinity;
                    for (int e = 0; e < n; e++)
                    {
                        int q = (e + 1) % n;
                        double t;
                        d = Math.Min(d, SegDistance(gx, gz, x[e], z[e], x[q], z[q], out t));
                    }
                    if (d > best) best = d;
                }
            }
            return best;
        }

        // -------------------------------------------------------------------------------------------------------
        // Geometry
        // -------------------------------------------------------------------------------------------------------

        private static bool Inside(double[] x, double[] z, int n, double px, double pz)
        {
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double ax = x[j], az = z[j], bx = x[i], bz = z[i];
                if ((az > pz) != (bz > pz) && px < (bx - ax) * (pz - az) / (bz - az) + ax) inside = !inside;
            }
            return inside;
        }

        /// <summary>Distance from p to segment a-b and the parameter of the closest point.</summary>
        internal static double SegDistance(double px, double pz, double ax, double az, double bx, double bz, out double t)
        {
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            t = l2 > 1e-12 ? ((px - ax) * dx + (pz - az) * dz) / l2 : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            double qx = ax + dx * t - px, qz = az + dz * t - pz;
            return Math.Sqrt(qx * qx + qz * qz);
        }

        /// <summary>Distance between segments a-b and c-d with the parameters of the closest points (0 when they cross).</summary>
        internal static double SegSeg(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz, out double t, out double u)
        {
            double rx = bx - ax, rz = bz - az, sx = dx - cx, sz = dz - cz;
            double den = rx * sz - rz * sx;
            if (Math.Abs(den) > 1e-12)
            {
                double qx = cx - ax, qz = cz - az;
                double tt = (qx * sz - qz * sx) / den, uu = (qx * rz - qz * rx) / den;
                if (tt >= 0 && tt <= 1 && uu >= 0 && uu <= 1)
                {
                    t = tt;
                    u = uu;
                    return 0;
                }
            }
            double best, tc;
            best = SegDistance(cx, cz, ax, az, bx, bz, out tc);
            t = tc;
            u = 0;
            double d = SegDistance(dx, dz, ax, az, bx, bz, out tc);
            if (d < best)
            {
                best = d;
                t = tc;
                u = 1;
            }
            double uc;
            d = SegDistance(ax, az, cx, cz, dx, dz, out uc);
            if (d < best)
            {
                best = d;
                t = 0;
                u = uc;
            }
            d = SegDistance(bx, bz, cx, cz, dx, dz, out uc);
            if (d < best)
            {
                best = d;
                t = 1;
                u = uc;
            }
            return best;
        }

        /// <summary>Signed distance to a closed polygon stored in (px, pz) from <paramref name="start"/> (negative inside).</summary>
        private static double PolygonSignedDistance(double[] px, double[] pz, int start, int n, double x, double z)
        {
            if (n < 3) return double.PositiveInfinity;
            double best = double.PositiveInfinity;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double ax = px[start + j], az = pz[start + j], bx = px[start + i], bz = pz[start + i];
                if ((az > z) != (bz > z) && x < (bx - ax) * (z - az) / (bz - az) + ax) inside = !inside;
                double t;
                double d = SegDistance(x, z, ax, az, bx, bz, out t);
                if (d < best) best = d;
            }
            return inside ? -best : best;
        }
    }
}
