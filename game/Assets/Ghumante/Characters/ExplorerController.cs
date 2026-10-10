using System;
using System.Collections.Generic;
using Ghumante.Characters.Avatar;
using Ghumante.Characters.Cameras;
using Ghumante.Characters.Rides;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Synth;
using Ghumante.Core.Traffic;
using Ghumante.Vehicles;
using Ghumante.Vehicles.Visuals;
using UnityEngine;

namespace Ghumante.Characters
{
    /// <summary>How the explorer gets around.</summary>
    public enum ExplorerMode : byte
    {
        Walk = 0,

        /// <summary>Driving or riding a vehicle the player controls (garage or community fleet).</summary>
        Ride = 1,

        /// <summary>Riding along in a taxi, bus, micro, tempo or rickshaw (W2-O5).</summary>
        Passenger = 2,
    }

    /// <summary>What the Action button and the prompt chip say (the HUD maps each to a localised text and icon).</summary>
    public enum ExplorerPrompt : byte
    {
        None = 0,

        /// <summary>Nothing to hop on: the Action button jumps.</summary>
        Jump = 1,

        /// <summary>A seat is offered: Hop on (the class icon from <see cref="ExplorerController.PromptRig"/>).</summary>
        HopOn = 2,

        /// <summary>In a vehicle: Hop off.</summary>
        HopOff = 3,

        /// <summary>In a sacred zone, or driving up to one: "Vehicles rest outside".</summary>
        VehiclesRestOutside = 4,

        /// <summary>A hailed taxi is coming.</summary>
        TaxiComing = 5,

        /// <summary>Riding along: the bell was rung, getting off at the next stop.</summary>
        StopRequested = 6,

        /// <summary>Riding along: Hop off asks the driver to stop (the bell).</summary>
        RideAlong = 7,
    }

    /// <summary>A parked vehicle near the player (community fleet), in game metres; filled by the session.</summary>
    public struct ParkedSpot
    {
        public double X, Z;
        public float Y, YawDeg;
        public int Variant;
        public byte Livery;
        public uint Id;
        public bool Fleet;
    }

    /// <summary>What the world tells the controller each frame (filled by the session from WorldRoot and LifeHost).</summary>
    public struct ExplorerContext
    {
        /// <summary>Sacred and compound zones (calm mode, "vehicles rest outside").</summary>
        public SacredZoneIndex Zones;

        /// <summary>The traffic snapshot (taxis, buses and micros to ride along in).</summary>
        public AgentPose[] Traffic;

        public int TrafficCount;

        /// <summary>Parked vehicles near the player (community fleet ones can be borrowed).</summary>
        public ParkedSpot[] Parked;

        public int ParkedCount;

        /// <summary>Distance to the nearest cow (∞ when none): the cushion slows the player's vehicle (ContactRules).</summary>
        public float NearestCowM;

        public float GameHour;
        public bool Urban;

        public ISoundService Sound;

        /// <summary>The camera in scene space (a summoned vehicle appears out of its view).</summary>
        public Vector3 CameraPosition, CameraForward;

        public bool Portrait;
    }

    /// <summary>
    /// The player in Explore (W2_DESIGN 6, M1 track D): a detailed cartoon character (<see cref="PlayerAvatar"/> from
    /// Core's <see cref="HumanoidMesher"/>, posed each frame by <see cref="CharacterPoser"/>) who walks, runs, jumps,
    /// namastes and fidgets, and hops on and off every class of vehicle without theft:
    /// <list type="bullet">
    /// <item><b>Own garage</b> (<see cref="Garage"/>): the whistle summons the selected vehicle, which drives itself up
    /// from out of view (≤ 20 s).</item>
    /// <item><b>Community fleet</b>: parked vehicles with the green key tag can be borrowed; they go home on their own
    /// (<see cref="FleetLoan"/>).</item>
    /// <item><b>Passenger</b>: taxis (hail by holding Action), buses, micros and tempos at stops, rickshaws; the bell
    /// asks for the next stop.</item>
    /// </list>
    /// Enter and exit follow W2_DESIGN 6.2: the offered seat comes from <see cref="MountDetector"/>, the walk and the side
    /// from <see cref="MountPlan"/> (right-hand drive, the other side when a wall is near), the clip lasts the class's
    /// time (bicycle 0.5 s … truck 1.4 s), move input in its first 60% cancels it, exit brakes first and lands at
    /// <see cref="ExitPlacement"/> (never inside geometry). Helmets pop on for two-wheelers. Handling is per class
    /// (<see cref="VehicleCatalogEntry.Spec"/>), the camera rig per class (<see cref="Rig"/>). Calm mode inside sacred
    /// zones caps the pace and quiets the fidgets; vehicles rest outside them. Footsteps follow the ground's surface,
    /// engines run through Track C2's sound service. The owner calls <see cref="Tick"/> once per frame.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ExplorerController : MonoBehaviour
    {
        /// <summary>Hold Action this long to ride as a passenger (W2_DESIGN 6.2) ...</summary>
        public const float PassengerHoldS = 0.35f;

        /// <summary>... or, with nothing offered, to hail a taxi (6.2).</summary>
        public const float HailHoldS = 0.4f;

        /// <summary>The helmet stays on this long after getting off a two-wheeler, then pops off.</summary>
        public const float HelmetOffDelayS = 1.5f;

        /// <summary>Moving input in the first 60% of an enter cancels it.</summary>
        public const float CancelFraction = 0.6f;

        /// <summary>A summoned vehicle that has not arrived by then is placed beside the player.</summary>
        public const float SummonTimeoutS = 20f;

        private const float HopHeightM = 0.35f;

        private enum Phase : byte
        {
            Free,
            Approach,
            Enter,
            Exit,
        }

        private struct Transition
        {
            public Phase Phase;
            public float T, Duration, BlockedS;
            public PlayerVehicle Vehicle;
            public int AgentId;
            public bool Passenger;
            public SeatSocket Seat;
            public SeatPose Pose;
            public double FromX, FromZ, ToX, ToZ;
            public float FromY, ToY, FromHeading, ToHeading;
            public float BestDistance;
        }

        private FixedStepDriver _walker;
        private PlayerAvatar _avatar;
        private CharacterPoser _poser;
        private readonly JumpModel _jump = new JumpModel();
        private IdleFidgets _fidgets;
        private readonly CalmMode _calm = new CalmMode();
        private readonly MountDetector _detector = new MountDetector();
        private readonly PassengerRide _ride = new PassengerRide();
        private readonly List<PlayerVehicle> _vehicles = new List<PlayerVehicle>();
        private VehicleMeshCache _cache;
        private Material _material;
        private PuffEmitter _puffs;
        private IGroundQuery _ground;
        private WorldPos _origin;
        private ExplorerMode _mode = ExplorerMode.Walk;
        private PlayerVehicle _current;
        private Transition _tr;
        private Garage _garage;
        private bool _exitPending;
        private float _actionHeld;
        private bool _holdConsumed;
        private Emote _emote;
        private float _emoteTime;
        private float _helmetOff;
        private float _hornHeld;
        private float _zoneBlockS;
        private bool _zoneAhead;
        private float _stillS;
        private float _lastSpeed;
        private StepEvents _events;
        private ExplorerContext _ctx;
        private long _nextId = 1;
        private AgentPose _ridePose;
        private bool _rideFound;
        private ISoundService _sound = NullSoundService.Instance;

        /// <summary>Builds the explorer as a new scene object. <paramref name="material"/> is a <c>Ghumante/ToonLit</c>
        /// material the caller owns; <paramref name="cache"/> builds the vehicle meshes.</summary>
        public static ExplorerController Create(Material material, VehicleMeshCache cache, CharacterRecipe recipe, Garage garage)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            var go = new GameObject("Explorer");
            ExplorerController c = go.AddComponent<ExplorerController>();
            c._material = material;
            c._cache = cache ?? new VehicleMeshCache();
            c._garage = garage ?? Garage.Starter(1u);
            recipe = recipe ?? CharacterRecipe.NewPlayer(1u);
            c._walker = new FixedStepDriver(new ArcadeVehicle(VehicleTuning.Walker()), FixedStepDriver.DefaultStepS, 0.1f);
            c._avatar = PlayerAvatar.Create(go.transform, material, recipe);
            c._poser = new CharacterPoser(c._avatar.Skeleton);
            c._fidgets = new IdleFidgets(recipe.Seed ^ 0x5F1D6E7u);
            c._puffs = PuffEmitter.Create(go.transform, material, 24);
            return c;
        }

        /// <summary>Raised when the explorer got on (at the end of the enter clip) or off (at the start of the exit).</summary>
        public event Action<ExplorerMode> ModeChanged;

        /// <summary>A short message for the HUD (a localisation key), e.g. "hud.rest_outside".</summary>
        public event Action<string> Notice;

        /// <summary>A community-fleet vehicle was borrowed (true: hide its parked copy) or went home (false: show it).</summary>
        public event Action<uint, bool> FleetVehicle;

        /// <summary>The player entered (non-zero rule) or left a sacred zone: the HUD shows the entry-rule card.</summary>
        public event Action<SacredZone, bool> SacredZoneChanged;

        public ExplorerMode Mode
        {
            get { return _mode; }
        }

        public CharacterRecipe Recipe
        {
            get { return _avatar.Recipe; }
        }

        /// <summary>The avatar's head (the audio listener follows it).</summary>
        public Transform Head
        {
            get { return _avatar.Head; }
        }

