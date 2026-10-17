using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;
using Ghumante.Core.Routing;
using Ghumante.Core.Search;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Gameplay integration of the W2 detail pass (package integ-play): the player's face (<see cref="FaceMood"/>), one
    /// vehicle shape per seed for traffic, parked and borrowed vehicles (<see cref="VehicleSeeds"/>), sadhus spawning at
    /// Pashupati (<see cref="PedestrianSim.ArchetypeFor"/>), and the route planner's car profile on the rebuilt GHRG car
    /// mask of the committed sample region (Thamel to Boudha).
    /// </summary>
    public class ExplorePlayIntegrationTests
    {
        // -------------------------------------------------------------------------------------------------------------
        // The player's face

        [Test]
        public void FaceRestsCalmInSacredZonesAndSmilesElsewhere()
        {
            var mood = new FaceMood();
            Assert.That(mood.Update(0.1f, false), Is.EqualTo(FaceExpression.Smile));
            Assert.That(mood.Update(0.1f, true), Is.EqualTo(FaceExpression.Calm));
            Assert.That(mood.Update(0.1f, false), Is.EqualTo(FaceExpression.Smile));
        }

        [Test]
        public void DiscoveriesAndArrivalsLightTheFaceUpForTheirTime()
        {
            var mood = new FaceMood();
            mood.Cue(MoodCue.Discovery, false);
            Assert.That(mood.Current, Is.EqualTo(FaceExpression.Joy));
            float t = 0f;
            while (mood.Update(0.1f, false) == FaceExpression.Joy && t < 10f) t += 0.1f;
            Assert.That(t, Is.EqualTo(FaceMood.HoldS(MoodCue.Discovery)).Within(0.15f));
            Assert.That(mood.Current, Is.EqualTo(FaceExpression.Smile));

            // An arrival inside a temple: joy first, then calm again.
            mood.Cue(MoodCue.Arrival, true);
            Assert.That(mood.Update(1f, true), Is.EqualTo(FaceExpression.Joy));
            Assert.That(mood.Update(FaceMood.HoldS(MoodCue.Arrival), true), Is.EqualTo(FaceExpression.Calm));
        }

        [Test]
        public void AWeakerCueNeverCutsAStrongerOneShortAndNoLaughingInTemples()
        {
            var mood = new FaceMood();
            mood.Cue(MoodCue.Arrival, false);
            mood.Update(0.5f, false);
            mood.Cue(MoodCue.Bump, false);
            Assert.That(mood.Update(0.1f, false), Is.EqualTo(FaceExpression.Joy), "the bump does not end the arrival's joy");

            mood.Reset();
            mood.Cue(MoodCue.Bump, false);
            Assert.That(mood.Update(0.1f, false), Is.EqualTo(FaceExpression.WinceLaugh));
            mood.Cue(MoodCue.Discovery, false);
            Assert.That(mood.Update(0.1f, false), Is.EqualTo(FaceExpression.Joy), "a stronger cue replaces a weaker one");

            mood.Reset();
            mood.Cue(MoodCue.Bump, true);
            Assert.That(mood.Update(0.1f, true), Is.EqualTo(FaceExpression.Calm), "a bump inside a sacred zone stays calm");
        }

        // -------------------------------------------------------------------------------------------------------------
        // One shape per vehicle

        [Test]
        public void ParkedAndBorrowedFleetVehiclesShareTheirModelType()
        {
            int differs = 0;
            for (int variant = 0; variant < VehicleCatalog.Count; variant++)
            {
                for (uint id = 1; id <= 64; id++)
                {
                    uint seed = VehicleSeeds.Fleet(id);
                    Assert.That(seed, Is.EqualTo(CharMath.Hash(id, VehicleSeeds.FleetSalt)), "the formula traffic and the explorer used before");
                    byte parked = VehicleSeeds.FleetModel(variant, id);
                    byte borrowed = VehicleSeeds.Model(variant, seed); // DrivenVehicleView: the model type of its plate seed
                    Assert.That(borrowed, Is.EqualTo(parked), "variant " + variant + " id " + id);
                    Assert.That(parked, Is.LessThan(VehicleMesher.ModelCount(variant)));
                    if (VehicleSeeds.Model(variant, id) != parked) differs++;
                }
            }
            Assert.That(differs, Is.GreaterThan(0), "the fleet seed is not the bare id (it is salted)");
        }

        [Test]
        public void TheDrivenModelTypeKeepsItsOwnWheels()
        {
            // The player's vehicle draws the wheels of its own model type, as traffic does (car model types differ in
            // track and wheel look).
            bool anyDiffer = false;
            for (int variant = 0; variant < VehicleCatalog.Count; variant++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(variant);
                WheelSocket[] first = VehicleMesher.Wheels(e, 0);
                for (int m = 0; m < VehicleMesher.ModelCount(variant); m++)
                {
                    WheelSocket[] own = VehicleMesher.Wheels(e, m);
                    Assert.That(own.Length, Is.GreaterThanOrEqualTo(2), e.AssetId);
                    if (own.Length != first.Length || own.Where((w, i) => w.X != first[i].X || w.Style != first[i].Style).Any()) anyDiffer = true;
                }
            }
            Assert.That(anyDiffer, Is.True, "some model types differ in track or wheel style from the first");
        }

        // -------------------------------------------------------------------------------------------------------------
        // The chase camera on the open world's obstacle query

        /// <summary>The tiles of the committed sample whose area holds (x, z), every level (what the streamer keeps
        /// resident round the player), as one fresh query: the region opened (again).</summary>
        private static TileGroundQuery OpenAround(double x, double z, out List<TileId> areas)
        {
            var g = new TileGroundQuery();
            areas = new List<TileId>();
            foreach (KeyValuePair<TileId, TileData> kv in DrivingData.SampleTiles())
            {
                if (!kv.Key.Equals(TileId.At(kv.Key.Level, x, z))) continue;
                g.Add(kv.Value);
                areas.Add(kv.Key);
            }
            return g;
        }

        /// <summary>The camera position of a boom solved from scratch (as after a spawn or teleport) at a pivot given in
        /// scene metres plus the floating origin, the way ChaseCameraRig turns its scene pivot into game metres.</summary>
        private static void CameraAt(IViewObstacleQuery q, float sx, float sy, float sz, double ox, double oy, double oz, float yaw,
                                     out double cx, out double cy, out double cz, out bool blocked)
        {
            var boom = new ChaseBoom();
            boom.Snap();
            boom.Solve(q, sx + ox, sy + oy, sz + oz, yaw, 12f, 6f, 2.5f, 0f);
            double dx, dy, dz;
            boom.CameraOffset(yaw, out dx, out dy, out dz);
            cx = sx + ox + dx;
            cy = sy + oy + dy;
            cz = sz + oz + dz;
            blocked = boom.Blocked;
        }

        [Test]
        public void TheChaseBoomStopsShortOfThamelHousesAcrossRebasesAndAReopenedRegion()
        {
            // ExploreSession hands the rig WorldRoot.GroundQuery every frame (ViewObstacles): a TileGroundQuery over the
            // streamed tiles' solids (BLDG footprints, structure colliders, decks). Here: that query over the committed
            // sample's tiles round Thamel Marg, the boom swept all the way round a scooter in the lane.
            DrivingData.ThamelMarg(out double tx, out double tz);
            TileGroundQuery world = OpenAround(tx, tz, out List<TileId> areas);
            Assert.That(areas.Count, Is.GreaterThan(0));
            Assert.That(world.TrySample(tx, tz, out GroundSample at), Is.True);
            float pivotY = at.Height + 1.5f;
            Assert.That(world.InsideSolid(tx, tz, at.Height + 0.2f), Is.False, "the pivot stands in the lane");

            // The origin by the player, then two floating origins 7 km and 14 km away (the scene pivot is a float relative
            // to the origin, as in Unity).
            double[][] origins = { new[] { tx - 3.0, 0.0, tz + 2.0 }, new[] { tx - 5000.0, at.Height - 40.0, tz + 5000.0 }, new[] { tx + 12000.5, 0.0, tz - 7000.25 } };
            int blockedYaws = 0, yaws = 0;
            for (float yaw = 0f; yaw < 360f; yaw += 10f)
            {
                yaws++;
                double refX = 0, refY = 0, refZ = 0;
                for (int o = 0; o < origins.Length; o++)
                {
                    double ox = origins[o][0], oy = origins[o][1], oz = origins[o][2];
                    float sx = (float)(tx - ox), sy = (float)(pivotY - oy), sz = (float)(tz - oz);
                    CameraAt(world, sx, sy, sz, ox, oy, oz, yaw, out double cx, out double cy, out double cz, out bool blocked);
                    if (o == 0)
                    {
                        refX = cx;
                        refY = cy;
                        refZ = cz;
                        if (blocked) blockedYaws++;
                        // Never in a house, and the boom from the pivot never passes through one.
                        Assert.That(world.InsideSolid(cx, cz, (float)cy), Is.False, "camera inside a house at yaw " + yaw);
                        double px = tx, py = pivotY, pz = tz;
                        double len = Math.Sqrt((cx - px) * (cx - px) + (cy - py) * (cy - py) + (cz - pz) * (cz - pz));
                        for (double d = 0.2; d < len; d += 0.2)
                        {
                            double f = d / len;
                            Assert.That(world.InsideSolid(px + (cx - px) * f, pz + (cz - pz) * f, (float)(py + (cy - py) * f)), Is.False,
                                "boom through a house at yaw " + yaw + ", " + d.ToString("0.0") + " m out");
                        }
                    }
                    else
                    {
                        // Same camera after a rebase (to float precision of the scene pivot).
                        Assert.That(cx, Is.EqualTo(refX).Within(0.03), "x after rebase " + o + " at yaw " + yaw);
                        Assert.That(cy, Is.EqualTo(refY).Within(0.03), "y after rebase " + o + " at yaw " + yaw);
                        Assert.That(cz, Is.EqualTo(refZ).Within(0.03), "z after rebase " + o + " at yaw " + yaw);
                    }
                }
            }
            TestContext.WriteLine(blockedYaws + " of " + yaws + " boom yaws pulled in by Thamel houses");
            Assert.That(blockedYaws, Is.GreaterThanOrEqualTo(4), "the lane's houses pull the boom in sideways");
            Assert.That(blockedYaws, Is.LessThan(yaws), "along the lane the boom has room");

            // Closing the region drops the solids (nothing to hit); reopening it builds a new query that the rig picks up
            // on the next frame (ViewObstacles is read every frame), with the same houses again.
            foreach (TileId a in areas) world.Remove(a);
            int stillBlocked = 0;
            for (float yaw = 0f; yaw < 360f; yaw += 10f)
            {
                CameraAt(world, 3f, pivotY, -2f, tx - 3.0, 0, tz + 2.0, yaw, out _, out _, out _, out bool b);
                if (b) stillBlocked++;
            }
            Assert.That(stillBlocked, Is.EqualTo(0), "a closed region has no solids");
            TileGroundQuery reopened = OpenAround(tx, tz, out _);
            int again = 0;
            for (float yaw = 0f; yaw < 360f; yaw += 10f)
            {
                CameraAt(reopened, 3f, pivotY, -2f, tx - 3.0, 0, tz + 2.0, yaw, out double cx, out double cy, out double cz, out bool b);
                if (b) again++;
                Assert.That(reopened.InsideSolid(cx, cz, (float)cy), Is.False);
            }
            Assert.That(again, Is.EqualTo(blockedYaws), "the reopened region's houses stop the boom again");
        }

        // -------------------------------------------------------------------------------------------------------------
        // Traffic spawns: the moving-vehicle cap and the gap across lane joins

        [Test]
        public void RoutedBusesShareTheMovingVehicleCap()
        {
            // Bus routes run on top of the ambient traffic at Ratna Park at the morning peak: the ambient spawner leaves
            // the slots of the departures the runner still has to place, and a routed spawn is refused at the cap, so the
            // moving vehicles never exceed TrafficSettings.MaxVehicles (they reached about 1.33 × before).
            LaneGraph g = TrafficData.Graph;
            TrafficData.Game(new[] { 85.3153, 27.7066 }, out double x, out double z);
            var sim = new TrafficSim(g, TrafficSettings.Mid(), 21) { Sacred = TrafficData.Zones };
            var bus = new BusRouteRunner(TrafficData.Routes, g, sim);
            sim.SetFocus(x, z, 0f);
            int most = 0, routedMost = 0;
            const float dt = 0.2f;
            for (int i = 0; i < (int)(12 * 60 / dt); i++)
            {
                float hour = 8.5f + i * dt / 3600f;
                bus.Step(dt, hour);
                sim.Step(dt, hour, 0f);
                int n = sim.AgentCount;
                most = Math.Max(most, n);
                routedMost = Math.Max(routedMost, bus.RoutedAlive);
                Assert.That(n, Is.LessThanOrEqualTo(sim.Settings.MaxVehicles), "moving vehicles at step " + i);
            }
            TestContext.WriteLine("most " + most + " moving of " + sim.Settings.MaxVehicles + ", " + routedMost + " routed at most");
            Assert.That(routedMost, Is.GreaterThanOrEqualTo(2), "buses still run their routes");
            Assert.That(most, Is.GreaterThanOrEqualTo(sim.Settings.MaxVehicles * 8 / 10), "the peak still fills up");
        }

        [Test]
        public void NoSpawnRightBehindOrInFrontOfAVehicleOnTheNextOrPreviousLane()
        {
            // A vehicle that just moved onto the next lane (a junction connector) still blocks a spawn at the end of the
            // lane behind it, and one about to leave a lane blocks a spawn just inside the next one (TrafficSim.Room
            // looks along successors and predecessors; the contact traced at Chabahil on the Ring Road).
            LaneGraph g = TrafficData.Graph;
            VehicleCatalogEntry hatch = VehicleCatalog.At(VehicleCatalog.Hatchback);
            float len = hatch.LengthM;
            uint bit = VehicleClasses.Bit(hatch.TrafficClass);
            int pairs = 0;
            foreach (LaneId l in g.AllLanes())
            {
                LaneInfo a = g.Info(l);
                if (a.Kind != LaneKind.Road || a.LengthM < 40f || (a.ClassMask & bit) == 0 || a.SuccessorCount != 1) continue;
                LaneId n = g.Successor(l, 0);
                LaneInfo b = g.Info(n);
                if (b.LengthM < len + 6f || (b.ClassMask & bit) == 0 || b.PredecessorCount != 1) continue;
                double gx = a.X[a.PointCount - 1] - b.X[0], gz = a.Z[a.PointCount - 1] - b.Z[0];
                if (gx * gx + gz * gz > 0.25) continue; // the lanes join end to start

                // Ahead on the next lane, its tail 3 m past the join: no spawn with its nose at the join.
                var sim = new TrafficSim(g, TrafficSettings.Mid(), 9);
                if (!sim.TrySpawnOnLane(n, len + 3f, VehicleCatalog.Hatchback, 0, out _)) continue;
                Assert.That(sim.TrySpawnOnLane(l, a.LengthM, VehicleCatalog.Hatchback, 0, out _), Is.False,
                    "3 m behind a vehicle on the next lane (way " + a.WayId + ")");
                Assert.That(sim.TrySpawnOnLane(l, a.LengthM - 15f, VehicleCatalog.Hatchback, 0, out _), Is.True,
                    "well behind it there is room (way " + a.WayId + ")");

                // About to leave this lane, its nose 1 m before the join: no spawn with its tail 2 m into the next lane.
                var sim2 = new TrafficSim(g, TrafficSettings.Mid(), 9);
                Assert.That(sim2.TrySpawnOnLane(l, a.LengthM - 1f, VehicleCatalog.Hatchback, 0, out _), Is.True);
                Assert.That(sim2.TrySpawnOnLane(n, len + 2f, VehicleCatalog.Hatchback, 0, out _), Is.False,
                    "3 m in front of a vehicle on the previous lane (way " + a.WayId + ")");
                if (++pairs >= 25) break;
            }
            TestContext.WriteLine(pairs + " lane joins checked");
            Assert.That(pairs, Is.GreaterThanOrEqualTo(5));
        }

        /// <summary>The player's vehicle as DrivenVehicleView assembles it (body of its model type, its own wheel
        /// sockets, left wheels mirrored across their pivot) beside the old unmirrored assembly, and the Pashupati crowd as
        /// drawn: preview only (set GHUMANTE_PREVIEW_DIR).</summary>
        [Test]
        public void DumpPlayerVehicleWheelsAndPashupatiCrowd()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the previews");
            var wheel = new MeshData();
            foreach (int v in new[] { VehicleCatalog.TruckPainted, VehicleCatalog.Tipper, VehicleCatalog.BusCityGreen, VehicleCatalog.Tractor })
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                byte model = VehicleSeeds.Model(v, 1234u);
                foreach (bool mirrored in new[] { false, true })
                {
                    var m = new MeshData { HasUv0 = true };
                    VehicleMesher.Build(v, 0, model, VehicleLod.Lod0, m, 1234u);
                    foreach (WheelSocket w in VehicleMesher.Wheels(e, model))
                    {
                        wheel.Clear();
                        VehicleMesher.BuildWheel(in w, VehicleLod.Lod0, wheel);
                        Affine3 at = Affine3.Translation(w.X, w.Y, w.Z);
                        if (mirrored && w.X < -0.01f) at = at * Affine3.Scaling(-1, 1, 1);
                        Shapes.CopyTransformed(m, wheel, at);
                    }
                    ObjDump.Write(m, "integ-play/wheels_" + e.AssetId + (mirrored ? "_mirrored" : "_before") + ".obj");
                }
            }
            // Pashupati: the drawn crowd for the first spawns of the sim's Pashupati row.
            WorldFrame.LonLatToGame(85.3488, 27.7105, out double px, out double pz);
            var crowd = new MeshData(65536, 196608) { HasUv0 = true };
            var body = new MeshData(16384, 49152) { HasUv0 = true };
            var weights = new SkinWeights(16384);
            int placed = 0;
            for (int i = 0; i < 400 && placed < 40; i += 10)
            {
                PedArchetype a = PedestrianSim.ArchetypeFor(px + 25, pz + 15, AreaType.OldCore, false, (i + 0.5f) / 400f);
                CharacterRecipe r = CrowdVariants.Recipe(CrowdVariants.KeyOf(a, StreetStyle.Pashupati, 0, i * 7919 + 13, i & 15));
                body.Clear();
                weights.Clear();
                HumanoidMesher.Build(r, 1, body, weights, HeadwearMode.Outfit); // garment colours baked in (no tint mask)
                ObjDump.Append(crowd, body, (placed % 10 - 4.5f) * 0.8f, 0f, (placed / 10) * 1.4f);
                placed++;
            }
            ObjDump.Write(crowd, "integ-play/pashupati_crowd.obj");
        }

        // -------------------------------------------------------------------------------------------------------------
        // Sadhus at Pashupati

        [Test]
        public void SadhusSpawnAtPashupatiAndNowhereElse()
        {
            WorldFrame.LonLatToGame(85.3488, 27.7105, out double px, out double pz); // Pashupatinath
            WorldFrame.LonLatToGame(85.3095, 27.7065, out double ax, out double az); // Asan
            WorldFrame.LonLatToGame(85.3620, 27.7215, out double bx, out double bz); // Boudha
            int n = 2000, atPashupati = 0, elsewhere = 0, koraSadhus = 0;
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n;
                if (PedestrianSim.ArchetypeFor(px + 40, pz - 30, AreaType.OldCore, false, u) == PedArchetype.Sadhu) atPashupati++;
                if (PedestrianSim.ArchetypeFor(ax, az, AreaType.OldCore, false, u) == PedArchetype.Sadhu) elsewhere++;
                if (PedestrianSim.ArchetypeFor(ax, az, AreaType.Urban, false, u) == PedArchetype.Sadhu) elsewhere++;
                if (PedestrianSim.ArchetypeFor(bx, bz, AreaType.OldCore, true, u) == PedArchetype.Sadhu) koraSadhus++;
            }
            TestContext.WriteLine(atPashupati + " sadhus of " + n + " spawns at Pashupati");
            Assert.That(atPashupati, Is.InRange(n * 4 / 100, n * 10 / 100), "about 6 in 91 near the temple");
            Assert.That(elsewhere, Is.EqualTo(0));
            Assert.That(koraSadhus, Is.EqualTo(0), "the Boudha kora walkers are monks and pilgrims");
            // Pilgrims still outnumber them: saris and daura suruwal.
            int pilgrims = Enumerable.Range(0, n).Count(i =>
            {
                PedArchetype a = PedestrianSim.ArchetypeFor(px, pz, AreaType.Urban, false, (i + 0.5f) / n);
                return a == PedArchetype.KurtaSari || a == PedArchetype.DauraSuruwal;
            });
            Assert.That(pilgrims, Is.GreaterThan(atPashupati * 4));
        }

        [Test]
        public void TheDrawnCrowdAtPashupatiHasTheResearchedSadhuShare()
        {
            // What the crowd presenter draws: the sim's archetype, then the look of that archetype at Pashupati
            // (CrowdVariants.KeyOf + Recipe, as CrowdPresenter.BodyKey does). The sim row has about 6 sadhus in 91
            // (W2_DESIGN 10.3); the recipe must not dress any more men as sadhus on top of that.
            WorldFrame.LonLatToGame(85.3488, 27.7105, out double px, out double pz);
            int n = 3000, drawn = 0, simSadhus = 0;
            for (int i = 0; i < n; i++)
            {
                PedArchetype a = PedestrianSim.ArchetypeFor(px + 25, pz + 15, AreaType.OldCore, false, (i + 0.5f) / n);
                if (a == PedArchetype.Sadhu) simSadhus++;
                StreetStyle style = StreetStyles.At(px + 25, pz + 15, AreaType.OldCore);
                Assert.That(style, Is.EqualTo(StreetStyle.Pashupati));
                CharacterRecipe r = CrowdVariants.Recipe(CrowdVariants.KeyOf(a, style, 0, i * 7919 + 13, i & 15));
                bool sadhu = r.Hair == CharacterRecipe.HairDreadlocks && r[OutfitSlotKind.Torso].Item == OutfitItem.Robe;
                if (sadhu) drawn++;
                if (sadhu) Assert.That(a, Is.EqualTo(PedArchetype.Sadhu), "only sadhu archetypes are dressed as sadhus");
            }
            TestContext.WriteLine(drawn + " drawn sadhus of " + n + " (sim: " + simSadhus + ")");
            Assert.That(drawn, Is.EqualTo(simSadhus), "drawn share = sim share");
            Assert.That(drawn / (double)n, Is.InRange(0.04, 0.09), "about 6 in 91, not twice that");
        }

        // -------------------------------------------------------------------------------------------------------------
        // game/README.md, "The whole detail pass on a Mac": no step calls a world layer playable before it is wired

        private const string NeedsWorldIntegration = "(needs integ-world)";

        /// <summary>Step <paramref name="n"/> of the README's detail-pass walkthrough (its numbered paragraph).</summary>
        private static string ReadmeStep(string readme, int n)
        {
            int section = readme.IndexOf("## The whole detail pass on a Mac", StringComparison.Ordinal);
            Assert.That(section, Is.GreaterThanOrEqualTo(0), "the walkthrough section");
            int start = readme.IndexOf("\n" + n + ". **", section, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "step " + n);
            int end = readme.IndexOf("\n" + (n + 1) + ". **", start + 1, StringComparison.Ordinal);
            if (end < 0) end = readme.IndexOf("\nWhat to report", start + 1, StringComparison.Ordinal);
            return readme.Substring(start, (end < 0 ? readme.Length : end) - start);
        }

        /// <summary>Code (not comments) of the Unity-side runtime that builds and streams the world.</summary>
        private static string RuntimeCode()
        {
            var sb = new System.Text.StringBuilder();
            string assets = Path.Combine(GoldenFiles.RepoRoot, "game", "Assets", "Ghumante");
            foreach (string dir in new[] { "World", "App" })
            {
                foreach (string file in Directory.GetFiles(Path.Combine(assets, dir), "*.cs", SearchOption.AllDirectories))
                {
                    foreach (string line in File.ReadAllLines(file))
                    {
                        string t = line.TrimStart();
                        if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal)) continue;
                        sb.AppendLine(line);
                    }
                }
            }
            return sb.ToString();
        }

        [Test]
        public void ReadmeMarksTheWorldLayersThatAreNotWiredYet()
        {
            // The walkthrough may only call a world layer playable once the streamer builds it: bridge decks (BridgeMesher,
            // BridgeDeckIndex as the ground query's DeckFactory), roundabout statues and gates (RoundaboutDecorator), and
            // statue and pier colliders (generator cylinders registered with the ground query). Until then the step
            // carries the mark; once wired, the mark must go (re-check the step on a Mac when it does).
            string readme = File.ReadAllText(Path.Combine(GoldenFiles.RepoRoot, "game", "README.md"));
            string code = RuntimeCode();
            bool bridges = code.Contains("BridgeMesher.") && code.Contains("DeckFactory");
            bool decor = code.Contains("RoundaboutDecorator.");
            // (or the streamer registers the hero collider set, cylinders included, with the ground as it is: perf-cells gave
            // the B0 cells their own sets, so the heroes' set is no longer merged with them through AddAll).
            bool statueColliders = code.Contains("Cylinders.AddRange") || code.Contains(".AddCylinder(") || code.Contains(".AddAll(") ||
                                   code.Contains("HeroColliders") && code.Contains("_ground.Register(");
            TestContext.WriteLine("wired: bridges " + bridges + ", roundabout decor " + decor + ", statue colliders " + statueColliders);
            Assert.That(ReadmeStep(readme, 6).Contains(NeedsWorldIntegration), Is.EqualTo(!bridges), "step 6: bridges and the flyover");
            Assert.That(ReadmeStep(readme, 7).Contains(NeedsWorldIntegration), Is.EqualTo(!decor), "step 7: Maitighar Mandala and Shahid Gate");
            Assert.That(ReadmeStep(readme, 2).Contains(NeedsWorldIntegration), Is.EqualTo(!(bridges && statueColliders)),
                "step 2: the camera against statues, piers and decks");
        }

        // -------------------------------------------------------------------------------------------------------------
        // game/README.md, "The whole detail pass on a Mac": every place the steps teleport to on the committed sample

        [TestCase("Thamel Chowk", "Thamel Chowk", 85.3110, 27.7150)]
        [TestCase("Basantapur", "Basantapur", 85.3070, 27.7040)]
        [TestCase("Indra Chowk", "Indra Chowk", 85.3085, 27.7060)]
        [TestCase("Boudha", "Boudhan", 85.3620, 27.7215)]
        [TestCase("Swayambhunath", "Swayambhunath", 85.2903, 27.7149)]
        [TestCase("Thapathali", "Thapathali", 85.3190, 27.6920)]
        [TestCase("Maitighar Mandala", "Maitighar Mandala", 85.3205, 27.6945)]
        [TestCase("Shahid Gate", "Shahid Gate", 85.3155, 27.6985)]
        [TestCase("Ratna Park bus station", "Ratna Park", 85.3150, 27.7060)]
        [TestCase("Pashupatinath", "Pashupati", 85.3488, 27.7105)]
        public void ReadmeTeleportsFindTheirPlaceOnTheSample(string query, string expect, double lon, double lat)
        {
            string path = Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.search.ghsi");
            if (!File.Exists(path)) Assert.Ignore("sample search index not present: " + path);
            var engine = new SearchEngine(SearchIndexReader.Read(File.ReadAllBytes(path)));
            var hits = engine.Search(query, 5);
            Assert.That(hits.Count, Is.GreaterThan(0), query);
            SearchEntry top = hits[0].Entry;
            Assert.That(top.Name.En + " / " + top.Name.Default, Does.Contain(expect), "the first hit for '" + query + "'");
            WorldFrame.LonLatToGame(lon, lat, out double x, out double z);
            double d = Math.Sqrt(Math.Pow(0.1 * top.XDm - x, 2) + Math.Pow(0.1 * top.ZDm - z, 2));
            Assert.That(d, Is.LessThan(900.0), "'" + query + "' lands near the real place");
        }

        // -------------------------------------------------------------------------------------------------------------
        // The car profile on the sample region's GHRG car mask

        private static RouteGraph SampleGraph()
        {
            string path = Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.route.ghrg");
            if (!File.Exists(path)) Assert.Ignore("sample route graph not present: " + path);
            return RouteGraphReader.Read(File.ReadAllBytes(path));
        }

        private const Travel NoCar = Travel.Car | Travel.Jeep | Travel.Bus;

        [Test]
        public void CarRouteFromThamelToBoudhaKeepsOutOfTheGallis()
        {
            RouteGraph g = SampleGraph();
            WorldFrame.LonLatToGame(85.3110, 27.7150, out double tx, out double tz); // Thamel Chowk
            WorldFrame.LonLatToGame(85.3620, 27.7215, out double bx, out double bz); // Boudhanath
            var astar = new AStar(g);

            // As RoutePlanner plans: start and end snapped per profile, then A* with that profile.
            Route Plan(Travel profile)
            {
                int src = new NearestNode(g, profile, false).Find(tx, tz);
                int dst = new NearestNode(g, profile, true).Find(bx, bz);
                Assert.That(src, Is.GreaterThanOrEqualTo(0), profile + " start");
                Assert.That(dst, Is.GreaterThanOrEqualTo(0), profile + " end");
                return astar.FindRoute(src, dst, profile);
            }

            Route car = Plan(Travel.Car), bike = Plan(Travel.Motorbike);
            Assert.That(car, Is.Not.Null, "a car can reach Boudha from Thamel");
            Assert.That(bike, Is.Not.Null, "a scooter can reach Boudha from Thamel");
            TestContext.WriteLine("car " + car.LengthM.ToString("0") + " m / " + car.TimeS.ToString("0") + " s over " + car.Edges.Length +
                                  " edges; scooter " + bike.LengthM.ToString("0") + " m / " + bike.TimeS.ToString("0") + " s over " +
                                  bike.Edges.Length + " edges");
            Assert.That(car.LengthM, Is.GreaterThan(4000.0), "about 6 km by road");

            // The car never takes an edge the car mask closes (gallis, real width under about 3 m, footways, paths,
            // steps, pedestrian streets, motorcar=no) and keeps to roads a car is drawn on.
            foreach (int e in car.Edges)
            {
                Assert.That(g.EdgeAccess[e] & Travel.Car, Is.EqualTo(Travel.Car), "edge " + e + " " + g.EdgeClass[e]);
                RoadClass c = g.EdgeClass[e];
                Assert.That(c, Is.Not.AnyOf(RoadClass.Footway, RoadClass.Path, RoadClass.Steps, RoadClass.Pedestrian, RoadClass.Cycleway,
                                            RoadClass.Bridleway), "edge " + e);
            }

            // Thamel's galli network: the edges within 600 m of Thamel Chowk that two-wheelers may use but cars may not.
            int[] src = g.EdgeSource;
            int gallis = 0, carOnGalli = 0, bikeOnGalli = 0;
            var galli = new bool[g.EdgeCount];
            for (int e = 0; e < g.EdgeCount; e++)
            {
                if ((g.EdgeAccess[e] & Travel.Motorbike) == 0 || (g.EdgeAccess[e] & NoCar) != 0) continue;
                double mx = 0.05 * (g.NodeXDm[src[e]] + g.NodeXDm[g.EdgeTarget[e]]), mz = 0.05 * (g.NodeZDm[src[e]] + g.NodeZDm[g.EdgeTarget[e]]);
                if ((mx - tx) * (mx - tx) + (mz - tz) * (mz - tz) > 600.0 * 600.0) continue;
                galli[e] = true;
                gallis++;
            }
            foreach (int e in car.Edges) if (galli[e]) carOnGalli++;
            foreach (int e in bike.Edges) if (galli[e]) bikeOnGalli++;
            TestContext.WriteLine(gallis + " two-wheeler-only edges round Thamel; the scooter route uses " + bikeOnGalli);
            Assert.That(gallis, Is.GreaterThan(20), "the rebuilt mask closes Thamel's gallis to cars");
            Assert.That(carOnGalli, Is.EqualTo(0), "the car route avoids the galli network");

            // The scooter may use them: from a galli in the middle of Thamel, the scooter's route starts down the galli;
            // the car is snapped to a street it can drive.
            int inner = Array.FindIndex(galli, b => b);
            int start = g.EdgeTarget[inner];
            Route bikeFromGalli = astar.FindRoute(start, new NearestNode(g, Travel.Motorbike, true).Find(bx, bz), Travel.Motorbike);
            Assert.That(bikeFromGalli, Is.Not.Null, "a scooter in a Thamel galli finds its way to Boudha");
            Assert.That(bikeFromGalli.Edges.Any(e => (g.EdgeAccess[e] & NoCar) == 0), Is.True, "the scooter route rides a galli");
            int carStart = new NearestNode(g, Travel.Car, false).Find(0.1 * g.NodeXDm[start], 0.1 * g.NodeZDm[start]);
            Assert.That(carStart, Is.GreaterThanOrEqualTo(0));
            bool carEdgeOut = false;
            for (int k = g.Offsets[carStart]; k < g.Offsets[carStart + 1]; k++)
                if ((g.EdgeAccess[k] & Travel.Car) != 0) carEdgeOut = true;
            Assert.That(carEdgeOut, Is.True, "the car starts on a node with a drivable street out");
            Assert.That(bike.TimeS, Is.LessThanOrEqualTo(car.TimeS * 1.5), "the scooter is not slower than the car by much");
        }
    }
}
