using System;

namespace Ghumante.Core.Synth
{
    /// <summary>
    /// A bank of exponentially decaying sinusoids (modal synthesis) for bells, clicks and metal (W2_DESIGN 7.1,
    /// A §4.3): each mode is a two-pole resonator <c>y = 2r·cos(ω)·y₁ − r²·y₂ + g·x</c>, so a strike is just an
    /// excitation fed to <see cref="Process"/>. Capacity is fixed at construction; no allocation afterwards.
    /// </summary>
    public sealed class ModalBank
    {
        private readonly float[] _c1, _c2, _gain, _y1, _y2;
        private int _count;

        public ModalBank(int capacity)
        {
            int n = Math.Max(1, capacity);
            _c1 = new float[n];
            _c2 = new float[n];
            _gain = new float[n];
            _y1 = new float[n];
            _y2 = new float[n];
        }

        public int Count
        {
            get { return _count; }
        }

        public int Capacity
        {
            get { return _c1.Length; }
        }

        /// <summary>Removes every mode and clears the state.</summary>
        public void Clear()
        {
            _count = 0;
            Array.Clear(_y1, 0, _y1.Length);
            Array.Clear(_y2, 0, _y2.Length);
        }

        /// <summary>Adds a mode at <paramref name="freqHz"/> that decays by 60 dB in <paramref name="t60S"/>.
        /// Modes at or above 0.45·fs are ignored (no aliasing). Returns false when full or ignored.</summary>
        public bool Add(float freqHz, float t60S, float amplitude, int sampleRate)
        {
            if (_count >= _c1.Length || sampleRate <= 0) return false;
            if (!(freqHz > 0f) || freqHz >= 0.45f * sampleRate) return false;
            double w = 2.0 * Math.PI * freqHz / sampleRate;
            double r = Dsp.T60Decay(Math.Max(0.001f, t60S), sampleRate);
            _c1[_count] = (float)(2.0 * r * Math.Cos(w));
            _c2[_count] = (float)(r * r);
            // Normalise so an impulse of 1 gives a peak near `amplitude` (resonator gain ≈ 1/sin ω).
            _gain[_count] = amplitude * (float)Math.Sin(w);
            _y1[_count] = 0f;
            _y2[_count] = 0f;
            _count++;
            return true;
        }

        /// <summary>Feeds <paramref name="excitation"/> to every mode and returns the summed output.</summary>
        public float Process(float excitation)
        {
            float sum = 0f;
            for (int i = 0; i < _count; i++)
            {
                float y = _c1[i] * _y1[i] - _c2[i] * _y2[i] + _gain[i] * excitation;
                _y2[i] = _y1[i];
                _y1[i] = y;
                sum += y;
            }
            if (float.IsNaN(sum) || float.IsInfinity(sum))
            {
                Array.Clear(_y1, 0, _y1.Length);
                Array.Clear(_y2, 0, _y2.Length);
                return 0f;
            }
            return sum;
        }
    }

    /// <summary>
    /// Hann-windowed grain player over a mono source buffer (W2_DESIGN 7.1): up to <c>capacity</c> overlapping
    /// grains, each with its own start, length, rate and gain. Used for granular textures (crowd footsteps,
    /// walla). The source array is not copied. No allocation after construction.
    /// </summary>
    public sealed class GrainPlayer
    {
        private readonly float[] _pos, _rate, _gain, _age, _len;
        private float[] _source;

        public GrainPlayer(int capacity)
        {
            int n = Math.Max(1, capacity);
            _pos = new float[n];
            _rate = new float[n];
            _gain = new float[n];
            _age = new float[n];
            _len = new float[n];
        }

        /// <summary>The buffer grains read from (may be replaced at any time; active grains are dropped).</summary>
        public float[] Source
        {
            get { return _source; }
            set
            {
                _source = value;
                Array.Clear(_len, 0, _len.Length);
            }
        }

        public int Active
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _len.Length; i++)
                    if (_len[i] > 0f) n++;
                return n;
            }
        }

        /// <summary>Starts a grain at source sample <paramref name="start"/> lasting <paramref name="lengthSamples"/>
        /// output samples. Returns false when every slot is busy or there is no source.</summary>
        public bool Trigger(int start, int lengthSamples, float rate, float gain)
        {
            if (_source == null || _source.Length < 2 || lengthSamples < 2) return false;
            for (int i = 0; i < _len.Length; i++)
            {
                if (_len[i] > 0f) continue;
                _pos[i] = Math.Max(0, Math.Min(start, _source.Length - 2));
                _rate[i] = Dsp.Clamp(rate, 0.1f, 4f);
                _gain[i] = Dsp.Sanitize(gain);
                _age[i] = 0f;
                _len[i] = lengthSamples;
                return true;
            }
            return false;
        }

        public float Process()
        {
            if (_source == null) return 0f;
            float sum = 0f;
            int last = _source.Length - 2;
            for (int i = 0; i < _len.Length; i++)
            {
                float len = _len[i];
                if (len <= 0f) continue;
                float p = _pos[i];
                int pi = (int)p;
                float s;
                if (pi >= last) s = _source[last];
                else s = _source[pi] + (_source[pi + 1] - _source[pi]) * (p - pi);
                sum += s * _gain[i] * Dsp.Hann(_age[i] / len);
                _pos[i] = p + _rate[i];
                _age[i] += 1f;
                if (_age[i] >= len || _pos[i] >= last) _len[i] = 0f;
            }
            return sum;
        }
    }
}
