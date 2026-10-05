using System;
using UnityEngine;
#if GHUMANTE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ghumante.Characters
{
    /// <summary>
    /// Keyboard, mouse and gamepad controls for Explore, as Input System actions created in code (no .inputactions
    /// asset to keep in sync). <see cref="Read"/> fills a <see cref="ControlFrame"/> each frame and reports which device
    /// was used, so the HUD can hide its touch controls when a keyboard or gamepad takes over.
    /// <code>
    ///                 keyboard                       gamepad
    /// steer / move    A D (or arrows), W S on foot   left stick
    /// throttle        W or up                        right trigger
    /// brake, reverse  S or down                      left trigger
    /// brake           Space                          B
    /// boost / sprint  Shift                          X
    /// hop on / off    E (hold: passenger, hail taxi) A (hold)
    /// jump            Space (on foot)                A (nothing to hop on)
    /// horn / bell     H                              left stick press
    /// namaste         N                              D-pad up
    /// garage whistle  G                              D-pad down
    /// stop (passenger) B                             D-pad right
    /// map (later)     M                              Select
    /// search          /                              Y
    /// pause           Esc (through the UI)           Start
    /// camera          scroll zooms, right-drag looks shoulders zoom, right stick looks
    /// </code>
    /// Escape and Android back are not bound here: UI Toolkit turns them into a cancel event that the Explore screen
    /// handles (close the top panel, else pause), so one key never acts twice.
    /// </summary>
    public sealed class ExplorerInput : IDisposable
    {
        /// <summary>Gamepad look speed at full stick, degrees per second (yaw, pitch).</summary>
        public const float StickLookYawDegPerS = 150f;
        public const float StickLookPitchDegPerS = 90f;

        /// <summary>Mouse look while the right button is held, degrees per pixel.</summary>
        public const float MouseLookDegPerPixel = 0.18f;

        /// <summary>Shoulder-button zoom, notches per second.</summary>
        public const float ShoulderZoomPerS = 3f;

#if GHUMANTE_INPUT_SYSTEM
        private readonly InputActionMap _map;
        private readonly InputActionMap _systemMap;
        private readonly InputAction _move;
        private readonly InputAction _throttle;
        private readonly InputAction _reverse;
        private readonly InputAction _brake;
        private readonly InputAction _boost;
        private readonly InputAction _toggle;
        private readonly InputAction _jump;
        private readonly InputAction _horn;
        private readonly InputAction _namaste;
        private readonly InputAction _whistle;
        private readonly InputAction _bell;
        private readonly InputAction _mapButton;
        private readonly InputAction _pause;
        private readonly InputAction _search;
        private readonly InputAction _zoomHold;
        private readonly InputAction _look;
        private readonly InputAction[] _deviceProbes;
#endif
        private bool _disposed;

        public ExplorerInput()
        {
#if GHUMANTE_INPUT_SYSTEM
            _map = new InputActionMap("Explore");
            _move = _map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                 .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                 .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _throttle = Axis("Throttle", "<Keyboard>/w", "<Keyboard>/upArrow", "<Gamepad>/rightTrigger");
            _reverse = Axis("Reverse", "<Keyboard>/s", "<Keyboard>/downArrow", "<Gamepad>/leftTrigger");
            _brake = Button("Brake", "<Keyboard>/space", "<Gamepad>/buttonEast");
            _boost = Button("Boost", "<Keyboard>/leftShift", "<Keyboard>/rightShift", "<Gamepad>/buttonWest");
            _toggle = Button("Action", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            _jump = Button("Jump", "<Keyboard>/space");
            _horn = Button("Horn", "<Keyboard>/h", "<Gamepad>/leftStickPress");
            _namaste = Button("Namaste", "<Keyboard>/n", "<Gamepad>/dpad/up");
            _whistle = Button("Whistle", "<Keyboard>/g", "<Gamepad>/dpad/down");
            _bell = Button("Bell", "<Keyboard>/b", "<Gamepad>/dpad/right");
            _mapButton = Button("Map", "<Keyboard>/m", "<Gamepad>/select");
            // Start lives in its own map that stays enabled while search or Settings own the keys, so it can still
            // close them (ExploreScreen.TogglePause).
            _systemMap = new InputActionMap("ExploreSystem");
            _pause = _systemMap.AddAction("Pause", InputActionType.Button, "<Gamepad>/start");
            _systemMap.Enable();
            _search = Button("Search", "<Keyboard>/slash", "<Gamepad>/buttonNorth");

            _zoomHold = _map.AddAction("ZoomHold", InputActionType.Value, expectedControlLayout: "Axis");
            _zoomHold.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/leftShoulder").With("Positive", "<Gamepad>/rightShoulder");
            _look = _map.AddAction("Look", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");

            _deviceProbes = new[] { _move, _throttle, _reverse, _brake, _boost, _toggle, _jump, _horn, _namaste, _whistle, _bell, _mapButton, _pause, _search, _zoomHold, _look };
#endif
        }

        /// <summary>True when a touchscreen is present (phones, tablets, the editor's Device Simulator): the HUD then
        /// starts with its touch controls showing.</summary>
        public static bool TouchscreenPresent
        {
            get
            {
#if GHUMANTE_INPUT_SYSTEM
                return Touchscreen.current != null;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        /// <summary>The device that produced the most recent input (None until something is pressed).</summary>
        public ControlDevice LastDevice { get; private set; }

        /// <summary>True while the actions listen. Disable them while a text field has the keyboard (search); gamepad
        /// Start is still read then.</summary>
        public bool Enabled
        {
            get
            {
#if GHUMANTE_INPUT_SYSTEM
                return !_disposed && _map.enabled;
#else
                return false;
#endif
            }
            set
            {
#if GHUMANTE_INPUT_SYSTEM
                if (_disposed || _map.enabled == value) return;
                if (value) _map.Enable();
                else _map.Disable();
#endif
            }
        }

        /// <summary>
        /// Writes this frame's keyboard and gamepad controls into <paramref name="frame"/> (sticks, pedals, buttons) and
        /// adds the camera deltas (mouse wheel and right-drag are read from the mouse directly: deltas, not states).
        /// </summary>
        public void Read(ref ControlFrame frame, float dt)
        {
#if GHUMANTE_INPUT_SYSTEM
            if (_disposed) return;
            if (_pause.WasPressedThisFrame())
            {
                frame.Pause = true;
                LastDevice = ControlDevice.Gamepad;
                frame.Device = ControlDevice.Gamepad;
            }
            if (!_map.enabled) return;
            Vector2 move = _move.ReadValue<Vector2>();
            frame.MoveX = move.x;
            frame.MoveY = move.y;
            frame.Throttle = Mathf.Clamp01(_throttle.ReadValue<float>());
            frame.Reverse = Mathf.Clamp01(_reverse.ReadValue<float>());
            frame.Brake = _brake.IsPressed() ? 1f : 0f;
            frame.Boost = _boost.IsPressed();
            frame.ToggleMode = _toggle.WasPressedThisFrame();
            frame.ActionHeld = _toggle.IsPressed();
            frame.Jump = _jump.WasPressedThisFrame();
            frame.Horn = _horn.IsPressed();
            frame.Namaste = _namaste.WasPressedThisFrame();
            frame.Whistle = _whistle.WasPressedThisFrame();
            frame.Bell = _bell.WasPressedThisFrame();
            frame.Map = _mapButton.WasPressedThisFrame();
            frame.Search = _search.WasPressedThisFrame();

            Mouse mouse = Mouse.current;
            float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            // Platforms that report raw wheel deltas use 120 per notch; normalised ones use 1.
            if (Mathf.Abs(scroll) > 10f) scroll /= 120f;
            float zoom = Mathf.Clamp(scroll, -3f, 3f) + _zoomHold.ReadValue<float>() * ShoulderZoomPerS * dt;
            frame.ZoomSteps += zoom;

            Vector2 stick = _look.ReadValue<Vector2>();
            float yaw = stick.x * StickLookYawDegPerS * dt;
            float pitch = -stick.y * StickLookPitchDegPerS * dt;
            bool mouseLooking = mouse != null && mouse.rightButton.isPressed;
            if (mouseLooking)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * MouseLookDegPerPixel;
                pitch -= delta.y * MouseLookDegPerPixel;
            }
            frame.LookYawDeg += yaw;
            frame.LookPitchDeg += pitch;
            frame.Looking |= mouseLooking || stick.sqrMagnitude > 0.04f;

            ControlDevice device = ControlDevice.None;
            for (int i = 0; i < _deviceProbes.Length && device == ControlDevice.None; i++)
            {
                InputControl control = _deviceProbes[i].activeControl;
                if (control != null) device = control.device is Gamepad ? ControlDevice.Gamepad : ControlDevice.KeyboardMouse;
            }
            if (device == ControlDevice.None && (scroll != 0f || mouseLooking)) device = ControlDevice.KeyboardMouse;
            if (device != ControlDevice.None)
            {
                LastDevice = device;
                frame.Device = device;
            }
#endif
        }

        /// <summary>Forgets which device was last used (a touch took over).</summary>
        public void NoteTouch()
        {
            LastDevice = ControlDevice.Touch;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
#if GHUMANTE_INPUT_SYSTEM
            _map.Disable();
            _map.Dispose();
            _systemMap.Disable();
            _systemMap.Dispose();
#endif
        }

#if GHUMANTE_INPUT_SYSTEM
        private InputAction Axis(string name, params string[] bindings)
        {
            InputAction action = _map.AddAction(name, InputActionType.Value, expectedControlLayout: "Axis");
            for (int i = 0; i < bindings.Length; i++) action.AddBinding(bindings[i]);
            return action;
        }

        private InputAction Button(string name, params string[] bindings)
        {
            InputAction action = _map.AddAction(name, InputActionType.Button);
            for (int i = 0; i < bindings.Length; i++) action.AddBinding(bindings[i]);
            return action;
        }
#endif
    }
}
