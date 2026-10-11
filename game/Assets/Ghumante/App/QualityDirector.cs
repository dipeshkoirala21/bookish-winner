using System;
using Ghumante.Core.Geo;
using Ghumante.Core.Platform;
using Ghumante.Core.Save;
using Ghumante.Platform;
using Ghumante.World;
using Ghumante.World.Rendering;
using UnityEngine;

namespace Ghumante.App
{
    /// <summary>What the <see cref="QualityDirector"/> needs from its owner (<see cref="Bootstrap"/>). Main thread.</summary>
    public interface IQualityHost
    {
        /// <summary>The world of the Explore session (null on the menu).</summary>
        WorldRoot QualityWorld { get; }

        /// <summary>True while frames say nothing about gameplay: no world playing, loading, paused, a sheet or search
        /// over the world.</summary>
        bool QualityBlocked { get; }

        /// <summary>True while the Settings sheet is open: a world redraw waits until it closes.</summary>
        bool QualitySettingsOpen { get; }

        /// <summary>Covers the world while it redraws for a new tier (Explore's loading overlay with
        /// <paramref name="statusKey"/> and the tier's name key), or uncovers it.</summary>
        void ShowQualityRedraw(bool show, string statusKey, string tierKey);

        /// <summary>A short friendly note (a string key).</summary>
        void ShowQualityToast(string key);

        /// <summary>Writes the save.</summary>
        void PersistQuality();
    }

    /// <summary>
    /// Graphics quality that fits the phone (ARCHITECTURE.md section 10; docs/research/w2/ref_quality.md): the Unity side
    /// of the engine-free <see cref="DeviceClassifier"/> and <see cref="QualityGovernor"/>, owned and ticked by
    /// <see cref="Bootstrap"/>.
    /// <list type="bullet">
    /// <item><b>Startup</b>: classifies the device once (RAM, cores, iPhone model, GPU family table) and logs the decision,
    /// resolves the player's choice (Settings > Graphics: Auto, Low, Medium, High; Auto starts from the detected tier, or
    /// the tier the governor settled on for this phone before), and applies the quality level (URP asset: MSAA, shadows,
    /// HDR, authored render scale), the frame rate (the tier's default or the player's 30/60 where the tier allows it), the
    /// look tier (<see cref="ToonLook.OverrideTier"/>) and the motion budget.</item>
    /// <item><b>A change at runtime</b> (a pick in Settings, or the governor on Auto) applies the same at once, without a
    /// restart, and switches the open world live (<see cref="WorldRoot.ApplyTierAsync"/>: streaming radii, building
    /// bands, meshing, life caps, dressing) behind a short "redrawing" overlay; a pick waits until the Settings sheet
    /// closes, so trying the options redraws once.</item>
    /// <item><b>Every frame</b> while a world is played: feeds the governor the unscaled frame interval and the GPU time
    /// (FrameTimingManager, when the platform reports it) and applies its dynamic resolution
    /// (<see cref="ToonLook.DynamicRenderScale"/>, 0.7 to 1.0). On Auto, a tier the phone cannot hold steps down once
    /// (with a toast, "Graphics lowered to keep things smooth"), remembered in the save for this phone, and steps up again
    /// at most once per session.</item>
    /// </list>
    /// No per-frame allocations. Main thread.
    /// </summary>
    public sealed class QualityDirector : IQualityOptions
    {
        /// <summary>String key of the toast after an automatic step down.</summary>
        public const string LoweredToastKey = "quality.lowered";

        /// <summary>String key of the overlay line while the world redraws ({0}: the tier's name).</summary>
        public const string RedrawStatusKey = "quality.redraw";

        /// <summary>String keys of the tier names (Settings' option labels), by <see cref="QualityTier"/>.</summary>
        public static readonly string[] TierNameKeys = { "settings.quality.low", "settings.quality.medium", "settings.quality.high" };

        /// <summary>Longest the redraw overlay waits for ground under the player before it lifts anyway.</summary>
        public const float RedrawPatienceS = 8f;

