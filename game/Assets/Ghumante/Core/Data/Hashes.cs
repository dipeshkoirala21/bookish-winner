namespace Ghumante.Core.Data
{
    /// <summary>CRC-32 (IEEE 802.3, as zlib.crc32) and FNV-1a hashes used by the data formats.</summary>
    public static class Hashes
    {
        private static readonly uint[] CrcTable = MakeCrcTable();

        private static uint[] MakeCrcTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        /// <summary>CRC-32 of <c>data[offset .. offset + count)</c>, continuing from <paramref name="crc"/>
        /// (pass 0 to start).</summary>
        public static uint Crc32(byte[] data, int offset, int count, uint crc = 0)
        {
            uint c = ~crc;
            int end = offset + count;
            for (int i = offset; i < end; i++) c = CrcTable[(c ^ data[i]) & 0xFF] ^ (c >> 8);
            return ~c;
        }

        public static uint Crc32(byte[] data)
        {
            return Crc32(data, 0, data.Length);
        }

        public const uint Fnv32Offset = 0x811C9DC5;
        public const uint Fnv32Prime = 0x01000193;
        public const ulong Fnv64Offset = 0xCBF29CE484222325;
        public const ulong Fnv64Prime = 0x100000001B3;

        public static uint Fnv1a32(byte[] data, int offset, int count)
        {
            uint h = Fnv32Offset;
            for (int i = offset; i < offset + count; i++) h = unchecked((h ^ data[i]) * Fnv32Prime);
            return h;
        }

        public static ulong Fnv1a64(byte[] data, int offset, int count)
        {
            ulong h = Fnv64Offset;
            for (int i = offset; i < offset + count; i++) h = unchecked((h ^ data[i]) * Fnv64Prime);
            return h;
        }

        /// <summary>Per-building variation seed: FNV-1a 32 over the LEB128 bytes of
        /// <paramref name="osmRef"/> (DATA_FORMATS.md 1.6).</summary>
        public static uint BuildingSeed(ulong osmRef)
        {
            var buf = new byte[10];
            int n = 0;
            ulong v = osmRef;
            while (v >= 0x80)
            {
                buf[n++] = (byte)(v & 0x7F | 0x80);
                v >>= 7;
            }
            buf[n++] = (byte)v;
            return Fnv1a32(buf, 0, n);
        }

        /// <summary>SEED chunk value: FNV-1a 64 over <c>(u64 tile_key, u32 data_version)</c> little-endian.</summary>
        public static ulong TileSeed(ulong tileKey, uint dataVersion)
        {
            var buf = new byte[12];
            for (int i = 0; i < 8; i++) buf[i] = (byte)(tileKey >> (8 * i));
            for (int i = 0; i < 4; i++) buf[8 + i] = (byte)(dataVersion >> (8 * i));
            return Fnv1a64(buf, 0, 12);
        }
    }
}
