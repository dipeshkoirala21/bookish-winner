using System;
using Ghumante.Core.Services;
using UnityEngine.UIElements;

namespace Ghumante.UI.Motion
{
    /// <summary>
    /// Touch feel for one button (UI/README.md, "Touch feel"): on press-down it squashes onto its base and
    /// plays <see cref="PressHaptic"/>; on release it springs back with an overshoot; a mouse hovering over
    /// it gives it a small wobble. The click itself is still <see cref="Button.clicked"/> (which fires on
    /// release), routed to the action given here.
    /// <para>Button's Clickable manipulator captures the pointer and handles PointerDown itself, so the
    /// callbacks here register in the trickle-down phase to see the press first. With Reduce motion the
    /// squash and wobble are skipped (USS still shows the pressed sink) but the haptic plays.</para>
    /// <para>A small control can take its whole row or pill as its touch target (<see cref="ExtendTo"/>), so
    /// it meets the 44 pt / 48 dp minimum without changing the art.</para>
    /// </summary>
    public sealed class PressFeel
    {
        private readonly UiAnimator _animator;
        private readonly IHaptics _haptics;
        private readonly Action _onClick;
        private readonly MotionNode _node;
        private Clickable _areaClick;
        private bool _pressed;
        private bool _wobbleRight;

        public PressFeel(UiAnimator animator, Button button, IHaptics haptics, HapticKind pressHaptic, Action onClick)
        {
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            Button = button ?? throw new ArgumentNullException(nameof(button));
            _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
            _onClick = onClick;
            PressHaptic = pressHaptic;
            _node = animator.Node(button);

            button.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            button.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            button.clicked += OnClicked;
        }

        public Button Button { get; private set; }

        /// <summary>The larger element that also presses and clicks the button (null: none).</summary>
        public VisualElement HitArea { get; private set; }

        /// <summary>The node that animates the button (squash amount, extra pops).</summary>
        public MotionNode Node
        {
            get { return _node; }
        }

        /// <summary>Haptic played on press-down (LightImpact for ordinary buttons, MediumImpact for Explore,
        /// Selection for toggles).</summary>
        public HapticKind PressHaptic { get; set; }

        /// <summary>False for buttons that must not squash or wobble (a full-screen scrim); the haptic stays.</summary>
        public bool Bouncy { get; set; } = true;

        /// <summary>
        /// Makes <paramref name="area"/>, an element around the button (its row, the pill it sits in), press
        /// and click it too: the button squashes and buzzes when the area is pressed, and a tap released over
        /// the area is the button's click. Taps on the button itself are unchanged (its Clickable stops the
        /// press before the area's own sees it). One area per button; null is ignored.
        /// </summary>
        public PressFeel ExtendTo(VisualElement area)
        {
            if (area == null || area == Button || HitArea != null) return this;
            HitArea = area;
            area.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            area.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            area.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            area.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            _areaClick = new Clickable(OnAreaClicked);
            area.AddManipulator(_areaClick);
            return this;
        }

        /// <summary>Plays the whole tap (press feedback, release, click) without a pointer. For tests and
        /// for keyboard or accessibility activation paths that bypass the pointer.</summary>
        public void SimulateTap()
        {
            Press();
            Release();
            OnClicked();
        }

        /// <summary>Unregisters every callback (the screen is going away).</summary>
        public void Detach()
        {
            Button.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            Button.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            Button.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            Button.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            Button.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            Button.clicked -= OnClicked;
            if (HitArea != null)
            {
                HitArea.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                HitArea.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
                HitArea.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
                HitArea.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
                HitArea.RemoveManipulator(_areaClick);
                _areaClick = null;
                HitArea = null;
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            // Only the primary button of a mouse; any finger or pen.
            if (evt.pointerType == PointerType.mouse && evt.button != 0) return;
            Press();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            Release();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            Release();
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            Release();
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (evt.pointerType != PointerType.mouse || _pressed || _animator.Reduced || !Bouncy) return;
            if (!Button.enabledInHierarchy) return;
            _wobbleRight = !_wobbleRight;
            _node.KickWobble(_wobbleRight ? 55f : -55f);
        }

        private void Press()
        {
            if (_pressed || !Button.enabledInHierarchy) return;
            _pressed = true;
            _haptics.Play(PressHaptic);
            if (!_animator.Reduced && Bouncy) _node.Press(true);
        }

        private void Release()
        {
            if (!_pressed) return;
            _pressed = false;
            if (Bouncy) _node.Press(false);
        }

        private void OnClicked()
        {
            if (_onClick != null) _onClick();
        }

        private void OnAreaClicked()
        {
            // A disabled button keeps its row from acting for it, too.
            if (Button.enabledInHierarchy) OnClicked();
        }
    }
}
