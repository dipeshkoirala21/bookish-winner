namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Terrain height at a game position (metres, X east, Z north, absolute Y); false where it has no data. The
    /// meshers place roads, buildings and areas with it; <see cref="TileHeightSampler"/> is the implementation that
    /// matches the rendered terrain exactly. Implementations used by worker threads must be thread-safe.
    /// </summary>
    public interface IHeightSampler
    {
        bool TryHeight(double x, double z, out float h);
    }
}
