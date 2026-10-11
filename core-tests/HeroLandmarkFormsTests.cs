using System;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The hiti (Manga Hiti) form of <see cref="HeroForms"/> in detail (docs/research/w2/ref_assets-b.md §4): the pit's
    /// levels from OSM, its stepped sides, spouts, Mani Mandap pavilions, walkable way down and the ground cut the
    /// terrain must leave open; and the dumps (GHUMANTE_PREVIEW_DIR) of the Dharahara and the hitis for the visual
    /// check. The reproductions of the reviewer's findings are in <see cref="HeroLandmarkLookTests"/>.
    /// </summary>
    public class HeroLandmarkFormsTests
    {
        private static readonly GenFrame Origin = new GenFrame(0, 0, 0f, 180f);

        [Test]
        public void HitiIsASunkenSteppedPitWithSpoutsAndAWayDown()
        {
            foreach (var (w, d, spouts, mandap) in new[] { (15f, 15f, 3, true), (12f, 8f, 3, false), (6f, 4f, 1, false) })
            {
                double tv, cv;
                HeroForms.HitiLevels(w, d, out tv, out cv);
                if (w >= 12f)
                {
                    Assert.That(tv, Is.EqualTo(-2.5).Within(0.01), "Manga Hiti terrace (OSM: walls from -2.5 m)");
                    Assert.That(cv, Is.InRange(-3.6, -3.2), "Manga Hiti court (OSM: -3.5 m)");
                }
                else Assert.That(cv, Is.InRange(-2.8, -1.5), "a neighbourhood hiti is shallower");
                for (int lod = 0; lod <= 3; lod++)
                {
                    var m = new MeshData();
                    var c = new GenColliders();
                    int tris = HeroForms.Hiti(w, d, spouts, true, Origin, lod, m, c, mandap);
                    string what = w + "x" + d + " LOD" + lod;
                    MeshingChecks.AssertWellFormed(m, what);
                    Assert.That(m.HasUv0, Is.True, what);
                    int cap = w < 10f ? SacredSelector.MaxTris(true, lod) : HeroRecipe.BudgetB[lod];
                    Assert.That(tris, Is.LessThanOrEqualTo(cap), what + " budget");
                    TestContext.WriteLine("hiti {0}: {1} tris", what, tris);
                    float x0, y0, z0, x1, y1, z1;
                    m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                    // Below grade: the pit reaches the court level; nothing of the hiti itself stands above its 0.8 m
                    // parapet (only the pavilions do).
                    if (lod <= 2) Assert.That(y0, Is.LessThanOrEqualTo(cv - 0.1), what + " pit floor below the square");
                    double top = 0;
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        double x = m.Positions[3 * v], z = m.Positions[3 * v + 2];
                        if (Math.Abs(x) <= 0.5 * w + 0.2 && Math.Abs(z) <= 0.5 * d + 0.2) top = Math.Max(top, m.Positions[3 * v + 1]);
                    }
                    Assert.That(top, Is.InRange(0.6, mandap && lod <= 2 ? 2.0 : 1.9), what + " parapet and railings only above the square");
                    if (mandap) Assert.That(y1, Is.GreaterThan(5.5f), what + " Mani Mandap roofs");
                    if (lod != 0) continue;
                    // Walkable: a stair ramp from the square to the court, the court and the ledge as walkable boxes.
                    Assert.That(c.Ramps.Count, Is.GreaterThanOrEqualTo(1), what + " stair");
                    Assert.That(c.Ramps[0].Y0 - c.Ramps[0].Y1, Is.EqualTo(-cv).Within(0.01), what + " the stair descends to the court");
                    bool court = false, ledge = false;
                    foreach (GenBox b in c.Boxes)
                    {
                        if ((b.Flags & GenColliderFlags.Walkable) == 0) continue;
                        if (Math.Abs(b.CY + b.HalfY - cv) < 0.01) court = true;
                        if (Math.Abs(b.CY + b.HalfY - tv) < 0.01) ledge = true;
                    }
                    Assert.That(court && ledge, Is.True, what + " court and ledge walkable");
                    Assert.That(c.Walls.Count, Is.GreaterThanOrEqualTo(4), what + " parapet walls");
                    double hu, hw;
                    HeroForms.HitiGroundCut(w, d, out hu, out hw);
                    Assert.That(hu, Is.EqualTo(0.5 * w - 0.4).Within(1e-6));
                    Assert.That(hw, Is.EqualTo(0.5 * d - 0.4).Within(1e-6));
                }
            }
        }

        /// <summary>The square round a hiti for the previews: paving out to <paramref name="extent"/> metres with the
        /// opening of <see cref="HeroForms.HitiGroundCut"/> left open (as the terrain will be cut in the game).</summary>
        private static MeshData SquareWithOpening(float w, float d, float extent)
        {
            double hu, hw;
            HeroForms.HitiGroundCut(w, d, out hu, out hw);
            var g = new MeshData();
            uint col = MeshColor.FromHex(0xA4553A);
            void Rect(double x0, double z0, double x1, double z1)
            {
                int a = g.AddVertex((float)x0, -0.01f, (float)z0, 0, 1, 0, col, (float)MaterialChannel.Brick, 1f);
                int b = g.AddVertex((float)x1, -0.01f, (float)z0, 0, 1, 0, col, (float)MaterialChannel.Brick, 1f);
                int c = g.AddVertex((float)x1, -0.01f, (float)z1, 0, 1, 0, col, (float)MaterialChannel.Brick, 1f);
                int e = g.AddVertex((float)x0, -0.01f, (float)z1, 0, 1, 0, col, (float)MaterialChannel.Brick, 1f);
                g.AddTriangle(a, c, b);
                g.AddTriangle(a, e, c);
            }
            Rect(-extent, -extent, extent, -hw);
            Rect(-extent, hw, extent, extent);
            Rect(-extent, -hw, -hu, hw);
            Rect(hu, -hw, extent, hw);
            return g;
        }

        [Test]
        public void DumpLandmarks()
        {
            if (!ObjDump.Enabled) Assert.Ignore("GHUMANTE_PREVIEW_DIR not set");
            for (int lod = 0; lod <= 3; lod++)
            {
                var m = new MeshData();
                HeroForms.Dharahara(72f, Origin, lod, m, null, null);
                ObjDump.Write(m, "landmarks/dharahara_lod" + lod + ".obj");
                var h = new MeshData();
                HeroForms.Hiti(15f, 15f, 3, true, Origin, lod, h, null, true);
                ObjDump.Write(h, "landmarks/manga_hiti_lod" + lod + ".obj");
                ObjDump.Append(h, SquareWithOpening(15f, 15f, 20f), 0, 0, 0);
                ObjDump.Write(h, "landmarks/manga_hiti_in_square_lod" + lod + ".obj");
            }
            var cat = new MeshData();
            HeroForms.Hiti(12f, 8f, 3, true, Origin, 0, cat, null);
            ObjDump.Append(cat, SquareWithOpening(12f, 8f, 12f), 0, 0, 0);
            ObjDump.Write(cat, "landmarks/manga_hiti_catalog_12x8.obj");
            var gen = new MeshData();
            HeroForms.Hiti(6f, 4f, 1, true, Origin, 0, gen, null);
            ObjDump.Append(gen, SquareWithOpening(6f, 4f, 7f), 0, 0, 0);
            ObjDump.Write(gen, "landmarks/hiti_generic_6x4.obj");
        }
    }
}
