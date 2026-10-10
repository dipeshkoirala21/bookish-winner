using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Flora;

namespace Ghumante.Core.Generators.Placement
{
    /// <summary>
    /// House gardens (research street_life.md 9; ref_nature.md): what stands round Kathmandu Valley homes. At the front
    /// wall of a house (the BFNT front edge, else its longest wall): groups of potted plants (terracotta, painted tins
    /// and cut jerry cans) on 30-45% of houses, a tulsi math pedestal in front of 6-12%; by peri-urban and village
    /// houses banana plants (30%), a bamboo clump behind one house in four, poinsettia (lalupate) by a side wall, a
    /// small marigold bed or sunflowers by the door; bougainvillea at the corner of 8% of urban compounds. Old cores get
    /// pots only. Big buildings (over 600 m²) and sheds (under 20 m²) get nothing. Every plant keeps out of road
    /// corridors and stands just outside its wall. Deterministic per building.
    /// </summary>
    internal static class GardenPlacement
    {
        private const uint Purpose = 0x47415244;

        public static void Place(PlacementContext c)
        {
            TileData t = c.T;
            for (int bi = 0; bi < t.Buildings.Count && !c.PlantsFull; bi++)
            {
                BuildingRecord b = t.Buildings[bi];
                int[] ring = b.Rings[0];
                int n = ring.Length / 2;
                if (n < 3) continue;
                double area = Math.Abs(AreaTypeGrid.RingArea(ring));
                if (area < 20 || area > 600) continue;
                double cx = 0, cz = 0;
                for (int k = 0; k < n; k++)
                {
                    cx += ring[2 * k] / 100.0;
                    cz += ring[2 * k + 1] / 100.0;
                }
                cx /= n;
                cz /= n;
                BuildingFrontRecord f = t.BuildingFrontOf(bi);
                AreaType at = f.Area != AreaType.Unknown ? f.Area : c.AreaAt(cx, cz);
                var rng = new FloraRng(b.Seed != 0 ? b.Seed : (uint)(b.OsmRef ^ (b.OsmRef >> 32)), Purpose);
                int front = f.HasFront && f.FrontEdge < n ? f.FrontEdge : LongestEdge(ring);
                bool ccw = AreaTypeGrid.RingArea(ring) > 0;
                bool village = at == AreaType.PeriUrban || at == AreaType.Rural || at == AreaType.Hill || at == AreaType.Forest;

                // Pots by the door.
                float potShare = at == AreaType.OldCore ? 0.45f : at == AreaType.Urban ? 0.4f : 0.32f;
                if (rng.Chance(potShare))
                {
                    int groups = rng.Chance(0.35f) ? 2 : 1;
                    for (int g = 0; g < groups; g++) AlongWall(c, ring, front, ccw, TreeSpecies.PottedPlant, rng.Range(0.15f, 0.85f), 0.45f, ref rng);
                }
                if (at == AreaType.OldCore) continue;
                if (rng.Chance(village ? 0.12f : 0.06f)) AlongWall(c, ring, front, ccw, TreeSpecies.TulsiMath, rng.Range(0.3f, 0.7f), 1.4f, ref rng);
                if (village)
                {
                    int back = (front + n / 2) % n, side = (front + 1) % n;
                    if (rng.Chance(0.3f))
                    {
                        int k = rng.Int(1, 3);
                        for (int q = 0; q < k; q++) AlongWall(c, ring, rng.Chance(0.5f) ? side : back, ccw, TreeSpecies.Banana, rng.Range(0.1f, 0.9f), rng.Range(1.8f, 3.5f), ref rng);
                    }
                    if (rng.Chance(0.25f)) AlongWall(c, ring, back, ccw, TreeSpecies.Bamboo, rng.Range(0.2f, 0.8f), rng.Range(5f, 8f), ref rng);
                    if (rng.Chance(0.15f)) AlongWall(c, ring, side, ccw, TreeSpecies.Poinsettia, rng.Range(0.2f, 0.8f), 1.3f, ref rng);
                    if (rng.Chance(0.08f)) AlongWall(c, ring, front, ccw, TreeSpecies.MarigoldBed, rng.Range(0.2f, 0.8f), 1.6f, ref rng, true);
                    if (rng.Chance(0.07f)) AlongWall(c, ring, side, ccw, TreeSpecies.Sunflower, rng.Range(0.2f, 0.8f), 1.2f, ref rng);
                }
                else
                {
                    if (rng.Chance(0.08f)) AlongWall(c, ring, front, ccw, TreeSpecies.Bougainvillea, rng.Chance(0.5f) ? 0.05f : 0.95f, 1.1f, ref rng, true);
                    if (rng.Chance(0.06f)) AlongWall(c, ring, (front + 1) % n, ccw, TreeSpecies.Poinsettia, rng.Range(0.2f, 0.8f), 1.3f, ref rng);
                }
            }
        }

        /// <summary>
        /// Put a plant outside wall <paramref name="edge"/> of a ring at fraction <paramref name="t"/> along it,
        /// <paramref name="off"/> metres out (plus its own half width), turned along the wall when
        /// <paramref name="alongWall"/> (beds, bougainvillea), else at a random yaw.
        /// </summary>
        private static void AlongWall(PlacementContext c, int[] ring, int edge, bool ccw, TreeSpecies sp, float t, float off, ref FloraRng rng, bool alongWall = false)
        {
            int n = ring.Length / 2;
            int j = (edge + 1) % n;
            double ax = ring[2 * edge] / 100.0, az = ring[2 * edge + 1] / 100.0, bx = ring[2 * j] / 100.0, bz = ring[2 * j + 1] / 100.0;
            double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            if (len < 1.5) return;
            double ux = (bx - ax) / len, uz = (bz - az) / len;
            // Outward normal: right of the edge for a counter-clockwise ring.
            double nx = ccw ? uz : -uz, nz = ccw ? -ux : ux;
            float h, w;
            PlacementContext.Size01(sp, ref rng, out h, out w);
            double depth = sp == TreeSpecies.MarigoldBed ? 0.25 * w : sp == TreeSpecies.Bougainvillea ? 0.3 * w : 0.5 * w;
            double x = ax + ux * (t * len) + nx * (off + depth), z = az + uz * (t * len) + nz * (off + depth);
            if (!c.Clear(x, z, 0.35 * w, PlacementContext.RoadMarginFor(sp, h, w))) return;
            float yaw = alongWall ? (float)(Math.Atan2(-uz, ux) * 180 / Math.PI) : rng.Range(0f, 360f);
            c.Add(sp, x, z, h, w, yaw, TreeOrigin.Garden);
        }

        private static int LongestEdge(int[] ring)
        {
            int n = ring.Length / 2, best = 0;
            double bestL = -1;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double dx = ring[2 * j] - ring[2 * i], dz = ring[2 * j + 1] - ring[2 * i + 1];
                double l = dx * dx + dz * dz;
                if (l > bestL)
                {
                    bestL = l;
                    best = i;
                }
            }
            return best;
        }
    }
}
