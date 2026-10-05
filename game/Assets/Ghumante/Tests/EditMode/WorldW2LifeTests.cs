using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Aviation;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.Core.Traffic;
using Ghumante.World.Aviation;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The instanced dressing and the life host (W2_DESIGN 5.1-5.8, 8): trees, props and parked vehicles of a real
    /// tile (never on a building, deterministic), the tier caps and nearest-first allocation, the kit and aircraft
    /// mesh budgets, the runway and its lights, officer posts, the crowd animation, and the life sims driven through
    /// <see cref="LifeHost"/> (vehicles and people appear, no motor agent inside a no-vehicle zone, same seed → same
    /// snapshots, aircraft from the aviation sidecar). Engine-free.
    /// </summary>
    public class WorldW2LifeTests
    {
        private static readonly TileId Thamel = TileId.At(10, SampleRegion.ThamelX, SampleRegion.ThamelZ);

        private static TileBuild BuildTile(TileId tile)
        {
            var b = new TileBuild { Node = new SelectedNode(tile, tile, true) };
            TileBuild.Execute(b, SampleRegion.Pack, StreamingConfig.ForTier(StreamingConfig.TierMid), new MeshingSettings(), null);
            return b;
        }

        [Test]
        public void TileInstancesHoldTreesPropsAndParkedVehiclesIndexedByCell()
        {
            TileBuild b = BuildTile(new TileId(10, 516, 161));
            TileInstances inst = b.Instances;
            Assert.IsNotNull(inst);
            Assert.Greater(inst.Props.Count, 20, "lamps, stops and signals");
            Assert.Greater(inst.Parked.Count, 100, "parked motorbikes line the old-core lanes");
            Assert.AreEqual(inst.CellsPerSide * inst.CellsPerSide + 1, inst.ParkedCells.Length);
            Assert.AreEqual(inst.Parked.Count, inst.ParkedCells[inst.ParkedCells.Length - 1]);
            for (int c = 0; c + 1 < inst.ParkedCells.Length; c++)
                for (int i = inst.ParkedCells[c]; i < inst.ParkedCells[c + 1]; i++)
                    Assert.AreEqual(c, inst.CellOf(inst.Parked[i].X, inst.Parked[i].Z));

            var mask = new FootprintGrid(b.Source);
            int fleet = 0;
            foreach (ParkedVehicle p in inst.Parked)
            {
                Assert.IsFalse(mask.Inside(p.X, p.Z), "parked on a building");
                Assert.That(p.Variant, Is.InRange(0, VehicleCatalog.Count - 1));
                if (p.CommunityFleet) fleet++;
            }
            float share = (float)fleet / inst.Parked.Count;
            Assert.That(share, Is.InRange(0.15f, 0.4f), "about 30% carry the community-fleet tag");

            // Deterministic between builds (D10 seed).
            TileBuild again = BuildTile(new TileId(10, 516, 161));
            Assert.AreEqual(inst.Parked.Count, again.Instances.Parked.Count);
            Assert.AreEqual(inst.Trees.Count, again.Instances.Trees.Count);
            for (int i = 0; i < inst.Parked.Count; i++)
            {
                Assert.AreEqual(inst.Parked[i].X, again.Instances.Parked[i].X);
                Assert.AreEqual(inst.Parked[i].Id, again.Instances.Parked[i].Id);
            }
        }

        [Test]
        public void TierCapsAndNearestFirstAllocation()
        {
            DressingConfig mid = DressingConfig.ForTier(1);
            Assert.AreEqual(0, mid.TreeLodAt(30));
            Assert.AreEqual(1, mid.TreeLodAt(100));
            Assert.AreEqual(2, mid.TreeLodAt(1000));
            Assert.AreEqual(3, mid.TreeLodAt(1300));
            Assert.AreEqual(new[] { 80, 200, 400 }, new[] { DressingConfig.ForTier(0).ParkedCap, mid.ParkedCap, DressingConfig.ForTier(2).ParkedCap });
            Assert.AreEqual(0x5E8F45u, DressingConfig.CrownColour(TreeSpecies.Jacaranda, 10), "October jacaranda is green");
            Assert.AreEqual(0x8E6CC8u, DressingConfig.CrownColour(TreeSpecies.Jacaranda, 4), "violet in April");

            LifeLod lod = LifeLod.ForTier(1);
            Assert.AreEqual(new[] { 1, 6, 16 }, lod.VehicleCaps);
            Assert.AreEqual(new[] { 3, 10, 39 }, lod.PeopleCaps);
            float[] d = { 50f, 5f, 12f, 3f, 30f, 500f, 20f };
            int[] level = new int[7], order = new int[7];
            float[] keys = new float[7];
            LifeLod.Assign(d, 7, new[] { 1, 2, 1 }, new[] { 10f, 40f, 100f }, level, order, keys);
            Assert.AreEqual(new[] { -1, 1, 1, 0, -1, -1, 2 }, level, "nearest first: 3 m takes LOD0, 5 and 12 m fill LOD1, 20 m the far level");
        }

        [Test]
        public void KitAndAircraftMeshesStayInsideTheirBudgets()
        {
            var m = new MeshData();
            for (int s = 0; s < 3; s++)
                for (int lod = 0; lod < KitMeshes.TreeLods; lod++)
                {
                    m.Clear();
                    int t = KitMeshes.Tree((TreeShape)s, lod, m);
                    Assert.Greater(t, 0);
                    Assert.LessOrEqual(t, KitMeshes.TreeBudget[lod], (TreeShape)s + " LOD" + lod);
                }
            for (int k = 0; k < KitMeshes.PropKinds; k++)
            {
                m.Clear();
                Assert.Greater(KitMeshes.Prop((StreetPropKind)k, m), 0, ((StreetPropKind)k).ToString());
                Assert.LessOrEqual(m.TriangleCount, 400);
            }
            for (int c = 0; c < 6; c++)
                for (int lod = 0; lod < 3; lod++)
                {
                    m.Clear();
                    int t = AircraftMesher.Build((AircraftClass)c, lod, m);
                    Assert.Greater(t, 50, (AircraftClass)c + " LOD" + lod);
                    Assert.LessOrEqual(t, AircraftMesher.Budget[lod], (AircraftClass)c + " LOD" + lod);
                    float x0, y0, z0, x1, y1, z1, len, span, h;
                    m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                    AircraftMesher.Dims((AircraftClass)c, out len, out span, out h);
                    Assert.AreEqual(len, z1 - z0, len * 0.12f, (AircraftClass)c + " length");
                    Assert.AreEqual(span, x1 - x0, span * 0.12f, (AircraftClass)c + " span");
                    Assert.GreaterOrEqual(y0, -0.1f, "nothing below the wheels");
                }
            m.Clear();
            int body = KitMeshes.Person(KitMeshes.PersonPart.Body, 0, 2, m);
            Assert.That(body, Is.InRange(50, 600));
            m.Clear();
            Assert.LessOrEqual(KitMeshes.Cow(false, 0, m), 2500);
        }

        [Test]
        public void TheRunwayFollowsTheThresholdsWithLights()
        {
            string path = SampleRegion.FilePath(".aviation.json");
            if (!File.Exists(path)) Assert.Ignore("no aviation sidecar");
            AviationConfig c = AviationConfig.Parse(File.ReadAllText(path));
            var rm = new RunwayMesher();
            var mesh = new MeshData();
            var lights = new List<AirportLight>();
            Assert.Greater(rm.Build(c, null, mesh, lights), 100);
            double heading = Math.Atan2(rm.DirX, rm.DirZ) * 180 / Math.PI;
            Assert.AreEqual(22.0, heading, 1.5, "runway 02/20 true heading");
            Assert.AreEqual(3326, rm.NorthEndS - rm.SouthEndS, 60, "paved length");
            Assert.Greater(lights.Count, 100);
            double farthest = 0;
            foreach (AirportLight l in lights) farthest = Math.Min(farthest, rm.Along(l.X, l.Z));
            Assert.AreEqual(rm.SouthEndS - RunwayMesher.ApproachLengthM, farthest, 1.0, "870 m of approach lights south of 02");
            // The profile climbs about 0.8% to the north.
            Assert.Greater(rm.HeightAt(rm.Threshold20S), rm.HeightAt(0));
        }

        [Test]
        public void CrowdAnimationStrikesFeetAndOfficersCycleSignals()
        {
            int strikes = 0;
            float prev = 0f;
            for (int f = 1; f <= 300; f++)
            {
                float t = f / 30f;
                PersonPose p = PersonAnimation.Pose(PedClip.Walk, t, prev, 1.4f, 3);
                if (p.Strike != 0) strikes++;
                prev = t;
            }
            float rate = PersonAnimation.StepRate(1.4f);
            Assert.AreEqual(rate * 10f, strikes, 2f, "one footstep per step");
            PersonPose sit = PersonAnimation.Pose(PedClip.Sit, 1f, 0.9f, 0f, 1);
            Assert.Greater(sit.DropM, 0.2f);
            var signals = new HashSet<int>();
            double start;
            for (int s = 0; s < 600; s += 5) signals.Add(OfficerPosts.SignalAt(12345u, s, out start));
            Assert.GreaterOrEqual(signals.Count, 4, "officers go through their hand signals");
        }

        [Test]
        public void OfficersStandOnlyAtPoliceChowks()
        {
            int posts = 0, tiles = 0;
            foreach (PackEntry e in SampleRegion.Pack.Entries)
            {
                TileId id = TileId.FromKey(e.Key);
                if (id.Level != 10) continue;
                TileData t = SampleRegion.Pack.ReadTile(id);
                if (t == null || t.Junctions.Count == 0) continue;
                List<OfficerPost> p = OfficerPosts.For(t);
                foreach (OfficerPost o in p)
                {
                    bool near = false;
                    foreach (JunctionRecord j in t.Junctions)
                    {
                        double dx = j.XCm / 100.0 - o.X, dz = j.ZCm / 100.0 - o.Z;
                        if (dx * dx + dz * dz < 60 * 60 && (j.Kind == JunctionKind.Police || j.Has(JunctionFlags.HasPolice) || j.Kind == JunctionKind.SyntheticIsland ||
                                                            j.Kind == JunctionKind.Roundabout || j.Kind == JunctionKind.Circular)) near = true;
                    }
                    Assert.IsTrue(near, "officer at " + o.X + "," + o.Z + " of " + id + " is not at a chowk");
                }
                posts += p.Count;
                tiles++;
            }
            Assert.Greater(tiles, 0);
            Assert.Greater(posts, 0, "the sample's police chowks get officers");
        }

        private static LifeHost NewHost(ulong seed, out List<TileId> tiles)
        {
            RouteSet routes = null;
            string ghrt = SampleRegion.FilePath(".transit.ghrt");
            if (File.Exists(ghrt)) routes = RouteSet.Read(File.ReadAllBytes(ghrt));
            CuratedDb db = null;
            string ghcd = SampleRegion.FilePath(".curated.ghcd");
            if (File.Exists(ghcd)) db = CuratedDb.Read(File.ReadAllBytes(ghcd));
            var host = new LifeHost(LifeSettings.ForTier(1), seed, routes, null, db);
            tiles = new List<TileId>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var id = new TileId(10, Thamel.Tx + dx, Thamel.Ty + dz);
                    if (!SampleRegion.Pack.Contains(id)) continue;
                    host.AddTile(id, SampleRegion.Pack.ReadTile(id));
                    tiles.Add(id);
                }
            return host;
        }

        private static void Run(LifeHost host, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                host.Tick(0.1f, SampleRegion.ThamelX, SampleRegion.ThamelZ, 0f, 1f, 10f, 10, 0f, WeatherKind.Clear);
                host.Wait();
            }
        }

        [Test]
        public void LifeHostRunsTrafficPeopleAndAnimalsAroundThamel()
        {
            List<TileId> tiles;
            using (LifeHost host = NewHost(42, out tiles))
            {
                Run(host, 300);
                Assert.IsNull(host.Error, host.Error != null ? host.Error.ToString() : "");
                Assert.AreEqual(tiles.Count, host.TileCount);
                Assert.Greater(host.VehicleCount, 3, "traffic spawned");
                Assert.Greater(host.PeopleCount, 3, "people spawned");
                Assert.Greater(host.Zones.ZoneCount, 0, "sacred zones of the visible tiles");

                // V5 from the presenter side: no motor agent stands inside a no-vehicle zone.
                for (int i = 0; i < host.VehicleCount; i++)
                {
                    AgentPose a = host.Vehicles[i];
                    if (!VehicleClasses.IsMotor(a.Class)) continue;
                    Assert.IsFalse(host.Zones.Contains(a.X, a.Z), "motor agent " + a.AgentId + " inside a no-vehicle zone");
                }
                // Removing the tiles empties the lanes.
                foreach (TileId id in tiles) host.RemoveTile(id);
                Run(host, 3);
                Assert.AreEqual(0, host.TileCount);
            }
        }

        [Test]
        public void SameSeedGivesTheSameSnapshots()
        {
            List<TileId> ta, tb;
            using (LifeHost a = NewHost(7, out ta))
            using (LifeHost b = NewHost(7, out tb))
            {
                Run(a, 150);
                Run(b, 150);
                Assert.AreEqual(a.VehicleCount, b.VehicleCount);
                Assert.AreEqual(a.PeopleCount, b.PeopleCount);
                for (int i = 0; i < a.VehicleCount; i++)
                {
                    Assert.AreEqual(a.Vehicles[i].AgentId, b.Vehicles[i].AgentId);
                    Assert.AreEqual(a.Vehicles[i].X, b.Vehicles[i].X);
                    Assert.AreEqual(a.Vehicles[i].Z, b.Vehicles[i].Z);
                }
            }
        }

        [Test]
        public void AircraftFlyFromTheAviationSidecar()
        {
            string path = SampleRegion.FilePath(".aviation.json");
            if (!File.Exists(path)) Assert.Ignore("no aviation sidecar");
            AviationConfig c = AviationConfig.Parse(File.ReadAllText(path));
            using (var host = new LifeHost(LifeSettings.ForTier(0), 9, null, c, null))
            {
                Assert.IsTrue(host.HasAirport);
                int seen = 0;
                for (int i = 0; i < 4000 && seen == 0; i++)
                {
                    host.Tick(0.1f, c.Threshold02.X, c.Threshold02.Z, 0f, 1f, 8f, 10, 0f, WeatherKind.Clear);
                    host.Wait();
                    seen = host.AircraftCount;
                }
                Assert.Greater(seen, 0, "the 08:00 peak puts aircraft in the sky within minutes");
                AircraftState s = host.Aircraft[0];
                float thrust, reverse;
                AircraftAudio.Power(s, out thrust, out reverse);
                Assert.That(thrust, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void HornsAreConsumedOnceAndAFailedSimGoesQuiet()
        {
            List<TileId> tiles;
            using (LifeHost host = NewHost(11, out tiles))
            {
                Run(host, 20);
                host.TakeHorns();
                Assert.AreEqual(0, host.TakeHorns(), "a step's horns are handed out once, however many frames read them");
                Assert.AreEqual(0, host.HornCount);

                int ran = 0;
                host.Post((t, p, a) => throw new InvalidOperationException("boom"));
                Run(host, 1);
                Assert.IsNotNull(host.Error, "the posted failure stops the sims");
                for (int i = 0; i < 20; i++) host.Post((t, p, a) => ran++);
                Run(host, 5);
                Assert.AreEqual(0, ran, "posts after a failure are dropped, not queued forever");
                Assert.AreEqual(0, host.HornCount, "no horn batch loops after a failure");
                Assert.AreEqual(0f, host.SnapshotAgeS);
            }
        }

        [Test]
        public void AHeldSnapshotReportsItsAgeForExtrapolation()
        {
            List<TileId> tiles;
            using (LifeHost host = NewHost(12, out tiles))
            {
                Run(host, 5);
                Assert.AreEqual(0f, host.SnapshotAgeS, 1e-5f, "every frame completes a step: no extrapolation");
                var gate = new System.Threading.ManualResetEventSlim(false);
                host.Post((t, p, a) => gate.Wait(5000));
                host.Tick(0.05f, SampleRegion.ThamelX, SampleRegion.ThamelZ, 0f, 1f, 10f, 10, 0f, WeatherKind.Clear); // starts the slow step
                host.Tick(0.03f, SampleRegion.ThamelX, SampleRegion.ThamelZ, 0f, 1f, 10f, 10, 0f, WeatherKind.Clear); // held
                Assert.AreEqual(0.05f, host.SnapshotAgeS, 1e-4f, "the held snapshot lags by the running step");
                host.Tick(0.03f, SampleRegion.ThamelX, SampleRegion.ThamelZ, 0f, 1f, 10f, 10, 0f, WeatherKind.Clear);
                Assert.AreEqual(0.08f, host.SnapshotAgeS, 1e-4f);
                gate.Set();
                host.Wait();
                Assert.IsNull(host.Error);
            }
        }

        [Test]
        public void CarryPropsHaveMeshesForEveryKind()
        {
            var m = new MeshData(256, 768);
            Assert.AreEqual(0, KitMeshes.Carry(0, 0, m), "no prop");
            for (int c = 1; c < KitMeshes.CarryProps; c++)
                for (int lod = 0; lod < 2; lod++)
                {
                    m.Clear();
                    int tris = KitMeshes.Carry(c, lod, m);
                    Assert.Greater(tris, 0, "carry prop " + c + " lod " + lod);
                    Assert.LessOrEqual(tris, 300, "carry props stay cheap");
                }
        }
    }
}
