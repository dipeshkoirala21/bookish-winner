using System;
using Ghumante.Core.Aviation;
using Ghumante.Core.Data;

namespace Ghumante.Core.Driving
{
    /// <summary>What an aircraft stands or lands on.</summary>
    public enum FlightSurfaceKind : byte
    {
        /// <summary>No ground is known there (outside the streamed tiles): the fallback floor. Never a landing place.</summary>
        Unknown = 0,

        /// <summary>The runway pavement or its shoulders.</summary>
        Runway = 1,

        /// <summary>Roads, aprons, squares and other paved ground.</summary>
        Paved = 2,

        /// <summary>Fields, grass, gravel and dirt.</summary>
        Open = 3,

        /// <summary>Rivers, ponds and wetland.</summary>
        Water = 4,

        /// <summary>A walkable structure or a bridge deck.</summary>
        Structure = 5,
    }

    /// <summary>The ground under an aircraft (game metres, absolute height, unit normal).</summary>
    public struct FlightGroundSample
    {
        public float Height;
        public float Nx, Ny, Nz;
        public FlightSurfaceKind Kind;

        /// <summary>Slope of the ground in radians (0 = level).</summary>
        public float Slope
        {
            get { return (float)Math.Acos(Math.Max(-1f, Math.Min(1f, Ny))); }
        }
    }

    /// <summary>What a solid sweep hit.</summary>
    public enum FlightObstacle : byte
    {
        None = 0,

        /// <summary>A building, wall, temple, statue, pole or the underside of a deck.</summary>
        Structure = 1,

        /// <summary>The terrain (hillsides).</summary>
        Terrain = 2,
    }

    /// <summary>
    /// The world as an aircraft sees it (docs/research/w2/ref_flight.md §5): ground heights with what they are, solid
    /// obstacles along a move, and where the nearest runway starts (for the friendly reset). <see cref="FlightWorld"/>
    /// implements it over the region's ground query and the airport sidecar; tests use small fakes.
    /// </summary>
    public interface IFlightWorld
    {
        /// <summary>The ground at (x, z) for an aircraft at height <paramref name="nearY"/> (bridge decks count when the
        /// aircraft is near them). Always answers: where nothing is known the floor is <see cref="FlightSurfaceKind.Unknown"/>.</summary>
        void Ground(double x, double z, float nearY, out FlightGroundSample g);

        /// <summary>Sweeps a sphere of <paramref name="radius"/> from a to b; true on contact, with the fraction of the
        /// move done before it and what was hit.</summary>
        bool Sweep(double ax, double ay, double az, double bx, double by, double bz, float radius, out float t, out FlightObstacle hit);

        /// <summary>The runway end nearest to (x, z), lined up for take-off (position on the centreline at the threshold,
        /// heading along the runway, surface height); false without an airport.</summary>
        bool TryRunwayStart(double x, double z, out double rx, out double rz, out float ry, out float headingRad);

        /// <summary>Distance from (x, z) to the runway pavement (∞ without an airport).</summary>
        double DistanceToRunway(double x, double z);
    }

    /// <summary>
    /// Soft region bounds for flying (the aircraft turns back gently past <see cref="MarginM"/> inside the edge and can
    /// never leave the hard rectangle) and the altitude ceiling.
    /// </summary>
    public struct FlightBounds
    {
        public double MinX, MinZ, MaxX, MaxZ;

        /// <summary>Width of the turn-back band inside the edge (m).</summary>
        public float MarginM;

        /// <summary>Highest altitude the aircraft may climb to (m above sea level).</summary>
        public float CeilingM;

        /// <summary>False: no bounds (tests, free flight).</summary>
        public bool Enabled;

        public FlightBounds(double minX, double minZ, double maxX, double maxZ, float marginM, float ceilingM)
        {
            MinX = Math.Min(minX, maxX);
            MinZ = Math.Min(minZ, maxZ);
            MaxX = Math.Max(minX, maxX);
            MaxZ = Math.Max(minZ, maxZ);
            MarginM = Math.Max(100f, Math.Min(marginM, (float)(0.45 * Math.Min(MaxX - MinX, MaxZ - MinZ))));
            CeilingM = ceilingM;
            Enabled = true;
        }

        public double CentreX
        {
            get { return 0.5 * (MinX + MaxX); }
        }

        public double CentreZ
        {
            get { return 0.5 * (MinZ + MaxZ); }
        }

