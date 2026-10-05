using System;
using Ghumante.Core.Services;
using UnityEngine;

namespace Ghumante.Platform.Haptics
{
    /// <summary>
    /// Creates the platform's <see cref="IHaptics"/> (ARCHITECTURE.md 7.9) and reports every haptic request,
    /// so the editor and development builds can show haptics that cannot be felt.
    /// <list type="bullet">
    /// <item>iOS player: <c>IosHaptics</c>, UIKit feedback generators through the native plugin
    /// <c>Assets/Plugins/iOS/GhumanteHaptics.mm</c>.</item>
    /// <item>Android player: <c>AndroidHaptics</c>, JNI to View.performHapticFeedback and Vibrator (no Java
    /// plugin).</item>
    /// <item>Everything else (editor, Device Simulator, macOS/Windows players): <see cref="SilentHaptics"/>.</item>
    /// </list>
    /// All of them apply <see cref="HapticGate"/>, honour <see cref="IHaptics.Enabled"/> and never throw.
    /// </summary>
    public static class MobileHaptics
    {
        /// <summary>
        /// Raised on the main thread for every request that passed <see cref="IHaptics.Enabled"/> and the rate
        /// limiter. <c>deviceWillPlay</c> is true when the device was asked to play it (it may still be silenced
        /// by the OS: iOS System Haptics off, Android touch feedback off, an iPad without a Taptic Engine), and
        /// false in the editor, on devices without a motor, or after a native error. Handler exceptions are
        /// logged, never propagated.
        /// </summary>
        public static event Action<HapticKind, bool> Requested;

        /// <summary>
        /// The haptics implementation for the platform the player runs on, with <see cref="IHaptics.Enabled"/>
        /// set to <paramref name="enabled"/> (normally <c>SaveData.Settings.Haptics</c>). Never throws; on a
        /// native failure the returned object reports <see cref="IHaptics.IsSupported"/> false. Native objects
        /// are released when the application quits, or earlier through <see cref="IDisposable"/>.
        /// </summary>
        public static IHaptics Create(bool enabled)
        {
            GatedHaptics haptics;
#if UNITY_IOS && !UNITY_EDITOR
            haptics = new IosHaptics();
#elif UNITY_ANDROID && !UNITY_EDITOR
            haptics = new AndroidHaptics();
#else
            haptics = new SilentHaptics();
#endif
            haptics.Enabled = enabled;
            return haptics;
        }

        /// <summary>
        /// Warms the hardware up when a request is likely soon (a screen with haptic buttons appeared). Does
        /// nothing for implementations that need no warm-up or were not created by <see cref="Create"/>.
        /// </summary>
        public static void Prepare(IHaptics haptics)
        {
            var gated = haptics as GatedHaptics;
            if (gated != null) gated.Prepare();
        }

        internal static void RaiseRequested(HapticKind kind, bool deviceWillPlay)
        {
            Action<HapticKind, bool> handler = Requested;
            if (handler == null) return;
            try
            {
                handler(kind, deviceWillPlay);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Drops subscribers left over from a previous play session when domain reload is off.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Requested = null;
        }
    }
}
