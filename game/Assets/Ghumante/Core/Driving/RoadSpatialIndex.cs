using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Driving
{
    /// <summary>Result of <see cref="RoadSpatialIndex.TryNearest(double, double, double, out RoadHit)"/>.</summary>
    public struct RoadHit
    {
        /// <summary>The tile holding the piece, and the road piece itself (as decoded).</summary>
        public TileData Tile;

        public RoadRecord Road;

        /// <summary>Index of <see cref="Road"/> in <see cref="TileData.Roads"/>.</summary>
        public int RoadIndex;

        /// <summary>The segment runs from point <c>Segment</c> to point <c>Segment + 1</c> of the record's point
        /// list (which includes context points; context segments are never returned).</summary>
        public int Segment;

        /// <summary>Position along the segment, 0 at its first point and 1 at its second.</summary>
        public float T;

        /// <summary>Nearest point on the centreline, game metres.</summary>
        public double X, Z;

        /// <summary>Distance from the query point to the centreline.</summary>
        public float DistanceM;

        /// <summary>Distance beyond the road edge: <see cref="DistanceM"/> − <see cref="HalfWidthM"/> (negative
        /// inside the road).</summary>
        public float EdgeDistanceM;

        /// <summary>Signed offset from the segment's line, positive to the right of the point order.</summary>
        public float LateralM;

        /// <summary>Unit tangent of the segment in point order (game X/Z).</summary>
        public float DirX, DirZ;

        /// <summary>Half the ribbon width (<see cref="RoadStyle.WidthM"/>, exactly as the road mesh).</summary>
        public float HalfWidthM;

        /// <summary>Distance along the piece from its first rendered point to the nearest point, and the
        /// rendered length of the piece (context segments excluded).</summary>
        public float AlongM, PieceLengthM;

        /// <summary>True within half the width + <see cref="RoadSpatialIndex.OnRoadMarginM"/>.</summary>
        public bool OnRoad;
    }

    /// <summary>
    /// Nearest-road lookups for one tile: a uniform grid over the tile square holding the segments of every
    /// road piece <see cref="RoadMesher"/> draws with the given <see cref="RoadOptions"/> (by default all but
    /// tunnels), with the mesher's widths (<see cref="RoadStyle.WidthM"/>). Context segments, which lie outside
    /// the tile and are never drawn, are left out. Built once per tile, it is read-only afterwards, so it may be
    /// built on a worker thread and queried from any thread. Queries are allocation free and deterministic (exact
    /// ties go to the lower segment index).
    ///
    /// "Nearest" means the nearest road *surface*: the road minimising <c>distance − halfWidth</c>, so a point
    /// on a wide trunk road where a narrow footway crosses it resolves to the trunk road.
    /// </summary>
    public sealed class RoadSpatialIndex
    {
        public const double DefaultCellM = 32.0;

        /// <summary>Cap on grid cells per side (bounds memory for big tiles; cells grow instead).</summary>
        public const int MaxCellsPerSide = 256;

        /// <summary>A point counts as on a road within half its width plus this margin.</summary>
        public const float OnRoadMarginM = 0.5f;

        private readonly TileData _tile;
        private readonly double _x0, _z0, _cell;
        private readonly int _n; // grid is _n × _n cells over the tile square

        // Segment table (local metres from the tile's south-west corner).
        private readonly double[] _ax, _az, _bx, _bz;
        private readonly float[] _along; // distance from the piece's first rendered point to the segment start
        private readonly int[] _road, _seg;
        private readonly float[] _roadHalfWidth, _roadLength; // per road record

        // CSR grid: segments of cell c are _cellSegs[_cellStart[c] .. _cellStart[c + 1]).
        private readonly int[] _cellStart, _cellSegs;

        public TileData Tile
        {
            get { return _tile; }
        }

        public TileId TileId
        {
            get { return _tile.Tile; }
        }

        public int SegmentCount
        {
            get { return _ax.Length; }
        }

        /// <summary>Largest half width of any road in this tile (0 without roads).</summary>
        public float MaxHalfWidthM { get; private set; }

        /// <summary>Index of the roads drawn with default <see cref="RoadOptions"/>.</summary>
        public RoadSpatialIndex(TileData t) : this(t, null, DefaultCellM)
        {
        }

        /// <summary>Index of the roads <see cref="RoadMesher"/> draws with <paramref name="drawn"/> (null: defaults),
        /// bucketed in cells of about <paramref name="cellM"/> metres.</summary>
        public RoadSpatialIndex(TileData t, RoadOptions drawn, double cellM = DefaultCellM)
        {
            _tile = t ?? throw new ArgumentNullException(nameof(t));
            if (!(cellM > 0)) throw new ArgumentOutOfRangeException(nameof(cellM));
            double size = t.Tile.Size;
            _x0 = t.Tile.X0;
            _z0 = t.Tile.Z0;
            _n = Math.Min(MaxCellsPerSide, Math.Max(1, (int)Math.Ceiling(size / cellM)));
            _cell = size / _n;

            if (drawn == null) drawn = new RoadOptions();
            var roads = t.Roads;
            int segCount = 0;
            _roadHalfWidth = new float[roads.Count];
            _roadLength = new float[roads.Count];
            var include = new bool[roads.Count];
            for (int r = 0; r < roads.Count; r++)
            {
                include[r] = RoadMesher.IsDrawn(roads[r], drawn);
                if (!include[r]) continue;
                int first, last;
                RenderedRange(roads[r], out first, out last);
                if (last > first) segCount += last - first;
            }

            _ax = new double[segCount];
            _az = new double[segCount];
            _bx = new double[segCount];
            _bz = new double[segCount];
            _along = new float[segCount];
            _road = new int[segCount];
            _seg = new int[segCount];

            int s = 0;
            float maxHalf = 0f;
            for (int r = 0; r < roads.Count; r++)
            {
                RoadRecord rec = roads[r];
                float half = 0.5f * RoadStyle.WidthM(rec);
                _roadHalfWidth[r] = half;
                if (!include[r]) continue;
                int first, last;
                RenderedRange(rec, out first, out last);
                if (last <= first) continue;
                if (half > maxHalf) maxHalf = half;
                int[] p = rec.Points;
                double along = 0.0;
                for (int k = first; k < last; k++)
                {
                    _ax[s] = p[2 * k] / 100.0;
                    _az[s] = p[2 * k + 1] / 100.0;
                    _bx[s] = p[2 * k + 2] / 100.0;
                    _bz[s] = p[2 * k + 3] / 100.0;
                    _along[s] = (float)along;
                    _road[s] = r;
                    _seg[s] = k;
                    double dx = _bx[s] - _ax[s], dz = _bz[s] - _az[s];
                    along += Math.Sqrt(dx * dx + dz * dz);
                    s++;
                }
                _roadLength[r] = (float)along;
            }
            MaxHalfWidthM = maxHalf;

            // Bucket each segment into every cell its bounding box touches (count, prefix sum, fill).
            int cells = _n * _n;
            _cellStart = new int[cells + 1];
            for (int i = 0; i < segCount; i++)
            {
                int cx0, cz0, cx1, cz1;
                SegmentCells(i, out cx0, out cz0, out cx1, out cz1);
                for (int cz = cz0; cz <= cz1; cz++)
                for (int cx = cx0; cx <= cx1; cx++)
                    _cellStart[cz * _n + cx + 1]++;
            }
            for (int c = 0; c < cells; c++) _cellStart[c + 1] += _cellStart[c];
            _cellSegs = new int[_cellStart[cells]];
            var fill = new int[cells];
            Array.Copy(_cellStart, fill, cells);
            for (int i = 0; i < segCount; i++)
            {
                int cx0, cz0, cx1, cz1;
                SegmentCells(i, out cx0, out cz0, out cx1, out cz1);
                for (int cz = cz0; cz <= cz1; cz++)
                for (int cx = cx0; cx <= cx1; cx++)
                    _cellSegs[fill[cz * _n + cx]++] = i;
            }
        }

        /// <summary>The rendered point range of a piece: context points (HAS_PREV_CTX / HAS_NEXT_CTX) are
        /// excluded, so its segments are <c>first .. last-1</c>.</summary>
        public static void RenderedRange(RoadRecord r, out int first, out int last)
        {
            first = r.HasPrevContext ? 1 : 0;
            last = r.PointCount - 1 - (r.HasNextContext ? 1 : 0);
        }

        /// <summary>Half the width of road <paramref name="roadIndex"/> of the tile.</summary>
        public float HalfWidthM(int roadIndex)
        {
            return _roadHalfWidth[roadIndex];
        }

        private void SegmentCells(int i, out int cx0, out int cz0, out int cx1, out int cz1)
        {
            cx0 = CellOf(Math.Min(_ax[i], _bx[i]));
            cx1 = CellOf(Math.Max(_ax[i], _bx[i]));
            cz0 = CellOf(Math.Min(_az[i], _bz[i]));
            cz1 = CellOf(Math.Max(_az[i], _bz[i]));
        }

        private int CellOf(double local)
        {
            double c = Math.Floor(local / _cell);
            if (c < 0) return 0;
            return c >= _n ? _n - 1 : (int)c;
        }

        /// <summary>
        /// The road surface nearest to game point (x, z): the indexed road with the smallest
        /// <c>distance − halfWidth</c> among those whose edge lies within <paramref name="maxDistM"/> of the point
        /// (so <c>maxDistM = OnRoadMarginM</c> finds exactly the roads the point is on).
        /// </summary>
        public bool TryNearest(double x, double z, double maxDistM, out RoadHit hit)
        {
            return TryNearest(x, z, maxDistM, RoadFlags.None, RoadFlags.None, out hit);
        }

        /// <summary>As <see cref="TryNearest(double, double, double, out RoadHit)"/>, among roads having all of
        /// <paramref name="mustHave"/> and none of <paramref name="mustNotHave"/> (e.g. only bridges, or no bridges).</summary>
        public bool TryNearest(double x, double z, double maxDistM, RoadFlags mustHave, RoadFlags mustNotHave, out RoadHit hit)
        {
            hit = default(RoadHit);
            bool filter = mustHave != RoadFlags.None || mustNotHave != RoadFlags.None;
            if (_ax.Length == 0 || double.IsNaN(x) || double.IsNaN(z) || double.IsNaN(maxDistM)) return false;
            double px = x - _x0, pz = z - _z0;
            double reach = maxDistM + MaxHalfWidthM;
            if (reach < 0) return false;
            double size = _n * _cell;
            if (px < -reach || pz < -reach || px > size + reach || pz > size + reach) return false;

            int cx0 = CellOf(px - reach), cx1 = CellOf(px + reach);
            int cz0 = CellOf(pz - reach), cz1 = CellOf(pz + reach);
            int best = -1;
            double bestEdge = double.PositiveInfinity, bestT = 0, bestD = 0;
            for (int cz = cz0; cz <= cz1; cz++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int c = cz * _n + cx;
                for (int k = _cellStart[c], end = _cellStart[c + 1]; k < end; k++)
                {
                    int i = _cellSegs[k];
                    if (filter)
                    {
                        RoadFlags f = _tile.Roads[_road[i]].Flags;
                        if ((f & mustHave) != mustHave || (f & mustNotHave) != 0) continue;
                    }
                    double ax = _ax[i], az = _az[i];
                    double dx = _bx[i] - ax, dz = _bz[i] - az;
                    double len2 = dx * dx + dz * dz;
                    double t = len2 > 0 ? ((px - ax) * dx + (pz - az) * dz) / len2 : 0.0;
                    if (t < 0) t = 0;
                    else if (t > 1) t = 1;
                    double qx = ax + t * dx - px, qz = az + t * dz - pz;
                    double d = Math.Sqrt(qx * qx + qz * qz);
                    double edge = d - _roadHalfWidth[_road[i]];
                    if (edge > maxDistM) continue;
                    if (edge < bestEdge || edge == bestEdge && i < best)
                    {
                        best = i;
                        bestEdge = edge;
                        bestT = t;
                        bestD = d;
                    }
                }
            }
            if (best < 0) return false;

            int r = _road[best];
            double sx = _bx[best] - _ax[best], sz = _bz[best] - _az[best];
            double slen = Math.Sqrt(sx * sx + sz * sz);
            double ux = slen > 0 ? sx / slen : 0.0, uz = slen > 0 ? sz / slen : 0.0;
            double nx = _ax[best] + bestT * sx, nz = _az[best] + bestT * sz;
            hit.Tile = _tile;
            hit.Road = _tile.Roads[r];
            hit.RoadIndex = r;
            hit.Segment = _seg[best];
            hit.T = (float)bestT;
            hit.X = _x0 + nx;
            hit.Z = _z0 + nz;
            hit.DistanceM = (float)bestD;
            hit.HalfWidthM = _roadHalfWidth[r];
            hit.EdgeDistanceM = (float)bestEdge;
            // Right of the direction (ux, uz) in the X-east/Z-north plane is (uz, -ux).
            hit.LateralM = (float)((px - _ax[best]) * uz - (pz - _az[best]) * ux);
            hit.DirX = (float)ux;
            hit.DirZ = (float)uz;
            hit.AlongM = (float)(_along[best] + bestT * slen);
            hit.PieceLengthM = _roadLength[r];
            hit.OnRoad = bestEdge <= OnRoadMarginM;
            return true;
        }
    }
}
