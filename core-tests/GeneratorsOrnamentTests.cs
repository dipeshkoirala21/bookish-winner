using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Ornaments;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Roundabout ornaments (W2 detail pass, package ornaments): the catalog of real centrepieces and its JNCT
    /// matching, generic designs (gardens only), islands that follow the road layout, furniture clear of the
    /// centrepiece and of the road corridors, mesh validity (UV0 channels and AO, unit normals, winding), LOD budgets,
    /// determinism, and the preview dumps (GHUMANTE_PREVIEW_DIR) for the visual self-check.</summary>
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

        /// <summary>A fake road corridor index (contract §3: test against a fake until RoadCorridorIndex lands): straight
        /// corridors given by their centreline segments and half widths, game metres.</summary>
        private sealed class FakeCorridors : IRoadCorridorQuery
        {
            private readonly List<double[]> _segs = new List<double[]>();

            public FakeCorridors Add(double ax, double az, double bx, double bz, double half)
            {
                _segs.Add(new[] { ax, az, bx, bz, half });
                return this;
            }

            public int Count
            {
                get { return _segs.Count; }
            }

            public double SignedDistance(double x, double z)
            {
                double best = 1e9;
                foreach (double[] s in _segs)
                {
                    double dx = s[2] - s[0], dz = s[3] - s[1], l2 = dx * dx + dz * dz;
                    double t = l2 > 0 ? Math.Max(0, Math.Min(1, ((x - s[0]) * dx + (z - s[1]) * dz) / l2)) : 0;
                    double px = s[0] + t * dx - x, pz = s[1] + t * dz - z;
                    best = Math.Min(best, Math.Sqrt(px * px + pz * pz) - s[4]);
                }
                return best;
            }

            public bool Overlaps(double[] x, double[] z, int n, out double depthM)
            {
                depthM = 0;
                for (int i = 0; i < n; i++) depthM = Math.Max(depthM, -SignedDistance(x[i], z[i]));
                return depthM > 0;
            }

            /// <summary>The motor roads of a tile as corridors: centreline segments, half the drawn width (never under
            /// the 4.8 m rideable corridor).</summary>
            public static FakeCorridors Of(TileData t)
            {
                var f = new FakeCorridors();
                foreach (RoadRecord r in t.Roads)
                {
                    if (r.RoadClass == RoadClass.Unknown || r.RoadClass >= RoadClass.Pedestrian) continue;
                    double half = 0.5 * Math.Max(RoadClearance.MinCorridorM, r.WidthCm > 0 ? r.WidthCm / 100.0 : Math.Max(1, (int)r.Lanes) * 3.5);
                    for (int k = 0; k + 1 < r.PointCount; k++)
                        f.Add(t.Tile.X0 + r.Points[2 * k] / 100.0, t.Tile.Z0 + r.Points[2 * k + 1] / 100.0, t.Tile.X0 + r.Points[2 * k + 2] / 100.0,
                              t.Tile.Z0 + r.Points[2 * k + 3] / 100.0, half);
                }
                return f;
            }
        }

        private static RoundaboutSite Site(RoundaboutEntry e, float radius, float apron)
        {
            return new RoundaboutSite
            {
                X = 0, Z = 0, RadiusM = radius, ApronM = apron, LayoutIsland = e == null || !e.Standalone,
                RoadLiftM = RoundaboutDecorator.DefaultRoadLiftM, MainArmDeg = 180, TileX0 = 0, TileZ0 = 0,
            };
        }

        /// <summary>Island radius used for a curated entry in the synthetic previews (the JNCT or mapped island).</summary>
        private static float RadiusOf(RoundaboutEntry e)
        {
            switch (e.Id)
            {
                case "maitighar": return 27.4f;
                case "tripureshwor": return 6.41f;
                case "jawalakhel": return 17.9f;
                case "airport": return 19.8f;
                default: return e.IslandRadiusM > 0 ? e.IslandRadiusM : 8f;
            }
        }

        private static MeshData BuildEntry(RoundaboutEntry e, int lod, OrnamentStats stats = null, float radius = 0f)
        {
            var m = new MeshData();
            float r = radius > 0 ? radius : RadiusOf(e);
            RoundaboutDecorator.Build(Site(e, r, e.Standalone ? 0f : 1.0f), e.Design, new SlopeSampler(), m, lod, stats);
            return m;
        }

        /// <summary>A fully furnished garden design of a given centrepiece (generic budget).</summary>
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
            Assert.That(seen, Is.SupersetOf(new[]
            {
                "maitighar", "shahid_gate", "narayan_gopal", "jawalakhel", "tripureshwor", "durbar_marg", "new_road", "pn_shah", "kalimati", "lagankhel",
            }));
        }

        /// <summary>The researched garden and police chowks (roundabouts.md §2 rows 10-11, 17-24, 30) keep their documented
        /// look: planting and the police post only, never a fountain, flag pole, statue or tower nobody mapped there (the
        /// airport approach's "tower" is the airport's control tower, 56 m away and outside the ring).</summary>
        [Test]
        public void ResearchedChowksNeverGetAnInventedLandmark()
        {
            string[] gardens = { "thapathali", "kalanki", "chabahil", "lainchaur", "bagbazar", "satdobato", "sinamangal", "balaju", "koteshwor", "airport" };
            foreach (string id in gardens)
            {
                RoundaboutEntry e;
                Assert.That(RoundaboutCatalog.TryGet(id, out e), Is.True, id);
                Assert.That(e.Design.Centre, Is.EqualTo(Centrepiece.Garden), id);
                Assert.That(e.Design.Fountain, Is.False, id);
                Assert.That(e.Design.FlagPoleM, Is.Zero, id);
                Assert.That(e.Standalone, Is.False, id);
            }
            // In the sample pack, Kalanki's and the airport approach's records take their entries, not a dice roll.
            var seen = new HashSet<string>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                foreach (JunctionRecord j in t.Junctions)
                {
                    RoundaboutDesign d;
                    RoundaboutEntry e;
                    if (!RoundaboutDecorator.TryDesign(j, t, 7f, out d, out e)) continue;
                    Assert.That(d.Centre, Is.Not.EqualTo(Centrepiece.Fountain).And.Not.EqualTo(Centrepiece.FlagPole), j.OsmNodeId.ToString());
                    if (e == null) continue;
                    seen.Add(e.Id);
                    if (Array.IndexOf(gardens, e.Id) >= 0) Assert.That(d.Centre, Is.EqualTo(Centrepiece.Garden), e.Id);
                }
            }
            Assert.That(seen, Is.SupersetOf(new[] { "kalanki", "airport" }));
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
            // kathmandu_core holds Maitighar, Tripureshwor, Durbar Marg, New Road, the airport approach and the chowks.
            Assert.That(matched, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void GenericDesignsAreGardensOnlyAndDeterministic()
        {
            var kinds = new HashSet<Centrepiece>();
            var gardens = new HashSet<GardenStyle>();
            for (long node = 1; node < 400; node++)
            {
                var j = new JunctionRecord { OsmNodeId = node * 7919, Kind = JunctionKind.Roundabout, Flags = (byte)(node % 3 == 0 ? JunctionFlags.HasPolice : 0) };
                foreach (float r in new[] { 0f, 2f, 4f, 7f, 15f, 25f })
                {
                    RoundaboutDesign d = RoundaboutCatalog.Generic(j, r);
                    RoundaboutDesign d2 = RoundaboutCatalog.Generic(j, r);
                    Assert.That(d.Seed, Is.EqualTo(d2.Seed));
                    Assert.That(d.Centre, Is.EqualTo(d2.Centre));
                    Assert.That(d.Garden, Is.EqualTo(d2.Garden));
                    Assert.That(d.Id, Is.EqualTo(RoundaboutCatalog.GenericId));
                    // Never a statue, shrine, monument, fountain, flag pole or tree platform nobody mapped there.
                    Assert.That(d.Centre, Is.EqualTo(Centrepiece.Garden).Or.EqualTo(Centrepiece.PolicePodium), "node " + j.OsmNodeId + " r" + r);
                    Assert.That(d.Fountain, Is.False);
                    Assert.That(d.FlagPoleM, Is.Zero);
                    kinds.Add(d.Centre);
                    gardens.Add(d.Garden);
                }
            }
            Assert.That(kinds, Is.EquivalentTo(new[] { Centrepiece.Garden, Centrepiece.PolicePodium }));
            Assert.That(gardens, Is.SupersetOf(new[] { GardenStyle.Lawn, GardenStyle.Marigold, GardenStyle.Roses, GardenStyle.Mixed }));
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

        // ------------------------------------------------------------------ islands follow the road layout

        [Test]
        public void SitesFollowTheRoadLayoutOrTheCallersRadius()
        {
            // A tile without roads draws no island: the layout's island (radius 0) wins, a police chowk keeps its post.
            TileData t = MeshingChecks.SyntheticTile(new TileId(10, 517, 160), (x, z) => 1300);
            var ring = new JunctionRecord { OsmNodeId = 7, Kind = JunctionKind.Circular, XCm = 40000, ZCm = 50000, RingDiameterCm = 3233, IslandDiameterCm = 1282 };
            RoundaboutSite s;
            Assert.That(RoundaboutDecorator.TrySite(ring, t, 0f, 90f, out s), Is.True);
            Assert.That(s.RadiusM, Is.Zero, "no layout island, no invented one");
            Assert.That(s.X, Is.EqualTo(400.0));
            Assert.That(s.Z, Is.EqualTo(500.0));
            Assert.That(s.MainArmDeg, Is.EqualTo(90f));
            Assert.That(RoundaboutDecorator.Build(ring, t, new TileHeightSampler(t), new MeshData(), 0), Is.Zero, "a ring without island and no police");
            // The caller's own layout: a positive radius is drawn as given (rings get the mountable apron) ...
            Assert.That(RoundaboutDecorator.TrySite(ring, t, 9.5f, 0f, out s), Is.True);
            Assert.That(s.RadiusM, Is.EqualTo(9.5f));
            Assert.That(s.ApronM, Is.EqualTo(1f));
            Assert.That(s.LayoutIsland, Is.False);
            // ... and a negative one means explicitly no island.
            Assert.That(RoundaboutDecorator.TrySite(ring, t, -1f, 0f, out s), Is.True);
            Assert.That(s.RadiusM, Is.Zero);
            var pol = new JunctionRecord { OsmNodeId = 9, Kind = JunctionKind.SyntheticIsland, XCm = 40000, ZCm = 50000, Flags = (byte)JunctionFlags.HasPolice };
            var stats = new OrnamentStats();
            Assert.That(RoundaboutDecorator.Build(pol, t, new TileHeightSampler(t), new MeshData(), 0, 0f, float.NaN, stats), Is.GreaterThan(50));
            Assert.That(stats.RadiusM, Is.Zero, "the post alone");
            Assert.That(stats.Footprints.Count, Is.EqualTo(1));
            Assert.That(stats.Footprints[0].Kind, Is.EqualTo(FootprintKind.PolicePost));
        }

        /// <summary>
        /// Over every level-10 tile of the sample pack: each decorated island has exactly the radius of the island the
        /// road layout draws there (RoadLayout clamps a ring island to the carriageway's inner edge and shrinks or drops
        /// a synthetic one), junctions where the layout has no island get the police post alone or nothing, and in the
        /// whole tile build nothing below the 4.5 m overhead clearance stands outside a layout island, a police post at
        /// its junction or a standalone curated island.
        /// </summary>
        [Test]
        public void SamplePackIslandsFollowTheRoadLayout()
        {
            int islands = 0, posts = 0, tiles = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t.Junctions.Count == 0 || t.Roads.Count == 0) continue;
                var h = new TileHeightSampler(t);
                RoadLayout layout = RoadLayout.For(t);
                var allowedX = new List<double>();
                var allowedZ = new List<double>();
                var allowedR = new List<double>();
                foreach (RoadIsland isl in layout.Islands)
                {
                    if (isl.Kind == IslandKind.Mini) continue;
                    allowedX.Add(isl.X);
                    allowedZ.Add(isl.Z);
                    allowedR.Add(isl.RadiusM + 0.03);
                }
                foreach (JunctionRecord j in t.Junctions)
                {
                    var stats = new OrnamentStats();
                    int tris = RoundaboutDecorator.Build(j, t, h, new MeshData(), 1, 0f, float.NaN, stats);
                    RoadIsland isl;
                    bool has = RoundaboutDecorator.TryLayoutIsland(t, j.XCm / 100.0, j.ZCm / 100.0, out isl);
                    string what = id + " node " + j.OsmNodeId + " " + j.Kind + " (" + stats.DesignId + ")";
                    if (tris == 0) continue;
                    if (has)
                    {
                        Assert.That(stats.RadiusM, Is.EqualTo(isl.RadiusM).Within(1e-4), what);
                        islands++;
                    }
                    else
                    {
                        Assert.That(stats.RadiusM, Is.Zero, what + ": the layout draws no island here");
                        foreach (OrnamentFootprint f in stats.Footprints) Assert.That(f.Kind, Is.EqualTo(FootprintKind.PolicePost), what);
                        allowedX.Add(j.XCm / 100.0);
                        allowedZ.Add(j.ZCm / 100.0);
                        allowedR.Add(1.2); // the podium's umbrella reaches 1.15 m
                        posts++;
                    }
                }
                var list = new List<RoundaboutEntry>();
                RoundaboutCatalog.StandaloneIn(t, list);
                foreach (RoundaboutEntry e in list)
                {
                    RoundaboutSite ss = RoundaboutDecorator.StandaloneSite(e, t);
                    allowedX.Add(ss.X);
                    allowedZ.Add(ss.Z);
                    allowedR.Add(ss.RadiusM + 0.03);
                }
                var m = new MeshData();
                if (RoundaboutDecorator.BuildTile(t, h, m, 0) == 0) continue;
                tiles++;
                CheckMesh(m, id.ToString());
                int outside = 0;
                string first = "";
                for (int v = 0; v < m.VertexCount; v++)
                {
                    double x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                    float g;
                    if (!h.TryHeight(t.Tile.X0 + x, t.Tile.Z0 + z, out g)) continue;
                    if (y - g > RoadClearance.MinOverheadClearanceM + 0.35) continue;
                    bool inside = false;
                    for (int k = 0; k < allowedX.Count && !inside; k++)
                    {
                        double dx = x - allowedX[k], dz = z - allowedZ[k];
                        inside = dx * dx + dz * dz <= allowedR[k] * allowedR[k];
                    }
                    if (inside) continue;
                    if (outside == 0)
                    {
                        int nk = 0;
                        double nd = double.MaxValue;
                        for (int k = 0; k < allowedX.Count; k++)
                        {
                            double dk = Math.Sqrt((x - allowedX[k]) * (x - allowedX[k]) + (z - allowedZ[k]) * (z - allowedZ[k])) - allowedR[k];
                            if (dk < nd)
                            {
                                nd = dk;
                                nk = k;
                            }
                        }
                        first = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                              " first at ({0:F2}, {1:F2}, {2:F2} above ground), {3:F2} m outside the island at ({4:F1}, {5:F1}) r {6:F2}, colour #{7:X2}{8:X2}{9:X2}",
                                              x, z, y - g, nd, allowedX[nk], allowedZ[nk], allowedR[nk], m.Colors[4 * v], m.Colors[4 * v + 1], m.Colors[4 * v + 2]);
                    }
                    outside++;
                }
                Assert.That(outside, Is.Zero, id + ": low geometry outside every island" + first);
            }
            Assert.That(tiles, Is.GreaterThan(3));
            Assert.That(islands, Is.GreaterThan(3));
            TestContext.Progress.WriteLine("layout islands decorated " + islands + ", posts alone " + posts + ", tiles " + tiles);
        }

        // ------------------------------------------------------------------ furniture and corridors

        /// <summary>The police post, lamps, sign, flag pole, trees and beds never stand on the centrepiece (statue
        /// platform, basin, gate, mandala) nor on each other, and stay inside the planting radius, for every catalogue
        /// entry at its own island and on smaller ones (down to 60 %).</summary>
        [Test]
        public void FurnitureKeepsClearOfTheCentrepieceAndEachOther()
        {
            foreach (RoundaboutEntry e in RoundaboutCatalog.Entries)
            {
                foreach (float k in new[] { 1f, 0.8f, 0.6f })
                {
                    float r = RadiusOf(e) * k;
                    float apron = e.Standalone ? 0f : 1.0f;
                    var stats = new OrnamentStats();
                    var m = new MeshData();
                    RoundaboutDecorator.Build(Site(e, r, apron), e.Design, new SlopeSampler(), m, 0, stats);
                    string what = e.Id + " r" + r.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    double inner = Math.Max(0.3, r - Math.Min(apron, 0.3 * r) - 0.3);
                    var centre = new List<OrnamentFootprint>();
                    var items = new List<OrnamentFootprint>();
                    foreach (OrnamentFootprint f in stats.Footprints) (f.Kind == FootprintKind.Centrepiece ? centre : items).Add(f);
                    if (e.Design.Centre != Centrepiece.Garden) Assert.That(centre.Count, Is.GreaterThan(0), what);
                    foreach (OrnamentFootprint f in items)
                    {
                        Assert.That(f.HalfU + f.HalfW, Is.Zero, what + " furniture footprints are discs");
                        foreach (OrnamentFootprint c in centre)
                            Assert.That(c.Distance(f.X, f.Z), Is.GreaterThanOrEqualTo(f.Round - 1e-6), what + ": " + f.Kind + " on the centrepiece");
                        Assert.That(Math.Sqrt(f.X * f.X + f.Z * f.Z) + f.Round, Is.LessThanOrEqualTo(inner + 1e-6), what + ": " + f.Kind + " off the island");
                        foreach (OrnamentFootprint o in items)
                        {
                            if (o.X == f.X && o.Z == f.Z) continue;
                            double d = Math.Sqrt((o.X - f.X) * (o.X - f.X) + (o.Z - f.Z) * (o.Z - f.Z));
                            Assert.That(d, Is.GreaterThanOrEqualTo(o.Round + f.Round - 1e-6), what + ": " + f.Kind + " on " + o.Kind);
                        }
                    }
                    if (k == 1f && e.Design.Police != PoliceStyle.None)
                        Assert.That(items.Exists(f => f.Kind == FootprintKind.PolicePost), Is.True, what + ": the post finds a free spot");
                }
            }
        }

        /// <summary>Tripureshwor (finding: the drum stood in the bottom steps of King Tribhuvan's platform): on the real
        /// 6.41 m island the post stands clear of the 6.4 m square platform, on the lawn by the kerb.</summary>
        [Test]
        public void TripureshworPoliceDrumStandsOffTheStatuePlatform()
        {
            RoundaboutEntry e;
            Assert.That(RoundaboutCatalog.TryGet("tripureshwor", out e), Is.True);
            var stats = new OrnamentStats();
            BuildEntry(e, 0, stats, 6.41f);
            OrnamentFootprint post = stats.Footprints.Find(f => f.Kind == FootprintKind.PolicePost);
            OrnamentFootprint platform = stats.Footprints.Find(f => f.Kind == FootprintKind.Centrepiece);
            Assert.That(post.Round, Is.GreaterThan(0), "a post was placed");
            Assert.That(platform.Distance(post.X, post.Z), Is.GreaterThanOrEqualTo(post.Round));
        }

        [Test]
        public void StandaloneSitesFitBetweenTheRoadCorridors()
        {
            RoundaboutEntry e;
            Assert.That(RoundaboutCatalog.TryGet("pn_shah", out e), Is.True);
            var site = new RoundaboutSite { X = 300, Z = 400, RadiusM = e.IslandRadiusM, RoadLiftM = RoundaboutDecorator.DefaultRoadLiftM, MainArmDeg = 270 };
            // A road whose corridor edge passes 3.6 m from the statue: the 3.1 m island is kept whole.
            var far = new FakeCorridors().Add(306, 300, 306, 500, 2.4);
            var stats = new OrnamentStats();
            var m = new MeshData();
            var o = RoundaboutDecorator.DecorOptions.Default;
            o.Corridors = far;
            Assert.That(RoundaboutDecorator.Build(site, e.Design, new SlopeSampler(), m, 0, stats, o), Is.GreaterThan(1000));
            Assert.That(stats.RadiusM, Is.EqualTo(e.IslandRadiusM));
            // Closer (2.1 m): the island shrinks to the gap less the margin, the statue keeps its pedestal, nothing low
            // stands on the corridor.
            var near = new FakeCorridors().Add(304.5, 300, 304.5, 500, 2.4).Add(200, 393, 400, 393, 2.4);
            o.Corridors = near;
            m = new MeshData();
            Assert.That(RoundaboutDecorator.Build(site, e.Design, new SlopeSampler(), m, 0, stats, o), Is.GreaterThan(500));
            Assert.That(stats.RadiusM, Is.EqualTo(2.1 - RoundaboutDecorator.CorridorMarginM).Within(1e-3));
            Assert.That(stats.Figures, Is.EqualTo(1));
            for (int v = 0; v < m.VertexCount; v++)
            {
                if (m.Positions[3 * v + 1] - 1302 > RoadClearance.MinOverheadClearanceM) continue;
                Assert.That(near.SignedDistance(m.Positions[3 * v], m.Positions[3 * v + 2]), Is.GreaterThan(0), "vertex " + v + " on a lane");
            }
            // No room at all: the site is left out (and counted) until the roads package cuts its island out.
            var over = new FakeCorridors().Add(301, 300, 301, 500, 2.4);
            o.Corridors = over;
            Assert.That(RoundaboutDecorator.Build(site, e.Design, new SlopeSampler(), new MeshData(), 0, stats, o), Is.Zero);
            Assert.That(stats.Skipped, Is.EqualTo(1));
            // A layout island is exempt (the corridor index covers rings and their islands): nothing shrinks, nothing is
            // skipped, because everything stands inside the island the lanes go round.
            RoundaboutEntry jw;
            Assert.That(RoundaboutCatalog.TryGet("jawalakhel", out jw), Is.True);
            var ringSite = new RoundaboutSite { X = 300, Z = 400, RadiusM = 17.9f, ApronM = 1f, LayoutIsland = true, RoadLiftM = 0.27f, MainArmDeg = 0 };
            o.Corridors = new FakeCorridors().Add(250, 400, 350, 400, 30);
            var full = new OrnamentStats();
            RoundaboutDecorator.Build(ringSite, jw.Design, new SlopeSampler(), new MeshData(), 0, full, o);
            Assert.That(full.RadiusM, Is.EqualTo(17.9f));
            Assert.That(full.Skipped, Is.Zero);
            Assert.That(full.LampPosts, Is.GreaterThanOrEqualTo(jw.Design.LampPosts));
        }

        [Test]
        public void SamplePackStandaloneIslandsAreListedForTheRoads()
        {
            int fitted = 0, found = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var entries = new List<RoundaboutEntry>();
                RoundaboutCatalog.StandaloneIn(t, entries);
                if (entries.Count == 0) continue;
                found += entries.Count;
                // The mapped islands, for the roads package to cut out of its junction plates and lane graph.
                var sites = new List<RoundaboutSite>();
                RoundaboutDecorator.StandaloneIslands(t, sites);
                Assert.That(sites.Count, Is.EqualTo(entries.Count));
                for (int i = 0; i < sites.Count; i++)
                {
                    Assert.That(sites[i].RadiusM, Is.EqualTo(entries[i].IslandRadiusM), entries[i].Id);
                    Assert.That(sites[i].LayoutIsland, Is.False, entries[i].Id);
                }
                // Until they do, the build fits each into the gap between the corridors (or leaves it out).
                FakeCorridors corridors = FakeCorridors.Of(t);
                foreach (RoundaboutEntry e in entries)
                {
                    RoundaboutSite s = RoundaboutDecorator.StandaloneSite(e, t);
                    double clear = corridors.SignedDistance(s.TileX0 + s.X, s.TileZ0 + s.Z);
                    bool fits = RoundaboutDecorator.FitToCorridors(ref s, e.Design, corridors);
                    TestContext.Progress.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                                                 "{0}: mapped r {1:F2}, corridor clearance {2:F2}, drawn r {3:F2} ({4})", e.Id, e.IslandRadiusM, clear,
                                                                 s.RadiusM, fits ? "fits" : "left out until the roads cut it out"));
                    if (!fits) continue;
                    fitted++;
                    Assert.That(clear, Is.GreaterThanOrEqualTo(s.RadiusM + RoundaboutDecorator.CorridorMarginM - 1e-6), e.Id);
                }
                var o = RoundaboutDecorator.DecorOptions.Default;
                o.Corridors = corridors;
                var m = new MeshData();
                RoundaboutDecorator.BuildTile(t, new TileHeightSampler(t), m, 1, o);
                if (m.VertexCount > 0) CheckMesh(m, id + " with corridors");
            }
            Assert.That(found, Is.GreaterThanOrEqualTo(3), "Shahid Gate, the Singha Durbar statue and Kalimati lie in kathmandu_core");
            Assert.That(fitted, Is.GreaterThanOrEqualTo(2), "Shahid Gate's park and Kalimati's island are clear of the roads");
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
                    TestContext.Progress.WriteLine(e.Id + " LOD" + lod + ": " + tris + " triangles" + parts);
                }
            }
        }

        [Test]
        public void GenericIslandsBuildValidMeshesWithinBudget()
        {
            var designs = new[]
            {
                GenericGarden(11, Centrepiece.Garden, GardenStyle.Marigold), GenericGarden(12, Centrepiece.Fountain, GardenStyle.Roses),
                GenericGarden(13, Centrepiece.FlagPole, GardenStyle.Mixed), GenericGarden(14, Centrepiece.Garden, GardenStyle.White),
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
        public void ShahidGateHasTheArchLanternAndFiveBustsOnItsRoundIsland()
        {
            RoundaboutEntry e;
            Assert.That(RoundaboutCatalog.TryGet("shahid_gate", out e), Is.True);
            var stats = new OrnamentStats();
            MeshData m = BuildEntry(e, 0, stats);
            Assert.That(stats.Busts, Is.EqualTo(5), "Tribhuvan in the lantern and four martyrs in the pavilions");
            Assert.That(stats.TopM, Is.InRange(14f, 18f), "lantern cap and flag mast at about 16 m");
            Assert.That(stats.RadiusM, Is.EqualTo(15.25f), "the mapped round park island w193705373 (equal-area Ø 30.5 m)");
            float minX, minY, minZ, maxX, maxY, maxZ;
            m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            Assert.That(maxX - minX, Is.InRange(30f, 31.5f), "round island across the gate");
            Assert.That(maxZ - minZ, Is.InRange(30f, 31.5f), "round island along the gate");
            // The pavilions' horn tips stay inside the planting radius, so lamps and planting find room round them.
            Assert.That(stats.Footprints.Find(f => f.Kind == FootprintKind.Centrepiece).Reach(0, 0), Is.LessThan(15.25 - 0.3));
        }

        [Test]
        public void MaitigharMandalaSitsOffCentreWhereTheIslandAllows()
        {
            RoundaboutEntry e;
            Assert.That(RoundaboutCatalog.TryGet("maitighar", out e), Is.True);
            // On the mapped 54.9 m island the mandala stands towards the WSW of the ring centre (OSM w120106732).
            var stats = new OrnamentStats();
            BuildEntry(e, 0, stats, 27.4f);
            OrnamentFootprint mand = stats.Footprints.Find(f => f.Kind == FootprintKind.Centrepiece);
            double off = Math.Sqrt(mand.X * mand.X + mand.Z * mand.Z), bearing = Math.Atan2(mand.X, mand.Z) * 180 / Math.PI + 360;
            Assert.That(off, Is.GreaterThan(5.0), "offset kept");
            Assert.That(bearing % 360, Is.InRange(225.0, 260.0), "WSW");
            foreach (OrnamentFootprint f in stats.Footprints)
                Assert.That(f.Reach(0, 0), Is.LessThanOrEqualTo(27.4 - 1.0 - 0.3 + 1e-6), f.Kind + " inside the planting radius");
            // On the smaller island the road layout draws (the ring ways come within 18.7 m) it moves in to fit.
            var small = new OrnamentStats();
            BuildEntry(e, 0, small, 18.73f);
            foreach (OrnamentFootprint f in small.Footprints)
                Assert.That(f.Reach(0, 0), Is.LessThanOrEqualTo(18.73 - 1.0 - 0.3 + 1e-6), f.Kind + " inside the layout island");
        }

        [Test]
        public void OptionsLeaveTheBaseAndPostToTheRoadsAndCollectColliders()
        {
            RoundaboutEntry e;
            Assert.That(RoundaboutCatalog.TryGet("tripureshwor", out e), Is.True);
            var full = new OrnamentStats();
            var bare = new OrnamentStats();
            RoundaboutDecorator.Build(Site(e, 6.41f, 1f), e.Design, new SlopeSampler(), new MeshData(), 0, full);
            var cols = new Generators.GenColliders();
            var o = new RoundaboutDecorator.DecorOptions { DrawBase = false, DrawPolice = false, Colliders = cols };
            RoundaboutDecorator.Build(Site(e, 6.41f, 1f), e.Design, new SlopeSampler(), new MeshData(), 0, bare, o);
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
            // Shahid Gate: the walkable platform and the two pavilions the arch legs spring from (the opening stays free).
            Assert.That(RoundaboutCatalog.TryGet("shahid_gate", out e), Is.True);
            cols.Clear();
            RoundaboutDecorator.Build(Site(e, e.IslandRadiusM, 0f), e.Design, new SlopeSampler(), new MeshData(), 1, null, o);
            Assert.That(cols.Boxes.Count, Is.EqualTo(3));
        }

        [Test]
        public void StandaloneSitesLandInTheirTiles()
        {
            int found = 0;
            var ids = new List<string>();
            var list = new List<RoundaboutEntry>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                list.Clear();
                RoundaboutCatalog.StandaloneIn(t, list);
                foreach (RoundaboutEntry e in list)
                {
                    found++;
                    ids.Add(e.Id);
                    RoundaboutSite s = RoundaboutDecorator.StandaloneSite(e, t);
                    Assert.That(s.X, Is.InRange(0.0, id.Size), e.Id);
                    Assert.That(s.Z, Is.InRange(0.0, id.Size), e.Id);
                    Assert.That(s.LayoutIsland, Is.False, e.Id);
                    var stats = new OrnamentStats();
                    var m = new MeshData();
                    RoundaboutDecorator.Build(s, e.Design, new TileHeightSampler(t), m, 0, stats);
                    CheckMesh(m, e.Id);
                    Assert.That(stats.IslandTopY, Is.InRange(1250f, 1400f), e.Id + " sits on the terrain");
                }
            }
            Assert.That(ids, Is.SupersetOf(new[] { "shahid_gate", "pn_shah", "kalimati" }), "Shahid Gate, the Singha Durbar statue and Kalimati lie in kathmandu_core");
            TestContext.Progress.WriteLine("standalone sites in the sample pack: " + string.Join(", ", ids));
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

        // ------------------------------------------------------------------ preview dumps

        /// <summary>Asphalt ring and four arms round a preview island (preview only, not part of the generator).</summary>
        private static MeshData RoadContext(float r)
        {
            var m = new MeshData();
            uint asphalt = MeshColor.FromHex(0x4A4D52);
            float ringW = 8.5f, y = 1300.274f;
            int n = 96;
            double outer = r + ringW;
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
                                      ", garden " + stats.GardenTriangles + ", furniture " + stats.FurnitureTriangles + ", skipped " + stats.Skipped + ")");
                    ObjDump.Write("ornaments/" + e.Id + "_lod" + lod + ".obj", new ObjPart("island", m), new ObjPart("road", RoadContext(r)));
                }
            }
            // Maitighar on the island the road layout draws in the sample pack (the ring ways come within 18.7 m).
            {
                RoundaboutEntry e;
                RoundaboutCatalog.TryGet("maitighar", out e);
                MeshData m = BuildEntry(e, 0, null, 18.73f);
                ObjDump.Write("ornaments/maitighar_layout_lod0.obj", new ObjPart("island", m), new ObjPart("road", RoadContext(18.73f)));
            }
            foreach (Centrepiece cp in new[] { Centrepiece.Garden, Centrepiece.Fountain, Centrepiece.FlagPole })
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
            // together with PreviewDumps' scene/tile_<tx>_<ty>/ OBJs, plus the base road mesher's junctions so the
            // islands can be checked against the ring holes.
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                if (!((id.Tx == 517 && id.Ty == 160) || (id.Tx == 517 && id.Ty == 161))) continue;
                TileData t = StreamingSampleRegion.Tile(id);
                var h = new TileHeightSampler(t);
                var m = new MeshData();
                RoundaboutDecorator.BuildTile(t, h, m, 0);
                var roads = new MeshData();
                RoadMesher.Build(t, h, new RoadOptions(), roads);
                var caps = new MeshData();
                JunctionMesher.Build(t, h, new RoadOptions(), caps, null);
                ObjDump.Write("ornaments/tile_" + id.Tx + "_" + id.Ty + ".obj", new ObjPart("island", m), new ObjPart("road", roads), new ObjPart("caps", caps));
            }
            float[] radii = { 7f, 9f, 12f, 14f };
            var gens = new[]
            {
                GenericGarden(101, Centrepiece.Garden, GardenStyle.Marigold, radii[0]), GenericGarden(102, Centrepiece.Garden, GardenStyle.Roses, radii[1]),
                GenericGarden(103, Centrepiece.Garden, GardenStyle.Mixed, radii[2]), GenericGarden(104, Centrepiece.Garden, GardenStyle.White, radii[3]),
            };
            for (int g = 0; g < gens.Length; g++)
            {
                var m = new MeshData();
                RoundaboutDecorator.Build(Site(null, radii[g], 1f), gens[g], new SlopeSampler(), m, 0);
                ObjDump.Write("ornaments/generic_" + gens[g].Garden.ToString().ToLowerInvariant() + ".obj", new ObjPart("island", m), new ObjPart("road", RoadContext(radii[g])));
            }
            // The flag of Nepal close up (the moon is a crescent cradling a rayed disc, the sun a rayed disc).
            {
                var d = GenericGarden(9, Centrepiece.FlagPole, GardenStyle.Lawn, 6f);
                d.Police = PoliceStyle.None;
                d.Signboard = false;
                var m = new MeshData();
                RoundaboutDecorator.Build(Site(null, 6f, 0f), d, new SlopeSampler(), m, 0);
                ObjDump.Write(m, "ornaments/flag.obj");
            }
        }
    }
}
