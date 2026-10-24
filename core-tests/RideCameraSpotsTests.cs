using System;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Routing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The spots the camera package's rides ran into (docs/research/w2/ref_camera.md §6, §7.1), on the camera rides' own
    /// ground query (<see cref="CameraRides.Ground"/>: every sample tile, Mid tier, deck and corridor factories) and with the
    /// camera's own bus autopilot: the bus blocked on Kantipath at the Ratna Park junction, the bus blocked turning into
    /// Bhaktithapa Road, and the "unidentified" overhead solid north of Thamel.
    /// </summary>
    public class RideCameraSpotsTests
    {
        private static void Game(double lon, double lat, out double x, out double z)
        {
            WorldFrame.LonLatToGame(lon, lat, out x, out z);
        }

        private static string Where(double x, double z)
        {
            double lon, lat;
            WorldFrame.GameToLonLat(x, z, out lon, out lat);
            return lon.ToString("0.00000") + ", " + lat.ToString("0.00000");
        }

        /// <summary>
        /// The camera package's blocked bus on Kantipath (ref_camera.md §6: "Ratna Park to Maitighar along Kantipath is
        /// blocked for a bus near Ratna Park"): at the Ratna Park junction under the four foot overbridges the data lowers
        /// every arm 4 m (RSTR Underpass, one height at the shared node), and the junction cap stood at the terrain, a
        /// 4.3 m wall across the arms (the bus stopped 94 m in, at 85.31613, 27.70623). The cap now follows its arms
        /// (<see cref="CapLevels"/>): the camera's own bus autopilot drives the route in the centre and 1.5 m left.
        /// </summary>
        [Test]
        public void TheBusRidesKantipathThroughTheRatnaParkJunction()
        {
            foreach (double left in new[] { 0.0, 1.5 })
            {
                double[] path = CameraRides.Route(Travel.Bus, 85.3153, 27.7066, 85.3207, 27.6946, left);
                RideStats stats;
                CameraRides.Drive(path, HandlingPreset.Bus, 8f, 150f, out stats);
                TestContext.WriteLine("bus Ratna Park-Maitighar, " + left + " m left: " + stats);
                Assert.That(stats.Stuck, Is.False, "the bus passes the Ratna Park junction, " + left + " m left: " + stats);
                Assert.That(stats.DrivenM, Is.GreaterThan(600.0), stats.ToString());
            }
        }

        /// <summary>
        /// The camera package's bus from Maitighar east along Tanka Prasad Ghumti Sadak, which turns into Bhaktithapa Road
        /// (7 m) at 85.32753, 27.69324 (ref_camera.md §6): the corner house there (OSM 1286599936) is solid exactly where it
        /// is drawn now (owner #14), and its corner stood where the pipeline trimmed it to the straight corridors, with no
        /// kerb return (the corner room clamp of the junction caps); the bus's front overhang swung 1.1 m into it and
        /// stopped. Bus corners (<see cref="RoadLayout.BusCornerRadiusM"/>: car roads both at least 6.5 m wide, at least
        /// 60° apart, outside the old cores) keep a 4.5 m kerb return, the corner house is trimmed back to it, and the
        /// camera's bus on the centre line drives on. Open (docs/research/w2/ref_ride.md §4): the camera package's bus 1.5 m
        /// left of the centre line still stops there. It keeps to the outside of the 46° bend Bhaktithapa Road takes at the
        /// node, its front swings out over the short 14 m arm of Ghumti Sadak (which bends 4.5 m from the node, so the cap
        /// has no room for a kerb return on that side), and it reaches 0.65 m into the corner house (with the house hidden)
        /// at that arm's right edge by its cut; a bus steered from its front axle stops there too.
        /// </summary>
        [Test]
        public void TheBusTurnsFromGhumtiSadakIntoBhaktithapaRoad()
        {
            double[] path = CameraRides.Route(Travel.Bus, 85.3240, 27.6950, 85.3340, 27.6930, 0.0);
            RideStats stats;
            CameraRides.Drive(path, HandlingPreset.Bus, 8f, 150f, out stats);
            TestContext.WriteLine("bus Maitighar-Baneshwor on the centre line: " + stats);
            Assert.That(stats.Stuck, Is.False, stats.ToString());
            Assert.That(stats.DrivenM, Is.GreaterThan(900.0), "past the Bhaktithapa Road corner (446 m): " + stats);
        }

        /// <summary>
        /// The "unidentified overhead solid" over the road north of Thamel (85.3130, 27.7178; ref_camera.md §7.1, the car's
        /// far view cut its boom there) is the foot overbridge at Lainchaur: OSM Footway 1268595053 (RSTR Flyover,
        /// FootOverbridge, layer 1) over Primary 176144636, drawn by the bridges package with railings. It is a real
        /// structure, not a stray solid: the lowest thing over the drawn road under it is that deck's slab in the road index
        /// (<see cref="RoadSpatialIndex.SlabOverlaps"/>, the deck surface down to <see cref="RoadSpatialIndex.DeckThicknessM"/>),
        /// 4.40 m over the carriageway at its lowest. Open (ref_ride.md §4): the slab takes the vehicle deck depth (1.2 m)
        /// where the data and the drawn foot deck are 0.6 m deep (<see cref="RoadStructureRecord.FootDeckDepthM"/>), which
        /// would leave 4.99 m; with that depth the camera package's chase boom snaps 1.75 m on its car ride under this bridge
        /// (CharactersCameraRideTests), so the change waits for the camera and bridges packages.
        /// </summary>
        [Test]
        public void TheOverheadSolidNorthOfThamelIsTheLainchaurFootOverbridge()
        {
            TileGroundQuery g = CameraRides.Ground;
            double x, z;
            Game(85.3130, 27.7178, out x, out z);
            GroundSample top;
            Assert.That(g.TrySample(x, z, float.PositiveInfinity, out top), Is.True);
            Assert.That(top.OnDeck, Is.True, "a deck over the road");
            TileId id = TileId.At(10, x, z);
            TileData t = DrivingData.SampleTiles()[id];
            int bridge = -1, road = -1;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (t.Roads[i].OsmWayId == 1268595053UL) bridge = i;
                if (t.Roads[i].OsmWayId == 176144636UL) road = i;
            }
            Assert.That(bridge, Is.GreaterThanOrEqualTo(0), "the foot overbridge way is in the tile");
            Assert.That(road, Is.GreaterThanOrEqualTo(0), "the road under it is in the tile");
            RoadStructureRecord st = t.RoadStructureOf(bridge);
            Assert.That(st.Kind, Is.EqualTo(RoadStructureKind.Flyover));
            Assert.That(st.Has(RoadStructureFlags.FootOverbridge), Is.True);
            Assert.That(RoadMesher.IsDrawn(t, bridge, null), Is.True);
            // Along the road under the overbridge (±12 m of the crossing): the lowest thing over the drawn surface, and the
            // foot deck's surface over the same point.
            RoadLayout layout = RoadLayout.For(t);
            RoadCentreline c = layout.Centres[road];
            int seg;
            double along, lateral, dist;
            Assert.That(c.Nearest(x - t.Tile.X0, z - t.Tile.Z0, out seg, out along, out lateral, out dist), Is.True);
            double lowest = double.MaxValue, deckOver = 0;
            string where = "";
            for (double s = along - 12; s <= along + 12; s += 0.5)
            for (double off = -3; off <= 3; off += 1.5)
            {
                double cx, cz, tx, tz;
                c.At(s, out cx, out cz, out tx, out tz);
                double px = t.Tile.X0 + cx - tz * off, pz = t.Tile.Z0 + cz + tx * off;
                GroundSample s0, deck;
                if (!g.TrySample(px, pz, float.NegativeInfinity, out s0) || s0.OnDeck) continue;
                double hit;
                // A 0.3 m sphere cast straight up from 0.5 m over the road: whatever it meets first.
                if (g.SphereCast(px, s0.Height + 0.5, pz, 0, 1, 0, 0.3, 10.0, out hit) && hit + 0.8 < lowest &&
                    g.TrySample(px, pz, float.PositiveInfinity, out deck) && deck.OnDeck)
                {
                    lowest = hit + 0.8;
                    deckOver = deck.Height - s0.Height;
                    where = Where(px, pz);
                }
            }
            TestContext.WriteLine("lowest structure over Primary 176144636 under the overbridge: " + lowest.ToString("0.00") + " m at " + where +
                                  ", the foot deck's surface " + deckOver.ToString("0.00") + " m over the road there");
            Assert.That(lowest, Is.LessThan(8.0), "the overbridge is over this road");
            // Nothing but the deck's slab: the lowest obstacle is its underside in the road index (within the 0.3 m sphere's
            // march step), and the drawn deck (0.6 m deep) clears the contract's 4.5 m.
            Assert.That(lowest, Is.EqualTo(deckOver - RoadSpatialIndex.DeckThicknessM).Within(0.15), "the deck slab, nothing lower, at " + where);
            Assert.That(deckOver - RoadStructureRecord.FootDeckDepthM, Is.GreaterThanOrEqualTo(RoadClearance.MinOverheadClearanceM), "the drawn foot deck's underside");
        }
    }
}
