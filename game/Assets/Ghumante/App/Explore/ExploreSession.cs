using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghumante.Audio;
using Ghumante.Characters;
using Ghumante.Characters.Cameras;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Routing;
using Ghumante.Core.Save;
using Ghumante.Core.Search;
using Ghumante.Core.Services;
using Ghumante.Core.Traffic;
using Ghumante.Platform;
using Ghumante.Platform.Regions;
using Ghumante.Traffic;
using Ghumante.UI.Hud;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using Ghumante.UI.Screens;
using Ghumante.Vehicles.Visuals;
using Ghumante.World;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using Ghumante.World.Rendering;
using Ghumante.World.Streaming;
using UnityEngine;

namespace Ghumante.App.Explore
{
    /// <summary>
    /// The Explore flow (M1 track D): from the main menu's Explore to riding through 1:1 Kathmandu and back.
    /// <list type="number">
    /// <item><b>Open</b>: a <see cref="WorldRoot"/> opens the best installed region (<see cref="ExploreRegions.Pick"/>:
    /// kathmandu_valley when imported, else the kathmandu_core sample) with progress on the loading overlay; no region
    /// shows how to install one.</item>
    /// <item><b>Spawn</b>: search "Thamel", snap to the nearest motorbike-routable node and face along its road
    /// (<see cref="ExploreSpawn"/>); wait until the ground there has streamed in, then stand there on foot beside the
    /// garage's scooter, in the saved appearance (<c>player.appearance</c>, a new random one on first play).</item>
    /// <item><b>W2 play</b> (W2_DESIGN 6): the explorer walks, jumps, namastes, hops on any garage or community-fleet
    /// vehicle, rides along in taxis and buses; the session feeds it the world (sacred zones, the traffic snapshot,
    /// community-fleet parked vehicles near the player, the nearest cow), posts the player to the sims through
    /// <see cref="LifeHost.Post"/> (a traffic obstacle, the pedestrians' hop-aside capsule, a taxi's pull-over), wires
    /// the audio listener and maps the explorer's prompt and layout onto the HUD.</item>
    /// <item><b>Play</b>: every frame the merged controls (keyboard and gamepad through <see cref="ExplorerInput"/>, touch
    /// through the HUD) drive the <see cref="ExplorerController"/>, the world streams around it (its focus), the
    /// <see cref="ChaseCameraRig"/> follows (W2 detail pass: collision-aware over the world's
    /// <see cref="IViewObstacleQuery"/>, a camera angle per vehicle class cycled with C, the right-stick press or the HUD
    /// camera button and remembered in the save), haptics answer bumps, surfaces and recoveries, and the HUD shows speed,
    /// surface, place, compass and the route chip. "Ride there" plans with Core's A* for how the explorer travels
    /// (<see cref="RoutePlanner.ProfileFor"/>: the GHRG car profile in any four-wheeler, so car routes keep off streets a
    /// car cannot use; motorbike, bicycle or foot otherwise), re-plans when the explorer changes vehicle, draws the world's
    /// route ribbon and shows the next turn; arriving within about 40 m celebrates.</item>
    /// <item><b>Close</b>: the world closes, everything is destroyed and unused assets are unloaded before the menu
    /// comes back (<see cref="ExitRequested"/>).</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ExploreSession : MonoBehaviour
    {
        /// <summary>Morning light for the first ride (local solar hours).</summary>
        public const float StartHours = 8.5f;

        /// <summary>Share of the selected tiles that must be on screen before the ride starts.</summary>
        public const float SpawnStreamProgress = 0.85f;

        /// <summary>Start anyway once the ground under the spawn exists and this long has passed.</summary>
        public const float SpawnPatienceS = 12f;

        /// <summary>Start even without ground after this long (the scooter waits for it).</summary>
        public const float SpawnGiveUpS = 40f;

        public const float SurfaceInterval = 0.2f;
        public const float PlaceInterval = 0.5f;
        public const float RerouteCooldownS = 6f;

        /// <summary>A new vehicle class must hold this long before the route is re-planned for it (hopping on and off).</summary>
        public const float ProfileSettleS = 0.6f;

        /// <summary>Farther than this from the route, the chip's arrow points back to it.</summary>
        public const double OffRouteChipM = 25.0;

        private enum Phase
        {
            Idle,
            Opening,
            Spawning,
            Playing,
            Failed,
            Closed,
        }

        /// <summary>Traffic obstacle ids the session owns (the player, a taxi's pull-over point).</summary>
        public const int PlayerObstacleId = 0x50_4C_41_59, PullOverObstacleId = 0x50_55_4C_4C;

        /// <summary>Community-fleet vehicles are offered within this distance.</summary>
        public const float FleetSearchM = 30f;

        private ExploreScreen _screen;
        private SaveData _save;
        private Action _persist;
        private VehicleMeshCache _vehicleCache;
        private readonly Dictionary<TileId, TileExtras> _detailTiles = new Dictionary<TileId, TileExtras>();
        /// <summary>Parked community-fleet vehicles the player has borrowed: not drawn (TrafficPresenter.HiddenParked)
        /// and not offered, by parked id, until the vehicle goes home, whatever happens to their tiles meanwhile.</summary>
        private readonly HashSet<uint> _hiddenParked = new HashSet<uint>();
        private readonly ParkedSpot[] _parked = new ParkedSpot[48];
        private int _parkedCount;
        private float _parkedTimer;
        private volatile float _cowDistance = float.PositiveInfinity;
        private double _cowX, _cowZ;
        private Action<TrafficSim, PedestrianSim, AnimalSim> _postPlayer;
        private double _obstacleX, _obstacleZ;
        private float _obstacleHeading, _obstacleSpeed, _obstacleWidth, _obstacleFront, _obstacleRadius;
        private bool _vehicleBody;
        private volatile bool _postPassenger;
        private float _postTimer;
        private int _postedPull = -1;

        /// <summary>The player is posted to the sims at 10 Hz (LifeHost.Post wraps each call in a closure).</summary>
        public const float PostIntervalS = 0.1f;
        private int _pullAgent = -1;
        private double _pullX, _pullZ;
        private bool _listenerAttached;
        private Vector3 _lastListener;
        private bool _garagePressed;
        private IHaptics _haptics;
        private MotionSettings _motion;
        private Localizer _localizer;
        private DeviceTier _tier;

        private GameObject _worldObject;
        private WorldRoot _world;
        private ExplorerController _explorer;
        private readonly ChaseCameraRig _rig = new ChaseCameraRig();
        private GameObject _ownCamera;
        private ExplorerInput _input;
        private TouchControlsVisibility _touchVisibility;
        private Material _toonMaterial;
        private RoutePlanner _planner;
        private PlaceNamer _namer;
        private RouteGuide _guide;
        private SearchEntry _destination;
        private bool _blockedLastFrame;
        private CancellationTokenSource _cts;
        private Phase _phase;
        private SpawnPoint _spawn;
        private float _spawnWait;
        private float _surfaceTimer;
        private float _placeTimer;
        private float _rerouteCooldown;
        private int _routeToken;
        private bool _planning;
        private ControlFrame _frame;
        private CameraViewMemory _views;
        private bool _cameraPressed;
        private Travel _routeProfile = Travel.Motorbike;
        private float _profileSettle;

        /// <summary>The player wants the main menu back (Main menu in the pause panel, Back on the loading overlay).
        /// The owner calls <see cref="Close"/> and shows the menu.</summary>
        public event Action ExitRequested;

        public WorldRoot World
        {
            get { return _world; }
        }

        public ExplorerController Explorer
        {
            get { return _explorer; }
        }

        /// <summary>True once the ride started (the loading overlay is gone).</summary>
        public bool Playing
        {
            get { return _phase == Phase.Playing; }
        }

        /// <summary>Creates the session object and starts opening the world behind <paramref name="screen"/>'s overlay.</summary>
        public static ExploreSession Begin(ExploreScreen screen, IHaptics haptics, MotionSettings motion, Localizer localizer,
                                           DeviceTier tier)
        {
            return Begin(screen, haptics, motion, localizer, tier, null, null);
        }

        /// <summary>As above, with the save (<c>player.appearance</c> and <c>player.garage</c> are read from it and written
        /// back) and the owner's persist call.</summary>
        public static ExploreSession Begin(ExploreScreen screen, IHaptics haptics, MotionSettings motion, Localizer localizer,
                                           DeviceTier tier, SaveData save, Action persist)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            var go = new GameObject("Explore Session");
            ExploreSession session = go.AddComponent<ExploreSession>();
            session._save = save ?? new SaveData();
            session._persist = persist;
            session._screen = screen;
            session._haptics = haptics ?? new NullHaptics();
            session._motion = motion ?? new MotionSettings();
            session._localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            session._tier = tier;
            session.Wire();
            session.Open();
            return session;
        }

