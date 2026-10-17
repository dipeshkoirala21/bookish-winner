using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #9, #14 and #1 (docs/W2_PLAYTEST_1.md): "Roads have been widen, but the actual driving space is
    /// same", "Cannot drive between the houses even if there is a space", "Road is being blocked by some structure". On the
    /// real kathmandu_core tiles streamed as the game streams them (Asan and Indra Chowk, Thamel's edge, Kalanki,
    /// <see cref="RideFeetTests"/>): the wheels treat every drawn carriageway as road out to its drawn edge, a motorbike
    /// reaches that edge, the open ground between drawn houses holds no invisible wall, nothing solid stands on a drawn
    /// road (retaining walls open where a street joins or crosses a cutting), and a motorbike rides every drawn motor road.
    /// The same checks over the whole sample, before and after, are in docs/research/w2/ref_ride.md.
    /// </summary>
    public class DrivableTests
    {
        /// <summary>Drawn houses (the corridor guard's outlines, a dropped house is not drawn) of a tile and its eight
        /// neighbours, game metres, in an 8 m grid.</summary>
        private sealed class Houses
        {
            private readonly Dictionary<long, List<double[]>> _cells = new Dictionary<long, List<double[]>>();
            private readonly Dictionary<long, List<(double[] ring, bool trimmed, bool dropped)>> _orig = new Dictionary<long, List<(double[], bool, bool)>>();

            public Houses(TileData centre)
            {
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    TileData t = StreamingSampleRegion.Tile(new TileId(centre.Tile.Level, centre.Tile.Tx + dx, centre.Tile.Ty + dy));
                    if (t == null || !t.HasDetail) continue;
                    BuildingFootprints fp = BuildingFootprints.For(t, RoadCorridorIndex.ForTile(t));
                    for (int i = 0; i < t.Buildings.Count; i++)
                    {
                        BuildingRecord b = t.Buildings[i];
                        if (b.Rings == null || b.Rings.Length == 0 || b.Rings[0] == null || b.Rings[0].Length < 6) continue;
                        bool dropped = fp.Dropped(i);
                        Index(_orig, (Ring(t, b.Rings[0]), fp.Trimmed(i), dropped));
                        if (dropped) continue;
                        BuildingRecord d = fp.Record(i);
                        if (d.Rings == null || d.Rings.Length == 0 || d.Rings[0] == null || d.Rings[0].Length < 6) continue;
                        double[] ring = Ring(t, d.Rings[0]);
                        foreach (long k in Keys(ring)) Get(_cells, k).Add(ring);
                    }
                }
            }

            private static List<T> Get<T>(Dictionary<long, List<T>> d, long k)
            {
                List<T> l;
                if (!d.TryGetValue(k, out l)) d[k] = l = new List<T>();
                return l;
            }

            private static void Index(Dictionary<long, List<(double[], bool, bool)>> d, (double[] ring, bool trimmed, bool dropped) e)
            {
                foreach (long k in Keys(e.ring)) Get(d, k).Add(e);
            }

            private static IEnumerable<long> Keys(double[] r)
            {
                double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                for (int k = 0; k + 1 < r.Length; k += 2)
                {
                    x0 = Math.Min(x0, r[k]);
                    x1 = Math.Max(x1, r[k]);
                    z0 = Math.Min(z0, r[k + 1]);
                    z1 = Math.Max(z1, r[k + 1]);
                }
                for (long cz = (long)Math.Floor((z0 - 3) / 8); cz <= (long)Math.Floor((z1 + 3) / 8); cz++)
                for (long cx = (long)Math.Floor((x0 - 3) / 8); cx <= (long)Math.Floor((x1 + 3) / 8); cx++)
                    yield return (cx << 32) ^ cz;
            }

            private static long Key(double x, double z)
            {
                return ((long)Math.Floor(x / 8) << 32) ^ (long)Math.Floor(z / 8);
            }

            /// <summary>Inside a drawn house, or within <paramref name="d"/> of its outline.</summary>
            public bool Near(double x, double z, double d)
            {
                List<double[]> l;
                if (!_cells.TryGetValue(Key(x, z), out l)) return false;
                foreach (double[] r in l)
                    if (Inside(r, x, z) || Dist(r, x, z) <= d) return true;
                return false;
            }

            /// <summary>Within <paramref name="d"/> of a mapped (pipeline) house outline, and whether that house was trimmed
            /// or dropped by the guard.</summary>
            public bool NearMapped(double x, double z, double d, out bool trimmedOrDropped)
            {
                trimmedOrDropped = false;
                List<(double[] ring, bool trimmed, bool dropped)> l;
                if (!_orig.TryGetValue(Key(x, z), out l)) return false;
                bool near = false;
                foreach (var e in l)
                {
                    if (!Inside(e.ring, x, z) && Dist(e.ring, x, z) > d) continue;
                    near = true;
                    if (Inside(e.ring, x, z) && (e.trimmed || e.dropped)) trimmedOrDropped = true;
                }
                return near;
            }

            private static double[] Ring(TileData t, int[] pts)
            {
                var r = new double[pts.Length - pts.Length % 2];
                for (int k = 0; k + 1 < pts.Length; k += 2)
                {
                    r[k] = t.Tile.X0 + pts[k] / 100.0;
                    r[k + 1] = t.Tile.Z0 + pts[k + 1] / 100.0;
                }
                return r;
            }

            private static bool Inside(double[] r, double x, double z)
            {
                bool inside = false;
                int n = r.Length / 2;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double xi = r[2 * i], zi = r[2 * i + 1], xj = r[2 * j], zj = r[2 * j + 1];
                    if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
                }
                return inside;
            }

            private static double Dist(double[] r, double x, double z)
            {
                double best = double.MaxValue;
                int n = r.Length / 2;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double ax = r[2 * j], az = r[2 * j + 1], bx = r[2 * i], bz = r[2 * i + 1];
                    double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                    double f = l2 > 0 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / l2)) : 0;
                    double ex = ax + dx * f - x, ez = az + dz * f - z;
                    best = Math.Min(best, Math.Sqrt(ex * ex + ez * ez));
                }
                return best;
            }
        }

        private static readonly Dictionary<TileId, Houses> HouseCache = new Dictionary<TileId, Houses>();

        private static Houses HousesOf(TileData t)
        {
            Houses h;
            if (!HouseCache.TryGetValue(t.Tile, out h)) HouseCache[t.Tile] = h = new Houses(t);
            return h;
        }

        /// <summary>A drawn road kind at a point from the road surface query (null: footpath, median or island).</summary>
        private static bool CarriagewayAt(RoadSurfaceQuery q, double x, double z, float drawnY, out RoadSurfaceHit hit)
        {
            if (!q.TrySample(x, z, drawnY + 0.05f, out hit) || Math.Abs(hit.Y - drawnY) > 0.25f || hit.OnDeck) return false;
            return !hit.OnFootpath && !hit.OnIsland && !hit.OnMedian;
        }

        /// <summary>
        /// #9: on every drawn carriageway, junction cap and shoulder of the feet tiles (1 m grid), the ground query tells the
        /// wheels they are on the road (not "off road" on the biome's surface, as it did over the widened edges and caps
        /// before: 1.02 % of the sample's drawn carriageway, 74 884 m², at a motorbike's dirt top speed), and the surface
        /// it reports is never slower than the road's own.
        /// </summary>
        [Test]
        public void DrawnCarriagewaysAreRoadForTheWheels()
        {
            TileGroundQuery g;
            List<(TileData tile, RideFeetTests.Drawn drawn, TileHeightSampler sampler)> tiles;
            RideFeetTests.Stream(out g, out tiles);
            VehicleSpec bike = VehicleSpec.Motorbike();
            long samples = 0, off = 0, slower = 0;
            string where = "";
            foreach ((TileData t, RideFeetTests.Drawn d, TileHeightSampler h) in tiles)
            {
                RoadSurfaceQuery q = RoadSurfaceQuery.For(t, h, new RoadOptions());
                for (double lz = 0.31; lz < t.Tile.Size; lz += 1.0)
                for (double lx = 0.17; lx < t.Tile.Size; lx += 1.0)
                {
                    double x = t.Tile.X0 + lx, z = t.Tile.Z0 + lz;
                    List<float> ys = d.Levels(x, z);
                    if (ys.Count == 0) continue;
                    float y = ys[ys.Count - 1];
                    RoadSurfaceHit rh;
                    if (!CarriagewayAt(q, x, z, y, out rh)) continue;
                    GroundSample s;
                    if (!g.TrySample(x, z, y + 0.05f, out s) || s.OnDeck) continue;
                    samples++;
                    if (!s.OnRoad && !s.OnFootpath)
                    {
                        off++;
                        if (where.Length == 0) where = t.Tile + " at " + lx.ToString("0.0") + ", " + lz.ToString("0.0");
                    }
                    float own = rh.Road >= 0 ? bike.TopSpeedFactorOf(SurfaceGroups.Of(t.Roads[rh.Road].Surface), 0f) : 1f;
                    if (bike.TopSpeedFactorOf(s.Surface, 0f) < own - 0.01f) slower++;
                }
            }
            TestContext.WriteLine("drawn carriageway samples " + samples + ", off road for the wheels " + off + " " + where + ", slower than the road's own surface " + slower);
            Assert.That(samples, Is.GreaterThan(150000));
            Assert.That(off, Is.EqualTo(0), "the drawn carriageway is road for the wheels: " + where);
            Assert.That(slower, Is.LessThanOrEqualTo(samples / 200), "the wheels get the road's own surface");
        }

        /// <summary>
        /// #9: every 4 m along every drawn ground road of the feet tiles, a motorbike body moved from the drawn centre outward
        /// on both sides reaches the end of the drawn road surface (carriageway, kerb, footpath, cap; a drawn house wall ends
        /// it), stopped neither by a solid nor by a step it cannot climb. Whole sample before: 2 734 sides lost 0.3 m or more
        /// (13.4 km of drawn half-width).
        /// </summary>
        [Test]
        public void TheDrivableWidthIsTheDrawnWidth()
        {
            TileGroundQuery g;
            List<(TileData tile, RideFeetTests.Drawn drawn, TileHeightSampler sampler)> tiles;
            RideFeetTests.Stream(out g, out tiles);
            CollisionBody bike = CollisionBody.For(VehicleSpec.Motorbike());
            CollisionBody body = CollisionBody.Upright(bike.Radius, bike.HeightM);
            long sides = 0, narrowed = 0;
            double drawnSum = 0, lostSum = 0;
            var examples = new List<string>();
            var loaded = new HashSet<TileId>(tiles.Select(e => e.tile.Tile)); // the streamed tiles (the ground ends at their edge)
            foreach ((TileData t, RideFeetTests.Drawn d, TileHeightSampler _) in tiles)
            {
                RoadLayout layout = RoadLayout.For(t);
                Houses houses = HousesOf(t);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadRecord r = t.Roads[i];
                    if (!RoadMesher.IsDrawn(t, i, null) || layout.IsPassage(i) || RoadWidthModel.IsFootClass(r.RoadClass) || !layout.DrawsRibbon(i)) continue;
                    if (t.RoadStructureOf(i).IsElevated) continue; // decks and their railings are the bridges package's
                    RoadCentreline c = layout.Centres[i];
                    RoadWidthProfile p = layout.Profiles[i];
                    if (c == null || c.Count < 2) continue;
                    for (double s = 2.0; s < p.LengthM - 2.0; s += 4.0)
                    {
                        double cx, cz, tx, tz;
                        c.At(s, out cx, out cz, out tx, out tz);
                        double nx = -tz, nz = tx, shift = p.ShiftAt(s);
                        cx = t.Tile.X0 + cx + nx * shift;
                        cz = t.Tile.Z0 + cz + nz * shift;
                        List<float> ys = d.Levels(cx, cz);
                        GroundSample g0;
                        if (ys.Count == 0 || !g.TrySample(cx, cz, ys[ys.Count - 1] + 0.05f, out g0) || g0.OnDeck) continue;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            double extent = 0;
                            bool layered = false;
                            for (double e = 0.2; e < 25; e += 0.2)
                            {
                                double ex = cx + side * nx * e, ez = cz + side * nz * e;
                                // Two drawn levels (a deck over the street, a street over a cutting): the bridges package's
                                // railings and decks (RideTests and the bridge tests ride those).
                                if (d.Levels(ex, ez).Count > 1) layered = true;
                                // The drawn surface ends, or a drawn house wall is closer than the body's radius (a body
                                // never gets nearer to a wall than that, drawn or solid).
                                if (d.Levels(ex, ez).Count == 0 || houses.Near(ex, ez, body.Radius + 0.05) || !loaded.Contains(TileId.At(10, ex, ez))) break;
                                extent = e;
                            }
                            if (extent < 1.0 || layered) continue;
                            double px = cx, pz = cz, reached = 0;
                            float py = g0.Height;
                            string why = null;
                            for (double e = 0.2; e <= extent + 0.6; e += 0.2)
                            {
                                double qx = cx + side * nx * e, qz = cz + side * nz * e;
                                GroundSample gs = default(GroundSample);
                                if (g.SweepBody(in body, px, pz, 0f, py, qx - px, qz - pz, out _, out _, out _)) why = "solid";
                                else if (!g.TrySample(qx, qz, py, out gs)) why = "no ground";
                                else if (gs.Height - py > ArcadeVehicle.MaxClimbM) why = "step " + (gs.Height - py).ToString("0.00");
                                if (why != null) break;
                                px = qx;
                                pz = qz;
                                py = gs.Height;
                                reached = e;
                            }
                            sides++;
                            drawnSum += extent;
                            double lost = why != null ? Math.Max(0, extent - (reached + body.Radius)) : 0;
                            lostSum += lost;
                            if (lost < 0.3) continue;
                            narrowed++;
                            if (examples.Count < 12)
                                examples.Add(why + ": " + r.RoadClass + " osm " + r.OsmWayId + " drawn " + extent.ToString("0.0") + " m, lost " + lost.ToString("0.0") + " m at " + t.Tile + " " +
                                             (px - t.Tile.X0).ToString("0.0") + ", " + (pz - t.Tile.Z0).ToString("0.0"));
                        }
                    }
                }
            }
            TestContext.WriteLine("cross-section sides " + sides + ", drawn half-width " + (drawnSum / 1000).ToString("0.0") + " km, lost " + lostSum.ToString("0") + " m, sides narrowed by 0.3 m or more " +
                                  narrowed + " (" + (100.0 * narrowed / Math.Max(1, sides)).ToString("0.00") + " %): " + string.Join("; ", examples));
            Assert.That(sides, Is.GreaterThan(20000));
            Assert.That(narrowed, Is.LessThanOrEqualTo(sides / 400), "the drivable width is the drawn width: " + string.Join("; ", examples));
        }

        /// <summary>
        /// #14 and #1: on a 0.5 m grid near the houses of the feet tiles, (a) the open ground at least 0.45 m outside every
        /// drawn house holds no solid at body height where a mapped house was trimmed back or dropped by the corridor guard
        /// (the house is not drawn there: whole sample before, 24 683 m² of such invisible walls), and (b) no solid stands
        /// on a drawn road surface outside the drawn houses (the retaining walls of a cutting open where a street joins or
        /// crosses it).
        /// </summary>
        [Test]
        public void NoSolidStandsWhereNothingIsDrawn()
        {
            TileGroundQuery g;
            List<(TileData tile, RideFeetTests.Drawn drawn, TileHeightSampler sampler)> tiles;
            RideFeetTests.Stream(out g, out tiles);
            long open = 0, openSolid = 0, trimmedSolid = 0, road = 0, roadSolid = 0;
            string whereOpen = "", whereRoad = "";
            const double step = 0.5;
            foreach ((TileData t, RideFeetTests.Drawn d, TileHeightSampler _) in tiles)
            {
                Houses houses = HousesOf(t);
                for (double lz = 0.23; lz < t.Tile.Size; lz += step)
                for (double lx = 0.41; lx < t.Tile.Size; lx += step)
                {
                    double x = t.Tile.X0 + lx, z = t.Tile.Z0 + lz;
                    bool trimmed;
                    if (!houses.NearMapped(x, z, 3.0, out trimmed) || houses.Near(x, z, 0.45)) continue;
                    List<float> ys = d.Levels(x, z);
                    float feet;
                    if (ys.Count > 0)
                    {
                        feet = ys[ys.Count - 1];
                        road++;
                    }
                    else
                    {
                        GroundSample s;
                        if (!g.TrySample(x, z, float.PositiveInfinity, out s)) continue;
                        feet = s.Height;
                        open++;
                    }
                    if (!g.InsideSolid(x, z, feet)) continue;
                    string at = t.Tile + " at " + lx.ToString("0.0") + ", " + lz.ToString("0.0");
                    if (ys.Count > 0)
                    {
                        roadSolid++;
                        if (whereRoad.Length == 0) whereRoad = at;
                    }
                    else
                    {
                        openSolid++;
                        if (trimmed) trimmedSolid++;
                        if (whereOpen.Length == 0) whereOpen = at;
                    }
                }
            }
            double a = step * step;
            TestContext.WriteLine("open ground near houses " + (open * a).ToString("0") + " m², inside a solid " + (openSolid * a).ToString("0") + " m² (of it in a trimmed or dropped house " +
                                  (trimmedSolid * a).ToString("0") + " m²) " + whereOpen + "; drawn road clear of houses " + (road * a).ToString("0") + " m², inside a solid " +
                                  (roadSolid * a).ToString("0") + " m² " + whereRoad);
            Assert.That(open, Is.GreaterThan(100000));
            Assert.That(trimmedSolid * a, Is.LessThanOrEqualTo(10.0), "no invisible wall where a house was trimmed or dropped: " + whereOpen);
            Assert.That(openSolid * a, Is.LessThanOrEqualTo(0.002 * open * a), "open ground between drawn houses is open: " + whereOpen);
            Assert.That(roadSolid * a, Is.LessThanOrEqualTo(5.0), "nothing solid on a drawn road: " + whereRoad);
        }

        /// <summary>
        /// The ride harness on the feet tiles: a motorbike (no road assist, pure-pursuit driver) rides every drawn motor
        /// road at its drawn centre, and again 0.9 m inside the drawn left edge (the widened road is drivable to its edge).
        /// </summary>
        [Test]
        public void RidesEveryDrawnRoadToItsEdge()
        {
            TileGroundQuery g;
            List<(TileData tile, RideFeetTests.Drawn drawn, TileHeightSampler sampler)> tiles;
            RideFeetTests.Stream(out g, out tiles);
            foreach (double edge in new[] { 0.0, 0.9 })
            {
                int rides = 0, done = 0;
                var stuck = new List<string>();
                foreach ((TileData t, RideFeetTests.Drawn d, TileHeightSampler _) in tiles)
                {
                    RoadLayout layout = RoadLayout.For(t);
                    for (int i = 0; i < t.Roads.Count; i++)
                    {
                        RoadRecord r = t.Roads[i];
                        if (!RoadMesher.IsDrawn(t, i, null) || layout.IsPassage(i) || RoadWidthModel.IsFootClass(r.RoadClass) || !layout.DrawsRibbon(i)) continue;
                        RoadCentreline c = layout.Centres[i];
                        RoadWidthProfile p = layout.Profiles[i];
                        if (c == null || c.Count < 2 || p.LengthM < 12) continue;
                        var px = new List<double>();
                        var pz = new List<double>();
                        for (int k = 0; k < c.Count; k++)
                        {
                            double half = 0.5 * p.DrawnAt(c.S[k]);
                            double off = p.ShiftAt(c.S[k]) + (edge > 0 ? Math.Max(0, half - edge) : 0);
                            px.Add(t.Tile.X0 + c.X[k] - c.Tz[k] * off);
                            pz.Add(t.Tile.Z0 + c.Z[k] + c.Tx[k] * off);
                        }
                        GroundSample s0;
                        if (!g.TrySample(px[0], pz[0], float.PositiveInfinity, out s0)) continue;
                        rides++;
                        RideTests.RideResult res = RideTests.Ride(g, VehicleSpec.Motorbike(), px, pz, 6f);
                        if (res.Finished) done++;
                        else stuck.Add(r.RoadClass + " osm " + r.OsmWayId + " " + t.RoadStructureOf(i).Kind + ": " + res.Why);
                    }
                }
                TestContext.WriteLine("edge " + edge + ": rides " + rides + ", finished " + done + " (" + (100.0 * done / Math.Max(1, rides)).ToString("0.0") + " %); " +
                                      string.Join("; ", stuck.Take(8)));
                Assert.That(rides, Is.GreaterThan(500));
                Assert.That(done, Is.GreaterThanOrEqualTo(rides * (edge > 0 ? 0.97 : 0.99)), "rides finished at edge " + edge + ": " + string.Join("; ", stuck.Take(8)));
            }
        }
    }
}
