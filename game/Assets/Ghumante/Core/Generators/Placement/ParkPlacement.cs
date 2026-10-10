using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Generators.Placement
{
    /// <summary>
    /// Park planting (research street_life.md 10; ref_nature.md: Ratna Park, Garden of Dreams, Tundikhel edges):
    /// inside every park polygon, ornamental trees (jacaranda, bottlebrush, camphor, silky oak, palms, a pipal) about
    /// one per 180 m², clipped hedges lining both sides of most footpaths through the park (as the Garden of Dreams and
    /// Ratna Park walks are lined) just outside the path's rideable corridor, a formal group of marigold beds with roses
    /// round a centre point, shrubs scattered on the lawns and clipped hedges along half of the boundary (1.2 m inside
    /// it, turned along it). Everything keeps out of road corridors (docs/W2_DETAIL_CONTRACT.md §1.1: every drawn way,
    /// footways included, keeps <see cref="RoadClearance.MinCorridorM"/> clear), buildings and water: a hedge's whole
    /// 3 × 0.9 m footprint, both ends included, so crossings stay open. Deterministic per park (OSM ref).
    /// </summary>
    internal static class ParkPlacement
    {
        private const uint Purpose = 0x5041524B;

        private static readonly float[] TreeMix = { 25, 20, 15, 10, 10, 5, 15 };

        /// <summary>Half the length and half the depth of a clipped hedge segment (3 × 0.9 m) and the gap it keeps
        /// from a road corridor.</summary>
        internal const double HedgeHalfLength = 1.5, HedgeHalfDepth = 0.45, HedgeGap = 0.3;

        public static void Place(PlacementContext c)
        {
            foreach (AreaRecord a in c.T.Areas)
            {
                if (a.Kind != AreaKind.Park || a.Indices == null || a.Indices.Length < 3) continue;
                double area = Area(a);
                if (area < 150) continue;
                var rng = new FloraRng((uint)(a.OsmRef ^ (a.OsmRef >> 32)) ^ c.Seed, Purpose);
                Trees(c, a, area, ref rng);
                PathHedges(c, a);
                Beds(c, a, area, ref rng);
                Scatter(c, a, TreeSpecies.Shrub, (int)Math.Min(60, area / 150), ref rng, TreeOrigin.Park);
                Hedges(c, a, ref rng);
                if (c.PlantsFull) return;
            }
        }

        private static void Trees(PlacementContext c, AreaRecord a, double area, ref FloraRng rng)
        {
            int n = (int)Math.Min(120, area / 180);
            for (int i = 0; i < n; i++)
            {
                double x, z;
                RandomPoint(a, ref rng, out x, out z);
                TreeSpecies sp;
                switch (rng.Pick(TreeMix))
                {
                    case 0: sp = TreeSpecies.Jacaranda; break;
                    case 1: sp = TreeSpecies.Bottlebrush; break;
                    case 2: sp = TreeSpecies.Camphor; break;
                    case 3: sp = TreeSpecies.SilkyOak; break;
                    case 4: sp = TreeSpecies.Palm; break;
                    case 5: sp = TreeSpecies.Pipal; break;
                    default: sp = TreeSpecies.Broadleaf; break;
                }
                float h, w;
                PlacementContext.Size01(sp, ref rng, out h, out w, 0.85f);
                if (!c.Clear(x, z, 3.0, PlacementContext.RoadMarginFor(sp, h, w))) continue;
                c.Add(sp, x, z, h, w, rng.Range(0f, 360f), TreeOrigin.Park);
            }
        }

        /// <summary>A formal flower garden: beds on a ring round a centre point (turned tangent to it), roses between.</summary>
        private static void Beds(PlacementContext c, AreaRecord a, double area, ref FloraRng rng)
        {
            int beds = (int)Math.Min(16, area / 500);
            if (beds < 1) return;
            double cx, cz;
            RandomPoint(a, ref rng, out cx, out cz);
            double ring = 3.5 + 0.6 * beds;
            double a0 = rng.Range(0f, 6.283f);
            for (int i = 0; i < beds; i++)
            {
                double ang = a0 + i * 2 * Math.PI / beds;
                double x = cx + Math.Cos(ang) * ring, z = cz + Math.Sin(ang) * ring;
                float h, w;
                PlacementContext.Size01(TreeSpecies.MarigoldBed, ref rng, out h, out w);
                if (!Inside(a, x, z) || !c.Clear(x, z, 0.5 * w, 0.6 * w)) continue;
                // The bed's long axis (mesh X) along the ring's tangent: yaw = atan2(-dz, dx) of the tangent.
                double tx = -Math.Sin(ang), tz = Math.Cos(ang);
                c.Add(TreeSpecies.MarigoldBed, x, z, h, w, (float)(Math.Atan2(-tz, tx) * 180 / Math.PI), TreeOrigin.Park);
                double rx = cx + Math.Cos(ang + Math.PI / beds) * (ring + 1.2), rz = cz + Math.Sin(ang + Math.PI / beds) * (ring + 1.2);
                PlacementContext.Size01(TreeSpecies.Rose, ref rng, out h, out w);
                if (Inside(a, rx, rz) && c.Clear(rx, rz, 0.5 * w, 0.6 * w)) c.Add(TreeSpecies.Rose, rx, rz, h, w, rng.Range(0f, 360f), TreeOrigin.Park);
            }
        }

        /// <summary>Clipped hedges along about half of the park's boundary edges, 1.2 m inside, every 3.1 m.</summary>
        private static void Hedges(PlacementContext c, AreaRecord a, ref FloraRng rng)
        {
            int[] idx = a.Indices;
            int[] v = a.Vertices;
            var uses = new System.Collections.Generic.Dictionary<long, int>();
            for (int t = 0; t + 2 < idx.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    long key = EdgeKey(idx[t + e], idx[t + (e + 1) % 3]);
                    int u;
                    uses.TryGetValue(key, out u);
                    uses[key] = u + 1;
                }
            for (int t = 0; t + 2 < idx.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int i0 = idx[t + e], i1 = idx[t + (e + 1) % 3];
                    if (uses[EdgeKey(i0, i1)] != 1) continue;
                    var er = new FloraRng(FloraRng.Mix((uint)Math.Min(i0, i1), (uint)Math.Max(i0, i1)) ^ (uint)a.OsmRef, Purpose + 1);
                    if (!er.Chance(0.5f)) continue;
                    double ax = v[2 * i0] / 100.0, az = v[2 * i0 + 1] / 100.0, bx = v[2 * i1] / 100.0, bz = v[2 * i1 + 1] / 100.0;
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    if (len < 4) continue;
                    double ux = (bx - ax) / len, uz = (bz - az) / len;
                    // Counter-clockwise triangles: the interior is on the left of each edge.
                    double nx = -uz, nz = ux;
                    float yaw = (float)(Math.Atan2(-uz, ux) * 180 / Math.PI);
                    for (double s = 1.6; s + 1.5 < len; s += 3.1)
                    {
                        double x = ax + ux * s + nx * 1.2, z = az + uz * s + nz * 1.2;
                        float h, w;
                        PlacementContext.Size01(TreeSpecies.Hedge, ref rng, out h, out w);
                        w = 3.0f;
                        if (!c.Clear(x, z, 0.5, 0.8) || !FootprintClear(c, x, z, ux, uz)) continue;
                        if (!c.Add(TreeSpecies.Hedge, x, z, h, w, yaw, TreeOrigin.Park, 0, 0, false, 0.4f)) return;
                    }
                }
        }

        /// <summary>
        /// Hedges lining the footpaths through a park: on two paths in three (by way id), both sides, segments every
        /// 3.1 m turned along the path, their near face <see cref="HedgeGap"/> outside the path's rideable corridor
        /// (half of <see cref="RoadClearance.MinCorridorM"/>, or the drawn half width when wider; with a corridor query
        /// the corridor itself), skipping spots outside the park, on buildings or water, or where either end of the
        /// segment would reach into the corridor of any way (path crossings and the park's roads stay open).
        /// </summary>
        private static void PathHedges(PlacementContext c, AreaRecord a)
        {
            var roads = c.T.Roads;
            for (int r = 0; r < roads.Count && !c.PlantsFull; r++)
            {
                RoadRecord road = roads[r];
                RoadClass rc = road.RoadClass;
                if (rc != RoadClass.Footway && rc != RoadClass.Path && rc != RoadClass.Pedestrian && rc != RoadClass.Cycleway) continue;
                if (road.Points == null || road.PointCount < 2) continue;
                var rr = new FloraRng((uint)(road.OsmWayId ^ (road.OsmWayId >> 32)), Purpose + 2);
                if (!rr.Chance(0.67f)) continue;
                double off = CorridorHalf(road) + HedgeHalfDepth + HedgeGap;
                int[] p = road.Points;
                for (int k = 0; k + 1 < road.PointCount; k++)
                {
                    double ax = p[2 * k] / 100.0, az = p[2 * k + 1] / 100.0, bx = p[2 * k + 2] / 100.0, bz = p[2 * k + 3] / 100.0;
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    if (len < 4.0 || !Inside(a, 0.5 * (ax + bx), 0.5 * (az + bz))) continue;
                    double ux = (bx - ax) / len, uz = (bz - az) / len;
                    float yaw = (float)(Math.Atan2(-uz, ux) * 180 / Math.PI);
                    for (int side = -1; side <= 1; side += 2)
                        for (double s = 2.0; s + 1.5 < len; s += 3.1)
                        {
                            double x = ax + ux * s - uz * side * off, z = az + uz * s + ux * side * off;
                            if (!Inside(a, x, z) || !c.ClearBesideWay(x, z, 0.5, HedgeHalfDepth + HedgeGap)) continue;
                            if (!FootprintClear(c, x, z, ux, uz)) continue;
                            if (c.Corridor == null && NearOtherWay(c.T, r, x, z, ux, uz)) continue;
                            float h, w;
                            PlacementContext.Size01(TreeSpecies.Hedge, ref rr, out h, out w);
                            if (!c.Add(TreeSpecies.Hedge, x, z, h, 3.0f, yaw, TreeOrigin.Park, 0, 0, false, 0.4f)) return;
                        }
                }
            }
        }

        /// <summary>Half the rideable corridor of a way (docs/W2_DETAIL_CONTRACT.md §1.1): half its drawn width (OSM
        /// width or the class default), at least half of <see cref="RoadClearance.MinCorridorM"/>.</summary>
        internal static double CorridorHalf(RoadRecord road)
        {
            double half = 0.5 * (road.WidthCm > 0 ? road.WidthCm / 100.0 : Meshing.RoadStyle.DefaultWidthM(road.RoadClass));
            return Math.Max(half, 0.5 * RoadClearance.MinCorridorM);
        }

        /// <summary>True when a hedge segment centred on (x, z) and turned along (ux, uz) keeps its whole footprint
        /// (the four corners and the end middles) <see cref="HedgeGap"/> outside every road corridor of the query;
        /// true without one.</summary>
        internal static bool FootprintClear(PlacementContext c, double x, double z, double ux, double uz)
        {
            if (c.Corridor == null) return true;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j += 1)
                {
                    if (i == 0 && j == 0) continue;
                    double px = x + ux * HedgeHalfLength * i - uz * HedgeHalfDepth * j, pz = z + uz * HedgeHalfLength * i + ux * HedgeHalfDepth * j;
                    if (c.Corridor.SignedDistance(px, pz) < HedgeGap) return false;
                }
            return true;
        }

        /// <summary>True when any part of a hedge segment centred on (x, z), turned along (ux, uz), lies within
        /// <see cref="HedgeGap"/> of the rideable corridor of any way but <paramref name="skip"/> (both ends and the
        /// middle of its centre line, widened by its half depth).</summary>
        private static bool NearOtherWay(TileData t, int skip, double x, double z, double ux, double uz)
        {
            for (int e = -1; e <= 1; e++)
            {
                double px = x + ux * HedgeHalfLength * e, pz = z + uz * HedgeHalfLength * e;
                if (NearOtherWay(t, skip, px, pz, HedgeHalfDepth + HedgeGap)) return true;
            }
            return false;
        }

        /// <summary>True when (x, z) lies within the corridor half width plus <paramref name="clear"/> of any way but
        /// <paramref name="skip"/>.</summary>
        private static bool NearOtherWay(TileData t, int skip, double x, double z, double clear)
        {
            for (int r = 0; r < t.Roads.Count; r++)
            {
                if (r == skip) continue;
                RoadRecord road = t.Roads[r];
                if (road.Points == null) continue;
                double reach = CorridorHalf(road) + clear;
                int[] p = road.Points;
                for (int k = 0; k + 1 < road.PointCount; k++)
                {
                    double ax = p[2 * k] / 100.0, az = p[2 * k + 1] / 100.0, bx = p[2 * k + 2] / 100.0, bz = p[2 * k + 3] / 100.0;
                    // Cheap reject on the segment's box.
                    if (x < Math.Min(ax, bx) - reach || x > Math.Max(ax, bx) + reach || z < Math.Min(az, bz) - reach || z > Math.Max(az, bz) + reach) continue;
                    double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                    double f = l2 > 1e-9 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
                    f = f < 0 ? 0 : f > 1 ? 1 : f;
                    double ex = ax + dx * f - x, ez = az + dz * f - z;
                    if (ex * ex + ez * ez < reach * reach) return true;
                }
            }
            return false;
        }

        private static long EdgeKey(int a, int b)
        {
            return a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a;
        }

        /// <summary>Scatter <paramref name="n"/> plants of a kind inside a polygon.</summary>
        internal static void Scatter(PlacementContext c, AreaRecord a, TreeSpecies sp, int n, ref FloraRng rng, TreeOrigin origin)
        {
            for (int i = 0; i < n && !c.PlantsFull; i++)
            {
                double x, z;
                RandomPoint(a, ref rng, out x, out z);
                float h, w;
                PlacementContext.Size01(sp, ref rng, out h, out w);
                if (!c.Clear(x, z, 0.45 * w, PlacementContext.RoadMarginFor(sp, h, w))) continue;
                c.Add(sp, x, z, h, w, rng.Range(0f, 360f), origin);
            }
        }

        internal static double Area(AreaRecord a)
        {
            double twice = 0;
            for (int k = 0; k + 2 < a.Indices.Length; k += 3) twice += Math.Abs(TriArea2(a, k));
            return twice * 0.5 / 10000.0;
        }

        private static double TriArea2(AreaRecord a, int k)
        {
            int i0 = a.Indices[k], i1 = a.Indices[k + 1], i2 = a.Indices[k + 2];
            double x0 = a.Vertices[2 * i0], z0 = a.Vertices[2 * i0 + 1];
            return (a.Vertices[2 * i1] - x0) * (a.Vertices[2 * i2 + 1] - z0) - (a.Vertices[2 * i1 + 1] - z0) * (a.Vertices[2 * i2] - x0);
        }

        /// <summary>A uniform random point (metres) inside an area's triangles.</summary>
        internal static void RandomPoint(AreaRecord a, ref FloraRng rng, out double x, out double z)
        {
            double total = 0;
            for (int k = 0; k + 2 < a.Indices.Length; k += 3) total += Math.Abs(TriArea2(a, k));
            double pick = rng.Next() * total;
            int tri = 0;
            for (int k = 0; k + 2 < a.Indices.Length; k += 3)
            {
                pick -= Math.Abs(TriArea2(a, k));
                tri = k;
                if (pick <= 0) break;
            }
            double u = rng.Next(), w = rng.Next();
            if (u + w > 1)
            {
                u = 1 - u;
                w = 1 - w;
            }
            int i0 = a.Indices[tri], i1 = a.Indices[tri + 1], i2 = a.Indices[tri + 2];
            double x0 = a.Vertices[2 * i0], z0 = a.Vertices[2 * i0 + 1];
            x = (x0 + (a.Vertices[2 * i1] - x0) * u + (a.Vertices[2 * i2] - x0) * w) / 100.0;
            z = (z0 + (a.Vertices[2 * i1 + 1] - z0) * u + (a.Vertices[2 * i2 + 1] - z0) * w) / 100.0;
        }

        internal static bool Inside(AreaRecord a, double x, double z)
        {
            double px = x * 100, pz = z * 100;
            for (int k = 0; k + 2 < a.Indices.Length; k += 3)
            {
                int i0 = a.Indices[k], i1 = a.Indices[k + 1], i2 = a.Indices[k + 2];
                double ax = a.Vertices[2 * i0], az = a.Vertices[2 * i0 + 1], bx = a.Vertices[2 * i1], bz = a.Vertices[2 * i1 + 1];
                double cx = a.Vertices[2 * i2], cz = a.Vertices[2 * i2 + 1];
                double d1 = (bx - ax) * (pz - az) - (bz - az) * (px - ax), d2 = (cx - bx) * (pz - bz) - (cz - bz) * (px - bx), d3 = (ax - cx) * (pz - cz) - (az - cz) * (px - cx);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(neg && pos)) return true;
            }
            return false;
        }
    }
}
