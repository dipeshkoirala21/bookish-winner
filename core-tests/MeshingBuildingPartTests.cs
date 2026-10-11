using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// <c>building:part</c> records at B0 (heritage package): a main volume is a house of the grammar with its host's
    /// front and its own levels and height (carved Newar windows, doors, eaves and balconies on its street and square
    /// walls); a raised volume keeps the styled extrusion with window rows on every street wall. Never a blank slab like
    /// the 13.5 m brick wall the reviewer found facing Kathmandu Durbar Square (osm 2830305602, part of 2059931096).
    /// </summary>
    public class MeshingBuildingPartTests
    {
        private static bool Opening(MeshData m, int v)
        {
            var ch = (MaterialChannel)(int)Math.Round(m.Uv0[2 * v]);
            return ch == MaterialChannel.Glass || ch == MaterialChannel.WoodCarved || ch == MaterialChannel.Wood;
        }

        /// <summary>Opening vertices (glass, carved and plain timber) in the slab in front of the wall a→b (outward to the
        /// right of travel) between heights y0 and y1.</summary>
        private static int OpeningsOn(MeshData m, double ax, double az, double bx, double bz, double y0, double y1)
        {
            double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
            double ux = dx / len, uz = dz / len, nx = uz, nz = -ux;
            int n = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                double x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                if (y < y0 || y > y1) continue;
                double along = (x - ax) * ux + (z - az) * uz, outward = (x - ax) * nx + (z - az) * nz;
                if (along < 0.2 || along > len - 0.2 || outward < -0.6 || outward > 1.5) continue;
                if (Opening(m, v)) n++;
            }
            return n;
        }

        /// <summary>
        /// True when the wall a→b looks onto a drawn corridor (the corridor edge within 4 m in front and coming nearer
        /// stepping out, as the house grammar's street sides) or a square (a pedestrian, marketplace or courtyard area or a
        /// heritage zone 3 m out), with no other footprint within 1.6 m in front of it.
        /// </summary>
        private static bool Faces(TileData t, IRoadCorridorQuery q, BuildingFootprints guard, int self, double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
            double nx = dz / len, nz = -dx / len;
            bool any = false;
            for (int k = 1; k <= 3; k++)
            {
                double mx = ax + dx * 0.25 * k, mz = az + dz * 0.25 * k;
                if (guard.Neighbours.Inside(mx + 0.8 * nx, mz + 0.8 * nz, self) || guard.Neighbours.Inside(mx + 1.6 * nx, mz + 1.6 * nz, self)) continue;
                double near = q.SignedDistance(t.Tile.X0 + mx + 0.3 * nx, t.Tile.Z0 + mz + 0.3 * nz);
                double far = q.SignedDistance(t.Tile.X0 + mx + 3.3 * nx, t.Tile.Z0 + mz + 3.3 * nz);
                if (near < 4.0 && far < near - 1.0 || BuildingDetailMesher.OnSquare(t, mx + 3.0 * nx, mz + 3.0 * nz)) any = true;
            }
            return any;
        }

        [Test]
        public void NoB0PartWallFacingACorridorOrASquareIsWindowFree()
        {
            ISet<ulong> hidden = StreamingScene.Heroes.HiddenRefs;
            int parts = 0, bodies = 0, walls = 0, blank = 0;
            var report = new List<string>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                var h = new TileHeightSampler(t, 1);
                IRoadCorridorQuery q = RoadCorridorIndex.ForTile(t);
                var o = new BuildingOptions { Corridors = RoadCorridorIndex.ForTile, HiddenRefs = hidden };
                BuildingFootprints guard = BuildingFootprints.For(t, q);
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    BuildingRecord rec = t.Buildings[i];
                    if ((rec.Flags & BuildingFlags.Part) == 0 || SacredSelector.HostOf(t, i) >= 0 || hidden.Contains(rec.OsmRef)) continue;
                    var m = new MeshData();
                    if (!BuildingDetailMesher.One(t, i, h, o, m, null)) continue;
                    parts++;
                    HousePlan plan = BuildingGrammar.Plan(t, i);
                    if (rec.MinHeightCm < 50) bodies++;
                    BuildingRecord b = guard.Record(i);
                    int[] ring = b.Rings[0];
                    int n = ring.Length / 2;
                    double area = 0;
                    for (int k = 0, j = n - 1; k < n; j = k++) area += (double)ring[2 * j] * ring[2 * k + 1] - (double)ring[2 * k] * ring[2 * j + 1];
                    double ground = double.MaxValue;
                    for (int k = 0; k < n; k++)
                    {
                        float y;
                        if (h.TryHeightClamped(t.Tile.X0 + ring[2 * k] / 100.0, t.Tile.Z0 + ring[2 * k + 1] / 100.0, out y)) ground = Math.Min(ground, y);
                    }
                    double bottom = ground + plan.MinHeightM, top = ground + plan.WallTopM;
                    if (top - bottom < BuildingDetailMesher.PartMinWallM) continue;
                    for (int k = 0; k < n; k++)
                    {
                        int j = k + 1 == n ? 0 : k + 1;
                        int a = area > 0 ? k : j, c = area > 0 ? j : k;
                        double ax = ring[2 * a] / 100.0, az = ring[2 * a + 1] / 100.0, bx = ring[2 * c] / 100.0, bz = ring[2 * c + 1] / 100.0;
                        double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                        if (len < 3.0 || !Faces(t, q, guard, i, ax, az, bx, bz)) continue;
                        walls++;
                        // Windows, not only a door: openings above the ground floor's door head.
                        double from = top - bottom >= 5.0 ? bottom + 2.6 : bottom + 0.8;
                        int open = OpeningsOn(m, ax, az, bx, bz, from, top);
                        if (open >= 8) continue;
                        blank++;
                        if (report.Count < 15)
                            report.Add(string.Format("{0} b{1} osm {2} {3} min {4:0.0} wall {5:0.0} m ({6:0.0},{7:0.0})->({8:0.0},{9:0.0}): {10} opening vertices", id, i,
                                                     rec.OsmRef, plan.Archetype, plan.MinHeightM, len, ax, az, bx, bz, open));
                    }
                }
            }
            TestContext.WriteLine("B0 parts {0} (main volumes {1}), walls facing a corridor or a square {2}, window-free {3}", parts, bodies, walls, blank);
            foreach (string r in report) TestContext.WriteLine(r);
            Assert.That(parts, Is.GreaterThan(80));
            Assert.That(walls, Is.GreaterThan(30));
            Assert.That(blank, Is.EqualTo(0), string.Join("\n", report));
        }

        [Test]
        public void TheDurbarSquarePartIsANewarHouseWithItsOwnHeightAndItsFrontOnTheSquare()
        {
            // osm 2830305602: a 4-level hipped building:part of outline 2059931096 (Newar, 13.5 m) on the north side of
            // the part of Kathmandu Durbar Square west of Hanuman Dhoka. Its front is the outline's corner edge onto the
            // square (BFNT), its plan keeps 4 storeys and 13.5 m (not the 6 storeys a row house would draw).
            var leaf = new TileId(10, 516, 161);
            TileData t = StreamingSampleRegion.Tile(leaf);
            int i = -1;
            for (int k = 0; k < t.Buildings.Count; k++)
                if (t.Buildings[k].OsmRef == 2830305602UL)
                    i = k;
            Assert.That(i, Is.GreaterThanOrEqualTo(0));
            HousePlan plan = BuildingGrammar.Plan(t, i);
            Assert.That(plan.Storeys, Is.EqualTo(4), "the part's own levels");
            Assert.That(plan.TotalM, Is.EqualTo(13.5f).Within(1.0f), "the part's own height");
            Assert.That(plan.Archetype, Is.EqualTo(BuildingArchetype.Newar));
            IRoadCorridorQuery q = RoadCorridorIndex.ForTile(t);
            BuildingFootprints guard = BuildingFootprints.For(t, q);
            int front, second;
            BuildingDetailMesher.PartFronts(t, i, guard.Record(i).Rings[0], BuildingDetailMesher.PartsOf(t), guard, out front, out second);
            int[] ring = guard.Record(i).Rings[0];
            int n = ring.Length / 2, j = front + 1 == n ? 0 : front + 1;
            double mz = 0.5 * (ring[2 * front + 1] + ring[2 * j + 1]) / 100.0, cz = 0;
            for (int k = 0; k < n; k++) cz += ring[2 * k + 1] / 100.0 / n;
            Assert.That(mz, Is.GreaterThan(cz + 2), "the front is the north wall, on the square");
            var h = new TileHeightSampler(t, 1);
            var m = new MeshData();
            HouseBuilder.Stats = default(HouseStats);
            Assert.That(BuildingDetailMesher.One(t, i, h, new BuildingOptions { Corridors = RoadCorridorIndex.ForTile }, m, null), Is.True);
            int carved = 0;
            for (int v = 0; v < m.VertexCount; v++)
                if ((MaterialChannel)(int)Math.Round(m.Uv0[2 * v]) == MaterialChannel.WoodCarved) carved++;
            TestContext.WriteLine("{0} triangles, {1} carved-timber vertices, {2} side openings", m.TriangleCount, carved, HouseBuilder.Stats.SideOpenings);
            Assert.That(carved, Is.GreaterThan(200), "carved Newar windows and doors");
            Assert.That(m.TriangleCount, Is.GreaterThan(1500), "a house of the grammar, not a 12-triangle slab");
        }

        [Test]
        public void APartOtherPartsStandOnGetsATerraceNotAGable()
        {
            // Two stacked parts: a 6 m base and a 4 m upper volume set on it. The base must not run a gable up into the
            // upper volume; the upper one keeps the styled extrusion with window rows on its street wall.
            var leaf = new TileId(10, 516, 161);
            TileData t = MeshingChecks.SyntheticTile(leaf, (x, z) => 1300);
            int[] ring = { 30000, 30000, 30800, 30000, 30800, 30900, 30000, 30900 };
            t.Buildings.Add(new BuildingRecord
            {
                Archetype = BuildingArchetype.Newar, Flags = BuildingFlags.HasParts, Levels = 3, HeightCm = 1000, Seed = 7, Rings = new[] { ring },
            });
            t.Buildings.Add(new BuildingRecord
            {
                Archetype = BuildingArchetype.Newar, Flags = BuildingFlags.Part, Levels = 2, HeightCm = 600, Seed = 8, Rings = new[] { ring },
            });
            t.Buildings.Add(new BuildingRecord
            {
                Archetype = BuildingArchetype.Newar, Flags = BuildingFlags.Part, Levels = 1, HeightCm = 1000, MinHeightCm = 600, Seed = 9,
                Rings = new[] { new[] { 30100, 30100, 30700, 30100, 30700, 30800, 30100, 30800 } },
            });
            t.Roads.Add(new RoadRecord { OsmWayId = 1, RoadClass = RoadClass.Residential, Points = new[] { 28000, 29600, 34000, 29600 }, WidthCm = 400 });
            var h = new TileHeightSampler(t, 1);
            var o = new BuildingOptions { Corridors = RoadCorridorIndex.ForTile };
            Assert.That(BuildingDetailMesher.PartsOf(t).Covered[1], Is.True);
            Assert.That(BuildingDetailMesher.PartsOf(t).Covered[2], Is.False);
            var basem = new MeshData();
            Assert.That(BuildingDetailMesher.One(t, 0, h, o, new MeshData(), null), Is.False, "the outline is drawn by its parts");
            Assert.That(BuildingDetailMesher.One(t, 1, h, o, basem, null), Is.True);
            float top = float.MinValue;
            for (int v = 0; v < basem.VertexCount; v++) top = Math.Max(top, basem.Positions[3 * v + 1]);
            Assert.That(top - 1300, Is.LessThan(6.0 + 2.6), "a terrace with a parapet and roof props at most, no ridge into the volume above");
            var upper = new MeshData();
            Assert.That(BuildingDetailMesher.One(t, 2, h, o, upper, null), Is.True);
            Assert.That(OpeningsOn(upper, 301, 301, 307, 301, 1306.5, 1310), Is.GreaterThan(0), "window rows on the upper volume's street wall");
        }
    }
}
