using System;

namespace Ghumante.Core.Meshing.Shapes
{
    /// <summary>Knobs of <see cref="ShapeAo.Bake"/>. Start from <see cref="ShapeAo.Defaults"/>.</summary>
    public struct AoSettings
    {
        /// <summary>Height of the ground the object stands on (mesh coordinates).</summary>
        public double GroundY;

        /// <summary>Height over which ground-contact darkening fades out (metres; 0 = off).</summary>
        public double GroundFade;

        /// <summary>AO right at the ground (0..1).</summary>
        public float GroundAo;

        /// <summary>Darkening of faces that look down (0..1 at a normal pointing straight down).</summary>
        public float Underside;

        /// <summary>Strength of the concavity term (inner corners, creases between neighbours).</summary>
        public float Concavity;

        /// <summary>Rays per vertex for the optional occlusion test (0 = off; 6-12 is plenty).</summary>
        public int Rays;

        /// <summary>Ray length in metres: occluders further away do not count.</summary>
        public double RayDistance;

        /// <summary>Strength of the ray term (0..1).</summary>
        public float RayStrength;

        /// <summary>The ray test runs only when vertices × triangles × rays stays under this (small meshes).</summary>
        public long RayBudget;
    }

    /// <summary>
    /// A cheap ambient-occlusion bake into <c>Uv0.v</c> (docs/W2_DETAIL_CONTRACT.md §5: 1 = open, 0 = occluded),
    /// multiplied into whatever is there so several bakes and the brush AO combine. Heuristics: ground contact
    /// (darker near the ground), undersides (faces looking down), concavity (a vertex whose neighbours rise above
    /// its tangent plane sits in a crease) and, for small meshes, a few-ray hemisphere test against the range's own
    /// triangles. Deterministic, no allocation after warm-up.
    /// </summary>
    public static class ShapeAo
    {
        /// <summary>Settings for an object standing on <paramref name="groundY"/>; rays off.</summary>
        public static AoSettings Defaults(double groundY = 0)
        {
            return new AoSettings
            {
                GroundY = groundY,
                GroundFade = 0.35,
                GroundAo = 0.7f,
                Underside = 0.25f,
                Concavity = 0.6f,
                Rays = 0,
                RayDistance = 0.5,
                RayStrength = 0.6f,
                RayBudget = 4000000,
            };
        }

        /// <summary>Make sure the mesh has UV0: if not, every existing vertex gets u = Plain, v = 1 (open).</summary>
        public static void EnsureUv(MeshData m)
        {
            if (m.HasUv0) return;
            for (int v = 0; v < m.VertexCount; v++)
            {
                m.Uv0[2 * v] = 0f;
                m.Uv0[2 * v + 1] = 1f;
            }
            m.HasUv0 = true;
        }

        [ThreadStatic] private static double[] s_sum;
        [ThreadStatic] private static int[] s_cnt;

        /// <summary>Bake AO for vertices [firstVertex, firstVertex + vertexCount) using the triangles
        /// [firstIndex, firstIndex + indexCount) (pass -1 counts for "to the end").</summary>
        public static void Bake(MeshData m, int firstVertex, int vertexCount, int firstIndex, int indexCount, in AoSettings s)
        {
            if (vertexCount < 0) vertexCount = m.VertexCount - firstVertex;
            if (indexCount < 0) indexCount = m.IndexCount - firstIndex;
            if (vertexCount <= 0) return;
            EnsureUv(m);
            float[] p = m.Positions, n = m.Normals, uv = m.Uv0;
            // Concavity: mean elevation of the opposite edge midpoints above each vertex's tangent plane.
            double[] sum = null;
            int[] cnt = null;
            if (s.Concavity > 0)
            {
                if (s_sum == null || s_sum.Length < vertexCount) s_sum = new double[Math.Max(vertexCount, 256)];
                if (s_cnt == null || s_cnt.Length < vertexCount) s_cnt = new int[Math.Max(vertexCount, 256)];
                sum = s_sum;
                cnt = s_cnt;
                Array.Clear(sum, 0, vertexCount);
                Array.Clear(cnt, 0, vertexCount);
                for (int t = firstIndex; t + 2 < firstIndex + indexCount; t += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int v = m.Indices[t + k], a = m.Indices[t + (k + 1) % 3], b = m.Indices[t + (k + 2) % 3];
                        int i = v - firstVertex;
                        if (i < 0 || i >= vertexCount) continue;
                        double mx = 0.5 * (p[3 * a] + p[3 * b]) - p[3 * v], my = 0.5 * (p[3 * a + 1] + p[3 * b + 1]) - p[3 * v + 1];
                        double mz = 0.5 * (p[3 * a + 2] + p[3 * b + 2]) - p[3 * v + 2];
                        double l = Math.Sqrt(mx * mx + my * my + mz * mz);
                        if (l < 1e-9) continue;
                        sum[i] += (mx * n[3 * v] + my * n[3 * v + 1] + mz * n[3 * v + 2]) / l;
                        cnt[i]++;
                    }
                }
            }
            int tris = indexCount / 3;
            bool rays = s.Rays > 0 && s.RayDistance > 0 && (long)vertexCount * tris * s.Rays <= s.RayBudget;
            for (int i = 0; i < vertexCount; i++)
            {
                int v = firstVertex + i;
                double ao = 1;
                if (s.GroundFade > 0)
                {
                    double f = (p[3 * v + 1] - s.GroundY) / s.GroundFade;
                    f = f < 0 ? 0 : f > 1 ? 1 : f;
                    f = f * f * (3 - 2 * f);
                    ao *= s.GroundAo + (1 - s.GroundAo) * f;
                }
                double ny = n[3 * v + 1];
                if (ny < 0 && s.Underside > 0) ao *= 1 - s.Underside * -ny;
                if (cnt != null && cnt[i] > 0)
                {
                    double c = sum[i] / cnt[i];
                    if (c > 0) ao *= 1 - s.Concavity * Math.Min(1, c * 1.5);
                }
                if (rays) ao *= 1 - s.RayStrength * RayOcclusion(m, v, firstIndex, indexCount, s.Rays, s.RayDistance);
                if (ao < 0) ao = 0;
                uv[2 * v + 1] = (float)(uv[2 * v + 1] * ao);
            }
        }

