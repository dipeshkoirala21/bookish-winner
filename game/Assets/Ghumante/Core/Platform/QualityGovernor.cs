using System;

namespace Ghumante.Core.Platform
{
    /// <summary>What a <see cref="QualityGovernor.Tick"/> decided.</summary>
    public enum GovernorAction
    {
        None = 0,

        /// <summary>The render scale went one step down (<see cref="QualityGovernor.RenderScale"/>).</summary>
        RenderScaleDown = 1,

        /// <summary>The render scale went one step up.</summary>
        RenderScaleUp = 2,

        /// <summary>Auto only: frames stayed too slow at the minimum render scale, so the tier went one step down
        /// (<see cref="QualityGovernor.Tier"/>). Show the player a friendly note.</summary>
        TierDown = 3,

        /// <summary>Auto only, at most once per session: lots of headroom at full scale, so the tier went one step back
        /// up (never above the detected ceiling).</summary>
        TierUp = 4,
    }

    /// <summary>Tuning of the <see cref="QualityGovernor"/> (defaults are the shipped values).</summary>
    public sealed class QualityGovernorSettings
    {
        /// <summary>Lowest render scale (URP render scale; ARCHITECTURE.md section 10: dynamic resolution down to 0.7).</summary>
        public float MinRenderScale = 0.7f;

        /// <summary>Highest render scale per tier (Low, Mid, High): the URP assets' authored values (Low is authored at
        /// 0.7, so Low has no range to adapt). The Unity side overwrites these with the assets' real values.</summary>
        public float[] MaxRenderScale = { 0.7f, 1f, 1f };

        /// <summary>One render-scale step (0.7, 0.8, 0.9, 1.0): each step changes the pixel count by about a quarter.</summary>
        public float RenderScaleStep = 0.1f;

        /// <summary>Frames are judged in windows of this many seconds.</summary>
        public float WindowS = 0.5f;

        /// <summary>A window whose mean frame interval is above budget × this is too slow (below about 89 % of the
        /// target frame rate).</summary>
        public float OverBudgetFactor = 1.12f;

        /// <summary>A frame above budget × this is a stutter; a window with <see cref="StutterShare"/> of them is too slow
        /// even when its mean is fine.</summary>
        public float StutterFactor = 1.6f;

        public float StutterShare = 0.2f;

        /// <summary>With GPU timings: a window whose mean GPU time is below budget × this has room for one more step
        /// (a step adds at most about 31 % of pixels).</summary>
        public float HeadroomFactor = 0.7f;

        /// <summary>Without GPU timings (unsupported driver, stats off): a window whose mean interval is within budget ×
        /// this, without a stutter, counts as room (a probe; a probe that fails backs off).</summary>
        public float OnTargetFactor = 1.03f;

        /// <summary>Too slow for this long: one render-scale step down.</summary>
        public float DownAfterS = 2f;

        /// <summary>Room for this long: one render-scale step up (doubles after each failed probe, see
        /// <see cref="BounceS"/>, up to <see cref="MaxUpDelayS"/>).</summary>
        public float UpAfterS = 5f;

        /// <summary>Without GPU timings the first probe waits at least this long.</summary>
        public float UpAfterWithoutTimingsS = 10f;

        public float MaxUpDelayS = 120f;

        /// <summary>A step down within this long of a step up means the probe failed: the next probe waits twice as long.</summary>
        public float BounceS = 10f;

        /// <summary>After any change, windows are not judged for this long (render targets are reallocated).</summary>
        public float CooldownS = 1.5f;

        /// <summary>After a blocked spell (loading, pause, menus, app in the background) frames are ignored this long.</summary>
        public float SettleS = 1f;

        /// <summary>A single frame longer than this is a hitch (a tile upload, a GC, a scene change) and is ignored.</summary>
        public float HitchMs = 250f;

        /// <summary>Auto: too slow at the minimum render scale for about this long (leaky: a good window takes one back)
        /// steps the tier down once.</summary>
        public float TierDownAfterS = 10f;

