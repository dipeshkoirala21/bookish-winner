using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// What the player (or an AI driver) is moving with: picks the handling preset family (W2_DESIGN 10.3,
    /// append-only; the byte values are stored in saves and in <see cref="VehicleCatalog"/>). Variants such as the
    /// tanker, the coach, the EV or the e-scooter keep their family's kind and differ by
    /// <see cref="HandlingPreset"/>.
    /// </summary>
    public enum VehicleKind : byte
    {
        Motorbike = 0,
        Taxi = 1,
        Walker = 2,
        Bicycle = 3,
        Scooter = 4,
        Car = 5,
        Suv = 6,
        Microbus = 7,
        Tempo = 8,
        Minibus = 9,
        Bus = 10,
        Truck = 11,
        Tractor = 12,
        Cruiser = 13,
    }

    /// <summary>
    /// Every handling preset (W2_DESIGN 6.3), the index stored as <see cref="VehicleCatalogEntry.HandlingPreset"/>.
    /// The first fourteen equal <see cref="VehicleKind"/>; the rest are variants of a kind. Append-only.
    /// </summary>
    public enum HandlingPreset : byte
    {
        Motorbike = 0,
        Taxi = 1,
        Walker = 2,
        Bicycle = 3,
        Scooter = 4,
        Hatchback = 5,
        Suv = 6,
        Microbus = 7,
        Tempo = 8,
        Minibus = 9,
        Bus = 10,
        Truck = 11,
        Tractor = 12,
        Cruiser = 13,
        Tanker = 14,
        Coach = 15,
        Ev = 16,
        EScooter = 17,
        Pickup = 18,
        Tipper = 19,
        SchoolBus = 20,
    }

    /// <summary>Where the driver sits (right-hand drive in Nepal; two-wheelers and the tempo are centred).</summary>
    public enum DriverSide : byte
    {
        Centre = 0,
        Left = 1,
        Right = 2,
    }

    /// <summary>
    /// Tuning of one <see cref="ArcadeVehicle"/> (ARCHITECTURE 7.6). Speeds are on flat ground; per-surface
    /// tables are indexed by <c>(int)SurfaceGroup</c>. The presets are the M1 tuning; gameplay code may clone and
    /// tweak them (a ScriptableObject in the Vehicles assembly can copy its fields into a spec). Call
    /// <see cref="Validate"/> after editing by hand.
    ///
    /// Wetness (0 dry .. 1 monsoon) makes DIRT behave like MUD: its grip and top-speed factor blend linearly to
    /// MUD's, and from 0.5 the effective surface reported to gameplay is MUD. Wet PAVED and GRAVEL lose a little
    /// grip (<see cref="WetPavedGripLoss"/>, <see cref="WetGravelGripLoss"/>) but keep their top speed.
    /// </summary>
    public sealed class VehicleSpec
    {
        public const int SurfaceGroupCount = 4;

        /// <summary>Longest supported <see cref="SteerInputDelayS"/> (the delay line holds this much input).</summary>
        public const float MaxSteerDelayS = 0.5f;

        public string Name = "";
        public VehicleKind Kind;

        /// <summary>Walker locomotion: speed is velocity-controlled, Steer turns in place at
        /// <see cref="TurnRateDegPerSec"/>, no drift, no slope gravity, never airborne from terrain.</summary>
        public bool TurnInPlace;

        // ---- speeds ----

        /// <summary>Top speed at full throttle on flat, dry PAVED (walker: running).</summary>
        public float MaxSpeedKmh;

        /// <summary>Walker only: speed at half throttle (walking). Throttle 0..0.5 walks, 0.5..1 blends to a run.</summary>
        public float WalkSpeedMps;

        /// <summary>Top-speed multiplier while Boost is held (walker: sprint). 1 = no boost.</summary>
        public float BoostSpeedFactor = 1f;

        public float ReverseSpeedKmh;

        // ---- longitudinal ----

        /// <summary>Drive acceleration from standstill on PAVED; it tapers as (1 − (v/target)²) towards the target
        /// speed. Walker: how fast the speed follows the stick.</summary>
        public float AccelMps2;

        /// <summary>Full-brake deceleration on PAVED (scaled by traction elsewhere).</summary>
        public float BrakeMps2;

        /// <summary>Rolling resistance while coasting.</summary>
        public float CoastDecelMps2;

        /// <summary>Extra deceleration while faster than the current surface's top speed (entering mud at speed,
        /// long descents: engine braking).</summary>
        public float OverSpeedDecelMps2;

        /// <summary>Reverse acceleration as a fraction of <see cref="AccelMps2"/>.</summary>
        public float ReverseAccelFactor = 0.6f;

        // ---- steering ----

        public float WheelbaseM;

        /// <summary>Front-wheel angle at full lock (low speed).</summary>
        public float MaxSteerDeg;

        /// <summary>How fast the wheel angle follows the steer input.</summary>
        public float SteerDegPerSec;

        /// <summary>Speed-dependent steering: at full lock the turn is limited to this lateral acceleration, so
        /// the turn radius grows with speed (v²/a). Below <see cref="LateralGripMps2"/> the vehicle grips on dry
        /// asphalt; where grip is lower (wet, gravel, dirt, mud) full lock asks for more than the surface gives and
        /// the vehicle drifts.</summary>
        public float SteerLateralMps2;

        /// <summary>Walker: yaw rate at full steer (turning in place).</summary>
        public float TurnRateDegPerSec;

        /// <summary>Clamp on the yaw rate (any speed).</summary>
        public float MaxYawRateDegPerSec = 240f;

        // ---- grip and drift ----

        /// <summary>Sideways grip on dry PAVED as an acceleration: the direction of travel can turn at most at
        /// <c>grip / speed</c> rad/s; beyond that the vehicle slides (drifts).</summary>
        public float LateralGripMps2;

        /// <summary>Largest drift angle between heading and direction of travel.</summary>
        public float MaxSlipDeg;

        /// <summary>Speed scrubbed while sliding, times |sin(slip)|.</summary>
        public float SlideDecelMps2;

        /// <summary>How fast the drift angle follows its target (1/s).</summary>
        public float DriftResponse = 5f;

        /// <summary>Traction per surface group (scales acceleration, braking, lateral grip, climbing).</summary>
        public float[] Grip = new float[SurfaceGroupCount];

        /// <summary>Top-speed factor per surface group.</summary>
        public float[] TopSpeedFactor = new float[SurfaceGroupCount];

        public float WetPavedGripLoss = 0.2f;
        public float WetGravelGripLoss = 0.1f;

        // ---- terrain ----

        /// <summary>Steepest climbable slope on full traction; with less traction the climbable grade shrinks in
        /// proportion (tan(max) × grip). Steeper: the wheels spin and gravity wins (walker: blocked).</summary>
        public float MaxSlopeDeg;

        /// <summary>How fast pitch and roll follow the ground normal (1/s, exponential smoothing).</summary>
        public float PitchRollResponse;

        /// <summary>Motorbike lean into turns (0 = none) and how fast it follows (1/s).</summary>
        public float MaxLeanDeg;

        public float LeanResponse = 8f;

        /// <summary>A Bump fires when the vertical speed jumps by at least this much against the terrain slope
        /// (kerb ramps, tile steps). 0 = no bump events.</summary>
        public float BumpThresholdMps;

        /// <summary>Leave the ground over a crest when moving up at least <see cref="LaunchMinVyMps"/> and the
        /// terrain's vertical speed drops by at least <see cref="LaunchDeltaVyMps"/> in one sub-step.
        /// <see cref="LaunchDeltaVyMps"/> 0 = never airborne from terrain.</summary>
        public float LaunchMinVyMps;

        public float LaunchDeltaVyMps;

        // ---- assists ----

        /// <summary>Road magnet: with gentle steering on a road the heading is pulled towards the road
        /// direction at this rate (1/s). 0 = off.</summary>
        public float RoadAssistPerS;

        /// <summary>Stuck recovery (ARCHITECTURE 7.6): after this long with throttle held and less than
        /// <see cref="StuckProgressM"/> of progress, hop and nudge. 0 = off.</summary>
        public float StuckSeconds;

        public float StuckProgressM = 1.5f;

        /// <summary>Upward speed of the recovery hop.</summary>
        public float RecoveryHopMps = 3.5f;

        /// <summary>How far the recovery nudge moves the vehicle.</summary>
        public float RecoveryNudgeM = 3f;

        /// <summary>How far to look for a road to hop onto.</summary>
        public float RecoverySearchM = 30f;

        // ---- W2 handling (W2_DESIGN 6.3; all optional, 0 = off) ----

        /// <summary>The preset this spec was made from (<see cref="Preset"/>).</summary>
        public HandlingPreset Preset;

        /// <summary>Pure delay between the steer input and the wheel (city bus 0.25 s).</summary>
        public float SteerInputDelayS;

        /// <summary>Visual body roll per m/s² of lateral acceleration (degrees; cars 0.6, buses and trucks 1.0), outward
        /// in a turn, capped at <see cref="VisualMaxRollDeg"/>. Physics stays flat.</summary>
        public float VisualRollPerMps2;

        public float VisualMaxRollDeg;

        /// <summary>Visual pitch per m/s² of longitudinal acceleration (degrees, nose up when accelerating, down when
        /// braking), capped at <see cref="VisualMaxPitchDeg"/>.</summary>
        public float VisualPitchPerMps2;

        public float VisualMaxPitchDeg;

        /// <summary>Body spring of the visual roll and pitch: natural frequency (Hz) and damping ratio. The tanker's
        /// water slosh is 0.8 Hz at ζ 0.3; the default 2.5 Hz at ζ 0.8 follows closely.</summary>
        public float BodySpringHz = 2.5f;

        public float BodySpringZeta = 0.8f;

        /// <summary>Two-wheelers never fall over: below <see cref="WobbleBelowKmh"/> the lean wobbles ±3° at 1.2 Hz,
        /// below 1 km/h a foot goes down (<see cref="ArcadeVehicle.FootDown"/>). Lean leads the yaw by
        /// <see cref="LeanLeadS"/>.</summary>
        public bool TwoWheeler;

        public float WobbleBelowKmh = 5f;
        public float WobbleDeg = 3f;
        public float WobbleHz = 1.2f;
        public float LeanLeadS;

        /// <summary>Boost only while stamina lasts (bicycle): stamina empties in <see cref="StaminaDrainS"/> of boost
        /// and refills in <see cref="StaminaRefillS"/> without it.</summary>
        public bool StaminaBoost;

        public float StaminaDrainS = 12f;
        public float StaminaRefillS = 8f;

        /// <summary>Uphill steeper than this grade (rise per metre) the rider pushes at <see cref="PushSpeedMps"/>
        /// (bicycle 0.10). 0 = never.</summary>
        public float PushGrade;

        public float PushSpeedMps = 1.2f;

        /// <summary>Top speed drops by <see cref="GradeSlowKmhPerPct"/> per percent of uphill grade above
        /// <see cref="GradeSlowFromPct"/> (bicycle: −1.5 km/h per % above 6 %).</summary>
        public float GradeSlowFromPct;

        public float GradeSlowKmhPerPct;

        /// <summary>Extra deceleration off the throttle (truck engine brake 1.2 m/s²).</summary>
        public float EngineBrakeMps2;

        /// <summary>Stronger acceleration below <see cref="LowSpeedKmh"/> (EV torque: 5.2 m/s² below 30 km/h). 0 = off.</summary>
        public float LowSpeedAccelMps2;

        public float LowSpeedKmh;

        /// <summary>Opt-in drift (taxi): brake + steer above <see cref="BrakeDriftMinKmh"/> drops the lateral grip to
        /// <see cref="BrakeDriftGrip"/> × for <see cref="BrakeDriftHoldS"/>, with <see cref="BrakeDriftCounterSteer"/>
        /// of the slip steered back. 0 = off.</summary>
        public float BrakeDriftMinKmh;

        public float BrakeDriftGrip = 0.6f;
        public float BrakeDriftHoldS = 0.8f;
        public float BrakeDriftCounterSteer = 0.5f;

        // ---- body (W2_DESIGN 5.2; vehicle frame: origin on the ground under the rear axle, +Z forward) ----

        public float LengthM, WidthM, HeightM;

        /// <summary>Body behind the rear axle (the bus's 2.6 m swings out in tight turns).</summary>
        public float RearOverhangM;

        /// <summary>Number of seat sockets (driver included); <see cref="VehicleSeats"/> places them.</summary>
        public int SeatSockets;

        public DriverSide DriverSide;

        /// <summary>Front overhang: length ahead of the front axle.</summary>
        public float FrontOverhangM
        {
            get { return Math.Max(0f, LengthM - WheelbaseM - RearOverhangM); }
        }

        /// <summary>Front-axle turning radius at full lock and walking speed: L / sin δ0 (W2_DESIGN 6.3 "Min R").</summary>
        public float MinTurnRadiusFrontM
        {
            get { return TurnInPlace ? 0f : WheelbaseM / MathF.Sin(MaxSteerDeg * (float)(Math.PI / 180.0)); }
        }

        /// <summary>Speed-sensitive steering limit δmax(v) = min(δ0, atan(L·aLat / v²)) in radians, with
        /// aLat = <see cref="SteerLateralMps2"/> (W2_DESIGN 6.3).</summary>
        public float MaxSteerAtRad(float speedMps)
        {
            float d0 = MaxSteerDeg * (float)(Math.PI / 180.0);
            float v2 = speedMps * speedMps;
            if (v2 < 1e-4f) return d0;
            float d = MathF.Atan(WheelbaseM * SteerLateralMps2 / v2);
            return d < d0 ? d : d0;
        }

        /// <summary>Outer front corner radius at full lock and walking speed (the body sweep; bus ≤ 11 m).</summary>
        public float BodySweepRadiusM
        {
            get
            {
                if (TurnInPlace) return 0.5f * Math.Max(LengthM, WidthM);
                float rear = WheelbaseM / MathF.Tan(MaxSteerDeg * (float)(Math.PI / 180.0));
                float x = rear + 0.5f * WidthM, z = WheelbaseM + FrontOverhangM;
                return MathF.Sqrt(x * x + z * z);
            }
        }

        public float MaxSpeedMps
        {
            get { return MaxSpeedKmh / 3.6f; }
        }

        public float ReverseSpeedMps
        {
            get { return ReverseSpeedKmh / 3.6f; }
        }

        /// <summary>The surface group gameplay sees: DIRT reads as MUD from wetness 0.5.</summary>
        public static SurfaceGroup Effective(SurfaceGroup g, float wetness01)
        {
            return g == SurfaceGroup.Dirt && wetness01 >= 0.5f ? SurfaceGroup.Mud : g;
        }

        private static float Clamp01(float w)
        {
            return w > 0f ? (w < 1f ? w : 1f) : 0f;
        }

        private static int Index(SurfaceGroup g)
        {
            int i = (int)g;
            return i < SurfaceGroupCount ? i : (int)SurfaceGroup.Dirt;
        }

        /// <summary>Traction on a surface at a wetness.</summary>
        public float GripOf(SurfaceGroup g, float wetness01)
        {
            float w = Clamp01(wetness01);
            switch (g)
            {
                case SurfaceGroup.Paved:
                    return Grip[0] * (1f - WetPavedGripLoss * w);
                case SurfaceGroup.Gravel:
                    return Grip[1] * (1f - WetGravelGripLoss * w);
                case SurfaceGroup.Dirt:
                    return Grip[2] + (Grip[3] - Grip[2]) * w;
                default:
                    return Grip[Index(g)];
            }
        }

        /// <summary>Top-speed factor on a surface at a wetness.</summary>
        public float TopSpeedFactorOf(SurfaceGroup g, float wetness01)
        {
            if (g == SurfaceGroup.Dirt) return TopSpeedFactor[2] + (TopSpeedFactor[3] - TopSpeedFactor[2]) * Clamp01(wetness01);
            return TopSpeedFactor[Index(g)];
        }

        /// <summary>Top speed in m/s on a surface (flat, full throttle, no boost).</summary>
        public float TopSpeedMps(SurfaceGroup g, float wetness01)
        {
            return MaxSpeedMps * TopSpeedFactorOf(g, wetness01);
        }

        private void SetTables(float paved, float gravel, float dirt, float mud, float tPaved, float tGravel, float tDirt, float tMud)
        {
            Grip[0] = paved;
            Grip[1] = gravel;
            Grip[2] = dirt;
            Grip[3] = mud;
            TopSpeedFactor[0] = tPaved;
            TopSpeedFactor[1] = tGravel;
            TopSpeedFactor[2] = tDirt;
            TopSpeedFactor[3] = tMud;
        }

        /// <summary>A deep copy that can be tuned without touching the original.</summary>
        public VehicleSpec Clone()
        {
            var c = (VehicleSpec)MemberwiseClone();
            c.Grip = (float[])Grip.Clone();
            c.TopSpeedFactor = (float[])TopSpeedFactor.Clone();
            return c;
        }

        /// <summary>Throws <see cref="ArgumentException"/> when a value would break the model (negative or
        /// non-finite tuning, a zero top speed).</summary>
        public void Validate()
        {
            Positive(MaxSpeedKmh, nameof(MaxSpeedKmh));
            Positive(AccelMps2, nameof(AccelMps2));
            Positive(BrakeMps2, nameof(BrakeMps2));
            NonNegative(ReverseSpeedKmh, nameof(ReverseSpeedKmh));
            NonNegative(CoastDecelMps2, nameof(CoastDecelMps2));
            NonNegative(OverSpeedDecelMps2, nameof(OverSpeedDecelMps2));
            NonNegative(MaxSlopeDeg, nameof(MaxSlopeDeg));
            NonNegative(PitchRollResponse, nameof(PitchRollResponse));
            NonNegative(MaxLeanDeg, nameof(MaxLeanDeg));
            NonNegative(LeanResponse, nameof(LeanResponse));
            NonNegative(BumpThresholdMps, nameof(BumpThresholdMps));
            NonNegative(LaunchMinVyMps, nameof(LaunchMinVyMps));
            NonNegative(LaunchDeltaVyMps, nameof(LaunchDeltaVyMps));
            NonNegative(RoadAssistPerS, nameof(RoadAssistPerS));
            NonNegative(StuckSeconds, nameof(StuckSeconds));
            NonNegative(StuckProgressM, nameof(StuckProgressM));
            NonNegative(RecoveryHopMps, nameof(RecoveryHopMps));
            NonNegative(RecoveryNudgeM, nameof(RecoveryNudgeM));
            NonNegative(RecoverySearchM, nameof(RecoverySearchM));
            NonNegative(ReverseAccelFactor, nameof(ReverseAccelFactor));
            NonNegative(MaxYawRateDegPerSec, nameof(MaxYawRateDegPerSec));
            NonNegative(SteerInputDelayS, nameof(SteerInputDelayS));
            if (SteerInputDelayS > MaxSteerDelayS) throw new ArgumentException("SteerInputDelayS must be at most " + MaxSteerDelayS);
            NonNegative(VisualRollPerMps2, nameof(VisualRollPerMps2));
            NonNegative(VisualMaxRollDeg, nameof(VisualMaxRollDeg));
            NonNegative(VisualPitchPerMps2, nameof(VisualPitchPerMps2));
            NonNegative(VisualMaxPitchDeg, nameof(VisualMaxPitchDeg));
            Positive(BodySpringHz, nameof(BodySpringHz));
            Positive(BodySpringZeta, nameof(BodySpringZeta));
            NonNegative(WobbleBelowKmh, nameof(WobbleBelowKmh));
            NonNegative(WobbleDeg, nameof(WobbleDeg));
            NonNegative(WobbleHz, nameof(WobbleHz));
            NonNegative(LeanLeadS, nameof(LeanLeadS));
            Positive(StaminaDrainS, nameof(StaminaDrainS));
            Positive(StaminaRefillS, nameof(StaminaRefillS));
            NonNegative(PushGrade, nameof(PushGrade));
            NonNegative(PushSpeedMps, nameof(PushSpeedMps));
            NonNegative(GradeSlowFromPct, nameof(GradeSlowFromPct));
            NonNegative(GradeSlowKmhPerPct, nameof(GradeSlowKmhPerPct));
            NonNegative(EngineBrakeMps2, nameof(EngineBrakeMps2));
            NonNegative(LowSpeedAccelMps2, nameof(LowSpeedAccelMps2));
            NonNegative(LowSpeedKmh, nameof(LowSpeedKmh));
            NonNegative(BrakeDriftMinKmh, nameof(BrakeDriftMinKmh));
            Positive(BrakeDriftGrip, nameof(BrakeDriftGrip));
            NonNegative(BrakeDriftHoldS, nameof(BrakeDriftHoldS));
            NonNegative(BrakeDriftCounterSteer, nameof(BrakeDriftCounterSteer));
            NonNegative(LengthM, nameof(LengthM));
            NonNegative(WidthM, nameof(WidthM));
            NonNegative(HeightM, nameof(HeightM));
            NonNegative(RearOverhangM, nameof(RearOverhangM));
            if (SeatSockets < 0) throw new ArgumentException("SeatSockets must be >= 0");
            if (MaxLeanDeg > 45f) throw new ArgumentException("MaxLeanDeg must be at most 45 (two-wheelers never tip)");
            if (!(BoostSpeedFactor >= 1f) || float.IsInfinity(BoostSpeedFactor)) throw new ArgumentException("BoostSpeedFactor must be >= 1");
            if (MaxSlopeDeg >= 89f) throw new ArgumentException("MaxSlopeDeg must be below 89");
            if (TurnInPlace)
            {
                NonNegative(WalkSpeedMps, nameof(WalkSpeedMps));
                Positive(TurnRateDegPerSec, nameof(TurnRateDegPerSec));
            }
            else
            {
                Positive(WheelbaseM, nameof(WheelbaseM));
                Positive(MaxSteerDeg, nameof(MaxSteerDeg));
                Positive(SteerDegPerSec, nameof(SteerDegPerSec));
                Positive(SteerLateralMps2, nameof(SteerLateralMps2));
                Positive(LateralGripMps2, nameof(LateralGripMps2));
                NonNegative(MaxSlipDeg, nameof(MaxSlipDeg));
                NonNegative(SlideDecelMps2, nameof(SlideDecelMps2));
                Positive(DriftResponse, nameof(DriftResponse));
                if (MaxSteerDeg >= 80f) throw new ArgumentException("MaxSteerDeg must be below 80");
            }
            if (Grip == null || Grip.Length != SurfaceGroupCount || TopSpeedFactor == null || TopSpeedFactor.Length != SurfaceGroupCount)
                throw new ArgumentException("Grip and TopSpeedFactor need one entry per SurfaceGroup");
            for (int i = 0; i < SurfaceGroupCount; i++)
            {
                Positive(Grip[i], "Grip[" + (SurfaceGroup)i + "]");
                Positive(TopSpeedFactor[i], "TopSpeedFactor[" + (SurfaceGroup)i + "]");
            }
            NonNegative(WetPavedGripLoss, nameof(WetPavedGripLoss));
            NonNegative(WetGravelGripLoss, nameof(WetGravelGripLoss));
            if (WetPavedGripLoss >= 1f || WetGravelGripLoss >= 1f) throw new ArgumentException("wet grip loss must be below 1");
        }

        private static void Positive(float v, string what)
        {
            if (!(v > 0f) || float.IsInfinity(v)) throw new ArgumentException(what + " must be positive and finite");
        }

        private static void NonNegative(float v, string what)
        {
            if (!(v >= 0f) || float.IsInfinity(v)) throw new ArgumentException(what + " must be >= 0 and finite");
        }

        // ---------------------------------------------------------------------------------------------------
        // Presets (W2_DESIGN 6.3; [R] values are the W1 tuning). Body sizes from the W2_DESIGN 5.2 catalogue.
        // ---------------------------------------------------------------------------------------------------

        /// <summary>A fresh, validated spec for a handling preset.</summary>
        public static VehicleSpec For(HandlingPreset p)
        {
            switch (p)
            {
                case HandlingPreset.Motorbike: return Motorbike();
                case HandlingPreset.Taxi: return Taxi();
                case HandlingPreset.Walker: return Walker();
                case HandlingPreset.Bicycle: return Bicycle();
                case HandlingPreset.Scooter: return Scooter();
                case HandlingPreset.Hatchback: return Hatchback();
                case HandlingPreset.Suv: return Suv();
                case HandlingPreset.Microbus: return Microbus();
                case HandlingPreset.Tempo: return Tempo();
                case HandlingPreset.Minibus: return Minibus();
                case HandlingPreset.Bus: return Bus();
                case HandlingPreset.Truck: return Truck();
                case HandlingPreset.Tractor: return Tractor();
                case HandlingPreset.Cruiser: return Cruiser();
                case HandlingPreset.Tanker: return Tanker();
                case HandlingPreset.Coach: return Coach();
                case HandlingPreset.Ev: return Ev();
                case HandlingPreset.EScooter: return EScooter();
                case HandlingPreset.Pickup: return Pickup();
                case HandlingPreset.Tipper: return Tipper();
                case HandlingPreset.SchoolBus: return SchoolBus();
                default: throw new ArgumentOutOfRangeException(nameof(p));
            }
        }

        /// <summary>The family preset of a kind (the first fourteen presets share the kind's value).</summary>
        public static VehicleSpec For(VehicleKind k)
        {
            return For((HandlingPreset)(byte)k);
        }

        /// <summary>Number of <see cref="HandlingPreset"/> values.</summary>
        public const int PresetCount = 21;

        /// <summary>
        /// The common base of a wheeled preset: the W2_DESIGN 6.3 row (top, accel, brake, wheelbase, δ0, steer rate,
        /// aLat) and the catalogue body. Lateral grip sits 5% above aLat so a full-lock turn grips on dry asphalt and
        /// drifts on looser or wet ground.
        /// </summary>
        private static VehicleSpec Wheeled(string name, VehicleKind kind, HandlingPreset preset, float topKmh, float accel, float brake,
                                           float wheelbase, float steerDeg, float steerRate, float aLat,
                                           float length, float width, float height, float rearOverhang, int seats, DriverSide side)
        {
            var s = new VehicleSpec
            {
                Name = name,
                Kind = kind,
                Preset = preset,
                MaxSpeedKmh = topKmh,
                BoostSpeedFactor = 1f,
                ReverseSpeedKmh = 12f,
                AccelMps2 = accel,
                BrakeMps2 = brake,
                CoastDecelMps2 = 0.8f,
                OverSpeedDecelMps2 = 3.5f,
                WheelbaseM = wheelbase,
                MaxSteerDeg = steerDeg,
                SteerDegPerSec = steerRate,
                SteerLateralMps2 = aLat,
                LateralGripMps2 = aLat * 1.05f,
                MaxSlipDeg = 25f,
                SlideDecelMps2 = 3.5f,
                MaxSlopeDeg = 22f,
                PitchRollResponse = 7f,
                MaxLeanDeg = 0f,
                BumpThresholdMps = 2f,
                LaunchMinVyMps = 2.5f,
                LaunchDeltaVyMps = 3.5f,
                RoadAssistPerS = 1.2f,
                StuckSeconds = 3f,
                LengthM = length,
                WidthM = width,
                HeightM = height,
                RearOverhangM = rearOverhang,
                SeatSockets = seats,
                DriverSide = side,
            };
            s.SetTables(1.0f, 0.8f, 0.7f, 0.45f, 1.0f, 0.75f, 0.6f, 0.35f);
            return s;
        }

        /// <summary>Two-wheeler defaults: lean into turns (W2_DESIGN 6.3 caps), never tip (wobble below 5 km/h, a foot
        /// down below 1 km/h), lean leading the yaw by 0.08 s, nimble road assist.</summary>
        private static void TwoWheel(VehicleSpec s, float leanDeg)
        {
            s.TwoWheeler = true;
            s.MaxLeanDeg = leanDeg;
            s.LeanResponse = 8f;
            s.LeanLeadS = 0.08f;
            s.CoastDecelMps2 = 1.0f;
            s.OverSpeedDecelMps2 = 4f;
            s.MaxSlipDeg = 30f;
            s.SlideDecelMps2 = 3f;
            s.MaxSlopeDeg = 28f;
            s.PitchRollResponse = 10f;
            s.LaunchDeltaVyMps = 3f;
            s.RoadAssistPerS = 1.6f;
            s.ReverseSpeedKmh = 5f;
        }

        /// <summary>Car-body visuals: roll 0.6°/(m/s²) (buses and trucks 1.0) capped at <paramref name="maxRollDeg"/>,
        /// pitch 0.8°/(m/s²) capped at 3°.</summary>
        private static void Body(VehicleSpec s, float rollPerMps2, float maxRollDeg)
        {
            s.VisualRollPerMps2 = rollPerMps2;
            s.VisualMaxRollDeg = maxRollDeg;
            s.VisualPitchPerMps2 = 0.8f;
            s.VisualMaxPitchDeg = 3f;
        }

        /// <summary>
        /// A 125–150 cc commuter bike, the Kathmandu default: about 85 km/h on asphalt (102 with boost), 0–60 km/h in
        /// about 3 s, nimble at low speed, leans into turns; grips on dry asphalt and drifts on loose or wet ground.
        /// </summary>
        public static VehicleSpec Motorbike()
        {
            var s = Wheeled("motorbike", VehicleKind.Motorbike, HandlingPreset.Motorbike, 85f, 6.5f, 8f, 1.3f, 35f, 220f, 10f,
                            2.05f, 0.77f, 1.08f, 0.38f, 2, DriverSide.Centre);
            TwoWheel(s, 38f);
            s.BoostSpeedFactor = 1.2f;
            s.ReverseSpeedKmh = 7f;
            s.LateralGripMps2 = 10.5f;
            s.LaunchMinVyMps = 2.5f;
            s.SetTables(1.0f, 0.8f, 0.7f, 0.45f, 1.0f, 0.75f, 0.6f, 0.35f);
            return s;
        }

        /// <summary>
        /// A small Kathmandu taxi (800-class hatchback): 75 km/h, heavier and slower to respond than the bike, no lean;
        /// opt-in drift (brake + steer above 30 km/h).
        /// </summary>
        public static VehicleSpec Taxi()
        {
            var s = Wheeled("taxi", VehicleKind.Taxi, HandlingPreset.Taxi, 75f, 4.2f, 7f, 2.36f, 34f, 120f, 8f,
                            3.40f, 1.48f, 1.48f, 0.42f, 4, DriverSide.Right);
            s.LateralGripMps2 = 8.5f;
            s.ReverseSpeedKmh = 15f;
            s.BrakeDriftMinKmh = 30f;
            Body(s, 0.6f, 4f);
            return s;
        }

        /// <summary>
        /// On foot: walk 1.6 m/s at half stick, run 4.5 m/s at full stick, sprint 6 m/s with Boost. Turns in
        /// place, never drifts, never launches, no stuck recovery (a walker can always turn away). Mud and snow
        /// slow the pace a little; slopes above 40° block.
        /// </summary>
        public static VehicleSpec Walker()
        {
            var s = new VehicleSpec
            {
                Name = "walker",
                Kind = VehicleKind.Walker,
                Preset = HandlingPreset.Walker,
                TurnInPlace = true,
                MaxSpeedKmh = 4.5f * 3.6f,
                WalkSpeedMps = 1.6f,
                BoostSpeedFactor = 6f / 4.5f,
                ReverseSpeedKmh = 1.2f * 3.6f,
                AccelMps2 = 12f,
                BrakeMps2 = 16f,
                CoastDecelMps2 = 12f,
                OverSpeedDecelMps2 = 12f,
                WheelbaseM = 0.4f,
                TurnRateDegPerSec = 300f,
                MaxYawRateDegPerSec = 360f,
                LateralGripMps2 = 50f,
                MaxSlopeDeg = 40f,
                PitchRollResponse = 0f,
                MaxLeanDeg = 0f,
                BumpThresholdMps = 0f,
                LaunchDeltaVyMps = 0f,
                RoadAssistPerS = 0f,
                StuckSeconds = 0f,
                LengthM = 0.3f,
                WidthM = 0.45f,
                HeightM = 1.55f,
            };
            s.SetTables(1.0f, 1.0f, 1.0f, 0.9f, 1.0f, 0.95f, 0.9f, 0.7f);
            return s;
        }

        /// <summary>Roadster or MTB: 25 km/h, 30 with stamina (drains in 12 s, refills in 8 s); pushed at 1.2 m/s above a
        /// 10% grade and −1.5 km/h per % above 6%; leans up to 30°.</summary>
        public static VehicleSpec Bicycle()
        {
            var s = Wheeled("bicycle", VehicleKind.Bicycle, HandlingPreset.Bicycle, 25f, 1.4f, 4.0f, 1.12f, 40f, 200f, 6f,
                            1.80f, 0.60f, 1.05f, 0.33f, 1, DriverSide.Centre);
            TwoWheel(s, 30f);
            s.BoostSpeedFactor = 30f / 25f;
            s.StaminaBoost = true;
            s.PushGrade = 0.10f;
            s.GradeSlowFromPct = 6f;
            s.GradeSlowKmhPerPct = 1.5f;
            s.ReverseSpeedKmh = 3f;
            s.CoastDecelMps2 = 0.4f;
            s.MaxSlopeDeg = 20f;
            s.StuckSeconds = 4f;
            s.SetTables(1.0f, 0.85f, 0.75f, 0.5f, 1.0f, 0.8f, 0.7f, 0.4f);
            return s;
        }

        /// <summary>CVT scooter: 70 km/h (84 boost), smooth, leans up to 32°.</summary>
        public static VehicleSpec Scooter()
        {
            var s = Wheeled("scooter", VehicleKind.Scooter, HandlingPreset.Scooter, 70f, 5.0f, 7.0f, 1.26f, 38f, 220f, 9.5f,
                            1.81f, 0.72f, 1.15f, 0.30f, 2, DriverSide.Centre);
            TwoWheel(s, 32f);
            s.BoostSpeedFactor = 1.2f;
            s.MaxSlopeDeg = 24f;
            s.SetTables(1.0f, 0.75f, 0.65f, 0.4f, 1.0f, 0.7f, 0.55f, 0.3f);
            return s;
        }

        /// <summary>Electric scooter: as the scooter with the catalogue body.</summary>
        public static VehicleSpec EScooter()
        {
            var s = Scooter();
            s.Name = "escooter";
            s.Preset = HandlingPreset.EScooter;
            s.LengthM = 1.80f;
            s.WidthM = 0.70f;
            s.HeightM = 1.10f;
            s.LowSpeedAccelMps2 = 5.6f;
            s.LowSpeedKmh = 25f;
            return s;
        }

        /// <summary>350 cc retro cruiser: 100 km/h, leans up to 35° on a softer lean spring.</summary>
        public static VehicleSpec Cruiser()
        {
            var s = Wheeled("cruiser", VehicleKind.Cruiser, HandlingPreset.Cruiser, 100f, 5.5f, 8f, 1.39f, 33f, 180f, 9.5f,
                            2.15f, 0.80f, 1.09f, 0.40f, 2, DriverSide.Centre);
            TwoWheel(s, 35f);
            s.LeanResponse = 2f * (float)Math.PI * 1.6f;
            s.ReverseSpeedKmh = 7f;
            return s;
        }

        /// <summary>Hatchback: 90 km/h, 4.5 m/s².</summary>
        public static VehicleSpec Hatchback()
        {
            var s = Wheeled("hatchback", VehicleKind.Car, HandlingPreset.Hatchback, 90f, 4.5f, 7.5f, 2.43f, 34f, 130f, 8.5f,
                            3.77f, 1.68f, 1.52f, 0.62f, 5, DriverSide.Right);
            s.ReverseSpeedKmh = 15f;
            Body(s, 0.6f, 4f);
            return s;
        }

        /// <summary>EV crossover: the hatchback row with 5.2 m/s² below 30 km/h and a 2.55 m wheelbase.</summary>
        public static VehicleSpec Ev()
        {
            var s = Wheeled("ev", VehicleKind.Car, HandlingPreset.Ev, 90f, 4.5f, 7.5f, 2.55f, 34f, 130f, 8.5f,
                            4.10f, 1.75f, 1.58f, 0.72f, 5, DriverSide.Right);
            s.LowSpeedAccelMps2 = 5.2f;
            s.LowSpeedKmh = 30f;
            s.ReverseSpeedKmh = 15f;
            Body(s, 0.6f, 4f);
            return s;
        }

        /// <summary>Pickup (car family): 90 km/h, 3.8 m/s², 3.01 m wheelbase.</summary>
        public static VehicleSpec Pickup()
        {
            var s = Wheeled("pickup", VehicleKind.Car, HandlingPreset.Pickup, 90f, 3.8f, 7f, 3.01f, 33f, 110f, 7.5f,
                            4.86f, 1.70f, 1.86f, 1.05f, 2, DriverSide.Right);
            s.ReverseSpeedKmh = 15f;
            Body(s, 0.6f, 5f);
            return s;
        }

        /// <summary>SUV or jeep: 90 km/h, 3.8 m/s², +10% grip on DIRT.</summary>
        public static VehicleSpec Suv()
        {
            var s = Wheeled("suv", VehicleKind.Suv, HandlingPreset.Suv, 90f, 3.8f, 7f, 2.68f, 33f, 110f, 7.5f,
                            4.46f, 1.82f, 1.98f, 0.85f, 7, DriverSide.Right);
            s.ReverseSpeedKmh = 15f;
            s.MaxSlopeDeg = 26f;
            Body(s, 0.6f, 5f);
            s.SetTables(1.0f, 0.85f, 0.77f, 0.5f, 1.0f, 0.8f, 0.7f, 0.4f);
            return s;
        }

        /// <summary>Microbus (14–15 seats): 80 km/h, 2.8 m/s².</summary>
        public static VehicleSpec Microbus()
        {
            var s = Wheeled("microbus", VehicleKind.Microbus, HandlingPreset.Microbus, 80f, 2.8f, 6.5f, 2.57f, 34f, 100f, 6.5f,
                            4.70f, 1.70f, 1.98f, 1.05f, 15, DriverSide.Right);
            s.MaxSlopeDeg = 20f;
            s.RoadAssistPerS = 1.1f;
            Body(s, 0.6f, 5f);
            return s;
        }

        /// <summary>Safa tempo (electric three-wheeler): 40 km/h, tilts up to 6° and never tips.</summary>
        public static VehicleSpec Tempo()
        {
            var s = Wheeled("tempo", VehicleKind.Tempo, HandlingPreset.Tempo, 40f, 2.2f, 5f, 2.10f, 38f, 110f, 5f,
                            3.60f, 1.45f, 1.90f, 0.85f, 12, DriverSide.Centre);
            s.MaxSlopeDeg = 16f;
            s.ReverseSpeedKmh = 8f;
            s.LowSpeedAccelMps2 = 2.6f;
            s.LowSpeedKmh = 20f;
            Body(s, 1.0f, 6f);
            return s;
        }

        /// <summary>7.2 m minibus: 65 km/h, 1.6 m/s².</summary>
        public static VehicleSpec Minibus()
        {
            var s = Wheeled("minibus", VehicleKind.Minibus, HandlingPreset.Minibus, 65f, 1.6f, 5f, 3.8f, 38f, 70f, 4f,
                            7.20f, 2.10f, 2.85f, 1.80f, 28, DriverSide.Right);
            HeavyBody(s);
            return s;
        }

        /// <summary>10.5 m city bus: 60 km/h (80 on highways), 1.1 m/s², a 0.25 s steering delay; the 2.6 m rear overhang
        /// swings out about 0.6 m and the body sweeps about 10.7 m at full lock (inside the 12.5 m bus circle).</summary>
        public static VehicleSpec Bus()
        {
            var s = Wheeled("bus", VehicleKind.Bus, HandlingPreset.Bus, 60f, 1.1f, 4.5f, 5.4f, 42f, 55f, 3.5f,
                            10.5f, 2.50f, 3.20f, 2.60f, 36, DriverSide.Right);
            HeavyBody(s);
            s.SteerInputDelayS = 0.25f;
            return s;
        }

        /// <summary>11 m long-distance coach (bus family): 5.6 m wheelbase and a 3.0 m rear overhang, so the body sweep stays
        /// within 11 m (W2_DESIGN 10.6 V7).</summary>
        public static VehicleSpec Coach()
        {
            var s = Wheeled("coach", VehicleKind.Bus, HandlingPreset.Coach, 60f, 1.1f, 4.5f, 5.6f, 42f, 55f, 3.5f,
                            11.0f, 2.50f, 3.35f, 3.00f, 44, DriverSide.Right);
            HeavyBody(s);
            s.SteerInputDelayS = 0.25f;
            return s;
        }

        /// <summary>8 m school bus (bus family, AI only).</summary>
        public static VehicleSpec SchoolBus()
        {
            var s = Wheeled("schoolbus", VehicleKind.Bus, HandlingPreset.SchoolBus, 60f, 1.2f, 4.5f, 4.2f, 42f, 60f, 3.5f,
                            8.0f, 2.30f, 3.00f, 2.00f, 36, DriverSide.Right);
            HeavyBody(s);
            s.SteerInputDelayS = 0.25f;
            return s;
        }

        /// <summary>Painted 2-axle truck: 55 km/h (70 on highways), 1.0 m/s² (×0.8 loaded), 1.2 m/s² engine brake.</summary>
        public static VehicleSpec Truck()
        {
            var s = Wheeled("truck", VehicleKind.Truck, HandlingPreset.Truck, 55f, 1.0f, 4.0f, 4.8f, 40f, 60f, 3.5f,
                            8.10f, 2.45f, 3.30f, 1.90f, 3, DriverSide.Right);
            HeavyBody(s);
            s.EngineBrakeMps2 = 1.2f;
            return s;
        }

        /// <summary>Tipper (truck family): tandem rear axle, effective wheelbase 4.48 m (3.8 + 1.35 / 2, W2_DESIGN 5.2) with
        /// δ0 36.7° so the front axle turns on the family's 7.5 m radius (6.3).</summary>
        public static VehicleSpec Tipper()
        {
            var s = Wheeled("tipper", VehicleKind.Truck, HandlingPreset.Tipper, 55f, 1.0f, 4.0f, 4.48f, 36.7f, 60f, 3.5f,
                            7.60f, 2.50f, 3.10f, 1.70f, 2, DriverSide.Right);
            HeavyBody(s);
            s.EngineBrakeMps2 = 1.2f;
            return s;
        }

        /// <summary>Water tanker (truck family): the truck with water slosh on the body spring (0.8 Hz, ζ 0.3).</summary>
        public static VehicleSpec Tanker()
        {
            var s = Wheeled("tanker", VehicleKind.Truck, HandlingPreset.Tanker, 55f, 1.0f, 4.0f, 4.8f, 40f, 60f, 3.5f,
                            7.50f, 2.40f, 3.00f, 1.40f, 2, DriverSide.Right);
            HeavyBody(s);
            s.EngineBrakeMps2 = 1.2f;
            s.BodySpringHz = 0.8f;
            s.BodySpringZeta = 0.3f;
            return s;
        }

        /// <summary>Tractor with trolley: 30 km/h, hitch 1.2 m behind the rear axle (the trailer comes in stage 2).</summary>
        public static VehicleSpec Tractor()
        {
            var s = Wheeled("tractor", VehicleKind.Tractor, HandlingPreset.Tractor, 30f, 1.5f, 4f, 1.95f, 38f, 90f, 3f,
                            3.45f, 1.75f, 2.10f, 0.60f, 1, DriverSide.Centre);
            s.ReverseSpeedKmh = 8f;
            s.MaxSlopeDeg = 24f;
            Body(s, 0.6f, 5f);
            s.SetTables(1.0f, 0.9f, 0.85f, 0.6f, 1.0f, 0.9f, 0.8f, 0.6f);
            return s;
        }

        private static void HeavyBody(VehicleSpec s)
        {
            Body(s, 1.0f, 6f);
            s.ReverseSpeedKmh = 10f;
            s.CoastDecelMps2 = 0.6f;
            s.OverSpeedDecelMps2 = 2.5f;
            s.MaxSlopeDeg = 16f;
            s.MaxSlipDeg = 12f;
            s.PitchRollResponse = 5f;
            s.RoadAssistPerS = 1.0f;
            s.LaunchDeltaVyMps = 0f;
            s.StuckSeconds = 4f;
            s.SetTables(1.0f, 0.8f, 0.7f, 0.5f, 1.0f, 0.75f, 0.6f, 0.4f);
        }
    }
}
