#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading;
using Ghumante.Core.Services;
using Ghumante.Platform.Native;
using UnityEngine;

namespace Ghumante.Platform.Haptics
{
    /// <summary>
    /// Android haptics through JNI, without a Java plugin. Each kind takes one of two paths:
    /// <list type="table">
    /// <item><term>Selection</term><description>View.performHapticFeedback(CLOCK_TICK)</description></item>
    /// <item><term>LightImpact</term><description>View.performHapticFeedback(KEYBOARD_TAP)</description></item>
    /// <item><term>MediumImpact</term><description>Vibrator, VibrationEffect.EFFECT_CLICK</description></item>
    /// <item><term>HeavyImpact</term><description>Vibrator, EFFECT_HEAVY_CLICK</description></item>
    /// <item><term>Success</term><description>Vibrator, EFFECT_DOUBLE_CLICK</description></item>
    /// <item><term>Warning</term><description>Vibrator, a waveform of two pulses</description></item>
    /// <item><term>Error</term><description>Vibrator, a waveform of three pulses</description></item>
    /// </list>
    /// <para>Why two paths: ordinary UI taps (Selection, LightImpact) go through the View so they obey the
    /// system "Touch feedback" setting like every other app's buttons. If the player turned it off they stay
    /// off: there is no Vibrator fallback when performHapticFeedback declines. The bigger moments use the
    /// Vibrator with predefined effects (API 29, our minSdk), which the OEM tunes for its motor and replaces
    /// with a plain buzz where the vibrator HAL lacks them, so a reward feels the same on every supported
    /// Android version. CONFIRM / REJECT (API 30+) are not used: on AOSP, CONFIRM plays the same EFFECT_CLICK
    /// as a key tap, so a reward would feel like a button press, and API 29 would need another mapping
    /// anyway. The Android "Vibration &amp; haptics" master switch silences both paths.</para>
    /// <para>Threading: requests are handed to the Android UI thread (Activity.runOnUiThread), where View
    /// calls must run. The Vibrator is called there too, so the Unity main thread never waits on a binder
    /// call. A request that arrives before the UI thread ran the previous one replaces it (latest wins; the
    /// gate keeps requests at least 35 ms apart, so this is rare).</para>
    /// <para>Permission: Vibrator.vibrate needs android.permission.VIBRATE (performHapticFeedback does not).
    /// Unity adds that permission to the manifest because this assembly references
    /// <see cref="Handheld.Vibrate"/>, the legacy fallback used only when VibrationEffect objects cannot be
    /// created. Keep that reference.</para>
    /// <para>Every Java object is created once and cached; <see cref="GatedHaptics.Dispose"/>, which runs on
    /// Application.quitting, releases them.</para>
    /// </summary>
    public sealed class AndroidHaptics : GatedHaptics
    {
        // android.view.HapticFeedbackConstants
        private const int FeedbackKeyboardTap = 3;
        private const int FeedbackClockTick = 4;

        // android.os.VibrationEffect predefined effects (API 29)
        private const int EffectClick = 0;
        private const int EffectDoubleClick = 1;
        private const int EffectTick = 2;
        private const int EffectHeavyClick = 5;

        /// <summary>Waveform timings in ms, alternating off and on, starting with off. On-times stay at 35 ms or
        /// more so the eccentric-mass motors of Low-tier phones spin up enough to be felt.</summary>
        private static readonly long[] WarningTimings = { 0, 40, 90, 40 };

        private static readonly long[] ErrorTimings = { 0, 35, 55, 35, 55, 35 };

        private const int NoRequest = -1;
        private const int KindCount = (int)HapticKind.Error + 1;

        private readonly object _sync = new object();
        private readonly AndroidJavaObject[] _effects = new AndroidJavaObject[KindCount];
        private readonly object[][] _vibrateArgs = new object[KindCount][];
        private readonly object[] _clockTickArgs = { FeedbackClockTick };
        private readonly object[] _keyboardTapArgs = { FeedbackKeyboardTap };
        private AndroidJavaObject _activity;
        private AndroidJavaObject _vibrator;
        private AndroidJavaObject _decorView;
        private bool _decorViewMissing;
        private UiThreadRunnable _runnable;
        private object[] _runOnUiThreadArgs;
        private bool _hasVibrator;
        private bool _released;
        private int _pending = NoRequest;

        public AndroidHaptics()
        {
            try
            {
                Initialise();
            }
            catch (Exception e)
            {
                Fault(e);
                ReleaseJavaObjects();
            }
            Application.quitting += Dispose;
        }

        protected override bool HasHardware
        {
            get { return _hasVibrator; }
        }

        /// <summary>Main thread: hands the request to the UI thread (or the legacy vibrate).</summary>
        protected override bool PlayNative(HapticKind kind)
        {
            int k = (int)kind;
            if (!_hasVibrator || _released || k < 0 || k >= KindCount) return false;

            bool viewPath = kind == HapticKind.Selection || kind == HapticKind.LightImpact;
            if (!viewPath && _vibrateArgs[k] == null)
            {
                // VibrationEffect could not be created. Handheld.Vibrate is one long fixed buzz, so it is kept
                // for the big moments only.
                if (kind < HapticKind.HeavyImpact) return false;
                Handheld.Vibrate();
                return true;
            }

            if (Interlocked.Exchange(ref _pending, k) == NoRequest)
            {
                try
                {
                    _activity.Call("runOnUiThread", _runOnUiThreadArgs);
                }
                catch (Exception)
                {
                    Interlocked.Exchange(ref _pending, NoRequest);
                    throw;
                }
            }
            return true;
        }

