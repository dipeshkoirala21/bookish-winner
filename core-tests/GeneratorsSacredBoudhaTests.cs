using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.Boudha;
using Ghumante.Core.Meshing;
using Ghumante.World.Sacred;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The bespoke Boudhanath site (W2 monuments round, docs/research/heritage/boudha.md): built through the game's own
    /// stack (the curated record of the kathmandu_core sample, <see cref="TileBuild.Execute"/> at the High tier), it
    /// stands on its OSM footprint at the measured section, holds its LOD budgets, flattens the plaza, hides the
    /// front-row houses it rebuilds and lets the player walk the terraces. <see cref="DumpBoudhaScene"/>
    /// (GHUMANTE_PREVIEW_DIR) writes the in-situ scene for tools/mesh-preview.
    /// </summary>
    public class GeneratorsSacredBoudhaTests
    {
        /// <summary>The level-10 tile of the stupa.</summary>
        private static readonly TileId Tile = new TileId(10, 521, 163);

        private static TileBuild _high;
        private static readonly object Lock = new object();

        /// <summary>The Boudha tile as the High tier streams it (built once).</summary>
        private static TileBuild High
        {
            get
            {
                lock (Lock)
                {
                    if (_high == null)
                    {
                        MeshingSettings s = StreamingScene.Settings(2, 10);
                        _high = StreamingScene.Build(Tile, s);
                    }
                    return _high;
                }
            }
        }

        private static HeritageRecord Record()
        {
            foreach (HeritageRecord r in Curated().Heritage)
                if (r.Id == BoudhaSite.HeroId) return r;
            Assert.Fail("no " + BoudhaSite.HeroId + " in the sample's curated DB");
            return null;
        }

        /// <summary>The catalogue record of a site part (the region DB holds only the curated hero; HeroSet adds the parts).</summary>
        private static HeritageRecord PartRecord(BoudhaPart part)
        {
            HeroRecipe recipe;
            Assert.That(HeroCatalog.TryGet(BoudhaSite.IdOf(part), out recipe), Is.True, part.ToString());
            return HeroCatalog.ToRecord(recipe);
        }

        private static CuratedDb _db;

        private static CuratedDb Curated()
        {
            lock (Lock)
            {
                if (_db == null)
                    _db = CuratedDb.Read(File.ReadAllBytes(Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.curated.ghcd")));
                return _db;
            }
        }

        /// <summary>A fresh decode of the tile with the plaza ground applied (as TileBuild does).</summary>
        private static TileData Source()
        {
            TileData t = StreamingSampleRegion.Tile(Tile);
            HeroBuilder.PrepareGround(t);
            return t;
        }

        [Test]
        public void TheStreamedTileBuildsTheBespokeSiteWithinItsBudgets()
        {
            // Every part of the site is its own hero in the anchor tile (so each picks its LOD by its own screen size),
            // each within its ceilings, LOD1 about 30 % and LOD2 about 8 % of LOD0, the whole within the site's budget.
            TileBuild b = High;
            int total = 0, budget = 0;
            var fails = new List<string>();
            foreach (HeroPiece h in b.Heroes)
                if (BoudhaSite.Handles(h.Id)) TestContext.WriteLine("GEOM " + h.Id + " " + h.CX + " " + h.CZ + " " + h.GroundY + " " + h.TopY + " " + h.Tris0 + " " + h.Tris1 + " " + h.Tris2 + " " + h.Tris3);
            {
                BoudhaFrame gf;
                Assert.That(BoudhaSite.TryFrame(Record(), b.Source, out gf), Is.True);
                double x0, z0, x1, z1, x2, z2;
                gf.ToTile(0, 0, out x0, out z0);
                gf.ToTile(1, 0, out x1, out z1);
                gf.ToTile(0, 1, out x2, out z2);
                TestContext.WriteLine("FRAME " + x0 + " " + z0 + " " + x1 + " " + z1 + " " + x2 + " " + z2 + " " + gf.Level);
            }
            foreach (BoudhaPart part in BoudhaSite.AllParts)
            {
                string id = BoudhaSite.IdOf(part);
                HeroPiece p = default(HeroPiece);
                bool found = false;
                foreach (HeroPiece h in b.Heroes)
                    if (h.Id == id)
                    {
                        p = h;
                        found = true;
                    }
                Assert.That(found, Is.True, id + " is built in its anchor tile");
                int[] cap = BoudhaSite.BudgetOf(part);
                TestContext.WriteLine(id + " tris LOD0..3: " + p.Tris0 + " " + p.Tris1 + " " + p.Tris2 + " " + p.Tris3 + " of " + string.Join(" ", cap) + ", top " +
                                      (p.TopY - p.GroundY));
                int[] tris = { p.Tris0, p.Tris1, p.Tris2, p.Tris3 };
                for (int l = 0; l < 4; l++)
                    if (tris[l] > cap[l]) fails.Add(id + " LOD" + l + " " + tris[l] + " over its ceiling " + cap[l]);
                int floor = part == BoudhaPart.Stupa ? 12000 : part == BoudhaPart.Gate ? 4000 : BoudhaSite.IsFlags(part) ? 1500 : 2000;
                if (p.Tris0 <= floor) fails.Add(id + " LOD0 " + p.Tris0 + " is not bespoke (at most " + floor + ")");
                if (p.Tris1 > 0.35 * p.Tris0) fails.Add(id + " LOD1 " + p.Tris1 + " over 35 % of LOD0 " + p.Tris0);
                // LOD2 keeps the identity (the eyes and arcs on the stupa, the house fronts round the kora): within 15 %.
                if (p.Tris2 > 0.15 * p.Tris0) fails.Add(id + " LOD2 " + p.Tris2 + " over 15 % of LOD0 " + p.Tris0);
                if (p.Tris3 <= 0) fails.Add(id + " has no LOD3");
                total += p.Tris0;
                budget += cap[0];
            }
            Assert.That(fails, Is.Empty, string.Join("\n", fails));
            Assert.That(budget, Is.LessThanOrEqualTo(BoudhaSite.SiteBudgetLod0), "the parts' ceilings add up to the site's");
            Assert.That(total, Is.LessThanOrEqualTo(BoudhaSite.SiteBudgetLod0));
        }

        /// <summary>The hero LODs the streamer picks with the camera at <paramref name="eye"/> (tile-local x, absolute y, z):
        /// the screen heights as WorldStreamer.UpdateHeroes computes them (fov 60°), allocated by the tier's hero slice
        /// (World/Sacred/HeroLodBudget) frame after frame until it settles.</summary>
        private static float[] LastScreens;

        private static int[] Allocate(TileBuild b, double ex, double ey, double ez, int budget, bool lod0)
        {
            int n = b.Heroes.Count;
            var entries = new HeroLodBudget.Entry[n];
            double tanHalf = Math.Tan(30.0 * Math.PI / 180.0);
            for (int i = 0; i < n; i++)
            {
                HeroPiece p = b.Heroes[i];
                double dx = p.CX - ex, dz = p.CZ - ez, dy = 0.5 * (p.GroundY + p.TopY) - ey;
                double d = Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy + dz * dz));
                entries[i] = new HeroLodBudget.Entry
                {
                    ScreenHeight = (float)((p.TopY - p.GroundY) / (2.0 * d * tanHalf)), Tris0 = p.Tris0, Tris1 = p.Tris1, Tris2 = p.Tris2, Tris3 = p.Tris3,
                    CurrentLod = -1,
                };
            }
            var alloc = new HeroLodBudget(budget, lod0);
            int tris = 0;
            for (int frame = 0; frame < 40; frame++) tris = alloc.Allocate(entries, n, frame * 1.0f);
            Assert.That(tris, Is.LessThanOrEqualTo(budget), "the hero slice holds");
            var lods = new int[n];
            for (int i = 0; i < n; i++) lods[i] = entries[i].Lod;
            LastScreens = new float[n];
            for (int i = 0; i < n; i++) LastScreens[i] = entries[i].ScreenHeight;
            return lods;
        }

        [Test]
        public void TheKoraKeepsWhatStandsBesideThePlayerInDetailOnEveryTier()
        {
            // Walking the kora at chase-camera height (r 58 m from the stupa axis, 3.5 m up, fov 60°) every 10°, up the lane
            // from the main gate and on the road before it, with the tiers' hero slices (High 60,000, Mid 40,000, Low 20,000
            // without LOD0): the stupa's shell keeps LOD0 or LOD1 on High and Mid and at least LOD2 (with the eyes) on Low,
            // the kora sector the player stands in keeps LOD1 or better, and on High and Mid every part whose centre is within
            // 25 m of the eye on the kora keeps LOD1 or better, as does the main gate seen from the road.
            TileBuild b = High;
            TileData src = b.Source;
            HeritageRecord r = Record();
            BoudhaFrame f;
            Assert.That(BoudhaSite.TryFrame(r, src, out f), Is.True);
            var spots = new List<(string, double, double)>();
            for (int brg = 0; brg < 360; brg += 10)
            {
                double a = brg * Math.PI / 180.0;
                spots.Add(("kora " + brg, 58 * Math.Sin(a), 58 * Math.Cos(a)));
            }
            spots.Add(("gate lane", -6.5, -66.0));
            spots.Add(("lane mouth", -6.0, -56.0));
            spots.Add(("main gate", -9.0, -93.0));
            int shell = -1;
            for (int i = 0; i < b.Heroes.Count; i++)
                if (b.Heroes[i].Id == BoudhaSite.HeroId) shell = i;
            Assert.That(shell, Is.GreaterThanOrEqualTo(0));
            var fails = new List<string>();
            foreach (var (tier, budget, lod0) in new[] { ("High", 60000, true), ("Mid", 40000, true), ("Low", 20000, false) })
            {
                foreach (var (name, u, v) in spots)
                {
                    double ex, ez;
                    f.ToTile(u, v, out ex, out ez);
                    double ey = f.Level + 3.5;
                    int[] lods = Allocate(b, ex, ey, ez, budget, lod0);
                    var line = new StringBuilder(tier + " " + name + ":");
                    BoudhaPart inner = BoudhaSite.InnerAt(u, v), outer = BoudhaSite.Sectors[0].Part;
                    foreach (BoudhaSector sec in BoudhaSite.Sectors)
                        if (BoudhaSector.In(BoudhaSite.BearingOf(u, v), sec.Out0, sec.Out1)) outer = sec.Part;
                    for (int i = 0; i < b.Heroes.Count; i++)
                    {
                        HeroPiece p = b.Heroes[i];
                        BoudhaPart part;
                        if (!BoudhaSite.TryPart(p.Id, out part)) continue;
                        line.Append(' ').Append(p.Id.Substring(BoudhaSite.HeroId.Length)).Append('=').Append(lods[i]).Append('(').Append(LastScreens[i].ToString("0.00")).Append(')');
                        double dist = Math.Sqrt((p.CX - ex) * (p.CX - ex) + (p.CZ - ez) * (p.CZ - ez));
                        if (i == shell && lods[i] > (lod0 ? 1 : 2)) fails.Add(tier + " " + name + ": the stupa at LOD" + lods[i]);
                        if (name.StartsWith("kora", StringComparison.Ordinal) && (part == inner || part == outer) && lods[i] > 1)
                            fails.Add(tier + " " + name + ": " + p.Id + " under the camera at LOD" + lods[i]);
                        if (lod0 && (name.StartsWith("kora", StringComparison.Ordinal) || name == "main gate") && dist < 25 && lods[i] > 1)
                            fails.Add(tier + " " + name + ": " + p.Id + " " + dist.ToString("0") + " m off at LOD" + lods[i]);
                    }
                    TestContext.WriteLine(line.ToString());
                }
            }
            Assert.That(fails, Is.Empty, string.Join("\n", fails));
        }

        [Test]
        public void TheStupaStandsOnItsFootprintAtTheMeasuredSection()
        {
            TileData t = Source();
            HeritageRecord r = Record();
            var m = new MeshData();
            var stats = new SacredStats();
            int tris = HeroBuilder.Build(r, t, 0, m, new GenColliders(), stats);
            Assert.That(tris, Is.GreaterThan(0));
            double ox, oz;
            Assert.That(BoudhaGround.TryCentre(t, out ox, out oz), Is.True);
            double level = BoudhaGround.KoraLevel(t, ox, oz);
            // The finial tip 44.8 m above the kora on the stupa axis.
            float top = float.MinValue;
            float tx = 0, tz = 0;
            for (int i = 0; i < m.VertexCount; i++)
            {
                float y = m.Positions[3 * i + 1];
                if (y > top)
                {
                    top = y;
                    tx = m.Positions[3 * i];
                    tz = m.Positions[3 * i + 2];
                }
            }
            Assert.That(top - level, Is.EqualTo(BoudhaStupa.Tip).Within(0.3), "finial tip above the kora");
            Assert.That(Math.Sqrt((tx - ox) * (tx - ox) + (tz - oz) * (tz - oz)), Is.LessThan(2.0), "the tip over the T1 centre");
            Assert.That(stats.TopM, Is.EqualTo((float)BoudhaStupa.Tip).Within(0.01f));
            // The T1 outline (OSM w56688295): every vertex lies within 1.5 m of the drawn terrace edge.
            int[] ring = null;
            foreach (BuildingRecord b in t.Buildings)
                if (b.OsmRef == 2 * BoudhaSite.T1Way) ring = b.Rings[0];
            Assert.That(ring, Is.Not.Null);
            BoudhaFrame f;
            Assert.That(BoudhaSite.TryFrame(r, t, out f), Is.True);
            double[] pu = new double[32], pv = new double[32];
            int n = BoudhaStupa.Terrace(0, pu, pv);
            Affine3Inv(f, out double c, out double s);
            double worst = 0;
            for (int i = 0; i < ring.Length / 2; i++)
            {
                double dx = ring[2 * i] / 100.0 - f.AX, dz = ring[2 * i + 1] / 100.0 - f.AZ;
                double u = dx * c - dz * s, v = dx * s + dz * c;
                worst = Math.Max(worst, BoudhaKit.EdgeDistance(pu, pv, n, u, v));
            }
            Assert.That(worst, Is.LessThan(1.5), "T1 outline follows OSM");
        }

        /// <summary>Site-local axes of the frame: u = x cos − z sin, v = x sin + z cos for a 9° turn.</summary>
        private static void Affine3Inv(in BoudhaFrame f, out double c, out double s)
        {
            double a = BoudhaSite.AxisBearingDeg * Math.PI / 180.0;
            c = Math.Cos(a);
            s = Math.Sin(a);
        }

        [Test]
        public void ThePlazaIsOneFlatLevel()
        {
            TileData t = Source();
            double ox, oz;
            Assert.That(BoudhaGround.TryCentre(t, out ox, out oz), Is.True);
            double level = BoudhaGround.KoraLevel(t, ox, oz);
            var h = new TileHeightSampler(t);
            double lo = double.MaxValue, hi = double.MinValue;
            for (int r = 0; r <= 75; r += 5)
                for (int a = 0; a < 360; a += 10)
                {
                    double x = ox + r * Math.Sin(a * Math.PI / 180), z = oz + r * Math.Cos(a * Math.PI / 180);
                    float y;
                    Assert.That(h.TryHeight(t.Tile.X0 + x, t.Tile.Z0 + z, out y), Is.True);
                    lo = Math.Min(lo, y);
                    hi = Math.Max(hi, y);
                }
            Assert.That(hi - lo, Is.LessThan(0.2), "plaza flat within r 75 m");
            Assert.That(Math.Abs(lo - level), Is.LessThan(0.2));
            Assert.That(BoudhaGround.Apply(t), Is.EqualTo(0), "idempotent");
        }

        [Test]
        public void TheFarStupaKeepsItsEyesAndItsWhiteDome()
        {
            // LOD2 (the Low tier's view from the kora) and LOD3 (the far shape) keep Boudha's identity: the dark-blue eyes on
            // all four faces of the harmika, and at LOD2 a white dome (at least 60 % of the dome surface white, the arcade
            // and the cap washes the rest).
            TileData t = Source();
            HeritageRecord r = Record();
            BoudhaFrame f;
            Assert.That(BoudhaSite.TryFrame(r, t, out f), Is.True);
            uint blue = BoudhaKit.EyeBlueDark;
            double h = BoudhaStupa.DomeApex - BoudhaStupa.RingTop;
            for (int lod = 2; lod <= 3; lod++)
            {
                var m = new MeshData();
                Assert.That(HeroBuilder.Build(r, t, lod, m, new GenColliders(), new SacredStats()), Is.GreaterThan(0));
                int[] eyes = new int[4];
                int dome = 0, white = 0;
                for (int i = 0; i < m.VertexCount; i++)
                {
                    double u, v, y = m.Positions[3 * i + 1] - f.Level;
                    f.ToSite(m.Positions[3 * i], m.Positions[3 * i + 2], out u, out v);
                    int cr = m.Colors[4 * i], cg = m.Colors[4 * i + 1], cb = m.Colors[4 * i + 2];
                    if (cr == MeshColor.R(blue) && cg == MeshColor.G(blue) && cb == MeshColor.B(blue) && y > BoudhaStupa.HarmikaBase && y < BoudhaStupa.HarmikaTop + 0.6)
                    {
                        // The face it sits on: the side of the harmika its offset points to.
                        if (Math.Abs(v) > Math.Abs(u)) eyes[v > 0 ? 0 : 2]++;
                        else eyes[u > 0 ? 1 : 3]++;
                    }
                    double rho = Math.Sqrt(u * u + v * v), q = (rho / BoudhaStupa.DomeR) * (rho / BoudhaStupa.DomeR) + (y - BoudhaStupa.RingTop) / h * ((y - BoudhaStupa.RingTop) / h);
                    if (y > BoudhaStupa.RingTop + 0.3 && rho > BoudhaStupa.HarmikaHalf + 0.5 && Math.Abs(q - 1) < 0.004)
                    {
                        dome++;
                        int lo = Math.Min(cr, Math.Min(cg, cb)), hi = Math.Max(cr, Math.Max(cg, cb));
                        if (lo > 200 && hi - lo < 30) white++;
                    }
                }
                for (int face = 0; face < 4; face++) Assert.That(eyes[face], Is.GreaterThanOrEqualTo(4), "LOD" + lod + ": the eyes on face " + face);
                if (lod == 2)
                {
                    Assert.That(dome, Is.GreaterThan(50), "LOD2 dome surface found");
                    Assert.That(white, Is.GreaterThanOrEqualTo(0.6 * dome), "LOD2: the dome is white (" + white + " of " + dome + ")");
                }
            }
        }

        [Test]
        public void EveryHouseStandsInItsSectorsOuterRing()
        {
            // The kora sectors are cut by bearing: each rebuilt house's footprint centroid lies in the outer range of the
            // sector that builds it (so a sector's bounds, and its screen height in the hero allocator, are where its
            // houses are).
            TileData t = Source();
            BoudhaFrame f;
            Assert.That(BoudhaSite.TryFrame(Record(), t, out f), Is.True);
            var wrong = new List<string>();
            foreach (RingHouse house in BoudhaRing.Houses)
            {
                double[] pu, pv;
                int n = BoudhaRing.Footprint(t, f, house.Way, out pu, out pv);
                if (n < 3) continue;
                double cu = 0, cv = 0;
                for (int i = 0; i < n; i++)
                {
                    cu += pu[i];
                    cv += pv[i];
                }
                double brg = BoudhaSite.BearingOf(cu / n, cv / n);
                BoudhaSector sec = BoudhaSite.SectorOf(house.Part);
                if (!BoudhaSector.In(brg, sec.Out0, sec.Out1)) wrong.Add("w" + house.Way + " at " + brg.ToString("0.0", CultureInfo.InvariantCulture) + " is not in " + house.Part);
            }
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
        }

        [Test]
        public void TheFrontRowHousesTheSiteRebuildsAreHidden()
        {
            HeroSet set = HeroSet.From(Curated());
            foreach (ulong w in BoudhaSite.HiddenWays) Assert.That(set.HiddenRefs.Contains(2 * w), Is.True, "w" + w + " hidden");
        }

        [Test]
        public void TheTerracesAndTheNorthStairAreWalkable()
        {
            TileData t = Source();
            HeritageRecord r = Record();
            var gen = new GenColliders();
            HeroBuilder.Build(r, t, 0, new MeshData(), gen);
            double ox, oz;
            BoudhaGround.TryCentre(t, out ox, out oz);
            double level = BoudhaGround.KoraLevel(t, ox, oz);
            int walk = 0;
            bool t1 = false, t2 = false, t3 = false;
            foreach (GenBox b in gen.Boxes)
            {
                if ((b.Flags & GenColliderFlags.Walkable) == 0) continue;
                walk++;
                double top = b.CY + b.HalfY - level;
                if (Math.Abs(top - BoudhaStupa.T1Top) < 0.05) t1 = true;
                if (Math.Abs(top - BoudhaStupa.T2Top) < 0.05) t2 = true;
                if (Math.Abs(top - BoudhaStupa.T3Top) < 0.05) t3 = true;
            }
            Assert.That(t1 && t2 && t3, Is.True, "walkable terrace tops at 4.0, 8.0, 10.5 m");
            Assert.That(gen.Ramps.Count, Is.GreaterThanOrEqualTo(4), "stairs as walkable ramps");
            // A ramp from the compound ground up to T1 (the north compound stair through the west gate pavilion).
            bool up = false;
            foreach (GenRamp q in gen.Ramps)
                if (Math.Abs(Math.Min(q.Y0, q.Y1) - level - BoudhaQuarter.CompoundTop) < 0.05 && Math.Abs(Math.Max(q.Y0, q.Y1) - level - BoudhaStupa.T1Top) < 0.05)
                    up = true;
            Assert.That(up, Is.True, "a stair from the compound ground to the lowest terrace");
            // The kora sectors: the compound wall's runs are solid and the kora slab is walkable at its paving height.
            var kora = new GenColliders();
            foreach (BoudhaPart part in BoudhaSite.AllParts)
                if (BoudhaSite.IsSector(part)) HeroBuilder.Build(PartRecord(part), t, 0, new MeshData(), kora);
            Assert.That(kora.Walls.Count, Is.GreaterThan(18), "the compound wall's runs are solid");
            int slab = 0;
            foreach (GenBox b in kora.Boxes)
                if ((b.Flags & GenColliderFlags.Walkable) != 0 && Math.Abs(b.CY + b.HalfY - level - BoudhaQuarter.PaveTop) < 0.02) slab++;
            Assert.That(slab, Is.GreaterThan(60), "the kora paving is walkable all round");
        }

        // ------------------------------------------------------------------------------------------------- previews

        /// <summary>
        /// The in-situ scene for tools/mesh-preview (GHUMANTE_PREVIEW_DIR): the Boudha tile as the High tier streams it
        /// (terrain, roads, decals, areas, B1 buildings) plus the B0 detail houses within 260 m and the site at LOD 0-3,
        /// each a separate OBJ under <c>boudha/</c>.
        /// </summary>
        [Test]
        public void DumpBoudhaScene()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the scene");
            MeshingSettings s = StreamingScene.Settings(2, 10);
            TileBuild b = High;
            TileData src = b.Source;
            double ox, oz;
            Assert.That(BoudhaGround.TryCentre(src, out ox, out oz), Is.True);
            double level = BoudhaGround.KoraLevel(src, ox, oz);
            float x0 = (float)(ox - 170), z0 = (float)(oz - 170), x1 = (float)(ox + 170), z1 = (float)(oz + 170);
            var layers = new List<(string, MeshData)>
            {
                ("terrain", ObjDump.Crop(StreamingScene.Layer(b, TileLayers.Terrain), x0, z0, x1, z1, true)),
                ("roads", ObjDump.Crop(StreamingScene.Layer(b, TileLayers.Roads), x0, z0, x1, z1, true)),
                ("decals", ObjDump.Crop(StreamingScene.Layer(b, TileLayers.RoadDecals), x0, z0, x1, z1, true)),
                ("areas", ObjDump.Crop(StreamingScene.Layer(b, TileLayers.Areas), x0, z0, x1, z1, true)),
            };
            var band = new MeshData();
            BuildingMesher.Build(src, b.Sampler, s.DetailBuildings, band);
            layers.Add(("buildings", ObjDump.Crop(band, x0, z0, x1, z1, true)));
            var far = new MeshData();
            BuildingMesher.Build(src, b.Sampler, s.Buildings, far);
            layers.Add(("buildings_b1", ObjDump.Crop(ObjDump.Crop(far, x0 - 300, z0 - 300, x1 + 300, z1 + 300, true), x0, z0, x1, z1, false)));
            // The site's parts exactly as the streamed tile carries them (its hero layer, part = hero × 4 + LOD), and their
            // placement for the preview's LOD choice (WorldStreamer: screen height, then the hero slice).
            CultureInfo c = CultureInfo.InvariantCulture;
            var heroes = new StringBuilder("# id cx cz groundY topY tris0 tris1 tris2 tris3 (tile-local = Unity x, z)\n");
            foreach (HeroPiece h in b.Heroes)
            {
                BoudhaPart part;
                if (!BoudhaSite.TryPart(h.Id, out part)) continue;
                for (int lod = 0; lod < 4; lod++)
                {
                    int want = h.Index * 4 + lod;
                    MeshData m = StreamingScene.Layer(b, TileLayers.Heroes, q => q == want);
                    layers.Add(("part_" + part.ToString().ToLowerInvariant() + "_lod" + lod, m));
                    TestContext.WriteLine(part + " LOD" + lod + ": " + m.TriangleCount + " triangles, " + m.VertexCount + " vertices");
                }
                heroes.AppendFormat(c, "{0} {1:0.###} {2:0.###} {3:0.###} {4:0.###} {5} {6} {7} {8}\n", part.ToString().ToLowerInvariant(), h.CX, h.CZ, h.GroundY, h.TopY,
                                    h.Tris0, h.Tris1, h.Tris2, h.Tris3);
            }
            var directives = new[]
            {
                "Boudhanath site, tile " + Tile + " of kathmandu_core (High tier); tile-local metres, absolute height",
                string.Format(c, "centre (Unity x,z) {0:0.###},{1:0.###} kora level {2:0.###}", ox, oz, level),
                "meshpreview: grid=0 sun=160,62",
            };
            foreach (var (name, mesh) in layers)
            {
                if (mesh.TriangleCount == 0) continue;
                ObjDump.Write(mesh, "boudha/" + name + ".obj", directives);
            }
            File.WriteAllText(Path.Combine(ObjDump.Dir, "boudha", "centre.txt"), string.Format(c, "{0:0.###} {1:0.###} {2:0.###}\n", ox, level, oz));
            File.WriteAllText(Path.Combine(ObjDump.Dir, "boudha", "heroes.txt"), heroes.ToString());
        }
    }
}
