using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghumante.Core.Aviation;
using Ghumante.Core.Data;
using Ghumante.Core.Traffic;

namespace Ghumante.World.Life
{
    /// <summary>Per-tier settings of the life simulations (W2_DESIGN 10.4 caps).</summary>
    public sealed class LifeSettings
    {
        public TrafficSettings Traffic;
        public PedestrianSettings Pedestrians;
        public AnimalSettings Animals;

        /// <summary>Longest simulated step (a hitch never teleports agents; the rest carries over to later steps).</summary>
        public float MaxStepS = 0.1f;

        /// <summary>Longest time presenters extrapolate a snapshot along each agent's heading (see
        /// <see cref="LifeHost.SnapshotAgeS"/>).</summary>
        public float MaxExtrapolateS = 0.25f;

        /// <summary>Prepared tiles spliced into the sims per step (each splice rebuilds the tile's junction connectors on
        /// the step's thread: 10-15 ms for a dense core tile on a desktop), so a step never carries several.</summary>
        public int MaxTileCommitsPerStep = 1;

        public static LifeSettings ForTier(int tier)
        {
            switch (tier <= 0 ? 0 : tier >= 2 ? 2 : 1)
            {
                case 0: return new LifeSettings { Traffic = TrafficSettings.Low(), Pedestrians = PedestrianSettings.Low(), Animals = AnimalSettings.Low() };
                case 1: return new LifeSettings { Traffic = TrafficSettings.Mid(), Pedestrians = PedestrianSettings.Mid(), Animals = AnimalSettings.Mid() };
                default: return new LifeSettings { Traffic = TrafficSettings.High(), Pedestrians = PedestrianSettings.High(), Animals = AnimalSettings.High() };
            }
        }
    }

    /// <summary>
    /// Runs Track B's engine-free life simulations for the open world (W2_DESIGN 5.1-5.5, 8.2; contract 10.3 "sims run
    /// on worker threads and expose double-buffered snapshots"): the <see cref="LaneGraph"/> fed with the visible detail
    /// tiles, <see cref="TrafficSim"/> with the <see cref="BusRouteRunner"/> on the region's real routes,
    /// <see cref="PedestrianSim"/>, <see cref="AnimalSim"/> (cows and dogs, pushed into traffic as obstacles) and the
    /// <see cref="AirTrafficSim"/> at TIA, plus the zone indexes the whole runtime shares (<see cref="Zones"/>, the
    /// <see cref="AreaTypes"/> grid). Every frame <see cref="Tick"/> collects the last step's snapshots (poses, horns,
    /// kora, aircraft), applies the tile changes that came in, and starts the next step on a worker; a frame whose step
    /// is still running skips (its time carries over, capped) and presenters extrapolate the held snapshot by
    /// <see cref="SnapshotAgeS"/> so motion stays smooth. The lanes and walk graph of a new tile (30-130 ms for a dense
    /// core tile on a desktop, several times that on a phone) are built by a separate background job
    /// (<see cref="LaneGraph.Prepare"/>, <see cref="PedestrianSim.PrepareTile"/>, nearest tile first) while the steps
    /// keep running on the resident tiles, and each finished tile is spliced in at the start of the next step
    /// (<see cref="LaneGraph.Commit"/>, <see cref="PedestrianSim.CommitTile"/>), so traffic, people and animals never
    /// freeze while a tile edge is crossed. Presenters read <see cref="Vehicles"/>, <see cref="People"/>,
    /// <see cref="Animals"/> and <see cref="Aircraft"/> on the main thread, consume each step's horns once through
    /// <see cref="TakeHorns"/>, and never touch the sims; gameplay calls into the sims through <see cref="Post"/>, which
    /// runs at the start of the next step on the worker. After a step fails the host goes quiet: no new steps, no horns,
    /// and posts are dropped. Main thread apart from the step job.
    /// </summary>
    public sealed class LifeHost : IDisposable
    {
        private readonly LaneGraph _graph;
        private readonly TrafficSim _traffic;
        private readonly PedestrianSim _peds;
        private readonly AnimalSim _animals;
        private readonly BusRouteRunner _buses;
        private readonly AirTrafficSim _air;
        private readonly AviationConfig _aviation;
        private readonly SacredZoneIndex _zones = new SacredZoneIndex();
        private readonly AreaTypeGrid _areaTypes = new AreaTypeGrid();
        private readonly CuratedDb _db;
        private readonly LifeSettings _settings;
        private readonly List<KeyValuePair<TileId, TileData>> _pendingAdd = new List<KeyValuePair<TileId, TileData>>();
        private readonly List<TileId> _pendingRemove = new List<TileId>();
        private readonly List<TileId> _jobRemove = new List<TileId>();

