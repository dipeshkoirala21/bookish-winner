using System;

namespace Ghumante.Core.Traffic
{
    /// <summary>
    /// Traffic classes of the simulation (W2_DESIGN 10.3; append-only). <see cref="Driving.VehicleCatalog"/> maps every
    /// catalogue asset to one; variants (taxi colours, tanker, coach) carry the rest.
    /// </summary>
    public enum VehicleClass : byte
    {
        TwoWheeler = 0,
        Bicycle = 1,
        Rickshaw = 2,
        Car = 3,
        Taxi = 4,
        Suv = 5,
        Microbus = 6,
        Tempo = 7,
        Minibus = 8,
        Bus = 9,
        Truck = 10,
        Tanker = 11,
        Tractor = 12,
        Service = 13,
    }

    /// <summary>Bit masks over <see cref="VehicleClass"/>.</summary>
    public static class VehicleClasses
    {
        public const int Count = 14;

        /// <summary>Every class.</summary>
        public const uint All = (1u << Count) - 1;

        /// <summary>Classes with an engine (everything but bicycles and cycle rickshaws).</summary>
        public const uint Motor = All & ~((1u << (int)VehicleClass.Bicycle) | (1u << (int)VehicleClass.Rickshaw));

        public static uint Bit(VehicleClass c)
        {
            return 1u << (int)c;
        }

        public static bool IsMotor(VehicleClass c)
        {
            return (Motor & Bit(c)) != 0;
        }

        /// <summary>Two-wheelers and bicycles: they filter, lean and keep to the kerb lane.</summary>
        public static bool IsTwoWheel(VehicleClass c)
        {
            return c == VehicleClass.TwoWheeler || c == VehicleClass.Bicycle;
        }

        /// <summary>Buses, trucks and tankers (long, slow, wide turns).</summary>
        public static bool IsHeavy(VehicleClass c)
        {
            return c == VehicleClass.Bus || c == VehicleClass.Truck || c == VehicleClass.Tanker || c == VehicleClass.Minibus;
        }
    }

    /// <summary>What an obstacle in the traffic sim is (W2_DESIGN 10.3).</summary>
    public enum ObstacleKind : byte
    {
        Cow = 0,
        Dog = 1,
        Player = 2,
        Pedestrian = 3,
        ParkedVehicle = 4,
        Goat = 5,
    }

    /// <summary>Why an agent honked (the audio hook picks the horn and its pattern).</summary>
    public enum HornKind : byte
    {
        Tap = 0,

        /// <summary>Blind bend or hairpin.</summary>
        Bend = 1,

        /// <summary>Overtaking: a short double tap.</summary>
        Overtake = 2,

        /// <summary>Blocked by a stopped vehicle.</summary>
        Blocked = 3,

        /// <summary>The front car did not move after a green phase or a police wave.</summary>
        GreenNotMoving = 4,
    }

    /// <summary>Animals of the street (W2_DESIGN 5.5; stage 1 simulates cows and dogs).</summary>
    public enum AnimalKind : byte
    {
        Cow = 0,
        Dog = 1,
        Goat = 2,
        Chicken = 3,
        Buffalo = 4,
        Duck = 5,
        Macaque = 6,
    }

    /// <summary>Birds of the flocks (W2_DESIGN 5.6).</summary>
    public enum BirdKind : byte
    {
        Pigeon = 0,
        Crow = 1,
        Kite = 2,
        Myna = 3,
        Sparrow = 4,
        Swallow = 5,
        Egret = 6,
    }

    public enum FlockMode : byte
    {
        Ground = 0,
        Burst = 1,
        Circuit = 2,
        Landing = 3,
    }

    /// <summary>A lane of the <see cref="LaneGraph"/> (stable while its tile stays resident).</summary>
    public readonly struct LaneId : IEquatable<LaneId>
    {
        public readonly int Value;

        public LaneId(int value)
        {
            Value = value;
        }

        public static readonly LaneId None = new LaneId(-1);

        public bool IsValid
        {
            get { return Value >= 0; }
        }

        public bool Equals(LaneId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is LaneId && Equals((LaneId)obj);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "lane " + Value;
        }
    }

