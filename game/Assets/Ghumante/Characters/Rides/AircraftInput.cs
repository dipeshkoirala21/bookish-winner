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
    public sealed class AircraftInput
    {
        /// <summary>Throttle lever travel per second while Shift / Ctrl or a trigger is held.</summary>
        public const float LeverRatePerS = 0.5f;

        /// <summary>Pull the stick back for nose up: the pitch input is −stick y (true) or +stick y (false).</summary>
        public bool PushForNoseDown = true;

        private float _lever;

        /// <summary>The throttle lever (0–1).</summary>
        public float Lever
        {
            get { return _lever; }
            set { _lever = Mathf.Clamp01(float.IsNaN(value) ? 0f : value); }
        }

        /// <summary>The flight input for this frame; <paramref name="flapsDown"/> etc. are the one-shot taps.</summary>
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
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.qKey.isPressed) yaw -= 1f;
                if (k.eKey.isPressed) yaw += 1f;
                if (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed) throttleRate += 1f;
                if (k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed) throttleRate -= 1f;
                if (k.fKey.wasPressedThisFrame) flapsDown = true;
                if (k.vKey.wasPressedThisFrame) flapsUp = true;
            }
            Gamepad g = Gamepad.current;
            if (g != null)
            {
                throttleRate += g.rightTrigger.ReadValue() - g.leftTrigger.ReadValue();
                if (g.rightShoulder.isPressed) yaw += 1f;
                if (g.leftShoulder.isPressed) yaw -= 1f;
                if (g.dpad.up.wasPressedThisFrame) flapsDown = true;
                if (g.dpad.down.wasPressedThisFrame) flapsUp = true;
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

        private static float Strong(float a, float b)
        {
            return Math.Abs(b) > Math.Abs(a) ? b : a;
        }
    }
}
