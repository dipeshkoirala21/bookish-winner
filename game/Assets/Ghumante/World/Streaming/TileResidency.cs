using System;
using System.Collections.Generic;
using Ghumante.Core.Streaming;

namespace Ghumante.World.Streaming
{
    /// <summary>
    /// Bookkeeping of which selected nodes are queued, loading, ready and visible, and the swap rule that keeps the
    /// screen free of holes and overlaps while the selection changes (M1_PLAN contract: "keep drawing an outgoing node
    /// until the desired nodes overlapping it are ready").
    /// <list type="bullet">
    /// <item>A <b>desired</b> node (in the latest selection) is queued until <see cref="BeginLoad"/>, loading until
    /// <see cref="EndLoad"/>, then ready.</item>
    /// <item>A visible node that is no longer desired <b>retires</b>: it stays visible until every desired node that
    /// overlaps it is ready, then it is released and those nodes appear in the same <see cref="Resolve"/>. Ready
    /// desired nodes stay hidden while a retiring node overlaps them, so visible nodes never overlap.</item>
    /// <item>Ready nodes that are neither desired nor visible are released at once; loading nodes that are no longer
    /// desired are dropped when their load ends.</item>
    /// <item>Resident nodes (loading + ready) are capped at <see cref="MaxResident"/>: while desired nodes wait and the
    /// cap is reached, retiring nodes are released farthest first (a brief hole far away beats a stall).</item>
    /// </list>
    /// Deterministic; allocation-free after warm-up; main thread only. Engine-free.
    /// </summary>
    public sealed class TileResidency
    {
        public enum NodeState : byte
        {
            None = 0,
            Queued = 1,
            Loading = 2,
            Ready = 3,
        }

        private sealed class Record
        {
            public SelectedNode Node;
            public NodeState State;
            public bool Desired;
            public bool Visible;
            public bool Empty;
        }

        private readonly Dictionary<SelectedNode, Record> _records = new Dictionary<SelectedNode, Record>(512);
        private readonly Stack<Record> _free = new Stack<Record>(64);
        private readonly List<SelectedNode> _desired = new List<SelectedNode>(512);
        private readonly HashSet<SelectedNode> _resident = new HashSet<SelectedNode>();
        private readonly List<SelectedNode> _queue = new List<SelectedNode>(512);
        private readonly List<SelectedNode> _unload = new List<SelectedNode>(128);
        private readonly List<Record> _retiring = new List<Record>(128);
        private readonly List<SelectedNode> _drop = new List<SelectedNode>(128);
        private readonly TileLoadPlanner _planner = new TileLoadPlanner();
        private double _x, _z;
        private int _maxResident;
        private int _loading, _ready, _visible, _readyDesired;

        public TileResidency(int maxResident)
        {
            MaxResident = maxResident;
        }

        /// <summary>Cap on loading + ready nodes (<see cref="StreamingConfig.MaxResidentTiles"/>).</summary>
        public int MaxResident
        {
            get { return _maxResident; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                _maxResident = value;
            }
        }

        /// <summary>The latest selection, in selection order (nearest first).</summary>
        public IReadOnlyList<SelectedNode> Desired
        {
            get { return _desired; }
        }

        /// <summary>Desired nodes waiting to load, nearest first (refreshed by <see cref="SetDesired"/> and
        /// <see cref="Resolve"/>; <see cref="BeginLoad"/> removes its node).</summary>
        public IReadOnlyList<SelectedNode> Queue
        {
            get { return _queue; }
        }

        public int DesiredCount
        {
            get { return _desired.Count; }
        }

        public int QueuedCount
        {
            get { return _queue.Count; }
        }

        public int LoadingCount
        {
            get { return _loading; }
        }

        public int ReadyCount
        {
            get { return _ready; }
        }

        public int VisibleCount
        {
            get { return _visible; }
        }

        /// <summary>Desired nodes that are ready (as of the last <see cref="Resolve"/>).</summary>
        public int ReadyDesiredCount
        {
            get { return _readyDesired; }
        }

