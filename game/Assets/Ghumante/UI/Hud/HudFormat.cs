using System;
using System.Globalization;
using System.Text;
using Ghumante.Core.Data;

namespace Ghumante.UI.Hud
{
    /// <summary>
    /// Text and keys for the Explore HUD, engine-free so they are unit-tested (ARCHITECTURE.md 7.9): speed in km/h,
    /// distances and ETAs, Devanagari digits (०-९) when the locale is Nepali with native numerals, localisation keys
    /// for road surfaces and place kinds. Small integers are cached per digit set, so the speedometer allocates nothing
    /// per frame. Values are formatted with the invariant culture.
    /// </summary>
    public static class HudFormat
    {
        /// <summary>Integers 0..<see cref="CachedNumbers"/> − 1 are cached (speeds, percentages, minutes).</summary>
        public const int CachedNumbers = 1000;

        private static readonly string[] LatinCache = new string[CachedNumbers];
        private static readonly string[] DevanagariCache = new string[CachedNumbers];

        /// <summary>Speed for the speedometer: |m/s| × 3.6 rounded to whole km/h (reversing reads positive), 0 for
        /// non-finite input, at most 999.</summary>
        public static int SpeedKmh(float metresPerSecond)
        {
            if (float.IsNaN(metresPerSecond) || float.IsInfinity(metresPerSecond)) return 0;
            double kmh = Math.Round(Math.Abs(metresPerSecond) * 3.6, MidpointRounding.AwayFromZero);
            return kmh > 999.0 ? 999 : (int)kmh;
        }

        /// <summary>A non-negative integer in ASCII or Devanagari digits, without grouping (cached below
        /// <see cref="CachedNumbers"/>).</summary>
        public static string Number(int value, bool devanagari)
        {
            if (value < 0) return "-" + Number(value == int.MinValue ? int.MaxValue : -value, devanagari);
            if (value < CachedNumbers)
            {
                string[] cache = devanagari ? DevanagariCache : LatinCache;
                string s = cache[value];
                if (s == null)
                {
                    s = value.ToString(CultureInfo.InvariantCulture);
                    if (devanagari) s = ToDevanagariDigits(s);
                    cache[value] = s;
                }
                return s;
            }
            string text = value.ToString(CultureInfo.InvariantCulture);
            return devanagari ? ToDevanagariDigits(text) : text;
        }

