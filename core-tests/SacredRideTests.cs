using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The owner's first Unity playtest (docs/W2_PLAYTEST_1.md #1): at Hiti Pokhari (Naxal) the rider rode up onto a
    /// raised brick platform spanning the whole of Narayanhiti Path and could not ride through. The platform was the plinth
    /// of a 96 m generic pagoda framed on the convex hull of the U-shaped temple compound across the road from the pond
    /// (OSM way 206874129, <c>building=temple</c>, 36 points). These rides use the game's own stack: the tile built as the
    /// streamer builds it (<see cref="TileBuild.Execute"/>, High tier), the layered <see cref="TileGroundQuery"/>, the
    /// heroes' and the B0 near-ring cells' colliders registered under the tile key as <c>WorldStreamer</c> does, and the
    /// motorbike's <see cref="ArcadeVehicle"/> steered down every lane of the drawn road both ways (heritage package).
    /// </summary>
    public class SacredRideTests
    {
        /// <summary>The level-10 tile of Hiti Pokhari.</summary>
        private static readonly TileId HitiPokhari = new TileId(10, 517, 162);

        /// <summary>The Narayanhiti Path pieces past the compound: Primary osm 165226102 (through the old plinth) and
        /// 753805280 (east of it).</summary>
        private static readonly ulong[] NarayanhitiPath = { 165226102UL, 753805280UL };

        private sealed class World
        {
            public TileGroundQuery Ground, Bare;
            public TileData Tile;
        }

        /// <summary>
        /// The streamed world of one tile: the tile built at the High tier, its ground in two layered queries, one with the
        /// colliders the streamer registers under the tile key (the heroes' and every B0 near-ring cell within
        /// <paramref name="cellsNear"/> m of the given roads) and a bare one with only the tile's own solids.
        /// </summary>
        private static World Build(TileId id, ulong[] roads, double cellsNear)
        {
            MeshingSettings s = StreamingScene.Settings(2, 10);
            s.DrawInstances = false;
            var w = new World { Ground = StreamingScene.Ground(s), Bare = StreamingScene.Ground(s) };
            TileBuild b = StreamingScene.Build(id, s, w.Ground);
            w.Tile = b.Source;
            w.Ground.Add(id, b.Sampler, b.Roads);
            w.Bare.Add(id, b.Sampler, b.Roads);
            var merged = new StructureColliders();
            if (b.HeroColliders != null) merged.AddAll(b.HeroColliders);
            // The near ring as DetailCells builds it (the band table's B0 options over the tier's detail options).
            BuildingOptions o = BuildingBandTable.Copy(s.DetailBuildings, BuildingBand.B0KitLite);
            BandConfig bc = BandConfig.ForTier(2);
            if (bc.B0CapTris > 0) o.B0CapTris = bc.B0CapTris;
            o.B0BaseDrop = Math.Max(bc.B0BaseDrop, s.DetailBuildings.B0BaseDrop);
            int sub = BuildingBandTable.NearSubdivision, per = BuildingDetailMesher.CellsPerSide(b.Source, sub);
            double cell = b.Source.Tile.Size / per;
            var gen = new GenColliders();
            for (int cz = 0; cz < per; cz++)
            for (int cx = 0; cx < per; cx++)
            {
                double mx = (cx + 0.5) * cell, mz = (cz + 0.5) * cell;
                if (NearestRoad(b.Source, roads, mx, mz) > cellsNear) continue;
                BuildingDetailMesher.BuildCell(b.Source, b.Sampler, cx, cz, sub, o, new MeshData(), gen);
            }
            merged.AddFrom(gen);
            if (merged.Count > 0) w.Ground.Register(id.Key, merged);
            return w;
        }

        private static double NearestRoad(TileData t, ulong[] osm, double x, double z)
        {
            double best = double.MaxValue;
            foreach (RoadRecord r in t.Roads)
            {
                if (Array.IndexOf(osm, r.OsmWayId) < 0) continue;
                for (int k = 0; k + 1 < r.PointCount; k++)
                {
                    double ox, oz;
                    best = Math.Min(best, RoadCorridor.PointSeg(x, z, r.Points[2 * k] / 100.0, r.Points[2 * k + 1] / 100.0, r.Points[2 * k + 2] / 100.0,
                                                                r.Points[2 * k + 3] / 100.0, out ox, out oz));
                }
            }
            return best;
        }

        /// <summary>A lane of a drawn road piece as a polyline (game metres): <paramref name="lane"/> (−1 … 1) of the way
        /// from the drawn carriageway's centre to 1 m inside its edge, to the right of travel; reversed when asked.</summary>
        private static void Lane(TileData t, int road, double lane, bool reverse, List<double> px, List<double> pz)
        {
            px.Clear();
            pz.Clear();
            RoadLayout layout = RoadLayout.For(t);
            RoadCentreline c = layout.Centres[road];
            RoadWidthProfile p = layout.Profiles[road];
            for (int k = 0; k < c.Count; k++)
            {
                double half = 0.5 * p.DrawnAt(c.S[k]);
                double sh = p.ShiftAt(c.S[k]) - lane * Math.Max(0, half - 1.0) * (reverse ? -1 : 1);
                px.Add(t.Tile.X0 + c.X[k] - c.Tz[k] * sh);
                pz.Add(t.Tile.Z0 + c.Z[k] + c.Tx[k] * sh);
            }
            if (reverse)
            {
                px.Reverse();
                pz.Reverse();
            }
        }

        private struct Ride
        {
            public bool Done;
            public double Reached, Length, MaxRise;
            public int Hits;
            public string Where;
        }

        /// <summary>Ride a motorbike down a polyline with a pure-pursuit rider (3.5 m look-ahead, 8 m/s, 3 m/s in bends):
        /// done within 3 m of the end, stuck after 4 s without progress. The rise is the bike's height over the bare
        /// ground (the drawn road without registered colliders).</summary>
        private static Ride RideAlong(World w, List<double> px, List<double> pz)
        {
            var ps = new List<double> { 0 };
            for (int k = 1; k < px.Count; k++) ps.Add(ps[k - 1] + Math.Sqrt((px[k] - px[k - 1]) * (px[k] - px[k - 1]) + (pz[k] - pz[k - 1]) * (pz[k] - pz[k - 1])));
            double total = ps[ps.Count - 1];
            var r = new Ride { Length = total };
            var v = new ArcadeVehicle(VehicleSpec.Motorbike()) { RoadAssist = false };
            double sx, sz, hx, hz;
            At(px, pz, ps, 1.0, out sx, out sz, out hx, out hz);
            v.Teleport(sx, sz, (float)Math.Atan2(hx, hz), w.Ground);
            const float dt = 1f / 30f;
            double best = 1.0, bestAt = 0, time = 0, maxTime = total / 2.0 + 15;
            while (time < maxTime)
            {
                double s = Project(px, pz, ps, v.X, v.Z, best);
                if (s > best + 0.5)
                {
                    best = s;
                    bestAt = time;
                }
                if (best > total - 3)
                {
                    r.Done = true;
                    break;
                }
                if (time - bestAt > 4.0) break;
                double tx, tz, ux, uz;
                At(px, pz, ps, Math.Min(total, best + 3.5), out tx, out tz, out ux, out uz);
                double err = Math.Atan2(tx - v.X, tz - v.Z) - v.HeadingRad;
                while (err > Math.PI) err -= 2 * Math.PI;
                while (err < -Math.PI) err += 2 * Math.PI;
                float target = Math.Abs(err) > 0.5 ? 3f : 8f;
                var input = new DriveInput((float)Math.Max(0, Math.Min(0.6, (target - v.SpeedMps) * 0.3)), v.SpeedMps > target + 1f ? 0.6f : 0f,
                                           (float)Math.Max(-1, Math.Min(1, err * 2.5)));
                if ((v.Step(input, dt, w.Ground, 0f) & StepEvents.HitWall) != 0) r.Hits++;
                GroundSample g;
                if (w.Bare.TrySample(v.X, v.Z, v.Y + 0.3f, out g) && v.Y - g.Height > r.MaxRise)
                {
                    r.MaxRise = v.Y - g.Height;
                    r.Where = string.Format("({0:0.0},{1:0.0})", v.X, v.Z);
                }
                time += dt;
            }
            r.Reached = best;
            if (r.Where == null) r.Where = string.Format("({0:0.0},{1:0.0})", v.X, v.Z);
            return r;
        }

        private static void At(List<double> px, List<double> pz, List<double> ps, double s, out double x, out double z, out double hx, out double hz)
        {
            int n = ps.Count;
            for (int k = 0; k + 1 < n; k++)
            {
                if (s > ps[k + 1] && k + 2 < n) continue;
                double len = ps[k + 1] - ps[k], f = len > 1e-9 ? Math.Max(0, Math.Min(1, (s - ps[k]) / len)) : 0;
                x = px[k] + (px[k + 1] - px[k]) * f;
                z = pz[k] + (pz[k + 1] - pz[k]) * f;
                hx = (px[k + 1] - px[k]) / Math.Max(1e-9, len);
                hz = (pz[k + 1] - pz[k]) / Math.Max(1e-9, len);
                return;
            }
            x = px[n - 1];
            z = pz[n - 1];
            hx = 0;
            hz = 1;
        }

        private static double Project(List<double> px, List<double> pz, List<double> ps, double x, double z, double near)
        {
            double best = double.MaxValue, at = near;
            for (int k = 0; k + 1 < ps.Count; k++)
            {
                if (ps[k + 1] < near - 15 || ps[k] > near + 15) continue;
                double ox, oz;
                double d = RoadCorridor.PointSeg(x, z, px[k], pz[k], px[k + 1], pz[k + 1], out ox, out oz);
                if (d >= best) continue;
                best = d;
                double len = ps[k + 1] - ps[k];
                at = ps[k] + (len > 1e-9 ? Math.Sqrt((ox - px[k]) * (ox - px[k]) + (oz - pz[k]) * (oz - pz[k])) : 0);
            }
            return best < 4.0 ? at : near;
        }

        [Test]
        public void NarayanhitiPathPastHitiPokhariIsRideableEndToEnd()
        {
            World w = Build(HitiPokhari, NarayanhitiPath, 90.0);
            var px = new List<double>();
            var pz = new List<double>();
            int rides = 0;
            var failed = new List<string>();
            for (int i = 0; i < w.Tile.Roads.Count; i++)
            {
                if (Array.IndexOf(NarayanhitiPath, w.Tile.Roads[i].OsmWayId) < 0) continue;
                foreach (double lane in new[] { -1.0, -0.5, 0.0, 0.5, 1.0 })
                foreach (bool reverse in new[] { false, true })
                {
                    Lane(w.Tile, i, lane, reverse, px, pz);
                    Ride r = RideAlong(w, px, pz);
                    rides++;
                    string line = string.Format("osm {0} lane {1:+0.0;-0.0;0} {2}: {3} at {4:0}/{5:0} m, wall hits {6}, highest {7:0.00} m over the road at {8}",
                                                w.Tile.Roads[i].OsmWayId, lane, reverse ? "east to west" : "west to east", r.Done ? "through" : "STUCK", r.Reached,
                                                r.Length, r.Hits, r.MaxRise, r.Where);
                    TestContext.WriteLine(line);
                    // Through to the end, never up on a platform (the old plinth's 0.45 m steps lifted the bike up to 2 m),
                    // never against a wall.
                    if (!r.Done || r.MaxRise > 0.25 || r.Hits > 0) failed.Add(line);
                }
            }
            Assert.That(rides, Is.EqualTo(20), "two pieces, five lanes, both ways");
            Assert.That(failed, Is.Empty, string.Join("\n", failed));
        }
    }
}
