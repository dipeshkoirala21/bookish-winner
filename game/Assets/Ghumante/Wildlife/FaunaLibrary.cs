using System;
using System.Collections.Generic;
using Ghumante.Core.Generators.Fauna;
using Ghumante.Core.Meshing;
using Ghumante.World.Instancing;
using Ghumante.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.Wildlife
{
    /// <summary>
    /// The fauna meshes of the wildlife presenters: the skinned bind meshes per (species, level, coat pattern) built on
    /// demand by <see cref="FaunaMesher"/>, and their baked keyframes (<see cref="FaunaSkinner.Bake"/> of a
    /// <see cref="FaunaAnimator"/> clip at evenly spaced phases) uploaded once and drawn instanced with a coat tint
    /// (<see cref="InstanceBatch"/>, GLES3-safe GPU instancing). Far animals and flock birds pick the frame nearest their
    /// clip phase; paper birds have four flap frames. Baking is spread out (<see cref="MaxBakesPerFrame"/>), so a new
    /// flock costs a few milliseconds over a few frames, never a hitch. Main thread only; nothing allocates per frame
    /// once a key has been seen.
    /// </summary>
    public sealed class FaunaLibrary : IDisposable
    {
        /// <summary>New keyframe meshes baked per frame at most (the rest wait a frame, drawn from a neighbour frame).</summary>
        public int MaxBakesPerFrame = 6;

        private readonly Material _material;
        private readonly Dictionary<int, FaunaMesh> _meshes = new Dictionary<int, FaunaMesh>();
        private readonly Dictionary<int, InstanceBatch> _frames = new Dictionary<int, InstanceBatch>();
        private readonly List<InstanceBatch> _all = new List<InstanceBatch>();
        private readonly FaunaPose _pose = new FaunaPose();
        private readonly FaunaSkinner _skinner = new FaunaSkinner();
        private readonly MeshData _scratch = new MeshData(2048, 6144);
        private int _bakes;
        private Bounds _bounds = new Bounds(Vector3.zero, new Vector3(1000f, 600f, 1000f));

        public FaunaLibrary(Material instancedTint)
        {
            _material = instancedTint;
        }

        /// <summary>Triangles, draws and instances submitted since <see cref="Begin"/>.</summary>
        public int Tris { get; private set; }

        public int Draws { get; private set; }

        public int Instances { get; private set; }

        /// <summary>Keyframe meshes uploaded so far (memory watch).</summary>
        public int FrameMeshes
        {
            get { return _frames.Count; }
        }

        /// <summary>Starts a frame: counters reset, culling bounds round the camera, bake budget refilled.</summary>
        public void Begin(Vector3 cameraScene)
        {
            _bounds = new Bounds(cameraScene, new Vector3(1200f, 800f, 1200f));
            for (int i = 0; i < _all.Count; i++)
            {
                _all[i].ResetCounters();
                _all[i].WorldBounds = _bounds;
            }
            _bakes = 0;
        }

        /// <summary>Submits every batch with instances and totals the counters.</summary>
        public void End()
        {
            int tris = 0, draws = 0, inst = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                InstanceBatch b = _all[i];
                if (b.Pending > 0) b.Flush();
                tris += b.Tris;
                draws += b.Draws;
                inst += b.Instances;
            }
            Tris = tris;
            Draws = draws;
            Instances = inst;
        }

        /// <summary>The skinned bind mesh of a species at a level and pattern (built on first use).</summary>
        public FaunaMesh Mesh(FaunaSpecies species, int lod, CoatPattern pattern)
        {
            int key = ((int)species * 4 + Math.Max(0, Math.Min(2, lod))) * 8 + (int)pattern;
            FaunaMesh m;
            if (!_meshes.TryGetValue(key, out m))
            {
                m = new FaunaMesh(1024, 3072);
                FaunaMesher.Build(species, lod, pattern, m);
                _meshes.Add(key, m);
            }
            return m;
        }

        /// <summary>Frames baked for a clip at a level (looping gaits get more; static rests one on the far level).</summary>
        public static int FrameCount(FaunaClip clip, int lod)
        {
            switch (clip)
            {
                case FaunaClip.Walk:
                case FaunaClip.Trot:
                case FaunaClip.Run:
                case FaunaClip.Flap:
                case FaunaClip.TakeOff:
                case FaunaClip.Swim:
                    return lod >= 2 ? 6 : 8;
                case FaunaClip.Peck:
                case FaunaClip.Hop:
                case FaunaClip.Scratch:
                case FaunaClip.Bark:
                case FaunaClip.Graze:
                case FaunaClip.Groom:
                case FaunaClip.Climb:
                    return 4;
                case FaunaClip.Lie:
                case FaunaClip.Sleep:
                case FaunaClip.Sit:
                    return lod >= 2 ? 1 : 2;
                default:
                    return lod >= 2 ? 2 : 3;
            }
        }

        /// <summary>
        /// The keyframe batch for <paramref name="clip"/> at clip time <paramref name="clipTime"/> (and ground speed
        /// <paramref name="speedMps"/>, which sets the gait's stride rate), baking it on first use within the frame's
        /// budget; null when not baked yet and the budget is spent (draw nothing this frame).
        /// </summary>
        public InstanceBatch Frame(FaunaSpecies species, CoatPattern pattern, int lod, FaunaClip clip, float clipTime, float speedMps)
        {
            int frames = FrameCount(clip, lod);
            float cycle = FaunaAnimator.CycleSeconds(species, clip, speedMps);
            int f = frames <= 1 ? 0 : (int)(Frac(clipTime / Math.Max(0.05f, cycle)) * frames) % frames;
            int key = ((((int)species * 4 + lod) * 8 + (int)pattern) * 32 + (int)clip) * 16 + f;
            InstanceBatch b;
            if (_frames.TryGetValue(key, out b)) return b;
            if (_bakes >= MaxBakesPerFrame) return null;
            _bakes++;
            FaunaMesh m = Mesh(species, lod, pattern);
            float t = frames <= 1 ? 0f : f * FaunaAnimator.CycleSeconds(species, clip, 0f) / frames;
            FaunaAnimator.Evaluate(m, clip, t, 0f, 0u, _pose);
            _skinner.Bake(m, _pose, _scratch);
            Compact(_scratch);
            b = NewBatch(_scratch, species + "_" + clip + "_" + lod + "_" + f, lod == 0);
            _frames.Add(key, b);
            return b;
        }

        /// <summary>The paper bird of a species at a flap phase (four frames per species, one draw each).</summary>
        public InstanceBatch Paper(FaunaSpecies species, float flapPhase01)
        {
            int f = (int)(Frac(flapPhase01) * 4f) & 3;
            int key = int.MinValue + (int)species * 4 + f;
            InstanceBatch b;
            if (_frames.TryGetValue(key, out b)) return b;
            BirdMesher.PaperBird(species, f == 0 ? 0.9f : f == 1 ? 0.2f : f == 2 ? -0.7f : 0.1f, _scratch);
            b = NewBatch(_scratch, species + "_paper_" + f, false);
            _frames.Add(key, b);
            return b;
        }

        private InstanceBatch NewBatch(MeshData m, string name, bool shadows)
        {
            Mesh mesh = MeshUpload.CreateWhole(m, "fauna_" + name);
            var b = new InstanceBatch(mesh, _material, m.TriangleCount, true)
            {
                Shadows = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                WorldBounds = _bounds,
            };
            _all.Add(b);
            return b;
        }

        /// <summary>Drops vertices no triangle uses (the hidden wing set of a baked bird), renumbering the indices.</summary>
        private static void Compact(MeshData m)
        {
            int n = m.VertexCount;
            if (_remap == null || _remap.Length < n) _remap = new int[Math.Max(n, 4096)];
            for (int v = 0; v < n; v++) _remap[v] = -1;
            int w = 0;
            for (int i = 0; i < m.IndexCount; i++)
            {
                int v = m.Indices[i];
                if (_remap[v] < 0) _remap[v] = -2;
            }
            for (int v = 0; v < n; v++)
            {
                if (_remap[v] != -2) continue;
                _remap[v] = w;
                if (w != v)
                {
                    Array.Copy(m.Positions, 3 * v, m.Positions, 3 * w, 3);
                    Array.Copy(m.Normals, 3 * v, m.Normals, 3 * w, 3);
                    Array.Copy(m.Colors, 4 * v, m.Colors, 4 * w, 4);
                    Array.Copy(m.Uv0, 2 * v, m.Uv0, 2 * w, 2);
                }
                w++;
            }
            for (int i = 0; i < m.IndexCount; i++) m.Indices[i] = _remap[m.Indices[i]];
            m.VertexCount = w;
        }

        [ThreadStatic] private static int[] _remap;

        private static float Frac(float x)
        {
            return x - (float)Math.Floor(x);
        }

        public void Dispose()
        {
            foreach (InstanceBatch b in _all) b.DestroyMesh();
            _all.Clear();
            _frames.Clear();
            _meshes.Clear();
        }
    }
}
