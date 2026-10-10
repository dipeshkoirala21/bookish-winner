using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Traffic
{
    /// <summary>
    /// Who may drive a road piece beyond its width mask (docs/W2_DETAIL_CONTRACT.md decision 5): cars, taxis, jeeps,
    /// micros, tempos, buses and trucks only on car-accessible roads, motorbikes and bicycles anywhere, and the deck and
    /// surface heights lanes and walkers follow on bridges, flyovers and underpasses (§3).
    /// </summary>
    public static class RoadAccess
    {
        /// <summary>A street narrower than this in reality is no car street, however wide the game draws it.</summary>
        public const float MinCarRealWidthM = 3.0f;

        /// <summary>
        /// True when cars may use road <paramref name="roadIndex"/> of the tile: its structure record's
        /// <see cref="RoadStructureFlags.CarAccessible"/> when the tile has structure records; otherwise the same rule
        /// from the data at hand: not a footway, path, steps, pedestrian street, cycleway or bridleway, no
        /// <c>motorcar=no</c>-style access without cars, jeeps and buses, and a real width of at least
        /// <see cref="MinCarRealWidthM"/> (<paramref name="realWidthM"/> ≤ 0: unknown, allowed).
        /// </summary>
        public static bool CarAllowed(TileData t, int roadIndex, float realWidthM)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            RoadRecord r = t.Roads[roadIndex];
            if ((r.Flags & RoadFlags.Tunnel) != 0) return false;
            if (t.RoadStructures.Count == t.Roads.Count && t.RoadStructures.Count > 0)
            {
                RoadStructureRecord st = t.RoadStructures[roadIndex];
                return st.Has(RoadStructureFlags.CarAccessible) && st.Kind != RoadStructureKind.Tunnel;
            }
            switch (r.RoadClass)
            {
                case RoadClass.Footway:
                case RoadClass.Path:
                case RoadClass.Steps:
                case RoadClass.Pedestrian:
                case RoadClass.Cycleway:
                case RoadClass.Bridleway:
                    return false;
            }
            if (r.Access != Travel.None && (r.Access & (Travel.Car | Travel.Jeep | Travel.Bus)) == 0) return false;
            return !(realWidthM > 0f) || realWidthM >= MinCarRealWidthM;
        }

        /// <summary>The class mask after the car rule: on a road cars may not use, only <see cref="VehicleClasses.NarrowStreet"/>.</summary>
        public static uint Restrict(uint mask, TileData t, int roadIndex, float realWidthM)
        {
            return CarAllowed(t, roadIndex, realWidthM) ? mask : mask & VehicleClasses.NarrowStreet;
        }

        /// <summary>True for a bridge or flyover (structure record, else the Bridge flag).</summary>
        public static bool IsElevated(TileData t, int roadIndex)
        {
            if (t.RoadStructures.Count == t.Roads.Count && t.RoadStructures.Count > 0)
            {
                RoadStructureKind k = t.RoadStructures[roadIndex].Kind;
                return k == RoadStructureKind.Bridge || k == RoadStructureKind.Flyover;
            }
            return (t.Roads[roadIndex].Flags & RoadFlags.Bridge) != 0;
        }

        /// <summary>The structure surface heights of a road (one per point), or null when it is draped.</summary>
        public static float[] SurfaceHeights(TileData t, int roadIndex)
        {
            RoadStructureRecord st = t.RoadStructureOf(roadIndex);
            return st.DeckY != null && st.DeckY.Length == t.Roads[roadIndex].PointCount ? st.DeckY : null;
        }

        /// <summary>The surface height at <paramref name="s"/> metres along a piece whose points <paramref name="first"/>..
        /// <paramref name="last"/> lie at the arc lengths <paramref name="along"/> (indexed by point).</summary>
        public static float SurfaceAt(float[] heights, double[] along, int first, int last, double s)
        {
            if (s <= along[first]) return heights[first];
            for (int i = first; i < last; i++)
            {
                if (s > along[i + 1]) continue;
                double len = along[i + 1] - along[i];
                double f = len > 1e-9 ? (s - along[i]) / len : 0.0;
                return (float)(heights[i] + (heights[i + 1] - heights[i]) * f);
            }
            return heights[last];
        }
    }
}
