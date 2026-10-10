using System;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// Entry point of the fauna generators: builds any <see cref="FaunaSpecies"/> at a level of detail (0 near,
    /// 1 mid, 2 far) and coat pattern into a reusable <see cref="FaunaMesh"/> (skinned bind pose with UV0 = material
    /// channel and baked AO). Deterministic: the same arguments give the same mesh on every device. Build time only.
    /// </summary>
    public static class FaunaMesher
    {
        /// <summary>Number of mesh levels per species (paper birds are separate: <see cref="PaperBird"/>).</summary>
        public const int LodCount = 3;

        /// <summary>Builds <paramref name="species"/> into <paramref name="target"/> (cleared first).</summary>
        public static void Build(FaunaSpecies species, int lod, CoatPattern pattern, FaunaMesh target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (lod < 0) lod = 0;
            if (lod >= LodCount) lod = LodCount - 1;
            switch (FaunaCatalog.Family(species))
            {
                case FaunaFamily.Bovine:
                    BovineMesher.Build(species, lod, pattern, target);
                    break;
                case FaunaFamily.Canine:
                    CanineMesher.Build(lod, pattern, target);
                    break;
                case FaunaFamily.Caprine:
                    CaprineMesher.Build(lod, pattern, target);
                    break;
                case FaunaFamily.Primate:
                    PrimateMesher.Build(species, lod, target);
                    break;
                case FaunaFamily.Fowl:
                    FowlMesher.Build(species, lod, pattern, target);
                    break;
                default:
                    BirdMesher.Build(species, lod, target);
                    break;
            }
        }

        /// <summary>A new mesh of <paramref name="species"/> (convenience for tools and tests).</summary>
        public static FaunaMesh Create(FaunaSpecies species, int lod, CoatPattern pattern = CoatPattern.Plain)
        {
            var m = new FaunaMesh(2048, 6144);
            Build(species, lod, pattern, m);
            return m;
        }
    }
}
