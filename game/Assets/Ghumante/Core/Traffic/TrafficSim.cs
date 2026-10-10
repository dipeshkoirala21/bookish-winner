using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;

namespace Ghumante.Core.Traffic
{
    /// <summary>Per-tier traffic settings (W2_DESIGN 5.1 and 10.4).</summary>
    public sealed class TrafficSettings
    {
        /// <summary>Moving-vehicle cap (12 / 30 / 50).</summary>
        public int MaxVehicles = 30;

        /// <summary>Full simulation within this distance of the focus (150 / 250 / 400 m); beyond it agents run as lane
        /// ghosts (IDM at <see cref="GhostHz"/>, lane position only) out to <see cref="GhostRadiusM"/>.</summary>
        public float FullRadiusM = 250f;

        public float GhostRadiusM = 500f;
        public float GhostHz = 2f;

        /// <summary>Spawns appear 80–150 m away and out of view; despawns happen beyond 1.2 × the ghost radius, out of
        /// view.</summary>
        public float SpawnMinM = 80f, SpawnMaxM = 150f;

        /// <summary>Half the camera's horizontal view angle (degrees) for the out-of-view test, plus a margin.</summary>
        public float ViewHalfAngleDeg = 55f;

        public float SpawnIntervalS = 0.25f;
        public int SpawnAttempts = 4;

        /// <summary>Weekly holiday: density × 0.6.</summary>
        public bool Saturday;

        /// <summary>Horns allowed at all (audio may switch them off).</summary>
        public bool Horns = true;

        public static TrafficSettings Low()
        {
            return new TrafficSettings { MaxVehicles = 12, FullRadiusM = 150f, GhostRadiusM = 300f };
        }

        public static TrafficSettings Mid()
        {
            return new TrafficSettings { MaxVehicles = 30, FullRadiusM = 250f, GhostRadiusM = 500f };
        }

        public static TrafficSettings High()
        {
            return new TrafficSettings { MaxVehicles = 50, FullRadiusM = 400f, GhostRadiusM = 800f };
        }
    }

    /// <summary>
    /// The street traffic of W2_DESIGN 5.1 on a <see cref="LaneGraph"/>: IDM car following per class, left-hand traffic
    /// with keep-left and overtaking on the right, junction controllers (police phases, signal cycles, clockwise
    /// roundabouts with gap acceptance, priority with yield-to-the-right), reservation of conflicting connectors so
    /// junction movements never collide, spawn tables by road class, area type and time of day, soft avoidance of
    /// cows, dogs, pedestrians and the player (never honking at animals), head-on passing on shared lanes, the honk
    /// model, and the simulation LOD (lane ghosts at 2 Hz beyond the full radius). No motor agent ever spawns or drives
    /// inside SACRED_NO_VEHICLE (lanes there carry no motor classes and spawns are checked against
    /// <see cref="SacredZoneIndex"/>).
    /// <para><b>Threading.</b> <see cref="Step"/> runs on a worker thread holding the graph's lock;
    /// <see cref="SetFocus"/>, <see cref="AddObstacle"/> and friends may be called from any thread (they are applied at
    /// the next step); <see cref="CopyPoses"/> reads a double-buffered snapshot. Deterministic: the same graph, seed and
    /// inputs give the same snapshots.</para>
    /// </summary>
    public sealed class TrafficSim
    {
        private const float StopLineM = 0.3f;
        private const float LaneChangeS = 2.0f;
        private const float LateralRateMps = 1.0f;
        private const float LookaheadM = 90f;
        private const int PathLen = 4;
        private const float Gravity = 9.81f;

        internal struct Agent
        {
            public bool Alive;
            public int Id;
            public VehicleClass Class;
            public ushort Variant;
            public byte Livery;
            public float Length, Half, FrontOverhang, Wheelbase;
            public IdmParams Idm;
            public float Personal;
            public int Lane, LaneGen, PrevLane;
            public float S, V, A;
            public int P0, P1, P2, P3; // planned lanes after the current one (-1 none)
            public bool Committed;
            public int CommitLane;
            public int TailLane; // the junction connector the body's tail is still in (its reservation held), -1 none
            public float Lat, LatTarget, Filter;
            public int ChangeFrom;
            public float ChangeT, ChangeTimer;
            public bool Ghost;
            public float GhostAcc;
            public float WaitT, BlockedT, HornTimer;
            public byte Horns;
            public bool BlockedByVehicle, BlockedByAnimal, AtStopLine;
            public int Route, RoutePos, NextStop;
            public int RouteId; // the transit route (index into the route set) of a routed agent, -1 none
            public float DwellT;
            public ulong Rng;
            public double X, Z;
            public float Y, Heading, Lean, Pitch, Roll, PrevHeading, YawRate;
            public byte Anim;
            public float RandomStopAt; // micros and tempos stop at random every ~600 m
            public float Odo;
        }

        internal struct Obstacle
        {
            public int Id;
            public double X, Z;
            public float R;
            public ObstacleKind Kind;
        }

        private struct LaneObstacle
        {
            public int Lane;
            public float S, Lat, R;
            public ObstacleKind Kind;
        }

        /// <summary>A bus-route plan: the lanes to follow and the stops along them.</summary>
        internal sealed class RoutePlan
        {
            public int[] Lanes;
            public int[] StopLane;
            public float[] StopS;
            public int[] StopIndex;
            public int RouteIndex;
        }

        private readonly LaneGraph _g;
        private readonly TrafficSettings _s;
        private readonly ulong _seed;

        /// <summary>Runtime state of a police or signal junction, kept here (not in the shared graph) so several
        /// simulations over one graph stay independent and replayable.</summary>
        private sealed class CtrlState
        {
            public LaneGraph.Controller Owner;
            public int Phase;
            public float PhaseT, PhaseLen, ClearT;
            public float GreenT; // time since the phase turned green (horn rule)
            public int[] Queue = new int[0];
        }

        private CtrlState[] _ctrl = new CtrlState[0];
        private SimRng _rng;
        private Agent[] _agents;
        private int _nextId = 1;
        private float _time;
        private float _spawnT;

        // Inputs (applied at the start of a step).
        private readonly object _inputLock = new object();
        private double _focusX, _focusZ, _pendFocusX, _pendFocusZ;
        private float _viewX, _viewZ, _pendViewX, _pendViewZ;
        private bool _hasView, _pendHasView, _hasFocus, _pendHasFocus;
        private readonly SortedDictionary<int, Obstacle> _pendObstacles = new SortedDictionary<int, Obstacle>();
        private Obstacle[] _obstacles = new Obstacle[64];
        private int _obstacleCount;
        private bool _obstaclesDirty = true;

        // Per-step scratch.
        private long[] _occKey = new long[64];
        private int[] _occAgent = new int[64];
        private float[] _occS = new float[64];
        private int _occCount;
        private int[] _laneFirst = new int[0], _laneCount = new int[0], _laneStamp = new int[0], _resv = new int[0];
        private int _stamp;
        private LaneObstacle[] _laneObs = new LaneObstacle[64];
        private int _laneObsCount;
        private int[] _laneObsFirst = new int[0], _laneObsN = new int[0], _laneObsStamp = new int[0];
        private readonly List<int> _near = new List<int>();
        internal readonly List<RoutePlan> Routes = new List<RoutePlan>();

        // Snapshot.
        private readonly object _snapLock = new object();
        private AgentPose[] _front = new AgentPose[0], _back = new AgentPose[0];
        private int _frontCount;
        private HornEvent[] _hornsBack = new HornEvent[16], _hornsFront = new HornEvent[16];
        private int _hornsBackCount, _hornsFrontCount;

        /// <summary>Optional: spawns of motor classes inside these zones are refused (the lane graph already drops lanes
        /// there).</summary>
        public SacredZoneIndex Sacred;

        /// <summary>Raised at the end of each step (on the stepping thread) for every horn of the step.</summary>
        public event Action<int, HornKind> Horn;

        /// <summary>Raised when a routed agent starts its dwell at a stop: agent id, route index, stop index.</summary>
        public event Action<int, int, int> StopReached;

        public TrafficSim(LaneGraph g, TrafficSettings s, ulong seed)
        {
            _g = g ?? throw new ArgumentNullException(nameof(g));
            _s = s ?? new TrafficSettings();
            _seed = seed;
            _rng = new SimRng(SimRng.Mix(seed, 0x54524146));
            _agents = new Agent[Math.Max(16, _s.MaxVehicles * 2 + 32)];
            _front = new AgentPose[_agents.Length];
            _back = new AgentPose[_agents.Length];
        }

        public TrafficSettings Settings
        {
            get { return _s; }
        }

        public LaneGraph Graph
        {
            get { return _g; }
        }

        /// <summary>Simulated seconds so far.</summary>
        public float Time
        {
            get { return _time; }
        }

        /// <summary>Live agents (full and ghost) after the last step.</summary>
        public int AgentCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _agents.Length; i++)
                    if (_agents[i].Alive)
                        n++;
                return n;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Inputs
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The player (or camera) position the simulation centres on. <paramref name="simRadiusM"/> &gt; 0
        /// overrides the settings' full radius.</summary>
        public void SetFocus(double x, double z, float simRadiusM)
        {
            lock (_inputLock)
            {
                _pendFocusX = x;
                _pendFocusZ = z;
                _pendHasFocus = true;
                if (simRadiusM > 0f) _s.FullRadiusM = simRadiusM;
            }
        }

        /// <summary>The camera's horizontal view direction (game X/Z), for the out-of-view spawn and despawn test. Without
        /// it everything counts as out of view (headless runs).</summary>
        public void SetView(float dirX, float dirZ)
        {
            lock (_inputLock)
            {
                float l = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
                _pendHasView = l > 1e-6f;
                _pendViewX = _pendHasView ? dirX / l : 0f;
                _pendViewZ = _pendHasView ? dirZ / l : 0f;
            }
        }

        /// <summary>Adds or moves an obstacle (cow, dog, player, crossing pedestrian): agents slow, steer round it within
        /// their lane or stop; they never honk at animals.</summary>
        public void AddObstacle(int id, double x, double z, float radiusM, ObstacleKind k)
        {
            lock (_inputLock)
            {
                _pendObstacles[id] = new Obstacle { Id = id, X = x, Z = z, R = Math.Max(0.1f, radiusM), Kind = k };
                _obstaclesDirty = true;
            }
        }

