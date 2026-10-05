using System;

namespace Ghumante.UI.Motion
{
    /// <summary>
    /// How much the UI may move, shared by every screen (App/Bootstrap owns the instance). Screens listen to
    /// <see cref="Changed"/>, so switching Reduce motion in Settings takes effect immediately.
    /// </summary>
    public sealed class MotionSettings
    {
        private bool _reduceMotion;
        private bool _lowPower;
        private bool _appFocused = true;
        private bool _pauseWhenUnfocused = true;

        public MotionSettings(bool reduceMotion = false, bool lowPower = false)
        {
            _reduceMotion = reduceMotion;
            _lowPower = lowPower;
        }

        /// <summary>Raised after any property changes.</summary>
        public event Action Changed;

        /// <summary>
        /// Accessibility (Settings &gt; Reduce motion, defaulting to the OS setting): no bounces, parallax,
        /// idle loops or particles; screens use short fades only. Haptics are unaffected.
        /// </summary>
        public bool ReduceMotion
        {
            get { return _reduceMotion; }
            set { Set(ref _reduceMotion, value); }
        }

        /// <summary>
        /// Low device tier (ARCHITECTURE.md 10): idle effects are thinned out (fewer clouds and flags, no
        /// birds, no shine mask, flags updated at half rate). The tier's 30 fps target frame rate already
        /// limits how often screens tick.
        /// </summary>
        public bool LowPower
        {
            get { return _lowPower; }
            set { Set(ref _lowPower, value); }
        }

        /// <summary>Whether the application has focus (Bootstrap forwards OnApplicationFocus/Pause).</summary>
        public bool AppFocused
        {
            get { return _appFocused; }
            set { Set(ref _appFocused, value); }
        }

        /// <summary>
        /// When true (players), idle loops freeze while the app is not focused: notification shade, Control
        /// Centre, a system dialog. The editor turns it off so the Game view keeps moving while you click
        /// around the Inspector.
        /// </summary>
        public bool PauseWhenUnfocused
        {
            get { return _pauseWhenUnfocused; }
            set { Set(ref _pauseWhenUnfocused, value); }
        }

        /// <summary>True when idle loops (breathing, drifting clouds, fluttering flags, parallax) may run.</summary>
        public bool IdleAllowed
        {
            get { return !_reduceMotion && (_appFocused || !_pauseWhenUnfocused); }
        }

        private void Set(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            Action handler = Changed;
            if (handler != null) handler();
        }
    }
}
