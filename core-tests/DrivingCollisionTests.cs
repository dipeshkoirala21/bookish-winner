using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Solid, rideable world (docs/W2_DETAIL_CONTRACT.md §1): swept bodies never pass through houses, walls,
    /// statues, parked vehicles, trees or railings at any speed or frame rate, slide along walls, step up only small
    /// steps and respect overhead clearance.</summary>
    public class DrivingCollisionTests
    {
        private const double H = 1300.0;
        private const float Dt60 = 1f / 60f;
        private const float Deg = (float)(Math.PI / 180.0);

        /// <summary>A flat urban tile with the given buildings (local metre rings, counter-clockwise).</summary>
        internal static TileData FlatTile(params BuildingRecord[] buildings)
        {
            TileData t = DrivingData.Flat(10, 520, 160, H);
            foreach (BuildingRecord b in buildings) t.Buildings.Add(b);
            return t;
        }

        /// <summary>A rectangular building from local metres (x0, z0)–(x1, z1).</summary>
        internal static BuildingRecord House(double x0, double z0, double x1, double z1, double heightM = 9, double minHeightM = 0,
                                             BuildingArchetype a = BuildingArchetype.ModernUrban, BuildingFlags flags = BuildingFlags.None)
        {
            return new BuildingRecord
            {
                OsmRef = (ulong)(x0 * 1000 + z0), Archetype = a, Use = BuildingUse.House, Levels = 3, Flags = flags,
                HeightCm = (ulong)(heightM * 100), MinHeightCm = (ulong)(minHeightM * 100),
                Rings = new[] { Ring(x0, z0, x1, z0, x1, z1, x0, z1) },
            };
        }

        internal static int[] Ring(params double[] localMetres)
        {
            var r = new int[localMetres.Length];
            for (int i = 0; i < r.Length; i++) r[i] = (int)Math.Round(localMetres[i] * 100.0);
            return r;
        }

        private static TileGroundQuery Ground(TileData t)
        {
            var g = new TileGroundQuery();
            g.Add(t);
            return g;
        }

        private static ArcadeVehicle At(VehicleSpec spec, IGroundQuery g, double x, double z, float headingRad)
        {
            var v = new ArcadeVehicle(spec);
            v.Teleport(x, z, headingRad, g);
            Assert.That(v.HasGround, Is.True);
            return v;
        }

        private static IEnumerable<VehicleSpec> Bodies()
        {
            yield return VehicleSpec.Walker();
            yield return VehicleSpec.Bicycle();
            yield return VehicleSpec.Motorbike();
            yield return VehicleSpec.For(HandlingPreset.Hatchback);
            yield return VehicleSpec.For(HandlingPreset.Bus);
        }

        /// <summary>Front-most reach of the body ahead of the vehicle origin.</summary>
        private static double FrontReach(ArcadeVehicle v)
        {
            return v.Body.LastM + v.Body.Radius;
        }

        [Test]
        public void BodiesAreCapsulesAlongTheVehicle()
        {
            CollisionBody walker = CollisionBody.For(VehicleSpec.Walker());
            Assert.That(walker.Circles, Is.EqualTo(1));
            Assert.That(walker.Radius, Is.GreaterThanOrEqualTo(0.3f));
            Assert.That(walker.FirstM, Is.EqualTo(0f));
            CollisionBody bike = CollisionBody.For(VehicleSpec.Motorbike());
            Assert.That(bike.HeightM, Is.GreaterThanOrEqualTo(CollisionBody.RiderHeightM), "the rider's head counts");
            VehicleSpec busSpec = VehicleSpec.For(HandlingPreset.Bus);
            CollisionBody bus = CollisionBody.For(busSpec);
            Assert.That(bus.Circles, Is.InRange(6, CollisionBody.MaxCircles));
            Assert.That(bus.FirstM - bus.Radius, Is.EqualTo(-busSpec.RearOverhangM).Within(1e-4f), "covers the rear overhang");
            Assert.That(bus.LastM + bus.Radius, Is.EqualTo(busSpec.LengthM - busSpec.RearOverhangM).Within(1e-4f), "and the nose");
            Assert.That((bus.LastM - bus.FirstM) / (bus.Circles - 1), Is.LessThanOrEqualTo(bus.Radius + 1e-4f), "no gaps between circles");
            Assert.That(bus.HeightM, Is.EqualTo(busSpec.HeightM));
        }

        [Test]
        public void SweptBodiesNeverTunnelAtAnySpeedOrFrameRate()
        {
            // A 10 cm railing across the street at z = 150 and a house wall at z = 300.
            TileData t = FlatTile(House(80, 300, 120, 320));
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            var c = new StructureColliders();
            c.AddWall(new ColliderWall { X0 = 0, Z0 = 150, X1 = 200, Z1 = 150, HalfThickness = 0.05f, Bottom = (float)H - 0.4f, Top = (float)H + 1.1f });
            g.Register(t.Tile.Key, c);

            // The sweep itself: a 100 m move stops exactly at the railing.
            CollisionBody body = CollisionBody.Upright(0.4f, 1.7f);
            float tHit, nx, nz;
            Assert.That(g.SweepBody(in body, x0 + 100, z0 + 100, 0f, (float)H, 0, 100, out tHit, out nx, out nz), Is.True);
            Assert.That(z0 + 100 + 100 * tHit, Is.EqualTo(z0 + 150 - 0.05 - 0.4).Within(1e-3));
            Assert.That(nz, Is.EqualTo(-1f).Within(1e-4f));
            // A body whose feet are above the railing passes over it.
            Assert.That(g.SweepBody(in body, x0 + 100, z0 + 100, 0f, (float)H + 1.2f, 0, 100, out tHit, out nx, out nz), Is.False);

            foreach (float frame in new[] { 1f / 60f, 1f / 15f, ArcadeVehicle.MaxFrameS })
            foreach (VehicleSpec spec in Bodies())
            {
                float secs = 20f; // the slowest (walker, bicycle) arrive too
                ArcadeVehicle v = At(spec, g, x0 + 100, z0 + 60, 0f);
                v.SpeedMps = spec.TurnInPlace ? 0f : 30f; // 108 km/h at the railing
                double reach = 0;
                for (int i = 0; i < (int)(secs / frame); i++)
                {
                    v.Step(new DriveInput(1f, 0f, 0f, true), frame, g, 0f);
                    reach = Math.Max(reach, v.Z + FrontReach(v));
                    Assert.That(v.Z + FrontReach(v), Is.LessThanOrEqualTo(z0 + 150 - 0.05 + 1e-3), spec.Name + " @" + frame + " crossed the railing");
                }
                Assert.That(reach, Is.GreaterThan(z0 + 150 - 0.05 - 0.2), spec.Name + " rode up to the railing");

                // The house: never inside its walls.
                ArcadeVehicle w = At(spec, g, x0 + 100, z0 + 200, 0f);
                w.SpeedMps = spec.TurnInPlace ? 0f : 30f;
                for (int i = 0; i < (int)(secs / frame); i++)
                {
                    w.Step(new DriveInput(1f, 0f, 0f, true), frame, g, 0f);
                    Assert.That(w.Z + FrontReach(w), Is.LessThanOrEqualTo(z0 + 300 + 1e-3), spec.Name + " @" + frame + " entered the house");
                }
                Assert.That(g.InsideSolid(w.X, w.Z, w.Y), Is.False);
            }
        }

        [Test]
        public void HeadOnHitsStopAndAreReported()
        {
            TileData t = FlatTile(House(80, 200, 120, 220));
            TileGroundQuery g = Ground(t);
            ArcadeVehicle v = At(VehicleSpec.For(HandlingPreset.Hatchback), g, t.Tile.X0 + 100, t.Tile.Z0 + 120, 0f);
            v.SpeedMps = 20f;
            StepEvents ev = StepEvents.None;
            float impact = 0f;
            for (int i = 0; i < 60 * 8; i++)
            {
                StepEvents e = v.Step(new DriveInput(1f, 0f, 0f), Dt60, g, 0f);
                if ((e & StepEvents.HitWall) != 0) impact = Math.Max(impact, v.LastImpactMps);
                ev |= e;
            }
            Assert.That(ev & StepEvents.HitWall, Is.EqualTo(StepEvents.HitWall));
            Assert.That(impact, Is.GreaterThan(20f), "the whole speed is lost head-on");
            Assert.That(v.Z + FrontReach(v), Is.InRange(t.Tile.Z0 + 199.7, t.Tile.Z0 + 200.0));
        }

        [Test]
        public void BodiesSlideSmoothlyAlongWalls()
        {
            // A long terrace along z = 140..160 from x = 0 to 400; ride north-east into it at 40°.
            TileData t = FlatTile(House(0, 140, 400, 160));
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            foreach (VehicleSpec spec in new[] { VehicleSpec.Motorbike(), VehicleSpec.For(HandlingPreset.Hatchback), VehicleSpec.Walker() })
            {
                ArcadeVehicle v = At(spec, g, x0 + 100, z0 + 120, 50f * Deg);
                if (!spec.TurnInPlace) v.SpeedMps = 12f;
                float minSpeed = float.MaxValue;
                double startX = v.X;
                int touching = 0;
                for (int i = 0; i < 60 * (spec.TurnInPlace ? 12 : 6); i++)
                {
                    // Walkers keep pushing towards the wall; vehicles hold the throttle with a slight pull to the left.
                    var input = spec.TurnInPlace ? new DriveInput(1f, 0f, v.SteerToward(50f * Deg, Dt60)) : new DriveInput(0.7f, 0f, -0.05f);
                    v.Step(input, Dt60, g, 0f);
                    Assert.That(g.InsideSolid(v.X, v.Z, v.Y), Is.False, spec.Name);
                    for (int k = 0; k < v.Body.Circles; k++)
                    {
                        double cx, cz;
                        v.Body.Centre(k, v.X, v.Z, v.HeadingRad, out cx, out cz);
                        Assert.That(cz + v.Body.Radius, Is.LessThanOrEqualTo(z0 + 140 + 1e-3), spec.Name + " in the wall");
                    }
                    if (z0 + 140 - (v.Z + FrontReach(v) * Math.Cos(v.HeadingRad)) < 0.6) touching++;
                    if (i > 60 && touching > 0 && !spec.TurnInPlace) minSpeed = Math.Min(minSpeed, v.SpeedMps);
                }
                Assert.That(touching, Is.GreaterThan(0), spec.Name + " reached the wall");
                Assert.That(v.X - startX, Is.GreaterThan(spec.TurnInPlace ? 5.0 : 25.0), spec.Name + " slid along it");
                if (!spec.TurnInPlace)
                {
                    Assert.That(minSpeed, Is.GreaterThan(2f), spec.Name + " kept moving along the wall");
                    float alongWall = Math.Abs(ArcadeVehicle.WrapAngle(v.HeadingRad - 90f * Deg));
                    Assert.That(alongWall, Is.LessThan(30f * Deg), spec.Name + " turned along the wall");
                }
            }
        }

        [Test]
        public void OnlySmallStepsAndOverheadClearanceHold()
        {
            TileData t = FlatTile();
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            var c = new StructureColliders();
            // Lane 100: a 0.3 m plinth (stepped onto); lane 200: a 0.9 m terrace (a wall); lane 300: an overhang from 2.4 m.
            c.AddBox(new OrientedBox { CX = 100, CZ = 150, CY = (float)H - 0.35f, HalfY = 0.65f, HalfX = 5f, HalfZ = 10f, Flags = ColliderFlags.Walkable });
            c.AddBox(new OrientedBox { CX = 200, CZ = 150, CY = (float)H + 0.45f, HalfY = 0.45f, HalfX = 5f, HalfZ = 10f, Flags = ColliderFlags.Walkable });
            c.AddBox(new OrientedBox { CX = 300, CZ = 150, CY = (float)H + 3.0f, HalfY = 0.6f, HalfX = 5f, HalfZ = 10f, Flags = ColliderFlags.NoClimb });
            g.Register(t.Tile.Key, c);
            foreach (VehicleSpec spec in Bodies())
            foreach (double lane in new[] { 100.0, 200.0, 300.0 })
            {
                ArcadeVehicle v = At(spec, g, x0 + lane, z0 + 120, 0f);
                float maxY = v.Y;
                for (int i = 0; i < 60 * 14; i++)
                {
                    v.Step(new DriveInput(1f, 0f, 0f), Dt60, g, 0f);
                    if (!v.Airborne) maxY = Math.Max(maxY, v.Y); // stuck-recovery hops fly higher, briefly
                }
                string what = spec.Name + " lane " + lane;
                if (lane == 100.0)
                {
                    Assert.That(v.Z - z0, Is.GreaterThan(165.0), what + ": a 30 cm plinth is ridden over");
                    Assert.That(maxY, Is.EqualTo((float)H + 0.3f).Within(0.02f), what);
                }
                else if (lane == 200.0)
                {
                    Assert.That(v.Z + FrontReach(v), Is.LessThanOrEqualTo(z0 + 140 + 1e-3), what + ": a 0.9 m terrace is a wall");
                    Assert.That(maxY, Is.LessThan((float)H + 0.1f), what);
                }
                else
                {
                    bool fits = v.Body.HeightM < 2.4f;
                    if (fits) Assert.That(v.Z - z0, Is.GreaterThan(165.0), what + " passes under the 2.4 m overhang");
                    else Assert.That(v.Z + FrontReach(v), Is.LessThanOrEqualTo(z0 + 140 + 1e-3), what + " is too tall for it");
                }
            }
        }

        [Test]
        public void FootprintSolidsFollowTheData()
        {
            var sacred = House(300, 100, 320, 120, 12, 0, BuildingArchetype.TemplePagoda);
            var raised = House(100, 300, 140, 310, 9, 5); // an upper-floor part from 5 m (a passage below)
            var canopy = House(300, 300, 320, 320, 5, 0, BuildingArchetype.Generic, BuildingFlags.OpenCanopy);
            var hidden = House(500, 500, 520, 520);
            var parts = House(600, 600, 640, 640, 9, 0, BuildingArchetype.Generic, BuildingFlags.HasParts);
            TileData t = FlatTile(House(100, 100, 120, 120), sacred, raised, canopy, hidden, parts);
            t.Props.Add(new PropRecord { Kind = ObjectKind.Tree, XCm = 20000, ZCm = 20000, HeightDm = 120 });
            t.Props.Add(new PropRecord { Kind = ObjectKind.Artwork, XCm = 21000, ZCm = 20000 });
            t.Props.Add(new PropRecord { Kind = ObjectKind.Helipad, XCm = 22000, ZCm = 20000 });
            var g = new TileGroundQuery { HiddenBuildingRefs = new HashSet<ulong> { hidden.OsmRef } };
            g.Add(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            float feet = (float)H;
            Assert.That(g.InsideSolid(x0 + 110, z0 + 110, feet), Is.True, "a house");
            Assert.That(g.InsideSolid(x0 + 310, z0 + 110, feet), Is.False, "temples are the generators' (walkable plinths)");
            Assert.That(g.InsideSolid(x0 + 120, z0 + 305, feet), Is.False, "a part from 5 m leaves room below");
            Assert.That(g.InsideSolid(x0 + 120, z0 + 305, feet + 5.5f), Is.True, "but is solid at its own height");
            Assert.That(g.InsideSolid(x0 + 310, z0 + 310, feet), Is.False, "under an open canopy");
            Assert.That(g.IsBlocked(x0 + 300, z0 + 300, feet), Is.True, "its corner posts");
            Assert.That(g.InsideSolid(x0 + 510, z0 + 510, feet), Is.False, "hidden for a hero replica");
            Assert.That(g.InsideSolid(x0 + 620, z0 + 620, feet), Is.False, "an outline drawn by its parts");
            Assert.That(g.IsBlocked(x0 + 200, z0 + 200, feet), Is.True, "a tree trunk");
            Assert.That(g.IsBlocked(x0 + 210, z0 + 200, feet), Is.True, "a statue");
            Assert.That(g.IsBlocked(x0 + 220, z0 + 200, feet), Is.False, "a helipad is flat");

            // A motorbike rides through the passage under the raised part.
            ArcadeVehicle v = At(VehicleSpec.Motorbike(), g, x0 + 120, z0 + 290, 0f);
            for (int i = 0; i < 60 * 4; i++) v.Step(new DriveInput(0.5f, 0f, 0f), Dt60, g, 0f);
            Assert.That(v.Z - z0, Is.GreaterThan(315.0));
        }

        [Test]
        public void FootprintsNeverBlockTheRoadCorridor()
        {
            // A street along z = 200 and a house whose front reaches 2 m into its carriageway.
            TileData t = FlatTile(House(140, 196, 170, 215));
            t.Roads.Add(DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 6, RoadFlags.None, 20, 200, 400, 200));
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            GroundSample s;
            Assert.That(g.TrySample(x0 + 155, z0 + 199, out s) && s.OnRoad, Is.True);
            float half = s.RoadHalfWidthM;
            Assert.That(g.InsideSolid(x0 + 155, z0 + 200 + half - 0.2, (float)H), Is.False, "the carriageway stays clear");
            Assert.That(g.InsideSolid(x0 + 155, z0 + 210, (float)H), Is.True, "the rest of the house is solid");
            // A car rides the whole street along its left edge, past the house, without touching it.
            ArcadeVehicle v = At(VehicleSpec.For(HandlingPreset.Hatchback), g, x0 + 40, z0 + 200 + half - 1.0, 90f * Deg);
            v.RoadAssist = false;
            v.SpeedMps = 10f;
            StepEvents ev = StepEvents.None;
            for (int i = 0; i < 60 * 40 && v.X < x0 + 300; i++) ev |= v.Step(new DriveInput(0.5f, 0f, 0f), Dt60, g, 0f);
            Assert.That(v.X - x0, Is.GreaterThan(300.0));
            Assert.That(ev & StepEvents.HitWall, Is.EqualTo(StepEvents.None));
        }

        [Test]
        public void DressingCollidersBlock()
        {
            TileData t = FlatTile();
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            var c = new StructureColliders();
            c.AddTree(100, 150, (float)H, 10f);
            c.AddPole(200, 150, (float)H, 9f, 0.15f);
            c.AddVehicle(300, 150, (float)H, 90f, 1); // a parked motorbike across the lane
            var gen = new GenColliders();
            gen.AddCylinder(400, 150, H, H + 4, 1.0); // a roundabout statue
            gen.AddWall(480, 150, 520, 150, H, H + 1.1, 0.08); // a parapet
            c.AddFrom(gen);
            g.Register(t.Tile.Key, c);
            foreach (double lane in new[] { 100.0, 200.0, 300.0, 400.0, 500.0 })
            {
                ArcadeVehicle v = At(VehicleSpec.Motorbike(), g, x0 + lane, z0 + 120, 0f);
                v.SpeedMps = 15f;
                for (int i = 0; i < 60 * 4; i++) v.Step(new DriveInput(0.6f, 0f, 0f), Dt60, g, 0f);
                Assert.That(v.Z - z0, Is.LessThan(150.0), "lane " + lane);
                Assert.That(v.Z - z0, Is.GreaterThan(140.0), "lane " + lane + " rode up to it");
            }
            g.Unregister(t.Tile.Key);
            ArcadeVehicle free = At(VehicleSpec.Motorbike(), g, x0 + 100, z0 + 120, 0f);
            for (int i = 0; i < 60 * 6; i++) free.Step(new DriveInput(0.6f, 0f, 0f), Dt60, g, 0f);
            Assert.That(free.Z - z0, Is.GreaterThan(160.0), "gone with the tile");
        }

        [Test]
        public void TurningIntoAWallIsPushedBackOrUndone()
        {
            TileData t = FlatTile(House(0, 104.5, 400, 130));
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            // A bus parked along the wall (heading east), turning left at walking pace: its nose swings into the wall.
            ArcadeVehicle v = At(VehicleSpec.For(HandlingPreset.Bus), g, x0 + 100, z0 + 102.5, 90f * Deg);
            for (int i = 0; i < 60 * 5; i++)
            {
                v.Step(new DriveInput(0.3f, 0f, -1f), Dt60, g, 0f);
                double px, pz;
                bool pen = g.Penetration(v.Body, v.X, v.Z, v.HeadingRad, v.Y, out px, out pz);
                Assert.That(!pen || Math.Sqrt(px * px + pz * pz) < 0.06, Is.True, "no deep overlap");
            }
            Assert.That(g.InsideSolid(v.X, v.Z, v.Y), Is.False);
        }

        [Test]
        public void SolidsAreDeterministic()
        {
            TileData t = DrivingData.SampleTiles()[TileId.At(10, TrafficX(), TrafficZ())];
            var a = new TileGroundQuery();
            var b = new TileGroundQuery();
            a.Add(t);
            b.Add(t);
            var pa = new List<SolidPrim>();
            var pb = new List<SolidPrim>();
            Assert.That(a.SolidsOf(t.Tile, pa), Is.GreaterThan(1000), "Thamel's houses");
            b.SolidsOf(t.Tile, pb);
            Assert.That(pb.Count, Is.EqualTo(pa.Count));
            for (int i = 0; i < pa.Count; i++)
            {
                Assert.That(pb[i].Ax, Is.EqualTo(pa[i].Ax));
                Assert.That(pb[i].Bz, Is.EqualTo(pa[i].Bz));
                Assert.That(pb[i].Top, Is.EqualTo(pa[i].Top));
            }
        }

        private static double TrafficX()
        {
            double x, z;
            DrivingData.ThamelMarg(out x, out z);
            return x;
        }

        private static double TrafficZ()
        {
            double x, z;
            DrivingData.ThamelMarg(out x, out z);
            return z;
        }

        [Test]
        public void ThamelRidesNeverEnterAHouse()
        {
            TileGroundQuery g = DrivingData.SampleGround();
            Assert.That(g.SolidCount, Is.GreaterThan(5000));
            double x, z;
            DrivingData.ThamelMarg(out x, out z);
            var rng = new Random(7);
            foreach (VehicleSpec spec in new[] { VehicleSpec.Motorbike(), VehicleSpec.Walker(), VehicleSpec.For(HandlingPreset.Hatchback) })
            {
                ArcadeVehicle v = At(spec, g, x, z, 0.4f);
                float steer = 0f;
                for (int i = 0; i < 60 * 90; i++)
                {
                    if (i % 45 == 0) steer = (float)(rng.NextDouble() * 2 - 1);
                    v.Step(new DriveInput(1f, 0f, steer), Dt60, g, 0f);
                    for (int k = 0; k < v.Body.Circles; k++)
                    {
                        double cx, cz;
                        v.Body.Centre(k, v.X, v.Z, v.HeadingRad, out cx, out cz);
                        Assert.That(g.InsideSolid(cx, cz, v.Y), Is.False, spec.Name + " inside a house at step " + i);
                    }
                }
            }
        }
    }
}
