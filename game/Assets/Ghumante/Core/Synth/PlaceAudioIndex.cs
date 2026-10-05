using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Synth
{
    /// <summary>Place overlays at a point (W2_DESIGN 7.2 "Polygons override"): nearness 0..1 to the Ring Road or an
    /// arterial, a park and water. The aerodrome comes from <see cref="PlaceAudioIndex.AirportNearness"/>.</summary>
    public struct PlaceSample
    {
        public float RingRoad01, Park01, Water01;
    }

    /// <summary>
    /// What the ambience and the occlusion probe need from the resident detail tiles (W2_DESIGN 7.1 "Occlusion",
    /// 7.2 zoning): arterial roads (motorway, trunk, primary: the Ring Road and the radial arterials), park and
    /// water polygons, rivers and canals, and the building footprints bucketed in a 32 m grid per tile for
    /// segment tests. The world has no physics colliders, so occlusion is "does the listener-to-source segment
    /// cross a building footprint below the roof", evaluated on game data.
    /// <para>
    /// Coordinates are game metres (X east, Z north). <see cref="AddTile"/> allocates once per tile; the queries
    /// do not allocate. Not thread-safe: one owner (the main thread).
    /// </para>
    /// </summary>
    public sealed class PlaceAudioIndex
    {
        /// <summary>Building bucket size.</summary>
        public const double CellM = 32.0;

        /// <summary>Ring Road / arterial roar: full within this distance of the centreline, silent beyond
        /// <see cref="RingZeroM"/>.</summary>
        public const float RingFullM = 25f, RingZeroM = 150f;

        /// <summary>Park: full inside, fading to 0 this far outside.</summary>
        public const float ParkFadeM = 40f;

        /// <summary>Water: full inside or within <see cref="WaterFullM"/>, silent beyond <see cref="WaterZeroM"/>.</summary>
        public const float WaterFullM = 15f, WaterZeroM = 120f;

        /// <summary>Aerodrome: full within this distance of the runway centreline (the TIA field half-width), then a
        /// linear fade over <see cref="AirportFadeM"/> (W2_DESIGN 7.2 "aerodrome + 3 km").</summary>
        public const float AirportFullM = 700f, AirportFadeM = 3000f;

        /// <summary>Galli slapback: a lane narrower than this (game width) with a building wall within
        /// <see cref="GalliWallM"/> of the lane edge on both sides.</summary>
        public const float GalliMaxWidthM = 8f, GalliWallM = 3.5f;

        private sealed class Poly
        {
            public AreaRecord Area;
            public double MinX, MinZ, MaxX, MaxZ; // tile-local metres
        }

        private sealed class Tile
        {
            public TileId Id;
            public TileData Data;
            public double X0, Z0, Size;
            public double[] Arterials = new double[0]; // tile-local ax, az, bx, bz per segment
            public double[] Rivers = new double[0];
            public Poly[] Parks = new Poly[0];
            public Poly[] Waters = new Poly[0];
            public int N;
            public double Cell;
            public int[] CellStart; // CSR: buildings per cell
            public int[] CellItems;
            public float[] Height;  // per building, metres; 0 = skipped
            public float[] Box;     // per building minX, minZ, maxX, maxZ (tile-local)
            public int[] Stamp;
        }

        private readonly Dictionary<TileId, Tile> _byId = new Dictionary<TileId, Tile>();
        private readonly List<Tile> _tiles = new List<Tile>();
        private int _stamp;

        public int TileCount
        {
            get { return _tiles.Count; }
        }

        /// <summary>Adds (or replaces) a detail tile.</summary>
        public void AddTile(TileId id, TileData t)
        {
            if (t == null) return;
            RemoveTile(id);
            Tile e = Build(id, t);
            _byId.Add(id, e);
            _tiles.Add(e);
        }

        public void RemoveTile(TileId id)
        {
            Tile e;
            if (!_byId.TryGetValue(id, out e)) return;
            _byId.Remove(id);
            _tiles.Remove(e);
        }

        public void Clear()
        {
            _byId.Clear();
            _tiles.Clear();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Building

        private static bool IsArterial(RoadClass c)
        {
            return c == RoadClass.Motorway || c == RoadClass.Trunk || c == RoadClass.Primary;
        }

        private static bool IsPark(AreaKind k)
        {
            return k == AreaKind.Park || k == AreaKind.Pitch;
        }

        private static bool IsWater(AreaKind k)
        {
            return k == AreaKind.WaterRiver || k == AreaKind.WaterLake || k == AreaKind.WaterPond;
        }

        private static Tile Build(TileId id, TileData t)
        {
            var e = new Tile { Id = id, Data = t, X0 = id.X0, Z0 = id.Z0, Size = id.Size };

            var segs = new List<double>();
            for (int r = 0; r < t.Roads.Count; r++)
            {
                RoadRecord rec = t.Roads[r];
                if (rec == null || rec.Points == null || !IsArterial(rec.RoadClass)) continue;
                AddPolyline(segs, rec.Points);
            }
            e.Arterials = segs.ToArray();
            segs.Clear();
            for (int l = 0; l < t.Lines.Count; l++)
            {
                LineRecord rec = t.Lines[l];
                if (rec == null || rec.Points == null) continue;
                if (rec.Kind != LineKind.River && rec.Kind != LineKind.Canal && rec.Kind != LineKind.Stream) continue;
                AddPolyline(segs, rec.Points);
            }
            e.Rivers = segs.ToArray();

            var parks = new List<Poly>();
            var waters = new List<Poly>();
            for (int a = 0; a < t.Areas.Count; a++)
            {
                AreaRecord rec = t.Areas[a];
                if (rec == null || rec.Vertices == null || rec.Vertices.Length < 6) continue;
                bool park = IsPark(rec.Kind), water = IsWater(rec.Kind);
                if (!park && !water) continue;
                var p = new Poly { Area = rec, MinX = double.MaxValue, MinZ = double.MaxValue, MaxX = double.MinValue, MaxZ = double.MinValue };
                for (int v = 0; v + 1 < rec.Vertices.Length; v += 2)
                {
                    double x = rec.Vertices[v] / 100.0, z = rec.Vertices[v + 1] / 100.0;
                    if (x < p.MinX) p.MinX = x;
                    if (x > p.MaxX) p.MaxX = x;
                    if (z < p.MinZ) p.MinZ = z;
                    if (z > p.MaxZ) p.MaxZ = z;
                }
                (park ? parks : waters).Add(p);
            }
            e.Parks = parks.ToArray();
            e.Waters = waters.ToArray();

            // Buildings: bounding boxes bucketed in a CSR grid.
            int nb = t.Buildings.Count;
            e.N = Math.Max(1, (int)Math.Ceiling(e.Size / CellM));
            if (e.N > 256) e.N = 256;
            e.Cell = e.Size / e.N;
            e.Height = new float[nb];
            e.Box = new float[nb * 4];
            e.Stamp = new int[nb];
            var counts = new int[e.N * e.N + 1];
            for (int b = 0; b < nb; b++)
            {
                BuildingRecord rec = t.Buildings[b];
                if (rec == null || rec.Rings == null || rec.Rings.Length == 0 || rec.Rings[0] == null || rec.Rings[0].Length < 6) continue;
                int[] ring = rec.Rings[0];
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                for (int v = 0; v + 1 < ring.Length; v += 2)
                {
                    float x = ring[v] / 100f, z = ring[v + 1] / 100f;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (z < minZ) minZ = z;
                    if (z > maxZ) maxZ = z;
                }
                e.Box[4 * b] = minX;
                e.Box[4 * b + 1] = minZ;
                e.Box[4 * b + 2] = maxX;
                e.Box[4 * b + 3] = maxZ;
                e.Height[b] = BuildingStyle.HeightM(rec);
                int cx0 = CellOf(e, minX), cx1 = CellOf(e, maxX), cz0 = CellOf(e, minZ), cz1 = CellOf(e, maxZ);
                for (int cz = cz0; cz <= cz1; cz++)
                for (int cx = cx0; cx <= cx1; cx++)
                    counts[cz * e.N + cx + 1]++;
            }
            for (int c = 1; c < counts.Length; c++) counts[c] += counts[c - 1];
            e.CellStart = counts;
            e.CellItems = new int[counts[counts.Length - 1]];
            var fill = new int[e.N * e.N];
            for (int b = 0; b < nb; b++)
            {
                if (!(e.Height[b] > 0f)) continue;
                int cx0 = CellOf(e, e.Box[4 * b]), cx1 = CellOf(e, e.Box[4 * b + 2]);
                int cz0 = CellOf(e, e.Box[4 * b + 1]), cz1 = CellOf(e, e.Box[4 * b + 3]);
                for (int cz = cz0; cz <= cz1; cz++)
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    int c = cz * e.N + cx;
                    e.CellItems[e.CellStart[c] + fill[c]++] = b;
                }
            }
            return e;
        }

        private static void AddPolyline(List<double> segs, int[] p)
        {
            for (int k = 0; k + 3 < p.Length; k += 2)
            {
                segs.Add(p[k] / 100.0);
                segs.Add(p[k + 1] / 100.0);
                segs.Add(p[k + 2] / 100.0);
                segs.Add(p[k + 3] / 100.0);
            }
        }

        private static int CellOf(Tile e, double v)
        {
            int c = (int)Math.Floor(v / e.Cell);
            return c < 0 ? 0 : c >= e.N ? e.N - 1 : c;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Place overlays

        /// <summary>Linear ramp: 1 at or below <paramref name="full"/>, 0 at or beyond <paramref name="zero"/>.</summary>
        public static float Ramp(double d, float full, float zero)
        {
            if (double.IsNaN(d)) return 0f;
            if (d <= full) return 1f;
            if (d >= zero) return 0f;
            return (float)((zero - d) / (zero - full));
        }

        /// <summary>Nearness 0..1 to the aerodrome: the distance from (x, z) to the runway centreline segment A–B,
        /// full within <see cref="AirportFullM"/>, 0 at <see cref="AirportFullM"/> + <see cref="AirportFadeM"/>.</summary>
        public static float AirportNearness(double x, double z, double ax, double az, double bx, double bz)
        {
            double d = Math.Sqrt(SegDist2(x, z, ax, az, bx, bz));
            return Ramp(d, AirportFullM, AirportFullM + AirportFadeM);
        }

        /// <summary>Ring Road, park and water nearness at (x, z) over the resident tiles.</summary>
        public PlaceSample Sample(double x, double z)
        {
            double ring = double.PositiveInfinity, river = double.PositiveInfinity;
            float park = 0f, water = 0f;
            for (int i = 0; i < _tiles.Count; i++)
            {
                Tile e = _tiles[i];
                double px = x - e.X0, pz = z - e.Z0;
                double reach = RingZeroM;
                if (px < -reach || pz < -reach || px > e.Size + reach || pz > e.Size + reach) continue;
                ring = Math.Min(ring, NearestSeg(e.Arterials, px, pz, RingZeroM));
                river = Math.Min(river, NearestSeg(e.Rivers, px, pz, WaterZeroM));
                park = Math.Max(park, PolyNearness(e.Parks, px, pz, 0f, ParkFadeM));
                water = Math.Max(water, PolyNearness(e.Waters, px, pz, WaterFullM, WaterZeroM));
            }
            water = Math.Max(water, Ramp(river, WaterFullM, WaterZeroM));
            return new PlaceSample { RingRoad01 = Ramp(ring, RingFullM, RingZeroM), Park01 = park, Water01 = water };
        }

        private static double NearestSeg(double[] s, double px, double pz, double maxD)
        {
            double best = double.PositiveInfinity;
            for (int k = 0; k + 3 < s.Length; k += 4)
            {
                double ax = s[k], az = s[k + 1], bx = s[k + 2], bz = s[k + 3];
                if (px < Math.Min(ax, bx) - maxD || px > Math.Max(ax, bx) + maxD || pz < Math.Min(az, bz) - maxD ||
                    pz > Math.Max(az, bz) + maxD) continue;
                double d2 = SegDist2(px, pz, ax, az, bx, bz);
                if (d2 < best) best = d2;
            }
            return Math.Sqrt(best);
        }

        private static float PolyNearness(Poly[] polys, double px, double pz, float full, float zero)
        {
            float best = 0f;
            for (int i = 0; i < polys.Length && best < 1f; i++)
            {
                Poly p = polys[i];
                if (px < p.MinX - zero || px > p.MaxX + zero || pz < p.MinZ - zero || pz > p.MaxZ + zero) continue;
                AreaRecord a = p.Area;
                if (px >= p.MinX && px <= p.MaxX && pz >= p.MinZ && pz <= p.MaxZ && InsideTriangles(a, px, pz)) return 1f;
                float v = Ramp(Math.Sqrt(RingDist2(a, px, pz)), full, zero);
                if (v > best) best = v;
            }
            return best;
        }

        private static bool InsideTriangles(AreaRecord a, double px, double pz)
        {
            int[] idx = a.Indices;
            int[] v = a.Vertices;
            if (idx == null) return false;
            for (int t = 0; t + 2 < idx.Length; t += 3)
            {
                double x0 = v[2 * idx[t]] / 100.0, z0 = v[2 * idx[t] + 1] / 100.0;
                double x1 = v[2 * idx[t + 1]] / 100.0, z1 = v[2 * idx[t + 1] + 1] / 100.0;
                double x2 = v[2 * idx[t + 2]] / 100.0, z2 = v[2 * idx[t + 2] + 1] / 100.0;
                double d0 = Cross(x0, z0, x1, z1, px, pz), d1 = Cross(x1, z1, x2, z2, px, pz), d2 = Cross(x2, z2, x0, z0, px, pz);
                bool neg = d0 < 0 || d1 < 0 || d2 < 0, pos = d0 > 0 || d1 > 0 || d2 > 0;
                if (!(neg && pos)) return true;
            }
            return false;
        }

        private static double RingDist2(AreaRecord a, double px, double pz)
        {
            double best = double.PositiveInfinity;
            int[] v = a.Vertices;
            int[] rings = a.Rings;
            if (rings == null || rings.Length < 2) return best;
            for (int r = 0; r + 1 < rings.Length; r += 2)
            {
                int s = rings[r], n = rings[r + 1];
                for (int k = 0; k < n; k++)
                {
                    int i0 = s + k, i1 = s + (k + 1) % n;
                    double d2 = SegDist2(px, pz, v[2 * i0] / 100.0, v[2 * i0 + 1] / 100.0, v[2 * i1] / 100.0, v[2 * i1 + 1] / 100.0);
                    if (d2 < best) best = d2;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Buildings: occlusion and galli walls

        /// <summary>
        /// True when the segment from (x0, y0, z0) to (x1, y1, z1) (game metres) crosses the footprint edge of a
        /// building whose roof is above the segment there. The ground under the crossing is taken as
        /// <c>min(y0, y1) − 1 m</c> (sources and listener sit within a couple of metres of the street), so a bird
        /// above the roofs or a bell on a tower top is heard over a low house but not through a tall one.
        /// </summary>
        public bool Occluded(double x0, float y0, double z0, double x1, float y1, double z1)
        {
            if (double.IsNaN(x0) || double.IsNaN(z0) || double.IsNaN(x1) || double.IsNaN(z1)) return false;
            float ground = Math.Min(y0, y1) - 1f;
            _stamp++;
            if (_stamp == int.MaxValue) ResetStamps();
            for (int i = 0; i < _tiles.Count; i++)
            {
                Tile e = _tiles[i];
                double ax = x0 - e.X0, az = z0 - e.Z0, bx = x1 - e.X0, bz = z1 - e.Z0;
                double minX = Math.Min(ax, bx), maxX = Math.Max(ax, bx), minZ = Math.Min(az, bz), maxZ = Math.Max(az, bz);
                if (maxX < 0 || maxZ < 0 || minX > e.Size || minZ > e.Size) continue;
                int cx0 = CellOf(e, minX), cx1 = CellOf(e, maxX), cz0 = CellOf(e, minZ), cz1 = CellOf(e, maxZ);
                for (int cz = cz0; cz <= cz1; cz++)
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    int c = cz * e.N + cx;
                    for (int k = e.CellStart[c], end = e.CellStart[c + 1]; k < end; k++)
                    {
                        int b = e.CellItems[k];
                        if (e.Stamp[b] == _stamp) continue;
                        e.Stamp[b] = _stamp;
                        if (e.Box[4 * b] > maxX || e.Box[4 * b + 2] < minX || e.Box[4 * b + 1] > maxZ || e.Box[4 * b + 3] < minZ) continue;
                        double t;
                        if (!CrossesRing(e.Data.Buildings[b].Rings[0], ax, az, bx, bz, out t)) continue;
                        float y = y0 + (float)t * (y1 - y0);
                        if (y < ground + e.Height[b]) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// The galli width at a road point: <paramref name="halfWidthM"/> × 2 when that is under
        /// <see cref="GalliMaxWidthM"/> and building footprints stand within <see cref="GalliWallM"/> of the lane
        /// edge on both sides (left and right of the direction <paramref name="dirX"/>, <paramref name="dirZ"/>);
        /// else 0 (open street, no slapback).
        /// </summary>
        public float GalliWidth(double x, double z, float dirX, float dirZ, float halfWidthM)
        {
            float w = 2f * halfWidthM;
            if (!(w > 0.5f) || w >= GalliMaxWidthM) return 0f;
            double len = Math.Sqrt(dirX * (double)dirX + dirZ * (double)dirZ);
            if (!(len > 1e-6)) return 0f;
            double nx = -dirZ / len, nz = dirX / len;
            double reach = halfWidthM + GalliWallM;
            bool left = Occluded(x, 0f, z, x + nx * reach, 0f, z + nz * reach) || InsideBuilding(x + nx * reach, z + nz * reach);
            if (!left) return 0f;
            bool right = Occluded(x, 0f, z, x - nx * reach, 0f, z - nz * reach) || InsideBuilding(x - nx * reach, z - nz * reach);
            return right ? w : 0f;
        }

        /// <summary>True when (x, z) is inside a building footprint (outer ring).</summary>
        public bool InsideBuilding(double x, double z)
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                Tile e = _tiles[i];
                double px = x - e.X0, pz = z - e.Z0;
                if (px < 0 || pz < 0 || px > e.Size || pz > e.Size) continue;
                int c = CellOf(e, pz) * e.N + CellOf(e, px);
                for (int k = e.CellStart[c], end = e.CellStart[c + 1]; k < end; k++)
                {
                    int b = e.CellItems[k];
                    if (px < e.Box[4 * b] || px > e.Box[4 * b + 2] || pz < e.Box[4 * b + 1] || pz > e.Box[4 * b + 3]) continue;
                    if (InsideRing(e.Data.Buildings[b].Rings[0], px, pz)) return true;
                }
            }
            return false;
        }

        private void ResetStamps()
        {
            _stamp = 1;
            for (int i = 0; i < _tiles.Count; i++) Array.Clear(_tiles[i].Stamp, 0, _tiles[i].Stamp.Length);
        }

        private static bool CrossesRing(int[] ring, double ax, double az, double bx, double bz, out double tHit)
        {
            tHit = 0;
            int n = ring.Length / 2;
            double best = double.PositiveInfinity;
            for (int k = 0; k < n; k++)
            {
                int j = (k + 1) % n;
                double cx = ring[2 * k] / 100.0, cz = ring[2 * k + 1] / 100.0;
                double dx = ring[2 * j] / 100.0, dz = ring[2 * j + 1] / 100.0;
                double t;
                if (SegSeg(ax, az, bx, bz, cx, cz, dx, dz, out t) && t < best) best = t;
            }
            if (double.IsPositiveInfinity(best)) return false;
            tHit = best;
            return true;
        }

        private static bool InsideRing(int[] ring, double px, double pz)
        {
            int n = ring.Length / 2;
            bool inside = false;
            for (int k = 0, j = n - 1; k < n; j = k++)
            {
                double xk = ring[2 * k] / 100.0, zk = ring[2 * k + 1] / 100.0;
                double xj = ring[2 * j] / 100.0, zj = ring[2 * j + 1] / 100.0;
                if ((zk > pz) != (zj > pz) && px < (xj - xk) * (pz - zk) / (zj - zk) + xk) inside = !inside;
            }
            return inside;
        }

        /// <summary>Proper or touching intersection of AB and CD; <paramref name="t"/> is the parameter along AB.</summary>
        private static bool SegSeg(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz, out double t)
        {
            t = 0;
            double rx = bx - ax, rz = bz - az, sx = dx - cx, sz = dz - cz;
            double den = rx * sz - rz * sx;
            if (Math.Abs(den) < 1e-12) return false;
            double qx = cx - ax, qz = cz - az;
            t = (qx * sz - qz * sx) / den;
            double u = (qx * rz - qz * rx) / den;
            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }

        private static double Cross(double ax, double az, double bx, double bz, double px, double pz)
        {
            return (bx - ax) * (pz - az) - (bz - az) * (px - ax);
        }

        private static double SegDist2(double px, double pz, double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az;
            double len2 = dx * dx + dz * dz;
            double t = len2 > 1e-12 ? ((px - ax) * dx + (pz - az) * dz) / len2 : 0;
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            double ex = ax + t * dx - px, ez = az + t * dz - pz;
            return ex * ex + ez * ez;
        }
    }
}
