using System;

namespace Ghumante.Core.Traffic
{
    /// <summary>
    /// Contact is never harm (W2_DESIGN 5.4, 5.5 and 6.6; non-violent 9+ rules): pedestrians hop aside from a capsule ahead
    /// of the player's vehicle, a pedestrian who is still touched bounce-hops and lands on their feet while the vehicle drops
    /// to 40% speed, cows bring the player to a gentle stop (they keep chewing), and vehicles meet with a bumper-car
    /// bounce. Pure functions; no ragdoll or injury path exists anywhere.
    /// </summary>
    public static class ContactRules
    {
        /// <summary>Within this distance of a cow the player is limited to <see cref="CowSlowKmh"/>.</summary>
        public const float CowSlowM = 4f;

        /// <summary>Within this distance of a cow the player stops with a gentle bump.</summary>
        public const float CowStopM = 1.6f;

        public const float CowSlowKmh = 5f;

        /// <summary>Hop aside: 0.35 m up, 1.5 m sideways over 0.45 s, after a 0.10–0.25 s reaction; 1 in 4 spin.</summary>
        public const float HopUpM = 0.35f, HopSideM = 1.5f, HopS = 0.45f, ReactMinS = 0.10f, ReactMaxS = 0.25f, SpinChance = 0.25f;

        /// <summary>Touched anyway: bounce 0.5 m up and 2 m sideways, land, dust off and wave; the vehicle keeps 40% speed.</summary>
        public const float BounceUpM = 0.5f, BounceSideM = 2f, ContactSpeedFactor = 0.4f;

        /// <summary>Crowds above 0.2 people/m² cap the player at 15 km/h.</summary>
        public const float CrowdDensity = 0.2f, CrowdCapKmh = 15f;

        /// <summary>Vehicle against vehicle: impulse 0.5 × closing speed; against walls, poles and temples a squash of 0.85.</summary>
        public const float BumperImpulseFactor = 0.5f, WallSquash = 0.85f;

        /// <summary>
        /// The hop-aside capsule of a vehicle at (x, z) heading <paramref name="headingRad"/> (0 north, clockwise) at
        /// <paramref name="speedMps"/>: length v·0.8 s + 2 m ahead of the front, width vehicle + 1.0 m. True when (px, pz)
        /// is inside; <paramref name="sideSign"/> is the side to hop to (+1 right of travel, −1 left), away from the axis.
        /// </summary>
        public static bool HopAsideCapsule(double x, double z, float headingRad, float speedMps, float vehicleWidthM, float frontM,
                                           double px, double pz, out float sideSign)
        {
            double fx = Math.Sin(headingRad), fz = Math.Cos(headingRad);
            double dx = px - x, dz = pz - z;
            double along = dx * fx + dz * fz - frontM;
            double lat = dx * fz - dz * fx; // right of travel positive
            sideSign = lat >= 0 ? 1f : -1f;
            float len = Math.Max(0f, speedMps) * 0.8f + 2f;
            float half = 0.5f * (vehicleWidthM + 1.0f);
            if (along < -frontM || along > len) return false;
            return Math.Abs(lat) <= half;
        }

        /// <summary>The player's speed cap near a cow: unlimited beyond <see cref="CowSlowM"/>, 5 km/h within it, 0 within
        /// <see cref="CowStopM"/> (a gentle bump; the cow keeps chewing).</summary>
        public static float PlayerSpeedCapMps(float distanceToCowM)
        {
            if (distanceToCowM <= CowStopM) return 0f;
            if (distanceToCowM <= CowSlowM) return CowSlowKmh / 3.6f;
            return float.PositiveInfinity;
        }

        /// <summary>Bumper-car bounce between two vehicles: the impulse (m/s) each gets along the contact normal.</summary>
        public static float BumperImpulse(float closingSpeedMps)
        {
            return BumperImpulseFactor * Math.Max(0f, closingSpeedMps);
        }

        /// <summary>The hop-aside offset at time t since the hop started (after the reaction): sideways distance and height.</summary>
        public static void HopOffset(float t, out float side, out float up)
        {
            float u = t <= 0f ? 0f : t >= HopS ? 1f : t / HopS;
            float ease = u * u * (3f - 2f * u);
            side = HopSideM * ease;
            up = HopUpM * 4f * u * (1f - u);
        }
    }
}