        /// <summary>A tile whose lanes and walk graph the build job prepares; <see cref="Ticket"/> tells a stale build
        /// (the tile went away, or came back meanwhile) from the current one.</summary>
        private sealed class TileWork
        {
            public TileId Id;
            public TileData Data;
            public int Ticket;
            public double Distance;
            public LaneGraph.PreparedTile Lanes;
            public PedestrianSim.PreparedWalk Walk;
            public bool Aprons;

            /// <summary>Set by the build job when this tile is prepared (read on the main thread).</summary>
            public volatile bool Done;

            /// <summary>Main thread: handed on to the step queue already.</summary>
            public bool Taken;
        }

        private readonly List<TileWork> _buildQueue = new List<TileWork>();  // waiting for the build job
        private readonly List<TileWork> _buildIn = new List<TileWork>();     // the running build job's tiles
        private readonly List<TileWork> _readyCommit = new List<TileWork>(); // prepared, waiting for the next step
        private readonly List<TileWork> _jobCommit = new List<TileWork>();   // spliced in by the running step
        private readonly Dictionary<TileId, int> _tickets = new Dictionary<TileId, int>();
        private readonly Action _buildStep;
        private Task _build;
        private int _nextTicket;
        private readonly List<Action<TrafficSim, PedestrianSim, AnimalSim>> _posted = new List<Action<TrafficSim, PedestrianSim, AnimalSim>>();
        private readonly List<Action<TrafficSim, PedestrianSim, AnimalSim>> _jobPosted = new List<Action<TrafficSim, PedestrianSim, AnimalSim>>();
        private readonly Action _step;
        private readonly HashSet<TileId> _resident = new HashSet<TileId>();
        private readonly TileData[] _one = new TileData[1];

        private Task _job;
        private volatile Exception _jobError;
        private float _carryS, _ageS;
        private bool _disposed;

        // Job inputs (written on the main thread while the job is idle).
        private double _fx, _fz;
        private float _vx, _vz = 1f, _dt, _hour = 10f, _wet;
        private int _month = 10;
        private WeatherKind _weather;

        // Snapshots (main thread).
        private AgentPose[] _vehicles;
        private PedPose[] _people;
        private AnimalPose[] _animalPoses;
        private HornEvent[] _horns = new HornEvent[64];
        private KoraState[] _kora;
        private AircraftState[] _aircraft = new AircraftState[64];
        private int _vehicleCount, _peopleCount, _animalCount, _hornCount, _koraCount, _aircraftCount;

