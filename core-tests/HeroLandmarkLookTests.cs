using System;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Reproductions of the reviewer's findings on the Dharahara and Manga Hiti (docs/research/w2/ref_assets-b.md
    /// §3-§4), on the public form API that existed before the fix (<see cref="HeroForms.Dharahara"/>,
    /// <see cref="HeroForms.Hiti"/> with its original parameters), so the same checks run against the code before and
    /// after: the 2021 tower had an almost straight shaft with 10 cm bands under a squat quarter-ellipse cap; the hiti
    /// was a raised stone tray standing on the square instead of a pit sunk about 3 m below it.
    /// </summary>
    public class HeroLandmarkLookTests
    {
        private static readonly GenFrame Origin = new GenFrame(0, 0, 0f, 180f);

        /// <summary>Largest distance from the vertical axis of the surface cut by the plane at height <paramref name="y"/>
        /// (every triangle crossing it, intersected exactly): the radius of the outline at that height, independent of
        /// where the vertices happen to lie.</summary>
        private static double SectionRadius(MeshData m, double y)
        {
            double best = 0;
            for (int t = 0; t < m.TriangleCount; t++)
                for (int e = 0; e < 3; e++)
                {
                    int a = m.Indices[3 * t + e], b = m.Indices[3 * t + (e + 1) % 3];
                    double ya = m.Positions[3 * a + 1], yb = m.Positions[3 * b + 1];
                    if ((ya - y) * (yb - y) > 0 || ya == yb) continue;
                    double f = (y - ya) / (yb - ya);
                    double x = m.Positions[3 * a] + f * (m.Positions[3 * b] - m.Positions[3 * a]);
                    double z = m.Positions[3 * a + 2] + f * (m.Positions[3 * b + 2] - m.Positions[3 * a + 2]);
                    best = Math.Max(best, Math.Sqrt(x * x + z * z));
                }
            return best;
        }

        /// <summary>Largest <see cref="SectionRadius"/> over [y0, y1] (sampled every 2 cm).</summary>
        private static double SectionMax(MeshData m, double y0, double y1)
        {
            double best = 0;
            for (double y = y0; y <= y1 + 1e-9; y += 0.02) best = Math.Max(best, SectionRadius(m, y));
            return best;
        }

        /// <summary>
        /// The rebuilt Dharahara (2021, refs dharahara_new c_00, c_02, c_05, c_07): eight storey cornices projecting at
        /// least 0.25 m from the shaft (a ring, not a painted band), a shaft tapering about 1.26 : 1 from its foot to the
        /// gallery, the corbelled gallery balcony oversailing it, and above it a drum, dome and gilt needle spire rising
        /// more than twice the dome's base radius to 72 m (no squat cap).
        /// </summary>
        [Test]
        public void DharaharaHasProjectingStoreyCorniceTaperAndDomeSpireTop()
        {
            for (int lod = 0; lod <= 3; lod++)
            {
                var m = new MeshData();
                var c = new GenColliders();
                var stats = new SacredStats();
                int tris = HeroForms.Dharahara(72f, Origin, lod, m, c, stats);
                MeshingChecks.AssertWellFormed(m, "Dharahara LOD" + lod);
                Assert.That(m.HasUv0, Is.True);
                Assert.That(tris, Is.LessThanOrEqualTo(HeroRecipe.BudgetB[lod]), "LOD" + lod + " budget");
                float x0, y0, z0, x1, y1, z1;
                m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                Assert.That(y1, Is.EqualTo(72f).Within(0.05f), "spire tip at 72 m");
                Assert.That(stats.TopM, Is.EqualTo(72f));
                TestContext.WriteLine("Dharahara LOD{0}: {1} tris", lod, tris);
                if (lod >= 3) continue;
                foreach (double yb in new[] { 9.0, 15.2, 21.2, 27.2, 33.1, 39.1, 45.1, 51.0 })
                {
                    double band = SectionMax(m, yb - 0.2, yb + 0.2), wall = SectionRadius(m, yb + 2.5);
                    TestContext.WriteLine("  cornice at {0:0.0} m: {1:0.00} m out of the shaft (r {2:0.00})", yb, band - wall, wall);
                    Assert.That(band - wall, Is.GreaterThan(0.25), "LOD" + lod + " cornice at " + yb + " m projects");
                }
                double foot = SectionRadius(m, 9.5), head = SectionRadius(m, 55.6);
                TestContext.WriteLine("  shaft radius {0:0.00} m at 9.5 m, {1:0.00} m at 55.6 m: taper {2:0.00}", foot, head, foot / head);
                Assert.That(foot / head, Is.InRange(1.15, 1.35), "taper");
                Assert.That(SectionMax(m, 57.0, 58.0), Is.GreaterThan(head + 1.8), "gallery balcony");
                double dome = SectionMax(m, 62.45, 62.9);
                Assert.That(dome, Is.InRange(3.4, 4.3), "dome base radius");
                Assert.That((72.0 - 62.36) / dome, Is.GreaterThan(2.2), "the top tapers to a spire");
                Assert.That(SectionMax(m, 68.5, 71.5), Is.LessThan(0.2), "needle spire");
            }
        }

        /// <summary>
        /// Manga Hiti (catalogue 12 × 8 m, three spouts) and a neighbourhood hiti (6 × 4 m, one spout) are sunk below the
        /// square (refs mangahiti_c 00, 12, 14; OSM walls from −2.5 to −3.5 m): the pit floor lies at least 2.5 m (Manga
        /// Hiti) or 1.5 m (a small hiti) below the ground at LOD0-2, nothing of the hiti stands above its parapet and
        /// railings (≤ 1.9 m) inside its footprint, and at LOD0 a walkable stair leads down to the court.
        /// </summary>
        [Test]
        public void HitiIsSunkBelowTheSquareWithAWayDown()
        {
            var bad = new System.Collections.Generic.List<string>();
            foreach (var (w, d, spouts, deep) in new[] { (12f, 8f, 3, 2.5), (6f, 4f, 1, 1.5) })
                for (int lod = 0; lod <= 2; lod++)
                {
                    var m = new MeshData();
                    var c = new GenColliders();
                    HeroForms.Hiti(w, d, spouts, true, Origin, lod, m, c);
                    string what = w + "x" + d + " LOD" + lod;
                    double floor = double.MaxValue, top = double.MinValue;
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        double x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                        floor = Math.Min(floor, y);
                        if (Math.Abs(x) <= 0.5 * w + 0.2 && Math.Abs(z) <= 0.5 * d + 0.2) top = Math.Max(top, y);
                    }
                    double drop = 0;
                    foreach (GenRamp r in c.Ramps) drop = Math.Max(drop, Math.Abs(r.Y0 - r.Y1));
                    TestContext.WriteLine("hiti {0}: floor {1:0.00} m, top {2:0.00} m, stair drop {3:0.00} m", what, floor, top, drop);
                    if (floor > -deep) bad.Add(what + ": floor at " + floor.ToString("0.00") + " m (sunk at least " + deep + " m below the square)");
                    if (top > 1.9) bad.Add(what + ": stands " + top.ToString("0.00") + " m above the square");
                    if (lod == 0 && drop < deep) bad.Add(what + ": no stair down to the court (drop " + drop.ToString("0.00") + " m)");
                }
            Assert.That(bad, Is.Empty, string.Join("; ", bad));
        }
    }
}
