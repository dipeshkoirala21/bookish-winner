using System;
using System.Collections.Generic;
using Ghumante.Core.Generators.Fauna;
using Ghumante.Core.Geo;
using Ghumante.Core.Synth;
using Ghumante.Core.Traffic;
using Ghumante.World;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using UnityEngine;

namespace Ghumante.Wildlife
{
    /// <summary>
    /// Draws and voices the street and village animals (W2_DESIGN 5.5): the cows, oxen and calves and the roaming dogs
    /// of <see cref="LifeHost.Animals"/> (lying and chewing, grazing at the waste, walking; asleep in the sun, sitting,
    /// barking at night) together with the ambient herds of <see cref="WildlifeDirector"/> (macaque troops at
    /// Swayambhu and Pashupati with babies riding, goats, hens and roosters, ducks, buffalo). Every animal is the
    /// detailed generated model of its species (<see cref="FaunaMesher"/>) animated by <see cref="FaunaAnimator"/>;
    /// nearest first under the animal caps (LOD0 ≤ 10 m: 0 / 1 / 2, LOD1 ≤ 30 m: 1 / 3 / 6, keyframed LOD2 beyond:
    /// 6 / 15 / 35), the near ones CPU-skinned every frame (<see cref="FaunaSkinnedSlots"/>), the far ones from baked
    /// keyframes (<see cref="FaunaLibrary"/>); coats are per-instance tints. Cows moo now and then and dogs bark when
    /// their clip turns to a bark. Never aggressive: the sim animals are obstacles the traffic yields to. Attached to
    /// every <see cref="WorldRoot"/> by <see cref="WorldRoot.AnyReady"/>. Main thread only.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ghumante/Animal Presenter")]
    public sealed class AnimalPresenter : MonoBehaviour
    {
        /// <summary>Mean seconds between moos of one cow near the camera.</summary>
        public const float MooIntervalS = 70f;

        private struct Candidate
        {
            public FaunaSpecies Species;
            public CoatPattern Pattern;
            public uint Rgb;
            public Vector3 Pos;
            public float HeadingDeg, ClipTime, Speed;
            public FaunaClip Clip;
            public int Owner;
            public uint Seed;
        }

        private WorldRoot _world;
        private LifeLod _lod;
        private FaunaLibrary _library;
        private FaunaSkinnedSlots _slots;
        private WildlifeDirector _director;
        private readonly FaunaPose _pose = new FaunaPose();
        private readonly Dictionary<int, byte> _lastClip = new Dictionary<int, byte>();
        private Candidate[] _cand = new Candidate[128];
        private float[] _dist = new float[128], _keys = new float[128];
        private int[] _level = new int[128], _order = new int[128];
        private uint _rng = 0x9E3779B9u;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            WorldRoot.AnyReady += w => Attach(w);
        }

        public static AnimalPresenter Attach(WorldRoot world)
        {
            if (world == null) return null;
            AnimalPresenter p = world.GetComponent<AnimalPresenter>();
            if (p == null) p = world.gameObject.AddComponent<AnimalPresenter>();
            p.Bind(world);
            return p;
        }

        private void Bind(WorldRoot world)
        {
            _world = world;
            _lod = LifeLod.ForTier((int)world.Tier);
            Release();
            if (world.Materials == null) return;
            _library = new FaunaLibrary(world.Materials.instancedTint);
            _slots = new FaunaSkinnedSlots(world.Materials.instancedTint, Math.Max(1, _lod.AnimalCaps[0] + _lod.AnimalCaps[1]));
            _lastClip.Clear();
        }