        /// <summary>Closes the world and frees everything (meshes, materials, region data); safe to call twice.</summary>
        public void Close()
        {
            Close(true);
        }

        private void Close(bool unloadAssets)
        {
            if (_phase == Phase.Closed) return;
            _phase = Phase.Closed;
            _routeToken++;
            if (_cts != null)
            {
                // Cancelled, not disposed: the open in flight may still pass its token around while it unwinds.
                _cts.Cancel();
                _cts = null;
            }
            if (_input != null)
            {
                _input.Dispose();
                _input = null;
            }
            _rig.Detach();
            if (AudioDirector.Exists)
            {
                // Silence the city before the menu shows (the director outlives the session).
                AudioDirector.Instance.SetWorldActive(false);
                if (_listenerAttached) AudioDirector.Instance.DetachListener();
            }
            _listenerAttached = false;
            if (_explorer != null)
            {
                // Keep the garage selection (and any wardrobe change) for next time.
                PlayerProfile.StoreGarage(_save, _explorer.Garage);
                PlayerProfile.StoreAppearance(_save, _explorer.Recipe);
                if (_persist != null) _persist();
                _explorer.ModeChanged -= OnModeChanged;
                _explorer.Notice -= OnNotice;
                _explorer.FleetVehicle -= OnFleetVehicle;
                _explorer.SacredZoneChanged -= OnSacredZone;
                Destroy(_explorer.gameObject);
                _explorer = null;
            }
            if (_world != null)
            {
                _world.OriginShifted -= OnOriginShifted;
                _world.DetailTileShown -= OnDetailShown;
                _world.DetailTileHidden -= OnDetailHidden;
                _world.Close();
                _world = null;
            }
            _detailTiles.Clear();
            _hiddenParked.Clear();
            if (_vehicleCache != null)
            {
                _vehicleCache.Dispose();
                _vehicleCache = null;
            }
            if (_worldObject != null) Destroy(_worldObject);
            _worldObject = null;
            if (_ownCamera != null) Destroy(_ownCamera);
            _ownCamera = null;
            if (_toonMaterial != null) Destroy(_toonMaterial);
            _toonMaterial = null;
            _planner = null;
            _namer = null;
            _guide = null;
            _destination = null;
            if (_screen != null) Unwire();
            _screen = null;
            Destroy(gameObject);
            if (!unloadAssets) return;
            // Region data, tile meshes and caches are unreferenced now: give the memory back before the menu.
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        // -------------------------------------------------------------------------------------------------------------
        // Opening and spawning

        private void Wire()
        {
            _screen.MenuRequested += OnMenuRequested;
            _screen.ModeToggleRequested += ToggleMode;
            _screen.GarageRequested += OnGarageRequested;
            _screen.RouteCancelRequested += CancelRoute;
            _screen.RideToRequested += RideTo;
            _screen.TeleportRequested += TeleportTo;
            _screen.TimeOfDayChanged += OnTimeOfDayChanged;
            _screen.CameraRequested += OnCameraRequested;
        }

        private void Unwire()
        {
            _screen.MenuRequested -= OnMenuRequested;
            _screen.ModeToggleRequested -= ToggleMode;
            _screen.GarageRequested -= OnGarageRequested;
            _screen.RouteCancelRequested -= CancelRoute;
            _screen.RideToRequested -= RideTo;
            _screen.TeleportRequested -= TeleportTo;
            _screen.TimeOfDayChanged -= OnTimeOfDayChanged;
            _screen.CameraRequested -= OnCameraRequested;
        }

        private async void Open()
        {
            _phase = Phase.Opening;
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;
            _worldObject = new GameObject("Ghumante World");
            _world = _worldObject.AddComponent<WorldRoot>();
            _world.Tier = _tier;
            _world.TimeOfDayHours = StartHours;
            _world.OriginShifted += OnOriginShifted;
            _world.DetailTileShown += OnDetailShown;
            _world.DetailTileHidden += OnDetailHidden;
            _screen.ShowLoading("explore.loading.finding", null);
            _screen.SetLoadingProgress(0.02f);
            try
            {
                IReadOnlyList<string> installed = await _world.RegionSource.ListRegionsAsync(ct);
                if (!StillOpening()) return;
                string regionId = ExploreRegions.Pick(installed);
                if (regionId == null)
                {
                    _phase = Phase.Failed;
                    Debug.LogWarning("ExploreSession: no region installed. Run Ghumante > Project Setup (copies the kathmandu_core " +
                                     "sample into StreamingAssets/Regions) or Ghumante > Import Region Pack.");
                    _screen.ShowNoRegion();
                    return;
                }
                // Audio before the world opens, so the world wires zones, clock and origin shifts to it (Track C2).
                AudioDirector.Create(AudioDirector.DetectTier(), (uint)WorldRoot.RegionSeed(regionId));
                // The manifest (with its Nepali name) is read only by OpenRegionAsync; until then Nepali gets a generic
                // line rather than the id spelled out in English.
                if (_localizer.Locale == Localizer.Nepali) _screen.ShowLoading("explore.loading.opening_map", null);
                else _screen.ShowLoading("explore.loading.opening", ExploreRegions.Label(regionId));
                var progress = new Progress<float>(p =>
                {
                    if (_phase == Phase.Opening && _screen != null) _screen.SetLoadingProgress(0.05f + 0.5f * p);
                });
                await _world.OpenRegionAsync(regionId, progress, ct);
                if (!StillOpening()) return;

                RegionManifest manifest = _world.Manifest;
                SearchEngine search = _world.Search;
                RouteGraph graph = _world.Routes;
                double cx = manifest.CentreX, cz = manifest.CentreZ;
                RoutePlanner planner = null;
                SpawnPoint spawn = default(SpawnPoint);
                await Task.Run(() =>
                {
                    planner = graph != null ? new RoutePlanner(graph) : null;
                    spawn = ExploreSpawn.Find(search, graph, planner != null ? planner.Starts : null, ExploreSpawn.DefaultPlace, cx, cz);
                }, ct);
                if (!StillOpening()) return;

                _planner = planner;
                _spawn = spawn;
                _namer = new PlaceNamer(search != null ? search.Index : null);
                bool nepali = _localizer.Locale == Localizer.Nepali;
                string place = spawn.Place != null ? spawn.Place.Display(nepali) : (nepali && !string.IsNullOrEmpty(manifest.NameNe) ? manifest.NameNe : manifest.NameEn);
                _screen.ShowLoading("explore.loading.streets", place);
                _screen.SetLoadingProgress(0.6f);
                _world.Teleport(new WorldPos(spawn.X, spawn.HeightHint, spawn.Z));
                _spawnWait = 0f;
                _phase = Phase.Spawning;
                Debug.Log("ExploreSession: spawning at " + spawn.X.ToString("0.0") + ", " + spawn.Z.ToString("0.0") +
                          (spawn.OnRoad ? " on road node " + spawn.Node : " (no road network)") +
                          ", heading " + (spawn.HeadingRad * Mathf.Rad2Deg).ToString("0") + " deg.");
            }
            catch (OperationCanceledException)
            {
                // Closed while opening.
            }
            catch (Exception e)
            {
                if (_phase != Phase.Opening || _screen == null) return;
                _phase = Phase.Failed;
                Debug.LogException(e);
                _screen.ShowLoadFailed(e.Message);
            }
        }

        private bool StillOpening()
        {
            return this != null && _phase == Phase.Opening && _world != null && _screen != null;
        }

        private void UpdateSpawning(float dt)
        {
            _spawnWait += dt;
            float stream = _world.Streamer != null ? _world.Streamer.Scheduler.Progress : 0f;
            bool ground = _world.HasGroundAt(_spawn.X, _spawn.Z);
            _screen.SetLoadingProgress(0.6f + 0.4f * Mathf.Clamp01(ground ? Mathf.Max(stream, 0.3f) : stream * 0.5f));
            bool ready = ground && (stream >= SpawnStreamProgress || _world.IsSettled || _spawnWait > SpawnPatienceS);
            if (ready || _spawnWait > SpawnGiveUpS) StartPlaying();
        }

        private void StartPlaying()
        {
            // The world's sound starts with play, not under the loading screen.
            if (AudioDirector.Exists) AudioDirector.Instance.SetWorldActive(true);
            _toonMaterial = CreateToonMaterial();
            _vehicleCache = new VehicleMeshCache();
            // The player's look and garage from the save; a first play draws a fresh look (random skin, never #1).
            bool fresh = !PlayerProfile.HasAppearance(_save);
            uint seed = (uint)(DateTime.UtcNow.Ticks ^ (DateTime.UtcNow.Ticks >> 32));
            CharacterRecipe recipe = PlayerProfile.LoadAppearance(_save, seed);
            Garage garage = PlayerProfile.LoadGarage(_save, recipe.Seed);
            if (fresh)
            {
                PlayerProfile.StoreAppearance(_save, recipe);
                PlayerProfile.StoreGarage(_save, garage);
                if (_persist != null) _persist();
            }
            _explorer = ExplorerController.Create(_toonMaterial, _vehicleCache, recipe, garage);
            _explorer.Bind(_world.GroundQuery, _world.Origin);
            _explorer.ReducedMotion = _motion.ReduceMotion;
            _explorer.ModeChanged += OnModeChanged;
            _explorer.Notice += OnNotice;
            _explorer.FleetVehicle += OnFleetVehicle;
            _explorer.SacredZoneChanged += OnSacredZone;
            _explorer.Spawn(_spawn.X, _spawn.Z, _spawn.HeadingRad, ExplorerMode.Walk, _spawn.HeightHint);
            _postPlayer = PostPlayer;

            Camera camera = Camera.main;
            if (camera == null)
            {
                _ownCamera = new GameObject("Explore Camera") { tag = "MainCamera" };
                camera = _ownCamera.AddComponent<Camera>();
            }
            _rig.Attach(camera, _world.Config);
            _rig.ReducedMotion = _motion.ReduceMotion;
            _views = CameraViewMemory.Load(_save);
            _rig.Views = _views;
            _rig.Obstacles = ViewObstacles();
            _rig.Snap();

            _input = new ExplorerInput { Enabled = true };
            _touchVisibility = new TouchControlsVisibility(ExplorerInput.TouchscreenPresent);
            _screen.SetTouchControlsVisible(_touchVisibility.Visible);
            _screen.Search.Engine = _world.Search;
            _screen.Search.TeleportAllowed = Application.isEditor || Debug.isDebugBuild;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _screen.EnableDebugTime(_world.TimeOfDayHours);
#endif
            _screen.SetLayout(ControlLayout.Walk);
            _screen.HideLoading();
            _phase = Phase.Playing;
            _placeTimer = 0f;
            _surfaceTimer = 0f;
            if (_spawn.Place != null)
            {
                string name = _spawn.Place.Display(_localizer.Locale == Localizer.Nepali);
                _screen.ShowToast(_localizer.Format("explore.spawn", name), "gh-toast__icon--star", 3.5f);
            }
            _haptics.Play(HapticKind.MediumImpact);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Frame

        private void Update()
        {
            if (_screen == null) return;
            float dt = Time.deltaTime;
            switch (_phase)
            {
                case Phase.Spawning:
                    UpdateSpawning(dt);
                    return;
                case Phase.Playing:
                    UpdatePlaying(dt);
                    return;
            }
        }

        private void UpdatePlaying(float dt)
        {
            bool blocked = _screen.BlocksGameplay;
            // A panel closed since the last frame: the button press that closed it (gamepad A on "Ride there" or
            // Resume) is not also a Walk/Ride, search or map press.
            bool settling = _blockedLastFrame;
            // Keys belong to the search field and Settings while they are open.
            _input.Enabled = !_screen.SearchOpen && !_screen.SettingsOpen;
            var frame = new ControlFrame();
            _input.Read(ref frame, dt);
            TouchState touch = _screen.Touch.Read();
            frame.Merge(ControlsFromTouch(touch));

            TouchControlsVisibility.Source source = TouchControlsVisibility.Source.None;
            if (touch.Touched || touch.Active) source = TouchControlsVisibility.Source.Touch;
            else if (frame.Device == ControlDevice.KeyboardMouse || frame.Device == ControlDevice.Gamepad)
                source = TouchControlsVisibility.Source.KeysOrPad;
            if (_touchVisibility.Update(source)) _screen.SetTouchControlsVisible(_touchVisibility.Visible);

            if (frame.Pause) _screen.TogglePause();
            if (_garagePressed)
            {
                frame.Whistle = true;
                _garagePressed = false;
            }
            if (_cameraPressed)
            {
                frame.CameraCycle = true;
                _cameraPressed = false;
            }
            if (!blocked && !settling)
            {
                if (frame.Search) _screen.OpenSearch(frame.Device == ControlDevice.Gamepad);
                if (frame.Map) _screen.ShowToast(_localizer.Get("menu.map_soon"), "gh-toast__icon--map");
                if (frame.CameraCycle) CycleCamera();
            }
            blocked = _screen.BlocksGameplay;
            _blockedLastFrame = blocked;
            if (blocked) frame = new ControlFrame { ZoomSteps = frame.ZoomSteps };
            _frame = frame;

            if (settling)
            {
                frame.ToggleMode = false;
                frame.ActionHeld = false;
            }
            _explorer.ReducedMotion = _motion.ReduceMotion;
            _rig.ReducedMotion = _motion.ReduceMotion;
            ExplorerContext context = BuildContext(dt);
            _explorer.Tick(dt, frame, _rig.YawRad, blocked, context);
            WorldPos position = _explorer.Position;
            PostToLife();
            UpdateHud();
            UpdateListener(dt, position);
            _world.Focus = position;
            _screen.Search.SetPlayerPosition(position.X, position.Z);

            HapticKind kind;
            if (!blocked && ExploreFeedback.HapticFor(_explorer.LastEvents, _explorer.Mode != ExplorerMode.Walk,
                                                      _explorer.Active.Vehicle.LastLandingSpeedMps, out kind))
            {
                _haptics.Play(kind);
            }

            _screen.SetSpeed(_explorer.SpeedMps);
            _surfaceTimer -= dt;
            if (_surfaceTimer <= 0f)
            {
                _surfaceTimer = SurfaceInterval;
                UpdateSurface(position);
            }
            _placeTimer -= dt;
            if (_placeTimer <= 0f)
            {
                _placeTimer = PlaceInterval;
                if (_namer != null && _namer.Update(_world.GroundQuery, position.X, position.Z)) _screen.SetPlace(_namer.Current);
            }
            UpdateRoute(dt, position);
            _screen.SetDebugClock(_world.TimeOfDayHours);
        }

        private void LateUpdate()
        {
            if (_phase != Phase.Playing || _explorer == null) return;
            Vector3 ground;
            float heading, speed, lean;
            _explorer.GetCameraTarget(out ground, out heading, out speed, out lean);
            var target = new CameraTarget
            {
                Ground = ground, HeadingRad = heading, SpeedMps = speed, LeanRad = lean, Rig = _explorer.Rig, PassengerOf = _explorer.PassengerRig,
            };
            target.HasMount = _explorer.TryGetCameraMount(out target.Mount);
            _rig.Obstacles = ViewObstacles();
            _rig.Tick(Time.deltaTime, target, _frame, _world.Ground, _world.Origin);
            // The driver's eye inside a closed body: the vehicle shows its stand-in cockpit (no outline hull around us).
            bool eyeView = _rig.Mounted && CameraViews.Spec(_rig.View).Kind == CameraViewKind.Eye;
            _explorer.UpdateCockpit(eyeView, _rig.Camera.transform.position);
            _screen.SetHeading(_rig.YawRad);
        }

        private void OnApplicationPause(bool paused)
        {
            // Back from the home screen into a paused game, not into traffic.
            if (paused && _phase == Phase.Playing && _screen != null) _screen.Pause();
        }

        private void OnDestroy()
        {
            // Leaving play mode or the scene: release what we hold, without forcing an unload while Unity tears down.
            Close(false);
        }

        /// <summary>The touch controls as a <see cref="ControlFrame"/> (the device is Touch when a finger is on them).</summary>
        public static ControlFrame ControlsFromTouch(in TouchState t)
        {
            return new ControlFrame
            {
                MoveX = t.MoveX,
                MoveY = t.MoveY,
                Throttle = t.Throttle,
                Reverse = t.Reverse,
                ZoomSteps = t.ZoomSteps,
                LookYawDeg = t.LookYawDeg,
                LookPitchDeg = t.LookPitchDeg,
                Looking = t.Looking,
                ToggleMode = t.ActionPressed,
                ActionHeld = t.ActionHeld,
                Horn = t.Horn,
                Namaste = t.Namaste,
                Bell = t.Bell,
                RidePassenger = t.Passenger,
                Device = t.Active || t.Touched ? ControlDevice.Touch : ControlDevice.None,
            };
        }

        private void UpdateSurface(WorldPos position)
        {
            ArcadeVehicle vehicle = _explorer.Active.Vehicle;
            if (!vehicle.HasGround) return;
            GroundSample g = vehicle.Ground;
            bool inferred = false;
            if (g.OnRoad)
            {
                RoadHit hit;
                if (_world.GroundQuery.TryNearestRoad(position.X, position.Z, g.RoadHalfWidthM + 1.0, out hit) && hit.Road != null)
                    inferred = HudFormat.IsInferred(hit.Road.SurfaceSource);
            }
            _screen.SetSurface(vehicle.Surface, g.OnRoad, g.RoadSurface, inferred);
        }

        private void OnOriginShifted(WorldPos delta)
        {
            if (_explorer != null) _explorer.ShiftOrigin(delta);
            _rig.ShiftOrigin(delta);
            _lastListener -= new Vector3((float)delta.X, delta.Y, (float)delta.Z);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Actions

        private void OnMenuRequested()
        {
            Action handler = ExitRequested;
            if (handler != null) handler();
        }

        private void ToggleMode()
        {
            if (_explorer == null || _phase != Phase.Playing) return;
            _explorer.ToggleMode();
        }

        private void OnGarageRequested()
        {
            _garagePressed = true;
        }

        private void OnCameraRequested()
        {
            _cameraPressed = true;
        }

        /// <summary>
        /// C, the right-stick press or the HUD camera button: the next camera angle of the class the explorer is in
        /// (walking, two-wheeler, car, bus...), remembered per class in the save, named in a short toast.
        /// </summary>
        private void CycleCamera()
        {
            if (_explorer == null || _views == null) return;
            RigClass rig = _explorer.Rig;
            CameraView view = _views.Cycle(rig);
            _views.Store(_save);
            if (_persist != null) _persist();
            _screen.ShowToast(_localizer.Format("hud.camera.toast", _localizer.Get(CameraViews.Key(view))), "gh-toast__icon--star", 1.6f);
            _haptics.Play(HapticKind.Selection);
        }

        /// <summary>
        /// The world's solid geometry for the camera boom (Track COLLIDE implements <see cref="IViewObstacleQuery"/> over
        /// structure colliders, decks and terrain): the ground query when it implements it; null otherwise (the camera
        /// then only keeps above the ground).
        /// </summary>
        private IViewObstacleQuery ViewObstacles()
        {
            if (_world == null) return null;
            object query = _world.GroundQuery;
            return query as IViewObstacleQuery ?? _world.Ground as IViewObstacleQuery;
        }

        private void OnModeChanged(ExplorerMode mode)
        {
            if (_screen == null || _explorer == null) return;
            _screen.SetLayout(_explorer.Layout);
            _haptics.Play(mode == ExplorerMode.Walk ? HapticKind.LightImpact : HapticKind.MediumImpact);
        }

        private void OnNotice(string key)
        {
            if (_screen == null || string.IsNullOrEmpty(key)) return;
            string icon = key.StartsWith("hud.garage", StringComparison.Ordinal) ? "gh-toast__icon--scooter" : "gh-toast__icon--star";
            _screen.ShowToast(_localizer.Get(key), icon, 2.4f);
        }

        private void OnSacredZone(SacredZone zone, bool entered)
        {
            if (!entered || _screen == null) return;
            string key = CalmMode.CardKey(zone.Rule) ?? "sacred.entered";
            _screen.ShowToast(_localizer.Get(key), "gh-toast__icon--star", 4f);
        }

        // -------------------------------------------------------------------------------------------------------------
        // The world around the explorer (W2)

        private void OnDetailShown(TileId id, TileExtras extras)
        {
            if (extras != null) _detailTiles[id] = extras;
        }

        private void OnDetailHidden(TileId id)
        {
            _detailTiles.Remove(id);
        }

        /// <summary>
        /// A community-fleet vehicle was borrowed (its parked copy is hidden) or went home (it shows again). The shared
        /// TileInstances are never changed: the hidden ids live in <see cref="_hiddenParked"/>, which the traffic
        /// presenter and <see cref="GatherParked"/> consult, so hiding and re-showing (or rebuilding) a tile meanwhile
        /// neither loses the parked copy nor shows it twice.
        /// </summary>
        private void OnFleetVehicle(uint id, bool borrowed)
        {
            TrafficPresenter traffic = _world != null ? _world.GetComponent<TrafficPresenter>() : null;
            if (traffic != null) traffic.HiddenParked = _hiddenParked;
            if (borrowed)
            {
                if (_hiddenParked.Add(id)) OnNotice("hud.fleet_borrowed");
                return;
            }
            _hiddenParked.Remove(id);
        }

        /// <summary>What the explorer needs from the world this frame (no allocation).</summary>
        private ExplorerContext BuildContext(float dt)
        {
            WorldPos at = _explorer.Position;
            _parkedTimer -= dt;
            if (_parkedTimer <= 0f)
            {
                _parkedTimer = 0.25f;
                GatherParked(at.X, at.Z);
            }
            LifeHost life = _world.Life;
            Camera cam = _rig.Camera;
            return new ExplorerContext
            {
                Zones = _world.Zones,
                Traffic = life != null ? life.Vehicles : null,
                TrafficCount = life != null ? life.VehicleCount : 0,
                Parked = _parked,
                ParkedCount = _parkedCount,
                NearestCowM = _cowDistance,
                GameHour = _world.TimeOfDayHours,
                Urban = _world.AreaTypes == null || _world.AreaTypes.At(at.X, at.Z) != AreaType.Rural,
                Sound = _world.Sound,
                CameraPosition = cam != null ? cam.transform.position : Vector3.zero,
                CameraForward = cam != null ? cam.transform.forward : Vector3.forward,
                Portrait = cam != null && cam.aspect < 1f,
            };
        }

        private void GatherParked(double x, double z)
        {
            _parkedCount = 0;
            foreach (KeyValuePair<TileId, TileExtras> kv in _detailTiles)
            {
                TileInstances inst = kv.Value.Instances;
                if (inst == null || inst.Parked.Count == 0) continue;
                TileId id = kv.Key;
                if (x < id.X0 - FleetSearchM || x > id.X0 + id.Size + FleetSearchM || z < id.Z0 - FleetSearchM || z > id.Z0 + id.Size + FleetSearchM)
                    continue;
                for (int i = 0; i < inst.Parked.Count && _parkedCount < _parked.Length; i++)
                {
                    ParkedVehicle p = inst.Parked[i];
                    if (!p.CommunityFleet || _hiddenParked.Contains(p.Id)) continue;
                    double px = id.X0 + p.X, pz = id.Z0 + p.Z;
                    double dx = px - x, dz = pz - z;
                    if (dx * dx + dz * dz > FleetSearchM * FleetSearchM) continue;
                    _parked[_parkedCount++] = new ParkedSpot
                    {
                        X = px, Z = pz, Y = p.Y, YawDeg = p.YawDeg, Variant = p.Variant, Livery = p.Livery, Id = p.Id, Fleet = true,
                    };
                }
            }
        }

        /// <summary>
        /// The player in the sims (only through LifeHost.Post): a traffic obstacle where the player stands or drives, the
        /// pedestrians' hop-aside capsule ahead of the player's vehicle, a hailed taxi's pull-over point, and the
        /// nearest-cow query for the cushion (read back next frame).
        /// </summary>
        private void PostToLife()
        {
            LifeHost life = _world.Life;
            if (life == null) return;
            _postTimer -= Time.deltaTime;
            int wantPull;
            double unusedX, unusedZ;
            _explorer.TryGetPullOver(out wantPull, out unusedX, out unusedZ);
            if (_postTimer > 0f && wantPull == _postedPull) return;
            _postTimer = PostIntervalS;
            _postedPull = wantPull;
            WorldPos at = _explorer.Position;
            _vehicleBody = _explorer.TryGetVehicleBody(out _obstacleX, out _obstacleZ, out _obstacleHeading, out _obstacleSpeed, out _obstacleWidth,
                                                       out _obstacleFront);
            if (!_vehicleBody)
            {
                _obstacleX = at.X;
                _obstacleZ = at.Z;
            }
            _obstacleRadius = _vehicleBody ? Mathf.Max(1f, 0.5f * _obstacleFront) : 0.6f;
            int agent;
            double px, pz;
            if (_explorer.TryGetPullOver(out agent, out px, out pz))
            {
                _pullAgent = agent;
                _pullX = px;
                _pullZ = pz;
            }
            else
            {
                _pullAgent = -1;
            }
            _cowX = at.X;
            _cowZ = at.Z;
            _postPassenger = _explorer.Mode == ExplorerMode.Passenger;
            life.Post(_postPlayer);
        }

        /// <summary>Runs on the life worker at the start of its next step.</summary>
        private void PostPlayer(TrafficSim traffic, PedestrianSim peds, AnimalSim animals)
        {
            // Worker thread: only plain fields written by PostToLife (never Unity objects).
            if (!_postPassenger) traffic.AddObstacle(PlayerObstacleId, _obstacleX, _obstacleZ, _obstacleRadius, ObstacleKind.Player);
            else traffic.RemoveObstacle(PlayerObstacleId);
            peds.SetPlayerVehicle(_vehicleBody, _obstacleX, _obstacleZ, _obstacleHeading, _obstacleSpeed, _obstacleWidth, _obstacleFront);
            if (_pullAgent >= 0) traffic.AddObstacle(PullOverObstacleId, _pullX, _pullZ, 1.2f, ObstacleKind.Player);
            else traffic.RemoveObstacle(PullOverObstacleId);
            float d;
            _cowDistance = animals.TryNearestCow(_cowX, _cowZ, out d) ? d : float.PositiveInfinity;
        }

        /// <summary>The HUD follows the explorer: layout, the Action button, the prompt chip, the passenger button.</summary>
        private void UpdateHud()
        {
            ControlLayout layout = _explorer.Layout;
            if (layout != _screen.Layout) _screen.SetLayout(layout);
            PlayerVehicleHorn();
            switch (_explorer.Prompt)
            {
                case ExplorerPrompt.HopOn:
                    _screen.SetAction("hud.action.hop_on", "hop", true);
                    _screen.SetPrompt("hud.prompt.hop_on", VehicleNameKey(_explorer.PromptRig));
                    break;
                case ExplorerPrompt.HopOff:
                    _screen.SetAction("hud.action.hop_off", "off", false);
                    _screen.SetPrompt(null);
                    break;
                case ExplorerPrompt.VehiclesRestOutside:
                    if (_explorer.Mode == ExplorerMode.Ride) _screen.SetAction("hud.action.hop_off", "off", false);
                    // On foot in a compound a tap rings a shrine bell (the only touch path to it), not a jump.
                    else if (_explorer.Calm.Active) _screen.SetAction("hud.action.ring_bell", null, false);
                    else _screen.SetAction("hud.action.jump", null, false);
                    _screen.SetPrompt("hud.prompt.rest_outside");
                    break;
                case ExplorerPrompt.TaxiComing:
                    _screen.SetAction("hud.action.jump", null, false);
                    _screen.SetPrompt("hud.prompt.taxi_coming");
                    break;
                case ExplorerPrompt.StopRequested:
                    _screen.SetAction("hud.action.stop", "off", false);
                    _screen.SetPrompt("hud.prompt.stop_requested");
                    break;
                case ExplorerPrompt.RideAlong:
                    _screen.SetAction("hud.action.stop", "off", false);
                    _screen.SetPrompt("hud.prompt.ride_along");
                    break;
                default:
                    _screen.SetAction("hud.action.jump", null, false);
                    _screen.SetPrompt(null);
                    break;
            }
            _screen.SetPassengerOffered(_explorer.PassengerOffered && _explorer.Mode == ExplorerMode.Walk);
        }

        private void PlayerVehicleHorn()
        {
            Characters.Rides.PlayerVehicle v = _explorer.CurrentVehicle;
            bool bell = v != null && VehicleRoles.HornOf(v.Entry) == Core.Synth.HornKind.BicycleBell;
            _screen.SetHornLabel(bell ? "hud.bell" : "hud.horn");
        }

        private static string VehicleNameKey(RigClass rig)
        {
            switch (rig)
            {
                case RigClass.Bicycle: return "vehicle.bicycle";
                case RigClass.TwoWheeler: return "vehicle.two_wheeler";
                case RigClass.Van: return "vehicle.van";
                case RigClass.Bus: return "vehicle.bus";
                case RigClass.Truck: return "vehicle.truck";
                case RigClass.Tractor: return "vehicle.tractor";
                case RigClass.Passenger: return "vehicle.passenger";
                default: return "vehicle.car";
            }
        }

        /// <summary>The audio listener rides with the camera and hears from the player's head (Track C2's director).</summary>
        private void UpdateListener(float dt, WorldPos position)
        {
            if (!AudioDirector.Exists) return;
            AudioDirector audio = AudioDirector.Instance;
            Camera cam = _rig.Camera;
            if (!_listenerAttached && cam != null)
            {
                audio.AttachListener(cam.transform, _explorer.Head);
                _listenerAttached = true;
            }
            Vector3 scene = _explorer.ToScene(position.X, position.Y, position.Z);
            Vector3 velocity = dt > 0f ? (scene - _lastListener) / dt : Vector3.zero;
            if (velocity.sqrMagnitude > 2500f) velocity = Vector3.zero; // a teleport or origin shift
            _lastListener = scene;
            audio.SetListenerVelocity(velocity);
            audio.SetListenerGamePosition(position.X, position.Z, position.Y);
            bool driving = _explorer.Mode == ExplorerMode.Ride;
            Characters.Rides.PlayerVehicle v = _explorer.CurrentVehicle;
            bool interior = _explorer.Mode == ExplorerMode.Passenger ||
                            v != null && !VehicleRoles.IsTwoWheeler(v.Entry.Shape) && v.Entry.Shape != BodyShape.Tractor;
            audio.SetPlayerState(driving, interior);
        }

        private void OnTimeOfDayChanged(float hours)
        {
            if (_world != null) _world.TimeOfDayHours = hours;
        }

        private void RideTo(SearchEntry entry)
        {
            if (entry == null || _phase != Phase.Playing) return;
            if (_planner == null)
            {
                _screen.ShowToast(_localizer.Get("hud.route.unavailable"), "gh-toast__icon--map");
                return;
            }
            _destination = entry;
            _guide = null;
            _routeProfile = CurrentProfile();
            _profileSettle = 0f;
            _world.ClearRoute();
            // The name record, not a string: the banner and the arrival toast follow a language switch mid-route.
            _screen.ShowRoute(entry.Name, "hud.route.finding");
            PlanRoute();
        }

        private async void PlanRoute()
        {
            if (_destination == null || _planner == null || _explorer == null) return;
            int token = ++_routeToken;
            _planning = true;
            WorldPos from = _explorer.Position;
            double tx = _destination.X, tz = _destination.Z;
            RoutePlanner planner = _planner;
            Travel profile = _routeProfile;
            PlannedRoute route = null;
            try
            {
                route = await Task.Run(() => planner.Plan(from.X, from.Z, tx, tz, profile));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (this == null || token != _routeToken || _phase != Phase.Playing) return;
            _planning = false;
            if (route == null)
            {
                _screen.ShowToast(_localizer.Format("hud.route.none", DestinationName()), "gh-toast__icon--map");
                CancelRoute();
                return;
            }
            bool fresh = _guide == null;
            _guide = new RouteGuide(route);
            _world.ShowRoute(route.Polyline);
            _rerouteCooldown = RerouteCooldownS;
            _haptics.Play(HapticKind.Selection);
            if (fresh && profile == Travel.Car) _screen.ShowToast(_localizer.Get("hud.route.car_roads"), "gh-toast__icon--map", 2.6f);
            Debug.Log("ExploreSession: " + TravelProfiles.NameOf(profile) + " route to " + DestinationName() + ": " +
                      (route.LengthM / 1000.0).ToString("0.0") + " km, " + (route.TimeS / 60.0).ToString("0") + " min, " +
                      route.Polyline.Length / 2 + " points.");
        }

        private void UpdateRoute(float dt, WorldPos position)
        {
            if (_destination == null) return;
            // A new vehicle (or none): plan again with its profile once it has settled, so a car never keeps a route
            // through lanes it cannot fit, and a scooter gets its shortcuts back.
            Travel want = CurrentProfile();
            if (want != _routeProfile)
            {
                _profileSettle += dt;
                if (_profileSettle >= ProfileSettleS && !_planning)
                {
                    _routeProfile = want;
                    _profileSettle = 0f;
                    _guide = null;
                    _world.ClearRoute();
                    _screen.ShowRoute(_destination.Name, "hud.route.rerouting");
                    // A car says why its route differs when it is found (hud.route.car_roads); other vehicles name themselves.
                    RigClass rig = _explorer.Rig;
                    if (want != Travel.Car && rig != RigClass.Walk && rig != RigClass.Passenger)
                    {
                        _screen.ShowToast(_localizer.Format("hud.route.replan", _localizer.Get(VehicleNameKey(rig))), "gh-toast__icon--map", 2f);
                    }
                    PlanRoute();
                    return;
                }
            }
            else
            {
                _profileSettle = 0f;
            }
            if (_guide == null) return;
            _rerouteCooldown -= dt;
            RouteStatus status = _guide.Update(position.X, position.Z, _screen.BlocksGameplay ? 0f : dt);
            if (status == RouteStatus.Arrived)
            {
                NameRecord name = _destination.Name;
                CancelRoute();
                _screen.CelebrateArrival(name);
                return;
            }
            if (status == RouteStatus.OffRoute && !_planning && _rerouteCooldown <= 0f)
            {
                _screen.ShowRoute(_destination.Name, "hud.route.rerouting");
                PlanRoute();
                return;
            }
            if (_planning) return;
            // The chip: the next turn's arrow and distance (or straight on), or the way back while off the route.
            float turnRad;
            double turnM;
            bool turn = _guide.NextTurn(out turnRad, out turnM);
            bool off = _guide.DistanceToRouteM > OffRouteChipM;
            RouteStepKind step = off ? RouteStepKind.BackToRoute : turn ? RouteStepKind.Turn : RouteStepKind.Straight;
            float arrow = off ? _guide.RelativeBearing(_rig.YawRad) : turn ? turnRad : 0f;
            _screen.SetRouteProgress(_guide.RemainingM, _guide.EtaSeconds, arrow);
            _screen.SetRouteStep(step, turnM);
        }

        /// <summary>The routing profile for how the explorer travels now (<see cref="RoutePlanner.ProfileFor"/>).</summary>
        private Travel CurrentProfile()
        {
            return _explorer != null ? RoutePlanner.ProfileFor(_explorer.Rig, _explorer.PassengerRig) : Travel.Motorbike;
        }

        /// <summary>The destination's name in the current language ("" without one).</summary>
        private string DestinationName()
        {
            return _destination != null ? _destination.Name.Display(_localizer.Locale == Localizer.Nepali) : "";
        }

        private void CancelRoute()
        {
            _routeToken++;
            _planning = false;
            _guide = null;
            _destination = null;
            if (_world != null) _world.ClearRoute();
            if (_screen != null) _screen.HideRoute();
        }

        /// <summary>Development builds and the editor: jump to a search hit (snapped onto the nearest road).</summary>
        private void TeleportTo(SearchEntry entry)
        {
            if (entry == null || _phase != Phase.Playing) return;
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            SpawnPoint spawn;
            if (!ExploreSpawn.TryRoadSpawn(_world.Routes, _planner != null ? _planner.Starts : null, entry.X, entry.Z, out spawn))
            {
                spawn = new SpawnPoint { X = entry.X, Z = entry.Z, HeadingRad = _explorer.Pose.HeadingRad, HeightHint = _explorer.Position.Y };
            }
            CancelRoute();
            _world.Teleport(new WorldPos(spawn.X, spawn.HeightHint, spawn.Z));
            _explorer.Teleport(spawn.X, spawn.Z, spawn.HeadingRad, spawn.HeightHint);
            _rig.Snap();
            if (_namer != null) _namer.Reset();
            _placeTimer = 0f;
            _screen.SetPlace(null);
            string name = entry.Name.Display(_localizer.Locale == Localizer.Nepali);
            _screen.ShowToast(_localizer.Format("hud.teleporting", name), "gh-toast__icon--star", 2f);
            _haptics.Play(HapticKind.MediumImpact);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Helpers

        /// <summary>The explorer's material: a copy of the world's ToonLit building material (Project Setup's asset), else
        /// a new one from the shader.</summary>
        private static Material CreateToonMaterial()
        {
            var set = Resources.Load<WorldMaterialSet>(WorldMaterialSet.ResourcePath);
            if (set != null && set.buildings != null)
            {
                return new Material(set.buildings) { name = "Ghumante Explorer" };
            }
            Shader shader = Shader.Find(WorldShaders.ToonLit);
            if (shader == null)
            {
                Debug.LogError("ExploreSession: shader " + WorldShaders.ToonLit + " not found; the explorer falls back to URP Lit " +
                               "(no vertex colours). Run Ghumante > Project Setup.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            var material = new Material(shader) { name = "Ghumante Explorer" };
            if (material.HasProperty(WorldShaders.BaseColor)) material.SetColor(WorldShaders.BaseColor, Color.white);
            return material;
        }
    }
}
