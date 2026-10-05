using System;

namespace Ghumante.Core.Synth
{
    /// <summary>Ambience zones (W2_DESIGN 7.2; A §7.1).</summary>
    public enum AmbienceZone : byte
    {
        OldCore = 0,
        Urban = 1,
        PeriUrban = 2,
        Fields = 3,
        Forest = 4,
        TempleCompound = 5,
        StupaKora = 6,
        DurbarSquare = 7,
        Ghat = 8,
        RingRoad = 9,
        Airport = 10,
        Park = 11,
        Water = 12,
        Hilltop = 13,
    }

    /// <summary>Time-of-day bands (A §7.2).</summary>
    public enum TimeBand : byte
    {
        Dawn = 0,        // 04:30–06:00
        MorningPuja = 1, // 06:00–08:00
        Rush = 2,        // 08:00–10:30
        Day = 3,         // 10:30–16:00
        Evening = 4,     // 16:00–19:00
        Night = 5,       // 19:00–22:00
        LateNight = 6,   // 22:00–04:30
    }

    public enum Season : byte
    {
        Spring = 0,  // Mar–May
        Monsoon = 1, // Jun–Sep
        Autumn = 2,  // Oct–Nov
        Winter = 3,  // Dec–Feb
    }

    /// <summary>Spot emitters scheduled around the listener (A §7.1 "Spot emitters").</summary>
    public enum SpotKind : byte
    {
        ShrineBell = 0,
        PigeonCoo = 1,
        Shutter = 2,
        PressureCooker = 3,
        TeaClink = 4,
        Crow = 5,
        Myna = 6,
        Sparrow = 7,
        Kite = 8,
        Bulbul = 9,
        Koel = 10,
        Dog = 11,
        Rooster = 12,
        Cow = 13,
        DistantHorn = 14,
        AirBrake = 15,
        Conch = 16,
        Cymbal = 17,
        PrayerWheel = 18,
        Cricket = 19,
        Thunder = 20,
    }

    /// <summary>Mixer snapshots done in code (W2_DESIGN 7.1 "Snapshots").</summary>
    public enum MixSnapshot : byte
    {
        Street = 0,
        Courtyard = 1,
        DurbarSquare = 2,
        Galli = 3,
        VehicleInterior = 4,
    }

    /// <summary>Time-of-day multipliers (A §7.2; 1 = nominal).</summary>
    public struct TimeMultipliers
    {
        public float Traffic, Crowd, HornsPerMin, Sacred, Birds, Dogs, Insects;
    }

    /// <summary>What the ambience model needs about the listener's surroundings, once per update.</summary>
    public struct AmbienceInputs
    {
        /// <summary>Area-type weights at the listener, indexed by <c>Ghumante.Core.Data.AreaType</c>
        /// (Unknown, OldCore, Urban, PeriUrban, Rural, Hill, Forest), from <c>AreaTypeGrid.Weights</c>.</summary>
        public float AreaUnknown, AreaOldCore, AreaUrban, AreaPeriUrban, AreaRural, AreaHill, AreaForest;
        /// <summary><c>Ghumante.Core.Data.SacredZoneKind</c> byte at the listener (0 none, 1 compound, 2 courtyard,
        /// 3 heritage square, 4 stupa kora, 5 ghat).</summary>
        public byte SacredKind;
        /// <summary>0..1 nearness to the Ring Road or an arterial, to the aerodrome (+3 km), a park, a river.</summary>
        public float RingRoad01, Airport01, Park01, Water01;
        public float ElevationM;
        /// <summary>Game hour 0..24 and month 1..12.</summary>
        public float Hour;
        public int Month;
        public float Rain01, Wind01;
        /// <summary>Lane width at the listener for the galli slapback (0 = unknown or open).</summary>
        public float LaneWidthM;
        /// <summary>True inside a car, bus or truck with a close camera (VehicleInterior snapshot).</summary>
        public bool InVehicle;

