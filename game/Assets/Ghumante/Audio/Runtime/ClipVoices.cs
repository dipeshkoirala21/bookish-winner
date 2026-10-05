using System;
using Ghumante.Core.Synth;
using UnityEngine;

namespace Ghumante.Audio
{
    /// <summary>
    /// The pool of AudioSources that play baked clips (W2_DESIGN 7.1 "Voices"): a fixed number of sources (the
    /// tier's real voices minus synth and bed voices), assigned through <see cref="VoiceAllocator"/> (priorities,
    /// stealing, same-sound caps, the −45 dBFS cull and the one-shots-per-second limit). Per voice: custom
    /// rolloff by class, custom Doppler from simulation velocity (smoothed 50 ms, clamped 0.7–1.4), dead-reckoned
    /// position for moving one-shots, bus gain and occlusion. At most four voices enable an air-absorption
    /// low-pass at a time. No allocation after construction.
    /// </summary>
    internal sealed class ClipVoices : IDisposable
    {
        public const int MaxAirFilters = 4;

        private struct Voice
        {
            public AudioSource Source;
            public Transform Transform;
            public AudioLowPassFilter Lpf;
            public SoundClass Class;
            public MixBus Bus;
            public float Gain;
            public float BasePitch;
            public Vector3 Position;
            public Vector3 Velocity;
            public bool Moving;
            public Smoothed Doppler;
            public float Occlusion;
            public float OcclusionTarget;
            public bool Loop;
            public bool Active;
            public bool AirOn;
        }

        private readonly Voice[] _voices;
        private readonly VoiceAllocator _alloc;
        private readonly GameObject _root;
        private int _airOn;

        public ClipVoices(Transform parent, int count, in VoiceBudget budget)
        {
            _root = new GameObject("ClipVoices");
            _root.transform.SetParent(parent, false);
            _voices = new Voice[Math.Max(1, count)];
            for (int i = 0; i < _voices.Length; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(_root.transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.dopplerLevel = 0f;
                AudioLowPassFilter lpf = go.AddComponent<AudioLowPassFilter>();
                lpf.enabled = false;
                _voices[i] = new Voice { Source = s, Transform = go.transform, Lpf = lpf, BasePitch = 1f };
            }
            _alloc = new VoiceAllocator(_voices.Length, budget);
        }

        public int Capacity
        {
            get { return _voices.Length; }
        }

        public int ActiveCount
        {
            get { return _alloc.ActiveCount; }
        }

        public VoiceDenial LastDenial
        {
            get { return _alloc.LastDenial; }
        }

        /// <summary>
        /// Plays <paramref name="clip"/> as class <paramref name="cls"/> at a scene position. Returns the voice
        /// index, or −1 when the allocator said no (too quiet, capped, rate-limited or no voice).
        /// </summary>
        public int Play(AudioClip clip, SoundClass cls, Vector3 pos, Vector3 vel, bool moving, float gainDb, float pitch, bool loop,
                        in Vector3 listener, double now, float busGain)
        {
            if (clip == null) return -1;
            SoundClassInfo info = SoundClasses.Get(cls);
            float dist = info.SpatialBlend > 0f ? Vector3.Distance(pos, listener) : 0f;
            float est = SoundSpace.EstimateDb(cls, dist, gainDb);
            var req = VoiceRequest.For(cls, est, !loop);
            if (!_alloc.TryAcquire(req, now, out int slot, out bool stolen)) return -1;
            ref Voice v = ref _voices[slot];
            if (stolen || v.Active) StopSlot(slot, false);
            v.Active = true;
            v.Class = cls;
            v.Bus = info.Bus;
            v.Gain = Dsp.DbToGain(gainDb);
            v.BasePitch = Dsp.Clamp(pitch, 0.25f, 3f);
            v.Position = pos;
            v.Velocity = vel;
            v.Moving = moving;
            v.Doppler = default;
            v.Occlusion = 0f;
            v.OcclusionTarget = 0f;
            v.Loop = loop;
            AudioCurves.Apply(v.Source, cls);
            v.Transform.position = pos;
            v.Source.clip = clip;
            v.Source.loop = loop;
            v.Source.pitch = v.BasePitch;
            v.Source.volume = Mathf.Clamp01(v.Gain * busGain);
            SetAir(ref v, info.AirLpf && dist > info.AirFromM ? SoundSpace.AirCutoffHz(dist) : 0f);
            v.Source.Play();
            return slot;
        }

        private void SetAir(ref Voice v, float cutoffHz)
        {
            bool want = cutoffHz > 0f;
            if (want && !v.AirOn && _airOn >= MaxAirFilters) want = false;
            if (want)
            {
                if (!v.AirOn)
                {
                    v.Lpf.enabled = true;
                    v.AirOn = true;
                    _airOn++;
                }
                v.Lpf.cutoffFrequency = cutoffHz;
            }
            else if (v.AirOn)
            {
                v.Lpf.enabled = false;
                v.AirOn = false;
                _airOn--;
            }
        }

        /// <summary>Per frame: retire finished voices, move and re-pitch moving ones, apply bus gains and occlusion.</summary>
        public void Update(float dt, in Vector3 listener, in Vector3 listenerVel, float[] busGain)
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                ref Voice v = ref _voices[i];
                if (!v.Active) continue;
                if (!v.Loop && !v.Source.isPlaying)
                {
                    StopSlot(i, true);
                    continue;
                }
                SoundClassInfo info = SoundClasses.Get(v.Class);
                if (v.Moving)
                {
                    v.Position += v.Velocity * dt;
                    v.Transform.position = v.Position;
                    float target = SoundSpace.DopplerPitch(v.Position.x, v.Position.y, v.Position.z, v.Velocity.x, v.Velocity.y, v.Velocity.z,
                                                           listener.x, listener.y, listener.z, listenerVel.x, listenerVel.y, listenerVel.z,
                                                           info.Doppler);
                    float p = v.Doppler.Step(target, dt, SoundSpace.DopplerSmoothS);
                    v.Source.pitch = v.BasePitch * p;
                }
                v.Occlusion += (v.OcclusionTarget - v.Occlusion) * Mathf.Clamp01(dt / 0.2f);
                float occ = 1f - 0.5f * v.Occlusion; // −6 dB fully occluded
                int bus = (int)v.Bus;
                float bg = busGain != null && bus < busGain.Length ? busGain[bus] : 1f;
                v.Source.volume = Mathf.Clamp01(v.Gain * bg * occ);
                if (info.SpatialBlend > 0f)
                {
                    float dist = Vector3.Distance(v.Position, listener);
                    float air = info.AirLpf && dist > info.AirFromM ? SoundSpace.AirCutoffHz(dist) : 0f;
                    if (v.Occlusion > 0.5f) air = air > 0f ? Math.Min(air, 1500f) : 1500f;
                    SetAir(ref v, air);
                    _alloc.UpdateLevel(i, SoundSpace.EstimateDb(v.Class, dist, Dsp.GainToDb(v.Gain)));
                }
            }
        }

