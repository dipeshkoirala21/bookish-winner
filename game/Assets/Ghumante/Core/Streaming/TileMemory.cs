using Ghumante.Core.Data;

namespace Ghumante.Core.Streaming
{
    /// <summary>
    /// Approximate managed-heap footprint of decoded tiles, for <see cref="LruByteCache{TKey,TValue}"/> budgets.
    /// Counts array payloads plus a fixed per-object overhead (header, fields, list slot), which is close enough
    /// for budgeting on a 64-bit runtime.
    /// </summary>
    public static class TileMemory
    {
        private const long ObjectOverhead = 24;
        private const long ArrayOverhead = 32;

        /// <summary>Estimated bytes held by a decoded <see cref="TileData"/> (0 for null).</summary>
        public static long EstimateBytes(TileData t)
        {
            if (t == null) return 0;
            long b = 256 + (long)t.Chunks.Length * 16;
            if (t.HeightsQ != null) b += ArrayOverhead + 2L * t.HeightsQ.Length;
            if (t.Biomes != null) b += ArrayOverhead + t.Biomes.Length;
            if (t.MetaJson != null) b += ObjectOverhead + 2L * t.MetaJson.Length;
            for (int i = 0; i < t.Names.Count; i++)
            {
                NameRecord n = t.Names[i];
                b += ObjectOverhead + 8 + Str(n.Default) + Str(n.En) + Str(n.Ne);
            }
            for (int i = 0; i < t.Roads.Count; i++) b += ObjectOverhead + 72 + ArrayOverhead + 4L * t.Roads[i].Points.Length;
            for (int i = 0; i < t.Lines.Count; i++) b += ObjectOverhead + 40 + ArrayOverhead + 4L * t.Lines[i].Points.Length;
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                int[][] rings = t.Buildings[i].Rings;
                b += ObjectOverhead + 64 + ArrayOverhead + 8L * rings.Length;
                for (int k = 0; k < rings.Length; k++) b += ArrayOverhead + 4L * rings[k].Length;
            }
            for (int i = 0; i < t.Areas.Count; i++)
            {
                AreaRecord a = t.Areas[i];
                b += ObjectOverhead + 40 + 3 * ArrayOverhead + 4L * (a.Vertices.Length + a.Indices.Length + a.Rings.Length);
            }
            b += (long)t.Pois.Count * (ObjectOverhead + 56);
            return b;
        }

        private static long Str(string s)
        {
            return s == null ? 0 : ObjectOverhead + 2L * s.Length;
        }
    }
}