        public void SetArea(int areaType, float w)
        {
            float v = Dsp.Clamp01(w);
            switch (areaType)
            {
                case 1: AreaOldCore = v; break;
                case 2: AreaUrban = v; break;
                case 3: AreaPeriUrban = v; break;
                case 4: AreaRural = v; break;
                case 5: AreaHill = v; break;
                case 6: AreaForest = v; break;
                default: AreaUnknown = v; break;
            }
        }
    }

    /// <summary>Parameters of a code-side mixer snapshot (blendable).</summary>
    public struct SnapshotParams
    {
        /// <summary>Gain on street beds and traffic (dB) and their low-pass (Hz, 0 = open).</summary>
        public float StreetDb, StreetLpfHz;
        /// <summary>Listener reverb: RT60 (s), pre-delay (ms), wet level (dB; −80 = off).</summary>
        public float ReverbRt60, ReverbPreDelayMs, ReverbWetDb;
        /// <summary>Slapback echo (galli): delay (ms), feedback, wet (dB; −80 = off).</summary>
        public float EchoDelayMs, EchoFeedback, EchoWetDb;
        /// <summary>Boost of sacred and pigeon layers (dB).</summary>
        public float SacredDb;

        public static SnapshotParams Lerp(in SnapshotParams a, in SnapshotParams b, float t)
        {
            float u = Dsp.Clamp01(t);
            return new SnapshotParams
            {
                StreetDb = Dsp.Lerp(a.StreetDb, b.StreetDb, u),
                StreetLpfHz = Dsp.Lerp(a.StreetLpfHz <= 0f ? 22000f : a.StreetLpfHz, b.StreetLpfHz <= 0f ? 22000f : b.StreetLpfHz, u),
                ReverbRt60 = Dsp.Lerp(a.ReverbRt60, b.ReverbRt60, u),
                ReverbPreDelayMs = Dsp.Lerp(a.ReverbPreDelayMs, b.ReverbPreDelayMs, u),
                ReverbWetDb = Dsp.Lerp(a.ReverbWetDb, b.ReverbWetDb, u),
                EchoDelayMs = Dsp.Lerp(a.EchoDelayMs, b.EchoDelayMs, u),
                EchoFeedback = Dsp.Lerp(a.EchoFeedback, b.EchoFeedback, u),
                EchoWetDb = Dsp.Lerp(a.EchoWetDb, b.EchoWetDb, u),
                SacredDb = Dsp.Lerp(a.SacredDb, b.SacredDb, u),
            };
        }
    }

    /// <summary>
    /// Engine-free ambience logic (W2_DESIGN 7.2; A §7): zone weights from the 250 m area-type grid with sacred
    /// polygons overriding, bed gains per zone × time band × season × weather, spot-emitter rates per minute with
    /// their time windows, and the mixer snapshot. Deterministic and allocation-free (callers pass spans).
    /// </summary>
    public static class AmbienceModel
    {
        public const int ZoneCount = 14;
        public const int BedCount = (int)BankSound.BedRiver - (int)BankSound.BedBase + 1;
        public const int SpotCount = 21;

        /// <summary>Crossfade time of the zone mix while walking and while driving (s).</summary>
        public const float CrossfadeWalkS = 3f, CrossfadeDriveS = 1.5f;

