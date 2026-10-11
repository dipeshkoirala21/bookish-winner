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

        /// <summary>
        /// Out of view, ambient vehicles farther than this from the player leave (W2 playtest #6: "there are absolutely no
        /// traffics on the road"): the moving-vehicle cap stays around the player instead of thinning out over the whole
        /// ghost radius. Routed buses, a hailed taxi and a vehicle pulling over keep the ghost radius. 0 = 1.2 × the ghost
        /// radius (the old behaviour).
        /// </summary>
        public float KeepRadiusM = 260f;

        /// <summary>
        /// Moving motorbikes and scooters kept near the player (playtest #3: "on busy areas, instead of parked
        /// motorcycles, bring up the actual person driving it instead. But not too many: 2-3"): when fewer two-wheelers than
        /// this ride within <see cref="NearRiderRadiusM"/>, one is started on a lane at least <see cref="NearRiderMinM"/>
        /// away, out of view, every spawn interval (within the cap).
        /// </summary>
        public int NearRiders = 3;

        public float NearRiderRadiusM = 70f, NearRiderMinM = 35f;

        /// <summary>Weekly holiday: density × 0.6.</summary>
        public bool Saturday;

        /// <summary>Horns allowed at all (audio may switch them off).</summary>
        public bool Horns = true;

        public static TrafficSettings Low()
        {
            return new TrafficSettings { MaxVehicles = 12, FullRadiusM = 150f, GhostRadiusM = 300f, KeepRadiusM = 180f, NearRiders = 2 };
        }

        public static TrafficSettings Mid()
        {
            return new TrafficSettings { MaxVehicles = 30, FullRadiusM = 250f, GhostRadiusM = 500f, KeepRadiusM = 230f, NearRiders = 3 };
        }

        public static TrafficSettings High()
        {
            return new TrafficSettings { MaxVehicles = 50, FullRadiusM = 400f, GhostRadiusM = 800f, KeepRadiusM = 280f, NearRiders = 3 };
        }
    }

    /// <summary>What a pull-over command asks of one agent (<see cref="TrafficSim.PullOver"/>). Append-only.</summary>
    public enum PullOverKind : byte
    {
        None = 0,

        /// <summary>Pick up a person waiting at (x, z) (a hailed taxi): the agent finds its way there through the lane
        /// graph and stops at the kerb with its passenger door beside them.</summary>
        Pickup = 1,

        /// <summary>Pull over at the kerb as soon as it comfortably can, about 14 m on (a taxi passenger's bell).</summary>
        Ahead = 2,

        /// <summary>Stand where it is (someone is climbing in or out).</summary>
        Hold = 3,

        /// <summary>A passenger rang the bell: a route vehicle stops at its next real stop when one lies within
        /// <see cref="TrafficSim.NextStopReachM"/>, else it pulls over ahead with its doors open.</summary>
        NextStop = 4,
    }

    /// <summary>Where a pull-over stands (<see cref="TrafficSim.PullOverStateOf"/>).</summary>
    public enum PullOverState : byte
    {
        None = 0,

        /// <summary>On its way to the place it will stop.</summary>
        Approaching = 1,

        /// <summary>Standing at the place (doors open for a route vehicle).</summary>
        Holding = 2,

        /// <summary>No way to that place was found within <see cref="TrafficSim.PickupSearchM"/> (tried again every few
        /// seconds); a hail should pick another taxi.</summary>
        Unreachable = 3,
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
    /// <para><b>Pulling over</b> (the player's rides): <see cref="PullOver"/> gives one agent a fixed place to stop at the
    /// kerb (a hailed taxi finds its way to the person waiting; a bell stops it about 14 m on; a boarding or alighting
    /// passenger holds it), resolved once to a lane position and kept until <see cref="CancelPullOver"/>, so the agent
    /// really stops instead of creeping after a target that moves with it. Route buses, micros and tempos dwell at every
    /// real stop of their route with their doors open, pull over for a person waiting at their kerb
    /// (<see cref="SetWaitingPassenger"/>), and micros, minibuses and tempos also stop at random about every 600 m.</para>
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

            // Pulling over: a command from the player's session (external) or the sim's own kerb pickups and random stops.
            public PullOverKind Pull;
            public PullOverState PullState;
            public bool PullExternal, PullHeld, PullAtStop;
            public double PullX, PullZ; // the waiting person (Pickup)
            public int PullLane; // resolved target: the front stops at PullS on PullLane (-1 unresolved)
            public float PullS, PullT, PullRetry, PullHoldS;
            public int[] PullPath; // lanes after the current one leading to PullLane (a hailed taxi's way), or null
            public float SinceServed; // seconds since this agent last stopped for a waiting passenger
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

        /// <summary>A bus-route plan: the lanes to follow and the stops along them (in path order).</summary>
        internal sealed class RoutePlan
        {
            public int[] Lanes;

            /// <summary>Arc length of each lane's start along the plan (same length as <see cref="Lanes"/>).</summary>
            public float[] LaneStart;

            public int[] StopLane;
            public float[] StopS;
            public int[] StopIndex;

            /// <summary>Where the front stands at each stop, along the plan (ascending).</summary>
            public float[] StopAlong;

            public int RouteIndex;
        }

        /// <summary>A pull-over command waiting for the next step.</summary>
        private struct PullCommand
        {
            public int AgentId;
            public PullOverKind Kind;
            public double X, Z;
        }

        /// <summary>One agent's pull-over in the published snapshot.</summary>
        private struct PullStatus
        {
            public int AgentId;
            public PullOverState State;
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
        private readonly List<PullCommand> _pendPull = new List<PullCommand>(8), _pullWork = new List<PullCommand>(8);
        private bool _pendWaiting, _waiting, _waitBusy;
        private double _pendWaitX, _pendWaitZ, _waitX, _waitZ;

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
        private PullStatus[] _pullBack = new PullStatus[8], _pullFront = new PullStatus[8];
        private int _pullBackCount, _pullFrontCount;

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

        /// <summary>
        /// Asks agent <paramref name="agentId"/> to pull over (applied at the next step; any thread). The place is
        /// resolved once to a lane position and kept, so the agent stops there instead of creeping after a moving target:
        /// <see cref="PullOverKind.Pickup"/> stops with the passenger door beside the person waiting at (x, z), finding a
        /// way there through the lane graph (within <see cref="PickupSearchM"/>); <see cref="PullOverKind.Ahead"/> about
        /// <see cref="AheadM"/> on; <see cref="PullOverKind.Hold"/> where it stands; <see cref="PullOverKind.NextStop"/> at a
        /// route vehicle's next real stop, else ahead with its doors open. Posting the same command again changes nothing
        /// (a session may post it every frame); a different one replaces it. It holds until <see cref="CancelPullOver"/>
        /// (or <see cref="CommandHoldMaxS"/> standing). (x, z) matter only for a pickup.
        /// </summary>
        public void PullOver(int agentId, PullOverKind kind, double x, double z)
        {
            if (kind == PullOverKind.None)
            {
                CancelPullOver(agentId);
                return;
            }
            lock (_inputLock) _pendPull.Add(new PullCommand { AgentId = agentId, Kind = kind, X = x, Z = z });
        }

        /// <summary>Lets agent <paramref name="agentId"/> go on its way (applied at the next step; any thread).</summary>
        public void CancelPullOver(int agentId)
        {
            lock (_inputLock) _pendPull.Add(new PullCommand { AgentId = agentId, Kind = PullOverKind.None });
        }

        /// <summary>
        /// A person (the player on foot) is waiting at (x, z): the next bus, minibus, micro or tempo coming along a kerb
        /// lane within <see cref="KerbReachM"/> of its edge pulls over with its door beside them and dwells with its doors
        /// open (one at a time; each vehicle once per <see cref="ServeCooldownS"/>). Any thread; applied at the next step.
        /// </summary>
        public void SetWaitingPassenger(bool waiting, double x, double z)
        {
            lock (_inputLock)
            {
                _pendWaiting = waiting;
                _pendWaitX = x;
                _pendWaitZ = z;
            }
        }

        /// <summary>Where agent <paramref name="agentId"/>'s pull-over stands after the last step (thread-safe).</summary>
        public PullOverState PullOverStateOf(int agentId)
        {
            lock (_snapLock)
            {
                for (int i = 0; i < _pullFrontCount; i++)
                    if (_pullFront[i].AgentId == agentId)
                        return _pullFront[i].State;
            }
            return PullOverState.None;
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
                _pullWork.Clear();
                _pullWork.AddRange(_pendPull);
                _pendPull.Clear();
                _waiting = _pendWaiting;
                _waitX = _pendWaitX;
                _waitZ = _pendWaitZ;
                if (_pendTaxiReq != _taxiReq || _pendTaxiReq && ((_pendTaxiX - _taxiX) * (_pendTaxiX - _taxiX) + (_pendTaxiZ - _taxiZ) * (_pendTaxiZ - _taxiZ) > 4.0))
                {
                    // A new wave (or a new place): answer it afresh.
                    _taxiDone = false;
                    _taxiX = _pendTaxiX;
                    _taxiZ = _pendTaxiZ;
                }
                _taxiReq = _pendTaxiReq;
            }
            for (int k = 0; k < _pullWork.Count; k++) ApplyPull(_pullWork[k]);
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

        /// <summary>Live agents now (full and ghost), without the publish step.</summary>
        private int AliveNow()
        {
            int n = 0;
            for (int i = 0; i < _agents.Length; i++)
                if (_agents[i].Alive)
                    n++;
            return n;
        }

        /// <summary>
        /// Departures the bus runner wants on the road around the player but has not placed yet (set by
        /// <see cref="BusRouteRunner"/> every step). The ambient spawner leaves this many slots of the moving-vehicle
        /// cap free, so routed buses fit under <see cref="TrafficSettings.MaxVehicles"/> instead of on top of it.
        /// </summary>
        internal int RoutedPending;

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
                PullLane = -1, SinceServed = ServeCooldownS,
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
        /// <remarks>Refused when the moving-vehicle cap is full: routed vehicles share <see cref="TrafficSettings.MaxVehicles"/>
        /// with the ambient traffic (which leaves <see cref="RoutedPending"/> slots for them).</remarks>
        internal bool SpawnRouted(int lane, float s, int variant, byte livery, int route, int routePos, out int agentId)
        {
            agentId = 0;
            if (AliveNow() >= _s.MaxVehicles) return false;
            if (!SpawnOnLane(lane, s, variant, livery, out agentId, false)) return false;
            int i = IndexOf(agentId);
            _agents[i].Route = route;
            _agents[i].RouteId = route >= 0 && route < Routes.Count ? Routes[route].RouteIndex : -1;
            _agents[i].RoutePos = routePos;
            _agents[i].NextStop = -1; // micros, minibuses and tempos keep their random stops between the real ones
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

        /// <summary>
        /// No agent body within the stretch [s − len − 6, s + 6] of a lane, measured on along the lane's successors and
        /// back along its predecessors too: a vehicle that just moved onto the next connector (or is about to leave the
        /// previous one) still blocks a spawn right behind (or in front of) it.
        /// </summary>
        private bool Room(int laneId, float s, float len)
        {
            if (!RoomOn(laneId, s, len)) return false;
            LaneGraph.Lane l = _g.Lanes[laneId];
            int[] next = l.Next, prev = l.Prev;
            for (int k = 0; k < next.Length; k++)
            {
                int n = next[k];
                if (n < 0 || n >= _g.Lanes.Count || !_g.Lanes[n].Alive) continue;
                if (!RoomOn(n, s - l.Length, len)) return false; // s in the successor's arc length (negative: before it)
            }
            for (int k = 0; k < prev.Length; k++)
            {
                int p = prev[k];
                if (p < 0 || p >= _g.Lanes.Count || !_g.Lanes[p].Alive) continue;
                if (!RoomOn(p, s + _g.Lanes[p].Length, len)) return false; // s in the predecessor's arc length
            }
            return true;
        }

        /// <summary><see cref="Room"/> on one lane, with s in that lane's arc length (may lie before or past it).</summary>
        private bool RoomOn(int laneId, float s, float len)
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
            if (target > _s.MaxVehicles) target = _s.MaxVehicles;
            // Leave the slots of the departures the bus runner is still waiting to place (they spawn under the cap).
            int pending = RoutedPending > 0 ? Math.Min(RoutedPending, target) : 0;
            target -= pending;
            // A full cap would starve the timetable: make room for a due bus by letting the farthest ambient lane ghost go
            // (out of view, beyond the full simulation radius), one per spawn interval.
            if (pending > 0 && alive >= _s.MaxVehicles) RetireFarthestGhost();
            if (alive >= target)
            {
                SpawnNearRider(hour, alive, target);
                return;
            }
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
            SpawnNearRider(hour, alive, target);
        }

        /// <summary>
        /// Keeps <see cref="TrafficSettings.NearRiders"/> moving two-wheelers round the player (playtest #3): counts the
        /// motorbikes and scooters (not ghosts) within <see cref="TrafficSettings.NearRiderRadiusM"/> and, when short, starts
        /// one on a lane between <see cref="TrafficSettings.NearRiderMinM"/> and that radius, out of view, on a lane two
        /// wheelers may use. Within the cap: when the cap is full the farthest ambient ghost out of view makes room.
        /// </summary>
        private void SpawnNearRider(float hour, int alive, int target)
        {
            if (_s.NearRiders <= 0 || target <= 0) return;
            double r2 = _s.NearRiderRadiusM * (double)_s.NearRiderRadiusM;
            int riders = 0;
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive || _agents[i].Ghost || _agents[i].Class != VehicleClass.TwoWheeler) continue;
                double dx = _agents[i].X - _focusX, dz = _agents[i].Z - _focusZ;
                if (dx * dx + dz * dz <= r2) riders++;
            }
            if (riders >= _s.NearRiders) return;
            if (alive >= _s.MaxVehicles)
            {
                RetireFarthestGhost();
                return; // the slot frees for the next interval
            }
            uint bit = VehicleClasses.Bit(VehicleClass.TwoWheeler);
            for (int attempt = 0; attempt < _s.SpawnAttempts; attempt++)
            {
                double ang = _rng.NextDouble() * 2 * Math.PI;
                double rad = _s.NearRiderMinM + (_s.NearRiderRadiusM - _s.NearRiderMinM) * _rng.NextDouble();
                double x = _focusX + rad * Math.Cos(ang), z = _focusZ + rad * Math.Sin(ang);
                if (InView(x, z)) continue;
                _near.Clear();
                _g.LanesNear(x, z, 15.0, _near);
                if (_near.Count == 0) continue;
                _near.Sort();
                int pick = _near[_rng.Next(_near.Count)];
                LaneGraph.Lane l = _g.Lanes[pick];
                if (l.Kind != LaneKind.Road || l.Length < 12f || (l.Mask & bit) == 0) continue;
                float s;
                double d = LaneGraph.Project(l, x, z, out s);
                if (d > 18.0) continue;
                // A rider of the street: the two-wheeler column of the lane's mix (scooter, commuter bike, cruiser, e-scooter).
                int variant = TrafficTables.PickVariant(ref _rng, l.Class, l.Area, hour, bit);
                if (variant < 0) continue;
                int id;
                byte livery = VehicleCatalog.At(variant).PickLivery(_rng.NextU32());
                if (SpawnOnLane(pick, s, variant, livery, out id, true)) return;
            }
        }

        /// <summary>Removes the farthest ambient ghost (not routed, not pulling over, out of view), if any.</summary>
        private void RetireFarthestGhost()
        {
            int best = -1;
            double bestD = 0;
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive || !_agents[i].Ghost || _agents[i].Route >= 0 || _agents[i].Pull != PullOverKind.None) continue;
                ref Agent a = ref _agents[i];
                double dx = a.X - _focusX, dz = a.Z - _focusZ, d = dx * dx + dz * dz;
                if (d <= bestD || InView(a.X, a.Z)) continue;
                bestD = d;
                best = i;
            }
            if (best < 0) return;
            ref Agent g = ref _agents[best];
            if (g.Committed && g.CommitLane >= 0 && _resv[g.CommitLane] > 0) _resv[g.CommitLane]--;
            if (g.TailLane >= 0 && _resv[g.TailLane] > 0) _resv[g.TailLane]--;
            Kill(best);
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
                _waitBusy = WaitingServed();
                ServeTaxiRequest();
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
                if (a.Pull != PullOverKind.None && a.PullLane >= 0 && !_g.IsAlive(new LaneId(a.PullLane)))
                {
                    // The place streamed out: find it again (or the next one) from where the agent is.
                    a.PullLane = -1;
                    a.PullPath = null;
                    a.PullHeld = false;
                    a.PullRetry = 0f;
                }
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
                // The tail frees the connector once less than TailFreeM of it is left in the connector's mouth: a scooter
                // waiting on a 3 m stub between two chowks (Bakhundol) otherwise holds the connector behind it for ever
                // and the two junctions lock each other. The geometric corridor check still sees the body.
                if (a.TailLane >= 0 && (a.PrevLane != a.TailLane || a.S >= a.Length - TailFreeM || !_g.IsAlive(new LaneId(a.TailLane)))) a.TailLane = -1;
                if (a.TailLane >= 0) _resv[a.TailLane]++;
            }
        }

        /// <summary>A body whose tail reaches less than this into the junction connector it left no longer reserves it.</summary>
        private const float TailFreeM = 1.0f;

        /// <summary>Signal cycle (W2_DESIGN 5.1: 90 s), shared by the phases with 5 s of amber and clear each, every phase
        /// between <see cref="SignalPhaseMinS"/> and <see cref="SignalPhaseMaxS"/>.</summary>
        private const float SignalCycleS = 90f, SignalPhaseMinS = 20f, SignalPhaseMaxS = 30f;

        private static float SignalPhaseS(int phases)
        {
            return Math.Max(SignalPhaseMinS, Math.Min(SignalPhaseMaxS, SignalCycleS / Math.Max(1, phases)));
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
                    st.PhaseLen = c.Kind == ControllerKind.Signal ? SignalPhaseS(c.Phases) : 30f;
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
                        st.PhaseLen = SignalPhaseS(c.Phases); // 90 s cycle: 25 s green + 5 s amber and clear at three phases
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
            // Spawns outside Step (bus runner, garage summon, hail) can reach here right after new tiles added lanes.
            EnsureLaneArrays();
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
            EnsureLaneArrays();
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
            if (a.PullPath != null)
            {
                // A hailed taxi follows the way it found to the person waiting.
                int[] path = a.PullPath;
                int at = Array.IndexOf(path, from), want = at >= 0 ? (at + 1 < path.Length ? path[at + 1] : -1) : from == a.Lane ? path[0] : -1;
                if (want >= 0 && Array.IndexOf(l.Next, want) >= 0 && _g.IsAlive(new LaneId(want))) return want;
            }
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
                        // Ask within braking distance plus the standing gap: IDM halts s0 short of the line, and a bus or a
                        // lorry (s0 3 m) standing there must still be near enough to ask, or it waits through every green.
                        float brake = a.V * a.V / (2f * p.B) + Math.Max(3f, p.S0 + StopLineM + 1f);
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
                        // IDM halts s0 short of a standing leader: behind a lane shorter than that (a stub between two
                        // junctions) the front would never reach the lane where permission is asked, so the virtual
                        // leader stands s0 beyond the line there.
                        float line = dist - StopLineM;
                        if (step == 1 && cl.Length < p.S0 + 1.5f) line += p.S0;
                        Lead(ref gap, ref lv, line, 0f, ref a, false);
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

            // 6. Pulling over: a hailed taxi, a passenger's bell, someone boarding, a person waiting at the kerb, a
            // micro's random stop.
            PullOverStep(idx, ref a, l, ref gap, ref lv, h);

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
                // Inside a junction on our reservation, a body waiting at another arm's stop line (not on our way) only
                // goes once we are through: waiting for its nose as well would hold both for ever.
                if (mine.Kind == LaneKind.Connector && b.AtStopLine && !b.Committed && b.V < 0.5f && b.Lane != a.Lane && b.Lane != a.P0 &&
                    b.Lane != a.P1 && b.Lane != a.P2 && b.Lane != a.P3)
                    continue;
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
            _blockedByPlayer = false;
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
                            _blockedByPlayer = o.Kind == ObstacleKind.Player;
                        }
                        if (animal) a.Anim |= (byte)AgentAnim.Yielding;
                    }
                }
            }
            if (want) a.LatTarget = avoid;
            _avoiding = want;
        }

        private bool _avoiding;

        /// <summary>This step's agent stands behind a player obstacle (someone in the road, maybe the one it stops for).</summary>
        private bool _blockedByPlayer;

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

        /// <summary>
        /// Route stops: a routed agent brakes for the next real stop of its plan (measured along the plan, so a stop
        /// several lanes ahead is seen in time) and dwells 8–15 s there with its doors open (<see cref="AgentAnim.AtStop"/>).
        /// </summary>
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
            float d;
            RoutePlan rp;
            if (!NextRouteStop(ref a, out rp, out d) || d >= 60f) return;
            if (d < 1.0f && a.V < 0.6f)
            {
                var r = new SimRng(a.Rng);
                a.DwellT = r.Range(8f, 15f);
                a.Rng = r.NextU64();
                a.Anim |= (byte)AgentAnim.AtStop;
                gap = Math.Min(gap, 0.1f);
                lv = 0f;
                int stopIdx = rp.StopIndex[a.NextStop];
                if (StopReached != null) _stopEvents.Add(new KeyValuePair<int, int>(a.Id, rp.RouteIndex * 65536 + stopIdx));
                a.NextStop++;
                if (a.NextStop >= rp.StopAlong.Length) a.NextStop = int.MaxValue;
                return;
            }
            // The stop is a standing leader the bus should reach: IDM keeps its minimum gap s0 to any leader, so the
            // virtual one stands s0 beyond the stop, or the bus would halt short of it.
            float stopGap = Math.Max(0f, d) + a.Idm.S0;
            if (stopGap < gap)
            {
                gap = stopGap;
                lv = 0f;
            }
        }

        /// <summary>
        /// The next real stop of a routed agent's plan and how far its front is from it along the plan (negative within a
        /// metre past it). Stops already passed by more than a metre are skipped. False when the agent is not routed or
        /// no stop is left on its plan.
        /// </summary>
        private bool NextRouteStop(ref Agent a, out RoutePlan rp, out float d)
        {
            rp = null;
            d = float.PositiveInfinity;
            if (a.Route < 0 || a.Route >= Routes.Count) return false;
            rp = Routes[a.Route];
            if (rp.StopAlong == null || rp.StopAlong.Length == 0) return false;
            int pos = a.RoutePos;
            if (pos < 0 || pos >= rp.Lanes.Length || rp.Lanes[pos] != a.Lane)
            {
                pos = Array.IndexOf(rp.Lanes, a.Lane);
                if (pos < 0) return false;
                a.RoutePos = pos;
            }
            float along = rp.LaneStart[pos] + a.S;
            if (a.NextStop < 0) a.NextStop = FirstStopFrom(rp, along);
            while (a.NextStop < rp.StopAlong.Length && rp.StopAlong[a.NextStop] < along - 1f) a.NextStop++;
            if (a.NextStop >= rp.StopAlong.Length)
            {
                a.NextStop = int.MaxValue;
                return false;
            }
            d = rp.StopAlong[a.NextStop] - along;
            return true;
        }

        private readonly List<KeyValuePair<int, int>> _stopEvents = new List<KeyValuePair<int, int>>();

        private static int FirstStopFrom(RoutePlan rp, float along)
        {
            for (int k = 0; k < rp.StopAlong.Length; k++)
                if (rp.StopAlong[k] >= along - 1f) return k;
            return int.MaxValue;
        }

        // ---- pulling over (W2_DESIGN 6.2: hailing, the bell, boarding; route vehicles at the kerb) ----

        /// <summary>The resolved place of an agent's pull-over (tests and debug drawing): lane and the front's arc length,
        /// and how many lanes its way there still has (0 when the place is on its lane or plan). False when the agent has
        /// no pull-over or its place is not resolved yet.</summary>
        public bool TryGetPullOverPlace(int agentId, out LaneId lane, out float s, out int wayLanes)
        {
            lane = LaneId.None;
            s = 0f;
            wayLanes = 0;
            lock (_g.SyncRoot)
            {
                int i = IndexOf(agentId);
                if (i < 0 || _agents[i].Pull == PullOverKind.None || _agents[i].PullLane < 0) return false;
                lane = new LaneId(_agents[i].PullLane);
                s = _agents[i].PullS;
                wayLanes = _agents[i].PullPath != null ? _agents[i].PullPath.Length : 0;
                return true;
            }
        }

        /// <summary>A bell stops a taxi this far on at least.</summary>
        public const float AheadM = 14f;

        /// <summary>A bell on a route vehicle waits for its next real stop when it lies within this distance along the
        /// route; farther, the vehicle pulls over ahead.</summary>
        public const float NextStopReachM = 600f;

        /// <summary>A hailed taxi looks this far through the lane graph for a way to the person waiting.</summary>
        public const float PickupSearchM = 1500f;

        /// <summary>The person waiting may stand this far beyond a lane's edge (on the footpath or the verge).</summary>
        public const float KerbReachM = 6f;

        /// <summary>A vehicle that stopped for a waiting passenger does not stop for one again within this time.</summary>
        public const float ServeCooldownS = 90f;

        /// <summary>A commanded stop is let go after standing this long without a cancel (a lost session).</summary>
        public const float CommandHoldMaxS = 120f;

        /// <summary>A rider who rang the bell gets off wherever the vehicle has stood this long (a jam, a long red).</summary>
        public const float BellJamS = 4f;

        /// <summary>Held up this close to its place (a queue, someone in the road), a vehicle pulling over stops there.</summary>
        public const float ShortStopM = 15f;

        /// <summary>Comfortable braking for pulling over (m/s²).</summary>
        private const float PullBrake = 2.5f;

        // Distance from the front to the left (kerb-side) passenger door, per catalogue variant (lazily filled).
        private static float[] _doorAhead;

        /// <summary>How far behind its front a vehicle's kerb-side passenger door is (the rear step of a tempo lies just
        /// behind it), so a stop puts that door beside the person waiting.</summary>
        internal static float DoorAhead(int variant)
        {
            float[] t = _doorAhead;
            if (t == null)
            {
                t = new float[VehicleCatalog.Count];
                for (int v = 0; v < t.Length; v++)
                {
                    VehicleCatalogEntry e = VehicleCatalog.At(v);
                    float front = e.LengthM - e.RearOverhangM; // rear axle to front
                    float door = 0.5f * e.LengthM;
                    SeatSocket[] seats = VehicleSeats.For(v);
                    for (int k = 0; k < seats.Length; k++)
                    {
                        if (seats[k].Kind == SeatKind.Driver || seats[k].Kind == SeatKind.Standing) continue;
                        if (seats[k].Side == EntrySide.Right) continue;
                        door = front - seats[k].EntryZ;
                        break;
                    }
                    t[v] = Math.Max(0.5f, door);
                }
                _doorAhead = t;
            }
            return variant >= 0 && variant < t.Length ? t[variant] : 2f;
        }

        /// <summary>The public-service variants that pick people up at the kerb: the city bus, the minibus, the micro and
        /// the tempo (not coaches or school buses).</summary>
        internal static bool PicksUpAtKerb(int variant)
        {
            return variant == VehicleCatalog.BusCityGreen || variant == VehicleCatalog.Minibus || variant == VehicleCatalog.Microbus ||
                   variant == VehicleCatalog.Tempo;
        }

        private static float StopNeed(float v, float brake)
        {
            return v * v / (2f * brake) + 1f;
        }

        private void ApplyPull(in PullCommand c)
        {
            int i = IndexOf(c.AgentId);
            if (i < 0) return;
            ref Agent a = ref _agents[i];
            if (c.Kind == PullOverKind.None)
            {
                if (a.PullExternal) ReleasePull(ref a);
                return;
            }
            if (a.PullExternal && a.Pull == c.Kind)
            {
                // The same command again: keep the place it was given (a session posts every frame).
                if (c.Kind != PullOverKind.Pickup) return;
                double dx = c.X - a.PullX, dz = c.Z - a.PullZ;
                if (dx * dx + dz * dz < 1.0) return;
            }
            bool doors = (a.Anim & (byte)AgentAnim.AtStop) != 0 || a.DwellT > 0f || a.PullHeld && a.PullAtStop;
            ReleasePull(ref a);
            a.Pull = c.Kind;
            a.PullExternal = true;
            a.PullX = c.X;
            a.PullZ = c.Z;
            a.PullState = PullOverState.Approaching;
            // A route vehicle asked to stop for its passenger opens its doors; one held for boarding keeps them open.
            a.PullAtStop = c.Kind == PullOverKind.NextStop || c.Kind == PullOverKind.Hold && doors;
        }

        private static void ReleasePull(ref Agent a)
        {
            a.Pull = PullOverKind.None;
            a.PullState = PullOverState.None;
            a.PullExternal = false;
            a.PullHeld = false;
            a.PullAtStop = false;
            a.PullLane = -1;
            a.PullPath = null;
            a.PullT = 0f;
            a.PullRetry = 0f;
            a.PullHoldS = 0f;
        }

        /// <summary>True when some agent is already pulling over for the waiting passenger (one at a time).</summary>
        private bool WaitingServed()
        {
            if (!_waiting) return false;
            for (int i = 0; i < _agents.Length; i++)
                if (_agents[i].Alive && _agents[i].Pull == PullOverKind.Pickup && !_agents[i].PullExternal)
                    return true;
            return false;
        }

        private void PullOverStep(int idx, ref Agent a, LaneGraph.Lane l, ref float gap, ref float lv, float h)
        {
            if (a.Pull == PullOverKind.None)
            {
                if (a.SinceServed < ServeCooldownS) a.SinceServed += h;
                if (a.Ghost || a.DwellT > 0f) return;
                if (_waiting && !_waitBusy && a.SinceServed >= ServeCooldownS && PicksUpAtKerb(a.Variant)) KerbPickup(ref a, l);
                if (a.Pull == PullOverKind.None && a.RandomStopAt > 0f && a.Odo >= a.RandomStopAt) RandomStop(ref a, l);
                if (a.Pull == PullOverKind.None) return;
            }
            a.PullT += h;
            if (a.PullHeld)
            {
                HoldStill(ref a, l, ref gap, ref lv);
                if (a.PullT >= (a.PullExternal ? CommandHoldMaxS : a.PullHoldS)) ReleasePull(ref a);
                return;
            }
            float d;
            RoutePlan rp;
            if (a.Pull == PullOverKind.NextStop && a.DwellT <= 0f && NextRouteStop(ref a, out rp, out d) && d <= NextStopReachM)
            {
                // The bell on a route vehicle: its next real stop is close enough; the stop's dwell lets the rider off
                // (or, stuck in a jam on the way, the doors open where it stands).
                a.PullState = PullOverState.Approaching;
                if (a.V < 0.3f && a.WaitT > BellJamS)
                {
                    a.PullHeld = true;
                    a.PullT = 0f;
                    a.PullState = PullOverState.Holding;
                    a.PullLane = a.Lane;
                    a.PullS = a.S;
                    HoldStill(ref a, l, ref gap, ref lv);
                }
                return;
            }
            if (a.PullLane < 0)
            {
                a.PullRetry -= h;
                if (a.PullRetry > 0f) return;
                bool ok = a.Pull == PullOverKind.Pickup && a.PullExternal ? ResolvePickup(ref a)
                    : ResolveAhead(ref a, a.Pull == PullOverKind.Hold ? 0f : a.Pull == PullOverKind.Ahead || a.Pull == PullOverKind.NextStop ? AheadM : 0f,
                                   a.Pull == PullOverKind.Hold);
                if (!ok)
                {
                    a.PullState = PullOverState.Unreachable;
                    a.PullRetry = 3f;
                    if (!a.PullExternal) ReleasePull(ref a);
                    return;
                }
                a.PullState = PullOverState.Approaching;
            }
            d = DistanceAlong(ref a, a.PullLane, a.PullS);
            if (float.IsPositiveInfinity(d))
            {
                // Not on the next few lanes yet: fine while the agent follows the way it found; else find it again.
                bool onWay = a.PullPath != null && (Array.IndexOf(a.PullPath, a.Lane) >= 0 || a.P0 == a.PullPath[0]);
                if (!onWay)
                {
                    a.PullLane = -1;
                    a.PullPath = null;
                }
                return;
            }
            if (d < -1.5f)
            {
                // Rolled past it (it came too late to stop): the next place on.
                a.PullLane = -1;
                a.PullPath = null;
                return;
            }
            // Arrived: the front at the place; or, held up short of it (the person waiting stands in the road, a queue
            // in front), standing still a moment within a few metres of it: the driver stops where he can. A passenger
            // who rang the bell is let off wherever the vehicle has stood a while (a jam, a long red light).
            bool bell = a.PullExternal && (a.Pull == PullOverKind.Ahead || a.Pull == PullOverKind.NextStop);
            if (d < 1.0f && a.V < 0.6f || d < 8f && a.V < 0.3f && _blockedByPlayer || d < ShortStopM && a.V < 0.3f && a.WaitT > 1.5f ||
                bell && a.V < 0.3f && a.WaitT > BellJamS)
            {
                a.PullHeld = true;
                a.PullT = 0f;
                a.PullState = PullOverState.Holding;
                a.PullPath = null;
                HoldStill(ref a, l, ref gap, ref lv);
                return;
            }
            float stopGap = Math.Max(0f, d) + a.Idm.S0;
            if (stopGap < gap)
            {
                gap = stopGap;
                lv = 0f;
            }
            if (d < 30f && a.Lane == a.PullLane) KerbSide(ref a, l);
        }

        private void HoldStill(ref Agent a, LaneGraph.Lane l, ref float gap, ref float lv)
        {
            gap = Math.Min(gap, 0.1f);
            lv = 0f;
            if (a.PullAtStop) a.Anim |= (byte)AgentAnim.AtStop;
            if (a.Lane == a.PullLane) KerbSide(ref a, l);
        }

        /// <summary>Drifts to the left (kerb) edge of a kerb lane (left-hand traffic).</summary>
        private static void KerbSide(ref Agent a, LaneGraph.Lane l)
        {
            if (l.Kind != LaneKind.Road || l.Index != 0) return;
            a.LatTarget = -Math.Max(0f, 0.5f * l.Width - a.Half - 0.15f);
        }

        /// <summary>Distance from the agent's front to arc length <paramref name="s"/> of <paramref name="lane"/> along
        /// its current lane and planned lanes; +∞ when the lane is not among them.</summary>
        private float DistanceAlong(ref Agent a, int lane, float s)
        {
            if (lane == a.Lane) return s - a.S;
            float dist = _g.Lanes[a.Lane].Length - a.S;
            for (int k = 0; k < PathLen; k++)
            {
                int p = k == 0 ? a.P0 : k == 1 ? a.P1 : k == 2 ? a.P2 : a.P3;
                if (p < 0) break;
                if (p == lane) return dist + s;
                dist += _g.Lanes[p].Length;
            }
            return float.PositiveInfinity;
        }

        /// <summary>
        /// A stop at least <paramref name="minAhead"/> on (and far enough to brake comfortably) along the agent's lane
        /// and plan, on a road lane with the whole body past the junction behind; never on a junction connector or a
        /// roundabout ring. <paramref name="here"/> (a hold) accepts the lane the agent is on, whatever it is.
        /// </summary>
        private bool ResolveAhead(ref Agent a, float minAhead, bool here)
        {
            Plan(ref a);
            float s = a.S + Math.Max(minAhead, here ? a.V * a.V / (2f * PullBrake) : StopNeed(a.V, PullBrake));
            for (int k = -1; k < PathLen; k++)
            {
                int cur = k < 0 ? a.Lane : k == 0 ? a.P0 : k == 1 ? a.P1 : k == 2 ? a.P2 : a.P3;
                if (cur < 0) return false;
                LaneGraph.Lane cl = _g.Lanes[cur];
                bool stoppable = here && k < 0 || cl.Kind == LaneKind.Road && !cl.Ring;
                if (stoppable)
                {
                    float lo = k < 0 ? 0f : Math.Min(a.Length + 0.5f, cl.Length - 1f);
                    float t = Math.Max(s, lo);
                    if (t <= cl.Length - (here && k < 0 ? 0f : 1f))
                    {
                        a.PullLane = cur;
                        a.PullS = t;
                        a.PullPath = null;
                        return true;
                    }
                }
                s = Math.Max(0f, s - cl.Length);
            }
            return false;
        }

        /// <summary>A bus, minibus, micro or tempo coming along a kerb lane towards the person waiting pulls over with
        /// its door beside them (within the next 70 m, when it can still stop comfortably).</summary>
        private void KerbPickup(ref Agent a, LaneGraph.Lane l)
        {
            uint bit = VehicleClasses.Bit(a.Class);
            float door = DoorAhead(a.Variant), need = StopNeed(a.V, PullBrake), start = -a.S;
            for (int k = -1; k < 2; k++)
            {
                int cur = k < 0 ? a.Lane : k == 0 ? a.P0 : a.P1;
                if (cur < 0 || start > 70f) return;
                LaneGraph.Lane cl = _g.Lanes[cur];
                if (cl.Kind == LaneKind.Road && cl.Index == 0 && (cl.Mask & bit) != 0)
                {
                    float sp;
                    double dist = LaneGraph.Project(cl, _waitX, _waitZ, out sp);
                    if (dist <= 0.5 * cl.Width + KerbReachM && Beside(cl, KerbLat(cl, sp, _waitX, _waitZ)))
                    {
                        float lo = k < 0 ? 0f : Math.Min(a.Length + 0.5f, cl.Length - 0.5f);
                        float st = Math.Max(lo, Math.Min(sp + door, cl.Length - 0.5f));
                        float d = start + st;
                        if (d >= need && d <= 70f && Math.Abs(st - (sp + door)) < 6f)
                        {
                            a.Pull = PullOverKind.Pickup;
                            a.PullExternal = false;
                            a.PullState = PullOverState.Approaching;
                            a.PullX = _waitX;
                            a.PullZ = _waitZ;
                            a.PullLane = cur;
                            a.PullS = st;
                            a.PullPath = null;
                            a.PullAtStop = true;
                            a.PullT = 0f;
                            var r = new SimRng(a.Rng);
                            a.PullHoldS = r.Range(10f, 16f);
                            a.Rng = r.NextU64();
                            a.SinceServed = 0f;
                            _waitBusy = true;
                            return;
                        }
                    }
                }
                start += cl.Length;
            }
        }

        /// <summary>True when someone <paramref name="lat"/> metres right of a lane's centre (negative: left) stands on its
        /// kerb side or on its own carriageway (in the road, between the lanes of its direction), not across the road.</summary>
        private static bool Beside(LaneGraph.Lane l, float lat)
        {
            return lat <= 1.0f || lat <= (Math.Max(1, (int)l.Count) - l.Index - 0.5f) * l.Width + 1.0f;
        }

        /// <summary>Signed offset of (x, z) from a lane at arc length <paramref name="s"/>, right of travel positive.</summary>
        private static float KerbLat(LaneGraph.Lane l, float s, double x, double z)
        {
            double px, pz;
            float py, hd;
            LaneGraph.PointAt(l, s, out px, out pz, out py, out hd);
            return (float)((x - px) * Math.Cos(hd) - (z - pz) * Math.Sin(hd));
        }

        /// <summary>Micros, minibuses and tempos also stop at random about every 600 m (a passenger gets on or off), at
        /// the kerb with their doors open, wherever the lane has room.</summary>
        private void RandomStop(ref Agent a, LaneGraph.Lane l)
        {
            if (l.Kind != LaneKind.Road || l.Ring) return;
            float st = a.S + StopNeed(a.V, PullBrake);
            if (st < a.Length || st > l.Length - 2f) return; // no room on this lane: try on the next
            var r = new SimRng(a.Rng);
            a.Pull = PullOverKind.Ahead;
            a.PullExternal = false;
            a.PullState = PullOverState.Approaching;
            a.PullLane = a.Lane;
            a.PullS = st;
            a.PullPath = null;
            a.PullAtStop = true;
            a.PullT = 0f;
            a.PullHoldS = r.Range(8f, 20f);
            a.RandomStopAt = a.Odo + r.Range(400f, 800f);
            a.Rng = r.NextU64();
        }

        /// <summary>A hailed taxi's place and way to the person waiting (<see cref="FindPickup"/>), applied to the agent.</summary>
        private bool ResolvePickup(ref Agent a)
        {
            int lane;
            float s, cost;
            int[] path;
            if (!FindPickup(ref a, a.PullX, a.PullZ, PickupSearchM, out lane, out s, out path, out cost)) return false;
            SetPickupPlace(ref a, lane, s, path);
            return true;
        }

        private void SetPickupPlace(ref Agent a, int lane, float s, int[] path)
        {
            a.PullLane = lane;
            a.PullS = s;
            a.PullPath = path;
            if (path == null) return;
            // Re-plan along the way (a held junction reservation stays: the way starts with that connector).
            if (!(a.Committed && a.CommitLane == path[0])) ClearPath(ref a);
            else a.P1 = a.P2 = a.P3 = -1;
            Plan(ref a);
        }

        /// <summary>
        /// The kerb lanes where a vehicle of class <paramref name="bit"/> could stop for a person at (x, z): road lanes
        /// within <see cref="KerbReachM"/> (plus a little) of their edge, with the stop place (the passenger door beside
        /// the person), the latest place it may slide to, and a penalty in metres of driving: the inner lanes of a wide
        /// road, each metre the person stands farther than 1.5 m from the lane's edge, and the far side of the road.
        /// Appends to the caller's lists (cleared first).
        /// </summary>
        private void PickupGoals(uint bit, float door, double x, double z, List<int> lanes, List<float> place, List<float> latest, List<float> penalty)
        {
            const float latePass = 12f; // the stop may slide this far on when the vehicle comes too fast for the exact place
            lanes.Clear();
            place.Clear();
            latest.Clear();
            penalty.Clear();
            _near.Clear();
            _g.LanesNear(x, z, KerbReachM + 8.0, _near);
            _near.Sort();
            foreach (int id in _near)
            {
                LaneGraph.Lane gl = _g.Lanes[id];
                if (gl.Kind != LaneKind.Road || (gl.Mask & bit) == 0) continue;
                float sp;
                double dist = LaneGraph.Project(gl, x, z, out sp);
                double beyond = dist - 0.5 * gl.Width;
                if (beyond > KerbReachM + 4.0) continue;
                lanes.Add(id);
                place.Add(Math.Max(0f, Math.Min(sp + door, gl.Length - 0.5f)));
                latest.Add(Math.Min(sp + door + latePass, gl.Length - 0.5f));
                penalty.Add(4f * gl.Index + 6f * (float)Math.Max(0.0, beyond - 1.5) + (Beside(gl, KerbLat(gl, sp, x, z)) ? 0f : 80f));
            }
        }

        private readonly List<int> _goalLane = new List<int>();
        private readonly List<float> _goalPlace = new List<float>(), _goalLatest = new List<float>(), _goalPen = new List<float>();

        /// <summary>
        /// Where agent <paramref name="a"/> should stop for a person at (x, z) and its way there: a shortest-path search
        /// over the lane graph from its front (through the junction connector it holds, if any), within
        /// <paramref name="searchM"/>, to the cheapest kerb place (<see cref="PickupGoals"/>) it can still stop at
        /// comfortably (a little on when it comes too fast). <paramref name="path"/> is null when the place is on its
        /// current lane; <paramref name="cost"/> is the driving distance plus the place's penalty. No state changes;
        /// allocates (once per hail).
        /// </summary>
        private bool FindPickup(ref Agent a, double x, double z, float searchM, out int lane, out float s, out int[] path, out float cost)
        {
            lane = -1;
            s = 0f;
            path = null;
            cost = float.PositiveInfinity;
            uint bit = VehicleClasses.Bit(a.Class);
            float need = StopNeed(a.V, PullBrake);
            PickupGoals(bit, DoorAhead(a.Variant), x, z, _goalLane, _goalPlace, _goalLatest, _goalPen);
            if (_goalLane.Count == 0) return false;
            int here = _goalLane.IndexOf(a.Lane);
            if (here >= 0)
            {
                // On the lane already: ahead of the front, or a little on when it comes too fast.
                float t = Math.Max(_goalPlace[here], a.S + need);
                if (t <= _goalLatest[here])
                {
                    cost = t - a.S + _goalPen[here];
                    lane = a.Lane;
                    s = t;
                }
            }
            // Shortest paths from the end of the current lane (through the committed connector when it holds one).
            var dist = new Dictionary<int, float>();
            var prev = new Dictionary<int, int>();
            var open = new List<KeyValuePair<float, int>>();
            LaneGraph.Lane cur = _g.Lanes[a.Lane];
            float rem = cur.Length - a.S;
            if (a.Committed && a.CommitLane >= 0 && _g.IsAlive(new LaneId(a.CommitLane)))
            {
                dist[a.CommitLane] = rem;
                prev[a.CommitLane] = -1;
                open.Add(new KeyValuePair<float, int>(rem, a.CommitLane));
            }
            else
            {
                foreach (int n in cur.Next)
                {
                    if (!Usable(n, bit) || dist.ContainsKey(n)) continue;
                    dist[n] = rem;
                    prev[n] = -1;
                    open.Add(new KeyValuePair<float, int>(rem, n));
                }
            }
            int best = -1;
            while (open.Count > 0)
            {
                int bi = 0;
                for (int k = 1; k < open.Count; k++)
                    if (open[k].Key < open[bi].Key || open[k].Key == open[bi].Key && open[k].Value < open[bi].Value)
                        bi = k;
                float c = open[bi].Key;
                int u = open[bi].Value;
                open.RemoveAt(bi);
                if (c > dist[u] || c >= cost || c > searchM) continue;
                LaneGraph.Lane ul = _g.Lanes[u];
                int gi = _goalLane.IndexOf(u);
                if (gi >= 0)
                {
                    // The whole body past the junction behind, and room to brake.
                    float t = Math.Max(_goalPlace[gi], Math.Max(need - c, Math.Min(a.Length + 0.5f, ul.Length - 1f)));
                    if (t <= _goalLatest[gi] && c + t + _goalPen[gi] < cost)
                    {
                        cost = c + t + _goalPen[gi];
                        lane = u;
                        s = t;
                        best = u;
                    }
                }
                float cn = c + ul.Length;
                foreach (int n in ul.Next)
                {
                    if (!Usable(n, bit)) continue;
                    float old;
                    if (dist.TryGetValue(n, out old) && old <= cn) continue;
                    dist[n] = cn;
                    prev[n] = u;
                    open.Add(new KeyValuePair<float, int>(cn, n));
                }
            }
            if (lane < 0) return false;
            if (best >= 0 && best == lane)
            {
                var way = new List<int>();
                for (int u = best; u >= 0; u = prev[u]) way.Add(u);
                way.Reverse();
                path = way.ToArray();
            }
            return true;
        }

        // ---- hailing a taxi (any taxi: the sim picks or sends one) ----

        /// <summary>A taxi this far (straight line) may answer a hail.</summary>
        public const float HailRadiusM = 150f;

        /// <summary>An existing taxi answers a hail only when its way to the person is at most this long; otherwise a taxi
        /// is sent from up the street, out of view.</summary>
        public const float HailWayMaxM = 200f;

        private bool _pendTaxiReq, _taxiReq, _taxiDone;
        private double _pendTaxiX, _pendTaxiZ, _taxiX, _taxiZ;
        private int _taxiFor, _taxiForFront;

        /// <summary>
        /// The player waves for a taxi at (x, z) (any thread; applied at the next step; posting it again for the same
        /// place changes nothing). The simulation answers once: the taxi within <see cref="HailRadiusM"/> with the
        /// shortest way to the kerb beside the person (at most <see cref="HailWayMaxM"/>, not one stuck in a jam), else a
        /// taxi sent from 60–150 m up the person's street, out of view; it gets a <see cref="PullOverKind.Pickup"/> for (x, z). Read the
        /// answer from <see cref="RequestedTaxi"/>.
        /// </summary>
        public void RequestTaxi(double x, double z)
        {
            lock (_inputLock)
            {
                _pendTaxiReq = true;
                _pendTaxiX = x;
                _pendTaxiZ = z;
            }
        }

        /// <summary>Withdraws the wave (the taxi keeps any pickup it was given until <see cref="CancelPullOver"/>).</summary>
        public void CancelTaxiRequest()
        {
            lock (_inputLock) _pendTaxiReq = false;
        }

        /// <summary>The answer to the current <see cref="RequestTaxi"/> after the last step (thread-safe): the agent id of
        /// the taxi on its way, 0 while undecided (or no request), −1 when no taxi can come (no taxi lane near the
        /// person).</summary>
        public int RequestedTaxi
        {
            get
            {
                lock (_snapLock) return _taxiForFront;
            }
        }

        /// <summary>Answers the taxi request once per place (caller holds the graph lock).</summary>
        private void ServeTaxiRequest()
        {
            if (!_taxiReq)
            {
                _taxiFor = 0;
                _taxiDone = false;
                return;
            }
            if (_taxiDone) return;
            _taxiDone = true;
            _taxiFor = -1;
            int bestIdx = -1, bestLane = -1;
            float bestCost = float.PositiveInfinity, bestS = 0f;
            int[] bestPath = null;
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive || _agents[i].Class != VehicleClass.Taxi || _agents[i].Ghost || _agents[i].Pull != PullOverKind.None) continue;
                if (_agents[i].WaitT > 5f) continue; // stuck in a jam: it would not come
                ref Agent a = ref _agents[i];
                double dx = a.X - _taxiX, dz = a.Z - _taxiZ;
                if (dx * dx + dz * dz > HailRadiusM * HailRadiusM) continue;
                int lane;
                float s, cost;
                int[] path;
                if (!FindPickup(ref a, _taxiX, _taxiZ, HailWayMaxM, out lane, out s, out path, out cost) || cost > HailWayMaxM || cost >= bestCost) continue;
                bestIdx = i;
                bestCost = cost;
                bestLane = lane;
                bestS = s;
                bestPath = path;
            }
            if (bestIdx < 0) bestIdx = SendTaxi(out bestLane, out bestS, out bestPath);
            if (bestIdx < 0) return;
            ref Agent t = ref _agents[bestIdx];
            ReleasePull(ref t);
            t.Pull = PullOverKind.Pickup;
            t.PullExternal = true;
            t.PullX = _taxiX;
            t.PullZ = _taxiZ;
            t.PullState = PullOverState.Approaching;
            SetPickupPlace(ref t, bestLane, bestS, bestPath);
            _taxiFor = t.Id;
        }

        // Distances (metres) up the street, tried in order, where a sent taxi may start.
        private static readonly float[] SendBackM = { 75f, 90f, 105f, 60f, 125f, 150f };

        /// <summary>
        /// Spawns a taxi on the kerb lane nearest the person waiting, about 100 m before the place it will stop (walking
        /// back over the lanes leading there, through junctions), out of view where it can be; returns its index (−1 when
        /// no taxi lane is near or nowhere has room) and its place and way.
        /// </summary>
        private int SendTaxi(out int lane, out float s, out int[] path)
        {
            lane = -1;
            s = 0f;
            path = null;
            int variant = VehicleCatalog.Taxi;
            uint bit = VehicleClasses.Bit(VehicleClass.Taxi);
            PickupGoals(bit, DoorAhead(variant), _taxiX, _taxiZ, _goalLane, _goalPlace, _goalLatest, _goalPen);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int g = 0; g < _goalLane.Count; g++)
                {
                    // The cheapest kerb place first.
                    int gi = -1;
                    for (int k = 0; k < _goalLane.Count; k++)
                        if (_goalPen[k] < float.MaxValue && (gi < 0 || _goalPen[k] < _goalPen[gi] || _goalPen[k] == _goalPen[gi] && _goalLane[k] < _goalLane[gi]))
                            gi = k;
                    if (gi < 0) break;
                    int goal = _goalLane[gi];
                    float place = _goalPlace[gi];
                    _goalPen[gi] = float.MaxValue;
                    foreach (float back in SendBackM)
                    {
                        int sl;
                        float ss;
                        if (!WalkBack(goal, place - back, bit, out sl, out ss)) continue;
                        double x, z;
                        float y, h;
                        LaneGraph.PointAt(_g.Lanes[sl], ss, out x, out z, out y, out h);
                        if (pass == 0 && InView(x, z)) continue; // out of view first; in view only as the last resort
                        byte livery = VehicleCatalog.At(variant).PickLivery(_rng.NextU32());
                        int id;
                        if (!SpawnOnLane(sl, ss, variant, livery, out id, false)) continue;
                        int idx = IndexOf(id);
                        ref Agent a = ref _agents[idx];
                        a.V = Math.Min(a.V, 6f);
                        float cost;
                        if (!FindPickup(ref a, _taxiX, _taxiZ, PickupSearchM, out lane, out s, out path, out cost))
                        {
                            Kill(idx);
                            continue;
                        }
                        return idx;
                    }
                }
                // Second pass: the same places again, in view allowed.
                PickupGoals(bit, DoorAhead(variant), _taxiX, _taxiZ, _goalLane, _goalPlace, _goalLatest, _goalPen);
            }
            return -1;
        }

        /// <summary>The place <paramref name="s"/> metres along lane <paramref name="lane"/> (negative: before its start,
        /// walking back over its predecessors, the same street first) on a road lane a taxi may use; false when the walk
        /// ends (no predecessor) or lands on a junction connector or a short lane.</summary>
        private bool WalkBack(int lane, float s, uint bit, out int at, out float atS)
        {
            at = lane;
            atS = s;
            for (int depth = 0; depth < 16 && atS < 0f; depth++)
            {
                LaneGraph.Lane l = _g.Lanes[at];
                int pick = -1;
                for (int k = 0; k < l.Prev.Length; k++)
                {
                    int p = l.Prev[k];
                    if (p < 0 || p >= _g.Lanes.Count || !_g.Lanes[p].Alive || (_g.Lanes[p].Mask & bit) == 0) continue;
                    LaneGraph.Lane pl = _g.Lanes[p];
                    // The same street (a road lane of the same way, or a connector fed by it) beats a turn in.
                    bool same = pl.WayId == l.WayId || pl.Kind == LaneKind.Connector && pl.Prev.Length > 0 && _g.Lanes[pl.Prev[0]].WayId == l.WayId;
                    if (pl.Kind == LaneKind.UTurn) continue;
                    if (pick < 0 || same && _g.Lanes[pick].WayId != l.WayId || p < pick && same == (_g.Lanes[pick].WayId == l.WayId)) pick = p;
                }
                if (pick < 0) return false;
                at = pick;
                atS += _g.Lanes[pick].Length;
            }
            if (atS < 0f) return false;
            LaneGraph.Lane fl = _g.Lanes[at];
            if (fl.Kind != LaneKind.Road || fl.Ring || fl.Length < 12f) return false;
            atS = Math.Max(VehicleCatalog.At(VehicleCatalog.Taxi).LengthM + 0.5f, Math.Min(atS, fl.Length - 1f));
            return true;
        }

        /// <summary>A lane the class may drive into (a connector only when its exit admits the class too).</summary>
        private bool Usable(int lane, uint bit)
        {
            if (lane < 0 || lane >= _g.Lanes.Count) return false;
            LaneGraph.Lane n = _g.Lanes[lane];
            if (!n.Alive || (n.Mask & bit) == 0) return false;
            return !(n.Kind == LaneKind.Connector && n.Next.Length > 0 && (_g.Lanes[n.Next[0]].Mask & bit) == 0);
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
                if (a.Ghost || a.ChangeFrom >= 0 || a.Committed || a.DwellT > 0f || a.Pull != PullOverKind.None) continue;
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
            double keep = _s.KeepRadiusM > 0f ? Math.Min(far, Math.Max(_s.KeepRadiusM, _s.SpawnMaxM + 30.0)) : far;
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive) continue;
                ref Agent a = ref _agents[i];
                double dx = a.X - _focusX, dz = a.Z - _focusZ;
                double d = Math.Sqrt(dx * dx + dz * dz);
                bool outOfView = !InView(a.X, a.Z);
                // Ambient traffic stays round the player; routed buses, pull-overs and hailed taxis keep the ghost radius.
                double limit = a.Route >= 0 || a.Pull != PullOverKind.None || a.PullExternal ? far : keep;
                if (d > limit && outOfView || a.WaitT > 60f && outOfView && d > _s.SpawnMinM && !a.PullExternal)
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
            _pullBackCount = 0;
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Alive || _agents[i].Pull == PullOverKind.None) continue;
                if (_pullBackCount == _pullBack.Length) Array.Resize(ref _pullBack, _pullBackCount * 2);
                _pullBack[_pullBackCount++] = new PullStatus { AgentId = _agents[i].Id, State = _agents[i].PullState };
            }
            lock (_snapLock)
            {
                PullStatus[] tp = _pullFront;
                _pullFront = _pullBack;
                _pullBack = tp.Length >= _pullFront.Length ? tp : new PullStatus[_pullFront.Length];
                _pullFrontCount = _pullBackCount;
                _taxiForFront = _taxiReq ? _taxiFor : 0;
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

        /// <summary>The agents on a lane and the reservations of the connectors that conflict with it (test diagnostics).</summary>
        internal string DescribeLane(int lane)
        {
            lock (_g.SyncRoot)
            {
                var sb = new System.Text.StringBuilder();
                LaneGraph.Lane l = _g.Lanes[lane];
                sb.Append("lane " + lane + " " + l.Kind + " len " + l.Length.ToString("0.0") + " ctrl " + l.Controller + " next " + string.Join(",", l.Next) +
                          " conflicts " + string.Join(",", l.Conflicts) + " resv " + (lane < _resv.Length ? _resv[lane] : 0) + ":");
                foreach (int c in l.Conflicts) sb.Append(" [" + c + " resv " + (c < _resv.Length ? _resv[c] : 0) + "]");
                for (int i = 0; i < _agents.Length; i++)
                    if (_agents[i].Alive && (_agents[i].Lane == lane || _agents[i].CommitLane == lane || _agents[i].TailLane == lane))
                        sb.Append(" | " + Describe(_agents[i].Id));
                return sb.ToString();
            }
        }

        /// <summary>One line about a junction controller: kind, phase and its timers, each arm's phase and queue (test
        /// diagnostics).</summary>
        internal string DescribeController(int id)
        {
            lock (_g.SyncRoot)
            {
                if (id < 0 || id >= _g.Controllers.Count) return "controller " + id + " none";
                LaneGraph.Controller c = _g.Controllers[id];
                CtrlState st = StateOf(id);
                return "controller " + id + " " + c.Kind + " alive " + c.Alive + " arms " + c.Arms + " phases " + c.Phases + " phase " + st.Phase + " t " +
                       st.PhaseT.ToString("0") + "/" + st.PhaseLen.ToString("0") + " clear " + st.ClearT.ToString("0.0") + " phaseOf " + string.Join(",", c.PhaseOf) +
                       " queue " + string.Join(",", st.Queue);
            }
        }

        /// <summary>One line about an agent's lane, plan and state (test diagnostics).</summary>
        internal string Describe(int agentId)
        {
            lock (_g.SyncRoot)
            {
                int i = IndexOf(agentId);
                if (i < 0) return "agent " + agentId + " gone";
                Agent a = _agents[i];
                LaneGraph.Lane l = _g.Lanes[a.Lane];
                return "agent " + a.Id + " " + a.Class + " lane " + a.Lane + " (" + l.Kind + " way " + l.WayId + " len " + l.Length.ToString("0") + ") s " +
                       a.S.ToString("0.0") + " v " + a.V.ToString("0.0") + " plan " + a.P0 + "," + a.P1 + "," + a.P2 + "," + a.P3 + " committed " +
                       (a.Committed ? a.CommitLane.ToString() : "-") + " wait " + a.WaitT.ToString("0") + " blockedVeh " + a.BlockedByVehicle +
                       " stopLine " + a.AtStopLine + " pull " + a.Pull + "/" + a.PullState + " at " + a.PullLane + "@" + a.PullS.ToString("0") +
                       (a.PullPath != null ? " path " + string.Join(",", a.PullPath) : "") + " route " + a.Route + " dwell " + a.DwellT.ToString("0");
            }
        }

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
