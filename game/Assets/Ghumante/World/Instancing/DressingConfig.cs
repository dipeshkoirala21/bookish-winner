using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// Per-tier radii, caps and triangle budgets of the instanced dressing (W2_DESIGN 5.8 vegetation, 5.2 parked
    /// vehicles, 10.4 budgets) and the W2 stage 1 crown palette. The vegetation part is the nature kit's
    /// <see cref="FloraBudget"/> (<see cref="Flora"/>, shared with the preview scenes so a preview shows what the game
    /// draws): trees draw at four LODs (<see cref="FloraMesher"/>), the species' detailed model (LOD0, ≤ 1 600 tris)
    /// and its simplified model (LOD1, ≤ 240) near the camera, then the family volume (≤ 112) out to
    /// <see cref="TreeVolumeM"/> and the family impostor (a rounded crown on a trunk, ≤ 24) out to
    /// <see cref="TreeLod2M"/>. Plants draw at LOD0 to <see cref="PlantLod0M"/> and LOD1 to <see cref="PlantM"/>, the
    /// large kinds (<see cref="FloraCatalog.DrawsFar"/>: straw stacks, boulders, hedges, flower beds...) at LOD1 on to
    /// <see cref="PlantFarM"/>. Every level is filled nearest first under both its instance cap and its triangle
    /// budget (<see cref="FloraLodPlan"/>); what overflows steps down a level. The budgets sum to the vegetation slice
    /// of W2_DESIGN 10.4 (18 k / 54 k / 100 k). The vegetation properties below read <see cref="Flora"/>. Engine-free.
    /// </summary>
    public sealed class DressingConfig
    {
        /// <summary>The vegetation budget of the tier (radii, caps, triangles, view culling).</summary>
        public FloraBudget Flora = FloraBudget.ForTier(1);

        /// <summary>Tree LOD0 and LOD1 radii; the far radius (volume, then impostor).</summary>
        public float TreeLod0M { get { return Flora.TreeLod0M; } }

        public float TreeLod1M { get { return Flora.TreeLod1M; } }

        public float TreeLod2M { get { return Flora.TreeFarM; } }

        /// <summary>Instance caps of LOD0, LOD1 and the far levels (volumes and impostors together).</summary>
        public int TreeLod0Cap { get { return Flora.TreeLod0Cap; } }

        public int TreeLod1Cap { get { return Flora.TreeLod1Cap; } }

        public int TreeLod2Cap { get { return Flora.TreeFarCap; } }

        /// <summary>Far trees nearer than this draw the family volume (LOD2), beyond it the impostor (LOD3); at most
        /// <see cref="TreeVolumeCap"/> volumes.</summary>
        public float TreeVolumeM { get { return Flora.TreeVolumeM; } }

        public int TreeVolumeCap { get { return Flora.TreeVolumeCap; } }

        /// <summary>Triangle budgets per tree level (LOD0, LOD1, volume, impostor).</summary>
        public int TreeLod0Tris { get { return Flora.TreeLod0Tris; } }

        public int TreeLod1Tris { get { return Flora.TreeLod1Tris; } }

        public int TreeVolumeTris { get { return Flora.TreeVolumeTris; } }

        public int TreeImpostorTris { get { return Flora.TreeImpostorTris; } }

        /// <summary>Plants: LOD0 to <see cref="PlantLod0M"/>, LOD1 to <see cref="PlantM"/> (the large kinds to
        /// <see cref="PlantFarM"/>), at most <see cref="PlantCap"/> instances and <see cref="PlantTris"/> triangles.</summary>
        public float PlantLod0M { get { return Flora.PlantLod0M; } }

        public float PlantM { get { return Flora.PlantM; } }

        public float PlantFarM { get { return Flora.PlantFarM; } }

        public int PlantCap { get { return Flora.PlantCap; } }

        public int PlantTris { get { return Flora.PlantTris; } }

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
            get { return Flora.VegetationTris; }
        }

        public static DressingConfig ForTier(int tier)
        {
            FloraBudget flora = FloraBudget.ForTier(tier);
            switch (tier <= 0 ? 0 : tier >= 2 ? 2 : 1)
            {
                case 0:
                    return new DressingConfig { Flora = flora, PropM = 120f, PropCap = 300, ParkedNearCap = 2, ParkedBlockCap = 20, ParkedCap = 80 };
                case 1:
                    return new DressingConfig { Flora = flora, PropM = 200f, PropCap = 800, ParkedNearCap = 10, ParkedBlockCap = 60, ParkedCap = 200 };
                default:
                    return new DressingConfig { Flora = flora, PropM = 250f, PropCap = 1500, ParkedNearCap = 10, ParkedBlockCap = 120, ParkedCap = 400 };
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
        /// <see cref="FloraCatalog.FoliageColour"/> (the same seasons, matched to the kit's
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