        // Time band starts (hours) and their multipliers (A §7.2 table; horn rate = mid of the range).
        private static readonly float[] BandStart = { 4.5f, 6f, 8f, 10.5f, 16f, 19f, 22f };
        private static readonly TimeMultipliers[] BandMult =
        {
            new TimeMultipliers { Traffic = 0.2f, Crowd = 0.1f, HornsPerMin = 1f, Sacred = 0.8f, Birds = 1.0f, Dogs = 0.6f, Insects = 0.4f },
            new TimeMultipliers { Traffic = 0.4f, Crowd = 0.4f, HornsPerMin = 5.5f, Sacred = 1.0f, Birds = 0.8f, Dogs = 0.3f, Insects = 0.1f },
            new TimeMultipliers { Traffic = 1.0f, Crowd = 0.8f, HornsPerMin = 15f, Sacred = 0.5f, Birds = 0.4f, Dogs = 0.1f, Insects = 0f },
            new TimeMultipliers { Traffic = 0.8f, Crowd = 1.0f, HornsPerMin = 11.5f, Sacred = 0.3f, Birds = 0.4f, Dogs = 0.1f, Insects = 0.3f },
            new TimeMultipliers { Traffic = 1.0f, Crowd = 1.0f, HornsPerMin = 15f, Sacred = 0.8f, Birds = 0.7f, Dogs = 0.3f, Insects = 0.3f },
            new TimeMultipliers { Traffic = 0.5f, Crowd = 0.4f, HornsPerMin = 5.5f, Sacred = 0.3f, Birds = 0.1f, Dogs = 0.6f, Insects = 0.8f },
            new TimeMultipliers { Traffic = 0.1f, Crowd = 0.05f, HornsPerMin = 0.5f, Sacred = 0f, Birds = 0f, Dogs = 1.0f, Insects = 1.0f },
        };

        /// <summary>Half-width of the blend at each band edge (h): 20 minutes either side.</summary>
        public const float BandBlendH = 1f / 3f;

        public static float WrapHour(float hour)
        {
            float h = Dsp.Sanitize(hour) % 24f;
            if (h < 0f) h += 24f;
            return h;
        }

        public static TimeBand BandOf(float hour)
        {
            float h = WrapHour(hour);
            for (int i = BandStart.Length - 1; i >= 0; i--)
                if (h >= BandStart[i]) return (TimeBand)i;
            return TimeBand.LateNight; // 00:00–04:30
        }

        public static TimeMultipliers BandMultipliers(TimeBand b)
        {
            int i = (int)b;
            if (i < 0 || i >= BandMult.Length) i = (int)TimeBand.Day;
            return BandMult[i];
        }

        /// <summary>Time multipliers at an hour, blended over ±20 minutes at each band edge (no audible steps).</summary>
        public static TimeMultipliers Multipliers(float hour)
        {
            float h = WrapHour(hour);
            TimeBand b = BandOf(h);
            TimeMultipliers m = BandMultipliers(b);
            for (int i = 0; i < BandStart.Length; i++)
            {
                float edge = BandStart[i];
                float d = h - edge;
                if (d > 12f) d -= 24f;
                if (d < -12f) d += 24f;
                if (d <= -BandBlendH || d >= BandBlendH) continue;
                TimeMultipliers before = BandMultipliers((TimeBand)((i + BandStart.Length - 1) % BandStart.Length));
                TimeMultipliers after = BandMultipliers((TimeBand)i);
                float t = (d + BandBlendH) / (2f * BandBlendH);
                return Blend(before, after, t);
            }
            return m;
        }

        private static TimeMultipliers Blend(in TimeMultipliers a, in TimeMultipliers b, float t)
        {
            return new TimeMultipliers
            {
                Traffic = Dsp.Lerp(a.Traffic, b.Traffic, t),
                Crowd = Dsp.Lerp(a.Crowd, b.Crowd, t),
                HornsPerMin = Dsp.Lerp(a.HornsPerMin, b.HornsPerMin, t),
                Sacred = Dsp.Lerp(a.Sacred, b.Sacred, t),
                Birds = Dsp.Lerp(a.Birds, b.Birds, t),
                Dogs = Dsp.Lerp(a.Dogs, b.Dogs, t),
                Insects = Dsp.Lerp(a.Insects, b.Insects, t),
            };
        }

        public static Season SeasonOf(int month)
        {
            int m = month < 1 || month > 12 ? 10 : month;
            if (m >= 3 && m <= 5) return Season.Spring;
            if (m >= 6 && m <= 9) return Season.Monsoon;
            if (m >= 10 && m <= 11) return Season.Autumn;
            return Season.Winter;
        }

