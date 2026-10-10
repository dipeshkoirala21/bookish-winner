using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>The runtime footprint guard (docs/W2_DETAIL_CONTRACT.md decision 1, <see cref="FootprintGuard"/>): a
    /// footprint reaching into a road is trimmed back to that road's edge on its own side, never across another road,
    /// split in two by a road running through it, wrapped round a dead end, and never left inside a carriageway; and it
    /// stays cheap.</summary>
    public class DrivingFootprintGuardTests
    {
        private const double H = 1300.0;
        private const float Dt60 = 1f / 60f;
        private const float Deg = (float)(Math.PI / 180.0);

        private static TileGroundQuery Ground(TileData t)
        {
            var g = new TileGroundQuery();
            g.Add(t);
            return g;
        }

        private static List<SolidPrim> Walls(TileGroundQuery g, TileData t)
        {
            var prims = new List<SolidPrim>();
            g.SolidsOf(t.Tile, prims);
            prims.RemoveAll(p => p.Group < 0);
            return prims;
        }

        /// <summary>The deepest any wall of the solids lies inside a ground carriageway, sampled every 0.25 m.</summary>
        private static double DeepestInRoad(TileGroundQuery g, List<SolidPrim> walls)
        {
            double deepest = 0;
            foreach (SolidPrim p in walls)
            {
                double dx = p.Bx - p.Ax, dz = p.Bz - p.Az;
                int n = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / 0.25));
                for (int k = 0; k <= n; k++)
                {
                    RoadHit hit;
                    if (g.TryNearestRoad(p.Ax + dx * k / n, p.Az + dz * k / n, 0.0, RoadFlags.None, RoadFlags.None, RoadLayer.Ground, out hit))
                        deepest = Math.Min(deepest, hit.EdgeDistanceM);
                }
            }
            return deepest;
        }

        private static float HalfWidthAt(TileGroundQuery g, double x, double z)
        {
            GroundSample s;
            Assert.That(g.TrySample(x, z, out s) && s.OnRoad, Is.True, "a road at " + x + ", " + z);
            return s.RoadHalfWidthM;
        }

        [Test]
        public void AHouseBetweenTwoStreetsIsTrimmedOnlyOnItsOwnSide()
        {
            // South street along z = 500, north street along z = 522; the house reaches into the south street only.
            TileData t = DrivingData.Flat(10, 520, 160, H);
            t.Roads.Add(DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 6, RoadFlags.None, 300, 500, 700, 500));
            t.Roads.Add(DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 6, RoadFlags.None, 300, 522, 700, 522));
            t.Buildings.Add(DrivingCollisionTests.House(480, 501.5, 500, 516));
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            float south = HalfWidthAt(g, x0 + 490, z0 + 500), north = HalfWidthAt(g, x0 + 490, z0 + 522);
            Assert.That(501.5, Is.LessThan(500 + south), "the house really reaches into the south street");
            Assert.That(516.0, Is.LessThan(522 - north), "and stays clear of the north street");

            List<SolidPrim> walls = Walls(g, t);
            foreach (SolidPrim p in walls)
            {
                Assert.That(Math.Max(p.Az, p.Bz) - z0, Is.LessThanOrEqualTo(516.0 + 1e-6), "the back wall never moves");
                Assert.That(Math.Min(p.Az, p.Bz) - z0, Is.GreaterThanOrEqualTo(500 + south - 1e-3), "the front stops at the south street's edge");
            }
            Assert.That(DeepestInRoad(g, walls), Is.GreaterThan(-0.01));
            Assert.That(g.InsideSolid(x0 + 490, z0 + 522, (float)H), Is.False, "the north street is free");
            Assert.That(g.InsideSolid(x0 + 490, z0 + 500 + south - 0.2, (float)H), Is.False, "the south street is free");
            Assert.That(g.InsideSolid(x0 + 490, z0 + 510, (float)H), Is.True, "the house is solid");

            // Both streets ride end to end without touching anything.
            foreach (double street in new[] { 500.0, 522.0 })
            {
                var v = new ArcadeVehicle(VehicleSpec.Motorbike());
                v.Teleport(x0 + 420, z0 + street, 90f * Deg, g);
                StepEvents ev = StepEvents.None;
                for (int i = 0; i < 60 * 20 && v.X < x0 + 600; i++) ev |= v.Step(new DriveInput(0.6f, 0f, 0f), Dt60, g, 0f);
                Assert.That(v.X - x0, Is.GreaterThanOrEqualTo(600.0), "street " + street);
                Assert.That(ev & (StepEvents.HitWall | StepEvents.StuckRecovered), Is.EqualTo(StepEvents.None), "street " + street);
            }
        }

        [Test]
        public void ARoadThroughAFootprintLeavesAHouseOnEachSide()
        {
            TileData t = DrivingData.Flat(10, 520, 160, H);
            t.Roads.Add(DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 6, RoadFlags.None, 300, 500, 700, 500));
            t.Buildings.Add(DrivingCollisionTests.House(480, 485, 510, 515));
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            float half = HalfWidthAt(g, x0 + 490, z0 + 500);
            List<SolidPrim> walls = Walls(g, t);
            var groups = new HashSet<int>();
            foreach (SolidPrim p in walls) groups.Add(p.Group);
            Assert.That(groups.Count, Is.EqualTo(2), "one outline on each side");
            Assert.That(DeepestInRoad(g, walls), Is.GreaterThan(-0.01));
            Assert.That(g.InsideSolid(x0 + 495, z0 + 500, (float)H), Is.False, "the road is free");
            Assert.That(g.InsideSolid(x0 + 495, z0 + 500 + half + 2, (float)H), Is.True, "north part");
            Assert.That(g.InsideSolid(x0 + 495, z0 + 500 - half - 2, (float)H), Is.True, "south part");
            var v = new ArcadeVehicle(VehicleSpec.For(HandlingPreset.Hatchback));
            v.Teleport(x0 + 420, z0 + 500, 90f * Deg, g);
            StepEvents ev = StepEvents.None;
            for (int i = 0; i < 60 * 20 && v.X < x0 + 600; i++) ev |= v.Step(new DriveInput(0.6f, 0f, 0f), Dt60, g, 0f);
            Assert.That(v.X - x0, Is.GreaterThanOrEqualTo(600.0));
            Assert.That(ev & (StepEvents.HitWall | StepEvents.StuckRecovered), Is.EqualTo(StepEvents.None));
        }

        [Test]
        public void ADeadEndIntoAFootprintIsWrappedAndTheCornerOfAJunctionCut()
        {
            // A lane ending 6 m inside a house, and a corner house reaching into both streets of a crossing.
            TileData t = DrivingData.Flat(10, 520, 160, H);
            t.Roads.Add(DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 5, RoadFlags.None, 200, 300, 200, 406));
            t.Buildings.Add(DrivingCollisionTests.House(185, 400, 215, 420));
            t.Roads.Add(DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 6, RoadFlags.None, 500, 300, 500, 500, 500, 700));
            t.Roads.Add(DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 6, RoadFlags.None, 300, 500, 500, 500, 700, 500));
            t.Buildings.Add(DrivingCollisionTests.House(501, 501, 520, 520));
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            List<SolidPrim> walls = Walls(g, t);
            Assert.That(DeepestInRoad(g, walls), Is.GreaterThan(-0.01), "no wall stands in a carriageway");
            Assert.That(g.InsideSolid(x0 + 200, z0 + 404, (float)H), Is.False, "the lane's end is free");
            Assert.That(g.InsideSolid(x0 + 200, z0 + 412, (float)H), Is.True, "the house beyond it is solid");
            Assert.That(g.InsideSolid(x0 + 190, z0 + 405, (float)H), Is.True, "and beside it");
            // (The width model narrows a street beside a house down to its real width, so measure where the house is.)
            float halfNs = HalfWidthAt(g, x0 + 500, z0 + 510), halfEw = HalfWidthAt(g, x0 + 510, z0 + 500);
            Assert.That(halfNs, Is.GreaterThan(1.0f).And.GreaterThan(halfEw - 1f), "the house reaches into the north-south street");
            Assert.That(g.InsideSolid(x0 + 500 + halfNs + 2, z0 + 500 + halfEw + 2, (float)H), Is.True, "the corner house");
            Assert.That(g.InsideSolid(x0 + 500 + halfNs - 0.2, z0 + 510, (float)H), Is.False, "clear of one street");
            Assert.That(g.InsideSolid(x0 + 510, z0 + 500 + halfEw - 0.2, (float)H), Is.False, "and of the other");
            // A car drives up the lane to its end, and round the corner, without being stopped short.
            var v = new ArcadeVehicle(VehicleSpec.For(HandlingPreset.Hatchback));
            v.Teleport(x0 + 200, z0 + 320, 0f, g);
            double front = 0;
            for (int i = 0; i < 60 * 12; i++)
            {
                v.Step(new DriveInput(0.4f, 0f, 0f), Dt60, g, 0f);
                front = Math.Max(front, v.Z + v.Body.LastM + v.Body.Radius - z0);
            }
            Assert.That(front, Is.GreaterThan(405.0), "reached the end of the lane");
            Assert.That(front, Is.LessThan(406.1), "and stopped at the house");
        }

        [Test]
        public void NoFootprintWallStandsInACarriagewayOfTheRealCity()
        {
            int tiles = 0, rings = 0;
            double worst = 0;
            string where = "";
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10 || !t.HasDetail) continue;
                tiles++;
                TileGroundQuery g = Ground(t);
                List<SolidPrim> walls = Walls(g, t);
                rings += walls.Count;
                foreach (SolidPrim p in walls)
                {
                    double dx = p.Bx - p.Ax, dz = p.Bz - p.Az;
                    int n = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / 0.5));
                    for (int k = 0; k <= n; k++)
                    {
                        double x = p.Ax + dx * k / n, z = p.Az + dz * k / n;
                        RoadHit hit;
                        if (!g.TryNearestRoad(x, z, 0.0, RoadFlags.None, RoadFlags.None, RoadLayer.Ground, out hit) || hit.EdgeDistanceM >= worst) continue;
                        worst = hit.EdgeDistanceM;
                        where = t.Tile + " at " + (x - t.Tile.X0).ToString("F1") + ", " + (z - t.Tile.Z0).ToString("F1");
                    }
                }
            }
            Assert.That(tiles, Is.GreaterThan(30));
            Assert.That(rings, Is.GreaterThan(100000));
            Assert.That(worst, Is.GreaterThan(-FootprintGuard.MaxInsideM), "deepest wall in a carriageway: " + worst + " m, " + where);
        }

        [Test]
        public void TheGuardStaysCheap()
        {
            // Solids with the guard against the same build without roads (no guard, no railings), over the real city.
            var opts = new RoadOptions();
            var guarded = new System.Diagnostics.Stopwatch();
            var plain = new System.Diagnostics.Stopwatch();
            int tiles = 0;
            long primsGuarded = 0, primsPlain = 0;
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10 || !t.HasDetail) continue;
                var idx = new RoadSpatialIndex(t, opts);
                TileSolids.Build(t, idx, null, null, opts); // warm up
                guarded.Start();
                SolidSet a = TileSolids.Build(t, idx, null, null, opts);
                guarded.Stop();
                plain.Start();
                SolidSet b = TileSolids.Build(t, null, null, null, opts);
                plain.Stop();
                primsGuarded += a.Count;
                primsPlain += b.Count;
                tiles++;
            }
            double perTile = guarded.Elapsed.TotalMilliseconds / tiles, plainPerTile = plain.Elapsed.TotalMilliseconds / tiles;
            TestContext.Progress.WriteLine("solids per tile: " + perTile.ToString("F1") + " ms with the guard, " + plainPerTile.ToString("F1") +
                                           " ms without; prims " + primsGuarded / tiles + " vs " + primsPlain / tiles);
            Assert.That(perTile, Is.LessThan(5.0 * plainPerTile + 10.0), "the guard costs a few times the plain build at most (it was 30 times)");
            Assert.That(perTile, Is.LessThan(150.0), "well under a tile's mesh build");
            Assert.That(primsGuarded, Is.LessThan(primsPlain * 1.15), "trimmed outlines add few walls");
        }

        [Test]
        public void TrimmingIsDeterministic()
        {
            double x, z;
            DrivingData.ThamelMarg(out x, out z);
            TileData t = DrivingData.SampleTiles()[TileId.At(10, x, z)];
            List<SolidPrim> a = Walls(Ground(t), t), b = Walls(Ground(t), t);
            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].Ax, Is.EqualTo(a[i].Ax));
                Assert.That(b[i].Az, Is.EqualTo(a[i].Az));
                Assert.That(b[i].Bx, Is.EqualTo(a[i].Bx));
                Assert.That(b[i].Bz, Is.EqualTo(a[i].Bz));
                Assert.That(b[i].Group, Is.EqualTo(a[i].Group));
            }
        }
    }
}
