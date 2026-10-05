using System;
using Ghumante.Core.Driving;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Characters
{
    /// <summary>Where a passenger ride is (W2_DESIGN 5.3 and 6.2). Append-only.</summary>
    public enum RideState : byte
    {
        None = 0,

        /// <summary>A hailed taxi is on its way (it pulls over within 30 m).</summary>
        Hailing = 1,

        /// <summary>Riding along.</summary>
        Riding = 2,

        /// <summary>The bell was pressed: get off at the next stop (bus) or as soon as it pulls over (taxi).</summary>
        StopRequested = 3,

        /// <summary>The vehicle has stopped for the player: hop off now.</summary>
        Alight = 4,
    }

    /// <summary>
    /// Riding as a passenger (W2-O5: moving NPC vehicles can only be ridden as a passenger; nobody is ever pulled out):
    /// taxis are hailed (the nearest taxi within 150 m pulls over within 30 m), buses, micros and tempos are boarded at
    /// stops (<see cref="AgentAnim.AtStop"/>, doors open), rickshaws when they stand. The bell asks for the next stop.
    /// Taxi fares are 10 + 2 coins per km (free when the player has fewer coins: the relaxed default, P §14 Q3).
    /// Engine-free: the runtime feeds the traffic snapshot and acts on <see cref="State"/>.
    /// </summary>
    public sealed class PassengerRide
    {
        public const float HailRadiusM = 150f, PullOverM = 30f, BoardRadiusM = 3.5f, StoppedMps = 0.6f;
        public const float FareBase = 10f, FarePerKm = 2f;

        /// <summary>A hail gives up after this long without a taxi stopping.</summary>
        public const float HailTimeoutS = 45f;

        public RideState State { get; private set; }
        public int AgentId { get; private set; } = -1;
        public VehicleClass Class { get; private set; }
        public int Variant { get; private set; }
        public byte SeatIndex { get; private set; }

        /// <summary>Distance ridden this trip, metres.</summary>
        public double DistanceM { get; private set; }

        /// <summary>Stops the vehicle has served since boarding.</summary>
        public int StopsPassed { get; private set; }

        private double _lastX, _lastZ;
        private bool _wasAtStop = true;
        private float _hailS;

        /// <summary>Classes a passenger can ride.</summary>
        public static bool IsRideable(VehicleClass c)
        {
            switch (c)
            {
                case VehicleClass.Taxi:
                case VehicleClass.Bus:
                case VehicleClass.Minibus:
                case VehicleClass.Microbus:
                case VehicleClass.Tempo:
                case VehicleClass.Rickshaw:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Route vehicles board only at stops; taxis and rickshaws whenever they stand still.</summary>
        public static bool IsBoardable(in AgentPose a)
        {
            if (!IsRideable(a.Class) || Math.Abs(a.SpeedMps) > StoppedMps) return false;
            if (a.Class == VehicleClass.Taxi || a.Class == VehicleClass.Rickshaw) return true;
            return (a.AnimState & (byte)AgentAnim.AtStop) != 0;
        }

        /// <summary>The nearest taxi within <see cref="HailRadiusM"/> of (x, z), or −1.</summary>
        public static int NearestTaxi(AgentPose[] poses, int count, double x, double z)
        {
            int best = -1;
            double bestD = HailRadiusM * HailRadiusM;
            for (int i = 0; i < count && poses != null && i < poses.Length; i++)
            {
                if (poses[i].Class != VehicleClass.Taxi || (poses[i].AnimState & (byte)AgentAnim.Ghost) != 0) continue;
                double dx = poses[i].X - x, dz = poses[i].Z - z, d = dx * dx + dz * dz;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Index of the agent with <paramref name="agentId"/> in a snapshot, or −1.</summary>
        public static int Find(AgentPose[] poses, int count, int agentId)
        {
            for (int i = 0; i < count && poses != null && i < poses.Length; i++)
                if (poses[i].AgentId == agentId) return i;
            return -1;
        }

        /// <summary>Starts waiting for a hailed taxi.</summary>
        public void Hail(in AgentPose taxi)
        {
            AgentId = taxi.AgentId;
            Class = taxi.Class;
            Variant = taxi.Variant;
            State = RideState.Hailing;
            _hailS = 0f;
        }

        /// <summary>The player is aboard (after the enter clip).</summary>
        public void Board(in AgentPose a, byte seat)
        {
            AgentId = a.AgentId;
            Class = a.Class;
            Variant = a.Variant;
            SeatIndex = seat;
            State = RideState.Riding;
            DistanceM = 0;
            StopsPassed = 0;
            _lastX = a.X;
            _lastZ = a.Z;
            _wasAtStop = true;
        }

        /// <summary>The bell (or Hop off at speed): get off at the next stop.</summary>
        public void RequestStop()
        {
            if (State == RideState.Riding) State = RideState.StopRequested;
        }

        public void End()
        {
            State = RideState.None;
            AgentId = -1;
        }

        /// <summary>
        /// Advances with this frame's pose of the vehicle (<paramref name="found"/> false when it left the simulation:
        /// the ride ends where it is). Returns the state.
        /// </summary>
        public RideState Update(float dt, bool found, in AgentPose a, double playerX, double playerZ)
        {
            switch (State)
            {
                case RideState.Hailing:
                    _hailS += Math.Max(0f, dt);
                    if (!found || _hailS > HailTimeoutS) End();
                    return State;
                case RideState.Riding:
                case RideState.StopRequested:
                {
                    if (!found)
                    {
                        State = RideState.Alight;
                        return State;
                    }
                    double dx = a.X - _lastX, dz = a.Z - _lastZ;
                    double step = Math.Sqrt(dx * dx + dz * dz);
                    if (step < 50.0) DistanceM += step;
                    _lastX = a.X;
                    _lastZ = a.Z;
                    bool atStop = (a.AnimState & (byte)AgentAnim.AtStop) != 0;
                    if (atStop && !_wasAtStop) StopsPassed++;
                    _wasAtStop = atStop;
                    bool stopped = Math.Abs(a.SpeedMps) <= StoppedMps;
                    if (State == RideState.StopRequested)
                    {
                        bool routeVehicle = a.Class == VehicleClass.Bus || a.Class == VehicleClass.Minibus || a.Class == VehicleClass.Microbus ||
                                            a.Class == VehicleClass.Tempo;
                        if (stopped && (!routeVehicle || atStop)) State = RideState.Alight;
                    }
                    return State;
                }
                default:
                    return State;
            }
        }

        /// <summary>The taxi fare for this trip in coins (0 for route vehicles, which are free in the game).</summary>
        public int Fare
        {
            get { return Class == VehicleClass.Taxi ? FareFor(DistanceM) : 0; }
        }

        public static int FareFor(double metres)
        {
            return (int)Math.Round(FareBase + FarePerKm * Math.Max(0.0, metres) / 1000.0);
        }

        /// <summary>
        /// Where a hailed taxi (or a taxi asked to pull over) should stop: a point on its heading <paramref name="aheadM"/>
        /// ahead; the runtime places a Player obstacle there so the traffic sim brakes it (IDM) and removes it on boarding.
        /// </summary>
        public static void PullOverPoint(in AgentPose a, float aheadM, out double x, out double z)
        {
            x = a.X + Math.Sin(a.HeadingRad) * aheadM;
            z = a.Z + Math.Cos(a.HeadingRad) * aheadM;
        }
    }
}
