using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.KdsTemples;
using Ghumante.Core.Meshing;
using Ghumante.World.Sacred;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The bespoke Kathmandu Durbar Square temples and square (Heroes/KdsTemples, docs/research/heritage/kds-temples.md):
    /// every structure builds at every LOD inside its ceiling and the site inside 150 k triangles at LOD0 (LOD1 about 30 %,
    /// LOD2 about 8 %); measured heights and door bearings; the structures fill their OSM footprints; walkable plinths and
    /// stairs; no fences, figures or stalls in a road corridor and no eave, valance or bell lower than the overhead clearance
    /// over one (contract §1); the catalogue keeps the six existing hero ids, adds the new parts and the hero set hides the
    /// buildings the site rebuilds.
    /// </summary>
    public class GeneratorsSacredKdsTemplesTests
    {
        private static TileData Tile
        {
            get { return StreamingSampleRegion.Tile(GeneratorsSacredKdsTemplesPreview.SiteTile); }
        }

        private sealed class Built
        {
            public string Id;
            public KdsTemplesSite.Entry Entry;
            public KdsCtx X;
            public readonly int[] Tris = new int[4];
            public MeshData Mesh0;
            public GenColliders Colliders;
            public SacredStats Stats;
        }

        private static List<Built> _built;

        private static List<Built> All()
        {
            if (_built != null) return _built;
            TileData tile = Tile;
            var l = new List<Built>();
            foreach (string id in KdsTemplesSite.Ids)
            {
                HeroRecipe recipe;
                Assert.That(HeroCatalog.TryGet(id, out recipe), Is.True, id);
                HeritageRecord r = HeroCatalog.ToRecord(recipe);
                var b = new Built { Id = id, Entry = GeneratorsSacredKdsTemplesPreview.Entry(id) };
                for (int lod = 0; lod <= 3; lod++)
                {
                    var m = new MeshData();
                    var c = new GenColliders();
                    var stats = new SacredStats();
                    b.Tris[lod] = KdsTemplesSite.Build(r, tile, lod, m, lod == 0 ? c : null, stats);
                    if (lod == 0)
                    {
                        b.Mesh0 = m;
                        b.Colliders = c;
                        b.Stats = stats;
                    }
                }
                KdsCtx x;
                Assert.That(KdsTemplesSite.Locate(b.Entry, tile, 0, new MeshData(), null, null, out x), Is.True, id);
                b.X = x;
                l.Add(b);
            }
            return _built = l;
        }

        [Test]
        public void EveryStructureBuildsInsideItsCeilings()
        {
            foreach (Built b in All())
            {
                int[] cap = KdsTemplesSite.Budget(b.Id);
                for (int lod = 0; lod <= 3; lod++)
                {
                    Assert.That(b.Tris[lod], Is.GreaterThan(0), b.Id + " lod " + lod);
                    Assert.That(b.Tris[lod], Is.LessThanOrEqualTo(cap[lod]), b.Id + " lod " + lod);
                }
                Assert.That(b.Tris[1], Is.LessThan(b.Tris[0]), b.Id);
                Assert.That(b.Tris[2], Is.LessThan(b.Tris[1]), b.Id);
            }
        }

        [Test]
        public void SiteFitsItsBudget()
        {
            var sum = new int[4];
            foreach (Built b in All())
                for (int lod = 0; lod <= 3; lod++)
                    sum[lod] += b.Tris[lod];
            TestContext.WriteLine("site LOD0 {0}, LOD1 {1}, LOD2 {2}, LOD3 {3}", sum[0], sum[1], sum[2], sum[3]);
            Assert.That(sum[0], Is.LessThanOrEqualTo(150000), "LOD0");
            Assert.That(sum[1], Is.LessThanOrEqualTo(0.4 * sum[0]), "LOD1 about 30 %");
            Assert.That(sum[2], Is.LessThanOrEqualTo(0.12 * sum[0]), "LOD2 about 8 %");
            Assert.That(sum[0], Is.GreaterThan(80000), "the site is drawn in detail");
        }

        [Test]
        public void HeightsAndDoorsAreMeasured()
        {
            foreach (Built b in All())
            {
                Assert.That(b.Stats.TopM, Is.EqualTo(b.Entry.TopM).Within(0.35), b.Id + " top");
                double top = double.MinValue;
                for (int v = 0; v < b.Mesh0.VertexCount; v++) top = Math.Max(top, b.Mesh0.Positions[3 * v + 1]);
                top -= b.X.F.GroundY;
                Assert.That(top, Is.LessThanOrEqualTo(b.Entry.TopM + 0.8), b.Id + " mesh top");
                Assert.That(top, Is.GreaterThanOrEqualTo(b.Entry.TopM - 1.2), b.Id + " mesh top");
                double d = Math.Abs(((b.Stats.DoorYawDeg - b.Entry.YawDeg) % 360 + 540) % 360 - 180);
                Assert.That(d, Is.LessThanOrEqualTo(15), b.Id + " door bearing " + b.Stats.DoorYawDeg);
            }
            // The structures the brief measured (kds-temples.md §8).
            Assert.That(Top("her.ktm.maju_dega"), Is.EqualTo(23.5).Within(0.1));
            Assert.That(Top("her.ktm.kasthamandap"), Is.EqualTo(18.8).Within(0.1));
            Assert.That(Top("her.ktm.kal_bhairav"), Is.EqualTo(8.12).Within(0.05));
            Assert.That(Top("her.ktm.trailokya_mohan"), Is.EqualTo(18.5).Within(0.1));
            Assert.That(Top("her.ktm.kds_taleju_bell"), Is.EqualTo(8.5).Within(0.1));
        }

        private static double Top(string id)
        {
            foreach (Built b in All())
                if (b.Id == id)
                    return b.Stats.TopM;
            return double.NaN;
        }

        [Test]
        public void KalBhairavStandsOnItsTrueFootprintFacingNorth()
        {
            HeroRecipe r;
            Assert.That(HeroCatalog.TryGet("her.ktm.kal_bhairav", out r), Is.True);
            Assert.That(r.Osm, Is.EqualTo("w908861416"));
            Assert.That(r.YawDeg, Is.EqualTo(10.7f).Within(0.1));
            Assert.That(r.Form, Is.EqualTo(HeroForm.Bespoke));
            HeroRecipe ln;
            Assert.That(HeroCatalog.TryGet("her.ktm.kds_lakshmi_narayan", out ln), Is.True);
            Assert.That(ln.Osm, Is.EqualTo("w196261745"), "the old Kal Bhairav footprint is the Lakshmi Narayan temple's");
        }

        [Test]
        public void StructuresFillTheirFootprints()
        {
            foreach (Built b in All())
            {
                if (b.X.OutU == null || b.Entry.Part == KdsPart.SwetaBhairav) continue;
                double u0 = double.MaxValue, u1 = double.MinValue, w0 = double.MaxValue, w1 = double.MinValue;
                MeshData m = b.Mesh0;
                for (int v = 0; v < m.VertexCount; v++)
                {
                    if (m.Positions[3 * v + 1] - b.X.F.GroundY > 4.0) continue;
                    double u, w;
                    b.X.Local(m.Positions[3 * v], m.Positions[3 * v + 2], out u, out w);
                    u0 = Math.Min(u0, u);
                    u1 = Math.Max(u1, u);
                    w0 = Math.Min(w0, w);
                    w1 = Math.Max(w1, w);
                }
                double hu = b.X.PlanHu, hw = b.X.PlanHw;
                // Mahendreshwar's outline is the temple's only; its compound runs well beyond it.
                Assert.That(u0, Is.LessThanOrEqualTo(-0.65 * hu), b.Id);
                Assert.That(u1, Is.GreaterThanOrEqualTo(0.65 * hu), b.Id);
                Assert.That(w0, Is.LessThanOrEqualTo(-0.65 * hw), b.Id);
                Assert.That(w1, Is.GreaterThanOrEqualTo(0.65 * hw), b.Id);
            }
        }

        [Test]
        public void PlinthsAndStairsAreWalkable()
        {
            var stairs = new HashSet<KdsPart>
            {
                KdsPart.MajuDega, KdsPart.TrailokyaMohan, KdsPart.Jagannath, KdsPart.ChyasinDega, KdsPart.Kasthamandap, KdsPart.ShivaParvati, KdsPart.Gopinath,
                KdsPart.Mahavishnu, KdsPart.Indrapur, KdsPart.LakshmiNarayan, KdsPart.Saraswati, KdsPart.TalejuBell, KdsPart.Kageshwor, KdsPart.Kotilingeshwar,
                KdsPart.Mahendreshwar, KdsPart.ShivaNorth, KdsPart.ShivaWest,
            };
            foreach (Built b in All())
            {
                GenColliders c = b.Colliders;
                Assert.That(c.Boxes.Count + c.Cylinders.Count + c.Walls.Count, Is.GreaterThan(0), b.Id);
                if (stairs.Contains(b.Entry.Part)) Assert.That(c.Ramps.Count, Is.GreaterThan(0), b.Id + " has a walkable stair");
            }
            // Maju Dega's top platform (6.6 m) is a walkable floor reached by its stair ramp.
            Built maju = All().Find(b => b.Entry.Part == KdsPart.MajuDega);
            bool top = false;
            foreach (GenBox box in maju.Colliders.Boxes)
                if ((box.Flags & GenColliderFlags.Walkable) != 0 && Math.Abs(box.CY + box.HalfY - maju.X.F.GroundY - MajuDega.PlinthTop) < 0.1)
                    top = true;
            Assert.That(top, Is.True, "Maju Dega's top stage is walkable");
            GenRamp ramp = maju.Colliders.Ramps[0];
            Assert.That(Math.Max(ramp.Y0, ramp.Y1) - maju.X.F.GroundY, Is.EqualTo(MajuDega.PlinthTop).Within(0.15));
        }

        [Test]
        public void FurnitureFencesAndStallsStayOutOfTheCorridors()
        {
            TileData tile = Tile;
            foreach (Built b in All())
            {
                KdsCtx x = b.X;
                if (x.Clear == null) continue;
                foreach (GenWall w in b.Colliders.Walls)
                {
                    if (InsideOutline(x, w.X0, w.Z0) || InsideOutline(x, w.X1, w.Z1)) continue;
                    Assert.That(x.Clear.Distance(w.X0, w.Z0), Is.GreaterThanOrEqualTo(-0.05), b.Id + " wall end in a corridor");
                    Assert.That(x.Clear.Distance(w.X1, w.Z1), Is.GreaterThanOrEqualTo(-0.05), b.Id + " wall end in a corridor");
                }
                foreach (GenCylinder cy in b.Colliders.Cylinders)
                {
                    if (InsideOutline(x, cy.CX, cy.CZ)) continue;
                    Assert.That(x.Clear.Distance(cy.CX, cy.CZ), Is.GreaterThanOrEqualTo(cy.Radius - 0.05), b.Id + " post or figure in a corridor");
                }
                foreach (GenBox box in b.Colliders.Boxes)
                {
                    if ((box.Flags & GenColliderFlags.Walkable) != 0 || box.CY - box.HalfY > x.F.GroundY + 2.5 || InsideOutline(x, box.CX, box.CZ)) continue;
                    Assert.That(x.Clear.Distance(box.CX, box.CZ), Is.GreaterThanOrEqualTo(0), b.Id + " solid in a corridor");
                }
            }
        }

        [Test]
        public void EavesValancesAndBellsClearTheCorridors()
        {
            foreach (Built b in All())
            {
                KdsCtx x = b.X;
                if (x.Clear == null) continue;
                MeshData m = b.Mesh0;
                int bad = 0;
                double worst = 0;
                for (int v = 0; v < m.VertexCount; v++)
                {
                    double px = m.Positions[3 * v], py = m.Positions[3 * v + 1], pz = m.Positions[3 * v + 2];
                    if (py - x.F.GroundY < 2.0 || InsideOutline(x, px, pz)) continue;
                    double min = x.Clear.MinHangY(px, pz, x.F.GroundY);
                    if (double.IsNegativeInfinity(min) || py >= min - 0.05) continue;
                    bad++;
                    worst = Math.Max(worst, min - py);
                }
                Assert.That(bad, Is.EqualTo(0), b.Id + " hangs into a corridor by up to " + worst.ToString("0.00") + " m");
            }
        }

        /// <summary>Inside the structure's OSM outline (grown 0.3 m), which keeps its plan even where a mapped path crosses it.</summary>
        private static bool InsideOutline(KdsCtx x, double tx, double tz)
        {
            if (x.OutU == null) return false;
            double u, w;
            x.Local(tx, tz, out u, out w);
            int n = x.OutU.Length;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if ((x.OutW[i] > w) != (x.OutW[j] > w) && u < (x.OutU[j] - x.OutU[i]) * (w - x.OutW[i]) / (x.OutW[j] - x.OutW[i]) + x.OutU[i]) inside = !inside;
            }
            if (inside) return true;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double du = x.OutU[i] - x.OutU[j], dw = x.OutW[i] - x.OutW[j], l2 = du * du + dw * dw;
                double t = l2 > 1e-9 ? Math.Max(0, Math.Min(1, ((u - x.OutU[j]) * du + (w - x.OutW[j]) * dw) / l2)) : 0;
                double eu = x.OutU[j] + t * du - u, ew = x.OutW[j] + t * dw - w;
                if (eu * eu + ew * ew < 0.09) return true;
            }
            return false;
        }

        [Test]
        public void CatalogueKeepsTheHeroIdsAndAddsTheParts()
        {
            foreach (string id in new[]
                     {
                         "her.ktm.kasthamandap", "her.ktm.maju_dega", "her.ktm.trailokya_mohan", "her.ktm.jagannath", "her.ktm.shiva_parvati", "her.ktm.kal_bhairav",
                     })
            {
                HeroRecipe r;
                Assert.That(HeroCatalog.TryGet(id, out r), Is.True, id);
                Assert.That(r.Form, Is.EqualTo(HeroForm.Bespoke), id);
                Assert.That(r.SiteOf, Is.Null, id);
                Assert.That(r.Measured, Is.True, id);
            }
            int parts = 0;
            foreach (HeroRecipe r in HeroCatalog.S1)
            {
                if (!KdsTemplesSite.Handles(r.Id)) continue;
                Assert.That(r.BudgetTris, Is.Not.Null.And.Length.EqualTo(4), r.Id);
                Assert.That(r.Hide, Is.Not.Null, r.Id);
                if (r.SiteOf == null) continue;
                Assert.That(r.SiteOf, Is.EqualTo(KdsTemplesSite.SiteHeroId), r.Id);
                Assert.That(r.Id, Does.StartWith("her.ktm.kds_"), r.Id);
                parts++;
            }
            Assert.That(parts, Is.EqualTo(23));
        }

        [Test]
        public void HeroSetAddsThePartsAndHidesWhatTheSiteRebuilds()
        {
            HeroSet set = StreamingScene.Heroes;
            var ids = new HashSet<string>();
            foreach (HeritageRecord r in set.InTile(GeneratorsSacredKdsTemplesPreview.SiteTile.Key)) ids.Add(r.Id);
            foreach (string id in KdsTemplesSite.Ids) Assert.That(ids.Contains(id), Is.True, id + " is streamed in the site's tile");
            foreach (string osm in new[] { "w908861416", "w196261745", "w183559499", "w183563646", "w907048005", "w1415152800", "w908861401" })
                Assert.That(set.HiddenRefs.Contains(HeritageRecord.WayRelationRef(osm)), Is.True, osm + " is hidden from the house bands");
        }

        [Test]
        public void BuildIsDeterministic()
        {
            TileData tile = Tile;
            foreach (string id in new[] { "her.ktm.kasthamandap", "her.ktm.kds_basantapur_square", "her.ktm.kal_bhairav" })
            {
                HeroRecipe recipe;
                HeroCatalog.TryGet(id, out recipe);
                HeritageRecord r = HeroCatalog.ToRecord(recipe);
                var a = new MeshData();
                var b = new MeshData();
                KdsTemplesSite.Build(r, tile, 0, a, null, null);
                KdsTemplesSite.Build(r, tile, 0, b, null, null);
                Assert.That(b.VertexCount, Is.EqualTo(a.VertexCount), id);
                for (int i = 0; i < a.VertexCount * 3; i++) Assert.That(b.Positions[i], Is.EqualTo(a.Positions[i]), id);
            }
        }

        [Test]
        public void HeroBuilderDispatchesTheSite()
        {
            TileData tile = Tile;
            HeroRecipe recipe;
            HeroCatalog.TryGet("her.ktm.kds_chyasin_dega", out recipe);
            var m = new MeshData();
            var stats = new SacredStats();
            int t = HeroBuilder.Build(HeroCatalog.ToRecord(recipe), tile, 0, m, null, stats);
            Assert.That(t, Is.GreaterThan(1000));
            Assert.That(stats.TopM, Is.EqualTo(ChyasinDega.TopM).Within(0.3));
        }
    }
}