        /// <summary>Zone weights (sum 1) at the listener. Sacred polygons override the grid (0.8 vs 0.2).</summary>
        public static void ZoneWeights(in AmbienceInputs a, Span<float> zones)
        {
            if (zones.Length < ZoneCount) throw new ArgumentException("zones span too short", nameof(zones));
            for (int i = 0; i < ZoneCount; i++) zones[i] = 0f;
            float unknown = Dsp.Clamp01(a.AreaUnknown);
            zones[(int)AmbienceZone.OldCore] = Dsp.Clamp01(a.AreaOldCore);
            zones[(int)AmbienceZone.Urban] = Dsp.Clamp01(a.AreaUrban);
            zones[(int)AmbienceZone.PeriUrban] = Dsp.Clamp01(a.AreaPeriUrban) + unknown;
            zones[(int)AmbienceZone.Fields] = Dsp.Clamp01(a.AreaRural) + Dsp.Clamp01(a.AreaHill);
            zones[(int)AmbienceZone.Forest] = Dsp.Clamp01(a.AreaForest);
            float grid = 0f;
            for (int i = 0; i <= (int)AmbienceZone.Forest; i++) grid += zones[i];
            if (!(grid > 1e-4f))
            {
                zones[(int)AmbienceZone.PeriUrban] = 1f;
                grid = 1f;
            }
            for (int i = 0; i <= (int)AmbienceZone.Forest; i++) zones[i] /= grid;

            // Overlays blend in on top of the grid mix.
            AddOverlay(zones, AmbienceZone.RingRoad, Dsp.Clamp01(a.RingRoad01) * 0.7f);
            AddOverlay(zones, AmbienceZone.Airport, Dsp.Clamp01(a.Airport01) * 0.5f);
            AddOverlay(zones, AmbienceZone.Park, Dsp.Clamp01(a.Park01) * 0.6f);
            AddOverlay(zones, AmbienceZone.Water, Dsp.Clamp01(a.Water01) * 0.6f);
            float hill = Dsp.Clamp01((Dsp.Sanitize(a.ElevationM) - 1550f) / 100f);
            AddOverlay(zones, AmbienceZone.Hilltop, hill * 0.7f);

            AmbienceZone sacred;
            if (TrySacredZone(a.SacredKind, out sacred)) AddOverlay(zones, sacred, 0.8f);
        }

        private static void AddOverlay(Span<float> zones, AmbienceZone z, float w)
        {
            if (!(w > 0f)) return;
            for (int i = 0; i < ZoneCount; i++) zones[i] *= 1f - w;
            zones[(int)z] += w;
        }

        /// <summary>The ambience zone a <c>SacredZoneKind</c> byte selects.</summary>
        public static bool TrySacredZone(byte sacredKind, out AmbienceZone zone)
        {
            switch (sacredKind)
            {
                case 1:
                case 2: zone = AmbienceZone.TempleCompound; return true;
                case 3: zone = AmbienceZone.DurbarSquare; return true;
                case 4: zone = AmbienceZone.StupaKora; return true;
                case 5: zone = AmbienceZone.Ghat; return true;
                default: zone = AmbienceZone.Urban; return false;
            }
        }

        private static int Bed(BankSound s)
        {
            return (int)s - (int)BankSound.BedBase;
        }

