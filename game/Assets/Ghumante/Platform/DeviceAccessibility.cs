using System;
using UnityEngine;
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
using Ghumante.Platform.Native;
#endif

namespace Ghumante.Platform
{
    /// <summary>
    /// Accessibility preferences the OS exposes that the game should follow. The UI combines them with the
    /// player's own settings, e.g. reduce motion when <c>SaveData.Settings.ReduceMotion</c> is on OR
    /// <see cref="PrefersReducedMotion"/> is true.
    /// </summary>
    public static class DeviceAccessibility
    {
        private static bool s_cached;
        private static bool s_reducedMotion;
        private static bool s_hooked;

        /// <summary>
        /// Raised on the main thread when the app regains focus and the OS reduce-motion preference changed
        /// while it was away (the player toggled it in Settings). Only queried when someone subscribed.
        /// </summary>
        public static event Action<bool> ReducedMotionChanged;

        /// <summary>
        /// True when the OS asks apps to minimise motion: iOS Settings &gt; Accessibility &gt; Motion &gt; Reduce
        /// Motion (UIAccessibilityIsReduceMotionEnabled); Android "Remove animations", i.e. an animator duration
        /// or transition animation scale of 0 (Settings.Global). False in the editor and elsewhere. The answer
        /// is cached until the app next regains focus, so calling this every frame is cheap. Main thread only.
        /// </summary>
        public static bool PrefersReducedMotion()
        {
            HookFocus();
            if (!s_cached)
            {
                s_reducedMotion = Query();
                s_cached = true;
            }
            return s_reducedMotion;
        }

        /// <summary>Forgets the cached answer; the next call asks the OS again.</summary>
        public static void Invalidate()
        {
            s_cached = false;
        }

        private static void HookFocus()
        {
            if (s_hooked) return;
            s_hooked = true;
            Application.focusChanged += OnFocusChanged;
        }

        private static void OnFocusChanged(bool focused)
        {
            if (!focused) return;
            bool wasCached = s_cached;
            bool previous = s_reducedMotion;
            s_cached = false;
            Action<bool> handler = ReducedMotionChanged;
            if (!wasCached || handler == null) return;
            bool now = PrefersReducedMotion();
            if (now == previous) return;
            try
            {
                handler(now);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Asks the OS. Never throws: an unavailable API means "no preference".</summary>
        private static bool Query()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                return IosPlugin.GhumanteHaptics_ReduceMotionEnabled() != 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("DeviceAccessibility: reduce-motion query failed: " + e.Message);
                return false;
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaObject activity = AndroidPlatform.CurrentActivity())
                {
                    if (activity == null) return false;
                    using (AndroidJavaObject resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                    using (var global = new AndroidJavaClass("android.provider.Settings$Global"))
                    {
                        // Accessibility > Remove animations sets all three animation scales to 0; developer
                        // options can set them one by one.
                        float animator = global.CallStatic<float>("getFloat", resolver, "animator_duration_scale", 1f);
                        float transition = global.CallStatic<float>("getFloat", resolver, "transition_animation_scale", 1f);
                        return animator == 0f || transition == 0f;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("DeviceAccessibility: animation scale query failed: " + e.Message);
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>Clears state left over from a previous play session when domain reload is off.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (s_hooked) Application.focusChanged -= OnFocusChanged;
            s_hooked = false;
            s_cached = false;
            s_reducedMotion = false;
            ReducedMotionChanged = null;
        }
    }
}
