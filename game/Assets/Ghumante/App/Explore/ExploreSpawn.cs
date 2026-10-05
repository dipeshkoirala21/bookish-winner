using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Routing;
using Ghumante.Core.Search;

namespace Ghumante.App.Explore
{
    /// <summary>Which region Explore opens (engine-free).</summary>
    public static class ExploreRegions
    {
        /// <summary>The full valley, built by the pipeline and imported with Ghumante > Import Region Pack.</summary>
        public const string Valley = "kathmandu_valley";

        /// <summary>The committed sample (Swayambhunath to Boudhanath), copied by Project Setup.</summary>
        public const string Sample = "kathmandu_core";

        /// <summary>The best installed region: the valley, else the sample, else the first (ordinal order); null for none.</summary>
        public static string Pick(IReadOnlyList<string> installed)
        {
            if (installed == null || installed.Count == 0) return null;
            string first = null;
            bool sample = false;
            for (int i = 0; i < installed.Count; i++)
            {
                string id = installed[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (id == Valley) return Valley;
                if (id == Sample) sample = true;
                if (first == null || string.CompareOrdinal(id, first) < 0) first = id;
            }
            return sample ? Sample : first;
        }
    }

    /// <summary>Where the explorer starts (or lands after a teleport): game metres and a heading along the road.</summary>
    public struct SpawnPoint
    {
        public double X, Z;

        /// <summary>Heading in radians (0 = north, clockwise), along the road when <see cref="OnRoad"/>.</summary>
        public float HeadingRad;

        /// <summary>Height hint (the routing node's elevation, metres) until the ground streams in.</summary>
        public float HeightHint;

        /// <summary>True when snapped onto a motorbike-routable road.</summary>
        public bool OnRoad;

        /// <summary>The routing node it snapped to (−1 when not on a road).</summary>
        public int Node;

        /// <summary>The place it was asked for (the search hit's name), or null.</summary>
        public NameRecord Place;
    }

    /// <summary>
    /// Finds the spawn (M1 track D): search the region for a place ("Thamel"), snap it to the nearest node a motorbike can
    /// start from (<see cref="NearestNode"/>, main network), and face along the biggest road leaving that node, a few
    /// metres into it so the scooter stands on the road, not in the middle of a junction. Engine-free; the same function
    /// places teleports.
    /// </summary>
    public static class ExploreSpawn
    {
        /// <summary>Where Explore starts.</summary>
        public const string DefaultPlace = "Thamel";

        /// <summary>How far along the chosen road the spawn sits (or less on a short edge).</summary>
        public const double IntoRoadM = 6.0;

        /// <summary>
        /// The spawn for <paramref name="query"/>: the first search hit, preferring a place over a POI among the top
        /// results, snapped to the road network (when there is one). Without a hit: the fallback point (the region
        /// centre), snapped the same way.
        /// </summary>
        public static SpawnPoint Find(SearchEngine search, RouteGraph graph, NearestNode starts, string query,
                                      double fallbackX, double fallbackZ)
        {
            double x = fallbackX, z = fallbackZ;
            NameRecord place = null;
            SearchEntry hit = Best(search, query);
            if (hit != null)
            {
                x = hit.X;
                z = hit.Z;
                place = hit.Name;
            }
            SpawnPoint spawn;
            if (!TryRoadSpawn(graph, starts, x, z, out spawn))
            {
                spawn = new SpawnPoint { X = x, Z = z, HeadingRad = 0f, HeightHint = 0f, OnRoad = false, Node = -1 };
            }
            spawn.Place = place;
            return spawn;
        }

        /// <summary>The top search hit for <paramref name="query"/>, preferring a place among the first three.</summary>
        public static SearchEntry Best(SearchEngine search, string query)
        {
            if (search == null || string.IsNullOrEmpty(query)) return null;
            List<SearchResult> results = search.Search(query, 3);
            if (results.Count == 0) return null;
            for (int i = 0; i < results.Count; i++)
                if (results[i].Entry.IsPlace) return results[i].Entry;
            return results[0].Entry;
        }

        /// <summary>
        /// Snaps (x, z) to the nearest node <paramref name="starts"/> holds (outgoing motorbike edges, main network) and
        /// faces along its biggest usable road: the lowest <see cref="RoadClass"/> value (motorway first), then the longest
        /// edge. False when there is no graph or no node.
        /// </summary>
        public static bool TryRoadSpawn(RouteGraph graph, NearestNode starts, double x, double z, out SpawnPoint spawn)
        {
            spawn = default(SpawnPoint);
            spawn.Node = -1;
            if (graph == null || starts == null) return false;
            int node = starts.Find(x, z);
            if (node < 0) return false;
            TravelProfile profile = TravelProfiles.Get(Travel.Motorbike);
            int best = -1;
            int bestRank = int.MaxValue;
            double bestLength = -1;
            for (int e = graph.Offsets[node]; e < graph.Offsets[node + 1]; e++)
            {
                if (double.IsPositiveInfinity(profile.EdgeTimeS(graph, e))) continue;
                int rank = graph.EdgeClass[e] == RoadClass.Unknown ? 100 : (int)graph.EdgeClass[e];
                double length = graph.EdgeLengthM(e);
                if (rank < bestRank || rank == bestRank && length > bestLength)
                {
                    best = e;
                    bestRank = rank;
                    bestLength = length;
                }
            }
            spawn.Node = node;
            spawn.HeightHint = graph.NodeElev[node] == RouteGraph.ElevUnknown ? 0f : graph.NodeElev[node];
            spawn.OnRoad = true;
            if (best < 0)
            {
                spawn.X = graph.NodeX(node);
                spawn.Z = graph.NodeZ(node);
                return true;
            }
            double[] g = graph.EdgeGeometry(best);
            double along = Math.Min(IntoRoadM, 0.4 * graph.EdgeLengthM(best));
            PointAlong(g, along, out spawn.X, out spawn.Z, out spawn.HeadingRad);
            return true;
        }

        /// <summary>The point <paramref name="distance"/> metres along an interleaved x, z polyline and the heading of
        /// the segment it lies on (radians, 0 = north, clockwise).</summary>
        public static void PointAlong(double[] xz, double distance, out double x, out double z, out float headingRad)
        {
            if (xz == null || xz.Length < 2) throw new ArgumentException("polyline needs at least one point", nameof(xz));
            x = xz[0];
            z = xz[1];
            headingRad = 0f;
            int n = xz.Length / 2;
            double left = Math.Max(0, distance);
            for (int i = 0; i + 1 < n; i++)
            {
                double ax = xz[2 * i], az = xz[2 * i + 1], bx = xz[2 * i + 2], bz = xz[2 * i + 3];
                double dx = bx - ax, dz = bz - az;
                double len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-9) continue;
                headingRad = (float)Math.Atan2(dx, dz);
                if (left <= len || i + 2 == n)
                {
                    double t = Math.Min(1.0, left / len);
                    x = ax + dx * t;
                    z = az + dz * t;
                    return;
                }
                left -= len;
            }
        }
    }
}
