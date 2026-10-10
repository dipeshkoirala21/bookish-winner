using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Fauna;
using Ghumante.Core.Geo;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Where the fauna lives and how much of it is drawn (review of the detail pass): ground birds beyond the caps are
    /// drawn as paper birds so a square's whole flock is there; the tier caps fit the animals-and-birds slice; the
    /// curated square flocks win the flock cap and running flocks are not dropped for marginally nearer ones; curated
    /// groups never sit inside a hero monument and stand on open ground in the sample region; generic groups and the
    /// sims keep off roads and out of houses, ducks swim only on water; vehicles push herds aside; macaques climb onto
    /// walls and sit there; the street clips are widened to graze, trot and scratch.
    /// </summary>
    [TestFixture]
    public class GeneratorsFaunaPlacementTests
    {
        // ------------------------------------------------------------------------------------------------------------
        // Drawing levels and budgets

        [Test]
        public void GroundPigeonsBeyondTheCapsAreAllDrawnOnLow()
        {
            const int n = 150;
            var dist = new float[n];
            var air = new bool[n];
            var sp = new FaunaSpecies[n];
            var level = new int[n];
            var order = new int[n];
            var keys = new float[n];
            for (int i = 0; i < n; i++)
            {
                dist[i] = 1f + 9f * i / n; // all within 10 m of the camera
                sp[i] = FaunaSpecies.Pigeon;
            }
            int budget = FaunaLod.SliceTris(0) - FaunaLod.AnimalCapTris(0);
            int tris = FaunaLod.AssignBirds(dist, air, sp, n, 0, budget, level, order, keys);
            int light = 0, sitting = 0;
            for (int i = 0; i < n; i++)
            {
                Assert.GreaterOrEqual(level[i], 0, "pigeon " + i + " is drawn");
                Assert.AreNotEqual(FaunaLod.Full, level[i], "no full birds on Low");
                if (level[i] == FaunaLod.Light) light++;
                if (level[i] == FaunaLod.PaperGround) sitting++;
            }
            Assert.LessOrEqual(light, 15, "light-bird cap");
            Assert.AreEqual(n, light + sitting, "the rest sit as paper birds");
            Assert.LessOrEqual(tris, budget, "within the birds' share of the slice");
            // Beyond 70 m a bird on the ground is a dot: not drawn; in the air it stays a paper bird to 600 m.
            dist[0] = 100f;
            air[1] = true;
            dist[1] = 300f;
            FaunaLod.AssignBirds(dist, air, sp, n, 0, budget, level, order, keys);
            Assert.AreEqual(FaunaLod.Hidden, level[0]);
            Assert.AreEqual(FaunaLod.Paper, level[1]);
        }

        [Test]
        public void TierCapsFitTheAnimalsAndBirdsSlice()
        {
            for (int tier = 0; tier < 3; tier++)
            {
                Assert.LessOrEqual(FaunaLod.CapTris(tier), FaunaLod.SliceTris(tier), "tier " + tier + ": worst case of every capped level");
                var caps = new int[3];
                FaunaLod.AnimalCaps(tier, caps);
                Assert.AreEqual(caps[0] * FaunaLod.AnimalLod0Tris + caps[1] * FaunaLod.AnimalLod1Tris + caps[2] * FaunaLod.AnimalLod2Tris,
                                FaunaLod.AnimalCapTris(tier), "tier " + tier + ": the cap sum matches the caps");
                // A 200-bird burst as paper birds on top of every animal cap still fits.
                Assert.LessOrEqual(FaunaLod.AnimalCapTris(tier) + 200 * FaunaLod.PaperBirdTris, FaunaLod.SliceTris(tier), "tier " + tier);
            }
            Assert.AreEqual(4000, FaunaLod.SliceTris(0));
            Assert.AreEqual(14000, FaunaLod.SliceTris(1));
            Assert.AreEqual(30000, FaunaLod.SliceTris(2));
            // Every species' level budget is within the design's per-object budget.
            for (int s = 0; s < FaunaCatalog.SpeciesCount; s++)
            {
                FaunaSpeciesInfo info = FaunaCatalog.Info((FaunaSpecies)s);
                if (info.Flies)
                {
                    Assert.LessOrEqual(info.Lod0Tris, FaunaLod.FullBirdTris, info.Species + " full bird");
                    Assert.LessOrEqual(info.Lod1Tris, FaunaLod.LightBirdTris, info.Species + " light bird");
                    Assert.AreEqual(FaunaLod.PaperBirdTris, info.Lod2Tris, info.Species + " paper bird");
                }
                else
                {
                    Assert.LessOrEqual(info.Lod0Tris, FaunaLod.AnimalLod0Tris, info.Species + " LOD0");
                    Assert.LessOrEqual(info.Lod1Tris, FaunaLod.AnimalLod1Tris, info.Species + " LOD1");
                    Assert.LessOrEqual(info.Lod2Tris, FaunaLod.AnimalLod2Tris, info.Species + " LOD2");
                }
            }
        }

        [Test]
        public void ABigFlockNextToCattleStaysWithinTheSlice()
        {
            for (int tier = 0; tier < 3; tier++)
            {
                // A buffalo herd and street cows all round the camera.
                const int na = 60;
                var ad = new float[na];
                var asp = new FaunaSpecies[na];
                var al = new int[na];
                var ao = new int[na];
                var ak = new float[na];
                var caps = new int[3];
                for (int i = 0; i < na; i++)
                {
                    ad[i] = 2f + i * 1.5f;
                    asp[i] = i % 2 == 0 ? FaunaSpecies.Buffalo : FaunaSpecies.Cow;
                }
                int animalBudget = FaunaLod.SliceTris(tier) - FaunaLod.BirdCapTris(tier);
                int animalTris = FaunaLod.AssignAnimals(ad, asp, na, tier, animalBudget, al, ao, ak, caps);
                Assert.LessOrEqual(animalTris, animalBudget, "tier " + tier + " animals");
                int u0 = 0, u1 = 0, u2 = 0;
                for (int i = 0; i < na; i++)
                {
                    if (al[i] == 0) u0++;
                    if (al[i] == 1) u1++;
                    if (al[i] == 2) u2++;
                }
                Assert.LessOrEqual(u0, caps[0]);
                Assert.LessOrEqual(u1, caps[1]);
                Assert.LessOrEqual(u2, caps[2]);
                // And a 180-bird square flock, half of it in the air.
                const int nb = 180;
                var bd = new float[nb];
                var air = new bool[nb];
                var bsp = new FaunaSpecies[nb];
                var bl = new int[nb];
                var bo = new int[nb];
                var bk = new float[nb];
                for (int i = 0; i < nb; i++)
                {
                    bd[i] = 3f + 0.3f * i;
                    air[i] = i % 2 == 1;
                    bsp[i] = FaunaSpecies.Pigeon;
                }
                int birdBudget = Math.Max(FaunaLod.BirdCapTris(tier), FaunaLod.SliceTris(tier) - animalTris);
                int birdTris = FaunaLod.AssignBirds(bd, air, bsp, nb, tier, birdBudget, bl, bo, bk);
                for (int i = 0; i < nb; i++) Assert.GreaterOrEqual(bl[i], 0, "tier " + tier + " bird " + i);
                Assert.LessOrEqual(animalTris + birdTris, FaunaLod.SliceTris(tier), "tier " + tier + " slice");
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Flock caps

        private static AmbientGroup Flock(AmbientKind kind, float d, bool curated, long id)
        {
            return new AmbientGroup { Kind = kind, DistanceM = d, Count = 20, Key = (curated ? 1L << 40 : 3L << 40) | id };
        }

        [Test]
        public void TheCuratedSquareFlockWinsThePigeonCap()
        {
            var g = new[] { Flock(AmbientKind.PigeonFlock, 30f, false, 1), Flock(AmbientKind.PigeonFlock, 60f, true, 2), Flock(AmbientKind.PigeonFlock, 45f, false, 3) };
            var keep = new bool[3];
            var score = new float[3];
            var order = new int[3];
            FaunaLod.SelectGroups(g, 3, new bool[3], 0, keep, score, order);
            Assert.IsTrue(keep[1], "the curated flock runs on Low");
            Assert.IsFalse(keep[0]);
            Assert.IsFalse(keep[2]);
            FaunaLod.SelectGroups(g, 3, new bool[3], 2, keep, score, order);
            Assert.IsTrue(keep[1] && keep[0] && !keep[2], "High: curated plus the nearest generic one");
        }

        [Test]
        public void ARunningFlockIsNotDroppedForAMarginallyNearerOne()
        {
            var g = new[] { Flock(AmbientKind.CrowGroup, 50f, false, 1), Flock(AmbientKind.CrowGroup, 44f, false, 2) };
            var keep = new bool[2];
            var score = new float[2];
            var order = new int[2];
            FaunaLod.SelectGroups(g, 2, new[] { true, false }, 0, keep, score, order);
            Assert.IsTrue(keep[0], "the running flock stays");
            Assert.IsFalse(keep[1]);
            // A much nearer one does take over.
            g[1].DistanceM = 20f;
            FaunaLod.SelectGroups(g, 2, new[] { true, false }, 0, keep, score, order);
            Assert.IsTrue(keep[1]);
            Assert.IsFalse(keep[0]);
            // Just beyond 90 m a running flock is still counted, so it does not flicker in and out.
            g[0].DistanceM = 95f;
            g[1].DistanceM = 80f;
            FaunaLod.SelectGroups(g, 2, new[] { true, false }, 0, keep, score, order);
            Assert.AreEqual(1, (keep[0] ? 1 : 0) + (keep[1] ? 1 : 0));
            // Kites and herds are never capped.
            var h = new[] { Flock(AmbientKind.KiteGroup, 10f, false, 1), Flock(AmbientKind.KiteGroup, 12f, false, 2), Flock(AmbientKind.GoatHerd, 5f, false, 3) };
            FaunaLod.SelectGroups(h, 3, null, 0, new bool[3], new float[3], new int[3]);
        }

        [Test]
        public void GenericPigeonFlocksKeepAwayFromTheCuratedSquares()
        {
            var p = new AmbientFaunaPlanner(11u);
            var dst = new AmbientGroup[96];
            var core = new FakeHabitat { Area = AreaType.OldCore };
            int bas = Array.FindIndex(AmbientFaunaPlanner.Sites, s => s.Name == "Basantapur");
            p.GroupPosition(bas, 0, out double bx, out double bz);
            int generic = 0;
            for (int t = 0; t < 6; t++)
            {
                int n = p.Plan(bx + 40.0 * t, bz - 25.0 * t, 260f, 10, 10f, core, dst);
                for (int i = 0; i < n; i++)
                {
                    if (dst[i].Kind != AmbientKind.PigeonFlock || dst[i].IsCurated) continue;
                    generic++;
                    Assert.IsFalse(p.NearCuratedPigeons(dst[i].X, dst[i].Z, AmbientFaunaPlanner.CuratedPigeonExclusionM), "generic flock by a square");
                }
            }
            Assert.Greater(generic, 0, "the old core still has roof and courtyard flocks further away");
        }

        // ------------------------------------------------------------------------------------------------------------
        // Curated sites

        [Test]
        public void NoCuratedGroupSitsInsideAHeroMonument()
        {
            var p = new AmbientFaunaPlanner(5u);
            int checkedGroups = 0;
            for (int s = 0; s < AmbientFaunaPlanner.Sites.Length; s++)
            {
                FaunaSite site = AmbientFaunaPlanner.Sites[s];
                for (int g = 0; g < site.Groups; g++)
                {
                    p.GroupPosition(s, g, out double x, out double z);
                    int zone = FaunaHeroZones.ZoneAt(x, z, 1.0);
                    Assert.AreEqual(-1, zone, site.Name + " group " + g + " inside " + (zone >= 0 ? FaunaHeroZones.NameOf(zone) : ""));
                    checkedGroups++;
                }
            }
            Assert.Greater(checkedGroups, 12);
            // The Boudha flock is on the kora plaza, outside the prayer-wheel wall; Swayambhu's troops are off the dome.
            int boudha = Array.FindIndex(AmbientFaunaPlanner.Sites, s => s.Name == "Boudha");
            p.GroupPosition(boudha, 0, out double bx, out double bz);
            WorldFrame.LonLatToGame(85.362004, 27.721436, out double sx, out double sz);
            Assert.Greater(Math.Sqrt((bx - sx) * (bx - sx) + (bz - sz) * (bz - sz)), 48.0 + 2.0);
            int sway = Array.FindIndex(AmbientFaunaPlanner.Sites, s => s.Name == "Swayambhu");
            WorldFrame.LonLatToGame(85.290391, 27.714931, out double wx, out double wz);
            for (int g = 0; g < AmbientFaunaPlanner.Sites[sway].Groups; g++)
            {
                p.GroupPosition(sway, g, out double tx, out double tz);
                Assert.Greater(Math.Sqrt((tx - wx) * (tx - wx) + (tz - wz) * (tz - wz)), 13.35 + 5.0, "troop " + g + " off the dome");
            }
        }

        [Test]
        public void NoCuratedAnimalEverStandsInsideAHero()
        {
            // Only the monuments are known (no tiles): the sims must still keep every animal out of them.
            var index = new FaunaGroundIndex();
            var habitat = new IndexHabitat { Index = index };
            var p = new AmbientFaunaPlanner(9u);
            for (int s = 0; s < AmbientFaunaPlanner.Sites.Length; s++)
            {
                FaunaSite site = AmbientFaunaPlanner.Sites[s];
                for (int g = 0; g < site.Groups; g++)
                {
                    p.GroupPosition(s, g, out double x, out double z);
                    uint seed = FaunaRng.Hash((uint)s, (uint)g);
                    if (site.Kind == AmbientKind.PigeonFlock || site.Kind == AmbientKind.CrowGroup)
                    {
                        var f = new FlockSim(AmbientFaunaPlanner.FlockOf(site.Kind), site.CountMax, x, z, 0f, seed, site.SpreadM, habitat);
                        for (int k = 0; k < 600; k++)
                        {
                            f.Step(0.05f, 500f, 500f, false);
                            if (k % 60 != 0) continue;
                            for (int i = 0; i < f.Count; i++)
                                if (f.Y[i] < 0.01f)
                                    Assert.IsFalse(FaunaHeroZones.Inside(x + f.X[i], z + f.Z[i]), site.Name + " bird " + i);
                        }
                    }
                    else
                    {
                        var h = new HerdSim(AmbientFaunaPlanner.SpeciesOf(site.Kind), site.CountMax, x, z, 0f, site.SpreadM, seed, habitat);
                        for (int k = 0; k < 600; k++)
                        {
                            h.Step(0.1f, 500f, 500f, false);
                            if (k % 30 != 0) continue;
                            for (int i = 0; i < h.Count; i++)
                                Assert.IsFalse(FaunaHeroZones.Inside(x + h.X[i], z + h.Z[i]), site.Name + " animal " + i + " at step " + k);
                        }
                    }
                }
            }
        }

        [Test]
        public void CuratedGroupsStandOnOpenGroundInTheSampleRegion()
        {
            var index = new FaunaGroundIndex();
            foreach (TileId t in StreamingSampleRegion.TilesAt(10)) index.Add(StreamingSampleRegion.Tile(t));
            var habitat = new IndexHabitat { Index = index };
            var p = new AmbientFaunaPlanner(3u);
            int covered = 0;
            for (int s = 0; s < AmbientFaunaPlanner.Sites.Length; s++)
            {
                FaunaSite site = AmbientFaunaPlanner.Sites[s];
                for (int g = 0; g < site.Groups; g++)
                {
                    p.GroupPosition(s, g, out double x, out double z);
                    FaunaGround c = index.At(x, z);
                    if ((c & FaunaGround.Loaded) == 0) continue; // Patan, Bhaktapur, Gokarna: outside the sample
                    covered++;
                    Assert.IsTrue(AmbientFaunaPlanner.Suits(site.Kind, c), site.Name + " group " + g + " centre: " + c);
                    int ok = 0, all = 0;
                    for (double dx = -site.SpreadM; dx <= site.SpreadM; dx += 1.0)
                        for (double dz = -site.SpreadM; dz <= site.SpreadM; dz += 1.0)
                        {
                            if (dx * dx + dz * dz > site.SpreadM * site.SpreadM) continue;
                            all++;
                            if (AmbientFaunaPlanner.Suits(site.Kind, index.At(x + dx, z + dz))) ok++;
                        }
                    Assert.Greater(ok, 0.85 * all, site.Name + " group " + g + ": most of the disc is open ground");
                    // The sims keep every animal on that open ground.
                    uint seed = FaunaRng.Hash((uint)s, (uint)g, 77u);
                    if (site.Kind == AmbientKind.MacaqueTroop)
                    {
                        var h = new HerdSim(FaunaSpecies.Macaque, site.CountMax, x, z, 0f, site.SpreadM, seed, habitat);
                        for (int k = 0; k < 400; k++)
                        {
                            h.Step(0.1f, 500f, 500f, false);
                            if (k % 20 != 0) continue;
                            for (int i = 0; i < h.Count; i++)
                                Assert.AreEqual(FaunaGround.None, index.At(x + h.X[i], z + h.Z[i]) & (FaunaGround.Building | FaunaGround.Road), site.Name + " macaque " + i);
                        }
                    }
                    else
                    {
                        var f = new FlockSim(AmbientFaunaPlanner.FlockOf(site.Kind), site.CountMax, x, z, 0f, seed, site.SpreadM, habitat);
                        for (int i = 0; i < f.Count; i++)
                            Assert.AreEqual(FaunaGround.None, index.At(x + f.X[i], z + f.Z[i]) & (FaunaGround.Building | FaunaGround.Road), site.Name + " bird " + i);
                    }
                }
            }
            Assert.GreaterOrEqual(covered, 9, "Swayambhu, Pashupati, Basantapur, Boudha and Tundikhel are in the sample region");
        }

        // ------------------------------------------------------------------------------------------------------------
        // Generic groups and the sims on a fake habitat

        /// <summary>Stripes along x: carriageway, houses, open yard, a pond and a paddy field, every 50 m.</summary>
        private sealed class FakeHabitat : IFaunaHabitat
        {
            public AreaType Area = AreaType.PeriUrban;

            public AreaType AreaAt(double x, double z)
            {
                return Area;
            }

            public FaunaGround GroundAt(double x, double z)
            {
                double u = x - 50.0 * Math.Floor(x / 50.0);
                if (u < 8.0) return FaunaGround.Loaded | FaunaGround.Road;
                if (u < 18.0) return FaunaGround.Loaded | FaunaGround.Building;
                if (u < 28.0) return FaunaGround.Loaded | FaunaGround.Square;
                if (u < 34.0) return FaunaGround.Loaded | FaunaGround.Water;
                return FaunaGround.Loaded | FaunaGround.Field;
            }
        }

        private sealed class IndexHabitat : IFaunaHabitat
        {
            public FaunaGroundIndex Index;

            public AreaType AreaAt(double x, double z)
            {
                return AreaType.Urban;
            }

            public FaunaGround GroundAt(double x, double z)
            {
                return Index.At(x, z);
            }
        }

        [Test]
        public void GenericGroupsKeepOffRoadsAndOutOfHousesAndDucksFindWater()
        {
            var habitat = new FakeHabitat();
            var p = new AmbientFaunaPlanner(21u);
            var dst = new AmbientGroup[96];
            int ducks = 0, egrets = 0, herds = 0;
            for (int month = 1; month <= 12; month += 5)
                foreach (AreaType area in new[] { AreaType.PeriUrban, AreaType.Rural, AreaType.OldCore, AreaType.Urban })
                {
                    habitat.Area = area;
                    int n = p.Plan(600000.0, 3100000.0, 400f, month, 9f, habitat, dst);
                    for (int i = 0; i < n; i++)
                    {
                        AmbientGroup g = dst[i];
                        if (g.IsCurated || g.Kind == AmbientKind.KiteGroup) continue;
                        FaunaGround c = habitat.GroundAt(g.X, g.Z);
                        Assert.AreEqual(FaunaGround.None, c & (FaunaGround.Road | FaunaGround.Building), g.Kind + " centre on " + c);
                        if (g.Kind == AmbientKind.DuckPond)
                        {
                            ducks++;
                            Assert.AreNotEqual(FaunaGround.None, c & FaunaGround.Water, "ducks at a pond");
                        }
                        if (g.Kind == AmbientKind.EgretGroup)
                        {
                            egrets++;
                            Assert.AreNotEqual(FaunaGround.None, c & (FaunaGround.Field | FaunaGround.Water), "egrets in the fields");
                        }
                        if (!g.IsFlock)
                        {
                            herds++;
                            var h = new HerdSim(AmbientFaunaPlanner.SpeciesOf(g.Kind), g.Count, g.X, g.Z, 0f, g.RadiusM, g.Seed, habitat);
                            for (int k = 0; k < 300; k++)
                            {
                                h.Step(0.1f, 1000f, 1000f, false);
                                for (int a = 0; a < h.Count; a++)
                                {
                                    FaunaGround at = habitat.GroundAt(g.X + h.X[a], g.Z + h.Z[a]);
                                    Assert.AreEqual(FaunaGround.None, at & (FaunaGround.Road | FaunaGround.Building), g.Kind + " animal " + a + " on " + at);
                                    if (h.Clip[a] == FaunaClip.Swim) Assert.IsTrue(h.OnWater(h.X[a], h.Z[a]), "a duck swims only on water");
                                }
                            }
                        }
                    }
                }
            Assert.Greater(herds, 3);
            Assert.Greater(ducks + egrets, 0);
            // Without water in reach a duck never swims.
            var dry = new HerdSim(FaunaSpecies.Duck, 8, 0.0, 0.0, 0f, 5f, 4u);
            for (int k = 0; k < 600; k++)
            {
                dry.Step(0.1f, 100f, 100f, false);
                for (int a = 0; a < dry.Count; a++) Assert.AreNotEqual(FaunaClip.Swim, dry.Clip[a]);
            }
        }

        [Test]
        public void PassingVehiclesPushHerdsAside()
        {
            var h = new HerdSim(FaunaSpecies.Goat, 8, 0.0, 0.0, 0f, 6f, 13u);
            for (int k = 0; k < 20; k++) h.Step(0.1f, 100f, 100f, false);
            // A vehicle drives through the middle of the herd.
            for (int k = 0; k < 15; k++) h.Step(0.1f, 100f, 100f, false, h.X[0], h.Z[0] - 1.5f + 0.2f * k, true);
            float vx = h.X[0], vz = h.Z[0] + 1.5f;
            Assert.AreEqual(FaunaClip.Trot, h.Clip[0], "the goat trots out of the way");
            for (int k = 0; k < 15; k++) h.Step(0.1f, 100f, 100f, false, vx, vz, true);
            for (int a = 0; a < h.Count; a++)
            {
                float d = (float)Math.Sqrt((h.X[a] - vx) * (h.X[a] - vx) + (h.Z[a] - vz) * (h.Z[a] - vz));
                Assert.Greater(d, 1.2f, "goat " + a + " clear of the vehicle");
            }
        }

        [Test]
        public void MacaquesClimbOntoWallsAndSitThere()
        {
            var h = new HerdSim(FaunaSpecies.Macaque, 16, 0.0, 0.0, 0f, 8f, 31u);
            h.SetPerches(new[] { 3f, -4f }, new[] { 2f, -3f }, new[] { 0f, 0f }, new[] { 1.6f, 2.4f }, 2);
            Assert.AreEqual(2, h.PerchCount);
            bool climbed = false, sat = false;
            for (int k = 0; k < 2400; k++)
            {
                h.Step(0.1f, 100f, 100f, false);
                for (int i = 0; i < h.Count; i++)
                {
                    if (h.Clip[i] == FaunaClip.Climb && h.Elevated[i]) climbed = true;
                    if (h.Elevated[i] && (h.Clip[i] == FaunaClip.Sit || h.Clip[i] == FaunaClip.Groom) && Math.Abs(h.Y[i] - 1.6f) < 0.01f || Math.Abs(h.Y[i] - 2.4f) < 0.01f)
                        sat = true;
                    if (h.Elevated[i]) Assert.That(h.Y[i], Is.InRange(-0.01f, 2.41f));
                }
            }
            Assert.IsTrue(climbed, "macaques climb");
            Assert.IsTrue(sat, "and sit on top");
            // Without perches they never leave the ground.
            var g = new HerdSim(FaunaSpecies.Goat, 8, 0.0, 0.0, 0f, 8f, 31u);
            g.SetPerches(new[] { 3f }, new[] { 2f }, new[] { 0f }, new[] { 1.6f }, 1);
            Assert.AreEqual(0, g.PerchCount, "only macaques climb");
        }

        [Test]
        public void StreetClipsGrazeTrotAndScratch()
        {
            int graze = 0;
            for (int id = 0; id < 400; id++)
            {
                FaunaClip c = FaunaAnimator.StreetClip(FaunaSpecies.Cow, 1, 0f, id, 10f);
                Assert.That(c, Is.EqualTo(FaunaClip.Graze).Or.EqualTo(FaunaClip.Stand));
                if (c == FaunaClip.Graze) graze++;
                Assert.AreEqual(c, FaunaAnimator.StreetClip(FaunaSpecies.Cow, 1, 0f, id, 500f), "stable per animal");
                Assert.AreEqual(FaunaClip.Lie, FaunaAnimator.StreetClip(FaunaSpecies.Bull, 0, 0f, id, 10f));
            }
            Assert.That(graze, Is.InRange(240, 320), "about 70% of the standing cows graze");
            Assert.AreEqual(FaunaClip.Trot, FaunaAnimator.StreetClip(FaunaSpecies.Dog, 2, 1.8f, 7, 0f));
            Assert.AreEqual(FaunaClip.Walk, FaunaAnimator.StreetClip(FaunaSpecies.Dog, 2, 0.9f, 7, 0f));
            int scratch = 0;
            for (float t = 0f; t < 220f; t += 0.1f)
                if (FaunaAnimator.StreetClip(FaunaSpecies.Dog, 4, 0f, 7, t) == FaunaClip.Scratch)
                    scratch++;
            Assert.That(scratch * 0.1f, Is.InRange(18f, 30f), "a sitting dog scratches about 2.4 s in every 22 s");
        }
    }
}