        public void RemoveObstacle(int id)
        {
            lock (_inputLock)
            {
                if (_pendObstacles.Remove(id)) _obstaclesDirty = true;
            }
        }

        /// <summary>Removes every obstacle of a kind.</summary>
        public void ClearObstacles(ObstacleKind k)
        {
            lock (_inputLock)
            {
                var drop = new List<int>();
                foreach (var kv in _pendObstacles)
                    if (kv.Value.Kind == k)
                        drop.Add(kv.Key);
                foreach (int id in drop) _pendObstacles.Remove(id);
                _obstaclesDirty = true;
            }
        }

        private void ApplyInputs()
        {
            lock (_inputLock)
            {
                if (_pendHasFocus)
                {
                    _focusX = _pendFocusX;
                    _focusZ = _pendFocusZ;
                    _hasFocus = true;
                }
                _hasView = _pendHasView;
                _viewX = _pendViewX;
                _viewZ = _pendViewZ;
                if (_obstaclesDirty)
                {
                    if (_obstacles.Length < _pendObstacles.Count) _obstacles = new Obstacle[_pendObstacles.Count * 2];
                    _obstacleCount = 0;
                    foreach (var kv in _pendObstacles) _obstacles[_obstacleCount++] = kv.Value;
                    _obstaclesDirty = false;
                }
            }
        }

        internal bool TryFocus(out double x, out double z)
        {
            x = _focusX;
            z = _focusZ;
            return _hasFocus;
        }

        internal bool OutOfView(double x, double z)
        {
            return !InView(x, z);
        }

        /// <summary>Live routed agents of a route plan (runner bookkeeping).</summary>
        internal int RoutedCount()
        {
            int n = 0;
            for (int i = 0; i < _agents.Length; i++)
                if (_agents[i].Alive && _agents[i].Route >= 0)
                    n++;
            return n;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Snapshot
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Copies the poses of the last step (at most <c>dst.Length</c>) and returns the count. Thread-safe.</summary>
        public int CopyPoses(AgentPose[] dst)
        {
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            lock (_snapLock)
            {
                int n = Math.Min(dst.Length, _frontCount);
                Array.Copy(_front, dst, n);
                return n;
            }
        }

        /// <summary>The horns of the last step (also raised as <see cref="Horn"/>).</summary>
        public int CopyHorns(HornEvent[] dst)
        {
            lock (_snapLock)
            {
                int n = Math.Min(dst.Length, _hornsFrontCount);
                Array.Copy(_hornsFront, dst, n);
                return n;
            }
        }

        /// <summary>True while agent <paramref name="agentId"/> is alive.</summary>
        public bool IsAlive(int agentId)
        {
            return IndexOf(agentId) >= 0;
        }

        private int IndexOf(int agentId)
        {
            for (int i = 0; i < _agents.Length; i++)
                if (_agents[i].Alive && _agents[i].Id == agentId)
                    return i;
            return -1;
        }

        /// <summary>Lane, arc length and speed of a live agent (tests, bus runner).</summary>
        public bool TryGetAgent(int agentId, out LaneId lane, out float s, out float speedMps)
        {
            int i = IndexOf(agentId);
            lane = i >= 0 ? new LaneId(_agents[i].Lane) : LaneId.None;
            s = i >= 0 ? _agents[i].S : 0f;
            speedMps = i >= 0 ? _agents[i].V : 0f;
            return i >= 0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Spawning
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Spawns catalogue variant <paramref name="variant"/> on the nearest lane its class may use within 15 m of
        /// (x, z). Refused (false) inside a SACRED_NO_VEHICLE zone for a motor class, where no lane admits the class,
        /// where the lane has no room, or when the agent table is full.
        /// </summary>
        public bool TrySpawnAt(double x, double z, int variant, out int agentId)
        {
            agentId = 0;
            if (variant < 0 || variant >= VehicleCatalog.Count) return false;
            VehicleClass c = VehicleCatalog.At(variant).TrafficClass;
            if (VehicleClasses.IsMotor(c) && Sacred != null && Sacred.Contains(x, z)) return false;
            LaneId lane;
            float s;
            if (!_g.TryNearestLane(x, z, c, 15.0, out lane, out s)) return false;
            lock (_g.SyncRoot)
            {
                if (!_g.IsAlive(lane)) return false;
                EnsureLaneArrays();
                return SpawnOnLane(lane.Value, s, variant, VehicleCatalog.At(variant).PickLivery((uint)_rng.NextU32()), out agentId, false);
            }
        }

        /// <summary>Spawns on a lane at arc length s (bus runner, garage summon). Same refusals as <see cref="TrySpawnAt"/>.</summary>
        public bool TrySpawnOnLane(LaneId lane, float s, int variant, byte livery, out int agentId)
        {
            agentId = 0;
            lock (_g.SyncRoot)
            {
                if (!_g.IsAlive(lane)) return false;
                EnsureLaneArrays();
                return SpawnOnLane(lane.Value, s, variant, livery, out agentId, false);
            }
        }

        private bool SpawnOnLane(int laneId, float s, int variant, byte livery, out int agentId, bool checkView)
        {
            agentId = 0;
            LaneGraph.Lane l = _g.Lanes[laneId];
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            VehicleClass c = e.TrafficClass;
            if ((l.Mask & VehicleClasses.Bit(c)) == 0) return false;
            double x, z;
            float y, h;
            LaneGraph.PointAt(l, s, out x, out z, out y, out h);
            if (VehicleClasses.IsMotor(c) && Sacred != null && Sacred.Contains(x, z)) return false;
            float len = e.LengthM;
            s = Math.Max(len, Math.Min(s, l.Length));
            if (!Room(laneId, s, len)) return false;
            if (!Clear(l, s, len)) return false;
            if (l.Twin >= 0 && !Room(l.Twin, _g.Lanes[l.Twin].Length - s + len, len + 4f)) return false;
            int slot = -1;
            for (int i = 0; i < _agents.Length; i++)
                if (!_agents[i].Alive)
                {
                    slot = i;
                    break;
                }
            if (slot < 0) return false;
            var a = new Agent
            {
                Alive = true, Id = _nextId++, Class = c, Variant = (ushort)variant, Livery = livery, Length = len, Half = 0.5f * e.WidthM,
                Wheelbase = e.WheelbaseM, Idm = TrafficTables.Idm(c), Lane = laneId, LaneGen = l.Gen, PrevLane = -1, S = s,
                P0 = -1, P1 = -1, P2 = -1, P3 = -1, CommitLane = -1, ChangeFrom = -1, Route = -1, NextStop = -1, RouteId = -1, TailLane = -1,
            };
            a.FrontOverhang = Math.Max(0.1f, len - e.WheelbaseM - e.RearOverhangM);
            a.Rng = SimRng.Mix(_seed, (ulong)a.Id, 0x41474E54);
            var r = new SimRng(a.Rng);
            a.Personal = r.Range(0.9f, 1.1f);
            if (VehicleClasses.IsTwoWheel(c)) a.Filter = r.Range(-0.6f, 0.6f);
            a.ChangeTimer = r.Range(0f, 1f);
            a.RandomStopAt = c == VehicleClass.Microbus || c == VehicleClass.Tempo || c == VehicleClass.Minibus ? r.Range(300f, 900f) : -1f;
            a.Rng = r.NextU64();
            a.V = Math.Min(0.6f * TargetSpeed(ref a, l, 0.5f), 8f);
            a.Heading = h;
            a.PrevHeading = h;
            double rx, rz;
            float ry, rh;
            LaneGraph.PointAt(l, s - a.FrontOverhang - a.Wheelbase, out rx, out rz, out ry, out rh);
            a.X = rx;
            a.Z = rz;
            a.Y = ry;
            _agents[slot] = a;
            agentId = a.Id;
            // Register in the occupancy of this step so later spawns see it.
            InsertOcc(slot);
            return true;
        }

        /// <summary>Spawns an agent that follows route plan <paramref name="route"/> (bus runner).</summary>
        internal bool SpawnRouted(int lane, float s, int variant, byte livery, int route, int routePos, out int agentId)
        {
            if (!SpawnOnLane(lane, s, variant, livery, out agentId, false)) return false;
            int i = IndexOf(agentId);
            _agents[i].Route = route;
            _agents[i].RouteId = route >= 0 && route < Routes.Count ? Routes[route].RouteIndex : -1;
            _agents[i].RoutePos = routePos;
            _agents[i].RandomStopAt = -1f;
            _agents[i].NextStop = -1;
            ClearPath(ref _agents[i]);
            return true;
        }

        /// <summary>
        /// After the route plans were rebuilt (the bus runner, when tiles stream in or out): every routed agent takes the
        /// new plan of its route that holds its lane, or continues as ordinary traffic when none does.
        /// </summary>
        internal void RebindRoutes()
        {
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive || _agents[i].RouteId < 0) continue;
                ref Agent a = ref _agents[i];
                int plan = -1, pos = -1;
                for (int p = 0; p < Routes.Count && plan < 0; p++)
                {
                    if (Routes[p].RouteIndex != a.RouteId) continue;
                    int at = Array.IndexOf(Routes[p].Lanes, a.Lane);
                    if (at < 0) continue;
                    plan = p;
                    pos = at;
                }
                a.Route = plan;
                a.RoutePos = Math.Max(0, pos);
                a.NextStop = -1;
                if (plan < 0) a.RouteId = -1;
                ClearPath(ref a);
            }
        }

