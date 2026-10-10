using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Deterministic draws for the building grammar and generators (W2_DESIGN 10.2): seeded with FNV-1a of (seed,
    /// purpose), then xorshift32. Same inputs, same sequence, on every device; no allocation.
    /// </summary>
    public struct GrammarRng
    {
        private uint _s;

        public GrammarRng(uint seed, uint purpose)
        {
            _s = Mix(seed, purpose);
            if (_s == 0) _s = 0x9E3779B9;
        }

        /// <summary>FNV-1a 32 over the 8 little-endian bytes of (a, b).</summary>
        public static uint Mix(uint a, uint b)
        {
            unchecked
            {
                uint h = Hashes.Fnv32Offset;
                for (int i = 0; i < 4; i++) h = (h ^ (byte)(a >> (8 * i))) * Hashes.Fnv32Prime;
                for (int i = 0; i < 4; i++) h = (h ^ (byte)(b >> (8 * i))) * Hashes.Fnv32Prime;
                return h;
            }
        }

        public uint NextUInt()
        {
            uint x = _s;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _s = x;
            return x;
        }

        /// <summary>
        /// A child sequence for one element (a door, a balcony, a sign): this sequence advances by exactly one draw
        /// whatever the element then draws, so detail that differs between drop levels (relief, props, plants) never
        /// shifts the structural choices that follow, and a house keeps its balconies, hoods and shops at every level.
        /// </summary>
        public GrammarRng Fork()
        {
            return new GrammarRng(NextUInt(), 0x464F524B);
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Next()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public float Range(float lo, float hi)
        {
            return lo + (hi - lo) * Next();
        }

        /// <summary>Uniform integer in [lo, hi] (inclusive).</summary>
        public int Int(int lo, int hi)
        {
            if (hi <= lo) return lo;
            return lo + (int)(NextUInt() % (uint)(hi - lo + 1));
        }

        public bool Chance(float p)
        {
            return Next() < p;
        }

        /// <summary>An index drawn from cumulative-free weights (any positive scale).</summary>
        public int Pick(float[] weights)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++) sum += weights[i];
            float r = Next() * sum;
            for (int i = 0; i < weights.Length; i++)
            {
                r -= weights[i];
                if (r < 0f) return i;
            }
            return weights.Length - 1;
        }
    }
}
