using System;
using Ghumante.Core.Synth;
using UnityEngine;

namespace Ghumante.Audio
{
    /// <summary>
    /// Ambience by place, time, season and weather (W2_DESIGN 7.2; A §7): every 0.25 s it asks
    /// <see cref="AmbienceModel"/> for bed gains, spot rates and the snapshot at the listener, keeps the loudest
    /// beds playing on 2D loop sources (crossfaded over 3 s walking, 1.5 s driving), and starts spot emitters
    /// (bells, pigeons, crows, dogs, shutters, cookers, distant horns …) as Poisson events around the listener,
    /// including night bark chains. Deterministic per region seed. No per-frame allocation.
    /// </summary>
    internal sealed class AmbienceDirector : IDisposable
    {
        /// <summary>Linear level that puts a 0.7-peak bed near −24 dBFS (W2_DESIGN 7.1 "beds −24").</summary>
        public const float BedLevel = 0.3f;

        private const float UpdateS = 0.25f;

        /// <summary>Fade of the beds when the world goes away (s).</summary>
        public const float SilenceFadeS = 0.5f;

        private struct Bed
        {
            public AudioSource Source;
            public AudioLowPassFilter Lpf;
            public int Index;      // bed index (BankSound - BedBase), −1 = free
            public float Gain;     // current (smoothed)
            public float Target;
        }

        private struct PendingBark
        {
            public float Time;
            public Vector3 Offset;
        }

        private readonly Bed[] _beds;
        private readonly float[] _bedGains = new float[AmbienceModel.BedCount];
        private readonly float[] _spotRates = new float[AmbienceModel.SpotCount];
        private readonly float[] _snapW = new float[5];
        private readonly int[] _top;
        private readonly PendingBark[] _barks = new PendingBark[8];
        private readonly GameObject _root;
        private Noise _rng;
        private float _accum = UpdateS;
        private float _clock;
        private SnapshotParams _snapshot;
        private SnapshotParams _snapshotTarget;

        /// <summary>What the model sees; the director fills it each frame.</summary>
        public AmbienceInputs Inputs;

