using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Streaming;
using Ghumante.World.Instancing;
using Ghumante.World.Sacred;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Builds tiles of the kathmandu_core sample exactly as the game's streamer does (<see cref="TileBuild.Execute"/> with
    /// <see cref="MeshingSettings.ForTier"/>, the layered <see cref="TileGroundQuery"/> with the bridge deck and corridor
    /// factories) and reassembles the uploaded chunks, so the integration tests check what would be on screen.
    /// </summary>
    internal static class StreamingScene
    {
        private static CuratedDb _db;
        private static HeroSet _heroes;
        private static readonly object Lock = new object();

        public static HeroSet Heroes
        {
            get
            {
                lock (Lock)
                {
                    if (_heroes == null)
                    {
                        _db = CuratedDb.Read(File.ReadAllBytes(Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core",
                                                                            "kathmandu_core.curated.ghcd")));
                        _heroes = HeroSet.From(_db);
                    }
                    return _heroes;
                }
            }
        }

        /// <summary>The level-10 tile holding a place (WGS84).</summary>
        public static TileId TileAt(double lon, double lat)
        {
            double x, z;
            WorldFrame.LonLatToGame(lon, lat, out x, out z);
            return TileId.At(10, x, z);
        }

        /// <summary>The game's settings for a tier and month (the world's <c>MeshingSettings.ForTier</c>).</summary>
        public static MeshingSettings Settings(int tier, int month, bool heroes = true)
        {
            return MeshingSettings.ForTier(tier, month, heroes ? Heroes : null);
        }

        /// <summary>The ground query the world makes for <paramref name="settings"/>.</summary>
        public static TileGroundQuery Ground(MeshingSettings settings)
        {
            return new TileGroundQuery(settings.Roads)
            {
                DeckFactory = BridgeDeckIndex.ForTile, CorridorFactory = settings.Corridors,
                HiddenBuildingRefs = settings.Heroes != null ? settings.Heroes.HiddenRefs : null,
            };
        }

        /// <summary>One exact detail node built on this thread as a streaming worker would (a fresh decode of the tile).</summary>
        public static TileBuild Build(TileId id, MeshingSettings settings, TileGroundQuery ground = null, int tier = -1)
        {
            var b = new TileBuild { Node = new SelectedNode(id, id) };
            StreamingConfig config = StreamingConfig.ForTier(tier >= 0 ? tier : settings.Tier);
            TileBuild.Execute(b, StreamingSampleRegion.Pack, config, settings, ground ?? Ground(settings));
            return b;
        }

        /// <summary>A layer as uploaded (the chunks put back together with absolute indices), optionally only the chunks
        /// of some parts.</summary>
        public static MeshData Layer(TileBuild b, int layer, Func<int, bool> part = null)
        {
            MeshData src = b.Layers[layer];
            var m = new MeshData();
            m.HasUv0 = src.HasUv0;
            foreach (UploadChunk c in b.Chunks)
            {
                if (c.Layer != layer || (part != null && !part(c.Part))) continue;
                int v0 = m.VertexCount;
                m.Reserve(c.VertexCount, c.IndexCount);
                Array.Copy(src.Positions, c.FirstVertex * 3, m.Positions, v0 * 3, c.VertexCount * 3);
                Array.Copy(src.Normals, c.FirstVertex * 3, m.Normals, v0 * 3, c.VertexCount * 3);
                Array.Copy(src.Colors, c.FirstVertex * 4, m.Colors, v0 * 4, c.VertexCount * 4);
                if (src.HasUv0) Array.Copy(src.Uv0, c.FirstVertex * 2, m.Uv0, v0 * 2, c.VertexCount * 2);
                m.VertexCount += c.VertexCount;
                for (int i = 0; i < c.IndexCount; i++) m.Indices[m.IndexCount + i] = src.Indices[c.FirstIndex + i] + v0;
                m.IndexCount += c.IndexCount;
            }
            return m;
        }

        /// <summary>The bridge and ornament pieces as the streamer shows them for a camera at tile-local (x, z).</summary>
        public static MeshData Pieces(TileBuild b, int layer, int tier, double camX, double camZ)
        {
            var show = new HashSet<int>();
            foreach (LodPiece p in b.Pieces)
            {
                if (p.Layer != layer) continue;
                int lod = PieceLod.Choose(p, tier, p.DistanceTo(camX, camZ), -1);
                if (lod >= 0) show.Add(p.Index * 4 + lod);
            }
            return Layer(b, layer, show.Contains);
        }
    }

    /// <summary>
    /// The streamed world after every detail-pass package merged (integration package): bridges as their own layer on
    /// the deck heights the roads are drawn at, roundabout statues and gardens on the islands, trees and houses kept out
    /// of the drawn road corridors, the month's season on terrain and areas (and rebuilt in place when it changes), and
    /// the dressing's colliders. <see cref="DumpStreetScenes"/> (GHUMANTE_PREVIEW_DIR) writes street-level OBJ scenes of
    /// Thamel, Asan, Basantapur, Maitighar and the Thapathali bridge over the Bagmati for tools/mesh-preview.
    /// </summary>
    public class StreamingSceneTests
    {
        // Places (WGS84 lon, lat).
        private const double ThamelLon = 85.31230, ThamelLat = 27.71530;
        private const double AsanLon = 85.31130, AsanLat = 27.70790;
        private const double BasantapurLon = 85.30740, BasantapurLat = 27.70420;
        private const double MaitigharLon = 85.32020, MaitigharLat = 27.69480;
        private const double ThapathaliLon = 85.31760, ThapathaliLat = 27.68930;

        [Test]
        public void BridgesAreTheirOwnLayerOnTheDeckHeightsTheRoadsUse()
        {
            MeshingSettings s = StreamingScene.Settings(2, 10);
            TileId id = StreamingScene.TileAt(ThapathaliLon, ThapathaliLat);
            TileData src = StreamingSampleRegion.Tile(id);
            Assume.That(src, Is.Not.Null, "Thapathali is in the sample");
            TileBuild b = StreamingScene.Build(id, s);
            Assert.That(b.Error, Is.Null);
            BridgeLayout layout = BridgeLayout.For(b.Source);
            Assert.That(layout.Spans.Count, Is.GreaterThan(0), "the tile has bridges");
            int pieces = 0;
            foreach (LodPiece p in b.Pieces)
            {
                if (p.Layer != TileLayers.Bridges) continue;
                pieces++;
                Assert.That(p.LodMask, Is.EqualTo(0x7), "High builds LOD 0, 1 and 2 of every span");
            }
            Assert.That(pieces, Is.GreaterThan(0));
            Assert.That(StreamingScene.Layer(b, TileLayers.Bridges).TriangleCount, Is.GreaterThan(1000));
            Assert.That(b.Layers[TileLayers.Bridges].HasUv0, Is.True, "channels and AO");

            // Low never builds LOD0 (BridgeOptions.Lod0RadiusM), so it never keeps it.
            TileBuild low = StreamingScene.Build(id, StreamingScene.Settings(0, 10));
            foreach (LodPiece p in low.Pieces)
                if (p.Layer == TileLayers.Bridges) Assert.That(p.LodMask & 1, Is.EqualTo(0));

            // The drawn road surface stands on the deck the bridge mesher builds and the ground query walks on.
            RoadSurfaceQuery road = RoadSurfaceQuery.For(b.Source, b.Sampler, s.Roads);
            BridgeDeckIndex decks = BridgeDeckIndex.ForTile(b.Source);
            int checkedPoints = 0;
            double worst = 0;
            foreach (BridgeSpan sp in layout.Spans)
            {
                if (sp.Foot || sp.Stair || sp.Count < 3) continue;
                for (int k = 1; k + 1 < sp.Count; k++)
                {
                    double gx = id.X0 + sp.Path.X[k], gz = id.Z0 + sp.Path.Z[k];
                    float deckY, nx, ny, nz;
                    RoadSurfaceHit hit;
                    if (!decks.TryDeck(gx, gz, sp.Path.Y[k] + 0.5f, out deckY, out nx, out ny, out nz)) continue;
                    if (!road.TrySample(gx, gz, deckY + 0.3f, out hit)) continue;
                    worst = Math.Max(worst, Math.Abs(hit.Y - deckY));
                    checkedPoints++;
                }
            }
            TestContext.WriteLine("{0} deck points checked, worst road-to-deck {1:0.000} m", checkedPoints, worst);
            Assert.That(checkedPoints, Is.GreaterThan(5));
            Assert.That(worst, Is.LessThan(0.12), "the carriageway is drawn on the deck");
        }

        /// <summary>The world's ground query (deck factory, RSTR heights, the road index built on the worker) puts a rider
        /// on a flyover deck when they come from the deck and on the road below when they ride under it.</summary>
        [Test]
        public void RidersUseTheRightLevelOverAndUnderAFlyover()
        {
            MeshingSettings s = StreamingScene.Settings(1, 10, false);
            s.DrawBuildings = false;
            s.DrawInstances = false;
            int checkedPoints = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || t.Roads.Count == 0 || !t.HasRoadStructures) continue;
                BridgeLayout layout = BridgeLayout.For(t);
                BridgeSpan flyover = null;
                foreach (BridgeSpan sp in layout.Spans)
                    if (!sp.Foot && !sp.Water && !sp.Stair && sp.Kind == RoadStructureKind.Flyover && sp.Count >= 3) flyover = sp;
                if (flyover == null) continue;
                TileGroundQuery ground = StreamingScene.Ground(s);
                TileBuild b = StreamingScene.Build(id, s, ground);
                ground.Add(id, b.Sampler, b.Roads);
                IBridgeDeckQuery decks = BridgeDeckIndex.ForTile(b.Source);
                for (int k = 1; k + 1 < flyover.Count; k++)
                {
                    double gx = id.X0 + flyover.Path.X[k], gz = id.Z0 + flyover.Path.Z[k];
                    float terrain, deckY, nx, ny, nz;
                    if (!b.Sampler.TryHeightClamped(gx, gz, out terrain)) continue;
                    if (!decks.TryDeck(gx, gz, flyover.Path.Y[k] + 0.3f, out deckY, out nx, out ny, out nz) || deckY - terrain < 5.5f) continue;
                    GroundSample on, under;
                    Assert.That(ground.TrySample(gx, gz, deckY + 0.2f, out on), Is.True);
                    Assert.That(on.Height, Is.EqualTo(deckY).Within(0.35f), "on the deck at " + id + " station " + k);
                    Assert.That(ground.TrySample(gx, gz, terrain + 0.5f, out under), Is.True);
                    Assert.That(under.Height, Is.LessThan(deckY - 4f), "under the deck at " + id + " station " + k);
                    checkedPoints++;
                }
                if (checkedPoints > 0) break;
            }
            TestContext.WriteLine("{0} flyover stations checked over and under", checkedPoints);
            Assert.That(checkedPoints, Is.GreaterThan(0), "the sample has a flyover over a road");
        }

        [Test]
        public void RoundaboutsAreDressedWithTheirCentrepieces()
        {
            MeshingSettings s = StreamingScene.Settings(2, 10);
            Assert.That(s.Roads.PolicePodiums, Is.False, "the decorator owns the police posts");
            TileId id = StreamingScene.TileAt(MaitigharLon, MaitigharLat);
            TileBuild b = StreamingScene.Build(id, s);
            Assert.That(b.Error, Is.Null);
            Assert.That(b.OrnamentFailures, Is.EqualTo(0));
            double mx, mz;
            WorldFrame.LonLatToGame(MaitigharLon, MaitigharLat, out mx, out mz);
            mx -= id.X0;
            mz -= id.Z0;
            bool maitighar = false;
            int sites = 0;
            foreach (LodPiece p in b.Pieces)
            {
                if (p.Layer != TileLayers.Ornaments) continue;
                sites++;
                Assert.That(p.LodMask, Is.EqualTo(0xF), "High builds LOD 0 to 3");
                if (p.DistanceTo(mx, mz) < 25) maitighar = true;
            }
            TestContext.WriteLine("{0} decorated sites, ornament triangles {1}", sites, b.Layers[TileLayers.Ornaments].TriangleCount);
            Assert.That(maitighar, Is.True, "Maitighar Mandala is dressed");
            MeshData near = StreamingScene.Pieces(b, TileLayers.Ornaments, 2, mx, mz);
            Assert.That(near.TriangleCount, Is.GreaterThan(3000), "the mandala at LOD0");
            Assert.That(b.Colliders, Is.Not.Null);
        }

        [Test]
        public void TreesAndHousesKeepOutOfTheDrawnRoads()
        {
            MeshingSettings s = StreamingScene.Settings(1, 10);
            foreach (var (name, lon, lat) in new[] { ("thamel", ThamelLon, ThamelLat), ("asan", AsanLon, AsanLat) })
            {
                TileId id = StreamingScene.TileAt(lon, lat);
                TileBuild b = StreamingScene.Build(id, s);
                Assert.That(b.Error, Is.Null);
                IRoadCorridorQuery q = RoadCorridorIndex.ForTile(b.Source);
                int trees = 0, inRoad = 0;
                foreach (TreeInstance t in b.Instances.Trees)
                {
                    if (t.Chautari || t.Origin == TreeOrigin.Osm) continue;
                    trees++;
                    if (q.SignedDistance(id.X0 + t.X, id.Z0 + t.Z) < -0.05) inRoad++;
                }
                // Temples, stupas and shrines keep their footprints (their generators own them; the data package
                // stops the roads at them), so only the houses are checked.
                var sacred = new List<float[]>();
                foreach (BuildingRecord br in b.Source.Buildings)
                {
                    if (!BuildingGrammar.IsSacred(br.Archetype) || br.Rings == null || br.Rings.Length == 0) continue;
                    float bx0 = float.MaxValue, bz0 = float.MaxValue, bx1 = float.MinValue, bz1 = float.MinValue;
                    int[] ring = br.Rings[0];
                    for (int k = 0; k + 1 < ring.Length; k += 2)
                    {
                        bx0 = Math.Min(bx0, ring[k] / 100f);
                        bx1 = Math.Max(bx1, ring[k] / 100f);
                        bz0 = Math.Min(bz0, ring[k + 1] / 100f);
                        bz1 = Math.Max(bz1, ring[k + 1] / 100f);
                    }
                    sacred.Add(new[] { bx0 - 3f, bz0 - 3f, bx1 + 3f, bz1 + 3f });
                }
                MeshData b1 = StreamingScene.Layer(b, TileLayers.Buildings);
                int verts = 0, deep = 0;
                for (int v = 0; v < b1.VertexCount; v++)
                {
                    float vx = b1.Positions[3 * v], vz = b1.Positions[3 * v + 2];
                    bool temple = false;
                    foreach (float[] box in sacred)
                        if (vx >= box[0] && vz >= box[1] && vx <= box[2] && vz <= box[3]) temple = true;
                    if (temple) continue;
                    verts++;
                    if (q.SignedDistance(id.X0 + vx, id.Z0 + vz) < -0.5) deep++;
                }
                TestContext.WriteLine("{0}: {1} generated plants, {2} in a corridor; B1 {3} vertices, {4} more than 0.5 m inside a corridor",
                                      name, trees, inRoad, verts, deep);
                Assert.That(trees, Is.GreaterThan(0));
                Assert.That(inRoad, Is.EqualTo(0), name + ": generated plants stay out of the drawn roads");
                // Building passages (the house stays whole over its gateway) are the only houses over a corridor.
                Assert.That(deep, Is.LessThanOrEqualTo(verts / 500), name + ": houses are trimmed to the corridors");
                Assert.That(b.Colliders, Is.Not.Null, name + ": dressing colliders");
                Assert.That(b.Colliders.Cylinders.Count + b.Colliders.Boxes.Count, Is.GreaterThan(0));
            }
        }

        [Test]
        public void TerrainAndAreasTakeTheSeasonOfTheMonth()
        {
            TileId id = StreamingScene.TileAt(MaitigharLon, MaitigharLat);
            MeshingSettings winter = StreamingScene.Settings(1, 1, false), monsoon = StreamingScene.Settings(1, 7, false);
            Assert.That(winter.Season, Is.EqualTo(BiomePalette.SeasonOf(1)));
            Assert.That(winter.Areas.Season, Is.EqualTo(BiomePalette.SeasonOf(1)));
            Assert.That(monsoon.Areas.Season, Is.EqualTo(BiomePalette.SeasonOf(7)));
            Assert.That(monsoon.Areas.MaxFieldLineTris, Is.EqualTo(AreaOptions.FieldLineCap(1)), "the tier's area options");
            var a = new TileBuild { Node = new SelectedNode(id, id), RefreshMask = MeshingSettings.SeasonalLayers };
            var c = new TileBuild { Node = new SelectedNode(id, id), RefreshMask = MeshingSettings.SeasonalLayers };
            TileBuild.Execute(a, StreamingSampleRegion.Pack, StreamingConfig.ForTier(1), winter, null);
            TileBuild.Execute(c, StreamingSampleRegion.Pack, StreamingConfig.ForTier(1), monsoon, null);
            Assert.That(a.HasLayer(TileLayers.Roads) || a.HasLayer(TileLayers.Buildings), Is.False, "a refresh builds only its layers");
            Assert.That(a.Instances, Is.Null);
            MeshData ta = StreamingScene.Layer(a, TileLayers.Terrain), tc = StreamingScene.Layer(c, TileLayers.Terrain);
            Assert.That(ta.VertexCount, Is.EqualTo(tc.VertexCount));
            int differ = 0;
            for (int i = 0; i < ta.VertexCount * 4; i++)
                if (ta.Colors[i] != tc.Colors[i]) differ++;
            Assert.That(differ, Is.GreaterThan(ta.VertexCount / 10), "winter and monsoon terrain colours differ");
            Assert.That(a.HasLayer(TileLayers.Areas), Is.True);
        }

        [Test]
        public void TierSettingsCarryEveryPackagesChoices()
        {
            for (int tier = 0; tier <= 2; tier++)
            {
                MeshingSettings s = MeshingSettings.ForTier(tier);
                Assert.That(s.Roads.Detail, Is.EqualTo(tier > 0), "light road kit on Low");
                Assert.That(s.Buildings.FrontDetail, Is.True, "B1 band table options");
                Assert.That(s.Buildings.Band, Is.EqualTo(BuildingBand.B1Styled));
                Assert.That(s.Areas.MaxFieldLineTris, Is.EqualTo(AreaOptions.FieldLineCap(tier)));
                Assert.That(s.Corridors, Is.Not.Null);
                Assert.That(s.Buildings.Corridors, Is.SameAs(s.Corridors));
                Assert.That(s.DetailBuildings.Corridors, Is.SameAs(s.Corridors));
                Assert.That(s.FarBuildings.Corridors, Is.SameAs(s.Corridors));
                Assert.That(s.Trees.Corridors, Is.Not.Null);
                Assert.That(s.BridgeLods & 1, Is.EqualTo(tier == 0 ? 0 : 1));
                Assert.That(s.OrnamentLods & 1, Is.EqualTo(tier == 0 ? 0 : 1));
            }
            var lod = World.Life.LifeLod.ForTier(2);
            Assert.That(lod.PeopleCaps, Is.EqualTo(Characters.CrowdLodPlan.Caps[2]));
            var caps = new int[3];
            Generators.Fauna.FaunaLod.AnimalCaps(2, caps);
            Assert.That(lod.AnimalCaps, Is.EqualTo(caps));
        }

        /// <summary>A pack without RSTR (stage one): the derived records go into the tile and its road layout before
        /// anything is meshed, so the road surface lies on the derived deck heights the bridge mesher builds.</summary>
        [Test]
        public void PacksWithoutStructureRecordsGetTheDerivedOnes()
        {
            TileId id = StreamingScene.TileAt(ThapathaliLon, ThapathaliLat);
            TileData t = StreamingSampleRegion.Pack.ReadTile(id); // a private decode: the shared cache keeps its records
            t.RoadStructures.Clear();
            Assert.That(t.HasRoadStructures, Is.False);
            Assert.That(StructureFallback.Apply(t), Is.True);
            Assert.That(t.HasRoadStructures, Is.True);
            Assert.That(StructureFallback.Apply(t), Is.False, "idempotent");
            RoadLayout layout = RoadLayout.For(t);
            IReadOnlyList<RoadStructureRecord> derived = BridgeStructures.For(t);
            int decks = 0;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                Assert.That(layout.Structures[i].Kind, Is.EqualTo(derived[i].Kind));
                if (derived[i].HasHeights) decks++;
            }
            Assert.That(decks, Is.GreaterThan(0));
            Assert.That(layout.FinalCorridor, Is.False, "the layout keeps the stage-one width rule");
        }

        [Test]
        public void PieceLodsFollowTheTierAndHoldAtTheEdges()
        {
            var p = new LodPiece { Layer = TileLayers.Bridges, LodMask = 0x7 };
            Assert.That(PieceLod.Choose(p, 2, 10, -1), Is.EqualTo(0));
            Assert.That(PieceLod.Choose(p, 2, 200, -1), Is.EqualTo(1));
            Assert.That(PieceLod.Choose(p, 2, 600, -1), Is.EqualTo(2));
            // Just past the LOD0 edge (90 m on High) the shown LOD0 is kept; well past it LOD1 takes over.
            Assert.That(PieceLod.Choose(p, 2, 92, 0), Is.EqualTo(0));
            Assert.That(PieceLod.Choose(p, 2, 100, 0), Is.EqualTo(1));
            // Low never built LOD0: the nearest built is LOD1.
            p.LodMask = 0x6;
            Assert.That(PieceLod.Choose(p, 0, 5, -1), Is.EqualTo(1));
            var o = new LodPiece { Layer = TileLayers.Ornaments, LodMask = 0xF };
            Assert.That(PieceLod.Choose(o, 2, 30, -1), Is.EqualTo(0));
            Assert.That(PieceLod.Choose(o, 2, 1000, -1), Is.EqualTo(3));
            Assert.That(OrnamentLod.BuiltMask(0) & 1, Is.EqualTo(0));
        }

        [Test]
        public void TileLocalCorridorsAnswerLikeTheGameMetreIndex()
        {
            TileId id = StreamingScene.TileAt(ThamelLon, ThamelLat);
            TileData t = StreamingSampleRegion.Tile(id);
            Func<TileData, IRoadCorridorQuery> local = CorridorFrames.TileLocal(RoadCorridorIndex.ForTile);
            IRoadCorridorQuery l = local(t), g = RoadCorridorIndex.ForTile(t);
            Assert.That(local(t), Is.SameAs(l), "cached per tile");
            for (int i = 0; i < 50; i++)
            {
                double x = 20 * i, z = 1000 - 19 * i;
                Assert.That(l.SignedDistance(x, z), Is.EqualTo(g.SignedDistance(id.X0 + x, id.Z0 + z)));
            }
            double[] px = { 100, 104, 104, 100 }, pz = { 100, 100, 104, 104 };
            double d1, d2;
            bool o1 = l.Overlaps(px, pz, 4, out d1);
            for (int i = 0; i < 4; i++)
            {
                px[i] += id.X0;
                pz[i] += id.Z0;
            }
            bool o2 = g.Overlaps(px, pz, 4, out d2);
            Assert.That(o1, Is.EqualTo(o2));
            Assert.That(d1, Is.EqualTo(d2));
        }

        // ---------------------------------------------------------------------------------------------------------
        // The scheduler swaps seasonal layers in place

        private sealed class RefreshSink : ITileSink, ITileRefreshSink
        {
            public readonly Dictionary<SelectedNode, uint> Terrain = new Dictionary<SelectedNode, uint>();
            public readonly HashSet<SelectedNode> Views = new HashSet<SelectedNode>();
            public readonly Dictionary<SelectedNode, uint> Staged = new Dictionary<SelectedNode, uint>();
            public int Commits, Releases, Aborts;

            private static uint FirstColour(TileBuild b, int chunk)
            {
                UploadChunk c = b.Chunks[chunk];
                byte[] col = b.Layers[c.Layer].Colors;
                int o = c.FirstVertex * 4;
                return (uint)(col[o] << 24 | col[o + 1] << 16 | col[o + 2] << 8 | col[o + 3]);
            }

            public void Upload(TileBuild build, int chunk)
            {
                Views.Add(build.Node);
                if (build.Chunks[chunk].Layer == TileLayers.Terrain && !Terrain.ContainsKey(build.Node)) Terrain[build.Node] = FirstColour(build, chunk);
            }

            public void SetVisible(SelectedNode node, bool visible)
            {
            }

            public void Release(SelectedNode node)
            {
                Releases++;
                Views.Remove(node);
                Terrain.Remove(node);
                Staged.Remove(node);
            }

            public void Ready(SelectedNode node, TileExtras extras)
            {
            }

            public void UploadRefresh(TileBuild build, int chunk)
            {
                Assert.That(Views.Contains(build.Node), "refreshes only nodes with a view");
                if (build.Chunks[chunk].Layer == TileLayers.Terrain && !Staged.ContainsKey(build.Node)) Staged[build.Node] = FirstColour(build, chunk);
            }

            public void CommitRefresh(SelectedNode node, int layerMask)
            {
                Assert.That(layerMask, Is.EqualTo(MeshingSettings.SeasonalLayers));
                Commits++;
                uint c;
                if (Staged.TryGetValue(node, out c)) Terrain[node] = c;
                Staged.Remove(node);
            }

            public void AbortRefresh(SelectedNode node)
            {
                Aborts++;
                Staged.Remove(node);
            }
        }

        [Test]
        public void SchedulerRebuildsTheSeasonalLayersInPlace()
        {
            MeshingSettings s = StreamingScene.Settings(0, 7, false);
            s.DrawBuildings = false;
            s.DrawInstances = false;
            s.DrawOrnaments = false;
            s.DrawBridges = false;
            var config = StreamingConfig.ForTier(0);
            var sink = new RefreshSink();
            var ground = StreamingScene.Ground(s);
            var sched = new StreamingScheduler(StreamingSampleRegion.Pack, config, TileSelector.ForPack(config, StreamingSampleRegion.Pack), ground, sink,
                                               new InlineJobRunner(), () => 0.0, s)
            {
                UploadBudgetBytes = long.MaxValue,
            };
            double fx, fz;
            WorldFrame.LonLatToGame(ThamelLon, ThamelLat, out fx, out fz);
            for (int i = 0; i < 2000 && !sched.IsSettled; i++) sched.Tick(fx, fz);
            Assert.That(sched.IsSettled, Is.True);
            int views = sink.Views.Count;
            var before = new Dictionary<SelectedNode, uint>(sink.Terrain);
            int releases = sink.Releases;

            Assert.That(s.SetMonth(1), Is.True, "July to January changes the season");
            Assert.That(s.SetMonth(1), Is.False);
            int marked = sched.RefreshLayers(MeshingSettings.SeasonalLayers);
            Assert.That(marked, Is.EqualTo(views));
            for (int i = 0; i < 4000 && sched.StaleCount > 0; i++) sched.Tick(fx, fz);
            for (int i = 0; i < 10; i++) sched.Tick(fx, fz);
            Assert.That(sched.StaleCount, Is.EqualTo(0));
            Assert.That(sink.Commits, Is.EqualTo(views), "every view refreshed once");
            Assert.That(sink.Releases, Is.EqualTo(releases), "nothing was released for the refresh");
            Assert.That(sink.Views.Count, Is.EqualTo(views));
            Assert.That(sink.Aborts, Is.EqualTo(0));
            int changed = 0;
            foreach (KeyValuePair<SelectedNode, uint> kv in before)
                if (sink.Terrain[kv.Key] != kv.Value) changed++;
            TestContext.WriteLine("{0} views refreshed, {1} first terrain colours changed", views, changed);
            Assert.That(changed, Is.GreaterThan(views / 4));
            Assert.That(sched.Stats.MeshBytes, Is.GreaterThan(0));
            sched.Dispose();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Street-level dumps (visual self-check)

        private struct Shot
        {
            public string Name;
            public double Lon, Lat;

            /// <summary>Eye on a drawn road this far from the target (below 0: beside the bridge, over the bank).</summary>
            public double DistM;

            public float EyeH, LookH;

            /// <summary>Target the nearest motor bridge span instead of the place itself.</summary>
            public bool Bridge;

            /// <summary>Target the nearest decorated roundabout instead of the place itself.</summary>
            public bool Island;

            /// <summary>Look down the street nearest the place (the eye on the same centreline).</summary>
            public bool Street;

            /// <summary>Pick the eye round the target with the clearest line of sight (fewest house walls crossed,
            /// preferably on a road).</summary>
            public bool Free;

            /// <summary>Target the nearest hero replica instead of the place itself.</summary>
            public bool Hero;
        }

        [Test]
        public void DumpStreetScenes()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the street scenes");
            int tier = 2, month = 10;
            string monthEnv = Environment.GetEnvironmentVariable("GHUMANTE_SCENE_MONTH");
            if (!string.IsNullOrEmpty(monthEnv)) month = int.Parse(monthEnv, CultureInfo.InvariantCulture);
            MeshingSettings s = StreamingScene.Settings(tier, month);
            var shots = new[]
            {
                new Shot { Name = "thamel", Lon = ThamelLon, Lat = ThamelLat, DistM = 45, EyeH = 2.2f, LookH = 2.2f, Street = true },
                new Shot { Name = "asan", Lon = AsanLon, Lat = AsanLat, DistM = 40, EyeH = 2.2f, LookH = 2.2f, Street = true },
                new Shot { Name = "basantapur", Lon = BasantapurLon, Lat = BasantapurLat, DistM = 75, EyeH = 24f, LookH = 4f, Free = true, Hero = true },
                new Shot { Name = "new_road_statue", Lon = 85.30914, Lat = 27.70365, DistM = 32, EyeH = 2.2f, LookH = 3.5f, Island = true },
                new Shot { Name = "shahid_gate", Lon = 85.31497, Lat = 27.69959, DistM = 48, EyeH = 2.2f, LookH = 6f, Island = true },
                new Shot { Name = "maitighar", Lon = MaitigharLon, Lat = MaitigharLat, DistM = 42, EyeH = 2.2f, LookH = 3f, Island = true },
                new Shot { Name = "bagmati_bridge", Lon = ThapathaliLon, Lat = ThapathaliLat, DistM = 55, EyeH = 2.2f, LookH = 2f, Bridge = true },
                new Shot { Name = "bagmati_bridge_side", Lon = ThapathaliLon, Lat = ThapathaliLat, Bridge = true, DistM = -1, EyeH = 9f, LookH = 0f },
            };
            var index = new StringBuilder();
            var cache = new Dictionary<ulong, TileBuild>();
            foreach (Shot shot in shots)
            {
                TileId id = StreamingScene.TileAt(shot.Lon, shot.Lat);
                TileBuild b;
                if (!cache.TryGetValue(id.Key, out b))
                {
                    b = StreamingScene.Build(id, s);
                    cache[id.Key] = b;
                }
                Assert.That(b.Error, Is.Null, shot.Name);
                double tx, tz;
                WorldFrame.LonLatToGame(shot.Lon, shot.Lat, out tx, out tz);
                tx -= id.X0;
                tz -= id.Z0;
                if (shot.Bridge) BridgeTarget(b.Source, ref tx, ref tz);
                if (shot.Island) IslandTarget(b, ref tx, ref tz);
                if (shot.Hero) HeroTarget(b, ref tx, ref tz);
                double ex, ez;
                if (shot.Free) FreeEye(b.Source, tx, tz, shot.DistM, out ex, out ez);
                else if (shot.DistM > 0 && shot.Street) EyeAlongRoad(b.Source, ref tx, ref tz, shot.DistM, out ex, out ez);
                else if (shot.DistM > 0) EyeOnRoad(b.Source, tx, tz, shot.DistM, out ex, out ez);
                else
                {
                    // Beside the bridge over the river bank, looking across at the deck, railings and piers.
                    BridgeSide(b.Source, tx, tz, 60, out ex, out ez);
                }
                float gy = Ground(b, ex, ez), ty = Ground(b, tx, tz);
                float ey = gy + shot.EyeH, ly = (shot.Bridge && shot.DistM < 0 ? DeckY(b, tx, tz, ty) : ty) + shot.LookH;
                DumpShot(b, s, tier, month, shot.Name, (float)ex, ey, (float)ez, (float)tx, ly, (float)tz, index);
            }
            File.WriteAllText(Path.Combine(ObjDump.Dir, "streaming" + (month != 10 ? "_m" + month : ""), "scenes.txt"), index.ToString());
            TestContext.WriteLine(index.ToString());
        }

        /// <summary>The seasonal layers (terrain and areas) of a fringe tile below Swayambhu in January, April, July and
        /// October, as the refresh builds make them (GHUMANTE_PREVIEW_DIR).</summary>
        [Test]
        public void DumpSeasons()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the seasons");
            TileId id = StreamingScene.TileAt(85.2840, 27.7120);
            Assume.That(StreamingSampleRegion.Tile(id), Is.Not.Null);
            foreach (int month in new[] { 1, 4, 7, 10 })
            {
                MeshingSettings s = StreamingScene.Settings(1, month, false);
                var b = new TileBuild { Node = new SelectedNode(id, id), RefreshMask = MeshingSettings.SeasonalLayers };
                TileBuild.Execute(b, StreamingSampleRegion.Pack, StreamingConfig.ForTier(1), s, null);
                ObjDump.Write(StreamingScene.Layer(b, TileLayers.Terrain), "streaming/seasons/m" + month + "/terrain.obj", new[] { "month " + month + " " + s.Season });
                ObjDump.Write(StreamingScene.Layer(b, TileLayers.Areas), "streaming/seasons/m" + month + "/areas.obj", new[] { "month " + month + " " + s.Season });
            }
        }

        private static float Ground(TileBuild b, double x, double z)
        {
            double gx = b.Node.Area.X0 + x, gz = b.Node.Area.Z0 + z;
            float y;
            b.Sampler.TryHeightClamped(gx, gz, out y);
            RoadSurfaceHit hit;
            if (RoadSurfaceQuery.For(b.Source, b.Sampler, new RoadOptions()).TrySample(gx, gz, y + 1f, out hit)) return hit.Y;
            return y;
        }

        private static float DeckY(TileBuild b, double x, double z, float fallback)
        {
            float y, nx, ny, nz;
            return BridgeDeckIndex.ForTile(b.Source).TryDeck(b.Node.Area.X0 + x, b.Node.Area.Z0 + z, fallback + 30f, out y, out nx, out ny, out nz) ? y : fallback;
        }

        /// <summary>The middle of the motor bridge span nearest the place.</summary>
        private static void BridgeTarget(TileData t, ref double x, ref double z)
        {
            BridgeLayout layout = BridgeLayout.For(t);
            double best = double.MaxValue, bx = x, bz = z;
            foreach (BridgeSpan sp in layout.Spans)
            {
                if (sp.Foot || sp.Stair || !sp.Water || sp.Count < 2) continue;
                int k = sp.Count / 2;
                double d = (sp.Path.X[k] - x) * (sp.Path.X[k] - x) + (sp.Path.Z[k] - z) * (sp.Path.Z[k] - z);
                if (d < best)
                {
                    best = d;
                    bx = sp.Path.X[k];
                    bz = sp.Path.Z[k];
                }
            }
            x = bx;
            z = bz;
        }

        /// <summary>The eye on a circle round the target whose sight line crosses the fewest house walls (inside no
        /// house), preferring points on a drawn road.</summary>
        private static void FreeEye(TileData t, double x, double z, double dist, out double ex, out double ez)
        {
            IRoadCorridorQuery q = RoadCorridorIndex.ForTile(t);
            double best = double.MaxValue;
            ex = x + dist;
            ez = z;
            for (int a = 0; a < 360; a += 10)
            {
                double r = a * Math.PI / 180, cx = x + dist * Math.Sin(r), cz = z + dist * Math.Cos(r);
                int walls = 0;
                bool inside = false;
                foreach (BuildingRecord br in t.Buildings)
                {
                    if (br.Rings == null || br.Rings.Length == 0) continue;
                    int[] ring = br.Rings[0];
                    int n = ring.Length / 2;
                    bool inPoly = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        double ax = ring[2 * i] / 100.0, az = ring[2 * i + 1] / 100.0, bx = ring[2 * j] / 100.0, bz = ring[2 * j + 1] / 100.0;
                        if ((az > cz) != (bz > cz) && cx < (bx - ax) * (cz - az) / (bz - az) + ax) inPoly = !inPoly;
                        if (Crosses(cx, cz, x, z, ax, az, bx, bz)) walls++;
                    }
                    if (inPoly) inside = true;
                }
                if (inside) continue;
                double score = walls + (q.SignedDistance(t.Tile.X0 + cx, t.Tile.Z0 + cz) < 0 ? 0 : 0.5);
                if (score < best)
                {
                    best = score;
                    ex = cx;
                    ez = cz;
                }
            }
        }

        private static bool Crosses(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            double d1 = (bx - ax) * (cz - az) - (bz - az) * (cx - ax), d2 = (bx - ax) * (dz - az) - (bz - az) * (dx - ax);
            double d3 = (dx - cx) * (az - cz) - (dz - cz) * (ax - cx), d4 = (dx - cx) * (bz - cz) - (dz - cz) * (bx - cx);
            return (d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0);
        }

        /// <summary>The centre of the hero replica nearest the place.</summary>
        private static void HeroTarget(TileBuild b, ref double x, ref double z)
        {
            double best = double.MaxValue, bx = x, bz = z;
            foreach (HeroPiece h in b.Heroes)
            {
                double d = (h.CX - x) * (h.CX - x) + (h.CZ - z) * (h.CZ - z);
                if (d >= best) continue;
                best = d;
                bx = h.CX;
                bz = h.CZ;
            }
            x = bx;
            z = bz;
        }

        /// <summary>The centre of the decorated roundabout nearest the place.</summary>
        private static void IslandTarget(TileBuild b, ref double x, ref double z)
        {
            double best = double.MaxValue, bx = x, bz = z;
            foreach (LodPiece p in b.Pieces)
            {
                if (p.Layer != TileLayers.Ornaments) continue;
                double d = p.DistanceTo(x, z);
                if (d >= best) continue;
                best = d;
                bx = p.CX;
                bz = p.CZ;
            }
            x = bx;
            z = bz;
        }

        private static void BridgeSide(TileData t, double x, double z, double dist, out double ex, out double ez)
        {
            BridgeLayout layout = BridgeLayout.For(t);
            ex = x + dist;
            ez = z;
            double best = double.MaxValue;
            foreach (BridgeSpan sp in layout.Spans)
                for (int k = 0; k < sp.Count; k++)
                {
                    double d = (sp.Path.X[k] - x) * (sp.Path.X[k] - x) + (sp.Path.Z[k] - z) * (sp.Path.Z[k] - z);
                    if (d >= best) continue;
                    best = d;
                    // Off to the side, a little along the span so the piers and the railing show in perspective.
                    ex = x + sp.Path.Nx[k] * dist + sp.Path.Ux[k] * dist * 0.35;
                    ez = z + sp.Path.Nz[k] * dist + sp.Path.Uz[k] * dist * 0.35;
                }
        }

        /// <summary>An eye on the drawn motor road nearest the target, <paramref name="dist"/> back along its centreline,
        /// and the target moved onto that centreline, so the camera looks down the street.</summary>
        private static void EyeAlongRoad(TileData t, ref double x, ref double z, double dist, out double ex, out double ez)
        {
            RoadLayout layout = RoadLayout.For(t);
            ex = x + dist;
            ez = z;
            double best = double.MaxValue;
            int br = -1, bk = 0;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadCentreline c = layout.Centres[i];
                if (c == null || c.Count < 2 || !RoadMesher.IsDrawn(t, i, new RoadOptions())) continue;
                for (int k = 0; k < c.Count; k++)
                {
                    double d = (c.X[k] - x) * (c.X[k] - x) + (c.Z[k] - z) * (c.Z[k] - z);
                    if (d < best)
                    {
                        best = d;
                        br = i;
                        bk = k;
                    }
                }
            }
            if (br < 0) return;
            RoadCentreline cl = layout.Centres[br];
            x = cl.X[bk];
            z = cl.Z[bk];
            // Back along the road (or forward when the road starts too close), as far as the road goes.
            double want = cl.S[bk] - dist >= cl.S[0] ? cl.S[bk] - dist : Math.Min(cl.S[cl.Count - 1], cl.S[bk] + dist);
            for (int k = 0; k + 1 < cl.Count; k++)
            {
                if (want < cl.S[k] || want > cl.S[k + 1]) continue;
                double f = cl.S[k + 1] > cl.S[k] ? (want - cl.S[k]) / (cl.S[k + 1] - cl.S[k]) : 0;
                ex = cl.X[k] + (cl.X[k + 1] - cl.X[k]) * f;
                ez = cl.Z[k] + (cl.Z[k + 1] - cl.Z[k]) * f;
                return;
            }
            ex = cl.X[0];
            ez = cl.Z[0];
        }

        /// <summary>An eye on a drawn motor road about <paramref name="dist"/> from the target (on the centreline).</summary>
        private static void EyeOnRoad(TileData t, double x, double z, double dist, out double ex, out double ez)
        {
            RoadLayout layout = RoadLayout.For(t);
            ex = x + dist;
            ez = z;
            double best = double.MaxValue;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadCentreline c = layout.Centres[i];
                if (c == null || !RoadWidthModel.IsMotor(t.Roads[i].RoadClass)) continue;
                for (int k = 0; k < c.Count; k++)
                {
                    double d = Math.Sqrt((c.X[k] - x) * (c.X[k] - x) + (c.Z[k] - z) * (c.Z[k] - z));
                    double score = Math.Abs(d - dist);
                    if (score < best)
                    {
                        best = score;
                        ex = c.X[k];
                        ez = c.Z[k];
                    }
                }
            }
        }

        private static void DumpShot(TileBuild b, MeshingSettings s, int tier, int month, string name, float ex, float ey, float ez, float lx, float ly, float lz,
                                     StringBuilder index)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            string dir = "streaming" + (month != 10 ? "_m" + month : "") + "/" + name;
            string eye = string.Format(c, "{0:0.##},{1:0.##},{2:0.##}", ex, ey, -ez);
            string look = string.Format(c, "{0:0.##},{1:0.##},{2:0.##}", lx, ly, -lz);
            var directives = new[]
            {
                "integration scene " + name + ": tile " + b.Node.Area + ", tier " + tier + ", month " + month + " (" + s.Season + ")",
                "meshpreview: grid=0 sun=160,62 eye=" + eye + " look=" + look,
            };
            const float r = 260f;
            float x0 = Math.Min(ex, lx) - r, z0 = Math.Min(ez, lz) - r, x1 = Math.Max(ex, lx) + r, z1 = Math.Max(ez, lz) + r;
            var files = new List<string>();
            void Put(string layer, MeshData m)
            {
                MeshData cropped = ObjDump.Crop(m, x0, z0, x1, z1, true);
                if (cropped.TriangleCount == 0) return;
                ObjDump.Write(cropped, dir + "/" + layer + ".obj", directives);
                files.Add(layer + ".obj");
            }
            Put("terrain", StreamingScene.Layer(b, TileLayers.Terrain));
            Put("roads", StreamingScene.Layer(b, TileLayers.Roads));
            Put("decals", StreamingScene.Layer(b, TileLayers.RoadDecals));
            Put("areas", StreamingScene.Layer(b, TileLayers.Areas));
            Put("bridges", StreamingScene.Pieces(b, TileLayers.Bridges, tier, ex, ez));
            Put("ornaments", StreamingScene.Pieces(b, TileLayers.Ornaments, tier, ex, ez));
            // Heroes at LOD0 near, as the hero budget would give them a street-level camera.
            Put("heroes", StreamingScene.Layer(b, TileLayers.Heroes, p => p % 4 == 0));
            // B0 within the tier's B0 radius of the eye (the streamer's detail cells: the band table's lite options with
            // the world's corridors), B1 beyond it.
            float b0 = BuildingBandTable.OuterM(tier, 0);
            var near = new MeshData();
            BuildingMesher.Build(b.Source, b.Sampler, BuildingBandTable.LiteOptions(tier, s.DetailBuildings), near);
            Put("buildings_b0", ObjDump.Crop(near, ex - b0, ez - b0, ex + b0, ez + b0, true));
            Put("buildings_b1", ObjDump.Crop(StreamingScene.Layer(b, TileLayers.Buildings), ex - b0, ez - b0, ex + b0, ez + b0, false));
            Put("dressing", Dressing(b, month, ex, ez, 140f));
            var sh = new StringBuilder();
            string full = Path.Combine(ObjDump.Dir, dir);
            string all = string.Join(" ", files.ConvertAll(f => "$D/" + f));
            sh.Append("#!/bin/sh\nD=").Append(full).Append('\n');
            sh.Append("python3 tools/mesh-preview/render.py --combine ").Append(all).Append(" -o $D/street.png --size 1200 --height 700 --fog 420\n");
            sh.Append("python3 tools/mesh-preview/render.py --combine ").Append(all).Append(" -o $D/bird.png --views bird --no-eye --size 1000 --radius 220\n");
            File.WriteAllText(Path.Combine(full, "render.sh"), sh.ToString());
            index.Append(name).Append(": tile ").Append(b.Node.Area).Append(" eye ").Append(eye).Append(" look ").Append(look)
                 .Append(" bridges ").Append(b.Layers[TileLayers.Bridges].TriangleCount).Append(" ornaments ").Append(b.Layers[TileLayers.Ornaments].TriangleCount)
                 .Append(" pieces ").Append(b.Pieces.Count).Append('\n');
        }

        /// <summary>Trees (LOD0 within 60 m, LOD1 beyond) and parked vehicles (LOD1) within <paramref name="radius"/> of the eye.</summary>
        private static MeshData Dressing(TileBuild b, int month, float ex, float ez, float radius)
        {
            var m = new MeshData();
            m.HasUv0 = true;
            var unit = new MeshData();
            foreach (TreeInstance t in b.Instances.Trees)
            {
                float dx = t.X - ex, dz = t.Z - ez;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d > radius) continue;
                unit.Clear();
                FloraMesher.Build(t.Species, d < 60f ? 0 : 1, month, unit);
                FloraObj.Append(m, unit, t.X, t.Y - 0.1f, t.Z, t.YawDeg, t.CrownM, t.HeightM, t.CrownM);
            }
            var wheel = new MeshData();
            foreach (ParkedVehicle p in b.Instances.Parked)
            {
                float dx = p.X - ex, dz = p.Z - ez;
                if (dx * dx + dz * dz > radius * radius) continue;
                unit.Clear();
                VehicleMesher.Build(p.Variant, p.Livery, VehicleLod.Lod1, unit, p.Id);
                foreach (WheelSocket w in VehicleMesher.Wheels(VehicleCatalog.At(p.Variant)))
                {
                    wheel.Clear();
                    VehicleMesher.BuildWheel(w, VehicleLod.Lod1, wheel);
                    ObjDump.Append(unit, wheel, w.X, w.Y, w.Z);
                }
                FloraObj.Append(m, unit, p.X, p.Y, p.Z, p.YawDeg);
            }
            return m;
        }
    }
}
