using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Fauna;
using Ghumante.Core.Geo;
using Ghumante.Core.Synth;
using Ghumante.Core.Traffic;
using Ghumante.World;
using Ghumante.World.Instancing;
using UnityEngine;

namespace Ghumante.Wildlife
{
    /// <summary>
    /// Keeps the ambient wildlife alive round the player (W2_DESIGN 5.5–5.6): every two seconds the
    /// <see cref="AmbientFaunaPlanner"/> lists the groups within reach (the macaque troops of Swayambhu and Pashupati,
    /// the pigeon flocks of the Durbar squares and Boudha, crows, sparrows and mynas in the city, kites overhead,
    /// goats, hens, buffalo, ducks, egrets and swallows in the fringe and the fields), groups that come into reach are
    /// created with their own deterministic <see cref="FlockSim"/> or <see cref="HerdSim"/>, and groups that drop out
    /// are released. Each frame the sims step (the player and passing vehicles startle the flocks) and the birds are
    /// drawn nearest first under the bird caps: full birds within 8 m (0 / 4 / 8), light birds within 25 m (15 / 30 /
    /// 75), paper birds beyond; at most 1 / 1 / 2 pigeon flocks and 1 / 2 / 3 small flocks within 90 m on Low / Mid /
    /// High. The ground herds are handed to <see cref="AnimalPresenter"/>, which draws them with the street cows and
    /// dogs. Wing bursts, coos, caws, kite whistles and dawn roosters go through the world's sound service. Main thread.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ghumante/Wildlife Director")]
    public sealed class WildlifeDirector : MonoBehaviour
    {
        /// <summary>Groups exist within this distance of the player (m).</summary>
        public const float PlanRadiusM = 260f;

        /// <summary>Seconds between plans.</summary>
        public const float PlanIntervalS = 2f;

        /// <summary>A running group.</summary>
        public sealed class Group
        {
            public AmbientGroup Plan;
            public FlockSim Flock;
            public HerdSim Herd;
            public bool Seen;
        }

        private sealed class Habitat : IFaunaHabitat
        {
            public AreaTypeGrid Grid;

            public AreaType AreaAt(double x, double z)
            {
                return Grid != null ? Grid.At(x, z) : AreaType.Unknown;
            }
        }

        private WorldRoot _world;
        private AmbientFaunaPlanner _planner;
        private readonly Habitat _habitat = new Habitat();
        private readonly AmbientGroup[] _planned = new AmbientGroup[96];
        private readonly Dictionary<long, Group> _groups = new Dictionary<long, Group>();
        private readonly List<Group> _active = new List<Group>();
        private readonly List<long> _drop = new List<long>();
        private FaunaLibrary _library;
        private float _planTimer;
        private int _tier;
        private uint _rng = 0xC0FFEEu;

        // Bird drawing scratch.
        private float[] _dist = new float[512], _keys = new float[512];
        private int[] _level = new int[512], _order = new int[512];
        private Group[] _birdGroup = new Group[512];
        private int[] _birdIndex = new int[512];
        private readonly int[] _caps = { 0, 0, int.MaxValue };

        /// <summary>Bird level radii: full birds, light birds, paper birds (m).</summary>
        private static readonly float[] BirdRadii = { 8f, 25f, 600f };

        /// <summary>The running ground herds (macaques, goats, hens, ducks, buffalo) for <see cref="AnimalPresenter"/>.</summary>
        public readonly List<Group> Herds = new List<Group>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            WorldRoot.AnyReady += w => Attach(w);
        }

        public static WildlifeDirector Attach(WorldRoot world)
        {
            if (world == null) return null;
            WildlifeDirector d = world.GetComponent<WildlifeDirector>();
            if (d == null) d = world.gameObject.AddComponent<WildlifeDirector>();
            d.Bind(world);
            return d;
        }

        private void Bind(WorldRoot world)
        {
            _world = world;
            _tier = (int)world.Tier;
            _planner = new AmbientFaunaPlanner((uint)WorldRoot.RegionSeed(world.RegionId ?? "region"));
            _groups.Clear();
            _active.Clear();
            Herds.Clear();
            if (_library != null) _library.Dispose();
            _library = world.Materials != null ? new FaunaLibrary(world.Materials.instancedTint) : null;
            _planTimer = 0f;
        }

        /// <summary>Bird caps (full within 8 m, light within 25 m) for the device tier.</summary>
        public static void BirdCaps(int tier, out int full, out int light)
        {
            full = tier <= 0 ? 0 : tier == 1 ? 4 : 8;
            light = tier <= 0 ? 15 : tier == 1 ? 30 : 75;
        }

        /// <summary>Flocks allowed within 90 m: pigeon flocks and small flocks.</summary>
        public static void FlockCaps(int tier, out int pigeonFlocks, out int smallFlocks)
        {
            pigeonFlocks = tier <= 1 ? 1 : 2;
            smallFlocks = tier <= 0 ? 1 : tier == 1 ? 2 : 3;
        }

