using System;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Motion;
using Ghumante.Vehicles;
using Ghumante.Vehicles.Visuals;
using UnityEngine;

namespace Ghumante.Characters
{
    /// <summary>How the explorer gets around.</summary>
    public enum ExplorerMode : byte
    {
        Walk = 0,
        Ride = 1,
    }

    /// <summary>
    /// The player in Explore (M1 track D, ARCHITECTURE.md 7.6): a walker and a scooter, each simulated by Core's
    /// <see cref="ArcadeVehicle"/> at a fixed step with interpolation (<see cref="FixedStepDriver"/>), with the
    /// placeholder cartoon models (<see cref="ScooterModel"/>, <see cref="WalkerModel"/>) and dust and mud puffs.
    /// <list type="bullet">
    /// <item><b>Modes.</b> <see cref="ToggleMode"/> gets off (a little hop to the kerb side; the scooter stays parked;
    /// at speed it brakes first) or on (a hop onto the seat; a scooter left more than <see cref="CallBikeDistanceM"/>
    /// away, or out of the loaded world, rolls up beside the walker first).</item>
    /// <item><b>Frames.</b> Simulation positions are game metres (doubles); the models are placed at
    /// <c>world − origin</c>. <see cref="Bind"/> sets the ground and origin, <see cref="ShiftOrigin"/> follows the
    /// world's floating origin (WorldRoot.OriginShifted passes the delta).</item>
    /// <item><b>Driving it.</b> The owner calls <see cref="Tick"/> once per frame with the merged controls; there is no
    /// Update of its own, so input, simulation, streaming focus and camera run in a fixed order.</item>
    /// <item><b>Feel.</b> Landings and bumps squash the models, stuck recoveries and landings throw puffs, riding on
    /// gravel, dirt or mud kicks up dust or mud. <see cref="LastEvents"/> reports what happened (for haptics).</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ExplorerController : MonoBehaviour
    {
        /// <summary>Getting off brakes first above this speed.</summary>
        public const float DismountMaxSpeedMps = 2.5f;

        public const float HopSeconds = 0.42f;
        public const float HopHeightM = 0.55f;

        /// <summary>A scooter parked farther than this rolls up to the walker when they get on.</summary>
        public const float CallBikeDistanceM = 18f;

        /// <summary>How far to the side the rider steps off.</summary>
        public const float DismountSideM = 0.95f;

        /// <summary>Seconds the parked scooter keeps simulating (braked) after a dismount, to settle on the ground.</summary>
        public const float ParkSettleSeconds = 1.5f;

        private struct Hop
        {
            public bool Active;
            public bool Mounting;
            public float T;
            public double FromX, FromZ, ToX, ToZ;
            public float FromY, ToY;
            public float Heading;
        }

        private FixedStepDriver _bike;
        private FixedStepDriver _walker;
        private ScooterModel _scooter;
        private WalkerModel _walkerModel;
        private PuffEmitter _puffs;
        private IGroundQuery _ground;
        private WorldPos _origin;
        private ExplorerMode _mode = ExplorerMode.Ride;
        private bool _pendingDismount;
        private Hop _hop;
        private float _bikeSettle;
        private float _dustTimer;
        private StepEvents _events;
        private MotionRandom _jitter = new MotionRandom(911u);

        /// <summary>Builds the explorer (scooter, walker, puffs) as a new scene object. <paramref name="material"/> is a
        /// <c>Ghumante/ToonLit</c> material the caller owns.</summary>
        public static ExplorerController Create(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            var go = new GameObject("Explorer");
            ExplorerController c = go.AddComponent<ExplorerController>();
            c._bike = new FixedStepDriver(new ArcadeVehicle(VehicleTuning.Motorbike()));
            c._walker = new FixedStepDriver(new ArcadeVehicle(VehicleTuning.Walker()), FixedStepDriver.DefaultStepS, 0.1f);
            c._scooter = ScooterModel.Create(go.transform, material);
            c._walkerModel = WalkerModel.Create(go.transform, material);
            c._walkerModel.gameObject.SetActive(false);
            c._puffs = PuffEmitter.Create(go.transform, material, 24);
            return c;
        }

        /// <summary>Raised when the explorer got on or off the scooter (at the start of the hop off, the end of the hop on).</summary>
        public event Action<ExplorerMode> ModeChanged;

        public ExplorerMode Mode
        {
            get { return _mode; }
        }

        /// <summary>True during the mount or dismount hop (controls are ignored).</summary>
        public bool IsHopping
        {
            get { return _hop.Active; }
        }

        /// <summary>True while braking to get off.</summary>
        public bool DismountPending
        {
            get { return _pendingDismount; }
        }

        public FixedStepDriver Bike
        {
            get { return _bike; }
        }

        public FixedStepDriver Walker
        {
            get { return _walker; }
        }

        /// <summary>The body being controlled: the scooter when riding, else the walker.</summary>
        public FixedStepDriver Active
        {
            get { return _mode == ExplorerMode.Ride ? _bike : _walker; }
        }

        /// <summary>What happened during the last <see cref="Tick"/> (all fixed steps of the frame together).</summary>
        public StepEvents LastEvents
        {
            get { return _events; }
        }

        /// <summary>No bob, lean or squash on the models (Settings, Reduce motion).</summary>
        public bool ReducedMotion { get; set; }

        /// <summary>Road wetness 0..1 (monsoon; 0 in M1).</summary>
        public float Wetness { get; set; }

        /// <summary>The floating origin the models are placed against.</summary>
        public WorldPos Origin
        {
            get { return _origin; }
        }

        /// <summary>Where the explorer is now (interpolated, game metres): the streaming focus.</summary>
        public WorldPos Position
        {
            get
            {
                if (_hop.Active)
                {
                    float y;
                    double x, z;
                    HopPoint(out x, out y, out z);
                    return new WorldPos(x, y, z);
                }
                VehiclePose p = Active.Interpolated;
                return new WorldPos(p.X, p.Y, p.Z);
            }
        }

        /// <summary>The interpolated pose of the controlled body.</summary>
        public VehiclePose Pose
        {
            get { return Active.Interpolated; }
        }

        /// <summary>Speed of the controlled body, m/s (signed).</summary>
        public float SpeedMps
        {
            get { return Active.Vehicle.SpeedMps; }
        }

        /// <summary>The ground under the controlled body (last step).</summary>
        public GroundSample GroundUnder
        {
            get { return Active.Vehicle.Ground; }
        }

        /// <summary>True once the controlled body stands on loaded ground.</summary>
        public bool HasGround
        {
            get { return Active.Vehicle.HasGround; }
        }

        /// <summary>Uses <paramref name="ground"/> for simulation and places models against <paramref name="origin"/>.</summary>
        public void Bind(IGroundQuery ground, WorldPos origin)
        {
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _origin = origin;
        }

        /// <summary>The floating origin moved by <paramref name="delta"/> (new − old).</summary>
        public void ShiftOrigin(WorldPos delta)
        {
            _origin = _origin + delta;
            if (_puffs != null) _puffs.ShiftOrigin(new Vector3((float)delta.X, delta.Y, (float)delta.Z));
        }

        /// <summary>Scene position of a world position (against this controller's origin).</summary>
        public Vector3 ToScene(double x, float y, double z)
        {
            return new Vector3((float)(x - _origin.X), y - _origin.Y, (float)(z - _origin.Z));
        }

        /// <summary>
        /// Puts the explorer at (x, z) facing <paramref name="headingRad"/>, riding or on foot. Where the ground is not
        /// loaded yet the bodies wait at <paramref name="heightHint"/> and drop onto it once it streams in.
        /// </summary>
        public void Spawn(double x, double z, float headingRad, ExplorerMode mode, float heightHint)
        {
            RequireBound();
            _hop = default(Hop);
            _pendingDismount = false;
            _bikeSettle = 0f;
            _puffs.Clear();
            Place(_bike, x, z, headingRad, heightHint);
            double sx, sz;
            Side(x, z, headingRad, -DismountSideM, out sx, out sz);
            Place(_walker, sx, sz, headingRad, heightHint);
            SetMode(mode, true);
            Render(0f);
        }

        /// <summary>Moves the explorer (both bodies) to (x, z): fast travel and the debug teleport.</summary>
        public void Teleport(double x, double z, float headingRad, float heightHint)
        {
            Spawn(x, z, headingRad, _mode, heightHint);
        }

        /// <summary>
        /// Gets off (hop to the side, scooter parked; at speed it brakes first) or on (hop onto the seat; a far scooter
        /// rolls up first). Returns false while a hop is under way.
        /// </summary>
        public bool ToggleMode()
        {
            RequireBound();
            if (_hop.Active) return false;
            if (_mode == ExplorerMode.Ride)
            {
                if (Math.Abs(_bike.Vehicle.SpeedMps) > DismountMaxSpeedMps)
                {
                    _pendingDismount = !_pendingDismount;
                    return true;
                }
                BeginDismount();
            }
            else
            {
                BeginMount();
            }
            return true;
        }

        /// <summary>
        /// One frame: steps the controlled body with <paramref name="controls"/> (camera-relative on foot, using
        /// <paramref name="cameraYawRad"/>), plays hops, puffs and squash, and poses the models. While
        /// <paramref name="paused"/> nothing moves (the models still follow origin shifts).
        /// </summary>
        public void Tick(float dt, in ControlFrame controls, float cameraYawRad, bool paused)
        {
            _events = StepEvents.None;
            if (_ground == null) return;
            if (paused || !(dt > 0f))
            {
                Render(0f);
                return;
            }

            if (_hop.Active) AdvanceHop(dt);

            var braked = new DriveInput(0f, 1f, 0f, false);
            if (_mode == ExplorerMode.Ride)
            {
                DriveInput input = ControlMapper.Ride(controls);
                if (_pendingDismount) input = new DriveInput(0f, 1f, input.Steer, false);
                _events = _bike.Advance(dt, input, _ground, Wetness);
                if (_pendingDismount && Math.Abs(_bike.Vehicle.SpeedMps) <= DismountMaxSpeedMps) BeginDismount();
            }
            else
            {
                // On foot; during either hop the walker stands still (the hop is drawn on top) and the scooter waits, braked.
                DriveInput input = _hop.Active ? new DriveInput(0f, 0f, 0f, false)
                                               : ControlMapper.WalkInput(_walker.Vehicle, controls, cameraYawRad, dt);
                _events = _walker.Advance(dt, input, _ground, Wetness);
                if (_bikeSettle > 0f || _hop.Active)
                {
                    _bikeSettle -= dt;
                    _bike.Advance(dt, braked, _ground, Wetness);
                }
            }

            React(dt);
            Render(dt);
            _puffs.Tick(dt);
        }

        /// <summary>Hop and nudge out of a stuck spot now (a "reset" button); false without ground.</summary>
        public bool Recover()
        {
            RequireBound();
            if (_hop.Active) return false;
            return Active.Recover(_ground);
        }

        /// <summary>Camera target: the controlled body's ground point in scene space, its heading and lean.</summary>
        public void GetCameraTarget(out Vector3 scenePosition, out float headingRad, out float speedMps, out float leanRad)
        {
            if (_hop.Active)
            {
                double x, z;
                float y;
                HopPoint(out x, out y, out z);
                scenePosition = ToScene(x, y, z);
                headingRad = _hop.Heading;
                speedMps = 0f;
                leanRad = 0f;
                return;
            }
            VehiclePose p = Active.Interpolated;
            scenePosition = ToScene(p.X, p.Y, p.Z);
            headingRad = p.HeadingRad;
            speedMps = p.SpeedMps;
            leanRad = p.Lean;
        }

        // -------------------------------------------------------------------------------------------------------------

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
            _scooter.RiderVisible = mode == ExplorerMode.Ride && !_hop.Active;
            _walkerModel.gameObject.SetActive(mode == ExplorerMode.Walk || _hop.Active);
            if (!changed && !force) return;
            Action<ExplorerMode> handler = ModeChanged;
            if (handler != null) handler(mode);
        }

