using System;

namespace Ghumante.Core.Synth
{
    /// <summary>Control block of an <see cref="AircraftVoice"/> (main thread → audio thread, ramped per buffer).</summary>
    public struct AircraftVoiceParams
    {
        /// <summary>0 idle … 1 take-off power (prop rpm or fan N1, normalised).</summary>
        public float Thrust01;
        /// <summary>0 … 1 reverse thrust / beta roar after touchdown (+6 dB, W2_DESIGN 8.6).</summary>
        public float Reverse01;
        /// <summary>Linear gain after the class level (distance attenuation is the caller's).</summary>
        public float Gain;
        /// <summary>Doppler frequency multiplier (1 = none).</summary>
        public float DopplerPitch;
        /// <summary>Air-absorption low-pass cutoff (Hz, 0 = off).</summary>
        public float AirCutoffHz;
        /// <summary>Ground-reflection delay <c>2h·sinθ / c</c> in seconds (0 or more than 50 ms = no comb).</summary>
        public float ReflectionDelayS;
        /// <summary>Ground-reflection gain (0.7 by design).</summary>
        public float ReflectionGain;
        /// <summary>1 while the wheels are on the runway (approach whine off, tyre chirp allowed).</summary>
        public byte OnGround;
    }

    /// <summary>
    /// Real-time aircraft voice (W2_DESIGN 8.6, A §2.11): turboprops as a harmonic stack at the blade-pass
    /// frequency (≈ 98 Hz approach, 120 Hz take-off) with turbine whine and beta roar; jets as fan tone
    /// (1.5–3 kHz), a buzz-saw comb at the shaft rate on take-off, broadband roar and the approach whine;
    /// helicopters as noise amplitude-modulated at 19.5 Hz with the 39 / 58 / 78 Hz harmonics, tail rotor and
    /// turbine. A ground-reflection comb (second path delayed by <see cref="AircraftVoiceParams.ReflectionDelayS"/>,
    /// gain 0.7) gives the flyby phasing sweep. Real-time safe and deterministic per seed.
    /// </summary>
    public sealed class AircraftVoice
    {
        private const int Block = 32;
        private const float MaxReflectionS = 0.05f;

        private static readonly float[] PropHarmonics = { 1f, .7f, .5f, .35f, .25f, .18f, .13f, .09f, .06f, .04f };

        private AircraftSoundClass _class;
        private Noise _noise;
        private Biquad _roar, _roar2, _buzz, _slap, _whineBp;
        private OnePole _air;
        private readonly DelayLine _reflect = new DelayLine(9600);
        private double _bpPhase, _fanPhase, _shaftPhase, _turbPhase, _tailPhase, _appPhase;
        private float _jit;
        private int _lastRate;
        private float _level;

        public AircraftVoice(AircraftSoundClass cls, uint seed)
        {
            Reset(cls, seed);
        }

        public AircraftSoundClass Class
        {
            get { return _class; }
        }

        public void Reset(AircraftSoundClass cls, uint seed)
        {
            _class = cls;
            _noise = new Noise(Dsp.Mix(seed, 0x41495252u, (uint)cls));
            _bpPhase = _fanPhase = _shaftPhase = _turbPhase = _tailPhase = _appPhase = 0;
            _jit = 0f;
            _lastRate = 0;
            _reflect.Clear();
            _roar = default;
            _roar2 = default;
            _buzz = default;
            _slap = default;
            _whineBp = default;
            _air = default;
            switch (cls)
            {
                case AircraftSoundClass.WideBody: _level = Dsp.DbToGain(8f); break;
                case AircraftSoundClass.NarrowBody: _level = Dsp.DbToGain(6f); break;
                case AircraftSoundClass.Helicopter: _level = Dsp.DbToGain(5f); break;
                case AircraftSoundClass.StolTurboprop: _level = Dsp.DbToGain(2f); break;
                default: _level = Dsp.DbToGain(4f); break;
            }
        }

        private void Setup(int sr)
        {
            _lastRate = sr;
            switch (_class)
            {
                case AircraftSoundClass.Helicopter:
                    _slap.Set(BiquadKind.BandPass, 420f, 0.8f, sr);
                    _roar.Set(BiquadKind.LowPass, 900f, 0.7f, sr);
                    break;
                case AircraftSoundClass.NarrowBody:
                case AircraftSoundClass.WideBody:
                    _roar.Set(BiquadKind.LowPass, 600f, 0.7f, sr);
                    _roar2.Set(BiquadKind.BandPass, 250f, 0.6f, sr);
                    _buzz.Set(BiquadKind.BandPass, 900f, 0.8f, sr);
                    _whineBp.Set(BiquadKind.BandPass, 550f, 6f, sr);
                    break;
                default:
                    _roar.Set(BiquadKind.LowPass, 1000f, 0.7f, sr);
                    _roar2.Set(BiquadKind.LowPass, 700f, 0.7f, sr);
                    break;
            }
        }

