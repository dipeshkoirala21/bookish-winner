using System;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// Every animal and bird the fauna generators build (W2_DESIGN 5.5-5.6, street_life 7-8). Append only: the value
    /// is part of mesh cache keys and deterministic spawn hashes.
    /// </summary>
    public enum FaunaSpecies : byte
    {
        /// <summary>Street cow: humped zebu, 1.2 m at the withers, dewlap, short upturned horns, drooping ears.</summary>
        Cow = 0,

        /// <summary>Calf (5% of street cattle): big head, long legs, horn buds.</summary>
        Calf = 1,

        /// <summary>Zebu bull (ox): bigger hump that leans, thick neck, longer dewlap, heavier horns.</summary>
        Bull = 2,

        /// <summary>Water buffalo: heavy slate-grey barrel, swept-back crescent horns, pale throat chevrons.</summary>
        Buffalo = 3,

        /// <summary>Street dog (Himalayan pariah type): wedge head, pricked or half-folded ears, tail curled over the back.</summary>
        Dog = 4,

        /// <summary>Khari hill goat: slim, floppy ears, short back-curved horns, beard, flicked-up tail.</summary>
        Goat = 5,

        /// <summary>Rhesus macaque (Swayambhu, Pashupati): brown-grey fur, orange rump, bare pink face.</summary>
        Macaque = 6,

        /// <summary>Macaque baby (rides its mother or sits close by).</summary>
        MacaqueBaby = 7,

        /// <summary>Village hen: red-brown, small comb.</summary>
        Hen = 8,

        /// <summary>Rooster: big red comb and wattles, golden hackles, arching green-black sickle tail.</summary>
        Rooster = 9,

        /// <summary>Domestic duck of the village ponds: white or brown, orange bill and feet.</summary>
        Duck = 10,

        /// <summary>Rock pigeon of the durbar squares: grey, iridescent neck, two dark wing bars.</summary>
        Pigeon = 11,

        /// <summary>House crow: black with a grey collar.</summary>
        Crow = 12,

        /// <summary>Black kite: brown, forked tail, circles over the city.</summary>
        BlackKite = 13,

        /// <summary>Common myna: brown body, black head, yellow bill and eye patch, white wing patch.</summary>
        Myna = 14,

        /// <summary>House sparrow: brown streaked back, grey crown, black bib.</summary>
        Sparrow = 15,

        /// <summary>Cattle egret: white, yellow bill, follows buffalo in the paddy.</summary>
        Egret = 16,

        /// <summary>Barn swallow: blue-black, rufous throat, long tail streamers (fields, Mar–Oct).</summary>
        Swallow = 17,
    }

    /// <summary>Body plan of a species: which rig layout, mesher and clip set it uses.</summary>
    public enum FaunaFamily : byte
    {
        /// <summary>Cattle and buffalo.</summary>
        Bovine = 0,

        /// <summary>Dogs.</summary>
        Canine = 1,

        /// <summary>Goats.</summary>
        Caprine = 2,

        /// <summary>Macaques.</summary>
        Primate = 3,

        /// <summary>Ground fowl (hens, roosters, ducks): walk most of the time.</summary>
        Fowl = 4,

        /// <summary>Flying birds.</summary>
        Bird = 5,
    }

    /// <summary>
    /// The bones every fauna rig has (unused bones of a body plan simply carry no vertices). Pivots are per species
    /// (<see cref="FaunaMesh.Pivot"/>); the hierarchy is shared (<see cref="FaunaRig.Parent"/>). Model space: +Z
    /// forward, +Y up, +X to the animal's right, the root on the ground under the body centre.
    /// </summary>
    public enum FaunaBone : byte
    {
        Root = 0,
        Pelvis = 1,
        Chest = 2,
        Neck = 3,
        Head = 4,
        Jaw = 5,
        EarL = 6,
        EarR = 7,
        Tail0 = 8,
        Tail1 = 9,
        Tail2 = 10,
        FrontUpperL = 11,
        FrontLowerL = 12,
        FrontFootL = 13,
        FrontUpperR = 14,
        FrontLowerR = 15,
        FrontFootR = 16,
        HindUpperL = 17,
        HindLowerL = 18,
        HindFootL = 19,
        HindUpperR = 20,
        HindLowerR = 21,
        HindFootR = 22,

        /// <summary>Spread wing (shoulder to wrist); scaled to zero while the bird stands.</summary>
        WingL = 23,
        WingTipL = 24,
        WingR = 25,
        WingTipR = 26,

        /// <summary>Folded wing lying on the body; scaled to zero in flight.</summary>
        FoldL = 27,
        FoldR = 28,

        /// <summary>A rider (macaque baby on its mother's back).</summary>
        Rider = 29,
    }

    /// <summary>The bone hierarchy shared by every fauna rig.</summary>
    public static class FaunaRig
    {
        /// <summary>Number of bones (<see cref="FaunaBone"/>).</summary>
        public const int BoneCount = 30;

        /// <summary>Parent of each bone (−1 for the root).</summary>
        public static readonly sbyte[] Parent =
        {
            -1, // Root
            0, // Pelvis
            1, // Chest
            2, // Neck
            3, // Head
            4, // Jaw
            4, // EarL
            4, // EarR
            1, // Tail0
            8, // Tail1
            9, // Tail2
            2, 11, 12, // front left
            2, 14, 15, // front right
            1, 17, 18, // hind left
            1, 20, 21, // hind right
            2, 23, // wing left
            2, 25, // wing right
            2, // FoldL
            2, // FoldR
            2, // Rider
        };

        /// <summary>Bone names (for debugging and previews).</summary>
        public static readonly string[] Names =
        {
            "root", "pelvis", "chest", "neck", "head", "jaw", "ear_l", "ear_r", "tail0", "tail1", "tail2",
            "front_upper_l", "front_lower_l", "front_foot_l", "front_upper_r", "front_lower_r", "front_foot_r",
            "hind_upper_l", "hind_lower_l", "hind_foot_l", "hind_upper_r", "hind_lower_r", "hind_foot_r",
            "wing_l", "wing_tip_l", "wing_r", "wing_tip_r", "fold_l", "fold_r", "rider",
        };

        /// <summary>The mirror of a bone (left and right swap; others map to themselves).</summary>
        public static FaunaBone Mirror(FaunaBone b)
        {
            switch (b)
            {
                case FaunaBone.EarL: return FaunaBone.EarR;
                case FaunaBone.EarR: return FaunaBone.EarL;
                case FaunaBone.FrontUpperL: return FaunaBone.FrontUpperR;
                case FaunaBone.FrontLowerL: return FaunaBone.FrontLowerR;
                case FaunaBone.FrontFootL: return FaunaBone.FrontFootR;
                case FaunaBone.FrontUpperR: return FaunaBone.FrontUpperL;
                case FaunaBone.FrontLowerR: return FaunaBone.FrontLowerL;
                case FaunaBone.FrontFootR: return FaunaBone.FrontFootL;
                case FaunaBone.HindUpperL: return FaunaBone.HindUpperR;
                case FaunaBone.HindLowerL: return FaunaBone.HindLowerR;
                case FaunaBone.HindFootL: return FaunaBone.HindFootR;
                case FaunaBone.HindUpperR: return FaunaBone.HindUpperL;
                case FaunaBone.HindLowerR: return FaunaBone.HindLowerL;
                case FaunaBone.HindFootR: return FaunaBone.HindFootL;
                case FaunaBone.WingL: return FaunaBone.WingR;
                case FaunaBone.WingTipL: return FaunaBone.WingTipR;
                case FaunaBone.WingR: return FaunaBone.WingL;
                case FaunaBone.WingTipR: return FaunaBone.WingTipL;
                case FaunaBone.FoldL: return FaunaBone.FoldR;
                case FaunaBone.FoldR: return FaunaBone.FoldL;
                default: return b;
            }
        }

        /// <summary>True when <paramref name="bone"/> is <paramref name="ancestor"/> or lies below it.</summary>
        public static bool IsUnder(FaunaBone bone, FaunaBone ancestor)
        {
            int b = (int)bone;
            while (b >= 0)
            {
                if (b == (int)ancestor) return true;
                b = Parent[b];
            }
            return false;
        }
    }

    /// <summary>
    /// Procedural clips (<see cref="FaunaAnimator"/>). The first six mirror <c>Core.Traffic.AnimalClip</c> so a sim
    /// pose maps across directly; the rest are for the wildlife presenters. Append only.
    /// </summary>
    public enum FaunaClip : byte
    {
        /// <summary>Lying on the sternum, chewing the cud (cows), or lying down (dogs, goats).</summary>
        Lie = 0,

        /// <summary>Standing idle: breathing, tail swish, ear flicks, slow head turns.</summary>
        Stand = 1,

        /// <summary>Four-beat walk (birds: walking with a head bob).</summary>
        Walk = 2,

        /// <summary>Asleep: dogs flat on one side with the legs stretched out; cattle with the head turned back; birds fluffed with the head sunk.</summary>
        Sleep = 3,

        /// <summary>Sitting (dogs, macaques on their haunches).</summary>
        Sit = 4,

        /// <summary>Barking (dogs).</summary>
        Bark = 5,

        /// <summary>Two-beat trot in diagonal pairs.</summary>
        Trot = 6,

        /// <summary>Head down at the vegetable waste or grass, chewing, a step now and then.</summary>
        Graze = 7,

        /// <summary>Dog sitting and scratching an ear with a hind foot.</summary>
        Scratch = 8,

        /// <summary>Macaque grooming (picking through a partner's fur).</summary>
        Groom = 9,

        /// <summary>Macaque climbing up a vertical surface.</summary>
        Climb = 10,

        /// <summary>Bird pecking at the ground.</summary>
        Peck = 11,

        /// <summary>Bird hop (sparrows, crows).</summary>
        Hop = 12,

        /// <summary>Bird take-off: crouch, leap, fast strokes.</summary>
        TakeOff = 13,

        /// <summary>Flapping flight.</summary>
        Flap = 14,

        /// <summary>Gliding on spread wings.</summary>
        Glide = 15,

        /// <summary>Soaring and circling (kites): banked, tail twisting.</summary>
        Soar = 16,

        /// <summary>Landing flare.</summary>
        Land = 17,

        /// <summary>Duck swimming (legs paddling under the water line).</summary>
        Swim = 18,

        /// <summary>Gallop or fast run (macaques bounding, startled chickens).</summary>
        Run = 19,
    }

    /// <summary>Body part of a vertex (for coat painting and tests).</summary>
    public enum FaunaPart : byte
    {
        Body = 0,
        Head = 1,
        Muzzle = 2,
        Ear = 3,
        Horn = 4,
        Leg = 5,
        Hoof = 6,
        Tail = 7,
        Eye = 8,
        Nose = 9,
        Mouth = 10,
        Hump = 11,
        Dewlap = 12,
        Udder = 13,
        Beak = 14,
        Comb = 15,
        Wing = 16,
        Feet = 17,
        Face = 18,
        Hand = 19,
        Beard = 20,
        Neck = 21,
        Collar = 22,
    }

    /// <summary>
    /// Coat pattern of a mesh variant. The coat colour itself is the per-instance tint (vertex alpha 1 = tinted);
    /// pattern colours are fixed in the mesh (alpha 0), so one mesh per pattern serves every tint.
    /// </summary>
    public enum CoatPattern : byte
    {
        /// <summary>One coat colour (lighter belly, darker points are shading in the tinted colour).</summary>
        Plain = 0,

        /// <summary>White patches over the tinted coat (pied cows, patched dogs and goats).</summary>
        Patched = 1,

        /// <summary>Black saddle and back over a tinted tan (black-and-tan dogs), dark face and legs on goats.</summary>
        Saddle = 2,

        /// <summary>White socks, chest blaze and tail tip (dogs), white face (cows).</summary>
        Socks = 3,
    }

    /// <summary>Static facts per species: family, size, LOD budgets, gait speeds.</summary>
    public readonly struct FaunaSpeciesInfo
    {
        public readonly FaunaSpecies Species;
        public readonly FaunaFamily Family;

        /// <summary>Height at the withers (birds: standing height), metres.</summary>
        public readonly float HeightM;

        /// <summary>Nose-to-tail length (birds: bill to tail tip), metres.</summary>
        public readonly float LengthM;

        /// <summary>Wingspan (birds), metres; 0 for the others.</summary>
        public readonly float WingspanM;

        /// <summary>Triangle budgets of LOD0 / LOD1 / LOD2 (W2_DESIGN 5.5-5.6, revised by the detail pass): triangles drawn in
        /// a pose (the spread or the folded wings are hidden); the LOD2 of flying birds is the paper bird.</summary>
        public readonly int Lod0Tris, Lod1Tris, Lod2Tris;

        /// <summary>Walking speed and the speed above which the gait changes to trot (or run), m/s.</summary>
        public readonly float WalkMps, TrotMps;

        /// <summary>Wing-beat frequency in flapping flight (Hz; birds only).</summary>
        public readonly float FlapHz;

        public FaunaSpeciesInfo(FaunaSpecies s, FaunaFamily f, float h, float len, float span, int l0, int l1, int l2, float walk, float trot, float flapHz)
        {
            Species = s;
            Family = f;
            HeightM = h;
            LengthM = len;
            WingspanM = span;
            Lod0Tris = l0;
            Lod1Tris = l1;
            Lod2Tris = l2;
            WalkMps = walk;
            TrotMps = trot;
            FlapHz = flapHz;
        }

        /// <summary>True for every species that flies (hens and ducks only flutter; they count as fowl).</summary>
        public bool Flies
        {
            get { return Family == FaunaFamily.Bird; }
        }

        /// <summary>True for birds and fowl (two legs and wings).</summary>
        public bool IsAvian
        {
            get { return Family == FaunaFamily.Bird || Family == FaunaFamily.Fowl; }
        }

        /// <summary>The triangle budget of a level (0..2).</summary>
        public int Budget(int lod)
        {
            return lod <= 0 ? Lod0Tris : lod == 1 ? Lod1Tris : Lod2Tris;
        }
    }

    /// <summary>The species table.</summary>
    public static class FaunaCatalog
    {
        /// <summary>Number of species (<see cref="FaunaSpecies"/>).</summary>
        public const int SpeciesCount = 18;

        private static readonly FaunaSpeciesInfo[] Table =
        {
            new FaunaSpeciesInfo(FaunaSpecies.Cow, FaunaFamily.Bovine, 1.20f, 2.05f, 0f, 3000, 1000, 340, 0.9f, 1.8f, 0f),
            new FaunaSpeciesInfo(FaunaSpecies.Calf, FaunaFamily.Bovine, 0.80f, 1.25f, 0f, 3000, 1000, 340, 0.9f, 1.8f, 0f),
            new FaunaSpeciesInfo(FaunaSpecies.Bull, FaunaFamily.Bovine, 1.32f, 2.25f, 0f, 3000, 1000, 340, 0.9f, 1.8f, 0f),
            new FaunaSpeciesInfo(FaunaSpecies.Buffalo, FaunaFamily.Bovine, 1.30f, 2.40f, 0f, 3000, 1000, 420, 0.8f, 1.6f, 0f),
            new FaunaSpeciesInfo(FaunaSpecies.Dog, FaunaFamily.Canine, 0.52f, 0.95f, 0f, 2000, 800, 300, 1.0f, 1.9f, 0f),
            new FaunaSpeciesInfo(FaunaSpecies.Goat, FaunaFamily.Caprine, 0.62f, 1.00f, 0f, 2200, 800, 320, 0.8f, 1.7f, 0f),
            new FaunaSpeciesInfo(FaunaSpecies.Macaque, FaunaFamily.Primate, 0.42f, 0.85f, 0f, 2500, 1000, 320, 0.9f, 2.2f, 0f),
            new FaunaSpeciesInfo(FaunaSpecies.MacaqueBaby, FaunaFamily.Primate, 0.20f, 0.42f, 0f, 2500, 1000, 320, 0.6f, 1.6f, 0f),
            new FaunaSpeciesInfo(FaunaSpecies.Hen, FaunaFamily.Fowl, 0.38f, 0.42f, 0.70f, 900, 160, 110, 0.5f, 1.6f, 6f),
            new FaunaSpeciesInfo(FaunaSpecies.Rooster, FaunaFamily.Fowl, 0.50f, 0.52f, 0.80f, 1000, 180, 140, 0.5f, 1.6f, 6f),
            new FaunaSpeciesInfo(FaunaSpecies.Duck, FaunaFamily.Fowl, 0.36f, 0.50f, 0.85f, 700, 160, 100, 0.45f, 1.2f, 5f),
            new FaunaSpeciesInfo(FaunaSpecies.Pigeon, FaunaFamily.Bird, 0.24f, 0.32f, 0.66f, 380, 105, 8, 0.6f, 1.2f, 5f),
            new FaunaSpeciesInfo(FaunaSpecies.Crow, FaunaFamily.Bird, 0.28f, 0.42f, 0.80f, 380, 105, 8, 0.6f, 1.4f, 3.5f),
            new FaunaSpeciesInfo(FaunaSpecies.BlackKite, FaunaFamily.Bird, 0.34f, 0.58f, 1.50f, 520, 110, 8, 0.4f, 0.8f, 2f),
            new FaunaSpeciesInfo(FaunaSpecies.Myna, FaunaFamily.Bird, 0.18f, 0.24f, 0.42f, 390, 105, 8, 0.7f, 1.4f, 7f),
            new FaunaSpeciesInfo(FaunaSpecies.Sparrow, FaunaFamily.Bird, 0.11f, 0.15f, 0.24f, 380, 105, 8, 0.5f, 1.0f, 9f),
            new FaunaSpeciesInfo(FaunaSpecies.Egret, FaunaFamily.Bird, 0.50f, 0.52f, 0.92f, 460, 125, 8, 0.5f, 1.0f, 3f),
            new FaunaSpeciesInfo(FaunaSpecies.Swallow, FaunaFamily.Bird, 0.10f, 0.18f, 0.33f, 410, 105, 8, 0.3f, 0.6f, 8f),
        };

        /// <summary>Facts of a species.</summary>
        public static FaunaSpeciesInfo Info(FaunaSpecies s)
        {
            int i = (int)s;
            if (i < 0 || i >= Table.Length) throw new ArgumentOutOfRangeException(nameof(s));
            return Table[i];
        }

        /// <summary>Family of a species.</summary>
        public static FaunaFamily Family(FaunaSpecies s)
        {
            return Info(s).Family;
        }
    }
}
