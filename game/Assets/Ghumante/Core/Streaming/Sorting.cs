using System.Collections.Generic;

namespace Ghumante.Core.Streaming
{
    /// <summary>
    /// In-place heap sort over an array prefix with a struct comparer: allocation-free on both .NET and Unity's
    /// runtimes (List.Sort allocates a delegate or a comparer wrapper per call on one or the other). Not stable,
    /// so callers sort by unique keys.
    /// </summary>
    internal static class Sorting
    {
        public static void HeapSort<T, TComparer>(T[] a, int n, TComparer cmp) where TComparer : struct, IComparer<T>
        {
            for (int i = n / 2 - 1; i >= 0; i--) SiftDown(a, i, n, cmp);
            for (int end = n - 1; end > 0; end--)
            {
                T t = a[0];
                a[0] = a[end];
                a[end] = t;
                SiftDown(a, 0, end, cmp);
            }
        }

        private static void SiftDown<T, TComparer>(T[] a, int i, int n, TComparer cmp) where TComparer : struct, IComparer<T>
        {
            while (true)
            {
                int c = 2 * i + 1;
                if (c >= n) return;
                if (c + 1 < n && cmp.Compare(a[c + 1], a[c]) > 0) c++;
                if (cmp.Compare(a[c], a[i]) <= 0) return;
                T t = a[i];
                a[i] = a[c];
                a[c] = t;
                i = c;
            }
        }

        /// <summary>Append to a grow-only array buffer.</summary>
        public static void Add<T>(ref T[] a, ref int n, T item)
        {
            if (n == a.Length) System.Array.Resize(ref a, a.Length == 0 ? 16 : a.Length * 2);
            a[n++] = item;
        }
    }
}