        /// <summary>Linear gain (0..1) of every bed, indexed <c>BankSound - BedBase</c>.</summary>
        public static void BedGains(in AmbienceInputs a, Span<float> beds)
        {
            if (beds.Length < BedCount) throw new ArgumentException("beds span too short", nameof(beds));
            Span<float> z = stackalloc float[ZoneCount];
            ZoneWeights(a, z);
            TimeMultipliers m = Multipliers(a.Hour);
            Season season = SeasonOf(a.Month);
            float rain = Dsp.Clamp01(a.Rain01);
            float wind = Dsp.Clamp01(a.Wind01);
            for (int i = 0; i < BedCount; i++) beds[i] = 0f;

            float crowd = m.Crowd;
            float traffic = m.Traffic;
            float birds = m.Birds * (1f - 0.75f * rain);            // −12 dB in heavy rain
            float crowdRain = 1f - 0.5f * rain;                       // −6 dB
            float insects = m.Insects * (season == Season.Winter ? 0.3f : season == Season.Monsoon ? 1.3f : 1f) * (1f - 0.6f * rain);
            float river = season == Season.Monsoon ? 1f : 0.5f;      // rivers +6 dB in monsoon

            beds[Bed(BankSound.BedCrowdDense)] += z[(int)AmbienceZone.OldCore] * crowd * crowdRain;
            beds[Bed(BankSound.BedTrafficHum)] += z[(int)AmbienceZone.OldCore] * 0.5f * traffic;
            beds[Bed(BankSound.BedTrafficHum)] += z[(int)AmbienceZone.Urban] * traffic;
            beds[Bed(BankSound.BedCrowdLight)] += z[(int)AmbienceZone.Urban] * 0.6f * crowd * crowdRain;
            beds[Bed(BankSound.BedTrafficHum)] += z[(int)AmbienceZone.PeriUrban] * 0.35f * traffic;
            beds[Bed(BankSound.BedBirdsPeri)] += z[(int)AmbienceZone.PeriUrban] * 0.8f * birds;
            beds[Bed(BankSound.BedWind)] += z[(int)AmbienceZone.Fields] * 0.7f;
            beds[Bed(BankSound.BedBirdsPeri)] += z[(int)AmbienceZone.Fields] * 0.4f * birds;
            beds[Bed(BankSound.BedWindLeaves)] += z[(int)AmbienceZone.Forest];
            beds[Bed(BankSound.BedBirdsPeri)] += z[(int)AmbienceZone.Forest] * 0.6f * birds;
            beds[Bed(BankSound.BedCourtyard)] += z[(int)AmbienceZone.TempleCompound] * Math.Max(0.25f, Math.Max(0.6f * crowd, m.Sacred));
            beds[Bed(BankSound.BedCrowdKora)] += z[(int)AmbienceZone.StupaKora] * Math.Max(0.3f, crowd) * crowdRain;
            beds[Bed(BankSound.BedCrowdDense)] += z[(int)AmbienceZone.DurbarSquare] * 0.8f * crowd * crowdRain;
            beds[Bed(BankSound.BedCourtyard)] += z[(int)AmbienceZone.DurbarSquare] * 0.3f;
            beds[Bed(BankSound.BedRiver)] += z[(int)AmbienceZone.Ghat] * river;
            beds[Bed(BankSound.BedCourtyard)] += z[(int)AmbienceZone.Ghat] * 0.5f * Math.Max(0.3f, m.Sacred);
            beds[Bed(BankSound.BedTrafficHeavy)] += z[(int)AmbienceZone.RingRoad] * traffic;
            beds[Bed(BankSound.BedCityHum)] += z[(int)AmbienceZone.Airport];
            beds[Bed(BankSound.BedBirdsPeri)] += z[(int)AmbienceZone.Park] * 0.8f * birds;
            beds[Bed(BankSound.BedTrafficHum)] += z[(int)AmbienceZone.Park] * 0.25f * traffic;
            beds[Bed(BankSound.BedRiver)] += z[(int)AmbienceZone.Water] * river;
            beds[Bed(BankSound.BedWind)] += z[(int)AmbienceZone.Hilltop];
            beds[Bed(BankSound.BedCityHum)] += z[(int)AmbienceZone.Hilltop] * (m.Traffic < 0.4f ? 0.8f : 0.4f);

            // Valley-wide layers: distant city hum at night, insects away from the old core, weather.
            float notCore = 1f - 0.7f * z[(int)AmbienceZone.OldCore];
            beds[Bed(BankSound.BedCityHum)] += 0.25f * (1f - traffic);
            beds[Bed(BankSound.BedInsectsNight)] += insects * notCore * 0.8f;
            beds[Bed(BankSound.BedWind)] *= 0.6f + 0.8f * wind;
            beds[Bed(BankSound.BedWindLeaves)] *= 0.6f + 0.8f * wind;
            beds[Bed(BankSound.BedWind)] += 0.4f * wind * wind;
            beds[Bed(BankSound.BedRainLight)] += Dsp.Clamp01(rain * 2f) * (1f - Dsp.Clamp01((rain - 0.5f) * 2f));
            beds[Bed(BankSound.BedRainHeavy)] += Dsp.Clamp01((rain - 0.4f) / 0.6f);
            if (season == Season.Winter && a.Hour < 10f) beds[Bed(BankSound.BedCityHum)] *= 0.7f; // fog −3 dB on distant layers

            for (int i = 0; i < BedCount; i++) beds[i] = Dsp.Clamp01(beds[i]);
        }

