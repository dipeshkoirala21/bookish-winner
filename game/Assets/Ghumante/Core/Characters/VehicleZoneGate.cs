using System;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// "Vehicles rest outside" for the player's vehicle (W2_DESIGN 6.6, owner decision W2-O1): sweeps the path the
    /// vehicle is about to drive (along its current steering arc, from the bumper on the side it moves towards) for the
    /// first sacred zone it may not enter, and turns that distance into a braking-curve speed cap, so the vehicle always
    /// stops at the zone edge whatever its speed, forwards or in reverse, and a road that only curves past a compound
    /// never stops it. Engine-free and allocation-free.
    /// </summary>
    public static class VehicleZoneGate
    {
        /// <summary>The vehicle stops this far short of the zone edge.</summary>
        public const float MarginM = 1.0f;

        /// <summary>Extra look-ahead beyond the braking distance.</summary>
        public const float SlackM = 6f;

        /// <summary>Arc sample step and the most steps per sweep.</summary>
        public const float StepM = 3f;

        public const int MaxSteps = 24;

        /// <summary>Share of the spec's brake deceleration the cap plans with (slack for the fixed step).</summary>
        public const float BrakeShare = 0.8f;

        /// <summary>Standing within this of the edge counts as "at the zone" (a held two-wheeler auto-parks).</summary>
        public const float AtEdgeM = MarginM + 0.75f;

        /// <summary>Zone kinds (bit <c>1 &lt;&lt; kind</c>) this vehicle may enter (a bicycle in a heritage square).</summary>
        public static uint AllowedKinds(in VehicleCatalogEntry e)
        {
            uint m = 1u << (int)SacredZoneKind.None;
            for (int k = 1; k <= (int)SacredZoneKind.Ghat; k++)
                if (VehicleRoles.AllowedIn(e, (SacredZoneKind)k)) m |= 1u << k;
            return m;
        }

        /// <summary>How far ahead to sweep at <paramref name="speedMps"/>: the braking distance plus slack.</summary>
        public static float LookAheadM(float speedMps, float brakeMps2)
        {
            float a = Math.Max(0.5f, brakeMps2 * BrakeShare);
            return speedMps * speedMps / (2f * a) + MarginM + SlackM;
        }

        /// <summary>
        /// Distance along the path from (<paramref name="x"/>, <paramref name="z"/>) (a bumper, game metres) to the first
        /// zone not in <paramref name="allowedKinds"/>, sweeping up to <paramref name="lookM"/> along an arc that starts
        /// at <paramref name="headingRad"/> (x += sin, z += cos) and turns by <paramref name="curvature"/> rad per metre
        /// (positive turns right). +∞ when the path is clear.
        /// </summary>
        public static float DistanceToZone(SacredZoneIndex zones, uint allowedKinds, double x, double z, float headingRad, float curvature, float lookM)
        {
            if (zones == null || !(lookM > 0f)) return float.PositiveInfinity;
            int n = Math.Min(MaxSteps, Math.Max(1, (int)Math.Ceiling(lookM / StepM)));
            float step = lookM / n, h = headingRad, walked = 0f;
            for (int i = 0; i < n; i++)
            {
                float hm = h + 0.5f * curvature * step;
                double nx = x + Math.Sin(hm) * step, nz = z + Math.Cos(hm) * step;
                double t;
                if (zones.TryFirstEntry(x, z, nx, nz, allowedKinds, out t)) return walked + (float)t * step;
                walked += step;
                x = nx;
                z = nz;
                h += curvature * step;
            }
            return float.PositiveInfinity;
        }

        /// <summary>The highest speed from which the vehicle still stops <see cref="MarginM"/> short of a zone
        /// <paramref name="distanceM"/> ahead (0 at the margin).</summary>
        public static float SpeedCapMps(float distanceM, float brakeMps2)
        {
            if (float.IsPositiveInfinity(distanceM)) return float.PositiveInfinity;
            float room = distanceM - MarginM;
            if (room <= 0f) return 0f;
            return (float)Math.Sqrt(2f * Math.Max(0.5f, brakeMps2 * BrakeShare) * room);
        }

        /// <summary>
        /// The drive input after the gate: brakes when the speed towards a zone exceeds its cap, and refuses to set off
        /// towards a zone at the edge. <paramref name="frontM"/>/<paramref name="rearM"/> are the swept distances ahead
        /// of the front bumper and behind the rear bumper. Returns true when it changed the input.
        /// </summary>
        public static bool Apply(ref DriveInput input, float speedMps, float frontM, float rearM, float brakeMps2)
        {
            float capF = SpeedCapMps(frontM, brakeMps2), capR = SpeedCapMps(rearM, brakeMps2);
            if (speedMps > 0.05f && speedMps > capF || speedMps < -0.05f && -speedMps > capR)
            {
                input = new DriveInput(0f, 1f, input.Steer, false);
                return true;
            }
            // Setting off (or still accelerating) towards a zone already at the edge: hold.
            if (input.Throttle > 0f && speedMps >= -0.05f && capF <= 0.5f || input.Throttle < 0f && speedMps <= 0.05f && capR <= 0.5f)
            {
                input = new DriveInput(0f, Math.Abs(speedMps) > 0.05f ? 1f : 0f, input.Steer, false);
                return true;
            }
            return false;
        }
    }
}
