using System;
using System.Collections.Generic;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Driving
{
    /// <summary>The procedural engine voice of a vehicle (audio research §4.2 preset table; Track C maps each value to
    /// its <c>EnginePreset</c>). Append-only.</summary>
    public enum EngineSound : byte
    {
        None = 0,
        MotoCommuter = 1,
        MotoSport = 2,
        MotoCruiser = 3,
        Scooter = 4,
        Moto2Stroke = 5,
        Car3Cyl = 6,
        Car4Cyl = 7,
        SuvDiesel = 8,
        Microbus = 9,
        BusCity = 10,
        Truck = 11,
        Tractor = 12,
        EScooter = 13,
        TempoSafa = 14,
        CarEv = 15,
        BusEv = 16,
    }

    /// <summary>Who owns a vehicle, for its plate colour and class letter (W2_DESIGN 5.2).</summary>
    public enum PlateOwnership : byte
    {
        Private = 0,
        Public = 1,
        Government = 2,
        Corporation = 3,
        Tourist = 4,
    }

    /// <summary>Plate size class: heavy (क / ख / ग), light (च / ज / झ), two-wheeler (प / फ / ब).</summary>
    public enum PlateSize : byte
    {
        Heavy = 0,
        Light = 1,
        TwoWheeler = 2,
    }

    /// <summary>The body shape family the procedural mesher builds (<see cref="VehicleMesher"/>).</summary>
    public enum BodyShape : byte
    {
        Scooter = 0,
        Motorbike = 1,
        Cruiser = 2,
        Bicycle = 3,
        Rickshaw = 4,
        Tempo = 5,
        Van = 6,
        Bus = 7,
        Hatchback = 8,
        Suv = 9,
        Pickup = 10,
        Truck = 11,
        Tipper = 12,
        Tanker = 13,
        Tractor = 14,
    }

    /// <summary>
    /// One livery: body, accent (stripes, bands, cab), roof and trim colours as 0xRRGGBB, plus the route-board or sign
    /// colour where the body has one. Generic only: no operator's livery is reproduced (W2_DESIGN 5.2 hard rule).
    /// </summary>
    public readonly struct VehicleLivery
    {
        public readonly uint Body, Accent, Roof, Trim, Sign;

        public VehicleLivery(uint body, uint accent, uint roof, uint trim, uint sign)
        {
            Body = body;
            Accent = accent;
            Roof = roof;
            Trim = trim;
            Sign = sign;
        }
    }

    /// <summary>
    /// One catalogue asset (W2_DESIGN 5.2 and 10.3): the asset id, its traffic class, the handling kind and preset the
    /// player drives it with, the engine voice, its liveries and plate class, and its body. The mapping is fixed here:
    /// tanker → DriveKind Truck (preset Tanker); pickup, hatchback and EV → Car; e-scooter → Scooter; tourist and school
    /// bus → Bus; police jeep → Suv; ambulance → Microbus; rickshaw → Bicycle (the player rides only as a passenger).
    /// </summary>
    public readonly struct VehicleCatalogEntry
    {
        public readonly string AssetId;

        /// <summary>Preset of a shared model ("city_green" and "minibus" of <c>ghm_veh_bus_city_a</c>), else empty.</summary>
        public readonly string Variant;

        public readonly VehicleClass TrafficClass;
        public readonly VehicleKind DriveKind;
        public readonly byte HandlingPreset, EnginePreset, LiveryCount;
        public readonly bool PlayerDrivable;

        public readonly BodyShape Shape;
        public readonly float LengthM, WidthM, HeightM, WheelbaseM, WheelDiaM, RearOverhangM;
        public readonly int Seats;
        public readonly PlateOwnership Plate;
        public readonly PlateSize PlateSize;

        /// <summary>Liveries; their weights (same length, summing to about 1) give the street shares.</summary>
        public readonly VehicleLivery[] Liveries;

        public readonly float[] LiveryWeights;

        public VehicleCatalogEntry(string assetId, string variant, VehicleClass cls, VehicleKind kind, HandlingPreset preset, EngineSound engine,
                                   bool drivable, BodyShape shape, float length, float width, float height, float wheelbase, float wheelDia,
                                   int seats, PlateOwnership plate, PlateSize plateSize, VehicleLivery[] liveries, float[] weights)
        {
            AssetId = assetId;
            Variant = variant ?? "";
            TrafficClass = cls;
            DriveKind = kind;
            HandlingPreset = (byte)preset;
            EnginePreset = (byte)engine;
            PlayerDrivable = drivable;
            Shape = shape;
            LengthM = length;
            WidthM = width;
            HeightM = height;
            WheelbaseM = wheelbase;
            WheelDiaM = wheelDia;
            Seats = seats;
            Plate = plate;
            PlateSize = plateSize;
            Liveries = liveries;
            LiveryWeights = weights;
            LiveryCount = (byte)liveries.Length;
            RearOverhangM = VehicleSpec.For(preset).RearOverhangM;
        }

        public HandlingPreset Handling
        {
            get { return (HandlingPreset)HandlingPreset; }
        }

        public EngineSound Engine
        {
            get { return (EngineSound)EnginePreset; }
        }

        /// <summary>A fresh handling spec for this asset (the preset with the catalogue body).</summary>
        public VehicleSpec Spec()
        {
            VehicleSpec s = VehicleSpec.For(Handling);
            s.LengthM = LengthM;
            s.WidthM = WidthM;
            s.HeightM = HeightM;
            s.SeatSockets = Seats;
            s.Validate();
            return s;
        }

        /// <summary>The livery at <paramref name="index"/> (wrapped).</summary>
        public VehicleLivery LiveryAt(int index)
        {
            int n = Liveries.Length;
            return Liveries[((index % n) + n) % n];
        }

        /// <summary>A livery index drawn by the street weights from a seed (deterministic per agent).</summary>
        public byte PickLivery(uint seed)
        {
            float u = (Mix(seed) >> 8) * (1f / 16777216f);
            float acc = 0f;
            for (int i = 0; i < LiveryWeights.Length; i++)
            {
                acc += LiveryWeights[i];
                if (u < acc) return (byte)i;
            }
            return (byte)(LiveryWeights.Length - 1);
        }

        internal static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7FEB352D;
                x ^= x >> 15;
                x *= 0x846CA68B;
                x ^= x >> 16;
                return x;
            }
        }
    }

    /// <summary>
    /// The one table that maps every W2_DESIGN 5.2 vehicle asset to its traffic class, handling kind, engine voice and
    /// liveries, so the traffic sim, the player's handling, the presenters and audio agree; nobody else switches on
    /// asset-id strings. <see cref="Traffic.AgentPose.Variant"/> indexes <see cref="All"/>. Append-only.
    /// </summary>
    public static class VehicleCatalog
    {
        private static readonly VehicleCatalogEntry[] Entries = Build();
        private static readonly IReadOnlyList<VehicleCatalogEntry> ReadOnly = Array.AsReadOnly(Entries);

        public static IReadOnlyList<VehicleCatalogEntry> All
        {
            get { return ReadOnly; }
        }

        public static int Count
        {
            get { return Entries.Length; }
        }

        public static VehicleCatalogEntry At(int variant)
        {
            return Entries[variant];
        }

        /// <summary>The first entry with this asset id (the <c>city_green</c> preset for <c>ghm_veh_bus_city_a</c>).</summary>
        public static bool TryGet(string assetId, out VehicleCatalogEntry e)
        {
            int i = IndexOf(assetId, null);
            e = i >= 0 ? Entries[i] : default(VehicleCatalogEntry);
            return i >= 0;
        }

        /// <summary>Index of the entry with this asset id and variant (null or empty variant: the first entry of the id),
        /// or -1.</summary>
        public static int IndexOf(string assetId, string variant)
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                if (!string.Equals(Entries[i].AssetId, assetId, StringComparison.Ordinal)) continue;
                if (string.IsNullOrEmpty(variant) || string.Equals(Entries[i].Variant, variant, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        // Fixed variant indices used by the spawners (must match Build's order; the tests check them).
        public const int Scooter = 0, MotorbikeCommuter = 1, MotorbikeCruiser = 2, EScooter = 3, Bicycle = 4, Rickshaw = 5,
                         Tempo = 6, Microbus = 7, BusCityGreen = 8, Minibus = 9, Coach = 10, SchoolBus = 11, Taxi = 12,
                         Hatchback = 13, Ev = 14, Suv = 15, Pickup = 16, TruckPainted = 17, Tipper = 18, Tanker = 19,
                         Tractor = 20, PoliceJeep = 21, Ambulance = 22;

        private static VehicleLivery L(uint body, uint accent = 0x212121, uint roof = 0, uint trim = 0x263238, uint sign = 0)
        {
            return new VehicleLivery(body, accent, roof == 0 ? body : roof, trim, sign);
        }

        private static VehicleLivery[] Each(uint accent, params uint[] bodies)
        {
            var l = new VehicleLivery[bodies.Length];
            for (int i = 0; i < bodies.Length; i++) l[i] = L(bodies[i], accent);
            return l;
        }

        private static float[] Even(int n)
        {
            var w = new float[n];
            for (int i = 0; i < n; i++) w[i] = 1f / n;
            return w;
        }

        private static VehicleCatalogEntry[] Build()
        {
            var e = new List<VehicleCatalogEntry>();
            VehicleLivery[] lv;

            lv = Each(0x212121, 0xE53935, 0x1E88E5, 0xFDD835, 0xFAFAFA, 0x212121, 0x8E24AA, 0x43A047, 0xFB8C00);
            e.Add(new VehicleCatalogEntry("ghm_veh_scooter_a", "", VehicleClass.TwoWheeler, VehicleKind.Scooter, HandlingPreset.Scooter,
                EngineSound.Scooter, true, BodyShape.Scooter, 1.81f, 0.72f, 1.15f, 1.26f, 0.48f, 2, PlateOwnership.Private,
                PlateSize.TwoWheeler, lv, Even(lv.Length)));

            lv = Each(0x9E9E9E, 0x212121, 0xC62828, 0x1565C0, 0x9E9E9E, 0xFAFAFA, 0x2E7D32);
            e.Add(new VehicleCatalogEntry("ghm_veh_motorbike_commuter_a", "", VehicleClass.TwoWheeler, VehicleKind.Motorbike,
                HandlingPreset.Motorbike, EngineSound.MotoCommuter, true, BodyShape.Motorbike, 2.05f, 0.77f, 1.08f, 1.32f, 0.62f, 2,
                PlateOwnership.Private, PlateSize.TwoWheeler, lv, new[] { 0.35f, 0.13f, 0.13f, 0.13f, 0.13f, 0.13f }));

            lv = new[] { L(0x212121, 0xCFD8DC), L(0x3E2723, 0xCFD8DC), L(0x455A64, 0xCFD8DC), L(0xCFD8DC, 0x212121) };
            e.Add(new VehicleCatalogEntry("ghm_veh_motorbike_cruiser_a", "", VehicleClass.TwoWheeler, VehicleKind.Cruiser,
                HandlingPreset.Cruiser, EngineSound.MotoCruiser, true, BodyShape.Cruiser, 2.15f, 0.80f, 1.09f, 1.39f, 0.66f, 2,
                PlateOwnership.Private, PlateSize.TwoWheeler, lv, Even(lv.Length)));

            lv = Each(0x37474F, 0x80DEEA, 0xF8BBD0, 0xFFF59D, 0xFAFAFA);
            e.Add(new VehicleCatalogEntry("ghm_veh_escooter_a", "", VehicleClass.TwoWheeler, VehicleKind.Scooter, HandlingPreset.EScooter,
                EngineSound.EScooter, true, BodyShape.Scooter, 1.80f, 0.70f, 1.10f, 1.30f, 0.45f, 2, PlateOwnership.Private,
                PlateSize.TwoWheeler, lv, Even(lv.Length)));

            lv = Each(0x9E9E9E, 0x212121, 0x1565C0, 0xC62828, 0x2E7D32);
            e.Add(new VehicleCatalogEntry("ghm_veh_bicycle_a", "", VehicleClass.Bicycle, VehicleKind.Bicycle, HandlingPreset.Bicycle,
                EngineSound.None, true, BodyShape.Bicycle, 1.80f, 0.60f, 1.05f, 1.12f, 0.71f, 1, PlateOwnership.Private,
                PlateSize.TwoWheeler, lv, Even(lv.Length)));

            lv = new[] { L(0x1565C0, 0x212121), L(0xC62828, 0xFBC02D), L(0x2E7D32, 0x212121) };
            e.Add(new VehicleCatalogEntry("ghm_veh_rickshaw_cycle_a", "", VehicleClass.Rickshaw, VehicleKind.Bicycle, HandlingPreset.Bicycle,
                EngineSound.None, false, BodyShape.Rickshaw, 2.60f, 1.10f, 1.90f, 1.55f, 0.66f, 3, PlateOwnership.Public,
                PlateSize.TwoWheeler, lv, Even(lv.Length)));

            lv = new[] { L(0xF4F4F0, 0x2E8B57, 0xF4F4F0, 0x263238, 0xFFD54F) };
            e.Add(new VehicleCatalogEntry("ghm_veh_tempo_safa_a", "", VehicleClass.Tempo, VehicleKind.Tempo, HandlingPreset.Tempo,
                EngineSound.TempoSafa, true, BodyShape.Tempo, 3.60f, 1.45f, 1.90f, 2.10f, 0.60f, 12, PlateOwnership.Public,
                PlateSize.Light, lv, Even(1)));

            lv = new[]
            {
                L(0xF5F5F5, 0x1565C0, 0xF5F5F5, 0x263238, 0xFFF3E0), L(0xF5F5F5, 0xC62828, 0xF5F5F5, 0x263238, 0xFFF3E0),
                L(0xF5F5F5, 0x2E7D32, 0xF5F5F5, 0x263238, 0xFFF3E0), L(0xF5F5F5, 0xF9A825, 0xF5F5F5, 0x263238, 0xFFF3E0),
            };
            e.Add(new VehicleCatalogEntry("ghm_veh_microvan_a", "", VehicleClass.Microbus, VehicleKind.Microbus, HandlingPreset.Microbus,
                EngineSound.Microbus, true, BodyShape.Van, 4.70f, 1.70f, 1.98f, 2.57f, 0.66f, 15, PlateOwnership.Public,
                PlateSize.Light, lv, Even(lv.Length)));

            lv = new[] { L(0x2E7D32, 0x66BB6A, 0xFFFFFF, 0x263238, 0xFDD835) };
            e.Add(new VehicleCatalogEntry("ghm_veh_bus_city_a", "city_green", VehicleClass.Bus, VehicleKind.Bus, HandlingPreset.Bus,
                EngineSound.BusCity, true, BodyShape.Bus, 10.5f, 2.50f, 3.20f, 5.4f, 0.95f, 36, PlateOwnership.Public,
                PlateSize.Heavy, lv, Even(1)));

            lv = new[]
            {
                L(0xFFF8E1, 0xC62828, 0xFFF8E1, 0x263238, 0xFDD835), L(0xFFF8E1, 0x1565C0, 0xFFF8E1, 0x263238, 0xFDD835),
                L(0xFFF8E1, 0xEF6C00, 0xFFF8E1, 0x263238, 0xFDD835),
            };
            e.Add(new VehicleCatalogEntry("ghm_veh_bus_city_a", "minibus", VehicleClass.Minibus, VehicleKind.Minibus, HandlingPreset.Minibus,
                EngineSound.BusCity, true, BodyShape.Bus, 7.2f, 2.10f, 2.85f, 3.8f, 0.85f, 28, PlateOwnership.Public,
                PlateSize.Heavy, lv, Even(lv.Length)));

            lv = new[]
            {
                L(0xFAFAFA, 0xD32F2F), L(0xFAFAFA, 0x1976D2), L(0xFFF8E1, 0xFBC02D), L(0xFFF8E1, 0x388E3C),
            };
            e.Add(new VehicleCatalogEntry("ghm_veh_bus_tourist_a", "", VehicleClass.Bus, VehicleKind.Bus, HandlingPreset.Coach,
                EngineSound.BusCity, true, BodyShape.Bus, 11.0f, 2.50f, 3.35f, 5.6f, 1.00f, 44, PlateOwnership.Tourist,
                PlateSize.Heavy, lv, Even(lv.Length)));

            lv = new[] { L(0xFBC02D, 0x212121, 0xFBC02D, 0x212121, 0xFFFFFF) };
            e.Add(new VehicleCatalogEntry("ghm_veh_bus_school_a", "", VehicleClass.Bus, VehicleKind.Bus, HandlingPreset.SchoolBus,
                EngineSound.BusCity, false, BodyShape.Bus, 8.0f, 2.30f, 3.00f, 4.2f, 0.90f, 36, PlateOwnership.Private,
                PlateSize.Heavy, lv, Even(1)));

            lv = new[] { L(0xF5F5F5, 0x212121, 0xF5F5F5, 0x263238, 0xFFEB3B), L(0xFBC02D, 0x212121, 0xFBC02D, 0x263238, 0xFFEB3B) };
            e.Add(new VehicleCatalogEntry("ghm_veh_taxi_small_a", "", VehicleClass.Taxi, VehicleKind.Taxi, HandlingPreset.Taxi,
                EngineSound.Car3Cyl, true, BodyShape.Hatchback, 3.40f, 1.48f, 1.48f, 2.36f, 0.55f, 4, PlateOwnership.Public,
                PlateSize.Light, lv, new[] { 0.7f, 0.3f }));

            lv = Each(0x263238, 0xFAFAFA, 0xBDBDBD, 0xC62828, 0x616161, 0x1565C0, 0x212121, 0xEF6C00, 0x00897B);
            e.Add(new VehicleCatalogEntry("ghm_veh_hatchback_a", "", VehicleClass.Car, VehicleKind.Car, HandlingPreset.Hatchback,
                EngineSound.Car4Cyl, true, BodyShape.Hatchback, 3.77f, 1.68f, 1.52f, 2.43f, 0.58f, 5, PlateOwnership.Private,
                PlateSize.Light, lv, new[] { 0.30f, 0.20f, 0.15f, 0.10f, 0.10f, 0.05f, 0.05f, 0.05f }));

            lv = Each(0x263238, 0xFAFAFA, 0x9E9E9E, 0x1E88E5, 0x00897B);
            e.Add(new VehicleCatalogEntry("ghm_veh_ev_compact_a", "", VehicleClass.Car, VehicleKind.Car, HandlingPreset.Ev,
                EngineSound.CarEv, true, BodyShape.Suv, 4.10f, 1.75f, 1.58f, 2.55f, 0.66f, 5, PlateOwnership.Private,
                PlateSize.Light, lv, Even(lv.Length)));

            lv = Each(0x263238, 0xFAFAFA, 0x212121, 0xBDBDBD, 0x6D1B1B);
            e.Add(new VehicleCatalogEntry("ghm_veh_suv_a", "", VehicleClass.Suv, VehicleKind.Suv, HandlingPreset.Suv,
                EngineSound.SuvDiesel, true, BodyShape.Suv, 4.46f, 1.82f, 1.98f, 2.68f, 0.75f, 7, PlateOwnership.Private,
                PlateSize.Light, lv, new[] { 0.40f, 0.20f, 0.20f, 0.20f }));

            lv = Each(0x263238, 0xFAFAFA, 0xC62828, 0x1565C0);
            e.Add(new VehicleCatalogEntry("ghm_veh_pickup_a", "", VehicleClass.Car, VehicleKind.Car, HandlingPreset.Pickup,
                EngineSound.SuvDiesel, true, BodyShape.Pickup, 4.86f, 1.70f, 1.86f, 3.01f, 0.70f, 2, PlateOwnership.Private,
                PlateSize.Light, lv, Even(lv.Length)));

            lv = new[]
            {
                L(0xD32F2F, 0xFF8F00), L(0x1976D2, 0x00897B), L(0xFBC02D, 0xFF8F00), L(0x388E3C, 0x00897B),
            };
            e.Add(new VehicleCatalogEntry("ghm_veh_truck_painted_a", "", VehicleClass.Truck, VehicleKind.Truck, HandlingPreset.Truck,
                EngineSound.Truck, true, BodyShape.Truck, 8.10f, 2.45f, 3.30f, 4.80f, 1.05f, 3, PlateOwnership.Public,
                PlateSize.Heavy, lv, Even(lv.Length)));

            lv = new[] { L(0xFBC02D, 0xF57F17), L(0xD32F2F, 0xF57F17) };
            e.Add(new VehicleCatalogEntry("ghm_veh_truck_plain_a", "", VehicleClass.Truck, VehicleKind.Truck, HandlingPreset.Tipper,
                EngineSound.Truck, true, BodyShape.Tipper, 7.60f, 2.50f, 3.10f, 4.48f, 1.05f, 2, PlateOwnership.Public,
                PlateSize.Heavy, lv, Even(lv.Length)));

            lv = new[] { L(0x1E88E5, 0xC62828, 0x1E88E5, 0x263238, 0xFFFFFF), L(0xFAFAFA, 0x1976D2, 0xFAFAFA, 0x263238, 0x1E88E5) };
            e.Add(new VehicleCatalogEntry("ghm_veh_tanker_a", "", VehicleClass.Tanker, VehicleKind.Truck, HandlingPreset.Tanker,
                EngineSound.Truck, true, BodyShape.Tanker, 7.50f, 2.40f, 3.00f, 4.20f, 1.00f, 2, PlateOwnership.Public,
                PlateSize.Heavy, lv, Even(lv.Length)));

            lv = new[] { L(0xC62828, 0xFDD835), L(0x1565C0, 0xFDD835), L(0x2E7D32, 0xFDD835) };
            e.Add(new VehicleCatalogEntry("ghm_veh_tractor_a", "", VehicleClass.Tractor, VehicleKind.Tractor, HandlingPreset.Tractor,
                EngineSound.Tractor, true, BodyShape.Tractor, 3.45f, 1.75f, 2.10f, 1.95f, 1.40f, 1, PlateOwnership.Private,
                PlateSize.Heavy, lv, Even(lv.Length)));

            lv = new[] { L(0xFAFAFA, 0x1565C0, 0xFAFAFA, 0x263238, 0x1565C0) };
            e.Add(new VehicleCatalogEntry("ghm_veh_police_jeep_a", "", VehicleClass.Service, VehicleKind.Suv, HandlingPreset.Suv,
                EngineSound.SuvDiesel, false, BodyShape.Suv, 4.40f, 1.75f, 1.90f, 2.68f, 0.70f, 5, PlateOwnership.Government,
                PlateSize.Light, lv, Even(1)));

            lv = new[] { L(0xFFFFFF, 0xD32F2F, 0xFFFFFF, 0x263238, 0xD32F2F) };
            e.Add(new VehicleCatalogEntry("ghm_veh_ambulance_a", "", VehicleClass.Service, VehicleKind.Microbus, HandlingPreset.Microbus,
                EngineSound.Microbus, false, BodyShape.Van, 4.70f, 1.70f, 2.10f, 2.57f, 0.66f, 2, PlateOwnership.Public,
                PlateSize.Light, lv, Even(1)));

            return e.ToArray();
        }
    }
}
