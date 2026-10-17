using System;
using System.Collections.Generic;
using Ghumante.Characters.Cameras;
using Ghumante.Core.Aviation;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Synth;
using Ghumante.World;
using Ghumante.World.Aviation;
using Ghumante.World.Life;
using Ghumante.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.Characters.Rides
{
    /// <summary>
    /// Flying from Tribhuvan International Airport (owner playtest #11; docs/research/w2/ref_flight.md): the parked
    /// aircraft on TIA's real stands (<see cref="FlightStands"/>), drawn from the flight package's models
    /// (<see cref="FlightMesher"/>, LOD by distance) with solid fuselages the explorer walks round; walking up to one
    /// offers "Board" and the Action button (E) boards it as pilot: the explorer sits in the cockpit (hidden), the engines
    /// start, and <see cref="FlightModel"/> flies it from the merged controls (<see cref="AircraftInput"/>) and the HUD's
    /// touch controls (<see cref="FlightTouch"/>). The chase and cockpit cameras (<see cref="FlightCamera"/>) take over the
    /// main camera; the propellers and rotors spin with the power; the engine voice is the aircraft synth; the ambient
    /// air traffic keeps off the runway while the player is near it (<see cref="LifeHost.HoldRunwayForPlayer"/>); bounces
    /// and resets show friendly toasts (<see cref="Notice"/>). Standing still on the ground, holding E (or Get out) puts
    /// the explorer down by the main door and leaves the aircraft parked where it stopped. Main thread only.
    /// </summary>
    public sealed class AircraftRide : IDisposable
    {
        /// <summary>Parked aircraft are drawn within this distance; LOD0 within <see cref="Lod0M"/>, LOD1 within
        /// <see cref="Lod1M"/>.</summary>
        public const float DrawRangeM = 4000f, Lod0M = 150f, Lod1M = 700f;

        /// <summary>Hold E this long at a stop to get out.</summary>
        public const float ExitHoldS = 0.8f;

        /// <summary>The ambient traffic keeps off the runway while the flown aircraft is this close to it and lower than
        /// <see cref="HoldRunwayAglM"/> above the ground.</summary>
        public const float HoldRunwayRadiusM = 6000f, HoldRunwayAglM = 1200f;

        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private readonly Material _material;
        private readonly AviationConfig _airport;
        private readonly FlightStands _stands;
        private readonly FlightWorld _world;
        private readonly FlightBounds _bounds;
        private readonly AircraftInput _input = new AircraftInput();
        private readonly FlightCamera _camera = new FlightCamera();
        private readonly Dictionary<int, Mesh> _meshes = new Dictionary<int, Mesh>();
        private readonly MeshData _scratch = new MeshData(4096, 12288);
        private readonly float[] _standY;
        private readonly bool[] _standGround;
        private readonly MaterialPropertyBlock _noOutline = new MaterialPropertyBlock();

        private FlightModel _model;
        private int _standIndex = -1;
        private FlightView _view = FlightView.Chase;
        private IAircraftSound _voice;
        private Transform _avatarRoot;
        private float _exitHeld, _propAngle, _rotorAngle, _groundTimer;
        private bool _offered, _collidersDirty = true, _holding;
        private FlightStand _offer;
        private FlightEvents _events;
        private TileGroundQuery _registeredIn;
        private ulong _colliderKey;

        /// <summary>A short message for the HUD (a localisation key), e.g. "flight.toast.reset_runway".</summary>
        public event Action<string> Notice;

        /// <summary>Every key <see cref="Notice"/> raises (the string tables carry each in English and Nepali).</summary>
        public static readonly string[] NoticeKeys =
        {
            "flight.toast.boarded", "flight.toast.boarded_heli", "flight.toast.got_out", "flight.toast.stop_first", "flight.toast.bump_building",
            "flight.toast.bump_water", "flight.toast.bump_hard", "flight.toast.bump_ground", "flight.toast.reset_runway", "flight.toast.reset_air",
            "flight.toast.turn_back", "flight.toast.stall", "flight.toast.smooth_landing", "flight.toast.landed", "flight.toast.airborne",
            "flight.toast.mind_obstacle",
        };

        /// <summary>True from boarding until the explorer is back on foot.</summary>
        public bool Flying
        {
            get { return _model != null; }
        }

        /// <summary>A parked aircraft is within reach of the explorer: the HUD offers "Board".</summary>
        public bool Offered
        {
            get { return _offered && _model == null; }
        }

        /// <summary>The craft offered for boarding (valid while <see cref="Offered"/>).</summary>
        public FlightCraft OfferedCraft
        {
            get { return _offer.Craft; }
        }

        /// <summary>The aircraft being flown (null on foot).</summary>
        public FlightModel Model
        {
            get { return _model; }
        }

        public FlightView View
        {
            get { return _view; }
        }

        public FlightStands Stands
        {
            get { return _stands; }
        }

        /// <summary>The throttle lever (the HUD's slider mirrors it).</summary>
        public float Lever
        {
            get { return _input.Lever; }
        }

        /// <summary>Events of the last frame (tests, haptics).</summary>
        public FlightEvents LastEvents
        {
            get { return _events; }
        }

        /// <summary>Where the aircraft is (the streaming focus while flying).</summary>
        public WorldPos Position
        {
            get
            {
                if (_model == null) return default(WorldPos);
                FlightPose p = _model.Interpolated;
                return new WorldPos(p.X, (float)p.Y, p.Z);
            }
        }

        private AircraftRide(Material material, AviationConfig airport, FlightBounds bounds)
        {
            _material = material;
            _airport = airport;
            _bounds = bounds;
            _stands = FlightStands.For(airport);
            _world = new FlightWorld(airport, null, null);
            _standY = new float[_stands.Count];
            _standGround = new bool[_stands.Count];
            _noOutline.SetFloat(OutlineWidthId, 0f);
        }

        /// <summary>
        /// The ride for a region: null without an airport. <paramref name="material"/> is the explorer's ToonLit material
        /// (vertex colours, material channels); the bounds come from the region's manifest (<see cref="FlightBounds.ForRegion"/>).
        /// </summary>
        public static AircraftRide Create(Material material, AviationConfig airport, double minX, double minZ, double maxX, double maxZ)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (airport == null) return null;
            return new AircraftRide(material, airport, FlightBounds.ForRegion(minX, minZ, maxX, maxZ, airport));
        }

        // ------------------------------------------------------------------------------------------------- frame

        /// <summary>
        /// One frame, before the explorer's own tick. On foot: offers the parked aircraft within reach and boards it when
        /// the Action button is pressed (the press is consumed). Flying: reads the controls, steps the flight, handles
        /// getting out, the voice and the runway hold, and keeps the hidden avatar in the cockpit. Returns true while the
        /// aircraft has the controls (the session then does not tick the explorer).
        /// </summary>
        public bool Frame(float dt, ref ControlFrame frame, in FlightTouch touch, ExplorerController explorer, WorldRoot world, ChaseCameraRig rig,
                          bool blocked)
        {
            _events = FlightEvents.None;
            if (explorer == null || world == null) return false;
            BindWorld(world);
            if (_model == null)
            {
                RefreshStandGround(dt, world);
                WorldPos at = explorer.Position;
                FlightStand s = default(FlightStand);
                float d;
                _offered = !blocked && explorer.Mode == ExplorerMode.Walk && !explorer.IsHopping && _stands.TryBoardable(at.X, at.Z, out s, out d);
                if (_offered) _offer = s;
                if (_offered && frame.ToggleMode)
                {
                    Board(_offer, explorer, world, rig);
                    frame.ToggleMode = false;
                    frame.ActionHeld = false;
                    _exitHeld = -1f; // the E that boarded must be released before it can get out
                }
                return _model != null;
            }

            LifeHost life = world.Life;
            if (blocked)
            {
                SetHold(life, false);
                return true;
            }
            bool view, exitTap;
            FlightInput input = _input.Read(dt, frame, touch, out view, out exitTap);
            if (view) _view = _view == FlightView.Chase ? FlightView.Cockpit : FlightView.Chase;
            _events = _model.Advance(dt, input);
            if ((_events & FlightEvents.ResetRunway) != 0) _input.Lever = 0f;
            else if ((_events & FlightEvents.ResetAir) != 0) _input.Lever = _model.Throttle;
            if ((_events & (FlightEvents.ResetRunway | FlightEvents.ResetAir)) != 0) _camera.Snap();
            RaiseNotices();

            // Getting out: hold E (released since boarding) or tap Get out while stopped on the ground.
            if (!frame.ActionHeld) _exitHeld = 0f;
            else if (_exitHeld >= 0f) _exitHeld += dt;
            bool wantsOut = exitTap || _exitHeld >= ExitHoldS;
            if (wantsOut)
            {
                if (_model.CanExit)
                {
                    GetOut(explorer, world, rig);
                    return false;
                }
                // E is also the right rudder: only a tap of Get out, or a long hold on the ground, explains why not.
                if (exitTap || _model.OnGround && _exitHeld >= ExitHoldS && _exitHeld - dt < ExitHoldS) Raise("flight.toast.stop_first");
            }

            // The ambient traffic keeps off the runway while the player is near it and low.
            bool near = _world.DistanceToRunway(_model.X, _model.Z) < HoldRunwayRadiusM && _model.Agl < HoldRunwayAglM;
            SetHold(life, near);
            UpdateVoice(world);
            FlightPose p = _model.Interpolated;
            if (_avatarRoot != null) _avatarRoot.position = ToScene(world.Origin, p.X, (float)p.Y + _model.Spec.EyeY, p.Z);
            float spin = _model.Spec.Rotorcraft ? _model.RotorRpm : (_model.EngineRunning ? 0.25f + 0.75f * _model.Power : 0f);
            _propAngle = Mathf.Repeat(_propAngle + spin * 360f * 5f * dt, 360f);
            _rotorAngle = Mathf.Repeat(_rotorAngle + spin * 360f * 6.5f * dt, 360f);
            return true;
        }

        private void BindWorld(WorldRoot world)
        {
            TileGroundQuery q = world.GroundQuery;
            _world.SetGround(q, q);
            if (q != _registeredIn)
            {
                _registeredIn = q;
                _collidersDirty = true;
            }
            if (_collidersDirty && q != null) RegisterColliders(q);
        }

        private void SetHold(LifeHost life, bool hold)
        {
            if (hold == _holding && life != null) return;
            _holding = hold;
            if (life != null) life.HoldRunwayForPlayer = hold;
        }

        // ---------------------------------------------------------------------------------------------- boarding

        private void Board(FlightStand s, ExplorerController explorer, WorldRoot world, ChaseCameraRig rig)
        {
            FlightSpec spec = FlightSpec.For(s.Craft);
            _stands.Take(s.Index);
            _standIndex = s.Index;
            _collidersDirty = true;
            BindWorld(world);
            _model = new FlightModel(spec, _world) { Bounds = _bounds };
            _model.PlaceOnGround(s.X, s.Z, s.HeadingRad);
            _model.EngineRunning = true;
            _input.Lever = 0f;
            _input.Enabled = true;
            _view = FlightView.Chase;
            _camera.Snap();
            // The explorer climbs in: hide the avatar (it rides along in the cockpit for the audio listener).
            _avatarRoot = AvatarRoot(explorer);
            if (_avatarRoot != null) _avatarRoot.gameObject.SetActive(false);
            ISoundService sound = world.Sound;
            if (sound != null) _voice = sound.OpenAircraft(SoundClassOf(s.Craft), (uint)(s.Index * 7919 + 17));
            sound?.PlayUi(BankSound.JumpWhoosh, -8f);
            Raise(spec.Rotorcraft ? "flight.toast.boarded_heli" : "flight.toast.boarded");
        }

        private void GetOut(ExplorerController explorer, WorldRoot world, ChaseCameraRig rig)
        {
            FlightSpec spec = _model.Spec;
            FlightPose p = _model.Current;
            double ex, ez;
            FlightStands.ExitPoint(spec, p.X, p.Z, p.Heading, out ex, out ez);
            _stands.Park(_standIndex, p.X, p.Z, p.Heading);
            _collidersDirty = true;
            SetHold(world.Life, false);
            if (_voice != null)
            {
                _voice.Release();
                _voice = null;
            }
            _model = null;
            _standIndex = -1;
            _input.Enabled = false;
            if (_avatarRoot != null) _avatarRoot.gameObject.SetActive(true);
            _avatarRoot = null;
            BindWorld(world);
            // Face away from the aircraft (to its left), on the ground by the door.
            explorer.Teleport(ex, ez, ArcadeVehicle.WrapAngle(p.Heading - Mathf.PI * 0.5f), (float)p.Y);
            if (rig != null) rig.Snap();
            Raise("flight.toast.got_out");
        }

        /// <summary>The avatar's root under the explorer (the child holding the head).</summary>
        private static Transform AvatarRoot(ExplorerController explorer)
        {
            Transform head = explorer.Head;
            if (head == null) return null;
            Transform t = head;
            while (t.parent != null && t.parent != explorer.transform) t = t.parent;
            return t.parent == explorer.transform ? t : null;
        }

        private static AircraftSoundClass SoundClassOf(FlightCraft c)
        {
            switch (c)
            {
                case FlightCraft.Stol: return AircraftSoundClass.StolTurboprop;
                case FlightCraft.Helicopter: return AircraftSoundClass.Helicopter;
                default: return AircraftSoundClass.Turboprop;
            }
        }

        private void UpdateVoice(WorldRoot world)
        {
            if (_voice == null) return;
            FlightPose p = _model.Interpolated;
            Vector3 s = ToScene(world.Origin, p.X, (float)p.Y + 2f, p.Z);
            float reverse = _model.OnGround && _model.Throttle < 0.05f && _model.Airspeed > 20f ? 0.6f : 0f;
            _voice.Set(s.x, s.y, s.z, (float)_model.Vx, (float)_model.Vy, (float)_model.Vz, _model.EngineRunning ? Math.Max(0.12f, _model.Power) : 0f,
                       reverse, _model.Agl, _model.OnGround);
        }

        // -------------------------------------------------------------------------------------------- notices

        private void RaiseNotices()
        {
            FlightEvents e = _events;
            if ((e & FlightEvents.Bounce) != 0)
            {
                switch (_model.LastBounceCause)
                {
                    case FlightBounceCause.Building: Raise("flight.toast.bump_building"); break;
                    case FlightBounceCause.Water: Raise("flight.toast.bump_water"); break;
                    case FlightBounceCause.HardLanding: Raise("flight.toast.bump_hard"); break;
                    default: Raise("flight.toast.bump_ground"); break;
                }
            }
            if ((e & FlightEvents.ResetRunway) != 0) Raise("flight.toast.reset_runway");
            if ((e & FlightEvents.ResetAir) != 0) Raise("flight.toast.reset_air");
            if ((e & FlightEvents.TurnBack) != 0) Raise("flight.toast.turn_back");
            if ((e & FlightEvents.Stall) != 0) Raise("flight.toast.stall");
            if ((e & FlightEvents.Touchdown) != 0) Raise(_model.LastTouchdownSinkMps < 1.5f ? "flight.toast.smooth_landing" : "flight.toast.landed");
            if ((e & FlightEvents.LiftOff) != 0) Raise("flight.toast.airborne");
            if ((e & FlightEvents.Bump) != 0 && _model.OnGround) Raise("flight.toast.mind_obstacle");
        }

        private void Raise(string key)
        {
            Action<string> h = Notice;
            if (h != null) h(key);
        }

        // ------------------------------------------------------------------------------------------------ camera

        /// <summary>Places the main camera for the flown aircraft (call in LateUpdate instead of the chase rig).</summary>
        public void UpdateCamera(Camera cam, float dt, in ControlFrame frame, WorldPos origin)
        {
            if (_model == null || cam == null) return;
            FlightPose p = _model.Interpolated;
            FlightCameraPose c = _camera.Update(dt, p, _model.Vx, _model.Vy, _model.Vz, _model.Spec, _view, frame.LookYawDeg, frame.LookPitchDeg,
                                                frame.Looking, _world);
            Vector3 pos = ToScene(origin, c.X, (float)c.Y, c.Z);
            Quaternion rot = Quaternion.Euler(-c.Pitch * Mathf.Rad2Deg, c.Yaw * Mathf.Rad2Deg, -c.Roll * Mathf.Rad2Deg);
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = Mathf.MoveTowards(cam.fieldOfView, c.FovDeg, 40f * Mathf.Max(0f, dt));
            float near = _view == FlightView.Cockpit ? 0.12f : 0.5f;
            if (!Mathf.Approximately(cam.nearClipPlane, near)) cam.nearClipPlane = near;
        }

        // ---------------------------------------------------------------------------------------------- drawing

        /// <summary>Draws the parked aircraft near the camera and the flown one with its spinning propellers or rotors.</summary>
        public void Draw(WorldPos origin, Vector3 cameraScene)
        {
            double camX = origin.X + cameraScene.x, camZ = origin.Z + cameraScene.z;
            for (int i = 0; i < _stands.Count; i++)
            {
                FlightStand s = _stands[i];
                if (s.Taken || !_standGround[i]) continue;
                double dx = s.X - camX, dz = s.Z - camZ;
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (d > DrawRangeM) continue;
                int lod = d < Lod0M ? 0 : d < Lod1M ? 1 : 2;
                Mesh mesh = Parked(s.Craft, s.Livery, lod);
                Matrix4x4 m = Matrix4x4.TRS(ToScene(origin, s.X, _standY[i], s.Z), Quaternion.Euler(0f, s.HeadingRad * Mathf.Rad2Deg, 0f), Vector3.one);
                Render(mesh, m, lod == 0, false);
            }
            if (_model == null) return;
            FlightPose p = _model.Interpolated;
            FlightCraft craft = _model.Spec.Craft;
            byte livery = _stands[_standIndex].Livery;
            Matrix4x4 body = Matrix4x4.TRS(ToScene(origin, p.X, (float)p.Y, p.Z),
                                           Quaternion.Euler(-p.Pitch * Mathf.Rad2Deg, p.Heading * Mathf.Rad2Deg, -p.Bank * Mathf.Rad2Deg), Vector3.one);
            bool cockpit = _view == FlightView.Cockpit;
            if (cockpit) Render(Part(craft, livery, FlightPart.Cockpit), body, false, true);
            else Render(Part(craft, livery, FlightPart.Body), body, true, false);
            Render(Part(craft, livery, FlightPart.Frame), body, !cockpit, false);
            if (_model.Spec.FixedGear || _model.Gear > 0.5f) Render(Part(craft, livery, FlightPart.Gear), body, !cockpit, false);
            if (_model.Spec.Rotorcraft)
            {
                float rx, ry, rz;
                FlightMesher.RotorMount(out rx, out ry, out rz);
                Render(Part(craft, livery, FlightPart.Rotor), body * Matrix4x4.TRS(new Vector3(rx, ry, rz), Quaternion.Euler(0f, _rotorAngle, 0f), Vector3.one), false,
                       false);
                FlightMesher.TailRotorMount(out rx, out ry, out rz);
                Render(Part(craft, livery, FlightPart.TailRotor),
                       body * Matrix4x4.TRS(new Vector3(rx, ry, rz), Quaternion.Euler(_rotorAngle * 4.7f, 0f, 0f), Vector3.one), false, false);
                return;
            }
            for (int i = 0; i < FlightMesher.PropCount(craft); i++)
            {
                float px, py, pz;
                bool cw;
                FlightMesher.PropMount(craft, i, out px, out py, out pz, out cw);
                Matrix4x4 prop = body * Matrix4x4.TRS(new Vector3(px, py, pz), Quaternion.Euler(0f, 0f, (cw ? -1f : 1f) * _propAngle + i * 23f), Vector3.one);
                Render(Part(craft, livery, FlightPart.Prop), prop, false, false);
            }
        }

        private void Render(Mesh mesh, in Matrix4x4 m, bool shadows, bool noOutline)
        {
            if (mesh == null) return;
            var rp = new RenderParams(_material)
            {
                shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                matProps = noOutline ? _noOutline : null,
            };
            Graphics.RenderMesh(rp, mesh, 0, m);
        }

        private Mesh Parked(FlightCraft craft, byte livery, int lod)
        {
            int key = ((int)craft * 16 + livery) * 64 + lod;
            Mesh mesh;
            if (_meshes.TryGetValue(key, out mesh)) return mesh;
            _scratch.Clear();
            FlightMesher.BuildParked(craft, livery, lod, _scratch);
            mesh = MeshUpload.CreateWhole(_scratch, "aircraft_" + craft + "_" + livery + "_lod" + lod);
            _meshes[key] = mesh;
            return mesh;
        }

        private Mesh Part(FlightCraft craft, byte livery, FlightPart part)
        {
            int key = ((int)craft * 16 + livery) * 64 + 8 + (int)part;
            Mesh mesh;
            if (_meshes.TryGetValue(key, out mesh)) return mesh;
            _scratch.Clear();
            if (FlightMesher.Build(craft, part, livery, 0, _scratch) <= 0)
            {
                _meshes[key] = null;
                return null;
            }
            mesh = MeshUpload.CreateWhole(_scratch, "aircraft_" + craft + "_" + part);
            _meshes[key] = mesh;
            return mesh;
        }

        private static Vector3 ToScene(WorldPos origin, double x, float y, double z)
        {
            return new Vector3((float)(x - origin.X), y - origin.Y, (float)(z - origin.Z));
        }

        // ------------------------------------------------------------------------------- ground and colliders

        /// <summary>Samples the ground under each parked aircraft now and then (tiles stream in and out).</summary>
        private void RefreshStandGround(float dt, WorldRoot world)
        {
            _groundTimer -= dt;
            if (_groundTimer > 0f) return;
            _groundTimer = 1f;
            IGroundQuery g = world.Ground;
            bool changed = false;
            for (int i = 0; i < _stands.Count; i++)
            {
                FlightStand s = _stands[i];
                GroundSample gs;
                bool has = g != null && g.TrySample(s.X, s.Z, out gs);
                float y = has ? HighestUnder(g, s) : 0f;
                if (has != _standGround[i] || has && Math.Abs(y - _standY[i]) > 0.02f) changed = true;
                _standGround[i] = has;
                _standY[i] = y;
            }
            if (changed) _collidersDirty = true;
        }

        /// <summary>The highest ground under the aircraft's wheels (so no wheel sinks into a slope).</summary>
        private static float HighestUnder(IGroundQuery g, in FlightStand s)
        {
            FlightSpec spec = FlightSpec.For(s.Craft);
            float best = float.NegativeInfinity;
            double fx = Math.Sin(s.HeadingRad), fz = Math.Cos(s.HeadingRad), rx = fz, rz = -fx;
            double half = 0.5 * spec.WheelbaseM, track = 0.5 * spec.TrackM;
            for (int k = 0; k < 3; k++)
            {
                double ax = k == 0 ? fx * half : -fx * half * 0.1 + rx * (k == 1 ? -track : track);
                double az = k == 0 ? fz * half : -fz * half * 0.1 + rz * (k == 1 ? -track : track);
                GroundSample gs;
                if (g.TrySample(s.X + ax, s.Z + az, out gs) && gs.Height > best) best = gs.Height;
            }
            if (float.IsNegativeInfinity(best))
            {
                GroundSample c;
                best = g.TrySample(s.X, s.Z, out c) ? c.Height : 0f;
            }
            return best;
        }

        /// <summary>
        /// Solid fuselages (and the low wing, nacelles and helicopter booms) of the parked aircraft in the world's ground
        /// query, so the explorer walks round them and taxiing aircraft bump them; high wings stay above head height.
        /// Registered under a level-16 tile key of their own (never a streamed tile's).
        /// </summary>
        private void RegisterColliders(TileGroundQuery q)
        {
            _collidersDirty = false;
            if (_stands.Count == 0) return;
            if (_colliderKey == 0)
            {
                FlightStand first = _stands[0];
                _colliderKey = TileId.At(TileId.MaxLevel, first.X, first.Z).Key;
            }
            TileId tile = TileId.FromKey(_colliderKey);
            var c = new StructureColliders();
            for (int i = 0; i < _stands.Count; i++)
            {
                FlightStand s = _stands[i];
                if (s.Taken || !_standGround[i]) continue;
                FlightSpec spec = FlightSpec.For(s.Craft);
                float y = _standY[i];
                float yaw = Mathf.PI * 0.5f - s.HeadingRad; // box X along the fuselage (counter-clockwise from east)
                double lx = s.X - tile.X0, lz = s.Z - tile.Z0;
                c.Boxes.Add(new OrientedBox
                {
                    CX = lx, CZ = lz, CY = y + spec.FuselageCentreM, HalfX = 0.46f * spec.LengthM * (spec.Rotorcraft ? 0.4f : 1f),
                    HalfY = spec.FuselageRadiusM, HalfZ = spec.FuselageRadiusM, YawRad = yaw, Flags = ColliderFlags.NoClimb,
                });
                if (spec.Craft == FlightCraft.Commuter)
                {
                    // The low wing blocks at knee to waist height.
                    c.Boxes.Add(new OrientedBox
                    {
                        CX = lx + Math.Sin(s.HeadingRad) * 1.0, CZ = lz + Math.Cos(s.HeadingRad) * 1.0, CY = y + 1.25f, HalfX = 1.3f, HalfY = 0.35f,
                        HalfZ = 0.5f * spec.SpanM, YawRad = yaw, Flags = ColliderFlags.NoClimb,
                    });
                }
            }
            if (c.Count == 0) q.Unregister(_colliderKey);
            else q.Register(_colliderKey, c);
        }

        public void Dispose()
        {
            if (_voice != null)
            {
                _voice.Release();
                _voice = null;
            }
            if (_avatarRoot != null) _avatarRoot.gameObject.SetActive(true);
            _avatarRoot = null;
            if (_registeredIn != null && _colliderKey != 0) _registeredIn.Unregister(_colliderKey);
            _registeredIn = null;
            foreach (Mesh m in _meshes.Values)
                if (m != null) UnityEngine.Object.Destroy(m);
            _meshes.Clear();
            _model = null;
            _input.Dispose();
        }
    }
}
