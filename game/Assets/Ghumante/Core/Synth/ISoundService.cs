namespace Ghumante.Core.Synth
{
    /// <summary>
    /// A vehicle's live engine sound (real-time synth voice when it ranks among the nearest, silent otherwise).
    /// Main thread only. Positions and velocities are Unity scene space (metres, after the floating origin);
    /// velocities come from the simulation, never from transform deltas (W2_DESIGN 7.1 Doppler rule).
    /// </summary>
    public interface IEngineSound
    {
        EngineModel Model { get; }

        /// <summary>Drive the sound from speed and pedals: rpm and load come from the built-in drivetrain model.</summary>
        void Drive(float x, float y, float z, float vx, float vy, float vz, float speedMps, float throttle01, float brake01,
                   FootstepSurface surface, float dt);

        /// <summary>Drive the sound from an explicit rpm and load (for vehicles that simulate their own engine).</summary>
        void SetEngine(float x, float y, float z, float vx, float vy, float vz, float rpm, float load, float speedMps, FootstepSurface surface);

        /// <summary>Taps the vehicle's horn (bicycles ring the bell). <paramref name="holdS"/> picks tap or blast.</summary>
        void Horn(float holdS);

        /// <summary>Air-brake release (buses and trucks only; ignored otherwise).</summary>
        void AirBrake(bool parking);

        /// <summary>Linear gain multiplier (1 = nominal), e.g. 0 to mute while the vehicle is hidden.</summary>
        float Gain { get; set; }

        /// <summary>Stops the sound and returns the voice to the pool. The handle must not be used afterwards.</summary>
        void Release();
    }

    /// <summary>An aircraft's live sound (at most two real-time aircraft voices; the rest are silent).</summary>
    public interface IAircraftSound
    {
        AircraftSoundClass Class { get; }

        /// <summary>Per-frame state: scene position, simulation velocity, power, reverse and height above ground.</summary>
        void Set(float x, float y, float z, float vx, float vy, float vz, float thrust01, float reverse01, float heightAglM, bool onGround);

        void Release();
    }

    /// <summary>
    /// The audio entry points gameplay assemblies call (W2_DESIGN 10.3 events: HornUsed, BellRung,
    /// VehicleEntered). <c>Ghumante.Audio.AudioDirector</c> implements it; App hands it to gameplay at wiring time
    /// (ARCHITECTURE 7.1), and <see cref="NullSoundService"/> stands in until then. Main thread only. Positions
    /// are Unity scene space.
    /// </summary>
    public interface ISoundService
    {
        /// <summary>A footstep of the player (protected voice) or of an NPC (capped per tier).
        /// <paramref name="footSurface"/> is a <c>FootSurface</c> byte (= <see cref="FootstepSurface"/>).</summary>
        void PlayFootstep(byte footSurface, FootstepGait gait, float x, float y, float z, bool player, bool barefoot = false);

        /// <summary>A horn tap or blast at a position (traffic, the player, the TrafficSim Horn event).</summary>
        void PlayHorn(HornKind kind, float x, float y, float z, float vx, float vy, float vz, float holdS);

        /// <summary>A traffic agent's honk (the TrafficSim Horn event): the horn follows the agent's sound model
        /// (<c>VehicleClass</c> byte, seeded by <paramref name="agentId"/>) and the hold follows the reason
        /// (<c>Ghumante.Core.Traffic.HornKind</c> byte).</summary>
        void PlayTrafficHorn(byte vehicleClass, int agentId, byte hornReason, float x, float y, float z, float vx, float vy, float vz);

        /// <summary>A shrine bell, hand bell, conch, cymbal or prayer wheel at a position (BellRung event).</summary>
        void PlayBell(BellKind kind, float x, float y, float z);

        /// <summary>Any bank sound as a one-shot at a position with an explicit class and gain.</summary>
        void PlayOneShot(BankSound sound, SoundClass cls, float x, float y, float z, float gainDb = 0f, float pitch = 1f);

        /// <summary>A UI or 2D foley sound (not spatialised).</summary>
        void PlayUi(BankSound sound, float gainDb = 0f);

        /// <summary>Opens a live engine voice for a vehicle; <paramref name="seed"/> (agent id) varies its sound.</summary>
        IEngineSound OpenEngine(EngineModel model, uint seed, bool player);

        /// <summary>Opens a live aircraft voice.</summary>
        IAircraftSound OpenAircraft(AircraftSoundClass cls, uint seed);
    }

    /// <summary>Does nothing; the default before App wires the real service.</summary>
    public sealed class NullSoundService : ISoundService
    {
        public static readonly NullSoundService Instance = new NullSoundService();

        private sealed class NullEngine : IEngineSound
        {
            public EngineModel Model { get; set; }

            public float Gain { get; set; } = 1f;

            public void Drive(float x, float y, float z, float vx, float vy, float vz, float speedMps, float throttle01, float brake01,
                              FootstepSurface surface, float dt)
            {
            }

            public void SetEngine(float x, float y, float z, float vx, float vy, float vz, float rpm, float load, float speedMps, FootstepSurface surface)
            {
            }

            public void Horn(float holdS)
            {
            }

            public void AirBrake(bool parking)
            {
            }

            public void Release()
            {
            }
        }

        private sealed class NullAircraft : IAircraftSound
        {
            public AircraftSoundClass Class { get; set; }

            public void Set(float x, float y, float z, float vx, float vy, float vz, float thrust01, float reverse01, float heightAglM, bool onGround)
            {
            }

            public void Release()
            {
            }
        }

        public void PlayFootstep(byte footSurface, FootstepGait gait, float x, float y, float z, bool player, bool barefoot = false)
        {
        }

        public void PlayHorn(HornKind kind, float x, float y, float z, float vx, float vy, float vz, float holdS)
        {
        }

        public void PlayTrafficHorn(byte vehicleClass, int agentId, byte hornReason, float x, float y, float z, float vx, float vy, float vz)
        {
        }

        public void PlayBell(BellKind kind, float x, float y, float z)
        {
        }

        public void PlayOneShot(BankSound sound, SoundClass cls, float x, float y, float z, float gainDb = 0f, float pitch = 1f)
        {
        }

        public void PlayUi(BankSound sound, float gainDb = 0f)
        {
        }

        public IEngineSound OpenEngine(EngineModel model, uint seed, bool player)
        {
            return new NullEngine { Model = model };
        }

        public IAircraftSound OpenAircraft(AircraftSoundClass cls, uint seed)
        {
            return new NullAircraft { Class = cls };
        }
    }
}
