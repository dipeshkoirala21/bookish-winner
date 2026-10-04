using System;
using System.IO;
using System.Text;

namespace Ghumante.Core.Data
{
    /// <summary>
    /// Sequential little-endian reader over a byte array window (docs/DATA_FORMATS.md section 0), the C#
    /// twin of <c>binio.Reader</c>. Reading past <see cref="End"/>, an over-long varint or invalid UTF-8
    /// throws <see cref="InvalidDataException"/>.
    /// </summary>
    public sealed class BinReader
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private readonly byte[] _data;

        /// <summary>Absolute index into <see cref="Data"/> of the next byte to read.</summary>
        public int Position;

        /// <summary>Absolute index one past the last readable byte.</summary>
        public readonly int End;

        public BinReader(byte[] data) : this(data, 0, data == null ? 0 : data.Length)
        {
        }

        /// <summary>Reader over <c>data[offset .. offset + count)</c>; <see cref="Position"/> starts at
        /// <paramref name="offset"/>.</summary>
        public BinReader(byte[] data, int offset, int count)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || count < 0 || offset > data.Length - count)
                throw new ArgumentOutOfRangeException(nameof(count));
            _data = data;
            Position = offset;
            End = offset + count;
        }

        public BinReader(ArraySegment<byte> segment) : this(segment.Array, segment.Offset, segment.Count)
        {
        }

        public byte[] Data
        {
            get { return _data; }
        }

        public int Remaining
        {
            get { return End - Position; }
        }

        private int Take(int n)
        {
            if (n < 0 || n > End - Position)
                throw new InvalidDataException("need " + n + " bytes at " + Position + ", have " + (End - Position));
            int p = Position;
            Position += n;
            return p;
        }

        public void Skip(int n)
        {
            Take(n);
        }

        public byte[] Bytes(int n)
        {
            int p = Take(n);
            var b = new byte[n];
            Buffer.BlockCopy(_data, p, b, 0, n);
            return b;
        }

        public byte U8()
        {
            return _data[Take(1)];
        }

        public sbyte I8()
        {
            return unchecked((sbyte)_data[Take(1)]);
        }

        public ushort U16()
        {
            int p = Take(2);
            return (ushort)(_data[p] | _data[p + 1] << 8);
        }

        public short I16()
        {
            return unchecked((short)U16());
        }

        public uint U32()
        {
            int p = Take(4);
            return ReadU32(_data, p);
        }

        public int I32()
        {
            return unchecked((int)U32());
        }

        public ulong U64()
        {
            int p = Take(8);
            return ReadU64(_data, p);
        }

        public long I64()
        {
            return unchecked((long)U64());
        }

        public float F32()
        {
            return BitConverter.Int32BitsToSingle(I32());
        }

        public double F64()
        {
            return BitConverter.Int64BitsToDouble(I64());
        }

        /// <summary>Unsigned LEB128, at most 10 bytes, value below 2^64.</summary>
        public ulong Varint()
        {
            ulong result = 0;
            int shift = 0;
            for (int i = 0; i < 10; i++)
            {
                byte b = _data[Take(1)];
                if (i == 9 && (b & 0x7F) > 1) throw new InvalidDataException("varint overflow");
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
            }
            throw new InvalidDataException("varint longer than 10 bytes");
        }

        /// <summary>Zigzag-decoded <see cref="Varint"/>.</summary>
        public long Svarint()
        {
            ulong z = Varint();
            return unchecked((long)(z >> 1) ^ -(long)(z & 1));
        }

        /// <summary>A varint that must fit an int (counts, indices).</summary>
        public int VarintInt()
        {
            ulong v = Varint();
            if (v > int.MaxValue) throw new InvalidDataException("varint " + v + " does not fit an int");
            return (int)v;
        }

        /// <summary>Varint byte length followed by that many UTF-8 bytes.</summary>
        public string Str()
        {
            int n = VarintInt();
            int p = Take(n);
            try
            {
                return Utf8.GetString(_data, p, n);
            }
            catch (ArgumentException e)
            {
                throw new InvalidDataException("invalid UTF-8 string at " + p, e);
            }
        }

        public static uint ReadU32(byte[] b, int p)
        {
            return (uint)(b[p] | b[p + 1] << 8 | b[p + 2] << 16 | b[p + 3] << 24);
        }

        public static ulong ReadU64(byte[] b, int p)
        {
            return ReadU32(b, p) | (ulong)ReadU32(b, p + 4) << 32;
        }

        public static ushort ReadU16(byte[] b, int p)
        {
            return (ushort)(b[p] | b[p + 1] << 8);
        }
    }
}
