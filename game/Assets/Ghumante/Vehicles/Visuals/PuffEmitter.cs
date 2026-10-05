using Ghumante.Core.Motion;
using UnityEngine;

namespace Ghumante.Vehicles.Visuals
{
    /// <summary>What a puff is made of (its colour).</summary>
    public enum PuffKind : byte
    {
        Dust = 0,
        Mud = 1,
        Smoke = 2,
    }

    /// <summary>
    /// Cartoon dust and mud puffs (ARCHITECTURE.md 7.4: particles by surface): a fixed pool of small opaque toon balls
    /// that pop up, drift and shrink away. Opaque on purpose: they use the same <c>Ghumante/ToonLit</c> material as
    /// the scooter (no transparency, no overdraw), and the shrinking reads as a puff in the cartoon style. No
    /// allocation after <see cref="Create"/>; deterministic (seeded). Positions are scene space: call
    /// <see cref="ShiftOrigin"/> when the floating origin moves.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class PuffEmitter : MonoBehaviour
    {
        private struct Puff
        {
            public bool Active;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Size;
        }

        private Transform[] _transforms;
        private MeshFilter[] _filters;
        private Puff[] _puffs;
        private Mesh[] _meshes;
        private MotionRandom _random = new MotionRandom(7349u);
        private int _next;
        private int _active;

        /// <summary>A pool of <paramref name="capacity"/> puffs under <paramref name="parent"/> (keep it at the identity).</summary>
        public static PuffEmitter Create(Transform parent, Material material, int capacity)
        {
            ToonMeshBuilder.RequireMaterial(material);
            var go = new GameObject("Puffs");
            go.transform.SetParent(parent, false);
            PuffEmitter emitter = go.AddComponent<PuffEmitter>();
            emitter.Build(material, Mathf.Max(1, capacity));
            return emitter;
        }

        public int Capacity
        {
            get { return _puffs != null ? _puffs.Length : 0; }
        }

        public int ActiveCount
        {
            get { return _active; }
        }

        /// <summary>One puff at a scene position, moving with <paramref name="velocity"/> (m/s) and about
        /// <paramref name="size"/> metres across at its biggest.</summary>
        public void Emit(Vector3 position, Vector3 velocity, float size, PuffKind kind)
        {
            if (_puffs == null || !(size > 0f)) return;
            int i = _next;
            _next = (_next + 1) % _puffs.Length;
            if (!_puffs[i].Active) _active++;
            _puffs[i] = new Puff
            {
                Active = true,
                Position = position,
                Velocity = velocity,
                Age = 0f,
                Life = _random.Range(0.55f, 0.95f),
                Size = size * _random.Range(0.75f, 1.15f),
            };
            _filters[i].sharedMesh = _meshes[(int)kind];
            _transforms[i].localPosition = position;
            _transforms[i].localScale = Vector3.zero;
            _transforms[i].gameObject.SetActive(true);
        }

        /// <summary><paramref name="count"/> puffs thrown out in a ring around a point (landings, recoveries).</summary>
        public void Burst(Vector3 position, int count, float size, PuffKind kind)
        {
            for (int n = 0; n < count; n++)
            {
                float a = _random.Range(0f, 2f * Mathf.PI);
                float speed = _random.Range(1.2f, 2.6f);
                var v = new Vector3(Mathf.Cos(a) * speed, _random.Range(0.4f, 1.2f), Mathf.Sin(a) * speed);
                Emit(position + new Vector3(v.x, 0f, v.z) * 0.08f, v, size, kind);
            }
        }

        /// <summary>Advances every live puff by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (_active == 0 || !(dt > 0f)) return;
            float drag = Mathf.Exp(-2.5f * dt);
            for (int i = 0; i < _puffs.Length; i++)
            {
                if (!_puffs[i].Active) continue;
                Puff p = _puffs[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    _puffs[i].Active = false;
                    _active--;
                    _transforms[i].gameObject.SetActive(false);
                    continue;
                }
                p.Velocity = p.Velocity * drag + new Vector3(0f, 0.9f * dt, 0f);
                p.Position += p.Velocity * dt;
                _puffs[i] = p;
                float t = p.Age / p.Life;
                // Pop up over the first quarter (OutBack), then shrink away.
                float scale = t < 0.25f ? Easing.OutBack(t / 0.25f) : 1f - Easing.InCubic((t - 0.25f) / 0.75f);
                float s = Mathf.Max(0f, scale) * p.Size;
                _transforms[i].localPosition = p.Position;
                _transforms[i].localScale = new Vector3(s, s * 0.85f, s);
            }
        }

        /// <summary>The floating origin moved by <paramref name="delta"/> (scene positions shift by −delta).</summary>
        public void ShiftOrigin(Vector3 delta)
        {
            if (_puffs == null) return;
            for (int i = 0; i < _puffs.Length; i++)
            {
                if (!_puffs[i].Active) continue;
                _puffs[i].Position -= delta;
                _transforms[i].localPosition = _puffs[i].Position;
            }
        }

        /// <summary>Hides every puff at once.</summary>
        public void Clear()
        {
            if (_puffs == null) return;
            for (int i = 0; i < _puffs.Length; i++)
            {
                if (!_puffs[i].Active) continue;
                _puffs[i].Active = false;
                _transforms[i].gameObject.SetActive(false);
            }
            _active = 0;
        }

        private void Build(Material material, int capacity)
        {
            _meshes = new Mesh[3];
            var b = new ToonMeshBuilder();
            Color32[] colors = { ToonPalette.Dust, ToonPalette.Mud, ToonPalette.Smoke };
            string[] names = { "Puff Dust", "Puff Mud", "Puff Smoke" };
            for (int k = 0; k < 3; k++)
            {
                b.Clear();
                b.Sphere(Vector3.zero, 0.5f, colors[k], 5, 9);
                _meshes[k] = b.ToMesh(names[k]);
            }
            _transforms = new Transform[capacity];
            _filters = new MeshFilter[capacity];
            _puffs = new Puff[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var go = new GameObject("Puff");
                go.transform.SetParent(transform, false);
                _filters[i] = go.AddComponent<MeshFilter>();
                _filters[i].sharedMesh = _meshes[0];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _transforms[i] = go.transform;
            }
        }

        private void OnDestroy()
        {
            if (_meshes == null) return;
            for (int i = 0; i < _meshes.Length; i++) ToonMeshBuilder.Destroy(_meshes[i]);
            _meshes = null;
        }
    }
}