        private void BeginDismount()
        {
            _pendingDismount = false;
            VehiclePose bike = _bike.Current;
            double tx, tz;
            float heading = bike.HeadingRad;
            if (!FindStepOff(bike, out tx, out tz)) return;
            Place(_walker, tx, tz, heading, bike.Y);
            Vector3 seat = SeatOffset(heading);
            _hop = new Hop
            {
                Active = true, Mounting = false, T = 0f, Heading = heading,
                FromX = bike.X + seat.x, FromZ = bike.Z + seat.z, FromY = bike.Y + seat.y,
                ToX = tx, ToZ = tz, ToY = _walker.Current.Y,
            };
            _bikeSettle = ParkSettleSeconds;
            if (!ReducedMotion) _walkerModel.KickSquash(-0.6f);
            SetMode(ExplorerMode.Walk, false);
        }

        private void BeginMount()
        {
            VehiclePose walker = _walker.Current;
            VehiclePose bike = _bike.Current;
            double dx = bike.X - walker.X, dz = bike.Z - walker.Z;
            if (!bike.HasGround || dx * dx + dz * dz > CallBikeDistanceM * CallBikeDistanceM)
            {
                // The scooter rolls up beside the walker, facing the same way.
                double bx, bz;
                Side(walker.X, walker.Z, walker.HeadingRad, DismountSideM * 1.2, out bx, out bz);
                GroundSample s;
                if (!_ground.TrySample(bx, bz, out s))
                {
                    bx = walker.X;
                    bz = walker.Z;
                }
                Place(_bike, bx, bz, walker.HeadingRad, walker.Y);
                _puffs.Burst(ToScene(bx, _bike.Current.Y, bz), 5, 0.5f, PuffKind.Smoke);
                bike = _bike.Current;
            }
            Vector3 seat = SeatOffset(bike.HeadingRad);
            _hop = new Hop
            {
                Active = true, Mounting = true, T = 0f, Heading = bike.HeadingRad,
                FromX = walker.X, FromZ = walker.Z, FromY = walker.Y,
                ToX = bike.X + seat.x, ToZ = bike.Z + seat.z, ToY = bike.Y + seat.y,
            };
            _bikeSettle = 0f;
            if (!ReducedMotion) _walkerModel.KickSquash(-0.6f);
            SetMode(_mode, false);
        }