    /// <summary>
    /// One drawn vehicle of the traffic snapshot (W2_DESIGN 10.3). Game metres (X east, Z north, Y absolute) as doubles;
    /// heading 0 = north, clockwise (as <see cref="Driving.ArcadeVehicle"/>); pitch nose-up, roll and lean right-side-down
    /// positive. <see cref="Variant"/> indexes <see cref="Driving.VehicleCatalog.All"/>; <see cref="Livery"/> indexes that
    /// entry's colours; <see cref="AgentId"/> is stable for the agent's life (seeds audio, tint and plate number).
    /// </summary>
    public struct AgentPose
    {
        public double X, Z;
        public float Y, HeadingRad, SpeedMps, Lean, Pitch, Roll;
        public VehicleClass Class;
        public ushort Variant;
        public byte Livery, AnimState;
        public int AgentId;
    }

    /// <summary><see cref="AgentPose.AnimState"/> bits.</summary>
    [Flags]
    public enum AgentAnim : byte
    {
        None = 0,
        Braking = 1,
        IndicatorLeft = 2,
        IndicatorRight = 4,

        /// <summary>A two-wheeler's foot is down (stopped).</summary>
        FootDown = 8,

        /// <summary>A bus, micro or tempo dwelling at a stop (doors open).</summary>
        AtStop = 16,

        /// <summary>A far "ghost" agent: lane position only (no lean or pitch).</summary>
        Ghost = 32,

        /// <summary>Hazard: waiting behind a cow or dog.</summary>
        Yielding = 64,
    }

    /// <summary>One drawn pedestrian (W2_DESIGN 10.3). <see cref="Archetype"/> follows the S §3.2 order
    /// (<see cref="PedArchetype"/>); <see cref="ClipId"/> the VAT clip order (<see cref="PedClip"/>).</summary>
    public struct PedPose
    {
        public double X, Z;
        public float Y, HeadingRad, SpeedMps;
        public byte Archetype, Tint, ClipId;
        public float ClipTime;

        /// <summary>0 none, 1 doko, 2 sack, 3 gas cylinder, 4 baby, 5 umbrella.</summary>
        public ushort CarryProp;

        public int AgentId;
    }

    /// <summary>Pedestrian archetypes in the street_life §3.2 order.</summary>
    public enum PedArchetype : byte
    {
        UrbanCasual = 0,
        KurtaSari = 1,
        DauraSuruwal = 2,
        Hakupatasi = 3,
        SchoolKid = 4,
        Porter = 5,
        Vendor = 6,
        Tourist = 7,
        Monk = 8,
        Sadhu = 9,
        Farmer = 10,
        TrafficPolice = 11,
    }

    /// <summary>VAT clips (ASSET_MANIFEST 9.5 order).</summary>
    public enum PedClip : byte
    {
        Idle = 0,
        Walk = 1,
        Carry = 2,
        Sit = 3,
        Chat = 4,
        Cheer = 5,
        Pray = 6,
        Pick = 7,
        Vendor = 8,
        Kite = 9,
        Run = 10,
        Namaste = 11,
    }

    /// <summary>One drawn animal (W2_DESIGN 10.3).</summary>
    public struct AnimalPose
    {
        public double X, Z;
        public float Y, HeadingRad, SpeedMps;
        public AnimalKind Kind;
        public byte Tint, ClipId;
        public float ClipTime;
        public int AgentId;
    }

    /// <summary>Animal clips: 0 lie (chewing), 1 stand, 2 walk, 3 sleep, 4 sit, 5 bark.</summary>
    public enum AnimalClip : byte
    {
        Lie = 0,
        Stand = 1,
        Walk = 2,
        Sleep = 3,
        Sit = 4,
        Bark = 5,
    }

    /// <summary>One bird flock (W2_DESIGN 10.3; flocks arrive in stage 2).</summary>
    public struct FlockState
    {
        public int FlockId;
        public BirdKind Kind;
        public double CX, CZ;
        public float CY;
        public ushort Count;
        public float Phase;
        public FlockMode Mode;
    }

    /// <summary>A horn event of the last step (also raised as <see cref="TrafficSim.Horn"/>).</summary>
    public struct HornEvent
    {
        public int AgentId;
        public HornKind Kind;
        public VehicleClass Class;
        public double X, Z;
    }
}