        public AmbienceDirector(Transform parent, int bedVoices, uint seed)
        {
            _root = new GameObject("Ambience");
            _root.transform.SetParent(parent, false);
            _beds = new Bed[Math.Max(1, bedVoices)];
            _top = new int[_beds.Length];
            for (int i = 0; i < _beds.Length; i++)
            {
                var go = new GameObject("Bed" + i);
                go.transform.SetParent(_root.transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                AudioCurves.Apply(s, SoundClass.Ambience);
                AudioLowPassFilter lpf = go.AddComponent<AudioLowPassFilter>();
                lpf.enabled = false;
                _beds[i] = new Bed { Source = s, Lpf = lpf, Index = -1 };
            }
            _rng = new Noise(Dsp.Mix(seed, 0x414D4249u));
            Inputs.Hour = 8.5f;
            Inputs.Month = 10;
            Inputs.AreaPeriUrban = 1f;
            _snapshot = AmbienceModel.Snapshot(MixSnapshot.Street);
            _snapshotTarget = _snapshot;
        }

        /// <summary>The blended mixer snapshot at the listener (smoothed over 0.8 s).</summary>
        public SnapshotParams Snapshot
        {
            get { return _snapshot; }
        }

        public int ActiveBeds
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _beds.Length; i++)
                    if (_beds[i].Index >= 0) n++;
                return n;
            }
        }

        private static bool IsStreetBed(int bed)
        {
            var s = (BankSound)(bed + (int)BankSound.BedBase);
            return s == BankSound.BedTrafficHum || s == BankSound.BedTrafficHeavy || s == BankSound.BedCrowdDense ||
                   s == BankSound.BedCrowdLight || s == BankSound.BedCityHum;
        }

        /// <summary>
        /// Advances the beds and spots. While <paramref name="active"/> is false (no world: menu, loading screen) the
        /// model is not consulted, no spot or bark starts and the playing beds fade out over
        /// <see cref="SilenceFadeS"/> and stop.
        /// </summary>
        public void Update(float dt, ClipBank bank, ClipVoices clips, in Vector3 listener, double now, float[] busGain, bool driving,
                           bool active = true)
        {
            _clock += dt;
            _accum += dt;
            if (!active)
            {
                for (int i = 0; i < _beds.Length; i++) _beds[i].Target = 0f;
                for (int i = 0; i < _barks.Length; i++) _barks[i].Time = 0f;
                _accum = UpdateS; // re-evaluate at once when the world comes back
            }
            else if (_accum >= UpdateS)
            {
                _accum = 0f;
                AmbienceModel.BedGains(Inputs, _bedGains);
                AmbienceModel.SpotRates(Inputs, _spotRates);
                AmbienceModel.SnapshotWeights(Inputs, _snapW);
                _snapshotTarget = AmbienceModel.Blend(_snapW, Inputs.LaneWidthM);
                AssignBeds(bank);
            }
            _snapshot = SnapshotParams.Lerp(_snapshot, _snapshotTarget, Mathf.Clamp01(dt / 0.8f));

            float fade = !active ? SilenceFadeS : driving ? AmbienceModel.CrossfadeDriveS : AmbienceModel.CrossfadeWalkS;
            float k = Mathf.Clamp01(dt / Math.Max(0.05f, fade));
            float ambBus = busGain != null && busGain.Length > (int)MixBus.Ambience ? busGain[(int)MixBus.Ambience] : 1f;
            float street = Dsp.DbToGain(_snapshot.StreetDb);
            float lpf = _snapshot.StreetLpfHz;
            for (int i = 0; i < _beds.Length; i++)
            {
                ref Bed b = ref _beds[i];
                if (b.Index < 0) continue;
                b.Gain += (b.Target - b.Gain) * k;
                bool streetBed = IsStreetBed(b.Index);
                b.Source.volume = Mathf.Clamp01(b.Gain * BedLevel * ambBus * (streetBed ? street : 1f));
                bool wantLpf = streetBed && lpf > 0f && lpf < 20000f;
                if (b.Lpf.enabled != wantLpf) b.Lpf.enabled = wantLpf;
                if (wantLpf) b.Lpf.cutoffFrequency = lpf;
                if (b.Target <= 0f && b.Gain < 0.002f)
                {
                    b.Source.Stop();
                    b.Source.clip = null;
                    b.Index = -1;
                }
            }

            if (active) Spots(dt, bank, clips, listener, now, busGain);
        }

        /// <summary>Forgets the place: area weights back to the no-world default (peri-urban), no sacred zone, no
        /// place overlays. Time, weather and the player state are kept.</summary>
        public void ResetPlace()
        {
            for (int t = 0; t < 7; t++) Inputs.SetArea(t, 0f);
            Inputs.AreaPeriUrban = 1f;
            Inputs.SacredKind = 0;
            Inputs.RingRoad01 = Inputs.Airport01 = Inputs.Park01 = Inputs.Water01 = 0f;
            Inputs.LaneWidthM = 0f;
        }

        private void AssignBeds(ClipBank bank)
        {
            // The K loudest beds above −34 dB relative.
            int k = _beds.Length;
            for (int i = 0; i < k; i++) _top[i] = -1;
            for (int bed = 0; bed < _bedGains.Length; bed++)
            {
                float g = _bedGains[bed];
                if (!(g > 0.02f)) continue;
                for (int j = 0; j < k; j++)
                {
                    if (_top[j] >= 0 && _bedGains[_top[j]] >= g) continue;
                    for (int m = k - 1; m > j; m--) _top[m] = _top[m - 1];
                    _top[j] = bed;
                    break;
                }
            }
            // Retarget playing beds; fade out the ones that left the top set.
            for (int i = 0; i < _beds.Length; i++)
            {
                if (_beds[i].Index < 0) continue;
                bool keep = false;
                for (int j = 0; j < k; j++)
                    if (_top[j] == _beds[i].Index) keep = true;
                _beds[i].Target = keep ? _bedGains[_beds[i].Index] : 0f;
            }
            // Start new beds in free slots.
            for (int j = 0; j < k; j++)
            {
                int bed = _top[j];
                if (bed < 0 || Playing(bed)) continue;
                int slot = -1;
                for (int i = 0; i < _beds.Length && slot < 0; i++)
                    if (_beds[i].Index < 0) slot = i;
                if (slot < 0) break;
                if (!bank.TryGet((BankSound)(bed + (int)BankSound.BedBase), 0, out AudioClip clip)) continue;
                ref Bed b = ref _beds[slot];
                b.Index = bed;
                b.Gain = 0f;
                b.Target = _bedGains[bed];
                b.Source.clip = clip;
                b.Source.volume = 0f;
                // Start each loop at a seeded offset so two zones never phase-lock.
                b.Source.timeSamples = (int)(_rng.Next01() * Math.Max(1, clip.samples - 1));
                b.Source.Play();
            }
        }

        private bool Playing(int bed)
        {
            for (int i = 0; i < _beds.Length; i++)
                if (_beds[i].Index == bed) return true;
            return false;
        }

        private void Spots(float dt, ClipBank bank, ClipVoices clips, in Vector3 listener, double now, float[] busGain)
        {
            for (int s = 0; s < _spotRates.Length; s++)
            {
                float rate = _spotRates[s];
                if (!(rate > 0f)) continue;
                if (_rng.Next01() >= rate / 60f * dt) continue;
                var kind = (SpotKind)s;
                Vector3 pos = SpotPosition(kind, listener);
                PlaySpot(kind, pos, bank, clips, listener, now, busGain);
                if (kind == SpotKind.Dog && AmbienceModel.Multipliers(Inputs.Hour).Dogs >= 0.6f) ChainBarks(pos - listener);
            }
            for (int i = 0; i < _barks.Length; i++)
            {
                if (_barks[i].Time <= 0f || _barks[i].Time > _clock) continue;
                _barks[i].Time = 0f;
                PlaySpot(SpotKind.Dog, listener + _barks[i].Offset, bank, clips, listener, now, busGain);
            }
        }

        private void ChainBarks(Vector3 from)
        {
            // Night bark chains (A §2.9): one dog sets off 1–4 others 1–5 s later, 50–300 m away.
            int n = 1 + _rng.Index(4);
            for (int i = 0; i < _barks.Length && n > 0; i++)
            {
                if (_barks[i].Time > 0f) continue;
                float bearing = _rng.Next01() * Dsp.TwoPi;
                float d = _rng.Range(50f, 300f);
                _barks[i].Offset = from + new Vector3(Mathf.Sin(bearing) * d, 0f, Mathf.Cos(bearing) * d);
                _barks[i].Time = _clock + _rng.Range(1f, 5f);
                n--;
            }
        }

        private Vector3 SpotPosition(SpotKind kind, in Vector3 listener)
        {
            AmbienceModel.SpotDistance(kind, out float min, out float max);
            if (max <= 0f) return listener;
            float bearing = _rng.Next01() * Dsp.TwoPi;
            float d = _rng.Range(min, max);
            bool high = kind == SpotKind.Crow || kind == SpotKind.PigeonCoo || kind == SpotKind.Sparrow || kind == SpotKind.Myna ||
                        kind == SpotKind.Bulbul || kind == SpotKind.Koel;
            float y = kind == SpotKind.Kite ? _rng.Range(30f, 80f) : high ? _rng.Range(3f, 12f) : _rng.Range(0f, 2f);
            return listener + new Vector3(Mathf.Sin(bearing) * d, y, Mathf.Cos(bearing) * d);
        }

        private void PlaySpot(SpotKind kind, Vector3 pos, ClipBank bank, ClipVoices clips, in Vector3 listener, double now, float[] busGain)
        {
            BankSound sound = AmbienceModel.SpotSound(kind);
            int variants = bank.VariantsOf(sound);
            if (variants <= 0) return;
            int v = _rng.Index(variants);
            if (kind == SpotKind.DistantHorn) v = _rng.Index(2); // distant taps only
            if (!bank.TryGet(sound, v, out AudioClip clip)) return;
            BankSoundInfo info = ProceduralBank.Info(sound);
            SoundClass cls = kind == SpotKind.ShrineBell ? SoundClass.ShrineBell : info.Class;
            float gainDb = kind == SpotKind.DistantHorn ? -6f : 0f;
            if (info.Bus == MixBus.Sacred) gainDb += _snapshot.SacredDb;
            int bus = (int)info.Bus;
            float bg = busGain != null && bus < busGain.Length ? busGain[bus] : 1f;
            float pitch = 1f + _rng.Range(-0.03f, 0.03f);
            clips.Play(clip, cls, pos, Vector3.zero, false, gainDb, pitch, false, listener, now, bg);
        }

        public void StopAll()
        {
            for (int i = 0; i < _beds.Length; i++)
            {
                _beds[i].Source.Stop();
                _beds[i].Source.clip = null;
                _beds[i].Index = -1;
                _beds[i].Gain = 0f;
            }
        }

        public void Dispose()
        {
            StopAll();
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
    }
}
