using System;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;

namespace Ghumante.Core.Characters
{
    /// <summary>The camera rig family of what the player is doing (W2_DESIGN 6.4). Append-only.</summary>
    public enum RigClass : byte
    {
        Walk = 0,
        Bicycle = 1,

        /// <summary>Scooter and motorbike.</summary>
        TwoWheeler = 2,

        /// <summary>Car and taxi.</summary>
        Car = 3,

        /// <summary>SUV, microbus and tempo.</summary>
        Van = 4,
        Bus = 5,

        /// <summary>Truck, tipper and tanker.</summary>
        Truck = 6,
        Tractor = 7,

        /// <summary>Riding along as a passenger (orbit at 1.2× the driver rig).</summary>
        Passenger = 8,
    }

    /// <summary>The touch layout family (W2_DESIGN 6.5) the HUD shows. Append-only.</summary>
    public enum ControlLayout : byte
    {
        Walk = 0,

        /// <summary>Bicycle, scooter and motorbike: lean steering, a bell or a horn.</summary>
        TwoWheeler = 1,

        /// <summary>Cars, taxis, SUVs, micros, tempos and tractors.</summary>
        Car = 2,

        /// <summary>Buses and trucks: adds Doors / Stop at stops.</summary>
        Heavy = 3,

        /// <summary>Riding along: Stop (the bell), skip to the next stop, Hop off.</summary>
        Passenger = 4,
    }

    /// <summary>
    /// What a catalogue vehicle means for the player (W2_DESIGN 6.2–6.6): its camera rig, touch layout, seat pose,
    /// helmet rule, exit braking and where it may go. All lookups go through <see cref="VehicleCatalogEntry"/> (never asset
    /// ids). Engine-free.
    /// </summary>
    public static class VehicleRoles
    {
        /// <summary>Exit auto-brakes below this speed first (8 km/h); buses and trucks stop fully.</summary>
        public const float ExitBrakeMps = 8f / 3.6f;

        private static readonly SeatSocket[][] SeatCache = new SeatSocket[VehicleCatalog.Count][];

        /// <summary>The seat sockets of a catalogue variant, built once (VehicleSeats.For allocates per call). Callers
        /// must not modify the returned array.</summary>
        public static SeatSocket[] Seats(int variant)
        {
            if (variant < 0 || variant >= SeatCache.Length) return VehicleSeats.For(VehicleCatalog.At(Math.Max(0, Math.Min(VehicleCatalog.Count - 1, variant))));
            SeatSocket[] s = SeatCache[variant];
            if (s == null)
            {
                s = VehicleSeats.For(variant);
                SeatCache[variant] = s;
            }
            return s;
        }

        public static bool IsTwoWheeler(BodyShape s)
        {
            return s == BodyShape.Scooter || s == BodyShape.Motorbike || s == BodyShape.Cruiser || s == BodyShape.Bicycle;
        }

        public static bool IsHeavy(BodyShape s)
        {
            return s == BodyShape.Bus || s == BodyShape.Truck || s == BodyShape.Tipper || s == BodyShape.Tanker;
        }

        public static RigClass RigOf(in VehicleCatalogEntry e)
        {
            switch (e.Shape)
            {
                case BodyShape.Bicycle: return RigClass.Bicycle;
                case BodyShape.Scooter:
                case BodyShape.Motorbike:
                case BodyShape.Cruiser: return RigClass.TwoWheeler;
                case BodyShape.Suv:
                case BodyShape.Van:
                case BodyShape.Tempo: return RigClass.Van;
                case BodyShape.Bus: return RigClass.Bus;
                case BodyShape.Truck:
                case BodyShape.Tipper:
                case BodyShape.Tanker: return RigClass.Truck;
                case BodyShape.Tractor: return RigClass.Tractor;
                case BodyShape.Rickshaw: return RigClass.Passenger;
                default: return RigClass.Car;
            }
        }

        public static ControlLayout LayoutOf(in VehicleCatalogEntry e)
        {
            if (IsTwoWheeler(e.Shape)) return ControlLayout.TwoWheeler;
            if (IsHeavy(e.Shape)) return ControlLayout.Heavy;
            if (e.Shape == BodyShape.Rickshaw) return ControlLayout.Passenger;
            return ControlLayout.Car;
        }

        /// <summary>The driver's seat pose for an entry.</summary>
        public static SeatPose DriverPose(in VehicleCatalogEntry e)
        {
            switch (e.Shape)
            {
                case BodyShape.Bicycle: return SeatPose.Bicycle;
                case BodyShape.Scooter: return SeatPose.Scooter;
                case BodyShape.Motorbike: return SeatPose.Motorbike;
                case BodyShape.Cruiser: return SeatPose.Cruiser;
                case BodyShape.Bus: return SeatPose.BusDriver;
                case BodyShape.Truck:
                case BodyShape.Tipper:
                case BodyShape.Tanker: return SeatPose.TruckDriver;
                case BodyShape.Tractor: return SeatPose.TractorDriver;
                case BodyShape.Rickshaw: return SeatPose.Rickshaw;
                default: return SeatPose.CarDriver;
            }
        }

        /// <summary>The pose for a seat of <paramref name="kind"/> on an entry (passengers, pillions, standing).</summary>
        public static SeatPose PoseFor(in VehicleCatalogEntry e, SeatKind kind)
        {
            switch (kind)
            {
                case SeatKind.Driver: return DriverPose(e);
                case SeatKind.Pillion: return SeatPose.Pillion;
                case SeatKind.Standing: return SeatPose.BusStanding;
                default:
                    if (e.Shape == BodyShape.Rickshaw) return SeatPose.Rickshaw;
                    return e.Shape == BodyShape.Bus || e.Shape == BodyShape.Tempo || e.Shape == BodyShape.Van ? SeatPose.BusSeated : SeatPose.CarPassenger;
            }
        }

