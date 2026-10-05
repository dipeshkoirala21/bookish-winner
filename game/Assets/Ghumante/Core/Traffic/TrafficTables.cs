using System;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;

namespace Ghumante.Core.Traffic
{
    /// <summary>
    /// A small deterministic generator (SplitMix64) for the simulations: seeded from <see cref="Hashes"/> values, never
    /// from the clock. A struct: copy it to fork a stream, keep it in a field to advance it.
    /// </summary>
    public struct SimRng
    {
        private ulong _s;

        public SimRng(ulong seed)
        {
            _s = seed ^ 0x9E3779B97F4A7C15UL;
        }

        /// <summary>A seed mixed from several values (order matters).</summary>
        public static ulong Mix(ulong a, ulong b)
        {
            unchecked
            {
                ulong z = a * 0x9E3779B97F4A7C15UL ^ (b + 0x632BE59BD9B4E019UL + (a << 6) + (a >> 2));
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public static ulong Mix(ulong a, ulong b, ulong c)
        {
            return Mix(Mix(a, b), c);
        }

        public ulong NextU64()
        {
            unchecked
            {
                ulong z = _s += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public uint NextU32()
        {
            return (uint)(NextU64() >> 32);
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextU64() >> 40) * (1f / 16777216f);
        }

        /// <summary>Uniform in [0, 1) as a double.</summary>
        public double NextDouble()
        {
            return (NextU64() >> 11) * (1.0 / 9007199254740992.0);
        }

        public float Range(float a, float b)
        {
            return a + (b - a) * NextFloat();
        }

        /// <summary>Uniform integer in [0, n).</summary>
        public int Next(int n)
        {
            return n <= 1 ? 0 : (int)((NextU64() >> 33) % (ulong)n);
        }

        public bool Chance(float p)
        {
            return NextFloat() < p;
        }
    }

    /// <summary>IDM parameters of one class (W2_DESIGN 5.1).</summary>
    public struct IdmParams
    {
        /// <summary>Desired speed factor on the lane's target (micro −10%, bus −15%, truck −10%) and an absolute cap
        /// (bicycle 14, rickshaw 8, tractor 18, tempo 25 km/h; 0 = none).</summary>
        public float SpeedFactor, CapKmh;

        /// <summary>Time headway T (s), minimum gap s0 (m), maximum acceleration a and comfortable deceleration b (m/s²).</summary>
        public float T, S0, A, B;
    }

    /// <summary>
    /// The numbers of the traffic simulation (W2_DESIGN 5.1-5.2 and vehicles research §2.2, §4.2-4.5): IDM per class,
    /// speed targets by road class, time-of-day density, the spawn mix by road class and area, and class sizes.
    /// </summary>
    public static class TrafficTables
    {
        /// <summary>The Intelligent Driver Model acceleration: a·[1 − (v/v0)⁴ − (s*/s)²] with
        /// s* = s0 + v·T + v·Δv / (2√(a·b)); <paramref name="gap"/> is bumper to bumper (+∞ for a free road),
        /// <paramref name="dv"/> the approach rate (own speed minus the leader's).</summary>
        public static float IdmAccel(in IdmParams p, float v, float v0, float gap, float dv)
        {
            if (v0 < 0.1f) v0 = 0.1f;
            float r = v / v0;
            float free = 1f - r * r * r * r;
            if (float.IsInfinity(gap)) return p.A * free;
            float sStar = p.S0 + Math.Max(0f, v * p.T + v * dv / (2f * MathF.Sqrt(p.A * p.B)));
            float s = Math.Max(gap, 0.05f);
            float q = sStar / s;
            return p.A * (free - q * q);
        }

        public static IdmParams Idm(VehicleClass c)
        {
            switch (c)
            {
                case VehicleClass.TwoWheeler: return new IdmParams { SpeedFactor = 1f, T = 0.9f, S0 = 1.0f, A = 2.5f, B = 3.0f };
                case VehicleClass.Car:
                case VehicleClass.Taxi:
                case VehicleClass.Suv:
                case VehicleClass.Service:
                    return new IdmParams { SpeedFactor = 1f, T = 1.2f, S0 = 2.0f, A = 2.0f, B = 3.0f };
                case VehicleClass.Microbus:
                case VehicleClass.Minibus:
                    return new IdmParams { SpeedFactor = 0.9f, T = 1.4f, S0 = 2.5f, A = 1.4f, B = 2.5f };
                case VehicleClass.Tempo: return new IdmParams { SpeedFactor = 0.9f, CapKmh = 25f, T = 1.4f, S0 = 2.5f, A = 1.4f, B = 2.5f };
                case VehicleClass.Bus: return new IdmParams { SpeedFactor = 0.85f, T = 1.7f, S0 = 3.0f, A = 0.9f, B = 2.0f };
                case VehicleClass.Truck:
                case VehicleClass.Tanker:
                    return new IdmParams { SpeedFactor = 0.9f, T = 1.7f, S0 = 3.0f, A = 0.9f, B = 2.0f };
                case VehicleClass.Bicycle: return new IdmParams { SpeedFactor = 1f, CapKmh = 14f, T = 1.0f, S0 = 1.0f, A = 0.8f, B = 2.0f };
                case VehicleClass.Rickshaw: return new IdmParams { SpeedFactor = 1f, CapKmh = 8f, T = 1.0f, S0 = 1.0f, A = 0.8f, B = 2.0f };
                case VehicleClass.Tractor: return new IdmParams { SpeedFactor = 1f, CapKmh = 18f, T = 1.0f, S0 = 1.0f, A = 0.8f, B = 2.0f };
                default: return new IdmParams { SpeedFactor = 1f, T = 1.2f, S0 = 2.0f, A = 2.0f, B = 3.0f };
            }
        }

        /// <summary>Agent target speeds (free / peak, km/h) by road class and area (W2_DESIGN 5.1): trunk 45 / 15, trunk
        /// service 28 / 10, primary 35 / 12, secondary 30 / 12, tertiary 25 / 10, residential and unclassified 18 / 8,
        /// old-core lanes and living streets 10 / 5.</summary>
        public static void Speeds(RoadClass c, AreaType area, bool serviceRoad, out float freeKmh, out float peakKmh)
        {
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk:
                    freeKmh = serviceRoad ? 28f : 45f;
                    peakKmh = serviceRoad ? 10f : 15f;
                    break;
                case RoadClass.Primary:
                    freeKmh = serviceRoad ? 28f : 35f;
                    peakKmh = serviceRoad ? 10f : 12f;
                    break;
                case RoadClass.Secondary:
                    freeKmh = 30f;
                    peakKmh = 12f;
                    break;
                case RoadClass.Tertiary:
                    freeKmh = 25f;
                    peakKmh = 10f;
                    break;
                case RoadClass.LivingStreet:
                case RoadClass.Pedestrian:
                    freeKmh = 10f;
                    peakKmh = 5f;
                    break;
                case RoadClass.Track:
                    freeKmh = 15f;
                    peakKmh = 8f;
                    break;
                default:
                    freeKmh = 18f;
                    peakKmh = 8f;
                    break;
            }
            if (area == AreaType.OldCore && c >= RoadClass.Tertiary)
            {
                freeKmh = Math.Min(freeKmh, 10f);
                peakKmh = Math.Min(peakKmh, 5f);
            }
        }

        /// <summary>Traffic density multiplier by game hour (W2_DESIGN 5.1): 05–07 0.3, 07–08 0.7, 08–10 1.0, 10–16 0.75,
        /// 16–19 1.0, 19–22 0.5, 22–05 0.12.</summary>
        public static float Density(float hour)
        {
            float h = ((hour % 24f) + 24f) % 24f;
            if (h < 5f) return 0.12f;
            if (h < 7f) return 0.3f;
            if (h < 8f) return 0.7f;
            if (h < 10f) return 1.0f;
            if (h < 16f) return 0.75f;
            if (h < 19f) return 1.0f;
            if (h < 22f) return 0.5f;
            return 0.12f;
        }

        /// <summary>Congestion in [0, 1] for the speed blend free → peak: 0 below density 0.5, 1 at 1.0.</summary>
        public static float Congestion(float density)
        {
            float c = (density - 0.5f) / 0.5f;
            return c < 0f ? 0f : c > 1f ? 1f : c;
        }

        /// <summary>Night (22–05): trucks and long-distance buses × 3 relative.</summary>
        public static bool IsNight(float hour)
        {
            float h = ((hour % 24f) + 24f) % 24f;
            return h >= 22f || h < 5f;
        }

        /// <summary>Saturday (the weekly holiday) × 0.6.</summary>
        public const float SaturdayFactor = 0.6f;

        // Spawn mix columns (vehicles research §2.2; per 100 moving vehicles).
        public const int MixTwoWheeler = 0, MixCar = 1, MixMicro = 2, MixBus = 3, MixTempo = 4, MixTruck = 5, MixTractor = 6,
                         MixBicycle = 7, MixRickshaw = 8, MixOther = 9, MixColumns = 10;

        private static readonly float[] MixTrunk = { 55, 22, 7, 4, 1, 7, 1, 2, 0, 1 };
        private static readonly float[] MixPrimary = { 62, 20, 7, 2, 3, 2, 0, 3, 0, 1 };
        private static readonly float[] MixSecondary = { 65, 19, 5, 1, 3, 2, 1, 3, 0, 1 };
        private static readonly float[] MixResidential = { 72, 15, 1, 0, 0, 2, 2, 6, 0, 2 };
        private static readonly float[] MixOldCore = { 70, 3, 0, 0, 0, 0, 0, 15, 10, 2 };
        private static readonly float[] MixPeri = { 60, 12, 6, 3, 0, 6, 7, 6, 0, 0 };

        /// <summary>The spawn mix row for a lane (road class, area type).</summary>
        public static float[] Mix(RoadClass c, AreaType area)
        {
            if (area == AreaType.OldCore && c >= RoadClass.Tertiary) return MixOldCore;
            if (area == AreaType.PeriUrban || area == AreaType.Rural || area == AreaType.Hill) return MixPeri;
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk:
                    return MixTrunk;
                case RoadClass.Primary: return MixPrimary;
                case RoadClass.Secondary:
                case RoadClass.Tertiary:
                    return MixSecondary;
                default: return MixResidential;
            }
        }

        /// <summary>
        /// A catalogue variant for a spawn on a lane: the mix column by weight, then the asset within it. Two-wheelers
        /// split 35 : 55 : 3 : 7 scooter : commuter : cruiser : e-scooter (e-scooters 8% in the old core); cars by the
        /// URBAN-primary shares (taxi 4, hatchback 8, EV 3, SUV 3.5, pickup 1.5); buses green city bus 0.5, minibus 1.0,
        /// coach 0.25, school bus 0.25 (school runs only); trucks painted 1.0, tipper 0.4, tanker 0.6 (×2 on residential
        /// mornings); others police jeep and ambulance. Trucks and coaches ×3 at night. Returns -1 when nothing fits.
        /// </summary>
        public static int PickVariant(ref SimRng rng, RoadClass c, AreaType area, float hour, uint classMask)
        {
            float[] mix = Mix(c, area);
            bool night = IsNight(hour);
            float total = 0f;
            for (int k = 0; k < MixColumns; k++) total += ColumnWeight(mix, k, night, classMask);
            if (total <= 0f) return -1;
            float u = rng.NextFloat() * total;
            int col = MixColumns - 1;
            for (int k = 0; k < MixColumns; k++)
            {
                u -= ColumnWeight(mix, k, night, classMask);
                if (u < 0f)
                {
                    col = k;
                    break;
                }
            }
            return VariantIn(ref rng, col, c, area, hour, classMask);
        }

        private static float ColumnWeight(float[] mix, int col, bool night, uint mask)
        {
            float w = mix[col];
            if (w <= 0f) return 0f;
            if (night && (col == MixTruck || col == MixBus)) w *= 3f;
            if ((mask & ColumnClasses(col)) == 0) return 0f;
            return w;
        }

        /// <summary>The classes a mix column can spawn.</summary>
        public static uint ColumnClasses(int col)
        {
            switch (col)
            {
                case MixTwoWheeler: return VehicleClasses.Bit(VehicleClass.TwoWheeler);
                case MixCar:
                    return VehicleClasses.Bit(VehicleClass.Car) | VehicleClasses.Bit(VehicleClass.Taxi) | VehicleClasses.Bit(VehicleClass.Suv);
                case MixMicro: return VehicleClasses.Bit(VehicleClass.Microbus);
                case MixBus: return VehicleClasses.Bit(VehicleClass.Bus) | VehicleClasses.Bit(VehicleClass.Minibus);
                case MixTempo: return VehicleClasses.Bit(VehicleClass.Tempo);
                case MixTruck: return VehicleClasses.Bit(VehicleClass.Truck) | VehicleClasses.Bit(VehicleClass.Tanker);
                case MixTractor: return VehicleClasses.Bit(VehicleClass.Tractor);
                case MixBicycle: return VehicleClasses.Bit(VehicleClass.Bicycle);
                case MixRickshaw: return VehicleClasses.Bit(VehicleClass.Rickshaw);
                default: return VehicleClasses.Bit(VehicleClass.Service);
            }
        }

        private static int VariantIn(ref SimRng rng, int col, RoadClass c, AreaType area, float hour, uint mask)
        {
            float u = rng.NextFloat();
            switch (col)
            {
                case MixTwoWheeler:
                {
                    float e = area == AreaType.OldCore ? 0.08f : 0.07f;
                    float rest = 1f - e;
                    if (u < 0.35f * rest / 0.93f) return VehicleCatalog.Scooter;
                    if (u < 0.90f * rest / 0.93f) return VehicleCatalog.MotorbikeCommuter;
                    if (u < rest) return VehicleCatalog.MotorbikeCruiser;
                    return VehicleCatalog.EScooter;
                }
                case MixCar:
                {
                    bool suvOk = (mask & VehicleClasses.Bit(VehicleClass.Suv)) != 0;
                    bool taxiOk = (mask & VehicleClasses.Bit(VehicleClass.Taxi)) != 0;
                    float taxi = taxiOk ? 4f : 0f, hatch = 8f, ev = 3f, suv = suvOk ? 3.5f : 0f, pickup = suvOk ? 1.5f : 0f;
                    if ((mask & VehicleClasses.Bit(VehicleClass.Car)) == 0) hatch = ev = pickup = 0f;
                    float t = (taxi + hatch + ev + suv + pickup) * u;
                    if ((t -= taxi) < 0f) return VehicleCatalog.Taxi;
                    if ((t -= hatch) < 0f) return VehicleCatalog.Hatchback;
                    if ((t -= ev) < 0f) return VehicleCatalog.Ev;
                    if ((t -= suv) < 0f) return VehicleCatalog.Suv;
                    return pickup > 0f ? VehicleCatalog.Pickup : taxiOk ? VehicleCatalog.Taxi : VehicleCatalog.Hatchback;
                }
                case MixMicro: return VehicleCatalog.Microbus;
                case MixBus:
                {
                    float h = ((hour % 24f) + 24f) % 24f;
                    bool school = h >= 6.5f && h < 9f || h >= 14.5f && h < 17f;
                    bool busOk = (mask & VehicleClasses.Bit(VehicleClass.Bus)) != 0;
                    bool miniOk = (mask & VehicleClasses.Bit(VehicleClass.Minibus)) != 0;
                    float green = busOk ? 0.5f : 0f, mini = miniOk ? 1.0f : 0f, coach = busOk ? 0.25f * (IsNight(hour) ? 3f : 1f) : 0f;
                    float sch = busOk && school ? 0.25f : 0f;
                    float t = (green + mini + coach + sch) * u;
                    if ((t -= green) < 0f) return VehicleCatalog.BusCityGreen;
                    if ((t -= mini) < 0f) return VehicleCatalog.Minibus;
                    if ((t -= coach) < 0f) return VehicleCatalog.Coach;
                    return sch > 0f ? VehicleCatalog.SchoolBus : miniOk ? VehicleCatalog.Minibus : VehicleCatalog.BusCityGreen;
                }
                case MixTempo: return VehicleCatalog.Tempo;
                case MixTruck:
                {
                    bool truckOk = (mask & VehicleClasses.Bit(VehicleClass.Truck)) != 0;
                    bool tankOk = (mask & VehicleClasses.Bit(VehicleClass.Tanker)) != 0;
                    float h = ((hour % 24f) + 24f) % 24f;
                    bool morningRes = c >= RoadClass.Unclassified && h >= 5f && h < 10f;
                    float painted = truckOk ? 1.0f : 0f, tipper = truckOk ? 0.4f : 0f, tanker = tankOk ? 0.6f * (morningRes ? 2f : 1f) : 0f;
                    float t = (painted + tipper + tanker) * u;
                    if ((t -= painted) < 0f) return VehicleCatalog.TruckPainted;
                    if ((t -= tipper) < 0f) return VehicleCatalog.Tipper;
                    return tankOk ? VehicleCatalog.Tanker : VehicleCatalog.TruckPainted;
                }
                case MixTractor: return VehicleCatalog.Tractor;
                case MixBicycle: return VehicleCatalog.Bicycle;
                case MixRickshaw: return VehicleCatalog.Rickshaw;
                default: return u < 0.5f ? VehicleCatalog.PoliceJeep : VehicleCatalog.Ambulance;
            }
        }

        private static readonly float[] ClassHalfWidth = BuildHalfWidths();
        private static readonly float[] ClassLength = BuildLengths();

        private static float[] BuildHalfWidths()
        {
            var h = new float[VehicleClasses.Count];
            foreach (VehicleCatalogEntry e in VehicleCatalog.All)
            {
                int c = (int)e.TrafficClass;
                h[c] = Math.Max(h[c], 0.5f * e.WidthM);
            }
            return h;
        }

        private static float[] BuildLengths()
        {
            var l = new float[VehicleClasses.Count];
            foreach (VehicleCatalogEntry e in VehicleCatalog.All)
            {
                int c = (int)e.TrafficClass;
                l[c] = Math.Max(l[c], e.LengthM);
            }
            return l;
        }

        /// <summary>Half the widest body of a class (catalogue).</summary>
        public static float HalfWidthOf(VehicleClass c)
        {
            return ClassHalfWidth[(int)c];
        }

        /// <summary>The longest body of a class (catalogue).</summary>
        public static float LengthOf(VehicleClass c)
        {
            return ClassLength[(int)c];
        }

        /// <summary>The classes a travel mask admits (W2_DESIGN 4.4 by final game width): Motorbike → two-wheelers;
        /// Bicycle → bicycles and rickshaws; Car → cars, taxis and service cars; Jeep → SUVs, micros, tempos and
        /// tractors; Bus → buses, minibuses, trucks and tankers.</summary>
        public static uint ClassesFor(Travel t)
        {
            uint m = 0;
            if ((t & Travel.Motorbike) != 0) m |= VehicleClasses.Bit(VehicleClass.TwoWheeler);
            if ((t & Travel.Bicycle) != 0) m |= VehicleClasses.Bit(VehicleClass.Bicycle) | VehicleClasses.Bit(VehicleClass.Rickshaw);
            if ((t & Travel.Car) != 0) m |= VehicleClasses.Bit(VehicleClass.Car) | VehicleClasses.Bit(VehicleClass.Taxi) | VehicleClasses.Bit(VehicleClass.Service);
            if ((t & Travel.Jeep) != 0)
                m |= VehicleClasses.Bit(VehicleClass.Suv) | VehicleClasses.Bit(VehicleClass.Microbus) | VehicleClasses.Bit(VehicleClass.Tempo) |
                     VehicleClasses.Bit(VehicleClass.Tractor);
            if ((t & Travel.Bus) != 0)
                m |= VehicleClasses.Bit(VehicleClass.Bus) | VehicleClasses.Bit(VehicleClass.Minibus) | VehicleClasses.Bit(VehicleClass.Truck) |
                     VehicleClasses.Bit(VehicleClass.Tanker);
            return m;
        }
    }
}
