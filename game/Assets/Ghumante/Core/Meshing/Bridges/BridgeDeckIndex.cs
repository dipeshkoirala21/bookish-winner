using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>
    /// <see cref="IBridgeDeckQuery"/> over a tile's <see cref="BridgeLayout"/>: the walkable surface of every deck
    /// (carriageway at the record's deck height, raised walkways one kerb higher, foot decks and stair flights as
    /// ramps) and the soffit of every structure, so ground queries pick the right level on and under bridges and
    /// flyovers and clearance checks see the real underside. Coordinates are game metres; a tile answers only for
    /// points inside its own square (the neighbour answers across the border, where the same span continues), so a
    /// query must go to the tile containing the point. Built once per tile (cached); queries are allocation-free and
    /// thread-safe.
    /// </summary>
    public sealed class BridgeDeckIndex : IBridgeDeckQuery
    {
        private const double CellM = 16.0;

        private static readonly ConditionalWeakTable<TileData, BridgeDeckIndex> Cache = new ConditionalWeakTable<TileData, BridgeDeckIndex>();

        /// <summary>The layout this index answers for.</summary>
        public readonly BridgeLayout Layout;

        private readonly double _x0, _z0, _size;
        private readonly int _cells;
        private readonly int[] _cellStart;
        private readonly int[] _items;

        // Items: a span segment (span, station k; k = -1 the context segment before station 0, k = -2 the one after
        // the last station) or a stair flight (span, -(3 + stair index)).
        private readonly int[] _span, _k;

        /// <summary>The deck index of a tile (built on first use, then cached for the tile's lifetime).</summary>
        public static BridgeDeckIndex ForTile(TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return Cache.GetValue(t, k => new BridgeDeckIndex(BridgeLayout.For(k)));
        }

        public BridgeDeckIndex(BridgeLayout layout)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            TileId id = layout.Tile.Tile;
            _x0 = id.X0;
            _z0 = id.Z0;
            _size = id.Size;
            _cells = Math.Max(1, (int)Math.Ceiling(_size / CellM));
            var span = new List<int>();
            var kk = new List<int>();
            for (int si = 0; si < layout.Spans.Count; si++)
            {
                BridgeSpan sp = layout.Spans[si];
                if (sp.HasPre)
                {
                    span.Add(si);
                    kk.Add(-1);
                }
                for (int k = 0; k + 1 < sp.Count; k++)
                {
                    span.Add(si);
                    kk.Add(k);
                }
                if (sp.HasPost)
                {
                    span.Add(si);
                    kk.Add(-2);
                }
                for (int i = 0; i < sp.Stairs.Count; i++)
                {
                    span.Add(si);
                    kk.Add(-3 - i);
                }
            }
            _span = span.ToArray();
            _k = kk.ToArray();
            var counts = new int[_cells * _cells + 1];
            for (int it = 0; it < _span.Length; it++) ForCells(it, c => counts[c + 1]++);
            for (int c = 0; c < _cells * _cells; c++) counts[c + 1] += counts[c];
            _cellStart = counts;
            _items = new int[counts[_cells * _cells]];
            var fill = new int[_cells * _cells];
            for (int it = 0; it < _span.Length; it++)
            {
                int item = it;
                ForCells(it, c => _items[_cellStart[c] + fill[c]++] = item);
            }
        }

        /// <summary>Number of indexed spans.</summary>
        public int SpanCount
        {
            get { return Layout.Spans.Count; }
        }

        private void ForCells(int item, Action<int> f)
        {
            double ax, az, bx, bz, reach;
            Extent(item, out ax, out az, out bx, out bz, out reach);
            int i0 = Clamp((int)Math.Floor((Math.Min(ax, bx) - reach) / CellM)), i1 = Clamp((int)Math.Floor((Math.Max(ax, bx) + reach) / CellM));
            int j0 = Clamp((int)Math.Floor((Math.Min(az, bz) - reach) / CellM)), j1 = Clamp((int)Math.Floor((Math.Max(az, bz) + reach) / CellM));
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
                f(j * _cells + i);
        }

        private void Extent(int item, out double ax, out double az, out double bx, out double bz, out double reach)
        {
            BridgeSpan sp = Layout.Spans[_span[item]];
            int k = _k[item];
            BridgePath p = sp.Path;
            if (k <= -3)
            {
                BridgeStair st = sp.Stairs[-3 - k];
                ax = st.X;
                az = st.Z;
                bx = st.X + st.Dx * st.Length;
                bz = st.Z + st.Dz * st.Length;
                reach = st.HalfWidth + 1;
                return;
            }
            int s0, s1;
            Stations(sp, k, out s0, out s1);
            Ends(sp, k, out ax, out az, out bx, out bz);
            reach = Math.Max(Math.Max(sp.EdgeL(s0), sp.EdgeR(s0)), Math.Max(sp.EdgeL(s1), sp.EdgeR(s1))) * 2.0 + 1;
        }

        private static void Stations(BridgeSpan sp, int k, out int s0, out int s1)
        {
            if (k == -1)
            {
                s0 = s1 = 0;
            }
            else if (k == -2)
            {
                s0 = s1 = sp.Count - 1;
            }
            else
            {
                s0 = k;
                s1 = k + 1;
            }
        }

        private static void Ends(BridgeSpan sp, int k, out double ax, out double az, out double bx, out double bz)
        {
            BridgePath p = sp.Path;
            if (k == -1)
            {
                ax = sp.PreX;
                az = sp.PreZ;
                bx = p.X[0];
                bz = p.Z[0];
            }
            else if (k == -2)
            {
                ax = p.X[p.Count - 1];
                az = p.Z[p.Count - 1];
                bx = sp.PostX;
                bz = sp.PostZ;
            }
            else
            {
                ax = p.X[k];
                az = p.Z[k];
                bx = p.X[k + 1];
                bz = p.Z[k + 1];
            }
        }

        /// <summary>The item after segment k along the span (int.MinValue at a real end).</summary>
        private static int Next(BridgeSpan sp, int k)
        {
            if (k == -1) return 0;
            if (k == -2) return int.MinValue;
            if (k + 1 <= sp.Count - 2) return k + 1;
            return sp.HasPost ? -2 : int.MinValue;
        }

        /// <summary>The item before segment k along the span (int.MinValue at a real end).</summary>
        private static int Prev(BridgeSpan sp, int k)
        {
            if (k == -2) return sp.Count - 2;
            if (k == -1) return int.MinValue;
            if (k - 1 >= 0) return k - 1;
            return sp.HasPre ? -1 : int.MinValue;
        }

        private int Clamp(int c)
        {
            return c < 0 ? 0 : c >= _cells ? _cells - 1 : c;
        }

        /// <inheritdoc />
        public bool TryDeck(double x, double z, float nearY, out float deckY, out float nx, out float ny, out float nz)
        {
            deckY = 0f;
            nx = nz = 0f;
            ny = 1f;
            double lx = x - _x0, lz = z - _z0;
            if (lx < -1e-3 || lz < -1e-3 || lx > _size + 1e-3 || lz > _size + 1e-3 || _items.Length == 0) return false;
            int cell = Clamp((int)Math.Floor(lz / CellM)) * _cells + Clamp((int)Math.Floor(lx / CellM));
            bool found = false;
            float best = float.NegativeInfinity;
            for (int c = _cellStart[cell]; c < _cellStart[cell + 1]; c++)
            {
                int item = _items[c];
                float y, gx, gz;
                if (!Surface(item, lx, lz, false, out y, out gx, out gz)) continue;
                if (y > nearY + BridgeStyle.StepUpM || y <= best) continue;
                best = y;
                found = true;
                deckY = y;
                double l = Math.Sqrt(gx * gx + 1 + gz * gz);
                nx = (float)(-gx / l);
                ny = (float)(1 / l);
                nz = (float)(-gz / l);
            }
            return found;
        }

        /// <inheritdoc />
        public bool TryCeiling(double x, double z, float fromY, out float undersideY)
        {
            undersideY = float.PositiveInfinity;
            double lx = x - _x0, lz = z - _z0;
            if (lx < -1e-3 || lz < -1e-3 || lx > _size + 1e-3 || lz > _size + 1e-3 || _items.Length == 0) return false;
            int cell = Clamp((int)Math.Floor(lz / CellM)) * _cells + Clamp((int)Math.Floor(lx / CellM));
            bool found = false;
            for (int c = _cellStart[cell]; c < _cellStart[cell + 1]; c++)
            {
                int item = _items[c];
                float y, gx, gz;
                if (!Surface(item, lx, lz, true, out y, out gx, out gz)) continue;
                if (y > fromY && y < undersideY)
                {
                    undersideY = y;
                    found = true;
                }
            }
            if (!found) undersideY = 0f;
            return found;
        }

        /// <summary>
        /// The walkable surface (or, with <paramref name="soffit"/>, the structure underside) of an item at tile-local
        /// (lx, lz), with the surface gradient (dy/dx, dy/dz); false when the item does not cover the point.
        /// </summary>
        private bool Surface(int item, double lx, double lz, bool soffit, out float y, out float gx, out float gz)
        {
            y = gx = gz = 0f;
            BridgeSpan sp = Layout.Spans[_span[item]];
            int k = _k[item];
            if (k <= -3)
            {
                BridgeStair st = sp.Stairs[-3 - k];
                double px = lx - st.X, pz = lz - st.Z;
                double along = px * st.Dx + pz * st.Dz, across = -px * st.Dz + pz * st.Dx;
                if (along < 0 || along > st.Length || Math.Abs(across) > st.HalfWidth) return false;
                double g = (st.TopY - st.BottomY) / Math.Max(st.Length, 1e-6);
                y = (float)(st.TopY - g * along);
                if (soffit) y -= 0.35f;
                gx = (float)(-g * st.Dx);
                gz = (float)(-g * st.Dz);
                return true;
            }
            int s0, s1;
            Stations(sp, k, out s0, out s1);
            double ax, az, bx, bz;
            Ends(sp, k, out ax, out az, out bx, out bz);
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            if (l2 < 1e-12) return false;
            double t = ((lx - ax) * dx + (lz - az) * dz) / l2;
            double len = Math.Sqrt(l2), ux = dx / len, uz = dz / len;
            if (t < 0 || t > 1)
            {
                // Beyond this segment: answered by the neighbour, unless the point sits in the outer wedge of a bend
                // (it projects past both segments), where the joint covers it out to the miter.
                int nb = t > 1 ? Next(sp, k) : Prev(sp, k);
                if (nb == int.MinValue) return false;
                double nax, naz, nbx, nbz;
                Ends(sp, nb, out nax, out naz, out nbx, out nbz);
                double ndx = nbx - nax, ndz = nbz - naz, nl2 = ndx * ndx + ndz * ndz;
                if (nl2 > 1e-12)
                {
                    double tn = ((lx - nax) * ndx + (lz - naz) * ndz) / nl2;
                    if (t > 1 && tn >= 0 || t < 0 && tn <= 1) return false;
                }
                double excess = (t > 1 ? t - 1 : -t) * len;
                double reach = 1.75 * Math.Max(Math.Max(sp.EdgeL(s0), sp.EdgeR(s0)), Math.Max(sp.EdgeL(s1), sp.EdgeR(s1)));
                if (excess > reach) return false;
            }
            double tc = t < 0 ? 0 : t > 1 ? 1 : t;
            double qx = ax + dx * tc, qz = az + dz * tc;
            double lat = -uz * (lx - qx) + ux * (lz - qz);
            double ya, yb;
            if (k == -1)
            {
                ya = sp.PreY;
                yb = sp.Path.Y[0];
            }
            else if (k == -2)
            {
                ya = sp.Path.Y[sp.Count - 1];
                yb = sp.PostY;
            }
            else
            {
                ya = sp.Path.Y[s0];
                yb = sp.Path.Y[s1];
            }
            double f = s0 == s1 ? 0 : tc;
            if (soffit)
            {
                double el = sp.EdgeL(s0) + (sp.EdgeL(s1) - sp.EdgeL(s0)) * f, er = sp.EdgeR(s0) + (sp.EdgeR(s1) - sp.EdgeR(s0)) * f;
                if (lat > el || lat < -er) return false;
                double depth = sp.Depth[s0] + (sp.Depth[s1] - sp.Depth[s0]) * f;
                y = (float)(ya + (yb - ya) * tc - depth);
                return true;
            }
            double il = sp.InnerL(s0) + (sp.InnerL(s1) - sp.InnerL(s0)) * f, ir = sp.InnerR(s0) + (sp.InnerR(s1) - sp.InnerR(s0)) * f;
            if (lat > il || lat < -ir) return false;
            double cl = sp.HalfL[s0] + (sp.HalfL[s1] - sp.HalfL[s0]) * f, cr = sp.HalfR[s0] + (sp.HalfR[s1] - sp.HalfR[s0]) * f;
            double yy = ya + (yb - ya) * tc;
            if (sp.RaisedWalk && (lat > cl + 0.05 || lat < -cr - 0.05)) yy += BridgeStyle.KerbHeightM;
            y = (float)yy;
            double g2 = (yb - ya) / len;
            gx = (float)(g2 * ux);
            gz = (float)(g2 * uz);
            return true;
        }
    }
}
