using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Life;
using Ghumante.World.Sacred;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The B0 band as the game draws it (World/Buildings B0CellSet, the engine-free core of DetailCells), measured
    /// honestly on the sample region: every shown cell counted whole with the selection DetailCells uses, per tier
    /// (Low included) at Asan, the densest chowk, and Thamel; each house drawn once at one level; the cell cache budgeted
    /// in bytes; uploads chunked within the frame's byte budget; builds sliced; collider sets per cell; Low tile builds
    /// without the hero LOD0 it never shows; and the resident meshes (tiles, the B0 cache, the vehicle caches) within the
    /// tier's mesh memory (W2_DESIGN 10.4: 100 / 180 / 250 MB).
    /// </summary>
    public class MeshingBuildingB0CellTests
    {
        private static readonly string[] TierName = { "Low", "Mid", "High" };

        private struct Place
        {
            public string Name;
            public double Lon, Lat;
        }

        private static readonly Place Asan = new Place { Name = "Asan", Lon = 85.3122, Lat = 27.7074 };
        private static readonly Place Thamel = new Place { Name = "Thamel", Lon = 85.3103, Lat = 27.7152 };
        private static readonly Place Basantapur = new Place { Name = "Basantapur", Lon = 85.3070, Lat = 27.7043 };

        // ------------------------------------------------------------------------------------------------------------
        // Bench: B0CellSet with a recording sink, inline builds and a frame clock

        /// <summary>Records the chunks B0CellSet uploads and which are shown.</summary>
        internal sealed class Sink : IB0CellSink
        {
            public sealed class Chunk
            {
                public TileId Tile;
                public int Tris, Vertices;
                public long Bytes;
                public bool Shown, ShortIndices;
                public double MinX, MinZ, MaxX, MaxZ;
            }

            public readonly HashSet<Chunk> Live = new HashSet<Chunk>();
            public long Uploads, Destroys;

            public object Upload(B0Tile tile, MeshData data, in UploadChunk chunk, ushort[] shortIndices)
            {
                var c = new Chunk
                {
                    Tile = tile.Id, Tris = chunk.IndexCount / 3, Vertices = chunk.VertexCount, Bytes = chunk.Bytes(data.HasUv0),
                    ShortIndices = chunk.UsesShortIndices && shortIndices != null, MinX = tile.Id.X0 + chunk.MinX, MinZ = tile.Id.Z0 + chunk.MinZ,
                    MaxX = tile.Id.X0 + chunk.MaxX, MaxZ = tile.Id.Z0 + chunk.MaxZ,
                };
                Live.Add(c);
                Uploads++;
                return c;
            }

            public void SetShown(object handle, bool shown)
            {
                ((Chunk)handle).Shown = shown;
            }

            public void Destroy(object handle)
            {
                Assert.IsTrue(Live.Remove((Chunk)handle), "a chunk is destroyed once");
                Destroys++;
            }

            public int ShownTris
            {
                get
                {
                    int t = 0;
                    foreach (Chunk c in Live)
                        if (c.Shown) t += c.Tris;
                    return t;
                }
            }

            /// <summary>Most shown triangles inside a 70 degree horizontal view from (x, z), over 16 headings (each chunk
            /// counted whole when its bounds meet the view: what the GPU's culling submits).</summary>
            public int InViewTris(double x, double z)
            {
                int best = 0;
                for (int h = 0; h < 16; h++)
                {
                    double heading = h * Math.PI / 8, hx = Math.Sin(heading), hz = Math.Cos(heading), cos = Math.Cos(35 * Math.PI / 180);
                    int t = 0;
                    foreach (Chunk c in Live)
                        if (c.Shown && InView(c, x, z, hx, hz, cos)) t += c.Tris;
                    best = Math.Max(best, t);
                }
                return best;
            }

            /// <summary>Shown triangles inside a 70 degree horizontal view from (x, z) along (hx, hz) (unit).</summary>
            public int InViewTris(double x, double z, double hx, double hz)
            {
                double cos = Math.Cos(35 * Math.PI / 180);
                int t = 0;
                foreach (Chunk c in Live)
                    if (c.Shown && InView(c, x, z, hx, hz, cos)) t += c.Tris;
                return t;
            }

            private static bool InView(Chunk c, double x, double z, double hx, double hz, double cos)
            {
                if (x >= c.MinX && x <= c.MaxX && z >= c.MinZ && z <= c.MaxZ) return true;
                for (int i = 0; i <= 16; i++)
                {
                    double t = i / 16.0, ax = c.MinX + (c.MaxX - c.MinX) * t, az = c.MinZ + (c.MaxZ - c.MinZ) * t;
                    if (Sees(ax, c.MinZ) || Sees(ax, c.MaxZ) || Sees(c.MinX, az) || Sees(c.MaxX, az)) return true;
                }
                return false;

                bool Sees(double qx, double qz)
                {
                    double dx = qx - x, dz = qz - z, len = Math.Sqrt(dx * dx + dz * dz);
                    return len < 1e-6 || (dx * hx + dz * hz) / len >= cos;
                }
            }
        }

        /// <summary>The ground the cells register with, recording what they register.</summary>
        internal sealed class Ground : IStructureGround
        {
            public readonly TileGroundQuery Query;
            public readonly Dictionary<ulong, int> Sets = new Dictionary<ulong, int>();
            public int Registers, Unregisters;

            public Ground(RoadOptions roads)
            {
                Query = new TileGroundQuery(roads);
            }

            public void Register(ulong key, StructureColliders c)
            {
                Query.Register(key, c);
                Sets[key] = c.Count;
                Registers++;
            }

            public void Unregister(ulong key)
            {
                Query.Unregister(key);
                Sets.Remove(key);
                Unregisters++;
            }
        }

        internal sealed class Bench
        {
            public readonly BandConfig Bands;
            public readonly MeshingSettings Settings;
            public readonly Sink Sink = new Sink();
            public readonly Ground Ground;
            public readonly B0CellSet Set;
            public readonly Dictionary<TileId, B0Tile> Tiles = new Dictionary<TileId, B0Tile>();
            public double NowMs;
            public double TickMs = 33.3;
            public int Frames;
            public long MaxTickUpload;

            public Bench(int tier, Func<double> clock = null)
            {
                Bands = BandConfig.ForTier(tier);
                Settings = MeshingSettings.ForTier(tier, BiomePalette.DefaultMonth, Heroes);
                Ground = new Ground(Settings.Roads);
                Set = new B0CellSet(Bands, Settings.DetailBuildings, Sink, Ground, new InlineJobRunner(), clock ?? Clock);
            }

            private double Clock()
            {
                return NowMs;
            }

            /// <summary>The detail tiles around (x, z) that a streamer would show (3 × 3 level-10 tiles).</summary>
            public void AddTilesAround(double x, double z)
            {
                TileId c = TileId.At(10, x, z);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var id = new TileId(10, c.Tx + dx, c.Ty + dz);
                        if (Tiles.ContainsKey(id)) continue;
                        TileData t = StreamingSampleRegion.Tile(id);
                        if (t == null || !t.HasDetail) continue;
                        Tiles[id] = Set.Add(id, t, TileHeightSampler.ForArea(t, id, 1), null);
                    }
            }

            /// <summary>One frame: the cells' update, then their uploads within <paramref name="budgetBytes"/>.</summary>
            public long Frame(double x, double z, long budgetBytes = long.MaxValue)
            {
                Set.Update(x, z);
                long up = Set.Upload(budgetBytes, true);
                MaxTickUpload = Math.Max(MaxTickUpload, up);
                NowMs += TickMs;
                Frames++;
                return up;
            }

            /// <summary>Frames until nothing is left to build or upload for a camera at (x, z).</summary>
            public void Settle(double x, double z, long budgetBytes = long.MaxValue, int maxFrames = 20000)
            {
                for (int f = 0; f < maxFrames; f++)
                {
                    Frame(x, z, budgetBytes);
                    if (Set.Settled && f > 2) break;
                }
                Set.Update(x, z);
                Assert.IsTrue(Set.Settled, "B0 settled within " + maxFrames + " frames");
            }
        }

        private static HeroSet _heroes;
        private static readonly object HeroLock = new object();

        private static HeroSet Heroes
        {
            get
            {
                lock (HeroLock)
                {
                    if (_heroes == null)
                    {
                        CuratedDb db = CuratedDb.Read(File.ReadAllBytes(Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core",
                                                                                     "kathmandu_core.curated.ghcd")));
                        _heroes = HeroSet.From(db);
                    }
                    return _heroes;
                }
            }
        }

        private static void At(Place p, out double x, out double z)
        {
            WorldFrame.LonLatToGame(p.Lon, p.Lat, out x, out z);
        }

        private static readonly Dictionary<string, Bench> Settled = new Dictionary<string, Bench>();

        /// <summary>B0 settled around a place on a tier (cached: the tests below only read it).</summary>
        private static Bench SettledAt(int tier, Place p)
        {
            string key = tier + p.Name;
            lock (Settled)
            {
                Bench b;
                if (Settled.TryGetValue(key, out b)) return b;
                double x, z;
                At(p, out x, out z);
                b = new Bench(tier);
                b.AddTilesAround(x, z);
                b.Settle(x, z);
                Settled[key] = b;
                return b;
            }
        }

        /// <summary>The triangles and houses of every B0 cell shown at once at Asan on a tier, as the game draws them (the
        /// band budget test of MeshingBuildingGrammarTests reads the same measure).</summary>
        internal static void ShownAtAsan(int tier, out int tris, out int houses)
        {
            Bench b = SettledAt(tier, Asan);
            tris = b.Sink.ShownTris;
            houses = 0;
            foreach (B0Tile t in b.Tiles.Values)
                foreach (B0CellSet.Cell c in t.Cells.Values)
                    if (c.Shown && c.Live != null) houses += c.Live.Positions.Length;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Selection

        /// <summary>The cells are built only with the houses whose footprint, grown by <see cref="B0HouseIndex.OverhangM"/>,
        /// meets the band: so every B0 house must stay within that overhang (measured: at most 2.03 m around Asan).</summary>
        [Test]
        public void B0GeometryStaysWithinItsOverhang()
        {
            double ax, az;
            At(Asan, out ax, out az);
            TileId id = TileId.At(10, ax, az);
            TileData t = StreamingSampleRegion.Tile(id);
            var h = TileHeightSampler.ForArea(t, id, 1);
            BandConfig high = BandConfig.ForTier(2);
            MeshingSettings settings = MeshingSettings.ForTier(2, BiomePalette.DefaultMonth, Heroes);
            B0HouseIndex idx = B0HouseIndex.Prepare(t);
            var m = new MeshData();
            double worst = 0;
            int built = 0;
            foreach (int level in new[] { BandConfig.LevelNear, BandConfig.LevelFlat })
            {
                BuildingOptions o = high.B0Options(level, settings.DetailBuildings);
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    if (idx.CentroidDistance(i, ax - id.X0, az - id.Z0) > 90) continue;
                    m.Clear();
                    if (!BuildingDetailMesher.One(t, i, h, o, m, null) || m.VertexCount == 0) continue;
                    float x0, y0, z0, x1, y1, z1, bx0, bz0, bx1, bz1;
                    m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                    idx.HouseBounds(i, out bx0, out bz0, out bx1, out bz1);
                    worst = Math.Max(worst, Math.Max(Math.Max(bx0 + B0HouseIndex.OverhangM - x0, x1 - (bx1 - B0HouseIndex.OverhangM)),
                                                     Math.Max(bz0 + B0HouseIndex.OverhangM - z0, z1 - (bz1 - B0HouseIndex.OverhangM))));
                    built++;
                }
            }
            TestContext.WriteLine("{0} B0 houses within 90 m of Asan: geometry reaches {1:0.00} m beyond the footprint box (overhang {2} m)", built, worst,
                                  B0HouseIndex.OverhangM);
            Assert.Greater(built, 300);
            Assert.LessOrEqual(worst, B0HouseIndex.OverhangM, "B0 geometry stays within the selection's overhang");
        }

        /// <summary>
        /// Each house is drawn once, by one cell, at one level: the near level for the houses whose centroid lies within
        /// the near radius (Mid and High), the lite or flat level beyond, nothing richer than flat on Low; every house
        /// that only B0 draws (nearer than its fade into B1) is drawn; and the shown cells meet B0.
        /// </summary>
        [Test]
        public void EachHouseIsDrawnOnceAtItsLevel()
        {
            for (int tier = 0; tier <= 2; tier++)
            {
                Bench b = SettledAt(tier, Asan);
                double x, z;
                At(Asan, out x, out z);
                var seen = new HashSet<long>();
                int near = 0, lite = 0, flat = 0;
                foreach (B0Tile t in b.Tiles.Values)
                {
                    double cx = x - t.Id.X0, cz = z - t.Id.Z0;
                    foreach (B0CellSet.Cell c in t.Cells.Values)
                    {
                        if (!c.Shown) continue;
                        Assert.IsNotNull(c.Live);
                        int[] houses = t.Index.Houses(b.Bands.B0FineCells, c.X, c.Z);
                        for (int k = 0; k < c.Live.Positions.Length; k++)
                        {
                            int i = houses[c.Live.Positions[k]];
                            Assert.IsTrue(seen.Add((long)t.Id.Key * 65536 + i), "house " + i + " of " + t.Id + " drawn twice");
                            Assert.IsTrue(t.Index.Meets(i, b.Bands.B0OuterM, cx, cz, b.Bands.B0BuildPadM), "a shown house meets B0 and the build pad");
                            double d = t.Index.CentroidDistance(i, cx, cz);
                            int level = c.Live.Levels[k];
                            Assert.AreEqual(b.Bands.B0Level(d, t.Index.Street(i), -1), level, TierName[tier] + ": house " + i + " at " + d.ToString("0.0") + " m");
                            if (level == BandConfig.LevelNear) near++;
                            else if (level == BandConfig.LevelLite) lite++;
                            else flat++;
                        }
                    }
                    // Every house nearer than the fade into B1 is drawn.
                    int per = t.Index.PerSide(b.Bands.B0FineCells);
                    for (int iz = 0; iz < per; iz++)
                        for (int ix = 0; ix < per; ix++)
                            foreach (int i in t.Index.Houses(b.Bands.B0FineCells, ix, iz))
                                if (t.Index.Meets(i, b.Bands.B0OuterM, cx, cz, -BandConfig.FadeM))
                                    Assert.IsTrue(seen.Contains((long)t.Id.Key * 65536 + i), TierName[tier] + ": house " + i + " of " + t.Id + " only B0 draws is shown");
                }
                TestContext.WriteLine("{0} at Asan: {1} houses near, {2} lite, {3} flat", TierName[tier], near, lite, flat);
                if (tier == 0) Assert.AreEqual(0, near + lite, "Low draws its whole B0 radius at the flat level");
                else Assert.Greater(near, 5, TierName[tier] + ": the houses around the camera are drawn at the near level");
                Assert.Greater(near + lite + flat, 60);
            }
        }

        /// <summary>
        /// The honest B0 budget (the review's measure): every cell shown at once, counted whole, with the selection
        /// DetailCells uses, per tier including Low, at Asan and Thamel: 40% of it within the tier's B0 share
        /// (<see cref="BandConfig.B0ShownBudgetTris"/>). Before this pass the same measure gave 430 k / 837 k / 1,124 k at
        /// Asan (whole 64 m cells grown by 24 m, every house at its ring's level, near houses submitted twice).
        /// </summary>
        [Test]
        public void ShownCellsHoldTheB0BudgetAtAsanAndThamel()
        {
            foreach (Place p in new[] { Asan, Thamel })
                for (int tier = 0; tier <= 2; tier++)
                {
                    Bench b = SettledAt(tier, p);
                    double x, z;
                    At(p, out x, out z);
                    int shown = b.Sink.ShownTris, budget = BandConfig.B0ShownBudgetTris(tier);
                    Assert.AreEqual(b.Set.ShownTris, shown, "the set counts what its sink shows");
                    int inView = b.Sink.InViewTris(x, z);
                    TestContext.WriteLine("{0,-5} {1,-7}: {2} cells, {3} draws, {4} tris shown (x{5} = {6:0.0} k vs share {7:0.0} k; budget {8} k), {9} k in the worst 70 degree view; {10:0.0} MB",
                                          TierName[tier], p.Name, b.Set.ShownCount, b.Set.ShownChunks, shown, BandConfig.InViewShare, shown * BandConfig.InViewShare / 1000,
                                          BuildingBandTable.B0ShareTris(tier) / 1000.0, budget / 1000, inView / 1000, b.Set.ResidentBytes / 1048576.0);
                    Assert.Greater(shown, 0, TierName[tier] + " " + p.Name + ": B0 draws the street");
                    Assert.LessOrEqual(shown, budget, TierName[tier] + " " + p.Name + ": every shown B0 cell within the tier's budget");
                    if (tier > 0)
                        Assert.LessOrEqual(shown * BandConfig.InViewShare, BuildingBandTable.B0ShareTris(tier), TierName[tier] + " " + p.Name + ": B0 within its share");
                    Assert.LessOrEqual(b.Set.ShownChunks, tier == 0 ? 16 : tier == 1 ? 40 : 64, TierName[tier] + ": draw calls");
                }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Memory

        /// <summary>
        /// The cache is budgeted in bytes: settling at five places in turn on High, the resident cells never exceed
        /// <see cref="BandConfig.CellCacheBytes"/> beyond the cells needed now (never evicted), and with a 2 MB budget the
        /// set evicts down to what the camera needs.
        /// </summary>
        [Test]
        public void CellCacheIsBudgetedInBytes()
        {
            var b = new Bench(2);
            Place[] tour = { Thamel, Asan, Basantapur, Asan, Thamel };
            foreach (Place p in tour)
            {
                double x, z;
                At(p, out x, out z);
                b.AddTilesAround(x, z);
                b.Settle(x, z);
                long needed = 0;
                foreach (B0Tile t in b.Tiles.Values)
                    foreach (B0CellSet.Cell c in t.Cells.Values)
                        if (c.Shown && c.Live != null) needed += c.Live.UploadedBytes;
                TestContext.WriteLine("High after {0}: {1:0.0} MB resident ({2:0.0} MB shown), cap {3} MB", p.Name, b.Set.ResidentBytes / 1048576.0, needed / 1048576.0,
                                      b.Bands.CellCacheBytes >> 20);
                Assert.LessOrEqual(b.Set.ResidentBytes, Math.Max(b.Bands.CellCacheBytes, needed));
                long live = 0;
                foreach (Sink.Chunk c in b.Sink.Live) live += c.Bytes;
                Assert.AreEqual(live, b.Set.ResidentBytes, "resident bytes are the live chunks'");
            }

            var small = new Bench(1);
            small.Bands.CellCacheBytes = 2L << 20;
            double ax, az, tx, tz;
            At(Asan, out ax, out az);
            At(Thamel, out tx, out tz);
            small.AddTilesAround(ax, az);
            small.AddTilesAround(tx, tz);
            small.Settle(ax, az);
            small.Settle(tx, tz);
            long shownBytes = 0;
            foreach (Sink.Chunk c in small.Sink.Live)
                if (c.Shown) shownBytes += c.Bytes;
            Assert.LessOrEqual(small.Set.ResidentBytes, Math.Max(small.Bands.CellCacheBytes, shownBytes), "evicted down to what is needed");
            Assert.Greater(small.Sink.Destroys, 0, "Asan's cells were evicted for Thamel's");
        }

        /// <summary>
        /// Resident meshes per tier (W2_DESIGN 10.4: 100 / 180 / 250 MB): the whole tile selection at Asan (and at
        /// Basantapur, the heaviest for heroes) as TileBuild builds it, plus the full B0 cache budget, plus the vehicle
        /// caches the traffic presenter warms (LOD1, LOD2, block-out and box bodies of every model and livery, the wheels,
        /// and the plated LOD0 bodies of the tier's LOD0 vehicles), within the budget. The shown B0 cells fit the cache.
        /// </summary>
        [Test]
        public void ResidentMeshesFitTheTierBudget()
        {
            long vehicles0 = VehicleCacheBytes(0), vehicles = VehicleCacheBytes(2);
            long[] budget = { 100L << 20, 180L << 20, 250L << 20 };
            for (int tier = 0; tier <= 2; tier++)
            {
                foreach (Place p in tier == 2 ? new[] { Asan, Basantapur } : new[] { Asan })
                {
                    long tiles = TileBytes(tier, p);
                    BandConfig bands = BandConfig.ForTier(tier);
                    long veh = tier == 0 ? vehicles0 : VehicleCacheBytes(tier);
                    long total = tiles + bands.CellCacheBytes + veh;
                    Bench b = SettledAt(tier, p.Name == "Asan" ? Asan : Basantapur);
                    TestContext.WriteLine("{0,-5} {1,-10}: tiles {2:0.0} MB + B0 cache {3} MB (shown {4:0.0}) + vehicles {5:0.0} MB = {6:0.0} MB of {7} MB",
                                          TierName[tier], p.Name, tiles / 1048576.0, bands.CellCacheBytes >> 20, b.Set.ResidentBytes / 1048576.0, veh / 1048576.0,
                                          total / 1048576.0, budget[tier] >> 20);
                    Assert.LessOrEqual(b.Set.ResidentBytes, bands.CellCacheBytes, TierName[tier] + ": the shown cells fit the B0 cache");
                    Assert.LessOrEqual(total, budget[tier], TierName[tier] + " " + p.Name + ": resident meshes");
                }
            }
            Assert.Greater(vehicles, 0);
        }

        /// <summary>GPU bytes of every node the selector picks around a place, built as the streamer builds them.</summary>
        private static long TileBytes(int tier, Place p)
        {
            double x, z;
            At(p, out x, out z);
            StreamingConfig cfg = StreamingConfig.ForTier(tier);
            MeshingSettings settings = MeshingSettings.ForTier(tier, BiomePalette.DefaultMonth, Heroes);
            var selector = TileSelector.ForPack(cfg, StreamingSampleRegion.Pack);
            var nodes = new List<SelectedNode>();
            selector.Select(x, z, nodes);
            long bytes = 0;
            object gate = new object();
            Parallel.For(0, nodes.Count, new ParallelOptions { MaxDegreeOfParallelism = 2 }, i =>
            {
                var b = new TileBuild { Node = nodes[i] };
                TileBuild.Execute(b, StreamingSampleRegion.Pack, cfg, settings, null);
                long sum = 0;
                for (int c = 0; c < b.Chunks.Count; c++) sum += b.ChunkBytes(c);
                lock (gate) bytes += sum;
            });
            return bytes;
        }

        /// <summary>GPU bytes of the vehicle bodies and wheels the traffic presenter keeps (VehicleMeshCache.Prewarm and
        /// WarmStep: LOD2, block-out and box prewarmed, LOD1 warmed in the background, wheels at LOD0 and LOD1) plus the
        /// plated LOD0 bodies of the tier's LOD0 vehicles and the player's.</summary>
        private static long VehicleCacheBytes(int tier)
        {
            var m = new MeshData(4096, 12288);
            long bytes = 0, lod0 = 0;
            foreach (VehicleLod lod in new[] { VehicleLod.Lod0, VehicleLod.Lod1, VehicleLod.Lod2, VehicleLod.Block, VehicleLod.Box })
                for (int v = 0; v < VehicleCatalog.Count; v++)
                {
                    VehicleCatalogEntry e = VehicleCatalog.At(v);
                    bool carries = e.Shape == BodyShape.Scooter || e.Shape == BodyShape.Motorbike || e.Shape == BodyShape.Cruiser || e.Shape == BodyShape.Bicycle ||
                                   e.Shape == BodyShape.Rickshaw;
                    int models = lod >= VehicleLod.Block ? 1 : VehicleMesher.ModelCount(v);
                    for (int k = 0; k < models; k++)
                        for (int l = 0; l < e.LiveryCount; l++)
                        {
                            bool rider = lod == VehicleLod.Lod1 && carries;
                            m.Clear();
                            VehicleMesher.Build(v, (byte)l, (byte)k, lod, m, 0u, rider);
                            long one = MeshBytes(m);
                            if (lod == VehicleLod.Lod0) lod0 = Math.Max(lod0, one);
                            else bytes += one;
                            if (lod == VehicleLod.Lod2 && carries)
                            {
                                m.Clear();
                                VehicleMesher.Build(v, (byte)l, (byte)k, lod, m, 0u, true);
                                bytes += MeshBytes(m);
                            }
                        }
                }
            var wheels = new HashSet<long>();
            foreach (VehicleLod lod in new[] { VehicleLod.Lod0, VehicleLod.Lod1 })
                for (int v = 0; v < VehicleCatalog.Count; v++)
                    for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                        foreach (WheelSocket s in VehicleMesher.Wheels(VehicleCatalog.At(v), k))
                        {
                            long key = ((long)s.Style << 40) | ((long)Math.Round(s.Radius * 100) << 24) | ((long)Math.Round(s.Width * 100) << 8) | (long)lod;
                            if (!wheels.Add(key)) continue;
                            m.Clear();
                            VehicleMesher.BuildWheel(s, lod, m);
                            bytes += MeshBytes(m);
                        }
            int lod0Count = LifeLod.ForTier(tier).VehicleCaps[0] + 1;
            return bytes + lod0 * lod0Count;
        }

        private static long MeshBytes(MeshData m)
        {
            return (long)m.VertexCount * (m.HasUv0 ? 36 : 28) + (long)m.IndexCount * (m.VertexCount <= 65535 ? 2 : 4);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Uploads, slices, colliders, rebuilds

        /// <summary>Cells reach the GPU in chunks of at most <see cref="B0CellSet.ChunkVertices"/> vertices with 16-bit
        /// indices, never more bytes in a frame than the frame's budget allows (one chunk at least).</summary>
        [Test]
        public void UploadsAreChunkedWithinTheFrameBudget()
        {
            const long budget = 512 * 1024;
            var b = new Bench(2);
            double x, z;
            At(Asan, out x, out z);
            b.AddTilesAround(x, z);
            long largest = 0;
            for (int f = 0; f < 20000; f++)
            {
                long up = b.Frame(x, z, budget);
                largest = Math.Max(largest, up);
                if (b.Set.Settled && f > 2) break;
            }
            foreach (Sink.Chunk c in b.Sink.Live)
            {
                Assert.LessOrEqual(c.Vertices, B0CellSet.ChunkVertices);
                Assert.IsTrue(c.ShortIndices, "16-bit indices");
                Assert.LessOrEqual(c.Bytes, budget, "a B0 chunk fits half the 1 MB frame cap");
            }
            TestContext.WriteLine("High at Asan: {0} chunks, most in one frame {1:0.00} MB (budget {2:0.00} MB), {3} frames", b.Sink.Live.Count, largest / 1048576.0,
                                  budget / 1048576.0, b.Frames);
            Assert.LessOrEqual(largest, budget, "no frame uploads more than its budget");
            Assert.Greater(b.Set.ShownTris, 0);
        }

        /// <summary>A build slice stops once its share of the frame has passed (<see cref="B0CellSet.WorkerDuty"/> of the
        /// frame time, at least <see cref="B0CellSet.SliceMs"/>; after a house at least), and the next waits three times as
        /// long: the worker is busy at most a quarter of the time.</summary>
        [Test]
        public void BuildsRunInSlicesOfAQuarterFrame()
        {
            double now = 0;
            int calls = 0;
            Bench b = null;
            b = new Bench(1, () =>
            {
                calls++;
                now += 0.5; // each clock read inside a slice is half a millisecond of work
                return now + b.NowMs;
            });
            double x, z;
            At(Asan, out x, out z);
            b.AddTilesAround(x, z);
            b.Settle(x, z);
            Assert.Greater(b.Set.Slices, b.Set.Builds, "dense cells take several slices");
            double perSlice = b.Set.WorkMs / b.Set.Slices, elapsed = now + b.NowMs;
            TestContext.WriteLine("Mid at Asan: {0} builds in {1} slices, {2:0.0} ms per slice, worker busy {3:0.0}% of {4:0} ms", b.Set.Builds, b.Set.Slices,
                                  perSlice, 100 * b.Set.WorkMs / elapsed, elapsed);
            Assert.LessOrEqual(perSlice, B0CellSet.WorkerDuty * b.TickMs + 1.0, "a quarter of a 30 fps frame (and one house)");
            Assert.LessOrEqual(b.Set.WorkMs, B0CellSet.WorkerDuty * elapsed + 50, "the worker is busy a quarter of the time at most");
        }

        /// <summary>
        /// Each shown cell registers its own collider set under the quadtree tile it coincides with (level 15 for the
        /// 32 m cells of a level-10 tile); moving the camera re-registers only the cells that changed, never a tile's set.
        /// </summary>
        [Test]
        public void CellTogglesRegisterOnlyTheirOwnColliders()
        {
            var b = new Bench(1);
            double x, z;
            At(Asan, out x, out z);
            b.AddTilesAround(x, z);
            b.Settle(x, z);
            Assert.Greater(b.Ground.Sets.Count, 5, "shown cells register colliders");
            foreach (KeyValuePair<ulong, int> kv in b.Ground.Sets)
            {
                TileId id = TileId.FromKey(kv.Key);
                Assert.AreEqual(15, id.Level, "a 32 m cell's set is its level-15 tile");
                Assert.Less(kv.Value, 400, "a cell's set is small");
            }
            Assert.AreEqual(b.Ground.Sets.Count, b.Ground.Query.StructureTileCount);
            int registers = b.Ground.Registers, cells = b.Set.ShownCount;
            // Walk 40 m east: some cells show, hide or are rebuilt; the others are untouched.
            for (int i = 1; i <= 40; i++) b.Frame(x + i, z);
            b.Settle(x + 40, z);
            int moved = b.Ground.Registers - registers;
            TestContext.WriteLine("Mid at Asan: {0} cells with colliders; walking 40 m re-registered {1} cell sets (of {2} shown)", b.Ground.Sets.Count, moved, cells);
            Assert.Greater(moved, 0);
            foreach (ulong key in b.Ground.Sets.Keys) Assert.AreEqual(15, TileId.FromKey(key).Level);
        }

        /// <summary>A rebuilt cell copies its unchanged houses from its previous version, and the copy is exactly what
        /// building them again gives (the grammar is deterministic).</summary>
        [Test]
        public void RebuiltCellsCopyUnchangedHousesExactly()
        {
            var b = new Bench(1);
            double x, z;
            At(Asan, out x, out z);
            b.AddTilesAround(x, z);
            b.Settle(x, z);
            for (int i = 1; i <= 30; i++) b.Frame(x, z + i);
            b.Settle(x, z + 30);
            Assert.Greater(b.Set.HousesReused, 0, "rebuilt cells reuse houses");
            int compared = 0;
            foreach (B0Tile t in b.Tiles.Values)
                foreach (B0CellSet.Cell c in t.Cells.Values)
                {
                    B0CellSet.Version v = c.Live;
                    if (v == null || v.Data == null || v.Positions.Length == 0 || compared >= 3) continue;
                    var fresh = new MeshData();
                    int[] houses = t.Index.Houses(b.Bands.B0FineCells, c.X, c.Z);
                    for (int k = 0; k < v.Positions.Length; k++)
                        BuildingDetailMesher.One(t.Source, houses[v.Positions[k]], t.Sampler, b.Bands.B0Options(v.Levels[k], b.Settings.DetailBuildings), fresh, null);
                    var chunks = new List<UploadChunk>();
                    ushort[] shorts = null;
                    TileBuild.SplitLayer(fresh, 0, B0CellSet.ChunkVertices, chunks, ref shorts);
                    Assert.AreEqual(fresh.VertexCount, v.Data.VertexCount);
                    Assert.AreEqual(fresh.IndexCount, v.Data.IndexCount);
                    for (int i = 0; i < fresh.VertexCount * 3; i++) Assert.AreEqual(fresh.Positions[i], v.Data.Positions[i]);
                    for (int i = 0; i < fresh.IndexCount; i++) Assert.AreEqual(fresh.Indices[i], v.Data.Indices[i]);
                    compared++;
                }
            Assert.Greater(compared, 0);
        }

        /// <summary>
        /// A ride through the old core at 10 m/s (Thamel chowk to Asan, 300 m): the cells keep up (a B1 block stands in
        /// for at most a few percent of the frames), and the rebuilds copy far more houses than they build.
        /// </summary>
        [Test]
        public void RideThroughTheOldCoreKeepsUp()
        {
            double x0, z0, x1, z1;
            At(Thamel, out x0, out z0);
            At(Asan, out x1, out z1);
            double len = Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)), ride = Math.Min(300, len);
            var b = new Bench(1);
            b.AddTilesAround(x0, z0);
            b.Settle(x0, z0);
            int built = b.Set.HousesBuilt, reused = b.Set.HousesReused, waiting = 0, frames = 0;
            for (double s = 0; s <= ride; s += 10.0 / 30)
            {
                double x = x0 + (x1 - x0) * s / len, z = z0 + (z1 - z0) * s / len;
                b.AddTilesAround(x, z);
                b.Frame(x, z, 1L << 20);
                if (b.Set.BlocksWaiting > 0) waiting++;
                frames++;
            }
            int nb = b.Set.HousesBuilt - built, nr = b.Set.HousesReused - reused;
            TestContext.WriteLine("Mid ride {0:0} m: {1} frames, {2} with a B1 block standing in; houses built {3}, copied {4}; builds by reason {5}", ride, frames, waiting, nb, nr,
                                  string.Join("/", b.Set.BuildsByReason));
            Assert.LessOrEqual(waiting, frames / 10, "B0 keeps up with a 10 m/s ride");
            Assert.Greater(nr, nb, "rebuilds copy more houses than they build");
        }

        // ------------------------------------------------------------------------------------------------------------
        // Heroes on Low

        /// <summary>
        /// Low never shows hero LOD0 (<see cref="BandConfig.HeroLod0Allowed"/>), so its tile builds neither build nor upload
        /// it: on the Basantapur tile its heroes come with LOD1 to LOD3 only, the hero layer shrinks by the LOD0 meshes,
        /// and the colliders (taken from LOD1, whose generators emit the same set as LOD0) are those of Mid.
        /// </summary>
        [Test]
        public void LowTileBuildsSkipHeroLod0()
        {
            double x, z;
            At(Basantapur, out x, out z);
            TileId id = TileId.At(10, x, z);
            var builds = new TileBuild[3];
            for (int tier = 0; tier <= 1; tier++)
            {
                MeshingSettings s = MeshingSettings.ForTier(tier, BiomePalette.DefaultMonth, Heroes);
                s.DrawInstances = false;
                var b = new TileBuild { Node = new SelectedNode(id, id, true) };
                TileBuild.Execute(b, StreamingSampleRegion.Pack, StreamingConfig.ForTier(tier), s, null);
                builds[tier] = b;
            }
            TileBuild low = builds[0], mid = builds[1];
            Assert.AreEqual(mid.Heroes.Count, low.Heroes.Count);
            Assert.Greater(low.Heroes.Count, 8);
            foreach (HeroPiece p in low.Heroes)
            {
                Assert.AreEqual(0, p.Tris0, p.Id + ": no LOD0 on Low");
                Assert.Greater(p.Tris1, 0, p.Id);
            }
            foreach (PartRange r in low.Parts[TileLayers.Heroes]) Assert.AreNotEqual(0, r.Part % HeroLodBudget.LodCount, "no LOD0 part");
            long lowBytes = low.LayerBytes(TileLayers.Heroes), midBytes = mid.LayerBytes(TileLayers.Heroes);
            TestContext.WriteLine("Basantapur heroes: Low {0:0.0} MB, Mid {1:0.0} MB", lowBytes / 1048576.0, midBytes / 1048576.0);
            Assert.Less(lowBytes, midBytes / 2, "the LOD0 meshes are most of the hero layer");
            Assert.AreEqual(mid.HeroColliders.Boxes.Count, low.HeroColliders.Boxes.Count);
            Assert.AreEqual(mid.HeroColliders.Ramps.Count, low.HeroColliders.Ramps.Count);
            for (int i = 0; i < mid.HeroColliders.Boxes.Count; i++) Assert.AreEqual(mid.HeroColliders.Boxes[i], low.HeroColliders.Boxes[i]);
            for (int i = 0; i < mid.HeroColliders.Ramps.Count; i++) Assert.AreEqual(mid.HeroColliders.Ramps[i], low.HeroColliders.Ramps[i]);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Preview (GHUMANTE_PREVIEW_DIR): the street at Asan before and after, from the same eye

        /// <summary>
        /// Dumps the B0 houses around Asan on Mid and Low as the cells now draw them (each house once at its level) and as
        /// the old rings drew them (whole cells grown by 24 m, the near ring's cut at its radius emulated per triangle), in
        /// the Asan tile's frame with its terrain and roads and a street-level eye, for tools/mesh-preview.
        /// </summary>
        [Test]
        public void DumpAsanStreetBeforeAndAfter()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the street");
            double ax, az;
            At(Asan, out ax, out az);
            TileId home = TileId.At(10, ax, az);
            TileData src = StreamingSampleRegion.Tile(home);
            TileHeightSampler sampler = TileHeightSampler.ForArea(src, home, 1);
            var terrain = new MeshData();
            TerrainMesher.Build(src, home, new TerrainOptions { Step = 1 }, terrain);
            var roads = new MeshData();
            RoadMesher.Build(src, sampler, new RoadOptions(), roads);
            float lx = (float)(ax - home.X0), lz = (float)(az - home.Z0), gy;
            sampler.TryHeightClamped(ax, az, out gy);
            ObjDump.Write(ObjDump.Crop(terrain, lx - 160, lz - 160, lx + 160, lz + 160, true), "b0/terrain.obj");
            ObjDump.Write(ObjDump.Crop(roads, lx - 160, lz - 160, lx + 160, lz + 160, true), "b0/roads.obj");
            foreach (int tier in new[] { 0, 1 })
            {
                Bench b = SettledAt(tier, Asan);
                var after = new MeshData();
                foreach (B0Tile t in b.Tiles.Values)
                    foreach (B0CellSet.Cell c in t.Cells.Values)
                    {
                        if (!c.Shown) continue;
                        var m = new MeshData();
                        int[] houses = t.Index.Houses(b.Bands.B0FineCells, c.X, c.Z);
                        for (int k = 0; k < c.Live.Positions.Length; k++)
                            BuildingDetailMesher.One(t.Source, houses[c.Live.Positions[k]], t.Sampler, b.Bands.B0Options(c.Live.Levels[k], b.Settings.DetailBuildings), m, null);
                        ObjDump.Append(after, m, (float)(t.Id.X0 - home.X0), 0f, (float)(t.Id.Z0 - home.Z0));
                    }
                ObjDump.Write(after, "b0/" + TierName[tier] + "_after.obj");
                ObjDump.Write(OldRings(tier, ax, az, home, b.Settings), "b0/" + TierName[tier] + "_before.obj");
                TestContext.WriteLine("{0}: after {1} tris; eye {2:0.0},{3:0.0},{4:0.0}", TierName[tier], after.TriangleCount, lx, gy + 3, lz);
            }
        }

        /// <summary>The old DetailCells selection: near ring 32 m cells and lite ring 64 m cells grown by 24 m, every house
        /// of a cell at its ring's level; the dithered cut at the near radius emulated by keeping each ring's triangles on
        /// its side of it.</summary>
        private static MeshData OldRings(int tier, double ax, double az, TileId home, MeshingSettings settings)
        {
            float nearR = tier == 0 ? 10f : BuildingBandTable.NearOuterM(tier), outerR = BuildingBandTable.OuterM(tier, 0);
            BuildingOptions nearO = BuildingBandTable.NearOptions(tier, settings.DetailBuildings), liteO = BuildingBandTable.LiteOptions(tier, settings.DetailBuildings);
            var all = new MeshData();
            TileId c = TileId.At(10, ax, az);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var id = new TileId(10, c.Tx + dx, c.Ty + dz);
                    TileData t = StreamingSampleRegion.Tile(id);
                    if (t == null || !t.HasDetail) continue;
                    var h = TileHeightSampler.ForArea(t, id, 1);
                    double cx = ax - id.X0, cz = az - id.Z0;
                    foreach (bool near in new[] { true, false })
                    {
                        int sub = near ? BuildingBandTable.NearSubdivision : 1, per = BuildingDetailMesher.CellsPerSide(t, sub);
                        double cell = id.Size / per;
                        float inner = near ? 0f : nearR, outer = near ? nearR : outerR;
                        for (int iz = 0; iz < per; iz++)
                            for (int ix = 0; ix < per; ix++)
                            {
                                if (!BandConfig.Touches(ix * cell - 24, iz * cell - 24, (ix + 1) * cell + 24, (iz + 1) * cell + 24, cx, cz, inner, outer)) continue;
                                var m = new MeshData();
                                BuildingDetailMesher.BuildCell(t, h, ix, iz, sub, near ? nearO : liteO, m, null);
                                var kept = new MeshData();
                                for (int q = 0; q + 2 < m.IndexCount; q += 3)
                                {
                                    double px = 0, pz = 0;
                                    for (int k = 0; k < 3; k++)
                                    {
                                        px += m.Positions[3 * m.Indices[q + k]] / 3.0;
                                        pz += m.Positions[3 * m.Indices[q + k] + 2] / 3.0;
                                    }
                                    double d = Math.Sqrt((px - cx) * (px - cx) + (pz - cz) * (pz - cz));
                                    if (d < inner || d > outer + BandConfig.FadeM * 0.5) continue;
                                    int b = kept.VertexCount;
                                    for (int k = 0; k < 3; k++)
                                    {
                                        int v = m.Indices[q + k];
                                        uint rgba = (uint)(m.Colors[4 * v] << 24 | m.Colors[4 * v + 1] << 16 | m.Colors[4 * v + 2] << 8 | m.Colors[4 * v + 3]);
                                        if (m.HasUv0)
                                            kept.AddVertex(m.Positions[3 * v], m.Positions[3 * v + 1], m.Positions[3 * v + 2], m.Normals[3 * v], m.Normals[3 * v + 1], m.Normals[3 * v + 2],
                                                           rgba, m.Uv0[2 * v], m.Uv0[2 * v + 1]);
                                        else
                                            kept.AddVertex(m.Positions[3 * v], m.Positions[3 * v + 1], m.Positions[3 * v + 2], m.Normals[3 * v], m.Normals[3 * v + 1], m.Normals[3 * v + 2], rgba);
                                    }
                                    kept.AddTriangle(b, b + 1, b + 2);
                                }
                                ObjDump.Append(all, kept, (float)(id.X0 - home.X0), 0f, (float)(id.Z0 - home.Z0));
                            }
                    }
                }
            return all;
        }
    }
}
