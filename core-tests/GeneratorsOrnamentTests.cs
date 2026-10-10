using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Ornaments;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Roundabout ornaments (W2 detail pass, package ornaments): the catalog of real centrepieces and its JNCT
    /// matching, generic designs (never a statue), mesh validity (UV0 channels and AO, unit normals, winding), LOD
    /// budgets, determinism, and the preview dumps (GHUMANTE_PREVIEW_DIR) for the visual self-check.</summary>
    public class GeneratorsOrnamentTests
    {
        /// <summary>A gently sloping terrain round 1300 m.</summary>
        private sealed class SlopeSampler : IHeightSampler
        {
            public bool TryHeight(double x, double z, out float h)
            {
                h = (float)(1300.0 + 0.004 * x + 0.002 * z);
                return true;
            }
        }

        private static RoundaboutSite Site(RoundaboutEntry e, float radius, float apron)
        {
            return new RoundaboutSite
            {
                X = 0, Z = 0, RadiusM = radius, ApronM = apron, Shape = e != null ? e.Shape : IslandShape.Round, HalfLengthM = e != null ? e.HalfLengthM : 0,
                RoadLiftM = RoundaboutDecorator.DefaultRoadLiftM, MainArmDeg = 180, TileX0 = 0, TileZ0 = 0,
            };
        }

        /// <summary>Island radius used for a curated entry in the synthetic previews (from the JNCT records).</summary>
        private static float RadiusOf(RoundaboutEntry e)
        {
            switch (e.Id)
            {
                case "maitighar": return 27.4f;
                case "tripureshwor": return 6.4f;
                case "jawalakhel": return 17.9f;
                case "airport": return 19.8f;
                default: return e.IslandRadiusM > 0 ? e.IslandRadiusM : 8f;
            }
        }

        private static MeshData BuildEntry(RoundaboutEntry e, int lod, OrnamentStats stats = null)
        {
            var m = new MeshData();
            float r = RadiusOf(e);
            RoundaboutDecorator.Build(Site(e, r, e.Standalone ? 0f : 1.0f), e.Design, new SlopeSampler(), m, lod, stats);
            return m;
        }

        /// <summary>A fully furnished generic design (as <see cref="RoundaboutCatalog.Generic"/> gives big islands).</summary>
        private static RoundaboutDesign GenericGarden(uint seed, Centrepiece centre, GardenStyle g, float radius = 14f)
        {
            return new RoundaboutDesign
            {
                Id = RoundaboutCatalog.GenericId, Centre = centre, Garden = g, Kerb = KerbPaint.BlackWhite, Railing = RailingStyle.WhiteArches,
                LampPosts = (byte)(radius >= 7f ? 4 : 0), Police = PoliceStyle.Drum, Signboard = true, FacingDeg = float.NaN, Seed = seed,
                Trees = (byte)(radius >= 12.5f ? Math.Min(6, (int)(radius / 3.5f)) : 0), Paths = (byte)(radius >= 12.5f ? 4 : 0),
            };
        }

        // ------------------------------------------------------------------ catalog

        [Test]
        public void CatalogIdsAreUniqueAndEveryEntryHasARealCentrepiece()
        {
            var seen = new HashSet<string>();
            foreach (RoundaboutEntry e in RoundaboutCatalog.Entries)
            {
                Assert.That(seen.Add(e.Id), Is.True, e.Id);
                Assert.That(e.Design.Id, Is.EqualTo(e.Id));
                Assert.That(e.Design.IsHero, Is.True, e.Id);
                Assert.That(e.Lon, Is.InRange(85.2, 85.5), e.Id);
                Assert.That(e.Lat, Is.InRange(27.6, 27.8), e.Id);
                double x, z;
                WorldFrame.LonLatToGame(e.Lon, e.Lat, out x, out z);
                Assert.That(Math.Abs(x - e.GameX) + Math.Abs(z - e.GameZ), Is.LessThan(1e-6), e.Id);
                if (e.Design.Centre == Centrepiece.Statue)
                {
                    Assert.That(e.Design.Statue.Subject, Is.Not.Null.And.Not.Empty, e.Id);
                    Assert.That(e.Design.Statue.FigureM, Is.InRange(2.0f, 3.5f), e.Id);
                }
                if (e.Standalone) Assert.That(e.IslandRadiusM, Is.GreaterThan(2f), e.Id);
            }
            Assert.That(seen, Is.SupersetOf(new[] { "maitighar", "shahid_gate", "narayan_gopal", "jawalakhel", "tripureshwor", "durbar_marg", "new_road" }));
        }

        [Test]
        public void SamplePackJunctionsMatchTheirCatalogEntriesByNodeAndPosition()
        {
            int matched = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                foreach (JunctionRecord j in t.Junctions)
                {
                    RoundaboutEntry e;
                    if (!RoundaboutCatalog.TryMatch(j, t, out e)) continue;
                    matched++;
                    double gx, gz;
                    t.LocalToGame(j.XCm, j.ZCm, out gx, out gz);
                    double d = Math.Sqrt((gx - e.GameX) * (gx - e.GameX) + (gz - e.GameZ) * (gz - e.GameZ));
                    Assert.That(d, Is.LessThan(RoundaboutCatalog.MatchRadiusM), e.Id + " " + j.OsmNodeId);
                    if (e.OsmNodeId != 0) Assert.That(j.OsmNodeId, Is.EqualTo(e.OsmNodeId), e.Id);
                    // The match by position alone (another node id, the record alone in its tile) finds the same entry.
                    JunctionRecord anon = j;
                    anon.OsmNodeId = 1;
                    var lone = new TileData { Tile = t.Tile };
                    lone.Names.AddRange(t.Names);
                    lone.Junctions.Add(anon);
                    RoundaboutEntry e2;
                    Assert.That(RoundaboutCatalog.TryMatch(anon, lone, out e2), Is.True, e.Id);
                    Assert.That(e2.Id, Is.EqualTo(e.Id));
                    // Signals next to a ring never take its centrepiece.
                    JunctionRecord sig = anon;
                    sig.Kind = JunctionKind.Signals;
                    Assert.That(RoundaboutCatalog.TryMatch(sig, lone, out e2), Is.False, e.Id);
                }
            }
            // kathmandu_core holds Maitighar, Tripureshwor, Durbar Marg, New Road and the airport approach.
            Assert.That(matched, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void GenericDesignsNeverInventAStatueAndAreDeterministic()
        {
            var kinds = new HashSet<Centrepiece>();
            for (long node = 1; node < 400; node++)
            {
                var j = new JunctionRecord { OsmNodeId = node * 7919, Kind = JunctionKind.Roundabout, Flags = (byte)(node % 3 == 0 ? JunctionFlags.HasPolice : 0) };
                foreach (float r in new[] { 0f, 2f, 4f, 7f, 15f })
                {
                    RoundaboutDesign d = RoundaboutCatalog.Generic(j, r);
                    RoundaboutDesign d2 = RoundaboutCatalog.Generic(j, r);
                    Assert.That(d.Seed, Is.EqualTo(d2.Seed));
                    Assert.That(d.Centre, Is.EqualTo(d2.Centre));
                    Assert.That(d.Id, Is.EqualTo(RoundaboutCatalog.GenericId));
                    Assert.That(d.Centre, Is.Not.EqualTo(Centrepiece.Statue).And.Not.EqualTo(Centrepiece.EquestrianStatue)
                                              .And.Not.EqualTo(Centrepiece.MemorialArch).And.Not.EqualTo(Centrepiece.Mandala));
                    kinds.Add(d.Centre);
                }
            }
            Assert.That(kinds, Is.SupersetOf(new[] { Centrepiece.Garden, Centrepiece.Fountain, Centrepiece.FlagPole, Centrepiece.ClockTower }));
        }

        [Test]
        public void PlainSignalAndMiniJunctionsGetNoDecoration()
        {
            var m = new MeshData();
            TileData t = MeshingChecks.SyntheticTile(new TileId(10, 517, 160), (x, z) => 1300);
            foreach (JunctionKind k in new[] { JunctionKind.Plain, JunctionKind.Signals, JunctionKind.MiniRoundabout })
            {
                var j = new JunctionRecord { OsmNodeId = 42, Kind = k, XCm = 50000, ZCm = 50000, RingDiameterCm = 2000 };
                Assert.That(RoundaboutDecorator.Build(j, t, new TileHeightSampler(t), m, 0), Is.Zero, k.ToString());
            }
            var police = new JunctionRecord { OsmNodeId = 43, Kind = JunctionKind.Police, Flags = (byte)JunctionFlags.HasPolice, XCm = 50000, ZCm = 50000 };
            Assert.That(RoundaboutDecorator.Build(police, t, new TileHeightSampler(t), m, 0), Is.GreaterThan(50), "police post alone");
        }

        // ------------------------------------------------------------------ meshes

        private static void CheckMesh(MeshData m, string what)
        {
            Assert.That(m.VertexCount, Is.GreaterThan(0), what);
            Assert.That(m.HasUv0, Is.True, what + ": UV0 (channel, AO)");
            for (int v = 0; v < m.VertexCount; v++)
            {
                float nx = m.Normals[3 * v], ny = m.Normals[3 * v + 1], nz = m.Normals[3 * v + 2];
                float l = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                Assert.That(l, Is.InRange(0.98f, 1.02f), what + " normal " + v);
                Assert.That(float.IsNaN(m.Positions[3 * v]) || float.IsNaN(m.Positions[3 * v + 1]) || float.IsNaN(m.Positions[3 * v + 2]), Is.False, what);
                float ch = m.Uv0[2 * v], ao = m.Uv0[2 * v + 1];
                Assert.That(ch, Is.InRange(0f, 25f), what + " channel");
                Assert.That(ch, Is.EqualTo((float)Math.Round(ch)), what + " channel integer");
                Assert.That(ao, Is.InRange(0f, 1f), what + " AO");
            }
            // Winding agrees with the vertex normals on nearly every triangle.
            int bad = 0, tris = m.IndexCount / 3;
            string badWhere = "";
            for (int t = 0; t < tris; t++)
            {
                int a = m.Indices[3 * t], b = m.Indices[3 * t + 1], c = m.Indices[3 * t + 2];
                double e1x = m.Positions[3 * b] - m.Positions[3 * a], e1y = m.Positions[3 * b + 1] - m.Positions[3 * a + 1], e1z = m.Positions[3 * b + 2] - m.Positions[3 * a + 2];
                double e2x = m.Positions[3 * c] - m.Positions[3 * a], e2y = m.Positions[3 * c + 1] - m.Positions[3 * a + 1], e2z = m.Positions[3 * c + 2] - m.Positions[3 * a + 2];
                double fx = e1y * e2z - e1z * e2y, fy = e1z * e2x - e1x * e2z, fz = e1x * e2y - e1y * e2x;
                double sx = m.Normals[3 * a] + m.Normals[3 * b] + m.Normals[3 * c], sy = m.Normals[3 * a + 1] + m.Normals[3 * b + 1] + m.Normals[3 * c + 1],
                       sz = m.Normals[3 * a + 2] + m.Normals[3 * b + 2] + m.Normals[3 * c + 2];
                if (fx * sx + fy * sy + fz * sz < -1e-12)
                {
                    if (bad < 3)
                        badWhere += string.Format(System.Globalization.CultureInfo.InvariantCulture, " [{0:F2},{1:F2},{2:F2} ch {3} #{4:X2}{5:X2}{6:X2}]",
                                                  m.Positions[3 * a], m.Positions[3 * a + 1], m.Positions[3 * a + 2], m.Uv0[2 * a], m.Colors[4 * a],
                                                  m.Colors[4 * a + 1], m.Colors[4 * a + 2]);
                    bad++;
                }
            }
            Assert.That(bad, Is.LessThanOrEqualTo(tris / 1000), what + ": triangles wound against their normals" + badWhere);
        }

        [Test]
        public void EveryCatalogDesignBuildsValidMeshesWithinBudgetAtEveryLod()
        {
            foreach (RoundaboutEntry e in RoundaboutCatalog.Entries)
            {
                int prev = int.MaxValue;
                for (int lod = 0; lod <= 3; lod++)
                {
                    var stats = new OrnamentStats();
                    MeshData m = BuildEntry(e, lod, stats);
                    CheckMesh(m, e.Id + " LOD" + lod);
                    int tris = m.IndexCount / 3;
                    Assert.That(tris, Is.EqualTo(stats.Triangles), e.Id);
                    string parts = " (base " + stats.BaseTriangles + ", centre " + stats.CentreTriangles + ", garden " + stats.GardenTriangles + ", furniture " +
                                   stats.FurnitureTriangles + ")";
                    Assert.That(tris, Is.LessThanOrEqualTo(RoundaboutDecorator.HeroBudget[lod]), e.Id + " LOD" + lod + " budget" + parts);
                    Assert.That(tris, Is.LessThan(prev), e.Id + " LOD" + lod + " fewer triangles than the LOD above");
                    prev = tris;
                    TestContext.Progress.WriteLine(e.Id + " LOD" + lod + ": " + tris + " triangles");
                }
            }
        }

        [Test]
        public void GenericIslandsBuildValidMeshesWithinBudget()
        {
            var designs = new[]
            {
                GenericGarden(11, Centrepiece.Garden, GardenStyle.Marigold), GenericGarden(12, Centrepiece.Fountain, GardenStyle.Roses),
                GenericGarden(13, Centrepiece.FlagPole, GardenStyle.Mixed), GenericGarden(14, Centrepiece.ClockTower, GardenStyle.White),
                GenericGarden(15, Centrepiece.Garden, GardenStyle.Lawn),
            };
            foreach (RoundaboutDesign d0 in designs)
            {
                foreach (float r in new[] { 4f, 8f, 14f })
                {
                    RoundaboutDesign d = GenericGarden(d0.Seed, d0.Centre, d0.Garden, r);
                    int prev = int.MaxValue;
                    for (int lod = 0; lod <= 3; lod++)
                    {
                        var m = new MeshData();
                        int tris = RoundaboutDecorator.Build(Site(null, r, 1f), d, new SlopeSampler(), m, lod);
                        CheckMesh(m, d.Centre + " r" + r + " LOD" + lod);
                        Assert.That(tris, Is.LessThanOrEqualTo(RoundaboutDecorator.GenericBudget[lod]), d.Centre + " " + d.Garden + " r" + r + " LOD" + lod);
                        Assert.That(tris, Is.LessThan(prev));
                        prev = tris;
                    }
                }
            }
        }

        [Test]
        public void RealGenericDesignsStayWithinBudget()
        {
            for (long node = 1; node < 60; node++)
            {
                var j = new JunctionRecord { OsmNodeId = node * 104729, Kind = node % 2 == 0 ? JunctionKind.Roundabout : JunctionKind.SyntheticIsland,
                                             Flags = (byte)JunctionFlags.HasPolice };
                foreach (float r in new[] { 0f, 2.5f, 4.5f, 7f, 10f, 16f, 20f })
                {
                    RoundaboutDesign d = RoundaboutCatalog.Generic(j, r);
                    for (int lod = 0; lod <= 3; lod++)
                    {
                        var m = new MeshData();
                        var st = new OrnamentStats();
                        int tris = RoundaboutDecorator.Build(Site(null, r, r > 6 ? 1f : 0f), d, new SlopeSampler(), m, lod, st);
                        Assert.That(tris, Is.LessThanOrEqualTo(RoundaboutDecorator.GenericBudget[lod]),
                                    d.Centre + " " + d.Garden + " r" + r + " LOD" + lod + " (base " + st.BaseTriangles + ", centre " + st.CentreTriangles +
                                    ", garden " + st.GardenTriangles + ", furniture " + st.FurnitureTriangles + ")");
                    }
                }
            }
        }

        [Test]
        public void EveryPoseAttireAndPlinthBuilds()
        {
            foreach (StatuePose pose in (StatuePose[])Enum.GetValues(typeof(StatuePose)))
                foreach (StatueAttire attire in (StatueAttire[])Enum.GetValues(typeof(StatueAttire)))
                {
                    var d = new RoundaboutDesign
                    {
                        Id = "test", Centre = pose == StatuePose.Rider ? Centrepiece.EquestrianStatue : Centrepiece.Statue, Garden = GardenStyle.Lawn,
                        FacingDeg = 30f, Seed = 5,
                        Statue = new StatueSpec
                        {
                            Subject = "test", Pose = pose, Attire = attire, Finish = (StatueFinish)((int)pose % 5), FigureM = 2.6f, Cloak = ((int)attire & 1) == 0,
                            Glasses = true, Garland = true, Plinth = (PlinthShape)((int)pose % 4), PlinthM = 2.5f, PlinthW = 1.4f,
                            Platform = (PlatformShape)((int)attire % 2), Steps = 2, PlatformW = 4f, Pots = true,
                        },
                    };
                    for (int lod = 0; lod <= 3; lod += 3)
                    {
                        var m = new MeshData();
                        var st = new OrnamentStats();
                        RoundaboutDecorator.Build(Site(null, 6f, 0f), d, new SlopeSampler(), m, lod, st);
                        CheckMesh(m, pose + " " + attire + " LOD" + lod);
                        Assert.That(st.Figures, Is.EqualTo(1));
                        Assert.That(st.TopM, Is.GreaterThan(4.5f), pose + " " + attire);
                    }
                }
        }

        [Test]
        public void BuildIsDeterministic()
        {
            RoundaboutEntry e;
            Assert.That(RoundaboutCatalog.TryGet("jawalakhel", out e), Is.True);
            MeshData a = BuildEntry(e, 0), b = BuildEntry(e, 0);
            Assert.That(a.VertexCount, Is.EqualTo(b.VertexCount));
            Assert.That(a.IndexCount, Is.EqualTo(b.IndexCount));
            for (int i = 0; i < a.VertexCount * 3; i++) Assert.That(a.Positions[i], Is.EqualTo(b.Positions[i]));
            for (int i = 0; i < a.VertexCount * 4; i++) Assert.That(a.Colors[i], Is.EqualTo(b.Colors[i]));
        }

        [Test]
        public void StatuesStandOnTheirPlinthsAboveTheIsland()
        {
            foreach (RoundaboutEntry e in RoundaboutCatalog.Entries)
            {
                if (e.Design.Centre != Centrepiece.Statue) continue;
                var stats = new OrnamentStats();
                MeshData m = BuildEntry(e, 0, stats);
                Assert.That(stats.Figures, Is.EqualTo(1), e.Id);
                StatueSpec s = e.Design.Statue;
                // The crown sits above platform + pedestal + figure height.
                float expected = s.PlinthM + s.FigureM;
                Assert.That(stats.TopM, Is.GreaterThan(expected * 0.95f), e.Id);
                float minX, minY, minZ, maxX, maxY, maxZ;
                m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
                Assert.That(maxY - stats.IslandTopY, Is.GreaterThan(expected * 0.95f), e.Id + " mesh height");
                // Nothing outside the island (lamps lean in, flags fly inside the kerb ring).
                float r = RadiusOf(e) + 1.2f;
                Assert.That(Math.Max(Math.Max(-minX, maxX), Math.Max(-minZ, maxZ)), Is.LessThan(r), e.Id + " footprint");
            }
        }

        [Test]
        public void ShahidGateHasTheArchLanternAndFiveBusts()
        {
            RoundaboutEntry e;
            Assert.That(RoundaboutCatalog.TryGet("shahid_gate", out e), Is.True);
            var stats = new OrnamentStats();
            MeshData m = BuildEntry(e, 0, stats);
            Assert.That(stats.Busts, Is.EqualTo(5), "Tribhuvan in the lantern and four martyrs in the pavilions");
            Assert.That(stats.TopM, Is.InRange(14f, 18f), "lantern cap and flag mast at about 16 m");
            float minX, minY, minZ, maxX, maxY, maxZ;
            m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            Assert.That(maxX - minX, Is.InRange(30f, 35f), "stadium island across the gate");
        }

        [Test]
        public void TileBuildDecoratesEverySamplePackIsland()
        {
            int tiles = 0, total = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t.Junctions.Count == 0) continue;
                var m = new MeshData();
                int tris = RoundaboutDecorator.BuildTile(t, new TileHeightSampler(t), m, 1);
                if (tris == 0) continue;
                tiles++;
                total += tris;
                CheckMesh(m, id.ToString());
                float minX, minY, minZ, maxX, maxY, maxZ;
                m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
                Assert.That(minX, Is.GreaterThan(-40f), id.ToString());
                Assert.That(maxX, Is.LessThan((float)id.Size + 40f), id.ToString());
                Assert.That(minY, Is.GreaterThan(1200f), id.ToString());
                Assert.That(maxY, Is.LessThan(1450f), id.ToString());
            }
            Assert.That(tiles, Is.GreaterThan(3));
            TestContext.Progress.WriteLine("decorated tiles " + tiles + ", triangles " + total);
        }

        [Test]
        public void SitesFollowTheJunctionRecords()
        {
            TileData t = MeshingChecks.SyntheticTile(new TileId(10, 517, 160), (x, z) => 1300);
            // A ring with a mapped island: the island diameter wins; rings get the mountable apron.
            var ring = new JunctionRecord { OsmNodeId = 7, Kind = JunctionKind.Circular, XCm = 40000, ZCm = 50000, RingDiameterCm = 3233, IslandDiameterCm = 1282 };
            RoundaboutSite s;
            Assert.That(RoundaboutDecorator.TrySite(ring, t, 0f, 90f, out s), Is.True);
            Assert.That(s.RadiusM, Is.EqualTo(6.41f).Within(1e-3));
            Assert.That(s.ApronM, Is.EqualTo(1f));
            Assert.That(s.X, Is.EqualTo(400.0));
            Assert.That(s.Z, Is.EqualTo(500.0));
            Assert.That(s.MainArmDeg, Is.EqualTo(90f));
            // No island: ring diameter minus the ring carriageway.
            ring.IslandDiameterCm = 0;
            Assert.That(RoundaboutDecorator.TrySite(ring, t, 0f, 0f, out s), Is.True);
            Assert.That(s.RadiusM, Is.EqualTo(32.33f / 2 - RoundaboutDecorator.DefaultRingWidthM).Within(1e-3));
            // The road layout's island radius overrides everything.
            Assert.That(RoundaboutDecorator.TrySite(ring, t, 9.5f, 0f, out s), Is.True);
            Assert.That(s.RadiusM, Is.EqualTo(9.5f));
            // A synthetic island: 0.7 x the widest arm, clamped to 6-16 m; no apron.
            var syn = new JunctionRecord { OsmNodeId = 8, Kind = JunctionKind.SyntheticIsland, XCm = 40000, ZCm = 50000, Flags = (byte)JunctionFlags.HasPolice };
            Assert.That(RoundaboutDecorator.TrySite(syn, t, 0f, 0f, out s), Is.True);
            Assert.That(s.RadiusM, Is.InRange(3f, 8f));
            Assert.That(s.ApronM, Is.Zero);
            // A police chowk without an island: the post alone.
            var pol = new JunctionRecord { OsmNodeId = 9, Kind = JunctionKind.Police, XCm = 40000, ZCm = 50000, Flags = (byte)JunctionFlags.HasPolice };
            Assert.That(RoundaboutDecorator.TrySite(pol, t, 0f, 0f, out s), Is.True);
            Assert.That(s.RadiusM, Is.Zero);
        }

        [Test]
        public void MainArmPointsAlongTheWidestRoadLeavingTheJunction()
        {
            TileData t = MeshingChecks.SyntheticTile(new TileId(10, 517, 160), (x, z) => 1300);
            // A primary road leaving east and a residential lane leaving north.
            t.Roads.Add(new RoadRecord { OsmWayId = 1, RoadClass = RoadClass.Primary, WidthCm = 1200, Points = new[] { 50000, 50000, 56000, 50000 } });
            t.Roads.Add(new RoadRecord { OsmWayId = 2, RoadClass = RoadClass.Residential, WidthCm = 500, Points = new[] { 50000, 50000, 50000, 56000 } });
            float b = RoundaboutDecorator.MainArmBearing(t, 500, 500, 8);
            Assert.That(b, Is.EqualTo(90f).Within(0.5f));
            Assert.That(RoundaboutDecorator.WidestArm(t, 500, 500, 10), Is.EqualTo(12f).Within(1e-3));
        }

        [Test]
        public void OptionsLeaveTheBaseAndPostToTheRoadsAndCollectColliders()
        {
            RoundaboutEntry e;
            Assert.That(RoundaboutCatalog.TryGet("tripureshwor", out e), Is.True);
            var full = new OrnamentStats();
            var bare = new OrnamentStats();
            RoundaboutDecorator.Build(Site(e, 6.4f, 1f), e.Design, new SlopeSampler(), new MeshData(), 0, full);
            var cols = new Generators.GenColliders();
            var o = new RoundaboutDecorator.DecorOptions { DrawBase = false, DrawPolice = false, Colliders = cols };
            RoundaboutDecorator.Build(Site(e, 6.4f, 1f), e.Design, new SlopeSampler(), new MeshData(), 0, bare, o);
            Assert.That(full.BaseTriangles, Is.GreaterThan(200));
            Assert.That(bare.BaseTriangles, Is.Zero);
            Assert.That(bare.Triangles, Is.LessThan(full.Triangles));
            Assert.That(cols.Boxes.Count, Is.GreaterThanOrEqualTo(2), "platform and pedestal");
            foreach (Generators.GenBox b in cols.Boxes)
            {
                Assert.That(b.HalfX, Is.GreaterThan(0f));
                Assert.That(b.HalfY, Is.GreaterThan(0f));
                Assert.That(Math.Abs(b.CX), Is.LessThan(7.0));
            }
            // Shahid Gate: platform, pavilions and arch feet.
            Assert.That(RoundaboutCatalog.TryGet("shahid_gate", out e), Is.True);
            cols.Clear();
            RoundaboutDecorator.Build(Site(e, e.IslandRadiusM, 0f), e.Design, new SlopeSampler(), new MeshData(), 1, null, o);
            Assert.That(cols.Boxes.Count, Is.EqualTo(5));
        }

        [Test]
        public void StandaloneSitesLandInTheirTiles()
        {
            int found = 0;
            var list = new List<RoundaboutEntry>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                list.Clear();
                RoundaboutCatalog.StandaloneIn(t, list);
                foreach (RoundaboutEntry e in list)
                {
                    found++;
                    RoundaboutSite s = RoundaboutDecorator.StandaloneSite(e, t);
                    Assert.That(s.X, Is.InRange(0.0, id.Size), e.Id);
                    Assert.That(s.Z, Is.InRange(0.0, id.Size), e.Id);
                    var stats = new OrnamentStats();
                    var m = new MeshData();
                    RoundaboutDecorator.Build(s, e.Design, new TileHeightSampler(t), m, 0, stats);
                    CheckMesh(m, e.Id);
                    Assert.That(stats.IslandTopY, Is.InRange(1250f, 1400f), e.Id + " sits on the terrain");
                }
            }
            Assert.That(found, Is.EqualTo(2), "Shahid Gate and the Singha Durbar statue lie in kathmandu_core");
        }

        // ------------------------------------------------------------------ preview dumps

        /// <summary>Asphalt ring and four arms round a preview island (preview only, not part of the generator).</summary>
        private static MeshData RoadContext(RoundaboutEntry e, float r)
        {
            var m = new MeshData();
            uint asphalt = MeshColor.FromHex(0x4A4D52);
            float ringW = 8.5f, y = 1300.274f;
            int n = 96;
            double outer = r + ringW;
            if (e != null && e.Shape == IslandShape.Stadium) outer = e.HalfLengthM + ringW;
            for (int k = 0; k <= n; k++)
            {
                double a = 2 * Math.PI * k / n;
                double sa = Math.Sin(a), ca = Math.Cos(a);
                m.AddVertex((float)(r * 0.98 * sa), y + (float)(0.004 * r * 0.98 * sa + 0.002 * r * 0.98 * ca), (float)(r * 0.98 * ca), 0, 1, 0, asphalt, 10f, 1f);
                m.AddVertex((float)(outer * sa), y + (float)(0.004 * outer * sa + 0.002 * outer * ca), (float)(outer * ca), 0, 1, 0, asphalt, 10f, 1f);
            }
            for (int k = 0; k < n; k++)
            {
                int a0 = 2 * k;
                m.AddTriangle(a0, a0 + 1, a0 + 3);
                m.AddTriangle(a0, a0 + 3, a0 + 2);
            }
            for (int arm = 0; arm < 4; arm++)
            {
                double a = Math.PI / 2 * arm, sa = Math.Sin(a), ca = Math.Cos(a), px = ca, pz = -sa;
                int v = m.VertexCount;
                double l0 = outer - 1, l1 = outer + 30, hw = 6.5;
                foreach (double l in new[] { l0, l1 })
                    foreach (int s in new[] { -1, 1 })
                    {
                        double x = l * sa + s * hw * px, z = l * ca + s * hw * pz;
                        m.AddVertex((float)x, y + (float)(0.004 * x + 0.002 * z) - 0.002f, (float)z, 0, 1, 0, asphalt, 10f, 1f);
                    }
                m.AddTriangle(v, v + 1, v + 3);
                m.AddTriangle(v, v + 3, v + 2);
            }
            // Fix winding to face up.
            for (int t = 0; t < m.IndexCount; t += 3)
            {
                int a = m.Indices[t], b = m.Indices[t + 1], c = m.Indices[t + 2];
                double e1x = m.Positions[3 * b] - m.Positions[3 * a], e1z = m.Positions[3 * b + 2] - m.Positions[3 * a + 2];
                double e2x = m.Positions[3 * c] - m.Positions[3 * a], e2z = m.Positions[3 * c + 2] - m.Positions[3 * a + 2];
                if (e1z * e2x - e1x * e2z < 0)
                {
                    m.Indices[t + 1] = c;
                    m.Indices[t + 2] = b;
                }
            }
            return m;
        }

        [Test]
        public void DumpPreviews()
        {
            if (!ObjDump.Enabled) Assert.Ignore("set GHUMANTE_PREVIEW_DIR to dump previews");
            var report = new System.Text.StringBuilder();
            foreach (RoundaboutEntry e in RoundaboutCatalog.Entries)
            {
                float r = RadiusOf(e);
                for (int lod = 0; lod <= 3; lod++)
                {
                    var stats = new OrnamentStats();
                    MeshData m = BuildEntry(e, lod, stats);
                    report.AppendLine(e.Id + " LOD" + lod + ": " + stats.Triangles + " (base " + stats.BaseTriangles + ", centre " + stats.CentreTriangles +
                                      ", garden " + stats.GardenTriangles + ", furniture " + stats.FurnitureTriangles + ")");
                    ObjDump.Write("ornaments/" + e.Id + "_lod" + lod + ".obj", new ObjPart("island", m), new ObjPart("road", RoadContext(e, r)));
                }
            }
            foreach (Centrepiece cp in new[] { Centrepiece.Garden, Centrepiece.Fountain, Centrepiece.FlagPole, Centrepiece.ClockTower })
                foreach (GardenStyle g in new[] { GardenStyle.Marigold, GardenStyle.Roses, GardenStyle.Mixed, GardenStyle.White, GardenStyle.Lawn })
                    foreach (float r in new[] { 8f, 16f })
                    {
                        var stats = new OrnamentStats();
                        RoundaboutDecorator.Build(Site(null, r, 1f), GenericGarden(7, cp, g, r), new SlopeSampler(), new MeshData(), 0, stats);
                        report.AppendLine("generic " + cp + " " + g + " r" + r + ": " + stats.Triangles + " (base " + stats.BaseTriangles + ", centre " +
                                          stats.CentreTriangles + ", garden " + stats.GardenTriangles + ", furniture " + stats.FurnitureTriangles + ")");
                    }
            System.IO.File.WriteAllText(System.IO.Path.Combine(ObjDump.Dir, "ornaments", "budgets.txt"), report.ToString());
            // Whole sample tiles (Tripureshwor and Maitighar; Durbar Marg, New Road and Shahid Gate) for scene renders
            // together with PreviewDumps' scene/tile_<tx>_<ty>/ OBJs.
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                if (!((id.Tx == 517 && id.Ty == 160) || (id.Tx == 517 && id.Ty == 161))) continue;
                TileData t = StreamingSampleRegion.Tile(id);
                var m = new MeshData();
                RoundaboutDecorator.BuildTile(t, new TileHeightSampler(t), m, 0);
                ObjDump.Write(m, "ornaments/tile_" + id.Tx + "_" + id.Ty + ".obj");
            }
            float[] radii = { 7f, 9f, 12f, 14f };
            var gens = new[]
            {
                GenericGarden(101, Centrepiece.Garden, GardenStyle.Marigold, radii[0]), GenericGarden(102, Centrepiece.Fountain, GardenStyle.Roses, radii[1]),
                GenericGarden(103, Centrepiece.FlagPole, GardenStyle.Mixed, radii[2]), GenericGarden(104, Centrepiece.ClockTower, GardenStyle.White, radii[3]),
            };
            for (int g = 0; g < gens.Length; g++)
            {
                var m = new MeshData();
                RoundaboutDecorator.Build(Site(null, radii[g], 1f), gens[g], new SlopeSampler(), m, 0);
                ObjDump.Write("ornaments/generic_" + gens[g].Centre.ToString().ToLowerInvariant() + ".obj", new ObjPart("island", m), new ObjPart("road", RoadContext(null, radii[g])));
            }
        }
    }
}
