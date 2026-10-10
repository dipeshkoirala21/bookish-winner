using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>What lies at a point of the ground, for placing animals (flags; several can hold at once).</summary>
    [Flags]
    public enum FaunaGround : byte
    {
        /// <summary>Nothing known: no detail tile covers the point yet.</summary>
        None = 0,

        /// <summary>A detail tile covers the point (the other flags are meaningful).</summary>
        Loaded = 1,

        /// <summary>Inside a building footprint or a hero monument (stupa, temple plinth, palace).</summary>
        Building = 2,

        /// <summary>Inside the clear corridor of a carriageway that motor vehicles use (at least
        /// <see cref="RoadClearance.MinCorridorM"/> wide).</summary>
        Road = 4,

        /// <summary>On a footway, path, steps or a pedestrian street (people only).</summary>
        Footway = 8,

        /// <summary>A pond, lake, river or wetland.</summary>
        Water = 16,

        /// <summary>Farmland, paddy, meadow, grassland, orchard or a park lawn.</summary>
        Field = 32,

        /// <summary>An open square: pedestrian area, marketplace, courtyard, religious compound or heritage zone.</summary>
        Square = 64,
    }

    /// <summary>
    /// The stage-1 hero monuments as no-go rectangles for animals (their plan, turned to their door yaw, from
    /// <see cref="HeroCatalog.S1"/>), plus the compound walls the fauna must stay outside of (Boudhanath's kora wall
    /// w56688296, 95.8 × 95.5 m; temples.md 4.1). Engine-free, built once.
    /// </summary>
    public static class FaunaHeroZones
    {
        private struct Zone
        {
            public string Name;
            public double X, Z, HalfW, HalfD, Cos, Sin;
        }

        private static readonly Zone[] Zones = Build();

        /// <summary>Number of zones.</summary>
        public static int Count
        {
            get { return Zones.Length; }
        }

        /// <summary>Name of zone <paramref name="i"/>.</summary>
        public static string NameOf(int i)
        {
            return Zones[i].Name;
        }

        /// <summary>Centre of zone <paramref name="i"/> (game metres) and its half sizes across and along its door yaw.</summary>
        public static void Extent(int i, out double x, out double z, out double halfW, out double halfD)
        {
            Zone q = Zones[i];
            x = q.X;
            z = q.Z;
            halfW = q.HalfW;
            halfD = q.HalfD;
        }

        /// <summary>Index of the first zone that contains (x, z) grown by <paramref name="marginM"/>, or −1.</summary>
        public static int ZoneAt(double x, double z, double marginM = 0.0)
        {
            for (int i = 0; i < Zones.Length; i++)
            {
                Zone q = Zones[i];
                double dx = x - q.X, dz = z - q.Z;
                if (Math.Abs(dx) > q.HalfW + q.HalfD + marginM || Math.Abs(dz) > q.HalfW + q.HalfD + marginM) continue;
                // Local axes: across the front (w) and along the door yaw (d).
                double u = dx * q.Cos - dz * q.Sin, v = dx * q.Sin + dz * q.Cos;
                if (Math.Abs(u) <= q.HalfW + marginM && Math.Abs(v) <= q.HalfD + marginM) return i;
            }
            return -1;
        }

        /// <summary>True when (x, z) lies inside a hero monument (grown by <paramref name="marginM"/>).</summary>
        public static bool Inside(double x, double z, double marginM = 0.0)
        {
            return ZoneAt(x, z, marginM) >= 0;
        }

        private static Zone[] Build()
        {
            var list = new List<Zone>();
            foreach (HeroRecipe r in HeroCatalog.S1)
            {
                if (double.IsNaN(r.Lat) || double.IsNaN(r.Lon) || !(r.PlanW > 0f) || !(r.PlanD > 0f)) continue;
                Add(list, r.Name, r.Lat, r.Lon, r.PlanW, r.PlanD, float.IsNaN(r.YawDeg) ? 0f : r.YawDeg);
            }
            // Boudhanath's kora wall with its prayer-wheel niches encloses the terraces: the plaza is outside it.
            Add(list, "Boudhanath kora wall", 27.721436, 85.362004, 95.8f, 95.5f, 0f);
            return list.ToArray();
        }

        private static void Add(List<Zone> list, string name, double lat, double lon, float w, float d, float yawDeg)
        {
            WorldFrame.LonLatToGame(lon, lat, out double x, out double z);
            double a = yawDeg * Math.PI / 180.0;
            list.Add(new Zone { Name = name, X = x, Z = z, HalfW = 0.5 * w, HalfD = 0.5 * d, Cos = Math.Cos(a), Sin = Math.Sin(a) });
        }
    }

    /// <summary>
    /// Where animals may go, from the detail tiles the player has nearby (W2_DESIGN 5.5–5.6, review of the detail
    /// pass): building footprints (outer rings), hero monuments (<see cref="FaunaHeroZones"/>), carriageway corridors
    /// (the real or class width, widened to <see cref="RoadClearance.MinCorridorM"/>; or the roads package's
    /// <see cref="IRoadCorridorQuery"/> when a factory is given), footways and pedestrian streets, water, fields and
    /// open squares. Point queries are allocation-free: each tile is bucketed in 16 m cells when it is added. Not
    /// thread safe; the wildlife presenters call it on the main thread.
    /// </summary>
    public sealed class FaunaGroundIndex
    {
        /// <summary>Bucket size (m).</summary>
        public const double CellM = 16.0;

        private sealed class TileIndex
        {
            public TileData Tile;
            public double X0, Z0, Size;
            public int N;
            public List<int>[] Cells;

            // Buildings: outer ring (metres, tile-local, interleaved) and bounds.
            public readonly List<double[]> Rings = new List<double[]>();
            public readonly List<double> RingBox = new List<double>();

            // Area triangles (tile-local metres) with their ground flag.
            public readonly List<double> Tris = new List<double>();
            public readonly List<FaunaGround> TriFlag = new List<FaunaGround>();

            // Road segments: x0, z0, x1, z1, half width; flag Road or Footway.
            public readonly List<double> Segs = new List<double>();
            public readonly List<FaunaGround> SegFlag = new List<FaunaGround>();

            public IRoadCorridorQuery Corridor;
        }

        // Cell entries are encoded: kind << 28 | index (kind 0 building, 1 triangle, 2 segment).
        private const int KindShift = 28, IndexMask = (1 << KindShift) - 1;

        private readonly Dictionary<ulong, TileIndex> _tiles = new Dictionary<ulong, TileIndex>();
        private readonly List<TileIndex> _list = new List<TileIndex>();

        /// <summary>Optional: builds the roads package's corridor query for a tile (RoadCorridorIndex.ForTile); when it
        /// answers "inside" over a carriageway the point counts as <see cref="FaunaGround.Road"/>.</summary>
        public Func<TileData, IRoadCorridorQuery> CorridorFactory;

        /// <summary>Hero monuments count as buildings (default true).</summary>
        public bool HeroZones = true;

        /// <summary>Tiles indexed.</summary>
        public int Count
        {
            get { return _list.Count; }
        }

        /// <summary>Adds (or replaces) a detail tile.</summary>
        public void Add(TileData t)
        {
            if (t == null) return;
            ulong key = t.Tile.Key;
            Remove(key);
            var ti = new TileIndex { Tile = t, X0 = t.Tile.X0, Z0 = t.Tile.Z0, Size = t.Tile.Size };
            ti.N = Math.Max(1, (int)Math.Ceiling(ti.Size / CellM));
            ti.Cells = new List<int>[ti.N * ti.N];
            IndexBuildings(ti);
            IndexAreas(ti);
            IndexRoads(ti);
            if (CorridorFactory != null)
            {
                try
                {
                    ti.Corridor = CorridorFactory(t);
                }
                catch (Exception)
                {
                    ti.Corridor = null; // the own road index still answers
                }
            }
            _tiles.Add(key, ti);
            _list.Add(ti);
        }

        /// <summary>True when the tile with key <paramref name="tileKey"/> is indexed.</summary>
        public bool Contains(ulong tileKey)
        {
            return _tiles.ContainsKey(tileKey);
        }

        /// <summary>Removes a tile by key (no-op when absent).</summary>
        public void Remove(ulong tileKey)
        {
            TileIndex ti;
            if (!_tiles.TryGetValue(tileKey, out ti)) return;
            _tiles.Remove(tileKey);
            _list.Remove(ti);
        }

        public void Clear()
        {
            _tiles.Clear();
            _list.Clear();
        }

        /// <summary>What is at game point (x, z). Finest tile first; <see cref="FaunaGround.None"/> where no tile is
        /// loaded (a hero monument still reports <see cref="FaunaGround.Building"/>).</summary>
        public FaunaGround At(double x, double z)
        {
            FaunaGround g = FaunaGround.None;
            if (HeroZones && FaunaHeroZones.Inside(x, z, 0.5)) g |= FaunaGround.Building;
            TileIndex best = null;
            for (int i = 0; i < _list.Count; i++)
            {
                TileIndex ti = _list[i];
                if (x < ti.X0 || z < ti.Z0 || x >= ti.X0 + ti.Size || z >= ti.Z0 + ti.Size) continue;
                if (best == null || ti.Size < best.Size) best = ti;
            }
            if (best == null) return g;
            return g | Probe(best, x - best.X0, z - best.Z0, x, z);
        }

        private FaunaGround Probe(TileIndex ti, double lx, double lz, double gx, double gz)
        {
            FaunaGround g = FaunaGround.Loaded;
            int c = Clamp(ti, (int)Math.Floor(lx / CellM)), r = Clamp(ti, (int)Math.Floor(lz / CellM));
            List<int> l = ti.Cells[r * ti.N + c];
            bool car = false;
            if (l != null)
            {
                for (int k = 0; k < l.Count; k++)
                {
                    int e = l[k], kind = e >> KindShift, i = e & IndexMask;
                    switch (kind)
                    {
                        case 0:
                        {
                            if ((g & FaunaGround.Building) != 0) break;
                            int b = 4 * i;
                            if (lx < ti.RingBox[b] || lz < ti.RingBox[b + 1] || lx > ti.RingBox[b + 2] || lz > ti.RingBox[b + 3]) break;
                            if (InRing(ti.Rings[i], lx, lz)) g |= FaunaGround.Building;
                            break;
                        }
                        case 1:
                        {
                            FaunaGround f = ti.TriFlag[i];
                            if ((g & f) == f) break;
                            int q = 6 * i;
                            if (InTri(ti.Tris, q, lx, lz)) g |= f;
                            break;
                        }
                        default:
                        {
                            FaunaGround f = ti.SegFlag[i];
                            int q = 5 * i;
                            double hw = ti.Segs[q + 4];
                            double d2 = SegDist2(ti.Segs[q], ti.Segs[q + 1], ti.Segs[q + 2], ti.Segs[q + 3], lx, lz);
                            if (d2 <= hw * hw) g |= f;
                            if (f == FaunaGround.Road && d2 <= (hw + 4.0) * (hw + 4.0)) car = true;
                            break;
                        }
                    }
                }
            }
            if (ti.Corridor != null && car && (g & FaunaGround.Road) == 0)
            {
                // The roads package widened the carriageway beyond our estimate.
                double sd = ti.Corridor.SignedDistance(gx, gz);
                if (sd < 0.0) g |= FaunaGround.Road;
            }
            return g;
        }

        // ------------------------------------------------------------------------------------------------------
        // Indexing

        private static void IndexBuildings(TileIndex ti)
        {
            List<BuildingRecord> bs = ti.Tile.Buildings;
            for (int i = 0; i < bs.Count; i++)
            {
                int[][] rings = bs[i].Rings;
                if (rings == null || rings.Length == 0 || rings[0] == null || rings[0].Length < 6) continue;
                int[] r = rings[0];
                var m = new double[r.Length];
                double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                for (int k = 0; k + 1 < r.Length; k += 2)
                {
                    double x = r[k] / 100.0, z = r[k + 1] / 100.0;
                    m[k] = x;
                    m[k + 1] = z;
                    x0 = Math.Min(x0, x);
                    z0 = Math.Min(z0, z);
                    x1 = Math.Max(x1, x);
                    z1 = Math.Max(z1, z);
                }
                int idx = ti.Rings.Count;
                ti.Rings.Add(m);
                ti.RingBox.Add(x0);
                ti.RingBox.Add(z0);
                ti.RingBox.Add(x1);
                ti.RingBox.Add(z1);
                Bucket(ti, x0, z0, x1, z1, (0 << KindShift) | idx);
            }
        }

        /// <summary>The ground flag of an area kind (None for kinds that do not matter to animals).</summary>
        public static FaunaGround FlagOf(AreaKind k, AreaFlags flags)
        {
            switch (k)
            {
                case AreaKind.WaterLake:
                case AreaKind.WaterRiver:
                case AreaKind.WaterPond:
                case AreaKind.Wetland:
                case AreaKind.Pool:
                    return FaunaGround.Water;
                case AreaKind.Farmland:
                case AreaKind.Orchard:
                case AreaKind.Meadow:
                case AreaKind.Grassland:
                case AreaKind.Park:
                case AreaKind.TeaGarden:
                    return FaunaGround.Field;
                case AreaKind.Pedestrian:
                case AreaKind.Marketplace:
                case AreaKind.Courtyard:
                case AreaKind.Religious:
                    return FaunaGround.Square;
                default:
                    return (flags & (AreaFlags.HeritageZone | AreaFlags.SacredNoVehicle)) != 0 ? FaunaGround.Square : FaunaGround.None;
            }
        }

        private static void IndexAreas(TileIndex ti)
        {
            List<AreaRecord> areas = ti.Tile.Areas;
            for (int a = 0; a < areas.Count; a++)
            {
                AreaRecord ar = areas[a];
                FaunaGround f = FlagOf(ar.Kind, ar.Flags);
                if (f == FaunaGround.None || ar.Indices == null || ar.Vertices == null) continue;
                for (int t = 0; t + 2 < ar.Indices.Length; t += 3)
                {
                    int i0 = ar.Indices[t], i1 = ar.Indices[t + 1], i2 = ar.Indices[t + 2];
                    if (2 * Math.Max(i0, Math.Max(i1, i2)) + 1 >= ar.Vertices.Length) continue;
                    double ax = ar.Vertices[2 * i0] / 100.0, az = ar.Vertices[2 * i0 + 1] / 100.0;
                    double bx = ar.Vertices[2 * i1] / 100.0, bz = ar.Vertices[2 * i1 + 1] / 100.0;
                    double cx = ar.Vertices[2 * i2] / 100.0, cz = ar.Vertices[2 * i2 + 1] / 100.0;
                    int idx = ti.TriFlag.Count;
                    ti.Tris.Add(ax);
                    ti.Tris.Add(az);
                    ti.Tris.Add(bx);
                    ti.Tris.Add(bz);
                    ti.Tris.Add(cx);
                    ti.Tris.Add(cz);
                    ti.TriFlag.Add(f);
                    Bucket(ti, Math.Min(ax, Math.Min(bx, cx)), Math.Min(az, Math.Min(bz, cz)), Math.Max(ax, Math.Max(bx, cx)), Math.Max(az, Math.Max(bz, cz)),
                           (1 << KindShift) | idx);
                }
            }
        }

        /// <summary>True for the road classes motor vehicles drive on (the others are footways).</summary>
        public static bool IsCarriageway(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Pedestrian:
                case RoadClass.Footway:
                case RoadClass.Path:
                case RoadClass.Steps:
                case RoadClass.Cycleway:
                case RoadClass.Bridleway:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>Real width of a road when OSM gives none (m; a conservative class default).</summary>
        public static float DefaultWidthM(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk: return 14f;
                case RoadClass.Primary: return 12f;
                case RoadClass.Secondary: return 10f;
                case RoadClass.Tertiary: return 8f;
                case RoadClass.Track: return 4f;
                case RoadClass.Pedestrian: return 6f;
                case RoadClass.Footway:
                case RoadClass.Path:
                case RoadClass.Steps:
                case RoadClass.Cycleway:
                case RoadClass.Bridleway: return 2.5f;
                default: return 6f;
            }
        }

        private static void IndexRoads(TileIndex ti)
        {
            List<RoadRecord> roads = ti.Tile.Roads;
            for (int i = 0; i < roads.Count; i++)
            {
                RoadRecord r = roads[i];
                if (r.Points == null || r.Points.Length < 4) continue;
                bool car = IsCarriageway(r.RoadClass);
                double w = r.WidthCm > 0 ? r.WidthCm / 100.0 : DefaultWidthM(r.RoadClass);
                if (car) w = Math.Max(w, RoadClearance.MinCorridorM);
                double hw = 0.5 * w;
                FaunaGround f = car ? FaunaGround.Road : FaunaGround.Footway;
                for (int k = 0; k + 3 < r.Points.Length; k += 2)
                {
                    double x0 = r.Points[k] / 100.0, z0 = r.Points[k + 1] / 100.0, x1 = r.Points[k + 2] / 100.0, z1 = r.Points[k + 3] / 100.0;
                    int idx = ti.SegFlag.Count;
                    ti.Segs.Add(x0);
                    ti.Segs.Add(z0);
                    ti.Segs.Add(x1);
                    ti.Segs.Add(z1);
                    ti.Segs.Add(hw);
                    ti.SegFlag.Add(f);
                    // Bucket a car road wider (+4 m) so the corridor check sees it.
                    double m = hw + (car ? 4.0 : 0.0);
                    Bucket(ti, Math.Min(x0, x1) - m, Math.Min(z0, z1) - m, Math.Max(x0, x1) + m, Math.Max(z0, z1) + m, (2 << KindShift) | idx);
                }
            }
        }

        private static void Bucket(TileIndex ti, double x0, double z0, double x1, double z1, int entry)
        {
            int i0 = Clamp(ti, (int)Math.Floor(x0 / CellM)), i1 = Clamp(ti, (int)Math.Floor(x1 / CellM));
            int j0 = Clamp(ti, (int)Math.Floor(z0 / CellM)), j1 = Clamp(ti, (int)Math.Floor(z1 / CellM));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    List<int> l = ti.Cells[j * ti.N + i];
                    if (l == null) ti.Cells[j * ti.N + i] = l = new List<int>(4);
                    l.Add(entry);
                }
        }

        private static int Clamp(TileIndex ti, int i)
        {
            return i < 0 ? 0 : i >= ti.N ? ti.N - 1 : i;
        }

        private static bool InRing(double[] r, double x, double z)
        {
            bool inside = false;
            int n = r.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = r[2 * i], zi = r[2 * i + 1], xj = r[2 * j], zj = r[2 * j + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        private static bool InTri(List<double> t, int q, double x, double z)
        {
            double ax = t[q], az = t[q + 1], bx = t[q + 2], bz = t[q + 3], cx = t[q + 4], cz = t[q + 5];
            double d1 = (x - bx) * (az - bz) - (ax - bx) * (z - bz);
            double d2 = (x - cx) * (bz - cz) - (bx - cx) * (z - cz);
            double d3 = (x - ax) * (cz - az) - (cx - ax) * (z - az);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static double SegDist2(double x0, double z0, double x1, double z1, double x, double z)
        {
            double dx = x1 - x0, dz = z1 - z0;
            double l2 = dx * dx + dz * dz;
            double t = l2 > 1e-12 ? ((x - x0) * dx + (z - z0) * dz) / l2 : 0.0;
            t = t < 0.0 ? 0.0 : t > 1.0 ? 1.0 : t;
            double ex = x0 + dx * t - x, ez = z0 + dz * t - z;
            return ex * ex + ez * ez;
        }
    }
}
