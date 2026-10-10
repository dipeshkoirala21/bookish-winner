namespace Ghumante.Core.Meshing.Roads
{
    /// <summary>Rideability limits from the owner's feedback (docs/W2_DETAIL_CONTRACT.md §1).</summary>
    public static class RoadClearance
    {
        /// <summary>Minimum clear corridor of any drawn road (three motorbikes side by side plus margins).</summary>
        public const float MinCorridorM = 4.8f;
        /// <summary>Nothing (balcony, eave, strut, sign, wire) may overhang a road corridor below this height.</summary>
        public const float MinOverheadClearanceM = 4.5f;
        /// <summary>Minimum free height of a road passing under a bridge or flyover.</summary>
        public const float MinUnderpassClearanceM = 5.5f;
    }

    /// <summary>
    /// Clear road corridors of one tile in game metres (implemented by Track ROADS as RoadCorridorIndex).
    /// Buildings use it to keep footprints and overhangs out of roads; collisions and props use it too.
    /// </summary>
    public interface IRoadCorridorQuery
    {
        /// <summary>Signed distance (m) from (x, z) to the nearest corridor edge: negative inside a corridor.</summary>
        double SignedDistance(double x, double z);

        /// <summary>True when the polygon (n points, game metres) intrudes into any corridor; depth = deepest intrusion.</summary>
        bool Overlaps(double[] x, double[] z, int n, out double depthM);
    }
}