        /// <param name="routes">The region's transit routes (.ghrt), or null.</param>
        /// <param name="aviation">The region's aviation sidecar, or null (no airport).</param>
        /// <param name="db">The region's curated DB for the sacred zones, or null.</param>
        public LifeHost(LifeSettings settings, ulong regionSeed, RouteSet routes, AviationConfig aviation, CuratedDb db)
        {
            _settings = settings ?? LifeSettings.ForTier(1);
            _db = db;
            _step = Step; // one delegate for the session, not one per frame
            _buildStep = BuildTiles;
            _graph = new LaneGraph { Sacred = _zones };
            if (routes != null && routes.Restrictions != null) _graph.SetRestrictions(routes.Restrictions);
            _traffic = new TrafficSim(_graph, _settings.Traffic, regionSeed ^ 0x5452414646494331UL) { Sacred = _zones };
            _peds = new PedestrianSim(_graph, _settings.Pedestrians, regionSeed ^ 0x5045444553545249UL) { Sacred = _zones, Traffic = _traffic };
            _animals = new AnimalSim(_graph, _settings.Animals, regionSeed ^ 0x414E494D414C5331UL) { Traffic = _traffic };
            if (routes != null && routes.Routes.Count > 0) _buses = new BusRouteRunner(routes, _graph, _traffic);
            if (aviation != null)
            {
                _aviation = aviation;
                _air = new AirTrafficSim(aviation, regionSeed);
            }
            _vehicles = new AgentPose[Math.Max(16, _settings.Traffic.MaxVehicles * 2)];
            _people = new PedPose[Math.Max(16, _settings.Pedestrians.MaxNpcs + 16)];
            _animalPoses = new AnimalPose[Math.Max(16, _settings.Animals.MaxAnimals + 16)];
            _kora = new KoraState[_people.Length];
        }

        /// <summary>Sacred and compound zones of the visible tiles (W2_DESIGN 10.3): audio, gameplay and traffic share it.
        /// Changed only between steps on the main thread.</summary>
        public SacredZoneIndex Zones
        {
            get { return _zones; }
        }

        /// <summary>The 250 m area-type grid of the visible tiles (ambience, spawns).</summary>
        public AreaTypeGrid AreaTypes
        {
            get { return _areaTypes; }
        }

        /// <summary>The aviation sidecar of the region (null without an airport).</summary>
        public AviationConfig Aviation
        {
            get { return _aviation; }
        }

        public bool HasAirport
        {
            get { return _air != null; }
        }

        /// <summary>Last step's failure (the sims stop stepping), or null.</summary>
        public Exception Error
        {
            get { return _jobError; }
        }

        public int VehicleCount
        {
            get { return _vehicleCount; }
        }

        public AgentPose[] Vehicles
        {
            get { return _vehicles; }
        }

        public int PeopleCount
        {
            get { return _peopleCount; }
        }

        public PedPose[] People
        {
            get { return _people; }
        }

        public int AnimalCount
        {
            get { return _animalCount; }
        }

        public AnimalPose[] Animals
        {
            get { return _animalPoses; }
        }

        /// <summary>Horns of the last collected step that <see cref="TakeHorns"/> has not consumed yet.</summary>
        public int HornCount
        {
            get { return _hornCount; }
        }

        /// <summary>Consumes the horns of the last collected step: returns how many of <see cref="Horns"/> are new and
        /// clears the count, so each horn is played exactly once however many frames a step takes.</summary>
        public int TakeHorns()
        {
            int n = _hornCount;
            _hornCount = 0;
            return n;
        }

        /// <summary>
        /// How far (seconds) the snapshots lag the frame beyond the normal one-step latency: zero while every frame
        /// completes a step, growing while a slow step holds the snapshot. Presenters move each pose this long along its
        /// heading at its speed (and advance clip times) so a held snapshot does not stutter. Capped at
        /// <see cref="LifeSettings.MaxExtrapolateS"/>; zero after a failure.
        /// </summary>
        public float SnapshotAgeS
        {
            get { return _ageS; }
        }

        public HornEvent[] Horns
        {
            get { return _horns; }
        }

        public int KoraCount
        {
            get { return _koraCount; }
        }

        public KoraState[] Kora
        {
            get { return _kora; }
        }

        public int AircraftCount
        {
            get { return _aircraftCount; }
        }

        public AircraftState[] Aircraft
        {
            get { return _aircraft; }
        }

        /// <summary>Detail tiles resident in the sims (zones in; their lanes may still be building, see
        /// <see cref="BuildingTiles"/>).</summary>
        public int TileCount
        {
            get { return _resident.Count; }
        }

