using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;

namespace Ghumante.Core.Traffic
{
    /// <summary>
    /// Buses, micros and tempos on the real routes of the region's <c>.ghrt</c> (W2_DESIGN 5.3): every route is followed
    /// along its ways through the resident lane graph (kerb lane), its stops projected onto that path, and vehicles run
    /// to a timetable (headway by mode and time of day, or the route's own) at an average 22 km/h including stops. A
    /// vehicle is spawned only inside the simulation radius, at the position its departure implies, so a bus always
    /// arrives along its route; it dwells 8–15 s at each stop and leaves the route as ordinary traffic where the
    /// resident path ends. Liveries follow the route's class (generic city green, minibus, microbus, Safa tempo, coach).
    /// Deterministic per seed; call <see cref="Step"/> on the traffic worker before <see cref="TrafficSim.Step"/>.
    /// </summary>
    public sealed class BusRouteRunner
    {
        /// <summary>Average route speed including stops (m/s).</summary>
        public const float RouteSpeedMps = 22f / 3.6f;

        private readonly RouteSet _routes;
        private readonly LaneGraph _g;
        private readonly TrafficSim _sim;
        private int _version = -1;
        private double _clock;

        private sealed class Segment
        {
            public int Route;
            public int PlanIndex; // into TrafficSim.Routes
            public int[] Lanes; // route path including connectors
            public float[] Start; // arc length of each lane's start along the segment
            public float Length;
            public float Phase;
            public int Variant;
        }

        private readonly List<Segment> _segments = new List<Segment>();
        private readonly Dictionary<long, int> _spawned = new Dictionary<long, int>();

        /// <summary>Cap on routed vehicles alive at once (a share of the moving-vehicle cap).</summary>
        public int MaxRouted;

        public BusRouteRunner(RouteSet routes, LaneGraph g, TrafficSim sim)
        {
            _routes = routes ?? throw new ArgumentNullException(nameof(routes));
            _g = g ?? throw new ArgumentNullException(nameof(g));
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
            MaxRouted = Math.Max(2, sim.Settings.MaxVehicles / 4);
        }

        /// <summary>Resident route segments (after the last <see cref="Step"/>).</summary>
        public int SegmentCount
        {
            get { return _segments.Count; }
        }

        /// <summary>Total resident route length in metres.</summary>
        public float ResidentLengthM
        {
            get
            {
                float s = 0f;
                foreach (Segment g in _segments) s += g.Length;
                return s;
            }
        }

        /// <summary>The catalogue variant a route runs (generic liveries only; no operator livery).</summary>
        public static int VariantFor(TransitRoute r)
        {
            switch (r.Livery)
            {
                case LiveryClass.CityGreen: return VehicleCatalog.BusCityGreen;
                case LiveryClass.Minibus: return VehicleCatalog.Minibus;
                case LiveryClass.Microbus: return VehicleCatalog.Microbus;
                case LiveryClass.SafaTempo: return VehicleCatalog.Tempo;
                case LiveryClass.Coach: return VehicleCatalog.Coach;
            }
            switch (r.Mode)
            {
                case TransitMode.Microbus: return VehicleCatalog.Microbus;
                case TransitMode.Tempo: return VehicleCatalog.Tempo;
                default: return VehicleCatalog.Minibus;
            }
        }

        /// <summary>Headway in seconds by mode and density (W2_DESIGN 5.3): bus 8 min (5 at peak), micro 5 (3), tempo 6;
        /// the route's own headways when the data has them; scaled by the inverse density off peak.</summary>
        public static float HeadwayS(TransitRoute r, float hour)
        {
            float d = TrafficTables.Density(hour);
            bool peak = d >= 1f;
            float data = peak ? r.HeadwayPeakS : r.HeadwayOffS;
            float h;
            if (data > 0f) h = data;
            else
            {
                switch (r.Mode)
                {
                    case TransitMode.Microbus:
                        h = peak ? 180f : 300f;
                        break;
                    case TransitMode.Tempo:
                        h = 360f;
                        break;
                    default:
                        h = peak ? 300f : 480f;
                        break;
                }
            }
            if (!peak) h /= Math.Max(0.15f, d);
            return h;
        }

