using System;

namespace Ghumante.Core.Synth
{
    /// <summary>A request for a real (clip) voice.</summary>
    public struct VoiceRequest
    {
        /// <summary>Unity-style priority: 0 most important … 256 least.</summary>
        public int Priority;
        /// <summary>Estimated level at the listener (dBFS).</summary>
        public float EstDb;
        public CapGroup Cap;
        /// <summary>Protected voices are never stolen (UI, player sounds, ambience beds).</summary>
        public bool Protected;
        /// <summary>One-shots count against the per-second start limit; loops do not.</summary>
        public bool OneShot;

        public static VoiceRequest For(SoundClass c, float estDb, bool oneShot)
        {
            SoundClassInfo i = SoundClasses.Get(c);
            return new VoiceRequest { Priority = i.Priority, EstDb = estDb, Cap = i.Cap, Protected = i.Protected, OneShot = oneShot };
        }
    }

    /// <summary>Why a request did not get a voice.</summary>
    public enum VoiceDenial : byte
    {
        None = 0,
        TooQuiet = 1,
        RateLimited = 2,
        CapReached = 3,
        NoVoice = 4,
    }

    /// <summary>
    /// Real-voice bookkeeping for clip voices (W2_DESIGN 7.1; A §6.1): a fixed set of slots, priorities with
    /// stealing (a less important, unprotected voice is stolen first; among equals the quietest, then the
    /// oldest), same-sound caps (horn 3, bell 3, crow 2, bark 2, NPC footsteps per tier), the −45 dBFS start
    /// cull and the one-shots-per-second limit. Engine-free and deterministic; no allocation after construction.
    /// The caller stops whatever played in a returned slot before reusing it.
    /// </summary>
    public sealed class VoiceAllocator
    {
        private readonly bool[] _active;
        private readonly int[] _priority;
        private readonly float[] _db;
        private readonly CapGroup[] _cap;
        private readonly bool[] _protected;
        private readonly double[] _started;
        private readonly double[] _recentStarts;
        private int _recentHead;
        private VoiceBudget _budget;

        public VoiceAllocator(int slots, in VoiceBudget budget)
        {
            int n = Math.Max(1, slots);
            _active = new bool[n];
            _priority = new int[n];
            _db = new float[n];
            _cap = new CapGroup[n];
            _protected = new bool[n];
            _started = new double[n];
            _budget = budget;
            _recentStarts = new double[Math.Max(1, budget.MaxOneShotsPerSecond)];
            for (int i = 0; i < _recentStarts.Length; i++) _recentStarts[i] = double.NegativeInfinity;
        }

