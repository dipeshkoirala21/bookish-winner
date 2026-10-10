using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>Where a generator builds (W2_DESIGN 10.3): tile-local metres, the ground height and the main door's
    /// compass bearing (the outward normal of the door, clockwise from north).</summary>
    public struct GenFrame
    {
        public double X, Z;
        public float GroundY, YawDeg;

        public GenFrame(double x, double z, float groundY, float yawDeg)
        {
            X = x;
            Z = z;
            GroundY = groundY;
            YawDeg = yawDeg;
        }

        /// <summary>The kit frame: +W points through the main door, U across it, origin at ground level.</summary>
        public KitFrame Kit
        {
            get { return KitFrame.FromYaw(X, GroundY, Z, YawDeg); }
        }
    }

    /// <summary>Roof finish of a pagoda (W2_DESIGN 3.2).</summary>
    public enum RoofFinish : byte
    {
        Tile = 0,
        GiltTop = 1,
        GiltAll = 2,
    }

    /// <summary>Guardian figures on the main stair, bottom to top.</summary>
    public enum GuardianSet : byte
    {
        None = 0,

        /// <summary>One pair of lions at the foot of the stair (the default).</summary>
        Lions = 1,

        /// <summary>Nyatapola: wrestlers, elephants, lions, griffins, goddesses (one pair per plinth level).</summary>
        Nyatapola = 2,

        /// <summary>The wrestler pair (Jaya and Patta) of Dattatreya.</summary>
        Wrestlers = 3,

        /// <summary>Stone elephants (Vishwanath's east door).</summary>
        Elephants = 4,
    }

    /// <summary>What a generator built, for checks (V4) and for presenters.</summary>
    public sealed class SacredStats
    {
        public int Tiers, PlinthLevels, Bells, Struts, Pinnacles, Rings, Terraces, Steps, Windows, Niches, FlagLines;

        /// <summary>Highest point above the frame's ground (the finial tip).</summary>
        public float TopM;

        /// <summary>Height of the top plinth or terrace above the ground.</summary>
        public float PlinthTopM;

        /// <summary>Main door bearing (degrees clockwise from north) as built.</summary>
        public float DoorYawDeg;

        /// <summary>Sanctum volume in frame space (u, w half extents and v range); empty when there is none.</summary>
        public float SanctumHalfU, SanctumHalfW, SanctumV0, SanctumV1;

        /// <summary>Foot of a hero's great stair (tile-local metres and the absolute height of its lowest tread;
        /// Swayambhu), 0 when there is none.</summary>
        public float StairFootX, StairFootZ, StairFootY;

        public void Clear()
        {
            Tiers = PlinthLevels = Bells = Struts = Pinnacles = Rings = Terraces = Steps = Windows = Niches = FlagLines = 0;
            TopM = PlinthTopM = DoorYawDeg = 0f;
            SanctumHalfU = SanctumHalfW = SanctumV0 = SanctumV1 = 0f;
            StairFootX = StairFootZ = StairFootY = 0f;
        }
    }

    /// <summary>Cartoon palette of the sacred generators (temples.md 2.0, W2_DESIGN 2.5).</summary>
    public static class SacredPalette
    {
        public static readonly uint BrickDachi = MeshColor.FromHex(0xA0472E);
        public static readonly uint BrickPlinth = MeshColor.FromHex(0xB5603E);
        public static readonly uint Ochre = MeshColor.FromHex(0x9A5A32);
        public static readonly uint Tile = MeshColor.FromHex(0x72352A);
        public static readonly uint TileLight = MeshColor.FromHex(0x8E3A2A);
        public static readonly uint WoodCarved = MeshColor.FromHex(0x3E2416);
        public static readonly uint WoodRed = MeshColor.FromHex(0xB3261E);
        public static readonly uint Gilt = MeshColor.FromHex(0xD9A62E);
        public static readonly uint GiltHi = MeshColor.FromHex(0xF2CC5B);
        public static readonly uint GiltLo = MeshColor.FromHex(0x9C7317);
        public static readonly uint Silver = MeshColor.FromHex(0xC9CED6);
        public static readonly uint StoneGrey = MeshColor.FromHex(0x8E8A80);
        public static readonly uint StoneBuff = MeshColor.FromHex(0xB9A684);
        public static readonly uint Whitewash = MeshColor.FromHex(0xF6F0E2);
        public static readonly uint Cream = MeshColor.FromHex(0xECCCA0);
        public static readonly uint Saffron = MeshColor.FromHex(0xF2A93B);
        public static readonly uint Sindoor = MeshColor.FromHex(0xE0412B);
        public static readonly uint Marigold = MeshColor.FromHex(0xF6A21B);
        public static readonly uint EyesInk = MeshColor.FromHex(0x1B1B24);
        public static readonly uint EyesBlue = MeshColor.FromHex(0x2F5DA8);
        public static readonly uint Terracotta = MeshColor.FromHex(0xB5603E);
        public static readonly uint Lime = MeshColor.FromHex(0xF4EFE0);

        /// <summary>The closed sanctum door: an opaque dark quad (never an interior).</summary>
        public static readonly uint SanctumDark = MeshColor.FromHex(0x14100C);

        /// <summary>The lamp glow on a sanctum door.</summary>
        public static readonly uint LampGlow = MeshColor.FromHex(0xFFC860);

        /// <summary>Prayer flags in the fixed order blue, white, red, green, yellow (sky, air, fire, water, earth).</summary>
        public static readonly uint[] Flags =
        {
            MeshColor.FromHex(0x2E6FD8), MeshColor.FromHex(0xFFFFFF), MeshColor.FromHex(0xD93A2B), MeshColor.FromHex(0x2E9E4F),
            MeshColor.FromHex(0xF2C230),
        };

        /// <summary>Dhyani Buddha colours of the chaitya niches E, S, W, N (Akshobhya blue, Ratnasambhava yellow,
        /// Amitabha red, Amoghasiddhi green).</summary>
        public static readonly uint[] NicheBuddha =
        {
            MeshColor.FromHex(0x2F5DA8), MeshColor.FromHex(0xE8B923), MeshColor.FromHex(0xC8282E), MeshColor.FromHex(0x2E8B3F),
        };
    }
}