        /// <summary>Loading plus ready nodes.</summary>
        public int ResidentCount
        {
            get { return _loading + _ready; }
        }

        /// <summary>True while another load may start without passing <see cref="MaxResident"/>.</summary>
        public bool CanStartLoad
        {
            get { return _loading + _ready < _maxResident; }
        }

        public NodeState StateOf(SelectedNode node)
        {
            Record r;
            return _records.TryGetValue(node, out r) ? r.State : NodeState.None;
        }

        public bool IsDesired(SelectedNode node)
        {
            Record r;
            return _records.TryGetValue(node, out r) && r.Desired;
        }

        public bool IsVisible(SelectedNode node)
        {
            Record r;
            return _records.TryGetValue(node, out r) && r.Visible;
        }

        /// <summary>Replace the selection (<see cref="TileSelector.Select"/>'s output) seen from focus (x, z).</summary>
        public void SetDesired(IReadOnlyList<SelectedNode> desired, double x, double z)
        {
            if (desired == null) throw new ArgumentNullException(nameof(desired));
            _x = x;
            _z = z;
            foreach (Record r in _records.Values) r.Desired = false;
            _desired.Clear();
            for (int i = 0; i < desired.Count; i++)
            {
                SelectedNode n = desired[i];
                Record r;
                if (!_records.TryGetValue(n, out r))
                {
                    r = _free.Count > 0 ? _free.Pop() : new Record();
                    r.Node = n;
                    r.State = NodeState.Queued;
                    r.Visible = false;
                    r.Empty = false;
                    _records.Add(n, r);
                }
                if (r.Desired) continue; // duplicate in the input
                r.Desired = true;
                _desired.Add(n);
            }
            // Queued nodes that dropped out of the selection were never started: forget them.
            _drop.Clear();
            foreach (Record r in _records.Values)
                if (!r.Desired && r.State == NodeState.Queued) _drop.Add(r.Node);
            for (int i = 0; i < _drop.Count; i++) Forget(_drop[i]);
            _planner.Plan(_desired, _resident, _x, _z, _queue, _unload);
        }

        /// <summary>Mark a queued desired node as loading.</summary>
        public void BeginLoad(SelectedNode node)
        {
            Record r;
            if (!_records.TryGetValue(node, out r) || r.State != NodeState.Queued)
                throw new InvalidOperationException("node " + node + " is not queued");
            r.State = NodeState.Loading;
            _resident.Add(node);
            _loading++;
            _queue.Remove(node);
        }

        /// <summary>
        /// A load finished. Returns true when the node is still desired: it is now ready (hidden until a
        /// <see cref="Resolve"/> shows it). Returns false when it is no longer wanted: the record is gone and the caller
        /// discards the result. <paramref name="empty"/> marks a node with nothing to draw (or a failed load); it still
        /// counts as ready, so it never blocks a retiring node.
        /// </summary>
        public bool EndLoad(SelectedNode node, bool empty)
        {
            Record r;
            if (!_records.TryGetValue(node, out r) || r.State != NodeState.Loading)
                throw new InvalidOperationException("node " + node + " is not loading");
            _loading--;
            if (!r.Desired)
            {
                _resident.Remove(node);
                Recycle(node, r);
                return false;
            }
            r.State = NodeState.Ready;
            r.Empty = empty;
            _ready++;
            return true;
        }

        /// <summary>True when the node is ready with nothing to draw.</summary>
        public bool IsEmpty(SelectedNode node)
        {
            Record r;
            return _records.TryGetValue(node, out r) && r.Empty;
        }

