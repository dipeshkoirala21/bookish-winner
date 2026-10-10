namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// Tessellation of a fauna level of detail: vertices round a body tube, round a limb and round small parts, rings
    /// per knot interval, and how much of the face and hooves is modelled. LOD0 is the near close-up (2,500 tris for
    /// a cow), LOD1 the mid range (1,000), LOD2 the far crowd version (300; W2_DESIGN 5.5-5.6).
    /// </summary>
    public readonly struct FaunaDetail
    {
        public readonly int Lod;

        /// <summary>Vertices round the body, neck and head tubes.</summary>
        public readonly int BodySegs;

        /// <summary>Vertices round limbs, horns, ears and tails.</summary>
        public readonly int LimbSegs;

        /// <summary>Vertices round small parts (toes, nostrils, wattles).</summary>
        public readonly int SmallSegs;

        /// <summary>Rings per knot interval of the body and head tubes.</summary>
        public readonly int Rings;

        /// <summary>Rings per knot interval of limbs and tails.</summary>
        public readonly int LimbRings;

        /// <summary>Eye detail: 2 = eyeball, pupil, catch-light and lid; 1 = eyeball and catch-light; 0 = a dark dot.</summary>
        public readonly int Eyes;

        /// <summary>Knot decimation of the lofted tubes: 1 keeps every knot, 2 every other one (the far levels).</summary>
        public readonly int KnotStep;

        public FaunaDetail(int lod, int bodySegs, int limbSegs, int smallSegs, int rings, int limbRings, int eyes, int knotStep = 1)
        {
            Lod = lod;
            BodySegs = bodySegs;
            LimbSegs = limbSegs;
            SmallSegs = smallSegs;
            Rings = rings;
            LimbRings = limbRings;
            Eyes = eyes;
            KnotStep = knotStep < 1 ? 1 : knotStep;
        }

        /// <summary>True from the mid level on: drop small parts that only read up close (teats, toes, lids).</summary>
        public bool Coarse
        {
            get { return Lod >= 1; }
        }

        /// <summary>True for the near level, which models the small details (nostrils, dew claws, teats, lids).</summary>
        public bool Fine
        {
            get { return Lod == 0; }
        }

        /// <summary>True for the far level, which leaves out what cannot be seen beyond 30 m.</summary>
        public bool Far
        {
            get { return Lod >= 2; }
        }

        /// <summary>Mammal tessellation per level.</summary>
        public static FaunaDetail Mammal(int lod)
        {
            switch (lod <= 0 ? 0 : lod >= 2 ? 2 : 1)
            {
                case 0: return new FaunaDetail(0, 12, 7, 6, 2, 1, 2);
                case 1: return new FaunaDetail(1, 8, 5, 4, 1, 1, 1, 2);
                default: return new FaunaDetail(2, 5, 4, 3, 1, 1, 0, 3);
            }
        }

        /// <summary>Bird tessellation per level (W2_DESIGN 5.6: a 300-triangle full bird within 8 m, an 80-triangle light
        /// bird to 25 m; beyond that the 4-triangle paper bird): 8 segments round the body loft with an iris-and-pupil
        /// eye, then a 5-sided loft without eyes.</summary>
        public static FaunaDetail Avian(int lod)
        {
            switch (lod <= 0 ? 0 : lod >= 2 ? 2 : 1)
            {
                case 0: return new FaunaDetail(0, 8, 4, 4, 1, 1, 1);
                case 1: return new FaunaDetail(1, 5, 3, 3, 1, 1, 0);
                default: return new FaunaDetail(2, 4, 3, 3, 1, 1, 0);
            }
        }

        /// <summary>Ground-fowl tessellation per level (hens, roosters, ducks: 600 / 160 / 40 tris).</summary>
        public static FaunaDetail Fowl(int lod)
        {
            switch (lod <= 0 ? 0 : lod >= 2 ? 2 : 1)
            {
                case 0: return new FaunaDetail(0, 10, 5, 4, 1, 1, 2);
                case 1: return new FaunaDetail(1, 5, 3, 3, 1, 1, 0, 3);
                default: return new FaunaDetail(2, 4, 3, 3, 1, 1, 0, 3);
            }
        }
    }
}
