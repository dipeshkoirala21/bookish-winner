#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Ghumante.Platform.Native
{
    /// <summary>
    /// P/Invoke declarations for <c>Assets/Plugins/iOS/GhumanteHaptics.mm</c>, which Unity compiles into the
    /// Xcode project and links statically (hence <c>"__Internal"</c>). Unity runs the player loop on the
    /// iOS main thread, so these UIKit calls happen on the main thread as UIKit requires.
    /// </summary>
    internal static class IosPlugin
    {
        /// <summary>UIImpactFeedbackStyle raw values (Soft and Rigid need iOS 13; our floor is 16).</summary>
        internal const int ImpactLight = 0;
        internal const int ImpactMedium = 1;
        internal const int ImpactHeavy = 2;
        internal const int ImpactSoft = 3;
        internal const int ImpactRigid = 4;

        /// <summary>UINotificationFeedbackType raw values.</summary>
        internal const int NotificationSuccess = 0;
        internal const int NotificationWarning = 1;
        internal const int NotificationError = 2;

        /// <summary>Creates the common generators (once) and calls prepare on them.</summary>
        [DllImport("__Internal")]
        internal static extern void GhumanteHaptics_Prepare();

        /// <summary>UIImpactFeedbackGenerator impactOccurred for a UIImpactFeedbackStyle (0-4).</summary>
        [DllImport("__Internal")]
        internal static extern void GhumanteHaptics_Impact(int style);

        /// <summary>UISelectionFeedbackGenerator selectionChanged.</summary>
        [DllImport("__Internal")]
        internal static extern void GhumanteHaptics_Selection();

        /// <summary>UINotificationFeedbackGenerator notificationOccurred for a UINotificationFeedbackType
        /// (0-2).</summary>
        [DllImport("__Internal")]
        internal static extern void GhumanteHaptics_Notification(int type);

        /// <summary>1 when the device has a Taptic Engine (an iPhone, not an iPad and not an app on a Mac), else 0
        /// (an int, not a C++ bool, so the marshalled width is unambiguous).</summary>
        [DllImport("__Internal")]
        internal static extern int GhumanteHaptics_IsSupported();

        /// <summary>UIAccessibilityIsReduceMotionEnabled as 0 or 1 (an int, not a C++ bool, so the marshalled
        /// width is unambiguous).</summary>
        [DllImport("__Internal")]
        internal static extern int GhumanteHaptics_ReduceMotionEnabled();
    }
}
#endif
