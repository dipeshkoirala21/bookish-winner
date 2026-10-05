using System;

namespace Ghumante.Core.Services
{
    /// <summary>
    /// What a haptic means, not how it feels: each platform maps these to its native vocabulary
    /// (iOS UIFeedbackGenerator styles, Android predefined vibration effects / HapticFeedbackConstants).
    /// Values are stable; append only.
    /// </summary>
    public enum HapticKind : byte
    {
        /// <summary>A light tick: toggles, tab changes, picker detents.</summary>
        Selection = 0,

        /// <summary>An ordinary button press.</summary>
        LightImpact = 1,

        /// <summary>A primary action (Explore), a landing, a door closing.</summary>
        MediumImpact = 2,

        /// <summary>Big moments: a passport stamp, a bungee jump release.</summary>
        HeavyImpact = 3,

        /// <summary>A reward or discovery.</summary>
        Success = 4,

        /// <summary>Not available yet, blocked, or needs attention.</summary>
        Warning = 5,

        /// <summary>Something failed.</summary>
        Error = 6,
    }

    /// <summary>
    /// Haptic feedback on phones that support it (ARCHITECTURE.md 7.9). Implementations never throw and
    /// never block: on unsupported hardware, with the player setting off, or in the editor, Play is a no-op.
    /// </summary>
    public interface IHaptics
    {
        /// <summary>True when the device has hardware this implementation can drive.</summary>
        bool IsSupported { get; }

        /// <summary>The player's setting (Settings &gt; Haptics). The OS-wide haptics setting still applies.</summary>
        bool Enabled { get; set; }

        /// <summary>Plays one haptic. Rate-limited by the implementation (see <see cref="HapticGate"/>).</summary>
        void Play(HapticKind kind);
    }

    /// <summary>
    /// Haptics that do nothing, for the editor and unsupported devices. Remembers the last request so
    /// development builds can show it on screen (DebugTools) when there is no motor to feel.
    /// </summary>
    public sealed class NullHaptics : IHaptics
    {
        public bool IsSupported
        {
            get { return false; }
        }

        public bool Enabled { get; set; } = true;

        /// <summary>The last kind passed to <see cref="Play"/> while enabled, or null.</summary>
        public HapticKind? LastPlayed { get; private set; }

        /// <summary>Number of Play calls accepted while enabled.</summary>
        public int PlayCount { get; private set; }

        /// <summary>Raised for every accepted Play call (debug overlays, tests).</summary>
        public event Action<HapticKind> Played;

        public void Play(HapticKind kind)
        {
            if (!Enabled) return;
            LastPlayed = kind;
            PlayCount++;
            Action<HapticKind> handler = Played;
            if (handler != null) handler(kind);
        }
    }

    /// <summary>
    /// Rate limiter so rapid taps or scrolling don't merge into a continuous buzz: any two haptics are
    /// at least <c>minIntervalSeconds</c> apart, and repeats of the same kind at least
    /// <c>sameKindIntervalSeconds</c> apart. Pure and deterministic; the caller supplies the clock.
    /// </summary>
    public sealed class HapticGate
    {
        private readonly double _minInterval;
        private readonly double _sameKindInterval;
        private double _lastAny = double.NegativeInfinity;
        private readonly double[] _lastByKind;

        public HapticGate(double minIntervalSeconds = 0.035, double sameKindIntervalSeconds = 0.07)
        {
            if (minIntervalSeconds < 0) throw new ArgumentOutOfRangeException(nameof(minIntervalSeconds));
            if (sameKindIntervalSeconds < 0) throw new ArgumentOutOfRangeException(nameof(sameKindIntervalSeconds));
            _minInterval = minIntervalSeconds;
            _sameKindInterval = sameKindIntervalSeconds;
            _lastByKind = new double[Enum.GetValues(typeof(HapticKind)).Length + 8];
            for (int i = 0; i < _lastByKind.Length; i++) _lastByKind[i] = double.NegativeInfinity;
        }

        /// <summary>True if a haptic of <paramref name="kind"/> may play at <paramref name="nowSeconds"/>;
        /// records it when it may.</summary>
        public bool TryPass(HapticKind kind, double nowSeconds)
        {
            int k = (int)kind;
            if (k < 0 || k >= _lastByKind.Length) return false;
            if (nowSeconds - _lastAny < _minInterval) return false;
            if (nowSeconds - _lastByKind[k] < _sameKindInterval) return false;
            _lastAny = nowSeconds;
            _lastByKind[k] = nowSeconds;
            return true;
        }

        /// <summary>Forgets history (e.g. after the app resumes).</summary>
        public void Reset()
        {
            _lastAny = double.NegativeInfinity;
            for (int i = 0; i < _lastByKind.Length; i++) _lastByKind[i] = double.NegativeInfinity;
        }
    }
}
