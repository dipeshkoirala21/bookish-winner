using System;

namespace Ghumante.Core.Geo
{
    /// <summary>
    /// Pure floating-origin arithmetic (ARCHITECTURE.md 5.2, ADR-003). The Unity side owns the scene shift;
    /// this class only decides when to rebase, where the new origin goes, and converts between
    /// <see cref="WorldPos"/> and origin-relative float coordinates.
    /// </summary>
    public static class FloatingOrigin
    {
        /// <summary>Default rebase distance (2 km, ARCHITECTURE.md 5.2).</summary>
        public const double DefaultThresholdM = 2000.0;

        /// <summary>Default grid the new origin snaps to, so origins are exact and reproducible.</summary>
        public const double DefaultSnapM = 1.0;

        /// <summary>True when <paramref name="pos"/> is farther than <paramref name="threshold"/> metres
        /// (horizontally) from <paramref name="origin"/>.</summary>
        public static bool ShouldRebase(WorldPos pos, WorldPos origin, double threshold = DefaultThresholdM)
        {
            return pos.DistanceXZSquared(origin) > threshold * threshold;
        }

        /// <summary>
        /// New origin for a rebase around <paramref name="pos"/>: its X/Z snapped to a multiple of
        /// <paramref name="snap"/> (Y stays 0, the origin is horizontal only). <paramref name="delta"/> is
        /// <c>newOrigin - oldOrigin</c>; listeners subtract it from every scene position.
        /// </summary>
        public static WorldPos Rebase(WorldPos pos, WorldPos oldOrigin, out WorldPos delta, double snap = DefaultSnapM)
        {
            if (!(snap > 0.0)) throw new ArgumentOutOfRangeException(nameof(snap));
            var origin = new WorldPos(Math.Round(pos.X / snap) * snap, 0f, Math.Round(pos.Z / snap) * snap);
            delta = new WorldPos(origin.X - oldOrigin.X, origin.Y - oldOrigin.Y, origin.Z - oldOrigin.Z);
            return origin;
        }

        /// <summary>Scene-local float coordinates of a world position.</summary>
        public static void ToLocal(WorldPos pos, WorldPos origin, out float x, out float y, out float z)
        {
            x = (float)(pos.X - origin.X);
            y = pos.Y - origin.Y;
            z = (float)(pos.Z - origin.Z);
        }

        /// <summary>World position of scene-local float coordinates.</summary>
        public static WorldPos FromLocal(float x, float y, float z, WorldPos origin)
        {
            return new WorldPos(origin.X + x, origin.Y + y, origin.Z + z);
        }
    }
}
