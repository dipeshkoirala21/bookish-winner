using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Driving
{
    public enum VehicleKind : byte
    {
        Motorbike = 0,
        Taxi = 1,
        Walker = 2,
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

        /// <summary>
        /// A 125–150 cc commuter bike, the Kathmandu default: about 85 km/h on asphalt, 0–60 km/h in about
        /// 3 s, nimble at low speed, leans into turns; grips on dry asphalt and drifts on loose or wet ground.
        /// </summary>
        public static VehicleSpec Motorbike()
        {
            var s = new VehicleSpec
            {
                Name = "motorbike",
                Kind = VehicleKind.Motorbike,
                MaxSpeedKmh = 85f,
                BoostSpeedFactor = 1f,
                ReverseSpeedKmh = 7f,
                AccelMps2 = 6.5f,
                BrakeMps2 = 8f,
                CoastDecelMps2 = 1.0f,
                OverSpeedDecelMps2 = 4f,
                WheelbaseM = 1.3f,
                MaxSteerDeg = 35f,
                SteerDegPerSec = 220f,
                SteerLateralMps2 = 10f,
                LateralGripMps2 = 10.5f,
                MaxSlipDeg = 30f,
                SlideDecelMps2 = 3f,
                MaxSlopeDeg = 28f,
                PitchRollResponse = 10f,
                MaxLeanDeg = 38f,
                LeanResponse = 8f,
                BumpThresholdMps = 2f,
                LaunchMinVyMps = 2.5f,
                LaunchDeltaVyMps = 3f,
                RoadAssistPerS = 1.6f,
                StuckSeconds = 3f,
            };
            s.SetTables(1.0f, 0.8f, 0.7f, 0.45f, 1.0f, 0.75f, 0.6f, 0.35f);
            return s;
        }

        /// <summary>
        /// A small Kathmandu taxi (Maruti-800 class hatchback): 75 km/h, heavier and slower to respond than the
        /// bike, no lean, a little less grip.
        /// </summary>
        public static VehicleSpec Taxi()
        {
            var s = new VehicleSpec
            {
                Name = "taxi",
                Kind = VehicleKind.Taxi,
                MaxSpeedKmh = 75f,
                BoostSpeedFactor = 1f,
                ReverseSpeedKmh = 15f,
                AccelMps2 = 4.2f,
                BrakeMps2 = 7f,
                CoastDecelMps2 = 0.8f,
                OverSpeedDecelMps2 = 3.5f,
                WheelbaseM = 2.2f,
                MaxSteerDeg = 32f,
                SteerDegPerSec = 120f,
                SteerLateralMps2 = 8f,
                LateralGripMps2 = 8.5f,
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
            };
            s.SetTables(1.0f, 0.8f, 0.7f, 0.45f, 1.0f, 0.75f, 0.6f, 0.35f);
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
            };
            s.SetTables(1.0f, 1.0f, 1.0f, 0.9f, 1.0f, 0.95f, 0.9f, 0.7f);
            return s;
        }
    }
}
