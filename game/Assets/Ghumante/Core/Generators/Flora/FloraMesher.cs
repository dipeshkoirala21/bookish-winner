using System;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>
    /// Meshes of the nature kit (W2_DESIGN 5.8; docs/W2_DETAIL_CONTRACT.md §1.6 and §5): every
    /// <see cref="TreeSpecies"/> at LOD0 (detailed: trunk, limbs, twigs, layered clumps with leafy fringes, bloom; ≤ 1,600 triangles)
    /// and LOD1 (simple: ≤ 240), the shared far LODs per <see cref="TreeShape"/> family (LOD2 volume ≤ 112 triangles,
    /// LOD3 impostor ≤ 8, both grey and tinted per instance with <see cref="FloraCatalog.FoliageColour"/>), and the
    /// chautari platform.
    /// <para>
    /// Kit meshes are <b>unit</b> models: the foot at the origin, height 1 and the largest horizontal extent 1, so an
    /// instance draws at scale (crown, height, crown) and the wind shader's y² sway weight runs from 0 at the ground
    /// to 1 at the top. With <c>unit = false</c> the mesh stays in metres at the catalogue model size (previews).
    /// Every vertex has UV0 (u = <see cref="MaterialChannel"/>, v = baked AO); vertex alpha 255 marks the foliage the
    /// instance tint varies, 0 the fixed parts. Colours depend on the month (bloom, flush, dry grass), so the
    /// renderer rebuilds the species meshes when the month changes. Deterministic; engine-free; allocates only while
    /// building.
    /// </para>
    /// </summary>
    public static class FloraMesher
    {
        /// <summary>LODs: 0 detailed, 1 simple (per species), 2 family volume, 3 family impostor.</summary>
        public const int Lods = 4;

        /// <summary>Triangle cap per LOD.</summary>
        public static readonly int[] Budget = { 1600, 240, 112, 8 };

        /// <summary>Number of <see cref="TreeShape"/> families with far LODs (Round, Cone, Umbrella, Column, Fountain).</summary>
        public const int Families = 5;

        /// <summary>The stable build seed of a species (the same tree everywhere; instances vary by yaw, scale and tint).</summary>
        public static uint SeedOf(TreeSpecies s)
        {
            return FloraRng.Mix(0x464C4F52u, (uint)s);
        }

        /// <summary>
        /// Build species <paramref name="s"/> at <paramref name="lod"/> (0 or 1; 2 and 3 build its family's far LOD)
        /// for <paramref name="month"/> (1-12) into <paramref name="m"/>. Returns the triangles added.
        /// </summary>
        public static int Build(TreeSpecies s, int lod, int month, MeshData m, bool unit = true)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            FloraInfo info = FloraCatalog.Info(s);
            if (lod >= 2) return info.Family == TreeShape.Low ? Build(s, 1, month, m, unit) : Family(info.Family, lod, m, unit ? 1f : info.ModelWidthM, unit ? 1f : info.ModelHeightM);
            if (month < 1 || month > 12) month = 10;
            int v0 = m.VertexCount, t0 = m.TriangleCount;
            var b = new FloraBuilder(m);
            var k = new TreeKit(b, FloraDetail.For(lod), SeedOf(s));
            if (info.Class == FloraClass.Tree) TreeForms.Build(s, k, month);
            else PlantForms.Build(s, k, month);
            if (unit) b.Normalize(v0, info.ModelWidthM, info.ModelHeightM);
            return m.TriangleCount - t0;
        }

        /// <summary>
        /// The far LOD of a family: <paramref name="lod"/> 2 is a smooth lumpy volume with a short trunk (≤ 112 triangles),
        /// 3 an impostor (a closed bipyramid card, ≤ 8 triangles) that keeps the silhouette at a few pixels. Unit model
        /// scaled by (<paramref name="width"/>, <paramref name="height"/>). Foliage is grey (alpha 255) for the
        /// instance's crown colour; the trunk is fixed bark. Returns the triangles added.
        /// </summary>
        public static int Family(TreeShape f, int lod, MeshData m, float width = 1f, float height = 1f)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int v0 = m.VertexCount, t0 = m.TriangleCount;
            var b = new FloraBuilder(m);
            float crownY, ry, bottom, top;
            Shape(f, out crownY, out ry, out bottom, out top);
            uint bark = FloraBuilder.Fixed(0x5E4A3Au);
            if (lod <= 2)
            {
                if (f != TreeShape.Low)
                {
                    float trunkTop = f == TreeShape.Fountain ? crownY : bottom + 0.1f;
                    var st = new FloraBuilder.TubeStyle { Rgb = 0x5E4A3Au, Channel = MaterialChannel.Bark };
                    Vec3[] p = FloraBuilder.Pts(2);
                    float[] r = FloraBuilder.Radii(2);
                    p[0] = new Vec3(0f, -0.02f, 0f);
                    p[1] = new Vec3(0f, trunkTop, 0f);
                    r[0] = f == TreeShape.Fountain ? 0.06f : 0.045f;
                    r[1] = 0.03f;
                    b.Tube(p, r, 2, 4, st, 0.5f, 0.8f, false);
                }
                var ps = FloraBuilder.PuffStyle.Foliage(0xFFFFFFu, new Vec3(0f, crownY, 0f), 0.5f);
                ps.ShadeLow = 0.72f;
                ps.ShadeHigh = 1f;
                ps.Bend = 0.2f;
                int puff = m.VertexCount;
                b.Puff(new Vec3(0f, crownY, 0f), new Vec3(0.5f, ry, 0.5f), 1, f == TreeShape.Cone ? 0.15f : 0.2f, 0x464Du + (uint)f, ps);
                // Conifers and columns narrow toward the top (one mass, as their mid LOD).
                if (f == TreeShape.Cone) b.Taper(puff, 0f, 0f, crownY - 0.5f * ry, top, 0.5f);
                else if (f == TreeShape.Column) b.Taper(puff, 0f, 0f, crownY - 0.5f * ry, top, 0.35f);
            }
            else
            {
                // Impostor: a closed triangular bipyramid (6 triangles; a pyramid of 4 for the cone).
                float mid = f == TreeShape.Cone ? bottom : crownY;
                uint lit = FloraBuilder.Leaf(0xFFFFFFu, 1f), shade = FloraBuilder.Leaf(0xFFFFFFu, 0.7f), dark = FloraBuilder.Leaf(0xFFFFFFu, 0.55f);
                int tv = b.Vertex(new Vec3(0f, top, 0f), Vec3.Up, lit, MaterialChannel.Foliage, 1f);
                int[] e = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    double a = i * 2 * Math.PI / 3 + 0.3;
                    var d = new Vec3((float)Math.Cos(a), 0f, (float)Math.Sin(a));
                    e[i] = b.Vertex(new Vec3(d.X * 0.5f, mid, d.Z * 0.5f), (d + Vec3.Up * 0.3f).Normalized, shade, MaterialChannel.Foliage, 0.8f);
                }
                for (int i = 0; i < 3; i++) b.Tri(tv, e[i], e[(i + 1) % 3]);
                if (f == TreeShape.Cone) b.AddOriented(e[0], e[1], e[2], -Vec3.Up);
                else
                {
                    int bv = b.Vertex(new Vec3(0f, bottom, 0f), -Vec3.Up, dark, MaterialChannel.Foliage, 0.5f);
                    for (int i = 0; i < 3; i++) b.Tri(bv, e[(i + 1) % 3], e[i]);
                }
            }
            if (width != 1f || height != 1f)
            {
                float[] pp = m.Positions;
                for (int v = v0; v < m.VertexCount; v++)
                {
                    pp[3 * v] *= width;
                    pp[3 * v + 1] *= height;
                    pp[3 * v + 2] *= width;
                }
            }
            return m.TriangleCount - t0;
        }

        /// <summary>Unit crown proportions of a family: crown centre height, vertical radius, crown bottom and top.</summary>
        private static void Shape(TreeShape f, out float crownY, out float ry, out float bottom, out float top)
        {
            switch (f)
            {
                case TreeShape.Umbrella: crownY = 0.68f; ry = 0.3f; bottom = 0.4f; top = 0.98f; break;
                case TreeShape.Cone: crownY = 0.66f; ry = 0.34f; bottom = 0.32f; top = 1f; break;
                case TreeShape.Column: crownY = 0.69f; ry = 0.31f; bottom = 0.38f; top = 1f; break;
                case TreeShape.Fountain: crownY = 0.68f; ry = 0.3f; bottom = 0.38f; top = 0.98f; break;
                case TreeShape.Low: crownY = 0.5f; ry = 0.5f; bottom = 0f; top = 1f; break;
                default: crownY = 0.7f; ry = 0.29f; bottom = 0.41f; top = 0.98f; break;
            }
        }

        /// <summary>
        /// The chautari platform under an OSM pipal or bar (W2_DESIGN 5.8): coursed stone walls, a projecting slab
        /// top, the porter ledge on the south (−Z) side with a step and a red-daubed shrine stone. Unit model (top at
        /// 1, width 1 across the square) unless <paramref name="unit"/> is false (6 × 6 m, 0.8 m high).
        /// </summary>
        public static int Chautari(int lod, MeshData m, bool unit = true)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int v0 = m.VertexCount, t0 = m.TriangleCount;
            var b = new FloraBuilder(m);
            PlantForms.Chautari(b, lod <= 0);
            if (unit) b.Normalize(v0, 6f, 0.8f);
            return m.TriangleCount - t0;
        }
    }
}
