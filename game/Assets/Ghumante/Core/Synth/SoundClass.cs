namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Spatial and priority class of a sound (W2_DESIGN 7.1 "Voices" and "Distance"; A §6.1–6.2). The class
    /// decides min / max distance, spatial blend, spread, air absorption, priority, Doppler strength and the
    /// same-sound cap group.
    /// </summary>
    public enum SoundClass : byte
    {
        Ui = 0,
        PlayerFootstep = 1,
        NpcFootstep = 2,
        Bicycle = 3,
        TwoWheeler = 4,
        Car = 5,
        Heavy = 6,
        Horn = 7,
        HornMoto = 8,
        AirBrake = 9,
        ShrineBell = 10,
        GreatBell = 11,
        Conch = 12,
        Crow = 13,
        Bird = 14,
        Kite = 15,
        DogBark = 16,
        Animal = 17,
        Street = 18,
        Foley = 19,
        PlayerEngine = 20,
        Helicopter = 21,
        Turboprop = 22,
        Jet = 23,
        Ambience = 24,
        Thunder = 25,
    }

    /// <summary>Same-sound concurrency groups (W2_DESIGN 7.1: horn 3, bell 3, crow 2, bark 2).</summary>
    public enum CapGroup : byte
    {
        None = 0,
        Horn = 1,
        Bell = 2,
        Crow = 3,
        Bark = 4,
        NpcFootstep = 5,
    }

    /// <summary>Spatial rules of one <see cref="SoundClass"/>.</summary>
    public readonly struct SoundClassInfo
    {
        public readonly float MinM, MaxM;
        /// <summary>0 = 2D, 1 = fully 3D.</summary>
        public readonly float SpatialBlend;
        public readonly float SpreadDeg;
        /// <summary>Apply the air-absorption low-pass beyond <see cref="AirFromM"/>.</summary>
        public readonly bool AirLpf;
        public readonly float AirFromM;
        /// <summary>Unity priority 0 (highest) … 256 (lowest).</summary>
        public readonly int Priority;
        /// <summary>Never stolen by another request (UI, player, beds).</summary>
        public readonly bool Protected;
        /// <summary>Doppler strength (0 = none; vehicles 1, cartoon may exaggerate).</summary>
        public readonly float Doppler;
        public readonly MixBus Bus;
        public readonly CapGroup Cap;
        /// <summary>Level target at 10 m (dBFS), used for the −45 dBFS start cull.</summary>
        public readonly float LevelDbAt10M;

        public SoundClassInfo(float minM, float maxM, float blend, float spread, bool air, float airFrom, int priority,
                              bool prot, float doppler, MixBus bus, CapGroup cap, float levelDb)
        {
            MinM = minM;
            MaxM = maxM;
            SpatialBlend = blend;
            SpreadDeg = spread;
            AirLpf = air;
            AirFromM = airFrom;
            Priority = priority;
            Protected = prot;
            Doppler = doppler;
            Bus = bus;
            Cap = cap;
            LevelDbAt10M = levelDb;
        }
    }

    /// <summary>The §7.1 / A §6.1–6.2 class table.</summary>
    public static class SoundClasses
    {
        private static readonly SoundClassInfo[] Table = Build();

        public static int Count
        {
            get { return Table.Length; }
        }

        public static SoundClassInfo Get(SoundClass c)
        {
            int i = (int)c;
            if (i < 0 || i >= Table.Length) i = (int)SoundClass.Foley;
            return Table[i];
        }

        private static SoundClassInfo[] Build()
        {
            var t = new SoundClassInfo[26];
            //                                       min    max  blend spread air   airFrom prio prot  dopp bus               cap                     dB@10m
            t[(int)SoundClass.Ui] = new SoundClassInfo(1f, 10f, 0f, 0f, false, 0f, 0, true, 0f, MixBus.Ui, CapGroup.None, -12f);
            t[(int)SoundClass.PlayerFootstep] = new SoundClassInfo(1f, 15f, 0.6f, 60f, false, 0f, 8, true, 0f, MixBus.Player, CapGroup.None, -22f);
            t[(int)SoundClass.NpcFootstep] = new SoundClassInfo(1f, 12f, 1f, 0f, false, 0f, 192, false, 0f, MixBus.Ambience, CapGroup.NpcFootstep, -26f);
            t[(int)SoundClass.Bicycle] = new SoundClassInfo(1.5f, 25f, 1f, 0f, false, 0f, 128, false, 1f, MixBus.Traffic, CapGroup.None, -26f);
            t[(int)SoundClass.TwoWheeler] = new SoundClassInfo(3f, 60f, 1f, 30f, false, 0f, 96, false, 1f, MixBus.Traffic, CapGroup.None, -20f);
            t[(int)SoundClass.Car] = new SoundClassInfo(4f, 80f, 1f, 45f, false, 0f, 96, false, 1f, MixBus.Traffic, CapGroup.None, -20f);
            t[(int)SoundClass.Heavy] = new SoundClassInfo(6f, 150f, 1f, 60f, true, 80f, 96, false, 1f, MixBus.Traffic, CapGroup.None, -16f);
            t[(int)SoundClass.Horn] = new SoundClassInfo(5f, 200f, 1f, 0f, true, 80f, 64, false, 1f, MixBus.Traffic, CapGroup.Horn, -10f);
            t[(int)SoundClass.HornMoto] = new SoundClassInfo(5f, 120f, 1f, 0f, true, 80f, 64, false, 1f, MixBus.Traffic, CapGroup.Horn, -10f);
            t[(int)SoundClass.AirBrake] = new SoundClassInfo(4f, 100f, 1f, 30f, false, 0f, 64, false, 0f, MixBus.Traffic, CapGroup.None, -14f);
            t[(int)SoundClass.ShrineBell] = new SoundClassInfo(2f, 40f, 1f, 0f, false, 0f, 128, false, 0f, MixBus.Sacred, CapGroup.Bell, -18f);
            t[(int)SoundClass.GreatBell] = new SoundClassInfo(10f, 400f, 1f, 90f, true, 80f, 32, false, 0f, MixBus.Sacred, CapGroup.Bell, -12f);
            t[(int)SoundClass.Conch] = new SoundClassInfo(5f, 150f, 1f, 0f, true, 80f, 32, false, 0f, MixBus.Sacred, CapGroup.None, -14f);
            t[(int)SoundClass.Crow] = new SoundClassInfo(3f, 80f, 1f, 0f, false, 0f, 128, false, 0f, MixBus.Nature, CapGroup.Crow, -24f);
            t[(int)SoundClass.Bird] = new SoundClassInfo(3f, 80f, 1f, 0f, false, 0f, 128, false, 0f, MixBus.Nature, CapGroup.None, -26f);
            t[(int)SoundClass.Kite] = new SoundClassInfo(5f, 200f, 1f, 0f, true, 80f, 128, false, 0f, MixBus.Nature, CapGroup.None, -24f);
            t[(int)SoundClass.DogBark] = new SoundClassInfo(4f, 250f, 1f, 0f, true, 80f, 128, false, 0f, MixBus.Nature, CapGroup.Bark, -18f);
            t[(int)SoundClass.Animal] = new SoundClassInfo(3f, 100f, 1f, 0f, false, 0f, 128, false, 0f, MixBus.Nature, CapGroup.None, -22f);
            t[(int)SoundClass.Street] = new SoundClassInfo(3f, 60f, 1f, 0f, false, 0f, 192, false, 0f, MixBus.Ambience, CapGroup.None, -24f);
            t[(int)SoundClass.Foley] = new SoundClassInfo(1f, 15f, 0.6f, 60f, false, 0f, 12, true, 0f, MixBus.Player, CapGroup.None, -20f);
            t[(int)SoundClass.PlayerEngine] = new SoundClassInfo(2f, 80f, 0.7f, 60f, false, 0f, 4, true, 0f, MixBus.Player, CapGroup.None, -14f);
            t[(int)SoundClass.Helicopter] = new SoundClassInfo(30f, 4000f, 1f, 120f, true, 80f, 64, false, 1f, MixBus.Aircraft, CapGroup.None, 2f);
            t[(int)SoundClass.Turboprop] = new SoundClassInfo(50f, 6000f, 1f, 120f, true, 80f, 64, false, 1f, MixBus.Aircraft, CapGroup.None, 6f);
            t[(int)SoundClass.Jet] = new SoundClassInfo(80f, 9000f, 1f, 120f, true, 80f, 64, false, 1f, MixBus.Aircraft, CapGroup.None, 10f);
            t[(int)SoundClass.Ambience] = new SoundClassInfo(1f, 10f, 0f, 0f, false, 0f, 40, true, 0f, MixBus.Ambience, CapGroup.None, -24f);
            t[(int)SoundClass.Thunder] = new SoundClassInfo(1f, 10f, 0f, 0f, false, 0f, 40, false, 0f, MixBus.Nature, CapGroup.None, -18f);
            return t;
        }

        /// <summary>The class an engine model's voice uses (§7.1 distances).</summary>
        public static SoundClass ForEngine(EngineModel m, bool player)
        {
            if (player) return SoundClass.PlayerEngine;
            switch (m)
            {
                case EngineModel.Bicycle: return SoundClass.Bicycle;
                case EngineModel.MotoCommuter:
                case EngineModel.MotoSport:
                case EngineModel.MotoCruiser:
                case EngineModel.Scooter:
                case EngineModel.Moto2Stroke:
                case EngineModel.EScooter:
                case EngineModel.TempoSafa:
                    return SoundClass.TwoWheeler;
                case EngineModel.BusCity:
                case EngineModel.BusEv:
                case EngineModel.Truck:
                case EngineModel.Tractor:
                    return SoundClass.Heavy;
                default:
                    return SoundClass.Car;
            }
        }

        public static SoundClass ForAircraft(AircraftSoundClass c)
        {
            switch (c)
            {
                case AircraftSoundClass.Helicopter: return SoundClass.Helicopter;
                case AircraftSoundClass.NarrowBody:
                case AircraftSoundClass.WideBody: return SoundClass.Jet;
                default: return SoundClass.Turboprop;
            }
        }

        /// <summary>Max same-sound concurrency of a cap group (NPC footsteps per tier come from the budget).</summary>
        public static int CapOf(CapGroup g, in VoiceBudget budget)
        {
            switch (g)
            {
                case CapGroup.Horn: return 3;
                case CapGroup.Bell: return 3;
                case CapGroup.Crow: return 2;
                case CapGroup.Bark: return 2;
                case CapGroup.NpcFootstep: return budget.MaxNpcFootsteps;
                default: return int.MaxValue;
            }
        }
    }
}