        /// <summary>Spot emitter rates (events per real minute around the listener), indexed by
        /// <see cref="SpotKind"/>, with the A §7.2 time windows (shutters, cookers, roosters, conch, bhajan
        /// cymbals) and seasons (koel Mar–Aug, monsoon thunder).</summary>
        public static void SpotRates(in AmbienceInputs a, Span<float> perMin)
        {
            if (perMin.Length < SpotCount) throw new ArgumentException("spot span too short", nameof(perMin));
            Span<float> z = stackalloc float[ZoneCount];
            ZoneWeights(a, z);
            TimeMultipliers m = Multipliers(a.Hour);
            float h = WrapHour(a.Hour);
            Season season = SeasonOf(a.Month);
            float rain = Dsp.Clamp01(a.Rain01);
            float birds = m.Birds * (1f - 0.75f * rain);
            for (int i = 0; i < SpotCount; i++) perMin[i] = 0f;

            float core = z[(int)AmbienceZone.OldCore], urban = z[(int)AmbienceZone.Urban], peri = z[(int)AmbienceZone.PeriUrban];
            float fields = z[(int)AmbienceZone.Fields], forest = z[(int)AmbienceZone.Forest], temple = z[(int)AmbienceZone.TempleCompound];
            float kora = z[(int)AmbienceZone.StupaKora], square = z[(int)AmbienceZone.DurbarSquare], ghat = z[(int)AmbienceZone.Ghat];
            float ring = z[(int)AmbienceZone.RingRoad], park = z[(int)AmbienceZone.Park];

            perMin[(int)SpotKind.ShrineBell] = (2f * core + 6f * temple + 4f * square + 3f * ghat + 2f * kora) * m.Sacred;
            perMin[(int)SpotKind.PigeonCoo] = (1f * core + 3f * temple + 4f * square + 3f * kora) * Math.Max(0.2f, birds);
            bool shutterHours = (h >= 8.5f && h < 10f) || (h >= 19.5f && h < 21.5f);
            perMin[(int)SpotKind.Shutter] = shutterHours ? 2f * core + 1f * urban + 0.5f * square : 0f;
            bool cookerHours = (h >= 7f && h < 9.5f) || (h >= 18f && h < 19.5f);
            perMin[(int)SpotKind.PressureCooker] = cookerHours ? 0.6f * core + 0.5f * urban + 0.8f * peri : 0f;
            perMin[(int)SpotKind.TeaClink] = (1f * core + 0.5f * urban + 0.4f * square) * m.Crowd;
            perMin[(int)SpotKind.Crow] = (3f * core + 4f * urban + 2f * peri + 4f * park + 1f * square) * birds;
            perMin[(int)SpotKind.Myna] = (1.5f * urban + 3f * peri + 3f * park + 2f * fields) * birds;
            perMin[(int)SpotKind.Sparrow] = (2f * core + 2f * urban + 3f * peri) * birds;
            bool kiteHours = h >= 8f && h < 17f;
            perMin[(int)SpotKind.Kite] = kiteHours ? 0.5f * (1f - rain) : 0f;
            perMin[(int)SpotKind.Bulbul] = (2f * park + 1.5f * peri + 2f * forest) * birds;
            bool koelSeason = a.Month >= 3 && a.Month <= 8;
            perMin[(int)SpotKind.Koel] = koelSeason ? (1f * park + 1f * peri + 1f * forest) * birds : 0f;
            perMin[(int)SpotKind.Dog] = (1f * peri + 0.8f * urban + 0.6f * core + 0.5f * fields) * m.Dogs * 2f;
            bool roosterHours = h >= 4.5f && h < 7f;
            perMin[(int)SpotKind.Rooster] = roosterHours ? 1.5f * peri + 1f * fields : 0f;
            perMin[(int)SpotKind.Cow] = 0.3f * ring + 0.4f * peri + 0.6f * fields;
            perMin[(int)SpotKind.DistantHorn] = m.HornsPerMin * (0.5f * urban + 1f * ring + 0.3f * core + 0.2f * peri);
            perMin[(int)SpotKind.AirBrake] = (1.5f * ring + 0.8f * urban) * m.Traffic;
            bool conchHours = (h >= 5f && h < 6.5f) || (h >= 18f && h < 19.25f);
            perMin[(int)SpotKind.Conch] = conchHours ? 0.5f * temple + 0.8f * ghat + 0.2f * square : 0f;
            bool bhajanHours = h >= 18f && h < 20.5f;
            perMin[(int)SpotKind.Cymbal] = bhajanHours ? 0.3f * temple + 0.5f * ghat + 0.2f * square : 0f;
            perMin[(int)SpotKind.PrayerWheel] = 8f * kora * Math.Max(0.3f, m.Crowd);
            perMin[(int)SpotKind.Cricket] = (6f * fields + 4f * forest + 3f * peri + 3f * park) * m.Insects * (season == Season.Winter ? 0.3f : 1f);
            perMin[(int)SpotKind.Thunder] = season == Season.Monsoon && rain > 0.6f ? 0.5f * rain : 0f;

            for (int i = 0; i < SpotCount; i++) perMin[i] = Math.Max(0f, Dsp.Sanitize(perMin[i]));
        }

