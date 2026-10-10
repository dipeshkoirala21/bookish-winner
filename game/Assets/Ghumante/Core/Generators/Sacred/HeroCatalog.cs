using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>The generator form of a hero replica.</summary>
    public enum HeroForm : byte
    {
        Pagoda = 0,
        Mandapa = 1,
        Stupa = 2,
        ShikharaStone = 3,
        ShikharaPlaster = 4,
        HouseTemple = 5,
        Relief = 6,
        Column = 7,
        PalaceTower = 8,
        Dharahara = 9,
        GateHanuman = 10,
        GateGolden = 11,
        PalaceRana = 12,
        PalaceWindows = 13,
        BellPavilion = 14,
        Hiti = 15,
        KumariGhar = 16,
        GoldenTemple = 17,
        PeacockWindow = 18,
    }

    /// <summary>
    /// One hero replica recipe (W2_DESIGN 3.4): the curated record's defaults plus the generator-specific numbers. The
    /// curated DB (<see cref="CuratedDb"/>, D12) overrides tiers, plinth levels, height, yaw, finish and plan when it has
    /// them. Unknown yaw is NaN (the builder then uses the footprint's road-facing side).
    /// </summary>
    public sealed class HeroRecipe
    {
        public string Id, Name, Osm;
        public HeroForm Form;
        public HeritageKind Kind;
        public int Tiers, PlinthLevels, Doors = 1, Storeys;
        public float StepRiseM, HeightM, YawDeg = float.NaN, PlanW, PlanD;
        public RoofFinish Finish;
        public GuardianSet Guardians = GuardianSet.Lions;
        public double Lat = double.NaN, Lon = double.NaN;

        /// <summary>Centrepiece heroes use the class A budget (25,000 / 8,000 / 2,000 / 200), the others class B
        /// (12,000 / 3,000 / 800 / 200; W2_DESIGN 3.2).</summary>
        public bool Centrepiece;

        public float CoreFrac, TierShrink, VahanaDistM;
        public bool VahanaGaruda, Pataka, NoStair, GiltBalcony, Figures;
        public float[] PlinthWidths, EaveWidths, EaveHeights, TerraceWidths, TerraceTops;
        public float DomeDiameterM, DomeRiseM, HarmikaHM, HarmikaWM, SpireHM, ParasolHM, FinialHM, DrumDiameterM;
        public int Terraces, FlagLines, BuddhaNiches, Pavilions1, Pavilions2, Windows, Spouts;

        /// <summary>Stupa look (Boudha, Swayambhu, generic) and the gate distance on the door side (0 = none).</summary>
        public StupaStyle StupaStyle;

        public float GateDistM;
        public bool WheelNiches, EastStair365, CompoundWall;
        public uint RoofColour, WallColour, PlinthColour;

        /// <summary>Detail flags from the reference photos (ref_temples.md): the red eave fringe of the Kathmandu
        /// Durbar Square temples, Taleju's bell gajur, a ring of posts round the sanctum, whitewashed upper storeys with a
        /// balcony (Kasthamandap), lime-washed painted guardians, a white stair balustrade (Maju Dega), a bronze bell and
        /// lamp pillars in front.</summary>
        public bool Fringe, GajurBell, Ambulatory, PlasterUpper, Balcony, PaintedGuardians, WhiteStair, FrontBell;

        public byte LampPillars;

        /// <summary>Strut pitch along the walls (0 = the 1.1 m formula).</summary>
        public float StrutPitchM;

        /// <summary>Roof pitches and the gajur share of the total height (0 = generator defaults).</summary>
        public float PitchBottomDeg, PitchTopDeg, GajurFrac;

        /// <summary>Upper storey width as a fraction of its eave (0 = derived; Kasthamandap's wide hall 0.86).</summary>
        public float UpperWallFrac;

        public HeroRecipe Clone()
        {
            return (HeroRecipe)MemberwiseClone();
        }

        /// <summary>Triangle budget of a LOD (W2_DESIGN 3.2 hero table).</summary>
        public int Budget(int lod)
        {
            lod = lod < 0 ? 0 : lod > 3 ? 3 : lod;
            return Centrepiece ? BudgetA[lod] : BudgetB[lod];
        }

        /// <summary>Hero LOD ceilings, class A (centrepieces) and B (W2_DESIGN 3.2, raised in the W2 detail pass for the
        /// rounded, detailed replicas: carved struts, bells, guardians; HeroLodBudget still fits the slice).</summary>
        public static readonly int[] BudgetA = { 32000, 9000, 2000, 200 }, BudgetB = { 18000, 4500, 800, 200 };
    }

    /// <summary>
    /// The stage-1 hero replicas (W2_DESIGN 3.4 rows marked S1, plus Annapurna at Asan): anchors, plans, heights, tiers,
    /// finishes and door yaws from the design table and temples.md 3-5. Coordinates of node anchors are the OSM centroids
    /// (temples.md 3); "[E]" values are design estimates and "[V]" yaws are pending the cultural review.
    /// </summary>
    public static class HeroCatalog
    {
        private static readonly List<HeroRecipe> S1List = Make();
        private static readonly Dictionary<string, HeroRecipe> ById = Index(S1List);

        public static IReadOnlyList<HeroRecipe> S1
        {
            get { return S1List; }
        }

        public static bool TryGet(string id, out HeroRecipe r)
        {
            return ById.TryGetValue(id ?? "", out r);
        }

        private static Dictionary<string, HeroRecipe> Index(List<HeroRecipe> l)
        {
            var d = new Dictionary<string, HeroRecipe>(StringComparer.Ordinal);
            foreach (HeroRecipe r in l) d[r.Id] = r;
            return d;
        }

        /// <summary>A heritage record (the curated-DB shape) for a recipe, so callers without a DB can build heroes.</summary>
        public static HeritageRecord ToRecord(HeroRecipe r)
        {
            return new HeritageRecord
            {
                Id = r.Id, Name = r.Name, Kind = r.Kind, Osm = r.Osm, Tiers = r.Tiers, PlinthLevels = r.PlinthLevels, Doors = r.Doors,
                HeightM = r.HeightM, YawDeg = r.YawDeg, PlanWM = r.PlanW, PlanDM = r.PlanD, Lat = r.Lat, Lon = r.Lon,
                Finish = r.Finish == RoofFinish.GiltAll ? HeritageFinish.GiltAll : r.Finish == RoofFinish.GiltTop ? HeritageFinish.GiltTop : HeritageFinish.Tile,
                Flags = HeritageFlags.SanctumClosed | HeritageFlags.WalkableCompound | HeritageFlags.NoVehicles,
                Kora = r.Form == HeroForm.Stupa && r.Centrepiece ? KoraDirection.Clockwise : KoraDirection.None,
            };
        }

        private static HeroRecipe Pagoda(string id, string name, string osm, float w, float d, int tiers, int levels, float rise, float height, float yaw)
        {
            return new HeroRecipe
            {
                Id = id, Name = name, Osm = osm, Form = HeroForm.Pagoda, Kind = HeritageKind.Pagoda, PlanW = w, PlanD = d, Tiers = tiers,
                PlinthLevels = levels, StepRiseM = rise, HeightM = height, YawDeg = yaw,
            };
        }

        private static List<HeroRecipe> Make()
        {
            var l = new List<HeroRecipe>();

            // --- Boudhanath and Swayambhunath ---------------------------------------------------------------------
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.boudhanath", Name = "Boudhanath", Osm = "w56688295", Form = HeroForm.Stupa, Kind = HeritageKind.Stupa, Centrepiece = true,
                Lat = 27.721436, Lon = 85.362004, PlanW = 81.5f, PlanD = 81.5f, HeightM = 36f, YawDeg = 180f, DomeDiameterM = 33.5f, DrumDiameterM = 38f,
                Terraces = 3, TerraceWidths = new[] { 81.5f, 62.5f, 50.2f }, TerraceTops = new[] { 4f, 8f, 10.5f }, DomeRiseM = 10.0f,
                HarmikaHM = 4f, HarmikaWM = 7f, SpireHM = 8f, ParasolHM = 1.5f, FinialHM = 1.5f, FlagLines = 64, WheelNiches = true,
                StupaStyle = StupaStyle.Boudha, GateDistM = 46f,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.swayambhunath", Name = "Swayambhunath", Osm = "w201223707", Form = HeroForm.Stupa, Kind = HeritageKind.Stupa, Centrepiece = true,
                Lat = 27.714931, Lon = 85.290391, PlanW = 26.7f, PlanD = 26.7f, HeightM = 33f, YawDeg = 90f, DomeDiameterM = 26.7f, DrumDiameterM = 28.3f,
                Terraces = 0, DomeRiseM = 11f, HarmikaHM = 4f, HarmikaWM = 6f, SpireHM = 12f, ParasolHM = 1.5f, FinialHM = 3.5f, FlagLines = 20,
                BuddhaNiches = 5, EastStair365 = true, StupaStyle = StupaStyle.Swayambhu,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.anantapur", Name = "Anantapur", Osm = "w255752871", Form = HeroForm.ShikharaPlaster, Kind = HeritageKind.ShikharaPlaster,
                PlanW = 8.8f, PlanD = 8.7f, HeightM = 17f, YawDeg = float.NaN, Lat = 27.71478, Lon = 85.29061,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.pratappur", Name = "Pratappur", Osm = "w115376623", Form = HeroForm.ShikharaPlaster, Kind = HeritageKind.ShikharaPlaster,
                PlanW = 7.9f, PlanD = 7.8f, HeightM = 17f, YawDeg = float.NaN, Lat = 27.71513, Lon = 85.29061,
            });

            // --- Pashupatinath ------------------------------------------------------------------------------------
            HeroRecipe pashupati = Pagoda("her.ktm.pashupatinath", "Pashupatinath", "w913170315", 19.5f, 19.2f, 2, 1, 1.5f, 23.7f, 270f);
            pashupati.Centrepiece = true;
            pashupati.Lat = 27.710465;
            pashupati.Lon = 85.348665;
            pashupati.Doors = 4;
            pashupati.Finish = RoofFinish.GiltAll;
            pashupati.EaveWidths = new[] { 18.5f, 13.3f };
            pashupati.VahanaDistM = 6f;
            pashupati.Pataka = true;
            pashupati.FrontBell = true;
            pashupati.LampPillars = 2;
            pashupati.Ambulatory = true;
            l.Add(pashupati);

            // --- Kathmandu Durbar Square ---------------------------------------------------------------------------
            HeroRecipe taleju = Pagoda("her.ktm.taleju", "Taleju (Kathmandu)", "w1414600808", 47.7f, 45.3f, 3, 12, 0.75f, 35f, 190f);
            taleju.Centrepiece = true;
            taleju.Lat = 27.704902;
            taleju.Lon = 85.307958;
            taleju.Finish = RoofFinish.GiltAll;
            taleju.CoreFrac = 0.2f;
            taleju.Pataka = true;
            taleju.CompoundWall = true;
            taleju.GajurBell = true;
            taleju.Fringe = true;
            taleju.RoofColour = Meshing.MeshColor.FromHex(0x96814A); // aged gilt copper, darker than new gilding
            // The twelve stages step in about 1 m each to a 26 m top platform that holds the walled compound.
            taleju.PlinthWidths = new float[12];
            for (int i = 0; i < 12; i++) taleju.PlinthWidths[i] = 47.7f - 2f * 1.0f * i;
            l.Add(taleju);
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.kasthamandap", Name = "Kasthamandap", Osm = "w183558418", Form = HeroForm.Mandapa, Kind = HeritageKind.Mandapa, PlanW = 21.8f,
                PlanD = 21.4f, Tiers = 3, PlinthLevels = 1, StepRiseM = 1.0f, HeightM = 20f, YawDeg = 90f, CoreFrac = 0.78f, Guardians = GuardianSet.None,
                Lat = 27.703934, Lon = 85.305779, Fringe = true, PlasterUpper = true, Balcony = true, PitchBottomDeg = 21f, PitchTopDeg = 30f,
                // A squat hall: a very wide, low first roof over the open ground floor, a white storey with the balcony
                // gallery, a second roof and a small third one (photos: refs/temples/kasthamandap).
                EaveWidths = new[] { 25.5f, 19.0f, 11.0f }, EaveHeights = new[] { 4.8f, 10.8f, 15.6f }, GajurFrac = 0.08f, UpperWallFrac = 0.86f,
            });
            HeroRecipe maju = Pagoda("her.ktm.maju_dega", "Maju Dega", "w183562980", 22.6f, 22.2f, 3, 9, 0.67f, 25f, 90f);
            maju.Lat = 27.704264;
            maju.Lon = 85.306174;
            maju.CoreFrac = 0.35f;
            maju.PlinthColour = Meshing.MeshColor.FromHex(0x9A5A32);
            maju.Ambulatory = true;
            maju.Fringe = true;
            maju.WhiteStair = true;
            l.Add(maju);
            HeroRecipe trailokya = Pagoda("her.ktm.trailokya_mohan", "Trailokya Mohan Narayan", "w1414593331", 13.1f, 12.5f, 3, 5, 0.6f, 18f, 270f);
            trailokya.Lat = 27.703958;
            trailokya.Lon = 85.306275;
            trailokya.VahanaDistM = 6f;
            trailokya.VahanaGaruda = true;
            trailokya.Ambulatory = true;
            trailokya.Fringe = true;
            l.Add(trailokya);
            HeroRecipe jagannath = Pagoda("her.ktm.jagannath", "Jagannath", "w169429518", 14.7f, 14.3f, 2, 3, 0.5f, 15f, 270f);
            jagannath.Lat = 27.704647;
            jagannath.Lon = 85.307220;
            jagannath.Ambulatory = true;
            jagannath.Fringe = true;
            l.Add(jagannath);
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.shiva_parvati", Name = "Shiva-Parvati", Osm = "w185882281", Form = HeroForm.HouseTemple, Kind = HeritageKind.HouseTemple,
                PlanW = 15.8f, PlanD = 10.9f, Storeys = 2, PlinthLevels = 2, StepRiseM = 0.4f, HeightM = 10f, YawDeg = 190f, Figures = true,
                RoofColour = Meshing.MeshColor.FromHex(0xFC8C64), Lat = 27.704413, Lon = 85.306492,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.kal_bhairav", Name = "Kal Bhairav", Osm = "w196261745", Form = HeroForm.Relief, Kind = HeritageKind.Relief, PlanW = 5.8f,
                PlanD = 5.6f, HeightM = 4.05f, YawDeg = 270f, Guardians = GuardianSet.None, Lat = 27.704725, Lon = 85.307092,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.basantapur_tower", Name = "Basantapur tower", Osm = "n6348849285", Form = HeroForm.PalaceTower, Kind = HeritageKind.Tower,
                PlanW = 10f, PlanD = 10f, Storeys = 9, Tiers = 4, HeightM = 30f, YawDeg = 180f, Lat = 27.704008, Lon = 85.307802,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.hanuman_dhoka", Name = "Hanuman Dhoka", Osm = "n11365076769", Form = HeroForm.GateHanuman, Kind = HeritageKind.Gate,
                PlanW = 5f, PlanD = 2.5f, HeightM = 6f, YawDeg = 280f, Lat = 27.704099, Lon = 85.307449,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.kumari_ghar", Name = "Kumari Ghar", Osm = "n2659104413", Form = HeroForm.KumariGhar, Kind = HeritageKind.Bahal,
                PlanW = 22f, PlanD = 22f, Storeys = 3, HeightM = 12f, YawDeg = 0f, Lat = 27.703770, Lon = 85.306524,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.gaddi_baithak", Name = "Gaddi Baithak", Osm = "w247501389", Form = HeroForm.PalaceRana, Kind = HeritageKind.Palace,
                PlanW = 46.4f, PlanD = 36.3f, Storeys = 3, HeightM = 14f, YawDeg = float.NaN, Lat = 27.70420, Lon = 85.30745,
            });

            // --- Asan and Thamel ------------------------------------------------------------------------------------
            HeroRecipe annapurna = Pagoda("her.ktm.annapurna_asan", "Annapurna (Asan Ajima)", "n3569849497", 7f, 7f, 3, 1, 0.45f, 12f, 180f);
            annapurna.Lat = 27.70737;
            annapurna.Lon = 85.31222;
            annapurna.Finish = RoofFinish.GiltAll;
            annapurna.FrontBell = true;
            l.Add(annapurna);
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.kathesimbhu", Name = "Kathesimbhu", Osm = "n3377725834", Form = HeroForm.Stupa, Kind = HeritageKind.Stupa, PlanW = 15f,
                PlanD = 15f, HeightM = 12f, YawDeg = 180f, DomeDiameterM = 11f, Terraces = 1, TerraceWidths = new[] { 15f }, TerraceTops = new[] { 1.0f },
                FlagLines = 16, Lat = 27.709542, Lon = 85.309756, StupaStyle = StupaStyle.Swayambhu,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ktm.dharahara", Name = "Dharahara", Osm = "n11622074774", Form = HeroForm.Dharahara, Kind = HeritageKind.Tower, PlanW = 14.2f,
                PlanD = 14.2f, HeightM = 72f, YawDeg = float.NaN, Lat = 27.700545, Lon = 85.312169,
            });

            // --- Patan ----------------------------------------------------------------------------------------------
            l.Add(new HeroRecipe
            {
                Id = "her.ptn.krishna_mandir", Name = "Krishna Mandir", Osm = "w120443307", Form = HeroForm.ShikharaStone, Kind = HeritageKind.ShikharaStone,
                Centrepiece = true, PlanW = 14.3f, PlanD = 14.2f, PlinthLevels = 3, StepRiseM = 0.33f, HeightM = 20f, YawDeg = 90f, Pavilions1 = 12,
                Pavilions2 = 8, Lat = 27.673623, Lon = 85.324964,
            });
            HeroRecipe ptaleju = Pagoda("her.ptn.taleju", "Taleju (Patan)", "w120443289", 16.9f, 15.3f, 3, 1, 17.5f, 34f, 270f);
            ptaleju.Lat = 27.673202;
            ptaleju.Lon = 85.325223;
            ptaleju.NoStair = true;
            ptaleju.EaveWidths = new[] { 18.3f, 13.5f, 9.4f };
            ptaleju.EaveHeights = new[] { 17.5f, 23.5f, 28.5f };
            ptaleju.CoreFrac = 0.57f;
            ptaleju.Finish = RoofFinish.Tile;
            ptaleju.Guardians = GuardianSet.None;
            l.Add(ptaleju);
            HeroRecipe vish = Pagoda("her.ptn.vishwanath", "Vishwanath", "w328903213", 12.6f, 11.4f, 2, 2, 0.65f, 15f, 90f);
            vish.Lat = 27.673711;
            vish.Lon = 85.325134;
            vish.RoofColour = Meshing.MeshColor.FromHex(0xC27C36);
            vish.Guardians = GuardianSet.Elephants;
            vish.FrontBell = true;
            l.Add(vish);
            l.Add(new HeroRecipe
            {
                Id = "her.ptn.bhimsen", Name = "Bhimsen (Patan)", Osm = "w326472980", Form = HeroForm.HouseTemple, Kind = HeritageKind.HouseTemple,
                PlanW = 11.5f, PlanD = 11.4f, Storeys = 3, PlinthLevels = 1, StepRiseM = 0.5f, HeightM = 14f, YawDeg = 90f, GiltBalcony = true,
                Lat = 27.673873, Lon = 85.325182,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ptn.yoganarendra_column", Name = "Yoganarendra Malla column", Osm = "n2097740385", Form = HeroForm.Column, Kind = HeritageKind.Column,
                PlanW = 2f, PlanD = 2f, HeightM = 8f, YawDeg = 270f, Lat = 27.67350, Lon = 85.32514,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ptn.taleju_bell", Name = "Taleju bell (Patan)", Osm = "w199775523", Form = HeroForm.BellPavilion, Kind = HeritageKind.Gate,
                PlanW = 12.6f, PlanD = 9.0f, HeightM = 6.5f, YawDeg = float.NaN, Lat = 27.67330, Lon = 85.32505,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ptn.manga_hiti", Name = "Manga Hiti", Osm = "n10034234987", Form = HeroForm.Hiti, Kind = HeritageKind.Hiti, PlanW = 12f, PlanD = 8f,
                HeightM = 0.5f, Spouts = 3, YawDeg = 180f, Lat = 27.67390, Lon = 85.32530,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.ptn.golden_temple", Name = "Golden Temple (Kwa Bahal)", Osm = "r4624856", Form = HeroForm.GoldenTemple, Kind = HeritageKind.Bahal,
                PlanW = 26.1f, PlanD = 25.0f, Tiers = 3, HeightM = 15f, YawDeg = 90f, Finish = RoofFinish.GiltAll, Lat = 27.675224, Lon = 85.324602,
            });
            HeroRecipe kumbh = Pagoda("her.ptn.kumbheshwar", "Kumbheshwar", "n1759661519", 12f, 12f, 5, 2, 0.75f, 25f, 90f);
            kumbh.Lat = 27.676581;
            kumbh.Lon = 85.326046;
            kumbh.TierShrink = 0.76f;
            kumbh.Finish = RoofFinish.GiltTop;
            kumbh.Pataka = true;
            kumbh.VahanaDistM = 5f;
            kumbh.FrontBell = true;
            l.Add(kumbh);

            // --- Bhaktapur ------------------------------------------------------------------------------------------
            HeroRecipe nyatapola = Pagoda("her.bkt.nyatapola", "Nyatapola", "w85470341", 24.2f, 21.4f, 5, 5, 1.4f, 33.2f, 180f);
            nyatapola.Centrepiece = true;
            nyatapola.Lat = 27.671410;
            nyatapola.Lon = 85.429373;
            nyatapola.PlinthWidths = new[] { 24.2f, 21.7f, 18.1f, 15.4f, 10.9f };
            nyatapola.EaveWidths = new[] { 19.3f, 16.3f, 12.6f, 9.3f, 5.8f };
            nyatapola.EaveHeights = new[] { 11.6f, 16.4f, 20.7f, 24.7f, 29.0f };
            nyatapola.CoreFrac = 0.45f;
            nyatapola.Guardians = GuardianSet.Nyatapola;
            nyatapola.RoofColour = Meshing.MeshColor.FromHex(0x7A2E22);
            nyatapola.Ambulatory = true;
            nyatapola.StrutPitchM = 1.9f;
            nyatapola.GajurFrac = 0.045f; // roof to 31.7 m + a 1.5 m finial (temples.md 4.6)
            l.Add(nyatapola);
            HeroRecipe bhairav = Pagoda("her.bkt.bhairavnath", "Bhairavnath", "w185746728", 16.9f, 14.6f, 3, 1, 1.0f, 20f, 270f);
            bhairav.Lat = 27.671094;
            bhairav.Lon = 85.429470;
            bhairav.Pataka = true;
            bhairav.Ambulatory = true;
            bhairav.FrontBell = true;
            l.Add(bhairav);
            l.Add(new HeroRecipe
            {
                Id = "her.bkt.fifty_five_window_palace", Name = "55-Window Palace", Osm = null, Form = HeroForm.PalaceWindows, Kind = HeritageKind.Palace, PlanW = 50f,
                PlanD = 12f, Storeys = 3, HeightM = 12f, YawDeg = 180f, Windows = 55, Lat = 27.67235, Lon = 85.42830,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.bkt.golden_gate", Name = "Golden Gate", Osm = "n11365076869", Form = HeroForm.GateGolden, Kind = HeritageKind.Gate, PlanW = 4f,
                PlanD = 2.5f, HeightM = 5.5f, YawDeg = 180f, Lat = 27.672172, Lon = 85.428616,
            });
            l.Add(new HeroRecipe
            {
                Id = "her.bkt.vatsala_durga", Name = "Vatsala Durga", Osm = "w211082585", Form = HeroForm.ShikharaStone, Kind = HeritageKind.ShikharaStone,
                PlanW = 7.3f, PlanD = 6.1f, PlinthLevels = 3, StepRiseM = 0.4f, HeightM = 14f, YawDeg = 180f, Pavilions1 = 8, Pavilions2 = 4,
                Lat = 27.672191, Lon = 85.428777,
            });
            HeroRecipe datta = Pagoda("her.bkt.dattatreya", "Dattatreya", "n11365112169", 12f, 12f, 3, 2, 0.6f, 18f, 270f);
            datta.Lat = 27.673539;
            datta.Lon = 85.435359;
            datta.Guardians = GuardianSet.Wrestlers;
            datta.Ambulatory = true;
            datta.LampPillars = 2;
            l.Add(datta);
            l.Add(new HeroRecipe
            {
                Id = "her.bkt.peacock_window", Name = "Peacock Window", Osm = null, Form = HeroForm.PeacockWindow, Kind = HeritageKind.None, PlanW = 4f,
                PlanD = 0.5f, HeightM = 4f, YawDeg = 180f, Lat = 27.6732, Lon = 85.4356,
            });
            return l;
        }
    }
}
