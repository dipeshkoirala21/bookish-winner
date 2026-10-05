using System;
using Ghumante.Core.Driving;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Characters
{
    /// <summary>Where a vehicle stands: its reference point (ground under the rear axle) and heading (0 north, clockwise),
    /// game metres. The seat sockets of <see cref="VehicleSeats"/> are relative to it.</summary>
    public struct VehicleFrame
    {
        public double X, Z;
        public float Y, HeadingRad;

        public VehicleFrame(double x, double z, float y, float headingRad)
        {
            X = x;
            Z = z;
            Y = y;
            HeadingRad = headingRad;
        }

        /// <summary>A point of the vehicle frame (+X right, +Z forward) in the world.</summary>
        public void ToWorld(float lx, float lz, out double wx, out double wz)
        {
            float wy;
            VehicleSeats.ToWorld(X, Z, Y, HeadingRad, lx, 0f, lz, out wx, out wz, out wy);
        }

        /// <summary>A world point in the vehicle frame.</summary>
        public void ToLocal(double wx, double wz, out float lx, out float lz)
        {
            double dx = wx - X, dz = wz - Z;
            double s = Math.Sin(HeadingRad), c = Math.Cos(HeadingRad);
            lx = (float)(dx * c - dz * s);
            lz = (float)(dx * s + dz * c);
        }
    }

    /// <summary>Optional companion of the ground query: is a standing body blocked at (x, z)? (A wall, a no-climb
    /// structure, a step too tall.) <see cref="TileGroundQuery.IsBlocked"/> is the game's.</summary>
    public interface IFootBlocker
    {
        bool IsBlocked(double x, double z, float feetY);
    }

    /// <summary>
    /// How the player gets into a seat (W2_DESIGN 6.2, contract 10.3): which side (left-hand traffic, right-hand drive:
    /// two-wheelers from the left, car drivers through the right door and passengers through the kerb-side left rear
    /// door, buses at the front-left door, trucks at the right cab door; the other side when a wall is within 0.8 m), the
    /// walk there (≤ 4 m at 2.2 m/s) and the enter and exit times (bicycle 0.5 / 0.35 s … truck 1.4 / 1.0 s).
    /// </summary>
    public sealed class MountPlan
    {
        public const float MaxPathM = 4f, ApproachMps = 2.2f, WallClearM = 0.8f, BlockedAbortS = 0.5f;

        public VehicleClass Class;
        public SeatSocket Seat;
        public EntrySide Side;

        /// <summary>The mirrored entry was used (a wall on the default side).</summary>
        public bool Alternate;

        /// <summary>Where the player walks to (world, game metres).</summary>
        public double EntryX, EntryZ;

        public float PathM;

        /// <summary>Walking time to the entry point.</summary>
        public float WalkS;

        /// <summary>The enter clip, from the seat socket (W2_DESIGN 6.2).</summary>
        public float EnterS;

        public float ExitS;

        /// <summary>From pressing Hop on to having control.</summary>
        public float TotalS
        {
            get { return WalkS + EnterS; }
        }

        /// <summary>
        /// Plans a mount of seat <paramref name="s"/> on a vehicle at <paramref name="v"/> for a player at
        /// (<paramref name="px"/>, <paramref name="pz"/>). Fails when the entry point is more than 4 m away or both sides
        /// are blocked. <paramref name="g"/> may also be an <see cref="IFootBlocker"/> (walls).
        /// </summary>
        public static bool TryPlan(VehicleClass c, in SeatSocket s, in VehicleFrame v, double px, double pz, IGroundQuery g, out MountPlan p)
        {
            p = null;
            double ex, ez;
            bool alternate = false;
            v.ToWorld(s.EntryX, s.EntryZ, out ex, out ez);
            if (!Clear(v, s.EntryX, s.EntryZ, g))
            {
                if (!s.HasAlternate) return false;
                if (!Clear(v, s.AltEntryX, s.EntryZ, g)) return false;
                v.ToWorld(s.AltEntryX, s.EntryZ, out ex, out ez);
                alternate = true;
            }
            double dx = ex - px, dz = ez - pz;
            float path = (float)Math.Sqrt(dx * dx + dz * dz);
            if (path > MaxPathM) return false;
            EntrySide side = s.Side;
            if (alternate) side = side == EntrySide.Left ? EntrySide.Right : side == EntrySide.Right ? EntrySide.Left : side;
            p = new MountPlan
            {
                Class = c, Seat = s, Side = side, Alternate = alternate, EntryX = ex, EntryZ = ez, PathM = path, WalkS = path / ApproachMps,
                EnterS = s.EnterS, ExitS = s.ExitS,
            };
            return true;
        }

        /// <summary>The contract's shape: plan for a vehicle at the origin facing north (tests and tools).</summary>
        public static bool TryPlan(VehicleClass c, in SeatSocket s, double px, double pz, IGroundQuery g, out MountPlan p)
        {
            return TryPlan(c, s, new VehicleFrame(0, 0, 0f, 0f), px, pz, g, out p);
        }

        /// <summary>An entry point is usable when there is ground and no wall within 0.8 m outward of it.</summary>
        private static bool Clear(in VehicleFrame v, float lx, float lz, IGroundQuery g)
        {
            if (g == null) return true;
            double x, z;
            v.ToWorld(lx, lz, out x, out z);
            if (!Standable(x, z, v.Y, g)) return false;
            float outward = lx >= 0f ? 1f : -1f;
            if (Math.Abs(lx) < 0.05f) return true; // rear steps: the ground test is enough
            v.ToWorld(lx + outward * (WallClearM - 0.3f), lz, out x, out z);
            return Standable(x, z, v.Y, g);
        }

        /// <summary>Ground exists at (x, z) near <paramref name="nearY"/> and no structure blocks a body there.</summary>
        public static bool Standable(double x, double z, float nearY, IGroundQuery g)
        {
            GroundSample s;
            if (!g.TrySample(x, z, out s)) return false;
            if (Math.Abs(s.Height - nearY) > 1.0f) return false;
            var blocker = g as IFootBlocker;
            if (blocker != null && blocker.IsBlocked(x, z, s.Height)) return false;
            var tiles = g as TileGroundQuery;
            if (tiles != null && tiles.IsBlocked(x, z, s.Height)) return false;
            return true;
        }
    }

    /// <summary>
    /// Where the player stands after leaving a seat (W2_DESIGN 6.2, V8): the seat's exit socket snapped to the ground,
    /// else the opposite side, else behind the vehicle, else a ring search out to 2.5 m; never inside the vehicle's body,
    /// a wall or a structure, never more than 1 m above or below the vehicle's ground.
    /// </summary>
    public static class ExitPlacement
    {
        public const float SearchRadiusM = 2.5f, BodyMarginM = 0.25f;

        /// <summary>Finds an exit point; false only when nothing within 2.5 m is standable (the caller then keeps the
        /// player seated and says "No room to hop off").</summary>
        public static bool Find(in VehicleFrame v, in VehicleCatalogEntry e, in SeatSocket s, IGroundQuery g, out double x, out double z, out float y)
        {
            VehicleMesher.Dims d = VehicleMesher.DimsOf(e);
            float side = s.EntryX;
            if (Math.Abs(side) < 0.05f) side = -(d.Half + 0.55f);
            // 1: the socket's side; 2: the other side; 3: behind; 4: a ring around the vehicle.
            if (Try(v, d, side, s.EntryZ, g, out x, out z, out y)) return true;
            if (Try(v, d, -side, s.EntryZ, g, out x, out z, out y)) return true;
            if (Try(v, d, 0f, -(d.Rear + 0.7f), g, out x, out z, out y)) return true;
            double cx, cz;
            float midZ = 0.5f * (d.Wheelbase + d.Front - d.Rear);
            v.ToWorld(0f, midZ, out cx, out cz);
            for (int ring = 1; ring <= 5; ring++)
            {
                float r = Math.Min(SearchRadiusM, 0.5f * ring) + 0.5f * Math.Max(d.Half * 2f, d.Length * 0.5f);
                for (int k = 0; k < 12; k++)
                {
                    double a = k * Math.PI / 6.0;
                    float lx = (float)(Math.Sin(a) * r), lz = midZ + (float)(Math.Cos(a) * r);
                    if (Try(v, d, lx, lz, g, out x, out z, out y)) return true;
                }
            }
            x = v.X;
            z = v.Z;
            y = v.Y;
            return false;
        }

        private static bool Try(in VehicleFrame v, in VehicleMesher.Dims d, float lx, float lz, IGroundQuery g, out double x, out double z, out float y)
        {
            v.ToWorld(lx, lz, out x, out z);
            y = v.Y;
            if (InsideBody(d, lx, lz)) return false;
            if (g == null) return true;
            if (!MountPlan.Standable(x, z, v.Y, g)) return false;
            GroundSample s;
            g.TrySample(x, z, out s);
            y = s.Height;
            return true;
        }

        /// <summary>True when a vehicle-frame point lies inside the body plus a 0.25 m margin.</summary>
        public static bool InsideBody(in VehicleMesher.Dims d, float lx, float lz)
        {
            return Math.Abs(lx) < d.Half + BodyMarginM && lz > -d.Rear - BodyMarginM && lz < d.Wheelbase + d.Front + BodyMarginM;
        }
    }

    /// <summary>
    /// Finds the seat the Hop on prompt offers (W2_DESIGN 6.2): sockets in a 120° cone in front of the player (or the
    /// camera in portrait) within 2.0 m (two-wheelers), 3.0 m (cars) or 3.5 m (bus and truck doors); score
    /// 0.6·(1 − d/range) + 0.4·cos(angle); the current pick keeps 0.3 m of hysteresis so the prompt never flickers. Drive
    /// and ride-as-passenger candidates are tracked separately (the second, smaller button). Call <see cref="Begin"/>,
    /// <see cref="Consider"/> per nearby vehicle, then read <see cref="Driver"/> and <see cref="Passenger"/>. No
    /// allocation per frame.
    /// </summary>
    public sealed class MountDetector
    {
        public const float ConeHalfDeg = 60f, HysteresisM = 0.3f;

        /// <summary>One candidate seat.</summary>
        public struct Candidate
        {
            public bool Valid;

            /// <summary>The caller's id for the vehicle (garage slot, parked id or traffic agent id).</summary>
            public long VehicleId;

            public int Variant;
            public byte SeatIndex;
            public SeatKind Kind;
            public float Score, DistanceM;
        }

        private double _px, _pz;
        private float _fx, _fz;
        private Candidate _driver, _passenger;
        private long _keepDriver = long.MinValue, _keepPassenger = long.MinValue;

        public Candidate Driver
        {
            get { return _driver; }
        }

        public Candidate Passenger
        {
            get { return _passenger; }
        }

        /// <summary>Starts a frame for a player at (x, z) facing <paramref name="facingRad"/> (0 north, clockwise).</summary>
        public void Begin(double x, double z, float facingRad)
        {
            _px = x;
            _pz = z;
            _fx = (float)Math.Sin(facingRad);
            _fz = (float)Math.Cos(facingRad);
            _keepDriver = _driver.Valid ? _driver.VehicleId : long.MinValue;
            _keepPassenger = _passenger.Valid ? _passenger.VehicleId : long.MinValue;
            _driver = default(Candidate);
            _passenger = default(Candidate);
        }

        /// <summary>Clears the picks (seated, or the prompt is not allowed).</summary>
        public void Clear()
        {
            _driver = default(Candidate);
            _passenger = default(Candidate);
        }

        /// <summary>
        /// Scores every seat of a vehicle: drivable seats when <paramref name="mayDrive"/> (own garage, community fleet),
        /// passenger seats when <paramref name="mayRide"/> (taxis, buses, micros, tempos and rickshaws that have stopped).
        /// </summary>
        public void Consider(long vehicleId, int variant, in VehicleFrame v, SeatSocket[] seats, bool mayDrive, bool mayRide)
        {
            if (seats == null) return;
            for (int i = 0; i < seats.Length; i++)
            {
                SeatSocket s = seats[i];
                bool driverSeat = s.Kind == SeatKind.Driver;
                if (driverSeat ? !mayDrive : !mayRide) continue;
                if (s.Kind == SeatKind.Standing) continue;
                double ex, ez;
                v.ToWorld(s.EntryX, s.EntryZ, out ex, out ez);
                float score, dist;
                bool sticky = driverSeat ? vehicleId == _keepDriver : vehicleId == _keepPassenger;
                if (!Score(ex, ez, s.DetectM + (sticky ? HysteresisM : 0f), out score, out dist))
                {
                    if (!s.HasAlternate) continue;
                    v.ToWorld(s.AltEntryX, s.EntryZ, out ex, out ez);
                    if (!Score(ex, ez, s.DetectM + (sticky ? HysteresisM : 0f), out score, out dist)) continue;
                }
                if (sticky) score += 0.05f;
                var c = new Candidate { Valid = true, VehicleId = vehicleId, Variant = variant, SeatIndex = s.Index, Kind = s.Kind, Score = score, DistanceM = dist };
                if (driverSeat)
                {
                    if (!_driver.Valid || c.Score > _driver.Score) _driver = c;
                }
                else if (!_passenger.Valid || c.Score > _passenger.Score)
                {
                    _passenger = c;
                }
            }
        }

        /// <summary>The score of an entry point: in range and inside the 120° cone.</summary>
        public bool Score(double ex, double ez, float range, out float score, out float distance)
        {
            double dx = ex - _px, dz = ez - _pz;
            distance = (float)Math.Sqrt(dx * dx + dz * dz);
            score = 0f;
            if (distance > range) return false;
            float cos = distance < 0.25f ? 1f : (float)((dx * _fx + dz * _fz) / distance);
            if (cos < (float)Math.Cos(ConeHalfDeg * Math.PI / 180.0)) return false;
            score = 0.6f * (1f - distance / range) + 0.4f * cos;
            return true;
        }
    }
}
