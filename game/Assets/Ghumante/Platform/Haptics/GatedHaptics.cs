using System;
using System.Threading;
using Ghumante.Core.Services;
using UnityEngine;

namespace Ghumante.Platform.Haptics
{
    /// <summary>
    /// What every <see cref="IHaptics"/> from <see cref="MobileHaptics.Create"/> does before it reaches the
    /// hardware: it honours <see cref="Enabled"/>, rate-limits with a <see cref="HapticGate"/> (through a
    /// <see cref="HapticPacer"/>) on <see cref="Time.realtimeSinceStartupAsDouble"/>, and raises
    /// <see cref="MobileHaptics.Requested"/> for each request that passes the gate. It never throws: the first
    /// native exception disables the implementation (logged once), after which requests still raise the event
    /// but play nothing.
    /// <para>A tap's outcome (Success, Warning, Error, HeavyImpact) that arrives within 35 ms of its own
    /// press-down tick, as a quick tap at 30 fps does, is not dropped: the pacer holds it and it plays on a
    /// later frame, once the gate allows it (at most 250 ms late). The wait is polled once per frame through
    /// Unity's main-thread <see cref="SynchronizationContext"/>, only while something waits; no allocations.</para>
    /// <para>Call <see cref="Play"/> from the Unity main thread (Unity's clock is main-thread only; a call from
    /// another thread is ignored).</para>
    /// </summary>
    public abstract class GatedHaptics : IHaptics, IDisposable
    {
        private readonly HapticPacer _pacer = new HapticPacer();
        private readonly SendOrPostCallback _pollDeferred;
        private bool _pollPosted;
        private int _faulted;
        private bool _disposed;

        protected GatedHaptics()
        {
            _pollDeferred = PollDeferred;
        }

        /// <summary>
        /// True when the device has hardware this implementation can drive and no native call has failed.
        /// </summary>
        public bool IsSupported
        {
            get { return !Faulted && !_disposed && HasHardware; }
        }

        /// <summary>The player's setting (Settings &gt; Haptics). The OS-wide haptics settings still apply.</summary>
        public bool Enabled { get; set; }

        /// <summary>True once a native call threw; the implementation then stays silent for the session.</summary>
        public bool Faulted
        {
            get { return Volatile.Read(ref _faulted) != 0; }
        }

        /// <summary>Whether the device has a motor this implementation can drive.</summary>
        protected abstract bool HasHardware { get; }

        /// <summary>
        /// Plays one haptic if enabled and allowed by the rate limiter; an outcome kind that only has to wait
        /// for the rate limiter plays a frame or two later instead. Never throws.
        /// </summary>
        public void Play(HapticKind kind)
        {
            if (!Enabled) return;
            double now;
            try
            {
                now = Time.realtimeSinceStartupAsDouble;
            }
            catch (Exception)
            {
                return; // not on the main thread
            }
            PlayAt(kind, now);
        }

        /// <summary><see cref="Play"/> at a given time on Unity's realtime clock (main thread; tests).</summary>
        internal void PlayAt(HapticKind kind, double nowSeconds)
        {
            if (!Enabled) return;
            // A waiting outcome goes first, so taps keep their order.
            PlayDeferredIfDue(nowSeconds);
            if (_pacer.TryPass(kind, nowSeconds))
            {
                PlayPassed(kind);
            }
            else if (_pacer.HasDeferred)
            {
                SchedulePoll();
            }
        }

        /// <summary>
        /// Plays the waiting outcome if the rate limiter now allows it (main thread; the per-frame poll and
        /// tests). Drops it if the player switched haptics off meanwhile.
        /// </summary>
        internal void PlayDeferredIfDue(double nowSeconds)
        {
            if (!_pacer.HasDeferred) return;
            if (!Enabled)
            {
                _pacer.CancelDeferred();
                return;
            }
            HapticKind due;
            if (_pacer.TryTakeDue(nowSeconds, out due)) PlayPassed(due);
        }

        /// <summary>True while an outcome waits for the rate limiter (tests).</summary>
        internal bool HasDeferred
        {
            get { return _pacer.HasDeferred; }
        }

        private void PlayPassed(HapticKind kind)
        {
            bool deviceWillPlay = false;
            if (!Faulted && !_disposed)
            {
                try
                {
                    deviceWillPlay = PlayNative(kind);
                }
                catch (Exception e)
                {
                    Fault(e);
                    deviceWillPlay = false;
                }
            }
            MobileHaptics.RaiseRequested(kind, deviceWillPlay);
        }

        /// <summary>
        /// Polls the waiting outcome on the next frame: Unity's main-thread SynchronizationContext runs posted
        /// callbacks once per frame (PlayerLoop ScriptRunDelayedTasks; in the editor on every editor update),
        /// and a callback posted while it runs waits for the following frame. Posting a cached delegate does
        /// not allocate. Without a context (not expected on the main thread) the outcome plays with the next
        /// request, or is dropped as stale.
        /// </summary>
        private void SchedulePoll()
        {
            if (_pollPosted) return;
            SynchronizationContext context = SynchronizationContext.Current;
            if (context == null) return;
            _pollPosted = true;
            context.Post(_pollDeferred, null);
        }

        private void PollDeferred(object state)
        {
            _pollPosted = false;
            if (!_pacer.HasDeferred) return;
            double now;
            try
            {
                now = Time.realtimeSinceStartupAsDouble;
            }
            catch (Exception)
            {
                _pacer.CancelDeferred();
                return;
            }
            PlayDeferredIfDue(now);
            if (_pacer.HasDeferred) SchedulePoll();
        }

        /// <summary>
        /// Warms the haptic hardware up for a request expected within the next second or two (iOS keeps its
        /// Taptic Engine prepared for a few seconds), e.g. when a screen with haptic buttons appears. Optional.
        /// </summary>
        public void Prepare()
        {
            if (!Enabled || Faulted || _disposed) return;
            try
            {
                PrepareNative();
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Forgets the rate limiter's history and any waiting outcome (e.g. after the app resumes).</summary>
        public void ResetGate()
        {
            _pacer.Reset();
        }

        /// <summary>Releases native objects. Later requests still raise the event but play nothing.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pacer.CancelDeferred();
            try
            {
                DisposeNative();
            }
            catch (Exception e)
            {
                Debug.LogWarning(GetType().Name + ": releasing native haptics failed: " + e.Message);
            }
        }

        /// <summary>
        /// Plays <paramref name="kind"/> on the device (main thread, after the gate). Returns whether the device
        /// was asked to play it; false for kinds this platform does not map. May throw: the caller faults.
        /// </summary>
        protected abstract bool PlayNative(HapticKind kind);

        /// <summary>Platform warm-up; see <see cref="Prepare"/>.</summary>
        protected virtual void PrepareNative()
        {
        }

        /// <summary>Releases cached native objects (main thread, once).</summary>
        protected virtual void DisposeNative()
        {
        }

        /// <summary>
        /// Disables native playback for the rest of the session and logs <paramref name="error"/> once.
        /// Thread-safe (Android plays on its UI thread).
        /// </summary>
        protected void Fault(Exception error)
        {
            if (Interlocked.Exchange(ref _faulted, 1) != 0) return;
            Debug.LogWarning(GetType().Name + ": haptics disabled for this session after a native error: " + error);
        }
    }
}
