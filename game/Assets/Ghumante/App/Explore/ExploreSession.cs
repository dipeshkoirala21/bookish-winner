using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghumante.Characters;
using Ghumante.Characters.Cameras;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Search;
using Ghumante.Core.Services;
using Ghumante.Platform;
using Ghumante.Platform.Regions;
using Ghumante.UI.Hud;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using Ghumante.UI.Screens;
using Ghumante.World;
using Ghumante.World.Rendering;
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
    /// (<see cref="ExploreSpawn"/>); wait until the ground there has streamed in, then hop on.</item>
    /// <item><b>Play</b>: every frame the merged controls (keyboard and gamepad through <see cref="ExplorerInput"/>, touch
    /// through the HUD) drive the <see cref="ExplorerController"/>, the world streams around it (its focus), the
    /// <see cref="ChaseCameraRig"/> follows, haptics answer bumps, surfaces and recoveries, and the HUD shows speed,
    /// surface, place, compass and the route. "Ride there" plans with Core's A* (motorbike) and draws the world's route
    /// ribbon; arriving within about 40 m celebrates.</item>
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

        private enum Phase
        {
            Idle,
            Opening,
            Spawning,
            Playing,
            Failed,
            Closed,
        }

        private ExploreScreen _screen;
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
        private string _destinationName;
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
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            var go = new GameObject("Explore Session");
            ExploreSession session = go.AddComponent<ExploreSession>();
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
            if (_explorer != null)
            {
                _explorer.ModeChanged -= OnModeChanged;
                Destroy(_explorer.gameObject);
                _explorer = null;
            }
            if (_world != null)
            {
                _world.OriginShifted -= OnOriginShifted;
                _world.Close();
                _world = null;
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
            _screen.RouteCancelRequested += CancelRoute;
            _screen.RideToRequested += RideTo;
            _screen.TeleportRequested += TeleportTo;
            _screen.TimeOfDayChanged += OnTimeOfDayChanged;
        }

        private void Unwire()
        {
            _screen.MenuRequested -= OnMenuRequested;
            _screen.ModeToggleRequested -= ToggleMode;
            _screen.RouteCancelRequested -= CancelRoute;
            _screen.RideToRequested -= RideTo;
            _screen.TeleportRequested -= TeleportTo;
            _screen.TimeOfDayChanged -= OnTimeOfDayChanged;
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
                _screen.ShowLoading("explore.loading.opening", ExploreRegions.Label(regionId));
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
            _toonMaterial = CreateToonMaterial();
            _explorer = ExplorerController.Create(_toonMaterial);
            _explorer.Bind(_world.GroundQuery, _world.Origin);
            _explorer.ReducedMotion = _motion.ReduceMotion;
            _explorer.ModeChanged += OnModeChanged;
            _explorer.Spawn(_spawn.X, _spawn.Z, _spawn.HeadingRad, ExplorerMode.Ride, _spawn.HeightHint);

            Camera camera = Camera.main;
            if (camera == null)
            {
                _ownCamera = new GameObject("Explore Camera") { tag = "MainCamera" };
                camera = _ownCamera.AddComponent<Camera>();
            }
            _rig.Attach(camera, _world.Config);
            _rig.ReducedMotion = _motion.ReduceMotion;
            _rig.Snap();

            _input = new ExplorerInput { Enabled = true };
            _touchVisibility = new TouchControlsVisibility(ExplorerInput.TouchscreenPresent);
            _screen.SetTouchControlsVisible(_touchVisibility.Visible);
            _screen.Search.Engine = _world.Search;
            _screen.Search.TeleportAllowed = Application.isEditor || Debug.isDebugBuild;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _screen.EnableDebugTime(_world.TimeOfDayHours);
#endif
            _screen.SetMode(true);
            _screen.HideLoading();
            _phase = Phase.Playing;
            _placeTimer = 0f;
            _surfaceTimer = 0f;
            if (_spawn.Place != null)
            {
                string name = _spawn.Place.Display(_localizer.Locale == Localizer.Nepali);
                _screen.ShowToast(_localizer.Format("explore.spawn", name), "gh-toast__icon--scooter", 3.5f);
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
            if (!blocked)
            {
                if (frame.ToggleMode) ToggleMode();
                if (frame.Search) _screen.OpenSearch();
                if (frame.Map) _screen.ShowToast(_localizer.Get("menu.map_soon"), "gh-toast__icon--map");
            }
            blocked = _screen.BlocksGameplay;
            if (blocked) frame = new ControlFrame { ZoomSteps = frame.ZoomSteps };
            _frame = frame;

            _explorer.ReducedMotion = _motion.ReduceMotion;
            _rig.ReducedMotion = _motion.ReduceMotion;
            _explorer.Tick(dt, frame, _rig.YawRad, blocked);
            WorldPos position = _explorer.Position;
            _world.Focus = position;
            _screen.Search.SetPlayerPosition(position.X, position.Z);

            HapticKind kind;
            if (!blocked && ExploreFeedback.HapticFor(_explorer.LastEvents, _explorer.Mode == ExplorerMode.Ride,
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
            Vector3 target;
            float heading, speed, lean;
            _explorer.GetCameraTarget(out target, out heading, out speed, out lean);
            _rig.Tick(Time.deltaTime, target, heading, speed, lean, _explorer.Mode == ExplorerMode.Ride && !_explorer.IsHopping,
                      _frame, _world.Ground, _world.Origin);
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
            if (_explorer.ToggleMode() && _explorer.DismountPending)
                _screen.ShowToast(_localizer.Get("hud.slow_down"), "gh-toast__icon--scooter", 1.6f);
        }

        private void OnModeChanged(ExplorerMode mode)
        {
            if (_screen != null) _screen.SetMode(mode == ExplorerMode.Ride);
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
            _destinationName = entry.Name.Display(_localizer.Locale == Localizer.Nepali);
            _guide = null;
            _world.ClearRoute();
            _screen.ShowRoute(_destinationName, "hud.route.finding");
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
            PlannedRoute route = null;
            try
            {
                route = await Task.Run(() => planner.Plan(from.X, from.Z, tx, tz));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (this == null || token != _routeToken || _phase != Phase.Playing) return;
            _planning = false;
            if (route == null)
            {
                _screen.ShowToast(_localizer.Format("hud.route.none", _destinationName), "gh-toast__icon--map");
                CancelRoute();
                return;
            }
            _guide = new RouteGuide(route);
            _world.ShowRoute(route.Polyline);
            _rerouteCooldown = RerouteCooldownS;
            _haptics.Play(HapticKind.Selection);
            Debug.Log("ExploreSession: route to " + _destinationName + ": " + (route.LengthM / 1000.0).ToString("0.0") + " km, " +
                      (route.TimeS / 60.0).ToString("0") + " min, " + route.Polyline.Length / 2 + " points.");
        }

        private void UpdateRoute(float dt, WorldPos position)
        {
            if (_guide == null) return;
            _rerouteCooldown -= dt;
            RouteStatus status = _guide.Update(position.X, position.Z, _screen.BlocksGameplay ? 0f : dt);
            if (status == RouteStatus.Arrived)
            {
                string name = _destinationName;
                CancelRoute();
                _screen.CelebrateArrival(name);
                return;
            }
            if (status == RouteStatus.OffRoute && !_planning && _rerouteCooldown <= 0f)
            {
                _screen.ShowRoute(_destinationName, "hud.route.rerouting");
                PlanRoute();
                return;
            }
            if (!_planning) _screen.SetRouteProgress(_guide.RemainingM, _guide.EtaSeconds, _guide.RelativeBearing(_rig.YawRad));
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
