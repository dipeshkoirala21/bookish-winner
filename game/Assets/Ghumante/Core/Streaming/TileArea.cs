using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Streaming
{
    /// <summary>Quadtree geometry helpers on <see cref="TileId"/> squares (game metres, X east, Z north).</summary>
    public static class TileArea
    {
        /// <summary>True when <paramref name="outer"/> is <paramref name="inner"/> or one of its ancestors.</summary>
        public static bool Contains(TileId outer, TileId inner)
        {
            int d = inner.Level - outer.Level;
            if (d < 0) return false;
            return inner.Tx >> d == outer.Tx && inner.Ty >> d == outer.Ty;
        }

        /// <summary>True when the two squares share interior (one contains the other).</summary>
        public static bool Overlaps(TileId a, TileId b)
        {
            return a.Level <= b.Level ? Contains(a, b) : Contains(b, a);
        }

        /// <summary>The ancestor of <paramref name="t"/> at <paramref name="level"/> (itself when equal).</summary>
        public static TileId AncestorAt(TileId t, int level)
        {
            int d = t.Level - level;
            if (d < 0) throw new ArgumentOutOfRangeException(nameof(level), "level " + level + " is finer than tile " + t);
            return d == 0 ? t : new TileId(level, t.Tx >> d, t.Ty >> d);
        }

        /// <summary>Squared distance in metres from (x, z) to the closest point of the tile's square (0 inside).</summary>
        public static double ClosestDistanceSquared(TileId t, double x, double z)
        {
            double s = TileId.SizeAt(t.Level);
            double x0 = t.Tx * s, z0 = t.Ty * s;
            double dx = x < x0 ? x0 - x : x > x0 + s ? x - (x0 + s) : 0.0;
            double dz = z < z0 ? z0 - z : z > z0 + s ? z - (z0 + s) : 0.0;
            return dx * dx + dz * dz;
        }

        /// <summary>Distance in metres from (x, z) to the closest point of the tile's square (0 inside).</summary>
        public static double ClosestDistance(TileId t, double x, double z)
        {
            return Math.Sqrt(ClosestDistanceSquared(t, x, z));
        }

        /// <summary>The child of <paramref name="t"/> in quadrant <paramref name="q"/> (0 SW, 1 SE, 2 NW, 3 NE), as
        /// <see cref="TileId.Children"/> but without allocating.</summary>
        public static TileId Child(TileId t, int q)
        {
            return new TileId(t.Level + 1, t.Tx * 2 + (q & 1), t.Ty * 2 + (q >> 1));
        }
    }
}
