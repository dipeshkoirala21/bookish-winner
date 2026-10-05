using System;

namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Sound-side RPM and load model (A §4.2 "RPM from speed"): callers that only know speed, throttle and brake
    /// (traffic agents, the arcade vehicle) step this to get the <see cref="EngineVoiceParams.Rpm"/> and
    /// <see cref="EngineVoiceParams.Load"/> an <see cref="EngineVoice"/> wants. Gears shift up at
    /// <see cref="EnginePreset.ShiftUp"/> × red under throttle and down at <see cref="EnginePreset.ShiftDown"/>;
    /// a shift drops rpm and cuts load for <see cref="EnginePreset.ShiftS"/> (cartoon-snappy). CVT scooters
    /// jump to the hold rpm and stay there while speed rises. The overall ratio is calibrated so top gear
    /// reaches 90% of red at the preset's top speed. A mutable struct, deterministic, no allocation.
    /// </summary>
    public struct Drivetrain
    {
        private EnginePreset _p;
        private float _overall;
        private int _gear;
        private float _shiftT;
        private float _rpm;
        private float _load;
        private bool _init;

        /// <summary>Current engine rpm (electric: a nominal motor rpm proportional to speed).</summary>
        public float Rpm
        {
            get { return _rpm; }
        }

        /// <summary>Load in [−1, 1]: positive under throttle, negative on overrun or engine braking.</summary>
        public float Load
        {
            get { return _load; }
        }

        /// <summary>Zero-based gear (0 for CVT, electric and pedal).</summary>
        public int Gear
        {
            get { return _gear; }
        }

        /// <summary>Number of gear changes so far (a gear clunk may be played when it increments).</summary>
        public int Shifts { get; private set; }

        public Drivetrain(in EnginePreset preset)
        {
            _p = preset;
            _gear = 0;
            _shiftT = 0f;
            _rpm = preset.IdleRpm;
            _load = 0f;
            _init = true;
            Shifts = 0;
            _overall = 1f;
            float vmax = Math.Max(5f, preset.MaxSpeedKmh) / 3.6f;
            float circ = preset.WheelCircM > 0.1f ? preset.WheelCircM : 1.9f;
            float top = preset.Gears != null && preset.Gears.Length > 0 ? preset.Gears[preset.Gears.Length - 1] : 1f;
            float wheelRpmAtTop = vmax / circ * 60f;
            if (preset.RedRpm > 0f && wheelRpmAtTop > 0f && top > 0f) _overall = 0.9f * preset.RedRpm / (wheelRpmAtTop * top);
        }

        /// <summary>Advances by <paramref name="dt"/> seconds. Speed is signed (reverse is negative).</summary>
        public void Step(float dt, float speedMps, float throttle01, float brake01)
        {
            if (!_init) this = new Drivetrain(_p);
            float h = Dsp.Clamp(dt, 0f, 0.25f);
            float v = Math.Abs(Dsp.Sanitize(speedMps));
            float th = Dsp.Clamp01(throttle01);
            float br = Dsp.Clamp01(brake01);
            float red = Math.Max(_p.RedRpm, _p.IdleRpm + 1f);

            if (_p.Family != EngineFamily.Combustion)
            {
                float vmax = Math.Max(5f, _p.MaxSpeedKmh) / 3.6f;
                _rpm = Dsp.Clamp(v / vmax, 0f, 1.2f) * 10000f;
                float target = th > 0.02f ? th : (v > 0.5f && br > 0.05f ? -0.5f * br : 0f);
                _load += (target - _load) * Dsp.Clamp01(h / 0.08f);
                return;
            }

            float circ = _p.WheelCircM > 0.1f ? _p.WheelCircM : 1.9f;
            float wheelRpm = v / circ * 60f;
            float rpmTarget;
            if (_p.Cvt)
            {
                float vmax = Math.Max(5f, _p.MaxSpeedKmh) / 3.6f;
                float hold = _p.CvtHoldRpm > 0f ? _p.CvtHoldRpm : 0.7f * red;
                float tr = Dsp.Lerp(_p.IdleRpm, hold, th);
                if (v > 0.6f * vmax) tr = Math.Max(tr, Dsp.Lerp(hold, 0.94f * red, Dsp.Clamp01((v / vmax - 0.6f) / 0.4f)));
                // Coasting: the belt still turns the engine a little above idle.
                float coast = _p.IdleRpm + wheelRpm * _overall * 0.4f;
                rpmTarget = Math.Max(tr, Math.Min(coast, 0.6f * red));
            }
            else
            {
                float[] g = _p.Gears;
                int n = g != null ? g.Length : 0;
                if (n == 0)
                {
                    rpmTarget = _p.IdleRpm + th * (red - _p.IdleRpm);
                }
                else
                {
                    if (_gear >= n) _gear = n - 1;
                    float gearRpm = wheelRpm * _overall * g[_gear];
                    if (_shiftT <= 0f)
                    {
                        if (gearRpm > _p.ShiftUp * red && _gear < n - 1 && th > 0.1f)
                        {
                            _gear++;
                            _shiftT = Math.Max(0.05f, _p.ShiftS);
                            Shifts++;
                        }
                        else if (_gear > 0 && gearRpm < _p.ShiftDown * red * (th > 0.1f ? 0.75f : 1f) &&
                                 wheelRpm * _overall * g[_gear - 1] < _p.ShiftUp * red * 0.95f)
                        {
                            _gear--;
                            _shiftT = Math.Max(0.05f, _p.ShiftS * 0.7f);
                            Shifts++;
                        }
                        gearRpm = wheelRpm * _overall * g[_gear];
                    }
                    // Clutch slip at launch: rpm rises with throttle while the wheels are slow.
                    float slip = _p.IdleRpm + th * (0.45f * red - _p.IdleRpm) * Dsp.Clamp01(1f - v / 6f);
                    rpmTarget = Math.Max(Math.Max(gearRpm, _p.IdleRpm), slip);
                }
            }

            float loadTarget;
            if (_shiftT > 0f)
            {
                _shiftT -= h;
                rpmTarget *= 0.82f;
                loadTarget = -0.2f;
            }
            else if (th > 0.02f)
            {
                loadTarget = th;
            }
            else if (v > 1f)
            {
                loadTarget = -0.3f - 0.4f * br;
            }
            else
            {
                loadTarget = 0f;
            }

            rpmTarget = Dsp.Clamp(rpmTarget, _p.IdleRpm * 0.9f, red);
            _rpm += (rpmTarget - _rpm) * Dsp.Clamp01(h / 0.12f);
            _load += (loadTarget - _load) * Dsp.Clamp01(h / 0.06f);
            _rpm = Dsp.Clamp(_rpm, 0f, red);
            _load = Dsp.Clamp(_load, -1f, 1f);
        }
    }
}
