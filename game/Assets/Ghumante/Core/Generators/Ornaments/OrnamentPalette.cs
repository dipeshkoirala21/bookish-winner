using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Ornaments
{
    /// <summary>Colours of the roundabout ornaments, read off the reference photos (docs/research/w2/ref_ornaments.md).
    /// All 0xRRGGBBAA with alpha 255.</summary>
    public static class OrnamentPalette
    {
        // Island
        public static readonly uint Lawn = MeshColor.FromHex(0x6FAE4A);
        public static readonly uint LawnDark = MeshColor.FromHex(0x5E9A3E);
        public static readonly uint Soil = MeshColor.FromHex(0x6B4A33);
        public static readonly uint Cobble = MeshColor.FromHex(0x9C978C);
        public static readonly uint KerbStone = MeshColor.FromHex(0xBDB8AE);
        public static readonly uint PaintYellow = MeshColor.FromHex(0xF2C230);
        public static readonly uint PaintBlack = MeshColor.FromHex(0x222222);
        public static readonly uint PaintWhite = MeshColor.FromHex(0xF2F2F0);
        public static readonly uint PathStone = MeshColor.FromHex(0xC7C0B2);
        public static readonly uint BrickPlanter = MeshColor.FromHex(0xA4553A);

        // Planting
        public static readonly uint Hedge = MeshColor.FromHex(0x2F6B2F);
        public static readonly uint HedgeLight = MeshColor.FromHex(0x3F8A3A);
        public static readonly uint ShrubGolden = MeshColor.FromHex(0x9DB33A);
        public static readonly uint MarigoldOrange = MeshColor.FromHex(0xF28C28);
        public static readonly uint MarigoldYellow = MeshColor.FromHex(0xF6C21C);
        public static readonly uint RoseRed = MeshColor.FromHex(0xC8233A);
        public static readonly uint RosePink = MeshColor.FromHex(0xE87AA0);
        public static readonly uint FlowerWhite = MeshColor.FromHex(0xF5F3EA);
        public static readonly uint Bougainvillea = MeshColor.FromHex(0xA0307A);
        public static readonly uint LeafGreen = MeshColor.FromHex(0x3E8E3A);
        public static readonly uint TreeCanopy = MeshColor.FromHex(0x3C7A34);
        public static readonly uint Conifer = MeshColor.FromHex(0x2C5E34);
        public static readonly uint Palm = MeshColor.FromHex(0x4E8F3A);
        public static readonly uint Bark = MeshColor.FromHex(0x6A5040);
        public static readonly uint Terracotta = MeshColor.FromHex(0xA0472E);

        // Statues
        public static readonly uint Bronze = MeshColor.FromHex(0x4D4B3A);
        public static readonly uint BronzeDark = MeshColor.FromHex(0x3C3A2E);
        public static readonly uint Verdigris = MeshColor.FromHex(0x3D7A5C);
        public static readonly uint Marble = MeshColor.FromHex(0xECEAE3);
        public static readonly uint BlackStone = MeshColor.FromHex(0x2E2E30);
        public static readonly uint GiltStatue = MeshColor.FromHex(0xD9A62E);
        public static readonly uint PlinthCream = MeshColor.FromHex(0xE6DCCB);
        public static readonly uint PlinthPink = MeshColor.FromHex(0xE4D3C2);
        public static readonly uint PlinthGrey = MeshColor.FromHex(0xD6D2C8);
        public static readonly uint PlinthWhite = MeshColor.FromHex(0xE8E4DA);
        public static readonly uint StepGrey = MeshColor.FromHex(0xB4AFA5);
        public static readonly uint StepRound = MeshColor.FromHex(0xC9C3B6);
        public static readonly uint Plaque = MeshColor.FromHex(0x2A2A2A);
        public static readonly uint PlaqueGilt = MeshColor.FromHex(0xB8963E);
        public static readonly uint SpireStone = MeshColor.FromHex(0x9A968E);
        public static readonly uint CollarRed = MeshColor.FromHex(0xC0392B);
        public static readonly uint CollarYellow = MeshColor.FromHex(0xE8B923);
        public static readonly uint GarlandRed = MeshColor.FromHex(0xC8233A);
        public static readonly uint ScarfRed = MeshColor.FromHex(0xD23A2F);

        // Shahid Gate
        public static readonly uint GateStone = MeshColor.FromHex(0xCBB99D);
        public static readonly uint GateStoneLight = MeshColor.FromHex(0xDCCFB8);
        public static readonly uint GateJoint = MeshColor.FromHex(0x8C8579);
        public static readonly uint NicheDark = MeshColor.FromHex(0x2B2620);
        public static readonly uint WallCream = MeshColor.FromHex(0xE3DCCB);
        public static readonly uint IronBlack = MeshColor.FromHex(0x1E1F22);

        // Flag of Nepal
        public static readonly uint FlagCrimson = MeshColor.FromHex(0xDC143C);
        public static readonly uint FlagBlue = MeshColor.FromHex(0x003893);
        public static readonly uint FlagWhite = MeshColor.FromHex(0xFFFFFF);

        // Maitighar mandala
        public static readonly uint MandalaTeal = MeshColor.FromHex(0x2E8B6E);
        public static readonly uint MandalaEdge = MeshColor.FromHex(0xF2E6A0);
        public static readonly uint MandalaRed = MeshColor.FromHex(0xC8323A);
        public static readonly uint MandalaBlue = MeshColor.FromHex(0x2F5DA8);
        public static readonly uint MandalaYellow = MeshColor.FromHex(0xF2C230);
        public static readonly uint MandalaOrange = MeshColor.FromHex(0xE8742A);
        public static readonly uint MandalaWhite = MeshColor.FromHex(0xF4EFE0);
        public static readonly uint MandalaGreen = MeshColor.FromHex(0x3E9E5A);
        public static readonly uint MandalaBlack = MeshColor.FromHex(0x2A2A2E);
        public static readonly uint Gilt = MeshColor.FromHex(0xD9A62E);
        public static readonly uint ColumnWhite = MeshColor.FromHex(0xF2F0E8);
        public static readonly uint ColumnGreen = MeshColor.FromHex(0x2E8B57);
        public static readonly uint TealPost = MeshColor.FromHex(0x3FA39B);

        // Furniture
        public static readonly uint PoleGrey = MeshColor.FromHex(0x8C9196);
        public static readonly uint PoleGreen = MeshColor.FromHex(0x2E5E46);
        public static readonly uint SolarPanel = MeshColor.FromHex(0x1E2A4A);
        public static readonly uint LampHead = MeshColor.FromHex(0xD8DCE0);
        public static readonly uint LampGlass = MeshColor.FromHex(0xFFF4D6);
        public static readonly uint Steel = MeshColor.FromHex(0xB9BEC4);
        public static readonly uint PodiumWhite = MeshColor.FromHex(0xF4F2EC);
        public static readonly uint PoliceBlue = MeshColor.FromHex(0x2D6FB7);
        public static readonly uint PoliceSign = MeshColor.FromHex(0x7A1E22);
        public static readonly uint UmbrellaRed = MeshColor.FromHex(0xC9433A);
        public static readonly uint SignBlue = MeshColor.FromHex(0x1F5FAF);
        public static readonly uint Water = MeshColor.FromHex(0x5FA8C8);
        public static readonly uint WaterJet = MeshColor.FromHex(0xD6F0FA);
        public static readonly uint BasinRim = MeshColor.FromHex(0xE3DCCB);
        public static readonly uint ClockFace = MeshColor.FromHex(0xF7F3E6);
        public static readonly uint TowerWall = MeshColor.FromHex(0xE9E1CF);
        public static readonly uint TowerTrim = MeshColor.FromHex(0xA4553A);
    }
}
