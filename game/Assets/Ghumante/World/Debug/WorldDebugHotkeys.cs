#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
#if GHUMANTE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ghumante.World.Debugging
{
    /// <summary>
    /// Development-only world shortcuts, added by <see cref="WorldRoot"/> in the editor and development builds
    /// (ARCHITECTURE.md 7.13): <b>F3</b> (or a three-finger tap) toggles the free-fly debug camera over gameplay;
    /// <b>T</b> toggles fast time (one game hour every 2 seconds); <b>[</b> / <b>]</b> step the clock one hour;
    /// <b>P</b> pauses the clock. Only while the world is open.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class WorldDebugHotkeys : MonoBehaviour
    {
        /// <summary>Time scale of fast time: 48-minute days become 48-second days.</summary>
        public const float FastTimeScale = 60f;

        private WorldRoot _world;
        private bool _threeFingers;

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
                if (kb.f3Key.wasPressedThisFrame) ToggleFreeFly();
                if (kb.tKey.wasPressedThisFrame)
                    _world.Sky.TimeScale = _world.Sky.TimeScale > 1.5f ? 1f : FastTimeScale;
                if (kb.pKey.wasPressedThisFrame) _world.Sky.Paused = !_world.Sky.Paused;
                if (kb.leftBracketKey.wasPressedThisFrame) _world.TimeOfDayHours -= 1f;
                if (kb.rightBracketKey.wasPressedThisFrame) _world.TimeOfDayHours += 1f;
            }
            Touchscreen ts = Touchscreen.current;
            if (ts != null)
            {
                int active = 0;
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                    if (touches[i].isInProgress) active++;
                if (active >= 3 && !_threeFingers) ToggleFreeFly();
                _threeFingers = active >= 3;
            }
#endif
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
