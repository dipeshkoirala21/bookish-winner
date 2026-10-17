using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Generators.Props;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Street prop placement on the sample pack (package props; review finding "street lamps on bridges stand on the
    /// river bed"): over every level-10 tile of shared/sample-regions/kathmandu_core, nothing stands under a deck or
    /// pierces one, lamps on decks stand at the parapet line, every generated prop sits within
    /// <see cref="PropPlacement.MaxOffSurfaceM"/> of the drawn road surface beside it, OSM road furniture leaves the
    /// carriageways, and the heritage and solar lamps appear where the brief puts them.
    /// </summary>
    public class PropsPlacementTests
    {
        private sealed class Sample
        {
            public TileId Id;
            public TileData Tile;
            public TileHeightSampler Heights;
            public List<StreetProp> Props;
            public RoadSurfaceQuery Surface;
            public IBridgeDeckQuery Decks;
        }

        private static List<Sample> _samples;

        private static List<Sample> Samples()
        {
            if (_samples != null) return _samples;
            var list = new List<Sample>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                TileHeightSampler h = TileHeightSampler.ForArea(t, id, 1);
                var props = new List<StreetProp>();
                PropPlacement.Place(t, h, props);
                list.Add(new Sample
                {
                    Id = id, Tile = t, Heights = h, Props = props, Surface = RoadSurfaceQuery.For(t, h, new RoadOptions()), Decks = BridgeDeckIndex.ForTile(t),
                });
            }
            return _samples = list;
        }

        private static string Where(Sample s, StreetProp p)
        {
            double lon, lat;
            WorldFrame.GameToLonLat(s.Id.X0 + p.X, s.Id.Z0 + p.Z, out lon, out lat);
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}{2} at {3:0.00000},{4:0.00000} base {5:0.00}", s.Id, p.Kind, p.Osm ? " (OSM)" : "", lon, lat, p.Y);
        }

        [Test]
        public void NothingStandsUnderADeckOrInTheRiverBed()
        {
            int lamps = 0, bad = 0;
            var examples = new List<string>();
            foreach (Sample s in Samples())
                foreach (StreetProp p in s.Props)
                {
                    if (PropPlacement.IsLampPost(p.Kind)) lamps++;
                    float dy, nx, ny, nz;
                    // The reviewer's measure: a deck more than 2 m above the base at the prop's position.
                    if (!s.Decks.TryDeck(s.Id.X0 + p.X, s.Id.Z0 + p.Z, p.Y + 30f, out dy, out nx, out ny, out nz) || dy - p.Y <= 2f) continue;
                    // An OSM object that fits under the deck on a road below it is fine (an underpass).
                    float under;
                    if (p.Osm && s.Decks.TryCeiling(s.Id.X0 + p.X, s.Id.Z0 + p.Z, p.Y + 0.1f, out under) && under > p.Y + p.HeightM) continue;
                    bad++;
                    if (examples.Count < 10) examples.Add(Where(s, p) + string.Format(CultureInfo.InvariantCulture, " deck {0:0.0}", dy));
                }
            TestContext.WriteLine("lamp posts " + lamps + ", under a deck " + bad);
            foreach (string e in examples) TestContext.WriteLine(e);
            Assert.That(lamps, Is.GreaterThan(5000), "the sample lights its streets");
            Assert.That(bad, Is.EqualTo(0), "props standing below a deck (in the river bed or on the bank under it)");
        }

        [Test]
        public void NoGeneratedPoleReachesADeckOverhead()
        {
            int bad = 0;
            foreach (Sample s in Samples())
                foreach (StreetProp p in s.Props)
                {
                    if (p.Osm) continue;
                    float under;
                    if (s.Decks.TryCeiling(s.Id.X0 + p.X, s.Id.Z0 + p.Z, p.Y + 0.1f, out under) && under < p.Y + p.HeightM)
                    {
                        bad++;
                        TestContext.WriteLine(Where(s, p) + " underside " + under);
                    }
                }
            Assert.That(bad, Is.EqualTo(0));
        }

        [Test]
        public void LampsOnDecksStandAtTheParapetLineOffTheCarriageway()
        {
            int onDecks = 0, onCarriageway = 0;
            foreach (Sample s in Samples())
                foreach (StreetProp p in s.Props)
                {
                    if (p.Osm || !PropPlacement.IsLampPost(p.Kind)) continue;
                    float dy, nx, ny, nz;
                    double gx = s.Id.X0 + p.X, gz = s.Id.Z0 + p.Z;
                    if (!s.Decks.TryDeck(gx, gz, p.Y + 0.5f, out dy, out nx, out ny, out nz) || Math.Abs(dy - p.Y) > 0.05f) continue;
                    onDecks++;
                    RoadSurfaceHit hit;
                    if (s.Surface.TrySample(gx, gz, p.Y + 0.2f, out hit) && hit.OnCarriageway && Math.Abs(hit.Y - p.Y) < 0.3f)
                    {
                        onCarriageway++;
                        TestContext.WriteLine("on the carriageway: " + Where(s, p));
                    }
                }
            TestContext.WriteLine("generated lamps on decks: " + onDecks);
            Assert.That(onCarriageway, Is.EqualTo(0));
        }

        [Test]
        public void GeneratedPropsSitOnTheDrawnSurfaceBesideThem()
        {
            int gen = 0, checkedProps = 0, off = 0;
            var c = CultureInfo.InvariantCulture;
            foreach (Sample s in Samples())
                foreach (StreetProp p in s.Props)
                {
                    if (p.Osm) continue;
                    gen++;
                    // Lamps on a deck stand on its walkway (the deck index's surface, which the road surface query does
                    // not hold on synthetic decks): checked by LampsOnDecksStandAtTheParapetLineOffTheCarriageway.
                    float dk, dnx, dny, dnz;
                    if (s.Decks.TryDeck(s.Id.X0 + p.X, s.Id.Z0 + p.Z, p.Y + 0.5f, out dk, out dnx, out dny, out dnz) && Math.Abs(dk - p.Y) < 0.05f) continue;
                    // Walk from the prop towards the road it serves (lamps and signals face it: +Z of the yaw) and compare
                    // its base with the first drawn surface met.
                    double yaw = p.YawDeg * Math.PI / 180, dx = Math.Sin(yaw), dz = Math.Cos(yaw);
                    if (StreetPropMesher.IsSignal(p.Kind)) dx = dz = 0;
                    for (double d = 0; d <= 4.0; d += 0.25)
                    {
                        RoadSurfaceHit hit;
                        double gx = s.Id.X0 + p.X + dx * d, gz = s.Id.Z0 + p.Z + dz * d;
                        if (!s.Surface.TrySample(gx, gz, p.Y + 0.5f, out hit)) continue;
                        if (!(hit.OnCarriageway || hit.OnFootpath || hit.OnShoulder || hit.OnMedian)) continue;
                        checkedProps++;
                        if (Math.Abs(hit.Y - p.Y) > PropPlacement.MaxOffSurfaceM + 0.1f)
                        {
                            off++;
                            if (off <= 10) TestContext.WriteLine(Where(s, p) + string.Format(c, " surface {0:0.00} at {1:0.00} m", hit.Y, d));
                        }
                        break;
                    }
                }
            TestContext.WriteLine(string.Format(c, "generated {0}, checked against a surface {1}, more than 1 m off {2}", gen, checkedProps, off));
            Assert.That(checkedProps, Is.GreaterThan(gen * 9 / 10), "generated props find their road");
            Assert.That(off, Is.EqualTo(0));
        }

        [Test]
        public void OsmRoadFurnitureLeavesTheCarriageway()
        {
            int furniture = 0, onRoad = 0;
            foreach (Sample s in Samples())
                foreach (StreetProp p in s.Props)
                {
                    if (!p.Osm) continue;
                    if (!StreetPropMesher.IsSignal(p.Kind) && p.Kind != StreetPropKind.BusStop && p.Kind != StreetPropKind.TaxiStand &&
                        !PropPlacement.IsLampPost(p.Kind) && p.Kind != StreetPropKind.Bench && p.Kind != StreetPropKind.BusShelter) continue;
                    furniture++;
                    RoadSurfaceHit hit;
                    if (s.Surface.TrySample(s.Id.X0 + p.X, s.Id.Z0 + p.Z, p.Y + 0.2f, out hit) && hit.OnCarriageway && Math.Abs(hit.Y - p.Y) < 0.3f &&
                        (hit.Road < 0 || RoadWidthModel.IsMotor(s.Tile.Roads[hit.Road].RoadClass)))
                    {
                        onRoad++;
                        TestContext.WriteLine("still on the carriageway: " + Where(s, p));
                    }
                }
            TestContext.WriteLine("OSM road furniture " + furniture + ", on a motor carriageway " + onRoad);
            Assert.That(furniture, Is.GreaterThan(100));
            Assert.That(onRoad, Is.LessThanOrEqualTo(furniture / 100), "at most 1% (no free spot within 10 m) stay where mapped");
        }

        [Test]
        public void HeritageStreetsGetLanternLampsAndSomeRoadsSolarLamps()
        {
            int heritage = 0, solar = 0, grid = 0;
            var streets = new HashSet<string>();
            foreach (Sample s in Samples())
                foreach (StreetProp p in s.Props)
                {
                    if (p.Osm) continue;
                    if (p.Kind == StreetPropKind.StreetLampHeritage)
                    {
                        heritage++;
                        Assert.That(p.HeightM, Is.InRange(5.7f, 6.2f));
                    }
                    else if (p.Kind == StreetPropKind.StreetLampSolar)
                    {
                        solar++;
                        Assert.That(p.HeightM, Is.InRange(7.2f, 8.4f));
                    }
                    else if (p.Kind == StreetPropKind.StreetLamp) grid++;
                }
            foreach (Sample s in Samples())
                foreach (RoadRecord r in s.Tile.Roads)
                    if (PropPlacement.IsHeritageStreet(s.Tile, r)) streets.Add(s.Tile.Name(r.NameRef).Display(false));
            TestContext.WriteLine("heritage " + heritage + " on " + string.Join(", ", streets) + "; solar " + solar + "; grid " + grid);
            Assert.That(streets, Has.Member("New Road").Or.Member("Durbar Marg"), "the sample holds the heritage streets");
            Assert.That(heritage, Is.GreaterThan(10));
            Assert.That(solar, Is.InRange(grid / 5, grid), "about a third of the lit roads are solar");
        }

        [Test]
        public void PlacementIsDeterministicAndKeepsEveryOsmObject()
        {
            foreach (Sample s in Samples().Take(6))
            {
                var again = new List<StreetProp>();
                PropPlacement.Place(s.Tile, s.Heights, again);
                Assert.That(again.Count, Is.EqualTo(s.Props.Count));
                for (int i = 0; i < again.Count; i++)
                {
                    Assert.That(again[i].X, Is.EqualTo(s.Props[i].X));
                    Assert.That(again[i].Y, Is.EqualTo(s.Props[i].Y));
                    Assert.That(again[i].YawDeg, Is.EqualTo(s.Props[i].YawDeg));
                    Assert.That(again[i].Kind, Is.EqualTo(s.Props[i].Kind));
                }
                // Every mappable OSM object but signals (one per approach of their junction), gates too narrow for
                // their drawn road and road furniture with no free spot off the drawn carriageway within 10 m (at most
                // 1 %: a lamp mapped on a galli junction where the houses meet the lane).
                int mappable = s.Tile.Props.Count(p => { StreetPropKind k; return PropPlacement.KindOf(p.Kind, out k) && k != StreetPropKind.TrafficSignal; });
                int gates = s.Tile.Props.Count(p => p.Kind == ObjectKind.Gate);
                int kept = s.Props.Count(p => p.Osm && !StreetPropMesher.IsSignal(p.Kind));
                Assert.That(kept, Is.InRange(mappable - gates - Math.Max(1, mappable / 100), mappable));
            }
        }
    }
}
