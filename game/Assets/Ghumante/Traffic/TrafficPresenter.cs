using System;
using System.Collections.Generic;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Synth;
using Ghumante.Core.Traffic;
using Ghumante.Vehicles.Visuals;
using Ghumante.World;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using Ghumante.World.Rendering;
using Ghumante.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;
using TrafficHorn = Ghumante.Core.Traffic.HornKind;

namespace Ghumante.Traffic
{
    /// <summary>
    /// Draws and voices the street traffic of the open world (W2_DESIGN 5.1-5.2, 10.3 "presenters only read
    /// snapshots"): every <see cref="AgentPose"/> of <see cref="LifeHost.Vehicles"/> nearest first under the tier's
    /// LOD0 / LOD1 / LOD2 caps (0 / 1 / 6, 1 / 6 / 16, 3 / 10 / 20), with Track B's procedural bodies from
    /// <see cref="VehicleMeshCache"/> (the model type and the LOD0 plate number from the agent id, LOD1 and LOD2 shared
    /// and instanced per model type), spinning wheels of the socket's style (at the model type's own track) on LOD0 and
    /// LOD1, lean, pitch and roll from the pose. Nothing is meshed on the main thread while driving: the LOD2, parked and
    /// wheel meshes are built while loading, the LOD1 bodies and the plated LOD0 bodies on worker threads
    /// (<see cref="VehicleMeshCache.TryBody"/>, <see cref="VehicleMeshCache.RequestUnique"/>) with at most
    /// <see cref="UploadsPerFrame"/> uploads a frame, the agent keeping its coarser shared body until its own is ready.
    /// The parked vehicles of the
    /// visible tiles (moving LOD2 to 20 m, block-out to 80 m, box to 150 m under the tier caps); the block-outs and boxes
    /// are livery-neutral (<see cref="ParkedLook"/>) and drawn with the instanced tint material, the livery's body colour
    /// riding on the instance tint, so one batch per variant and level serves every livery. Draw calls are capped per tier
    /// (<see cref="LifeDrawBudget"/>, spent by <see cref="TrafficDrawPlan"/>: moving, then parked, nearest first): an
    /// instance whose new batch would break the cap falls back to a coarser shared batch (LOD1 to LOD2 to the tinted
    /// block-out, block-out to box) or is skipped. Only near moving traffic (plated LOD0 and shared LOD1 bodies, LOD0
    /// wheels) draws the outline pass; far traffic (LOD2), LOD1 wheels, parked vehicles and the tinted levels use
    /// outline-free copies of the materials (<see cref="WorldMaterialDefaults.CreateOutlineFree"/>). Live engine voices for
    /// the nearest agents (<see cref="ISoundService.OpenEngine"/>, seeded by the agent id so an agent sounds the same all
    /// its life), air-brake hisses when buses and trucks stop, and the sim's horns. Attached to every
    /// <see cref="WorldRoot"/> by <see cref="WorldRoot.AnyReady"/>. Main thread only.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ghumante/Traffic Presenter")]
    public sealed class TrafficPresenter : MonoBehaviour
    {
        /// <summary>Horns farther than this from the camera are not played.</summary>
        public const float HornRangeM = 250f;

        /// <summary>Ids of parked vehicles not drawn (a community-fleet vehicle the player has borrowed; the session owns
        /// the set and keeps an id until the vehicle goes home). Null draws every parked vehicle.</summary>
        public HashSet<uint> HiddenParked { get; set; }

        /// <summary>Side of the green key tag that marks a borrowable community-fleet parked vehicle (W2_DESIGN 5.2,
        /// W2-O5), metres: large enough to read at the parked LOD2/block-out distances.</summary>
        public const float FleetTagM = 0.32f;

        private Mesh _tagMesh;
        private InstanceBatch _tagBatch;

        /// <summary>An agent holding a LOD0 body ranks at this fraction of its distance (and keeps LOD0 to
        /// <c>radius / factor</c>), so two agents at almost the same distance do not swap the unique body every frame.</summary>
        public const float Lod0Hysteresis = 0.8f;

        /// <summary>A LOD0 body is kept this long after its agent drops to LOD1/LOD2 (it is reused if the agent comes
        /// back), so the plated body is not rebuilt and destroyed in a loop.</summary>
        public const float UniqueKeepS = 3f;

        /// <summary>Bodies built on worker threads that are uploaded per frame (each ≈ 0.1-0.3 ms on a phone).</summary>
        public const int UploadsPerFrame = 1;

