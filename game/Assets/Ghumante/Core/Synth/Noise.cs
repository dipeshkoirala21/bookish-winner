namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Seeded noise and random numbers for the synth (W2_DESIGN 7.1): xorshift32 white noise, Paul Kellet's
    /// pink filter and a leaky-integrator brown noise. A mutable struct: keep it in a field. Deterministic per
    /// seed, allocation-free. A zero seed is replaced by a fixed non-zero one (xorshift has a zero fixed point).
    /// </summary>
    public struct Noise
    {
        private uint _s;
        private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;
        private float _brown;

        public Noise(uint seed)
        {
            _s = seed == 0 ? 0x6D2B79F5u : seed;
            _b0 = _b1 = _b2 = _b3 = _b4 = _b5 = _b6 = 0f;
            _brown = 0f;
        }

        /// <summary>Next raw 32-bit value (xorshift32).</summary>
        public uint NextUInt()
        {
            uint x = _s;
            if (x == 0) x = 0x6D2B79F5u;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _s = x;
            return x;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Next01()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform in [−1, 1).</summary>
        public float White()
        {
            return Next01() * 2f - 1f;
        }

        /// <summary>Uniform in [lo, hi).</summary>
        public float Range(float lo, float hi)
        {
            return lo + (hi - lo) * Next01();
        }

        /// <summary>Uniform integer in [0, n).</summary>
        public int Index(int n)
        {
            if (n <= 1) return 0;
            return (int)(NextUInt() % (uint)n);
        }

        /// <summary>Approximately normal (sum of four uniforms), mean 0, standard deviation about 1.</summary>
        public float Gauss()
        {
            float s = Next01() + Next01() + Next01() + Next01();
            return (s - 2f) * 1.7320508f;
        }

        /// <summary>Pink noise (−3 dB/octave), roughly within ±1 (Kellet's "refined" 7-pole filter).</summary>
        public float Pink()
        {
            float w = White();
            _b0 = 0.99886f * _b0 + w * 0.0555179f;
            _b1 = 0.99332f * _b1 + w * 0.0750759f;
            _b2 = 0.96900f * _b2 + w * 0.1538520f;
            _b3 = 0.86650f * _b3 + w * 0.3104856f;
            _b4 = 0.55000f * _b4 + w * 0.5329522f;
            _b5 = -0.7616f * _b5 - w * 0.0168980f;
            float p = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + w * 0.5362f;
            _b6 = w * 0.115926f;
            return p * 0.11f;
        }

        /// <summary>Brown noise (−6 dB/octave), leaky so it never drifts, roughly within ±1.</summary>
        public float Brown()
        {
            _brown = 0.995f * _brown + 0.05f * White();
            return _brown * 3.2f;
        }
    }
}
