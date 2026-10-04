using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Ghumante.Core.Data
{
    /// <summary>GHT1 constants and small helpers (DATA_FORMATS.md section 1).</summary>
    public static class Ght
    {
        public const ushort Version = 1;
        public const int HeaderSize = 32;
        public const int ChunkEntrySize = 16;
        public const ushort FlagHasDetail = 1;
        public const byte CodecStored = 0;
        public const byte CodecDeflate = 1;

        /// <summary>Decompression-bomb guard, as tile_format.MAX_CHUNK_RAW_SIZE.</summary>
        public const int MaxChunkRawSize = 64 << 20;

        public const float HeightMinM = -100.0f;
        public const float HeightStepM = 0.15f;

        public static readonly uint Magic = FourCC("GHT1");
        public static readonly uint Area = FourCC("AREA");
        public static readonly uint Biom = FourCC("BIOM");
        public static readonly uint Bldg = FourCC("BLDG");
        public static readonly uint Hght = FourCC("HGHT");
        public static readonly uint Line = FourCC("LINE");
        public static readonly uint Meta = FourCC("META");
        public static readonly uint Name = FourCC("NAME");
        public static readonly uint Pois = FourCC("POIS");
        public static readonly uint Road = FourCC("ROAD");
        public static readonly uint Seed = FourCC("SEED");

        /// <summary>A fourcc as the little-endian u32 of its four ASCII bytes.</summary>
        public static uint FourCC(string s)
        {
            if (s == null || s.Length != 4) throw new ArgumentException("fourcc must be 4 characters");
            return (uint)(s[0] | s[1] << 8 | s[2] << 16 | s[3] << 24);
        }

        public static string FourCCToString(uint v)
        {
            return new string(new[] { (char)(v & 0xFF), (char)(v >> 8 & 0xFF), (char)(v >> 16 & 0xFF), (char)(v >> 24) });
        }

        /// <summary>u16 height code to metres: <c>float32(-100.0 + q * 0.15)</c> evaluated in double.</summary>
        public static float Dequantize(ushort q)
        {
            return (float)(-100.0 + q * 0.15);
        }

        /// <summary>Metres to the global u16 height code (round half to even, clamped).</summary>
        public static ushort Quantize(double h)
        {
            double q = Math.Round((h + 100.0) / 0.15, MidpointRounding.ToEven);
            if (q < 0) return 0;
            return q > 65535 ? (ushort)65535 : (ushort)q;
        }
    }

    /// <summary>
    /// Decoder for GHT1 tile blobs, the C# twin of <c>tile_format.decode_tile</c>. Verifies the payload
    /// CRC-32, skips unknown fourccs, inflates raw-DEFLATE chunks and rejects any corruption with
    /// <see cref="InvalidDataException"/>.
    /// </summary>
    public static class TileReader
    {
        /// <summary>Parse and bounds-check the header and chunk table (no CRC check, no chunk decoding).</summary>
        public static TileData ReadHeader(byte[] blob)
        {
            return ReadHeader(blob, 0, blob.Length);
        }

        public static TileData ReadHeader(byte[] blob, int offset, int count)
        {
            if (count < Ght.HeaderSize) throw new InvalidDataException("tile shorter than its header");
            var r = new BinReader(blob, offset, count);
            if (r.U32() != Ght.Magic) throw new InvalidDataException("bad tile magic");
            ushort version = r.U16();
            if (version != Ght.Version) throw new InvalidDataException("unsupported GHT1 version " + version);
            ushort flags = r.U16();
            byte level = r.U8();
            r.Skip(3);
            uint tx = r.U32(), ty = r.U32(), dataVersion = r.U32();
            ushort chunkCount = r.U16();
            r.Skip(2);
            uint crc = r.U32();
            long payloadStart = Ght.HeaderSize + (long)Ght.ChunkEntrySize * chunkCount;
            if (payloadStart > count) throw new InvalidDataException("chunk table runs past the end of the tile");
            var chunks = new ChunkInfo[chunkCount];
            for (int k = 0; k < chunkCount; k++)
            {
                var c = new ChunkInfo { FourCC = r.U32(), Codec = r.U8() };
                r.Skip(3);
                c.Offset = r.U32();
                c.StoredSize = r.U32();
                if (c.Offset < payloadStart || (long)c.Offset + c.StoredSize > count)
                    throw new InvalidDataException("chunk " + c.FourCCString + " outside the tile");
                chunks[k] = c;
            }
            if (level > TileId.MaxLevel || tx >= 1u << level || ty >= 1u << level)
                throw new InvalidDataException("bad tile address " + level + "/" + tx + "/" + ty);
            return new TileData
            {
                Tile = new TileId(level, (int)tx, (int)ty), Version = version, Flags = flags,
                DataVersion = dataVersion, PayloadCrc32 = crc, Chunks = chunks,
            };
        }

        public static TileData Decode(byte[] blob, bool verifyCrc = true)
        {
            return Decode(blob, 0, blob.Length, verifyCrc);
        }

        /// <summary>Decode the tile in <c>blob[offset .. offset + count)</c>.</summary>
        public static TileData Decode(byte[] blob, int offset, int count, bool verifyCrc = true)
        {
            TileData td = ReadHeader(blob, offset, count);
            int payloadStart = Ght.HeaderSize + Ght.ChunkEntrySize * td.Chunks.Length;
            if (verifyCrc && Hashes.Crc32(blob, offset + payloadStart, count - payloadStart) != td.PayloadCrc32)
                throw new InvalidDataException("tile payload CRC mismatch");
            var seen = new HashSet<uint>();
            foreach (var c in td.Chunks)
                if (!seen.Add(c.FourCC)) throw new InvalidDataException("duplicate chunk fourcc " + c.FourCCString);

            // NAME first: the other chunks validate their name references against it.
            BinReader r = Body(blob, offset, td, Ght.Name);
            if (r != null)
            {
                int n = Count(r);
                for (int k = 0; k < n; k++) td.Names.Add(NameRecord.Read(r));
                Done(r, "NAME");
            }
            int nn = td.Names.Count;

            r = Body(blob, offset, td, Ght.Hght);
            if (r != null)
            {
                ReadHeights(r, td);
                Done(r, "HGHT");
            }
            r = Body(blob, offset, td, Ght.Biom);
            if (r != null)
            {
                int n = GridSize(r);
                var b = new Biome[n * n];
                for (int k = 0; k < b.Length; k++) b[k] = (Biome)r.U8();
                td.BiomesN = n;
                td.Biomes = b;
                Done(r, "BIOM");
            }
            r = Body(blob, offset, td, Ght.Road);
            if (r != null)
            {
                ReadRoads(r, td.Roads, nn);
                Done(r, "ROAD");
            }
            r = Body(blob, offset, td, Ght.Line);
            if (r != null)
            {
                ReadLines(r, td.Lines, nn);
                Done(r, "LINE");
            }
            r = Body(blob, offset, td, Ght.Bldg);
            if (r != null)
            {
                ReadBuildings(r, td.Buildings, nn);
                Done(r, "BLDG");
            }
            r = Body(blob, offset, td, Ght.Area);
            if (r != null)
            {
                ReadAreas(r, td.Areas, nn);
                Done(r, "AREA");
            }
            r = Body(blob, offset, td, Ght.Pois);
            if (r != null)
            {
                ReadPois(r, td.Pois, nn);
                Done(r, "POIS");
            }
            r = Body(blob, offset, td, Ght.Seed);
            if (r != null)
            {
                td.HasSeed = true;
                td.TileSeed = r.U64();
                td.ScatterRuleset = r.U16();
                Done(r, "SEED");
            }
            r = Body(blob, offset, td, Ght.Meta);
            if (r != null)
            {
                td.MetaJson = r.Str();
                Done(r, "META");
            }
            return td;
        }

        /// <summary>The decompressed body (after the u32 raw_size) of a chunk, or null when absent.</summary>
        public static byte[] ChunkBody(byte[] blob, int offset, ChunkInfo info)
        {
            byte[] raw;
            int start = offset + (int)info.Offset;
            int size = (int)info.StoredSize;
            if (info.Codec == Ght.CodecStored)
            {
                raw = new byte[size];
                Buffer.BlockCopy(blob, start, raw, 0, size);
            }
            else if (info.Codec == Ght.CodecDeflate)
            {
                raw = Inflate(blob, start, size, info.FourCCString);
            }
            else
            {
                throw new InvalidDataException("chunk " + info.FourCCString + ": unknown codec " + info.Codec);
            }
            if (raw.Length < 4) throw new InvalidDataException("chunk " + info.FourCCString + " too short");
            uint rawSize = BinReader.ReadU32(raw, 0);
            if (rawSize != raw.Length - 4)
                throw new InvalidDataException("chunk " + info.FourCCString + ": raw_size " + rawSize + " != " + (raw.Length - 4));
            var body = new byte[raw.Length - 4];
            Buffer.BlockCopy(raw, 4, body, 0, body.Length);
            return body;
        }

        private static byte[] Inflate(byte[] blob, int start, int size, string what)
        {
            const int limit = Ght.MaxChunkRawSize + 4;
            try
            {
                using (var input = new MemoryStream(blob, start, size, false))
                using (var z = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream(Math.Min(limit, Math.Max(256, size * 4))))
                {
                    var buf = new byte[16384];
                    int n;
                    while ((n = z.Read(buf, 0, buf.Length)) > 0)
                    {
                        if (output.Length + n > limit)
                            throw new InvalidDataException("chunk " + what + " exceeds the maximum raw size");
                        output.Write(buf, 0, n);
                    }
                    return output.ToArray();
                }
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                throw new InvalidDataException("chunk " + what + ": bad DEFLATE stream", e);
            }
        }

        private static BinReader Body(byte[] blob, int offset, TileData td, uint fourcc)
        {
            foreach (var c in td.Chunks)
                if (c.FourCC == fourcc) return new BinReader(ChunkBody(blob, offset, c));
            return null;
        }

        private static void Done(BinReader r, string what)
        {
            if (r.Remaining != 0) throw new InvalidDataException(r.Remaining + " trailing bytes in " + what + " chunk");
        }

        private static int Count(BinReader r)
        {
            ulong n = r.Varint();
            if (n > (ulong)r.Remaining) throw new InvalidDataException("count " + n + " exceeds the " + r.Remaining + " bytes left");
            return (int)n;
        }

        private static int NameRef(BinReader r, int nNames)
        {
            ulong v = r.Varint();
            if (v > (ulong)nNames) throw new InvalidDataException("name_ref " + v + " outside the name table (" + nNames + " entries)");
            return (int)v;
        }

        private static int GridSize(BinReader r)
        {
            int n = r.U16();
            r.U16();
            if (n < 2 || ((n - 1) & (n - 2)) != 0) throw new InvalidDataException("grid size " + n + " is not 2^k + 1");
            return n;
        }

        private static void ReadHeights(BinReader r, TileData td)
        {
            int n = GridSize(r);
            float hMin = r.F32(), hStep = r.F32();
            if (hMin != Ght.HeightMinM || hStep != Ght.HeightStepM)
                throw new InvalidDataException("unsupported height quantisation (" + hMin + ", " + hStep + ")");
            var q = new ushort[n * n];
            ushort colStart = 0;
            for (int j = 0; j < n; j++)
            {
                // pred = q[j][i-1] for i > 0, q[j-1][0] for i == 0 (0 for the first sample); mod 65536
                colStart = unchecked((ushort)(colStart + r.U16()));
                ushort prev = colStart;
                q[j * n] = prev;
                for (int i = 1; i < n; i++)
                {
                    prev = unchecked((ushort)(prev + r.U16()));
                    q[j * n + i] = prev;
                }
            }
            td.HeightsN = n;
            td.HeightsQ = q;
        }

        private static void CheckContextPoints(int n, bool prev, bool next, string what)
        {
            int need = 2 + (prev ? 1 : 0) + (next ? 1 : 0);
            if (n < need) throw new InvalidDataException(what + ": " + n + " points, need at least " + need);
        }

        /// <summary>n delta-coded points; the first relative to the tile origin. Accumulates in long.</summary>
        private static int[] ReadPoints(BinReader r, int n)
        {
            if (n > r.Remaining) throw new InvalidDataException(n + " points cannot fit in " + r.Remaining + " bytes");
            var pts = new int[2 * n];
            long x = 0, z = 0;
            for (int k = 0; k < n; k++)
            {
                x += r.Svarint();
                z += r.Svarint();
                if (x < int.MinValue || x > int.MaxValue || z < int.MinValue || z > int.MaxValue)
                    throw new InvalidDataException("local coordinate outside the i32 range");
                pts[2 * k] = (int)x;
                pts[2 * k + 1] = (int)z;
            }
            return pts;
        }

        private static void ReadRoads(BinReader r, List<RoadRecord> outList, int nn)
        {
            int count = Count(r);
            for (int k = 0; k < count; k++)
            {
                var rd = new RoadRecord
                {
                    OsmWayId = r.Varint(), RoadClass = (RoadClass)r.U8(), Surface = (Surface)r.U8(),
                    SurfaceSource = (SurfaceSource)r.U8(), Flags = (RoadFlags)r.U8(), Lanes = r.U8(),
                    SacScale = (SacScale)r.U8(), TrailVisibility = r.U8(), Layer = r.I8(), WidthCm = r.Varint(),
                    Access = (Travel)r.U8(),
                };
                rd.NameRef = NameRef(r, nn);
                rd.RefRef = NameRef(r, nn);
                int n = r.VarintInt();
                CheckContextPoints(n, rd.HasPrevContext, rd.HasNextContext, "road");
                rd.Points = ReadPoints(r, n);
                outList.Add(rd);
            }
        }

        private static void ReadLines(BinReader r, List<LineRecord> outList, int nn)
        {
            int count = Count(r);
            for (int k = 0; k < count; k++)
            {
                var ln = new LineRecord
                {
                    OsmWayId = r.Varint(), Kind = (LineKind)r.U8(), Flags = (LineFlags)r.U8(), WidthCm = r.Varint(),
                };
                ln.NameRef = NameRef(r, nn);
                int n = r.VarintInt();
                CheckContextPoints(n, (ln.Flags & LineFlags.HasPrevCtx) != 0, (ln.Flags & LineFlags.HasNextCtx) != 0, "line");
                ln.Points = ReadPoints(r, n);
                outList.Add(ln);
            }
        }

        private static void ReadBuildings(BinReader r, List<BuildingRecord> outList, int nn)
        {
            int count = Count(r);
            for (int k = 0; k < count; k++)
            {
                var b = new BuildingRecord
                {
                    OsmRef = r.Varint(), Archetype = (BuildingArchetype)r.U8(), Use = (BuildingUse)r.U8(),
                    Levels = r.U8(), Flags = (BuildingFlags)r.U8(), HeightCm = r.Varint(), MinHeightCm = r.Varint(),
                    RoofShape = (RoofShape)r.U8(), RoofMaterial = (RoofMaterial)r.U8(),
                    WallMaterial = (WallMaterial)r.U8(), Seed = r.U32(),
                };
                b.NameRef = NameRef(r, nn);
                int rings = Count(r);
                if (rings < 1) throw new InvalidDataException("building without rings");
                b.Rings = new int[rings][];
                for (int i = 0; i < rings; i++)
                {
                    int n = r.VarintInt();
                    if (n < 3) throw new InvalidDataException("building ring with " + n + " points");
                    b.Rings[i] = ReadPoints(r, n);
                }
                outList.Add(b);
            }
        }

        private static void ReadAreas(BinReader r, List<AreaRecord> outList, int nn)
        {
            int count = Count(r);
            for (int k = 0; k < count; k++)
            {
                var a = new AreaRecord { OsmRef = r.Varint(), Kind = (AreaKind)r.U8(), Flags = (AreaFlags)r.U8() };
                a.NameRef = NameRef(r, nn);
                int nv = Count(r);
                a.Vertices = ReadPoints(r, nv);
                int ni = Count(r);
                if (ni % 3 != 0) throw new InvalidDataException("area index count " + ni + " is not a multiple of 3");
                a.Indices = new int[ni];
                for (int i = 0; i < ni; i++)
                {
                    ulong idx = r.Varint();
                    if (idx >= (ulong)nv) throw new InvalidDataException("area triangle index out of range");
                    a.Indices[i] = (int)idx;
                }
                int nr = Count(r);
                a.Rings = new int[2 * nr];
                for (int i = 0; i < nr; i++)
                {
                    ulong s = r.Varint(), n = r.Varint();
                    if (n < 1 || s + n > (ulong)nv || s + n < s)
                        throw new InvalidDataException("area ring (" + s + ", " + n + ") outside " + nv + " vertices");
                    a.Rings[2 * i] = (int)s;
                    a.Rings[2 * i + 1] = (int)n;
                }
                outList.Add(a);
            }
        }

        private static int SvarintInt(BinReader r)
        {
            long v = r.Svarint();
            if (v < int.MinValue || v > int.MaxValue) throw new InvalidDataException("value " + v + " outside the i32 range");
            return (int)v;
        }

        private static void ReadPois(BinReader r, List<PoiRecord> outList, int nn)
        {
            int count = Count(r);
            for (int k = 0; k < count; k++)
            {
                var p = new PoiRecord
                {
                    OsmRef = r.Varint(), Kind = (PoiKind)r.U16(), Flags = (PoiFlags)r.U8(), Importance = r.U8(),
                    XCm = SvarintInt(r), ZCm = SvarintInt(r), EleDm = SvarintInt(r),
                };
                p.NameRef = NameRef(r, nn);
                p.SearchId = r.Varint();
                outList.Add(p);
            }
        }
    }
}
