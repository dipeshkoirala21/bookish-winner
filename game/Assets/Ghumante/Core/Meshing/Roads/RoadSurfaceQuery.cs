using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing.Roads
{
    /// <summary>What lies under a point of the drawn road surface (<see cref="RoadSurfaceQuery.TrySample"/>).</summary>
    public struct RoadSurfaceHit
    {
        /// <summary>The road piece (index into <see cref="TileData.Roads"/>); −1 on a junction cap or roundabout.</summary>
        public int Road;

        /// <summary>Surface height (absolute game metres) as the road mesher draws it.</summary>
        public float Y;

        /// <summary>Unit surface normal.</summary>
        public float Nx, Ny, Nz;

        /// <summary>Raw along position on the piece (metres from its first rendered point) and the lateral offset from its
        /// carriageway centre (positive = left of the point order); 0 on caps and rings.</summary>
        public float AlongM, LateralM;

        /// <summary>Half the drawn carriageway width there (the ring width / 2 on a roundabout).</summary>
        public float HalfWidthM;

        /// <summary>Unit travel direction of the piece (point order) there; 0 on caps and rings.</summary>
        public float DirX, DirZ;

        /// <summary>On the carriageway (or a cap or ring carriageway).</summary>
        public bool OnCarriageway;

        /// <summary>On a raised footpath (kerb height above the carriageway edge), a median top or a road island.</summary>
        public bool OnFootpath, OnMedian, OnIsland;

        /// <summary>On an unpaved shoulder.</summary>
        public bool OnShoulder;

        /// <summary>On a junction cap or roundabout (ring, entries, island).</summary>
        public bool OnJunction;

        /// <summary>On a deck drawn at its absolute heights (bridge, flyover, synthetic bridge).</summary>
        public bool OnDeck;

        /// <summary>On a lowered underpass stretch (the terrain above it must be cut: <see cref="RoadSurfaceQuery.TryCut"/>).</summary>
        public bool Lowered;
    }

    /// <summary>
    /// The drawn road surface of one tile for one height sampler (W2 detail pass review): the height, normal and kind of
    /// surface the road mesher draws at any point — the ribbon carriageways with their smoothed centrelines, drawn widths,
    /// shifts and the smoothed vertical profile of <see cref="RoadGrade"/>, raised footpaths, medians and shoulders, junction
    /// caps, true roundabouts and their islands, decks at their absolute heights — so physics (RoadSpatialIndex,
    /// TileGroundQuery), traffic (LaneGraph), parked cars and props stand on exactly what is drawn instead of the terrain
    /// plus a lift over the raw polylines. Also the lowered underpass stretches the terrain mesher must cut
    /// (<see cref="TryCut"/>, <see cref="HasLowered"/>). Game metres in, absolute heights out. Built once per (tile,
    /// sampler) and cached on the sampler (<see cref="For"/>); immutable and thread-safe afterwards; queries do not
    /// allocate. Heights agree with the mesh to within the ribbon's row tolerance (<see cref="RibbonMesher.RowToleranceM"/>).
    /// </summary>
    public sealed class RoadSurfaceQuery
    {
        /// <summary>Grid cell size.</summary>
        public const double CellM = 16.0;

        /// <summary>A surface counts as the one a body at <c>nearY</c> stands on when it is at most this above it (a kerb or a
        /// ramp step); higher ones (a deck overhead) are passed under.</summary>
        public const float StepUpM = 1.0f;

        private sealed class Key
        {
            public TileData Tile;
            public RoadSurfaceQuery Query;
            public float LiftM;
        }

        private static readonly ConditionalWeakTable<IHeightSampler, Key> Cache = new ConditionalWeakTable<IHeightSampler, Key>();
        private static readonly object CacheLock = new object();

        public readonly TileData Tile;
        public readonly RoadLayout Layout;
        public readonly RoadGrade Grade;
        private readonly RoadOptions _o;
        private readonly double _x0, _z0, _size;
        private readonly int _cells;

        // Centreline segments (tile-local): A, B, raw along at A and B, reach (largest extent from the centreline), road.
        private readonly double[] _ax, _az, _bx, _bz, _s0, _s1, _reach;

        /// <summary>The ribbon's row directions (left normals of the curve) at each segment's two points: its quads fan
        /// between them on a bend (cut/fill package: on a steep curved street the perpendicular projection answered the
        /// surface up to 15 cm off what is drawn).</summary>
        private readonly double[] _wax, _waz, _wbx, _wbz;
        private readonly int[] _road;
        private readonly int[] _cellStart, _items;
        private readonly bool _anyLowered;

        /// <summary>The query of a tile on a sampler (cached on the sampler for the same tile and lift).</summary>
        public static RoadSurfaceQuery For(TileData t, IHeightSampler h, RoadOptions o = null)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (o == null) o = new RoadOptions();
            lock (CacheLock)
            {
                Key k;
                if (Cache.TryGetValue(h, out k) && ReferenceEquals(k.Tile, t) && k.LiftM == o.LiftM) return k.Query;
            }
            var q = new RoadSurfaceQuery(t, h, o);
            lock (CacheLock)
            {
                Cache.Remove(h);
                Cache.Add(h, new Key { Tile = t, Query = q, LiftM = o.LiftM });
            }
            return q;
        }

        private RoadSurfaceQuery(TileData t, IHeightSampler h, RoadOptions o)
        {
            Tile = t;
            _o = o;
            Layout = RoadLayout.For(t);
            Grade = RoadGrade.For(t, h, o);
            _x0 = t.Tile.X0;
            _z0 = t.Tile.Z0;
            _size = t.Tile.Size;
            _cells = Math.Max(1, (int)Math.Ceiling(_size / CellM));
            var ax = new List<double>();
            var az = new List<double>();
            var bx = new List<double>();
            var bz = new List<double>();
            var s0 = new List<double>();
            var s1 = new List<double>();
            var reach = new List<double>();
            var road = new List<int>();
            var wax = new List<double>();
            var waz = new List<double>();
            var wbx = new List<double>();
            var wbz = new List<double>();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (!RoadMesher.IsDrawn(t, i, o) || !Layout.DrawsRibbon(i) || !Grade.Has(i)) continue;
                RoadCentreline c = Layout.Centres[i];
                if (c == null || c.Count < 2) continue;
                if (Grade.IsLowered(i)) _anyLowered = true;
                RoadWidthProfile prof = Layout.Profiles[i];
                for (int k = 0; k + 1 < c.Count; k++)
                {
                    if (c.S[k + 1] - c.S[k] < 1e-9 && Math.Abs(c.X[k + 1] - c.X[k]) + Math.Abs(c.Z[k + 1] - c.Z[k]) < 1e-9) continue;
                    float la, ra, lb, rb;
                    Layout.CorridorSides(i, c.S[k], out la, out ra);
                    Layout.CorridorSides(i, c.S[k + 1], out lb, out rb);
                    float median = prof.Dual ? 0.5f * Math.Max(RoadWidthModel.MinMedianM, Layout.Attrs[i].MedianCm / 100f) : 0f;
                    double rch = Math.Max(Math.Max(la, ra), Math.Max(lb, rb)) + median + 0.5;
                    ax.Add(c.X[k]);
                    az.Add(c.Z[k]);
                    bx.Add(c.X[k + 1]);
                    bz.Add(c.Z[k + 1]);
                    s0.Add(c.S[k]);
                    s1.Add(c.S[k + 1]);
                    reach.Add(rch);
                    road.Add(i);
                    wax.Add(-c.Tz[k]);
                    waz.Add(c.Tx[k]);
                    wbx.Add(-c.Tz[k + 1]);
                    wbz.Add(c.Tx[k + 1]);
                }
            }
            _wax = wax.ToArray();
            _waz = waz.ToArray();
            _wbx = wbx.ToArray();
            _wbz = wbz.ToArray();
            _ax = ax.ToArray();
            _az = az.ToArray();
            _bx = bx.ToArray();
            _bz = bz.ToArray();
            _s0 = s0.ToArray();
            _s1 = s1.ToArray();
            _reach = reach.ToArray();
            _road = road.ToArray();
            int nc = _cells * _cells;
            var count = new int[nc];
            for (int pass = 0; pass < 2; pass++)
            {
                int[] fill = null;
                if (pass == 1)
                {
                    _cellStart = new int[nc + 1];
                    for (int k = 0; k < nc; k++) _cellStart[k + 1] = _cellStart[k] + count[k];
                    _items = new int[_cellStart[nc]];
                    fill = new int[nc];
                    Array.Copy(_cellStart, fill, nc);
                }
                for (int g = 0; g < _ax.Length; g++)
                {
                    double r = _reach[g];
                    int i0 = CellOf(Math.Min(_ax[g], _bx[g]) - r), i1 = CellOf(Math.Max(_ax[g], _bx[g]) + r);
                    int j0 = CellOf(Math.Min(_az[g], _bz[g]) - r), j1 = CellOf(Math.Max(_az[g], _bz[g]) + r);
                    for (int j = j0; j <= j1; j++)
                        for (int i = i0; i <= i1; i++)
                        {
                            int cell = j * _cells + i;
                            if (pass == 0) count[cell]++;
                            else _items[fill[cell]++] = g;
                        }
                }
            }
        }

        private int CellOf(double v)
        {
            int c = (int)Math.Floor(v / CellM);
            return c < 0 ? 0 : c >= _cells ? _cells - 1 : c;
        }

        /// <summary>True when some piece of the tile is lowered (an underpass made to clear 5.5 m).</summary>
        public bool HasLowered
        {
            get { return _anyLowered; }
        }

        /// <summary>The drawn road surface at game (x, z), whatever a body there stands on: the highest surface.</summary>
        public bool TrySample(double x, double z, out RoadSurfaceHit hit)
        {
            return TrySample(x, z, float.PositiveInfinity, out hit);
        }

        /// <summary>
        /// The drawn road surface at game (x, z) for a body near height <paramref name="nearY"/>: of every surface there
        /// (ribbons, caps, rings, decks), the highest one at most <see cref="StepUpM"/> above <paramref name="nearY"/>, else
        /// the lowest. False when no road surface lies under the point.
        /// </summary>
        public bool TrySample(double x, double z, float nearY, out RoadSurfaceHit hit)
        {
            hit = default(RoadSurfaceHit);
            double lx = x - _x0, lz = z - _z0;
            if (lx < -1 || lz < -1 || lx > _size + 1 || lz > _size + 1) return false;
            bool found = false;
            float limit = nearY + StepUpM;
            // Junction caps and roundabouts.
            foreach (JunctionCap cap in Layout.Caps)
            {
                if (!InsideOrOn(cap.PolyX, cap.PolyZ, cap.Count, lx, lz)) continue;
                float y = Grade.Terrain(lx, lz) + Grade.LiftOf(cap.TopRoad) + JunctionMesher.CapExtraLiftM;
                RoadSurfaceHit h = Junction(lx, lz, y, 0.5f * cap.WidestArmM);
                Pick(ref hit, ref found, h, limit);
            }
            foreach (RoadRing ring in Layout.Rings)
            {
                double dx = lx - ring.X, dz = lz - ring.Z, r = Math.Sqrt(dx * dx + dz * dz);
                if (r > ring.OuterRadiusM + ring.FlareRadiusM + 20.0) continue;
                float lift = Grade.LiftOf(ring.TopRoad) + JunctionMesher.CapExtraLiftM;
                if (r < ring.InnerRadiusM)
                {
                    RoadSurfaceHit h = Junction(lx, lz, Grade.Terrain(lx, lz) + lift + RoadLayout.IslandInteriorHeightM, 0.5f * ring.WidthM);
                    h.OnCarriageway = false;
                    h.OnIsland = true;
                    Pick(ref hit, ref found, h, limit);
                    continue;
                }
                bool on = r <= ring.OuterRadiusM;
                for (int a = 0; a < ring.Arms.Length && !on; a++) on = InPatch(ring, a, lx, lz);
                if (!on) continue;
                Pick(ref hit, ref found, Junction(lx, lz, Grade.Terrain(lx, lz) + lift, 0.5f * ring.WidthM), limit);
            }
            // Ribbons. A point near a bend lies over two segments of the same road (the inside of the bend, or past a
            // segment's end within the bend tolerance): it is on the one it projects onto inside its length, not on the
            // other's clamped end (on a steep street that answered the surface a metre up or down the hill; cut/fill
            // package, Gaurighat). A cell lists a road's segments one after another.
            {
                int cell = CellOf(lz) * _cells + CellOf(lx);
                int runRoad = -1;
                double runOver = double.PositiveInfinity;
                RoadSurfaceHit runHit = default(RoadSurfaceHit);
                for (int q = _cellStart[cell]; q < _cellStart[cell + 1]; q++)
                {
                    int g = _items[q];
                    if (_road[g] != runRoad)
                    {
                        if (runRoad >= 0 && !double.IsPositiveInfinity(runOver)) Pick(ref hit, ref found, runHit, limit);
                        runRoad = _road[g];
                        runOver = double.PositiveInfinity;
                    }
                    RoadSurfaceHit h;
                    double over;
                    if (!Ribbon(g, lx, lz, out h, out over)) continue;
                    if (!double.IsPositiveInfinity(runOver) && Math.Abs(h.Y - runHit.Y) > 1f)
                    {
                        // Another level of the same road (a switchback passing over or under itself): its own candidate.
                        Pick(ref hit, ref found, h, limit);
                        continue;
                    }
                    if (over < runOver - 1e-6 || over <= runOver + 1e-6 && h.Y > runHit.Y)
                    {
                        runOver = over;
                        runHit = h;
                    }
                }
                if (runRoad >= 0 && !double.IsPositiveInfinity(runOver)) Pick(ref hit, ref found, runHit, limit);
            }
            return found;
        }

        /// <summary>
        /// Where a lowered underpass stretch is drawn below the terrain (game (x, z) on its carriageway, footpaths or
        /// shoulders), the height the terrain must be cut down to there (<paramref name="maxTerrainY"/>: the surface less
        /// the piece's lift). For the terrain mesher (open issue, ref_roads.md §8); false elsewhere.
        /// </summary>
        public bool TryCut(double x, double z, out float maxTerrainY)
        {
            maxTerrainY = 0f;
            if (!_anyLowered) return false;
            RoadSurfaceHit h;
            if (!TrySample(x, z, float.NegativeInfinity, out h) || !h.Lowered) return false;
            maxTerrainY = h.Y - Grade.LiftOf(h.Road) - 0.05f;
            return true;
        }

        /// <summary>The lowered stretches of the tile: (road, start, end) raw along triples appended to
        /// <paramref name="into"/>; returns how many.</summary>
        public int LoweredSpans(List<double> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            int n = 0;
            for (int i = 0; i < Tile.Roads.Count; i++)
            {
                if (!Grade.IsLowered(i)) continue;
                double len = Layout.Profiles[i].LengthM, start = -1;
                for (double s = 0; s <= len + 1e-9; s += 1.0)
                {
                    bool low = Grade.LoweringAt(i, Math.Min(s, len)) > 0.01f;
                    if (low && start < 0) start = s;
                    if ((!low || s + 1.0 > len + 1e-9) && start >= 0)
                    {
                        into.Add(i);
                        into.Add(start);
                        into.Add(low ? len : s);
                        n++;
                        start = -1;
                    }
                }
            }
            return n;
        }

        private RoadSurfaceHit Junction(double lx, double lz, float y, float half)
        {
            var h = new RoadSurfaceHit { Road = -1, Y = y, OnJunction = true, OnCarriageway = true, HalfWidthM = half };
            Grade.TerrainNormal(lx, lz, out h.Nx, out h.Ny, out h.Nz);
            return h;
        }

        private static void Pick(ref RoadSurfaceHit best, ref bool found, in RoadSurfaceHit h, float limit)
        {
            if (!found)
            {
                best = h;
                found = true;
                return;
            }
            bool hOk = h.Y <= limit, bOk = best.Y <= limit;
            if (hOk && !bOk || hOk == bOk && (hOk ? h.Y > best.Y : h.Y < best.Y)) best = h;
        }

        /// <summary>
        /// Inverse of the fanned quad C(t) + off · W(t) with C(t) = t (ex, ez) and W(t) = (wax, waz) + t (dwx, dwz), for the
        /// point (qx, qz) relative to its start: the fraction <paramref name="t"/> along (outside [0, 1] beyond the ends) and
        /// the lateral offset <paramref name="lat"/> along the row direction there, in the ribbon's offset units (left
        /// positive; the ribbon places a vertex at offset d of a row at C + d · W).
        /// </summary>
        private static void FanParam(double qx, double qz, double ex, double ez, double wax, double waz, double dwx, double dwz, out double t, out double lat)
        {
            double l2 = ex * ex + ez * ez;
            double lin = l2 > 1e-12 ? (qx * ex + qz * ez) / l2 : 0;
            // (Q - t E) x (Wa + t dW) = 0.
            double c2 = -(ex * dwz - ez * dwx);
            double c1 = qx * dwz - qz * dwx - (ex * waz - ez * wax);
            double c0 = qx * waz - qz * wax;
            if (Math.Abs(c2) <= 1e-9 * (Math.Abs(c1) + 1e-12)) t = Math.Abs(c1) > 1e-12 ? -c0 / c1 : lin;
            else
            {
                double disc = c1 * c1 - 4 * c2 * c0;
                if (disc < 0) t = lin;
                else
                {
                    double sq = Math.Sqrt(disc), q = -0.5 * (c1 + (c1 >= 0 ? sq : -sq));
                    double t1 = q / c2, t2 = Math.Abs(q) > 1e-18 ? c0 / q : t1;
                    double d1 = t1 < 0 ? -t1 : t1 > 1 ? t1 - 1 : 0, d2 = t2 < 0 ? -t2 : t2 > 1 ? t2 - 1 : 0;
                    t = d1 < d2 || d1 == d2 && Math.Abs(t1 - 0.5) <= Math.Abs(t2 - 0.5) ? t1 : t2;
                }
            }
            double wx = wax + dwx * t, wz = waz + dwz * t, w2 = wx * wx + wz * wz;
            double rx = qx - ex * t, rz = qz - ez * t;
            lat = w2 > 1e-12 ? (rx * wx + rz * wz) / w2 : 0;
        }

        /// <summary>The ribbon of segment <paramref name="g"/> at tile-local (lx, lz), as RibbonMesher and RoadSweep draw it.</summary>
        private bool Ribbon(int g, double lx, double lz, out RoadSurfaceHit hit, out double overshoot)
        {
            hit = default(RoadSurfaceHit);
            overshoot = 0;
            double ax = _ax[g], az = _az[g], ex = _bx[g] - ax, ez = _bz[g] - az;
            double l2 = ex * ex + ez * ez;
            double l = Math.Sqrt(l2);
            double dirx = l > 1e-12 ? ex / l : 1, dirz = l > 1e-12 ? ez / l : 0;
            // Where the point lies in the ribbon's quad between the segment's two rows: the centre runs straight between
            // the points and the row direction blends from one point's to the other's, so on a bend the quads fan.
            double t, lat;
            FanParam(lx - ax, lz - az, ex, ez, _wax[g], _waz[g], _wbx[g] - _wax[g], _wbz[g] - _waz[g], out t, out lat);
            if (Math.Abs(lat) > _reach[g]) return false;
            // A little past the segment's ends (the next quad answers there; a gap between the two is a float error).
            double u = t * l, over = 0.02;
            if (u < -over || u > l + over) return false;
            overshoot = u < 0 ? -u : u > l ? u - l : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            int road = _road[g];
            double s = _s0[g] + (_s1[g] - _s0[g]) * t;
            if (Layout.InGap(road, s) || Layout.InAbsorbed(road, s)) return false;
            RoadWidthProfile prof = Layout.Profiles[road];
            float w = prof.DrawnAt(s), shift = prof.ShiftAt(s), half = 0.5f * w;
            double rel = lat - shift;
            hit.Road = road;
            hit.AlongM = (float)s;
            hit.LateralM = (float)rel;
            hit.HalfWidthM = half;
            hit.DirX = (float)dirx;
            hit.DirZ = (float)dirz;
            hit.OnDeck = Grade.IsAbsolute(road) || Grade.OnDeck(road, s);
            hit.Lowered = Grade.LoweringAt(road, s) > 0.01f;
            if (Math.Abs(rel) <= half + EdgeToleranceM)
            {
                hit.OnCarriageway = true;
                double clamped = shift + Math.Max(-half, Math.Min(half, rel));
                hit.Y = Grade.SurfaceY(road, s, clamped, lx, lz);
                Grade.SurfaceNormal(road, s, lx, lz, out hit.Nx, out hit.Ny, out hit.Nz);
                return true;
            }
            // Beyond an edge: footpath, median or shoulder as the ribbon's side treatment draws them.
            bool left = rel > 0;
            double edgeOff = shift + (left ? half : -half), d = Math.Abs(rel) - half;
            double ux = -dirz, uz = dirx; // left normal
            double ex0 = lx - ux * (lat - edgeOff), ez0 = lz - uz * (lat - edgeOff);
            float edgeY = Grade.SurfaceY(road, s, edgeOff, ex0, ez0);
            if (prof.Dual && !left)
            {
                float mh = 0.5f * Math.Max(RoadWidthModel.MinMedianM, Layout.Attrs[road].MedianCm / 100f);
                if (d > mh) return false;
                hit.OnMedian = true;
                // The mountable kerb rises over its first 0.2 m, then the median top (RoadSweep.Side.Median).
                hit.Y = edgeY + _o.MedianHeightM * (float)Math.Min(1.0, d / 0.2) + (d > 0.2 ? 0.01f : 0f);
                hit.Ny = 1f;
                return true;
            }
            float foot = prof.Sample(left ? prof.FootLeft : prof.FootRight, s);
            if (foot > 0f && !Grade.IsSyntheticDeck(road))
            {
                foot = Math.Max(foot, RoadWidthModel.MinFootpathM);
                if (d > foot) return false;
                hit.OnFootpath = true;
                // As RoadSweep.Side.Footpath: the kerb top at kerb height over the edge, then the pavers rising 2.5 %
                // outward plus the terrain's own rise from the kerb to the outer edge, linear across the footpath.
                const double KerbTop = RoadWidthModel.KerbTopM;
                double sx = left ? ux : -ux, sz = left ? uz : -uz;
                double fw = Math.Max(KerbTop + 0.05, foot);
                double kx = ex0 + sx * KerbTop, kz = ez0 + sz * KerbTop, ox = ex0 + sx * fw, oz = ez0 + sz * fw;
                float outer = (float)(0.025 * (fw - KerbTop)) + (hit.OnDeck ? 0f : Grade.Terrain(ox, oz) - Grade.Terrain(kx, kz));
                float f = (float)Math.Max(0.0, Math.Min(1.0, (d - KerbTop) / (fw - KerbTop)));
                hit.Y = edgeY + _o.KerbHeightM + outer * f;
                hit.Ny = 1f;
                return true;
            }
            float sh = Layout.ShoulderAt(road, s);
            if (sh > 0f && !hit.OnDeck && d <= sh)
            {
                hit.OnShoulder = true;
                float rise = Grade.Terrain(lx, lz) - Grade.Terrain(ex0, ez0);
                hit.Y = edgeY - 0.02f - 0.02f * (float)d + rise;
                Grade.TerrainNormal(lx, lz, out hit.Nx, out hit.Ny, out hit.Nz);
                return true;
            }
            return false;
        }

        private static bool InPatch(RoadRing ring, int a, double x, double z)
        {
            RingArm arm = ring.Arms[a];
            // The patch polygon: cut edge right, left, left flare, circle (inside the circle is handled by the caller), right
            // flare. Without the circle points the polygon is still the patch minus a sliver inside the circle.
            int n = 2 + arm.LeftX.Length + arm.RightX.Length;
            bool inside = false;
            double px = 0, pz = 0;
            for (int i = 0; i <= n; i++)
            {
                double cx, cz;
                PatchPoint(arm, i % n, out cx, out cz);
                if (i > 0)
                {
                    if ((pz > z) != (cz > z) && x < (cx - px) * (z - pz) / (cz - pz) + px) inside = !inside;
                }
                px = cx;
                pz = cz;
            }
            return inside;
        }

        private static void PatchPoint(in RingArm arm, int i, out double x, out double z)
        {
            if (i == 0)
            {
                x = arm.RightEdgeX;
                z = arm.RightEdgeZ;
                return;
            }
            if (i == 1)
            {
                x = arm.LeftEdgeX;
                z = arm.LeftEdgeZ;
                return;
            }
            int k = i - 2;
            if (k < arm.LeftX.Length)
            {
                x = arm.LeftX[k];
                z = arm.LeftZ[k];
                return;
            }
            k -= arm.LeftX.Length;
            x = arm.RightX[k];
            z = arm.RightZ[k];
        }

        /// <summary>Inside the polygon or within <see cref="EdgeToleranceM"/> of its outline (the cap's own outline
        /// vertices and the kerb rows standing on it).</summary>
        private static bool InsideOrOn(double[] px, double[] pz, int n, double x, double z)
        {
            if (Inside(px, pz, n, x, z)) return true;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double t;
                if (RoadCorridorIndex.SegDistance(x, z, px[j], pz[j], px[i], pz[i], out t) <= EdgeToleranceM) return true;
            }
            return false;
        }

        /// <summary>Points this close outside a carriageway edge or a cap outline still count as on it (the drawn edge
        /// vertices themselves, float rounding).</summary>
        public const double EdgeToleranceM = 0.03;

        private static bool Inside(double[] px, double[] pz, int n, double x, double z)
        {
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
                if ((pz[i] > z) != (pz[j] > z) && x < (px[j] - px[i]) * (z - pz[i]) / (pz[j] - pz[i]) + px[i]) inside = !inside;
            return inside;
        }
    }
}
