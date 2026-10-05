using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Routing
{
    /// <summary>
    /// Snaps game positions to routing-graph nodes (<c>routing.nearest_node</c>): the closest node with at
    /// least one usable outgoing edge for a profile (or, for destinations, a usable incoming edge). With
    /// <c>mainNetwork</c> (the default) only nodes that can reach the profile's main component (the largest
    /// strongly connected component; destinations: nodes reachable from it) qualify, so a landmark never snaps
    /// into a small disconnected island and every start can reach every destination. Uses a uniform grid of
    /// buckets; exact distance ties go to the lowest node index.
    /// </summary>
    public sealed class NearestNode
    {
        public const double DefaultCellM = 512.0;

        private readonly RouteGraph _g;
        private readonly double _cell;
        private readonly Dictionary<long, int[]> _buckets = new Dictionary<long, int[]>();
        private readonly int _minCx, _minCz, _maxCx, _maxCz;

        public int Count { get; private set; }

        /// <summary>Index of the nodes <paramref name="profile"/> may snap to in <paramref name="graph"/>
        /// (<see cref="SnapNodes"/>).</summary>
        public NearestNode(RouteGraph graph, Travel profile, bool incoming, double cellM = DefaultCellM,
                           bool mainNetwork = true)
            : this(graph, SnapNodes(graph, profile, incoming, mainNetwork), cellM)
        {
        }

        /// <summary>Index of an explicit node set (ascending node indices).</summary>
        public NearestNode(RouteGraph graph, int[] nodes, double cellM = DefaultCellM)
        {
            _g = graph ?? throw new ArgumentNullException(nameof(graph));
            if (!(cellM > 0)) throw new ArgumentOutOfRangeException(nameof(cellM));
            _cell = cellM;
            var lists = new Dictionary<long, List<int>>();
            _minCx = _minCz = int.MaxValue;
            _maxCx = _maxCz = int.MinValue;
            foreach (int v in nodes)
            {
                int cx = CellOf(graph.NodeX(v)), cz = CellOf(graph.NodeZ(v));
                List<int> l;
                long k = Key(cx, cz);
                if (!lists.TryGetValue(k, out l)) lists[k] = l = new List<int>();
                l.Add(v);
                _minCx = Math.Min(_minCx, cx);
                _minCz = Math.Min(_minCz, cz);
                _maxCx = Math.Max(_maxCx, cx);
                _maxCz = Math.Max(_maxCz, cz);
            }
            foreach (var kv in lists) _buckets[kv.Key] = kv.Value.ToArray();
            Count = nodes.Length;
        }

        /// <summary>Nodes with a usable outgoing (or incoming) edge for the profile, ascending.</summary>
        public static int[] UsableNodes(RouteGraph g, Travel profile, bool incoming)
        {
            TravelProfile p = TravelProfiles.Get(profile);
            var use = new bool[g.NodeCount];
            int[] src = g.EdgeSource;
            for (int e = 0; e < g.EdgeCount; e++)
                if (!double.IsPositiveInfinity(p.EdgeTimeS(g, e))) use[incoming ? g.EdgeTarget[e] : src[e]] = true;
            var outList = new List<int>();
            for (int v = 0; v < use.Length; v++)
                if (use[v]) outList.Add(v);
            return outList.ToArray();
        }

        /// <summary>
        /// Nodes a query may snap to (<c>routing.snap_nodes</c>), ascending: <see cref="UsableNodes"/>, and with
        /// <paramref name="mainNetwork"/> (when the profile has a main component) only those that can reach it
        /// (<paramref name="incoming"/> false) or can be reached from it (true).
        /// </summary>
        public static int[] SnapNodes(RouteGraph g, Travel profile, bool incoming, bool mainNetwork = true)
        {
            int[] usable = UsableNodes(g, profile, incoming);
            if (!mainNetwork) return usable;
            int[] main = MainComponent(g, profile);
            if (main.Length == 0) return usable;
            bool[] reach = Reachable(g, Usable(g, profile), main[0], !incoming);
            var outList = new List<int>();
            foreach (int v in usable)
                if (reach[v]) outList.Add(v);
            return outList.ToArray();
        }

        private static bool[] Usable(RouteGraph g, Travel profile)
        {
            TravelProfile p = TravelProfiles.Get(profile);
            var ok = new bool[g.EdgeCount];
            for (int e = 0; e < ok.Length; e++) ok[e] = !double.IsPositiveInfinity(p.EdgeTimeS(g, e));
            return ok;
        }

        /// <summary>Nodes reachable from <paramref name="start"/> over usable edges (backwards when
        /// <paramref name="reverse"/>: the nodes that can reach it).</summary>
        private static bool[] Reachable(RouteGraph g, bool[] ok, int start, bool reverse)
        {
            int n = g.NodeCount;
            int[] off = g.Offsets, tgt = g.EdgeTarget, src = g.EdgeSource;
            int[] rOff = null, rEdge = null;
            if (reverse)
            {
                rOff = new int[n + 1];
                for (int e = 0; e < ok.Length; e++)
                    if (ok[e]) rOff[tgt[e] + 1]++;
                for (int v = 0; v < n; v++) rOff[v + 1] += rOff[v];
                rEdge = new int[rOff[n]];
                var fill = (int[])rOff.Clone();
                for (int e = 0; e < ok.Length; e++)
                    if (ok[e]) rEdge[fill[tgt[e]]++] = e;
            }
            var seen = new bool[n];
            var stack = new Stack<int>();
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int v = stack.Pop();
                if (reverse)
                {
                    for (int j = rOff[v]; j < rOff[v + 1]; j++)
                    {
                        int w = src[rEdge[j]];
                        if (!seen[w]) { seen[w] = true; stack.Push(w); }
                    }
                }
                else
                {
                    for (int e = off[v]; e < off[v + 1]; e++)
                    {
                        if (!ok[e]) continue;
                        int w = tgt[e];
                        if (!seen[w]) { seen[w] = true; stack.Push(w); }
                    }
                }
            }
            return seen;
        }

        /// <summary>
        /// The profile's main component (<c>routing.main_component</c>), ascending: the largest strongly
        /// connected component of the usable-edge graph; ties go to the component holding the smallest node
        /// index. Empty when no component has two or more nodes.
        /// </summary>
        public static int[] MainComponent(RouteGraph g, Travel profile)
        {
            int n = g.NodeCount;
            if (n == 0) return new int[0];
            bool[] ok = Usable(g, profile);
            int[] off = g.Offsets, tgt = g.EdgeTarget;
            // Iterative Tarjan.
            var index = new int[n];
            var low = new int[n];
            var comp = new int[n];
            var onStack = new bool[n];
            var edgePos = new int[n];
            for (int v = 0; v < n; v++) { index[v] = -1; comp[v] = -1; }
            var sccStack = new Stack<int>();
            var call = new Stack<int>();
            var sizes = new List<int>();
            int counter = 0;
            for (int root = 0; root < n; root++)
            {
                if (index[root] >= 0) continue;
                call.Push(root);
                index[root] = low[root] = counter++;
                edgePos[root] = off[root];
                sccStack.Push(root);
                onStack[root] = true;
                while (call.Count > 0)
                {
                    int v = call.Peek();
                    bool descended = false;
                    while (edgePos[v] < off[v + 1])
                    {
                        int e = edgePos[v]++;
                        if (!ok[e]) continue;
                        int w = tgt[e];
                        if (index[w] < 0)
                        {
                            index[w] = low[w] = counter++;
                            edgePos[w] = off[w];
                            sccStack.Push(w);
                            onStack[w] = true;
                            call.Push(w);
                            descended = true;
                            break;
                        }
                        if (onStack[w] && index[w] < low[v]) low[v] = index[w];
                    }
                    if (descended) continue;
                    call.Pop();
                    if (call.Count > 0)
                    {
                        int u = call.Peek();
                        if (low[v] < low[u]) low[u] = low[v];
                    }
                    if (low[v] == index[v])
                    {
                        int id = sizes.Count, size = 0, w;
                        do
                        {
                            w = sccStack.Pop();
                            onStack[w] = false;
                            comp[w] = id;
                            size++;
                        } while (w != v);
                        sizes.Add(size);
                    }
                }
            }
            int best = 0;
            foreach (int sz in sizes) best = Math.Max(best, sz);
            if (best < 2) return new int[0];
            int pick = -1;
            for (int v = 0; v < n && pick < 0; v++)
                if (sizes[comp[v]] == best) pick = comp[v];
            var outList = new List<int>();
            for (int v = 0; v < n; v++)
                if (comp[v] == pick) outList.Add(v);
            return outList.ToArray();
        }

        private int CellOf(double v)
        {
            return (int)Math.Floor(v / _cell);
        }

        private static long Key(int cx, int cz)
        {
            return (long)cx << 32 | (uint)cz;
        }

        /// <summary>The closest node to game (x, z), or -1 when none qualifies or the nearest is farther than
        /// <paramref name="maxDistM"/>.</summary>
        public int Find(double x, double z, double maxDistM = double.PositiveInfinity)
        {
            if (Count == 0) return -1;
            int qx = CellOf(x), qz = CellOf(z);
            int bestNode = -1;
            double bestD2 = double.PositiveInfinity;
            // Rings of cells around the query cell; any node in ring r+1 is at least r cells away.
            int maxR = Math.Max(Math.Max(Math.Abs(qx - _minCx), Math.Abs(qx - _maxCx)),
                                Math.Max(Math.Abs(qz - _minCz), Math.Abs(qz - _maxCz)));
            for (int r = 0; r <= maxR; r++)
            {
                double ringMin = (r - 1) * _cell;
                if (r > 0 && ringMin > 0 && ringMin * ringMin > bestD2) break;
                if (r > 0 && ringMin > maxDistM) break;
                for (int cx = qx - r; cx <= qx + r; cx++)
                {
                    bool edgeCol = cx == qx - r || cx == qx + r;
                    for (int cz = qz - r; cz <= qz + r; cz += edgeCol ? 1 : 2 * r)
                    {
                        int[] bucket;
                        if (_buckets.TryGetValue(Key(cx, cz), out bucket))
                        {
                            foreach (int v in bucket)
                            {
                                double dx = _g.NodeX(v) - x, dz = _g.NodeZ(v) - z;
                                double d2 = dx * dx + dz * dz;
                                if (d2 < bestD2 || d2 == bestD2 && v < bestNode)
                                {
                                    bestD2 = d2;
                                    bestNode = v;
                                }
                            }
                        }
                        if (r == 0) break;
                    }
                }
            }
            if (bestNode < 0 || Math.Sqrt(bestD2) > maxDistM) return -1;
            return bestNode;
        }
    }
}