        /// <summary>Resident tiles whose lanes and walk graph are not in the sims yet (queued, building in the background,
        /// or waiting for the next step to splice them in).</summary>
        public int BuildingTiles
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _pendingAdd.Count; i++)
                    if (!_resident.Contains(_pendingAdd[i].Key)) n++;
                foreach (KeyValuePair<TileId, int> kv in _tickets)
                {
                    int handed;
                    if (!_handed.TryGetValue(kv.Key, out handed) || handed != kv.Value) n++;
                }
                return n;
            }
        }

        /// <summary>The ticket of each resident tile whose prepared lanes were handed to a step.</summary>
        private readonly Dictionary<TileId, int> _handed = new Dictionary<TileId, int>();

        /// <summary>A detail tile became visible: its zones join between steps (once no tile build is running), its lanes
        /// and walk graph are prepared by the background build job and spliced in at the step after that.</summary>
        public void AddTile(TileId id, TileData t)
        {
            if (_disposed || t == null) return;
            _pendingRemove.Remove(id);
            _pendingAdd.Add(new KeyValuePair<TileId, TileData>(id, t));
        }

        public void RemoveTile(TileId id)
        {
            if (_disposed) return;
            for (int i = _pendingAdd.Count - 1; i >= 0; i--)
                if (_pendingAdd[i].Key == id) _pendingAdd.RemoveAt(i);
            _pendingRemove.Add(id);
        }

        /// <summary>Run <paramref name="action"/> on the sims at the start of the next step (worker thread): the only
        /// safe way for gameplay to call TrafficSim, PedestrianSim or AnimalSim (obstacles, spawns, the player vehicle).</summary>
        public void Post(Action<TrafficSim, PedestrianSim, AnimalSim> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (_jobError != null || _disposed) return; // a failed sim never drains them
            _posted.Add(action);
        }

        /// <summary>
        /// One frame: when the previous step is done, copy its snapshots, apply tile changes (zones between steps while no
        /// tile build runs; finished tile builds are handed to the step) and start the next step of <paramref name="dtS"/>
        /// (plus any skipped time) around the focus, looking along (viewX, viewZ). New tiles are prepared by the background
        /// build job, nearest the focus first.
        /// </summary>
        public void Tick(float dtS, double focusX, double focusZ, float viewX, float viewZ, float gameHour, int month, float wetness01,
                         WeatherKind weather)
        {
            if (_disposed) return;
            dtS = Math.Max(0f, dtS);
            _carryS = Math.Min(_carryS + dtS, 1f);
            if (_job != null)
            {
                if (!_job.IsCompleted)
                {
                    // The step is slow this frame; tile builds still move on (they never wait for a step).
                    TakeBuilt();
                    if (_build == null && _buildQueue.Count > 0 && _jobError == null) StartBuild(focusX, focusZ);
                    Age(dtS);
                    return;
                }
                _job = null;
                Collect();
            }
            if (_jobError != null)
            {
                // Go quiet: frozen poses, no horns, nothing queued forever.
                _hornCount = 0;
                _ageS = 0f;
                _posted.Clear();
                _pendingAdd.Clear();
                _pendingRemove.Clear();
                _readyCommit.Clear();
                _buildQueue.Clear();
                return;
            }

            TakeBuilt();
            // Zones change only here, between steps (the zone index is locked, so a running tile build may read it).
            for (int i = 0; i < _pendingRemove.Count; i++)
            {
                TileId id = _pendingRemove[i];
                if (!_resident.Remove(id)) continue;
                _zones.RemoveTile(id);
                _areaTypes.RemoveTile(id);
                _tickets.Remove(id);
                _handed.Remove(id);
                _jobRemove.Add(id);
            }
            _pendingRemove.Clear();
            for (int i = 0; i < _pendingAdd.Count; i++)
            {
                KeyValuePair<TileId, TileData> kv = _pendingAdd[i];
                if (!_resident.Add(kv.Key)) continue;
                // The tile's zones are in before its lanes are built (sacred lanes are dropped by the build).
                _zones.AddTile(kv.Key, kv.Value, _db);
                _areaTypes.AddTile(kv.Key, kv.Value);
                int ticket = ++_nextTicket;
                _tickets[kv.Key] = ticket;
                _buildQueue.Add(new TileWork { Id = kv.Key, Data = kv.Value, Ticket = ticket });
            }
            _pendingAdd.Clear();
            if (_build == null && _buildQueue.Count > 0) StartBuild(focusX, focusZ);
            // Prepared tiles still wanted go to the steps, nearest first and a few per step (a dense tile's splice costs
            // 10-15 ms on a desktop); stale ones (removed, or re-added meanwhile) are dropped.
            int keep = 0;
            for (int i = 0; i < _readyCommit.Count; i++)
            {
                TileWork w = _readyCommit[i];
                int ticket;
                if (!_tickets.TryGetValue(w.Id, out ticket) || ticket != w.Ticket) continue;
                if (_jobCommit.Count < Math.Max(1, _settings.MaxTileCommitsPerStep))
                {
                    _handed[w.Id] = ticket;
                    _jobCommit.Add(w);
                }
                else _readyCommit[keep++] = w;
            }
            _readyCommit.RemoveRange(keep, _readyCommit.Count - keep);
            _jobPosted.AddRange(_posted);
            _posted.Clear();

            _fx = focusX;
            _fz = focusZ;
            _vx = viewX;
            _vz = viewZ;
            _dt = Math.Min(_carryS, _settings.MaxStepS);
            _carryS -= _dt; // a long hitch is caught up over the next steps, not lost
            _hour = gameHour;
            _month = month;
            _wet = wetness01;
            _weather = weather;
            _job = Task.Run(_step);
            Age(dtS);
        }

        private static readonly Comparison<TileWork> NearestFirst = (a, b) => a.Distance.CompareTo(b.Distance);

        /// <summary>Starts the build job on the queued tiles still wanted, nearest the focus first: the street the player
        /// stands in comes alive first.</summary>
        private void StartBuild(double focusX, double focusZ)
        {
            for (int i = 0; i < _buildQueue.Count; i++)
            {
                TileWork w = _buildQueue[i];
                int ticket;
                if (!_tickets.TryGetValue(w.Id, out ticket) || ticket != w.Ticket) continue; // gone again meanwhile
                double cx = w.Id.X0 + 0.5 * w.Id.Size - focusX, cz = w.Id.Z0 + 0.5 * w.Id.Size - focusZ;
                w.Distance = cx * cx + cz * cz;
                _buildIn.Add(w);
            }
            _buildQueue.Clear();
            if (_buildIn.Count == 0) return;
            _buildIn.Sort(NearestFirst);
            _build = Task.Run(_buildStep);
        }

        /// <summary>Main thread: moves the tiles the build job has finished to the commit queue (the rest of the job keeps
        /// running), and retires the job once it is done.</summary>
        private void TakeBuilt()
        {
            if (_build == null) return;
            bool finished = _build.IsCompleted;
            for (int i = 0; i < _buildIn.Count; i++)
            {
                TileWork w = _buildIn[i];
                if (w.Taken || !w.Done) continue;
                w.Taken = true;
                if (w.Lanes != null) _readyCommit.Add(w);
            }
            if (!finished) return;
            _buildIn.Clear();
            _build = null;
        }

        // Owed time = the running step's dt plus carried time; one frame of it is the normal latency.
        private void Age(float dtS)
        {
            float owed = (_job != null ? _dt : 0f) + _carryS;
            _ageS = Math.Max(0f, Math.Min(owed - dtS, _settings.MaxExtrapolateS));
        }

        /// <summary>Waits for a running step and a running tile build (tests and shutdown); the built tiles join at the
        /// next <see cref="Tick"/>.</summary>
        public void Wait()
        {
            Task b = _build;
            if (b != null)
            {
                try
                {
                    b.Wait();
                }
                catch (AggregateException)
                {
                    // Recorded in _jobError.
                }
            }
            Task j = _job;
            if (j != null)
            {
                try
                {
                    j.Wait();
                }
                catch (AggregateException)
                {
                    // Recorded in _jobError.
                }
                _job = null;
                Collect();
            }
        }

        /// <summary>Waits for a running step only (a tile build may go on): tests and the perf harness.</summary>
        public void WaitStep()
        {
            Task j = _job;
            if (j == null) return;
            try
            {
                j.Wait();
            }
            catch (AggregateException)
            {
                // Recorded in _jobError.
            }
            _job = null;
            Collect();
        }

        /// <summary>Tests: runs on the build job before each tile is prepared (holding it there keeps the tile
        /// building).</summary>
        internal Action<TileId> BeforeTileBuild { get; set; }

        /// <summary>Tests: the lane graph (read it only between steps).</summary>
        internal LaneGraph Graph
        {
            get { return _graph; }
        }

        /// <summary>Background build job: prepares the lanes and walk graph of each queued tile, nearest first. Reads only
        /// the tiles, their cached road layouts and the (internally locked) zone index, never the simulations, so the steps
        /// run on meanwhile.</summary>
        private void BuildTiles()
        {
            for (int i = 0; i < _buildIn.Count; i++)
            {
                TileWork w = _buildIn[i];
                try
                {
                    if (_disposed || _jobError != null) return;
                    Action<TileId> hook = BeforeTileBuild;
                    if (hook != null) hook(w.Id);
                    w.Lanes = _graph.Prepare(w.Id, w.Data);
                    w.Walk = PedestrianSim.PrepareTile(w.Id, w.Data);
                    w.Aprons = _aviation != null && HasAprons(w.Data);
                }
                catch (Exception e)
                {
                    _jobError = e;
                    return;
                }
                finally
                {
                    w.Done = true;
                }
            }
        }

        private void Step()
        {
            try
            {
                for (int i = 0; i < _jobRemove.Count; i++)
                {
                    _graph.RemoveTile(_jobRemove[i]);
                    _peds.RemoveTile(_jobRemove[i]);
                }
                for (int i = 0; i < _jobCommit.Count; i++)
                {
                    TileWork w = _jobCommit[i];
                    _graph.Commit(w.Lanes);
                    _peds.CommitTile(w.Walk);
                    if (w.Aprons)
                    {
                        _one[0] = w.Data;
                        _aviation.AddApronsFrom(_one);
                        _one[0] = null;
                    }
                    w.Data = null;
                    w.Lanes = null;
                    w.Walk = null;
                }
                for (int i = 0; i < _jobPosted.Count; i++) _jobPosted[i](_traffic, _peds, _animals);
                _traffic.SetFocus(_fx, _fz, 0f);
                _traffic.SetView(_vx, _vz);
                _peds.SetFocus(_fx, _fz, 0f);
                _peds.SetView(_vx, _vz);
                _animals.SetFocus(_fx, _fz, 0f);
                if (_dt > 0f)
                {
                    if (_buses != null) _buses.Step(_dt, _hour);
                    _traffic.Step(_dt, _hour, _wet);
                    _peds.Step(_dt, _hour, _wet);
                    _animals.Step(_dt, _hour, _wet);
                    if (_air != null) _air.Step(_dt, _hour, _month, _weather);
                }
            }
            catch (Exception e)
            {
                _jobError = e;
            }
            finally
            {
                _jobCommit.Clear();
                _jobRemove.Clear();
                _jobPosted.Clear();
            }
        }

        private static bool HasAprons(TileData t)
        {
            for (int i = 0; i < t.Areas.Count; i++)
                if (t.Areas[i].Kind == AreaKind.Apron) return true;
            return false;
        }

        private void Collect()
        {
            _vehicleCount = _traffic.CopyPoses(_vehicles);
            _hornCount = _traffic.CopyHorns(_horns);
            _peopleCount = _peds.CopyPoses(_people);
            _koraCount = _peds.CopyKora(_kora);
            _animalCount = _animals.CopyPoses(_animalPoses);
            _aircraftCount = _air != null ? _air.CopyStates(_aircraft) : 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Task j = _job;
            if (j != null)
            {
                try
                {
                    j.Wait(2000);
                }
                catch (AggregateException)
                {
                }
            }
            Task b = _build;
            if (b != null)
            {
                try
                {
                    b.Wait(2000);
                }
                catch (AggregateException)
                {
                }
            }
            _job = null;
            _build = null;
            Interlocked.MemoryBarrier();
        }
    }
}
