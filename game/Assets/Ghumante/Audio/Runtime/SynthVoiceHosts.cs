using Ghumante.Core.Synth;
using UnityEngine;

namespace Ghumante.Audio
{
    /// <summary>What the main thread hands an <see cref="EngineVoiceHost"/> each frame.</summary>
    internal struct EngineHostParams
    {
        public EngineVoiceParams P;
        public EngineModel Model;
        public uint Seed;
        /// <summary>Bumped when the host is given to another vehicle: the audio thread re-seeds the voice.</summary>
        public int Generation;
    }

    /// <summary>What the main thread hands an <see cref="AircraftVoiceHost"/> each frame.</summary>
    internal struct AircraftHostParams
    {
        public AircraftVoiceParams P;
        public AircraftSoundClass Class;
        public uint Seed;
        public int Generation;
    }

    /// <summary>
    /// Shared plumbing of a real-time synth voice (W2_DESIGN 7.1 "Real-time voices", the <c>OnAudioFilterRead</c>
    /// path): the AudioSource plays a looping 1 s constant-1.0 clip so Unity's 3D panning and the custom rolloff
    /// still apply, and the filter multiplies the synthesised mono signal into the (already spatialised) buffer.
    /// The audio thread never touches Unity API and never allocates; parameters arrive through a
    /// <see cref="ParamMailbox{T}"/> and are ramped across each buffer by the voice.
    /// </summary>
    internal static class SynthHostShared
    {
        public const int ScratchFrames = 4096;

        /// <summary>How long a released voice keeps running at a zero gain target before its source stops: the
        /// gain ramps to 0 across the next DSP buffer (≤ 1,024 frames ≈ 21 ms), so a release never clicks.</summary>
        public const float ReleaseS = 0.1f;

        /// <summary>Multiplies <paramref name="mono"/> into <c>data</c> frames [frame0, frame0 + n).</summary>
        public static void MultiplyInto(float[] data, int channels, int frame0, int n, float[] mono)
        {
            int idx = frame0 * channels;
            for (int f = 0; f < n; f++)
            {
                float s = mono[f];
                for (int c = 0; c < channels; c++) data[idx + c] *= s;
                idx += channels;
            }
        }

        public static void Silence(float[] data)
        {
            System.Array.Clear(data, 0, data.Length);
        }

        /// <summary>The looping constant-one carrier clip (one per output rate).</summary>
        public static AudioClip CreateCarrier(int rate)
        {
            int n = rate > 0 ? rate : 48000;
            AudioClip clip = AudioClip.Create("ghm_synth_carrier", n, 1, n, false);
            var ones = new float[n];
            for (int i = 0; i < n; i++) ones[i] = 1f;
            clip.SetData(ones, 0);
            return clip;
        }
    }

    /// <summary>Real-time engine voice on an AudioSource (player engine and the nearest NPC engines).</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    [RequireComponent(typeof(AudioSource))]
    internal sealed class EngineVoiceHost : MonoBehaviour
    {
        private readonly ParamMailbox<EngineHostParams> _box = new ParamMailbox<EngineHostParams>();
        private EngineVoice _voice;
        private float[] _mono;
        private EngineVoiceParams _from;
        private int _generation = -1;
        private volatile int _sampleRate = 48000;
        private volatile bool _running;
        private int _nextGeneration;
        private EngineHostParams _last;
        private float _stopAt = -1f;

        public AudioSource Source { get; private set; }

        /// <summary>The handle currently using this host (main thread bookkeeping).</summary>
        public object Owner { get; set; }

        /// <summary>True while a released voice fades out (the host is not free yet).</summary>
        public bool Releasing
        {
            get { return _stopAt >= 0f; }
        }

        public static EngineVoiceHost Create(Transform parent, AudioClip carrier, int index)
        {
            var go = new GameObject("EngineVoice" + index);
            go.transform.SetParent(parent, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.clip = carrier;
            s.dopplerLevel = 0f;
            EngineVoiceHost h = go.AddComponent<EngineVoiceHost>();
            h.Source = s;
            h._voice = new EngineVoice(EnginePresets.Get(EngineModel.Car4Cyl), 1);
            h._mono = new float[SynthHostShared.ScratchFrames];
            return h;
        }

        public int SampleRate
        {
            set { _sampleRate = value > 0 ? value : 48000; }
        }

        /// <summary>Main thread: hands the host to a vehicle (re-seeds on the audio thread) and starts it.</summary>
        public void Begin(EngineModel model, uint seed, in EngineVoiceParams p, bool player)
        {
            _nextGeneration++;
            _stopAt = -1f;
            _last = new EngineHostParams { P = p, Model = model, Seed = seed, Generation = _nextGeneration };
            _box.Write(_last);
            AudioCurves.Apply(Source, SoundClasses.ForEngine(model, player));
            Source.volume = 1f;
            _running = true;
            if (!Source.isPlaying) Source.Play();
        }

        public void Push(EngineModel model, uint seed, in EngineVoiceParams p)
        {
            _last = new EngineHostParams { P = p, Model = model, Seed = seed, Generation = _nextGeneration };
            _box.Write(_last);
        }

        /// <summary>Releases the host: the gain ramps to 0 on the audio thread and the source stops
        /// <see cref="SynthHostShared.ReleaseS"/> later (<see cref="Releasing"/> until then).</summary>
        public void End()
        {
            Owner = null;
            if (!_running || !Source.isPlaying)
            {
                Stop();
                return;
            }
            _last.P.Gain = 0f;
            _box.Write(_last);
            _stopAt = Time.unscaledTime + SynthHostShared.ReleaseS;
        }

        private void Stop()
        {
            _running = false;
            _stopAt = -1f;
            if (Source.isPlaying) Source.Stop();
        }

        private void Update()
        {
            if (_stopAt >= 0f && Owner == null && Time.unscaledTime >= _stopAt) Stop();
        }

        private void OnDisable()
        {
            if (_stopAt >= 0f) Stop();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_running || channels <= 0 || _mono == null)
            {
                SynthHostShared.Silence(data);
                return;
            }
            _box.Read(out EngineHostParams target);
            if (target.Generation != _generation)
            {
                _generation = target.Generation;
                _voice.Reset(EnginePresets.Get(target.Model), target.Seed);
                _from = target.P;
                _from.Gain = 0f; // fade in over the first buffer
            }
            int frames = data.Length / channels;
            int sr = _sampleRate;
            EngineVoiceParams to = target.P;
            int done = 0;
            while (done < frames)
            {
                int n = frames - done;
                if (n > _mono.Length) n = _mono.Length;
                // Ramp across the whole buffer even when it is split into chunks.
                EngineVoiceParams a = Lerp(_from, to, (float)done / frames);
                EngineVoiceParams b = Lerp(_from, to, (float)(done + n) / frames);
                _voice.Render(_mono, n, sr, a, b);
                SynthHostShared.MultiplyInto(data, channels, done, n, _mono);
                done += n;
            }
            _from = to;
        }

