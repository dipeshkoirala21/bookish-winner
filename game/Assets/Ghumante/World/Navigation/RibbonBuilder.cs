using System;

namespace Ghumante.World.Navigation
{
    /// <summary>
    /// Geometry of a route ribbon: resamples a game-metre polyline (interleaved x, z) so no segment is longer than
    /// <see cref="MaxSegmentM"/> (at most <see cref="MaxPoints"/> points, spacing grows for very long routes), keeps a
    /// height per point (filled from the ground as tiles stream in; gaps are interpolated from the nearest known
    /// heights) and writes a flat strip <see cref="WidthM"/> wide, <see cref="HoverM"/> above the ground, with mitred
    /// corners. Vertices are relative to the first point (<see cref="AnchorX"/>, <see cref="AnchorZ"/>) so the strip
    /// survives floating-origin shifts unchanged. UV u runs 0..1 across, v is metres along the route. Engine-free.
    /// </summary>
    public sealed class RibbonBuilder
    {
        public const int MaxPoints = 20000;

        public float WidthM = 2.4f;
        public float HoverM = 0.35f;
        public double MaxSegmentM = 4.0;
        public float MaxMiter = 2f;

        private double[] _x = new double[256], _z = new double[256], _s = new double[256];
        private float[] _h = new float[256];
        private bool[] _known = new bool[256];
        private int _count;

        public int PointCount
        {
            get { return _count; }
        }

        public int VertexCount
        {
            get { return _count * 2; }
        }

        public int IndexCount
        {
            get { return _count < 2 ? 0 : (_count - 1) * 6; }
        }

        public double AnchorX { get; private set; }
        public double AnchorZ { get; private set; }

        /// <summary>Route length in metres.</summary>
        public double LengthM
        {
            get { return _count > 0 ? _s[_count - 1] : 0.0; }
        }

        public double X(int i)
        {
            return _x[i];
        }

        public double Z(int i)
        {
            return _z[i];
        }

        public double DistanceAlong(int i)
        {
            return _s[i];
        }

        public bool HasHeight(int i)
        {
            return _known[i];
        }

        public float Height(int i)
        {
            return _h[i];
        }

        /// <summary>Replace the route (heights unknown until <see cref="SetHeight"/>). Points closer than 1 cm to their
        /// predecessor are dropped.</summary>
        public void SetPath(double[] xz, int pointCount)
        {
            if (xz == null) throw new ArgumentNullException(nameof(xz));
            if (pointCount < 0 || pointCount * 2 > xz.Length) throw new ArgumentOutOfRangeException(nameof(pointCount));
            _count = 0;
            if (pointCount == 0) return;
            double total = 0;
            for (int i = 1; i < pointCount; i++)
                total += Math.Sqrt(Sq(xz[2 * i] - xz[2 * i - 2]) + Sq(xz[2 * i + 1] - xz[2 * i - 1]));
            double seg = Math.Max(MaxSegmentM, total / (MaxPoints - pointCount > 1 ? MaxPoints - pointCount : 1));

            AnchorX = xz[0];
            AnchorZ = xz[1];
            Add(xz[0], xz[1], 0.0);
            for (int i = 1; i < pointCount; i++)
            {
                double x0 = _x[_count - 1], z0 = _z[_count - 1], x1 = xz[2 * i], z1 = xz[2 * i + 1];
                double len = Math.Sqrt(Sq(x1 - x0) + Sq(z1 - z0));
                if (len < 0.01) continue;
                int pieces = Math.Max(1, (int)Math.Ceiling(len / seg));
                double s0 = _s[_count - 1];
                for (int k = 1; k <= pieces; k++)
                {
                    double t = (double)k / pieces;
                    Add(x0 + (x1 - x0) * t, z0 + (z1 - z0) * t, s0 + len * t);
                }
            }
        }

        /// <summary>Record the ground height under point <paramref name="i"/>.</summary>
        public void SetHeight(int i, float h)
        {
            _h[i] = h;
            _known[i] = true;
        }

        /// <summary>Forget every height (a new route, or ground that changed everywhere).</summary>
        public void ClearHeights()
        {
            Array.Clear(_known, 0, _count);
        }

