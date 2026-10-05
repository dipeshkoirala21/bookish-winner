using System;
using System.Collections.Generic;

namespace Ghumante.Core.Streaming
{
    /// <summary>
    /// A least-recently-used cache bounded by a byte budget (ARCHITECTURE.md 7.2 and 10: the decoded-tile cache is
    /// 30 / 60 / 90 MB per tier). Each entry carries the byte size the caller declares
    /// (<see cref="TileMemory.EstimateBytes"/> for decoded tiles). <see cref="Put"/> evicts least recently used
    /// entries until the total fits the budget; the entry just put is never evicted by its own insertion, so one
    /// entry larger than the budget stays alone until something else is put. <see cref="TryGet"/> and
    /// <see cref="Get"/> mark an entry as most recently used; <see cref="Contains"/> does not.
    /// <para>
    /// <see cref="Evicted"/> is called for every entry the cache drops on its own: budget evictions, a value
    /// replaced by <see cref="Put"/> with a different value, and <see cref="Clear"/>. <see cref="Remove"/> hands
    /// the value back instead. Entries live in pooled arrays, so steady-state use allocates nothing.
    /// Not thread-safe: guard it with a lock when worker threads share it.
    /// </para>
    /// </summary>
    public sealed class LruByteCache<TKey, TValue>
    {
        private const int None = -1;

        private readonly Dictionary<TKey, int> _index;
        private readonly EqualityComparer<TValue> _valueEq = EqualityComparer<TValue>.Default;
        private TKey[] _keys;
        private TValue[] _values;
        private long[] _bytes;
        private int[] _prev, _next;
        private int _head = None, _tail = None, _free = None, _used;
        private long _budget, _total;

        /// <summary>Called with each entry the cache drops by itself (see the class remarks).</summary>
        public Action<TKey, TValue> Evicted;

        public LruByteCache(long budgetBytes, int initialCapacity = 64, IEqualityComparer<TKey> comparer = null)
        {
            if (budgetBytes < 0) throw new ArgumentOutOfRangeException(nameof(budgetBytes));
            if (initialCapacity < 1) initialCapacity = 1;
            _budget = budgetBytes;
            _index = new Dictionary<TKey, int>(initialCapacity, comparer ?? EqualityComparer<TKey>.Default);
            _keys = new TKey[initialCapacity];
            _values = new TValue[initialCapacity];
            _bytes = new long[initialCapacity];
            _prev = new int[initialCapacity];
            _next = new int[initialCapacity];
        }

        /// <summary>Byte budget; lowering it evicts immediately.</summary>
        public long BudgetBytes
        {
            get { return _budget; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _budget = value;
                Trim(_budget, None);
            }
        }

        /// <summary>Sum of the declared sizes of the entries held.</summary>
        public long Bytes
        {
            get { return _total; }
        }

        public int Count
        {
            get { return _index.Count; }
        }

        public bool Contains(TKey key)
        {
            return _index.ContainsKey(key);
        }

        /// <summary>The value for <paramref name="key"/>, marked most recently used.</summary>
        public bool TryGet(TKey key, out TValue value)
        {
            int slot;
            if (!_index.TryGetValue(key, out slot))
            {
                value = default(TValue);
                return false;
            }
            MoveToFront(slot);
            value = _values[slot];
            return true;
        }

        /// <summary>The value for <paramref name="key"/>, marked most recently used; throws
        /// <see cref="KeyNotFoundException"/> when absent.</summary>
        public TValue Get(TKey key)
        {
            TValue v;
            if (!TryGet(key, out v)) throw new KeyNotFoundException("key not in cache: " + key);
            return v;
        }

        /// <summary>Insert or replace an entry of <paramref name="bytes"/> bytes as most recently used, then evict
        /// least recently used entries until <see cref="Bytes"/> fits <see cref="BudgetBytes"/>.</summary>
        public void Put(TKey key, TValue value, long bytes)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            int slot;
            if (_index.TryGetValue(key, out slot))
            {
                TValue old = _values[slot];
                _total += bytes - _bytes[slot];
                _values[slot] = value;
                _bytes[slot] = bytes;
                MoveToFront(slot);
                if (!_valueEq.Equals(old, value) && Evicted != null) Evicted(key, old);
            }
            else
            {
                slot = Allocate();
                _keys[slot] = key;
                _values[slot] = value;
                _bytes[slot] = bytes;
                _total += bytes;
                LinkFront(slot);
                _index.Add(key, slot);
            }
            Trim(_budget, slot);
        }

        /// <summary>Remove an entry and hand its value back (no <see cref="Evicted"/> call).</summary>
        public bool Remove(TKey key, out TValue value)
        {
            int slot;
            if (!_index.TryGetValue(key, out slot))
            {
                value = default(TValue);
                return false;
            }
            value = _values[slot];
            Release(slot);
            return true;
        }

        public bool Remove(TKey key)
        {
            TValue unused;
            return Remove(key, out unused);
        }

        /// <summary>Evict least recently used entries until <see cref="Bytes"/> is at most
        /// <paramref name="targetBytes"/>.</summary>
        public void Trim(long targetBytes)
        {
            Trim(targetBytes, None);
        }

        /// <summary>Drop every entry, calling <see cref="Evicted"/> for each (least recently used first).</summary>
        public void Clear()
        {
            while (_tail != None)
            {
                int slot = _tail;
                TKey k = _keys[slot];
                TValue v = _values[slot];
                Release(slot);
                if (Evicted != null) Evicted(k, v);
            }
        }

        /// <summary>Keys from most to least recently used (for diagnostics and tests; allocates).</summary>
        public List<TKey> KeysByRecency()
        {
            var list = new List<TKey>(_index.Count);
            for (int s = _head; s != None; s = _next[s]) list.Add(_keys[s]);
            return list;
        }

        private void Trim(long target, int keep)
        {
            while (_total > target && _tail != None)
            {
                int slot = _tail;
                if (slot == keep)
                {
                    slot = _prev[slot];
                    if (slot == None) break;
                }
                TKey k = _keys[slot];
                TValue v = _values[slot];
                Release(slot);
                if (Evicted != null) Evicted(k, v);
            }
        }

        private int Allocate()
        {
            if (_free != None)
            {
                int s = _free;
                _free = _next[s];
                return s;
            }
            if (_used == _keys.Length)
            {
                int cap = _keys.Length * 2;
                Array.Resize(ref _keys, cap);
                Array.Resize(ref _values, cap);
                Array.Resize(ref _bytes, cap);
                Array.Resize(ref _prev, cap);
                Array.Resize(ref _next, cap);
            }
            return _used++;
        }

        private void Release(int slot)
        {
            _index.Remove(_keys[slot]);
            Unlink(slot);
            _total -= _bytes[slot];
            _keys[slot] = default(TKey);
            _values[slot] = default(TValue);
            _bytes[slot] = 0;
            _next[slot] = _free;
            _prev[slot] = None;
            _free = slot;
        }

        private void LinkFront(int slot)
        {
            _prev[slot] = None;
            _next[slot] = _head;
            if (_head != None) _prev[_head] = slot;
            _head = slot;
            if (_tail == None) _tail = slot;
        }

        private void Unlink(int slot)
        {
            int p = _prev[slot], n = _next[slot];
            if (p != None) _next[p] = n;
            else _head = n;
            if (n != None) _prev[n] = p;
            else _tail = p;
        }

        private void MoveToFront(int slot)
        {
            if (_head == slot) return;
            Unlink(slot);
            LinkFront(slot);
        }
    }
}
