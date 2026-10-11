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
    /// <see cref="SnapshotAgeS"/> so motion stays smooth. Presenters read <see cref="Vehicles"/>, <see cref="People"/>,
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
        private readonly List<KeyValuePair<TileId, TileData>> _jobAdd = new List<KeyValuePair<TileId, TileData>>();
        private readonly List<TileId> _jobRemove = new List<TileId>();
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

        /// <summary>Lane tiles resident in the sims.</summary>
        public int TileCount
        {
            get { return _resident.Count; }
        }

        /// <summary>A detail tile became visible: its zones join at once (between steps), its lanes and walk graph at the
        /// next step.</summary>
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
        /// One frame: when the previous step is done, copy its snapshots, apply tile changes and start the next step
        /// of <paramref name="dtS"/> (plus any skipped time) around the focus, looking along (viewX, viewZ).
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
                return;
            }

            // Zones change only here, while no step runs (the step's lane building reads them).
            for (int i = 0; i < _pendingRemove.Count; i++)
            {
                TileId id = _pendingRemove[i];
                if (!_resident.Remove(id)) continue;
                _zones.RemoveTile(id);
                _areaTypes.RemoveTile(id);
                _jobRemove.Add(id);
            }
            _pendingRemove.Clear();
            for (int i = 0; i < _pendingAdd.Count; i++)
            {
                KeyValuePair<TileId, TileData> kv = _pendingAdd[i];
                if (!_resident.Add(kv.Key)) continue;
                _zones.AddTile(kv.Key, kv.Value, _db);
                _areaTypes.AddTile(kv.Key, kv.Value);
                _jobAdd.Add(kv);
            }
            _pendingAdd.Clear();
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

        // Owed time = the running step's dt plus carried time; one frame of it is the normal latency.
        private void Age(float dtS)
        {
            float owed = (_job != null ? _dt : 0f) + _carryS;
            _ageS = Math.Max(0f, Math.Min(owed - dtS, _settings.MaxExtrapolateS));
        }

        /// <summary>Waits for a running step (tests and shutdown).</summary>
        public void Wait()
        {
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

        private void Step()
        {
            try
            {
                for (int i = 0; i < _jobRemove.Count; i++)
                {
                    _graph.RemoveTile(_jobRemove[i]);
                    _peds.RemoveTile(_jobRemove[i]);
                }
                for (int i = 0; i < _jobAdd.Count; i++)
                {
                    _graph.AddTile(_jobAdd[i].Key, _jobAdd[i].Value);
                    _peds.AddTile(_jobAdd[i].Key, _jobAdd[i].Value);
                    if (_aviation != null && HasAprons(_jobAdd[i].Value))
                    {
                        _one[0] = _jobAdd[i].Value;
                        _aviation.AddApronsFrom(_one);
                        _one[0] = null;
                    }
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
                _jobAdd.Clear();
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
            _job = null;
            Interlocked.MemoryBarrier();
        }
    }
}