        /// <summary>Fraction (0..1, distance-weighted) of hemisphere rays from vertex v blocked within
        /// <paramref name="dist"/> by the triangles of the range.</summary>
        private static double RayOcclusion(MeshData m, int v, int firstIndex, int indexCount, int rays, double dist)
        {
            float[] p = m.Positions, n = m.Normals;
            double nx = n[3 * v], ny = n[3 * v + 1], nz = n[3 * v + 2];
            double ox = p[3 * v] + nx * 1e-3, oy = p[3 * v + 1] + ny * 1e-3, oz = p[3 * v + 2] + nz * 1e-3;
            // Tangent basis.
            double tx, ty, tz;
            if (Math.Abs(ny) < 0.9)
            {
                tx = nz;
                ty = 0;
                tz = -nx;
            }
            else
            {
                tx = 0;
                ty = -nz;
                tz = ny;
            }
            double tl = Math.Sqrt(tx * tx + ty * ty + tz * tz);
            tx /= tl;
            ty /= tl;
            tz /= tl;
            double bx = ny * tz - nz * ty, by = nz * tx - nx * tz, bz = nx * ty - ny * tx;
            double occ = 0;
            for (int r = 0; r < rays; r++)
            {
                // Golden-angle spiral, cosine-weighted hemisphere.
                double u = (r + 0.5) / rays, phi = r * 2.399963229728653;
                double sr = Math.Sqrt(u), cz = Math.Sqrt(1 - u);
                double lx = sr * Math.Cos(phi), ly = sr * Math.Sin(phi);
                double dx = tx * lx + bx * ly + nx * cz, dy = ty * lx + by * ly + ny * cz, dz = tz * lx + bz * ly + nz * cz;
                double hit = Cast(m, ox, oy, oz, dx, dy, dz, firstIndex, indexCount, dist);
                if (hit < dist) occ += 1 - hit / dist * 0.5;
            }
            return occ / rays;
        }

        private static double Cast(MeshData m, double ox, double oy, double oz, double dx, double dy, double dz, int firstIndex, int indexCount, double best)
        {
            float[] p = m.Positions;
            int[] ix = m.Indices;
            for (int t = firstIndex; t + 2 < firstIndex + indexCount; t += 3)
            {
                int a = ix[t], b = ix[t + 1], c = ix[t + 2];
                double ax = p[3 * a], ay = p[3 * a + 1], az = p[3 * a + 2];
                double e1x = p[3 * b] - ax, e1y = p[3 * b + 1] - ay, e1z = p[3 * b + 2] - az;
                double e2x = p[3 * c] - ax, e2y = p[3 * c + 1] - ay, e2z = p[3 * c + 2] - az;
                double px = dy * e2z - dz * e2y, py = dz * e2x - dx * e2z, pz = dx * e2y - dy * e2x;
                double det = e1x * px + e1y * py + e1z * pz;
                if (det > -1e-12 && det < 1e-12) continue;
                double inv = 1 / det;
                double sx = ox - ax, sy = oy - ay, sz = oz - az;
                double u = (sx * px + sy * py + sz * pz) * inv;
                if (u < 0 || u > 1) continue;
                double qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
                double w = (dx * qx + dy * qy + dz * qz) * inv;
                if (w < 0 || u + w > 1) continue;
                double tt = (e2x * qx + e2y * qy + e2z * qz) * inv;
                if (tt > 1e-4 && tt < best) best = tt;
            }
            return best;
        }
    }
}
