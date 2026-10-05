using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Streaming
{
    /// <summary>
    /// CDLOD-style tile selection over the quadtree <em>area</em> (M1_PLAN contract, refined): the output is a set
    /// of <see cref="SelectedNode"/>s whose areas cover every point that has data within the view radius exactly
    /// once, finer near the focus.
    /// <para>
    /// Descent starts at the coarsest existing level. An area A is split into its four children when (a) its
    /// closest point to the focus lies within <see cref="StreamingConfig.SplitRadius"/> of level(A) + 1 and
    /// (b) a tile finer than A's source exists inside A (an existing strict descendant; otherwise splitting is
    /// pointless). An area without any source is always split while data lies inside it. Children without finer
    /// data are still emitted and drawn from the ancestor source, cropped. Areas with no source at all and areas
    /// beyond the view radius are skipped. Levels a pack does not hold (kathmandu_core has 5, 6, 8, 9, 10) simply
    /// have no tiles; tiles finer than the finest ring are ignored.
    /// </para>
    /// <para>
    /// The result is ordered by distance from the focus to the area's closest point, then by area key, and is
    /// deterministic. After warm-up a call allocates nothing (scratch lists are reused; the lazily memoised
    /// existence index of the <see cref="Func{T,TResult}"/> constructor grows only when new areas are visited).
    /// Not thread-safe: use one selector per thread.
    /// </para>
    /// </summary>
    public sealed class TileSelector
    {
        private struct Pending
        {
            public TileId Area;
            public TileId Source;
            public bool HasSource;
        }

        private struct Candidate
        {
            public double D2;
            public SelectedNode Node;
        }

        private struct CandidateOrder : IComparer<Candidate>
        {
            public int Compare(Candidate a, Candidate b)
            {
                int c = a.D2.CompareTo(b.D2);
                return c != 0 ? c : a.Node.Area.Key.CompareTo(b.Node.Area.Key);
            }
        }

        private readonly StreamingConfig _config;
        private readonly TileIndex _index;
        private readonly List<Pending> _stack = new List<Pending>(64);
        private readonly List<TileId> _roots = new List<TileId>(64);
        private Candidate[] _found = new Candidate[256];
        private int _foundCount;

        /// <summary>Selector over the tiles for which <paramref name="exists"/> answers true. Roots are the tiles of
        /// the coarsest ring level around the focus; existence below them is probed lazily and memoised.</summary>
        public TileSelector(StreamingConfig config, Func<TileId, bool> exists)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (exists == null) throw new ArgumentNullException(nameof(exists));
            _config = config.Clone();
            _config.Validate();
            _index = new FuncIndex(exists, _config.CoarsestLevel, _config.FinestLevel);
        }

        /// <summary>Selector over an explicit tile set (for example every installed pack's directory). Roots are
        /// the coarsest existing level, so data coarser than the coarsest ring is used too.</summary>
        public TileSelector(StreamingConfig config, IEnumerable<TileId> tiles)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            _config = config.Clone();
            _config.Validate();
            _index = new SetIndex(tiles, _config.FinestLevel);
        }

        /// <summary>Selector over every tile of a region pack.</summary>
        public static TileSelector ForPack(StreamingConfig config, PackReader pack)
        {
            if (pack == null) throw new ArgumentNullException(nameof(pack));
            PackEntry[] entries = pack.Entries;
            var tiles = new TileId[entries.Length];
            for (int i = 0; i < entries.Length; i++) tiles[i] = entries[i].Tile;
            return new TileSelector(config, tiles);
        }

        /// <summary>A copy of the configuration taken at construction (later changes to the original do not
        /// apply; build a new selector for a new tier).</summary>
        public StreamingConfig Config
        {
            get { return _config.Clone(); }
        }

        /// <summary>True when the tile exists (and is not finer than the finest ring).</summary>
        public bool Exists(TileId t)
        {
            return t.Level <= _config.FinestLevel && _index.Exists(t);
        }

        /// <summary>Clear <paramref name="result"/> and fill it with the selection around game point (x, z).</summary>
        public void Select(double x, double z, List<SelectedNode> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z)) return;
            double view = _config.ViewRadiusM, view2 = view * view;
            int finest = _config.FinestLevel;

            _stack.Clear();
            _foundCount = 0;
            _roots.Clear();
            _index.Roots(x, z, view, _roots);
            for (int k = 0; k < _roots.Count; k++)
            {
                TileId r = _roots[k];
                bool has = _index.Exists(r);
                _stack.Add(new Pending { Area = r, Source = r, HasSource = has });
            }

            while (_stack.Count > 0)
            {
                Pending p = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);
                TileId a = p.Area;
                double d2 = TileArea.ClosestDistanceSquared(a, x, z);
                if (d2 > view2) continue;

                bool split = false;
                if (a.Level < finest && _index.FinestBelow(a) >= 0)
                {
                    if (!p.HasSource) split = true;
                    else
                    {
                        double r = _config.SplitRadius(a.Level + 1);
                        split = d2 <= r * r;
                    }
                }

                if (split)
                {
                    for (int q = 3; q >= 0; q--)
                    {
                        TileId c = TileArea.Child(a, q);
                        if (_index.Exists(c)) _stack.Add(new Pending { Area = c, Source = c, HasSource = true });
                        else if (p.HasSource) _stack.Add(new Pending { Area = c, Source = p.Source, HasSource = true });
                        else if (_index.FinestBelow(c) >= 0) _stack.Add(new Pending { Area = c, Source = c, HasSource = false });
                    }
                }
                else if (p.HasSource)
                {
                    Sorting.Add(ref _found, ref _foundCount, new Candidate { D2 = d2, Node = new SelectedNode(a, p.Source) });
                }
            }

            Sorting.HeapSort(_found, _foundCount, default(CandidateOrder));
            for (int k = 0; k < _foundCount; k++) result.Add(_found[k].Node);
        }

        // ---------------------------------------------------------------------------------------------------
        // Existence indexes
        // ---------------------------------------------------------------------------------------------------

        private abstract class TileIndex
        {
            public abstract bool Exists(TileId t);

            /// <summary>The finest level of an existing strict descendant of t (within the level cap), or -1.</summary>
            public abstract int FinestBelow(TileId t);

            /// <summary>Append the descent roots whose square comes within <paramref name="radius"/> of (x, z).</summary>
            public abstract void Roots(double x, double z, double radius, List<TileId> roots);
        }

        private sealed class SetIndex : TileIndex
        {
            private readonly HashSet<ulong> _exists = new HashSet<ulong>();
            private readonly Dictionary<ulong, sbyte> _finestBelow = new Dictionary<ulong, sbyte>();
            private readonly TileId[] _roots;

            public SetIndex(IEnumerable<TileId> tiles, int maxLevel)
            {
                int coarsest = int.MaxValue;
                var kept = new List<TileId>();
                foreach (TileId t in tiles)
                {
                    if (t.Level > maxLevel) continue;
                    if (_exists.Add(t.Key)) kept.Add(t);
                    if (t.Level < coarsest) coarsest = t.Level;
                }
                var roots = new HashSet<ulong>();
                foreach (TileId t in kept)
                {
                    for (int l = t.Level - 1; l >= coarsest; l--)
                    {
                        ulong k = TileArea.AncestorAt(t, l).Key;
                        sbyte cur;
                        if (!_finestBelow.TryGetValue(k, out cur) || cur < t.Level) _finestBelow[k] = (sbyte)t.Level;
                    }
                    roots.Add(TileArea.AncestorAt(t, coarsest).Key);
                }
                var keys = new List<ulong>(roots);
                keys.Sort();
                _roots = new TileId[keys.Count];
                for (int i = 0; i < keys.Count; i++) _roots[i] = TileId.FromKey(keys[i]);
            }

            public override bool Exists(TileId t)
            {
                return _exists.Contains(t.Key);
            }

            public override int FinestBelow(TileId t)
            {
                sbyte l;
                return _finestBelow.TryGetValue(t.Key, out l) ? l : -1;
            }

            public override void Roots(double x, double z, double radius, List<TileId> roots)
            {
                double r2 = radius * radius;
                for (int i = 0; i < _roots.Length; i++)
                    if (TileArea.ClosestDistanceSquared(_roots[i], x, z) <= r2) roots.Add(_roots[i]);
            }
        }

        private sealed class FuncIndex : TileIndex
        {
            private readonly Func<TileId, bool> _exists;
            private readonly int _rootLevel, _maxLevel;
            private readonly Dictionary<ulong, sbyte> _finestBelow = new Dictionary<ulong, sbyte>();

            public FuncIndex(Func<TileId, bool> exists, int rootLevel, int maxLevel)
            {
                _exists = exists;
                _rootLevel = rootLevel;
                _maxLevel = maxLevel;
            }

            public override bool Exists(TileId t)
            {
                return t.Level <= _maxLevel && _exists(t);
            }

            public override int FinestBelow(TileId t)
            {
                if (t.Level >= _maxLevel) return -1;
                sbyte l;
                if (_finestBelow.TryGetValue(t.Key, out l)) return l;
                int best = -1;
                for (int q = 0; q < 4; q++)
                {
                    TileId c = TileArea.Child(t, q);
                    if (c.Level > best && _exists(c)) best = c.Level;
                    int below = FinestBelow(c);
                    if (below > best) best = below;
                }
                _finestBelow[t.Key] = (sbyte)best;
                return best;
            }

            public override void Roots(double x, double z, double radius, List<TileId> roots)
            {
                double s = TileId.SizeAt(_rootLevel);
                int n = 1 << _rootLevel;
                int x0 = Clamp((int)Math.Floor((x - radius) / s), n), x1 = Clamp((int)Math.Floor((x + radius) / s), n);
                int z0 = Clamp((int)Math.Floor((z - radius) / s), n), z1 = Clamp((int)Math.Floor((z + radius) / s), n);
                double r2 = radius * radius;
                for (int ty = z0; ty <= z1; ty++)
                {
                    for (int tx = x0; tx <= x1; tx++)
                    {
                        var t = new TileId(_rootLevel, tx, ty);
                        if (TileArea.ClosestDistanceSquared(t, x, z) > r2) continue;
                        if (Exists(t) || FinestBelow(t) >= 0) roots.Add(t);
                    }
                }
            }

            private static int Clamp(int v, int n)
            {
                return v < 0 ? 0 : v >= n ? n - 1 : v;
            }
        }
    }
}
