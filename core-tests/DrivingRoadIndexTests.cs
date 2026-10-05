using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class DrivingRoadIndexTests
    {
        private const int Tx = 520, Ty = 160; // a level-10 tile inside the valley

        private static TileData TileWith(params RoadRecord[] roads)
        {
            TileData t = DrivingData.Flat(10, Tx, Ty, 1300.0);
            t.Roads.AddRange(roads);
            return t;
        }

        [Test]
        public void IndexesWhatTheRoadMesherDraws()
        {
            // Widths are the mesher's (tag, else class default); tunnels are never drawn; trails follow the options.
            TileData t = TileWith(
                DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 0, RoadFlags.None, 100, 100, 900, 100),
                DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 6.5, RoadFlags.None, 100, 300, 900, 300),
                DrivingData.Road(RoadClass.Primary, Surface.Asphalt, 0, RoadFlags.Tunnel, 100, 500, 900, 500),
                DrivingData.Road(RoadClass.Footway, Surface.Brick, 0, RoadFlags.None, 100, 700, 900, 700));
            var all = new RoadSpatialIndex(t);
            Assert.That(all.HalfWidthM(0), Is.EqualTo(0.5f * RoadStyle.WidthM(t.Roads[0])));
            Assert.That(all.HalfWidthM(1), Is.EqualTo(3.25f).Within(1e-5));
            Assert.That(all.SegmentCount, Is.EqualTo(3), "the tunnel is not indexed");
            Assert.That(all.MaxHalfWidthM, Is.EqualTo(3.25f).Within(1e-5));
            RoadHit hit;
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            Assert.That(all.TryNearest(x0 + 500, z0 + 500, 0.5, out hit), Is.False, "tunnels run under the terrain");
            Assert.That(all.TryNearest(x0 + 500, z0 + 700, 0.5, out hit), Is.True);
            Assert.That(hit.Road.RoadClass, Is.EqualTo(RoadClass.Footway));
            var noTrails = new RoadSpatialIndex(t, new RoadOptions { IncludeTrails = false });
            Assert.That(noTrails.TryNearest(x0 + 500, z0 + 700, 0.5, out hit), Is.False, "trails not drawn, not a road");
            Assert.That(noTrails.SegmentCount, Is.EqualTo(2));
        }

        [Test]
        public void StraightRoadHitGeometry()
        {
            // East-west residential street (5 m wide) along local z = 500 from x = 100 to 900.
            TileData t = TileWith(DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 0, RoadFlags.None, 100, 500, 900, 500));
            var idx = new RoadSpatialIndex(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            Assert.That(idx.SegmentCount, Is.EqualTo(1));
            Assert.That(idx.MaxHalfWidthM, Is.EqualTo(2.5f));

            RoadHit hit;
            Assert.That(idx.TryNearest(x0 + 300, z0 + 500, RoadSpatialIndex.OnRoadMarginM, out hit), Is.True);
            Assert.That(hit.OnRoad, Is.True);
            Assert.That(hit.Tile, Is.SameAs(t));
            Assert.That(hit.Road, Is.SameAs(t.Roads[0]));
            Assert.That(hit.RoadIndex, Is.EqualTo(0));
            Assert.That(hit.Segment, Is.EqualTo(0));
            Assert.That(hit.DistanceM, Is.EqualTo(0f).Within(1e-4));
            Assert.That(hit.DirX, Is.EqualTo(1f).Within(1e-6));
            Assert.That(hit.DirZ, Is.EqualTo(0f).Within(1e-6));
            Assert.That(hit.T, Is.EqualTo(0.25f).Within(1e-5));
            Assert.That(hit.AlongM, Is.EqualTo(200f).Within(1e-3));
            Assert.That(hit.PieceLengthM, Is.EqualTo(800f).Within(1e-3));
            Assert.That(hit.X, Is.EqualTo(x0 + 300).Within(1e-6));
            Assert.That(hit.Z, Is.EqualTo(z0 + 500).Within(1e-6));

            // 2 m north: inside (half width 2.5); heading east, north is on the left, so the offset is negative.
            Assert.That(idx.TryNearest(x0 + 300, z0 + 502, RoadSpatialIndex.OnRoadMarginM, out hit), Is.True);
            Assert.That(hit.DistanceM, Is.EqualTo(2f).Within(1e-4));
            Assert.That(hit.LateralM, Is.EqualTo(-2f).Within(1e-4));
            Assert.That(hit.EdgeDistanceM, Is.EqualTo(-0.5f).Within(1e-4));
            Assert.That(idx.TryNearest(x0 + 300, z0 + 497, RoadSpatialIndex.OnRoadMarginM, out hit), Is.True);
            Assert.That(hit.LateralM, Is.EqualTo(3f).Within(1e-4), "south of an eastbound road is its right");

            // Exactly half width + margin is still on the road; a centimetre more is not.
            Assert.That(idx.TryNearest(x0 + 300, z0 + 503.0, RoadSpatialIndex.OnRoadMarginM, out hit), Is.True);
            Assert.That(hit.OnRoad, Is.True);
            Assert.That(idx.TryNearest(x0 + 300, z0 + 503.01, RoadSpatialIndex.OnRoadMarginM, out hit), Is.False);
            Assert.That(idx.TryNearest(x0 + 300, z0 + 510, 20.0, out hit), Is.True);
            Assert.That(hit.OnRoad, Is.False);
            Assert.That(hit.EdgeDistanceM, Is.EqualTo(7.5f).Within(1e-4));

            // Past the end of the road the distance is to the end point.
            Assert.That(idx.TryNearest(x0 + 904, z0 + 503, 20.0, out hit), Is.True);
            Assert.That(hit.T, Is.EqualTo(1f));
            Assert.That(hit.DistanceM, Is.EqualTo(5f).Within(1e-4));
            Assert.That(idx.TryNearest(x0 - 500, z0 + 500, 20.0, out hit), Is.False, "far outside the tile");
            Assert.That(idx.TryNearest(double.NaN, z0, 20.0, out hit), Is.False);
        }

        [Test]
        public void ContextSegmentsAreNotRoads()
        {
            // A piece cut at the west edge: (-30, 200) is the context point, (0, 200) the cut point.
            TileData t = TileWith(DrivingData.Road(RoadClass.Tertiary, Surface.Asphalt, 0, RoadFlags.HasPrevCtx | RoadFlags.HasNextCtx,
                -30, 200, 0, 200, 50, 200, 50, 1024, 50, 1060));
            var idx = new RoadSpatialIndex(t);
            Assert.That(idx.SegmentCount, Is.EqualTo(2));
            RoadHit hit;
            Assert.That(idx.TryNearest(t.Tile.X0 + 10, t.Tile.Z0 + 200, 0.5, out hit), Is.True);
            Assert.That(hit.Segment, Is.EqualTo(1), "segment index counts the context point");
            Assert.That(hit.AlongM, Is.EqualTo(10f).Within(1e-3), "along starts at the first rendered point");
            Assert.That(idx.TryNearest(t.Tile.X0 - 20, t.Tile.Z0 + 200, 0.5, out hit), Is.False, "the context segment is not drivable");
            Assert.That(hit.PieceLengthM, Is.EqualTo(0f));
            Assert.That(idx.TryNearest(t.Tile.X0 + 50, t.Tile.Z0 + 1000, 0.5, out hit), Is.True);
            Assert.That(hit.Segment, Is.EqualTo(2));
            Assert.That(hit.PieceLengthM, Is.EqualTo(50f + 824f).Within(1e-3));
        }

        [Test]
        public void WideRoadWinsOverCrossingFootway()
        {
            TileData t = TileWith(
                DrivingData.Road(RoadClass.Footway, Surface.Brick, 0, RoadFlags.None, 400, 300, 400, 700),
                DrivingData.Road(RoadClass.Trunk, Surface.Asphalt, 0, RoadFlags.None, 100, 500, 900, 500),
                DrivingData.Road(RoadClass.Primary, Surface.Concrete, 0, RoadFlags.Tunnel, 100, 520, 900, 520));
            var idx = new RoadSpatialIndex(t);
            RoadHit hit;
            // On the footway centreline, 3 m north of the trunk centre line (half width 5): the trunk surface.
            Assert.That(idx.TryNearest(t.Tile.X0 + 400, t.Tile.Z0 + 503, 0.5, out hit), Is.True);
            Assert.That(hit.Road.RoadClass, Is.EqualTo(RoadClass.Trunk));
            Assert.That(hit.EdgeDistanceM, Is.LessThan(-1.5f));
            // Above the tunnel: nothing (it is not drawn).
            Assert.That(idx.TryNearest(t.Tile.X0 + 200, t.Tile.Z0 + 519, 0.5, out hit), Is.False);
        }

        [Test]
        public void MatchesBruteForceOnRandomRoads()
        {
            var rng = new Random(1234);
            TileData t = DrivingData.Flat(10, Tx, Ty, 1300.0);
            var classes = new[] { RoadClass.Residential, RoadClass.Footway, RoadClass.Primary, RoadClass.Service, RoadClass.Track };
            for (int r = 0; r < 80; r++)
            {
                int n = 2 + rng.Next(6);
                var pts = new double[n * 2];
                double x = rng.NextDouble() * 1024, z = rng.NextDouble() * 1024;
                for (int k = 0; k < n; k++)
                {
                    pts[2 * k] = Math.Clamp(x, 0, 1024);
                    pts[2 * k + 1] = Math.Clamp(z, 0, 1024);
                    x += rng.NextDouble() * 160 - 80;
                    z += rng.NextDouble() * 160 - 80;
                }
                t.Roads.Add(DrivingData.Road(classes[r % classes.Length], Surface.Asphalt, rng.Next(3) == 0 ? 3 + rng.NextDouble() * 8 : 0,
                    RoadFlags.None, pts));
            }
            var idx = new RoadSpatialIndex(t, null, 16.0);
            for (int q = 0; q < 3000; q++)
            {
                double x = t.Tile.X0 - 20 + rng.NextDouble() * 1064, z = t.Tile.Z0 - 20 + rng.NextDouble() * 1064;
                double maxDist = q % 3 == 0 ? 0.5 : rng.NextDouble() * 60;
                double best = BruteForceEdge(t, x, z);
                RoadHit hit;
                bool found = idx.TryNearest(x, z, maxDist, out hit);
                Assert.That(found, Is.EqualTo(best <= maxDist), "query " + q);
                if (found) Assert.That(hit.EdgeDistanceM, Is.EqualTo(best).Within(1e-3), "query " + q);
            }
        }

        private static double BruteForceEdge(TileData t, double x, double z)
        {
            double best = double.PositiveInfinity;
            foreach (RoadRecord r in t.Roads)
            {
                int first, last;
                RoadSpatialIndex.RenderedRange(r, out first, out last);
                if (!RoadMesher.IsDrawn(r, new RoadOptions())) continue;
                double half = 0.5 * RoadStyle.WidthM(r);
                for (int k = first; k < last; k++)
                {
                    double ax = t.Tile.X0 + r.Points[2 * k] / 100.0, az = t.Tile.Z0 + r.Points[2 * k + 1] / 100.0;
                    double bx = t.Tile.X0 + r.Points[2 * k + 2] / 100.0, bz = t.Tile.Z0 + r.Points[2 * k + 3] / 100.0;
                    double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                    double u = l2 > 0 ? Math.Clamp(((x - ax) * dx + (z - az) * dz) / l2, 0, 1) : 0;
                    double px = ax + u * dx - x, pz = az + u * dz - z;
                    best = Math.Min(best, Math.Sqrt(px * px + pz * pz) - half);
                }
            }
            return best;
        }

        // ---- the real sample region ----

        [Test]
        public void ThamelMargIsARoad()
        {
            // A vertex of Thamel Marg in the pack (pipeline-decoded lon/lat of the stored point).
            double x, z;
            WorldFrame.LonLatToGame(85.31172094019205, 27.716693189023914, out x, out z);
            TileData t = DrivingData.SampleTiles()[TileId.At(10, x, z)];
            var idx = new RoadSpatialIndex(t);
            RoadHit hit;
            Assert.That(idx.TryNearest(x, z, RoadSpatialIndex.OnRoadMarginM, out hit), Is.True);
            Assert.That(hit.OnRoad, Is.True);
            Assert.That(hit.DistanceM, Is.LessThan(0.05f));
            Assert.That(t.Name(hit.Road.NameRef).Default, Is.EqualTo("Thamel Marg"));
            Assert.That(SurfaceGroups.Of(hit.Road.Surface), Is.EqualTo(SurfaceGroup.Paved));

            // The Thamel place node itself sits in a courtyard ~24 m from the nearest lane.
            WorldFrame.LonLatToGame(85.312702, 27.716658, out x, out z);
            Assert.That(idx.TryNearest(x, z, RoadSpatialIndex.OnRoadMarginM, out hit), Is.False);
            Assert.That(idx.TryNearest(x, z, 40.0, out hit), Is.True);
            Assert.That(hit.DistanceM, Is.InRange(15f, 35f));
        }

        [Test]
        public void RealTilesMatchBruteForce()
        {
            var rng = new Random(7);
            int checkedTiles = 0, onRoad = 0, total = 0;
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10 || t.Roads.Count == 0) continue;
                if (checkedTiles++ % 6 != 0) continue; // a spread of tiles keeps the test quick
                var idx = new RoadSpatialIndex(t);
                for (int q = 0; q < 300; q++)
                {
                    double x = t.Tile.X0 + rng.NextDouble() * 1024, z = t.Tile.Z0 + rng.NextDouble() * 1024;
                    double best = BruteForceEdge(t, x, z);
                    RoadHit hit;
                    bool found = idx.TryNearest(x, z, 0.5, out hit);
                    Assert.That(found, Is.EqualTo(best <= 0.5), t.Tile + " query " + q);
                    if (found)
                    {
                        Assert.That(hit.EdgeDistanceM, Is.EqualTo(best).Within(1e-3));
                        onRoad++;
                    }
                    total++;
                }
            }
            Assert.That(checkedTiles, Is.GreaterThan(30));
            // Central Kathmandu is dense with streets: a fair share of random points land on one.
            Assert.That(onRoad, Is.GreaterThan(total / 20), onRoad + " of " + total);
        }
    }
}
