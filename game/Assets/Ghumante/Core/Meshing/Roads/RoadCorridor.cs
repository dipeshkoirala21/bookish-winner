using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Runtime stand-in for the RATR corridor samples (D17) on packs without RATR: for every 20 m stretch of a piece
    /// (sample i covers [20i − 10, 20i + 10] metres from the first rendered point), twice the smallest distance from
    /// the centreline stretch to a building edge of this tile on either side, in decimetres (0 = open). Only
    /// buildings within the search radius matter (the corridor can only bind a road that wants to be wider than
    /// it). Also used to tighten RATR corridors to the nearer side. Near a tile border the caller decides (see
    /// <see cref="Sample"/>): the layout floors border stretches at the shared border width so both tiles meet.
    /// Deterministic and tile-local.
    /// </summary>
    internal sealed class RoadCorridor
    {
        private const double CellM = 16.0;

        /// <summary>Distance from the tile border within which the neighbour's buildings may be near the road.</summary>
        public const double BorderMarginM = 25.0;

        private readonly double _size;
        private readonly int _cells;
        private readonly int[] _cellStart;
        private readonly int[] _edgeIdx;
        private readonly double[] _ex0, _ez0, _ex1, _ez1;

        public RoadCorridor(TileData t)
        {
            _size = t.Tile.Size;
            _cells = Math.Max(1, (int)Math.Ceiling(_size / CellM));
            int edges = 0;
            foreach (BuildingRecord b in t.Buildings) edges += b.Rings[0].Length / 2;
            _ex0 = new double[edges];
            _ez0 = new double[edges];
            _ex1 = new double[edges];
            _ez1 = new double[edges];
            int e = 0;
            foreach (BuildingRecord b in t.Buildings)
            {
                int[] ring = b.Rings[0];
                int n = ring.Length / 2;
                for (int i = 0; i < n; i++)
                {
                    int j = i + 1 == n ? 0 : i + 1;
                    _ex0[e] = ring[2 * i] / 100.0;
                    _ez0[e] = ring[2 * i + 1] / 100.0;
                    _ex1[e] = ring[2 * j] / 100.0;
                    _ez1[e] = ring[2 * j + 1] / 100.0;
                    e++;
                }
            }
            // CSR grid of edge indices by the cells their bounding box touches.
            var counts = new int[_cells * _cells + 1];
            for (int k = 0; k < edges; k++)
                ForCells(k, c => counts[c + 1]++);
            for (int c = 0; c < _cells * _cells; c++) counts[c + 1] += counts[c];
            _cellStart = counts;
            _edgeIdx = new int[counts[_cells * _cells]];
            var fill = new int[_cells * _cells];
            for (int k = 0; k < edges; k++)
            {
                int kk = k;
                ForCells(k, c => _edgeIdx[_cellStart[c] + fill[c]++] = kk);
            }
        }

        private void ForCells(int k, Action<int> f)
        {
            int i0 = Clamp((int)Math.Floor(Math.Min(_ex0[k], _ex1[k]) / CellM)), i1 = Clamp((int)Math.Floor(Math.Max(_ex0[k], _ex1[k]) / CellM));
            int j0 = Clamp((int)Math.Floor(Math.Min(_ez0[k], _ez1[k]) / CellM)), j1 = Clamp((int)Math.Floor(Math.Max(_ez0[k], _ez1[k]) / CellM));
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
                f(j * _cells + i);
        }

        private int Clamp(int c)
        {
            return c < 0 ? 0 : c >= _cells ? _cells - 1 : c;
        }

        /// <summary>
        /// Corridor samples for a piece (decimetres, 0 = open within <paramref name="radiusM"/>; an edge visible
        /// from two grid cells is simply measured twice). For a stretch within
        /// <see cref="BorderMarginM"/> + radius of the tile border, a positive <paramref name="borderDm"/> caps the
        /// sample at that value (open counts as wider), a negative value −v raises a binding sample to at least v
        /// (so a tile never narrows a border cut below what its neighbour pins), and 0 leaves it as measured.
        /// </summary>
        public int[] Sample(RoadRecord r, double radiusM, int borderDm)
        {
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            double length = RoadWidthModel.RenderedLengthM(r);
            int n = Math.Max(1, (int)Math.Floor(length / RoadAttrRecord.CorridorSpacingM) + 1);
            var result = new int[n];
            for (int i = 0; i < n; i++)
            {
                double s0 = i * RoadAttrRecord.CorridorSpacingM - 10, s1 = s0 + 20;
                double dl = double.PositiveInfinity, dr = double.PositiveInfinity;
                bool nearBorder = false;
                double s = 0;
                for (int k = first; k < last; k++)
                {
                    double ax = p[2 * k] / 100.0, az = p[2 * k + 1] / 100.0, bx = p[2 * k + 2] / 100.0, bz = p[2 * k + 3] / 100.0;
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    double t0 = s, t1 = s + len;
                    s = t1;
                    if (len < 1e-6 || t1 < s0 || t0 > s1) continue;
                    double f0 = Math.Max(0, (s0 - t0) / len), f1 = Math.Min(1, (s1 - t0) / len);
                    double px = ax + (bx - ax) * f0, pz = az + (bz - az) * f0, qx = ax + (bx - ax) * f1, qz = az + (bz - az) * f1;
                    double margin = BorderMarginM + radiusM;
                    if (Math.Min(px, qx) < margin || Math.Min(pz, qz) < margin || Math.Max(px, qx) > _size - margin ||
                        Math.Max(pz, qz) > _size - margin)
                        nearBorder = true;
                    Nearest(px, pz, qx, qz, radiusM, ref dl, ref dr);
                }
                double d = Math.Min(dl, dr);
                int dm = double.IsPositiveInfinity(d) ? 0 : Math.Max(1, (int)Math.Floor(2 * d * 10));
                if (nearBorder && borderDm < 0)
                {
                    // Floor mode: a border stretch never binds below -borderDm (the width both tiles pin the cut to).
                    if (dm != 0 && dm < -borderDm) dm = -borderDm;
                }
                else if (nearBorder && borderDm > 0 && (dm == 0 || dm > borderDm)) dm = borderDm;
                result[i] = dm;
            }
            return result;
        }

        /// <summary>True when segment p-q keeps at least <paramref name="clearanceM"/> from every building edge of the
        /// tile (it neither crosses nor grazes a footprint; a segment wholly inside one is the caller's business).</summary>
        public bool IsClear(double px, double pz, double qx, double qz, double clearanceM)
        {
            double dl = double.PositiveInfinity, dr = double.PositiveInfinity;
            Nearest(px, pz, qx, qz, clearanceM, ref dl, ref dr);
            return Math.Min(dl, dr) >= clearanceM;
        }

        /// <summary>Smallest distance from segment p-q to the building edges within radius, per side (left of p→q in
        /// dl). An edge crossing the segment gives 0 on both sides.</summary>
        private void Nearest(double px, double pz, double qx, double qz, double radius, ref double dl, ref double dr)
        {
            int i0 = Clamp((int)Math.Floor((Math.Min(px, qx) - radius) / CellM)), i1 = Clamp((int)Math.Floor((Math.Max(px, qx) + radius) / CellM));
            int j0 = Clamp((int)Math.Floor((Math.Min(pz, qz) - radius) / CellM)), j1 = Clamp((int)Math.Floor((Math.Max(pz, qz) + radius) / CellM));
            double dx = qx - px, dz = qz - pz;
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                int c = j * _cells + i;
                for (int k = _cellStart[c]; k < _cellStart[c + 1]; k++)
                {
                    int e = _edgeIdx[k];
                    double cx, cz, ex, ez;
                    double d = SegSeg(px, pz, qx, qz, _ex0[e], _ez0[e], _ex1[e], _ez1[e], out cx, out cz, out ex, out ez);
                    if (d > radius) continue;
                    if (d < 1e-9)
                    {
                        dl = dr = 0;
                        return;
                    }
                    double side = dx * (ez - cz) - dz * (ex - cx);
                    if (side > 0)
                    {
                        if (d < dl) dl = d;
                    }
                    else if (d < dr)
                    {
                        dr = d;
                    }
                }
            }
        }

        /// <summary>Distance between segments a-b and c-d with the closest points (on a-b, on c-d).</summary>
        internal static double SegSeg(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz,
                                      out double px, out double pz, out double qx, out double qz)
        {
            // Intersection?
            double d1 = Orient(cx, cz, dx, dz, ax, az), d2 = Orient(cx, cz, dx, dz, bx, bz);
            double d3 = Orient(ax, az, bx, bz, cx, cz), d4 = Orient(ax, az, bx, bz, dx, dz);
            if ((d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0) && d1 != 0 && d2 != 0 && d3 != 0 && d4 != 0)
            {
                double t = d1 / (d1 - d2);
                px = qx = ax + (bx - ax) * t;
                pz = qz = az + (bz - az) * t;
                return 0;
            }
            double best = double.PositiveInfinity;
            px = pz = qx = qz = 0;
            double ox, oz, d;
            d = PointSeg(ax, az, cx, cz, dx, dz, out ox, out oz);
            if (d < best)
            {
                best = d;
                px = ax;
                pz = az;
                qx = ox;
                qz = oz;
            }
            d = PointSeg(bx, bz, cx, cz, dx, dz, out ox, out oz);
            if (d < best)
            {
                best = d;
                px = bx;
                pz = bz;
                qx = ox;
                qz = oz;
            }
            d = PointSeg(cx, cz, ax, az, bx, bz, out ox, out oz);
            if (d < best)
            {
                best = d;
                px = ox;
                pz = oz;
                qx = cx;
                qz = cz;
            }
            d = PointSeg(dx, dz, ax, az, bx, bz, out ox, out oz);
            if (d < best)
            {
                best = d;
                px = ox;
                pz = oz;
                qx = dx;
                qz = dz;
            }
            return best;
        }

        private static double Orient(double ax, double az, double bx, double bz, double cx, double cz)
        {
            return (bx - ax) * (cz - az) - (bz - az) * (cx - ax);
        }

        internal static double PointSeg(double x, double z, double ax, double az, double bx, double bz, out double ox, out double oz)
        {
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            double t = l2 > 0 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            ox = ax + dx * t;
            oz = az + dz * t;
            return Math.Sqrt((x - ox) * (x - ox) + (z - oz) * (z - oz));
        }
    }
}
