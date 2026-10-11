using System;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// The aircraft the player can fly from Tribhuvan International Airport (docs/research/w2/ref_flight.md): the common
    /// types of Nepal's domestic fleet as generic, unbranded cartoon models. Append-only (saved stand ids use it).
    /// </summary>
    public enum FlightCraft : byte
    {
        /// <summary>High-wing twin turboprop, T-tail, six-blade props, gear in fuselage sponsons (ATR 72-class: the
        /// commonest domestic airliner in Nepal).</summary>
        Turboprop = 0,

        /// <summary>The longer high-wing twin with long nacelles that hold the main gear (Dash 8-400-class).</summary>
        TurbopropStretch = 1,

        /// <summary>Low-wing 29-seat commuter twin with a cruciform tail and five-blade props (Jetstream 41-class).</summary>
        Commuter = 2,

        /// <summary>Strutted high-wing STOL twin with fixed tricycle gear (Twin Otter-class: the mountain airstrip
        /// workhorse).</summary>
        Stol = 3,

        /// <summary>Single-engine light helicopter with skids and a three-blade rotor (H125 / AS350 B3-class).</summary>
        Helicopter = 4,
    }

    /// <summary>
    /// What one <see cref="FlightCraft"/> is like: real outer dimensions (manufacturer data, ref_flight.md §2) and an
    /// arcade-but-believable performance set (stall, rotate and top speeds, thrust, roll and pitch rates). The aero
    /// coefficients the model uses (<see cref="ClMaxClean"/>, <see cref="ClMaxFull"/>, <see cref="PowerEta"/> ...) are
    /// derived from those numbers by <see cref="For"/>, so a type flies at its real stall speed at the airport's
    /// elevation and reaches its real top speed at 3,000 m. Engine-free, immutable after <see cref="For"/>.
    /// </summary>
    public sealed class FlightSpec
    {
        /// <summary>Standard gravity (m/s²).</summary>
        public const float G = 9.81f;

        /// <summary>Elevation the stall speeds are quoted at (TIA, about 1,340 m).</summary>
        public const float ReferenceAltitudeM = 1340f;

        /// <summary>Altitude at which <see cref="MaxSpeedMps"/> is reached at full power.</summary>
        public const float CruiseAltitudeM = 3000f;

        /// <summary>Lift slope per radian of angle of attack (whole aircraft) and the clean zero-alpha lift.</summary>
        public const float ClAlpha = 6.5f, Cl0Clean = 0.5f;

        public FlightCraft Craft;

        /// <summary>Overall length, span (rotor diameter for the helicopter) and height, metres.</summary>
        public float LengthM, SpanM, HeightM;

        /// <summary>Fuselage radius (cartoon, slightly boldened) and its centre height above the ground at rest.</summary>
        public float FuselageRadiusM, FuselageCentreM;

        /// <summary>Flying mass (kg) and wing area (m²).</summary>
        public float MassKg, WingAreaM2;

        /// <summary>Stall speed clean and with full flaps at <see cref="ReferenceAltitudeM"/>, rotate speed, and top speed
        /// at <see cref="CruiseAltitudeM"/> (m/s).</summary>
        public float StallCleanMps, StallFullMps, RotateMps, MaxSpeedMps;

        /// <summary>Static thrust (N, sea level, full power) and the engines' spool time to full power (s).</summary>
        public float StaticThrustN, SpoolS;

        /// <summary>Clean zero-lift drag coefficient and the extra drag of the gear and of each flap stage.</summary>
        public float Cd0, CdGear, CdFlap1, CdFlap2;

        /// <summary>Roll and pitch rates at full stick, nose-wheel steering lock, rudder yaw rate (rad/s, rad).</summary>
        public float RollRate, PitchRate, NoseWheelLock, RudderRate;

        /// <summary>Bank and pitch limits (rad).</summary>
        public float MaxBank, MaxPitchUp, MaxPitchDown;

        /// <summary>Distance from the nose wheel to the main wheels (m) and the main-gear track (m).</summary>
        public float WheelbaseM, TrackM;

        /// <summary>Most nose-up attitude on the ground before the tail would touch (rad).</summary>
        public float TailStrike;

        /// <summary>Fixed gear (no retraction).</summary>
        public bool FixedGear;

        /// <summary>A rotorcraft (hover, collective, cyclic).</summary>
        public bool Rotorcraft;

        /// <summary>Pilot's eye in the aircraft frame (x right, y up from the ground at rest, z forward from the centre).</summary>
        public float EyeX, EyeY, EyeZ;

        /// <summary>Main door (left side) in the aircraft frame: where the pilot climbs in and out.</summary>
        public float DoorX, DoorZ;

        // ---- derived (For) ----

        /// <summary>Maximum lift coefficient clean and with full flaps.</summary>
        public float ClMaxClean, ClMaxHalf, ClMaxFull;

        /// <summary>Zero-alpha lift with half and full flaps.</summary>
        public float Cl0Half, Cl0Full;

        /// <summary>Induced drag factor k = 1 / (π · AR · e).</summary>
        public float InducedK;

        /// <summary>Shaft power × propeller efficiency at sea level (W): thrust = min(static, PowerEta / V).</summary>
        public float PowerEta;

        /// <summary>Weight (N).</summary>
        public float WeightN
        {
            get { return MassKg * G; }
        }

        /// <summary>The longest of length and span: the size the camera frames.</summary>
        public float Size
        {
            get { return Math.Max(LengthM, SpanM); }
        }

        private static readonly FlightSpec[] Cache = new FlightSpec[5];

        /// <summary>The spec of a craft (built once, shared, never changed).</summary>
        public static FlightSpec For(FlightCraft craft)
        {
            int i = (int)craft;
            if (i < 0 || i >= Cache.Length) throw new ArgumentOutOfRangeException(nameof(craft));
            FlightSpec s = Cache[i];
            if (s != null) return s;
            s = Make(craft);
            s.Derive();
            Cache[i] = s;
            return s;
        }

        /// <summary>Every craft in enum order.</summary>
        public static readonly FlightCraft[] All =
        {
            FlightCraft.Turboprop, FlightCraft.TurbopropStretch, FlightCraft.Commuter, FlightCraft.Stol, FlightCraft.Helicopter,
        };

        private const float Deg = (float)(Math.PI / 180.0);
        private const float Kt = 0.514444f;

        private static FlightSpec Make(FlightCraft c)
        {
            switch (c)
            {
                case FlightCraft.TurbopropStretch:
                    return new FlightSpec
                    {
                        Craft = c, LengthM = 32.8f, SpanM = 28.4f, HeightM = 8.34f, FuselageRadiusM = 1.42f, FuselageCentreM = 2.05f,
                        MassKg = 25000f, WingAreaM2 = 63.1f, StallCleanMps = 108 * Kt, StallFullMps = 88 * Kt, RotateMps = 117 * Kt,
                        MaxSpeedMps = 330 * Kt, StaticThrustN = 56000f, SpoolS = 2.2f, Cd0 = 0.026f, CdGear = 0.016f, CdFlap1 = 0.018f,
                        CdFlap2 = 0.055f, RollRate = 32 * Deg, PitchRate = 7 * Deg, NoseWheelLock = 60 * Deg, RudderRate = 4 * Deg,
                        MaxBank = 55 * Deg, MaxPitchUp = 25 * Deg, MaxPitchDown = 30 * Deg, WheelbaseM = 13.9f, TrackM = 8.8f,
                        TailStrike = 8 * Deg, EyeX = -0.55f, EyeY = 2.95f, EyeZ = 14.1f, DoorX = -1.55f, DoorZ = 11.5f,
                    };
                case FlightCraft.Commuter:
                    return new FlightSpec
                    {
                        Craft = c, LengthM = 19.25f, SpanM = 18.29f, HeightM = 5.74f, FuselageRadiusM = 1.08f, FuselageCentreM = 1.75f,
                        MassKg = 9500f, WingAreaM2 = 32.6f, StallCleanMps = 104 * Kt, StallFullMps = 88 * Kt, RotateMps = 110 * Kt,
                        MaxSpeedMps = 290 * Kt, StaticThrustN = 24000f, SpoolS = 1.8f, Cd0 = 0.027f, CdGear = 0.017f, CdFlap1 = 0.018f,
                        CdFlap2 = 0.05f, RollRate = 45 * Deg, PitchRate = 9 * Deg, NoseWheelLock = 60 * Deg, RudderRate = 5 * Deg,
                        MaxBank = 60 * Deg, MaxPitchUp = 25 * Deg, MaxPitchDown = 30 * Deg, WheelbaseM = 7.1f, TrackM = 5.9f,
                        TailStrike = 10 * Deg, EyeX = -0.45f, EyeY = 2.25f, EyeZ = 7.75f, DoorX = -1.15f, DoorZ = 6.2f,
                    };
                case FlightCraft.Stol:
                    return new FlightSpec
                    {
                        Craft = c, LengthM = 15.77f, SpanM = 19.81f, HeightM = 5.94f, FuselageRadiusM = 0.92f, FuselageCentreM = 1.55f,
                        MassKg = 5000f, WingAreaM2 = 39f, StallCleanMps = 66 * Kt, StallFullMps = 56 * Kt, RotateMps = 68 * Kt,
                        MaxSpeedMps = 175 * Kt, StaticThrustN = 17000f, SpoolS = 1.5f, Cd0 = 0.042f, CdGear = 0f, CdFlap1 = 0.02f,
                        CdFlap2 = 0.06f, RollRate = 50 * Deg, PitchRate = 11 * Deg, NoseWheelLock = 60 * Deg, RudderRate = 7 * Deg,
                        MaxBank = 60 * Deg, MaxPitchUp = 25 * Deg, MaxPitchDown = 30 * Deg, WheelbaseM = 4.5f, TrackM = 3.7f,
                        TailStrike = 11 * Deg, FixedGear = true, EyeX = -0.4f, EyeY = 2.15f, EyeZ = 5.4f, DoorX = -1.0f, DoorZ = 4.6f,
                    };
                case FlightCraft.Helicopter:
                    return new FlightSpec
                    {
                        Craft = c, LengthM = 12.94f, SpanM = 10.69f, HeightM = 3.34f, FuselageRadiusM = 0.95f, FuselageCentreM = 1.35f,
                        MassKg = 2000f, WingAreaM2 = 0f, StallCleanMps = 0f, StallFullMps = 0f, RotateMps = 0f, MaxSpeedMps = 135 * Kt,
                        StaticThrustN = 0f, SpoolS = 6f, RollRate = 60 * Deg, PitchRate = 45 * Deg, RudderRate = 50 * Deg,
                        MaxBank = 28 * Deg, MaxPitchUp = 18 * Deg, MaxPitchDown = 22 * Deg, WheelbaseM = 3.2f, TrackM = 2.2f,
                        TailStrike = 20 * Deg, FixedGear = true, Rotorcraft = true, EyeX = 0.35f, EyeY = 1.95f, EyeZ = 1.55f, DoorX = -1.05f,
                        DoorZ = 0.6f,
                    };
                default:
                    return new FlightSpec
                    {
                        Craft = FlightCraft.Turboprop, LengthM = 27.17f, SpanM = 27.05f, HeightM = 7.65f, FuselageRadiusM = 1.5f,
                        FuselageCentreM = 2.0f, MassKg = 19000f, WingAreaM2 = 61f, StallCleanMps = 98 * Kt, StallFullMps = 82 * Kt,
                        RotateMps = 107 * Kt, MaxSpeedMps = 280 * Kt, StaticThrustN = 40000f, SpoolS = 2f, Cd0 = 0.028f, CdGear = 0.016f,
                        CdFlap1 = 0.018f, CdFlap2 = 0.055f, RollRate = 35 * Deg, PitchRate = 8 * Deg, NoseWheelLock = 60 * Deg,
                        RudderRate = 4 * Deg, MaxBank = 55 * Deg, MaxPitchUp = 25 * Deg, MaxPitchDown = 30 * Deg, WheelbaseM = 10.8f,
                        TrackM = 4.1f, TailStrike = 9 * Deg, EyeX = -0.55f, EyeY = 2.85f, EyeZ = 11.3f, DoorX = -1.6f, DoorZ = -9.0f,
                    };
            }
        }

        /// <summary>Air density (kg/m³) of the ISA troposphere at <paramref name="altitudeM"/>.</summary>
        public static float Density(float altitudeM)
        {
            double h = Math.Max(-500.0, Math.Min(11000.0, altitudeM));
            return (float)(1.225 * Math.Pow(1.0 - 2.25577e-5 * h, 4.25588));
        }

        private void Derive()
        {
            if (Rotorcraft)
            {
                InducedK = 0f;
                return;
            }
            float rho0 = Density(ReferenceAltitudeM);
            float w = WeightN;
            ClMaxClean = 2f * w / (rho0 * WingAreaM2 * StallCleanMps * StallCleanMps);
            ClMaxFull = 2f * w / (rho0 * WingAreaM2 * StallFullMps * StallFullMps);
            ClMaxHalf = 0.5f * (ClMaxClean + ClMaxFull);
            // Flaps shift the lift curve up by most of what they add to its maximum, so the stall angle barely moves.
            Cl0Full = Cl0Clean + 0.8f * (ClMaxFull - ClMaxClean);
            Cl0Half = Cl0Clean + 0.8f * (ClMaxHalf - ClMaxClean);
            float ar = SpanM * SpanM / WingAreaM2;
            InducedK = (float)(1.0 / (Math.PI * ar * 0.8));
            // Top speed at the cruise altitude with full power and gear up: thrust (PowerEta · σ^0.7 / V) equals drag there.
            float rho = Density(CruiseAltitudeM), v = MaxSpeedMps;
            float q = 0.5f * rho * v * v;
            float cl = w / (q * WingAreaM2);
            float drag = q * WingAreaM2 * (Cd0 + InducedK * cl * cl);
            float sigma = (float)Math.Pow(rho / 1.225f, 0.7);
            PowerEta = drag * v / sigma;
        }

        /// <summary>The stall angle of attack (rad) for a flap stage 0–2.</summary>
        public float StallAlpha(int flaps)
        {
            return (ClMax(flaps) - Cl0(flaps)) / ClAlpha;
        }

        /// <summary>Maximum lift coefficient for a flap stage 0–2.</summary>
        public float ClMax(int flaps)
        {
            return flaps <= 0 ? ClMaxClean : flaps == 1 ? ClMaxHalf : ClMaxFull;
        }

        /// <summary>Zero-alpha lift coefficient for a flap stage 0–2.</summary>
        public float Cl0(int flaps)
        {
            return flaps <= 0 ? Cl0Clean : flaps == 1 ? Cl0Half : Cl0Full;
        }

        /// <summary>Stall speed (m/s) at an altitude, a flap stage and a load factor.</summary>
        public float StallSpeed(float altitudeM, int flaps, float loadFactor = 1f)
        {
            if (Rotorcraft) return 0f;
            float rho = Density(altitudeM);
            return (float)Math.Sqrt(2.0 * WeightN * Math.Max(0.1f, loadFactor) / (rho * WingAreaM2 * ClMax(flaps)));
        }
    }
}
