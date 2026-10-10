using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The second facade pass (docs/research/w2/ref_buildings.md §12): dense Newar window grids, carved toranas, the
    /// timber bhaygah, and windows on every exposed side wall, within the clearance rules.
    /// </summary>
    public class MeshingBuildingDressingTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);

        private static TileData House(StyleProfile profile, BuildingArchetype arch, int levels, uint seed, double w, double d, bool shop, bool neighbours)
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            int X(double v) => (int)Math.Round(v * 100);
            void Add(double x0, uint s)
            {
                t.Buildings.Add(new BuildingRecord
                {
                    Archetype = arch, Levels = (byte)levels, Seed = s,
                    Rings = new[] { new[] { X(x0), X(500), X(x0 + w), X(500), X(x0 + w), X(500 + d), X(x0), X(500 + d) } },
                });
                var f = BuildingFrontRecord.Absent;
                f.Profile = profile;
                f.Area = AreaType.OldCore;
                f.FrontEdge = 0;
                f.ShopBays = (byte)(shop ? 1 : 0);
                f.Flags = (byte)(arch == BuildingArchetype.Newar ? BuildingFrontFlags.StructureMud : BuildingFrontFlags.StructureRcc);
                t.BuildingFronts.Add(f);
            }
            Add(300, seed);
            if (neighbours)
            {
                Add(300 - w, seed + 1);
                Add(300 + w, seed + 2);
            }
            // The lane in front (south), its corridor edge 1 m off the front wall.
            t.Roads.Add(new RoadRecord { OsmWayId = 1, RoadClass = RoadClass.Residential, Points = new[] { 25000, 49600, 36000, 49600 }, WidthCm = 400 });
            return t;
        }

        [Test]
        public void NewarFrontsCarryToranasAndBhaygahsAndDenseWindowRows()
        {
            long toranas = 0, bhaygahs = 0, houses = 0;
            foreach (StyleProfile profile in new[] { StyleProfile.Bhaktapur, StyleProfile.Patan, StyleProfile.KathmanduCore })
                for (uint seed = 1; seed <= 16; seed++)
                {
                    TileData t = House(profile, BuildingArchetype.Newar, 4, seed * 7919, 6.4, 8, false, true);
                    HouseBuilder.Stats = default(HouseStats);
                    var m = new MeshData();
                    Assert.That(BuildingDetailMesher.One(t, 0, new TileHeightSampler(t, 1), new BuildingOptions { B0CapTris = int.MaxValue }, m, null), Is.True);
                    MeshingChecks.AssertWellFormed(m, profile + " " + seed);
                    toranas += HouseBuilder.Stats.Toranas;
                    bhaygahs += HouseBuilder.Stats.Bhaygahs;
                    houses++;
                    // The glazed dachi apa courses at the floor lines.
                    bool glazed = false;
                    for (int v = 0; v < m.VertexCount && !glazed; v++)
                        glazed = (MaterialChannel)(int)m.Uv0[2 * v] == MaterialChannel.BrickGlazed && m.Positions[3 * v + 2] < 500.05;
                    Assert.That(glazed, Is.True, profile + " " + seed + ": dachi apa bands on the front");
                }
            TestContext.WriteLine("{0} Newar houses: {1} toranas, {2} bhaygahs", houses, toranas, bhaygahs);
            Assert.That(toranas, Is.GreaterThanOrEqualTo(houses), "every Newar house door (shops aside) carries a torana");
            Assert.That(bhaygahs, Is.GreaterThan(houses / 10), "timber balconies on a share of the upper floors");
        }

        [Test]
        public void EveryExposedSideWallHasWindowsAndPartyWallsStayBlank()
        {
            foreach (var (profile, arch) in new[] { (StyleProfile.Bhaktapur, BuildingArchetype.Newar), (StyleProfile.Metro, BuildingArchetype.ModernUrban),
                                                     (StyleProfile.KathmanduCore, BuildingArchetype.NewarHybrid) })
            {
                // Free-standing: the back and both sides are dressed. In a row: only the back (the party walls stay blank).
                for (int row = 0; row < 2; row++)
                {
                    TileData t = House(profile, arch, 4, 4242, 7, 9, false, row == 1);
                    HouseBuilder.Stats = default(HouseStats);
                    var m = new MeshData();
                    Assert.That(BuildingDetailMesher.One(t, 0, new TileHeightSampler(t, 1), new BuildingOptions { B0CapTris = int.MaxValue }, m, null), Is.True);
                    HouseStats st = HouseBuilder.Stats;
                    TestContext.WriteLine("{0} {1} {2}: {3} side walls dressed, {4} openings", profile, arch, row == 1 ? "in a row" : "free-standing", st.SideWalls,
                                          st.SideOpenings);
                    Assert.That(st.SideWalls, Is.EqualTo(row == 1 ? 1 : 3), profile + " " + arch);
                    Assert.That(st.SideOpenings, Is.GreaterThanOrEqualTo(row == 1 ? 4 : 12), profile + " " + arch + ": a window grid, not a token window");
                }
            }
        }

        [Test]
        public void StreetDressingKeepsClearOfTheRoadBelowTheClearance()
        {
            // Cables, laundry, plants, toranas and balconies on a front 0.3 m from the corridor: nothing below 4.5 m stands
            // more than a few centimetres out of the wall (the final clamp's margin).
            int checkedHouses = 0;
            foreach (var (profile, arch) in new[] { (StyleProfile.Patan, BuildingArchetype.Newar), (StyleProfile.Thamel, BuildingArchetype.ModernUrban),
                                                     (StyleProfile.KathmanduCore, BuildingArchetype.NewarHybrid) })
                for (uint seed = 1; seed <= 8; seed++)
                {
                    TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
                    int X(double v) => (int)Math.Round(v * 100);
                    t.Buildings.Add(new BuildingRecord
                    {
                        Archetype = arch, Levels = 5, Seed = seed * 104729,
                        Rings = new[] { new[] { X(300), X(500), X(307), X(500), X(307), X(509), X(300), X(509) } },
                    });
                    var f = BuildingFrontRecord.Absent;
                    f.Profile = profile;
                    f.Area = AreaType.OldCore;
                    f.FrontEdge = 0;
                    f.ShopBays = 1;
                    t.BuildingFronts.Add(f);
                    t.Roads.Add(new RoadRecord { OsmWayId = 1, RoadClass = RoadClass.Residential, Points = new[] { 28000, 49730, 34000, 49730 }, WidthCm = 400 });
                    var m = new MeshData();
                    Assert.That(BuildingDetailMesher.One(t, 0, new TileHeightSampler(t, 1), new BuildingOptions { B0CapTris = int.MaxValue }, m, null), Is.True);
                    double worst = 0;
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        double y = m.Positions[3 * v + 1] - 1300, z = m.Positions[3 * v + 2];
                        if (y > 4.45 || y < 0.05) continue;
                        worst = Math.Max(worst, 500 - z);
                    }
                    Assert.That(worst, Is.LessThan(0.36), profile + " " + seed + ": stands " + worst.ToString("0.00") + " m out below the clearance");
                    checkedHouses++;
                }
            Assert.That(checkedHouses, Is.EqualTo(24));
        }
    }
}