        /// <summary>The bank sound a spot plays.</summary>
        public static BankSound SpotSound(SpotKind k)
        {
            switch (k)
            {
                case SpotKind.ShrineBell: return BankSound.ShrineBellMedium;
                case SpotKind.PigeonCoo: return BankSound.PigeonCoo;
                case SpotKind.Shutter: return BankSound.Shutter;
                case SpotKind.PressureCooker: return BankSound.PressureCooker;
                case SpotKind.TeaClink: return BankSound.TeaClink;
                case SpotKind.Crow: return BankSound.Crow;
                case SpotKind.Myna: return BankSound.Myna;
                case SpotKind.Sparrow: return BankSound.Sparrow;
                case SpotKind.Kite: return BankSound.Kite;
                case SpotKind.Bulbul: return BankSound.Bulbul;
                case SpotKind.Koel: return BankSound.Koel;
                case SpotKind.Dog: return BankSound.DogBark;
                case SpotKind.Rooster: return BankSound.Rooster;
                case SpotKind.Cow: return BankSound.CowMoo;
                case SpotKind.DistantHorn: return BankSound.HornCar;
                case SpotKind.AirBrake: return BankSound.AirBrake;
                case SpotKind.Conch: return BankSound.Conch;
                case SpotKind.Cymbal: return BankSound.Cymbal;
                case SpotKind.PrayerWheel: return BankSound.PrayerWheel;
                case SpotKind.Cricket: return BankSound.Cricket;
                case SpotKind.Thunder: return BankSound.Thunder;
                default: return BankSound.None;
            }
        }

        /// <summary>Distance range (m) at which a spot is placed around the listener.</summary>
        public static void SpotDistance(SpotKind k, out float minM, out float maxM)
        {
            switch (k)
            {
                case SpotKind.Kite: minM = 40f; maxM = 150f; return;
                case SpotKind.Dog: minM = 30f; maxM = 220f; return;
                case SpotKind.DistantHorn: minM = 40f; maxM = 150f; return;
                case SpotKind.AirBrake: minM = 20f; maxM = 80f; return;
                case SpotKind.Conch: minM = 15f; maxM = 90f; return;
                case SpotKind.Thunder: minM = 0f; maxM = 0f; return; // 2D
                case SpotKind.PrayerWheel: minM = 3f; maxM = 15f; return;
                case SpotKind.ShrineBell: minM = 5f; maxM = 30f; return;
                case SpotKind.TeaClink: minM = 4f; maxM = 20f; return;
                case SpotKind.Cricket: minM = 4f; maxM = 30f; return;
                default: minM = 8f; maxM = 50f; return;
            }
        }

