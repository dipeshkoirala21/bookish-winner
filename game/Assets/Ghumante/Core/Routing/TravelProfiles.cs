using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Routing
{
    /// <summary>
    /// Speed and capability table of one travel mode: an exact port of <c>routing.TravelProfile</c>. For an
    /// edge, <c>speed = SpeedKmh[class] * SurfaceFactor[group(surface)]</c>, capped at
    /// <see cref="AlpineSpeedKmh"/> when <c>sac_scale &gt;= T4</c>; trails above <see cref="MaxSac"/> and
    /// classes with speed 0 are unusable. <c>time = length * 3.6 / speed * (1 + max(0, climb) / length * ClimbK)</c>.
    /// The arithmetic order matches Python, so edge times agree to the last bit.
    /// </summary>
    public sealed class TravelProfile
    {
        public readonly Travel Travel;
        public readonly double[] SpeedKmh; // by RoadClass value
        public readonly double[] SurfaceFactor; // by SurfaceGroup value
        public readonly int MaxSac;
        public readonly double ClimbK;
        public readonly double AlpineSpeedKmh;
        public readonly double MaxSpeedKmh;

        internal TravelProfile(Travel travel, double[] speedKmh, double[] surfaceFactor, int maxSac, double climbK,
                               double alpineSpeedKmh)
        {
            Travel = travel;
            SpeedKmh = speedKmh;
            SurfaceFactor = surfaceFactor;
            MaxSac = maxSac;
            ClimbK = climbK;
            AlpineSpeedKmh = alpineSpeedKmh;
            double ms = 0, mf = 0;
            foreach (double v in speedKmh) ms = Math.Max(ms, v);
            foreach (double v in surfaceFactor) mf = Math.Max(mf, v);
            MaxSpeedKmh = ms * mf;
        }

        public string Name
        {
            get { return TravelProfiles.NameOf(Travel); }
        }

        /// <summary>Whether the class/sac_scale combination is physically usable (ignoring access tags).</summary>
        public bool Allows(int roadClass, int sacScale)
        {
            if (roadClass < 0 || roadClass >= SpeedKmh.Length || SpeedKmh[roadClass] <= 0.0) return false;
            return !(TravelProfiles.IsTrail(roadClass) && sacScale > MaxSac);
        }

        /// <summary>Effective speed in km/h; 0 when the profile cannot use such an edge.</summary>
        public double Speed(int roadClass, int surface, int sacScale)
        {
            if (!Allows(roadClass, sacScale)) return 0.0;
            double v = SpeedKmh[roadClass] * SurfaceFactor[(int)SurfaceGroups.Of((Surface)(surface & 0xFF))];
            if (sacScale >= TravelProfiles.AlpineSac) v = Math.Min(v, AlpineSpeedKmh);
            return v;
        }

        /// <summary>Seconds to traverse an edge, or +infinity when not allowed.</summary>
        public double EdgeTimeS(Travel access, int roadClass, int surface, int sacScale, uint lengthDm, int climbM)
        {
            if ((access & Travel) == 0) return double.PositiveInfinity;
            double speed = Speed(roadClass, surface, sacScale);
            if (speed <= 0.0) return double.PositiveInfinity;
            double lengthM = lengthDm / 10.0;
            double cf = lengthM > 0.0 ? 1.0 + Math.Max(0.0, climbM) / lengthM * ClimbK : 1.0;
            return lengthM * 3.6 / speed * cf;
        }

        public double EdgeTimeS(RouteGraph g, int e)
        {
            return EdgeTimeS(g.EdgeAccess[e], (int)g.EdgeClass[e], (int)g.EdgeSurface[e], (int)g.EdgeSac[e],
                             g.EdgeLengthDm[e], g.EdgeClimb[e]);
        }
    }

    /// <summary>The seven travel profiles (routing.TRAVEL_PROFILES), one per single <see cref="Travel"/> bit.</summary>
    public static class TravelProfiles
    {
        public const int AlpineSac = (int)SacScale.AlpineHiking;

        /// <summary>Profiles in bit order: FOOT, BICYCLE, MOTORBIKE, CAR, JEEP, BUS, HORSE.</summary>
        public static readonly Travel[] Order =
        {
            Travel.Foot, Travel.Bicycle, Travel.Motorbike, Travel.Car, Travel.Jeep, Travel.Bus, Travel.Horse,
        };

        private static readonly string[] Names = { "FOOT", "BICYCLE", "MOTORBIKE", "CAR", "JEEP", "BUS", "HORSE" };

        // Base speed km/h by RoadClass (rows) and profile (columns, Order).
        private static readonly double[,] SpeedRows =
        {
            //  FOOT  BIKE  MBIKE  CAR  JEEP   BUS  HORSE
            { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 }, // UNKNOWN
            { 5.0, 20.0, 80.0, 90.0, 85.0, 70.0, 7.0 }, // MOTORWAY
            { 5.0, 20.0, 80.0, 80.0, 75.0, 60.0, 7.0 }, // TRUNK
            { 5.0, 18.0, 70.0, 70.0, 65.0, 50.0, 7.0 }, // PRIMARY
            { 5.0, 18.0, 60.0, 60.0, 55.0, 45.0, 7.0 }, // SECONDARY
            { 5.0, 18.0, 50.0, 50.0, 50.0, 40.0, 7.0 }, // TERTIARY
            { 5.0, 16.0, 40.0, 40.0, 40.0, 30.0, 7.0 }, // UNCLASSIFIED
            { 5.0, 15.0, 30.0, 30.0, 30.0, 25.0, 7.0 }, // RESIDENTIAL
            { 5.0, 10.0, 15.0, 15.0, 15.0, 10.0, 6.0 }, // LIVING_STREET
            { 5.0, 14.0, 20.0, 20.0, 20.0, 15.0, 7.0 }, // SERVICE
            { 5.0, 12.0, 25.0, 20.0, 30.0, 15.0, 7.0 }, // TRACK
            { 5.0, 16.0, 35.0, 35.0, 35.0, 30.0, 7.0 }, // ROAD
            { 5.0, 8.0, 10.0, 0.0, 0.0, 0.0, 5.0 }, // PEDESTRIAN
            { 5.0, 10.0, 10.0, 0.0, 0.0, 0.0, 6.0 }, // FOOTWAY
            { 5.0, 10.0, 12.0, 0.0, 0.0, 0.0, 7.0 }, // PATH
            { 4.0, 3.0, 0.0, 0.0, 0.0, 0.0, 0.0 }, // STEPS
            { 5.0, 20.0, 15.0, 0.0, 0.0, 0.0, 6.0 }, // CYCLEWAY
            { 5.0, 8.0, 10.0, 0.0, 0.0, 0.0, 8.0 }, // BRIDLEWAY
        };

        // Surface factor by SurfaceGroup (rows: PAVED, GRAVEL, DIRT, MUD) and profile.
        private static readonly double[,] SurfaceRows =
        {
            { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 },
            { 1.0, 0.75, 0.75, 0.75, 0.9, 0.75, 1.0 },
            { 1.0, 0.6, 0.6, 0.6, 0.85, 0.6, 1.0 },
            { 0.8, 0.35, 0.35, 0.35, 0.6, 0.35, 0.8 },
        };

        private static readonly int[] MaxSacs = { 6, 2, 1, 0, 0, 0, 3 };
        private static readonly double[] ClimbKs = { 6.0, 8.0, 1.0, 1.0, 1.0, 1.0, 4.0 };

        private static readonly double[] AlpineSpeeds =
        {
            3.5, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity,
            double.PositiveInfinity, double.PositiveInfinity,
        };

        private static readonly TravelProfile[] All = Build();

        private static TravelProfile[] Build()
        {
            var all = new TravelProfile[Order.Length];
            for (int p = 0; p < Order.Length; p++)
            {
                var speed = new double[SpeedRows.GetLength(0)];
                for (int c = 0; c < speed.Length; c++) speed[c] = SpeedRows[c, p];
                var surf = new double[SurfaceRows.GetLength(0)];
                for (int g = 0; g < surf.Length; g++) surf[g] = SurfaceRows[g, p];
                all[p] = new TravelProfile(Order[p], speed, surf, MaxSacs[p], ClimbKs[p], AlpineSpeeds[p]);
            }
            return all;
        }

        /// <summary>The trail classes whose sac_scale limits a profile (model.TRAIL_CLASSES).</summary>
        public static bool IsTrail(int roadClass)
        {
            if (roadClass < 0 || roadClass > 255) return false;
            switch ((RoadClass)roadClass)
            {
                case RoadClass.Pedestrian:
                case RoadClass.Footway:
                case RoadClass.Path:
                case RoadClass.Steps:
                case RoadClass.Cycleway:
                case RoadClass.Bridleway:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The profile of a single travel bit.</summary>
        public static TravelProfile Get(Travel travel)
        {
            for (int p = 0; p < Order.Length; p++)
                if (Order[p] == travel) return All[p];
            throw new ArgumentException("not a single travel profile: " + travel);
        }

        /// <summary>The profile by its pipeline name ("FOOT", "car", ...).</summary>
        public static TravelProfile Get(string name)
        {
            for (int p = 0; p < Names.Length; p++)
                if (string.Equals(Names[p], name, StringComparison.OrdinalIgnoreCase)) return All[p];
            throw new ArgumentException("unknown travel profile " + name);
        }

        public static string NameOf(Travel travel)
        {
            for (int p = 0; p < Order.Length; p++)
                if (Order[p] == travel) return Names[p];
            return travel.ToString();
        }

        /// <summary>Travel bits whose profile can physically use an edge of this class and sac_scale.</summary>
        public static Travel UsableMask(int roadClass, int sacScale)
        {
            Travel m = Travel.None;
            for (int p = 0; p < All.Length; p++)
                if (All[p].Allows(roadClass, sacScale)) m |= Order[p];
            return m;
        }
    }
}