        private static float P(float a, float b, float t)
        {
            return Dsp.Sanitize(a) + (Dsp.Sanitize(b) - Dsp.Sanitize(a)) * t;
        }

        /// <summary>Renders <paramref name="frames"/> mono samples (overwriting <c>mono[0..frames)</c>).</summary>
        public void Render(float[] mono, int frames, int sampleRate, in AircraftVoiceParams from, in AircraftVoiceParams to)
        {
            if (mono == null || frames <= 0) return;
            if (frames > mono.Length) frames = mono.Length;
            int sr = sampleRate > 0 ? sampleRate : 48000;
            if (sr != _lastRate) Setup(sr);
            _air.SetLowPass(Dsp.Sanitize(to.AirCutoffHz), sr);
            float invSr = 1f / sr;
            for (int b0 = 0; b0 < frames; b0 += Block)
            {
                int n = Math.Min(Block, frames - b0);
                float ta = (float)b0 / frames, tb = (float)(b0 + n) / frames, tm = 0.5f * (ta + tb);
                float thrust = Dsp.Clamp01(P(from.Thrust01, to.Thrust01, tm));
                float rev = Dsp.Clamp01(P(from.Reverse01, to.Reverse01, tm));
                float dop = Dsp.Clamp(P(from.DopplerPitch, to.DopplerPitch, tm), 0.5f, 2f);
                float gA = Math.Max(0f, P(from.Gain, to.Gain, ta)) * _level;
                float gB = Math.Max(0f, P(from.Gain, to.Gain, tb)) * _level;
                float delayS = P(from.ReflectionDelayS, to.ReflectionDelayS, tm);
                float rGain = Dsp.Clamp01(P(from.ReflectionGain, to.ReflectionGain, tm));
                if (!(delayS > 0f)) rGain = 0f;
                else if (delayS > MaxReflectionS) rGain *= Dsp.Clamp01(1f - (delayS - MaxReflectionS) / 0.01f);
                float delaySamples = Math.Min(delayS * sr, _reflect.Capacity - 3);
                bool ground = to.OnGround != 0;
                switch (_class)
                {
                    case AircraftSoundClass.Helicopter:
                        Heli(mono, b0, n, invSr, thrust, dop);
                        break;
                    case AircraftSoundClass.NarrowBody:
                    case AircraftSoundClass.WideBody:
                        Jet(mono, b0, n, invSr, thrust, rev, dop, ground);
                        break;
                    default:
                        Prop(mono, b0, n, invSr, thrust, rev, dop);
                        break;
                }
                for (int i = 0; i < n; i++)
                {
                    float d = mono[b0 + i];
                    _reflect.Write(d);
                    float s = d + (rGain > 0f ? _reflect.Read(delaySamples) * rGain : 0f);
                    s = _air.Process(s);
                    mono[b0 + i] = Dsp.SoftClip(s * (gA + (gB - gA) * ((float)i / n)));
                }
            }
            Dsp.Finish(mono, 0, frames);
        }

        private void Prop(float[] o, int start, int n, float invSr, float thrust, float rev, float dop)
        {
            bool stol = _class == AircraftSoundClass.StolTurboprop;
            // 6 blades × 980–1,200 rpm → 98–120 Hz (ATR); STOL 3 blades × 1,900–2,300 rpm → 95–115 Hz.
            float bpf = (stol ? Dsp.Lerp(95f, 115f, thrust) : Dsp.Lerp(98f, 120f, thrust)) * dop;
            float turb = Dsp.Lerp(3000f, 5500f, thrust) * dop;
            float roar = 0.25f + 0.35f * thrust;
            float beta = 2f * rev; // +6 dB
            int hc = PropHarmonics.Length;
            float limit = 0.45f / invSr;
            while (hc > 0 && bpf * hc >= limit) hc--;
            for (int i = 0; i < n; i++)
            {
                _bpPhase += bpf * invSr;
                if (_bpPhase >= 1.0) _bpPhase -= Math.Floor(_bpPhase);
                float ph = (float)_bpPhase;
                float s = 0f;
                for (int k = 0; k < hc; k++)
                {
                    float x = ph * (k + 1);
                    x -= (int)x;
                    s += PropHarmonics[k] * Dsp.Sin01(x);
                }
                s *= 0.28f;
                _jit += (_noise.White() * 0.001f - _jit) * 0.001f;
                _turbPhase += turb * (1f + _jit) * invSr;
                if (_turbPhase >= 1.0) _turbPhase -= Math.Floor(_turbPhase);
                s += Dsp.Sin01((float)_turbPhase) * 0.05f;
                s += _roar.Process(_noise.Pink()) * roar;
                if (beta > 0.01f) s += _roar2.Process(_noise.Pink()) * beta;
                o[start + i] = s;
            }
        }

