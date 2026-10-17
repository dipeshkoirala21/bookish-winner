using System;
using Ghumante.Core.Driving;
using UnityEngine;
#if GHUMANTE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ghumante.Characters.Rides
{
    /// <summary>
    /// The pilot's controls (docs/research/w2/ref_flight.md §5) from the merged <see cref="ControlFrame"/> (WASD and the
    /// gamepad's left stick, Space, C), the keys and pads the explore controls do not map (Q / E rudder, Shift / Ctrl
    /// throttle, F / V flaps; the gamepad's triggers move the throttle lever and its shoulders work the rudder) and the
    /// aircraft HUD's touch controls. Keeps the throttle lever's position (it stays where it was put).
    /// <code>
    ///               keyboard            gamepad              touch
    /// pitch          W / S (S pulls up)  left stick y         stick up / down
    /// roll           A / D               left stick x         stick left / right
    /// rudder, steer  Q / E               shoulders            rudder buttons (the stick steers on the ground)
    /// throttle       Shift / Ctrl        right / left trigger the slider
    /// flaps          F down, V up        D-pad up / down      Flaps buttons
    /// brakes         Space               B                    Brake button
    /// view           C                   right-stick press    View button
    /// get out        hold E at a stop    hold A at a stop     Get out button
    /// </code>
    /// </summary>
    public sealed class AircraftInput : IDisposable
    {
        /// <summary>Throttle lever travel per second while Shift / Ctrl or a trigger is held.</summary>
        public const float LeverRatePerS = 0.5f;

        /// <summary>Pull the stick back for nose up: the pitch input is −stick y (true) or +stick y (false).</summary>
        public bool PushForNoseDown = true;

        private float _lever;
        private bool _disposed;
#if GHUMANTE_INPUT_SYSTEM
        // The pilot's own action map (the explore map has no rudder, lever or flaps): bindings through the Input System's
        // control paths, enabled only while flying (Enabled).
        private readonly InputActionMap _map;
        private readonly InputAction _rudder, _rudderPad, _lever1, _lever2, _leverPad, _flapsDown, _flapsUp;
#endif

        public AircraftInput()
        {
#if GHUMANTE_INPUT_SYSTEM
            _map = new InputActionMap("Aircraft");
            _rudder = Axis("Rudder", "<Keyboard>/q", "<Keyboard>/e");
            _rudderPad = Axis("RudderPad", "<Gamepad>/leftShoulder", "<Gamepad>/rightShoulder");
            _lever1 = Axis("Lever", "<Keyboard>/leftCtrl", "<Keyboard>/leftShift");
            _lever2 = Axis("LeverRight", "<Keyboard>/rightCtrl", "<Keyboard>/rightShift");
            _leverPad = Axis("LeverPad", "<Gamepad>/leftTrigger", "<Gamepad>/rightTrigger");
            _flapsDown = _map.AddAction("FlapsDown", InputActionType.Button);
            _flapsDown.AddBinding("<Keyboard>/f");
            _flapsDown.AddBinding("<Gamepad>/dpad/up");
            _flapsUp = _map.AddAction("FlapsUp", InputActionType.Button);
            _flapsUp.AddBinding("<Keyboard>/v");
            _flapsUp.AddBinding("<Gamepad>/dpad/down");
#endif
        }

        /// <summary>The throttle lever (0–1).</summary>
        public float Lever
        {
            get { return _lever; }
            set { _lever = Mathf.Clamp01(float.IsNaN(value) ? 0f : value); }
        }

        /// <summary>True while the pilot's keys and pad buttons are read (the ride enables them while flying).</summary>
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
                if (_disposed) return;
#if GHUMANTE_INPUT_SYSTEM
                if (value == _map.enabled) return;
                if (value) _map.Enable();
                else _map.Disable();
#endif
            }
        }

        /// <summary>The flight input for this frame; <paramref name="view"/> and <paramref name="exitTap"/> are the
        /// one-shot taps of View (C, right-stick press, the HUD's View) and the HUD's Get out.</summary>
        public FlightInput Read(float dt, in ControlFrame frame, in FlightTouch touch, out bool view, out bool exitTap)
        {
            float stickX, stickY;
            ControlMapper.ApplyDeadZone(frame.MoveX, frame.MoveY, ControlMapper.StickDeadZone, out stickX, out stickY);
            if (touch.Active)
            {
                stickX = Strong(stickX, touch.StickX);
                stickY = Strong(stickY, touch.StickY);
            }
            float yaw = touch.Rudder;
            float throttleRate = 0f;
            bool flapsDown = touch.FlapsDown, flapsUp = touch.FlapsUp;
            view = frame.CameraCycle || touch.View;
            exitTap = touch.Exit;
#if GHUMANTE_INPUT_SYSTEM
            if (!_disposed && _map.enabled)
            {
                yaw += _rudder.ReadValue<float>() + _rudderPad.ReadValue<float>();
                throttleRate += _lever1.ReadValue<float>() + _lever2.ReadValue<float>() + _leverPad.ReadValue<float>();
                if (_flapsDown.WasPressedThisFrame()) flapsDown = true;
                if (_flapsUp.WasPressedThisFrame()) flapsUp = true;
            }
#endif
            if (touch.Throttle >= 0f) _lever = Mathf.Clamp01(touch.Throttle);
            _lever = Mathf.Clamp01(_lever + Mathf.Clamp(throttleRate, -1f, 1f) * LeverRatePerS * dt);
            float pitch = PushForNoseDown ? -stickY : stickY;
            var input = new FlightInput(pitch, stickX, Mathf.Clamp(yaw, -1f, 1f), _lever, Mathf.Max(frame.Brake, touch.Brake))
            {
                FlapsDown = flapsDown,
                FlapsUp = flapsUp,
            };
            return input;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
#if GHUMANTE_INPUT_SYSTEM
            _map.Disable();
            _map.Dispose();
#endif
        }

        private static float Strong(float a, float b)
        {
            return Math.Abs(b) > Math.Abs(a) ? b : a;
        }

#if GHUMANTE_INPUT_SYSTEM
        /// <summary>A −1…+1 axis from two buttons (or triggers): <paramref name="negative"/> and <paramref name="positive"/>.</summary>
        private InputAction Axis(string name, string negative, string positive)
        {
            InputAction action = _map.AddAction(name, InputActionType.Value, expectedControlLayout: "Axis");
            action.AddCompositeBinding("1DAxis").With("Negative", negative).With("Positive", positive);
            return action;
        }
#endif
    }
}
