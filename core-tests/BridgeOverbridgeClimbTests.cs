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
    /// Owner playtest #15 (docs/W2_PLAYTEST_1.md: "Overhead sidewalks do not work. I tried crossing the road using that,
    /// and i could not even reach the top"): every foot overbridge of the sample crossed end to end the way the player
    /// does it, on the world built as the game streams it (<see cref="BridgeWorld"/>) with the real walker
    /// (<see cref="ArcadeVehicle"/>): from the pavement at the foot of one stair, up every flight and landing, along the
    /// deck over the road, and down a stair on the other side to the pavement. The route is found over the structure's
    /// own pieces (<see cref="OverbridgeRoutes"/>).
    /// </summary>
    public class BridgeOverbridgeClimbTests
    {
        /// <summary>The overbridge structures of the sample whose decks clear a road, with a route from a stair foot on
        /// one side of the road to one on the other.</summary>
        private static IEnumerable<TileId> OverbridgeTiles()
        {
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (!t.HasDetail) continue;
                foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
                    if (sp.Overbridge)
                    {
                        yield return id;
                        break;
                    }
            }
        }

        /// <summary>Climbs every overbridge: the walker must reach the far stair foot, stand on the deck on the way (at
        /// the deck's height over the road), and never sink into what is drawn.</summary>
        [Test, Explicit]
        public void EveryOverbridgeCanBeClimbedAndCrossed()
        {
            string only = Environment.GetEnvironmentVariable("BRIDGE_CLIMB");
            var tiles = new List<TileId>();
            foreach (TileId id in OverbridgeTiles())
                if (string.IsNullOrEmpty(only) || BridgeLayout.For(StreamingSampleRegion.Tile(id)).Spans.Exists(sp => sp.GroupWay.ToString() == only)) tiles.Add(id);
            Assert.That(tiles.Count, Is.GreaterThan(0), "the sample has foot overbridges (Kanti Path, Durbar Marg, New Baneshwor)");
            BridgeWorld w = BridgeWorld.Around(tiles, 1);
            var report = new StringBuilder();
            int routes = 0, bad = 0, structures = 0;
            foreach (TileId id in tiles)
            {
                TileData t = w.Tile(id);
                foreach (OverbridgeRoutes.Route r in OverbridgeRoutes.For(t, w))
                {
                    if (!string.IsNullOrEmpty(only) && r.Way.ToString() != only) continue;
                    if (r.Waypoints == null)
                    {
                        structures++;
                        bad++;
                        report.Append(t.Tile).Append(" overbridge ").Append(r.Way).Append(": NO ROUTE (").Append(r.Note).Append(")\n");
                        continue;
                    }
                    structures++;
                    routes++;
                    StringBuilder trace = string.IsNullOrEmpty(only) ? null : new StringBuilder();
                    ProbeRun run = BridgeProbe.Follow(w, VehicleSpec.Walker(), r.Waypoints, 1f, r.StartY + 0.05f, 120f, trace);
                    if (trace != null)
                    {
                        var wp = new StringBuilder("waypoints:");
                        for (int i = 0; i + 1 < r.Waypoints.Count; i += 2) wp.Append(" (").Append(r.Waypoints[i].ToString("F1")).Append(", ").Append(r.Waypoints[i + 1].ToString("F1")).Append(')');
                        TestContext.WriteLine(wp.ToString());
                        TestContext.WriteLine(trace.ToString());
                    }
                    bool topped = run.MaxY > r.DeckY - 0.3f;
                    bool ok = run.Reached && topped && run.BridgeSink < 0.25f;
                    if (!ok) bad++;
                    report.Append(t.Tile).Append(" overbridge ").Append(r.Way).Append(" deck ").Append(r.DeckY.ToString("F1")).Append(" (").Append(r.Note).Append("): ")
                          .Append(ok ? "ok " : "BAD ").Append(run).Append(topped ? "" : " NEVER REACHED THE DECK").Append('\n');
                }
            }
            TestContext.WriteLine("overbridges " + structures + ", routes " + routes + ", bad " + bad);
            TestContext.WriteLine(report.ToString());
            Assert.That(bad, Is.EqualTo(0), report.ToString());
        }

        /// <summary>Playtest #15 on the Kanti Path style overbridge as OSM maps it (<see cref="BridgeScenes.MappedOverbridge"/>:
        /// at each end of the deck two steps ways run down along the pavement, so the walk turns 90 degrees where a
        /// flight meets the deck end): the real walker, ground query and structure solids take the player from the
        /// north-west pavement up the flight, round the turn onto the deck, over the road and down the south-west flight
        /// to the pavement on the other side, both ways.</summary>
        [Test]
        public void MappedOverbridgeIsClimbedCrossedAndDescended()
        {
            TileData t = BridgeScenes.MappedOverbridge(false);
            var g = new TileGroundQuery { DeckFactory = BridgeDeckIndex.ForTile };
            g.Add(t);
            BridgeSpan deck = BridgeLayout.For(t).Spans.Find(s => s.Record.OsmWayId == 701UL);
            Assert.That(deck, Is.Not.Null);
            float deckY = deck.Path.Y[deck.Count / 2];
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            // The mapped lines a player follows: the pavement footway, steps 703 up eastward to the deck end, the deck
            // south over the road, steps 705 down westward, the pavement beyond.
            double[] plan = { 478, 508, 486, 508, 490, 508, 494, 508, 498, 508, 500, 507, 500, 504, 500, 500, 500, 496, 500, 493, 498, 492, 494, 492, 490, 492, 486, 492, 478, 492 };
            for (int dir = 0; dir < 2; dir++)
            {
                var pts = new List<double>();
                for (int i = 0; i < plan.Length / 2; i++)
                {
                    int k = dir == 0 ? i : plan.Length / 2 - 1 - i;
                    pts.Add(x0 + plan[2 * k]);
                    pts.Add(z0 + plan[2 * k + 1]);
                }
                float maxY, worstStep;
                bool reached = Walk(g, pts, out maxY, out worstStep);
                string what = dir == 0 ? "north-west to south-west" : "south-west to north-west";
                Assert.That(maxY, Is.GreaterThan(deckY - 0.3f), "reached the deck (" + what + ")");
                Assert.That(reached, Is.True, "crossed to the far pavement (" + what + "), top " + maxY.ToString("F2") + " of " + deckY.ToString("F2"));
                Assert.That(worstStep, Is.LessThan(0.5f), "no jump in height in one step (" + what + ")");
            }
        }

        /// <summary>Walks a waypoint line (world metres) with the real walker, as <see cref="BridgeProbe.Follow"/> does
        /// without the drawn world: true when the last point is reached within a minute.</summary>
        private static bool Walk(IGroundQuery g, IList<double> pts, out float maxY, out float worstStep)
        {
            var v = new ArcadeVehicle(VehicleSpec.Walker());
            v.Teleport(pts[0], pts[1], (float)Math.Atan2(pts[2] - pts[0], pts[3] - pts[1]), g);
            maxY = v.Y;
            worstStep = 0f;
            int target = 1, n = pts.Count / 2;
            for (int i = 0; i < 60 * 60; i++)
            {
                double dx = pts[2 * target] - v.X, dz = pts[2 * target + 1] - v.Z;
                while (target < n - 1 && dx * dx + dz * dz < 0.8 * 0.8)
                {
                    target++;
                    dx = pts[2 * target] - v.X;
                    dz = pts[2 * target + 1] - v.Z;
                }
                if (target == n - 1 && dx * dx + dz * dz < 0.8 * 0.8) return true;
                float y = v.Y;
                v.Step(new DriveInput(1f, 0f, v.SteerToward((float)Math.Atan2(dx, dz), BridgeProbe.Dt)), BridgeProbe.Dt, g, 0f);
                maxY = Math.Max(maxY, v.Y);
                worstStep = Math.Max(worstStep, Math.Abs(v.Y - y));
            }
            return false;
        }
    }

    /// <summary>
    /// Walking routes over a foot overbridge structure (the deck spans and stairs sharing its <see cref="BridgeSpan.GroupWay"/>):
    /// a graph of flight ends, landing pads and deck stations joined where the walking surface continues (within a plan
    /// gap and a step), searched from every stair foot on the ground to the farthest one reachable over the deck.
    /// </summary>
    internal static class OverbridgeRoutes
    {
        internal sealed class Route
        {
            public ulong Way;
            public List<double> Waypoints;
            public float StartY, DeckY;
            public string Note;
        }

        private struct Node
        {
            public double X, Z;
            public float Y;
            public bool Deck, Foot;

            /// <summary>Unit plan direction out of a flight at this end (0 for other nodes): a walker leaving the flight
            /// carries on straight for a step before turning.</summary>
            public double Ox, Oz;
        }

        public static List<Route> For(TileData t, BridgeWorld w)
        {
            var result = new List<Route>();
            BridgeLayout l = BridgeLayout.For(t);
            var groups = new SortedSet<ulong>();
            foreach (BridgeSpan sp in l.Spans)
                if (sp.Overbridge)
                    groups.Add(sp.GroupWay);
            foreach (ulong g in groups) result.Add(RouteOf(t, l, g, w));
            return result;
        }

        private static Route RouteOf(TileData t, BridgeLayout l, ulong group, BridgeWorld w)
        {
            var nodes = new List<Node>();
            var edges = new List<List<int>>();
            // Interpolated walking line of each edge (world points, without the end nodes), keyed by (a, b).
            var lines = new Dictionary<long, List<double>>();
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            float deckY = float.MinValue;
            foreach (BridgeSpan sp in l.Spans)
            {
                if (sp.GroupWay != group) continue;
                if (!sp.Stair)
                {
                    // Deck stations every ~2 m along the centreline.
                    int prev = -1;
                    for (double s = sp.Path.Start; s <= sp.Path.End + 1e-6; s += Math.Max(0.5, Math.Min(2.0, (sp.Path.End - sp.Path.Start) / 4)))
                    {
                        if (!sp.HasDeck(s) && s > sp.Path.Start + 0.5 && s < sp.Path.End - 0.5) continue;
                        double px, pz, ux, uz, nx, nz, tx, tz;
                        float y;
                        sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                        int k = Add(nodes, edges, x0 + px, z0 + pz, y, true);
                        if (sp.Overbridge) deckY = Math.Max(deckY, y);
                        if (prev >= 0) Link(edges, prev, k);
                        prev = k;
                    }
                }
                foreach (BridgeStair st in sp.Stairs)
                {
                    if (st.Steps < 0) continue;
                    double bx = st.X + st.Dx * st.Length, bz = st.Z + st.Dz * st.Length;
                    int a = Add(nodes, edges, x0 + st.X, z0 + st.Z, st.TopY, false);
                    int b = Add(nodes, edges, x0 + bx, z0 + bz, st.BottomY, false);
                    Node na = nodes[a], nb0 = nodes[b];
                    na.Ox = -st.Dx;
                    na.Oz = -st.Dz;
                    nb0.Ox = st.Dx;
                    nb0.Oz = st.Dz;
                    nodes[a] = na;
                    nodes[b] = nb0;
                    Link(edges, a, b);
                    var line = new List<double>();
                    int n = Math.Max(1, (int)Math.Ceiling(st.Length / 0.5));
                    for (int i = 1; i < n; i++)
                    {
                        double f = st.Length * i / n;
                        line.Add(x0 + st.X + st.Dx * f);
                        line.Add(z0 + st.Z + st.Dz * f);
                    }
                    lines[Key(a, b)] = line;
                    if (st.Steps == 0) continue;
                    // A stair foot on the ground: its bottom within a step of the terrain (or the road) there.
                    GroundSample g;
                    double fx = x0 + bx + st.Dx * 0.6, fz = z0 + bz + st.Dz * 0.6;
                    if (w.Ground.TrySample(fx, fz, st.BottomY + 0.3f, out g) && Math.Abs(g.Height - st.BottomY) < 0.45f && !g.OnDeck)
                    {
                        Node nb = nodes[b];
                        nb.Foot = true;
                        nodes[b] = nb;
                        // The pavement 2 m beyond the foot, straight on.
                        int street = Add(nodes, edges, x0 + bx + st.Dx * 2.0, z0 + bz + st.Dz * 2.0, g.Height, false);
                        Node ns = nodes[street];
                        ns.Foot = true;
                        nodes[street] = ns;
                        Link(edges, b, street);
                    }
                }
            }
            // Joins: ends of different pieces within 1.6 m in plan and a step in height (flight to pad, flight to flight,
            // flight top to the nearest deck station).
            for (int i = 0; i < nodes.Count; i++)
            for (int j = i + 1; j < nodes.Count; j++)
            {
                double dx = nodes[i].X - nodes[j].X, dz = nodes[i].Z - nodes[j].Z;
                double d = Math.Sqrt(dx * dx + dz * dz);
                double reach = nodes[i].Deck || nodes[j].Deck ? 3.5 : 1.6;
                if (d < reach && Math.Abs(nodes[i].Y - nodes[j].Y) < 0.45f) Link(edges, i, j);
            }
            var route = new Route { Way = group, DeckY = deckY };
            // Every foot to every other foot whose route goes over the deck: keep the longest such route.
            double bestLen = 0;
            List<int> best = null;
            for (int s = 0; s < nodes.Count; s++)
            {
                if (!nodes[s].Foot) continue;
                int[] prevOf;
                double[] dist;
                Dijkstra(nodes, edges, s, out prevOf, out dist);
                for (int e = 0; e < nodes.Count; e++)
                {
                    if (e == s || !nodes[e].Foot || double.IsPositiveInfinity(dist[e])) continue;
                    var path = new List<int>();
                    for (int v = e; v >= 0; v = prevOf[v]) path.Insert(0, v);
                    bool overDeck = false;
                    foreach (int v in path) overDeck |= nodes[v].Deck && nodes[v].Y > deckY - 0.5f;
                    if (!overDeck) continue;
                    double straight = Math.Sqrt((nodes[s].X - nodes[e].X) * (nodes[s].X - nodes[e].X) + (nodes[s].Z - nodes[e].Z) * (nodes[s].Z - nodes[e].Z));
                    if (straight > bestLen)
                    {
                        bestLen = straight;
                        best = path;
                    }
                }
            }
            if (best == null)
            {
                int feet = 0;
                foreach (Node n in nodes) feet += n.Foot ? 1 : 0;
                var why = new StringBuilder();
                // Each flight top's nearest deck node (plan distance and height step): where the joins failed.
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].Deck || nodes[i].Ox == 0 && nodes[i].Oz == 0) continue;
                    double bestD = double.MaxValue;
                    float dy = 0;
                    for (int j = 0; j < nodes.Count; j++)
                    {
                        if (!nodes[j].Deck) continue;
                        double d = Math.Sqrt((nodes[i].X - nodes[j].X) * (nodes[i].X - nodes[j].X) + (nodes[i].Z - nodes[j].Z) * (nodes[i].Z - nodes[j].Z));
                        if (d < bestD)
                        {
                            bestD = d;
                            dy = nodes[j].Y - nodes[i].Y;
                        }
                    }
                    if (bestD < 6) why.Append(" end (").Append(nodes[i].X.ToString("F1")).Append(", ").Append(nodes[i].Z.ToString("F1")).Append(") y ").Append(nodes[i].Y.ToString("F1"))
                                      .Append(" deck@").Append(bestD.ToString("F1")).Append(" dy ").Append(dy.ToString("F2")).Append(" links ").Append(edges[i].Count).Append(';');
                }
                route.Note = "feet " + feet + ", nodes " + nodes.Count + why;
                return route;
            }
            var pts = new List<double>();
            for (int i = 0; i < best.Count; i++)
            {
                if (i > 0)
                {
                    List<double> line;
                    int a = best[i - 1], b = best[i];
                    if (lines.TryGetValue(Key(a, b), out line))
                    {
                        if (a < b) pts.AddRange(line);
                        else
                            for (int k = line.Count - 2; k >= 0; k -= 2)
                            {
                                pts.Add(line[k]);
                                pts.Add(line[k + 1]);
                            }
                    }
                    else
                    {
                        // A join between pieces: off a flight straight on for a step, onto one straight in.
                        Node na = nodes[a], nb = nodes[b];
                        if (na.Ox != 0 || na.Oz != 0)
                        {
                            pts.Add(na.X + na.Ox * 0.8);
                            pts.Add(na.Z + na.Oz * 0.8);
                        }
                        if (nb.Ox != 0 || nb.Oz != 0)
                        {
                            pts.Add(nb.X + nb.Ox * 0.8);
                            pts.Add(nb.Z + nb.Oz * 0.8);
                        }
                    }
                }
                Node nd = nodes[best[i]];
                if (pts.Count >= 2 && Math.Abs(pts[pts.Count - 2] - nd.X) + Math.Abs(pts[pts.Count - 1] - nd.Z) < 0.05) continue;
                pts.Add(nd.X);
                pts.Add(nd.Z);
            }
            route.Waypoints = pts;
            route.StartY = nodes[best[0]].Y;
            route.Note = "from (" + nodes[best[0]].X.ToString("F1") + ", " + nodes[best[0]].Z.ToString("F1") + ") to (" + nodes[best[best.Count - 1]].X.ToString("F1") + ", "
                         + nodes[best[best.Count - 1]].Z.ToString("F1") + "), " + best.Count + " nodes";
            return route;
        }

        private static long Key(int a, int b)
        {
            return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        }

        private static int Add(List<Node> nodes, List<List<int>> edges, double x, double z, float y, bool deck)
        {
            nodes.Add(new Node { X = x, Z = z, Y = y, Deck = deck });
            edges.Add(new List<int>());
            return nodes.Count - 1;
        }

        private static void Link(List<List<int>> edges, int a, int b)
        {
            if (!edges[a].Contains(b)) edges[a].Add(b);
            if (!edges[b].Contains(a)) edges[b].Add(a);
        }

        private static void Dijkstra(List<Node> nodes, List<List<int>> edges, int s, out int[] prev, out double[] dist)
        {
            int n = nodes.Count;
            prev = new int[n];
            dist = new double[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++)
            {
                prev[i] = -1;
                dist[i] = double.PositiveInfinity;
            }
            dist[s] = 0;
            for (int it = 0; it < n; it++)
            {
                int u = -1;
                for (int i = 0; i < n; i++)
                    if (!done[i] && (u < 0 || dist[i] < dist[u]))
                        u = i;
                if (u < 0 || double.IsPositiveInfinity(dist[u])) break;
                done[u] = true;
                foreach (int v in edges[u])
                {
                    double dx = nodes[u].X - nodes[v].X, dz = nodes[u].Z - nodes[v].Z;
                    double d = dist[u] + Math.Sqrt(dx * dx + dz * dz) + Math.Abs(nodes[u].Y - nodes[v].Y);
                    if (d < dist[v])
                    {
                        dist[v] = d;
                        prev[v] = u;
                    }
                }
            }
        }
    }
}
