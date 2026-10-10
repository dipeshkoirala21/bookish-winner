using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Decisions 1 and 2 of docs/W2_DETAIL_CONTRACT.md for buildings: footprints never stand in a road corridor (the
    /// runtime guard trims them, or drops a house that stands in the road, in every band) and nothing below 4.5 m
    /// projects over a corridor (balconies, signs, awnings, sunshades, aprons, eaves are clipped or raised, each by its own
    /// free depth, not pressed flat afterwards). Tested with a fake corridor (a straight band) so the roads package's
    /// index is not needed, and with the package's stand-in (real distances, so only walls on a road are dressed).
    /// </summary>
    public class MeshingBuildingClearanceTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);
        private static readonly double Ground = Ght.Dequantize(Ght.Quantize(1300));

        /// <summary>A straight east-west corridor between game z0 and z1 (tile-local zl0..zl1).</summary>
        private sealed class Band : IRoadCorridorQuery
        {
            private readonly double _z0, _z1;

            public Band(double gameZ0, double gameZ1)
            {
                _z0 = gameZ0;
                _z1 = gameZ1;
            }

            public double SignedDistance(double x, double z)
            {
                double mid = 0.5 * (_z0 + _z1), half = 0.5 * (_z1 - _z0);
                return Math.Abs(z - mid) - half;
            }

            public bool Overlaps(double[] x, double[] z, int n, out double depthM)
            {
                depthM = 0;
                bool hit = false;
                for (int i = 0; i < n; i++)
                {
                    int j = i + 1 == n ? 0 : i + 1;
                    for (int k = 0; k <= 8; k++)
                    {
                        double t = k / 8.0, d = -SignedDistance(x[i] + (x[j] - x[i]) * t, z[i] + (z[j] - z[i]) * t);
                        if (d > 0)
                        {
                            hit = true;
                            depthM = Math.Max(depthM, d);
                        }
                    }
                }
                return hit;
            }
        }

        private static BuildingRecord Rect(BuildingArchetype a, int levels, uint seed, double x0, double z0, double w, double d)
        {
            int X(double v) => (int)Math.Round(v * 100);
            return new BuildingRecord
            {
                Archetype = a, Levels = (byte)levels, Seed = seed,
                Rings = new[] { new[] { X(x0), X(z0), X(x0 + w), X(z0), X(x0 + w), X(z0 + d), X(x0), X(z0 + d) } },
            };
        }

        /// <summary>A row of houses whose front (edge 0, facing south) stands exactly on the corridor's north edge.</summary>
        private static TileData Row(StyleProfile profile, BuildingArchetype arch, int levels, bool shop, int count, out Band band, double gap)
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            double zl = 500;
            band = new Band(Leaf.Z0 + zl - 6.0, Leaf.Z0 + zl - gap);
            for (int i = 0; i < count; i++)
            {
                t.Buildings.Add(Rect(arch, levels, (uint)(977 * i + 13), 300 + 7.0 * i, zl, 6.8, 8));
                var f = BuildingFrontRecord.Absent;
                f.Profile = profile;
                f.Area = AreaType.OldCore;
                f.FrontEdge = 0;
                f.ShopBays = (byte)(shop ? 1 : 0);
                f.Flags = (byte)(arch == BuildingArchetype.Newar ? BuildingFrontFlags.StructureMud : BuildingFrontFlags.StructureRcc);
                t.BuildingFronts.Add(f);
            }
            return t;
        }

        /// <summary>The lowest point of the mesh that lies inside the corridor (deeper than 1 cm), or +∞.</summary>
        private static double LowestIntrusion(MeshData m, TileData t, IRoadCorridorQuery q, out double depth)
        {
            double low = double.PositiveInfinity;
            depth = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                double x = m.Positions[3 * v] + t.Tile.X0, y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2] + t.Tile.Z0;
                double sd = q.SignedDistance(x, z);
                if (sd >= -0.01) continue;
                if (y < low)
                {
                    low = y;
                    depth = -sd;
                }
            }
            return low;
        }

        [Test]
        public void NothingBelowTheClearanceOverhangsACorridor()
        {
            var cases = new[]
            {
                (StyleProfile.Thamel, BuildingArchetype.ModernUrban, 6, true),
                (StyleProfile.Metro, BuildingArchetype.ModernUrban, 5, true),
                (StyleProfile.Metro, BuildingArchetype.ModernUrban, 4, false),
                (StyleProfile.KathmanduCore, BuildingArchetype.NewarHybrid, 6, true),
                (StyleProfile.Bhaktapur, BuildingArchetype.Newar, 4, false),
                (StyleProfile.Bhaktapur, BuildingArchetype.Newar, 2, true),
                (StyleProfile.Patan, BuildingArchetype.Newar, 3, true),
            };
            int checkedHouses = 0;
            foreach (var (profile, arch, levels, shop) in cases)
            {
                Band band;
                TileData t = Row(profile, arch, levels, shop, 8, out band, 0.0);
                var h = new TileHeightSampler(t, 1);
                var o = new BuildingOptions { Corridors = _ => band };
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    var m = new MeshData();
                    Assert.That(BuildingDetailMesher.One(t, i, h, o, m, null), Is.True);
                    MeshingChecks.AssertWellFormed(m, profile + " " + i);
                    double depth;
                    double low = LowestIntrusion(m, t, band, out depth);
                    Assert.That(low, Is.GreaterThanOrEqualTo(Ground + RoadClearance.MinOverheadClearanceM - 1e-3),
                                profile + "/" + arch + " house " + i + ": something " + depth.ToString("0.00") + " m into the road at " + low.ToString("0.00"));
                    checkedHouses++;
                }
            }
            Assert.That(checkedHouses, Is.EqualTo(56));
        }

        [Test]
        public void ProjectionsStayWhenTheRoadIsFarEnough()
        {
            // The same Thamel row with the corridor 3 m in front: balconies, sunshades and signs project again.
            Band near, far;
            TileData tn = Row(StyleProfile.Thamel, BuildingArchetype.ModernUrban, 6, true, 8, out near, 0.0);
            TileData tf = Row(StyleProfile.Thamel, BuildingArchetype.ModernUrban, 6, true, 8, out far, 3.0);
            double outNear = 0, outFar = 0;
            for (int i = 0; i < tn.Buildings.Count; i++)
            {
                var mn = new MeshData();
                var mf = new MeshData();
                BuildingDetailMesher.One(tn, i, new TileHeightSampler(tn, 1), new BuildingOptions { Corridors = _ => near }, mn, null);
                BuildingDetailMesher.One(tf, i, new TileHeightSampler(tf, 1), new BuildingOptions { Corridors = _ => far }, mf, null);
                outNear += LowFront(mn, tn);
                outFar += LowFront(mf, tf);
            }
            Assert.That(outFar, Is.GreaterThan(outNear + 1.0), "with room in front, low projections come back");
        }

        /// <summary>The summed projection (m) south of the front wall of everything below 4.5 m.</summary>
        private static double LowFront(MeshData m, TileData t)
        {
            double sum = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                double y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                if (y < 1300 + 4.4 && z < 500 - 0.05) sum += 500 - z;
            }
            return sum / Math.Max(1, m.VertexCount) * 100;
        }

        [Test]
        public void FootprintsAreTrimmedOutOfTheRoadInEveryBand()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            // A corridor between tile-local z 494 and 500: house 0 intrudes 1.5 m, house 1 stands in the road, house 2
            // is clear.
            var band = new Band(Leaf.Z0 + 494, Leaf.Z0 + 500);
            t.Buildings.Add(Rect(BuildingArchetype.ModernUrban, 4, 5, 300, 498.5, 7, 9));
            t.Buildings.Add(Rect(BuildingArchetype.ModernUrban, 3, 6, 320, 494.5, 6, 5));
            t.Buildings.Add(Rect(BuildingArchetype.ModernUrban, 3, 7, 340, 501, 7, 8));
            var o = new BuildingOptions { Corridors = _ => band };
            BuildingFootprints g = BuildingFootprints.For(t, band);
            Assert.That(g.Trimmed(0), Is.True);
            Assert.That(g.Dropped(1), Is.True, "a house in the road is not drawn");
            Assert.That(g.Trimmed(2) || g.Dropped(2), Is.False);
            int[] r = g.Record(0).Rings[0];
            for (int k = 0; k < r.Length / 2; k++)
                Assert.That(band.SignedDistance(Leaf.X0 + r[2 * k] / 100.0, Leaf.Z0 + r[2 * k + 1] / 100.0), Is.GreaterThanOrEqualTo(-0.01));
            Assert.That(Math.Abs(Polygon.SignedArea(Xs(r), Zs(r), r.Length / 2)), Is.EqualTo(7 * 7.5).Within(0.3), "trimmed back to the corridor edge");
            var h = new TileHeightSampler(t, 1);
            foreach (BuildingBand b in new[] { BuildingBand.B0KitLite, BuildingBand.B1Styled, BuildingBand.B2Prism })
            {
                var m = new MeshData();
                o.Band = b;
                int drawn = BuildingMesher.Build(t, h, o, m);
                Assert.That(drawn, Is.EqualTo(2), b.ToString());
                double depth;
                double low = LowestIntrusion(m, t, band, out depth);
                Assert.That(low, Is.GreaterThanOrEqualTo(Ground + RoadClearance.MinOverheadClearanceM - 1e-3), b + ": " + depth.ToString("0.00") + " m into the road");
            }
            Assert.That(BuildingMesher.IsDrawn(t, 1, o), Is.False);
            // Off: as mapped.
            Assert.That(BuildingMesher.Build(t, h, new BuildingOptions { RoadGuard = false }, new MeshData()), Is.EqualTo(3));
        }

        [Test]
        public void TheStandInCorridorsFollowTheRoadsOfTheTile()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            t.Roads.Add(new RoadRecord { OsmWayId = 9, RoadClass = RoadClass.Residential, Points = new[] { 20000, 50000, 60000, 50000 }, WidthCm = 300 });
            t.Roads.Add(new RoadRecord { OsmWayId = 10, RoadClass = RoadClass.Residential, Flags = RoadFlags.Tunnel, Points = new[] { 20000, 60000, 60000, 60000 } });
            IRoadCorridorQuery q = new BuildingOptions().CorridorsFor(t);
            Assert.That(q, Is.Not.Null);
            Assert.That(q.SignedDistance(Leaf.X0 + 400, Leaf.Z0 + 500), Is.LessThanOrEqualTo(-0.5 * RoadClearance.MinCorridorM + 1e-6), "at least the minimum corridor");
            Assert.That(q.SignedDistance(Leaf.X0 + 400, Leaf.Z0 + 600), Is.GreaterThan(0), "tunnels claim no corridor");
            Assert.That(new BuildingOptions { RoadGuard = false }.CorridorsFor(t), Is.Null);
        }

        [Test]
        public void TheStandInReportsRealDistancesBeyondAFewMetres()
        {
            // The stand-in used to clamp every distance to 3 m, so "a road within 4 m" held for every wall of the tile.
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            t.Roads.Add(new RoadRecord { OsmWayId = 9, RoadClass = RoadClass.Residential, Points = new[] { 20000, 50000, 60000, 50000 }, WidthCm = 300 });
            IRoadCorridorQuery q = new BuildingOptions().CorridorsFor(t);
            double half = 0.5 * RoadClearance.MinCorridorM;
            foreach (double off in new[] { 0.5, 3.5, 4.5, 10.0, 25.0, 50.0 })
                Assert.That(q.SignedDistance(Leaf.X0 + 400, Leaf.Z0 + 500 + half + off), Is.EqualTo(off).Within(1e-6), "at " + off + " m");
            // Beyond the end cap, diagonally.
            Assert.That(q.SignedDistance(Leaf.X0 + 600 + 6, Leaf.Z0 + 500 + 8), Is.EqualTo(10 - half).Within(1e-6));
            // Nothing within the search radius: the bound, never less.
            Assert.That(q.SignedDistance(Leaf.X0 + 400, Leaf.Z0 + 800), Is.EqualTo(RoadCorridorStandIn.MaxSearchM));
            // A tile without roads.
            TileData empty = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            Assert.That(new BuildingOptions().CorridorsFor(empty).SignedDistance(Leaf.X0 + 10, Leaf.Z0 + 10), Is.EqualTo(RoadCorridorStandIn.MaxSearchM));
        }

        /// <summary>Vertices of <paramref name="m"/> between 0.3 m above the ground and 1.5 m under the wall top (below
        /// the eaves) that stand more than <paramref name="beyond"/> outside the rectangle [x0, x1] × [z0, z1] on its back
        /// (+z) or sides.</summary>
        private static int OutsideBackAndSides(MeshData m, double x0, double x1, double z0, double z1, double top, double beyond)
        {
            int n = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                double x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                if (y < 1300 + 0.3 || y > top - 1.5) continue;
                if (z > z1 + beyond || x < x0 - beyond || x > x1 + beyond) n++;
            }
            return n;
        }

        [Test]
        public void ABackWallWithNoRoadBehindItGetsNoShopOrSign()
        {
            // A shop house on a lane, with the default corridor source (the stand-in): its front is dressed, its back
            // and side walls face nothing and stay blank brick or paint (no shop bays, signs, sills or sunshades).
            foreach (var (profile, arch) in new[] { (StyleProfile.Thamel, BuildingArchetype.ModernUrban), (StyleProfile.KathmanduCore, BuildingArchetype.NewarHybrid),
                                                     (StyleProfile.Bhaktapur, BuildingArchetype.Newar) })
            {
                for (int behind = 0; behind < 2; behind++)
                {
                    TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
                    t.Buildings.Add(Rect(arch, 5, 4242, 300, 500, 7, 9));
                    var f = BuildingFrontRecord.Absent;
                    f.Profile = profile;
                    f.Area = AreaType.OldCore;
                    f.FrontEdge = 0;
                    f.ShopBays = 1;
                    f.Flags = (byte)(arch == BuildingArchetype.Newar ? BuildingFrontFlags.StructureMud : BuildingFrontFlags.StructureRcc);
                    t.BuildingFronts.Add(f);
                    // The lane in front (south), its corridor edge 0.5 m off the front wall; with behind = 1 a second lane
                    // 0.5 m behind the back wall.
                    t.Roads.Add(new RoadRecord { OsmWayId = 1, RoadClass = RoadClass.Residential, Points = new[] { 28000, 49710, 34000, 49710 }, WidthCm = 400 });
                    if (behind == 1)
                        t.Roads.Add(new RoadRecord { OsmWayId = 2, RoadClass = RoadClass.Residential, Points = new[] { 28000, 51190, 34000, 51190 }, WidthCm = 400 });
                    var m = new MeshData();
                    Assert.That(BuildingDetailMesher.One(t, 0, new TileHeightSampler(t, 1), new BuildingOptions(), m, null), Is.True);
                    HousePlan plan = BuildingGrammar.Plan(t, 0);
                    int outside = OutsideBackAndSides(m, 300, 307, 500, 509, 1300 + plan.WallTopM, 0.06);
                    TestContext.WriteLine("{0} {1}, road behind {2}: {3} vertices dressing the back and sides", profile, arch, behind == 1, outside);
                    if (behind == 0) Assert.That(outside, Is.EqualTo(0), profile + ": a wall with no road in front of it stays blank");
                    else Assert.That(outside, Is.GreaterThan(0), profile + ": a wall on a lane is dressed");
                }
            }
        }

        [Test]
        public void ProjectionsAreClippedByTheBuilderNotPressedFlatByTheClamp()
        {
            // The final clamp is a safety net for frames and bands a few centimetres proud of a wall on a corridor edge:
            // every larger element (sanjhya sill and brackets, pots, garlands, hanging goods, shutter boxes, struts,
            // copings) clips itself to the free depth, so the clamp never moves a vertex further than 5 cm.
            var cases = new[]
            {
                (StyleProfile.Thamel, BuildingArchetype.ModernUrban, 6, true), (StyleProfile.KathmanduCore, BuildingArchetype.NewarHybrid, 6, true),
                (StyleProfile.KathmanduCore, BuildingArchetype.ModernUrban, 4, true), (StyleProfile.Bhaktapur, BuildingArchetype.Newar, 4, false),
                (StyleProfile.Bhaktapur, BuildingArchetype.Newar, 2, true), (StyleProfile.Patan, BuildingArchetype.Newar, 3, true),
                (StyleProfile.Patan, BuildingArchetype.Newar, 5, false), (StyleProfile.Kirtipur, BuildingArchetype.Newar, 4, true),
            };
            long clamped = 0;
            foreach (var (profile, arch, levels, shop) in cases)
            {
                foreach (double gap in new[] { 0.0, 0.1, 0.25 })
                {
                    Band band;
                    TileData t = Row(profile, arch, levels, shop, 8, out band, gap);
                    var h = new TileHeightSampler(t, 1);
                    foreach (BuildingOptions o in new[]
                             {
                                 new BuildingOptions { Corridors = _ => band }, BuildingBandTable.LiteOptions(1, new BuildingOptions { Corridors = _ => band }),
                                 BuildingBandTable.LiteOptions(0, new BuildingOptions { Corridors = _ => band }),
                             })
                    {
                        HouseBuilder.Stats = default(HouseStats);
                        for (int i = 0; i < t.Buildings.Count; i++) BuildingDetailMesher.One(t, i, h, o, new MeshData(), null);
                        clamped += HouseBuilder.Stats.Clamped;
                        Assert.That(HouseBuilder.Stats.ClampedFar, Is.EqualTo(0),
                                    profile + " " + arch + " gap " + gap + ": " + HouseBuilder.Stats.ClampedFar + " vertices pressed back more than 5 cm (max " +
                                    HouseBuilder.Stats.MaxPush.ToString("0.00") + " m)");
                    }
                }
            }
            TestContext.WriteLine("clamp moves (all within 5 cm): {0}", clamped);
            // On the densest real tile, with the stand-in corridors: next to none.
            TileData asan = StreamingSampleRegion.Tile(Leaf);
            var ha = new TileHeightSampler(asan, 2);
            HouseBuilder.Stats = default(HouseStats);
            long verts = 0;
            var mm = new MeshData();
            for (int i = 0; i < asan.Buildings.Count; i += 3)
            {
                mm.Clear();
                BuildingDetailMesher.One(asan, i, ha, BuildingBandTable.NearOptions(2), mm, null);
                verts += mm.VertexCount;
            }
            TestContext.WriteLine("Asan: {0} of {1} vertices clamped, {2} further than 5 cm", HouseBuilder.Stats.Clamped, verts, HouseBuilder.Stats.ClampedFar);
            Assert.That(HouseBuilder.Stats.ClampedFar, Is.LessThanOrEqualTo(verts / 20000));
        }

        [Test]
        public void AlternatingCorridorSourcesReuseTheirGuards()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            t.Buildings.Add(Rect(BuildingArchetype.ModernUrban, 4, 5, 300, 498.5, 7, 9));
            var a = new Band(Leaf.Z0 + 494, Leaf.Z0 + 500);
            var b = new Band(Leaf.Z0 + 495, Leaf.Z0 + 499);
            BuildingFootprints ga = BuildingFootprints.For(t, a), gb = BuildingFootprints.For(t, b);
            long builds = BuildingFootprints.BuildCount;
            for (int k = 0; k < 10; k++)
            {
                Assert.That(BuildingFootprints.For(t, a), Is.SameAs(ga));
                Assert.That(BuildingFootprints.For(t, b), Is.SameAs(gb));
            }
            Assert.That(BuildingFootprints.BuildCount, Is.EqualTo(builds), "alternating two sources rebuilds nothing");
            Assert.That(ga.Trimmed(0), Is.True);
        }

        [Test]
        public void NeighboursAreTheFootprintsTheGuardLeaves()
        {
            // House 0 stands in the road and is dropped; house 1 abuts it. House 1's wall toward the road is a street
            // wall: B2 draws it (it is no longer "shared" with a house that is not there), B0 dresses it.
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            var band = new Band(Leaf.Z0 + 494, Leaf.Z0 + 500);
            t.Buildings.Add(Rect(BuildingArchetype.ModernUrban, 3, 6, 320, 494.5, 6, 5));
            t.Buildings.Add(Rect(BuildingArchetype.ModernUrban, 4, 7, 320, 499.5 + 0.0, 6, 8));
            var o = new BuildingOptions { Corridors = _ => band, Band = BuildingBand.B2Prism };
            BuildingFootprints g = BuildingFootprints.For(t, band);
            Assert.That(g.Dropped(0), Is.True);
            var m = new MeshData();
            Assert.That(BuildingMesher.Build(t, new TileHeightSampler(t, 1), o, m), Is.EqualTo(1));
            // Four walls and a cap (2 triangles each wall, the cap 2): the south wall is drawn.
            int south = 0;
            for (int v = 0; v < m.VertexCount; v++)
                if (m.Normals[3 * v + 2] < -0.9) south++;
            Assert.That(south, Is.GreaterThan(0), "the wall facing the dropped house (and the road) is drawn");
            Assert.That(g.Neighbours.Inside(323, 497, 1), Is.False, "the dropped footprint is not a neighbour");
        }

        private static double[] Xs(int[] r)
        {
            var a = new double[r.Length / 2];
            for (int k = 0; k < a.Length; k++) a[k] = r[2 * k] / 100.0;
            return a;
        }

        private static double[] Zs(int[] r)
        {
            var a = new double[r.Length / 2];
            for (int k = 0; k < a.Length; k++) a[k] = r[2 * k + 1] / 100.0;
            return a;
        }
    }
}