        private void Jet(float[] o, int start, int n, float invSr, float thrust, float rev, float dop, bool ground)
        {
            bool wide = _class == AircraftSoundClass.WideBody;
            float fan = (wide ? Dsp.Lerp(900f, 2000f, thrust) : Dsp.Lerp(1600f, 3000f, thrust)) * dop;
            float shaft = (wide ? Dsp.Lerp(30f, 60f, thrust) : Dsp.Lerp(45f, 83f, thrust)) * dop;
            float buzz = Dsp.Clamp01((thrust - 0.75f) / 0.25f) * 0.25f; // supersonic fan tips on take-off
            float roar = (wide ? 0.55f : 0.45f) * (0.35f + 0.65f * thrust) * (1f + rev);
            float fanLevel = 0.06f + 0.06f * thrust;
            float whine = !ground && thrust < 0.5f && !wide ? 0.06f : 0f; // A320ceo approach whine [V]
            for (int i = 0; i < n; i++)
            {
                _fanPhase += fan * invSr;
                if (_fanPhase >= 1.0) _fanPhase -= Math.Floor(_fanPhase);
                float s = Dsp.Sin01((float)_fanPhase) * fanLevel;
                float pink = _noise.Pink();
                s += (_roar.Process(pink) + 0.6f * _roar2.Process(_noise.Brown())) * roar;
                if (buzz > 0f)
                {
                    _shaftPhase += shaft * invSr;
                    if (_shaftPhase >= 1.0) _shaftPhase -= Math.Floor(_shaftPhase);
                    s += _buzz.Process(Dsp.Saw((float)_shaftPhase, shaft * invSr)) * buzz;
                }
                if (whine > 0f)
                {
                    _appPhase += 550f * dop * invSr;
                    if (_appPhase >= 1.0) _appPhase -= Math.Floor(_appPhase);
                    s += (Dsp.Sin01((float)_appPhase) * 0.6f + _whineBp.Process(pink) * 2f) * whine;
                }
                o[start + i] = s;
            }
        }

        private void Heli(float[] o, int start, int n, float invSr, float thrust, float dop)
        {
            // 3 blades × 390 rpm → 19.5 Hz blade pass; tail rotor ≈ 68 Hz; turbine 4–8 kHz.
            float bpf = 19.5f * dop;
            float depth = Dsp.Lerp(0.6f, 0.9f, thrust);
            float tail = 68f * dop;
            float turb = Dsp.Lerp(4500f, 7000f, thrust) * dop;
            for (int i = 0; i < n; i++)
            {
                _bpPhase += bpf * invSr;
                if (_bpPhase >= 1.0) _bpPhase -= Math.Floor(_bpPhase);
                float ph = (float)_bpPhase;
                float c = 0.5f + 0.5f * Dsp.Sin01(ph);
                float pulse = c * c * c * c;
                float s = _slap.Process(_noise.White()) * (1f - depth + depth * pulse) * 1.4f;
                float x2 = ph * 2f; x2 -= (int)x2;
                float x3 = ph * 3f; x3 -= (int)x3;
                float x4 = ph * 4f; x4 -= (int)x4;
                float x6 = ph * 6f; x6 -= (int)x6;
                float x8 = ph * 8f; x8 -= (int)x8;
                s += (0.5f * Dsp.Sin01(x2) + 0.45f * Dsp.Sin01(x3) + 0.4f * Dsp.Sin01(x4) + 0.3f * Dsp.Sin01(x6) + 0.2f * Dsp.Sin01(x8)) * 0.18f;
                _tailPhase += tail * invSr;
                if (_tailPhase >= 1.0) _tailPhase -= Math.Floor(_tailPhase);
                float tp = (float)_tailPhase;
                float t2 = tp * 2f; t2 -= (int)t2;
                s += (Dsp.Sin01(tp) + 0.5f * Dsp.Sin01(t2)) * 0.06f;
                _turbPhase += turb * invSr;
                if (_turbPhase >= 1.0) _turbPhase -= Math.Floor(_turbPhase);
                s += Dsp.Sin01((float)_turbPhase) * 0.03f;
                s += _roar.Process(_noise.Pink()) * 0.15f;
                o[start + i] = s;
            }
        }
    }
}
