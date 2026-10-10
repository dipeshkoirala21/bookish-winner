using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing.Bridges;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Layered ground (docs/W2_DETAIL_CONTRACT.md §3): on a bridge or flyover the deck carries you, under it
    /// the road below stays the ground, you are never snapped up or stuck, underpass profiles hold, low decks block
    /// tall bodies, and the bridges package's deck query takes over where it answers.</summary>
    public class DrivingLayeredGroundTests
    {
        private const double H = 1300.0;
        private const float Dt60 = 1f / 60f;
        private const float Deg = (float)(Math.PI / 180.0);
        private const float Top = 1307f; // flyover deck: 7 m over the road below (5.8 m free under its 1.2 m slab)

        /// <summary>
        /// A flat tile with a road along z = 500 (x 100..900, draped) and a flyover along x = 500 (z 150..850) crossing
        /// over it: ramps from grade at z 150 / 850 to <paramref name="top"/> over z 350..650, railings 1.1 m.
        /// </summary>
        internal static TileData Flyover(float top = Top, RoadStructureKind lowerKind = RoadStructureKind.None, float lowerDrop = 0f)
        {
            TileData t = DrivingData.Flat(10, 520, 160, H);
            t.Roads.Add(DrivingData.Road(RoadClass.Primary, Surface.Asphalt, 10, RoadFlags.None, 100, 500, 300, 500, 500, 500, 700, 500, 900, 500));
            t.Roads.Add(DrivingData.Road(RoadClass.Trunk, Surface.Concrete, 9, RoadFlags.Bridge,
                                         500, 150, 500, 250, 500, 350, 500, 450, 500, 550, 500, 650, 500, 750, 500, 850));
            float grade = (float)H + 0.3f;
            var lower = new RoadStructureRecord { Kind = lowerKind, Flags = RoadStructureFlags.CarAccessible };
            if (lowerKind == RoadStructureKind.Underpass)
                lower.DeckY = new[] { grade, grade - 0.5f * lowerDrop, grade - lowerDrop, grade - 0.5f * lowerDrop, grade };
            t.RoadStructures.Add(lower);
            t.RoadStructures.Add(new RoadStructureRecord
            {
                Kind = RoadStructureKind.Flyover, Layer = 1, Flags = RoadStructureFlags.CarAccessible, RailingHeightM = 1.1f, ClearanceM = 0f,
                DeckY = new[] { grade, 0.5f * (grade + top), top, top, top, top, 0.5f * (grade + top), grade },
            });
            return t;
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

        [Test]
        public void LayeredGroundPicksTheRightLevel()
        {
            TileData t = Flyover();
            TileGroundQuery g = Ground(t);
            double x = t.Tile.X0 + 500, z = t.Tile.Z0 + 500;
            GroundSample s;
            // Without a hint (teleports): the deck.
            Assert.That(g.TrySample(x, z, out s), Is.True);
            Assert.That(s.Height, Is.EqualTo(Top).Within(1e-3f));
            Assert.That(s.OnDeck, Is.True);
            Assert.That(s.RoadClass, Is.EqualTo(RoadClass.Trunk));
            // From the deck: the deck. From the road below: the road below, never the deck 7 m up.
            Assert.That(g.TrySample(x, z, Top, out s), Is.True);
            Assert.That(s.OnDeck && s.RoadClass == RoadClass.Trunk, Is.True);
            Assert.That(g.TrySample(x, z, (float)H + 0.3f, out s), Is.True);
            Assert.That(s.OnDeck, Is.False);
            Assert.That(s.RoadClass, Is.EqualTo(RoadClass.Primary));
            Assert.That(s.Height, Is.EqualTo(s.TerrainHeight + g.RoadLiftM(t.Roads[0])).Within(0.01f));
            // Halfway up a ramp: the ramp from its own surface, the ground beside it from below.
            Assert.That(g.TrySample(x, t.Tile.Z0 + 250, (float)H + 3.6f, out s), Is.True);
            Assert.That(s.OnDeck, Is.True);
            Assert.That(s.Height, Is.EqualTo(0.5f * ((float)H + 0.3f + Top)).Within(0.01f));
            Assert.That(s.Ny, Is.LessThan(0.9999f), "the ramp's slope, not the flat ground");
            // Beside the deck (beyond its kerb): the ground below.
            Assert.That(g.TrySample(x + 8, z, Top, out s), Is.True);
            Assert.That(s.OnDeck, Is.False);
            Assert.That(s.Height, Is.LessThan((float)H + 1f));
            // The deck slab over the road below is high enough for a bus; a low span would block.
            Assert.That(g.IsBlocked(x, z, (float)H + 0.3f), Is.False);
            Assert.That(Ground(Flyover(1302.5f)).IsBlocked(x, z, (float)H + 0.3f), Is.True, "a 1 m clearance blocks a walker");
        }

        [Test]
        public void RideTheFlyoverAndPassUnderIt()
        {
            TileData t = Flyover();
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            foreach (VehicleSpec spec in new[] { VehicleSpec.Motorbike(), VehicleSpec.For(HandlingPreset.Hatchback), VehicleSpec.For(HandlingPreset.Bus) })
            {
                // Up the ramp, over the top, down the other side.
                ArcadeVehicle over = At(spec, g, x0 + 500, z0 + 160, 0f);
                over.SpeedMps = 10f;
                float maxY = float.MinValue;
                StepEvents ev = StepEvents.None;
                for (int i = 0; i < 60 * 150 && over.Z < z0 + 840; i++)
                {
                    ev |= over.Step(new DriveInput(0.9f, 0f, 0f), Dt60, g, 0f);
                    maxY = Math.Max(maxY, over.Y);
                    double along = over.Z - z0;
                    if (along > 360 && along < 640) Assert.That(over.Y, Is.EqualTo(Top).Within(0.05f), spec.Name + " on the deck at " + along);
                }
                Assert.That(over.Z - z0, Is.GreaterThanOrEqualTo(840.0), spec.Name + " crossed the flyover");
                Assert.That(maxY, Is.EqualTo(Top).Within(0.05f));
                Assert.That(ev & (StepEvents.Landed | StepEvents.HitWall | StepEvents.StuckRecovered), Is.EqualTo(StepEvents.None), spec.Name);

                // Along the road below, under the flyover: never snapped up, never stuck.
                ArcadeVehicle under = At(spec, g, x0 + 150, z0 + 500, 90f * Deg);
                under.SpeedMps = 10f;
                float topY = float.MinValue;
                StepEvents uev = StepEvents.None;
                for (int i = 0; i < 60 * 150 && under.X < x0 + 850; i++)
                {
                    uev |= under.Step(new DriveInput(0.9f, 0f, 0f), Dt60, g, 0f);
                    topY = Math.Max(topY, under.Y);
                    Assert.That(under.Ground.OnDeck, Is.False);
                }
                Assert.That(under.X - x0, Is.GreaterThanOrEqualTo(850.0), spec.Name + " passed under");
                Assert.That(topY, Is.LessThan((float)H + 0.6f), spec.Name + " stayed on the road below");
                Assert.That(uev & (StepEvents.HitWall | StepEvents.StuckRecovered | StepEvents.Landed), Is.EqualTo(StepEvents.None), spec.Name);
            }
        }

        [Test]
        public void RailingsKeepYouOnTheDeckAndOutOfTheRampSide()
        {
            TileData t = Flyover();
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            // On the top, swerve hard towards the edge: the railing holds the bike on the deck.
            ArcadeVehicle v = At(VehicleSpec.Motorbike(), g, x0 + 500, z0 + 400, 0f);
            Assert.That(v.Y, Is.EqualTo(Top).Within(0.01f));
            v.SpeedMps = 12f;
            for (int i = 0; i < 60 * 6; i++)
            {
                v.Step(new DriveInput(0.6f, 0f, i < 60 ? 1f : 0f), Dt60, g, 0f);
                if (v.Z - z0 < 640) Assert.That(v.Y, Is.GreaterThan(Top - 0.1f), "never fell off the deck");
            }
            // Beside the ramp where it is 2 m up: a walker running at its side is stopped, never lifted onto it.
            ArcadeVehicle w = At(VehicleSpec.Walker(), g, x0 + 490, z0 + 200, 90f * Deg);
            for (int i = 0; i < 60 * 6; i++)
            {
                w.Step(new DriveInput(1f, 0f, 0f), Dt60, g, 0f);
                Assert.That(w.Y, Is.LessThan((float)H + 0.6f));
            }
            Assert.That(w.X - x0, Is.LessThan(497.0), "stopped at the ramp's railing");
        }

        [Test]
        public void UnderpassProfilesHold()
        {
            // The road below dips 2 m under the flyover (a lowered underpass between retaining walls).
            TileData t = Flyover(Top, RoadStructureKind.Underpass, 2f);
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            GroundSample s;
            Assert.That(g.TrySample(x0 + 500, z0 + 500, (float)H - 1.7f, out s), Is.True);
            Assert.That(s.Height, Is.EqualTo((float)H + 0.3f - 2f).Within(0.01f), "the lowered profile");
            Assert.That(s.Ny, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(g.TrySample(x0 + 400, z0 + 500, (float)H, out s), Is.True);
            Assert.That(s.Height, Is.EqualTo((float)H + 0.3f - 1.5f).Within(0.01f), "on the way down");
            Assert.That(s.Ny, Is.LessThan(0.999995f), "on its slope");
            // Beside the lowered road: the terrain (a hard edge), not a ramp up the wall.
            Assert.That(g.TrySample(x0 + 500, z0 + 500 + 6.5, (float)H, out s), Is.True);
            Assert.That(s.OnRoad, Is.False);
            Assert.That(s.Height, Is.EqualTo(s.TerrainHeight).Within(1e-4f));
            Assert.That(s.Height, Is.EqualTo((float)H).Within(0.06f));

            // A car runs the underpass end to end, and cannot climb out of it sideways.
            ArcadeVehicle v = At(VehicleSpec.For(HandlingPreset.Hatchback), g, x0 + 120, z0 + 500, 90f * Deg);
            v.SpeedMps = 8f;
            float minY = float.MaxValue;
            for (int i = 0; i < 60 * 150 && v.X < x0 + 880; i++)
            {
                v.Step(new DriveInput(0.7f, 0f, 0f), Dt60, g, 0f);
                minY = Math.Min(minY, v.Y);
            }
            Assert.That(v.X - x0, Is.GreaterThanOrEqualTo(880.0));
            Assert.That(minY, Is.EqualTo((float)H + 0.3f - 2f).Within(0.05f));
            ArcadeVehicle side = At(VehicleSpec.Motorbike(), g, x0 + 400, z0 + 500, 0f); // 1.5 m down, beside the flyover
            side.RoadAssist = false;
            for (int i = 0; i < 60 * 4; i++) side.Step(new DriveInput(0.5f, 0f, 0f), Dt60, g, 0f);
            Assert.That(side.Y, Is.LessThan((float)H - 1f), "the retaining edge holds it in the cut");
        }

        /// <summary>
        /// A flyover along x = 500 from a ramp foot at <paramref name="footZ"/> (0.3 m over the ground) up through the
        /// points <paramref name="midZ"/> (heights <paramref name="midY"/> over the ground) to a crest at
        /// <paramref name="crestZ"/> (<paramref name="top"/>) and back down symmetrically, over a road along z = 500;
        /// with <paramref name="approach"/>, a draped road joins the foot from z = 150.
        /// </summary>
        internal static TileData Ramp(float top, double footZ, double crestZ, double[] midZ, float[] midY, bool approach = false)
        {
            TileData t = DrivingData.Flat(10, 520, 160, H);
            t.Roads.Add(DrivingData.Road(RoadClass.Primary, Surface.Asphalt, 10, RoadFlags.None, 100, 500, 300, 500, 500, 500, 700, 500, 900, 500));
            var pts = new List<double> { 500, footZ };
            var ys = new List<float> { (float)H + 0.3f };
            for (int i = 0; i < midZ.Length; i++)
            {
                pts.Add(500);
                pts.Add(midZ[i]);
                ys.Add((float)H + midY[i]);
            }
            pts.Add(500);
            pts.Add(crestZ);
            ys.Add(top);
            for (int i = midZ.Length - 1; i >= 0; i--)
            {
                pts.Add(500);
                pts.Add(crestZ + (crestZ - midZ[i]));
                ys.Add((float)H + midY[i]);
            }
            pts.Add(500);
            pts.Add(crestZ + (crestZ - footZ));
            ys.Add((float)H + 0.3f);
            t.Roads.Add(DrivingData.Road(RoadClass.Trunk, Surface.Concrete, 9, RoadFlags.Bridge, pts.ToArray()));
            t.RoadStructures.Add(new RoadStructureRecord { Kind = RoadStructureKind.None, Flags = RoadStructureFlags.CarAccessible });
            t.RoadStructures.Add(new RoadStructureRecord
            {
                Kind = RoadStructureKind.Flyover, Layer = 1, Flags = RoadStructureFlags.CarAccessible, RailingHeightM = 1.1f, DeckY = ys.ToArray(),
            });
            if (approach)
            {
                t.Roads.Add(DrivingData.Road(RoadClass.Trunk, Surface.Concrete, 9, RoadFlags.None, 500, 150, 500, footZ));
                t.RoadStructures.Add(new RoadStructureRecord { Kind = RoadStructureKind.None, Flags = RoadStructureFlags.CarAccessible });
            }
            return t;
        }

        [Test]
        public void RailingsFollowASlopedDeckSoBusesPassUnderIt()
        {
            // The road below (z = 500) passes under the sloped stretch between a 3 m point at z = 430 and the 8.3 m crest
            // at z = 520: the deck there is 7.1 m up, 5.9 m free under its slab.
            TileData t = Ramp((float)H + 8.3f, 380, 520, new[] { 430.0 }, new[] { 3.0f });
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            var prims = new List<SolidPrim>();
            g.SolidsOf(t.Tile, prims);
            Assert.That(prims.Count, Is.GreaterThan(8), "railings on both sides of the ramps");
            foreach (SolidPrim p in prims)
                Assert.That(p.Top - p.Bottom, Is.LessThanOrEqualTo(1.1f + TileSolids.RailingBelowDeckM + TileSolids.RailingMaxRiseM + 0.01f),
                            "each railing piece hugs the local deck");
            foreach (VehicleSpec spec in new[] { VehicleSpec.Motorbike(), VehicleSpec.For(HandlingPreset.Hatchback), VehicleSpec.For(HandlingPreset.Truck),
                                                 VehicleSpec.For(HandlingPreset.Bus) })
            {
                ArcadeVehicle v = At(spec, g, x0 + 150, z0 + 500, 90f * Deg);
                v.SpeedMps = 10f;
                StepEvents ev = StepEvents.None;
                for (int i = 0; i < 60 * 150 && v.X < x0 + 850; i++) ev |= v.Step(new DriveInput(0.9f, 0f, 0f), Dt60, g, 0f);
                Assert.That(v.X - x0, Is.GreaterThanOrEqualTo(850.0), spec.Name + " (" + v.Body.HeightM + " m) passed under");
                Assert.That(ev & (StepEvents.HitWall | StepEvents.StuckRecovered | StepEvents.Landed), Is.EqualTo(StepEvents.None), spec.Name);
            }
        }

        [Test]
        public void BusesAndTrucksClimbRampsFromTheApproachRoad()
        {
            foreach (float grade in new[] { 0.06f, 0.07f, 0.08f })
            {
                const float rise = 7f;
                double run = rise / grade;
                var mids = new List<double>();
                var ys = new List<float>();
                for (double d = 25; d < run; d += 25)
                {
                    mids.Add(380 + d);
                    ys.Add(0.3f + (float)(d * grade));
                }
                TileData t = Ramp((float)H + 0.3f + rise, 380, 380 + run, mids.ToArray(), ys.ToArray(), true);
                TileGroundQuery g = Ground(t);
                double x0 = t.Tile.X0, z0 = t.Tile.Z0, end = 380 + 2 * run;
                foreach (VehicleSpec spec in new[] { VehicleSpec.For(HandlingPreset.Hatchback), VehicleSpec.For(HandlingPreset.Truck), VehicleSpec.For(HandlingPreset.Bus) })
                {
                    ArcadeVehicle v = At(spec, g, x0 + 500, z0 + 250, 0f); // on the approach road, 130 m before the foot
                    v.SpeedMps = 8f;
                    StepEvents ev = StepEvents.None;
                    float maxY = float.MinValue;
                    for (int i = 0; i < 60 * 150 && v.Z < z0 + end - 10; i++)
                    {
                        ev |= v.Step(new DriveInput(1f, 0f, 0f), Dt60, g, 0f);
                        maxY = Math.Max(maxY, v.Y);
                    }
                    string what = spec.Name + " at " + grade * 100 + "%";
                    Assert.That(v.Z - z0, Is.GreaterThanOrEqualTo(end - 10), what + " crossed the flyover");
                    Assert.That(maxY, Is.EqualTo((float)H + 0.3f + rise).Within(0.05f), what + " rode over the crest");
                    Assert.That(ev & (StepEvents.HitWall | StepEvents.StuckRecovered | StepEvents.Landed), Is.EqualTo(StepEvents.None), what);
                }
            }
        }

        [Test]
        public void ASparseFlyoverIsRiddenAndItsRailingsHold()
        {
            // Three points only (foot, crest, foot): the railings come from the deck walked metre by metre.
            TileData t = Ramp(1308f, 380, 505, new double[0], new float[0]);
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            foreach (VehicleSpec spec in new[] { VehicleSpec.Motorbike(), VehicleSpec.For(HandlingPreset.Bus) })
            {
                ArcadeVehicle v = At(spec, g, x0 + 500, z0 + 370, 0f);
                v.SpeedMps = 10f;
                StepEvents ev = StepEvents.None;
                for (int i = 0; i < 60 * 60 && v.Z < z0 + 625; i++) ev |= v.Step(new DriveInput(0.9f, 0f, 0f), Dt60, g, 0f);
                Assert.That(v.Z - z0, Is.GreaterThanOrEqualTo(625.0), spec.Name);
                Assert.That(ev & (StepEvents.HitWall | StepEvents.StuckRecovered | StepEvents.Landed), Is.EqualTo(StepEvents.None), spec.Name);
            }
            // Near the crest, swerving hard: held on the deck.
            ArcadeVehicle b = At(VehicleSpec.Motorbike(), g, x0 + 500, z0 + 470, 0f);
            float start = b.Y;
            Assert.That(start - (float)H, Is.GreaterThan(5f));
            b.SpeedMps = 10f;
            for (int i = 0; i < 60 * 3; i++)
            {
                b.Step(new DriveInput(0.5f, 0f, 1f), Dt60, g, 0f);
                Assert.That(b.Y, Is.GreaterThan(start - 0.6f), "never fell off the ramp");
            }
        }

        [Test]
        public void RealBridgesHaveRailingsThatHoldRiders()
        {
            var names = new[] { "बागमती पुल", "Kalopul", "Teku Dovaan marg" };
            int bridges = 0;
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10 || !t.HasDetail) continue;
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    RoadRecord r = t.Roads[ri];
                    NameRecord n = t.Name(r.NameRef);
                    if ((r.Flags & RoadFlags.Bridge) == 0 || n == null || Array.IndexOf(names, n.Default) < 0) continue;
                    var g = new TileGroundQuery();
                    foreach (TileData u in DrivingData.SampleTiles().Values)
                        if (u.Tile.Level == 10 && Math.Abs(u.Tile.X0 - t.Tile.X0) < 2000 && Math.Abs(u.Tile.Z0 - t.Tile.Z0) < 2000) g.Add(u);
                    int first, last;
                    RoadSpatialIndex.RenderedRange(r, out first, out last);
                    double ax, az, bx, bz;
                    t.LocalToGame(r.Points[2 * first], r.Points[2 * first + 1], out ax, out az);
                    t.LocalToGame(r.Points[2 * last], r.Points[2 * last + 1], out bx, out bz);
                    double span = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    if (span < 15) continue;
                    bridges++;
                    string what = n.Default + " " + t.Tile;

                    // Railing walls along both sides of the whole span.
                    var prims = new List<SolidPrim>();
                    g.SolidsOf(t.Tile, prims);
                    double railed = 0;
                    foreach (SolidPrim p in prims)
                    {
                        if (p.Group >= 0 || p.Ax == p.Bx && p.Az == p.Bz) continue;
                        double qx, qz;
                        SolidSet.Closest(ax, az, bx, bz, 0.5 * (p.Ax + p.Bx), 0.5 * (p.Az + p.Bz), out qx, out qz);
                        double mx = 0.5 * (p.Ax + p.Bx) - qx, mz = 0.5 * (p.Az + p.Bz) - qz;
                        if (mx * mx + mz * mz < 15 * 15) railed += Math.Sqrt((p.Bx - p.Ax) * (p.Bx - p.Ax) + (p.Bz - p.Az) * (p.Bz - p.Az));
                    }
                    Assert.That(railed, Is.GreaterThan(1.6 * span), what + ": railings along both sides (open only where a street joins at grade)");

                    // A motorbike swerving hard in the middle stays on the deck.
                    double mxw = 0.5 * (ax + bx), mzw = 0.5 * (az + bz);
                    float heading = (float)Math.Atan2(bx - ax, bz - az);
                    ArcadeVehicle v = At(VehicleSpec.Motorbike(), g, mxw - 10 * Math.Sin(heading), mzw - 10 * Math.Cos(heading), heading);
                    Assert.That(v.Ground.OnRoad, Is.True, what);
                    v.SpeedMps = 10f;
                    for (int i = 0; i < 60 * 3; i++)
                    {
                        v.Step(new DriveInput(0.5f, 0f, 1f), Dt60, g, 0f);
                        GroundSample deck;
                        Assert.That(g.TrySample(v.X, v.Z, out deck), Is.True);
                        double along = ((v.X - ax) * (bx - ax) + (v.Z - az) * (bz - az)) / (span * span);
                        if (along > 0.05 && along < 0.95)
                            Assert.That(v.Y, Is.GreaterThan(deck.Height - 0.3f), what + ": fell off the deck at step " + i);
                    }
                }
            }
            Assert.That(bridges, Is.GreaterThanOrEqualTo(3), "the Bagmati bridges of the sample");
        }

        [Test]
        public void AnUnderpassTrenchHoldsItsTraffic()
        {
            // The road below dips 0.8 m under the flyover: retaining walls along the trench, nobody climbs out sideways.
            TileData t = Flyover(Top, RoadStructureKind.Underpass, 0.8f);
            TileGroundQuery g = Ground(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            Assert.That(ArcadeVehicle.MaxClimbM, Is.LessThanOrEqualTo(TileGroundQuery.HardEdgeM), "a hard edge is never a step");
            Assert.That(ArcadeVehicle.MaxClimbM, Is.GreaterThan(g.RoadLiftM(RoadClass.Trunk) + g.RoadOptions.KerbHeightM),
                        "but the outer edge of a raised footpath is");
            Assert.That(g.IsBlocked(x0 + 500 - 30, z0 + 500 + 6.5, (float)H), Is.True, "a retaining wall beside the deepest stretch");
            foreach (VehicleSpec spec in new[] { VehicleSpec.Motorbike(), VehicleSpec.For(HandlingPreset.Hatchback), VehicleSpec.Walker() })
            {
                foreach (float steer in new[] { -1f, 1f })
                {
                    ArcadeVehicle v = At(spec, g, x0 + 470, z0 + 500, 90f * Deg);
                    float low = v.Y;
                    Assert.That(low, Is.LessThan((float)H - 0.2f), spec.Name + " starts in the trench");
                    if (!spec.TurnInPlace) v.SpeedMps = 6f;
                    for (int i = 0; i < 60 * 3; i++)
                    {
                        v.Step(new DriveInput(0.7f, 0f, steer), Dt60, g, 0f);
                        if (Math.Abs(v.X - x0 - 500) < 60) Assert.That(v.Y, Is.LessThan(low + 0.35f), spec.Name + " climbed out of the trench");
                    }
                }
            }
        }

        /// <summary>A deck the bridges package would report: a flat slab over a box at a given height.</summary>
        private sealed class FakeDecks : IBridgeDeckQuery
        {
            public double X0, Z0, X1, Z1;
            public float Y;
            public int Calls;

            public bool TryDeck(double x, double z, float nearY, out float deckY, out float nx, out float ny, out float nz)
            {
                Calls++;
                deckY = Y;
                nx = 0f;
                ny = 1f;
                nz = 0f;
                bool covers = x >= X0 && x <= X1 && z >= Z0 && z <= Z1;
                return covers && Y <= nearY + 0.6f;
            }

            public bool TryCeiling(double x, double z, float fromY, out float undersideY)
            {
                undersideY = Y - 1.2f;
                return x >= X0 && x <= X1 && z >= Z0 && z <= Z1 && undersideY > fromY;
            }
        }

        [Test]
        public void TheBridgesPackagesDecksTakeOver()
        {
            TileData t = Flyover();
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            var fake = new FakeDecks { X0 = x0 + 494, X1 = x0 + 506, Z0 = z0 + 300, Z1 = z0 + 700, Y = Top + 0.4f };
            var g = new TileGroundQuery { DeckFactory = tile => fake };
            g.Add(t);
            GroundSample s;
            Assert.That(g.TrySample(x0 + 500, z0 + 500, Top + 0.4f, out s), Is.True);
            Assert.That(s.Height, Is.EqualTo(Top + 0.4f).Within(1e-4f), "the drawn deck decides the height");
            Assert.That(s.OnDeck, Is.True);
            Assert.That(s.RoadClass, Is.EqualTo(RoadClass.Trunk), "the structure record names the road");
            Assert.That(g.TrySample(x0 + 500, z0 + 500, (float)H + 0.3f, out s), Is.True);
            Assert.That(s.OnDeck, Is.False, "from below: the road below");
            Assert.That(fake.Calls, Is.GreaterThan(0));
            // A global deck query works the same.
            var g2 = new TileGroundQuery { Decks = fake };
            g2.Add(t);
            Assert.That(g2.TrySample(x0 + 500, z0 + 500, Top + 0.4f, out s) && s.OnDeck, Is.True);
            Assert.That(s.Height, Is.EqualTo(Top + 0.4f).Within(1e-4f));
        }

        [Test]
        public void LanesAndWalkersFollowTheDeck()
        {
            TileData t = Flyover();
            var lanes = new Traffic.LaneGraph();
            lanes.AddTile(t.Tile, t);
            float maxY = float.MinValue, minY = float.MaxValue;
            int onFlyover = 0;
            foreach (Traffic.LaneId l in lanes.AllLanes())
            {
                Traffic.LaneInfo i = lanes.Info(l);
                if (i.Kind != Traffic.LaneKind.Road || i.RoadClass != RoadClass.Trunk) continue;
                onFlyover++;
                for (int k = 0; k < i.PointCount; k++)
                {
                    maxY = Math.Max(maxY, i.Y[k]);
                    minY = Math.Min(minY, i.Y[k]);
                }
            }
            Assert.That(onFlyover, Is.GreaterThan(0));
            Assert.That(maxY, Is.EqualTo(Top).Within(0.01f), "lanes ride the deck");
            Assert.That(minY, Is.LessThan((float)H + 1f), "down the ramps");
            // The deck has no footpath: nobody walks on it (the same street at grade has its two street edges).
            var bare = new Traffic.PedestrianSim(lanes, new Traffic.PedestrianSettings(), 1);
            bare.AddTile(t.Tile, t);
            TileData plain = Flyover();
            plain.RoadStructures.Clear();
            plain.Roads[1].Flags = RoadFlags.None;
            var atGrade = new Traffic.PedestrianSim(lanes, new Traffic.PedestrianSettings(), 1);
            atGrade.AddTile(plain.Tile, plain);
            Assert.That(atGrade.EdgeCount - bare.EdgeCount, Is.EqualTo(2), "the flyover's street edges are gone");
        }
    }
}
