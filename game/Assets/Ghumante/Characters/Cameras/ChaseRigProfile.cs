using System;

namespace Ghumante.Characters.Cameras
{
    /// <summary>
    /// Framing of the chase camera for one mode and orientation (ARCHITECTURE.md 7.10a, ADR-017): how far behind and how
    /// steeply it looks, how high above the ground it aims, how far it looks ahead along the motion, and the rig's
    /// minimum horizontal field of view (CameraFov: driving 62°, walking 55°). In portrait the camera rises and pulls
    /// back with a steeper pitch and a longer look-ahead, so the road ahead stays on the tall screen. Engine-free.
    /// </summary>
    public struct ChaseRigProfile
    {
        /// <summary>Distance from the aim point to the camera.</summary>
        public float DistanceM;

        /// <summary>Downward pitch of the camera, degrees.</summary>
        public float PitchDeg;

        /// <summary>Height of the aim point above the explorer's ground point.</summary>
        public float AimHeightM;

        /// <summary>The aim point leads the explorer by speed × this many seconds ...</summary>
        public float LookAheadS;

        /// <summary>... up to this many metres.</summary>
        public float LookAheadMaxM;

        /// <summary>Minimum horizontal field of view of the rig, degrees.</summary>
        public float MinHorizontalFovDeg;

        /// <summary>How quickly the camera swings behind the explorer's heading (smoothing time, seconds).</summary>
        public float FollowSeconds;

        public static readonly ChaseRigProfile RideLandscape = new ChaseRigProfile
        {
            DistanceM = 6.2f, PitchDeg = 13f, AimHeightM = 1.15f, LookAheadS = 0.45f, LookAheadMaxM = 9f,
            MinHorizontalFovDeg = 62f, FollowSeconds = 0.28f,
        };

        public static readonly ChaseRigProfile RidePortrait = new ChaseRigProfile
        {
            DistanceM = 9.2f, PitchDeg = 21f, AimHeightM = 1.0f, LookAheadS = 0.9f, LookAheadMaxM = 16f,
            MinHorizontalFovDeg = 62f, FollowSeconds = 0.32f,
        };

        public static readonly ChaseRigProfile WalkLandscape = new ChaseRigProfile
        {
            DistanceM = 4.4f, PitchDeg = 11f, AimHeightM = 1.35f, LookAheadS = 0.3f, LookAheadMaxM = 2.5f,
            MinHorizontalFovDeg = 55f, FollowSeconds = 0.9f,
        };

        public static readonly ChaseRigProfile WalkPortrait = new ChaseRigProfile
        {
            DistanceM = 6.6f, PitchDeg = 19f, AimHeightM = 1.2f, LookAheadS = 0.6f, LookAheadMaxM = 4.5f,
            MinHorizontalFovDeg = 55f, FollowSeconds = 1.0f,
        };

        /// <summary>Component-wise blend; <paramref name="t"/> clamped to [0, 1].</summary>
        public static ChaseRigProfile Lerp(in ChaseRigProfile a, in ChaseRigProfile b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return new ChaseRigProfile
            {
                DistanceM = a.DistanceM + (b.DistanceM - a.DistanceM) * t,
                PitchDeg = a.PitchDeg + (b.PitchDeg - a.PitchDeg) * t,
                AimHeightM = a.AimHeightM + (b.AimHeightM - a.AimHeightM) * t,
                LookAheadS = a.LookAheadS + (b.LookAheadS - a.LookAheadS) * t,
                LookAheadMaxM = a.LookAheadMaxM + (b.LookAheadMaxM - a.LookAheadMaxM) * t,
                MinHorizontalFovDeg = a.MinHorizontalFovDeg + (b.MinHorizontalFovDeg - a.MinHorizontalFovDeg) * t,
                FollowSeconds = a.FollowSeconds + (b.FollowSeconds - a.FollowSeconds) * t,
            };
        }

        /// <summary>The rig between walking (0) and riding (1), landscape (0) and portrait (1).</summary>
        public static ChaseRigProfile Blend(float ride01, float portrait01)
        {
            ChaseRigProfile walk = Lerp(WalkLandscape, WalkPortrait, portrait01);
            ChaseRigProfile ride = Lerp(RideLandscape, RidePortrait, portrait01);
            return Lerp(walk, ride, ride01);
        }

        /// <summary>Camera height above the explorer's ground point (flat ground, no user zoom or pitch).</summary>
        public float CameraHeightM
        {
            get { return AimHeightM + DistanceM * (float)Math.Sin(PitchDeg * Math.PI / 180.0); }
        }

        /// <summary>How far ahead the aim point leads at <paramref name="speedMps"/>.</summary>
        public float LookAhead(float speedMps)
        {
            if (!(speedMps > 0f)) return 0f;
            return Math.Min(LookAheadMaxM, speedMps * LookAheadS);
        }
    }
}
