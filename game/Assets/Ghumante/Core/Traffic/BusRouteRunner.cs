using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;

namespace Ghumante.Core.Traffic
{
    /// <summary>
    /// Buses, micros and tempos on the real routes of the region's <c>.ghrt</c> (W2_DESIGN 5.3, owner: "Add busses"):
    /// every route is followed along its ways through the resident lane graph (kerb lane), its real stops projected
    /// onto that path, and vehicles run to a timetable at an average 22 km/h including stops. Every route runs at a
    /// realistic peak headway of 4–8 minutes (<see cref="PeakHeadwayS"/>: the route's own, else by mode, varied per
    /// route), longer off peak and at night, so several buses are about in the city at any time. The timetable is
    /// anchored on the route itself (the stops' distances along it), so it does not jump when tiles stream in and out;
    /// a departure is one vehicle (keyed by route and departure), spawned only inside the simulation radius at the
    /// position its departure implies, so a bus always arrives along its route; it dwells 8–15 s at each stop and
    /// leaves the route as ordinary traffic where the resident path ends. Routed vehicles share the moving-vehicle cap
    /// (at most a third of it). Liveries follow the route's class (generic city green, minibus, microbus, Safa tempo,
    /// coach). Deterministic per seed; call <see cref="Step"/> on the traffic worker before <see cref="TrafficSim.Step"/>.
    /// </summary>
    public sealed class BusRouteRunner
    {
        /// <summary>Average route speed including stops (m/s).</summary>
        public const float RouteSpeedMps = 22f / 3.6f;

        /// <summary>Peak headway bounds of every route (owner request: realistic 4–8 minutes).</summary>
        public const float MinPeakHeadwayS = 240f, MaxPeakHeadwayS = 480f;

        /// <summary>Off-peak headways stretch with the inverse traffic density, at most this much.</summary>
        public const float MaxOffPeakStretch = 1f / 0.35f;

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
            public double Offset; // distance along the whole route of the segment's start (timetable anchor)
            public int Variant;
        }

        private readonly List<Segment> _segments = new List<Segment>();
        private readonly Dictionary<long, int> _spawned = new Dictionary<long, int>();
        private readonly List<long> _stale = new List<long>();

        /// <summary>Cap on routed vehicles alive at once (a share of the moving-vehicle cap).</summary>
        public int MaxRouted;