        private void Update()
        {
            if (_world == null || !_world.IsOpen || _planner == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            WorldPos focus = _world.Focus;
            _planTimer -= dt;
            if (_planTimer <= 0f)
            {
                _planTimer = PlanIntervalS;
                Replan(focus);
            }
            Step(dt, focus);
        }

        private void Replan(WorldPos focus)
        {
            _habitat.Grid = _world.AreaTypes;
            int n = _planner.Plan(focus.X, focus.Z, PlanRadiusM, Math.Max(1, _world.Month), _world.TimeOfDayHours, _habitat, _planned);
            foreach (Group g in _groups.Values) g.Seen = false;
            FlockCaps(_tier, out int pigeonCap, out int smallCap);
            int pigeons = 0, small = 0;
            for (int i = 0; i < n; i++)
            {
                AmbientGroup p = _planned[i];
                if (p.Kind == AmbientKind.PigeonFlock && p.DistanceM < 90f && ++pigeons > pigeonCap) continue;
                if (p.IsFlock && p.Kind != AmbientKind.PigeonFlock && p.Kind != AmbientKind.KiteGroup && p.DistanceM < 90f && ++small > smallCap) continue;
                Group g;
                if (!_groups.TryGetValue(p.Key, out g))
                {
                    g = Create(p);
                    if (g == null) continue;
                    _groups.Add(p.Key, g);
                }
                g.Seen = true;
            }
            _drop.Clear();
            foreach (KeyValuePair<long, Group> kv in _groups)
                if (!kv.Value.Seen)
                    _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) _groups.Remove(_drop[i]);
            _active.Clear();
            Herds.Clear();
            foreach (Group g in _groups.Values)
            {
                _active.Add(g);
                if (g.Herd != null) Herds.Add(g);
            }
        }

        private Group Create(in AmbientGroup p)
        {
            float y = GroundY(p.X, p.Z);
            if (float.IsNaN(y)) return null; // not loaded yet: try again at the next plan
            var g = new Group { Plan = p };
            if (p.IsFlock)
                g.Flock = new FlockSim(AmbientFaunaPlanner.FlockOf(p.Kind), Math.Min(p.Count, FlockSim.MaxBirds), p.X, p.Z, y, p.Seed);
            else
                g.Herd = new HerdSim(AmbientFaunaPlanner.SpeciesOf(p.Kind), Math.Min(p.Count, HerdSim.MaxAnimals), p.X, p.Z, y, p.RadiusM, p.Seed);
            return g;
        }

        /// <summary>Ground height under a point, NaN where nothing is loaded.</summary>
        public float GroundY(double x, double z)
        {
            IGroundQuery q = _world != null ? _world.Ground : null;
            GroundSample s;
            if (q != null && q.TrySample(x, z, out s)) return s.Height;
            return float.NaN;
        }

        private void Step(float dt, WorldPos focus)
        {
            ISoundService sound = _world.Sound;
            float hour = _world.TimeOfDayHours;
            for (int k = 0; k < _active.Count; k++)
            {
                Group g = _active[k];
                float px = (float)(focus.X - g.Plan.X), pz = (float)(focus.Z - g.Plan.Z);
                bool near = px * px + pz * pz < 400f * 400f;
                if (g.Flock != null)
                {
                    bool wasUp = g.Flock.Airborne;
                    if (VehicleNear(g.Plan.X, g.Plan.Z, g.Plan.RadiusM + 3f)) g.Flock.Startle();
                    g.Flock.Step(dt, px, pz, near);
                    if (sound != null && px * px + pz * pz < 60f * 60f)
                    {
                        Vector3 at = _world.ToScene(g.Plan.X, g.Flock.HomeY + 0.5f, g.Plan.Z);
                        if (!wasUp && g.Flock.Airborne && g.Flock.Kind == FlockKind.Pigeons)
                            sound.PlayOneShot(BankSound.PigeonFlock, SoundClass.Bird, at.x, at.y + 2f, at.z);
                        else if (Next() < dt / 9f) Voice(sound, g, at);
                    }
                }
                else if (g.Herd != null)
                {
                    g.Herd.Step(dt, px, pz, near);
                    // Roosters crow at dawn (04:30–06:30), and now and then through the morning.
                    if (sound != null && g.Plan.Kind == AmbientKind.ChickenYard && px * px + pz * pz < 120f * 120f)
                    {
                        float rate = hour >= 4.5f && hour < 6.5f ? 1f / 25f : hour >= 6.5f && hour < 11f ? 1f / 240f : 0f;
                        if (Next() < dt * rate)
                        {
                            Vector3 at = _world.ToScene(g.Plan.X, g.Herd.HomeY + 0.4f, g.Plan.Z);
                            sound.PlayOneShot(BankSound.Rooster, SoundClass.Animal, at.x, at.y, at.z);
                        }
                    }
                }
            }
        }

