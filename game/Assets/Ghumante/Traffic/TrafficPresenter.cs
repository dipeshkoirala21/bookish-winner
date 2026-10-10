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
    /// visible tiles (moving LOD2 to 20 m, block-out to 80 m, box to 150 m under the tier caps); live engine voices for
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
        private Material _material;
        private LifeLod _lod;
        private DressingConfig _dressing;
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
            WorldMaterialSet set = world.Materials;
            _material = set != null ? set.instanced : null;
            if (_cache == null)
            {
                _cache = new VehicleMeshCache();
                // Moving far LOD, parked near LOD, block-out, box and every LOD0/LOD1 wheel: built while loading. The LOD1
                // bodies (≈ 155 variant × model × livery combinations) are warmed on worker threads over the first seconds
                // (WarmStep) or built there on first need, the plated LOD0 bodies too (DrawMoving).
                _cache.Prewarm(VehicleLod.Lod2);
                _cache.Prewarm(VehicleLod.Block);
                _cache.Prewarm(VehicleLod.Box);
                _cache.PrewarmWheels(VehicleLod.Lod0);
                _cache.PrewarmWheels(VehicleLod.Lod1);
            }
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
            if (_cache != null)
            {
                if (!_lod1Warm) _lod1Warm = _cache.WarmStep(VehicleLod.Lod1);
                _cache.Pump(UploadsPerFrame);
            }
            ISoundService sound = _world.Sound;
            DrawMoving(_world.Life, origin, camPos, cam.transform.forward, sound);
            DrawParked(_world.Streamer, origin, camPos);
            int vehTris = 0, parkedTris = _parkedTris;
            foreach (InstanceBatch b in _batches.Values)
            {
                b.Flush();
                _draws += b.Draws;
            }
            foreach (KeyValuePair<ulong, InstanceBatch> kv in _batches)
            {
                bool parked = (kv.Key & 0x80000000UL) != 0;
                if (parked) parkedTris += kv.Value.Tris;
                else vehTris += kv.Value.Tris;
            }
            Horns(_world.Life, origin, camPos, sound);
            _world.ReportLife(vehTris + _tris, _drawn, 0, 0, 0, 0, parkedTris, _parkedDrawn, _draws);
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
                bool rider = VehicleClasses.IsTwoWheel(a.Class) || a.Class == VehicleClass.Rickshaw;
                int level = _level[i];
                if (level >= 0)
                {
                    _drawn++;
                    if (level == 0 && TakeUnique(st, variant, a.Livery, model, (uint)a.AgentId, rider))
                    {
                        st.UniqueIdleS = 0f;
                        var rp = new RenderParams(_material) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, worldBounds = new Bounds(p, new Vector3(30f, 10f, 30f)) };
                        Graphics.RenderMesh(rp, st.Unique, 0, m);
                        _tris += (int)(st.Unique.GetIndexCount(0) / 3);
                        Wheels(wheels, m, st.Odometer, VehicleLod.Lod0);
                    }
                    else
                    {
                        // LOD1 (or a LOD0 agent whose plated body is still being built): the shared LOD1 body once a worker
                        // has built it, the prewarmed LOD2 body until then.
                        if (level != 0) IdleUnique(st, dt);
                        InstanceBatch near = level <= 1 ? TryBatch(variant, a.Livery, model, VehicleLod.Lod1, rider) : null;
                        if (near != null)
                        {
                            near.Add(m);
                            Wheels(wheels, m, st.Odometer, VehicleLod.Lod1);
                        }
                        else Batch(variant, a.Livery, model, VehicleLod.Lod2, rider, false).Add(m);
                    }
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
                WheelBatch(s, lod).Add(m);
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

        private InstanceBatch Batch(int variant, byte livery, byte model, VehicleLod lod, bool rider, bool parked)
        {
            if (lod >= VehicleLod.Block) model = 0; // the block-out and the box have no model types
            ulong key = ((ulong)(uint)variant << 40) | ((ulong)livery << 32) | (parked ? 0x80000000UL : 0UL) | ((ulong)model << 16) | ((ulong)lod << 8) |
                        (rider ? 1UL : 0UL);
            InstanceBatch b;
            if (_batches.TryGetValue(key, out b)) return b;
            Mesh mesh = _cache.Body(variant, livery, model, lod, rider);
            b = new InstanceBatch(mesh, _material, (int)(mesh.GetIndexCount(0) / 3), false)
            {
                Shadows = lod <= VehicleLod.Lod1 ? ShadowCastingMode.On : ShadowCastingMode.Off,
            };
            _batches.Add(key, b);
            return b;
        }

        /// <summary>The batch of a shared moving body that may still be building (null until a worker has built it; see
        /// <see cref="VehicleMeshCache.TryBody"/>).</summary>
        private InstanceBatch TryBatch(int variant, byte livery, byte model, VehicleLod lod, bool rider)
        {
            ulong key = ((ulong)(uint)variant << 40) | ((ulong)livery << 32) | ((ulong)model << 16) | ((ulong)lod << 8) | (rider ? 1UL : 0UL);
            InstanceBatch b;
            if (_batches.TryGetValue(key, out b)) return b;
            Mesh mesh;
            if (!_cache.TryBody(variant, livery, model, lod, rider, out mesh)) return null;
            b = new InstanceBatch(mesh, _material, (int)(mesh.GetIndexCount(0) / 3), false)
            {
                Shadows = lod <= VehicleLod.Lod1 ? ShadowCastingMode.On : ShadowCastingMode.Off,
            };
            _batches.Add(key, b);
            return b;
        }

        private InstanceBatch WheelBatch(in WheelSocket s, VehicleLod lod)
        {
            ulong key = (1UL << 62) | ((ulong)(byte)s.Style << 48) | ((ulong)(uint)Mathf.RoundToInt(s.Radius * 100f) << 32) |
                        ((ulong)(uint)Mathf.RoundToInt(s.Width * 100f) << 8) | (byte)lod;
            InstanceBatch b;
            if (_batches.TryGetValue(key, out b)) return b;
            Mesh wheel = _cache.Wheel(s, lod);
            b = new InstanceBatch(wheel, _material, (int)(wheel.GetIndexCount(0) / 3), false);
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
            for (int i = 0; i < n; i++)
            {
                int level = _plevel[i];
                if (level < 0) continue;
                ParkedVehicle pv = _parked[i];
                VehicleLod lod = level == 0 ? VehicleLod.Lod2 : level == 1 ? VehicleLod.Block : VehicleLod.Box;
                Matrix4x4 body = Matrix4x4.TRS(_parkedAt[i], Quaternion.Euler(0f, pv.YawDeg, 0f), Vector3.one);
                Batch(pv.Variant, pv.Livery, ParkedModel(pv), lod, false, true).Add(body);
                if (pv.CommunityFleet && level <= 1) FleetTag(pv.Variant, body);
                _parkedDrawn++;
            }
        }

        /// <summary>The model type a parked vehicle is drawn as: the one its borrowed copy gets, whose plate seed the
        /// explorer derives from the parked id (ExplorerController, <c>CharMath.Hash(id, 0x464C54)</c>), so a borrowed
        /// fleet bike keeps its shape.</summary>
        private static byte ParkedModel(in ParkedVehicle pv)
        {
            return VehicleMesher.ModelFor(pv.Variant, Core.Characters.CharMath.Hash(pv.Id, 0x464C54u));
        }

        /// <summary>The green key tag of a community-fleet vehicle (the one DrivenVehicleView hangs on a borrowed
        /// vehicle, larger): a two-sided diamond above the handlebar or on the driver's side of the roof.</summary>
        private void FleetTag(int variant, in Matrix4x4 body)
        {
            if (_tagBatch == null)
            {
                if (_tagMesh == null) _tagMesh = BuildTagMesh();
                _tagBatch = new InstanceBatch(_tagMesh, _material, 4, false) { Shadows = ShadowCastingMode.Off };
                _batches.Add((2UL << 62) | 0x80000000UL, _tagBatch); // counted with the parked triangles
            }
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
            if (_cache != null) _cache.Dispose();
            _cache = null;
        }
    }
}