        /// <summary>No agent body near the new body's place, whatever lane it is on (a vehicle on a connector about to
        /// enter, one on a neighbouring lane at a junction): bounding circles of the bodies plus a metre apart.</summary>
        private bool Clear(LaneGraph.Lane l, float s, float len)
        {
            double fx, fz, bx, bz;
            float y, h;
            LaneGraph.PointAt(l, s, out fx, out fz, out y, out h);
            LaneGraph.PointAt(l, Math.Max(0f, s - len), out bx, out bz, out y, out h);
            double cx = 0.5 * (fx + bx), cz = 0.5 * (fz + bz);
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                double fwdX = Math.Sin(a.Heading), fwdZ = Math.Cos(a.Heading);
                float mid = 0.5f * a.Length - (a.Length - a.Wheelbase - a.FrontOverhang); // rear axle to body middle
                double ax = a.X + fwdX * mid, az = a.Z + fwdZ * mid;
                double reach = 0.5 * len + 0.5 * a.Length + 1.0;
                double dx = ax - cx, dz = az - cz;
                if (dx * dx + dz * dz < reach * reach) return false;
            }
            return true;
        }

        /// <summary>No agent body within the stretch [s − len − 6, s + 6] of a lane.</summary>
        private bool Room(int laneId, float s, float len)
        {
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                if (a.Lane != laneId && a.ChangeFrom != laneId) continue;
                float lo = a.S - a.Length - 6f, hi = a.S + 6f;
                if (s >= lo && s - len <= hi) return false;
            }
            for (int k = 0; k < _laneObsCount; k++)
            {
                LaneObstacle o = _laneObs[k];
                if (o.Lane == laneId && Math.Abs(o.S - s) < len + o.R + 4f) return false;
            }
            return true;
        }

        private void SpawnAround(float hour, float density)
        {
            if (!_hasFocus) return;
            int alive = 0, rickshaws = 0;
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                alive++; // routed buses count too: the moving-vehicle cap holds
                if (_agents[i].Class == VehicleClass.Rickshaw) rickshaws++;
            }
            int target = (int)Math.Round(_s.MaxVehicles * density * (_s.Saturday ? TrafficTables.SaturdayFactor : 1f));
            if (density > 0f && target < 1) target = 1;
            if (alive >= target) return;
            for (int attempt = 0; attempt < _s.SpawnAttempts && alive < target; attempt++)
            {
                double ang = _rng.NextDouble() * 2 * Math.PI;
                double rad = _s.SpawnMinM + (_s.SpawnMaxM - _s.SpawnMinM) * _rng.NextDouble();
                double x = _focusX + rad * Math.Cos(ang), z = _focusZ + rad * Math.Sin(ang);
                if (InView(x, z)) continue;
                _near.Clear();
                _g.LanesNear(x, z, 20.0, _near);
                if (_near.Count == 0) continue;
                _near.Sort();
                int pick = _near[_rng.Next(_near.Count)];
                LaneGraph.Lane l = _g.Lanes[pick];
                if (l.Kind != LaneKind.Road || l.Length < 12f) continue;
                if (l.Controller >= 0 && QueueOf(l) > 25) continue; // jam throttle
                float s;
                double d = LaneGraph.Project(l, x, z, out s);
                if (d > 25.0) continue;
                int variant = TrafficTables.PickVariant(ref _rng, l.Class, l.Area, hour, l.Mask);
                if (variant < 0) continue;
                if (VehicleCatalog.At(variant).TrafficClass == VehicleClass.Rickshaw && rickshaws >= 2) continue;
                int id;
                byte livery = VehicleCatalog.At(variant).PickLivery(_rng.NextU32());
                if (SpawnOnLane(pick, s, variant, livery, out id, true))
                {
                    alive++;
                    if (VehicleCatalog.At(variant).TrafficClass == VehicleClass.Rickshaw) rickshaws++;
                }
            }
        }

        private int QueueOf(LaneGraph.Lane l)
        {
            LaneGraph.Controller c = _g.Controllers[l.Controller];
            if (!c.Alive) return 0;
            CtrlState st = StateOf(l.Controller);
            if (l.Approach < 0 || l.Approach >= st.Queue.Length) return 0;
            return st.Queue[l.Approach];
        }

        private bool InView(double x, double z)
        {
            if (!_hasView) return false;
            double dx = x - _focusX, dz = z - _focusZ;
            double d = Math.Sqrt(dx * dx + dz * dz);
            if (d < 1e-6) return true;
            double cos = (dx * _viewX + dz * _viewZ) / d;
            return cos > Math.Cos((_s.ViewHalfAngleDeg + 10f) * Math.PI / 180.0);
        }

        // ---------------------------------------------------------------------------------------------------------
        // The step
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Advances the traffic by <paramref name="dt"/> seconds (clamped to 0.25 s) at game hour
        /// <paramref name="gameHourOfDay"/>; wetness lowers speeds a little. Runs on a worker thread; budget §10.4.
        /// </summary>
        public void Step(float dt, float gameHourOfDay, float wetness01)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            if (dt > 0.25f) dt = 0.25f;
            ApplyInputs();
            _hornsBackCount = 0;
            float density = TrafficTables.Density(gameHourOfDay);
            float cong = TrafficTables.Congestion(density);
            float wet = wetness01 > 0f ? (wetness01 < 1f ? wetness01 : 1f) : 0f;
            lock (_g.SyncRoot)
            {
                _time += dt;
                EnsureLaneArrays();
                Validate();
                Controllers(dt);
                ProjectObstacles();
                BuildOccupancy();
                for (int i = 0; i < _agents.Length; i++)
                {
                    if (!_agents[i].Alive) continue;
                    ref Agent a = ref _agents[i];
                    double dx = a.X - _focusX, dz = a.Z - _focusZ;
                    double dist = _hasFocus ? Math.Sqrt(dx * dx + dz * dz) : 0.0;
                    a.Ghost = dist > _s.FullRadiusM;
                    float h = dt;
                    if (a.Ghost)
                    {
                        a.GhostAcc += dt;
                        if (a.GhostAcc < 1f / _s.GhostHz) continue;
                        h = Math.Min(a.GhostAcc, 0.6f);
                        a.GhostAcc = 0f;
                    }
                    Drive(i, h, cong, wet);
                }
                LaneChanges(dt);
                Despawn();
                _spawnT += dt;
                if (_spawnT >= _s.SpawnIntervalS)
                {
                    _spawnT = 0f;
                    BuildOccupancy();
                    SpawnAround(gameHourOfDay, density);
                }
                Poses(dt);
            }
            Publish();
        }

        private void EnsureLaneArrays()
        {
            int n = _g.LaneCapacity;
            if (_laneFirst.Length >= n) return;
            int cap = Math.Max(n, _laneFirst.Length * 2);
            Array.Resize(ref _laneFirst, cap);
            Array.Resize(ref _laneCount, cap);
            Array.Resize(ref _laneStamp, cap);
            Array.Resize(ref _resv, cap);
            Array.Resize(ref _laneObsFirst, cap);
            Array.Resize(ref _laneObsN, cap);
            Array.Resize(ref _laneObsStamp, cap);
        }

        /// <summary>Agents whose lane went away (tile unloaded, connector rebuilt) leave; stale reservations are dropped.</summary>
        private void Validate()
        {
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                if (!_g.IsAlive(new LaneId(a.Lane), a.LaneGen))
                {
                    Kill(i);
                    continue;
                }
                if (a.P0 >= 0 && !_g.IsAlive(new LaneId(a.P0))) ClearPath(ref a);
                if (a.ChangeFrom >= 0 && !_g.IsAlive(new LaneId(a.ChangeFrom))) a.ChangeFrom = -1;
            }
            // Reservations are recounted from the agents each step. A long body keeps the junction connector it left
            // reserved until its tail is out of it, so no crossing movement starts into its tail.
            Array.Clear(_resv, 0, _resv.Length);
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                if (a.Committed && a.CommitLane >= 0 && _g.IsAlive(new LaneId(a.CommitLane))) _resv[a.CommitLane]++;
                else a.Committed = false;
                if (a.TailLane >= 0 && (a.PrevLane != a.TailLane || a.S >= a.Length + 0.5f || !_g.IsAlive(new LaneId(a.TailLane)))) a.TailLane = -1;
                if (a.TailLane >= 0) _resv[a.TailLane]++;
            }
        }

        private void Kill(int i)
        {
            _agents[i].Alive = false;
        }

        private void ClearPath(ref Agent a)
        {
            a.P0 = a.P1 = a.P2 = a.P3 = -1;
            if (a.Committed)
            {
                a.Committed = false;
                if (a.CommitLane >= 0 && a.CommitLane < _resv.Length && _resv[a.CommitLane] > 0) _resv[a.CommitLane]--;
                a.CommitLane = -1;
            }
        }

        // ---- controllers ----

        /// <summary>The sim-side state of controller <paramref name="id"/>, reset when the graph rebuilt that junction.</summary>
        private CtrlState StateOf(int id)
        {
            LaneGraph.Controller c = _g.Controllers[id];
            if (id >= _ctrl.Length) Array.Resize(ref _ctrl, Math.Max(_g.Controllers.Count, Math.Max(id + 1, _ctrl.Length * 2)));
            CtrlState st = _ctrl[id];
            if (st == null)
            {
                st = new CtrlState();
                _ctrl[id] = st;
            }
            if (!ReferenceEquals(st.Owner, c))
            {
                st.Owner = c;
                st.Phase = 0;
                st.PhaseT = st.PhaseLen = st.ClearT = st.GreenT = 0f;
                if (st.Queue.Length != c.Arms) st.Queue = new int[Math.Max(0, c.Arms)];
                else Array.Clear(st.Queue, 0, st.Queue.Length);
            }
            return st;
        }

        private void Controllers(float dt)
        {
            List<LaneGraph.Controller> cs = _g.Controllers;
            for (int i = 0; i < cs.Count; i++)
            {
                LaneGraph.Controller c = cs[i];
                if (!c.Alive) continue;
                if (c.Kind != ControllerKind.Police && c.Kind != ControllerKind.Signal) continue;
                CtrlState st = StateOf(i);
                if (st.PhaseLen <= 0f)
                {
                    // First step: a deterministic phase offset per node so the city does not switch in lockstep.
                    var r = new SimRng(SimRng.Mix(_seed, (ulong)c.NodeKey));
                    st.Phase = r.Next(Math.Max(1, c.Phases));
                    st.PhaseLen = 30f;
                    st.PhaseT = r.Range(0f, st.PhaseLen);
                }
                st.PhaseT += dt;
                st.GreenT += dt;
                if (st.ClearT > 0f) st.ClearT -= dt;
                if (st.PhaseT >= st.PhaseLen)
                {
                    st.Phase = (st.Phase + 1) % Math.Max(1, c.Phases);
                    st.PhaseT = 0f;
                    st.GreenT = 0f;
                    if (c.Kind == ControllerKind.Signal)
                    {
                        st.PhaseLen = 30f; // 25 s green + 5 s amber and clear: a 90 s cycle at three phases
                        st.ClearT = 0f;
                    }
                    else
                    {
                        // Police: 30 s, +15 s for an arm with more than 12 waiting, within 20–60 s; a 2 s all-red
                        // while the officer turns.
                        int q = 0;
                        for (int k = 0; k < c.PhaseOf.Length && k < st.Queue.Length; k++)
                            if (c.PhaseOf[k] == st.Phase)
                                q = Math.Max(q, st.Queue[k]);
                        st.PhaseLen = Math.Max(20f, Math.Min(60f, 30f + (q > 12 ? 15f : 0f)));
                        st.ClearT = 2f;
                    }
                }
                Array.Clear(st.Queue, 0, st.Queue.Length);
            }
            // Queues: agents waiting at the stop line of each approach.
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                if (a.WaitT <= 0f) continue;
                LaneGraph.Lane l = _g.Lanes[a.Lane];
                if (l.Controller < 0 || l.Approach < 0) continue;
                if (!_g.Controllers[l.Controller].Alive) continue;
                CtrlState st = StateOf(l.Controller);
                if (l.Approach < st.Queue.Length) st.Queue[l.Approach]++;
            }
        }

        /// <summary>Whether approach <paramref name="arm"/> of a police or signal junction may go now.</summary>
        private bool Green(LaneGraph.Controller c, int arm)
        {
            if (arm < 0 || arm >= c.PhaseOf.Length) return true;
            CtrlState st = StateOf(c.Id);
            if (st.PhaseLen <= 0f) return false; // not stepped yet
            if (st.ClearT > 0f) return false;
            if (c.Kind == ControllerKind.Signal && st.PhaseT > st.PhaseLen - 5f) return false; // amber and clear
            return c.PhaseOf[arm] == st.Phase;
        }

        // ---- occupancy ----

        private void BuildOccupancy()
        {
            _stamp++;
            _occCount = 0;
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                AddOcc(_agents[i].Lane, _agents[i].S, i);
                if (_agents[i].ChangeFrom >= 0)
                {
                    LaneGraph.Lane from = _g.Lanes[_agents[i].ChangeFrom], to = _g.Lanes[_agents[i].Lane];
                    AddOcc(_agents[i].ChangeFrom, _agents[i].S * from.Length / Math.Max(0.1f, to.Length), i);
                }
            }
            Array.Sort(_occKey, _occAgent, 0, _occCount);
            for (int k = 0; k < _occCount; k++)
            {
                int lane = (int)(_occKey[k] >> 32);
                _occS[k] = (uint)(_occKey[k] & 0xFFFFFFFF) / 1000f - 1000f;
                if (_laneStamp[lane] != _stamp)
                {
                    _laneStamp[lane] = _stamp;
                    _laneFirst[lane] = k;
                    _laneCount[lane] = 0;
                }
                _laneCount[lane]++;
            }
        }

        private void AddOcc(int lane, float s, int agent)
        {
            if (_occCount == _occKey.Length)
            {
                Array.Resize(ref _occKey, _occCount * 2);
                Array.Resize(ref _occAgent, _occCount * 2);
                Array.Resize(ref _occS, _occCount * 2);
            }
            float sc = Math.Max(-999f, Math.Min(4000000f, s));
            _occKey[_occCount] = ((long)lane << 32) | (uint)((sc + 1000f) * 1000f);
            _occAgent[_occCount] = agent;
            _occCount++;
        }

        /// <summary>Inserts a just-spawned agent into the sorted occupancy (keeps later spawns and room checks honest).</summary>
        private void InsertOcc(int agent)
        {
            BuildOccupancy();
        }

        private int OccFirst(int lane)
        {
            return lane >= 0 && lane < _laneStamp.Length && _laneStamp[lane] == _stamp ? _laneFirst[lane] : -1;
        }

        private int OccCount(int lane)
        {
            return lane >= 0 && lane < _laneStamp.Length && _laneStamp[lane] == _stamp ? _laneCount[lane] : 0;
        }

        // ---- obstacles on lanes ----

        private void ProjectObstacles()
        {
            _laneObsCount = 0;
            for (int k = 0; k < _obstacleCount; k++)
            {
                Obstacle o = _obstacles[k];
                _near.Clear();
                _g.LanesNear(o.X, o.Z, o.R + 6.0, _near);
                _near.Sort();
                foreach (int id in _near)
                {
                    LaneGraph.Lane l = _g.Lanes[id];
                    float s;
                    double d = LaneGraph.Project(l, o.X, o.Z, out s);
                    if (d > 0.5 * l.Width + o.R + 2.0) continue;
                    // Signed lateral (right of travel positive).
                    double px, pz;
                    float py, hd;
                    LaneGraph.PointAt(l, s, out px, out pz, out py, out hd);
                    double rx = Math.Cos(hd), rz = -Math.Sin(hd);
                    float lat = (float)((o.X - px) * rx + (o.Z - pz) * rz);
                    if (_laneObsCount == _laneObs.Length) Array.Resize(ref _laneObs, _laneObsCount * 2);
                    _laneObs[_laneObsCount++] = new LaneObstacle { Lane = id, S = s, Lat = lat, R = o.R, Kind = o.Kind };
                }
            }
            // Group by lane (insertion sort; few obstacles).
            for (int i = 1; i < _laneObsCount; i++)
            {
                LaneObstacle v = _laneObs[i];
                int j = i - 1;
                while (j >= 0 && (_laneObs[j].Lane > v.Lane || _laneObs[j].Lane == v.Lane && _laneObs[j].S > v.S))
                {
                    _laneObs[j + 1] = _laneObs[j];
                    j--;
                }
                _laneObs[j + 1] = v;
            }
            _stamp++;
            for (int k = 0; k < _laneObsCount; k++)
            {
                int lane = _laneObs[k].Lane;
                if (_laneObsStamp[lane] != _stamp)
                {
                    _laneObsStamp[lane] = _stamp;
                    _laneObsFirst[lane] = k;
                    _laneObsN[lane] = 0;
                }
                _laneObsN[lane]++;
            }
            _obsStamp = _stamp;
        }

        private int _obsStamp;

        // ---- driving one agent ----

        private float TargetSpeed(ref Agent a, LaneGraph.Lane l, float cong)
        {
            float kmh = l.FreeKmh + (l.PeakKmh - l.FreeKmh) * cong;
            kmh *= a.Idm.SpeedFactor * a.Personal;
            if (a.Idm.CapKmh > 0f) kmh = Math.Min(kmh, a.Idm.CapKmh);
            return Math.Max(1.5f, kmh / 3.6f);
        }

        private void Plan(ref Agent a)
        {
            if (a.P0 < 0) a.P0 = Choose(ref a, a.Lane);
            if (a.P0 >= 0 && a.P1 < 0) a.P1 = Choose(ref a, a.P0);
            if (a.P1 >= 0 && a.P2 < 0) a.P2 = Choose(ref a, a.P1);
            if (a.P2 >= 0 && a.P3 < 0) a.P3 = Choose(ref a, a.P2);
        }

        /// <summary>The next lane after <paramref name="from"/>: the route's when routed, else a weighted random successor
        /// the class may use (straight 3, turns 1, a U-turn only when nothing else).</summary>
        private int Choose(ref Agent a, int from)
        {
            LaneGraph.Lane l = _g.Lanes[from];
            if (l.Next.Length == 0) return -1;
            uint bit = VehicleClasses.Bit(a.Class);
            if (a.Route >= 0 && a.Route < Routes.Count)
            {
                RoutePlan rp = Routes[a.Route];
                // The next planned lane after 'from' along the route, if it is a successor.
                int pos = Array.IndexOf(rp.Lanes, from, Math.Max(0, a.RoutePos));
                if (pos >= 0 && pos + 1 < rp.Lanes.Length)
                {
                    int want = rp.Lanes[pos + 1];
                    if (Array.IndexOf(l.Next, want) >= 0 && _g.IsAlive(new LaneId(want))) return want;
                    // The route continues through a connector: find the successor leading to 'want'.
                    foreach (int nx in l.Next)
                        if (_g.Lanes[nx].Next.Length > 0 && Array.IndexOf(_g.Lanes[nx].Next, want) >= 0)
                            return nx;
                }
            }
            float total = 0f;
            int any = -1;
            for (int k = 0; k < l.Next.Length; k++)
            {
                LaneGraph.Lane n = _g.Lanes[l.Next[k]];
                if ((n.Mask & bit) == 0 || !n.Alive) continue;
                if (n.Kind == LaneKind.Connector && n.Next.Length > 0 && (_g.Lanes[n.Next[0]].Mask & bit) == 0) continue;
                any = l.Next[k];
                total += Weight(l, n);
            }
            if (any < 0) return -1;
            var r = new SimRng(a.Rng);
            float u = r.NextFloat() * total;
            a.Rng = r.NextU64();
            for (int k = 0; k < l.Next.Length; k++)
            {
                LaneGraph.Lane n = _g.Lanes[l.Next[k]];
                if ((n.Mask & bit) == 0 || !n.Alive) continue;
                if (n.Kind == LaneKind.Connector && n.Next.Length > 0 && (_g.Lanes[n.Next[0]].Mask & bit) == 0) continue;
                u -= Weight(l, n);
                if (u < 0f) return l.Next[k];
            }
            return any;
        }

        private static float Weight(LaneGraph.Lane from, LaneGraph.Lane n)
        {
            if (n.Kind == LaneKind.UTurn) return 0.02f;
            if (n.Kind == LaneKind.Ring) return 1f;
            if (from.Kind == LaneKind.Ring) return n.Kind == LaneKind.Connector ? 1f : 1f;
            int e = from.N - 1;
            double ax = from.X[e] - from.X[e - 1], az = from.Z[e] - from.Z[e - 1];
            int m = n.N - 1;
            double bx = n.X[m] - n.X[m - 1], bz = n.Z[m] - n.Z[m - 1];
            double la = Math.Sqrt(ax * ax + az * az), lb = Math.Sqrt(bx * bx + bz * bz);
            double cos = la > 0 && lb > 0 ? (ax * bx + az * bz) / (la * lb) : 1.0;
            if (cos < -0.5) return 0.05f;
            return cos > 0.85 ? 3f : 1f;
        }

        private void Advance(ref Agent a)
        {
            a.P0 = a.P1;
            a.P1 = a.P2;
            a.P2 = a.P3;
            a.P3 = -1;
        }

        private void Drive(int idx, float h, float cong, float wet)
        {
            ref Agent a = ref _agents[idx];
            LaneGraph.Lane l = _g.Lanes[a.Lane];
            Plan(ref a);

            float v0 = TargetSpeed(ref a, l, cong) * (1f - 0.1f * wet);
            float gap = float.PositiveInfinity, lv = 0f;
            a.BlockedByVehicle = false;
            a.BlockedByAnimal = false;
            a.AtStopLine = false;
            a.Anim = 0;
            bool creep = a.WaitT > 0f && l.Controller >= 0 && QueueOf(l) > 25;
            IdmParams p = a.Idm;
            if (creep)
            {
                v0 = Math.Min(v0, 5f / 3.6f);
                p.S0 = 2f;
            }

            // 1. Same-lane leader.
            int self = OccIndex(idx, a.Lane);
            int first = OccFirst(a.Lane), cnt = OccCount(a.Lane);
            if (self >= 0 && self + 1 < first + cnt)
            {
                int j = _occAgent[self + 1];
                float g = _occS[self + 1] - _agents[j].Length - a.S;
                Lead(ref gap, ref lv, g, _agents[j].V, ref a, true);
            }

            // 2. Ahead along the planned path: first agents, diverge siblings, merging feeders, stop lines, speed limits.
            float dist = l.Length - a.S;
            int cur = a.Lane;
            for (int step = 0; step < PathLen && dist < LookaheadM; step++)
            {
                int nxt = step == 0 ? a.P0 : step == 1 ? a.P1 : step == 2 ? a.P2 : a.P3;
                LaneGraph.Lane cl = _g.Lanes[cur];
                if (nxt < 0)
                {
                    // A sink (unloaded neighbour): slow to a stop at the end only when very close (it despawns there).
                    break;
                }
                LaneGraph.Lane nl = _g.Lanes[nxt];
                // Keep the box clear: never drive into a junction whose exit lane has no room for the whole body (a
                // standing queue's tail within one body length of the exit's start), or the body would stand in the
                // junction across the other movements.
                if (step == 0 && nl.Kind == LaneKind.Connector && a.P1 >= 0 && !(a.Committed && a.CommitLane == nxt))
                {
                    int ef = OccFirst(a.P1);
                    if (ef >= 0)
                    {
                        int j = _occAgent[ef];
                        if (j != idx && _agents[j].V < 1f && _occS[ef] - _agents[j].Length < a.Length + 1f)
                        {
                            Lead(ref gap, ref lv, dist - StopLineM, 0f, ref a, false);
                            break;
                        }
                    }
                }
                // Stop line at the end of 'cur' when entering 'nxt' needs permission.
                if (NeedsPermission(cl, nl) && !(step == 0 ? a.Committed && a.CommitLane == nxt : false))
                {
                    if (step == 0)
                    {
                        float brake = a.V * a.V / (2f * p.B) + 3f;
                        if (dist <= brake && CanEnter(idx, ref a, cl, nl))
                        {
                            a.Committed = true;
                            a.CommitLane = nxt;
                            _resv[nxt]++;
                        }
                        else
                        {
                            Lead(ref gap, ref lv, dist - StopLineM, 0f, ref a, false);
                            a.AtStopLine = dist < 15f;
                            break;
                        }
                    }
                    else
                    {
                        // A later junction: approach its stop line ready to stop (permission is asked one lane before).
                        Lead(ref gap, ref lv, dist - StopLineM, 0f, ref a, false);
                        break;
                    }
                }
                // Speed limit of the next lane (curves, rings): v_allowed = sqrt(v_next² + 2 b d).
                float vn = TargetSpeed(ref a, nl, cong);
                if (vn < v0)
                {
                    float vAllowed = MathF.Sqrt(vn * vn + 2f * p.B * Math.Max(0f, dist));
                    if (vAllowed < v0) v0 = vAllowed;
                }
                // First agent on the next lane.
                int nf = OccFirst(nxt);
                if (nf >= 0)
                {
                    int j = _occAgent[nf];
                    if (j != idx) Lead(ref gap, ref lv, dist + _occS[nf] - _agents[j].Length, _agents[j].V, ref a, true);
                }
                // Diverging siblings near the split.
                for (int k = 0; k < cl.Next.Length; k++)
                {
                    int sib = cl.Next[k];
                    if (sib == nxt) continue;
                    int sf = OccFirst(sib);
                    if (sf < 0) continue;
                    int j = _occAgent[sf];
                    if (j == idx || _occS[sf] > _agents[j].Length + 6f) continue;
                    Lead(ref gap, ref lv, dist + _occS[sf] - _agents[j].Length, _agents[j].V, ref a, true);
                }
                // Merging feeders of the next lane: an agent closer to the merge point will be ahead.
                for (int k = 0; k < nl.Prev.Length; k++)
                {
                    int pr = nl.Prev[k];
                    if (pr == cur) continue;
                    LaneGraph.Lane pl = _g.Lanes[pr];
                    int pf = OccFirst(pr), pn = OccCount(pr);
                    for (int q = pf; q >= 0 && q < pf + pn; q++)
                    {
                        int j = _occAgent[q];
                        if (j == idx) continue;
                        ref Agent b = ref _agents[j];
                        int bNext = b.Lane == pr ? b.P0 : -1;
                        if (bNext != nxt) continue;
                        float dp = pl.Length - _occS[q];
                        if (dp < dist || dp == dist && b.Id < a.Id)
                            Lead(ref gap, ref lv, dist - dp - b.Length, b.V, ref a, true);
                    }
                }
                dist += nl.Length;
                cur = nxt;
            }

            // 2b. Geometric safety net: any body (same direction or crossing) inside my corridor within 12 m ahead is a
            // leader, whatever lanes the data gave us (overlapping ways, odd junction mouths).
            if (!a.Ghost) Corridor(idx, ref a, ref gap, ref lv);

            // 3. Obstacles (cows, dogs, pedestrians, the player) on the lane ahead, with soft avoidance.
            Obstacles(ref a, l, ref gap, ref lv, ref v0);

            // 4. Shared lanes: pass oncoming agents by shifting left; otherwise two-wheelers filter.
            if (!SharedPass(idx, ref a, l, ref gap, ref lv, ref v0) && !_avoiding)
                a.LatTarget = VehicleClasses.IsTwoWheel(a.Class) ? ClampLat(a.Filter, l, a.Half) : 0f;

            // 5. Bus and micro stops.
            Stops(idx, ref a, l, ref gap, ref lv, h);

            // IDM and integration.
            float acc = TrafficTables.IdmAccel(p, a.V, v0, gap, a.V - lv);
            if (acc < -9f) acc = -9f;
            if (a.DwellT > 0f) acc = -p.B * 2f;
            float vNew = a.V + acc * h;
            if (vNew < 0f) vNew = 0f;
            float ds = 0.5f * (a.V + vNew) * h;
            if (!float.IsInfinity(gap))
            {
                float room = Math.Max(0f, gap - 0.2f);
                if (ds > room)
                {
                    ds = room;
                    vNew = Math.Min(vNew, room / h);
                    if (gap < 0.5f) vNew = 0f;
                }
            }
            a.A = (vNew - a.V) / h;
            a.V = vNew;
            a.S += ds;
            a.Odo += ds;

            // Waiting and horns.
            bool stopped = a.V < 0.3f;
            if (stopped) a.WaitT += h;
            else a.WaitT = 0f;
            if (stopped && a.BlockedByVehicle) a.BlockedT += h;
            else if (!stopped)
            {
                a.BlockedT = 0f;
                a.Horns = 0;
            }
            if (!a.Ghost) Horns(idx, ref a, l, h);
            if (a.Anim == 0 && a.A < -1.0f) a.Anim |= (byte)AgentAnim.Braking;
            if (a.Committed && a.CommitLane >= 0) Indicator(ref a, l, _g.Lanes[a.CommitLane]);

            // Lateral blend (lane change, filtering, avoidance, passing).
            float latRate = LateralRateMps * h;
            if (a.ChangeFrom >= 0)
            {
                a.ChangeT -= h;
                if (a.ChangeT <= 0f) a.ChangeFrom = -1;
            }
            a.Lat += Math.Max(-latRate, Math.Min(latRate, a.LatTarget - a.Lat));

            // Lane transitions.
            while (a.S > _g.Lanes[a.Lane].Length)
            {
                LaneGraph.Lane cl = _g.Lanes[a.Lane];
                int nxt = a.P0;
                if (nxt < 0)
                {
                    Kill(idx); // a sink: the neighbour tile is not loaded (far from the player)
                    return;
                }
                if (NeedsPermission(cl, _g.Lanes[nxt]) && !(a.Committed && a.CommitLane == nxt))
                {
                    a.S = cl.Length; // never cross an unpermitted stop line
                    a.V = 0f;
                    break;
                }
                a.S -= cl.Length;
                if (a.Committed && a.CommitLane == a.Lane)
                {
                    // Leaving the committed connector: the front is out, the tail still in (its reservation stays).
                    a.Committed = false;
                    a.TailLane = a.CommitLane;
                    a.CommitLane = -1;
                }
                a.PrevLane = a.Lane;
                a.Lane = nxt;
                a.LaneGen = _g.Lanes[nxt].Gen;
                a.ChangeFrom = -1;
                if (a.Route >= 0 && a.Route < Routes.Count)
                {
                    int pos = Array.IndexOf(Routes[a.Route].Lanes, nxt, Math.Max(0, a.RoutePos));
                    if (pos >= 0) a.RoutePos = pos;
                    else if (_g.Lanes[nxt].Kind == LaneKind.Road) a.Route = -1; // left the route: free traffic from now
                }
                Advance(ref a);
                Plan(ref a);
            }
            if (a.Committed && a.CommitLane != a.Lane && a.CommitLane != a.P0)
            {
                // The plan changed under a reservation (lane change): drop it.
                if (_resv[a.CommitLane] > 0) _resv[a.CommitLane]--;
                a.Committed = false;
                a.CommitLane = -1;
            }
        }

        private const float CorridorAheadM = 12f, CorridorMarginM = 0.1f;

        private void Corridor(int idx, ref Agent a, ref float gap, ref float lv)
        {
            double fx = Math.Sin(a.Heading), fz = Math.Cos(a.Heading);
            double ox = a.X + fx * (a.Wheelbase + a.FrontOverhang), oz = a.Z + fz * (a.Wheelbase + a.FrontOverhang);
            LaneGraph.Lane mine = _g.Lanes[a.Lane];
            bool mineLoose = mine.Kind != LaneKind.Road || mine.Shared;
            for (int j = 0; j < _agents.Length; j++)
            {
                if (j == idx || !_agents[j].Alive || _agents[j].Ghost) continue;
                ref Agent b = ref _agents[j];
                double dx = b.X - ox, dz = b.Z - oz;
                if (dx * dx + dz * dz > 400.0) continue;
                double bfx = Math.Sin(b.Heading), bfz = Math.Cos(b.Heading);
                double cos = fx * bfx + fz * bfz;
                float lim = a.Half + CorridorMarginM, reach = CorridorAheadM;
                if (cos < -0.3)
                {
                    // Oncoming bodies matter only around connectors and shared lanes (on ordinary two-way roads each
                    // direction has its own lane): no margin, so a pair properly shifted to pass never blocks.
                    LaneGraph.Lane theirs = _g.Lanes[b.Lane];
                    if (!mineLoose && theirs.Kind == LaneKind.Road && !theirs.Shared) continue;
                    lim = a.Half;
                    reach = 8f;
                }
                double brx = bfz, brz = -bfx;
                float back = b.Length - b.Wheelbase - b.FrontOverhang, ahead = b.Wheelbase + b.FrontOverhang;
                float best = float.PositiveInfinity;
                // Sample the other body's outline (rectangle edges, 5 points each).
                for (int e = 0; e < 4; e++)
                {
                    float l0 = e == 0 || e == 3 ? -back : ahead, w0 = e < 2 ? -b.Half : b.Half;
                    float l1 = e == 0 || e == 1 ? ahead : -back, w1 = e == 0 ? -b.Half : e == 1 ? b.Half : e == 2 ? b.Half : -b.Half;
                    if (e == 1)
                    {
                        l0 = ahead;
                        w0 = -b.Half;
                        l1 = ahead;
                        w1 = b.Half;
                    }
                    else if (e == 3)
                    {
                        l0 = -back;
                        w0 = b.Half;
                        l1 = -back;
                        w1 = -b.Half;
                    }
                    else if (e == 0)
                    {
                        l0 = -back;
                        w0 = -b.Half;
                        l1 = ahead;
                        w1 = -b.Half;
                    }
                    else
                    {
                        l0 = ahead;
                        w0 = b.Half;
                        l1 = -back;
                        w1 = b.Half;
                    }
                    for (int k = 0; k <= 4; k++)
                    {
                        float t = k / 4f;
                        float l = l0 + (l1 - l0) * t, w = w0 + (w1 - w0) * t;
                        double px = b.X + bfx * l + brx * w - ox, pz = b.Z + bfz * l + brz * w - oz;
                        double along = px * fx + pz * fz, lat = px * fz - pz * fx;
                        if (Math.Abs(lat) > lim || along < -0.5 || along > reach) continue;
                        if (along < best) best = (float)along;
                    }
                }
                if (best < gap)
                {
                    gap = best;
                    lv = (float)(b.V * Math.Max(0.0, cos));
                    a.BlockedByVehicle = true;
                }
            }
        }

        private int OccIndex(int agent, int lane)
        {
            int f = OccFirst(lane), n = OccCount(lane);
            for (int k = f; k >= 0 && k < f + n; k++)
                if (_occAgent[k] == agent)
                    return k;
            return -1;
        }

        private static void Lead(ref float gap, ref float lv, float g, float v, ref Agent a, bool vehicle)
        {
            if (g < gap)
            {
                gap = g;
                lv = v;
                a.BlockedByVehicle = vehicle;
            }
        }

        /// <summary>True when entering <paramref name="next"/> from <paramref name="cur"/> needs a reservation or a
        /// controller's permission (junction connectors, ring entries); plain continuations (seams, joints) do not.</summary>
        private bool NeedsPermission(LaneGraph.Lane cur, LaneGraph.Lane next)
        {
            if (next.Kind != LaneKind.Connector && next.Kind != LaneKind.UTurn) return false;
            if (cur.Controller >= 0 && _g.ControllerKindOf(cur.Controller) != ControllerKind.None) return true;
            return next.Conflicts.Length > 0;
        }

        /// <summary>
        /// May agent <paramref name="a"/> commit to connector <paramref name="next"/> now? The controller must allow its
        /// approach (police phase, signal green, roundabout gap, priority yield), no conflicting connector may be
        /// reserved, and the target lane must have room for the body.
        /// </summary>
        private bool CanEnter(int idx, ref Agent a, LaneGraph.Lane cur, LaneGraph.Lane next)
        {
            for (int k = 0; k < next.Conflicts.Length; k++)
                if (_resv[next.Conflicts[k]] > 0)
                    return false;
            if (next.Next.Length > 0 && !TargetRoom(idx, ref a, next)) return false;
            if (cur.Controller < 0) return true;
            LaneGraph.Controller c = _g.Controllers[cur.Controller];
            if (!c.Alive) return true;
            switch (c.Kind)
            {
                case ControllerKind.Police:
                case ControllerKind.Signal:
                    return Green(c, cur.Approach);
                case ControllerKind.Roundabout:
                    return cur.Priority || RingGap(ref a, next);
                case ControllerKind.Priority:
                    return a.WaitT > 4f || !MustYield(idx, ref a, cur, next, c);
                default:
                    return true;
            }
        }

        /// <summary>Room on the connector's target lane: free length at its start minus the bodies already headed there.</summary>
        private bool TargetRoom(int idx, ref Agent a, LaneGraph.Lane conn)
        {
            int target = conn.Next[0];
            LaneGraph.Lane t = _g.Lanes[target];
            float room;
            int tf = OccFirst(target);
            if (tf >= 0)
            {
                int j = _occAgent[tf];
                room = _occS[tf] - _agents[j].Length;
            }
            else
            {
                room = Math.Max(t.Length, 30f);
            }
            // Bodies on (or committed to) the connector itself.
            for (int i = 0; i < _agents.Length; i++)
            {
                if (i == idx || !_agents[i].Alive) continue;
                ref Agent b = ref _agents[i];
                if (b.Lane == conn.Id || b.Committed && b.CommitLane == conn.Id) room -= b.Length + b.Idm.S0;
            }
            return room >= a.Length + a.Idm.S0 * 0.5f;
        }

        /// <summary>Gap acceptance at a roundabout entry (W2_DESIGN 5.1): no circulating agent may reach the merge point
        /// within 3.0 s (cars), 2.0 s (two-wheelers) or 4.5 s (buses and trucks).</summary>
        private bool RingGap(ref Agent a, LaneGraph.Lane entry)
        {
            float need = VehicleClasses.IsTwoWheel(a.Class) ? 2.0f : VehicleClasses.IsHeavy(a.Class) ? 4.5f : 3.0f;
            if (entry.Next.Length == 0) return true;
            LaneGraph.Lane target = _g.Lanes[entry.Next[0]];
            // Ring feeders of the target (other than this entry) and one lane further up.
            for (int k = 0; k < target.Prev.Length; k++)
            {
                int pr = target.Prev[k];
                if (pr == entry.Id) continue;
                if (!RingFeederClear(pr, 0f, need, 0)) return false;
            }
            return true;
        }

        private const float RingLookMps = 12f;

        private bool RingFeederClear(int lane, float extra, float need, int depth)
        {
            LaneGraph.Lane l = _g.Lanes[lane];
            int f = OccFirst(lane), n = OccCount(lane);
            for (int q = f; q >= 0 && q < f + n; q++)
            {
                ref Agent b = ref _agents[_occAgent[q]];
                float d = l.Length - _occS[q] + extra;
                if (d / Math.Max(b.V, 1.0f) < need) return false;
            }
            // Look up the ring by distance, not lane count: rings are cut into short pieces and joints, so a car three
            // pieces up can be a second away. Bounded by what a circulating car could cover within the gap (12 m/s).
            if (depth >= 8 || extra + l.Length > need * RingLookMps) return true;
            for (int k = 0; k < l.Prev.Length; k++)
            {
                LaneGraph.Lane pl = _g.Lanes[l.Prev[k]];
                if (!pl.Ring && pl.Kind != LaneKind.Ring && !pl.Priority) continue;
                if (!RingFeederClear(l.Prev[k], extra + l.Length, need, depth + 1)) return false;
            }
            return true;
        }

        /// <summary>Priority junctions: the higher road class goes first; equal classes yield to the right; an agent
        /// within 2.5 s of a conflicting stop line with priority makes this one wait.</summary>
        private bool MustYield(int idx, ref Agent a, LaneGraph.Lane cur, LaneGraph.Lane next, LaneGraph.Controller c)
        {
            int myArm = cur.Approach;
            for (int k = 0; k < next.Conflicts.Length; k++)
            {
                LaneGraph.Lane x = _g.Lanes[next.Conflicts[k]];
                if (x.Prev.Length == 0) continue;
                int src = x.Prev[0];
                LaneGraph.Lane sl = _g.Lanes[src];
                int arm = sl.Approach;
                if (arm < 0 || arm == myArm || !HasPriority(c, arm, myArm)) continue;
                int f = OccFirst(src), n = OccCount(src);
                for (int q = f; q >= 0 && q < f + n; q++)
                {
                    int j = _occAgent[q];
                    if (j == idx) continue;
                    ref Agent b = ref _agents[j];
                    if (b.Lane != src || b.P0 != x.Id) continue;
                    float d = sl.Length - _occS[q];
                    if (d / Math.Max(b.V, 1.5f) < 2.5f) return true;
                }
            }
            return false;
        }

        private static bool HasPriority(LaneGraph.Controller c, int arm, int other)
        {
            int ra = c.ArmRank[arm], rb = c.ArmRank[other];
            if (ra != rb) return ra > rb;
            // Equal classes: yield to the one coming from the right. Arms are sorted by angle (counter-clockwise); the arm
            // to the right of an approaching vehicle is the next one clockwise, i.e. the previous index.
            int n = c.Arms;
            return arm == (other - 1 + n) % n;
        }

        private void Indicator(ref Agent a, LaneGraph.Lane cur, LaneGraph.Lane conn)
        {
            if (conn.N < 2 || cur.N < 2) return;
            int e = cur.N - 1;
            double ax = cur.X[e] - cur.X[e - 1], az = cur.Z[e] - cur.Z[e - 1];
            int m = conn.N - 1;
            double bx = conn.X[m] - conn.X[m - 1], bz = conn.Z[m] - conn.Z[m - 1];
            double cross = ax * bz - az * bx;
            double la = Math.Sqrt(ax * ax + az * az) * Math.Sqrt(bx * bx + bz * bz);
            if (la < 1e-9) return;
            double s = cross / la;
            if (s > 0.35) a.Anim |= (byte)AgentAnim.IndicatorLeft;
            else if (s < -0.35) a.Anim |= (byte)AgentAnim.IndicatorRight;
        }

        private static float ClampLat(float lat, LaneGraph.Lane l, float half)
        {
            float m = Math.Max(0f, 0.5f * l.Width - half - 0.1f);
            return Math.Max(-m, Math.Min(m, lat));
        }

        private void Obstacles(ref Agent a, LaneGraph.Lane l, ref float gap, ref float lv, ref float v0)
        {
            _avoiding = false;
            if (_laneObsCount == 0) return;
            float avoid = 0f;
            bool want = false;
            // Obstacles on this lane (ahead or alongside) and on the next planned lane.
            for (int step = 0; step < 2; step++)
            {
                int lane = step == 0 ? a.Lane : a.P0;
                if (lane < 0 || _laneObsStamp[lane] != _obsStamp) continue;
                LaneGraph.Lane cl = _g.Lanes[lane];
                float offset = step == 0 ? -a.S : l.Length - a.S; // obstacle arc length → distance ahead of my front
                int f = _laneObsFirst[lane], n = _laneObsN[lane];
                for (int k = f; k < f + n; k++)
                {
                    LaneObstacle o = _laneObs[k];
                    float ahead = o.S + offset;
                    if (ahead < -(a.Length + o.R) || ahead > 40f) continue;
                    bool animal = o.Kind == ObstacleKind.Cow || o.Kind == ObstacleKind.Dog || o.Kind == ObstacleKind.Goat;
                    float myLat = step == 0 ? a.Lat : 0f;
                    float clear = Math.Abs(o.Lat - myLat) - (o.R + a.Half);
                    if (o.Kind == ObstacleKind.Pedestrian && ahead < 6f && (cl.Area == AreaType.Urban || cl.Area == AreaType.OldCore))
                        v0 = Math.Min(v0, 15f / 3.6f);
                    // Soft avoidance (about 3 m around animals): steer to the far side within the lane, slowly.
                    if (step == 0 && ahead < 25f)
                    {
                        float side = o.Lat >= 0f ? -1f : 1f;
                        float wantLat = o.Lat + side * (o.R + a.Half + 0.35f);
                        float lim = 0.5f * cl.Width + 0.3f - a.Half;
                        if (Math.Abs(wantLat) <= lim)
                        {
                            avoid = wantLat;
                            want = true;
                        }
                        if (animal) v0 = Math.Min(v0, 10f / 3.6f);
                    }
                    if (clear < 0.25f)
                    {
                        // In the path: stop short of it (the cow keeps chewing; nobody honks at it).
                        float g = ahead - o.R - 1.0f;
                        if (g < gap)
                        {
                            gap = g;
                            lv = 0f;
                            a.BlockedByVehicle = o.Kind == ObstacleKind.Player || o.Kind == ObstacleKind.ParkedVehicle;
                            a.BlockedByAnimal = animal;
                        }
                        if (animal) a.Anim |= (byte)AgentAnim.Yielding;
                    }
                }
            }
            if (want) a.LatTarget = avoid;
            _avoiding = want;
        }

        private bool _avoiding;

        /// <summary>
        /// Shared lanes (two-way roads under 5.5 m real): each direction keeps to its half; an oncoming agent within 40 m
        /// along the path (on the twin of the current or a coming shared lane, or about to come onto it) slows both to
        /// 10 km/h while they pass.
        /// </summary>
        private bool SharedPass(int idx, ref Agent a, LaneGraph.Lane l, ref float gap, ref float lv, ref float v0)
        {
            bool oncoming = false;
            float closest = float.PositiveInfinity;
            float start = -a.S; // arc of the path lane's start relative to my front
            int lane = a.Lane;
            for (int step = 0; step < 3 && lane >= 0 && start < 40f; step++)
            {
                LaneGraph.Lane cl = _g.Lanes[lane];
                if (cl.Shared && cl.Twin >= 0)
                {
                    LaneGraph.Lane tl = _g.Lanes[cl.Twin];
                    Oncoming(idx, ref a, cl.Twin, start + tl.Length, ref oncoming, ref closest);
                    for (int k = 0; k < tl.Prev.Length; k++)
                    {
                        LaneGraph.Lane fl = _g.Lanes[tl.Prev[k]];
                        Oncoming(idx, ref a, tl.Prev[k], start + cl.Length + fl.Length, ref oncoming, ref closest);
                    }
                }
                start += cl.Length;
                lane = step == 0 ? a.P0 : step == 1 ? a.P1 : a.P2;
            }
            if (oncoming) v0 = Math.Min(v0, 10f / 3.6f);
            return false;
        }

        /// <summary>Oncoming agents on <paramref name="lane"/>, which runs against my path and ends
        /// <paramref name="endAhead"/> metres... at arc <c>endAhead − S</c> ahead of my front.</summary>
        private void Oncoming(int idx, ref Agent a, int lane, float endAhead, ref bool oncoming, ref float closest)
        {
            int f = OccFirst(lane), n = OccCount(lane);
            for (int q = f; q >= 0 && q < f + n; q++)
            {
                int j = _occAgent[q];
                if (j == idx) continue;
                ref Agent b = ref _agents[j];
                float front = endAhead - _occS[q]; // b's front, ahead of my front
                if (front + b.Length < -a.Length - 1f) continue; // fully passed
                if (front > 40f) continue;
                oncoming = true;
                if (front < closest) closest = front;
            }
        }

        private void Stops(int idx, ref Agent a, LaneGraph.Lane l, ref float gap, ref float lv, float h)
        {
            if (a.DwellT > 0f)
            {
                a.DwellT -= h;
                a.Anim |= (byte)AgentAnim.AtStop;
                gap = Math.Min(gap, 0.1f);
                lv = 0f;
                return;
            }
            // Route stops.
            if (a.Route >= 0 && a.Route < Routes.Count)
            {
                RoutePlan rp = Routes[a.Route];
                if (a.NextStop < 0) a.NextStop = FirstStopFrom(rp, a.Lane, a.S);
                if (a.NextStop >= 0 && a.NextStop < rp.StopLane.Length)
                {
                    int sl = rp.StopLane[a.NextStop];
                    float d = float.PositiveInfinity; // not on this lane or the next: no stop yet
                    if (sl == a.Lane) d = rp.StopS[a.NextStop] - a.S;
                    else if (sl == a.P0) d = l.Length - a.S + rp.StopS[a.NextStop];
                    if (d >= -1f && d < 60f)
                    {
                        if (d < 1.0f && a.V < 0.6f)
                        {
                            var r = new SimRng(a.Rng);
                            a.DwellT = r.Range(8f, 15f);
                            a.Rng = r.NextU64();
                            int stopIdx = rp.StopIndex[a.NextStop];
                            if (StopReached != null) _stopEvents.Add(new KeyValuePair<int, int>(a.Id, rp.RouteIndex * 65536 + stopIdx));
                            a.NextStop++;
                            if (a.NextStop >= rp.StopLane.Length) a.NextStop = int.MaxValue;
                        }
                        else
                        {
                            // The stop is a standing leader the bus should reach: IDM keeps its minimum gap s0 to any
                            // leader, so the virtual one stands s0 beyond the stop, or the bus would halt short of it.
                            float stopGap = Math.Max(0f, d) + a.Idm.S0;
                            if (stopGap < gap)
                            {
                                gap = stopGap;
                                lv = 0f;
                            }
                        }
                    }
                    else if (d < -1f && sl == a.Lane)
                    {
                        a.NextStop++; // passed (spawned beyond it)
                    }
                }
            }
            // Micros, minibuses and tempos also stop at random about every 600 m.
            if (a.RandomStopAt > 0f && a.Odo >= a.RandomStopAt && !a.Ghost && l.Kind == LaneKind.Road && a.V < 3f)
            {
                var r = new SimRng(a.Rng);
                a.DwellT = r.Range(8f, 20f);
                a.RandomStopAt = a.Odo + r.Range(400f, 800f);
                a.Rng = r.NextU64();
            }
        }

        private readonly List<KeyValuePair<int, int>> _stopEvents = new List<KeyValuePair<int, int>>();

        private static int FirstStopFrom(RoutePlan rp, int lane, float s)
        {
            int pos = Array.IndexOf(rp.Lanes, lane);
            if (pos < 0) return -1;
            for (int k = 0; k < rp.StopLane.Length; k++)
            {
                int sp = Array.IndexOf(rp.Lanes, rp.StopLane[k]);
                if (sp > pos || sp == pos && rp.StopS[k] >= s - 1f) return k;
            }
            return int.MaxValue;
        }

        // ---- horns (W2_DESIGN 5.1, post-2017 no-horn rule) ----

        private void Horns(int idx, ref Agent a, LaneGraph.Lane l, float h)
        {
            if (!_s.Horns || a.BlockedByAnimal) return; // never at cows or dogs
            float scale = l.Area == AreaType.OldCore ? 0.3f : 1f;
            var r = new SimRng(a.Rng);
            bool changed = false;
            // Blocked more than 4 s by a stopped vehicle: 0.3, then 0.2 every 6–10 s, at most 3 per blockage.
            if (a.BlockedByVehicle && a.BlockedT > 4f && a.Horns < 3)
            {
                a.HornTimer -= h;
                if (a.Horns == 0 && a.HornTimer <= 0f || a.Horns > 0 && a.HornTimer <= 0f)
                {
                    float pr = (a.Horns == 0 ? 0.3f : 0.2f) * scale;
                    if (r.Chance(pr)) Honk(ref a, HornKind.Blocked);
                    a.Horns++;
                    a.HornTimer = r.Range(6f, 10f);
                    changed = true;
                }
            }
            // The front car did not move 2 s after a green phase or a police wave: 0.4.
            if (a.BlockedByVehicle && l.Controller >= 0 && a.WaitT > 2f)
            {
                LaneGraph.Controller c = _g.Controllers[l.Controller];
                if (c.Alive && (c.Kind == ControllerKind.Police || c.Kind == ControllerKind.Signal) && Green(c, l.Approach) &&
                    StateOf(c.Id).GreenT > 2f && StateOf(c.Id).GreenT - h <= 2f)
                {
                    if (r.Chance(0.4f * scale)) Honk(ref a, HornKind.GreenNotMoving);
                    changed = true;
                }
            }
            if (changed) a.Rng = r.NextU64();
        }

        private void Honk(ref Agent a, HornKind k)
        {
            if (_hornsBackCount == _hornsBack.Length) Array.Resize(ref _hornsBack, _hornsBackCount * 2);
            _hornsBack[_hornsBackCount++] = new HornEvent { AgentId = a.Id, Kind = k, Class = a.Class, X = a.X, Z = a.Z };
        }

        // ---- lane changes: overtaking on the right, keeping left ----

        private void LaneChanges(float dt)
        {
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                if (a.Ghost || a.ChangeFrom >= 0 || a.Committed || a.DwellT > 0f) continue;
                a.ChangeTimer -= dt;
                if (a.ChangeTimer > 0f) continue;
                a.ChangeTimer = 1f;
                LaneGraph.Lane l = _g.Lanes[a.Lane];
                if (l.Kind != LaneKind.Road || l.Count < 2 || l.Length - a.S < 25f) continue;
                if (a.Route >= 0) continue;
                // Leader on this lane.
                float gap = float.PositiveInfinity, lv = 0f;
                int self = OccIndex(i, a.Lane);
                if (self >= 0 && self + 1 < OccFirst(a.Lane) + OccCount(a.Lane))
                {
                    int j = _occAgent[self + 1];
                    gap = _occS[self + 1] - _agents[j].Length - a.S;
                    lv = _agents[j].V;
                }
                float v0 = TargetSpeed(ref a, l, 0f);
                int target = -1;
                bool overtake = false;
                if (l.Right >= 0 && gap < 30f && lv < v0 - 3f && !VehicleClasses.IsTwoWheel(a.Class) || l.Right >= 0 && gap < 12f && lv < a.V - 1f)
                {
                    target = l.Right;
                    overtake = true;
                }
                else if (l.Left >= 0 && (gap > 60f || lv >= v0 - 0.5f))
                {
                    target = l.Left; // keep left
                }
                if (target < 0 || !_g.IsAlive(new LaneId(target))) continue;
                LaneGraph.Lane tl = _g.Lanes[target];
                if ((tl.Mask & VehicleClasses.Bit(a.Class)) == 0) continue;
                float s2 = a.S * tl.Length / Math.Max(0.1f, l.Length);
                if (!ChangeSafe(i, ref a, target, s2)) continue;
                float dLat = l.Offset - tl.Offset;
                if (!l.Forward) dLat = -dLat;
                a.ChangeFrom = a.Lane;
                a.ChangeT = LaneChangeS;
                a.Lane = target;
                a.LaneGen = tl.Gen;
                a.S = s2;
                a.Lat += dLat;
                ClearPath(ref a);
                if (overtake && _s.Horns && !a.Ghost)
                {
                    var r = new SimRng(a.Rng);
                    float p = a.Class == VehicleClass.TwoWheeler ? 0.35f : 0.25f;
                    if (_g.Lanes[a.Lane].Area == AreaType.OldCore) p *= 0.3f;
                    if (r.Chance(p)) Honk(ref a, HornKind.Overtake);
                    a.Rng = r.NextU64();
                }
            }
        }

        private bool ChangeSafe(int idx, ref Agent a, int lane, float s)
        {
            int f = OccFirst(lane), n = OccCount(lane);
            for (int q = f; q >= 0 && q < f + n; q++)
            {
                int j = _occAgent[q];
                if (j == idx) continue;
                ref Agent b = ref _agents[j];
                float bs = _occS[q];
                if (bs >= s)
                {
                    float g = bs - b.Length - s;
                    if (g < a.Idm.S0 + a.V * a.Idm.T * 0.6f + 1f) return false;
                }
                else
                {
                    float g = s - a.Length - bs;
                    if (g < b.Idm.S0 + b.V * b.Idm.T + 1f) return false;
                }
            }
            return true;
        }

        // ---- despawn ----

        private void Despawn()
        {
            if (!_hasFocus) return;
            double far = 1.2 * _s.GhostRadiusM;
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                double dx = a.X - _focusX, dz = a.Z - _focusZ;
                double d = Math.Sqrt(dx * dx + dz * dz);
                bool outOfView = !InView(a.X, a.Z);
                if (d > far && outOfView || a.WaitT > 60f && outOfView && d > _s.SpawnMinM)
                {
                    if (a.Committed && a.CommitLane >= 0 && _resv[a.CommitLane] > 0) _resv[a.CommitLane]--;
                    if (a.TailLane >= 0 && _resv[a.TailLane] > 0) _resv[a.TailLane]--;
                    Kill(i);
                }
            }
        }

        // ---- poses ----

        private void Poses(float dt)
        {
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                LaneGraph.Lane l = _g.Lanes[a.Lane];
                if (a.Ghost)
                {
                    double x, z;
                    float y, hd;
                    LaneGraph.PointAt(l, a.S - a.FrontOverhang - a.Wheelbase, out x, out z, out y, out hd);
                    a.X = x;
                    a.Z = z;
                    a.Y = y;
                    a.Heading = hd;
                    a.Lean = 0f;
                    a.Pitch = 0f;
                    a.Roll = 0f;
                    a.Anim |= (byte)AgentAnim.Ghost;
                    continue;
                }
                double rx, rz, fx, fz;
                float ry, fy;
                BodyPoint(ref a, a.S - a.FrontOverhang - a.Wheelbase, out rx, out rz, out ry);
                BodyPoint(ref a, a.S - a.FrontOverhang, out fx, out fz, out fy);
                float heading = (float)Math.Atan2(fx - rx, fz - rz);
                // Lateral offset to the right of travel.
                double nx = Math.Cos(heading), nz = -Math.Sin(heading);
                a.X = rx + nx * a.Lat;
                a.Z = rz + nz * a.Lat;
                a.Y = ry;
                float dh = ArcadeVehicle.WrapAngle(heading - a.Heading);
                a.YawRate += (dh / Math.Max(dt, 1e-3f) - a.YawRate) * Math.Min(1f, dt * 6f);
                a.Heading = heading;
                a.Pitch = (float)Math.Atan2(fy - ry, Math.Max(0.5f, a.Wheelbase));
                float aLat = a.V * a.YawRate;
                if (VehicleClasses.IsTwoWheel(a.Class))
                {
                    float cap = 38f * (float)(Math.PI / 180.0);
                    a.Lean = Math.Max(-cap, Math.Min(cap, MathF.Atan(aLat / Gravity)));
                    a.Roll = 0f;
                    if (a.V < 1f / 3.6f) a.Anim |= (byte)AgentAnim.FootDown;
                }
                else
                {
                    bool heavy = VehicleClasses.IsHeavy(a.Class) || a.Class == VehicleClass.Tempo;
                    float k = (heavy ? 1.0f : 0.6f) * (float)(Math.PI / 180.0), cap = (heavy ? 6f : 4f) * (float)(Math.PI / 180.0);
                    a.Roll = Math.Max(-cap, Math.Min(cap, -k * aLat));
                    a.Lean = 0f;
                }
            }
        }

        /// <summary>A point of the body at arc length <paramref name="s"/> of the agent's lane, continuing into the previous
        /// lane (or straight back along the lane start) when negative.</summary>
        private void BodyPoint(ref Agent a, float s, out double x, out double z, out float y)
        {
            LaneGraph.Lane l = _g.Lanes[a.Lane];
            float h;
            if (s >= 0f || a.PrevLane < 0 || !_g.IsAlive(new LaneId(a.PrevLane)))
            {
                if (s >= 0f)
                {
                    LaneGraph.PointAt(l, s, out x, out z, out y, out h);
                    return;
                }
                LaneGraph.PointAt(l, 0f, out x, out z, out y, out h);
                x += Math.Sin(h) * s;
                z += Math.Cos(h) * s;
                return;
            }
            LaneGraph.Lane p = _g.Lanes[a.PrevLane];
            LaneGraph.PointAt(p, p.Length + s, out x, out z, out y, out h);
        }

        private void Publish()
        {
            int n = 0;
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                if (n == _back.Length) Array.Resize(ref _back, n * 2);
                _back[n++] = new AgentPose
                {
                    X = a.X, Z = a.Z, Y = a.Y, HeadingRad = a.Heading, SpeedMps = a.V, Lean = a.Lean, Pitch = a.Pitch, Roll = a.Roll,
                    Class = a.Class, Variant = a.Variant, Livery = a.Livery, AnimState = a.Anim, AgentId = a.Id,
                };
            }
            lock (_snapLock)
            {
                AgentPose[] t = _front;
                _front = _back;
                _back = t.Length >= _front.Length ? t : new AgentPose[_front.Length];
                _frontCount = n;
                HornEvent[] th = _hornsFront;
                _hornsFront = _hornsBack;
                _hornsBack = th.Length >= _hornsFront.Length ? th : new HornEvent[_hornsFront.Length];
                _hornsFrontCount = _hornsBackCount;
            }
            if (Horn != null)
                for (int k = 0; k < _hornsFrontCount; k++)
                    Horn(_hornsFront[k].AgentId, _hornsFront[k].Kind);
            if (_stopEvents.Count > 0)
            {
                if (StopReached != null)
                    foreach (var kv in _stopEvents)
                        StopReached(kv.Key, kv.Value / 65536, kv.Value % 65536);
                _stopEvents.Clear();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Diagnostics for tests (allocation is fine here)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The body rectangle of every live agent: rear-axle point, heading, length behind and ahead of it,
        /// half width; plus whether the agent is a ghost.</summary>
        public int CopyBodies(BodyBox[] dst)
        {
            int n = 0;
            for (int i = 0; i < _agents.Length && n < dst.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                VehicleCatalogEntry e = VehicleCatalog.At(a.Variant);
                dst[n++] = new BodyBox
                {
                    AgentId = a.Id, X = a.X, Z = a.Z, HeadingRad = a.Heading, Back = e.RearOverhangM, Ahead = a.Wheelbase + a.FrontOverhang,
                    Half = a.Half, Ghost = a.Ghost, Class = a.Class, Lane = new LaneId(a.Lane), S = a.S, Length = a.Length,
                    SpeedMps = a.V, Committed = a.Committed ? new LaneId(a.CommitLane) : LaneId.None,
                };
            }
            return n;
        }
    }

    /// <summary>An agent's body rectangle (tests and debug drawing).</summary>
    public struct BodyBox
    {
        public int AgentId;
        public double X, Z;
        public float HeadingRad, Back, Ahead, Half;
        public bool Ghost;
        public VehicleClass Class;
        public LaneId Lane, Committed;
        public float S, Length, SpeedMps;
    }
}
