using Ghumante.Core.Services;
using Ghumante.Platform.Haptics;
using Ghumante.World;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;

namespace Ghumante.DebugTools
{
    /// <summary>
    /// FPS, frame-time and memory overlay (ARCHITECTURE.md 7.13). It also shows the last haptic request for
    /// about a second ("haptic: Success (device)", or "(editor - not felt)" in the editor), from
    /// <see cref="MobileHaptics.Requested"/>, so haptics can be seen firing where they cannot be felt: the
    /// editor and Device Simulator, a Mac, a phone without a motor. The Ghumante.DebugTools assembly only
    /// compiles in development builds and the editor (asmdef define constraint
    /// <c>DEVELOPMENT_BUILD || UNITY_EDITOR</c>), and nothing references it: it installs itself after the
    /// first scene loads, so release builds simply do not contain it. While a world is open it adds the streaming
    /// lines of <see cref="WorldRoot.DebugSummary"/> (F3 there toggles the free-fly camera).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class DebugHud : MonoBehaviour
    {
        private const float SampleSeconds = 0.5f;
        private const float HapticShowSeconds = 1f;
        private const string HudName = "gh-debug-hud";

        private Label _label;
        private float _elapsed;
        private int _frames;
        private float _worstMs;
        private string _stats = string.Empty;
        private string _haptic;
        private float _hapticHideAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindAnyObjectByType<DebugHud>() != null) return;
            var go = new GameObject("[Ghumante Debug HUD]");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugHud>();
        }

        private void OnEnable()
        {
            MobileHaptics.Requested += OnHapticRequested;
        }

        private void OnDisable()
        {
            MobileHaptics.Requested -= OnHapticRequested;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _frames++;
            if (dt * 1000f > _worstMs) _worstMs = dt * 1000f;

            if (_haptic != null && Time.realtimeSinceStartup >= _hapticHideAt)
            {
                _haptic = null;
                ShowText();
            }
            if (_elapsed < SampleSeconds) return;

            EnsureAttached();
            if (_label != null)
            {
                float avgMs = _elapsed * 1000f / _frames;
                int level = QualitySettings.GetQualityLevel();
                string quality = level >= 0 && level < QualitySettings.names.Length ? QualitySettings.names[level] : "?";
                _stats = string.Format(
                    "{0:0} fps  avg {1:0.0} ms  worst {2:0.0} ms\nalloc {3:0} MB  mono {4:0} MB  gfx {5:0} MB\n{6}  {7}",
                    _frames / _elapsed, avgMs, _worstMs,
                    Profiler.GetTotalAllocatedMemoryLong() / 1048576.0,
                    Profiler.GetMonoUsedSizeLong() / 1048576.0,
                    Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576.0,
                    quality, SystemInfo.graphicsDeviceType);
                // World streaming (M1 track C): tiles, queue, upload ms, cache, focus and origin.
                WorldRoot world = WorldRoot.Active;
                if (world != null) _stats += "\n" + world.DebugSummary();
                ShowText();
            }
            _elapsed = 0f;
            _frames = 0;
            _worstMs = 0f;
        }

        /// <summary>Main thread, once per request that passed the player's setting and the rate limiter.</summary>
        private void OnHapticRequested(HapticKind kind, bool deviceWillPlay)
        {
            string where = deviceWillPlay ? "device" : Application.isEditor ? "editor - not felt" : "not played here";
            _haptic = "haptic: " + kind + " (" + where + ")";
            _hapticHideAt = Time.realtimeSinceStartup + HapticShowSeconds;
            ShowText();
        }

        private void ShowText()
        {
            EnsureAttached();
            if (_label == null) return;
            if (_haptic == null) _label.text = _stats;
            else _label.text = _stats.Length == 0 ? _haptic : _stats + "\n" + _haptic;
        }

        /// <summary>
        /// Adds the overlay label to the first UIDocument's root. Screens swap the document's visual tree,
        /// which drops our label, so this re-attaches whenever the label lost its panel.
        /// </summary>
        private void EnsureAttached()
        {
            if (_label != null && _label.panel != null) return;
            UIDocument doc = Object.FindAnyObjectByType<UIDocument>();
            if (doc == null || doc.rootVisualElement == null) return;

            if (_label == null)
            {
                _label = new Label { name = HudName, pickingMode = PickingMode.Ignore };
                IStyle s = _label.style;
                s.position = Position.Absolute;
                // Above the bottom-right OpenStreetMap credit, which must stay readable (LICENSES 1.1).
                s.right = 12;
                s.bottom = 72;
                s.paddingLeft = 10;
                s.paddingRight = 10;
                s.paddingTop = 4;
                s.paddingBottom = 4;
                s.fontSize = 18;
                s.color = Color.white;
                s.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
                s.borderTopLeftRadius = 8;
                s.borderTopRightRadius = 8;
                s.borderBottomLeftRadius = 8;
                s.borderBottomRightRadius = 8;
                s.whiteSpace = WhiteSpace.Normal;
            }
            doc.rootVisualElement.Add(_label);
            _label.BringToFront();
        }
    }
}
