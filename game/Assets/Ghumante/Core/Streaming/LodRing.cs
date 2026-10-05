using System.Globalization;

namespace Ghumante.Core.Streaming
{
    /// <summary>
    /// One LOD ring (ARCHITECTURE.md 7.2): quadtree areas of <see cref="Level"/> are used where their closest point
    /// lies within <see cref="RadiusM"/> of the focus (an area of level <c>Level - 1</c> is split into four of
    /// <c>Level</c> there). Rings are listed finest level first; the coarsest ring's radius is the view radius.
    /// </summary>
    public struct LodRing
    {
        /// <summary>Quadtree level of the areas this ring holds (10 = 1 024 m leaf tiles).</summary>
        public int Level;

        /// <summary>Distance in metres from the focus to an area's closest point within which this level is used.</summary>
        public double RadiusM;

        /// <summary>Target terrain quads along one side of an area of this level; 0 uses every source sample
        /// (<see cref="StreamingConfig.TerrainStep"/> turns it into the mesher's decimation step).</summary>
        public int TerrainQuads;

        public LodRing(int level, double radiusM, int terrainQuads = 0)
        {
            Level = level;
            RadiusM = radiusM;
            TerrainQuads = terrainQuads;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "L{0} {1:0} m ({2} quads)", Level, RadiusM, TerrainQuads);
        }
    }
}
