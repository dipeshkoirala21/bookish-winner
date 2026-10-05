using System;

namespace Ghumante.App.Explore
{
    /// <summary>Where the explorer is with respect to the route.</summary>
    public enum RouteStatus : byte
    {
        OnRoute = 0,

        /// <summary>More than <see cref="RouteGuide.OffRouteM"/> from the route for <see cref="RouteGuide.OffRouteSeconds"/>:
        /// time to find a new way.</summary>
        OffRoute = 1,

        /// <summary>Within <see cref="RouteGuide.ArrivalRadiusM"/> of the end (or of the destination itself). Final.</summary>
        Arrived = 2,
    }

    /// <summary>
    /// Follows the explorer along a <see cref="PlannedRoute"/> (ARCHITECTURE.md 7.8, M1 track D): where on the route they are
    /// (projection onto the polyline, searched near the last position so a route that comes back along the same street
    /// does not jump ahead), the remaining distance and ETA (the route's own time, scaled by what is left), a direction
    /// to steer by (towards a point <see cref="LookAheadM"/> further along the route) and arrival within
    /// <see cref="ArrivalRadiusM"/> (about 40 m) of the destination, or of the end of the road once the route is nearly
    /// done. Engine-free; no allocation per update.
    /// </summary>
    public sealed class RouteGuide
    {
        public const double ArrivalRadiusM = 40.0;
        public const double OffRouteM = 45.0;
        public const float OffRouteSeconds = 3f;
        public const double LookAheadM = 30.0;

        /// <summary>Beyond this from the local search, the whole route is searched again.</summary>
        private const double RelocateM = 60.0;

        /// <summary>Cost of a jump in progress, metres of distance per metre along the route (see Nearest).</summary>
        private const double ContinuityPerM = 0.05;

        private const int WindowBehind = 4;
        private const int WindowAhead = 48;

        private readonly double[] _xz;
        private readonly double[] _cumulative;
        private readonly int _points;
        private int _segment;
        private float _offTime;

        public RouteGuide(PlannedRoute route)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            if (route.Polyline == null || route.Polyline.Length < 4 || route.Polyline.Length % 2 != 0)
                throw new ArgumentException("a route needs at least two points", nameof(route));
            _xz = route.Polyline;
            _points = _xz.Length / 2;
            _cumulative = new double[_points];
            for (int i = 1; i < _points; i++)
            {
                double dx = _xz[2 * i] - _xz[2 * i - 2], dz = _xz[2 * i + 1] - _xz[2 * i - 1];
                _cumulative[i] = _cumulative[i - 1] + Math.Sqrt(dx * dx + dz * dz);
            }
            TotalM = _cumulative[_points - 1];
            DistanceToRouteM = double.PositiveInfinity;
        }

        public PlannedRoute Route { get; private set; }

        /// <summary>Length of the polyline, metres.</summary>
        public double TotalM { get; private set; }

        /// <summary>Distance along the route to the explorer's projection.</summary>
        public double ProgressM { get; private set; }

        public double RemainingM
        {
            get { return Math.Max(0.0, TotalM - ProgressM); }
        }

        /// <summary>The route's travel time scaled by the share still to go, seconds.</summary>
        public double EtaSeconds
        {
            get
            {
                if (Arrived) return 0.0;
                if (!(TotalM > 1e-6)) return 0.0;
                return Route.TimeS * RemainingM / TotalM;
            }
        }

        /// <summary>Distance from the explorer to the route (after <see cref="Update"/>).</summary>
        public double DistanceToRouteM { get; private set; }

        /// <summary>Heading (radians, 0 = north, clockwise) from the explorer to the steering point ahead on the route.</summary>
        public float BearingRad { get; private set; }

        public bool Arrived { get; private set; }

        public RouteStatus Status { get; private set; }

        /// <summary>The steering direction relative to <paramref name="referenceHeadingRad"/> (the camera or the
        /// explorer), in [−π, π): 0 is straight ahead, positive to the right.</summary>
        public float RelativeBearing(float referenceHeadingRad)
        {
            return Wrap(BearingRad - referenceHeadingRad);
        }

