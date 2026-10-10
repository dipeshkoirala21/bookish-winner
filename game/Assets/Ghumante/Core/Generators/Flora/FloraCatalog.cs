using Ghumante.Core.Generators.Placement;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>What a plant kind is, for LOD and material choices.</summary>
    public enum FloraClass : byte
    {
        /// <summary>A tree: two species LODs, then the family volume and the far impostor; long draw distances.</summary>
        Tree = 0,

        /// <summary>A small plant (flowers, shrubs, ground cover): two LODs, short draw distances, sways in the wind.</summary>
        Plant = 1,

        /// <summary>Rocks and other rigid things (straw stacks, pots, the tulsi pedestal): two LODs, no wind.</summary>
        Rigid = 2,
    }

    /// <summary>Static facts about one plant kind (<see cref="FloraCatalog.Info"/>).</summary>
    public readonly struct FloraInfo
    {
        /// <summary>Size the mesh is modelled at (metres): height and largest horizontal extent (the unit kit mesh is
        /// this divided by (width, height, width)).</summary>
        public readonly float ModelHeightM, ModelWidthM;

        /// <summary>Placement height range (metres) when the data has none.</summary>
        public readonly float MinHeightM, MaxHeightM;

        /// <summary>Tree, swaying plant or rigid kind (material and LOD rules).</summary>
        public readonly FloraClass Class;

        /// <summary>The shape family whose far LODs the kind shares.</summary>
        public readonly TreeShape Family;

        public FloraInfo(float modelH, float modelW, float minH, float maxH, FloraClass cls, TreeShape family)
        {
            ModelHeightM = modelH;
            ModelWidthM = modelW;
            MinHeightM = minH;
            MaxHeightM = maxH;
            Class = cls;
            Family = family;
        }

        /// <summary>Width over height of the model (the crown ratio placement keeps).</summary>
        public float Aspect
        {
            get { return ModelWidthM / ModelHeightM; }
        }
    }

    /// <summary>
    /// The nature kit's catalogue (W2_DESIGN 5.8; research street_life.md 9-13; docs/research/w2/ref_nature.md):
    /// model size, placement size range, class and far-LOD family of every <see cref="TreeSpecies"/>, the leaf
    /// colour by month, bark and bloom colours, how much of a kind is in bloom in a month (jacaranda violet from late
    /// March, at its peak late April and May, rhododendron red February to April, bottlebrush red March to May with an
    /// autumn flush, silky oak gold April and May, Schima white May and June, poinsettia red bracts November to February, marigold peaking at
    /// Dashain and Tihar) and the far-LOD crown colour that mixes the two. Hex values are the design's [E] palette.
    /// Engine-free.
    /// </summary>
    public static class FloraCatalog
    {
        /// <summary>Number of <see cref="TreeSpecies"/> values.</summary>
        public const int Count = 33;

        private static readonly FloraInfo[] Infos =
        {
            new FloraInfo(14f, 11f, 8f, 18f, FloraClass.Tree, TreeShape.Round), // Broadleaf
            new FloraInfo(18f, 18f, 15f, 25f, FloraClass.Tree, TreeShape.Umbrella), // Pipal
            new FloraInfo(16f, 24f, 15f, 20f, FloraClass.Tree, TreeShape.Umbrella), // Bar
            new FloraInfo(12f, 12f, 8f, 15f, FloraClass.Tree, TreeShape.Umbrella), // Jacaranda
            new FloraInfo(22f, 10.5f, 18f, 30f, FloraClass.Tree, TreeShape.Column), // SilkyOak (a teardrop about half as wide as tall)
            new FloraInfo(6f, 5f, 4f, 8f, FloraClass.Tree, TreeShape.Round), // Bottlebrush
            new FloraInfo(15f, 13f, 10f, 20f, FloraClass.Tree, TreeShape.Round), // Camphor
            new FloraInfo(28f, 11f, 15f, 35f, FloraClass.Tree, TreeShape.Column), // Eucalyptus
            new FloraInfo(12f, 7f, 8f, 15f, FloraClass.Tree, TreeShape.Fountain), // Bamboo
            new FloraInfo(9f, 6f, 6f, 12f, FloraClass.Tree, TreeShape.Fountain), // Palm
            new FloraInfo(15f, 11f, 10f, 22f, FloraClass.Tree, TreeShape.Round), // Schima
            new FloraInfo(15f, 12f, 10f, 22f, FloraClass.Tree, TreeShape.Round), // Castanopsis
            new FloraInfo(16f, 10f, 10f, 22f, FloraClass.Tree, TreeShape.Column), // Alnus
            new FloraInfo(20f, 12f, 12f, 25f, FloraClass.Tree, TreeShape.Cone), // ChirPine (mature: a broad rounded open crown)
            new FloraInfo(15f, 13f, 10f, 22f, FloraClass.Tree, TreeShape.Round), // Oak
            new FloraInfo(8f, 7f, 6f, 12f, FloraClass.Tree, TreeShape.Round), // Rhododendron
            new FloraInfo(14f, 12f, 10f, 20f, FloraClass.Tree, TreeShape.Round), // BrownOak
            new FloraInfo(25f, 12f, 18f, 32f, FloraClass.Tree, TreeShape.Round), // Sal
            new FloraInfo(4.5f, 5f, 3f, 5f, FloraClass.Tree, TreeShape.Fountain), // Banana
            new FloraInfo(1.3f, 1.8f, 0.8f, 2.2f, FloraClass.Plant, TreeShape.Low), // Shrub
            new FloraInfo(1.1f, 3f, 1.0f, 1.25f, FloraClass.Plant, TreeShape.Low), // Hedge (3 m segment, 0.9 m deep)
            new FloraInfo(0.55f, 2.6f, 0.45f, 0.65f, FloraClass.Plant, TreeShape.Low), // MarigoldBed (2.6 × 1.3 m bed)
            new FloraInfo(1.1f, 1.2f, 0.7f, 1.5f, FloraClass.Plant, TreeShape.Low), // Rose
            new FloraInfo(2.2f, 1.8f, 1.5f, 3.5f, FloraClass.Plant, TreeShape.Low), // Poinsettia
            new FloraInfo(2.6f, 3.2f, 2f, 3.4f, FloraClass.Plant, TreeShape.Low), // Bougainvillea
            new FloraInfo(2f, 1.2f, 1.6f, 2.6f, FloraClass.Plant, TreeShape.Low), // Sunflower (clump of 3)
            new FloraInfo(0.8f, 0.7f, 0.6f, 1.0f, FloraClass.Rigid, TreeShape.Low), // PottedPlant (group of three)
            new FloraInfo(1.45f, 0.75f, 1.3f, 1.7f, FloraClass.Rigid, TreeShape.Low), // TulsiMath
            new FloraInfo(0.45f, 0.5f, 0.3f, 0.7f, FloraClass.Plant, TreeShape.Low), // GrassTuft
            new FloraInfo(0.48f, 1.2f, 0.35f, 0.7f, FloraClass.Plant, TreeShape.Low), // Fern
            new FloraInfo(0.5f, 0.8f, 0.25f, 0.8f, FloraClass.Rigid, TreeShape.Low), // Rock
            new FloraInfo(1.6f, 2.4f, 1f, 3f, FloraClass.Rigid, TreeShape.Low), // Boulder
            new FloraInfo(3f, 2.4f, 2.4f, 3.6f, FloraClass.Rigid, TreeShape.Low), // StrawStack (kunyu)
        };

        /// <summary>Facts about a kind (out-of-range values read as <see cref="TreeSpecies.Broadleaf"/>).</summary>
        public static FloraInfo Info(TreeSpecies s)
        {
            int i = (int)s;
            return i < Infos.Length ? Infos[i] : Infos[0];
        }

        /// <summary>True for trees (far LODs, tree caps); false for plants and rigid kinds.</summary>
        public static bool IsTree(TreeSpecies s)
        {
            return Info(s).Class == FloraClass.Tree;
        }

        /// <summary>True for kinds drawn with the vertex-wind material (trees and plants; not rocks, pots or stacks).</summary>
        public static bool Sways(TreeSpecies s)
        {
            return Info(s).Class != FloraClass.Rigid;
        }

        /// <summary>
        /// Leaf colour (sRGB 0xRRGGBB) of a kind in a month (1-12), without its bloom: the clump colour of the near
        /// LODs. Pipal, camphor, Schima and sal flush coppery or bronze in spring on top of this (the recipes mix it
        /// in); grass greens in the monsoon and dries to straw in winter.
        /// </summary>
        public static uint LeafColour(TreeSpecies s, int month)
        {
            switch (s)
            {
                case TreeSpecies.Pipal: return 0x4E8A3Au;
                case TreeSpecies.Bar: return 0x2F6B2Fu;
                case TreeSpecies.Jacaranda: return 0x5E8F45u;
                case TreeSpecies.SilkyOak: return 0x557A3Au;
                case TreeSpecies.Bottlebrush: return 0x4F7D3Au;
                case TreeSpecies.Camphor: return 0x3F7F3Au;
                case TreeSpecies.Eucalyptus: return 0x7FA08Au;
                case TreeSpecies.Bamboo: return 0x6FA03Au;
                case TreeSpecies.Palm: return 0x5C9A3Au;
                case TreeSpecies.Schima: return 0x4A7F36u;
                case TreeSpecies.Castanopsis: return 0x527F36u;
                case TreeSpecies.Alnus: return 0x4C8B3Eu;
                case TreeSpecies.ChirPine: return 0x4F7A3Au;
                case TreeSpecies.Oak: return 0x3B6B34u;
                case TreeSpecies.Rhododendron: return 0x3B6B34u;
                case TreeSpecies.BrownOak: return 0x6B7A4Au;
                case TreeSpecies.Sal: return 0x4F8A3Au;
                case TreeSpecies.Banana: return 0x7DBA3Au;
                case TreeSpecies.Shrub: return 0x4F8A3Au;
                case TreeSpecies.Hedge: return 0x3F7F3Au;
                case TreeSpecies.MarigoldBed: return 0x3E7A32u;
                case TreeSpecies.Rose: return 0x3F6E35u;
                case TreeSpecies.Poinsettia: return 0x3E7A32u;
                case TreeSpecies.Bougainvillea: return 0x4A8A3Au;
                case TreeSpecies.Sunflower: return 0x5E9A3Au;
                case TreeSpecies.PottedPlant: return 0x4E8A3Au;
                case TreeSpecies.TulsiMath: return 0x4C7A3Au;
                case TreeSpecies.GrassTuft:
                    if (month >= 6 && month <= 9) return 0x7FB04Au;
                    if (month == 10 || month == 11) return 0x9CB04Au;
                    if (month == 12 || month <= 2) return 0xB8A86Au;
                    return 0xA8B05Au;
                case TreeSpecies.Fern: return 0x4E8A3Au;
                case TreeSpecies.Rock:
                case TreeSpecies.Boulder: return 0xA39E96u;
                case TreeSpecies.StrawStack: return 0xD9B65Au;
                default: return 0x4C8B3Eu;
            }
        }

        /// <summary>
        /// Crown colour (sRGB 0xRRGGBB) of a kind in a month as it reads from afar: the leaf colour mixed with the
        /// bloom by how much of the crown the bloom covers (W2_DESIGN 5.8: jacaranda violet March-May, silky oak
        /// golden April-May, bottlebrush and rhododendron red in their seasons, pipal copper flush March-April). The
        /// far LODs (family volume and impostor) are tinted with it.
        /// </summary>
        public static uint FoliageColour(TreeSpecies s, int month)
        {
            uint leaf = LeafColour(s, month);
            if ((s == TreeSpecies.Pipal || s == TreeSpecies.Camphor) && (month == 3 || month == 4)) leaf = Mix(leaf, 0xC27C5Eu, s == TreeSpecies.Pipal ? 0.45f : 0.2f);
            float b = Bloom(s, month) * BloomCover(s);
            uint bloom = BloomColour(s);
            if (s == TreeSpecies.Jacaranda) bloom = 0x8E6CC8u;
            return b > 0f && bloom != 0 ? Mix(leaf, bloom, b) : leaf;
        }

        /// <summary>How much of the crown a full bloom covers (as seen from afar).</summary>
        private static float BloomCover(TreeSpecies s)
        {
            switch (s)
            {
                case TreeSpecies.Jacaranda: return 0.9f;
                case TreeSpecies.Rhododendron: return 0.55f;
                case TreeSpecies.Bottlebrush: return 0.45f;
                case TreeSpecies.SilkyOak: return 0.5f;
                case TreeSpecies.Schima: return 0.25f;
                case TreeSpecies.Castanopsis: return 0.2f;
                case TreeSpecies.Poinsettia: return 0.5f;
                case TreeSpecies.Bougainvillea: return 0.7f;
                case TreeSpecies.MarigoldBed: return 0.6f;
                default: return 0.3f;
            }
        }

        private static uint Mix(uint a, uint b, float t)
        {
            int ra = (int)(a >> 16 & 0xFF), ga = (int)(a >> 8 & 0xFF), ba = (int)(a & 0xFF);
            int rb = (int)(b >> 16 & 0xFF), gb = (int)(b >> 8 & 0xFF), bb = (int)(b & 0xFF);
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            int r = (int)(ra + (rb - ra) * t + 0.5f), g = (int)(ga + (gb - ga) * t + 0.5f), bl = (int)(ba + (bb - ba) * t + 0.5f);
            return (uint)(r << 16 | g << 8 | bl);
        }

        /// <summary>Bark colour (sRGB) of a tree or stem (W2_DESIGN 5.8 trunk colours; bamboo culm and banana
        /// pseudostem greens).</summary>
        public static uint BarkColour(TreeSpecies s)
        {
            switch (s)
            {
                case TreeSpecies.Pipal: return 0x9A948Au;
                case TreeSpecies.Bar: return 0x8A8274u;
                case TreeSpecies.Jacaranda: return 0x5A4A3Eu;
                case TreeSpecies.SilkyOak: return 0x5E4A3Au;
                case TreeSpecies.Bottlebrush: return 0x6A5040u;
                case TreeSpecies.Camphor: return 0x5E4E40u;
                case TreeSpecies.Eucalyptus: return 0xD9D2C3u;
                case TreeSpecies.Bamboo: return 0x7DA83Eu;
                case TreeSpecies.Palm: return 0x8A7A64u;
                case TreeSpecies.Schima: return 0x4E4038u;
                case TreeSpecies.Castanopsis: return 0x6E665Au;
                case TreeSpecies.ChirPine: return 0x7A4A2Eu;
                case TreeSpecies.Rhododendron: return 0x6A4A3Au;
                case TreeSpecies.BrownOak: return 0x6B7A4Au;
                case TreeSpecies.Sal: return 0x5A4636u;
                case TreeSpecies.Alnus: return 0x8C8A80u;
                case TreeSpecies.Banana: return 0x8DA04Au;
                default: return 0x6B4A2Eu;
            }
        }

        /// <summary>Bloom colour (sRGB) of a kind (0 when it has no showy bloom).</summary>
        public static uint BloomColour(TreeSpecies s)
        {
            switch (s)
            {
                case TreeSpecies.Jacaranda: return 0xA58AD8u;
                case TreeSpecies.SilkyOak: return 0xF2A33Au;
                case TreeSpecies.Bottlebrush: return 0xD7263Du;
                case TreeSpecies.Rhododendron: return 0xC8102Eu;
                case TreeSpecies.Schima: return 0xF4F1E6u;
                case TreeSpecies.Castanopsis: return 0xD9C46Au;
                case TreeSpecies.MarigoldBed: return 0xF5821Fu;
                case TreeSpecies.Rose: return 0xD81B3Cu;
                case TreeSpecies.Poinsettia: return 0xD11F2Au;
                case TreeSpecies.Bougainvillea: return 0xC2185Bu;
                case TreeSpecies.Sunflower: return 0xF6C21Bu;
                case TreeSpecies.PottedPlant: return 0xE8443Au;
                default: return 0u;
            }
        }

        /// <summary>
        /// How much of a kind is in bloom in a month (0..1; month 1-12). Out-of-season months keep a little colour
        /// where the plant really has a second flush (bottlebrush in autumn [S]; a light autumn jacaranda sprinkle and
        /// garden marigolds outside Tihar are [E]).
        /// </summary>
        public static float Bloom(TreeSpecies s, int month)
        {
            switch (s)
            {
                // Kathmandu's jacarandas (Tundikhel, Ratna Park, Durbar Marg, the Ring Road) peak from late April
                // into May: first flowers in March, the last in June, a light autumn sprinkle [E].
                case TreeSpecies.Jacaranda: return month == 4 || month == 5 ? 1f : month == 3 ? 0.45f : month == 6 ? 0.2f : month == 10 || month == 11 ? 0.12f : 0f;
                case TreeSpecies.SilkyOak: return month == 4 || month == 5 ? 1f : 0f;
                case TreeSpecies.Bottlebrush: return month >= 3 && month <= 5 ? 1f : month == 10 || month == 11 ? 0.35f : 0f;
                case TreeSpecies.Rhododendron: return month >= 2 && month <= 4 ? 1f : month == 1 || month == 5 ? 0.25f : 0f;
                case TreeSpecies.Schima: return month == 5 || month == 6 ? 1f : 0f;
                case TreeSpecies.Castanopsis: return month == 3 || month == 4 ? 0.8f : 0f;
                case TreeSpecies.MarigoldBed: return month == 10 || month == 11 ? 1f : 0.7f;
                case TreeSpecies.Rose: return month >= 3 && month <= 5 || month >= 10 ? 1f : 0.5f;
                case TreeSpecies.Poinsettia: return month >= 11 || month <= 2 ? 1f : month == 10 ? 0.4f : 0f;
                case TreeSpecies.Bougainvillea: return month >= 3 && month <= 6 || month == 10 || month == 11 ? 1f : 0.5f;
                case TreeSpecies.Sunflower: return month >= 6 && month <= 10 ? 1f : 0f;
                case TreeSpecies.PottedPlant: return 0.8f;
                default: return 0f;
            }
        }

        /// <summary>
        /// True for the plants big or bright enough to read from a moving bike well past the small-plant radius
        /// (<see cref="FloraBudget.PlantFarM"/>, drawn at LOD1): straw stacks, boulders, hedge rows, marigold beds,
        /// bougainvillea, poinsettia, sunflowers and the tulsi math. Grass tufts, ferns, rocks, shrubs, roses and pots
        /// stay on the short radius.
        /// </summary>
        public static bool DrawsFar(TreeSpecies s)
        {
            switch (s)
            {
                case TreeSpecies.StrawStack:
                case TreeSpecies.Boulder:
                case TreeSpecies.Hedge:
                case TreeSpecies.MarigoldBed:
                case TreeSpecies.Bougainvillea:
                case TreeSpecies.Poinsettia:
                case TreeSpecies.Sunflower:
                case TreeSpecies.TulsiMath:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>False when a seasonal kind is not out in that month: sunflowers stand June to November, straw
        /// stacks from the October paddy harvest to March (S 12).</summary>
        public static bool InSeason(TreeSpecies s, int month)
        {
            switch (s)
            {
                case TreeSpecies.Sunflower: return month >= 6 && month <= 11;
                case TreeSpecies.StrawStack: return month >= 10 || month <= 3;
                default: return true;
            }
        }
    }
}
