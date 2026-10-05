using System;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The street front of every building of a tile: the BFNT records when the tile has them, else a runtime stand-in
    /// (W1 packs): the profile from <see cref="BuildingGrammar.FallbackProfile"/>, the tile-local area type, and the
    /// ring-0 edge whose outward side faces the nearest road (footways and paths count a little less, so a house on a
    /// lane faces the lane), plus a second road-facing edge for corner houses. <see cref="MainLane"/> says whether that
    /// road is tertiary or above (the shop-share rule of W2_DESIGN 2.2). Built once per decoded tile and cached;
    /// immutable afterwards, so worker threads may share it. Deterministic.
    /// </summary>
    public sealed class BuildingFronts
    {
        /// <summary>A front edge looks this far for a road.</summary>
        public const double SearchM = 25.0;

        private const double CellM = 16.0;
        private static readonly ConditionalWeakTable<TileData, BuildingFronts> Cache = new ConditionalWeakTable<TileData, BuildingFronts>();

        private readonly BuildingFrontRecord[] _fronts;
        private readonly bool[] _main;

        public static BuildingFronts For(TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return Cache.GetValue(t, k => new BuildingFronts(k));
        }

        public BuildingFrontRecord Front(int index)
        {
            return _fronts[index];
        }

        public bool MainLane(int index)
        {
            return _main[index];
        }

        public int Count
        {
            get { return _fronts.Length; }
        }

        private BuildingFronts(TileData t)
        {
            int n = t.Buildings.Count;
            _fronts = new BuildingFrontRecord[n];
            _main = new bool[n];
            var roads = new SegmentGrid(t, CellM);
            AreaType[] cells = t.HasBuildingFronts ? null : AreaTypeGrid.ClassifyTile(t, 8);
            double cellM = t.Tile.Size / 8;
            for (int i = 0; i < n; i++)
            {
                BuildingRecord b = t.Buildings[i];
                int front, second;
                RoadClass cls;
                double dist;
                FindFront(b.Rings[0], roads, out front, out second, out cls, out dist);
                _main[i] = cls != RoadClass.Unknown && RoadStyle.Priority(cls) >= RoadStyle.Priority(RoadClass.Tertiary);
                if (t.HasBuildingFronts)
                {
                    _fronts[i] = t.BuildingFronts[i];
                    continue;
                }
                double cx = 0, cz = 0;
                int pn = b.Rings[0].Length / 2;
                for (int k = 0; k < pn; k++)
                {
                    cx += b.Rings[0][2 * k] / 100.0;
                    cz += b.Rings[0][2 * k + 1] / 100.0;
                }
                cx /= pn;
                cz /= pn;
                int ci = Math.Max(0, Math.Min(7, (int)(cx / cellM))), cj = Math.Max(0, Math.Min(7, (int)(cz / cellM)));
                AreaType area = cells[cj * 8 + ci];
                var f = BuildingFrontRecord.Absent;
                f.Area = area;
                f.Profile = BuildingGrammar.FallbackProfile(t.Tile.X0 + cx, t.Tile.Z0 + cz, area);
                if (front >= 0)
                {
                    f.FrontEdge = (byte)front;
                    f.FrontDistDm = (byte)Math.Min(255, (int)Math.Round(dist * 10));
                }
                if (second >= 0)
                {
                    f.SecondEdge = (byte)second;
                    f.Flags |= (byte)BuildingFrontFlags.Corner;
                }
                _fronts[i] = f;
            }
        }

        private static void FindFront(int[] ring, SegmentGrid roads, out int front, out int second, out RoadClass cls, out double dist)
        {
            int n = ring.Length / 2;
            front = second = -1;
            cls = RoadClass.Unknown;
            dist = double.PositiveInfinity;
            double bestScore = double.PositiveInfinity, longest = -1;
            int longestEdge = 0;
            if (n > 254) n = 254;
            double fnx = 0, fnz = 0;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == ring.Length / 2 ? 0 : i + 1;
                double ax = ring[2 * i] / 100.0, az = ring[2 * i + 1] / 100.0, bx = ring[2 * j] / 100.0, bz = ring[2 * j + 1] / 100.0;
                double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 0.5) continue;
                if (len > longest)
                {
                    longest = len;
                    longestEdge = i;
                }
                double nx = dz / len, nz = -dx / len, mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
                RoadClass c;
                double d = roads.NearestInFront(mx, mz, nx, nz, SearchM, out c);
                if (double.IsPositiveInfinity(d)) continue;
                double score = d - 0.05 * len;
                if (score < bestScore)
                {
                    bestScore = score;
                    front = i;
                    dist = d;
                    cls = c;
                    fnx = nx;
                    fnz = nz;
                }
            }
            if (front < 0)
            {
                front = longest >= 0 ? longestEdge : -1;
                dist = 0;
                return;
            }
            // Corner house: another road-facing edge turned at least 45 degrees from the front.
            double bestSecond = 15.0;
            for (int i = 0; i < n; i++)
            {
                if (i == front) continue;
                int j = i + 1 == ring.Length / 2 ? 0 : i + 1;
                double ax = ring[2 * i] / 100.0, az = ring[2 * i + 1] / 100.0, bx = ring[2 * j] / 100.0, bz = ring[2 * j + 1] / 100.0;
                double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 2.0) continue;
                double nx = dz / len, nz = -dx / len;
                if (nx * fnx + nz * fnz > 0.707) continue;
                RoadClass c;
                double d = roads.NearestInFront(0.5 * (ax + bx), 0.5 * (az + bz), nx, nz, bestSecond, out c);
                if (d < bestSecond)
                {
                    bestSecond = d;
                    second = i;
                }
            }
        }

        /// <summary>Road centreline segments of a tile on a grid (tile-local metres).</summary>
        internal sealed class SegmentGrid
        {
            private readonly double _cell;
            private readonly int _n;
            private readonly int[] _start, _idx;
            private readonly double[] _ax, _az, _bx, _bz;
            private readonly RoadClass[] _cls;

            public SegmentGrid(TileData t, double cell)
            {
                _cell = cell;
                _n = Math.Max(1, (int)Math.Ceiling(t.Tile.Size / cell));
                int segs = 0;
                foreach (RoadRecord r in t.Roads) segs += Math.Max(0, r.PointCount - 1);
                _ax = new double[segs];
                _az = new double[segs];
                _bx = new double[segs];
                _bz = new double[segs];
                _cls = new RoadClass[segs];
                int s = 0;
                foreach (RoadRecord r in t.Roads)
                {
                    if ((r.Flags & RoadFlags.Tunnel) != 0) continue;
                    int[] p = r.Points;
                    for (int k = 0; k + 1 < p.Length / 2; k++)
                    {
                        _ax[s] = p[2 * k] / 100.0;
                        _az[s] = p[2 * k + 1] / 100.0;
                        _bx[s] = p[2 * k + 2] / 100.0;
                        _bz[s] = p[2 * k + 3] / 100.0;
                        _cls[s] = r.RoadClass;
                        s++;
                    }
                }
                var counts = new int[_n * _n + 1];
                for (int k = 0; k < s; k++) Cells(k, c => counts[c + 1]++);
                for (int c = 0; c < _n * _n; c++) counts[c + 1] += counts[c];
                _start = counts;
                _idx = new int[counts[_n * _n]];
                var fill = new int[_n * _n];
                for (int k = 0; k < s; k++)
                {
                    int kk = k;
                    Cells(k, c => _idx[_start[c] + fill[c]++] = kk);
                }
            }

            private int Clamp(int v)
            {
                return v < 0 ? 0 : v >= _n ? _n - 1 : v;
            }

            private void Cells(int k, Action<int> f)
            {
                int i0 = Clamp((int)Math.Floor(Math.Min(_ax[k], _bx[k]) / _cell)), i1 = Clamp((int)Math.Floor(Math.Max(_ax[k], _bx[k]) / _cell));
                int j0 = Clamp((int)Math.Floor(Math.Min(_az[k], _bz[k]) / _cell)), j1 = Clamp((int)Math.Floor(Math.Max(_az[k], _bz[k]) / _cell));
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    f(j * _n + i);
            }

            /// <summary>Distance from (x, z) to the nearest road segment lying in front of the normal (within 60° of
            /// it), footways, paths and steps counted 3 m further; +∞ when none within <paramref name="max"/>.</summary>
            public double NearestInFront(double x, double z, double nx, double nz, double max, out RoadClass cls)
            {
                cls = RoadClass.Unknown;
                double best = double.PositiveInfinity;
                int i0 = Clamp((int)Math.Floor((x - max) / _cell)), i1 = Clamp((int)Math.Floor((x + max) / _cell));
                int j0 = Clamp((int)Math.Floor((z - max) / _cell)), j1 = Clamp((int)Math.Floor((z + max) / _cell));
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int c = j * _n + i;
                    for (int k = _start[c]; k < _start[c + 1]; k++)
                    {
                        int e = _idx[k];
                        double ox, oz;
                        double d = RoadCorridor.PointSeg(x, z, _ax[e], _az[e], _bx[e], _bz[e], out ox, out oz);
                        if (d > max) continue;
                        double vx = ox - x, vz = oz - z;
                        if (d > 0.05 && (vx * nx + vz * nz) < 0.5 * d) continue;
                        double score = d + (RoadStyle.IsTrail(_cls[e]) ? 3.0 : 0.0);
                        if (score < best)
                        {
                            best = score;
                            cls = _cls[e];
                        }
                    }
                }
                return best;
            }
        }
    }
}
