using System;
using Ghumante.Core.Data;
using Ghumante.Core.Save;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Characters
{
    /// <summary>The four adult builds (W2_DESIGN 6.1, P §2.3). Builds scale the bones; the topology is the same.</summary>
    public enum BodyBuild : byte
    {
        /// <summary>Slim, 1.52 m.</summary>
        A = 0,

        /// <summary>Average, 1.55 m (the default).</summary>
        B = 1,

        /// <summary>Sturdy, 1.56 m.</summary>
        C = 2,

        /// <summary>Tall, 1.66 m.</summary>
        D = 3,
    }

    /// <summary>Where an outfit item goes. A recipe holds one item per slot (<see cref="CharacterRecipe.Outfit"/>).</summary>
    public enum OutfitSlotKind : byte
    {
        Head = 0,
        Torso = 1,
        Legs = 2,
        Feet = 3,
        Back = 4,
    }

    /// <summary>
    /// Outfit items as parameter recipes on the same primitives (P §2.7, street_life §3.3, ref_characters.md); no brands,
    /// no logos, no insignia. Append-only (saved by value in <c>player.appearance</c>).
    /// </summary>
    public enum OutfitItem : byte
    {
        None = 0,

        // Head
        DhakaTopi = 1,
        BhadgaunleTopi = 2,
        Beanie = 3,
        SunHat = 4,

        /// <summary>A plain peaked cap (no logo).</summary>
        Cap = 5,

        /// <summary>The traffic police's navy peaked cap (generic, no badge).</summary>
        PoliceCap = 6,

        /// <summary>A cotton headscarf (farmers, older women).</summary>
        Headscarf = 7,

        // Torso
        TShirt = 10,
        Hoodie = 11,
        DhakaJacket = 12,
        Kurta = 13,
        Daura = 14,
        Sari = 15,
        Robe = 16,
        Uniform = 17,

        /// <summary>A collared button shirt.</summary>
        Shirt = 18,

        /// <summary>Traffic police: a blue shirt with a fluorescent reflective vest.</summary>
        PoliceUniform = 19,

        /// <summary>A zipped trekking fleece (tourists).</summary>
        Fleece = 20,

        /// <summary>A sleeveless vest (porters, labourers).</summary>
        Vest = 21,

        /// <summary>School uniform: a light shirt with a striped tie and a belt.</summary>
        SchoolShirt = 22,

        /// <summary>The Tibetan chuba: an ankle-length sleeveless wrap dress over a blouse, with the striped pangden apron
        /// (Boudha, Swayambhu).</summary>
        Chuba = 23,

        /// <summary>A quilted puffer jacket (winter mornings; very common in the valley).</summary>
        Jacket = 24,

        /// <summary>An office blazer over a collared shirt.</summary>
        Blazer = 25,

        // Legs
        Jeans = 30,
        Joggers = 31,
        Shorts = 32,
        Suruwal = 33,

        /// <summary>Plain trousers with a crease (police, school, office).</summary>
        Trousers = 34,

        /// <summary>A pleated school skirt.</summary>
        Skirt = 35,

        /// <summary>Trekking trousers with side pockets and knee panels.</summary>
        TrekTrousers = 36,

        /// <summary>Trousers rolled up to mid-shin (porters, farmers).</summary>
        RolledTrousers = 37,

        // Feet
        Sneakers = 40,
        Chappal = 41,
        TrekBoots = 42,
        Barefoot = 43,

        /// <summary>Polished leather shoes (daura, police, school, office).</summary>
        LeatherShoes = 44,

        // Back
        Daypack = 50,
        Doko = 51,

        /// <summary>A hessian sack on the back with a namlo strap (bhariya porters).</summary>
        Sack = 52,

        /// <summary>An LPG cylinder carried on the back with a namlo strap.</summary>
        GasCylinder = 53,

        /// <summary>A baby carried on the back in a patterned shawl sling.</summary>
        BabySling = 54,
    }

    /// <summary>Age group of a character: scales the body (a child is 1.22 m with a bigger head) and steers the NPC
    /// wardrobe (elders wear grey hair, glasses and shawls more often). Append-only.</summary>
    public enum AgeGroup : byte
    {
        Adult = 0,
        Child = 1,
        Teen = 2,
        Elder = 3,
    }

    /// <summary>Facial hair on the head mesh (P §2.6 extension). Append-only.</summary>
    public enum FacialHair : byte
    {
        None = 0,

        /// <summary>A neat moustache.</summary>
        Moustache = 1,

        /// <summary>A full moustache with drooping ends (common on older men in daura suruwal).</summary>
        BushyMoustache = 2,

        /// <summary>Stubble: the jaw tinted with the hair colour.</summary>
        Stubble = 3,

        /// <summary>A short boxed beard with a moustache.</summary>
        ShortBeard = 4,

        /// <summary>A chin goatee with a moustache.</summary>
        Goatee = 5,

        /// <summary>A long flowing beard (sadhus).</summary>
        LongBeard = 6,
    }

    /// <summary>Small worn details that sit on top of the outfit (flags; append-only).</summary>
    [Flags]
    public enum CharacterAccents : ushort
    {
        None = 0,
        Glasses = 1,
        Sunglasses = 2,

        /// <summary>A small red dot between the brows.</summary>
        Bindi = 4,

        /// <summary>A red tika on the forehead (festival days).</summary>
        Tika = 8,

        Earrings = 16,

        /// <summary>The green bead necklace of married women, with sindoor in the hair parting.</summary>
        Pote = 32,

        Watch = 64,

        /// <summary>A shawl over the shoulders (older women, cool mornings) or a dupatta with a kurta.</summary>
        Shawl = 128,

        /// <summary>Prayer beads (monks, sadhus, kora walkers).</summary>
        Mala = 256,

        /// <summary>The sadhu's forehead marking (three ash lines with a red dot).</summary>
        Tilak = 512,

        /// <summary>White gloves (traffic police hand signals).</summary>
        Gloves = 1024,

        /// <summary>An open umbrella held in the right hand (monsoon rain, May sun).</summary>
        Umbrella = 2048,

        /// <summary>A small gold nose stud (phuli) on the left nostril.</summary>
        NoseStud = 4096,

        /// <summary>A light blue face mask against dust (traffic police, some commuters).</summary>
        Mask = 8192,

        /// <summary>A hand prayer wheel in the right hand (kora walkers at Boudha and Swayambhu).</summary>
        PrayerWheel = 16384,
    }

    /// <summary>The face states the procedural systems call (P §2.8). The player mesh carries them as blend shapes.
    /// Append-only.</summary>
    public enum FaceExpression : byte
    {
        /// <summary>The default friendly closed smile.</summary>
        Smile = 0,

        Neutral = 1,

        /// <summary>Discovery, landing a jump: squint-smile eyes and an open smile.</summary>
        Joy = 2,

        /// <summary>Bumping into something: squeezed eyes and a teeth grin.</summary>
        WinceLaugh = 3,

        /// <summary>Out of breath: half-lidded eyes and an O mouth with puffed cheeks.</summary>
        Puff = 4,

        /// <summary>Inside temple compounds: soft eyes and a gentle smile.</summary>
        Calm = 5,
    }

    /// <summary>The face the mesher sculpts: an expression, the eyelid closure (1 = closed, for the blink shape) and
    /// where the eyes look (−1..1 across, −1..1 up). Every face state has the same topology, so the player's mesh
    /// carries them as blend shapes.</summary>
    public struct FaceState
    {
        public FaceExpression Expression;
        public float Blink;
        public float LookX, LookY;

        public FaceState(FaceExpression expression, float blink = 0f, float lookX = 0f, float lookY = 0f)
        {
            Expression = expression;
            Blink = blink;
            LookX = lookX;
            LookY = lookY;
        }

        /// <summary>The default face (closed smile, eyes open, looking ahead).</summary>
        public static FaceState Default
        {
            get { return new FaceState(FaceExpression.Smile); }
        }
    }

    /// <summary>One worn item: what it is, its colour (an index into the item's palette, <see cref="CharacterPalette"/>)
    /// and a pattern where the item has one (the dhaka weave, a sari border, a shawl print).</summary>
    public struct OutfitSlot
    {
        public OutfitItem Item;
        public byte Colour;
        public byte Pattern;

        public OutfitSlot(OutfitItem item, byte colour = 0, byte pattern = 0)
        {
            Item = item;
            Colour = colour;
            Pattern = pattern;
        }
    }

    /// <summary>
    /// A character as data (W2_DESIGN 10.3, P §2): build, figure, age group and a height nudge, skin swatch (numbered
    /// 1–10, never named after a group), one of <see cref="HairStyleCount"/> hairstyles in one of
    /// <see cref="HairColourCount"/> colours, facial hair, eye colour, small accents (glasses, bindi, earrings, shawl...),
    /// the helmet colour auto-equipped on two-wheelers, one outfit item per slot and a seed for small variation.
    /// <see cref="HumanoidMesher"/> builds the mesh from it, for the player and for NPC bodies
    /// (<see cref="ForPedestrian(PedArchetype, int, uint, StreetStyle, int)"/>). Saved as the <c>player.appearance</c>
    /// object (<see cref="ToJson"/>). Values out of range are clamped by <see cref="Validate"/>, so an old or edited
    /// save never breaks the mesher.
    /// </summary>
    public sealed class CharacterRecipe : IEquatable<CharacterRecipe>
    {
        public const int SkinCount = 10, HairStyleCount = 17, HairColourCount = 10, HelmetColourCount = 8, SlotCount = 5, EyeColourCount = 5;

        /// <summary>Hairstyles added in the detail pass: 13 shaved (monks), 14 dreadlocks (sadhus), 15 receding
        /// (older men), 16 two plaits with ribbons (school girls), 17 short back and sides with a quiff (young men).</summary>
        public const byte HairShaved = 13, HairDreadlocks = 14, HairReceding = 15, HairTwoPlaits = 16, HairQuiff = 17;

        /// <summary>The player's default name, "wanderer" (P §2.2).</summary>
        public const string DefaultName = "Ghumante";

        public BodyBuild Build = BodyBuild.B;

        /// <summary>Body outline: 0 straight, 1 soft (narrower shoulders, fuller hips, a gentle chest curve). No gender
        /// gate: any outfit goes on any figure.</summary>
        public byte Figure;

        public AgeGroup Age = AgeGroup.Adult;

        /// <summary>Height nudge in percent of the build's height, −6..+6 (NPC variety).</summary>
        public sbyte HeightPct;

        /// <summary>Skin swatch number, 1..10.</summary>
        public byte Skin = 5;

        /// <summary>Hairstyle h01..h17 as 1..17 (P §2.6; 13..17 added in the detail pass).</summary>
        public byte Hair = 1;

        /// <summary>Hair colour index 0..9 (black, soft black, dark brown, brown, auburn, grey, white, teal, blonde,
        /// light brown).</summary>
        public byte HairColour;

        public FacialHair Beard;

        /// <summary>Eye colour index 0..4 (dark brown, brown, hazel, green, grey-blue).</summary>
        public byte EyeColour;

        public CharacterAccents Accents;

        /// <summary>Helmet colour index 0..7 (red, blue, yellow, white, black, green, orange, pink).</summary>
        public byte HelmetColour;

        /// <summary>One item per <see cref="OutfitSlotKind"/>, in that order.</summary>
        public OutfitSlot[] Outfit = DefaultOutfit();

        public uint Seed = 1u;

        public string Name = DefaultName;

        public OutfitSlot this[OutfitSlotKind slot]
        {
            get { return Outfit[(int)slot]; }
            set { Outfit[(int)slot] = value; }
        }

        /// <summary>True when the recipe wears <paramref name="a"/>.</summary>
        public bool Has(CharacterAccents a)
        {
            return (Accents & a) != 0;
        }

        /// <summary>The default explorer: dhaka topi, dhaka jacket over jeans, sneakers and a daypack.</summary>
        public static OutfitSlot[] DefaultOutfit()
        {
            return new[]
            {
                new OutfitSlot(OutfitItem.DhakaTopi, 0, 0),
                new OutfitSlot(OutfitItem.DhakaJacket, 0, 0),
                new OutfitSlot(OutfitItem.Jeans, 0),
                new OutfitSlot(OutfitItem.Sneakers, 0),
                new OutfitSlot(OutfitItem.Daypack, 2),
            };
        }

        /// <summary>
        /// A fresh player recipe for <paramref name="seed"/>: the default outfit with the skin swatch drawn at random but
        /// never #1 (P §2.5: the picker opens on a random swatch, never on the first), and a random hairstyle (of the
        /// first 12) and colour.
        /// </summary>
        public static CharacterRecipe NewPlayer(uint seed)
        {
            uint h = CharMath.Hash(seed, 0x504C4159u);
            var r = new CharacterRecipe
            {
                Seed = seed,
                Skin = (byte)(2 + h % (SkinCount - 1)),
                Hair = (byte)(1 + (h >> 8) % 12),
                HairColour = (byte)((h >> 16) % 5),
                HelmetColour = (byte)((h >> 20) % HelmetColourCount),
            };
            return r;
        }

        /// <summary>An NPC body for a pedestrian archetype with no place or carry information.</summary>
        public static CharacterRecipe ForPedestrian(PedArchetype archetype, int tint, uint seed)
        {
            return ForPedestrian(archetype, tint, seed, StreetStyle.Urban, 0);
        }

        /// <summary>The share of men walking at Pashupati (casual, daura suruwal, vendors) who are dressed as sadhus
        /// (<see cref="StreetStyle.Pashupati"/>).</summary>
        public const float PashupatiSadhuShare = 0.12f;

        /// <summary>An NPC body whose wardrobe follows the area type (see <see cref="StreetStyles.OfArea"/>).</summary>
        public static CharacterRecipe ForPedestrian(PedArchetype archetype, int tint, uint seed, AreaType area, int carry)
        {
            return ForPedestrian(archetype, tint, seed, StreetStyles.OfArea(area), carry);
        }

        /// <summary>
        /// An NPC body for a pedestrian archetype (S §3.2–3.3, ref_characters.md; the crowd uses the same generator as
        /// the player, W2_DESIGN 10.3): age, build, figure, height, hair, facial hair, accents and outfit follow the
        /// archetype and the place (<paramref name="style"/>: black Bhadgaunle topis in Bhaktapur, trek gear in Thamel,
        /// chuba at Boudha, shirts and blazers in the office districts, rolled trousers and headscarves in the
        /// villages); colours follow <paramref name="tint"/> and <paramref name="seed"/>. <paramref name="carry"/> is the
        /// sim's carry prop (<c>PedPose.CarryProp</c>: 1 doko, 2 sack, 3 gas cylinder, 4 baby on the back, 5 umbrella in
        /// the hand). Monks get maroon robes and a shaved head, sadhus saffron, dreadlocks and a tilak, porters a doko
        /// with its namlo, traffic police a blue uniform with a reflective vest and white gloves; nobody gets a brand
        /// or an insignia.
        /// </summary>
        public static CharacterRecipe ForPedestrian(PedArchetype archetype, int tint, uint seed, StreetStyle style, int carry)
        {
            uint h = CharMath.Hash(seed, (uint)archetype, (uint)tint);
            var rng = new Pick(CharMath.Hash(h, 0x5EEDu, (uint)style));
            var r = new CharacterRecipe
            {
                Seed = seed,
                Build = (BodyBuild)(h % 4),
                Skin = (byte)(1 + (h >> 4) % SkinCount),
                Hair = (byte)(1 + (h >> 9) % 12),
                HairColour = (byte)((h >> 13) % 3),
                HelmetColour = (byte)((h >> 17) % HelmetColourCount),
                EyeColour = (byte)(rng.Chance(0.8f) ? 0 : 1),
                HeightPct = (sbyte)(rng.Range(9) - 4),
                Name = "",
            };
            byte c = (byte)(tint & 0x7F);
            bool pashupati = style == StreetStyle.Pashupati;
            // Sadhus gather at Pashupati: some of the men walking there are dressed as one (saffron, jata, tilak).
            if (pashupati && (archetype == PedArchetype.UrbanCasual || archetype == PedArchetype.DauraSuruwal || archetype == PedArchetype.Vendor) &&
                rng.Chance(PashupatiSadhuShare))
                archetype = PedArchetype.Sadhu;
            bool newar = style == StreetStyle.NewarTown, bazaar = style == StreetStyle.OldBazaar || newar || pashupati;
            bool village = style == StreetStyle.Village, office = style == StreetStyle.Office, buddhist = style == StreetStyle.Buddhist;
            bool tourist = style == StreetStyle.Tourist;
            // Young people dye their hair brown now and then.
            if (rng.Chance(0.08f)) r.HairColour = (byte)rng.Of(3, 4, 9);
            switch (archetype)
            {
                case PedArchetype.KurtaSari:
                {
                    r.Figure = 1;
                    r.Age = rng.Chance(0.25f) ? AgeGroup.Elder : rng.Chance(0.12f) ? AgeGroup.Teen : AgeGroup.Adult;
                    float sariP = pashupati ? 0.8f : r.Age == AgeGroup.Elder ? 0.65f : office ? 0.4f : 0.3f;
                    r.Hair = rng.Of(7, 9, 8, 6, 9, 7, 11, 5);
                    if (buddhist && rng.Chance(0.5f))
                    {
                        r.Outfit = Slots(OutfitItem.None, OutfitItem.Chuba, OutfitItem.None, rng.Chance(0.6f) ? OutfitItem.LeatherShoes : OutfitItem.Sneakers,
                                         OutfitItem.None, c);
                        r.Outfit[1].Pattern = (byte)rng.Range(4);
                        r.Hair = rng.Of(7, 9);
                        if (rng.Chance(0.5f)) r.Accents |= CharacterAccents.Mala;
                        if (rng.Chance(0.3f)) r.Accents |= CharacterAccents.PrayerWheel;
                    }
                    else if (rng.Chance(sariP))
                    {
                        r.Outfit = Slots(village && rng.Chance(0.4f) ? OutfitItem.Headscarf : OutfitItem.None, OutfitItem.Sari, OutfitItem.None,
                                         rng.Chance(0.7f) ? OutfitItem.Chappal : OutfitItem.LeatherShoes, OutfitItem.None, c);
                        r.Outfit[1].Pattern = (byte)rng.Range(4);
                        // Pilgrims at Pashupati wear red and maroon most of all.
                        if (pashupati && rng.Chance(0.6f)) r.Outfit[1].Colour = (byte)rng.Of(0, 0, 6, 1);
                        if (rng.Chance(0.5f)) r.Accents |= CharacterAccents.Pote;
                    }
                    else
                    {
                        r.Outfit = Slots(village && rng.Chance(0.3f) ? OutfitItem.Headscarf : OutfitItem.None, OutfitItem.Kurta, OutfitItem.Suruwal,
                                         rng.Chance(0.55f) ? OutfitItem.Chappal : rng.Chance(0.5f) ? OutfitItem.Sneakers : OutfitItem.LeatherShoes, OutfitItem.None, c);
                        r.Outfit[2].Colour = (byte)rng.Range(6);
                        if (rng.Chance(0.6f)) r.Accents |= CharacterAccents.Shawl; // the dupatta
                        if (rng.Chance(0.25f)) r.Accents |= CharacterAccents.Pote;
                    }
                    if (rng.Chance(0.55f)) r.Accents |= CharacterAccents.Bindi;
                    if (rng.Chance(0.7f)) r.Accents |= CharacterAccents.Earrings;
                    if (rng.Chance(0.45f)) r.Accents |= CharacterAccents.NoseStud;
                    if (r.Age == AgeGroup.Elder && rng.Chance(0.5f)) r.Accents |= CharacterAccents.Shawl;
                    break;
                }
                case PedArchetype.DauraSuruwal:
                    r.Age = rng.Chance(0.65f) ? AgeGroup.Elder : AgeGroup.Adult;
                    r.Hair = r.Age == AgeGroup.Elder && rng.Chance(0.5f) ? HairReceding : (byte)rng.Of(1, 2, 1);
                    // The black Bhadgaunle topi is the Bhaktapur cap; the dhaka topi everywhere else.
                    r.Outfit = Slots(rng.Chance(newar ? 0.6f : 0.25f) ? OutfitItem.BhadgaunleTopi : OutfitItem.DhakaTopi, OutfitItem.Daura,
                                     OutfitItem.Suruwal, rng.Chance(0.6f) ? OutfitItem.LeatherShoes : OutfitItem.Chappal, OutfitItem.None, c);
                    r.Outfit[0].Pattern = (byte)rng.Range(CharacterPalette.DhakaWeaves);
                    r.Outfit[0].Colour = (byte)rng.Range(CharacterPalette.TopiBase.Length);
                    r.Outfit[1].Pattern = (byte)rng.Range(3); // the coat colour
                    r.Beard = rng.Chance(0.6f) ? (rng.Chance(0.5f) ? FacialHair.BushyMoustache : FacialHair.Moustache) : FacialHair.None;
                    if (rng.Chance(0.35f)) r.Accents |= CharacterAccents.Glasses;
                    if (rng.Chance(0.3f)) r.Accents |= CharacterAccents.Watch;
                    if (rng.Chance(0.15f)) r.Accents |= CharacterAccents.Tika;
                    break;
                case PedArchetype.Hakupatasi:
                    r.Figure = 1;
                    r.Age = rng.Chance(0.75f) ? AgeGroup.Elder : AgeGroup.Adult;
                    r.Hair = 9;
                    r.Outfit = Slots(OutfitItem.None, OutfitItem.Sari, OutfitItem.None, OutfitItem.Chappal, OutfitItem.None, CharacterPalette.HakuSari);
                    // Half drape the patasi like a sari with its red-bordered pallu across the chest; the others tie it with
                    // the patuka sash and often wear a shawl.
                    bool drape = rng.Chance(0.5f);
                    r.Outfit[1].Pattern = (byte)(drape ? 1 : 0);
                    r.Accents |= CharacterAccents.Earrings | CharacterAccents.NoseStud | (!drape && rng.Chance(0.6f) ? CharacterAccents.Shawl : CharacterAccents.None);
                    if (drape || rng.Chance(0.6f)) r.Accents |= CharacterAccents.Pote; // the red bead necklace with its gold pendant
                    break;
                case PedArchetype.SchoolKid:
                {
                    r.Age = rng.Chance(0.7f) ? AgeGroup.Child : AgeGroup.Teen;
                    r.Build = BodyBuild.A;
                    bool skirt = rng.Chance(0.5f);
                    if (skirt) r.Figure = (byte)(r.Age == AgeGroup.Teen ? 1 : 0);
                    r.Hair = skirt ? rng.Of(HairTwoPlaits, HairTwoPlaits, 7, 8, 5) : rng.Of(1, 17, 2, 3);
                    r.Outfit = Slots(OutfitItem.None, OutfitItem.SchoolShirt, skirt ? OutfitItem.Skirt : OutfitItem.Trousers,
                                     rng.Chance(0.65f) ? OutfitItem.LeatherShoes : OutfitItem.Sneakers, OutfitItem.Daypack, c);
                    // One school's colours: shirt, bottom and tie go together.
                    int school = rng.Range(6);
                    r.Outfit[1].Colour = (byte)(school % 2);
                    r.Outfit[1].Pattern = (byte)(school % 3);
                    r.Outfit[2].Colour = (byte)(school % 3);
                    r.Outfit[3].Colour = 0;
                    r.HairColour = 0;
                    if (rng.Chance(0.12f)) r.Accents |= CharacterAccents.Glasses;
                    break;
                }
                case PedArchetype.Porter:
                    r.Age = rng.Chance(0.3f) ? AgeGroup.Elder : AgeGroup.Adult;
                    r.Build = rng.Chance(0.5f) ? BodyBuild.C : BodyBuild.A;
                    r.Hair = rng.Of(1, 2, 3);
                    r.Outfit = Slots(r.Age == AgeGroup.Elder || rng.Chance(0.45f) ? (newar ? OutfitItem.BhadgaunleTopi : OutfitItem.DhakaTopi) : OutfitItem.None,
                                     rng.Chance(0.4f) ? OutfitItem.Vest : rng.Chance(0.5f) ? OutfitItem.Shirt : OutfitItem.TShirt,
                                     rng.Chance(0.45f) ? OutfitItem.Shorts : OutfitItem.RolledTrousers,
                                     rng.Chance(0.75f) ? OutfitItem.Chappal : OutfitItem.Sneakers, OutfitItem.Doko, c);
                    r.Outfit[0].Pattern = (byte)rng.Range(CharacterPalette.DhakaWeaves);
                    r.Beard = rng.Chance(0.4f) ? FacialHair.Moustache : rng.Chance(0.3f) ? FacialHair.Stubble : FacialHair.None;
                    break;
                case PedArchetype.Tourist:
                {
                    r.Skin = (byte)(1 + rng.Range(7));
                    r.HairColour = (byte)rng.Of(8, 9, 3, 2, 4, 0);
                    r.EyeColour = (byte)rng.Range(EyeColourCount);
                    r.Build = rng.Chance(0.45f) ? BodyBuild.D : r.Build;
                    r.Age = rng.Chance(0.2f) ? AgeGroup.Elder : AgeGroup.Adult;
                    bool longHair = rng.Chance(0.45f);
                    if (longHair)
                    {
                        r.Figure = 1;
                        r.Hair = rng.Of(8, 6, 5, 9, 11, 7);
                    }
                    else r.Hair = rng.Of(1, 17, 2, 3, 15);
                    OutfitItem head = rng.Chance(0.35f) ? OutfitItem.SunHat : rng.Chance(0.35f) ? OutfitItem.Cap : OutfitItem.None;
                    r.Outfit = Slots(head, rng.Chance(0.4f) ? OutfitItem.Fleece : rng.Chance(0.15f) ? OutfitItem.Shirt : OutfitItem.TShirt,
                                     rng.Chance(0.55f) ? OutfitItem.TrekTrousers : OutfitItem.Shorts,
                                     rng.Chance(0.6f) ? OutfitItem.TrekBoots : OutfitItem.Sneakers, rng.Chance(0.8f) ? OutfitItem.Daypack : OutfitItem.None, c);
                    if (rng.Chance(0.3f)) r.Accents |= CharacterAccents.Sunglasses;
                    else if (rng.Chance(0.2f)) r.Accents |= CharacterAccents.Glasses;
                    if (rng.Chance(0.4f)) r.Accents |= CharacterAccents.Watch;
                    if (!longHair && rng.Chance(0.4f)) r.Beard = rng.Chance(0.5f) ? FacialHair.ShortBeard : FacialHair.Stubble;
                    break;
                }
                case PedArchetype.Monk:
                    r.Hair = HairShaved;
                    r.Age = rng.Chance(0.25f) ? AgeGroup.Teen : rng.Chance(0.2f) ? AgeGroup.Elder : AgeGroup.Adult;
                    if (rng.Chance(0.1f)) r.Age = AgeGroup.Child; // novices
                    r.Outfit = Slots(OutfitItem.None, OutfitItem.Robe, OutfitItem.None, rng.Chance(0.6f) ? OutfitItem.Chappal : OutfitItem.Sneakers,
                                     OutfitItem.None, 0);
                    r.Outfit[3].Colour = (byte)(r.Outfit[3].Item == OutfitItem.Sneakers ? 7 : 2);
                    if (rng.Chance(0.5f)) r.Accents |= CharacterAccents.Mala;
                    if (rng.Chance(0.15f)) r.Accents |= CharacterAccents.Glasses;
                    if (rng.Chance(0.1f)) r.Accents |= CharacterAccents.PrayerWheel;
                    break;
                case PedArchetype.Sadhu:
                    r.Hair = HairDreadlocks;
                    r.Age = rng.Chance(0.6f) ? AgeGroup.Elder : AgeGroup.Adult;
                    r.Build = BodyBuild.A;
                    r.Skin = (byte)(4 + rng.Range(6));
                    r.HairColour = (byte)(r.Age == AgeGroup.Elder ? 5 : 1);
                    r.Beard = FacialHair.LongBeard;
                    r.Outfit = Slots(OutfitItem.None, OutfitItem.Robe, OutfitItem.None, OutfitItem.Barefoot, OutfitItem.None, 1);
                    r.Accents |= CharacterAccents.Tilak | CharacterAccents.Mala;
                    break;
                case PedArchetype.Farmer:
                    r.Age = rng.Chance(0.35f) ? AgeGroup.Elder : AgeGroup.Adult;
                    if (rng.Chance(0.5f))
                    {
                        r.Figure = 1;
                        r.Hair = rng.Of(7, 9);
                        r.Outfit = Slots(rng.Chance(0.6f) ? OutfitItem.Headscarf : OutfitItem.SunHat, rng.Chance(0.6f) ? OutfitItem.Kurta : OutfitItem.Sari,
                                         OutfitItem.Suruwal, rng.Chance(0.7f) ? OutfitItem.Chappal : OutfitItem.Barefoot, OutfitItem.None, c);
                        if (rng.Chance(0.5f)) r.Accents |= CharacterAccents.Shawl;
                        if (rng.Chance(0.5f)) r.Accents |= CharacterAccents.Earrings;
                        if (rng.Chance(0.4f)) r.Accents |= CharacterAccents.NoseStud;
                        if (rng.Chance(0.4f)) r.Accents |= CharacterAccents.Pote;
                    }
                    else
                    {
                        r.Hair = rng.Of(1, 2, 3);
                        r.Outfit = Slots(rng.Chance(0.5f) ? OutfitItem.SunHat : OutfitItem.DhakaTopi, rng.Chance(0.5f) ? OutfitItem.Shirt : OutfitItem.Vest,
                                         OutfitItem.RolledTrousers, rng.Chance(0.7f) ? OutfitItem.Chappal : OutfitItem.Barefoot, OutfitItem.None, c);
                        r.Outfit[0].Pattern = (byte)rng.Range(CharacterPalette.DhakaWeaves);
                        r.Beard = rng.Chance(0.4f) ? FacialHair.Moustache : FacialHair.Stubble;
                    }
                    break;
                case PedArchetype.TrafficPolice:
                    r.Age = AgeGroup.Adult;
                    r.Hair = rng.Of(1, 2, 17);
                    r.Build = rng.Chance(0.5f) ? BodyBuild.B : BodyBuild.D;
                    r.Outfit = Slots(OutfitItem.PoliceCap, OutfitItem.PoliceUniform, OutfitItem.Trousers, OutfitItem.LeatherShoes, OutfitItem.None, 0);
                    r.Outfit[2].Colour = 0;
                    r.Accents |= CharacterAccents.Gloves;
                    if (rng.Chance(0.3f)) r.Accents |= CharacterAccents.Mask;
                    if (rng.Chance(0.4f)) r.Beard = FacialHair.Moustache;
                    break;
                case PedArchetype.Vendor:
                    r.Age = rng.Chance(0.3f) ? AgeGroup.Elder : AgeGroup.Adult;
                    if (rng.Chance(0.45f))
                    {
                        r.Figure = 1;
                        r.Hair = rng.Of(7, 9, 6);
                        r.Outfit = Slots(OutfitItem.None, rng.Chance(0.7f) ? OutfitItem.Kurta : OutfitItem.Sari, OutfitItem.Suruwal, OutfitItem.Chappal,
                                         OutfitItem.None, c);
                        if (rng.Chance(0.6f)) r.Accents |= CharacterAccents.Bindi;
                        if (rng.Chance(0.5f)) r.Accents |= CharacterAccents.Shawl;
                        if (rng.Chance(0.4f)) r.Accents |= CharacterAccents.NoseStud;
                    }
                    else
                    {
                        r.Outfit = Slots(bazaar && rng.Chance(0.35f) ? (newar ? OutfitItem.BhadgaunleTopi : OutfitItem.DhakaTopi) : rng.Chance(0.15f) ? OutfitItem.Cap : OutfitItem.None,
                                         rng.Chance(0.4f) ? OutfitItem.Shirt : rng.Chance(0.5f) ? OutfitItem.Jacket : OutfitItem.TShirt,
                                         rng.Chance(0.5f) ? OutfitItem.Trousers : OutfitItem.Jeans, rng.Chance(0.7f) ? OutfitItem.Chappal : OutfitItem.Sneakers,
                                         OutfitItem.None, c);
                        r.Outfit[0].Pattern = (byte)rng.Range(CharacterPalette.DhakaWeaves);
                        r.Beard = rng.Chance(0.4f) ? FacialHair.Moustache : FacialHair.None;
                    }
                    break;
                default:
                    CasualLocal(r, ref rng, c, style);
                    break;
            }
            if (r.Age == AgeGroup.Elder)
            {
                r.HairColour = (byte)(rng.Chance(0.6f) ? 6 : 5);
                if (archetype == PedArchetype.Tourist && rng.Chance(0.3f)) r.HairColour = 6;
                if (rng.Chance(0.3f)) r.Accents |= CharacterAccents.Glasses;
            }
            if (r.Age == AgeGroup.Child || r.Age == AgeGroup.Teen)
            {
                r.Beard = FacialHair.None;
                r.Accents &= ~(CharacterAccents.Pote | CharacterAccents.Watch);
            }
            if (r.Figure != 0) r.Beard = FacialHair.None;
            switch (carry)
            {
                case 1: r.Outfit[(int)OutfitSlotKind.Back] = new OutfitSlot(OutfitItem.Doko, (byte)rng.Range(2), (byte)rng.Range(3)); break;
                case 2: r.Outfit[(int)OutfitSlotKind.Back] = new OutfitSlot(OutfitItem.Sack); break;
                case 3: r.Outfit[(int)OutfitSlotKind.Back] = new OutfitSlot(OutfitItem.GasCylinder); break;
                case 4: r.Outfit[(int)OutfitSlotKind.Back] = new OutfitSlot(OutfitItem.BabySling, (byte)rng.Range(4)); break;
                case 5: r.Accents |= CharacterAccents.Umbrella; break;
            }
            if (r.Has(CharacterAccents.Umbrella)) r.Accents &= ~CharacterAccents.PrayerWheel;
            // Most pilgrims come away from the temple with a tika.
            if (pashupati && archetype != PedArchetype.Sadhu && archetype != PedArchetype.TrafficPolice && rng.Chance(0.7f) && !r.Has(CharacterAccents.Bindi))
                r.Accents |= CharacterAccents.Tika;
            if (UsesNamlo(r.Outfit[(int)OutfitSlotKind.Back].Item))
            {
                OutfitItem hat = r.Outfit[0].Item;
                if (hat == OutfitItem.SunHat || hat == OutfitItem.Cap || hat == OutfitItem.PoliceCap)
                    r.Outfit[0] = new OutfitSlot(OutfitItem.None); // the namlo goes over the forehead
            }
            r.Validate();
            return r;
        }

        /// <summary>The everyday wardrobe of the valley today (ref_characters.md §10): jeans, T-shirts, hoodies,
        /// jackets and sneakers on the young; kurta suruwal and leggings on many women; shirts and trousers in the
        /// office districts; more chappals and topis in the old bazaars and the villages.</summary>
        private static void CasualLocal(CharacterRecipe r, ref Pick rng, byte c, StreetStyle style)
        {
            bool bazaar = style == StreetStyle.OldBazaar || style == StreetStyle.NewarTown || style == StreetStyle.Pashupati;
            bool village = style == StreetStyle.Village || style == StreetStyle.Suburb;
            bool office = style == StreetStyle.Office;
            r.Age = rng.Chance(0.25f) ? AgeGroup.Teen : rng.Chance(bazaar ? 0.18f : 0.1f) ? AgeGroup.Elder : AgeGroup.Adult;
            bool woman = rng.Chance(0.45f);
            OutfitItem top, legs, feet, head = OutfitItem.None, back = OutfitItem.None;
            if (woman)
            {
                r.Figure = 1;
                r.Hair = rng.Of(8, 6, 7, 9, 5, 11, 8, 12);
                if (rng.Chance(office ? 0.55f : 0.35f))
                {
                    top = OutfitItem.Kurta;
                    legs = OutfitItem.Suruwal;
                    if (rng.Chance(0.5f)) r.Accents |= CharacterAccents.Shawl;
                }
                else
                {
                    top = rng.Of(OutfitItem.TShirt, OutfitItem.Jacket, OutfitItem.Hoodie, OutfitItem.Shirt, OutfitItem.Fleece, OutfitItem.TShirt);
                    legs = rng.Of(OutfitItem.Jeans, OutfitItem.Jeans, OutfitItem.Joggers, OutfitItem.Trousers);
                }
                float u = rng.Unit();
                feet = u < (bazaar || village ? 0.45f : 0.25f) ? OutfitItem.Chappal : u < 0.8f ? OutfitItem.Sneakers : OutfitItem.LeatherShoes;
                if (rng.Chance(0.5f)) r.Accents |= CharacterAccents.Earrings;
                if (rng.Chance(0.3f)) r.Accents |= CharacterAccents.NoseStud;
                if (rng.Chance(0.25f)) r.Accents |= CharacterAccents.Bindi;
                if (r.Age != AgeGroup.Teen && rng.Chance(0.2f)) r.Accents |= CharacterAccents.Pote;
                if (rng.Chance(0.18f)) back = OutfitItem.Daypack;
            }
            else
            {
                r.Hair = rng.Of(1, 17, 1, 17, 3, 2, 4, 10);
                if (office)
                {
                    top = rng.Chance(0.35f) ? OutfitItem.Blazer : OutfitItem.Shirt;
                    legs = OutfitItem.Trousers;
                    feet = rng.Chance(0.75f) ? OutfitItem.LeatherShoes : OutfitItem.Sneakers;
                }
                else
                {
                    top = rng.Of(OutfitItem.TShirt, OutfitItem.TShirt, OutfitItem.Hoodie, OutfitItem.Jacket, OutfitItem.Shirt, OutfitItem.DhakaJacket, OutfitItem.Jacket);
                    legs = rng.Of(OutfitItem.Jeans, OutfitItem.Jeans, OutfitItem.Jeans, OutfitItem.Joggers, OutfitItem.Trousers, village ? OutfitItem.RolledTrousers : OutfitItem.Jeans);
                    float u = rng.Unit();
                    // Footwear: chappal 40%, sneakers 40%, leather shoes 20% (S §3.3); more chappals in the bazaars and villages.
                    float chappal = bazaar || village ? 0.5f : 0.3f;
                    feet = u < chappal ? OutfitItem.Chappal : u < chappal + 0.45f ? OutfitItem.Sneakers : OutfitItem.LeatherShoes;
                }
                if (rng.Chance(0.12f)) head = OutfitItem.Cap;
                if ((bazaar || village) && rng.Chance(r.Age == AgeGroup.Elder ? 0.6f : 0.12f))
                    head = style == StreetStyle.NewarTown && rng.Chance(0.6f) ? OutfitItem.BhadgaunleTopi : OutfitItem.DhakaTopi;
                if (r.Age != AgeGroup.Teen && rng.Chance(0.35f))
                    r.Beard = rng.Of(FacialHair.Moustache, FacialHair.Stubble, FacialHair.ShortBeard, FacialHair.Goatee, FacialHair.Stubble);
                if (rng.Chance(0.25f)) r.Accents |= CharacterAccents.Watch;
                if (rng.Chance(office ? 0.3f : 0.12f)) back = OutfitItem.Daypack;
            }
            if (r.Age == AgeGroup.Elder && woman && rng.Chance(0.6f))
            {
                top = rng.Chance(0.6f) ? OutfitItem.Sari : OutfitItem.Kurta;
                legs = OutfitItem.Suruwal;
                r.Accents |= CharacterAccents.Shawl;
            }
            r.Outfit = Slots(head, top, legs, feet, back, c);
            r.Outfit[0].Pattern = (byte)rng.Range(CharacterPalette.DhakaWeaves);
            r.Outfit[2].Colour = (byte)rng.Range(8);
            r.Outfit[3].Colour = (byte)rng.Range(8);
            if (rng.Chance(0.15f)) r.Accents |= CharacterAccents.Glasses;
            if (rng.Chance(0.03f)) r.Accents |= CharacterAccents.Mask;
        }

        /// <summary>A tiny deterministic chooser over one hash (no allocation beyond the params arrays).</summary>
        private struct Pick
        {
            private uint _h;

            public Pick(uint h)
            {
                _h = h;
            }

            private uint Next()
            {
                _h = CharMath.Hash(_h, 0x9E3779B9u);
                return _h;
            }

            public float Unit()
            {
                return CharMath.Unit(Next());
            }

            public bool Chance(float p)
            {
                return Unit() < p;
            }

            public int Range(int n)
            {
                return n <= 1 ? 0 : (int)(Next() % (uint)n);
            }

            public byte Of(params int[] values)
            {
                return (byte)values[Range(values.Length)];
            }

            public T Of<T>(params T[] values)
            {
                return values[Range(values.Length)];
            }
        }

        private static OutfitSlot[] Slots(OutfitItem head, OutfitItem torso, OutfitItem legs, OutfitItem feet, OutfitItem back, byte colour)
        {
            return new[]
            {
                new OutfitSlot(head, colour), new OutfitSlot(torso, colour), new OutfitSlot(legs, (byte)(colour / 3)),
                new OutfitSlot(feet, (byte)(colour / 2)), new OutfitSlot(back, colour),
            };
        }

        /// <summary>Which slot an item belongs to.</summary>
        public static OutfitSlotKind SlotOf(OutfitItem item)
        {
            int v = (int)item;
            if (v >= 50) return OutfitSlotKind.Back;
            if (v >= 40) return OutfitSlotKind.Feet;
            if (v >= 30) return OutfitSlotKind.Legs;
            if (v >= 10) return OutfitSlotKind.Torso;
            return OutfitSlotKind.Head;
        }

        /// <summary>True for torso items whose hem covers the legs (no separate legs garment is drawn under a sari, a
        /// robe or a chuba; a kurta reaches the knee and shows the legs below).</summary>
        public static bool IsFullLength(OutfitItem torso)
        {
            return torso == OutfitItem.Sari || torso == OutfitItem.Robe || torso == OutfitItem.Chuba;
        }

        /// <summary>True for back items carried with a namlo strap over the forehead.</summary>
        public static bool UsesNamlo(OutfitItem back)
        {
            return back == OutfitItem.Doko || back == OutfitItem.Sack || back == OutfitItem.GasCylinder;
        }

        /// <summary>Clamps every value into range and fixes items in the wrong slot. Returns this recipe.</summary>
        public CharacterRecipe Validate()
        {
            if ((byte)Build > (byte)BodyBuild.D) Build = BodyBuild.B;
            if (Figure > 1) Figure = 1;
            if ((byte)Age > (byte)AgeGroup.Elder) Age = AgeGroup.Adult;
            if (HeightPct < -6 || HeightPct > 6) HeightPct = 0;
            if (Skin < 1 || Skin > SkinCount) Skin = 5;
            if (Hair < 1 || Hair > HairStyleCount) Hair = 1;
            if (HairColour >= HairColourCount) HairColour = 0;
            if ((byte)Beard > (byte)FacialHair.LongBeard) Beard = FacialHair.None;
            if (EyeColour >= EyeColourCount) EyeColour = 0;
            Accents &= (CharacterAccents)0x7FFF;
            if (HelmetColour >= HelmetColourCount) HelmetColour = 0;
            if (Outfit == null || Outfit.Length != SlotCount)
            {
                OutfitSlot[] fresh = DefaultOutfit();
                if (Outfit != null)
                    for (int i = 0; i < Outfit.Length && i < SlotCount; i++) fresh[i] = Outfit[i];
                Outfit = fresh;
            }
            for (int i = 0; i < SlotCount; i++)
            {
                OutfitSlot s = Outfit[i];
                if (s.Item != OutfitItem.None && (!Enum.IsDefined(typeof(OutfitItem), s.Item) || (int)SlotOf(s.Item) != i))
                    s.Item = OutfitItem.None;
                s.Colour = (byte)(s.Colour % Math.Max(1, CharacterPalette.ColourCount(s.Item)));
                s.Pattern = (byte)(s.Pattern % CharacterPalette.PatternCount(s.Item));
                Outfit[i] = s;
            }
            if (Name == null) Name = "";
            if (Name.Length > 24) Name = Name.Substring(0, 24);
            return this;
        }

        public CharacterRecipe Clone()
        {
            var c = (CharacterRecipe)MemberwiseClone();
            c.Outfit = (OutfitSlot[])Outfit.Clone();
            return c;
        }

        /// <summary>The <c>player.appearance</c> object (version 2; detail-pass keys are written only when set, so the
        /// object stays small).</summary>
        public JsonObject ToJson()
        {
            var outfit = new JsonArray();
            for (int i = 0; i < Outfit.Length; i++)
                outfit.Add(new JsonObject().Set("item", (long)Outfit[i].Item).Set("colour", (long)Outfit[i].Colour)
                                           .Set("pattern", (long)Outfit[i].Pattern));
            var o = new JsonObject()
                .Set("version", 2L)
                .Set("build", (long)Build)
                .Set("skin", (long)Skin)
                .Set("hair", (long)Hair)
                .Set("hairColour", (long)HairColour)
                .Set("helmetColour", (long)HelmetColour)
                .Set("seed", (long)Seed)
                .Set("name", Name ?? "");
            if (Figure != 0) o.Set("figure", (long)Figure);
            if (Age != AgeGroup.Adult) o.Set("age", (long)Age);
            if (HeightPct != 0) o.Set("height", (long)HeightPct);
            if (Beard != FacialHair.None) o.Set("beard", (long)Beard);
            if (EyeColour != 0) o.Set("eyes", (long)EyeColour);
            if (Accents != CharacterAccents.None) o.Set("accents", (long)Accents);
            return o.Set("outfit", outfit);
        }

        /// <summary>Reads a <c>player.appearance</c> object (version 1 saves lack the detail-pass keys and read as their
        /// defaults); missing or bad values fall back to defaults.</summary>
        public static CharacterRecipe FromJson(JsonObject o)
        {
            var r = new CharacterRecipe();
            if (o == null) return r;
            r.Build = (BodyBuild)Byte(o, "build", (int)BodyBuild.B);
            r.Figure = Byte(o, "figure", 0);
            r.Age = (AgeGroup)Byte(o, "age", 0);
            long hp = o.GetLong("height", 0);
            r.HeightPct = hp >= -6 && hp <= 6 ? (sbyte)hp : (sbyte)0;
            r.Skin = Byte(o, "skin", 5);
            r.Hair = Byte(o, "hair", 1);
            r.HairColour = Byte(o, "hairColour", 0);
            r.Beard = (FacialHair)Byte(o, "beard", 0);
            r.EyeColour = Byte(o, "eyes", 0);
            long acc = o.GetLong("accents", 0);
            r.Accents = acc >= 0 && acc <= ushort.MaxValue ? (CharacterAccents)acc : CharacterAccents.None;
            r.HelmetColour = Byte(o, "helmetColour", 0);
            long seed = o.GetLong("seed", 1);
            r.Seed = seed >= 0 && seed <= uint.MaxValue ? (uint)seed : 1u;
            r.Name = o.GetString("name", DefaultName);
            JsonArray a = o.GetArray("outfit");
            if (a != null)
            {
                for (int i = 0; i < a.Count && i < SlotCount; i++)
                {
                    JsonObject s = a[i].AsObject();
                    if (s == null) continue;
                    r.Outfit[i] = new OutfitSlot((OutfitItem)Byte(s, "item", 0), Byte(s, "colour", 0), Byte(s, "pattern", 0));
                }
            }
            return r.Validate();
        }

        private static byte Byte(JsonObject o, string key, int fallback)
        {
            long v = o.GetLong(key, fallback);
            return v >= 0 && v <= 255 ? (byte)v : (byte)fallback;
        }

        /// <summary>True when the two recipes have the same skeleton (build, figure, age and height).</summary>
        public bool SameSkeleton(CharacterRecipe other)
        {
            return other != null && Build == other.Build && Figure == other.Figure && Age == other.Age && HeightPct == other.HeightPct;
        }

        public bool Equals(CharacterRecipe other)
        {
            if (other == null) return false;
            if (!SameSkeleton(other) || Skin != other.Skin || Hair != other.Hair || HairColour != other.HairColour || Beard != other.Beard ||
                EyeColour != other.EyeColour || Accents != other.Accents || HelmetColour != other.HelmetColour || Seed != other.Seed ||
                !string.Equals(Name, other.Name, StringComparison.Ordinal))
                return false;
            for (int i = 0; i < SlotCount; i++)
            {
                OutfitSlot a = Outfit[i], b = other.Outfit[i];
                if (a.Item != b.Item || a.Colour != b.Colour || a.Pattern != b.Pattern) return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CharacterRecipe);
        }

        public override int GetHashCode()
        {
            return (int)ShapeHash();
        }

        /// <summary>A hash of everything that changes the mesh (not the name): the crowd's mesh cache key.</summary>
        public uint ShapeHash()
        {
            uint h = CharMath.Hash((uint)Build | (uint)Skin << 8 | (uint)Hair << 16 | (uint)HairColour << 24, HelmetColour, Seed);
            h = CharMath.Hash(h, (uint)Age | (uint)(byte)HeightPct << 8 | (uint)Beard << 16 | (uint)EyeColour << 24, (uint)Accents | (uint)Figure << 16);
            for (int i = 0; i < Outfit.Length; i++)
                h = CharMath.Hash(h, (uint)Outfit[i].Item | (uint)Outfit[i].Colour << 8 | (uint)Outfit[i].Pattern << 16);
            return h;
        }
    }
}
