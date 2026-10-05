using System;

namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Sound models for every powered (and pedalled) vehicle (W2_DESIGN 7.3, A §2.2–2.5, §4.2). Append-only. Values
    /// 0–16 equal Track B's <c>Ghumante.Core.Driving.EngineSound</c> (stored in <c>VehicleCatalogEntry.EnginePreset</c>,
    /// §10.3), so a catalog byte casts straight to this type.
    /// </summary>
    public enum EngineModel : byte
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
        /// <summary>Audio-only addition: pedal bicycles and cycle rickshaws (their catalog entries carry
        /// <c>EngineSound.None</c>; see <see cref="EnginePresets.ForCatalog"/>).</summary>
        Bicycle = 17,
    }

    /// <summary>How an <see cref="EngineVoice"/> makes its core tone.</summary>
    public enum EngineFamily : byte
    {
        /// <summary>Additive firing-order synthesis (A §4.2).</summary>
        Combustion = 0,
        /// <summary>Motor whine (sine + 2nd + 3rd), inverter hiss, no fake engine (A §4.2 electric).</summary>
        Electric = 1,
        /// <summary>Bicycle: freewheel ratchet, chain, tyres (A §2.3).</summary>
        Pedal = 2,
    }

    /// <summary>
    /// Parameters of one engine sound (the §10.3 contract fields first, then the A §4.2 table). Values are the
    /// research starting points [E], tuned by ear later. <see cref="Harmonics"/> and <see cref="Gears"/> are
    /// shared by every voice built from a preset: treat them as read-only.
    /// </summary>
    public struct EnginePreset
    {
        // ---- contract (W2_DESIGN 10.3) ----
        public byte Cylinders;
        public bool FourStroke;
        public float IdleRpm, RedRpm;
        /// <summary>Relative amplitude of firing-frequency harmonics 1..N (A §4.2 "A_1..A_8", extended).</summary>
        public float[] Harmonics;

        // ---- A §4.2 table ----
        public EngineModel Model;
        public EngineFamily Family;
        /// <summary>Spectral tilt off load and on load (A §4.2): A_k(load) = A_k · k^(1 − lerp(off, on, load)), so a
        /// smaller value is brighter (on load) and a larger one darker (overrun).</summary>
        public float TiltOff, TiltOn;
        /// <summary>Cycle-to-cycle period and amplitude jitter (fractions).</summary>
        public float JitterPeriod, JitterAmp;
        public float NoiseBase, NoiseLoad;
        public float PipeHz, PipeQ;
        /// <summary>Soft-clip drive at full load (1 = clean).</summary>
        public float Drive;
        /// <summary>Cruiser "dug-dug": each firing cycle is gated with this duty (0 = off).</summary>
        public float ThumpDuty;
        /// <summary>3-cylinder 0.5-order burble: amplitude-modulation depth at half the crank rate (0 = off).</summary>
        public float HalfOrderAm;
        /// <summary>Diesel injection tick level (linear, 0 = none).</summary>
        public float InjectionTick;
        /// <summary>Turbo whistle level (linear, 0 = none), rising 2→4 kHz with rpm and load.</summary>
        public float Turbo;
        /// <summary>CVT belt whine level (linear; 600–1,200 Hz with speed).</summary>
        public float BeltWhine;
        /// <summary>Overrun pops per second at full overrun (2-strokes and bikes).</summary>
        public float PopsPerS;
        /// <summary>Panel / body rattle depth on rough surfaces (8–14 Hz AM).</summary>
        public float Rattle;

        // ---- electric ----
        public float WhineMinHz, WhineMaxHz;
        /// <summary>Lag of the electric whine behind speed (s), e.g. 0.3 for the Safa tempo DC motor.</summary>
        public float WhineLagS;
        /// <summary>Gear-mesh tone as a ratio of the whine (0 = none).</summary>
        public float GearMesh;

        // ---- drivetrain (RPM from speed, A §4.2) ----
        public float[] Gears;
        public float FinalDrive, WheelCircM;
        public float ShiftUp, ShiftDown, ShiftS;
        /// <summary>CVT: rpm jumps to <see cref="CvtHoldRpm"/> under throttle and holds while speed rises.</summary>
        public bool Cvt;
        public float CvtHoldRpm;
        /// <summary>Top speed used to normalise electric whine and tyre levels.</summary>
        public float MaxSpeedKmh;

        // ---- mix ----
        /// <summary>Output level of the voice relative to full scale (dB) before the caller's gain.</summary>
        public float LevelDb;
        /// <summary>Tyre noise level (linear) at 15 m/s.</summary>
        public float TyreLevel;

        /// <summary>Firing frequency at <paramref name="rpm"/>: rpm / 60 × cylinders / (2 for 4-stroke).</summary>
        public float FiringHz(float rpm)
        {
            float c = Cylinders < 1 ? 1 : Cylinders;
            return rpm / 60f * c / (FourStroke ? 2f : 1f);
        }
    }

    /// <summary>
    /// The A §4.2 preset table plus the electric and pedal models, and the mapping from the §10.3 vehicle enums
    /// (by byte value, so this assembly does not depend on Track B's enum additions).
    /// </summary>
    public static class EnginePresets
    {
        private static readonly EnginePreset[] Table = Build();

        public static int Count
        {
            get { return Table.Length; }
        }

        /// <summary>The preset of <paramref name="model"/> (<see cref="EngineModel.None"/> for unknown values).</summary>
        public static EnginePreset Get(EngineModel model)
        {
            int i = (int)model;
            if (i < 0 || i >= Table.Length) i = 0;
            return Table[i];
        }

        /// <summary>
        /// Sound model of a <c>VehicleCatalogEntry</c>: its <c>EnginePreset</c> byte (Track B's <c>EngineSound</c>,
        /// numbered like <see cref="EngineModel"/>) and, for entries without an engine, its <c>VehicleKind</c> byte
        /// (Bicycle 3 → the pedal model; anything else → None).
        /// </summary>
        public static EngineModel ForCatalog(byte engineSound, byte vehicleKind)
        {
            if (engineSound == 0) return vehicleKind == 3 ? EngineModel.Bicycle : EngineModel.None;
            return engineSound < Table.Length ? (EngineModel)engineSound : EngineModel.None;
        }

        /// <summary>
        /// Sound model for a player-drivable vehicle kind (<c>Ghumante.Core.Driving.VehicleKind</c> byte values,
        /// §10.3: Motorbike 0, Taxi 1, Walker 2, Bicycle 3, Scooter 4, Car 5, Suv 6, Microbus 7, Tempo 8, Minibus 9,
        /// Bus 10, Truck 11, Tractor 12, Cruiser 13). <paramref name="seed"/> picks the rare variants
        /// (2-stroke bikes, e-scooters, EV cars and buses) deterministically.
        /// </summary>
        public static EngineModel ForVehicleKind(byte kind, uint seed)
        {
            float r = (Dsp.Mix(seed, 0x4B494E44u) >> 8) * (1f / 16777216f);
            switch (kind)
            {
                case 0: return r < 0.02f ? EngineModel.Moto2Stroke : r < 0.16f ? EngineModel.MotoSport : EngineModel.MotoCommuter;
                case 1: return EngineModel.Car3Cyl;
                case 2: return EngineModel.None;
                case 3: return EngineModel.Bicycle;
                case 4: return r < 0.08f ? EngineModel.EScooter : EngineModel.Scooter;
                case 5: return r < 0.15f ? EngineModel.CarEv : EngineModel.Car4Cyl;
                case 6: return EngineModel.SuvDiesel;
                case 7: return EngineModel.Microbus;
                case 8: return EngineModel.TempoSafa;
                case 9: return EngineModel.Microbus;
                case 10: return r < 0.1f ? EngineModel.BusEv : EngineModel.BusCity;
                case 11: return EngineModel.Truck;
                case 12: return EngineModel.Tractor;
                case 13: return EngineModel.MotoCruiser;
                default: return EngineModel.Car4Cyl;
            }
        }

        /// <summary>
        /// Sound model for a traffic agent (<c>Ghumante.Core.Traffic.VehicleClass</c> byte values, §10.3:
        /// TwoWheeler 0, Bicycle 1, Rickshaw 2, Car 3, Taxi 4, Suv 5, Microbus 6, Tempo 7, Minibus 8, Bus 9, Truck 10,
        /// Tanker 11, Tractor 12, Service 13). Two-wheelers split by seed (A §2.2): commuter 50%, scooter 28%,
        /// sport 10%, cruiser 6%, e-scooter 4%, 2-stroke 2%. Seed with <c>AgentPose.AgentId</c> so an agent
        /// keeps its sound for life.
        /// </summary>
        public static EngineModel ForTrafficClass(byte vehicleClass, uint seed)
        {
            float r = (Dsp.Mix(seed, 0x434C5353u) >> 8) * (1f / 16777216f);
            switch (vehicleClass)
            {
                case 0:
                    if (r < 0.50f) return EngineModel.MotoCommuter;
                    if (r < 0.78f) return EngineModel.Scooter;
                    if (r < 0.88f) return EngineModel.MotoSport;
                    if (r < 0.94f) return EngineModel.MotoCruiser;
                    if (r < 0.98f) return EngineModel.EScooter;
                    return EngineModel.Moto2Stroke;
                case 1:
                case 2: return EngineModel.Bicycle;
                case 3: return r < 0.15f ? EngineModel.CarEv : EngineModel.Car4Cyl;
                case 4: return EngineModel.Car3Cyl;
                case 5: return EngineModel.SuvDiesel;
                case 6: return EngineModel.Microbus;
                case 7: return EngineModel.TempoSafa;
                case 8: return EngineModel.Microbus;
                case 9: return r < 0.1f ? EngineModel.BusEv : EngineModel.BusCity;
                case 10:
                case 11: return EngineModel.Truck;
                case 12: return EngineModel.Tractor;
                case 13: return EngineModel.SuvDiesel;
                default: return EngineModel.Car4Cyl;
            }
        }

        /// <summary>The horn a vehicle of this sound model carries (A §2.6).</summary>
        public static HornKind HornFor(EngineModel m)
        {
            switch (m)
            {
                case EngineModel.MotoCommuter:
                case EngineModel.MotoSport:
                case EngineModel.MotoCruiser:
                case EngineModel.Moto2Stroke:
                    return HornKind.Moto;
                case EngineModel.Scooter:
                case EngineModel.EScooter:
                    return HornKind.Scooter;
                case EngineModel.TempoSafa:
                    return HornKind.Tempo;
                case EngineModel.BusCity:
                case EngineModel.BusEv:
                case EngineModel.Truck:
                    return HornKind.Bus;
                case EngineModel.Bicycle:
                    return HornKind.BicycleBell;
                case EngineModel.None:
                    return HornKind.None;
                default:
                    return HornKind.Car;
            }
        }

        /// <summary>
        /// How long to hold the horn for a traffic honk reason (<c>Ghumante.Core.Traffic.HornKind</c> byte values:
        /// Tap 0, Bend 1, Overtake 2, Blocked 3, GreenNotMoving 4). Friendly taps (A §2.6, the 2017 no-horn rule):
        /// overtaking is a double tap (returned as 0), only "blocked" gets a longer blast.
        /// </summary>
        public static float HornHoldS(byte trafficHornReason)
        {
            switch (trafficHornReason)
            {
                case 1: return 0.3f;
                case 2: return 0f;
                case 3: return 0.6f;
                case 4: return 0.25f;
                default: return 0.15f;
            }
        }

        /// <summary>Bank variant of a horn for a hold time (0 or less = double tap; short tap; tap; long blast).</summary>
        public static int HornVariant(float holdS)
        {
            float h = Dsp.Sanitize(holdS);
            if (h <= 0f) return 2;
            if (h < 0.18f) return 0;
            if (h < 0.45f) return 1;
            return 3;
        }

        /// <summary>True for buses and trucks (air brakes, reverse alarm).</summary>
        public static bool IsHeavy(EngineModel m)
        {
            return m == EngineModel.BusCity || m == EngineModel.BusEv || m == EngineModel.Truck;
        }

        private static EnginePreset Combustion(EngineModel m, byte cyl, bool four, float idle, float red, float[] h,
                                               float tiltOff, float tiltOn, float jp, float ja, float nb, float nl,
                                               float pipeHz, float pipeQ, float drive, float levelDb, float maxKmh)
        {
            return new EnginePreset
            {
                Model = m, Family = EngineFamily.Combustion, Cylinders = cyl, FourStroke = four, IdleRpm = idle, RedRpm = red,
                Harmonics = h, TiltOff = tiltOff, TiltOn = tiltOn, JitterPeriod = jp, JitterAmp = ja, NoiseBase = nb,
                NoiseLoad = nl, PipeHz = pipeHz, PipeQ = pipeQ, Drive = drive, LevelDb = levelDb, MaxSpeedKmh = maxKmh,
                ShiftUp = 0.88f, ShiftDown = 0.40f, ShiftS = 0.18f, FinalDrive = 3.1f, WheelCircM = 1.9f, TyreLevel = 0.12f,
            };
        }

        private static float[] Ext(float[] a8, int n)
        {
            // Extend an 8-entry table to n harmonics with a gentle geometric tail.
            var h = new float[n];
            for (int i = 0; i < n; i++) h[i] = i < a8.Length ? a8[i] : a8[a8.Length - 1] * (float)Math.Pow(0.82, i - a8.Length + 1);
            return h;
        }

        private static EnginePreset[] Build()
        {
            var t = new EnginePreset[18];
            t[(int)EngineModel.None] = new EnginePreset { Model = EngineModel.None, Family = EngineFamily.Electric, Harmonics = new float[0], Gears = new float[0], LevelDb = -120f };

            var moto = Combustion(EngineModel.MotoCommuter, 1, true, 1400, 8500, Ext(new[] { 1f, .8f, .6f, .5f, .35f, .3f, .2f, .15f }, 12),
                                  1.4f, 0.8f, 0.02f, 0.08f, 0.10f, 0.25f, 105, 2f, 1.6f, -1f, 95f);
            moto.Gears = new[] { 3.0f, 1.9f, 1.4f, 1.1f, 0.9f };
            moto.PopsPerS = 1.5f;
            moto.TyreLevel = 0.06f;
            t[(int)EngineModel.MotoCommuter] = moto;

            var sport = Combustion(EngineModel.MotoSport, 1, true, 1400, 10000, Ext(new[] { 1f, .9f, .7f, .6f, .5f, .4f, .3f, .25f }, 14),
                                   1.2f, 0.6f, 0.015f, 0.06f, 0.10f, 0.30f, 120, 2.5f, 2.0f, 0f, 120f);
            sport.Gears = new[] { 2.9f, 2.0f, 1.5f, 1.2f, 1.0f, 0.85f };
            sport.PopsPerS = 2.5f;
            sport.TyreLevel = 0.06f;
            t[(int)EngineModel.MotoSport] = sport;

            var cruiser = Combustion(EngineModel.MotoCruiser, 1, true, 900, 5500, Ext(new[] { 1f, 1.1f, .9f, .6f, .45f, .3f, .2f, .1f }, 16),
                                     1.6f, 1.0f, 0.04f, 0.15f, 0.08f, 0.20f, 80, 3f, 2.2f, 0f, 110f);
            cruiser.Gears = new[] { 2.6f, 1.7f, 1.3f, 1.05f, 0.88f };
            cruiser.ThumpDuty = 0.3f;
            cruiser.PopsPerS = 1f;
            cruiser.TyreLevel = 0.06f;
            t[(int)EngineModel.MotoCruiser] = cruiser;

            var scooter = Combustion(EngineModel.Scooter, 1, true, 1700, 8000, Ext(new[] { 1f, .7f, .5f, .3f, .2f, .15f, .1f, .05f }, 10),
                                     1.6f, 1.0f, 0.01f, 0.04f, 0.12f, 0.20f, 140, 1.5f, 1.3f, -3f, 85f);
            scooter.Gears = new[] { 1f };
            scooter.Cvt = true;
            scooter.CvtHoldRpm = 5500f;
            scooter.BeltWhine = 0.063f; // −24 dB
            scooter.TyreLevel = 0.05f;
            t[(int)EngineModel.Scooter] = scooter;

            var two = Combustion(EngineModel.Moto2Stroke, 1, false, 1500, 9500, Ext(new[] { 1f, .9f, .8f, .7f, .6f, .55f, .5f, .45f }, 16),
                                 0.9f, 0.4f, 0.02f, 0.10f, 0.20f, 0.40f, 160, 6f, 2.5f, -2f, 100f);
            two.Gears = new[] { 2.8f, 1.8f, 1.35f, 1.1f, 0.92f };
            two.PopsPerS = 4f;
            two.TyreLevel = 0.06f;
            t[(int)EngineModel.Moto2Stroke] = two;

            t[(int)EngineModel.EScooter] = new EnginePreset
            {
                Model = EngineModel.EScooter, Family = EngineFamily.Electric, Harmonics = new[] { 1f, 0.3f, 0.15f }, Gears = new float[0],
                WhineMinHz = 150f, WhineMaxHz = 900f, WhineLagS = 0.08f, LevelDb = -15f, MaxSpeedKmh = 60f, TyreLevel = 0.05f,
                NoiseBase = 0.02f,
            };
            t[(int)EngineModel.TempoSafa] = new EnginePreset
            {
                Model = EngineModel.TempoSafa, Family = EngineFamily.Electric, Harmonics = new[] { 1f, 0.3f, 0.15f }, Gears = new float[0],
                WhineMinHz = 200f, WhineMaxHz = 1500f, WhineLagS = 0.3f, GearMesh = 0.45f, Rattle = 0.35f, LevelDb = -9f,
                MaxSpeedKmh = 45f, TyreLevel = 0.08f, NoiseBase = 0.03f,
            };

            var c3 = Combustion(EngineModel.Car3Cyl, 3, true, 800, 6000, Ext(new[] { 1f, .6f, .5f, .3f, .25f, .2f, .1f, .1f }, 12),
                                1.3f, 0.8f, 0.01f, 0.05f, 0.06f, 0.15f, 45, 1.5f, 1.3f, -4f, 140f);
            c3.Gears = new[] { 3.5f, 2.0f, 1.3f, 1.0f, 0.8f };
            c3.FinalDrive = 4.0f;
            c3.WheelCircM = 1.75f;
            c3.HalfOrderAm = 0.15f;
            c3.TyreLevel = 0.12f;
            t[(int)EngineModel.Car3Cyl] = c3;

            var c4 = Combustion(EngineModel.Car4Cyl, 4, true, 750, 6500, Ext(new[] { 1f, .5f, .35f, .2f, .15f, .1f, .05f, .05f }, 10),
                                1.5f, 1.0f, 0.005f, 0.03f, 0.05f, 0.12f, 50, 1.2f, 1.2f, -5f, 160f);
            c4.Gears = new[] { 3.5f, 1.95f, 1.3f, 1.0f, 0.8f };
            c4.FinalDrive = 4.1f;
            c4.WheelCircM = 1.85f;
            c4.TyreLevel = 0.14f;
            t[(int)EngineModel.Car4Cyl] = c4;

            var suv = Combustion(EngineModel.SuvDiesel, 4, true, 750, 4000, Ext(new[] { 1f, .7f, .5f, .4f, .3f, .25f, .2f, .1f }, 12),
                                 1.2f, 0.8f, 0.01f, 0.05f, 0.08f, 0.20f, 45, 1.5f, 1.6f, -3f, 140f);
            suv.Gears = new[] { 4.0f, 2.3f, 1.4f, 1.0f, 0.78f };
            suv.FinalDrive = 3.7f;
            suv.WheelCircM = 2.3f;
            suv.InjectionTick = 0.25f; // −12 dB
            suv.Turbo = 0.04f;
            suv.TyreLevel = 0.16f;
            t[(int)EngineModel.SuvDiesel] = suv;

            t[(int)EngineModel.CarEv] = new EnginePreset
            {
                Model = EngineModel.CarEv, Family = EngineFamily.Electric, Harmonics = new[] { 1f, 0.3f, 0.15f }, Gears = new float[0],
                WhineMinHz = 300f, WhineMaxHz = 2000f, WhineLagS = 0.05f, LevelDb = -19f, MaxSpeedKmh = 150f, TyreLevel = 0.14f,
                NoiseBase = 0.01f,
            };

            var micro = Combustion(EngineModel.Microbus, 4, true, 700, 3800, Ext(new[] { 1f, .7f, .5f, .4f, .3f, .25f, .2f, .1f }, 12),
                                   1.2f, 0.8f, 0.01f, 0.06f, 0.10f, 0.20f, 40, 1.5f, 1.6f, -2f, 110f);
            micro.Gears = new[] { 4.3f, 2.4f, 1.5f, 1.0f, 0.82f };
            micro.FinalDrive = 4.3f;
            micro.WheelCircM = 2.1f;
            micro.InjectionTick = 0.25f;
            micro.Turbo = 0.03f;
            micro.Rattle = 0.25f;
            micro.TyreLevel = 0.16f;
            t[(int)EngineModel.Microbus] = micro;

            var bus = Combustion(EngineModel.BusCity, 6, true, 600, 2600, Ext(new[] { 1f, .9f, .7f, .6f, .5f, .4f, .3f, .25f }, 16),
                                 1.0f, 0.6f, 0.008f, 0.04f, 0.08f, 0.25f, 30, 2f, 2.0f, 1f, 80f);
            bus.Gears = new[] { 6.0f, 3.4f, 2.1f, 1.4f, 1.0f };
            bus.FinalDrive = 5.3f;
            bus.WheelCircM = 3.2f;
            bus.InjectionTick = 0.2f;
            bus.Turbo = 0.06f;
            bus.Rattle = 0.3f;
            bus.ShiftS = 0.25f;
            bus.TyreLevel = 0.2f;
            t[(int)EngineModel.BusCity] = bus;

            var truck = bus;
            truck.Model = EngineModel.Truck;
            truck.Harmonics = Ext(new[] { 1f, .95f, .75f, .6f, .5f, .4f, .3f, .25f }, 16);
            truck.RedRpm = 2500f;
            truck.Drive = 2.2f;
            truck.Rattle = 0.35f;
            truck.LevelDb = 2f;
            t[(int)EngineModel.Truck] = truck;

            t[(int)EngineModel.BusEv] = new EnginePreset
            {
                Model = EngineModel.BusEv, Family = EngineFamily.Electric, Harmonics = new[] { 1f, 0.3f, 0.15f }, Gears = new float[0],
                WhineMinHz = 200f, WhineMaxHz = 1200f, WhineLagS = 0.1f, LevelDb = -11f, MaxSpeedKmh = 80f, TyreLevel = 0.2f,
                Rattle = 0.2f, NoiseBase = 0.02f,
            };

            var tractor = Combustion(EngineModel.Tractor, 1, true, 900, 2400, Ext(new[] { 1f, 1f, .8f, .7f, .5f, .4f, .3f, .2f }, 14),
                                     1.0f, 0.7f, 0.03f, 0.12f, 0.10f, 0.20f, 60, 4f, 2.0f, -1f, 30f);
            tractor.Gears = new[] { 9f, 5f, 3f };
            tractor.FinalDrive = 4f;
            tractor.WheelCircM = 3.6f;
            tractor.InjectionTick = 0.3f;
            tractor.Rattle = 0.4f;
            tractor.TyreLevel = 0.1f;
            t[(int)EngineModel.Tractor] = tractor;

            t[(int)EngineModel.Bicycle] = new EnginePreset
            {
                Model = EngineModel.Bicycle, Family = EngineFamily.Pedal, Harmonics = new float[0], Gears = new float[0],
                LevelDb = 2f, MaxSpeedKmh = 25f, TyreLevel = 0.08f, WheelCircM = 2.1f,
            };

            for (int i = 0; i < t.Length; i++)
            {
                if (t[i].Gears == null) t[i].Gears = new float[0];
                if (t[i].Harmonics == null) t[i].Harmonics = new float[0];
            }
            return t;
        }
    }
}
