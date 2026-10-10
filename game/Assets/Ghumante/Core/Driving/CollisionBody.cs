using System;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// The solid footprint of a body for swept collisions (docs/W2_DETAIL_CONTRACT.md §1): a capsule along the heading,
    /// approximated by <see cref="Circles"/> circles of radius <see cref="Radius"/> spaced at most one radius apart from
    /// <see cref="FirstM"/> to <see cref="LastM"/> metres ahead of the vehicle origin (on the ground under the rear axle,
    /// as <see cref="VehicleSpec"/>), and a height <see cref="HeightM"/> above the feet that overhangs must clear.
    /// </summary>
    public readonly struct CollisionBody
    {
        /// <summary>Most circles a body uses (a 10.5 m bus needs 8).</summary>
        public const int MaxCircles = 12;

        /// <summary>Rider plus machine: a seated rider's head is about this high on a bike or scooter.</summary>
        public const float RiderHeightM = 1.75f;

        public readonly float Radius, HeightM, FirstM, LastM;
        public readonly int Circles;

        public CollisionBody(float radius, float heightM, float firstM, float lastM, int circles)
        {
            if (!(radius > 0f)) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!(heightM > 0f)) throw new ArgumentOutOfRangeException(nameof(heightM));
            Radius = radius;
            HeightM = heightM;
            FirstM = firstM;
            LastM = lastM < firstM ? firstM : lastM;
            Circles = circles < 1 ? 1 : circles > MaxCircles ? MaxCircles : circles;
            if (LastM <= FirstM) Circles = 1;
        }

        /// <summary>A single upright circle (walkers, the ground query's default body).</summary>
        public static CollisionBody Upright(float radius, float heightM)
        {
            return new CollisionBody(radius, heightM, 0f, 0f, 1);
        }

        /// <summary>
        /// The body of a vehicle spec: half its width (at least 0.3 m) as radius, the circles spanning its length from
        /// the rear overhang to the front, the machine's height (two-wheelers: at least <see cref="RiderHeightM"/> for the
        /// rider; walkers: the person plus 0.1 m). A walker's circle is centred on its position.
        /// </summary>
        public static CollisionBody For(VehicleSpec s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.TurnInPlace)
            {
                float rw = Math.Max(0.3f, 0.5f * Math.Max(s.WidthM, s.LengthM));
                return Upright(rw, Math.Max(1.5f, s.HeightM) + 0.1f);
            }
            float r = Math.Max(0.3f, 0.5f * s.WidthM);
            float h = s.HeightM > 0f ? s.HeightM : 1.5f;
            if (s.TwoWheeler || s.Kind == VehicleKind.Bicycle) h = Math.Max(h, RiderHeightM);
            float len = s.LengthM > 0f ? s.LengthM : 2f * r;
            float back = -s.RearOverhangM, front = len - s.RearOverhangM;
            float first = back + r, last = front - r;
            if (last <= first)
            {
                float mid = 0.5f * (back + front);
                return new CollisionBody(r, h, mid, mid, 1);
            }
            int n = (int)Math.Ceiling((last - first) / r - 1e-4) + 1;
            return new CollisionBody(r, h, first, last, n);
        }

        /// <summary>Centre of circle <paramref name="k"/> for a body at (x, z) heading <paramref name="headingRad"/>
        /// (0 north, clockwise).</summary>
        public void Centre(int k, double x, double z, float headingRad, out double cx, out double cz)
        {
            float along = Circles <= 1 ? FirstM : FirstM + (LastM - FirstM) * k / (Circles - 1);
            cx = x + along * Math.Sin(headingRad);
            cz = z + along * Math.Cos(headingRad);
        }
    }

    /// <summary>
    /// Solid world geometry for swept bodies (docs/W2_DETAIL_CONTRACT.md §1, decision 1 and 2): building footprints,
    /// generated structures, props, railings and deck undersides, each spanning a height range. A solid blocks a body
    /// when it spans the body's height (from above the step-up over the feet to the head), so low plinths are stepped
    /// onto and overhangs above the head pass. <see cref="TileGroundQuery"/> implements it; <see cref="ArcadeVehicle"/>
    /// uses it automatically when its ground query does.
    /// </summary>
    public interface ISolidQuery
    {
        /// <summary>
        /// Sweeps <paramref name="body"/> (at (x, z), heading <paramref name="headingRad"/>, feet at
        /// <paramref name="feetY"/>) by (dx, dz) without rotating it. Returns true on contact: <paramref name="t"/> is
        /// the fraction of the move done before it, (nx, nz) the unit contact normal (from the solid towards the body).
        /// Exact at any speed (no tunnelling). A body already touching a solid may move away from it.
        /// </summary>
        bool SweepBody(in CollisionBody body, double x, double z, float headingRad, float feetY, double dx, double dz,
                       out float t, out float nx, out float nz);

        /// <summary>The push that separates a body at rest from the solid it overlaps most (false when it overlaps none).</summary>
        bool Penetration(in CollisionBody body, double x, double z, float headingRad, float feetY, out double pushX, out double pushZ);
    }
}