        /// <summary>Updates the guide for the explorer at (x, z) after <paramref name="dt"/> seconds.</summary>
        public RouteStatus Update(double x, double z, float dt)
        {
            if (Arrived) return RouteStatus.Arrived;
            if (double.IsNaN(x) || double.IsNaN(z)) return Status;

            int first = Math.Max(0, _segment - WindowBehind);
            int last = Math.Min(_points - 2, _segment + WindowAhead);
            int segment;
            double t, distance;
            Nearest(x, z, first, last, ProgressM, ContinuityPerM, out segment, out t, out distance);
            if (distance > RelocateM)
            {
                // Lost (a teleport, a long detour): the plain nearest point of the whole route.
                int s2;
                double t2, d2;
                Nearest(x, z, 0, _points - 2, 0.0, 0.0, out s2, out t2, out d2);
                if (d2 < distance - 1e-6)
                {
                    segment = s2;
                    t = t2;
                    distance = d2;
                }
            }
            _segment = segment;
            ProgressM = _cumulative[segment] + t * (_cumulative[segment + 1] - _cumulative[segment]);
            DistanceToRouteM = distance;

            // Arrived: at the destination itself; or at the end of the road near the end of the route (a route that passes
            // close to its end earlier, out and back, does not count); or on the route with 40 m to go.
            double endX = _xz[2 * _points - 2], endZ = _xz[2 * _points - 1];
            if (Hypot(x - Route.DestinationX, z - Route.DestinationZ) <= ArrivalRadiusM ||
                Hypot(x - endX, z - endZ) <= ArrivalRadiusM && RemainingM <= 2.0 * ArrivalRadiusM ||
                RemainingM <= ArrivalRadiusM && distance <= ArrivalRadiusM)
            {
                Arrived = true;
                ProgressM = TotalM;
                Status = RouteStatus.Arrived;
                return Status;
            }

            double px, pz;
            PointAt(Math.Min(TotalM, ProgressM + LookAheadM), out px, out pz);
            if (Hypot(px - x, pz - z) > 1e-3) BearingRad = (float)Math.Atan2(px - x, pz - z);

            if (distance > OffRouteM) _offTime += Math.Max(0f, dt);
            else _offTime = 0f;
            Status = _offTime >= OffRouteSeconds ? RouteStatus.OffRoute : RouteStatus.OnRoute;
            return Status;
        }

        /// <summary>The point <paramref name="distance"/> metres along the route (clamped to its ends).</summary>
        public void PointAt(double distance, out double x, out double z)
        {
            if (distance <= 0)
            {
                x = _xz[0];
                z = _xz[1];
                return;
            }
            if (distance >= TotalM)
            {
                x = _xz[2 * _points - 2];
                z = _xz[2 * _points - 1];
                return;
            }
            int lo = 0, hi = _points - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_cumulative[mid] <= distance) lo = mid;
                else hi = mid;
            }
            double len = _cumulative[hi] - _cumulative[lo];
            double f = len > 1e-9 ? (distance - _cumulative[lo]) / len : 0.0;
            x = _xz[2 * lo] + (_xz[2 * hi] - _xz[2 * lo]) * f;
            z = _xz[2 * lo + 1] + (_xz[2 * hi + 1] - _xz[2 * lo + 1]) * f;
        }

        /// <summary>
        /// The point of segments [first, last] closest to (x, z), where a candidate costs its distance plus
        /// <paramref name="perMetre"/> times how far its progress is from <paramref name="progress"/>: on a route that
        /// passes the same street twice the leg being ridden wins over the one further along.
        /// </summary>
        private void Nearest(double x, double z, int first, int last, double progress, double perMetre,
                             out int bestSegment, out double bestT, out double bestDistance)
        {
            bestSegment = first;
            bestT = 0.0;
            bestDistance = double.PositiveInfinity;
            double bestCost = double.PositiveInfinity;
            for (int i = first; i <= last; i++)
            {
                double ax = _xz[2 * i], az = _xz[2 * i + 1];
                double dx = _xz[2 * i + 2] - ax, dz = _xz[2 * i + 3] - az;
                double len2 = dx * dx + dz * dz;
                double t = len2 > 1e-12 ? ((x - ax) * dx + (z - az) * dz) / len2 : 0.0;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                double d = Hypot(ax + dx * t - x, az + dz * t - z);
                double along = _cumulative[i] + t * (_cumulative[i + 1] - _cumulative[i]);
                double cost = d + perMetre * Math.Abs(along - progress);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestDistance = d;
                    bestSegment = i;
                    bestT = t;
                }
            }
        }

        private static double Hypot(double a, double b)
        {
            return Math.Sqrt(a * a + b * b);
        }

        private static float Wrap(float a)
        {
            const float pi = (float)Math.PI;
            a = (a + pi) % (2f * pi);
            if (a < 0f) a += 2f * pi;
            return a - pi;
        }
    }
}
