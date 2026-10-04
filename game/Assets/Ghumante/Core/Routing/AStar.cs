using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Routing
{
    /// <summary>A route from source to destination node.</summary>
    public sealed class Route
    {
        /// <summary>Node indices from source to destination.</summary>
        public int[] Nodes;

        /// <summary>Edge indices, Nodes.Length - 1 of them.</summary>
        public int[] Edges;

        public double LengthM;
        public double TimeS;
    }

    /// <summary>
    /// The reference router (<c>routing.route</c>) over a <see cref="RouteGraph"/>: A* with
    /// <c>h(v) = |v - dst| / max_speed</c>, re-opening nodes, ties in the open list broken exactly like
    /// Python's heapq on <c>(f, g, node)</c> tuples, so routes and times are identical to the reference.
    /// Edge times are cached per profile on first use; one instance may serve one thread at a time.
    /// </summary>
    public sealed class AStar
    {
        private readonly RouteGraph _g;
        private readonly Dictionary<Travel, double[]> _times = new Dictionary<Travel, double[]>();

        public AStar(RouteGraph graph)
        {
            _g = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        public RouteGraph Graph
        {
            get { return _g; }
        }

        /// <summary>Seconds per edge for a profile (+infinity where unusable), as routing.edge_times.</summary>
        public double[] EdgeTimes(Travel profile)
        {
            double[] t;
            lock (_times)
            {
                if (_times.TryGetValue(profile, out t)) return t;
            }
            TravelProfile p = TravelProfiles.Get(profile);
            t = new double[_g.EdgeCount];
            for (int e = 0; e < t.Length; e++) t[e] = p.EdgeTimeS(_g, e);
            lock (_times)
            {
                _times[profile] = t;
            }
            return t;
        }

        /// <summary>Fastest route from node <paramref name="src"/> to <paramref name="dst"/>, or null when
        /// unreachable. <paramref name="heuristic"/> false runs plain Dijkstra.</summary>
        public Route FindRoute(int src, int dst, Travel profile, bool heuristic = true)
        {
            int n = _g.NodeCount;
            if (src < 0 || src >= n || dst < 0 || dst >= n)
                throw new ArgumentOutOfRangeException(nameof(src), "node index out of range (node_count " + n + ")");
            if (src == dst) return new Route { Nodes = new[] { src }, Edges = new int[0], LengthM = 0.0, TimeS = 0.0 };

            TravelProfile p = TravelProfiles.Get(profile);
            double[] times = EdgeTimes(profile);
            int[] off = _g.Offsets, tgt = _g.EdgeTarget, xs = _g.NodeXDm, zs = _g.NodeZDm;
            long xd = xs[dst], zd = zs[dst];
            double vmax = p.MaxSpeedKmh;

            var best = new Dictionary<int, double> { { src, 0.0 } };
            var prev = new Dictionary<int, int>(); // node -> edge used to reach it
            var hc = new Dictionary<int, double>();
            var heap = new MinHeap();
            heap.Push(new HeapItem(H(xs[src] - xd, zs[src] - zd, vmax, heuristic), 0.0, src));
            bool found = false;
            while (heap.Count > 0)
            {
                HeapItem it = heap.Pop();
                int v = it.Node;
                double gv = it.G;
                if (gv > best[v]) continue;
                if (v == dst)
                {
                    found = true;
                    break;
                }
                for (int e = off[v]; e < off[v + 1]; e++)
                {
                    double te = times[e];
                    if (double.IsPositiveInfinity(te)) continue;
                    int w = tgt[e];
                    double ng = gv + te;
                    double old;
                    if (!best.TryGetValue(w, out old) || ng < old)
                    {
                        best[w] = ng;
                        prev[w] = e;
                        double hw;
                        if (!hc.TryGetValue(w, out hw))
                        {
                            hw = H(xs[w] - xd, zs[w] - zd, vmax, heuristic);
                            hc[w] = hw;
                        }
                        heap.Push(new HeapItem(ng + hw, ng, w));
                    }
                }
            }
            if (!found) return null;

            int[] srcOf = _g.EdgeSource;
            var edges = new List<int>();
            var nodes = new List<int> { dst };
            int cur = dst;
            while (cur != src)
            {
                int e = prev[cur];
                edges.Add(e);
                cur = srcOf[e];
                nodes.Add(cur);
            }
            edges.Reverse();
            nodes.Reverse();
            long lengthDm = 0;
            foreach (int e in edges) lengthDm += _g.EdgeLengthDm[e];
            return new Route { Nodes = nodes.ToArray(), Edges = edges.ToArray(), LengthM = lengthDm / 10.0, TimeS = best[dst] };
        }

        // Straight-line seconds at the profile's top speed. The squared decimetre sum is exact in a double for
        // anything inside the 2^20 m quadtree, so Math.Sqrt is the correctly rounded hypot that Python uses.
        private static double H(long dx, long dz, double vmax, bool heuristic)
        {
            if (!heuristic) return 0.0;
            return Math.Sqrt((double)dx * dx + (double)dz * dz) / 10.0 * 3.6 / vmax;
        }

        /// <summary>Concatenated game-metre polyline of a route (shared joints once), interleaved x, z.</summary>
        public double[] RouteGeometry(Route r)
        {
            var pts = new List<double>();
            if (r.Edges.Length == 0)
            {
                pts.Add(_g.NodeX(r.Nodes[0]));
                pts.Add(_g.NodeZ(r.Nodes[0]));
                return pts.ToArray();
            }
            for (int k = 0; k < r.Edges.Length; k++)
            {
                double[] g = _g.EdgeGeometry(r.Edges[k]);
                pts.AddRange(k == 0 ? g : new ArraySegment<double>(g, 2, g.Length - 2));
            }
            return pts.ToArray();
        }

        private struct HeapItem
        {
            public readonly double F;
            public readonly double G;
            public readonly int Node;

            public HeapItem(double f, double g, int node)
            {
                F = f;
                G = g;
                Node = node;
            }

            public bool LessThan(HeapItem o)
            {
                if (F != o.F) return F < o.F;
                if (G != o.G) return G < o.G;
                return Node < o.Node;
            }
        }

        /// <summary>Binary min-heap on (f, g, node), the order of Python tuples in heapq.</summary>
        private sealed class MinHeap
        {
            private HeapItem[] _a = new HeapItem[64];
            public int Count;

            public void Push(HeapItem x)
            {
                if (Count == _a.Length) Array.Resize(ref _a, _a.Length * 2);
                int i = Count++;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (!x.LessThan(_a[parent])) break;
                    _a[i] = _a[parent];
                    i = parent;
                }
                _a[i] = x;
            }

            public HeapItem Pop()
            {
                HeapItem top = _a[0];
                HeapItem last = _a[--Count];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= Count) break;
                    int c = l + 1 < Count && _a[l + 1].LessThan(_a[l]) ? l + 1 : l;
                    if (!_a[c].LessThan(last)) break;
                    _a[i] = _a[c];
                    i = c;
                }
                if (Count > 0) _a[i] = last;
                return top;
            }
        }
    }
}
