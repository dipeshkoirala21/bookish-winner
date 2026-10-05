using Ghumante.Core.Driving;

namespace Ghumante.Vehicles
{
    /// <summary>
    /// Explore's tuning on top of Core's presets (ARCHITECTURE.md 7.6, <see cref="VehicleSpec"/>). Core's motorbike has
    /// no boost; the explorer's scooter gets a small one (Shift, gamepad X) so the boost button means something on
    /// the bike as well as on foot (where Core's walker sprints). Every call returns a fresh, validated spec.
    /// Engine-free.
    /// </summary>
    public static class VehicleTuning
    {
        /// <summary>Top-speed multiplier of the scooter while boost is held (85 km/h becomes 102 km/h).</summary>
        public const float MotorbikeBoostFactor = 1.2f;

        /// <summary>Wheel radius of the cartoon scooter (tyre included), for the wheel spin.</summary>
        public const float ScooterWheelRadiusM = 0.25f;

        /// <summary>The explorer's scooter: Core's motorbike with <see cref="MotorbikeBoostFactor"/>.</summary>
        public static VehicleSpec Motorbike()
        {
            VehicleSpec spec = VehicleSpec.Motorbike();
            spec.Name = "explorer-scooter";
            spec.BoostSpeedFactor = MotorbikeBoostFactor;
            spec.Validate();
            return spec;
        }

        /// <summary>The explorer on foot: Core's walker (walk at half stick, run at full, sprint with boost).</summary>
        public static VehicleSpec Walker()
        {
            VehicleSpec spec = VehicleSpec.Walker();
            spec.Validate();
            return spec;
        }
    }
}