        private sealed class AgentState
        {
            /// <summary>Metres rolled since the agent appeared: each wheel turns by it over its own radius.</summary>
            public double Odometer;
            public float Speed, UniqueIdleS;
            public IEngineSound Engine;
            public Mesh Unique;

            /// <summary>The plated LOD0 body being built on a worker (null when none is).</summary>
            public MeshJob Pending;
            public bool AtStop, Seen;
        }

        private WorldRoot _world;
        private VehicleMeshCache _cache;

        /// <summary>Outlined (Mid, High): near moving bodies, LOD0 wheels. Outline-free copies: far moving and parked
        /// bodies, LOD1 wheels, fleet tags (<see cref="_far"/>); the tinted block-outs and boxes (<see cref="_tinted"/>).</summary>
        private Material _material, _far, _tinted;
        private LifeLod _lod;
        private LifeDrawBudget _budget;
        private readonly TrafficDrawPlan _plan = new TrafficDrawPlan();
        private DressingConfig _dressing;

        /// <summary>Livery-neutral block-out and box meshes per variant ([variant * 2 + (lod - Block)]) and the body tint
        /// of each variant's liveries.</summary>
        private Mesh[] _neutral;
        private Vector4[][] _liveryTint;
        private int _movingTris;
        private readonly Dictionary<ulong, InstanceBatch> _batches = new Dictionary<ulong, InstanceBatch>();
        private readonly Dictionary<int, AgentState> _agents = new Dictionary<int, AgentState>();
        private readonly List<int> _drop = new List<int>();
        private WheelSocket[][][] _wheels; // [variant][model type]
        private bool _lod1Warm;
        private float[] _dist = new float[64], _keys = new float[64];
        private float[] _audioDist = new float[64];
        private bool[] _wantVoice = new bool[64];
        private int[] _voicePick = new int[16];

        /// <summary>Engines farther than this from the camera get no live voice.</summary>
        public const float EngineVoiceRangeM = 120f;

        /// <summary>An agent that already has a voice ranks at this fraction of its distance, so two agents at almost
        /// the same distance do not trade the voice back and forth.</summary>
        public const float VoiceHysteresis = 0.9f;
        private int[] _level = new int[64], _order = new int[64];
        private float[] _pdist = new float[256], _pkeys = new float[256];
        private int[] _plevel = new int[256], _porder = new int[256];
        private ParkedVehicle[] _parked = new ParkedVehicle[256];
        private Vector3[] _parkedAt = new Vector3[256];
        private int _tris, _drawn, _parkedTris, _parkedDrawn, _draws;
        private readonly int[] _parkedCaps = new int[3];
        private readonly float[] _parkedRadii = new float[3];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            WorldRoot.AnyReady += w => Attach(w);
        }

        /// <summary>Adds the presenter to a world (idempotent).</summary>
        public static TrafficPresenter Attach(WorldRoot world)
        {
            if (world == null) return null;
            TrafficPresenter p = world.GetComponent<TrafficPresenter>();
            if (p == null) p = world.gameObject.AddComponent<TrafficPresenter>();
            p.Bind(world);
            return p;
        }