        public Garage Garage
        {
            get { return _garage; }
        }

        /// <summary>True during an approach, enter or exit (move input cancels an approach or an early enter).</summary>
        public bool IsHopping
        {
            get { return _tr.Phase != Phase.Free; }
        }

        /// <summary>True while braking to get off.</summary>
        public bool DismountPending
        {
            get { return _exitPending; }
        }

        /// <summary>The body being controlled: the vehicle when driving, else the walker.</summary>
        public FixedStepDriver Active
        {
            get { return _mode == ExplorerMode.Ride && _current != null ? _current.Driver : _walker; }
        }

        public FixedStepDriver Walker
        {
            get { return _walker; }
        }

        /// <summary>The vehicle being driven (null on foot or as a passenger).</summary>
        public PlayerVehicle CurrentVehicle
        {
            get { return _mode == ExplorerMode.Ride ? _current : null; }
        }

        public StepEvents LastEvents
        {
            get { return _events; }
        }

        /// <summary>The ride-along state (passenger).</summary>
        public PassengerRide Ride
        {
            get { return _ride; }
        }

        public CalmMode Calm
        {
            get { return _calm; }
        }

        public bool ReducedMotion { get; set; }

        public float Wetness { get; set; }

        public WorldPos Origin
        {
            get { return _origin; }
        }

        /// <summary>The Action button's meaning now.</summary>
        public ExplorerPrompt Prompt { get; private set; }

        /// <summary>The class icon for <see cref="ExplorerPrompt.HopOn"/>.</summary>
        public RigClass PromptRig { get; private set; }

        /// <summary>A passenger seat is offered (the small "Ride as passenger" button shows).</summary>
        public bool PassengerOffered { get; private set; }

        /// <summary>The touch layout family for the HUD.</summary>
        public ControlLayout Layout
        {
            get
            {
                if (_mode == ExplorerMode.Passenger) return ControlLayout.Passenger;
                if (_mode == ExplorerMode.Ride && _current != null) return VehicleRoles.LayoutOf(_current.Entry);
                return ControlLayout.Walk;
            }
        }

        /// <summary>The camera rig for this frame.</summary>
        public RigClass Rig
        {
            get
            {
                if (_tr.Phase == Phase.Exit || _tr.Phase == Phase.Approach) return RigClass.Walk;
                if (_tr.Phase == Phase.Enter)
                {
                    if (_tr.Passenger) return RigClass.Passenger;
                    return _tr.Vehicle != null ? VehicleRoles.RigOf(_tr.Vehicle.Entry) : RigClass.Walk;
                }
                if (_mode == ExplorerMode.Ride && _current != null) return VehicleRoles.RigOf(_current.Entry);
                if (_mode == ExplorerMode.Passenger) return RigClass.Passenger;
                return RigClass.Walk;
            }
        }

        /// <summary>The driver rig of the vehicle ridden along (or being boarded) as a passenger; the passenger camera
        /// orbits at 1.2× it (W2_DESIGN 6.4). Car when not riding along.</summary>
        public RigClass PassengerRig
        {
            get
            {
                int variant = _mode == ExplorerMode.Passenger ? _ride.Variant
                    : _tr.Phase == Phase.Enter && _tr.Passenger && _tr.Vehicle == null ? _ridePose.Variant : -1;
                if (variant < 0 || variant >= VehicleCatalog.Count) return RigClass.Car;
                return VehicleRoles.RigOf(VehicleCatalog.At(variant));
            }
        }

        /// <summary>Where the explorer is now (game metres): the streaming focus.</summary>
        public WorldPos Position
        {
            get
            {
                double x, z;
                float y;
                PlayerPoint(out x, out y, out z);
                return new WorldPos(x, y, z);
            }
        }

        /// <summary>The interpolated pose of the controlled body (the vehicle when driving).</summary>
        public VehiclePose Pose
        {
            get { return Active.Interpolated; }
        }

        public float SpeedMps
        {
            get
            {
                if (_mode == ExplorerMode.Passenger) return _rideFound ? _ridePose.SpeedMps : 0f;
                return Active.Vehicle.SpeedMps;
            }
        }

        public GroundSample GroundUnder
        {
            get { return Active.Vehicle.Ground; }
        }

        public bool HasGround
        {
            get { return Active.Vehicle.HasGround; }
        }

        /// <summary>The player's vehicle as an obstacle for traffic and pedestrians (the session posts it to LifeHost):
        /// active while driving.</summary>
        public bool TryGetVehicleBody(out double x, out double z, out float headingRad, out float speedMps, out float widthM, out float frontM)
        {
            x = z = 0;
            headingRad = speedMps = widthM = frontM = 0f;
            if (_mode != ExplorerMode.Ride || _current == null) return false;
            VehiclePose p = _current.Driver.Current;
            x = p.X;
            z = p.Z;
            headingRad = p.HeadingRad;
            speedMps = p.SpeedMps;
            VehicleMesher.Dims d = VehicleMesher.DimsOf(_current.Entry);
            widthM = d.Width;
            frontM = d.Wheelbase + d.Front;
            return true;
        }

        /// <summary>A taxi asked to pull over (hail, or a passenger's stop): the session places a stop obstacle at
        /// (x, z) for agent <paramref name="agentId"/>; cleared when false.</summary>
        public bool TryGetPullOver(out int agentId, out double x, out double z)
        {
            agentId = -1;
            x = z = 0;
            bool wanted = _ride.State == RideState.Hailing || _ride.State == RideState.StopRequested && _ride.Class == VehicleClass.Taxi;
            if (!wanted || !_rideFound) return false;
            agentId = _ride.AgentId;
            float ahead = _ride.State == RideState.Hailing ? HailAhead() : 14f;
            PassengerRide.PullOverPoint(_ridePose, ahead, out x, out z);
            return true;
        }

        private float HailAhead()
        {
            // Stop the taxi level with the player (within 30 m), never behind it.
            double dx = Position.X - _ridePose.X, dz = Position.Z - _ridePose.Z;
            double along = dx * Math.Sin(_ridePose.HeadingRad) + dz * Math.Cos(_ridePose.HeadingRad);
            return (float)Math.Max(8.0, Math.Min(PassengerRide.PullOverM, along + 4.0));
        }

        public void Bind(IGroundQuery ground, WorldPos origin)
        {
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _origin = origin;
        }

        public void ShiftOrigin(WorldPos delta)
        {
            _origin = _origin + delta;
            if (_puffs != null) _puffs.ShiftOrigin(new Vector3((float)delta.X, delta.Y, (float)delta.Z));
        }

        public Vector3 ToScene(double x, float y, double z)
        {
            return new Vector3((float)(x - _origin.X), y - _origin.Y, (float)(z - _origin.Z));
        }

        /// <summary>
        /// Puts the explorer at (x, z) facing <paramref name="headingRad"/>. With <paramref name="mode"/> Ride the garage's
        /// selected vehicle is placed there with the player on it; on foot it stands parked beside the player.
        /// </summary>
        public void Spawn(double x, double z, float headingRad, ExplorerMode mode, float heightHint)
        {
            RequireBound();
            CancelTransition();
            _exitPending = false;
            _jump.Cancel();
            _ride.End();
            _puffs.Clear();
            GarageVehicle gv = _garage.Current;
            PlayerVehicle own = OwnVehicle();
            if (own == null)
            {
                own = new PlayerVehicle(_nextId++, gv.Variant, gv.Livery, gv.PlateSeed, VehicleSource.Garage, transform, _material, _cache, null);
                _vehicles.Add(own);
            }
            Place(own.Driver, x, z, headingRad, heightHint);
            double sx, sz;
            Side(x, z, headingRad, -(VehicleMesher.DimsOf(own.Entry).Half + 0.7), out sx, out sz);
            Place(_walker, sx, sz, headingRad, heightHint);
            if (mode == ExplorerMode.Ride)
            {
                _current = own;
                own.StartEngine(_sound);
                SetMode(ExplorerMode.Ride, true);
            }
            else
            {
                _current = null;
                SetMode(ExplorerMode.Walk, true);
            }
            Render(0f);
        }

        /// <summary>Moves the explorer to (x, z) (fast travel and the debug teleport); a passenger gets off first.</summary>
        public void Teleport(double x, double z, float headingRad, float heightHint)
        {
            if (_mode == ExplorerMode.Passenger)
            {
                _ride.End();
                _mode = ExplorerMode.Walk;
            }
            RequireBound();
            CancelTransition();
            if (_mode == ExplorerMode.Ride && _current != null)
            {
                Place(_current.Driver, x, z, headingRad, heightHint);
                Render(0f);
                return;
            }
            Place(_walker, x, z, headingRad, heightHint);
            _jump.Cancel();
            Render(0f);
        }

        /// <summary>The Action button pressed (from a HUD tap or a key): Hop on the offered seat, Hop off, or jump.</summary>
        public bool ToggleMode()
        {
            RequireBound();
            return Act(false);
        }

