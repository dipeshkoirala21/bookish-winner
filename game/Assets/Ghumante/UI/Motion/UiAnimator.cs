using System;
using System.Collections.Generic;
using Ghumante.Core.Motion;
using UnityEngine.UIElements;

namespace Ghumante.UI.Motion
{
    /// <summary>A per-frame callback: <paramref name="time"/> is the clock it runs on, in seconds.</summary>
    public delegate void MotionUpdate(float time, float dt);

    /// <summary>
    /// One lightweight animator per screen (UI/README.md, "Motion"). It is driven by the panel scheduler of
    /// the screen's root, so it stops by itself when the tree leaves the panel and <see cref="Dispose"/>
    /// unregisters it. Every tick it:
    /// <list type="number">
    /// <item>fires due <see cref="After"/> callbacks;</item>
    /// <item>runs idle loops (<see cref="OnIdle"/>) on their own clock, only while
    /// <see cref="MotionSettings.IdleAllowed"/> (not with Reduce motion, not while the app is unfocused);</item>
    /// <item>runs reactive updates (<see cref="OnUpdate"/>: particles, sheets);</item>
    /// <item>advances every <see cref="MotionNode"/> and writes the styles that changed.</item>
    /// </list>
    /// It ticks on every panel update, i.e. once per rendered frame, and integrates the measured time step, so
    /// the frame rate (Application.targetFrameRate: 30 fps on the Low and Mid tiers) is the only throttle.
    /// <see cref="Tick"/> is public so tests can drive it without a panel.
    /// </summary>
    public sealed class UiAnimator : IDisposable
    {
        /// <summary>
        /// Scheduler interval: 0 runs the tick on every panel update. The scheduler runs an item only when
        /// <c>now - interval &gt;= lastRun</c>, with <c>now</c> in whole milliseconds sampled once per update and no
        /// carry-over, so an interval close to the frame period (16 at 60 fps, 33 at 30 fps) skips every frame
        /// that arrives a fraction of a millisecond early, and the motion judders.
        /// </summary>
        public const long TickIntervalMs = 0;

        /// <summary>Longest step a tick may take; longer gaps (a hitch, a resume) are clamped.</summary>
        public const float MaxStep = 0.1f;

        private struct Timer
        {
            public float At;
            public Action Action;
        }

        private readonly MotionSettings _settings;
        private readonly List<MotionNode> _nodes = new List<MotionNode>();
        private readonly Dictionary<VisualElement, MotionNode> _byElement = new Dictionary<VisualElement, MotionNode>();
        private readonly List<MotionUpdate> _updates = new List<MotionUpdate>();
        private readonly List<MotionUpdate> _idle = new List<MotionUpdate>();
        private readonly List<Action> _rests = new List<Action>();
        private readonly List<Timer> _timers = new List<Timer>();
        private IVisualElementScheduledItem _tick;
        private bool _wasReduced;
        private bool _disposed;

        public UiAnimator(VisualElement host, MotionSettings settings)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _wasReduced = settings.ReduceMotion;
            _settings.Changed += OnSettingsChanged;
            _tick = host.schedule.Execute(OnTimer).Every(TickIntervalMs);
        }

        public MotionSettings Settings
        {
            get { return _settings; }
        }

        /// <summary>Seconds this animator has run.</summary>
        public float Time { get; private set; }

        /// <summary>Seconds idle loops have run (frozen while idle motion is not allowed).</summary>
        public float IdleTime { get; private set; }

        /// <summary>True when Reduce motion is on: callers skip bounces and use short fades.</summary>
        public bool Reduced
        {
            get { return _settings.ReduceMotion; }
        }

        /// <summary>True while idle loops run (false with Reduce motion or while the app is unfocused).</summary>
        public bool IdleRunning
        {
            get { return !_disposed && _settings.IdleAllowed; }
        }

        public int NodeCount
        {
            get { return _nodes.Count; }
        }

        /// <summary>The node animating <paramref name="element"/>, created on first use.</summary>
        /// <param name="element">The element to animate.</param>
        /// <param name="dynamic">Hint UI Toolkit that the transform changes every frame (clouds, flags, particles):
        /// the renderer then updates it on the GPU without re-tessellating.</param>
        public MotionNode Node(VisualElement element, bool dynamic = false)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            MotionNode node;
            if (_byElement.TryGetValue(element, out node)) return node;
            node = new MotionNode(element);
            _byElement.Add(element, node);
            _nodes.Add(node);
            if (dynamic) element.usageHints |= UsageHints.DynamicTransform;
            return node;
        }

        /// <summary>Starts <paramref name="tween"/> on a channel now (its delay counts from now).</summary>
        public void Play(MotionNode node, MotionChannel channel, Tween tween)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            node.Play(channel, tween, Time);
        }

        /// <summary>Runs <paramref name="action"/> once, <paramref name="seconds"/> from now (on this
        /// animator's clock, so it never fires after <see cref="Dispose"/>).</summary>
        public void After(float seconds, Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _timers.Add(new Timer { At = Time + Math.Max(0f, seconds), Action = action });
        }

        /// <summary>Registers a callback that runs every tick (on <see cref="Time"/>).</summary>
        public void OnUpdate(MotionUpdate update)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            _updates.Add(update);
        }

        /// <summary>
        /// Registers an idle loop (on <see cref="IdleTime"/>) and the action that puts its elements back at
        /// rest when Reduce motion is switched on.
        /// </summary>
        public void OnIdle(MotionUpdate update, Action rest)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            _idle.Add(update);
            if (rest != null) _rests.Add(rest);
        }

        /// <summary>Writes every node's current pose now, so the first rendered frame already shows it.</summary>
        public void Commit()
        {
            for (int i = 0; i < _nodes.Count; i++) _nodes[i].WriteNow();
        }

        /// <summary>Advances everything by <paramref name="dt"/> seconds (clamped to [0, <see cref="MaxStep"/>]).</summary>
        public void Tick(float dt)
        {
            if (_disposed) return;
            if (!(dt > 0f)) dt = 0f;
            if (dt > MaxStep) dt = MaxStep;
            Time += dt;

            for (int i = 0; i < _timers.Count;)
            {
                if (_timers[i].At <= Time)
                {
                    Action action = _timers[i].Action;
                    _timers.RemoveAt(i);
                    action();
                    if (_disposed) return;
                }
                else
                {
                    i++;
                }
            }

            if (_settings.IdleAllowed)
            {
                IdleTime += dt;
                for (int i = 0; i < _idle.Count; i++) _idle[i](IdleTime, dt);
            }
            for (int i = 0; i < _updates.Count; i++) _updates[i](Time, dt);
            for (int i = 0; i < _nodes.Count; i++) _nodes[i].Update(Time, dt);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _settings.Changed -= OnSettingsChanged;
            if (_tick != null)
            {
                _tick.Pause();
                _tick = null;
            }
            _timers.Clear();
            _updates.Clear();
            _idle.Clear();
            _rests.Clear();
        }

        private void OnTimer(TimerState state)
        {
            Tick(state.deltaTime / 1000f);
        }

        private void OnSettingsChanged()
        {
            if (_disposed) return;
            if (_settings.ReduceMotion && !_wasReduced)
            {
                // Everything back to rest at once: no parallax offset, no half-finished bounce.
                for (int i = 0; i < _rests.Count; i++) _rests[i]();
                for (int i = 0; i < _nodes.Count; i++) _nodes[i].Settle();
            }
            _wasReduced = _settings.ReduceMotion;
        }
    }
}
