using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The decoded edge neighbours of a source tile (same level, sharing a whole edge), used so terrain normals on a
    /// tile border are central differences across the border: both tiles then compute the same normal and slope
    /// colour for their shared edge vertices instead of a one-sided difference each (a visible shading seam on
    /// hillsides). A missing or mismatched neighbour (wrong tile, different grid size, no heights) is ignored and
    /// that border falls back to the one-sided difference. Immutable; safe to share between threads as long as
    /// the tiles are not modified.
    /// </summary>
    public readonly struct TileNeighbours
    {
        public readonly TileData West;
        public readonly TileData East;
        public readonly TileData South;
        public readonly TileData North;

        public TileNeighbours(TileData west, TileData east, TileData south, TileData north)
        {
            West = west;
            East = east;
            South = south;
            North = north;
        }

        /// <summary>No neighbours (one-sided differences on every border).</summary>
        public static TileNeighbours None
        {
            get { return default(TileNeighbours); }
        }

        /// <summary>True when no neighbour is set.</summary>
        public bool IsEmpty
        {
            get { return West == null && East == null && South == null && North == null; }
        }

        /// <summary>
        /// The neighbours of <paramref name="source"/> as <paramref name="lookup"/> answers them (null for tiles that
        /// are not decoded or not in the pack; tiles off the quadtree are never asked for).
        /// </summary>
        public static TileNeighbours Of(TileId source, Func<TileId, TileData> lookup)
        {
            if (lookup == null) throw new ArgumentNullException(nameof(lookup));
            int n = 1 << source.Level, l = source.Level, x = source.Tx, y = source.Ty;
            return new TileNeighbours(x > 0 ? lookup(new TileId(l, x - 1, y)) : null, x < n - 1 ? lookup(new TileId(l, x + 1, y)) : null,
                                      y > 0 ? lookup(new TileId(l, x, y - 1)) : null, y < n - 1 ? lookup(new TileId(l, x, y + 1)) : null);
        }

        /// <summary>Keep only the neighbours that really border <paramref name="source"/> with the same grid.</summary>
        internal TileNeighbours ValidFor(TileData source)
        {
            if (IsEmpty || source == null) return None;
            TileId s = source.Tile;
            int n = source.HeightsN;
            return new TileNeighbours(Match(West, s, -1, 0, n), Match(East, s, 1, 0, n), Match(South, s, 0, -1, n), Match(North, s, 0, 1, n));
        }

        private static TileData Match(TileData t, TileId s, int dx, int dy, int n)
        {
            if (t == null || !TerrainGrid.HasHeights(t) || t.HeightsN != n) return null;
            TileId id = t.Tile;
            return id.Level == s.Level && id.Tx == s.Tx + dx && id.Ty == s.Ty + dy ? t : null;
        }
    }
}
