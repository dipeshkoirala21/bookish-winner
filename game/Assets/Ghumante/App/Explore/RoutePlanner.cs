using System;
using System.Collections.Generic;
using Ghumante.Core.Characters;
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

        /// <summary>The travel profile the route was planned for (<see cref="RoutePlanner.ProfileFor"/>).</summary>
        public Travel Profile = Travel.Motorbike;

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
    /// Routes for "Ride there" (ARCHITECTURE.md 7.8): Core's <see cref="AStar"/> from the node nearest the explorer
    /// (outgoing edges, main network) to the node nearest the destination (incoming edges), so every start reaches every
    /// destination, with the travel profile of how the explorer travels (<see cref="ProfileFor"/>, W2 detail pass): in a
    /// car, taxi, SUV, micro, bus, truck or tractor the GHRG car profile, whose access masks leave out every street a car
    /// cannot use (gallis, real width under about 3 m, footways, paths, steps, pedestrian streets, <c>motorcar=no</c>;
    /// W2_DETAIL_CONTRACT decision 5); on a motorbike or scooter the motorbike profile; on a bicycle the bicycle profile;
    /// on foot the foot profile. The snapping indexes of a profile are built on first use (a few tens of milliseconds on
    /// the valley graph): construct the planner and plan on a worker. <see cref="Plan(double, double, double, double, Travel)"/>
    /// is safe to call from any thread (one route at a time). Engine-free.
    /// </summary>
    public sealed class RoutePlanner
    {
        private readonly object _lock = new object();
        private readonly Dictionary<Travel, NearestNode> _starts = new Dictionary<Travel, NearestNode>();
        private readonly Dictionary<Travel, NearestNode> _ends = new Dictionary<Travel, NearestNode>();

        public RoutePlanner(RouteGraph graph, Travel profile = Travel.Motorbike)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            TravelProfiles.Get(profile); // a single profile bit, or throw
            Profile = profile;
            Starts = new NearestNode(graph, profile, false);
            Ends = new NearestNode(graph, profile, true);
            _starts[profile] = Starts;
            _ends[profile] = Ends;
            AStar = new AStar(graph);
        }

        public RouteGraph Graph { get; private set; }

        /// <summary>The default profile (spawns and teleports snap with it).</summary>
        public Travel Profile { get; private set; }

        /// <summary>Nodes a route of the default profile may start from (also where spawns and teleports snap).</summary>
        public NearestNode Starts { get; private set; }

        /// <summary>Nodes a route of the default profile may end at.</summary>
        public NearestNode Ends { get; private set; }

        public AStar AStar { get; private set; }

        /// <summary>
        /// The routing profile for the explorer's rig (W2 detail pass, owner: "if I am on a car, avoid the roads that
        /// have narrower streets through which cars cannot pass"): <see cref="Travel.Car"/> for every four-wheeler the
        /// player drives (car, taxi, SUV, micro, tempo, bus, truck, tractor), <see cref="Travel.Motorbike"/> for scooters
        /// and motorbikes, <see cref="Travel.Bicycle"/> for bicycles, <see cref="Travel.Foot"/> on foot. Riding along it is
        /// the profile of the vehicle ridden (<paramref name="passengerOf"/>; a cycle rickshaw pedals like a bicycle).
        /// </summary>
        public static Travel ProfileFor(RigClass rig, RigClass passengerOf = RigClass.Car)
        {
            switch (rig)
            {
                case RigClass.Walk: return Travel.Foot;
                case RigClass.Bicycle: return Travel.Bicycle;
                case RigClass.TwoWheeler: return Travel.Motorbike;
                case RigClass.Car:
                case RigClass.Van:
                case RigClass.Bus:
                case RigClass.Truck:
                case RigClass.Tractor: return Travel.Car;
                case RigClass.Passenger:
                    if (passengerOf == RigClass.Passenger) return Travel.Bicycle; // the cycle rickshaw
                    if (passengerOf == RigClass.Walk) return Travel.Car;
                    return ProfileFor(passengerOf);
                default: return Travel.Motorbike;
            }
        }

        /// <summary>Nodes a route of <paramref name="profile"/> may start from (built on first use).</summary>
        public NearestNode StartsFor(Travel profile)
        {
            lock (_lock)
            {
                return Index(_starts, profile, false);
            }
        }

        /// <summary>The fastest route from (fromX, fromZ) to (toX, toZ) with the default profile, or null.</summary>
        public PlannedRoute Plan(double fromX, double fromZ, double toX, double toZ)
        {
            return Plan(fromX, fromZ, toX, toZ, Profile);
        }

        /// <summary>The fastest route from (fromX, fromZ) to (toX, toZ) for <paramref name="profile"/>, or null when there
        /// is none. The route only uses edges whose access mask lets the profile through.</summary>
        public PlannedRoute Plan(double fromX, double fromZ, double toX, double toZ, Travel profile)
        {
            TravelProfiles.Get(profile);
            lock (_lock)
            {
                int src = Index(_starts, profile, false).Find(fromX, fromZ);
                int dst = Index(_ends, profile, true).Find(toX, toZ);
                if (src < 0 || dst < 0) return null;
                Route route = AStar.FindRoute(src, dst, profile);
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
                    Profile = profile,
                };
            }
        }

        private NearestNode Index(Dictionary<Travel, NearestNode> cache, Travel profile, bool incoming)
        {
            NearestNode n;
            if (!cache.TryGetValue(profile, out n))
            {
                n = new NearestNode(Graph, profile, incoming);
                cache[profile] = n;
            }
            return n;
        }
    }
}
