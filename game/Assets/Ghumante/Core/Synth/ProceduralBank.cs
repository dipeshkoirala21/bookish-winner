using System;

namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Every sound the bank bakes at region load (W2_DESIGN 7.1 "Baked bank", 7.3 method B). Append-only values.
    /// Footstep sets are <c>FootstepBase + (int)FootstepSurface</c>. Values ≥ <see cref="BedBase"/> are seamless
    /// ambience loops.
    /// </summary>
    public enum BankSound : ushort
    {
        None = 0,

        // ---- footsteps (variants: 0–5 walk, 6–11 run, 12 land, 13 scuff) ----
        FootstepAsphalt = 1,
        FootstepConcrete = 2,
        FootstepBrick = 3,
        FootstepStone = 4,
        FootstepGravel = 5,
        FootstepDirt = 6,
        FootstepMud = 7,
        FootstepGrass = 8,
        FootstepWood = 9,
        FootstepMetal = 10,
        FootstepWater = 11,
        FootstepBarefoot = 12,

        // ---- horns and vehicle one-shots (variants: 0 short tap, 1 tap, 2 double tap, 3 long blast) ----
        HornMoto = 20,
        HornScooter = 21,
        HornCar = 22,
        HornBus = 23,
        HornTempo = 24,
        BicycleBell = 26,
        RimSqueal = 27,
        AirBrake = 30,
        ParkingBrake = 31,
        ReverseAlarm = 32,
        Indicator = 33,
        DoorHiss = 34,
        ConductorSlap = 35,
        GearClunk = 36,
        KeyStart = 37,
        GearGrind = 38,

        // ---- sacred (A §2.8; no recorded or synthesised chant) ----
        ShrineBellSmall = 40,
        ShrineBellMedium = 41,
        ShrineBellLarge = 42,
        HandBell = 43,
        PrayerWheel = 44,
        Conch = 45,
        Cymbal = 46,
        GreatBell = 47,

        // ---- birds and animals (A §2.9) ----
        Crow = 50,
        PigeonCoo = 51,
        PigeonFlock = 52,
        Myna = 53,
        Sparrow = 54,
        Kite = 55,
        DogBark = 56,
        CowMoo = 57,
        Rooster = 58,
        Cricket = 59,
        Koel = 60,
        Bulbul = 61,

        // ---- people and street (A §2.7) ----
        Shutter = 70,
        PressureCooker = 71,
        TeaClink = 72,

        // ---- player foley and UI (A §2.12) ----
        UiBoing = 80,
        JumpWhoosh = 81,
        HelmetPop = 82,
        ClothRustle = 83,

        // ---- weather ----
        Thunder = 90,

        // ---- ambience beds (seamless loops) ----
        BedBase = 100,
        BedWind = 100,
        BedWindLeaves = 101,
        BedRainLight = 102,
        BedRainHeavy = 103,
        BedCrowdDense = 104,
        BedCrowdLight = 105,
        BedCrowdKora = 106,
        BedTrafficHum = 107,
        BedTrafficHeavy = 108,
        BedCityHum = 109,
        BedCourtyard = 110,
        BedBirdsPeri = 111,
        BedInsectsNight = 112,
        BedRiver = 113,
    }

    /// <summary>Static facts about a <see cref="BankSound"/>.</summary>
    public readonly struct BankSoundInfo
    {
        public readonly BankSound Sound;
        /// <summary>Variants baked on Mid and High (Low bakes <see cref="LowVariants"/>).</summary>
        public readonly byte Variants;
        public readonly byte LowVariants;
        /// <summary>Preferred sample rate (32 kHz; small bells, birds and beds 22.05 kHz; long rumbles 16 kHz).</summary>
        public readonly int SampleRate;
        public readonly bool Loop;
        public readonly MixBus Bus;
        /// <summary>Default sound class for spatial rules (§7.1 distances and priorities).</summary>
        public readonly SoundClass Class;

        public BankSoundInfo(BankSound s, byte variants, byte lowVariants, int rate, bool loop, MixBus bus, SoundClass cls)
        {
            Sound = s;
            Variants = variants;
            LowVariants = lowVariants;
            SampleRate = rate;
            Loop = loop;
            Bus = bus;
            Class = cls;
        }

        public bool IsValid
        {
            get { return Sound != BankSound.None && Variants > 0; }
        }
    }

    /// <summary>
    /// The baked procedural bank (W2_DESIGN 10.3 contract <c>ProceduralBank.Render</c>): every one-shot and
    /// ambience loop is rendered from code and a seed, so nothing is downloaded and every device hears the same
    /// variants. Thread-safe (no shared mutable state); allocates only the returned array. Output is mono,
    /// finite and within [−1, 1]; loops join seamlessly (equal-power crossfade of the tail into the head).
    /// </summary>
    public static partial class ProceduralBank
    {
        /// <summary>The crowd walla beds (W2 detail pass decision 7: off by default, so the runtime bank skips them
        /// unless <see cref="AmbienceInputs.CrowdWalla"/> is wanted).</summary>
        public static bool IsCrowdWalla(BankSound s)
        {
            return s == BankSound.BedCrowdDense || s == BankSound.BedCrowdLight || s == BankSound.BedCrowdKora;
        }

        public const int Rate32k = 32000;
        public const int Rate22k = 22050;
        public const int Rate16k = 16000;

        /// <summary>Longest sound the bank renders (s); guards against runaway lengths.</summary>
        public const float MaxSeconds = 12f;

        private static readonly BankSoundInfo[] Infos = BuildInfos();

        /// <summary>Every bank sound with its info (stable order: by value).</summary>
        public static BankSoundInfo[] All
        {
            get { return (BankSoundInfo[])Infos.Clone(); }
        }

        public static int Count
        {
            get { return Infos.Length; }
        }

        public static BankSoundInfo InfoAt(int index)
        {
            return Infos[index];
        }

        public static BankSoundInfo Info(BankSound s)
        {
            for (int i = 0; i < Infos.Length; i++)
                if (Infos[i].Sound == s) return Infos[i];
            return default;
        }

        public static int VariantCount(BankSound s)
        {
            return Info(s).Variants;
        }

        /// <summary>The footstep set for a surface.</summary>
        public static BankSound Footstep(FootstepSurface surface)
        {
            int v = (int)surface;
            if (v < 0 || v > (int)FootstepSurface.Barefoot) v = 0;
            return (BankSound)((int)BankSound.FootstepAsphalt + v);
        }

        public static BankSound Horn(HornKind kind)
        {
            switch (kind)
            {
                case HornKind.Moto: return BankSound.HornMoto;
                case HornKind.Scooter: return BankSound.HornScooter;
                case HornKind.Car: return BankSound.HornCar;
                case HornKind.Bus: return BankSound.HornBus;
                case HornKind.Tempo: return BankSound.HornTempo;
                case HornKind.BicycleBell: return BankSound.BicycleBell;
                default: return BankSound.None;
            }
        }

        public static BankSound Bell(BellKind kind)
        {
            switch (kind)
            {
                case BellKind.ShrineSmall: return BankSound.ShrineBellSmall;
                case BellKind.ShrineMedium: return BankSound.ShrineBellMedium;
                case BellKind.ShrineLarge: return BankSound.ShrineBellLarge;
                case BellKind.HandBell: return BankSound.HandBell;
                case BellKind.PrayerWheel: return BankSound.PrayerWheel;
                case BellKind.Conch: return BankSound.Conch;
                case BellKind.Cymbal: return BankSound.Cymbal;
                case BellKind.GreatBell: return BankSound.GreatBell;
                default: return BankSound.None;
            }
        }

        /// <summary>
        /// Renders variant <paramref name="variant"/> of <paramref name="s"/> at <paramref name="sampleRate"/>
        /// (pass <see cref="BankSoundInfo.SampleRate"/> normally). The same (sound, variant, rate, seed) always
        /// gives the same samples. Unknown sounds return an empty array.
        /// </summary>
        public static float[] Render(BankSound s, int variant, int sampleRate, uint seed)
        {
            BankSoundInfo info = Info(s);
            if (!info.IsValid) return new float[0];
            int sr = sampleRate >= 8000 && sampleRate <= 96000 ? sampleRate : info.SampleRate;
            int v = variant < 0 ? 0 : variant % info.Variants;
            var ctx = new BankContext(sr, Dsp.Mix(seed, (uint)s, (uint)v), v);
            float[] o;
            int si = (int)s;
            if (si >= (int)BankSound.FootstepAsphalt && si <= (int)BankSound.FootstepBarefoot)
                o = RenderFootstep((FootstepSurface)(si - (int)BankSound.FootstepAsphalt), ref ctx);
            else if (si >= (int)BankSound.BedBase)
                o = RenderBed(s, ref ctx);
            else
                o = RenderOneShot(s, ref ctx);
            if (o == null) return new float[0];
            Dsp.Finish(o, 0, o.Length);
            return o;
        }

        private static BankSoundInfo I(BankSound s, int v, int low, int rate, MixBus bus, SoundClass cls, bool loop = false)
        {
            return new BankSoundInfo(s, (byte)v, (byte)low, rate, loop, bus, cls);
        }

        private static BankSoundInfo[] BuildInfos()
        {
            var list = new System.Collections.Generic.List<BankSoundInfo>();
            for (int i = (int)BankSound.FootstepAsphalt; i <= (int)BankSound.FootstepBarefoot; i++)
                list.Add(I((BankSound)i, FootstepVariants, FootstepVariants, Rate22k, MixBus.Player, SoundClass.PlayerFootstep));
            list.Add(I(BankSound.HornMoto, 4, 3, Rate22k, MixBus.Traffic, SoundClass.HornMoto));
            list.Add(I(BankSound.HornScooter, 4, 3, Rate22k, MixBus.Traffic, SoundClass.HornMoto));
            list.Add(I(BankSound.HornCar, 4, 3, Rate22k, MixBus.Traffic, SoundClass.Horn));
            list.Add(I(BankSound.HornBus, 4, 3, Rate22k, MixBus.Traffic, SoundClass.Horn));
            list.Add(I(BankSound.HornTempo, 4, 3, Rate22k, MixBus.Traffic, SoundClass.HornMoto));
            list.Add(I(BankSound.BicycleBell, 4, 2, Rate22k, MixBus.Traffic, SoundClass.Bicycle));
            list.Add(I(BankSound.RimSqueal, 3, 2, Rate22k, MixBus.Traffic, SoundClass.Bicycle));
            list.Add(I(BankSound.AirBrake, 4, 2, Rate22k, MixBus.Traffic, SoundClass.AirBrake));
            list.Add(I(BankSound.ParkingBrake, 2, 1, Rate22k, MixBus.Traffic, SoundClass.AirBrake));
            list.Add(I(BankSound.ReverseAlarm, 1, 1, Rate22k, MixBus.Traffic, SoundClass.Heavy, true));
            list.Add(I(BankSound.Indicator, 1, 1, Rate22k, MixBus.Player, SoundClass.Foley, true));
            list.Add(I(BankSound.DoorHiss, 2, 1, Rate22k, MixBus.Traffic, SoundClass.Heavy));
            list.Add(I(BankSound.ConductorSlap, 3, 2, Rate22k, MixBus.Traffic, SoundClass.Heavy));
            list.Add(I(BankSound.GearClunk, 3, 2, Rate22k, MixBus.Player, SoundClass.Foley));
            list.Add(I(BankSound.KeyStart, 2, 1, Rate22k, MixBus.Player, SoundClass.Foley));
            list.Add(I(BankSound.GearGrind, 1, 1, Rate22k, MixBus.Player, SoundClass.Foley));
            list.Add(I(BankSound.ShrineBellSmall, 3, 2, Rate16k, MixBus.Sacred, SoundClass.ShrineBell));
            list.Add(I(BankSound.ShrineBellMedium, 3, 2, Rate16k, MixBus.Sacred, SoundClass.ShrineBell));
            list.Add(I(BankSound.ShrineBellLarge, 3, 2, Rate16k, MixBus.Sacred, SoundClass.ShrineBell));
            list.Add(I(BankSound.HandBell, 3, 2, Rate22k, MixBus.Sacred, SoundClass.ShrineBell));
            list.Add(I(BankSound.PrayerWheel, 3, 2, Rate22k, MixBus.Sacred, SoundClass.ShrineBell));
            list.Add(I(BankSound.Conch, 2, 1, Rate22k, MixBus.Sacred, SoundClass.Conch));
            list.Add(I(BankSound.Cymbal, 4, 2, Rate22k, MixBus.Sacred, SoundClass.ShrineBell));
            list.Add(I(BankSound.GreatBell, 3, 3, Rate16k, MixBus.Sacred, SoundClass.GreatBell));
            list.Add(I(BankSound.Crow, 8, 4, Rate22k, MixBus.Nature, SoundClass.Crow));
            list.Add(I(BankSound.PigeonCoo, 6, 3, Rate22k, MixBus.Nature, SoundClass.Bird));
            list.Add(I(BankSound.PigeonFlock, 3, 2, Rate22k, MixBus.Nature, SoundClass.Bird));
            list.Add(I(BankSound.Myna, 6, 3, Rate22k, MixBus.Nature, SoundClass.Bird));
            list.Add(I(BankSound.Sparrow, 8, 4, Rate22k, MixBus.Nature, SoundClass.Bird));
            list.Add(I(BankSound.Kite, 4, 2, Rate22k, MixBus.Nature, SoundClass.Kite));
            list.Add(I(BankSound.DogBark, 10, 5, Rate22k, MixBus.Nature, SoundClass.DogBark));
            list.Add(I(BankSound.CowMoo, 4, 2, Rate22k, MixBus.Nature, SoundClass.Animal));
            list.Add(I(BankSound.Rooster, 3, 2, Rate22k, MixBus.Nature, SoundClass.Animal));
            list.Add(I(BankSound.Cricket, 2, 1, Rate22k, MixBus.Nature, SoundClass.Bird));
            list.Add(I(BankSound.Koel, 3, 2, Rate22k, MixBus.Nature, SoundClass.Bird));
            list.Add(I(BankSound.Bulbul, 4, 2, Rate22k, MixBus.Nature, SoundClass.Bird));
            list.Add(I(BankSound.Shutter, 3, 2, Rate22k, MixBus.Ambience, SoundClass.Street));
            list.Add(I(BankSound.PressureCooker, 3, 2, Rate22k, MixBus.Ambience, SoundClass.Street));
            list.Add(I(BankSound.TeaClink, 4, 2, Rate22k, MixBus.Ambience, SoundClass.Street));
            list.Add(I(BankSound.UiBoing, 2, 2, Rate22k, MixBus.Ui, SoundClass.Ui));
            list.Add(I(BankSound.JumpWhoosh, 3, 2, Rate22k, MixBus.Player, SoundClass.Foley));
            list.Add(I(BankSound.HelmetPop, 2, 1, Rate22k, MixBus.Player, SoundClass.Foley));
            list.Add(I(BankSound.ClothRustle, 4, 2, Rate22k, MixBus.Player, SoundClass.Foley));
            list.Add(I(BankSound.Thunder, 3, 2, Rate16k, MixBus.Nature, SoundClass.Thunder));
            for (int i = (int)BankSound.BedWind; i <= (int)BankSound.BedRiver; i++)
                list.Add(I((BankSound)i, 1, 1, Rate16k, MixBus.Ambience, SoundClass.Ambience, true));
            return list.ToArray();
        }

        /// <summary>Footstep variants per set (6 walk + 6 run + land + scuff).</summary>
        public const int FootstepVariants = 14;

        /// <summary>The bank variant for a gait and a 0-based index within it (walk/run 0–5).</summary>
        public static int FootstepVariant(FootstepGait gait, int index)
        {
            int i = index < 0 ? -index : index;
            switch (gait)
            {
                case FootstepGait.Run: return 6 + i % 6;
                case FootstepGait.Land: return 12;
                case FootstepGait.Scuff: return 13;
                default: return i % 6;
            }
        }

        /// <summary>Approximate total PCM16 bytes the bank holds (Mid/High, or Low with its reduced variants and
        /// rate), for budgets and the audio memory HUD.</summary>
        public static long EstimateBytes(bool low)
        {
            long total = 0;
            for (int i = 0; i < Infos.Length; i++)
            {
                BankSoundInfo info = Infos[i];
                int variants = low ? info.LowVariants : info.Variants;
                int rate = low ? LowRate(info.SampleRate) : info.SampleRate;
                total += (long)(NominalSeconds(info.Sound) * rate) * 2 * variants;
            }
            return total;
        }

        /// <summary>Sample rate the Low tier bakes at (W2_DESIGN 7.1: ≈ 3 MB-class bank on Low): at most 16 kHz.</summary>
        public static int LowRate(int preferred)
        {
            return preferred > Rate16k ? Rate16k : preferred;
        }

        /// <summary>Nominal length of a sound (s), used for memory estimates.</summary>
        public static float NominalSeconds(BankSound s)
        {
            int si = (int)s;
            if (si >= (int)BankSound.BedBase) return BedSeconds;
            if (si <= (int)BankSound.FootstepBarefoot) return 0.36f;
            switch (s)
            {
                case BankSound.HornMoto:
                case BankSound.HornScooter:
                case BankSound.HornCar:
                case BankSound.HornBus:
                case BankSound.HornTempo: return 0.38f;
                case BankSound.ShrineBellSmall: return 3f;
                case BankSound.ShrineBellMedium: return 4f;
                case BankSound.ShrineBellLarge: return 5f;
                case BankSound.GreatBell: return 8f;
                case BankSound.Thunder: return 4.8f;
                case BankSound.Conch: return 3.2f;
                case BankSound.PressureCooker: return 2.4f;
                case BankSound.Shutter: return 2.0f;
                case BankSound.Rooster: return 2.2f;
                case BankSound.Kite: return 1.8f;
                case BankSound.PigeonFlock: return 1.6f;
                case BankSound.ParkingBrake: return 1.1f;
                case BankSound.Crow: return 1.2f;
                case BankSound.DogBark: return 1.1f;
                case BankSound.Myna: return 1.6f;
                case BankSound.Sparrow: return 1.1f;
                case BankSound.Koel: return 3.0f;
                case BankSound.HandBell: return 2.3f;
                case BankSound.PrayerWheel: return 1.6f;
                case BankSound.KeyStart: return 1.5f;
                case BankSound.CowMoo: return 1.5f;
                case BankSound.Cricket: return 1.4f;
                case BankSound.AirBrake: return 1.1f;
                case BankSound.Indicator: return 0.67f;
                case BankSound.ReverseAlarm: return 1f;
                case BankSound.UiBoing:
                case BankSound.JumpWhoosh:
                case BankSound.HelmetPop:
                case BankSound.GearClunk:
                case BankSound.GearGrind:
                case BankSound.ClothRustle: return 0.3f;
                default: return 0.8f;
            }
        }

        /// <summary>Length of every ambience loop (s).</summary>
        public const float BedSeconds = 6f;
    }

    /// <summary>Per-render state handed to the recipes (sample rate, seeded noise, variant).</summary>
    internal struct BankContext
    {
        public readonly int Rate;
        public readonly int Variant;
        public Noise Rng;

        public BankContext(int rate, uint seed, int variant)
        {
            Rate = rate;
            Variant = variant;
            Rng = new Noise(seed);
        }

        public int Samples(float seconds)
        {
            float s = Dsp.Clamp(seconds, 0.001f, ProceduralBank.MaxSeconds);
            return Math.Max(1, (int)(s * Rate));
        }

        public float Range(float lo, float hi)
        {
            return Rng.Range(lo, hi);
        }
    }
}
