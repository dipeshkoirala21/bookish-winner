using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Ghumante.Core.Characters;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;
using Ghumante.World.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using SkinWeights = Ghumante.Core.Characters.SkinWeights;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// Unity meshes from Core's character meshes: a skinned mesh (positions, normals, colours, UV0 = material channel and
    /// baked AO, two bone weights, bind poses as inverse bone translations since the <c>hum</c> bind rotations are
    /// identity) and blend shapes from same-topology targets (the player's face states). Shared by the player avatar and
    /// the crowd. Main thread.
    /// </summary>
    public static class CharacterMeshes
    {
        public static Mesh UploadSkinned(MeshData m, SkinWeights w, HumanoidSkeleton skeleton, string name)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int n = m.VertexCount;
            var vertices = new Vector3[n];
            var normals = new Vector3[n];
            var colours = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                vertices[i] = new Vector3(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]);
                normals[i] = new Vector3(m.Normals[i * 3], m.Normals[i * 3 + 1], m.Normals[i * 3 + 2]);
                colours[i] = new Color32(m.Colors[i * 4], m.Colors[i * 4 + 1], m.Colors[i * 4 + 2], m.Colors[i * 4 + 3]);
            }
            var triangles = new int[m.IndexCount];
            Array.Copy(m.Indices, triangles, m.IndexCount);
            var mesh = new Mesh { name = name };
            mesh.indexFormat = n > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors32 = colours;
            if (m.HasUv0)
            {
                var uv = new Vector2[n];
                for (int i = 0; i < n; i++) uv[i] = new Vector2(m.Uv0[i * 2], m.Uv0[i * 2 + 1]);
                mesh.uv = uv;
            }
            mesh.triangles = triangles;
            if (w != null && skeleton != null)
            {
                var weights = new BoneWeight[n];
                for (int i = 0; i < n && i < w.Count; i++)
                    weights[i] = new BoneWeight { boneIndex0 = w.Bone0[i], weight0 = w.Weight0[i], boneIndex1 = w.Bone1[i], weight1 = 1f - w.Weight0[i] };
                mesh.boneWeights = weights;
                var bindposes = new Matrix4x4[HumanoidSkeleton.BoneCount];
                for (int b = 0; b < bindposes.Length; b++)
                {
                    V3 p = skeleton.BindPosition[b];
                    bindposes[b] = Matrix4x4.Translate(new Vector3(-p.X, -p.Y, -p.Z));
                }
                mesh.bindposes = bindposes;
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Adds <paramref name="target"/> (same topology as <paramref name="basis"/>) as a one-frame blend shape.
        /// Returns false (nothing added) when the vertex counts differ.</summary>
        public static bool AddBlendShape(Mesh mesh, string name, MeshData basis, MeshData target)
        {
            if (mesh == null || basis == null || target == null || basis.VertexCount != target.VertexCount || mesh.vertexCount != basis.VertexCount) return false;
            int n = basis.VertexCount;
            var dv = new Vector3[n];
            var dn = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                int k = i * 3;
                dv[i] = new Vector3(target.Positions[k] - basis.Positions[k], target.Positions[k + 1] - basis.Positions[k + 1], target.Positions[k + 2] - basis.Positions[k + 2]);
                dn[i] = new Vector3(target.Normals[k] - basis.Normals[k], target.Normals[k + 1] - basis.Normals[k + 1], target.Normals[k + 2] - basis.Normals[k + 2]);
            }
            mesh.AddBlendShapeFrame(name, 100f, dv, dn, null);
            return true;
        }

        /// <summary>A static mesh (the far crowd's baked frames) with UV0.</summary>
        public static Mesh UploadStatic(MeshData m, string name)
        {
            return Streaming.MeshUpload.CreateWhole(m, name);
        }
    }

    /// <summary>
    /// Draws the crowd with the same generator as the player (W2_DESIGN 5.4 and 10.3, docs/research/w2/ref_characters.md):
    /// every person shows one of the shared variant bodies of its archetype, place and carry prop
    /// (<see cref="CrowdVariants"/>), built by <see cref="HumanoidMesher"/> on a worker thread and uploaded a few per frame.
    /// Near and mid people are <see cref="SkinnedMeshRenderer"/>s from a fixed pool posed with
    /// <see cref="CrowdPoser"/> (LOD0, LOD1 or LOD2 by <see cref="CrowdLodPlan"/>), far people are baked LOD2 poses
    /// (<see cref="CrowdBaker"/>, six walk phases, standing, sitting, palms together, arm up) drawn with
    /// <see cref="InstanceBatch"/> and tinted per person, so the four looks of a variant keep their colours with two far
    /// shapes. While a body is still building, a person falls back to a coarser ready level. Call <see cref="Begin"/>, then
    /// <see cref="Add"/> per person, then <see cref="End"/>, once per frame. Main thread only (the worker threads touch
    /// only engine-free data).
    /// </summary>
    public sealed class PeopleRenderer : IDisposable
    {
        /// <summary>Uploads per frame (each is one character mesh).</summary>
        public const int UploadsPerFrame = 2;

        private sealed class Body
        {
            public int Key;
            public CharacterRecipe Recipe;
            public HumanoidSkeleton Skeleton;
            public CrowdHold Hold;
            public uint Tint;
            public readonly Mesh[] Skinned = new Mesh[3];
            public readonly bool[] Queued = new bool[3];
            public readonly int[] Tris = new int[3];
            public int LastUsed;

            // Far: the tint-masked LOD2 for baking, its frames and their batches.
            public MeshData FarMesh;
            public SkinWeights FarWeights;
            public bool FarQueued;
            public readonly InstanceBatch[] Frames = new InstanceBatch[CrowdVariants.FrameCount];
            public int FarTris;
        }

        private sealed class Npc
        {
            public GameObject Go;
            public Transform[] Bones;
            public SkinnedMeshRenderer Renderer;
            public Body Body;
            public int Lod = -1;
            public bool Active;
        }

        private struct Job
        {
            public Body Body;
            public int Lod; // 0..2 skinned, 3 far
        }

        private struct Result
        {
            public Body Body;
            public int Lod;
            public MeshData Mesh;
            public SkinWeights Weights;
        }

        private readonly Material _skinnedMaterial, _farMaterial;
        private readonly int _tier;
        private readonly Dictionary<int, Body> _bodies = new Dictionary<int, Body>();
        private readonly Npc[] _pool;
        private readonly ConcurrentQueue<Job> _jobs = new ConcurrentQueue<Job>();
        private readonly ConcurrentQueue<Result> _results = new ConcurrentQueue<Result>();
        private readonly List<InstanceBatch> _used = new List<InstanceBatch>(64);
        private readonly HashSet<InstanceBatch> _usedSet = new HashSet<InstanceBatch>();
        private readonly List<int> _evict = new List<int>();
        private readonly Quat[] _local = new Quat[HumanoidSkeleton.BoneCount];
        private readonly CrowdBaker _baker = new CrowdBaker();
        private readonly Transform _root;
        private int _poolUsed, _frame, _workers;
        private Bounds _bounds;
        private bool _disposed;

        /// <summary>Most worker builds in flight.</summary>
        private const int MaxWorkers = 2;

        public PeopleRenderer(WorldMaterialSet materials, Transform parent, int tier)
        {
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            _skinnedMaterial = materials.instanced;
            _farMaterial = materials.instancedTint;
            _tier = tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
            int[] caps = CrowdLodPlan.Caps[_tier];
            var holder = new GameObject("Crowd");
            if (parent != null) holder.transform.SetParent(parent, false);
            _root = holder.transform;
            _pool = new Npc[caps[0] + caps[1] + 2];
            for (int i = 0; i < _pool.Length; i++) _pool[i] = MakeNpc(i);
        }

        /// <summary>People and triangles submitted since <see cref="Begin"/>, and draw calls.</summary>
        public int People { get; private set; }

        public int Tris { get; private set; }

        public int Draws { get; private set; }

        /// <summary>Variant bodies alive (for the debug HUD).</summary>
        public int BodyCount
        {
            get { return _bodies.Count; }
        }

        private Npc MakeNpc(int index)
        {
            var go = new GameObject("Crowd NPC " + index);
            go.transform.SetParent(_root, false);
            var bones = new Transform[HumanoidSkeleton.BoneCount];
            bones[0] = go.transform;
            for (int i = 1; i < bones.Length; i++)
            {
                var b = new GameObject(HumanoidSkeleton.Names[i]);
                b.transform.SetParent(bones[HumanoidSkeleton.Parent[i]], false);
                bones[i] = b.transform;
            }
            var holder = new GameObject("Body");
            holder.transform.SetParent(go.transform, false);
            SkinnedMeshRenderer r = holder.AddComponent<SkinnedMeshRenderer>();
            r.sharedMaterial = _skinnedMaterial;
            r.bones = bones;
            r.rootBone = bones[(int)Bone.Hips];
            r.updateWhenOffscreen = false;
            r.quality = SkinQuality.Bone2;
            r.localBounds = new Bounds(new Vector3(0f, 0.2f, 0f), new Vector3(2.4f, 2.6f, 2.4f));
            go.SetActive(false);
            return new Npc { Go = go, Bones = bones, Renderer = r };
        }

        public void Begin(Vector3 cameraScene)
        {
            _frame++;
            _bounds = new Bounds(cameraScene, new Vector3(1000f, 600f, 1000f));
            People = 0;
            Tris = 0;
            Draws = 0;
            _poolUsed = 0;
            for (int i = 0; i < _used.Count; i++) _used[i].ResetCounters();
            _used.Clear();
            _usedSet.Clear();
            Collect();
        }

        /// <summary>
        /// One person at scene position <paramref name="p"/> facing <paramref name="headingDeg"/> (clockwise from north):
        /// body <paramref name="key"/> (<see cref="CrowdVariants.Key"/>) in <paramref name="band"/> (0 near, 1 mid,
        /// 2 far) at <paramref name="rank"/> within the band (0 = nearest); <paramref name="walkPhase"/> picks the far
        /// frame of a walk (<see cref="CrowdAnimation.WalkPhase"/>).
        /// </summary>
        public void Add(Vector3 p, float headingDeg, in PersonPose pose, int key, int band, int rank, PedClip clip, double walkPhase)
        {
            Body body = BodyFor(key);
            body.LastUsed = _frame;
            People++;
            Quaternion rot = Quaternion.Euler(0f, headingDeg, 0f);
            if (CrowdLodPlan.Skinned(band) && _poolUsed < _pool.Length)
            {
                int want = CrowdLodPlan.MeshLod(_tier, band, rank);
                int lod = Ready(body, want);
                if (lod >= 0)
                {
                    Skinned(_pool[_poolUsed++], body, lod, p, rot, pose, band == 0);
                    return;
                }
            }
            Far(body, p, rot, clip, walkPhase);
        }

        public void End()
        {
            for (int i = 0; i < _used.Count; i++)
            {
                InstanceBatch b = _used[i];
                b.Flush();
                Tris += b.Tris;
                Draws += b.Draws;
            }
            for (int i = _poolUsed; i < _pool.Length; i++)
            {
                Npc n = _pool[i];
                if (n.Active)
                {
                    n.Go.SetActive(false);
                    n.Active = false;
                }
            }
            Pump();
            if ((_frame & 63) == 0) Evict();
        }

        // ----- Bodies and building -----------------------------------------------------------------------------------

        private Body BodyFor(int key)
        {
            if (_bodies.TryGetValue(key, out Body b)) return b;
            CharacterRecipe r = CrowdVariants.Recipe(key);
            b = new Body { Key = key, Recipe = r, Skeleton = new HumanoidSkeleton(r), Hold = CrowdVariants.HoldOf(r), Tint = HumanoidMesher.TintKeyOf(r) };
            _bodies.Add(key, b);
            return b;
        }

        /// <summary>The level to draw a body at: the wanted one when ready, else the nearest ready coarser or finer one
        /// (the build is queued); −1 when none is ready yet.</summary>
        private int Ready(Body b, int want)
        {
            if (b.Skinned[want] != null) return want;
            Queue(b, want);
            for (int l = want + 1; l <= 2; l++)
                if (b.Skinned[l] != null) return l;
            for (int l = want - 1; l >= 0; l--)
                if (b.Skinned[l] != null) return l;
            if (want != 2) Queue(b, 2);
            return -1;
        }

        private void Queue(Body b, int lod)
        {
            if (lod < 3)
            {
                if (b.Queued[lod]) return;
                b.Queued[lod] = true;
            }
            else
            {
                if (b.FarQueued) return;
                b.FarQueued = true;
            }
            _jobs.Enqueue(new Job { Body = b, Lod = lod });
            Pump();
        }

        /// <summary>Starts worker builds while there are jobs and free workers.</summary>
        private void Pump()
        {
            while (_workers < MaxWorkers && _jobs.TryDequeue(out Job job))
            {
                Interlocked.Increment(ref _workers);
                Job j = job;
                ThreadPool.QueueUserWorkItem(_ => Work(j));
            }
        }

        private void Work(Job j)
        {
            try
            {
                if (_disposed) return;
                var m = new MeshData(j.Lod == 0 ? 12288 : 4096, j.Lod == 0 ? 36864 : 12288);
                var w = new SkinWeights(m.VertexCapacity);
                var o = CharacterMeshOptions.For(HeadwearMode.Outfit);
                o.TintMask = j.Lod == 3;
                HumanoidMesher.Build(j.Body.Recipe, j.Lod >= 3 ? 2 : j.Lod, m, w, o);
                _results.Enqueue(new Result { Body = j.Body, Lod = j.Lod, Mesh = m, Weights = w });
            }
            catch (Exception)
            {
                // A failed body stays unbuilt; the person keeps a coarser level or the far frame.
            }
            finally
            {
                Interlocked.Decrement(ref _workers);
            }
        }

        /// <summary>Uploads finished builds (a few per frame).</summary>
        private void Collect()
        {
            for (int k = 0; k < UploadsPerFrame && _results.TryDequeue(out Result res); k++)
            {
                Body b = res.Body;
                if (!_bodies.ContainsKey(b.Key)) continue; // evicted meanwhile
                if (res.Lod < 3)
                {
                    b.Skinned[res.Lod] = CharacterMeshes.UploadSkinned(res.Mesh, res.Weights, b.Skeleton, "crowd_" + b.Key.ToString("X") + "_lod" + res.Lod);
                    b.Tris[res.Lod] = res.Mesh.TriangleCount;
                }
                else
                {
                    b.FarMesh = res.Mesh;
                    b.FarWeights = res.Weights;
                    b.FarTris = res.Mesh.TriangleCount;
                }
            }
        }

        // ----- Drawing -----------------------------------------------------------------------------------------------

        private void Skinned(Npc n, Body body, int lod, Vector3 p, Quaternion rot, in PersonPose pose, bool shadows)
        {
            if (n.Body != body)
            {
                // A new body: its bones take the body's bind pose (age, build and figure change the skeleton).
                V3[] bind = body.Skeleton.BindLocal;
                for (int i = 1; i < n.Bones.Length; i++) n.Bones[i].localPosition = new Vector3(bind[i].X, bind[i].Y, bind[i].Z);
                n.Body = body;
                n.Lod = -1;
            }
            if (n.Lod != lod)
            {
                n.Renderer.sharedMesh = body.Skinned[lod];
                n.Lod = lod;
            }
            n.Renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (!n.Active)
            {
                n.Go.SetActive(true);
                n.Active = true;
            }
            n.Go.transform.SetPositionAndRotation(p, rot);
            CrowdPose cp = pose.Body;
            cp.Hold = body.Hold;
            CrowdPoser.Solve(cp, body.Skeleton, _local, out V3 hips);
            for (int i = 2; i < n.Bones.Length; i++)
            {
                Quat q = _local[i];
                n.Bones[i].localRotation = new Quaternion(q.X, q.Y, q.Z, q.W);
            }
            V3 bh = body.Skeleton.BindLocal[(int)Bone.Hips];
            n.Bones[(int)Bone.Hips].localPosition = new Vector3(bh.X + hips.X, bh.Y + hips.Y, bh.Z + hips.Z);
            Tris += body.Tris[lod];
            Draws++;
        }

        private void Far(Body body, Vector3 p, Quaternion rot, PedClip clip, double walkPhase)
        {
            Body far = BodyFor(CrowdVariants.FarKey(body.Key));
            far.LastUsed = _frame;
            if (far.FarMesh == null)
            {
                Queue(far, 3);
                return;
            }
            FarFrame f = CrowdVariants.FrameOf(clip, walkPhase);
            InstanceBatch batch = far.Frames[(int)f];
            if (batch == null)
            {
                var baked = new MeshData(far.FarMesh.VertexCount + 4, far.FarMesh.IndexCount + 6);
                _baker.Bake(far.FarMesh, far.FarWeights, far.Skeleton, CrowdVariants.FramePose(f, far.Hold), baked);
                Mesh mesh = CharacterMeshes.UploadStatic(baked, "crowd_far_" + far.Key.ToString("X") + "_" + f);
                batch = new InstanceBatch(mesh, _farMaterial, baked.TriangleCount, true) { Shadows = ShadowCastingMode.Off };
                far.Frames[(int)f] = batch;
            }
            batch.WorldBounds = _bounds;
            if (_usedSet.Add(batch)) _used.Add(batch);
            batch.Add(Matrix4x4.TRS(p, rot, Vector3.one), Tint.Hex(body.Tint));
        }

        /// <summary>Drops bodies unused for a while (a few seconds), beyond a small working set.</summary>
        private void Evict()
        {
            int keep = _tier == 0 ? 24 : _tier == 1 ? 48 : 80;
            if (_bodies.Count <= keep) return;
            _evict.Clear();
            foreach (KeyValuePair<int, Body> kv in _bodies)
                if (_frame - kv.Value.LastUsed > 300 && !InPool(kv.Value)) _evict.Add(kv.Key);
            for (int i = 0; i < _evict.Count; i++)
            {
                Body b = _bodies[_evict[i]];
                Destroy(b);
                _bodies.Remove(_evict[i]);
            }
        }

        private bool InPool(Body b)
        {
            for (int i = 0; i < _pool.Length; i++)
                if (_pool[i].Body == b) return true;
            return false;
        }

        private static void Destroy(Body b)
        {
            for (int l = 0; l < 3; l++)
            {
                Kill(b.Skinned[l]);
                b.Skinned[l] = null;
            }
            for (int f = 0; f < b.Frames.Length; f++)
            {
                if (b.Frames[f] != null) b.Frames[f].DestroyMesh();
                b.Frames[f] = null;
            }
            b.FarMesh = null;
            b.FarWeights = null;
        }

        private static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        public void Dispose()
        {
            _disposed = true;
            foreach (KeyValuePair<int, Body> kv in _bodies) Destroy(kv.Value);
            _bodies.Clear();
            if (_root != null) Kill(_root.gameObject);
        }
    }
}
