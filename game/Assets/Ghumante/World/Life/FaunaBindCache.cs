using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ghumante.Core.Generators.Fauna;

namespace Ghumante.World.Life
{
    /// <summary>
    /// The skinned bind meshes of the fauna presenters per (species, level, coat pattern), built on worker threads
    /// (W2 perf pass, owner playtest #4 "stutters"): a near-level bind mesh takes 2-11 ms to build on a desktop (a cow at
    /// LOD0 10.6 ms, a dog 9.4 ms, a macaque at LOD1 5.4 ms; 3-5 times that on a phone), which the main thread used to
    /// pay the first time such an animal came close. <see cref="TryGet"/> never blocks: it starts a worker build and
    /// answers false until the mesh is ready, so the presenter draws a baked stand-in meanwhile; <see cref="Prewarm"/>
    /// starts the builds of every level of a species and coat the first time it is seen far away, so its near levels are
    /// ready before it comes close; <see cref="Get"/> (a presenter that must have the mesh now) takes a finished build,
    /// waits for one already running, or builds on the calling thread as the last resort. A failed build is retried on
    /// the calling thread by <see cref="Get"/>. Meshes are built once and kept for the session. Main thread (the builds
    /// run on the thread pool and touch nothing shared); engine-free.
    /// </summary>
    public sealed class FaunaBindCache
    {
        private readonly Dictionary<int, FaunaMesh> _ready = new Dictionary<int, FaunaMesh>();
        private readonly Dictionary<int, Task<FaunaMesh>> _building = new Dictionary<int, Task<FaunaMesh>>();
        private readonly HashSet<int> _failed = new HashSet<int>();

        /// <summary>Bind meshes built on the calling thread (<see cref="Get"/> with nothing built or building): the cost
        /// the frame paid.</summary>
        public int BuiltInline { get; private set; }

        /// <summary>Bind meshes built on worker threads.</summary>
        public int BuiltInBackground { get; private set; }

        /// <summary>Worker builds still running.</summary>
        public int Pending
        {
            get { return _building.Count; }
        }

        /// <summary>Bind meshes ready.</summary>
        public int Count
        {
            get { return _ready.Count; }
        }

        /// <summary>The cache key of a species at a level (0-2, clamped) and coat pattern.</summary>
        public static int Key(FaunaSpecies species, int lod, CoatPattern pattern)
        {
            return ((int)species * 4 + Math.Max(0, Math.Min(2, lod))) * 8 + (int)pattern;
        }

        /// <summary>True with the mesh when it is built; otherwise starts its worker build (once) and returns false
        /// without waiting.</summary>
        public bool TryGet(FaunaSpecies species, int lod, CoatPattern pattern, out FaunaMesh mesh)
        {
            int key = Key(species, lod, pattern);
            if (_ready.TryGetValue(key, out mesh)) return true;
            Task<FaunaMesh> task;
            if (_building.TryGetValue(key, out task))
            {
                if (!task.IsCompleted) return false;
                return Collect(key, task, out mesh);
            }
            if (_failed.Contains(key)) return false; // a worker build failed: only Get tries again
            Start(key, species, lod, pattern);
            mesh = null;
            return false;
        }

        /// <summary>The mesh now: a finished build, a running one waited for, or built on the calling thread.</summary>
        public FaunaMesh Get(FaunaSpecies species, int lod, CoatPattern pattern)
        {
            int key = Key(species, lod, pattern);
            FaunaMesh mesh;
            if (_ready.TryGetValue(key, out mesh)) return mesh;
            Task<FaunaMesh> task;
            if (_building.TryGetValue(key, out task))
            {
                try
                {
                    task.Wait();
                }
                catch (AggregateException)
                {
                    // Retried below on this thread.
                }
                if (Collect(key, task, out mesh)) return mesh;
            }
            mesh = Build(species, lod, pattern);
            BuiltInline++;
            _failed.Remove(key);
            _ready[key] = mesh;
            return mesh;
        }

        /// <summary>Starts the worker builds of every level of a species and coat that is neither built nor building.</summary>
        public void Prewarm(FaunaSpecies species, CoatPattern pattern)
        {
            for (int lod = 0; lod <= 2; lod++)
            {
                int key = Key(species, lod, pattern);
                if (_ready.ContainsKey(key) || _building.ContainsKey(key) || _failed.Contains(key)) continue;
                Start(key, species, lod, pattern);
            }
        }

        /// <summary>True when the mesh is built (no build is started).</summary>
        public bool IsReady(FaunaSpecies species, int lod, CoatPattern pattern)
        {
            int key = Key(species, lod, pattern);
            if (_ready.ContainsKey(key)) return true;
            Task<FaunaMesh> task;
            if (!_building.TryGetValue(key, out task) || !task.IsCompleted) return false;
            FaunaMesh mesh;
            return Collect(key, task, out mesh);
        }

        /// <summary>Waits for every running build (tests and shutdown); finished ones are collected on the next query.</summary>
        public void WaitAll()
        {
            foreach (Task<FaunaMesh> t in _building.Values)
            {
                try
                {
                    t.Wait();
                }
                catch (AggregateException)
                {
                }
            }
        }

        /// <summary>Forgets every mesh (running builds finish unseen).</summary>
        public void Clear()
        {
            _ready.Clear();
            _building.Clear();
            _failed.Clear();
        }

        private void Start(int key, FaunaSpecies species, int lod, CoatPattern pattern)
        {
            _building[key] = Task.Run(() => Build(species, lod, pattern));
        }

        private bool Collect(int key, Task<FaunaMesh> task, out FaunaMesh mesh)
        {
            _building.Remove(key);
            if (task.Status != TaskStatus.RanToCompletion || task.Result == null)
            {
                mesh = null;
                _failed.Add(key); // Get builds it on the calling thread (and reports the error there)
                return false;
            }
            mesh = task.Result;
            _ready[key] = mesh;
            BuiltInBackground++;
            return true;
        }

        private static FaunaMesh Build(FaunaSpecies species, int lod, CoatPattern pattern)
        {
            var m = new FaunaMesh(1024, 3072);
            FaunaMesher.Build(species, Math.Max(0, Math.Min(2, lod)), pattern, m);
            return m;
        }
    }
}
