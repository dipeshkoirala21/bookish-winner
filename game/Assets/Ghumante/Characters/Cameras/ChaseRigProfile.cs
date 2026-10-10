using System;
using Ghumante.Core.Characters;

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

        /// <summary>Collision minimum distance of the boom (<see cref="ChaseBoom"/>): walk 2.5 m, vehicles 4 m, less for
        /// the close views.</summary>
        public float MinDistanceM;

        /// <summary>Sideways pivot offset, metres, right positive (the over-the-shoulder view).</summary>
        public float ShoulderM;

        /// <summary>0 pins the explorer's ground point at <see cref="FootScreenY"/>; 1 looks straight through the pivot
        /// (over the shoulder). Blends between views.</summary>
        public float Aim01;

        public static readonly ChaseRigProfile RideLandscape = new ChaseRigProfile
        {
            DistanceM = 8.0f, PitchDeg = 13f, AimHeightM = 1.0f, LookAheadS = 0.45f, LookAheadMaxM = 9f,
            FootScreenYRest = -0.48f, FootScreenYFast = -0.6f, MinHorizontalFovDeg = 62f, FollowSeconds = 0.28f, MinDistanceM = 4f,
        };

        public static readonly ChaseRigProfile RidePortrait = new ChaseRigProfile
        {
            DistanceM = 9.2f, PitchDeg = 21f, AimHeightM = 1.0f, LookAheadS = 0.9f, LookAheadMaxM = 16f,
            FootScreenYRest = -0.45f, FootScreenYFast = -0.6f, MinHorizontalFovDeg = 62f, FollowSeconds = 0.32f, MinDistanceM = 4f,
        };

        public static readonly ChaseRigProfile WalkLandscape = new ChaseRigProfile
        {
            DistanceM = 8.4f, PitchDeg = 12f, AimHeightM = 1.0f, LookAheadS = 0.3f, LookAheadMaxM = 2.5f,
            FootScreenYRest = -0.58f, FootScreenYFast = -0.62f, MinHorizontalFovDeg = 55f, FollowSeconds = 0.9f, MinDistanceM = 2.5f,
        };

        /// <summary>W2 change (W2_DESIGN 6.4): 6.8 m / 20° / 0.9 m instead of 8.6 / 22 / 1.0, so the player grows from
        /// about 187 px to about 240 px on a 1080 × 2340 portrait phone.</summary>
        public static readonly ChaseRigProfile WalkPortrait = new ChaseRigProfile
        {
            DistanceM = 6.8f, PitchDeg = 20f, AimHeightM = 0.9f, LookAheadS = 0.6f, LookAheadMaxM = 4.5f,
            FootScreenYRest = -0.42f, FootScreenYFast = -0.5f, MinHorizontalFovDeg = 55f, FollowSeconds = 1.0f, MinDistanceM = 2.5f,
        };

        /// <summary>The rig of a class in landscape or portrait from <see cref="CameraRigTable"/> (W2_DESIGN 6.4), with
        /// the framing heights of the walk and ride rigs.</summary>
        public static ChaseRigProfile For(RigClass rig, bool portrait)
        {
            return For(rig, portrait, RigClass.Car);
        }

        /// <summary>As <see cref="For(RigClass, bool)"/>; a passenger rig orbits at 1.2× the driver rig of
        /// <paramref name="passengerOf"/> (CameraRigTable).</summary>
        public static ChaseRigProfile For(RigClass rig, bool portrait, RigClass passengerOf)
        {
            return For(rig, portrait, passengerOf, CameraViews.Default(rig));
        }

        /// <summary>The rig of a class seen through a chase <paramref name="view"/> (<see cref="CameraViews.Chase"/>:
        /// near, far, over the shoulder, low cinematic, high chase); a mounted view gives the class rig.</summary>
        public static ChaseRigProfile For(RigClass rig, bool portrait, RigClass passengerOf, CameraView view)
        {
            RigParams r = CameraViews.Chase(rig, view, portrait, passengerOf);
            bool walk = rig == RigClass.Walk;
            return new ChaseRigProfile
            {
                DistanceM = r.DistanceM, PitchDeg = r.PitchDeg, AimHeightM = r.PivotM, LookAheadS = r.LookAheadS, LookAheadMaxM = r.LookAheadMaxM,
                MinHorizontalFovDeg = r.MinHFovDeg, FollowSeconds = r.FollowS, MinDistanceM = r.MinDistanceM, ShoulderM = r.ShoulderM,
                Aim01 = r.AimAtPivot ? 1f : 0f,
                FootScreenYRest = walk ? (portrait ? -0.42f : -0.58f) : (portrait ? -0.45f : -0.48f),
                FootScreenYFast = walk ? (portrait ? -0.5f : -0.62f) : -0.6f,
            };
        }

        /// <summary>The rig of a class through a view between landscape (0) and portrait (1).</summary>
        public static ChaseRigProfile For(RigClass rig, float portrait01, RigClass passengerOf, CameraView view)
        {
            return Lerp(For(rig, false, passengerOf, view), For(rig, true, passengerOf, view), portrait01);
        }

        /// <summary>The rig of a class between landscape (0) and portrait (1).</summary>
        public static ChaseRigProfile For(RigClass rig, float portrait01)
        {
            return For(rig, portrait01, RigClass.Car);
        }

        /// <summary>The rig of a class between landscape (0) and portrait (1), riding along in a
        /// <paramref name="passengerOf"/> when <paramref name="rig"/> is <see cref="RigClass.Passenger"/>.</summary>
        public static ChaseRigProfile For(RigClass rig, float portrait01, RigClass passengerOf)
        {
            return Lerp(For(rig, false, passengerOf), For(rig, true, passengerOf), portrait01);
        }

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
                MinDistanceM = a.MinDistanceM + (b.MinDistanceM - a.MinDistanceM) * t,
                ShoulderM = a.ShoulderM + (b.ShoulderM - a.ShoulderM) * t,
                Aim01 = a.Aim01 + (b.Aim01 - a.Aim01) * t,
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
