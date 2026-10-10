namespace Ghumante.Core.Generators.Placement
{
    /// <summary>
    /// Every instanced plant kind of the nature kit (W2_DESIGN 5.8; S 9-12): the trees first (values 0-16 are the
    /// W2 stage 1 set and keep their numbers), then the detail-pass additions: sal, banana, shrubs and hedges, the
    /// garden flowers (marigold beds, roses, poinsettia, bougainvillea, sunflowers, potted plants, tulsi math), ground
    /// cover (grass tufts, ferns), rocks and boulders, and the harvest straw stacks. Append-only.
    /// <see cref="Flora.FloraCatalog"/> describes each kind; <see cref="Flora.FloraMesher"/> builds its meshes.
    /// </summary>
    public enum TreeSpecies : byte
    {
        Broadleaf = 0,
        Pipal = 1,
        Bar = 2,
        Jacaranda = 3,
        SilkyOak = 4,
        Bottlebrush = 5,
        Camphor = 6,
        Eucalyptus = 7,
        Bamboo = 8,
        Palm = 9,
        Schima = 10,
        Castanopsis = 11,
        Alnus = 12,
        ChirPine = 13,
        Oak = 14,
        Rhododendron = 15,
        BrownOak = 16,

        // Detail pass (append only).
        Sal = 17,
        Banana = 18,
        Shrub = 19,
        Hedge = 20,
        MarigoldBed = 21,
        Rose = 22,
        Poinsettia = 23,
        Bougainvillea = 24,
        Sunflower = 25,
        PottedPlant = 26,
        TulsiMath = 27,
        GrassTuft = 28,
        Fern = 29,
        Rock = 30,
        Boulder = 31,
        StrawStack = 32,
    }

    /// <summary>
    /// Shape family of a plant: the silhouette its far LODs (LOD2 volume and LOD3 impostor) share, so far trees of
    /// many species draw in a handful of instanced calls. <see cref="Low"/> kinds (flowers, ground cover, rocks) have
    /// no far LOD. Values 0-2 are the W2 stage 1 families. Append-only.
    /// </summary>
    public enum TreeShape : byte
    {
        Round = 0,
        Cone = 1,
        Umbrella = 2,

        /// <summary>Tall and narrow (silky oak, eucalyptus).</summary>
        Column = 3,

        /// <summary>Arching from the base (bamboo clumps, banana, palm).</summary>
        Fountain = 4,

        /// <summary>Small plants and rocks: near LODs only.</summary>
        Low = 5,
    }

    /// <summary>Where a plant came from. Values 0-2 are the W2 stage 1 origins. Append-only.</summary>
    public enum TreeOrigin : byte
    {
        /// <summary>An OSM tree at its real position (PROP).</summary>
        Osm = 0,

        /// <summary>A generated avenue tree along an URBAN arterial (or an OSM tree row).</summary>
        Avenue = 1,

        /// <summary>Generated forest scatter inside real forest, with its understorey.</summary>
        Forest = 2,

        /// <summary>A house garden: pots and tulsi by the door, poinsettia, banana and bamboo by village houses,
        /// bougainvillea on compound walls.</summary>
        Garden = 3,

        /// <summary>Park planting: flower beds, hedges along the edges, ornamental trees and shrubs.</summary>
        Park = 4,

        /// <summary>Wild ground cover: grass tufts and rocks on hills and field edges, boulders on steep slopes and river
        /// banks.</summary>
        Wild = 5,

        /// <summary>Field furniture: harvest straw stacks.</summary>
        Field = 6,
    }

    /// <summary>One placed plant (tile-local metres, absolute Y). Trees of one forest clump share a
    /// <see cref="ClumpId"/> (> 0). Instances draw at scale (<see cref="CrownM"/>, <see cref="HeightM"/>,
    /// <see cref="CrownM"/>) of the unit kit mesh, turned by <see cref="YawDeg"/> clockwise from north.</summary>
    public struct TreeInstance
    {
        public float X, Y, Z;

        /// <summary>Height and largest horizontal extent (crown diameter, bed or hedge length) in metres.</summary>
        public float HeightM, CrownM, YawDeg;

        public TreeSpecies Species;
        public TreeShape Shape;
        public byte SizeClass;
        public TreeOrigin Origin;
        public bool Chautari;
        public int ClumpId;
        public ulong OsmRef;
    }
}
