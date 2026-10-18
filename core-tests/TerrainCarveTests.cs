using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The terrain carve of the cut/fill package (owner playtest #16, docs/W2_PLAYTEST_1.md): the carved terrain of every
    /// tile meets its neighbours' exactly along the tile seams (a road's bench crossing a border is carved alike on both
    /// sides), and the hill roads of the sample are held by retaining walls where the cut is deep and guarded by parapets
    /// and rails where the ground drops away (docs/research/w2/ref_cutfill.md).
    /// </summary>
    public class TerrainCarveTests
    {
        /// <summary>Border vertices of the terrain mesh, per border (west, east, south, north): along the border (game
        /// metres) and height, sorted along.</summary>
        private static List<double[]>[] Borders(TileData t, MeshData m)
        {
            var lists = new List<double[]>[4];
            for (int b = 0; b < 4; b++) lists[b] = new List<double[]>();
            float size = (float)t.Tile.Size;
            var used = new bool[m.VertexCount];
            for (int k = 0; k < m.IndexCount; k++) used[m.Indices[k]] = true;
            for (int v = 0; v < m.VertexCount; v++)
            {
                if (!used[v]) continue;
                float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                if (Math.Abs(x) < 1e-3f) lists[0].Add(new[] { t.Tile.Z0 + z, (double)y });
                if (Math.Abs(x - size) < 1e-3f) lists[1].Add(new[] { t.Tile.Z0 + z, (double)y });
                if (Math.Abs(z) < 1e-3f) lists[2].Add(new[] { t.Tile.X0 + x, (double)y });
                if (Math.Abs(z - size) < 1e-3f) lists[3].Add(new[] { t.Tile.X0 + x, (double)y });
            }
            foreach (List<double[]> l in lists) l.Sort((a, b) => a[0] != b[0] ? a[0].CompareTo(b[0]) : a[1].CompareTo(b[1]));
            return lists;
        }

        /// <summary>Height of the border polyline <paramref name="l"/> at <paramref name="along"/> (linear between its
        /// vertices); NaN beyond its ends.</summary>
        private static double On(List<double[]> l, double along)
        {
            for (int k = 0; k + 1 < l.Count; k++)
            {
                if (l[k][0] > along + 1e-6 || l[k + 1][0] < along - 1e-6) continue;
                double d = l[k + 1][0] - l[k][0];
                double f = d < 1e-9 ? 0 : Math.Max(0, Math.Min(1, (along - l[k][0]) / d));
                return l[k][1] + (l[k + 1][1] - l[k][1]) * f;
            }
            return double.NaN;
        }

        /// <summary>
        /// Every tile's carved terrain (High tier step, no skirts) meets its east and north neighbours': each border vertex
        /// of either side lies on the other side's border edge within 1 cm, so no crack or step shows along a seam, also
        /// where a road's bench, its cut slope or its embankment crosses the border (both tiles carve the crossing from the
        /// same road extents, <see cref="TerrainCarve"/>'s border crossings).
        /// </summary>
        [Test]
        public void CarvedTerrainMeetsItsNeighboursAtEveryTileSeam()
        {
            var borders = new Dictionary<ulong, List<double[]>[]>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null) continue;
                var m = new MeshData();
                TerrainMesher.Build(t, t.Tile, new TerrainOptions { Step = 2, SkirtDepthM = 0f, Walls = false }, m);
                borders[t.Tile.Key] = Borders(t, m);
            }
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            int checkedVerts = 0, off = 0, seams = 0;
            double worst = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                List<double[]>[] a;
                if (!borders.TryGetValue(id.Key, out a)) continue;
                for (int dir = 0; dir < 2; dir++)
                {
                    TileId nb = dir == 0 ? new TileId(10, id.Tx + 1, id.Ty) : new TileId(10, id.Tx, id.Ty + 1);
                    List<double[]>[] b;
                    if (!borders.TryGetValue(nb.Key, out b)) continue;
                    seams++;
                    List<double[]> la = dir == 0 ? a[1] : a[3], lb = dir == 0 ? b[0] : b[2];
                    for (int side = 0; side < 2; side++)
                    {
                        List<double[]> from = side == 0 ? la : lb, to = side == 0 ? lb : la;
                        foreach (double[] p in from)
                        {
                            double y = On(to, p[0]);
                            if (double.IsNaN(y)) continue;
                            checkedVerts++;
                            double d = Math.Abs(y - p[1]);
                            if (d <= 0.01) continue;
                            off++;
                            if (d > worst) worst = d;
                            if (off <= 10) sb.AppendLine(string.Format(ic, "  {0}/{1} at {2:0.00}: {3:0.000} against {4:0.000}", id, nb, p[0], p[1], y));
                        }
                    }
                }
            }
            TestContext.Progress.WriteLine(string.Format(ic, "terrain seams: {0} seams, {1} border vertices, {2} off the neighbour's edge by more than 1 cm (worst {3:0.000} m)",
                                                         seams, checkedVerts, off, worst));
            if (sb.Length > 0) TestContext.Progress.WriteLine(sb.ToString());
            Assert.That(seams, Is.GreaterThan(10));
            Assert.That(checkedVerts, Is.GreaterThan(10000));
            Assert.That(off, Is.EqualTo(0), sb.ToString());
        }

        /// <summary>
        /// At the owner's screenshot (Gaurighat) and the other hill roads of the sample, the carve holds its deep cuts with
        /// breast walls (gabion baskets or dry stone) and guards its drops with parapet posts (and a steel rail beside main
        /// roads), drawn in the terrain mesh and handed to the solids: every breast wall has higher ground behind its face
        /// (it holds a cut) and every parapet stands at the crest of a real drop. Before the cut/fill package the hill roads
        /// had neither.
        /// </summary>
        [Test]
        public void HillRoadsAreHeldByWallsAndGuardedAtDrops()
        {
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            int walls = 0, rails = 0, wallsHolding = 0, railsAtDrop = 0, gaurighatRails = 0, gaurighatWalls = 0;
            var barriers = new List<CarveBarrier>();
            var seen = new HashSet<ulong>();
            foreach (HillPlaces.Place place in HillPlaces.All)
            {
                TileData t = StreamingSampleRegion.Tile(place.Tile);
                if (t == null) continue;
                TileHeightSampler h = TileHeightSampler.ForArea(t, t.Tile, HillPlaces.RuntimeStep);
                TerrainCarve carve = h.Carve;
                Assert.That(carve, Is.Not.Null, place.Name + ": the hill roads are carved");
                double px, pz;
                place.Game(out px, out pz);
                barriers.Clear();
                carve.CopyBarriers(barriers);
                int pw = 0, pr = 0, ph = 0, pd = 0;
                foreach (CarveBarrier b in barriers)
                {
                    double mx = 0.5 * (b.AX + b.BX), mz = 0.5 * (b.AZ + b.BZ);
                    if ((mx - px) * (mx - px) + (mz - pz) * (mz - pz) > place.RadiusM * place.RadiusM) continue;
                    double dx = b.BX - b.AX, dz = b.BZ - b.AZ, l = Math.Sqrt(dx * dx + dz * dz);
                    if (l < 0.2) continue;
                    double nx = -dz / l, nz = dx / l;
                    float bench = b.Bottom + 0.3f;
                    // The ground either side of the barrier (one side is the road).
                    float y0, y1;
                    double reach = b.Wall ? carve.WallDepthM + 1.0 : TerrainCarve.ParapetReachM;
                    bool h0 = h.TryHeight(mx + nx * reach, mz + nz * reach, out y0), h1 = h.TryHeight(mx - nx * reach, mz - nz * reach, out y1);
                    if (!h0 || !h1) continue;
                    if (b.Wall)
                    {
                        pw++;
                        if (Math.Max(y0, y1) > bench + 1.0f) ph++;
                    }
                    else
                    {
                        pr++;
                        if (Math.Min(y0, y1) < bench - 1.0f) pd++;
                    }
                }
                var drawn = new MeshData();
                int tris = seen.Add(t.Tile.Key) ? carve.AppendWalls(drawn) : -1;
                sb.AppendLine(string.Format(ic, "{0} ({1}): {2} breast wall bays ({3} with higher ground behind), {4} parapet runs ({5} at a drop of 1 m and more){6}",
                                            place.Name, t.Tile, pw, ph, pr, pd, tris >= 0 ? ", " + tris + " wall and parapet triangles in the tile's terrain" : ""));
                if (tris >= 0) Assert.That(tris, Is.GreaterThan(0), place.Name + ": walls and parapets are drawn");
                walls += pw;
                rails += pr;
                wallsHolding += ph;
                railsAtDrop += pd;
                if (place.Name == "gaurighat")
                {
                    gaurighatRails = pr;
                    gaurighatWalls = pw;
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
            Assert.That(walls, Is.GreaterThan(20), "breast walls hold the deep cuts");
            Assert.That(rails, Is.GreaterThan(20), "parapets guard the drops");
            Assert.That(gaurighatRails + gaurighatWalls, Is.GreaterThan(0), "the owner's screenshot");
            Assert.That((double)wallsHolding / walls, Is.GreaterThan(0.9), "breast walls hold a cut");
            Assert.That((double)railsAtDrop / rails, Is.GreaterThan(0.9), "parapets stand at a drop");
        }
    }
}
