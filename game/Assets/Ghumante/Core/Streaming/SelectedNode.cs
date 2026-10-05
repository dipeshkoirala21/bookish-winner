using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Streaming
{
    /// <summary>
    /// One quadtree cell chosen by <see cref="TileSelector"/>: <see cref="Area"/> is the cell to draw and
    /// <see cref="Source"/> the finest existing tile (the area itself or an ancestor) whose data renders it. When
    /// they differ, the source's terrain is cropped to the area. Vector content (roads, buildings, areas, POIs) is
    /// drawn only for nodes with <see cref="DrawsDetail"/> (exact nodes inside the tier's
    /// <see cref="StreamingConfig.DetailRadiusM"/>) whose source has detail chunks. <see cref="DrawsDetail"/> is part
    /// of the node's identity, so a node that enters or leaves the detail radius is rebuilt.
    /// </summary>
    public readonly struct SelectedNode : IEquatable<SelectedNode>, IComparable<SelectedNode>
    {
        public readonly TileId Area;
        public readonly TileId Source;

        /// <summary>Roads, buildings and areas are meshed for this node (only ever true for exact nodes).</summary>
        public readonly bool DrawsDetail;

        /// <summary>A node that draws detail whenever it is exact.</summary>
        public SelectedNode(TileId area, TileId source) : this(area, source, true)
        {
        }

        /// <summary>A node; <paramref name="drawsDetail"/> is ignored (false) unless the node is exact.</summary>
        public SelectedNode(TileId area, TileId source, bool drawsDetail)
        {
            if (source.Level > area.Level || !TileArea.Contains(source, area))
                throw new ArgumentException("source " + source + " does not contain area " + area);
            Area = area;
            Source = source;
            DrawsDetail = drawsDetail && area == source;
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
            return Area == other.Area && Source == other.Source && DrawsDetail == other.DrawsDetail;
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
                return ((int)h ^ (int)(h >> 32)) ^ (DrawsDetail ? 0x5BD1E995 : 0);
            }
        }

        /// <summary>Orders by area key, then source key, then nodes without detail first.</summary>
        public int CompareTo(SelectedNode other)
        {
            int c = Area.Key.CompareTo(other.Area.Key);
            if (c == 0) c = Source.Key.CompareTo(other.Source.Key);
            return c != 0 ? c : DrawsDetail.CompareTo(other.DrawsDetail);
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
            if (!IsExact) return Area + "<" + Source;
            return DrawsDetail ? Area.ToString() : Area + " (terrain)";
        }
    }
}