        /// <summary>
        /// Apply the swap rule: fills <paramref name="show"/> (ready desired nodes that become visible),
        /// <paramref name="hide"/> (visible nodes to hide but keep) and <paramref name="release"/> (nodes whose views must
        /// be destroyed; their records are gone). Lists are cleared first. Apply hides and releases before shows.
        /// </summary>
        public void Resolve(List<SelectedNode> show, List<SelectedNode> hide, List<SelectedNode> release)
        {
            if (show == null) throw new ArgumentNullException(nameof(show));
            if (hide == null) throw new ArgumentNullException(nameof(hide));
            if (release == null) throw new ArgumentNullException(nameof(release));
            show.Clear();
            hide.Clear();
            release.Clear();
            _planner.Plan(_desired, _resident, _x, _z, _queue, _unload);

            // Non-desired resident nodes, farthest first: drop the hidden ones, keep visible ones as retiring.
            _retiring.Clear();
            for (int i = 0; i < _unload.Count; i++)
            {
                Record r = _records[_unload[i]];
                if (r.State != NodeState.Ready) continue;
                if (r.Visible) _retiring.Add(r);
                else release.Add(r.Node);
            }

            // A retiring node goes once every desired node overlapping it is ready.
            for (int k = _retiring.Count - 1; k >= 0; k--)
            {
                if (!Covered(_retiring[k].Node)) continue;
                release.Add(_retiring[k].Node);
                _retiring.RemoveAt(k);
            }

            // Over the cap with work waiting: give up the farthest retiring nodes (every node in release is ready).
            int released = release.Count;
            while (_queue.Count > 0 && _retiring.Count > 0 && _loading + _ready - released >= _maxResident)
            {
                release.Add(_retiring[0].Node);
                _retiring.RemoveAt(0);
                released++;
            }

            for (int i = 0; i < release.Count; i++)
            {
                SelectedNode n = release[i];
                Record r = _records[n];
                if (r.Visible) _visible--;
                _ready--;
                _resident.Remove(n);
                Recycle(n, r);
            }

            // Ready desired nodes show unless a retiring node still covers part of them.
            _readyDesired = 0;
            for (int i = 0; i < _desired.Count; i++)
            {
                Record r = _records[_desired[i]];
                if (r.State != NodeState.Ready) continue;
                _readyDesired++;
                bool blocked = false;
                for (int k = 0; k < _retiring.Count && !blocked; k++)
                    blocked = TileArea.Overlaps(_retiring[k].Node.Area, r.Node.Area);
                if (!blocked && !r.Visible)
                {
                    r.Visible = true;
                    _visible++;
                    show.Add(r.Node);
                }
                else if (blocked && r.Visible)
                {
                    r.Visible = false;
                    _visible--;
                    hide.Add(r.Node);
                }
            }
            if (release.Count > 0) _planner.Plan(_desired, _resident, _x, _z, _queue, _unload);
        }

        /// <summary>
        /// Forget the selection and release everything that is ready (into <paramref name="release"/>). Loading nodes
        /// stay until <see cref="EndLoad"/>, which then returns false.
        /// </summary>
        public void Clear(List<SelectedNode> release)
        {
            if (release == null) throw new ArgumentNullException(nameof(release));
            release.Clear();
            _desired.Clear();
            _queue.Clear();
            _unload.Clear();
            _drop.Clear();
            foreach (Record r in _records.Values)
            {
                r.Desired = false;
                if (r.State == NodeState.Ready) release.Add(r.Node);
                else if (r.State == NodeState.Queued) _drop.Add(r.Node);
            }
            for (int i = 0; i < release.Count; i++)
            {
                Record r = _records[release[i]];
                if (r.Visible) _visible--;
                _ready--;
                _resident.Remove(r.Node);
                Recycle(r.Node, r);
            }
            for (int i = 0; i < _drop.Count; i++) Forget(_drop[i]);
            _readyDesired = 0;
        }

        private bool Covered(SelectedNode retiring)
        {
            for (int i = 0; i < _desired.Count; i++)
            {
                SelectedNode d = _desired[i];
                if (!TileArea.Overlaps(d.Area, retiring.Area)) continue;
                if (_records[d].State != NodeState.Ready) return false;
            }
            return true;
        }

        private void Forget(SelectedNode node)
        {
            Record r;
            if (_records.TryGetValue(node, out r)) Recycle(node, r);
        }

        private void Recycle(SelectedNode node, Record r)
        {
            _records.Remove(node);
            r.State = NodeState.None;
            r.Desired = false;
            r.Visible = false;
            r.Empty = false;
            _free.Push(r);
        }
    }
}