        public void Step(float dt, float hour)
        {
            if (!(dt > 0f)) return;
            _clock += dt;
            lock (_g.SyncRoot)
            {
                if (_g.Version != _version) Rebuild();
                double fx, fz;
                if (!_sim.TryFocus(out fx, out fz)) return;
                int alive = _sim.RoutedCount();
                float radius = _sim.Settings.FullRadiusM;
                foreach (Segment seg in _segments)
                {
                    if (alive >= MaxRouted) break;
                    TransitRoute r = _routes.Routes[seg.Route];
                    float head = HeadwayS(r, hour);
                    double t = _clock + seg.Phase;
                    // Departures whose vehicle is on the segment now: position (t − k·head) · v in [0, length).
                    long kHi = (long)Math.Floor(t / head);
                    long kLo = (long)Math.Floor((t - seg.Length / RouteSpeedMps) / head);
                    for (long k = Math.Max(0, kLo); k <= kHi && alive < MaxRouted; k++)
                    {
                        long key = ((long)seg.PlanIndex << 40) ^ k;
                        int id;
                        if (_spawned.TryGetValue(key, out id)) continue;
                        float along = (float)((t - k * head) * RouteSpeedMps);
                        if (along < 0f || along >= seg.Length) continue;
                        int li = LaneAt(seg, along);
                        if (li < 0) continue;
                        int lane = seg.Lanes[li];
                        LaneGraph.Lane l = _g.Lanes[lane];
                        if (l.Kind != LaneKind.Road) continue;
                        float s = along - seg.Start[li];
                        double x, z;
                        float y, h;
                        LaneGraph.PointAt(l, s, out x, out z, out y, out h);
                        double dx = x - fx, dz = z - fz;
                        double dist = Math.Sqrt(dx * dx + dz * dz);
                        if (dist > radius) continue;
                        if (dist < _sim.Settings.SpawnMinM && !_sim.OutOfView(x, z)) continue;
                        byte livery = VehicleCatalog.At(seg.Variant).PickLivery((uint)SimRng.Mix((ulong)r.OsmRelationId, (ulong)k));
                        if (_sim.SpawnRouted(lane, s, seg.Variant, livery, seg.PlanIndex, li, out id))
                        {
                            _spawned[key] = id;
                            alive++;
                        }
                    }
                }
                // Forget departures long gone (bounded bookkeeping).
                if (_spawned.Count > 4096) _spawned.Clear();
            }
        }

        private static int LaneAt(Segment seg, float along)
        {
            for (int i = seg.Lanes.Length - 1; i >= 0; i--)
                if (seg.Start[i] <= along)
                    return i;
            return -1;
        }

        /// <summary>Rebuilds the resident route segments and their plans (caller holds the graph lock).</summary>
        private void Rebuild()
        {
            _version = _g.Version;
            _segments.Clear();
            _sim.Routes.Clear();
            _spawned.Clear();
            for (int ri = 0; ri < _routes.Routes.Count; ri++)
            {
                TransitRoute r = _routes.Routes[ri];
                if (r.Mode != TransitMode.Bus && r.Mode != TransitMode.Microbus && r.Mode != TransitMode.Tempo) continue;
                if (r.WayIds == null || r.WayIds.Length == 0) continue;
                int variant = VariantFor(r);
                uint bit = VehicleClasses.Bit(VehicleCatalog.At(variant).TrafficClass);
                // Route order of every (way, direction).
                var order = new Dictionary<long, int>();
                for (int i = 0; i < r.WayIds.Length; i++)
                {
                    long k = r.WayIds[i] * 2 + (r.WayForward[i] ? 1 : 0);
                    if (!order.ContainsKey(k)) order[k] = i;
                }
                // Kerb lanes of the route's ways in the route's direction.
                var lanes = new List<int>();
                for (int i = 0; i < r.WayIds.Length; i++)
                    foreach (LaneId l in _g.LanesOfWay(r.WayIds[i]))
                    {
                        LaneGraph.Lane x = _g.Lanes[l.Value];
                        if (x.Kind != LaneKind.Road || x.Index != 0 || x.Forward != r.WayForward[i] || (x.Mask & bit) == 0) continue;
                        if (!lanes.Contains(l.Value)) lanes.Add(l.Value);
                    }
                lanes.Sort();
                var onRoute = new HashSet<int>(lanes);
                // Route successor of each lane: through one connector to a route lane of the same or a later way.
                var succ = new Dictionary<int, KeyValuePair<int, int>>(); // lane → (connector, next lane)
                var hasPred = new HashSet<int>();
                foreach (int a in lanes)
                {
                    LaneGraph.Lane la = _g.Lanes[a];
                    int ord = order[la.WayId * 2 + (la.Forward ? 1 : 0)];
                    int bestC = -1, bestL = -1, bestOrd = int.MaxValue;
                    foreach (int c in la.Next)
                    {
                        LaneGraph.Lane lc = _g.Lanes[c];
                        if ((lc.Mask & bit) == 0) continue;
                        foreach (int b in lc.Next)
                        {
                            if (!onRoute.Contains(b) || b == a) continue;
                            LaneGraph.Lane lb = _g.Lanes[b];
                            int ob = order[lb.WayId * 2 + (lb.Forward ? 1 : 0)];
                            if (ob < ord || ob - ord > 2) continue;
                            if (ob < bestOrd || ob == bestOrd && b < bestL)
                            {
                                bestOrd = ob;
                                bestC = c;
                                bestL = b;
                            }
                        }
                    }
                    if (bestL < 0) continue;
                    succ[a] = new KeyValuePair<int, int>(bestC, bestL);
                    hasPred.Add(bestL);
                }
                // Chains from lanes without a route predecessor.
                var used = new HashSet<int>();
                foreach (int start in lanes)
                {
                    if (hasPred.Contains(start) || used.Contains(start)) continue;
                    var path = new List<int>();
                    int cur = start;
                    while (cur >= 0 && !used.Contains(cur))
                    {
                        used.Add(cur);
                        path.Add(cur);
                        KeyValuePair<int, int> nx;
                        if (!succ.TryGetValue(cur, out nx)) break;
                        path.Add(nx.Key);
                        cur = nx.Value;
                    }
                    if (path.Count == 0) continue;
                    if (_g.Lanes[path[path.Count - 1]].Kind != LaneKind.Road) path.RemoveAt(path.Count - 1);
                    AddSegment(ri, r, variant, path);
                }
            }
        }

