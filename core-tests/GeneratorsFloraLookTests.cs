using System;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Reproductions of the reviewer's findings on the far plants and the marigold beds (docs/research/w2/
    /// ref_assets-b.md §1-§2), on the public build API only (<see cref="FloraMesher.Build"/>,
    /// <see cref="FloraMesher.Family"/>), so the same checks run against the code before and after the fix:
    /// palms, bananas and bamboo must stay blades and plumes at every far LOD (they turned into round broadleaf crowns
    /// on a stick at LOD2/LOD3), and a sayapatri bed at Tihar must read as a carpet of orange pom-poms (about 15 % of it
    /// read as flower).
    /// </summary>
    public class GeneratorsFloraLookTests
    {
        private static readonly TreeSpecies[] Fountains = { TreeSpecies.Palm, TreeSpecies.Banana, TreeSpecies.Bamboo };

        /// <summary>
        /// The far LODs of palms, bananas and bamboo keep their own silhouettes, never a round crown on a stick
        /// (ref_assets-b.md §1): from afar (side outlines from four bearings at a few dozen pixels) each far LOD matches
        /// the species' detailed model clearly better than a round broadleaf crown does (palm, banana), or at least as
        /// well (bamboo, whose broad crown on a vase of culms a blurred ball also covers); its crown is blades and plumes
        /// with gaps between them like the detailed model's (row solidity within 0.15 of LOD0's, where a round crown is
        /// solid); the blades reach the crown's edge; the palm's fronds spring from the top of a bare trunk, the banana's
        /// leaves start low on its stems and the bamboo's sprays where the detailed clump's do.
        /// </summary>
        [Test]
        public void PalmsBananasAndBambooStayBladesAtEveryFarLod()
        {
            var report = new System.Text.StringBuilder();
            var bad = new System.Collections.Generic.List<string>();
            for (int lod = 2; lod < 4; lod++)
            {
                var round = new MeshData();
                FloraMesher.Family(TreeShape.Round, lod, round);
                float roundSolid = FloraSilhouette.RowSolidity(round, 0.45f);
                float[] roundCover = FloraSilhouette.Coverage(round);
                report.AppendFormat("round crown LOD{0}: solidity {1:0.00}\n", lod, roundSolid);
                Assert.That(roundSolid, Is.GreaterThan(0.9f), "a round crown is solid");
                foreach (TreeSpecies sp in Fountains)
                {
                    var m0 = new MeshData();
                    FloraMesher.Build(sp, 0, 10, m0);
                    float[] c0 = FloraSilhouette.Coverage(m0);
                    float solid0 = FloraSilhouette.RowSolidity(m0, 0.45f);
                    float base0 = LeafBase(m0, 0.03f);
                    var own = new MeshData();
                    int tris = FloraMesher.Build(sp, lod, 10, own);
                    float iouOwn = FloraSilhouette.SoftIoU(c0, FloraSilhouette.Coverage(own)), iouBall = FloraSilhouette.SoftIoU(c0, roundCover);
                    float solid = FloraSilhouette.RowSolidity(own, 0.45f);
                    float minY = LeafBase(own, 0f), maxR = 0f;
                    for (int v = 0; v < own.VertexCount; v++)
                    {
                        if (own.Uv0[2 * v] != (float)MaterialChannel.Foliage) continue;
                        float x = own.Positions[3 * v], z = own.Positions[3 * v + 2];
                        maxR = Math.Max(maxR, (float)Math.Sqrt(x * x + z * z));
                    }
                    report.AppendFormat("{0,-7} LOD{1}: {2,3} tris, outline vs LOD0 {3:0.00} (round crown {4:0.00}), solidity {5:0.00} (LOD0 {6:0.00}), leaves from y {7:0.00} (LOD0 {8:0.00}), reach {9:0.00}\n",
                                        sp, lod, tris, iouOwn, iouBall, solid, solid0, minY, base0, maxR);
                    string what = sp + " LOD" + lod;
                    // (Bamboo: a blurred ball also covers its broad crown on a vase of culms, so "about as well", within
                    // 0.02; its solidity and leaf base below tell it from a ball on a stick.)
                    float margin = sp == TreeSpecies.Bamboo ? -0.02f : lod == 2 ? 0.1f : 0.03f;
                    if (iouOwn < iouBall + margin) bad.Add(what + ": outline " + iouOwn.ToString("0.00") + " vs a round crown's " + iouBall.ToString("0.00"));
                    if (Math.Abs(solid - solid0) > 0.15f) bad.Add(what + ": solidity " + solid.ToString("0.00") + " vs LOD0's " + solid0.ToString("0.00"));
                    if (solid >= roundSolid - 0.15f) bad.Add(what + ": solid crown " + solid.ToString("0.00") + " (blades with gaps expected)");
                    if (maxR <= 0.4f) bad.Add(what + ": blades do not reach the crown edge (" + maxR.ToString("0.00") + ")");
                    if (sp == TreeSpecies.Palm && minY <= 0.45f) bad.Add(what + ": palm leaves from y " + minY.ToString("0.00") + " (fronds spring from the top of a bare trunk)");
                    if (sp == TreeSpecies.Banana && minY >= 0.5f) bad.Add(what + ": banana leaves only from y " + minY.ToString("0.00") + " (they start low on the stems)");
                    if (sp == TreeSpecies.Bamboo && Math.Abs(minY - base0) > 0.12f)
                        bad.Add(what + ": bamboo sprays from y " + minY.ToString("0.00") + " vs LOD0's " + base0.ToString("0.00"));
                }
            }
            TestContext.WriteLine(report.ToString());
            Assert.That(bad, Is.Empty, string.Join("; ", bad));
        }

        /// <summary>The height below which a fraction <paramref name="q"/> of a model's foliage vertices lie (q = 0: the
        /// lowest leaf).</summary>
        private static float LeafBase(MeshData m, float q)
        {
            var ys = new System.Collections.Generic.List<float>();
            for (int v = 0; v < m.VertexCount; v++)
                if (m.Uv0[2 * v] == (float)MaterialChannel.Foliage) ys.Add(m.Positions[3 * v + 1]);
            ys.Sort();
            return ys.Count == 0 ? 0f : ys[(int)(q * (ys.Count - 1))];
        }

        private static readonly MaterialChannel[] BedSurface = { MaterialChannel.Foliage, MaterialChannel.Dirt };

        /// <summary>
        /// A sayapatri (African marigold) bed at Tihar is a dense carpet of orange and yellow pom-poms over the green
        /// (ref_assets-b.md §2; refs marigold_np 05, marigold_field 00, 07): seen from above, at least 60 % of the bed
        /// (foliage and soil inside the brick edge) is bloom at LOD0 and at least 45 % at the far LOD1; walking past
        /// (30° down) the bloom still covers half of it; in the rest of the year (bloom 0.7) it thins but stays a
        /// flower bed.
        /// </summary>
        [Test]
        public void MarigoldBedIsACarpetOfPompomsAtTihar()
        {
            var report = new System.Text.StringBuilder();
            var bad = new System.Collections.Generic.List<string>();
            foreach (int month in new[] { 10, 11, 7 })
                for (int lod = 0; lod < 2; lod++)
                {
                    var m = new MeshData();
                    int tris = FloraMesher.Build(TreeSpecies.MarigoldBed, lod, month, m, false);
                    double top = FloraSilhouette.ViewShare(m, 0, 90, 260, BedSurface, FloraSilhouette.IsBloom);
                    double side = FloraSilhouette.ViewShare(m, 20, 30, 260, BedSurface, FloraSilhouette.IsBloom);
                    report.AppendFormat("month {0,2} LOD{1}: {2,4} tris, bloom from above {3:P0}, from 30° {4:P0}\n", month, lod, tris, top, side);
                    bool tihar = month == 10 || month == 11;
                    double wantTop = tihar ? (lod == 0 ? 0.6 : 0.45) : 0.3, wantSide = tihar ? (lod == 0 ? 0.5 : 0.4) : 0.25;
                    if (top < wantTop) bad.Add("month " + month + " LOD" + lod + " from above " + top.ToString("P0") + " < " + wantTop.ToString("P0"));
                    if (side < wantSide) bad.Add("month " + month + " LOD" + lod + " from 30° " + side.ToString("P0") + " < " + wantSide.ToString("P0"));
                }
            TestContext.WriteLine(report.ToString());
            Assert.That(bad, Is.Empty, string.Join("; ", bad));
        }

        /// <summary>Instance tint as the renderer applies it: vertices with tint weight (alpha 255) take
        /// <paramref name="rgb"/> times their grey.</summary>
        private static void Tint(MeshData m, uint rgb)
        {
            int r = (int)(rgb >> 16) & 0xFF, g = (int)(rgb >> 8) & 0xFF, b = (int)rgb & 0xFF;
            for (int v = 0; v < m.VertexCount; v++)
            {
                if (m.Colors[4 * v + 3] != 255) continue;
                m.Colors[4 * v] = (byte)(m.Colors[4 * v] * r / 255);
                m.Colors[4 * v + 1] = (byte)(m.Colors[4 * v + 1] * g / 255);
                m.Colors[4 * v + 2] = (byte)(m.Colors[4 * v + 2] * b / 255);
            }
        }

        /// <summary>Preview dumps (GHUMANTE_PREVIEW_DIR): each fountain species at LOD0-3 side by side at model size,
        /// tinted with its October crown colour (flora_look/lods_&lt;species&gt;.obj, and each level alone), and the
        /// marigold bed at LOD0 and LOD1 in October and July.</summary>
        [Test]
        public void DumpFarPlantsAndMarigoldBeds()
        {
            if (FloraObj.Dir == null) Assert.Ignore("GHUMANTE_PREVIEW_DIR not set");
            foreach (TreeSpecies sp in Fountains)
            {
                FloraInfo info = FloraCatalog.Info(sp);
                var row = new MeshData();
                for (int lod = 0; lod < FloraMesher.Lods; lod++)
                {
                    var m = new MeshData();
                    FloraMesher.Build(sp, lod, 10, m, false);
                    if (lod >= 2) Tint(m, FloraCatalog.FoliageColour(sp, 10));
                    FloraObj.Write(m, "flora_look/" + sp + "_lod" + lod + ".obj");
                    FloraObj.Append(row, m, lod * 1.5f * info.ModelWidthM, 0f, 0f);
                }
                FloraObj.Write(row, "flora_look/lods_" + sp + ".obj");
                // What these plants drew far away before their own far forms: the round broadleaf volume.
                var ball = new MeshData();
                FloraMesher.Family(TreeShape.Round, 2, ball, info.ModelWidthM, info.ModelHeightM);
                Tint(ball, FloraCatalog.FoliageColour(sp, 10));
                FloraObj.Write(ball, "flora_look/" + sp + "_round_lod2.obj");
            }
            foreach (int month in new[] { 10, 7 })
                for (int lod = 0; lod < 2; lod++)
                {
                    var m = new MeshData();
                    FloraMesher.Build(TreeSpecies.MarigoldBed, lod, month, m, false);
                    FloraObj.Write(m, "flora_look/marigold_m" + month + "_lod" + lod + ".obj");
                }
        }
    }
}
