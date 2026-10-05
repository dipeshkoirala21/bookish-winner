using System;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Driving
{
    /// <summary>What a seat socket is for (Track D's <c>SeatRole</c> maps one to one).</summary>
    public enum SeatKind : byte
    {
        Driver = 0,
        Pillion = 1,
        Passenger = 2,
        Standing = 3,
    }

    /// <summary>Which side of the vehicle a seat is entered from.</summary>
    public enum EntrySide : byte
    {
        Left = 0,
        Right = 1,
        Rear = 2,
    }

    /// <summary>
    /// One seat of a vehicle (W2_DESIGN 6.2) in the vehicle frame: origin on the ground under the rear axle, +X right,
    /// +Y up, +Z forward (the <see cref="ArcadeVehicle"/> reference point, so a pose places it directly). The player
    /// walks to (<see cref="EntryX"/>, <see cref="EntryZ"/>) on the ground, then the mount clip moves them to the seat
    /// (<see cref="X"/>, <see cref="Y"/>, <see cref="Z"/>). When a wall is within 0.8 m of the entry point, the mirrored
    /// entry (<see cref="AltEntryX"/>) is used where <see cref="HasAlternate"/>.
    /// </summary>
    public struct SeatSocket
    {
        public byte Index;
        public SeatKind Kind;
        public float X, Y, Z;
        public EntrySide Side;
        public float EntryX, EntryZ;
        public bool HasAlternate;
        public float AltEntryX;

        /// <summary>Enter and exit durations (W2_DESIGN 6.2).</summary>
        public float EnterS, ExitS;

        /// <summary>Detection range from the entry point: 2.0 m two-wheelers, 3.0 m cars, 3.5 m bus and truck doors.</summary>
        public float DetectM;
    }

    /// <summary>
    /// Seat sockets per catalogue entry, from the body dimensions and the Nepal rules of W2_DESIGN 6.2: two-wheelers are
    /// mounted from the left; car and taxi drivers enter through the right door and passengers through the left (kerb)
    /// rear door; micros through the left side door; buses at the front-left door; trucks at the right cab door; the
    /// tractor from the left over the axle step; the rickshaw's passengers from the left. Deterministic, allocation per
    /// call (cache per entry on the caller side).
    /// </summary>
    public static class VehicleSeats
    {
        public const float TwoWheelerDetectM = 2.0f, CarDetectM = 3.0f, HeavyDetectM = 3.5f;

        /// <summary>Enter and exit durations by family (W2_DESIGN 6.2).</summary>
        public static void Durations(in VehicleCatalogEntry e, out float enter, out float exit)
        {
            switch (e.Shape)
            {
                case BodyShape.Bicycle:
                    enter = 0.5f;
                    exit = 0.35f;
                    return;
                case BodyShape.Scooter:
                    enter = 0.6f;
                    exit = 0.45f;
                    return;
                case BodyShape.Motorbike:
                case BodyShape.Cruiser:
                    enter = 0.7f;
                    exit = 0.5f;
                    return;
                case BodyShape.Rickshaw:
                    enter = 0.8f;
                    exit = 0.6f;
                    return;
                case BodyShape.Van:
                case BodyShape.Tempo:
                    enter = 1.0f;
                    exit = 0.8f;
                    return;
                case BodyShape.Bus:
                    enter = 1.2f;
                    exit = 0.9f;
                    return;
                case BodyShape.Truck:
                case BodyShape.Tipper:
                case BodyShape.Tanker:
                    enter = 1.4f;
                    exit = 1.0f;
                    return;
                case BodyShape.Tractor:
                    enter = 1.0f;
                    exit = 0.7f;
                    return;
                default:
                    enter = 0.9f;
                    exit = 0.7f;
                    return;
            }
        }

        /// <summary>The seat sockets of catalogue entry <paramref name="variant"/>: the driver first (none for the
        /// rickshaw, whose puller is an NPC), then pillion or passengers, then standing places on buses.</summary>
        public static SeatSocket[] For(int variant)
        {
            return For(VehicleCatalog.At(variant));
        }

        public static SeatSocket[] For(in VehicleCatalogEntry e)
        {
            float enter, exit;
            Durations(e, out enter, out exit);
            float w = e.WidthM, wb = e.WheelbaseM;
            float half = 0.5f * w, step = 0.55f; // entry point this far outside the body side
            switch (e.Shape)
            {
                case BodyShape.Scooter:
                case BodyShape.Motorbike:
                case BodyShape.Cruiser:
                case BodyShape.Bicycle:
                {
                    float seatZ = 0.42f * wb, seatY = e.Shape == BodyShape.Bicycle ? 0.95f : 0.80f;
                    var s = new SeatSocket[e.Seats >= 2 ? 2 : 1];
                    s[0] = Socket(0, SeatKind.Driver, 0f, seatY, seatZ, EntrySide.Left, -(half + step), seatZ, true, enter, exit,
                                  TwoWheelerDetectM);
                    if (s.Length > 1)
                        s[1] = Socket(1, SeatKind.Pillion, 0f, seatY + 0.05f, seatZ - 0.45f, EntrySide.Left, -(half + step), seatZ - 0.45f,
                                      true, enter, exit, TwoWheelerDetectM);
                    return s;
                }
                case BodyShape.Rickshaw:
                {
                    // The puller sits in front over the single front wheel; two passengers on the rear bench.
                    var s = new SeatSocket[2];
                    for (int i = 0; i < 2; i++)
                        s[i] = Socket((byte)i, SeatKind.Passenger, (i == 0 ? -0.25f : 0.25f), 0.75f, 0.1f, EntrySide.Left,
                                      -(half + step), 0.1f, false, enter, exit, CarDetectM);
                    return s;
                }
                case BodyShape.Tractor:
                {
                    var s = new SeatSocket[1];
                    s[0] = Socket(0, SeatKind.Driver, 0f, 1.35f, 0.15f, EntrySide.Left, -(half + step), 0.15f, true, enter, exit, CarDetectM);
                    return s;
                }
                case BodyShape.Tempo:
                {
                    // Driver centred at the front; passengers on two facing rear benches, boarding by the rear step.
                    int pax = Math.Max(0, e.Seats - 1);
                    var s = new SeatSocket[1 + pax];
                    float fz = wb + 0.15f;
                    s[0] = Socket(0, SeatKind.Driver, 0f, 0.85f, fz - 0.55f, EntrySide.Right, half + step, fz - 0.55f, true, enter, exit,
                                  CarDetectM);
                    for (int i = 0; i < pax; i++)
                    {
                        int row = i / 2;
                        float x = (i % 2 == 0 ? -1f : 1f) * (half - 0.3f);
                        s[1 + i] = Socket((byte)(1 + i), SeatKind.Passenger, x, 0.75f, 0.9f - row * 0.45f, EntrySide.Rear, 0f,
                                          -(e.RearOverhangM + step), false, enter, exit, CarDetectM);
                    }
                    return s;
                }
                case BodyShape.Bus:
                {
                    int seats = Math.Max(1, e.Seats);
                    int standing = Math.Max(0, seats / 3);
                    var s = new SeatSocket[seats + standing];
                    float front = wb + Math.Max(0.5f, e.LengthM - wb - e.RearOverhangM) - 0.9f;
                    float doorZ = front - 0.2f, seatY = 1.25f;
                    s[0] = Socket(0, SeatKind.Driver, half - 0.55f, seatY, front - 0.3f, EntrySide.Left, -(half + step), doorZ, false, enter,
                                  exit, HeavyDetectM);
                    // Passenger rows of four (2 + 2) back from behind the door, 0.8 m pitch.
                    for (int i = 1; i < seats; i++)
                    {
                        int row = (i - 1) / 4, col = (i - 1) % 4;
                        float x = (col < 2 ? -1f : 1f) * (col % 2 == 0 ? half - 0.35f : half - 0.8f);
                        s[i] = Socket((byte)i, SeatKind.Passenger, x, seatY, front - 1.6f - row * 0.8f, EntrySide.Left, -(half + step), doorZ,
                                      false, enter, exit, HeavyDetectM);
                    }
                    for (int k = 0; k < standing; k++)
                        s[seats + k] = Socket((byte)(seats + k), SeatKind.Standing, (k % 2 == 0 ? -0.2f : 0.2f), 0.75f,
                                              front - 1.6f - (k / 2) * 0.7f, EntrySide.Left, -(half + step), doorZ, false, enter, exit,
                                              HeavyDetectM);
                    return s;
                }
                case BodyShape.Truck:
                case BodyShape.Tipper:
                case BodyShape.Tanker:
                {
                    int n = Math.Max(1, e.Seats);
                    var s = new SeatSocket[n];
                    float front = wb + Math.Max(0.5f, e.LengthM - wb - e.RearOverhangM);
                    float cabZ = front - 1.1f;
                    s[0] = Socket(0, SeatKind.Driver, half - 0.55f, 1.55f, cabZ, EntrySide.Right, half + step, cabZ, false, enter, exit,
                                  HeavyDetectM);
                    for (int i = 1; i < n; i++)
                        s[i] = Socket((byte)i, SeatKind.Passenger, half - 0.55f - i * 0.65f, 1.55f, cabZ, EntrySide.Left, -(half + step),
                                      cabZ, false, enter, exit, HeavyDetectM);
                    return s;
                }
                default:
                {
                    // Cars, taxis, SUVs, pickups and vans: right-hand drive.
                    int n = Math.Max(1, e.Seats);
                    var s = new SeatSocket[n];
                    bool van = e.Shape == BodyShape.Van;
                    float frontRowZ = van ? wb + 0.2f : wb * 0.5f + 0.1f;
                    float seatY = van ? 1.0f : 0.65f;
                    s[0] = Socket(0, SeatKind.Driver, 0.25f * w, seatY, frontRowZ, EntrySide.Right, half + step, frontRowZ, false, enter, exit,
                                  CarDetectM);
                    for (int i = 1; i < n; i++)
                    {
                        // Front passenger beside the driver, then rear rows of three, entering by the kerb (left) side.
                        float x, z;
                        if (i == 1)
                        {
                            x = -0.25f * w;
                            z = frontRowZ;
                        }
                        else
                        {
                            int row = (i - 2) / 3, col = (i - 2) % 3;
                            x = (col - 1) * 0.3f * w;
                            z = frontRowZ - 0.85f * (row + 1);
                        }
                        float doorZ = i == 1 ? frontRowZ : van ? frontRowZ - 0.85f : z;
                        s[i] = Socket((byte)i, SeatKind.Passenger, x, seatY, z, EntrySide.Left, -(half + step), doorZ, false, enter, exit,
                                      CarDetectM);
                    }
                    return s;
                }
            }
        }

        private static SeatSocket Socket(byte index, SeatKind kind, float x, float y, float z, EntrySide side, float ex, float ez, bool alt,
                                         float enter, float exit, float detect)
        {
            return new SeatSocket
            {
                Index = index, Kind = kind, X = x, Y = y, Z = z, Side = side, EntryX = ex, EntryZ = ez, HasAlternate = alt,
                AltEntryX = alt ? -ex : ex, EnterS = enter, ExitS = exit, DetectM = detect,
            };
        }

        /// <summary>A seat socket's world position for a vehicle at (x, z, y) with heading <paramref name="headingRad"/>
        /// (0 north, clockwise): the frame's +X (right) is (cos h, −sin h) and +Z (forward) is (sin h, cos h).</summary>
        public static void ToWorld(double x, double z, float y, float headingRad, float lx, float ly, float lz,
                                   out double wx, out double wz, out float wy)
        {
            double s = Math.Sin(headingRad), c = Math.Cos(headingRad);
            wx = x + lx * c + lz * s;
            wz = z - lx * s + lz * c;
            wy = y + ly;
        }

        /// <summary>The class a traffic agent of this entry belongs to (for D's MountPlan).</summary>
        public static VehicleClass ClassOf(int variant)
        {
            return VehicleCatalog.At(variant).TrafficClass;
        }
    }
}
