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
    /// projects over a corridor (balconies, signs, awnings, sunshades, aprons, eaves are clipped or raised). Tested with a
    /// fake corridor (a straight band) so the roads package's index is not needed.
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