        /// <summary>Auto: GPU time below budget × <see cref="TierUpHeadroomFactor"/> at full scale for this long steps the
        /// tier up (needs GPU timings).</summary>
        public float TierUpAfterS = 60f;

        public float TierUpHeadroomFactor = 0.45f;

        /// <summary>Nothing is judged for this long after a tier change (the world redraws).</summary>
        public float TierCooldownS = 8f;

        /// <summary>Automatic tier steps up per session (the owner's rule: never more than once).</summary>
        public int MaxTierUpsPerSession = 1;
    }

    /// <summary>
    /// The adaptive performance governor (ARCHITECTURE.md section 10), engine-free: fed one frame at a time with the
    /// frame interval (unscaled delta time, what the player feels) and the GPU time when the platform reports it
    /// (FrameTimingManager), it keeps the frame rate on target with dynamic resolution and, on Auto, the tier.
    /// <list type="bullet">
    /// <item><b>Render scale</b> between <see cref="QualityGovernorSettings.MinRenderScale"/> (0.7) and the tier's
    /// maximum, in steps of 0.1: down after 2 s of too-slow windows, up after 5 s of headroom. Hysteresis: "too slow"
    /// is a mean interval 12 % over budget, "room" is GPU time under 70 % of it (or, without timings, simply on target),
    /// so a frame time between the two moves nothing. A step down soon after a step up doubles the wait before the next
    /// probe (5, 10, 20 ... 120 s), so it never oscillates.</item>
    /// <item><b>Tier</b> (Auto only): too slow at the minimum render scale for about 10 s steps the tier down once
    /// (<see cref="GovernorAction.TierDown"/>; the caller shows a friendly note) and the render scale carries on from
    /// there; a tier step up needs a minute of GPU time under 45 % of budget at full scale and happens at most once per
    /// session, never above the detected ceiling.</item>
    /// <item><b>Ignored</b>: blocked frames (loading, pause, menus, background), the second after them, single hitches
    /// over 250 ms, and the cool-down after every change.</item>
    /// </list>
    /// Deterministic, allocation-free per frame.
    /// </summary>
    public sealed class QualityGovernor
    {
        private const float Epsilon = 1e-4f;

        private readonly QualityGovernorSettings _s;
        private QualityTier _tier = QualityTier.Mid;
        private QualityTier _ceiling = QualityTier.High;
        private bool _auto = true;
        private float _budgetMs = 1000f / 30f;
        private int _level;
        private int _maxLevel;

        // The window being filled.
        private float _winS;
        private int _winFrames, _winStutters, _winWorkFrames;
        private float _winWorkMs;

        // Sustained conditions (seconds).
        private float _overS, _roomS, _strongRoomS, _slowAtMinS;
        private float _settleS, _cooldownS, _sinceUpS = float.PositiveInfinity, _upDelayS;

        public QualityGovernor(QualityGovernorSettings settings = null)
        {
            _s = settings ?? new QualityGovernorSettings();
            _upDelayS = _s.UpAfterS;
            Reset(QualityTier.Mid, QualityTier.High, true, 30);
        }

        public QualityGovernorSettings Settings
        {
            get { return _s; }
        }

        /// <summary>The tier the governor runs (Auto may have stepped it).</summary>
        public QualityTier Tier
        {
            get { return _tier; }
        }

        /// <summary>Highest tier Auto may step up to (the detected tier).</summary>
        public QualityTier Ceiling
        {
            get { return _ceiling; }
        }

        /// <summary>True when the tier may step (the player chose Auto).</summary>
        public bool Auto
        {
            get { return _auto; }
        }

        /// <summary>Target frame rate the budget follows.</summary>
        public int TargetFps { get; private set; }

        /// <summary>Frame budget in milliseconds (1000 / target).</summary>
        public float BudgetMs
        {
            get { return _budgetMs; }
        }

        /// <summary>The render scale to apply (URP render scale, at most the tier's maximum).</summary>
        public float RenderScale
        {
            get { return Math.Min(MaxScale(_tier), _s.MinRenderScale + _level * _s.RenderScaleStep); }
        }

