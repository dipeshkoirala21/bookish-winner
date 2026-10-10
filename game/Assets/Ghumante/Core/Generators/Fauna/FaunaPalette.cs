using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// Colours (0xRRGGBBAA; alpha 255 = coat, tinted per instance, alpha 0 = fixed) and material channels of the
    /// fauna, from street_life 7-8 and W2_DESIGN 5.5-5.6, plus the coat table that turns a sim tint index into a
    /// (pattern, coat colour) pair.
    /// </summary>
    public static class FaunaPalette
    {
        /// <summary>Material channel of fur (the look package's fine strand texture).</summary>
        public const MaterialChannel FurChannel = MaterialChannel.Hair;

        /// <summary>Material channel of feathers (a soft fine texture).</summary>
        public const MaterialChannel FeatherChannel = MaterialChannel.Fabric;

        /// <summary>Bare skin: muzzles, macaque faces, combs, udders, tongues.</summary>
        public const MaterialChannel SkinChannel = MaterialChannel.Skin;

        /// <summary>Horn, hoof, beak and claw (smooth keratin).</summary>
        public const MaterialChannel KeratinChannel = MaterialChannel.Plain;

        /// <summary>Wet noses (leathery).</summary>
        public const MaterialChannel NoseChannel = MaterialChannel.Leather;

        /// <summary>Eyes (glossy paint).</summary>
        public const MaterialChannel EyeChannel = MaterialChannel.Paint;

        // Tinted coat shades (white × the instance tint).
        public const uint Coat = 0xFFFFFFFFu;
        public const uint CoatBelly = 0xF4F2F0FFu;
        public const uint CoatLeg = 0xE6E4E0FFu;
        public const uint CoatFace = 0xF0EEEBFFu;
        public const uint CoatDark = 0xC8C4BEFFu;

        // Fixed colours.
        public const uint Hoof = 0x2E2A2600u;
        public const uint HornBase = 0xD9CDB300u;
        public const uint HornTip = 0x3E363000u;
        public const uint BuffaloHorn = 0x4A4643FFu;
        public const uint MuzzleDark = 0x55504C00u;
        public const uint MuzzlePink = 0xCFA595FFu;
        public const uint Nose = 0x1F1B1A00u;
        public const uint EyeDark = 0x22170F00u;
        public const uint EyeBlack = 0x0E0C0C00u;
        public const uint EarInner = 0xDCC0B200u;
        public const uint Tongue = 0xE4787A00u;
        public const uint Udder = 0xE6BDAC00u;
        public const uint Switch = 0x38322E00u;
        public const uint PatchWhite = 0xF3F1EB00u;
        public const uint PatchBlack = 0x26242200u;
        public const uint Tan = 0xC8955E00u;
        public const uint Claw = 0x3A332E00u;

        // Macaque.
        public const uint MacaqueFur = 0x8C7A64FFu;
        public const uint MacaqueRump = 0xBC844900u;
        public const uint MacaqueFace = 0xDB9C88FFu;
        public const uint MacaqueHand = 0x7A5E50FFu;

        // Birds (fixed; bird bodies are tinted only where the species has colour morphs).
        public const uint PigeonGrey = 0x8A8F99FFu;
        public const uint PigeonPale = 0xA7ABB4FFu;
        public const uint PigeonNeckGreen = 0x4E7A6A00u;
        public const uint PigeonNeckPurple = 0x7B5C8E00u;
        public const uint PigeonBar = 0x2F2F3500u;
        public const uint PigeonTailBand = 0x3A3A4200u;
        public const uint PigeonEye = 0xE0782800u;
        public const uint PigeonFeet = 0xC8706800u;
        public const uint PigeonBill = 0x2E2A2A00u;
        public const uint PigeonCere = 0xE8E6E000u;
        public const uint CrowBlack = 0x16161800u;
        public const uint CrowGrey = 0x6E6E7000u;
        public const uint KiteBrown = 0x5C463200u;
        public const uint KitePale = 0x8F755800u;
        public const uint KiteDark = 0x3C2E2400u;
        public const uint Yellow = 0xF2C21B00u;
        public const uint MynaBrown = 0x5A3E2B00u;
        public const uint MynaHead = 0x1A1A1A00u;
        public const uint White = 0xF7F7F200u;
        public const uint SparrowBrown = 0x8B6B4A00u;
        public const uint SparrowStreak = 0x4A3424u << 8;
        public const uint SparrowCrown = 0x8C8C8C00u;
        public const uint SparrowCheek = 0xD8D2C400u;
        public const uint SparrowBib = 0x1E1C1C00u;
        public const uint SparrowBill = 0x3A3636u << 8;
        public const uint EgretWhite = 0xF7F7F200u;
        public const uint EgretBuff = 0xE0B07000u;
        public const uint EgretLeg = 0x4A4A3A00u;
        public const uint SwallowBlue = 0x1C2A4400u;
        public const uint SwallowThroat = 0xA8492E00u;
        public const uint SwallowBelly = 0xF0E8DC00u;
        public const uint Comb = 0xD8282800u;
        public const uint HenBrown = 0x9C5A2EFFu;
        public const uint RoosterHackle = 0xE0A030FFu;
        public const uint RoosterTail = 0x1E3A2E00u;
        public const uint RoosterBreast = 0x2A2220FFu;
        public const uint FowlLeg = 0xE0B04A00u;
        public const uint FowlBeak = 0xE8C060u << 8;
        public const uint DuckBill = 0xF09A2A00u;

        /// <summary>Cow coats (W2_DESIGN 5.5): white, cream, grey, black-and-white, brown, brown-and-white.</summary>
        public static readonly uint[] CowCoats = { 0xEDE7DA, 0xD8C9A8, 0x9D9890, 0x2B2B2B, 0x8B5A3C, 0x9A6440 };

        /// <summary>Dog coats (W2_DESIGN 5.5): tan, black, black-and-tan, cream, patched.</summary>
        public static readonly uint[] DogCoats = { 0xC49A6C, 0x2A2A2A, 0xC9955E, 0xEFE3C8, 0xA88A6A };

        /// <summary>Goat coats (Khari goats: black, brown, white, mixed).</summary>
        public static readonly uint[] GoatCoats = { 0x2A2624, 0x7A4A2C, 0xEDE8DE, 0x5A3E2A, 0x8E6A4A };

        /// <summary>Buffalo coats (slate grey to near-black).</summary>
        public static readonly uint[] BuffaloCoats = { 0x3C3C3E, 0x2E2C2C, 0x4A4846 };

        /// <summary>Macaque coats (brown-grey).</summary>
        public static readonly uint[] MacaqueCoats = { 0x8C7A64, 0x96826A, 0x7E6E5A };

        /// <summary>Hen coats (red-brown, buff, black, white) and rooster body coats.</summary>
        public static readonly uint[] HenCoats = { 0x9C5A2E, 0xC89A5A, 0x2A2624, 0xF2EEE4, 0x8A4A26 };

        /// <summary>Duck coats (white, brown mallard-like).</summary>
        public static readonly uint[] DuckCoats = { 0xF4F2EC, 0x8A6A4A, 0xF0EDE4 };

        /// <summary>The coat for a sim tint index: the pattern of the mesh variant and the instance tint (0xRRGGBB).</summary>
        public static void CoatFor(FaunaSpecies s, int tint, out CoatPattern pattern, out uint rgb)
        {
            if (tint < 0) tint = -tint;
            switch (s)
            {
                case FaunaSpecies.Cow:
                case FaunaSpecies.Calf:
                case FaunaSpecies.Bull:
                {
                    int i = tint % CowCoats.Length;
                    rgb = CowCoats[i];
                    pattern = i == 3 || i == 5 ? CoatPattern.Patched : i == 1 && tint % 12 == 7 ? CoatPattern.Socks : CoatPattern.Plain;
                    return;
                }
                case FaunaSpecies.Dog:
                {
                    int i = tint % DogCoats.Length;
                    rgb = DogCoats[i];
                    pattern = i == 2 ? CoatPattern.Saddle : i == 4 ? CoatPattern.Patched : (tint / 5) % 3 == 1 ? CoatPattern.Socks : CoatPattern.Plain;
                    return;
                }
                case FaunaSpecies.Goat:
                {
                    int i = tint % GoatCoats.Length;
                    rgb = GoatCoats[i];
                    pattern = i == 3 ? CoatPattern.Patched : i == 4 ? CoatPattern.Saddle : CoatPattern.Plain;
                    return;
                }
                case FaunaSpecies.Macaque:
                case FaunaSpecies.MacaqueBaby:
                    rgb = MacaqueCoats[tint % MacaqueCoats.Length];
                    pattern = CoatPattern.Plain;
                    return;
                case FaunaSpecies.Hen:
                    rgb = HenCoats[tint % HenCoats.Length];
                    pattern = CoatPattern.Plain;
                    return;
                case FaunaSpecies.Rooster:
                    rgb = tint % 3 == 2 ? 0xF2EEE4u : 0xB0552Au;
                    pattern = tint % 3 == 2 ? CoatPattern.Patched : CoatPattern.Plain;
                    return;
                case FaunaSpecies.Duck:
                    rgb = DuckCoats[tint % DuckCoats.Length];
                    pattern = tint % DuckCoats.Length == 1 ? CoatPattern.Saddle : CoatPattern.Plain;
                    return;
                case FaunaSpecies.Pigeon:
                    // Most feral rock pigeons are blue-bar grey; some are darker chequers, a few pale or brownish.
                    rgb = tint % 7 == 3 ? 0x7E808Au : tint % 7 == 5 ? 0xC9C6C2u : tint % 11 == 4 ? 0xB09A8Cu : BirdMesher.PigeonBaseTint;
                    pattern = CoatPattern.Plain;
                    return;
                case FaunaSpecies.Buffalo:
                    rgb = BuffaloCoats[tint % BuffaloCoats.Length];
                    pattern = tint % 4 == 1 ? CoatPattern.Socks : CoatPattern.Plain;
                    return;
                default:
                    rgb = 0xFFFFFF;
                    pattern = CoatPattern.Plain;
                    return;
            }
        }

        private static readonly CoatPattern[] Four = { CoatPattern.Plain, CoatPattern.Patched, CoatPattern.Saddle, CoatPattern.Socks };
        private static readonly CoatPattern[] PlainPatchedSaddle = { CoatPattern.Plain, CoatPattern.Patched, CoatPattern.Saddle };
        private static readonly CoatPattern[] PlainSocks = { CoatPattern.Plain, CoatPattern.Socks };
        private static readonly CoatPattern[] PlainPatched = { CoatPattern.Plain, CoatPattern.Patched };
        private static readonly CoatPattern[] PlainSaddle = { CoatPattern.Plain, CoatPattern.Saddle };
        private static readonly CoatPattern[] PlainOnly = { CoatPattern.Plain };

        /// <summary>The coat patterns a species uses (one mesh variant each). Do not modify the returned array.</summary>
        public static CoatPattern[] Patterns(FaunaSpecies s)
        {
            switch (s)
            {
                case FaunaSpecies.Cow:
                case FaunaSpecies.Calf:
                case FaunaSpecies.Bull:
                case FaunaSpecies.Dog:
                    return Four;
                case FaunaSpecies.Goat:
                    return PlainPatchedSaddle;
                case FaunaSpecies.Buffalo:
                    return PlainSocks;
                case FaunaSpecies.Rooster:
                    return PlainPatched;
                case FaunaSpecies.Duck:
                case FaunaSpecies.Egret:
                    return PlainSaddle;
                default:
                    return PlainOnly;
            }
        }

        /// <summary>Number of coat patterns of a species (<see cref="Patterns"/>).</summary>
        public static int PatternCount(FaunaSpecies s)
        {
            return Patterns(s).Length;
        }

        /// <summary>
        /// Smooth 3D value noise in [0, 1) (deterministic; for coat patches), with features about
        /// 1 / <paramref name="freq"/> metres across.
        /// </summary>
        public static float Noise(float x, float y, float z, float freq, uint seed)
        {
            x *= freq;
            y *= freq;
            z *= freq;
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y), iz = (int)Math.Floor(z);
            float fx = x - ix, fy = y - iy, fz = z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            float c000 = Corner(ix, iy, iz, seed), c100 = Corner(ix + 1, iy, iz, seed);
            float c010 = Corner(ix, iy + 1, iz, seed), c110 = Corner(ix + 1, iy + 1, iz, seed);
            float c001 = Corner(ix, iy, iz + 1, seed), c101 = Corner(ix + 1, iy, iz + 1, seed);
            float c011 = Corner(ix, iy + 1, iz + 1, seed), c111 = Corner(ix + 1, iy + 1, iz + 1, seed);
            float x00 = FMath.Lerp(c000, c100, fx), x10 = FMath.Lerp(c010, c110, fx);
            float x01 = FMath.Lerp(c001, c101, fx), x11 = FMath.Lerp(c011, c111, fx);
            return FMath.Lerp(FMath.Lerp(x00, x10, fy), FMath.Lerp(x01, x11, fy), fz);
        }

        private static float Corner(int x, int y, int z, uint seed)
        {
            return FaunaRng.Unit(FaunaRng.Hash((uint)x, (uint)y, (uint)z, seed));
        }

        /// <summary>Two octaves of <see cref="Noise"/> (patch outlines with some wiggle).</summary>
        public static float Patches(Fv3 p, float freq, uint seed)
        {
            return 0.7f * Noise(p.X, p.Y, p.Z, freq, seed) + 0.3f * Noise(p.X, p.Y, p.Z, freq * 2.7f, seed ^ 0x5BD1E995u);
        }
    }
}