        /// <summary>The overlay lifts once this share of the new streaming selection is up (and ground is under the player).</summary>
        public const float RedrawProgress = 0.45f;

        private readonly SaveData _save;
        private readonly IQualityHost _host;
        private readonly string _deviceModel;
        private readonly QualityGovernor _governor;
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private GraphicsChoice _choice;
        private QualityTier _autoTier;
        private QualityTier _active;
        private bool _redrawing;
        private bool _awaitingGround;
        private float _redrawStart;
        private bool _toastAfterRedraw;
        private int _redrawToken;
        private int _failedTier = -1;
        private bool _gpuTimingsSeen;

        /// <param name="save">The local save (settings section read and written).</param>
        /// <param name="host">The owner's callbacks.</param>
        /// <param name="facts">The device (<see cref="DeviceFacts.Current"/>; tests pass any).</param>
        public QualityDirector(SaveData save, IQualityHost host, DeviceFacts facts)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _deviceModel = facts.deviceModel ?? "";
            Decision = DeviceTierDetector.Decide(facts);
            Profile = facts.ToProfile();
            DetectedTier = Decision.Tier;
            _choice = QualityChoices.Choice(save.Settings);
            _autoTier = QualityChoices.AutoStartTier(DetectedTier, save.Settings.AutoTierCap, save.Settings.AutoTierDevice, _deviceModel);
            _active = QualityChoices.Resolve(_choice, _autoTier);
            _governor = new QualityGovernor();
        }

        /// <summary>The classifier's decision at launch (logged once).</summary>
        public TierDecision Decision { get; private set; }

        /// <summary>The device facts the decision was made from.</summary>
        public DeviceProfile Profile { get; private set; }

        public GraphicsChoice Choice
        {
            get { return _choice; }
        }

        public QualityTier DetectedTier { get; private set; }

        public QualityTier AutoTier
        {
            get { return _autoTier; }
        }

        public QualityTier ActiveTier
        {
            get { return _active; }
        }

        /// <summary><see cref="ActiveTier"/> as the runtime's tier type.</summary>
        public DeviceTier Tier
        {
            get { return (DeviceTier)_active; }
        }

        public int FrameRate
        {
            get { return QualityChoices.FrameRate(_active, _save.Settings.FrameRate); }
        }

        public int MaxFrameRate
        {
            get { return QualityTiers.MaxFrameRate(_active); }
        }

        public bool WorldRedrawPending
        {
            get
            {
                WorldRoot w = _host.QualityWorld;
                return w != null && w.IsOpen && w.BuiltTier >= 0 && w.BuiltTier != (int)_active;
            }
        }

        /// <summary>The governor (diagnostics, tests).</summary>
        public QualityGovernor Governor
        {
            get { return _governor; }
        }

        /// <summary>True while the world redraws for a new tier (behind the overlay).</summary>
        public bool Redrawing
        {
            get { return _redrawing; }
        }

        public event Action Changed;

        /// <summary>The tier in use changed (quality level, frame rate and look already applied).</summary>
        public event Action<DeviceTier> TierApplied;

        /// <summary>Applies the startup tier and logs the decision once. Call from Bootstrap's Awake.</summary>
        public void ApplyAtStartup()
        {
            QualityGovernorSettings gs = _governor.Settings;
            for (int t = 0; t < QualityTiers.Count && t < gs.MaxRenderScale.Length; t++)
            {
                int level = QualityLevelIndex((QualityTier)t);
                if (level >= 0) gs.MaxRenderScale[t] = ToonLook.AuthoredRenderScaleOf(level);
            }
            Apply(_active, true);
            Debug.Log("Quality: device " + Decision.Describe(Profile) + "; choice " + QualityChoices.Key(_choice) +
                      (_choice == GraphicsChoice.Auto && _autoTier != DetectedTier ? " (lowered to " + _autoTier + " on this phone before)" : "") +
                      " -> " + _active + " ('" + QualityTiers.QualityLevelName(_active) + "'), " + FrameRate + " fps, render scale " +
                      _governor.RenderScale.ToString("0.0") + " to " + gs.MinRenderScale.ToString("0.0") + ".");
        }

