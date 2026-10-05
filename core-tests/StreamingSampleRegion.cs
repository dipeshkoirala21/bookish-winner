using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Data;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The committed sample region shared/sample-regions/kathmandu_core (Swayambhunath..Boudhanath): levels 5 and 6
    /// (horizon) and 8, 9, 10 (detail; roads, buildings, areas and POIs only at 10). Loaded once and decoded tiles are
    /// cached for the Streaming and Meshing tests.
    /// </summary>
    internal static class StreamingSampleRegion
    {
        private static readonly object Lock = new object();
        private static PackReader _pack;
        private static readonly Dictionary<ulong, TileData> Decoded = new Dictionary<ulong, TileData>();

        public static string PackPath
        {
            get { return Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.ghpk"); }
        }

        public static PackReader Pack
        {
            get
            {
                lock (Lock)
                {
                    if (_pack == null)
                    {
                        if (!File.Exists(PackPath)) Assert.Fail("sample region missing: " + PackPath);
                        _pack = new PackReader(File.ReadAllBytes(PackPath));
                    }
                    return _pack;
                }
            }
        }

        /// <summary>Every tile in the pack, ascending key.</summary>
        public static List<TileId> Tiles
        {
            get
            {
                var list = new List<TileId>();
                foreach (PackEntry e in Pack.Entries) list.Add(e.Tile);
                return list;
            }
        }

        public static List<TileId> TilesAt(int level)
        {
            var list = new List<TileId>();
            foreach (TileId t in Tiles)
                if (t.Level == level) list.Add(t);
            return list;
        }

        /// <summary>Decoded tile (cached), or null when the pack does not hold it.</summary>
        public static TileData Tile(TileId t)
        {
            PackReader pack = Pack;
            lock (Lock)
            {
                TileData td;
                if (Decoded.TryGetValue(t.Key, out td)) return td;
                td = pack.ReadTile(t);
                Decoded[t.Key] = td;
                return td;
            }
        }

        /// <summary>The level-10 tile with the most buildings (dense Kathmandu core).</summary>
        public static TileId DensestLeaf()
        {
            TileId best = default(TileId);
            int most = -1;
            foreach (TileId t in TilesAt(10))
            {
                int n = Tile(t).Buildings.Count;
                if (n > most)
                {
                    most = n;
                    best = t;
                }
            }
            return best;
        }
    }
}
