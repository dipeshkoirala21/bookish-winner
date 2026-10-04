using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Search
{
    /// <summary>One ranked search hit.</summary>
    public struct SearchResult
    {
        /// <summary>Index into the index's entries.</summary>
        public int EntryIndex;

        /// <summary><c>1785 · m + 3000 · importance</c> (integer, compared by the golden tests).</summary>
        public long ScoreInt;

        public SearchEntry Entry;

        /// <summary>Score in [0, 1]: <c>m/1000 · 0.7 + importance/255 · 0.3</c>.</summary>
        public double Score
        {
            get { return ScoreInt / (double)SearchEngine.ScoreScale; }
        }
    }

    /// <summary>
    /// Search over one decoded GHSI index: an exact port of <c>search_index.SearchIndex.rank</c>
    /// (integer arithmetic, so results and scores are identical to the Python reference). Exact and
    /// prefix matches come from a binary search over the sorted keys; fuzzy matches use the
    /// optimal-string-alignment distance within the keys sharing the query's first letter.
    /// Thread-safe for concurrent searches after construction.
    /// </summary>
    public sealed class SearchEngine
    {
        public const long ScoreScale = 2550000; // 1785 * 1000 + 3000 * 255

        private readonly SearchIndexData _index;

        public SearchEngine(SearchIndexData index)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public SearchIndexData Index
        {
            get { return _index; }
        }

        /// <summary>Tie-break rank (lower first): places before POIs, then ascending kind value.</summary>
        public static int KindPriority(int kind)
        {
            return kind >= SearchEntry.PlaceKindOffset ? kind - SearchEntry.PlaceKindOffset : kind + SearchEntry.PlaceKindOffset;
        }

        public static int MatchScorePrefix(int extra)
        {
            return extra == 0 ? 1000 : Math.Max(100, 900 - 10 * extra);
        }

        /// <summary>Ranked results, best first; at most <paramref name="limit"/>.</summary>
        public List<SearchResult> Search(string query, int limit = 10)
        {
            var results = new List<SearchResult>();
            if (limit <= 0) return results;
            Dictionary<int, int> best = MatchScores(query);
            foreach (var kv in best)
                results.Add(new SearchResult
                {
                    EntryIndex = kv.Key, Entry = _index.Entries[kv.Key],
                    ScoreInt = 1785L * kv.Value + 3000L * _index.Entries[kv.Key].Importance,
                });
            results.Sort(Compare);
            if (results.Count > limit) results.RemoveRange(limit, results.Count - limit);
            return results;
        }

        private static int Compare(SearchResult a, SearchResult b)
        {
            int c = b.ScoreInt.CompareTo(a.ScoreInt);
            if (c != 0) return c;
            c = KindPriority(a.Entry.Kind).CompareTo(KindPriority(b.Entry.Kind));
            return c != 0 ? c : a.EntryIndex.CompareTo(b.EntryIndex);
        }

        /// <summary>Entry index to best match score in thousandths (steps 1-4 of the lookup).</summary>
        public Dictionary<int, int> MatchScores(string query)
        {
            var best = new Dictionary<int, int>();
            string q1 = Fold.Apply(query);
            Match(q1, best);
            if (Romanize.HasDevanagari(query))
            {
                string q2 = Fold.Apply(Romanize.Apply(query));
                if (q2 != q1) Match(q2, best);
            }
            return best;
        }

        private void Hit(Dictionary<int, int> best, int keyIndex, int m)
        {
            int e = _index.KeyEntries[keyIndex];
            int old;
            if (!best.TryGetValue(e, out old) || m > old) best[e] = m;
        }

        private void Match(string q, Dictionary<int, int> best)
        {
            if (q.Length == 0) return;
            string[] keys = _index.Keys;
            int n = q.Length;
            for (int i = LowerBound(keys, q); i < keys.Length && keys[i].StartsWith(q, StringComparison.Ordinal); i++)
                Hit(best, i, MatchScorePrefix(keys[i].Length - n));

            if (n <= 3) return;
            int maxD = Math.Max(1, n / 4);
            int lo = LowerBound(keys, q.Substring(0, 1));
            int hi = LowerBound(keys, ((char)(q[0] + 1)).ToString());
            string lastKey = null;
            int d = maxD + 1;
            for (int i = lo; i < hi; i++)
            {
                string k = keys[i];
                if (Math.Abs(k.Length - n) > maxD) continue;
                if (!string.Equals(k, lastKey, StringComparison.Ordinal)) // identical keys are adjacent
                {
                    lastKey = k;
                    d = OsaDistance(q, k, maxD);
                }
                if (d <= maxD) Hit(best, i, 750 - 100 * d);
            }
        }

        /// <summary>First index whose key is not ordinally less than <paramref name="q"/> (bisect_left).</summary>
        public static int LowerBound(string[] keys, string q)
        {
            int lo = 0, hi = keys.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (string.CompareOrdinal(keys[mid], q) < 0) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>Optimal-string-alignment distance, or <c>maxD + 1</c> once it is known to exceed maxD.</summary>
        public static int OsaDistance(string a, string b, int maxD)
        {
            int la = a.Length, lb = b.Length;
            if (Math.Abs(la - lb) > maxD) return maxD + 1;
            var prev2 = new int[lb + 1];
            var prev = new int[lb + 1];
            var cur = new int[lb + 1];
            for (int j = 0; j <= lb; j++) prev[j] = j;
            for (int i = 1; i <= la; i++)
            {
                cur[0] = i;
                char ai = a[i - 1];
                int rowMin = i;
                for (int j = 1; j <= lb; j++)
                {
                    int cost = ai == b[j - 1] ? 0 : 1;
                    int v = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
                    if (i > 1 && j > 1 && ai == b[j - 2] && a[i - 2] == b[j - 1]) v = Math.Min(v, prev2[j - 2] + 1);
                    cur[j] = v;
                    if (v < rowMin) rowMin = v;
                }
                if (rowMin > maxD) return maxD + 1; // row minima never decrease
                var t = prev2;
                prev2 = prev;
                prev = cur;
                cur = t;
            }
            return prev[lb] <= maxD ? prev[lb] : maxD + 1;
        }
    }
}
