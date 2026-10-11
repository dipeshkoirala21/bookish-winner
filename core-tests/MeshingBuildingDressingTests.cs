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
        public void NewarFrontsCarryBhaygahsAndDenseWindowRowsAndToranasOnlyOnAFewRichFronts()
        {
            long toranas = 0, bhaygahs = 0, houses = 0;
            foreach (StyleProfile profile in new[] { StyleProfile.Bhaktapur, StyleProfile.Patan, StyleProfile.KathmanduCore })
                for (uint seed = 1; seed <= 24; seed++)
                {
                    // Narrow, wide, three to five storeys: the ordinary and the well-off houses of the old towns.
                    double w = seed % 3 == 0 ? 6.4 : seed % 3 == 1 ? 4.6 : 5.2;
                    int levels = 3 + (int)(seed / 3 % 3);
                    TileData t = House(profile, BuildingArchetype.Newar, levels, seed * 7919, w, 8, false, true);
                    HouseBuilder.Stats = default(HouseStats);
                    var m = new MeshData();
                    Assert.That(BuildingDetailMesher.One(t, 0, new TileHeightSampler(t, 1), new BuildingOptions { B0CapTris = int.MaxValue }, m, null), Is.True);
                    MeshingChecks.AssertWellFormed(m, profile + " " + seed);
                    toranas += HouseBuilder.Stats.Toranas;
                    bhaygahs += HouseBuilder.Stats.Bhaygahs;
                    houses++;
                    bool glazed = false;
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        var ch = (MaterialChannel)(int)m.Uv0[2 * v];
                        // Gilt belongs to temples, maths and palaces, never to a house.
                        Assert.That(ch, Is.Not.EqualTo(MaterialChannel.Gilt), profile + " " + seed + ": a gilt part on a house");
                        // The glazed dachi apa courses at the floor lines.
                        glazed |= ch == MaterialChannel.BrickGlazed && m.Positions[3 * v + 2] < 500.05;
                    }
                    Assert.That(glazed, Is.True, profile + " " + seed + ": dachi apa bands on the front");
                }
            TestContext.WriteLine("{0} Newar houses: {1} toranas, {2} bhaygahs", houses, toranas, bhaygahs);
            // Ordinary house doors carry the small painted deity panel; a carved torana (door or window crest) only on a
            // few big fronts of well-off families (ref_buildings.md §12).
            Assert.That(toranas, Is.GreaterThan(0), "a few rich fronts carry a carved torana");
            Assert.That(toranas, Is.LessThanOrEqualTo(houses / 8), "toranas stay rare on houses");
            Assert.That(bhaygahs, Is.GreaterThan(houses / 12), "timber balconies on a share of the upper floors");
        }

        [Test]
        public void TheBhaygahRunsAcrossTheSecondFloorInPlaceOfItsSanjhyaNeverUnderTheEaves()
        {
            // Patan, four and five storeys: wherever a balcony is drawn it runs across the second floor (the Patan rows),
            // never across the attic floor, whose gajhya it would enclose, and never over a sanjhya.
            int balconies = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                int levels = 4 + (int)(seed % 2);
                TileData t = House(StyleProfile.Patan, BuildingArchetype.Newar, levels, seed * 104729, 6.0, 8, false, true);
                HouseBuilder.Stats = default(HouseStats);
                var m = new MeshData();
                Assert.That(BuildingDetailMesher.One(t, 0, new TileHeightSampler(t, 1), new BuildingOptions { B0CapTris = int.MaxValue }, m, null), Is.True);
                MeshingChecks.AssertWellFormed(m, "Patan " + seed);
                balconies += (int)HouseBuilder.Stats.Bhaygahs;
                Assert.That(HouseBuilder.Stats.BhaygahsMisplaced, Is.EqualTo(0), seed + ": a balcony under the eaves or over a sanjhya");
            }
            TestContext.WriteLine("{0} balconies on 40 Patan houses", balconies);
            Assert.That(balconies, Is.GreaterThan(6), "Patan rows carry balconies");
        }

        [Test]
        public void TheCableDropStandsOnASolidPierClearOfEveryOpening()
        {
            // A shop front whose dalan openings fill the wall: no drop at all.
            var holes = new[] { new KitHole(0.35, 0.3, 2.9, 2.4), new KitHole(3.08, 0.3, 5.65, 2.4) };
            Assert.That(double.IsNaN(HouseBuilder.DropPier(0, 6, 1.2, 3.0, holes, 2)), Is.True, "no pier on a shop front");
            // A door in the middle, small windows either side: the drop stands on a pier between them, the box on solid wall.
            holes = new[] { new KitHole(2.6, 0.3, 3.4, 2.0), new KitHole(0.9, 1.25, 1.4, 1.85), new KitHole(4.6, 1.25, 5.1, 1.85) };
            for (double target = 0.2; target < 6; target += 0.4)
            {
                double u = HouseBuilder.DropPier(0, 6, target, 3.0, holes, 3);
                Assert.That(double.IsNaN(u), Is.False, "a pier at " + target);
                Assert.That(u, Is.InRange(0.3 + 0.14, 6 - 0.3 - 0.14), "clear of the wall ends");
                foreach (KitHole o in holes)
                    Assert.That(u + 0.14 <= o.U0 - 0.2 || u - 0.14 >= o.U1 + 0.2, Is.True,
                                "the meter box at " + u.ToString("0.00") + " clear of the opening " + o.U0.ToString("0.00") + "-" + o.U1.ToString("0.00"));
            }
            // An opening below the meter box (a low window under 1.5 m) does not block it.
            holes = new[] { new KitHole(0.8, 0.3, 5.2, 1.2) };
            Assert.That(HouseBuilder.DropPier(0, 6, 2.0, 3.0, holes, 1), Is.EqualTo(2.0).Within(1e-9));
        }

        [Test]
        public void LaundryNeverSpansTheSanjhyaOrTheGajhya()
        {
            // A sanjhya floor: tikijhya at 0.4-1.1, 1.6-2.3 (left), 6.0-6.7, 7.2-7.9 (right) of a 8.4 m front; the bay at 2.8-5.6.
            var holes = new[] { new KitHole(0.4, 5, 1.1, 6), new KitHole(1.6, 5, 2.3, 6), new KitHole(6.0, 5, 6.7, 6), new KitHole(7.2, 5, 7.9, 6) };
            for (int i = 0; i < 4; i++)
            {
                double la, lb;
                HouseBuilder.LaundrySpan(holes, 4, i, 1.4, 2.8, 5.6, out la, out lb);
                Assert.That(lb > la, Is.True);
                Assert.That(lb <= 2.8 - 0.05 + 1e-9 || la >= 5.6 + 0.05 - 1e-9, Is.True, "window " + i + ": the line " + la + "-" + lb + " crosses the bay");
                Assert.That(la, Is.LessThanOrEqualTo(holes[i].U0));
            }
            // Neighbouring windows on the same side share a line; with no bay it may run between any two neighbours.
            double a, b;
            HouseBuilder.LaundrySpan(holes, 4, 0, 1.4, 2.8, 5.6, out a, out b);
            Assert.That(b, Is.EqualTo(2.4).Within(1e-9));
            HouseBuilder.LaundrySpan(holes, 4, 2, 1.4, double.NaN, double.NaN, out a, out b);
            Assert.That(a, Is.EqualTo(5.9).Within(1e-9));
            Assert.That(b, Is.EqualTo(8.0).Within(1e-9));
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
