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
    }

    /// <summary>What the planner asks of the world: the area type of a point (W2_DESIGN 1.1 classifier).</summary>
    public interface IFaunaHabitat
    {
        AreaType AreaAt(double x, double z);
    }

    /// <summary>A curated place with its resident animals (street_life §7.3, §8).</summary>
    public readonly struct FaunaSite
    {
        public readonly string Name;
        public readonly double Lon, Lat;
        public readonly AmbientKind Kind;
        public readonly int Groups, CountMin, CountMax;
        public readonly float SpreadM;

        public FaunaSite(string name, double lat, double lon, AmbientKind kind, int groups, int countMin, int countMax, float spreadM)
        {
            Name = name;
            Lat = lat;
            Lon = lon;
            Kind = kind;
            Groups = groups;
            CountMin = countMin;
            CountMax = countMax;
            SpreadM = spreadM;
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

        /// <summary>The curated residents.</summary>
        public static readonly FaunaSite[] Sites =
        {
            new FaunaSite("Swayambhu", 27.71493, 85.29043, AmbientKind.MacaqueTroop, 3, 14, 24, 45f),
            new FaunaSite("Swayambhu pigeons", 27.71470, 85.29010, AmbientKind.PigeonFlock, 1, 50, 80, 8f),
            new FaunaSite("Pashupati", 27.71080, 85.35020, AmbientKind.MacaqueTroop, 2, 10, 18, 40f),
            new FaunaSite("Basantapur", 27.70440, 85.30690, AmbientKind.PigeonFlock, 1, 120, 180, 10f),
            new FaunaSite("Patan Durbar Square", 27.67330, 85.32503, AmbientKind.PigeonFlock, 1, 80, 120, 9f),
            new FaunaSite("Taumadhi, Bhaktapur", 27.67140, 85.42920, AmbientKind.PigeonFlock, 1, 70, 110, 9f),
            new FaunaSite("Boudha", 27.72148, 85.36203, AmbientKind.PigeonFlock, 1, 80, 140, 10f),
            new FaunaSite("Gokarna", 27.74260, 85.39460, AmbientKind.MacaqueTroop, 1, 6, 14, 30f),
            new FaunaSite("Bajrayogini, Sankhu", 27.75430, 85.47400, AmbientKind.MacaqueTroop, 1, 6, 14, 30f),
            new FaunaSite("Patan Durbar Square macaques", 27.67380, 85.32540, AmbientKind.MacaqueTroop, 1, 5, 9, 20f),
            new FaunaSite("Tundikhel crows", 27.70200, 85.31500, AmbientKind.CrowGroup, 2, 25, 45, 25f),
        };

        private readonly uint _seed;
        private readonly double[] _siteX, _siteZ;

        public AmbientFaunaPlanner(uint regionSeed)
        {
            _seed = regionSeed;
            _siteX = new double[Sites.Length];
            _siteZ = new double[Sites.Length];
            for (int i = 0; i < Sites.Length; i++) WorldFrame.LonLatToGame(Sites[i].Lon, Sites[i].Lat, out _siteX[i], out _siteZ[i]);
        }

        /// <summary>Game position of curated site <paramref name="i"/>.</summary>
        public void SitePosition(int i, out double x, out double z)
        {
            x = _siteX[i];
            z = _siteZ[i];
        }

        /// <summary>
        /// Fills <paramref name="dst"/> with the groups within <paramref name="radiusM"/> of (<paramref name="fx"/>,
        /// <paramref name="fz"/>) at local solar hour <paramref name="hour"/> of <paramref name="month"/> (1–12),
        /// nearest first; returns how many. <paramref name="habitat"/> may be null (then only the curated residents and
        /// the kites appear).
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
                    double a = FaunaRng.Unit(hsh) * Math.PI * 2.0, r = g == 0 ? 0.0 : site.SpreadM * (0.6 + 0.6 * FaunaRng.Unit(hsh >> 5));
                    double x = _siteX[s] + Math.Cos(a) * r, z = _siteZ[s] + Math.Sin(a) * r;
                    int count = site.CountMin + (int)(FaunaRng.Unit(FaunaRng.Hash(hsh)) * (site.CountMax - site.CountMin + 1));
                    Push(dst, ref n, site.Kind, x, z, count, site.Kind == AmbientKind.MacaqueTroop ? 12f : site.SpreadM, hsh, (1L << 40) | ((long)s << 8) | (long)g, fx, fz,
                         radiusM);
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
                    double x = x0 + (FaunaRng.Unit(hsh >> 3) - 0.5) * CellM * 0.7, z = z0 + (FaunaRng.Unit(hsh >> 11) - 0.5) * CellM * 0.7;
                    GroupSize(kind, hsh, out int count, out float spread);
                    Push(dst, ref n, kind, x, z, count, spread, hsh, (3L << 40) | ((cx & 0xFFFF) << 24) | ((cz & 0xFFFF) << 8) | (long)k, fx, fz, radiusM);
                }
            }
            return Sort(dst, n);
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