        public void SetOcclusion(int slot, bool occluded)
        {
            if (slot < 0 || slot >= _voices.Length || !_voices[slot].Active) return;
            _voices[slot].OcclusionTarget = occluded ? 1f : 0f;
        }

        /// <summary>A 3D voice worth an occlusion ray (priority ≤ 128); false for 2D or idle slots.</summary>
        public bool TryGetProbe(int slot, out Vector3 pos, out int priority)
        {
            pos = default;
            priority = 256;
            if (slot < 0 || slot >= _voices.Length) return false;
            ref Voice v = ref _voices[slot];
            if (!v.Active) return false;
            SoundClassInfo info = SoundClasses.Get(v.Class);
            if (info.SpatialBlend < 0.9f || info.Priority > 128) return false;
            pos = v.Position;
            priority = info.Priority;
            return true;
        }

        public void Stop(int slot)
        {
            if (slot >= 0 && slot < _voices.Length && _voices[slot].Active) StopSlot(slot, true);
        }

        private void StopSlot(int slot, bool release)
        {
            ref Voice v = ref _voices[slot];
            if (v.Source.isPlaying) v.Source.Stop();
            v.Source.clip = null;
            SetAir(ref v, 0f);
            v.Active = false;
            if (release) _alloc.Release(slot);
        }

        /// <summary>Floating-origin rebase: moves every playing voice by −delta (scene space).</summary>
        public void ShiftOrigin(Vector3 delta)
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].Active) continue;
                _voices[i].Position -= delta;
                _voices[i].Transform.position = _voices[i].Position;
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < _voices.Length; i++)
                if (_voices[i].Active) StopSlot(i, true);
            _alloc.ReleaseAll();
        }

        public void Dispose()
        {
            StopAll();
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
    }
}
