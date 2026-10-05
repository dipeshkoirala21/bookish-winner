using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Streaming
{
    /// <summary>
    /// One quadtree cell chosen by <see cref="TileSelector"/>: <see cref="Area"/> is the cell to draw and
    /// <see cref="Source"/> the finest existing tile (the area itself or an ancestor) whose data renders it. When
    /// they differ, the source's terrain is cropped to the area. Vector content (roads, buildings, areas, POIs) is
    /// drawn only for exact nodes (<see cref="IsExact"/>) whose source has detail chunks.
    /// </summary>
    public readonly struct SelectedNode : IEquatable<SelectedNode>, IComparable<SelectedNode>
    {
        public readonly TileId Area;
        public readonly TileId Source;

        public SelectedNode(TileId area, TileId source)
        {
            if (source.Level > area.Level || !TileArea.Contains(source, area))
                throw new ArgumentException("source " + source + " does not contain area " + area);
            Area = area;
            Source = source;
        }

        /// <summary>The area is an existing tile drawn from its own data.</summary>
        public bool IsExact
        {
            get { return Area == Source; }
        }

        /// <summary>Levels between the source and the area (0 when exact).</summary>
        public int CropDepth
        {
            get { return Area.Level - Source.Level; }
        }

        public bool Equals(SelectedNode other)
        {
            return Area == other.Area && Source == other.Source;
        }

        public override bool Equals(object obj)
        {
            return obj is SelectedNode other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                ulong a = Area.Key, s = Source.Key;
                ulong h = a * 0x9E3779B97F4A7C15UL ^ (s + 0x632BE59BD9B4E019UL + (a << 6) + (a >> 2));
                return (int)h ^ (int)(h >> 32);
            }
        }

        /// <summary>Orders by area key, then source key.</summary>
        public int CompareTo(SelectedNode other)
        {
            int c = Area.Key.CompareTo(other.Area.Key);
            return c != 0 ? c : Source.Key.CompareTo(other.Source.Key);
        }

        public static bool operator ==(SelectedNode a, SelectedNode b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(SelectedNode a, SelectedNode b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return IsExact ? Area.ToString() : Area + "<" + Source;
        }
    }
}
