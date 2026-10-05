using Ghumante.Core.Synth;
using UnityEngine;

namespace Ghumante.Audio
{
    /// <summary>
    /// A vehicle's engine sound (<see cref="IEngineSound"/>). It always tracks state; it only sounds while the
    /// director has given it one of the tier's real-time synth voices (the player's always, then the loudest NPC
    /// engines at the listener). Main thread only. Pooled by the director.
    /// </summary>
    internal sealed class EngineHandle : IEngineSound
    {
        internal AudioDirector Director;
        internal EnginePreset Preset;
        internal Drivetrain Train;
        internal uint SeedValue;
        internal bool Player;
        internal bool Released = true;
        internal Vector3 Position, Velocity;
        internal float Rpm, Load, Speed;
        internal FootstepSurface Surface;
        internal EngineVoiceHost Host;
        internal Smoothed Doppler;
        internal float Occlusion, OcclusionTarget;
        internal float Score;
        private EngineModel _model;

        public EngineModel Model
        {
            get { return _model; }
        }

        public float Gain { get; set; } = 1f;

        internal void Open(AudioDirector d, EngineModel model, uint seed, bool player)
        {
            Director = d;
            _model = model;
            Preset = EnginePresets.Get(model);
            Train = new Drivetrain(Preset);
            SeedValue = seed;
            Player = player;
            Released = false;
            Position = Velocity = Vector3.zero;
            Rpm = Preset.IdleRpm;
            Load = 0f;
            Speed = 0f;
            Surface = FootstepSurface.Asphalt;
            Host = null;
            Doppler = default;
            Occlusion = OcclusionTarget = 0f;
            Gain = 1f;
        }

        public void Drive(float x, float y, float z, float vx, float vy, float vz, float speedMps, float throttle01, float brake01,
                          FootstepSurface surface, float dt)
        {
            if (Released) return;
            int before = Train.Shifts;
            Train.Step(dt, speedMps, throttle01, brake01);
            if (Train.Shifts != before && Player && Director != null) Director.OnGearShift(this);
            SetEngine(x, y, z, vx, vy, vz, Train.Rpm, Train.Load, speedMps, surface);
        }

        public void SetEngine(float x, float y, float z, float vx, float vy, float vz, float rpm, float load, float speedMps, FootstepSurface surface)
        {
            if (Released) return;
            Position = new Vector3(Dsp.Sanitize(x), Dsp.Sanitize(y), Dsp.Sanitize(z));
            Velocity = new Vector3(Dsp.Sanitize(vx), Dsp.Sanitize(vy), Dsp.Sanitize(vz));
            Rpm = Dsp.Sanitize(rpm);
            Load = Dsp.Clamp(load, -1f, 1f);
            Speed = Mathf.Abs(Dsp.Sanitize(speedMps));
            Surface = surface;
        }

        public void Horn(float holdS)
        {
            if (Released || Director == null) return;
            Director.PlayHorn(EnginePresets.HornFor(_model), Position.x, Position.y, Position.z, Velocity.x, Velocity.y, Velocity.z, holdS);
        }

        public void AirBrake(bool parking)
        {
            if (Released || Director == null || !EnginePresets.IsHeavy(_model)) return;
            Director.PlayOneShot(parking ? BankSound.ParkingBrake : BankSound.AirBrake, SoundClass.AirBrake, Position.x, Position.y, Position.z);
        }

        public void Release()
        {
            if (Released) return;
            Released = true;
            if (Director != null) Director.ReleaseEngine(this);
        }
    }

    /// <summary>An aircraft's sound (<see cref="IAircraftSound"/>); sounds only while it holds an aircraft voice.</summary>
    internal sealed class AircraftHandle : IAircraftSound
    {
        internal AudioDirector Director;
        internal uint SeedValue;
        internal bool Released = true;
        internal Vector3 Position, Velocity;
        internal float Thrust, Reverse, HeightAgl;
        internal bool OnGround;
        internal AircraftVoiceHost Host;
        internal Smoothed Doppler;
        internal float Score;
        private AircraftSoundClass _class;

        public AircraftSoundClass Class
        {
            get { return _class; }
        }

        internal void Open(AudioDirector d, AircraftSoundClass cls, uint seed)
        {
            Director = d;
            _class = cls;
            SeedValue = seed;
            Released = false;
            Position = Velocity = Vector3.zero;
            Thrust = Reverse = HeightAgl = 0f;
            OnGround = false;
            Host = null;
            Doppler = default;
        }

        public void Set(float x, float y, float z, float vx, float vy, float vz, float thrust01, float reverse01, float heightAglM, bool onGround)
        {
            if (Released) return;
            Position = new Vector3(Dsp.Sanitize(x), Dsp.Sanitize(y), Dsp.Sanitize(z));
            Velocity = new Vector3(Dsp.Sanitize(vx), Dsp.Sanitize(vy), Dsp.Sanitize(vz));
            Thrust = Dsp.Clamp01(thrust01);
            Reverse = Dsp.Clamp01(reverse01);
            HeightAgl = Mathf.Max(0f, Dsp.Sanitize(heightAglM));
            OnGround = onGround;
        }

        public void Release()
        {
            if (Released) return;
            Released = true;
            if (Director != null) Director.ReleaseAircraft(this);
        }
    }
}
