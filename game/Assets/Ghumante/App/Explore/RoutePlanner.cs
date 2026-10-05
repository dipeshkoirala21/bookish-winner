using System;
using Ghumante.Core.Data;
using Ghumante.Core.Routing;

namespace Ghumante.App.Explore
{
    /// <summary>A route to follow: the polyline (interleaved x, z game metres), its length and time, and the destination.</summary>
    public sealed class PlannedRoute
    {
        public double[] Polyline;
        public double LengthM;

        /// <summary>Travel time of the whole route for the profile (Core's <see cref="TravelProfiles"/>), seconds.</summary>
        public double TimeS;

        /// <summary>Where the player wanted to go (the search hit), which may be off the road the route ends on.</summary>
        public double DestinationX, DestinationZ;

        public int FromNode, ToNode;

        public double EndX
        {
            get { return Polyline[Polyline.Length - 2]; }
        }

        public double EndZ
        {
            get { return Polyline[Polyline.Length - 1]; }
        }
    }

    /// <summary>
    /// Routes for "Ride there" (ARCHITECTURE.md 7.8): Core's <see cref="AStar"/> with the motorbike profile from the node
    /// nearest the explorer (outgoing edges, main network) to the node nearest the destination (incoming edges), so every
    /// start reaches every destination. Building the snapping indexes takes a few tens of milliseconds on the sample
    /// region: construct it on a worker. <see cref="Plan"/> is safe to call from any thread (one route at a time).
    /// Engine-free.
    /// </summary>
    public sealed class RoutePlanner
    {
        private readonly object _lock = new object();

        public RoutePlanner(RouteGraph graph, Travel profile = Travel.Motorbike)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            Profile = profile;
            Starts = new NearestNode(graph, profile, false);
            Ends = new NearestNode(graph, profile, true);
            AStar = new AStar(graph);
        }

        public RouteGraph Graph { get; private set; }
        public Travel Profile { get; private set; }

        /// <summary>Nodes a route may start from (also where spawns and teleports snap).</summary>
        public NearestNode Starts { get; private set; }

        /// <summary>Nodes a route may end at.</summary>
        public NearestNode Ends { get; private set; }

        public AStar AStar { get; private set; }

        /// <summary>The fastest route from (fromX, fromZ) to (toX, toZ), or null when there is none.</summary>
        public PlannedRoute Plan(double fromX, double fromZ, double toX, double toZ)
        {
            lock (_lock)
            {
                int src = Starts.Find(fromX, fromZ);
                int dst = Ends.Find(toX, toZ);
                if (src < 0 || dst < 0) return null;
                Route route = AStar.FindRoute(src, dst, Profile);
                if (route == null) return null;
                double[] polyline = AStar.RouteGeometry(route);
                if (polyline.Length < 2) return null;
                if (polyline.Length == 2)
                {
                    // Start and end on the same node: a one-point "route" still has an end to arrive at.
                    polyline = new[] { polyline[0], polyline[1], polyline[0], polyline[1] };
                }
                return new PlannedRoute
                {
                    Polyline = polyline,
                    LengthM = route.LengthM,
                    TimeS = route.TimeS,
                    DestinationX = toX,
                    DestinationZ = toZ,
                    FromNode = src,
                    ToNode = dst,
                };
            }
        }
    }
}
