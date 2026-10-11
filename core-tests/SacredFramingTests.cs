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
    /// The generic sacred framing (heritage package; docs/research/w2/ref_heritage.md): a temple is framed only on a
    /// temple-shaped outline of a real Kathmandu temple's size, its height is capped, and its plinth, steps, guardians and
    /// posts keep out of the drawn road corridors (decision 1). Compounds and oversized outlines are Newar compound
    /// buildings of the house grammar, trimmed out of the road like houses.
    /// </summary>
    public class SacredFramingTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);

        private static BuildingRecord Temple(BuildingArchetype a, double heightM, int levels, params int[] ringCm)
        {
            return new BuildingRecord
            {
                Archetype = a, RoofShape = RoofShape.Pagoda, HeightCm = (ulong)Math.Round(heightM * 100), Levels = (byte)levels, Seed = 4242,
                Flags = BuildingFlags.LevelsInferred, Rings = new[] { ringCm },
            };
        }

        private static TileData Tile()
        {
            return MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
        }

        /// <summary>Sample points of the blocking colliders (bottom under 4 m above <paramref name="ground"/>, not a
        /// walkable step under 0.45 m) that lie deeper than <paramref name="tolerance"/> in a corridor.</summary>
        private static int InRoad(TileData t, GenColliders c, IRoadCorridorQuery q, double ground, double tolerance, List<string> where = null)
        {
            int n = 0;
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            Func<double, double, string, bool> hit = (x, z, what) =>
            {
                double sd = q.SignedDistance(x0 + x, z0 + z);
                if (sd >= -tolerance) return false;
                if (where != null && where.Count < 8) where.Add(what + " sd " + sd.ToString("0.00"));
                return true;
            };
            foreach (GenBox b in c.Boxes)
            {
                if (b.HalfY * 2 < 0.45f && (b.Flags & GenColliderFlags.Walkable) != 0) continue;
                if (b.CY - b.HalfY > ground + SacredSelector.BlockingBelowM) continue;
                double c0 = Math.Cos(b.YawRad), s0 = Math.Sin(b.YawRad);
                for (double u = -b.HalfX; u <= b.HalfX + 1e-6; u += Math.Max(0.05, b.HalfX / Math.Max(1, Math.Ceiling(b.HalfX / 0.25))))
                for (double w = -b.HalfZ; w <= b.HalfZ + 1e-6; w += Math.Max(0.05, b.HalfZ / Math.Max(1, Math.Ceiling(b.HalfZ / 0.25))))
                    if (hit(b.CX + u * c0 - w * s0, b.CZ + u * s0 + w * c0, "box " + b.Flags)) n++;
            }
            foreach (GenCylinder cy in c.Cylinders)
            {
                if (cy.Y0 > ground + SacredSelector.BlockingBelowM) continue;
                for (int a = 0; a < 8; a++)
                    if (hit(cy.CX + cy.Radius * Math.Cos(a * Math.PI / 4), cy.CZ + cy.Radius * Math.Sin(a * Math.PI / 4), "cylinder")) n++;
            }
            foreach (GenWall wl in c.Walls)
            {
                if (wl.Y0 > ground + SacredSelector.BlockingBelowM) continue;
                double len = Math.Sqrt((wl.X1 - wl.X0) * (wl.X1 - wl.X0) + (wl.Z1 - wl.Z0) * (wl.Z1 - wl.Z0));
                int k = Math.Max(1, (int)Math.Ceiling(len / 0.25));
                for (int i = 0; i <= k; i++)
                    if (hit(wl.X0 + (wl.X1 - wl.X0) * i / k, wl.Z0 + (wl.Z1 - wl.Z0) * i / k, "wall")) n++;
            }
            foreach (GenRamp r in c.Ramps)
            {
                if (Math.Min(r.Y0, r.Y1) > ground + SacredSelector.BlockingBelowM) continue;
                double len = Math.Sqrt((r.X1 - r.X0) * (r.X1 - r.X0) + (r.Z1 - r.Z0) * (r.Z1 - r.Z0));
                int k = Math.Max(1, (int)Math.Ceiling(len / 0.25));
                for (int i = 0; i <= k; i++)
                    if (hit(r.X0 + (r.X1 - r.X0) * i / k, r.Z0 + (r.Z1 - r.Z0) * i / k, "stair")) n++;
            }
            return n;
        }

        private static float Top(MeshData m)
        {
            float top = float.MinValue;
            for (int v = 0; v < m.VertexCount; v++) top = Math.Max(top, m.Positions[3 * v + 1]);
            return top;
        }

        [Test]
        public void ACompoundOrAnOversizedOutlineIsNeverFramedAsAPagoda()
        {
            // The Hiti Pokhari case (osm 413748258): a U of narrow ranges round a temple court, tagged a temple of one level. Its
            // convex hull's box made an 89 × 55 m plinth and a 96 m pagoda; now it is a Newar compound building.
            TileData t = Tile();
            int[] u = { 20000, 20000, 26000, 20000, 26000, 26000, 25000, 26000, 25000, 21000, 21000, 21000, 21000, 26000, 20000, 26000 };
            t.Buildings.Add(Temple(BuildingArchetype.TemplePagoda, 7, 1, u));
            // A 60 × 50 m "temple" block (a temple complex mapped as one building).
            t.Buildings.Add(Temple(BuildingArchetype.TemplePagoda, 10, 2, 40000, 40000, 46000, 40000, 46000, 45000, 40000, 45000));
            // A 30 × 6 m "shikhara" and a 20 m "shrine": not those forms either.
            t.Buildings.Add(Temple(BuildingArchetype.TempleShikhara, 9, 1, 50000, 20000, 53000, 20000, 53000, 20600, 50000, 20600));
            t.Buildings.Add(Temple(BuildingArchetype.Shrine, 4, 1, 30000, 50000, 32000, 50000, 32000, 51500, 30000, 51500));
            var h = new TileHeightSampler(t, 1);
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                SacredKind kind;
                SacredParams p;
                Assert.That(SacredSelector.Classify(t, i), Is.EqualTo(SacredOutcome.Fallback), "outline " + i);
                Assert.That(SacredSelector.TrySelect(t, i, out kind, out p), Is.False, "outline " + i);
                // B1 (and beyond): the styled extrusion of the compound building, at the record's height.
                var b1 = new MeshData();
                Assert.That(SacredSelector.BuildGeneric(t, i, h, 1, b1, null), Is.True);
                double record = t.Buildings[i].HeightCm / 100.0;
                Assert.That(Top(b1) - 1300, Is.LessThanOrEqualTo(record + 0.6), "B1 outline " + i);
                // B0: the house grammar (windows, doors, eaves), as tall, with its outline walls as colliders.
                var b0 = new MeshData();
                var c = new GenColliders();
                Assert.That(BuildingDetailMesher.One(t, i, h, new BuildingOptions(), b0, c), Is.True);
                Assert.That(Top(b0) - 1300, Is.LessThanOrEqualTo(record + 5.0), "B0 outline " + i + " (a pitched roof or a stair cabin and tank on the terrace at most)");
                Assert.That(c.Walls.Count, Is.GreaterThanOrEqualTo(t.Buildings[i].Rings[0].Length / 2), "outline walls " + i);
                bool openings = false;
                for (int v = 0; v < b0.VertexCount && !openings; v++)
                {
                    var ch = (MaterialChannel)(int)Math.Round(b0.Uv0[2 * v]);
                    openings = ch == MaterialChannel.Glass || ch == MaterialChannel.WoodCarved || ch == MaterialChannel.Wood;
                }
                Assert.That(openings, Is.True, "a house with windows and doors, not a slab: " + i);
            }
        }

        [Test]
        public void GenericHeightsAreCappedByTheRecordAndByRealTemples()
        {
            // A 13 m untagged outline (the pipeline estimated 7 m from one level): one tier, never the 15.6 m the plan
            // alone gives; a 20 m one with two estimated levels: at most 16 m (Jagannath and Vishwanath are 15 m).
            TileData t = Tile();
            t.Buildings.Add(Temple(BuildingArchetype.TemplePagoda, 7, 1, 10000, 10000, 11300, 10000, 11300, 11300, 10000, 11300));
            t.Buildings.Add(Temple(BuildingArchetype.TemplePagoda, 10, 2, 20000, 10000, 22000, 10000, 22000, 12000, 20000, 12000));
            t.Buildings.Add(Temple(BuildingArchetype.TempleShikhara, 9, 1, 30000, 10000, 31500, 10000, 31500, 11500, 30000, 11500));
            // The tallest generic structure anywhere: a tagged height is trusted up to the generic maximum.
            BuildingRecord tall = Temple(BuildingArchetype.TemplePagoda, 60, 3, 40000, 10000, 41200, 10000, 41200, 11200, 40000, 11200);
            tall.Flags = BuildingFlags.HeightTagged;
            t.Buildings.Add(tall);
            SacredKind kind;
            SacredParams p;
            Assert.That(SacredSelector.TrySelect(t, 0, out kind, out p), Is.True);
            Assert.That(p.Pagoda.TotalHeightM, Is.LessThanOrEqualTo(SacredSelector.MaxOneTierM + 1e-4));
            Assert.That(SacredSelector.TrySelect(t, 1, out kind, out p), Is.True);
            Assert.That(p.Pagoda.Tiers, Is.EqualTo(2));
            Assert.That(p.Pagoda.TotalHeightM, Is.LessThanOrEqualTo(Math.Min(SacredSelector.MaxTwoTierM, 15.0) + 1e-4));
            Assert.That(SacredSelector.TrySelect(t, 2, out kind, out p), Is.True);
            Assert.That(p.Shikhara.TotalHeightM, Is.LessThanOrEqualTo(Math.Min(SacredSelector.MaxShikharaHeightM, 13.5) + 1e-4));
            Assert.That(SacredSelector.TrySelect(t, 3, out kind, out p), Is.True);
            Assert.That(p.Pagoda.TotalHeightM, Is.LessThanOrEqualTo(SacredSelector.MaxTaggedM + 1e-4));
        }

        [Test]
        public void ATempleBesideARoadGivesWayToTheCorridor()
        {
            // A 7 m temple whose door faces a lane; the lane's drawn corridor (4.8 m at least) runs 1.2 m into the plan.
            TileData t = Tile();
            t.Buildings.Add(Temple(BuildingArchetype.TemplePagoda, 7, 1, 30000, 30000, 30700, 30000, 30700, 30700, 30000, 30700));
            // And a temple whose plan lies almost all in the same road.
            t.Buildings.Add(Temple(BuildingArchetype.TemplePagoda, 7, 1, 40000, 29600, 40500, 29600, 40500, 30100, 40000, 30100));
            t.Roads.Add(new RoadRecord { OsmWayId = 1, RoadClass = RoadClass.Residential, Points = new[] { 28000, 29880, 33000, 29880 }, WidthCm = 400 });
            IRoadCorridorQuery q = RoadCorridorIndex.ForTile(t);
            Assert.That(q.SignedDistance(t.Tile.X0 + 303, t.Tile.Z0 + 300.2), Is.LessThan(-0.5), "the corridor runs into the temple");
            var h = new TileHeightSampler(t, 1);
            SacredKind kind;
            SacredParams fitted, raw;
            Assert.That(SacredSelector.TrySelect(t, 0, q, out kind, out fitted), Is.True);
            Assert.That(kind, Is.EqualTo(SacredKind.Pagoda));
            Assert.That(SacredSelector.TrySelectUnfitted(t, 0, out kind, out raw), Is.True);
            Assert.That(fitted.Pagoda.PlinthD * fitted.Pagoda.PlinthW, Is.LessThan(raw.Pagoda.PlinthD * raw.Pagoda.PlinthW), "the plan gave way");
            var m = new MeshData();
            var c = new GenColliders();
            int lod;
            Assert.That(SacredSelector.BuildGeneric(t, 0, h, 0, m, c, q, out lod), Is.True);
            var where = new List<string>();
            Assert.That(InRoad(t, c, q, 1300, 0.05, where), Is.EqualTo(0), string.Join("; ", where));
            // The unfitted temple stood in the lane.
            var c0 = new GenColliders();
            SacredSelector.Build(raw, 0, new MeshData(), c0);
            Assert.That(InRoad(t, c0, q, 0, 0.05), Is.GreaterThan(0));
            // The temple whose plan lies almost all in the road: a wayside shrine on what is left, or nothing at all.
            m.Clear();
            c.Clear();
            SacredOutcome how = SacredSelector.Classify(t, 1, q);
            Assert.That(how == SacredOutcome.Omitted || how == SacredOutcome.Generic, Is.True, how.ToString());
            SacredSelector.BuildGeneric(t, 1, h, 0, m, c, q, out lod);
            Assert.That(InRoad(t, c, q, 1300, 0.05), Is.EqualTo(0));
        }

        [Test]
        public void NoGenericSacredStructurePutsABlockingColliderIntoADrawnCorridor()
        {
            // The reviewer's measure (final-ride sacred_colliders_in_roads.txt): 65 of 233 generic structures had blocking
            // colliders below 4 m in the drawn road corridors (2,317 m²), the Hiti Pokhari compound 978 m² of a 96 m
            // pagoda's plinth over a primary road. Every generic structure the B0 cells build (temples, stupas, shrines and
            // the compound buildings that stand in for oversized outlines), with the roads package's drawn corridors.
            HashSet<ulong> hidden = StreamingScene.Heroes.HiddenRefs as HashSet<ulong> ?? new HashSet<ulong>(StreamingScene.Heroes.HiddenRefs);
            int structures = 0, fallbacks = 0, omitted = 0, bad = 0;
            var report = new List<string>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                var h = new TileHeightSampler(t, 1);
                IRoadCorridorQuery q = RoadCorridorIndex.ForTile(t);
                var o = new BuildingOptions { Corridors = RoadCorridorIndex.ForTile, HiddenRefs = hidden };
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    BuildingRecord b = t.Buildings[i];
                    if (!SacredSelector.DrawsGeneric(b) || hidden.Contains(b.OsmRef)) continue;
                    SacredOutcome how = SacredSelector.Classify(t, i, q);
                    if (how == SacredOutcome.Omitted)
                    {
                        omitted++;
                        continue;
                    }
                    if (how == SacredOutcome.Fallback) fallbacks++;
                    var m = new MeshData();
                    var c = new GenColliders();
                    if (!BuildingDetailMesher.One(t, i, h, o, m, c)) continue;
                    structures++;
                    double ground = double.MaxValue;
                    int[] r = b.Rings[0];
                    for (int k = 0; k < r.Length / 2; k++)
                    {
                        float y;
                        if (h.TryHeightClamped(t.Tile.X0 + r[2 * k] / 100.0, t.Tile.Z0 + r[2 * k + 1] / 100.0, out y)) ground = Math.Min(ground, y);
                    }
                    var where = new List<string>();
                    int n = InRoad(t, c, q, ground, 0.05, where);
                    if (n > 0)
                    {
                        bad++;
                        if (report.Count < 12) report.Add(id + " b" + i + " osm " + b.OsmRef + " " + how + ": " + n + " points (" + string.Join(", ", where) + ")");
                    }
                    // Never a giant: the B0 build stays within the generic maximum over its ground.
                    Assert.That(Top(m) - ground, Is.LessThanOrEqualTo(SacredSelector.MaxTaggedM + 1.0), id + " b" + i + " " + how);
                }
            }
            TestContext.WriteLine("generic sacred structures {0} (compound buildings {1}), omitted in a road {2}, with blocking colliders in a corridor {3}",
                                  structures, fallbacks, omitted, bad);
            foreach (string s in report) TestContext.WriteLine(s);
            Assert.That(structures, Is.GreaterThan(150));
            Assert.That(omitted, Is.LessThanOrEqualTo(3), "only a temple that stands in the road itself is left out");
            Assert.That(bad, Is.EqualTo(0), string.Join("\n", report));
        }

        [Test]
        public void HitiPokhariCompoundIsABuildingNotA96MetrePagodaOverTheRoad()
        {
            // OSM way 206874129 (building=temple, 36 points): the U of ranges round the Narayan temple court across
            // Narayanhiti Path from Hiti Pokhari, Naxal (the owner's playtest #1). Framed on its convex hull's box it was a
            // 96 m pagoda on an 89 x 55 m plinth over Primary osm 165226102; it is a compound building of the house grammar.
            TileData t = StreamingSampleRegion.Tile(new TileId(10, 517, 162));
            int i = -1;
            for (int k = 0; k < t.Buildings.Count; k++)
                if (t.Buildings[k].OsmRef == 413748258UL)
                    i = k;
            Assert.That(i, Is.GreaterThanOrEqualTo(0), "osm way 206874129 in the sample");
            IRoadCorridorQuery q = RoadCorridorIndex.ForTile(t);
            Assert.That(SacredSelector.Classify(t, i, q), Is.EqualTo(SacredOutcome.Fallback));
            var h = new TileHeightSampler(t, 1);
            double ground = double.MaxValue;
            foreach (int[] ring in new[] { t.Buildings[i].Rings[0] })
                for (int k = 0; k < ring.Length / 2; k++)
                {
                    float y;
                    if (h.TryHeightClamped(t.Tile.X0 + ring[2 * k] / 100.0, t.Tile.Z0 + ring[2 * k + 1] / 100.0, out y)) ground = Math.Min(ground, y);
                }
            var b1 = new MeshData();
            Assert.That(SacredSelector.BuildGeneric(t, i, h, 1, b1, null, q, out _), Is.True);
            var b0 = new MeshData();
            var c = new GenColliders();
            Assert.That(BuildingDetailMesher.One(t, i, h, new BuildingOptions { Corridors = RoadCorridorIndex.ForTile }, b0, c), Is.True);
            TestContext.WriteLine("B1 top {0:0.0} m, B0 top {1:0.0} m over the ground", Top(b1) - ground, Top(b0) - ground);
            Assert.That(Top(b1) - ground, Is.LessThan(10), "B1");
            Assert.That(Top(b0) - ground, Is.LessThan(10), "B0");
            Assert.That(InRoad(t, c, q, ground, 0.05), Is.EqualTo(0), "nothing over Primary osm 165226102");
        }

        /// <summary>True when a stair ramp runs along a drawn steps, footway or path way (its axis within 30 degrees of the
        /// nearest such segment, its middle within 3 m of it): the stair is that road (Swayambhu's 365 steps).</summary>
        private static bool AlongFootWay(TileData t, in GenRamp r)
        {
            double mx = 0.5 * (r.X0 + r.X1), mz = 0.5 * (r.Z0 + r.Z1), dx = r.X1 - r.X0, dz = r.Z1 - r.Z0, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6) return false;
            foreach (RoadRecord road in t.Roads)
            {
                if (road.RoadClass != RoadClass.Steps && road.RoadClass != RoadClass.Footway && road.RoadClass != RoadClass.Path) continue;
                for (int k = 0; k + 1 < road.PointCount; k++)
                {
                    double ax = road.Points[2 * k] / 100.0, az = road.Points[2 * k + 1] / 100.0, bx = road.Points[2 * k + 2] / 100.0, bz = road.Points[2 * k + 3] / 100.0;
                    double ox, oz;
                    if (RoadCorridor.PointSeg(mx, mz, ax, az, bx, bz, out ox, out oz) > 3.0) continue;
                    double sx = bx - ax, sz = bz - az, sl = Math.Sqrt(sx * sx + sz * sz);
                    if (sl > 1e-6 && Math.Abs(dx * sx + dz * sz) / (len * sl) >= Math.Cos(Math.PI / 6)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Area (m², 0.25 m samples) of the colliders standing on the drawn carriageways (<see cref="SacredDrawnRoads"/>,
        /// tile-local): every plinth, platform, terrace, step, wall, post and solid whose bottom is below 4 m and whose top
        /// is more than 10 cm over the terrain, more than <paramref name="tolerance"/> inside a drawn road (a walkable step
        /// in a road is a platform the rider rides up onto). Stairs along a steps, footway or path way are that way.
        /// </summary>
        private static double OnDrawnRoads(TileData t, TileHeightSampler h, GenColliders c, double tolerance, List<string> where)
        {
            SacredDrawnRoads drawn = SacredDrawnRoads.For(t);
            double area = 0;
            Func<double, double, float, float, string, double, bool> hit = (x, z, y0, y1, what, w) =>
            {
                double sd = drawn.SignedDistance(x, z);
                if (sd >= -tolerance) return false;
                float g;
                if (!h.TryHeightClamped(t.Tile.X0 + x, t.Tile.Z0 + z, out g) || y0 > g + SacredSelector.BlockingBelowM || y1 < g + 0.10f) return false;
                area += w;
                if (where != null && where.Count < 4) where.Add(what + " " + (y0 - g).ToString("0.00") + ".." + (y1 - g).ToString("0.00") + " m, " + (-sd).ToString("0.00") + " m in");
                return true;
            };
            foreach (GenBox b in c.Boxes)
            {
                double c0 = Math.Cos(b.YawRad), s0 = Math.Sin(b.YawRad);
                for (double u = -b.HalfX + 0.125; u < b.HalfX; u += 0.25)
                for (double w = -b.HalfZ + 0.125; w < b.HalfZ; w += 0.25)
                    hit(b.CX + u * c0 - w * s0, b.CZ + u * s0 + w * c0, b.CY - b.HalfY, b.CY + b.HalfY, "box " + b.Flags, 0.0625);
            }
            foreach (GenCylinder cy in c.Cylinders)
                for (double u = -cy.Radius + 0.125; u < cy.Radius; u += 0.25)
                for (double w = -cy.Radius + 0.125; w < cy.Radius; w += 0.25)
                    if (u * u + w * w <= cy.Radius * cy.Radius) hit(cy.CX + u, cy.CZ + w, cy.Y0, cy.Y1, "cylinder", 0.0625);
            foreach (GenWall wl in c.Walls)
            {
                double len = Math.Sqrt((wl.X1 - wl.X0) * (wl.X1 - wl.X0) + (wl.Z1 - wl.Z0) * (wl.Z1 - wl.Z0));
                int k = Math.Max(1, (int)Math.Ceiling(len / 0.25));
                for (int i = 0; i < k; i++)
                    hit(wl.X0 + (wl.X1 - wl.X0) * (i + 0.5) / k, wl.Z0 + (wl.Z1 - wl.Z0) * (i + 0.5) / k, wl.Y0, wl.Y1, "wall", 0.25 * Math.Max(0.25, 2 * wl.HalfThickness));
            }
            foreach (GenRamp r in c.Ramps)
            {
                double dx = r.X1 - r.X0, dz = r.Z1 - r.Z0, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-6 || AlongFootWay(t, r)) continue;
                double nx = -dz / len, nz = dx / len;
                int k = Math.Max(1, (int)Math.Ceiling(len / 0.25));
                for (int i = 0; i < k; i++)
                for (double o = -r.HalfWidth + 0.125; o < r.HalfWidth; o += 0.25)
                {
                    double f = (i + 0.5) / k;
                    hit(r.X0 + dx * f + nx * o, r.Z0 + dz * f + nz * o, Math.Min(r.Y0, r.Y1) - 0.5f, r.Y0 + (r.Y1 - r.Y0) * (float)f, "stair", 0.0625);
                }
            }
            return area;
        }

        [Test]
        public void NoSacredPlinthPlatformStepOrWallStandsOnADrawnRoad()
        {
            // The whole sample (playtest #1): every generic sacred structure the B0 cells build and every hero replica
            // (LOD0 colliders, as the streamer registers them), against the carriageways as drawn. On base 5a7668a: 60 generic
            // structures (1,069 m²) and 2 heroes (338 m²: the corners of Boudhanath's square terrace boxes in its kora street,
            // the square box round Swayambhu's drum on its kora path).
            ISet<ulong> hidden = StreamingScene.Heroes.HiddenRefs;
            int generic = 0, genericOn = 0, heroes = 0, heroesOn = 0;
            double genericM2 = 0, heroM2 = 0;
            var report = new List<string>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                var h = new TileHeightSampler(t, 1);
                var o = new BuildingOptions { Corridors = RoadCorridorIndex.ForTile, HiddenRefs = hidden };
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    BuildingRecord b = t.Buildings[i];
                    if (!SacredSelector.DrawsGeneric(b) || hidden.Contains(b.OsmRef)) continue;
                    var c = new GenColliders();
                    if (!BuildingDetailMesher.One(t, i, h, o, new MeshData(), c)) continue;
                    generic++;
                    var where = new List<string>();
                    double a = OnDrawnRoads(t, h, c, 0.05, where);
                    if (a <= 0) continue;
                    genericOn++;
                    genericM2 += a;
                    report.Add(id + " b" + i + " osm " + b.OsmRef + ": " + a.ToString("0.0") + " m² (" + string.Join("; ", where) + ")");
                }
                foreach (HeritageRecord r in StreamingScene.Heroes.InTile(t.Tile.Key))
                {
                    var c = new GenColliders();
                    if (HeroBuilder.Build(r, t, 0, new MeshData(), c) <= 0) continue;
                    heroes++;
                    var where = new List<string>();
                    // Heroes keep their real plan; within 15 cm of a drawn edge is the squeezed kora path at Swayambhu
                    // (the drawn 2.5 m path overlaps the protected dome outline; open issue for the roads package).
                    double a = OnDrawnRoads(t, h, c, 0.15, where);
                    if (a <= 0) continue;
                    heroesOn++;
                    heroM2 += a;
                    report.Add(id + " " + r.Id + ": " + a.ToString("0.0") + " m² (" + string.Join("; ", where) + ")");
                }
            }
            TestContext.WriteLine("generic sacred structures {0}, on a drawn road {1} ({2:0.0} m²); heroes {3}, on a drawn road {4} ({5:0.0} m²)", generic,
                                  genericOn, genericM2, heroes, heroesOn, heroM2);
            foreach (string s in report) TestContext.WriteLine(s);
            Assert.That(generic, Is.GreaterThan(150));
            Assert.That(heroes, Is.GreaterThan(15));
            Assert.That(genericOn + heroesOn, Is.EqualTo(0), string.Join("\n", report));
        }

        [Test]
        public void BoudhanathTerracesAreTheTwentyCorneredMandalaOffItsKoraStreet()
        {
            // The real terraces (OSM ways 56688295 and 235084185) step back twice at every corner; the square terrace
            // boxes stood 3.6 m into the kora street (Pedestrian osm 1196490820) at the corners.
            TileData t = StreamingSampleRegion.Tile(new TileId(10, 521, 163));
            HeritageRecord boudha = null;
            foreach (HeritageRecord r in StreamingScene.Heroes.InTile(t.Tile.Key))
                if (r.Id == "her.ktm.boudhanath")
                    boudha = r;
            Assert.That(boudha, Is.Not.Null);
            var m = new MeshData();
            var c = new GenColliders();
            var stats = new SacredStats();
            Assert.That(HeroBuilder.Build(boudha, t, 0, m, c, stats), Is.GreaterThan(0));
            Assert.That(stats.Terraces, Is.EqualTo(3));
            Assert.That(stats.Niches, Is.EqualTo(147), "the prayer-wheel niches follow the mandala's walls");
            // Each terrace: two arms and the inner square (nine walkable boxes, none a full square), the drum as round solids.
            int walkable = 0;
            foreach (GenBox b in c.Boxes)
                if ((b.Flags & GenColliderFlags.Walkable) != 0 && Math.Max(b.HalfX, b.HalfZ) > 20)
                {
                    walkable++;
                    Assert.That(Math.Min(b.HalfX, b.HalfZ) / Math.Max(b.HalfX, b.HalfZ), Is.LessThan(0.55).Or.EqualTo(1.0).Within(1e-3));
                }
            Assert.That(walkable, Is.GreaterThanOrEqualTo(6));
            Assert.That(c.Cylinders.Count, Is.GreaterThanOrEqualTo(3), "drum, dome and spire");
            var h = new TileHeightSampler(t, 1);
            var where = new List<string>();
            Assert.That(OnDrawnRoads(t, h, c, 0.05, where), Is.EqualTo(0), string.Join("; ", where));
            Assert.That(InRoad(t, c, RoadCorridorIndex.ForTile(t), 1330, 0.05), Is.EqualTo(0), "off the kora street's corridor too");
        }
    }
}
