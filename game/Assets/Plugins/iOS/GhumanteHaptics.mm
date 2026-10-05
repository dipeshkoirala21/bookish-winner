// Ghumante haptics and accessibility bridge for iOS (ARCHITECTURE.md 7.9).
//
// Called from C# through [DllImport("__Internal")] in Assets/Ghumante/Platform/Native/IosPlugin.cs; Unity
// copies this file into the Xcode project and links it into UnityFramework. Unity runs its player loop on
// the main thread, so every call here arrives on the main thread, as UIKit requires.
//
// Memory management: the generators live in static variables, are created once on first use and are kept
// for the life of the process. The file uses no retain/release/autorelease and no bridging casts, so it is
// correct both with ARC (the statics are __strong and own the +1 object returned by alloc/init) and without
// it (the +1 object from alloc/init is simply never released).
//
// GhumanteHaptics_Prepare creates and prepares the common generators ahead of use, and every generator is
// prepared again right after it fires, so a follow-up within the next few seconds (rapid taps) plays with
// minimal latency. Devices without a Taptic Engine, and players who turned off Settings > Sounds & Haptics >
// System Haptics, get silence; UIKit handles both. GhumanteHaptics_IsSupported tells the game whether the
// device has a Taptic Engine at all, so Settings can say so instead of offering a switch that does nothing.

#import <UIKit/UIKit.h>

// The C entry points, declared up front (also keeps -Wmissing-prototypes quiet).
extern "C" {
void GhumanteHaptics_Prepare(void);
void GhumanteHaptics_Impact(int style);
void GhumanteHaptics_Selection(void);
void GhumanteHaptics_Notification(int type);
int GhumanteHaptics_IsSupported(void);
int GhumanteHaptics_ReduceMotionEnabled(void);
}

static UISelectionFeedbackGenerator *s_selection = nil;
static UINotificationFeedbackGenerator *s_notification = nil;
// Indexed by UIImpactFeedbackStyle: Light 0, Medium 1, Heavy 2, Soft 3, Rigid 4 (Soft and Rigid: iOS 13+).
static UIImpactFeedbackGenerator *s_impact[5] = { nil, nil, nil, nil, nil };

static const int kImpactStyleCount = 5;

static void GhumanteEnsureSelection(void)
{
    if (s_selection == nil)
    {
        s_selection = [[UISelectionFeedbackGenerator alloc] init];
    }
}

static void GhumanteEnsureNotification(void)
{
    if (s_notification == nil)
    {
        s_notification = [[UINotificationFeedbackGenerator alloc] init];
    }
}

static void GhumanteEnsureImpact(int style)
{
    if (s_impact[style] == nil)
    {
        s_impact[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:(UIImpactFeedbackStyle)style];
    }
}

extern "C" {

// Creates the generators the menus use most and prepares them (call when a haptic is expected soon).
void GhumanteHaptics_Prepare(void)
{
    GhumanteEnsureSelection();
    GhumanteEnsureNotification();
    GhumanteEnsureImpact((int)UIImpactFeedbackStyleLight);
    GhumanteEnsureImpact((int)UIImpactFeedbackStyleMedium);
    GhumanteEnsureImpact((int)UIImpactFeedbackStyleHeavy);
    [s_selection prepare];
    [s_notification prepare];
    [s_impact[UIImpactFeedbackStyleLight] prepare];
    [s_impact[UIImpactFeedbackStyleMedium] prepare];
    [s_impact[UIImpactFeedbackStyleHeavy] prepare];
}

// style: UIImpactFeedbackStyle raw value (Light 0, Medium 1, Heavy 2, Soft 3, Rigid 4). Out of range = Medium.
void GhumanteHaptics_Impact(int style)
{
    if (style < 0 || style >= kImpactStyleCount) style = (int)UIImpactFeedbackStyleMedium;
    GhumanteEnsureImpact(style);
    [s_impact[style] impactOccurred];
    [s_impact[style] prepare];
}

void GhumanteHaptics_Selection(void)
{
    GhumanteEnsureSelection();
    [s_selection selectionChanged];
    [s_selection prepare];
}

// type: UINotificationFeedbackType raw value (Success 0, Warning 1, Error 2). Out of range = Warning.
void GhumanteHaptics_Notification(int type)
{
    if (type < (int)UINotificationFeedbackTypeSuccess || type > (int)UINotificationFeedbackTypeError)
    {
        type = (int)UINotificationFeedbackTypeWarning;
    }
    GhumanteEnsureNotification();
    [s_notification notificationOccurred:(UINotificationFeedbackType)type];
    [s_notification prepare];
}

// 1 when UIFeedbackGenerator can be felt here, else 0. Every iPhone that runs iOS 16 (our floor) has a Taptic
// Engine; iPads have none, and an iPhone or iPad app running on an Apple-silicon Mac has none either (it may
// report the phone idiom there). Uses UIKit only, so no extra framework has to be linked (CoreHaptics'
// CHHapticEngine.capabilitiesForHardware would need one). Returns int, not bool: unambiguous marshalled width.
int GhumanteHaptics_IsSupported(void)
{
    if ([[NSProcessInfo processInfo] isiOSAppOnMac]) return 0;
    return [[UIDevice currentDevice] userInterfaceIdiom] == UIUserInterfaceIdiomPhone ? 1 : 0;
}

// 1 when Settings > Accessibility > Motion > Reduce Motion is on, else 0. Returns int, not bool, so the
// marshalled width is unambiguous.
int GhumanteHaptics_ReduceMotionEnabled(void)
{
    return UIAccessibilityIsReduceMotionEnabled() ? 1 : 0;
}

} // extern "C"
