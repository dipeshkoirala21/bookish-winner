using System;

namespace Ghumante.Core.Generators.Ornaments
{
    /// <summary>What stands in the middle of a roundabout or chowk island (docs/research/w2/roundabouts.md §2,
    /// ref_ornaments.md).</summary>
    public enum Centrepiece : byte
    {
        /// <summary>Lawn and flower beds only.</summary>
        Garden = 0,

        /// <summary>A round basin with a tiered bowl and water jets.</summary>
        Fountain = 1,

        /// <summary>A figure on a pedestal and platform (<see cref="StatueSpec"/>).</summary>
        Statue = 2,

        /// <summary>A rider on a horse on a long pedestal (<see cref="StatueSpec"/>).</summary>
        EquestrianStatue = 3,

        /// <summary>Shahid Gate: a pointed stone arch with a lantern holding a bust and two side pavilions with busts.</summary>
        MemorialArch = 4,

        /// <summary>The Maitighar Mandala: a square platform with a coloured mandala inlay, a marigold collar and a
        /// colonnade.</summary>
        Mandala = 5,

        /// <summary>A slim square clock tower with four clock faces and a small dome.</summary>
        ClockTower = 6,

        /// <summary>A tall flag pole with the national flag.</summary>
        FlagPole = 7,

        /// <summary>Only the traffic police post (busy chowks without a real island).</summary>
        PolicePodium = 8,
    }

    /// <summary>The recognisable pose of a statue figure (respectful: pose, attire and finish only).</summary>
    public enum StatuePose : byte
    {
        /// <summary>Standing, arms at the sides.</summary>
        Standing = 0,

        /// <summary>Standing, right hand raised in greeting.</summary>
        Wave = 1,

        /// <summary>Standing, right hand on the chest (a singer's or speaker's bow).</summary>
        HandOnChest = 2,

        /// <summary>Standing, a book held in the left hand against the waist (a poet or scholar).</summary>
        Book = 3,

        /// <summary>Standing, palms joined at the chest.</summary>
        Namaste = 4,

        /// <summary>Riding, reins in the left hand, right hand on the thigh (equestrian statues).</summary>
        Rider = 5,

        /// <summary>Head and shoulders only (busts in memorial niches and lanterns).</summary>
        Bust = 6,

        /// <summary>Standing, the right index finger raised (Prithvi Narayan Shah's iconic pose).</summary>
        PointUp = 7,

        /// <summary>Standing in a long cloak, the left hand at the waist on the sword, the right hand at the chest
        /// (King Tribhuvan at Tripureshwor).</summary>
        CloakHands = 8,

        /// <summary>Standing, the left hand on the sword hilt at the hip, the right arm down (Juddha Shumsher, King
        /// Mahendra).</summary>
        HandOnHilt = 9,

        /// <summary>Standing, a short sceptre held upright in the right hand (King Birendra at Jawalakhel).</summary>
        Sceptre = 10,
    }

    /// <summary>The attire of a statue figure: the silhouette that makes it recognisable from the road.</summary>
    public enum StatueAttire : byte
    {
        /// <summary>Daura suruwal with a waistcoat and the dhaka topi (civilians, poets, singers).</summary>
        DauraTopi = 0,

        /// <summary>A long buttoned coat over trousers with the dhaka topi.</summary>
        CoatTopi = 1,

        /// <summary>Royal dress: a long coat with a sash and the plumed crown (shirpech).</summary>
        RoyalPlumed = 2,

        /// <summary>Rana-era court uniform: a high-collared coat and the tall curved plume helmet.</summary>
        RanaPlumed = 3,

        /// <summary>A coat and trousers without headgear.</summary>
        Coat = 4,
    }

    /// <summary>The finish of a statue (sets its colour and <see cref="Meshing.MaterialChannel"/>).</summary>
    public enum StatueFinish : byte
    {
        Bronze = 0,
        Gilt = 1,
        BlackStone = 2,
        WhiteMarble = 3,

