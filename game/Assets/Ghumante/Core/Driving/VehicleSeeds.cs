using Ghumante.Core.Characters;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// The seeds a vehicle's look is drawn from, shared by every presenter so one vehicle keeps one shape: traffic agents
    /// use their agent id; a parked (community-fleet) vehicle and the player's borrowed copy of it use
    /// <see cref="Fleet"/> of the parked id; the player's garage vehicles keep the plate seed stored in the save. The
    /// model type is <see cref="VehicleMesher.ModelFor"/> of that seed, so the parked body, the borrowed body and its plate
    /// all agree. Deterministic and allocation-free.
    /// </summary>
    public static class VehicleSeeds
    {
        /// <summary>Salt of the fleet seed ("FLT").</summary>
        public const uint FleetSalt = 0x464C54u;

        /// <summary>The plate (and model type) seed of parked vehicle <paramref name="parkedId"/> and of the player's
        /// borrowed copy of it.</summary>
        public static uint Fleet(uint parkedId)
        {
            return CharMath.Hash(parkedId, FleetSalt);
        }

        /// <summary>The model type parked vehicle <paramref name="parkedId"/> (catalogue entry <paramref name="variant"/>)
        /// is drawn as, parked or borrowed.</summary>
        public static byte FleetModel(int variant, uint parkedId)
        {
            return VehicleMesher.ModelFor(variant, Fleet(parkedId));
        }

        /// <summary>The model type a vehicle with plate seed <paramref name="plateSeed"/> is drawn as (the player's own
        /// vehicles, traffic agents by agent id).</summary>
        public static byte Model(int variant, uint plateSeed)
        {
            return VehicleMesher.ModelFor(variant, plateSeed);
        }
    }
}
