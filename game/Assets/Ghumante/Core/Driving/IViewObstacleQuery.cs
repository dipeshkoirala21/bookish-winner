namespace Ghumante.Core.Driving
{
    /// <summary>
    /// Solid world geometry for cameras (docs/W2_DETAIL_CONTRACT.md §3): buildings, walls, temples, statues,
    /// decks. Implemented by Track COLLIDE; the chase camera pulls in instead of entering walls.
    /// </summary>
    public interface IViewObstacleQuery
    {
        /// <summary>Sweep a sphere from (ox, oy, oz) along the unit direction (dx, dy, dz) up to maxDist; hitDist is
        /// the distance travelled before contact. False when nothing is hit.</summary>
        bool SphereCast(double ox, double oy, double oz, double dx, double dy, double dz, double radius, double maxDist,
            out double hitDist);
    }
}