        /// <summary>Green verdigris bronze (Jawalakhel).</summary>
        Verdigris = 4,
    }

    /// <summary>The pedestal (die) under a statue figure.</summary>
    public enum PlinthShape : byte
    {
        /// <summary>A square die with a moulded base and cornice and a front panel.</summary>
        Square = 0,

        /// <summary>A round drum with a moulded base and cornice and a dark band.</summary>
        Round = 1,

        /// <summary>An octagonal die.</summary>
        Octagonal = 2,

        /// <summary>A tall tapered die (an obelisk stump) on a wider square base (King Mahendra at Durbar Marg).</summary>
        Tapered = 3,

        /// <summary>A tall concave-sided stone spire with a coloured collar (King Birendra at Jawalakhel).</summary>
        Spire = 4,
    }

    /// <summary>The stepped platform under the pedestal.</summary>
    public enum PlatformShape : byte
    {
        Square = 0,
        Round = 1,
    }

    /// <summary>Planting of the island around the centrepiece.</summary>
    public enum GardenStyle : byte
    {
        /// <summary>Lawn with a marigold ring and a low clipped hedge.</summary>
        Marigold = 0,

        /// <summary>Lawn with rose bushes in a soil ring and a hedge.</summary>
        Roses = 1,

        /// <summary>Lawn with sweeping mixed beds (marigold, rose, white flowers), round clipped shrubs and palms
        /// (Jawalakhel).</summary>
        Mixed = 2,

        /// <summary>Mostly white flowers (the Maitighar "Garden of Hope" plan) with marigold accents.</summary>
        White = 3,

        /// <summary>Plain lawn with a few shrubs (small or neglected islands).</summary>
        Lawn = 4,

        /// <summary>Stone paving with potted plants, no lawn (New Road west).</summary>
        Paved = 5,
    }

    /// <summary>The paint of the island kerb faces.</summary>
    public enum KerbPaint : byte
    {
        /// <summary>Alternating yellow and black bands (Maitighar and older islands).</summary>
        YellowBlack = 0,

        /// <summary>Alternating black and white bands (Ring Road and newer islands, IRC:35).</summary>
        BlackWhite = 1,

        /// <summary>Plain grey stone.</summary>
        Stone = 2,
    }

    /// <summary>The railing just inside the island kerb.</summary>
    public enum RailingStyle : byte
    {
        None = 0,

        /// <summary>A low white ornamental railing with arched bays (Jawalakhel).</summary>
        WhiteArches = 1,

        /// <summary>A black iron railing on a low cream plinth wall (Shahid Gate, Durbar Marg).</summary>
        BlackIron = 2,

        /// <summary>Teal-green square posts with pyramid caps and two rails (Maitighar).</summary>
        TealPosts = 3,
    }

    /// <summary>The traffic police post on a police chowk.</summary>
    public enum PoliceStyle : byte
    {
        None = 0,

        /// <summary>A round white drum with a blue band, a rail and a post carrying a box sign under a small roof
        /// with a solar panel (Koteshwor).</summary>
        Drum = 1,

        /// <summary>The low white podium under a big red and white umbrella.</summary>
        Umbrella = 2,
    }

    /// <summary>Plan shape of an island.</summary>
    public enum IslandShape : byte
    {
        /// <summary>A circle of <see cref="RoundaboutSite.RadiusM"/>.</summary>
        Round = 0,

        /// <summary>A stadium (rounded rectangle) of half length <see cref="RoundaboutSite.HalfLengthM"/> across the
        /// facing direction and half width <see cref="RoundaboutSite.RadiusM"/> along it (Shahid Gate).</summary>
        Stadium = 1,
    }

    /// <summary>A statue: who it shows (an English label for the docs and the info card, never Devanagari made by
    /// code), how it stands and what it wears, and its pedestal and platform. Figures are respectful generic
    /// likenesses: pose, attire and finish only, never a portrait or a caricature.</summary>
    public struct StatueSpec
    {
        public string Subject;
        public StatuePose Pose;
        public StatueAttire Attire;
        public StatueFinish Finish;