        public BusRouteRunner(RouteSet routes, LaneGraph g, TrafficSim sim)
        {
            _routes = routes ?? throw new ArgumentNullException(nameof(routes));
            _g = g ?? throw new ArgumentNullException(nameof(g));
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
            MaxRouted = Math.Max(2, sim.Settings.MaxVehicles / 3);
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

        /// <summary>
        /// The peak headway of a route in seconds, within <see cref="MinPeakHeadwayS"/>..<see cref="MaxPeakHeadwayS"/>:
        /// the route's own peak headway when the data has one (clamped), else by mode (micro 4 min, tempo 5, bus 6) varied
        /// by up to a minute either way per route (stable, from its relation id).
        /// </summary>
        public static float PeakHeadwayS(TransitRoute r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            float h;
            if (r.HeadwayPeakS > 0f)
            {
                h = r.HeadwayPeakS;
            }
            else
            {
                switch (r.Mode)
                {
                    case TransitMode.Microbus:
                        h = 240f;
                        break;
                    case TransitMode.Tempo:
                        h = 300f;
                        break;
                    default:
                        h = 360f;
                        break;
                }
                h += (float)(SimRng.Mix((ulong)r.OsmRelationId, 0x48454144) % 121UL) - 60f;
            }
            return h < MinPeakHeadwayS ? MinPeakHeadwayS : h > MaxPeakHeadwayS ? MaxPeakHeadwayS : h;
        }

        /// <summary>Headway in seconds at <paramref name="hour"/>: the peak headway at peak density, stretched off peak by
        /// the inverse density (or to the route's own off-peak headway when the data has a longer one), at most
        /// <see cref="MaxOffPeakStretch"/> times the peak one.</summary>
        public static float HeadwayS(TransitRoute r, float hour)
        {
            float peak = PeakHeadwayS(r);
            float d = TrafficTables.Density(hour);
            if (d >= 1f) return peak;
            float h = peak * Math.Min(MaxOffPeakStretch, 1f / Math.Max(0.05f, d));
            if (r.HeadwayOffS > h) h = Math.Min(r.HeadwayOffS, peak * MaxOffPeakStretch);
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
                    double head = HeadwayS(r, hour);
                    // Departure k leaves the route's start at k·head and is (t − k·head)·v along the route at time t; the
                    // ones on this segment now are those whose position lies in [Offset, Offset + Length).
                    double t = _clock;
                    long kHi = (long)Math.Floor((t - seg.Offset / RouteSpeedMps) / head);
                    long kLo = (long)Math.Floor((t - (seg.Offset + seg.Length) / RouteSpeedMps) / head);
                    for (long k = kHi; k >= kLo && alive < MaxRouted; k--)
                    {
                        long key = ((long)seg.Route << 32) ^ (k & 0xFFFFFFFFL);
                        int id;
                        if (_spawned.TryGetValue(key, out id) && _sim.IsAlive(id)) continue;
                        float along = (float)((t - k * head) * RouteSpeedMps - seg.Offset);
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
                Prune();
            }
        }

        /// <summary>Forgets departures whose vehicle is gone once the book grows (bounded bookkeeping, no per-step garbage).</summary>
        private void Prune()
        {
            if (_spawned.Count <= 1024) return;
            _stale.Clear();
            foreach (KeyValuePair<long, int> kv in _spawned)
                if (!_sim.IsAlive(kv.Value)) _stale.Add(kv.Key);
            _stale.Sort();
            foreach (long k in _stale) _spawned.Remove(k);
        }

        /// <summary>Routed vehicles alive now (tests).</summary>
        public int RoutedAlive
        {
            get { return _sim.RoutedCount(); }
        }

        private static int LaneAt(Segment seg, float along)
        {
            for (int i = seg.Lanes.Length - 1; i >= 0; i--)
                if (seg.Start[i] <= along)
                    return i;
            return -1;
        }

        /// <summary>Rebuilds the resident route segments and their plans (caller holds the graph lock); routed vehicles
        /// on the road keep their route where it is still resident.</summary>
        private void Rebuild()
        {
            _version = _g.Version;
            _segments.Clear();
            _sim.Routes.Clear();
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
                // Route successor of each lane: a route lane of the same or a later way, directly (plain joints and tile
                // seams link lane to lane) or through one connector (junctions).
                var succ = new Dictionary<int, KeyValuePair<int, int>>(); // lane → (connector or -1, next lane)
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
                        if (onRoute.Contains(c))
                        {
                            if (c == a) continue;
                            int oc = order[lc.WayId * 2 + (lc.Forward ? 1 : 0)];
                            if (oc < ord || oc - ord > 2) continue;
                            if (oc < bestOrd || oc == bestOrd && (bestC >= 0 || c < bestL))
                            {
                                bestOrd = oc;
                                bestC = -1;
                                bestL = c;
                            }
                            continue;
                        }
                        foreach (int b in lc.Next)
                        {
                            if (!onRoute.Contains(b) || b == a) continue;
                            LaneGraph.Lane lb = _g.Lanes[b];
                            int ob = order[lb.WayId * 2 + (lb.Forward ? 1 : 0)];
                            if (ob < ord || ob - ord > 2) continue;
                            if (ob < bestOrd || ob == bestOrd && bestC >= 0 && b < bestL)
                            {
                                bestOrd = ob;
                                bestC = c;
                                bestL = b;
                            }
                        }
                    }
                    if (bestL < 0 || hasPred.Contains(bestL)) continue;
                    succ[a] = new KeyValuePair<int, int>(bestC, bestL);
                    hasPred.Add(bestL);
                }
                // Chains from lanes without a route predecessor, then (closed loops) from the lowest lane left.
                var used = new HashSet<int>();
                for (int pass = 0; pass < 2; pass++)
                foreach (int start in lanes)
                {
                    if (pass == 0 && hasPred.Contains(start) || used.Contains(start)) continue;
                    var path = new List<int>();
                    int cur = start;
                    while (cur >= 0 && !used.Contains(cur))
                    {
                        used.Add(cur);
                        path.Add(cur);
                        KeyValuePair<int, int> nx;
                        if (!succ.TryGetValue(cur, out nx)) break;
                        if (nx.Key >= 0) path.Add(nx.Key);
                        cur = nx.Value;
                    }
                    if (path.Count == 0) continue;
                    if (_g.Lanes[path[path.Count - 1]].Kind != LaneKind.Road) path.RemoveAt(path.Count - 1);
                    AddSegment(ri, r, variant, path);
                }
            }
            _sim.RebindRoutes();
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
            // Stops projected onto the segment's road lanes (within 25 m), in path order.
            var stopLane = new List<int>();
            var stopS = new List<float>();
            var stopIdx = new List<int>();
            var stopPos = new List<float>();
            // A stop stands where the whole vehicle fits on its lane (its tail never in the junction behind).
            float minS = VehicleCatalog.At(variant).LengthM + 2f;
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
                        if (d < best && s > minS && s < l.Length - 2f)
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
            // The timetable anchor: where the segment starts along the whole route, from the stops on it (their distance
            // along the route minus their place on the segment; the median, so one odd stop does not shift it), else a
            // stable spot from the route and the segment's first way.
            if (stopPos.Count > 0)
            {
                var offs = new List<double>(stopPos.Count);
                for (int k = 0; k < stopPos.Count; k++) offs.Add(r.Stops[stopIdx[k]].AlongM - stopPos[k]);
                offs.Sort();
                seg.Offset = offs[offs.Count / 2];
            }
            else
            {
                long way = _g.Lanes[path[0]].WayId;
                double span = Math.Max(seg.Length, (double)RouteSpeedMps * MaxPeakHeadwayS);
                seg.Offset = (SimRng.Mix((ulong)r.OsmRelationId, (ulong)way) % 1000000UL) / 1000000.0 * span;
            }
            var plan = new TrafficSim.RoutePlan
            {
                Lanes = seg.Lanes, StopLane = stopLane.ToArray(), StopS = stopS.ToArray(), StopIndex = stopIdx.ToArray(), RouteIndex = ri,
            };
            seg.PlanIndex = _sim.Routes.Count;
            _sim.Routes.Add(plan);
            _segments.Add(seg);
        }

        /// <summary>The route (index into the route set) of a resident segment and its length (tests).</summary>
        public int RouteOf(int segment, out float lengthM)
        {
            lengthM = _segments[segment].Length;
            return _segments[segment].Route;
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
