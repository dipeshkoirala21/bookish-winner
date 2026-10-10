using System;
using System.Collections.Generic;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.Vehicles.Visuals
{
    /// <summary>
    /// Uploads the procedural cartoon vehicles of <see cref="VehicleMesher"/> (W2_DESIGN 5.2) to Unity meshes and keeps
    /// one shared mesh per (variant, model type, livery, level, rider) for traffic and parked vehicles, plus one wheel
    /// mesh per style, size and level, so instanced traffic draws from a handful of meshes with one
    /// <c>Ghumante/ToonLit</c> material. Meshes carry UV0 (u = <see cref="MaterialChannel"/>, v = baked AO) for the
    /// shader's material channels. Shared bodies carry a blank-numbered plate; <see cref="CreateUnique(int, byte, VehicleLod, uint, bool)"/>
    /// builds a body with its own plate number (the player's vehicle and the nearest traffic), which the caller owns and
    /// destroys. The model type of a vehicle comes from its seed (<see cref="VehicleMesher.ModelFor"/>): pass the same
    /// model to <see cref="Body(int, byte, byte, VehicleLod, bool)"/> so a vehicle keeps its shape across levels. Main
    /// thread only; building allocates, so presenters warm the cache while loading (<see cref="Prewarm"/>,
    /// <see cref="PrewarmWheels"/>), never per frame. Bodies needed while driving (the LOD1 bodies of the nearest traffic,
    /// the plated LOD0 bodies) are built off the main thread instead (<see cref="TryBody"/>, <see cref="RequestUnique"/>):
    /// <see cref="VehicleMesher"/> is engine-free with per-thread scratch, so a pooled job builds the
    /// <see cref="MeshData"/> on the thread pool and <see cref="Pump"/> uploads finished ones on the main thread, at most a
    /// few per frame; the presenter keeps drawing the coarser shared level until the mesh is ready.
    /// </summary>
    public sealed class VehicleMeshCache : IDisposable
    {
        // Stream 0 positions, 1 normals, 2 colours (RGBA8, sRGB values: the shader linearises them), 3 UV0.
        private static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
        };

        private static readonly VertexAttributeDescriptor[] LayoutUv =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3),
        };

        private const MeshUpdateFlags Flags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds |
                                              MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds;

        private readonly Dictionary<ulong, Mesh> _bodies = new Dictionary<ulong, Mesh>();
        private readonly Dictionary<ulong, Mesh> _wheels = new Dictionary<ulong, Mesh>();
        private readonly MeshData _scratch = new MeshData(4096, 12288);
        private static ushort[] s_shorts;

        /// <summary>Jobs at most in flight at once (the rest wait for a free job): a few cores on a phone, and every
        /// finished job waits for its upload anyway.</summary>
        public const int MaxJobs = 4;

        private readonly Dictionary<ulong, MeshJob> _pendingBodies = new Dictionary<ulong, MeshJob>();
        private readonly List<MeshJob> _inflight = new List<MeshJob>(MaxJobs);
        private readonly Stack<MeshJob> _freeJobs = new Stack<MeshJob>(MaxJobs);
        private static readonly System.Threading.WaitCallback s_work = Work;
        private static bool s_warned;

        /// <summary>Meshes held by the cache.</summary>
        public int Count
        {
            get { return _bodies.Count + _wheels.Count; }
        }

        /// <summary>The shared body of catalogue <paramref name="variant"/> as the model type a zero seed picks (wheels
        /// included from LOD2 down). Prefer <see cref="Body(int, byte, byte, VehicleLod, bool)"/> with the vehicle's own
        /// model type.</summary>
        public Mesh Body(int variant, byte livery, VehicleLod lod, bool rider = false)
        {
            return Body(variant, livery, VehicleMesher.ModelFor(variant, 0u), lod, rider);
        }

        /// <summary>The shared body of catalogue <paramref name="variant"/>, model type <paramref name="model"/> (wrapped;
        /// the block-out and box levels ignore it), wheels included from LOD2 down.</summary>
        public Mesh Body(int variant, byte livery, byte model, VehicleLod lod, bool rider = false)
        {
            byte mdl;
            bool rd;
            ulong key = BodyKey(variant, livery, model, lod, rider, out mdl, out rd);
            Mesh mesh;
            if (_bodies.TryGetValue(key, out mesh) && mesh != null) return mesh;
            _scratch.Clear();
            VehicleMesher.Build(variant, livery, mdl, lod, _scratch, 0u, rd);
            mesh = Upload(_scratch, VehicleCatalog.At(variant).AssetId + "_" + VehicleMesher.ModelName(variant, mdl) + "_" + lod);
            _bodies[key] = mesh;
            return mesh;
        }

        /// <summary>The key of a shared body (variant, wrapped model type, livery, level, rider), as <see cref="Body(int,
        /// byte, byte, VehicleLod, bool)"/> files it.</summary>
        private static ulong BodyKey(int variant, byte livery, byte model, VehicleLod lod, bool rider, out byte mdl, out bool rd)
        {
            mdl = lod >= VehicleLod.Block ? (byte)0 : (byte)(model % VehicleMesher.ModelCount(variant));
            rd = rider && lod <= VehicleLod.Lod2;
            return ((ulong)(uint)variant << 32) | ((ulong)mdl << 24) | ((ulong)livery << 16) | ((ulong)lod << 8) | (rd ? 1UL : 0UL);
        }

        /// <summary>
        /// The shared body if it is ready; otherwise starts building it on a worker thread (once) and returns false, so
        /// the caller draws a coarser shared level this frame (the LOD2 bodies are prewarmed). No main-thread build:
        /// <see cref="Pump"/> uploads it when the worker is done.
        /// </summary>
        public bool TryBody(int variant, byte livery, byte model, VehicleLod lod, bool rider, out Mesh mesh)
        {
            byte mdl;
            bool rd;
            ulong key = BodyKey(variant, livery, model, lod, rider, out mdl, out rd);
            if (_bodies.TryGetValue(key, out mesh) && mesh != null) return true;
            mesh = null;
            if (_pendingBodies.ContainsKey(key)) return false;
            MeshJob job = Start(variant, livery, mdl, lod, 0u, rd);
            if (job == null) return false; // every job busy: ask again next frame
            job.Key = key;
            job.Shared = true;
            _pendingBodies.Add(key, job);
            return false;
        }

        /// <summary>
        /// Starts building a body with its own plate number from <paramref name="plateSeed"/> (model type
        /// <paramref name="model"/>) on a worker thread. Returns the job (poll <see cref="MeshJob.Mesh"/> after
        /// <see cref="Pump"/>; the caller then owns and destroys the mesh and hands the job back with
        /// <see cref="Recycle"/>, or drops it unfinished with <see cref="Cancel"/>), or null when every job is busy (ask
        /// again next frame).
        /// </summary>
        public MeshJob RequestUnique(int variant, byte livery, byte model, VehicleLod lod, uint plateSeed, bool rider = false)
        {
            return Start(variant, livery, model, lod, plateSeed, rider);
        }

        /// <summary>Hands a finished unique job back to the pool (its mesh now belongs to the caller).</summary>
        public void Recycle(MeshJob job)
        {
            if (job == null || job.Shared || _inflight.Contains(job)) return;
            job.Reset();
            _freeJobs.Push(job);
        }

        /// <summary>Drops a unique job the caller no longer wants: an uploaded mesh is destroyed, a running build is
        /// discarded when it finishes.</summary>
        public void Cancel(MeshJob job)
        {
            if (job == null || job.Shared) return;
            if (job.Mesh != null)
            {
                Destroy(job.Mesh);
                job.Mesh = null;
            }
            if (_inflight.Contains(job)) job.Abandoned = true;
            else Recycle(job);
        }

        /// <summary>
        /// Main thread, once per frame: uploads up to <paramref name="maxUploads"/> bodies the workers have finished
        /// (shared ones go into the cache, unique ones to their job) and recycles abandoned jobs. Returns the uploads done.
        /// </summary>
        public int Pump(int maxUploads = 1)
        {
            int uploads = 0;
            for (int i = _inflight.Count - 1; i >= 0; i--)
            {
                MeshJob job = _inflight[i];
                int state = job.State;
                if (state == MeshJob.Queued) continue;
                if (state == MeshJob.Built && !job.Abandoned)
                {
                    if (uploads >= maxUploads) continue;
                    uploads++;
                    Mesh mesh = Upload(job.Data, VehicleCatalog.At(job.Variant).AssetId + (job.Shared ? "_" + VehicleMesher.ModelName(job.Variant, job.Model) + "_" + job.Lod : "_unique"));
                    if (job.Shared) _bodies[job.Key] = mesh;
                    else job.Mesh = mesh;
                }
                else if (state == MeshJob.Failed && !s_warned)
                {
                    s_warned = true;
                    Debug.LogWarning("VehicleMeshCache: a vehicle body failed to build on a worker thread; the coarser level stays.");
                }
                _inflight[i] = _inflight[_inflight.Count - 1];
                _inflight.RemoveAt(_inflight.Count - 1);
                if (job.Shared)
                {
                    _pendingBodies.Remove(job.Key);
                    job.Reset();
                    _freeJobs.Push(job);
                }
                else if (job.Abandoned || state == MeshJob.Failed)
                {
                    if (job.Abandoned)
                    {
                        job.Reset();
                        _freeJobs.Push(job);
                    }
                    else job.Done = true; // the owner sees Failed and recycles it
                }
                else job.Done = true;
            }
            return uploads;
        }

        private int _warmV, _warmK, _warmL;

        /// <summary>
        /// Background warm-up of the shared moving bodies at <paramref name="lod"/> (LOD1: every variant × model type ×
        /// livery, with riders on two-wheelers and rickshaws as traffic draws them, ≈ 155 meshes / 8 MB): call once per
        /// frame; each call queues builds on the worker threads while jobs are free (<see cref="TryBody"/>) and
        /// <see cref="Pump"/> uploads them a few per frame. Returns true once every body is queued or cached.
        /// </summary>
        public bool WarmStep(VehicleLod lod)
        {
            while (_warmV < VehicleCatalog.Count)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(_warmV);
                if (_warmK >= VehicleMesher.ModelCount(_warmV))
                {
                    _warmV++;
                    _warmK = 0;
                    _warmL = 0;
                    continue;
                }
                if (_warmL >= e.LiveryCount)
                {
                    _warmK++;
                    _warmL = 0;
                    continue;
                }
                byte mdl;
                bool rd;
                ulong key = BodyKey(_warmV, (byte)_warmL, (byte)_warmK, lod, Carries(e.Shape), out mdl, out rd);
                if (!_bodies.ContainsKey(key) && !_pendingBodies.ContainsKey(key))
                {
                    if (_inflight.Count >= MaxJobs) return false;
                    Mesh unused;
                    TryBody(_warmV, (byte)_warmL, (byte)_warmK, lod, rd, out unused);
                }
                _warmL++;
            }
            return true;
        }

        /// <summary>Bodies being built on workers.</summary>
        public int Pending
        {
            get { return _inflight.Count; }
        }

        private MeshJob Start(int variant, byte livery, byte model, VehicleLod lod, uint seed, bool rider)
        {
            if (_inflight.Count >= MaxJobs) return null;
            MeshJob job = _freeJobs.Count > 0 ? _freeJobs.Pop() : new MeshJob();
            job.Reset();
            job.Variant = variant;
            job.Livery = livery;
            job.Model = model;
            job.Lod = lod;
            job.Seed = seed;
            job.Rider = rider;
            job.State = MeshJob.Queued;
            _inflight.Add(job);
            System.Threading.ThreadPool.QueueUserWorkItem(s_work, job);
            return job;
        }

        /// <summary>Worker thread: builds the job's body into its own <see cref="MeshData"/> (no Unity API).</summary>
        private static void Work(object state)
        {
            var job = (MeshJob)state;
            try
            {
                job.Data.Clear();
                VehicleMesher.Build(job.Variant, job.Livery, job.Model, job.Lod, job.Data, job.Seed, job.Rider);
                job.State = MeshJob.Built;
            }
            catch (Exception)
            {
                job.State = MeshJob.Failed;
            }
        }

        /// <summary>The wheel of a catalogue socket (axis along X, centred on the origin) for LOD0 and LOD1 bodies;
        /// sockets of one style share meshes to the centimetre.</summary>
        public Mesh Wheel(in WheelSocket socket, VehicleLod lod)
        {
            return Wheel(socket.Style, socket.Radius, socket.Width, lod);
        }

        /// <summary>A wheel by size only: the style of the catalogue socket of that size (<see cref="VehicleMesher.StyleFor"/>,
        /// unambiguous over every entry and model type). Prefer <see cref="Wheel(in WheelSocket, VehicleLod)"/> with a
        /// socket of <see cref="VehicleMesher.Wheels(in VehicleCatalogEntry, int)"/> for the vehicle's own model type: car
        /// model types differ in track as well as in wheel look.</summary>
        public Mesh Wheel(float radius, float width, VehicleLod lod)
        {
            return Wheel(VehicleMesher.StyleFor(radius, width), radius, width, lod);
        }

        /// <summary>A wheel of <paramref name="style"/> (axis along X, centred on the origin).</summary>
        public Mesh Wheel(WheelStyle style, float radius, float width, VehicleLod lod)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(radius * 100f), 1, 0xFFFF);
            uint w = (uint)Mathf.Clamp(Mathf.RoundToInt(width * 100f), 1, 0xFFFF);
            ulong key = ((ulong)(byte)style << 48) | ((ulong)r << 32) | ((ulong)w << 8) | (byte)lod;
            Mesh mesh;
            if (_wheels.TryGetValue(key, out mesh) && mesh != null) return mesh;
            _scratch.Clear();
            VehicleMesher.BuildWheel(style, r * 0.01f, w * 0.01f, lod, _scratch);
            mesh = Upload(_scratch, "ghm_veh_wheel_" + style + "_" + lod);
            _wheels[key] = mesh;
            return mesh;
        }

        /// <summary>A body with its own plate number from <paramref name="plateSeed"/>, as the model type that seed picks;
        /// the caller owns (and destroys) it.</summary>
        public Mesh CreateUnique(int variant, byte livery, VehicleLod lod, uint plateSeed, bool rider = false)
        {
            return CreateUnique(variant, livery, VehicleMesher.ModelFor(variant, plateSeed), lod, plateSeed, rider);
        }

        /// <summary>A body of model type <paramref name="model"/> with its own plate number from
        /// <paramref name="plateSeed"/>; the caller owns (and destroys) it.</summary>
        public Mesh CreateUnique(int variant, byte livery, byte model, VehicleLod lod, uint plateSeed, bool rider = false)
        {
            _scratch.Clear();
            VehicleMesher.Build(variant, livery, model, lod, _scratch, plateSeed, rider);
            return Upload(_scratch, VehicleCatalog.At(variant).AssetId + "_unique");
        }

        /// <summary>Builds every model type and livery of every catalogue entry at <paramref name="lod"/> (load time),
        /// with the rider versions of two-wheelers and rickshaws at LOD1 and LOD2 (moving traffic; at LOD1 only those,
        /// since parked vehicles never use it), and at LOD0 and LOD1 the wheels of every model type
        /// (<see cref="PrewarmWheels"/>).</summary>
        public void Prewarm(VehicleLod lod)
        {
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                int models = lod >= VehicleLod.Block ? 1 : VehicleMesher.ModelCount(v);
                bool carries = Carries(e.Shape), riders = lod <= VehicleLod.Lod2 && lod >= VehicleLod.Lod1 && carries;
                bool plain = lod != VehicleLod.Lod1 || !carries;
                for (int k = 0; k < models; k++)
                    for (int l = 0; l < e.LiveryCount; l++)
                    {
                        if (plain) Body(v, (byte)l, (byte)k, lod);
                        if (riders) Body(v, (byte)l, (byte)k, lod, true);
                    }
            }
            if (lod <= VehicleLod.Lod1) PrewarmWheels(lod);
        }

        /// <summary>Builds the wheel of every socket of every catalogue entry and model type at <paramref name="lod"/>
        /// (LOD0 or LOD1; a few dozen small meshes, shared to the centimetre).</summary>
        public void PrewarmWheels(VehicleLod lod)
        {
            for (int v = 0; v < VehicleCatalog.Count; v++)
                for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                    foreach (WheelSocket s in VehicleMesher.Wheels(VehicleCatalog.At(v), k)) Wheel(s, lod);
        }

        /// <summary>Shapes drawn with a rider (or a puller) in traffic.</summary>
        private static bool Carries(BodyShape s)
        {
            return s == BodyShape.Scooter || s == BodyShape.Motorbike || s == BodyShape.Cruiser || s == BodyShape.Bicycle || s == BodyShape.Rickshaw;
        }

        /// <summary>Destroys every cached mesh (builds still running are discarded when they finish).</summary>
        public void Dispose()
        {
            for (int i = 0; i < _inflight.Count; i++) _inflight[i].Abandoned = true;
            _inflight.Clear();
            _pendingBodies.Clear();
            _warmV = _warmK = _warmL = 0;
            foreach (Mesh m in _bodies.Values) Destroy(m);
            foreach (Mesh m in _wheels.Values) Destroy(m);
            _bodies.Clear();
            _wheels.Clear();
        }

        private static void Destroy(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(m);
            else UnityEngine.Object.DestroyImmediate(m);
        }

        /// <summary>One <see cref="MeshData"/> to a GPU-only Unity mesh through the advanced Mesh API (with a UV0 stream
        /// when the data has one).</summary>
        public static Mesh Upload(MeshData m, string name)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int vc = m.VertexCount, ic = m.IndexCount;
            float minX, minY, minZ, maxX, maxY, maxZ;
            m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            var mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertexBufferParams(vc, m.HasUv0 ? LayoutUv : Layout);
            mesh.SetVertexBufferData(m.Positions, 0, 0, vc * 3, 0, Flags);
            mesh.SetVertexBufferData(m.Normals, 0, 0, vc * 3, 1, Flags);
            mesh.SetVertexBufferData(m.Colors, 0, 0, vc * 4, 2, Flags);
            if (m.HasUv0) mesh.SetVertexBufferData(m.Uv0, 0, 0, vc * 2, 3, Flags);
            if (vc <= 65535)
            {
                if (s_shorts == null || s_shorts.Length < ic) s_shorts = new ushort[Math.Max(ic, 8192)];
                for (int i = 0; i < ic; i++) s_shorts[i] = (ushort)m.Indices[i];
                mesh.SetIndexBufferParams(ic, IndexFormat.UInt16);
                mesh.SetIndexBufferData(s_shorts, 0, 0, ic, Flags);
            }
            else
            {
                mesh.SetIndexBufferParams(ic, IndexFormat.UInt32);
                mesh.SetIndexBufferData(m.Indices, 0, 0, ic, Flags);
            }
            var bounds = new Bounds(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f),
                                    new Vector3(maxX - minX, maxY - minY, maxZ - minZ));
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, ic, MeshTopology.Triangles)
            {
                firstVertex = 0,
                vertexCount = vc,
                bounds = bounds,
            }, Flags);
            mesh.bounds = bounds;
            mesh.UploadMeshData(true);
            return mesh;
        }
    }

    /// <summary>
    /// A vehicle body built off the main thread (<see cref="VehicleMeshCache.RequestUnique"/>,
    /// <see cref="VehicleMeshCache.TryBody"/>): the worker fills its own <see cref="MeshData"/>, the main thread uploads it
    /// in <see cref="VehicleMeshCache.Pump"/>. Pooled by the cache; nothing here is touched by the worker except
    /// <see cref="Data"/> and <see cref="State"/>.
    /// </summary>
    public sealed class MeshJob
    {
        internal const int Idle = 0, Queued = 1, Built = 2, Failed = 3;

        internal int Variant;
        internal byte Livery, Model;
        internal VehicleLod Lod;
        internal uint Seed;
        internal bool Rider, Shared, Abandoned, Done;
        internal ulong Key;
        internal readonly MeshData Data = new MeshData(4096, 12288);
        private volatile int _state;

        internal int State
        {
            get { return _state; }
            set { _state = value; }
        }

        /// <summary>The uploaded mesh of a unique body once it is ready (null before; the caller owns it).</summary>
        public Mesh Mesh { get; internal set; }

        /// <summary>The build failed (the caller keeps its coarser level and recycles the job).</summary>
        public bool HasFailed
        {
            get { return Done && _state == Failed; }
        }

        internal void Reset()
        {
            _state = Idle;
            Shared = false;
            Abandoned = false;
            Done = false;
            Mesh = null;
            Key = 0;
        }
    }
}