        /// <summary>Changes the wardrobe (rebuilds the meshes; the save keeps it).</summary>
        public void SetRecipe(CharacterRecipe recipe)
        {
            _avatar.SetRecipe(recipe, _material);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Frame

        /// <summary>One frame: input, the controlled body, transitions, the pose, sounds and the models.</summary>
        public void Tick(float dt, in ControlFrame controls, float cameraYawRad, bool paused, in ExplorerContext context)
        {
            _events = StepEvents.None;
            _ctx = context;
            _sound = context.Sound ?? NullSoundService.Instance;
            if (_ground == null) return;
            if (paused || !(dt > 0f))
            {
                Render(0f);
                return;
            }
            dt = Mathf.Min(dt, 0.1f);
            UpdateRideSnapshot(dt);
            if (_ride.State == RideState.Hailing && !_rideFound) _ride.End();
            UpdateZone(dt);
            HandleButtons(dt, controls);

            switch (_tr.Phase)
            {
                case Phase.Approach:
                    StepApproach(dt, controls);
                    break;
                case Phase.Enter:
                case Phase.Exit:
                    StepClip(dt, controls);
                    break;
            }
            if (_tr.Phase == Phase.Free || _tr.Phase == Phase.Approach)
            {
                switch (_mode)
                {
                    case ExplorerMode.Walk:
                        if (_tr.Phase == Phase.Free) StepWalk(dt, controls, cameraYawRad);
                        break;
                    case ExplorerMode.Ride:
                        StepDrive(dt, controls);
                        break;
                    case ExplorerMode.Passenger:
                        StepPassenger(dt);
                        break;
                }
            }
            StepOthers(dt);
            UpdatePrompt();
            Animate(dt, controls);
            Render(dt);
            _puffs.Tick(dt);
        }

        /// <summary>Hop and nudge out of a stuck spot ("reset").</summary>
        public bool Recover()
        {
            RequireBound();
            if (_tr.Phase != Phase.Free || _mode == ExplorerMode.Passenger) return false;
            return Active.Recover(_ground);
        }

        /// <summary>Camera target: the controlled body's ground point in scene space, its heading, speed and lean.</summary>
        public void GetCameraTarget(out Vector3 scenePosition, out float headingRad, out float speedMps, out float leanRad)
        {
            if (_mode == ExplorerMode.Passenger && _rideFound)
            {
                scenePosition = ToScene(_ridePose.X, _ridePose.Y, _ridePose.Z);
                headingRad = _ridePose.HeadingRad;
                speedMps = _ridePose.SpeedMps;
                leanRad = 0f;
                return;
            }
            double x, z;
            float y;
            PlayerPoint(out x, out y, out z);
            if (_mode == ExplorerMode.Ride && _current != null && _tr.Phase == Phase.Free)
            {
                VehiclePose p = _current.Driver.Interpolated;
                scenePosition = ToScene(p.X, p.Y, p.Z);
                headingRad = p.HeadingRad;
                speedMps = p.SpeedMps;
                leanRad = p.Lean;
                return;
            }
            scenePosition = ToScene(x, y - _jump.Height, z);
            headingRad = _tr.Phase != Phase.Free ? CurrentHeading() : _walker.Interpolated.HeadingRad;
            speedMps = _tr.Phase == Phase.Free ? _walker.Interpolated.SpeedMps : 0f;
            leanRad = 0f;
        }

        /// <summary>
        /// Seated (driving, or riding along, with no hop in progress): the head bone, the body frame of the vehicle as it
        /// is drawn and its bonnet point, for the mounted camera views (handlebar, bonnet, interior, driver's and
        /// passenger's seat; <see cref="ChaseCameraRig"/>). False on foot and while hopping on or off.
        /// </summary>
        public bool TryGetCameraMount(out CameraMount mount)
        {
            const float rad2Deg = 57.2957795f;
            mount = default(CameraMount);
            if (_tr.Phase != Phase.Free || _avatar == null) return false;
            if (_mode == ExplorerMode.Ride && _current != null)
            {
                VehiclePose p = _current.Driver.Interpolated;
                VehicleCatalogEntry e = _current.Entry;
                float pitch, roll;
                DrivenVehicleView.BodyAngles(p, e.Shape, ReducedMotion, out pitch, out roll);
                mount.Head = _avatar.Head.position;
                mount.Origin = ToScene(p.X, p.Y, p.Z);
                mount.HeadingDeg = p.HeadingRad * rad2Deg;
                mount.PitchDeg = pitch * rad2Deg;
                mount.RollDeg = roll * rad2Deg;
                if (!VehicleRoles.IsTwoWheeler(e.Shape) && e.Shape != BodyShape.Tractor)
                {
                    VehicleMesher.Dims d = VehicleMesher.DimsOf(e);
                    float y, z;
                    CameraViews.HoodEye(d.Wheelbase, d.Front, d.Height, out y, out z);
                    mount.HoodLocal = new Vector3(0f, y, z);
                    mount.HasHood = true;
                }
                return true;
            }
            if (_mode == ExplorerMode.Passenger && _rideFound)
            {
                mount.Head = _avatar.Head.position;
                mount.Origin = ToScene(_ridePose.X, _ridePose.Y, _ridePose.Z);
                mount.HeadingDeg = _ridePose.HeadingRad * rad2Deg;
                return true;
            }
            return false;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Buttons

        private void HandleButtons(float dt, in ControlFrame f)
        {
            // Action: a tap acts at once when nothing waits on a hold; a hold rides as a passenger or hails a taxi.
            if (f.ActionHeld) _actionHeld += dt;
            if (f.ToggleMode)
            {
                _holdConsumed = false;
                // With a passenger seat offered and no seat to drive, wait for the hold; else act now.
                bool holdMatters = _mode == ExplorerMode.Walk && _tr.Phase == Phase.Free && !_detector.Driver.Valid;
                if (!holdMatters) Act(false);
                // A tap from a source without a held state (a HUD tap) still resolves on the next frame's release.
                else _actionHeld = Math.Max(_actionHeld, 1e-4f);
            }
            if (f.ActionHeld && !_holdConsumed && _mode == ExplorerMode.Walk && _tr.Phase == Phase.Free)
            {
                if (_detector.Passenger.Valid && _actionHeld >= PassengerHoldS)
                {
                    _holdConsumed = true;
                    Act(true);
                }
                else if (!_detector.Passenger.Valid && !_detector.Driver.Valid && !_calm.Active && _actionHeld >= HailHoldS)
                {
                    _holdConsumed = true;
                    Hail();
                }
            }
            if (!f.ActionHeld)
            {
                // Released before the hold: a tap with nothing to drive jumps (or boards the offered passenger seat).
                if (_actionHeld > 0f && !_holdConsumed && _mode == ExplorerMode.Walk && _tr.Phase == Phase.Free && !_detector.Driver.Valid)
                {
                    if (_detector.Passenger.Valid) Act(true);
                    else if (_calm.Active) RingShrineBell(); // In a compound the Action button is "Ring bell".
                    else _jumpPressed = true;
                }
                _actionHeld = 0f;
                _holdConsumed = false;
            }
            if (f.RidePassenger && _mode == ExplorerMode.Walk) Act(true);
            if (f.Jump && _mode == ExplorerMode.Walk) _jumpPressed = true;
            if (f.Namaste && _mode == ExplorerMode.Walk && _tr.Phase == Phase.Free) StartEmote(Emote.Namaste);
            if (f.Whistle) Summon();
            if (f.Bell)
            {
                if (_mode == ExplorerMode.Passenger) RequestStop();
                else RingShrineBell();
            }
            HornInput(dt, f.Horn);
        }

        /// <summary>Rings a shrine bell (W2_DESIGN 6.6): on foot in calm mode, within the per-visit limits.</summary>
        private void RingShrineBell()
        {
            if (!_calm.Active || _mode != ExplorerMode.Walk || !_calm.TryRingBell()) return;
            StartEmote(Emote.RingBell);
            double x, z;
            float y;
            PlayerPoint(out x, out y, out z);
            _sound.PlayBell(BellKind.ShrineMedium, (float)(x - _origin.X), y - _origin.Y + 1.6f, (float)(z - _origin.Z));
        }

        private bool _jumpPressed;

        private void HornInput(float dt, bool held)
        {
            if (_mode != ExplorerMode.Ride || _current == null)
            {
                _hornHeld = 0f;
                return;
            }
            if (held)
            {
                if (_hornHeld == 0f && !ReducedMotion) _current.View.Kick(0.25f);
                _hornHeld += dt;
                return;
            }
            if (_hornHeld <= 0f) return;
            float hold = _hornHeld;
            _hornHeld = 0f;
            VehiclePose p = _current.Driver.Current;
            Vector3 s = ToScene(p.X, p.Y + 1f, p.Z);
            float vx = Mathf.Sin(p.HeadingRad) * p.SpeedMps, vz = Mathf.Cos(p.HeadingRad) * p.SpeedMps;
            Core.Synth.HornKind kind = VehicleRoles.HornOf(_current.Entry);
            if (_current.Engine != null && kind != Core.Synth.HornKind.BicycleBell) _current.Engine.Horn(hold);
            else _sound.PlayHorn(kind, s.x, s.y, s.z, vx, 0f, vz, hold);
        }

        private bool Act(bool asPassenger)
        {
            if (_tr.Phase == Phase.Approach || _tr.Phase == Phase.Enter)
            {
                CancelTransition();
                return true;
            }
            if (_tr.Phase == Phase.Exit) return false;
            switch (_mode)
            {
                case ExplorerMode.Ride:
                    if (_exitPending)
                    {
                        _exitPending = false;
                        return true;
                    }
                    if (Math.Abs(_current.Vehicle.SpeedMps) > VehicleRoles.ExitSpeedMps(_current.Entry))
                    {
                        _exitPending = true;
                        Raise("hud.slow_down");
                        return true;
                    }
                    BeginExitVehicle();
                    return true;
                case ExplorerMode.Passenger:
                    RequestStop();
                    return true;
                default:
                    if (_calm.Active && !asPassenger)
                    {
                        Raise("hud.rest_outside");
                        return true;
                    }
                    MountDetector.Candidate c = asPassenger ? _detector.Passenger : _detector.Driver;
                    if (!c.Valid)
                    {
                        if (!asPassenger) _jumpPressed = true;
                        return false;
                    }
                    BeginApproach(c);
                    return true;
            }
        }

        private void StartEmote(Emote e)
        {
            if (_calm.Active && e == Emote.Cheer) return;
            _emote = e;
            _emoteTime = 0f;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Walking

        private void StepWalk(float dt, in ControlFrame f, float cameraYawRad)
        {
            ControlFrame c = f;
            if (_calm.Active) c.Boost = false;
            bool moving = c.MoveX != 0f || c.MoveY != 0f;
            if (moving) _emote = Emote.None;
            DriveInput input = _emote != Emote.None ? new DriveInput(0f, 0f, 0f, false) : ControlMapper.WalkInput(_walker.Vehicle, c, cameraYawRad, dt);
            _events = _walker.Advance(dt, input, _ground, Wetness);
            bool ground = _walker.Vehicle.HasGround && !_walker.Vehicle.Airborne;
            _jump.Update(dt, _jumpPressed, ground, _emote == Emote.None);
            if (_jump.TookOff)
            {
                _sound.PlayUi(BankSound.JumpWhoosh, -6f);
                if (!ReducedMotion) _poser.KickSquash(1.6f);
            }
            if (_jump.Landed)
            {
                if (!ReducedMotion) _poser.Land(_jump.LandingSpeed);
                FootstepAt(FootstepGait.Land);
                if (_jump.RollLanding) Raise("hud.ta_da");
            }
            _jumpPressed = false;
            if ((_events & StepEvents.Landed) != 0 && !ReducedMotion) _poser.Land(_walker.Vehicle.LastLandingSpeedMps);
            if (_emote != Emote.None)
            {
                _emoteTime += dt;
                if (_emoteTime > EmoteLength(_emote)) _emote = Emote.None;
            }
        }

        private static float EmoteLength(Emote e)
        {
            switch (e)
            {
                case Emote.Namaste:
                case Emote.Namaskar: return CharacterPoser.NamasteS;
                case Emote.Wave: return CharacterPoser.WaveS;
                case Emote.Cheer: return CharacterPoser.CheerS;
                default: return CharacterPoser.RingBellS;
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Driving

        private void StepDrive(float dt, in ControlFrame f)
        {
            if (_current == null)
            {
                SetMode(ExplorerMode.Walk, false);
                return;
            }
            DriveInput input = ControlMapper.Ride(f);
            ArcadeVehicle v = _current.Vehicle;
            // The cow cushion: 5 km/h within 4 m, a gentle stop within 1.6 m (ContactRules; the cow keeps chewing).
            float cap = ContactRules.PlayerSpeedCapMps(_ctx.NearestCowM > 0f ? _ctx.NearestCowM : float.PositiveInfinity);
            if (Math.Abs(v.SpeedMps) > cap)
            {
                input = new DriveInput(0f, 1f, input.Steer, false);
                if (cap <= 0f && Math.Abs(v.SpeedMps) > 0.5f && !ReducedMotion) _current.View.Kick(0.3f);
            }
            // Vehicles rest outside sacred zones: a braking curve on the swept distance stops the vehicle at the edge;
            // a two-wheeler held against the edge (standing, throttle on) auto-parks there.
            bool towardZone = input.Throttle > 0f;
            VehicleZoneGate.Apply(ref input, v.SpeedMps, _zoneFrontM, _zoneRearM, v.Spec.BrakeMps2);
            if (towardZone && _zoneFrontM <= VehicleZoneGate.AtEdgeM && Math.Abs(v.SpeedMps) < 1f)
            {
                _zoneBlockS += dt;
                if (_zoneBlockS > 0.6f && VehicleRoles.IsTwoWheeler(_current.Entry.Shape)) BeginExitVehicle();
            }
            else
            {
                _zoneBlockS = 0f;
            }
            if (_exitPending)
            {
                input = new DriveInput(0f, 1f, input.Steer, false);
                if (Math.Abs(v.SpeedMps) <= VehicleRoles.ExitSpeedMps(_current.Entry))
                {
                    _exitPending = false;
                    BeginExitVehicle();
                }
            }
            if (_tr.Phase != Phase.Free) return;
            _events = _current.Driver.Advance(dt, input, _ground, Wetness);
            React(dt);
            DriveEngine(dt, input);
        }

        private void DriveEngine(float dt, in DriveInput input)
        {
            IEngineSound e = _current.Engine;
            if (e == null) return;
            VehiclePose p = _current.Driver.Current;
            Vector3 s = ToScene(p.X, p.Y + 0.6f, p.Z);
            float vx = Mathf.Sin(p.HeadingRad) * p.SpeedMps, vz = Mathf.Cos(p.HeadingRad) * p.SpeedMps;
            var surface = (FootstepSurface)(byte)FootSurfaces.Effective(_current.Vehicle.Foot, Wetness);
            e.Drive(s.x, s.y, s.z, vx, 0f, vz, Math.Abs(p.SpeedMps), Mathf.Clamp01(input.Throttle), Mathf.Clamp01(input.Brake), surface, dt);
            bool heavy = VehicleRoles.IsHeavy(_current.Entry.Shape);
            if (heavy && Math.Abs(_lastSpeed) > 0.5f && Math.Abs(p.SpeedMps) <= 0.5f) e.AirBrake(false);
            _lastSpeed = p.SpeedMps;
        }

        private void React(float dt)
        {
            ArcadeVehicle v = _current.Vehicle;
            if ((_events & StepEvents.Landed) != 0 && !ReducedMotion) _current.View.Kick(Mathf.Clamp(v.LastLandingSpeedMps / 4f, 0.3f, 1.5f));
            else if ((_events & StepEvents.Bump) != 0 && !ReducedMotion) _current.View.Kick(Mathf.Clamp(v.LastBumpStrength / 6f, 0.15f, 0.6f));
            if ((_events & StepEvents.StuckRecovered) != 0)
            {
                VehiclePose p = _current.Driver.Current;
                _puffs.Burst(ToScene(p.X, p.Y, p.Z), 6, 0.55f, PuffKind.Smoke);
                _sound.PlayUi(BankSound.UiBoing, -8f);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Passenger

        private void UpdateRideSnapshot(float dt)
        {
            _rideFound = false;
            // The vehicle of a ride, a hail, or a passenger approach in progress.
            bool approaching = _tr.Phase != Phase.Free && _tr.AgentId >= 0;
            int agent = _ride.State != RideState.None ? _ride.AgentId : approaching ? _tr.AgentId : -1;
            if (agent < 0) return;
            int i = PassengerRide.Find(_ctx.Traffic, _ctx.TrafficCount, agent);
            if (i < 0)
            {
                if (approaching && _tr.Phase == Phase.Approach) CancelTransition();
                return;
            }
            AgentPose a = _ctx.Traffic[i];
            if (approaching && _tr.Phase == Phase.Approach)
            {
                // The bus or taxi pulled away before we reached the door.
                double mx = a.X - _ridePose.X, mz = a.Z - _ridePose.Z;
                if (mx * mx + mz * mz > 1.5 * 1.5 || Math.Abs(a.SpeedMps) > 1f)
                {
                    CancelTransition();
                    Raise("hud.cant_reach");
                    return;
                }
            }
            _ridePose = a;
            _rideFound = true;
            if (_ride.State == RideState.Hailing)
            {
                double x, z;
                float y;
                PlayerPoint(out x, out y, out z);
                _ride.Update(dt, true, a, x, z);
            }
        }

        private void StepPassenger(float dt)
        {
            double x, z;
            float y;
            PlayerPoint(out x, out y, out z);
            RideState s = _ride.Update(dt, _rideFound, _ridePose, x, z);
            if (s == RideState.Alight) BeginExitPassenger();
        }

        private void Hail()
        {
            double x, z;
            float y;
            PlayerPoint(out x, out y, out z);
            int i = PassengerRide.NearestTaxi(_ctx.Traffic, _ctx.TrafficCount, x, z);
            if (i < 0)
            {
                Raise("hud.no_taxi");
                return;
            }
            _ride.Hail(_ctx.Traffic[i]);
            StartEmote(Emote.Wave);
            Raise("hud.taxi_coming");
        }

        private void RequestStop()
        {
            if (_ride.State != RideState.Riding) return;
            _ride.RequestStop();
            _sound.PlayUi(BankSound.BicycleBell, -6f);
            Raise("hud.stop_requested");
        }

        // -------------------------------------------------------------------------------------------------------------
        // Garage summon and the community fleet

        private PlayerVehicle OwnVehicle()
        {
            for (int i = 0; i < _vehicles.Count; i++)
                if (_vehicles[i].Source == VehicleSource.Garage) return _vehicles[i];
            return null;
        }

        private void Summon()
        {
            if (_mode != ExplorerMode.Walk || _tr.Phase != Phase.Free)
            {
                Raise("hud.whistle_on_foot");
                return;
            }
            // Vehicles rest outside: no whistling a vehicle into a compound or heritage square.
            if (_calm.Active)
            {
                Raise("hud.garage_rest_outside");
                return;
            }
            double px, pz;
            float py;
            PlayerPoint(out px, out py, out pz);
            GarageVehicle gv = _garage.Current;
            PlayerVehicle own = OwnVehicle();
            if (own != null && own.Variant == gv.Variant && own.Livery == gv.Livery)
            {
                VehiclePose op = own.Driver.Current;
                double ddx = op.X - px, ddz = op.Z - pz;
                if (ddx * ddx + ddz * ddz < 18.0 * 18.0)
                {
                    // Already here: whistling again calls the next vehicle of the garage.
                    if (_garage.Vehicles.Count < 2)
                    {
                        Raise("hud.garage_here");
                        return;
                    }
                    gv = _garage.Next();
                }
            }
            // It stops beside the player, on whichever side lies outside any zone it may not enter.
            uint allowed = VehicleZoneGate.AllowedKinds(VehicleCatalog.At(gv.Variant));
            double ax, az;
            Side(px, pz, _walker.Current.HeadingRad, 1.6, out ax, out az);
            if (Blocked(ax, az, allowed)) Side(px, pz, _walker.Current.HeadingRad, -1.6, out ax, out az);
            if (Blocked(ax, az, allowed))
            {
                Raise("hud.garage_rest_outside");
                return;
            }
            if (own != null)
            {
                _vehicles.Remove(own);
                if (_current == own) _current = null;
                own.Dispose();
            }
            own = new PlayerVehicle(_nextId++, gv.Variant, gv.Livery, gv.PlateSeed, VehicleSource.Garage, transform, _material, _cache, null);
            _vehicles.Add(own);
            double sx, sz;
            SummonStart(px, py, pz, allowed, out sx, out sz);
            float heading = Mathf.Atan2((float)(ax - sx), (float)(az - sz));
            Place(own.Driver, sx, sz, heading, py);
            own.Arriving = true;
            own.ArriveS = 0f;
            own.ArriveRelocated = false;
            own.ArriveX = ax;
            own.ArriveZ = az;
            _sound.PlayUi(BankSound.UiBoing, -10f);
            Raise("hud.garage_coming");
        }

        // Bearings tried for a summoned vehicle's start, relative to straight behind the camera (radians).
        private static readonly float[] SummonBearings = { 0f, 0.45f, -0.45f, 0.9f, -0.9f, 1.35f, -1.35f };

        /// <summary>True when (x, z) lies in a sacred zone of a kind not in <paramref name="allowedKinds"/>.</summary>
        private bool Blocked(double x, double z, uint allowedKinds)
        {
            SacredZone zz;
            return _ctx.Zones != null && _ctx.Zones.TryGetZone(x, z, out zz) && (allowedKinds & (1u << (int)zz.Kind)) == 0;
        }

        /// <summary>
        /// Where a summoned vehicle starts (W2_DESIGN 6.2): on a road at least <see cref="Garage.SummonMinDistanceM"/>
        /// away, out of the camera's view and outside any zone it may not enter, preferring straight behind the camera;
        /// failing that, off-road the same distance straight behind the camera.
        /// </summary>
        private void SummonStart(double px, float py, double pz, uint allowed, out double sx, out double sz)
        {
            Vector3 back = -_ctx.CameraForward;
            back.y = 0f;
            if (back.sqrMagnitude < 1e-4f) back = new Vector3(-Mathf.Sin(_walker.Current.HeadingRad), 0f, -Mathf.Cos(_walker.Current.HeadingRad));
            back.Normalize();
            float baseBearing = Mathf.Atan2(back.x, back.z);
            double min = Garage.SummonMinDistanceM;
            var roads = _ground as IRoadQuery;
            for (int r = 0; r < 2; r++)
            {
                double radius = min * (r == 0 ? 1.4 : 2.0);
                for (int k = 0; k < SummonBearings.Length; k++)
                {
                    float b = baseBearing + SummonBearings[k];
                    double cx = px + Math.Sin(b) * radius, cz = pz + Math.Cos(b) * radius;
                    RoadHit hit;
                    if (roads == null || !roads.TryNearestRoad(cx, cz, 40.0, out hit)) continue;
                    double dx = hit.X - px, dz = hit.Z - pz;
                    if (dx * dx + dz * dz < min * min || InView(hit.X, py, hit.Z) || Blocked(hit.X, hit.Z, allowed)) continue;
                    sx = hit.X;
                    sz = hit.Z;
                    return;
                }
            }
            sx = px + back.x * min * 1.2;
            sz = pz + back.z * min * 1.2;
        }

        /// <summary>A late arrival jumps once to a road just behind the camera, out of view and outside zones, and
        /// drives the last metres from there. False when no such spot exists.</summary>
        private bool RelocateOutOfView(PlayerVehicle v, double px, float py, double pz, uint allowed)
        {
            Vector3 back = -_ctx.CameraForward;
            back.y = 0f;
            if (back.sqrMagnitude < 1e-4f) return false;
            back.Normalize();
            float baseBearing = Mathf.Atan2(back.x, back.z);
            var roads = _ground as IRoadQuery;
            for (int k = 0; k < SummonBearings.Length; k++)
            {
                float b = baseBearing + SummonBearings[k];
                double cx = px + Math.Sin(b) * LateArrivalM, cz = pz + Math.Cos(b) * LateArrivalM;
                RoadHit hit;
                if (roads != null && roads.TryNearestRoad(cx, cz, 8.0, out hit))
                {
                    cx = hit.X;
                    cz = hit.Z;
                }
                if (InView(cx, py, cz) || Blocked(cx, cz, allowed)) continue;
                Place(v.Driver, cx, cz, Mathf.Atan2((float)(v.ArriveX - cx), (float)(v.ArriveZ - cz)), py);
                return true;
            }
            return false;
        }

        /// <summary>How far behind the player a late arrival is moved.</summary>
        private const double LateArrivalM = 16.0;

        /// <summary>A relocated arrival gets this long to drive in before it parks where it stands.</summary>
        private const float LateArrivalGraceS = 8f;

        /// <summary>Parked and arriving vehicles: settling, summoned arrivals, fleet returns.</summary>
        private void StepOthers(float dt)
        {
            double px, pz;
            float py;
            PlayerPoint(out px, out py, out pz);
            for (int i = _vehicles.Count - 1; i >= 0; i--)
            {
                PlayerVehicle v = _vehicles[i];
                bool aboard = v == _current && (_mode == ExplorerMode.Ride || _tr.Phase != Phase.Free);
                if (aboard) continue;
                if (v.Arriving)
                {
                    StepArrival(v, dt, px, pz);
                    continue;
                }
                v.StepParked(dt, _ground, Wetness);
                if (v.Loan == null) continue;
                VehiclePose p = v.Driver.Current;
                if (v.Loan.Update(dt, false, p.X, p.Z, px, pz) && !InView(p.X, p.Y, p.Z))
                {
                    // Home again, out of sight: the parked copy reappears.
                    _vehicles.RemoveAt(i);
                    if (v == _current) _current = null;
                    uint id = v.Loan.ParkedId;
                    v.Dispose();
                    Action<uint, bool> h = FleetVehicle;
                    if (h != null) h(id, false);
                }
            }
        }

        private void StepArrival(PlayerVehicle v, float dt, double px, double pz)
        {
            v.ArriveS += dt;
            VehiclePose p = v.Driver.Current;
            double dx = v.ArriveX - p.X, dz = v.ArriveZ - p.Z;
            double d = Math.Sqrt(dx * dx + dz * dz);
            uint allowed = VehicleZoneGate.AllowedKinds(v.Entry);
            if (d < 2.5 || v.ArriveS > SummonTimeoutS)
            {
                // Late: once, jump to a road just out of view behind the camera and drive in from there; never pop
                // into view beside the player. A second timeout parks it where it stands.
                if (d >= 2.5 && !v.ArriveRelocated)
                {
                    v.ArriveRelocated = true;
                    if (RelocateOutOfView(v, px, _walker.Current.Y, pz, allowed))
                    {
                        v.ArriveS = SummonTimeoutS - LateArrivalGraceS;
                        return;
                    }
                }
                v.Arriving = false;
                v.Park();
                _sound.PlayUi(BankSound.UiBoing, -8f);
                Raise("hud.garage_arrived");
                return;
            }
            float target = Mathf.Atan2((float)dx, (float)dz);
            float steer = v.Vehicle.SteerToward(target, dt);
            float throttle = d > 12 ? 0.7f : 0.3f;
            float brake = Math.Abs(v.Vehicle.SpeedMps) > (d > 12 ? 8f : 3f) ? 0.6f : 0f;
            var input = new DriveInput(brake > 0f ? 0f : throttle, brake, steer, false);
            // It too stops at the edge of a zone it may not enter (the timeout then brings it round).
            VehicleMesher.Dims dims = VehicleMesher.DimsOf(v.Entry);
            float sin = (float)Math.Sin(p.HeadingRad), cos = (float)Math.Cos(p.HeadingRad), front = dims.Wheelbase + dims.Front;
            float ahead = VehicleZoneGate.DistanceToZone(_ctx.Zones, allowed, p.X + sin * front, p.Z + cos * front, p.HeadingRad, 0f,
                                                         VehicleZoneGate.LookAheadM(Math.Abs(p.SpeedMps), v.Vehicle.Spec.BrakeMps2));
            VehicleZoneGate.Apply(ref input, v.Vehicle.SpeedMps, ahead, float.PositiveInfinity, v.Vehicle.Spec.BrakeMps2);
            v.Driver.Advance(dt, input, _ground, Wetness);
        }

        private bool InView(double x, float y, double z)
        {
            Vector3 s = ToScene(x, y, z) - _ctx.CameraPosition;
            if (_ctx.CameraForward.sqrMagnitude < 1e-4f) return false;
            float d = s.magnitude;
            if (d > 250f) return false;
            return Vector3.Dot(s / Mathf.Max(0.01f, d), _ctx.CameraForward) > 0.35f;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Detecting seats, approach, enter and exit

        private void Detect()
        {
            if (_mode != ExplorerMode.Walk || _tr.Phase != Phase.Free || _calm.Active)
            {
                _detector.Clear();
                return;
            }
            double px, pz;
            float py;
            PlayerPoint(out px, out py, out pz);
            _detector.Begin(px, pz, _walker.Current.HeadingRad);
            for (int i = 0; i < _vehicles.Count; i++)
            {
                PlayerVehicle v = _vehicles[i];
                if (v.Arriving) continue;
                _detector.Consider(v.Id, v.Variant, v.Frame, v.Seats, v.Entry.PlayerDrivable, false);
            }
            if (_ctx.Parked != null)
            {
                for (int i = 0; i < _ctx.ParkedCount; i++)
                {
                    ParkedSpot s = _ctx.Parked[i];
                    if (!s.Fleet || Borrowed(s.Id)) continue;
                    VehicleCatalogEntry e = VehicleCatalog.At(s.Variant);
                    if (!e.PlayerDrivable) continue;
                    var frame = new VehicleFrame(s.X, s.Z, s.Y, s.YawDeg * Mathf.Deg2Rad);
                    _detector.Consider(ParkedKey(s.Id), s.Variant, frame, VehicleRoles.Seats(s.Variant), true, false);
                }
            }
            if (_ctx.Traffic != null)
            {
                for (int i = 0; i < _ctx.TrafficCount; i++)
                {
                    AgentPose a = _ctx.Traffic[i];
                    if (!PassengerRide.IsBoardable(a)) continue;
                    double dx = a.X - px, dz = a.Z - pz;
                    if (dx * dx + dz * dz > 20.0 * 20.0) continue;
                    var frame = new VehicleFrame(a.X, a.Z, a.Y, a.HeadingRad);
                    _detector.Consider(AgentKey(a.AgentId), a.Variant, frame, VehicleRoles.Seats(a.Variant), false, true);
                }
            }
        }

        private static long ParkedKey(uint id)
        {
            return (1L << 40) | id;
        }

        private static long AgentKey(int agentId)
        {
            return (2L << 40) | (uint)agentId;
        }

        private bool Borrowed(uint parkedId)
        {
            for (int i = 0; i < _vehicles.Count; i++)
                if (_vehicles[i].Loan != null && _vehicles[i].Loan.ParkedId == parkedId) return true;
            return false;
        }

        private void BeginApproach(in MountDetector.Candidate c)
        {
            PlayerVehicle vehicle = null;
            VehicleFrame frame;
            int agentId = -1;
            long kind = c.VehicleId >> 40;
            if (kind == 0)
            {
                for (int i = 0; i < _vehicles.Count; i++)
                    if (_vehicles[i].Id == c.VehicleId) vehicle = _vehicles[i];
                if (vehicle == null) return;
                frame = vehicle.Frame;
            }
            else if (kind == 1)
            {
                // Borrow the fleet vehicle: it becomes one of ours (the parked copy hides) until it goes home.
                uint id = (uint)(c.VehicleId & 0xFFFFFFFFL);
                ParkedSpot spot = default(ParkedSpot);
                bool found = false;
                for (int i = 0; i < _ctx.ParkedCount; i++)
                    if (_ctx.Parked[i].Id == id)
                    {
                        spot = _ctx.Parked[i];
                        found = true;
                    }
                if (!found) return;
                var loan = new FleetLoan
                {
                    ParkedId = id, Variant = spot.Variant, Livery = spot.Livery, HomeX = spot.X, HomeZ = spot.Z, HomeY = spot.Y, HomeYawDeg = spot.YawDeg,
                };
                vehicle = new PlayerVehicle(_nextId++, spot.Variant, spot.Livery, CharMath.Hash(id, 0x464C54u), VehicleSource.Fleet, transform, _material, _cache, loan);
                Place(vehicle.Driver, spot.X, spot.Z, spot.YawDeg * Mathf.Deg2Rad, spot.Y);
                _vehicles.Add(vehicle);
                Action<uint, bool> h = FleetVehicle;
                if (h != null) h(id, true);
                frame = vehicle.Frame;
            }
            else
            {
                agentId = (int)(uint)(c.VehicleId & 0xFFFFFFFFL);
                int i = PassengerRide.Find(_ctx.Traffic, _ctx.TrafficCount, agentId);
                if (i < 0) return;
                AgentPose a = _ctx.Traffic[i];
                frame = new VehicleFrame(a.X, a.Z, a.Y, a.HeadingRad);
                _ridePose = a;
                _rideFound = true;
            }
            SeatSocket[] seats = vehicle != null ? vehicle.Seats : VehicleRoles.Seats(c.Variant);
            SeatSocket seat = seats[0];
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].Index == c.SeatIndex) seat = seats[i];
            double px, pz;
            float py;
            PlayerPoint(out px, out py, out pz);
            MountPlan plan;
            VehicleClass cls = vehicle != null ? vehicle.Entry.TrafficClass : VehicleCatalog.At(c.Variant).TrafficClass;
            if (!MountPlan.TryPlan(cls, seat, frame, px, pz, _ground, out plan))
            {
                Raise("hud.cant_reach");
                return;
            }
            VehicleCatalogEntry entry = VehicleCatalog.At(c.Variant);
            _tr = new Transition
            {
                Phase = Phase.Approach, Vehicle = vehicle, AgentId = agentId, Passenger = vehicle == null || seat.Kind != SeatKind.Driver, Seat = seat,
                Pose = VehicleRoles.PoseFor(entry, seat.Kind), ToX = plan.EntryX, ToZ = plan.EntryZ, Duration = plan.EnterS, BestDistance = float.MaxValue,
            };
            _emote = Emote.None;
            _sound.PlayUi(BankSound.ClothRustle, -12f);
        }

        private void StepApproach(float dt, in ControlFrame f)
        {
            if (f.MoveX != 0f || f.MoveY != 0f)
            {
                CancelTransition();
                return;
            }
            VehiclePose w = _walker.Current;
            double dx = _tr.ToX - w.X, dz = _tr.ToZ - w.Z;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d < 0.3f)
            {
                BeginEnter();
                return;
            }
            // Auto-walk at about 2.2 m/s; give up after 0.5 s without progress.
            float heading = Mathf.Atan2((float)dx, (float)dz);
            var input = new DriveInput(0.6f, 0f, _walker.Vehicle.SteerToward(heading, dt), false);
            _events = _walker.Advance(dt, input, _ground, Wetness);
            if (d < _tr.BestDistance - 0.02f)
            {
                _tr.BestDistance = d;
                _tr.BlockedS = 0f;
            }
            else
            {
                _tr.BlockedS += dt;
                if (_tr.BlockedS > MountPlan.BlockedAbortS)
                {
                    Raise("hud.cant_reach");
                    CancelTransition();
                }
            }
        }

        private void BeginEnter()
        {
            VehiclePose w = _walker.Current;
            _tr.Phase = Phase.Enter;
            _tr.T = 0f;
            _tr.FromX = w.X;
            _tr.FromZ = w.Z;
            _tr.FromY = w.Y;
            _tr.FromHeading = w.HeadingRad;
            VehicleCatalogEntry e = _tr.Vehicle != null ? _tr.Vehicle.Entry : VehicleCatalog.At(_ridePose.Variant);
            if (VehicleRoles.NeedsHelmet(e))
            {
                _avatar.Helmet = true;
                _helmetOff = 0f;
                _sound.PlayUi(BankSound.HelmetPop, -6f);
            }
            if (_tr.Vehicle != null && !_tr.Passenger) _tr.Vehicle.StartEngine(_sound);
        }

        /// <summary>The enter and exit clips: the root arcs between the ground point and the seat, the pose blends.</summary>
        private void StepClip(float dt, in ControlFrame f)
        {
            _tr.T += dt / Mathf.Max(0.1f, _tr.Duration);
            if (_tr.Phase == Phase.Enter && _tr.T < CancelFraction && (f.MoveX != 0f || f.MoveY != 0f) && f.Throttle <= 0f)
            {
                // Cancelled: step back down.
                CancelTransition();
                return;
            }
            if (_tr.T < 1f) return;
            if (_tr.Phase == Phase.Enter) FinishEnter();
            else FinishExit();
        }

        private void FinishEnter()
        {
            if (_tr.Passenger)
            {
                if (_tr.Vehicle != null)
                {
                    // Pillion and passenger seats of our own vehicles are not used in S1.
                    CancelTransition();
                    return;
                }
                _ride.Board(_ridePose, _tr.Seat.Index);
                _tr = default(Transition);
                SetMode(ExplorerMode.Passenger, false);
                _sound.PlayUi(BankSound.DoorHiss, -10f);
                return;
            }
            _current = _tr.Vehicle;
            _tr = default(Transition);
            _exitPending = false;
            if (!ReducedMotion) _current.View.Kick(0.6f);
            SetMode(ExplorerMode.Ride, false);
        }

        private void BeginExitVehicle()
        {
            if (_current == null) return;
            PlayerVehicle v = _current;
            SeatSocket seat = v.Seats[0];
            double x, z;
            float y;
            if (!ExitPlacement.Find(v.Frame, v.Entry, seat, _ground, out x, out z, out y))
            {
                Raise("hud.no_room");
                return;
            }
            v.StopEngine();
            v.Park();
            BeginExit(v, seat, VehicleRoles.DriverPose(v.Entry), x, z, y, v.Frame.HeadingRad);
            _current = v;
            SetMode(ExplorerMode.Walk, false);
        }

        private void BeginExitPassenger()
        {
            VehicleCatalogEntry e = VehicleCatalog.At(_ride.Variant);
            SeatSocket[] seats = VehicleRoles.Seats(_ride.Variant);
            SeatSocket seat = seats[0];
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].Index == _ride.SeatIndex) seat = seats[i];
            var frame = new VehicleFrame(_ridePose.X, _ridePose.Z, _ridePose.Y, _ridePose.HeadingRad);
            double x, z;
            float y;
            if (!ExitPlacement.Find(frame, e, seat, _ground, out x, out z, out y))
            {
                x = _ridePose.X;
                z = _ridePose.Z;
                y = _ridePose.Y;
            }
            int fare = _ride.Fare;
            _ride.End();
            BeginExit(null, seat, VehicleRoles.PoseFor(e, seat.Kind), x, z, y, _ridePose.HeadingRad);
            SetMode(ExplorerMode.Walk, false);
            if (fare > 0) Raise("hud.taxi_fare");
        }

        private void BeginExit(PlayerVehicle v, in SeatSocket seat, SeatPose pose, double x, double z, float y, float heading)
        {
            double sx, sz;
            float sy;
            SeatWorld(v, seat, out sx, out sy, out sz);
            _tr = new Transition
            {
                Phase = Phase.Exit, T = 0f, Duration = seat.ExitS, Vehicle = v, Seat = seat, Pose = pose, Passenger = v == null,
                FromX = sx, FromZ = sz, FromY = sy, ToX = x, ToZ = z, ToY = y, FromHeading = heading, ToHeading = heading,
            };
            _sound.PlayUi(BankSound.ClothRustle, -12f);
        }

        private void FinishExit()
        {
            Place(_walker, _tr.ToX, _tr.ToZ, _tr.ToHeading, _tr.ToY);
            if (!ReducedMotion) _poser.Land(2.5f);
            _puffs.Burst(ToScene(_tr.ToX, _tr.ToY, _tr.ToZ), 3, 0.35f, PuffKind.Smoke);
            FootstepAt(FootstepGait.Land);
            if (_avatar.Helmet) _helmetOff = HelmetOffDelayS;
            _tr = default(Transition);
        }

        private void CancelTransition()
        {
            if (_tr.Phase == Phase.Enter && _tr.Vehicle != null && !_tr.Passenger) _tr.Vehicle.StopEngine();
            if (_tr.Phase == Phase.Enter && _avatar != null && _avatar.Helmet) _helmetOff = 0.3f;
            _tr = default(Transition);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Sacred zones and calm mode

        private void UpdateZone(float dt)
        {
            SacredZoneIndex zones = _ctx.Zones;
            double x, z;
            float y;
            PlayerPoint(out x, out y, out z);
            SacredZone zone = default(SacredZone);
            bool inZone = zones != null && zones.TryGetZone(x, z, out zone);
            int change = _calm.Update(dt, inZone, zone);
            if (change != 0)
            {
                Action<SacredZone, bool> h = SacredZoneChanged;
                if (h != null) h(zone, change > 0);
            }
            // Driving: sweep the path both ways for a zone this vehicle may not enter (VehicleZoneGate); StepDrive
            // brakes on the distance so the vehicle stops at the edge at any speed, forwards or reversing.
            _zoneAhead = false;
            _zoneFrontM = _zoneRearM = float.PositiveInfinity;
            if (_mode == ExplorerMode.Ride && _current != null && zones != null)
            {
                VehiclePose p = _current.Driver.Current;
                VehicleMesher.Dims d = VehicleMesher.DimsOf(_current.Entry);
                VehicleSpec spec = _current.Vehicle.Spec;
                uint allowed = VehicleZoneGate.AllowedKinds(_current.Entry);
                SacredZone zz;
                if (zones.TryGetZone(p.X, p.Z, out zz) && (allowed & (1u << (int)zz.Kind)) == 0)
                {
                    // Already inside (placed there): no gate, so it can drive out; the prompt still shows.
                    _zoneAhead = true;
                    return;
                }
                float curvature = spec.WheelbaseM > 0.1f ? (float)Math.Tan(p.SteerRad) / spec.WheelbaseM : 0f;
                float look = VehicleZoneGate.LookAheadM(Math.Abs(p.SpeedMps), spec.BrakeMps2);
                float sin = (float)Math.Sin(p.HeadingRad), cos = (float)Math.Cos(p.HeadingRad);
                float front = d.Wheelbase + d.Front, rear = d.Rear;
                _zoneFrontM = VehicleZoneGate.DistanceToZone(zones, allowed, p.X + sin * front, p.Z + cos * front, p.HeadingRad, curvature, look);
                // Reversing: the path runs backwards and the arc turns the other way.
                _zoneRearM = VehicleZoneGate.DistanceToZone(zones, allowed, p.X - sin * rear, p.Z - cos * rear, p.HeadingRad + (float)Math.PI,
                                                            -curvature, p.SpeedMps < 0.5f ? look : VehicleZoneGate.MarginM + VehicleZoneGate.SlackM);
                _zoneAhead = _zoneFrontM < ZonePromptM || p.SpeedMps < -0.1f && _zoneRearM < ZonePromptM;
            }
        }

        /// <summary>The "Vehicles rest outside" chip shows when a zone is this close along the path.</summary>
        private const float ZonePromptM = 12f;

        private float _zoneFrontM = float.PositiveInfinity, _zoneRearM = float.PositiveInfinity;

        private void UpdatePrompt()
        {
            Detect();
            PassengerOffered = _detector.Passenger.Valid;
            PromptRig = RigClass.Walk;
            switch (_mode)
            {
                case ExplorerMode.Ride:
                    Prompt = _zoneAhead ? ExplorerPrompt.VehiclesRestOutside : ExplorerPrompt.HopOff;
                    return;
                case ExplorerMode.Passenger:
                    Prompt = _ride.State == RideState.StopRequested ? ExplorerPrompt.StopRequested : ExplorerPrompt.RideAlong;
                    return;
            }
            if (_ride.State == RideState.Hailing)
            {
                Prompt = ExplorerPrompt.TaxiComing;
                return;
            }
            if (_calm.Active)
            {
                Prompt = ExplorerPrompt.VehiclesRestOutside;
                return;
            }
            if (_detector.Driver.Valid)
            {
                Prompt = ExplorerPrompt.HopOn;
                PromptRig = VehicleRoles.RigOf(VehicleCatalog.At(_detector.Driver.Variant));
                return;
            }
            Prompt = ExplorerPrompt.Jump;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Pose and render

        private void Animate(float dt, in ControlFrame f)
        {
            var input = new PoseInput { Dt = dt, Calm = _calm.Active, ReducedMotion = ReducedMotion };
            VehiclePose w = _walker.Interpolated;
            bool idle = true;
            switch (_tr.Phase)
            {
                case Phase.Enter:
                    input.Seat = _tr.Pose;
                    input.Seated01 = Mathf.SmoothStep(0f, 1f, _tr.T);
                    idle = false;
                    break;
                case Phase.Exit:
                    input.Seat = _tr.Pose;
                    input.Seated01 = 1f - Mathf.SmoothStep(0f, 1f, _tr.T);
                    idle = false;
                    break;
                default:
                    if (_mode == ExplorerMode.Ride && _current != null)
                    {
                        VehiclePose p = _current.Driver.Interpolated;
                        input.Seat = VehicleRoles.DriverPose(_current.Entry);
                        input.Seated01 = 1f;
                        float maxSteer = _current.Vehicle.Spec.MaxSteerDeg * Mathf.Deg2Rad;
                        input.Steer = maxSteer > 1e-3f ? Mathf.Clamp(p.SteerRad / maxSteer, -1f, 1f) : 0f;
                        input.CrankRad = (float)(p.OdometerM / Math.Max(0.1, _current.Entry.WheelDiaM * 0.5) / 1.6);
                        input.FootDown = p.FootDown || VehicleRoles.FootDown(_current.Entry, p.SpeedMps);
                        input.LateralMps2 = p.SpeedMps * _current.Vehicle.YawRateRadPerS;
                        idle = false;
                    }
                    else if (_mode == ExplorerMode.Passenger)
                    {
                        VehicleCatalogEntry e = VehicleCatalog.At(_ride.Variant);
                        SeatKind kind = SeatKind.Passenger;
                        SeatSocket[] seats = VehicleRoles.Seats(_ride.Variant);
                        for (int i = 0; i < seats.Length; i++)
                            if (seats[i].Index == _ride.SeatIndex) kind = seats[i].Kind;
                        input.Seat = VehicleRoles.PoseFor(e, kind);
                        input.Seated01 = 1f;
                        input.LateralMps2 = 0f;
                        idle = false;
                    }
                    else
                    {
                        input.SpeedMps = _tr.Phase == Phase.Approach ? Math.Max(1.2f, w.SpeedMps) : w.SpeedMps;
                        input.YawRateRadPerS = _walker.Vehicle.YawRateRadPerS;
                        input.Airborne = _jump.Airborne || _walker.Vehicle.Airborne;
                        input.VerticalMps = _jump.VerticalSpeed;
                        input.Emote = _emote;
                        input.EmoteTime = _emoteTime;
                        idle = Math.Abs(w.SpeedMps) < 0.05f && _emote == Emote.None && !input.Airborne && _tr.Phase == Phase.Free;
                    }
                    break;
            }
            var ctx = new FidgetContext
            {
                Sacred = _calm.Active, HasTopi = Recipe[OutfitSlotKind.Head].Item == OutfitItem.DhakaTopi && !_avatar.Helmet,
                LongHair = Recipe.Hair == 6 || Recipe.Hair == 7 || Recipe.Hair == 8, Urban = _ctx.Urban, Hour = _ctx.GameHour,
            };
            bool input01 = !idle || f.HasDriveInput || f.LookYawDeg != 0f;
            Fidget fid = _fidgets.Update(dt, input01, ctx);
            input.Fidget = fid;
            input.FidgetTime = _fidgets.Time;
            _poser.Update(input);
            if (idle) _stillS += dt;
            else _stillS = 0f;
            _avatar.RingVisible = idle && _stillS > 1f;
            // Footsteps from the gait's heel strikes, on the surface under the walker.
            if (_mode == ExplorerMode.Walk && _tr.Phase != Phase.Enter && (_poser.LeftStrike || _poser.RightStrike))
                FootstepAt(_poser.Running ? FootstepGait.Run : FootstepGait.Walk);
            if (_helmetOff > 0f && _mode == ExplorerMode.Walk && _tr.Phase == Phase.Free)
            {
                _helmetOff -= dt;
                if (_helmetOff <= 0f)
                {
                    _avatar.Helmet = false;
                    _sound.PlayUi(BankSound.HelmetPop, -8f);
                }
            }
        }

        private void FootstepAt(FootstepGait gait)
        {
            VehiclePose w = _walker.Current;
            var surface = FootSurfaces.Effective(_walker.Vehicle.Foot, Wetness);
            Vector3 s = ToScene(w.X, w.Y, w.Z);
            bool barefoot = Recipe[OutfitSlotKind.Feet].Item == OutfitItem.Barefoot;
            _sound.PlayFootstep((byte)surface, gait, s.x, s.y, s.z, true, barefoot);
        }

        private void Render(float dt)
        {
            for (int i = 0; i < _vehicles.Count; i++)
            {
                PlayerVehicle v = _vehicles[i];
                VehiclePose p = v.Driver.Interpolated;
                v.View.Apply(p, ToScene(p.X, p.Y, p.Z), dt, ReducedMotion);
            }
            Vector3 position;
            Quaternion rotation;
            AvatarPlacement(out position, out rotation);
            _avatar.Apply(_poser, position, rotation);
        }

        private void AvatarPlacement(out Vector3 position, out Quaternion rotation)
        {
            const float rad2Deg = 57.2957795f;
            double sx, sz;
            float sy;
            switch (_tr.Phase)
            {
                case Phase.Enter:
                case Phase.Exit:
                {
                    // Seat end follows the vehicle; the ground end is fixed. A small hop arc between them.
                    SeatWorld(_tr.Vehicle, _tr.Seat, out sx, out sy, out sz);
                    Quaternion seatRot = SeatRotation(_tr.Vehicle);
                    bool enter = _tr.Phase == Phase.Enter;
                    double gx = enter ? _tr.FromX : _tr.ToX, gz = enter ? _tr.FromZ : _tr.ToZ;
                    float gy = enter ? _tr.FromY : _tr.ToY;
                    float t = Mathf.Clamp01(_tr.T);
                    float k = enter ? Mathf.SmoothStep(0f, 1f, t) : 1f - Mathf.SmoothStep(0f, 1f, t);
                    double x = gx + (sx - gx) * k, z = gz + (sz - gz) * k;
                    float y = gy + (sy - gy) * k + HopHeightM * 4f * k * (1f - k);
                    position = ToScene(x, y, z);
                    Quaternion ground = Quaternion.Euler(0f, (enter ? _tr.FromHeading : _tr.ToHeading) * rad2Deg, 0f);
                    rotation = Quaternion.Slerp(ground, seatRot, k);
                    return;
                }
            }
            if (_mode == ExplorerMode.Ride && _current != null)
            {
                SeatWorld(_current, _current.Seats[0], out sx, out sy, out sz);
                position = ToScene(sx, sy, sz);
                rotation = SeatRotation(_current);
                return;
            }
            if (_mode == ExplorerMode.Passenger && _rideFound)
            {
                SeatSocket seat = default(SeatSocket);
                SeatSocket[] seats = VehicleRoles.Seats(_ride.Variant);
                for (int i = 0; i < seats.Length; i++)
                    if (seats[i].Index == _ride.SeatIndex) seat = seats[i];
                VehicleSeats.ToWorld(_ridePose.X, _ridePose.Z, _ridePose.Y, _ridePose.HeadingRad, seat.X, seat.Y, seat.Z, out sx, out sz, out sy);
                position = ToScene(sx, sy, sz);
                rotation = Quaternion.Euler(0f, _ridePose.HeadingRad * rad2Deg, 0f);
                return;
            }
            VehiclePose w = _walker.Interpolated;
            position = ToScene(w.X, w.Y + _jump.Height, w.Z);
            rotation = Quaternion.Euler(0f, w.HeadingRad * rad2Deg, 0f);
        }

        /// <summary>The seat point of a vehicle (ours: through its posed transform, so lean and roll carry the rider; a
        /// traffic agent: from its pose).</summary>
        private void SeatWorld(PlayerVehicle v, in SeatSocket seat, out double x, out float y, out double z)
        {
            if (v != null)
            {
                Vector3 local = new Vector3(seat.X, seat.Y, seat.Z);
                Vector3 scene = v.View.transform.TransformPoint(local);
                x = scene.x + _origin.X;
                y = scene.y + _origin.Y;
                z = scene.z + _origin.Z;
                return;
            }
            VehicleSeats.ToWorld(_ridePose.X, _ridePose.Z, _ridePose.Y, _ridePose.HeadingRad, seat.X, seat.Y, seat.Z, out x, out z, out y);
        }

        private Quaternion SeatRotation(PlayerVehicle v)
        {
            if (v != null) return v.View.transform.rotation;
            return Quaternion.Euler(0f, _ridePose.HeadingRad * 57.2957795f, 0f);
        }

        private float CurrentHeading()
        {
            if (_tr.Vehicle != null) return _tr.Vehicle.Driver.Interpolated.HeadingRad;
            if (_rideFound) return _ridePose.HeadingRad;
            return _walker.Interpolated.HeadingRad;
        }

        /// <summary>The player's own ground point (game metres).</summary>
        private void PlayerPoint(out double x, out float y, out double z)
        {
            if (_tr.Phase == Phase.Enter || _tr.Phase == Phase.Exit)
            {
                Vector3 p;
                Quaternion q;
                AvatarPlacement(out p, out q);
                x = p.x + _origin.X;
                y = p.y + _origin.Y;
                z = p.z + _origin.Z;
                return;
            }
            if (_mode == ExplorerMode.Ride && _current != null)
            {
                VehiclePose p = _current.Driver.Interpolated;
                x = p.X;
                y = p.Y;
                z = p.Z;
                return;
            }
            if (_mode == ExplorerMode.Passenger && _rideFound)
            {
                x = _ridePose.X;
                y = _ridePose.Y;
                z = _ridePose.Z;
                return;
            }
            VehiclePose w = _walker.Interpolated;
            x = w.X;
            y = w.Y + _jump.Height;
            z = w.Z;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Helpers

        private void RequireBound()
        {
            if (_ground == null) throw new InvalidOperationException("ExplorerController: call Bind(ground, origin) first");
        }

        private void Place(FixedStepDriver driver, double x, double z, float heading, float heightHint)
        {
            driver.Vehicle.Teleport(x, z, heading, _ground);
            if (!driver.Vehicle.HasGround) driver.Vehicle.Y = heightHint;
            driver.Snap();
        }

        private void SetMode(ExplorerMode mode, bool force)
        {
            bool changed = mode != _mode;
            _mode = mode;
            if (!changed && !force) return;
            Action<ExplorerMode> handler = ModeChanged;
            if (handler != null) handler(mode);
        }

        private void Raise(string key)
        {
            Action<string> h = Notice;
            if (h != null) h(key);
        }

        /// <summary>A point <paramref name="offset"/> metres to the right (negative: left) of heading at (x, z).</summary>
        private static void Side(double x, double z, float heading, double offset, out double sx, out double sz)
        {
            sx = x + Math.Cos(heading) * offset;
            sz = z - Math.Sin(heading) * offset;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _vehicles.Count; i++) _vehicles[i].StopEngine();
            _vehicles.Clear();
        }
    }
}
