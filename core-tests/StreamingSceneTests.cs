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

        /// <summary>A pack without RSTR (stage one): the derived records go into the tile before anything is meshed and its
        /// road layout is rebuilt from them, so the road surface lies on the derived deck heights the bridge mesher builds,
        /// elevated decks keep their mapped points (no fillet, unlike the layout built from absent records), derived foot
        /// structures lose car access, and the corridor widths keep the stage-one rule.</summary>
        [Test]
        public void PacksWithoutStructureRecordsGetTheDerivedOnes()
        {
            TileId id = StreamingScene.TileAt(ThapathaliLon, ThapathaliLat);
            TileData t = StreamingSampleRegion.Pack.ReadTile(id); // a private decode: the shared cache keeps its records
            t.RoadStructures.Clear();
            Assert.That(t.HasRoadStructures, Is.False);
            // What the layout looked like before (absent records): a layout read before the fallback is replaced.
            RoadLayout before = RoadLayout.For(t);
            Assert.That(StructureFallback.Apply(t), Is.True);
            Assert.That(t.HasRoadStructures, Is.True);
            Assert.That(StructureFallback.Apply(t), Is.False, "idempotent");
            RoadLayout layout = RoadLayout.For(t);
            Assert.That(layout, Is.Not.SameAs(before), "the cached layout is rebuilt from the installed records");
            IReadOnlyList<RoadStructureRecord> derived = BridgeStructures.For(t);
            int decks = 0, elevated = 0;
            double worstNew = 0, worstOld = 0;
            double size = t.Tile.Size;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                Assert.That(layout.Structures[i].Kind, Is.EqualTo(derived[i].Kind));
                Assert.That(layout.Profiles[i].NoCars, Is.EqualTo(!t.RoadStructures[i].Has(RoadStructureFlags.CarAccessible)),
                            "car access of road " + i + " follows the installed record");
                if (derived[i].HasHeights) decks++;
                if (!derived[i].IsElevated || layout.RingOf[i] >= 0) continue;
                // An elevated deck keeps its mapped points (only the simplifier's tolerance, no fillet): the points the
                // bridge mesher builds the deck on.
                elevated++;
                double tol = RoadCentreline.SimplifyToleranceM(t.Roads[i].RoadClass) + 0.02;
                int[] pts = t.Roads[i].Points;
                for (int k = 0; k < t.Roads[i].PointCount; k++)
                {
                    double px = pts[2 * k] / 100.0, pz = pts[2 * k + 1] / 100.0;
                    if (px < 1 || pz < 1 || px > size - 1 || pz > size - 1) continue;
                    double dNew = DistanceToCentre(layout.Centres[i], px, pz), dOld = DistanceToCentre(before.Centres[i], px, pz);
                    worstNew = Math.Max(worstNew, dNew);
                    worstOld = Math.Max(worstOld, dOld);
                    Assert.That(dNew, Is.LessThan(tol), "elevated road " + i + " point " + k + " is on the drawn centreline");
                }
            }
            TestContext.WriteLine("{0} decks, {1} elevated roads; mapped points off the drawn centreline: {2:0.000} m (rebuilt), {3:0.000} m (absent records)",
                                  decks, elevated, worstNew, worstOld);
            Assert.That(decks, Is.GreaterThan(0));
            Assert.That(elevated, Is.GreaterThan(0));
            Assert.That(layout.FinalCorridor, Is.False, "the layout keeps the stage-one width rule");
            Assert.That(layout.FinalCorridor, Is.EqualTo(before.FinalCorridor));
        }

        private static double DistanceToCentre(RoadCentreline c, double x, double z)
        {
            double best = double.MaxValue;
            for (int q = 0; q + 1 < c.Count; q++)
            {
                double ax = c.X[q], az = c.Z[q], dx = c.X[q + 1] - ax, dz = c.Z[q + 1] - az, l2 = dx * dx + dz * dz;
                double f = l2 > 1e-12 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
                f = f < 0 ? 0 : f > 1 ? 1 : f;
                double qx = ax + dx * f - x, qz = az + dz * f - z;
                best = Math.Min(best, Math.Sqrt(qx * qx + qz * qz));
            }
            return best;
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

        /// <summary>The instanced dressing's colliders (trunks, poles, parked vehicles) are a set of their own in the
        /// ground query: the small hero and B0 set is rebuilt alone when a B0 cell shows or hides, and dropping it leaves
        /// the dressing standing.</summary>
        [Test]
        public void DressingCollidersAreASetOfTheirOwn()
        {
            MeshingSettings s = StreamingScene.Settings(1, 10);
            TileId id = StreamingScene.TileAt(AsanLon, AsanLat);
            TileBuild b = StreamingScene.Build(id, s);
            Assert.That(b.Colliders, Is.Not.Null);
            // Trunks, street-prop poles and the parked vehicles nobody can borrow. Since the first playtest (#3) every parked
            // vehicle the player can drive is a community-fleet one, registered round the player by ExploreSession rather
            // than in this static set, and the bazaar keeps few parked vehicles: what is left is the trees and the props.
            Assert.That(b.Colliders.Count, Is.GreaterThan(150), "Asan's dressing");
            ColliderCylinder trunk = default(ColliderCylinder);
            bool found = false;
            foreach (ColliderCylinder c in b.Colliders.Cylinders)
            {
                if (c.Radius < 0.15f || c.Top - c.Bottom < 2f) continue;
                trunk = c;
                found = true;
                break;
            }
            Assert.That(found, Is.True, "a trunk or pole");
            TileGroundQuery ground = StreamingScene.Ground(s);
            ulong key = id.Key, staticKey = key | TileGroundQuery.StaticSetKeyBit;
            double gx = id.X0 + trunk.CX, gz = id.Z0 + trunk.CZ;
            float feet = trunk.Bottom + 0.1f;
            Assert.That(ground.IsBlocked(gx, gz, feet), Is.False, "nothing registered yet");
            ground.Register(staticKey, b.Colliders);
            Assert.That(ground.IsBlocked(gx, gz, feet), Is.True, "the trunk blocks once the static set is in");
            // The hero/B0 set comes and goes (a B0 cell toggling) without touching the static set.
            var small = new StructureColliders();
            small.Boxes.Add(new OrientedBox { CX = 5, CZ = 5, CY = 1300f, HalfX = 1f, HalfY = 1f, HalfZ = 1f, Material = FootSurface.Stone });
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 20; i++)
            {
                ground.Register(key, small);
                ground.Unregister(key);
            }
            double smallMs = sw.Elapsed.TotalMilliseconds / 20;
            sw.Restart();
            ground.Register(staticKey, b.Colliders);
            double staticMs = sw.Elapsed.TotalMilliseconds;
            TestContext.WriteLine("{0} dressing colliders: static set {1:0.00} ms once, hero/B0 set {2:0.000} ms per toggle", b.Colliders.Count,
                                  staticMs, smallMs);
            Assert.That(ground.StructureTileCount, Is.EqualTo(1));
            Assert.That(ground.IsBlocked(gx, gz, feet), Is.True, "the dressing stays while the hero set toggles");
            ground.Register(key, small);
            Assert.That(ground.StructureTileCount, Is.EqualTo(2), "two sets for one tile");
            ground.Unregister(staticKey);
            Assert.That(ground.IsBlocked(gx, gz, feet), Is.False, "dropping the static set frees the trunk");
            Assert.That(ground.StructureTileCount, Is.EqualTo(1));
        }

        /// <summary>The walkable top of a standalone island (Shahid Gate, 10/517/160) stays inside the island: no corner or
        /// edge of it lies on the drawn carriageway, so riders circling the gate meet no invisible kerb.</summary>
        [Test]
        public void StandaloneIslandTopsStayOffTheCarriageway()
        {
            MeshingSettings s = StreamingScene.Settings(2, 10);
            TileId id = StreamingScene.TileAt(85.31497, 27.69959);
            TileBuild b = StreamingScene.Build(id, s);
            Assert.That(b.Error, Is.Null);
            IRoadCorridorQuery q = RoadCorridorIndex.ForTile(b.Source);
            var sites = new List<Generators.Ornaments.RoundaboutEntry>();
            Generators.Ornaments.RoundaboutCatalog.StandaloneIn(b.Source, sites);
            Assert.That(sites.Count, Is.GreaterThan(0), "Shahid Gate is a standalone site");
            int tops = 0;
            double worst = double.MaxValue;
            foreach (Generators.Ornaments.RoundaboutEntry e in sites)
            {
                Generators.Ornaments.RoundaboutSite site = Generators.Ornaments.RoundaboutDecorator.StandaloneSite(e, b.Source);
                foreach (OrientedBox box in b.Colliders.Boxes)
                {
                    if (box.Material != FootSurface.Grass) continue;
                    double d = Math.Sqrt((box.CX - site.X) * (box.CX - site.X) + (box.CZ - site.Z) * (box.CZ - site.Z));
                    if (d > 0.5) continue;
                    tops++;
                    double cs = Math.Cos(box.YawRad), sn = Math.Sin(box.YawRad);
                    for (int i = -1; i <= 1; i++)
                        for (int j = -1; j <= 1; j++)
                        {
                            if (i == 0 && j == 0) continue;
                            double lx = i * box.HalfX, lz = j * box.HalfZ;
                            double px = id.X0 + box.CX + lx * cs - lz * sn, pz = id.Z0 + box.CZ + lx * sn + lz * cs;
                            worst = Math.Min(worst, q.SignedDistance(px, pz));
                        }
                    // The top still covers most of the island (union of turned inscribed squares, about 0.8 r).
                    Assert.That(Math.Max(box.HalfX, box.HalfZ), Is.GreaterThan(0.5 * site.RadiusM), e.Id);
                }
            }
            TestContext.WriteLine("{0} island top boxes, nearest corner {1:0.00} m outside the corridors", tops, worst);
            Assert.That(tops, Is.GreaterThanOrEqualTo(TileBuild.IslandTopSquares), "the island top");
            Assert.That(worst, Is.GreaterThanOrEqualTo(0.0), "no island-top corner on the carriageway");
        }

        /// <summary>Parked vehicles stand on the drawn road they line (within <see cref="ParkedPlacement.SurfaceToleranceM"/>)
        /// on every tile with bridges, flyovers or ramps, and never under a deck or inside an embankment.</summary>
        [Test]
        public void ParkedVehiclesStandOnTheDrawnRoadNotUnderDecks()
        {
            MeshingSettings s = StreamingScene.Settings(1, 10, false);
            int tiles = 0, vehicles = 0, off = 0, under = 0;
            string first = null;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || t.Roads.Count == 0 || !t.HasRoadStructures) continue;
                bool elevated = false;
                for (int i = 0; i < t.Roads.Count && !elevated; i++)
                    elevated = t.RoadStructureOf(i).IsElevated || t.RoadStructureOf(i).HasHeights;
                if (!elevated) continue;
                tiles++;
                TileHeightSampler h = TileHeightSampler.ForArea(t, id, StreamingConfig.ForTier(1).TerrainStep(id, id, t.HeightsN));
                var parked = new List<ParkedVehicle>();
                ParkedPlacement.Place(t, h, parked, s.Roads);
                RoadSurfaceQuery road = RoadSurfaceQuery.For(t, h, s.Roads);
                IBridgeDeckQuery decks = BridgeDeckIndex.ForTile(t);
                foreach (ParkedVehicle p in parked)
                {
                    vehicles++;
                    double gx = id.X0 + p.X, gz = id.Z0 + p.Z;
                    RoadSurfaceHit hit;
                    if (road.TrySample(gx, gz, p.Y + 0.3f, out hit) && (hit.OnDeck || Math.Abs(hit.Y - p.Y) > ParkedPlacement.SurfaceToleranceM))
                    {
                        off++;
                        if (first == null) first = id + " at (" + p.X.ToString("0.0", CultureInfo.InvariantCulture) + ", " + p.Z.ToString("0.0", CultureInfo.InvariantCulture) + ")";
                    }
                    float under0;
                    if (decks.TryCeiling(gx, gz, p.Y + 0.2f, out under0) && under0 - p.Y < RoadClearance.MinOverheadClearanceM)
                    {
                        under++;
                        if (first == null) first = id + " under a deck at (" + p.X.ToString("0.0", CultureInfo.InvariantCulture) + ", " + p.Z.ToString("0.0", CultureInfo.InvariantCulture) + ")";
                    }
                }
            }
            TestContext.WriteLine("{0} tiles with structures, {1} parked vehicles, {2} off the drawn surface, {3} under a low deck; first {4}",
                                  tiles, vehicles, off, under, first);
            Assert.That(tiles, Is.GreaterThan(0));
            Assert.That(vehicles, Is.GreaterThan(1000));
            Assert.That(off, Is.EqualTo(0), "parked vehicles stand on the drawn surface");
            Assert.That(under, Is.EqualTo(0), "nobody parks under a deck lower than a lorry");
        }

        /// <summary>Parked bikes, cycles and cars along the lanes of Thamel and Asan never overlap one another.</summary>
        [Test]
        public void ParkedVehiclesDoNotOverlap()
        {
            MeshingSettings s = StreamingScene.Settings(1, 10, false);
            int vehicles = 0, overlaps = 0;
            string first = null;
            foreach (var (name, lon, lat) in new[] { ("thamel", ThamelLon, ThamelLat), ("asan", AsanLon, AsanLat) })
            {
                TileId id = StreamingScene.TileAt(lon, lat);
                TileData t = StreamingSampleRegion.Tile(id);
                TileHeightSampler h = TileHeightSampler.ForArea(t, id, StreamingConfig.ForTier(1).TerrainStep(id, id, t.HeightsN));
                var parked = new List<ParkedVehicle>();
                ParkedPlacement.Place(t, h, parked, s.Roads);
                vehicles += parked.Count;
                for (int i = 0; i < parked.Count; i++)
                    for (int j = i + 1; j < parked.Count; j++)
                    {
                        ParkedVehicle a = parked[i], b = parked[j];
                        float dx = a.X - b.X, dz = a.Z - b.Z;
                        if (dx * dx + dz * dz > 36f || !ParkedPlacement.Overlap(a, b)) continue;
                        overlaps++;
                        if (first == null) first = name + " (" + a.X + ", " + a.Z + ") and (" + b.X + ", " + b.Z + ")";
                    }
            }
            TestContext.WriteLine("{0} parked vehicles, {1} overlapping pairs; first {2}", vehicles, overlaps, first);
            Assert.That(vehicles, Is.GreaterThan(500));
            Assert.That(overlaps, Is.EqualTo(0));
        }

        /// <summary>Bridge and trench pieces pick their LOD by the distance to the structure along its centreline, not to a
        /// circle round its footprint, and every trench is a piece of its own.</summary>
        [Test]
        public void BridgePiecesMeasureTheirDistanceAlongTheStructure()
        {
            MeshingSettings s = StreamingScene.Settings(2, 10, false);
            s.DrawBuildings = false;
            s.DrawInstances = false;
            int spans = 0, trenches = 0, long_ = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || t.Roads.Count == 0 || !t.HasRoadStructures) continue;
                if (BridgeLayout.For(t).Spans.Count == 0) continue;
                TileBuild b = StreamingScene.Build(id, s);
                BridgeLayout layout = BridgeLayout.For(b.Source); // the build's own decode
                int pieceTrenches = 0;
                var seen = new HashSet<BridgeSpan>();
                foreach (LodPiece p in b.Pieces)
                {
                    if (p.Layer != TileLayers.Bridges) continue;
                    if (p.Span != null) Assert.That(seen.Add(p.Span), Is.True, id + ": a structure in two pieces");
                    if (p.Span == null) continue; // a stair span: measured by its footprint circle
                    bool trench = layout.Trenches.Contains(p.Span);
                    if (trench) pieceTrenches++;
                    else spans++;
                    BridgePath path = p.Span.Path;
                    int k = p.Span.Count / 2;
                    // On a span's centreline the camera is on the structure (a trench draws walls only where the road is
                    // cut, so its middle station may lie beside its footprint).
                    if (!trench)
                        Assert.That(p.DistanceTo(path.X[k], path.Z[k]), Is.LessThan(0.5),
                                    id + " span of road " + p.Span.Road + " (" + p.Span.Kind + (p.Span.Foot ? ", foot" : "") + (p.Span.Stair ? ", stair" : "") +
                                    ", " + p.Span.Count + " stations, mask " + p.LodMask + ") at (" + path.X[k] + ", " + path.Z[k] + "), footprint (" + p.CX + ", " + p.CZ +
                                    ") r " + p.RadiusM + ": on the structure");
                    // 150 m to the side of the middle station: about 150 m away, whatever the span's length.
                    double ox = path.X[k] + path.Nx[k] * 150, oz = path.Z[k] + path.Nz[k] * 150;
                    double d = p.DistanceTo(ox, oz), centreline = BridgeMesher.Distance(p.Span, ox, oz);
                    Assert.That(d, Is.LessThanOrEqualTo(centreline + 1e-9));
                    Assert.That(d, Is.GreaterThanOrEqualTo(centreline - p.SpanHalfM - 1e-9));
                    double circle = Math.Max(0.0, Math.Sqrt((ox - p.CX) * (ox - p.CX) + (oz - p.CZ) * (oz - p.CZ)) - p.RadiusM);
                    if (p.RadiusM > 60 && centreline > 120)
                    {
                        long_++;
                        Assert.That(d, Is.GreaterThan(circle + 20), "a long span is not near everywhere in its circle");
                        Assert.That(PieceLod.Choose(p, 2, d, -1), Is.Not.EqualTo(0), "no LOD0 150 m beside a long span");
                    }
                }
                // One piece per trench that draws walls (a trench whose road is nowhere cut 0.5 m deep draws none).
                Assert.That(pieceTrenches, Is.LessThanOrEqualTo(layout.Trenches.Count), id + ": one piece per trench");
                trenches += pieceTrenches;
            }
            TestContext.WriteLine("{0} span pieces ({1} long), {2} trench pieces", spans, long_, trenches);
            Assert.That(spans, Is.GreaterThan(0));
            Assert.That(long_, Is.GreaterThan(0), "the sample has spans over 120 m to check");
        }

        /// <summary>No tree stands on a hero's plinth, walls, stairs or statue (Taleju's stepped plinth in Hanuman Dhoka
        /// had a mapped 24 m tree on it): mapped trees are moved beside the structure, generated ones dropped.</summary>
        [Test]
        public void TreesKeepOffTheHeroPlinths()
        {
            MeshingSettings s = StreamingScene.Settings(1, 10);
            int checkedTrees = 0, heroTiles = 0;
            foreach (var (name, lon, lat) in new[] { ("basantapur", BasantapurLon, BasantapurLat), ("asan", AsanLon, AsanLat) })
            {
                TileId id = StreamingScene.TileAt(lon, lat);
                TileBuild b = StreamingScene.Build(id, s);
                Assert.That(b.Error, Is.Null);
                if (b.HeroColliders == null) continue;
                heroTiles++;
                foreach (TreeInstance t in b.Instances.Trees)
                {
                    checkedTrees++;
                    foreach (OrientedBox box in b.HeroColliders.Boxes)
                    {
                        if (box.Top <= t.Y + 0.3f) continue;
                        double dx = t.X - box.CX, dz = t.Z - box.CZ, cs = Math.Cos(box.YawRad), sn = Math.Sin(box.YawRad);
                        double lx = dx * cs + dz * sn, lz = -dx * sn + dz * cs;
                        Assert.That(Math.Abs(lx) >= box.HalfX || Math.Abs(lz) >= box.HalfZ, Is.True,
                                    name + ": " + t.Species + " (" + t.Origin + ") at (" + t.X + ", " + t.Z + ") stands on a hero structure");
                    }
                }
            }
            Assert.That(heroTiles, Is.GreaterThan(0));
            TestContext.WriteLine("{0} trees checked against the hero structures", checkedTrees);
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

            /// <summary>With <see cref="Hero"/>: the hero whose id contains this (else the nearest).</summary>
            public string HeroId;
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
                // Street level: a rider's eye (1.8-2.2 m) on a drawn carriageway, looking down the street, with a clear
                // line of sight (no house between the eye and the target, the eye outside every footprint).
                new Shot { Name = "thamel", Lon = ThamelLon, Lat = ThamelLat, DistM = 45, EyeH = 1.9f, LookH = 2.2f, Street = true },
                new Shot { Name = "asan", Lon = AsanLon, Lat = AsanLat, DistM = 40, EyeH = 1.9f, LookH = 2.2f, Street = true },
                new Shot { Name = "basantapur", Lon = BasantapurLon, Lat = BasantapurLat, DistM = 60, EyeH = 1.8f, LookH = 9f, Free = true, Hero = true, HeroId = "taleju" },
                new Shot { Name = "basantapur_square", Lon = 85.30705, Lat = 27.70398, DistM = 40, EyeH = 1.8f, LookH = 3f, Street = true },
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
                if (shot.Hero) HeroTarget(b, shot.HeroId, ref tx, ref tz);
                double ex, ez;
                if (shot.Free) FreeEye(b, tx, tz, shot.DistM, out ex, out ez);
                else if (shot.DistM > 0 && shot.Street) EyeAlongRoad(b, ref tx, ref tz, shot.DistM, out ex, out ez);
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

        /// <summary>The eye on a circle round the target whose sight line crosses the fewest house walls and hero
        /// structures (inside no house, off every plinth), preferring points on a drawn road or open paving.</summary>
        private static void FreeEye(TileBuild b, double x, double z, double dist, out double ex, out double ez)
        {
            TileData t = b.Source;
            IRoadCorridorQuery q = RoadCorridorIndex.ForTile(t);
            var mask = new FootprintGrid(t);
            double best = double.MaxValue;
            ex = x + dist;
            ez = z;
            for (int a = 0; a < 360; a += 5)
            {
                double r = a * Math.PI / 180, cx = x + dist * Math.Sin(r), cz = z + dist * Math.Cos(r);
                if (mask.Inside(cx, cz) || OnHeroStructure(b, cx, cz, 1.0)) continue;
                int walls = 0;
                foreach (BuildingRecord br in t.Buildings)
                {
                    if (br.Rings == null || br.Rings.Length == 0) continue;
                    int[] ring = br.Rings[0];
                    int n = ring.Length / 2;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                        if (Crosses(cx, cz, x, z, ring[2 * i] / 100.0, ring[2 * i + 1] / 100.0, ring[2 * j] / 100.0, ring[2 * j + 1] / 100.0)) walls++;
                }
                // Hero structures between the eye and the target (other than the target's own) block the view too.
                int heroes = 0;
                for (int k = 1; k < 20; k++)
                {
                    double f = k / 20.0 * 0.7;
                    if (OnHeroStructure(b, cx + (x - cx) * f, cz + (z - cz) * f, 0.0)) heroes++;
                }
                double score = walls + heroes * 0.5 + (q.SignedDistance(t.Tile.X0 + cx, t.Tile.Z0 + cz) < 0 ? 0 : 0.25);
                if (score < best)
                {
                    best = score;
                    ex = cx;
                    ez = cz;
                }
            }
        }

        /// <summary>True when tile-local (x, z) is within <paramref name="margin"/> of a hero collider box or cylinder that
        /// stands more than 0.5 m high (a plinth, wall or statue).</summary>
        private static bool OnHeroStructure(TileBuild b, double x, double z, double margin)
        {
            if (b.HeroColliders == null) return false;
            foreach (OrientedBox box in b.HeroColliders.Boxes)
            {
                if (box.Top - box.Bottom < 0.5f) continue;
                double dx = x - box.CX, dz = z - box.CZ, cs = Math.Cos(box.YawRad), sn = Math.Sin(box.YawRad);
                double lx = dx * cs + dz * sn, lz = -dx * sn + dz * cs;
                if (Math.Abs(lx) < box.HalfX + margin && Math.Abs(lz) < box.HalfZ + margin) return true;
            }
            foreach (ColliderCylinder c in b.HeroColliders.Cylinders)
            {
                double dx = x - c.CX, dz = z - c.CZ, r = c.Radius + margin;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        private static bool Crosses(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            double d1 = (bx - ax) * (cz - az) - (bz - az) * (cx - ax), d2 = (bx - ax) * (dz - az) - (bz - az) * (dx - ax);
            double d3 = (dx - cx) * (az - cz) - (dz - cz) * (ax - cx), d4 = (dx - cx) * (bz - cz) - (dz - cz) * (bx - cx);
            return (d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0);
        }

        /// <summary>The centre of the hero replica whose id contains <paramref name="id"/>, else the one nearest the place.</summary>
        private static void HeroTarget(TileBuild b, string id, ref double x, ref double z)
        {
            double best = double.MaxValue, bx = x, bz = z;
            foreach (HeroPiece h in b.Heroes)
            {
                double d = (h.CX - x) * (h.CX - x) + (h.CZ - z) * (h.CZ - z);
                if (id != null && h.Id != null && h.Id.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0) d = -1;
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

        /// <summary>
        /// A street-level view down a drawn road near the target: candidate targets are the centreline stations of every
        /// drawn road within 35 m of the place, the eye <paramref name="dist"/> back (or forward) along the same
        /// centreline. The eye must stand well inside the drawn corridor (houses are trimmed to it, so it is outside every
        /// footprint) and off every hero structure; the sight line is sampled every metre and each sample inside a house
        /// footprint or a hero structure costs, as does each sample off the corridor (the view cuts a corner). The
        /// clearest candidate wins, ties to the one nearest the place; the target moves onto its centreline.
        /// </summary>
        private static void EyeAlongRoad(TileBuild b, ref double x, ref double z, double dist, out double ex, out double ez)
        {
            TileData t = b.Source;
            RoadLayout layout = RoadLayout.For(t);
            IRoadCorridorQuery q = RoadCorridorIndex.ForTile(t);
            var mask = new FootprintGrid(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            ex = x + dist;
            ez = z;
            double best = double.MaxValue, bx = x, bz = z;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadCentreline cl = layout.Centres[i];
                if (cl == null || cl.Count < 2 || !RoadMesher.IsDrawn(t, i, new RoadOptions())) continue;
                for (int k = 0; k < cl.Count; k++)
                {
                    double near = Math.Sqrt((cl.X[k] - x) * (cl.X[k] - x) + (cl.Z[k] - z) * (cl.Z[k] - z));
                    if (near > 35) continue;
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        double want = cl.S[k] + dir * dist;
                        if (want < cl.S[0] || want > cl.S[cl.Count - 1]) continue;
                        double cx = cl.X[0], cz = cl.Z[0];
                        for (int j = 0; j + 1 < cl.Count; j++)
                        {
                            if (want < cl.S[j] || want > cl.S[j + 1]) continue;
                            double f = cl.S[j + 1] > cl.S[j] ? (want - cl.S[j]) / (cl.S[j + 1] - cl.S[j]) : 0;
                            cx = cl.X[j] + (cl.X[j + 1] - cl.X[j]) * f;
                            cz = cl.Z[j] + (cl.Z[j + 1] - cl.Z[j]) * f;
                            break;
                        }
                        if (q.SignedDistance(x0 + cx, z0 + cz) > -0.8 || mask.Inside(cx, cz) || OnHeroStructure(b, cx, cz, 1.0)) continue;
                        double tx = cl.X[k], tz = cl.Z[k], len = Math.Sqrt((tx - cx) * (tx - cx) + (tz - cz) * (tz - cz));
                        int n = Math.Max(2, (int)len), blocked = 0, offRoad = 0;
                        for (int m = 1; m < n; m++)
                        {
                            double f = (double)m / n, px = cx + (tx - cx) * f, pz = cz + (tz - cz) * f;
                            if (mask.Inside(px, pz) || OnHeroStructure(b, px, pz, 0.0)) blocked++;
                            else if (q.SignedDistance(x0 + px, z0 + pz) > 0) offRoad++;
                        }
                        // A street a rider uses (a motor road) over a footway or path beside it.
                        double score = blocked * 10 + offRoad + near / 10 + (RoadWidthModel.IsMotor(t.Roads[i].RoadClass) ? 0 : 5);
                        if (score >= best) continue;
                        best = score;
                        ex = cx;
                        ez = cz;
                        bx = tx;
                        bz = tz;
                    }
                }
            }
            x = bx;
            z = bz;
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
