using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>W2_DESIGN 2.2-2.4 and 10.6 V3: storey stacks, archetypes, the B0 grammar's caps and drop order, and
    /// the B0-B3 band budgets on the densest tile (10/516/161, Asan).</summary>
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
            public int Count, Max;
        }

        /// <summary>B0 triangles per building of the densest tile (houses only; parts and sacred buildings use
        /// their own budgets).</summary>
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
            }
            return per;
        }

        [Test]
        public void B0HousesAreRichButNeverExceedTheCap()
        {
            TileData t = StreamingSampleRegion.Tile(Asan);
            Tally houses;
            Dictionary<int, int> per = B0Tris(t, new BuildingOptions(), out houses);
            double avg = (double)houses.Tris / houses.Count;
            TestContext.WriteLine("B0 houses {0}: avg {1:0} tris, max {2}", houses.Count, avg, houses.Max);
            Assert.That(houses.Count, Is.GreaterThan(3000));
            // The detail pass: about three times the stage-1 grammar per house (rounded, real openings, lattice, props),
            // held under the per-house cap by the drop levels.
            Assert.That(avg, Is.GreaterThan(1500), "not the boxy stage-1 houses");
            Assert.That(avg, Is.LessThanOrEqualTo(4200));
            Assert.That(houses.Max, Is.LessThanOrEqualTo(BuildingBandTable.B0CapTris));
            // The Low tier's cap holds too.
            Tally low;
            B0Tris(t, new BuildingOptions { B0CapTris = BuildingBandTable.B0CapFor(0) }, out low, 7);
            TestContext.WriteLine("B0 houses at the Low cap: avg {0:0}, max {1}", (double)low.Tris / low.Count, low.Max);
            Assert.That(low.Max, Is.LessThanOrEqualTo(BuildingBandTable.B0CapFor(0)));
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

        [Test]
        public void HousesOverTheCapDropDetailBeforeFallingBackToB1()
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
                BuildingDetailMesher.One(t, i, h, new BuildingOptions { B0CapTris = 1000000 }, full, null);
                if (full.TriangleCount < 600) continue;
                var b1 = new MeshData();
                BuildingMesher.Build(SingleTile(t, i), new TileHeightSampler(SingleTile(t, i), 2), new BuildingOptions(), b1);
                var dropped = new MeshData();
                BuildingDetailMesher.One(t, i, h, new BuildingOptions { B0CapTris = full.TriangleCount - 1 }, dropped, null);
                Assert.That(dropped.TriangleCount, Is.LessThan(full.TriangleCount), "building " + i);
                Assert.That(dropped.TriangleCount, Is.GreaterThan(b1.TriangleCount), "a lighter B0 level, not the B1 fallback");
                MeshingChecks.AssertWellFormed(dropped, "dropped " + i);
                // A cap under every level falls back to the styled extrusion.
                var floor = new MeshData();
                BuildingDetailMesher.One(t, i, h, new BuildingOptions { B0CapTris = 10 }, floor, null);
                Assert.That(floor.TriangleCount, Is.LessThan(dropped.TriangleCount));
                tested++;
            }
            Assert.That(tested, Is.GreaterThan(5));
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
                var m = new MeshData();
                int drawn = BuildingMesher.Build(t, h, new BuildingOptions { Band = band, FrontDetail = true }, m);
                Assert.That(drawn, Is.GreaterThan(0), band.ToString());
                MeshingChecks.AssertWellFormed(m, band.ToString());
                Assert.That(m.HasUv0, Is.True, band + " carries channels and AO");
                counts[band] = (double)m.TriangleCount / t.Buildings.Count;
            }
            TestContext.WriteLine("tris per building: B1 {0:0.0}, B2 {1:0.0}, B3 {2:0.0}", counts[BuildingBand.B1Styled], counts[BuildingBand.B2Prism],
                                  counts[BuildingBand.B3Block]);
            Assert.That(counts[BuildingBand.B1Styled], Is.LessThanOrEqualTo(70 * 1.15));
            Assert.That(counts[BuildingBand.B2Prism], Is.LessThanOrEqualTo(10 * 1.15));
            Assert.That(counts[BuildingBand.B3Block], Is.LessThan(counts[BuildingBand.B2Prism]));

            // §2.4 budget check with the detail pass's band table: buildings in each band's ring around the camera at
            // Asan, 40% in the frustum; B0 at the tier's cap.
            double cx, cz;
            Geo.WorldFrame.LonLatToGame(85.3122, 27.7074, out cx, out cz);
            cx -= Asan.X0;
            cz -= Asan.Z0;
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
            string[] tier = { "Low", "Mid", "High" };
            var b0 = new MeshData();
            for (int k = 0; k < 3; k++)
            {
                var o0 = new BuildingOptions { B0CapTris = BuildingBandTable.B0CapFor(k) };
                double total = 0;
                var bandTris = new double[4];
                int inB0 = 0;
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    double d = dist[i];
                    int band = d < BuildingBandTable.OuterM(k, 0) ? 0 : d < BuildingBandTable.OuterM(k, 1) ? 1 : d < BuildingBandTable.OuterM(k, 2) ? 2 :
                               d < BuildingBandTable.OuterM(k, 3) ? 3 : -1;
                    if (band < 0) continue;
                    double tris;
                    if (band == 0)
                    {
                        b0.Clear();
                        tris = BuildingDetailMesher.One(t, i, h, o0, b0, null) ? b0.TriangleCount : 0;
                        inB0++;
                    }
                    else tris = band == 1 ? counts[BuildingBand.B1Styled] : band == 2 ? counts[BuildingBand.B2Prism] : counts[BuildingBand.B3Block];
                    bandTris[band] += 0.4 * tris;
                    total += 0.4 * tris;
                }
                double slice = BuildingBandTable.SliceTris(k);
                TestContext.WriteLine("{0}: B0 {1:0} ({2} buildings) B1 {3:0} B2 {4:0} B3 {5:0} total {6:0} (table {7})", tier[k], bandTris[0], inB0, bandTris[1],
                                      bandTris[2], bandTris[3], total, slice);
                Assert.That(total, Is.LessThanOrEqualTo(slice * 1.15), tier[k]);
                Assert.That(inB0, Is.GreaterThan(3), tier[k] + ": B0 still covers the street around the camera");
            }
        }
    }
}
