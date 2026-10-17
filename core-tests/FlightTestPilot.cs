using System;
using Ghumante.Core.Driving;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// A small autopilot that flies the <see cref="FlightModel"/> only through <see cref="FlightInput"/> (the same sticks,
    /// lever and brake the player has), for the headless take-off, circuit and landing tests: speed by throttle, climb
    /// angle by pitch, heading by bank, leg tracking with an intercept angle, a 3° glide to the touchdown zone, a flare and
    /// the brakes after touchdown.
    /// </summary>
    internal sealed class FlightTestPilot
    {
        private const float Deg = (float)(Math.PI / 180.0);

        public readonly FlightModel M;
        public readonly FlightWorld W;
        private readonly double _ox, _oz, _dx, _dz;
        public readonly double RunwayLength;
        public readonly float RunwayHeading;
        private float _throttleI = 0.6f;

        public FlightTestPilot(FlightModel m, FlightWorld w)
        {
            M = m;
            W = w;
            var c = w.Airport;
            _ox = c.Threshold02.X;
            _oz = c.Threshold02.Z;
            double dx = c.Threshold20.X - _ox, dz = c.Threshold20.Z - _oz;
            RunwayLength = Math.Sqrt(dx * dx + dz * dz);
            _dx = dx / RunwayLength;
            _dz = dz / RunwayLength;
            RunwayHeading = (float)Math.Atan2(_dx, _dz);
        }

        /// <summary>Runway frame: along 02→20 from threshold 02, lateral positive to the right (east).</summary>
        public void ToRunway(double x, double z, out double s, out double l)
        {
            s = (x - _ox) * _dx + (z - _oz) * _dz;
            l = (x - _ox) * _dz - (z - _oz) * _dx;
        }

        public void FromRunway(double s, double l, out double x, out double z)
        {
            x = _ox + _dx * s + _dz * l;
            z = _oz + _dz * s - _dx * l;
        }

        public float Track
        {
            get
            {
                double hs = Math.Sqrt(M.Vx * M.Vx + M.Vz * M.Vz);
                return hs > 5 ? (float)Math.Atan2(M.Vx, M.Vz) : M.Heading;
            }
        }

        /// <summary>Hold a climb angle (rad) and a speed, turning to a heading.</summary>
        public FlightInput Hold(float gammaWant, float speedWant, float headingWant, float maxBankDeg = 25f)
        {
            var i = new FlightInput();
            double v = Math.Max(1.0, M.Airspeed);
            float gamma = (float)Math.Asin(Math.Max(-1.0, Math.Min(1.0, M.Vy / v)));
            i.Pitch = Clamp((gammaWant - gamma) * 6f, -1f, 1f);
            float err = ArcadeVehicle.WrapAngle(headingWant - Track);
            float bankWant = Clamp(err * 2.2f, -maxBankDeg * Deg, maxBankDeg * Deg);
            i.Roll = Clamp((bankWant - M.Bank) * 2.5f, -1f, 1f);
            float dv = speedWant - (float)v;
            _throttleI = Clamp(_throttleI + dv * 0.004f, 0f, 1f);
            i.Throttle = Clamp(_throttleI + dv * 0.08f, 0f, 1f);
            return i;
        }

        /// <summary>Hold an altitude (m) and a speed on a heading.</summary>
        public FlightInput HoldAltitude(float altWant, float speedWant, float headingWant, float maxBankDeg = 25f)
        {
            double v = Math.Max(1.0, M.Airspeed);
            float vsWant = Clamp((float)(altWant - M.Y) * 0.08f, -5f, 6f);
            float gammaWant = (float)Math.Asin(Clamp(vsWant / (float)v, -0.25f, 0.25f));
            return Hold(gammaWant, speedWant, headingWant, maxBankDeg);
        }

        /// <summary>The heading that flies the leg A→B (game metres), with an intercept of up to 35°.</summary>
        public float LegHeading(double ax, double az, double bx, double bz)
        {
            double lx = bx - ax, lz = bz - az, len = Math.Sqrt(lx * lx + lz * lz);
            lx /= len;
            lz /= len;
            double cross = (M.X - ax) * lz - (M.Z - az) * lx; // positive: right of the leg
            float legH = (float)Math.Atan2(lx, lz);
            return ArcadeVehicle.WrapAngle(legH - Clamp((float)cross * 0.004f, -30f * Deg, 30f * Deg));
        }

        /// <summary>Distance still to fly along the leg A→B.</summary>
        public double LegRemaining(double ax, double az, double bx, double bz)
        {
            double lx = bx - ax, lz = bz - az, len = Math.Sqrt(lx * lx + lz * lz);
            return len - ((M.X - ax) * lx + (M.Z - az) * lz) / len;
        }

        /// <summary>Final approach to a touchdown point <paramref name="tdS"/> metres past threshold 02 on a 3° glide,
        /// with a flare, then the brakes on the ground.</summary>
        public FlightInput Final(double tdS, float vref)
        {
            double s, l;
            ToRunway(M.X, M.Z, out s, out l);
            if (M.OnGround)
            {
                float hErr = ArcadeVehicle.WrapAngle(RunwayHeading - M.Heading);
                return new FlightInput(0f, 0f, Clamp(hErr * 3f - (float)l * 0.04f, -1f, 1f), 0f, 1f);
            }
            double tdx, tdz;
            FromRunway(tdS, 0, out tdx, out tdz);
            FlightGroundSample g;
            W.Ground(tdx, tdz, 1e6f, out g);
            double dist = tdS - s;
            float agl = M.Agl;
            double glide = g.Height + Math.Tan(3.0 * Deg) * Math.Max(0.0, dist);
            double v = Math.Max(1.0, M.Airspeed);
            float vsWant = (float)(-v * Math.Tan(3.0 * Deg) + (glide - M.Y) * 0.15);
            vsWant = Clamp(vsWant, -6f, 2f);
            float speedWant = vref;
            if (agl < 12f)
            {
                // Flare: arrest the sink smoothly, power off.
                vsWant = -Math.Max(0.5f, agl * 0.18f);
                speedWant = 0f;
            }
            float gammaWant = (float)Math.Asin(Clamp(vsWant / (float)v, -0.2f, 0.1f));
            double ax, az, bx, bz;
            FromRunway(-8000, 0, out ax, out az);
            FromRunway(RunwayLength, 0, out bx, out bz);
            float want = LegHeading(ax, az, bx, bz);
            bool established = Math.Abs(l) < 25 && Math.Abs(ArcadeVehicle.WrapAngle(want - Track)) < 8f * Deg;
            FlightInput i = Hold(gammaWant, speedWant, want, established ? 12f : 25f);
            if (agl < 12f) i.Throttle = 0f;
            return i;
        }

        private static float Clamp(float v, float a, float b)
        {
            return v < a ? a : v > b ? b : v;
        }
    }
}
