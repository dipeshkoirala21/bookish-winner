#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UIElements;
#if GHUMANTE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ghumante.World.Debugging
{
    /// <summary>
    /// Development-only world shortcuts, added by <see cref="WorldRoot"/> in the editor and development builds
    /// (ARCHITECTURE.md 7.13): <b>F3</b> (or holding four fingers on the screen for <see cref="FourFingerHoldS"/>)
    /// toggles the free-fly debug camera over gameplay; <b>T</b> toggles fast time (one game hour every 2 seconds);
    /// <b>[</b> / <b>]</b> step the clock one hour; <b>P</b> pauses the clock. Only while the world is open, and never
    /// while a UI Toolkit text field has keyboard focus (typing "Thamel" in search must not change the clock).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class WorldDebugHotkeys : MonoBehaviour
    {
        /// <summary>Time scale of fast time: 48-minute days become 48-second days.</summary>
        public const float FastTimeScale = 60f;

        /// <summary>Seconds four fingers must stay down to toggle the free-fly camera (a deliberate gesture: three
        /// fingers happen in play, for example stick, throttle and a pinch).</summary>
        public const float FourFingerHoldS = 1f;

        private WorldRoot _world;
        private float _fourFingersSince = -1f;
        private bool _fourFingersFired;

        private void Awake()
        {
            _world = GetComponent<WorldRoot>();
        }

        private void Update()
        {
            if (_world == null || !_world.IsOpen) return;
#if GHUMANTE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                bool f3 = kb.f3Key.wasPressedThisFrame, t = kb.tKey.wasPressedThisFrame, p = kb.pKey.wasPressedThisFrame;
                bool earlier = kb.leftBracketKey.wasPressedThisFrame, later = kb.rightBracketKey.wasPressedThisFrame;
                if ((f3 || t || p || earlier || later) && !TextFieldFocused())
                {
                    if (f3) ToggleFreeFly();
                    if (t) _world.Sky.TimeScale = _world.Sky.TimeScale > 1.5f ? 1f : FastTimeScale;
                    if (p) _world.Sky.Paused = !_world.Sky.Paused;
                    if (earlier) _world.TimeOfDayHours -= 1f;
                    if (later) _world.TimeOfDayHours += 1f;
                }
            }
            Touchscreen ts = Touchscreen.current;
            if (ts != null)
            {
                int active = 0;
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                    if (touches[i].isInProgress) active++;
                if (active >= 4)
                {
                    float now = Time.unscaledTime;
                    if (_fourFingersSince < 0f) _fourFingersSince = now;
                    if (!_fourFingersFired && now - _fourFingersSince >= FourFingerHoldS)
                    {
                        _fourFingersFired = true;
                        ToggleFreeFly();
                    }
                }
                else
                {
                    _fourFingersSince = -1f;
                    _fourFingersFired = false;
                }
            }
#endif
        }

        /// <summary>True when a UI Toolkit text field of any UIDocument has keyboard focus (only checked on a key
        /// press, so the search for documents does not run every frame).</summary>
        private static bool TextFieldFocused()
        {
            UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (int i = 0; i < docs.Length; i++)
            {
                VisualElement root = docs[i].rootVisualElement;
                IPanel panel = root != null ? root.panel : null;
                if (panel == null || panel.focusController == null) continue;
                var focused = panel.focusController.focusedElement as VisualElement;
                if (focused != null && focused.GetFirstOfType<TextField>() != null) return true;
            }
            return false;
        }

        private void ToggleFreeFly()
        {
            // A scene that already flies the main camera (the World Preview) needs no overlay.
            FreeFlyCamera existing = FindAnyObjectByType<FreeFlyCamera>();
            if (existing != null && !FreeFlyCamera.OverlayActive)
            {
                Debug.Log("WorldDebugHotkeys: the camera is already free-flying.");
                return;
            }
            FreeFlyCamera.Toggle(_world);
            Debug.Log("WorldDebugHotkeys: free-fly camera " + (FreeFlyCamera.OverlayActive ? "on (F3 to return)" : "off"));
        }
    }
}
#endif
