using Ghumante.Core.Services;

namespace Ghumante.Platform.Haptics
{
    /// <summary>
    /// Haptics for the editor (including the Device Simulator), desktop players and anything else without a
    /// supported motor. Requests go through the same gate and raise <see cref="MobileHaptics.Requested"/> like
    /// on a phone, so DebugTools can show what would have been felt; they are recorded by a
    /// <see cref="NullHaptics"/> and nothing plays.
    /// </summary>
    public sealed class SilentHaptics : GatedHaptics
    {
        private readonly NullHaptics _recorder = new NullHaptics();

        /// <summary>
        /// Records every request that passed <see cref="GatedHaptics.Enabled"/> and the gate (tests, overlays).
        /// </summary>
        public NullHaptics Recorder
        {
            get { return _recorder; }
        }

        protected override bool HasHardware
        {
            get { return false; }
        }

        protected override bool PlayNative(HapticKind kind)
        {
            _recorder.Play(kind);
            return false;
        }
    }
}