        // ----- IQualityOptions (the Settings sheet) ------------------------------------------------------------------

        public void SetChoice(GraphicsChoice choice)
        {
            if (choice == _choice) return;
            _choice = choice;
            QualityChoices.SetChoice(_save.Settings, choice);
            _host.PersistQuality();
            QualityTier tier = QualityChoices.Resolve(choice, _autoTier);
            Debug.Log("Quality: player chose " + QualityChoices.Key(choice) + " -> " + tier + ".");
            Apply(tier, true);
        }

        public void SetFrameRate(int fps)
        {
            if (fps != 30 && fps != 60) return;
            QualityChoices.SetFrameRate(_save.Settings, fps);
            _host.PersistQuality();
            ApplyFrameRate();
            _governor.SetTargetFps(FrameRate);
            RaiseChanged();
        }

        // ----- Frame ---------------------------------------------------------------------------------------------------

        /// <summary>One frame (Bootstrap.Update): the governor, its decisions, and a pending world redraw.</summary>
        public void Tick(float unscaledDeltaS)
        {
            WorldRoot world = _host.QualityWorld;
            if (_redrawing)
            {
                UpdateRedraw(world);
            }
            else if (world != null && world.IsOpen && !world.IsSwitchingTier && world.BuiltTier >= 0 && world.BuiltTier != (int)_active &&
                     (int)_active != _failedTier && !_host.QualitySettingsOpen)
            {
                StartRedraw(world);
            }

            float gpuMs = SampleGpuMs();
            bool blocked = _redrawing || world == null || !world.IsOpen || world.IsSwitchingTier || _host.QualityBlocked || !Application.isFocused;
            GovernorAction action = _governor.Tick(unscaledDeltaS * 1000f, gpuMs, blocked);
            switch (action)
            {
                case GovernorAction.RenderScaleDown:
                case GovernorAction.RenderScaleUp:
                    ToonLook.DynamicRenderScale = _governor.RenderScale;
                    break;
                case GovernorAction.TierDown:
                case GovernorAction.TierUp:
                    OnGovernorTier(action == GovernorAction.TierDown);
                    break;
            }
        }

        /// <summary>Gives the render scale back (the app is closing).</summary>
        public void Dispose()
        {
            ToonLook.DynamicRenderScale = 1f;
            _redrawToken++;
        }

        private void OnGovernorTier(bool down)
        {
            if (_choice != GraphicsChoice.Auto) return; // the governor only steps Auto (it was reset for manual picks)
            _autoTier = _governor.Tier;
            // Remember a lowered Auto for this phone (the next launch starts there; one step up per session can undo it).
            QualityChoices.SetAutoTierCap(_save.Settings, _autoTier < DetectedTier ? (int)_autoTier : -1, _deviceModel);
            _host.PersistQuality();
            Debug.Log("Quality: frames " + (down ? "too slow at render scale " : "comfortable at render scale ") +
                      _governor.RenderScale.ToString("0.0") + " (" + _governor.LastIntervalMs.ToString("0.0") + " ms, GPU " +
                      (float.IsNaN(_governor.LastGpuMs) ? "n/a" : _governor.LastGpuMs.ToString("0.0") + " ms") + "): Auto " +
                      (down ? "lowered" : "raised") + " to " + _autoTier + ".");
            if (down) _toastAfterRedraw = true;
            Apply(_autoTier, false);
            // No world to redraw (it closed meanwhile): say it now.
            if (_toastAfterRedraw && !WorldRedrawPending && !_redrawing) ShowLoweredToast();
        }

