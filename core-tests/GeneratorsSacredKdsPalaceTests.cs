using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.KdsPalace;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Instancing;
using Ghumante.World.Sacred;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The bespoke Hanuman Dhoka palace site (Heroes/KdsPalace; docs/research/heritage/kds-palace.md): every structure at
    /// its true place, budgets and LOD ordering, walkable plinths, stairs and courts, the generic boxes hidden, one level
    /// under the palace, and the streamed world (TileBuild, as the game builds it) drawing the bespoke models.
    /// <see cref="DumpKdsPalaceSite"/> and <see cref="DumpKdsPalaceParts"/> (GHUMANTE_PREVIEW_DIR) write the in-situ scene
    /// and each part at LOD0-3 for the preview tool.
    /// </summary>
    public class GeneratorsSacredKdsPalaceTests
    {
        private static readonly TileId SiteTile = new TileId(10, 516, 161);

        private static TileData Tile()
        {
            TileData t = StreamingSampleRegion.Tile(SiteTile);
            Assume.That(t, Is.Not.Null, "the sample pack holds the Durbar Square tile");
            return t;
        }

        /// <summary>One part at a LOD (a fresh mesh).</summary>
        private static MeshData Part(KdsPalacePart part, int lod, GenColliders c = null, SacredStats stats = null)
        {
            var m = new MeshData();
            KdsPalaceSite.Build(part, Tile(), lod, m, c, stats);
            return m;
        }

        /// <summary>Highest vertex of a mesh (tile-local) and its bounds, in the site's local metres (x east, y north, h above
        /// the square).</summary>
        private static void Measure(MeshData m, out double topX, out double topY, out double topH, out double cx, out double cy)
        {
            KpFrame f = KpFrame.For(Tile(), 0, null);
            int best = 0;
            double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
            for (int i = 0; i < m.VertexCount; i++)
            {
                if (m.Positions[3 * i + 1] > m.Positions[3 * best + 1]) best = i;
                minX = Math.Min(minX, m.Positions[3 * i]);
                maxX = Math.Max(maxX, m.Positions[3 * i]);
                minZ = Math.Min(minZ, m.Positions[3 * i + 2]);
                maxZ = Math.Max(maxZ, m.Positions[3 * i + 2]);
            }
            f.FromTile(m.Positions[3 * best], m.Positions[3 * best + 2], out topX, out topY);
            topH = m.Positions[3 * best + 1] - KdsPalaceSite.LevelM;
            f.FromTile(0.5 * (minX + maxX), 0.5 * (minZ + maxZ), out cx, out cy);
        }

        [Test]
        public void EveryPartBuildsWithinItsBudgetAndItsLodsStepDown()
        {
            var report = new StringBuilder();
            int total0 = 0;
            for (int i = 0; i < KdsPalaceSite.PartCount; i++)
            {
                var part = (KdsPalacePart)i;
                int[] budget = KdsPalaceSite.BudgetOf(part);
                var tris = new int[4];
                for (int lod = 0; lod < 4; lod++)
                {
                    MeshData m = Part(part, lod);
                    tris[lod] = m.TriangleCount;
                    Assert.That(m.HasUv0, Is.True, part + " LOD" + lod + " writes UV0 (material channel and AO)");
                    Assert.That(tris[lod], Is.GreaterThan(0), part + " LOD" + lod);
                    Assert.That(tris[lod], Is.LessThanOrEqualTo(budget[lod]), part + " LOD" + lod + " within its ceiling");
                }
                Assert.That(tris[1], Is.LessThan(0.45 * tris[0]), part + ": LOD1 about a third of LOD0");
                Assert.That(tris[2], Is.LessThan(0.12 * tris[0]), part + ": LOD2 under an eighth of LOD0");
                Assert.That(tris[3], Is.LessThanOrEqualTo(200), part + ": LOD3 a few boxes");
                Assert.That(tris[2], Is.GreaterThan(tris[3]), part + ": LOD2 above LOD3");
                total0 += tris[0];
                report.AppendFormat(CultureInfo.InvariantCulture, "{0,-16} {1,6} {2,6} {3,5} {4,4}\n", part, tris[0], tris[1], tris[2], tris[3]);
            }
            report.AppendFormat(CultureInfo.InvariantCulture, "site LOD0 {0}\n", total0);
            TestContext.WriteLine(report.ToString());
            Assert.That(total0, Is.LessThanOrEqualTo(KdsPalaceSite.SiteBudgetLod0), "the whole palace at LOD0 stays in the monuments round's site allowance");
        }

        /// <summary>Each part's main finial or tower top where the brief measures it (kds-palace.md §0.2) and as tall.</summary>
        [TestCase(KdsPalacePart.Taleju, 45.5, 44.8, 2.0, 36.0)]
        [TestCase(KdsPalacePart.BasantapurTower, 1.6, -66.7, 2.5, 30.0)]
        [TestCase(KdsPalacePart.DeguTaleju, -0.1, 18.5, 3.0, 26.0)]
        [TestCase(KdsPalacePart.PanchMukhi, 19.5, -8.0, 2.5, 25.0)]
        [TestCase(KdsPalacePart.KirtipurTower, 16.5, -42.5, 3.0, 23.0)]
        [TestCase(KdsPalacePart.BhaktapurTower, 40.0, -47.5, 3.0, 19.5)]
        [TestCase(KdsPalacePart.LalitpurTower, 39.5, -71.0, 4.0, 21.0)] // the brief's point is an aerial estimate; the tower stands on its outline
        [TestCase(KdsPalacePart.Gate, -6.0, 3.0, 4.0, 23.0)]
        public void TopsStandWhereTheBriefMeasuresThem(KdsPalacePart part, double x, double y, double tol, double top)
        {
            double tx, ty, th, cx, cy;
            Measure(Part(part, 0), out tx, out ty, out th, out cx, out cy);
            TestContext.WriteLine(part + " top at (" + tx.ToString("0.0") + ", " + ty.ToString("0.0") + ") h " + th.ToString("0.0"));
            Assert.That(Math.Sqrt((tx - x) * (tx - x) + (ty - y) * (ty - y)), Is.LessThan(tol), part + " top position");
            Assert.That(th, Is.EqualTo(top).Within(1.2), part + " height");
        }

        /// <summary>The buildings measured from their OSM outlines sit on them (bounding-box centre).</summary>
        [TestCase(KdsPalacePart.KumariGhar, -102.3, -84.0, 2.0)]
        [TestCase(KdsPalacePart.GaddiBaithak, -69.0, -50.0, 6.0)]
        [TestCase(KdsPalacePart.NasalChowk, 6.0, -33.0, 6.0)]
        public void BuildingsSitOnTheirOutlines(KdsPalacePart part, double x, double y, double tol)
        {
            double tx, ty, th, cx, cy;
            Measure(Part(part, 0), out tx, out ty, out th, out cx, out cy);
            TestContext.WriteLine(part + " centre at (" + cx.ToString("0.0") + ", " + cy.ToString("0.0") + ")");
            Assert.That(Math.Sqrt((cx - x) * (cx - x) + (cy - y) * (cy - y)), Is.LessThan(tol), part + " centre");
        }

        /// <summary>Vertices of a material channel inside a palace-grid box (a0..a1, b0..b1, v0..v1 above the square).</summary>
        private static int CountInGridBox(MeshData m, MaterialChannel channel, double a0, double a1, double b0, double b1, double v0, double v1)
        {
            KpFrame f = KpFrame.For(Tile(), 0, null);
            int n = 0;
            for (int i = 0; i < m.VertexCount; i++)
            {
                if ((int)Math.Round(m.Uv0[2 * i]) != (int)channel) continue;
                double v = m.Positions[3 * i + 1] - KdsPalaceSite.LevelM;
                if (v < v0 || v > v1) continue;
                double x, y, a, b;
                f.FromTile(m.Positions[3 * i], m.Positions[3 * i + 2], out x, out y);
                KpFrame.ToGrid(x, y, out a, out b);
                if (a >= a0 && a <= a1 && b >= b0 && b <= b1) n++;
            }
            return n;
        }

        /// <summary>
        /// The tile roofs along the Basantapur fronts (photos basantapur_tower c_02, c_10, gaddi ref_01, ref_06): the Lohan
        /// south range carries its long pent roof at mid height over its carved lattice band (eave about 7.3 m); the white
        /// south-west wing has none (judge F16): three storeys under one deep tile roof whose eave (about 9.6 m) stands out
        /// over the square right above the second row of windows.
        /// </summary>
        [Test]
        public void PentRoofsRunAlongTheBasantapurFronts()
        {
            MeshData lohan = Part(KdsPalacePart.LohanChowk, 0), wing = Part(KdsPalacePart.SouthWing, 0);
            int l = CountInGridBox(lohan, MaterialChannel.RoofTile, 22.0, 46.2, KpCourts.SouthB - 1.7, KpCourts.SouthB - 0.2, 7.2, 9.0);
            int pent = CountInGridBox(wing, MaterialChannel.RoofTile, KpRanges.FrontA0, KpRanges.FrontA1, -77.5, -72.0, 7.0, 9.3);
            int eave = CountInGridBox(wing, MaterialChannel.RoofTile, KpRanges.FrontA0, KpRanges.FrontA1, -77.5, -72.0, 9.3, 10.8);
            TestContext.WriteLine("tile vertices: Lohan pent " + l + ", white wing below 9.3 m " + pent + ", white wing main eave " + eave);
            Assert.That(l, Is.GreaterThanOrEqualTo(8), "the Lohan south range carries its pent roof over the square");
            Assert.That(pent, Is.EqualTo(0), "no pent roof on the white wing");
            Assert.That(eave, Is.GreaterThanOrEqualTo(8), "the white wing's main eave stands out over the square at about 9.6 m");
        }

        /// <summary>The Gaddi Baithak portico's three tall arched recesses (gaddi c_08, c_09): the arcade face stands about
        /// 0.9 m out from the hall's west wall between the arches, from the podium to the capitals.</summary>
        [Test]
        public void GaddiPorticoHasItsArcadeOfRecesses()
        {
            MeshData g = Part(KdsPalacePart.GaddiBaithak, 0);
            double a = KpGaddi.HA0;
            int piers = CountInGridBox(g, MaterialChannel.Plaster, a - 1.0, a - 0.8, KpGaddi.PortB - 8.3, KpGaddi.PortB + 8.3, 7.0, 13.0);
            TestContext.WriteLine("arcade face vertices " + piers);
            Assert.That(piers, Is.GreaterThan(8), "the arcade face stands out of the hall wall");
        }

        [Test]
        public void CatalogueCarriesTheMeasuredSiteAndItsParts()
        {
            for (int i = 0; i < KdsPalaceSite.PartCount; i++)
            {
                var part = (KdsPalacePart)i;
                string id = KdsPalaceSite.IdOf(part);
                HeroRecipe r;
                Assert.That(HeroCatalog.TryGet(id, out r), Is.True, id + " is in the catalogue");
                Assert.That(r.Measured, Is.True, id);
                Assert.That(r.BudgetTris, Is.Not.Null, id);
                Assert.That(r.Budget(0), Is.EqualTo(KdsPalaceSite.BudgetOf(part)[0]), id);
                Assert.That(KdsPalaceSite.Handles(id), Is.True, id);
                if (i >= 5) Assert.That(r.SiteOf, Is.EqualTo(KdsPalaceSite.GateId), id + " is a part of the gate's site");
            }
            Assert.That(KdsPalaceSite.Handles("her.ktm.jagannath"), Is.False, "the square's temples stay with their own package");
        }

        [Test]
        public void PlinthsStairsAndCourtsAreWalkable()
        {
            var c = new GenColliders();
            Part(KdsPalacePart.Taleju, 0, c);
            int walk = 0;
            float top = 0;
            foreach (GenBox b in c.Boxes)
                if ((b.Flags & GenColliderFlags.Walkable) != 0)
                {
                    walk++;
                    top = Math.Max(top, b.CY + b.HalfY);
                }
            Assert.That(walk, Is.GreaterThanOrEqualTo(11), "Taleju: the plinth stages are walkable");
            Assert.That(top - KdsPalaceSite.LevelM, Is.EqualTo(6.8).Within(0.1), "Taleju: the top stage under the temple");
            Assert.That(c.Ramps.Count, Is.GreaterThanOrEqualTo(3), "Taleju: the stairs up the plinth, through the wall and to the temple");
            foreach (KdsPalacePart part in new[] { KdsPalacePart.Gate, KdsPalacePart.NasalChowk, KdsPalacePart.LohanChowk, KdsPalacePart.KumariGhar, KdsPalacePart.Dakha })
            {
                var cc = new GenColliders();
                Part(part, 0, cc);
                bool any = false;
                foreach (GenBox b in cc.Boxes) any |= (b.Flags & GenColliderFlags.Walkable) != 0;
                Assert.That(any || cc.Ramps.Count > 0, Is.True, part + ": its plinth, platform or steps are walkable");
                var none = new GenColliders();
                Part(part, 1, none);
                Assert.That(none.Boxes.Count + none.Ramps.Count, Is.EqualTo(0), part + ": colliders come with LOD0 only");
            }
        }

        [Test]
        public void GroundIsOneLevelUnderThePalace()
        {
            TileData t = Tile();
            KpGround.Apply(t);
            Assert.That(KpGround.Apply(t), Is.EqualTo(0), "idempotent");
            var h = new TileHeightSampler(t);
            double ox, oz;
            WorldFrame.LonLatToGame(KdsPalaceSite.OriginLon, KdsPalaceSite.OriginLat, out ox, out oz);
            // Nasal Chowk, the gate apron, Lohan Chowk, the Taleju plinth, Kumari Ghar (local metres).
            double[] pts = { 4, -34, -14, -2, 30.5, -57.5, 45, 44, -102, -84 };
            for (int i = 0; i < pts.Length; i += 2)
            {
                float g;
                Assert.That(h.TryHeight(ox + pts[i], oz + pts[i + 1], out g), Is.True);
                Assert.That(g, Is.EqualTo(KdsPalaceSite.LevelM).Within(0.2), "level at (" + pts[i] + ", " + pts[i + 1] + ")");
            }
        }

        [Test]
        public void BuildIsDeterministic()
        {
            MeshData a = Part(KdsPalacePart.Gate, 0), b = Part(KdsPalacePart.Gate, 0);
            Assert.That(a.VertexCount, Is.EqualTo(b.VertexCount));
            for (int i = 0; i < a.VertexCount * 3; i++) Assert.That(a.Positions[i], Is.EqualTo(b.Positions[i]));
        }

        /// <summary>The streamer (TileBuild.Execute, as the game builds the tile) draws every bespoke part from the site code,
        /// and the generic house boxes the palace replaces are hidden from the building bands.</summary>
        [Test]
        public void StreamedTileDrawsTheBespokePalaceAndHidesTheGenericBoxes()
        {
            MeshingSettings s = StreamingScene.Settings(2, 10);
            TileBuild b = StreamingScene.Build(SiteTile, s);
            Assert.That(b.Error, Is.Null);
            for (int i = 0; i < KdsPalaceSite.PartCount; i++)
            {
                var part = (KdsPalacePart)i;
                string id = KdsPalaceSite.IdOf(part);
                HeroPiece? found = null;
                foreach (HeroPiece p in b.Heroes)
                    if (p.Id == id) found = p;
                Assert.That(found.HasValue, Is.True, id + " is streamed");
                Assert.That(found.Value.Tris0, Is.EqualTo(Part(part, 0).TriangleCount), id + ": the streamed LOD0 is the bespoke model");
                Assert.That(found.Value.TopY - found.Value.GroundY, Is.EqualTo(KdsPalaceSite.TopOf(part)).Within(1.5), id + " height");
            }
            ISet<ulong> hidden = StreamingScene.Heroes.HiddenRefs;
            foreach (string r in new[] { "w420797415", "w1374620542", "w906985354", "w906935471", "w910121098", "w910121099", "w906985353", "w429890388", "w430743883" })
                Assert.That(hidden.Contains(HeritageRecord.WayRelationRef(r)), Is.True, r + " is hidden from the house bands");
        }

        /// <summary>
        /// No generic house of the B0 band stands inside the palace outline west of the gate (round r8: the house grammar
        /// built the white ranges continuing the Dakha front as modern apartment boxes): the centroid of every building the
        /// streamer still draws lies outside the union of the Dakha range (w420797415), Gaddi Baithak (w247501389), the
        /// Hnulunche range (w420797472) and the four ranges west of the Dakha front (w907080042, w1415152801, w1029965548,
        /// w1117943799).
        /// </summary>
        [Test]
        public void NoGenericHouseStandsInsideThePalaceOutline()
        {
            TileData t = Tile();
            ISet<ulong> hidden = StreamingScene.Heroes.HiddenRefs;
            string[] outline = { "w420797415", "w247501389", "w420797472", "w907080042", "w1415152801", "w1029965548", "w1117943799" };
            var rings = new List<int[]>();
            foreach (string id in outline)
            {
                ulong r = HeritageRecord.WayRelationRef(id);
                Assert.That(hidden.Contains(r), Is.True, id + " is hidden from the house bands");
                foreach (BuildingRecord b in t.Buildings)
                    if (b.OsmRef == r && b.Rings != null && b.Rings.Length > 0) rings.Add(b.Rings[0]);
            }
            Assert.That(rings.Count, Is.GreaterThanOrEqualTo(5), "the outline's ways are in the sample pack");
            int drawn = 0;
            foreach (BuildingRecord b in t.Buildings)
            {
                if (hidden.Contains(b.OsmRef) || b.Rings == null || b.Rings.Length == 0) continue;
                int[] ring = b.Rings[0];
                int n = ring.Length / 2;
                if (n < 3) continue;
                double cx = 0, cz = 0;
                for (int i = 0; i < n; i++)
                {
                    cx += ring[2 * i];
                    cz += ring[2 * i + 1];
                }
                cx /= n;
                cz /= n;
                drawn++;
                foreach (int[] o in rings)
                    Assert.That(Inside(o, cx, cz), Is.False,
                                "house " + b.OsmRef + " (centroid " + (cx / 100).ToString("0.0") + ", " + (cz / 100).ToString("0.0") + ") stands inside the palace outline");
            }
            Assert.That(drawn, Is.GreaterThan(0));
        }

        private static bool Inside(int[] r, double x, double z)
        {
            int n = r.Length / 2;
            bool inside = false;
            for (int i = 0, k = n - 1; i < n; k = i++)
            {
                double xi = r[2 * i], zi = r[2 * i + 1], xk = r[2 * k], zk = r[2 * k + 1];
                if ((zi > z) != (zk > z) && x < (xk - xi) * (z - zi) / (zk - zi) + xi) inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// Round r8 judge findings, checked on the geometry: Taleju's lowest roof about 1.4 × its body and its gilt-window
        /// storey about 7.5 m tall; the forecourt paved in grey flags wall to wall in front of the gate; Gaddi Baithak's
        /// south front a colonnade standing proud of a recessed loggia wall; the railing along the white wing on Basantapur.
        /// </summary>
        [Test]
        public void JudgedProportionsAndFrontagesHold()
        {
            MeshData gate = Part(KdsPalacePart.Gate, 0);
            int flags = CountInGridBox(gate, MaterialChannel.Flagstone, KpGround.ForecourtA0, KpGround.ForecourtA1, KpGround.ForecourtB0, KpGround.ForecourtB1, 0.0, 0.05);
            TestContext.WriteLine("forecourt flag vertices " + flags);
            Assert.That(flags, Is.GreaterThan(1500), "the forecourt is paved in flags");
            MeshData gaddi = Part(KdsPalacePart.GaddiBaithak, 0);
            // Gaddi's south front: open between the giant columns (b HB0 + 0.45) and the loggia wall 2.5 m behind the podium edge.
            int wallInFront = CountInGridBox(gaddi, MaterialChannel.Plaster, KpGaddi.HA0 + 6.0, KpGaddi.HA1 - 6.0, KpGaddi.HB0 + 1.1, KpGaddi.HB0 + 2.2, 9.0, 13.0);
            int loggiaWall = CountInGridBox(gaddi, MaterialChannel.Plaster, KpGaddi.HA0 + 6.0, KpGaddi.HA1 - 6.0, KpGaddi.HB0 + KpGaddi.LoggiaBack - 0.15,
                                            KpGaddi.HB0 + KpGaddi.LoggiaBack + 0.05, 2.0, 14.0);
            TestContext.WriteLine("Gaddi plaster vertices between the columns and the loggia wall " + wallInFront + ", on the loggia wall " + loggiaWall);
            Assert.That(wallInFront, Is.LessThan(10), "the south loggia is open behind the columns");
            Assert.That(loggiaWall, Is.GreaterThan(100), "the loggia wall with its arched openings stands 2.5 m back");
            MeshData wing = Part(KdsPalacePart.SouthWing, 0);
            int rail = CountInGridBox(wing, MaterialChannel.Metal, KpRanges.FrontA0, KpRanges.FrontA1, -79.0, -73.0, 0.2, 1.6);
            TestContext.WriteLine("white wing railing vertices " + rail);
            Assert.That(rail, Is.GreaterThan(200), "the black railing runs along the white wing on Basantapur");
        }

        // =============================================================================================================
        // Previews
        // =============================================================================================================

        /// <summary>The parts named in GHUMANTE_KDSP_PARTS (comma separated part names, e.g. "Gate,Taleju"; all when unset).</summary>
        private static bool Wanted(KdsPalacePart part)
        {
            string only = Environment.GetEnvironmentVariable("GHUMANTE_KDSP_PARTS");
            if (string.IsNullOrEmpty(only)) return true;
            foreach (string s in only.Split(','))
                if (string.Equals(s.Trim(), part.ToString(), StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Every part (or those in GHUMANTE_KDSP_PARTS) at LOD0-3 on its own, in tile-local metres, for the preview
        /// tool (GHUMANTE_PREVIEW_DIR), with a triangle report.</summary>
        [Test]
        public void DumpKdsPalaceParts()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the palace");
            var report = new StringBuilder();
            for (int i = 0; i < KdsPalaceSite.PartCount; i++)
            {
                var part = (KdsPalacePart)i;
                if (!Wanted(part)) continue;
                report.AppendFormat(CultureInfo.InvariantCulture, "{0,-16}", part);
                for (int lod = 0; lod <= 3; lod++)
                {
                    MeshData m = Part(part, lod);
                    ObjDump.Write(m, "parts/" + part.ToString().ToLowerInvariant() + "_lod" + lod + ".obj", new[] { "meshpreview: views=4" });
                    report.AppendFormat(CultureInfo.InvariantCulture, " {0,7}", m.TriangleCount);
                }
                report.Append('\n');
            }
            // Merge with the lines of the parts not dumped this time.
            string path = Path.Combine(ObjDump.Dir, "parts", "report.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var lines = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (File.Exists(path))
                foreach (string l in File.ReadAllLines(path))
                    if (l.Length > 16) lines[l.Substring(0, 16).Trim()] = l;
            foreach (string l in report.ToString().Split('\n'))
                if (l.Length > 16) lines[l.Substring(0, 16).Trim()] = l;
            File.WriteAllLines(path, lines.Values);
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// The site as the streamer builds it (TileBuild.Execute, tier 2, October): the terrain, roads, areas, the heroes as
        /// uploaded (LOD0 chunks; LOD1 and LOD2 in their own files) and the house bands round the palace, cropped to the site.
        /// </summary>
        [Test]
        public void DumpKdsPalaceSite()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the palace");
            MeshingSettings s = StreamingScene.Settings(2, 10);
            TileBuild b = StreamingScene.Build(SiteTile, s);
            Assert.That(b.Error, Is.Null);
            double ox, oz;
            WorldFrame.LonLatToGame(KdsPalaceSite.OriginLon, KdsPalaceSite.OriginLat, out ox, out oz);
            float cx = (float)(ox - SiteTile.X0), cz = (float)(oz - SiteTile.Z0);
            float x0 = cx - 200, x1 = cx + 150, z0 = cz - 170, z1 = cz + 150;
            var directives = new[] { "kds-palace site: tile " + SiteTile + " tier 2", "meshpreview: grid=0 sun=160,62" };
            void Put(string layer, MeshData m)
            {
                MeshData cropped = ObjDump.Crop(m, x0, z0, x1, z1, true);
                if (cropped.TriangleCount > 0) ObjDump.Write(cropped, "site/" + layer + ".obj", directives);
            }
            Put("terrain", StreamingScene.Layer(b, TileLayers.Terrain));
            Put("roads", StreamingScene.Layer(b, TileLayers.Roads));
            Put("decals", StreamingScene.Layer(b, TileLayers.RoadDecals));
            Put("areas", StreamingScene.Layer(b, TileLayers.Areas));
            Put("heroes", StreamingScene.Layer(b, TileLayers.Heroes, p => p % 4 == 0));
            Put("heroes_lod1", StreamingScene.Layer(b, TileLayers.Heroes, p => p % 4 == 1));
            Put("heroes_lod2", StreamingScene.Layer(b, TileLayers.Heroes, p => p % 4 == 2));
            var near = new MeshData();
            BuildingMesher.Build(b.Source, b.Sampler, BuildingBandTable.LiteOptions(2, s.DetailBuildings), near);
            Put("buildings_b0", near);
            Put("ornaments", StreamingScene.Pieces(b, TileLayers.Ornaments, 2, cx, cz));
            var trees = new MeshData();
            trees.HasUv0 = true;
            var unit = new MeshData();
            foreach (TreeInstance t in b.Instances.Trees)
            {
                if (t.X < x0 || t.X > x1 || t.Z < z0 || t.Z > z1) continue;
                unit.Clear();
                FloraMesher.Build(t.Species, 1, 10, unit);
                FloraObj.Append(trees, unit, t.X, t.Y - 0.1f, t.Z, t.YawDeg, t.CrownM, t.HeightM, t.CrownM);
            }
            Put("trees", trees);
            var report = new StringBuilder();
            report.AppendFormat(CultureInfo.InvariantCulture, "origin tile-local {0:0.00},{1:0.00}\n", cx, cz);
            foreach (HeroPiece p in b.Heroes)
                report.AppendFormat(CultureInfo.InvariantCulture, "{0,-40} {1,6} {2,6} {3,6} {4,4}  top {5:0.0} r {6:0.0}\n", p.Id, p.Tris0, p.Tris1, p.Tris2, p.Tris3,
                                    p.TopY - p.GroundY, p.RadiusM);
            Directory.CreateDirectory(Path.Combine(ObjDump.Dir, "site"));
            File.WriteAllText(Path.Combine(ObjDump.Dir, "site", "report.txt"), report.ToString());
            TestContext.WriteLine(report.ToString());
        }
    }
}
