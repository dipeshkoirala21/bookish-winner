using System;
using System.Globalization;

namespace Ghumante.Core.Geo
{
    /// <summary>
    /// A position in the game frame (ARCHITECTURE.md 5.2): double-precision X (east) and Z (north) in metres,
    /// single-precision Y (up). Every persistent and simulation position uses this type; Unity transforms
    /// hold floats relative to the floating origin (<see cref="FloatingOrigin"/>).
    /// </summary>
    [Serializable]
    public struct WorldPos : IEquatable<WorldPos>
    {
        public double X;
        public float Y;
        public double Z;

        public WorldPos(double x, float y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static WorldPos FromXZ(double x, double z)
        {
            return new WorldPos(x, 0f, z);
        }

        public static readonly WorldPos Zero = new WorldPos(0.0, 0f, 0.0);

        /// <summary>Horizontal (XZ) distance in metres.</summary>
        public double DistanceXZ(WorldPos other)
        {
            double dx = X - other.X, dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public double DistanceXZSquared(WorldPos other)
        {
            double dx = X - other.X, dz = Z - other.Z;
            return dx * dx + dz * dz;
        }

        /// <summary>3D distance in metres.</summary>
        public double Distance(WorldPos other)
        {
            double dx = X - other.X, dy = (double)Y - other.Y, dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public WorldPos WithY(float y)
        {
            return new WorldPos(X, y, Z);
        }

        public WorldPos Offset(double dx, float dy, double dz)
        {
            return new WorldPos(X + dx, Y + dy, Z + dz);
        }

        /// <summary>Linear interpolation (t is not clamped).</summary>
        public static WorldPos Lerp(WorldPos a, WorldPos b, double t)
        {
            return new WorldPos(a.X + (b.X - a.X) * t, (float)(a.Y + (b.Y - a.Y) * t), a.Z + (b.Z - a.Z) * t);
        }

        /// <summary>WGS84 longitude/latitude of this position.</summary>
        public void ToLonLat(out double lonDeg, out double latDeg)
        {
            WorldFrame.GameToLonLat(X, Z, out lonDeg, out latDeg);
        }

        public static WorldPos operator +(WorldPos a, WorldPos b)
        {
            return new WorldPos(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static WorldPos operator -(WorldPos a, WorldPos b)
        {
            return new WorldPos(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static bool operator ==(WorldPos a, WorldPos b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(WorldPos a, WorldPos b)
        {
            return !a.Equals(b);
        }

        public bool Equals(WorldPos other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldPos other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = X.GetHashCode();
                h = h * 397 ^ Y.GetHashCode();
                return h * 397 ^ Z.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:F3}, {1:F2}, {2:F3})", X, Y, Z);
        }
    }
}
