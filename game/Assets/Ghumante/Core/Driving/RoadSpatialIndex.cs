using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Driving
{
    /// <summary>Which level of roads a lookup considers (layered ground, docs/W2_DETAIL_CONTRACT.md §3).</summary>
    public enum RoadLayer : byte
    {
        /// <summary>Every drawn road.</summary>
        Any = 0,

        /// <summary>Bridges and flyovers only (<see cref="RoadSpatialIndex.IsElevated"/>).</summary>
        Elevated = 1,

        /// <summary>Everything but bridges and flyovers (draped roads, underpasses, fords).</summary>
        Ground = 2,
    }

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

        /// <summary>Distance beyond the carriageway edge (negative inside the carriageway): <see cref="DistanceM"/> −
        /// <see cref="HalfWidthM"/> for a centred carriageway.</summary>
        public float EdgeDistanceM;

        /// <summary>Signed offset from the segment's line, positive to the right of the point order.</summary>
        public float LateralM;

        /// <summary>Unit tangent of the segment in point order (game X/Z).</summary>
        public float DirX, DirZ;

        /// <summary>Half the carriageway width at the nearest point, exactly as the road mesh draws it: the W2 width
        /// model (<see cref="RoadLayout.HalfWidthAt"/>) when the index follows <see cref="RoadOptions.WidthModel"/>, else
        /// <see cref="RoadStyle.WidthM"/>.</summary>
        public float HalfWidthM;

        /// <summary>Lateral position of the carriageway centre (positive right of the point order): non-zero on a dual
        /// carriageway, which grows outward away from its median (W2_DESIGN 4.6).</summary>
        public float CentreShiftM;

        /// <summary>Raised footpath widths on the left and right of the carriageway at the nearest point (0 = none).</summary>
        public float FootLeftM, FootRightM;

        /// <summary>True when the point lies on a raised footpath beside the carriageway (not on the carriageway).</summary>
        public bool OnFootpath;

        /// <summary>Distance along the piece from its first rendered point to the nearest point, and the
        /// rendered length of the piece (context segments excluded).</summary>
        public float AlongM, PieceLengthM;

        /// <summary>True within half the width + <see cref="RoadSpatialIndex.OnRoadMarginM"/>.</summary>
        public bool OnRoad;
    }

    /// <summary>
    /// Nearest-road lookups for one tile: a uniform grid over the tile square holding the segments of every
    /// road piece <see cref="RoadMesher"/> draws with the given <see cref="RoadOptions"/> (by default all but
    /// tunnels), with the mesher's widths: the W2 width model through the tile's cached <see cref="RoadLayout"/>
    /// (carriageway widths varying along the piece, dual carriageways shifted outward, raised footpaths) when
    /// <see cref="RoadOptions.WidthModel"/> is on, else <see cref="RoadStyle.WidthM"/>. Context segments, which lie
    /// outside the tile and are never drawn, are left out. Built once per tile, it is read-only afterwards, so it may
    /// be built on a worker thread and queried from any thread. Queries are allocation free and deterministic (exact
    /// ties go to the lower segment index).
    ///
    /// "Nearest" means the nearest road *surface*: the road minimising <c>distance − halfWidth</c>, so a point
    /// on a wide trunk road where a narrow footway crosses it resolves to the trunk road.
    /// </summary>
    public sealed class RoadSpatialIndex
    {
        /// <summary>The tile's solids, built with the index by <see cref="TileGroundQuery.BuildRoadIndex"/> (may be null).</summary>
        internal SolidSet Solids;

        /// <summary>The tile's deck index from <see cref="TileGroundQuery.DeckFactory"/> (may be null).</summary>
        internal Meshing.Bridges.IBridgeDeckQuery Decks;

        /// <summary>True once <see cref="TileGroundQuery.BuildRoadIndex"/> attached the solids and decks.</summary>
        internal bool Prepared;

        public const double DefaultCellM = 32.0;

        /// <summary>Cap on grid cells per side (bounds memory for big tiles; cells grow instead).</summary>
        public const int MaxCellsPerSide = 256;

        /// <summary>A point counts as on a road within half its width plus this margin.</summary>
        public const float OnRoadMarginM = 0.5f;

        /// <summary>Widest raised footpath any profile draws (search reach beyond the carriageway).</summary>
        public const float MaxFootpathM = 6f;

        /// <summary>Deck beyond the carriageway edge on each side of a bridge or flyover (kerb), where its railing stands.</summary>
        public const float DeckKerbM = 0.5f;

        /// <summary>Structural depth of a deck under its surface (for clearance and camera checks).</summary>
        public const float DeckThicknessM = 1.2f;

        private readonly TileData _tile;
        private readonly double _x0, _z0, _cell;
        private readonly int _n; // grid is _n × _n cells over the tile square

        // Segment table (local metres from the tile's south-west corner).
        private readonly double[] _ax, _az, _bx, _bz;
        private readonly float[] _along; // distance from the piece's first rendered point to the segment start
        private readonly int[] _road, _seg;
        private readonly float[] _roadHalfWidth, _roadLength; // per road record (half width: the widest along the piece)
        private readonly RoadLayout _layout; // W2 widths, or null (W1 widths)
        private readonly bool[] _dual, _bridge;
        private readonly bool[] _elevated; // bridge or flyover (structure data, else the Bridge flag)
        private readonly float[][] _surfaceY; // absolute surface heights per point (RoadStructureRecord.DeckY), or null
        private readonly int _elevatedSegs;
        private readonly bool _anyElevated;

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
            _layout = drawn.WidthModel && roads.Count > 0 ? RoadLayout.For(t) : null;
            _dual = new bool[roads.Count];
            _bridge = new bool[roads.Count];
            _elevated = new bool[roads.Count];
            _surfaceY = new float[roads.Count][];
            bool structures = t.RoadStructures.Count == roads.Count && roads.Count > 0;
            for (int r = 0; r < roads.Count; r++)
            {
                if (structures)
                {
                    RoadStructureRecord st = t.RoadStructures[r];
                    _elevated[r] = st.Kind == RoadStructureKind.Bridge || st.Kind == RoadStructureKind.Flyover;
                    if (st.DeckY != null && st.DeckY.Length == roads[r].PointCount) _surfaceY[r] = st.DeckY;
                }
                else
                {
                    _elevated[r] = (roads[r].Flags & RoadFlags.Bridge) != 0;
                }
            }
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
                if (_layout != null)
                {
                    RoadWidthProfile prof = _layout.Profiles[r];
                    _dual[r] = _layout.Attrs[r].Has(RoadAttrFlags.Dual);
                    _bridge[r] = (rec.Flags & RoadFlags.Bridge) != 0 || _elevated[r] || !drawn.CrossSections;
                    // Widest reach of the drawn carriageway: max width, plus the outward shift of a dual carriageway.
                    half = 0.5f * prof.MaxWidth + (_dual[r] ? 0.5f * Math.Max(0f, prof.MaxWidth - prof.RealM) : 0f);
                }
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
                    if (_elevated[r] && _surfaceY[r] != null) _elevatedSegs++;
                    if (_elevated[r]) _anyElevated = true;
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

        /// <summary>Half the widest drawn carriageway of road <paramref name="roadIndex"/> of the tile (the W1 width
        /// without the width model).</summary>
        public float HalfWidthM(int roadIndex)
        {
            return _roadHalfWidth[roadIndex];
        }

        /// <summary>True for a bridge or flyover: the tile's <see cref="RoadStructureRecord"/> says so, or (without
        /// structure data) the road carries <see cref="RoadFlags.Bridge"/>.</summary>
        public bool IsElevated(int roadIndex)
        {
            return _elevated[roadIndex];
        }

        /// <summary>The absolute surface heights per point of a road (its <see cref="RoadStructureRecord.DeckY"/>), or
        /// null when it is draped on the terrain.</summary>
        public float[] SurfaceHeights(int roadIndex)
        {
            return _surfaceY[roadIndex];
        }

        /// <summary>True when any bridge or flyover of the tile has deck heights.</summary>
        public bool HasDecks
        {
            get { return _elevatedSegs > 0; }
        }

        /// <summary>The road surface height of a hit on a road with structure heights (interpolated between the
        /// segment's points), or false when the road is draped.</summary>
        public bool TrySurfaceHeight(in RoadHit hit, out float y, out float gradeAlong)
        {
            y = 0f;
            gradeAlong = 0f;
            if (hit.Road == null || hit.RoadIndex < 0 || hit.RoadIndex >= _surfaceY.Length) return false;
            float[] d = _surfaceY[hit.RoadIndex];
            if (d == null || hit.Segment < 0 || hit.Segment + 1 >= d.Length) return false;
            float a = d[hit.Segment], b = d[hit.Segment + 1];
            y = a + (b - a) * hit.T;
            int[] p = hit.Road.Points;
            double dx = (p[2 * hit.Segment + 2] - p[2 * hit.Segment]) / 100.0, dz = (p[2 * hit.Segment + 3] - p[2 * hit.Segment + 1]) / 100.0;
            double len = Math.Sqrt(dx * dx + dz * dz);
            gradeAlong = len > 1e-6 ? (float)((b - a) / len) : 0f;
            return true;
        }

        /// <summary>The road layout whose widths the index follows (null with W1 widths).</summary>
        public RoadLayout Layout
        {
            get { return _layout; }
        }

        /// <summary>Carriageway half width, centre shift (positive right) and footpaths of road <paramref name="r"/> at
        /// <paramref name="along"/> metres from its first rendered point, as drawn.</summary>
        public void SectionAt(int r, double along, out float half, out float shiftRight, out float footL, out float footR)
        {
            if (_layout == null)
            {
                half = _roadHalfWidth[r];
                shiftRight = 0f;
                footL = 0f;
                footR = 0f;
                return;
            }
            RoadWidthProfile prof = _layout.Profiles[r];
            float w = prof.WidthAt(along);
            half = 0.5f * w;
            // The mesher shifts a dual carriageway along its left normal by (w − real)/2: outward, away from the
            // median on its right.
            shiftRight = _dual[r] ? -0.5f * (w - prof.RealM) : 0f;
            // Footpaths are drawn with cross sections, never on bridges, and a dual carriageway has its median on the right.
            footL = _bridge[r] ? 0f : prof.Sample(prof.FootLeft, along);
            footR = _bridge[r] || _dual[r] ? 0f : prof.Sample(prof.FootRight, along);
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
            return TryNearest(x, z, maxDistM, mustHave, mustNotHave, RoadLayer.Any, out hit);
        }

        /// <summary>As <see cref="TryNearest(double, double, double, RoadFlags, RoadFlags, out RoadHit)"/>, restricted to
        /// one level of roads (<see cref="RoadLayer"/>).</summary>
        public bool TryNearest(double x, double z, double maxDistM, RoadFlags mustHave, RoadFlags mustNotHave, RoadLayer layer, out RoadHit hit)
        {
            hit = default(RoadHit);
            bool filter = mustHave != RoadFlags.None || mustNotHave != RoadFlags.None;
            bool wantElevated = layer == RoadLayer.Elevated;
            if (_ax.Length == 0 || double.IsNaN(x) || double.IsNaN(z) || double.IsNaN(maxDistM)) return false;
            if (wantElevated && !_anyElevated) return false;
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
                    if (layer != RoadLayer.Any && _elevated[_road[i]] != wantElevated) continue;
                    if (filter)
                    {
                        RoadFlags f = _tile.Roads[_road[i]].Flags;
                        if ((f & mustHave) != mustHave || (f & mustNotHave) != 0) continue;
                    }
                    double ax = _ax[i], az = _az[i];
                    double dx = _bx[i] - ax, dz = _bz[i] - az;
                    double len2 = dx * dx + dz * dz;
                    double tRaw = len2 > 0 ? ((px - ax) * dx + (pz - az) * dz) / len2 : 0.0;
                    double t = tRaw < 0 ? 0 : tRaw > 1 ? 1 : tRaw;
                    double qx = ax + t * dx - px, qz = az + t * dz - pz;
                    double d = Math.Sqrt(qx * qx + qz * qz);
                    double edge;
                    if (_layout == null)
                    {
                        edge = d - _roadHalfWidth[_road[i]];
                    }
                    else
                    {
                        // Distance beyond the drawn carriageway (shifted on a dual road), round past the segment ends.
                        double len = Math.Sqrt(len2);
                        double lat = len > 0 ? ((px - ax) * dz - (pz - az) * dx) / len : d;
                        double over = len > 0 ? Math.Max(0.0, Math.Max(-tRaw, tRaw - 1.0)) * len : 0.0;
                        float half, shift, fl, fr;
                        SectionAt(_road[i], _along[i] + t * len, out half, out shift, out fl, out fr);
                        double side = Math.Abs(lat - shift) - half;
                        edge = side > 0 ? Math.Sqrt(side * side + over * over) : over > 0 ? over : side;
                    }
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
            Fill(best, bestT, bestD, bestEdge, px, pz, ref hit);
            return true;
        }

        /// <summary>Completes a hit on segment <paramref name="best"/> (local point px, pz).</summary>
        private void Fill(int best, double bestT, double bestD, double bestEdge, double px, double pz, ref RoadHit hit)
        {
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
            hit.EdgeDistanceM = (float)bestEdge;
            // Right of the direction (ux, uz) in the X-east/Z-north plane is (uz, -ux).
            hit.LateralM = (float)((px - _ax[best]) * uz - (pz - _az[best]) * ux);
            hit.DirX = (float)ux;
            hit.DirZ = (float)uz;
            hit.AlongM = (float)(_along[best] + bestT * slen);
            hit.PieceLengthM = _roadLength[r];
            float h0, sh, fl0, fr0;
            SectionAt(r, hit.AlongM, out h0, out sh, out fl0, out fr0);
            hit.HalfWidthM = h0;
            hit.CentreShiftM = sh;
            hit.FootLeftM = fl0;
            hit.FootRightM = fr0;
            float footHere = hit.LateralM - sh < 0f ? fl0 : fr0;
            hit.OnFootpath = bestEdge > 0 && footHere > 0f && bestEdge <= footHere;
            hit.OnRoad = bestEdge <= OnRoadMarginM && !hit.OnFootpath;
        }

        /// <summary>Signed distance from local (px, pz) to the drawn carriageway of segment i (negative inside), with the
        /// segment parameter and distance to the centreline.</summary>
        private double EdgeOf(int i, double px, double pz, out double t, out double d)
        {
            double ax = _ax[i], az = _az[i];
            double dx = _bx[i] - ax, dz = _bz[i] - az;
            double len2 = dx * dx + dz * dz;
            double tRaw = len2 > 0 ? ((px - ax) * dx + (pz - az) * dz) / len2 : 0.0;
            t = tRaw < 0 ? 0 : tRaw > 1 ? 1 : tRaw;
            double qx = ax + t * dx - px, qz = az + t * dz - pz;
            d = Math.Sqrt(qx * qx + qz * qz);
            if (_layout == null) return d - _roadHalfWidth[_road[i]];
            double len = Math.Sqrt(len2);
            double lat = len > 0 ? ((px - ax) * dz - (pz - az) * dx) / len : d;
            double over = len > 0 ? Math.Max(0.0, Math.Max(-tRaw, tRaw - 1.0)) * len : 0.0;
            float half, shift, fl, fr;
            SectionAt(_road[i], _along[i] + t * len, out half, out shift, out fl, out fr);
            double side = Math.Abs(lat - shift) - half;
            return side > 0 ? Math.Sqrt(side * side + over * over) : over > 0 ? over : side;
        }

        /// <summary>
        /// The deck of a bridge or flyover with structure heights under (x, z) (within its carriageway plus
        /// <see cref="DeckKerbM"/>) that a body with feet at <paramref name="nearY"/> stands on: the highest deck at most
        /// <paramref name="reachM"/> above the feet (any deck when nearY is +∞). Fills the road hit and the deck height
        /// and its rise per metre along the road. Exact ties go to the lower segment index.
        /// </summary>
        public bool TryDeck(double x, double z, float nearY, float reachM, out RoadHit hit, out float deckY, out float gradeAlong)
        {
            hit = default(RoadHit);
            deckY = 0f;
            gradeAlong = 0f;
            if (_elevatedSegs == 0 || double.IsNaN(x) || double.IsNaN(z)) return false;
            double px = x - _x0, pz = z - _z0;
            double reach = MaxHalfWidthM + DeckKerbM;
            double size = _n * _cell;
            if (px < -reach || pz < -reach || px > size + reach || pz > size + reach) return false;
            float limit = float.IsPositiveInfinity(nearY) ? float.PositiveInfinity : nearY + reachM;
            int cx0 = CellOf(px - reach), cx1 = CellOf(px + reach), cz0 = CellOf(pz - reach), cz1 = CellOf(pz + reach);
            int best = -1;
            double bestT = 0, bestD = 0, bestEdge = 0;
            float bestY = float.NegativeInfinity;
            for (int cz = cz0; cz <= cz1; cz++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int c = cz * _n + cx;
                for (int k = _cellStart[c], end = _cellStart[c + 1]; k < end; k++)
                {
                    int i = _cellSegs[k];
                    int r = _road[i];
                    if (!_elevated[r] || _surfaceY[r] == null) continue;
                    double t, d;
                    double edge = EdgeOf(i, px, pz, out t, out d);
                    if (edge > DeckKerbM) continue;
                    float[] ys = _surfaceY[r];
                    int sg = _seg[i];
                    float y = ys[sg] + (ys[sg + 1] - ys[sg]) * (float)t;
                    if (y > limit) continue;
                    if (y > bestY || y == bestY && i < best)
                    {
                        best = i;
                        bestY = y;
                        bestT = t;
                        bestD = d;
                        bestEdge = edge;
                    }
                }
            }
            if (best < 0) return false;
            Fill(best, bestT, bestD, bestEdge, px, pz, ref hit);
            hit.OnRoad = true;
            hit.OnFootpath = false;
            deckY = bestY;
            float dummy;
            TrySurfaceHeight(in hit, out dummy, out gradeAlong);
            return true;
        }

        /// <summary>True when the slab of a bridge or flyover with structure heights (its surface down to
        /// <see cref="DeckThicknessM"/> below, over its deck and railing) covers (x, z) and overlaps the heights
        /// (lowY, highY): a body spanning them there would stand inside the deck.</summary>
        public bool SlabOverlaps(double x, double z, float lowY, float highY, float pad = 0f)
        {
            if (_elevatedSegs == 0 || double.IsNaN(x) || double.IsNaN(z)) return false;
            double px = x - _x0, pz = z - _z0;
            double reach = MaxHalfWidthM + DeckKerbM + 0.3 + pad;
            double size = _n * _cell;
            if (px < -reach || pz < -reach || px > size + reach || pz > size + reach) return false;
            int cx0 = CellOf(px - reach), cx1 = CellOf(px + reach), cz0 = CellOf(pz - reach), cz1 = CellOf(pz + reach);
            for (int cz = cz0; cz <= cz1; cz++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int c = cz * _n + cx;
                for (int k = _cellStart[c], end = _cellStart[c + 1]; k < end; k++)
                {
                    int i = _cellSegs[k];
                    int r = _road[i];
                    if (!_elevated[r] || _surfaceY[r] == null) continue;
                    double t, d;
                    if (EdgeOf(i, px, pz, out t, out d) > DeckKerbM + 0.3 + pad) continue;
                    float[] ys = _surfaceY[r];
                    int sg = _seg[i];
                    float top = ys[sg] + (ys[sg + 1] - ys[sg]) * (float)t;
                    if (top > lowY && top - DeckThicknessM < highY) return true;
                }
            }
            return false;
        }

        /// <summary>The lowest deck underside (surface − <see cref="DeckThicknessM"/>) of a bridge or flyover with
        /// structure heights above (x, z) and higher than <paramref name="fromY"/>, within its deck and railing.</summary>
        public bool TryCeiling(double x, double z, float fromY, out float undersideY)
        {
            undersideY = float.PositiveInfinity;
            if (_elevatedSegs == 0 || double.IsNaN(x) || double.IsNaN(z)) return false;
            double px = x - _x0, pz = z - _z0;
            double reach = MaxHalfWidthM + DeckKerbM + 0.3;
            double size = _n * _cell;
            if (px < -reach || pz < -reach || px > size + reach || pz > size + reach) return false;
            int cx0 = CellOf(px - reach), cx1 = CellOf(px + reach), cz0 = CellOf(pz - reach), cz1 = CellOf(pz + reach);
            bool found = false;
            for (int cz = cz0; cz <= cz1; cz++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int c = cz * _n + cx;
                for (int k = _cellStart[c], end = _cellStart[c + 1]; k < end; k++)
                {
                    int i = _cellSegs[k];
                    int r = _road[i];
                    if (!_elevated[r] || _surfaceY[r] == null) continue;
                    double t, d;
                    if (EdgeOf(i, px, pz, out t, out d) > DeckKerbM + 0.3) continue;
                    float[] ys = _surfaceY[r];
                    int sg = _seg[i];
                    float under = ys[sg] + (ys[sg + 1] - ys[sg]) * (float)t - DeckThicknessM;
                    if (under <= fromY || under >= undersideY) continue;
                    undersideY = under;
                    found = true;
                }
            }
            return found;
        }
    }
}
