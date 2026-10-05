using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Ghumante.Core.Synth;
using UnityEngine;

namespace Ghumante.Audio
{
    /// <summary>
    /// The baked bank at runtime (W2_DESIGN 7.1 "Baked bank"): a background thread renders every
    /// <see cref="ProceduralBank"/> sound and variant from the region seed, and the main thread turns them into
    /// AudioClips with <c>AudioClip.Create</c> + <c>SetData</c> in slices, within a per-frame time budget (2 ms, the
    /// ARCHITECTURE 7.2 upload cap). Low bakes fewer variants at ≤ 16 kHz. Sounds become playable as they arrive
    /// (footsteps and horns first); asking for one that is not ready yet returns false.
    /// </summary>
    internal sealed class ClipBank : IDisposable
    {
        private struct Rendered
        {
            public BankSound Sound;
            public int Variant;
            public int Rate;
            public float[] Data;
        }

        private const int SliceSamples = 4096;

        private readonly AudioClip[][] _clips;
        private readonly int[] _soundIndex;
        private readonly ConcurrentQueue<Rendered> _ready = new ConcurrentQueue<Rendered>();
        private readonly float[] _slice = new float[SliceSamples];
        private readonly Stopwatch _watch = new Stopwatch();
        private readonly bool _low;
        private readonly uint _seed;
        private Thread _worker;
        private volatile bool _cancel;
        private int _expected;
        private int _done;

        // In-flight upload (a long sound may span frames).
        private bool _uploading;
        private Rendered _current;
        private AudioClip _currentClip;
        private int _offset;

        public ClipBank(uint seed, bool low)
        {
            _seed = seed;
            _low = low;
            int maxSound = 0;
            for (int i = 0; i < ProceduralBank.Count; i++) maxSound = Math.Max(maxSound, (int)ProceduralBank.InfoAt(i).Sound);
            _soundIndex = new int[maxSound + 1];
            for (int i = 0; i < _soundIndex.Length; i++) _soundIndex[i] = -1;
            _clips = new AudioClip[ProceduralBank.Count][];
            for (int i = 0; i < ProceduralBank.Count; i++)
            {
                BankSoundInfo info = ProceduralBank.InfoAt(i);
                _soundIndex[(int)info.Sound] = i;
                int variants = low ? info.LowVariants : info.Variants;
                _clips[i] = new AudioClip[variants];
                _expected += variants;
            }
        }

        /// <summary>Clips created so far / in total.</summary>
        public int Done
        {
            get { return _done; }
        }

        public int Expected
        {
            get { return _expected; }
        }

        public bool Complete
        {
            get { return _done >= _expected; }
        }

        /// <summary>Approximate resident bytes of the created clips (PCM16 equivalent).</summary>
        public long Bytes { get; private set; }

        /// <summary>Starts the background render (idempotent).</summary>
        public void Start()
        {
            if (_worker != null) return;
            _worker = new Thread(RenderAll) { IsBackground = true, Name = "Ghumante audio bank", Priority = System.Threading.ThreadPriority.BelowNormal };
            _worker.Start();
        }

        private static int Order(BankSound s)
        {
            int v = (int)s;
            if (v <= (int)BankSound.FootstepBarefoot) return 0;
            if (v >= (int)BankSound.HornMoto && v <= (int)BankSound.GearGrind) return 1;
            if (v >= (int)BankSound.BedBase) return 2;
            if (v >= (int)BankSound.ShrineBellSmall && v <= (int)BankSound.GreatBell) return 3;
            return 4;
        }

        private void RenderAll()
        {
            try
            {
                for (int pass = 0; pass <= 4 && !_cancel; pass++)
                {
                    for (int i = 0; i < ProceduralBank.Count && !_cancel; i++)
                    {
                        BankSoundInfo info = ProceduralBank.InfoAt(i);
                        if (Order(info.Sound) != pass) continue;
                        int variants = _low ? info.LowVariants : info.Variants;
                        int rate = _low ? ProceduralBank.LowRate(info.SampleRate) : info.SampleRate;
                        for (int v = 0; v < variants && !_cancel; v++)
                        {
                            float[] data = ProceduralBank.Render(info.Sound, v, rate, _seed);
                            _ready.Enqueue(new Rendered { Sound = info.Sound, Variant = v, Rate = rate, Data = data });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        /// <summary>Main thread: creates clips for up to <paramref name="budgetMs"/> of work.</summary>
        public void Upload(float budgetMs)
        {
            if (Complete && !_uploading) return;
            _watch.Restart();
            while (_watch.Elapsed.TotalMilliseconds < budgetMs)
            {
                if (!_uploading)
                {
                    if (!_ready.TryDequeue(out _current)) return;
                    if (_current.Data == null || _current.Data.Length == 0)
                    {
                        _done++;
                        continue;
                    }
                    string name = "ghm_bank_" + _current.Sound + "_" + _current.Variant;
                    _currentClip = AudioClip.Create(name, _current.Data.Length, 1, _current.Rate, false);
                    _offset = 0;
                    _uploading = true;
                }
                int left = _current.Data.Length - _offset;
                float[] chunk;
                if (left >= SliceSamples)
                {
                    Array.Copy(_current.Data, _offset, _slice, 0, SliceSamples);
                    chunk = _slice;
                }
                else
                {
                    // The tail of a clip (load time only, never per frame).
                    chunk = new float[left];
                    Array.Copy(_current.Data, _offset, chunk, 0, left);
                }
                _currentClip.SetData(chunk, _offset);
                _offset += chunk.Length;
                if (_offset >= _current.Data.Length) Finish();
            }
        }

        private void Finish()
        {
            int idx = (int)_current.Sound < _soundIndex.Length ? _soundIndex[(int)_current.Sound] : -1;
            if (idx >= 0 && _current.Variant < _clips[idx].Length) _clips[idx][_current.Variant] = _currentClip;
            Bytes += _current.Data.Length * 2L;
            _done++;
            _uploading = false;
            _currentClip = null;
            _current = default;
        }

        /// <summary>A clip of <paramref name="s"/>; the variant wraps over what this tier baked. False until baked.</summary>
        public bool TryGet(BankSound s, int variant, out AudioClip clip)
        {
            clip = null;
            int si = (int)s;
            if (si < 0 || si >= _soundIndex.Length) return false;
            int idx = _soundIndex[si];
            if (idx < 0) return false;
            AudioClip[] set = _clips[idx];
            if (set.Length == 0) return false;
            int v = variant < 0 ? 0 : variant % set.Length;
            clip = set[v];
            if (clip == null)
            {
                // Fall back to any baked variant of the same sound.
                for (int k = 0; k < set.Length && clip == null; k++) clip = set[k];
            }
            return clip != null;
        }

        public int VariantsOf(BankSound s)
        {
            int si = (int)s;
            if (si < 0 || si >= _soundIndex.Length || _soundIndex[si] < 0) return 0;
            return _clips[_soundIndex[si]].Length;
        }

        public void Dispose()
        {
            _cancel = true;
            if (_worker != null && _worker.IsAlive) _worker.Join(2000);
            _worker = null;
            while (_ready.TryDequeue(out _))
            {
            }
            for (int i = 0; i < _clips.Length; i++)
            {
                for (int v = 0; v < _clips[i].Length; v++)
                {
                    if (_clips[i][v] != null) UnityEngine.Object.Destroy(_clips[i][v]);
                    _clips[i][v] = null;
                }
            }
            if (_currentClip != null) UnityEngine.Object.Destroy(_currentClip);
            _currentClip = null;
            _uploading = false;
        }
    }
}