        /// <summary>True at the lowest render scale of the tier.</summary>
        public bool AtMinimumScale
        {
            get { return _level <= 0; }
        }

        /// <summary>Automatic tier steps so far (down and up) this session.</summary>
        public int TierDowns { get; private set; }

        public int TierUps { get; private set; }

        /// <summary>Mean frame interval and GPU time (ms) of the last judged window (NaN for GPU time without timings).</summary>
        public float LastIntervalMs { get; private set; }

        public float LastGpuMs { get; private set; }

        /// <summary>Seconds of the probe wait now in force (grows after failed probes).</summary>
        public float UpDelayS
        {
            get { return _upDelayS; }
        }

        /// <summary>
        /// Starts over for a tier (a pick in Settings, a frame-rate change, startup): full render scale, every
        /// sustained condition and the probe back-off cleared. The session's tier-step counts are kept.
        /// </summary>
        public void Reset(QualityTier tier, QualityTier ceiling, bool auto, int targetFps)
        {
            _tier = tier;
            _ceiling = ceiling;
            _auto = auto;
            TargetFps = targetFps > 0 ? targetFps : 30;
            _budgetMs = 1000f / TargetFps;
            _maxLevel = LevelOf(MaxScale(tier));
            _level = _maxLevel;
            _upDelayS = _s.UpAfterS;
            _sinceUpS = float.PositiveInfinity;
            ClearSustained();
            ClearWindow();
            _settleS = _s.SettleS;
            _cooldownS = 0f;
        }

        /// <summary>The frame rate target changed (same tier): the budget follows; the render scale stays.</summary>
        public void SetTargetFps(int targetFps)
        {
            TargetFps = targetFps > 0 ? targetFps : 30;
            _budgetMs = 1000f / TargetFps;
            ClearSustained();
            ClearWindow();
            _settleS = _s.SettleS;
        }

        /// <summary>
        /// One frame. <paramref name="intervalMs"/>: the time since the previous frame (unscaled), <paramref name="gpuMs"/>:
        /// the GPU time of a recent frame, 0 or NaN when unknown, <paramref name="blocked"/>: loading, paused, a menu over
        /// the world or the app in the background (nothing is judged).
        /// </summary>
        public GovernorAction Tick(float intervalMs, float gpuMs, bool blocked)
        {
            if (blocked)
            {
                _settleS = _s.SettleS;
                ClearWindow();
                _overS = 0f;
                _roomS = 0f;
                _strongRoomS = 0f;
                return GovernorAction.None;
            }
            if (!(intervalMs > 0f) || float.IsInfinity(intervalMs) || intervalMs > _s.HitchMs) return GovernorAction.None;
            float dt = intervalMs * 0.001f;
            if (_settleS > 0f)
            {
                _settleS -= dt;
                return GovernorAction.None;
            }
            if (_cooldownS > 0f)
            {
                _cooldownS -= dt;
                return GovernorAction.None;
            }
            _sinceUpS += dt;
            _winS += dt;
            _winFrames++;
            if (intervalMs > _budgetMs * _s.StutterFactor) _winStutters++;
            if (gpuMs > 0f && !float.IsInfinity(gpuMs))
            {
                _winWorkMs += gpuMs;
                _winWorkFrames++;
            }
            if (_winS < _s.WindowS) return GovernorAction.None;
            return Judge();
        }

