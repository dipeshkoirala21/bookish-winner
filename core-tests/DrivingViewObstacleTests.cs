using System;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>IViewObstacleQuery for the chase camera (docs/W2_DETAIL_CONTRACT.md §3): sphere casts stop at houses,
    /// structures, deck slabs and the terrain, pass over low walls and thin poles, and ignore what the target stands in.</summary>
    public class DrivingViewObstacleTests
    {
        private const double H = 1300.0;

        [Test]
        public void SphereCastsStopAtHousesStructuresDecksAndTerrain()
        {
            // A house (x 100..120, z 140..160, 9 m), a low wall at z 200, a lamp post, a hill to the west and the flyover.
            TileData t = DrivingLayeredGroundTests.Flyover();
            t.Buildings.Add(DrivingCollisionTests.House(100, 140, 120, 160));
            t.Props.Add(new PropRecord { Kind = ObjectKind.StreetLamp, XCm = 30000, ZCm = 12000 });
            var g = new TileGroundQuery();
            g.Add(t);
            var c = new StructureColliders();
            c.AddWall(new ColliderWall { X0 = 280, Z0 = 200, X1 = 320, Z1 = 200, HalfThickness = 0.1f, Bottom = (float)H - 0.3f, Top = (float)H + 1.0f });
            g.Register(t.Tile.Key, c);
            IViewObstacleQuery view = g;
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            double eye = H + 1.6, d;

            // Towards the house wall at z = 140 from z = 130: the 0.3 m sphere stops 0.3 m short (plus the wall's 5 cm).
            Assert.That(view.SphereCast(x0 + 110, eye, z0 + 130, 0, 0, 1, 0.3, 20, out d), Is.True);
            Assert.That(d, Is.EqualTo(10 - 0.05 - 0.3).Within(0.02));
            // Nothing in the way: the full distance.
            Assert.That(view.SphereCast(x0 + 110, eye, z0 + 130, 0, 0, -1, 0.3, 20, out d), Is.False);
            Assert.That(d, Is.EqualTo(20.0));
            // Over the top of the house (a camera looking down from 15 m): clear; aimed into its roof: hit.
            Assert.That(view.SphereCast(x0 + 110, H + 15, z0 + 130, 0, 0, 1, 0.3, 40, out d), Is.False);
            Assert.That(view.SphereCast(x0 + 110, H + 15, z0 + 130, 0, -0.5, 1, 0.3, 40, out d), Is.True);
            // A 1 m wall: blocks a low cast, not one at eye height above it.
            Assert.That(view.SphereCast(x0 + 300, H + 0.6, z0 + 190, 0, 0, 1, 0.2, 20, out d), Is.True);
            Assert.That(d, Is.EqualTo(10 - 0.1 - 0.2).Within(0.05));
            Assert.That(view.SphereCast(x0 + 300, eye, z0 + 190, 0, 0, 1, 0.2, 20, out d), Is.False);
            // Thin lamp posts never pull the camera in.
            Assert.That(view.SphereCast(x0 + 300, eye, z0 + 110, 0, 0, 1, 0.3, 20, out d), Is.False);
            // Into the ground: hits the terrain.
            float ground;
            Assert.That(g.TryTerrainHeight(x0 + 200, z0 + 300, out ground), Is.True);
            Assert.That(view.SphereCast(x0 + 200, H + 3, z0 + 300, 0, -1, 0, 0.3, 10, out d), Is.True);
            Assert.That(d, Is.EqualTo(H + 3 - 0.3 - ground).Within(0.05));
            // Under the flyover, rising into its slab: hits its underside (deck 1307, 1.2 m thick).
            Assert.That(view.SphereCast(x0 + 500, H + 1.6, z0 + 500, 0, 1, 0, 0.3, 20, out d), Is.True);
            Assert.That(H + 1.6 + d, Is.EqualTo(1307 - 1.2 - 0.3).Within(0.1));
            // A camera starting inside the house (the target walked into a doorway) ignores that house.
            Assert.That(view.SphereCast(x0 + 110, eye, z0 + 150, 0, 0, -1, 0.3, 5, out d), Is.False);
        }

        [Test]
        public void CastsFromBesideAWallStopAtItAndPassAlongIt()
        {
            // The south wall of a house at z = 100 (x 100..140); the pivot just outside it, closer than the sphere.
            TileData t = DrivingCollisionTests.FlatTile(DrivingCollisionTests.House(100, 100, 140, 120));
            var g = new TileGroundQuery();
            g.Add(t);
            IViewObstacleQuery view = g;
            double x0 = t.Tile.X0, z0 = t.Tile.Z0, d;
            foreach (double gap in new[] { 0.1, 0.3, 0.5, 0.8, 1.2 })
            {
                // Backwards and up into the house, diagonally: never through it.
                double oz = z0 + 100 - gap;
                Assert.That(view.SphereCast(x0 + 120, H + 1.5, oz, -0.3, 0.2, 1.0, 0.35, 8, out d), Is.True, "gap " + gap);
                double reach = Math.Max(0.0, gap - 0.05 - 0.35);
                Assert.That(d, Is.LessThanOrEqualTo(reach / (1.0 / Math.Sqrt(0.09 + 0.04 + 1.0)) + 0.02), "gap " + gap);
            }
            // From 0.1 m off the wall: along it, or away from it, the cast is free.
            Assert.That(view.SphereCast(x0 + 120, H + 1.5, z0 + 99.9, 1, 0, 0, 0.35, 8, out d), Is.False, "along the wall");
            Assert.That(view.SphereCast(x0 + 120, H + 1.5, z0 + 99.9, 0, 0.2, -1, 0.35, 8, out d), Is.False, "away from it");
        }

        [Test]
        public void StatuesColumnsAndPiersStopTheCameraButPostsAndTrunksDoNot()
        {
            TileData t = DrivingCollisionTests.FlatTile();
            var g = new TileGroundQuery();
            g.Add(t);
            var gen = new Generators.GenColliders();
            gen.AddCylinder(100, 150, H, H + 4, 1.0); // a roundabout statue
            gen.AddCylinder(200, 150, H, H + 6, 0.6); // a flyover pier
            gen.AddCylinder(300, 150, H, H + 1, 0.12); // a bollard
            gen.AddCylinder(400, 150, H, H + 6, 0.5, true); // a trunk the generator lets cameras through
            var c = new StructureColliders();
            c.AddFrom(gen);
            c.AddTree(500, 150, (float)H, 12f);
            g.Register(t.Tile.Key, c);
            IViewObstacleQuery view = g;
            double x0 = t.Tile.X0, z0 = t.Tile.Z0, d;
            Assert.That(view.SphereCast(x0 + 100, H + 1.6, z0 + 140, 0, 0, 1, 0.3, 20, out d), Is.True, "statue");
            Assert.That(d, Is.EqualTo(10 - 1.0 - 0.3).Within(0.02));
            Assert.That(view.SphereCast(x0 + 200, H + 1.6, z0 + 140, 0, 0, 1, 0.3, 20, out d), Is.True, "pier");
            Assert.That(view.SphereCast(x0 + 300, H + 0.5, z0 + 140, 0, 0, 1, 0.3, 20, out d), Is.False, "bollard");
            Assert.That(view.SphereCast(x0 + 400, H + 1.6, z0 + 140, 0, 0, 1, 0.3, 20, out d), Is.False, "generator trunk");
            Assert.That(view.SphereCast(x0 + 500, H + 1.6, z0 + 140, 0, 0, 1, 0.3, 20, out d), Is.False, "placed tree");
            // All of them still stop a body.
            foreach (double lane in new[] { 100.0, 200.0, 300.0, 400.0, 500.0 })
                Assert.That(g.IsBlocked(x0 + lane, z0 + 150 - 0.1, (float)H), Is.True, "lane " + lane);
        }

        [Test]
        public void SphereCastsDoNotAllocate()
        {
            TileGroundQuery g = DrivingData.SampleGround();
            double x, z;
            DrivingData.ThamelMarg(out x, out z);
            GroundSample s;
            Assert.That(g.TrySample(x, z, out s), Is.True);
            double d;
            for (int i = 0; i < 50; i++) g.SphereCast(x, s.Height + 1.5, z, Math.Sin(i), 0.3, Math.Cos(i), 0.3, 8, out d);
            long before = GC.GetAllocatedBytesForCurrentThread();
            int hits = 0;
            for (int i = 0; i < 2000; i++)
                if (g.SphereCast(x, s.Height + 1.5, z, Math.Sin(i * 0.1), 0.35, Math.Cos(i * 0.1), 0.3, 8, out d)) hits++;
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
            Assert.That(hits, Is.GreaterThan(100), "Thamel's houses line the street");
        }
    }
}
