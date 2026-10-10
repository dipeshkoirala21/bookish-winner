using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The detail pass's road checks (docs/W2_DETAIL_CONTRACT.md §1, §3; owner: very rideable, smooth, wide roads): no
    /// corner sharper than its class radius allows, the rideable width everywhere on every leaf tile, continuous seams at
    /// tile borders, triangle budgets, no NaN, the material-channel contract, the corridor index, structures from
    /// synthetic RSTR records, the smoothed vertical profile and the final-corridor semantics.
    /// </summary>
    public class MeshingRoadDetailTests
    {
        private static readonly TileId A = new TileId(10, 516, 161);

        /// <summary>How far the drawn ribbon may stray from its analytic edges in plan (the row decimation's tolerance).</summary>
        private const double RibbonPlanToleranceM = 0.05;

        /// <summary>How far the drawn ribbon may stray from the profile in height (the row decimation's tolerance).</summary>
        private const float RibbonMesher_RowToleranceM = 0.05f;

        private static double Smooth(double x, double z)
        {
            return 1300 + 6 * Math.Sin(x / 170.0) + 4 * Math.Cos(z / 130.0) + 0.01 * (x - 528000);
        }

        private static RoadRecord Road(RoadClass c, RoadFlags flags, params int[] pointsCm)
        {
            return new RoadRecord { OsmWayId = 4242, RoadClass = c, Surface = Surface.Asphalt, Flags = flags, Points = pointsCm };
        }

        private static IEnumerable<TileData> LeafTiles()
        {
            foreach (TileId id in StreamingSampleRegion.TilesAt(10).OrderBy(i => i.Key))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t != null && t.Roads.Count > 0) yield return t;
            }
        }

        /// <summary>Raw along <paramref name="s"/> of road <paramref name="road"/> lies in or at a junction gap (the cap draws
        /// the turn there; the ribbon stops at the cut).</summary>
        private static bool NearGap(RoadLayout layout, int road, double s)
        {
            RoadCut[] cuts = layout.Cuts[road];
            if (cuts == null) return false;
            for (int g = 0; g + 1 < cuts.Length; g += 2)
                if (s >= cuts[g].S - 0.05 && s <= cuts[g + 1].S + 0.05) return true;
            return false;
        }

        // -------------------------------------------------------------------------------------------------------
        // Plan geometry
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Every corner of every drawn piece of the sample is filleted with the class / design-speed radius unless the
        /// geometry forbids it (neighbouring corners, the deviation limit that keeps corner houses, a tile-border cut, a
        /// junction cap that draws the turn), and then with the largest radius the geometry allows; the drawn curve never
        /// turns more than one arc step between two points outside junction caps.
        /// </summary>
        [Test]
        public void NoCornerIsSharperThanItsClassRadiusAllows()
        {
            var o = new RoadOptions();
            int corners = 0, atDesign = 0, kinks = 0, points = 0, tightArcs = 0, tight = 0, atCaps = 0, atCuts = 0;
            var byLimit = new int[5];
            foreach (TileData t in LeafTiles())
            {
                RoadLayout layout = RoadLayout.For(t);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    if (!RoadMesher.IsDrawn(t, i, o)) continue;
                    RoadCentreline c = layout.Centres[i];
                    if (c == null) continue;
                    RoadClass cls = t.Roads[i].RoadClass;
                    AreaType area = RoadWidthModel.AreaOf(layout.Attrs[i]);
                    double minDeflection = RoadCentreline.MinDeflectionDegFor(cls);
                    double classRadius = RoadCentreline.DesignRadiusM(cls, area, 0);
                    bool mappedCurve = layout.Attrs[i].Has(RoadAttrFlags.RingMember) || layout.Structures[i].IsElevated;
                    foreach (RoadCorner k in c.Corners)
                    {
                        if (k.DeflectionDeg < minDeflection || k.Limit == CornerLimit.Junction) continue;
                        if (mappedCurve)
                        {
                            // Ring ways and decks keep their mapped plan (the ring is drawn as a circle, decks follow DeckY).
                            Assert.That(k.RadiusM, Is.EqualTo(0f));
                            continue;
                        }
                        corners++;
                        string what = t.Tile + " road " + i + " (" + cls + ") corner at " + k.S.ToString("0.0");
                        Assert.That(k.WantedRadiusM, Is.GreaterThanOrEqualTo(classRadius - 1e-3), what + ": wanted under the class radius");
                        Assert.That(k.RadiusM, Is.LessThanOrEqualTo(k.WantedRadiusM + 1e-3), what);
                        if (k.TangentM > 0f)
                            Assert.That(k.RadiusM, Is.GreaterThanOrEqualTo(Math.Min(k.WantedRadiusM, k.AllowedRadiusM) * 0.999 - 1e-3), what + ": sharper than allowed");
                        if (k.Limit == CornerLimit.None)
                        {
                            Assert.That(k.RadiusM, Is.EqualTo(k.WantedRadiusM).Within(1e-3), what + ": unlimited corner not at the class radius");
                            atDesign++;
                        }
                        byLimit[(int)k.Limit]++;
                        if (k.RadiusM > 0f && k.RadiusM < 0.5f * k.WantedRadiusM) tight++;
                    }
                    // Raw along values never run backwards (all along-based data maps onto the curve).
                    for (int p = 1; p < c.Count; p++)
                        if (c.S[p] < c.S[p - 1] - 1e-9) Assert.Fail(t.Tile + " road " + i + ": raw along runs backwards at point " + p);
                    // The drawn curve: no kink outside junction caps and gaps.
                    for (int p = 1; p + 1 < c.Count; p++)
                    {
                        double ax = c.X[p] - c.X[p - 1], az = c.Z[p] - c.Z[p - 1], bx = c.X[p + 1] - c.X[p], bz = c.Z[p + 1] - c.Z[p];
                        double la = Math.Sqrt(ax * ax + az * az), lb = Math.Sqrt(bx * bx + bz * bz);
                        if (la < 1e-6 || lb < 1e-6) continue;
                        points++;
                        double turn = Math.Abs(Math.Atan2(ax * bz - az * bx, ax * bx + az * bz)) * 180 / Math.PI;
                        if (turn <= RoadCentreline.MaxArcStepDeg + 0.5 || mappedCurve || NearGap(layout, i, c.S[p])) continue;
                        // A tight arc is stepped by its shortest chord.
                        float kmax = Math.Max(Math.Abs(c.Curv[p - 1]), Math.Max(Math.Abs(c.Curv[p]), Math.Abs(c.Curv[p + 1])));
                        double r = kmax > 0f ? 1.0 / kmax : 0;
                        if (r > 0 && turn <= 2 * Math.Asin(Math.Min(1.0, 0.5 * Math.Max(la, lb) / r)) * 180 / Math.PI + 0.5) { tightArcs++; continue; }
                        // A corner left sharp next to a junction cap (the cap draws the turn; the ribbon ends within a
                        // gap margin of it).
                        CornerLimit lim = CornerLimit.None;
                        foreach (RoadCorner k in c.Corners)
                            if (Math.Abs(k.S - c.S[p]) < 0.05) lim = k.Limit;
                        if (lim == CornerLimit.Junction)
                        {
                            atCaps++;
                            continue;
                        }
                        if (lim == CornerLimit.TileCut)
                        {
                            // A mapped vertex within the cut margin of a tile border: the curve is straight through the cut
                            // (both tiles agree), and the neighbour tile does not know the corner (only one context point).
                            Assert.That(Math.Min(c.S[p], c.RawLengthM - c.S[p]), Is.LessThan(RoadCentreline.CutMarginM + 1.0),
                                        t.Tile + " road " + i + ": a tile-cut corner away from the cut");
                            atCuts++;
                            continue;
                        }
                        kinks++;
                        if (kinks <= 20) TestContext.Progress.WriteLine("kink " + turn.ToString("0.0") + " deg (" + lim + "): " + t.Tile + " road " + i + " (" + cls + ") at " + c.S[p].ToString("0.0"));
                    }
                }
            }
            TestContext.Progress.WriteLine("corners " + corners + ", at the class radius " + atDesign + ", limited by segment " + byLimit[1] + ", deviation " +
                                           byLimit[2] + ", tile cut " + byLimit[3] + ", under half the class radius " + tight + "; curve points " + points + ", tight arcs " + tightArcs + ", sharp at caps " + atCaps + ", at tile cuts " + atCuts + ", kinks " + kinks);
            Assert.That(corners, Is.GreaterThan(5000));
            Assert.That(atDesign, Is.GreaterThan(corners * 3 / 4), "most corners get their class radius");
            Assert.That(kinks, Is.EqualTo(0), "kinks on drawn curves");
            Assert.That(atCuts, Is.LessThan(points / 5000), "corners left sharp at tile cuts");
        }

        // -------------------------------------------------------------------------------------------------------
        // Widths, coverage and the corridor index on the sample
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Up-facing triangles of a mesh bucketed by plan cell (tile-local metres).</summary>
        private sealed class UpGrid
        {
            private const double Cell = 4.0;
            private readonly MeshData _m;
            private readonly int _n;
            private readonly double _origin;
            private readonly List<int>[] _cells;

            public UpGrid(MeshData m, double size)
            {
                _m = m;
                _origin = -32;
                _n = (int)Math.Ceiling((size + 64) / Cell);
                _cells = new List<int>[_n * _n];
                for (int tri = 0; tri < m.TriangleCount; tri++)
                {
                    double fx, fy, fz;
                    MeshingChecks.Facet(m, tri, out fx, out fy, out fz);
                    double len = Math.Sqrt(fx * fx + fy * fy + fz * fz);
                    if (len < 1e-9 || fy < 0.5 * len) continue;
                    double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                    for (int k = 0; k < 3; k++)
                    {
                        int v = m.Indices[3 * tri + k];
                        x0 = Math.Min(x0, m.Positions[3 * v]);
                        x1 = Math.Max(x1, m.Positions[3 * v]);
                        z0 = Math.Min(z0, m.Positions[3 * v + 2]);
                        z1 = Math.Max(z1, m.Positions[3 * v + 2]);
                    }
                    for (int j = Index(z0); j <= Index(z1); j++)
                    {
                        for (int i = Index(x0); i <= Index(x1); i++)
                        {
                            int c = j * _n + i;
                            (_cells[c] ?? (_cells[c] = new List<int>())).Add(tri);
                        }
                    }
                }
            }

            private int Index(double v)
            {
                int i = (int)Math.Floor((v - _origin) / Cell);
                return i < 0 ? 0 : i >= _n ? _n - 1 : i;
            }

            public bool Covers(double x, double z)
            {
                List<int> list = _cells[Index(z) * _n + Index(x)];
                if (list == null) return false;
                foreach (int tri in list)
                {
                    double h;
                    if (MeshingChecks.TriangleHeight(_m, tri, x, z, out h)) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Owner: three motorbikes side by side on every street. On every leaf tile of the sample every drawn street is at
        /// least <see cref="RoadClearance.MinCorridorM"/> wide (footways and paths
        /// <see cref="RoadWidthModel.FootRideableM"/>) along its whole length, never narrower than the W2_DESIGN 4.2 width,
        /// its clear corridor is at least 4.8 m, and the drawn road surface really covers that width (points 0.1 m inside
        /// the floor width on both sides lie on an up-facing triangle of the tile's road mesh; a tenth of a percent do not:
        /// the inner edge of an arc too tight for the width is pinched so the ribbon never folds, and mapped corners left
        /// sharp at a junction cap leave slivers beside the cap's kerb return).
        /// </summary>
        [Test]
        public void EveryDrawnRoadIsAtLeastRideableOnEveryLeafTile()
        {
            var o = new RoadOptions();
            long samples = 0, uncovered = 0, atCaps = 0, atTight = 0;
            int tiles = 0, pieces = 0;
            foreach (TileData t in LeafTiles())
            {
                tiles++;
                RoadLayout layout = RoadLayout.For(t);
                var m = new MeshData();
                RoadMesher.Build(t, new TileHeightSampler(t, 2), o, m);
                var grid = new UpGrid(m, t.Tile.Size);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    if (!RoadMesher.IsDrawn(t, i, o)) continue;
                    pieces++;
                    RoadClass cls = t.Roads[i].RoadClass;
                    float floor = RoadWidthModel.RideableMinM(cls);
                    RoadWidthProfile prof = layout.Profiles[i];
                    for (int k = 0; k < prof.Count; k++)
                    {
                        if (prof.Drawn[k] < floor - 1e-4f) Assert.Fail(t.Tile + " road " + i + " (" + cls + "): drawn " + prof.Drawn[k] + " m under the floor");
                        if (prof.Drawn[k] < prof.Width[k] - 1e-4f) Assert.Fail(t.Tile + " road " + i + ": drawn narrower than the 4.2 width");
                    }
                    RoadCentreline c = layout.Centres[i];
                    if (c == null) continue;
                    for (double s = 0.25; s <= c.RawLengthM - 0.25; s += 2)
                    {
                        if (layout.InGap(i, s)) continue;
                        Assert.That(layout.CorridorHalfM(i, s), Is.GreaterThanOrEqualTo(0.5f * RoadClearance.MinCorridorM - 1e-4f));
                        RoadCut cut = layout.DrawnCutAt(t, i, s);
                        Assert.That(2 * cut.Half, Is.GreaterThanOrEqualTo(floor - 1e-3), t.Tile + " road " + i);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            double e = cut.Shift + side * (0.5 * floor - 0.1);
                            double x = cut.CX + cut.UX * e, z = cut.CZ + cut.UZ * e;
                            if (x < 0.05 || z < 0.05 || x > t.Tile.Size - 0.05 || z > t.Tile.Size - 0.05) continue;
                            samples++;
                            if (grid.Covers(x, z)) continue;
                            // Where: next to a junction cap's cut (the cap's kerb return), on the inner side of an arc too
                            // tight for the width (the ribbon pinches its inner edge there so it never folds), or elsewhere.
                            if (NearGap(layout, i, s - 1.5) || NearGap(layout, i, s + 1.5)) { atCaps++; continue; }
                            float kmax = 0f;
                            for (int p = 0; p < c.Count; p++)
                                if (Math.Abs(c.S[p] - s) <= 3.0) kmax = Math.Max(kmax, Math.Abs(c.Curv[p]));
                            if (kmax * (0.5 * floor) > 0.9) { atTight++; continue; }
                            uncovered++;
                            if (uncovered <= 10) TestContext.Progress.WriteLine("uncovered: " + t.Tile + " road " + i + " (" + cls + ") at " + s.ToString("0.0") + " side " + side);
                        }
                    }
                }
            }
            TestContext.Progress.WriteLine("rideable: " + tiles + " tiles, " + pieces + " pieces, " + samples + " width samples; uncovered next to caps " + atCaps +
                                           ", inside tight arcs " + atTight + ", elsewhere " + uncovered);
            Assert.That(tiles, Is.GreaterThan(30));
            Assert.That(samples, Is.GreaterThan(200000));
            Assert.That(uncovered + atCaps + atTight, Is.LessThan(samples / 1000), "floor-width points without a road surface");
            Assert.That(uncovered, Is.LessThan(samples / 2000), "floor-width points without a road surface away from caps and tight arcs");
        }

        /// <summary>
        /// The corridor index (RoadCorridorIndex, IRoadCorridorQuery) on every leaf tile: the drawn carriageway and
        /// footpath edges (to the ribbon's 5 cm plan tolerance) and every cap and ring outline lie inside it, the 4.8 m
        /// floor lies inside it, and its signed distance and nearest road equal a brute-force evaluation over all corridor
        /// primitives at random points.
        /// </summary>
        [Test]
        public void CorridorIndexHoldsEveryDrawnSurface()
        {
            var o = new RoadOptions();
            var rng = new Random(5);
            int tiles = 0, edges = 0, queries = 0;
            double worstEdge = double.NegativeInfinity;
            foreach (TileData t in LeafTiles())
            {
                tiles++;
                RoadLayout layout = RoadLayout.For(t);
                RoadCorridorIndex idx = RoadCorridorIndex.ForTile(t);
                IRoadCorridorQuery q = idx;
                double x0 = t.Tile.X0, z0 = t.Tile.Z0;
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadCentreline c = layout.Centres[i];
                    if (!RoadMesher.IsDrawn(t, i, o) || c == null) continue;
                    // The ribbon's rows stand at centreline points and its edges run straight between them: check the
                    // edge points of every possible row and the midpoints of the edges between neighbouring rows.
                    for (int p = 0; p + 1 < c.Count; p++)
                    {
                        double sa = c.S[p], sb = c.S[p + 1];
                        if (layout.InGap(i, 0.5 * (sa + sb))) continue;
                        RoadCut ca = layout.DrawnCutAt(t, i, sa), cb = layout.DrawnCutAt(t, i, sb);
                        RoadProfile pa = layout.ProfileAt(i, sa), pb = layout.ProfileAt(i, sb);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            double fa = side > 0 ? pa.FootpathLeftM : pa.FootpathRightM, fb = side > 0 ? pb.FootpathLeftM : pb.FootpathRightM;
                            if (fa > 0) fa = Math.Max(fa, RoadWidthModel.MinFootpathM);
                            if (fb > 0) fb = Math.Max(fb, RoadWidthModel.MinFootpathM);
                            double[] ea = { ca.Shift + side * ca.Half, ca.Shift + side * (ca.Half + fa), side * 0.5 * (RoadClearance.MinCorridorM - 0.1) };
                            double[] eb = { cb.Shift + side * cb.Half, cb.Shift + side * (cb.Half + fb), side * 0.5 * (RoadClearance.MinCorridorM - 0.1) };
                            for (int qi = 0; qi < 3; qi++)
                            {
                                double xa = ca.CX + ca.UX * ea[qi], za = ca.CZ + ca.UZ * ea[qi], xb = cb.CX + cb.UX * eb[qi], zb = cb.CZ + cb.UZ * eb[qi];
                                foreach (var (px, pz) in new[] { (xa, za), (0.5 * (xa + xb), 0.5 * (za + zb)) })
                                {
                                    double d = q.SignedDistance(x0 + px, z0 + pz);
                                    worstEdge = Math.Max(worstEdge, d);
                                    edges++;
                                    if (d > RibbonPlanToleranceM) Assert.Fail(t.Tile + " road " + i + " at " + sa.ToString("0.0") + ": drawn edge " + d.ToString("0.000") + " m outside the corridor");
                                }
                            }
                        }
                    }
                }
                foreach (JunctionCap cap in layout.Caps)
                    for (int k = 0; k < cap.Count; k++)
                        Assert.That(q.SignedDistance(x0 + cap.PolyX[k], z0 + cap.PolyZ[k]), Is.LessThanOrEqualTo(1e-6), t.Tile + " cap vertex");
                foreach (RoadRing ring in layout.Rings)
                {
                    Assert.That(q.SignedDistance(x0 + ring.X, z0 + ring.Z), Is.LessThan(-ring.InnerRadiusM + 1e-3), t.Tile + ": the island is in the corridor");
                    for (int a = 0; a < 360; a += 10)
                    {
                        double ang = a * Math.PI / 180, rr = ring.OuterRadiusM - 0.05;
                        Assert.That(q.SignedDistance(x0 + ring.X + rr * Math.Cos(ang), z0 + ring.Z + rr * Math.Sin(ang)), Is.LessThan(0), t.Tile + " ring");
                    }
                }
                // Brute force at random points (half of them near a road).
                List<double[]> polys = Polygons(layout), segs = Segments(t, layout, o);
                Assert.That(segs.Count, Is.EqualTo(idx.SegmentCount), t.Tile + " segments");
                Assert.That(polys.Count, Is.EqualTo(idx.PolygonCount), t.Tile + " polygons");
                for (int k = 0; k < 60; k++)
                {
                    double lx = -20 + rng.NextDouble() * (t.Tile.Size + 40), lz = -20 + rng.NextDouble() * (t.Tile.Size + 40);
                    if (k % 2 == 1 && segs.Count > 0)
                    {
                        double[] g = segs[rng.Next(segs.Count)];
                        lx = g[0] + (rng.NextDouble() - 0.5) * 30;
                        lz = g[1] + (rng.NextDouble() - 0.5) * 30;
                    }
                    double brute = Brute(segs, polys, lx, lz);
                    double got = q.SignedDistance(x0 + lx, z0 + lz);
                    if (brute >= RoadCorridorIndex.MaxSearchM) Assert.That(got, Is.EqualTo(RoadCorridorIndex.MaxSearchM).Within(1e-9));
                    else Assert.That(got, Is.EqualTo(brute).Within(1e-6), t.Tile + " query " + k);
                    double sd;
                    int road = idx.NearestRoad(x0 + lx, z0 + lz, out sd);
                    if (brute < RoadCorridorIndex.MaxSearchM) Assert.That(sd, Is.EqualTo(brute).Within(1e-6));
                    else Assert.That(road, Is.EqualTo(-1));
                    queries++;
                }
            }
            TestContext.Progress.WriteLine("corridor: " + tiles + " tiles, " + edges + " edge points (worst " + worstEdge.ToString("0.000") + " m), " + queries + " brute-force queries");
            Assert.That(edges, Is.GreaterThan(100000));
        }

        private static List<double[]> Polygons(RoadLayout layout)
        {
            var list = new List<double[]>();
            foreach (JunctionCap cap in layout.Caps)
            {
                var v = new double[2 * cap.Count];
                for (int k = 0; k < cap.Count; k++)
                {
                    v[2 * k] = cap.PolyX[k];
                    v[2 * k + 1] = cap.PolyZ[k];
                }
                list.Add(v);
            }
            var xs = new List<double>();
            var zs = new List<double>();
            foreach (RoadRing ring in layout.Rings)
            {
                int n = ring.CircleSegments;
                var v = new double[2 * n];
                for (int k = 0; k < n; k++)
                {
                    double a = 2 * Math.PI * k / n;
                    v[2 * k] = ring.X + ring.OuterRadiusM * Math.Cos(a);
                    v[2 * k + 1] = ring.Z + ring.OuterRadiusM * Math.Sin(a);
                }
                list.Add(v);
                for (int a = 0; a < ring.Arms.Length; a++)
                {
                    xs.Clear();
                    zs.Clear();
                    ring.Patch(a, xs, zs);
                    var pv = new double[2 * xs.Count];
                    for (int k = 0; k < xs.Count; k++)
                    {
                        pv[2 * k] = xs[k];
                        pv[2 * k + 1] = zs[k];
                    }
                    list.Add(pv);
                }
            }
            return list;
        }

        private static double Seg(double px, double pz, double ax, double az, double bx, double bz, out double t)
        {
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            t = l2 > 1e-12 ? Math.Max(0, Math.Min(1, ((px - ax) * dx + (pz - az) * dz) / l2)) : 0;
            double qx = ax + dx * t - px, qz = az + dz * t - pz;
            return Math.Sqrt(qx * qx + qz * qz);
        }

        /// <summary>The corridor's segment primitives, built independently of the index: every drawn piece's smoothed
        /// centreline split at its width profile's samples and at the shoulders' border-blend boundaries next to tile cuts
        /// (where the half width is the larger side's), half widths <see cref="RoadLayout.CorridorHalfM"/> at both ends
        /// (ax, az, bx, bz, ha, hb per entry).</summary>
        private static List<double[]> Segments(TileData t, RoadLayout layout, RoadOptions o)
        {
            var list = new List<double[]>();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (!RoadMesher.IsDrawn(t, i, o)) continue;
                RoadCentreline c = layout.Centres[i];
                if (c == null || c.Count < 2) continue;
                RoadRecord r = t.Roads[i];
                double step = layout.Profiles[i].StepM, len = c.S[c.Count - 1];
                var knots = new List<double>();
                for (int j = 1; j * step < len; j++) knots.Add(j * step);
                if (r.HasPrevContext && RoadLayout.BorderBlendM < len) knots.Add(RoadLayout.BorderBlendM);
                double endBlend = layout.Profiles[i].LengthM - RoadLayout.BorderBlendM;
                if (r.HasNextContext && endBlend > 0) knots.Add(endBlend);
                knots.Sort();
                var pts = new List<double[]>();
                int kn = 0;
                for (int k = 0; k + 1 < c.Count; k++)
                {
                    float h0 = layout.CorridorHalfM(i, c.S[k]), h1 = layout.CorridorHalfM(i, c.S[k + 1]);
                    pts.Add(new[] { c.X[k], c.Z[k], h0 });
                    while (kn < knots.Count && knots[kn] <= c.S[k] + 1e-6) kn++;
                    for (; kn < knots.Count && knots[kn] < c.S[k + 1] - 1e-6; kn++)
                    {
                        double sj = knots[kn], f = (sj - c.S[k]) / (c.S[k + 1] - c.S[k]);
                        float chord = h0 + (h1 - h0) * (float)f;
                        pts.Add(new[] { c.X[k] + (c.X[k + 1] - c.X[k]) * f, c.Z[k] + (c.Z[k + 1] - c.Z[k]) * f,
                                        Math.Max(chord, Math.Max(layout.CorridorHalfM(i, sj - 1e-3), layout.CorridorHalfM(i, sj + 1e-3))) });
                    }
                }
                pts.Add(new[] { c.X[c.Count - 1], c.Z[c.Count - 1], layout.CorridorHalfM(i, c.S[c.Count - 1]) });
                for (int k = 0; k + 1 < pts.Count; k++) list.Add(new[] { pts[k][0], pts[k][1], pts[k + 1][0], pts[k + 1][1], pts[k][2], pts[k + 1][2] });
            }
            return list;
        }

        /// <summary>Brute-force corridor signed distance (tile-local) over every segment and polygon primitive.</summary>
        private static double Brute(List<double[]> segs, List<double[]> polys, double x, double z)
        {
            double best = double.PositiveInfinity;
            foreach (double[] g in segs)
            {
                double u;
                double d = Seg(x, z, g[0], g[1], g[2], g[3], out u);
                best = Math.Min(best, d - (g[4] + (g[5] - g[4]) * u));
            }
            foreach (double[] v in polys)
            {
                int n = v.Length / 2;
                bool inside = false;
                double edge = double.PositiveInfinity;
                for (int a = 0, b = n - 1; a < n; b = a++)
                {
                    double ax = v[2 * b], az = v[2 * b + 1], bx = v[2 * a], bz = v[2 * a + 1];
                    if ((az > z) != (bz > z) && x < (bx - ax) * (z - az) / (bz - az) + ax) inside = !inside;
                    double u;
                    edge = Math.Min(edge, Seg(x, z, ax, az, bx, bz, out u));
                }
                best = Math.Min(best, inside ? -edge : edge);
            }
            return best;
        }

        /// <summary>The corridor index of a synthetic straight street: exact signed distances, the nearest road and polygon
        /// overlaps (a footprint 0.5 m into the corridor reports 0.5 m; one 1 m clear reports none).</summary>
        [Test]
        public void CorridorIndexOfAStraightStreet()
        {
            TileData t = MeshingChecks.SyntheticTile(A, Smooth);
            t.Roads.Add(Road(RoadClass.Residential, RoadFlags.None, 10000, 50000, 90000, 50000));
            t.RoadAttrs.Add(new RoadAttrRecord { Area = AreaType.Urban, CorridorDm = new[] { 80, 80, 80, 80, 80 } });
            t.RoadStructures.Add(RoadStructureRecord.Absent);
            Assert.That(RoadWidthModel.UsesFinalCorridor(t), Is.True);
            RoadLayout layout = RoadLayout.For(t);
            Assert.That(layout.FinalCorridor, Is.True);
            RoadCorridorIndex idx = RoadCorridorIndex.ForTile(t);
            Assert.That(RoadCorridorIndex.ForTile(t), Is.SameAs(idx), "cached per tile");
            Assert.That(idx.SegmentCount, Is.GreaterThan(0));
            double h = layout.CorridorHalfM(0, 400);
            Assert.That(h, Is.GreaterThanOrEqualTo(0.5 * RoadClearance.MinCorridorM));
            double gx = A.X0 + 500, gz = A.Z0 + 500;
            Assert.That(idx.SignedDistance(gx, gz), Is.EqualTo(-h).Within(1e-6));
            Assert.That(idx.SignedDistance(gx, gz + h + 3), Is.EqualTo(3).Within(1e-6));
            Assert.That(idx.SignedDistance(gx, gz - h - 7.5), Is.EqualTo(7.5).Within(1e-6));
            Assert.That(idx.SignedDistance(gx, gz + 500), Is.EqualTo(RoadCorridorIndex.MaxSearchM), "far away");
            double sd;
            Assert.That(idx.NearestRoad(gx, gz + h + 1, out sd), Is.EqualTo(0));
            Assert.That(sd, Is.EqualTo(1).Within(1e-6));
            double depth;
            // A house whose front wall stands 0.5 m inside the corridor.
            double[] bx = { gx - 5, gx + 5, gx + 5, gx - 5 }, bz = { gz + h - 0.5, gz + h - 0.5, gz + h + 8, gz + h + 8 };
            Assert.That(idx.Overlaps(bx, bz, 4, out depth), Is.True);
            Assert.That(depth, Is.EqualTo(0.5).Within(1e-6));
            double[] cz = { gz + h + 1, gz + h + 1, gz + h + 9, gz + h + 9 };
            Assert.That(idx.Overlaps(bx, cz, 4, out depth), Is.False);
            Assert.That(depth, Is.EqualTo(0));
            // A footprint straddling the whole road.
            double[] wz = { gz - 20, gz - 20, gz + 20, gz + 20 };
            Assert.That(idx.Overlaps(bx, wz, 4, out depth), Is.True);
            Assert.That(depth, Is.EqualTo(h).Within(1e-6));
        }

        /// <summary>Tunnels (RSTR) are not drawn, have no corridor, and the rest of the tile is unaffected.</summary>
        [Test]
        public void TunnelsAreNotDrawnAndHaveNoCorridor()
        {
            TileData t = MeshingChecks.SyntheticTile(A, Smooth);
            t.Roads.Add(Road(RoadClass.Primary, RoadFlags.None, 10000, 30000, 90000, 30000));
            t.Roads.Add(Road(RoadClass.Primary, RoadFlags.None, 10000, 70000, 90000, 70000));
            t.RoadAttrs.Add(new RoadAttrRecord { Area = AreaType.Urban, CorridorDm = new[] { 200 } });
            t.RoadAttrs.Add(new RoadAttrRecord { Area = AreaType.Urban, CorridorDm = new[] { 200 } });
            t.RoadStructures.Add(new RoadStructureRecord { Kind = RoadStructureKind.Tunnel, Flags = RoadStructureFlags.CarAccessible });
            t.RoadStructures.Add(RoadStructureRecord.Absent);
            var o = new RoadOptions();
            Assert.That(RoadMesher.IsDrawn(t, 0, o), Is.False);
            Assert.That(RoadMesher.IsDrawn(t, 1, o), Is.True);
            var m = new MeshData();
            Assert.That(RoadMesher.Build(t, new TileHeightSampler(t, 2), o, m), Is.EqualTo(1));
            for (int v = 0; v < m.VertexCount; v++) Assert.That(m.Positions[3 * v + 2], Is.GreaterThan(600f), "nothing drawn along the tunnel");
            RoadCorridorIndex idx = RoadCorridorIndex.ForTile(t);
            Assert.That(idx.SignedDistance(A.X0 + 500, A.Z0 + 300), Is.GreaterThan(30), "no corridor over the tunnel");
            Assert.That(idx.SignedDistance(A.X0 + 500, A.Z0 + 700), Is.LessThan(0));
        }

        // -------------------------------------------------------------------------------------------------------
        // Seams, budgets, well-formedness
        // -------------------------------------------------------------------------------------------------------

        private static List<double[]>[] BorderLists(TileData t, MeshData m)
        {
            // 0: west (x = 0), 1: east (x = size), 2: south (z = 0), 3: north (z = size); entries (game along, y).
            var lists = new List<double[]>[4];
            for (int b = 0; b < 4; b++) lists[b] = new List<double[]>();
            float size = (float)t.Tile.Size;
            for (int v = 0; v < m.VertexCount; v++)
            {
                float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                if (Math.Abs(x) < 1e-3f) lists[0].Add(new[] { t.Tile.Z0 + z, (double)y });
                if (Math.Abs(x - size) < 1e-3f) lists[1].Add(new[] { t.Tile.Z0 + z, (double)y });
                if (Math.Abs(z) < 1e-3f) lists[2].Add(new[] { t.Tile.X0 + x, (double)y });
                if (Math.Abs(z - size) < 1e-3f) lists[3].Add(new[] { t.Tile.X0 + x, (double)y });
            }
            foreach (List<double[]> l in lists) l.Sort((a, b) => a[0] != b[0] ? a[0].CompareTo(b[0]) : a[1].CompareTo(b[1]));
            return lists;
        }

        /// <summary>Vertices of <paramref name="a"/> without a partner in <paramref name="b"/> within <paramref name="tol"/>
        /// (along the border and in height).</summary>
        private static int Unmatched(List<double[]> a, List<double[]> b, double tol)
        {
            int bad = 0, j0 = 0;
            foreach (double[] p in a)
            {
                while (j0 < b.Count && b[j0][0] < p[0] - tol) j0++;
                bool found = false;
                for (int j = j0; j < b.Count && b[j][0] <= p[0] + tol && !found; j++)
                    found = Math.Abs(b[j][1] - p[1]) <= tol;
                if (!found) bad++;
            }
            return bad;
        }

        /// <summary>
        /// Seams: every W2 road vertex that lies on a tile border of the sample (cut ribbons: carriageway, kerbs,
        /// footpaths, skirts, bridges) has a partner in the neighbouring tile's road mesh at the same place and height
        /// (within 1 cm everywhere, 2 mm almost everywhere), so the ribbons, their kerbs and their profile run on across
        /// every border without a step or a crack.
        /// </summary>
        [Test]
        public void RibbonsAreContinuousAcrossEveryTileSeam()
        {
            var o = new RoadOptions();
            var borders = new Dictionary<ulong, List<double[]>[]>();
            foreach (TileData t in LeafTiles())
            {
                var m = new MeshData();
                RoadMesher.Build(t, new TileHeightSampler(t, 2), o, m);
                MeshingChecks.AssertWellFormed(m, t.Tile.ToString());
                borders[t.Tile.Key] = BorderLists(t, m);
            }
            int seams = 0, vertices = 0, bad = 0, gaps = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                List<double[]>[] a;
                if (!borders.TryGetValue(id.Key, out a)) continue;
                for (int dir = 0; dir < 2; dir++)
                {
                    TileId nb = dir == 0 ? new TileId(10, id.Tx + 1, id.Ty) : new TileId(10, id.Tx, id.Ty + 1);
                    List<double[]>[] b;
                    if (!borders.TryGetValue(nb.Key, out b)) continue;
                    List<double[]> la = dir == 0 ? a[1] : a[3], lb = dir == 0 ? b[0] : b[2];
                    if (la.Count == 0 && lb.Count == 0) continue;
                    seams++;
                    vertices += la.Count + lb.Count;
                    int u = Unmatched(la, lb, 2e-3) + Unmatched(lb, la, 2e-3);
                    if (u > 0 && bad == 0) TestContext.Progress.WriteLine("seam " + id + " / " + nb + ": " + u + " unmatched of " + (la.Count + lb.Count));
                    bad += u;
                    gaps += Unmatched(la, lb, 0.01) + Unmatched(lb, la, 0.01);
                }
            }
            TestContext.Progress.WriteLine("seams " + seams + ", border vertices " + vertices + ", over 2 mm " + bad + ", over 1 cm " + gaps);
            Assert.That(seams, Is.GreaterThan(50));
            Assert.That(vertices, Is.GreaterThan(10000));
            Assert.That(gaps, Is.LessThanOrEqualTo(vertices / 5000), "border vertices without a partner within 1 cm across the seam");
            // A few pieces crossing a border right next to a tile corner meet a few millimetres apart (the cut lies on
            // two borders at once).
            Assert.That(bad, Is.LessThanOrEqualTo(vertices / 200), "border vertices without a partner within 2 mm across the seam");
        }

        /// <summary>
        /// Budgets: on the four leaf tiles with the most road length, the road layer (ribbons, caps, rings, islands) and
        /// the decal layer stay within their per-tile triangle budgets, and the low-detail options (Detail off) cut the
        /// road layer by at least a fifth.
        /// </summary>
        [Test]
        public void RoadTriangleBudgetsHoldOnTheBusiestTiles()
        {
            const int RoadBudget = 90000, DecalBudget = 30000;
            var busiest = LeafTiles().OrderByDescending(t => t.Roads.Sum(r => r.PointCount)).Take(4).ToList();
            foreach (TileData t in busiest)
            {
                var h = new TileHeightSampler(t, 2);
                var roads = new MeshData();
                var decals = new MeshData();
                var o = new RoadOptions();
                RoadMesher.Build(t, h, o, roads);
                MarkingMesher.Build(t, h, o, decals);
                var low = new MeshData();
                RoadMesher.Build(t, h, new RoadOptions { Detail = false }, low);
                TestContext.Progress.WriteLine("budget " + t.Tile + ": roads " + roads.TriangleCount + " (low " + low.TriangleCount + "), decals " + decals.TriangleCount);
                Assert.That(roads.TriangleCount, Is.LessThanOrEqualTo(RoadBudget), t.Tile + " roads");
                Assert.That(decals.TriangleCount, Is.LessThanOrEqualTo(DecalBudget), t.Tile + " decals");
                Assert.That(low.TriangleCount, Is.LessThan(roads.TriangleCount * 0.8), t.Tile + " low detail");
            }
        }

        /// <summary>
        /// Every leaf tile meshes without NaN (well-formed roads and decals: finite positions, unit normals, opaque
        /// colours) and with the material channel contract on every vertex (UV0: u = a <see cref="MaterialChannel"/>, v =
        /// baked AO in [0, 1]); building a tile twice gives identical meshes.
        /// </summary>
        [Test]
        public void EveryLeafTileMeshesWithoutNaNAndWithTheChannelContract()
        {
            var o = new RoadOptions();
            int tiles = 0;
            long tris = 0;
            var seen = new HashSet<MaterialChannel>();
            foreach (TileData t in LeafTiles())
            {
                tiles++;
                var h = new TileHeightSampler(t, 2);
                var roads = new MeshData();
                var decals = new MeshData();
                RoadMesher.Build(t, h, o, roads);
                MarkingMesher.Build(t, h, o, decals);
                foreach (var (m, what) in new[] { (roads, " roads"), (decals, " decals") })
                {
                    MeshingChecks.AssertWellFormed(m, t.Tile + what);
                    if (m.VertexCount == 0) continue;
                    Assert.That(m.HasUv0, Is.True, t.Tile + what);
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        float u = m.Uv0[2 * v], ao = m.Uv0[2 * v + 1];
                        if (u != Math.Floor(u) || !Enum.IsDefined(typeof(MaterialChannel), (byte)u))
                            Assert.Fail(t.Tile + what + ": vertex " + v + " has channel " + u);
                        if (!(ao >= 0f && ao <= 1f)) Assert.Fail(t.Tile + what + ": vertex " + v + " has AO " + ao);
                        seen.Add((MaterialChannel)(byte)u);
                    }
                    tris += m.TriangleCount;
                }
            }
            TestContext.Progress.WriteLine("channels: " + string.Join(", ", seen.OrderBy(c => c)) + "; " + tris + " triangles on " + tiles + " tiles");
            Assert.That(seen, Does.Contain(MaterialChannel.Asphalt));
            Assert.That(seen, Does.Contain(MaterialChannel.Brick), "heritage brick paving");
            Assert.That(seen, Does.Contain(MaterialChannel.Flagstone), "heritage stone flags");
            Assert.That(seen, Does.Contain(MaterialChannel.Marking), "road paint");

            TileData d = StreamingSampleRegion.Tile(StreamingSampleRegion.DensestLeaf());
            var m1 = new MeshData();
            var m2 = new MeshData();
            RoadMesher.Build(d, new TileHeightSampler(d, 2), o, m1);
            RoadMesher.Build(d, new TileHeightSampler(d, 2), o, m2);
            Assert.That(m2.VertexCount, Is.EqualTo(m1.VertexCount));
            Assert.That(m2.Positions.Take(3 * m2.VertexCount), Is.EqualTo(m1.Positions.Take(3 * m1.VertexCount)), "deterministic");
            Assert.That(m2.Indices.Take(m2.IndexCount), Is.EqualTo(m1.Indices.Take(m1.IndexCount)), "deterministic");
        }

        // -------------------------------------------------------------------------------------------------------
        // Vertical profile and structures (synthetic RSTR records)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The smoothed profile irons out DEM steps (a 0.25 m, 7 m ripple on a hillside) yet stays on the drawn terrain:
        /// the carriageway centre never leaves the band around the lifted terrain, no road vertex is buried more than
        /// <see cref="RoadGrade.MaxBuryM"/>, and the profile's roughness (second differences along the road) is a small
        /// fraction of the terrain's.
        /// </summary>
        [Test]
        public void SmoothProfileIronsOutDemStepsAndStaysOnTheTerrain()
        {
            Func<double, double, double> f = (x, z) => Smooth(x, z) + 0.25 * Math.Sin(x * 0.9) + 0.02 * (z - A.Z0);
            TileData t = MeshingChecks.SyntheticTile(A, f, 513);
            t.Roads.Add(Road(RoadClass.Secondary, RoadFlags.None, 10000, 40000, 50000, 42000, 90000, 60000));
            var h = new TileHeightSampler(t, 1);
            var o = new RoadOptions();
            var m = new MeshData();
            Assert.That(RoadMesher.Build(t, h, o, m), Is.EqualTo(1));
            MeshingChecks.AssertWellFormed(m, "ripple");
            RoadGrade g = RoadGrade.For(t, h, o);
            RoadLayout layout = RoadLayout.For(t);
            RoadCentreline c = layout.Centres[0];
            float lift = g.LiftOf(0);
            var ys = new List<double>();
            var ts = new List<double>();
            for (double s = 0; s <= c.RawLengthM; s += 1)
            {
                double x, z, tx, tz;
                c.At(s, out x, out z, out tx, out tz);
                float y = g.SurfaceY(0, s, 0, x, z);
                float ground = g.Terrain(x, z) + lift;
                Assert.That(y, Is.GreaterThanOrEqualTo(ground - RoadGrade.BandM - 1e-3f), "under the band at " + s);
                Assert.That(y, Is.LessThanOrEqualTo(ground + 0.6f), "floating at " + s);
                ys.Add(y);
                ts.Add(g.Terrain(x, z));
            }
            double road = 0, terrain = 0;
            for (int k = 1; k + 1 < ys.Count; k++)
            {
                road += Math.Abs(ys[k + 1] - 2 * ys[k] + ys[k - 1]);
                terrain += Math.Abs(ts[k + 1] - 2 * ts[k] + ts[k - 1]);
            }
            TestContext.Progress.WriteLine("roughness: road " + road.ToString("0.00") + ", terrain " + terrain.ToString("0.00"));
            Assert.That(road, Is.LessThan(0.25 * terrain), "the road irons out the ripple");
            // No up-facing road vertex buried more than MaxBuryM under the lifted terrain.
            for (int tri = 0; tri < m.TriangleCount; tri++)
            {
                double fx, fy, fz;
                MeshingChecks.Facet(m, tri, out fx, out fy, out fz);
                double len = Math.Sqrt(fx * fx + fy * fy + fz * fz);
                if (len < 1e-9 || fy < 0.9 * len) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = m.Indices[3 * tri + k];
                    float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                    if (y < g.Terrain(x, z) + lift - RoadGrade.MaxBuryM - 2e-3f) Assert.Fail("vertex " + v + " buried at " + x + "," + z);
                }
            }
        }

        private static float[] DeckOver(TileData t, int[] pointsCm, float above)
        {
            var d = new float[pointsCm.Length / 2];
            for (int k = 0; k < d.Length; k++) d[k] = (float)Smooth(t.Tile.X0 + pointsCm[2 * k] / 100.0, t.Tile.Z0 + pointsCm[2 * k + 1] / 100.0) + above;
            return d;
        }

        /// <summary>
        /// IsElevated pieces (RSTR flyover with DeckY) are drawn at their deck heights plus the surfacing (the bridges
        /// package draws the slab under them), in plan exactly on the mapped polyline; a deck with NaN at its ends is
        /// draped there and rises onto the deck in between.
        /// </summary>
        [Test]
        public void ElevatedPiecesAreDrawnAtTheirDeckHeight()
        {
            int[] pts = { 10000, 50000, 30000, 50000, 50000, 50000 };
            TileData t = MeshingChecks.SyntheticTile(A, Smooth);
            t.Roads.Add(Road(RoadClass.Primary, RoadFlags.Bridge, pts));
            t.RoadAttrs.Add(new RoadAttrRecord { Area = AreaType.Urban, CorridorDm = new[] { 200 } });
            float[] deck = DeckOver(t, pts, 7f);
            t.RoadStructures.Add(new RoadStructureRecord { Kind = RoadStructureKind.Flyover, Flags = RoadStructureFlags.CarAccessible, DeckY = deck });
            var h = new TileHeightSampler(t, 2);
            var o = new RoadOptions();
            var m = new MeshData();
            Assert.That(RoadMesher.Build(t, h, o, m), Is.EqualTo(1));
            MeshingChecks.AssertWellFormed(m, "flyover");
            RoadGrade g = RoadGrade.For(t, h, o);
            Assert.That(g.IsAbsolute(0), Is.True);
            Assert.That(g.OnDeck(0, 200), Is.True);
            RoadLayout layout = RoadLayout.For(t);
            for (int k = 0; k < 3; k++)
            {
                double s = 200 * k, x = 100 + s;
                Assert.That(g.SurfaceY(0, s, 0, x, 500), Is.EqualTo(deck[k] + RoadGrade.DeckSurfacingM).Within(1e-3), "deck point " + k);
            }
            // The mapped plan: an elevated piece is not filleted.
            Assert.That(layout.Centres[0].MinRadiusM, Is.EqualTo(0f));
            // Every up-facing vertex of the ribbon lies on the deck surface (to the row tolerance); nothing hangs under it.
            int onDeck = 0;
            for (int tri = 0; tri < m.TriangleCount; tri++)
            {
                double fx, fy, fz;
                MeshingChecks.Facet(m, tri, out fx, out fy, out fz);
                double len = Math.Sqrt(fx * fx + fy * fy + fz * fz);
                if (len < 1e-9 || fy < 0.95 * len) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = m.Indices[3 * tri + k];
                    float x = m.Positions[3 * v], y = m.Positions[3 * v + 1];
                    if (x < 120 || x > 480 || Math.Abs(m.Positions[3 * v + 2] - 500f) > layout.DrawnHalfWidthAt(0, x - 100) + 0.01f) continue;
                    float want = g.SurfaceY(0, x - 100, 0, x, m.Positions[3 * v + 2]);
                    Assert.That(y, Is.EqualTo(want).Within(RibbonMesher_RowToleranceM + 1e-3), "deck vertex at " + x);
                    onDeck++;
                }
            }
            Assert.That(onDeck, Is.GreaterThan(6));
            for (int v = 0; v < m.VertexCount; v++)
            {
                float x = m.Positions[3 * v];
                if (x < 150 || x > 450) continue;
                float deckAt = g.SurfaceY(0, x - 100, 0, x, 500);
                Assert.That(m.Positions[3 * v + 1], Is.GreaterThan(deckAt - 0.5f), "nothing hangs under the deck at " + x);
            }

            // Partial deck: NaN at both ends, the ends follow the terrain.
            TileData p = MeshingChecks.SyntheticTile(A, Smooth);
            p.Roads.Add(Road(RoadClass.Primary, RoadFlags.Bridge, pts));
            p.RoadAttrs.Add(new RoadAttrRecord { Area = AreaType.Urban, CorridorDm = new[] { 200 } });
            float[] part = DeckOver(p, pts, 7f);
            part[0] = float.NaN;
            part[2] = float.NaN;
            p.RoadStructures.Add(new RoadStructureRecord { Kind = RoadStructureKind.Flyover, Flags = RoadStructureFlags.CarAccessible, DeckY = part });
            var hp = new TileHeightSampler(p, 2);
            var mp = new MeshData();
            Assert.That(RoadMesher.Build(p, hp, o, mp), Is.EqualTo(1));
            MeshingChecks.AssertWellFormed(mp, "partial deck");
            RoadGrade gp = RoadGrade.For(p, hp, o);
            Assert.That(gp.IsAbsolute(0), Is.False);
            Assert.That(gp.SurfaceY(0, 200, 0, 300, 500), Is.EqualTo(part[1] + RoadGrade.DeckSurfacingM).Within(1e-3));
            float end = gp.SurfaceY(0, 0, 0, 100, 500), ground = gp.Terrain(100, 500) + gp.LiftOf(0);
            Assert.That(end, Is.EqualTo(ground).Within(0.3), "a NaN end is draped (the band around the highest point across)");
        }

        /// <summary>An underpass whose clearance is under 5.5 m is lowered by the difference in its middle (ramps at
        /// <see cref="RoadGrade.RampGrade"/>), and stays at grade at its ends.</summary>
        [Test]
        public void UnderpassesAreLoweredToClearFiveAndAHalfMetres()
        {
            TileData t = MeshingChecks.SyntheticTile(A, Smooth);
            t.Roads.Add(Road(RoadClass.Secondary, RoadFlags.None, 20000, 50000, 80000, 50000));
            t.RoadAttrs.Add(new RoadAttrRecord { Area = AreaType.Urban, CorridorDm = new[] { 150 } });
            t.RoadStructures.Add(new RoadStructureRecord { Kind = RoadStructureKind.Underpass, Flags = RoadStructureFlags.CarAccessible, ClearanceM = 4.0f });
            var h = new TileHeightSampler(t, 2);
            var o = new RoadOptions();
            var m = new MeshData();
            Assert.That(RoadMesher.Build(t, h, o, m), Is.EqualTo(1));
            MeshingChecks.AssertWellFormed(m, "underpass");
            RoadGrade g = RoadGrade.For(t, h, o);
            Assert.That(g.IsLowered(0), Is.True);
            float drop = RoadClearance.MinUnderpassClearanceM - 4.0f;
            float mid = g.SurfaceY(0, 300, 0, 500, 500), midGround = g.Terrain(500, 500) + g.LiftOf(0);
            Assert.That(midGround - mid, Is.EqualTo(drop).Within(RoadGrade.BandM + 0.01), "lowered by the missing clearance");
            float end = g.SurfaceY(0, 0, 0, 200, 500), endGround = g.Terrain(200, 500) + g.LiftOf(0);
            Assert.That(end, Is.EqualTo(endGround).Within(0.3), "at grade at the ends (the band around the highest point across)");
            // The ramp is never steeper than the ramp grade (plus the terrain's own slope).
            float prev = g.SurfaceY(0, 0, 0, 200, 500);
            for (int s = 2; s <= 600; s += 2)
            {
                float y = g.SurfaceY(0, s, 0, 200 + s, 500);
                double slope = Math.Abs(y - prev) / 2.0;
                Assert.That(slope, Is.LessThan(RoadGrade.RampGrade + 0.05), "at " + s);
                prev = y;
            }
            // The default (clearance 5.5 m or more) is not lowered.
            TileData u = MeshingChecks.SyntheticTile(A, Smooth);
            u.Roads.Add(Road(RoadClass.Secondary, RoadFlags.None, 20000, 50000, 80000, 50000));
            u.RoadAttrs.Add(new RoadAttrRecord { Area = AreaType.Urban, CorridorDm = new[] { 150 } });
            u.RoadStructures.Add(new RoadStructureRecord { Kind = RoadStructureKind.Underpass, Flags = RoadStructureFlags.CarAccessible, ClearanceM = 6f });
            Assert.That(RoadGrade.For(u, new TileHeightSampler(u, 2), o).IsLowered(0), Is.False);
        }

        /// <summary>
        /// Final-corridor semantics (data package: RATR with RSTR carries the final game corridor, at least 4.8 m): the
        /// width uses the corridor without the stage-one 1 m clearance, the drawn width never goes under the rideable floor
        /// even where the corridor or the mapped width is narrower, and a stage-one pack (RATR without RSTR) still
        /// subtracts the clearance.
        /// </summary>
        [Test]
        public void FinalCorridorsAreDrawnWithoutTheStageOneClearance()
        {
            var lane = new RoadRecord { OsmWayId = 9, RoadClass = RoadClass.Residential, Surface = Surface.Asphalt, Points = new[] { 10000, 50000, 90000, 50000 } };
            var narrow = new RoadRecord { OsmWayId = 10, RoadClass = RoadClass.Service, Surface = Surface.Asphalt, WidthCm = 250, Points = new[] { 10000, 20000, 90000, 20000 } };
            var a0 = new RoadAttrRecord { Area = AreaType.Urban, CorridorDm = new[] { 60, 60, 60, 60, 60 } };
            var a1 = new RoadAttrRecord { Area = AreaType.OldCore, CorridorDm = new[] { 48, 48, 48, 48, 48 } };
            TileData t = MeshingChecks.SyntheticTile(A, Smooth);
            t.Roads.Add(lane);
            t.Roads.Add(narrow);
            t.RoadAttrs.Add(a0);
            t.RoadAttrs.Add(a1);
            TileData stage1 = MeshingChecks.SyntheticTile(A, Smooth);
            stage1.Roads.AddRange(t.Roads);
            stage1.RoadAttrs.AddRange(t.RoadAttrs);
            t.RoadStructures.Add(RoadStructureRecord.Absent);
            t.RoadStructures.Add(RoadStructureRecord.Absent);
            RoadLayout fin = RoadLayout.For(t), old = RoadLayout.For(stage1);
            Assert.That(fin.FinalCorridor, Is.True);
            Assert.That(old.FinalCorridor, Is.False);
            float real = RoadWidthModel.RealWidthM(lane, a0), nominal = RoadWidthModel.NominalGameWidthM(lane, a0);
            Assert.That(RoadWidthModel.LimitAt(a0, real, 400, true), Is.EqualTo(6f).Within(1e-3), "the final corridor as it is");
            Assert.That(RoadWidthModel.LimitAt(a0, real, 400, false), Is.EqualTo(5f).Within(1e-3), "stage one: less the 1 m clearance");
            float wFinal = fin.Profiles[0].WidthAt(400), wOld = old.Profiles[0].WidthAt(400);
            TestContext.Progress.WriteLine("final corridor: real " + real + ", nominal " + nominal + ", final " + wFinal + ", stage one " + wOld);
            Assert.That(wFinal, Is.EqualTo(Math.Max(real, Math.Min(nominal, 6f))).Within(1e-3));
            Assert.That(wOld, Is.EqualTo(Math.Max(real, Math.Min(nominal, 5f))).Within(1e-3));
            Assert.That(fin.Profiles[0].MinDrawn, Is.GreaterThanOrEqualTo(RoadClearance.MinCorridorM - 1e-3f));
            Assert.That(fin.Profiles[1].MinDrawn, Is.GreaterThanOrEqualTo(RoadClearance.MinCorridorM - 1e-3f), "a 2.5 m lane is drawn rideable");
            Assert.That(fin.Profiles[1].MinAccessWidth, Is.LessThan(RoadClearance.MinCorridorM), "its lanes keep the mapped width");
            Assert.That(old.Profiles[1].MinDrawn, Is.GreaterThanOrEqualTo(RoadClearance.MinCorridorM - 1e-3f), "the floor holds on stage-one packs too");
            // The corridor index keeps 4.8 m clear around the narrow lane.
            Assert.That(RoadCorridorIndex.ForTile(t).SignedDistance(A.X0 + 500, A.Z0 + 200 + 2.35), Is.LessThan(0));
        }
    }
}
