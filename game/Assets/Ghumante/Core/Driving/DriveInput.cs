using System;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// One frame of player (or AI) intent for <see cref="ArcadeVehicle.Step"/>. Values outside their ranges are
    /// clamped and NaN counts as 0.
    /// </summary>
    public struct DriveInput
    {
        /// <summary>[-1, 1]. Forward drive; negative brakes while moving forward and reverses once stopped.
        /// For the walker the magnitude picks the gait: up to 0.5 walks, 1 runs.</summary>
        public float Throttle;

        /// <summary>[0, 1]. Brake (also stops reversing).</summary>
        public float Brake;

        /// <summary>[-1, 1]. Positive steers right (clockwise seen from above, heading increases).</summary>
        public float Steer;

        /// <summary>Boost: sprint for the walker; a higher top speed for vehicles whose spec has one.</summary>
        public bool Boost;

        public DriveInput(float throttle, float brake, float steer, bool boost = false)
        {
            Throttle = throttle;
            Brake = brake;
            Steer = steer;
            Boost = boost;
        }
    }

    /// <summary>What happened during one <see cref="ArcadeVehicle.Step"/> (several flags may be set).</summary>
    [Flags]
    public enum StepEvents
    {
        None = 0,

        /// <summary>The effective surface group (after wetness) changed.</summary>
        SurfaceChanged = 1,

        /// <summary>A sudden height change under the wheels: a kerb ramp, a step between tiles, rough ground.
        /// <see cref="ArcadeVehicle.LastBumpStrength"/> says how hard.</summary>
        Bump = 2,

        /// <summary>Touched down after being airborne (<see cref="ArcadeVehicle.LastLandingSpeedMps"/>).</summary>
        Landed = 4,

        /// <summary>Automatic stuck recovery fired (hop and nudge, ARCHITECTURE 7.6).</summary>
        StuckRecovered = 8,

        /// <summary>Left the road surface.</summary>
        OffRoad = 16,

        /// <summary>Ran into something solid (a house, wall, tree, railing, parked vehicle) and lost at least
        /// <see cref="ArcadeVehicle.HitWallMinMps"/>; <see cref="ArcadeVehicle.LastImpactMps"/> says how much.</summary>
        HitWall = 32,
    }
}
