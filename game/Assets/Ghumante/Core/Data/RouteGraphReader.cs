using System;
using System.IO;

namespace Ghumante.Core.Data
{
    /// <summary>
    /// A decoded GHRG routing graph (DATA_FORMATS.md section 4; routing.RoutingGraph) in CSR form: the edges
    /// of node <c>v</c> are <c>Offsets[v] .. Offsets[v + 1]</c>. Arrays are shared, treat them as read-only.
    /// </summary>
    public sealed class RouteGraph
    {
        public const short ElevUnknown = short.MinValue;
        public const byte EdgeBridge = 1;
        public const byte EdgeTunnel = 2;
        public const byte EdgeFord = 4;
        public const byte EdgeLink = 8;

        public ushort Flags;
        public int[] NodeXDm;
        public int[] NodeZDm;
        public short[] NodeElev;
        public int[] Offsets;

        public int[] EdgeTarget;
        public uint[] EdgeLengthDm;
        public RoadClass[] EdgeClass;
        public Surface[] EdgeSurface;
        public Travel[] EdgeAccess;
        public byte[] EdgeFlags;
        public SacScale[] EdgeSac;
        public short[] EdgeClimb;
        public uint[] EdgeGeomOffset;
        public ushort[] EdgeName;
        public ushort[] EdgeGeomCount;

        public byte[] Geometry;
        public NameRecord[] Names;

        private int[] _edgeSource;

        public int NodeCount
        {
            get { return NodeXDm.Length; }
        }

        public int EdgeCount
        {
            get { return EdgeTarget.Length; }
        }

        /// <summary>Source node of every edge (derived from <see cref="Offsets"/>, built once).</summary>
        public int[] EdgeSource
        {
            get
            {
                if (_edgeSource == null)
                {
                    var src = new int[EdgeCount];
                    for (int v = 0; v < NodeCount; v++)
                        for (int e = Offsets[v]; e < Offsets[v + 1]; e++) src[e] = v;
                    _edgeSource = src;
                }
                return _edgeSource;
            }
        }

        public double NodeX(int v)
        {
            return NodeXDm[v] / 10.0;
        }

        public double NodeZ(int v)
        {
            return NodeZDm[v] / 10.0;
        }

        public double EdgeLengthM(int e)
        {
            return EdgeLengthDm[e] / 10.0;
        }

        public NameRecord EdgeNameRecord(int e)
        {
            int k = EdgeName[e];
            return k == 0 ? null : Names[k - 1];
        }

        /// <summary>Edge polyline in decimetres, interleaved {x0, z0, x1, z1, ...}, from source to target.</summary>
        public long[] EdgeGeometryDm(int e)
        {
            int n = EdgeGeomCount[e];
            var r = new BinReader(Geometry, (int)EdgeGeomOffset[e], Geometry.Length - (int)EdgeGeomOffset[e]);
            int v = EdgeSource[e];
            long x = NodeXDm[v], z = NodeZDm[v];
            var pts = new long[2 * n];
            for (int i = 0; i < n; i++)
            {
                x += r.Svarint();
                z += r.Svarint();
                pts[2 * i] = x;
                pts[2 * i + 1] = z;
            }
            return pts;
        }

        /// <summary>Edge polyline in game metres, interleaved.</summary>
        public double[] EdgeGeometry(int e)
        {
            long[] dm = EdgeGeometryDm(e);
            var m = new double[dm.Length];
            for (int i = 0; i < dm.Length; i++) m[i] = dm[i] / 10.0;
            return m;
        }
    }

    /// <summary>Decoder for GHRG files, the C# twin of <c>routing.decode_graph</c>.</summary>
    public static class RouteGraphReader
    {
        public const ushort Version = 1;
        public const int HeaderSize = 32;
        public const int NodeSize = 12;
        public const int EdgeSize = 24;
        public static readonly uint Magic = Ght.FourCC("GHRG");

        public static RouteGraph Read(byte[] data)
        {
            if (data.Length < HeaderSize) throw new InvalidDataException("GHRG blob shorter than its header");
            var r = new BinReader(data);
            if (r.U32() != Magic) throw new InvalidDataException("bad GHRG magic");
            ushort version = r.U16();
            if (version != Version) throw new InvalidDataException("unsupported GHRG version " + version);
            ushort flags = r.U16();
            uint n = r.U32(), e = r.U32(), gb = r.U32(), nc = r.U32();
            r.U64();
            long need = HeaderSize + (long)n * NodeSize + ((long)n + 1) * 4 + (long)e * EdgeSize + gb;
            if (data.Length < need)
                throw new InvalidDataException("GHRG blob truncated: " + data.Length + " bytes, sections need " + need);

            var g = new RouteGraph
            {
                Flags = flags, NodeXDm = new int[n], NodeZDm = new int[n], NodeElev = new short[n],
                Offsets = new int[n + 1], EdgeTarget = new int[e], EdgeLengthDm = new uint[e],
                EdgeClass = new RoadClass[e], EdgeSurface = new Surface[e], EdgeAccess = new Travel[e],
                EdgeFlags = new byte[e], EdgeSac = new SacScale[e], EdgeClimb = new short[e],
                EdgeGeomOffset = new uint[e], EdgeName = new ushort[e], EdgeGeomCount = new ushort[e],
            };
            for (int i = 0; i < n; i++)
            {
                g.NodeXDm[i] = r.I32();
                g.NodeZDm[i] = r.I32();
                g.NodeElev[i] = r.I16();
                r.U16();
            }
            for (int i = 0; i <= n; i++)
            {
                uint o = r.U32();
                if (o > e) throw new InvalidDataException("offsets are not a valid CSR index");
                g.Offsets[i] = (int)o;
            }
            if (g.Offsets[0] != 0 || g.Offsets[n] != e) throw new InvalidDataException("offsets are not a valid CSR index");
            for (int i = 0; i < n; i++)
                if (g.Offsets[i + 1] < g.Offsets[i]) throw new InvalidDataException("offsets are not a valid CSR index");
            for (int i = 0; i < e; i++)
            {
                uint t = r.U32();
                if (t >= n) throw new InvalidDataException("edge target out of range");
                g.EdgeTarget[i] = (int)t;
                g.EdgeLengthDm[i] = r.U32();
                g.EdgeClass[i] = (RoadClass)r.U8();
                g.EdgeSurface[i] = (Surface)r.U8();
                g.EdgeAccess[i] = (Travel)r.U8();
                g.EdgeFlags[i] = r.U8();
                g.EdgeSac[i] = (SacScale)r.U8();
                r.U8();
                g.EdgeClimb[i] = r.I16();
                g.EdgeGeomOffset[i] = r.U32();
                g.EdgeName[i] = r.U16();
                g.EdgeGeomCount[i] = r.U16();
                if (g.EdgeName[i] > nc) throw new InvalidDataException("edge name index out of range");
                if (g.EdgeGeomCount[i] < 2 || g.EdgeGeomOffset[i] >= gb) throw new InvalidDataException("edge geometry out of range");
            }
            g.Geometry = r.Bytes((int)gb);
            if (nc > (ulong)r.Remaining) throw new InvalidDataException("GHRG name count too large");
            g.Names = new NameRecord[nc];
            for (int i = 0; i < nc; i++) g.Names[i] = NameRecord.Read(r);
            if (r.Remaining != 0) throw new InvalidDataException(r.Remaining + " trailing bytes after the names section");
            return g;
        }
    }
}