        private void Bind(WorldRoot world)
        {
            _world = world;
            int tier = (int)world.Tier;
            _lod = LifeLod.ForTier(tier);
            _dressing = DressingConfig.ForTier(tier);
            _parkedCaps[0] = _dressing.ParkedNearCap;
            _parkedCaps[1] = _dressing.ParkedBlockCap;
            _parkedCaps[2] = Math.Max(0, _dressing.ParkedCap - _dressing.ParkedNearCap - _dressing.ParkedBlockCap);
            _parkedRadii[0] = _dressing.ParkedNearM;
            _parkedRadii[1] = _dressing.ParkedBlockM;
            _parkedRadii[2] = _dressing.ParkedBoxM;
            _budget = LifeDrawBudget.ForTier(tier);
            WorldMaterialSet set = world.Materials;
            ReleaseMaterials();
            _material = set != null ? set.instanced : null;
            _far = WorldMaterialDefaults.CreateOutlineFree(_material, "Ghumante Instanced (far, no outline)");
            _tinted = WorldMaterialDefaults.CreateOutlineFree(set != null ? set.instancedTint : null, "Ghumante Instanced Tint (parked, no outline)");
            // Vehicles never dissolve between the camera and the player (the role of 'instanced'); the tint copy follows it.
            if (_tinted != null) WorldMaterialDefaults.ApplyLook(_tinted, new WorldMaterialDefaults.LookRole(0f, false));
            else _tinted = _far;
            if (_cache == null)
            {
                _cache = new VehicleMeshCache();
                // Moving far LOD (also the parked near LOD) and every LOD0/LOD1 wheel: built while loading; the
                // livery-neutral block-outs and boxes too (BuildNeutral). The LOD1 bodies (≈ 155 variant × model × livery
                // combinations) are warmed on worker threads over the first seconds (WarmStep) or built there on first
                // need, the plated LOD0 bodies too (DrawMoving).
                _cache.Prewarm(VehicleLod.Lod2);
                _cache.PrewarmWheels(VehicleLod.Lod0);
                _cache.PrewarmWheels(VehicleLod.Lod1);
            }
            if (_neutral == null) BuildNeutral();
            ReleaseAll();
            _batches.Clear(); // materials change with each open
            _tagBatch = null;
            _wheels = new WheelSocket[VehicleCatalog.Count][][];
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                _wheels[v] = new WheelSocket[VehicleMesher.ModelCount(v)][];
                for (int k = 0; k < _wheels[v].Length; k++) _wheels[v][k] = VehicleMesher.Wheels(VehicleCatalog.At(v), k);
            }
            _world.Closed -= OnClosed;
            _world.Closed += OnClosed;
        }

        private void OnClosed()
        {
            ReleaseAll();
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsOpen || _world.Life == null || _material == null) return;
            Camera cam = _world.ViewCamera;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;
            WorldPos origin = _world.Origin;
            var bounds = new Bounds(camPos, new Vector3(2000f, 800f, 2000f));
            foreach (InstanceBatch b in _batches.Values)
            {
                b.ResetCounters();
                b.WorldBounds = bounds;
            }
            _tris = _drawn = _parkedTris = _parkedDrawn = _draws = 0;
            _movingTris = 0;
            _plan.Begin(_budget);
            if (_cache != null)
            {
                if (!_lod1Warm) _lod1Warm = _cache.WarmStep(VehicleLod.Lod1);
                _cache.Pump(UploadsPerFrame);
            }
            ISoundService sound = _world.Sound;
            DrawMoving(_world.Life, origin, camPos, cam.transform.forward, sound);
            DrawParked(_world.Streamer, origin, camPos);
            foreach (InstanceBatch b in _batches.Values)
            {
                b.Flush();
                _draws += b.Draws;
            }
            Horns(_world.Life, origin, camPos, sound);
            _world.ReportLife(_movingTris + _tris, _drawn, 0, 0, 0, 0, _parkedTris, _parkedDrawn, _draws);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Moving traffic

        private void DrawMoving(LifeHost life, WorldPos origin, Vector3 camPos, Vector3 camFwd, ISoundService sound)
        {
            int n = life.VehicleCount;
            AgentPose[] poses = life.Vehicles;
            Grow(ref _dist, ref _keys, ref _level, ref _order, n);
            if (_audioDist.Length < _dist.Length)
            {
                _audioDist = new float[_dist.Length];
                _wantVoice = new bool[_dist.Length];
            }
            if (_voicePick.Length < _lod.EngineVoices) _voicePick = new int[_lod.EngineVoices];
            float dt = Time.deltaTime, age = life.SnapshotAgeS;
            foreach (AgentState s in _agents.Values) s.Seen = false;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Scene(poses[i], origin, age);
                Vector3 d = p - camPos;
                float dist = d.magnitude;
                AgentState held;
                bool known = _agents.TryGetValue(poses[i].AgentId, out held);
                // Engines are heard all round: rank them by the true distance, never the render priority.
                _audioDist[i] = known && held.Engine != null ? dist * VoiceHysteresis : dist;
                // Spend the caps on what the camera can see: far agents behind the camera are skipped.
                if (dist > 15f && Vector3.Dot(d, camFwd) < -0.2f * dist) dist = float.MaxValue;
                // Hysteresis: the current holder of a LOD0 body keeps it until another agent is clearly nearer.
                if (dist < float.MaxValue && known && held.Unique != null && held.UniqueIdleS <= 0f)
                    dist *= Lod0Hysteresis;
                _dist[i] = dist;
            }
            LifeLod.Assign(_dist, n, _lod.VehicleCaps, _lod.VehicleRadii, _level, _order, _keys);
            NearestSelect.Select(_audioDist, n, _lod.EngineVoices, EngineVoiceRangeM, _wantVoice, _voicePick);
            for (int k = 0; k < n; k++)
            {
                int i = _order[k];
                AgentPose a = poses[i];
                AgentState st;
                if (!_agents.TryGetValue(a.AgentId, out st))
                {
                    st = new AgentState { Speed = a.SpeedMps };
                    _agents.Add(a.AgentId, st);
                }
                st.Seen = true;
                float accel = dt > 1e-4f ? (a.SpeedMps - st.Speed) / dt : 0f;
                st.Speed = a.SpeedMps;
                int variant = Mathf.Clamp(a.Variant, 0, VehicleCatalog.Count - 1);
                VehicleCatalogEntry e = VehicleCatalog.At(variant);
                st.Odometer += a.SpeedMps * dt;
                // The model type follows the agent id, the same seed that numbers its LOD0 plate, so the shape (and the
                // wheel track) holds across levels.
                byte model = VehicleMesher.ModelFor(variant, (uint)a.AgentId);
                WheelSocket[] wheels = _wheels[variant][model % _wheels[variant].Length];
                Vector3 p = Scene(a, origin, age);
                Matrix4x4 m = Matrix4x4.TRS(p, Quaternion.Euler(-a.Pitch * Mathf.Rad2Deg, a.HeadingRad * Mathf.Rad2Deg, -(a.Roll + a.Lean) * Mathf.Rad2Deg), Vector3.one);
                bool rider = TrafficDrawKeys.CarriesRider(a.Class);
                int level = _level[i];
                if (level >= 0)
                {
                    // Nearest first under the moving cap (TrafficDrawPlan): the plated LOD0 body once a worker has built
                    // it, else the shared LOD1 body once built, else the prewarmed LOD2 body, else the tinted block-out.
                    bool drawn = false;
                    if (level == 0 && TakeUnique(st, variant, a.Livery, model, (uint)a.AgentId, rider) && _plan.TryUnique(a.AgentId, wheels))
                    {
                        st.UniqueIdleS = 0f;
                        var rp = new RenderParams(_material) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, worldBounds = new Bounds(p, new Vector3(30f, 10f, 30f)) };
                        Graphics.RenderMesh(rp, st.Unique, 0, m);
                        _tris += (int)(st.Unique.GetIndexCount(0) / 3);
                        Wheels(wheels, m, st.Odometer, VehicleLod.Lod0);
                        drawn = true;
                    }
                    if (!drawn)
                    {
                        if (level != 0) IdleUnique(st, dt);
                        InstanceBatch near = level <= 1 ? TryShared(variant, a.Livery, model, VehicleLod.Lod1, rider) : null;
                        if (near != null && _plan.TryNear(variant, a.Livery, model, rider, wheels))
                        {
                            near.Add(m);
                            _movingTris += near.TrisPerInstance;
                            Wheels(wheels, m, st.Odometer, VehicleLod.Lod1);
                            drawn = true;
                        }
                        else
                        {
                            TrafficDraw far = _plan.TryFar(variant, a.Livery, model, rider);
                            if (far == TrafficDraw.Lod2)
                            {
                                InstanceBatch b = Shared(variant, a.Livery, model, VehicleLod.Lod2, rider);
                                b.Add(m);
                                _movingTris += b.TrisPerInstance;
                                drawn = true;
                            }
                            else if (far == TrafficDraw.Block)
                            {
                                InstanceBatch b = TintedBatch(variant, VehicleLod.Block);
                                b.Add(m, LiveryTint(variant, a.Livery));
                                _movingTris += b.TrisPerInstance;
                                drawn = true;
                            }
                        }
                    }
                    if (drawn) _drawn++;
                }
                else IdleUnique(st, dt);

                // Engines: the nearest agents (all round, by true distance) get a live voice.
                if (_wantVoice[i])
                {
                    if (st.Engine == null && sound != null)
                        st.Engine = sound.OpenEngine(EnginePresets.ForCatalog((byte)e.Engine, (byte)e.DriveKind), (uint)a.AgentId, false);
                    if (st.Engine != null)
                    {
                        float vx = Mathf.Sin(a.HeadingRad) * a.SpeedMps, vz = Mathf.Cos(a.HeadingRad) * a.SpeedMps;
                        bool braking = (a.AnimState & (byte)AgentAnim.Braking) != 0;
                        float throttle = braking ? 0f : Mathf.Clamp01(0.15f + accel * 0.35f);
                        st.Engine.Drive(p.x, p.y, p.z, vx, 0f, vz, a.SpeedMps, throttle, braking ? 0.7f : 0f, FootstepSurface.Asphalt, dt);
                        bool atStop = (a.AnimState & (byte)AgentAnim.AtStop) != 0;
                        if (atStop && !st.AtStop && VehicleClasses.IsHeavy(a.Class)) st.Engine.AirBrake(false);
                        st.AtStop = atStop;
                    }
                }
                else if (st.Engine != null)
                {
                    st.Engine.Release();
                    st.Engine = null;
                }
            }
            // Agents gone from the snapshot.
            _drop.Clear();
            foreach (KeyValuePair<int, AgentState> kv in _agents)
                if (!kv.Value.Seen) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++)
            {
                AgentState st = _agents[_drop[i]];
                if (st.Engine != null) st.Engine.Release();
                DropUnique(st);
                _agents.Remove(_drop[i]);
            }
        }

        /// <summary>Instances the wheels of a body, each turned by the distance rolled over its own radius (a tractor's
        /// small front wheels spin faster than its big rear ones).</summary>
        private void Wheels(WheelSocket[] wheels, in Matrix4x4 body, double odometerM, VehicleLod lod)
        {
            for (int w = 0; w < wheels.Length; w++)
            {
                WheelSocket s = wheels[w];
                float spinDeg = (float)((odometerM / Math.Max(0.1, s.Radius) * (180.0 / Math.PI)) % 360.0);
                Matrix4x4 m = body * Matrix4x4.TRS(new Vector3(s.X, s.Y, s.Z), Quaternion.Euler(spinDeg, 0f, 0f), Vector3.one);
                InstanceBatch b = WheelBatch(s, lod);
                b.Add(m);
                _movingTris += b.TrisPerInstance;
            }
        }

        /// <summary>The agent is at LOD0: true when its plated body is ready; otherwise starts (or keeps) building it on
        /// a worker and returns false (the caller draws the shared body meanwhile).</summary>
        private bool TakeUnique(AgentState st, int variant, byte livery, byte model, uint seed, bool rider)
        {
            if (st.Unique != null) return true;
            if (st.Pending == null)
            {
                st.Pending = _cache.RequestUnique(variant, livery, model, VehicleLod.Lod0, seed, rider);
                return false;
            }
            if (st.Pending.Mesh != null)
            {
                st.Unique = st.Pending.Mesh;
                _cache.Recycle(st.Pending);
                st.Pending = null;
                return true;
            }
            if (st.Pending.HasFailed)
            {
                _cache.Recycle(st.Pending);
                st.Pending = null;
            }
            return false;
        }

        /// <summary>The agent is not at LOD0 this frame: keep its body a while in case it comes back (a build still
        /// running is dropped).</summary>
        private void IdleUnique(AgentState st, float dt)
        {
            if (st.Pending != null)
            {
                _cache.Cancel(st.Pending);
                st.Pending = null;
            }
            if (st.Unique == null) return;
            st.UniqueIdleS += Mathf.Max(dt, 1e-3f);
            if (st.UniqueIdleS > UniqueKeepS) DropUnique(st);
        }

        private void DropUnique(AgentState st)
        {
            if (st.Pending != null)
            {
                if (_cache != null) _cache.Cancel(st.Pending);
                st.Pending = null;
            }
            if (st.Unique == null) return;
            Destroy(st.Unique);
            st.Unique = null;
            st.UniqueIdleS = 0f;
        }

        /// <summary>Scene position of a pose, moved <paramref name="ageS"/> along its heading (LifeHost.SnapshotAgeS).</summary>
        private static Vector3 Scene(in AgentPose a, WorldPos origin, float ageS)
        {
            float ahead = a.SpeedMps * ageS;
            return new Vector3((float)(a.X - origin.X) + Mathf.Sin(a.HeadingRad) * ahead, a.Y - origin.Y, (float)(a.Z - origin.Z) + Mathf.Cos(a.HeadingRad) * ahead);
        }

        /// <summary>The batch of a prewarmed shared body (LOD2, moving or parked: the parked near level shares the
        /// moving far batch of its look), outline-free.</summary>
        private InstanceBatch Shared(int variant, byte livery, byte model, VehicleLod lod, bool rider)
        {
            ulong key = TrafficDrawKeys.Body(variant, livery, model, lod, rider);
            InstanceBatch b;
            if (_batches.TryGetValue(key, out b)) return b;
            Mesh mesh = _cache.Body(variant, livery, model, lod, rider);
            b = new InstanceBatch(mesh, lod <= VehicleLod.Lod1 ? _material : _far, (int)(mesh.GetIndexCount(0) / 3), false)
            {
                Shadows = lod <= VehicleLod.Lod1 ? ShadowCastingMode.On : ShadowCastingMode.Off,
            };
            _batches.Add(key, b);
            return b;
        }

        /// <summary>The batch of a shared moving body that may still be building (null until a worker has built it; see
        /// <see cref="VehicleMeshCache.TryBody"/>).</summary>
        private InstanceBatch TryShared(int variant, byte livery, byte model, VehicleLod lod, bool rider)
        {
            ulong key = TrafficDrawKeys.Body(variant, livery, model, lod, rider);
            InstanceBatch b;
            if (_batches.TryGetValue(key, out b)) return b;
            Mesh mesh;
            if (!_cache.TryBody(variant, livery, model, lod, rider, out mesh)) return null;
            b = new InstanceBatch(mesh, lod <= VehicleLod.Lod1 ? _material : _far, (int)(mesh.GetIndexCount(0) / 3), false)
            {
                Shadows = lod <= VehicleLod.Lod1 ? ShadowCastingMode.On : ShadowCastingMode.Off,
            };
            _batches.Add(key, b);
            return b;
        }

        /// <summary>The tinted, livery-neutral block-out or box batch of a variant (every livery and model type).</summary>
        private InstanceBatch TintedBatch(int variant, VehicleLod lod)
        {
            ulong key = TrafficDrawKeys.Tinted(variant, lod);
            InstanceBatch b;
            if (_batches.TryGetValue(key, out b)) return b;
            Mesh mesh = _neutral[variant * 2 + (lod == VehicleLod.Box ? 1 : 0)];
            b = new InstanceBatch(mesh, _tinted, (int)(mesh.GetIndexCount(0) / 3), true) { Shadows = ShadowCastingMode.Off };
            _batches.Add(key, b);
            return b;
        }

        /// <summary>The instance tint of a livery: its body colour (sRGB).</summary>
        private Vector4 LiveryTint(int variant, byte livery)
        {
            Vector4[] t = _liveryTint[variant];
            return t[livery % t.Length];
        }

        /// <summary>Uploads the livery-neutral block-out and box of every variant (<see cref="ParkedLook.BuildNeutral"/>),
        /// about 50 tiny meshes, and the body tint of every livery. Load time.</summary>
        private void BuildNeutral()
        {
            var data = new Core.Meshing.MeshData(256, 768);
            _neutral = new Mesh[VehicleCatalog.Count * 2];
            _liveryTint = new Vector4[VehicleCatalog.Count][];
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                for (int k = 0; k < 2; k++)
                {
                    data.Clear();
                    ParkedLook.BuildNeutral(v, k == 0 ? VehicleLod.Block : VehicleLod.Box, data);
                    _neutral[v * 2 + k] = VehicleMeshCache.Upload(data, e.AssetId + (k == 0 ? "_block_tinted" : "_box_tinted"));
                }
                _liveryTint[v] = new Vector4[Math.Max(1, (int)e.LiveryCount)];
                for (int l = 0; l < _liveryTint[v].Length; l++) _liveryTint[v][l] = Tint.Hex(ParkedLook.TintOf(v, (byte)l));
            }
        }

        private InstanceBatch WheelBatch(in WheelSocket s, VehicleLod lod)
        {
            ulong key = TrafficDrawKeys.Wheel(s.Style, s.Radius, s.Width, lod);
            InstanceBatch b;
            if (_batches.TryGetValue(key, out b)) return b;
            Mesh wheel = _cache.Wheel(s, lod);
            b = new InstanceBatch(wheel, TrafficDrawKeys.WheelOutlined(lod) ? _material : _far, (int)(wheel.GetIndexCount(0) / 3), false);
            _batches.Add(key, b);
            return b;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Parked vehicles (W2_DESIGN 5.2)

        private void DrawParked(WorldStreamer streamer, WorldPos origin, Vector3 camPos)
        {
            if (streamer == null) return;
            int n = 0;
            double camX = origin.X + camPos.x, camZ = origin.Z + camPos.z;
            IReadOnlyList<TileView> views = streamer.DetailViews;
            for (int v = 0; v < views.Count; v++)
            {
                TileView view = views[v];
                TileInstances inst = view.Extras != null ? view.Extras.Instances : null;
                if (inst == null || inst.Parked.Count == 0) continue;
                double x0 = view.Node.Area.X0, z0 = view.Node.Area.Z0, cx = camX - x0, cz = camZ - z0;
                int cells = inst.CellsPerSide;
                for (int c = 0; c < cells * cells; c++)
                {
                    double bx = (c % cells) * TileInstances.CellM, bz = (c / cells) * TileInstances.CellM;
                    if (World.Buildings.BandConfig.NearestDistance(bx, bz, bx + TileInstances.CellM, bz + TileInstances.CellM, cx, cz) > _dressing.ParkedBoxM) continue;
                    for (int i = inst.ParkedCells[c]; i < inst.ParkedCells[c + 1]; i++)
                    {
                        ParkedVehicle pv = inst.Parked[i];
                        if (HiddenParked != null && HiddenParked.Contains(pv.Id)) continue;
                        double dx = pv.X - cx, dz = pv.Z - cz;
                        float d = (float)Math.Sqrt(dx * dx + dz * dz);
                        if (d > _dressing.ParkedBoxM) continue;
                        if (n == _parked.Length)
                        {
                            Array.Resize(ref _parked, n * 2);
                            Array.Resize(ref _parkedAt, n * 2);
                        }
                        Grow(ref _pdist, ref _pkeys, ref _plevel, ref _porder, n + 1);
                        _parked[n] = pv;
                        _parkedAt[n] = new Vector3((float)(x0 + pv.X - origin.X), pv.Y - origin.Y, (float)(z0 + pv.Z - origin.Z));
                        _pdist[n++] = d;
                    }
                }
            }
            LifeLod.Assign(_pdist, n, _parkedCaps, _parkedRadii, _plevel, _porder, _pkeys);
            // Nearest first (the order Assign sorted), so the cap keeps the near ones' batches; the parked share of the
            // cap is what moving traffic left plus the parked slice (TrafficDrawPlan).
            for (int k = 0; k < n; k++)
            {
                int i = _porder[k];
                int level = _plevel[i];
                if (level < 0) continue;
                ParkedVehicle pv = _parked[i];
                int variant = Mathf.Clamp(pv.Variant, 0, VehicleCatalog.Count - 1);
                byte model = ParkedModel(pv);
                TrafficDraw d = _plan.PlaceParked(level, variant, pv.Livery, model);
                if (d == TrafficDraw.None) continue; // over the cap: the farthest parked vehicles go first
                Matrix4x4 body = Matrix4x4.TRS(_parkedAt[i], Quaternion.Euler(0f, pv.YawDeg, 0f), Vector3.one);
                InstanceBatch b;
                if (d == TrafficDraw.Lod2)
                {
                    b = Shared(variant, pv.Livery, model, VehicleLod.Lod2, false);
                    b.Add(body);
                }
                else
                {
                    b = TintedBatch(variant, d == TrafficDraw.Block ? VehicleLod.Block : VehicleLod.Box);
                    b.Add(body, LiveryTint(variant, pv.Livery));
                }
                _parkedTris += b.TrisPerInstance;
                if (pv.CommunityFleet && d != TrafficDraw.Box) FleetTag(variant, body);
                _parkedDrawn++;
            }
        }

        /// <summary>The model type a parked vehicle is drawn as: the one its borrowed copy gets (both use
        /// <see cref="VehicleSeeds.Fleet"/> of the parked id), so a borrowed fleet bike keeps its shape.</summary>
        private static byte ParkedModel(in ParkedVehicle pv)
        {
            return VehicleSeeds.FleetModel(pv.Variant, pv.Id);
        }

        /// <summary>The green key tag of a community-fleet vehicle (the one DrivenVehicleView hangs on a borrowed
        /// vehicle, larger): a two-sided diamond above the handlebar or on the driver's side of the roof.</summary>
        private void FleetTag(int variant, in Matrix4x4 body)
        {
            if (!_plan.TryFleetTag()) return;
            if (_tagBatch == null)
            {
                if (_tagMesh == null) _tagMesh = BuildTagMesh();
                _tagBatch = new InstanceBatch(_tagMesh, _far, 4, false) { Shadows = ShadowCastingMode.Off };
                _batches.Add(TrafficDrawKeys.FleetTag, _tagBatch);
            }
            _parkedTris += 4;
            VehicleCatalogEntry e = VehicleCatalog.At(Mathf.Clamp(variant, 0, VehicleCatalog.Count - 1));
            VehicleMesher.Dims d = VehicleMesher.DimsOf(e);
            bool two = Core.Characters.VehicleRoles.IsTwoWheeler(e.Shape);
            var at = two ? new Vector3(0f, 1.25f + 0.5f * FleetTagM, d.Wheelbase) : new Vector3(-0.25f * d.Width, d.Height + 0.5f * FleetTagM, d.Wheelbase * 0.6f);
            _tagBatch.Add(body * Matrix4x4.TRS(at, Quaternion.Euler(0f, 90f, 0f), Vector3.one));
        }

        private static Mesh BuildTagMesh()
        {
            float s = 0.5f * FleetTagM;
            var v = new[] { new Vector3(0f, s, 0f), new Vector3(s * 0.7f, 0f, 0f), new Vector3(0f, -s, 0f), new Vector3(-s * 0.7f, 0f, 0f) };
            var n = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            var green = new Color32(0x3F, 0xB9, 0x4F, 0xFF);
            var c = new[] { green, green, green, green };
            var t = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            var m = new Mesh { name = "Fleet tag (parked)", vertices = v, normals = n, colors32 = c, triangles = t };
            m.RecalculateBounds();
            return m;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Horns (TrafficSim.Horn, collected per step and consumed once)

        private void Horns(LifeHost life, WorldPos origin, Vector3 camPos, ISoundService sound)
        {
            int count = life.TakeHorns();
            if (sound == null) return;
            HornEvent[] horns = life.Horns;
            AgentPose[] poses = life.Vehicles;
            for (int h = 0; h < count; h++)
            {
                HornEvent e = horns[h];
                var p = new Vector3((float)(e.X - origin.X), camPos.y, (float)(e.Z - origin.Z));
                float vx = 0f, vz = 0f;
                int variant = -1;
                for (int i = 0; i < life.VehicleCount; i++)
                {
                    if (poses[i].AgentId != e.AgentId) continue;
                    p.y = poses[i].Y - origin.Y + 1f;
                    vx = Mathf.Sin(poses[i].HeadingRad) * poses[i].SpeedMps;
                    vz = Mathf.Cos(poses[i].HeadingRad) * poses[i].SpeedMps;
                    variant = poses[i].Variant;
                    break;
                }
                if ((p - camPos).sqrMagnitude > HornRangeM * HornRangeM) continue;
                if (variant >= 0)
                {
                    // The horn follows the catalogue entry the agent is drawn and voiced as (W2_DESIGN 10.3:
                    // VehicleCatalog is the one mapping to sound), so a scooter never honks like a motorbike.
                    VehicleCatalogEntry ce = VehicleCatalog.At(Mathf.Clamp(variant, 0, VehicleCatalog.Count - 1));
                    EngineModel model = EnginePresets.ForCatalog((byte)ce.Engine, (byte)ce.DriveKind);
                    sound.PlayHorn(EnginePresets.HornFor(model), p.x, p.y, p.z, vx, 0f, vz, EnginePresets.HornHoldS((byte)(TrafficHorn)e.Kind));
                }
                else sound.PlayTrafficHorn((byte)e.Class, e.AgentId, (byte)(TrafficHorn)e.Kind, p.x, p.y, p.z, vx, 0f, vz);
            }
        }

        private static void Grow(ref float[] a, ref float[] b, ref int[] c, ref int[] d, int n)
        {
            if (a.Length >= n) return;
            int cap = Math.Max(n, a.Length * 2);
            Array.Resize(ref a, cap);
            Array.Resize(ref b, cap);
            Array.Resize(ref c, cap);
            Array.Resize(ref d, cap);
        }

        private void ReleaseAll()
        {
            foreach (AgentState st in _agents.Values)
            {
                if (st.Engine != null) st.Engine.Release();
                DropUnique(st);
            }
            _agents.Clear();
        }

        private void OnDestroy()
        {
            if (_world != null) _world.Closed -= OnClosed;
            ReleaseAll();
            _batches.Clear(); // the meshes belong to the cache (and the tag mesh to this presenter)
            _tagBatch = null;
            if (_tagMesh != null) Destroy(_tagMesh);
            _tagMesh = null;
            if (_neutral != null)
                for (int i = 0; i < _neutral.Length; i++)
                    if (_neutral[i] != null) Destroy(_neutral[i]);
            _neutral = null;
            ReleaseMaterials();
            if (_cache != null) _cache.Dispose();
            _cache = null;
        }

        /// <summary>Destroys the outline-free material copies (made per open).</summary>
        private void ReleaseMaterials()
        {
            if (_tinted != _far) WorldMaterialDefaults.DestroyRuntime(_tinted);
            WorldMaterialDefaults.DestroyRuntime(_far);
            _tinted = _far = null;
        }
    }
}
