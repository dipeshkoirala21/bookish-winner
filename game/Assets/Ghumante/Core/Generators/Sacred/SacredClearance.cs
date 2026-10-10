using System;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>
    /// The road corridors a sacred structure keeps its loose props, eave bells and fringes out of (docs/W2_DETAIL_CONTRACT.md
    /// §1: nothing stands in a road corridor, nothing overhangs one below <see cref="RoadClearance.MinOverheadClearanceM"/>).
    /// Wraps an <see cref="IRoadCorridorQuery"/> (game metres) for the tile-local coordinates the generators work in, with
    /// the road surface height under a point. The temple itself keeps its exact OSM plan (W2_DESIGN 3.1); only what the
    /// generator adds around and above it gives way. Immutable; safe to share between worker threads when its query is.
    /// </summary>
    public sealed class SacredClearance
    {
        /// <summary>Margin (m) a hanging part keeps above the overhead limit, and a prop's clear distance from a corridor edge.</summary>
        public const double MarginM = 0.25;

        private readonly IRoadCorridorQuery _q;
        private readonly double _x0, _z0;
        private readonly TileData _tile;
        private readonly IHeightSampler _heights;

        /// <param name="corridors">The tile's corridors in game metres.</param>
        /// <param name="tile">The tile the generator builds in (its origin converts tile-local to game metres; its roads give the surface height).</param>
        /// <param name="heights">Terrain heights for the road surface (null: the frame ground is used).</param>
        public SacredClearance(IRoadCorridorQuery corridors, TileData tile, IHeightSampler heights)
        {
            _q = corridors ?? throw new ArgumentNullException(nameof(corridors));
            _tile = tile ?? throw new ArgumentNullException(nameof(tile));
            _x0 = tile.Tile.X0;
            _z0 = tile.Tile.Z0;
            _heights = heights;
        }

        /// <summary>The clearance of a tile from the sacred generators' own corridor stand-in (<see cref="SacredRoads"/>)
        /// until the roads package's index is wired in.</summary>
        public static SacredClearance For(TileData tile, IHeightSampler heights)
        {
            return new SacredClearance(SacredRoads.For(tile), tile, heights);
        }

        /// <summary>Signed distance (m) from a tile-local point to the nearest corridor edge (negative inside).</summary>
        public double Distance(double x, double z)
        {
            return _q.SignedDistance(_x0 + x, _z0 + z);
        }

        /// <summary>True when a round prop of <paramref name="radius"/> at a tile-local point stays out of every corridor.</summary>
        public bool Clear(double x, double z, double radius)
        {
            return Distance(x, z) >= radius + MarginM;
        }

        /// <summary>Road surface height under a tile-local point (terrain when no sampler: NaN).</summary>
        public double Ground(double x, double z)
        {
            if (_heights == null) return double.NaN;
            return new RoadSurface(_tile, _heights).Height(x, z);
        }

        /// <summary>
        /// The lowest absolute height a part hanging over tile-local (x, z) may reach: the road surface plus the overhead
        /// clearance (and the margin) when the point is over a corridor, else negative infinity. <paramref name="groundY"/>
        /// stands in for the surface when there is no height sampler.
        /// </summary>
        public double MinHangY(double x, double z, double groundY)
        {
            if (Distance(x, z) > MarginM) return double.NegativeInfinity;
            double g = Ground(x, z);
            if (double.IsNaN(g)) g = groundY;
            return Math.Max(g, groundY) + RoadClearance.MinOverheadClearanceM + MarginM;
        }
    }

    /// <summary>
    /// The sacred generators' stand-in for the road corridors (<see cref="IRoadCorridorQuery"/>) until the roads package's
    /// <c>RoadCorridorIndex</c> is wired through: every road of the tile on the ground (no tunnels or underground layers)
    /// is a stadium round its centreline as wide as the road mesher's widest game width
    /// (<see cref="RoadWidthModel.MaxGameWidthM"/>) and never narrower than <see cref="RoadClearance.MinCorridorM"/> (the same
    /// rule the buildings package applies). Game metres; distances are clamped to ±<see cref="FarM"/>. Built once per tile
    /// (cached) and immutable.
    /// </summary>
    internal sealed class SacredRoads : IRoadCorridorQuery
    {
        public const double FarM = 8.0;
        private const double CellM = 16.0;
        private static readonly ConditionalWeakTable<TileData, SacredRoads> Cache = new ConditionalWeakTable<TileData, SacredRoads>();

        private readonly double _x0, _z0;
        private readonly int _n;
        private readonly int[] _start, _idx;
        private readonly double[] _ax, _az, _bx, _bz, _half;

        public static SacredRoads For(TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return Cache.GetValue(t, k => new SacredRoads(k));
        }

        private SacredRoads(TileData t)
        {
            _x0 = t.Tile.X0;
            _z0 = t.Tile.Z0;
            _n = Math.Max(1, (int)Math.Ceiling(t.Tile.Size / CellM));
            int segs = 0;
            for (int i = 0; i < t.Roads.Count; i++)
                if (Corridor(t.Roads[i])) segs += t.Roads[i].PointCount - 1;
            _ax = new double[segs];
            _az = new double[segs];
            _bx = new double[segs];
            _bz = new double[segs];
            _half = new double[segs];
            int s = 0;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!Corridor(r)) continue;
                double half = 0.5 * Math.Max(RoadClearance.MinCorridorM, RoadWidthModel.MaxGameWidthM(r, t.RoadAttrOf(i)));
                int[] p = r.Points;
                for (int k = 0; k + 1 < p.Length / 2; k++)
                {
                    _ax[s] = p[2 * k] / 100.0;
                    _az[s] = p[2 * k + 1] / 100.0;
                    _bx[s] = p[2 * k + 2] / 100.0;
                    _bz[s] = p[2 * k + 3] / 100.0;
                    _half[s] = half;
                    s++;
                }
            }
            // Bucket the segments into cells (with their corridor and the far margin).
            var counts = new int[_n * _n + 1];
            for (int pass = 0; pass < 2; pass++)
            {
                int[] fill = pass == 1 ? new int[_n * _n] : null;
                for (int k = 0; k < segs; k++)
                {
                    double pad = _half[k] + FarM;
                    int i0 = Cell(Math.Min(_ax[k], _bx[k]) - pad), i1 = Cell(Math.Max(_ax[k], _bx[k]) + pad);
                    int j0 = Cell(Math.Min(_az[k], _bz[k]) - pad), j1 = Cell(Math.Max(_az[k], _bz[k]) + pad);
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int c = j * _n + i;
                        if (pass == 0) counts[c + 1]++;
                        else _idx[_start[c] + fill[c]++] = k;
                    }
                }
                if (pass == 0)
                {
                    for (int c = 0; c < _n * _n; c++) counts[c + 1] += counts[c];
                    _start = counts;
                    _idx = new int[counts[_n * _n]];
                }
            }
        }

        private static bool Corridor(RoadRecord r)
        {
            return (r.Flags & RoadFlags.Tunnel) == 0 && r.Layer >= 0 && r.PointCount >= 2 && r.RoadClass != RoadClass.Unknown;
        }

        private int Cell(double v)
        {
            int c = (int)Math.Floor(v / CellM);
            return c < 0 ? 0 : c >= _n ? _n - 1 : c;
        }

        public double SignedDistance(double x, double z)
        {
            double lx = x - _x0, lz = z - _z0;
            if (lx < -FarM || lz < -FarM || lx > _n * CellM + FarM || lz > _n * CellM + FarM) return FarM;
            int c = Cell(lz) * _n + Cell(lx);
            double best = FarM;
            for (int q = _start[c]; q < _start[c + 1]; q++)
            {
                int k = _idx[q];
                double ox, oz;
                double d = RoadCorridor.PointSeg(lx, lz, _ax[k], _az[k], _bx[k], _bz[k], out ox, out oz) - _half[k];
                if (d < best) best = d;
            }
            return Math.Max(-FarM, best);
        }

        public bool Overlaps(double[] x, double[] z, int n, out double depthM)
        {
            depthM = 0;
            for (int i = 0; i < n; i++)
            {
                double d = SignedDistance(x[i], z[i]);
                if (d < -depthM) depthM = -d;
            }
            return depthM > 0;
        }
    }
}
