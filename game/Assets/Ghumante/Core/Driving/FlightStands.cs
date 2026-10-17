using System;
using System.Collections.Generic;
using Ghumante.Core.Aviation;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Driving
{
    /// <summary>One parked aircraft the player can board: where it stands now (game metres), which way it points and how
    /// it is painted. A stand the player flew away from is empty until the aircraft is parked again (anywhere).</summary>
    public struct FlightStand
    {
        /// <summary>Index in <see cref="FlightStands.All"/> (stable for the session).</summary>
        public int Index;

        /// <summary>The real stand name ("D2", "Gate 10", "Helipad"); never shown with an airline.</summary>
        public string Ref;

        /// <summary>OSM element the stand comes from (provenance; ODbL).</summary>
        public string Osm;

        public FlightCraft Craft;

        /// <summary>Generic livery index (<see cref="FlightLiveries"/>).</summary>
        public byte Livery;

        /// <summary>Where the aircraft stands now (centre of the fuselage on the ground) and its heading (0 north, cw).</summary>
        public double X, Z;

        public float HeadingRad;

        /// <summary>The player is flying it (nothing stands here now).</summary>
        public bool Taken;
    }

    /// <summary>
    /// The aircraft parked at Tribhuvan International Airport for the player (docs/research/w2/ref_flight.md §3): the
    /// domestic apron's real stands D1–D7 (OSM <c>aeroway=parking_position</c>) with ATR 72-class, Dash 8-class,
    /// Jetstream-class and Twin Otter-class turboprops, helicopters on the small stands D15–D16 and the charter helipad west
    /// of the runway, and an ATR 72-class turboprop at the international terminal's gate 10 (domestic turboprops also fly
    /// the Varanasi route from there). Each aircraft stands on its stand line with its nose towards the taxi lane, clear
    /// of the points where the ambient traffic parks and lifts off (<see cref="AirTrafficSim"/>). Boarding is offered
    /// within reach of the aircraft (<see cref="TryBoardable"/>); the pilot climbs out at the main door
    /// (<see cref="ExitPoint"/>). Other airports get two generic stands beside their apron. Engine-free.
    /// </summary>
    public sealed class FlightStands
    {
        /// <summary>How close to the fuselage (beyond its radius) the explorer must be to board.</summary>
        public const float BoardReachM = 3.5f;

        /// <summary>Clearance kept from the ambient traffic's apron parking and helicopter lift-off points.</summary>
        public const double AmbientClearanceM = 30.0;

        private readonly List<FlightStand> _all = new List<FlightStand>();

        private FlightStands()
        {
        }

        public IReadOnlyList<FlightStand> All
        {
            get { return _all; }
        }

        public int Count
        {
            get { return _all.Count; }
        }

        public FlightStand this[int i]
        {
            get { return _all[i]; }
        }

        // A stand line from OSM (lon/lat of its two ends; the aircraft points from the first towards the second), the
        // craft and where along the line its centre sits (metres from the first end).
        private struct Def
        {
            public string Ref, Osm;
            public double Lon0, Lat0, Lon1, Lat1;
            public FlightCraft Craft;
            public byte Livery;
            public double AtM;
        }

        // Domestic apron (w736369694): stand lines drawn from the terminal side (west) to the taxi lane (east); D1 is drawn
        // the other way and is given here west to east. D15–D17 continue D4–D6 towards the taxi lane.
        private static readonly Def[] Tia =
        {
            new Def { Ref = "D1", Osm = "w220704665", Lon0 = 85.360135, Lat0 = 27.704458, Lon1 = 85.360474, Lat1 = 27.70469, Craft = FlightCraft.Stol, Livery = 0, AtM = 14 },
            new Def { Ref = "D2", Osm = "w909580836", Lon0 = 85.360017, Lat0 = 27.704781, Lon1 = 85.360559, Lat1 = 27.705073, Craft = FlightCraft.Turboprop, Livery = 0, AtM = 22 },
            new Def { Ref = "D3", Osm = "w909580837", Lon0 = 85.359903, Lat0 = 27.705094, Lon1 = 85.360641, Lat1 = 27.705305, Craft = FlightCraft.TurbopropStretch, Livery = 1, AtM = 25 },
            new Def { Ref = "D4", Osm = "w909580838", Lon0 = 85.359786, Lat0 = 27.705415, Lon1 = 85.360534, Lat1 = 27.705587, Craft = FlightCraft.Turboprop, Livery = 2, AtM = 22 },
            new Def { Ref = "D5", Osm = "w1550068566", Lon0 = 85.359677, Lat0 = 27.705713, Lon1 = 85.360422, Lat1 = 27.705879, Craft = FlightCraft.Commuter, Livery = 3, AtM = 50 },
            new Def { Ref = "D6", Osm = "w1550068567", Lon0 = 85.359571, Lat0 = 27.706004, Lon1 = 85.360308, Lat1 = 27.706177, Craft = FlightCraft.Turboprop, Livery = 4, AtM = 50 },
            new Def { Ref = "D7", Osm = "w909580839", Lon0 = 85.359484, Lat0 = 27.706242, Lon1 = 85.360196, Lat1 = 27.70647, Craft = FlightCraft.Stol, Livery = 5, AtM = 12 },
            new Def { Ref = "D16", Osm = "w1550068572", Lon0 = 85.360422, Lat0 = 27.705879, Lon1 = 85.360829, Lat1 = 27.705997, Craft = FlightCraft.Helicopter, Livery = 0, AtM = 26 },
            new Def { Ref = "D17", Osm = "w1550068571", Lon0 = 85.360534, Lat0 = 27.705587, Lon1 = 85.360839, Lat1 = 27.705672, Craft = FlightCraft.Helicopter, Livery = 1, AtM = 22 },
            // International apron (w736369689): a remote stand in front of gate 10, nose out towards the runway.
            new Def { Ref = "Gate 10", Osm = "n9593444182", Lon0 = 85.358497, Lat0 = 27.701709, Lon1 = 85.359157, Lat1 = 27.701476, Craft = FlightCraft.Turboprop, Livery = 5, AtM = 42 },
            // Charter helipad outside the fence, west of threshold 02 (n10121417304).
            new Def { Ref = "Helipad", Osm = "n10121417304", Lon0 = 85.345946, Lat0 = 27.684082, Lon1 = 85.346250, Lat1 = 27.684335, Craft = FlightCraft.Helicopter, Livery = 2, AtM = 0 },
        };

        /// <summary>The stands of an airport (TIA's real ones for VNKT, two generic ones elsewhere); empty without one.</summary>
        public static FlightStands For(AviationConfig airport)
        {
            var s = new FlightStands();
            if (airport == null) return s;
            if (airport.Icao == "VNKT")
            {
                for (int i = 0; i < Tia.Length; i++)
                {
                    Def d = Tia[i];
                    double x0, z0, x1, z1;
                    WorldFrame.LonLatToGame(d.Lon0, d.Lat0, out x0, out z0);
                    WorldFrame.LonLatToGame(d.Lon1, d.Lat1, out x1, out z1);
                    double dx = x1 - x0, dz = z1 - z0, len = Math.Sqrt(dx * dx + dz * dz);
                    if (len < 1e-3) continue;
                    s.Add(d.Ref, d.Osm, d.Craft, d.Livery, x0 + dx / len * d.AtM, z0 + dz / len * d.AtM, (float)Math.Atan2(dx, dz));
                }
                return s;
            }
            // Elsewhere: a turboprop and a helicopter beside the domestic apron point, pointing along the runway.
            double hx = airport.Threshold20.X - airport.Threshold02.X, hz = airport.Threshold20.Z - airport.Threshold02.Z;
            float heading = (float)Math.Atan2(hx, hz);
            double rx = Math.Cos(heading), rz = -Math.Sin(heading);
            s.Add("A1", "", FlightCraft.Turboprop, 0, airport.DomesticApronX - rx * 60, airport.DomesticApronZ - rz * 60, heading);
            s.Add("H1", "", FlightCraft.Helicopter, 0, airport.DomesticApronX - rx * 100, airport.DomesticApronZ - rz * 100, heading);
            return s;
        }

        private void Add(string reference, string osm, FlightCraft craft, byte livery, double x, double z, float heading)
        {
            _all.Add(new FlightStand
            {
                Index = _all.Count, Ref = reference, Osm = osm, Craft = craft, Livery = livery, X = x, Z = z,
                HeadingRad = ArcadeVehicle.WrapAngle(heading),
            });
        }

        /// <summary>The player took the aircraft of stand <paramref name="index"/> (it is no longer parked).</summary>
        public void Take(int index)
        {
            FlightStand s = _all[index];
            s.Taken = true;
            _all[index] = s;
        }

        /// <summary>The player parked the aircraft of stand <paramref name="index"/> at (x, z): it stays there.</summary>
        public void Park(int index, double x, double z, float headingRad)
        {
            FlightStand s = _all[index];
            s.Taken = false;
            s.X = x;
            s.Z = z;
            s.HeadingRad = ArcadeVehicle.WrapAngle(headingRad);
            _all[index] = s;
        }

        /// <summary>
        /// The parked aircraft the explorer at (x, z) can board: the one whose fuselage (a segment from tail to nose of
        /// its spec's radius) is nearest, within <see cref="BoardReachM"/> of its skin. False when none is in reach.
        /// </summary>
        public bool TryBoardable(double x, double z, out FlightStand stand, out float distanceM)
        {
            stand = default(FlightStand);
            distanceM = float.PositiveInfinity;
            int best = -1;
            for (int i = 0; i < _all.Count; i++)
            {
                FlightStand s = _all[i];
                if (s.Taken) continue;
                FlightSpec spec = FlightSpec.For(s.Craft);
                float d = SkinDistance(spec, s.X, s.Z, s.HeadingRad, x, z);
                if (d < distanceM)
                {
                    distanceM = d;
                    best = i;
                }
            }
            if (best < 0 || distanceM > BoardReachM) return false;
            stand = _all[best];
            return true;
        }

        /// <summary>Distance from (px, pz) to the skin of a fuselage (negative inside it).</summary>
        public static float SkinDistance(FlightSpec spec, double x, double z, float heading, double px, double pz)
        {
            double fx = Math.Sin(heading), fz = Math.Cos(heading);
            double half = 0.5 * spec.LengthM - spec.FuselageRadiusM;
            double dx = px - x, dz = pz - z;
            double along = Math.Max(-half, Math.Min(half, dx * fx + dz * fz));
            double cx = x + fx * along, cz = z + fz * along;
            double ex = px - cx, ez = pz - cz;
            return (float)(Math.Sqrt(ex * ex + ez * ez) - Math.Max(spec.FuselageRadiusM, spec.Rotorcraft ? 1.2f : 0f));
        }

        /// <summary>Where the pilot steps out: beside the main door on the left, a little outside the fuselage (or under a
        /// low wing's trailing edge for the low-wing commuter).</summary>
        public static void ExitPoint(FlightSpec spec, double x, double z, float heading, out double ex, out double ez)
        {
            double fx = Math.Sin(heading), fz = Math.Cos(heading), rx = Math.Cos(heading), rz = -Math.Sin(heading);
            double side = spec.DoorX - 1.4;
            ex = x + fx * spec.DoorZ + rx * side;
            ez = z + fz * spec.DoorZ + rz * side;
        }

        /// <summary>The door point (on the skin) in game metres.</summary>
        public static void DoorPoint(FlightSpec spec, double x, double z, float heading, out double dx, out double dz)
        {
            double fx = Math.Sin(heading), fz = Math.Cos(heading), rx = Math.Cos(heading), rz = -Math.Sin(heading);
            dx = x + fx * spec.DoorZ + rx * spec.DoorX;
            dz = z + fz * spec.DoorZ + rz * spec.DoorX;
        }
    }

    /// <summary>
    /// Generic liveries (aviation research §7.3; ASSET_MANIFEST branding rule: no names, logos, registrations, flags or a
    /// carrier's colour scheme): body, belly, tail and cheat-line colours (0xRRGGBB). Helicopters use their own three.
    /// </summary>
    public static class FlightLiveries
    {
        public struct Livery
        {
            public uint Body, Belly, Tail, Stripe, Accent;
        }

        /// <summary>Fixed-wing: glacier, rhododendron, kite, monsoon, sunrise, yak.</summary>
        public static readonly Livery[] FixedWing =
        {
            new Livery { Body = 0xF4F6F8, Belly = 0xD9DEE4, Tail = 0x2A9D8F, Stripe = 0x264653, Accent = 0xF2F4F5 },
            new Livery { Body = 0xFAF7F2, Belly = 0xE9E2D8, Tail = 0xD1495B, Stripe = 0xEDAE49, Accent = 0xFFF4E0 },
            new Livery { Body = 0xF7F9FB, Belly = 0xCFE8F7, Tail = 0x4FC3F7, Stripe = 0xFFD54F, Accent = 0xFFFFFF },
            new Livery { Body = 0xEEF2F0, Belly = 0xB7C9BF, Tail = 0x5B8C5A, Stripe = 0xA3C4BC, Accent = 0xF6FAF7 },
            new Livery { Body = 0xFFFFFF, Belly = 0xF3E3D3, Tail = 0xE76F51, Stripe = 0x6D597A, Accent = 0xF4A261 },
            new Livery { Body = 0xF3E9D2, Belly = 0xD8C8A8, Tail = 0x8D6E63, Stripe = 0x3E2723, Accent = 0xFBF5E8 },
        };

        /// <summary>Helicopters: red, yellow, blue bodies with white and a dark trim (no medical crosses).</summary>
        public static readonly Livery[] Rotor =
        {
            new Livery { Body = 0xE63946, Belly = 0xF4F4F4, Tail = 0xE63946, Stripe = 0xF4F4F4, Accent = 0x2B2F33 },
            new Livery { Body = 0xFFC93C, Belly = 0x2B2F33, Tail = 0xFFC93C, Stripe = 0x2B2F33, Accent = 0x2B2F33 },
            new Livery { Body = 0x3A86FF, Belly = 0xF4F4F4, Tail = 0x3A86FF, Stripe = 0xF4F4F4, Accent = 0x2B2F33 },
        };

        public static Livery For(FlightCraft craft, int livery)
        {
            Livery[] set = craft == FlightCraft.Helicopter ? Rotor : FixedWing;
            return set[((livery % set.Length) + set.Length) % set.Length];
        }
    }
}
