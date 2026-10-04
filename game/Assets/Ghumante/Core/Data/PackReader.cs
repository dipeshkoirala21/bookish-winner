using System;
using System.IO;
using System.Text;

namespace Ghumante.Core.Data
{
    /// <summary>One GHPK directory entry.</summary>
    public struct PackEntry
    {
        public ulong Key;
        public ulong Offset;
        public uint Size;
        public uint Crc32;

        public TileId Tile
        {
            get { return TileId.FromKey(Key); }
        }
    }

    /// <summary>
    /// Random-access reader for a GHPK region pack (DATA_FORMATS.md section 2; pack.py). Opens over a
    /// seekable <see cref="Stream"/> (the tile data is read on demand) or a byte array. The header and
    /// directory (including <c>directory_crc32</c>, key order and ranges) are validated on open, and each
    /// tile's CRC-32 is checked on read. Reads through a stream are serialised with a lock, so one reader
    /// may be shared by IO threads.
    /// </summary>
    public sealed class PackReader : IDisposable
    {
        public const ushort Version = 1;
        public const int HeaderSize = 64;
        public const int DirEntrySize = 24;
        public const int RegionIdSize = 16;
        public static readonly uint Magic = Ght.FourCC("GHPK");

        private readonly Stream _stream;
        private readonly byte[] _bytes;
        private readonly bool _ownsStream;
        private readonly object _lock = new object();
        private readonly ulong[] _keys;
        private readonly PackEntry[] _entries;

        public string RegionId { get; private set; }
        public uint DataVersion { get; private set; }
        public ushort Flags { get; private set; }

        public int TileCount
        {
            get { return _entries.Length; }
        }

        /// <summary>Directory entries in ascending key order.</summary>
        public PackEntry[] Entries
        {
            get { return (PackEntry[])_entries.Clone(); }
        }

        /// <summary>Reader over a whole pack held in memory.</summary>
        public PackReader(byte[] pack)
        {
            _bytes = pack ?? throw new ArgumentNullException(nameof(pack));
            _entries = Open(pack.LongLength, ReadBytes, out _keys);
        }

        /// <summary>Reader over a seekable stream; disposing the reader disposes the stream when
        /// <paramref name="ownsStream"/>.</summary>
        public PackReader(Stream stream, bool ownsStream = true)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanSeek || !stream.CanRead) throw new ArgumentException("pack stream must be readable and seekable");
            _ownsStream = ownsStream;
            _entries = Open(stream.Length, ReadBytes, out _keys);
        }

        private PackEntry[] Open(long fileSize, Func<long, int, byte[]> read, out ulong[] keys)
        {
            if (fileSize < HeaderSize) throw new InvalidDataException("file shorter than a pack header");
            var r = new BinReader(read(0, HeaderSize));
            if (r.U32() != Magic) throw new InvalidDataException("bad pack magic");
            ushort version = r.U16();
            if (version != Version) throw new InvalidDataException("unsupported GHPK version " + version);
            Flags = r.U16();
            uint count = r.U32();
            DataVersion = r.U32();
            ulong dirOff = r.U64(), dirSize = r.U64();
            byte[] rid = r.Bytes(RegionIdSize);
            uint dirCrc = r.U32();
            if (dirSize != (ulong)count * DirEntrySize) throw new InvalidDataException("directory size does not match tile count");
            if (dirOff < HeaderSize || dirOff > (ulong)fileSize || dirSize > (ulong)fileSize - dirOff)
                throw new InvalidDataException("directory outside the file");
            if (dirSize > int.MaxValue) throw new InvalidDataException("directory too large");
            byte[] dir = read((long)dirOff, (int)dirSize);
            if (Hashes.Crc32(dir) != dirCrc) throw new InvalidDataException("pack directory CRC mismatch");

            int len = RegionIdSize;
            while (len > 0 && rid[len - 1] == 0) len--;
            for (int i = 0; i < len; i++)
                if (rid[i] >= 0x80) throw new InvalidDataException("region id is not ASCII");
            RegionId = Encoding.ASCII.GetString(rid, 0, len);

            var entries = new PackEntry[count];
            keys = new ulong[count];
            var dr = new BinReader(dir);
            for (int i = 0; i < count; i++)
            {
                var e = new PackEntry { Key = dr.U64(), Offset = dr.U64(), Size = dr.U32(), Crc32 = dr.U32() };
                if (i > 0 && e.Key <= keys[i - 1]) throw new InvalidDataException("pack directory keys not strictly ascending");
                if (e.Offset < HeaderSize || e.Offset > dirOff || e.Size > dirOff - e.Offset)
                    throw new InvalidDataException("tile data outside the tile area");
                if (e.Size > int.MaxValue) throw new InvalidDataException("tile larger than 2 GiB");
                entries[i] = e;
                keys[i] = e.Key;
            }
            return entries;
        }

        private byte[] ReadBytes(long offset, int count)
        {
            var buf = new byte[count];
            if (_bytes != null)
            {
                Buffer.BlockCopy(_bytes, (int)offset, buf, 0, count);
                return buf;
            }
            lock (_lock)
            {
                _stream.Seek(offset, SeekOrigin.Begin);
                int got = 0;
                while (got < count)
                {
                    int n = _stream.Read(buf, got, count - got);
                    if (n <= 0) throw new InvalidDataException("unexpected end of pack stream");
                    got += n;
                }
            }
            return buf;
        }

        /// <summary>Directory index of a key (binary search), or -1.</summary>
        public int IndexOf(ulong key)
        {
            int lo = 0, hi = _keys.Length - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                ulong k = _keys[mid];
                if (k == key) return mid;
                if (k < key) lo = mid + 1;
                else hi = mid - 1;
            }
            return -1;
        }

        public bool Contains(ulong key)
        {
            return IndexOf(key) >= 0;
        }

        public bool Contains(TileId tile)
        {
            return IndexOf(tile.Key) >= 0;
        }

        public bool TryGetEntry(ulong key, out PackEntry entry)
        {
            int i = IndexOf(key);
            entry = i >= 0 ? _entries[i] : default(PackEntry);
            return i >= 0;
        }

        /// <summary>The raw GHT1 blob of a tile, or null when the pack does not hold it. Throws
        /// <see cref="InvalidDataException"/> when <paramref name="verify"/> and the CRC does not match.</summary>
        public byte[] GetTileBytes(ulong key, bool verify = true)
        {
            int i = IndexOf(key);
            if (i < 0) return null;
            PackEntry e = _entries[i];
            byte[] blob = ReadBytes((long)e.Offset, (int)e.Size);
            if (verify && Hashes.Crc32(blob) != e.Crc32)
                throw new InvalidDataException("tile " + TileId.FromKey(key) + " CRC mismatch");
            return blob;
        }

        /// <summary>Read and decode a tile, or null when absent. Checks that the blob holds the tile asked for
        /// and the pack's data version.</summary>
        public TileData ReadTile(TileId tile, bool verify = true)
        {
            byte[] blob = GetTileBytes(tile.Key, verify);
            if (blob == null) return null;
            TileData td = TileReader.Decode(blob, verify);
            if (td.Tile != tile) throw new InvalidDataException("pack entry for " + tile + " holds tile " + td.Tile);
            if (td.DataVersion != DataVersion)
                throw new InvalidDataException("tile " + tile + " has data_version " + td.DataVersion + ", pack " + DataVersion);
            return td;
        }

        public void Dispose()
        {
            if (_ownsStream && _stream != null) _stream.Dispose();
        }
    }
}
