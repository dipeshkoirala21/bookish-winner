using System;

namespace Ghumante.Core.Synth
{
    /// <summary>Response of a <see cref="Biquad"/> (RBJ audio-EQ cookbook).</summary>
    public enum BiquadKind : byte
    {
        LowPass = 0,
        HighPass = 1,
        /// <summary>Band-pass with 0 dB peak gain.</summary>
        BandPass = 2,
        /// <summary>Peaking EQ (gain in dB).</summary>
        Peak = 3,
        HighShelf = 4,
        LowShelf = 5,
    }

    /// <summary>
    /// RBJ biquad (transposed direct form II) whose coefficients glide to new targets with a one-pole smoother
    /// (5 ms by default, W2_DESIGN 7.1), so parameter changes never click. A mutable struct: keep it in a field.
    /// Call <see cref="Set"/> as often as wanted (per block is typical); <see cref="Snap"/> jumps to the
    /// target. Frequencies are clamped to (10 Hz, 0.45·fs); the state is reset if it ever becomes non-finite.
    /// </summary>
    public struct Biquad
    {
        private float _b0, _b1, _b2, _a1, _a2;      // current
        private float _tb0, _tb1, _tb2, _ta1, _ta2; // target
        private float _z1, _z2;
        private float _smooth;
        private bool _init;

        /// <summary>Sets the target response. <paramref name="smoothS"/> is the coefficient glide time.</summary>
        public void Set(BiquadKind kind, float freqHz, float q, int sampleRate, float gainDb = 0f, float smoothS = 0.005f)
        {
            if (sampleRate <= 0) sampleRate = 48000;
            float nyq = 0.45f * sampleRate;
            float f = Dsp.Clamp(freqHz, 10f, nyq);
            float qq = Dsp.Clamp(q, 0.05f, 60f);
            double w0 = 2.0 * Math.PI * f / sampleRate;
            double cs = Math.Cos(w0), sn = Math.Sin(w0);
            double alpha = sn / (2.0 * qq);
            double A = Math.Pow(10.0, Dsp.Clamp(gainDb, -48f, 48f) / 40.0);
            double b0, b1, b2, a0, a1, a2;
            switch (kind)
            {
                case BiquadKind.HighPass:
                    b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2;
                    a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha;
                    break;
                case BiquadKind.BandPass:
                    b0 = alpha; b1 = 0; b2 = -alpha;
                    a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha;
                    break;
                case BiquadKind.Peak:
                    b0 = 1 + alpha * A; b1 = -2 * cs; b2 = 1 - alpha * A;
                    a0 = 1 + alpha / A; a1 = -2 * cs; a2 = 1 - alpha / A;
                    break;
                case BiquadKind.HighShelf:
                {
                    double sq = 2 * Math.Sqrt(A) * alpha;
                    b0 = A * ((A + 1) + (A - 1) * cs + sq);
                    b1 = -2 * A * ((A - 1) + (A + 1) * cs);
                    b2 = A * ((A + 1) + (A - 1) * cs - sq);
                    a0 = (A + 1) - (A - 1) * cs + sq;
                    a1 = 2 * ((A - 1) - (A + 1) * cs);
                    a2 = (A + 1) - (A - 1) * cs - sq;
                    break;
                }
                case BiquadKind.LowShelf:
                {
                    double sq = 2 * Math.Sqrt(A) * alpha;
                    b0 = A * ((A + 1) - (A - 1) * cs + sq);
                    b1 = 2 * A * ((A - 1) - (A + 1) * cs);
                    b2 = A * ((A + 1) - (A - 1) * cs - sq);
                    a0 = (A + 1) + (A - 1) * cs + sq;
                    a1 = -2 * ((A - 1) + (A + 1) * cs);
                    a2 = (A + 1) + (A - 1) * cs - sq;
                    break;
                }
                default: // LowPass
                    b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = (1 - cs) / 2;
                    a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha;
                    break;
            }
            _tb0 = (float)(b0 / a0);
            _tb1 = (float)(b1 / a0);
            _tb2 = (float)(b2 / a0);
            _ta1 = (float)(a1 / a0);
            _ta2 = (float)(a2 / a0);
            _smooth = Dsp.SmoothCoef(smoothS, sampleRate);
            if (!_init)
            {
                Snap();
                _init = true;
            }
        }

        /// <summary>Jumps the current coefficients to the target.</summary>
        public void Snap()
        {
            _b0 = _tb0; _b1 = _tb1; _b2 = _tb2; _a1 = _ta1; _a2 = _ta2;
        }

        /// <summary>Clears the filter memory.</summary>
        public void Reset()
        {
            _z1 = _z2 = 0f;
        }

        public float Process(float x)
        {
            if (!_init) return x;
            float k = _smooth;
            _b0 += (_tb0 - _b0) * k;
            _b1 += (_tb1 - _b1) * k;
            _b2 += (_tb2 - _b2) * k;
            _a1 += (_ta1 - _a1) * k;
            _a2 += (_ta2 - _a2) * k;
            float y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            if (float.IsNaN(y) || float.IsInfinity(y) || y > 1e6f || y < -1e6f)
            {
                _z1 = _z2 = 0f;
                return 0f;
            }
            return y;
        }
    }

    /// <summary>
    /// One-pole low-pass (6 dB/octave) for air absorption and parameter smoothing. Cutoff 0 or ≥ 0.45·fs is a
    /// bypass. A mutable struct.
    /// </summary>
    public struct OnePole
    {
        private float _z;
        private float _a;
        private bool _bypass;

        public void SetLowPass(float cutoffHz, int sampleRate)
        {
            if (sampleRate <= 0 || !(cutoffHz > 0f) || cutoffHz >= 0.45f * sampleRate)
            {
                _bypass = true;
                _a = 1f;
                return;
            }
            _bypass = false;
            _a = 1f - (float)Math.Exp(-2.0 * Math.PI * cutoffHz / sampleRate);
        }

        public float Process(float x)
        {
            if (_bypass)
            {
                _z = x;
                return x;
            }
            _z += (x - _z) * _a;
            if (float.IsNaN(_z) || float.IsInfinity(_z)) _z = 0f;
            return _z;
        }

        /// <summary>High-pass complement of the low-pass (x − lp(x)).</summary>
        public float ProcessHighPass(float x)
        {
            return x - Process(x);
        }

        public void Reset()
        {
            _z = 0f;
        }
    }

    /// <summary>
    /// Fixed-capacity delay line with linear fractional read, for the ground-reflection comb, slapback and
    /// combs. Allocates once in the constructor.
    /// </summary>
    public sealed class DelayLine
    {
        private readonly float[] _buf;
        private int _w;

        public DelayLine(int capacitySamples)
        {
            _buf = new float[Math.Max(4, capacitySamples)];
        }

        public int Capacity
        {
            get { return _buf.Length; }
        }

        public void Write(float x)
        {
            _buf[_w] = x;
            _w++;
            if (_w >= _buf.Length) _w = 0;
        }

        /// <summary>The sample written <paramref name="delaySamples"/> ago (fractional; clamped to the capacity).</summary>
        public float Read(float delaySamples)
        {
            float d = Dsp.Clamp(delaySamples, 1f, _buf.Length - 2);
            int di = (int)d;
            float f = d - di;
            int i0 = _w - di;
            if (i0 < 0) i0 += _buf.Length;
            int i1 = i0 - 1;
            if (i1 < 0) i1 += _buf.Length;
            return _buf[i0] + (_buf[i1] - _buf[i0]) * f;
        }

        public void Clear()
        {
            Array.Clear(_buf, 0, _buf.Length);
            _w = 0;
        }
    }
}
