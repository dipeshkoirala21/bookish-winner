using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class MeshingRoadTests
    {
        private static readonly TileId A = new TileId(10, 516, 161);
        private static readonly TileId B = new TileId(10, 517, 161);

        private static double Smooth(double x, double z)
        {
            return 1300 + 6 * Math.Sin(x / 170.0) + 4 * Math.Cos(z / 130.0) + 0.01 * (x - 528000);
        }

        private static RoadRecord Road(RoadClass c, Surface s, RoadFlags flags, params int[] pointsCm)
        {
            return new RoadRecord { RoadClass = c, Surface = s, Flags = flags, Points = pointsCm, OsmWayId = 1 };
        }

        /// <summary>A sampler that is not a <see cref="TileHeightSampler"/>: the mesher then emits plain
        /// cross-sections at sampled heights instead of draping.</summary>
        private sealed class PlainSampler : IHeightSampler
        {
            private readonly TileHeightSampler _inner;

            public PlainSampler(TileHeightSampler inner)
            {
                _inner = inner;
            }

            public bool TryHeight(double x, double z, out float h)
            {
                return _inner.TryHeight(x, z, out h);
            }
        }

        /// <summary>The vertices of a mesh lying on a tile border line (x or z == <paramref name="at"/> in local
        /// metres), as game (along, y) pairs sorted by the coordinate along the border.</summary>
        private static List<double[]> BorderVertices(MeshData m, TileId tile, bool vertical, float at)
        {
            var list = new List<double[]>();
            for (int v = 0; v < m.VertexCount; v++)
            {
                float c = vertical ? m.Positions[3 * v] : m.Positions[3 * v + 2];
                if (Math.Abs(c - at) > 1e-3f) continue;
                double along = vertical ? tile.Z0 + m.Positions[3 * v + 2] : tile.X0 + m.Positions[3 * v];
                list.Add(new[] { along, m.Positions[3 * v + 1] });
            }
            list.Sort((a, b) => a[0].CompareTo(b[0]));
            // Drop duplicates (shared corners of neighbouring pieces).
            var unique = new List<double[]>();
            foreach (double[] e in list)
                if (unique.Count == 0 || Math.Abs(unique[unique.Count - 1][0] - e[0]) > 1e-3) unique.Add(e);
            return unique;
        }

        private static void AssertSameBorder(List<double[]> a, List<double[]> b, string what)
        {
            Assert.That(a.Count, Is.GreaterThanOrEqualTo(2), what);
            Assert.That(b.Count, Is.EqualTo(a.Count), what);
            for (int i = 0; i < a.Count; i++)
            {
                if (Math.Abs(a[i][0] - b[i][0]) > 2e-3 || Math.Abs(a[i][1] - b[i][1]) > 2e-3)
                    Assert.Fail(what + ": border vertex " + i + " differs: " + a[i][0] + "/" + a[i][1] + " vs " + b[i][0] + "/" + b[i][1]);
            }
        }

        /// <summary>
        /// Overlapping ribbons of the same class used to share one lift and z-fight at every junction. On the real
        /// sample, pieces of different ways that meet at a rendered point and share a class now mostly get different
        /// lifts (a hash rank collides about once in PieceLiftLevels), a way keeps one lift in every tile, and the
        /// piece lift never breaks the class order.
        /// </summary>
        [Test]
        public void SameClassRibbonsAtJunctionsGetDistinctLifts()
        {
            var o = new RoadOptions();
            Assert.That((o.PieceLiftLevels - 1) * o.PieceLiftStepM, Is.LessThan(o.ClassLiftStepM), "class order kept");
            int pairs = 0, coplanar = 0;
            var wayLift = new Dictionary<ulong, float>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var at = new Dictionary<long, List<RoadRecord>>();
                foreach (RoadRecord r in t.Roads)
                {
                    if (!RoadMesher.IsDrawn(r, o)) continue;
                    float lift = RoadMesher.LiftOf(r, o);
                    int prio = Math.Min(8, RoadStyle.Priority(r.RoadClass));
                    Assert.That(lift, Is.GreaterThanOrEqualTo(o.LiftM + prio * o.ClassLiftStepM - 1e-6));
                    Assert.That(lift, Is.LessThan(o.LiftM + (prio + 1) * o.ClassLiftStepM));
                    float seen;
                    if (wayLift.TryGetValue(r.OsmWayId, out seen) && r.WidthCm == 0)
                        Assert.That(lift, Is.EqualTo(seen), "way " + r.OsmWayId + " has one lift everywhere");
                    else if (r.WidthCm == 0) wayLift[r.OsmWayId] = lift;
                    int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? r.PointCount - 2 : r.PointCount - 1;
                    for (int i = first; i <= last; i++)
                    {
                        long key = ((long)r.Points[2 * i] << 32) ^ (uint)r.Points[2 * i + 1];
                        List<RoadRecord> list;
                        if (!at.TryGetValue(key, out list)) at[key] = list = new List<RoadRecord>();
                        if (!list.Contains(r)) list.Add(r);
                    }
                }
                foreach (List<RoadRecord> list in at.Values)
                {
                    for (int a = 0; a < list.Count; a++)
                    {
                        for (int b = a + 1; b < list.Count; b++)
                        {
                            RoadRecord ra = list[a], rb = list[b];
                            if (ra.OsmWayId == rb.OsmWayId || RoadStyle.Priority(ra.RoadClass) != RoadStyle.Priority(rb.RoadClass)) continue;
                            pairs++;
                            if (RoadMesher.LiftOf(ra, o) == RoadMesher.LiftOf(rb, o)) coplanar++;
                        }
                    }
                }
            }
            TestContext.WriteLine("same-class junction pairs {0}, still coplanar {1}", pairs, coplanar);
            Assert.That(pairs, Is.GreaterThan(1000));
            Assert.That(coplanar, Is.LessThan(pairs * 0.2), "was every pair before the piece lift");

            var off = new RoadOptions { PieceLiftLevels = 1 };
            Assert.That(RoadMesher.PieceRank(StreamingSampleRegion.Tile(StreamingSampleRegion.DensestLeaf()).Roads[0], off), Is.EqualTo(0));
        }

        [Test]
        public void StraightRibbonWidthDensityColoursAndUvs()
        {
            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => 1300);
            t.Roads.Add(Road(RoadClass.Residential, Surface.Brick, RoadFlags.None, 10000, 20000, 20000, 20000));
            var m = new MeshData();
            var o = new RoadOptions();
            int drawn = RoadMesher.Build(t, new PlainSampler(new TileHeightSampler(t, 1)), o, m);
            Assert.That(drawn, Is.EqualTo(1));
            Assert.That(m.VertexCount, Is.EqualTo(3 * 14), "100 m at 8 m spacing: 13 segments");
            Assert.That(m.TriangleCount, Is.EqualTo(4 * 13));
            MeshingChecks.AssertWellFormed(m, "straight");
            MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, 0.99, "straight");
            float y = Ght.Dequantize(Ght.Quantize(1300)) + RoadMesher.LiftOf(t.Roads[0], o);
            for (int sec = 0; sec < 14; sec++)
            {
                int l = 3 * sec, c = l + 1, r = l + 2;
                Assert.That(m.Positions[3 * c], Is.EqualTo(100f + 100f * sec / 13).Within(1e-3));
                Assert.That(m.Positions[3 * l + 2] - m.Positions[3 * r + 2], Is.EqualTo(5f).Within(1e-4), "left is north of an eastbound road");
                Assert.That(m.Positions[3 * c + 1], Is.EqualTo(y).Within(1e-4));
                Assert.That(m.Uv0[2 * l], Is.EqualTo(0f));
                Assert.That(m.Uv0[2 * c], Is.EqualTo(0.5f));
                Assert.That(m.Uv0[2 * r], Is.EqualTo(1f));
                Assert.That(m.Uv0[2 * c + 1], Is.EqualTo(100f * sec / 13 / 4).Within(1e-4));
            }
            uint edge = RoadStyle.SurfaceRgba(Surface.Brick);
            Assert.That(m.Colors[0], Is.EqualTo((byte)(edge >> 24)));
            Assert.That(m.Colors[4], Is.GreaterThan(m.Colors[0]), "lighter centre line");
            Assert.That(m.HasUv0, Is.True);
        }

        [Test]
        public void WidthsTunnelsTrailsAndBridges()
        {
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Trunk), Is.EqualTo(10f));
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Primary), Is.EqualTo(8f));
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Secondary), Is.EqualTo(7f));
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Tertiary), Is.EqualTo(6f));
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Unclassified), Is.EqualTo(5f));
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Residential), Is.EqualTo(5f));
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Service), Is.EqualTo(4f));
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Track), Is.EqualTo(4f));
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Path), Is.EqualTo(2f));
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Footway), Is.EqualTo(2f));
            Assert.That(RoadStyle.DefaultWidthM(RoadClass.Steps), Is.EqualTo(2f));
            Assert.That(RoadStyle.WidthM(new RoadRecord { RoadClass = RoadClass.Primary, WidthCm = 1250 }), Is.EqualTo(12.5f));
            Assert.That(RoadStyle.WidthM(new RoadRecord { RoadClass = RoadClass.Primary, WidthCm = 20 }), Is.EqualTo(RoadStyle.MinWidthM));
            foreach (Surface s in Enum.GetValues(typeof(Surface))) Assert.That(MeshColor.A(RoadStyle.SurfaceRgba(s)), Is.EqualTo(255));

            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => 1300 - 30 * Math.Exp(-Math.Pow((x - A.X0 - 500) / 40, 2)));
            t.Roads.Add(Road(RoadClass.Primary, Surface.Asphalt, RoadFlags.Tunnel, 10000, 10000, 20000, 10000));
            t.Roads.Add(Road(RoadClass.Footway, Surface.Dirt, RoadFlags.None, 10000, 20000, 20000, 20000));
            t.Roads.Add(Road(RoadClass.Secondary, Surface.Concrete, RoadFlags.Bridge, 40000, 50000, 60000, 50000)); // over a 30 m deep valley
            var m = new MeshData();
            var sampler = new TileHeightSampler(t, 1);
            Assert.That(RoadMesher.Build(t, sampler, new RoadOptions(), m), Is.EqualTo(2), "the tunnel is skipped");
            Assert.That(RoadMesher.Build(t, sampler, new RoadOptions { IncludeTrails = false }, new MeshData()), Is.EqualTo(1));
            // The bridge deck runs straight between its ends instead of dipping into the valley.
            float end;
            sampler.TryHeight(A.X0 + 400, A.Z0 + 500, out end);
            float deckMin = float.MaxValue;
            for (int v = 0; v < m.VertexCount; v++)
                if (m.Positions[3 * v + 2] > 495) deckMin = Math.Min(deckMin, m.Positions[3 * v + 1]);
            Assert.That(deckMin, Is.GreaterThan(end), "deck above the valley floor (" + end + ")");
            MeshingChecks.AssertWellFormed(m, "bridge");
        }

        /// <summary>A road crossing from tile A into tile B: the context points give both pieces the same tangent at
        /// the cut and the cut cross-section lies on the border line, so the ribbons meet exactly (draped or not)
        /// and neither overhangs into the other tile.</summary>
        [Test]
        public void RibbonsMeetAcrossATileBorder()
        {
            TileData ta = MeshingChecks.SyntheticTile(A, Smooth);
            TileData tb = MeshingChecks.SyntheticTile(B, Smooth);
            // Game path: P0 (900,300), P1 (1000,350), cut C (1024,362), P2 (1084,392), P3 (1174,450) in A-local metres.
            ta.Roads.Add(Road(RoadClass.Primary, Surface.Asphalt, RoadFlags.HasNextCtx, 90000, 30000, 100000, 35000, 102400, 36200, 108400, 39200));
            tb.Roads.Add(Road(RoadClass.Primary, Surface.Asphalt, RoadFlags.HasPrevCtx, -2400, 35000, 0, 36200, 6000, 39200, 15000, 45000));
            var sa = new TileHeightSampler(ta, 2);
            var sb = new TileHeightSampler(tb, 2);

            // Plain cross-sections: A's last section equals B's first, on the border line x = 1024 / 0.
            var ma = new MeshData();
            var mb = new MeshData();
            RoadMesher.Build(ta, new PlainSampler(sa), new RoadOptions(), ma);
            RoadMesher.Build(tb, new PlainSampler(sb), new RoadOptions(), mb);
            int lastA = ma.VertexCount - 3;
            for (int k = 0; k < 3; k++)
            {
                int va = lastA + k, vb = k;
                Assert.That(ma.Positions[3 * va], Is.EqualTo(1024f).Within(1e-3), "on the border " + k);
                Assert.That(mb.Positions[3 * vb], Is.EqualTo(0f).Within(1e-3), "on the border " + k);
                Assert.That(A.Z0 + ma.Positions[3 * va + 2], Is.EqualTo(B.Z0 + mb.Positions[3 * vb + 2]).Within(1e-3), "z " + k);
                Assert.That(ma.Positions[3 * va + 1], Is.EqualTo(mb.Positions[3 * vb + 1]).Within(1e-3), "y " + k);
            }
            // The cut section spans the full road width across the oblique road: 8 m / cos(26.6 deg) along the border.
            double span = ma.Positions[3 * lastA + 2] - ma.Positions[3 * (lastA + 2) + 2];
            Assert.That(span, Is.EqualTo(8 * Math.Sqrt(5) / 2).Within(0.05));

            // Draped: identical vertices along the border on both sides, nothing beyond it.
            ma.Clear();
            mb.Clear();
            RoadMesher.Build(ta, sa, new RoadOptions(), ma);
            RoadMesher.Build(tb, sb, new RoadOptions(), mb);
            for (int v = 0; v < ma.VertexCount; v++) Assert.That(ma.Positions[3 * v], Is.LessThanOrEqualTo(1024.001f));
            for (int v = 0; v < mb.VertexCount; v++) Assert.That(mb.Positions[3 * v], Is.GreaterThanOrEqualTo(-0.001f));
            AssertSameBorder(BorderVertices(ma, A, true, 1024f), BorderVertices(mb, B, true, 0f), "draped join");
        }

        [Test]
        public void DrapedRibbonLiesExactlyOnTheRenderedSurface()
        {
            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => Smooth(x, z) + 3 * Math.Sin(x * 0.37) * Math.Cos(z * 0.23));
            t.Roads.Add(Road(RoadClass.Secondary, Surface.Concrete, RoadFlags.None, 10000, 12000, 30000, 18000, 52000, 15500, 70000, 40000));
            var s = new TileHeightSampler(t, 2);
            var m = new MeshData();
            var o = new RoadOptions();
            Assert.That(RoadMesher.Build(t, s, o, m), Is.EqualTo(1));
            MeshingChecks.AssertWellFormed(m, "draped");
            Assert.That(m.HasUv0, Is.True);
            float lift = RoadMesher.LiftOf(t.Roads[0], o);
            double area = 0;
            for (int tri = 0; tri < m.TriangleCount; tri++)
            {
                double fx, fy, fz;
                MeshingChecks.Facet(m, tri, out fx, out fy, out fz);
                Assert.That(fy, Is.GreaterThan(0), "up-facing");
                area += 0.5 * fy;
                // Every point of every triangle (vertices and edge midpoints) sits exactly lift above the surface.
                for (int k = 0; k < 3; k++)
                {
                    int a = m.Indices[3 * tri + k], b = m.Indices[3 * tri + (k + 1) % 3];
                    for (int w = 0; w <= 1; w++)
                    {
                        double px = w == 0 ? m.Positions[3 * a] : 0.5 * (m.Positions[3 * a] + m.Positions[3 * b]);
                        double pz = w == 0 ? m.Positions[3 * a + 2] : 0.5 * (m.Positions[3 * a + 2] + m.Positions[3 * b + 2]);
                        double py = w == 0 ? m.Positions[3 * a + 1] : 0.5 * (m.Positions[3 * a + 1] + m.Positions[3 * b + 1]);
                        float h;
                        Assert.That(s.TryHeight(A.X0 + px, A.Z0 + pz, out h), Is.True);
                        Assert.That(py - h, Is.EqualTo(lift).Within(1e-3));
                    }
                }
            }
            // Plan area of a 7 m ribbon along the polyline (corners make it a little different).
            double len = Math.Sqrt(200 * 200 + 60 * 60) + Math.Sqrt(220 * 220 + 25 * 25) + Math.Sqrt(180 * 180 + 245 * 245);
            Assert.That(area, Is.EqualTo(7 * len).Within(0.03 * 7 * len));
            // UVs: U spans 0..1 across, V grows along at a quarter unit per metre.
            float vmax = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                Assert.That(m.Uv0[2 * v], Is.InRange(-1e-4f, 1.0001f));
                vmax = Math.Max(vmax, m.Uv0[2 * v + 1]);
            }
            Assert.That(vmax, Is.EqualTo((float)(len / 4)).Within(0.01));
        }

        private static TileData OnlyRoad(TileData t, RoadRecord r)
        {
            var c = new TileData { Tile = t.Tile, HeightsN = t.HeightsN, HeightsQ = t.HeightsQ, BiomesN = t.BiomesN, Biomes = t.Biomes, Flags = t.Flags };
            c.Roads.Add(r);
            return c;
        }

        /// <summary>Real data: every drawn piece lies exactly lift above the rendered surface (vertices and edge
        /// midpoints; bridges at or above it), triangles face up, and every piece cut by a tile edge meets its
        /// continuation in the neighbouring tile vertex for vertex along the border.</summary>
        [Test]
        public void SampleRoadsFollowTheRenderedSurfaceAndJoinAcrossTiles()
        {
            var o = new RoadOptions();
            int pieces = 0, joins = 0, triangles = 0, looseJoins = 0;
            double worst = 0, worstJoin = 0;
            var m = new MeshData();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var s = new TileHeightSampler(t, 2);
                m.Clear();
                int drawn = RoadMesher.Build(t, s, o, m);
                int expected = 0;
                foreach (RoadRecord r in t.Roads)
                    if (RoadMesher.IsDrawn(r, o)) expected++;
                Assert.That(drawn, Is.EqualTo(expected), id.ToString());
                pieces += drawn;
                triangles += m.TriangleCount;
                MeshingChecks.AssertWellFormed(m, id.ToString());
                for (int tri = 0; tri < m.TriangleCount; tri++)
                {
                    double fx, fy, fz;
                    MeshingChecks.Facet(m, tri, out fx, out fy, out fz);
                    if (!(fy > 0)) Assert.Fail(id + ": road triangle " + tri + " does not face up");
                }
            }

            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var s = new TileHeightSampler(t, 2);
                foreach (RoadRecord r in t.Roads)
                {
                    if (!RoadMesher.IsDrawn(r, o)) continue;
                    bool bridge = (r.Flags & RoadFlags.Bridge) != 0;
                    var pm = new MeshData();
                    RoadMesher.Build(OnlyRoad(t, r), s, o, pm);
                    float lift = RoadMesher.LiftOf(r, o);
                    for (int tri = 0; tri < pm.TriangleCount; tri++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            int a = pm.Indices[3 * tri + k], b = pm.Indices[3 * tri + (k + 1) % 3];
                            double mx = 0.5 * (pm.Positions[3 * a] + pm.Positions[3 * b]), mz = 0.5 * (pm.Positions[3 * a + 2] + pm.Positions[3 * b + 2]);
                            double my = 0.5 * (pm.Positions[3 * a + 1] + pm.Positions[3 * b + 1]);
                            float ha, hm;
                            s.TryHeightClamped(id.X0 + pm.Positions[3 * a], id.Z0 + pm.Positions[3 * a + 2], out ha);
                            s.TryHeightClamped(id.X0 + mx, id.Z0 + mz, out hm);
                            double va = pm.Positions[3 * a + 1] - ha - lift, vm = my - hm - lift;
                            if (bridge)
                            {
                                if (va < -1e-3) Assert.Fail(id + " bridge " + r.OsmWayId + " below the lifted terrain by " + va);
                                continue;
                            }
                            worst = Math.Max(worst, Math.Max(Math.Abs(va), Math.Abs(vm)));
                            if (Math.Abs(va) > 1e-3 || Math.Abs(vm) > 1e-3)
                                Assert.Fail(id + " way " + r.OsmWayId + ": " + va + " / " + vm + " m off the lifted surface");
                        }
                    }

                    if (!r.HasNextContext) continue;
                    int n = r.PointCount;
                    int cx = r.Points[2 * (n - 2)], cz = r.Points[2 * (n - 2) + 1];
                    TileId nb;
                    int ncx = cx, ncz = cz;
                    bool vertical;
                    float at, nat;
                    if (cx == 102400 || cx == 0)
                    {
                        if (cx == 0 && id.Tx == 0) continue;
                        nb = new TileId(10, id.Tx + (cx == 0 ? -1 : 1), id.Ty);
                        ncx = cx == 0 ? 102400 : 0;
                        vertical = true;
                        at = cx / 100f;
                        nat = ncx / 100f;
                    }
                    else if (cz == 102400 || cz == 0)
                    {
                        if (cz == 0 && id.Ty == 0) continue;
                        nb = new TileId(10, id.Tx, id.Ty + (cz == 0 ? -1 : 1));
                        ncz = cz == 0 ? 102400 : 0;
                        vertical = false;
                        at = cz / 100f;
                        nat = ncz / 100f;
                    }
                    else continue;
                    if (!StreamingSampleRegion.Pack.Contains(nb)) continue;
                    TileData tn = StreamingSampleRegion.Tile(nb);
                    RoadRecord next = null;
                    foreach (RoadRecord q in tn.Roads)
                        if (q.OsmWayId == r.OsmWayId && q.HasPrevContext && q.Points[2] == ncx && q.Points[3] == ncz) next = q;
                    if (next == null || !RoadMesher.IsDrawn(next, o)) continue;
                    var nm = new MeshData();
                    var sn = new TileHeightSampler(tn, 2);
                    RoadMesher.Build(OnlyRoad(tn, next), sn, o, nm);
                    // Draped: both meshes have the cut point on the border at the same height.
                    double along = vertical ? id.Z0 + cz / 100.0 : id.X0 + cx / 100.0;
                    double[] ca = BorderVertices(pm, id, vertical, at).Find(e => Math.Abs(e[0] - along) < 1e-3);
                    double[] cb = BorderVertices(nm, nb, vertical, nat).Find(e => Math.Abs(e[0] - along) < 1e-3);
                    Assert.That(ca, Is.Not.Null, id + " way " + r.OsmWayId + ": cut point not on the border");
                    Assert.That(cb, Is.Not.Null, nb + " way " + r.OsmWayId + ": cut point not on the border");
                    Assert.That(ca[1], Is.EqualTo(cb[1]).Within(2e-3), id + " -> " + nb + " way " + r.OsmWayId);
                    // Plain cross-sections: A's last section is B's first, vertex for vertex, on the border line.
                    var qa = new MeshData();
                    var qb = new MeshData();
                    RoadMesher.Build(OnlyRoad(t, r), new PlainSampler(s), o, qa);
                    RoadMesher.Build(OnlyRoad(tn, next), new PlainSampler(sn), o, qb);
                    int last = qa.VertexCount - 3;
                    for (int k = 0; k < 3; k++)
                    {
                        double dx = id.X0 + qa.Positions[3 * (last + k)] - (nb.X0 + qb.Positions[3 * k]);
                        double dz = id.Z0 + qa.Positions[3 * (last + k) + 2] - (nb.Z0 + qb.Positions[3 * k + 2]);
                        double dy = qa.Positions[3 * (last + k) + 1] - qb.Positions[3 * k + 1];
                        double d = Math.Max(Math.Abs(dx), Math.Max(Math.Abs(dy), Math.Abs(dz)));
                        worstJoin = Math.Max(worstJoin, d);
                        if (d > 2e-3) looseJoins++;
                        float c = vertical ? qa.Positions[3 * (last + k)] : qa.Positions[3 * (last + k) + 2];
                        Assert.That(c, Is.EqualTo(at).Within(1e-3), "cut section on the border line");
                    }
                    joins++;
                }
            }
            TestContext.WriteLine("pieces {0}, road triangles {1}, joins checked {2}, worst offset {3:0.0000} m, worst join {4:0.0000} m, joins over 2 mm {5}",
                                  pieces, triangles, joins, worst, worstJoin, looseJoins);
            Assert.That(joins, Is.GreaterThan(1000));
            // Identical but for the centimetre rounding of an intermediate cut point on a very short segment.
            Assert.That(worstJoin, Is.LessThan(0.01), "cut sections of neighbouring tiles coincide");
            Assert.That(looseJoins, Is.LessThanOrEqualTo(joins / 100));
        }
    }
}
