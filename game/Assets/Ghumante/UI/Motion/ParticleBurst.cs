using System;
using Ghumante.Core.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Motion
{
    /// <summary>How emitted particles move.</summary>
    public enum ParticleStyle
    {
        /// <summary>Thrown up and out, then fall with gravity, spinning (coins, stars, sparkles).</summary>
        Burst = 0,

        /// <summary>Drift slowly up and back, growing and fading (exhaust puffs, dust).</summary>
        Puff = 1,
    }

    /// <summary>
    /// A fixed pool of particle elements in an overlay layer (no allocation after construction). Particles
    /// move with translate/rotate/scale/opacity only and are hidden with <c>visibility</c> when idle. Emission
    /// is skipped with Reduce motion, and the pool is smaller on the Low tier. Bursts never rise above the
    /// layer's top edge: the layer sits inside the safe area, so coins thrown up from the counters stay out
    /// of the notch and status bar (near the top they fly out sideways instead).
    /// </summary>
    public sealed class ParticleBurst
    {
        public const string ParticleClass = "gh-particle";
        private const float Gravity = 1700f;

        /// <summary>Half a particle (USS .gh-particle is 48 units, centred on its position).</summary>
        private const float HalfSize = 24f;

        private struct Particle
        {
            public bool Active;
            public ParticleStyle Style;
            public float X, Y, Vx, Vy, Rotation, Spin, Age, Life, Size;
        }

        private readonly UiAnimator _animator;
        private readonly VisualElement _layer;
        private readonly VisualElement[] _elements;
        private readonly string[] _spriteClass;
        private readonly Particle[] _particles;
        private MotionRandom _random;
        private int _active;
        private int _next;

        public ParticleBurst(UiAnimator animator, VisualElement layer, int capacity, uint seed)
        {
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _random = new MotionRandom(seed);
            _elements = new VisualElement[capacity];
            _spriteClass = new string[capacity];
            _particles = new Particle[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var e = new VisualElement { pickingMode = PickingMode.Ignore };
                e.AddToClassList(ParticleClass);
                e.usageHints = UsageHints.DynamicTransform;
                e.style.visibility = Visibility.Hidden;
                _layer.Add(e);
                _elements[i] = e;
            }
            animator.OnUpdate(Update);
        }

        public int Capacity
        {
            get { return _elements.Length; }
        }

        /// <summary>Particles currently flying.</summary>
        public int ActiveCount
        {
            get { return _active; }
        }

        /// <summary>
        /// Emits up to <paramref name="count"/> particles from a point in panel (world) coordinates, e.g. the
        /// centre of an icon's <c>worldBound</c>. Reuses the oldest particles when the pool is full.
        /// </summary>
        /// <param name="worldPoint">Origin in panel coordinates.</param>
        /// <param name="spriteClass">USS class with the sprite (gh-particle--coin, --star, --sparkle, ...).</param>
        /// <param name="count">How many.</param>
        /// <param name="style">How they move.</param>
        public void Emit(Vector2 worldPoint, string spriteClass, int count, ParticleStyle style = ParticleStyle.Burst)
        {
            if (_animator.Reduced || count <= 0) return;
            if (float.IsNaN(worldPoint.x) || float.IsNaN(worldPoint.y)) return;  // not laid out yet
            Vector2 local = _layer.WorldToLocal(worldPoint);
            if (float.IsNaN(local.x) || float.IsNaN(local.y)) return;
            for (int n = 0; n < count; n++)
            {
                int i = _next;
                _next = (_next + 1) % _particles.Length;
                if (!_particles[i].Active) _active++;
                Particle p = default(Particle);
                p.Active = true;
                p.Style = style;
                p.X = local.x;
                p.Y = local.y;
                if (style == ParticleStyle.Burst)
                {
                    float angle = _random.Range(-160f, -20f) * Mathf.Deg2Rad;
                    float speed = _random.Range(380f, 760f);
                    p.Vx = Mathf.Cos(angle) * speed;
                    p.Vy = Mathf.Sin(angle) * speed;
                    // Rise at most to the layer's top edge (apex height = vy² / 2g).
                    float maxRise = Mathf.Sqrt(2f * Gravity * Mathf.Max(0f, local.y - HalfSize));
                    if (-p.Vy > maxRise) p.Vy = -maxRise;
                    p.Spin = _random.Range(-420f, 420f);
                    p.Life = _random.Range(0.6f, 0.95f);
                    p.Size = _random.Range(0.55f, 1f);
                }
                else
                {
                    p.Vx = _random.Range(-90f, -40f);
                    p.Vy = _random.Range(-70f, -30f);
                    p.Spin = _random.Range(-40f, 40f);
                    p.Life = _random.Range(0.7f, 1.1f);
                    p.Size = _random.Range(0.6f, 0.9f);
                }
                p.Rotation = _random.Range(0f, 360f);
                _particles[i] = p;

                VisualElement e = _elements[i];
                if (_spriteClass[i] != spriteClass)
                {
                    if (_spriteClass[i] != null) e.RemoveFromClassList(_spriteClass[i]);
                    if (spriteClass != null) e.AddToClassList(spriteClass);
                    _spriteClass[i] = spriteClass;
                }
                Apply(i);
                e.style.visibility = Visibility.Visible;
            }
        }

        /// <summary>Hides every particle at once.</summary>
        public void Clear()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].Active) continue;
                _particles[i].Active = false;
                _elements[i].style.visibility = Visibility.Hidden;
            }
            _active = 0;
        }

        private void Update(float time, float dt)
        {
            if (_active == 0) return;
            if (_animator.Reduced)
            {
                Clear();
                return;
            }
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].Active) continue;
                Particle p = _particles[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    _particles[i].Active = false;
                    _active--;
                    _elements[i].style.visibility = Visibility.Hidden;
                    continue;
                }
                if (p.Style == ParticleStyle.Burst)
                {
                    p.Vy += Gravity * dt;
                    p.Vx *= 1f - 1.2f * dt;
                }
                else
                {
                    p.Vx *= 1f - 0.8f * dt;
                    p.Vy *= 1f - 0.8f * dt;
                }
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                p.Rotation += p.Spin * dt;
                _particles[i] = p;
                Apply(i);
            }
        }

        private void Apply(int i)
        {
            Particle p = _particles[i];
            float t = p.Life > 0f ? p.Age / p.Life : 1f;
            float scale;
            float opacity;
            if (p.Style == ParticleStyle.Burst)
            {
                // Pop in over the first 12 %, shrink and fade over the last 40 %.
                float grow = Easing.OutBack(t / 0.12f);
                float shrink = t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f;
                scale = p.Size * grow * (0.4f + 0.6f * shrink);
                opacity = shrink;
            }
            else
            {
                scale = p.Size * (0.6f + 0.9f * Easing.OutCubic(t));
                opacity = 0.9f * (1f - t);
            }
            IStyle s = _elements[i].style;
            s.translate = new Translate(new Length(p.X, LengthUnit.Pixel), new Length(p.Y, LengthUnit.Pixel));
            s.rotate = new Rotate(new Angle(p.Rotation, AngleUnit.Degree));
            s.scale = new Scale(new Vector3(scale, scale, 1f));
            s.opacity = opacity;
        }
    }
}
