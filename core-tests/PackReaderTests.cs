using System.IO;
using System.Linq;
using System.Text.Json;
using Ghumante.Core.Data;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class PackReaderTests
    {
        private static void Check(PackReader pr)
        {
            JsonElement x = GoldenFiles.Json("golden_pack.json");
            Assert.That(pr.RegionId, Is.EqualTo(x.GetProperty("region_id").GetString()));
            Assert.That(pr.RegionId.Length, Is.EqualTo(16)); // "golden_region_long_id" truncated to 16
            Assert.That(pr.DataVersion, Is.EqualTo(x.GetProperty("data_version").GetUInt32()));
            Assert.That(pr.TileCount, Is.EqualTo(x.GetProperty("tile_count").GetInt32()));
            var entries = x.GetProperty("entries").EnumerateArray().ToArray();
            PackEntry[] dir = pr.Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                JsonElement e = entries[i];
                Assert.That(dir[i].Key, Is.EqualTo(e.GetProperty("key").GetUInt64()));
                Assert.That(dir[i].Offset, Is.EqualTo(e.GetProperty("offset").GetUInt64()));
                Assert.That(dir[i].Size, Is.EqualTo(e.GetProperty("size").GetUInt32()));
                Assert.That(dir[i].Crc32, Is.EqualTo(e.GetProperty("crc32").GetUInt32()));
                Assert.That(dir[i].Tile, Is.EqualTo(new TileId(e.GetProperty("level").GetInt32(), e.GetProperty("tx").GetInt32(), e.GetProperty("ty").GetInt32())));
                byte[] blob = pr.GetTileBytes(dir[i].Key);
                Assert.That(Hashes.Crc32(blob), Is.EqualTo(e.GetProperty("sha_crc32").GetUInt32()));
                TileData td = pr.ReadTile(dir[i].Tile);
                Assert.That(td.Tile, Is.EqualTo(dir[i].Tile));
                Assert.That(pr.Contains(dir[i].Tile), Is.True);
            }
            foreach (JsonElement k in x.GetProperty("absent_keys").EnumerateArray())
            {
                Assert.That(pr.Contains(k.GetUInt64()), Is.False);
                Assert.That(pr.GetTileBytes(k.GetUInt64()), Is.Null);
                Assert.That(pr.TryGetEntry(k.GetUInt64(), out _), Is.False);
            }
            TileData horizon = pr.ReadTile(new TileId(6, 17, 12));
            TileReaderTests.AssertTileMatches(horizon, x.GetProperty("horizon_tile"));
            Assert.That(horizon.Roads, Is.Empty);
            Assert.That(horizon.HasDetail, Is.False);

            TileData golden = pr.ReadTile(new TileId(10, 266, 200));
            TileReaderTests.AssertTileMatches(golden, GoldenFiles.Json("golden_tile.json"));
        }

        [Test]
        public void ReadsFromBytes()
        {
            Check(new PackReader(GoldenFiles.Bytes("golden.ghpk")));
        }

        [Test]
        public void ReadsFromASeekableStream()
        {
            using (var pr = new PackReader(File.OpenRead(GoldenFiles.PathOf("golden.ghpk"))))
                Check(pr);
        }

        [Test]
        public void RejectsCorruption()
        {
            byte[] good = GoldenFiles.Bytes("golden.ghpk");
            var pr = new PackReader(good);
            PackEntry first = pr.Entries[0];

            byte[] b = (byte[])good.Clone();
            b[(int)first.Offset + 40] ^= 1; // inside the first tile
            var bad = new PackReader(b);
            Assert.Throws<InvalidDataException>(() => bad.GetTileBytes(first.Key));
            Assert.That(bad.GetTileBytes(first.Key, verify: false), Is.Not.Null);

            b = (byte[])good.Clone();
            b[b.Length - 5] ^= 1; // inside the directory
            Assert.Throws<InvalidDataException>(() => new PackReader(b));

            b = (byte[])good.Clone();
            b[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => new PackReader(b));

            b = (byte[])good.Clone();
            b[4] = 9;
            Assert.Throws<InvalidDataException>(() => new PackReader(b));

            Assert.Throws<InvalidDataException>(() => new PackReader(good.Take(good.Length - 1).ToArray()));
            Assert.Throws<InvalidDataException>(() => new PackReader(new byte[10]));
        }
    }
}