        private void AddSegment(int ri, TransitRoute r, int variant, List<int> path)
        {
            var seg = new Segment { Route = ri, Lanes = path.ToArray(), Start = new float[path.Count], Variant = variant };
            float acc = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                seg.Start[i] = acc;
                acc += _g.Lanes[path[i]].Length;
            }
            seg.Length = acc;
            if (seg.Length < 30f) return;
            var rng = new SimRng(SimRng.Mix((ulong)r.OsmRelationId, (ulong)path[0]));
            seg.Phase = rng.Range(0f, 600f);
            // Stops projected onto the segment's road lanes (within 25 m), in path order.
            var stopLane = new List<int>();
            var stopS = new List<float>();
            var stopIdx = new List<int>();
            var stopPos = new List<float>();
            if (r.Stops != null)
                for (int k = 0; k < r.Stops.Length; k++)
                {
                    TransitStop st = r.Stops[k];
                    double best = 25.0;
                    int bl = -1;
                    float bs = 0f, bp = 0f;
                    for (int i = 0; i < path.Count; i++)
                    {
                        LaneGraph.Lane l = _g.Lanes[path[i]];
                        if (l.Kind != LaneKind.Road) continue;
                        float s;
                        double d = LaneGraph.Project(l, st.X, st.Z, out s);
                        if (d < best && s > 2f && s < l.Length - 2f)
                        {
                            best = d;
                            bl = path[i];
                            bs = s;
                            bp = seg.Start[i] + s;
                        }
                    }
                    if (bl < 0) continue;
                    int at = 0;
                    while (at < stopPos.Count && stopPos[at] <= bp) at++;
                    stopPos.Insert(at, bp);
                    stopLane.Insert(at, bl);
                    stopS.Insert(at, bs);
                    stopIdx.Insert(at, k);
                }
            var plan = new TrafficSim.RoutePlan
            {
                Lanes = seg.Lanes, StopLane = stopLane.ToArray(), StopS = stopS.ToArray(), StopIndex = stopIdx.ToArray(), RouteIndex = ri,
            };
            seg.PlanIndex = _sim.Routes.Count;
            _sim.Routes.Add(plan);
            _segments.Add(seg);
        }

        /// <summary>The stops of a resident segment's plan (tests): lane ids and arc lengths.</summary>
        public int StopsOf(int segment, out int routeIndex)
        {
            Segment s = _segments[segment];
            routeIndex = s.Route;
            return _sim.Routes[s.PlanIndex].StopLane.Length;
        }
    }
}
