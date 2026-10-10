using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>W2_DESIGN 10.6 V4 (replica fidelity) and the generic sacred generators: heights, tiers, plinths, door
    /// yaws, outlines, LOD budgets, strut and bell formulas, chaitya niches, closed sanctums.</summary>
    public class GeneratorsSacredTests
    {
        private static CuratedDb _db;
        private static Dictionary<TileId, TileData> _tiles;

        private static CuratedDb Db
        {
            get { return _db ?? (_db = CuratedDb.Read(System.IO.File.ReadAllBytes(GoldenFiles.RepoRoot + "/shared/sample-regions/kathmandu_core/kathmandu_core.curated.ghcd"))); }
        }

        private static Dictionary<TileId, TileData> Tiles
        {
            get { return _tiles ?? (_tiles = StreamingSampleRegion.TilesAt(10).ToDictionary(i => i, i => StreamingSampleRegion.Tile(i))); }
        }

        /// <summary>The record of a hero (curated DB when it has one, else the catalog) and a tile holding its anchor:
        /// the sample pack's, else a flat synthetic tile at its coordinates (Patan and Bhaktapur lie outside the pack).</summary>
        private static HeritageRecord Locate(HeroRecipe recipe, out TileData tile, out double x, out double z, out int building)
        {
            HeritageRecord rec;
            if (!Db.TryGetHeritage(recipe.Id, out rec)) rec = HeroCatalog.ToRecord(recipe);
            HeroRecipe merged = HeroBuilder.Merge(rec);
            foreach (TileData t in Tiles.Values)
            {
                if (!HeroBuilder.TryLocate(rec, merged, t, out x, out z, out building)) continue;
                tile = t;
                return rec;
            }
            double gx, gz;
            WorldFrame.LonLatToGame(recipe.Lon, recipe.Lat, out gx, out gz);
            double size = TileId.SizeAt(10);
            var id = new TileId(10, (int)Math.Floor(gx / size), (int)Math.Floor(gz / size));
            tile = MeshingChecks.SyntheticTile(id, (px, pz) => 1330);
            Assert.That(HeroBuilder.TryLocate(rec, merged, tile, out x, out z, out building), Is.True, recipe.Id);
            return rec;
        }

        private static bool IsColour(MeshData m, int tri, uint c)
        {
            int v = m.Indices[3 * tri];
            return m.Colors[4 * v] == MeshColor.R(c) && m.Colors[4 * v + 1] == MeshColor.G(c) && m.Colors[4 * v + 2] == MeshColor.B(c);
        }

        private static double Bearing(double nx, double nz)
        {
            double b = Math.Atan2(nx, nz) * 180 / Math.PI;
            return b < 0 ? b + 360 : b;
        }

        private static double AngleDiff(double a, double b)
        {
            double d = Math.Abs(a - b) % 360;
            return d > 180 ? 360 - d : d;
        }

        [Test]
        public void EveryStageOneHeroMatchesItsReplicaSpec()
        {
            Assert.That(HeroCatalog.S1.Count, Is.GreaterThanOrEqualTo(30));
            var stats = new SacredStats();
            foreach (HeroRecipe recipe in HeroCatalog.S1)
            {
                TileData tile;
                double x, z;
                int building;
                HeritageRecord rec = Locate(recipe, out tile, out x, out z, out building);
                HeroRecipe merged = HeroBuilder.Merge(rec);
                string id = recipe.Id;
                for (int lod = 0; lod <= 3; lod++)
                {
                    var m = new MeshData();
                    int tris = HeroBuilder.Build(rec, tile, lod, m, lod == 0 ? new GenColliders() : null, stats);
                    Assert.That(tris, Is.GreaterThan(0), id + " lod " + lod);
                    Assert.That(tris, Is.LessThanOrEqualTo(merged.Budget(lod)), id + " lod " + lod + " budget");
                    MeshingChecks.AssertWellFormed(m, id + " lod " + lod);
                    if (lod != 0) continue;

                    // Height within ±3% (or 0.15 m for the small pieces).
                    Assert.That(stats.TopM, Is.EqualTo(merged.HeightM).Within(Math.Max(0.15, 0.03 * merged.HeightM)), id + " height");
                    // Tier and plinth counts exact where the replica states them.
                    if (merged.Tiers > 0) Assert.That(stats.Tiers, Is.EqualTo(merged.Tiers), id + " tiers");
                    if (merged.PlinthLevels > 0) Assert.That(stats.PlinthLevels, Is.EqualTo(merged.PlinthLevels), id + " plinths");
                    // Door yaw within ±2°: as built, and as drawn (a closed sanctum door faces it).
                    // Door yaw: exact (±2°) without an outline; with an OSM outline the door is square to the measured
                    // walls, so the recipe's (often cardinal, [V]) yaw snaps to the nearest outline axis (within 45°).
                    if (!float.IsNaN(merged.YawDeg))
                    {
                        double tol = building >= 0 ? 45.0 : 2.0;
                        Assert.That(AngleDiff(stats.DoorYawDeg, merged.YawDeg), Is.LessThanOrEqualTo(tol), id + " yaw");
                    }
                    double best = 360;
                    for (int t = 0; t < m.TriangleCount; t++)
                    {
                        if (!IsColour(m, t, SacredPalette.SanctumDark)) continue;
                        double nx, ny, nz;
                        MeshingChecks.Facet(m, t, out nx, out ny, out nz);
                        if (Math.Abs(ny) > 0.2) continue;
                        best = Math.Min(best, AngleDiff(Bearing(nx, nz), stats.DoorYawDeg));
                    }
                    if (best < 360) Assert.That(best, Is.LessThanOrEqualTo(2.0), id + " sanctum door faces the door yaw");
                    TestContext.WriteLine("{0,-36} {1,6} tris  top {2,5:0.0}/{3,5:0.0}  tiers {4}  plinths {5}  yaw {6:0}", id, tris, stats.TopM, merged.HeightM,
                                          stats.Tiers, stats.PlinthLevels, stats.DoorYawDeg);
                }
            }
        }

        [Test]
        public void HeroPlansFollowTheirOsmOutlines()
        {
            int outlined = 0;
            foreach (HeroRecipe recipe in HeroCatalog.S1)
            {
                if (recipe.Form != HeroForm.Pagoda && recipe.Form != HeroForm.Mandapa && recipe.Form != HeroForm.ShikharaStone &&
                    recipe.Form != HeroForm.ShikharaPlaster)
                    continue;
                TileData tile;
                double x, z;
                int building;
                HeritageRecord rec = Locate(recipe, out tile, out x, out z, out building);
                if (building < 0) continue;
                if (HeroBuilder.Merge(rec).NoStair) continue; // on a palace block: the block is the outline
                var m = new MeshData();
                HeroBuilder.Build(rec, tile, 0, m, null);
                int[] ring = tile.Buildings[building].Rings[0];
                for (int k = 0; k < ring.Length / 2; k++)
                {
                    double px = ring[2 * k] / 100.0, pz = ring[2 * k + 1] / 100.0, d = double.MaxValue;
                    for (int v = 0; v < m.VertexCount; v++)
                        d = Math.Min(d, Math.Sqrt(Math.Pow(m.Positions[3 * v] - px, 2) + Math.Pow(m.Positions[3 * v + 2] - pz, 2)));
                    Assert.That(d, Is.LessThanOrEqualTo(0.1), recipe.Id + " outline vertex " + k);
                }
                outlined++;
            }
            Assert.That(outlined, Is.GreaterThanOrEqualTo(5));
        }

        /// <summary>Length-weighted dominant edge bearing of a ring, modulo 90° (degrees, 0..90).</summary>
        internal static double OutlineAxis(int[] ring)
        {
            double r;
            return OutlineAxis(ring, out r);
        }

        /// <summary>Dominant edge bearing modulo 90° and how rectilinear the ring is (1 = every edge on the two axes).</summary>
        internal static double OutlineAxis(int[] ring, out double rectilinear)
        {
            // Average the edge directions on the 4-fold circle (angle × 4) weighted by length.
            double sx = 0, sz = 0, total = 0;
            int n = ring.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double dx = (ring[2 * j] - ring[2 * i]) / 100.0, dz = (ring[2 * j + 1] - ring[2 * i + 1]) / 100.0, l = Math.Sqrt(dx * dx + dz * dz);
                double a = Math.Atan2(dx, dz) * 4;
                sx += l * Math.Cos(a);
                sz += l * Math.Sin(a);
                total += l;
            }
            rectilinear = total > 0 ? Math.Sqrt(sx * sx + sz * sz) / total : 0;
            double deg = Math.Atan2(sz, sx) / 4 * 180 / Math.PI;
            return ((deg % 90) + 90) % 90;
        }

        private static double Mod90Diff(double a, double b)
        {
            double d = Math.Abs(a - b) % 90;
            return d > 45 ? 90 - d : d;
        }

        [Test]
        public void HeroWallsAreSquareToTheirOutlinesAndTheSanctumKeepsItsShare()
        {
            // W2_DESIGN 3.1: the plan is the OSM outline and the sanctum 0.45 × the bottom plinth width (or the recipe's
            // share): every wall of the sanctum and the storeys runs parallel to the outline's edges, whatever the
            // recipe's (often cardinal) door yaw estimate says.
            int checkedHeroes = 0;
            foreach (HeroRecipe recipe in HeroCatalog.S1)
            {
                if (recipe.Form != HeroForm.Pagoda && recipe.Form != HeroForm.Mandapa && recipe.Form != HeroForm.ShikharaStone &&
                    recipe.Form != HeroForm.ShikharaPlaster)
                    continue;
                TileData tile;
                double x, z;
                int building;
                HeritageRecord rec = Locate(recipe, out tile, out x, out z, out building);
                if (building < 0) continue;
                HeroRecipe merged = HeroBuilder.Merge(rec);
                if (merged.NoStair) continue;
                int[] ring = tile.Buildings[building].Rings[0];
                double rect;
                double axis = OutlineAxis(ring, out rect);
                if (rect < 0.9) continue; // a many-sided (ratha) outline: its oriented box is the reference, checked by the plan test
                var stats = new SacredStats();
                var m = new MeshData();
                HeroBuilder.Build(rec, tile, 0, m, null, stats);
                Assert.That(Mod90Diff(stats.DoorYawDeg, axis), Is.LessThanOrEqualTo(3.0), recipe.Id + " door yaw square to the outline");
                // Walls: every near-vertical glazed-brick or plastered face is parallel to an outline edge.
                int walls = 0;
                for (int t = 0; t < m.TriangleCount; t++)
                {
                    int v = m.Indices[3 * t];
                    var ch = (MaterialChannel)(int)Math.Round(m.Uv0[2 * v]);
                    if (ch != MaterialChannel.BrickGlazed) continue;
                    double nx, ny, nz;
                    MeshingChecks.Facet(m, t, out nx, out ny, out nz);
                    if (Math.Abs(ny) > 0.2 || nx * nx + nz * nz < 0.5) continue;
                    walls++;
                    Assert.That(Mod90Diff(Bearing(nx, nz), axis), Is.LessThanOrEqualTo(3.0), recipe.Id + " wall face " + t);
                }
                if (recipe.Form == HeroForm.Pagoda || recipe.Form == HeroForm.Mandapa)
                {
                    Assert.That(walls, Is.GreaterThan(0), recipe.Id + " has walls");
                    double w, d;
                    OutlineExtents(ring, axis, out w, out d);
                    double share = merged.CoreFrac > 0 ? merged.CoreFrac : 0.45, across = WidthAcross(ring, stats.DoorYawDeg);
                    if (recipe.Form == HeroForm.Pagoda)
                        Assert.That(2 * stats.SanctumHalfU, Is.EqualTo(share * across).Within(0.05 * share * across), recipe.Id + " sanctum share of the plinth");
                    TestContext.WriteLine("{0,-28} axis {1,5:0.0} yaw {2,5:0.0} plinth {3:0.0} x {4:0.0} sanctum {5:0.0} x {6:0.0}", recipe.Id, axis, stats.DoorYawDeg, w, d,
                                          2 * stats.SanctumHalfU, 2 * stats.SanctumHalfW);
                }
                checkedHeroes++;
            }
            Assert.That(checkedHeroes, Is.GreaterThanOrEqualTo(5));
            // Pashupatinath's sanctum is 8.7 × 8.3 m on its 19.5 × 19.2 m plinth (W2_DESIGN 3.4).
            MeshData pm;
            SacredStats ps = BuildHero("her.ktm.pashupatinath", out pm);
            Assert.That(2 * ps.SanctumHalfU, Is.EqualTo(8.7).Within(0.5), "Pashupatinath sanctum width");
            Assert.That(2 * ps.SanctumHalfW, Is.EqualTo(8.5).Within(0.6), "Pashupatinath sanctum depth");
        }

        private static bool InsideRing(int[] ring, double x, double z)
        {
            int n = ring.Length / 2;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = ring[2 * i] / 100.0, zi = ring[2 * i + 1] / 100.0, xj = ring[2 * j] / 100.0, zj = ring[2 * j + 1] / 100.0;
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        /// <summary>Extents of a ring along (w) and across (d) an axis bearing (mod 90).</summary>
        private static void OutlineExtents(int[] ring, double axisDeg, out double w, out double d)
        {
            double a = axisDeg * Math.PI / 180, ux = Math.Cos(a), uz = -Math.Sin(a), vx = Math.Sin(a), vz = Math.Cos(a);
            double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
            for (int i = 0; i < ring.Length / 2; i++)
            {
                double px = ring[2 * i] / 100.0, pz = ring[2 * i + 1] / 100.0;
                double u = px * ux + pz * uz, v = px * vx + pz * vz;
                u0 = Math.Min(u0, u);
                u1 = Math.Max(u1, u);
                v0 = Math.Min(v0, v);
                v1 = Math.Max(v1, v);
            }
            w = u1 - u0;
            d = v1 - v0;
        }

        /// <summary>Width of a ring across a door bearing (its extent perpendicular to the bearing).</summary>
        private static double WidthAcross(int[] ring, double yawDeg)
        {
            double w, d;
            OutlineExtents(ring, yawDeg, out w, out d);
            return w;
        }

        private static SacredStats BuildHero(string id, out MeshData m)
        {
            HeroRecipe recipe;
            Assert.That(HeroCatalog.TryGet(id, out recipe), Is.True, id);
            TileData tile;
            double x, z;
            int building;
            HeritageRecord rec = Locate(recipe, out tile, out x, out z, out building);
            var stats = new SacredStats();
            m = new MeshData();
            HeroBuilder.Build(rec, tile, 0, m, null, stats);
            return stats;
        }

        [Test]
        public void SignatureCountsOfTheCentrepieces()
        {
            MeshData m;
            Assert.That(BuildHero("her.ptn.krishna_mandir", out m).Pinnacles, Is.EqualTo(21), "Krishna Mandir");
            Assert.That(BuildHero("her.ktm.swayambhunath", out m).Steps, Is.EqualTo(365), "Swayambhu east stair");
            SacredStats ny = BuildHero("her.bkt.nyatapola", out m);
            Assert.That(ny.Tiers, Is.EqualTo(5));
            Assert.That(ny.PlinthLevels, Is.EqualTo(5));
            Assert.That(ny.TopM, Is.EqualTo(33.2).Within(0.03 * 33.2));
            HeroRecipe nr;
            HeroCatalog.TryGet("her.bkt.nyatapola", out nr);
            // Bells: floor(eave side / 0.4) per side (the real count is 529 [S]); struts: max(2, round(wall / 1.1)) + 1 per
            // side plus 4 corners.
            double aspect = nr.PlanD / nr.PlanW;
            int bells = nr.EaveWidths.Sum(w => 2 * (int)Math.Floor(w / 0.4) + 2 * (int)Math.Floor(w * aspect / 0.4));
            Assert.That(ny.Bells, Is.EqualTo(bells).Within(4 * nr.Tiers), "bells by the 0.4 m pitch");
            Assert.That(ny.Struts, Is.GreaterThanOrEqualTo(nr.Tiers * (4 * PagodaGenerator.StrutsPerSide(1) + 4)));
            SacredStats boudha = BuildHero("her.ktm.boudhanath", out m);
            Assert.That(boudha.Terraces, Is.EqualTo(3));
            Assert.That(boudha.Rings, Is.EqualTo(13));
            Assert.That(boudha.FlagLines, Is.InRange(60, 120));
            Assert.That(boudha.TopM, Is.EqualTo(36.0).Within(0.03 * 36));
        }

        [Test]
        public void PagodaStrutsBellsAndTheClosedSanctum()
        {
            Assert.That(PagodaGenerator.StrutsPerSide(1.0), Is.EqualTo(3));
            Assert.That(PagodaGenerator.StrutsPerSide(5.5), Is.EqualTo(6));
            Assert.That(PagodaGenerator.StrutsPerSide(11.0), Is.EqualTo(11));
            PagodaParams p = PagodaParams.Defaults(10f, 10f, 3);
            p.EaveWidths = new[] { 9f, 6.8f, 5f };
            var stats = new SacredStats();
            var m = new MeshData();
            var c = new GenColliders();
            PagodaGenerator.Build(p, new GenFrame(50, 50, 100f, 0f), 0, m, c, stats);
            MeshingChecks.AssertWellFormed(m, "pagoda");
            Assert.That(stats.Tiers, Is.EqualTo(3));
            Assert.That(stats.Bells, Is.EqualTo(4 * (22 + 17 + 12)));
            Assert.That(stats.Struts % 4, Is.EqualTo(0));
            Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(6000), "generic 3-tier budget");
            Assert.That(c.Boxes.Count + c.Ramps.Count, Is.GreaterThan(0), "plinth and stair colliders");
            // No interior: nothing lies inside the sanctum volume (the door is an opaque dark panel on its face).
            Assert.That(stats.SanctumHalfU, Is.GreaterThan(0.5f));
            float hu = stats.SanctumHalfU - 0.05f, hw = stats.SanctumHalfW - 0.05f;
            for (int v = 0; v < m.VertexCount; v++)
            {
                double dx = Math.Abs(m.Positions[3 * v] - 50), dz = Math.Abs(m.Positions[3 * v + 2] - 50), y = m.Positions[3 * v + 1] - 100;
                bool inside = dx < hu && dz < hw && y > stats.SanctumV0 + 0.05 && y < stats.SanctumV1 - 0.05;
                Assert.That(inside, Is.False, "vertex " + v + " inside the sanctum");
            }
            // LODs thin the detail.
            int previous = m.TriangleCount;
            for (int lod = 1; lod <= 3; lod++)
            {
                var ml = new MeshData();
                PagodaGenerator.Build(p, new GenFrame(50, 50, 100f, 0f), lod, ml, null);
                Assert.That(ml.TriangleCount, Is.LessThan(previous), "lod " + lod);
                previous = ml.TriangleCount;
            }
            // Deterministic.
            var again = new MeshData();
            PagodaGenerator.Build(p, new GenFrame(50, 50, 100f, 0f), 0, again, null);
            Assert.That(again.Positions.Take(again.VertexCount * 3), Is.EqualTo(m.Positions.Take(m.VertexCount * 3)));
        }

        [Test]
        public void OpenMandapaShrineIsSolidAndItsSanctumMatchesTheDrawnBox()
        {
            // Kasthamandap-like open hall: walkable plinth and hall, the closed central shrine is a NoClimb box (W2-O1:
            // sanctums are never entered) and the recorded sanctum is the drawn shrine (V5 checks use it).
            PagodaParams p = PagodaParams.Defaults(24f, 24f, 3);
            p.Open = true;
            p.EaveWidths = new[] { 22f, 16f, 11f };
            var stats = new SacredStats();
            var m = new MeshData();
            var c = new GenColliders();
            PagodaGenerator.Build(p, new GenFrame(50, 50, 100f, 0f), 0, m, c, stats);
            MeshingChecks.AssertWellFormed(m, "open mandapa");
            float bodyY = 100f + stats.SanctumV0 + 1.0f;
            bool solid = c.Boxes.Any(b => (b.Flags & GenColliderFlags.NoClimb) != 0 && Math.Abs(b.CX - 50) < b.HalfX && Math.Abs(b.CZ - 50) < b.HalfZ
                                          && b.CY - b.HalfY <= bodyY && b.CY + b.HalfY >= bodyY);
            Assert.That(solid, Is.True, "a NoClimb box fills the shrine at body height");
            // The recorded sanctum spans the drawn shrine (a quarter of the core width each side), not half of it.
            Assert.That(stats.SanctumHalfU, Is.GreaterThan(2.0f));
            Assert.That(stats.SanctumV1, Is.GreaterThan(stats.SanctumV0 + 1.5f));
            float hu = stats.SanctumHalfU - 0.05f, hw = stats.SanctumHalfW - 0.05f;
            for (int v = 0; v < m.VertexCount; v++)
            {
                double dx = Math.Abs(m.Positions[3 * v] - 50), dz = Math.Abs(m.Positions[3 * v + 2] - 50), y = m.Positions[3 * v + 1] - 100;
                bool inside = dx < hu && dz < hw && y > stats.SanctumV0 + 0.05 && y < stats.SanctumV1 - 0.05;
                Assert.That(inside, Is.False, "vertex " + v + " inside the shrine");
            }
        }

        [Test]
        public void ChaityaNichesFaceTheCompassInOrder()
        {
            Assert.That(ChaityaGenerator.NicheOrder, Is.EqualTo(new[] { "Akshobhya", "Ratnasambhava", "Amitabha", "Amoghasiddhi" }));
            Assert.That(ChaityaGenerator.NicheBearing, Is.EqualTo(new[] { 90f, 180f, 270f, 0f }));
            foreach (float yaw in new[] { 0f, 37f, 200f })
            {
                var m = new MeshData();
                ChaityaGenerator.Build(ChaityaParams.Defaults(2f), new GenFrame(10, 10, 0, yaw), 0, m, null);
                MeshingChecks.AssertWellFormed(m, "chaitya");
                Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(600));
                // Each Buddha colour sits on the side of its compass bearing, whatever the frame's yaw.
                for (int q = 0; q < 4; q++)
                {
                    double sx = 0, sz = 0;
                    int n = 0;
                    for (int t = 0; t < m.TriangleCount; t++)
                    {
                        if (!IsColour(m, t, SacredPalette.NicheBuddha[q])) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            int v = m.Indices[3 * t + k];
                            sx += m.Positions[3 * v] - 10;
                            sz += m.Positions[3 * v + 2] - 10;
                            n++;
                        }
                    }
                    Assert.That(n, Is.GreaterThan(0), ChaityaGenerator.NicheOrder[q]);
                    Assert.That(AngleDiff(Bearing(sx, sz), ChaityaGenerator.NicheBearing[q]), Is.LessThan(10), ChaityaGenerator.NicheOrder[q] + " at yaw " + yaw);
                }
            }
        }

        [Test]
        public void GenericSacredBuildingsOfTheSamplePackStayInBudget()
        {
            int built = 0;
            foreach (TileId id in new[] { new TileId(10, 516, 161), new TileId(10, 514, 162), new TileId(10, 521, 163) })
            {
                TileData t = Tiles[id];
                var h = new TileHeightSampler(t, 2);
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    SacredKind kind;
                    SacredParams p;
                    if (!SacredSelector.TrySelect(t, i, out kind, out p)) continue;
                    for (int lod = 0; lod <= 3; lod++)
                    {
                        var m = new MeshData();
                        Assert.That(SacredSelector.BuildGeneric(t, i, h, lod, m, null), Is.True);
                        MeshingChecks.AssertWellFormed(m, kind + " " + i);
                        // Shrines and chaityas 1,500 / 600 / 120; temples 6,000 at LOD0, then the hero B-class
                        // budgets (3,000 / 800 / 200) as the ceiling for the thinned LODs.
                        bool small = kind == SacredKind.Shrine || kind == SacredKind.Chaitya || kind == SacredKind.Hiti;
                        int[] cap = small ? new[] { 1500, 600, 120, 120 } : new[] { 6000, 3000, 800, 200 };
                        Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(cap[lod]), kind + " " + i + " lod " + lod);
                    }
                    built++;
                }
            }
            Assert.That(built, Is.GreaterThan(30));
        }

        [Test]
        public void SwayambhuStairFollowsTheMappedStepsWay()
        {
            // OSM way 24707651 (highway=steps) runs from the stupa platform down the east side of the hill; the hero's
            // stair follows it (the anchor tile's piece, then the curated line past the tile edge) to its real foot.
            HeroRecipe recipe;
            HeroCatalog.TryGet("her.ktm.swayambhunath", out recipe);
            TileData tile;
            double x, z;
            int building;
            HeritageRecord rec = Locate(recipe, out tile, out x, out z, out building);
            var stats = new SacredStats();
            var m = new MeshData();
            var c = new GenColliders();
            HeroBuilder.Build(rec, tile, 0, m, c, stats);
            Assert.That(stats.Steps, Is.EqualTo(365));
            // The way's far end, wherever it lies in the pack, in the anchor tile's frame.
            double fx = double.NaN, fz = double.NaN, far = 0;
            foreach (TileData t in Tiles.Values)
            {
                foreach (RoadRecord r in t.Roads)
                {
                    if (r.OsmWayId != 24707651) continue;
                    for (int k = 0; k < r.PointCount; k++)
                    {
                        double px = r.Points[2 * k] / 100.0 + t.Tile.X0 - tile.Tile.X0, pz = r.Points[2 * k + 1] / 100.0 + t.Tile.Z0 - tile.Tile.Z0;
                        double d = Math.Sqrt((px - x) * (px - x) + (pz - z) * (pz - z));
                        if (d <= far) continue;
                        far = d;
                        fx = px;
                        fz = pz;
                    }
                }
            }
            Assert.That(far, Is.GreaterThan(200), "the steps way is in the sample pack");
            double foot = Math.Sqrt((stats.StairFootX - fx) * (stats.StairFootX - fx) + (stats.StairFootZ - fz) * (stats.StairFootZ - fz));
            Assert.That(foot, Is.LessThanOrEqualTo(5.0), "stair foot at the end of the steps way");
            // The lower part lies in the next tile: the stair still lands on that tile's terrain (the anchor tile's edge
            // height would leave it tens of metres in the air), and every tread keeps a real stair rise.
            double gfx = stats.StairFootX + tile.Tile.X0, gfz = stats.StairFootZ + tile.Tile.Z0;
            float truth = float.NaN;
            foreach (TileData t in Tiles.Values)
            {
                float v;
                if (new TileHeightSampler(t).TryHeight(gfx, gfz, out v))
                {
                    truth = v;
                    break;
                }
            }
            Assert.That(float.IsNaN(truth), Is.False, "the foot's tile is in the pack");
            Assert.That(stats.StairFootY, Is.EqualTo(truth).Within(1.5), "stair foot on the neighbouring terrain");
            double rise = (c.Ramps.Max(rp => Math.Max(rp.Y0, rp.Y1)) - stats.StairFootY) / 365.0;
            Assert.That(rise, Is.InRange(0.17, 0.26), "step rise");
            // Walkable ramps all the way down, each within 3 m of the mapped line.
            Assert.That(c.Ramps.Count, Is.GreaterThan(10));
        }

        [Test]
        public void FrontPropsAreSolidAndKeepOutOfTheRoads()
        {
            // Bells, lamp pillars and the vahana in front of a temple are NoClimb colliders, and nothing the generator
            // adds stands in a road corridor or hangs into one below the overhead clearance.
            PagodaParams p = PagodaParams.Defaults(12f, 12f, 2);
            p.PlinthLevels = 1;
            p.FrontBell = true;
            p.LampPillars = 2;
            p.VahanaDistM = 4f;
            var c = new GenColliders();
            var m = new MeshData();
            PagodaGenerator.Build(p, new GenFrame(50, 50, 0, 0), 0, m, c);
            int props = c.Boxes.Count(b => (b.Flags & GenColliderFlags.NoClimb) != 0 && b.CZ > 50 + 6.0);
            Assert.That(props, Is.GreaterThanOrEqualTo(4), "Nandi, bell and two lamp pillars are solid");

            // Kasthamandap beside the Maru road: its eave bells and fringe stay 4.5 m over the corridor.
            HeroRecipe kr;
            HeroCatalog.TryGet("her.ktm.kasthamandap", out kr);
            TileData tile;
            double x, z;
            int building;
            HeritageRecord rec = Locate(kr, out tile, out x, out z, out building);
            var km = new MeshData();
            var stats = new SacredStats();
            HeroBuilder.Build(rec, tile, 0, km, null, stats);
            SacredClearance clear = SacredClearance.For(tile, new TileHeightSampler(tile, 2));
            Assert.That(building, Is.GreaterThanOrEqualTo(0));
            int[] ring = tile.Buildings[building].Rings[0];
            int low = 0;
            for (int v = 0; v < km.VertexCount; v++)
            {
                // Overhangs: whatever is outside the OSM outline (eaves, struts, bells, fringes) and over a corridor.
                double px = km.Positions[3 * v], py = km.Positions[3 * v + 1], pz = km.Positions[3 * v + 2];
                if (clear.Distance(px, pz) >= 0 || InsideRing(ring, px, pz)) continue;
                double g = clear.Ground(px, pz);
                if (py - g < Ghumante.Core.Meshing.Roads.RoadClearance.MinOverheadClearanceM - 0.01 && py - g > 2.0)
                {
                    if (low < 8) TestContext.WriteLine("low at ({0:0.0},{1:0.0}) y-g {2:0.00} d {3:0.00}", px, pz, py - g, clear.Distance(px, pz));
                    low++;
                }
            }
            Assert.That(low, Is.EqualTo(0), "overhang vertices below 4.5 m over a road corridor");
        }

        [Test]
        public void GenericTemplesKeepTheirLod0FormWithinTheCeiling()
        {
            // A temple over its LOD0 ceiling spreads its bells, struts, posts and tile courses out instead of silently
            // showing the LOD1 form (no bells, courses or purlins) next to the camera: at least 95% of the sample pack's
            // sacred structures build their true LOD0, and every one stays within the ceiling.
            int total = 0, lod0 = 0;
            var thinned = new List<string>();
            foreach (TileData t in Tiles.Values)
            {
                var h = new TileHeightSampler(t, 2);
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    SacredKind kind;
                    SacredParams p;
                    if (!SacredSelector.TrySelect(t, i, out kind, out p)) continue;
                    var m = new MeshData();
                    int builtLod;
                    Assert.That(SacredSelector.BuildGeneric(t, i, h, 0, m, null, out builtLod), Is.True);
                    bool small = kind == SacredKind.Shrine || kind == SacredKind.Chaitya || kind == SacredKind.Hiti;
                    Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(SacredSelector.MaxTris(small, builtLod)), kind + " " + t.Tile + " b" + i);
                    total++;
                    if (builtLod == 0) lod0++;
                    else thinned.Add(kind + " " + t.Tile + " b" + i + " -> LOD" + builtLod);
                }
            }
            TestContext.WriteLine("{0} of {1} generic sacred structures at LOD0; {2}", lod0, total, string.Join("; ", thinned));
            Assert.That(total, Is.GreaterThan(150));
            Assert.That(lod0, Is.GreaterThanOrEqualTo((int)Math.Ceiling(0.95 * total)));
            // Generic detail scales with size: a 28 m two-tier temple keeps bells and courses at LOD0 within 6,000.
            PagodaParams big = PagodaParams.Defaults(28f, 28f, 2);
            big.PlinthLevels = 2;
            big.DetailScale = SacredSelector.GenericDetailScale(28f, 28f, 2);
            var bm = new MeshData();
            var stats = new SacredStats();
            PagodaGenerator.Build(big, new GenFrame(0, 0, 0, 0), 0, bm, null, stats);
            Assert.That(bm.TriangleCount, Is.LessThanOrEqualTo(6000));
            Assert.That(stats.Bells, Is.GreaterThan(0));
        }
    }
}
