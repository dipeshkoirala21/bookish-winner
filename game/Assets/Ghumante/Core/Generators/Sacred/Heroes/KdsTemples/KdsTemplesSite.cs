using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>The structures of Kathmandu Durbar Square's temples and square (docs/research/heritage/kds-temples.md §3).</summary>
    internal enum KdsPart : byte
    {
        Kasthamandap,
        SilyanSattal,
        AshokBinayak,
        MajuDega,
        WhiteShikhara,
        NarayanRuin,
        ShivaParvati,
        TrailokyaMohan,
        Dabali,
        LaxmiNarayanSattal,
        TalejuBell,
        Saraswati,
        ChyasinDega,
        BigDrums,
        LakshmiNarayan,
        KalBhairav,
        Jagannath,
        PratapMallaColumn,
        Indrapur,
        Gopinath,
        Kotilingeshwar,
        Kageshwor,
        Mahavishnu,
        Mahendreshwar,
        Bhagwati,
        SwetaBhairav,
        BasantapurSquare,
        ShivaNorth,
        ShivaWest,
        SquarePaving,
        Kavindrapur,
        ShivaMandira1,
        ShivaMandira2,
        GorakhnathPipal,
        Kashinath,
        NilaVarahi,
    }

    /// <summary>
    /// The bespoke Kathmandu Durbar Square temples and square (W2_PLAYTEST_1 item 17; docs/research/heritage/kds-temples.md):
    /// every free-standing temple, statue, bell, drum house, rest house, platform and the Basantapur square, each built
    /// structure by structure from the DANAM measured drawings and 2023-2026 photos, at its true OSM footprint, as its own
    /// hero (its own LODs in the hero allocator). The six heroes that existed keep their ids (Kasthamandap, Maju Dega,
    /// Trailokya Mohan, Jagannath, Shiva-Parvati and Kal Bhairav, the last moved to its true footprint facing north); the
    /// others are new parts (<see cref="HeroRecipe.SiteOf"/>) that the hero set adds with the curated Kasthamandap record.
    /// Positions: the OSM outline in the anchor tile when the structure has one (its oriented box, the door square to it),
    /// else the brief's local frame (origin 27.7045 N, 85.3075 E; x east, y north). Engine-free and deterministic.
    /// </summary>
    public static class KdsTemplesSite
    {
        /// <summary>The curated hero the new parts belong to (always in the region's curated DB).</summary>
        public const string SiteHeroId = "her.ktm.kasthamandap";

        /// <summary>Origin of the brief's local frame.</summary>
        public const double OriginLat = 27.7045, OriginLon = 85.3075;

        /// <summary>Metres per degree of latitude and longitude at the origin (kds-temples.md §2.1).</summary>
        public const double MPerDegLat = 110574.0, MPerDegLon = 98573.0;

        internal sealed class Entry
        {
            public string Id, Name, Osm;
            public KdsPart Part;
            public HeritageKind Kind;

            /// <summary>Brief local position (m east, m north) and the door bearing (degrees clockwise from north).</summary>
            public double X, Y;

            public float YawDeg, TopM;

            /// <summary>Plan (m) when there is no outline.</summary>
            public float PlanW, PlanD;

            /// <summary>Tiers and plinth stages as built (0 = none stated).</summary>
            public int Tiers, Plinths;

            /// <summary>Extra OSM buildings this structure replaces (hidden from the house bands).</summary>
            public string[] Hide;

            /// <summary>Class: 0 major temple, 1 temple, 2 small structure, 3 the square.</summary>
            public int Class;

            /// <summary>True for the heroes that existed before this package (their ids are kept).</summary>
            public bool Existing;

            /// <summary>An unmapped structure placed relative to a mapped one (its entry id): the brief's offset between the two
            /// is applied to the mapped one's outline centre, and the bearing follows that outline's axes.</summary>
            public string Rel;

            /// <summary>An unmapped structure's radius (m) kept clear of the road corridors: placed by the brief, it is
            /// nudged (up to 3 m) out of a corridor it would stand in (0 = not nudged).</summary>
            public double KeepClearM;

            /// <summary>Radius (m) of the structure's level pad round its brief position (<see cref="KdsGround"/>; 0 = none):
            /// the outline's half diagonal and what stands beside it (stairs, lions, the Garuda), plus the brief's offset.</summary>
            public double PadR;
        }

        // LOD0 / LOD1 / LOD2 / LOD3 ceilings by class (heritage heroes: about 60 k per major temple at LOD0, LOD1 about
        // 30 %, LOD2 about 8 %, the far stand-in under 200).
        private static readonly int[][] Budgets =
        {
            new[] { 64000, 20000, 6000, 220 }, new[] { 34000, 11000, 3200, 220 }, new[] { 16000, 5200, 1600, 220 }, new[] { 30000, 9000, 2400, 220 },
        };

        private static readonly Entry[] Table = MakeTable();
        private static readonly Dictionary<string, Entry> ById = Index();

        private static Entry E(string id, string name, KdsPart part, HeritageKind kind, string osm, double x, double y, float yaw, float top, int cls,
                               params string[] hide)
        {
            return new Entry { Id = id, Name = name, Part = part, Kind = kind, Osm = osm, X = x, Y = y, YawDeg = yaw, TopM = top, Class = cls, Hide = hide };
        }

        private static Entry[] MakeTable()
        {
            var l = new List<Entry>
            {
                // --- existing heroes (ids kept) ---
                E("her.ktm.kasthamandap", "Kasthamandap", KdsPart.Kasthamandap, HeritageKind.Mandapa, "w183558418", -169.7, -62.6, 101.6f, Kasthamandap.TopM, 0,
                  "w1416709264"),
                E("her.ktm.maju_dega", "Maju Dega", KdsPart.MajuDega, HeritageKind.Pagoda, "w183562980", -130.7, -26.1, 106f, MajuDega.TopM, 0, "w908861401",
                  "w1415139993", "w1415139994", "w1415140479", "w1415349760", "w246110126", "w246110133", "w246110142", "w246110143", "w246110145",
                  "w246110418", "w247405487", "w248254287", "w248256020", "w248794188", "w420245279", "w420245280", "w420245281", "w420245283",
                  "w420245285", "w420245286", "w420245287", "w420245288", "w420286758", "w420286760", "w420286761", "w420286762", "w420286764",
                  "w420286765", "w420286766", "w420286767", "w420286768", "w420286769", "w420286770", "w420286771", "w420286772", "w420286773",
                  "w420286774", "w420286775", "w420286786", "w420286787", "w420286788", "w420286790", "w420286798"),
                E("her.ktm.trailokya_mohan", "Trailokya Mohan Narayan", KdsPart.TrailokyaMohan, HeritageKind.Pagoda, "w1414593331", -120.8, -59.9, 280.5f,
                  TrailokyaMohan.TopM, 0, "w111984928", "w420797427", "w429890385", "w1414593332", "w1414593333", "w1414593334", "w1415125978",
                  "w1415349761", "w1415349762", "w1416278296", "w1416278297", "w1416686188", "w1418075022", "w429890379", "w429890322", "w429890323",
                  "w429890324", "w429890325", "w429890327", "w429890330", "w429890331", "w429890334", "w429890358", "w429890363", "w429890368",
                  "w429890370", "w429890371", "w429890372", "w429890374", "w429890375", "w429890376", "w429890377", "w429890378", "w429890380"),
                E("her.ktm.jagannath", "Jagannath", KdsPart.Jagannath, HeritageKind.Pagoda, "w169429518", -27.6, 16.2, 100.3f, Jagannath.TopM, 0),
                E("her.ktm.shiva_parvati", "Shiva-Parvati", KdsPart.ShivaParvati, HeritageKind.HouseTemple, "w185882281", -99.4, -9.6, 198.8f, ShivaParvati.TopM, 1,
                  "w420291371", "w420291378", "w420291382", "w420291386", "w420291390", "w420291391", "w420291393", "w420291394", "w420291395",
                  "w420291396", "w420291397", "w420291398", "w420291399", "w420291400", "w420291402", "w420291403", "w420292573", "w420292574",
                  "w420292575", "w420292576"),
                E("her.ktm.kal_bhairav", "Kal Bhairav", KdsPart.KalBhairav, HeritageKind.Relief, "w908861416", -38.9, 30.9, 10.7f, KalBhairav.TopM, 1),

                // --- new structures ---
                E("her.ktm.kds_silyan_sattal", "Silyan Sattal", KdsPart.SilyanSattal, HeritageKind.Mandapa, "w183559499", -173.2, -95.9, 188f, Pavilions.SilyanTopM, 1),
                E("her.ktm.kds_ashok_binayak", "Ashok Binayak", KdsPart.AshokBinayak, HeritageKind.Pagoda, null, -158.6, -47.0, 191f, Pavilions.AshokTopM, 2),
                E("her.ktm.kds_white_shikhara", "Shiva shikhara (Maju Dega stair)", KdsPart.WhiteShikhara, HeritageKind.ShikharaPlaster, "w1028854863", -113.7,
                  -37.4, 106f, StoneShrines.WhiteShikharaTopM, 2, "w420286759", "w420286785", "w420286734", "w420286741", "w420286746", "w420286749",
                  "w420286751", "w420286753", "w420286754", "w420286755", "w420286757", "w420286776", "w420286777", "w420286778", "w420286779",
                  "w420286780", "w420286781", "w420286782", "w420286783", "w420286784"),
                E("her.ktm.kds_narayan_ruin", "Narayan temple (ruin)", KdsPart.NarayanRuin, HeritageKind.Pagoda, "w111984927", -120.1, -5.4, 200.8f, Basantapur.RuinTopM, 2),
                E("her.ktm.kds_dabali", "Basantapur Dabali", KdsPart.Dabali, HeritageKind.None, "w906786371", -111.0, -61.5, 281.5f, Basantapur.DabaliTopM, 2),
                E("her.ktm.kds_laxmi_narayan_sattal", "Laxmi Narayan Sattal", KdsPart.LaxmiNarayanSattal, HeritageKind.Mandapa, "w183559497", -145.0, -50.3, 188.8f,
                  Pavilions.LaxmiSattalTopM, 1),
                E("her.ktm.kds_taleju_bell", "Taleju Bell", KdsPart.TalejuBell, HeritageKind.Gate, "w183563646", -79.2, 8.6, 111f, Pavilions.BellTopM, 2),
                E("her.ktm.kds_saraswati", "Saraswati phalcha", KdsPart.Saraswati, HeritageKind.Mandapa, "w907080044", -71.2, 15.0, 120.7f, Pavilions.SaraswatiTopM, 2),
                E("her.ktm.kds_chyasin_dega", "Chyasin Dega (Krishna)", KdsPart.ChyasinDega, HeritageKind.Pagoda, "w907080047", -63.1, 27.8, 100f, ChyasinDega.TopM, 0),
                E("her.ktm.kds_big_drums", "Big Drums", KdsPart.BigDrums, HeritageKind.Gate, "w907080046", -60.9, 37.7, 107f, Pavilions.DrumsTopM, 2),
                E("her.ktm.kds_lakshmi_narayan", "Lakshmi Narayan temple", KdsPart.LakshmiNarayan, HeritageKind.Pagoda, "w196261745", -40.2, 24.8, 281.7f,
                  ForecourtTemples.LakshmiNarayanTopM, 2),
                E("her.ktm.kds_pratap_malla_column", "Pratap Malla's column", KdsPart.PratapMallaColumn, HeritageKind.Column, null, -19.8, 14.7, 100.3f,
                  Jagannath.ColumnTopM, 2),
                E("her.ktm.kds_indrapur", "Indrapur", KdsPart.Indrapur, HeritageKind.Pagoda, "w907080039", -28.7, 34.3, 192.9f, ForecourtTemples.IndrapurTopM, 1),
                E("her.ktm.kds_gopinath", "Gopinath (Shri Krishna Mahavishnu)", KdsPart.Gopinath, HeritageKind.Pagoda, "w169429514", -19.0, 32.0, 101.9f,
                  ForecourtTemples.GopinathTopM, 1),
                E("her.ktm.kds_kotilingeshwar", "Kotilingeshwar Mahadev", KdsPart.Kotilingeshwar, HeritageKind.ShikharaStone, null, -29.5, 63.5, 200f,
                  StoneShrines.KotilingeshwarTopM, 1),
                E("her.ktm.kds_kageshwor", "Kageshwor Mahadev", KdsPart.Kageshwor, HeritageKind.Pagoda, "w169429525", -7.3, 53.2, 283.7f, ForecourtTemples.KageshworTopM, 1,
                  "w1414590540", "w1414590541", "w1414590542"),
                E("her.ktm.kds_mahavishnu", "Mahavishnu", KdsPart.Mahavishnu, HeritageKind.Pagoda, "w1415081497", -15.5, 72.2, 191.4f, ForecourtTemples.MahavishnuTopM, 1),
                E("her.ktm.kds_mahendreshwar", "Mahendreshwar", KdsPart.Mahendreshwar, HeritageKind.Pagoda, "w907048006", 12.7, 91.6, 184.2f, Mahendreshwar.TopM, 1,
                  "w907048005"),
                E("her.ktm.kds_bhagwati", "Bhagwati temple", KdsPart.Bhagwati, HeritageKind.Pagoda, "w1415152800", -79.0, -16.0, 305.4f, Bhagwati.TopM, 1),
                E("her.ktm.kds_sweta_bhairav", "Sweta Bhairav", KdsPart.SwetaBhairav, HeritageKind.Relief, null, -3.6, 17.6, 280.7f, KalBhairav.SwetaTopM, 2),
                E("her.ktm.kds_basantapur_square", "Basantapur square", KdsPart.BasantapurSquare, HeritageKind.None, null, -45.0, -96.0, 191.6f, Basantapur.SquareTopM, 3),
                E("her.ktm.kds_shiva_north", "Shiva temple (Kasthamandap north)", KdsPart.ShivaNorth, HeritageKind.Pagoda, "w196809462", -176.6, -44.5, 188.3f,
                  StoneShrines.ShivaNorthTopM, 2),
                E("her.ktm.kds_shiva_west", "Shiva temple (Kasthamandap west)", KdsPart.ShivaWest, HeritageKind.Pagoda, "w196809346", -187.9, -73.6, 108.3f,
                  StoneShrines.ShivaWestTopM, 2),

                // --- judge round 1: the square's flag paving, the missing monuments ---
                E("her.ktm.kds_square_paving", "Durbar Square paving", KdsPart.SquarePaving, HeritageKind.None, null, -130.0, -40.0, 191.6f, SquarePaving.TopM, 3),
                // Kavindrapur (KAT2000) on its outline; the modern house north of it (w1416684423) stays (photos 04, 07).
                E("her.ktm.kds_kavindrapur", "Kavindrapur (Kabindrapur)", KdsPart.Kavindrapur, HeritageKind.HouseTemple, "w906786393", -153.0, -87.6, 281.5f,
                  Kavindrapur.TopM, 1),
                // The north-east corner (KAT1550/1551/1552/1560 site plans, offsets from Shiva Mandira 2's outline).
                E("her.ktm.kds_shiva_mandira_2", "Shiva Mandira 2", KdsPart.ShivaMandira2, HeritageKind.Pagoda, "w907048009", 7.5, 45.1, 284.2f,
                  NorthShrines.ShivaMandira2TopM, 2),
                E("her.ktm.kds_shiva_mandira_1", "Shiva Mandira 1", KdsPart.ShivaMandira1, HeritageKind.Pagoda, null, 12.2, 59.0, 284.2f, NorthShrines.ShivaMandira1TopM, 2),
                E("her.ktm.kds_gorakhnath_pipal", "Gorakhnath shrine, pipal and Pira Pati", KdsPart.GorakhnathPipal, HeritageKind.Relief, null, 13.0, 64.1, 284.2f,
                  Gorakhnath.TopM, 2),
                // Behind Mahavishnu (KAT1521, KAT1522): Kashinath on its outline, Nila Varahi against the house north of it.
                E("her.ktm.kds_kashinath", "Kashinath", KdsPart.Kashinath, HeritageKind.Pagoda, "w907080024", -10.2, 82.6, 194.5f, NorthShrines.KashinathTopM, 2),
                E("her.ktm.kds_nila_varahi", "Nila Varahi (Varaha)", KdsPart.NilaVarahi, HeritageKind.Pagoda, null, -22.1, 83.1, 191.4f, NorthShrines.NilaVarahiTopM, 2),
            };
            foreach (Entry e in l)
            {
                if (e.Part == KdsPart.AshokBinayak)
                {
                    e.Rel = "her.ktm.kasthamandap";
                    // The cage corners (2.25 × 2.35 m half sizes) stay clear of the lane's corridor.
                    e.KeepClearM = 3.3;
                }
                if (e.Part == KdsPart.PratapMallaColumn) e.KeepClearM = 1.3;
                if (e.Part == KdsPart.PratapMallaColumn) e.Rel = "her.ktm.jagannath";
                if (e.Part == KdsPart.Kotilingeshwar) e.Rel = "her.ktm.kds_mahavishnu";
                if (e.Part == KdsPart.BasantapurSquare) e.Rel = "her.ktm.kds_dabali";
                if (e.Part == KdsPart.ShivaMandira1 || e.Part == KdsPart.GorakhnathPipal) e.Rel = "her.ktm.kds_shiva_mandira_2";
                if (e.Part == KdsPart.NilaVarahi) e.Rel = "her.ktm.kds_mahavishnu";
                e.Existing = e.Id == "her.ktm.kasthamandap" || e.Id == "her.ktm.maju_dega" || e.Id == "her.ktm.trailokya_mohan" || e.Id == "her.ktm.jagannath" ||
                             e.Id == "her.ktm.shiva_parvati" || e.Id == "her.ktm.kal_bhairav";
                switch (e.Part)
                {
                    case KdsPart.Kasthamandap:
                        e.Tiers = 3;
                        e.Plinths = 2;
                        break;
                    case KdsPart.MajuDega:
                        e.Tiers = 3;
                        e.Plinths = 9;
                        break;
                    case KdsPart.TrailokyaMohan:
                        e.Tiers = 3;
                        e.Plinths = 5;
                        break;
                    case KdsPart.Jagannath:
                        e.Tiers = 2;
                        e.Plinths = 3;
                        break;
                    case KdsPart.ChyasinDega:
                        e.Tiers = 3;
                        break;
                    case KdsPart.Mahavishnu:
                    case KdsPart.Gopinath:
                        e.Tiers = 3;
                        break;
                    case KdsPart.Indrapur:
                    case KdsPart.LakshmiNarayan:
                    case KdsPart.Mahendreshwar:
                    case KdsPart.ShivaMandira1:
                    case KdsPart.ShivaMandira2:
                    case KdsPart.Kashinath:
                        e.Tiers = 2;
                        break;
                    case KdsPart.Kavindrapur:
                        e.Tiers = 3;
                        break;
                }
                e.PlanW = e.PlanD = 8f;
                e.PadR = PadRadius(e.Part);
            }
            return l.ToArray();
        }

        /// <summary>The level pad's radius of a structure (m): the half diagonal of its outline (placement of the sample
        /// tile) with what stands beside it, plus the brief position's offset from the outline (up to 3 m).</summary>
        private static double PadRadius(KdsPart p)
        {
            switch (p)
            {
                case KdsPart.Kasthamandap: return 16.0;
                case KdsPart.MajuDega: return 16.0;
                case KdsPart.TrailokyaMohan: return 13.0; // the Garuda 10.7 m west of the centre
                case KdsPart.Jagannath: return 11.0;
                case KdsPart.ShivaParvati: return 10.0;
                case KdsPart.KalBhairav: return 5.0;
                case KdsPart.SilyanSattal: return 9.5;
                case KdsPart.AshokBinayak: return 6.0;
                case KdsPart.WhiteShikhara: return 2.5;
                case KdsPart.NarayanRuin: return 9.5;
                case KdsPart.Dabali: return 8.5;
                case KdsPart.LaxmiNarayanSattal: return 8.5;
                case KdsPart.TalejuBell: return 6.0;
                case KdsPart.Saraswati: return 4.5;
                case KdsPart.ChyasinDega: return 9.0;
                case KdsPart.BigDrums: return 5.0;
                case KdsPart.LakshmiNarayan: return 4.5;
                case KdsPart.PratapMallaColumn: return 1.5;
                case KdsPart.Indrapur: return 6.5;
                case KdsPart.Gopinath: return 7.5;
                case KdsPart.Kotilingeshwar: return 6.0;
                case KdsPart.Kageshwor: return 8.0;
                case KdsPart.Mahavishnu: return 9.0;
                case KdsPart.Mahendreshwar: return 9.0;
                case KdsPart.Bhagwati: return 15.5;
                case KdsPart.SwetaBhairav: return 0;
                case KdsPart.BasantapurSquare: return 0;
                case KdsPart.ShivaNorth: return 6.0;
                case KdsPart.ShivaWest: return 4.5;
                case KdsPart.Kavindrapur: return 9.5;
                case KdsPart.ShivaMandira1: return 3.5;
                case KdsPart.ShivaMandira2: return 4.5;
                case KdsPart.GorakhnathPipal: return 9.5; // the shrine, the chautara and Pira Pati 6 m north of it
                case KdsPart.Kashinath: return 5.0;
                case KdsPart.NilaVarahi: return 2.5;
                default: return 0;
            }
        }

        private static Dictionary<string, Entry> Index()
        {
            var d = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (Entry e in Table) d[e.Id] = e;
            return d;
        }

        /// <summary>True when the hero is one of this site's bespoke structures.</summary>
        public static bool Handles(string id)
        {
            return id != null && ById.ContainsKey(id);
        }

        /// <summary>The table entry of a structure.</summary>
        internal static bool TryEntry(string id, out Entry e)
        {
            return ById.TryGetValue(id ?? "", out e);
        }

        /// <summary>Every structure's table entry (existing heroes first).</summary>
        internal static Entry[] Entries
        {
            get { return Table; }
        }

        /// <summary>The ids of every structure of the site (existing heroes first).</summary>
        public static IEnumerable<string> Ids
        {
            get
            {
                foreach (Entry e in Table) yield return e.Id;
            }
        }

        /// <summary>The OSM refs a structure rebuilds (its footprint and the extra parts), for the hide zones.</summary>
        public static string[] HideRefs(string id)
        {
            Entry e;
            if (!ById.TryGetValue(id ?? "", out e)) return new string[0];
            var l = new List<string>();
            if (!string.IsNullOrEmpty(e.Osm)) l.Add(e.Osm);
            if (e.Hide != null) l.AddRange(e.Hide);
            return l.ToArray();
        }

        /// <summary>Per-LOD triangle ceilings of a structure (LOD0..3).</summary>
        public static int[] Budget(string id)
        {
            Entry e;
            return ById.TryGetValue(id ?? "", out e) ? (int[])Budgets[e.Class].Clone() : null;
        }

        /// <summary>The brief's local (x east, y north) metres as WGS84.</summary>
        public static void LocalToLonLat(double x, double y, out double lon, out double lat)
        {
            lat = OriginLat + y / MPerDegLat;
            lon = OriginLon + x / MPerDegLon;
        }

        /// <summary>
        /// Brings the catalogue in line with the bespoke site (called once at the end of <see cref="HeroCatalog"/>'s list):
        /// the six existing KDS recipes become bespoke structures with their measured height, door bearing, footprint,
        /// budgets and hide lists (Kal Bhairav moves to its true footprint, OSM way 908861416), and the new structures are
        /// appended as parts of <see cref="SiteHeroId"/>.
        /// </summary>
        public static void Apply(List<HeroRecipe> catalogue)
        {
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            foreach (Entry e in Table)
            {
                HeroRecipe r = null;
                for (int i = 0; i < catalogue.Count; i++)
                    if (catalogue[i].Id == e.Id)
                        r = catalogue[i];
                double lon, lat;
                LocalToLonLat(e.X, e.Y, out lon, out lat);
                if (r == null)
                {
                    r = new HeroRecipe { Id = e.Id, Name = e.Name, Kind = e.Kind, SiteOf = SiteHeroId };
                    catalogue.Add(r);
                }
                r.Form = HeroForm.Bespoke;
                r.Osm = e.Osm;
                r.Lat = lat;
                r.Lon = lon;
                r.HeightM = e.TopM;
                r.YawDeg = e.YawDeg;
                r.Tiers = e.Tiers;
                r.PlinthLevels = e.Plinths;
                if (r.PlanW <= 0) r.PlanW = e.PlanW;
                if (r.PlanD <= 0) r.PlanD = e.PlanD;
                // The Basantapur square is open ground (pigeon flocks, stalls and crowds on it), not a monument the animals
                // keep out of (Generators/Fauna FaunaHeroZones reads the plan).
                if (e.Part == KdsPart.BasantapurSquare || e.Part == KdsPart.SquarePaving) r.PlanW = r.PlanD = 0f;
                r.Measured = true;
                r.BudgetTris = (int[])Budgets[e.Class].Clone();
                r.Hide = HideRefs(e.Id);
                r.Centrepiece = e.Class == 0;
            }
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Build
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds one structure of the site into <paramref name="m"/> at a LOD (0..3; tile-local metres with absolute Y),
        /// its LOD0 colliders into <paramref name="c"/> and its stats. Returns the triangles, 0 when the structure does not
        /// stand in this tile.
        /// </summary>
        public static int Build(HeritageRecord r, TileData tile, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (m == null) throw new ArgumentNullException(nameof(m));
            Entry e;
            if (!ById.TryGetValue(r.Id, out e)) return 0;
            KdsCtx x;
            if (!Locate(e, tile, lod, m, c, stats, out x)) return 0;
            stats?.Clear();
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            m.HasUv0 = true;
            double top = Dispatch(e, x);
            SacredDraw.Bake(m, v0, i0, x.F.GroundY);
            if (stats != null)
            {
                stats.TopM = (float)top;
                stats.DoorYawDeg = x.F.YawDeg;
                stats.Tiers = e.Tiers;
                stats.PlinthLevels = e.Plinths;
            }
            return m.TriangleCount - t0;
        }

        /// <summary>Builds a located structure into its context's mesh (the tests read the context afterwards: its eaves,
        /// the clearance still missing under pulled-back eaves).</summary>
        internal static double BuildIn(Entry e, KdsCtx x)
        {
            return Dispatch(e, x);
        }

        private static double Dispatch(Entry e, KdsCtx x)
        {
            switch (e.Part)
            {
                case KdsPart.Kasthamandap: return Kasthamandap.Build(x);
                case KdsPart.MajuDega: return MajuDega.Build(x);
                case KdsPart.WhiteShikhara: return StoneShrines.WhiteShikhara(x);
                case KdsPart.TrailokyaMohan: return TrailokyaMohan.Build(x);
                case KdsPart.Jagannath: return Jagannath.Build(x);
                case KdsPart.PratapMallaColumn: return Jagannath.Column(x);
                case KdsPart.ShivaParvati: return ShivaParvati.Build(x);
                case KdsPart.KalBhairav: return KalBhairav.Build(x);
                case KdsPart.SwetaBhairav: return KalBhairav.Sweta(x);
                case KdsPart.ChyasinDega: return ChyasinDega.Build(x);
                case KdsPart.LakshmiNarayan: return ForecourtTemples.LakshmiNarayan(x);
                case KdsPart.Indrapur: return ForecourtTemples.Indrapur(x);
                case KdsPart.Gopinath: return ForecourtTemples.Gopinath(x);
                case KdsPart.Mahavishnu: return ForecourtTemples.Mahavishnu(x);
                case KdsPart.Kageshwor: return ForecourtTemples.Kageshwor(x);
                case KdsPart.Kotilingeshwar: return StoneShrines.Kotilingeshwar(x);
                case KdsPart.ShivaNorth: return StoneShrines.ShivaNorth(x);
                case KdsPart.ShivaWest: return StoneShrines.ShivaWest(x);
                case KdsPart.Mahendreshwar: return Mahendreshwar.Build(x);
                case KdsPart.Bhagwati: return Bhagwati.Build(x);
                case KdsPart.SilyanSattal: return Pavilions.Silyan(x);
                case KdsPart.LaxmiNarayanSattal: return Pavilions.LaxmiSattal(x);
                case KdsPart.Saraswati: return Pavilions.Saraswati(x);
                case KdsPart.TalejuBell: return Pavilions.Bell(x);
                case KdsPart.BigDrums: return Pavilions.Drums(x);
                case KdsPart.AshokBinayak: return Pavilions.AshokBinayak(x);
                case KdsPart.NarayanRuin: return Basantapur.NarayanRuin(x);
                case KdsPart.Dabali: return Basantapur.Dabali(x);
                case KdsPart.BasantapurSquare: return Basantapur.Square(x);
                case KdsPart.SquarePaving: return SquarePaving.Build(x);
                case KdsPart.Kavindrapur: return Kavindrapur.Build(x);
                case KdsPart.ShivaMandira1: return NorthShrines.ShivaMandira1(x);
                case KdsPart.ShivaMandira2: return NorthShrines.ShivaMandira2(x);
                case KdsPart.GorakhnathPipal: return Gorakhnath.Build(x);
                case KdsPart.Kashinath: return NorthShrines.Kashinath(x);
                case KdsPart.NilaVarahi: return NorthShrines.NilaVarahi(x);
                default: return 0;
            }
        }

        /// <summary>
        /// The build frame of a structure in a tile: its OSM outline's oriented box (centre, axes, half extents; the door
        /// bearing snapped onto the nearest axis) when the outline is in the tile, else the brief's position and bearing;
        /// the ground is the lowest terrain under the footprint. False when it does not stand in this tile.
        /// </summary>
        internal static bool Locate(Entry e, TileData tile, int lod, MeshData m, GenColliders c, SacredStats stats, out KdsCtx x)
        {
            x = null;
            double cx, cz, yaw = e.YawDeg, hu = 0.5 * e.PlanW, hw = 0.5 * e.PlanD;
            int[] ring = null;
            ulong wr = HeritageRecord.WayRelationRef(e.Osm);
            if (wr != 0)
            {
                for (int i = 0; i < tile.Buildings.Count; i++)
                {
                    if (tile.Buildings[i].OsmRef != wr) continue;
                    ring = tile.Buildings[i].Rings[0];
                    break;
                }
            }
            if (ring != null)
            {
                double fyaw, fw, fd;
                SacredSelector.Frame(ring, -1, out cx, out cz, out fyaw, out fw, out fd);
                yaw = HeroBuilder.SnapYaw(e.YawDeg, fyaw, 45);
                // The octagon (Chyasin Dega) has a face every 45°: the door is on the face nearest the measured bearing.
                if (e.Part == KdsPart.ChyasinDega) yaw = fyaw + 45.0 * Math.Round((e.YawDeg - fyaw) / 45.0);
                double w, d;
                SacredSelector.Extents(ring, cx, cz, yaw, out w, out d);
                hu = 0.5 * w;
                hw = 0.5 * d;
            }
            else
            {
                double lon, lat, gx, gz;
                LocalToLonLat(e.X, e.Y, out lon, out lat);
                WorldFrame.LonLatToGame(lon, lat, out gx, out gz);
                cx = gx - tile.Tile.X0;
                cz = gz - tile.Tile.Z0;
                Entry rel;
                double rx, rz, ryaw;
                if (e.Rel != null && ById.TryGetValue(e.Rel, out rel) && OutlineFrame(tile, rel, out rx, out rz, out ryaw))
                {
                    // The brief's offset from the mapped neighbour, applied to that neighbour's outline in this tile.
                    cx = rx + (e.X - rel.X);
                    cz = rz + (e.Y - rel.Y);
                    // Square to the neighbour's axes when the brief's bearing is (nearly) theirs; a structure on its own
                    // grid (Kotilingeshwar at about 20°, beside Mahavishnu's 11°) keeps the brief's bearing.
                    double snapped = HeroBuilder.SnapYaw(e.YawDeg, ryaw, 45), off = Math.Abs(((snapped - e.YawDeg) % 360 + 540) % 360 - 180);
                    yaw = off <= 5 ? snapped : e.YawDeg;
                }
                if (cx < 0 || cz < 0 || cx >= tile.Tile.Size || cz >= tile.Tile.Size) return false;
            }
            // The square's levelled ground (KdsGround): the tile's heights as the streamer prepares them.
            IHeightSampler h = KdsGround.Sampler(tile);
            if (ring == null && e.KeepClearM > 0 && tile.Roads.Count > 0) Nudge(SacredClearance.For(tile, h), SacredDrawnRoads.For(tile), e.KeepClearM, ref cx, ref cz);
            var g = new RoadSurface(tile, h);
            KitFrame kf = KitFrame.FromYaw(cx, 0, cz, yaw);
            double ground = double.MaxValue;
            if (h != null)
            {
                // Every structure stands on its own level pad, the plaza's level at its place (judge round 1: not the lowest
                // terrain sample under it, which left plinths buried on one side and raised on the other).
                double pad = e.PadR > 0 ? KdsGround.PadLevel(e.X) : KdsGround.LevelAt(e.X, e.Y);
                if (!double.IsNaN(pad))
                {
                    ground = pad;
                }
                else
                {
                    if (ring != null)
                    {
                        for (int i = 0; i < ring.Length / 2; i++) ground = Math.Min(ground, g.Height(ring[2 * i] / 100.0, ring[2 * i + 1] / 100.0));
                    }
                    for (int q = 0; q < 5; q++)
                    {
                        double px, py, pz;
                        kf.ToWorld(q == 4 ? 0 : (q % 2 == 0 ? -hu : hu), 0, q == 4 ? 0 : (q < 2 ? -hw : hw), out px, out py, out pz);
                        ground = Math.Min(ground, g.Height(px, pz));
                    }
                }
            }
            if (ground == double.MaxValue) ground = 0;
            var f = new GenFrame(cx, cz, (float)ground, (float)yaw);
            x = new KdsCtx
            {
                M = m, C = lod <= 0 ? c : null, Lod = Math.Max(0, Math.Min(3, lod)), F = f, Kf = f.Kit, Tile = tile, Stats = stats, PlanHu = hu, PlanHw = hw,
                Clear = tile.Roads.Count > 0 ? SacredClearance.For(tile, h) : null, Drawn = tile.Roads.Count > 0 ? SacredDrawnRoads.For(tile) : null,
            };
            x.K = SacredDraw.Xf(x.Kf);
            if (h != null)
            {
                x.Ground = g;
                x.HasGround = true;
                x.Heights = h;
            }
            if (ring != null)
            {
                int n = ring.Length / 2;
                x.OutU = new double[n];
                x.OutW = new double[n];
                for (int i = 0; i < n; i++) x.Local(ring[2 * i] / 100.0, ring[2 * i + 1] / 100.0, out x.OutU[i], out x.OutW[i]);
                if (SacredDraw.Area(x.OutU, x.OutW, n) < 0)
                {
                    Array.Reverse(x.OutU);
                    Array.Reverse(x.OutW);
                }
            }
            return true;
        }

        /// <summary>Moves a point (tile-local) up to 3 m down the corridor distance until a circle of radius r round it is clear
        /// of every road corridor (by the margin) and of every drawn carriageway (the drawn road can run off the corridor's
        /// centreline where the pipeline shifted it).</summary>
        private static void Nudge(SacredClearance clear, SacredDrawnRoads drawn, double r, ref double cx, ref double cz)
        {
            for (int step = 0; step < 12; step++)
            {
                double min = double.MaxValue;
                for (int i = 0; i < 16; i++)
                {
                    double a = Math.PI * i / 8, px = cx + r * Math.Cos(a), pz = cz + r * Math.Sin(a);
                    min = Math.Min(min, Math.Min(clear.Distance(px, pz) - SacredClearance.MarginM, drawn.SignedDistance(px, pz) - 0.1));
                }
                if (min >= 0) return;
                const double e = 0.5;
                double gx = Clearance(clear, drawn, cx + e, cz) - Clearance(clear, drawn, cx - e, cz);
                double gz = Clearance(clear, drawn, cx, cz + e) - Clearance(clear, drawn, cx, cz - e);
                double l = Math.Sqrt(gx * gx + gz * gz);
                if (l < 1e-6) return;
                cx += 0.25 * gx / l;
                cz += 0.25 * gz / l;
            }
        }

        /// <summary>The nearer of the corridor margin and the drawn carriageway at a tile-local point.</summary>
        private static double Clearance(SacredClearance clear, SacredDrawnRoads drawn, double x, double z)
        {
            return Math.Min(clear.Distance(x, z) - SacredClearance.MarginM, drawn.SignedDistance(x, z) - 0.1);
        }

        /// <summary>The oriented-box centre (tile-local) and door bearing of an entry's OSM outline in a tile.</summary>
        internal static bool OutlineFrame(TileData tile, Entry e, out double cx, out double cz, out double yaw)
        {
            cx = cz = yaw = 0;
            ulong wr = HeritageRecord.WayRelationRef(e.Osm);
            if (wr == 0) return false;
            for (int i = 0; i < tile.Buildings.Count; i++)
            {
                if (tile.Buildings[i].OsmRef != wr) continue;
                double fyaw, fw, fd;
                SacredSelector.Frame(tile.Buildings[i].Rings[0], -1, out cx, out cz, out fyaw, out fw, out fd);
                yaw = HeroBuilder.SnapYaw(e.YawDeg, fyaw, 45);
                return true;
            }
            return false;
        }

        /// <summary>The frame of another entry of the site in this tile (its outline's oriented box), for structures placed
        /// against a mapped neighbour; false when it is not in the tile.</summary>
        internal static bool FrameOf(TileData tile, string id, out KitFrame kf, out double hu, out double hw)
        {
            kf = default(KitFrame);
            hu = hw = 0;
            Entry e;
            double cx, cz, yaw;
            if (!ById.TryGetValue(id, out e) || !OutlineFrame(tile, e, out cx, out cz, out yaw)) return false;
            kf = KitFrame.FromYaw(cx, 0, cz, yaw);
            ulong wr = HeritageRecord.WayRelationRef(e.Osm);
            for (int i = 0; i < tile.Buildings.Count; i++)
            {
                if (tile.Buildings[i].OsmRef != wr) continue;
                double w, d;
                SacredSelector.Extents(tile.Buildings[i].Rings[0], cx, cz, yaw, out w, out d);
                hu = 0.5 * w;
                hw = 0.5 * d;
            }
            return true;
        }

        /// <summary>The tile-local position of a brief local point (for structures placed relative to the brief).</summary>
        internal static void LocalToTile(TileData tile, double bx, double by, out double tx, out double tz)
        {
            double lon, lat, gx, gz;
            LocalToLonLat(bx, by, out lon, out lat);
            WorldFrame.LonLatToGame(lon, lat, out gx, out gz);
            tx = gx - tile.Tile.X0;
            tz = gz - tile.Tile.Z0;
        }
    }
}
