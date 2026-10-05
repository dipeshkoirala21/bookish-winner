using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Streaming
{
    /// <summary>
    /// Diffs the desired selection (<see cref="TileSelector.Select"/>) against what is resident and orders the work
    /// (ARCHITECTURE.md 7.2): <c>toLoad</c> holds desired nodes that are not resident, nearest first (distance to
    /// the area's closest point, then area key, then source key); <c>toUnload</c> holds resident nodes that are no
    /// longer desired, farthest first, so a caller that evicts before loading frees the least useful memory first.
    /// A caller should keep drawing an outgoing node until the desired nodes overlapping it
    /// (<see cref="TileArea.Overlaps"/>) are ready, so coverage never has holes.
    /// <para>Deterministic; allocation-free after warm-up when <c>resident</c> is a <see cref="HashSet{T}"/> or a
    /// <see cref="List{T}"/>. Not thread-safe.</para>
    /// </summary>
    public sealed class TileLoadPlanner
    {
        private struct Item
        {
            public double D2;
            public SelectedNode Node;
        }

        private struct NearFirst : IComparer<Item>
        {
            public int Compare(Item a, Item b)
            {
                int c = a.D2.CompareTo(b.D2);
                return c != 0 ? c : a.Node.CompareTo(b.Node);
            }
        }

        private struct FarFirst : IComparer<Item>
        {
            public int Compare(Item a, Item b)
            {
                int c = b.D2.CompareTo(a.D2);
                return c != 0 ? c : a.Node.CompareTo(b.Node);
            }
        }

        private readonly HashSet<SelectedNode> _desired = new HashSet<SelectedNode>();
        private readonly HashSet<ulong> _sourceSeen = new HashSet<ulong>();
        private Item[] _load = new Item[64], _unload = new Item[64];
        private int _loadCount, _unloadCount;

        /// <summary>
        /// Clear <paramref name="toLoad"/> and <paramref name="toUnload"/> and fill them with the difference between
        /// <paramref name="desired"/> and <paramref name="resident"/>, prioritised by distance from focus (x, z).
        /// </summary>
        public void Plan(IReadOnlyList<SelectedNode> desired, ICollection<SelectedNode> resident, double x, double z,
                         List<SelectedNode> toLoad, List<SelectedNode> toUnload)
        {
            if (desired == null) throw new ArgumentNullException(nameof(desired));
            if (resident == null) throw new ArgumentNullException(nameof(resident));
            if (toLoad == null) throw new ArgumentNullException(nameof(toLoad));
            if (toUnload == null) throw new ArgumentNullException(nameof(toUnload));
            toLoad.Clear();
            toUnload.Clear();
            _desired.Clear();
            _loadCount = 0;
            _unloadCount = 0;

            for (int i = 0; i < desired.Count; i++)
            {
                SelectedNode n = desired[i];
                if (!_desired.Add(n)) continue;
                if (!resident.Contains(n))
                    Sorting.Add(ref _load, ref _loadCount, new Item { D2 = TileArea.ClosestDistanceSquared(n.Area, x, z), Node = n });
            }

            var set = resident as HashSet<SelectedNode>;
            var list = resident as List<SelectedNode>;
            if (set != null)
            {
                foreach (SelectedNode n in set) ConsiderUnload(n, x, z);
            }
            else if (list != null)
            {
                for (int i = 0; i < list.Count; i++) ConsiderUnload(list[i], x, z);
            }
            else
            {
                foreach (SelectedNode n in resident) ConsiderUnload(n, x, z);
            }

            Sorting.HeapSort(_load, _loadCount, default(NearFirst));
            Sorting.HeapSort(_unload, _unloadCount, default(FarFirst));
            for (int i = 0; i < _loadCount; i++) toLoad.Add(_load[i].Node);
            for (int i = 0; i < _unloadCount; i++)
            {
                // A List may hold duplicates; report each node once.
                if (i > 0 && _unload[i].Node == _unload[i - 1].Node) continue;
                toUnload.Add(_unload[i].Node);
            }
        }

        private void ConsiderUnload(SelectedNode n, double x, double z)
        {
            if (!_desired.Contains(n))
                Sorting.Add(ref _unload, ref _unloadCount, new Item { D2 = TileArea.ClosestDistanceSquared(n.Area, x, z), Node = n });
        }

        /// <summary>
        /// The same diff over plain tiles (the original M1_PLAN signature), for example the decoded source tiles of
        /// a selection (<see cref="Sources"/>) against a tile cache. Same ordering rules, keyed by tile.
        /// </summary>
        public void Plan(IReadOnlyList<TileId> desired, ICollection<TileId> resident, double x, double z,
                         List<TileId> toLoad, List<TileId> toUnload)
        {
            if (desired == null) throw new ArgumentNullException(nameof(desired));
            if (resident == null) throw new ArgumentNullException(nameof(resident));
            if (toLoad == null) throw new ArgumentNullException(nameof(toLoad));
            if (toUnload == null) throw new ArgumentNullException(nameof(toUnload));
            toLoad.Clear();
            toUnload.Clear();
            _desired.Clear();
            _loadCount = 0;
            _unloadCount = 0;

            for (int i = 0; i < desired.Count; i++)
            {
                var n = new SelectedNode(desired[i], desired[i]);
                if (!_desired.Add(n)) continue;
                if (!resident.Contains(desired[i]))
                    Sorting.Add(ref _load, ref _loadCount, new Item { D2 = TileArea.ClosestDistanceSquared(n.Area, x, z), Node = n });
            }
            var set = resident as HashSet<TileId>;
            var list = resident as List<TileId>;
            if (set != null)
            {
                foreach (TileId t in set) ConsiderUnload(new SelectedNode(t, t), x, z);
            }
            else if (list != null)
            {
                for (int i = 0; i < list.Count; i++) ConsiderUnload(new SelectedNode(list[i], list[i]), x, z);
            }
            else
            {
                foreach (TileId t in resident) ConsiderUnload(new SelectedNode(t, t), x, z);
            }

            Sorting.HeapSort(_load, _loadCount, default(NearFirst));
            Sorting.HeapSort(_unload, _unloadCount, default(FarFirst));
            for (int i = 0; i < _loadCount; i++) toLoad.Add(_load[i].Node.Area);
            for (int i = 0; i < _unloadCount; i++)
            {
                if (i > 0 && _unload[i].Node == _unload[i - 1].Node) continue;
                toUnload.Add(_unload[i].Node.Area);
            }
        }

        /// <summary>Clear <paramref name="sources"/> and fill it with the distinct <see cref="SelectedNode.Source"/>
        /// tiles of <paramref name="nodes"/> in order of first appearance (nearest first for a selection): the
        /// decoded tiles a caller needs.</summary>
        public void Sources(IReadOnlyList<SelectedNode> nodes, List<TileId> sources)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            sources.Clear();
            _sourceSeen.Clear();
            for (int i = 0; i < nodes.Count; i++)
                if (_sourceSeen.Add(nodes[i].Source.Key)) sources.Add(nodes[i].Source);
        }
    }
}
