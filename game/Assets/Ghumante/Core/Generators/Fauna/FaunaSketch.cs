using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// Shared drafting state of the fauna meshers: the builder, the level's tessellation, a species scale
    /// (<see cref="S"/>: knot coordinates are written in species units), a rear-length factor and a scratch knot list.
    /// </summary>
    internal sealed class FaunaSketch
    {
        public FaunaBuilder B;
        public FaunaDetail D;
        public float S;

        /// <summary>Length factor of the rear half (z &lt; 0): shortens the calf's body.</summary>
        public float Zb = 1f;

        public Knot[] K = new Knot[16];
        public int N;

        /// <summary>Scratch paths and profile for the shapes-library details (<see cref="FaunaShapes"/>).</summary>
        public readonly Path3 Ctrl = new Path3(16), Path = new Path3(64);

        public readonly Profile2 Prof = new Profile2(32);

        public void Begin()
        {
            N = 0;
        }

        /// <summary>Adds a knot in species units (scaled by <see cref="S"/>).</summary>
        public void Add(float x, float y, float z, float a, float bt, float bb, FaunaBone bone, uint col, float e = 2f)
        {
            if (z < 0f) z *= Zb;
            K[N++] = new Knot(x * S, y * S, z * S, a * S, bt * S, bb * S, bone, col, e);
        }

        public void Add(float x, float y, float z, float r, FaunaBone bone, uint col)
        {
            Add(x, y, z, r, r, r, bone, col);
        }

        /// <summary>Lofts the knots added since <see cref="Begin"/> (decimated by the level's knot step, always
        /// keeping the first and last knot).</summary>
        public int Tube(int segs, int rings, FaunaPart part, MaterialChannel ch, Fv3 up, float cap0 = 0.6f, float cap1 = 0.6f)
        {
            Decimate();
            return B.Tube(K, N, rings, segs, up, ch, part, cap0, cap1);
        }

        /// <summary>Keeps every <see cref="FaunaDetail.KnotStep"/>-th knot (and the last) of the current list.</summary>
        public void Decimate()
        {
            int step = D.KnotStep;
            if (step <= 1 || N < 4) return;
            int w = 1;
            for (int i = step; i < N - 1; i += step) K[w++] = K[i];
            K[w++] = K[N - 1];
            N = w;
        }

        public Fv3 P(float x, float y, float z)
        {
            if (z < 0f) z *= Zb;
            return new Fv3(x * S, y * S, z * S);
        }

        /// <summary>An ellipsoid in species units (centre and radii scaled by <see cref="S"/>).</summary>
        public int Ell(float x, float y, float z, float rSide, float rUp, float rFwd, Fv3 fwd, Fv3 up, int rings, int segs, FaunaBone bone, uint col,
                       MaterialChannel ch, FaunaPart part, float e = 2f)
        {
            return B.Ellipsoid(P(x, y, z), fwd, up, rSide * S, rUp * S, rFwd * S, rings, segs, bone, col, ch, part, e);
        }

        /// <summary>Starts a left-side group that <see cref="EndSide"/> mirrors to the right.</summary>
        public SideMark StartSide()
        {
            return new SideMark(B.VertexCount, B.IndexCount, B.OccluderCount);
        }

        /// <summary>Mirrors everything added since <paramref name="m"/> to the other side.</summary>
        public void EndSide(SideMark m)
        {
            B.Mirror(m.V, m.I, m.O);
        }
    }

    /// <summary>Where a mirrored group starts (vertex, index and occluder counts).</summary>
    internal readonly struct SideMark
    {
        public readonly int V, I, O;

        public SideMark(int v, int i, int o)
        {
            V = v;
            I = i;
            O = o;
        }
    }
}
