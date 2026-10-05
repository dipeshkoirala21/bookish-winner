using System;
using Ghumante.Core.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Motion
{
    /// <summary>
    /// The main menu's living background (Screens/MainMenu.uxml, "backdrop"): parallax layers that shift with
    /// device tilt or the mouse, clouds drifting across, a string of prayer flags fluttering in the wind, the
    /// sun turning slowly and, now and then, a few birds gliding past. Everything is idle motion: it stops with
    /// Reduce motion (the scene rests in place) and freezes while the app is unfocused. On the Low tier there
    /// are fewer clouds and flags, no birds, and the flags update at half rate.
    /// <para>The backdrop also bleeds under the safe-area padding of the document root, so the sky reaches
    /// the notch and the meadow reaches the home indicator. The bleed follows rotation: it reads the padding
    /// <see cref="SafeArea"/> writes (not the resolved padding, which lags a layout pass behind) and is
    /// recomputed when the root resizes or <see cref="Screen.safeArea"/> changes (a 180-degree turn).</para>
    /// </summary>
    public sealed class LivingBackdrop : IDisposable
    {
        /// <summary>Flags on the string (full / Low tier).</summary>
        public const int FlagCount = 17;
        public const int LowPowerFlagCount = 11;

        /// <summary>Lungta order: blue (sky), white (air), red (fire), green (water), yellow (earth).</summary>
        public static readonly string[] FlagColourClasses =
            { "gh-flag--blue", "gh-flag--white", "gh-flag--red", "gh-flag--green", "gh-flag--yellow" };

        /// <summary>Maximum parallax shift of the nearest layer, in pixels (layers overhang by 48 in USS).</summary>
        private const float ParallaxX = 34f;
        private const float ParallaxY = 14f;

        private struct Layer
        {
            public MotionNode Node;
            public float Depth;
        }

        private struct Cloud
        {
            public MotionNode Node;
            public float Speed;
            public float Depth;
        }

        private readonly UiAnimator _animator;
        private readonly VisualElement _backdrop;
        private readonly VisualElement _bleedRoot;
        private readonly TiltInput _tilt;
        private readonly Layer[] _layers;
        private readonly Cloud[] _clouds;
        private readonly MotionNode[] _flags;
        private readonly MotionNode _sun;
        private readonly MotionNode _flock;
        private readonly MotionNode[] _birds;
        private readonly bool _lowPower;
        private MotionRandom _random = new MotionRandom(1951);
        private float _nextFlight;
        private float _flightStart = -1f;
        private bool _flightLeftToRight = true;
        private float _flightY;
        private int _frame;
        private Rect _bledFor;

        /// <param name="animator">The screen's animator.</param>
        /// <param name="backdrop">The <c>backdrop</c> element.</param>
        /// <param name="pointerArea">Element whose pointer moves steer the parallax (the screen root).</param>
        /// <param name="bleedRoot">The document root whose padding is the safe area (may be null).</param>
        public LivingBackdrop(UiAnimator animator, VisualElement backdrop, VisualElement pointerArea, VisualElement bleedRoot)
        {
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _backdrop = backdrop ?? throw new ArgumentNullException(nameof(backdrop));
            _bleedRoot = bleedRoot;
            _lowPower = animator.Settings.LowPower;
            _tilt = new TiltInput(pointerArea ?? backdrop);

            _layers = new[]
            {
                MakeLayer("bg-mountains", 0.35f),
                MakeLayer("bg-hills", 0.62f),
                MakeLayer("bg-flags", 0.82f),
                MakeLayer("bg-front", 1f),
            };

            VisualElement sun = Optional("bg-sun");
            _sun = sun != null ? animator.Node(sun, true) : null;

            string[] cloudNames = { "bg-cloud-1", "bg-cloud-2", "bg-cloud-3", "bg-cloud-4" };
            float[] speeds = { 9f, 14f, 6f, 11f };
            int cloudCount = _lowPower ? 2 : cloudNames.Length;
            _clouds = new Cloud[cloudCount];
            for (int i = 0; i < cloudNames.Length; i++)
            {
                VisualElement e = Optional(cloudNames[i]);
                if (e == null) continue;
                if (i >= cloudCount)
                {
                    e.style.display = DisplayStyle.None;  // once, at setup
                    continue;
                }
                _clouds[i] = new Cloud { Node = animator.Node(e, true), Speed = speeds[i], Depth = 0.2f + 0.05f * i };
            }

            VisualElement flock = Optional("bg-birds");
            if (flock != null)
            {
                _flock = animator.Node(flock, true);
                _birds = new MotionNode[3];
                for (int i = 0; i < _birds.Length; i++)
                {
                    VisualElement b = Optional("bg-bird-" + (i + 1));
                    if (b != null) _birds[i] = animator.Node(b, true);
                }
                _flock.SetRest(MotionChannel.TranslateX, -4000f);  // parked off-screen between flights
                if (_lowPower) flock.style.display = DisplayStyle.None;
            }
            else
            {
                _birds = new MotionNode[0];
            }
            _nextFlight = 3f + _random.Range(0f, 3f);

            _flags = BuildFlags(Optional("bg-flags"), _lowPower ? LowPowerFlagCount : FlagCount);

            if (_bleedRoot != null)
            {
                _bleedRoot.RegisterCallback<GeometryChangedEvent>(OnRootGeometry);
                animator.OnUpdate(WatchSafeArea);
            }
            Bleed();
            animator.OnIdle(UpdateIdle, Rest);
            animator.Settings.Changed += OnMotionSettingsChanged;
        }

        /// <summary>Number of prayer flags created on the string.</summary>
        public int FlagsCreated
        {
            get { return _flags.Length; }
        }

        /// <summary>
        /// Height of the rope at <paramref name="p"/> (0 = left end, 1 = right end) as a fraction of the flags
        /// element's height. Must match flag_rope() in game/Tools/make_ui_art.py, which draws the rope.
        /// </summary>
        public static float RopeY(float p)
        {
            float u = 2f * p - 1f;
            return 0.08f + (0.88f - 0.08f) * (1f - u * u);
        }

        public void Dispose()
        {
            _animator.Settings.Changed -= OnMotionSettingsChanged;
            if (_bleedRoot != null) _bleedRoot.UnregisterCallback<GeometryChangedEvent>(OnRootGeometry);
            _tilt.Dispose();
        }

        private Layer MakeLayer(string name, float depth)
        {
            VisualElement e = Optional(name);
            return new Layer { Node = e != null ? _animator.Node(e, true) : null, Depth = depth };
        }

        private VisualElement Optional(string name)
        {
            VisualElement e = _backdrop.Q(name);
            if (e == null) Debug.LogWarning("LivingBackdrop: no element named '" + name + "' (decorative; skipped).");
            return e;
        }

        private MotionNode[] BuildFlags(VisualElement container, int count)
        {
            if (container == null) return new MotionNode[0];
            var flags = new MotionNode[count];
            for (int i = 0; i < count; i++)
            {
                float p = 0.04f + 0.92f * i / (count - 1);
                var flag = new VisualElement { name = "bg-flag-" + i, pickingMode = PickingMode.Ignore };
                flag.AddToClassList("gh-flag");
                flag.AddToClassList(FlagColourClasses[i % FlagColourClasses.Length]);
                // Placed once on the rope's curve (layout, not animation); fluttering is rotate/scale only.
                flag.style.left = Length.Percent(p * 100f);
                flag.style.top = Length.Percent(RopeY(p) * 100f);
                container.Add(flag);
                flags[i] = _animator.Node(flag, true);
            }
            return flags;
        }

        private void OnRootGeometry(GeometryChangedEvent evt)
        {
            Bleed();
        }

        /// <summary>
        /// A 180-degree turn moves the insets to the other side without resizing the root, so no geometry
        /// event reports it: compare the screen's safe area every frame instead (a struct compare).
        /// </summary>
        private void WatchSafeArea(float time, float dt)
        {
            if (_bleedRoot.panel != null && Screen.safeArea != _bledFor) Bleed();
        }

        /// <summary>Extends the backdrop over the document root's safe-area padding (layout, written only when
        /// the insets change).</summary>
        private void Bleed()
        {
            if (_bleedRoot == null) return;
            _bledFor = Screen.safeArea;
            float left, top, right, bottom;
            SafeArea.Insets(_bleedRoot, out left, out top, out right, out bottom);
            SetIfChanged(_backdrop, -left, -top, -right, -bottom);
        }

        private void UpdateIdle(float time, float dt)
        {
            _frame++;
            _tilt.Update(dt);
            Vector2 look = _tilt.Value;

            for (int i = 0; i < _layers.Length; i++)
            {
                Layer l = _layers[i];
                if (l.Node == null) continue;
                l.Node.SetOffset(-look.x * ParallaxX * l.Depth, -look.y * ParallaxY * l.Depth);
            }

            float width = Finite(_backdrop.layout.width);
            if (width < 1f) width = 1000f;
            for (int i = 0; i < _clouds.Length; i++)
            {
                Cloud c = _clouds[i];
                if (c.Node == null) continue;
                float home = Finite(c.Node.Element.layout.x);
                float cw = Mathf.Max(Finite(c.Node.Element.layout.width), 1f);
                float span = width + 2f * cw;
                // Position wraps from just off the right edge back to just off the left.
                float x = Mathf.Repeat(home + cw + c.Speed * time, span) - cw;
                float bob = 3f * Wave.Sine(time, 7f + i, i * 0.31f);
                c.Node.SetOffset(x - home - look.x * ParallaxX * c.Depth, bob - look.y * ParallaxY * c.Depth);
            }

            if (_sun != null)
            {
                _sun.SetOffsetRotate(time * 4f);
                float pulse = 1f + 0.03f * Wave.Sine(time, 4.5f);
                _sun.SetOffsetScale(pulse, pulse);
                _sun.SetOffset(-look.x * ParallaxX * 0.12f, -look.y * ParallaxY * 0.12f);
            }

            // Half-rate flutter on the Low tier: every other tick.
            if (!_lowPower || (_frame & 1) == 0)
            {
                for (int i = 0; i < _flags.Length; i++)
                {
                    // A gust travelling along the string plus a quick shiver.
                    float sway = 8f * Wave.Sine(time, 1.7f, -i * 0.085f) + 2.5f * Wave.Sine(time, 0.43f, i * 0.37f);
                    float billow = 1f - 0.09f * (0.5f + 0.5f * Wave.Sine(time, 0.9f, i * 0.21f));
                    _flags[i].SetOffsetRotate(sway);
                    _flags[i].SetOffsetScale(billow, 1f);
                }
            }

            if (!_lowPower && _flock != null) UpdateBirds(time, width);
        }

        private void UpdateBirds(float time, float width)
        {
            const float duration = 11f;
            if (_flightStart < 0f)
            {
                if (time < _nextFlight) return;
                _flightStart = time;
                _flightLeftToRight = !_flightLeftToRight;
                _flightY = _random.Range(-30f, 50f);
            }
            float t = (time - _flightStart) / duration;
            if (t >= 1f)
            {
                _flightStart = -1f;
                _nextFlight = time + _random.Range(8f, 15f);
                _flock.Set(MotionChannel.TranslateX, -4000f);
                return;
            }
            float flockWidth = 150f;
            float from = _flightLeftToRight ? -flockWidth - 40f : width + 40f;
            float to = _flightLeftToRight ? width + 40f : -flockWidth - 40f;
            _flock.Set(MotionChannel.TranslateX, Mathf.Lerp(from, to, t));
            _flock.Set(MotionChannel.TranslateY, _flightY + 12f * Wave.Sine(time, 3.2f));
            _flock.Set(MotionChannel.ScaleX, _flightLeftToRight ? 1f : -1f);
            for (int i = 0; i < _birds.Length; i++)
            {
                if (_birds[i] == null) continue;
                // Glide with an occasional burst of flaps.
                float flapping = Wave.Sine(time, 2.6f, i * 0.2f) > 0.2f ? 1f : 0.25f;
                float wing = 1f - 0.45f * flapping * (0.5f + 0.5f * Wave.Sine(time, 0.32f, i * 0.33f));
                _birds[i].SetOffsetScale(1f, wing);
            }
        }

        private void Rest()
        {
            _tilt.Reset();
            _flightStart = -1f;
        }

        /// <summary>Idle loops stopped (Reduce motion, app unfocused): nothing reads the tilt sensor until they
        /// resume, so let it sleep. The scene keeps its pose; the next idle tick wakes the sensor.</summary>
        private void OnMotionSettingsChanged()
        {
            if (!_animator.IdleRunning) _tilt.Suspend();
        }

        private static float Finite(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }

        private static void SetIfChanged(VisualElement e, float left, float top, float right, float bottom)
        {
            IStyle s = e.style;
            if (!Mathf.Approximately(s.left.value.value, left)) s.left = left;
            if (!Mathf.Approximately(s.top.value.value, top)) s.top = top;
            if (!Mathf.Approximately(s.right.value.value, right)) s.right = right;
            if (!Mathf.Approximately(s.bottom.value.value, bottom)) s.bottom = bottom;
        }
    }
}