        protected override void DisposeNative()
        {
            Application.quitting -= Dispose;
            ReleaseJavaObjects();
        }

        private void Initialise()
        {
            int sdk = AndroidPlatform.SdkInt();
            _activity = AndroidPlatform.CurrentActivity();
            if (_activity == null) throw new InvalidOperationException("UnityPlayer.currentActivity is null");

            _vibrator = DefaultVibrator(_activity, sdk);
            _hasVibrator = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            if (!_hasVibrator)
            {
                ReleaseJavaObjects();
                return;
            }

            try
            {
                using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    // Selection and LightImpact normally use the View; these are for when it is unavailable.
                    SetEffect(HapticKind.Selection, effect.CallStatic<AndroidJavaObject>("createPredefined", EffectTick));
                    SetEffect(HapticKind.LightImpact, effect.CallStatic<AndroidJavaObject>("createPredefined", EffectClick));
                    SetEffect(HapticKind.MediumImpact, effect.CallStatic<AndroidJavaObject>("createPredefined", EffectClick));
                    SetEffect(HapticKind.HeavyImpact, effect.CallStatic<AndroidJavaObject>("createPredefined", EffectHeavyClick));
                    SetEffect(HapticKind.Success, effect.CallStatic<AndroidJavaObject>("createPredefined", EffectDoubleClick));
                    SetEffect(HapticKind.Warning, effect.CallStatic<AndroidJavaObject>("createWaveform", WarningTimings, -1));
                    SetEffect(HapticKind.Error, effect.CallStatic<AndroidJavaObject>("createWaveform", ErrorTimings, -1));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("AndroidHaptics: VibrationEffect unavailable, falling back to Handheld.Vibrate for " +
                                 "the big moments: " + e.Message);
                ReleaseEffects();
            }

            _runnable = new UiThreadRunnable(this);
            _runOnUiThreadArgs = new object[] { _runnable };
        }

        private static AndroidJavaObject DefaultVibrator(AndroidJavaObject context, int sdk)
        {
            if (sdk >= 31)
            {
                // Context.getSystemService("vibrator") is deprecated from API 31 in favour of VibratorManager.
                using (AndroidJavaObject manager = context.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                {
                    if (manager != null) return manager.Call<AndroidJavaObject>("getDefaultVibrator");
                }
            }
            return context.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }

        private void SetEffect(HapticKind kind, AndroidJavaObject effect)
        {
            if (effect == null) throw new InvalidOperationException("VibrationEffect for " + kind + " is null");
            _effects[(int)kind] = effect;
            _vibrateArgs[(int)kind] = new object[] { effect };
        }

        /// <summary>Runs on the Android UI thread. Must not let any exception escape into Java.</summary>
        private void RunOnUiThread()
        {
            int k = Interlocked.Exchange(ref _pending, NoRequest);
            if (k == NoRequest) return;
            try
            {
                lock (_sync)
                {
                    if (_released || Faulted) return;
                    PlayOnUiThread((HapticKind)k);
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void PlayOnUiThread(HapticKind kind)
        {
            if (kind == HapticKind.Selection || kind == HapticKind.LightImpact)
            {
                AndroidJavaObject view = DecorView();
                if (view != null)
                {
                    // Returns false when the player turned touch feedback off: respected, no fallback.
                    view.Call<bool>("performHapticFeedback",
                                    kind == HapticKind.Selection ? _clockTickArgs : _keyboardTapArgs);
                    return;
                }
            }
            object[] args = _vibrateArgs[(int)kind];
            if (args != null) _vibrator.Call("vibrate", args);
        }

        /// <summary>The window's decor view, looked up once on the UI thread (under <see cref="_sync"/>).</summary>
        private AndroidJavaObject DecorView()
        {
            if (_decorView == null && !_decorViewMissing)
            {
                using (AndroidJavaObject window = _activity.Call<AndroidJavaObject>("getWindow"))
                {
                    _decorView = window != null ? window.Call<AndroidJavaObject>("getDecorView") : null;
                }
                _decorViewMissing = _decorView == null;
            }
            return _decorView;
        }

        private void ReleaseJavaObjects()
        {
            lock (_sync)
            {
                _released = true;
                ReleaseEffects();
                DisposeAndClear(ref _decorView);
                DisposeAndClear(ref _vibrator);
                DisposeAndClear(ref _activity);
                if (_runnable != null)
                {
                    _runnable.javaInterface.Dispose();
                    _runnable = null;
                }
                _runOnUiThreadArgs = null;
            }
        }

        private void ReleaseEffects()
        {
            for (int i = 0; i < KindCount; i++)
            {
                DisposeAndClear(ref _effects[i]);
                _vibrateArgs[i] = null;
            }
        }

        private static void DisposeAndClear(ref AndroidJavaObject obj)
        {
            if (obj == null) return;
            obj.Dispose();
            obj = null;
        }

        /// <summary>
        /// java.lang.Runnable implemented in C#, created once. Dispatches run() without reflection, so IL2CPP
        /// managed stripping cannot remove the target method.
        /// </summary>
        private sealed class UiThreadRunnable : AndroidJavaProxy
        {
            private readonly AndroidHaptics _owner;

            public UiThreadRunnable(AndroidHaptics owner) : base("java.lang.Runnable")
            {
                _owner = owner;
            }

            public override AndroidJavaObject Invoke(string methodName, object[] args)
            {
                if (methodName == "run")
                {
                    _owner.RunOnUiThread();
                    return null;
                }
                return base.Invoke(methodName, args);
            }
        }
    }
}
#endif