        /// <summary>How far (m) a point lies inside the turn-back band, measured from its inner edge (0 = not in it).</summary>
        public double DepthIntoMargin(double x, double z)
        {
            if (!Enabled) return 0;
            double ix0 = MinX + MarginM, ix1 = MaxX - MarginM, iz0 = MinZ + MarginM, iz1 = MaxZ - MarginM;
            double dx = x < ix0 ? ix0 - x : x > ix1 ? x - ix1 : 0;
            double dz = z < iz0 ? iz0 - z : z > iz1 ? z - iz1 : 0;
            return Math.Max(dx, dz);
        }

        /// <summary>
        /// The region of a manifest's game bbox, grown to hold the whole runway with room for a circuit
        /// (<paramref name="airport"/> may be null), plus <paramref name="outsideM"/> on every side.
        /// </summary>
        public static FlightBounds ForRegion(double minX, double minZ, double maxX, double maxZ, AviationConfig airport, float outsideM = 3000f,
                                             float ceilingM = 6000f)
        {
            if (airport != null)
            {
                double ax0 = Math.Min(airport.Threshold02.X, airport.Threshold20.X) - 6000, ax1 = Math.Max(airport.Threshold02.X, airport.Threshold20.X) + 6000;
                double az0 = Math.Min(airport.Threshold02.Z, airport.Threshold20.Z) - 6000, az1 = Math.Max(airport.Threshold02.Z, airport.Threshold20.Z) + 6000;
                minX = Math.Min(minX, ax0);
                maxX = Math.Max(maxX, ax1);
                minZ = Math.Min(minZ, az0);
                maxZ = Math.Max(maxZ, az1);
            }
            return new FlightBounds(minX - outsideM, minZ - outsideM, maxX + outsideM, maxZ + outsideM, 2500f, ceilingM);
        }
    }

    /// <summary>
    /// <see cref="IFlightWorld"/> over the region: the runway of the airport sidecar (its drawn profile: the straight line
    /// between the threshold elevations raised over the terrain, plus the runway mesh's lift, exactly as
    /// <c>RunwayMesher</c> draws it, with 7.5 m shoulders), everything else from the ground query (roads, aprons and
    /// squares are paved, water is water, structures and decks are structures) and the solids of its
    /// <see cref="IViewObstacleQuery"/>. Where no tile is resident the floor is <see cref="FallbackHeightM"/>. Main
    /// thread (the ground query is not thread safe).
    /// </summary>
    public sealed class FlightWorld : IFlightWorld
    {
        /// <summary>Lift of the drawn runway over its profile (RunwayMesher.SurfaceLiftM) and its shoulder width.</summary>
        public const float RunwayLiftM = 0.3f, ShoulderM = 7.5f;

        private readonly AviationConfig _airport;
        private IGroundQuery _ground;
        private IViewObstacleQuery _solids;
        private double _ox, _oz, _dirX, _dirZ, _len, _southS, _northS;
        private float _halfW;

        /// <summary>Floor height where nothing is resident (default: the airport's elevation minus 40 m, else 1,250 m).</summary>
        public float FallbackHeightM;

        public FlightWorld(AviationConfig airport, IGroundQuery ground, IViewObstacleQuery solids)
        {
            _airport = airport;
            _ground = ground;
            _solids = solids ?? ground as IViewObstacleQuery;
            FallbackHeightM = airport != null ? airport.ElevationM - 40f : 1250f;
            if (airport == null) return;
            _ox = airport.Threshold02.X;
            _oz = airport.Threshold02.Z;
            double dx = airport.Threshold20.X - _ox, dz = airport.Threshold20.Z - _oz;
            _len = Math.Sqrt(dx * dx + dz * dz);
            if (_len < 100)
            {
                _airport = null;
                return;
            }
            _dirX = dx / _len;
            _dirZ = dz / _len;
            _southS = Along(airport.PavementSouthX, airport.PavementSouthZ);
            _northS = Along(airport.PavementNorthX, airport.PavementNorthZ);
            if (!(_southS < 0) || airport.PavementSouthX == 0) _southS = -285;
            if (!(_northS > _len) || airport.PavementNorthX == 0) _northS = _len + 277;
            _halfW = 0.5f * airport.RunwayWidthM;
        }

        /// <summary>The airport (null without one).</summary>
        public AviationConfig Airport
        {
            get { return _airport; }
        }

        /// <summary>Swaps the ground query (a region reopened).</summary>
        public void SetGround(IGroundQuery ground, IViewObstacleQuery solids)
        {
            _ground = ground;
            _solids = solids ?? ground as IViewObstacleQuery;
        }

        /// <summary>Along distance of a point from threshold 02 towards 20.</summary>
        public double Along(double x, double z)
        {
            return (x - _ox) * _dirX + (z - _oz) * _dirZ;
        }

        /// <summary>Signed lateral offset of a point from the centreline (positive to the right of 02→20).</summary>
        public double Lateral(double x, double z)
        {
            return (x - _ox) * _dirZ - (z - _oz) * _dirX;
        }

