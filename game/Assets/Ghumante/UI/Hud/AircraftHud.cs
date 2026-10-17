using System;
using Ghumante.Core.Driving;
using Ghumante.Core.Services;
using Ghumante.UI.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Hud
{
    /// <summary>
    /// The aircraft HUD (owner playtest #11; docs/research/w2/ref_flight.md §5), built in code over the Explore HUD so the
    /// shared UXML stays untouched: an instrument strip at the top (speed in knots, height above the ground in feet,
    /// climb in feet per minute, heading, power, flaps, wheels), a stall or turn-back banner, a short how-to-fly hint, and
    /// the touch controls — a floating flight stick on the left (pitch and roll; it steers on the ground), a throttle
    /// slider on the right that stays where it is put, brake and flaps beside it, rudder buttons, and View / Get out under
    /// the top bar. While it shows, the explorer's speedometer is hidden (the session hides the explorer's touch controls).
    /// Labels change only when a shown value changes; nothing allocates per frame. Main thread only.
    /// </summary>
    public sealed class AircraftHud : IDisposable
    {
        /// <summary>The how-to hint shows this long after boarding.</summary>
        public const float HintS = 12f;

        private const int NoPointer = -1;
        private const float StickRest = 120f, KnobHalf = 52f;

        private static readonly Color Cream = new Color32(0xFF, 0xF6, 0xE3, 0xF2), CreamEdge = new Color32(0xE3, 0xC5, 0x8F, 0xFF);
        private static readonly Color Ink = new Color32(0x5B, 0x37, 0x14, 0xFF), InkSoft = new Color32(0x8C, 0x67, 0x41, 0xFF);
        private static readonly Color Yellow = new Color32(0xFF, 0xCB, 0x2E, 0xFF), YellowDark = new Color32(0xDB, 0x8D, 0x12, 0xFF);
        private static readonly Color Red = new Color32(0xFF, 0x5B, 0x4D, 0xFF), RedDark = new Color32(0xBE, 0x32, 0x24, 0xFF);
        private static readonly Color Cyan = new Color32(0x35, 0xC2, 0xF1, 0xFF), CyanDark = new Color32(0x17, 0x81, 0xBF, 0xFF);
        private static readonly Color Counter = new Color32(62, 36, 12, 140), Glassy = new Color(1f, 1f, 1f, 0.16f);

        private readonly Localizer _localizer;
        private readonly IHaptics _haptics;
        private readonly VisualElement _root, _speedo, _touch;
        private readonly VisualElement _banner, _hintBubble, _stickZone, _stick, _knob, _track, _fill;
        private readonly Label _bannerLabel, _hint;
        private readonly Label[] _values = new Label[7], _captions = new Label[7];
        private readonly VisualElement[] _cells = new VisualElement[7];
        private readonly Label[] _buttonLabels = new Label[8];
        private readonly string[] _buttonKeys = new string[8];
        private readonly VisualElement _rudderLeft, _rudderRight, _brake, _flapsDown, _flapsUp, _view, _exit;
        private readonly Label _powerCaption;

        private static readonly string[] CaptionKeys =
        {
            "flight.hud.speed", "flight.hud.height", "flight.hud.climb", "flight.hud.heading", "flight.hud.power", "flight.hud.flaps", "flight.hud.gear",
        };

        private readonly int[] _shown = new int[7];
        private string _locale;
        private bool _devanagari, _visible, _touchVisible = true, _rotorcraft, _fixedGear;
        private int _bannerState = -1, _hintState = -1;
        private float _hintLeft, _flash;
        private string _rotateText;
        private int _rotateKt = -1;

        private int _stickPointer = NoPointer, _throttlePointer = NoPointer, _leftPointer = NoPointer, _rightPointer = NoPointer, _brakePointer = NoPointer;
        private Vector2 _stickOrigin;
        private float _stickX, _stickY, _throttle = -1f;
        private bool _flapsDownTap, _flapsUpTap, _viewTap, _exitTap, _touched;

        /// <summary>Builds the HUD into the Explore screen's styled root (<c>ExploreScreen.StyledRoot</c>), just above
        /// the explorer's touch controls (so the toast and the top bar stay on top). Hidden until <see cref="Show"/>.</summary>
        public AircraftHud(VisualElement hudRoot, Localizer localizer, IHaptics haptics)
        {
            if (hudRoot == null) throw new ArgumentNullException(nameof(hudRoot));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _haptics = haptics;
            _speedo = hudRoot.Q("hud-speedo");

            _root = new VisualElement { name = "flight-hud", pickingMode = PickingMode.Ignore };
            Fill(_root);
            _root.style.display = DisplayStyle.None;
            VisualElement controls = hudRoot.Q("hud-controls");
            VisualElement parent = controls != null ? controls.parent : hudRoot;
            int at = controls != null ? parent.IndexOf(controls) + 1 : parent.childCount;
            parent.Insert(at, _root);

            // Instruments: a cream strip under the top bar.
            var strip = new VisualElement { name = "flight-instruments", pickingMode = PickingMode.Ignore };
            strip.style.position = Position.Absolute;
            strip.style.top = Length.Percent(13f);
            strip.style.left = 0f;
            strip.style.right = 0f;
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.justifyContent = Justify.Center;
            _root.Add(strip);
            var panel = Chip(Cream, CreamEdge, 30f);
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.paddingLeft = panel.style.paddingRight = 12f;
            panel.style.paddingTop = panel.style.paddingBottom = 6f;
            strip.Add(panel);
            for (int i = 0; i < 7; i++)
            {
                var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                cell.style.alignItems = Align.Center;
                cell.style.minWidth = 104f;
                cell.style.paddingLeft = cell.style.paddingRight = 10f;
                _values[i] = Text("", 40f, Ink, true);
                _captions[i] = Text("", 19f, InkSoft, false);
                cell.Add(_values[i]);
                cell.Add(_captions[i]);
                panel.Add(cell);
                _cells[i] = cell;
                _shown[i] = int.MinValue;
            }

            // Banner (stall, turning back) and the hint bubble.
            _banner = Chip(Red, RedDark, 30f);
            _banner.style.paddingLeft = _banner.style.paddingRight = 30f;
            _banner.style.paddingTop = _banner.style.paddingBottom = 8f;
            _bannerLabel = Text("", 32f, Color.white, true);
            _banner.Add(_bannerLabel);
            var bannerRow = Row(Length.Percent(25f));
            bannerRow.Add(_banner);
            _root.Add(bannerRow);
            _banner.style.display = DisplayStyle.None;

            _hintBubble = Chip(Cream, CreamEdge, 24f);
            _hintBubble.style.paddingLeft = _hintBubble.style.paddingRight = 22f;
            _hintBubble.style.paddingTop = _hintBubble.style.paddingBottom = 6f;
            _hintBubble.style.maxWidth = Length.Percent(70f);
            _hint = Text("", 24f, Ink, false);
            _hint.style.whiteSpace = WhiteSpace.Normal;
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hintBubble.Add(_hint);
            var hintRow = Row(StyleKeyword.Auto);
            hintRow.style.bottom = 26f;
            hintRow.Add(_hintBubble);
            _root.Add(hintRow);
            _hintBubble.style.display = DisplayStyle.None;

            // Touch controls.
            _touch = new VisualElement { name = "flight-touch", pickingMode = PickingMode.Ignore };
            Fill(_touch);
            _root.Add(_touch);

            _stickZone = new VisualElement { name = "flight-stick-zone" };
            _stickZone.style.position = Position.Absolute;
            _stickZone.style.left = 0f;
            _stickZone.style.bottom = 0f;
            _stickZone.style.width = Length.Percent(36f);
            _stickZone.style.height = Length.Percent(56f);
            _touch.Add(_stickZone);
            _stick = new VisualElement { pickingMode = PickingMode.Ignore };
            _stick.style.position = Position.Absolute;
            _stick.style.left = 60f;
            _stick.style.bottom = 60f;
            _stick.style.width = _stick.style.height = StickRest * 2f;
            Round(_stick, StickRest);
            _stick.style.backgroundColor = Glassy;
            Border(_stick, new Color(1f, 1f, 1f, 0.75f), 6f);
            _stickZone.Add(_stick);
            _knob = new VisualElement { pickingMode = PickingMode.Ignore };
            _knob.style.position = Position.Absolute;
            _knob.style.left = _knob.style.top = StickRest - KnobHalf - 6f;
            _knob.style.width = _knob.style.height = KnobHalf * 2f;
            Round(_knob, KnobHalf);
            _knob.style.backgroundColor = new Color(1f, 1f, 1f, 0.9f);
            Border(_knob, CreamEdge, 4f);
            _stick.Add(_knob);
            _stickZone.RegisterCallback<PointerDownEvent>(OnStickDown);
            _stickZone.RegisterCallback<PointerMoveEvent>(OnStickMove);
            _stickZone.RegisterCallback<PointerUpEvent>(OnStickUp);
            _stickZone.RegisterCallback<PointerCancelEvent>(OnStickCancel);
            _stickZone.RegisterCallback<PointerCaptureOutEvent>(OnStickCaptureOut);

            // Throttle slider on the right edge.
            _track = new VisualElement { name = "flight-throttle" };
            _track.style.position = Position.Absolute;
            _track.style.right = 40f;
            _track.style.bottom = 70f;
            _track.style.width = 112f;
            _track.style.height = Length.Percent(42f);
            Round(_track, 30f);
            _track.style.backgroundColor = Counter;
            Border(_track, Cream, 6f);
            _track.style.overflow = Overflow.Hidden;
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.style.position = Position.Absolute;
            _fill.style.left = _fill.style.right = _fill.style.bottom = 0f;
            _fill.style.height = Length.Percent(0f);
            _fill.style.backgroundColor = Yellow;
            _track.Add(_fill);
            _touch.Add(_track);
            _powerCaption = Text("", 22f, Color.white, true);
            _powerCaption.style.position = Position.Absolute;
            _powerCaption.style.right = 40f;
            _powerCaption.style.width = 112f;
            _powerCaption.style.bottom = 24f;
            _powerCaption.style.unityTextAlign = TextAnchor.MiddleCenter;
            _powerCaption.style.unityTextOutlineColor = Ink;
            _powerCaption.style.unityTextOutlineWidth = 1.5f;
            _touch.Add(_powerCaption);
            _track.RegisterCallback<PointerDownEvent>(OnThrottleDown);
            _track.RegisterCallback<PointerMoveEvent>(OnThrottleMove);
            _track.RegisterCallback<PointerUpEvent>(e => ReleaseThrottle(e.pointerId));
            _track.RegisterCallback<PointerCancelEvent>(e => ReleaseThrottle(e.pointerId));
            _track.RegisterCallback<PointerCaptureOutEvent>(e => ReleaseThrottle(_throttlePointer));

            // Buttons: brake and flaps beside the throttle, rudder beside the stick, View / Get out under the top bar.
            _brake = Pill(0, "flight.hud.brake", Red, RedDark, Color.white);
            Place(_brake, StyleKeyword.Auto, 176f, 70f, StyleKeyword.Auto);
            _flapsDown = Pill(1, "flight.hud.flaps_down", Cream, CreamEdge, Ink);
            Place(_flapsDown, StyleKeyword.Auto, 176f, 170f, StyleKeyword.Auto);
            _flapsUp = Pill(2, "flight.hud.flaps_up", Cream, CreamEdge, Ink);
            Place(_flapsUp, StyleKeyword.Auto, 176f, 262f, StyleKeyword.Auto);
            _rudderLeft = Pill(3, "flight.hud.left", Cream, CreamEdge, Ink);
            Place(_rudderLeft, Length.Percent(37f), StyleKeyword.Auto, 40f, StyleKeyword.Auto);
            _rudderRight = Pill(4, "flight.hud.right", Cream, CreamEdge, Ink);
            Place(_rudderRight, Length.Percent(37f), StyleKeyword.Auto, 40f, StyleKeyword.Auto);
            _rudderRight.style.marginLeft = 170f;
            _view = Pill(5, "flight.hud.view", Cyan, CyanDark, Color.white);
            Place(_view, StyleKeyword.Auto, 210f, StyleKeyword.Auto, Length.Percent(13f));
            _exit = Pill(6, "flight.hud.get_out", Yellow, YellowDark, Ink);
            Place(_exit, StyleKeyword.Auto, 36f, StyleKeyword.Auto, Length.Percent(13f));
            Hold(_brake, 0);
            Hold(_rudderLeft, 1);
            Hold(_rudderRight, 2);
            Tap(_flapsDown, 0);
            Tap(_flapsUp, 1);
            Tap(_view, 2);
            Tap(_exit, 3);
            ApplyText();
        }

        /// <summary>True while the HUD shows (the explorer is flying).</summary>
        public bool Visible
        {
            get { return _visible; }
        }

        /// <summary>The overlay's root (tests).</summary>
        public VisualElement Root
        {
            get { return _root; }
        }

        /// <summary>Shows the HUD for <paramref name="craft"/> (the how-to hint starts) or hides it and lets go of every
        /// control; the explorer's speedometer is hidden while it shows.</summary>
        public void Show(bool show, FlightCraft craft = FlightCraft.Turboprop)
        {
            if (show)
            {
                FlightSpec spec = FlightSpec.For(craft);
                _rotorcraft = spec.Rotorcraft;
                _fixedGear = spec.FixedGear;
                _hintLeft = HintS;
                _hintState = -1;
                _bannerState = -1;
                _rotateKt = -1;
                for (int i = 0; i < _shown.Length; i++) _shown[i] = int.MinValue;
                _cells[6].style.display = _rotorcraft ? DisplayStyle.None : DisplayStyle.Flex;
                _cells[5].style.display = _rotorcraft ? DisplayStyle.None : DisplayStyle.Flex;
                Display(_flapsDown, !_rotorcraft);
                Display(_flapsUp, !_rotorcraft);
                _buttonKeys[7] = _rotorcraft ? "flight.hud.collective" : "flight.hud.power";
                _powerCaption.text = _localizer.Get(_buttonKeys[7]);
            }
            if (show == _visible) return;
            _visible = show;
            _root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (_speedo != null) _speedo.style.display = show ? new StyleEnum<DisplayStyle>(DisplayStyle.None) : new StyleEnum<DisplayStyle>(StyleKeyword.Null);
            if (!show) ReleaseAll();
        }

        /// <summary>Shows or hides the touch controls (a finger or the keyboard took over); hiding lets go of them.</summary>
        public void SetTouchVisible(bool visible)
        {
            if (visible == _touchVisible) return;
            _touchVisible = visible;
            _touch.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) ReleaseAll();
            _hintState = -1;
        }

        /// <summary>This frame's touch controls; the taps reset. <see cref="FlightTouch.Throttle"/> is negative while the
        /// slider is not touched (the lever keeps its place).</summary>
        public FlightTouch Read()
        {
            var t = new FlightTouch
            {
                StickX = _stickX, StickY = _stickY, Throttle = _throttlePointer != NoPointer ? _throttle : -1f,
                Rudder = (_rightPointer != NoPointer ? 1f : 0f) - (_leftPointer != NoPointer ? 1f : 0f), Brake = _brakePointer != NoPointer ? 1f : 0f,
                FlapsDown = _flapsDownTap, FlapsUp = _flapsUpTap, View = _viewTap, Exit = _exitTap, Active = _stickPointer != NoPointer, Touched = _touched,
            };
            _flapsDownTap = _flapsUpTap = _viewTap = _exitTap = _touched = false;
            return t;
        }

        /// <summary>The instruments, banner and hint for this frame, and the throttle slider at the lever's place.</summary>
        public void UpdateHud(in FlightInstruments ins, float lever, float dt)
        {
            if (!_visible) return;
            if (!ReferenceEquals(_locale, _localizer.Locale) || _devanagari != _localizer.UsesDevanagariDigits)
            {
                _locale = _localizer.Locale;
                _devanagari = _localizer.UsesDevanagariDigits;
                ApplyText();
                for (int i = 0; i < _shown.Length; i++) _shown[i] = int.MinValue;
                _hintState = _bannerState = -1;
                _rotateKt = -1;
            }
            Set(0, Mathf.RoundToInt(Mathf.Max(0f, ins.AirspeedKt)), false);
            Set(1, Mathf.RoundToInt(Mathf.Max(0f, ins.AglM) * 3.28084f / 10f) * 10, false);
            Set(2, Mathf.RoundToInt(ins.VerticalSpeedMps * 196.85f / 50f) * 50, true);
            int heading = Mathf.RoundToInt(Mathf.Repeat(ins.HeadingDeg, 360f));
            Set(3, heading == 0 ? 360 : heading, false);
            Set(4, Mathf.RoundToInt(Mathf.Clamp01(ins.Power) * 100f), false);
            Set(5, ins.Flaps, false);
            int gear = _fixedGear ? 2 : ins.GearDown > 0.5f ? 1 : 0;
            if (gear != _shown[6])
            {
                _shown[6] = gear;
                _values[6].text = _localizer.Get(gear == 0 ? "flight.hud.gear_up" : "flight.hud.gear_down");
            }

            float h = Mathf.Clamp01(_throttlePointer != NoPointer ? _throttle : lever) * 100f;
            _fill.style.height = Length.Percent(h);

            // Banner: stall first, then turning back.
            int banner = ins.Stalled || ins.StallWarning ? 1 : ins.TurningBack ? 2 : 0;
            if (banner != _bannerState)
            {
                _bannerState = banner;
                _banner.style.display = banner == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                if (banner != 0)
                {
                    _banner.style.backgroundColor = banner == 1 ? Red : Cyan;
                    Border(_banner, banner == 1 ? RedDark : CyanDark, 5f);
                    _bannerLabel.text = _localizer.Get(banner == 1 ? "flight.hud.stall" : "flight.hud.turn_back");
                }
            }
            if (banner == 1)
            {
                _flash += dt * 5f;
                _banner.style.opacity = 0.75f + 0.25f * Mathf.Sin(_flash);
            }

            // Hint: how to fly for a while after boarding, then how to take off while rolling, else nothing.
            _hintLeft -= dt;
            int hint = _hintLeft > 0f ? (_touchVisible ? 1 : 2) : 0;
            if (hint == 0 && !_rotorcraft && ins.OnGround && ins.AirspeedKt > 15f && ins.AirspeedKt < ins.RotateKt) hint = 3;
            if (hint != _hintState)
            {
                _hintState = hint;
                _hintBubble.style.display = hint == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                if (hint == 1) _hint.text = _localizer.Get(_rotorcraft ? "flight.hud.touch_keys_heli" : "flight.hud.touch_keys");
                else if (hint == 2) _hint.text = _localizer.Get(_rotorcraft ? "flight.hud.keys_heli" : "flight.hud.keys");
                else if (hint == 3)
                {
                    int kt = Mathf.RoundToInt(ins.RotateKt);
                    if (kt != _rotateKt || _rotateText == null)
                    {
                        _rotateKt = kt;
                        _rotateText = _localizer.Format("flight.hud.rotate", HudFormat.Number(kt, _devanagari));
                    }
                    _hint.text = _rotateText;
                }
            }
        }

        /// <summary>Lets go of the stick, the slider and the held buttons.</summary>
        public void ReleaseAll()
        {
            ReleaseStick();
            ReleaseThrottle(_throttlePointer);
            Release(_brake, ref _brakePointer);
            Release(_rudderLeft, ref _leftPointer);
            Release(_rudderRight, ref _rightPointer);
            _flapsDownTap = _flapsUpTap = _viewTap = _exitTap = false;
        }

        public void Dispose()
        {
            ReleaseAll();
            if (_speedo != null) _speedo.style.display = StyleKeyword.Null;
            _root.RemoveFromHierarchy();
        }

        // ----------------------------------------------------------------------------------------------- text

        private void ApplyText()
        {
            for (int i = 0; i < _captions.Length; i++) _captions[i].text = _localizer.Get(CaptionKeys[i]);
            for (int i = 0; i < 7; i++)
                if (_buttonKeys[i] != null) _buttonLabels[i].text = _localizer.Get(_buttonKeys[i]);
            if (_buttonKeys[7] != null) _powerCaption.text = _localizer.Get(_buttonKeys[7]);
        }

        private void Set(int i, int value, bool signed)
        {
            if (value == _shown[i]) return;
            _shown[i] = value;
            string s = HudFormat.Number(value, _devanagari);
            _values[i].text = signed && value > 0 ? "+" + s : s;
        }

        // ----------------------------------------------------------------------------------------------- stick

        private void OnStickDown(PointerDownEvent e)
        {
            if (_stickPointer != NoPointer) return;
            _touched |= e.pointerType != UnityEngine.UIElements.PointerType.mouse;
            _stickPointer = e.pointerId;
            _stickOrigin = e.localPosition;
            _stickZone.CapturePointer(e.pointerId);
            Rect rest = _stick.layout;
            if (!float.IsNaN(rest.width))
                _stick.style.translate = new Translate(_stickOrigin.x - rest.center.x, _stickOrigin.y - rest.center.y);
            _knob.style.translate = new Translate(0f, 0f);
            if (_haptics != null) _haptics.Play(HapticKind.Selection);
            e.StopPropagation();
        }

        private void OnStickMove(PointerMoveEvent e)
        {
            if (e.pointerId != _stickPointer) return;
            Vector2 d = (Vector2)e.localPosition - _stickOrigin;
            float kx, ky;
            TouchMath.Stick(d.x, d.y, out _stickX, out _stickY, out kx, out ky);
            _knob.style.translate = new Translate(kx, ky);
        }

        private void OnStickUp(PointerUpEvent e)
        {
            if (e.pointerId == _stickPointer) ReleaseStick();
        }

        private void OnStickCancel(PointerCancelEvent e)
        {
            if (e.pointerId == _stickPointer) ReleaseStick();
        }

        private void OnStickCaptureOut(PointerCaptureOutEvent e)
        {
            if (_stickPointer != NoPointer) ReleaseStick();
        }

        private void ReleaseStick()
        {
            if (_stickPointer == NoPointer) return;
            int p = _stickPointer;
            _stickPointer = NoPointer;
            _stickX = _stickY = 0f;
            if (_stickZone.HasPointerCapture(p)) _stickZone.ReleasePointer(p);
            _stick.style.translate = new Translate(0f, 0f);
            _knob.style.translate = new Translate(0f, 0f);
        }

        // -------------------------------------------------------------------------------------------- throttle

        private void OnThrottleDown(PointerDownEvent e)
        {
            if (_throttlePointer != NoPointer) return;
            _touched |= e.pointerType != UnityEngine.UIElements.PointerType.mouse;
            _throttlePointer = e.pointerId;
            _track.CapturePointer(e.pointerId);
            SetThrottle(e.localPosition.y);
            if (_haptics != null) _haptics.Play(HapticKind.Selection);
            e.StopPropagation();
        }

        private void OnThrottleMove(PointerMoveEvent e)
        {
            if (e.pointerId == _throttlePointer) SetThrottle(e.localPosition.y);
        }

        private void SetThrottle(float y)
        {
            float h = _track.layout.height;
            if (float.IsNaN(h) || h < 1f) return;
            _throttle = Mathf.Clamp01(1f - y / h);
        }

        private void ReleaseThrottle(int pointerId)
        {
            if (_throttlePointer == NoPointer || pointerId != _throttlePointer) return;
            _throttlePointer = NoPointer;
            if (_track.HasPointerCapture(pointerId)) _track.ReleasePointer(pointerId);
        }

        // --------------------------------------------------------------------------------------------- buttons

        private void Hold(VisualElement b, int which)
        {
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                _touched |= e.pointerType != UnityEngine.UIElements.PointerType.mouse;
                if (which == 0) _brakePointer = e.pointerId;
                else if (which == 1) _leftPointer = e.pointerId;
                else _rightPointer = e.pointerId;
                if (!b.HasPointerCapture(e.pointerId)) b.CapturePointer(e.pointerId);
                b.style.scale = new Scale(new Vector3(0.92f, 0.92f, 1f));
                if (_haptics != null) _haptics.Play(HapticKind.LightImpact);
                e.StopPropagation();
            });
            EventCallback<PointerUpEvent> up = e => Up(b, which, e.pointerId);
            b.RegisterCallback(up);
            b.RegisterCallback<PointerCancelEvent>(e => Up(b, which, e.pointerId));
            b.RegisterCallback<PointerCaptureOutEvent>(e => Up(b, which, which == 0 ? _brakePointer : which == 1 ? _leftPointer : _rightPointer));
        }

        private void Up(VisualElement b, int which, int pointerId)
        {
            if (which == 0) Release(b, ref _brakePointer, pointerId);
            else if (which == 1) Release(b, ref _leftPointer, pointerId);
            else Release(b, ref _rightPointer, pointerId);
        }

        private static void Release(VisualElement b, ref int pointer, int pointerId)
        {
            if (pointer == NoPointer || pointer != pointerId) return;
            Release(b, ref pointer);
        }

        private static void Release(VisualElement b, ref int pointer)
        {
            if (pointer == NoPointer) return;
            int p = pointer;
            pointer = NoPointer;
            if (b.HasPointerCapture(p)) b.ReleasePointer(p);
            b.style.scale = StyleKeyword.Null;
        }

        private void Tap(VisualElement b, int which)
        {
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                _touched |= e.pointerType != UnityEngine.UIElements.PointerType.mouse;
                if (which == 0) _flapsDownTap = true;
                else if (which == 1) _flapsUpTap = true;
                else if (which == 2) _viewTap = true;
                else _exitTap = true;
                if (_haptics != null) _haptics.Play(HapticKind.Selection);
                e.StopPropagation();
            });
        }

        // ------------------------------------------------------------------------------------------- building

        private VisualElement Pill(int index, string key, Color fill, Color edge, Color ink)
        {
            var b = Chip(fill, edge, 34f);
            b.name = "flight-btn-" + index;
            b.pickingMode = PickingMode.Position;
            b.style.position = Position.Absolute;
            b.style.minWidth = 150f;
            b.style.height = 76f;
            b.style.justifyContent = Justify.Center;
            b.style.alignItems = Align.Center;
            b.style.paddingLeft = b.style.paddingRight = 18f;
            Label l = Text("", 26f, ink, true);
            b.Add(l);
            _buttonLabels[index] = l;
            _buttonKeys[index] = key;
            _touch.Add(b);
            return b;
        }

        private static void Place(VisualElement e, StyleLength left, StyleLength right, StyleLength bottom, StyleLength top)
        {
            e.style.left = left;
            e.style.right = right;
            e.style.bottom = bottom;
            e.style.top = top;
        }

        private static VisualElement Chip(Color fill, Color edge, float radius)
        {
            var c = new VisualElement { pickingMode = PickingMode.Ignore };
            c.style.backgroundColor = fill;
            Round(c, radius);
            Border(c, edge, 5f);
            return c;
        }

        private static VisualElement Row(StyleLength top)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.position = Position.Absolute;
            r.style.left = 0f;
            r.style.right = 0f;
            r.style.top = top;
            r.style.flexDirection = FlexDirection.Row;
            r.style.justifyContent = Justify.Center;
            return r;
        }

        private static Label Text(string text, float size, Color color, bool bold)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0f;
            l.style.paddingTop = l.style.paddingBottom = 0f;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        private static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = e.style.top = e.style.right = e.style.bottom = 0f;
        }

        private static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        private static void Border(VisualElement e, Color c, float w)
        {
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
        }

        private static void Display(VisualElement e, bool on)
        {
            e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
