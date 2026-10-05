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
    /// <see cref="IHaptics.IsSupported"/> is true on every iOS device: UIFeedbackGenerator does nothing, silently,
    /// on hardware without a Taptic Engine (iPads) and when the player turned System Haptics off in iOS Settings.
    /// </summary>
    public sealed class IosHaptics : GatedHaptics
    {
        public IosHaptics()
        {
            try
            {
                // Creates the generators now rather than on the first tap.
                IosPlugin.GhumanteHaptics_Prepare();
            }
            catch (System.Exception e)
            {
                Fault(e);
            }
        }

        protected override bool HasHardware
        {
            get { return true; }
        }

        protected override bool PlayNative(HapticKind kind)
        {
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
            IosPlugin.GhumanteHaptics_Prepare();
        }
    }
}
#endif
