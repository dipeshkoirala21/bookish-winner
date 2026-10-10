using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Fauna;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Synth;
using Ghumante.Core.Traffic;
using Ghumante.World;
using Ghumante.World.Instancing;
using Ghumante.World.Streaming;
using UnityEngine;

namespace Ghumante.Wildlife
{
    /// <summary>
    /// Keeps the ambient wildlife alive round the player (W2_DESIGN 5.5–5.6): every two seconds the
    /// <see cref="AmbientFaunaPlanner"/> lists the groups within reach (the macaque troops of Swayambhu and Pashupati,
    /// the pigeon flocks of the Durbar squares and Boudha, crows, sparrows and mynas in the city, kites overhead,
    /// goats, hens, buffalo, ducks, egrets and swallows in the fringe and the fields), placed on ground that suits them
    /// (<see cref="FaunaGroundIndex"/> over the detail tiles: never in a house or a monument, never on a carriageway,
    /// ducks on ponds). <see cref="FaunaLod.SelectGroups"/> keeps the flock caps within 90 m (1 / 1 / 2 pigeon flocks and
    /// 1 / 2 / 3 small flocks on Low / Mid / High; the curated square flocks first, running flocks kept against slightly
    /// nearer ones); groups that come into reach are created with their own deterministic <see cref="FlockSim"/> or
    /// <see cref="HerdSim"/> (macaque troops get the walls, plinths and chaitya terraces round them as perches), groups
    /// that drop out are released. Each frame the sims step (the player and passing vehicles startle the flocks and
    /// make herds step aside) and the birds are drawn by <see cref="FaunaLod.AssignBirds"/>: full birds within 8 m
    /// (0 / 4 / 8), light birds within 25 m (15 / 30 / 75), and every other bird as a 4-triangle paper bird, flying or
    /// sitting on the ground (to 70 m), so a square's whole flock is always there; the triangles stay within the
    /// animals-and-birds slice after <see cref="AnimalPresenter"/>'s share. The ground herds are handed to
    /// <see cref="AnimalPresenter"/>, which draws them with the street cows and dogs. Wing bursts, coos, caws, kite
    /// whistles and dawn roosters go through the world's sound service. Main thread.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    [AddComponentMenu("Ghumante/Wildlife Director")]
    public sealed class WildlifeDirector : MonoBehaviour
    {
        /// <summary>Groups exist within this distance of the player (m).</summary>
        public const float PlanRadiusM = 260f;

        /// <summary>Seconds between plans.</summary>
        public const float PlanIntervalS = 2f;

        /// <summary>
        /// Optional: the roads package's corridor query for a tile (RoadCorridorIndex.ForTile), so the fauna use exactly
        /// the widened corridor the road mesher draws; without it the ground index estimates corridors from the road
        /// widths. Set once by the integration before the world opens.
        /// </summary>
        public static Func<TileData, IRoadCorridorQuery> CorridorFactory;

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
            public FaunaGroundIndex Index;

            public AreaType AreaAt(double x, double z)
            {
                return Grid != null ? Grid.At(x, z) : AreaType.Unknown;
            }

            public FaunaGround GroundAt(double x, double z)
            {
                return Index != null ? Index.At(x, z) : FaunaGround.None;
            }
        }

        private WorldRoot _world;
        private AmbientFaunaPlanner _planner;
        private readonly Habitat _habitat = new Habitat();
        private readonly FaunaGroundIndex _ground = new FaunaGroundIndex();
        private readonly Dictionary<ulong, ulong> _shownSource = new Dictionary<ulong, ulong>();
        private readonly Dictionary<ulong, int> _sourceRefs = new Dictionary<ulong, int>();
        private readonly AmbientGroup[] _planned = new AmbientGroup[96];
        private readonly bool[] _running = new bool[96], _keep = new bool[96];
        private readonly float[] _score = new float[96];
        private readonly int[] _scoreOrder = new int[96];
        private readonly Dictionary<long, Group> _groups = new Dictionary<long, Group>();
        private readonly List<Group> _active = new List<Group>();
        private readonly List<long> _drop = new List<long>();
        private FaunaLibrary _library;
        private AnimalPresenter _animals;
        private float _planTimer;
        private int _tier;
        private uint _rng = 0xC0FFEEu;

