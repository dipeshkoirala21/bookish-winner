#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using UnityEngine;
#if GHUMANTE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Ghumante.World.Debugging
{
    /// <summary>
    /// Development-only free-fly camera for inspecting streaming (ARCHITECTURE.md 7.13). Keyboard: WASD to move, Q/E
    /// down/up, Shift to go 6x faster, mouse wheel to change speed; drag with the left or right mouse button to look.
    /// Touch (and the Device Simulator): drag one finger to look, pinch to fly forward or back, drag two fingers to
    /// slide. Speed grows with height above the ground (40 m/s at 50 m by default), and the camera never goes below
    /// the ground. It streams the world around itself: it sets <see cref="WorldRoot.Focus"/>, or
    /// <see cref="WorldRoot.FocusOverride"/> when it is a debug overlay over gameplay (<see cref="Toggle"/>, F3), and
    /// follows floating-origin shifts.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ghumante/Debug/Free-Fly Camera")]
    public sealed class FreeFlyCamera : MonoBehaviour
    {
        [SerializeField] private WorldRoot world;

        [Tooltip("Speed in m/s at 50 m above the ground; scales with height.")]
        [SerializeField] private float speed = 40f;

        [SerializeField] private float boost = 6f;

        [Tooltip("Degrees of rotation per pixel dragged.")]
        [SerializeField] private float lookDegreesPerPixel = 0.15f;

        [Tooltip("Minimum height above the ground.")]
        [SerializeField] private float clearanceM = 1.5f;

        [Tooltip("Stream through WorldRoot.FocusOverride (debug overlay) instead of WorldRoot.Focus.")]
        [SerializeField] private bool overrideFocus;

        private float _yaw, _pitch, _lastPinch = -1f;
        private WorldRoot _subscribed;
        private static FreeFlyCamera _overlay;
        private static Camera _hiddenCamera;

        public WorldRoot World
        {
            get { return world != null ? world : WorldRoot.Active; }
            set { world = value; }
        }

        public float Speed
        {
            get { return speed; }
            set { speed = Mathf.Clamp(value, 1f, 5000f); }
        }

        public bool OverrideFocus
        {
            get { return overrideFocus; }
            set { overrideFocus = value; }
        }

        /// <summary>True while the debug overlay camera of <see cref="Toggle"/> is active.</summary>
        public static bool OverlayActive
        {
            get { return _overlay != null; }
        }

        /// <summary>
        /// Debug overlay: take over from the main camera with a free-fly camera that streams around itself, or give
        /// control back. Gameplay keeps running with its focus; only streaming follows the free-fly camera.
        /// </summary>
        public static void Toggle(WorldRoot world)
        {
            if (_overlay != null)
            {
                WorldRoot w = _overlay.World;
                if (w != null) w.FocusOverride = null;
                Destroy(_overlay.gameObject);
                _overlay = null;
                if (_hiddenCamera != null) _hiddenCamera.enabled = true;
                _hiddenCamera = null;
                return;
            }
            if (world == null || !world.IsOpen) return;
            Camera main = Camera.main;
            var go = new GameObject("Debug Free-Fly Camera");
            Camera cam = go.AddComponent<Camera>();
            if (main != null)
            {
                cam.CopyFrom(main);
                go.transform.SetPositionAndRotation(main.transform.position + Vector3.up * 30f, main.transform.rotation);
                main.enabled = false;
                _hiddenCamera = main;
            }
            else
            {
                WorldRoot.ConfigureCamera(cam, world.Config);
                go.transform.position = world.ToScene(world.Focus) + Vector3.up * 80f;
            }
            _overlay = go.AddComponent<FreeFlyCamera>();
            _overlay.world = world;
            _overlay.overrideFocus = true;
        }

        /// <summary>
        /// Close the debug overlay if it streams <paramref name="world"/> (or any world when null): the hidden main
        /// camera is enabled again and the overlay camera destroyed. <see cref="WorldRoot.Close"/> calls it, so leaving
        /// Explore with the overlay on never leaves the menu drawn by a stale camera.
        /// </summary>
        public static void CloseOverlay(WorldRoot world)
        {
            if (_overlay == null) return;
            if (world != null && _overlay.world != null && _overlay.world != world) return;
            Toggle(world);
        }

        private void OnEnable()
        {
            Vector3 e = transform.rotation.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        private void OnDisable()
        {
            Subscribe(null);
            if (overrideFocus && World != null) World.FocusOverride = null;
        }

        private void OnDestroy()
        {
            if (_overlay == this)
            {
                _overlay = null;
                if (_hiddenCamera != null) _hiddenCamera.enabled = true;
                _hiddenCamera = null;
            }
        }

        private void Subscribe(WorldRoot w)
        {
            if (_subscribed == w) return;
            if (_subscribed != null) _subscribed.OriginShifted -= OnOriginShifted;
            _subscribed = w;
            if (_subscribed != null) _subscribed.OriginShifted += OnOriginShifted;
        }

        private void OnOriginShifted(WorldPos delta)
        {
            transform.position -= new Vector3((float)delta.X, delta.Y, (float)delta.Z);
        }

        private void Update()
        {
            WorldRoot w = World;
            if (_overlay == this && (w == null || !w.IsOpen))
            {
                // The world closed under the overlay (for example a scene change): give the main camera back.
                Toggle(null);
                return;
            }
            Subscribe(w);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Vector3 move = Vector3.zero;
            Vector2 look = Vector2.zero;
            float speedScale = 1f;
#if GHUMANTE_INPUT_SYSTEM
            bool touching = ReadTouch(ref move, ref look);
            if (!touching) ReadMouse(ref look);
            ReadKeyboard(ref move, ref speedScale);
#endif
            _yaw += look.x * lookDegreesPerPixel;
            _pitch = Mathf.Clamp(_pitch - look.y * lookDegreesPerPixel, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Vector3 p = transform.position;
            float ground;
            bool hasGround = TryGround(w, p, out ground);
            float height = hasGround ? Mathf.Max(0f, p.y - ground) : 50f;
            float v = speed * Mathf.Clamp(height / 50f, 0.25f, 80f) * speedScale;
            Vector3 flat = transform.rotation * new Vector3(move.x, 0f, move.z);
            p += (flat + Vector3.up * move.y) * (v * dt);
            if (TryGround(w, p, out ground) && p.y < ground + clearanceM) p.y = ground + clearanceM;
            transform.position = p;

            if (w != null && w.IsOpen)
            {
                WorldPos at = w.ToWorld(p);
                if (overrideFocus) w.FocusOverride = at;
                else w.Focus = at;
            }
        }

        private static bool TryGround(WorldRoot w, Vector3 scene, out float height)
        {
            height = 0f;
            if (w == null || !w.IsOpen || w.Ground == null) return false;
            WorldPos at = w.ToWorld(scene);
            GroundSample s;
            if (!w.Ground.TrySample(at.X, at.Z, out s)) return false;
            height = s.Height - w.Origin.Y;
            return true;
        }

#if GHUMANTE_INPUT_SYSTEM
        private bool ReadTouch(ref Vector3 move, ref Vector2 look)
        {
            Touchscreen ts = Touchscreen.current;
            if (ts == null) return false;
            TouchControl a = null, b = null;
            int active = 0;
            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                TouchControl t = touches[i];
                if (!t.isInProgress) continue;
                if (active == 0) a = t;
                else if (active == 1) b = t;
                active++;
            }
            if (active == 0)
            {
                _lastPinch = -1f;
                return false;
            }
            if (active == 1)
            {
                _lastPinch = -1f;
                look += a.delta.ReadValue();
                return true;
            }
            Vector2 pa = a.position.ReadValue(), pb = b.position.ReadValue();
            float pinch = Vector2.Distance(pa, pb);
            if (_lastPinch > 0f) move.z += Mathf.Clamp((pinch - _lastPinch) * 0.25f, -8f, 8f);
            _lastPinch = pinch;
            Vector2 pan = (a.delta.ReadValue() + b.delta.ReadValue()) * 0.5f;
            move.x -= Mathf.Clamp(pan.x * 0.1f, -4f, 4f);
            move.y -= Mathf.Clamp(pan.y * 0.1f, -4f, 4f);
            return true;
        }

        private void ReadMouse(ref Vector2 look)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.isPressed || mouse.leftButton.isPressed) look += mouse.delta.ReadValue();
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.01f) Speed = speed * 1.15f;
            else if (scroll < -0.01f) Speed = speed / 1.15f;
        }

        private void ReadKeyboard(ref Vector3 move, ref float speedScale)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.wKey.isPressed) move.z += 1f;
            if (kb.sKey.isPressed) move.z -= 1f;
            if (kb.dKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed) move.x -= 1f;
            if (kb.eKey.isPressed) move.y += 1f;
            if (kb.qKey.isPressed) move.y -= 1f;
            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) speedScale = boost;
        }
#endif
    }
}
#endif
