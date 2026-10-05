using Ghumante.Core.Data;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// Deterministic per-object randomness for the runtime dressing (W2_DESIGN 10.2): FNV-1a 32 (the
    /// <see cref="Hashes"/> constants) over a 64-bit key and a 32-bit purpose, then a float in [0, 1). The same key and
    /// purpose always give the same value on every device. Engine-free, allocation-free.
    /// </summary>
    public static class WorldHash
    {
        /// <summary>FNV-1a 32 of the 12 little-endian bytes of (key, purpose).</summary>
        public static uint Fnv1a(ulong key, uint purpose)
        {
            unchecked
            {
                uint h = Hashes.Fnv32Offset;
                for (int i = 0; i < 8; i++)
                {
                    h ^= (byte)(key >> (8 * i));
                    h *= Hashes.Fnv32Prime;
                }
                for (int i = 0; i < 4; i++)
                {
                    h ^= (byte)(purpose >> (8 * i));
                    h *= Hashes.Fnv32Prime;
                }
                // FNV's low bits are weak for small keys: finish with an avalanche.
                h ^= h >> 16;
                h *= 0x7FEB352D;
                h ^= h >> 15;
                h *= 0x846CA68B;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>A uniform float in [0, 1) from (key, purpose).</summary>
        public static float Unit(ulong key, uint purpose)
        {
            return (Fnv1a(key, purpose) >> 8) * (1f / 16777216f);
        }

        /// <summary>A uniform float in [a, b).</summary>
        public static float Range(ulong key, uint purpose, float a, float b)
        {
            return a + (b - a) * Unit(key, purpose);
        }
    }
}
