using System.Reflection;
using Ghumante.Core.Aviation;

namespace Ghumante.Core.Tests
{
    /// <summary>Helpers shared by the flight repro tests (so the same test text runs on the base build, where the
    /// ambient traffic has no runway hold, and after the flight package).</summary>
    internal static class FlightReproSupport
    {
        /// <summary>Asks the ambient traffic to keep off the runway for the player (no-op where the sim cannot).</summary>
        public static bool HoldRunway(AirTrafficSim sim, bool hold)
        {
            PropertyInfo p = typeof(AirTrafficSim).GetProperty("HoldRunwayForPlayer");
            if (p == null || !p.CanWrite) return false;
            p.SetValue(sim, hold);
            return true;
        }
    }
}