        private void AdvanceHop(float dt)
        {
            _hop.T += dt / HopSeconds;
            if (_hop.T < 1f) return;
            bool mounting = _hop.Mounting;
            _hop = default(Hop);
            if (mounting)
            {
                if (!ReducedMotion) _scooter.KickSquash(0.8f);
                SetMode(ExplorerMode.Ride, false);
            }
            else
            {
                if (!ReducedMotion) _walkerModel.KickSquash(0.9f);
                _puffs.Burst(ToScene(_walker.Current.X, _walker.Current.Y, _walker.Current.Z), 3, 0.35f, PuffKind.Smoke);
                SetMode(ExplorerMode.Walk, false);
            }
        }

        private void HopPoint(out double x, out float y, out double z)
        {
            float t = Mathf.Clamp01(_hop.T);
            x = _hop.FromX + (_hop.ToX - _hop.FromX) * t;
            z = _hop.FromZ + (_hop.ToZ - _hop.FromZ) * t;
            y = _hop.FromY + (_hop.ToY - _hop.FromY) * t + HopHeightM * 4f * t * (1f - t);
        }

        /// <summary>Where to step off: the left of the scooter, else the right, else behind it.</summary>
        private bool FindStepOff(in VehiclePose bike, out double x, out double z)
        {
            GroundSample s;
            for (int k = 0; k < 3; k++)
            {
                if (k < 2) Side(bike.X, bike.Z, bike.HeadingRad, k == 0 ? -DismountSideM : DismountSideM, out x, out z);
                else
                {
                    x = bike.X - Math.Sin(bike.HeadingRad) * 1.4;
                    z = bike.Z - Math.Cos(bike.HeadingRad) * 1.4;
                }
                if (_ground.TrySample(x, z, out s)) return true;
            }
            x = bike.X;
            z = bike.Z;
            return bike.HasGround;
        }

