using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Routing
{
    /// <summary>
    /// Snaps game positions to routing-graph nodes (<c>routing.nearest_node</c>): the closest node with at
    /// least one usable outgoing edge for a profile (or, for destinations, a usable incoming edge). Uses a
    /// uniform grid of buckets; exact distance ties go to the lowest node index.
    /// </summary>
    public sealed class NearestNode
    {
        public const double DefaultCellM = 512.0;

        private readonly RouteGraph _g;
        private readonly double _cell;
        private readonly Dictionary<long, int[]> _buckets = new Dictionary<long, int[]>();
        private readonly int _minCx, _minCz, _maxCx, _maxCz;

        public int Count { get; private set; }

        /// <summary>Index of the nodes usable by <paramref name="profile"/> in <paramref name="graph"/>.</summary>
        public NearestNode(RouteGraph graph, Travel profile, bool incoming, double cellM = DefaultCellM)
            : this(graph, UsableNodes(graph, profile, incoming), cellM)
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
