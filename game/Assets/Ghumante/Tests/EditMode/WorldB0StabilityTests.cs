using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Routing;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// Owner playtest #4 (docs/W2_PLAYTEST_1.md: "Buildings constantly flickers and stutters... Make then stable and
    /// solid") in the World assembly as Unity compiles it: the B0 cells (B0CellSet, the engine-free core of DetailCells)
    /// ridden from Thamel Chowk to Asan on the streets a scooter takes (A* on the sample routing graph) at 10 m/s, the
    /// bands centred by <see cref="BandAnchor"/> while a chase camera sways behind the rider, under the streamer's upload
    /// policy (<see cref="B0UploadShare"/>) with tiles streaming in every frame and taking all the room they are left
    /// (the worst case: B0 goes first only for a build the band needs now, or after three starved frames). Every frame:
    /// no cell toggles, no B1 block flips between standing in and handing over, no house drawn twice or by nobody, no
    /// half-uploaded version drawn, the colliders those of what is drawn, the band within its budget, B0 within half the
    /// upload cap; standing at Asan, nothing is rebuilt. The full
    /// before-and-after harness (with the old ring cells as the owner played them) is core-tests
    /// MeshingBuildingB0StabilityTests.
    /// </summary>
    public class WorldB0StabilityTests
    {
        private const double FrameMs = 1000.0 / 30.0;

        /// <summary>The streamer's per-frame upload cap (StreamingScheduler.UploadBudgetBytes), shared by tiles and B0.</summary>
        private const long CapBytes = 1L << 20;

        private static readonly string[] TierName = { "Low", "Mid", "High" };

        /// <summary>Records the chunks the cell set uploads, shows and destroys.</summary>
        private sealed class Sink : IB0CellSink
        {
            public sealed class Chunk
            {
                public int Tris;
                public bool Shown, Destroyed;
            }

            public readonly HashSet<Chunk> Live = new HashSet<Chunk>();

            /// <summary>Chunks and bytes uploaded since the bench's last frame began.</summary>
            public int FrameChunks;

            public long FrameBytes;

            public object Upload(B0Tile tile, MeshData data, in UploadChunk chunk, ushort[] shortIndices)
            {
                Assert.LessOrEqual(chunk.VertexCount, B0CellSet.ChunkVertices, "B0 chunks are small");
                var c = new Chunk { Tris = chunk.IndexCount / 3 };
                Live.Add(c);
                FrameChunks++;
                FrameBytes += chunk.Bytes(data.HasUv0);
                return c;
            }

            public void SetShown(object handle, bool shown)
            {
                var c = (Chunk)handle;
                Assert.IsFalse(c.Destroyed, "a destroyed chunk is never shown again");
                c.Shown = shown;
            }

            public void Destroy(object handle)
            {
                var c = (Chunk)handle;
                c.Destroyed = true;
                c.Shown = false;
                Live.Remove(c);
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
        }

        /// <summary>Records the collider sets registered per key (their collider counts).</summary>
        private sealed class Ground : IStructureGround
        {
            public readonly Dictionary<ulong, int> Sets = new Dictionary<ulong, int>();

            public void Register(ulong tileKey, StructureColliders c)
            {
                Sets[tileKey] = c.Count;
            }

            public void Unregister(ulong tileKey)
            {
                Assert.IsTrue(Sets.Remove(tileKey), "only registered sets are dropped");
            }
        }

        /// <summary>A polyline in game metres with its arc length.</summary>
        private sealed class Street
        {
            private readonly List<double> _x = new List<double>(), _z = new List<double>(), _s = new List<double>();

            public double Length
            {
                get { return _s.Count > 0 ? _s[_s.Count - 1] : 0; }
            }

            public void Add(double x, double z)
            {
                if (_x.Count > 0)
                {
                    double dx = x - _x[_x.Count - 1], dz = z - _z[_z.Count - 1], d = Math.Sqrt(dx * dx + dz * dz);
                    if (d < 0.01) return;
                    _s.Add(_s[_s.Count - 1] + d);
                }
                else _s.Add(0);
                _x.Add(x);
                _z.Add(z);
            }

            /// <summary>The point at arc length <paramref name="s"/> and the unit heading there.</summary>
            public void At(double s, out double x, out double z, out double hx, out double hz)
            {
                int i = 1;
                while (i < _s.Count - 1 && _s[i] < s) i++;
                double s0 = _s[i - 1], s1 = _s[i], t = s1 > s0 ? Math.Max(0, Math.Min(1, (s - s0) / (s1 - s0))) : 0;
                double dx = _x[i] - _x[i - 1], dz = _z[i] - _z[i - 1], d = Math.Sqrt(dx * dx + dz * dz);
                x = _x[i - 1] + dx * t;
                z = _z[i - 1] + dz * t;
                hx = d > 0 ? dx / d : 1;
                hz = d > 0 ? dz / d : 0;
            }
        }

        /// <summary>The scooter's route from Thamel Chowk to Asan on the sample routing graph (A*, motorbike profile).</summary>
        private static Street ThamelToAsan()
        {
            RouteGraph g = RouteGraphReader.Read(File.ReadAllBytes(SampleRegion.FilePath(".route.ghrg")));
            double tx, tz, ax, az;
            WorldFrame.LonLatToGame(85.3110, 27.7150, out tx, out tz); // Thamel Chowk
            WorldFrame.LonLatToGame(85.3122, 27.7074, out ax, out az); // Asan
            int src = new NearestNode(g, Travel.Motorbike, false).Find(tx, tz);
            int dst = new NearestNode(g, Travel.Motorbike, true).Find(ax, az);
            Route r = new AStar(g).FindRoute(src, dst, Travel.Motorbike);
            Assert.IsNotNull(r, "a scooter route from Thamel to Asan");
            var st = new Street();
            foreach (int e in r.Edges)
            {
                double[] p = g.EdgeGeometry(e);
                for (int i = 0; i < p.Length / 2; i++) st.Add(p[2 * i], p[2 * i + 1]);
            }
            return st;
        }

        /// <summary>The cell set with its tiles, sink, ground and frame clock.</summary>
        private sealed class Bench
        {
            public readonly int Tier;
            public readonly BandConfig Bands;
            public readonly Sink Sink = new Sink();
            public readonly Ground Ground = new Ground();
            public readonly B0CellSet Set;
            public readonly Dictionary<TileId, B0Tile> Tiles = new Dictionary<TileId, B0Tile>();
            public readonly B0UploadShare Share = new B0UploadShare();
            public double Now;

            /// <summary>True while tiles stream in, taking every byte and millisecond of the cap B0 leaves them.</summary>
            public bool TilesStream;

            public Bench(int tier)
            {
                Tier = tier;
                Bands = BandConfig.ForTier(tier);
                Set = new B0CellSet(Bands, MeshingSettings.ForTier(tier).DetailBuildings, Sink, Ground, new InlineJobRunner(), () => Now);
            }

            public void AddTilesAround(double x, double z)
            {
                TileId c = TileId.At(10, x, z);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var id = new TileId(10, c.Tx + dx, c.Ty + dz);
                        if (Tiles.ContainsKey(id) || !SampleRegion.Pack.Contains(id)) continue;
                        TileData t = SampleRegion.Pack.ReadTile(id);
                        if (t == null || !t.HasDetail) continue;
                        Tiles[id] = Set.Add(id, t, TileHeightSampler.ForArea(t, id, 1), null);
                    }
            }

            /// <summary>One frame as the streamer runs it (WorldStreamer.Tick, then UpdateView): B0 uploads first when a
            /// build the band needs now waits or it starved, the tiles take the rest of the cap (all of it while
            /// <see cref="TilesStream"/>), B0 what they leave; then the cells update.</summary>
            public void Frame(double x, double z)
            {
                Sink.FrameChunks = 0;
                Sink.FrameBytes = 0;
                bool waiting = Set.NextUploadBytes > 0;
                long first = Share.First(CapBytes, waiting, waiting && Set.UrgentUploadWaiting), b0 = 0;
                if (first > 0) b0 = Set.Upload(first, true);
                long tiles = TilesStream ? CapBytes - b0 : 0;
                long after = Share.After(CapBytes, b0 + tiles, waiting, first > 0, TilesStream ? 0.0 : 1.0);
                if (after > 0) b0 += Set.Upload(after, false);
                Share.EndFrame(waiting, b0);
                Assert.AreEqual(b0, Sink.FrameBytes, "Upload reports the bytes it uploaded");
                Set.Update(x, z);
                Now += FrameMs;
            }
        }

        /// <summary>What went wrong on a ride (all zero when the band is stable).</summary>
        private sealed class Faults
        {
            public int Frames, Toggles, BlockFlips, Doubles, Holes, HalfShown, ColliderMismatches, OverBudget, OverUpload, StopBuilds;
            public string First;

            public void Note(string what)
            {
                if (First == null) First = what;
            }

            public override string ToString()
            {
                return Frames + " frames: " + Toggles + " cell toggles, " + BlockFlips + " block flips, " + Doubles + " houses drawn twice, " + Holes + " houses drawn by nobody, " +
                       HalfShown + " half-uploaded versions drawn, " + ColliderMismatches + " collider mismatches, " + OverBudget + " frames over the B0 budget, " + OverUpload +
                       " frames over the upload share, " + StopBuilds + " rebuilds standing at Asan" + (First != null ? "; first: " + First : "");
            }
        }

        /// <summary>The upload policy WorldStreamer.Tick runs: B0 first only for a missing house or after three starved
        /// frames, otherwise after the tiles with what they leave.</summary>
        [Test]
        public void UploadShareGoesFirstOnlyForAMissingHouseOrWhenStarved()
        {
            var s = new B0UploadShare();
            Assert.AreEqual(CapBytes / 2, s.First(CapBytes, true, true));
            Assert.AreEqual(0, s.First(CapBytes, false, true), "nothing waiting");
            for (int f = 0; f < B0UploadShare.StarvedTicks; f++)
            {
                Assert.AreEqual(0, s.First(CapBytes, true, false));
                s.EndFrame(true, 0);
            }
            Assert.AreEqual(CapBytes / 2, s.First(CapBytes, true, false), "starved: first");
            Assert.AreEqual(CapBytes - 1000, s.After(CapBytes, 1000, true, false, 1.0), "after the tiles: what they left");
        }

        [Test]
        public void B0CellsStayStableRidingFromThamelToAsan()
        {
            Street st = ThamelToAsan();
            Assert.That(st.Length, Is.InRange(800.0, 2000.0), "the route through the old core");
            for (int tier = 0; tier <= 2; tier++)
            {
                Faults f = Ride(tier, st, 10.0);
                TestContext.WriteLine("{0,-4}: {1}", TierName[tier], f);
                string at = TierName[tier] + " (" + f + ")";
                Assert.AreEqual(0, f.Toggles, at + ": no cell hides and shows again");
                Assert.AreEqual(0, f.BlockFlips, at + ": no B1 block flips between boxes and houses");
                Assert.AreEqual(0, f.Doubles, at + ": no house drawn twice");
                Assert.AreEqual(0, f.Holes, at + ": no house the band draws is missing");
                Assert.AreEqual(0, f.HalfShown, at + ": a version still uploading is never drawn");
                Assert.AreEqual(0, f.ColliderMismatches, at + ": the colliders are those of what is drawn");
                Assert.AreEqual(0, f.OverBudget, at + ": the band holds its budget");
                Assert.AreEqual(0, f.OverUpload, at + ": while tiles stream, B0 uploads within half the frame's cap");
                Assert.AreEqual(0, f.StopBuilds, at + ": nothing is rebuilt while the rider stands");
            }
        }

        /// <summary>Rides the route on a tier and counts the faults; then stands at the end with the camera swinging until
        /// B0 has settled and 3 s more.</summary>
        private static Faults Ride(int tier, Street st, double speed)
        {
            var b = new Bench(tier);
            var faults = new Faults();
            var anchor = new BandAnchor();
            var shown = new Dictionary<B0CellSet.Cell, bool>();
            var hiddenAt = new Dictionary<B0CellSet.Cell, double[]>();
            var blocks = new Dictionary<long, bool>();
            var drawnBy = new Dictionary<long, int>();
            double x, z, hx, hz;
            st.At(0, out x, out z, out hx, out hz);
            anchor.Follow(x - 4.5 * hx, z - 4.5 * hz, x, z, true);
            b.AddTilesAround(anchor.X, anchor.Z);
            for (int f = 0, quiet = 0; f < 3000 && quiet < 45; f++)
            {
                b.Frame(anchor.X, anchor.Z);
                quiet = b.Set.Settled ? quiet + 1 : 0;
            }
            Assert.IsTrue(b.Set.Settled, TierName[tier] + ": B0 settles at Thamel Chowk");
            b.TilesStream = true; // from here on, as if new tiles streamed in every frame of the ride

            double dt = FrameMs / 1000.0, t = 0, s = 0, standing = -1, settledFor = 0;
            int buildsWhenSettled = -1;
            bool first = true;
            while (settledFor < 3.0 && standing < 30.0)
            {
                st.At(s, out x, out z, out hx, out hz);
                double camX, camZ;
                if (standing < 0)
                {
                    // Riding: the chase camera sways sideways and along its boom.
                    double side = 0.8 * Math.Sin(2 * Math.PI * 0.4 * t), boom = 4.5 + 0.3 * Math.Sin(2 * Math.PI * 1.1 * t);
                    camX = x - hx * boom + hz * side;
                    camZ = z - hz * boom - hx * side;
                }
                else
                {
                    // Standing: it swings round the rider.
                    double a = 0.7 * Math.Sin(2 * Math.PI * 0.15 * standing), ca = Math.Cos(a), sa = Math.Sin(a);
                    camX = x - 4.5 * (hx * ca - hz * sa);
                    camZ = z - 4.5 * (hx * sa + hz * ca);
                }
                anchor.Follow(camX, camZ, x, z, true);
                b.AddTilesAround(anchor.X, anchor.Z);
                int builds = b.Set.Builds;
                b.Frame(anchor.X, anchor.Z);
                faults.Frames++;
                Check(b, anchor.X, anchor.Z, t, faults, shown, hiddenAt, blocks, drawnBy, first);
                first = false;
                // While tiles stream: within half the cap, or one chunk when even that does not fit (it never starves);
                // with the tiles all in, B0 may take the whole cap.
                if (b.TilesStream && b.Sink.FrameBytes > CapBytes / 2 && b.Sink.FrameChunks > 1)
                {
                    faults.OverUpload++;
                    faults.Note(b.Sink.FrameChunks + " chunks, " + b.Sink.FrameBytes + " bytes uploaded at t " + t.ToString("0.0") + " s");
                }
                if (standing >= 0)
                {
                    // Once the rider has stood a second and the set has caught up (the near level of Asan's houses takes a
                    // few seconds on High), nothing more is built for 3 s.
                    if (buildsWhenSettled < 0 && standing >= 1.0 && b.Set.Settled) buildsWhenSettled = b.Set.Builds;
                    else if (buildsWhenSettled >= 0)
                    {
                        faults.StopBuilds += b.Set.Builds - builds;
                        settledFor += dt;
                    }
                    standing += dt;
                }
                else if ((s += speed * dt) >= st.Length)
                {
                    // Arrived: the rider stands at Asan, and the tiles around a standing rider are all in.
                    s = st.Length;
                    standing = 0;
                    b.TilesStream = false;
                }
                t += dt;
            }
            Assert.GreaterOrEqual(buildsWhenSettled, 0, TierName[tier] + ": the set settles while the rider stands at Asan");
            b.Set.Dispose();
            Assert.AreEqual(0, b.Sink.Live.Count, "disposing frees every chunk");
            return faults;
        }

        /// <summary>One frame's checks against what the cells and the B1 blocks draw.</summary>
        private static void Check(Bench b, double cx, double cz, double t, Faults faults, Dictionary<B0CellSet.Cell, bool> shown, Dictionary<B0CellSet.Cell, double[]> hiddenAt,
                                  Dictionary<long, bool> blocks, Dictionary<long, int> drawnBy, bool first)
        {
            BandConfig bands = b.Bands;
            double speed = Math.Sqrt(b.Set.VelocityX * b.Set.VelocityX + b.Set.VelocityZ * b.Set.VelocityZ);
            // A cell hidden and shown again before the centre moved out past its exit pad and back within the lead is a
            // toggle; the lead along the velocity brings a cell in that much earlier on purpose.
            double lead = speed * bands.B0NeedLeadS;
            if (lead < 0.5) lead = 0;
            double hysteresis = Math.Max(BandAnchor.DeadzoneM, B0CellSet.ExitPadM + bands.B0BuildPadM - bands.B0LeadM - Math.Min(lead, B0CellSet.MaxLeadM));
            if (b.Set.ShownTris > BandConfig.B0ShownBudgetTris(b.Tier))
            {
                faults.OverBudget++;
                faults.Note("B0 at " + b.Set.ShownTris + " tris at t " + t.ToString("0.0") + " s");
            }
            if (b.Sink.ShownTris != b.Set.ShownTris)
            {
                faults.HalfShown++;
                faults.Note("the sink draws " + b.Sink.ShownTris + " tris, the shown cells hold " + b.Set.ShownTris);
            }
            foreach (KeyValuePair<TileId, B0Tile> kv in b.Tiles)
            {
                B0Tile tile = kv.Value;
                if (tile.Index == null) continue;
                B0HouseIndex idx = tile.Index;
                bool fine = bands.B0FineCells;
                int per = idx.PerSide(fine);
                double lx = cx - tile.Id.X0, lz = cz - tile.Id.Z0;

                // B1 blocks: one switch between standing in and handing over is a flip (the whole block pops).
                for (int k = 0; k < tile.BlocksPerSide * tile.BlocksPerSide; k++)
                {
                    bool ready = b.Set.BlockReady(tile, k), before;
                    long bk = (long)tile.Id.Key * 4096 + k;
                    if (!first && blocks.TryGetValue(bk, out before) && before != ready)
                    {
                        faults.BlockFlips++;
                        faults.Note("block " + k + " of " + tile.Id + (ready ? " handed over" : " stood in") + " at t " + t.ToString("0.0") + " s");
                    }
                    blocks[bk] = ready;
                }

                drawnBy.Clear();
                foreach (B0CellSet.Cell c in tile.Cells.Values)
                {
                    // Show and hide history.
                    bool was;
                    shown.TryGetValue(c, out was);
                    if (c.Shown != was)
                    {
                        double[] where;
                        if (c.Shown && hiddenAt.TryGetValue(c, out where) && Math.Sqrt((cx - where[0]) * (cx - where[0]) + (cz - where[1]) * (cz - where[1])) < hysteresis)
                        {
                            faults.Toggles++;
                            faults.Note("cell " + c.X + "," + c.Z + " of " + tile.Id + " shown again at t " + t.ToString("0.0") + " s");
                        }
                        if (!c.Shown) hiddenAt[c] = new[] { cx, cz };
                        shown[c] = c.Shown;
                    }

                    // Colliders: a shown cell registers exactly its version's set, a hidden one none.
                    ulong key = B0CellSet.CellKey(tile.Id, per, c.X, c.Z);
                    int registered;
                    bool has = b.Ground.Sets.TryGetValue(key, out registered);
                    int want = c.Shown && c.Live != null && c.Live.Colliders != null ? c.Live.Colliders.Count : 0;
                    if (want > 0 ? !has || registered != want : has)
                    {
                        faults.ColliderMismatches++;
                        faults.Note("cell " + c.X + "," + c.Z + " of " + tile.Id + " registers " + (has ? registered : 0) + " colliders, draws " + want);
                    }

                    // A version still uploading is never drawn; a shown one is whole and every chunk of it drawn.
                    B0CellSet.Version p = c.Pending;
                    if (p != null && p.Handles != null)
                        for (int k = 0; k < p.Uploaded; k++)
                            if (((Sink.Chunk)p.Handles[k]).Shown)
                            {
                                faults.HalfShown++;
                                faults.Note("a chunk of a pending version of cell " + c.X + "," + c.Z + " is drawn");
                            }
                    if (!c.Shown) continue;
                    B0CellSet.Version v = c.Live;
                    if (v == null || v.Uploaded != v.Chunks.Count)
                    {
                        faults.HalfShown++;
                        faults.Note("cell " + c.X + "," + c.Z + " of " + tile.Id + " is shown without a whole version");
                        continue;
                    }
                    for (int k = 0; k < v.Uploaded; k++)
                        if (!((Sink.Chunk)v.Handles[k]).Shown) faults.HalfShown++;

                    // A shown cell's houses: never also drawn by a standing-in block.
                    int[] houses = idx.Houses(fine, c.X, c.Z);
                    for (int k = 0; k < v.Positions.Length; k++)
                    {
                        int i = houses[v.Positions[k]];
                        int n;
                        drawnBy.TryGetValue(i, out n);
                        drawnBy[i] = n + 1;
                        if (n > 0 || !idx.Meets(i, bands.B0OuterM, lx, lz, 0)) continue;
                        int kx0, kz0, kx1, kz1;
                        idx.HouseBlocks(i, tile.BlocksPerSide, out kx0, out kz0, out kx1, out kz1);
                        for (int kz = kz0; kz <= kz1; kz++)
                            for (int kx = kx0; kx <= kx1; kx++)
                                if (!b.Set.BlockReady(tile, kz * tile.BlocksPerSide + kx))
                                {
                                    faults.Doubles++;
                                    faults.Note("house " + i + " of " + tile.Id + " drawn by B0 and its standing-in block at t " + t.ToString("0.0") + " s");
                                }
                    }
                }
                foreach (KeyValuePair<long, int> d in drawnBy)
                    if (d.Value > 1)
                    {
                        faults.Doubles++;
                        faults.Note("house " + d.Key + " of " + tile.Id + " drawn by " + d.Value + " cells");
                    }

                // Every house the band draws: drawn by its cell, or every block it lies in stands in for it.
                for (int iz = 0; iz < per; iz++)
                    for (int ix = 0; ix < per; ix++)
                    {
                        if (!idx.CellMeets(fine, ix, iz, bands.B0OuterM, lx, lz, 0)) continue;
                        foreach (int i in idx.Houses(fine, ix, iz))
                        {
                            if (drawnBy.ContainsKey(i) || !idx.Meets(i, bands.B0OuterM, lx, lz, 0)) continue;
                            int kx0, kz0, kx1, kz1;
                            idx.HouseBlocks(i, tile.BlocksPerSide, out kx0, out kz0, out kx1, out kz1);
                            for (int kz = kz0; kz <= kz1; kz++)
                                for (int kx = kx0; kx <= kx1; kx++)
                                    if (b.Set.BlockReady(tile, kz * tile.BlocksPerSide + kx))
                                    {
                                        faults.Holes++;
                                        faults.Note("house " + i + " of " + tile.Id + " at " + idx.Distance(i, lx, lz).ToString("0.0") + " m drawn by nobody at t " + t.ToString("0.0") + " s");
                                    }
                        }
                    }
            }
        }
    }
}
