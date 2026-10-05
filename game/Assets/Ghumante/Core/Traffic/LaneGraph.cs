using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Traffic
{
    /// <summary>What a lane is.</summary>
    public enum LaneKind : byte
    {
        /// <summary>A lane along a road piece between two nodes.</summary>
        Road = 0,

        /// <summary>A cubic Bézier through a junction (or across a tile seam) from a lane end to a lane start.</summary>
        Connector = 1,

        /// <summary>A segment of a synthesised roundabout ring (clockwise).</summary>
        Ring = 2,

        /// <summary>The turn at a dead end, from the lane in to the lane out.</summary>
        UTurn = 3,
    }

    /// <summary>How a node's traffic is controlled (W2_DESIGN 5.1).</summary>
    public enum ControllerKind : byte
    {
        None = 0,
        Priority = 1,
        Police = 2,
        Signal = 3,
        Roundabout = 4,
    }

    /// <summary>A read-only view of one lane (W2_DESIGN 10.3): centreline polyline in game metres with heights, arc length
    /// at each point, width, speed targets, the classes that may use it and its junction controller.</summary>
    public struct LaneInfo
    {
        public LaneId Id;
        public LaneKind Kind;
        public double[] X, Z;
        public float[] Y, S;
        public int PointCount;
        public float LengthM, WidthM;

        /// <summary>Target speeds (km/h) at free flow and at peak.</summary>
        public float FreeKmh, PeakKmh;

        /// <summary><see cref="VehicleClasses"/> bit mask of the classes allowed.</summary>
        public uint ClassMask;

        /// <summary>The controller at the lane's end node (-1 none) and the lane's approach (arm) index there.</summary>
        public int ControllerId, Approach;

        public long WayId;
        public RoadClass RoadClass;
        public AreaType Area;

        /// <summary>Lane index from the kerb (0 = leftmost in left-hand traffic) and the lanes in this direction.</summary>
        public byte Index, Count;

        /// <summary>A two-way road with one shared lane: <see cref="Twin"/> is the lane of the other direction on the
        /// same centreline; agents pass head-on by both shifting left.</summary>
        public bool Shared;

        public LaneId Twin;

        /// <summary>Lane of a roundabout ring (mapped ring way or synthesised ring), always clockwise.</summary>
        public bool Ring;

        /// <summary>Signed lateral offset of the lane from the road centreline at its middle, positive right of the way's
        /// point order (left-hand traffic: forward lanes are negative).</summary>
        public float OffsetM;

        /// <summary>True when the lane runs in the way's point order.</summary>
        public bool Forward;

        /// <summary>A lane of a two-way road (left-hand traffic puts it left of the centreline).</summary>
        public bool TwoWay;

        public int SuccessorCount, PredecessorCount;
    }

    /// <summary>
    /// The left-hand-traffic lane graph (W2_DESIGN 5.1), built incrementally per resident level-10 tile from ROAD, RATR
    /// and JNCT through the shared <see cref="RoadLayout"/> (so lanes sit inside the drawn carriageway), and stitched at
    /// tile seams through the pieces' cut ends. Only motor-legal pieces with a game width of at least 1.8 m carry lanes,
    /// with class masks from <see cref="RoadWidthModel.AccessFor"/>; lanes inside sacred zones are dropped
    /// (SACRED_NO_VEHICLE). Lane k (0 = kerb) of a two-way road lies left of the travel direction; one-way carriageways
    /// spread their lanes over the width; two-way roads under 5.5 m real carry one shared lane per direction on the
    /// centreline. Junction nodes get Bézier connectors (0.4 × chord handles) with conflict sets, a controller (police,
    /// signal, roundabout or priority) and clockwise rings where JNCT records a roundabout without ring ways.
    /// <para>Thread safety: <see cref="AddTile"/> and <see cref="RemoveTile"/> build outside the lock and splice under
    /// <see cref="SyncRoot"/>; simulations hold <see cref="SyncRoot"/> for a step. Lane ids stay valid while their tile
    /// is resident; a reused id has a new <see cref="Generation"/>.</para>
    /// </summary>
    public sealed class LaneGraph
    {
        public const double GridCellM = 64.0;
        public const float MinLaneWidthM = 1.8f;
        public const float DensifyM = 8f;
        public const int ConnectorPoints = 7;

        /// <summary>Lock held by <see cref="TrafficSim.Step"/> and the splice of <see cref="AddTile"/>.</summary>
        public readonly object SyncRoot = new object();

        internal readonly List<Lane> Lanes = new List<Lane>();
        private readonly Stack<int> _free = new Stack<int>();
        private readonly Dictionary<long, Node> _nodes = new Dictionary<long, Node>();
        private readonly Dictionary<ulong, TileLanes> _tiles = new Dictionary<ulong, TileLanes>();
        private readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _byWay = new Dictionary<long, List<int>>();
        internal readonly List<Controller> Controllers = new List<Controller>();
        private readonly Stack<int> _freeControllers = new Stack<int>();
        private readonly Dictionary<long, TurnRestrictionRecord[]> _restrictions = new Dictionary<long, TurnRestrictionRecord[]>();
        private int _alive;

        /// <summary>Optional: lanes inside these zones are dropped (motor) or kept for bicycles only (heritage squares).</summary>
        public SacredZoneIndex Sacred;

        /// <summary>Incremented whenever lanes are added or removed (route plans rebuild on change).</summary>
        public int Version { get; private set; }

        /// <summary>Number of live lanes (road lanes, connectors and ring segments).</summary>
        public int LaneCount
        {
            get { return _alive; }
        }

        /// <summary>Capacity of the lane id space (ids are below this).</summary>
        public int LaneCapacity
        {
            get { return Lanes.Count; }
        }

        public int NodeCount
        {
            get { return _nodes.Count; }
        }

        public int TileCount
        {
            get { return _tiles.Count; }
        }

        public bool ContainsTile(TileId id)
        {
            lock (SyncRoot) return _tiles.ContainsKey(id.Key);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Internal model
        // ---------------------------------------------------------------------------------------------------------

        internal sealed class Lane
        {
            public int Id, Gen;
            public bool Alive;
            public LaneKind Kind;
            public ulong TileKey;
            public double[] X, Z;
            public float[] Y, S;
            public int N;
            public float Length, Width, FreeKmh, PeakKmh, Offset;
            public uint Mask;
            public int Controller = -1, Approach = -1;
            public int[] Next = Empty, Prev = Empty, Conflicts = Empty;
            public int Twin = -1, Left = -1, Right = -1;
            public byte Index, Count;
            public bool Shared, Ring, Forward, Priority, TwoWay;
            public long WayId;
            public RoadClass Class;
            public AreaType Area;
            public long NodeStart, NodeEnd; // node keys (connectors: both the node)
            public float SweepM, MaxHalfWidth;
            public static readonly int[] Empty = new int[0];
        }

        /// <summary>One end of a road edge at a node.</summary>
        internal sealed class EdgeEnd
        {
            public ulong TileKey;
            public int[] In = Lane.Empty, Out = Lane.Empty; // lanes ending at / starting at the node, by index from the kerb
            public double DirX, DirZ; // unit direction away from the node
            public float Width;
            public int Rank;
            public long WayId;
            public bool Ring, Cut, TwoWay;
        }

        internal sealed class Node
        {
            public long Key;
            public double X, Z;
            public readonly List<EdgeEnd> Ends = new List<EdgeEnd>();
            public readonly List<int> Owned = new List<int>(); // connectors and ring lanes
            public int Controller = -1;
            public JunctionKind Kind;
            public byte JFlags;
            public long OsmNodeId;
            public float RingRadius; // synthesised ring
            public double RingX, RingZ;
            public bool Synthetic;
        }

        private sealed class TileLanes
        {
            public TileId Id;
            public readonly List<int> Lanes = new List<int>();
            public readonly List<KeyValuePair<long, EdgeEnd>> Ends = new List<KeyValuePair<long, EdgeEnd>>();
        }

        /// <summary>A junction controller (W2_DESIGN 5.1): police phases, signal cycles, roundabout yield or priority.</summary>
        internal sealed class Controller
        {
            public int Id;
            public bool Alive;
            public ControllerKind Kind;
            public long NodeKey;
            public double X, Z;
            public int Arms;
            public int[] PhaseOf = Lane.Empty; // per approach (arm) index: its phase
            public int Phases;
            public int[] ArmRank = Lane.Empty;
            public double[] ArmAngle = new double[0];
            public bool Officers24;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Public queries
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>True when <paramref name="l"/> names a live lane of generation <paramref name="gen"/>.</summary>
        public bool IsAlive(LaneId l, int gen)
        {
            if (l.Value < 0 || l.Value >= Lanes.Count) return false;
            Lane x = Lanes[l.Value];
            return x.Alive && x.Gen == gen;
        }

        public bool IsAlive(LaneId l)
        {
            return l.Value >= 0 && l.Value < Lanes.Count && Lanes[l.Value].Alive;
        }

        public int Generation(LaneId l)
        {
            return Lanes[l.Value].Gen;
        }

        public LaneInfo Info(LaneId l)
        {
            Lane x = Lanes[l.Value];
            return new LaneInfo
            {
                Id = l, Kind = x.Kind, X = x.X, Z = x.Z, Y = x.Y, S = x.S, PointCount = x.N, LengthM = x.Length, WidthM = x.Width,
                FreeKmh = x.FreeKmh, PeakKmh = x.PeakKmh, ClassMask = x.Mask, ControllerId = x.Controller, Approach = x.Approach,
                WayId = x.WayId, RoadClass = x.Class, Area = x.Area, Index = x.Index, Count = x.Count, Shared = x.Shared,
                Twin = new LaneId(x.Twin), Ring = x.Ring, OffsetM = x.Offset, Forward = x.Forward, TwoWay = x.TwoWay, SuccessorCount = x.Next.Length,
                PredecessorCount = x.Prev.Length,
            };
        }

        /// <summary>Successor <paramref name="i"/> of a lane.</summary>
        public LaneId Successor(LaneId l, int i)
        {
            return new LaneId(Lanes[l.Value].Next[i]);
        }

        public LaneId Predecessor(LaneId l, int i)
        {
            return new LaneId(Lanes[l.Value].Prev[i]);
        }

        /// <summary>Kind of the controller <paramref name="id"/> (None when absent).</summary>
        public ControllerKind ControllerKindOf(int id)
        {
            if (id < 0 || id >= Controllers.Count || !Controllers[id].Alive) return ControllerKind.None;
            return Controllers[id].Kind;
        }

        /// <summary>The live lanes, in id order (allocates; tests and tools).</summary>
        public List<LaneId> AllLanes()
        {
            var l = new List<LaneId>();
            lock (SyncRoot)
                for (int i = 0; i < Lanes.Count; i++)
                    if (Lanes[i].Alive) l.Add(new LaneId(i));
            return l;
        }

        /// <summary>The live road lanes of a way (allocates).</summary>
        public List<LaneId> LanesOfWay(long wayId)
        {
            var r = new List<LaneId>();
            lock (SyncRoot)
            {
                List<int> l;
                if (_byWay.TryGetValue(wayId, out l))
                    foreach (int i in l)
                        if (Lanes[i].Alive)
                            r.Add(new LaneId(i));
            }
            return r;
        }

        /// <summary>Position, height and heading (0 north, clockwise) at <paramref name="s"/> metres along a lane (clamped).</summary>
        public void PointAt(LaneId l, float s, out double x, out double z, out float y, out float headingRad)
        {
            PointAt(Lanes[l.Value], s, out x, out z, out y, out headingRad);
        }

        internal static void PointAt(Lane l, float s, out double x, out double z, out float y, out float heading)
        {
            float[] S = l.S;
            int n = l.N;
            if (n < 2)
            {
                x = l.X[0];
                z = l.Z[0];
                y = l.Y[0];
                heading = 0f;
                return;
            }
            int i;
            if (s <= 0f) i = 0;
            else if (s >= l.Length) i = n - 2;
            else
            {
                int lo = 0, hi = n - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (S[mid] <= s) lo = mid;
                    else hi = mid;
                }
                i = lo;
            }
            float seg = S[i + 1] - S[i];
            float t = seg > 1e-6f ? (s - S[i]) / seg : 0f;
            double dx = l.X[i + 1] - l.X[i], dz = l.Z[i + 1] - l.Z[i];
            x = l.X[i] + dx * t;
            z = l.Z[i] + dz * t;
            y = l.Y[i] + (l.Y[i + 1] - l.Y[i]) * t;
            heading = (float)Math.Atan2(dx, dz);
        }

        /// <summary>
        /// The nearest lane of a class to (x, z) within 30 m (road lanes, connectors and rings), with the arc length of the
        /// nearest point. Deterministic: exact ties go to the lower lane id.
        /// </summary>
        public bool TryNearestLane(double x, double z, VehicleClass c, out LaneId lane, out float along)
        {
            return TryNearestLane(x, z, c, 30.0, out lane, out along);
        }

        public bool TryNearestLane(double x, double z, VehicleClass c, double maxDistM, out LaneId lane, out float along)
        {
            lane = LaneId.None;
            along = 0f;
            uint bit = VehicleClasses.Bit(c);
            double best = maxDistM;
            lock (SyncRoot)
            {
                long cx0 = (long)Math.Floor((x - maxDistM) / GridCellM), cx1 = (long)Math.Floor((x + maxDistM) / GridCellM);
                long cz0 = (long)Math.Floor((z - maxDistM) / GridCellM), cz1 = (long)Math.Floor((z + maxDistM) / GridCellM);
                for (long cz = cz0; cz <= cz1; cz++)
                for (long cx = cx0; cx <= cx1; cx++)
                {
                    List<int> list;
                    if (!_grid.TryGetValue(CellKey(cx, cz), out list)) continue;
                    foreach (int id in list)
                    {
                        Lane l = Lanes[id];
                        if (!l.Alive || (l.Mask & bit) == 0) continue;
                        float s;
                        double d = Project(l, x, z, out s);
                        if (d < best || d == best && lane.IsValid && id < lane.Value)
                        {
                            best = d;
                            lane = new LaneId(id);
                            along = s;
                        }
                    }
                }
            }
            return lane.IsValid;
        }

        /// <summary>Distance from (x, z) to a lane's polyline and the arc length of the nearest point.</summary>
        internal static double Project(Lane l, double x, double z, out float along)
        {
            double best = double.PositiveInfinity;
            along = 0f;
            for (int i = 0; i + 1 < l.N; i++)
            {
                double ax = l.X[i], az = l.Z[i], dx = l.X[i + 1] - ax, dz = l.Z[i + 1] - az;
                double len2 = dx * dx + dz * dz;
                double t = len2 > 0 ? ((x - ax) * dx + (z - az) * dz) / len2 : 0;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                double qx = ax + t * dx - x, qz = az + t * dz - z;
                double d = Math.Sqrt(qx * qx + qz * qz);
                if (d < best)
                {
                    best = d;
                    along = (float)(l.S[i] + t * (l.S[i + 1] - l.S[i]));
                }
            }
            return best;
        }

        /// <summary>Road lanes whose bounds touch the square of half side <paramref name="r"/> around (x, z) (appends ids;
        /// caller holds <see cref="SyncRoot"/>).</summary>
        internal void LanesNear(double x, double z, double r, List<int> dst)
        {
            long cx0 = (long)Math.Floor((x - r) / GridCellM), cx1 = (long)Math.Floor((x + r) / GridCellM);
            long cz0 = (long)Math.Floor((z - r) / GridCellM), cz1 = (long)Math.Floor((z + r) / GridCellM);
            for (long cz = cz0; cz <= cz1; cz++)
            for (long cx = cx0; cx <= cx1; cx++)
            {
                List<int> list;
                if (!_grid.TryGetValue(CellKey(cx, cz), out list)) continue;
                foreach (int id in list)
                    if (Lanes[id].Alive && !dst.Contains(id))
                        dst.Add(id);
            }
        }

        private static long CellKey(long cx, long cz)
        {
            return (cx << 32) ^ (cz & 0xFFFFFFFFL);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Turn restrictions (D2 .ghrt)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Applies OSM turn restrictions at their via nodes (matched through JNCT node ids); takes effect on the
        /// next build of each node.</summary>
        public void SetRestrictions(IReadOnlyList<TurnRestrictionRecord> restrictions)
        {
            lock (SyncRoot)
            {
                _restrictions.Clear();
                if (restrictions == null) return;
                var tmp = new Dictionary<long, List<TurnRestrictionRecord>>();
                foreach (TurnRestrictionRecord r in restrictions)
                {
                    if (r.ViaNode == 0) continue;
                    List<TurnRestrictionRecord> l;
                    if (!tmp.TryGetValue(r.ViaNode, out l)) tmp[r.ViaNode] = l = new List<TurnRestrictionRecord>();
                    l.Add(r);
                }
                foreach (var kv in tmp) _restrictions[kv.Key] = kv.Value.ToArray();
            }
        }

        private bool Restricted(Node n, long fromWay, long toWay)
        {
            TurnRestrictionRecord[] rs;
            if (n.OsmNodeId == 0 || !_restrictions.TryGetValue(n.OsmNodeId, out rs)) return false;
            foreach (TurnRestrictionRecord r in rs)
            {
                if (r.FromWay != fromWay) continue;
                switch (r.Kind)
                {
                    case TurnRestriction.OnlyLeftTurn:
                    case TurnRestriction.OnlyRightTurn:
                    case TurnRestriction.OnlyStraightOn:
                        if (r.ToWay != toWay) return true;
                        break;
                    case TurnRestriction.NoEntry:
                    case TurnRestriction.NoExit:
                        break;
                    default:
                        if (r.ToWay == toWay) return true;
                        break;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Building
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Adds (or replaces) the lanes of a tile. Heavy work (offsets, heights) happens outside the lock.</summary>
        public void AddTile(TileId id, TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var built = new TileBuilder(this, id, t).Build();
            lock (SyncRoot)
            {
                RemoveLocked(id.Key);
                var tl = new TileLanes { Id = id };
                var dirty = new HashSet<long>();
                foreach (Lane l in built.Lanes)
                {
                    int lid = AllocLane(l);
                    tl.Lanes.Add(lid);
                }
                // Lane-local references (twin, left, right) were built as indices into built.Lanes: remap to ids.
                foreach (int lid in tl.Lanes)
                {
                    Lane l = Lanes[lid];
                    if (l.Twin >= 0) l.Twin = tl.Lanes[l.Twin];
                    if (l.Left >= 0) l.Left = tl.Lanes[l.Left];
                    if (l.Right >= 0) l.Right = tl.Lanes[l.Right];
                    AddToGrid(lid);
                    List<int> w;
                    if (!_byWay.TryGetValue(l.WayId, out w)) _byWay[l.WayId] = w = new List<int>();
                    w.Add(lid);
                }
                foreach (TileBuilder.EndRef e in built.Ends)
                {
                    var end = e.End;
                    end.In = Remap(end.In, tl.Lanes);
                    end.Out = Remap(end.Out, tl.Lanes);
                    Node n;
                    if (!_nodes.TryGetValue(e.Key, out n))
                    {
                        n = new Node { Key = e.Key, X = e.X, Z = e.Z };
                        _nodes[e.Key] = n;
                    }
                    if (e.Junction.HasValue)
                    {
                        JunctionRecord j = e.Junction.Value;
                        n.Kind = j.Kind;
                        n.JFlags = j.Flags;
                        n.OsmNodeId = j.OsmNodeId;
                        if (j.RingDiameterCm > 0 && e.SynthRing)
                        {
                            n.Synthetic = true;
                            n.RingRadius = Math.Max(4.5f, j.RingDiameterCm / 200f);
                            n.RingX = e.JX;
                            n.RingZ = e.JZ;
                        }
                    }
                    n.Ends.Add(end);
                    tl.Ends.Add(new KeyValuePair<long, EdgeEnd>(e.Key, end));
                    dirty.Add(e.Key);
                }
                _tiles[id.Key] = tl;
                Version++;
                var keys = new List<long>(dirty);
                keys.Sort();
                foreach (long k in keys) RebuildNode(_nodes[k]);
            }
        }

        private static int[] Remap(int[] local, List<int> ids)
        {
            var r = new int[local.Length];
            for (int i = 0; i < r.Length; i++) r[i] = ids[local[i]];
            return r;
        }

        public void RemoveTile(TileId id)
        {
            lock (SyncRoot) RemoveLocked(id.Key);
        }

        private void RemoveLocked(ulong key)
        {
            TileLanes tl;
            if (!_tiles.TryGetValue(key, out tl)) return;
            _tiles.Remove(key);
            Version++;
            var dirty = new HashSet<long>();
            foreach (var kv in tl.Ends)
            {
                Node n;
                if (!_nodes.TryGetValue(kv.Key, out n)) continue;
                n.Ends.Remove(kv.Value);
                dirty.Add(kv.Key);
            }
            foreach (int lid in tl.Lanes)
            {
                Lane l = Lanes[lid];
                RemoveFromGrid(lid);
                List<int> w;
                if (_byWay.TryGetValue(l.WayId, out w))
                {
                    w.Remove(lid);
                    if (w.Count == 0) _byWay.Remove(l.WayId);
                }
                FreeLane(lid);
            }
            var keys = new List<long>(dirty);
            keys.Sort();
            foreach (long k in keys)
            {
                Node n = _nodes[k];
                if (n.Ends.Count == 0)
                {
                    ClearNode(n);
                    _nodes.Remove(k);
                }
                else
                {
                    RebuildNode(n);
                }
            }
        }

        private int AllocLane(Lane l)
        {
            int id;
            if (_free.Count > 0)
            {
                id = _free.Pop();
                l.Gen = Lanes[id].Gen + 1;
                Lanes[id] = l;
            }
            else
            {
                id = Lanes.Count;
                Lanes.Add(l);
            }
            l.Id = id;
            l.Alive = true;
            _alive++;
            return id;
        }

        private void FreeLane(int id)
        {
            Lane l = Lanes[id];
            if (!l.Alive) return;
            l.Alive = false;
            l.Next = l.Prev = l.Conflicts = Lane.Empty;
            _free.Push(id);
            _alive--;
        }

        private void AddToGrid(int id)
        {
            Lane l = Lanes[id];
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int i = 0; i < l.N; i++)
            {
                minX = Math.Min(minX, l.X[i]);
                maxX = Math.Max(maxX, l.X[i]);
                minZ = Math.Min(minZ, l.Z[i]);
                maxZ = Math.Max(maxZ, l.Z[i]);
            }
            for (long cz = (long)Math.Floor(minZ / GridCellM); cz <= (long)Math.Floor(maxZ / GridCellM); cz++)
            for (long cx = (long)Math.Floor(minX / GridCellM); cx <= (long)Math.Floor(maxX / GridCellM); cx++)
            {
                List<int> list;
                long k = CellKey(cx, cz);
                if (!_grid.TryGetValue(k, out list)) _grid[k] = list = new List<int>();
                list.Add(id);
            }
        }

        private void RemoveFromGrid(int id)
        {
            Lane l = Lanes[id];
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int i = 0; i < l.N; i++)
            {
                minX = Math.Min(minX, l.X[i]);
                maxX = Math.Max(maxX, l.X[i]);
                minZ = Math.Min(minZ, l.Z[i]);
                maxZ = Math.Max(maxZ, l.Z[i]);
            }
            for (long cz = (long)Math.Floor(minZ / GridCellM); cz <= (long)Math.Floor(maxZ / GridCellM); cz++)
            for (long cx = (long)Math.Floor(minX / GridCellM); cx <= (long)Math.Floor(maxX / GridCellM); cx++)
            {
                List<int> list;
                long k = CellKey(cx, cz);
                if (!_grid.TryGetValue(k, out list)) continue;
                list.Remove(id);
                if (list.Count == 0) _grid.Remove(k);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Nodes: connectors, rings, controllers
        // ---------------------------------------------------------------------------------------------------------

        private void ClearNode(Node n)
        {
            foreach (int id in n.Owned)
            {
                RemoveFromGrid(id);
                FreeLane(id);
            }
            n.Owned.Clear();
            if (n.Controller >= 0)
            {
                Controllers[n.Controller].Alive = false;
                _freeControllers.Push(n.Controller);
                n.Controller = -1;
            }
        }

        private void RebuildNode(Node n)
        {
            // Keep connectors whose (from, to) pair survives, so agents on them keep their lane.
            var keep = new Dictionary<long, int>();
            foreach (int id in n.Owned)
            {
                Lane c = Lanes[id];
                if (c.Kind == LaneKind.Connector && c.Prev.Length == 1 && c.Next.Length == 1 && Lanes[c.Prev[0]].Alive && Lanes[c.Next[0]].Alive)
                    keep[PairKey(c.Prev[0], c.Next[0])] = id;
            }
            var stale = new List<int>(n.Owned);
            n.Owned.Clear();
            if (n.Controller >= 0)
            {
                Controllers[n.Controller].Alive = false;
                _freeControllers.Push(n.Controller);
                n.Controller = -1;
            }

            // Reset the node's lane links (incoming lanes' Next, outgoing lanes' Prev).
            foreach (EdgeEnd e in n.Ends)
            {
                foreach (int l in e.In)
                {
                    Lanes[l].Next = Lane.Empty;
                    Lanes[l].Controller = -1;
                    Lanes[l].Approach = -1;
                }
                foreach (int l in e.Out) Lanes[l].Prev = Lane.Empty;
            }
            var ends = new List<EdgeEnd>(n.Ends);
            // Deterministic order: by angle of the arm direction.
            ends.Sort((a, b) =>
            {
                int c = Math.Atan2(a.DirZ, a.DirX).CompareTo(Math.Atan2(b.DirZ, b.DirX));
                return c != 0 ? c : a.WayId.CompareTo(b.WayId);
            });

            int degree = ends.Count;
            var created = new List<int>();
            var next = new Dictionary<int, List<int>>();
            var prev = new Dictionary<int, List<int>>();
            Action<int, int> link = (a, b) =>
            {
                List<int> l;
                if (!next.TryGetValue(a, out l)) next[a] = l = new List<int>();
                l.Add(b);
                if (!prev.TryGetValue(b, out l)) prev[b] = l = new List<int>();
                l.Add(a);
            };

            if (n.Synthetic && degree >= 2)
            {
                BuildRing(n, ends, created, link, keep);
            }
            else
            {
                bool joint = degree == 2 && n.Kind == JunctionKind.Plain && (n.JFlags & (byte)(JunctionFlags.HasPolice | JunctionFlags.HasSignals)) == 0;
                for (int ai = 0; ai < degree; ai++)
                {
                    EdgeEnd a = ends[ai];
                    if (joint && DirectJoint(a, ends[1 - ai], link)) continue;
                    if (a.In.Length == 0) continue;
                    bool any = false;
                    for (int bi = 0; bi < degree; bi++)
                    {
                        if (bi == ai) continue;
                        EdgeEnd b = ends[bi];
                        if (b.Out.Length == 0) continue;
                        if (Restricted(n, a.WayId, b.WayId)) continue;
                        any |= ConnectArms(n, a, b, created, link, keep);
                    }
                    // A dead end of a two-way road inside the world turns round.
                    if (!any && degree == 1 && !a.Cut && a.Out.Length > 0) ConnectArms(n, a, a, created, link, keep);
                }
            }

            foreach (int id in stale)
                if (!created.Contains(id))
                {
                    RemoveFromGrid(id);
                    FreeLane(id);
                }
            foreach (var kv in next) Lanes[kv.Key].Next = Sorted(kv.Value);
            foreach (var kv in prev) Lanes[kv.Key].Prev = Sorted(kv.Value);
            n.Owned.AddRange(created);
            Conflicts(n, created);

            // Controller.
            ControllerKind kind = ControllerFor(n, ends);
            if (kind != ControllerKind.None)
            {
                int cid = _freeControllers.Count > 0 ? _freeControllers.Pop() : Controllers.Count;
                var c = new Controller { Id = cid, Alive = true, Kind = kind, NodeKey = n.Key, X = n.X, Z = n.Z, Arms = degree };
                c.Officers24 = (n.JFlags & (byte)JunctionFlags.Officers24) != 0;
                c.ArmRank = new int[degree];
                c.ArmAngle = new double[degree];
                for (int i = 0; i < degree; i++)
                {
                    c.ArmRank[i] = ends[i].Rank;
                    c.ArmAngle[i] = Math.Atan2(ends[i].DirZ, ends[i].DirX);
                    foreach (int l in ends[i].In)
                    {
                        Lanes[l].Controller = cid;
                        Lanes[l].Approach = i;
                    }
                }
                Phases(c, ends);
                if (cid == Controllers.Count) Controllers.Add(c);
                else Controllers[cid] = c;
                n.Controller = cid;
            }
        }

        /// <summary>A plain joint (two pieces meeting, or a tile seam) whose lanes line up within 0.75 m: lane i links to
        /// lane i directly, without a connector.</summary>
        private bool DirectJoint(EdgeEnd a, EdgeEnd b, Action<int, int> link)
        {
            if (a.In.Length == 0 || a.In.Length != b.Out.Length) return false;
            for (int k = 0; k < a.In.Length; k++)
            {
                Lane x = Lanes[a.In[k]], y = Lanes[b.Out[k]];
                double dx = x.X[x.N - 1] - y.X[0], dz = x.Z[x.N - 1] - y.Z[0];
                if (dx * dx + dz * dz > 0.75 * 0.75 || (x.Mask & y.Mask) == 0) return false;
            }
            for (int k = 0; k < a.In.Length; k++) link(a.In[k], b.Out[k]);
            return true;
        }

        private static int[] Sorted(List<int> l)
        {
            l.Sort();
            return l.ToArray();
        }

        private static long PairKey(int a, int b)
        {
            return ((long)a << 32) | (uint)b;
        }

        private ControllerKind ControllerFor(Node n, List<EdgeEnd> ends)
        {
            if (n.Synthetic) return ControllerKind.Roundabout;
            int arms = 0;
            bool ring = false;
            foreach (EdgeEnd e in ends)
            {
                if (e.In.Length > 0 || e.Out.Length > 0) arms++;
                ring |= e.Ring;
            }
            bool police = n.Kind == JunctionKind.Police || (n.JFlags & (byte)JunctionFlags.HasPolice) != 0;
            bool signals = n.Kind == JunctionKind.Signals || (n.JFlags & (byte)JunctionFlags.HasSignals) != 0;
            if (ring && arms >= 3) return ControllerKind.Roundabout;
            if (arms < 3) return ControllerKind.None;
            if (signals) return ControllerKind.Signal;
            if (police) return ControllerKind.Police;
            if (n.Kind == JunctionKind.Roundabout || n.Kind == JunctionKind.Circular || n.Kind == JunctionKind.MiniRoundabout)
                return ControllerKind.Roundabout;
            return ControllerKind.Priority;
        }

        /// <summary>Phase groups: the straightest pair of arms shares a phase; every other arm gets its own (police: 2
        /// phases at T-junctions, 3 at 4-arm chowks; signals: 3 phases).</summary>
        private static void Phases(Controller c, List<EdgeEnd> ends)
        {
            int n = ends.Count;
            c.PhaseOf = new int[n];
            for (int i = 0; i < n; i++) c.PhaseOf[i] = -1;
            int bi = -1, bj = -1;
            double best = -0.5; // the pair must be at least 120 degrees apart
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                double dot = -(ends[i].DirX * ends[j].DirX + ends[i].DirZ * ends[j].DirZ);
                if (dot > best)
                {
                    best = dot;
                    bi = i;
                    bj = j;
                }
            }
            int p = 0;
            if (bi >= 0)
            {
                c.PhaseOf[bi] = 0;
                c.PhaseOf[bj] = 0;
                p = 1;
            }
            for (int i = 0; i < n; i++)
                if (c.PhaseOf[i] < 0)
                    c.PhaseOf[i] = p++;
            if (c.Kind == ControllerKind.Signal && p < 3) p = Math.Max(p, 2);
            c.Phases = Math.Max(1, p);
        }

        /// <summary>Connectors from arm a's incoming lanes to arm b's outgoing lanes (a == b: the U-turn of a dead end).
        /// Left turns (the kerb side in left-hand traffic) leave from the kerb lane into the kerb lane, right turns from
        /// the rightmost lane into the rightmost lane, straight movements lane by lane.</summary>
        private bool ConnectArms(Node n, EdgeEnd a, EdgeEnd b, List<int> created, Action<int, int> link, Dictionary<long, int> keep)
        {
            double inX = -a.DirX, inZ = -a.DirZ; // travel direction arriving at the node
            double cross = inX * b.DirZ - inZ * b.DirX, dot = inX * b.DirX + inZ * b.DirZ;
            double ang = Math.Atan2(cross, dot); // > 0 turning left (counter-clockwise from above)
            int ni = a.In.Length, no = b.Out.Length;
            bool any = false;
            LaneKind kind = a == b ? LaneKind.UTurn : LaneKind.Connector;
            if (a == b || Math.Abs(ang) < 0.5)
            {
                // Straight (or U-turn): lane by lane.
                int pairs = Math.Max(ni, no);
                for (int k = 0; k < pairs; k++)
                {
                    int from = a.In[Math.Min(k, ni - 1)], to = b.Out[Math.Min(k, no - 1)];
                    if (a == b && Lanes[from].Twin == to && Lanes[from].Shared) to = Lanes[from].Twin;
                    any |= Connector(n, from, to, kind, created, link, keep);
                }
            }
            else if (ang > 0)
            {
                any |= Connector(n, a.In[0], b.Out[0], kind, created, link, keep);
            }
            else
            {
                any |= Connector(n, a.In[ni - 1], b.Out[no - 1], kind, created, link, keep);
            }
            return any;
        }

        private bool Connector(Node n, int from, int to, LaneKind kind, List<int> created, Action<int, int> link, Dictionary<long, int> keep)
        {
            Lane a = Lanes[from], b = Lanes[to];
            uint mask = a.Mask & b.Mask;
            if (mask == 0) return false;
            int id;
            if (keep.TryGetValue(PairKey(from, to), out id) && !created.Contains(id))
            {
                created.Add(id);
                link(from, id);
                link(id, to);
                return true;
            }
            var c = Bezier(a, b, kind);
            c.Mask = mask;
            c.NodeStart = c.NodeEnd = n.Key;
            c.WayId = b.WayId;
            c.Class = b.Class;
            c.Area = b.Area;
            c.TileKey = b.TileKey;
            id = AllocLane(c);
            AddToGrid(id);
            created.Add(id);
            link(from, id);
            link(id, to);
            return true;
        }

        /// <summary>A cubic Bézier lane from a's end to b's start with handles of 0.4 × chord along the lanes.</summary>
        private static Lane Bezier(Lane a, Lane b, LaneKind kind)
        {
            int ea = a.N - 1;
            double p0x = a.X[ea], p0z = a.Z[ea], p3x = b.X[0], p3z = b.Z[0];
            double d0x = a.X[ea] - a.X[ea - 1], d0z = a.Z[ea] - a.Z[ea - 1];
            double d3x = b.X[1] - b.X[0], d3z = b.Z[1] - b.Z[0];
            Norm(ref d0x, ref d0z);
            Norm(ref d3x, ref d3z);
            double chord = Math.Sqrt((p3x - p0x) * (p3x - p0x) + (p3z - p0z) * (p3z - p0z));
            double h = 0.4 * chord;
            if (kind == LaneKind.UTurn) h = Math.Max(h, 0.6 * Math.Max(a.Width, 3.0)); // a loop beyond the dead end
            double p1x = p0x + d0x * h, p1z = p0z + d0z * h, p2x = p3x - d3x * h, p2z = p3z - d3z * h;
            int m = ConnectorPoints;
            var l = new Lane
            {
                Kind = kind, X = new double[m], Z = new double[m], Y = new float[m], S = new float[m], N = m,
                Width = Math.Min(a.Width, b.Width), FreeKmh = Math.Min(a.FreeKmh, b.FreeKmh), PeakKmh = Math.Min(a.PeakKmh, b.PeakKmh),
                Forward = b.Forward, Ring = a.Ring && b.Ring, Priority = a.Ring && b.Ring,
            };
            for (int i = 0; i < m; i++)
            {
                double t = i / (double)(m - 1), u = 1 - t;
                l.X[i] = u * u * u * p0x + 3 * u * u * t * p1x + 3 * u * t * t * p2x + t * t * t * p3x;
                l.Z[i] = u * u * u * p0z + 3 * u * u * t * p1z + 3 * u * t * t * p2z + t * t * t * p3z;
                l.Y[i] = (float)(a.Y[ea] * u + b.Y[0] * t);
            }
            Finish(l);
            // Turning speed: v = sqrt(2.5 m/s² · R) from the sharpest bend, within the lanes' targets.
            float r = MinRadius(l);
            float vTurn = (float)Math.Sqrt(2.5 * r) * 3.6f;
            if (vTurn < l.FreeKmh) l.FreeKmh = Math.Max(8f, vTurn);
            if (vTurn < l.PeakKmh) l.PeakKmh = Math.Max(5f, vTurn);
            return l;
        }

        /// <summary>A clockwise ring of lanes around a synthesised roundabout island, with entry connectors from every
        /// incoming arm onto the ring and exit connectors off it (W2_DESIGN 5.1: circulate clockwise, enter turning left).</summary>
        private void BuildRing(Node n, List<EdgeEnd> ends, List<int> created, Action<int, int> link, Dictionary<long, int> keep)
        {
            double cx = n.RingX, cz = n.RingZ;
            float r = n.RingRadius;
            // Attach angles per arm: exit just before the arm (clockwise travel reaches it first), entry just after.
            int m = ends.Count;
            var attach = new List<KeyValuePair<double, int>>(); // angle, code (2*arm for exit, 2*arm+1 for entry)
            double off = Math.Min(0.35, 4.0 / Math.Max(r, 1.0));
            for (int i = 0; i < m; i++)
            {
                double a = Math.Atan2(ends[i].DirZ, ends[i].DirX);
                attach.Add(new KeyValuePair<double, int>(Wrap(a + off), 2 * i)); // clockwise = decreasing angle: exit first
                attach.Add(new KeyValuePair<double, int>(Wrap(a - off), 2 * i + 1));
            }
            // Order clockwise (descending angle) starting from the first attach.
            attach.Sort((p, q) => q.Key.CompareTo(p.Key) != 0 ? q.Key.CompareTo(p.Key) : p.Value.CompareTo(q.Value));
            int k = attach.Count;
            var seg = new int[k]; // ring segment i runs from attach i to attach i+1 (clockwise)
            float y = 0f;
            int ny = 0;
            foreach (EdgeEnd e in ends)
            {
                foreach (int l in e.In)
                {
                    y += Lanes[l].Y[Lanes[l].N - 1];
                    ny++;
                }
                foreach (int l in e.Out)
                {
                    y += Lanes[l].Y[0];
                    ny++;
                }
            }
            y = ny > 0 ? y / ny : 0f;
            float width = 4.0f;
            uint mask = 0;
            float free = 20f, peak = 15f;
            foreach (EdgeEnd e in ends)
            {
                foreach (int l in e.In) mask |= Lanes[l].Mask;
                foreach (int l in e.Out) mask |= Lanes[l].Mask;
            }
            for (int i = 0; i < k; i++)
            {
                double a0 = attach[i].Key, a1 = attach[(i + 1) % k].Key;
                double sweep = a0 - a1;
                if (sweep <= 0) sweep += 2 * Math.PI;
                int pts = Math.Max(3, (int)Math.Ceiling(sweep * r / 3.0) + 1);
                var l = new Lane
                {
                    Kind = LaneKind.Ring, X = new double[pts], Z = new double[pts], Y = new float[pts], S = new float[pts], N = pts,
                    Width = width, FreeKmh = free, PeakKmh = peak, Mask = mask, Ring = true, Priority = true, NodeStart = n.Key,
                    NodeEnd = n.Key, Class = RoadClass.Unknown, TileKey = ends[0].TileKey,
                };
                for (int j = 0; j < pts; j++)
                {
                    double a = a0 - sweep * j / (pts - 1);
                    l.X[j] = cx + r * Math.Cos(a);
                    l.Z[j] = cz + r * Math.Sin(a);
                    l.Y[j] = y;
                }
                Finish(l);
                int id = AllocLane(l);
                AddToGrid(id);
                created.Add(id);
                seg[i] = id;
            }
            for (int i = 0; i < k; i++) link(seg[i], seg[(i + 1) % k]);
            for (int i = 0; i < k; i++)
            {
                int code = attach[i].Value, arm = code >> 1;
                EdgeEnd e = ends[arm];
                int before = seg[(i - 1 + k) % k], after = seg[i];
                if ((code & 1) == 0)
                {
                    // Exit: from the ring segment ending here onto the arm's outgoing lanes (kerb lane).
                    if (e.Out.Length > 0) ConnectorTo(n, before, e.Out[0], created, link, keep);
                }
                else
                {
                    // Entry: the arm's incoming lanes merge onto the ring segment starting here.
                    foreach (int inl in e.In) ConnectorTo(n, inl, after, created, link, keep);
                }
            }
        }

        private void ConnectorTo(Node n, int from, int to, List<int> created, Action<int, int> link, Dictionary<long, int> keep)
        {
            Connector(n, from, to, LaneKind.Connector, created, link, keep);
        }

        /// <summary>Conflict sets of a node's connectors: two connectors conflict when their swept bodies can touch (the
        /// polylines come closer than the widest bodies' half widths plus their curve sweeps plus 0.3 m) or they end in
        /// the same lane. Ring segments do not take part (merges onto them are handled by gap acceptance).</summary>
        private void Conflicts(Node n, List<int> created)
        {
            var list = new List<int>();
            foreach (int id in created)
            {
                Lane l = Lanes[id];
                if (l.Kind == LaneKind.Ring) continue;
                l.MaxHalfWidth = MaxHalfWidth(l.Mask);
                float r = MinRadius(l);
                float len = MaxLength(l.Mask);
                l.SweepM = Math.Min(2f, len * len / (8f * Math.Max(r, 1f)));
                list.Add(id);
            }
            var conf = new Dictionary<int, List<int>>();
            foreach (int id in list) conf[id] = new List<int>();
            for (int i = 0; i < list.Count; i++)
            for (int j = i + 1; j < list.Count; j++)
            {
                Lane a = Lanes[list[i]], b = Lanes[list[j]];
                bool c = a.Next.Length > 0 && b.Next.Length > 0 && a.Next[0] == b.Next[0];
                if (!c)
                {
                    double thr = a.MaxHalfWidth + b.MaxHalfWidth + a.SweepM + b.SweepM + 0.3;
                    c = MinDistance(a, b, a.Prev.Length > 0 && b.Prev.Length > 0 && a.Prev[0] == b.Prev[0]) < thr;
                }
                if (!c) continue;
                conf[list[i]].Add(list[j]);
                conf[list[j]].Add(list[i]);
            }
            foreach (var kv in conf) Lanes[kv.Key].Conflicts = Sorted(kv.Value);
        }

        /// <summary>Closest approach of two polylines. Connectors leaving the same lane share their first metres: those
        /// are ordered by the lane they leave, so the shared start is skipped.</summary>
        private static double MinDistance(Lane a, Lane b, bool sameSource)
        {
            double best = double.PositiveInfinity;
            int i0 = sameSource ? 3 : 0;
            for (int i = i0; i < a.N; i++)
            for (int j = i0; j + 1 < b.N; j++)
            {
                double ax = b.X[j], az = b.Z[j], dx = b.X[j + 1] - ax, dz = b.Z[j + 1] - az;
                double len2 = dx * dx + dz * dz;
                double t = len2 > 0 ? ((a.X[i] - ax) * dx + (a.Z[i] - az) * dz) / len2 : 0;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                double qx = ax + t * dx - a.X[i], qz = az + t * dz - a.Z[i];
                double d = Math.Sqrt(qx * qx + qz * qz);
                if (d < best) best = d;
            }
            return best;
        }

        private static float MaxHalfWidth(uint mask)
        {
            float h = 0f;
            for (int c = 0; c < VehicleClasses.Count; c++)
                if ((mask & (1u << c)) != 0)
                    h = Math.Max(h, TrafficTables.HalfWidthOf((VehicleClass)c));
            return h;
        }

        private static float MaxLength(uint mask)
        {
            float h = 0f;
            for (int c = 0; c < VehicleClasses.Count; c++)
                if ((mask & (1u << c)) != 0)
                    h = Math.Max(h, TrafficTables.LengthOf((VehicleClass)c));
            return h;
        }

        internal static float MinRadius(Lane l)
        {
            double best = 1e6;
            for (int i = 1; i + 1 < l.N; i++)
            {
                double ax = l.X[i - 1], az = l.Z[i - 1], bx = l.X[i], bz = l.Z[i], cx = l.X[i + 1], cz = l.Z[i + 1];
                double ab = Hyp(bx - ax, bz - az), bc = Hyp(cx - bx, cz - bz), ca = Hyp(ax - cx, az - cz);
                double cr = Math.Abs((bx - ax) * (cz - az) - (bz - az) * (cx - ax));
                if (cr < 1e-9) continue;
                double r = ab * bc * ca / (2 * cr);
                if (r < best) best = r;
            }
            return (float)best;
        }

        private static double Hyp(double x, double z)
        {
            return Math.Sqrt(x * x + z * z);
        }

        private static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a <= -Math.PI) a += 2 * Math.PI;
            return a;
        }

        private static void Norm(ref double x, ref double z)
        {
            double l = Math.Sqrt(x * x + z * z);
            if (l < 1e-12)
            {
                x = 0;
                z = 1;
                return;
            }
            x /= l;
            z /= l;
        }

        /// <summary>Arc lengths and total length of a lane's polyline.</summary>
        internal static void Finish(Lane l)
        {
            l.S[0] = 0f;
            double s = 0;
            for (int i = 1; i < l.N; i++)
            {
                s += Hyp(l.X[i] - l.X[i - 1], l.Z[i] - l.Z[i - 1]);
                l.S[i] = (float)s;
            }
            l.Length = (float)s;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Tile builder (outside the lock)
        // ---------------------------------------------------------------------------------------------------------

        private sealed class TileBuilder
        {
            public struct EndRef
            {
                public long Key;
                public double X, Z;
                public EdgeEnd End;
                public JunctionRecord? Junction;
                public bool SynthRing;
                public double JX, JZ;
            }

            public sealed class Result
            {
                public readonly List<Lane> Lanes = new List<Lane>();
                public readonly List<EndRef> Ends = new List<EndRef>();
            }

            private readonly LaneGraph _g;
            private readonly TileId _id;
            private readonly TileData _t;
            private readonly long _gx0, _gz0;
            private readonly Result _r = new Result();
            private readonly Dictionary<long, float> _nodeWidth = new Dictionary<long, float>();
            private readonly Dictionary<long, float> _ringRadius = new Dictionary<long, float>();
            private readonly Dictionary<long, List<double>> _armAngles = new Dictionary<long, List<double>>();
            private readonly Dictionary<long, int> _arms = new Dictionary<long, int>();

            public TileBuilder(LaneGraph g, TileId id, TileData t)
            {
                _g = g;
                _id = id;
                _t = t;
                _gx0 = (long)Math.Round(id.X0 * 100.0);
                _gz0 = (long)Math.Round(id.Z0 * 100.0);
            }

            private long Key(int xcm, int zcm)
            {
                long gx = _gx0 + xcm, gz = _gz0 + zcm;
                return (gx << 32) ^ (gz & 0xFFFFFFFFL);
            }

            private sealed class Piece
            {
                public int Road;
                public uint Mask;
                public bool Oneway, Ring, Dual, Service;
                public float Real;
                public RoadWidthProfile Prof;
                public RoadAttrRecord Attr;
                public int First, Last;
                public double[] Along;
            }

            private sealed class JInfo
            {
                public JunctionRecord J;
            }

            public Result Build()
            {
                TileData t = _t;
                if (t.Roads.Count == 0) return _r;
                RoadLayout lay = RoadLayout.For(t);
                var sampler = new TileHeightSampler(t, 1);
                var opts = new RoadOptions();
                var noVehicle = NoVehicleAreas(t);

                // 1. Pieces with lanes.
                var pieces = new List<Piece>();
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    RoadRecord r = t.Roads[ri];
                    if ((r.Flags & RoadFlags.Tunnel) != 0 || !RoadMesher.IsDrawn(r, opts)) continue;
                    RoadWidthProfile prof = lay.Profiles[ri];
                    RoadAttrRecord a = lay.Attrs[ri];
                    float minW = prof.Count > 0 ? prof.MinWidth : 0f;
                    if (minW < MinLaneWidthM) continue;
                    uint mask = TrafficTables.ClassesFor(RoadWidthModel.AccessFor(minW, r, a));
                    if ((mask & ~VehicleClasses.Bit(VehicleClass.Bicycle) & ~VehicleClasses.Bit(VehicleClass.Rickshaw)) == 0 &&
                        (r.RoadClass == RoadClass.Footway || r.RoadClass == RoadClass.Path || r.RoadClass == RoadClass.Steps ||
                         r.RoadClass == RoadClass.Cycleway || r.RoadClass == RoadClass.Bridleway || r.RoadClass == RoadClass.Pedestrian))
                        continue; // trails belong to the pedestrian graph
                    if (mask == 0) continue;
                    int first, last;
                    Driving.RoadSpatialIndex.RenderedRange(r, out first, out last);
                    if (last <= first) continue;
                    var p = new Piece
                    {
                        Road = ri, Mask = mask, Prof = prof, Attr = a, First = first, Last = last, Real = prof.RealM,
                        Ring = a.Has(RoadAttrFlags.RingMember), Dual = a.Has(RoadAttrFlags.Dual), Service = a.Has(RoadAttrFlags.ServiceRoad),
                    };
                    p.Oneway = (r.Flags & RoadFlags.Oneway) != 0 || p.Ring;
                    p.Along = new double[r.PointCount];
                    for (int i = first + 1; i <= last; i++)
                        p.Along[i] = p.Along[i - 1] + Math.Sqrt(Sq((r.Points[2 * i] - r.Points[2 * i - 2]) / 100.0) +
                                                                 Sq((r.Points[2 * i + 1] - r.Points[2 * i - 1]) / 100.0));
                    pieces.Add(p);
                }

                // 2. Node points: piece ends and interior points shared by two or more pieces; the arm count of a node
                // (an end is one arm, a piece passing through is two) decides junction or plain joint.
                var count = new Dictionary<long, int>();
                foreach (Piece p in pieces)
                {
                    int[] pts = t.Roads[p.Road].Points;
                    for (int i = p.First; i <= p.Last; i++)
                    {
                        long k = Key(pts[2 * i], pts[2 * i + 1]);
                        int c;
                        count.TryGetValue(k, out c);
                        count[k] = c + 1;
                    }
                }
                foreach (Piece p in pieces)
                {
                    int[] pts = t.Roads[p.Road].Points;
                    for (int i = p.First; i <= p.Last; i++)
                    {
                        long k = Key(pts[2 * i], pts[2 * i + 1]);
                        int c;
                        count.TryGetValue(k, out c);
                        if (c < 2 && i != p.First && i != p.Last) continue;
                        int a;
                        _arms.TryGetValue(k, out a);
                        _arms[k] = a + (i == p.First || i == p.Last ? 1 : 2);
                    }
                }

                // Widest carriageway at every node (setbacks keep a stopped lane end out of the crossing road).
                foreach (Piece p in pieces)
                {
                    int[] pts = t.Roads[p.Road].Points;
                    for (int i = p.First; i <= p.Last; i++)
                    {
                        long k = Key(pts[2 * i], pts[2 * i + 1]);
                        int c;
                        count.TryGetValue(k, out c);
                        if (c < 2 && i != p.First && i != p.Last) continue;
                        float w = p.Prof.WidthAt(p.Along[i]), old;
                        if (!_nodeWidth.TryGetValue(k, out old) || w > old) _nodeWidth[k] = w;
                    }
                }

                // Junction records by node key (exact point match, else the nearest node within 3 m).
                var jByKey = new Dictionary<long, JInfo>();
                foreach (JunctionRecord j in t.Junctions) jByKey[Key(j.XCm, j.ZCm)] = new JInfo { J = j };
                foreach (var kv in _nodeWidth)
                {
                    JInfo ji;
                    if (!jByKey.TryGetValue(kv.Key, out ji)) continue;
                    JunctionKind jk = ji.J.Kind;
                    if (jk == JunctionKind.Roundabout || jk == JunctionKind.Circular || jk == JunctionKind.MiniRoundabout)
                        _ringRadius[kv.Key] = Math.Max(4.5f, ji.J.RingDiameterCm > 0 ? ji.J.RingDiameterCm / 200f : jk == JunctionKind.MiniRoundabout ? 4.5f : 8f);
                    else if (jk == JunctionKind.SyntheticIsland)
                    {
                        float rr = SyntheticRingRadius(lay, ji.J);
                        if (rr > 0f) _ringRadius[kv.Key] = rr;
                    }
                }

                // Ring centres: roundabout records with a ring diameter; mapped ring ways are oriented clockwise around them.
                var rings = new List<JunctionRecord>();
                foreach (JunctionRecord j in t.Junctions)
                    if (j.RingDiameterCm > 0 && (j.Kind == JunctionKind.Roundabout || j.Kind == JunctionKind.Circular || j.Kind == JunctionKind.MiniRoundabout))
                        rings.Add(j);

                // 3. Edges: split pieces at node points; record the arm directions at every node (setbacks grow where
                // arms meet at a narrow angle, so bodies waiting at neighbouring stop lines never touch).
                var edges = new List<KeyValuePair<Piece, int[]>>();
                foreach (Piece p in pieces)
                {
                    RoadRecord r = t.Roads[p.Road];
                    int[] pts = r.Points;
                    int start = p.First;
                    for (int i = p.First + 1; i <= p.Last; i++)
                    {
                        long k = Key(pts[2 * i], pts[2 * i + 1]);
                        int c;
                        count.TryGetValue(k, out c);
                        if (i < p.Last && c < 2) continue;
                        edges.Add(new KeyValuePair<Piece, int[]>(p, new[] { start, i }));
                        ArmAngle(Key(pts[2 * start], pts[2 * start + 1]), pts, start, start + 1);
                        ArmAngle(k, pts, i, i - 1);
                        start = i;
                    }
                }
                // 4. Lanes.
                foreach (var e in edges)
                {
                    Piece p = e.Key;
                    RoadRecord r = t.Roads[p.Road];
                    bool reverseRing = p.Ring && RingIsAnticlockwise(r, p, rings);
                    Edge(p, r, e.Value[0], e.Value[1], reverseRing, count, sampler, opts, noVehicle);
                }

                // 4. Junction info on the ends: attach the JNCT record of a node (exact, else within 3 m) and decide
                // whether a roundabout record without ring ways gets a synthesised ring.
                var ringNodes = new HashSet<long>();
                for (int i = 0; i < _r.Ends.Count; i++)
                    if (_r.Ends[i].End.Ring)
                        ringNodes.Add(_r.Ends[i].Key);
                for (int i = 0; i < _r.Ends.Count; i++)
                {
                    EndRef e = _r.Ends[i];
                    JInfo ji;
                    if (!jByKey.TryGetValue(e.Key, out ji)) ji = NearJunction(e.X, e.Z, 3.0);
                    if (ji == null) continue;
                    e.Junction = ji.J;
                    bool roundabout = ji.J.Kind == JunctionKind.Roundabout || ji.J.Kind == JunctionKind.Circular ||
                                      ji.J.Kind == JunctionKind.MiniRoundabout;
                    // A synthetic police island (RoadLayout draws it, W2_DESIGN 4.7) is circulated clockwise like a
                    // roundabout: lanes never run through the island or its podium.
                    float synthR = ji.J.Kind == JunctionKind.SyntheticIsland ? SyntheticRingRadius(lay, ji.J) : 0f;
                    if ((roundabout || synthR > 0f) && !ringNodes.Contains(e.Key))
                    {
                        e.SynthRing = true;
                        e.JX = _id.X0 + ji.J.XCm / 100.0;
                        e.JZ = _id.Z0 + ji.J.ZCm / 100.0;
                        if (synthR > 0f)
                        {
                            JunctionRecord jj = ji.J;
                            jj.RingDiameterCm = (int)Math.Round(200.0 * synthR);
                            e.Junction = jj;
                        }
                        else if (ji.J.RingDiameterCm == 0)
                        {
                            JunctionRecord jj = ji.J;
                            jj.RingDiameterCm = jj.Kind == JunctionKind.MiniRoundabout ? 900 : 1600;
                            e.Junction = jj;
                        }
                    }
                    _r.Ends[i] = e;
                }
                return _r;
            }

            /// <summary>Ring-lane radius around the synthetic island RoadLayout draws at a SYNTHETIC_ISLAND record (the
            /// island plus half the 4 m ring lane and 0.5 m clearance), 0 when the layout has no island there.</summary>
            private static float SyntheticRingRadius(RoadLayout lay, JunctionRecord j)
            {
                double jx = j.XCm / 100.0, jz = j.ZCm / 100.0;
                foreach (RoadIsland island in lay.Islands)
                {
                    if (island.Kind != IslandKind.Synthetic) continue;
                    double dx = island.X - jx, dz = island.Z - jz;
                    if (dx * dx + dz * dz > 1.0) continue;
                    return island.RadiusM + 2.5f;
                }
                return 0f;
            }

            private void ArmAngle(long node, int[] pts, int at, int toward)
            {
                double dx = pts[2 * toward] - pts[2 * at], dz = pts[2 * toward + 1] - pts[2 * at + 1];
                if (dx == 0 && dz == 0) return;
                List<double> l;
                if (!_armAngles.TryGetValue(node, out l)) _armAngles[node] = l = new List<double>();
                l.Add(Math.Atan2(dz, dx));
            }

            /// <summary>The smallest angle between two arms at a node (π when it has fewer than two).</summary>
            private double MinArmAngle(long node)
            {
                List<double> l;
                if (!_armAngles.TryGetValue(node, out l) || l.Count < 2) return Math.PI;
                double best = Math.PI;
                for (int i = 0; i < l.Count; i++)
                for (int j = i + 1; j < l.Count; j++)
                {
                    double d = Math.Abs(l[i] - l[j]);
                    if (d > Math.PI) d = 2 * Math.PI - d;
                    if (d < best) best = d;
                }
                return Math.Max(best, 0.05);
            }

            private JInfo NearJunction(double x, double z, double maxM)
            {
                JInfo best = null;
                double bd = maxM;
                foreach (JunctionRecord j in _t.Junctions)
                {
                    double d = Math.Sqrt(Sq(_id.X0 + j.XCm / 100.0 - x) + Sq(_id.Z0 + j.ZCm / 100.0 - z));
                    if (d <= bd)
                    {
                        bd = d;
                        best = new JInfo { J = j };
                    }
                }
                return best;
            }

            /// <summary>True when a ring way runs anticlockwise around its ring centre (a mapping error in left-hand
            /// traffic): its lanes are then built against the point order so circulation stays clockwise.</summary>
            private bool RingIsAnticlockwise(RoadRecord r, Piece p, List<JunctionRecord> rings)
            {
                int[] pts = r.Points;
                int mid = (p.First + p.Last) / 2;
                if (mid >= p.Last) mid = p.Last - 1;
                double mx = pts[2 * mid] / 100.0, mz = pts[2 * mid + 1] / 100.0;
                double dx = (pts[2 * mid + 2] - pts[2 * mid]) / 100.0, dz = (pts[2 * mid + 3] - pts[2 * mid + 1]) / 100.0;
                double cx, cz;
                JunctionRecord? best = null;
                double bd = double.MaxValue;
                foreach (JunctionRecord j in rings)
                {
                    double d = Sq(j.XCm / 100.0 - mx) + Sq(j.ZCm / 100.0 - mz);
                    double lim = j.RingDiameterCm / 100.0 + 15.0;
                    if (d < bd && d < lim * lim)
                    {
                        bd = d;
                        best = j;
                    }
                }
                if (best.HasValue)
                {
                    cx = best.Value.XCm / 100.0;
                    cz = best.Value.ZCm / 100.0;
                }
                else
                {
                    // No record: the piece's own centroid (a closed ring way) is the centre.
                    cx = 0;
                    cz = 0;
                    int n = 0;
                    for (int i = p.First; i <= p.Last; i++)
                    {
                        cx += pts[2 * i] / 100.0;
                        cz += pts[2 * i + 1] / 100.0;
                        n++;
                    }
                    cx /= n;
                    cz /= n;
                    if (Sq(mx - cx) + Sq(mz - cz) < 1.0) return false;
                }
                double rx = mx - cx, rz = mz - cz;
                return rx * dz - rz * dx > 0; // counter-clockwise seen from above
            }

            private struct Tri
            {
                public double Ax, Az, Bx, Bz, Cx, Cz;
            }

            /// <summary>Triangles of the tile's SACRED_NO_VEHICLE areas (tile-local metres).</summary>
            private static List<Tri> NoVehicleAreas(TileData t)
            {
                var l = new List<Tri>();
                foreach (AreaRecord a in t.Areas)
                {
                    if ((a.Flags & AreaFlags.SacredNoVehicle) == 0 || a.Indices == null) continue;
                    for (int k = 0; k + 2 < a.Indices.Length; k += 3)
                    {
                        int i0 = a.Indices[k], i1 = a.Indices[k + 1], i2 = a.Indices[k + 2];
                        l.Add(new Tri
                        {
                            Ax = a.Vertices[2 * i0] / 100.0, Az = a.Vertices[2 * i0 + 1] / 100.0, Bx = a.Vertices[2 * i1] / 100.0,
                            Bz = a.Vertices[2 * i1 + 1] / 100.0, Cx = a.Vertices[2 * i2] / 100.0, Cz = a.Vertices[2 * i2 + 1] / 100.0,
                        });
                    }
                }
                return l;
            }

            private static bool InTris(List<Tri> tris, double x, double z)
            {
                foreach (Tri q in tris)
                {
                    double d1 = (x - q.Bx) * (q.Az - q.Bz) - (q.Ax - q.Bx) * (z - q.Bz);
                    double d2 = (x - q.Cx) * (q.Bz - q.Cz) - (q.Bx - q.Cx) * (z - q.Cz);
                    double d3 = (x - q.Ax) * (q.Cz - q.Az) - (q.Cx - q.Ax) * (z - q.Az);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) return true;
                }
                return false;
            }

            /// <summary>The class mask after the sacred rules: a lane touching a no-vehicle zone loses its motor classes
            /// (bicycles stay only in heritage squares).</summary>
            private uint SacredMask(uint mask, double[] xs, double[] zs, int n, List<Tri> noVehicle)
            {
                bool motorOut = false, allOut = false;
                for (int i = 0; i < n; i++)
                {
                    double lx = xs[i] - _id.X0, lz = zs[i] - _id.Z0;
                    if (noVehicle.Count > 0 && InTris(noVehicle, lx, lz)) motorOut = true;
                    SacredZone z;
                    if (_g.Sacred != null && _g.Sacred.TryGetZone(xs[i], zs[i], out z))
                    {
                        motorOut = true;
                        if (z.Kind != SacredZoneKind.HeritageSquare) allOut = true;
                    }
                }
                if (allOut) return 0;
                if (motorOut) mask &= VehicleClasses.Bit(VehicleClass.Bicycle);
                return mask;
            }

            private void Edge(Piece p, RoadRecord r, int i0, int i1, bool reverseRing, Dictionary<long, int> count, TileHeightSampler sampler,
                              RoadOptions opts, List<Tri> noVehicle)
            {
                int[] pts = r.Points;
                // Densified centreline in game metres with along values.
                var cx = new List<double>();
                var cz = new List<double>();
                var cs = new List<double>();
                for (int i = i0; i <= i1; i++)
                {
                    double x = _id.X0 + pts[2 * i] / 100.0, z = _id.Z0 + pts[2 * i + 1] / 100.0;
                    if (i > i0)
                    {
                        double px = cx[cx.Count - 1], pz = cz[cz.Count - 1], ps = cs[cs.Count - 1];
                        double len = p.Along[i] - p.Along[i - 1];
                        int sub = (int)Math.Ceiling(len / DensifyM);
                        for (int k = 1; k < sub; k++)
                        {
                            double f = k / (double)sub;
                            cx.Add(px + (x - px) * f);
                            cz.Add(pz + (z - pz) * f);
                            cs.Add(ps + len * f);
                        }
                    }
                    cx.Add(x);
                    cz.Add(z);
                    cs.Add(p.Along[i]);
                }
                int n = cx.Count;
                if (n < 2) return;
                double edgeLen = cs[n - 1] - cs[0];
                if (edgeLen < 0.5) return;

                // Lanes per direction at the middle of the edge. A mapped ring way running anticlockwise is driven
                // against its point order, so the circulation stays clockwise.
                double sMid = 0.5 * (cs[0] + cs[n - 1]);
                RoadProfile prof = RoadWidthModel.ProfileFrom(r, p.Attr, p.Prof, sMid);
                int lanes0 = prof.LanesFwd, lanes1 = prof.LanesBwd;
                if (p.Oneway)
                {
                    int l = Math.Max(1, (int)prof.Lanes);
                    lanes0 = reverseRing ? 0 : l;
                    lanes1 = reverseRing ? l : 0;
                }
                if (lanes0 + lanes1 == 0) lanes0 = 1;
                // A two-way road under 5.5 m real is one shared lane in the design; its two directions keep left on their own
                // half (±w/4), so a stop line never sits on the other direction's lane start at a junction mouth.
                bool shared = !p.Oneway && lanes0 == 1 && lanes1 == 1 && prof.Lanes <= 1;
                int total = lanes0 + lanes1;
                // Right normals per vertex (averaged, mitred).
                var nx = new double[n];
                var nz = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double ax = 0, az = 0;
                    if (i > 0)
                    {
                        double dx = cx[i] - cx[i - 1], dz = cz[i] - cz[i - 1];
                        Norm(ref dx, ref dz);
                        ax += dz;
                        az += -dx;
                    }
                    if (i < n - 1)
                    {
                        double dx = cx[i + 1] - cx[i], dz = cz[i + 1] - cz[i];
                        Norm(ref dx, ref dz);
                        ax += dz;
                        az += -dx;
                    }
                    double l = Math.Sqrt(ax * ax + az * az);
                    if (l < 1e-9)
                    {
                        nx[i] = 1;
                        nz[i] = 0;
                        continue;
                    }
                    ax /= l;
                    az /= l;
                    // Mitre: scale by 1/cos(half angle), at most 2.
                    double c = 1.0;
                    if (i > 0 && i < n - 1)
                    {
                        double dx = cx[i] - cx[i - 1], dz = cz[i] - cz[i - 1];
                        Norm(ref dx, ref dz);
                        c = ax * dz - az * dx;
                        c = Math.Max(0.5, Math.Abs(c));
                    }
                    nx[i] = ax / c;
                    nz[i] = az / c;
                }

                // Class mask: the piece's, minus sacred zones (sampled along the centreline).
                uint mask = SacredMask(p.Mask, cx.ToArray(), cz.ToArray(), n, noVehicle);
                if (mask == 0) return;
                AreaType area = RoadWidthModel.AreaOf(p.Attr);
                float free, peak;
                TrafficTables.Speeds(r.RoadClass, area, p.Service, out free, out peak);
                if (p.Attr.MaxspeedKmh > 0) free = Math.Min(free, p.Attr.MaxspeedKmh);

                // Shared lanes: directional masks so any two oncoming bodies can pass by shifting left (W2_DESIGN 4.4:
                // under 4.5 m cars run one way only).
                float wMin = float.MaxValue;
                for (int i = 0; i < n; i++) wMin = Math.Min(wMin, p.Prof.WidthAt(cs[i]));
                uint maskF = mask, maskB = mask;
                if (shared) SharedMasks(r, wMin, ref maskF, ref maskB);

                long k0 = Key(pts[2 * i0], pts[2 * i0 + 1]), k1 = Key(pts[2 * i1], pts[2 * i1 + 1]);
                bool cut0 = i0 == p.First && r.HasPrevContext, cut1 = i1 == p.Last && r.HasNextContext;
                int deg0, deg1;
                _arms.TryGetValue(k0, out deg0);
                _arms.TryGetValue(k1, out deg1);

                var fLanes = new List<int>();
                var bLanes = new List<int>();
                float lift = RoadMesher.LiftOf(r, opts);
                for (int dir = 0; dir < 2; dir++)
                {
                    int count2 = dir == 0 ? lanes0 : lanes1;
                    for (int k = 0; k < count2; k++)
                    {
                        var xs = new double[n];
                        var zs = new double[n];
                        var ys = new float[n];
                        float offMid = 0f;
                        for (int i = 0; i < n; i++)
                        {
                            int src = dir == 0 ? i : n - 1 - i;
                            float w = p.Prof.WidthAt(cs[src]);
                            float shift = p.Dual ? -0.5f * (w - p.Real) : 0f;
                            // Right-positive lateral in the way's point order, as MarkingMesher paints it: one-way carriageways
                            // spread their lanes over the width; two-way roads keep the divider on the centreline, travel in
                            // point order in the left half and against it in the right half; lane k = 0 is the kerb lane.
                            float lw = p.Oneway ? w / total : 0.5f * w / count2;
                            float lat = dir == 0 ? shift - 0.5f * w + (k + 0.5f) * lw : shift + 0.5f * w - (k + 0.5f) * lw;
                            xs[i] = cx[src] + nx[src] * lat;
                            zs[i] = cz[src] + nz[src] * lat;
                            if (src == n / 2) offMid = lat;
                            float h;
                            if (!sampler.TryHeightClamped(xs[i], zs[i], out h)) h = 0f;
                            ys[i] = h + lift;
                        }
                        var lane = new Lane
                        {
                            Kind = LaneKind.Road, X = xs, Z = zs, Y = ys, S = new float[n], N = n, TileKey = _id.Key,
                            Width = p.Oneway ? wMin / total : 0.5f * wMin / count2, FreeKmh = free, PeakKmh = peak, Mask = dir == 0 ? maskF : maskB,
                            Offset = offMid, Index = (byte)k, Count = (byte)count2, Shared = shared, Ring = p.Ring, Priority = p.Ring,
                            Forward = dir == 0, WayId = (long)r.OsmWayId, Class = r.RoadClass, Area = area, TwoWay = !p.Oneway,
                        };
                        Finish(lane);
                        // Trim at both ends by the node setbacks (point-order node 0 is where forward lanes start).
                        float sb0 = Setback(k0, deg0, cut0, lane.Length, wMin), sb1 = Setback(k1, deg1, cut1, lane.Length, wMin);
                        if (!Trim(lane, dir == 0 ? sb0 : sb1, dir == 0 ? sb1 : sb0)) continue;
                        int local = _r.Lanes.Count;
                        _r.Lanes.Add(lane);
                        (dir == 0 ? fLanes : bLanes).Add(local);
                    }
                }
                if (fLanes.Count == 0 && bLanes.Count == 0) return;
                // Adjacent lanes (same direction) and shared twins, as local indices.
                Adjacent(fLanes);
                Adjacent(bLanes);
                if (shared && fLanes.Count == 1 && bLanes.Count == 1)
                {
                    _r.Lanes[fLanes[0]].Twin = bLanes[0];
                    _r.Lanes[bLanes[0]].Twin = fLanes[0];
                }

                // Edge ends at both nodes (point order: node 0 at i0, node 1 at i1). Forward lanes leave node 0 and
                // arrive at node 1.
                float wEdge = p.Prof.WidthAt(sMid);
                int rank = RoadStyle.Priority(r.RoadClass);
                AddEnd(k0, cx[0], cz[0], cx[1] - cx[0], cz[1] - cz[0], bLanes, fLanes, wEdge, rank, r, p, cut0);
                AddEnd(k1, cx[n - 1], cz[n - 1], cx[n - 2] - cx[n - 1], cz[n - 2] - cz[n - 1], fLanes, bLanes, wEdge, rank, r, p, cut1);
            }

            private void Adjacent(List<int> lanes)
            {
                lanes.Sort((a, b) => _r.Lanes[a].Index.CompareTo(_r.Lanes[b].Index));
                for (int i = 0; i < lanes.Count; i++)
                {
                    if (i > 0) _r.Lanes[lanes[i]].Left = lanes[i - 1];
                    if (i + 1 < lanes.Count) _r.Lanes[lanes[i]].Right = lanes[i + 1];
                }
            }

            private void AddEnd(long key, double x, double z, double dx, double dz, List<int> incoming, List<int> outgoing, float w, int rank,
                                RoadRecord r, Piece p, bool cut)
            {
                Norm(ref dx, ref dz);
                var e = new EdgeEnd
                {
                    TileKey = _id.Key, In = incoming.ToArray(), Out = outgoing.ToArray(), DirX = dx, DirZ = dz, Width = w, Rank = rank,
                    WayId = (long)r.OsmWayId, Ring = p.Ring, Cut = cut, TwoWay = !p.Oneway,
                };
                _r.Ends.Add(new EndRef { Key = key, X = x, Z = z, End = e });
            }

            /// <summary>Set the lane back from a node: junctions (3+ incident pieces) by half the widest carriageway there
            /// plus 1.5 m (at least 4 m; a synthesised roundabout by its ring), seams and plain joints by 2 m, dead ends
            /// not at all; never more than 45% of the lane.</summary>
            private float Setback(long key, int degree, bool cut, float laneLen, float ownWidth)
            {
                float sb;
                if (degree >= 3)
                {
                    float w;
                    _nodeWidth.TryGetValue(key, out w);
                    sb = Math.Max(4f, 0.5f * w + 1.5f);
                    // Two stop lines on arms meeting at angle θ are 2·d·sin(θ/2) apart: keep two bus bodies clear.
                    double theta = MinArmAngle(key);
                    float clear = (float)(3.1 / (2.0 * Math.Sin(0.5 * theta)));
                    sb = Math.Max(sb, Math.Min(25f, clear));
                }
                else if (degree == 2 || cut)
                {
                    sb = 0f; // plain joints and tile seams link lane to lane (connectors only where lanes do not line up)
                }
                else
                {
                    sb = 0f;
                }
                float ring;
                if (_ringRadius.TryGetValue(key, out ring)) sb = Math.Max(sb, ring + 0.5f * ownWidth + 2f);
                return Math.Min(sb, 0.45f * laneLen);
            }

            private static bool Trim(Lane l, float a, float b)
            {
                float len = l.Length;
                if (len - a - b < 0.5f) return false;
                var xs = new List<double>();
                var zs = new List<double>();
                var ys = new List<float>();
                double px, pz;
                float py, h;
                PointAt(l, a, out px, out pz, out py, out h);
                xs.Add(px);
                zs.Add(pz);
                ys.Add(py);
                for (int i = 0; i < l.N; i++)
                    if (l.S[i] > a + 1e-3f && l.S[i] < len - b - 1e-3f)
                    {
                        xs.Add(l.X[i]);
                        zs.Add(l.Z[i]);
                        ys.Add(l.Y[i]);
                    }
                PointAt(l, len - b, out px, out pz, out py, out h);
                xs.Add(px);
                zs.Add(pz);
                ys.Add(py);
                l.X = xs.ToArray();
                l.Z = zs.ToArray();
                l.Y = ys.ToArray();
                l.N = l.X.Length;
                l.S = new float[l.N];
                Finish(l);
                return l.Length >= 0.5f;
            }

            /// <summary>Directional class masks of a shared (narrow two-way) road: each direction keeps to its half, so a
            /// pair of oncoming bodies passes when h_a + h_b + 0.05 ≤ w/2; under 4.5 m cars and larger keep only the
            /// way's chosen direction (by its id), and the greedy pass removes any class pair that cannot pass from the
            /// other direction.</summary>
            private static void SharedMasks(RoadRecord r, float w, ref uint fwd, ref uint bwd)
            {
                bool forwardPreferred = (SimRng.Mix((ulong)r.OsmWayId, 0x53484152) & 1) == 0;
                uint carLike = VehicleClasses.Bit(VehicleClass.Car) | VehicleClasses.Bit(VehicleClass.Taxi) | VehicleClasses.Bit(VehicleClass.Suv) |
                               VehicleClasses.Bit(VehicleClass.Service) | VehicleClasses.Bit(VehicleClass.Microbus) |
                               VehicleClasses.Bit(VehicleClass.Tempo) | VehicleClasses.Bit(VehicleClass.Tractor);
                if (w < 4.5f)
                {
                    if (forwardPreferred) bwd &= ~carLike;
                    else fwd &= ~carLike;
                }
                for (int guard = 0; guard < 32; guard++)
                {
                    int worstA = -1, worstB = -1;
                    for (int a = 0; a < VehicleClasses.Count && worstA < 0; a++)
                    {
                        if ((fwd & (1u << a)) == 0) continue;
                        for (int b = 0; b < VehicleClasses.Count; b++)
                        {
                            if ((bwd & (1u << b)) == 0) continue;
                            float ha = TrafficTables.HalfWidthOf((VehicleClass)a), hb = TrafficTables.HalfWidthOf((VehicleClass)b);
                            if (0.5f * w < ha + hb + 0.05f)
                            {
                                worstA = a;
                                worstB = b;
                                break;
                            }
                        }
                    }
                    if (worstA < 0) return;
                    // Remove the wider class from the non-preferred direction.
                    float wa = TrafficTables.HalfWidthOf((VehicleClass)worstA), wb = TrafficTables.HalfWidthOf((VehicleClass)worstB);
                    if (forwardPreferred)
                    {
                        if (wb >= wa) bwd &= ~(1u << worstB);
                        else fwd &= ~(1u << worstA);
                    }
                    else
                    {
                        if (wa >= wb) fwd &= ~(1u << worstA);
                        else bwd &= ~(1u << worstB);
                    }
                }
            }

            private static double Sq(double v)
            {
                return v * v;
            }
        }

        /// <summary>How far an agent of half width <paramref name="h"/> shifts left on a shared lane of width
        /// <paramref name="w"/> to pass an oncoming one: min(w/2 − h − 0.1, max(0.6, h + 0.15)), never negative.</summary>
        public static float PassOffset(float w, float h)
        {
            float o = Math.Min(0.5f * w - h - 0.1f, Math.Max(0.6f, h + 0.15f));
            return o > 0f ? o : 0f;
        }
    }
}
