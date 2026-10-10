using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>W2_DESIGN 2.2-2.4 and 10.6 V3: storey stacks, archetypes, the B0 grammar's per-plot caps and drop order
    /// (never a bare box), the cost of cap retries, and the B0-B3 band budgets of the detail pass's band table on the
    /// densest tile (10/516/161, Asan).</summary>
    public class MeshingBuildingGrammarTests
    {
        private static readonly TileId Asan = new TileId(10, 516, 161);

        [Test]
        public void StoreyHeightsFollowTheArchetypeStacks()
        {
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.Newar, 0), Is.EqualTo(2.40f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.Newar, 1), Is.EqualTo(2.25f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.Newar, 2), Is.EqualTo(2.25f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.Newar, 3), Is.EqualTo(2.10f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.NewarHybrid, 2), Is.EqualTo(2.40f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.NewarHybrid, 3), Is.EqualTo(2.80f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.ModernUrban, 0), Is.EqualTo(3.0f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.ModernUrban, 0, true), Is.EqualTo(3.2f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.RanaPalace, 0), Is.EqualTo(4.5f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.RanaPalace, 1), Is.EqualTo(5.0f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.RanaPalace, 2), Is.EqualTo(4.0f));
            Assert.That(BuildingGrammar.StoreyHeightM(BuildingArchetype.Generic, 4), Is.EqualTo(3.0f));
        }

        [Test]
        public void ArchetypesResolveFromHintsTagsAndProfileShares()
        {
            var house = new BuildingRecord { Archetype = BuildingArchetype.Generic, Rings = new[] { new int[0] } };
            var bkt = new BuildingFrontRecord { Profile = StyleProfile.Bhaktapur };
            Assert.That(BuildingGrammar.ResolveArchetype(house, new BuildingFrontRecord { Profile = StyleProfile.Patan, Flags = (byte)BuildingFrontFlags.RanaHint }, 1),
                        Is.EqualTo(BuildingArchetype.RanaPalace));
            Assert.That(BuildingGrammar.ResolveArchetype(house, new BuildingFrontRecord { Profile = StyleProfile.Patan, Flags = (byte)BuildingFrontFlags.StructureMud }, 1),
                        Is.EqualTo(BuildingArchetype.Newar));
            BuildingArchetype rcc = BuildingGrammar.ResolveArchetype(house, new BuildingFrontRecord { Profile = StyleProfile.Patan, Flags = (byte)BuildingFrontFlags.StructureRcc }, 1);
            Assert.That(rcc, Is.EqualTo(BuildingArchetype.NewarHybrid).Or.EqualTo(BuildingArchetype.ModernUrban));
            var temple = new BuildingRecord { Archetype = BuildingArchetype.TemplePagoda, Rings = new[] { new int[0] } };
            Assert.That(BuildingGrammar.ResolveArchetype(temple, bkt, 1), Is.EqualTo(BuildingArchetype.TemplePagoda));
            Assert.That(BuildingGrammar.ResolveArchetype(house, new BuildingFrontRecord { Profile = StyleProfile.Metro }, 7), Is.EqualTo(BuildingArchetype.ModernUrban));
            // Shares: Bhaktapur 60 / 30 / 10, Thamel 5 / 15 / 80 (seeded, so the counts are exact and stable).
            var counts = new int[32];
            for (uint s = 0; s < 2000; s++) counts[(int)BuildingGrammar.ResolveArchetype(house, bkt, s * 2654435761u)]++;
            Assert.That(counts[(int)BuildingArchetype.Newar] / 2000.0, Is.EqualTo(0.60).Within(0.05));
            Assert.That(counts[(int)BuildingArchetype.NewarHybrid] / 2000.0, Is.EqualTo(0.30).Within(0.05));
            Array.Clear(counts, 0, counts.Length);
            var thamel = new BuildingFrontRecord { Profile = StyleProfile.Thamel };
            for (uint s = 0; s < 2000; s++) counts[(int)BuildingGrammar.ResolveArchetype(house, thamel, s * 2654435761u)]++;
            Assert.That(counts[(int)BuildingArchetype.ModernUrban] / 2000.0, Is.EqualTo(0.80).Within(0.05));
            Assert.That(BuildingGrammar.IsNewarProfile(StyleProfile.Panauti), Is.True);
            Assert.That(BuildingGrammar.IsNewarProfile(StyleProfile.Metro), Is.False);
            Assert.That(BuildingGrammar.For(StyleProfile.Bhaktapur).RoofPitchDeg, Is.EqualTo(35f));
            Assert.That(BuildingGrammar.For(StyleProfile.Bhaktapur).EaveOverhangM, Is.EqualTo(1.2f));
        }

        [Test]
        public void HousePlansStackStoreysOnThePlinth()
        {
            TileData t = StreamingSampleRegion.Tile(Asan);
            int checkedPlans = 0;
            for (int i = 0; i < t.Buildings.Count && checkedPlans < 400; i++)
            {
                HousePlan p = BuildingGrammar.Plan(t, i);
                if (!BuildingGrammar.IsHouse(p.Archetype) || p.Archetype == BuildingArchetype.Generic || (t.Buildings[i].Flags & BuildingFlags.HeightTagged) != 0) continue;
                float expect = p.PlinthM;
                for (int f = 0; f < p.Storeys; f++) expect += BuildingGrammar.StoreyHeightM(p.Archetype, f, p.ShopGround) * p.StoreyScale;
                Assert.That(p.WallTopM, Is.EqualTo(expect).Within(0.05), "building " + i + " " + p.Archetype);
                Assert.That(p.FloorBase(p.Storeys), Is.EqualTo(p.WallTopM).Within(0.05));
                checkedPlans++;
            }
            Assert.That(checkedPlans, Is.GreaterThan(100));
            // Determinism: the same building plans the same way twice.
            HousePlan a = BuildingGrammar.Plan(t, 123), b = BuildingGrammar.Plan(t, 123);
            Assert.That(b.Archetype == a.Archetype && b.Storeys == a.Storeys && b.Wall == a.Wall && b.Seed == a.Seed, Is.True);
        }

        private struct Tally
        {
            public long Tris;
            public int Count, Max, Bare;
        }

        /// <summary>A house mesh with fewer triangles than this is a box without openings (the old B1 fallback).</summary>
        private const int BareTris = 120;

        /// <summary>B0 triangles per building of a tile (houses only in the tally; parts and sacred buildings use their
        /// own budgets).</summary>
        private static Dictionary<int, int> B0Tris(TileData t, BuildingOptions o, out Tally houses, int stride = 1)
        {
            var h = new TileHeightSampler(t, 2);
            var per = new Dictionary<int, int>();
            houses = new Tally();
            var m = new MeshData();
            for (int i = 0; i < t.Buildings.Count; i += stride)
            {
                m.Clear();
                if (!BuildingDetailMesher.One(t, i, h, o, m, null)) continue;
                per[i] = m.TriangleCount;
                HousePlan p = BuildingGrammar.Plan(t, i);
                if (!BuildingGrammar.IsHouse(p.Archetype) || p.Sacred || (t.Buildings[i].Flags & BuildingFlags.Part) != 0) continue;
                houses.Tris += m.TriangleCount;
                houses.Count++;
                houses.Max = Math.Max(houses.Max, m.TriangleCount);
                if (m.TriangleCount < BareTris) houses.Bare++;
            }
            return per;
        }

        [Test]
        public void B0HousesAreRichAndHeldToTheirCapPerPlot()
        {
            TileData t = StreamingSampleRegion.Tile(Asan);
            Tally houses;
            HouseBuilder.Stats = default(HouseStats);
            Dictionary<int, int> per = B0Tris(t, new BuildingOptions(), out houses);
            HouseStats st = HouseBuilder.Stats;
            double avg = (double)houses.Tris / houses.Count;
            TestContext.WriteLine("B0 houses {0}: avg {1:0} tris, max {2}; plots {3}, over the cap at the lite level {4}", houses.Count, avg, houses.Max,
                                  st.Plots, st.OverCap);
            Assert.That(houses.Count, Is.GreaterThan(3000));
            // The detail pass: about three times the stage-1 grammar per house (rounded, real openings, lattice, props).
            Assert.That(avg, Is.GreaterThan(1500), "not the boxy stage-1 houses");
            // Every plot (one house of a row) keeps to the cap; only a plot too big even at the lite level is kept over it.
            Assert.That(st.OverCap, Is.LessThanOrEqualTo(st.Plots / 100), "plots over the cap at the lightest level");
            Assert.That(houses.Bare, Is.EqualTo(0), "a house in B0 is never a bare box");
            // Each tier's near ring holds the same way.
            for (int k = 0; k < 3; k++)
            {
                Tally tier;
                HouseBuilder.Stats = default(HouseStats);
                B0Tris(t, BuildingBandTable.NearOptions(k), out tier, 7);
                st = HouseBuilder.Stats;
                TestContext.WriteLine("tier {0} near ring: avg {1:0}, max {2}, plots {3}, over {4}", k, (double)tier.Tris / tier.Count, tier.Max, st.Plots, st.OverCap);
                Assert.That(st.OverCap, Is.LessThanOrEqualTo(Math.Max(1, st.Plots / 100)), "tier " + k);
                Assert.That(tier.Bare, Is.EqualTo(0), "tier " + k);
            }
            // Generic sacred buildings stay inside their own LOD0 budgets (§3.2: 6,000 for a 3-tier pagoda).
            for (int i = 0; i < t.Buildings.Count; i++)
                if (BuildingGrammar.Plan(t, i).Sacred && per.ContainsKey(i)) Assert.That(per[i], Is.LessThanOrEqualTo(6000), "sacred " + i);
            // Buildings with parts draw their parts instead, except generic sacred outlines, whose generator takes the
            // heights from the parts and draws them (§3.2); landmarks drop when the heroes take over.
            int sacredWithParts = 0;
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                if (SacredSelector.HostOf(t, i) >= 0) Assert.That(per.ContainsKey(i), Is.False, "part of a sacred outline " + i);
                if ((t.Buildings[i].Flags & BuildingFlags.HasParts) == 0) continue;
                if (SacredSelector.DrawsGeneric(t.Buildings[i]))
                {
                    Assert.That(per.ContainsKey(i), Is.True, "sacred outline with parts " + i);
                    sacredWithParts++;
                }
                else Assert.That(per.ContainsKey(i), Is.False);
            }
            Assert.That(sacredWithParts, Is.GreaterThan(0));
            var skip = new BuildingOptions { SkipLandmarks = true };
            int landmark = Enumerable.Range(0, t.Buildings.Count).First(i => (t.Buildings[i].Flags & BuildingFlags.Landmark) != 0);
            Assert.That(BuildingDetailMesher.One(t, landmark, new TileHeightSampler(t, 2), skip, new MeshData(), null), Is.False);
        }

        /// <summary>True when the mesh has real openings: glass, timber leaves or lattice, or a shop's shutter.</summary>
        private static bool HasOpenings(MeshData m)
        {
            for (int v = 0; v < m.VertexCount; v++)
            {
                var ch = (MaterialChannel)(int)m.Uv0[2 * v];
                if (ch == MaterialChannel.Glass || ch == MaterialChannel.Wood || ch == MaterialChannel.WoodCarved || ch == MaterialChannel.Metal) return true;
            }
            return false;
        }

        /// <summary>How far a house stands out in front of its front wall (z = <paramref name="z0"/>, facing south) in each
        /// 0.5 m height slice from 2.5 m over the ground up to <paramref name="top"/>, triangle by triangle (each counts in
        /// every slice its height range touches), leaving out the carved timber (frames and struts, which the lighter
        /// levels thin out by design).</summary>
        private static double[] FrontProfile(MeshData m, double ground, double z0, double top)
        {
            var slices = new double[Math.Max(1, (int)((top - 2.5) / 0.5))];
            for (int q = 0; q + 2 < m.IndexCount; q += 3)
            {
                int a = m.Indices[q], b = m.Indices[q + 1], c = m.Indices[q + 2];
                if ((MaterialChannel)(int)m.Uv0[2 * a] == MaterialChannel.WoodCarved) continue;
                double y0 = Math.Min(m.Positions[3 * a + 1], Math.Min(m.Positions[3 * b + 1], m.Positions[3 * c + 1])) - ground;
                double y1 = Math.Max(m.Positions[3 * a + 1], Math.Max(m.Positions[3 * b + 1], m.Positions[3 * c + 1])) - ground;
                double out0 = z0 - Math.Min(m.Positions[3 * a + 2], Math.Min(m.Positions[3 * b + 2], m.Positions[3 * c + 2]));
                if (out0 <= 0) continue;
                int k0 = Math.Max(0, (int)Math.Floor((y0 - 2.5) / 0.5)), k1 = Math.Min(slices.Length - 1, (int)Math.Floor((y1 - 2.5) / 0.5));
                for (int k = k0; k <= k1; k++) slices[k] = Math.Max(slices[k], out0);
            }
            return slices;
        }

        [Test]
        public void EveryDropLevelKeepsTheHouseStructure()
        {
            // The grammar forks its random draws per element, so the detail a level drops (relief, props, plants, grilles)
            // never shifts the structural choices that follow: a house keeps its balconies, cantilever, hoods, awnings
            // and sanjhya at the full, lite and flat levels, and nothing grows or vanishes when it crosses from the near
            // ring into the lite ring.
            var cases = new[]
            {
                (StyleProfile.Metro, BuildingArchetype.ModernUrban, true), (StyleProfile.Thamel, BuildingArchetype.ModernUrban, true),
                (StyleProfile.KathmanduCore, BuildingArchetype.ModernUrban, true), (StyleProfile.Metro, BuildingArchetype.ModernUrban, false),
                (StyleProfile.KathmanduCore, BuildingArchetype.NewarHybrid, true), (StyleProfile.Patan, BuildingArchetype.NewarHybrid, false),
                (StyleProfile.Bhaktapur, BuildingArchetype.Newar, false), (StyleProfile.Patan, BuildingArchetype.Newar, true),
            };
            int compared = 0, projecting = 0;
            foreach (var (profile, arch, shop) in cases)
            {
                for (int seed = 0; seed < 12; seed++)
                {
                    TileData t = MeshingChecks.SyntheticTile(Asan, (x, z) => 1300);
                    int X(double v) => (int)Math.Round(v * 100);
                    double w = 4.6 + 0.08 * seed;
                    t.Buildings.Add(new BuildingRecord
                    {
                        Archetype = arch, Levels = (byte)(4 + seed % 3), Seed = (uint)(7919 * seed + 31 * (int)profile + 3),
                        Rings = new[] { new[] { X(300), X(500), X(300 + w), X(500), X(300 + w), X(509), X(300), X(509) } },
                    });
                    var f = BuildingFrontRecord.Absent;
                    f.Profile = profile;
                    f.Area = AreaType.OldCore;
                    f.FrontEdge = 0;
                    f.ShopBays = (byte)(shop ? 1 : 0);
                    f.Flags = (byte)(arch == BuildingArchetype.Newar ? BuildingFrontFlags.StructureMud : BuildingFrontFlags.StructureRcc);
                    t.BuildingFronts.Add(f);
                    var h = new TileHeightSampler(t, 1);
                    HousePlan plan = BuildingGrammar.Plan(t, 0);
                    double top = plan.WallTopM;
                    double[][] prof = new double[3][];
                    int[] levels = { 0, BuildingBandTable.LiteDrop, BuildingBandTable.FlatDrop };
                    for (int l = 0; l < 3; l++)
                    {
                        var m = new MeshData();
                        Assert.That(BuildingDetailMesher.One(t, 0, h, new BuildingOptions { B0CapTris = int.MaxValue, B0BaseDrop = levels[l] }, m, null), Is.True);
                        prof[l] = FrontProfile(m, 1300, 500, top);
                    }
                    bool any = false;
                    for (int k = 0; k < prof[0].Length; k++)
                    {
                        any |= prof[0][k] > 0.5;
                        for (int l = 1; l < 3; l++)
                            Assert.That(prof[l][k], Is.EqualTo(prof[0][k]).Within(0.45),
                                        profile + " " + arch + " seed " + seed + ": level " + levels[l] + " at " + (2.5 + 0.5 * k) + " m stands out " + prof[l][k].ToString("0.00") +
                                        " m, the full level " + prof[0][k].ToString("0.00") + " m");
                    }
                    compared++;
                    if (any) projecting++;
                }
            }
            TestContext.WriteLine("{0} houses compared, {1} with balconies, cantilevers, hoods or bays", compared, projecting);
            Assert.That(projecting, Is.GreaterThan(compared / 4), "the cases include houses that stand out over the street");
        }

        [Test]
        public void EveryDropLevelDressesTheSameSideWalls()
        {
            // The side walls a house dresses (on a lane, on open ground seen from a road) and their openings are judged
            // on the walls' square-cornered extent, so rounding the corners at the richer levels never changes them.
            TileData t = StreamingSampleRegion.Tile(Asan);
            var h = new TileHeightSampler(t, 2);
            double ax, az;
            Geo.WorldFrame.LonLatToGame(85.3122, 27.7074, out ax, out az);
            ax -= Asan.X0;
            az -= Asan.Z0;
            int[] levels = { 0, BuildingBandTable.LiteDrop, BuildingBandTable.FlatDrop };
            int houses = 0, dressed = 0;
            var m = new MeshData();
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                int[] r = t.Buildings[i].Rings[0];
                double x = 0, z = 0;
                for (int k = 0; k < r.Length / 2; k++)
                {
                    x += r[2 * k] / 100.0 / (r.Length / 2);
                    z += r[2 * k + 1] / 100.0 / (r.Length / 2);
                }
                if ((x - ax) * (x - ax) + (z - az) * (z - az) > 100 * 100) continue;
                if (!BuildingGrammar.IsHouse(BuildingGrammar.Plan(t, i).Archetype)) continue;
                var walls = new long[3];
                var openings = new long[3];
                for (int l = 0; l < 3; l++)
                {
                    HouseBuilder.Stats = default(HouseStats);
                    m.Clear();
                    BuildingDetailMesher.One(t, i, h, new BuildingOptions { B0CapTris = int.MaxValue, B0BaseDrop = levels[l] }, m, null);
                    walls[l] = HouseBuilder.Stats.SideWalls;
                    openings[l] = HouseBuilder.Stats.SideOpenings;
                }
                houses++;
                if (walls[0] > 0) dressed++;
                for (int l = 1; l < 3; l++)
                {
                    Assert.That(walls[l], Is.EqualTo(walls[0]), "building " + i + ": side walls dressed at level " + levels[l]);
                    Assert.That(openings[l], Is.EqualTo(openings[0]), "building " + i + ": side openings at level " + levels[l]);
                }
            }
            TestContext.WriteLine("{0} houses within 100 m of Asan, {1} with side facades", houses, dressed);
            Assert.That(dressed, Is.GreaterThan(20));
        }

        [Test]
        public void HousesOverTheCapDropDetailPerPlotAndNeverBecomeTheB1Box()
        {
            TileData t = StreamingSampleRegion.Tile(Asan);
            var h = new TileHeightSampler(t, 2);
            int tested = 0;
            for (int i = 0; i < t.Buildings.Count && tested < 25; i++)
            {
                HousePlan p = BuildingGrammar.Plan(t, i);
                if (!BuildingGrammar.IsHouse(p.Archetype) || p.Sacred || (t.Buildings[i].Flags & (BuildingFlags.Part | BuildingFlags.HasParts)) != 0) continue;
                if (p.Archetype != BuildingArchetype.Newar) continue;
                var full = new MeshData();
                HouseBuilder.Stats = default(HouseStats);
                BuildingDetailMesher.One(t, i, h, new BuildingOptions { B0CapTris = 1000000 }, full, null);
                long plots = HouseBuilder.Stats.Plots;
                if (full.TriangleCount < 600 || plots < 1) continue;
                var b1 = new MeshData();
                BuildingMesher.Build(SingleTile(t, i), new TileHeightSampler(SingleTile(t, i), 2), new BuildingOptions(), b1);
                // A cap a little under the mean plot cost: the plots that overflow are rebuilt lighter, the others stay.
                var dropped = new MeshData();
                BuildingDetailMesher.One(t, i, h, new BuildingOptions { B0CapTris = (int)(0.9 * full.TriangleCount / plots) }, dropped, null);
                Assert.That(dropped.TriangleCount, Is.LessThan(full.TriangleCount), "building " + i);
                Assert.That(dropped.TriangleCount, Is.GreaterThan(b1.TriangleCount), "a lighter B0 level, not the B1 extrusion");
                MeshingChecks.AssertWellFormed(dropped, "dropped " + i);
                // A cap under every level keeps the lite level: openings, frames and lattice, never the bare extrusion.
                var floor = new MeshData();
                BuildingDetailMesher.One(t, i, h, new BuildingOptions { B0CapTris = 10 }, floor, null);
                var lite = new MeshData();
                BuildingDetailMesher.One(t, i, h, BuildingBandTable.LiteOptions(), lite, null);
                Assert.That(floor.TriangleCount, Is.LessThanOrEqualTo(1.2 * lite.TriangleCount),
                            "the lightest level is the lite ring's (with corners rounded or not, by the base level)");
                Assert.That(floor.TriangleCount, Is.LessThanOrEqualTo(dropped.TriangleCount));
                Assert.That(floor.TriangleCount, Is.GreaterThan(2 * b1.TriangleCount), "lite houses are still detailed");
                Assert.That(HasOpenings(floor), Is.True, "lite houses keep their openings");
                MeshingChecks.AssertWellFormed(floor, "lite " + i);
                tested++;
            }
            Assert.That(tested, Is.GreaterThan(5));
        }

        [Test]
        public void CapRetriesStayLocalAndTheLowTierDoesNoMoreWorkThanHigh()
        {
            TileData t = StreamingSampleRegion.Tile(Asan);
            var built = new long[3];
            for (int k = 0; k < 3; k++)
            {
                Tally tally;
                HouseBuilder.Stats = default(HouseStats);
                B0Tris(t, BuildingBandTable.NearOptions(k), out tally, 3);
                HouseStats st = HouseBuilder.Stats;
                built[k] = st.BuiltTris;
                TestContext.WriteLine("tier {0}: plots {1}, rebuilt {2} ({3:P0}), built {4} tris for {5} kept ({6:0.00}x)", k, st.Plots, st.Rebuilds,
                                      (double)st.Rebuilds / st.Plots, st.BuiltTris, tally.Tris, (double)st.BuiltTris / Math.Max(1, tally.Tris));
                // Only the plots that overflow are rebuilt, once, at the level their measured cost predicts.
                Assert.That(st.Rebuilds, Is.LessThanOrEqualTo(0.3 * st.Plots), "tier " + k + ": rebuilds per plot");
                Assert.That((double)st.BuiltTris / Math.Max(1, tally.Tris), Is.LessThanOrEqualTo(1.5), "tier " + k + ": work per kept triangle");
            }
            Assert.That(built[0], Is.LessThanOrEqualTo(built[2]), "the Low tier (the weakest phones) does no more work than High");
            // The lite ring builds every plot once.
            HouseBuilder.Stats = default(HouseStats);
            Tally lite;
            B0Tris(t, BuildingBandTable.LiteOptions(), out lite, 3);
            Assert.That(HouseBuilder.Stats.Rebuilds, Is.EqualTo(0));
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BuildingRecord, TileData> Singles =
            new System.Runtime.CompilerServices.ConditionalWeakTable<BuildingRecord, TileData>();

        /// <summary>A copy of the tile holding just building <paramref name="i"/> (same terrain), for B1 of one house.</summary>
        private static TileData SingleTile(TileData t, int i)
        {
            return Singles.GetValue(t.Buildings[i], _ =>
            {
                var s = new TileData { Tile = t.Tile, HeightsN = t.HeightsN, HeightsQ = t.HeightsQ, Biomes = t.Biomes, BiomesN = t.BiomesN };
                s.Buildings.Add(t.Buildings[i]);
                if (t.HasBuildingFronts) s.BuildingFronts.Add(t.BuildingFronts[i]);
                return s;
            });
        }

        [Test]
        public void BandTotalsAtAsanFitTheBudgetTable()
        {
            TileData t = StreamingSampleRegion.Tile(Asan);
            var h = new TileHeightSampler(t, 2);
            var counts = new Dictionary<BuildingBand, double>();
            foreach (BuildingBand band in new[] { BuildingBand.B1Styled, BuildingBand.B2Prism, BuildingBand.B3Block })
            {
                // The B1 layer as the band table configures it (styled, with the front detail).
                var m = new MeshData();
                BuildingOptions o = band == BuildingBand.B1Styled ? BuildingBandTable.B1Options() : new BuildingOptions { Band = band };
                int drawn = BuildingMesher.Build(t, h, o, m);
                Assert.That(drawn, Is.GreaterThan(0), band.ToString());
                MeshingChecks.AssertWellFormed(m, band.ToString());
                Assert.That(m.HasUv0, Is.True, band + " carries channels and AO");
                counts[band] = (double)m.TriangleCount / t.Buildings.Count;
            }
            var b1Plain = new MeshData();
            BuildingMesher.Build(t, h, new BuildingOptions(), b1Plain);
            Assert.That((double)b1Plain.TriangleCount / t.Buildings.Count, Is.LessThan(counts[BuildingBand.B1Styled]),
                        "the band table's B1 adds the front detail to the default extrusion");
            TestContext.WriteLine("tris per building: B1 {0:0.0}, B2 {1:0.0}, B3 {2:0.0}", counts[BuildingBand.B1Styled], counts[BuildingBand.B2Prism],
                                  counts[BuildingBand.B3Block]);
            Assert.That(counts[BuildingBand.B1Styled], Is.LessThanOrEqualTo(70 * 1.15));
            Assert.That(counts[BuildingBand.B2Prism], Is.LessThanOrEqualTo(10 * 1.15));
            Assert.That(counts[BuildingBand.B3Block], Is.LessThan(counts[BuildingBand.B2Prism]));

            // §2.4 budget check with the detail pass's band table (B1 with its front detail, as the table configures it),
            // honestly: Asan (the chowk) lies
            // 36 m from the east edge of its tile, so the rings reach into the neighbouring tiles; every band is
            // measured on the 3 × 3 tiles around it. B0: each house by its distance, in the near ring at the tier's cap
            // or in the lite ring at the tier's lite level (flat on Low); B1-B3: each triangle weighted by the band
            // shader's cross-fade (BandConfig.Opacity); 40% of everything in the frustum (W2_DESIGN 2.4).
            double ax, az;
            Geo.WorldFrame.LonLatToGame(85.3122, 27.7074, out ax, out az);
            var tiles = new List<TileData>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    TileData n = StreamingSampleRegion.Tile(new TileId(10, Asan.Tx + dx, Asan.Ty + dz));
                    if (n != null && n.Buildings.Count > 0) tiles.Add(n);
                }
            Assert.That(tiles.Count, Is.EqualTo(9), "the 3 × 3 tiles around Asan are in the sample");
            string[] tier = { "Low", "Mid", "High" };
            float[] w2B0 = { 35f, 60f, 80f };
            var far = new double[3, 4];
            var b0 = new double[3];
            var inNear = new int[3];
            var inLite = new int[3];
            var one = new MeshData();
            foreach (TileData n in tiles)
            {
                var hn = new TileHeightSampler(n, 2);
                double cx = ax - n.Tile.X0, cz = az - n.Tile.Z0;
                for (int band = 1; band <= 3; band++)
                {
                    var m = new MeshData();
                    BuildingMesher.Build(n, hn, band == 1 ? BuildingBandTable.B1Options() : new BuildingOptions { Band = band == 2 ? BuildingBand.B2Prism : BuildingBand.B3Block }, m);
                    for (int q = 0; q + 2 < m.IndexCount; q += 3)
                    {
                        double x = 0, z = 0;
                        for (int c = 0; c < 3; c++)
                        {
                            x += m.Positions[3 * m.Indices[q + c]] / 3.0;
                            z += m.Positions[3 * m.Indices[q + c] + 2] / 3.0;
                        }
                        double d = Math.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                        for (int k = 0; k < 3; k++) far[k, band] += BandOpacity(d, BuildingBandTable.OuterM(k, band - 1), BuildingBandTable.OuterM(k, band));
                    }
                }
                double[] dist = Distances(n, cx, cz);
                for (int k = 0; k < 3; k++)
                {
                    BuildingOptions near = BuildingBandTable.NearOptions(k), lite = BuildingBandTable.LiteOptions(k);
                    for (int i = 0; i < n.Buildings.Count; i++)
                    {
                        if (dist[i] >= BuildingBandTable.OuterM(k, 0)) continue;
                        bool inner = dist[i] < BuildingBandTable.NearOuterM(k);
                        one.Clear();
                        if (!BuildingDetailMesher.One(n, i, hn, inner ? near : lite, one, null)) continue;
                        b0[k] += one.TriangleCount;
                        if (inner) inNear[k]++;
                        else inLite[k]++;
                    }
                }
            }
            for (int k = 0; k < 3; k++)
            {
                Assert.That(BuildingBandTable.OuterM(k, 0), Is.GreaterThanOrEqualTo(w2B0[k]), tier[k] + ": B0 keeps the W2 radius");
                double slice = BuildingBandTable.SliceTris(k), vB0 = 0.4 * b0[k], vFar = 0.4 * (far[k, 1] + far[k, 2] + far[k, 3]);
                TestContext.WriteLine("{0}: B0 {1:0} ({2} near < {3} m, {4} lite) of its share {5}; B1 {6:0} B2 {7:0} B3 {8:0}; total {9:0} (slice {10}, x{11})",
                                      tier[k], vB0, inNear[k], BuildingBandTable.NearOuterM(k), inLite[k], BuildingBandTable.B0ShareTris(k), 0.4 * far[k, 1],
                                      0.4 * far[k, 2], 0.4 * far[k, 3], vB0 + vFar, slice, BuildingBandTable.SliceHeadroom);
                Assert.That(vB0, Is.LessThanOrEqualTo(BuildingBandTable.B0ShareTris(k)), tier[k] + ": B0 within its share");
                Assert.That(vB0 + vFar, Is.LessThanOrEqualTo(slice * BuildingBandTable.SliceHeadroom), tier[k] + ": the building slice");
                Assert.That(BuildingBandTable.B0ShareTris(k) + vFar, Is.LessThanOrEqualTo(slice * BuildingBandTable.SliceHeadroom),
                            tier[k] + ": the B0 share and the far bands fit the slice (the EditMode band test relies on it)");
                Assert.That(inNear[k] + inLite[k], Is.GreaterThan(30), tier[k] + ": B0 covers the street around the camera");
            }
        }

        /// <summary>The band shader's cross-fade (World/Buildings BandConfig.Opacity): 1 inside [inner, outer], ramping
        /// linearly over 4 m centred on each edge; B0's inner edge (0) has no fade.</summary>
        private static double BandOpacity(double d, double inner, double outer)
        {
            const double Fade = 4.0;
            double a = inner <= 0 ? 1.0 : Math.Max(0.0, Math.Min(1.0, (d - (inner - 0.5 * Fade)) / Fade));
            double b = Math.Max(0.0, Math.Min(1.0, ((outer + 0.5 * Fade) - d) / Fade));
            return Math.Min(a, b);
        }

        /// <summary>Building centroid distances (m) from the tile-local point (cx, cz).</summary>
        private static double[] Distances(TileData t, double cx, double cz)
        {
            var dist = new double[t.Buildings.Count];
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                int[] r = t.Buildings[i].Rings[0];
                double x = 0, z = 0;
                for (int k = 0; k < r.Length / 2; k++)
                {
                    x += r[2 * k] / 100.0;
                    z += r[2 * k + 1] / 100.0;
                }
                dist[i] = Math.Sqrt(Math.Pow(x / (r.Length / 2) - cx, 2) + Math.Pow(z / (r.Length / 2) - cz, 2));
            }
            return dist;
        }

        /// <summary>Houses within 120 m of a place drawn at a tier's near-ring options and at the lite and flat levels:
        /// none may be a box without openings (the old fallback dropped up to 93% of a Patan lane's area to bare prisms).</summary>
        private static void AssertNoBareHouses(string name, TileData t, double lx, double lz)
        {
            var h = new TileHeightSampler(t, 1);
            var m = new MeshData();
            for (int k = 0; k <= 4; k++)
            {
                BuildingOptions o = k < 3 ? BuildingBandTable.NearOptions(k) : k == 3 ? BuildingBandTable.LiteOptions(1) : BuildingBandTable.LiteOptions(0);
                // The flat level lays a small house's few openings on its walls: half the bar there (still twice the B1 box).
                int houses = 0, bare = 0, noOpenings = 0, bar = k == 4 ? BareTris / 2 : BareTris;
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    int[] r = t.Buildings[i].Rings[0];
                    double x = 0, z = 0;
                    for (int q = 0; q < r.Length / 2; q++)
                    {
                        x += r[2 * q] / 100.0 / (r.Length / 2);
                        z += r[2 * q + 1] / 100.0 / (r.Length / 2);
                    }
                    if ((x - lx) * (x - lx) + (z - lz) * (z - lz) > 120 * 120) continue;
                    HousePlan p = BuildingGrammar.Plan(t, i);
                    if (!BuildingGrammar.IsHouse(p.Archetype) || p.Sacred || (t.Buildings[i].Flags & (BuildingFlags.Part | BuildingFlags.HasParts)) != 0) continue;
                    m.Clear();
                    if (!BuildingDetailMesher.One(t, i, h, o, m, null)) continue;
                    houses++;
                    if (m.TriangleCount < bar) bare++;
                    if (!HasOpenings(m)) noOpenings++;
                }
                string what = name + (k < 3 ? " tier " + k + " near" : k == 3 ? " lite" : " flat");
                TestContext.WriteLine("{0}: {1} houses, {2} under {3} tris, {4} without openings", what, houses, bare, bar, noOpenings);
                Assert.That(houses, Is.GreaterThan(50), what);
                Assert.That(bare, Is.LessThanOrEqualTo(houses / 50), what + ": at most 2% bare");
                Assert.That(noOpenings, Is.LessThanOrEqualTo(houses / 50), what + ": at most 2% without openings");
            }
        }

        [Test]
        public void NoHouseInB0IsABareBoxAtAnyTier()
        {
            TileData asan = StreamingSampleRegion.Tile(Asan);
            double ax, az;
            Geo.WorldFrame.LonLatToGame(85.3122, 27.7074, out ax, out az);
            AssertNoBareHouses("Asan", asan, ax - Asan.X0, az - Asan.Z0);
            // Patan and Bhaktapur from the valley pack when it is built here (pipeline/build is not committed).
            string path = System.IO.Path.Combine(GoldenFiles.RepoRoot, "pipeline", "build", "regions", "kathmandu_valley", "kathmandu_valley.ghpk");
            if (!System.IO.File.Exists(path))
            {
                TestContext.WriteLine("valley pack not built here: Patan and Bhaktapur skipped");
                return;
            }
            var valley = new PackReader(System.IO.File.ReadAllBytes(path));
            foreach (var (name, lon, lat) in new[] { ("Patan lane", 85.3250, 27.6745), ("Bhaktapur lane", 85.4318, 27.6720), ("Taumadhi", 85.4290, 27.6712) })
            {
                double gx, gz;
                Geo.WorldFrame.LonLatToGame(lon, lat, out gx, out gz);
                TileId id = TileId.At(10, gx, gz);
                TileData t = valley.ReadTile(id);
                AssertNoBareHouses(name, t, gx - id.X0, gz - id.Z0);
            }
        }
    }
}
