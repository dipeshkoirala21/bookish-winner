using System;
using Ghumante.App.Explore;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Routing;
using Ghumante.Core.Search;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// Getting into the world (M1 track D): which region Explore opens, where the scooter spawns (Thamel, on a
    /// motorbike-routable road, facing along it) and what the HUD calls the place you are in. Uses the committed
    /// kathmandu_core sample. Engine-free.
    /// </summary>
    public class ExploreSetupTests
    {
        [Test]
        public void PicksTheValleyThenTheSampleThenAnything()
        {
            Assert.IsNull(ExploreRegions.Pick(null));
            Assert.IsNull(ExploreRegions.Pick(new string[0]));
            Assert.AreEqual("kathmandu_core", ExploreRegions.Pick(new[] { "kathmandu_core" }));
            Assert.AreEqual("kathmandu_valley", ExploreRegions.Pick(new[] { "kathmandu_core", "kathmandu_valley" }));
            Assert.AreEqual("kathmandu_core", ExploreRegions.Pick(new[] { "pokhara", "kathmandu_core" }));
            Assert.AreEqual("annapurna", ExploreRegions.Pick(new[] { "pokhara", "annapurna" }), "else the first by name");
        }

        [Test]
        public void SpawnsOnARoadInThamelFacingAlongIt()
        {
            RouteGraph graph = ExploreSample.Graph;
            NearestNode starts = ExploreSample.Planner.Starts;
            SpawnPoint spawn = ExploreSpawn.Find(ExploreSample.Search, graph, starts, ExploreSpawn.DefaultPlace, 0, 0);
            SearchEntry thamel = ExploreSpawn.Best(ExploreSample.Search, "Thamel");
            Assert.IsNotNull(thamel);
            Assert.IsTrue(thamel.IsPlace);
            Assert.IsTrue(spawn.OnRoad);
            Assert.GreaterOrEqual(spawn.Node, 0);
            Assert.AreEqual("Thamel", spawn.Place.Display(false));
            Assert.AreEqual("ठमेल", spawn.Place.Display(true));
            double d = Math.Sqrt((spawn.X - thamel.X) * (spawn.X - thamel.X) + (spawn.Z - thamel.Z) * (spawn.Z - thamel.Z));
            Assert.Less(d, 120.0, "the spawn is in Thamel");

            // Facing along a road a motorbike may leave the node by.
            TravelProfile bike = TravelProfiles.Get(Travel.Motorbike);
            bool along = false;
            for (int e = graph.Offsets[spawn.Node]; e < graph.Offsets[spawn.Node + 1]; e++)
            {
                if (double.IsPositiveInfinity(bike.EdgeTimeS(graph, e))) continue;
                double[] g = graph.EdgeGeometry(e);
                float heading = (float)Math.Atan2(g[2] - g[0], g[3] - g[1]);
                if (Math.Abs(ArcadeVehicle.WrapAngle(heading - spawn.HeadingRad)) < 1e-3f) along = true;
            }
            Assert.IsTrue(along, "the heading is the first segment of a usable edge");
            Assert.That(spawn.HeightHint, Is.InRange(1200f, 1450f), "Kathmandu sits about 1 300 m up");
        }

        [Test]
        public void WithoutSearchOrRoadsTheSpawnIsTheFallback()
        {
            SpawnPoint spawn = ExploreSpawn.Find(null, null, null, "Thamel", 10, 20);
            Assert.AreEqual(10, spawn.X);
            Assert.AreEqual(20, spawn.Z);
            Assert.IsFalse(spawn.OnRoad);
            Assert.AreEqual(-1, spawn.Node);
            Assert.IsNull(spawn.Place);
        }

        [Test]
        public void PointAlongWalksAPolyline()
        {
            double[] xz = { 0, 0, 0, 10, 10, 10 };
            double x, z;
            float h;
            ExploreSpawn.PointAlong(xz, 15, out x, out z, out h);
            Assert.AreEqual(5, x, 1e-9);
            Assert.AreEqual(10, z, 1e-9);
            Assert.AreEqual((float)(Math.PI / 2), h, 1e-6f, "east");
            ExploreSpawn.PointAlong(xz, 0, out x, out z, out h);
            Assert.AreEqual(0, z, 1e-9);
            Assert.AreEqual(0f, h, 1e-6f, "north");
            ExploreSpawn.PointAlong(xz, 100, out x, out z, out h);
            Assert.AreEqual(10, x, 1e-9, "clamped to the end");
        }

        [Test]
        public void ThePlaceIsTheNeighbourhoodOrTheLandmarkYouStandBy()
        {
            var namer = new PlaceNamer(ExploreSample.Search.Index);
            Assert.IsTrue(namer.HasIndexPlaces);
            SearchEntry thamel = ExploreSpawn.Best(ExploreSample.Search, "Thamel");
            NameRecord here = namer.Resolve(null, thamel.X + 30, thamel.Z - 20);
            Assert.IsNotNull(here);
            Assert.AreEqual("Thamel", here.Display(false));

            // At the stupa, with its level-10 tile loaded (as the streamer adds it), the landmark beats Baudha.
            SearchEntry stupa = ExploreSpawn.Best(ExploreSample.Search, "Boudhanath");
            var ground = new TileGroundQuery();
            ground.Add(SampleRegion.Pack.ReadTile(TileId.At(10, stupa.X, stupa.Z)));
            NameRecord atStupa = namer.Resolve(ground, stupa.X, stupa.Z);
            Assert.IsNotNull(atStupa);
            StringAssert.StartsWith("Boudh", atStupa.Display(false));
            Assert.AreEqual("बौद्धनाथ स्तूप", atStupa.Display(true));

            // Without the tile only the place is known.
            StringAssert.StartsWith("Baudha", namer.Resolve(null, stupa.X, stupa.Z).Display(false));
        }

        [Test]
        public void ThePlaceLabelDoesNotFlicker()
        {
            var namer = new PlaceNamer(ExploreSample.Search.Index);
            SearchEntry thamel = ExploreSpawn.Best(ExploreSample.Search, "Thamel");
            SearchEntry boudha = ExploreSpawn.Best(ExploreSample.Search, "Baudha");
            Assert.IsTrue(namer.Update(null, thamel.X, thamel.Z), "the first name shows at once");
            Assert.AreEqual("Thamel", namer.Current.Display(false));
            Assert.IsFalse(namer.Update(null, thamel.X, thamel.Z));
            Assert.IsFalse(namer.Update(null, boudha.X, boudha.Z), "a new name must win twice");
            Assert.AreEqual("Thamel", namer.Current.Display(false));
            Assert.IsTrue(namer.Update(null, boudha.X, boudha.Z));
            StringAssert.StartsWith("Baudha", namer.Current.Display(false));
            Assert.IsFalse(namer.Update(null, 0, 0));
            Assert.IsTrue(namer.Update(null, 0, 0), "nowhere: the label goes after two looks");
            Assert.IsNull(namer.Current);
            namer.Reset();
            Assert.IsNull(namer.Current);
        }

        [Test]
        public void BusinessesNeverNameTheSpot()
        {
            Assert.IsTrue(PlaceNamer.Landmarkish(PoiKind.Stupa));
            Assert.IsTrue(PlaceNamer.Landmarkish(PoiKind.HeritageSquare));
            Assert.IsTrue(PlaceNamer.Landmarkish(PoiKind.Peak));
            Assert.IsFalse(PlaceNamer.Landmarkish(PoiKind.Restaurant));
            Assert.IsFalse(PlaceNamer.Landmarkish(PoiKind.Hotel));
            Assert.IsFalse(PlaceNamer.Landmarkish(PoiKind.Shop));
            Assert.IsFalse(PlaceNamer.Landmarkish(PoiKind.Bank));
            Assert.IsFalse(PlaceNamer.Landmarkish(PoiKind.Fuel));
            Assert.AreEqual(0.0, PlaceNamer.PlaceRadius(PlaceKind.District), "admin areas are too big for 'you are here'");
            Assert.Greater(PlaceNamer.PlaceRadius(PlaceKind.City), PlaceNamer.PlaceRadius(PlaceKind.Neighbourhood));
        }

        [Test]
        public void RegionLabelsReadNicely()
        {
            Assert.AreEqual("Kathmandu Core", ExploreRegions.Label("kathmandu_core"));
            Assert.AreEqual("Kathmandu Valley", ExploreRegions.Label("kathmandu_valley"));
            Assert.AreEqual("", ExploreRegions.Label(null));
        }
    }
}