        /// <summary>The species a sim animal is drawn as: of the street cattle about 5% are calves and a quarter are
        /// oxen (street_life §7.1), the rest cows.</summary>
        public static FaunaSpecies SpeciesOf(AnimalKind kind, int agentId)
        {
            uint h = FaunaRng.Hash((uint)agentId, 0xCA77u);
            switch (kind)
            {
                case AnimalKind.Cow:
                {
                    float u = FaunaRng.Unit(h);
                    return u < 0.05f ? FaunaSpecies.Calf : u < 0.3f ? FaunaSpecies.Bull : FaunaSpecies.Cow;
                }
                case AnimalKind.Dog: return FaunaSpecies.Dog;
                case AnimalKind.Goat: return FaunaSpecies.Goat;
                case AnimalKind.Chicken: return (h & 7u) == 0 ? FaunaSpecies.Rooster : FaunaSpecies.Hen;
                case AnimalKind.Buffalo: return FaunaSpecies.Buffalo;
                case AnimalKind.Duck: return FaunaSpecies.Duck;
                default: return FaunaSpecies.Macaque;
            }
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsOpen || _library == null) return;
            Camera cam = _world.ViewCamera;
            if (cam == null) return;
            if (_director == null) _director = GetComponent<WildlifeDirector>();
            Vector3 camPos = cam.transform.position;
            WorldPos origin = _world.Origin;
            ISoundService sound = _world.Sound;
            float now = Time.time, dt = Time.deltaTime;
            int n = 0;

            // Street animals of the traffic sim.
            LifeHost life = _world.Life;
            if (life != null)
            {
                int count = life.AnimalCount;
                AnimalPose[] poses = life.Animals;
                float age = life.SnapshotAgeS;
                for (int i = 0; i < count && i < poses.Length; i++)
                {
                    AnimalPose a = poses[i];
                    Vector3 at = At(a, origin, age);
                    float d = (at - camPos).magnitude;
                    Sounds(a, at, d, sound, dt);
                    FaunaSpecies sp = SpeciesOf(a.Kind, a.AgentId);
                    uint h = FaunaRng.Hash((uint)a.AgentId, 0x7117u);
                    FaunaPalette.CoatFor(sp, a.Tint + 5 * (int)(h % 4u), out CoatPattern pat, out uint rgb);
                    Push(ref n, new Candidate
                    {
                        Species = sp, Pattern = pat, Rgb = rgb, Pos = at, HeadingDeg = a.HeadingRad * Mathf.Rad2Deg, Clip = (FaunaClip)a.ClipId,
                        ClipTime = now + (h & 1023u) * 0.37f, Speed = a.SpeedMps, Owner = a.AgentId, Seed = h,
                    }, d);
                }
            }

            // Ambient herds.
            if (_director != null)
            {
                for (int k = 0; k < _director.Herds.Count; k++)
                {
                    WildlifeDirector.Group g = _director.Herds[k];
                    HerdSim herd = g.Herd;
                    for (int i = 0; i < herd.Count; i++)
                    {
                        double x = g.Plan.X + herd.X[i], z = g.Plan.Z + herd.Z[i];
                        float y = _director.GroundY(x, z);
                        if (float.IsNaN(y)) y = herd.HomeY;
                        FaunaSpecies sp = herd.SpeciesOf(i);
                        float heading = herd.Heading[i] * Mathf.Rad2Deg;
                        if (herd.IsRider(i))
                        {
                            // A baby rides on its mother's back while she moves, else sits close beside her.
                            int m = herd.Carrier[i];
                            bool moving = herd.Speed[m] > 0.1f;
                            float hr = herd.Heading[m];
                            float side = moving ? 0f : 0.32f;
                            x = g.Plan.X + herd.X[m] + Math.Cos(hr) * side;
                            z = g.Plan.Z + herd.Z[m] - Math.Sin(hr) * side;
                            if (moving) y += 0.36f;
                        }
                        Vector3 at = _world.ToScene(x, y, z);
                        float d = (at - camPos).magnitude;
                        FaunaPalette.CoatFor(sp, herd.Tint[i], out CoatPattern pat, out uint rgb);
                        Push(ref n, new Candidate
                        {
                            Species = sp, Pattern = pat, Rgb = rgb, Pos = at, HeadingDeg = heading, Clip = herd.Clip[i], ClipTime = herd.ClipTime[i],
                            Speed = herd.Speed[i], Owner = (int)(g.Plan.Key * 31 + i), Seed = (uint)herd.Tint[i],
                        }, d);
                    }
                }
            }

