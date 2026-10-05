using System;

namespace Ghumante.Core.Aviation
{
    /// <summary>Generic aircraft classes at TIA (W2_DESIGN 8.5; append-only).</summary>
    public enum AircraftClass : byte
    {
        /// <summary>High-wing twin turboprop (ghm_veh_air_turboprop_a).</summary>
        Turboprop = 0,

        /// <summary>The stretched turboprop preset (same model, longer).</summary>
        TurbopropStretch = 1,

        /// <summary>Strutted high-wing STOL (ghm_veh_stol_turboprop_a).</summary>
        Stol = 2,

        /// <summary>Twin-fan narrow-body jet (ghm_veh_air_narrowbody_a).</summary>
        Narrowbody = 3,

        /// <summary>Twin-aisle wide-body jet (ghm_veh_air_widebody_a).</summary>
        Widebody = 4,

        /// <summary>Single-engine helicopter (ghm_veh_helicopter_a).</summary>
        Helicopter = 5,
    }

    /// <summary>Schedule classes of the movement table (W2_DESIGN 8.2).</summary>
    public enum ScheduleClass : byte
    {
        DomesticTurboprop = 0,
        Helicopter = 1,
        InternationalNarrowbody = 2,
        InternationalWidebody = 3,
    }

    /// <summary>Weather as the airport sees it.</summary>
    public enum WeatherKind : byte
    {
        Clear = 0,
        Haze = 1,
        Rain = 2,

        /// <summary>Winter fog: domestic and helicopter movements stop until 10:00, then the backlog clears by 14:00.</summary>
        Fog = 3,

        /// <summary>Monsoon thunderstorm: domestic × 0.7 and helicopters × 0.4 (14:00–18:00, June–September).</summary>
        Storm = 4,
    }

    /// <summary>Exterior lights (14 CFR §25.1385–1401): navigation (red left, green right, white tail), red anti-collision
    /// beacon, white strobes on the runway and airborne, landing lights below 3,050 m, taxi light on the ground.</summary>
    [Flags]
    public enum LightState : byte
    {
        None = 0,
        Navigation = 1,
        Beacon = 2,
        Strobe = 4,
        Landing = 8,
        Taxi = 16,
    }

    /// <summary>What an aircraft is doing.</summary>
    public enum FlightPhase : byte
    {
        Approach = 0,
        Rollout = 1,
        TaxiIn = 2,
        Parked = 3,
        TaxiOut = 4,
        Backtrack = 5,
        LineUp = 6,
        TakeoffRoll = 7,
        Climb = 8,
        Hover = 9,
    }

    /// <summary>
    /// One aircraft of the snapshot (W2_DESIGN 10.3). Game metres (X east, Z north) as doubles and Y absolute metres;
    /// heading 0 = north, clockwise; pitch nose-up and bank right-wing-down positive. <see cref="Livery"/> indexes the
    /// sidecar's fixed-wing liveries (helicopters: their colours).
    /// </summary>
    public struct AircraftState
    {
        public int Id;
        public AircraftClass Class;
        public byte Livery;
        public double X, Z;
        public float Y, HeadingRad, PitchRad, BankRad, SpeedMps, GearDown01, PropRpm;
        public LightState Lights;
        public bool OnGround;
        public FlightPhase Phase;
    }

    /// <summary>One scheduled movement (the day's log, tests and the debug HUD).</summary>
    public struct Movement
    {
        public int AircraftId;
        public ScheduleClass Class;
        public AircraftClass Aircraft;
        public bool Arrival;

        /// <summary>Real seconds (sim clock) at which the movement was drawn from the schedule.</summary>
        public double EmittedAt;

        /// <summary>Runway time (touchdown or start of the take-off roll; helicopters: lift-off or landing on the apron).</summary>
        public double SlotAt;

        public string Procedure;
        public string Runway;
    }
}
