using System;
using Ghumante.Core.Motion;
using Ghumante.Core.Services;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Screens
{
    /// <summary>The elements of the settings sheet (resolved by the owning screen with Required, so
    /// tools/unity-compile-check/check_ui.py verifies every name against the UXML).</summary>
    public sealed class SettingsSheetView
    {
        public VisualElement Layer;
        public Button Scrim;
        public VisualElement Sheet;
        public VisualElement Content;
        public Button Close;
        public Button Done;
        public Button HapticsToggle;
        public VisualElement HapticsKnob;
        public Label HapticsNote;
        public Button MotionToggle;
        public VisualElement MotionKnob;
        public Button LanguageEnglish;
        public Button LanguageNepali;
        public Button FeelPlay;
        public Label FeelStatus;

        /// <summary>The grabber and header (may be null): dragging it towards the sheet's edge dismisses the
        /// sheet. Buttons inside it (close) keep their own taps.</summary>
        public VisualElement Handle;

        /// <summary>Rows that slide in one after another when the sheet opens.</summary>
        public VisualElement[] Rows;
    }

    /// <summary>
    /// Settings (Screens/MainMenu.uxml, "settings-layer"): Vibration, Reduce motion, Language and "Feel the
    /// haptics". A bottom sheet in portrait and a side panel in landscape (ARCHITECTURE.md 7.10a); it slides
    /// in with a little overshoot (a stretch from its screen edge, see <see cref="EdgeSheet.Pose"/>), or just
    /// fades with Reduce motion. It closes with the scrim, the close button, Done, a drag on its grabber and
    /// header towards the edge (with a Selection tick where letting go would close it), and the Android back
    /// button or Escape. Switches apply at once to the shared <see cref="IHaptics"/> and
    /// <see cref="MotionSettings"/>; the events tell App to persist them.
    /// </summary>
    public sealed class SettingsSheet : IDisposable
    {
        public const string OpenClass = "gh-sheet-layer--open";
        public const string ToggleOnClass = "gh-toggle--on";
        public const string SelectedClass = "gh-segment__option--selected";

        /// <summary>Knob travel between off and on (USS .gh-toggle__knob left 4px vs 50px).</summary>
        private const float KnobTravel = 46f;

        /// <summary>Order of the "Feel the haptics" demo, with the string key naming each kind.</summary>
        public static readonly HapticKind[] FeelOrder =
        {
            HapticKind.Selection, HapticKind.LightImpact, HapticKind.MediumImpact, HapticKind.HeavyImpact,
            HapticKind.Success, HapticKind.Warning, HapticKind.Error,
        };

        private static readonly string[] FeelKeys =
        {
            "haptics.kind.selection", "haptics.kind.light", "haptics.kind.medium", "haptics.kind.heavy",
            "haptics.kind.success", "haptics.kind.warning", "haptics.kind.error",
        };

        private const float FeelSpacing = 0.75f;

        /// <summary>Added to the sheet's size for its hidden position, so its shadow clears the screen too.</summary>
        private const float HiddenMargin = 48f;

        /// <summary>Panel units a finger moves on the handle before the press becomes a drag.</summary>
        private const float DragSlop = 12f;

        /// <summary>Milliseconds a finger may rest before letting go and still count as a flick.</summary>
        private const long FlickMaxRestMs = 90;

        private const int NoPointer = -1;

        private readonly SettingsSheetView _view;
        private readonly UiAnimator _animator;
        private readonly Localizer _localizer;
        private readonly IHaptics _haptics;
        private readonly MotionSettings _motion;
        private readonly VisualElement _orientationRoot;
        private readonly VisualElement _bleedRoot;
        private readonly MotionNode _sheetNode;
        private readonly MotionNode _scrimNode;
        private readonly MotionNode _hapticsKnob;
        private readonly MotionNode _motionKnob;
        private readonly MotionNode _statusNode;
        private readonly MotionNode[] _rowNodes;
        private bool _open;
        private float _progress;
        private float _from;
        private float _target;
        private float _start;
        private float _duration;
        private bool _sliding;
        private bool _slideFromDrag;
        private int _feelToken;
        private Rect _bledFor;
        private VisualElement _backTarget;
        private int _dragPointer = NoPointer;
        private bool _dragMoved;
        private Vector2 _dragOrigin;
        private float _dragStartProgress;
        private float _dragLastProgress;
        private long _dragLastTime;
        private float _dragVelocity;
        private bool _pastDismiss;

        /// <param name="view">Elements.</param>
        /// <param name="animator">The owning screen's animator.</param>
        /// <param name="localizer">Strings.</param>
        /// <param name="haptics">Shared haptics (its Enabled is the Vibration switch).</param>
        /// <param name="motion">Shared motion settings (ReduceMotion is the Reduce motion switch).</param>
        /// <param name="feel">The owning screen's touch-feel factory.</param>
        /// <param name="orientationRoot">Element carrying the orient-* classes.</param>
        /// <param name="bleedRoot">Document root whose padding is the safe area (may be null).</param>
        public SettingsSheet(SettingsSheetView view, UiAnimator animator, Localizer localizer, IHaptics haptics,
                             MotionSettings motion, Func<Button, HapticKind, Action, PressFeel> feel,
                             VisualElement orientationRoot, VisualElement bleedRoot)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
            if (feel == null) throw new ArgumentNullException(nameof(feel));
            _orientationRoot = orientationRoot;
            _bleedRoot = bleedRoot;

            _sheetNode = animator.Node(view.Sheet, true);
            _scrimNode = animator.Node(view.Scrim);
            _hapticsKnob = animator.Node(view.HapticsKnob);
            _motionKnob = animator.Node(view.MotionKnob);
            _hapticsKnob.SquashAmount = 0.2f;
            _motionKnob.SquashAmount = 0.2f;
            _statusNode = animator.Node(view.FeelStatus);
            _rowNodes = new MotionNode[view.Rows == null ? 0 : view.Rows.Length];
            for (int i = 0; i < _rowNodes.Length; i++) _rowNodes[i] = animator.Node(view.Rows[i]);

            feel(view.Scrim, HapticKind.Selection, Close).Bouncy = false;  // a full-screen backdrop must not squash
            feel(view.Close, HapticKind.LightImpact, Close);
            feel(view.Done, HapticKind.LightImpact, Close);
            // The switches and Play are small; their whole row (label and hint too) is the touch target.
            feel(view.HapticsToggle, HapticKind.Selection, ToggleHaptics).ExtendTo(view.HapticsToggle.parent);
            feel(view.MotionToggle, HapticKind.Selection, ToggleReduceMotion).ExtendTo(view.MotionToggle.parent);
            feel(view.LanguageEnglish, HapticKind.Selection, () => ChooseLanguage(Localizer.English));
            feel(view.LanguageNepali, HapticKind.Selection, () => ChooseLanguage(Localizer.Nepali));
            feel(view.FeelPlay, HapticKind.LightImpact, PlayFeelDemo).ExtendTo(view.FeelPlay.parent);

            if (view.Handle != null)
            {
                view.Handle.RegisterCallback<PointerDownEvent>(OnHandleDown);
                view.Handle.RegisterCallback<PointerMoveEvent>(OnHandleMove);
                view.Handle.RegisterCallback<PointerUpEvent>(OnHandleUp);
                view.Handle.RegisterCallback<PointerCancelEvent>(OnHandleCancel);
                view.Handle.RegisterCallback<PointerCaptureOutEvent>(OnHandleCaptureOut);
            }

            if (_bleedRoot != null)
            {
                _bleedRoot.RegisterCallback<GeometryChangedEvent>(OnRootGeometry);
                animator.OnUpdate(WatchSafeArea);
            }
            Bleed();
            animator.OnUpdate(UpdateSlide);
            Refresh();
        }

        /// <summary>The player switched Vibration (already applied to IHaptics.Enabled).</summary>
        public event Action<bool> HapticsChanged;

        /// <summary>The player switched Reduce motion (already applied to MotionSettings.ReduceMotion).</summary>
        public event Action<bool> ReduceMotionChanged;

        /// <summary>The player picked a language ("en" or "ne"); App applies and persists it.</summary>
        public event Action<string> LanguageRequested;

        public bool IsOpen
        {
            get { return _open; }
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            _view.Layer.AddToClassList(OpenClass);
            ListenForBack();
            Refresh();
            StartSlide(1f, _animator.Reduced ? 0.15f : 0.42f);
            if (_animator.Reduced) return;
            var rows = new Stagger(0.1f, 0.05f, 0.4f, Ease.OutBack);
            for (int i = 0; i < _rowNodes.Length; i++)
            {
                _animator.Play(_rowNodes[i], MotionChannel.TranslateY, rows.At(i, 46f, 0f));
                _animator.Play(_rowNodes[i], MotionChannel.Opacity, new Tween(0f, 1f, 0.2f, Ease.Linear, rows.DelayOf(i)));
            }
        }

        public void Close()
        {
            Dismiss(false);
        }

        /// <summary>
        /// What the Android back button and Escape do (the sheet listens for them itself while open): closes
        /// the sheet with the close button's tap. False when it was not open, so the caller may handle back.
        /// </summary>
        public bool Back()
        {
            if (!_open) return false;
            _haptics.Play(HapticKind.LightImpact);
            Close();
            return true;
        }

        /// <summary>Re-reads every switch and label from the shared state (after a locale change, too).</summary>
        public void Refresh()
        {
            _view.HapticsToggle.EnableInClassList(ToggleOnClass, _haptics.Enabled);
            _view.MotionToggle.EnableInClassList(ToggleOnClass, _motion.ReduceMotion);
            bool nepali = _localizer.Locale == Localizer.Nepali;
            _view.LanguageEnglish.EnableInClassList(SelectedClass, !nepali);
            _view.LanguageNepali.EnableInClassList(SelectedClass, nepali);
            DisplayStyle note = _haptics.IsSupported ? DisplayStyle.None : DisplayStyle.Flex;
            if (_view.HapticsNote.style.display != note) _view.HapticsNote.style.display = note;
            _view.FeelStatus.text = _localizer.Get("settings.feel.hint");
        }

        public void Dispose()
        {
            _feelToken++;
            CancelDrag();
            StopListeningForBack();
            if (_bleedRoot != null) _bleedRoot.UnregisterCallback<GeometryChangedEvent>(OnRootGeometry);
            VisualElement handle = _view.Handle;
            if (handle != null)
            {
                handle.UnregisterCallback<PointerDownEvent>(OnHandleDown);
                handle.UnregisterCallback<PointerMoveEvent>(OnHandleMove);
                handle.UnregisterCallback<PointerUpEvent>(OnHandleUp);
                handle.UnregisterCallback<PointerCancelEvent>(OnHandleCancel);
                handle.UnregisterCallback<PointerCaptureOutEvent>(OnHandleCaptureOut);
            }
        }

        private void ToggleHaptics()
        {
            bool on = !_haptics.Enabled;
            _haptics.Enabled = on;
            AnimateToggle(_view.HapticsToggle, _hapticsKnob, on);
            // Turning vibration on answers with a buzz, so the player feels what they enabled.
            if (on) _haptics.Play(HapticKind.Success);
            else _feelToken++;
            Action<bool> handler = HapticsChanged;
            if (handler != null) handler(on);
        }

        private void ToggleReduceMotion()
        {
            bool on = !_motion.ReduceMotion;
            // Apply first: the knob then slides only when motion is (now) allowed, i.e. when switching it off.
            _motion.ReduceMotion = on;
            AnimateToggle(_view.MotionToggle, _motionKnob, on);
            Action<bool> handler = ReduceMotionChanged;
            if (handler != null) handler(on);
        }

        private void ChooseLanguage(string locale)
        {
            if (_localizer.Locale == locale) return;
            Action<string> handler = LanguageRequested;
            if (handler != null) handler(locale);
        }

        private void AnimateToggle(Button toggle, MotionNode knob, bool on)
        {
            // FLIP: the class moves the knob (USS `left`, once); a translate from where it was slides it over.
            toggle.EnableInClassList(ToggleOnClass, on);
            if (_animator.Reduced) return;
            float from = on ? -KnobTravel : KnobTravel;
            _animator.Play(knob, MotionChannel.TranslateX, new Tween(from, 0f, 0.34f, Ease.OutBack));
            knob.Press(true);
            _animator.After(0.11f, () => knob.Press(false));
        }

        private void PlayFeelDemo()
        {
            int token = ++_feelToken;
            if (!_haptics.Enabled)
            {
                _view.FeelStatus.text = _localizer.Get("settings.feel.off");
                if (!_animator.Reduced) _animator.Node(_view.HapticsToggle).KickWobble(260f);
                return;
            }
            for (int i = 0; i < FeelOrder.Length; i++)
            {
                int index = i;  // the closure needs its own copy
                _animator.After(0.3f + index * FeelSpacing, () => PlayFeelStep(token, index));
            }
            _animator.After(0.3f + FeelOrder.Length * FeelSpacing + 0.4f, () =>
            {
                if (token == _feelToken) _view.FeelStatus.text = _localizer.Get("settings.feel.hint");
            });
        }

        private void PlayFeelStep(int token, int index)
        {
            if (token != _feelToken || !_open) return;
            _haptics.Play(FeelOrder[index]);
            _view.FeelStatus.text = _localizer.Format("settings.feel.now", _localizer.Get(FeelKeys[index]));
            if (_animator.Reduced) return;
            _animator.Play(_statusNode, MotionChannel.ScaleX, new Tween(1.15f, 1f, 0.3f, Ease.OutBack));
            _animator.Play(_statusNode, MotionChannel.ScaleY, new Tween(1.15f, 1f, 0.3f, Ease.OutBack));
        }

        private void Dismiss(bool afterDrag)
        {
            if (!_open) return;
            _open = false;
            _feelToken++;
            CancelDrag();
            float duration = _animator.Reduced ? 0.12f : 0.24f;
            // After a drag the sheet carries on from where the finger left it.
            if (afterDrag) duration *= Mathf.Clamp(_progress, 0.4f, 1f);
            StartSlide(0f, duration, afterDrag);
        }

        private void StartSlide(float target, float duration, bool afterDrag = false)
        {
            _from = _progress;
            _target = target;
            _start = _animator.Time;
            _duration = duration;
            _slideFromDrag = afterDrag;
            _sliding = true;
            ApplySlide();
        }

        private void UpdateSlide(float time, float dt)
        {
            if (!_sliding) return;
            float t = _duration > 0f ? (time - _start) / _duration : 1f;
            float eased;
            if (_target > _from) eased = _animator.Reduced ? Easing.OutCubic(t) : Easing.OutBack(t, 1.1f);
            // Released from a drag it decelerates on from the finger's speed; a plain close accelerates away.
            else eased = _slideFromDrag ? Easing.OutCubic(t) : Easing.InCubic(t);
            _progress = Easing.Lerp(_from, _target, eased);
            if (t >= 1f)
            {
                _progress = _target;
                _sliding = false;
                if (_target <= 0f)
                {
                    _view.Layer.RemoveFromClassList(OpenClass);
                    StopListeningForBack();
                }
            }
            ApplySlide();
        }

        private void ApplySlide()
        {
            float shown = Mathf.Clamp01(_progress);
            _scrimNode.Set(MotionChannel.Opacity, shown);
            if (_animator.Reduced)
            {
                _sheetNode.Set(MotionChannel.TranslateX, 0f);
                _sheetNode.Set(MotionChannel.TranslateY, 0f);
                _sheetNode.Set(MotionChannel.ScaleX, 1f);
                _sheetNode.Set(MotionChannel.ScaleY, 1f);
                _sheetNode.Set(MotionChannel.Opacity, shown);
                return;
            }
            // Off-screen distance from the sheet's current size, read every frame, so a rotation in the
            // middle of the slide simply continues along the new axis. Past open (the OutBack overshoot, a
            // finger pulling it in) the sheet stays on its edge and stretches from it instead, so no gap ever
            // shows under its flat, borderless edge (USS transform-origin on that edge).
            bool portrait;
            float size = SlideSize(out portrait);
            float offset, stretch;
            EdgeSheet.Pose(_progress, size, size + HiddenMargin, out offset, out stretch);
            _sheetNode.Set(MotionChannel.Opacity, 1f);
            _sheetNode.Set(MotionChannel.TranslateX, portrait ? 0f : offset);
            _sheetNode.Set(MotionChannel.TranslateY, portrait ? offset : 0f);
            _sheetNode.Set(MotionChannel.ScaleX, portrait ? 1f : stretch);
            _sheetNode.Set(MotionChannel.ScaleY, portrait ? stretch : 1f);
        }

        /// <summary>The sheet's size along its slide axis: the bottom sheet's height or the side panel's width.</summary>
        private float SlideSize(out bool portrait)
        {
            portrait = _orientationRoot != null && _orientationRoot.ClassListContains(OrientationWatcher.PortraitClass);
            Rect r = _view.Sheet.layout;
            float size = portrait ? r.height : r.width;
            if (float.IsNaN(size) || size < 1f) size = 2400f;  // first frame after display:flex, not laid out yet
            return size;
        }

        // ----- Drag to dismiss (grabber and header) ------------------------------------------------------

        private void OnHandleDown(PointerDownEvent evt)
        {
            if (!_open || _dragPointer != NoPointer) return;
            if (evt.pointerType == UnityEngine.UIElements.PointerType.mouse && evt.button != 0) return;
            _dragPointer = evt.pointerId;
            _dragMoved = false;
            _dragOrigin = evt.position;
            _view.Handle.CapturePointer(evt.pointerId);
        }

        private void OnHandleMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _dragPointer) return;
            bool portrait;
            float size = SlideSize(out portrait);
            Vector2 delta = (Vector2)evt.position - _dragOrigin;
            float outward = portrait ? delta.y : delta.x;
            if (!_dragMoved)
            {
                if (Mathf.Abs(outward) < DragSlop) return;
                // From here the finger holds the sheet (even mid-slide): measure from this point on.
                _dragMoved = true;
                _sliding = false;
                _dragOrigin = evt.position;
                _dragStartProgress = _progress;
                _dragLastProgress = _progress;
                _dragLastTime = evt.timestamp;
                _dragVelocity = 0f;
                _pastDismiss = _progress < EdgeSheet.DismissBelow;
                return;
            }
            float progress = EdgeSheet.Drag(_dragStartProgress, outward, size + HiddenMargin);
            long now = evt.timestamp;
            if (now > _dragLastTime)
            {
                float v = (progress - _dragLastProgress) * 1000f / (now - _dragLastTime);
                _dragVelocity = Mathf.Lerp(_dragVelocity, v, 0.5f);  // smoothed: one jittery sample does not decide
                _dragLastTime = now;
                _dragLastProgress = progress;
            }
            _progress = progress;
            bool past = progress < EdgeSheet.DismissBelow;
            if (past != _pastDismiss)
            {
                // A detent: from here letting go closes the sheet (or, back over it, no longer does).
                _pastDismiss = past;
                _haptics.Play(HapticKind.Selection);
            }
            ApplySlide();
        }

        private void OnHandleUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _dragPointer) return;
            // A finger that rested before letting go was not flicking.
            float velocity = evt.timestamp - _dragLastTime > FlickMaxRestMs ? 0f : _dragVelocity;
            EndDrag(true, velocity);
        }

        private void OnHandleCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _dragPointer) EndDrag(false, 0f);
        }

        private void OnHandleCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_dragPointer != NoPointer) EndDrag(false, 0f);
        }

        private void EndDrag(bool released, float velocity)
        {
            bool moved = _dragMoved;
            CancelDrag();
            if (!moved || !_open) return;  // a tap on the title: nothing to do
            if (released && EdgeSheet.Dismisses(_progress, velocity)) Dismiss(true);
            else StartSlide(1f, 0.3f, true);
        }

        /// <summary>Forgets the drag in progress and releases its pointer, without moving the sheet.</summary>
        private void CancelDrag()
        {
            if (_dragPointer == NoPointer) return;
            int pointer = _dragPointer;
            _dragPointer = NoPointer;
            _dragMoved = false;
            VisualElement handle = _view.Handle;
            if (handle != null && handle.HasPointerCapture(pointer)) handle.ReleasePointer(pointer);
        }

        // ----- Back button ----------------------------------------------------------------------------------

        /// <summary>
        /// Android's back button and Escape arrive as NavigationCancelEvent (UI/Cancel). With nothing focused,
        /// UI Toolkit sends it to the panel's visual tree, above the document root, so the sheet listens there,
        /// in the trickle-down phase, while it is open.
        /// </summary>
        private void ListenForBack()
        {
            if (_backTarget != null) return;
            VisualElement root = _bleedRoot ?? _view.Layer;
            _backTarget = root.panel != null ? root.panel.visualTree : root;
            _backTarget.RegisterCallback<NavigationCancelEvent>(OnBack, TrickleDown.TrickleDown);
        }

        private void StopListeningForBack()
        {
            if (_backTarget == null) return;
            _backTarget.UnregisterCallback<NavigationCancelEvent>(OnBack, TrickleDown.TrickleDown);
            _backTarget = null;
        }

        private void OnBack(NavigationCancelEvent evt)
        {
            if (Back()) evt.StopPropagation();
        }

        private void OnRootGeometry(GeometryChangedEvent evt)
        {
            Bleed();
        }

        /// <summary>A 180-degree turn moves the insets without resizing the root (no geometry event).</summary>
        private void WatchSafeArea(float time, float dt)
        {
            if (_bleedRoot.panel != null && Screen.safeArea != _bledFor) Bleed();
        }

        /// <summary>
        /// The layer covers the whole screen, under the notch and home indicator too; the sheet's content
        /// stays inside the safe area through margins on its content element (layout, written only when the
        /// insets change, never animated). The insets are the padding <see cref="SafeArea"/> writes, not the
        /// resolved padding, which still holds the previous orientation's values inside the root's
        /// GeometryChangedEvent.
        /// </summary>
        private void Bleed()
        {
            if (_bleedRoot == null) return;
            _bledFor = Screen.safeArea;
            float left, top, right, bottom;
            SafeArea.Insets(_bleedRoot, out left, out top, out right, out bottom);
            // Only the edges the sheet touches: the bottom sheet spans the width, the side panel the height.
            // Classified like the orient-* classes that lay the sheet out (those are updated in a later
            // callback, so the class itself may still be the old one here).
            bool portrait = OrientationWatcher.Detect() == LayoutOrientation.Portrait;
            IStyle layer = _view.Layer.style;
            if (!Mathf.Approximately(layer.left.value.value, -left)) layer.left = -left;
            if (!Mathf.Approximately(layer.top.value.value, -top)) layer.top = -top;
            if (!Mathf.Approximately(layer.right.value.value, -right)) layer.right = -right;
            if (!Mathf.Approximately(layer.bottom.value.value, -bottom)) layer.bottom = -bottom;
            if (portrait) top = 0f;
            else left = 0f;
            IStyle content = _view.Content.style;
            if (!Mathf.Approximately(content.marginLeft.value.value, left)) content.marginLeft = left;
            if (!Mathf.Approximately(content.marginTop.value.value, top)) content.marginTop = top;
            if (!Mathf.Approximately(content.marginRight.value.value, right)) content.marginRight = right;
            if (!Mathf.Approximately(content.marginBottom.value.value, bottom)) content.marginBottom = bottom;
        }
    }
}
