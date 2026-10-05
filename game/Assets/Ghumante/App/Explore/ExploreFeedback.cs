using Ghumante.Core.Driving;
using Ghumante.Core.Services;

namespace Ghumante.App.Explore
{
    /// <summary>
    /// Which haptic a frame of driving plays (UI/README.md, "Haptics"): stuck recovery MediumImpact, a bump or a firm
    /// landing LightImpact, a change of surface Selection (on the scooter only: on foot every kerb would tick). At most one
    /// per frame, the strongest; the platform's rate limiter spaces them further. Arrival's Success comes from the HUD's
    /// celebration. Engine-free.
    /// </summary>
    public static class ExploreFeedback
    {
        /// <summary>A landing slower than this (m/s) is not felt.</summary>
        public const float FeltLandingMps = 2.5f;

        /// <summary>The haptic for this frame's <paramref name="events"/>, or false for none.</summary>
        public static bool HapticFor(StepEvents events, bool riding, float landingSpeedMps, out HapticKind kind)
        {
            if ((events & StepEvents.StuckRecovered) != 0)
            {
                kind = HapticKind.MediumImpact;
                return true;
            }
            if ((events & StepEvents.Bump) != 0 ||
                (events & StepEvents.Landed) != 0 && landingSpeedMps >= FeltLandingMps)
            {
                kind = HapticKind.LightImpact;
                return true;
            }
            if (riding && (events & StepEvents.SurfaceChanged) != 0)
            {
                kind = HapticKind.Selection;
                return true;
            }
            kind = HapticKind.Selection;
            return false;
        }
    }
}
