using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Meshing
{
    /// <summary>LOD band of a building (W2_DESIGN 2.4).</summary>
    public enum BuildingBand : byte
    {
        /// <summary>Full facade grammar, per 64 m detail cell (<see cref="BuildingDetailMesher"/>).</summary>
        B0KitLite = 0,

        /// <summary>W1 extrusion with a real roof, floor bands and window rows (default).</summary>
        B1Styled = 1,

        /// <summary>Oriented-box prism, flat or two-triangle roof, shared walls culled.</summary>
        B2Prism = 2,

        /// <summary>16 m city-block boxes from rasterised footprints.</summary>
        B3Block = 3,
    }

    /// <summary>
    /// Seeded priors and palette of one style profile (W2_DESIGN 2.2, 2.5). Shares are fractions (0..1).
    /// </summary>
    public sealed class StyleParams
    {
        public StyleProfile Profile;

        /// <summary>Storey shares for 2, 3, 4, 5 and 6+ storeys (W2_DESIGN 2.2).</summary>
        public float[] Storeys = new float[5];

        /// <summary>Share of 1-storey sheds before the table applies (Metro 14%, Rim 44%).</summary>
        public float OneStoreyShare;

        public float NewarShare, HybridShare, ModernShare;

        /// <summary>Shop ground floor without a POI, on main lanes (tertiary and above) and elsewhere.</summary>
        public float ShopMain, ShopSide;

        /// <summary>Tile-roof share of NEWAR and HYBRID roofs.</summary>
        public float TileRoofShare;

        public bool HasBrick;
        public uint BrickFace, BrickJoint, BrickHighlight;

        /// <summary>Front edges longer than 9 m split into 4-8 m plots.</summary>
        public bool PlotSplit;

        public float RoofPitchDeg = 32f;
        public float EaveOverhangM = 1.0f;

        /// <summary>Carved timber floor bands instead of brick cornices (Bhaktapur).</summary>
        public bool CarvedBands;

        public float BlackWindowShare;
        public float GlazedTileShare;

        /// <summary>Rooftop restaurant terraces with umbrellas (Thamel, Boudha kora).</summary>
        public float RoofTerraceShare;

        /// <summary>Generic signboards per shop frontage (Thamel 3-8, elsewhere 1).</summary>
        public int SignsMin = 1, SignsMax = 1;

        public float CgiRoofShare;
        public float RanaShare;

        /// <summary>Gompa accent facades (white with a maroon band) on the Boudha kora.</summary>
        public float GompaAccentShare;

        /// <summary>Untagged buildings never exceed this many storeys (0 = no cap).</summary>
        public int MaxUntaggedStoreys;
    }

    /// <summary>Roof of a planned building.</summary>
    public enum PlanRoof : byte
    {
        Flat = 0,
        Gable = 1,
        Hip = 2,
        Skillion = 3,
        Pyramid = 4,
        Dome = 5,
        Pagoda = 6,
        Shikhara = 7,
        Stupa = 8,
    }

    /// <summary>
    /// What one building looks like at every band: the resolved archetype and profile, storeys and their heights,
    /// roof, front edge and colours. Every band builds from the same plan, so heights and colours do not pop between
    /// bands. Heights are metres above the building's ground.
    /// </summary>
    public struct HousePlan
    {
        public BuildingArchetype Archetype;
        public StyleProfile Profile;
        public AreaType Area;
        public int Storeys;

        /// <summary>Plinth height (the ground floor starts here).</summary>
        public float PlinthM;

        /// <summary>Top of the walls (eave line).</summary>
        public float WallTopM;

        /// <summary>Roof rise above the wall top.</summary>
        public float RoofRiseM;

        /// <summary>Raised-part start (building:part min_height), 0 for whole buildings.</summary>
        public float MinHeightM;

        public float StoreyScale;
        public PlanRoof Roof;
        public bool ShopGround;
        public int ShopBays;
        public bool MainLane;
        public bool TileRoof;
        public int FrontEdge;
        public int SecondEdge;
        public uint Seed;
        public uint Wall, Front, Trim, RoofColour, Wood;

        /// <summary>Temples, stupas and shrines: drawn by the sacred generators in B0.</summary>
        public bool Sacred;

        public float TotalM
        {
            get { return WallTopM + RoofRiseM; }
        }

        /// <summary>Bottom of storey <paramref name="floor"/> above the ground.</summary>
        public float FloorBase(int floor)
        {
            float y = MinHeightM + PlinthM;
            for (int f = 0; f < floor; f++) y += StoreyScale * BuildingGrammar.StoreyHeightM(Archetype, f, ShopGround);
            return y;
        }
    }

    /// <summary>
    /// The building grammar's data side (W2_DESIGN 2.1-2.5): style profiles and their priors, storey stacks, the
    /// archetype choice and the per-building <see cref="HousePlan"/>. Engine-free and deterministic: every draw comes
    /// from <see cref="GrammarRng"/> seeded by the building's seed and a purpose.
    /// </summary>
    public static class BuildingGrammar
    {
        private const uint PurposeArchetype = 0x41524348;
        private const uint PurposeStoreys = 0x53544F52;
        private const uint PurposeShop = 0x53484F50;
        private const uint PurposePalette = 0x50414C54;
        private const uint PurposeRoof = 0x524F4F46;

        private static readonly StyleParams[] Profiles = MakeProfiles();

        private static StyleParams P(StyleProfile p, float s2, float s3, float s4, float s5, float s6, float newar, float hybrid, float modern,
                                     float shopMain, float shopSide, float tile, uint face, uint joint, uint hi, bool split)
        {
            return new StyleParams
            {
                Profile = p, Storeys = new[] { s2, s3, s4, s5, s6 }, NewarShare = newar, HybridShare = hybrid, ModernShare = modern,
                ShopMain = shopMain, ShopSide = shopSide, TileRoofShare = tile, HasBrick = face != 0,
                BrickFace = face == 0 ? 0 : MeshColor.FromHex(face), BrickJoint = joint == 0 ? 0 : MeshColor.FromHex(joint),
                BrickHighlight = hi == 0 ? 0 : MeshColor.FromHex(hi), PlotSplit = split,
            };
        }

        private static StyleParams[] MakeProfiles()
        {
            var a = new StyleParams[13];
            a[(int)StyleProfile.KathmanduCore] = P(StyleProfile.KathmanduCore, 6, 13, 18, 34, 29, 0.20f, 0.35f, 0.45f, 0.70f, 0.40f, 0.25f, 0xC25A3C, 0x7A3022, 0xE07A57, true);
            a[(int)StyleProfile.KathmanduCore].BlackWindowShare = 0.30f;
            a[(int)StyleProfile.KathmanduCore].GlazedTileShare = 0.12f;
            a[(int)StyleProfile.Thamel] = P(StyleProfile.Thamel, 10, 17, 25, 28, 20, 0.05f, 0.15f, 0.80f, 0.85f, 0.85f, 0.10f, 0xC25A3C, 0x7A3022, 0xE07A57, true);
            a[(int)StyleProfile.Thamel].RoofTerraceShare = 0.35f;
            a[(int)StyleProfile.Thamel].SignsMin = 3;
            a[(int)StyleProfile.Thamel].SignsMax = 8;
            a[(int)StyleProfile.Patan] = P(StyleProfile.Patan, 22, 29, 25, 17, 7, 0.35f, 0.40f, 0.25f, 0.30f, 0.10f, 0.45f, 0xBF5236, 0x74291D, 0xDE7352, true);
            a[(int)StyleProfile.Bhaktapur] = P(StyleProfile.Bhaktapur, 2, 32, 51, 13, 2, 0.60f, 0.30f, 0.10f, 0.25f, 0.10f, 0.70f, 0xB4432F, 0x6E2219, 0xD4664A, false);
            a[(int)StyleProfile.Bhaktapur].RoofPitchDeg = 35f;
            a[(int)StyleProfile.Bhaktapur].EaveOverhangM = 1.2f;
            a[(int)StyleProfile.Bhaktapur].CarvedBands = true;
            a[(int)StyleProfile.Bhaktapur].MaxUntaggedStoreys = 6;
            a[(int)StyleProfile.Kirtipur] = P(StyleProfile.Kirtipur, 20, 30, 30, 18, 2, 0.40f, 0.35f, 0.25f, 0.20f, 0.20f, 0.50f, 0xB85A40, 0x713526, 0xD47A5C, true);
            a[(int)StyleProfile.Kirtipur].MaxUntaggedStoreys = 6;
            a[(int)StyleProfile.Thimi] = P(StyleProfile.Thimi, 20, 30, 30, 18, 2, 0.45f, 0.30f, 0.25f, 0.20f, 0.20f, 0.50f, 0xC9663F, 0x7E3A22, 0xE8885E, false);
            a[(int)StyleProfile.Bungamati] = P(StyleProfile.Bungamati, 10, 30, 35, 20, 5, 0.35f, 0.40f, 0.25f, 0.15f, 0.15f, 0.45f, 0xB86A4A, 0x76402C, 0xD48A68, false);
            a[(int)StyleProfile.Khokana] = P(StyleProfile.Khokana, 15, 40, 30, 13, 2, 0.50f, 0.35f, 0.15f, 0.10f, 0.10f, 0.55f, 0xB86A4A, 0x76402C, 0xD48A68, false);
            a[(int)StyleProfile.Panauti] = P(StyleProfile.Panauti, 30, 45, 20, 5, 0, 0.60f, 0.25f, 0.15f, 0.15f, 0.15f, 0.70f, 0xBC553B, 0x742C1F, 0xDA7556, false);
            a[(int)StyleProfile.Panauti].MaxUntaggedStoreys = 6;
            a[(int)StyleProfile.Metro] = P(StyleProfile.Metro, 18, 38, 18, 7, 5, 0f, 0f, 1f, 0.80f, 0.20f, 0f, 0, 0, 0, false);
            a[(int)StyleProfile.Metro].OneStoreyShare = 0.14f;
            a[(int)StyleProfile.Metro].RanaShare = 0.05f;
            a[(int)StyleProfile.Rim] = P(StyleProfile.Rim, 18, 23, 11, 3, 1, 0f, 0f, 1f, 0.10f, 0.10f, 0f, 0, 0, 0, false);
            a[(int)StyleProfile.Rim].OneStoreyShare = 0.44f;
            a[(int)StyleProfile.Rim].CgiRoofShare = 0.40f;
            a[(int)StyleProfile.BoudhaKora] = P(StyleProfile.BoudhaKora, 18, 38, 18, 7, 5, 0f, 0f, 1f, 0.80f, 0.40f, 0f, 0, 0, 0, false);
            a[(int)StyleProfile.BoudhaKora].GompaAccentShare = 0.30f;
            a[(int)StyleProfile.BoudhaKora].RoofTerraceShare = 0.35f;
            a[(int)StyleProfile.None] = a[(int)StyleProfile.Metro];
            return a;
        }

        /// <summary>The priors and palette of a profile (None and unknown values: Metro).</summary>
        public static StyleParams For(StyleProfile p)
        {
            int i = (int)p;
            return i >= 0 && i < Profiles.Length && Profiles[i] != null ? Profiles[i] : Profiles[(int)StyleProfile.Metro];
        }

        /// <summary>True for the profiles of Newar cores and towns.</summary>
        public static bool IsNewarProfile(StyleProfile p)
        {
            return p >= StyleProfile.KathmanduCore && p <= StyleProfile.Panauti;
        }

        /// <summary>
        /// Floor-to-floor height (W2_DESIGN 2.3 storey stacks; floors G = 0, 1, 2, ...): NEWAR 2.40 / 2.25 / 2.25 /
        /// 2.10; NEWAR_HYBRID 2.40 × 3 then 2.80; MODERN_URBAN 3.0 (3.2 for a shop ground floor); RANA_PALACE 4.5 / 5.0 /
        /// 4.0; everything else 3.0 (the W1 value). Replaces the W1 constant LevelHeightM.
        /// </summary>
        public static float StoreyHeightM(BuildingArchetype a, int floor)
        {
            return StoreyHeightM(a, floor, false);
        }

        public static float StoreyHeightM(BuildingArchetype a, int floor, bool shopGround)
        {
            switch (a)
            {
                case BuildingArchetype.Newar:
                    return floor <= 0 ? 2.40f : floor <= 2 ? 2.25f : 2.10f;
                case BuildingArchetype.NewarHybrid:
                    return floor <= 2 ? 2.40f : 2.80f;
                case BuildingArchetype.ModernUrban:
                    return floor == 0 && shopGround ? 3.2f : 3.0f;
                case BuildingArchetype.RanaPalace:
                    return floor <= 0 ? 4.5f : floor == 1 ? 5.0f : 4.0f;
                default:
                    return 3.0f;
            }
        }

        /// <summary>Storey count of a storey-share draw: 2..6 from the five shares (6 stands for 6+).</summary>
        private static int DrawStoreys(StyleParams p, ref GrammarRng rng)
        {
            if (p.OneStoreyShare > 0 && rng.Chance(p.OneStoreyShare)) return 1;
            int k = rng.Pick(p.Storeys);
            int s = 2 + k;
            if (k == 4 && rng.Chance(0.3f)) s = 7;
            return s;
        }

        /// <summary>True for archetypes the house grammar draws (others keep their own forms).</summary>
        public static bool IsHouse(BuildingArchetype a)
        {
            return a == BuildingArchetype.Newar || a == BuildingArchetype.NewarHybrid || a == BuildingArchetype.ModernUrban ||
                   a == BuildingArchetype.Generic || a == BuildingArchetype.RanaPalace;
        }

        /// <summary>True for temples, stupas, chortens and shrines.</summary>
        public static bool IsSacred(BuildingArchetype a)
        {
            return a == BuildingArchetype.TemplePagoda || a == BuildingArchetype.TempleShikhara || a == BuildingArchetype.Stupa ||
                   a == BuildingArchetype.Chorten || a == BuildingArchetype.Shrine;
        }

        /// <summary>
        /// The archetype a house is drawn with (W2_DESIGN 2.2, H 10): non-house archetypes are kept; BFNT hints win
        /// (RANA_HINT → RANA_PALACE, reinforced concrete → HYBRID or MODERN, mud mortar → NEWAR, a tagged flat roof →
        /// HYBRID or MODERN); a tagged gabled or hipped roof means NEWAR; otherwise the profile's seeded
        /// NEWAR / HYBRID / MODERN shares.
        /// </summary>
        public static BuildingArchetype ResolveArchetype(in BuildingRecord b, in BuildingFrontRecord f, uint seed)
        {
            if (!IsHouse(b.Archetype)) return b.Archetype;
            if (b.Archetype == BuildingArchetype.RanaPalace || b.Archetype == BuildingArchetype.NewarHybrid) return b.Archetype;
            StyleParams p = For(f.Profile);
            var rng = new GrammarRng(seed, PurposeArchetype);
            if (f.Has(BuildingFrontFlags.RanaHint)) return BuildingArchetype.RanaPalace;
            bool rcc = f.Has(BuildingFrontFlags.StructureRcc) || f.Has(BuildingFrontFlags.RoofFlatTagged) ||
                       (b.Flags & BuildingFlags.RoofTagged) != 0 && b.RoofShape == RoofShape.Flat;
            if (rcc)
            {
                float h = p.HybridShare, m = p.ModernShare;
                return h + m <= 0 || rng.Next() * (h + m) >= h ? BuildingArchetype.ModernUrban : BuildingArchetype.NewarHybrid;
            }
            if (f.Has(BuildingFrontFlags.StructureMud)) return BuildingArchetype.Newar;
            if ((b.Flags & BuildingFlags.RoofTagged) != 0 && (b.RoofShape == RoofShape.Gabled || b.RoofShape == RoofShape.Hipped))
                return BuildingArchetype.Newar;
            if (f.Profile == StyleProfile.None)
                return b.Archetype == BuildingArchetype.Generic ? BuildingArchetype.ModernUrban : b.Archetype;
            float r = rng.Next() * (p.NewarShare + p.HybridShare + p.ModernShare);
            if (r < p.NewarShare) return BuildingArchetype.Newar;
            if (r < p.NewarShare + p.HybridShare) return BuildingArchetype.NewarHybrid;
            return BuildingArchetype.ModernUrban;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Style zones without BFNT (W1 packs): the §2.1 rule-1 cores and circles, then the area type
        // ---------------------------------------------------------------------------------------------------------

        private struct Zone
        {
            public StyleProfile Profile;
            public double Lon0, Lat0, Lon1, Lat1;
        }

        // Thamel first (it lies next to the Kathmandu core), then the newar_core rectangles of archetype_zones.yaml.
        private static readonly Zone[] Zones =
        {
            new Zone { Profile = StyleProfile.Thamel, Lon0 = 85.306, Lat0 = 27.712, Lon1 = 85.316, Lat1 = 27.719 },
            new Zone { Profile = StyleProfile.KathmanduCore, Lon0 = 85.3015, Lat0 = 27.6975, Lon1 = 85.3155, Lat1 = 27.7115 },
            new Zone { Profile = StyleProfile.Patan, Lon0 = 85.3180, Lat0 = 27.6650, Lon1 = 85.3320, Lat1 = 27.6800 },
            new Zone { Profile = StyleProfile.Bhaktapur, Lon0 = 85.4190, Lat0 = 27.6690, Lon1 = 85.4400, Lat1 = 27.6760 },
            new Zone { Profile = StyleProfile.Kirtipur, Lon0 = 85.2720, Lat0 = 27.6740, Lon1 = 85.2840, Lat1 = 27.6820 },
            new Zone { Profile = StyleProfile.Thimi, Lon0 = 85.3810, Lat0 = 27.6760, Lon1 = 85.3920, Lat1 = 27.6860 },
            new Zone { Profile = StyleProfile.Bungamati, Lon0 = 85.2960, Lat0 = 27.6220, Lon1 = 85.3050, Lat1 = 27.6300 },
            new Zone { Profile = StyleProfile.Khokana, Lon0 = 85.2910, Lat0 = 27.6330, Lon1 = 85.2990, Lat1 = 27.6410 },
            new Zone { Profile = StyleProfile.Kirtipur, Lon0 = 85.4620, Lat0 = 27.7220, Lon1 = 85.4760, Lat1 = 27.7340 }, // Sankhu
            new Zone { Profile = StyleProfile.Panauti, Lon0 = 85.5100, Lat0 = 27.5800, Lon1 = 85.5220, Lat1 = 27.5900 },
        };

        private const double BoudhaLon = 85.3620, BoudhaLat = 27.7215, BoudhaRadiusM = 350;

        /// <summary>
        /// The profile of a point when the tile has no BFNT (W2_DESIGN 2.1 rules 1 and 4): the Thamel rectangle, the
        /// Boudha kora circle and the Newar core rectangles, then Metro in built-up area types and Rim elsewhere.
        /// </summary>
        public static StyleProfile FallbackProfile(double gameX, double gameZ, AreaType area)
        {
            double lon, lat;
            WorldFrame.GameToLonLat(gameX, gameZ, out lon, out lat);
            foreach (Zone z in Zones)
                if (lon >= z.Lon0 && lon <= z.Lon1 && lat >= z.Lat0 && lat <= z.Lat1) return z.Profile;
            double bx, bz;
            WorldFrame.LonLatToGame(BoudhaLon, BoudhaLat, out bx, out bz);
            if ((gameX - bx) * (gameX - bx) + (gameZ - bz) * (gameZ - bz) <= BoudhaRadiusM * BoudhaRadiusM) return StyleProfile.BoudhaKora;
            return area == AreaType.Urban || area == AreaType.OldCore ? StyleProfile.Metro : StyleProfile.Rim;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Palettes (W2_DESIGN 2.5)
        // ---------------------------------------------------------------------------------------------------------

        private static readonly uint[] ModernPaint = Hex(0xF7F6F0, 0xF6E6BE, 0xFFD95A, 0xF8B48A, 0xF49AC1, 0x7FC8F8, 0x9FE2B8, 0xB6E05A,
                                                         0x3CC9C0, 0xB79CE8, 0xF59A3B, 0xABA79F, 0xB8654A);
        private static readonly float[] ModernPaintShare = { 16, 14, 9, 9, 8, 8, 7, 5, 5, 4, 4, 6, 5 };
        public static readonly uint[] Jhingati = Hex(0xB8322B, 0xD2452E, 0xE2603A, 0xEC8A3E, 0x6F5A3C);
        private static readonly float[] JhingatiShare = { 25, 35, 25, 10, 5 };
        public static readonly uint JhingatiRidge = MeshColor.FromHex(0x9E3A26);
        public static readonly uint[] Cgi = Hex(0x3D7CC9, 0x3FA35C, 0xC9433A, 0xA2603A, 0xB9BEC3);
        public static readonly uint RawBrick = MeshColor.FromHex(0xB8654A);
        public static readonly uint NeutralBrick = MeshColor.FromHex(0xB4543A);
        public static readonly uint SalDark = MeshColor.FromHex(0x4A2C1C);
        public static readonly uint SalMid = MeshColor.FromHex(0x6B4129);
        public static readonly uint SalLight = MeshColor.FromHex(0x8E5B37);
        public static readonly uint PaintedBrown = MeshColor.FromHex(0x5A3222);
        /// <summary>Green-painted window frames and shutters (Patan and Kathmandu lanes).</summary>
        public static readonly uint PaintedGreen = MeshColor.FromHex(0x2F6E4E);
        /// <summary>Red-painted doors and windows of the southern villages (Bungamati, Khokana).</summary>
        public static readonly uint PaintedRed = MeshColor.FromHex(0x9E2B25);
        public static readonly uint Hay = MeshColor.FromHex(0xC9A24E);
        public static readonly uint RawConcrete = MeshColor.FromHex(0xABA79F);
        public static readonly uint RanaShadow = MeshColor.FromHex(0xD9CDB0);
        public static readonly uint RanaTrim = MeshColor.FromHex(0xFFFFFF);
        public static readonly uint Brass = MeshColor.FromHex(0xD9A93A);
        public static readonly uint Ochre = MeshColor.FromHex(0xE0A845);
        public static readonly uint MudPlaster = MeshColor.FromHex(0xC9A27A);
        public static readonly uint Interior = MeshColor.FromHex(0x2A211C);
        public static readonly uint DoorDark = MeshColor.FromHex(0x3A2418);
        public static readonly uint Aluminium = MeshColor.FromHex(0xC9CED3);
        public static readonly uint SteelDark = MeshColor.FromHex(0x2E3440);
        /// <summary>Window glass as it reads from the street in the valley: tinted blue, teal, brown or plain dark.</summary>
        public static readonly uint[] GlassTints = Hex(0x3E5F7A, 0x2F6B6E, 0x5A4A3A, 0x34495E, 0x4A7A9A);
        /// <summary>Aluminium composite cladding of new commercial blocks (silver, blue, red, champagne).</summary>
        public static readonly uint[] Acp = Hex(0xC4C9CE, 0x2F6FB0, 0xC23B33, 0xD8C8A0, 0x5B6B7A);
        /// <summary>Awnings: tin and canvas.</summary>
        public static readonly uint[] Awning = Hex(0x3D7CC9, 0x2FA84F, 0xE8483A, 0xF2C230, 0xB9BEC3, 0xF07A2A);
        /// <summary>Clothes on the line and goods at shop fronts.</summary>
        public static readonly uint[] Cloth = Hex(0xD93A2B, 0xF2C230, 0x2E6FD8, 0xFFFFFF, 0xE85D9E, 0x2E9E4F, 0x7A1F1F, 0xF59A3B);
        public static readonly uint Foliage = MeshColor.FromHex(0x3E8E3A);
        public static readonly uint FoliageLight = MeshColor.FromHex(0x6DB33F);
        public static readonly uint PaintedBlack = MeshColor.FromHex(0x2B221D);
        public static readonly uint Sindoor = MeshColor.FromHex(0xE23B2A);
        public static readonly uint Marigold = MeshColor.FromHex(0xF6A21B);
        public static readonly uint PlinthStone = MeshColor.FromHex(0x9A948A);
        public static readonly uint Concrete = MeshColor.FromHex(0xBDB8AE);
        public static readonly uint Glass = MeshColor.FromHex(0x34495E);
        public static readonly uint Lime = MeshColor.FromHex(0xF4EFE0);
        public static readonly uint GompaWhite = MeshColor.FromHex(0xF3EFE6);
        public static readonly uint GompaMaroon = MeshColor.FromHex(0x7A1F1F);
        public static readonly uint Rust = MeshColor.FromHex(0x8B4A2B);
        public static readonly uint[] Railing = Hex(0x2E3440, 0x2E3440, 0x2E3440, 0x3D7CC9, 0xC9A13A);
        public static readonly uint[] Shutter = Hex(0x8C949C, 0x8C949C, 0x8C949C, 0x3D7CC9, 0xC9433A, 0x3FA35C);
        public static readonly uint[] Tank = Hex(0x2A2A2E, 0x2A2A2E, 0x2F6FD6, 0xE9E4D4, 0xF2C230);
        public static readonly uint[] Sign = Hex(0xE8483A, 0xFFD23F, 0x2F7DE1, 0x2FA84F, 0xFFFFFF);
        public static readonly uint[] Glazed = Hex(0xE8E2D4, 0xCFE3EA, 0xE9D7C9);
        public static readonly uint RanaStucco = MeshColor.FromHex(0xF8F5EC);
        public static readonly uint RanaStuccoYellow = MeshColor.FromHex(0xF1E3BE);
        public static readonly uint RanaShutter = MeshColor.FromHex(0x4E7D52);

        private static uint[] Hex(params uint[] rgb)
        {
            var a = new uint[rgb.Length];
            for (int i = 0; i < rgb.Length; i++) a[i] = MeshColor.FromHex(rgb[i]);
            return a;
        }

        /// <summary>A seeded jhingati tile colour (the 5-way mix incl. aged patches).</summary>
        public static uint JhingatiColour(ref GrammarRng rng)
        {
            return Jhingati[rng.Pick(JhingatiShare)];
        }

        public static uint ModernPaintColour(ref GrammarRng rng)
        {
            return ModernPaint[rng.Pick(ModernPaintShare)];
        }

        /// <summary>Weathered plaster of the old cores (Asan, Indra Chowk, Patan, Kirtipur): off-white, beige, grey
        /// plaster, faded peach, pink, sage and yellow (ref_buildings.md §1: the cores are muted, the bright pastels are the
        /// metro's).</summary>
        public static readonly uint[] CorePaint = Hex(0xE8E2D6, 0xD9CBB0, 0xC9C2B5, 0xE6C9A8, 0xD8B4A6, 0xBFC9C2, 0xEAD9A0, 0xDCD3C4);

        /// <summary>A paint colour for a modern or hybrid front in a profile: the old cores draw 65% from the muted
        /// <see cref="CorePaint"/>, Thamel and the Boudha kora 40%, the metro and the rim the pastel set.</summary>
        public static uint ModernPaintColour(StyleProfile profile, ref GrammarRng rng)
        {
            float muted = profile == StyleProfile.KathmanduCore || profile == StyleProfile.Patan || profile == StyleProfile.Kirtipur ||
                          profile == StyleProfile.Thimi || profile == StyleProfile.Bhaktapur || profile == StyleProfile.Panauti ? 0.65f
                : profile == StyleProfile.Thamel || profile == StyleProfile.BoudhaKora ? 0.4f : 0f;
            if (muted > 0 && rng.Chance(muted)) return CorePaint[rng.Int(0, CorePaint.Length - 1)];
            return ModernPaint[rng.Pick(ModernPaintShare)];
        }

        /// <summary>Brick face of a profile, varied ±6% by the draw.</summary>
        public static uint BrickColour(StyleParams p, ref GrammarRng rng)
        {
            uint c = p.HasBrick ? p.BrickFace : NeutralBrick;
            return MeshColor.Scale(c, rng.Range(0.94f, 1.06f));
        }

        // ---------------------------------------------------------------------------------------------------------
        // The plan
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Plan building <paramref name="index"/> of a tile: BFNT (or the fallback profile, area type and front edge
        /// from <see cref="BuildingFronts"/>), the resolved archetype, storeys (tagged levels win; inferred ones come
        /// from the profile's shares, capped in the small Newar towns), storey heights scaled to a tagged height,
        /// roof, shop ground floor and colours.
        /// </summary>
        public static HousePlan Plan(TileData t, int index)
        {
            BuildingRecord b = t.Buildings[index];
            BuildingFronts fronts = BuildingFronts.For(t);
            BuildingFrontRecord f = fronts.Front(index);
            return Plan(b, f, fronts.MainLane(index));
        }

        /// <summary>The plan of one record with its front (see <see cref="Plan(TileData, int)"/>).</summary>
        public static HousePlan Plan(BuildingRecord b, BuildingFrontRecord f, bool mainLane)
        {
            StyleParams p = For(f.Profile);
            uint seed = b.Seed;
            var plan = new HousePlan
            {
                Profile = f.Profile, Area = f.Area, Seed = seed, FrontEdge = f.FrontEdge == BuildingFrontRecord.NoEdge ? -1 : f.FrontEdge,
                SecondEdge = f.SecondEdge == BuildingFrontRecord.NoEdge ? -1 : f.SecondEdge, MainLane = mainLane, StoreyScale = 1f,
            };
            plan.Archetype = ResolveArchetype(b, f, seed);
            plan.Sacred = IsSacred(plan.Archetype);
            bool house = IsHouse(plan.Archetype);
            var srng = new GrammarRng(seed, PurposeStoreys);
            int storeys = b.Levels < 1 ? 1 : b.Levels;
            bool heightTagged = (b.Flags & BuildingFlags.HeightTagged) != 0;
            if (house && (b.Flags & BuildingFlags.LevelsInferred) != 0 && !heightTagged && f.Profile != StyleProfile.None)
            {
                storeys = DrawStoreys(p, ref srng);
                if (p.MaxUntaggedStoreys > 0 && storeys > p.MaxUntaggedStoreys) storeys = p.MaxUntaggedStoreys;
            }
            if (plan.Archetype == BuildingArchetype.Newar && storeys > 5) plan.Archetype = BuildingArchetype.NewarHybrid;
            plan.Storeys = storeys;

            // Shop ground floor: a shop POI inside forces it; else the profile's share on main or side lanes.
            var shop = new GrammarRng(seed, PurposeShop);
            plan.ShopBays = f.ShopCount;
            plan.ShopGround = house && plan.Archetype != BuildingArchetype.RanaPalace &&
                              (f.ShopCount > 0 || shop.Chance(mainLane ? p.ShopMain : p.ShopSide) && storeys >= 2);
            plan.MinHeightM = (float)(b.MinHeightCm / 100.0);
            switch (plan.Archetype)
            {
                case BuildingArchetype.Newar:
                case BuildingArchetype.NewarHybrid: plan.PlinthM = 0.15f + 0.3f * ((seed >> 7 & 0xFF) / 255f); break;
                case BuildingArchetype.ModernUrban: plan.PlinthM = 0.3f; break;
                case BuildingArchetype.RanaPalace: plan.PlinthM = 0.6f + 0.6f * ((seed >> 7 & 0xFF) / 255f); break;
                default: plan.PlinthM = 0.2f; break;
            }
            float walls = 0f;
            for (int k = 0; k < storeys; k++) walls += StoreyHeightM(plan.Archetype, k, plan.ShopGround);

            // Roof.
            var rr = new GrammarRng(seed, PurposeRoof);
            RoofShape shape = BuildingStyle.ResolvedShape(b);
            switch (plan.Archetype)
            {
                case BuildingArchetype.Newar:
                    plan.Roof = PlanRoof.Gable;
                    plan.TileRoof = true;
                    break;
                case BuildingArchetype.NewarHybrid:
                    // A hybrid in a heritage zone keeps a jhingati gable over its new floors (the B0 grammar draws it on
                    // near-rectangular plots; the far bands follow).
                    plan.TileRoof = rr.Chance(p.TileRoofShare);
                    plan.Roof = plan.TileRoof ? PlanRoof.Gable : PlanRoof.Flat;
                    break;
                case BuildingArchetype.ModernUrban:
                case BuildingArchetype.Generic:
                    plan.Roof = storeys <= 1 && p.CgiRoofShare > 0 && rr.Chance(p.CgiRoofShare) ? PlanRoof.Skillion : PlanRoof.Flat;
                    if ((b.Flags & BuildingFlags.RoofTagged) != 0 && shape != RoofShape.Flat) plan.Roof = RoofOf(shape);
                    break;
                case BuildingArchetype.RanaPalace:
                    plan.Roof = PlanRoof.Flat;
                    break;
                default:
                    plan.Roof = RoofOf(shape);
                    break;
            }
            float roofAllowance = plan.Roof == PlanRoof.Flat ? BuildingStyle.RoofAllowanceM(RoofShape.Flat)
                : plan.Roof == PlanRoof.Gable ? BuildingStyle.RoofAllowanceM(RoofShape.Gabled) : BuildingStyle.RoofAllowanceM(shape);
            if (heightTagged && b.HeightCm > 0)
            {
                float total = (float)(b.HeightCm / 100.0) - plan.MinHeightM;
                float target = Math.Max(1f, total - roofAllowance - plan.PlinthM);
                plan.StoreyScale = house ? Clamp(target / walls, 0.6f, 1.6f) : target / walls;
            }
            else if (!house && b.HeightCm > 0)
            {
                // Non-house archetypes keep the pipeline's height (temples and institutions carry their own).
                float total = (float)(b.HeightCm / 100.0) - plan.MinHeightM;
                plan.StoreyScale = Math.Max(0.3f, (total - roofAllowance - plan.PlinthM) / walls);
            }
            plan.WallTopM = plan.MinHeightM + plan.PlinthM + plan.StoreyScale * walls;
            plan.RoofRiseM = roofAllowance;

            // Colours.
            var pal = new GrammarRng(seed, PurposePalette);
            plan.Wood = p.BlackWindowShare > 0 && pal.Chance(p.BlackWindowShare) ? PaintedBlack : SalDark;
            switch (plan.Archetype)
            {
                case BuildingArchetype.Newar:
                    plan.Wall = BrickColour(p, ref pal);
                    plan.Front = plan.Wall;
                    plan.RoofColour = JhingatiColour(ref pal);
                    break;
                case BuildingArchetype.NewarHybrid:
                    plan.Wall = BrickColour(p, ref pal);
                    plan.Front = IsNewarProfile(f.Profile) && pal.Chance(0.6f) ? plan.Wall : ModernPaintColour(f.Profile, ref pal);
                    plan.RoofColour = plan.TileRoof ? JhingatiColour(ref pal) : Concrete;
                    break;
                case BuildingArchetype.RanaPalace:
                    plan.Wall = pal.Chance(0.5f) ? RanaStucco : RanaStuccoYellow;
                    plan.Front = plan.Wall;
                    plan.RoofColour = Concrete;
                    break;
                case BuildingArchetype.ModernUrban:
                case BuildingArchetype.Generic:
                    plan.Front = p.GlazedTileShare > 0 && pal.Chance(p.GlazedTileShare) ? Glazed[pal.Int(0, Glazed.Length - 1)] : ModernPaintColour(f.Profile, ref pal);
                    if (p.GompaAccentShare > 0 && pal.Chance(p.GompaAccentShare)) plan.Front = GompaWhite;
                    plan.Wall = pal.Chance(0.6f) ? RawBrick : plan.Front;
                    plan.RoofColour = plan.Roof == PlanRoof.Skillion ? Cgi[pal.Int(0, Cgi.Length - 1)] : Concrete;
                    break;
                default:
                    plan.Wall = BuildingStyle.WallRgba(b);
                    plan.Front = plan.Wall;
                    plan.RoofColour = BuildingStyle.RoofRgba(b, shape);
                    break;
            }
            plan.Trim = pal.Chance(0.5f) ? MeshColor.FromHex(0xFFFFFF) : MeshColor.Scale(plan.Front, 0.8f);
            return plan;
        }

        private static PlanRoof RoofOf(RoofShape s)
        {
            switch (s)
            {
                case RoofShape.Gabled:
                case RoofShape.Round: return PlanRoof.Gable;
                case RoofShape.Hipped:
                case RoofShape.HalfHipped:
                case RoofShape.Gambrel:
                case RoofShape.Mansard: return PlanRoof.Hip;
                case RoofShape.Skillion: return PlanRoof.Skillion;
                case RoofShape.Pyramidal:
                case RoofShape.Cone: return PlanRoof.Pyramid;
                case RoofShape.Dome:
                case RoofShape.Onion: return PlanRoof.Dome;
                case RoofShape.Pagoda: return PlanRoof.Pagoda;
                case RoofShape.Shikhara: return PlanRoof.Shikhara;
                default: return PlanRoof.Flat;
            }
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }
    }
}
