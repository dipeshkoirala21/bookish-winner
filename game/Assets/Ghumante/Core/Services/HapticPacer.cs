using System;

namespace Ghumante.Core.Services
{
    /// <summary>
    /// A <see cref="HapticGate"/> that does not lose a tap's outcome to the tap's own press tick. A button plays
    /// a tick on press-down and its outcome (a reward's Success, a "not yet" Warning) on release, often less
    /// than the gate's any-kind interval later and sometimes in the same frame, so a plain gate drops the
    /// outcome, which is the haptic that matters. The pacer keeps such a request instead: an outcome kind
    /// (<see cref="IsOutcome"/>) that the gate rejected only because of the any-kind interval waits in a single
    /// slot (a newer one replaces it) and is handed out by <see cref="TryTakeDue"/> once the gate lets it
    /// through, at most <c>maxDelaySeconds</c> after it was asked for; after that it is dropped as stale.
    /// Everything else is exactly the gate: ticks are still dropped, and an outcome repeated within the
    /// same-kind interval is still dropped, so rapid input never turns into a continuous buzz.
    /// <para>Pure and deterministic; the caller supplies the clock. Call <see cref="TryTakeDue"/> before
    /// <see cref="TryPass"/> for each new request (so a waiting outcome keeps its place in the order) and
    /// poll it, e.g. once per frame, while <see cref="HasDeferred"/> is true.</para>
    /// </summary>
    public sealed class HapticPacer
    {
        /// <summary>The gate's default any-kind interval (35 ms).</summary>
        public const double DefaultMinIntervalSeconds = 0.035;

        /// <summary>The gate's default same-kind interval (70 ms).</summary>
        public const double DefaultSameKindIntervalSeconds = 0.07;

        /// <summary>How late a deferred outcome may still play (250 ms); later it would feel unrelated to the
        /// tap.</summary>
        public const double DefaultMaxDelaySeconds = 0.25;

        private readonly HapticGate _gate;
        private readonly double _sameKindInterval;
        private readonly double _maxDelay;
        private readonly double[] _lastPassByKind;
        private bool _hasDeferred;
        private HapticKind _deferred;
        private double _deferredAt;

        public HapticPacer(double minIntervalSeconds = DefaultMinIntervalSeconds,
                           double sameKindIntervalSeconds = DefaultSameKindIntervalSeconds,
                           double maxDelaySeconds = DefaultMaxDelaySeconds)
        {
            if (maxDelaySeconds < 0) throw new ArgumentOutOfRangeException(nameof(maxDelaySeconds));
            _gate = new HapticGate(minIntervalSeconds, sameKindIntervalSeconds);
            _sameKindInterval = sameKindIntervalSeconds;
            _maxDelay = maxDelaySeconds;
            _lastPassByKind = new double[Enum.GetValues(typeof(HapticKind)).Length];
            ClearHistory();
        }

        /// <summary>True while an outcome is waiting for the gate (poll <see cref="TryTakeDue"/>).</summary>
        public bool HasDeferred
        {
            get { return _hasDeferred; }
        }

        /// <summary>
        /// The kinds that report how a tap turned out (HeavyImpact, Success, Warning, Error). They are worth
        /// playing a few milliseconds late; the press ticks (Selection, LightImpact, MediumImpact) are not.
        /// </summary>
        public static bool IsOutcome(HapticKind kind)
        {
            return kind == HapticKind.HeavyImpact || kind == HapticKind.Success ||
                   kind == HapticKind.Warning || kind == HapticKind.Error;
        }

        /// <summary>
        /// True if a haptic of <paramref name="kind"/> may play at <paramref name="nowSeconds"/> (recorded, as
        /// with <see cref="HapticGate.TryPass"/>). When false and <paramref name="kind"/> is an outcome kind
        /// that lost only to the any-kind interval, it is deferred (replacing any deferred one).
        /// </summary>
        public bool TryPass(HapticKind kind, double nowSeconds)
        {
            if (_gate.TryPass(kind, nowSeconds))
            {
                RecordPass(kind, nowSeconds);
                return true;
            }
            int k = (int)kind;
            if (IsOutcome(kind) && k < _lastPassByKind.Length &&
                nowSeconds - _lastPassByKind[k] >= _sameKindInterval)
            {
                _hasDeferred = true;
                _deferred = kind;
                _deferredAt = nowSeconds;
            }
            return false;
        }

        /// <summary>
        /// True, with the deferred kind, when the deferred outcome may play at <paramref name="nowSeconds"/>
        /// (recorded by the gate; it is then no longer deferred). Drops it once it is older than the maximum
        /// delay. False when nothing is deferred or it must wait longer.
        /// </summary>
        public bool TryTakeDue(double nowSeconds, out HapticKind kind)
        {
            kind = _deferred;
            if (!_hasDeferred) return false;
            if (nowSeconds - _deferredAt > _maxDelay)
            {
                _hasDeferred = false;
                return false;
            }
            if (!_gate.TryPass(_deferred, nowSeconds)) return false;
            _hasDeferred = false;
            RecordPass(_deferred, nowSeconds);
            return true;
        }

        /// <summary>Drops the deferred outcome, if any (haptics switched off, or released).</summary>
        public void CancelDeferred()
        {
            _hasDeferred = false;
        }

        /// <summary>Forgets history and drops the deferred outcome (e.g. after the app resumes).</summary>
        public void Reset()
        {
            _gate.Reset();
            ClearHistory();
            _hasDeferred = false;
        }

        private void RecordPass(HapticKind kind, double nowSeconds)
        {
            int k = (int)kind;
            if (k < _lastPassByKind.Length) _lastPassByKind[k] = nowSeconds;
        }

        private void ClearHistory()
        {
            for (int i = 0; i < _lastPassByKind.Length; i++) _lastPassByKind[i] = double.NegativeInfinity;
        }
    }
}
