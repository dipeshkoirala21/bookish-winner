using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Traffic
{
    /// <summary>Per-tier pedestrian settings (W2_DESIGN 5.4; NPC caps 25 / 60 / 120).</summary>
    public sealed class PedestrianSettings
    {
        public int MaxNpcs = 60;
        public float RadiusM = 180f;
        public float SpawnMinM = 25f;
        public float ViewHalfAngleDeg = 55f;

        public static PedestrianSettings Low()
        {
            return new PedestrianSettings { MaxNpcs = 25, RadiusM = 120f };
        }

        public static PedestrianSettings Mid()
        {
            return new PedestrianSettings { MaxNpcs = 60, RadiusM = 180f };
        }

        public static PedestrianSettings High()
        {
            return new PedestrianSettings { MaxNpcs = 120, RadiusM = 250f };
        }
    }

    /// <summary>
    /// Pedestrian sim v1 (W2_DESIGN 5.4): people walk the footpaths of the road profiles (both sides), the edges of shared
    /// streets without footpaths, OSM footways, paths, steps and pedestrian streets; they cross at junction corners and at
    /// OSM crossings (and register with the <see cref="TrafficSim"/> while on the carriageway, so cars yield); they wander
    /// inside temple compounds, courtyards and heritage squares (free walk, half of them standing or sitting) and walk the
    /// kora clockwise around stupas (anticlockwise only at Bon sites). Density follows the area type and the S §14 daily
    /// curve within the NPC cap; archetypes follow the S §3.2 mix; 8–12% carry a doko, sack, gas cylinder or baby, and
    /// umbrellas come out in the rain. Contact is never harm: anyone in the hop-aside capsule of the player's vehicle hops
    /// aside (<see cref="ContactRules"/>). Deterministic per seed; worker-thread step, double-buffered snapshot.
    /// </summary>
    public sealed class PedestrianSim
    {
        public const int ObstacleIdBase = 0x30000000;
        public const float CrossingRadiusM = 0.4f;

        private enum EdgeKind : byte
        {
            Sidewalk = 0,
            StreetEdge = 1,
            Path = 2,
            Corner = 3,
            Crossing = 4,
        }

        private sealed class WalkEdge
        {
            public int Id;
            public ulong Tile;
            public EdgeKind Kind;
            public double[] X, Z;
            public float[] Y, S;
            public int N;
            public float Length;
            public long A, B; // endpoint node keys (1 m grid)
            public AreaType Area;
            public bool Alive = true;
        }

        private readonly LaneGraph _g;
        private readonly PedestrianSettings _s;
        private readonly ulong _seed;
        private SimRng _rng;
        private readonly object _inputLock = new object();
        private readonly List<WalkEdge> _edges = new List<WalkEdge>();
        private readonly Stack<int> _freeEdges = new Stack<int>();
        private readonly Dictionary<long, List<int>> _nodes = new Dictionary<long, List<int>>();
        private readonly Dictionary<ulong, List<int>> _tileEdges = new Dictionary<ulong, List<int>>();
        private readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
        private readonly Dictionary<ulong, TileHeightSampler> _heights = new Dictionary<ulong, TileHeightSampler>();
        private int _heightLevel = -1;
        private const double GridM = 32.0;

        private struct Ped
        {
            public bool Alive;
            public int Id;
            public byte Archetype, Tint, Clip;
            public ushort Carry;
            public float Speed, Personal;
            // Walking on an edge.
            public int Edge;
            public float S;
            public sbyte Dir;
            public float Side;
            // Free walk inside a zone, or the kora.
            public byte Mode; // 0 edge, 1 zone wander, 2 kora, 3 standing
            public long ZoneRef;
            public double TX, TZ, CX, CZ;
            public float Radius, Angle, AngVel, Wait;
            public double X, Z;
            public float Y, Heading, ClipTime;
            public float HopT, HopSide;
            public bool Hopping, OnCarriageway;
            public ulong Rng;
        }

        private Ped[] _peds;
        private int _nextId = 1;
        private double _fx, _fz, _pfx, _pfz;
        private bool _hasFocus, _pHas;
        private float _viewX, _viewZ, _pViewX, _pViewZ;
        private bool _hasView, _pHasView;
        private bool _playerVeh, _pPlayerVeh;
        private double _vx, _vz, _pvx, _pvz;
        private float _vHeading, _vSpeed, _vWidth, _vFront, _pvHeading, _pvSpeed, _pvWidth, _pvFront;
        private float _spawnT;
        private readonly List<int> _near = new List<int>();
        private readonly HashSet<int> _pushed = new HashSet<int>();
        private readonly HashSet<int> _keep = new HashSet<int>();

        private readonly object _snapLock = new object();
        private PedPose[] _front = new PedPose[0], _back = new PedPose[0];
        private int _frontCount;

        public SacredZoneIndex Sacred;
        public TrafficSim Traffic;

        public PedestrianSim(LaneGraph g, PedestrianSettings s, ulong seed)
        {
            _g = g;
            _s = s ?? new PedestrianSettings();
            _seed = seed;
            _rng = new SimRng(SimRng.Mix(seed, 0x50454453));
            _peds = new Ped[Math.Max(8, _s.MaxNpcs + 8)];
            _front = new PedPose[_peds.Length];
            _back = new PedPose[_peds.Length];
        }

        public int EdgeCount
        {
            get
            {
                int n = 0;
                foreach (WalkEdge e in _edges)
                    if (e.Alive)
                        n++;
                return n;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Walk graph
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Builds the walk graph of a tile: footpaths, street edges, paths, corner and crossing links.</summary>
        public void AddTile(TileId id, TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var built = new List<WalkEdge>();
            if (t.Roads.Count > 0)
            {
                RoadLayout lay = RoadLayout.For(t);
                var sampler = new TileHeightSampler(t, 1);
                var opts = new RoadOptions();
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    RoadRecord r = t.Roads[ri];
                    if ((r.Flags & RoadFlags.Tunnel) != 0 || !RoadMesher.IsDrawn(r, opts)) continue;
                    int first, last;
                    Driving.RoadSpatialIndex.RenderedRange(r, out first, out last);
                    if (last <= first) continue;
                    RoadWidthProfile prof = lay.Profiles[ri];
                    RoadAttrRecord a = lay.Attrs[ri];
                    AreaType area = RoadWidthModel.AreaOf(a);
                    bool foot = RoadWidthModel.IsFootClass(r.RoadClass) || r.RoadClass == RoadClass.Pedestrian || r.RoadClass == RoadClass.Track;
                    bool bridge = (r.Flags & RoadFlags.Bridge) != 0;
                    float lift = RoadMesher.LiftOf(r, opts);
                    if (foot)
                    {
                        built.Add(Line(id, t, r, first, last, prof, a, sampler, lift, 0, EdgeKind.Path, area));
                        continue;
                    }
                    bool dual = a.Has(RoadAttrFlags.Dual);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        // side −1: left of point order; +1: right. A dual carriageway has its median on the right.
                        if (side > 0 && dual) continue;
                        float fw = prof.Sample(side < 0 ? prof.FootLeft : prof.FootRight, 0.5 * prof.LengthM);
                        EdgeKind k = fw > 0.3f && !bridge ? EdgeKind.Sidewalk : EdgeKind.StreetEdge;
                        built.Add(Line(id, t, r, first, last, prof, a, sampler, lift + (k == EdgeKind.Sidewalk ? opts.KerbHeightM : 0f), side,
                                       k, area));
                    }
                }
                // Corners and crossings: link line ends near each other (within 9 m) at junction nodes.
                var ends = new List<KeyValuePair<int, bool>>(); // (edge, at start)
                for (int i = 0; i < built.Count; i++)
                {
                    if (built[i] == null) continue;
                    ends.Add(new KeyValuePair<int, bool>(i, true));
                    ends.Add(new KeyValuePair<int, bool>(i, false));
                }
                int baseCount = built.Count;
                for (int p = 0; p < ends.Count; p++)
                for (int q = p + 1; q < ends.Count; q++)
                {
                    WalkEdge ea = built[ends[p].Key], eb = built[ends[q].Key];
                    if (ends[p].Key == ends[q].Key) continue;
                    int ia = ends[p].Value ? 0 : ea.N - 1, ib = ends[q].Value ? 0 : eb.N - 1;
                    double dx = ea.X[ia] - eb.X[ib], dz = ea.Z[ia] - eb.Z[ib];
                    double d = Math.Sqrt(dx * dx + dz * dz);
                    if (d < 0.3 || d > 9.0) continue;
                    var link = new WalkEdge
                    {
                        Kind = d > 3.0 ? EdgeKind.Crossing : EdgeKind.Corner, X = new[] { ea.X[ia], eb.X[ib] }, Z = new[] { ea.Z[ia], eb.Z[ib] },
                        Y = new[] { ea.Y[ia], eb.Y[ib] }, S = new float[2], N = 2, Area = ea.Area,
                    };
                    Finish(link);
                    built.Add(link);
                }
                // OSM crossings: link the nearest street side to the nearest one across the crossing point.
                foreach (PropRecord pr in t.Props)
                {
                    if (pr.Kind != ObjectKind.CrossingMarked && pr.Kind != ObjectKind.CrossingUnmarked) continue;
                    double px = id.X0 + pr.XCm / 100.0, pz = id.Z0 + pr.ZCm / 100.0;
                    int ea = -1, eb = -1;
                    double ax = 0, az = 0, bx = 0, bz = 0, da = 15.0, db = 15.0;
                    float ay = 0f, by = 0f;
                    for (int pass = 0; pass < 2; pass++)
                        for (int i = 0; i < baseCount; i++)
                        {
                            WalkEdge e = built[i];
                            if (e == null || e.Kind == EdgeKind.Path || i == ea) continue;
                            float s0;
                            double d = ProjectEdge(e, px, pz, out s0);
                            double qx, qz;
                            float qy, qh;
                            PointOn(e, s0, out qx, out qz, out qy, out qh);
                            if (pass == 0 && d < da)
                            {
                                ea = i;
                                da = d;
                                ax = qx;
                                az = qz;
                                ay = qy;
                            }
                            else if (pass == 1 && d < db && (qx - px) * (ax - px) + (qz - pz) * (az - pz) < 0)
                            {
                                eb = i;
                                db = d;
                                bx = qx;
                                bz = qz;
                                by = qy;
                            }
                        }
                    if (ea < 0 || eb < 0) continue;
                    var link = new WalkEdge
                    {
                        Kind = EdgeKind.Crossing, X = new[] { ax, bx }, Z = new[] { az, bz }, Y = new[] { ay, by }, S = new float[2], N = 2,
                        Area = built[ea].Area,
                    };
                    Finish(link);
                    if (link.Length > 0.5f && link.Length < 40f) built.Add(link);
                }
            }
            lock (_inputLock)
            {
                RemoveTileLocked(id.Key);
                var list = new List<int>();
                foreach (WalkEdge e in built)
                {
                    if (e == null || e.Length < 0.5f) continue;
                    e.Tile = id.Key;
                    e.A = NodeKey(e.X[0], e.Z[0]);
                    e.B = NodeKey(e.X[e.N - 1], e.Z[e.N - 1]);
                    int eid;
                    if (_freeEdges.Count > 0)
                    {
                        eid = _freeEdges.Pop();
                        _edges[eid] = e;
                    }
                    else
                    {
                        eid = _edges.Count;
                        _edges.Add(e);
                    }
                    e.Id = eid;
                    list.Add(eid);
                    AddNode(e.A, eid);
                    AddNode(e.B, eid);
                    AddGrid(e);
                }
                _tileEdges[id.Key] = list;
                if (t.HeightsQ != null)
                {
                    _heights[id.Key] = new TileHeightSampler(t, 1);
                    _heightLevel = id.Level;
                }
            }
        }

        public void RemoveTile(TileId id)
        {
            lock (_inputLock) RemoveTileLocked(id.Key);
        }

        private void RemoveTileLocked(ulong key)
        {
            List<int> list;
            if (!_tileEdges.TryGetValue(key, out list)) return;
            foreach (int eid in list)
            {
                WalkEdge e = _edges[eid];
                e.Alive = false;
                RemoveNode(e.A, eid);
                RemoveNode(e.B, eid);
                RemoveGrid(e);
                _freeEdges.Push(eid);
            }
            _tileEdges.Remove(key);
            _heights.Remove(key);
        }

        /// <summary>Terrain height under a free walker (zones and koras), from the resident tile containing it.</summary>
        private float GroundY(double x, double z, float fallback)
        {
            if (_heightLevel < 0) return fallback;
            TileHeightSampler h;
            float y;
            if (_heights.TryGetValue(TileId.At(_heightLevel, x, z).Key, out h) && h.TryHeightClamped(x, z, out y)) return y;
            return fallback;
        }

        /// <summary>A walk line along a road piece at a lateral offset: side 0 the centreline, ±1 the carriageway edge
        /// (beyond it onto the footpath's middle when there is one, else 0.6 m inside the edge).</summary>
        private static WalkEdge Line(TileId id, TileData t, RoadRecord r, int first, int last, RoadWidthProfile prof, RoadAttrRecord a,
                                     TileHeightSampler sampler, float lift, int side, EdgeKind kind, AreaType area)
        {
            int[] p = r.Points;
            var xs = new List<double>();
            var zs = new List<double>();
            var ys = new List<float>();
            double along = 0;
            bool dual = a.Has(RoadAttrFlags.Dual);
            for (int i = first; i <= last; i++)
            {
                double x = id.X0 + p[2 * i] / 100.0, z = id.Z0 + p[2 * i + 1] / 100.0;
                if (i > first) along += Math.Sqrt(Sq((p[2 * i] - p[2 * i - 2]) / 100.0) + Sq((p[2 * i + 1] - p[2 * i - 1]) / 100.0));
                // Right normal of the local direction.
                int i0 = i > first ? i - 1 : i, i1 = i > first ? i : i + 1;
                double dx = (p[2 * i1] - p[2 * i0]) / 100.0, dz = (p[2 * i1 + 1] - p[2 * i0 + 1]) / 100.0;
                double l = Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-9)
                {
                    dx = 0;
                    dz = 1;
                    l = 1;
                }
                double nx = dz / l, nz = -dx / l;
                float w = prof.WidthAt(along);
                float shift = dual ? -0.5f * (w - prof.RealM) : 0f;
                float lat = 0f;
                if (side != 0)
                {
                    float fw = prof.Sample(side < 0 ? prof.FootLeft : prof.FootRight, along);
                    lat = kind == EdgeKind.Sidewalk ? side * (0.5f * w + 0.5f * fw) : side * Math.Max(0f, 0.5f * w - 0.6f);
                    lat += shift;
                }
                double px = x + nx * lat, pz = z + nz * lat;
                float h;
                if (!sampler.TryHeightClamped(px, pz, out h)) h = 0f;
                xs.Add(px);
                zs.Add(pz);
                ys.Add(h + lift);
            }
            var e = new WalkEdge { Kind = kind, X = xs.ToArray(), Z = zs.ToArray(), Y = ys.ToArray(), N = xs.Count, Area = area };
            e.S = new float[e.N];
            Finish(e);
            return e;
        }

        private static void Finish(WalkEdge e)
        {
            double s = 0;
            e.S[0] = 0f;
            for (int i = 1; i < e.N; i++)
            {
                s += Math.Sqrt(Sq(e.X[i] - e.X[i - 1]) + Sq(e.Z[i] - e.Z[i - 1]));
                e.S[i] = (float)s;
            }
            e.Length = (float)s;
        }

        private static double Sq(double v)
        {
            return v * v;
        }

        private static long NodeKey(double x, double z)
        {
            long gx = (long)Math.Round(x), gz = (long)Math.Round(z);
            return (gx << 32) ^ (gz & 0xFFFFFFFFL);
        }

        private void AddNode(long k, int e)
        {
            List<int> l;
            if (!_nodes.TryGetValue(k, out l)) _nodes[k] = l = new List<int>();
            l.Add(e);
        }

        private void RemoveNode(long k, int e)
        {
            List<int> l;
            if (!_nodes.TryGetValue(k, out l)) return;
            l.Remove(e);
            if (l.Count == 0) _nodes.Remove(k);
        }

        private void AddGrid(WalkEdge e)
        {
            ForCells(e, k =>
            {
                List<int> l;
                if (!_grid.TryGetValue(k, out l)) _grid[k] = l = new List<int>();
                l.Add(e.Id);
            });
        }

        private void RemoveGrid(WalkEdge e)
        {
            ForCells(e, k =>
            {
                List<int> l;
                if (!_grid.TryGetValue(k, out l)) return;
                l.Remove(e.Id);
                if (l.Count == 0) _grid.Remove(k);
            });
        }

        private static void ForCells(WalkEdge e, Action<long> f)
        {
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int i = 0; i < e.N; i++)
            {
                minX = Math.Min(minX, e.X[i]);
                maxX = Math.Max(maxX, e.X[i]);
                minZ = Math.Min(minZ, e.Z[i]);
                maxZ = Math.Max(maxZ, e.Z[i]);
            }
            for (long cz = (long)Math.Floor(minZ / GridM); cz <= (long)Math.Floor(maxZ / GridM); cz++)
            for (long cx = (long)Math.Floor(minX / GridM); cx <= (long)Math.Floor(maxX / GridM); cx++)
                f((cx << 32) ^ (cz & 0xFFFFFFFFL));
        }

        private static double ProjectEdge(WalkEdge e, double x, double z, out float along)
        {
            double best = double.PositiveInfinity;
            along = 0f;
            for (int i = 0; i + 1 < e.N; i++)
            {
                double ax = e.X[i], az = e.Z[i], dx = e.X[i + 1] - ax, dz = e.Z[i + 1] - az;
                double l2 = dx * dx + dz * dz;
                double t = l2 > 0 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                double d = Math.Sqrt(Sq(ax + t * dx - x) + Sq(az + t * dz - z));
                if (d < best)
                {
                    best = d;
                    along = (float)(e.S[i] + t * (e.S[i + 1] - e.S[i]));
                }
            }
            return best;
        }

        private static void PointOn(WalkEdge e, float s, out double x, out double z, out float y, out float heading)
        {
            int i = 0;
            while (i < e.N - 2 && e.S[i + 1] < s) i++;
            float seg = e.S[i + 1] - e.S[i];
            float t = seg > 1e-6f ? Math.Max(0f, Math.Min(1f, (s - e.S[i]) / seg)) : 0f;
            double dx = e.X[i + 1] - e.X[i], dz = e.Z[i + 1] - e.Z[i];
            x = e.X[i] + dx * t;
            z = e.Z[i] + dz * t;
            y = e.Y[i] + (e.Y[i + 1] - e.Y[i]) * t;
            heading = (float)Math.Atan2(dx, dz);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Inputs
        // ---------------------------------------------------------------------------------------------------------

        public void SetFocus(double x, double z, float radiusM)
        {
            lock (_inputLock)
            {
                _pfx = x;
                _pfz = z;
                _pHas = true;
                if (radiusM > 0f) _s.RadiusM = radiusM;
            }
        }

        public void SetView(float dirX, float dirZ)
        {
            lock (_inputLock)
            {
                float l = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
                _pHasView = l > 1e-6f;
                _pViewX = _pHasView ? dirX / l : 0f;
                _pViewZ = _pHasView ? dirZ / l : 0f;
            }
        }

        /// <summary>The player's vehicle (for the hop-aside capsule); <paramref name="active"/> false on foot.</summary>
        public void SetPlayerVehicle(bool active, double x, double z, float headingRad, float speedMps, float widthM, float frontM)
        {
            lock (_inputLock)
            {
                _pPlayerVeh = active;
                _pvx = x;
                _pvz = z;
                _pvHeading = headingRad;
                _pvSpeed = speedMps;
                _pvWidth = widthM;
                _pvFront = frontM;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Tables (W2_DESIGN 5.4, street_life §3 and §14)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Daily multiplier (S §14): 04:30–05:30 0.05, 05:30–08:00 0.4 (sacred 1.0), 08:00–10:30 0.8, 10:30–13:00
        /// 1.0, 13:00–15:00 0.8, 15:00–17:00 0.9, 17:00–19:00 1.0, 19:00–21:00 0.6, 21:00–23:00 0.1, later 0.01.</summary>
        public static float Daily(float hour, bool sacred)
        {
            float h = ((hour % 24f) + 24f) % 24f;
            if (h < 4.5f) return 0.01f;
            if (h < 5.5f) return 0.05f;
            if (h < 8f) return sacred ? 1.0f : 0.4f;
            if (h < 10.5f) return 0.8f;
            if (h < 13f) return 1.0f;
            if (h < 15f) return 0.8f;
            if (h < 17f) return 0.9f;
            if (h < 19f) return 1.0f;
            if (h < 21f) return 0.6f;
            if (h < 23f) return 0.1f;
            return 0.01f;
        }

        /// <summary>Peak crowd factor of an area relative to the old-core bazaar.</summary>
        public static float AreaFactor(AreaType a)
        {
            switch (a)
            {
                case AreaType.OldCore: return 1.0f;
                case AreaType.Urban: return 0.6f;
                case AreaType.PeriUrban: return 0.3f;
                case AreaType.Rural: return 0.15f;
                case AreaType.Hill:
                case AreaType.Forest:
                    return 0.1f;
                default: return 0.5f;
            }
        }

        // Archetype mix (S §3.2): old core, urban, peri-urban, fields, Boudha/Swayambhu (stupa koras).
        private static readonly float[][] Mix =
        {
            new float[] { 40, 18, 6, 3, 8, 6, 6, 6, 1, 0, 0 },
            new float[] { 52, 15, 3, 0, 10, 1, 3, 2, 0.5f, 0, 0 },
            new float[] { 45, 18, 6, 0, 10, 2, 2, 0, 0.5f, 0, 6 },
            new float[] { 25, 20, 8, 0, 8, 4, 0, 0, 0, 0, 33 },
            new float[] { 30, 12, 3, 0, 3, 1, 4, 20, 22, 0, 0 },
        };

        private static byte PickArchetype(ref SimRng r, AreaType a, bool kora)
        {
            float[] row = kora ? Mix[4] : a == AreaType.OldCore ? Mix[0] : a == AreaType.Urban ? Mix[1] : a == AreaType.PeriUrban ? Mix[2] :
                a == AreaType.Rural || a == AreaType.Hill ? Mix[3] : Mix[1];
            float total = 0f;
            foreach (float w in row) total += w;
            float u = r.NextFloat() * total;
            for (int i = 0; i < row.Length; i++)
            {
                u -= row[i];
                if (u < 0f) return (byte)i;
            }
            return 0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // The step
        // ---------------------------------------------------------------------------------------------------------

        public void Step(float dt, float gameHour, float wetness01)
        {
            if (!(dt > 0f)) return;
            if (dt > 0.25f) dt = 0.25f;
            lock (_inputLock)
            {
                if (_pHas)
                {
                    _fx = _pfx;
                    _fz = _pfz;
                    _hasFocus = true;
                }
                _hasView = _pHasView;
                _viewX = _pViewX;
                _viewZ = _pViewZ;
                _playerVeh = _pPlayerVeh;
                _vx = _pvx;
                _vz = _pvz;
                _vHeading = _pvHeading;
                _vSpeed = _pvSpeed;
                _vWidth = _pvWidth;
                _vFront = _pvFront;
                if (!_hasFocus) return;
                for (int i = 0; i < _peds.Length; i++)
                {
                    if (!_peds[i].Alive) continue;
                    Move(i, dt, wetness01);
                }
                Despawn();
                _spawnT += dt;
                if (_spawnT >= 0.2f)
                {
                    _spawnT = 0f;
                    Spawn(gameHour, wetness01);
                }
            }
            PushObstacles();
            Publish();
        }

        private bool InView(double x, double z)
        {
            if (!_hasView) return false;
            double dx = x - _fx, dz = z - _fz, d = Math.Sqrt(dx * dx + dz * dz);
            if (d < 1e-6) return true;
            return (dx * _viewX + dz * _viewZ) / d > Math.Cos((_s.ViewHalfAngleDeg + 10f) * Math.PI / 180.0);
        }

        private void Spawn(float hour, float wet)
        {
            SacredZone focusZone = default(SacredZone);
            bool sacredNear = false;
            // A kora within the radius: probe its centre from the focus (the zone the focus is in, or a sample ring).
            if (Sacred != null)
            {
                for (int k = 0; k < 9 && !sacredNear; k++)
                {
                    double ang = k * Math.PI * 2 / 8, r = k == 0 ? 0 : 0.5 * _s.RadiusM;
                    SacredZone z;
                    if (Sacred.TryGetZone(_fx + r * Math.Cos(ang), _fz + r * Math.Sin(ang), out z) && z.Kora)
                    {
                        focusZone = z;
                        sacredNear = true;
                    }
                }
            }
            AreaType area = AreaType.Urban;
            _near.Clear();
            CollectEdges(_fx, _fz, 40.0);
            if (_near.Count > 0) area = _edges[_near[0]].Area;
            float daily = Daily(hour, sacredNear);
            int target = (int)Math.Round(_s.MaxNpcs * daily * Math.Max(AreaFactor(area), sacredNear ? 0.9f : 0f));
            int alive = 0, kora = 0;
            for (int i = 0; i < _peds.Length; i++)
            {
                if (!_peds[i].Alive) continue;
                alive++;
                if (_peds[i].Mode == 2) kora++;
            }
            if (alive >= target) return;
            int slot = -1;
            for (int i = 0; i < _peds.Length; i++)
                if (!_peds[i].Alive)
                {
                    slot = i;
                    break;
                }
            if (slot < 0) return;
            float h = ((hour % 24f) + 24f) % 24f;
            float koraShare = h >= 5.5f && h < 8f ? 0.7f : 0.35f;
            if (sacredNear && kora < target * koraShare)
            {
                SpawnKora(slot, focusZone, wet);
                return;
            }
            // On a walk edge in the annulus, out of view.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                double ang = _rng.NextDouble() * Math.PI * 2;
                double rad = _s.SpawnMinM + (_s.RadiusM - _s.SpawnMinM) * Math.Sqrt(_rng.NextDouble());
                double x = _fx + rad * Math.Cos(ang), z = _fz + rad * Math.Sin(ang);
                if (InView(x, z) && rad < 60.0) continue;
                // Inside a compound or square: free walk there.
                SacredZone zone;
                if (Sacred != null && Sacred.TryGetZone(x, z, out zone) && !zone.Kora)
                {
                    SpawnWander(slot, x, z, zone, area, wet);
                    return;
                }
                _near.Clear();
                CollectEdges(x, z, 20.0);
                if (_near.Count == 0) continue;
                _near.Sort();
                int eid = _near[_rng.Next(_near.Count)];
                WalkEdge e = _edges[eid];
                if (e.Kind == EdgeKind.Crossing || e.Kind == EdgeKind.Corner) continue;
                float s;
                ProjectEdge(e, x, z, out s);
                var p = NewPed(e.Area, false, wet);
                p.Mode = 0;
                p.Edge = eid;
                p.S = s;
                p.Dir = (sbyte)(_rng.Chance(0.5f) ? 1 : -1);
                p.Side = _rng.Range(-0.4f, 0.4f);
                var pr = new SimRng(p.Rng);
                if (pr.Chance(0.15f + (e.Area == AreaType.OldCore ? 0.15f : 0f)))
                {
                    p.Mode = 3; // standing (chatting, waiting, a vendor)
                    p.Wait = pr.Range(10f, 60f);
                }
                p.Rng = pr.NextU64();
                _peds[slot] = p;
                Place(slot);
                return;
            }
        }

        private Ped NewPed(AreaType area, bool kora, float wet)
        {
            var p = new Ped { Alive = true, Id = _nextId++ };
            var r = new SimRng(SimRng.Mix(_seed, (ulong)p.Id, 0x504544));
            p.Archetype = PickArchetype(ref r, area, kora);
            p.Tint = (byte)r.Next(16);
            p.Personal = r.Range(0.85f, 1.15f);
            p.Speed = 1.35f * p.Personal * (p.Archetype == (byte)PedArchetype.Porter ? 0.8f : 1f);
            float carry = r.NextFloat();
            if (p.Archetype == (byte)PedArchetype.Porter) p.Carry = 1;
            else if (carry < 0.10f) p.Carry = (ushort)(1 + r.Next(4));
            if (p.Carry == 0 && wet > 0.3f && r.Chance(0.3f + 0.3f * wet)) p.Carry = 5;
            p.Rng = r.NextU64();
            return p;
        }

        private void SpawnKora(int slot, SacredZone zone, float wet)
        {
            // The kora ring: just inside the zone's outer edge (the widest radius where 12 of 16 directions are inside).
            float outer = 0f;
            for (int r = 4; r <= 120; r += 2)
            {
                int inside = 0;
                for (int a = 0; a < 16; a++)
                {
                    SacredZone z;
                    if (Sacred.TryGetZone(zone.CX + r * Math.Cos(a * Math.PI / 8), zone.CZ + r * Math.Sin(a * Math.PI / 8), out z) &&
                        z.AreaRef == zone.AreaRef)
                        inside++;
                }
                if (inside >= 12) outer = r;
                else if (outer > 0f) break;
            }
            if (outer < 6f) return;
            var p = NewPed(AreaType.OldCore, true, wet);
            var rr = new SimRng(p.Rng);
            p.Mode = 2;
            p.ZoneRef = zone.AreaRef;
            p.CX = zone.CX;
            p.CZ = zone.CZ;
            p.Radius = Math.Max(4f, outer - 2.5f + rr.Range(-1.5f, 1.0f));
            p.Angle = rr.Range(0f, (float)(2 * Math.PI));
            float dir = zone.KoraDir == KoraDirection.Anticlockwise ? 1f : -1f; // clockwise from above: the angle decreases
            p.AngVel = dir * p.Speed * 0.85f / p.Radius;
            p.Rng = rr.NextU64();
            _peds[slot] = p;
            Place(slot);
        }

        private void SpawnWander(int slot, double x, double z, SacredZone zone, AreaType area, float wet)
        {
            var p = NewPed(area, false, wet);
            var r = new SimRng(p.Rng);
            p.Mode = r.Chance(0.5f) ? (byte)3 : (byte)1; // half stand or sit
            p.Wait = r.Range(20f, 90f);
            p.ZoneRef = zone.AreaRef;
            p.X = x;
            p.Z = z;
            p.TX = x;
            p.TZ = z;
            p.Rng = r.NextU64();
            _peds[slot] = p;
            Place(slot);
        }

        private void CollectEdges(double x, double z, double r)
        {
            long cx0 = (long)Math.Floor((x - r) / GridM), cx1 = (long)Math.Floor((x + r) / GridM);
            long cz0 = (long)Math.Floor((z - r) / GridM), cz1 = (long)Math.Floor((z + r) / GridM);
            for (long cz = cz0; cz <= cz1; cz++)
            for (long cx = cx0; cx <= cx1; cx++)
            {
                List<int> l;
                if (!_grid.TryGetValue((cx << 32) ^ (cz & 0xFFFFFFFFL), out l)) continue;
                foreach (int e in l)
                    if (_edges[e].Alive && !_near.Contains(e))
                        _near.Add(e);
            }
        }

        private void Move(int i, float dt, float wet)
        {
            ref Ped p = ref _peds[i];
            p.ClipTime += dt;
            switch (p.Mode)
            {
                case 2:
                {
                    p.Angle += p.AngVel * dt;
                    break;
                }
                case 1:
                {
                    // Free walk inside a zone towards a target; a new target from 3–15 m away inside the same zone.
                    double dx = p.TX - p.X, dz = p.TZ - p.Z, d = Math.Sqrt(dx * dx + dz * dz);
                    if (d < 0.4)
                    {
                        var r = new SimRng(p.Rng);
                        for (int k = 0; k < 6; k++)
                        {
                            double ang = r.NextDouble() * Math.PI * 2, rad = r.Range(3f, 15f);
                            double tx = p.X + rad * Math.Cos(ang), tz = p.Z + rad * Math.Sin(ang);
                            if (InZone(tx, tz, p.ZoneRef) && InZone(0.5 * (tx + p.X), 0.5 * (tz + p.Z), p.ZoneRef))
                            {
                                p.TX = tx;
                                p.TZ = tz;
                                break;
                            }
                        }
                        if (r.Chance(0.3f))
                        {
                            p.Mode = 3;
                            p.Wait = r.Range(10f, 40f);
                        }
                        p.Rng = r.NextU64();
                    }
                    else
                    {
                        double step = Math.Min(d, p.Speed * dt);
                        p.X += dx / d * step;
                        p.Z += dz / d * step;
                    }
                    break;
                }
                case 3:
                {
                    p.Wait -= dt;
                    if (p.Wait <= 0f) p.Mode = p.ZoneRef != 0 ? (byte)1 : (byte)0;
                    break;
                }
                default:
                {
                    WalkEdge e = _edges[p.Edge];
                    if (!e.Alive)
                    {
                        p.Alive = false;
                        return;
                    }
                    p.S += p.Dir * p.Speed * dt * (wet > 0.5f ? 1.1f : 1f);
                    if (p.S < 0f || p.S > e.Length)
                    {
                        long node = p.S < 0f ? e.A : e.B;
                        float over = p.S < 0f ? -p.S : p.S - e.Length;
                        int next = NextEdge(ref p, node, e.Id);
                        if (next < 0)
                        {
                            p.Dir = (sbyte)-p.Dir; // dead end: turn round
                            p.S = p.S < 0f ? 0f : e.Length;
                        }
                        else
                        {
                            WalkEdge ne = _edges[next];
                            p.Edge = next;
                            bool atA = ne.A == node;
                            p.Dir = (sbyte)(atA ? 1 : -1);
                            p.S = atA ? over : ne.Length - over;
                        }
                    }
                    break;
                }
            }
            Place(i);
            HopAside(ref p, dt);
        }

        private bool InZone(double x, double z, long areaRef)
        {
            SacredZone zn;
            return Sacred != null && Sacred.TryGetZone(x, z, out zn) && zn.AreaRef == areaRef;
        }

        private int NextEdge(ref Ped p, long node, int from)
        {
            List<int> l;
            if (!_nodes.TryGetValue(node, out l)) return -1;
            int count = 0;
            foreach (int e in l)
                if (e != from && _edges[e].Alive)
                    count++;
            if (count == 0) return -1;
            var r = new SimRng(p.Rng);
            int pick = r.Next(count);
            p.Rng = r.NextU64();
            foreach (int e in l)
            {
                if (e == from || !_edges[e].Alive) continue;
                if (pick-- == 0) return e;
            }
            return -1;
        }

        /// <summary>Pose from the mode (edge position with the lateral spread, zone position, kora angle).</summary>
        private void Place(int i)
        {
            ref Ped p = ref _peds[i];
            float prevHeading = p.Heading;
            switch (p.Mode)
            {
                case 2:
                {
                    double x = p.CX + p.Radius * Math.Cos(p.Angle), z = p.CZ + p.Radius * Math.Sin(p.Angle);
                    // Direction of motion: d/dθ (cos, sin) × sign(ω).
                    double vx = -Math.Sin(p.Angle) * Math.Sign(p.AngVel), vz = Math.Cos(p.Angle) * Math.Sign(p.AngVel);
                    p.X = x;
                    p.Z = z;
                    p.Y = GroundY(x, z, p.Y);
                    p.Heading = (float)Math.Atan2(vx, vz);
                    p.Clip = (byte)PedClip.Walk;
                    break;
                }
                case 1:
                {
                    double dx = p.TX - p.X, dz = p.TZ - p.Z;
                    if (dx * dx + dz * dz > 1e-4) p.Heading = (float)Math.Atan2(dx, dz);
                    p.Y = GroundY(p.X, p.Z, p.Y);
                    p.Clip = (byte)PedClip.Walk;
                    break;
                }
                case 3:
                    p.Clip = p.ZoneRef != 0 ? (byte)PedClip.Sit : (byte)PedClip.Chat;
                    if (p.ZoneRef != 0) p.Y = GroundY(p.X, p.Z, p.Y);
                    break;
                default:
                {
                    WalkEdge e = _edges[p.Edge];
                    double x, z;
                    float y, h;
                    PointOn(e, Math.Max(0f, Math.Min(e.Length, p.S)), out x, out z, out y, out h);
                    double rx = Math.Cos(h), rz = -Math.Sin(h);
                    p.X = x + rx * p.Side;
                    p.Z = z + rz * p.Side;
                    p.Y = y;
                    p.Heading = p.Dir > 0 ? h : h + (float)Math.PI;
                    p.Clip = p.Carry == 1 || p.Carry == 2 ? (byte)PedClip.Carry : (byte)PedClip.Walk;
                    p.OnCarriageway = e.Kind == EdgeKind.Crossing || e.Kind == EdgeKind.StreetEdge;
                    break;
                }
            }
            if (p.Mode != 0) p.OnCarriageway = false;
            if (float.IsNaN(p.Heading)) p.Heading = prevHeading;
        }

        private void HopAside(ref Ped p, float dt)
        {
            if (p.Hopping)
            {
                p.HopT += dt;
                if (p.HopT > ContactRules.ReactMaxS + ContactRules.HopS + 0.3f) p.Hopping = false;
                return;
            }
            if (!_playerVeh) return;
            float side;
            if (ContactRules.HopAsideCapsule(_vx, _vz, _vHeading, _vSpeed, _vWidth, _vFront, p.X, p.Z, out side) && _vSpeed > 1f)
            {
                var r = new SimRng(p.Rng);
                p.Hopping = true;
                p.HopT = -r.Range(ContactRules.ReactMinS, ContactRules.ReactMaxS);
                p.HopSide = side;
                p.Rng = r.NextU64();
            }
        }

        private void Despawn()
        {
            for (int i = 0; i < _peds.Length; i++)
            {
                if (!_peds[i].Alive) continue;
                double dx = _peds[i].X - _fx, dz = _peds[i].Z - _fz;
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (d > 1.2 * _s.RadiusM && !InView(_peds[i].X, _peds[i].Z)) _peds[i].Alive = false;
            }
        }

        private void PushObstacles()
        {
            TrafficSim t = Traffic;
            if (t == null) return;
            _keep.Clear();
            for (int i = 0; i < _peds.Length; i++)
            {
                if (!_peds[i].Alive || !_peds[i].OnCarriageway) continue;
                int id = ObstacleIdBase + (_peds[i].Id & 0x0FFFFFFF);
                _keep.Add(id);
                t.AddObstacle(id, _peds[i].X, _peds[i].Z, CrossingRadiusM, ObstacleKind.Pedestrian);
            }
            foreach (int id in _pushed)
                if (!_keep.Contains(id))
                    t.RemoveObstacle(id);
            _pushed.Clear();
            foreach (int id in _keep) _pushed.Add(id);
        }

        private void Publish()
        {
            int n = 0;
            for (int i = 0; i < _peds.Length; i++)
            {
                if (!_peds[i].Alive) continue;
                Ped p = _peds[i];
                double x = p.X, z = p.Z;
                float y = p.Y;
                if (p.Hopping && p.HopT > 0f)
                {
                    float side, up;
                    ContactRules.HopOffset(p.HopT, out side, out up);
                    double rx = Math.Cos(_vHeading) * p.HopSide, rz = -Math.Sin(_vHeading) * p.HopSide;
                    x += rx * side;
                    z += rz * side;
                    y += up;
                }
                float speed = p.Mode == 3 ? 0f : p.Speed;
                _back[n++] = new PedPose
                {
                    X = x, Z = z, Y = y, HeadingRad = p.Heading, SpeedMps = speed, Archetype = p.Archetype, Tint = p.Tint,
                    ClipId = p.Clip, ClipTime = p.ClipTime, CarryProp = p.Carry, AgentId = p.Id,
                };
            }
            lock (_snapLock)
            {
                PedPose[] t = _front;
                _front = _back;
                _back = t;
                _frontCount = n;
            }
        }

        /// <summary>Copies the pedestrians of the last step and returns the count. Thread-safe.</summary>
        public int CopyPoses(PedPose[] dst)
        {
            lock (_snapLock)
            {
                int n = Math.Min(dst.Length, _frontCount);
                Array.Copy(_front, dst, n);
                return n;
            }
        }

        /// <summary>Kora walkers of the last step about their centre: id, centre and angular velocity (rad/s, negative =
        /// clockwise seen from above) — the V5 check.</summary>
        public int CopyKora(KoraState[] dst)
        {
            lock (_inputLock)
            {
                int n = 0;
                for (int i = 0; i < _peds.Length && n < dst.Length; i++)
                {
                    if (!_peds[i].Alive || _peds[i].Mode != 2) continue;
                    dst[n++] = new KoraState { AgentId = _peds[i].Id, CX = _peds[i].CX, CZ = _peds[i].CZ, X = _peds[i].X, Z = _peds[i].Z, AngularVelocity = _peds[i].AngVel, ZoneRef = _peds[i].ZoneRef };
                }
                return n;
            }
        }
    }

    /// <summary>A kora walker (tests and debug).</summary>
    public struct KoraState
    {
        public int AgentId;
        public double CX, CZ, X, Z;
        public float AngularVelocity;
        public long ZoneRef;
    }
}