        /// <summary>Replaces ASCII digits 0-9 with Devanagari digits ०-९ (U+0966-U+096F); other characters stay.</summary>
        public static string ToDevanagariDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c >= '0' && c <= '9' ? (char)('०' + (c - '0')) : c);
            return sb.ToString();
        }

        /// <summary>
        /// A distance for the HUD: below 1 km in metres rounded to 10 m (to 5 m below 100 m), below 10 km in kilometres
        /// with one decimal ("4.9"), else whole kilometres. <paramref name="kilometres"/> says which unit the number is in.
        /// </summary>
        public static string Distance(double metres, bool devanagari, out bool kilometres)
        {
            if (double.IsNaN(metres) || metres < 0) metres = 0;
            string text;
            if (metres < 995)
            {
                kilometres = false;
                double step = metres < 100 ? 5 : 10;
                text = (Math.Round(metres / step, MidpointRounding.AwayFromZero) * step).ToString("0", CultureInfo.InvariantCulture);
            }
            else if (metres < 9950)
            {
                kilometres = true;
                text = (Math.Round(metres / 100.0, MidpointRounding.AwayFromZero) / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
            }
            else
            {
                kilometres = true;
                text = Math.Round(metres / 1000.0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
            }
            return devanagari ? ToDevanagariDigits(text) : text;
        }

        /// <summary>
        /// An integer that changes exactly when <see cref="Distance"/>'s text changes (the rounded value and its unit), so
        /// the HUD can compare it every frame and format only on change.
        /// </summary>
        public static long DistanceBucket(double metres)
        {
            if (double.IsNaN(metres) || metres < 0) metres = 0;
            if (metres < 995)
            {
                double step = metres < 100 ? 5 : 10;
                return (long)(Math.Round(metres / step, MidpointRounding.AwayFromZero) * step);
            }
            if (metres < 9950) return 1000000L + (long)Math.Round(metres / 100.0, MidpointRounding.AwayFromZero);
            return 2000000L + (long)Math.Round(Math.Min(metres, 1e12) / 1000.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Whole minutes for an ETA, rounded up; at least 1 for any positive time, 0 for none.</summary>
        public static int EtaMinutes(double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0) return 0;
            double minutes = Math.Ceiling(seconds / 60.0);
            return minutes > 9999 ? 9999 : Math.Max(1, (int)minutes);
        }

        /// <summary>A clock for the debug time slider: hours [0, 24) as "06:30".</summary>
        public static string Clock(float hours, bool devanagari)
        {
            if (float.IsNaN(hours) || float.IsInfinity(hours)) hours = 0f;
            hours %= 24f;
            if (hours < 0f) hours += 24f;
            int total = (int)Math.Floor(hours * 60f) % (24 * 60);
            string text = (total / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                          (total % 60).ToString("00", CultureInfo.InvariantCulture);
            return devanagari ? ToDevanagariDigits(text) : text;
        }

        /// <summary>
        /// String key of the surface chip: on a road the road's own surface (asphalt, brick, gravel...), falling back to
        /// its physics group when the road's surface is unknown; off road the group of the ground (paved, gravel, dirt, mud).
        /// </summary>
        public static string SurfaceKey(bool onRoad, Surface roadSurface, SurfaceGroup group)
        {
            if (onRoad)
            {
                switch (roadSurface)
                {
                    case Surface.Asphalt: return "hud.surface.asphalt";
                    case Surface.Concrete: return "hud.surface.concrete";
                    case Surface.Brick: return "hud.surface.brick";
                    case Surface.Cobble: return "hud.surface.cobble";
                    case Surface.Gravel: return "hud.surface.gravel";
                    case Surface.Compacted: return "hud.surface.compacted";
                    case Surface.Dirt: return "hud.surface.dirt";
                    case Surface.Mud: return "hud.surface.mud";
                    case Surface.Sand: return "hud.surface.sand";
                    case Surface.Grass: return "hud.surface.grass";
                    case Surface.Rock: return "hud.surface.rock";
                    case Surface.SnowIce: return "hud.surface.snow";
                    case Surface.Wood: return "hud.surface.wood";
                    case Surface.Metal: return "hud.surface.metal";
                }
            }
            return GroupKey(group);
        }

        /// <summary>String key of a physics surface group.</summary>
        public static string GroupKey(SurfaceGroup group)
        {
            switch (group)
            {
                case SurfaceGroup.Gravel: return "hud.surface.gravel";
                case SurfaceGroup.Dirt: return "hud.surface.dirt";
                case SurfaceGroup.Mud: return "hud.surface.mud";
                default: return "hud.surface.paved";
            }
        }

        /// <summary>USS modifier class that colours the surface chip by physics group.</summary>
        public static string SurfaceClass(SurfaceGroup group)
        {
            switch (group)
            {
                case SurfaceGroup.Gravel: return "gh-hud__surface--gravel";
                case SurfaceGroup.Dirt: return "gh-hud__surface--dirt";
                case SurfaceGroup.Mud: return "gh-hud__surface--mud";
                default: return "gh-hud__surface--paved";
            }
        }

        /// <summary>True when the pipeline guessed the road's surface (ADR-005: inferred or a class default), which the
        /// chip marks with a subtle dot.</summary>
        public static bool IsInferred(SurfaceSource source)
        {
            return source == SurfaceSource.Inferred || source == SurfaceSource.Default;
        }

        /// <summary>String key naming a search entry's kind (PoiKind, or PlaceKind + 1000 for places).</summary>
        public static string KindKey(int kind)
        {
            if (kind >= SearchEntry.PlaceKindOffset)
            {
                switch ((PlaceKind)(kind - SearchEntry.PlaceKindOffset))
                {
                    case PlaceKind.City: return "kind.city";
                    case PlaceKind.Town: return "kind.town";
                    case PlaceKind.Village:
                    case PlaceKind.Hamlet:
                    case PlaceKind.IsolatedDwelling:
                    case PlaceKind.Farm: return "kind.village";
                    case PlaceKind.Suburb:
                    case PlaceKind.Neighbourhood:
                    case PlaceKind.Quarter:
                    case PlaceKind.Locality: return "kind.neighbourhood";
                    case PlaceKind.Square: return "kind.square";
                    case PlaceKind.Country:
                    case PlaceKind.Province:
                    case PlaceKind.District:
                    case PlaceKind.LocalLevel:
                    case PlaceKind.Ward: return "kind.area";
                    default: return "kind.place";
                }
            }
            switch ((PoiKind)kind)
            {
                case PoiKind.TempleHindu: return "kind.temple";
                case PoiKind.Stupa:
                case PoiKind.Chorten: return "kind.stupa";
                case PoiKind.Gompa: return "kind.monastery";
                case PoiKind.Shrine:
                case PoiKind.PlaceOfWorship:
                case PoiKind.ManiWall: return "kind.shrine";
                case PoiKind.Mosque: return "kind.mosque";
                case PoiKind.Church: return "kind.church";
                case PoiKind.HeritageSquare:
                case PoiKind.Palace:
                case PoiKind.Monument:
                case PoiKind.Ruins:
                case PoiKind.StoneTap:
                case PoiKind.CityGate: return "kind.heritage";
                case PoiKind.Museum: return "kind.museum";
                case PoiKind.Peak:
                case PoiKind.Ridge:
                case PoiKind.Pass: return "kind.peak";
                case PoiKind.Viewpoint: return "kind.viewpoint";
                case PoiKind.Waterfall: return "kind.waterfall";
                case PoiKind.Lake: return "kind.lake";
                case PoiKind.River: return "kind.river";
                case PoiKind.Park:
                case PoiKind.ProtectedArea:
                case PoiKind.PicnicSite: return "kind.park";
                case PoiKind.Cave:
                case PoiKind.HotSpring:
                case PoiKind.Glacier:
                case PoiKind.Spring:
                case PoiKind.NotableTree: return "kind.nature";
                case PoiKind.Airport:
                case PoiKind.Helipad: return "kind.airport";
                case PoiKind.BusStation:
                case PoiKind.CableCarStation:
                case PoiKind.RailwayStation:
                case PoiKind.TaxiStand: return "kind.transport";
                case PoiKind.Bridge: return "kind.bridge";
                case PoiKind.Fuel:
                case PoiKind.Parking: return "kind.parking";
                case PoiKind.Hotel:
                case PoiKind.GuestHouse:
                case PoiKind.Hostel:
                case PoiKind.CampSite:
                case PoiKind.Teahouse: return "kind.lodging";
                case PoiKind.Restaurant:
                case PoiKind.Cafe: return "kind.food";
                case PoiKind.Shop:
                case PoiKind.Marketplace: return "kind.shop";
                case PoiKind.Attraction:
                case PoiKind.ThemePark:
                case PoiKind.Zoo: return "kind.attraction";
                case PoiKind.Information: return "kind.information";
                case PoiKind.School: return "kind.school";
                case PoiKind.Hospital: return "kind.hospital";
                case PoiKind.Government: return "kind.government";
                case PoiKind.Bank: return "kind.bank";
            }
            if (kind >= 500 && kind < 600) return "kind.adventure";
            return "kind.place";
        }

        /// <summary>Every key <see cref="KindKey"/> can return (tests check the string tables have them).</summary>
        public static readonly string[] AllKindKeys =
        {
            "kind.city", "kind.town", "kind.village", "kind.neighbourhood", "kind.square", "kind.area", "kind.place",
            "kind.temple", "kind.stupa", "kind.monastery", "kind.shrine", "kind.mosque", "kind.church", "kind.heritage",
            "kind.museum", "kind.peak", "kind.viewpoint", "kind.waterfall", "kind.lake", "kind.river", "kind.park",
            "kind.nature", "kind.airport", "kind.transport", "kind.bridge", "kind.parking", "kind.lodging", "kind.food",
            "kind.shop", "kind.attraction", "kind.information", "kind.adventure", "kind.school", "kind.hospital",
            "kind.government", "kind.bank",
        };

        /// <summary>Every key <see cref="SurfaceKey"/> can return.</summary>
        public static readonly string[] AllSurfaceKeys =
        {
            "hud.surface.asphalt", "hud.surface.concrete", "hud.surface.brick", "hud.surface.cobble", "hud.surface.gravel",
            "hud.surface.compacted", "hud.surface.dirt", "hud.surface.mud", "hud.surface.sand", "hud.surface.grass",
            "hud.surface.rock", "hud.surface.snow", "hud.surface.wood", "hud.surface.metal", "hud.surface.paved",
        };
    }
}