        /// <summary>A point <paramref name="offset"/> metres to the right (negative: left) of heading at (x, z).</summary>
        private static void Side(double x, double z, float heading, double offset, out double sx, out double sz)
        {
            // Right of heading h is (cos h, −sin h) in game X/Z.
            sx = x + Math.Cos(heading) * offset;
            sz = z - Math.Sin(heading) * offset;
        }

        /// <summary>The seat relative to the scooter's ground point, rotated by its heading (game X/Z, Y up).</summary>
        private static Vector3 SeatOffset(float heading)
        {
            Vector3 local = ScooterModel.SeatLocal;
            float s = Mathf.Sin(heading), c = Mathf.Cos(heading);
            return new Vector3(local.z * s + local.x * c, local.y, local.z * c - local.x * s);
        }

        private void React(float dt)
        {
            ArcadeVehicle v = Active.Vehicle;
            bool riding = _mode == ExplorerMode.Ride;
            if ((_events & StepEvents.Landed) != 0)
            {
                float strength = Mathf.Clamp(v.LastLandingSpeedMps / 4f, 0.3f, 1.5f);
                if (!ReducedMotion)
                {
                    if (riding) _scooter.KickSquash(strength);
                    else _walkerModel.KickSquash(strength);
                }
                if (riding) _puffs.Burst(_scooter.RearContact, 5, 0.45f, PuffFor(v.Surface, true));
            }
            else if ((_events & StepEvents.Bump) != 0 && riding && !ReducedMotion)
            {
                _scooter.KickSquash(Mathf.Clamp(v.LastBumpStrength / 6f, 0.15f, 0.6f));
            }
            if ((_events & StepEvents.StuckRecovered) != 0)
            {
                VehiclePose p = Active.Current;
                _puffs.Burst(ToScene(p.X, p.Y, p.Z), 6, 0.55f, PuffFor(v.Surface, true));
                if (!ReducedMotion)
                {
                    if (riding) _scooter.KickSquash(-0.8f);
                    else _walkerModel.KickSquash(-0.8f);
                }
            }

            // Dust or mud behind the rear wheel on unpaved ground.
            float speed = Math.Abs(v.SpeedMps);
            if (riding && v.HasGround && !v.Airborne && v.Surface != SurfaceGroup.Paved && speed > 4f)
            {
                _dustTimer -= dt;
                if (_dustTimer <= 0f)
                {
                    _dustTimer = Mathf.Lerp(0.16f, 0.05f, Mathf.Clamp01(speed / 20f));
                    float fx = Mathf.Sin(v.HeadingRad), fz = Mathf.Cos(v.HeadingRad);
                    float side = _jitter.Range(-0.8f, 0.8f);
                    var velocity = new Vector3(-fx * (0.8f + 0.06f * speed) + fz * side, 0.7f, -fz * (0.8f + 0.06f * speed) - fx * side);
                    _puffs.Emit(_scooter.RearContact, velocity, Mathf.Lerp(0.35f, 0.7f, Mathf.Clamp01(speed / 18f)), PuffFor(v.Surface, false));
                }
            }
            else
            {
                _dustTimer = 0f;
            }
        }

        private static PuffKind PuffFor(SurfaceGroup surface, bool impact)
        {
            switch (surface)
            {
                case SurfaceGroup.Mud: return PuffKind.Mud;
                case SurfaceGroup.Paved: return impact ? PuffKind.Smoke : PuffKind.Dust;
                default: return PuffKind.Dust;
            }
        }

        private void Render(float dt)
        {
            VehiclePose bike = _bike.Interpolated;
            _scooter.Apply(bike, ToScene(bike.X, bike.Y, bike.Z), dt, ReducedMotion);
            if (!_walkerModel.gameObject.activeSelf) return;
            if (_hop.Active)
            {
                double x, z;
                float y;
                HopPoint(out x, out y, out z);
                VehiclePose still = _walker.Current;
                still.HeadingRad = _hop.Heading;
                still.SpeedMps = 0f;
                _walkerModel.Apply(still, ToScene(x, y, z), 0f, dt, ReducedMotion);
            }
            else
            {
                VehiclePose walker = _walker.Interpolated;
                _walkerModel.Apply(walker, ToScene(walker.X, walker.Y, walker.Z), 0f, dt, ReducedMotion);
            }
        }
    }
}
