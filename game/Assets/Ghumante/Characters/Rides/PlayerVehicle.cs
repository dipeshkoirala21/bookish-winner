using System;
using Ghumante.Core.Characters;
using Ghumante.Core.Driving;
using Ghumante.Core.Synth;
using Ghumante.Vehicles;
using Ghumante.Vehicles.Visuals;
using UnityEngine;

namespace Ghumante.Characters.Rides
{
    /// <summary>Where a vehicle the player may drive comes from (W2-O5: no theft).</summary>
    public enum VehicleSource : byte
    {
        /// <summary>The player's own (summoned with the whistle).</summary>
        Garage = 0,

        /// <summary>A parked community-fleet vehicle with the green key tag, borrowed; it returns home on its own.</summary>
        Fleet = 1,
    }

    /// <summary>
    /// A vehicle the player can drive, in the world: its catalogue entry and handling (<see cref="VehicleCatalogEntry.Spec"/>
    /// on Core's <see cref="ArcadeVehicle"/> at a fixed step), its look (<see cref="DrivenVehicleView"/>), its seats and its
    /// live engine voice while someone sits in it. Parked vehicles settle with the brake held, then stop simulating.
    /// </summary>
    public sealed class PlayerVehicle : IDisposable
    {
        /// <summary>Seconds a parked vehicle keeps simulating (braked) after the player leaves, to settle.</summary>
        public const float SettleSeconds = 1.5f;

        public readonly long Id;
        public readonly int Variant;
        public readonly byte Livery;
        public readonly uint PlateSeed;
        public readonly VehicleSource Source;
        public readonly VehicleCatalogEntry Entry;
        public readonly SeatSocket[] Seats;
        public readonly FixedStepDriver Driver;
        public readonly DrivenVehicleView View;

        /// <summary>The fleet loan (home and return rule) of a borrowed vehicle, else null.</summary>
        public readonly FleetLoan Loan;

        private IEngineSound _engine;
        private float _settle;

        /// <summary>A summoned garage vehicle driving itself to the player.</summary>
        public bool Arriving;

        public double ArriveX, ArriveZ;
        public float ArriveS;

        /// <summary>A late arrival was moved once to a road just out of view behind the camera.</summary>
        public bool ArriveRelocated;

        public PlayerVehicle(long id, int variant, byte livery, uint plateSeed, VehicleSource source, Transform parent, Material material,
                             VehicleMeshCache cache, FleetLoan loan)
        {
            Id = id;
            Variant = variant;
            Livery = livery;
            PlateSeed = plateSeed;
            Source = source;
            Entry = VehicleCatalog.At(variant);
            Seats = VehicleSeats.For(Entry);
            float wheel = Math.Max(0.15f, 0.5f * Entry.WheelDiaM * VehicleMesher.WheelScale);
            Driver = new FixedStepDriver(new ArcadeVehicle(Entry.Spec()), FixedStepDriver.DefaultStepS, wheel);
            View = DrivenVehicleView.Create(parent, material, cache, variant, livery, plateSeed, source == VehicleSource.Fleet);
            Loan = loan;
        }

        public ArcadeVehicle Vehicle
        {
            get { return Driver.Vehicle; }
        }

        /// <summary>The vehicle frame (pose for the seat sockets) from the interpolated pose.</summary>
        public VehicleFrame Frame
        {
            get
            {
                VehiclePose p = Driver.Interpolated;
                return new VehicleFrame(p.X, p.Z, p.Y, p.HeadingRad);
            }
        }

        public bool Settling
        {
            get { return _settle > 0f; }
        }

        public void Park()
        {
            _settle = SettleSeconds;
        }

        /// <summary>Steps a parked vehicle while it settles (braked).</summary>
        public void StepParked(float dt, IGroundQuery ground, float wetness)
        {
            if (_settle <= 0f) return;
            _settle -= dt;
            Driver.Advance(dt, new DriveInput(0f, 1f, 0f, false), ground, wetness);
        }

        /// <summary>Opens the engine voice (the player sits in it).</summary>
        public void StartEngine(ISoundService sound)
        {
            if (_engine != null || sound == null) return;
            _engine = sound.OpenEngine(VehicleRoles.EngineOf(Entry), PlateSeed, true);
        }

        public IEngineSound Engine
        {
            get { return _engine; }
        }

        public void StopEngine()
        {
            if (_engine == null) return;
            _engine.Release();
            _engine = null;
        }

        public void Dispose()
        {
            StopEngine();
            if (View != null) UnityEngine.Object.Destroy(View.gameObject);
        }
    }
}
