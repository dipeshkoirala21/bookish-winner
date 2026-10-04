using System;
using System.IO;
using Ghumante.Core.Data;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class BinReaderTests
    {
        [Test]
        public void FixedWidthLittleEndian()
        {
            var b = new byte[]
            {
                0xFE, 0xFF, 0x34, 0x12, 0xFE, 0xFF, 0x78, 0x56, 0x34, 0x12, 0xFE, 0xFF, 0xFF, 0xFF,
                0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00, 0x00, 0xC0, 0x3F,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0xBF,
            };
            var r = new BinReader(b);
            Assert.That(r.U8(), Is.EqualTo(0xFE));
            Assert.That(r.I8(), Is.EqualTo(-1));
            Assert.That(r.U16(), Is.EqualTo(0x1234));
            Assert.That(r.I16(), Is.EqualTo(-2));
            Assert.That(r.U32(), Is.EqualTo(0x12345678u));
            Assert.That(r.I32(), Is.EqualTo(-2));
            Assert.That(r.U64(), Is.EqualTo(0x0102030405060708UL));
            Assert.That(r.F32(), Is.EqualTo(1.5f));
            Assert.That(r.F64(), Is.EqualTo(-1.5));
            Assert.That(r.Remaining, Is.EqualTo(0));
            Assert.Throws<InvalidDataException>(() => r.U8());
        }

        [TestCase(new byte[] { 0x00 }, 0UL)]
        [TestCase(new byte[] { 0x7F }, 127UL)]
        [TestCase(new byte[] { 0x80, 0x01 }, 128UL)]
        [TestCase(new byte[] { 0xAC, 0x02 }, 300UL)]
        [TestCase(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, 0xFFFFFFFFUL)]
        [TestCase(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 }, ulong.MaxValue)]
        public void Varint(byte[] bytes, ulong expected)
        {
            var r = new BinReader(bytes);
            Assert.That(r.Varint(), Is.EqualTo(expected));
            Assert.That(r.Remaining, Is.EqualTo(0));
        }

        [Test]
        public void VarintRejectsOverflowAndOverlong()
        {
            Assert.Throws<InvalidDataException>(() =>
                new BinReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x02 }).Varint());
            Assert.Throws<InvalidDataException>(() =>
                new BinReader(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00 }).Varint());
            Assert.Throws<InvalidDataException>(() => new BinReader(new byte[] { 0x80 }).Varint());
        }

        [TestCase(new byte[] { 0x00 }, 0L)]
        [TestCase(new byte[] { 0x01 }, -1L)]
        [TestCase(new byte[] { 0x02 }, 1L)]
        [TestCase(new byte[] { 0x03 }, -2L)]
        [TestCase(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0x0F }, 2147483647L)]
        [TestCase(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, -2147483648L)]
        [TestCase(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 }, long.MaxValue)]
        [TestCase(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 }, long.MinValue)]
        public void Svarint(byte[] bytes, long expected)
        {
            Assert.That(new BinReader(bytes).Svarint(), Is.EqualTo(expected));
        }

        [Test]
        public void StringsAndWindows()
        {
            byte[] ne = System.Text.Encoding.UTF8.GetBytes("काठमाडौं");
            var b = new byte[2 + 1 + ne.Length + 1];
            b[0] = 0xAA;
            b[1] = 0xBB;
            b[2] = (byte)ne.Length;
            Array.Copy(ne, 0, b, 3, ne.Length);
            var r = new BinReader(b, 2, b.Length - 3);
            Assert.That(r.Str(), Is.EqualTo("काठमाडौं"));
            Assert.That(r.Remaining, Is.EqualTo(0));
            Assert.Throws<InvalidDataException>(() => r.U8()); // window ends before the last byte

            Assert.Throws<InvalidDataException>(() => new BinReader(new byte[] { 2, 0xC3, 0x28 }).Str()); // bad UTF-8
            Assert.Throws<InvalidDataException>(() => new BinReader(new byte[] { 5, 0x41 }).Str()); // truncated
            Assert.Throws<ArgumentOutOfRangeException>(() => new BinReader(new byte[4], 3, 2));
        }

        [Test]
        public void Hashes()
        {
            byte[] check = System.Text.Encoding.ASCII.GetBytes("123456789");
            Assert.That(Data.Hashes.Crc32(check), Is.EqualTo(0xCBF43926u));
            Assert.That(Data.Hashes.Crc32(new byte[0]), Is.EqualTo(0u));
            uint part = Data.Hashes.Crc32(check, 0, 4);
            Assert.That(Data.Hashes.Crc32(check, 4, 5, part), Is.EqualTo(0xCBF43926u));
            byte[] a = System.Text.Encoding.ASCII.GetBytes("a");
            Assert.That(Data.Hashes.Fnv1a32(a, 0, 1), Is.EqualTo(0xE40C292Cu));
            Assert.That(Data.Hashes.Fnv1a64(a, 0, 1), Is.EqualTo(0xAF63DC4C8601EC8CUL));
        }
    }
}
