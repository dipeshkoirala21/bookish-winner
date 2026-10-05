#if UNITY_IOS && !UNITY_EDITOR
using Ghumante.Core.Services;
using Ghumante.Platform.Native;

namespace Ghumante.Platform.Haptics
{
    /// <summary>
    /// iOS haptics through UIKit's feedback generators (native plugin <c>Assets/Plugins/iOS/GhumanteHaptics.mm</c>).
    /// <list type="table">
    /// <item><term>Selection</term><description>UISelectionFeedbackGenerator selectionChanged</description></item>
    /// <item><term>LightImpact / MediumImpact / HeavyImpact</term><description>UIImpactFeedbackGenerator,
    /// style Light / Medium / Heavy</description></item>
    /// <item><term>Success / Warning / Error</term><description>UINotificationFeedbackGenerator, type Success /
    /// Warning / Error</description></item>
    /// </list>
    /// <see cref="IHaptics.IsSupported"/> is true only on hardware with a Taptic Engine (every iPhone that runs
    /// iOS 16), as reported once at start-up by <c>GhumanteHaptics_IsSupported</c>. UIFeedbackGenerator does
    /// nothing, silently, on iPads and on Apple-silicon Macs, so there requests are not sent to UIKit and
    /// <see cref="MobileHaptics.Requested"/> reports them as not played. System Haptics switched off in iOS
    /// Settings still silences a supported iPhone (UIKit handles it; the app cannot read that setting).
    /// </summary>
    public sealed class IosHaptics : GatedHaptics
    {
        private readonly bool _hasTapticEngine;

        public IosHaptics()
        {
            try
            {
                _hasTapticEngine = IosPlugin.GhumanteHaptics_IsSupported() != 0;
                // Creates the generators now rather than on the first tap.
                if (_hasTapticEngine) IosPlugin.GhumanteHaptics_Prepare();
            }
            catch (System.Exception e)
            {
                Fault(e);
            }
        }

        protected override bool HasHardware
        {
            get { return _hasTapticEngine; }
        }

        protected override bool PlayNative(HapticKind kind)
        {
            if (!_hasTapticEngine) return false;
            switch (kind)
            {
                case HapticKind.Selection:
                    IosPlugin.GhumanteHaptics_Selection();
                    return true;
                case HapticKind.LightImpact:
                    IosPlugin.GhumanteHaptics_Impact(IosPlugin.ImpactLight);
                    return true;
                case HapticKind.MediumImpact:
                    IosPlugin.GhumanteHaptics_Impact(IosPlugin.ImpactMedium);
                    return true;
                case HapticKind.HeavyImpact:
                    IosPlugin.GhumanteHaptics_Impact(IosPlugin.ImpactHeavy);
                    return true;
                case HapticKind.Success:
                    IosPlugin.GhumanteHaptics_Notification(IosPlugin.NotificationSuccess);
                    return true;
                case HapticKind.Warning:
                    IosPlugin.GhumanteHaptics_Notification(IosPlugin.NotificationWarning);
                    return true;
                case HapticKind.Error:
                    IosPlugin.GhumanteHaptics_Notification(IosPlugin.NotificationError);
                    return true;
                default:
                    return false;
            }
        }

        protected override void PrepareNative()
        {
            if (_hasTapticEngine) IosPlugin.GhumanteHaptics_Prepare();
        }
    }
}
#endif