        /// <summary>
        /// Write the strip: <paramref name="positions"/> xyz per vertex (two per point, left then right),
        /// <paramref name="uv"/> uv per vertex, <paramref name="visible"/> 1 where the height is known from the ground
        /// (unknown stretches take the nearest known heights and are faded out by the shader). Arrays must hold
        /// <see cref="VertexCount"/> vertices.
        /// </summary>
        public void WriteVertices(float[] positions, float[] uv, byte[] visible)
        {
            if (_count < 2) return;
            float half = WidthM * 0.5f;
            int prevKnown = -1;
            for (int i = 0; i < _count; i++)
            {
                if (_known[i]) prevKnown = i;
                float h = HeightOrNearest(i, prevKnown) + HoverM;

                double dx0, dz0, dx1, dz1;
                Direction(i > 0 ? i - 1 : 0, i > 0 ? i : 1, out dx0, out dz0);
                Direction(i < _count - 1 ? i : i - 1, i < _count - 1 ? i + 1 : i, out dx1, out dz1);
                // Left normals of both segments; the mitre is their bisector, lengthened to keep the width.
                double nx = -dz0 - dz1, nz = dx0 + dx1;
                double nl = Math.Sqrt(nx * nx + nz * nz);
                if (nl < 1e-9)
                {
                    nx = -dz0;
                    nz = dx0;
                    nl = 1.0;
                }
                nx /= nl;
                nz /= nl;
                double cos = nx * -dz0 + nz * dx0;
                double scale = 1.0 / Math.Max(cos, 1.0 / MaxMiter);

                double cx = _x[i] - AnchorX, cz = _z[i] - AnchorZ;
                double ox = nx * half * scale, oz = nz * half * scale;
                int p = i * 6;
                positions[p] = (float)(cx + ox);
                positions[p + 1] = h;
                positions[p + 2] = (float)(cz + oz);
                positions[p + 3] = (float)(cx - ox);
                positions[p + 4] = h;
                positions[p + 5] = (float)(cz - oz);
                int u = i * 4;
                float v = (float)_s[i];
                uv[u] = 0f;
                uv[u + 1] = v;
                uv[u + 2] = 1f;
                uv[u + 3] = v;
                byte vis = _known[i] ? (byte)255 : (byte)0;
                visible[i * 2] = vis;
                visible[i * 2 + 1] = vis;
            }
        }

        /// <summary>Triangle indices of the strip (Unity winding: faces up).</summary>
        public void WriteIndices(int[] indices)
        {
            for (int i = 0; i + 1 < _count; i++)
            {
                int l0 = i * 2, r0 = l0 + 1, l1 = l0 + 2, r1 = l0 + 3, k = i * 6;
                indices[k] = l0;
                indices[k + 1] = l1;
                indices[k + 2] = r0;
                indices[k + 3] = r0;
                indices[k + 4] = l1;
                indices[k + 5] = r1;
            }
        }

        private float HeightOrNearest(int i, int prevKnown)
        {
            if (_known[i]) return _h[i];
            int next = -1;
            for (int k = i + 1; k < _count; k++)
            {
                if (_known[k])
                {
                    next = k;
                    break;
                }
            }
            if (prevKnown >= 0 && next >= 0)
            {
                double t = (_s[i] - _s[prevKnown]) / Math.Max(1e-6, _s[next] - _s[prevKnown]);
                return (float)(_h[prevKnown] + (_h[next] - _h[prevKnown]) * t);
            }
            if (prevKnown >= 0) return _h[prevKnown];
            return next >= 0 ? _h[next] : 0f;
        }

        private void Direction(int a, int b, out double dx, out double dz)
        {
            dx = _x[b] - _x[a];
            dz = _z[b] - _z[a];
            double l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-9)
            {
                dx = 0;
                dz = 1;
                return;
            }
            dx /= l;
            dz /= l;
        }

        private void Add(double x, double z, double s)
        {
            if (_count == _x.Length)
            {
                int cap = _count * 2;
                Array.Resize(ref _x, cap);
                Array.Resize(ref _z, cap);
                Array.Resize(ref _s, cap);
                Array.Resize(ref _h, cap);
                Array.Resize(ref _known, cap);
            }
            _x[_count] = x;
            _z[_count] = z;
            _s[_count] = s;
            _h[_count] = 0f;
            _known[_count] = false;
            _count++;
        }

        private static double Sq(double v)
        {
            return v * v;
        }
    }
}
