using Ghumante.Core.Generators.Placement;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// Per-tier radii, caps and triangle budgets of the instanced dressing (W2_DESIGN 5.8 vegetation, 5.2 parked
    /// vehicles, 10.4 budgets) and the W2 stage 1 crown palette. Trees draw at four LODs (the nature kit,
    /// <see cref="Core.Generators.Flora.FloraMesher"/>): the species' detailed model (LOD0, ≤ 1 600 tris) and its
    /// simplified model (LOD1, ≤ 240) near the camera, then the far range split into the family volume (≤ 112) out
    /// to <see cref="TreeVolumeM"/> and the family impostor (≤ 8) out to <see cref="TreeLod2M"/>. Plants (flowers,
    /// shrubs, hedges, pots, ground cover, rocks, straw stacks) draw at LOD0 to <see cref="PlantLod0M"/> and LOD1 to
    /// <see cref="PlantM"/>. Every level is filled nearest first under both its instance cap and its triangle budget;
    /// what overflows steps down a level. The budgets sum to the vegetation slice of W2_DESIGN 10.4 (18 k / 54 k /
    /// 100 k). Engine-free.
    /// </summary>
    public sealed class DressingConfig
    {
        /// <summary>Tree LOD0 radius and cap; LOD1 radius and cap; far radius (volume, then impostor) and the far cap
        /// (volume and impostor instances together).</summary>
        public float TreeLod0M, TreeLod1M, TreeLod2M;

        public int TreeLod0Cap, TreeLod1Cap, TreeLod2Cap;

        /// <summary>Far trees nearer than this draw the family volume (LOD2), beyond it the impostor (LOD3); at most
        /// <see cref="TreeVolumeCap"/> volumes.</summary>
        public float TreeVolumeM;

        public int TreeVolumeCap;

        /// <summary>Triangle budgets per tree level (LOD0, LOD1, volume, impostor).</summary>
        public int TreeLod0Tris, TreeLod1Tris, TreeVolumeTris, TreeImpostorTris;

        /// <summary>Plants: LOD0 to <see cref="PlantLod0M"/>, LOD1 to <see cref="PlantM"/>, at most
        /// <see cref="PlantCap"/> instances and <see cref="PlantTris"/> triangles.</summary>
        public float PlantLod0M, PlantM;

        public int PlantCap, PlantTris;

        /// <summary>Street props are drawn to this radius, at most this many.</summary>
        public float PropM;

        public int PropCap;

        /// <summary>Parked vehicles (W2_DESIGN 5.2): near LOD (moving LOD2) to 20 m, block-out to 80 m, box to 150 m;
        /// caps per level and in all.</summary>
        public float ParkedNearM = 20f, ParkedBlockM = 80f, ParkedBoxM = 150f;

        public int ParkedNearCap, ParkedBlockCap, ParkedCap;

        /// <summary>The vegetation triangle budget: the sum of the tree and plant budgets.</summary>
        public int VegetationTris
        {
            get { return TreeLod0Tris + TreeLod1Tris + TreeVolumeTris + TreeImpostorTris + PlantTris; }
        }

        public static DressingConfig ForTier(int tier)
        {
            switch (tier <= 0 ? 0 : tier >= 2 ? 2 : 1)
            {
                case 0:
                    return new DressingConfig
                    {
                        TreeLod0M = 22f, TreeLod0Cap = 3, TreeLod0Tris = 4600, TreeLod1M = 100f, TreeLod1Cap = 12, TreeLod1Tris = 2700,
                        TreeVolumeM = 160f, TreeVolumeCap = 30, TreeVolumeTris = 2700, TreeLod2M = 750f, TreeLod2Cap = 630, TreeImpostorTris = 3600,
                        PlantLod0M = 10f, PlantM = 18f, PlantCap = 60, PlantTris = 3200,
                        PropM = 120f, PropCap = 300, ParkedNearCap = 2, ParkedBlockCap = 20, ParkedCap = 80,
                    };
                case 1:
                    return new DressingConfig
                    {
                        TreeLod0M = 35f, TreeLod0Cap = 10, TreeLod0Tris = 15000, TreeLod1M = 120f, TreeLod1Cap = 45, TreeLod1Tris = 9500,
                        TreeVolumeM = 220f, TreeVolumeCap = 110, TreeVolumeTris = 9700, TreeLod2M = 1250f, TreeLod2Cap = 1710, TreeImpostorTris = 9600,
                        PlantLod0M = 16f, PlantM = 30f, PlantCap = 160, PlantTris = 9000,
                        PropM = 200f, PropCap = 800, ParkedNearCap = 10, ParkedBlockCap = 60, ParkedCap = 200,
                    };
                default:
                    return new DressingConfig
                    {
                        TreeLod0M = 50f, TreeLod0Cap = 20, TreeLod0Tris = 30000, TreeLod1M = 180f, TreeLod1Cap = 90, TreeLod1Tris = 19000,
                        TreeVolumeM = 300f, TreeVolumeCap = 220, TreeVolumeTris = 19500, TreeLod2M = 1750f, TreeLod2Cap = 3220, TreeImpostorTris = 18000,
                        PlantLod0M = 22f, PlantM = 45f, PlantCap = 300, PlantTris = 13000,
                        PropM = 250f, PropCap = 1500, ParkedNearCap = 10, ParkedBlockCap = 120, ParkedCap = 400,
                    };
            }
        }

        /// <summary>The tree level a distance asks for (0, 1, 2 = far: volume or impostor; 3 = not drawn), before the
        /// caps.</summary>
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

        /// <summary>Crown colour (sRGB hex) of a species in a month in the W2 stage 1 palette (W2_DESIGN 5.8 look and
        /// season): jacaranda violet in March-May, silky oak golden in April-May, bottlebrush and rhododendron red in
        /// their seasons, else foliage. The detail-pass renderer tints the far LODs with
        /// <see cref="Core.Generators.Flora.FloraCatalog.FoliageColour"/> (the same seasons, matched to the kit's
        /// leaf colours); this palette stays for the callers that want one flat colour per species.</summary>
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
