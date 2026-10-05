using System;

namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Per-voice control block (W2_DESIGN 10.3; written by the main thread, read once per audio buffer and ramped
    /// linearly across it). 32 bytes, inside the 64-byte budget. Non-finite values are treated as 0.
    /// </summary>
    public struct EngineVoiceParams
    {
        /// <summary>Engine rpm (combustion). Ignored by electric and pedal models, which follow speed.</summary>
        public float Rpm;
        /// <summary>−1 (engine braking / overrun) … 0 (cruise) … 1 (full throttle). Pedal: > 0.05 = pedalling.</summary>
        public float Load;
        /// <summary>Road speed (m/s, magnitude): tyres, electric whine, freewheel, belt whine.</summary>
        public float SpeedMps;
        /// <summary>Linear gain applied after the preset level (distance attenuation is applied by the caller
        /// or the AudioSource).</summary>
        public float Gain;
        /// <summary>Frequency multiplier from the custom Doppler solver (1 = none; clamped 0.5–2).</summary>
        public float DopplerPitch;
        /// <summary>Air-absorption low-pass cutoff in Hz (0 = off).</summary>
        public float AirCutoffHz;
        /// <summary>0 dry … 1 monsoon (tyre hiss on wet roads).</summary>
        public float Wetness;
        /// <summary>Surface under the tyres (<see cref="FootstepSurface"/> byte values).</summary>
        public byte Surface;
        /// <summary>Reserved for event bits (keep 0).</summary>
        public byte Flags;

        /// <summary>Idle at standstill with unit gain.</summary>
        public static EngineVoiceParams Idle(in EnginePreset p)
        {
            return new EngineVoiceParams { Rpm = p.IdleRpm, Load = 0f, SpeedMps = 0f, Gain = 1f, DopplerPitch = 1f };
        }
    }

    /// <summary>
    /// Real-time engine voice (W2_DESIGN 7.1, 7.3; A §4.2): additive firing-order synthesis for combustion
    /// engines (8–16 load-tilted harmonics of the firing frequency, cycle jitter, exhaust pipe band-pass,
    /// cycle-gated combustion noise, intake noise, diesel injection ticks, turbo whistle, CVT belt whine, overrun
    /// pops, cruiser thump gating, the 3-cylinder half-order burble), motor whine for electric models, freewheel
    /// and chain for bicycles, plus tyre noise per surface for all of them. Phone-speaker friendly: harmonics
    /// 2–8 carry the pitch of low engines (missing fundamental, §7.1).
    /// <para>Render is real-time safe: no allocation, no locks, no Unity API. Output is mono in [−1, 1] and
    /// deterministic per (preset, seed, parameter sequence, sample rate).</para>
    /// </summary>
    public sealed class EngineVoice
    {
        /// <summary>Most harmonics any preset uses.</summary>
        public const int MaxHarmonics = 24;

        private const int Block = 32;

        private EnginePreset _p;
        private uint _seed;
        private readonly float[] _amps = new float[MaxHarmonics];
        private float _levelGain;

        private Noise _noise;
        private Biquad _pipe, _intake, _inject, _tyre, _grit, _ring, _click;
        private OnePole _air, _hiss;

        private double _firePhase;
        private double _halfPhase;
        private float _jitPeriod;
        private float _jitAmp;
        private float _tickEnv;
        private float _popEnv;
        private double _turboPhase, _beltPhase, _rattlePhase;
        private double _whinePhase, _meshPhase;
        private float _whineHz;
        private double _clickPhase, _chainPhase, _crankPhase;
        private float _clickEnv;
        private double _rumblePhase, _impactPhase;
        private float _impactEnv, _grainEnv;
        private int _lastRate;
        private int _filterSurface = -1;

        public EngineVoice(in EnginePreset p, uint seed)
        {
            Reset(p, seed);
        }

        public EnginePreset Preset
        {
            get { return _p; }
        }

        /// <summary>Re-initialises the voice for another vehicle (pooling without allocation).</summary>
        public void Reset(in EnginePreset p, uint seed)
        {
            _p = p;
            _seed = seed;
            _noise = new Noise(Dsp.Mix(seed, 0x454E4749u));
            _levelGain = Dsp.DbToGain(p.LevelDb);
            _firePhase = (_noise.Next01());
            _halfPhase = 0;
            _jitPeriod = 0f;
            _jitAmp = 1f;
            _tickEnv = _popEnv = 0f;
            _turboPhase = _beltPhase = _rattlePhase = 0;
            _whinePhase = _meshPhase = 0;
            _whineHz = p.WhineMinHz;
            _clickPhase = _chainPhase = _crankPhase = 0;
            _clickEnv = 0f;
            _rumblePhase = _impactPhase = 0;
            _impactEnv = _grainEnv = 0f;
            _lastRate = 0;
            _filterSurface = -1;
            _pipe = default;
            _intake = default;
            _inject = default;
            _tyre = default;
            _grit = default;
            _ring = default;
            _click = default;
            _air = default;
            _hiss = default;
        }

        public uint Seed
        {
            get { return _seed; }
        }

        private void SetupFilters(int sr)
        {
            _lastRate = sr;
            _pipe.Set(BiquadKind.BandPass, _p.PipeHz > 0f ? _p.PipeHz * 2f : 200f, Math.Max(0.5f, _p.PipeQ), sr);
            _intake.Set(BiquadKind.BandPass, 1100f, 1.2f, sr);
            _inject.Set(BiquadKind.BandPass, 3000f, 2.5f, sr);
            _tyre.Set(BiquadKind.BandPass, 800f, 0.6f, sr);
            _click.Set(BiquadKind.BandPass, 4000f, 4f, sr);
            _hiss.SetLowPass(6000f, sr);
            _filterSurface = -1;
        }

        private void SetupSurface(int surface, int sr)
        {
            _filterSurface = surface;
            switch ((FootstepSurface)surface)
            {
                case FootstepSurface.Gravel:
                    _grit.Set(BiquadKind.BandPass, 4500f, 1.2f, sr);
                    break;
                case FootstepSurface.Metal:
                    _grit.Set(BiquadKind.BandPass, 2500f, 2f, sr);
                    break;
                case FootstepSurface.Water:
                    _grit.Set(BiquadKind.BandPass, 1500f, 0.8f, sr);
                    break;
                default:
                    _grit.Set(BiquadKind.BandPass, 3500f, 1.5f, sr);
                    break;
            }
            _ring.Set(BiquadKind.BandPass, surface == (int)FootstepSurface.Metal ? 900f : 300f, surface == (int)FootstepSurface.Metal ? 9f : 2f, sr);
        }

        /// <summary>Tyre level (dB) and roughness (0..1) per surface (A §2.5 tyre table).</summary>
        private static void SurfaceTraits(int surface, out float gain, out float rough)
        {
            switch ((FootstepSurface)surface)
            {
                case FootstepSurface.Brick: gain = 1.41f; rough = 0.5f; return;
                case FootstepSurface.Stone: gain = 1.78f; rough = 1.0f; return;
                case FootstepSurface.Gravel: gain = 2.0f; rough = 0.8f; return;
                case FootstepSurface.Dirt: gain = 1.26f; rough = 0.6f; return;
                case FootstepSurface.Mud: gain = 1.26f; rough = 0.4f; return;
                case FootstepSurface.Grass: gain = 0.8f; rough = 0.3f; return;
                case FootstepSurface.Wood: gain = 1.58f; rough = 0.7f; return;
                case FootstepSurface.Metal: gain = 2.0f; rough = 0.9f; return;
                case FootstepSurface.Water: gain = 1.58f; rough = 0.3f; return;
                default: gain = 1f; rough = 0f; return;
            }
        }

        /// <summary>
        /// Renders <paramref name="frames"/> mono samples into <c>mono[0..frames)</c> (overwriting), ramping every
        /// parameter linearly from <paramref name="from"/> to <paramref name="to"/>.
        /// </summary>
        public void Render(float[] mono, int frames, int sampleRate, in EngineVoiceParams from, in EngineVoiceParams to)
        {
            if (mono == null || frames <= 0) return;
            if (frames > mono.Length) frames = mono.Length;
            int sr = sampleRate > 0 ? sampleRate : 48000;
            if (sr != _lastRate) SetupFilters(sr);
            int surf = to.Surface;
            if (surf != _filterSurface) SetupSurface(surf, sr);
            _air.SetLowPass(Dsp.Sanitize(to.AirCutoffHz), sr);

            float invSr = 1f / sr;
            for (int b0 = 0; b0 < frames; b0 += Block)
            {
                int n = Math.Min(Block, frames - b0);
                float ta = (float)b0 / frames;
                float tb = (float)(b0 + n) / frames;
                RenderBlock(mono, b0, n, sr, invSr, from, to, ta, tb);
            }
            Dsp.Finish(mono, 0, frames);
        }

        private static float P(float a, float b, float t)
        {
            return Dsp.Sanitize(a) + (Dsp.Sanitize(b) - Dsp.Sanitize(a)) * t;
        }

        private void RenderBlock(float[] o, int start, int n, int sr, float invSr, in EngineVoiceParams f, in EngineVoiceParams t,
                                 float ta, float tb)
        {
            float tm = 0.5f * (ta + tb);
            float load = Dsp.Clamp(P(f.Load, t.Load, tm), -1f, 1f);
            float speed = Math.Abs(P(f.SpeedMps, t.SpeedMps, tm));
            float dop = Dsp.Clamp(P(f.DopplerPitch, t.DopplerPitch, tm), 0.5f, 2f);
            if (dop <= 0f) dop = 1f;
            float wet = Dsp.Clamp01(P(f.Wetness, t.Wetness, tm));
            float gainA = Math.Max(0f, P(f.Gain, t.Gain, ta));
            float gainB = Math.Max(0f, P(f.Gain, t.Gain, tb));
            float vmax = Math.Max(5f, _p.MaxSpeedKmh) / 3.6f;
            float speedNorm = Dsp.Clamp01(speed / vmax);

            SurfaceTraits(t.Surface, out float sGain, out float rough);
            float tyreLevel = 8f * _p.TyreLevel * (float)Math.Pow(Math.Min(speed, 40f) / 15f, 1.5) * sGain * (1f + 0.6f * wet);

            // Tyre texture rates (A §2.5): brick rumble at v / 0.23 m, cobble at v / 0.4 m, planks at v / 0.25 m.
            float rumbleHz = 0f, impactHz = 0f, grainRate = 0f;
            switch ((FootstepSurface)t.Surface)
            {
                case FootstepSurface.Brick: rumbleHz = speed / 0.23f; break;
                case FootstepSurface.Stone: impactHz = speed / 0.4f; break;
                case FootstepSurface.Wood: impactHz = speed / 0.25f; break;
                case FootstepSurface.Metal: impactHz = speed / 0.5f; break;
                case FootstepSurface.Gravel: grainRate = 200f + 600f * Dsp.Clamp01(speed / 15f); break;
                case FootstepSurface.Dirt:
                case FootstepSurface.Mud: rumbleHz = speed / 0.6f; break;
            }
            if (speed < 0.3f) grainRate = 0f;
            float rumbleInc = rumbleHz * dop * invSr;
            float impactInc = impactHz * dop * invSr;
            float grainP = grainRate * invSr;

            switch (_p.Family)
            {
                case EngineFamily.Combustion:
                    BlockCombustion(o, start, n, invSr, f, t, ta, tb, load, speedNorm, dop, rough);
                    break;
                case EngineFamily.Electric:
                    BlockElectric(o, start, n, invSr, load, speed, speedNorm, dop, rough);
                    break;
                default:
                    BlockPedal(o, start, n, invSr, load, speed, dop);
                    break;
            }

            // Tyres, air absorption and the gain ramp.
            float impactDecay = 1f - 0.004f * (48000f * invSr);
            float grainDecay = 1f - 0.02f * (48000f * invSr);
            for (int i = 0; i < n; i++)
            {
                // The preset level scales the engine only: tyres keep their own level (EVs are mostly tyres).
                float s = o[start + i] * _levelGain;
                if (tyreLevel > 1e-5f)
                {
                    float tyre = _tyre.Process(_noise.Pink());
                    if (rumbleInc > 0f)
                    {
                        _rumblePhase += rumbleInc;
                        if (_rumblePhase >= 1.0) _rumblePhase -= 1.0;
                        tyre *= 1f + 0.6f * Dsp.Sin01((float)_rumblePhase);
                    }
                    float tex = 0f;
                    if (impactInc > 0f)
                    {
                        _impactPhase += impactInc;
                        if (_impactPhase >= 1.0)
                        {
                            _impactPhase -= 1.0;
                            _impactEnv = 0.5f + 0.5f * _noise.Next01();
                        }
                        tex += _ring.Process(_noise.White() * _impactEnv) * 2f;
                        _impactEnv *= impactDecay;
                    }
                    if (grainP > 0f)
                    {
                        if (_noise.Next01() < grainP) _grainEnv = 0.4f + 0.6f * _noise.Next01();
                        tex += _grit.Process(_noise.White() * _grainEnv);
                        _grainEnv *= grainDecay;
                    }
                    s += (tyre + tex * 0.7f) * tyreLevel;
                }
                s = _air.Process(s);
                float g = gainA + (gainB - gainA) * ((float)i / n);
                o[start + i] = s * g;
            }
        }

        private void ComputeAmps(float load, float fireHz, int sr, out int count, out float norm)
        {
            float[] h = _p.Harmonics;
            int hn = Math.Min(h.Length, MaxHarmonics);
            float tilt;
            if (load >= 0f) tilt = Dsp.Lerp(_p.TiltOff, _p.TiltOn, load);
            else if (EnginePresets.IsHeavy(_p.Model) && load < -0.5f) tilt = _p.TiltOn * 0.8f; // exhaust-brake "blat"
            else tilt = _p.TiltOff + 0.3f * -load;
            count = 0;
            norm = 0f;
            float limit = 0.45f * sr;
            for (int k = 0; k < hn; k++)
            {
                float fk = fireHz * (k + 1);
                if (fk >= limit) break;
                float a = h[k] * (float)Math.Pow(k + 1, -tilt + 1f);
                _amps[k] = a;
                norm += a;
                count = k + 1;
            }
            if (norm < 1e-6f) norm = 1f;
        }

        private void BlockCombustion(float[] o, int start, int n, float invSr, in EngineVoiceParams f, in EngineVoiceParams t,
                                     float ta, float tb, float load, float speedNorm, float dop, float rough)
        {
            float red = Math.Max(_p.RedRpm, _p.IdleRpm + 1f);
            float rpmA = Dsp.Clamp(P(f.Rpm, t.Rpm, ta), 0f, red * 1.1f);
            float rpmB = Dsp.Clamp(P(f.Rpm, t.Rpm, tb), 0f, red * 1.1f);
            float rpm = 0.5f * (rpmA + rpmB);
            float rpmNorm = Dsp.Clamp01((rpm - _p.IdleRpm) / (red - _p.IdleRpm));
            float fireA = _p.FiringHz(rpmA) * dop, fireB = _p.FiringHz(rpmB) * dop;
            int sr = _lastRate;
            ComputeAmps(load, 0.5f * (fireA + fireB), sr, out int hc, out float norm);
            float invNorm = 1.6f / norm;
            float pos = load > 0f ? load : 0f;
            float noiseLevel = _p.NoiseBase + _p.NoiseLoad * pos;
            float drive = Dsp.Lerp(1f, Math.Max(1f, _p.Drive), 0.4f + 0.6f * pos);
            float driveNorm = 1f / Dsp.SoftClip(drive * 0.7f);
            // On load the engine is louder as well as brighter; overrun is quieter.
            driveNorm *= 0.7f + 0.3f * load;
            float intakeLevel = 0.3f * pos * rpmNorm;
            float turboHz = Dsp.Lerp(2000f, 4000f, rpmNorm) * dop;
            float turboLevel = _p.Turbo * pos * rpmNorm;
            float beltHz = Dsp.Lerp(600f, 1200f, speedNorm) * dop;
            float beltLevel = _p.BeltWhine * (0.3f + 0.7f * speedNorm);
            float rattle = _p.Rattle * rough * Math.Min(1f, speedNorm * 3f);
            float popChance = _p.PopsPerS > 0f && load < -0.1f ? _p.PopsPerS * -load : 0f;
            float tickDecay = 1f - 0.33f * (48000f * invSr) * 0.05f;
            float popDecay = 1f - 0.0015f * (48000f * invSr);
            float duty = _p.ThumpDuty;
            float halfAm = _p.HalfOrderAm;
            bool diesel = _p.InjectionTick > 0f;

            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                double inc = (fireA + (fireB - fireA) * u) * invSr * (1f + _jitPeriod);
                _firePhase += inc;
                if (_firePhase >= 1.0)
                {
                    _firePhase -= Math.Floor(_firePhase);
                    _jitPeriod = Dsp.Clamp(_noise.Gauss() * _p.JitterPeriod, -0.12f, 0.12f);
                    _jitAmp = Dsp.Clamp(1f + _noise.Gauss() * _p.JitterAmp, 0.5f, 1.5f);
                    if (diesel) _tickEnv = 1f;
                    float fireHz = fireA + (fireB - fireA) * u;
                    if (popChance > 0f && fireHz > 0f && _noise.Next01() < popChance / fireHz) _popEnv = 1f;
                }
                float ph = (float)_firePhase;
                float pulse = 0f;
                for (int k = 0; k < hc; k++)
                {
                    float x = ph * (k + 1);
                    x -= (int)x;
                    pulse += _amps[k] * Dsp.Sin01(x);
                }
                pulse *= invNorm * _jitAmp;
                if (duty > 0f) pulse *= ph < duty ? 0.15f + 0.85f * Dsp.Hann(ph / duty) * 1.6f : 0.15f;
                if (halfAm > 0f)
                {
                    _halfPhase += (rpm / 120f) * dop * invSr;
                    if (_halfPhase >= 1.0) _halfPhase -= 1.0;
                    pulse *= 1f + halfAm * Dsp.Sin01((float)_halfPhase);
                }

                float exhaust = _pipe.Process(pulse) * 0.6f + pulse * 0.5f;
                float gate = 0.5f + 0.5f * Dsp.Sin01(ph + 0.25f >= 1f ? ph - 0.75f : ph + 0.25f);
                float comb = _noise.Pink() * noiseLevel * gate;
                float intake = intakeLevel > 0f ? _intake.Process(_noise.White()) * intakeLevel : 0f;
                float mech = 0f;
                if (diesel && _tickEnv > 1e-3f)
                {
                    mech = _inject.Process(_noise.White() * _tickEnv) * _p.InjectionTick * 3f;
                    _tickEnv *= tickDecay;
                }
                float pop = 0f;
                if (_popEnv > 1e-3f)
                {
                    pop = _noise.White() * _popEnv * 0.8f;
                    _popEnv *= popDecay;
                }
                float core = Dsp.SoftClip(drive * (exhaust + comb + intake + mech + pop)) * driveNorm;

                if (turboLevel > 1e-4f)
                {
                    _turboPhase += turboHz * invSr;
                    if (_turboPhase >= 1.0) _turboPhase -= 1.0;
                    core += Dsp.Sin01((float)_turboPhase) * turboLevel;
                }
                if (beltLevel > 1e-4f)
                {
                    _beltPhase += beltHz * invSr;
                    if (_beltPhase >= 1.0) _beltPhase -= 1.0;
                    core += Dsp.Sin01((float)_beltPhase) * beltLevel;
                }
                if (rattle > 1e-3f)
                {
                    _rattlePhase += 11f * invSr;
                    if (_rattlePhase >= 1.0) _rattlePhase -= 1.0;
                    core *= 1f + 0.5f * rattle * Dsp.Sin01((float)_rattlePhase);
                }
                o[start + i] = core * 0.7f;
            }
        }

        private void BlockElectric(float[] o, int start, int n, float invSr, float load, float speed, float speedNorm, float dop, float rough)
        {
            float target = Dsp.Lerp(_p.WhineMinHz, _p.WhineMaxHz, speedNorm);
            float lag = _p.WhineLagS > 0f ? _p.WhineLagS : 0.05f;
            float k = Dsp.Clamp01(n * invSr / lag);
            _whineHz += (target - _whineHz) * k;
            if (!(_whineHz > 0f)) _whineHz = _p.WhineMinHz;
            float level = speed < 0.3f && load <= 0.02f ? 0.15f : 0.35f + 0.65f * Dsp.Clamp01(Math.Abs(load));
            if (_p.Model == EngineModel.CarEv) level *= speed < 5.5f ? 1f : 0.3f; // pedestrian hum below 20 km/h
            float hz = _whineHz * dop;
            float meshHz = hz * _p.GearMesh;
            float meshLevel = _p.GearMesh > 0f ? 0.35f : 0f;
            float hissLevel = _p.NoiseBase * (load > 0f ? 1f : 0.3f);
            float rattle = _p.Rattle * rough * Math.Min(1f, speedNorm * 3f);
            float[] h = _p.Harmonics;
            float a2 = h.Length > 1 ? h[1] : 0.3f;
            float a3 = h.Length > 2 ? h[2] : 0.15f;
            for (int i = 0; i < n; i++)
            {
                _whinePhase += hz * invSr;
                if (_whinePhase >= 1.0) _whinePhase -= Math.Floor(_whinePhase);
                float ph = (float)_whinePhase;
                float x2 = ph * 2f; x2 -= (int)x2;
                float x3 = ph * 3f; x3 -= (int)x3;
                float s = (Dsp.Sin01(ph) + a2 * Dsp.Sin01(x2) + a3 * Dsp.Sin01(x3)) * 0.6f * level;
                if (meshLevel > 0f)
                {
                    _meshPhase += meshHz * invSr;
                    if (_meshPhase >= 1.0) _meshPhase -= Math.Floor(_meshPhase);
                    s += Dsp.Sin01((float)_meshPhase) * meshLevel * level * 0.5f;
                }
                if (hissLevel > 0f) s += _hiss.ProcessHighPass(_noise.White()) * hissLevel;
                if (rattle > 1e-3f)
                {
                    _rattlePhase += 12f * invSr;
                    if (_rattlePhase >= 1.0) _rattlePhase -= 1.0;
                    float r = Dsp.Sin01((float)_rattlePhase);
                    s += (r > 0.92f ? _noise.White() * 0.4f : 0f) * rattle;
                }
                o[start + i] = s;
            }
        }

        private void BlockPedal(float[] o, int start, int n, float invSr, float load, float speed, float dop)
        {
            float circ = _p.WheelCircM > 0.1f ? _p.WheelCircM : 2.1f;
            bool coasting = load <= 0.05f && speed > 0.5f;
            bool pedalling = load > 0.05f && speed > 0.2f;
            // Freewheel: 20 pawl engagements per wheel revolution (A §2.3: 36–48 clicks/s at 15 km/h).
            float clickInc = coasting ? 20f * speed / circ * dop * invSr : 0f;
            float chainHz = Dsp.Lerp(40f, 66f, Dsp.Clamp01(speed / 7f)) * dop;
            float crankHz = Dsp.Lerp(1.0f, 1.5f, Dsp.Clamp01(speed / 7f));
            float clickDecay = 1f - 0.035f * (48000f * invSr);
            for (int i = 0; i < n; i++)
            {
                float s = 0f;
                if (clickInc > 0f)
                {
                    _clickPhase += clickInc;
                    if (_clickPhase >= 1.0)
                    {
                        _clickPhase -= Math.Floor(_clickPhase);
                        _clickEnv = 0.8f + 0.2f * _noise.Next01();
                    }
                }
                if (_clickEnv > 1e-3f)
                {
                    s += _click.Process(_noise.White() * _clickEnv) * 1.6f;
                    _clickEnv *= clickDecay;
                }
                if (pedalling)
                {
                    _chainPhase += chainHz * invSr;
                    if (_chainPhase >= 1.0) _chainPhase -= Math.Floor(_chainPhase);
                    float ph = (float)_chainPhase;
                    float x2 = ph * 2f; x2 -= (int)x2;
                    float x3 = ph * 3f; x3 -= (int)x3;
                    s += (Dsp.Sin01(ph) + 0.6f * Dsp.Sin01(x2) + 0.4f * Dsp.Sin01(x3)) * 0.05f;
                    _crankPhase += 2f * crankHz * invSr;
                    if (_crankPhase >= 1.0)
                    {
                        _crankPhase -= Math.Floor(_crankPhase);
                        _clickEnv = Math.Max(_clickEnv, 0.35f);
                    }
                }
                o[start + i] = s;
            }
        }
    }
}
