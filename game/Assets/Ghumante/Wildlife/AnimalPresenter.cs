using System;
using System.Collections.Generic;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Synth;
using Ghumante.Core.Traffic;
using Ghumante.World;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using Ghumante.World.Rendering;
using Ghumante.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.Wildlife
{
    /// <summary>
    /// Draws and voices the street animals of stage 1 (W2_DESIGN 5.5): the cows (humped zebu, lying and chewing 60% of
    /// the time) and the roaming dogs (asleep most of the day, awake and barking in chains at night) of
    /// <see cref="LifeHost.Animals"/>, nearest first under the animal caps (LOD0 ≤ 10 m: 0 / 1 / 2, LOD1 ≤ 30 m: 1 / 3 /
    /// 6, block-out beyond: 6 / 15 / 35), coat colours from the street palettes as instance tints, with a gentle
    /// breathing and walking bob. Cows moo now and then and dogs bark when their clip turns to a bark (one-shots through
    /// <see cref="ISoundService"/>, which caps barks at 2). Never aggressive: they are obstacles the traffic yields to.
    /// Attached to every <see cref="WorldRoot"/> by <see cref="WorldRoot.AnyReady"/>. Main thread only.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ghumante/Animal Presenter")]
    public sealed class AnimalPresenter : MonoBehaviour
    {
        /// <summary>Cow coats (W2_DESIGN 5.5).</summary>
        public static readonly uint[] CowCoats = { 0xEDE7DA, 0xD8C9A8, 0x9D9890, 0x2B2B2B, 0x8B5A3C, 0xEFEFEF };

        /// <summary>Dog coats: tan 40%, black 20%, black-and-tan 15%, cream 10%, patched 15%.</summary>
        public static readonly uint[] DogCoats = { 0xC49A6C, 0xC49A6C, 0xC49A6C, 0xC49A6C, 0x2A2A2A, 0x2A2A2A, 0x5A3B22, 0x5A3B22, 0xEFE3C8, 0xA88A6A };

        /// <summary>Mean seconds between moos of one cow near the camera.</summary>
        public const float MooIntervalS = 70f;

        private WorldRoot _world;
        private LifeLod _lod;
        private readonly InstanceBatch[,] _cow = new InstanceBatch[2, 3];
        private readonly InstanceBatch[,] _dog = new InstanceBatch[2, 3];
        private readonly Dictionary<int, byte> _lastClip = new Dictionary<int, byte>();
        private float[] _dist = new float[64], _keys = new float[64];
        private int[] _level = new int[64], _order = new int[64];
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
            DestroyMeshes();
            WorldMaterialSet set = world.Materials;
            if (set == null) return;
            var m = new MeshData(512, 1536);
            for (int pose = 0; pose < 2; pose++)
                for (int lod = 0; lod < 3; lod++)
                {
                    m.Clear();
                    int t = KitMeshes.Cow(pose == 1, lod, m);
                    _cow[pose, lod] = new InstanceBatch(MeshUpload.CreateWhole(m, "cow_" + pose + "_" + lod), set.instancedTint, t, true)
                    {
                        Shadows = lod == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    };
                    m.Clear();
                    t = KitMeshes.Dog(pose == 1, lod, m);
                    _dog[pose, lod] = new InstanceBatch(MeshUpload.CreateWhole(m, "dog_" + pose + "_" + lod), set.instancedTint, t, true)
                    {
                        Shadows = lod == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    };
                }
            _lastClip.Clear();
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsOpen || _world.Life == null || _cow[0, 0] == null) return;
            Camera cam = _world.ViewCamera;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;
            WorldPos origin = _world.Origin;
            LifeHost life = _world.Life;
            ISoundService sound = _world.Sound;
            var bounds = new Bounds(camPos, new Vector3(800f, 400f, 800f));
            foreach (InstanceBatch b in _cow)
            {
                b.ResetCounters();
                b.WorldBounds = bounds;
            }
            foreach (InstanceBatch b in _dog)
            {
                b.ResetCounters();
                b.WorldBounds = bounds;
            }
            int n = life.AnimalCount;
            AnimalPose[] poses = life.Animals;
            if (_dist.Length < n)
            {
                int cap = Math.Max(n, _dist.Length * 2);
                _dist = new float[cap];
                _keys = new float[cap];
                _level = new int[cap];
                _order = new int[cap];
            }
            float age = life.SnapshotAgeS;
            for (int i = 0; i < n; i++) _dist[i] = (At(poses[i], origin, age) - camPos).magnitude;
            LifeLod.Assign(_dist, n, _lod.AnimalCaps, _lod.AnimalRadii, _level, _order, _keys);
            float now = Time.time, dt = Time.deltaTime;
            int drawn = 0;
            for (int i = 0; i < n; i++)
            {
                AnimalPose a = poses[i];
                Vector3 at = At(a, origin, age);
                byte last;
                bool had = _lastClip.TryGetValue(a.AgentId, out last);
                _lastClip[a.AgentId] = a.ClipId;
                if (sound != null && _dist[i] < 60f)
                {
                    if (a.Kind == AnimalKind.Dog && a.ClipId == (byte)AnimalClip.Bark && (!had || last != a.ClipId))
                        sound.PlayOneShot(BankSound.DogBark, SoundClass.DogBark, at.x, at.y + 0.5f, at.z);
                    else if (a.Kind == AnimalKind.Cow && _dist[i] < 40f && Next() < dt / MooIntervalS)
                        sound.PlayOneShot(BankSound.CowMoo, SoundClass.Animal, at.x, at.y + 1.2f, at.z);
                }
                int level = _level[i];
                if (level < 0) continue;
                bool resting = a.Kind == AnimalKind.Cow ? a.ClipId == (byte)AnimalClip.Lie : a.ClipId == (byte)AnimalClip.Sleep;
                float bob = resting ? 0.015f * Mathf.Sin(now * 1.6f + a.AgentId) : a.SpeedMps > 0.05f ? 0.025f * Mathf.Abs(Mathf.Sin((a.ClipTime + age) * 6f)) : 0f;
                Matrix4x4 m = Matrix4x4.TRS(at + new Vector3(0f, bob, 0f), Quaternion.Euler(0f, a.HeadingRad * Mathf.Rad2Deg, 0f), Vector3.one);
                int pose = resting ? 1 : 0;
                if (a.Kind == AnimalKind.Cow) _cow[pose, level].Add(m, Tint.Hex(CowCoats[a.Tint % CowCoats.Length]));
                else if (a.Kind == AnimalKind.Dog) _dog[pose, level].Add(m, Tint.Hex(DogCoats[a.Tint % DogCoats.Length]));
                else continue;
                drawn++;
            }
            int tris = 0, draws = 0;
            foreach (InstanceBatch b in _cow)
            {
                b.Flush();
                tris += b.Tris;
                draws += b.Draws;
            }
            foreach (InstanceBatch b in _dog)
            {
                b.Flush();
                tris += b.Tris;
                draws += b.Draws;
            }
            _world.ReportLife(0, 0, 0, 0, tris, drawn, 0, 0, draws);
            if (_lastClip.Count > 4 * Math.Max(16, n)) _lastClip.Clear(); // bound the clip memory
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

        private void DestroyMeshes()
        {
            for (int p = 0; p < 2; p++)
                for (int l = 0; l < 3; l++)
                {
                    if (_cow[p, l] != null) _cow[p, l].DestroyMesh();
                    if (_dog[p, l] != null) _dog[p, l].DestroyMesh();
                    _cow[p, l] = null;
                    _dog[p, l] = null;
                }
        }

        private void OnDestroy()
        {
            DestroyMeshes();
        }
    }
}
