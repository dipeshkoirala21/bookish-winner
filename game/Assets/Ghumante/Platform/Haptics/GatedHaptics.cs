using System;
using System.Threading;
using Ghumante.Core.Services;
using UnityEngine;

namespace Ghumante.Platform.Haptics
{
    /// <summary>
    /// What every <see cref="IHaptics"/> from <see cref="MobileHaptics.Create"/> does before it reaches the
    /// hardware: it honours <see cref="Enabled"/>, rate-limits with a <see cref="HapticGate"/> on
    /// <see cref="Time.realtimeSinceStartupAsDouble"/>, and raises <see cref="MobileHaptics.Requested"/> for each
    /// request that passes the gate. It never throws: the first native exception disables the implementation
    /// (logged once), after which requests still raise the event but play nothing.
    /// <para>Call <see cref="Play"/> from the Unity main thread (Unity's clock is main-thread only; a call from
    /// another thread is ignored).</para>
    /// </summary>
    public abstract class GatedHaptics : IHaptics, IDisposable
    {
        private readonly HapticGate _gate = new HapticGate();
        private int _faulted;
        private bool _disposed;

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

        /// <summary>Plays one haptic if enabled and allowed by the rate limiter. Never throws.</summary>
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
            if (!_gate.TryPass(kind, now)) return;

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

        /// <summary>Forgets the rate limiter's history (e.g. after the app resumes).</summary>
        public void ResetGate()
        {
            _gate.Reset();
        }

        /// <summary>Releases native objects. Later requests still raise the event but play nothing.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
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
