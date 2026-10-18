using System;
using System.Collections.Generic;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing.Bridges;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #2 and #15 (docs/W2_PLAYTEST_1.md) on the committed sample region, with the ground query the world
    /// makes (<see cref="StreamingScene.Ground"/>: the bridges package's decks and solids, the roads package's corridors)
    /// over the sample tiles, and the real walker and motorbike (<see cref="ArcadeVehicle"/>): the riverside roads of the
    /// Dhobi Khola corridor pass under Ratopul, Setopul, Kalopul and Bhatkeko Pul, and the foot overbridges are climbed
    /// from a stair foot on one side of the road, over the deck, down to the other side. The drawn-world probes
    /// (<see cref="BridgeCrossingTests"/>, <see cref="BridgeOverbridgeClimbTests"/>) cover every structure, explicitly.
    /// </summary>
    public class BridgeSampleRideTests
    {
        private const float Dt = 1f / 60f;

        private static readonly Dictionary<string, TileGroundQuery> Grounds = new Dictionary<string, TileGroundQuery>();

        /// <summary>The world's ground query over the sample tiles within one tile of the given ones (cached).</summary>
        private static TileGroundQuery Ground(IEnumerable<TileId> centres)
        {
            var want = new SortedSet<ulong>();
            var ids = new Dictionary<ulong, TileId>();
            foreach (TileId c in centres)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var id = new TileId(c.Level, c.Tx + dx, c.Ty + dy);
                if (StreamingSampleRegion.Tile(id) == null) continue;
                want.Add(id.Key);
                ids[id.Key] = id;
            }
            var key = new StringBuilder();
            foreach (ulong k in want) key.Append(k).Append(',');
            lock (Grounds)
            {
                TileGroundQuery g;
                if (Grounds.TryGetValue(key.ToString(), out g)) return g;
                g = StreamingScene.Ground(StreamingScene.Settings(2, 10));
                foreach (ulong k in want) g.Add(StreamingSampleRegion.Tile(ids[k]));
                Grounds[key.ToString()] = g;
                return g;
            }
        }

        /// <summary>Follows waypoints (world metres) with the stick towards the next one, as the player steers: true when
        /// the last is reached within <paramref name="seconds"/>; the highest feet and whether a deck ever carried it.</summary>
        private static bool Follow(TileGroundQuery g, VehicleSpec spec, IList<double> pts, float throttle, float startY, float seconds, out float maxY,
                                   out bool onDeck, out string stop)
        {
            var v = new ArcadeVehicle(spec);
            float heading = (float)Math.Atan2(pts[2] - pts[0], pts[3] - pts[1]);
            v.Teleport(pts[0], pts[1], heading, g);
            GroundSample s0;
            if (!float.IsNaN(startY) && g.TrySample(pts[0], pts[1], startY, out s0)) v.Y = s0.Height;
            maxY = v.Y;
            onDeck = false;
            stop = "";
            double reach = spec.TurnInPlace ? 0.8 : 2.5;
            int target = 1, n = pts.Count / 2;
            double lastX = v.X, lastZ = v.Z;
            float idle = 0f;
            for (int i = 0; i < (int)(seconds / Dt); i++)
            {
                double dx = pts[2 * target] - v.X, dz = pts[2 * target + 1] - v.Z;
                while (target < n - 1)
                {
                    double ux = pts[2 * target] - pts[2 * target - 2], uz = pts[2 * target + 1] - pts[2 * target - 1];
                    if (dx * dx + dz * dz > reach * reach && dx * ux + dz * uz >= 0) break;
                    target++;
                    dx = pts[2 * target] - v.X;
                    dz = pts[2 * target + 1] - v.Z;
                }
                if (target == n - 1 && dx * dx + dz * dz < reach * reach) return true;
                v.Step(new DriveInput(throttle, 0f, v.SteerToward((float)Math.Atan2(dx, dz), Dt)), Dt, g, 0f);
                maxY = Math.Max(maxY, v.Y);
                onDeck |= v.Ground.OnDeck;
                double px = v.X - lastX, pz = v.Z - lastZ;
                if (px * px + pz * pz > 1.0)
                {
                    lastX = v.X;
                    lastZ = v.Z;
                    idle = 0f;
                }
                else if ((idle += Dt) > 4f)
                {
                    break;
                }
            }
            stop = "stopped at (" + v.X.ToString("F1") + ", " + v.Z.ToString("F1") + ") y " + v.Y.ToString("F2");
            return false;
        }

        /// <summary>
        /// Playtest #2: "the same bridge acts as a overpass in areas like ratopul, kalopul, setopul ... none of those
        /// underpass works. The bridge wont let us pass through." Every road the sample's Dhobi Khola bridges cross (the
        /// riverside corridor roads in their cuttings, each named bridge's crossings) is walked and ridden under the
        /// bridge, 30 m to each side, never carried onto the deck. Before the fix the corridor roads met invisible walls
        /// as high as the cutting is deep at their joints (the ground query's hard edge of a lowered road past a
        /// segment's end) under Kalopul and Setopul, and the road-based retaining walls of the approach cuttings stood
        /// across them where a side street joins.
        /// </summary>
        [Test]
        public void RiversideRoadsPassUnderTheDhobiKholaBridges()
        {
            var names = new[] { "कालो पुल", "सेतोपुल", "रातोपुल", "भत्केको पुल", "Kalopul" };
            var tiles = new List<TileId>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
                {
                    NameRecord n = t.Name(sp.Record.NameRef);
                    if (n != null && Array.IndexOf(names, n.Default) >= 0 && sp.Crossings.Count > 0)
                    {
                        tiles.Add(id);
                        break;
                    }
                }
            }
            Assert.That(tiles.Count, Is.GreaterThanOrEqualTo(3), "Kalopul, Setopul, Ratopul and Bhatkeko Pul are in the sample");
            TileGroundQuery g = Ground(tiles);
            var bad = new StringBuilder();
            int runs = 0;
            var seen = new HashSet<string>();
            foreach (TileId id in tiles)
            {
                TileData t = StreamingSampleRegion.Tile(id);
                foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
                {
                    NameRecord n = t.Name(sp.Record.NameRef);
                    if (n == null || Array.IndexOf(names, n.Default) < 0) continue;
                    foreach (BridgeCrossing c in sp.Crossings)
                    {
                        string key = sp.Record.OsmWayId + "/" + t.Roads[c.Road].OsmWayId;
                        if (!seen.Add(key)) continue;
                        List<double> line = BridgeProbe.RoadAround(t, c.Road, c.X, c.Z, 30, Math.Min(1.5, 0.3 * c.LowHalfM));
                        foreach (bool ride in new[] { false, true })
                        {
                            runs++;
                            float maxY;
                            bool deck;
                            string stop;
                            bool ok = Follow(g, ride ? VehicleSpec.Motorbike() : VehicleSpec.Walker(), line, ride ? 0.5f : 1f, c.LowY + 0.3f, 90f, out maxY, out deck, out stop);
                            if (!ok || deck)
                                bad.Append(n.Default).Append(" over ").Append(t.Roads[c.Road].OsmWayId).Append(ride ? " ride: " : " walk: ").Append(ok ? "carried onto the deck" : stop).Append('\n');
                        }
                    }
                }
            }
            TestContext.WriteLine(runs + " runs under the Dhobi Khola bridges");
            Assert.That(runs, Is.GreaterThanOrEqualTo(12));
            Assert.That(bad.ToString(), Is.Empty, "passing under the bridges");
        }
    }
}
