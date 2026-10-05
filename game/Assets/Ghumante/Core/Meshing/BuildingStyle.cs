using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Building heights, default roofs and colours (ASSET_MANIFEST.md 1.9 tokens, 4.2 archetypes, 4.4 roofs, 4.5
    /// materials). Colour variation is deterministic: it comes from the record's <see cref="BuildingRecord.Seed"/>
    /// only, so a building looks the same on every visit and device (ARCHITECTURE 7.2).
    /// </summary>
    public static class BuildingStyle
    {
        /// <summary>Storey height used when a record has no height (pipeline buildings.LEVEL_HEIGHT_M).</summary>
        public const float LevelHeightM = 3f;

        public static readonly uint Gold = MeshColor.FromHex(0xE9B23B);
        public static readonly uint Whitewash = MeshColor.FromHex(0xF6F0E2);

        /// <summary>Height the pipeline reserves for a roof shape on top of the storeys
        /// (buildings.ROOF_ALLOWANCE_M): the total height of a record includes it.</summary>
        public static float RoofAllowanceM(RoofShape s)
        {
            switch (s)
            {
                case RoofShape.Flat: return 0.5f;
                case RoofShape.Skillion: return 1.0f;
                case RoofShape.Pyramidal:
                case RoofShape.Cone:
                case RoofShape.Round: return 2.0f;
                case RoofShape.Dome:
                case RoofShape.Onion: return 3.0f;
                case RoofShape.Pagoda: return 4.0f;
                case RoofShape.Shikhara: return 6.0f;
                default: return 1.5f;
            }
        }

        /// <summary>Roof for <see cref="RoofShape.Unknown"/> (ASSET_MANIFEST 4.4 <c>ghm_roof_default</c>).</summary>
        public static RoofShape DefaultRoof(BuildingArchetype a)
        {
            switch (a)
            {
                case BuildingArchetype.Newar:
                case BuildingArchetype.HillVillage:
                case BuildingArchetype.SherpaHimalayan:
                case BuildingArchetype.Church:
                case BuildingArchetype.Industrial:
                case BuildingArchetype.Hut:
                case BuildingArchetype.Teahouse: return RoofShape.Gabled;
                case BuildingArchetype.Terai: return RoofShape.Hipped;
                case BuildingArchetype.TemplePagoda: return RoofShape.Pagoda;
                case BuildingArchetype.TempleShikhara: return RoofShape.Shikhara;
                case BuildingArchetype.Stupa:
                case BuildingArchetype.Chorten:
                case BuildingArchetype.Mosque: return RoofShape.Dome;
                case BuildingArchetype.Shrine: return RoofShape.Pyramidal;
                case BuildingArchetype.Greenhouse: return RoofShape.Round;
                default: return RoofShape.Flat;
            }
        }

        /// <summary>Total height in metres including the roof: the record's height, or storeys × 3 m plus the roof
        /// allowance (as the pipeline derives it), clamped to [2, 300].</summary>
        public static float HeightM(BuildingRecord b)
        {
            float h = b.HeightCm > 0
                ? (float)(b.HeightCm / 100.0)
                : (float)(b.MinHeightCm / 100.0) + (b.Levels < 1 ? 1 : b.Levels) * LevelHeightM + RoofAllowanceM(ResolvedShape(b));
            return h < 2f ? 2f : h > 300f ? 300f : h;
        }

        /// <summary>The record's roof shape, or the archetype default when unknown.</summary>
        public static RoofShape ResolvedShape(BuildingRecord b)
        {
            return b.RoofShape == RoofShape.Unknown ? DefaultRoof(b.Archetype) : b.RoofShape;
        }

        private static readonly uint[] Brick = Hex(0xB4543A, 0xA84E36, 0xBF5E42, 0x9E4A33);
        private static readonly uint[] Stone = Hex(0x9C968C, 0xA8A196, 0x8F897F);
        private static readonly uint[] Mud = Hex(0xA97C52, 0xB3532E, 0xCF8A3A);
        private static readonly uint[] Wood = Hex(0x6A3E22, 0x7A4A2A);
        private static readonly uint[] Bamboo = Hex(0xC8A86A);
        private static readonly uint[] MetalWall = Hex(0x9AA0A6, 0x3D7CC9, 0x3FA35C);
        private static readonly uint[] Glass = Hex(0x8FC6E0, 0x7FB8D8);

        private static readonly uint[] GenericPlaster = Hex(0xD8CFC0, 0xCFC6B4, 0xE0D6C6, 0xC8BFAE);
        private static readonly uint[] NewarPlaster = Hex(0xE3D2B8, 0xD9C3A5, 0xF0E2C8);
        private static readonly uint[] ModernPaint = Hex(0xF49AC1, 0x3CC9C0, 0xB6E05A, 0xFFD95A, 0x7FC8F8, 0xB79CE8,
                                                         0xF3EBDD, 0xEADFCB, 0xD9D4CC);
        private static readonly uint[] HillWash = Hex(0xB3532E, 0xCF8A3A, 0xF6F0E2);
        private static readonly uint[] TeraiWash = Hex(0xA97C52, 0xF6F0E2, 0xD8B98A);
        private static readonly uint[] SherpaWash = Hex(0xF6F0E2, 0x9C968C);
        private static readonly uint[] TransWash = Hex(0xF6F0E2, 0xD8C8A8, 0xA97C52);
        private static readonly uint[] ShikharaWash = Hex(0xF6F0E2, 0xE8C9A0, 0x9C968C);
        private static readonly uint[] WhiteOnly = Hex(0xF6F0E2);
        private static readonly uint[] GompaWash = Hex(0xF6F0E2, 0xCF8A3A, 0x8E2F2A);
        private static readonly uint[] ShrineWash = Hex(0xC8432E, 0xB4543A, 0xF6F0E2);
        private static readonly uint[] MosqueWash = Hex(0xF6F0E2, 0xE6EFE6);
        private static readonly uint[] ChurchWash = Hex(0xF6F0E2, 0xEADFCB);
        private static readonly uint[] IndustrialWall = Hex(0xA8A39A, 0xB4543A, 0xBDB8AE);
        private static readonly uint[] InstitutionalWash = Hex(0xF3E3C0, 0x9CC3E8, 0xF6F0E2, 0xE8D7A8);
        private static readonly uint[] HutWall = Hex(0x8A6A45, 0xA97C52);
        private static readonly uint[] Plastic = Hex(0xE6EEF0);
        private static readonly uint[] TeahouseWall = Hex(0xF6F0E2, 0x9C968C, 0x3D7CC9);

        private static readonly uint[] RoofConcrete = Hex(0xBDB8AE, 0xC9C4BA, 0xB0ABA2);
        private static readonly uint[] RoofMetal = Hex(0x3D7CC9, 0x3FA35C, 0xC9433A, 0xA2603A, 0x9AA0A6);
        private static readonly uint[] RoofTiles = Hex(0x9E4630, 0xA9503A, 0x8F3F2B);
        private static readonly uint[] RoofSlate = Hex(0x5E646B);
        private static readonly uint[] RoofThatch = Hex(0xC9A35A);
        private static readonly uint[] RoofWood = Hex(0x6A3E22);
        private static readonly uint[] RoofGlass = Hex(0x9CC9E0);
        private static readonly uint[] RoofMud = Hex(0xA97C52);
        private static readonly uint[] RoofStone = Hex(0x9C968C);

        /// <summary>Wall colour: the wall material's swatch, or the archetype's plaster palette, picked and
        /// brightened ±7 % by the seed.</summary>
        public static uint WallRgba(BuildingRecord b)
        {
            uint[] pal;
            switch (b.WallMaterial)
            {
                case WallMaterial.Brick: pal = Brick; break;
                case WallMaterial.Stone: pal = Stone; break;
                case WallMaterial.Mud: pal = Mud; break;
                case WallMaterial.Wood: pal = Wood; break;
                case WallMaterial.Bamboo: pal = Bamboo; break;
                case WallMaterial.Metal: pal = MetalWall; break;
                case WallMaterial.Glass: pal = Glass; break;
                default: pal = ArchetypeWalls(b.Archetype); break;
            }
            if (b.Archetype == BuildingArchetype.Stupa || b.Archetype == BuildingArchetype.Chorten) pal = WhiteOnly;
            return Pick(pal, b.Seed, 3, 11);
        }

        /// <summary>Roof colour for the resolved shape: the roof material's swatch (unknown: concrete for flat
        /// roofs, clay tiles for Newar and temples, painted metal otherwise), picked and varied by the seed.</summary>
        public static uint RoofRgba(BuildingRecord b, RoofShape shape)
        {
            uint[] pal;
            switch (b.RoofMaterial)
            {
                case RoofMaterial.Concrete: pal = RoofConcrete; break;
                case RoofMaterial.Metal: pal = RoofMetal; break;
                case RoofMaterial.Tiles: pal = RoofTiles; break;
                case RoofMaterial.Slate: pal = RoofSlate; break;
                case RoofMaterial.Thatch: pal = RoofThatch; break;
                case RoofMaterial.Wood: pal = RoofWood; break;
                case RoofMaterial.Glass: pal = RoofGlass; break;
                case RoofMaterial.Mud: pal = RoofMud; break;
                case RoofMaterial.Stone: pal = RoofStone; break;
                default:
                    if (shape == RoofShape.Flat) pal = RoofConcrete;
                    else if (b.Archetype == BuildingArchetype.Newar || b.Archetype == BuildingArchetype.TemplePagoda) pal = RoofTiles;
                    else pal = RoofMetal;
                    break;
            }
            if (b.Archetype == BuildingArchetype.Stupa || b.Archetype == BuildingArchetype.Chorten) pal = WhiteOnly;
            return Pick(pal, b.Seed, 19, 24);
        }

        private static uint[] ArchetypeWalls(BuildingArchetype a)
        {
            switch (a)
            {
                case BuildingArchetype.Newar: return NewarPlaster;
                case BuildingArchetype.ModernUrban: return ModernPaint;
                case BuildingArchetype.HillVillage: return HillWash;
                case BuildingArchetype.Terai: return TeraiWash;
                case BuildingArchetype.SherpaHimalayan: return SherpaWash;
                case BuildingArchetype.TransHimalayan: return TransWash;
                case BuildingArchetype.TemplePagoda: return Brick;
                case BuildingArchetype.TempleShikhara: return ShikharaWash;
                case BuildingArchetype.Stupa:
                case BuildingArchetype.Chorten: return WhiteOnly;
                case BuildingArchetype.Gompa: return GompaWash;
                case BuildingArchetype.Shrine: return ShrineWash;
                case BuildingArchetype.Mosque: return MosqueWash;
                case BuildingArchetype.Church: return ChurchWash;
                case BuildingArchetype.Industrial: return IndustrialWall;
                case BuildingArchetype.Institutional: return InstitutionalWash;
                case BuildingArchetype.Hut: return HutWall;
                case BuildingArchetype.Greenhouse: return Plastic;
                case BuildingArchetype.Teahouse: return TeahouseWall;
                default: return GenericPlaster;
            }
        }

        private static uint Pick(uint[] pal, uint seed, int pickShift, int shadeShift)
        {
            uint c = pal[(int)((seed >> pickShift) % (uint)pal.Length)];
            float f = 0.93f + (seed >> shadeShift & 0xFF) / 255f * 0.14f;
            return MeshColor.Scale(c, f);
        }

        private static uint[] Hex(params uint[] rgb)
        {
            var a = new uint[rgb.Length];
            for (int i = 0; i < rgb.Length; i++) a[i] = MeshColor.FromHex(rgb[i]);
            return a;
        }
    }
}