        /// <summary>The snapshot weights at the listener (sum 1), indexed by <see cref="MixSnapshot"/>.</summary>
        public static void SnapshotWeights(in AmbienceInputs a, Span<float> w)
        {
            if (w.Length < 5) throw new ArgumentException("snapshot span too short", nameof(w));
            for (int i = 0; i < 5; i++) w[i] = 0f;
            if (a.InVehicle)
            {
                w[(int)MixSnapshot.VehicleInterior] = 1f;
                return;
            }
            switch (a.SacredKind)
            {
                case 1:
                case 2:
                    w[(int)MixSnapshot.Courtyard] = 1f;
                    return;
                case 3:
                case 4:
                    w[(int)MixSnapshot.DurbarSquare] = 1f;
                    return;
            }
            float lane = Dsp.Sanitize(a.LaneWidthM);
            if (lane > 0.5f && lane < 8f)
            {
                // Narrow lanes in built-up areas: full slapback below 5 m, fading out by 8 m.
                float built = Dsp.Clamp01(a.AreaOldCore + a.AreaUrban);
                float g = Dsp.Clamp01((8f - lane) / 3f) * built;
                w[(int)MixSnapshot.Galli] = g;
                w[(int)MixSnapshot.Street] = 1f - g;
                return;
            }
            w[(int)MixSnapshot.Street] = 1f;
        }

        /// <summary>Parameters of one snapshot (W2_DESIGN 7.1; A §6.5).</summary>
        public static SnapshotParams Snapshot(MixSnapshot s, float laneWidthM = 4f)
        {
            switch (s)
            {
                case MixSnapshot.Courtyard:
                    return new SnapshotParams { StreetDb = -9f, StreetLpfHz = 2500f, ReverbRt60 = 0.85f, ReverbPreDelayMs = 30f, ReverbWetDb = -16f,
                                                EchoWetDb = -80f, SacredDb = 3f };
                case MixSnapshot.DurbarSquare:
                    return new SnapshotParams { StreetDb = -3f, StreetLpfHz = 0f, ReverbRt60 = 1.2f, ReverbPreDelayMs = 60f, ReverbWetDb = -18f,
                                                EchoWetDb = -80f, SacredDb = 2f };
                case MixSnapshot.Galli:
                    return new SnapshotParams { StreetDb = 0f, StreetLpfHz = 0f, ReverbRt60 = 0.3f, ReverbWetDb = -80f,
                                                EchoDelayMs = SoundSpace.SlapbackDelayS(laneWidthM) * 1000f, EchoFeedback = 0.15f, EchoWetDb = -14f };
                case MixSnapshot.VehicleInterior:
                    return new SnapshotParams { StreetDb = -8f, StreetLpfHz = 3000f, ReverbRt60 = 0.2f, ReverbWetDb = -80f, EchoWetDb = -80f };
                default:
                    return new SnapshotParams { StreetDb = 0f, StreetLpfHz = 0f, ReverbRt60 = 0.5f, ReverbWetDb = -80f, EchoWetDb = -80f };
            }
        }

        /// <summary>The blended snapshot for the given weights.</summary>
        public static SnapshotParams Blend(ReadOnlySpan<float> weights, float laneWidthM)
        {
            SnapshotParams acc = Snapshot(MixSnapshot.Street, laneWidthM);
            float total = 0f;
            for (int i = 0; i < 5 && i < weights.Length; i++)
            {
                float w = Dsp.Clamp01(weights[i]);
                if (!(w > 0f)) continue;
                total += w;
                acc = SnapshotParams.Lerp(acc, Snapshot((MixSnapshot)i, laneWidthM), w / total);
            }
            return acc;
        }
    }
}
