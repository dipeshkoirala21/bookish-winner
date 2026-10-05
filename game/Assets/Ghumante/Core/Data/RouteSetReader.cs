using System;
using System.Collections.Generic;
using System.IO;

namespace Ghumante.Core.Data
{
    /// <summary>One stop of a transit route (DATA_FORMATS 6). Position in absolute game metres.</summary>
    public struct TransitStop
    {
        /// <summary><c>(osm_id &lt;&lt; 2) | type</c>, 0 = none.</summary>
        public ulong OsmRef;

        public StopFlags Flags;
        public double X, Z;

        /// <summary>Distance along the route in metres.</summary>
        public float AlongM;

        /// <summary>The stop's name, or null.</summary>
        public NameRecord Name;
    }

    /// <summary>One bus, micro, tempo, share-taxi, hiking or cycling route (D2 + D22). <see cref="WayIds"/> are in
    /// travel order with <see cref="WayForward"/> true when the way is travelled in its OSM node order.</summary>
    public sealed class TransitRoute
    {
        public long OsmRelationId;
        public string Id, Ref;
        public TransitMode Mode;
        public LiveryClass Livery;
        public RouteFlags Flags;
        public NameRecord Name, From, To;
        public long[] WayIds;
        public bool[] WayForward;
        public TransitStop[] Stops;
        public float HeadwayPeakS, HeadwayOffS;
        public float LengthM;
    }

    /// <summary>A <c>type=restriction</c> relation (DATA_FORMATS 6); via is a node or (when <see cref="ViaNode"/> is 0) a way.</summary>
    public struct TurnRestrictionRecord
    {
        public long OsmRelationId;
        public TurnRestriction Kind;
        public long FromWay, ViaNode, ViaWay, ToWay;
    }

    /// <summary>
    /// A region's transit routes and turn restrictions: the decoded <c>.ghrt</c> file (magic <c>GHRT</c>, DATA_FORMATS 6).
    /// <see cref="Read"/> checks the header, the payload size and CRC-32, and every reference; corruption throws
    /// <see cref="InvalidDataException"/>.
    /// </summary>
    public sealed class RouteSet
    {
        public const ushort Version = 1;
        public const int HeaderSize = 32;
        public static readonly uint Magic = Ght.FourCC("GHRT");

        private readonly List<TransitRoute> _routes = new List<TransitRoute>();
        private readonly List<TurnRestrictionRecord> _restrictions = new List<TurnRestrictionRecord>();

        public IReadOnlyList<TransitRoute> Routes
        {
            get { return _routes; }
        }

        public IReadOnlyList<TurnRestrictionRecord> Restrictions
        {
            get { return _restrictions; }
        }

        public static RouteSet Read(byte[] ghrt)
        {
            if (ghrt == null) throw new ArgumentNullException(nameof(ghrt));
            if (ghrt.Length < HeaderSize) throw new InvalidDataException("route file shorter than its header");
            var h = new BinReader(ghrt, 0, HeaderSize);
            if (h.U32() != Magic) throw new InvalidDataException("bad route file magic");
            ushort version = h.U16();
            if (version != Version) throw new InvalidDataException("unsupported GHRT version " + version);
            h.U16();
            uint routeCount = h.U32(), restrictionCount = h.U32(), nameCount = h.U32(), payloadBytes = h.U32(), crc = h.U32();
            if ((long)HeaderSize + payloadBytes != ghrt.Length) throw new InvalidDataException("route file payload size mismatch");
            if (Hashes.Crc32(ghrt, HeaderSize, (int)payloadBytes) != crc) throw new InvalidDataException("route file CRC mismatch");
            var r = new BinReader(ghrt, HeaderSize, (int)payloadBytes);
            ulong nn = r.Varint();
            if (nn != nameCount || nn > (ulong)r.Remaining) throw new InvalidDataException("route name count mismatch");
            var names = new NameRecord[(int)nn];
            for (int i = 0; i < names.Length; i++) names[i] = NameRecord.Read(r);
            var set = new RouteSet();
            if (routeCount > (uint)r.Remaining || restrictionCount > (uint)r.Remaining) throw new InvalidDataException("route counts exceed the payload");
            for (uint k = 0; k < routeCount; k++) set._routes.Add(ReadRoute(r, names));
            for (uint k = 0; k < restrictionCount; k++)
            {
                set._restrictions.Add(new TurnRestrictionRecord
                {
                    OsmRelationId = Long(r), Kind = (TurnRestriction)r.U8(), FromWay = Long(r), ViaNode = Long(r), ViaWay = Long(r), ToWay = Long(r),
                });
            }
            if (r.Remaining != 0) throw new InvalidDataException(r.Remaining + " trailing bytes in the route file");
            return set;
        }

        private static long Long(BinReader r)
        {
            ulong v = r.Varint();
            if (v > long.MaxValue) throw new InvalidDataException("route id outside the i64 range");
            return (long)v;
        }

        private static NameRecord Name(BinReader r, NameRecord[] names)
        {
            ulong v = r.Varint();
            if (v > (ulong)names.Length) throw new InvalidDataException("route name_ref " + v + " outside the name table");
            return v == 0 ? null : names[(int)v - 1];
        }

        private static int Count(BinReader r)
        {
            ulong n = r.Varint();
            if (n > (ulong)r.Remaining) throw new InvalidDataException("count " + n + " exceeds the " + r.Remaining + " bytes left");
            return (int)n;
        }

        private static TransitRoute ReadRoute(BinReader r, NameRecord[] names)
        {
            var t = new TransitRoute { OsmRelationId = Long(r), Id = r.Str() };
            t.Mode = (TransitMode)r.U8();
            t.Livery = (LiveryClass)r.U8();
            t.Flags = (RouteFlags)r.U8();
            t.Name = Name(r, names);
            NameRecord rf = Name(r, names);
            t.Ref = rf == null ? "" : rf.Default;
            t.From = Name(r, names);
            t.To = Name(r, names);
            t.HeadwayPeakS = r.U16();
            t.HeadwayOffS = r.U16();
            t.LengthM = r.Varint();
            int ways = Count(r);
            t.WayIds = new long[ways];
            t.WayForward = new bool[ways];
            for (int i = 0; i < ways; i++)
            {
                ulong v = r.Varint();
                t.WayIds[i] = (long)(v >> 1);
                t.WayForward[i] = (v & 1) != 0;
            }
            int stops = Count(r);
            t.Stops = new TransitStop[stops];
            for (int i = 0; i < stops; i++)
            {
                var s = new TransitStop { OsmRef = r.Varint(), Flags = (StopFlags)r.U8() };
                s.X = r.Svarint() / 10.0;
                s.Z = r.Svarint() / 10.0;
                s.AlongM = r.Varint() / 10f;
                s.Name = Name(r, names);
                t.Stops[i] = s;
            }
            return t;
        }
    }
}
