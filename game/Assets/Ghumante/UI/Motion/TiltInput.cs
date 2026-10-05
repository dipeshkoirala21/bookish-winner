using System;
using UnityEngine;
using UnityEngine.UIElements;
#if GHUMANTE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ghumante.UI.Motion
{
    /// <summary>
    /// A smoothed "look" direction in [-1, 1]² for the parallax backdrop:
    /// <list type="bullet">
    /// <item>On phones, device tilt from the Input System's <c>Accelerometer</c> (or <c>GravitySensor</c>),
    /// which this class enables on the first <see cref="Update"/> and disables again (if it enabled it) on
    /// <see cref="Suspend"/> or <see cref="Dispose"/>, so the sensor only samples while the parallax runs.
    /// The Input System compensates for screen orientation, so x is always "towards the right of the
    /// screen". A slow re-centring filter makes whatever angle the phone is held at the neutral pose.</item>
    /// <item>With a mouse (editor Game view, desktop), the pointer position over the screen. In the editor any
    /// pointer counts, so dragging in the Device Simulator also moves the scene.</item>
    /// </list>
    /// Compiled with the sensor path when the Input System package is present (versionDefines in
    /// Ghumante.UI.asmdef define GHUMANTE_INPUT_SYSTEM); otherwise pointer only.
    /// </summary>
    public sealed class TiltInput : IDisposable
    {
        /// <summary>Tilt (in g) that maps to full deflection.</summary>
        private const float FullTilt = 0.32f;

        /// <summary>Seconds for the neutral pose to follow how the phone is held.</summary>
        private const float RecentreSeconds = 2.5f;

        /// <summary>Seconds of smoothing on the output.</summary>
        private const float SmoothSeconds = 0.14f;

        private readonly VisualElement _pointerArea;
        private Vector2 _value;
        private Vector2 _pointerTarget;
        private bool _hasPointer;
        private Vector2 _neutral;
        private bool _hasNeutral;
        private bool _disposed;
#if GHUMANTE_INPUT_SYSTEM
        private InputDevice _sensor;
        private bool _enabledSensor;
        private bool _sensorFailed;
#endif

        public TiltInput(VisualElement pointerArea)
        {
            _pointerArea = pointerArea ?? throw new ArgumentNullException(nameof(pointerArea));
            _pointerArea.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _pointerArea.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        /// <summary>Smoothed look direction: x right, y down, each in [-1, 1].</summary>
        public Vector2 Value
        {
            get { return _value; }
        }

        /// <summary>Advances the filters by <paramref name="dt"/> seconds.</summary>
        public void Update(float dt)
        {
            if (_disposed || !(dt > 0f)) return;
            Vector2 target = Vector2.zero;
            Vector2 raw;
            if (TryReadSensor(out raw))
            {
                if (!_hasNeutral)
                {
                    _neutral = raw;
                    _hasNeutral = true;
                }
                _neutral += (raw - _neutral) * (1f - Mathf.Exp(-dt / RecentreSeconds));
                Vector2 d = raw - _neutral;
                // Screen y grows downwards; tipping the top edge away from you (y acceleration falling)
                // should look up into the sky, which moves the scene down.
                target = new Vector2(Mathf.Clamp(d.x / FullTilt, -1f, 1f), Mathf.Clamp(-d.y / FullTilt, -1f, 1f));
            }
            else if (_hasPointer)
            {
                target = _pointerTarget;
            }
            _value += (target - _value) * (1f - Mathf.Exp(-dt / SmoothSeconds));
        }

        /// <summary>Snaps back to neutral and stops the sensor (Reduce motion switched on).</summary>
        public void Reset()
        {
            _value = Vector2.zero;
            Suspend();
        }

        /// <summary>
        /// Stops the sensor while nothing reads it (Reduce motion, app unfocused): the Input System would
        /// otherwise keep sampling it and queuing an event per sample. Disables the device only if this
        /// instance enabled it; the next <see cref="Update"/> enables it again and re-centres.
        /// </summary>
        public void Suspend()
        {
            _hasNeutral = false;
#if GHUMANTE_INPUT_SYSTEM
            if (_enabledSensor && _sensor != null && _sensor.added && _sensor.enabled)
            {
                InputSystem.DisableDevice(_sensor);
            }
            _sensor = null;
            _enabledSensor = false;
#endif
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pointerArea.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _pointerArea.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            Suspend();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
#if !UNITY_EDITOR
            if (evt.pointerType != UnityEngine.UIElements.PointerType.mouse) return;
#endif
            Rect r = _pointerArea.worldBound;
            if (!(r.width > 0f) || !(r.height > 0f)) return;
            Vector2 p = evt.position;
            _pointerTarget = new Vector2(
                Mathf.Clamp((p.x - r.center.x) / (r.width * 0.5f), -1f, 1f),
                Mathf.Clamp((p.y - r.center.y) / (r.height * 0.5f), -1f, 1f));
            _hasPointer = true;
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            _pointerTarget = Vector2.zero;
        }

        private bool TryReadSensor(out Vector2 reading)
        {
            reading = Vector2.zero;
#if GHUMANTE_INPUT_SYSTEM
            if (_sensorFailed) return false;
            try
            {
                if (_sensor == null || !_sensor.added)
                {
                    _sensor = null;
                    InputDevice candidate = Accelerometer.current;
                    if (candidate == null) candidate = GravitySensor.current;
                    if (candidate == null) return false;
                    if (!candidate.enabled)
                    {
                        InputSystem.EnableDevice(candidate);
                        _enabledSensor = true;
                    }
                    _sensor = candidate;
                }
                Vector3 g;
                var accelerometer = _sensor as Accelerometer;
                if (accelerometer != null)
                {
                    g = accelerometer.acceleration.ReadValue();
                }
                else
                {
                    var gravity = _sensor as GravitySensor;
                    if (gravity == null) return false;
                    g = gravity.gravity.ReadValue();
                }
                // A sensor that has not reported yet reads exactly zero.
                if (g.sqrMagnitude < 1e-6f) return false;
                reading = new Vector2(g.x, g.y);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("TiltInput: sensor unavailable, using the pointer only: " + e.Message);
                _sensor = null;
                _sensorFailed = true;
                return false;
            }
#else
            return false;
#endif
        }
    }
}