            LifeLod.Assign(_dist, n, _lod.AnimalCaps, _lod.AnimalRadii, _level, _order, _keys);
            _library.Begin(camPos);
            _slots.Begin(camPos);
            int drawn = 0;
            for (int i = 0; i < n; i++)
            {
                int level = _level[i];
                if (level < 0) continue;
                Candidate c = _cand[i];
                Matrix4x4 m = Matrix4x4.TRS(c.Pos, Quaternion.Euler(0f, c.HeadingDeg, 0f), Vector3.one);
                Vector4 tint = Tint.Hex(c.Rgb);
                bool done = false;
                if (level <= 1)
                {
                    FaunaMesh mesh = _library.Mesh(c.Species, level, c.Pattern);
                    FaunaAnimator.Evaluate(mesh, c.Clip, c.ClipTime, c.Speed, c.Seed, _pose);
                    done = _slots.Draw(c.Owner, mesh, _pose, m, tint, level == 0);
                }
                if (!done)
                {
                    InstanceBatch b = _library.Frame(c.Species, c.Pattern, 2, c.Clip, c.ClipTime, c.Speed);
                    if (b == null) continue;
                    b.Add(m, tint);
                }
                drawn++;
            }
            _library.End();
            _world.ReportLife(0, 0, 0, 0, _library.Tris + _slots.Tris, drawn, 0, 0, _library.Draws + _slots.Draws);
            if (_lastClip.Count > 4 * Math.Max(16, n)) _lastClip.Clear(); // bound the clip memory
        }

        private void Push(ref int n, in Candidate c, float distance)
        {
            if (n == _cand.Length)
            {
                int cap = n * 2;
                Array.Resize(ref _cand, cap);
                Array.Resize(ref _dist, cap);
                Array.Resize(ref _keys, cap);
                Array.Resize(ref _level, cap);
                Array.Resize(ref _order, cap);
            }
            _cand[n] = c;
            _dist[n] = distance;
            n++;
        }

        private void Sounds(in AnimalPose a, Vector3 at, float distance, ISoundService sound, float dt)
        {
            byte last;
            bool had = _lastClip.TryGetValue(a.AgentId, out last);
            _lastClip[a.AgentId] = a.ClipId;
            if (sound == null || distance >= 60f) return;
            if (a.Kind == AnimalKind.Dog && a.ClipId == (byte)AnimalClip.Bark && (!had || last != a.ClipId))
                sound.PlayOneShot(BankSound.DogBark, SoundClass.DogBark, at.x, at.y + 0.5f, at.z);
            else if (a.Kind == AnimalKind.Cow && distance < 40f && Next() < dt / MooIntervalS)
                sound.PlayOneShot(BankSound.CowMoo, SoundClass.Animal, at.x, at.y + 1.2f, at.z);
        }

        /// <summary>Scene position of a pose, moved <paramref name="ageS"/> along its heading (LifeHost.SnapshotAgeS: a
        /// held snapshot keeps moving).</summary>
        private static Vector3 At(in AnimalPose a, WorldPos origin, float ageS)
        {
            float ahead = a.SpeedMps * ageS;
            return new Vector3((float)(a.X - origin.X) + Mathf.Sin(a.HeadingRad) * ahead, a.Y - origin.Y,
                               (float)(a.Z - origin.Z) + Mathf.Cos(a.HeadingRad) * ahead);
        }

        private float Next()
        {
            // xorshift32: the moo timing is presentation only (not part of the deterministic sims).
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng >> 8) * (1f / 16777216f);
        }

        private void Release()
        {
            if (_library != null) _library.Dispose();
            if (_slots != null) _slots.Dispose();
            _library = null;
            _slots = null;
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