        private static EngineVoiceParams Lerp(in EngineVoiceParams a, in EngineVoiceParams b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            EngineVoiceParams r = b;
            r.Rpm = a.Rpm + (b.Rpm - a.Rpm) * t;
            r.Load = a.Load + (b.Load - a.Load) * t;
            r.SpeedMps = a.SpeedMps + (b.SpeedMps - a.SpeedMps) * t;
            r.Gain = a.Gain + (b.Gain - a.Gain) * t;
            r.DopplerPitch = a.DopplerPitch + (b.DopplerPitch - a.DopplerPitch) * t;
            r.Wetness = a.Wetness + (b.Wetness - a.Wetness) * t;
            return r;
        }
    }

    /// <summary>Real-time aircraft voice on an AudioSource (at most two at once, the nearest).</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    [RequireComponent(typeof(AudioSource))]
    internal sealed class AircraftVoiceHost : MonoBehaviour
    {
        private readonly ParamMailbox<AircraftHostParams> _box = new ParamMailbox<AircraftHostParams>();
        private AircraftVoice _voice;
        private float[] _mono;
        private AircraftVoiceParams _from;
        private int _generation = -1;
        private volatile int _sampleRate = 48000;
        private volatile bool _running;
        private int _nextGeneration;
        private AircraftHostParams _last;
        private float _stopAt = -1f;

        public AudioSource Source { get; private set; }

        public object Owner { get; set; }

        /// <summary>True while a released voice fades out (the host is not free yet).</summary>
        public bool Releasing
        {
            get { return _stopAt >= 0f; }
        }

        public static AircraftVoiceHost Create(Transform parent, AudioClip carrier, int index)
        {
            var go = new GameObject("AircraftVoice" + index);
            go.transform.SetParent(parent, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.clip = carrier;
            s.dopplerLevel = 0f;
            AircraftVoiceHost h = go.AddComponent<AircraftVoiceHost>();
            h.Source = s;
            h._voice = new AircraftVoice(AircraftSoundClass.Turboprop, 1);
            h._mono = new float[SynthHostShared.ScratchFrames];
            return h;
        }

        public int SampleRate
        {
            set { _sampleRate = value > 0 ? value : 48000; }
        }

        public void Begin(AircraftSoundClass cls, uint seed, in AircraftVoiceParams p)
        {
            _nextGeneration++;
            _stopAt = -1f;
            _last = new AircraftHostParams { P = p, Class = cls, Seed = seed, Generation = _nextGeneration };
            _box.Write(_last);
            AudioCurves.Apply(Source, SoundClasses.ForAircraft(cls));
            Source.volume = 1f;
            _running = true;
            if (!Source.isPlaying) Source.Play();
        }

        public void Push(AircraftSoundClass cls, uint seed, in AircraftVoiceParams p)
        {
            _last = new AircraftHostParams { P = p, Class = cls, Seed = seed, Generation = _nextGeneration };
            _box.Write(_last);
        }

        /// <summary>Releases the host with a gain ramp to 0 (see <see cref="EngineVoiceHost.End"/>).</summary>
        public void End()
        {
            Owner = null;
            if (!_running || !Source.isPlaying)
            {
                Stop();
                return;
            }
            _last.P.Gain = 0f;
            _box.Write(_last);
            _stopAt = Time.unscaledTime + SynthHostShared.ReleaseS;
        }

        private void Stop()
        {
            _running = false;
            _stopAt = -1f;
            if (Source.isPlaying) Source.Stop();
        }

        private void Update()
        {
            if (_stopAt >= 0f && Owner == null && Time.unscaledTime >= _stopAt) Stop();
        }

        private void OnDisable()
        {
            if (_stopAt >= 0f) Stop();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_running || channels <= 0 || _mono == null)
            {
                SynthHostShared.Silence(data);
                return;
            }
            _box.Read(out AircraftHostParams target);
            if (target.Generation != _generation)
            {
                _generation = target.Generation;
                _voice.Reset(target.Class, target.Seed);
                _from = target.P;
                _from.Gain = 0f;
            }
            int frames = data.Length / channels;
            int sr = _sampleRate;
            AircraftVoiceParams to = target.P;
            int done = 0;
            while (done < frames)
            {
                int n = frames - done;
                if (n > _mono.Length) n = _mono.Length;
                AircraftVoiceParams a = done == 0 ? _from : to;
                _voice.Render(_mono, n, sr, a, to);
                SynthHostShared.MultiplyInto(data, channels, done, n, _mono);
                done += n;
            }
            _from = to;
        }
    }
}