        private void Voice(ISoundService sound, Group g, Vector3 at)
        {
            switch (g.Flock.Kind)
            {
                case FlockKind.Pigeons: sound.PlayOneShot(BankSound.PigeonCoo, SoundClass.Bird, at.x, at.y, at.z, -4f); break;
                case FlockKind.Crows: sound.PlayOneShot(BankSound.Crow, SoundClass.Crow, at.x, at.y + 3f, at.z); break;
                case FlockKind.Sparrows: sound.PlayOneShot(BankSound.Sparrow, SoundClass.Bird, at.x, at.y + 2f, at.z, -3f); break;
                case FlockKind.Mynas: sound.PlayOneShot(BankSound.Myna, SoundClass.Bird, at.x, at.y + 1f, at.z); break;
                case FlockKind.Kites: sound.PlayOneShot(BankSound.Kite, SoundClass.Kite, at.x, at.y + 80f, at.z); break;
            }
        }

        /// <summary>True when a traffic vehicle moves within <paramref name="r"/> of a point (it startles the flock).</summary>
        private bool VehicleNear(double x, double z, float r)
        {
            var life = _world.Life;
            if (life == null) return false;
            int n = life.VehicleCount;
            AgentPose[] v = life.Vehicles;
            float r2 = r * r;
            for (int i = 0; i < n && i < v.Length; i++)
            {
                if (v[i].SpeedMps < 2f) continue;
                double dx = v[i].X - x, dz = v[i].Z - z;
                if (dx * dx + dz * dz < r2) return true;
            }
            return false;
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsOpen || _library == null) return;
            Camera cam = _world.ViewCamera;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;
            _library.Begin(camPos);
            int n = 0;
            for (int k = 0; k < _active.Count; k++)
            {
                FlockSim f = _active[k].Flock;
                if (f == null) continue;
                Vector3 home = _world.ToScene(_active[k].Plan.X, f.HomeY, _active[k].Plan.Z);
                for (int i = 0; i < f.Count; i++)
                {
                    if (n == _dist.Length) Grow();
                    float dx = home.x + f.X[i] - camPos.x, dy = home.y + f.Y[i] - camPos.y, dz = home.z + f.Z[i] - camPos.z;
                    _dist[n] = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                    _birdGroup[n] = _active[k];
                    _birdIndex[n] = i;
                    n++;
                }
            }
            BirdCaps(_tier, out _caps[0], out _caps[1]);
            Ghumante.World.Life.LifeLod.Assign(_dist, n, _caps, BirdRadii, _level, _order, _keys);
            int drawn = 0;
            for (int b = 0; b < n; b++)
            {
                int lvl = _level[b];
                if (lvl < 0) continue;
                Group g = _birdGroup[b];
                FlockSim f = g.Flock;
                int i = _birdIndex[b];
                FaunaSpecies sp = f.P.Species;
                FaunaPalette.CoatFor(sp, (int)(f.Seed >> 4) + i * 7, out CoatPattern pat, out uint rgb);
                Vector3 pos = _world.ToScene(g.Plan.X + f.X[i], f.HomeY + f.Y[i], g.Plan.Z + f.Z[i]);
                FaunaClip clip = f.Clip[i];
                bool air = FaunaAnimator.IsFlight(clip);
                Quaternion rot = Quaternion.Euler(air ? -f.Pitch[i] * Mathf.Rad2Deg : 0f, f.Heading[i] * Mathf.Rad2Deg, air ? -f.Bank[i] * Mathf.Rad2Deg : 0f);
                Matrix4x4 m = Matrix4x4.TRS(pos, rot, Vector3.one);
                InstanceBatch batch;
                if (lvl >= 2)
                {
                    if (!air) continue; // a bird on the ground beyond 25 m is a dot: skip it
                    float cycle = FaunaAnimator.CycleSeconds(sp, FaunaClip.Flap, 0f);
                    float phase = clip == FaunaClip.Flap || clip == FaunaClip.TakeOff ? f.ClipTime[i] / cycle : 0.3f;
                    batch = _library.Paper(sp, phase);
                }
                else
                {
                    batch = _library.Frame(sp, pat, lvl, clip, f.ClipTime[i], 0f);
                }
                if (batch == null) continue;
                batch.Add(m, Tint.Hex(rgb));
                drawn++;
            }
            _library.End();
            _world.ReportLife(0, 0, 0, 0, _library.Tris, drawn, 0, 0, _library.Draws);
        }

        private void Grow()
        {
            int cap = _dist.Length * 2;
            Array.Resize(ref _dist, cap);
            Array.Resize(ref _keys, cap);
            Array.Resize(ref _level, cap);
            Array.Resize(ref _order, cap);
            Array.Resize(ref _birdGroup, cap);
            Array.Resize(ref _birdIndex, cap);
        }

        private float Next()
        {
            // xorshift32: sound timing is presentation only (not part of the deterministic sims).
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng >> 8) * (1f / 16777216f);
        }

        private void OnDestroy()
        {
            if (_library != null) _library.Dispose();
            _library = null;
        }
    }
}