        public int Capacity
        {
            get { return _active.Length; }
        }

        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _active.Length; i++)
                    if (_active[i]) n++;
                return n;
            }
        }

        public VoiceDenial LastDenial { get; private set; }

        public bool IsActive(int slot)
        {
            return slot >= 0 && slot < _active.Length && _active[slot];
        }

        public int CountInGroup(CapGroup g)
        {
            if (g == CapGroup.None) return 0;
            int n = 0;
            for (int i = 0; i < _active.Length; i++)
                if (_active[i] && _cap[i] == g) n++;
            return n;
        }

        /// <summary>
        /// Finds a slot for <paramref name="r"/> at time <paramref name="now"/> (s). Returns false (see
        /// <see cref="LastDenial"/>) when the sound should not play. <paramref name="stolen"/> is true when the slot
        /// held another sound that must be stopped.
        /// </summary>
        public bool TryAcquire(in VoiceRequest r, double now, out int slot, out bool stolen)
        {
            slot = -1;
            stolen = false;
            LastDenial = VoiceDenial.None;
            if (!r.Protected && !(r.EstDb >= SoundSpace.CullDb))
            {
                LastDenial = VoiceDenial.TooQuiet;
                return false;
            }
            if (r.OneShot && !r.Protected)
            {
                double oldest = _recentStarts[_recentHead];
                if (now - oldest < 1.0)
                {
                    LastDenial = VoiceDenial.RateLimited;
                    return false;
                }
            }

            // Same-sound cap: replace the quietest of the group only if the newcomer is louder.
            int cap = SoundClasses.CapOf(r.Cap, _budget);
            if (r.Cap != CapGroup.None && CountInGroup(r.Cap) >= cap)
            {
                int victim = -1;
                for (int i = 0; i < _active.Length; i++)
                {
                    if (!_active[i] || _cap[i] != r.Cap || _protected[i]) continue;
                    if (victim < 0 || Worse(i, victim)) victim = i;
                }
                if (victim < 0 || !(r.EstDb > _db[victim]))
                {
                    LastDenial = VoiceDenial.CapReached;
                    return false;
                }
                slot = victim;
                stolen = true;
                Take(slot, r, now);
                return true;
            }

            for (int i = 0; i < _active.Length; i++)
            {
                if (_active[i]) continue;
                slot = i;
                Take(slot, r, now);
                return true;
            }

            // Steal: the least important unprotected voice that is less important than (or as important as but
            // quieter than) the request.
            int best = -1;
            for (int i = 0; i < _active.Length; i++)
            {
                if (_protected[i]) continue;
                bool lessImportant = _priority[i] > r.Priority || (_priority[i] == r.Priority && _db[i] < r.EstDb);
                if (!lessImportant) continue;
                if (best < 0 || Worse(i, best)) best = i;
            }
            if (best < 0)
            {
                LastDenial = VoiceDenial.NoVoice;
                return false;
            }
            slot = best;
            stolen = true;
            Take(slot, r, now);
            return true;
        }

        // True when voice a is a better steal victim than b.
        private bool Worse(int a, int b)
        {
            if (_priority[a] != _priority[b]) return _priority[a] > _priority[b];
            if (_db[a] != _db[b]) return _db[a] < _db[b];
            return _started[a] < _started[b];
        }

        private void Take(int slot, in VoiceRequest r, double now)
        {
            _active[slot] = true;
            _priority[slot] = r.Priority;
            _db[slot] = r.EstDb;
            _cap[slot] = r.Cap;
            _protected[slot] = r.Protected;
            _started[slot] = now;
            if (r.OneShot && !r.Protected)
            {
                _recentStarts[_recentHead] = now;
                _recentHead = (_recentHead + 1) % _recentStarts.Length;
            }
        }

        /// <summary>Updates a playing voice's estimated level (moving sources).</summary>
        public void UpdateLevel(int slot, float estDb)
        {
            if (IsActive(slot)) _db[slot] = Dsp.Sanitize(estDb);
        }

        public void Release(int slot)
        {
            if (slot >= 0 && slot < _active.Length) _active[slot] = false;
        }

        public void ReleaseAll()
        {
            Array.Clear(_active, 0, _active.Length);
        }
    }

    /// <summary>A vehicle or aircraft that could use a real-time synth voice.</summary>
    public struct SynthCandidate
    {
        /// <summary>Caller's handle id.</summary>
        public int Id;
        /// <summary>Audibility score: estimated dBFS at the listener (higher wins).</summary>
        public float Score;
        public bool Player;
        public bool Aircraft;
    }

    /// <summary>
    /// Picks which engines and aircraft get the tier's real-time synth voices (W2_DESIGN 7.1: the player's
    /// engine always, then the nearest / loudest NPC engines; at most <see cref="VoiceBudget.AircraftVoices"/>
    /// aircraft, the nearest two). Candidates below the −45 dBFS cull never get a voice. No allocation.
    /// </summary>
    public static class SynthAssign
    {
        /// <summary>Marks <c>selected[i]</c> for the chosen candidates among the first <paramref name="count"/>.
        /// Returns how many were selected. Ties break by lower index (deterministic).</summary>
        public static int Select(SynthCandidate[] candidates, int count, in VoiceBudget budget, bool[] selected)
        {
            if (candidates == null || selected == null) return 0;
            int n = Math.Min(count, Math.Min(candidates.Length, selected.Length));
            for (int i = 0; i < n; i++) selected[i] = false;
            int voices = budget.SynthVoices;
            int air = budget.AircraftVoices;
            int used = 0;
            for (int i = 0; i < n && used < voices; i++)
            {
                if (!candidates[i].Player) continue;
                selected[i] = true;
                used++;
            }
            // Aircraft have their own small cap (they are not counted against the engine voices).
            int airUsed = 0;
            while (airUsed < air)
            {
                int best = -1;
                for (int i = 0; i < n; i++)
                {
                    if (selected[i] || !candidates[i].Aircraft || candidates[i].Player) continue;
                    if (!(candidates[i].Score >= SoundSpace.CullDb)) continue;
                    if (best < 0 || candidates[i].Score > candidates[best].Score) best = i;
                }
                if (best < 0) break;
                selected[best] = true;
                airUsed++;
            }
            while (used < voices)
            {
                int best = -1;
                for (int i = 0; i < n; i++)
                {
                    if (selected[i] || candidates[i].Aircraft) continue;
                    if (!(candidates[i].Score >= SoundSpace.CullDb)) continue;
                    if (best < 0 || candidates[i].Score > candidates[best].Score) best = i;
                }
                if (best < 0) break;
                selected[best] = true;
                used++;
            }
            return used + airUsed;
        }
    }

    /// <summary>
    /// Chooses which agents open a live engine voice: the <c>max</c> nearest by true 3D distance to the listener
    /// within a range (W2_DESIGN 7.1). Kept apart from the render LOD order on purpose: a vehicle that just passed
    /// the player is behind the camera (no render priority) but still loud, and must keep its voice through the
    /// Doppler sweep. No allocation.
    /// </summary>
    public static class NearestSelect
    {
        /// <summary>Marks <c>selected[i]</c> for the up to <paramref name="max"/> entries of <paramref name="distM"/>
        /// (first <paramref name="count"/>) nearest and below <paramref name="rangeM"/>; ties break by lower index.
        /// <paramref name="scratch"/> needs at least <paramref name="max"/> slots. Returns how many were chosen.</summary>
        public static int Select(float[] distM, int count, int max, float rangeM, bool[] selected, int[] scratch)
        {
            if (distM == null || selected == null || scratch == null) return 0;
            int n = Math.Min(count, Math.Min(distM.Length, selected.Length));
            for (int i = 0; i < n; i++) selected[i] = false;
            int cap = Math.Min(max, scratch.Length);
            int used = 0;
            for (int i = 0; i < n && cap > 0; i++)
            {
                float d = distM[i];
                if (!(d < rangeM)) continue;
                if (used == cap && !(d < distM[scratch[used - 1]])) continue;
                int j = used < cap ? used++ : used - 1;
                while (j > 0 && distM[scratch[j - 1]] > d)
                {
                    scratch[j] = scratch[j - 1];
                    j--;
                }
                scratch[j] = i;
            }
            for (int k = 0; k < used; k++) selected[scratch[k]] = true;
            return used;
        }
    }
}
