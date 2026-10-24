using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The height of a junction cap where its arms are drawn at structure heights (docs/DATA_FORMATS.md 1.15: the stations
    /// of all ways at one OSM node share one height). A cap is drawn a lift above the terrain, where the pinned ribbons of
    /// draped arms meet it; an arm whose ribbon follows its deck heights at the cut (an underpass lowered into a cutting, a
    /// ramp onto a structure) stands off that by its offset there, and the cap follows its arms instead. The Ratna Park
    /// junction under the foot overbridges, whose four arms the data lowers 4 m, stood at the terrain before: a 4.3 m wall
    /// across every arm that stopped a bus on Kantipath (camera package rides, docs/research/w2/ref_camera.md §6).
    /// <para>The offset at a point of the cap blends the arms' offsets by inverse squared distance to their cut sections
    /// (exactly an arm's offset on its cut), so a cap with every arm lowered sinks with them and a cap with draped arms keeps
    /// its height. One instance per thread (<see cref="ForThread"/>): <see cref="Of"/> fills it for one cap without
    /// allocating.</para>
    /// </summary>
    internal sealed class CapLevels
    {
        /// <summary>An arm off the draped cap height by less than this counts as draped.</summary>
        public const float MinOffsetM = 0.05f;

        [ThreadStatic] private static CapLevels _thread;

        private double[] _ax = new double[8], _az = new double[8], _bx = new double[8], _bz = new double[8];
        private float[] _d = new float[8];
        private int[] _road = new int[8];
        private RoadCut[] _cut = new RoadCut[8];
        private int _n;

        /// <summary>True when some arm of the cap filled last is drawn off the draped cap height.</summary>
        public bool Any { get; private set; }

        /// <summary>This thread's instance.</summary>
        public static CapLevels ForThread()
        {
            return _thread ?? (_thread = new CapLevels());
        }

        /// <summary>
        /// Fills the arm offsets of <paramref name="cap"/> drawn with lift <paramref name="lift"/> (the top road's lift plus
        /// <see cref="JunctionMesher.CapExtraLiftM"/>): each arm's ribbon height at its cut centre minus the terrain there plus
        /// the lift (0 for a draped arm, whose pinned ribbon end meets the draped cap).
        /// </summary>
        public void Of(TileData t, RoadLayout layout, RoadGrade grade, JunctionCap cap, float lift)
        {
            _n = 0;
            Any = false;
            int n = cap.ArmRoads.Length;
            if (_d.Length < n)
            {
                _ax = new double[n];
                _az = new double[n];
                _bx = new double[n];
                _bz = new double[n];
                _d = new float[n];
                _road = new int[n];
                _cut = new RoadCut[n];
            }
            for (int k = 0; k < n; k++)
            {
                int ri = cap.ArmRoads[k];
                RoadCut c = layout.StoredCut(t, ri, cap.ArmCutS[k]);
                double lo = c.Shift - c.Half, hi = c.Shift + c.Half;
                _ax[k] = c.CX + c.UX * lo;
                _az[k] = c.CZ + c.UZ * lo;
                _bx[k] = c.CX + c.UX * hi;
                _bz[k] = c.CZ + c.UZ * hi;
                _road[k] = ri;
                _cut[k] = c;
                float d = grade.Has(ri) ? ArmOffset(grade, ri, c, lift) : 0f;
                _d[k] = d;
                if (d != 0f) Any = true;
            }
            _n = n;
        }

        /// <summary>The offset of arm <paramref name="arm"/> (index into the cap's arms) of the cap filled last.</summary>
        public float ArmOffset(int arm)
        {
            return arm >= 0 && arm < _n ? _d[arm] : 0f;
        }

        /// <summary>
        /// How far the ribbon of road <paramref name="ri"/> at its cut <paramref name="c"/> stands off the draped cap height
        /// (terrain + <paramref name="lift"/>) at the cut's centre: its deck heights, an absolute pin onto a deck end or a
        /// clearance lowering there; 0 where it is draped (a ribbon end pinned to the cap lift meets the draped cap
        /// exactly).
        /// </summary>
        public static float ArmOffset(RoadGrade grade, int ri, in RoadCut c, float lift)
        {
            RoadGrade.Row row = grade.RowAt(ri, c.S);
            if (!row.Valid) return 0f;
            double x = c.CX + c.UX * c.Shift, z = c.CZ + c.UZ * c.Shift;
            float d = grade.Y(row, c.Shift, x, z) - (grade.Terrain(x, z) + lift);
            return Math.Abs(d) >= MinOffsetM ? d : 0f;
        }

        /// <summary>The cap's offset from its draped height at tile-local (x, z): the arms' offsets blended by inverse
        /// squared distance to their cut sections (0 when every arm is draped).</summary>
        public float At(double x, double z)
        {
            if (!Any) return 0f;
            double sw = 0, sd = 0;
            for (int k = 0; k < _n; k++)
            {
                double d2 = SegmentDistanceSq(x, z, _ax[k], _az[k], _bx[k], _bz[k]);
                if (d2 < 1e-8) return _d[k];
                double w = 1.0 / d2;
                sw += w;
                sd += w * _d[k];
            }
            return sw > 0 ? (float)(sd / sw) : 0f;
        }

        /// <summary>The cap's drawn height at tile-local (x, z) on <paramref name="grade"/>'s terrain: exactly the arm's ribbon
        /// on the cut section of an arm drawn off the draped height (the cap and the ribbon end row share those vertices),
        /// else the terrain + <paramref name="lift"/> + <see cref="At"/>.</summary>
        public float Height(RoadGrade grade, double x, double z, float lift)
        {
            if (Any)
            {
                for (int k = 0; k < _n; k++)
                {
                    if (_d[k] == 0f || SegmentDistanceSq(x, z, _ax[k], _az[k], _bx[k], _bz[k]) > 1e-6) continue;
                    // The ribbon end row of the arm at this column (its lateral offset along the cut section).
                    RoadCut c = _cut[k];
                    double off = (x - c.CX) * c.UX + (z - c.CZ) * c.UZ;
                    return grade.Y(grade.RowAt(_road[k], c.S), off, x, z);
                }
            }
            return grade.Terrain(x, z) + lift + At(x, z);
        }

        private static double SegmentDistanceSq(double px, double pz, double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            double f = l2 > 1e-12 ? ((px - ax) * dx + (pz - az) * dz) / l2 : 0;
            f = f < 0 ? 0 : f > 1 ? 1 : f;
            double ex = ax + dx * f - px, ez = az + dz * f - pz;
            return ex * ex + ez * ez;
        }
    }
}
