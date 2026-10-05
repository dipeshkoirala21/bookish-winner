using Ghumante.Core.Data;
using Ghumante.Core.Synth;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Place overlays, footprint occlusion and the galli test (W2_DESIGN 7.1, 7.2), and the engine-voice
    /// pick by true distance.</summary>
    public class SynthPlaceTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);

        private static int[] Cm(params double[] metres)
        {
            var r = new int[metres.Length];
            for (int i = 0; i < metres.Length; i++) r[i] = (int)System.Math.Round(metres[i] * 100.0);
            return r;
        }

        private static BuildingRecord Box(double x0, double z0, double x1, double z1, double heightM)
        {
            return new BuildingRecord
            {
                HeightCm = (ulong)(heightM * 100.0),
                Rings = new[] { Cm(x0, z0, x1, z0, x1, z1, x0, z1) },
            };
        }

        private static TileData MakeTile()
        {
            var t = new TileData { Tile = Leaf };
            t.Roads.Add(new RoadRecord { RoadClass = RoadClass.Primary, Points = Cm(100, 500, 900, 500) });
            t.Roads.Add(new RoadRecord { RoadClass = RoadClass.Residential, Points = Cm(100, 650, 900, 650) });
            t.Lines.Add(new LineRecord { Kind = LineKind.River, Points = Cm(500, 800, 900, 800) });
            t.Areas.Add(new AreaRecord
            {
                Kind = AreaKind.Park,
                Vertices = Cm(100, 100, 200, 100, 200, 200, 100, 200),
                Indices = new[] { 0, 1, 2, 0, 2, 3 },
                Rings = new[] { 0, 4 },
            });
            t.Buildings.Add(Box(400, 300, 410, 310, 10)); // a lone 10 m house
            t.Buildings.Add(Box(300, 601.5, 340, 610, 9)); // galli walls either side of z = 600
            t.Buildings.Add(Box(300, 590, 340, 598.5, 9));
            return t;
        }

        private static PlaceAudioIndex MakeIndex()
        {
            var idx = new PlaceAudioIndex();
            idx.AddTile(Leaf, MakeTile());
            return idx;
        }

        private static double X(double local)
        {
            return Leaf.X0 + local;
        }

        private static double Z(double local)
        {
            return Leaf.Z0 + local;
        }

        [Test]
        public void RingRoadNearnessFollowsArterialsOnly()
        {
            PlaceAudioIndex idx = MakeIndex();
            Assert.That(idx.Sample(X(500), Z(520)).RingRoad01, Is.EqualTo(1f));
            Assert.That(idx.Sample(X(500), Z(600)).RingRoad01, Is.EqualTo(0.4f).Within(1e-4f));
            // On the residential street (150 m from the primary): no roar.
            Assert.That(idx.Sample(X(500), Z(650)).RingRoad01, Is.EqualTo(0f));
        }

        [Test]
        public void ParkAndWaterFadeWithDistance()
        {
            PlaceAudioIndex idx = MakeIndex();
            Assert.That(idx.Sample(X(150), Z(150)).Park01, Is.EqualTo(1f));
            Assert.That(idx.Sample(X(220), Z(150)).Park01, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(idx.Sample(X(300), Z(150)).Park01, Is.EqualTo(0f));
            Assert.That(idx.Sample(X(700), Z(810)).Water01, Is.EqualTo(1f));
            Assert.That(idx.Sample(X(700), Z(700)).Water01, Is.EqualTo((120f - 100f) / 105f).Within(1e-4f));
            Assert.That(idx.Sample(X(150), Z(150)).Water01, Is.EqualTo(0f));
        }

        [Test]
        public void AirportNearnessCoversTheFieldPlusThreeKilometres()
        {
            Assert.That(PlaceAudioIndex.AirportNearness(0, 0, -1000, 0, 1000, 0), Is.EqualTo(1f));
            Assert.That(PlaceAudioIndex.AirportNearness(0, 700 + 1500, -1000, 0, 1000, 0), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(PlaceAudioIndex.AirportNearness(0, 5000, -1000, 0, 1000, 0), Is.EqualTo(0f));
        }

        [Test]
        public void OcclusionHitsFootprintsBelowTheRoofOnly()
        {
            PlaceAudioIndex idx = MakeIndex();
            Assert.IsTrue(idx.Occluded(X(405), 1.6f, Z(290), X(405), 1f, Z(320)), "through the house");
            Assert.IsFalse(idx.Occluded(X(395), 1.6f, Z(290), X(395), 1f, Z(320)), "beside the house");
            Assert.IsFalse(idx.Occluded(X(405), 1.6f, Z(290), X(405), 40f, Z(320)), "a bird high above the roof");
            Assert.IsFalse(idx.Occluded(X(405), 1.6f, Z(290), X(405), 1f, Z(295)), "short of the wall");
            // Segments crossing into the neighbouring (absent) tile are fine too.
            Assert.IsFalse(idx.Occluded(X(-50), 1.6f, Z(290), X(10), 1f, Z(290)));
        }

        [Test]
        public void GalliNeedsANarrowLaneWithWallsOnBothSides()
        {
            PlaceAudioIndex idx = MakeIndex();
            Assert.That(idx.GalliWidth(X(320), Z(600), 1f, 0f, 1.5f), Is.EqualTo(3f));
            Assert.That(idx.GalliWidth(X(320), Z(600), 0f, 0f, 1.5f), Is.EqualTo(0f), "no direction");
            Assert.That(idx.GalliWidth(X(500), Z(600), 1f, 0f, 1.5f), Is.EqualTo(0f), "no walls");
            Assert.That(idx.GalliWidth(X(320), Z(600), 1f, 0f, 5f), Is.EqualTo(0f), "a wide street");
            Assert.IsTrue(idx.InsideBuilding(X(320), Z(605)));
            Assert.IsFalse(idx.InsideBuilding(X(320), Z(600)));
        }

        [Test]
        public void RemovedTilesStopContributing()
        {
            PlaceAudioIndex idx = MakeIndex();
            Assert.That(idx.TileCount, Is.EqualTo(1));
            idx.AddTile(Leaf, MakeTile()); // replace, not duplicate
            Assert.That(idx.TileCount, Is.EqualTo(1));
            idx.RemoveTile(Leaf);
            PlaceSample s = idx.Sample(X(500), Z(520));
            Assert.That(s.RingRoad01 + s.Park01 + s.Water01, Is.EqualTo(0f));
            Assert.IsFalse(idx.Occluded(X(405), 1.6f, Z(290), X(405), 1f, Z(320)));
        }

        [Test]
        public void EngineVoicesGoToTheNearestByTrueDistance()
        {
            // Index 0 is a bus 8 m behind the camera: the render LOD gives it no priority, the voice pick must.
            float[] d = { 8f, 60f, 130f, 30f, 60f, float.NaN, 25f };
            var sel = new bool[d.Length];
            var scratch = new int[4];
            int n = NearestSelect.Select(d, d.Length, 3, 120f, sel, scratch);
            Assert.That(n, Is.EqualTo(3));
            Assert.That(sel, Is.EqualTo(new[] { true, false, false, true, false, false, true }));
            // Ties break by the lower index; out-of-range and NaN never get a voice.
            n = NearestSelect.Select(d, d.Length, 4, 120f, sel, scratch);
            Assert.That(n, Is.EqualTo(4));
            Assert.That(sel, Is.EqualTo(new[] { true, true, false, true, false, false, true }));
            Assert.That(NearestSelect.Select(d, d.Length, 0, 120f, sel, scratch), Is.EqualTo(0));
            Assert.That(sel, Has.None.True);
        }
    }
}
