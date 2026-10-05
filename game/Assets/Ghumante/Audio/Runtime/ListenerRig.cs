using System;
using Ghumante.Core.Synth;
using UnityEngine;

namespace Ghumante.Audio
{
    /// <summary>
    /// The audio listener (W2_DESIGN 7.1 "Listener"): 35% of the way from the camera to the player's head, facing
    /// the camera's way, so device rotation (portrait / landscape) changes nothing. It carries the code-side
    /// snapshot effects: a reverb (Courtyard RT60 0.7–1.0 s, DurbarSquare 1.2 s with 60 ms pre-delay) and the galli
    /// slapback echo (delay 2w/343, feedback 0.15, wet −14 dB). 2D beds and UI bypass listener effects. While
    /// attached, the camera's own AudioListener is disabled (one listener per scene) and restored on detach.
    /// </summary>
    internal sealed class ListenerRig : IDisposable
    {
        private readonly GameObject _go;
        private readonly AudioListener _listener;
        private readonly AudioReverbFilter _reverb;
        private readonly AudioEchoFilter _echo;
        private Transform _camera;
        private Transform _head;
        private AudioListener _cameraListener;

        public ListenerRig(Transform parent)
        {
            _go = new GameObject("Listener");
            _go.transform.SetParent(parent, false);
            _listener = _go.AddComponent<AudioListener>();
            _reverb = _go.AddComponent<AudioReverbFilter>();
            _reverb.reverbPreset = AudioReverbPreset.User;
            _reverb.enabled = false;
            _echo = _go.AddComponent<AudioEchoFilter>();
            _echo.enabled = false;
            _listener.enabled = false;
        }

        public bool Attached
        {
            get { return _camera != null; }
        }

        /// <summary>Scene position of the listener this frame.</summary>
        public Vector3 Position { get; private set; }

        /// <summary>Listener velocity from the simulation (the player's vehicle or walker), for Doppler.</summary>
        public Vector3 Velocity { get; set; }

        public void Attach(Transform camera, Transform head)
        {
            Detach();
            if (camera == null) return;
            _camera = camera;
            _head = head;
            _cameraListener = camera.GetComponent<AudioListener>();
            if (_cameraListener != null && _cameraListener.enabled) _cameraListener.enabled = false;
            else _cameraListener = null;
            _listener.enabled = true;
            Update();
        }

        public void Detach()
        {
            if (_cameraListener != null) _cameraListener.enabled = true;
            _cameraListener = null;
            _camera = null;
            _head = null;
            _listener.enabled = false;
            _reverb.enabled = false;
            _echo.enabled = false;
        }

        /// <summary>Moves the listener; without an attached camera it follows <c>Camera.main</c>.</summary>
        public void Update()
        {
            Transform cam = _camera;
            if (cam == null)
            {
                Camera main = Camera.main;
                if (main != null) Position = main.transform.position;
                return;
            }
            Vector3 c = cam.position;
            Vector3 h = _head != null ? _head.position : c;
            SoundSpace.ListenerPoint(c.x, c.y, c.z, h.x, h.y, h.z, out float x, out float y, out float z);
            Position = new Vector3(x, y, z);
            _go.transform.SetPositionAndRotation(Position, cam.rotation);
        }

        /// <summary>Applies the blended snapshot's reverb and slapback (filters off when their wet level is −80 dB).</summary>
        public void Apply(in SnapshotParams p)
        {
            if (!Attached) return;
            bool rev = p.ReverbWetDb > -60f;
            if (_reverb.enabled != rev) _reverb.enabled = rev;
            if (rev)
            {
                _reverb.decayTime = Mathf.Clamp(p.ReverbRt60, 0.1f, 20f);
                _reverb.reverbLevel = Mathf.Clamp(p.ReverbWetDb * 100f, -10000f, 2000f);
                _reverb.reverbDelay = Mathf.Clamp(p.ReverbPreDelayMs / 1000f, 0f, 0.1f);
                _reverb.room = -1000f;
                _reverb.dryLevel = 0f;
            }
            bool echo = p.EchoWetDb > -60f && p.EchoDelayMs >= 10f;
            if (_echo.enabled != echo) _echo.enabled = echo;
            if (echo)
            {
                _echo.delay = Mathf.Clamp(p.EchoDelayMs, 10f, 5000f);
                _echo.decayRatio = Mathf.Clamp01(p.EchoFeedback);
                _echo.wetMix = Mathf.Clamp01(Dsp.DbToGain(p.EchoWetDb));
                _echo.dryMix = 1f;
            }
        }

        public void Dispose()
        {
            Detach();
            if (_go != null) UnityEngine.Object.Destroy(_go);
        }
    }
}
