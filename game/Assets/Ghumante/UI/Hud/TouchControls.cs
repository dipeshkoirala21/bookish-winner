using System;
using Ghumante.Core.Motion;
using Ghumante.Core.Services;
using Ghumante.UI.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Hud
{
    /// <summary>What the touch controls say this frame (read once per frame with <see cref="TouchControls.Read"/>).</summary>
    public struct TouchState
    {
        /// <summary>Stick or drive-zone steering (x right) and, on foot, the walking direction (y up), in [−1, 1].</summary>
        public float MoveX, MoveY;

        /// <summary>Throttle pedal or the held drive zone, and the brake/reverse pedal, in [0, 1].</summary>
        public float Throttle, Reverse;

        /// <summary>Pinch zoom in notches (spreading zooms in) and world drags (camera turn, degrees) since the last read.</summary>
        public float ZoomSteps, LookYawDeg, LookPitchDeg;

        /// <summary>A finger rests on the world (the camera holds where it was turned).</summary>
        public bool Looking;

        /// <summary>A finger (not a mouse) touched the HUD since the last read: touch controls should show.</summary>
        public bool Touched;

        /// <summary>A stick, zone or pedal is held.</summary>
        public bool Active;
    }

    /// <summary>The elements of the touch controls, resolved by the Explore screen (check_ui verifies the names).</summary>
    public sealed class TouchControlsView
    {
        /// <summary>Container of the sticks, zones and pedals (hidden for keyboard and gamepad).</summary>
        public VisualElement Controls;

        /// <summary>The world surface behind the HUD: one finger looks around, two pinch to zoom. Always active.</summary>
        public VisualElement Pad;

        /// <summary>Floating stick zone: landscape left side; portrait lower area on foot.</summary>
        public VisualElement StickZone;
        public VisualElement Stick;
        public VisualElement StickKnob;

        /// <summary>Portrait riding: hold anywhere in the lower area to throttle, slide sideways to steer.</summary>
        public VisualElement DriveZone;
        public VisualElement DriveMarker;

        /// <summary>Landscape throttle pedal and the brake/reverse pedal (both orientations while riding).</summary>
        public Button Throttle;
        public Button Brake;
    }

    /// <summary>
    /// Touch controls of the Explore HUD (ARCHITECTURE.md 7.10a, ADR-017). Landscape is two-thumb: a floating stick on
    /// the left steers (or walks), throttle and brake/reverse pedals sit on the right. Portrait is one-thumb: while riding,
    /// press and hold in the lower area to throttle and slide sideways to steer, with a brake/reverse button; on foot a
    /// floating stick appears wherever the thumb lands in the lower area. Which zones exist is USS (the
    /// <c>gh-hud--ride</c>/<c>gh-hud--walk</c> and <c>orient-*</c> classes); this class follows the fingers. Pedals use the
    /// shared press feel and haptics; the stick and zone tick (Selection) when a thumb lands. Multi-touch: each control
    /// captures its own pointer. The world pad behind the HUD turns the camera (one finger) and zooms (pinch).
    /// </summary>
    public sealed class TouchControls : IDisposable
    {
        public const string ActiveClass = "gh-stick--active";
        public const string DriveActiveClass = "gh-drive-marker--active";

        private const int NoPointer = -1;

        private readonly TouchControlsView _view;
        private readonly UiAnimator _animator;
        private readonly IHaptics _haptics;
        private readonly MotionNode _stickNode;
        private readonly MotionNode _knobNode;
        private readonly MotionNode _markerNode;

        private int _stickPointer = NoPointer;
        private Vector2 _stickOrigin;
        private float _stickX, _stickY;

        private int _drivePointer = NoPointer;
        private float _driveOriginX;
        private float _driveSteer;

        private int _throttlePointer = NoPointer;
        private int _brakePointer = NoPointer;

        private int _padA = NoPointer, _padB = NoPointer;
        private Vector2 _padPosA, _padPosB;
        private float _padSpan;

        private float _zoom, _lookYaw, _lookPitch;
        private bool _touched;
        private bool _visible = true;

        public TouchControls(TouchControlsView view, UiAnimator animator, IHaptics haptics,
                             Func<Button, HapticKind, Action, PressFeel> feel)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
            if (feel == null) throw new ArgumentNullException(nameof(feel));
            _stickNode = animator.Node(view.Stick, true);
            _knobNode = animator.Node(view.StickKnob, true);
            _markerNode = animator.Node(view.DriveMarker, true);
            _markerNode.SetRest(MotionChannel.Opacity, 0f);

            feel(view.Throttle, HapticKind.LightImpact, null);
            feel(view.Brake, HapticKind.LightImpact, null);
            HoldTracking(view.Throttle, true);
            HoldTracking(view.Brake, false);

            view.StickZone.RegisterCallback<PointerDownEvent>(OnStickDown);
            view.StickZone.RegisterCallback<PointerMoveEvent>(OnStickMove);
            view.StickZone.RegisterCallback<PointerUpEvent>(OnStickUp);
            view.StickZone.RegisterCallback<PointerCancelEvent>(OnStickCancel);
            view.StickZone.RegisterCallback<PointerCaptureOutEvent>(OnStickCaptureOut);

            view.DriveZone.RegisterCallback<PointerDownEvent>(OnDriveDown);
            view.DriveZone.RegisterCallback<PointerMoveEvent>(OnDriveMove);
            view.DriveZone.RegisterCallback<PointerUpEvent>(OnDriveUp);
            view.DriveZone.RegisterCallback<PointerCancelEvent>(OnDriveCancel);
            view.DriveZone.RegisterCallback<PointerCaptureOutEvent>(OnDriveCaptureOut);

            view.Pad.RegisterCallback<PointerDownEvent>(OnPadDown);
            view.Pad.RegisterCallback<PointerMoveEvent>(OnPadMove);
            view.Pad.RegisterCallback<PointerUpEvent>(OnPadUp);
            view.Pad.RegisterCallback<PointerCancelEvent>(OnPadCancel);
        }

        /// <summary>Riding (pedals, steering only) or walking (the stick is a direction). Set by the screen.</summary>
        public bool Riding { get; set; } = true;

        /// <summary>Shows or hides the sticks, zones and pedals (the world pad stays); hiding lets go of them.</summary>
        public bool Visible
        {
            get { return _visible; }
            set
            {
                if (_visible == value) return;
                _visible = value;
                if (!value) ReleaseControls();
                DisplayStyle d = value ? DisplayStyle.Flex : DisplayStyle.None;
                if (_view.Controls.style.display != d) _view.Controls.style.display = d;
            }
        }

        /// <summary>True while a stick, zone or pedal is held.</summary>
        public bool AnyHeld
        {
            get { return _stickPointer != NoPointer || _drivePointer != NoPointer || _throttlePointer != NoPointer || _brakePointer != NoPointer; }
        }

        /// <summary>This frame's touch input; the zoom and look deltas and the touched flag reset.</summary>
        public TouchState Read()
        {
            var s = new TouchState();
            if (Riding)
            {
                s.MoveX = _drivePointer != NoPointer ? _driveSteer : _stickX;
                s.Throttle = _drivePointer != NoPointer || _throttlePointer != NoPointer ? 1f : 0f;
                s.Reverse = _brakePointer != NoPointer ? 1f : 0f;
            }
            else
            {
                s.MoveX = _stickX;
                s.MoveY = _stickY;
            }
            s.ZoomSteps = _zoom;
            s.LookYawDeg = _lookYaw;
            s.LookPitchDeg = _lookPitch;
            s.Looking = _padA != NoPointer;
            s.Touched = _touched;
            s.Active = AnyHeld;
            _zoom = _lookYaw = _lookPitch = 0f;
            _touched = false;
            return s;
        }

        /// <summary>Lets go of every control and the world pad (pause, a sheet opening, rotation).</summary>
        public void ReleaseAll()
        {
            ReleaseControls();
            ReleasePad(_padA);
            ReleasePad(_padB);
        }

        public void Dispose()
        {
            ReleaseAll();
            VisualElement zone = _view.StickZone;
            zone.UnregisterCallback<PointerDownEvent>(OnStickDown);
            zone.UnregisterCallback<PointerMoveEvent>(OnStickMove);
            zone.UnregisterCallback<PointerUpEvent>(OnStickUp);
            zone.UnregisterCallback<PointerCancelEvent>(OnStickCancel);
            zone.UnregisterCallback<PointerCaptureOutEvent>(OnStickCaptureOut);
            VisualElement drive = _view.DriveZone;
            drive.UnregisterCallback<PointerDownEvent>(OnDriveDown);
            drive.UnregisterCallback<PointerMoveEvent>(OnDriveMove);
            drive.UnregisterCallback<PointerUpEvent>(OnDriveUp);
            drive.UnregisterCallback<PointerCancelEvent>(OnDriveCancel);
            drive.UnregisterCallback<PointerCaptureOutEvent>(OnDriveCaptureOut);
            VisualElement pad = _view.Pad;
            pad.UnregisterCallback<PointerDownEvent>(OnPadDown);
            pad.UnregisterCallback<PointerMoveEvent>(OnPadMove);
            pad.UnregisterCallback<PointerUpEvent>(OnPadUp);
            pad.UnregisterCallback<PointerCancelEvent>(OnPadCancel);
        }

        // ----- Stick ------------------------------------------------------------------------------------------

        private void OnStickDown(PointerDownEvent evt)
        {
            if (_stickPointer != NoPointer || !IsPrimary(evt)) return;
            Note(evt.pointerType);
            _stickPointer = evt.pointerId;
            _stickOrigin = evt.localPosition;
            _view.StickZone.CapturePointer(evt.pointerId);
            // The stick jumps under the thumb (floating), from its rest place in the zone.
            Rect rest = _view.Stick.layout;
            if (!float.IsNaN(rest.width))
            {
                _stickNode.Set(MotionChannel.TranslateX, _stickOrigin.x - rest.center.x);
                _stickNode.Set(MotionChannel.TranslateY, _stickOrigin.y - rest.center.y);
            }
            _knobNode.Set(MotionChannel.TranslateX, 0f);
            _knobNode.Set(MotionChannel.TranslateY, 0f);
            _view.Stick.AddToClassList(ActiveClass);
            _haptics.Play(HapticKind.Selection);
            evt.StopPropagation();
        }

        private void OnStickMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _stickPointer) return;
            Vector2 d = (Vector2)evt.localPosition - _stickOrigin;
            float kx, ky;
            TouchMath.Stick(d.x, d.y, out _stickX, out _stickY, out kx, out ky);
            _knobNode.Set(MotionChannel.TranslateX, kx);
            _knobNode.Set(MotionChannel.TranslateY, ky);
        }

        private void OnStickUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _stickPointer) ReleaseStick();
        }

        private void OnStickCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _stickPointer) ReleaseStick();
        }

        private void OnStickCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_stickPointer != NoPointer) ReleaseStick();
        }

        private void ReleaseStick()
        {
            if (_stickPointer == NoPointer) return;
            int pointer = _stickPointer;
            _stickPointer = NoPointer;
            _stickX = _stickY = 0f;
            if (_view.StickZone.HasPointerCapture(pointer)) _view.StickZone.ReleasePointer(pointer);
            _view.Stick.RemoveFromClassList(ActiveClass);
            if (_animator.Reduced)
            {
                _knobNode.Set(MotionChannel.TranslateX, 0f);
                _knobNode.Set(MotionChannel.TranslateY, 0f);
                _stickNode.Set(MotionChannel.TranslateX, 0f);
                _stickNode.Set(MotionChannel.TranslateY, 0f);
                return;
            }
            _animator.Play(_knobNode, MotionChannel.TranslateX, new Tween(_knobNode.Get(MotionChannel.TranslateX), 0f, 0.18f, Ease.OutBack));
            _animator.Play(_knobNode, MotionChannel.TranslateY, new Tween(_knobNode.Get(MotionChannel.TranslateY), 0f, 0.18f, Ease.OutBack));
            _animator.Play(_stickNode, MotionChannel.TranslateX, new Tween(_stickNode.Get(MotionChannel.TranslateX), 0f, 0.3f, Ease.OutCubic));
            _animator.Play(_stickNode, MotionChannel.TranslateY, new Tween(_stickNode.Get(MotionChannel.TranslateY), 0f, 0.3f, Ease.OutCubic));
        }

        // ----- Portrait drive zone --------------------------------------------------------------------------------

        private void OnDriveDown(PointerDownEvent evt)
        {
            if (_drivePointer != NoPointer || !IsPrimary(evt)) return;
            Note(evt.pointerType);
            _drivePointer = evt.pointerId;
            _driveOriginX = evt.localPosition.x;
            _driveSteer = 0f;
            _view.DriveZone.CapturePointer(evt.pointerId);
            Rect marker = _view.DriveMarker.layout;
            float half = float.IsNaN(marker.width) ? 0f : marker.width * 0.5f;
            _markerNode.Set(MotionChannel.TranslateX, evt.localPosition.x - half);
            _markerNode.Set(MotionChannel.TranslateY, evt.localPosition.y - half);
            _markerNode.Set(MotionChannel.RotateDegrees, 0f);
            _markerNode.Set(MotionChannel.Opacity, 1f);
            if (!_animator.Reduced)
            {
                _animator.Play(_markerNode, MotionChannel.ScaleX, new Tween(0.6f, 1f, 0.25f, Ease.OutBack));
                _animator.Play(_markerNode, MotionChannel.ScaleY, new Tween(0.6f, 1f, 0.25f, Ease.OutBack));
            }
            _view.DriveMarker.AddToClassList(DriveActiveClass);
            _haptics.Play(HapticKind.Selection);
            evt.StopPropagation();
        }

        private void OnDriveMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _drivePointer) return;
            _driveSteer = TouchMath.DriveSteer(evt.localPosition.x - _driveOriginX);
            if (!_animator.Reduced) _markerNode.Set(MotionChannel.RotateDegrees, 28f * _driveSteer);
        }

        private void OnDriveUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _drivePointer) ReleaseDrive();
        }

        private void OnDriveCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _drivePointer) ReleaseDrive();
        }

        private void OnDriveCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_drivePointer != NoPointer) ReleaseDrive();
        }

        private void ReleaseDrive()
        {
            if (_drivePointer == NoPointer) return;
            int pointer = _drivePointer;
            _drivePointer = NoPointer;
            _driveSteer = 0f;
            if (_view.DriveZone.HasPointerCapture(pointer)) _view.DriveZone.ReleasePointer(pointer);
            _view.DriveMarker.RemoveFromClassList(DriveActiveClass);
            if (_animator.Reduced) _markerNode.Set(MotionChannel.Opacity, 0f);
            else _animator.Play(_markerNode, MotionChannel.Opacity, new Tween(1f, 0f, 0.2f, Ease.Linear));
        }

        // ----- Pedals --------------------------------------------------------------------------------------------

        private void HoldTracking(Button pedal, bool throttle)
        {
            // Trickle-down: the Clickable captures the pointer on press-down, so see the press first (like PressFeel).
            pedal.RegisterCallback<PointerDownEvent>(evt =>
            {
                Note(evt.pointerType);
                if (throttle) _throttlePointer = evt.pointerId;
                else _brakePointer = evt.pointerId;
            }, TrickleDown.TrickleDown);
            pedal.RegisterCallback<PointerUpEvent>(evt => ReleasePedal(throttle, evt.pointerId), TrickleDown.TrickleDown);
            pedal.RegisterCallback<PointerCancelEvent>(evt => ReleasePedal(throttle, evt.pointerId), TrickleDown.TrickleDown);
            pedal.RegisterCallback<PointerCaptureOutEvent>(evt => ReleasePedal(throttle, evt.pointerId));
        }

        private void ReleasePedal(bool throttle, int pointerId)
        {
            if (throttle)
            {
                if (_throttlePointer == pointerId) _throttlePointer = NoPointer;
            }
            else if (_brakePointer == pointerId)
            {
                _brakePointer = NoPointer;
            }
        }

        private void ReleaseControls()
        {
            ReleaseStick();
            ReleaseDrive();
            if (_throttlePointer != NoPointer && _view.Throttle.HasPointerCapture(_throttlePointer)) _view.Throttle.ReleasePointer(_throttlePointer);
            if (_brakePointer != NoPointer && _view.Brake.HasPointerCapture(_brakePointer)) _view.Brake.ReleasePointer(_brakePointer);
            _throttlePointer = NoPointer;
            _brakePointer = NoPointer;
        }

        // ----- World pad: look and pinch -------------------------------------------------------------------------------

        private void OnPadDown(PointerDownEvent evt)
        {
            if (evt.pointerType == UnityEngine.UIElements.PointerType.mouse && evt.button != 0) return;
            Note(evt.pointerType);
            if (_padA == NoPointer)
            {
                _padA = evt.pointerId;
                _padPosA = evt.position;
            }
            else if (_padB == NoPointer && evt.pointerId != _padA)
            {
                _padB = evt.pointerId;
                _padPosB = evt.position;
                _padSpan = Vector2.Distance(_padPosA, _padPosB);
            }
            else
            {
                return;
            }
            _view.Pad.CapturePointer(evt.pointerId);
        }

        private void OnPadMove(PointerMoveEvent evt)
        {
            Vector2 p = evt.position;
            if (evt.pointerId == _padA)
            {
                Vector2 delta = p - _padPosA;
                _padPosA = p;
                if (_padB == NoPointer)
                {
                    _lookYaw += delta.x * TouchMath.LookDegPerUnit;
                    _lookPitch += delta.y * TouchMath.LookDegPerUnit * 0.6f;
                }
            }
            else if (evt.pointerId == _padB)
            {
                _padPosB = p;
            }
            else
            {
                return;
            }
            if (_padB != NoPointer)
            {
                float span = Vector2.Distance(_padPosA, _padPosB);
                _zoom += TouchMath.PinchNotches(_padSpan, span);
                _padSpan = span;
            }
        }

        private void OnPadUp(PointerUpEvent evt)
        {
            ReleasePad(evt.pointerId);
        }

        private void OnPadCancel(PointerCancelEvent evt)
        {
            ReleasePad(evt.pointerId);
        }

        private void ReleasePad(int pointer)
        {
            if (pointer == NoPointer || pointer != _padA && pointer != _padB) return;
            if (_view.Pad.HasPointerCapture(pointer)) _view.Pad.ReleasePointer(pointer);
            if (pointer == _padA)
            {
                _padA = _padB;
                _padPosA = _padPosB;
            }
            _padB = NoPointer;
        }

        private static bool IsPrimary(PointerDownEvent evt)
        {
            return evt.pointerType != UnityEngine.UIElements.PointerType.mouse || evt.button == 0;
        }

        private void Note(string pointerType)
        {
            if (pointerType != UnityEngine.UIElements.PointerType.mouse) _touched = true;
        }
    }
}
