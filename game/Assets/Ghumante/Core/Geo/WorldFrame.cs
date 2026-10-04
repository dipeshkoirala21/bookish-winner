namespace Ghumante.Core.Geo
{
    /// <summary>
    /// The game frame (ARCHITECTURE.md 5.1, ADR-004): <c>game = canonical - origin</c>, true 1:1 with no
    /// scale warp. X is east, Z is north (Unity's Y is up). Mirrors pipeline/ghumante_pipeline/projection.py.
    /// </summary>
    public static class WorldFrame
    {
        /// <summary>Canonical easting subtracted to get game X.</summary>
        public const double OriginE = 100000.0;

        /// <summary>Canonical northing subtracted to get game Z.</summary>
        public const double OriginN = 2900000.0;

        /// <summary>Side of the quadtree root tile in metres (2^20).</summary>
        public const double RootSizeM = 1048576.0;

        public static void CanonicalToGame(double easting, double northing, out double x, out double z)
        {
            x = easting - OriginE;
            z = northing - OriginN;
        }

        public static void GameToCanonical(double x, double z, out double easting, out double northing)
        {
            easting = x + OriginE;
            northing = z + OriginN;
        }

        public static void LonLatToGame(double lonDeg, double latDeg, out double x, out double z)
        {
            double e, n;
            Tm84.Forward(lonDeg, latDeg, out e, out n);
            CanonicalToGame(e, n, out x, out z);
        }

        public static void GameToLonLat(double x, double z, out double lonDeg, out double latDeg)
        {
            double e, n;
            GameToCanonical(x, z, out e, out n);
            Tm84.Inverse(e, n, out lonDeg, out latDeg);
        }

        /// <summary>Game position (height 0) of a geographic point.</summary>
        public static WorldPos LonLatToWorldPos(double lonDeg, double latDeg, float y = 0f)
        {
            double x, z;
            LonLatToGame(lonDeg, latDeg, out x, out z);
            return new WorldPos(x, y, z);
        }
    }
}
