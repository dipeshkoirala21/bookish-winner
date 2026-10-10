using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// A cheap baked ambient occlusion for character meshes (docs/W2_DETAIL_CONTRACT.md §5): the body is approximated by
    /// a dozen capsules (head, neck, torso, arms, legs) and every vertex is darkened by the cosine-weighted solid angle of
    /// the capsules it faces (inner arms against the torso, under the chin, between the legs, the armpits), multiplied
    /// into UV0.v on top of the per-part AO the mesher already wrote (hat rims, mouth interior). Engine-free, no
    /// allocation beyond a small occluder array per call.
    /// </summary>
    public static class CharacterAo
    {
        /// <summary>The darkest the body AO may make a vertex.</summary>
        public const float Floor = 0.42f;

        private struct Capsule
        {
            public V3 A, B;
            public float R;
        }

        /// <summary>Bakes the body AO into <paramref name="m"/>.Uv0 for vertices from <paramref name="first"/> on.</summary>
        public static void Bake(MeshData m, int first, HumanoidSkeleton sk, CharacterRecipe r, int lod)
        {
            if (m == null || sk == null || !m.HasUv0 || m.Uv0 == null) return;
            BodyMetrics bm = sk.Metrics;
            var occ = new Capsule[12];
            int n = 0;
            HumanoidMesher.HeadShape(bm, out V3 hc, out V3 hr);
            occ[n++] = new Capsule { A = hc + new V3(0f, 0.03f, 0f), B = hc - new V3(0f, 0.05f, 0f), R = 0.85f * hr.X };
            occ[n++] = new Capsule { A = new V3(0f, bm.ShoulderY - 0.02f, 0f), B = new V3(0f, bm.HeadBaseY + 0.02f, 0f), R = 0.05f };
            float torsoR = 0.5f * (0.5f * bm.ShoulderW + 0.5f * bm.ChestDepth) * 0.95f;
            occ[n++] = new Capsule { A = new V3(0f, bm.HipJointY + 0.06f, 0f), B = new V3(0f, bm.ShoulderY - 0.08f, 0.005f), R = torsoR };
            for (int side = 0; side < 2; side++)
            {
                int o = side * 4;
                occ[n++] = new Capsule { A = sk.BindPosition[8 + o], B = sk.BindPosition[9 + o], R = 0.045f };
                occ[n++] = new Capsule { A = sk.BindPosition[9 + o], B = sk.BindPosition[10 + o], R = 0.038f };
                occ[n++] = new Capsule { A = sk.BindPosition[23 + o], B = sk.BindPosition[24 + o], R = 0.065f };
                occ[n++] = new Capsule { A = sk.BindPosition[24 + o], B = sk.BindPosition[25 + o], R = 0.045f };
            }
            float strength = lod >= 2 ? 0.7f : 0.85f;
            float[] p = m.Positions, nr = m.Normals, uv = m.Uv0;
            for (int v = first; v < m.VertexCount; v++)
            {
                var pos = new V3(p[v * 3], p[v * 3 + 1], p[v * 3 + 2]);
                var nrm = new V3(nr[v * 3], nr[v * 3 + 1], nr[v * 3 + 2]);
                float sum = 0f;
                for (int c = 0; c < n; c++)
                {
                    V3 q = Closest(occ[c].A, occ[c].B, pos);
                    V3 d = q - pos;
                    float dist = d.Length;
                    float rr = occ[c].R;
                    if (dist <= rr * 1.02f) continue; // on or inside this part: its own surface
                    float facing = V3.Dot(nrm, d) / dist;
                    if (facing <= 0f) continue;
                    float solid = rr * rr / (dist * dist);
                    sum += Math.Min(0.6f, solid * facing);
                }
                // Under the soles and the undersides near the ground.
                if (pos.Y < 0.04f && nrm.Y < -0.3f) sum += 0.3f;
                float ao = 1f - strength * sum;
                if (ao < Floor) ao = Floor;
                uv[v * 2 + 1] *= ao;
            }
        }

        private static V3 Closest(V3 a, V3 b, V3 p)
        {
            V3 ab = b - a;
            float len2 = V3.Dot(ab, ab);
            if (len2 < 1e-10f) return a;
            float t = V3.Dot(p - a, ab) / len2;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return a + ab * t;
        }
    }
}
