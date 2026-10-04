using System;
using System.IO;

namespace Ghumante.Core.Data
{
    /// <summary>One GHSI entry (DATA_FORMATS.md section 3).</summary>
    public sealed class SearchEntry
    {
        public const int PlaceKindOffset = 1000;
        public const byte FlagDiscoverable = 1;
        public const byte FlagLandmark = 2;
        public const byte FlagTransportHub = 4;

        public NameRecord Name;

        /// <summary><see cref="PoiKind"/> value, or <see cref="PlaceKind"/> value + 1000 for places.</summary>
        public int Kind;

        public byte Importance;
        public byte Flags;
        public int XDm;
        public int ZDm;
        public int LonE7;
        public int LatE7;

        /// <summary><c>(osm_id &lt;&lt; 2) | type</c>, truncated to 32 bits (diagnostics only).</summary>
        public uint OsmRef;

        public NameRecord District;
        public NameRecord Province;

        public bool IsPlace
        {
            get { return Kind >= PlaceKindOffset; }
        }

        public PlaceKind PlaceKind
        {
            get { return IsPlace ? (PlaceKind)(Kind - PlaceKindOffset) : PlaceKind.None; }
        }

        public PoiKind PoiKind
        {
            get { return IsPlace ? PoiKind.None : (PoiKind)Kind; }
        }

        public double X
        {
            get { return XDm / 10.0; }
        }

        public double Z
        {
            get { return ZDm / 10.0; }
        }

        public double Lon
        {
            get { return LonE7 / 1e7; }
        }

        public double Lat
        {
            get { return LatE7 / 1e7; }
        }

        public bool Discoverable
        {
            get { return (Flags & FlagDiscoverable) != 0; }
        }

        public bool Landmark
        {
            get { return (Flags & FlagLandmark) != 0; }
        }

        public bool TransportHub
        {
            get { return (Flags & FlagTransportHub) != 0; }
        }

        /// <summary>The name shown in English UI (search_index.SearchEntry.display_name).</summary>
        public string DisplayName
        {
            get { return Name.En.Length > 0 ? Name.En : Name.Default.Length > 0 ? Name.Default : Name.Ne; }
        }
    }

    /// <summary>A decoded GHSI search index: entries, the names section and the sorted key table.</summary>
    public sealed class SearchIndexData
    {
        public ushort Flags;
        public NameRecord[] Names;
        public SearchEntry[] Entries;

        /// <summary>Folded keys, sorted by ordinal (ASCII byte) order, then entry index.</summary>
        public string[] Keys;

        public int[] KeyEntries;
    }

    /// <summary>Decoder for GHSI files, the C# twin of <c>search_index.decode_index</c>.</summary>
    public static class SearchIndexReader
    {
        public const ushort Version = 1;
        public const int HeaderSize = 32;
        public const int EntrySize = 32;
        public static readonly uint Magic = Ght.FourCC("GHSI");

        public static SearchIndexData Read(byte[] data)
        {
            if (data.Length < HeaderSize) throw new InvalidDataException("GHSI blob shorter than its header");
            var r = new BinReader(data);
            if (r.U32() != Magic) throw new InvalidDataException("not a GHSI file (bad magic)");
            ushort version = r.U16();
            if (version != Version) throw new InvalidDataException("unsupported GHSI version " + version);
            ushort flags = r.U16();
            uint entryCount = r.U32(), keyCount = r.U32();
            uint namesOff = r.U32(), entriesOff = r.U32(), keysOff = r.U32();
            r.U32();
            if (!(HeaderSize <= namesOff && namesOff <= entriesOff && entriesOff <= keysOff && keysOff <= data.Length)
                || keysOff - entriesOff != (ulong)entryCount * EntrySize)
                throw new InvalidDataException("GHSI section offsets are inconsistent");

            r = new BinReader(data, (int)namesOff, (int)(entriesOff - namesOff));
            ulong nNames = r.Varint();
            if (nNames > (ulong)r.Remaining) throw new InvalidDataException("GHSI names count too large");
            var names = new NameRecord[nNames];
            for (int i = 0; i < names.Length; i++) names[i] = NameRecord.Read(r);
            if (r.Remaining != 0) throw new InvalidDataException("GHSI names section has trailing bytes");

            r = new BinReader(data, (int)entriesOff, (int)(keysOff - entriesOff));
            var entries = new SearchEntry[entryCount];
            for (int i = 0; i < entries.Length; i++)
            {
                uint ni = r.U32();
                var e = new SearchEntry
                {
                    Kind = r.U16(), Importance = r.U8(), Flags = r.U8(), XDm = r.I32(), ZDm = r.I32(),
                    LonE7 = r.I32(), LatE7 = r.I32(), OsmRef = r.U32(),
                };
                ushort di = r.U16(), pi = r.U16();
                if (ni >= names.Length) throw new InvalidDataException("entry name index " + ni + " out of range");
                e.Name = names[ni];
                e.District = Ref(names, di);
                e.Province = Ref(names, pi);
                entries[i] = e;
            }

            r = new BinReader(data, (int)keysOff, data.Length - (int)keysOff);
            if (keyCount > (ulong)r.Remaining) throw new InvalidDataException("GHSI key count too large");
            var keys = new string[keyCount];
            var keyEntries = new int[keyCount];
            for (int i = 0; i < keys.Length; i++)
            {
                string k = r.Str();
                ulong e = r.Varint();
                if (e >= entryCount) throw new InvalidDataException("key entry index " + e + " out of range");
                if (i > 0)
                {
                    int c = CompareUtf8(k, keys[i - 1]);
                    if (c < 0 || c == 0 && (int)e < keyEntries[i - 1]) throw new InvalidDataException("GHSI keys are not sorted");
                }
                keys[i] = k;
                keyEntries[i] = (int)e;
            }
            if (r.Remaining != 0) throw new InvalidDataException("GHSI has trailing bytes after the keys section");
            return new SearchIndexData { Flags = flags, Names = names, Entries = entries, Keys = keys, KeyEntries = keyEntries };
        }

        private static NameRecord Ref(NameRecord[] names, int i)
        {
            if (i == 0) return null;
            if (i > names.Length) throw new InvalidDataException("name reference " + i + " out of range");
            return names[i - 1];
        }

        /// <summary>UTF-8 byte order of two strings (code point order; ordinal UTF-16 order differs only
        /// above U+FFFF, so surrogates are handled explicitly).</summary>
        public static int CompareUtf8(string a, string b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                char ca = a[i], cb = b[i];
                if (ca == cb) continue;
                bool sa = char.IsSurrogate(ca), sb = char.IsSurrogate(cb);
                if (sa != sb) return sa ? 1 : -1;
                return ca < cb ? -1 : 1;
            }
            return a.Length.CompareTo(b.Length);
        }
    }
}
