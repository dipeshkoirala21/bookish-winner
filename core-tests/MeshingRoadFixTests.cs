using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The roads package's review fixes (W2 detail pass): hill roads hug the slope instead of standing on causeways, the
    /// exported drawn-surface query matches the mesh, parallel carriageways and mapped sidewalks do not overlap, urban
    /// arterials keep their footpaths, junction caps share their cut edges (positions and colours) with the ribbons, the
    /// drawn width is continuous at two-piece nodes, roundabouts get footpaths round the circle and nose islands between
    /// one-way pairs, police podiums are left to the ornaments package, and building passages and corridor shifts.
    /// </summary>
    public class MeshingRoadFixTests
    {
        private static readonly TileId A = new TileId(10, 516, 161);

        private static RoadRecord Road(ulong way, RoadClass c, RoadFlags flags, ulong widthCm, params int[] pointsCm)
        {
            return new RoadRecord { OsmWayId = way, RoadClass = c, Surface = Surface.Asphalt, Flags = flags, WidthCm = widthCm, Points = pointsCm };
        }

        private static RoadAttrRecord Attr(AreaType area, params int[] corridorDm)
        {
            return new RoadAttrRecord { Area = area, CorridorDm = corridorDm };
        }

        private static IEnumerable<TileData> LeafTiles()
        {
            foreach (TileId id in StreamingSampleRegion.TilesAt(10).OrderBy(i => i.Key))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t != null && t.Roads.Count > 0) yield return t;
            }
        }

        private static TileData Leaf(int x, int y)
        {
            return StreamingSampleRegion.Tile(new TileId(10, x, y));
        }

        // -------------------------------------------------------------------------------------------------------
        // Hill roads (review finding 1)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A primary along the contour of a 70 % hillside (the Pasang Lhamu Highway case: the terrain cross slope about 80 %)
        /// leans with the slope: its centre stays within the band of the lifted terrain and neither edge stands more than a
        /// few centimetres off it, so there is no causeway; no up-facing vertex is buried or floats.
        /// </summary>
        [Test]
        public void HillRoadsHugASteepCrossSlope()
        {
            Func<double, double, double> f = (x, z) => 1500 + 0.7 * (z - (A.Z0 + 500)) + 2 * Math.Sin((x - A.X0) / 90.0);
            TileData t = MeshingChecks.SyntheticTile(A, f, 513, Biome.HillTerraces);
            t.Roads.Add(Road(1, RoadClass.Primary, RoadFlags.None, 0, 10000, 50000, 40000, 50600, 90000, 49600));
            t.RoadAttrs.Add(Attr(AreaType.Hill));
            var h = new TileHeightSampler(t, 1);
            var o = new RoadOptions();
            var m = new MeshData();
            Assert.That(RoadMesher.Build(t, h, o, m), Is.EqualTo(1));
            MeshingChecks.AssertWellFormed(m, "hill road");
            RoadGrade g = RoadGrade.For(t, h, o);
            RoadLayout layout = RoadLayout.For(t);
            RoadCentreline c = layout.Centres[0];
            RoadWidthProfile prof = layout.Profiles[0];
            float lift = g.LiftOf(0);
            double worstCentre = 0, worstEdge = 0, bank = 0;
            for (double s = 1; s < c.RawLengthM - 1; s += 1)
            {
                double x, z, tx, tz;
                c.At(s, out x, out z, out tx, out tz);
                double ux = -tz, uz = tx;
                float shift = prof.ShiftAt(s), half = 0.5f * prof.DrawnAt(s);
                double cx = x + ux * shift, cz = z + uz * shift;
                double dc = g.SurfaceY(0, s, shift, cx, cz) - (g.Terrain(cx, cz) + lift);
                worstCentre = Math.Max(worstCentre, Math.Abs(dc));
                for (int side = -1; side <= 1; side += 2)
                {
                    double off = shift + side * half, ex = x + ux * off, ez = z + uz * off;
                    double de = g.SurfaceY(0, s, off, ex, ez) - (g.Terrain(ex, ez) + lift);
                    worstEdge = Math.Max(worstEdge, de);
                    Assert.That(de, Is.GreaterThanOrEqualTo(-RoadGrade.MaxBuryM - 2e-3), "edge buried at " + s);
                }
                bank = Math.Max(bank, Math.Abs(g.RowAt(0, s).B));
            }
            TestContext.Progress.WriteLine("hill road: centre within " + worstCentre.ToString("0.000") + " m, highest edge " + worstEdge.ToString("0.000") +
                                           " m over the lifted terrain, bank up to " + (100 * bank).ToString("0") + " %");
            // The band of the smoothed profile (the synthetic DEM is quantised, so a few centimetres of steps are ironed out).
            Assert.That(worstCentre, Is.LessThanOrEqualTo(RoadGrade.BandM + RoadGrade.MaxBuryM), "the centre stays on the lifted terrain");
            Assert.That(worstEdge, Is.LessThanOrEqualTo(RoadGrade.BandM + RoadGrade.MaxBuryM + 0.05), "no causeway above the downhill side");
            Assert.That(bank, Is.GreaterThan(0.6), "the road leans with the 70 % slope");
            // Every up-facing vertex of the ribbon (carriageway, shoulders) lies within half a metre of the lifted terrain.
            for (int v = 0; v < m.VertexCount; v++)
            {
                if (m.Normals[3 * v + 1] < 0.5f) continue;
                float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                Assert.That(Math.Abs(y - (g.Terrain(x, z) + lift)), Is.LessThan(0.5), "vertex " + v + " off the hillside");
            }
        }

        /// <summary>
        /// Sample-wide: outside decks and synthetic bridges almost every drawn carriageway centre stays within the profile
        /// band of the lifted terrain, nearly all within the band plus the dish allowance of a concave cross-section, none
        /// half a metre above it, and no carriageway edge as much as 0.8 m (the review measured 16 roads above 2 m, the
        /// Pasang Lhamu Highway at 9.4 m).
        /// </summary>
        [Test]
        public void NoRoadFloatsAboveTheTerrainOnTheSample()
        {
            long samples = 0, overBand = 0, overDish = 0;
            double worstCentre = 0, worstEdge = 0;
            string where = "";
            foreach (TileData t in LeafTiles())
            {
                var h = new TileHeightSampler(t, 2);
                RoadGrade g = RoadGrade.For(t, h, new RoadOptions());
                RoadLayout lay = RoadLayout.For(t);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadRecord r = t.Roads[i];
                    if (!g.Has(i) || !lay.DrawsRibbon(i) || g.IsAbsolute(i) || (r.Flags & RoadFlags.Bridge) != 0 || g.IsLowered(i)) continue;
                    RoadCentreline c = lay.Centres[i];
                    RoadWidthProfile prof = lay.Profiles[i];
                    float lift = g.LiftOf(i);
                    for (double s = 1; s < c.RawLengthM - 1; s += 2)
                    {
                        if (g.OnDeck(i, s)) continue;
                        double x, z, tx, tz;
                        c.At(s, out x, out z, out tx, out tz);
                        double ux = -tz, uz = tx;
                        float shift = prof.ShiftAt(s), half = 0.5f * prof.DrawnAt(s);
                        double cx = x + ux * shift, cz = z + uz * shift;
                        double dc = g.SurfaceY(i, s, shift, cx, cz) - (g.Terrain(cx, cz) + lift);
                        samples++;
                        if (dc > RoadGrade.BandM + 0.03) overBand++;
                        if (dc > RoadGrade.BandM + RoadGrade.MaxDishM) overDish++;
                        if (dc > worstCentre)
                        {
                            worstCentre = dc;
                            where = t.Tile + " road " + i + " at " + s.ToString("0");
                        }
                        for (int side = -1; side <= 1; side += 2)
                        {
                            double off = shift + side * half, ex = x + ux * off, ez = z + uz * off;
                            worstEdge = Math.Max(worstEdge, g.SurfaceY(i, s, off, ex, ez) - (g.Terrain(ex, ez) + lift));
                        }
                    }
                }
            }
            TestContext.Progress.WriteLine("float: " + samples + " centre samples, " + (100.0 * overBand / samples).ToString("0.00") +
                                           " % over the band, worst centre " + worstCentre.ToString("0.00") + " m (" + where + "), worst edge " +
                                           worstEdge.ToString("0.00") + " m");
            // The band (smoothing) and the dish allowance of concave cross-sections; terrain dips between the profile's
            // 2 m stations add a few centimetres more at single points.
            Assert.That(samples, Is.GreaterThan(100000));
            Assert.That((double)overBand / samples, Is.LessThan(0.06), "centres more than the band above the lifted terrain");
            Assert.That((double)overDish / samples, Is.LessThan(0.005), "centres above the band and the dish allowance");
            Assert.That(worstCentre, Is.LessThan(0.5), where);
            Assert.That(worstEdge, Is.LessThan(0.8));
        }

        // -------------------------------------------------------------------------------------------------------
        // The drawn surface for physics and traffic (review finding 2)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// <see cref="RoadSurfaceQuery"/> reports the height the mesh is drawn at: every up-facing asphalt vertex of the busy
        /// sample tiles (ribbons, caps, rings) is found on a carriageway at its drawn height (to the ribbon's row tolerance).
        /// The §4.2 width lanes, access masks and the stage-one driving index still follow
        /// (<see cref="RoadWidthProfile.Width"/>) lies within the drawn width except beside unconnected parallel pieces (the
        /// one-way pairs of Durbar Marg and Jamal, the Ring Road's service carriageways: the drawn edges stop at the shared
        /// gap, the access of the piece does not change) and at a few knees to a narrower piece; the open issue in
        /// docs/research/w2/ref_roads.md section 8 moves lanes and the driving index to this query.
        /// </summary>
        [Test]
        public void DrawnSurfaceQueryMatchesTheMesh()
        {
            long vertices = 0, missed = 0, off = 0, samples = 0, narrower = 0;
            double worst = 0;
            foreach (TileData t in new[] { Leaf(517, 160), Leaf(516, 161), Leaf(514, 159), Leaf(515, 164) })
            {
                var h = new TileHeightSampler(t, 2);
                var o = new RoadOptions();
                var m = new MeshData();
                RoadMesher.Build(t, h, o, m);
                RoadSurfaceQuery q = RoadSurfaceQuery.For(t, h, o);
                Assert.That(RoadSurfaceQuery.For(t, h, o), Is.SameAs(q), "cached on the sampler");
                RoadLayout lay = RoadLayout.For(t);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadWidthProfile p = lay.Profiles[i];
                    if (!RoadMesher.IsDrawn(t, i, o)) continue;
                    for (int k = 0; k < p.Count; k++)
                    {
                        samples++;
                        if (p.Drawn[k] < p.Width[k] - 0.01f) narrower++;
                    }
                }
                for (int v = 0; v < m.VertexCount; v++)
                {
                    if (m.Normals[3 * v + 1] < 0.97f || (int)Math.Round(m.Uv0[2 * v]) != (int)MaterialChannel.Asphalt) continue;
                    float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                    if (x < 1 || z < 1 || x > t.Tile.Size - 1 || z > t.Tile.Size - 1) continue;
                    vertices++;
                    RoadSurfaceHit hit;
                    if (!q.TrySample(t.Tile.X0 + x, t.Tile.Z0 + z, y + 0.2f, out hit))
                    {
                        missed++;
                        continue;
                    }
                    // Where a footpath or median overlaps another piece's carriageway (junctions without a cap) the higher,
                    // visible surface is reported.
                    if (!hit.OnCarriageway) continue;
                    double d = Math.Abs(hit.Y - y);
                    worst = Math.Max(worst, d);
                    if (d > RibbonMesher.RowToleranceM + 0.02) off++;
                }
            }
            TestContext.Progress.WriteLine("surface query: " + vertices + " asphalt vertices, " + missed + " not found, " + off +
                                           " off by more than 7 cm (worst " + worst.ToString("0.000") + " m); drawn narrower than the 4.2 width at " +
                                           narrower + " of " + samples + " width samples");
            Assert.That(vertices, Is.GreaterThan(20000));
            Assert.That((double)missed / vertices, Is.LessThan(0.01));
            Assert.That((double)off / vertices, Is.LessThan(0.02));
            Assert.That((double)narrower / samples, Is.LessThan(0.1));
        }

        /// <summary>The query on a synthetic street: carriageway at the profile, raised footpath beside it at kerb height,
        /// nothing beyond; and a lowered underpass reports where the terrain must be cut.</summary>
        [Test]
        public void DrawnSurfaceQueryOnASyntheticStreetAndUnderpass()
        {
            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => 1300.0, 129);
            t.Roads.Add(Road(5, RoadClass.Primary, RoadFlags.None, 0, 10000, 50000, 90000, 50000));
            t.RoadAttrs.Add(Attr(AreaType.Urban, 200, 200, 200, 200, 200));
            t.Roads.Add(Road(6, RoadClass.Secondary, RoadFlags.None, 0, 30000, 10000, 30000, 90000));
            t.RoadAttrs.Add(Attr(AreaType.Rural));
            t.RoadStructures.Add(RoadStructureRecord.Absent);
            t.RoadStructures.Add(new RoadStructureRecord { Kind = RoadStructureKind.Underpass, Flags = RoadStructureFlags.CarAccessible, ClearanceM = 3.5f });
            var h = new TileHeightSampler(t, 2);
            var o = new RoadOptions { JunctionCaps = false };
            RoadSurfaceQuery q = RoadSurfaceQuery.For(t, h, o);
            RoadLayout lay = RoadLayout.For(t);
            RoadWidthProfile p = lay.Profiles[0];
            float half = 0.5f * p.DrawnAt(200), foot = p.Sample(p.FootLeft, 200);
            Assert.That(foot, Is.GreaterThan(1f), "an urban primary with room keeps its footpath");
            RoadSurfaceHit hit;
            double gx = t.Tile.X0 + 600, gz = t.Tile.Z0 + 500;
            Assert.That(q.TrySample(gx, gz, out hit), Is.True);
            Assert.That(hit.OnCarriageway && hit.Road == 0, Is.True);
            RoadGrade g = RoadGrade.For(t, h, o);
            float ground = g.Terrain(600, 500);
            Assert.That(hit.Y, Is.EqualTo(ground + g.LiftOf(0)).Within(0.02));
            Assert.That(q.TrySample(gx, gz + half + 0.5 * foot, out hit) && hit.OnFootpath, Is.True, "the footpath");
            Assert.That(hit.Y, Is.EqualTo(ground + g.LiftOf(0) + o.KerbHeightM).Within(0.08));
            Assert.That(q.TrySample(gx, gz + half + foot + 1.0, out hit), Is.False, "beyond the footpath");
            // The underpass is lowered by 2 m: the terrain above its middle must be cut.
            Assert.That(q.HasLowered, Is.True);
            float cut;
            Assert.That(q.TryCut(t.Tile.X0 + 300, t.Tile.Z0 + 350, out cut), Is.True);
            Assert.That(cut, Is.LessThan(ground - 1.5f));
            Assert.That(q.TryCut(gx, gz, out cut), Is.False);
            var spans = new List<double>();
            Assert.That(q.LoweredSpans(spans), Is.EqualTo(1));
            Assert.That(spans[0], Is.EqualTo(1));
        }

        // -------------------------------------------------------------------------------------------------------
        // Side by side (review findings 3 and 4)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Two one-way carriageways of a divided street mapped 10 m apart (no shared node) stop at their share of the gap
        /// (their widened carriageways would overlap by about 2 m); a sidewalk mapped along the primary becomes its
        /// footpath (absorbed, not drawn as a second ribbon), and a footway crossing it is not drawn over the asphalt.
        /// </summary>
        [Test]
        public void ParallelCarriagewaysAndMappedSidewalksDoNotOverlap()
        {
            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => 1300.0, 129);
            t.Roads.Add(Road(11, RoadClass.Primary, RoadFlags.Oneway, 800, 10000, 50000, 90000, 50000));
            t.Roads.Add(Road(12, RoadClass.Primary, RoadFlags.Oneway, 800, 90000, 51000, 10000, 51000));
            t.Roads.Add(Road(13, RoadClass.Footway, RoadFlags.None, 0, 15000, 49560, 85000, 49560));
            t.Roads.Add(Road(14, RoadClass.Footway, RoadFlags.None, 0, 60000, 46000, 60000, 49000, 60000, 53000));
            for (int i = 0; i < 4; i++) t.RoadAttrs.Add(Attr(AreaType.Urban));
            RoadLayout lay = RoadLayout.For(t);
            RoadWidthProfile a = lay.Profiles[0], b = lay.Profiles[1];
            for (double s = 20; s < 60; s += 5)
            {
                // Road 0 runs east at z = 500 (left = north), road 1 west at z = 510 (left = south): the facing sides are
                // road 0's left and road 1's left.
                float ea = a.ShiftAt(s) + 0.5f * a.DrawnAt(s), eb = b.ShiftAt(s) + 0.5f * b.DrawnAt(s);
                float fa = a.Sample(a.FootLeft, s), fb = b.Sample(b.FootLeft, s);
                Assert.That(ea + Math.Max(fa, 0f) + eb + Math.Max(fb, 0f), Is.LessThanOrEqualTo(10.05f), "overlap at " + s);
                Assert.That(a.DrawnAt(s), Is.GreaterThanOrEqualTo(RoadClearance.MinCorridorM - 1e-3f));
            }
            // The sidewalk 4.4 m south of the primary is its footpath now.
            Assert.That(lay.InAbsorbed(2, 30), Is.True, "mapped sidewalk absorbed");
            Assert.That(a.Sample(a.FootRight, 30), Is.GreaterThanOrEqualTo(RoadWidthModel.MinFootpathM), "the primary's footpath on that side");
            // The crossing footway is not drawn across the carriageways (z 496-514), but still south of them.
            Assert.That(lay.InAbsorbed(3, 40.0), Is.True, "crossing over the carriageway");
            Assert.That(lay.InAbsorbed(3, 50.0), Is.True, "crossing over the other carriageway");
            Assert.That(lay.InAbsorbed(3, 10.0), Is.False);
            var m = new MeshData();
            RoadMesher.Build(t, new TileHeightSampler(t, 2), new RoadOptions(), m);
            MeshingChecks.AssertWellFormed(m, "side by side");
            // No up-facing footway (Flagstone at the footway lift) triangle over the primary's carriageway.
            RoadCorridorIndex idx = RoadCorridorIndex.ForTile(t);
            Assert.That(idx.SignedDistance(t.Tile.X0 + 500, t.Tile.Z0 + 505), Is.LessThan(0), "between the carriageways is corridor");
        }

        /// <summary>
        /// Sample-wide: mapped sidewalks are no longer drawn across the carriageways (the review measured 5.1 km of footway
        /// centrelines inside drawn carriageways) and carriageway edges of unconnected pieces rarely lie inside another
        /// carriageway (3.5 km before; what remains are pairs whose width tags exceed their mapped spacing at bends, where
        /// the two pieces' fillets differ).
        /// </summary>
        [Test]
        public void UnconnectedCarriagewaysAndSidewalksBarelyOverlapOnTheSample()
        {
            double footInside = 0, motorEdges = 0, motorInside = 0;
            foreach (TileData t in LeafTiles())
            {
                RoadLayout lay = RoadLayout.For(t);
                int n = t.Roads.Count;
                var nodes = new HashSet<long>[n];
                var box = new double[n, 4];
                for (int i = 0; i < n; i++)
                {
                    nodes[i] = new HashSet<long>();
                    RoadRecord r = t.Roads[i];
                    for (int k = 0; k < r.PointCount; k++) nodes[i].Add((long)r.Points[2 * k] << 32 ^ (uint)r.Points[2 * k + 1]);
                    box[i, 0] = box[i, 1] = double.MaxValue;
                    box[i, 2] = box[i, 3] = double.MinValue;
                    RoadCentreline c = lay.Centres[i];
                    if (c == null) continue;
                    for (int k = 0; k < c.Count; k++)
                    {
                        box[i, 0] = Math.Min(box[i, 0], c.X[k]);
                        box[i, 1] = Math.Min(box[i, 1], c.Z[k]);
                        box[i, 2] = Math.Max(box[i, 2], c.X[k]);
                        box[i, 3] = Math.Max(box[i, 3], c.Z[k]);
                    }
                }
                for (int i = 0; i < n; i++)
                {
                    RoadRecord ri = t.Roads[i];
                    RoadCentreline ci = lay.Centres[i];
                    if (ci == null || !RoadMesher.IsDrawn(t, i, null) || !lay.DrawsRibbon(i)) continue;
                    bool foot = RoadWidthModel.IsFootClass(ri.RoadClass);
                    for (double s = 10; s < ci.RawLengthM - 10; s += 2)
                    {
                        if (lay.InGap(i, s)) continue;
                        double x, z, tx, tz;
                        ci.At(s, out x, out z, out tx, out tz);
                        RoadWidthProfile pi = lay.Profiles[i];
                        double hi = 0.5 * pi.DrawnAt(s), sh = pi.ShiftAt(s);
                        double[] offs = foot ? new[] { 0.0 } : new[] { sh + hi - 0.3, sh - hi + 0.3 };
                        foreach (double o in offs)
                        {
                            double px = x - tz * o, pz = z + tx * o;
                            if (!foot) motorEdges += 2;
                            for (int j = 0; j < n; j++)
                            {
                                if (j == i) continue;
                                RoadRecord rj = t.Roads[j];
                                RoadCentreline cj = lay.Centres[j];
                                if (RoadWidthModel.IsFootClass(rj.RoadClass) || cj == null || !RoadMesher.IsDrawn(t, j, null) || !lay.DrawsRibbon(j)) continue;
                                if (px < box[j, 0] - 20 || px > box[j, 2] + 20 || pz < box[j, 1] - 20 || pz > box[j, 3] + 20) continue;
                                if (nodes[i].Overlaps(nodes[j]) || ri.Layer != rj.Layer) continue;
                                if (((ri.Flags | rj.Flags) & RoadFlags.Bridge) != 0) continue;
                                int seg;
                                double along, lat, d;
                                if (!cj.Nearest(px, pz, out seg, out along, out lat, out d)) continue;
                                if (along < 10 || along > cj.RawLengthM - 10 || lay.InGap(j, along)) continue;
                                RoadWidthProfile pj = lay.Profiles[j];
                                if (Math.Abs(lat - pj.ShiftAt(along)) >= 0.5 * pj.DrawnAt(along) - 0.2) continue;
                                if (foot) footInside += 2;
                                else motorInside += 2;
                                break;
                            }
                        }
                    }
                }
            }
            TestContext.Progress.WriteLine("overlap: footway centres inside carriageways " + footInside.ToString("0") + " m, motor edges inside unconnected carriageways " +
                                           motorInside.ToString("0") + " m of " + motorEdges.ToString("0") + " m");
            Assert.That(footInside, Is.LessThan(100), "sidewalks drawn across carriageways");
            Assert.That(motorInside / motorEdges, Is.LessThan(1e-3), "unconnected carriageways overlapping");
        }

        /// <summary>
        /// Footpaths are reserved before the carriageway widens (review finding 4): an urban primary in a 17 m corridor keeps
        /// its class footpaths on both sides at their full width and draws its carriageway between them, never under the
        /// real width; and on the sample, where an urban primary's or secondary's corridor holds the real width and both
        /// nominal footpaths, they are drawn (the review measured 43 % and 18 % against 99 % and 87 % nominal).
        /// </summary>
        [Test]
        public void UrbanArterialsWithRoomKeepTheirFootpaths()
        {
            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => 1300.0, 129);
            t.Roads.Add(Road(21, RoadClass.Primary, RoadFlags.None, 0, 10000, 50000, 90000, 50000));
            t.RoadAttrs.Add(Attr(AreaType.Urban, 170, 170, 170, 170, 170));
            RoadLayout lay = RoadLayout.For(t);
            RoadWidthProfile p = lay.Profiles[0];
            float nl, nr;
            RoadWidthModel.NominalFootpaths(t.Roads[0], lay.Attrs[0], p.RealM, out nl, out nr);
            Assert.That(nl, Is.GreaterThan(0f));
            for (int k = 2; k < p.Count - 2; k++)
            {
                Assert.That(p.FootLeft[k], Is.EqualTo(nl).Within(1e-3), "left footpath kept");
                Assert.That(p.FootRight[k], Is.EqualTo(nr).Within(1e-3), "right footpath kept");
                Assert.That(p.Drawn[k], Is.GreaterThanOrEqualTo(p.RealM - 1e-3f));
                Assert.That(p.Drawn[k] + p.FootLeft[k] + p.FootRight[k], Is.LessThanOrEqualTo(p.Limit[k] + 1e-3f));
            }
            // The sample: urban primaries and secondaries where the corridor has room for both.
            long room = 0, kept = 0;
            foreach (TileData tile in LeafTiles())
            {
                RoadLayout l = RoadLayout.For(tile);
                for (int i = 0; i < tile.Roads.Count; i++)
                {
                    RoadRecord r = tile.Roads[i];
                    if (r.RoadClass != RoadClass.Primary && r.RoadClass != RoadClass.Secondary) continue;
                    if (RoadWidthModel.AreaOf(l.Attrs[i]) != AreaType.Urban || l.Attrs[i].Has(RoadAttrFlags.Dual)) continue;
                    RoadWidthProfile q = l.Profiles[i];
                    float fl, fr;
                    RoadWidthModel.NominalFootpaths(r, l.Attrs[i], q.RealM, out fl, out fr);
                    if (fl <= 0f || fr <= 0f) continue;
                    for (int k = 0; k < q.Count; k++)
                    {
                        // Away from the ends (tile-border cross-sections, knees) and junction caps.
                        double s = k * q.StepM;
                        if (s < 40 || s > q.LengthM - 40 || l.InGap(i, s) || l.InGap(i, s - 10) || l.InGap(i, s + 10)) continue;
                        if (float.IsPositiveInfinity(q.Limit[k]) || q.Limit[k] < q.RealM + 2 * Math.Max(fl, fr) + 0.5f) continue;
                        if (q.Drawn[k] > q.Limit[k] - 2 * Math.Max(fl, fr) + 0.01f) continue; // the rideability floor took it
                        room++;
                        if (q.FootLeft[k] > 0f && q.FootRight[k] > 0f) kept++;
                    }
                }
            }
            TestContext.Progress.WriteLine("footpaths: kept on " + kept + " of " + room + " urban arterial samples with room");
            Assert.That(room, Is.GreaterThan(100));
            Assert.That((double)kept / room, Is.GreaterThan(0.9));
        }

        // -------------------------------------------------------------------------------------------------------
        // Junction caps (review finding 5)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Junction caps share their cut edges with the ribbons: just either side of every arm's cut line the surface is
        /// covered (no hairline slivers of terrain showing through), and every cap vertex on a cut edge has a ribbon vertex
        /// at the same place with the same colour (the lighter crown included), so no darker rectangle or colour seam
        /// shows at junctions.
        /// </summary>
        [Test]
        public void JunctionCapsShareTheirCutEdgesWithTheRibbons()
        {
            int caps = 0, probes = 0, uncovered = 0, edgeVerts = 0, colourSeams = 0;
            foreach (TileData t in new[] { Leaf(517, 160), Leaf(516, 161), Leaf(516, 160) })
            {
                var h = new TileHeightSampler(t, 2);
                var o = new RoadOptions();
                var m = new MeshData();
                RoadMesher.Build(t, h, o, m);
                RoadLayout lay = RoadLayout.For(t);
                // Up-facing triangles in a plan grid.
                var grid = new Dictionary<long, List<int>>();
                for (int tri = 0; tri < m.TriangleCount; tri++)
                {
                    double fx, fy, fz;
                    MeshingChecks.Facet(m, tri, out fx, out fy, out fz);
                    double len = Math.Sqrt(fx * fx + fy * fy + fz * fz);
                    if (len < 1e-12 || fy < 0.5 * len) continue;
                    double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                    for (int k = 0; k < 3; k++)
                    {
                        int v = m.Indices[3 * tri + k];
                        x0 = Math.Min(x0, m.Positions[3 * v]);
                        x1 = Math.Max(x1, m.Positions[3 * v]);
                        z0 = Math.Min(z0, m.Positions[3 * v + 2]);
                        z1 = Math.Max(z1, m.Positions[3 * v + 2]);
                    }
                    for (int j = (int)Math.Floor(z0 / 4); j <= (int)Math.Floor(z1 / 4); j++)
                        for (int i = (int)Math.Floor(x0 / 4); i <= (int)Math.Floor(x1 / 4); i++)
                        {
                            long key = (long)i << 32 ^ (uint)j;
                            List<int> l;
                            if (!grid.TryGetValue(key, out l)) grid[key] = l = new List<int>();
                            l.Add(tri);
                        }
                }
                // Vertex colours by position (1 mm).
                var colours = new Dictionary<long, HashSet<uint>>();
                for (int v = 0; v < m.VertexCount; v++)
                {
                    if (m.Normals[3 * v + 1] < 0.9f) continue;
                    long key = (long)Math.Round(m.Positions[3 * v] * 1000) << 32 ^ (uint)Math.Round(m.Positions[3 * v + 2] * 1000);
                    HashSet<uint> set;
                    if (!colours.TryGetValue(key, out set)) colours[key] = set = new HashSet<uint>();
                    set.Add((uint)(m.Colors[4 * v] << 16 | m.Colors[4 * v + 1] << 8 | m.Colors[4 * v + 2]));
                }
                foreach (JunctionCap cap in lay.Caps)
                {
                    caps++;
                    for (int a = 0; a < cap.ArmRoads.Length; a++)
                    {
                        double rx = cap.PolyX[cap.ArmRightIndex[a]], rz = cap.PolyZ[cap.ArmRightIndex[a]];
                        double lx = cap.PolyX[cap.ArmLeftIndex[a]], lz = cap.PolyZ[cap.ArmLeftIndex[a]];
                        double ex = lx - rx, ez = lz - rz, el = Math.Sqrt(ex * ex + ez * ez);
                        if (el < 0.5) continue;
                        double nx = -ez / el, nz = ex / el;
                        for (int q = 1; q < 40; q++)
                        {
                            double f = q / 40.0;
                            for (int side = -1; side <= 1; side += 2)
                            {
                                double px = rx + ex * f + nx * 0.03 * side, pz = rz + ez * f + nz * 0.03 * side;
                                probes++;
                                if (!Covered(m, grid, px, pz)) uncovered++;
                            }
                        }
                    }
                    for (int k = 0; k < cap.ArmRoads.Length; k++)
                    {
                        foreach (int idx in new[] { cap.ArmRightIndex[k], cap.ArmLeftIndex[k] })
                        {
                            long key = (long)Math.Round(cap.PolyX[idx] * 1000) << 32 ^ (uint)Math.Round(cap.PolyZ[idx] * 1000);
                            HashSet<uint> set;
                            if (!colours.TryGetValue(key, out set)) continue;
                            edgeVerts++;
                            if (set.Count > 1) colourSeams++;
                        }
                    }
                }
            }
            TestContext.Progress.WriteLine("caps: " + caps + ", cut-line probes " + probes + " (" + uncovered + " uncovered), cut-edge vertices " + edgeVerts +
                                           " (" + colourSeams + " with two colours)");
            Assert.That(caps, Is.GreaterThan(20));
            Assert.That((double)uncovered / probes, Is.LessThan(0.002), "terrain showing through along cut lines");
            Assert.That(edgeVerts, Is.GreaterThan(2 * caps));
            Assert.That(colourSeams, Is.EqualTo(0), "colour seams at cut edges");
        }

        private static bool Covered(MeshData m, Dictionary<long, List<int>> grid, double x, double z)
        {
            List<int> l;
            if (!grid.TryGetValue((long)(int)Math.Floor(x / 4) << 32 ^ (uint)(int)Math.Floor(z / 4), out l)) return false;
            foreach (int tri in l)
            {
                int a = m.Indices[3 * tri], b = m.Indices[3 * tri + 1], c = m.Indices[3 * tri + 2];
                double ax = m.Positions[3 * a], az = m.Positions[3 * a + 2], bx = m.Positions[3 * b], bz = m.Positions[3 * b + 2];
                double cx = m.Positions[3 * c], cz = m.Positions[3 * c + 2];
                double d1 = (bx - ax) * (z - az) - (bz - az) * (x - ax), d2 = (cx - bx) * (z - bz) - (cz - bz) * (x - bx), d3 = (ax - cx) * (z - cz) - (az - cz) * (x - cx);
                bool neg = d1 < -1e-9 || d2 < -1e-9 || d3 < -1e-9, pos = d1 > 1e-9 || d2 > 1e-9 || d3 > 1e-9;
                if (!(neg && pos)) return true;
            }
            return false;
        }

        // -------------------------------------------------------------------------------------------------------
        // Width continuity (review finding 6)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Wherever exactly two drawn street pieces end at one node and nothing else passes, both meet at one drawn width
        /// (the review found 24 such joins stepping by more than 3 m, Kanti Path 10 m against 17.5 m): knees share the
        /// width (but for short pieces between a knee and a tile-border cut, whose cut keeps the width both tiles agree on); a
        /// street meeting a bridge deck end is never wider than the deck there. A synthetic 14 m way continued by a
        /// 6 m way tapers down to meet it.
        /// </summary>
        [Test]
        public void DrawnWidthIsContinuousAtTwoPieceNodes()
        {
            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => 1300.0, 129);
            t.Roads.Add(Road(31, RoadClass.Primary, RoadFlags.None, 1400, 10000, 50000, 50000, 50000));
            t.Roads.Add(Road(32, RoadClass.Primary, RoadFlags.None, 600, 50000, 50000, 90000, 52000));
            t.RoadAttrs.Add(Attr(AreaType.Rural));
            t.RoadAttrs.Add(Attr(AreaType.Rural));
            RoadLayout lay = RoadLayout.For(t);
            Assert.That(lay.Knees[1].Has && lay.Knees[2].Has, Is.True, "joined as a knee despite the 9.5 m step");
            RoadWidthProfile a = lay.Profiles[0], b = lay.Profiles[1];
            Assert.That(a.Drawn[a.Count - 1], Is.EqualTo(b.Drawn[0]).Within(1e-3));
            Assert.That(a.Drawn[0], Is.EqualTo(17.5f).Within(1e-3), "the wide way keeps its width away from the join");
            for (int k = 1; k < a.Count; k++)
                Assert.That(Math.Abs(a.Drawn[k] - a.Drawn[k - 1]), Is.LessThanOrEqualTo(a.StepM / RoadLayout.KneeTaperRatio + 1e-3f), "tapered");
            int joins = 0, steps = 0;
            foreach (TileData tile in LeafTiles())
            {
                RoadLayout l = RoadLayout.For(tile);
                var ends = new Dictionary<long, List<int>>();
                var through = new Dictionary<long, int>();
                for (int i = 0; i < tile.Roads.Count; i++)
                {
                    RoadRecord r = tile.Roads[i];
                    if (!RoadMesher.IsDrawn(tile, i, null) || l.Centres[i] == null || !l.DrawsRibbon(i)) continue;
                    int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? r.PointCount - 2 : r.PointCount - 1;
                    for (int k = first; k <= last; k++)
                    {
                        long key = (long)r.Points[2 * k] << 32 ^ (uint)r.Points[2 * k + 1];
                        int c;
                        through.TryGetValue(key, out c);
                        through[key] = c + 1;
                        bool end = k == first && !r.HasPrevContext || k == last && !r.HasNextContext;
                        if (!end) continue;
                        List<int> e;
                        if (!ends.TryGetValue(key, out e)) ends[key] = e = new List<int>();
                        e.Add(i << 1 | (k == first ? 0 : 1));
                    }
                }
                foreach (KeyValuePair<long, List<int>> kv in ends)
                {
                    if (kv.Value.Count != 2 || through[kv.Key] != 2) continue;
                    int ia = kv.Value[0] >> 1, ib = kv.Value[1] >> 1;
                    RoadRecord ra = tile.Roads[ia], rb = tile.Roads[ib];
                    if (RoadWidthModel.IsFootClass(ra.RoadClass) || RoadWidthModel.IsFootClass(rb.RoadClass)) continue;
                    if (l.IsPassage(ia) || l.IsPassage(ib)) continue;
                    RoadWidthProfile pa = l.Profiles[ia], pb = l.Profiles[ib];
                    float wa = (kv.Value[0] & 1) == 0 ? pa.Drawn[0] : pa.Drawn[pa.Count - 1];
                    float wb = (kv.Value[1] & 1) == 0 ? pb.Drawn[0] : pb.Drawn[pb.Count - 1];
                    bool deckA = (ra.Flags & RoadFlags.Bridge) != 0 || l.IsElevated(ia), deckB = (rb.Flags & RoadFlags.Bridge) != 0 || l.IsElevated(ib);
                    joins++;
                    bool ok = deckA == deckB ? Math.Abs(wa - wb) <= 0.5f : deckA ? wb <= wa + 0.01f || Math.Abs(wa - wb) <= 0.5f : wa <= wb + 0.01f || Math.Abs(wa - wb) <= 0.5f;
                    if (!ok)
                    {
                        steps++;
                        TestContext.Progress.WriteLine("  step at " + tile.Tile + " " + ia + "/" + ib + " " + ra.RoadClass + "/" + rb.RoadClass + " " + wa.ToString("0.0") + " vs " +
                                                       wb.ToString("0.0") + " knee " + l.Knees[kv.Value[0]].Has + " decks " + deckA + "/" + deckB + " ctx " +
                                                       (ra.HasPrevContext || ra.HasNextContext) + "/" + (rb.HasPrevContext || rb.HasNextContext) + " ring " +
                                                       l.Attrs[ia].Has(RoadAttrFlags.RingMember) + "/" + l.Attrs[ib].Has(RoadAttrFlags.RingMember));
                    }
                }
            }
            TestContext.Progress.WriteLine("two-piece nodes: " + joins + ", width steps over 0.5 m: " + steps);
            Assert.That(joins, Is.GreaterThan(500));
            // What remains are streets wider than the deck they meet whose other end is a tile-border cut close by (the cut
            // keeps the width both tiles agree on).
            Assert.That(steps, Is.LessThanOrEqualTo(joins / 250), "drawn width steps at two-piece nodes");
        }

        // -------------------------------------------------------------------------------------------------------
        // Roundabouts (review finding 7) and police podiums (finding 8)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The Jamal roundabout (85.3173 E, 27.7100 N): a kerbed paver footpath runs round most of the outer circle between
        /// the entries (built-up rings get one even where an approach has none), and the wedges between its one-way
        /// approach pairs are raised kerbed nose islands (banded paint beyond the circle), not bare ground.
        /// </summary>
        [Test]
        public void RoundaboutsGetFootpathsRoundTheCircleAndNoseIslands()
        {
            double gx, gz;
            WorldFrame.LonLatToGame(85.31732, 27.70997, out gx, out gz);
            TileId id = TileId.At(10, gx, gz);
            TileData t = StreamingSampleRegion.Tile(id);
            RoadLayout lay = RoadLayout.For(t);
            RoadRing ring = lay.Rings.OrderBy(r => Math.Abs(r.X - (gx - id.X0)) + Math.Abs(r.Z - (gz - id.Z0))).First();
            var m = new MeshData();
            RoadMesher.Build(t, new TileHeightSampler(t, 2), new RoadOptions(), m);
            const int Bins = 72;
            var paver = new bool[Bins];
            // The sectors between neighbouring one-way approaches that are not dual carriageways (no median of their own).
            var sectors = new List<double[]>();
            for (int a = 0; a < ring.Arms.Length; a++)
            {
                RingArm aa = ring.Arms[a], bb = ring.Arms[(a + 1) % ring.Arms.Length];
                bool oneway = (t.Roads[aa.Road].Flags & t.Roads[bb.Road].Flags & RoadFlags.Oneway) != 0;
                bool dual = lay.Attrs[aa.Road].Has(RoadAttrFlags.Dual) || lay.Attrs[bb.Road].Has(RoadAttrFlags.Dual);
                double chord = Math.Sqrt(Math.Pow(aa.LeftEdgeX - bb.RightEdgeX, 2) + Math.Pow(aa.LeftEdgeZ - bb.RightEdgeZ, 2));
                if (oneway && !dual && chord <= 8.0) sectors.Add(new[] { aa.Angle, RoadRing.Wrap(bb.Angle - aa.Angle) });
            }
            int paint = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                double dx = m.Positions[3 * v] - ring.X, dz = m.Positions[3 * v + 2] - ring.Z, r = Math.Sqrt(dx * dx + dz * dz);
                int ch = (int)Math.Round(m.Uv0[2 * v]);
                double ang = Math.Atan2(dz, dx);
                int bin = (int)Math.Floor((ang + Math.PI) / (2 * Math.PI) * Bins) % Bins;
                if (ch == (int)MaterialChannel.Flagstone && m.Normals[3 * v + 1] > 0.9f && r > ring.OuterRadiusM + 0.2 && r < ring.OuterRadiusM + 4.0) paver[bin] = true;
                if (ch != (int)MaterialChannel.Paint || r < ring.OuterRadiusM - 0.5 || r > ring.OuterRadiusM + 25) continue;
                foreach (double[] sec in sectors)
                    if (RoadRing.Wrap(ang - sec[0]) <= sec[1]) paint++;
            }
            Assert.That(sectors.Count, Is.GreaterThanOrEqualTo(1), "Jamal has a one-way pair (the southern approaches)");
            // Bins not taken by an entry.
            int free = 0, withPaver = 0;
            for (int b = 0; b < Bins; b++)
            {
                double ang = -Math.PI + (b + 0.5) * 2 * Math.PI / Bins;
                bool entry = false;
                foreach (RingArm arm in ring.Arms)
                {
                    double span = RoadRing.Wrap(arm.AngleLeft - arm.AngleRight);
                    if (RoadRing.Wrap(ang - arm.AngleRight) <= span + 0.15 || RoadRing.Wrap(arm.AngleRight - ang) < 0.15) entry = true;
                }
                if (entry) continue;
                free++;
                if (paver[b]) withPaver++;
            }
            TestContext.Progress.WriteLine("Jamal: footpath round " + withPaver + " of " + free + " free bins, " + paint + " island paint vertices beyond the circle");
            Assert.That(free, Is.GreaterThan(10));
            Assert.That((double)withPaver / free, Is.GreaterThan(0.6), "footpath round the circle");
            Assert.That(paint, Is.GreaterThan(20), "nose islands between the one-way pairs");
        }

        /// <summary>The ornaments package draws the police furniture: by default the road mesh has no podium or umbrella at
        /// police islands; with <see cref="RoadOptions.PolicePodiums"/> on, it does.</summary>
        [Test]
        public void PolicePodiumsAreLeftToTheOrnamentsPackage()
        {
            Assert.That(new RoadOptions().PolicePodiums, Is.False);
            TileData t = null;
            foreach (TileData tile in LeafTiles())
            {
                RoadLayout l = RoadLayout.For(tile);
                if (l.Islands.Any(i => i.PolicePodium && i.Kind != IslandKind.Mini))
                {
                    t = tile;
                    break;
                }
            }
            Assert.That(t, Is.Not.Null, "a police island on the sample");
            Func<MeshData, int> fabric = m =>
            {
                int n = 0;
                for (int v = 0; v < m.VertexCount; v++)
                    if ((int)Math.Round(m.Uv0[2 * v]) == (int)MaterialChannel.Fabric) n++;
                return n;
            };
            var off = new MeshData();
            RoadMesher.Build(t, new TileHeightSampler(t, 2), new RoadOptions(), off);
            var on = new MeshData();
            RoadMesher.Build(t, new TileHeightSampler(t, 2), new RoadOptions { PolicePodiums = true }, on);
            Assert.That(fabric(off), Is.EqualTo(0), "no umbrella by default");
            Assert.That(fabric(on), Is.GreaterThan(0), "the umbrella when asked for");
        }

        // -------------------------------------------------------------------------------------------------------
        // RSTR passages and corridor shifts (review finding 9)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>A building passage (RSTR kind Passage) is drawn as the gateway (its real width, no 4.8 m floor, no
        /// footpaths) and has no clear corridor, so the guard never trims the house over it.</summary>
        [Test]
        public void BuildingPassagesAreGatewaysWithoutCorridors()
        {
            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => 1300.0, 129);
            t.Roads.Add(Road(41, RoadClass.Footway, RoadFlags.None, 180, 40000, 50000, 40000, 53000));
            t.RoadAttrs.Add(Attr(AreaType.OldCore));
            t.RoadStructures.Add(new RoadStructureRecord { Kind = RoadLayout.PassageKind, Flags = RoadStructureFlags.DeckFromTags, ClearanceM = 4.5f });
            RoadLayout lay = RoadLayout.For(t);
            Assert.That(lay.IsPassage(0), Is.True);
            Assert.That(lay.Profiles[0].MaxDrawn, Is.LessThanOrEqualTo(RoadLayout.MaxPassageWidthM));
            Assert.That(lay.Profiles[0].MinDrawn, Is.GreaterThanOrEqualTo(RoadLayout.MinPassageWidthM));
            RoadCorridorIndex idx = RoadCorridorIndex.ForTile(t);
            Assert.That(idx.SignedDistance(t.Tile.X0 + 400, t.Tile.Z0 + 515), Is.EqualTo(RoadCorridorIndex.MaxSearchM), "no corridor over the passage");
            var m = new MeshData();
            Assert.That(RoadMesher.Build(t, new TileHeightSampler(t, 2), new RoadOptions(), m), Is.EqualTo(1), "the gateway strip is drawn");
            for (int v = 0; v < m.VertexCount; v++)
                Assert.That(Math.Abs(m.Positions[3 * v] - 400), Is.LessThan(0.5 * RoadLayout.MaxPassageWidthM + 0.4), "within the gateway");
        }

        /// <summary>A corridor shift (RSTR CorridorShiftCm, here set directly) moves the drawn carriageway, its cut sections
        /// and the clear corridor sideways together, away from the protected footprint.</summary>
        [Test]
        public void CorridorShiftsMoveRibbonAndCorridorTogether()
        {
            TileData t = MeshingChecks.SyntheticTile(A, (x, z) => 1300.0, 129);
            t.Roads.Add(Road(51, RoadClass.Residential, RoadFlags.None, 0, 10000, 50000, 90000, 50000));
            t.RoadAttrs.Add(Attr(AreaType.Rural));
            var shift = new float[200];
            for (int k = 0; k < shift.Length; k++) shift[k] = 2f;
            RoadLayout lay = RoadLayout.WithCorridorShifts(t, new[] { shift });
            Assert.That(RoadLayout.For(t), Is.SameAs(lay));
            RoadWidthProfile p = lay.Profiles[0];
            Assert.That(p.ShiftAt(400), Is.EqualTo(2f).Within(1e-4));
            RoadCut c = lay.DrawnCutAt(t, 0, 400);
            Assert.That(c.Shift, Is.EqualTo(2f).Within(1e-4));
            var m = new MeshData();
            RoadMesher.Build(t, new TileHeightSampler(t, 2), new RoadOptions(), m);
            double zMin = double.MaxValue, zMax = double.MinValue;
            for (int v = 0; v < m.VertexCount; v++)
            {
                if (m.Normals[3 * v + 1] < 0.97f || Math.Abs(m.Positions[3 * v] - 400) > 20) continue;
                zMin = Math.Min(zMin, m.Positions[3 * v + 2]);
                zMax = Math.Max(zMax, m.Positions[3 * v + 2]);
            }
            double half = 0.5 * p.DrawnAt(400);
            Assert.That(zMax, Is.EqualTo(500 + 2 + half).Within(0.05), "left (north) edge moved by the shift");
            Assert.That(zMin, Is.EqualTo(500 + 2 - half).Within(0.05), "right edge moved by the shift");
            RoadCorridorIndex idx = RoadCorridorIndex.ForTile(t);
            Assert.That(idx.SignedDistance(t.Tile.X0 + 400, t.Tile.Z0 + 500 + 2 + half - 0.2), Is.LessThan(0));
            Assert.That(idx.SignedDistance(t.Tile.X0 + 400, t.Tile.Z0 + 500 + 2 - half - 0.3), Is.GreaterThan(0), "the corridor moved away too");
        }
    }
}
