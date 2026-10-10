using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>Kinds of ambient animal groups the wildlife presenters keep alive round the player. Append only.</summary>
    public enum AmbientKind : byte
    {
        PigeonFlock = 0,
        KiteGroup = 1,
        CrowGroup = 2,
        SparrowGroup = 3,
        MynaGroup = 4,
        SwallowGroup = 5,
        EgretGroup = 6,
        MacaqueTroop = 7,
        GoatHerd = 8,
        ChickenYard = 9,
        DuckPond = 10,
        BuffaloGroup = 11,
    }

    /// <summary>One planned group: what, where (game metres), how many, how far it spreads and its seed.</summary>
    public struct AmbientGroup
    {
        public AmbientKind Kind;
        public double X, Z;
        public int Count;
        public float RadiusM;
        public uint Seed;

        /// <summary>Stable identity (site or grid cell and kind): the same group comes back with the same key.</summary>
        public long Key;

        /// <summary>Distance from the planning focus (m).</summary>
        public float DistanceM;

        /// <summary>True for bird flocks (simulated by <see cref="FlockSim"/>), false for <see cref="HerdSim"/> groups.</summary>
        public bool IsFlock
        {
            get { return Kind <= AmbientKind.EgretGroup; }
        }

        /// <summary>True for the curated residents (<see cref="AmbientFaunaPlanner.Sites"/>): the Durbar-square pigeon
        /// flocks, the macaque troops of the temples, the Tundikhel crow roost.</summary>
        public bool IsCurated
        {
            get { return (Key >> 40) == 1L; }
        }
    }

    /// <summary>
    /// What the planner and the sims ask of the world: the area type of a point (W2_DESIGN 1.1 classifier) and what
    /// lies on the ground there (buildings, hero monuments, carriageways, footways, water, fields, squares;
    /// <see cref="FaunaGroundIndex"/>), so no flock pecks inside a house, no herd grazes on a carriageway and ducks
    /// swim only on water.
    /// </summary>
    public interface IFaunaHabitat
    {
        AreaType AreaAt(double x, double z);

        /// <summary>What lies at (x, z); <see cref="FaunaGround.None"/> where nothing is loaded yet.</summary>
        FaunaGround GroundAt(double x, double z);
    }

    /// <summary>
    /// A curated place with its resident animals (street_life §7.3, §8): an anchor (the monument or square it belongs
    /// to, WGS84) and one offset per group in metres east and north of it, placed on the real gathering spots (the
    /// open brick square of Basantapur, the kora plaza of Boudha outside the prayer-wheel wall, the platform, east stair
    /// and chaitya terraces round Swayambhu's dome), never inside a monument (<see cref="FaunaHeroZones"/>).
    /// </summary>
    public readonly struct FaunaSite
    {
        public readonly string Name;
        public readonly double Lon, Lat;
        public readonly AmbientKind Kind;
        public readonly int CountMin, CountMax;

        /// <summary>Peck disc of a flock, home radius of a troop (m).</summary>
        public readonly float SpreadM;

        /// <summary>Group offsets from the anchor: east, north (m), one pair per group.</summary>
        private readonly float[] _offsets;

        public FaunaSite(string name, double lat, double lon, AmbientKind kind, int countMin, int countMax, float spreadM, params float[] offsetsEastNorth)
        {
            Name = name;
            Lat = lat;
            Lon = lon;
            Kind = kind;
            CountMin = countMin;
            CountMax = countMax;
            SpreadM = spreadM;
            _offsets = offsetsEastNorth != null && offsetsEastNorth.Length >= 2 ? offsetsEastNorth : new[] { 0f, 0f };
        }

        /// <summary>Number of groups.</summary>
        public int Groups
        {
            get { return _offsets.Length / 2; }
        }

        /// <summary>Offset of group <paramref name="g"/> from the anchor (m east, m north).</summary>
        public void Offset(int g, out float east, out float north)
        {
            east = _offsets[2 * g];
            north = _offsets[2 * g + 1];
        }
    }

    /// <summary>
    /// Plans the ambient animals and birds round the player (W2_DESIGN 5.5–5.6, street_life §7–8), deterministically
    /// from the region seed: the curated residents first (the rhesus macaque troops of Swayambhu, Pashupati, Gokarna and
    /// Bajrayogini; the pigeon flocks of Basantapur, Patan, Bhaktapur, Boudha and Swayambhu; the evening crow roost at
    /// Tundikhel), then a 120 m grid where each cell rolls its groups from its area type: crows, sparrows, mynas and
    /// roof pigeons in the old core and the city; goats, hens, buffalo, egrets and swallows (March–October) in the
    /// peri-urban fringe and the fields; ducks at village ponds; kites over everything by day (08:30–16:00).
    /// Birds roost at night (no ground flocks between 19:30 and 05:30). Nearest first, allocation-free.
    /// </summary>
    public sealed class AmbientFaunaPlanner
    {
        /// <summary>Grid cell of the generic groups (m).</summary>
        public const double CellM = 120.0;

        /// <summary>Kites are rolled on a coarser grid (2–8 in any square kilometre).</summary>
        public const double KiteCellM = 400.0;

        /// <summary>Generic pigeon flocks stay this far from a curated square flock (the square's flock is the one).</summary>
        public const double CuratedPigeonExclusionM = 150.0;

        /// <summary>
        /// The curated residents. Anchors are the monuments of temples.md and HeroCatalog; offsets put each group on
        /// the open ground where the animals really gather (checked against the sample region's building footprints
        /// and the hero plans by the fauna tests).
        /// </summary>
        public static readonly FaunaSite[] Sites =
        {
            // Swayambhu stupa w201223707: troops on the paved platform south-west of the dome, on the upper flight of
            // the 365-step east stairway, and among the chaityas and shrines north-west of the dome; pigeons on the
            // platform east of the dome by the vajra.
            new FaunaSite("Swayambhu", 27.714931, 85.290391, AmbientKind.MacaqueTroop, 14, 24, 9f, -21f, -6f, 46f, -11f, -16f, 30f),
            new FaunaSite("Swayambhu pigeons", 27.714931, 85.290391, AmbientKind.PigeonFlock, 50, 80, 6f, 22f, -4f),
            // Pashupatinath w913170315: the east-bank terraces with the rows of linga shrines and the forest above.
            new FaunaSite("Pashupati", 27.710465, 85.348665, AmbientKind.MacaqueTroop, 10, 18, 14f, 150f, 37f, 105f, -32f),
            // Kathmandu Durbar Square: the dense flock on the brick paving between Jagannath and the palace, round
            // King Pratap Malla's column (the photo everyone takes); a smaller one on the open Basantapur square.
            new FaunaSite("Basantapur", 27.704647, 85.307220, AmbientKind.PigeonFlock, 120, 180, 8f, 8f, -14f),
            new FaunaSite("Basantapur square", 27.704000, 85.307100, AmbientKind.PigeonFlock, 40, 80, 10f, -6f, -40f),
            // Patan Durbar Square: the paving between Krishna Mandir, the Taleju bell and the Yoganarendra column.
            new FaunaSite("Patan Durbar Square", 27.673500, 85.325140, AmbientKind.PigeonFlock, 80, 120, 7f, -4f, -6f),
            // Taumadhi, Bhaktapur: the square south-west of Nyatapola's plinth, west of Bhairavnath.
            new FaunaSite("Taumadhi, Bhaktapur", 27.671410, 85.429373, AmbientKind.PigeonFlock, 70, 110, 8f, -20f, -25f),
            // Boudhanath w56688295: the kora plaza outside the prayer-wheel wall (r ≈ 48 m), west side where it is widest.
            new FaunaSite("Boudha", 27.721436, 85.362004, AmbientKind.PigeonFlock, 80, 140, 7f, -56f, -6f),
            new FaunaSite("Gokarna", 27.74260, 85.39460, AmbientKind.MacaqueTroop, 6, 14, 12f, 0f, 0f),
            new FaunaSite("Bajrayogini, Sankhu", 27.75430, 85.47400, AmbientKind.MacaqueTroop, 6, 14, 12f, 0f, 0f),
            new FaunaSite("Patan Durbar Square macaques", 27.67380, 85.32540, AmbientKind.MacaqueTroop, 5, 9, 8f, 0f, 0f),
            new FaunaSite("Tundikhel crows", 27.70200, 85.31500, AmbientKind.CrowGroup, 25, 45, 8f, 0f, 0f, 30f, 12f),
        };

        /// <summary>Candidate points tried per cell and kind before the cell goes without that group.</summary>
        public const int Candidates = 6;

        private readonly uint _seed;
        private readonly double[] _siteX, _siteZ;

        public AmbientFaunaPlanner(uint regionSeed)
        {
            _seed = regionSeed;
            _siteX = new double[Sites.Length];
            _siteZ = new double[Sites.Length];
            for (int i = 0; i < Sites.Length; i++) WorldFrame.LonLatToGame(Sites[i].Lon, Sites[i].Lat, out _siteX[i], out _siteZ[i]);
        }

        /// <summary>Game position of curated site <paramref name="i"/>'s anchor.</summary>
        public void SitePosition(int i, out double x, out double z)
        {
            x = _siteX[i];
            z = _siteZ[i];
        }

        /// <summary>Game position of group <paramref name="g"/> of curated site <paramref name="i"/>.</summary>
        public void GroupPosition(int i, int g, out double x, out double z)
        {
            Sites[i].Offset(g, out float east, out float north);
            x = _siteX[i] + east;
            z = _siteZ[i] + north;
        }

        /// <summary>
        /// Fills <paramref name="dst"/> with the groups within <paramref name="radiusM"/> of (<paramref name="fx"/>,
        /// <paramref name="fz"/>) at local solar hour <paramref name="hour"/> of <paramref name="month"/> (1–12),
        /// nearest first; returns how many. <paramref name="habitat"/> may be null (then only the curated residents and
        /// the kites appear). Generic groups take the first of a few candidate points in their cell whose ground suits
        /// them (<see cref="Fits"/>); generic pigeon flocks keep away from the curated square flocks.
        /// </summary>
        public int Plan(double fx, double fz, float radiusM, int month, float hour, IFaunaHabitat habitat, AmbientGroup[] dst)
        {
            if (dst == null || dst.Length == 0) return 0;
            int n = 0;
            float h = ((hour % 24f) + 24f) % 24f;
            bool night = h >= 19.5f || h < 5.5f;
            bool swallows = month >= 3 && month <= 10;
            // Curated residents.
            for (int s = 0; s < Sites.Length; s++)
            {
                FaunaSite site = Sites[s];
                if (night && site.Kind != AmbientKind.MacaqueTroop && site.Kind != AmbientKind.CrowGroup) continue;
                for (int g = 0; g < site.Groups; g++)
                {
                    uint hsh = FaunaRng.Hash(_seed, (uint)s, (uint)g, 0x517Eu);
                    GroupPosition(s, g, out double x, out double z);
                    if (FaunaHeroZones.Inside(x, z)) continue; // never inside a monument
                    int count = site.CountMin + (int)(FaunaRng.Unit(FaunaRng.Hash(hsh)) * (site.CountMax - site.CountMin + 1));
                    Push(dst, ref n, site.Kind, x, z, count, site.SpreadM, hsh, (1L << 40) | ((long)s << 8) | (long)g, fx, fz, radiusM);
                }
            }
            // Kites over the city by day.
            if (h >= 8.5f && h < 16f)
            {
                long k0 = (long)Math.Floor((fx - radiusM) / KiteCellM), k1 = (long)Math.Floor((fx + radiusM) / KiteCellM);
                long l0 = (long)Math.Floor((fz - radiusM) / KiteCellM), l1 = (long)Math.Floor((fz + radiusM) / KiteCellM);
                for (long cx = k0; cx <= k1; cx++)
                for (long cz = l0; cz <= l1; cz++)
                {
                    uint hsh = FaunaRng.Hash(_seed, (uint)cx, (uint)cz, 0x6173u);
                    if (FaunaRng.Unit(hsh) > 0.45f) continue;
                    double x = (cx + 0.5) * KiteCellM, z = (cz + 0.5) * KiteCellM;
                    Push(dst, ref n, AmbientKind.KiteGroup, x, z, 1 + (int)(FaunaRng.Unit(hsh >> 7) * 3f), 150f, hsh, (2L << 40) | ((cx & 0xFFFFF) << 20) | (cz & 0xFFFFF),
                         fx, fz, radiusM + 200f);
                }
            }
            if (habitat == null) return Sort(dst, n);
            long c0 = (long)Math.Floor((fx - radiusM) / CellM), c1 = (long)Math.Floor((fx + radiusM) / CellM);
            long d0 = (long)Math.Floor((fz - radiusM) / CellM), d1 = (long)Math.Floor((fz + radiusM) / CellM);
            for (long cx = c0; cx <= c1; cx++)
            for (long cz = d0; cz <= d1; cz++)
            {
                double x0 = (cx + 0.5) * CellM, z0 = (cz + 0.5) * CellM;
                AreaType area = habitat.AreaAt(x0, z0);
                if (area == AreaType.Unknown) continue;
                for (int k = 0; k <= (int)AmbientKind.BuffaloGroup; k++)
                {
                    var kind = (AmbientKind)k;
                    if (kind == AmbientKind.PigeonFlock && area != AreaType.OldCore && area != AreaType.Urban) continue;
                    if (kind == AmbientKind.KiteGroup || (kind == AmbientKind.MacaqueTroop && area != AreaType.Forest)) continue;
                    if (kind == AmbientKind.SwallowGroup && !swallows) continue;
                    bool bird = kind <= AmbientKind.EgretGroup;
                    if (night && (bird || kind == AmbientKind.ChickenYard || kind == AmbientKind.DuckPond)) continue;
                    float p = Chance(kind, area, month);
                    if (p <= 0f) continue;
                    uint hsh = FaunaRng.Hash(_seed, (uint)cx, (uint)cz, 0xA000u + (uint)k);
                    if (FaunaRng.Unit(hsh) >= p) continue;
                    if (kind == AmbientKind.PigeonFlock && NearCuratedPigeons(x0, z0, CuratedPigeonExclusionM + 0.71 * CellM)) continue;
                    GroupSize(kind, hsh, out int count, out float spread);
                    // A few candidate points in the cell: the first whose ground suits the kind wins (no flock in a
                    // house, no herd on a carriageway, ducks on a pond, egrets in the fields).
                    double x = 0.0, z = 0.0;
                    bool ok = false;
                    for (int t = 0; t < Candidates && !ok; t++)
                    {
                        uint ht = t == 0 ? hsh : FaunaRng.Hash(hsh, (uint)t, 0xCA4Du);
                        x = x0 + (FaunaRng.Unit(ht >> 3) - 0.5) * CellM * 0.8;
                        z = z0 + (FaunaRng.Unit(FaunaRng.Hash(ht, 0x2u)) - 0.5) * CellM * 0.8;
                        ok = Fits(kind, x, z, spread, habitat);
                    }
                    if (!ok) continue;
                    if (kind == AmbientKind.PigeonFlock && NearCuratedPigeons(x, z, CuratedPigeonExclusionM)) continue;
                    Push(dst, ref n, kind, x, z, count, spread, hsh, (3L << 40) | ((cx & 0xFFFF) << 24) | ((cz & 0xFFFF) << 8) | (long)k, fx, fz, radiusM);
                }
            }
            return Sort(dst, n);
        }

        /// <summary>True when a curated pigeon flock lives within <paramref name="r"/> of (x, z).</summary>
        public bool NearCuratedPigeons(double x, double z, double r)
        {
            for (int s = 0; s < Sites.Length; s++)
            {
                if (Sites[s].Kind != AmbientKind.PigeonFlock) continue;
                for (int g = 0; g < Sites[s].Groups; g++)
                {
                    GroupPosition(s, g, out double gx, out double gz);
                    double dx = gx - x, dz = gz - z;
                    if (dx * dx + dz * dz < r * r) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True when ground <paramref name="g"/> suits an animal of a group of <paramref name="kind"/>: never inside a
        /// building or a monument, never on a carriageway (kites and swallows fly; swallows still want fields or
        /// water under them); ducks on water, egrets in fields or wetland, the others on dry open ground (squares,
        /// footways, yards, fields). Unloaded ground (None) never suits.
        /// </summary>
        public static bool Suits(AmbientKind kind, FaunaGround g)
        {
            if ((g & FaunaGround.Loaded) == 0) return false;
            if (kind == AmbientKind.KiteGroup) return true;
            if ((g & FaunaGround.Building) != 0) return false;
            switch (kind)
            {
                case AmbientKind.SwallowGroup: return (g & (FaunaGround.Field | FaunaGround.Water)) != 0;
                case AmbientKind.DuckPond: return (g & FaunaGround.Road) == 0;
                case AmbientKind.EgretGroup: return (g & FaunaGround.Road) == 0 && (g & (FaunaGround.Field | FaunaGround.Water)) != 0;
                default: return (g & (FaunaGround.Road | FaunaGround.Water)) == 0;
            }
        }

        /// <summary>
        /// True when a group of <paramref name="kind"/> can live round (x, z): not in a monument, the centre suits the
        /// kind (ducks: the centre is water), and at least three of four points half a spread out suit it too (ducks:
        /// the bank or the pond). Without a habitat only the monuments are checked.
        /// </summary>
        public static bool Fits(AmbientKind kind, double x, double z, float spread, IFaunaHabitat habitat)
        {
            if (FaunaHeroZones.Inside(x, z, 1.0)) return false;
            if (habitat == null) return true;
            FaunaGround c = habitat.GroundAt(x, z);
            if (!Suits(kind, c)) return false;
            if (kind == AmbientKind.DuckPond && (c & FaunaGround.Water) == 0) return false;
            if (kind == AmbientKind.KiteGroup || kind == AmbientKind.SwallowGroup) return true;
            int good = 0;
            double r = 0.5 * spread;
            for (int k = 0; k < 4; k++)
            {
                double px = x + (k == 0 ? r : k == 1 ? -r : 0.0), pz = z + (k == 2 ? r : k == 3 ? -r : 0.0);
                if (Suits(kind, habitat.GroundAt(px, pz))) good++;
            }
            return good >= 3;
        }

        /// <summary>Chance per 120 m cell of a group of <paramref name="kind"/> in <paramref name="area"/> (densities of
        /// street_life §7.4 and §8 turned into groups).</summary>
        public static float Chance(AmbientKind kind, AreaType area, int month)
        {
            bool paddy = month >= 6 && month <= 7;
            switch (area)
            {
                case AreaType.OldCore:
                    return kind == AmbientKind.PigeonFlock ? 0.25f : kind == AmbientKind.CrowGroup ? 0.3f : kind == AmbientKind.SparrowGroup ? 0.35f :
                           kind == AmbientKind.MynaGroup ? 0.12f : 0f;
                case AreaType.Urban:
                    return kind == AmbientKind.PigeonFlock ? 0.1f : kind == AmbientKind.CrowGroup ? 0.25f : kind == AmbientKind.SparrowGroup ? 0.2f :
                           kind == AmbientKind.MynaGroup ? 0.25f : kind == AmbientKind.ChickenYard ? 0.03f : 0f;
                case AreaType.PeriUrban:
                    switch (kind)
                    {
                        case AmbientKind.CrowGroup: return 0.15f;
                        case AmbientKind.SparrowGroup: return 0.1f;
                        case AmbientKind.MynaGroup: return 0.25f;
                        case AmbientKind.GoatHerd: return 0.15f;
                        case AmbientKind.ChickenYard: return 0.25f;
                        case AmbientKind.BuffaloGroup: return 0.05f;
                        case AmbientKind.EgretGroup: return paddy ? 0.15f : 0.05f;
                        case AmbientKind.SwallowGroup: return 0.1f;
                        case AmbientKind.DuckPond: return 0.03f;
                        default: return 0f;
                    }
                case AreaType.Rural:
                case AreaType.Hill:
                    switch (kind)
                    {
                        case AmbientKind.CrowGroup: return 0.05f;
                        case AmbientKind.MynaGroup: return 0.1f;
                        case AmbientKind.GoatHerd: return 0.2f;
                        case AmbientKind.ChickenYard: return 0.2f;
                        case AmbientKind.BuffaloGroup: return 0.12f;
                        case AmbientKind.EgretGroup: return paddy ? 0.35f : 0.12f;
                        case AmbientKind.SwallowGroup: return 0.2f;
                        case AmbientKind.DuckPond: return 0.08f;
                        default: return 0f;
                    }
                case AreaType.Forest:
                    return kind == AmbientKind.MacaqueTroop ? 0.04f : kind == AmbientKind.CrowGroup ? 0.03f : 0f;
                default:
                    return 0f;
            }
        }

        private static void GroupSize(AmbientKind kind, uint h, out int count, out float spread)
        {
            float u = FaunaRng.Unit(FaunaRng.Hash(h, 0x5E2u));
            switch (kind)
            {
                case AmbientKind.PigeonFlock: count = 8 + (int)(u * 13f); spread = 5f; return;
                case AmbientKind.CrowGroup: count = 2 + (int)(u * 9f); spread = 6f; return;
                case AmbientKind.SparrowGroup: count = 5 + (int)(u * 16f); spread = 3f; return;
                case AmbientKind.MynaGroup: count = 2 + (int)(u * 5f); spread = 4f; return;
                case AmbientKind.SwallowGroup: count = 5 + (int)(u * 16f); spread = 30f; return;
                case AmbientKind.EgretGroup: count = 5 + (int)(u * 16f); spread = 12f; return;
                case AmbientKind.MacaqueTroop: count = 8 + (int)(u * 13f); spread = 12f; return;
                case AmbientKind.GoatHerd: count = 3 + (int)(u * 8f); spread = 7f; return;
                case AmbientKind.ChickenYard: count = 3 + (int)(u * 10f); spread = 4f; return;
                case AmbientKind.DuckPond: count = 3 + (int)(u * 8f); spread = 5f; return;
                case AmbientKind.BuffaloGroup: count = 1 + (int)(u * 3f); spread = 5f; return;
                default: count = 1; spread = 100f; return;
            }
        }

        private static void Push(AmbientGroup[] dst, ref int n, AmbientKind kind, double x, double z, int count, float spread, uint seed, long key, double fx,
                                 double fz, float radius)
        {
            double dx = x - fx, dz = z - fz;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d > radius) return;
            var g = new AmbientGroup { Kind = kind, X = x, Z = z, Count = count, RadiusM = spread, Seed = seed, Key = key, DistanceM = d };
            if (n < dst.Length)
            {
                dst[n++] = g;
                return;
            }
            // Full: replace the farthest if this one is nearer.
            int far = 0;
            for (int i = 1; i < n; i++)
                if (dst[i].DistanceM > dst[far].DistanceM)
                    far = i;
            if (dst[far].DistanceM > d) dst[far] = g;
        }

        private static int Sort(AmbientGroup[] a, int n)
        {
            // Insertion sort by distance then key (n is small; no allocation).
            for (int i = 1; i < n; i++)
            {
                AmbientGroup v = a[i];
                int j = i - 1;
                while (j >= 0 && (a[j].DistanceM > v.DistanceM || a[j].DistanceM == v.DistanceM && a[j].Key > v.Key))
                {
                    a[j + 1] = a[j];
                    j--;
                }
                a[j + 1] = v;
            }
            return n;
        }

        /// <summary>The species a group is made of.</summary>
        public static FaunaSpecies SpeciesOf(AmbientKind kind)
        {
            switch (kind)
            {
                case AmbientKind.PigeonFlock: return FaunaSpecies.Pigeon;
                case AmbientKind.KiteGroup: return FaunaSpecies.BlackKite;
                case AmbientKind.CrowGroup: return FaunaSpecies.Crow;
                case AmbientKind.SparrowGroup: return FaunaSpecies.Sparrow;
                case AmbientKind.MynaGroup: return FaunaSpecies.Myna;
                case AmbientKind.SwallowGroup: return FaunaSpecies.Swallow;
                case AmbientKind.EgretGroup: return FaunaSpecies.Egret;
                case AmbientKind.MacaqueTroop: return FaunaSpecies.Macaque;
                case AmbientKind.GoatHerd: return FaunaSpecies.Goat;
                case AmbientKind.ChickenYard: return FaunaSpecies.Hen;
                case AmbientKind.DuckPond: return FaunaSpecies.Duck;
                default: return FaunaSpecies.Buffalo;
            }
        }

        /// <summary>The flock kind of a bird group.</summary>
        public static FlockKind FlockOf(AmbientKind kind)
        {
            switch (kind)
            {
                case AmbientKind.KiteGroup: return FlockKind.Kites;
                case AmbientKind.CrowGroup: return FlockKind.Crows;
                case AmbientKind.SparrowGroup: return FlockKind.Sparrows;
                case AmbientKind.MynaGroup: return FlockKind.Mynas;
                case AmbientKind.SwallowGroup: return FlockKind.Swallows;
                case AmbientKind.EgretGroup: return FlockKind.Egrets;
                default: return FlockKind.Pigeons;
            }
        }
    }
}
