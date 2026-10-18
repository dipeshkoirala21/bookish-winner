using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ghumante.Core.Aviation;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.Core.Traffic;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// W2 perf pass, V11-style: the draw calls of the life presenters at Asan (the sample's densest street) per tier,
    /// measured with the real simulations and parked placement through the very plans the presenters run
    /// (<see cref="TrafficDrawPlan"/>, <see cref="CrowdDrawPlan"/>), summed with the rest of the frame's slices
    /// (<see cref="LifeDrawBudget"/>) against 10.4's 120 / 200 / 300 SRP batches. Also: the livery-neutral parked levels
    /// show every livery exactly, and the before/after numbers of the pass.
    /// </summary>
    public class TrafficDrawBudgetTests
    {
        private static readonly string[] TierName = { "Low", "Mid", "High" };

        [Test]
        public void TheFrameBudgetAddsUpPerTier()
        {
            int[] totals = { 120, 200, 300 };
            for (int tier = 0; tier <= 2; tier++)
            {
                LifeDrawBudget b = LifeDrawBudget.ForTier(tier);
                Assert.That(b.Total, Is.EqualTo(totals[tier]));
                Assert.That(b.Sum, Is.EqualTo(b.Total), TierName[tier] + ": the slices fill the frame exactly");
                Assert.That(b.Outlines, Is.EqualTo(tier > 0), "Low draws no outline pass (ToonLookTier)");
                Assert.That(b.MovingDraws, Is.GreaterThan(0));
                Assert.That(b.ParkedDraws, Is.GreaterThan(0));
                Assert.That(b.CrowdDraws, Is.GreaterThan(0));
                Assert.That(b.Cost(true), Is.EqualTo(b.Outlines ? 2 : 1));
                Assert.That(b.Cost(false), Is.EqualTo(1));
            }
            Assert.That(TrafficDrawKeys.Outlined(VehicleLod.Lod0, false) && TrafficDrawKeys.Outlined(VehicleLod.Lod1, false));
            Assert.That(TrafficDrawKeys.Outlined(VehicleLod.Lod2, false), Is.False, "far traffic is outline-free");
            foreach (VehicleLod lod in new[] { VehicleLod.Lod2, VehicleLod.Block, VehicleLod.Box })
                Assert.That(TrafficDrawKeys.Outlined(lod, true), Is.False, "parked vehicles are outline-free");
            // Block-outs and boxes share one batch per variant and level whatever the livery and model type.
            Assert.That(TrafficDrawKeys.Body(3, 0, 0, VehicleLod.Block, false), Is.EqualTo(TrafficDrawKeys.Body(3, 5, 2, VehicleLod.Block, true)));
            Assert.That(TrafficDrawKeys.Body(3, 0, 0, VehicleLod.Box, false), Is.Not.EqualTo(TrafficDrawKeys.Body(3, 0, 0, VehicleLod.Block, false)));
            Assert.That(TrafficDrawKeys.Body(3, 0, 0, VehicleLod.Lod2, false), Is.Not.EqualTo(TrafficDrawKeys.Body(3, 1, 0, VehicleLod.Lod2, false)));
        }

        [Test]
        public void NeutralParkedLevelsShowEveryLiveryExactly()
        {
            var neutral = new MeshData(256, 768);
            var baked = new MeshData(256, 768);
            int tinted = 0;
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                foreach (VehicleLod lod in new[] { VehicleLod.Block, VehicleLod.Box })
                {
                    neutral.Clear();
                    int tris = ParkedLook.BuildNeutral(v, lod, neutral);
                    Assert.That(tris, Is.GreaterThan(0));
                    int masked = 0;
                    for (int i = 0; i < neutral.VertexCount; i++)
                        if (neutral.Colors[i * 4 + 3] == 255) masked++;
                    Assert.That(masked, Is.GreaterThan(0), e.AssetId + " " + lod + " has body paint to tint");
                    tinted += masked;
                    for (int l = 0; l < e.LiveryCount; l++)
                    {
                        baked.Clear();
                        VehicleMesher.Build(v, (byte)l, 0, lod, baked);
                        Assert.That(baked.VertexCount, Is.EqualTo(neutral.VertexCount));
                        Assert.That(baked.IndexCount, Is.EqualTo(neutral.IndexCount));
                        uint tint = ParkedLook.TintOf(v, (byte)l);
                        for (int i = 0; i < baked.VertexCount; i++)
                        {
                            uint want = (uint)(baked.Colors[i * 4] << 24 | baked.Colors[i * 4 + 1] << 16 | baked.Colors[i * 4 + 2] << 8 | 0xFF);
                            Assert.That(ParkedLook.Shown(neutral, i, tint), Is.EqualTo(want), e.AssetId + " " + lod + " livery " + l + " vertex " + i);
                            Assert.That(neutral.Positions[i * 3 + 1], Is.EqualTo(baked.Positions[i * 3 + 1]));
                        }
                    }
                }
            }
            Assert.That(tinted, Is.GreaterThan(0));
            DumpParkedRows();
        }

        /// <summary>Preview (GHUMANTE_PREVIEW_DIR): every variant's block-out in each livery, baked (west row) and drawn
        /// livery-neutral with the tint applied as the shader does (east row), side by side.</summary>
        private static void DumpParkedRows()
        {
            if (!ObjDump.Enabled) return;
            var baked = new MeshData(4096, 12288);
            var tintedRow = new MeshData(4096, 12288);
            var one = new MeshData(256, 768);
            float z = 0f;
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                float x = 0f;
                for (int l = 0; l < e.LiveryCount; l++)
                {
                    foreach (VehicleLod lod in new[] { VehicleLod.Block, VehicleLod.Box })
                    {
                        float dx = x + (lod == VehicleLod.Box ? 40f : 0f);
                        one.Clear();
                        VehicleMesher.Build(v, (byte)l, 0, lod, one);
                        ObjDump.Append(baked, one, dx, 0f, z);
                        one.Clear();
                        ParkedLook.BuildNeutral(v, lod, one);
                        uint tint = ParkedLook.TintOf(v, (byte)l);
                        for (int i = 0; i < one.VertexCount; i++)
                        {
                            uint c = ParkedLook.Shown(one, i, tint);
                            one.Colors[i * 4] = (byte)(c >> 24);
                            one.Colors[i * 4 + 1] = (byte)(c >> 16);
                            one.Colors[i * 4 + 2] = (byte)(c >> 8);
                            one.Colors[i * 4 + 3] = 255;
                        }
                        ObjDump.Append(tintedRow, one, dx + 90f, 0f, z);
                    }
                    x += 4.5f;
                }
                z += 12f;
            }
            ObjDump.Write("perf-life/parked_baked_vs_tinted.obj", new ObjPart("baked", baked), new ObjPart("tinted", tintedRow));
        }

        [Test]
        public void TheDrawKeySetCountsDistinctBatchesUnderItsCap()
        {
            var set = new DrawKeySet(8);
            Assert.That(set.TryUse(1, 2, 5), Is.True);
            Assert.That(set.TryUse(1, 2, 5), Is.True, "a batch in use costs nothing more");
            Assert.That(set.Draws, Is.EqualTo(2));
            Assert.That(set.TryUse(2, 2, 5), Is.True);
            Assert.That(set.TryUse(3, 2, 5), Is.False, "a new batch over the cap is refused");
            Assert.That(set.Refused, Is.EqualTo(1));
            Assert.That(set.TryUse(4, 1, 5), Is.True);
            Assert.That(set.Count, Is.EqualTo(3));
            set.Clear();
            Assert.That(set.Count + set.Draws + set.Refused, Is.EqualTo(0));
            Assert.That(set.Has(1), Is.False);
            for (ulong k = 0; k < 8; k++) Assert.That(set.TryUse(k * 0x10000000000UL, 1, 100), Is.True);
            Assert.That(set.Count, Is.EqualTo(8));
        }

        // -------------------------------------------------------------------------------------------------------------
        // Asan

        private sealed class Measured
        {
            public int Moving, Parked, Crowd, MovingBefore, ParkedBefore, CrowdBefore;
            public int MovingFree, ParkedFree, CrowdFree;
            public int ParkedDrawn, ParkedAssigned, VehiclesDrawn, VehiclesAssigned, People, Overflow, Snapped, Demoted, Fallbacks, TrafficOverflow;
        }

        [TestCase("Asan", 85.3122, 27.7074)]
        [TestCase("Boudha", 85.3620, 27.7215)]
        [TestCase("Basantapur", 85.3070, 27.7043)]
        public void LifeDrawsHoldTheFrameBudgetPerTier(string place, double lon, double lat)
        {
            WorldFrame.LonLatToGame(lon, lat, out double ax, out double az);
            var report = new StringBuilder();
            report.AppendLine(place + ", worst of 16 headings x 6 sim times (15-40 s); draws include outline passes (Mid, High), shadow casters excluded");
            var all = new Measured[3];
            for (int tier = 0; tier <= 2; tier++)
            {
                Measured m = all[tier] = MeasureAt(tier, ax, az);
                LifeDrawBudget b = LifeDrawBudget.ForTier(tier);
                int life = m.Moving + m.Parked + m.Crowd, lifeBefore = m.MovingBefore + m.ParkedBefore + m.CrowdBefore;
                int rest = b.Total - b.MovingDraws - b.ParkedDraws - b.CrowdDraws;
                report.AppendFormat("{0,-4}: moving {1} (cap {2}, uncapped {24}, before {3}), parked {4} (slice {5} + what traffic left, uncapped {25}, before {6}; {7}/{8} drawn), " +
                                    "crowd {9} (cap {10}, uncapped {26}, before {11}; {12} people, demoted {13}, snapped {14}, past the cap {15}); " +
                                    "life {16} (before {17}) + rest of frame {18} = {19} of {20}; vehicles {21}/{22} drawn, {23} fell back to a coarser batch",
                                    TierName[tier], m.Moving, b.MovingDraws, m.MovingBefore, m.Parked, b.ParkedDraws, m.ParkedBefore, m.ParkedDrawn, m.ParkedAssigned,
                                    m.Crowd, b.CrowdDraws, m.CrowdBefore, m.People, m.Demoted, m.Snapped, m.Overflow, life, lifeBefore, rest, life + rest, b.Total,
                                    m.VehiclesDrawn, m.VehiclesAssigned, m.Fallbacks, m.MovingFree, m.ParkedFree, m.CrowdFree);
                report.AppendLine();
            }
            TestContext.WriteLine(report.ToString());
            for (int tier = 0; tier <= 2; tier++)
            {
                Measured m = all[tier];
                LifeDrawBudget b = LifeDrawBudget.ForTier(tier);
                int life = m.Moving + m.Parked + m.Crowd, lifeBefore = m.MovingBefore + m.ParkedBefore + m.CrowdBefore;
                int rest = b.Total - b.MovingDraws - b.ParkedDraws - b.CrowdDraws;
                Assert.That(life + rest, Is.LessThanOrEqualTo(b.Total), TierName[tier] + ": life draws at " + place + " fit the frame");
                Assert.That(m.Moving, Is.LessThanOrEqualTo(b.MovingDraws));
                Assert.That(m.Moving + m.Parked, Is.LessThanOrEqualTo(b.TrafficDraws));
                Assert.That(m.Crowd, Is.LessThanOrEqualTo(b.CrowdDraws));
                // The caps cut waste, not life: every assigned vehicle and person is drawn.
                Assert.That(m.ParkedDrawn, Is.EqualTo(m.ParkedAssigned), TierName[tier] + ": every parked vehicle within the tier caps is drawn");
                Assert.That(m.VehiclesDrawn, Is.EqualTo(m.VehiclesAssigned), TierName[tier] + ": every moving vehicle within the tier caps is drawn");
                Assert.That(m.Overflow, Is.EqualTo(0), TierName[tier] + ": every person within the tier caps is drawn under the cap");
                Assert.That(m.TrafficOverflow, Is.EqualTo(0), TierName[tier] + ": every vehicle within the tier caps is drawn under the cap");
                Assert.That(m.People, Is.GreaterThan(0));
                Assert.That(m.ParkedAssigned, Is.GreaterThan(0));
                // Parked batches: one per variant and level for the tinted levels (was one per livery too, and outlined on Mid
                // and High). Since the playtest the parked vehicles near the bazaars are few and of many kinds (a car, a micro,
                // a scooter: one livery each), so on Low, without outlines to save, fewer batches is what is left to gain.
                if (b.Outlines) Assert.That(m.Parked * 2, Is.LessThanOrEqualTo(m.ParkedBefore), TierName[tier] + ": parked draws at least halved");
                else Assert.That(m.Parked, Is.LessThan(m.ParkedBefore), TierName[tier] + ": fewer parked draws than one per livery");
                Assert.That(life, Is.LessThan(lifeBefore), TierName[tier] + ": fewer life draws than before");
            }
        }

        private static readonly float[] Mix = { 52, 15, 3, 0, 10, 1, 3, 2, 0.5f, 0, 0 };
        private const float MixTotal = 86.5f;

        [Test]
        public void AFullStreetKeepsItsNearestPeopleAndDegradesFarOnly()
        {
            // Every band full (the plan's caps: 6 / 20 / 78 on High) with the street's archetype mix and one shape per
            // person: more than any sample anchor shows (the sims put 7-40 people within 90 m), so the cap bites. The
            // nearest people keep their skinned bodies; past the cap far people share a drawn frame of their shape or a
            // sibling's, and nobody blinks out.
            for (int tier = 0; tier <= 2; tier++)
            {
                LifeDrawBudget b = LifeDrawBudget.ForTier(tier);
                int[] caps = CrowdLodPlan.Caps[tier];
                var plan = new CrowdDrawPlan();
                plan.Begin(b);
                uint h = 99u;
                int skinned = 0, far = 0, people = 0;
                for (int band = 0; band <= 2; band++)
                    for (int i = 0; i < caps[band]; i++, people++)
                    {
                        h = CharMath.Hash(h, (uint)people);
                        // The sim's urban archetype mix (PedestrianSim, S 3.2) and carry odds, as CharactersMeshCrowdTests.
                        float u = CharMath.Unit(h) * MixTotal;
                        int a = 0;
                        for (; a < Mix.Length - 1; a++)
                        {
                            u -= Mix[a];
                            if (u < 0f) break;
                        }
                        float c = CharMath.Unit(CharMath.Hash(h, 9u));
                        int carry = a == (int)PedArchetype.Porter ? 1 : c < 0.10f ? 1 + (int)(CharMath.Hash(h, 11u) % 4u) : 0;
                        int key = CrowdVariants.KeyOf((PedArchetype)a, StreetStyle.Urban, carry, people, (int)(CharMath.Hash(h, 7u) % 16u));
                        double phase = CharMath.Unit(CharMath.Hash(h, 13u)) * 2.0 * Math.PI;
                        int shape = CrowdVariants.ShapeKey(key);
                        int drawn = plan.Place(ref shape, band, PedClip.Walk, ref phase);
                        if (band <= 1 && people < caps[0] + caps[1])
                            Assert.That(drawn, Is.EqualTo(band), TierName[tier] + ": the near and mid people keep their skinned bodies");
                        if (drawn <= 1 && drawn >= 0) skinned++;
                        else if (drawn == 2) far++;
                    }
                TestContext.WriteLine(TierName[tier] + ": " + people + " people at full bands: " + plan.Draws + " draws (cap " + b.CrowdDraws + "), " + skinned +
                                      " skinned, " + far + " far, " + plan.Snapped + " far walkers share a drawn frame, " + plan.StoodIn + " drawn as a sibling shape, " + plan.Overflow + " past the cap");
                Assert.That(plan.Draws, Is.LessThanOrEqualTo(b.CrowdDraws));
                Assert.That(skinned, Is.EqualTo(caps[0] + caps[1]));
                Assert.That(plan.Overflow, Is.EqualTo(0), TierName[tier] + ": everybody is drawn under the cap, the far overflow as a drawn frame or a sibling shape");
            }
        }

        /// <summary>
        /// Owner playtest #6 ("absolutely no traffic") and #4 (flicker): the draw caps may only coarsen what a vehicle or
        /// a person is drawn as, never hide one. A street fuller and more varied than any sample anchor (every LOD band at
        /// the tier's cap, a different catalogue entry, livery and model type for each vehicle, people of every archetype
        /// and carry prop in equal shares) breaks the caps on Mid and High: every moving vehicle, every parked vehicle,
        /// every fleet tag and every person is still drawn, two-wheelers and rickshaws keep their rider (never the
        /// riderless parked block-out), and what goes past the cap is bounded (one batch per catalogue entry at most).
        /// </summary>
        [Test]
        public void AnOverfullStreetNeverHidesAVehicleOrAPerson()
        {
            var failures = new List<string>();
            for (int tier = 0; tier <= 2; tier++)
            {
                LifeDrawBudget b = LifeDrawBudget.ForTier(tier);
                LifeLod lod = LifeLod.ForTier(tier);
                DressingConfig dressing = DressingConfig.ForTier(tier);
                var plan = new TrafficDrawPlan();
                plan.Begin(b);
                uint h = 7u + (uint)tier;
                int moving = 0, movingDrawn = 0, riderless = 0, id = 1;
                // Moving traffic: every band at its cap, nearest first, a different look each.
                for (int level = 0; level <= 2; level++)
                    for (int i = 0; i < lod.VehicleCaps[level]; i++, id++)
                    {
                        h = CharMath.Hash(h, (uint)id);
                        int variant = (int)(h % (uint)VehicleCatalog.Count);
                        VehicleCatalogEntry e = VehicleCatalog.At(variant);
                        byte livery = (byte)(CharMath.Hash(h, 3u) % Math.Max(1u, e.LiveryCount));
                        byte model = VehicleMesher.ModelFor(variant, (uint)id);
                        bool rider = TrafficDrawKeys.CarriesRider(e.TrafficClass);
                        WheelSocket[] ws = VehicleMesher.Wheels(e, model % VehicleMesher.ModelCount(variant));
                        TrafficDraw d = plan.PlaceMoving(level, id, variant, livery, model, rider, ws, true, true);
                        moving++;
                        if (d != TrafficDraw.None) movingDrawn++;
                        if (rider && d == TrafficDraw.Block) riderless++;
                    }
                int movingDraws = plan.Draws;
                // Parked: the tier's parked caps, every catalogue entry in turn, a fleet vehicle among every five.
                int parked = 0, parkedDrawn = 0, tags = 0, tagsDrawn = 0;
                int[] caps = { dressing.ParkedNearCap, dressing.ParkedBlockCap, Math.Max(0, dressing.ParkedCap - dressing.ParkedNearCap - dressing.ParkedBlockCap) };
                for (int level = 0; level <= 2; level++)
                    for (int i = 0; i < caps[level]; i++, id++)
                    {
                        h = CharMath.Hash(h, (uint)id);
                        int variant = (int)(h % (uint)VehicleCatalog.Count);
                        VehicleCatalogEntry e = VehicleCatalog.At(variant);
                        byte livery = (byte)(CharMath.Hash(h, 5u) % Math.Max(1u, e.LiveryCount));
                        TrafficDraw d = plan.PlaceParked(level, variant, livery, VehicleSeeds.FleetModel(variant, (uint)id));
                        parked++;
                        if (d == TrafficDraw.None) continue;
                        parkedDrawn++;
                        if (i % 5 != 0 || d == TrafficDraw.Box) continue;
                        tags++;
                        if (plan.TryFleetTag()) tagsDrawn++;
                    }
                // People: every band at its cap, archetypes and carry props in equal shares (no street mix to lean on).
                var crowd = new CrowdDrawPlan();
                crowd.Begin(b);
                int[] pcaps = CrowdLodPlan.Caps[tier];
                int people = 0, peopleDrawn = 0;
                int archetypes = Enum.GetValues(typeof(PedArchetype)).Length;
                for (int band = 0; band <= 2; band++)
                    for (int i = 0; i < pcaps[band]; i++, people++)
                    {
                        h = CharMath.Hash(h, (uint)people + 1000u);
                        var a = (PedArchetype)(people % archetypes);
                        int key = CrowdVariants.KeyOf(a, (StreetStyle)(people / archetypes % 3), people % 5, people, (int)(h % 16u));
                        double phase = CharMath.Unit(CharMath.Hash(h, 13u)) * 2.0 * Math.PI;
                        int shape = CrowdVariants.ShapeKey(key);
                        PedClip clip = people % 3 == 0 ? PedClip.Idle : PedClip.Walk;
                        if (crowd.Place(ref shape, band, clip, ref phase) >= 0) peopleDrawn++;
                    }
                TestContext.WriteLine(TierName[tier] + ": moving " + movingDrawn + "/" + moving + " drawn in " + movingDraws + " draws (cap " + b.MovingDraws +
                                      "), riderless " + riderless + "; parked " + parkedDrawn + "/" + parked + ", tags " + tagsDrawn + "/" + tags + ", traffic " +
                                      plan.Draws + " draws (cap " + b.TrafficDraws + ", " + plan.Overflow + " batches past it); people " + peopleDrawn + "/" + people +
                                      " in " + crowd.Draws + " draws (cap " + b.CrowdDraws + ", " + crowd.Overflow + " past it)");
                if (movingDrawn != moving) failures.Add(TierName[tier] + ": " + (moving - movingDrawn) + " moving vehicles hidden");
                if (riderless != 0) failures.Add(TierName[tier] + ": " + riderless + " two-wheelers drawn without their rider");
                if (parkedDrawn != parked) failures.Add(TierName[tier] + ": " + (parked - parkedDrawn) + " parked vehicles hidden");
                if (tagsDrawn != tags) failures.Add(TierName[tier] + ": " + (tags - tagsDrawn) + " fleet tags hidden");
                if (peopleDrawn != people) failures.Add(TierName[tier] + ": " + (people - peopleDrawn) + " people hidden");
                // What goes past the cap is bounded: the shared last fallbacks (a moving LOD2 look per rider vehicle, a
                // block-out and a box per catalogue entry, the fleet tags), never one batch per vehicle beyond those.
                if (plan.Overflow > lod.VehicleCaps[0] + lod.VehicleCaps[1] + lod.VehicleCaps[2] + 2 * VehicleCatalog.Count + 1)
                    failures.Add(TierName[tier] + ": " + plan.Overflow + " traffic batches past the cap");
                if (crowd.Overflow > people) failures.Add(TierName[tier] + ": " + crowd.Overflow + " crowd batches past the cap");
            }
            Assert.That(failures, Is.Empty, "the caps coarsen, they never hide");
        }

        /// <summary>The budget with the life caps lifted (what the presenters would draw without them).</summary>
        private static LifeDrawBudget Uncapped(LifeDrawBudget b)
        {
            return new LifeDrawBudget
            {
                Total = b.Total, Outlines = b.Outlines, StaticWorld = b.StaticWorld, NearBuildings = b.NearBuildings, Dressing = b.Dressing,
                Wildlife = b.Wildlife, Aircraft = b.Aircraft, Player = b.Player, MovingDraws = 100000, ParkedDraws = 100000, CrowdDraws = 100000,
            };
        }

        /// <summary>The detail tiles of a tier around a point (level-10 tiles within the tier's detail radius).</summary>
        private static List<TileId> DetailTiles(int tier, double x, double z)
        {
            StreamingConfig cfg = StreamingConfig.ForTier(tier);
            Dictionary<TileId, TileData> tiles = DrivingData.SampleTiles();
            TileId c = TileId.At(10, x, z);
            var list = new List<TileId>();
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    var id = new TileId(10, c.Tx + dx, c.Ty + dz);
                    if (!tiles.ContainsKey(id)) continue;
                    double nx = Math.Max(id.X0, Math.Min(x, id.X0 + id.Size)), nz = Math.Max(id.Z0, Math.Min(z, id.Z0 + id.Size));
                    if ((nx - x) * (nx - x) + (nz - z) * (nz - z) > cfg.DetailRadiusM * cfg.DetailRadiusM) continue;
                    list.Add(id);
                }
            Assert.That(list, Does.Contain(c));
            return list;
        }

        private struct Parked
        {
            public ParkedVehicle P;
            public double X, Z;
        }

        private static Measured MeasureAt(int tier, double ax, double az)
        {
            Dictionary<TileId, TileData> tiles = DrivingData.SampleTiles();
            List<TileId> ids = DetailTiles(tier, ax, az);
            LifeDrawBudget budget = LifeDrawBudget.ForTier(tier);
            LifeLod lod = LifeLod.ForTier(tier);
            DressingConfig dressing = DressingConfig.ForTier(tier);
            MeshingSettings settings = MeshingSettings.ForTier(tier);

            // Parked vehicles of the detail tiles (as the tile worker places them) and the officers of their chowks.
            var parked = new List<Parked>();
            var officers = new List<KeyValuePair<double, double>>();
            foreach (TileId id in ids)
            {
                TileData t = tiles[id];
                var list = new List<ParkedVehicle>();
                ParkedPlacement.Place(t, TileHeightSampler.ForArea(t, id, 1), list, settings.Roads);
                foreach (ParkedVehicle p in list) parked.Add(new Parked { P = p, X = id.X0 + p.X, Z = id.Z0 + p.Z });
                foreach (OfficerPost o in OfficerPosts.For(t)) officers.Add(new KeyValuePair<double, double>(id.X0 + o.X, id.Z0 + o.Z));
            }

            CuratedDb db = CuratedDb.Read(File.ReadAllBytes(DrivingData.SamplePath(".curated.ghcd")));
            var m = new Measured();
            using (var host = new LifeHost(LifeSettings.ForTier(tier), 0x1234UL, TrafficData.Routes, null, db))
            {
                foreach (TileId id in ids) host.AddTile(id, tiles[id]);
                var plan = new TrafficDrawPlan();
                var crowd = new CrowdDrawPlan();
                var freePlan = new TrafficDrawPlan();
                var freeCrowd = new CrowdDrawPlan();
                LifeDrawBudget free = Uncapped(budget);
                var scratch = new Measured();
                var wheels = new Dictionary<int, WheelSocket[]>();
                for (int step = 0; step <= 400; step++)
                {
                    host.Tick(0.1f, ax, az, 0f, 1f, 9f, 10, 0f, WeatherKind.Clear);
                    host.Wait();
                    if (step < 150 || step % 50 != 0) continue;
                    Assert.IsNull(host.Error);
                    for (int h = 0; h < 16; h++)
                    {
                        double heading = h * Math.PI / 8;
                        MeasureTraffic(host, plan, budget, lod, dressing, parked, wheels, ax, az, Math.Sin(heading), Math.Cos(heading), m);
                        MeasureTraffic(host, freePlan, free, lod, dressing, parked, wheels, ax, az, Math.Sin(heading), Math.Cos(heading), scratch);
                    }
                    MeasureCrowd(host, crowd, budget, lod, officers, ax, az, m);
                    MeasureCrowd(host, freeCrowd, free, lod, officers, ax, az, scratch);
                    m.MovingFree = Math.Max(m.MovingFree, scratch.Moving);
                    m.ParkedFree = Math.Max(m.ParkedFree, scratch.Parked);
                    m.CrowdFree = Math.Max(m.CrowdFree, scratch.Crowd);
                }
            }
            return m;
        }

        private static void MeasureTraffic(LifeHost host, TrafficDrawPlan plan, LifeDrawBudget budget, LifeLod lod, DressingConfig dressing, List<Parked> parked,
                                           Dictionary<int, WheelSocket[]> wheels, double ax, double az, double fx, double fz, Measured m)
        {
            int n = host.VehicleCount;
            var dist = new float[n];
            var keys = new float[n];
            var level = new int[n];
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                AgentPose a = host.Vehicles[i];
                double dx = a.X - ax, dz = a.Z - az;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                // As the presenter: far agents behind the camera are not drawn.
                if (d > 15f && dx * fx + dz * fz < -0.2 * d) d = float.MaxValue;
                dist[i] = d;
            }
            LifeLod.Assign(dist, n, lod.VehicleCaps, lod.VehicleRadii, level, order, keys);
            plan.Begin(budget);
            var before = new HashSet<ulong>();
            int beforeDraws = 0, outlined = budget.Outlines ? 2 : 1, assigned = 0, drawn = 0, fallbacks = 0;
            for (int k = 0; k < n; k++)
            {
                int i = order[k];
                if (level[i] < 0) continue;
                AgentPose a = host.Vehicles[i];
                int variant = Math.Max(0, Math.Min(VehicleCatalog.Count - 1, (int)a.Variant));
                byte model = VehicleMesher.ModelFor(variant, (uint)a.AgentId);
                bool rider = TrafficDrawKeys.CarriesRider(a.Class);
                int wk = variant * 16 + model % VehicleMesher.ModelCount(variant);
                if (!wheels.TryGetValue(wk, out WheelSocket[] ws)) wheels[wk] = ws = VehicleMesher.Wheels(VehicleCatalog.At(variant), model % VehicleMesher.ModelCount(variant));
                assigned++;
                // Worst case for draws: every plated and LOD1 body is built.
                TrafficDraw d = plan.PlaceMoving(level[i], a.AgentId, variant, a.Livery, model, rider, ws, true, true);
                if (d != TrafficDraw.None) drawn++;
                if ((level[i] == 0 && d != TrafficDraw.Unique) || (level[i] == 1 && d != TrafficDraw.Lod1) || (level[i] == 2 && d != TrafficDraw.Lod2)) fallbacks++;
                // Before the pass: one batch per body and wheel key, every one outlined on Mid and High.
                VehicleLod bl = level[i] == 0 ? VehicleLod.Lod0 : level[i] == 1 ? VehicleLod.Lod1 : VehicleLod.Lod2;
                ulong bk = level[i] == 0 ? TrafficDrawKeys.Unique(a.AgentId) : TrafficDrawKeys.Body(variant, a.Livery, model, bl, rider);
                if (before.Add(bk)) beforeDraws += outlined;
                if (level[i] <= 1)
                    foreach (WheelSocket w in ws)
                        if (before.Add(TrafficDrawKeys.Wheel(w.Style, w.Radius, w.Width, bl)))
                            beforeDraws += outlined;
            }
            int moving = plan.Draws;
            int movingBefore = beforeDraws;

            // Parked, nearest first under the tier caps, after the moving traffic (as the presenter).
            int pn = 0;
            var pd = new float[parked.Count];
            var pl = new int[parked.Count];
            var po = new int[parked.Count];
            var pk = new float[parked.Count];
            var pidx = new int[parked.Count];
            for (int i = 0; i < parked.Count; i++)
            {
                double dx = parked[i].X - ax, dz = parked[i].Z - az;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d > dressing.ParkedBoxM) continue;
                pidx[pn] = i;
                pd[pn++] = d;
            }
            int[] caps = { dressing.ParkedNearCap, dressing.ParkedBlockCap, Math.Max(0, dressing.ParkedCap - dressing.ParkedNearCap - dressing.ParkedBlockCap) };
            float[] radii = { dressing.ParkedNearM, dressing.ParkedBlockM, dressing.ParkedBoxM };
            LifeLod.Assign(pd, pn, caps, radii, pl, po, pk);
            var parkedBefore = new HashSet<ulong>();
            int pAssigned = 0, pDrawn = 0, tagBefore = 0;
            bool tag = false;
            for (int k = 0; k < pn; k++)
            {
                int j = po[k];
                if (pl[j] < 0) continue;
                ParkedVehicle p = parked[pidx[j]].P;
                int variant = Math.Max(0, Math.Min(VehicleCatalog.Count - 1, (int)p.Variant));
                byte model = VehicleSeeds.FleetModel(variant, p.Id);
                pAssigned++;
                TrafficDraw d = plan.PlaceParked(pl[j], variant, p.Livery, model);
                if (d != TrafficDraw.None)
                {
                    pDrawn++;
                    if (p.CommunityFleet && d != TrafficDraw.Box) tag |= plan.TryFleetTag();
                }
                // Before: (variant, livery, model, level) per batch, outlined on Mid and High.
                VehicleLod lv = pl[j] == 0 ? VehicleLod.Lod2 : pl[j] == 1 ? VehicleLod.Block : VehicleLod.Box;
                parkedBefore.Add(((ulong)(uint)variant << 40) | ((ulong)p.Livery << 32) | ((ulong)(pl[j] == 0 ? model : 0) << 16) | ((ulong)lv << 8));
                if (p.CommunityFleet && pl[j] <= 1) tagBefore = 1;
            }
            int parkedDraws = plan.Draws - moving;
            int parkedDrawsBefore = (parkedBefore.Count + tagBefore) * (budget.Outlines ? 2 : 1);
            if (moving + parkedDraws >= m.Moving + m.Parked)
            {
                m.Moving = moving;
                m.Parked = parkedDraws;
                m.MovingBefore = Math.Max(m.MovingBefore, movingBefore);
                m.ParkedBefore = Math.Max(m.ParkedBefore, parkedDrawsBefore);
            }
            m.ParkedAssigned = Math.Max(m.ParkedAssigned, pAssigned);
            m.ParkedDrawn = Math.Max(m.ParkedDrawn, pDrawn);
            if (assigned > m.VehiclesAssigned)
            {
                m.VehiclesAssigned = assigned;
                m.VehiclesDrawn = drawn;
            }
            else if (assigned == m.VehiclesAssigned) m.VehiclesDrawn = Math.Min(m.VehiclesDrawn, drawn);
            m.Fallbacks = Math.Max(m.Fallbacks, fallbacks);
            m.TrafficOverflow = Math.Max(m.TrafficOverflow, plan.Overflow);
        }

        private static void MeasureCrowd(LifeHost host, CrowdDrawPlan plan, LifeDrawBudget budget, LifeLod lod, List<KeyValuePair<double, double>> officers,
                                         double ax, double az, Measured m)
        {
            int n = host.PeopleCount;
            var dist = new float[n];
            var keys = new float[n];
            var level = new int[n];
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                PedPose p = host.People[i];
                double dx = p.X - ax, dz = p.Z - az;
                dist[i] = (float)Math.Sqrt(dx * dx + dz * dz);
            }
            LifeLod.Assign(dist, n, lod.PeopleCaps, lod.PeopleRadii, level, order, keys);
            plan.Begin(budget);
            var farBefore = new HashSet<ulong>();
            int skinnedBefore = 0, people = 0;
            AreaTypeGrid areas = host.AreaTypes;
            for (int k = 0; k < n; k++)
            {
                int i = order[k];
                if (level[i] < 0) continue;
                PedPose p = host.People[i];
                AreaType area = areas != null ? areas.At(p.X, p.Z) : AreaType.Unknown;
                int key = CrowdVariants.KeyOf((PedArchetype)p.Archetype, StreetStyles.At(p.X, p.Z, area), p.CarryProp, p.AgentId, p.Tint);
                var clip = (PedClip)p.ClipId;
                double phase = CrowdAnimation.WalkPhase(p.ClipTime, p.SpeedMps, clip == PedClip.Run);
                people++;
                if (CrowdLodPlan.Skinned(level[i])) skinnedBefore++;
                else farBefore.Add(CrowdDrawPlan.FarKey(CrowdVariants.ShapeKey(key), CrowdVariants.FrameOf(clip, phase)));
                int shape = CrowdVariants.ShapeKey(key);
                plan.Place(ref shape, level[i], clip, ref phase);
            }
            // The officers of the visible chowks (CrowdPresenter.Officers): within 200 m, banded by distance.
            foreach (KeyValuePair<double, double> o in officers)
            {
                double dx = o.Key - ax, dz = o.Value - az, d = Math.Sqrt(dx * dx + dz * dz);
                if (d > 200) continue;
                int band = d <= 15 ? 0 : d <= 40 ? 1 : 2;
                int key = CrowdVariants.Key(PedArchetype.TrafficPolice, StreetStyle.Urban, 0, 0);
                double phase = 0;
                people++;
                if (band <= 1) skinnedBefore++;
                else farBefore.Add(CrowdDrawPlan.FarKey(CrowdVariants.ShapeKey(key), FarFrame.Stand));
                int shape = CrowdVariants.ShapeKey(key);
                plan.Place(ref shape, band, PedClip.Idle, ref phase);
            }
            int before = (skinnedBefore + farBefore.Count) * (budget.Outlines ? 2 : 1);
            if (plan.Draws >= m.Crowd)
            {
                m.Crowd = plan.Draws;
                m.CrowdBefore = Math.Max(m.CrowdBefore, before);
            }
            m.People = Math.Max(m.People, people);
            m.Overflow = Math.Max(m.Overflow, plan.Overflow);
            m.Snapped = Math.Max(m.Snapped, plan.Snapped);
            m.Demoted = Math.Max(m.Demoted, plan.Demoted);
        }
    }
}
