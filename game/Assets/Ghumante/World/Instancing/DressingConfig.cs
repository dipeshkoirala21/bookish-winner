using Ghumante.Core.Generators.Placement;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// Per-tier radii and caps of the instanced dressing (W2_DESIGN 5.8 vegetation table, 5.2 parked vehicles, 10.4
    /// budgets) and the October palette of the tree species. Engine-free.
    /// </summary>
    public sealed class DressingConfig
    {
        /// <summary>Tree LOD0 (≤ 600 tris) radius and cap; LOD1 (≤ 120) radius and cap; far LOD radius and cap.</summary>
        public float TreeLod0M, TreeLod1M, TreeLod2M;

        public int TreeLod0Cap, TreeLod1Cap, TreeLod2Cap;

        /// <summary>Street props are drawn to this radius, at most this many.</summary>
        public float PropM;

        public int PropCap;

        /// <summary>Parked vehicles (W2_DESIGN 5.2): near LOD (moving LOD2) to 20 m, block-out to 80 m, box to 150 m;
        /// caps per level and in all.</summary>
        public float ParkedNearM = 20f, ParkedBlockM = 80f, ParkedBoxM = 150f;

        public int ParkedNearCap, ParkedBlockCap, ParkedCap;

        public static DressingConfig ForTier(int tier)
        {
            switch (tier <= 0 ? 0 : tier >= 2 ? 2 : 1)
            {
                case 0:
                    return new DressingConfig
                    {
                        TreeLod0M = 25f, TreeLod0Cap = 12, TreeLod1M = 120f, TreeLod1Cap = 65, TreeLod2M = 750f, TreeLod2Cap = 1500,
                        PropM = 120f, PropCap = 300, ParkedNearCap = 2, ParkedBlockCap = 20, ParkedCap = 80,
                    };
                case 1:
                    return new DressingConfig
                    {
                        TreeLod0M = 40f, TreeLod0Cap = 33, TreeLod1M = 180f, TreeLod1Cap = 200, TreeLod2M = 1250f, TreeLod2Cap = 4000,
                        PropM = 200f, PropCap = 800, ParkedNearCap = 10, ParkedBlockCap = 60, ParkedCap = 200,
                    };
                default:
                    return new DressingConfig
                    {
                        TreeLod0M = 60f, TreeLod0Cap = 60, TreeLod1M = 250f, TreeLod1Cap = 400, TreeLod2M = 1750f, TreeLod2Cap = 6000,
                        PropM = 250f, PropCap = 1500, ParkedNearCap = 10, ParkedBlockCap = 120, ParkedCap = 400,
                    };
            }
        }

        /// <summary>The tree LOD a distance asks for (0, 1, 2; 3 = not drawn), before the caps.</summary>
        public int TreeLodAt(double distanceM)
        {
            if (distanceM <= TreeLod0M) return 0;
            if (distanceM <= TreeLod1M) return 1;
            if (distanceM <= TreeLod2M) return 2;
            return 3;
        }

        /// <summary>The parked-vehicle level a distance asks for: 0 near (moving LOD2), 1 block-out, 2 box, 3 culled.</summary>
        public int ParkedLevelAt(double distanceM)
        {
            if (distanceM <= ParkedNearM) return 0;
            if (distanceM <= ParkedBlockM) return 1;
            if (distanceM <= ParkedBoxM) return 2;
            return 3;
        }

        /// <summary>Crown colour (sRGB hex) of a species in a month (W2_DESIGN 5.8 look and season): jacaranda violet in
        /// March-May, silky oak golden in April-May, bottlebrush and rhododendron red in their seasons, else foliage.</summary>
        public static uint CrownColour(TreeSpecies s, int month)
        {
            switch (s)
            {
                case TreeSpecies.Pipal: return month == 3 || month == 4 ? 0xC27C5Eu : 0x4E8A3Au;
                case TreeSpecies.Bar: return 0x2F6B2Fu;
                case TreeSpecies.Jacaranda: return month >= 3 && month <= 5 ? 0x8E6CC8u : 0x5E8F45u;
                case TreeSpecies.SilkyOak: return month == 4 || month == 5 ? 0xF2A33Au : 0x557A3Au;
                case TreeSpecies.Bottlebrush: return month >= 3 && month <= 5 ? 0xD7263Du : 0x4F7D3Au;
                case TreeSpecies.Camphor: return 0x3F7F3Au;
                case TreeSpecies.Eucalyptus: return 0x7FA08Au;
                case TreeSpecies.Bamboo: return 0x6FA03Au;
                case TreeSpecies.Palm: return 0x5C9A3Au;
                case TreeSpecies.Schima: return month == 5 || month == 6 ? 0xC9D7B8u : 0x4A7F36u;
                case TreeSpecies.Castanopsis: return 0x4A7F36u;
                case TreeSpecies.Alnus: return 0x4C8B3Eu;
                case TreeSpecies.ChirPine: return 0x4F7A3Au;
                case TreeSpecies.Oak: return 0x3B6B34u;
                case TreeSpecies.Rhododendron: return month >= 2 && month <= 4 ? 0xC8102Eu : 0x3B6B34u;
                case TreeSpecies.BrownOak: return 0x6B7A4Au;
                default: return 0x4C8B3Eu;
            }
        }

        /// <summary>Water-tank colours (W2_DESIGN 2.5: black 55, blue 25, cream 15, yellow 5).</summary>
        public static uint TankColour(float u01)
        {
            return u01 < 0.55f ? 0x2A2A2Eu : u01 < 0.80f ? 0x2F6FD6u : u01 < 0.95f ? 0xE9E4D4u : 0xF2C230u;
        }
    }
}
