using System;
using System.Collections.Generic;

namespace Ghumante.Core.Meshing
{
    /// <summary>One approach of a <see cref="RoadRing"/>: the piece, where its ribbon stops, and its entry flares.</summary>
    public struct RingArm
    {
        /// <summary>The approach piece (index into <see cref="Data.TileData.Roads"/>).</summary>
        public int Road;

        /// <summary>The piece touches the ring with its first rendered point (else its last).</summary>
        public bool AtStart;

        /// <summary>The section where the approach ribbon stops and the ring surface takes over.</summary>
        public RoadCut Cut;

        /// <summary>Bearing of the arm from the ring centre (radians, counter-clockwise from +X).</summary>
        public double Angle;

        /// <summary>Unit direction of the arm away from the ring at the cut.</summary>
        public double DirX, DirZ;

        /// <summary>Carriageway width at the cut.</summary>
        public float WidthM;

        /// <summary>Traffic enters the ring from this arm / leaves the ring into it (one-way approaches do one).</summary>
        public bool Entry, Exit;

        /// <summary>A raised splitter island separates entry and exit (two-way approaches at least 7 m wide).</summary>
        public bool Splitter;

        public float SplitterLengthM, SplitterWidthM;

        /// <summary>The cut's carriageway edge points: right and left seen from the centre looking out.</summary>
        public double RightEdgeX, RightEdgeZ, LeftEdgeX, LeftEdgeZ;

        /// <summary>The right flare from its circle point (first) toward the right edge point (excluded), and the left
        /// flare from next to the left edge point to its circle point (last): kerb-return curves onto the outer circle.</summary>
        public double[] RightX, RightZ, LeftX, LeftZ;

        /// <summary>Bearings (radians) of the right and left flares' circle points.</summary>
        public double AngleRight, AngleLeft;
    }

    /// <summary>
    /// A roundabout drawn as a true circle (W2_DESIGN 4.7, owner: "make roundabouts more detailed"): a circular ring
    /// carriageway between the island edge (<see cref="InnerRadiusM"/>, apron included) and <see cref="OuterRadiusM"/>,
    /// with an entry patch for every approach from its cut section to the circle between two kerb-return flares, and
    /// splitter islands on wide two-way approaches. The mapped ring ways (<see cref="Members"/>) are not drawn as ribbons
    /// (their lanes still follow them); the approach ribbons stop at <see cref="RingArm.Cut"/>. Tile-local metres.
    /// </summary>
    public sealed class RoadRing
    {
        public double X, Z;

        /// <summary>Island edge: where the ring carriageway starts (the island's mountable apron is inside it).</summary>
        public double InnerRadiusM;

        /// <summary>Outer edge of the circular carriageway (between entries).</summary>
        public double OuterRadiusM;

        /// <summary>Ring carriageway width (widest approach + 1 m, at least 7 m).</summary>
        public float WidthM;

        /// <summary>Kerb-return radius of the entry flares.</summary>
        public float FlareRadiusM;

        /// <summary>The ring member whose surface colour and lift the ring takes.</summary>
        public int TopRoad;

        /// <summary>Mapped ring ways (RING_MEMBER pieces) this ring replaces.</summary>
        public int[] Members = new int[0];

        /// <summary>Approaches, counter-clockwise by bearing.</summary>
        public RingArm[] Arms = new RingArm[0];

        /// <summary>Index of the island in <see cref="RoadLayout.Islands"/>, or -1.</summary>
        public int Island = -1;

        /// <summary>Largest angle between neighbouring points of the outer circle.</summary>
        public const double CircleStepDeg = 6.0;

        /// <summary>Number of outer-circle segments (a chord at most <see cref="CircleStepDeg"/>).</summary>
        public int CircleSegments
        {
            get { return Math.Max(24, (int)Math.Ceiling(360.0 / CircleStepDeg)); }
        }

        /// <summary>
        /// The entry patch of arm <paramref name="a"/> as a polygon (appended to <paramref name="xs"/>, <paramref name="zs"/>):
        /// the cut's right and left edge points, the left flare out to the circle, the circle back clockwise (at the outer
        /// circle's segment angles) to the right flare's circle point, and the right flare back to the edge.
        /// </summary>
        public void Patch(int a, List<double> xs, List<double> zs)
        {
            RingArm arm = Arms[a];
            xs.Add(arm.RightEdgeX);
            zs.Add(arm.RightEdgeZ);
            xs.Add(arm.LeftEdgeX);
            zs.Add(arm.LeftEdgeZ);
            for (int k = 0; k < arm.LeftX.Length; k++)
            {
                xs.Add(arm.LeftX[k]);
                zs.Add(arm.LeftZ[k]);
            }
            double step = 2 * Math.PI / CircleSegments;
            double from = arm.AngleLeft, to = arm.AngleRight;
            double sweep = Wrap(from - to); // clockwise from left to right
            if (sweep > Math.PI) sweep = 0; // degenerate (flares crossed): close directly
            double first = Math.Floor(from / step) * step;
            for (double ang = first; Wrap(from - ang) < sweep - 1e-6; ang -= step)
            {
                if (Wrap(from - ang) <= 1e-6) continue;
                xs.Add(X + OuterRadiusM * Math.Cos(ang));
                zs.Add(Z + OuterRadiusM * Math.Sin(ang));
            }
            for (int k = 0; k < arm.RightX.Length; k++)
            {
                xs.Add(arm.RightX[k]);
                zs.Add(arm.RightZ[k]);
            }
        }

        /// <summary>Signed distance from (x, z) to the ring carriageway and its entries (negative on them, positive in
        /// the island and outside).</summary>
        public double SignedDistance(double x, double z)
        {
            double dx = x - X, dz = z - Z;
            double r = Math.Sqrt(dx * dx + dz * dz);
            double best = Math.Max(InnerRadiusM - r, r - OuterRadiusM);
            if (best <= 0) return best;
            var xs = new List<double>();
            var zs = new List<double>();
            for (int a = 0; a < Arms.Length; a++)
            {
                xs.Clear();
                zs.Clear();
                Patch(a, xs, zs);
                double d = PolygonSignedDistance(xs.ToArray(), zs.ToArray(), xs.Count, x, z);
                if (d < best) best = d;
            }
            return best;
        }

        internal static double Wrap(double a)
        {
            while (a < 0) a += 2 * Math.PI;
            while (a >= 2 * Math.PI) a -= 2 * Math.PI;
            return a;
        }

        /// <summary>Signed distance to a closed polygon (negative inside).</summary>
        internal static double PolygonSignedDistance(double[] px, double[] pz, int n, double x, double z)
        {
            if (n < 3) return double.PositiveInfinity;
            double best = double.PositiveInfinity;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double ax = px[j], az = pz[j], bx = px[i], bz = pz[i];
                if ((az > z) != (bz > z) && x < (bx - ax) * (z - az) / (bz - az) + ax) inside = !inside;
                double ox, oz;
                double d = RoadCorridor.PointSeg(x, z, ax, az, bx, bz, out ox, out oz);
                if (d < best) best = d;
            }
            return inside ? -best : best;
        }
    }
}
