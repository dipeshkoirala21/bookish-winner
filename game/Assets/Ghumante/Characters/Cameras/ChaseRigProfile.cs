using System;

namespace Ghumante.Characters.Cameras
{
    /// <summary>
    /// Framing of the chase camera for one mode and orientation (ARCHITECTURE.md 7.10a, ADR-017): how far behind and how
    /// steeply it sits, how high above the ground it pivots, where on screen the explorer stands and how that shifts with
    /// speed (the look-ahead), and the rig's minimum horizontal field of view (CameraFov: driving 62°, walking 55°). In
    /// portrait the camera rises and pulls back with a steeper pitch and a longer look-ahead, so the road ahead stays on
    /// the tall screen. Engine-free.
    /// <para>The camera's position is anchored on the explorer (<see cref="AimHeightM"/>, <see cref="DistanceM"/>,
    /// <see cref="PitchDeg"/>); the look-ahead only tilts the view up the road, by pinning the explorer's ground point at
    /// <see cref="FootScreenY"/> on screen. So the explorer stays in the lower part of the frame at any speed and aspect
    /// and the camera can never overtake it.</para>
    /// </summary>
    public struct ChaseRigProfile
    {
        /// <summary>Distance from the pivot (<see cref="AimHeightM"/> above the explorer) to the camera.</summary>
        public float DistanceM;

        /// <summary>Elevation of the camera above the pivot, seen from the pivot, degrees.</summary>
        public float PitchDeg;

        /// <summary>Height of the pivot (what the camera orbits) above the explorer's ground point.</summary>
        public float AimHeightM;

        /// <summary>The look-ahead leads the explorer by speed × this many seconds ...</summary>
        public float LookAheadS;

        /// <summary>... up to this many metres.</summary>
        public float LookAheadMaxM;

        /// <summary>Minimum horizontal field of view of the rig, degrees.</summary>
        public float MinHorizontalFovDeg;

        /// <summary>Screen height of the explorer's ground point at rest (NDC: −1 bottom edge, +1 top edge).</summary>
        public float FootScreenYRest;

        /// <summary>... and at full look-ahead (lower, so more road ahead shows).</summary>
        public float FootScreenYFast;

        /// <summary>How quickly the camera swings behind the explorer's heading (smoothing time, seconds).</summary>
        public float FollowSeconds;

        public static readonly ChaseRigProfile RideLandscape = new ChaseRigProfile
        {
            DistanceM = 8.0f, PitchDeg = 13f, AimHeightM = 1.0f, LookAheadS = 0.45f, LookAheadMaxM = 9f,
            FootScreenYRest = -0.48f, FootScreenYFast = -0.6f, MinHorizontalFovDeg = 62f, FollowSeconds = 0.28f,
        };

        public static readonly ChaseRigProfile RidePortrait = new ChaseRigProfile
        {
            DistanceM = 9.2f, PitchDeg = 21f, AimHeightM = 1.0f, LookAheadS = 0.9f, LookAheadMaxM = 16f,
            FootScreenYRest = -0.45f, FootScreenYFast = -0.6f, MinHorizontalFovDeg = 62f, FollowSeconds = 0.32f,
        };

        public static readonly ChaseRigProfile WalkLandscape = new ChaseRigProfile
        {
            DistanceM = 8.4f, PitchDeg = 12f, AimHeightM = 1.0f, LookAheadS = 0.3f, LookAheadMaxM = 2.5f,
            FootScreenYRest = -0.58f, FootScreenYFast = -0.62f, MinHorizontalFovDeg = 55f, FollowSeconds = 0.9f,
        };

        public static readonly ChaseRigProfile WalkPortrait = new ChaseRigProfile
        {
            DistanceM = 8.6f, PitchDeg = 22f, AimHeightM = 1.0f, LookAheadS = 0.6f, LookAheadMaxM = 4.5f,
            FootScreenYRest = -0.42f, FootScreenYFast = -0.5f, MinHorizontalFovDeg = 55f, FollowSeconds = 1.0f,
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
                FootScreenYRest = a.FootScreenYRest + (b.FootScreenYRest - a.FootScreenYRest) * t,
                FootScreenYFast = a.FootScreenYFast + (b.FootScreenYFast - a.FootScreenYFast) * t,
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

        /// <summary>Where on screen (NDC y) the explorer's ground point stands at <paramref name="speedMps"/>: from
        /// <see cref="FootScreenYRest"/> towards <see cref="FootScreenYFast"/> as the look-ahead grows.</summary>
        public float FootScreenY(float speedMps)
        {
            float t = LookAheadMaxM > 0f ? LookAhead(speedMps) / LookAheadMaxM : 0f;
            return FootScreenYRest + (FootScreenYFast - FootScreenYRest) * t;
        }

        /// <summary>
        /// Downward pitch of the view (degrees) that shows a point <paramref name="cameraAbovePointM"/> below the camera
        /// and <paramref name="horizontalDistanceM"/> in front of it at screen height <paramref name="screenY"/> (NDC)
        /// with a vertical field of view of <paramref name="verticalFovDeg"/>.
        /// </summary>
        public static float ViewPitchDeg(float cameraAbovePointM, float horizontalDistanceM, float screenY, float verticalFovDeg)
        {
            double tanHalf = Math.Tan(verticalFovDeg * 0.5 * Math.PI / 180.0);
            double below = Math.Atan2(cameraAbovePointM, Math.Max(1e-3, horizontalDistanceM));
            return (float)((below + Math.Atan(screenY * tanHalf)) * 180.0 / Math.PI);
        }

        /// <summary>Inverse of <see cref="ViewPitchDeg"/>: the screen height (NDC y) of a point
        /// <paramref name="cameraAbovePointM"/> below the camera and <paramref name="horizontalDistanceM"/> in front of it,
        /// for a view pitched down by <paramref name="viewPitchDeg"/>.</summary>
        public static float ScreenY(float cameraAbovePointM, float horizontalDistanceM, float viewPitchDeg, float verticalFovDeg)
        {
            double tanHalf = Math.Tan(verticalFovDeg * 0.5 * Math.PI / 180.0);
            double below = Math.Atan2(cameraAbovePointM, Math.Max(1e-3, horizontalDistanceM));
            return (float)(Math.Tan(viewPitchDeg * Math.PI / 180.0 - below) / tanHalf);
        }
    }
}
