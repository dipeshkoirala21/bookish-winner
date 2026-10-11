using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The data package's road structures (RSTR, docs/DATA_FORMATS.md 1.4 and 1.15) as the merged consumers read them on
    /// the sample region: a grade-separated crossing whose station the pipeline writes into both ways (a deck and the
    /// road under it hold a point at the same position, which is no node), and roads with heights on part of their
    /// length (NaN at the draped points).
    /// </summary>
    public class StructureCrossingTests
    {
        private const ulong ThapathaliDeck = 52916461UL, BankRoad = 751465485UL, RatnaParkOverbridge = 112448812UL, LoweredPrimary = 171603746UL;

        private static int RoadOf(TileData t, ulong way)
        {
            for (int i = 0; i < t.Roads.Count; i++)
                if (t.Roads[i].OsmWayId == way) return i;
            return -1;
        }

        /// <summary>
        /// The Thapathali bridge deck and the Bagmati bank road lowered under it share a point at the crossing, 6 m apart
        /// in height: the bridge keeps the clearance over the bank road there (a crossing, not a junction), and no lane
        /// turns from the deck down onto the bank road or up from it (no lane graph node at the crossing).
        /// </summary>
        [Test]
        public void CoincidentCrossingStationsAreCrossingsNotJunctions()
        {
            TileData t = StreamingSampleRegion.Tile(new TileId(10, 517, 159));
            int deck = RoadOf(t, ThapathaliDeck), bank = RoadOf(t, BankRoad);
            Assert.That(deck, Is.GreaterThanOrEqualTo(0));
            Assert.That(bank, Is.GreaterThanOrEqualTo(0));
            int[] p = t.Roads[deck].Points, q = t.Roads[bank].Points;
            int a = -1, b = -1;
            for (int i = 1; i + 1 < p.Length / 2 && a < 0; i++)
            for (int j = 1; j + 1 < q.Length / 2; j++)
            {
                if (p[2 * i] != q[2 * j] || p[2 * i + 1] != q[2 * j + 1]) continue;
                a = i;
                b = j;
                break;
            }
            Assert.That(a, Is.GreaterThan(0), "the pipeline wrote the crossing station into both ways");
            float upper, lower;
            Assert.That(t.RoadStructureOf(deck).TryHeightAt(a, out upper), Is.True);
            Assert.That(t.RoadStructureOf(bank).TryHeightAt(b, out lower), Is.True);
            Assert.That(upper - lower, Is.GreaterThan(RoadStructureRecord.GradeSeparationM));
            Assert.That(BridgeStructures.PassesThroughAt(p, a, q, b), Is.True);

            BridgeLayout layout = BridgeLayout.For(t);
            bool crossing = false;
            foreach (BridgeSpan sp in layout.Spans)
            {
                if (sp.Record.OsmWayId != ThapathaliDeck) continue;
                foreach (BridgeCrossing c in sp.Crossings)
                    if (t.Roads[c.Road].OsmWayId == BankRoad) crossing = true;
            }
            Assert.That(crossing, Is.True, "the deck keeps the clearance over the bank road");

            LaneGraph g = TrafficData.Graph;
            int deckLanes = 0, links = 0;
            foreach (LaneId l in g.AllLanes())
            {
                LaneInfo li = g.Info(l);
                if (li.WayId != (long)ThapathaliDeck && li.WayId != (long)BankRoad) continue;
                if (li.WayId == (long)ThapathaliDeck) deckLanes++;
                for (int i = 0; i < li.SuccessorCount; i++)
                {
                    LaneInfo c = g.Info(g.Successor(l, i));
                    for (int j = 0; j < c.SuccessorCount; j++)
                    {
                        long w = g.Info(g.Successor(c.Id, j)).WayId;
                        if (w != li.WayId && (w == (long)ThapathaliDeck || w == (long)BankRoad)) links++;
                    }
                }
            }
            Assert.That(deckLanes, Is.GreaterThan(0));
            Assert.That(links, Is.EqualTo(0), "lanes between the deck and the bank road under it");
        }

        /// <summary>
        /// The Ratna Park foot overbridge crosses the lowered primary road at a station both ways hold (a crossing now,
        /// kept clear) and then turns to run beside it: its west end stands 12 m from the road's centreline, outside the
        /// widened corridor in plan though inside the overlap stretch a straight deck would have. The end is not carried
        /// on onto the pavement beside the road (a flight there stood in that footway).
        /// </summary>
        [Test]
        public void OverbridgeEndTurnedBesideTheRoadIsNotExtended()
        {
            TileData t = StreamingSampleRegion.Tile(new TileId(10, 517, 161));
            BridgeLayout layout = BridgeLayout.For(t);
            BridgeSpan sp = layout.Spans.Find(s => s.Record.OsmWayId == RatnaParkOverbridge);
            Assert.That(sp, Is.Not.Null);
            Assert.That(sp.Overbridge, Is.True);
            BridgeCrossing c = sp.Crossings.Find(x => t.Roads[x.Road].OsmWayId == LoweredPrimary);
            Assert.That(c.HalfAlong, Is.GreaterThan(0), "the deck keeps the clearance over the lowered primary road");
            double alongEnd = sp.Path.End - sp.ExtEnd; // (the mapped end)
            Assert.That(alongEnd, Is.LessThan(c.S + c.HalfAlong), "the end lies inside the straight-deck overlap stretch");
            Assert.That(sp.ExtEnd, Is.EqualTo(0), "the turned end already stands outside the corridor");
        }

        /// <summary>
        /// A coincident point of two roads at one height stays a junction for the bridges (no crossing over a road the
        /// deck meets), and one where the other road only touches the deck (both its neighbours on one side) is no
        /// crossing either.
        /// </summary>
        [Test]
        public void PassesThroughOnlyWhereTheOtherRoadCrossesTheLine()
        {
            int[] deck = { 0, 0, 1000, 0, 2000, 0 };
            Assert.That(BridgeStructures.PassesThroughAt(deck, 1, new[] { 1000, -500, 1000, 0, 1000, 500 }, 1), Is.True);
            Assert.That(BridgeStructures.PassesThroughAt(deck, 1, new[] { 500, -500, 1000, 0, 1500, -500 }, 1), Is.False, "a touch from one side");
            Assert.That(BridgeStructures.PassesThroughAt(deck, 1, new[] { 1000, -500, 1000, 1, 1000, 500 }, 1), Is.False, "not the same point");
            Assert.That(BridgeStructures.PassesThroughAt(deck, 1, new[] { 1000, 0, 1000, 500 }, 0), Is.False, "an end of the other road");
        }

        /// <summary>
        /// Every road with structure heights on part of its length (ramps, stairs, cuttings) gives finite ground along its
        /// whole centreline, and every lane of the sample's lane graph has finite heights: a draped point reads the drape,
        /// a segment from a structure point to it blends into the ground (the ride from Thamel to Boudha stopped dead at
        /// a NaN road surface at Chabahil).
        /// </summary>
        [Test]
        public void PartlyStructuredRoadsHaveFiniteGroundAndLanes()
        {
            TileGroundQuery g = DrivingData.SampleGround(false);
            int roads = 0;
            long samples = 0;
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10) continue;
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    float[] y = t.RoadStructureOf(ri).DeckY;
                    if (y == null || !RoadSpatialIndex.HasDraped(y) || !RoadMesher.IsDrawn(t.Roads[ri], null)) continue;
                    roads++;
                    int first, last;
                    RoadSpatialIndex.RenderedRange(t.Roads[ri], out first, out last);
                    int[] p = t.Roads[ri].Points;
                    for (int k = first; k < last; k++)
                    {
                        double ax, az, bx, bz;
                        t.LocalToGame(p[2 * k], p[2 * k + 1], out ax, out az);
                        t.LocalToGame(p[2 * k + 2], p[2 * k + 3], out bx, out bz);
                        double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                        int n = Math.Max(1, (int)Math.Ceiling(len));
                        for (int i = 0; i < n; i++)
                        {
                            double f = i / (double)n, x = ax + (bx - ax) * f, z = az + (bz - az) * f;
                            float near = float.IsNaN(y[k]) ? (float.IsNaN(y[k + 1]) ? float.NaN : y[k + 1]) : y[k];
                            GroundSample s;
                            bool ok = float.IsNaN(near) ? g.TrySample(x, z, out s) : g.TrySample(x, z, near, out s);
                            if (!ok) continue;
                            samples++;
                            Assert.That(float.IsNaN(s.Height) || float.IsInfinity(s.Height), Is.False, t.Tile + " way " + t.Roads[ri].OsmWayId + " at point " + k);
                        }
                    }
                }
            }
            TestContext.WriteLine("partly structured roads " + roads + ", " + samples + " ground samples");
            Assert.That(roads, Is.GreaterThan(20));
            Assert.That(samples, Is.GreaterThan(1000));

            LaneGraph lanes = TrafficData.Graph;
            foreach (LaneId l in lanes.AllLanes())
            {
                LaneInfo li = lanes.Info(l);
                for (int k = 0; k < li.PointCount; k++)
                    Assert.That(float.IsNaN(li.Y[k]), Is.False, "lane of way " + li.WayId);
            }
        }
    }
}