        /// <summary>Figure height (sole to crown, without the plume) in metres.</summary>
        public float FigureM;

        /// <summary>A long cloak from the shoulders to the ground.</summary>
        public bool Cloak;

        /// <summary>Glasses (King Mahendra's dark glasses, King Birendra's, Narayan Gopal's round ones).</summary>
        public bool Glasses;

        /// <summary>Marigold garlands round the neck (offerings; Tripureshwor).</summary>
        public bool Garland;

        /// <summary>Height of the crown's plume for plumed attire (metres on a 1.8 m figure; 0 = the default tall
        /// curved plume of 0.42 m, Rana helmets 0.5 m).</summary>
        public float PlumeM;

        public PlinthShape Plinth;

        /// <summary>Height of the pedestal (die, base and cornice) in metres.</summary>
        public float PlinthM;

        /// <summary>Side (square, octagonal, tapered base) or diameter (round, spire base) of the pedestal in metres.</summary>
        public float PlinthW;

        /// <summary>Pedestal stone colour (0xRRGGBBAA); 0 = the default cream marble.</summary>
        public uint PlinthColour;

        public PlatformShape Platform;

        /// <summary>Steps of the platform under the pedestal (0-5).</summary>
        public byte Steps;

        /// <summary>Side (square) or diameter (round) of the lowest step in metres.</summary>
        public float PlatformW;

        /// <summary>Platform stone colour (0xRRGGBBAA); 0 = the default grey stone.</summary>
        public uint PlatformColour;

        /// <summary>Big terracotta pots at the platform corners (Tripureshwor) or potted plants on the top step.</summary>
        public bool Pots;

        /// <summary>A round crest medallion on the front of the cornice (Juddha Shumsher).</summary>
        public bool Medallion;
    }

    /// <summary>
    /// Everything the decorator needs to dress one island: the centrepiece, the statue, the planting and the street
    /// furniture. Real roundabouts take theirs from <see cref="RoundaboutCatalog"/>; the rest get a deterministic
    /// generic design (<see cref="RoundaboutCatalog.Generic"/>) that never invents a statue or a shrine (W2_DESIGN 4.7, O2).
    /// </summary>
    public struct RoundaboutDesign
    {
        /// <summary>Catalog id (<c>"maitighar"</c>...) or <c>"generic"</c>.</summary>
        public string Id;

        public string NameEn;
        public Centrepiece Centre;
        public StatueSpec Statue;
        public GardenStyle Garden;
        public KerbPaint Kerb;

        /// <summary>A fountain basin around the centrepiece (Jawalakhel) or as the centrepiece.</summary>
        public bool Fountain;

        /// <summary>Height of a national flag pole on the island in metres (0 = none).</summary>
        public float FlagPoleM;

        public RailingStyle Railing;

        /// <summary>Solar street lights around the island (0 = none).</summary>
        public byte LampPosts;

        /// <summary>A blue direction sign (arrows only, no text) at the kerb facing the main arm.</summary>
        public bool Signboard;

        public PoliceStyle Police;

        /// <summary>Trees in a ring on big islands (0 = none).</summary>
        public byte Trees;

        /// <summary>Radial stone paths from the kerb to the centrepiece (0 = none).</summary>
        public byte Paths;

        /// <summary>Bearing (degrees clockwise from north) the centrepiece faces; NaN = face the main arm.</summary>
        public float FacingDeg;

        /// <summary>Variation seed (FNV-1a of the junction node).</summary>
        public uint Seed;

        /// <summary>True for a curated real design (a hero: higher triangle budget).</summary>
        public bool IsHero
        {
            get { return Id != null && Id != RoundaboutCatalog.GenericId; }
        }
    }

