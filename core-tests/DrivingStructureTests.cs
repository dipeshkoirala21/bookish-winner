using System;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Structure colliders in the ground query, foot surfaces, and the vehicle catalogue with its plates, seats
    /// and procedural models (W2_DESIGN 3.3, 5.2, 6.2, 10.3, 10.4).</summary>
    public class DrivingStructureTests
    {
        private const double H = 1300.0;

        private static TileGroundQuery FlatGround(out TileData t)
        {
            t = DrivingData.Flat(10, 520, 160, H, Biome.UrbanDense);
            var g = new TileGroundQuery();
            g.Add(t);
            return g;
        }

        private static OrientedBox Box(double cx, double cz, float bottom, float top, float halfX, float halfZ, ColliderFlags f,
                                       FootSurface m = FootSurface.Stone, float yaw = 0f)
        {
            return new OrientedBox
            {
                CX = cx, CZ = cz, CY = 0.5f * (bottom + top), HalfY = 0.5f * (top - bottom), HalfX = halfX, HalfZ = halfZ,
                YawRad = yaw, Flags = f, Material = m,
            };
        }

        [Test]
        public void WalkableTopsRaiseTheGroundAndNoClimbBoxesBlock()
        {
            TileData t;
            TileGroundQuery g = FlatGround(out t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            GroundSample s;
            Assert.That(g.TrySample(x0 + 100, z0 + 100, out s), Is.True);
            Assert.That(s.Height, Is.EqualTo((float)H).Within(0.1f));
            Assert.That(s.Foot, Is.EqualTo(FootSurface.Concrete), "UrbanDense biome");
            Assert.That(s.OnStructure, Is.False);

            var c = new StructureColliders();
            c.AddBox(Box(100, 100, (float)H - 1f, (float)H + 0.4f, 3f, 3f, ColliderFlags.Walkable)); // plinth: one step
            c.AddBox(Box(200, 100, (float)H - 1f, (float)H + 1.2f, 3f, 3f, ColliderFlags.Walkable, FootSurface.Wood)); // dabali
            c.AddBox(Box(300, 100, (float)H, (float)H + 8f, 2f, 4f, ColliderFlags.NoClimb | ColliderFlags.SoftMargin, FootSurface.Brick,
                         0.5f)); // temple wall
            c.AddRamp(new StepRamp { X0 = 200, Z0 = 90, X1 = 200, Z1 = 96.9, Y0 = (float)H, Y1 = (float)H + 1.2f, HalfWidth = 1f,
                                     Material = FootSurface.Stone });
            ulong key = t.Tile.Key;
            g.Register(key, c);

            Assert.That(g.TrySample(x0 + 100, z0 + 100, (float)H, out s), Is.True);
            Assert.That(s.Height, Is.EqualTo((float)H + 0.4f).Within(1e-3f), "step onto the plinth");
            Assert.That(s.OnStructure, Is.True);
            Assert.That(s.Foot, Is.EqualTo(FootSurface.Stone));

            // The dabali is 1.2 m high: a wall from the street, ground from its own top, reachable by the stair.
            Assert.That(g.TrySample(x0 + 200, z0 + 100, (float)H, out s), Is.False, "too high to step onto");
            Assert.That(g.IsBlocked(x0 + 200, z0 + 100, (float)H), Is.True);
            Assert.That(g.TrySample(x0 + 200, z0 + 100, (float)H + 1.2f, out s), Is.True);
            Assert.That(s.Height, Is.EqualTo((float)H + 1.2f).Within(1e-3f));
            Assert.That(s.Foot, Is.EqualTo(FootSurface.Wood));
            Assert.That(g.TrySample(x0 + 200, z0 + 93.45, (float)H + 0.5f, out s), Is.True, "on the stair");
            Assert.That(s.Height, Is.EqualTo((float)H + 0.6f).Within(0.02f));

            // The temple wall blocks at every height, plus the soft margin around it.
            Assert.That(g.TrySample(x0 + 300, z0 + 100, (float)H, out s), Is.False);
            if (g.TrySample(x0 + 300, z0 + 100, (float)H + 8.5f, out s))
                Assert.That(s.Height, Is.LessThan((float)H + 1f), "a no-climb top is never ground");
            Assert.That(g.IsBlocked(x0 + 300, z0 + 100, (float)H), Is.True);
            double ux = Math.Cos(0.5), uz = Math.Sin(0.5); // the wall's local X axis
            Assert.That(g.IsBlocked(x0 + 300 + ux * 2.6, z0 + 100 + uz * 2.6, (float)H), Is.True, "soft margin");
            Assert.That(g.IsBlocked(x0 + 300 + ux * 3.5, z0 + 100 + uz * 3.5, (float)H), Is.False);
            Assert.That(g.IsBlocked(x0 + 50, z0 + 50, (float)H), Is.False);

            g.Unregister(key);
            Assert.That(g.TrySample(x0 + 300, z0 + 100, (float)H, out s), Is.True);
            Assert.That(s.Height, Is.EqualTo((float)H).Within(0.1f));
            Assert.That(g.IsBlocked(x0 + 200, z0 + 100, (float)H), Is.False);
        }

        [Test]
        public void VehiclesStopAtWallsAndRideOverPlinths()
        {
            TileData t;
            TileGroundQuery g = FlatGround(out t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            var c = new StructureColliders();
            c.AddBox(Box(100, 130, (float)H, (float)H + 6f, 10f, 1f, ColliderFlags.NoClimb));
            c.AddBox(Box(200, 115, (float)H - 0.5f, (float)H + 0.15f, 10f, 3f, ColliderFlags.Walkable));
            g.Register(t.Tile.Key, c);
            foreach (double lane in new[] { 100.0, 200.0 })
            {
                var v = new ArcadeVehicle(VehicleSpec.For(HandlingPreset.Hatchback));
                v.Teleport(x0 + lane, z0 + 100, 0f, g);
                for (int i = 0; i < 60 * 6; i++) v.Step(new DriveInput(1f, 0f, 0f), 1f / 60f, g, 0f);
                if (lane == 100.0) Assert.That(v.Z - z0, Is.LessThan(129.0), "the wall stops the car");
                else Assert.That(v.Z - z0, Is.GreaterThan(140.0), "a low plinth is driven over");
            }
        }

        [Test]
        public void FootSurfacesFollowRoadsAreasAndWetness()
        {
            Assert.That(FootSurfaces.OfRoad(Surface.Asphalt, RoadClass.Primary), Is.EqualTo(FootSurface.Asphalt));
            Assert.That(FootSurfaces.OfBiome(Biome.Water), Is.EqualTo(FootSurface.Water));
            FootSurface f;
            Assert.That(FootSurfaces.TryOfArea(AreaKind.Religious, out f) && f == FootSurface.Stone, Is.True);
            Assert.That(FootSurfaces.Effective(FootSurface.Dirt, 0.5f), Is.EqualTo(FootSurface.Mud));
            Assert.That(FootSurfaces.Effective(FootSurface.Dirt, 0.4f), Is.EqualTo(FootSurface.Dirt));
            Assert.That(FootSurfaces.GroupOf(FootSurface.Mud), Is.EqualTo(SurfaceGroup.Mud));
            Assert.That(FootSurfaces.GroupOf(FootSurface.Brick), Is.EqualTo(SurfaceGroup.Paved));
        }

        [Test]
        public void TheCatalogueIsConsistent()
        {
            Assert.That(VehicleCatalog.Count, Is.EqualTo(23));
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                Assert.That(VehicleCatalog.IndexOf(e.AssetId, e.Variant), Is.EqualTo(v), e.AssetId);
                Assert.That(e.AssetId, Does.StartWith("ghm_veh_"));
                VehicleSpec s = e.Spec();
                Assert.DoesNotThrow(s.Validate, e.AssetId);
                Assert.That(e.LiveryCount, Is.GreaterThan(0));
                Assert.That(e.LengthM, Is.GreaterThan(e.WheelbaseM));
                byte l1 = e.PickLivery(1234u), l2 = e.PickLivery(1234u);
                Assert.That(l1, Is.EqualTo(l2));
                Assert.That(l1, Is.LessThan(e.LiveryCount));
            }
            Assert.That(VehicleCatalog.IndexOf("ghm_veh_bus_city_a", "minibus"), Is.EqualTo(VehicleCatalog.Minibus));
            Assert.That(VehicleCatalog.IndexOf("ghm_veh_bus_city_a", "city_green"), Is.EqualTo(VehicleCatalog.BusCityGreen));
            Assert.That(VehicleCatalog.IndexOf("ghm_veh_nope", ""), Is.LessThan(0));
        }

        [Test]
        public void PlatesAreFictionalNepaliPlatesTheFontCanDraw()
        {
            Assert.That(VehiclePlates.Devanagari(0), Is.EqualTo("०"));
            Assert.That(VehiclePlates.Devanagari(4567), Is.EqualTo("४५६७"));
            Assert.That(VehiclePlates.Letter(PlateOwnership.Private, PlateSize.Light), Is.EqualTo("च"));
            Assert.That(VehiclePlates.Letter(PlateOwnership.Public, PlateSize.Heavy), Is.EqualTo("ख"));
            Assert.That(VehiclePlates.Letter(PlateOwnership.Private, PlateSize.TwoWheeler), Is.EqualTo("प"));
            Assert.That(VehiclePlates.Letter(PlateOwnership.Government, PlateSize.Light), Is.EqualTo("झ"));
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                for (uint seed = 0; seed < 50; seed++)
                {
                    PlateText p = VehiclePlates.For(e, seed);
                    Assert.That(p.Full, Does.StartWith(VehiclePlates.Zone + " "));
                    Assert.That(p.Full, Is.EqualTo(p.Line1 + " " + p.Line2));
                    Assert.That(p.Line2.Length, Is.EqualTo(4), "number 1000–9999");
                    foreach (char ch in p.Full) Assert.That(PlateGlyphs.Has(ch), Is.True, "glyph for U+" + ((int)ch).ToString("X4"));
                    Assert.That(VehiclePlates.For(e, seed).Full, Is.EqualTo(p.Full), "deterministic");
                    Assert.That(p.WidthM, Is.GreaterThan(0.1f));
                }
            }
        }

        [Test]
        public void SeatsFollowTheNepalEntryRules()
        {
            SeatSocket[] scooter = VehicleSeats.For(VehicleCatalog.Scooter);
            Assert.That(scooter[0].Kind, Is.EqualTo(SeatKind.Driver));
            Assert.That(scooter[0].Side, Is.EqualTo(EntrySide.Left));
            Assert.That(scooter[0].EntryX, Is.LessThan(0f));
            Assert.That(scooter[0].DetectM, Is.EqualTo(VehicleSeats.TwoWheelerDetectM));

            SeatSocket[] taxi = VehicleSeats.For(VehicleCatalog.Taxi);
            Assert.That(taxi[0].Kind, Is.EqualTo(SeatKind.Driver));
            Assert.That(taxi[0].Side, Is.EqualTo(EntrySide.Right), "right-hand drive");
            Assert.That(taxi[0].X, Is.GreaterThan(0f));
            for (int i = 1; i < taxi.Length; i++) Assert.That(taxi[i].Side, Is.EqualTo(EntrySide.Left), "passengers at the kerb");

            SeatSocket[] bus = VehicleSeats.For(VehicleCatalog.BusCityGreen);
            Assert.That(bus.Length, Is.GreaterThan(4));
            for (int i = 1; i < bus.Length; i++)
            {
                Assert.That(bus[i].Side, Is.EqualTo(EntrySide.Left), "front-left door");
                Assert.That(bus[i].DetectM, Is.EqualTo(VehicleSeats.HeavyDetectM));
            }
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                SeatSocket[] s = VehicleSeats.For(v);
                Assert.That(s.Length, Is.GreaterThan(0), e.AssetId);
                // The rickshaw's puller is an NPC: the player rides it only as a passenger.
                Assert.That(s[0].Kind, Is.EqualTo(e.Shape == BodyShape.Rickshaw ? SeatKind.Passenger : SeatKind.Driver), e.AssetId);
                foreach (SeatSocket k in s)
                {
                    Assert.That(Math.Abs(k.X), Is.LessThanOrEqualTo(0.5f * e.WidthM), e.AssetId + " seat inside the body");
                    Assert.That(Math.Abs(k.EntryX), Is.GreaterThan(0.5f * e.WidthM - 0.01f).Or.EqualTo(0f), e.AssetId + " entry outside");
                    Assert.That(k.EnterS, Is.GreaterThan(0f));
                }
            }
        }

        [Test]
        public void ModelsStayWithinTheirBudgetsAndBoxes()
        {
            var m = new MeshData(4096, 12288);
            var m2 = new MeshData(4096, 12288);
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                WheelSocket[] wheels = VehicleMesher.Wheels(e);
                Assert.That(wheels.Length, Is.GreaterThanOrEqualTo(2), e.AssetId);
                for (int l = 0; l < e.LiveryCount; l++)
                {
                    foreach (VehicleLod lod in new[] { VehicleLod.Lod0, VehicleLod.Lod1, VehicleLod.Lod2, VehicleLod.Block, VehicleLod.Box })
                    {
                        m.Clear();
                        int tris = VehicleMesher.Build(v, (byte)l, lod, m, 77u, true);
                        if (lod <= VehicleLod.Lod1)
                            foreach (WheelSocket w in wheels) tris += VehicleMesher.BuildWheel(w.Radius, w.Width, lod, m);
                        Assert.That(tris, Is.GreaterThan(0));
                        Assert.That(tris, Is.LessThanOrEqualTo(VehicleMesher.Budget(lod)), e.AssetId + " " + lod);
                    }
                }
                // The LOD0 body fits the catalogue box (cartoon overlay: wheels ×1.15 and cab ×1.1 bulge a little).
                m.Clear();
                VehicleMesher.Build(v, 0, VehicleLod.Lod0, m, 5u);
                float minX, minY, minZ, maxX, maxY, maxZ;
                m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
                Assert.That(maxZ - minZ, Is.EqualTo(e.LengthM).Within(e.LengthM * 0.12f + 0.15f), e.AssetId + " length");
                Assert.That(maxX - minX, Is.LessThanOrEqualTo(e.WidthM * 1.12f + 0.05f), e.AssetId + " width");
                Assert.That(maxY, Is.LessThanOrEqualTo(e.HeightM * 1.15f + 0.05f), e.AssetId + " height");
                Assert.That(minY, Is.GreaterThanOrEqualTo(-0.01f), e.AssetId + " above the ground");

                // Deterministic.
                m2.Clear();
                VehicleMesher.Build(v, 0, VehicleLod.Lod0, m2, 5u);
                Assert.That(m2.VertexCount, Is.EqualTo(m.VertexCount));
                for (int i = 0; i < m.VertexCount * 3; i++) Assert.That(m2.Positions[i], Is.EqualTo(m.Positions[i]));
            }
        }
    }
}
