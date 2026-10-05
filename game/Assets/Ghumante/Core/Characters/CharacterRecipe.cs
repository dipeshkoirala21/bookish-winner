using System;
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
    /// Outfit items as parameter recipes on the same primitives (P §2.7); no brands, no logos. Append-only (saved by
    /// value in <c>player.appearance</c>).
    /// </summary>
    public enum OutfitItem : byte
    {
        None = 0,

        // Head
        DhakaTopi = 1,
        BhadgaunleTopi = 2,
        Beanie = 3,
        SunHat = 4,

        // Torso
        TShirt = 10,
        Hoodie = 11,
        DhakaJacket = 12,
        Kurta = 13,
        Daura = 14,
        Sari = 15,
        Robe = 16,
        Uniform = 17,

        // Legs
        Jeans = 30,
        Joggers = 31,
        Shorts = 32,
        Suruwal = 33,

        // Feet
        Sneakers = 40,
        Chappal = 41,
        TrekBoots = 42,
        Barefoot = 43,

        // Back
        Daypack = 50,
        Doko = 51,
    }

    /// <summary>One worn item: what it is, its colour (an index into the item's palette, <see cref="CharacterPalette"/>)
    /// and a pattern colour where the item has one (the dhaka lattice).</summary>
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
    /// A character as data (W2_DESIGN 10.3, P §2): build, skin swatch (numbered 1–10, never named after a group), one of
    /// 12 hairstyles in one of 8 colours, the helmet colour auto-equipped on two-wheelers, one outfit item per slot and a
    /// seed for small variation. <see cref="HumanoidMesher"/> builds the mesh from it, for the player and for NPC bodies.
    /// Saved as the <c>player.appearance</c> object (<see cref="ToJson"/>, about 150 bytes). Values out of range are
    /// clamped by <see cref="Validate"/>, so an old or edited save never breaks the mesher.
    /// </summary>
    public sealed class CharacterRecipe : IEquatable<CharacterRecipe>
    {
        public const int SkinCount = 10, HairStyleCount = 12, HairColourCount = 8, HelmetColourCount = 8, SlotCount = 5;

        /// <summary>The player's default name, "wanderer" (P §2.2).</summary>
        public const string DefaultName = "Ghumante";

        public BodyBuild Build = BodyBuild.B;

        /// <summary>Skin swatch number, 1..10.</summary>
        public byte Skin = 5;

        /// <summary>Hairstyle h01..h12 as 1..12 (P §2.6).</summary>
        public byte Hair = 1;

        /// <summary>Hair colour index 0..7 (black, soft black, dark brown, brown, auburn, grey, white, teal).</summary>
        public byte HairColour;

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
        /// never #1 (P §2.5: the picker opens on a random swatch, never on the first), and a random hairstyle and colour.
        /// </summary>
        public static CharacterRecipe NewPlayer(uint seed)
        {
            uint h = CharMath.Hash(seed, 0x504C4159u);
            var r = new CharacterRecipe
            {
                Seed = seed,
                Skin = (byte)(2 + h % (SkinCount - 1)),
                Hair = (byte)(1 + (h >> 8) % HairStyleCount),
                HairColour = (byte)((h >> 16) % 5),
                HelmetColour = (byte)((h >> 20) % HelmetColourCount),
            };
            return r;
        }

        /// <summary>
        /// An NPC body for a pedestrian archetype (S §3.2; the crowd uses the same generator as the player, W2_DESIGN 10.3):
        /// outfit, hair and build follow the archetype, colours follow <paramref name="tint"/> and <paramref name="seed"/>.
        /// Monks get a robe and a shaved head, porters a doko, police a uniform; nobody gets a brand.
        /// </summary>
        public static CharacterRecipe ForPedestrian(PedArchetype archetype, int tint, uint seed)
        {
            uint h = CharMath.Hash(seed, (uint)archetype, (uint)tint);
            var r = new CharacterRecipe
            {
                Seed = seed,
                Build = (BodyBuild)(h % 4),
                Skin = (byte)(1 + (h >> 4) % SkinCount),
                Hair = (byte)(1 + (h >> 9) % HairStyleCount),
                HairColour = (byte)((h >> 13) % 6),
                HelmetColour = (byte)((h >> 17) % HelmetColourCount),
                Name = "",
            };
            byte c = (byte)(tint & 0x7F);
            switch (archetype)
            {
                case PedArchetype.KurtaSari:
                    r.Outfit = ((h >> 21) & 1) == 0
                        ? Slots(OutfitItem.None, OutfitItem.Sari, OutfitItem.None, OutfitItem.Chappal, OutfitItem.None, c)
                        : Slots(OutfitItem.None, OutfitItem.Kurta, OutfitItem.Suruwal, OutfitItem.Chappal, OutfitItem.None, c);
                    break;
                case PedArchetype.DauraSuruwal:
                    r.Outfit = Slots(OutfitItem.DhakaTopi, OutfitItem.Daura, OutfitItem.Suruwal, OutfitItem.Chappal, OutfitItem.None, c);
                    break;
                case PedArchetype.Hakupatasi:
                    r.Outfit = Slots(OutfitItem.None, OutfitItem.Sari, OutfitItem.None, OutfitItem.Chappal, OutfitItem.None, 7);
                    break;
                case PedArchetype.SchoolKid:
                    r.Build = BodyBuild.A;
                    r.Outfit = Slots(OutfitItem.None, OutfitItem.Uniform, OutfitItem.Joggers, OutfitItem.Sneakers, OutfitItem.Daypack, c);
                    break;
                case PedArchetype.Porter:
                    r.Build = BodyBuild.C;
                    r.Outfit = Slots(OutfitItem.DhakaTopi, OutfitItem.TShirt, OutfitItem.Shorts, OutfitItem.Chappal, OutfitItem.Doko, c);
                    break;
                case PedArchetype.Tourist:
                    r.Outfit = Slots(OutfitItem.SunHat, OutfitItem.TShirt, OutfitItem.Shorts, OutfitItem.TrekBoots, OutfitItem.Daypack, c);
                    break;
                case PedArchetype.Monk:
                    r.Hair = 2;
                    r.Outfit = Slots(OutfitItem.None, OutfitItem.Robe, OutfitItem.None, OutfitItem.Chappal, OutfitItem.None, 0);
                    break;
                case PedArchetype.Sadhu:
                    r.Outfit = Slots(OutfitItem.None, OutfitItem.Robe, OutfitItem.None, OutfitItem.Barefoot, OutfitItem.None, 1);
                    break;
                case PedArchetype.Farmer:
                    r.Outfit = Slots(OutfitItem.SunHat, OutfitItem.Kurta, OutfitItem.Suruwal, OutfitItem.Chappal, OutfitItem.Doko, c);
                    break;
                case PedArchetype.TrafficPolice:
                    r.Outfit = Slots(OutfitItem.None, OutfitItem.Uniform, OutfitItem.Jeans, OutfitItem.Sneakers, OutfitItem.None, 3);
                    break;
                case PedArchetype.Vendor:
                    r.Outfit = Slots(OutfitItem.None, OutfitItem.Kurta, OutfitItem.Joggers, OutfitItem.Chappal, OutfitItem.None, c);
                    break;
                default:
                    r.Outfit = ((h >> 22) & 1) == 0
                        ? Slots(OutfitItem.None, OutfitItem.TShirt, OutfitItem.Jeans, OutfitItem.Sneakers, OutfitItem.None, c)
                        : Slots(OutfitItem.None, OutfitItem.Hoodie, OutfitItem.Joggers, OutfitItem.Sneakers, OutfitItem.None, c);
                    break;
            }
            r.Validate();
            return r;
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

        /// <summary>True for torso items whose hem covers the legs (no separate legs garment is drawn under a sari or a
        /// robe; a kurta reaches the knee and shows the legs below).</summary>
        public static bool IsFullLength(OutfitItem torso)
        {
            return torso == OutfitItem.Sari || torso == OutfitItem.Robe;
        }

        /// <summary>Clamps every value into range and fixes items in the wrong slot. Returns this recipe.</summary>
        public CharacterRecipe Validate()
        {
            if ((byte)Build > (byte)BodyBuild.D) Build = BodyBuild.B;
            if (Skin < 1 || Skin > SkinCount) Skin = 5;
            if (Hair < 1 || Hair > HairStyleCount) Hair = 1;
            if (HairColour >= HairColourCount) HairColour = 0;
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
                s.Pattern = (byte)(s.Pattern % CharacterPalette.TopiPatterns.Length);
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

        /// <summary>The <c>player.appearance</c> object.</summary>
        public JsonObject ToJson()
        {
            var outfit = new JsonArray();
            for (int i = 0; i < Outfit.Length; i++)
                outfit.Add(new JsonObject().Set("item", (long)Outfit[i].Item).Set("colour", (long)Outfit[i].Colour)
                                           .Set("pattern", (long)Outfit[i].Pattern));
            return new JsonObject()
                .Set("version", 1L)
                .Set("build", (long)Build)
                .Set("skin", (long)Skin)
                .Set("hair", (long)Hair)
                .Set("hairColour", (long)HairColour)
                .Set("helmetColour", (long)HelmetColour)
                .Set("seed", (long)Seed)
                .Set("name", Name ?? "")
                .Set("outfit", outfit);
        }

        /// <summary>Reads a <c>player.appearance</c> object; missing or bad values fall back to defaults.</summary>
        public static CharacterRecipe FromJson(JsonObject o)
        {
            var r = new CharacterRecipe();
            if (o == null) return r;
            r.Build = (BodyBuild)Byte(o, "build", (int)BodyBuild.B);
            r.Skin = Byte(o, "skin", 5);
            r.Hair = Byte(o, "hair", 1);
            r.HairColour = Byte(o, "hairColour", 0);
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

        public bool Equals(CharacterRecipe other)
        {
            if (other == null) return false;
            if (Build != other.Build || Skin != other.Skin || Hair != other.Hair || HairColour != other.HairColour ||
                HelmetColour != other.HelmetColour || Seed != other.Seed || !string.Equals(Name, other.Name, StringComparison.Ordinal))
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
            uint h = CharMath.Hash((uint)Build | (uint)Skin << 8 | (uint)Hair << 16 | (uint)HairColour << 24, HelmetColour, Seed);
            for (int i = 0; i < Outfit.Length; i++)
                h = CharMath.Hash(h, (uint)Outfit[i].Item | (uint)Outfit[i].Colour << 8 | (uint)Outfit[i].Pattern << 16);
            return (int)h;
        }
    }
}
