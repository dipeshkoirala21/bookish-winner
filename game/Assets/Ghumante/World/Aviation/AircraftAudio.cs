using Ghumante.Core.Aviation;
using Ghumante.Core.Synth;

namespace Ghumante.World.Aviation
{
    /// <summary>How an aircraft snapshot drives its synth voice (W2_DESIGN 8.6): engine power and reverse by flight
    /// phase, and the voice class of each aircraft class. Engine-free.</summary>
    public static class AircraftAudio
    {
        /// <summary>Engine power and reverse by flight phase (take-off full power, approach 40%, rollout reverse).</summary>
        public static void Power(in AircraftState a, out float thrust, out float reverse)
        {
            reverse = 0f;
            switch (a.Phase)
            {
                case FlightPhase.TakeoffRoll:
                case FlightPhase.Climb: thrust = 1f; return;
                case FlightPhase.Approach: thrust = 0.4f; return;
                case FlightPhase.Rollout:
                    thrust = 0.3f;
                    reverse = a.SpeedMps > 25f ? 0.8f : 0f;
                    return;
                case FlightPhase.Hover: thrust = 0.7f; return;
                case FlightPhase.TaxiIn:
                case FlightPhase.TaxiOut:
                case FlightPhase.Backtrack:
                case FlightPhase.LineUp: thrust = 0.15f; return;
                default: thrust = 0f; return;
            }
        }

        public static AircraftSoundClass SoundClassOf(AircraftClass c)
        {
            switch (c)
            {
                case AircraftClass.Stol: return AircraftSoundClass.StolTurboprop;
                case AircraftClass.Narrowbody: return AircraftSoundClass.NarrowBody;
                case AircraftClass.Widebody: return AircraftSoundClass.WideBody;
                case AircraftClass.Helicopter: return AircraftSoundClass.Helicopter;
                default: return AircraftSoundClass.Turboprop;
            }
        }

    }
}