        /// <summary>Helmets pop on at every two-wheeler mount (P §2.7), for riders and pillions.</summary>
        public static bool NeedsHelmet(in VehicleCatalogEntry e)
        {
            return IsTwoWheeler(e.Shape);
        }

        /// <summary>Speed below which an exit may start: 8 km/h, buses and trucks at a standstill.</summary>
        public static float ExitSpeedMps(in VehicleCatalogEntry e)
        {
            return IsHeavy(e.Shape) || e.Shape == BodyShape.Tractor ? 0.3f : ExitBrakeMps;
        }

        /// <summary>The horn this entry sounds when the player presses Horn (a bell on the bicycle).</summary>
        public static Synth.HornKind HornOf(in VehicleCatalogEntry e)
        {
            switch (e.Shape)
            {
                case BodyShape.Bicycle:
                case BodyShape.Rickshaw: return Synth.HornKind.BicycleBell;
                case BodyShape.Scooter: return Synth.HornKind.Scooter;
                case BodyShape.Motorbike:
                case BodyShape.Cruiser: return Synth.HornKind.Moto;
                case BodyShape.Tempo: return Synth.HornKind.Tempo;
                case BodyShape.Bus:
                case BodyShape.Truck:
                case BodyShape.Tipper:
                case BodyShape.Tanker: return Synth.HornKind.Bus;
                default: return Synth.HornKind.Car;
            }
        }

        /// <summary>
        /// May this vehicle be ridden in a sacred zone of <paramref name="kind"/>? Motor vehicles never (L16
        /// SACRED_NO_VEHICLE); bicycles only in heritage squares ("walk-and-cycle only", W2_DESIGN 1.2 Basantapur), never in
        /// compounds, courtyards, stupa koras or ghats (W2-O1: no vehicles in compounds).
        /// </summary>
        public static bool AllowedIn(in VehicleCatalogEntry e, SacredZoneKind kind)
        {
            if (kind == SacredZoneKind.None) return true;
            return e.Shape == BodyShape.Bicycle && kind == SacredZoneKind.HeritageSquare;
        }

        /// <summary>The engine voice for the player's vehicle (Track C2's mapping of the catalogue).</summary>
        public static Synth.EngineModel EngineOf(in VehicleCatalogEntry e)
        {
            return Synth.EnginePresets.ForCatalog(e.EnginePreset, (byte)e.DriveKind);
        }

        /// <summary>Two-wheeler pose: a foot goes down below 1 km/h (P §6.3).</summary>
        public static bool FootDown(in VehicleCatalogEntry e, float speedMps)
        {
            return IsTwoWheeler(e.Shape) && Math.Abs(speedMps) < 1f / 3.6f;
        }
    }

    /// <summary>
    /// Calm mode inside sacred areas (W2_DESIGN 6.6): walking pace capped at the run (no sprint), no comic fidgets or
    /// running emotes, no "boing" accents, an entry-rule card where curated, and the shrine bell at most once per 3 s and
    /// 3 times per visit. Engine-free state for one player.
    /// </summary>
    public sealed class CalmMode
    {
        public const float BellCooldownS = 3f;
        public const int BellsPerVisit = 3;

        /// <summary>Walking pace cap inside: the run speed (4.5 m/s, no sprint).</summary>
        public const float SpeedCapMps = 4.5f;

        private float _sinceBell = 999f;
        private int _bells;
        private long _zone;

        public bool Active { get; private set; }

        /// <summary>The zone the player is in (0 outside).</summary>
        public long ZoneRef
        {
            get { return _zone; }
        }

        public SacredZoneKind Kind { get; private set; }
        public EntryRule Rule { get; private set; }

        /// <summary>
        /// Updates from the zone under the player. Returns +1 on entering a zone (show the entry-rule card), −1 on leaving,
        /// 0 otherwise. A new visit (a different zone) resets the bell count.
        /// </summary>
        public int Update(float dt, bool inZone, in SacredZone zone)
        {
            if (dt > 0f) _sinceBell += dt;
            if (!inZone)
            {
                if (!Active) return 0;
                Active = false;
                _zone = 0;
                Kind = SacredZoneKind.None;
                Rule = EntryRule.None;
                return -1;
            }
            if (Active && zone.AreaRef == _zone) return 0;
            Active = true;
            if (zone.AreaRef != _zone) _bells = 0;
            _zone = zone.AreaRef;
            Kind = zone.Kind;
            Rule = zone.Rule;
            return 1;
        }

        /// <summary>True (and counted) when the player may ring a bell now.</summary>
        public bool TryRingBell()
        {
            if (!Active || _sinceBell < BellCooldownS || _bells >= BellsPerVisit) return false;
            _sinceBell = 0f;
            _bells++;
            return true;
        }

        /// <summary>Sprint is not allowed while calm.</summary>
        public bool AllowSprint
        {
            get { return !Active; }
        }

        /// <summary>The localisation key of the entry-rule card (null for none).</summary>
        public static string CardKey(EntryRule rule)
        {
            switch (rule)
            {
                case EntryRule.ShoesOff: return "sacred.rule.shoes_off";
                case EntryRule.QuietWorship: return "sacred.rule.quiet";
                case EntryRule.RealCompoundClosedToNonHindus: return "sacred.rule.closed_in_reality";
                case EntryRule.InteriorNoPhoto: return "sacred.rule.no_photo";
                case EntryRule.KumariNotShown: return "sacred.rule.kumari";
                case EntryRule.NoLeather: return "sacred.rule.no_leather";
                default: return null;
            }
        }
    }
}
