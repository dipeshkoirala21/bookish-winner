using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ghumante.Audio;
using Ghumante.Core.Aviation;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Search;
using Ghumante.Core.Services;
using Ghumante.Core.Streaming;
using Ghumante.Core.Synth;
using Ghumante.Platform;
using Ghumante.Platform.Regions;
using Ghumante.World.Aviation;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using Ghumante.World.Rendering;
using Ghumante.World.Navigation;
using Ghumante.World.Sacred;
using Ghumante.World.Sky;
using Ghumante.World.Streaming;
using UnityEngine;

namespace Ghumante.World
{
    /// <summary>
    /// The streamed 1:1 world (M1_PLAN contract; ARCHITECTURE.md 5, 7.2-7.5, 8). One per scene.
    /// <list type="bullet">
    /// <item><see cref="OpenRegionAsync(string)"/> provisions the region through <see cref="RegionSource"/> (unpacking it on
    /// Android), reads the manifest, opens the pack for random access and loads search and routing on a worker.</item>
    /// <item>Every frame the world streams around <see cref="Focus"/> (the player sets it; game metres): tiles are decoded
    /// and meshed on worker threads and uploaded within a per-frame budget (<see cref="WorldStreamer"/>).</item>
    /// <item><see cref="Ground"/> answers height, normal and surface for exactly what is drawn (Core's
    /// <see cref="TileGroundQuery"/>); <see cref="GroundQuery"/> adds bridge decks and road queries.</item>
    /// <item>Floating origin (ADR-003): Unity positions are <c>world - <see cref="Origin"/></c>. When the focus is more than
    /// 2 km from the origin the scene is rebased at the start of the frame (this runs before default scripts) and
    /// <see cref="OriginShifted"/> passes the delta: subtract it from every Unity position you own.
    /// <see cref="ToScene(WorldPos)"/> and <see cref="ToWorld"/> convert.</item>
    /// <item><see cref="TimeOfDayHours"/> drives the sky, light, haze and ambient (<see cref="WorldSky"/>), and
    /// <see cref="ShowRoute"/> draws the route ribbon.</item>
    /// <item><see cref="Close"/> unloads everything and restores the camera and render settings (back to the menu).</item>
    /// <item>W2 (World/README.md): the building bands, B0 cells and hero replicas (with their hide zones) around the
    /// camera, the instanced dressing (<see cref="DressingRenderer"/>), the life simulations (<see cref="Life"/>: traffic,
    /// people, cows and dogs, aircraft; the Traffic and Wildlife assemblies attach their presenters through
    /// <see cref="AnyReady"/>), the airport (<see cref="AviationPresenter"/>), the shared zone indexes
    /// (<see cref="Zones"/>, <see cref="AreaTypes"/>) and the region sidecars (<see cref="Curated"/>,
    /// <see cref="Transit"/>, <see cref="AviationConfig"/>).</item>
    /// </list>
    /// Main thread only. Keep this object's transform at the identity; the world builds its own scene roots.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Ghumante/World Root")]
    public sealed class WorldRoot : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("Camera configured for the world (far clip for the horizon, skybox). Empty: Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("Set the camera's clip planes and skybox clear while the world is open, restore them on Close.")]
        [SerializeField] private bool configureCamera = true;

        [Header("Rendering")]
        [Tooltip("Materials (Project Setup creates Settings/Resources/GhumanteWorldMaterials). Empty: loaded from Resources.")]
        [SerializeField] private WorldMaterialSet materials;

        [Header("Floating origin")]
        [Tooltip("Rebase when the focus is farther than this from the origin (ARCHITECTURE 5.2: 2 km).")]
        [SerializeField] private float rebaseDistanceM = (float)FloatingOrigin.DefaultThresholdM;

        private IRegionPackSource _source;
        private DeviceTier? _tier;
        private StreamingConfig _config;
        private RegionManifest _manifest;
        private WorldStreamer _streamer;
        private TileGroundQuery _ground;
        private MeshingSettings _meshing;
        private WorldMaterialSet _materials;
        private WorldSky _sky;
        private float _pendingHours = 6.2f;
        private RouteRibbon _route;
        private WorldPos _focus, _origin;
        private WorldPos? _focusOverride;
        private bool _hasFocus;
        private int _openVersion;
        private float _progress;
        private CameraState _camera;
        private string _debugText;
        private float _debugAt;
        private LifeHost _life;
        private DressingRenderer _dressing;
        private AviationPresenter _aviation;
        private HeroSet _heroes;
        private CuratedDb _curated;
        private RouteSet _transit;
        private AviationConfig _aviationConfig;
        private ISoundService _sound;
        private bool _audioWired;
        private WorldAudioPlace _audioPlace;
        private RenderStats _renderStats;
        private int _month = 10;
        private bool _lifeErrorLogged;

        private struct CameraState
        {
            public Camera Camera;
            public float Near, Far;
            public CameraClearFlags Clear;
        }

        /// <summary>The open (or opening) world, for debug tools; null when none.</summary>
        public static WorldRoot Active { get; private set; }

        /// <summary>Raised on the main thread when <see cref="OpenRegionAsync(string)"/> finished: search, routes and
        /// ground are available and tiles start streaming.</summary>
        public event Action Ready;

        /// <summary>Open progress in [0, 1] (unpacking, loading); raised on the main thread.</summary>
        public event Action<float> Progress;

        /// <summary>The floating origin moved by the given delta (new origin - old origin): subtract it from every Unity
        /// position you own. Raised at the start of a frame, before default-order scripts update.</summary>
        public event Action<WorldPos> OriginShifted;

        /// <summary>The world was closed.</summary>
        public event Action Closed;

        /// <summary>Any world became ready (raised after <see cref="Ready"/>). Presenter assemblies that World cannot
        /// reference (Traffic, Wildlife) attach to it from a <c>RuntimeInitializeOnLoadMethod</c>.</summary>
        public static event Action<WorldRoot> AnyReady;

        /// <summary>A detail tile became visible (decoded tile and extras: instances, heroes) or was hidden.</summary>
        public event Action<TileId, TileExtras> DetailTileShown;

        public event Action<TileId> DetailTileHidden;

        /// <summary>Where regions come from. Default: the built-in source (StreamingAssets, unpacked on Android).
        /// Set before opening.</summary>
        public IRegionPackSource RegionSource
        {
            get
            {
                if (_source == null) _source = BuiltInRegionSource.CreateDefault();
                return _source;
            }
            set { _source = value; }
        }

        /// <summary>Device tier for the streaming rings and budgets (ARCHITECTURE 10). Default: the active quality
        /// level (Bootstrap sets it from the detected tier). Takes effect at the next open.</summary>
        public DeviceTier Tier
        {
            get
            {
                if (_tier.HasValue) return _tier.Value;
                int level = QualitySettings.GetQualityLevel();
                return (DeviceTier)Mathf.Clamp(level, (int)DeviceTier.Low, (int)DeviceTier.High);
            }
            set { _tier = value; }
        }

        /// <summary>True between <see cref="OpenRegionAsync(string)"/> and Ready.</summary>
        public bool IsOpening { get; private set; }

        /// <summary>True from Ready until <see cref="Close"/>.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Contract name for <see cref="IsOpen"/>.</summary>
        public bool IsReady
        {
            get { return IsOpen; }
        }

        public string RegionId { get; private set; }

        public RegionManifest Manifest
        {
            get { return _manifest; }
        }

        /// <summary>Streaming settings in use (a copy), or null when closed.</summary>
        public StreamingConfig Config
        {
            get { return _config != null ? _config.Clone() : null; }
        }

        /// <summary>Open progress in [0, 1].</summary>
        public float LoadProgress
        {
            get { return _progress; }
        }

        /// <summary>Where the world streams and the origin follows: the player's position (game metres). Defaults to the
        /// region centre on open.</summary>
        public WorldPos Focus
        {
            get { return _focus; }
            set
            {
                _focus = value;
                _hasFocus = true;
            }
        }

        /// <summary>Debug override of the streaming focus (the free-fly camera): when set, streaming and the floating
        /// origin follow it instead of <see cref="Focus"/>.</summary>
        public WorldPos? FocusOverride
        {
            get { return _focusOverride; }
            set { _focusOverride = value; }
        }

        /// <summary>The focus streaming uses: <see cref="FocusOverride"/> or <see cref="Focus"/>.</summary>
        public WorldPos StreamingFocus
        {
            get { return _focusOverride ?? _focus; }
        }

        /// <summary>The floating origin: Unity position = world position - Origin.</summary>
        public WorldPos Origin
        {
            get { return _origin; }
        }

        /// <summary>Height, normal and surface of what is drawn (null when closed).</summary>
        public IGroundQuery Ground
        {
            get { return _ground; }
        }

        /// <summary><see cref="Ground"/> with bridge-deck layering and nearest-road queries (null when closed).</summary>
        public TileGroundQuery GroundQuery
        {
            get { return _ground; }
        }

        /// <summary>Changes whenever the ground gained or lost an area.</summary>
        public int GroundVersion
        {
            get { return _streamer != null ? _streamer.Scheduler.GroundVersion : 0; }
        }

        /// <summary>Region search (null when closed or the region has no index).</summary>
        public SearchEngine Search { get; private set; }

        /// <summary>Region routing graph (null when closed or the region has none).</summary>
        public RouteGraph Routes { get; private set; }

        /// <summary>Time of day, local solar hours [0, 24). May be set before opening.</summary>
        public float TimeOfDayHours
        {
            get { return _sky != null ? _sky.TimeOfDayHours : _pendingHours; }
            set
            {
                float h = value % 24f;
                _pendingHours = h < 0f ? h + 24f : h;
                if (_sky != null) _sky.TimeOfDayHours = _pendingHours;
            }
        }

        /// <summary>Day/night, light, haze (created when the world opens; null before).</summary>
        public WorldSky Sky
        {
            get { return _sky; }
        }

        /// <summary>The route ribbon (created on first use).</summary>
        public RouteRibbon Route
        {
            get { return EnsureRoute(); }
        }

        public WorldStreamer Streamer
        {
            get { return _streamer; }
        }

        /// <summary>Streaming counters (default when closed).</summary>
        public StreamingStats Stats
        {
            get { return _streamer != null ? _streamer.Scheduler.Stats : default(StreamingStats); }
        }

        /// <summary>The life simulations of the open world (null when closed).</summary>
        public LifeHost Life
        {
            get { return _life; }
        }

        /// <summary>Sacred and compound zones of the visible detail tiles (null when closed): calm mode, prompts, audio.</summary>
        public SacredZoneIndex Zones
        {
            get { return _life != null ? _life.Zones : null; }
        }

        /// <summary>The 250 m area-type grid of the visible detail tiles (null when closed).</summary>
        public AreaTypeGrid AreaTypes
        {
            get { return _life != null ? _life.AreaTypes : null; }
        }

        /// <summary>The region's curated heritage DB (<c>.ghcd</c>), or null when the region has none.</summary>
        public CuratedDb Curated
        {
            get { return _curated; }
        }

        /// <summary>The region's transit routes (<c>.ghrt</c>), or null.</summary>
        public RouteSet Transit
        {
            get { return _transit; }
        }

        /// <summary>The region's aviation sidecar, or null (no airport).</summary>
        public AviationConfig AviationConfig
        {
            get { return _aviationConfig; }
        }

        /// <summary>Hero replicas and hide zones of the region (null when closed).</summary>
        public HeroSet Heroes
        {
            get { return _heroes; }
        }

        /// <summary>The sound service presenters use: the one App sets, else the audio director when it exists, else
        /// silence.</summary>
        public ISoundService Sound
        {
            get
            {
                if (_sound != null) return _sound;
                return AudioDirector.Exists ? AudioDirector.Instance : (ISoundService)NullSoundService.Instance;
            }
            set { _sound = value; }
        }

        /// <summary>Month for the seasonal palettes and the airport schedule (1-12; W2 acceptance runs in October).</summary>
        public int Month
        {
            get { return _month; }
            set
            {
                _month = Mathf.Clamp(value, 1, 12);
                if (_dressing != null) _dressing.Month = _month;
            }
        }

        /// <summary>The materials the world draws with (null when closed); presenters use its instanced materials.</summary>
        public WorldMaterialSet Materials
        {
            get { return _materials; }
        }

        /// <summary>The camera the world draws for (the configured one, else Camera.main).</summary>
        public Camera ViewCamera
        {
            get { return worldCamera != null ? worldCamera : Camera.main; }
        }

        /// <summary>This frame's render counters (streamer, dressing, airport, plus what presenters report).</summary>
        public RenderStats RenderStats
        {
            get { return _renderStats; }
        }

        /// <summary>Presenters report what they drew this frame (the debug HUD counters, W2_DESIGN 10.4).</summary>
        public void ReportLife(int vehicleTris, int vehicles, int peopleTris, int people, int animalTris, int animals, int parkedTris, int parked,
                               int draws)
        {
            _renderStats.VehicleTris += vehicleTris;
            _renderStats.Vehicles += vehicles;
            _renderStats.PeopleTris += peopleTris;
            _renderStats.People += people;
            _renderStats.AnimalTris += animalTris;
            _renderStats.Animals += animals;
            _renderStats.ParkedTris += parkedTris;
            _renderStats.Parked += parked;
            _renderStats.InstancedDraws += draws;
        }

        /// <summary>True when everything selected around the focus is loaded and shown.</summary>
        public bool IsSettled
        {
            get { return _streamer != null && _streamer.Scheduler.IsSettled; }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Open / close

        /// <summary>Open a region by id (for example "kathmandu_core"). Closes any open region first.</summary>
        public Task OpenRegionAsync(string regionId)
        {
            return OpenRegionAsync(regionId, null, CancellationToken.None);
        }

        /// <summary>Open a region, reporting progress in [0, 1] (also raised as <see cref="Progress"/>).</summary>
        public async Task OpenRegionAsync(string regionId, IProgress<float> progress, CancellationToken cancellationToken)
        {
            if (!RegionFiles.IsValidRegionId(regionId)) throw new ArgumentException("invalid region id '" + regionId + "'", nameof(regionId));
            if (IsOpen || IsOpening) Close();
            int version = ++_openVersion;
            IsOpening = true;
            RegionId = regionId;
            Active = this;
            ReportProgress(version, 0f, progress);
            Stream packStream = null, searchStream = null, routeStream = null;
            RegionData data = null;
            try
            {
                IRegionPackSource source = RegionSource;
                if (!await source.IsAvailableAsync(regionId, cancellationToken))
                {
                    var unpack = new Progress<float>(p => ReportProgress(version, p * 0.7f, progress));
                    if (!await source.RequestAsync(regionId, unpack, cancellationToken))
                    {
                        var builtIn = source as BuiltInRegionSource;
                        throw new FileNotFoundException("region '" + regionId + "' is not available" +
                                                        (builtIn != null && builtIn.LastError != null ? ": " + builtIn.LastError : ""));
                    }
                }
                ThrowIfStale(version, cancellationToken);
                ReportProgress(version, 0.7f, progress);

                RegionManifest manifest;
                using (Stream s = await source.OpenAsync(regionId, RegionFileKind.Manifest, cancellationToken))
                {
                    string text = await Task.Run(() => ReadText(s), cancellationToken);
                    manifest = RegionManifest.Parse(text);
                }
                packStream = await source.OpenAsync(regionId, RegionFileKind.Pack, cancellationToken);
                if (manifest.FileFor(RegionFileKind.SearchIndex) != null)
                    searchStream = await source.OpenAsync(regionId, RegionFileKind.SearchIndex, cancellationToken);
                if (manifest.FileFor(RegionFileKind.RouteGraph) != null)
                    routeStream = await source.OpenAsync(regionId, RegionFileKind.RouteGraph, cancellationToken);
                StreamingConfig config = StreamingConfig.ForTier((int)Tier);
                Stream ps = packStream, ss = searchStream, rs = routeStream;
                packStream = searchStream = routeStream = null; // owned by LoadRegionData from here
                // No cancellation token here: a task cancelled before it starts never runs LoadRegionData, and nothing
                // would dispose the three streams. Cancellation is honoured right after (ThrowIfStale), and the catch
                // below disposes what was loaded.
                string sidecarFolder = SidecarFolder(source, regionId);
                data = await Task.Run(() => LoadRegionData(ps, ss, rs, config, manifest, sidecarFolder));
                ThrowIfStale(version, cancellationToken);
                for (int i = 0; i < data.Warnings.Count; i++) Debug.LogWarning("WorldRoot: " + data.Warnings[i]);

                Build(manifest, config, data);
                data = null; // owned by the streamer now
                IsOpening = false;
                IsOpen = true;
                ReportProgress(version, 1f, progress);
                Debug.Log("WorldRoot: opened region '" + regionId + "' (" + manifest.NameEn + ") for tier " + Tier + ", " +
                          _config.Rings.Length + " LOD rings to " + (_config.ViewRadiusM / 1000.0).ToString("0") + " km.");
                Raise(Ready);
                RaiseAnyReady(this);
            }
            catch
            {
                if (packStream != null) packStream.Dispose();
                if (searchStream != null) searchStream.Dispose();
                if (routeStream != null) routeStream.Dispose();
                if (data != null && data.Pack != null) data.Pack.Dispose();
                if (version == _openVersion)
                {
                    IsOpening = false;
                    if (!IsOpen && Active == this) Active = null;
                }
                throw;
            }
        }

        /// <summary>Unload everything (tiles, ground, search, routes, route ribbon), restore the camera and render
        /// settings. Safe to call when nothing is open; an open in progress is abandoned.</summary>
        public void Close()
        {
            _openVersion++;
            bool wasOpen = IsOpen || IsOpening;
            IsOpen = false;
            IsOpening = false;
            if (_life != null)
            {
                _life.Dispose();
                _life = null;
            }
            if (_dressing != null)
            {
                _dressing.Dispose();
                _dressing = null;
            }
            if (_aviation != null)
            {
                _aviation.Dispose();
                _aviation = null;
            }
            if (_audioWired && AudioDirector.Exists)
            {
                AudioDirector audio = AudioDirector.Instance;
                audio.SetZones(null, null);
                audio.SetPlace(0f, 0f, 0f, 0f, 0f);
                if (audio.Occlusion == _audioPlace) audio.Occlusion = null;
                audio.SetWorldActive(false);
                OriginShifted -= ShiftAudio;
            }
            _audioWired = false;
            if (_audioPlace != null) _audioPlace.Clear();
            _audioPlace = null;
            if (_streamer != null)
            {
                _streamer.Dispose();
                _streamer = null;
            }
            _heroes = null;
            _curated = null;
            _transit = null;
            _aviationConfig = null;
            if (_ground != null) _ground.Clear();
            _ground = null;
            _meshing = null;
            _manifest = null;
            _config = null;
            Search = null;
            Routes = null;
            _focusOverride = null;
            if (_route != null) _route.Clear();
            if (_sky != null)
            {
                _pendingHours = _sky.TimeOfDayHours;
                _sky.enabled = false;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debugging.FreeFlyCamera.CloseOverlay(this); // re-enables the main camera the F3 overlay hid
#endif
            RestoreCamera();
            if (_materials != null)
            {
                bool runtime = _materials.RuntimeCreated;
                _materials.DestroyRuntimeMaterials(); // also the W2 materials made at runtime for an older asset set
                if (runtime) Destroy(_materials);
            }
            _materials = null;
            _progress = 0f;
            _debugText = null;
            if (Active == this) Active = null;
            if (wasOpen) Raise(Closed);
        }

        private void Build(RegionManifest manifest, StreamingConfig config, RegionData data)
        {
            _manifest = manifest;
            _config = config;
            Search = data.Search;
            Routes = data.Routes;
            _curated = data.Curated;
            _transit = data.Transit;
            _aviationConfig = data.Aviation;
            _heroes = data.Heroes;
            _meshing = new MeshingSettings();
            _meshing.Heroes = _heroes;
            if (_heroes != null) _meshing.SetHiddenRefs(_heroes.HiddenRefs);
            _ground = new TileGroundQuery(_meshing.Roads);
            _materials = materials != null && materials.IsComplete ? materials : WorldMaterialSet.Load();
            _materials.EnsureExtras();
            if (!_hasFocus) _focus = new WorldPos(manifest.CentreX, 0f, manifest.CentreZ);
            WorldPos unused;
            _origin = FloatingOrigin.Rebase(StreamingFocus, WorldPos.Zero, out unused);
            int tier = (int)Tier;
            _streamer = new WorldStreamer(transform, _materials, data.Pack, config, data.Selector, _ground, _meshing, tier);
            _streamer.Rebase(_origin);
            _life = new LifeHost(LifeSettings.ForTier(tier), RegionSeed(manifest.RegionId), _transit, _aviationConfig, _curated);
            _streamer.DetailShown += OnDetailShown;
            _streamer.DetailHidden += OnDetailHidden;
            _dressing = new DressingRenderer(DressingConfig.ForTier(tier), _materials) { Month = _month };
            if (_aviationConfig != null) _aviation = new AviationPresenter(tier, _materials);
            _audioPlace = new WorldAudioPlace(this);
            if (AudioDirector.Exists)
            {
                AudioDirector.Instance.SetZones(_life.AreaTypes, _life.Zones);
                AudioDirector.Instance.Occlusion = _audioPlace;
                OriginShifted += ShiftAudio;
                _audioWired = true;
            }
            WorldSky sky = EnsureSky();
            if (sky.SkyMaterial == null) sky.SkyMaterial = _materials.sky;
            sky.ViewRadiusM = (float)config.ViewRadiusM; // fog thick enough to hide the end of the last ring
            sky.TimeOfDayHours = _pendingHours;
            sky.enabled = true;
            ConfigureCamera();
            EnsureRoute().Material = _materials.route;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Frame

        private void Awake()
        {
            if (transform.position != Vector3.zero || transform.rotation != Quaternion.identity || transform.lossyScale != Vector3.one)
                Debug.LogWarning("WorldRoot: keep this transform at the identity; the world is placed in scene space.");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GetComponent<Debugging.WorldDebugHotkeys>() == null) gameObject.AddComponent<Debugging.WorldDebugHotkeys>();
#endif
            // A WorldSky placed in the scene only takes over the render settings while a world is open.
            _sky = GetComponent<WorldSky>();
            if (_sky != null)
            {
                _pendingHours = _sky.TimeOfDayHours;
                _sky.enabled = false;
            }
        }

        private void Update()
        {
            if (!IsOpen) return;
            WorldPos focus = StreamingFocus;
            if (FloatingOrigin.ShouldRebase(focus, _origin, rebaseDistanceM)) RebaseTo(focus);
        }

        private void LateUpdate()
        {
            if (!IsOpen) return;
            _streamer.Tick(StreamingFocus);
            Camera cam = ViewCamera;
            Vector3 camPos = cam != null ? cam.transform.position : ToScene(StreamingFocus);
            Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;
            float fov = cam != null ? cam.fieldOfView : 60f;
            float now = Time.time;
            _streamer.UpdateView(camPos, fov, now);
            _renderStats = _streamer.RenderStats;
            int treeTris, trees, propTris, props, draws;
            _dressing.Draw(_streamer.DetailViews, _origin, camPos, out treeTris, out trees, out propTris, out props, out draws);
            _renderStats.TreeTris = treeTris;
            _renderStats.Trees = trees;
            _renderStats.PropTris = propTris;
            _renderStats.Props = props;
            _renderStats.InstancedDraws += draws;
            WorldPos f = StreamingFocus;
            var flat = new Vector2(camFwd.x, camFwd.z);
            if (flat.sqrMagnitude < 1e-6f) flat = new Vector2(0f, 1f);
            flat.Normalize();
            const float wetness = 0f; // no weather source yet: dry and clear (the life sims and the audio agree)
            _life.Tick(Time.deltaTime, f.X, f.Z, flat.x, flat.y, TimeOfDayHours, _month, wetness, WeatherKind.Clear);
            if (_life.Error != null && !_lifeErrorLogged)
            {
                _lifeErrorLogged = true;
                Debug.LogException(_life.Error);
            }
            if (_aviation != null)
            {
                _aviation.Draw(_life, _ground, _origin, camPos, TimeOfDayHours, now, Sound);
                _renderStats.AircraftTris += _aviation.Tris;
                _renderStats.Aircraft += _aviation.Drawn;
            }
            if (_audioWired && AudioDirector.Exists)
            {
                AudioDirector audio = AudioDirector.Instance;
                audio.SetClock(TimeOfDayHours, _month);
                audio.SetWeather(0f, 0f, wetness);
                _audioPlace.Tick(Time.unscaledDeltaTime, audio, f, _ground, _aviationConfig);
            }
        }

        private void OnDetailShown(TileId id, TileExtras extras)
        {
            if (_life != null && extras != null && extras.Source != null) _life.AddTile(id, extras.Source);
            if (_audioPlace != null && extras != null && extras.Source != null) _audioPlace.AddTile(id, extras.Source);
            Action<TileId, TileExtras> h = DetailTileShown;
            if (h != null) h(id, extras);
        }

        private void OnDetailHidden(TileId id)
        {
            if (_life != null) _life.RemoveTile(id);
            if (_audioPlace != null) _audioPlace.RemoveTile(id);
            Action<TileId> h = DetailTileHidden;
            if (h != null) h(id);
        }

        private static void ShiftAudio(WorldPos delta)
        {
            if (AudioDirector.Exists) AudioDirector.Instance.ShiftOrigin(new Vector3((float)delta.X, delta.Y, (float)delta.Z));
        }

        /// <summary>The region seed (FNV-1a 64 of the region id): life sims and audio variation.</summary>
        public static ulong RegionSeed(string regionId)
        {
            byte[] b = Encoding.UTF8.GetBytes(regionId ?? "");
            return Hashes.Fnv1a64(b, 0, b.Length);
        }

        private static void RaiseAnyReady(WorldRoot w)
        {
            Action<WorldRoot> handler = AnyReady;
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<WorldRoot>)d)(w);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private void OnDestroy()
        {
            Close();
            if (_sky != null) Destroy(_sky);
            if (_route != null) Destroy(_route.gameObject);
        }

        /// <summary>Move the focus to <paramref name="position"/> at once: rebase the origin there now (raising
        /// <see cref="OriginShifted"/>) and re-select tiles on the next frame. For spawning and fast travel.</summary>
        public void Teleport(WorldPos position)
        {
            Focus = position;
            if (!IsOpen) return;
            RebaseTo(StreamingFocus);
            _streamer.Scheduler.ForceReselect();
        }

        private void RebaseTo(WorldPos focus)
        {
            WorldPos delta;
            WorldPos origin = FloatingOrigin.Rebase(focus, _origin, out delta);
            if (origin == _origin) return;
            _origin = origin;
            _streamer.Rebase(_origin);
            Action<WorldPos> handler = OriginShifted;
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<WorldPos>)d)(delta);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Helpers

        /// <summary>Unity scene position of a world position.</summary>
        public Vector3 ToScene(WorldPos p)
        {
            return new Vector3((float)(p.X - _origin.X), p.Y - _origin.Y, (float)(p.Z - _origin.Z));
        }

        /// <summary>Unity scene position of game metres (x east, y up, z north).</summary>
        public Vector3 ToScene(double x, float y, double z)
        {
            return new Vector3((float)(x - _origin.X), y - _origin.Y, (float)(z - _origin.Z));
        }

        /// <summary>World position of a Unity scene position.</summary>
        public WorldPos ToWorld(Vector3 scene)
        {
            return new WorldPos(_origin.X + scene.x, _origin.Y + scene.y, _origin.Z + scene.z);
        }

        /// <summary>True when ground is loaded at (x, z) (spawn when this turns true).</summary>
        public bool HasGroundAt(double x, double z)
        {
            TileId area;
            return _ground != null && _ground.TryFinestArea(x, z, out area);
        }

        /// <summary>Draw the route ribbon along a polyline (interleaved x, z game metres, e.g. AStar.RouteGeometry).</summary>
        public void ShowRoute(double[] polylineXZ)
        {
            EnsureRoute().Show(polylineXZ);
        }

        public void ClearRoute()
        {
            if (_route != null) _route.Clear();
        }

        /// <summary>Clip planes and clear mode for a world camera: near 0.3 m, far just past the coarsest ring (up to
        /// 155 km on High), skybox clear. For camera rigs. Depth precision over that range needs a floating-point
        /// depth buffer with reversed Z: ProjectSetup asks URP for D32F (Metal uses it anyway); URP's Android default
        /// is D24 UNorm, where reversed Z gains nothing (about 15 m of depth resolution at 10 km), and GLES3 has no
        /// reversed Z at all.</summary>
        public static void ConfigureCamera(Camera camera, StreamingConfig config)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            double view = config != null ? config.ViewRadiusM : 50000.0;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = (float)(view * 1.25 + 5000.0);
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        /// <summary>Two or three lines of streaming state for the debug HUD (refreshed twice a second; allocates then).</summary>
        public string DebugSummary()
        {
            if (!IsOpen)
                return IsOpening ? "world: opening " + RegionId + " " + Mathf.RoundToInt(_progress * 100f) + "%" : "world: closed";
            if (_debugText != null && Time.unscaledTime - _debugAt < 0.5f) return _debugText;
            WorldPos f = StreamingFocus;
            var sb = new StringBuilder(256);
            sb.Append("world ").Append(RegionId).Append(' ').Append(Tier).Append("  ").Append(_sky != null ? _sky.ClockText() : "");
            if (_focusOverride.HasValue) sb.Append("  free-fly");
            sb.Append('\n').Append(_streamer.Scheduler.Stats.Format());
            sb.Append("\nfocus ").Append(f.X.ToString("0")).Append(", ").Append(f.Z.ToString("0"))
              .Append("  origin ").Append(_origin.X.ToString("0")).Append(", ").Append(_origin.Z.ToString("0"));
            sb.Append('\n').Append(_renderStats.Format());
            if (_life != null)
                sb.Append("\nlife: veh ").Append(_life.VehicleCount).Append(" ppl ").Append(_life.PeopleCount).Append(" anim ")
                  .Append(_life.AnimalCount).Append(" air ").Append(_life.AircraftCount).Append(" tiles ").Append(_life.TileCount)
                  .Append(_life.Error != null ? "  sim error: " + _life.Error.Message : "");
            _debugText = sb.ToString();
            _debugAt = Time.unscaledTime;
            return _debugText;
        }

        private WorldSky EnsureSky()
        {
            if (_sky == null)
            {
                _sky = GetComponent<WorldSky>();
                if (_sky == null)
                {
                    // Only created while opening: its OnEnable takes over the render settings.
                    _sky = gameObject.AddComponent<WorldSky>();
                }
            }
            return _sky;
        }

        private RouteRibbon EnsureRoute()
        {
            if (_route != null) return _route;
            var go = new GameObject("Route Ribbon");
            go.transform.SetParent(transform, false);
            _route = go.AddComponent<RouteRibbon>();
            _route.Init(this);
            if (_materials != null) _route.Material = _materials.route;
            return _route;
        }

        private void ConfigureCamera()
        {
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (!configureCamera || cam == null) return;
            _camera = new CameraState { Camera = cam, Near = cam.nearClipPlane, Far = cam.farClipPlane, Clear = cam.clearFlags };
            ConfigureCamera(cam, _config);
        }

        private void RestoreCamera()
        {
            if (_camera.Camera == null) return;
            _camera.Camera.nearClipPlane = _camera.Near;
            _camera.Camera.farClipPlane = _camera.Far;
            _camera.Camera.clearFlags = _camera.Clear;
            _camera = default(CameraState);
        }

        private void ReportProgress(int version, float value, IProgress<float> progress)
        {
            if (version != _openVersion || value < _progress && value > 0f) return;
            _progress = value;
            if (progress != null) progress.Report(value);
            Action<float> handler = Progress;
            if (handler == null) return;
            try
            {
                handler(value);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void ThrowIfStale(int version, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (version != _openVersion || this == null) throw new OperationCanceledException("the world was closed while opening");
        }

        private static void Raise(Action handler)
        {
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action)d)();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Worker-thread loading (no Unity API)

        private sealed class RegionData
        {
            public PackReader Pack;
            public TileSelector Selector;
            public SearchEngine Search;
            public RouteGraph Routes;
            public CuratedDb Curated;
            public RouteSet Transit;
            public AviationConfig Aviation;
            public HeroSet Heroes;
            public readonly System.Collections.Generic.List<string> Warnings = new System.Collections.Generic.List<string>();
        }

        /// <summary>The local folder of a region's sidecar files (W2: curated DB, transit, aviation), or null when the
        /// source cannot give one (a URL root).</summary>
        private static string SidecarFolder(IRegionPackSource source, string regionId)
        {
            var builtIn = source as BuiltInRegionSource;
            if (builtIn == null || RegionFiles.IsUrl(builtIn.ReadRoot)) return null;
            return RegionFiles.RegionFolder(builtIn.ReadRoot, regionId);
        }

        /// <summary>The manifest entry whose file name ends with <paramref name="suffix"/>.</summary>
        private static string SidecarPath(RegionManifest manifest, string folder, string suffix)
        {
            if (folder == null) return null;
            for (int i = 0; i < manifest.Files.Count; i++)
            {
                string p = manifest.Files[i].Path;
                if (p.EndsWith(suffix, StringComparison.Ordinal))
                {
                    string full = RegionFiles.Join(folder, p);
                    return File.Exists(full) ? full : null;
                }
            }
            return null;
        }

        /// <summary>The W2 sidecars: curated heritage DB (.ghcd, else hero_recipes.json), transit routes (.ghrt) and the
        /// aviation JSON; each is optional and a broken one only disables its feature.</summary>
        private static void LoadSidecars(RegionData d, RegionManifest manifest, string folder)
        {
            try
            {
                string ghcd = SidecarPath(manifest, folder, ".curated.ghcd") ?? SidecarPath(manifest, folder, "hero_recipes.json");
                if (ghcd != null) d.Curated = CuratedDb.Read(File.ReadAllBytes(ghcd));
            }
            catch (Exception e)
            {
                d.Warnings.Add("curated DB unreadable, heroes from the built-in catalogue: " + e.Message);
            }
            try
            {
                string ghrt = SidecarPath(manifest, folder, ".transit.ghrt");
                if (ghrt != null) d.Transit = RouteSet.Read(File.ReadAllBytes(ghrt));
            }
            catch (Exception e)
            {
                d.Warnings.Add("transit routes unreadable, no buses on routes: " + e.Message);
            }
            try
            {
                string av = SidecarPath(manifest, folder, ".aviation.json");
                if (av != null) d.Aviation = AviationConfig.Parse(File.ReadAllText(av, Encoding.UTF8));
            }
            catch (Exception e)
            {
                d.Warnings.Add("aviation sidecar unreadable, no airport: " + e.Message);
            }
            d.Heroes = HeroSet.From(d.Curated);
        }

        private static RegionData LoadRegionData(Stream pack, Stream search, Stream route, StreamingConfig config, RegionManifest manifest,
                                                 string sidecarFolder)
        {
            var d = new RegionData();
            try
            {
                d.Pack = new PackReader(pack, true);
                pack = null;
                d.Selector = TileSelector.ForPack(config, d.Pack);
                if (search != null)
                {
                    try
                    {
                        d.Search = new SearchEngine(SearchIndexReader.Read(ReadAll(search)));
                    }
                    catch (Exception e)
                    {
                        d.Warnings.Add("search index unreadable, search disabled: " + e.Message);
                    }
                }
                if (route != null)
                {
                    try
                    {
                        d.Routes = RouteGraphReader.Read(ReadAll(route));
                    }
                    catch (Exception e)
                    {
                        d.Warnings.Add("routing graph unreadable, routing disabled: " + e.Message);
                    }
                }
                if (manifest != null) LoadSidecars(d, manifest, sidecarFolder);
                return d;
            }
            catch
            {
                if (d.Pack != null) d.Pack.Dispose();
                throw;
            }
            finally
            {
                if (pack != null) pack.Dispose();
                if (search != null) search.Dispose();
                if (route != null) route.Dispose();
            }
        }

        private static byte[] ReadAll(Stream s)
        {
            if (s.CanSeek)
            {
                long len = s.Length - s.Position;
                if (len > int.MaxValue) throw new InvalidDataException("file too large");
                var buf = new byte[len];
                int got = 0;
                while (got < buf.Length)
                {
                    int n = s.Read(buf, got, buf.Length - got);
                    if (n <= 0) throw new EndOfStreamException();
                    got += n;
                }
                return buf;
            }
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static string ReadText(Stream s)
        {
            using (var reader = new StreamReader(s, Encoding.UTF8, true, 4096, true)) return reader.ReadToEnd();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
            AnyReady = null;
        }
    }
}
