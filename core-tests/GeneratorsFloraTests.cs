using System;
using System.Collections.Generic;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class GeneratorsFloraTests
    {
        private static void AssertFloraMesh(MeshData m, int first, string what)
        {
            Assert.That(m.HasUv0, Is.True, what + ": UV0 (channel, AO)");
            Assert.That(m.IndexCount % 3, Is.EqualTo(0), what);
            for (int v = first; v < m.VertexCount; v++)
            {
                for (int k = 0; k < 3; k++)
                {
                    Assert.That(MeshingChecks.IsFinite(m.Positions[3 * v + k]), what + " position " + v);
                    Assert.That(MeshingChecks.IsFinite(m.Normals[3 * v + k]), what + " normal " + v);
                }
                double len = Math.Sqrt(m.Normals[3 * v] * m.Normals[3 * v] + m.Normals[3 * v + 1] * m.Normals[3 * v + 1] + m.Normals[3 * v + 2] * m.Normals[3 * v + 2]);
                Assert.That(len, Is.EqualTo(1.0).Within(1e-3), what + " unit normal " + v);
                float ch = m.Uv0[2 * v], ao = m.Uv0[2 * v + 1];
                Assert.That(ch, Is.EqualTo(Math.Round(ch)), what + " channel is whole");
                Assert.That(Enum.IsDefined(typeof(MaterialChannel), (byte)ch), what + " channel " + ch);
                Assert.That(ao, Is.InRange(0.05f, 1f), what + " AO " + v);
                byte a = m.Colors[4 * v + 3];
                Assert.That(a == 0 || a == 255, what + " tint weight is 0 or 255");
            }
            for (int i = 0; i < m.IndexCount; i++) Assert.That(m.Indices[i], Is.InRange(0, m.VertexCount - 1), what);
        }

        [Test]
        public void EverySpeciesBuildsBothLodsWithinBudgetWithChannelsAndAo()
        {
            var m = new MeshData();
            var over = new System.Text.StringBuilder();
            for (int s = 0; s < FloraCatalog.Count; s++)
                for (int lod = 0; lod < 2; lod++)
                    foreach (int month in new[] { 1, 4, 10 })
                    {
                        m.Clear();
                        var sp = (TreeSpecies)s;
                        int tris = FloraMesher.Build(sp, lod, month, m);
                        string what = sp + " LOD" + lod + " month " + month;
                        Assert.That(tris, Is.GreaterThan(lod == 0 ? 20 : 8), what);
                        if (tris > FloraMesher.Budget[lod]) over.Append(what).Append(": ").Append(tris).Append("; ");
                        Assert.That(tris, Is.EqualTo(m.TriangleCount));
                        AssertFloraMesh(m, 0, what);
                        if (lod == 1)
                        {
                            var m0 = new MeshData();
                            Assert.That(FloraMesher.Build(sp, 0, month, m0), Is.GreaterThan(tris), what + ": LOD1 is lighter than LOD0");
                        }
                    }
            Assert.That(over.ToString(), Is.Empty, "over the triangle budget");
        }

        [Test]
        public void UnitModelsStandOnTheGroundInsideTheUnitBox()
        {
            var m = new MeshData();
            var bad = new System.Text.StringBuilder();
            for (int s = 0; s < FloraCatalog.Count; s++)
            {
                m.Clear();
                var sp = (TreeSpecies)s;
                FloraMesher.Build(sp, 0, 10, m);
                float x0, y0, z0, x1, y1, z1;
                m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                float wide = Math.Max(x1 - x0, z1 - z0);
                // The top near 1, the foot at the ground (slightly sunk), the widest extent near 1, centred: the
                // wind weight (y²) then runs from the still foot to the swaying top.
                if (y1 < 0.8f || y1 > 1.2f || y0 < -0.12f || y0 > 0.05f || wide < 0.55f || wide > 1.5f || Math.Abs(x0 + x1) * 0.5f > 0.3f)
                    bad.Append(sp).Append(" y ").Append(y0.ToString("0.00")).Append("..").Append(y1.ToString("0.00")).Append(" wide ").Append(wide.ToString("0.00"))
                       .Append(" cx ").Append(((x0 + x1) * 0.5f).ToString("0.00")).Append("; ");
            }
            Assert.That(bad.ToString(), Is.Empty);
        }

        [Test]
        public void BuildsAreDeterministic()
        {
            foreach (TreeSpecies sp in new[] { TreeSpecies.Pipal, TreeSpecies.Jacaranda, TreeSpecies.ChirPine, TreeSpecies.Bamboo, TreeSpecies.MarigoldBed, TreeSpecies.Boulder })
            {
                var a = new MeshData();
                var b = new MeshData();
                FloraMesher.Build(sp, 0, 4, a);
                FloraMesher.Build(sp, 0, 4, b);
                Assert.That(a.VertexCount, Is.EqualTo(b.VertexCount), sp.ToString());
                for (int i = 0; i < a.VertexCount * 3; i++) Assert.That(a.Positions[i], Is.EqualTo(b.Positions[i]), sp + " position " + i);
                for (int i = 0; i < a.VertexCount * 4; i++) Assert.That(a.Colors[i], Is.EqualTo(b.Colors[i]), sp + " colour " + i);
                for (int i = 0; i < a.IndexCount; i++) Assert.That(a.Indices[i], Is.EqualTo(b.Indices[i]), sp + " index " + i);
            }
        }

        [Test]
        public void FrontFacesAgreeWithTheNormals()
        {
            var m = new MeshData();
            var report = new System.Text.StringBuilder();
            for (int s = 0; s < FloraCatalog.Count; s++)
                for (int lod = 0; lod < 2; lod++)
                {
                    m.Clear();
                    FloraMesher.Build((TreeSpecies)s, lod, 4, m, false);
                    int bad = 0, n = 0;
                    for (int t = 0; t < m.TriangleCount; t++)
                    {
                        double nx, ny, nz;
                        MeshingChecks.Facet(m, t, out nx, out ny, out nz);
                        double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        if (len < 1e-9) continue;
                        double sum = 0;
                        for (int k = 0; k < 3; k++)
                        {
                            int v = m.Indices[3 * t + k];
                            sum += (nx * m.Normals[3 * v] + ny * m.Normals[3 * v + 1] + nz * m.Normals[3 * v + 2]) / len;
                        }
                        n++;
                        if (sum < 0) bad++;
                    }
                    if (bad > 0) report.Append((TreeSpecies)s).Append(" LOD").Append(lod).Append(": ").Append(bad).Append('/').Append(n).Append("; ");
                }
            Assert.That(report.ToString(), Is.Empty, "triangles facing away from their normals");
        }

        [Test]
        public void SeasonsChangeTheBloom()
        {
            // Jacaranda: violet in April, green in August.
            Assert.That(Violet(TreeSpecies.Jacaranda, 4), Is.GreaterThan(0.3), "April jacaranda is violet");
            Assert.That(Violet(TreeSpecies.Jacaranda, 8), Is.LessThan(0.02), "August jacaranda is green");
            Assert.That(Red(TreeSpecies.Rhododendron, 3), Is.GreaterThan(0.08), "laligurans in March");
            Assert.That(Red(TreeSpecies.Rhododendron, 8), Is.LessThan(0.01));
            Assert.That(Red(TreeSpecies.Poinsettia, 12), Is.GreaterThan(0.05), "lalupate at Tihar and winter");
            Assert.That(FloraCatalog.FoliageColour(TreeSpecies.Jacaranda, 4), Is.Not.EqualTo(FloraCatalog.FoliageColour(TreeSpecies.Jacaranda, 8)));
            Assert.That(FloraCatalog.InSeason(TreeSpecies.StrawStack, 11), Is.True);
            Assert.That(FloraCatalog.InSeason(TreeSpecies.StrawStack, 7), Is.False);
        }

        private static double Violet(TreeSpecies s, int month)
        {
            return Share(s, month, (r, g, b) => b > g + 20 && r > g);
        }

        private static double Red(TreeSpecies s, int month)
        {
            return Share(s, month, (r, g, b) => r > 150 && g < 90 && b < 110);
        }

        private static double Share(TreeSpecies s, int month, Func<int, int, int, bool> test)
        {
            var m = new MeshData();
            FloraMesher.Build(s, 0, month, m);
            int hit = 0;
            for (int v = 0; v < m.VertexCount; v++)
                if (test(m.Colors[4 * v], m.Colors[4 * v + 1], m.Colors[4 * v + 2])) hit++;
            return (double)hit / Math.Max(1, m.VertexCount);
        }

        [Test]
        public void FamilyFarLodsAreCheapAndGrey()
        {
            var m = new MeshData();
            for (int f = 0; f < FloraMesher.Families; f++)
                for (int lod = 2; lod < 4; lod++)
                {
                    m.Clear();
                    int tris = FloraMesher.Family((TreeShape)f, lod, m);
                    Assert.That(tris, Is.InRange(3, FloraMesher.Budget[lod]), (TreeShape)f + " LOD" + lod);
                    AssertFloraMesh(m, 0, (TreeShape)f + " LOD" + lod);
                    float x0, y0, z0, x1, y1, z1;
                    m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                    Assert.That(y1, Is.InRange(0.85f, 1.1f));
                    // Foliage is grey (R = G = B) so the instance tint gives it the species colour.
                    for (int v = 0; v < m.VertexCount; v++)
                        if (m.Colors[4 * v + 3] == 255)
                        {
                            Assert.That(m.Colors[4 * v], Is.EqualTo(m.Colors[4 * v + 1]));
                            Assert.That(m.Colors[4 * v + 1], Is.EqualTo(m.Colors[4 * v + 2]));
                        }
                }
            m.Clear();
            Assert.That(FloraMesher.Chautari(0, m), Is.InRange(100, 600));
            AssertFloraMesh(m, 0, "chautari");
        }

        // -------------------------------------------------------------------------------------------------------------
        // Visual self-check dumps (GHUMANTE_PREVIEW_DIR=/home/user/wt/previews/nature/obj dotnet test --filter Dump)

        private static readonly TreeSpecies[] Trees =
        {
            TreeSpecies.Pipal, TreeSpecies.Bar, TreeSpecies.Jacaranda, TreeSpecies.SilkyOak, TreeSpecies.Bottlebrush, TreeSpecies.Camphor,
            TreeSpecies.Eucalyptus, TreeSpecies.Bamboo, TreeSpecies.Palm, TreeSpecies.Schima, TreeSpecies.Castanopsis, TreeSpecies.Alnus,
            TreeSpecies.ChirPine, TreeSpecies.Oak, TreeSpecies.Rhododendron, TreeSpecies.BrownOak, TreeSpecies.Sal, TreeSpecies.Banana, TreeSpecies.Broadleaf,
        };

        /// <summary>
        /// The LODs of a tree keep its silhouette so the switch does not pop: the near-mid model (LOD1) and the family
        /// volume (LOD2) span about the width, height and crown base of the detailed model (LOD0), measured robustly
        /// on the foliage vertices (the width from the 92nd percentile of their distance from the axis, the top and
        /// the crown base from the 97th and 3rd percentiles of their height).
        /// </summary>
        [Test]
        public void LodsKeepTheSilhouette()
        {
            var report = new System.Text.StringBuilder();
            var bad = new List<string>();
            foreach (TreeSpecies sp in Trees)
            {
                float[] w = new float[3], top = new float[3], bottom = new float[3];
                for (int lod = 0; lod < 3; lod++)
                {
                    var m = new MeshData();
                    FloraMesher.Build(sp, lod, 10, m);
                    var rad = new List<float>();
                    var ys = new List<float>();
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        if (m.Uv0[2 * v] != (float)MaterialChannel.Foliage) continue;
                        float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                        rad.Add((float)Math.Sqrt(x * x + z * z));
                        ys.Add(y);
                    }
                    rad.Sort();
                    ys.Sort();
                    // Robust extents: a stray leaf card does not count.
                    w[lod] = 2f * rad[(int)(0.92f * (rad.Count - 1))];
                    top[lod] = ys[(int)(0.97f * (ys.Count - 1))];
                    bottom[lod] = ys[(int)(0.03f * (ys.Count - 1))];
                }
                report.AppendFormat("{0,-12} w {1:0.00} {2:0.00} {3:0.00}  top {4:0.00} {5:0.00} {6:0.00}  base {7:0.00} {8:0.00} {9:0.00}\n", sp, w[0], w[1], w[2],
                                    top[0], top[1], top[2], bottom[0], bottom[1], bottom[2]);
                // (Banana: its leaves rise from the ground, which no shared Fountain volume follows; it is a garden plant.)
                for (int lod = 1; lod < (sp == TreeSpecies.Banana ? 2 : 3); lod++)
                {
                    // (Banana: the paddle leaves sample their length evenly, so the vertex percentile is no width.)
                    if (sp != TreeSpecies.Banana && Math.Abs(w[lod] - w[0]) > 0.2f * w[0]) bad.Add(sp + " LOD" + lod + " width " + w[lod].ToString("0.00") + " vs " + w[0].ToString("0.00"));
                    if (Math.Abs(top[lod] - top[0]) > 0.1f) bad.Add(sp + " LOD" + lod + " top " + top[lod].ToString("0.00") + " vs " + top[0].ToString("0.00"));
                    // The family volume (LOD2) is shared by the species of a family: its crown base may differ more.
                    if (Math.Abs(bottom[lod] - bottom[0]) > (lod == 1 ? 0.12f : 0.22f)) bad.Add(sp + " LOD" + lod + " crown base " + bottom[lod].ToString("0.00") + " vs " + bottom[0].ToString("0.00"));
                }
            }
            TestContext.WriteLine(report.ToString());
            Assert.That(bad, Is.Empty, string.Join("; ", bad));
        }

        [Test]
        public void DumpFloraKit()
        {
            if (FloraObj.Dir == null) Assert.Ignore("GHUMANTE_PREVIEW_DIR not set");
            int[] months = { 4, 10, 12 };
            for (int s = 0; s < FloraCatalog.Count; s++)
                foreach (int month in months)
                    for (int lod = 0; lod < 2; lod++)
                    {
                        var sp = (TreeSpecies)s;
                        var m = new MeshData();
                        FloraMesher.Build(sp, lod, month, m, false);
                        FloraObj.Write(m, "flora/" + sp + "_m" + month + "_lod" + lod + ".obj");
                    }
            for (int f = 0; f < FloraMesher.Families; f++)
                for (int lod = 2; lod < 4; lod++)
                {
                    var m = new MeshData();
                    FloraMesher.Family((TreeShape)f, lod, m, 10f, 14f);
                    FloraObj.Write(m, "flora/family_" + (TreeShape)f + "_lod" + lod + ".obj");
                }
            var c = new MeshData();
            FloraMesher.Chautari(0, c, false);
            var pipal = new MeshData();
            FloraMesher.Build(TreeSpecies.Pipal, 0, 10, pipal, false);
            FloraObj.Append(c, pipal, 0f, 0.8f, 0f);
            FloraObj.Write(c, "flora/chautari_pipal.obj");
        }

        private static void TintFoliage(MeshData m, uint rgb)
        {
            float r = (rgb >> 16 & 0xFF) / 255f, g = (rgb >> 8 & 0xFF) / 255f, b = (rgb & 0xFF) / 255f;
            for (int v = 0; v < m.VertexCount; v++)
            {
                if (m.Colors[4 * v + 3] != 255) continue;
                m.Colors[4 * v] = (byte)(m.Colors[4 * v] * r);
                m.Colors[4 * v + 1] = (byte)(m.Colors[4 * v + 1] * g);
                m.Colors[4 * v + 2] = (byte)(m.Colors[4 * v + 2] * b);
            }
        }

        [Test]
        public void DumpLineups()
        {
            if (FloraObj.Dir == null) Assert.Ignore("GHUMANTE_PREVIEW_DIR not set");
            foreach (int month in new[] { 4, 10 })
            {
                var line = new MeshData();
                float x = 0f;
                foreach (TreeSpecies sp in Trees)
                {
                    var m = new MeshData();
                    FloraMesher.Build(sp, 0, month, m, false);
                    float w = FloraCatalog.Info(sp).ModelWidthM;
                    x += w * 0.5f;
                    FloraObj.Append(line, m, x, 0f, 0f);
                    x += w * 0.5f + 2f;
                }
                FloraObj.Write(line, "flora/lineup_trees_m" + month + ".obj");
                // LOD ladders: per species LOD 0..3 side by side (far LODs tinted as the renderer does).
                foreach (TreeSpecies sp in Trees)
                {
                    FloraInfo info = FloraCatalog.Info(sp);
                    var lods = new MeshData();
                    for (int lod = 0; lod < FloraMesher.Lods; lod++)
                    {
                        var m = new MeshData();
                        FloraMesher.Build(sp, lod, month, m);
                        if (lod >= 2) TintFoliage(m, FloraCatalog.FoliageColour(sp, month));
                        FloraObj.Append(lods, m, lod * (info.ModelWidthM + 2f), 0f, 0f, 0f, info.ModelWidthM, info.ModelHeightM, info.ModelWidthM);
                    }
                    FloraObj.Write(lods, "flora/lods/" + sp + "_m" + month + ".obj");
                }
                var plants = new MeshData();
                x = 0f;
                for (int s = (int)TreeSpecies.Shrub; s < FloraCatalog.Count; s++)
                {
                    var m = new MeshData();
                    FloraMesher.Build((TreeSpecies)s, 0, month, m, false);
                    float w = FloraCatalog.Info((TreeSpecies)s).ModelWidthM;
                    x += w * 0.5f;
                    FloraObj.Append(plants, m, x, 0f, 0f);
                    x += w * 0.5f + 0.6f;
                }
                FloraObj.Write(plants, "flora/lineup_plants_m" + month + ".obj");
            }
        }
    }
}
