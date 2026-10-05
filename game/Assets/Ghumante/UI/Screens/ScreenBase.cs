using System;
using System.Collections.Generic;
using Ghumante.Core.Motion;
using Ghumante.Core.Services;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using UnityEngine.UIElements;

namespace Ghumante.UI.Screens
{
    /// <summary>
    /// Base for a full-screen presenter over a cloned UXML tree. A presenter owns no Unity objects; the
    /// App assembly decides which screen is visible and swaps the UIDocument's visual tree.
    /// <para>Every screen gets an <see cref="Animator"/> on its root (UI/Motion) and plays haptics through
    /// <see cref="Haptics"/> (Ghumante.Core.Services.IHaptics, injected by App). Buttons get their touch feel
    /// through <see cref="Feel"/>.</para>
    /// </summary>
    public abstract class ScreenBase : IDisposable
    {
        private readonly Dictionary<string, PressFeel> _feels = new Dictionary<string, PressFeel>(StringComparer.Ordinal);
        private bool _disposed;

        protected ScreenBase(VisualElement root, Localizer localizer)
            : this(root, localizer, null, null)
        {
        }

        /// <param name="root">The UIDocument root the screen was cloned into.</param>
        /// <param name="localizer">String tables.</param>
        /// <param name="haptics">Haptics (null: none).</param>
        /// <param name="motion">Shared motion settings (null: full motion).</param>
        protected ScreenBase(VisualElement root, Localizer localizer, IHaptics haptics, MotionSettings motion)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (localizer == null) throw new ArgumentNullException(nameof(localizer));
            Root = root;
            // Our UXML puts the Ghumante stylesheet on a .gh-root element under the document root. Classes
            // that USS selectors key on (gh-lang-*, orient-*, spike--standard) go there, inside the sheet's scope.
            StyledRoot = root.Q(className: "gh-root") ?? root;
            Localizer = localizer;
            Haptics = haptics ?? new NullHaptics();
            Motion = motion ?? new MotionSettings();
            Animator = new UiAnimator(StyledRoot, Motion);
            Localizer.Changed += OnLocaleChanged;
            // Portrait and landscape layouts are USS rules keyed on orient-* classes (ADR-017).
            OrientationWatcher.Track(StyledRoot);
        }

        /// <summary>The UIDocument root the screen was cloned into.</summary>
        public VisualElement Root { get; private set; }

        /// <summary>The screen's .gh-root element (falls back to <see cref="Root"/>).</summary>
        public VisualElement StyledRoot { get; private set; }

        /// <summary>This screen's animator (ticked by the panel; tests call <see cref="UiAnimator.Tick"/>).</summary>
        public UiAnimator Animator { get; private set; }

        /// <summary>The shared motion settings (Reduce motion, Low tier, focus).</summary>
        public MotionSettings Motion { get; private set; }

        protected Localizer Localizer { get; private set; }

        protected IHaptics Haptics { get; private set; }

        /// <summary>Re-applies all localised and computed text.</summary>
        public void Refresh()
        {
            Localizer.Apply(StyledRoot);
            OnRefresh();
        }

        /// <summary>Screen-specific text that is computed rather than keyed (counters, device info...).</summary>
        protected virtual void OnRefresh()
        {
        }

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Localizer.Changed -= OnLocaleChanged;
            foreach (PressFeel feel in _feels.Values) feel.Detach();
            _feels.Clear();
            Animator.Dispose();
        }

        /// <summary>
        /// Gives <paramref name="button"/> the touch feel (squash, haptic on press-down, spring back, hover
        /// wobble) and routes its click to <paramref name="onClick"/>.
        /// </summary>
        protected PressFeel Feel(Button button, HapticKind pressHaptic, Action onClick)
        {
            var feel = new PressFeel(Animator, button, Haptics, pressHaptic, onClick);
            if (!string.IsNullOrEmpty(button.name)) _feels[button.name] = feel;
            return feel;
        }

        /// <summary>
        /// Taps the named button, or the row or pill it was extended to (<see cref="PressFeel.ExtendTo"/>), as a
        /// finger would (press feedback, release, click). EditMode tests use it: without a panel, UI Toolkit
        /// dispatches no pointer events. Returns false if no such button or hit area has a feel.
        /// </summary>
        internal bool SimulateTap(string name)
        {
            if (name == null) return false;
            PressFeel feel;
            if (!_feels.TryGetValue(name, out feel))
            {
                foreach (PressFeel f in _feels.Values)
                {
                    if (f.HitArea != null && f.HitArea.name == name) feel = f;
                }
                if (feel == null) return false;
            }
            feel.SimulateTap();
            return true;
        }

        /// <summary>
        /// Fades the whole screen in: the entrance used with Reduce motion (and by simple screens). Call
        /// <see cref="UiAnimator.Commit"/> once the screen is set up so the first frame starts transparent.
        /// </summary>
        protected void FadeIn(float seconds)
        {
            Animator.Play(Animator.Node(StyledRoot), MotionChannel.Opacity, new Tween(0f, 1f, seconds, Ease.OutCubic));
        }

        private void OnLocaleChanged()
        {
            Refresh();
        }

        /// <summary>Finds a named element and fails loudly if the UXML and the presenter disagree.</summary>
        protected T Required<T>(string name) where T : VisualElement
        {
            T element = Root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    GetType().Name + ": UXML has no " + typeof(T).Name + " named '" + name + "'.");
            }
            return element;
        }
    }
}