        /// <summary>
        /// Applies a tier at once: quality level (URP asset), frame rate, look tier, motion budget; the world follows
        /// in <see cref="Tick"/> (redraw). <paramref name="resetGovernor"/>: a pick or startup (the governor starts over
        /// for the tier); false when the governor itself stepped.
        /// </summary>
        private void Apply(QualityTier tier, bool resetGovernor)
        {
            _active = tier;
            _failedTier = -1;
            int level = QualityLevelIndex(tier);
            if (level >= 0)
            {
                if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
            }
            else
            {
                Debug.LogWarning("Quality: quality level '" + QualityTiers.QualityLevelName(tier) + "' missing. Run Ghumante > Project Setup.");
            }
            QualitySettings.vSyncCount = 0;
            ApplyFrameRate();
            ToonLook.OverrideTier((DeviceTier)tier);
            if (resetGovernor)
            {
                bool auto = _choice == GraphicsChoice.Auto;
                _governor.Reset(tier, auto ? DetectedTier : tier, auto, FrameRate);
            }
            else
            {
                _governor.SetTargetFps(FrameRate);
            }
            ToonLook.DynamicRenderScale = _governor.RenderScale;
            Action<DeviceTier> applied = TierApplied;
            if (applied != null) applied((DeviceTier)tier);
            RaiseChanged();
        }

        private void ApplyFrameRate()
        {
            Application.targetFrameRate = FrameRate;
        }

        // ----- World redraw ----------------------------------------------------------------------------------------------

        private void StartRedraw(WorldRoot world)
        {
            _redrawing = true;
            _awaitingGround = false;
            _redrawStart = Time.unscaledTime;
            int token = ++_redrawToken;
            _host.ShowQualityRedraw(true, RedrawStatusKey, TierNameKeys[(int)_active]);
            Redraw(world, _active, token);
        }

        private async void Redraw(WorldRoot world, QualityTier tier, int token)
        {
            try
            {
                await world.ApplyTierAsync((DeviceTier)tier);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                // Not retried for this tier (the world keeps drawing the old one) until the tier changes again.
                Debug.LogException(e);
                _failedTier = (int)tier;
            }
            if (token != _redrawToken) return;
            _awaitingGround = true;
        }

        private void UpdateRedraw(WorldRoot world)
        {
            bool gone = world == null || !world.IsOpen;
            float waited = Time.unscaledTime - _redrawStart;
            if (!gone && !_awaitingGround && waited < RedrawPatienceS * 2f) return;
            if (!gone && waited < RedrawPatienceS)
            {
                // Lift the curtain once ground is back under the player and the nearest tiles are up.
                WorldPos f = world.Focus;
                bool ground = world.HasGroundAt(f.X, f.Z);
                float progress = world.Streamer != null ? world.Streamer.Scheduler.Progress : 0f;
                if (!ground || (progress < RedrawProgress && !world.IsSettled)) return;
            }
            _redrawing = false;
            _awaitingGround = false;
            _redrawToken++;
            _host.ShowQualityRedraw(false, null, null);
            if (_toastAfterRedraw) ShowLoweredToast();
            RaiseChanged();
        }

        private void ShowLoweredToast()
        {
            _toastAfterRedraw = false;
            _host.ShowQualityToast(LoweredToastKey);
        }

        // ----- Helpers ---------------------------------------------------------------------------------------------------

        /// <summary>GPU time of the latest measured frame in ms (FrameTimingManager), 0 when the platform reports none.</summary>
        private float SampleGpuMs()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) == 0) return 0f;
            double gpu = _timings[0].gpuFrameTime;
            if (!(gpu > 0.0) || gpu > 1000.0) return 0f;
            if (!_gpuTimingsSeen)
            {
                _gpuTimingsSeen = true;
                Debug.Log("Quality: GPU frame timings available (" + gpu.ToString("0.0") + " ms); the governor measures headroom.");
            }
            return (float)gpu;
        }

        /// <summary>Index of a tier's quality level (Project Setup names them Low, Medium, High), -1 when missing.</summary>
        public static int QualityLevelIndex(QualityTier tier)
        {
            return Array.IndexOf(QualitySettings.names, QualityTiers.QualityLevelName(tier));
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }
    }
}
