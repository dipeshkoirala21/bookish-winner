using System;
using System.Collections.Generic;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Streaming;
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
                // As the streamer adds them on High (the owner's tier): each exact tile with the terrain step its meshes use.
                g = StreamingScene.Ground(StreamingScene.Settings(2, 10));
                StreamingConfig config = StreamingConfig.ForTier(2);
                foreach (ulong k in want)
                {
                    TileData t = StreamingSampleRegion.Tile(ids[k]);
                    g.Add(t, config.TerrainStep(ids[k], ids[k], t.HeightsN));
                }
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
    
        /// <summary>The tiles of the sample that hold a span matching <paramref name="want"/>.</summary>
        private static List<TileId> TilesWith(Func<BridgeSpan, bool> want)
        {
            var tiles = new List<TileId>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                if (BridgeLayout.For(t).Spans.Exists(sp => want(sp))) tiles.Add(id);
            }
            return tiles;
        }

        /// <summary>Most the walker's feet may lie under the walking surface of a deck or flight it is on or beside
        /// (the owner's "sucked in").</summary>
        private const float SinkM = 0.15f;

        /// <summary>How far the feet at (x, y, z) lie under a deck or flight surface of the bridges package within two
        /// metres above them (0 when none): a body inside the structure.</summary>
        private static float DeckSink(TileGroundQuery g, double x, double z, float y)
        {
            TileId c = TileId.At(10, x, z);
            float worst = 0f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                TileData t = StreamingSampleRegion.Tile(new TileId(10, c.Tx + dx, c.Ty + dy));
                if (t == null || !t.HasDetail) continue;
                float d, nx, ny, nz;
                if (!BridgeDeckIndex.ForTile(t).TryDeck(x, z, y + 1.0f, out d, out nx, out ny, out nz)) continue;
                if (d - y > worst && d - y < 2.0f) worst = d - y;
            }
            return worst;
        }

        /// <summary>
        /// Playtest #15: "Overhead sidewalks do not work. I tried crossing the road using that, and i could not even reach
        /// the top." Every flight of every stair of the sample (the foot overbridges over Kanti Path, Durbar Marg, the Ring
        /// Road and the other arterials, the stairs of the foot bridges over the rivers) is climbed by the real walker
        /// from its last tread to 0.6 m past its top, onto whatever continues it there (the deck, a landing, the next
        /// flight), and walked down again to 0.6 m past its foot; the feet never sink into a deck or flight. Before this
        /// round the road ribbons drawn over the flights at the ways' structure heights (a ramp the ground query
        /// followed past the drawn foot), deck end extensions inside the houses beside the pavement, footprint walls of
        /// those houses across the flights, landings over the flights below them, and the wedge of a deck over the steps
        /// in its middle stopped the walker on 44 of 135 flights (60 of 270 runs). The flights of
        /// <see cref="StairDataQuirks"/> are reported, not asserted: their mapped heights put them under a deck.
        /// </summary>
        [Test]
        public void EveryStairFlightIsClimbedAndDescended()
        {
            List<TileId> tiles = TilesWith(sp => sp.Stairs.Exists(st => st.Steps > 0));
            Assert.That(tiles.Count, Is.GreaterThanOrEqualTo(4), "the sample has foot overbridges with stairs");
            TileGroundQuery g = Ground(tiles);
            var bad = new StringBuilder();
            var quirks = new StringBuilder();
            int flights = 0, overbridgeFlights = 0, quirkRuns = 0;
            foreach (TileId id in tiles)
            {
                TileData t = StreamingSampleRegion.Tile(id);
                BridgeLayout l = BridgeLayout.For(t);
                var overbridges = new HashSet<ulong>();
                foreach (BridgeSpan sp in l.Spans)
                    if (sp.Overbridge)
                        overbridges.Add(sp.GroupWay);
                foreach (BridgeSpan sp in l.Spans)
                    for (int i = 0; i < sp.Stairs.Count; i++)
                    {
                        BridgeStair st = sp.Stairs[i];
                        if (st.Steps <= 0) continue;
                        flights++;
                        if (overbridges.Contains(sp.GroupWay)) overbridgeFlights++;
                        foreach (bool up in new[] { true, false })
                        {
                            List<double> line = BridgeProbe.Flight(st, t.Tile, up);
                            float start = (up ? st.SurfaceAt(st.Length - 0.25) : st.SurfaceAt(0.25)) + 0.05f;
                            float maxY, minY, sink;
                            string stop;
                            bool ok = Walk(g, line, start, out maxY, out minY, out sink, out stop);
                            // (Reaching the end point 0.6 m past the flight is the test; the height only shows the walker did
                            // not walk round the flight: up it rises to within half a metre of the top.)
                            bool topped = !up || maxY > st.TopY - 0.5f;
                            if (!ok || !topped || sink > SinkM)
                            {
                                bool quirk = StairDataQuirks.ContainsKey(sp.Record.OsmWayId);
                                (quirk ? quirks : bad).Append(t.Tile).Append(' ').Append(sp.Record.OsmWayId).Append(" flight ").Append(i).Append(up ? " up: " : " down: ")
                                   .Append(!ok ? stop : !topped ? "never reached the " + (up ? "top" : "foot") : "sank " + sink.ToString("F2")).Append('\n');
                                if (quirk) quirkRuns++;
                            }
                        }
                    }
            }
            TestContext.WriteLine(flights + " flights, " + overbridgeFlights + " of foot overbridges");
            if (quirks.Length > 0) TestContext.WriteLine("known data quirks (" + quirkRuns + " runs):\n" + quirks);
            Assert.That(overbridgeFlights, Is.GreaterThanOrEqualTo(40));
            Assert.That(bad.ToString(), Is.Empty, "climbing and descending every stair flight");
            Assert.That(quirkRuns, Is.LessThanOrEqualTo(8), "the known data quirks do not spread");
        }

        /// <summary>
        /// Playtest #15 end to end: "I tried crossing the road using that, and i could not even reach the top." Every
        /// foot overbridge of the sample (Kanti Path at Jamal and Bhadrakali, Durbar Marg, Putalisadak, New Baneshwor,
        /// Tripureshwor, Chabahil, the Ring Road) is crossed by the real walker the way the player does it: from the
        /// pavement beyond the foot of a stair on one side of the road, up every flight and landing, along the deck at its
        /// height over the road and down a stair on the other side to the pavement (the route over the structure's own
        /// pieces, <see cref="OverbridgeRoutes"/>, on the streamer's ground).
        /// </summary>
        [Test]
        public void EveryFootOverbridgeIsCrossedFootToFoot()
        {
            List<TileId> tiles = TilesWith(sp => sp.Overbridge);
            TileGroundQuery g = Ground(tiles);
            var bad = new StringBuilder();
            int structures = 0, crossed = 0, fallbacks = 0;
            foreach (TileId id in tiles)
            {
                TileData t = StreamingSampleRegion.Tile(id);
                foreach (OverbridgeRoutes.Route r in OverbridgeRoutes.For(t, g))
                {
                    structures++;
                    if (r.Waypoints == null)
                    {
                        bad.Append(t.Tile).Append(" overbridge ").Append(r.Way).Append(": no route from a foot to a foot (").Append(r.Note).Append(")\n");
                        continue;
                    }
                    // The longest route between two stair feet first; where a stair of the structure is one of the
                    // known data quirks (StairDataQuirks: its mapped heights put it under another flight), the next
                    // route crosses the road too, as a player picks the other stair.
                    var tried = new StringBuilder();
                    bool any = false;
                    for (int c = -1; c < r.Alternatives.Count && !any; c++)
                    {
                        List<double> pts = c < 0 ? r.Waypoints : r.Alternatives[c];
                        float startY = c < 0 ? r.StartY : r.AlternativeStartY[c];
                        float maxY, minY, sink;
                        string stop;
                        bool ok = Walk(g, pts, startY + 0.05f, out maxY, out minY, out sink, out stop);
                        bool topped = maxY > r.DeckY - 0.3f;
                        any = ok && topped && sink <= SinkM;
                        if (!any)
                            tried.Append("\n      ").Append(c < 0 ? r.Note : r.AlternativeNotes[c]).Append(": ")
                                 .Append(!ok ? stop : !topped ? "never reached the deck at " + r.DeckY.ToString("F2") : "sank " + sink.ToString("F2"));
                        else if (c >= 0) fallbacks++;
                        if (any && c >= 0) TestContext.WriteLine(t.Tile + " overbridge " + r.Way + " crossed on route " + (c + 2) + " (" + r.AlternativeNotes[c] + "); not on:" + tried);
                    }
                    if (any)
                    {
                        crossed++;
                        continue;
                    }
                    bad.Append(t.Tile).Append(" overbridge ").Append(r.Way).Append(':').Append(tried).Append('\n');
                }
            }
            TestContext.WriteLine(structures + " foot overbridges, " + crossed + " crossed");
            Assert.That(structures, Is.GreaterThanOrEqualTo(15), "the sample's foot overbridges");
            Assert.That(bad.ToString(), Is.Empty, "crossing every foot overbridge");
            // (Only the overbridge 1174960075 at the sidewalk hump 1174960072, StairDataQuirks, needs its second route.)
            Assert.That(fallbacks, Is.LessThanOrEqualTo(1), "overbridges crossed only on another route");
        }

        /// <summary>
        /// Ways whose mapped structure heights leave their flights under a deck or across another flight: a fault of the
        /// heights the data package derives, not of the layout (each is an open issue for it). Sidewalks sharing a node with
        /// the foot of a stair or the end of a deck get that node's height, so a footway along the street climbs over the
        /// stair's foot in a hump of flights that runs under the deck or the stair beside it.
        /// </summary>
        private static readonly Dictionary<ulong, string> StairDataQuirks = new Dictionary<ulong, string>
        {
            // The sidewalk 1429946088 passes through the joint of the stairs of the overbridge 268851407 at 1288.30, 2.4 m
            // over the street, so this 1.2 m way from that joint up to the deck is extended down across its flights.
            { 1429946087, "joint of a sidewalk hump" },
            // The sidewalk passes through the end node of the overbridge 112449571 at the deck's height; its flights,
            // moved with the extended deck end, lie under the deck's edge.
            { 301779943, "sidewalk hump through a deck end" },
            // The foot node of the steps 1174960074 is mapped 1.5 m over the street; this sidewalk leaves it 28 degrees
            // off the steps, its flight under theirs.
            { 1174960072, "sidewalk hump at a raised stair foot" },
            // A footway crossing the end of the road bridge 1540981400, its steps mapped to tops 1.3 m under the deck and
            // 2.5 m inside its edge.
            { 1368711450, "steps mapped under a road deck" },
        };

        /// <summary>The walker along waypoints (world metres) from feet height <paramref name="startY"/>: true when it
        /// arrives; the highest and lowest feet and the worst sink under a deck or flight (<see cref="DeckSink"/>).</summary>
        private static bool Walk(TileGroundQuery g, IList<double> pts, float startY, out float maxY, out float minY, out float sink, out string stop)
        {
            return Ride(g, VehicleSpec.Walker(), 1f, pts, startY, 60f, out maxY, out minY, out sink, out stop);
        }

        private static bool Ride(TileGroundQuery g, VehicleSpec spec, float throttle, IList<double> pts, float startY, float seconds, out float maxY, out float minY,
                                 out float sink, out string stop)
        {
            var v = new ArcadeVehicle(spec);
            float heading = (float)Math.Atan2(pts[2] - pts[0], pts[3] - pts[1]);
            v.Teleport(pts[0], pts[1], heading, g);
            GroundSample s0;
            if (!float.IsNaN(startY) && g.TrySample(pts[0], pts[1], startY, out s0)) v.Y = s0.Height;
            maxY = minY = v.Y;
            sink = 0f;
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
                bool slow = !spec.TurnInPlace && v.SpeedMps > CornerMps && SharpBendAhead(pts, target, v.X, v.Z);
                v.Step(new DriveInput(slow ? 0f : throttle, slow ? 1f : 0f, v.SteerToward((float)Math.Atan2(dx, dz), Dt)), Dt, g, 0f);
                maxY = Math.Max(maxY, v.Y);
                minY = Math.Min(minY, v.Y);
                if (!v.Airborne) sink = Math.Max(sink, DeckSink(g, v.X, v.Z, v.Y));
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
            stop = "stopped at (" + v.X.ToString("F1") + ", " + v.Z.ToString("F1") + ") y " + v.Y.ToString("F2") + Ahead(g, v, pts[2 * target], pts[2 * target + 1]);
            return false;
        }

        /// <summary>The speed a rider takes a sharp bend at (18 km/h): the crossings follow the road's centreline through
        /// the bends at a bridge head (the Bishnumati bridge's approach turns 70 degrees onto the deck), and a rider
        /// does not take those at the half-throttle cruise of the straight.</summary>
        private const float CornerMps = 5f;

        /// <summary>The waypoints within the next 12 m (from the target on) turn by more than 50 degrees from the way the
        /// rider is heading to the target.</summary>
        private static bool SharpBendAhead(IList<double> pts, int target, double x, double z)
        {
            int n = pts.Count / 2;
            double hx = pts[2 * target] - x, hz = pts[2 * target + 1] - z, hl = Math.Sqrt(hx * hx + hz * hz);
            if (hl < 1e-6) return false;
            double ahead = hl;
            for (int k = target; k + 1 < n && ahead < 12.0; k++)
            {
                double ux = pts[2 * k + 2] - pts[2 * k], uz = pts[2 * k + 3] - pts[2 * k + 1], ul = Math.Sqrt(ux * ux + uz * uz);
                if (ul < 1e-6) continue;
                if ((hx * ux + hz * uz) / (hl * ul) < 0.64) return true;
                ahead += ul;
            }
            return false;
        }

        /// <summary>What lies 0.3 m ahead of a stopped body towards (tx, tz) (diagnostics for the failure messages).</summary>
        private static string Ahead(TileGroundQuery g, ArcadeVehicle v, double tx, double tz)
        {
            double dx = tx - v.X, dz = tz - v.Z, l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-6) return "";
            double ax = v.X + dx / l * 0.3, az = v.Z + dz / l * 0.3;
            GroundSample s;
            bool got = g.TrySample(ax, az, v.Y, out s);
            float d = 0f, h = 0f, nx, ny, nz;
            bool feet = false, head = false;
            TileData t = StreamingSampleRegion.Tile(TileId.At(10, ax, az));
            if (t != null && t.HasDetail)
            {
                BridgeDeckIndex di = BridgeDeckIndex.ForTile(t);
                feet = di.TryDeck(ax, az, v.Y, out d, out nx, out ny, out nz);
                head = di.TryDeck(ax, az, v.Y + 1.2f, out h, out nx, out ny, out nz);
            }
            return " [ahead (" + ax.ToString("F2") + "," + az.ToString("F2") + ") " + (got ? s.Height.ToString("F2") + " ny " + s.Ny.ToString("F2") + (s.OnDeck ? " deck" : "") : "none") + " blocked " + g.IsBlocked(ax, az, v.Y) + " by " + g.BlockedBy(ax, az, v.Y)
                   + " solid " + g.InsideSolid(ax, az, v.Y) + " deck@feet " + (feet ? d.ToString("F2") : "-") + " deck@head " + (head ? h.ToString("F2") : "-") + "]";
        }

        /// <summary>
        /// Playtest #2: "Bridges is not walkable or drivable at all. Characters gets sucked in. Character basically
        /// disappears when trying to cross the bridge." Every river bridge and flyover of the sample with a deck at least
        /// 15 m long and 1.5 m over the ground under it (the Bagmati and Bishnumati bridges, Kalopul, Setopul, Ratopul,
        /// Thapathali, the Ring Road bridges and the Prithvi Path bridge at Tripureshwor that the owner's screenshot shows)
        /// is walked and ridden across along the road it carries, from 1.5 m before the deck to 1.5 m beyond it in the left
        /// lane: it is crossed and the feet never sink into the deck.
        /// </summary>
        [Test]
        public void RiverBridgesAndFlyoversAreWalkedAndRiddenAcross()
        {
            Func<BridgeSpan, bool> want = sp =>
            {
                if (sp.Stair || sp.Overbridge || sp.Foot || sp.Length < 15) return false;
                float rise = 0f;
                for (int k = 0; k < sp.Count; k++) rise = Math.Max(rise, sp.Path.Y[k] - sp.Ground[k]);
                return rise >= 1.5f;
            };
            List<TileId> tiles = TilesWith(want);
            TileGroundQuery g = Ground(tiles);
            var bad = new StringBuilder();
            int spans = 0, runs = 0;
            foreach (TileId id in tiles)
            {
                TileData t = StreamingSampleRegion.Tile(id);
                foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
                {
                    if (!want(sp)) continue;
                    spans++;
                    int mid = sp.Count / 2;
                    double lane = 0.5 * Math.Min(sp.HalfL[mid], sp.HalfR[mid]);
                    List<double> across = BridgeProbe.RoadAround(t, sp.Road, sp.Path.X[mid], sp.Path.Z[mid], 0.5 * sp.Length + 1.5, lane);
                    foreach (bool ride in new[] { false, true })
                    {
                        runs++;
                        float maxY, minY, sink;
                        string stop;
                        bool ok = Ride(g, ride ? VehicleSpec.Motorbike() : VehicleSpec.Walker(), ride ? 0.5f : 1f, across, float.NaN, 120f, out maxY, out minY, out sink, out stop);
                        if (!ok || sink > SinkM)
                        {
                            NameRecord n = t.Name(sp.Record.NameRef);
                            bad.Append(t.Tile).Append(' ').Append(sp.Record.OsmWayId).Append(" '").Append(n != null ? n.Default : "").Append(ride ? "' ride: " : "' walk: ")
                               .Append(ok ? "sank " + sink.ToString("F2") : stop).Append('\n');
                        }
                    }
                }
            }
            TestContext.WriteLine(spans + " bridges and flyovers, " + runs + " runs");
            Assert.That(spans, Is.GreaterThanOrEqualTo(25));
            Assert.That(bad.ToString(), Is.Empty, "crossing the bridges and flyovers");
        }
    
        /// <summary>What the streamer-style ground answers at a point: BRIDGE_LIGHT_POINT = "x,z,y;..." (diagnostics).</summary>
        [Test, Explicit]
        public void LightPoint()
        {
            string specs = Environment.GetEnvironmentVariable("BRIDGE_LIGHT_POINT");
            if (string.IsNullOrEmpty(specs)) Assert.Ignore("set BRIDGE_LIGHT_POINT");
            foreach (string spec in specs.Split(';'))
            {
                string[] a = spec.Split(',');
                double x = double.Parse(a[0]), z = double.Parse(a[1]);
                float y = float.Parse(a[2]);
                TileGroundQuery g = Ground(new[] { TileId.At(10, x, z) });
                GroundSample s;
                bool got = g.TrySample(x, z, y, out s);
                TestContext.WriteLine("==== " + spec + ": ground " + (got ? s.Height.ToString("F2") + (s.OnDeck ? " deck" : "") + (s.OnRoad ? " road" : "") : "none") + " blocked " + g.IsBlocked(x, z, y) + " by " + g.BlockedBy(x, z, y));
                RoadHit rh;
                if (g.TryNearestRoad(x, z, RoadSpatialIndex.MaxFootpathM, RoadFlags.None, RoadFlags.None, RoadLayer.Ground, out rh))
                {
                    RoadSpatialIndex ri = g.RoadIndexOf(rh.Tile.Tile);
                    float sy = 0f, sg;
                    bool hs = ri != null && ri.TrySurfaceHeight(in rh, out sy, out sg);
                    TestContext.WriteLine("   nearest ground road " + rh.Road.OsmWayId + " " + rh.Road.RoadClass + " #" + rh.RoadIndex + " seg " + rh.Segment + " edge " + rh.EdgeDistanceM.ToString("F2")
                                          + " lat " + rh.LateralM.ToString("F2") + "/" + rh.HalfWidthM.ToString("F2") + " along " + rh.AlongM.ToString("F1") + " on " + rh.OnRoad + "/" + rh.OnFootpath
                                          + " surface " + (hs ? sy.ToString("F2") : "-") + " structure " + rh.Tile.RoadStructureOf(rh.RoadIndex).Kind);
                }
                float th;
                if (g.TryTerrainHeight(x, z, out th)) TestContext.WriteLine("   terrain " + th.ToString("F2"));
                var prims = new List<SolidPrim>();
                TileId pc = TileId.At(10, x, z);
                for (int qy = -1; qy <= 1; qy++)
                for (int qx0 = -1; qx0 <= 1; qx0++)
                    g.SolidsOf(new TileId(10, pc.Tx + qx0, pc.Ty + qy), prims);
                foreach (SolidPrim p in prims)
                {
                    double qx, qz;
                    SolidSet.Closest(p.Ax, p.Az, p.Bx, p.Bz, x, z, out qx, out qz);
                    double dd = Math.Sqrt((qx - x) * (qx - x) + (qz - z) * (qz - z)) - p.Radius;
                    if (dd > 0.5) continue;
                    TestContext.WriteLine("   solid (" + p.Ax.ToString("F2") + "," + p.Az.ToString("F2") + ")-(" + p.Bx.ToString("F2") + "," + p.Bz.ToString("F2") + ") r " + p.Radius.ToString("F2")
                                          + " y " + p.Bottom.ToString("F2") + ".." + p.Top.ToString("F2") + " grp " + p.Group + " d " + dd.ToString("F2"));
                }
                TileId c = TileId.At(10, x, z);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    TileData t = StreamingSampleRegion.Tile(new TileId(10, c.Tx + dx, c.Ty + dy));
                    if (t == null || !t.HasDetail) continue;
                    BridgeDeckIndex d = BridgeDeckIndex.ForTile(t);
                    float f, h, u, nx, ny, nz;
                    bool feet = d.TryDeck(x, z, y, out f, out nx, out ny, out nz), head = d.TryDeck(x, z, y + 1.2f, out h, out nx, out ny, out nz), ceil = d.TryCeiling(x, z, y + 0.45f, out u);
                    if (feet || head || ceil) TestContext.WriteLine("   " + t.Tile + " deck@feet " + (feet ? f.ToString("F2") : "-") + " deck@head " + (head ? h.ToString("F2") : "-") + " ceiling " + (ceil ? u.ToString("F2") : "-"));
                    string items = d.Explain(x, z);
                    if (items.Length > 0) TestContext.WriteLine("   " + t.Tile + " items: " + items);
                }
            }
        }
    
        /// <summary>Traces one flight walk on the streamer-style ground: BRIDGE_LIGHT_FLIGHT = "tx,ty,way,flight,up|down", or a
        /// crossing of a span: "tx,ty,way,walk|ride".</summary>
        [Test, Explicit]
        public void LightTraceFlight()
        {
            string spec = Environment.GetEnvironmentVariable("BRIDGE_LIGHT_FLIGHT");
            if (string.IsNullOrEmpty(spec)) Assert.Ignore("set BRIDGE_LIGHT_FLIGHT");
            string[] a = spec.Split(',');
            var id = new TileId(10, int.Parse(a[0]), int.Parse(a[1]));
            TileData t = StreamingSampleRegion.Tile(id);
            BridgeSpan sp = BridgeLayout.For(t).Spans.Find(x => x.Record.OsmWayId == ulong.Parse(a[2]));
            TileGroundQuery g = Ground(new[] { id });
            List<double> pts;
            float startY;
            bool ride = false;
            if (a[3] == "route")
            {
                // The route of EveryFootOverbridgeIsCrossedFootToFoot over the structure of group way a[2].
                OverbridgeRoutes.Route r = OverbridgeRoutes.For(t, g).Find(x => x.Way == ulong.Parse(a[2]));
                TestContext.WriteLine("route " + r.Note);
                if (r.Waypoints == null) return;
                pts = r.Waypoints;
                startY = r.StartY + 0.05f;
            }
            else if (a[3] == "walk" || a[3] == "ride")
            {
                // The crossing of RiverBridgesAndFlyoversAreWalkedAndRiddenAcross.
                int mid = sp.Count / 2;
                double lane = 0.5 * Math.Min(sp.HalfL[mid], sp.HalfR[mid]);
                pts = BridgeProbe.RoadAround(t, sp.Road, sp.Path.X[mid], sp.Path.Z[mid], 0.5 * sp.Length + 1.5, lane);
                startY = float.NaN;
                ride = a[3] == "ride";
            }
            else
            {
                BridgeStair st = sp.Stairs[int.Parse(a[3])];
                bool up = a[4] == "up";
                pts = BridgeProbe.Flight(st, t.Tile, up);
                startY = (up ? st.SurfaceAt(st.Length - 0.25) : st.SurfaceAt(0.25)) + 0.05f;
            }
            var v = new ArcadeVehicle(ride ? VehicleSpec.Motorbike() : VehicleSpec.Walker());
            v.Teleport(pts[0], pts[1], (float)Math.Atan2(pts[2] - pts[0], pts[3] - pts[1]), g);
            GroundSample s0;
            if (!float.IsNaN(startY) && g.TrySample(pts[0], pts[1], startY, out s0)) v.Y = s0.Height;
            var wp = new StringBuilder("waypoints:");
            for (int i = 0; i + 1 < pts.Count; i += 2) wp.Append(" (").Append(pts[i].ToString("F1")).Append(", ").Append(pts[i + 1].ToString("F1")).Append(')');
            TestContext.WriteLine(wp.ToString());
            int n = pts.Count / 2, target = 1;
            double reach = ride ? 2.5 : 0.8;
            for (int i = 0; i < 2400; i++)
            {
                double dx = pts[2 * target] - v.X, dz = pts[2 * target + 1] - v.Z;
                while (target < n - 1 && dx * dx + dz * dz < reach * reach)
                {
                    target++;
                    dx = pts[2 * target] - v.X;
                    dz = pts[2 * target + 1] - v.Z;
                }
                double bx = v.X, bz = v.Z;
                StepEvents ev = v.Step(new DriveInput(ride ? 0.5f : 1f, 0f, v.SteerToward((float)Math.Atan2(dx, dz), Dt)), Dt, g, 0f);
                if (i % 10 == 0 || ev != StepEvents.None)
                {
                    GroundSample ga;
                    double ax = v.X + Math.Sin(v.HeadingRad) * 0.35, az = v.Z + Math.Cos(v.HeadingRad) * 0.35;
                    bool got = g.TrySample(ax, az, v.Y, out ga);
                    TestContext.WriteLine(i + " (" + v.X.ToString("F2") + ", " + v.Z.ToString("F2") + ") y " + v.Y.ToString("F2") + " v " + v.SpeedMps.ToString("F2") + " ev " + ev
                                          + " gnd " + v.Ground.Height.ToString("F2") + " ny " + v.Ground.Ny.ToString("F2") + (v.Ground.OnDeck ? " deck" : "")
                                          + " ahead " + (got ? ga.Height.ToString("F2") + " ny " + ga.Ny.ToString("F2") + (ga.OnDeck ? " deck" : "") : "none") + " blocked " + g.IsBlocked(ax, az, v.Y));
                }
            }
        }
    }
}
