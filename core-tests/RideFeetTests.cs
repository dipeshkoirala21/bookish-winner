using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #8 (docs/W2_PLAYTEST_1.md: "on some roads, character feet look like they are being sucked in"):
    /// over the drawn road surface of real kathmandu_core tiles, streamed exactly as the game streams them, the ground query
    /// answers the height of the very triangles the player sees: carriageways on their smoothed profile with crown and
    /// camber, kerbs, footpaths, medians, junction caps and their corner footpaths, rings and islands, the Kalanki
    /// underpass in its cutting. Before this round the ground stood on the terrain plus the ribbon lift over the raw
    /// polylines: 68 % of the sample's drawn road surface was 3 cm or more off, sunk by up to 12 m at the Indra Chowk caps.
    /// </summary>
    public class RideFeetTests
    {
        // Asan / Indra Chowk (old-core lanes, caps, footpaths), Kalanki (the lowered Ring Road underpass), Thamel.
        private static readonly TileId[] FeetTiles = { new TileId(10, 516, 161), new TileId(10, 517, 161), new TileId(10, 514, 161), new TileId(10, 516, 162) };

        /// <summary>A small grid over the up-facing triangles of an uploaded roads layer (tile-local x/z, absolute y).</summary>
        private sealed class Drawn
        {
            private readonly MeshData _m;
            private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
            private readonly double _x0, _z0;

            public Drawn(MeshData m, double x0, double z0)
            {
                _m = m;
                _x0 = x0;
                _z0 = z0;
                for (int k = 0; k + 2 < m.IndexCount; k += 3)
                {
                    int a = m.Indices[k], b = m.Indices[k + 1], c = m.Indices[k + 2];
                    double ux = P(b, 0) - P(a, 0), uy = P(b, 1) - P(a, 1), uz = P(b, 2) - P(a, 2);
                    double vx = P(c, 0) - P(a, 0), vy = P(c, 1) - P(a, 1), vz = P(c, 2) - P(a, 2);
                    double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                    double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (nl < 1e-9 || Math.Abs(ny) / nl < DrawnRoadSurface.MinUpNormal) continue; // kerb faces and skirts are steps
                    int i0 = (int)Math.Floor(Math.Min(P(a, 0), Math.Min(P(b, 0), P(c, 0)))), i1 = (int)Math.Floor(Math.Max(P(a, 0), Math.Max(P(b, 0), P(c, 0))));
                    int j0 = (int)Math.Floor(Math.Min(P(a, 2), Math.Min(P(b, 2), P(c, 2)))), j1 = (int)Math.Floor(Math.Max(P(a, 2), Math.Max(P(b, 2), P(c, 2))));
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        long key = ((long)i << 32) ^ (uint)j;
                        List<int> l;
                        if (!_cells.TryGetValue(key, out l)) _cells[key] = l = new List<int>();
                        l.Add(k);
                    }
                }
            }

            private double P(int v, int c)
            {
                return _m.Positions[3 * v + c];
            }

            /// <summary>The drawn levels over game (x, z), ascending, levels within 1 cm merged to the higher.</summary>
            public List<float> Levels(double x, double z)
            {
                var ys = new List<float>();
                double lx = x - _x0, lz = z - _z0;
                List<int> l;
                if (!_cells.TryGetValue(((long)Math.Floor(lx) << 32) ^ (uint)(int)Math.Floor(lz), out l)) return ys;
                foreach (int k in l)
                {
                    int a = _m.Indices[k], b = _m.Indices[k + 1], c = _m.Indices[k + 2];
                    double ax = P(a, 0), az = P(a, 2), bx = P(b, 0), bz = P(b, 2), cx = P(c, 0), cz = P(c, 2);
                    double d = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
                    if (Math.Abs(d) < 1e-12) continue;
                    double w0 = ((bz - cz) * (lx - cx) + (cx - bx) * (lz - cz)) / d, w1 = ((cz - az) * (lx - cx) + (ax - cx) * (lz - cz)) / d;
                    double w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    ys.Add((float)(w0 * P(a, 1) + w1 * P(b, 1) + w2 * P(c, 1)));
                }
                ys.Sort();
                for (int i = ys.Count - 1; i > 0; i--)
                {
                    if (ys[i] - ys[i - 1] >= 0.01f) continue;
                    ys.RemoveAt(i - 1);
                }
                return ys;
            }
        }

        private static TileGroundQuery _ground;
        private static List<(TileData tile, Drawn drawn)> _tiles;

        /// <summary>The feet tiles streamed as the game does at High tier (TileBuild.Execute: roads, bridges, ornaments;
        /// the ground query wired as WorldRoot.Build), with their roads layers as uploaded.</summary>
        private static void Stream(out TileGroundQuery ground, out List<(TileData tile, Drawn drawn)> tiles)
        {
            if (_ground == null)
            {
                MeshingSettings settings = MeshingSettings.ForTier(2);
                settings.DrawBuildings = false;
                settings.DrawAreas = false;
                settings.DrawInstances = false;
                settings.DrawRoadDecals = false;
                var g = StreamingScene.Ground(settings);
                var list = new List<(TileData, Drawn)>();
                foreach (TileId id in FeetTiles)
                {
                    TileBuild b = StreamingScene.Build(id, settings, g, 2);
                    Assert.That(b.Error, Is.Null);
                    g.Add(id, b.Sampler, b.Roads);
                    Assert.That(b.Roads.DrawnSurface, Is.Not.Null, "the streamer hands the drawn surface to the ground query");
                    list.Add((b.Source, new Drawn(StreamingScene.Layer(b, TileLayers.Roads), b.Source.Tile.X0, b.Source.Tile.Z0)));
                }
                _ground = g;
                _tiles = list;
            }
            ground = _ground;
            tiles = _tiles;
        }

        /// <summary>
        /// Every drawn level of the roads layer on a 1 m grid over the Asan, Indra Chowk, Kalanki and Thamel tiles: the ground
        /// query for feet on that level answers it within 3 cm (decks are the bridges package's and are left out).
        /// </summary>
        [Test]
        public void FeetStandOnTheDrawnRoadSurface()
        {
            TileGroundQuery g;
            List<(TileData tile, Drawn drawn)> tiles;
            Stream(out g, out tiles);
            long samples = 0, over3 = 0, sunk = 0, decks = 0, blocked = 0;
            double worst = 0;
            string where = "", blockedAt = "";
            var hist = new long[5];
            double[] edges = { 0.01, 0.03, 0.1, 0.3, double.MaxValue };
            foreach ((TileData t, Drawn d) in tiles)
            {
                for (double lz = 0.31; lz < t.Tile.Size; lz += 1.0)
                for (double lx = 0.17; lx < t.Tile.Size; lx += 1.0)
                {
                    double x = t.Tile.X0 + lx, z = t.Tile.Z0 + lz;
                    foreach (float y in d.Levels(x, z))
                    {
                        GroundSample s;
                        if (!g.TrySample(x, z, y + 0.005f, out s))
                        {
                            // A wall: a deck slab lower than a body over this drawn road (the bridges package's), never a miss.
                            blocked++;
                            if (blockedAt.Length == 0) blockedAt = t.Tile + " at " + lx.ToString("0.0") + ", " + lz.ToString("0.0") + " drawn " + y.ToString("0.00");
                            continue;
                        }
                        if (s.OnDeck)
                        {
                            decks++;
                            continue;
                        }
                        samples++;
                        double err = s.Height - y, a = Math.Abs(err);
                        int b = 0;
                        while (a >= edges[b]) b++;
                        hist[b]++;
                        if (a < 0.03) continue;
                        over3++;
                        if (err < 0) sunk++;
                        if (a > worst)
                        {
                            worst = a;
                            where = t.Tile + " at " + lx.ToString("0.0") + ", " + lz.ToString("0.0") + " drawn " + y.ToString("0.00") + " ground " + s.Height.ToString("0.00");
                        }
                    }
                }
            }
            TestContext.WriteLine("drawn road samples " + samples + " (decks left out " + decks + ", under a low deck " + blocked + " " + blockedAt + "); |error| <1 cm / 1-3 / 3-10 / 10-30 / >30 cm: " +
                                  string.Join(" / ", hist) + "; 3 cm or more: " + over3 + " (sunk " + sunk + "), worst " + worst.ToString("0.000") + " m " + where);
            Assert.That(samples, Is.GreaterThan(200000));
            Assert.That(blocked, Is.LessThanOrEqualTo(samples / 1000), "drawn roads under a deck slab (the bridges package's clearance): " + blockedAt);
            Assert.That(over3, Is.LessThanOrEqualTo(samples / 10000), "feet on the drawn road surface within 3 cm (worst " + where + ")");
            Assert.That(sunk, Is.EqualTo(0), "no foot sinks into a drawn road");
        }

        /// <summary>
        /// Walkers walk every footpath and cross every junction cap of the Asan and Indra Chowk tiles (the walk harness):
        /// whenever a walker stands on a drawn road surface, its feet are on it within 3 cm (histogram of the feet error).
        /// </summary>
        [Test]
        public void WalkersFeetStayOnTheDrawnSurface()
        {
            TileGroundQuery g;
            List<(TileData tile, Drawn drawn)> tiles;
            Stream(out g, out tiles);
            long steps = 0, over3 = 0;
            double worst = 0;
            string where = "";
            var hist = new long[5];
            double[] edges = { 0.01, 0.03, 0.1, 0.3, double.MaxValue };
            int walks = 0;
            foreach ((TileData t, Drawn d) in tiles.Take(2))
            {
                RoadLayout layout = RoadLayout.For(t);
                var paths = new List<(double ax, double az, double bx, double bz)>();
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadCentreline c = layout.Centres[i];
                    if (c == null || c.Count < 2 || !RoadMesher.IsDrawn(t, i, null) || !layout.DrawsRibbon(i)) continue;
                    RoadWidthProfile p = layout.Profiles[i];
                    // Along the middle of each footpath, 40 m at most.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double s0 = 0.25 * p.LengthM, s1 = Math.Min(p.LengthM - 1, s0 + 40);
                        float foot = p.Sample(side > 0 ? p.FootLeft : p.FootRight, s0);
                        if (foot < 1f || s1 - s0 < 8) continue;
                        double ax, az, atx, atz, bx, bz, btx, btz;
                        c.At(s0, out ax, out az, out atx, out atz);
                        c.At(s1, out bx, out bz, out btx, out btz);
                        double oa = p.ShiftAt(s0) + side * (0.5 * p.DrawnAt(s0) + 0.5 * foot), ob = p.ShiftAt(s1) + side * (0.5 * p.DrawnAt(s1) + 0.5 * foot);
                        paths.Add((t.Tile.X0 + ax - atz * oa, t.Tile.Z0 + az + atx * oa, t.Tile.X0 + bx - btz * ob, t.Tile.Z0 + bz + btx * ob));
                    }
                }
                foreach (JunctionCap cap in layout.Caps)
                {
                    // Across the cap from one arm's cut to the next arm's.
                    if (cap.ArmRoads.Length < 2) continue;
                    RoadCut ca = layout.DrawnCutAt(t, cap.ArmRoads[0], cap.ArmCutS[0]), cb = layout.DrawnCutAt(t, cap.ArmRoads[1], cap.ArmCutS[1]);
                    paths.Add((t.Tile.X0 + ca.CX, t.Tile.Z0 + ca.CZ, t.Tile.X0 + cb.CX, t.Tile.Z0 + cb.CZ));
                }
                foreach (var (ax, az, bx, bz) in paths)
                {
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    if (len < 3) continue;
                    var w = new ArcadeVehicle(VehicleSpec.Walker()) { RoadAssist = false };
                    float heading = (float)Math.Atan2(bx - ax, bz - az);
                    w.Teleport(ax, az, heading, g);
                    walks++;
                    for (int i = 0; i < 60 * 40; i++)
                    {
                        double rx = bx - w.X, rz = bz - w.Z;
                        if (rx * rx + rz * rz < 1.0) break;
                        w.Step(new DriveInput(0.5f, 0f, w.SteerToward((float)Math.Atan2(rx, rz), 1f / 60f)), 1f / 60f, g, 0f);
                        if (w.Airborne) continue;
                        List<float> ys = d.Levels(w.X, w.Z);
                        if (ys.Count == 0) continue;
                        float best = ys.OrderBy(y => Math.Abs(y - w.Y)).First();
                        if (Math.Abs(best - w.Y) > 0.5f) continue; // the walker stands on something else (a plinth, the terrain beside)
                        GroundSample s;
                        if (g.TrySample(w.X, w.Z, w.Y, out s) && (s.OnDeck || s.OnStructure)) continue;
                        steps++;
                        double a = Math.Abs(w.Y - best);
                        int b = 0;
                        while (a >= edges[b]) b++;
                        hist[b]++;
                        if (a < 0.03) continue;
                        over3++;
                        if (a > worst)
                        {
                            worst = a;
                            where = t.Tile + " feet " + w.Y.ToString("0.00") + " drawn " + best.ToString("0.00") + " at " + (w.X - t.Tile.X0).ToString("0.0") + ", " + (w.Z - t.Tile.Z0).ToString("0.0");
                        }
                    }
                }
            }
            TestContext.WriteLine("walks " + walks + ", grounded steps on drawn roads " + steps + "; feet error <1 cm / 1-3 / 3-10 / 10-30 / >30 cm: " + string.Join(" / ", hist) +
                                  "; worst " + worst.ToString("0.000") + " m " + where);
            Assert.That(walks, Is.GreaterThan(300));
            Assert.That(steps, Is.GreaterThan(100000));
            Assert.That(over3, Is.LessThanOrEqualTo(steps / 5000), "feet within 3 cm of the drawn surface: " + where);
        }
    }
}
