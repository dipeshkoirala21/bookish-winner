// Ghumante.Vehicles: vehicles on top of Core's arcade model (ARCHITECTURE.md 7.6). M1 track D adds the explorer's
// scooter: Explore tuning (VehicleTuning), a fixed-step driver with interpolation (FixedStepDriver, engine-free), and
// the placeholder cartoon model with dust and mud puffs (Visuals/). W2 track B adds VehicleMeshCache: Unity meshes
// for Core's procedural catalogue vehicles (VehicleMesher), shared per variant, livery and level for traffic.
namespace Ghumante.Vehicles
{
    /// <summary>Marker for the <c>Ghumante.Vehicles</c> assembly.</summary>
    public static class VehiclesAssembly
    {
        /// <summary>Assembly name, as declared in the asmdef.</summary>
        public const string Name = "Ghumante.Vehicles";
    }
}
