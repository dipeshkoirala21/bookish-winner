using System;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>
    /// A straight stair flight (or, with <see cref="Steps"/> = 0, a flat landing pad) of a foot structure: from its
    /// top edge centre (<see cref="X"/>, <see cref="Z"/>) at <see cref="TopY"/> it descends along (<see cref="Dx"/>,
    /// <see cref="Dz"/>) to <see cref="BottomY"/>. The flight is <see cref="Steps"/> risers of equal rise; after riser
    /// i comes tread i of <see cref="Tread"/> metres, except that every <see cref="BridgeStyle.StairsPerFlight"/>-th
    /// tread before the last is a landing of <see cref="LandingM"/>, and the last tread (at the bottom height) runs on
    /// for <see cref="BottomRun"/>. The mesh (<see cref="BridgeMesher"/>) and the walkable surface
    /// (<see cref="SurfaceAt"/>, used by <see cref="BridgeDeckIndex"/>) both come from this one profile.
    /// </summary>
    public struct BridgeStair
    {
        /// <summary>Tile-local top edge centre and the heights of the top (deck) and bottom (ground) levels.</summary>
        public double X, Z;
        public float TopY, BottomY;

        /// <summary>Unit plan direction of descent.</summary>
        public double Dx, Dz;

        public double HalfWidth;

        /// <summary>Plan length of the flight: (Steps − Landings) × Tread + Landings × LandingM + BottomRun.</summary>
        public double Length;

        /// <summary>Number of risers (0 for a flat landing pad).</summary>
        public int Steps;

        /// <summary>Number of intermediate landings.</summary>
        public int Landings;

        public double Tread, LandingM, BottomRun;

        /// <summary>The flight follows a mapped steps way (otherwise it was generated at a deck end).</summary>
        public bool Mapped;

        /// <summary>Height of one riser.</summary>
        public float Rise
        {
            get { return Steps > 0 ? (TopY - BottomY) / Steps : 0f; }
        }

        /// <summary>True when tread <paramref name="i"/> is an intermediate landing (one every
        /// <see cref="BridgeStyle.StairsPerFlight"/> steps, as many as <see cref="Landings"/>: a flight fitted to a
        /// short mapped way has none).</summary>
        public bool IsLanding(int i)
        {
            int n = (i + 1) / BridgeStyle.StairsPerFlight;
            return Landings > 0 && (i + 1) % BridgeStyle.StairsPerFlight == 0 && i < Steps - 1 && n <= Landings;
        }

        /// <summary>Plan length of tread <paramref name="i"/> (landings, and the last tread with the bottom run).</summary>
        public double TreadLength(int i)
        {
            if (i == Steps - 1) return Tread + BottomRun;
            return IsLanding(i) ? LandingM : Tread;
        }

        /// <summary>Height of tread <paramref name="i"/>: <see cref="TopY"/> − (i + 1) × <see cref="Rise"/>.</summary>
        public float TreadY(int i)
        {
            return TopY - (i + 1) * Rise;
        }

        /// <summary>
        /// The walkable surface at <paramref name="along"/> metres from the top edge: a ramp through the tread nosings
        /// (from the back of each tread, one riser above it, down to its front edge over one tread length) and flat on
        /// landings and the bottom run. It never lies under the drawn tread and at most one riser above it.
        /// </summary>
        public float SurfaceAt(double along)
        {
            if (Steps <= 0) return TopY;
            if (along <= 0) return TopY;
            float rise = Rise;
            double a = 0;
            for (int i = 0; i < Steps; i++)
            {
                double len = TreadLength(i);
                if (along <= a + len || i == Steps - 1)
                {
                    double f = Tread > 1e-6 ? (along - a) / Tread : 1;
                    if (f > 1) f = 1;
                    float yi = TopY - (i + 1) * rise;
                    return (float)(yi + rise * (1 - f));
                }
                a += len;
            }
            return BottomY;
        }

        /// <summary>The surface grade (dy per metre along) at <paramref name="along"/>, for the deck normal.</summary>
        public float GradeAt(double along)
        {
            if (Steps <= 0 || Tread < 1e-6) return 0f;
            double a = 0;
            for (int i = 0; i < Steps; i++)
            {
                double len = TreadLength(i);
                if (along <= a + len || i == Steps - 1) return along - a <= Tread ? (float)(-Rise / Tread) : 0f;
                a += len;
            }
            return 0f;
        }

        /// <summary>
        /// A flight of <paramref name="drop"/> metres over <paramref name="length"/> metres of plan (a mapped steps way):
        /// risers near <see cref="BridgeStyle.StairRiseM"/>, landings on long flights, treads of 0.22 to 0.32 m where the
        /// length allows (a longer way ends in a flat bottom run; a much shorter one gives steeper steps).
        /// </summary>
        public static BridgeStair Fit(double x, double z, double dx, double dz, float top, float bottom, double length, double halfWidth)
        {
            var st = new BridgeStair { X = x, Z = z, Dx = dx, Dz = dz, TopY = top, BottomY = bottom, HalfWidth = halfWidth, Mapped = true };
            double drop = top - bottom;
            length = Math.Max(0.3, length);
            if (drop < 0.05)
            {
                st.Steps = 0;
                st.Length = length;
                st.BottomY = top;
                return st;
            }
            int steps = Math.Max(1, (int)Math.Round(drop / BridgeStyle.StairRiseM));
            int landings = (steps - 1) / BridgeStyle.StairsPerFlight;
            double land = BridgeStyle.StairLandingM;
            double tread = (length - landings * land) / Math.Max(1, steps - landings);
            if (tread < 0.24 && landings > 0)
            {
                landings = 0;
                tread = length / steps;
            }
            if (tread < 0.22)
            {
                steps = Math.Max(steps, (int)Math.Ceiling(drop / 0.2));
                tread = Math.Max(0.12, length / steps);
            }
            double run = 0;
            if (tread > 0.32)
            {
                tread = 0.32;
                run = length - ((steps - landings) * tread + landings * land);
            }
            st.Steps = steps;
            st.Landings = landings;
            st.Tread = tread;
            st.LandingM = land;
            st.BottomRun = Math.Max(0, run);
            st.Length = (steps - landings) * tread + landings * land + st.BottomRun;
            return st;
        }

        /// <summary>A generated flight of the standard stair (<see cref="BridgeStyle.StairRiseM"/> /
        /// <see cref="BridgeStyle.StairTreadM"/>, a landing every <see cref="BridgeStyle.StairsPerFlight"/> steps) for a
        /// drop of <paramref name="drop"/> metres; its length follows from the steps.</summary>
        public static BridgeStair Standard(double x, double z, double dx, double dz, float top, float drop, double halfWidth)
        {
            int steps = Math.Max(2, (int)Math.Round(drop / BridgeStyle.StairRiseM));
            int landings = (steps - 1) / BridgeStyle.StairsPerFlight;
            var st = new BridgeStair
            {
                X = x, Z = z, Dx = dx, Dz = dz, TopY = top, BottomY = top - drop, HalfWidth = halfWidth, Steps = steps, Landings = landings,
                Tread = BridgeStyle.StairTreadM, LandingM = BridgeStyle.StairLandingM,
            };
            st.Length = (steps - landings) * st.Tread + landings * st.LandingM;
            return st;
        }

        /// <summary>A flat square landing pad centred on (x, z) at height y, facing (dx, dz).</summary>
        public static BridgeStair Pad(double x, double z, double dx, double dz, float y, double halfWidth)
        {
            return new BridgeStair
            {
                X = x - dx * halfWidth, Z = z - dz * halfWidth, Dx = dx, Dz = dz, TopY = y, BottomY = y, HalfWidth = halfWidth,
                Length = 2 * halfWidth, Mapped = true,
            };
        }
    }
}
