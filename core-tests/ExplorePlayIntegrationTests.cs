using System;
using System.IO;
using System.Linq;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
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