        // Perch search scratch.
        private readonly float[] _perchX = new float[HerdSim.MaxPerches], _perchZ = new float[HerdSim.MaxPerches];
        private readonly float[] _perchBase = new float[HerdSim.MaxPerches], _perchTop = new float[HerdSim.MaxPerches];
        private readonly float[] _perchAx = new float[HerdSim.MaxPerches], _perchAz = new float[HerdSim.MaxPerches];

        // Bird drawing scratch.
        private float[] _dist = new float[512], _keys = new float[512];
        private int[] _level = new int[512], _order = new int[512];
        private bool[] _air = new bool[512];
        private FaunaSpecies[] _species = new FaunaSpecies[512];
        private Group[] _birdGroup = new Group[512];
        private int[] _birdIndex = new int[512];

        /// <summary>The running ground herds (macaques, goats, hens, ducks, buffalo) for <see cref="AnimalPresenter"/>.</summary>
        public readonly List<Group> Herds = new List<Group>();

        /// <summary>Birds drawn last frame per level (full, light, paper flying, paper sitting).</summary>
        public readonly int[] BirdsByLevel = new int[4];

        /// <summary>Triangles of the birds drawn last frame.</summary>
        public int BirdTris { get; private set; }

        /// <summary>The ground the fauna is placed on (detail tiles near the player).</summary>
        public FaunaGroundIndex Ground
        {
            get { return _ground; }
        }

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
            if (_world != null)
            {
                _world.DetailTileShown -= OnShown;
                _world.DetailTileHidden -= OnHidden;
            }
            _world = world;
            _tier = (int)world.Tier;
            _planner = new AmbientFaunaPlanner((uint)WorldRoot.RegionSeed(world.RegionId ?? "region"));
            _groups.Clear();
            _active.Clear();
            Herds.Clear();
            _ground.Clear();
            _ground.CorridorFactory = CorridorFactory;
            _shownSource.Clear();
            _sourceRefs.Clear();
            _habitat.Index = _ground;
            if (_library != null) _library.Dispose();
            _library = world.Materials != null ? new FaunaLibrary(world.Materials.instancedTint) : null;
            _planTimer = 0f;
            world.DetailTileShown += OnShown;
            world.DetailTileHidden += OnHidden;
            // Tiles already visible before the director attached.
            WorldStreamer s = world.Streamer;
            if (s != null)
                for (int i = 0; i < s.DetailViews.Count; i++)
                    if (s.DetailViews[i].Extras != null) OnShown(s.DetailViews[i].Node.Area, s.DetailViews[i].Extras);
        }

        private void OnShown(TileId id, TileExtras e)
        {
            if (e == null || e.Source == null || _shownSource.ContainsKey(id.Key)) return;
            ulong src = e.Source.Tile.Key;
            _shownSource.Add(id.Key, src);
            int refs;
            _sourceRefs.TryGetValue(src, out refs);
            _sourceRefs[src] = refs + 1;
            if (refs > 0) return;
            try
            {
                _ground.Add(e.Source);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("WildlifeDirector: fauna ground of " + id + " failed: " + ex.Message);
            }
        }

        private void OnHidden(TileId id)
        {
            ulong src;
            if (!_shownSource.TryGetValue(id.Key, out src)) return;
            _shownSource.Remove(id.Key);
            int refs;
            if (!_sourceRefs.TryGetValue(src, out refs)) return;
            if (refs > 1)
            {
                _sourceRefs[src] = refs - 1;
                return;
            }
            _sourceRefs.Remove(src);
            _ground.Remove(src);
        }

        /// <summary>Bird caps (full within 8 m, light within 25 m) for the device tier (<see cref="FaunaLod.BirdCaps"/>).</summary>
        public static void BirdCaps(int tier, out int full, out int light)
        {
            FaunaLod.BirdCaps(tier, out full, out light);
        }

        /// <summary>Flocks allowed within 90 m: pigeon flocks and small flocks (<see cref="FaunaLod.FlockCaps"/>).</summary>
        public static void FlockCaps(int tier, out int pigeonFlocks, out int smallFlocks)
        {
            FaunaLod.FlockCaps(tier, out pigeonFlocks, out smallFlocks);
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
            for (int i = 0; i < n; i++) _running[i] = _groups.ContainsKey(_planned[i].Key);
            FaunaLod.SelectGroups(_planned, n, _running, _tier, _keep, _score, _scoreOrder);
            foreach (Group g in _groups.Values) g.Seen = false;
            for (int i = 0; i < n; i++)
            {
                if (!_keep[i]) continue;
                AmbientGroup p = _planned[i];
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
            {
                float disc = p.IsCurated ? p.RadiusM : 0f;
                g.Flock = new FlockSim(AmbientFaunaPlanner.FlockOf(p.Kind), Math.Min(p.Count, FlockSim.MaxBirds), p.X, p.Z, y, p.Seed, disc, _habitat);
            }
            else
            {
                g.Herd = new HerdSim(AmbientFaunaPlanner.SpeciesOf(p.Kind), Math.Min(p.Count, HerdSim.MaxAnimals), p.X, p.Z, y, p.RadiusM, p.Seed, _habitat);
                if (p.Kind == AmbientKind.MacaqueTroop) FindPerches(g.Herd);
            }
            return g;
        }

        /// <summary>
        /// The walls, plinths, chaitya terraces and rails round a troop that a macaque can climb onto: walkable
        /// structure tops 0.5–6 m above the ground under them, from the layered ground query (a deterministic spiral of
        /// sample points over the troop's range). For each, the nearest edge is found by stepping out in eight
        /// directions: the macaque sits just inside it and climbs from the ground just outside it (which must be open
        /// ground, not a house or a carriageway), so it never climbs out of the inside of a plinth.
        /// </summary>
        private void FindPerches(HerdSim herd)
        {
            var q = _world.Ground as ILayeredGroundQuery;
            if (q == null) return;
            int n = 0;
            float range = herd.RadiusM * 1.25f;
            const int samples = 48;
            for (int k = 0; k < samples && n < HerdSim.MaxPerches; k++)
            {
                // Golden-angle spiral: even cover of the disc.
                float r = range * Mathf.Sqrt((k + 0.5f) / samples), a = k * 2.39996323f;
                float x = r * Mathf.Cos(a), z = r * Mathf.Sin(a);
                GroundSample s;
                if (!q.TrySample(herd.HomeX + x, herd.HomeZ + z, herd.HomeY + 6f, out s) || !s.OnStructure) continue;
                float rise = s.Height - s.TerrainHeight;
                if (rise < 0.5f || rise > 6f) continue;
                if (!FindEdge(q, herd, x, z, out float dx, out float dz, out float edge)) continue;
                float ax = x + dx * (edge + 0.12f), az = z + dz * (edge + 0.12f);
                if (herd.Habitat != null &&
                    (herd.Habitat.GroundAt(herd.HomeX + ax, herd.HomeZ + az) & (FaunaGround.Building | FaunaGround.Road)) != 0)
                    continue;
                float sx = x + dx * Mathf.Max(0f, edge - 0.25f), sz = z + dz * Mathf.Max(0f, edge - 0.25f);
                bool near = false;
                for (int j = 0; j < n && !near; j++) near = (_perchX[j] - sx) * (_perchX[j] - sx) + (_perchZ[j] - sz) * (_perchZ[j] - sz) < 4f;
                if (near) continue;
                _perchX[n] = sx;
                _perchZ[n] = sz;
                _perchAx[n] = ax;
                _perchAz[n] = az;
                _perchBase[n] = s.TerrainHeight - herd.HomeY;
                _perchTop[n] = s.Height - herd.HomeY;
                n++;
            }
            herd.SetPerches(_perchX, _perchZ, _perchAx, _perchAz, _perchBase, _perchTop, n);
        }

        /// <summary>The nearest edge of the structure top under (x, z) (relative to the herd's home): the outward
        /// direction and the distance to the first ground point within 2 m, in eight directions.</summary>
        private static bool FindEdge(ILayeredGroundQuery q, HerdSim herd, float x, float z, out float dirX, out float dirZ, out float dist)
        {
            dirX = dirZ = 0f;
            dist = float.MaxValue;
            for (int d = 0; d < 8; d++)
            {
                float a = d * (Mathf.PI * 0.25f);
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                for (float step = 0.2f; step <= 2.01f && step < dist; step += 0.2f)
                {
                    GroundSample g;
                    if (!q.TrySample(herd.HomeX + x + cx * step, herd.HomeZ + z + cz * step, herd.HomeY + 6f, out g)) break;
                    if (g.OnStructure && g.Height - g.TerrainHeight >= 0.2f) continue;
                    dist = step;
                    dirX = cx;
                    dirZ = cz;
                    break;
                }
            }
            return dist < float.MaxValue;
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
                    if (NearestVehicle(g.Plan.X, g.Plan.Z, g.Flock.DiscRadiusM + 3f, out _, out _)) g.Flock.Startle();
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
                    bool veh = NearestVehicle(g.Plan.X, g.Plan.Z, g.Plan.RadiusM * 1.3f + HerdSim.VehicleKeepM + 2f, out float vx, out float vz);
                    g.Herd.Step(dt, px, pz, near, vx, vz, veh);
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

        /// <summary>The nearest traffic vehicle moving within <paramref name="r"/> of a point (it startles a flock and
        /// makes a herd step aside), relative to the point.</summary>
        private bool NearestVehicle(double x, double z, float r, out float rx, out float rz)
        {
            rx = rz = 0f;
            var life = _world.Life;
            if (life == null) return false;
            int n = life.VehicleCount;
            AgentPose[] v = life.Vehicles;
            double best = r * (double)r;
            bool found = false;
            for (int i = 0; i < n && i < v.Length; i++)
            {
                if (v[i].SpeedMps < 2f) continue;
                double dx = v[i].X - x, dz = v[i].Z - z;
                double d2 = dx * dx + dz * dz;
                if (d2 >= best) continue;
                best = d2;
                rx = (float)dx;
                rz = (float)dz;
                found = true;
            }
            return found;
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsOpen || _library == null) return;
            Camera cam = _world.ViewCamera;
            if (cam == null) return;
            if (_animals == null) _animals = GetComponent<AnimalPresenter>();
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
                    _air[n] = FaunaAnimator.IsFlight(f.Clip[i]);
                    _species[n] = f.P.Species;
                    _birdGroup[n] = _active[k];
                    _birdIndex[n] = i;
                    n++;
                }
            }
            // The birds get what the street and village animals left of the slice (never less than their own caps).
            int animalTris = _animals != null ? _animals.AnimalTris : 0;
            int budget = Math.Max(FaunaLod.BirdCapTris(_tier), FaunaLod.SliceTris(_tier) - animalTris);
            BirdTris = FaunaLod.AssignBirds(_dist, _air, _species, n, _tier, budget, _level, _order, _keys);
            for (int l = 0; l < BirdsByLevel.Length; l++) BirdsByLevel[l] = 0;
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
                bool air = _air[b];
                Quaternion rot = Quaternion.Euler(air ? -f.Pitch[i] * Mathf.Rad2Deg : 0f, f.Heading[i] * Mathf.Rad2Deg, air ? -f.Bank[i] * Mathf.Rad2Deg : 0f);
                Matrix4x4 m = Matrix4x4.TRS(pos, rot, Vector3.one);
                InstanceBatch batch;
                switch (lvl)
                {
                    case FaunaLod.Paper:
                    {
                        float cycle = FaunaAnimator.CycleSeconds(sp, FaunaClip.Flap, 0f);
                        float phase = clip == FaunaClip.Flap || clip == FaunaClip.TakeOff ? f.ClipTime[i] / cycle : 0.3f;
                        batch = _library.Paper(sp, phase);
                        break;
                    }
                    case FaunaLod.PaperGround:
                        batch = _library.PaperSitting(sp);
                        break;
                    default:
                        batch = _library.Frame(sp, pat, lvl, clip, f.ClipTime[i], 0f);
                        break;
                }
                if (batch == null) continue;
                batch.Add(m, Tint.Hex(rgb));
                BirdsByLevel[lvl]++;
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
            Array.Resize(ref _air, cap);
            Array.Resize(ref _species, cap);
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
            if (_world != null)
            {
                _world.DetailTileShown -= OnShown;
                _world.DetailTileHidden -= OnHidden;
            }
            if (_library != null) _library.Dispose();
            _library = null;
        }
    }
}