        /// <summary>True on the paved runway (with shoulders).</summary>
        public bool OnRunway(double x, double z)
        {
            if (_airport == null) return false;
            double s = Along(x, z), l = Lateral(x, z);
            return s >= _southS && s <= _northS && Math.Abs(l) <= _halfW + ShoulderM;
        }

        public void Ground(double x, double z, float nearY, out FlightGroundSample g)
        {
            g = default(FlightGroundSample);
            GroundSample s;
            bool known;
            var layered = _ground as ILayeredGroundQuery;
            if (layered != null) known = layered.TrySample(x, z, nearY, out s);
            else if (_ground != null) known = _ground.TrySample(x, z, out s);
            else
            {
                known = false;
                s = default(GroundSample);
            }
            if (OnRunway(x, z))
            {
                double along = Along(x, z);
                float y = (float)(_airport.Threshold02.ElevM + (_airport.Threshold20.ElevM - _airport.Threshold02.ElevM) * (along / _len));
                if (known && s.Height + 0.05f > y) y = s.Height + 0.05f;
                g.Height = y + RunwayLiftM;
                // The profile's slope along the runway.
                float grade = (float)((_airport.Threshold20.ElevM - _airport.Threshold02.ElevM) / _len);
                float inv = 1f / (float)Math.Sqrt(1f + grade * grade);
                g.Nx = (float)(-grade * _dirX) * inv;
                g.Ny = inv;
                g.Nz = (float)(-grade * _dirZ) * inv;
                g.Kind = FlightSurfaceKind.Runway;
                return;
            }
            if (!known)
            {
                g.Height = FallbackHeightM;
                g.Ny = 1f;
                g.Kind = FlightSurfaceKind.Unknown;
                return;
            }
            g.Height = s.Height;
            g.Nx = s.Nx;
            g.Ny = s.Ny > 0.05f ? s.Ny : 1f;
            g.Nz = s.Nz;
            if (s.OnDeck || s.OnStructure) g.Kind = FlightSurfaceKind.Structure;
            else if (s.Biome == Biome.Water && !s.OnRoad) g.Kind = FlightSurfaceKind.Water;
            else if (s.OnRoad || s.Surface == SurfaceGroup.Paved) g.Kind = FlightSurfaceKind.Paved;
            else g.Kind = FlightSurfaceKind.Open;
        }

        public bool Sweep(double ax, double ay, double az, double bx, double by, double bz, float radius, out float t, out FlightObstacle hit)
        {
            t = 1f;
            hit = FlightObstacle.None;
            if (_solids == null) return false;
            double dx = bx - ax, dy = by - ay, dz = bz - az;
            double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-6) return false;
            double d;
            if (!_solids.SphereCast(ax, ay, az, dx / len, dy / len, dz / len, radius, len, out d)) return false;
            t = (float)Math.Max(0.0, Math.Min(1.0, d / len));
            // Terrain or a solid? The ground under the contact point tells.
            double hx = ax + dx * t, hy = ay + dy * t, hz = az + dz * t;
            FlightGroundSample g;
            Ground(hx, hz, (float)hy, out g);
            // The sphere's bottom on the ground surface: terrain; clear of it: a building, a wall or a deck.
            hit = hy - radius <= g.Height + 0.3 ? FlightObstacle.Terrain : FlightObstacle.Structure;
            return true;
        }

        public bool TryRunwayStart(double x, double z, out double rx, out double rz, out float ry, out float headingRad)
        {
            rx = rz = 0;
            ry = 0f;
            headingRad = 0f;
            if (_airport == null) return false;
            double s = Along(x, z);
            // Line up a little inside the threshold of the nearer end (the displaced section stays behind for the roll).
            bool from02 = s < 0.5 * _len;
            double at = from02 ? 30.0 : _len - 30.0;
            rx = _ox + _dirX * at;
            rz = _oz + _dirZ * at;
            FlightGroundSample g;
            Ground(rx, rz, 1e6f, out g);
            ry = g.Height;
            float h = (float)Math.Atan2(_dirX, _dirZ);
            headingRad = from02 ? h : ArcadeVehicle.WrapAngle(h + (float)Math.PI);
            return true;
        }

        public double DistanceToRunway(double x, double z)
        {
            if (_airport == null) return double.PositiveInfinity;
            double s = Along(x, z), l = Math.Abs(Lateral(x, z)) - _halfW;
            double ds = s < _southS ? _southS - s : s > _northS ? s - _northS : 0;
            double dl = Math.Max(0, l);
            return Math.Sqrt(ds * ds + dl * dl);
        }
    }
}
