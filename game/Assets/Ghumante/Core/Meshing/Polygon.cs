using System;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Small planar polygon routines on parallel coordinate arrays (X east, Z north): signed area, convexity,
    /// centroid and ear-clipping triangulation. Callers pass their own scratch arrays, so nothing allocates.
    /// </summary>
    internal static class Polygon
    {
        /// <summary>Shoelace signed area; positive for counter-clockwise (X east, Z north, seen from above).</summary>
        public static double SignedArea(double[] x, double[] z, int n)
        {
            double a = 0;
            for (int i = 0, j = n - 1; i < n; j = i++) a += x[j] * z[i] - x[i] * z[j];
            return a * 0.5;
        }

        /// <summary>Area centroid (falls back to the vertex mean for a degenerate polygon).</summary>
        public static void Centroid(double[] x, double[] z, int n, out double cx, out double cz)
        {
            double a = 0, sx = 0, sz = 0, ox = x[0], oz = z[0];
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xj = x[j] - ox, zj = z[j] - oz, xi = x[i] - ox, zi = z[i] - oz;
                double c = xj * zi - xi * zj;
                a += c;
                sx += (xj + xi) * c;
                sz += (zj + zi) * c;
            }
            if (Math.Abs(a) < 1e-12)
            {
                cx = cz = 0;
                for (int i = 0; i < n; i++)
                {
                    cx += x[i];
                    cz += z[i];
                }
                cx /= n;
                cz /= n;
                return;
            }
            cx = ox + sx / (3 * a);
            cz = oz + sz / (3 * a);
        }

        /// <summary>True when a counter-clockwise polygon has no reflex vertex (turns sharper than
        /// <paramref name="toleranceDeg"/> to the right count as reflex).</summary>
        public static bool IsConvex(double[] x, double[] z, int n, double toleranceDeg = 3.0)
        {
            double sinTol = Math.Sin(toleranceDeg * Math.PI / 180.0);
            for (int i = 0; i < n; i++)
            {
                int p = i == 0 ? n - 1 : i - 1, q = i == n - 1 ? 0 : i + 1;
                double ax = x[i] - x[p], az = z[i] - z[p], bx = x[q] - x[i], bz = z[q] - z[i];
                double cr = ax * bz - az * bx;
                double l = Math.Sqrt((ax * ax + az * az) * (bx * bx + bz * bz));
                if (cr < -sinTol * l) return false;
            }
            return true;
        }

        /// <summary>
        /// Triangulate a simple counter-clockwise polygon by ear clipping. Writes counter-clockwise index triples
        /// into <paramref name="tris"/> (needs 3·(n - 2) entries) and returns the triangle count. Uses
        /// <paramref name="next"/> and <paramref name="prev"/> (n entries each) as scratch. Convex polygons take a
        /// fan. On a self-intersecting or otherwise degenerate input it still terminates, clipping the best
        /// available corner, so a broken footprint gets a roof instead of a hole.
        /// </summary>
        public static int Triangulate(double[] x, double[] z, int n, int[] tris, int[] next, int[] prev, bool convex)
        {
            if (n < 3) return 0;
            int t = 0;
            if (convex || n == 3)
            {
                for (int i = 1; i < n - 1; i++)
                {
                    tris[t++] = 0;
                    tris[t++] = i;
                    tris[t++] = i + 1;
                }
                return t / 3;
            }
            for (int i = 0; i < n; i++)
            {
                next[i] = i + 1 == n ? 0 : i + 1;
                prev[i] = i == 0 ? n - 1 : i - 1;
            }
            int remaining = n, v = 0, misses = 0;
            while (remaining > 3)
            {
                int p = prev[v], q = next[v];
                bool ear = IsEar(x, z, p, v, q, next);
                if (ear || misses > remaining)
                {
                    // After a full fruitless lap (degenerate input), clip the corner anyway.
                    double cr = Cross(x, z, p, v, q);
                    if (cr > 0 || misses > 2 * remaining || ear)
                    {
                        if (cr > 1e-12)
                        {
                            tris[t++] = p;
                            tris[t++] = v;
                            tris[t++] = q;
                        }
                        next[p] = q;
                        prev[q] = p;
                        remaining--;
                        misses = 0;
                        v = q;
                        continue;
                    }
                }
                misses++;
                v = q;
            }
            int a = prev[v], c = next[v];
            if (Cross(x, z, a, v, c) > 1e-12)
            {
                tris[t++] = a;
                tris[t++] = v;
                tris[t++] = c;
            }
            return t / 3;
        }

        private static double Cross(double[] x, double[] z, int p, int v, int q)
        {
            return (x[v] - x[p]) * (z[q] - z[p]) - (z[v] - z[p]) * (x[q] - x[p]);
        }

        private static bool IsEar(double[] x, double[] z, int p, int v, int q, int[] next)
        {
            if (Cross(x, z, p, v, q) <= 1e-12) return false;
            for (int k = next[q]; k != p; k = next[k])
            {
                if (k == v) continue;
                if (x[k] == x[p] && z[k] == z[p] || x[k] == x[v] && z[k] == z[v] || x[k] == x[q] && z[k] == z[q]) continue;
                if (InTriangle(x[k], z[k], x[p], z[p], x[v], z[v], x[q], z[q])) return false;
            }
            return true;
        }

        private static bool InTriangle(double px, double pz, double ax, double az, double bx, double bz, double cx, double cz)
        {
            double d1 = (bx - ax) * (pz - az) - (bz - az) * (px - ax);
            double d2 = (cx - bx) * (pz - bz) - (cz - bz) * (px - bx);
            double d3 = (ax - cx) * (pz - cz) - (az - cz) * (px - cx);
            return d1 >= 0 && d2 >= 0 && d3 >= 0;
        }
    }
}
