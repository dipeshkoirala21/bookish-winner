using System.Threading;

namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Single-writer / single-reader parameter hand-off from the main thread to the audio thread (W2_DESIGN 7.1:
    /// "a 64-byte parameter struct per voice with a sequence counter"). A seqlock: the writer bumps the sequence
    /// to odd, writes, bumps it to even; the reader copies and retries once if the sequence moved, otherwise keeps
    /// its last good copy. No locks, no allocation, never blocks either thread.
    /// </summary>
    public sealed class ParamMailbox<T> where T : struct
    {
        private T _slot;
        private int _seq;
        private int _readSeq;
        private T _last;

        /// <summary>Main thread: publishes new targets.</summary>
        public void Write(in T value)
        {
            Interlocked.Increment(ref _seq);
            _slot = value;
            Interlocked.Increment(ref _seq);
        }

        /// <summary>Number of completed writes (even sequence / 2).</summary>
        public int Version
        {
            get { return Volatile.Read(ref _seq) >> 1; }
        }

        /// <summary>
        /// Audio thread: the newest consistent value. Returns true when it changed since the previous read.
        /// Before the first write it returns <c>default(T)</c>.
        /// </summary>
        public bool Read(out T value)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int s1 = Volatile.Read(ref _seq);
                if ((s1 & 1) != 0) continue;
                T copy = _slot;
                Thread.MemoryBarrier();
                int s2 = Volatile.Read(ref _seq);
                if (s1 != s2) continue;
                bool changed = s1 != _readSeq;
                _readSeq = s1;
                _last = copy;
                value = copy;
                return changed;
            }
            value = _last;
            return false;
        }
    }
}
