using System;

namespace Ghumante.Core.Characters
{
    /// <summary>The numbers of one chase-camera rig (W2_DESIGN 6.4).</summary>
    public struct RigParams
    {
        /// <summary>Distance from the pivot to the camera, metres.</summary>
        public float DistanceM;

        /// <summary>Elevation of the camera above the pivot, degrees.</summary>
        public float PitchDeg;

        /// <summary>Pivot height above the player's (or vehicle's) ground point.</summary>
        public float PivotM;

        /// <summary>Look-ahead: speed × this many seconds, up to <see cref="LookAheadMaxM"/>.</summary>
        public float LookAheadS, LookAheadMaxM;

        /// <summary>Minimum horizontal field of view, degrees (walk 55°, drive 62°, bus and truck 64°).</summary>
        public float MinHFovDeg;

        /// <summary>Heading follow smoothing, seconds.</summary>
        public float FollowS;

        /// <summary>Speed FOV kick, degrees, reached at <see cref="KickFullMps"/> (above <see cref="KickFromMps"/>).</summary>
        public float FovKickDeg, KickFromMps, KickFullMps;

        /// <summary>Collision minimum distance (walk 2.5 m, vehicles 4 m).</summary>
        public float MinDistanceM;
    }

    /// <summary>
    /// The camera rig per class and orientation (W2_DESIGN 6.4): distance ≈ 6 + 1 × vehicle length in landscape, portrait
    /// 10–15% farther with 8–10° more pitch and twice the look-ahead time. The portrait walking rig is 6.8 m / 20° / 0.9 m
    /// (W2 change from 8.6 / 22 / 1.0) so the player grows from about 187 px to about 240 px on a 1080 × 2340 phone.
    /// Passengers orbit at 1.2× the driver rig of their vehicle. Engine-free; the Unity rig reads it.
    /// </summary>
    public static class CameraRigTable
    {
        public const float PassengerScale = 1.2f;

        /// <summary>Passenger ride-along auto-yaw towards points of interest within 300 m, degrees per second.</summary>
        public const float PassengerAutoYawDegPerS = 6f;

        public static RigParams For(RigClass rig, bool portrait)
        {
            if (rig == RigClass.Passenger) return For(rig, portrait, RigClass.Car);
            switch (rig)
            {
                case RigClass.Walk:
                    return portrait
                        ? R(6.8f, 20f, 0.9f, 0.6f, 4.5f, 55f, 1.0f, 0f, 0f, 1f, 2.5f)
                        : R(8.4f, 12f, 1.0f, 0.3f, 2.5f, 55f, 0.9f, 0f, 0f, 1f, 2.5f);
                case RigClass.Bicycle:
                    return portrait
                        ? R(8.5f, 20f, 1.0f, 0.8f, 10f, 62f, 0.30f, 3f, 3f, 8.3f, 4f)
                        : R(7.5f, 12f, 1.0f, 0.4f, 6f, 62f, 0.30f, 3f, 3f, 8.3f, 4f);
                case RigClass.TwoWheeler:
                    return portrait
                        ? R(9.2f, 21f, 1.0f, 0.9f, 16f, 62f, 0.32f, 6f, 16.7f, 25f, 4f)
                        : R(8.0f, 13f, 1.0f, 0.45f, 9f, 62f, 0.28f, 6f, 16.7f, 25f, 4f);
                case RigClass.Car:
                    return portrait
                        ? R(10.8f, 22f, 1.3f, 1.0f, 20f, 62f, 0.35f, 5f, 5f, 25f, 4f)
                        : R(9.5f, 14f, 1.3f, 0.5f, 12f, 62f, 0.35f, 5f, 5f, 25f, 4f);
                case RigClass.Van:
                    return portrait
                        ? R(12.0f, 23f, 1.6f, 1.0f, 20f, 62f, 0.40f, 4f, 5f, 22f, 4f)
                        : R(10.5f, 15f, 1.6f, 0.5f, 12f, 62f, 0.40f, 4f, 5f, 22f, 4f);
                case RigClass.Bus:
                    return portrait
                        ? R(18.5f, 25f, 2.6f, 1.2f, 28f, 64f, 0.60f, 2f, 5f, 17f, 4f)
                        : R(16.5f, 17f, 2.6f, 0.6f, 18f, 64f, 0.60f, 2f, 5f, 17f, 4f);
                case RigClass.Truck:
                    return portrait
                        ? R(16.5f, 24f, 2.4f, 1.2f, 26f, 64f, 0.55f, 2f, 5f, 15f, 4f)
                        : R(14.5f, 16f, 2.4f, 0.6f, 16f, 64f, 0.55f, 2f, 5f, 15f, 4f);
                case RigClass.Tractor:
                    return portrait
                        ? R(11.5f, 26f, 1.8f, 0.6f, 10f, 62f, 0.50f, 0f, 0f, 1f, 4f)
                        : R(10.0f, 18f, 1.8f, 0.3f, 6f, 62f, 0.50f, 0f, 0f, 1f, 4f);
                default:
                    return Passenger(For(RigClass.Car, portrait));
            }
        }

        /// <summary>
        /// As <see cref="For(RigClass, bool)"/>, with the class of the vehicle ridden along: a passenger orbits at 1.2×
        /// the driver rig of <paramref name="passengerOf"/> (a bus ride frames like a bus, not a car). Other rigs ignore it.
        /// </summary>
        public static RigParams For(RigClass rig, bool portrait, RigClass passengerOf)
        {
            if (rig != RigClass.Passenger) return For(rig, portrait);
            if (passengerOf == RigClass.Walk || passengerOf == RigClass.Passenger) passengerOf = RigClass.Car;
            return Passenger(For(passengerOf, portrait));
        }

        /// <summary>The ride-along rig for a driver rig: 1.2× the distance, no look-ahead, no FOV kick.</summary>
        public static RigParams Passenger(in RigParams driver)
        {
            RigParams p = driver;
            p.DistanceM *= PassengerScale;
            p.LookAheadS = 0f;
            p.LookAheadMaxM = 0f;
            p.FovKickDeg = 0f;
            p.FollowS = Math.Max(driver.FollowS, 0.6f);
            return p;
        }

        /// <summary>The FOV kick at a speed (none below the threshold, full at the top).</summary>
        public static float FovKick(in RigParams r, float speedMps)
        {
            if (r.FovKickDeg <= 0f || r.KickFullMps <= r.KickFromMps) return 0f;
            float t = (Math.Abs(speedMps) - r.KickFromMps) / (r.KickFullMps - r.KickFromMps);
            return r.FovKickDeg * CharMath.Clamp01(t);
        }

        private static RigParams R(float d, float pitch, float pivot, float laS, float laMax, float fov, float follow, float kick, float kickFrom,
                                   float kickFull, float minD)
        {
            return new RigParams
            {
                DistanceM = d, PitchDeg = pitch, PivotM = pivot, LookAheadS = laS, LookAheadMaxM = laMax, MinHFovDeg = fov, FollowS = follow,
                FovKickDeg = kick, KickFromMps = kickFrom, KickFullMps = kickFull, MinDistanceM = minD,
            };
        }
    }
}