        private GovernorAction Judge()
        {
            float windowS = _winS;
            float meanInterval = _winS * 1000f / _winFrames;
            float stutterShare = (float)_winStutters / _winFrames;
            bool timings = _winWorkFrames * 2 >= _winFrames;
            float meanGpu = timings ? _winWorkMs / _winWorkFrames : float.NaN;
            LastIntervalMs = meanInterval;
            LastGpuMs = meanGpu;
            ClearWindow();

            bool tooSlow = meanInterval > _budgetMs * _s.OverBudgetFactor || stutterShare >= _s.StutterShare;
            bool room = !tooSlow && (timings
                ? meanGpu < _budgetMs * _s.HeadroomFactor
                : meanInterval <= _budgetMs * _s.OnTargetFactor && stutterShare <= 0f);
            bool strongRoom = !tooSlow && timings && meanGpu < _budgetMs * _s.TierUpHeadroomFactor && _level >= _maxLevel;

            _overS = tooSlow ? _overS + windowS : 0f;
            _roomS = room ? _roomS + windowS : 0f;
            _strongRoomS = strongRoom ? _strongRoomS + windowS : 0f;
            if (tooSlow && _level <= 0) _slowAtMinS += windowS;
            else _slowAtMinS = Math.Max(0f, _slowAtMinS - windowS);

            // 1. Auto, at the minimum render scale and still too slow for about 10 s: one tier down.
            if (_auto && _tier > QualityTier.Low && _slowAtMinS >= _s.TierDownAfterS)
            {
                ChangeTier(_tier - 1);
                TierDowns++;
                return GovernorAction.TierDown;
            }

            // 2. Too slow for 2 s: one render-scale step down. Soon after a step up, that probe failed: back off.
            if (_overS >= _s.DownAfterS && _level > 0)
            {
                _level--;
                if (_sinceUpS < _s.BounceS) _upDelayS = Math.Min(_upDelayS * 2f, _s.MaxUpDelayS);
                _sinceUpS = float.PositiveInfinity;
                AfterChange(_s.CooldownS);
                return GovernorAction.RenderScaleDown;
            }

            // 3. Auto, once per session: a minute of plenty of headroom at full scale, below the ceiling: one tier up.
            if (_auto && _tier < _ceiling && TierUps < _s.MaxTierUpsPerSession && _strongRoomS >= _s.TierUpAfterS)
            {
                ChangeTier(_tier + 1);
                TierUps++;
                return GovernorAction.TierUp;
            }

            // 4. Room for long enough: one render-scale step up (a probe).
            float upAfter = timings ? _upDelayS : Math.Max(_upDelayS, _s.UpAfterWithoutTimingsS);
            if (_roomS >= upAfter && _level < _maxLevel)
            {
                _level++;
                _sinceUpS = 0f;
                AfterChange(_s.CooldownS);
                return GovernorAction.RenderScaleUp;
            }
            return GovernorAction.None;
        }

        private void ChangeTier(QualityTier tier)
        {
            float scale = RenderScale;
            _tier = tier;
            _maxLevel = LevelOf(MaxScale(tier));
            // Carry the resolution over (capped by the new tier): a lighter tier then earns its way back up.
            _level = Math.Min(LevelOf(scale), _maxLevel);
            _upDelayS = _s.UpAfterS;
            _sinceUpS = float.PositiveInfinity;
            _slowAtMinS = 0f;
            AfterChange(_s.TierCooldownS);
        }

        private void AfterChange(float cooldownS)
        {
            ClearSustained();
            ClearWindow();
            _cooldownS = cooldownS;
        }

        private void ClearSustained()
        {
            _overS = 0f;
            _roomS = 0f;
            _strongRoomS = 0f;
            _slowAtMinS = 0f;
        }

        private void ClearWindow()
        {
            _winS = 0f;
            _winFrames = 0;
            _winStutters = 0;
            _winWorkFrames = 0;
            _winWorkMs = 0f;
        }

        private float MaxScale(QualityTier tier)
        {
            float[] max = _s.MaxRenderScale;
            int i = (int)tier;
            float v = max != null && i >= 0 && i < max.Length ? max[i] : 1f;
            return Math.Max(_s.MinRenderScale, Math.Min(1f, v));
        }

        /// <summary>The step level of a render scale (rounded to the nearest step).</summary>
        private int LevelOf(float scale)
        {
            if (!(_s.RenderScaleStep > 0f)) return 0;
            return Math.Max(0, (int)Math.Floor((scale - _s.MinRenderScale) / _s.RenderScaleStep + 0.5f + Epsilon));
        }
    }
}