    /// <summary>Where an island is: centre in tile-local metres, its plan, the mountable apron inside its edge, the
    /// height of the surrounding road surface above the terrain, and the bearing of the main approach arm.</summary>
    public struct RoundaboutSite
    {
        public double X, Z;

        /// <summary>Island radius (round) or half width along the facing direction (stadium), in metres.</summary>
        public float RadiusM;

        /// <summary>Half length across the facing direction (stadium only).</summary>
        public float HalfLengthM;

        public IslandShape Shape;

        /// <summary>Mountable cobble apron inside the island edge (buses on rings), 0 for none.</summary>
        public float ApronM;

        /// <summary>Road surface height above the terrain round the island (the kerb rises from it).
        /// <see cref="RoundaboutDecorator.DefaultRoadLiftM"/> matches the road mesher's primary-road lift.</summary>
        public float RoadLiftM;

        /// <summary>Bearing (degrees clockwise from north) of the main approach arm; the statue faces it.</summary>
        public float MainArmDeg;

        /// <summary>Tile origin (game metres) for the height sampler, which takes absolute game coordinates.</summary>
        public double TileX0, TileZ0;
    }

    /// <summary>What the decorator built, for checks and presenters.</summary>
    public sealed class OrnamentStats
    {
        public int Triangles, Vertices, Flowers, Shrubs, Trees, LampPosts, RailingPosts, Figures, Busts, Steps, Jets;

        /// <summary>Triangles per part: kerb, apron and lawn; the centrepiece; beds, shrubs and trees; railing,
        /// lamps, flag pole, police post and sign.</summary>
        public int BaseTriangles, CentreTriangles, GardenTriangles, FurnitureTriangles;

        /// <summary>Highest point above the island top (finial, flag mast or statue crown).</summary>
        public float TopM;

        /// <summary>Island top height (absolute) at the centre.</summary>
        public float IslandTopY;

        public Centrepiece Centre;
        public string DesignId;

        public void Clear()
        {
            Triangles = Vertices = Flowers = Shrubs = Trees = LampPosts = RailingPosts = Figures = Busts = Steps = Jets = 0;
            BaseTriangles = CentreTriangles = GardenTriangles = FurnitureTriangles = 0;
            TopM = IslandTopY = 0f;
            Centre = Centrepiece.Garden;
            DesignId = null;
        }
    }

    /// <summary>Deterministic seeds for ornaments: FNV-1a 32 over the little-endian bytes of a 64-bit key and a salt
    /// (no allocation; the constants of <see cref="Data.Hashes"/>).</summary>
    public static class OrnamentSeed
    {
        public static uint Of(long key, uint salt)
        {
            uint h = Data.Hashes.Fnv32Offset;
            ulong v = unchecked((ulong)key);
            for (int i = 0; i < 8; i++) h = unchecked((h ^ (byte)(v >> (8 * i))) * Data.Hashes.Fnv32Prime);
            for (int i = 0; i < 4; i++) h = unchecked((h ^ (byte)(salt >> (8 * i))) * Data.Hashes.Fnv32Prime);
            return h;
        }

        /// <summary>A value in [0, 1) from a seed and a stream index (cheap integer hash).</summary>
        public static float Unit(uint seed, int i)
        {
            uint h = unchecked(seed ^ (uint)i * 0x9E3779B9u);
            h ^= h >> 16;
            h = unchecked(h * 0x7FEB352Du);
            h ^= h >> 15;
            h = unchecked(h * 0x846CA68Bu);
            h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }

        /// <summary><paramref name="a"/> + (<paramref name="b"/> - <paramref name="a"/>) × Unit.</summary>
        public static float Range(uint seed, int i, float a, float b)
        {
            return a + (b - a) * Unit(seed, i);
        }

        /// <summary>An index in [0, n) from a seed and a stream index.</summary>
        public static int Pick(uint seed, int i, int n)
        {
            return Math.Min(n - 1, (int)(Unit(seed, i) * n));
        }
    }
}
