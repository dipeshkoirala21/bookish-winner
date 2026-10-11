using System;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>
    /// Meshes of the nature kit (W2_DESIGN 5.8; docs/W2_DETAIL_CONTRACT.md §1.6 and §5): every
    /// <see cref="TreeSpecies"/> at LOD0 (detailed: trunk, limbs, twigs, layered clumps with leafy fringes, bloom; ≤ 1,600 triangles)
    /// and LOD1 (simple: ≤ 240), the shared far LODs per far form (<see cref="FarForms"/>: the crowns of leaves of the
    /// <see cref="TreeShape"/> families, the banana clump, the palm and the bamboo clump; LOD2 volume ≤ 112 triangles, LOD3
    /// impostor ≤ 24, both grey and tinted per instance with <see cref="FloraCatalog.FoliageColour"/>), and the
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
        public static readonly int[] Budget = { 1600, 240, 112, 24 };

        /// <summary>Number of <see cref="TreeShape"/> families with far LODs (Round, Cone, Umbrella, Column, Fountain).</summary>
        public const int Families = 5;

        /// <summary>
        /// Number of far forms (the shared LOD2 volume and LOD3 impostor shapes): the five <see cref="TreeShape"/>
        /// families, then <see cref="PalmForm"/> and <see cref="BambooForm"/>. The Fountain family is the clump of banana
        /// mats (stems from the ground, big arching paddle blades); palms, whose fronds spring from the top of a bare
        /// trunk, and bamboo, a tall vase of culms arching over under feathery plumes, have their own forms. A renderer
        /// that keeps one far mesh per form (<see cref="Far"/>, indexed by <see cref="FarFormOf"/>) draws every far tree
        /// with its own silhouette; one keyed by <see cref="TreeShape"/> (<see cref="Family"/>) draws palms and bamboo
        /// as banana clumps.
        /// </summary>
        public const int FarForms = 7;

        /// <summary>The far form of palms: a crown of arching fronds on a bare ringed trunk.</summary>
        public const int PalmForm = 5;

        /// <summary>The far form of bamboo: a vase of arching culms under feathery leaf plumes.</summary>
        public const int BambooForm = 6;

        /// <summary>The far form a species draws with (<see cref="Far"/>), or −1 for plants without far LODs
        /// (<see cref="TreeShape.Low"/>).</summary>
        public static int FarFormOf(TreeSpecies s)
        {
            if (s == TreeSpecies.Palm) return PalmForm;
            if (s == TreeSpecies.Bamboo) return BambooForm;
            TreeShape f = FloraCatalog.Info(s).Family;
            return f == TreeShape.Low ? -1 : (int)f;
        }

        /// <summary>The stable build seed of a species (the same tree everywhere; instances vary by yaw, scale and tint).</summary>
        public static uint SeedOf(TreeSpecies s)
        {
            return FloraRng.Mix(0x464C4F52u, (uint)s);
        }

        /// <summary>
        /// Build species <paramref name="s"/> at <paramref name="lod"/> (0 or 1; 2 and 3 build its far form,
        /// <see cref="FarFormOf"/>)
        /// for <paramref name="month"/> (1-12) into <paramref name="m"/>. Returns the triangles added.
        /// </summary>
        public static int Build(TreeSpecies s, int lod, int month, MeshData m, bool unit = true)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            FloraInfo info = FloraCatalog.Info(s);
            if (lod >= 2) return info.Family == TreeShape.Low ? Build(s, 1, month, m, unit) : Far(FarFormOf(s), lod, m, unit ? 1f : info.ModelWidthM, unit ? 1f : info.ModelHeightM);
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
        /// The far LOD of a <see cref="TreeShape"/> family (<see cref="Far"/> of the same index): the Fountain family
        /// is the clump of banana and bamboo. Unit model scaled by (<paramref name="width"/>, <paramref name="height"/>).
        /// Returns the triangles added.
        /// </summary>
        public static int Family(TreeShape f, int lod, MeshData m, float width = 1f, float height = 1f)
        {
            if (f != TreeShape.Low) return Far((int)f, lod, m, width, height);
            // Low plants have no far LOD of their own; a small rounded mass stands in (as before the far forms).
            if (m == null) throw new ArgumentNullException(nameof(m));
            int v0 = m.VertexCount, t0 = m.TriangleCount;
            Crown(new FloraBuilder(m), f, lod);
            Scale(m, v0, width, height);
            return m.TriangleCount - t0;
        }

        /// <summary>
        /// A far form (<see cref="FarForms"/>; <see cref="FarFormOf"/> gives a species' form) at <paramref name="lod"/>
        /// 2 (≤ 112 triangles) or 3 (≤ 24). Crowns of leaves (Round, Cone, Umbrella, Column): LOD2 a smooth lumpy volume
        /// on a short trunk, LOD3 a lumpy rounded crown on a three-sided trunk that keeps the silhouette, crown base and
        /// stem at a few dozen pixels. The clump (Fountain: banana): stems from the ground and nine paddle blades arching
        /// up, out and over from low on the clump. The palm: a leaning trunk under a star of arching fronds. The bamboo:
        /// a vase of culms arching over under leaf plumes. At LOD3 each blade or plume is one two-sided triangle over its
        /// arch. Unit model scaled by (<paramref name="width"/>,
        /// <paramref name="height"/>). Foliage is grey (alpha 255) for the instance's crown colour; trunks are fixed bark
        /// (clump stems are tinted a shade darker than the leaves). Returns the triangles added.
        /// </summary>
        public static int Far(int form, int lod, MeshData m, float width = 1f, float height = 1f)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (form < 0 || form >= FarForms) throw new ArgumentOutOfRangeException(nameof(form));
            int v0 = m.VertexCount, t0 = m.TriangleCount;
            var b = new FloraBuilder(m);
            if (form == (int)TreeShape.Fountain) FountainFar.Clump(b, lod);
            else if (form == PalmForm) FountainFar.Palm(b, lod);
            else if (form == BambooForm) FountainFar.Bamboo(b, lod);
            else Crown(b, (TreeShape)form, lod);
            Scale(m, v0, width, height);
            return m.TriangleCount - t0;
        }

        /// <summary>Scale the unit model built from <paramref name="v0"/> by (width, height, width).</summary>
        private static void Scale(MeshData m, int v0, float width, float height)
        {
            if (width == 1f && height == 1f) return;
            float[] pp = m.Positions;
            for (int v = v0; v < m.VertexCount; v++)
            {
                pp[3 * v] *= width;
                pp[3 * v + 1] *= height;
                pp[3 * v + 2] *= width;
            }
        }

        /// <summary>The far LOD of a crown of leaves (Round, Cone, Umbrella, Column).</summary>
        private static void Crown(FloraBuilder b, TreeShape f, int lod)
        {
            MeshData m = b.M;
            float crownY, ry, bottom, top;
            Shape(f, out crownY, out ry, out bottom, out top);
            if (lod <= 2)
            {
                if (f != TreeShape.Low)
                {
                    var st = new FloraBuilder.TubeStyle { Rgb = 0x5E4A3Au, Channel = MaterialChannel.Bark };
                    Vec3[] p = FloraBuilder.Pts(2);
                    float[] r = FloraBuilder.Radii(2);
                    p[0] = new Vec3(0f, -0.02f, 0f);
                    p[1] = new Vec3(0f, bottom + 0.1f, 0f);
                    r[0] = 0.045f;
                    r[1] = 0.03f;
                    b.Tube(p, r, 2, 4, st, 0.5f, 0.8f, false);
                }
                var ps = FloraBuilder.PuffStyle.Foliage(0xFFFFFFu, new Vec3(0f, crownY, 0f), 0.5f);
                ps.ShadeLow = 0.72f;
                ps.ShadeHigh = 1f;
                ps.Bend = 0.2f;
                int puff = m.VertexCount;
                b.Puff(new Vec3(0f, crownY, 0f), new Vec3(0.5f, ry, 0.5f), 1, f == TreeShape.Cone ? 0.15f : 0.2f, 0x464Du + (uint)f, ps);
                // Pines and columns narrow toward the top (one mass, as their mid LOD; a mature chir pine only a little).
                if (f == TreeShape.Cone) b.Taper(puff, 0f, 0f, crownY - 0.5f * ry, top, 0.25f);
                else if (f == TreeShape.Column) b.Taper(puff, 0f, 0f, crownY - 0.5f * ry, top, 0.35f);
            }
            else Impostor(b, f, crownY, ry, bottom, top);
        }

        /// <summary>Points per impostor ring (two rings: 4 × 5 crown triangles plus 3 for the trunk).</summary>
        private const int ImpostorSides = 5;

        /// <summary>
        /// The far impostor (LOD3): a lumpy rounded crown on a trunk, ≤ <see cref="Budget"/>[3] triangles. The crown is
        /// an icosahedron fitted to the family's crown ellipsoid (the roundest closed solid of 20 triangles: a top, two
        /// rings of five half a step apart at ±27° of latitude, a bottom at the crown base), its rings alternately a
        /// little in and out, up and down, so the outline is an irregular round of ten bumps; its normals are the
        /// ellipsoid's, so it shades as one soft ball (sunlit top, dark underside). Pines and columns narrow the upper
        /// ring (a rounded, not a pointed, top), umbrellas widen it. A three-sided trunk rises from the ground into the
        /// crown, so a far tree still stands on a visible stem (23 triangles).
        /// </summary>
        private static void Impostor(FloraBuilder b, TreeShape f, float crownY, float ry, float bottom, float top)
        {
            const float rx = 0.5f;
            var centre = new Vec3(0f, crownY, 0f);
            uint lit = FloraBuilder.Leaf(0xFFFFFFu, 1.04f), side = FloraBuilder.Leaf(0xFFFFFFu, 0.84f), under = FloraBuilder.Leaf(0xFFFFFFu, 0.6f);
            const int n = ImpostorSides;
            // An icosahedron fitted to the crown ellipsoid (the roundest 20-triangle solid: the rings at ±26.6° of
            // latitude), the upper ring narrowed for conifers and columns and widened for umbrellas.
            float eqOff = -0.447f, upR = 1f, upOff = 0.447f;
            if (f == TreeShape.Cone)
            {
                eqOff = -0.47f;
                upR = 0.78f;
            }
            else if (f == TreeShape.Column) upR = 0.8f;
            else if (f == TreeShape.Umbrella)
            {
                eqOff = -0.35f;
                upOff = 0.5f;
            }
            float eqY = crownY + eqOff * ry;
            // Trunk: three faces from the ground up into the crown (apex hidden inside it).
            float apex = 0.5f * (bottom + eqY), tr = 0.03f;
            int ap = b.Vertex(new Vec3(0f, apex, 0f), Vec3.Up, FloraBuilder.Fixed(0x5E4A3Au, 0.9f), MaterialChannel.Bark, 0.6f);
            int[] tb = new int[3];
            for (int i = 0; i < 3; i++)
            {
                double a = i * 2 * Math.PI / 3 + 0.4;
                var d = new Vec3((float)Math.Cos(a), 0f, (float)Math.Sin(a));
                tb[i] = b.Vertex(new Vec3(d.X * tr, -0.03f, d.Z * tr), d, FloraBuilder.Fixed(0x5E4A3Au), MaterialChannel.Bark, 0.45f);
            }
            for (int i = 0; i < 3; i++) b.Tri(ap, tb[i], tb[(i + 1) % 3]);
            // Crown: a shallow underside down to the crown base, the wide ring, the upper ring, the domed top.
            int tv = b.Vertex(new Vec3(0f, top, 0f), Vec3.Up, lit, MaterialChannel.Foliage, 1f);
            int bv = b.Vertex(new Vec3(0f, Math.Max(bottom, crownY - ry), 0f), -Vec3.Up, under, MaterialChannel.Foliage, 0.5f);
            int[] eq = new int[n], up = new int[n];
            for (int k = 0; k < n; k++)
            {
                bool odd = (k & 1) == 1;
                double a = k * 2 * Math.PI / n + 0.26;
                float r = rx * (odd ? 0.92f : 1f), y = eqY + (odd ? 0.05f : -0.05f) * ry;
                eq[k] = CrownVertex(b, new Vec3((float)Math.Cos(a) * r, y, (float)Math.Sin(a) * r), centre, rx, ry, side, 0.8f);
                double a2 = a + Math.PI / n;
                float r2 = rx * upR * (odd ? 0.92f : 1f), y2 = crownY + (upOff + (odd ? -0.05f : 0.05f)) * ry;
                up[k] = CrownVertex(b, new Vec3((float)Math.Cos(a2) * r2, y2, (float)Math.Sin(a2) * r2), centre, rx, ry, FloraBuilder.Leaf(0xFFFFFFu, 0.97f), 0.93f);
            }
            for (int k = 0; k < n; k++)
            {
                int k1 = (k + 1) % n;
                // The upper ring sits half a step round from the wide ring: up[k] between eq[k] and eq[k + 1].
                b.Tri(tv, up[k], up[k1]);
                b.Tri(up[k], eq[k], eq[k1]);
                b.Tri(eq[k1], up[k1], up[k]);
                b.Tri(bv, eq[k1], eq[k]);
            }
        }

        /// <summary>A crown vertex with the crown ellipsoid's normal (leaning up a little: sunlit tops).</summary>
        private static int CrownVertex(FloraBuilder b, Vec3 p, Vec3 centre, float rx, float ry, uint rgba, float ao)
        {
            Vec3 d = p - centre;
            var n = new Vec3(d.X / (rx * rx), d.Y / (ry * ry), d.Z / (rx * rx)).Normalized;
            n = (n + Vec3.Up * 0.15f).Normalized;
            return b.Vertex(p, n, rgba, MaterialChannel.Foliage, ao);
        }

        /// <summary>Unit crown proportions of a family: crown centre height, vertical radius, crown bottom and top.</summary>
        private static void Shape(TreeShape f, out float crownY, out float ry, out float bottom, out float top)
        {
            switch (f)
            {
                case TreeShape.Umbrella: crownY = 0.68f; ry = 0.3f; bottom = 0.4f; top = 0.98f; break;
                case TreeShape.Cone: crownY = 0.69f; ry = 0.31f; bottom = 0.38f; top = 1f; break; // a mature chir pine: rounded, high
                case TreeShape.Column: crownY = 0.69f; ry = 0.31f; bottom = 0.38f; top = 1f; break;
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
