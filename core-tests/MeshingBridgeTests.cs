using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class MeshingBridgeTests
    {
        // ---------------------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------------------

        private static MeshData Mesh(TileData t, int lod = 0)
        {
            var m = new MeshData();
            BridgeMesher.Build(t, new TileHeightSampler(t), new BridgeOptions { Lod = lod }, m);
            return m;
        }

        private static MeshData SpanMesh(BridgeLayout layout, int span, int lod = 0)
        {
            var m = new MeshData();
            BridgeMesher.BuildSpan(layout, span, layout.Ground, new BridgeOptions { Lod = lod }, m);
            return m;
        }

        /// <summary>Well formed, UV0 on every vertex with a known channel and AO in [0, 1], front faces agree with the
        /// normals.</summary>
        private static void AssertGoodMesh(MeshData m, string what)
        {
            MeshingChecks.AssertWellFormed(m, what);
            Assert.That(m.HasUv0, Is.True, what + ": UV0");
            int maxCh = (int)MaterialChannel.Marking;
            for (int v = 0; v < m.VertexCount; v++)
            {
                float u = m.Uv0[2 * v], ao = m.Uv0[2 * v + 1];
                if (u < 0 || u > maxCh || Math.Abs(u - Math.Round(u)) > 1e-4) Assert.Fail(what + ": vertex " + v + " channel " + u);
                if (ao < 0 || ao > 1) Assert.Fail(what + ": vertex " + v + " AO " + ao);
            }
            MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, -0.75, what);
        }

        /// <summary>For every station of every span: a railing (or median barrier) vertex on both sides within the
        /// railing base, at least 0.8 m above the deck.</summary>
        private static void AssertRailingsBothSides(BridgeLayout layout, MeshData m, string what)
        {
            for (int si = 0; si < layout.Spans.Count; si++)
            {
                BridgeSpan sp = layout.Spans[si];
                MeshData sm = SpanMesh(layout, si);
                for (int k = 0; k < sp.Count; k++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        // The railing line in this station's cross-section (mitred or on the border at a cut end).
                        double u = side * ((side > 0 ? sp.InnerL(k) : sp.InnerR(k)) + 0.5 * BridgeStyle.RailBaseM);
                        double rx = sp.Path.X[k] + sp.Path.Ux[k] * u, rz = sp.Path.Z[k] + sp.Path.Uz[k] * u;
                        bool found = false;
                        for (int v = 0; v < sm.VertexCount && !found; v++)
                        {
                            double dx = sm.Positions[3 * v] - rx, dz = sm.Positions[3 * v + 2] - rz;
                            if (dx * dx + dz * dz > 0.4 * 0.4) continue;
                            if (sm.Positions[3 * v + 1] - sp.Path.Y[k] >= 0.8) found = true;
                        }
                        if (!found)
                            Assert.Fail(what + ": span " + si + " (road " + sp.Road + ", " + sp.Railing + ") has no railing on side " + side +
                                        " at station " + k + " of " + sp.Count);
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Kit
        // ---------------------------------------------------------------------------------------------------------

        [Test]
        public void KitPrimitivesFaceOutward()
        {
            var m = new MeshData();
            BridgeKit.RoundedBox(m, 0, 0, 0.3, 1, 0.5, 0, 2, 0.1, 2, 0xFFFFFFFF, MaterialChannel.Concrete, 0.5f, 1f);
            BridgeKit.Column(m, 5, 0, 0.5, 0, 3, 12, 0xFFFFFFFF, MaterialChannel.Concrete, 0.5f, 1f);
            var p = new BridgeProfile().RoundRect(0, 0, 0.2, 0.1, 0.05, 2);
            var path = new BridgePath();
            path.Add(0, 10, 0, 1, 0, 1, 0, 100f);
            path.Add(10, 10, 0, 1, 0, 1, 10, 101f);
            BridgeKit.Sweep(m, path, 0, 10, p, 0, 0, 0xFFFFFFFF, MaterialChannel.Metal, 1f, 0, 1.0, 0x000000FF, 0, 0, true, true);
            var xs = new[] { 0.0, 1, 2 };
            var ys = new[] { 0.0, 1, 1.2 };
            var zs = new[] { 20.0, 20, 21 };
            BridgeKit.Tube(m, xs, ys, zs, 3, 0.1, 6, true, 0xFFFFFFFF, MaterialChannel.Metal, 1f);
            AssertGoodMesh(m, "kit");
            // Outward: every vertex normal of the box points away from its centre.
            for (int v = 0; v < 40; v++)
            {
                double dx = m.Positions[3 * v], dy = m.Positions[3 * v + 1] - 1, dz = m.Positions[3 * v + 2];
                Assert.That(dx * m.Normals[3 * v] + dy * m.Normals[3 * v + 1] + dz * m.Normals[3 * v + 2], Is.GreaterThan(-1e-3), "box normal " + v);
            }
        }

        [Test]
        public void StripesAreWorldAnchored()
        {
            // Two paths along the same line, split at an arbitrary point, give the same stripe boundaries.
            var a = new BridgePath();
            a.Add(0, 0, 0, 1, 0, 1, 0, 0f);
            a.Add(17.3, 0, 0, 1, 0, 1, 17.3, 0f);
            var b = new BridgePath();
            b.Add(17.3 - 1024, 0, 0, 1, 0, 1, 0, 0f);
            b.Add(40 - 1024, 0, 0, 1, 0, 1, 22.7, 0f);
            int na = BridgeKit.BuildStations(a, 0, 17.3, 0, 1.0, 1000, 0);
            var sa = new List<double>();
            for (int i = 0; i < na; i++) sa.Add(BridgeKit.Stations[i]);
            int nb = BridgeKit.BuildStations(b, 0, 22.7, 0, 1.0, 1000 + 1024, 0);
            Assert.That(sa[1], Is.EqualTo(1.0).Within(1e-6));
            Assert.That(BridgeKit.Stations[1] + 17.3, Is.EqualTo(18.0).Within(1e-6));
            Assert.That(nb, Is.GreaterThan(20));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Layout and meshes
        // ---------------------------------------------------------------------------------------------------------

        [Test]
        public void RiverBridgeHasDeckRailingsAbutmentsAndPiers()
        {
            TileData t = BridgeScenes.RiverBridge();
            BridgeLayout layout = BridgeLayout.For(t);
            Assert.That(layout.Derived, Is.False);
            Assert.That(layout.Spans.Count, Is.EqualTo(1));
            BridgeSpan sp = layout.Spans[0];
            Assert.That(sp.Water, Is.True);
            Assert.That(sp.StartCut || sp.EndCut, Is.False);
            Assert.That(sp.Length, Is.EqualTo(120).Within(0.01));
            // Piers every ~21 m where the deck stands clear of the ground (the banks carry the rest).
            Assert.That(sp.Piers.Count, Is.GreaterThanOrEqualTo(2));
            for (int i = 1; i < sp.Piers.Count; i++) Assert.That(sp.Piers[i] - sp.Piers[i - 1], Is.LessThanOrEqualTo(25));
            // Clear width between the railings holds the rideable corridor.
            for (int k = 0; k < sp.Count; k++) Assert.That(sp.InnerL(k) + sp.InnerR(k), Is.GreaterThanOrEqualTo(RoadClearance.MinCorridorM));
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "river bridge");
            AssertRailingsBothSides(layout, m, "river bridge");
            // Abutments reach down below the deck at both ends; something stands under the middle (a pier).
            float minY, maxY, a, b, c, d;
            m.GetBounds(out a, out minY, out b, out c, out maxY, out d);
            Assert.That(minY, Is.LessThan(1280f), "piers reach the river bed");
        }

        [Test]
        public void DeckQueryPicksTheRightLevel()
        {
            TileData t = BridgeScenes.RiverBridge();
            BridgeDeckIndex idx = BridgeDeckIndex.ForTile(t);
            double x = t.Tile.X0 + 500, z = t.Tile.Z0 + 500;
            float deck, nx, ny, nz;
            Assert.That(idx.TryDeck(x, z, 1300f, out deck, out nx, out ny, out nz), Is.True, "from above");
            BridgeSpan sp = idx.Layout.Spans[0];
            Assert.That(deck, Is.EqualTo(sp.Path.YAt(60)).Within(0.01));
            Assert.That(ny, Is.GreaterThan(0.99f));
            // From the river bed under it: no deck to stand on, but a ceiling.
            float under;
            Assert.That(idx.TryDeck(x, z, 1279.5f, out deck, out nx, out ny, out nz), Is.False, "from below");
            Assert.That(idx.TryCeiling(x, z, 1279.5f, out under), Is.True);
            Assert.That(under, Is.EqualTo(sp.Path.YAt(60) - sp.Lerp(sp.Depth, 60)).Within(0.01));
            // On the walkway the surface is one kerb higher; beyond the railing nothing.
            double walk = sp.HalfL[0] + 0.5 * sp.WalkL[0];
            Assert.That(idx.TryDeck(x, z + walk, 1300f, out deck, out nx, out ny, out nz), Is.True);
            Assert.That(deck, Is.EqualTo(sp.Path.YAt(60) + BridgeStyle.KerbHeightM).Within(0.01));
            Assert.That(idx.TryDeck(x, z + sp.EdgeL(0) + 0.5, 1300f, out deck, out nx, out ny, out nz), Is.False);
            // Beyond the abutments: nothing; on a slope the normal tilts along the deck.
            Assert.That(idx.TryDeck(t.Tile.X0 + 430, z, 1300f, out deck, out nx, out ny, out nz), Is.False);
            Assert.That(idx.TryDeck(t.Tile.X0 + 455, z, 1300f, out deck, out nx, out ny, out nz), Is.True);
            Assert.That(nx, Is.LessThan(-0.005f), "rising toward +x");
            // Outside the tile square: the neighbour answers.
            Assert.That(idx.TryDeck(t.Tile.X0 - 5, z, 1300f, out deck, out nx, out ny, out nz), Is.False);
        }

        [Test]
        public void FlyoverKeepsUnderpassClearance()
        {
            int lower;
            float lowY;
            TileData t = BridgeScenes.Flyover(out lower, out lowY);
            BridgeLayout layout = BridgeLayout.For(t);
            Assert.That(layout.Spans.Count, Is.EqualTo(1));
            BridgeSpan sp = layout.Spans[0];
            Assert.That(sp.Crossings.Count, Is.EqualTo(1));
            Assert.That(sp.Railing, Is.EqualTo(RailingStyle.CrashBarrier));
            Assert.That(sp.Fill[0], Is.True, "ramps are fill between retaining walls");
            Assert.That(sp.Piers.Count, Is.GreaterThan(2));
            Assert.That(sp.Lamps.Count, Is.GreaterThan(4));
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "flyover");
            AssertRailingsBothSides(layout, m, "flyover");
            // Nothing of the structure inside the lower road's clearance envelope (corridor × 5.5 m).
            BridgeCrossing c = sp.Crossings[0];
            double half = c.LowHalfM;
            for (int tri = 0; tri < m.TriangleCount; tri++)
            {
                double cx = 0, cy = 0, cz = 0;
                for (int k = 0; k < 3; k++)
                {
                    int v = m.Indices[3 * tri + k];
                    cx += m.Positions[3 * v] / 3;
                    cy += m.Positions[3 * v + 1] / 3;
                    cz += m.Positions[3 * v + 2] / 3;
                }
                if (Math.Abs(cx - 500) < half && cy > lowY - 0.01 && cy < lowY + RoadClearance.MinUnderpassClearanceM)
                    Assert.Fail("triangle " + tri + " at (" + cx + ", " + cy + ", " + cz + ") inside the underpass clearance");
            }
            // The ceiling seen from the lower road everywhere under the deck.
            BridgeDeckIndex idx = BridgeDeckIndex.ForTile(t);
            for (double dz = -6; dz <= 6; dz += 1.5)
            for (double dx = -half + 0.2; dx < half; dx += 1.0)
            {
                float under;
                if (!idx.TryCeiling(t.Tile.X0 + 500 + dx, t.Tile.Z0 + 500 + dz, lowY + 0.5f, out under)) continue;
                Assert.That(under - lowY, Is.GreaterThanOrEqualTo(RoadClearance.MinUnderpassClearanceM - 1e-3), "ceiling at " + dx + ", " + dz);
            }
        }

        [Test]
        public void TightDeckThinsToKeepClearance()
        {
            int lower;
            float lowY;
            TileData t = BridgeScenes.Flyover(out lower, out lowY);
            // Lower the whole deck to 6.2 m above the road: the box girder (1.5 m) would leave 4.7 m.
            RoadStructureRecord rec = t.RoadStructures[0];
            for (int i = 0; i < rec.DeckY.Length; i++) rec.DeckY[i] = Math.Min(rec.DeckY[i], lowY + 6.2f);
            t.RoadStructures[0] = rec;
            BridgeLayout layout = BridgeLayout.For(t);
            BridgeSpan sp = layout.Spans[0];
            float under;
            BridgeDeckIndex idx = BridgeDeckIndex.ForTile(t);
            Assert.That(sp.Crossings.Count, Is.EqualTo(1));
            float road = sp.Crossings[0].LowY; // the lower ribbon's own lift
            Assert.That(idx.TryCeiling(t.Tile.X0 + 500, t.Tile.Z0 + 500, road + 0.5f, out under), Is.True);
            Assert.That(under - road, Is.GreaterThanOrEqualTo(RoadClearance.MinUnderpassClearanceM - 1e-3));
            Assert.That(sp.Lerp(sp.Depth, 350), Is.EqualTo(6.2f - (road - lowY) - RoadClearance.MinUnderpassClearanceM).Within(0.02));
        }

        [Test]
        public void FootOverbridgeHasStairsTrussAndClearance()
        {
            TileData t = BridgeScenes.FootOverbridge();
            BridgeLayout layout = BridgeLayout.For(t);
            Assert.That(layout.Spans.Count, Is.EqualTo(1));
            BridgeSpan sp = layout.Spans[0];
            Assert.That(sp.Overbridge, Is.True);
            Assert.That(sp.Railing, Is.EqualTo(RailingStyle.SteelTruss));
            Assert.That(sp.Stairs.Count, Is.EqualTo(2));
            foreach (BridgeStair st in sp.Stairs)
            {
                Assert.That(st.Steps, Is.GreaterThan(30));
                Assert.That(st.Landings, Is.GreaterThanOrEqualTo(2));
                Assert.That((st.TopY - st.BottomY) / st.Steps, Is.EqualTo(BridgeStyle.StairRiseM).Within(0.02));
            }
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "foot overbridge");
            AssertRailingsBothSides(layout, m, "foot overbridge");
            // The stairs are walkable through the deck query, down to the ground.
            BridgeDeckIndex idx = BridgeDeckIndex.ForTile(t);
            BridgeStair s0 = sp.Stairs[0];
            float y, nx, ny, nz;
            Assert.That(idx.TryDeck(t.Tile.X0 + s0.X + s0.Dx * (s0.Length - 0.5), t.Tile.Z0 + s0.Z + s0.Dz * (s0.Length - 0.5), 1301f, out y, out nx, out ny, out nz),
                        Is.True);
            Assert.That(y, Is.LessThan(1300.8f));
        }

        [Test]
        public void LongBridgeIsMultiSpan()
        {
            TileData t = BridgeScenes.LongBridge();
            BridgeLayout layout = BridgeLayout.For(t);
            BridgeSpan sp = layout.Spans[0];
            Assert.That(sp.Length, Is.GreaterThan(310));
            Assert.That(sp.Piers.Count, Is.GreaterThanOrEqualTo(10));
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "long bridge");
            AssertRailingsBothSides(layout, m, "long bridge");
        }

        [Test]
        public void UnderpassGetsTrenchWalls()
        {
            TileData t = BridgeScenes.Underpass();
            BridgeLayout layout = BridgeLayout.For(t);
            Assert.That(layout.Spans.Count, Is.EqualTo(0));
            Assert.That(layout.Trenches.Count, Is.EqualTo(1));
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "underpass");
            // Walls on both sides of the deepest stretch, from the road up to a parapet above the ground.
            BridgeSpan tr = layout.Trenches[0];
            int k = tr.StationAt(300);
            for (int side = -1; side <= 1; side += 2)
            {
                double u = side * (side > 0 ? tr.InnerL(k) : tr.InnerR(k));
                double wx = tr.Path.X[k] + tr.Path.Nx[k] * u, wz = tr.Path.Z[k] + tr.Path.Nz[k] * u;
                float lo = float.MaxValue, hi = float.MinValue;
                for (int v = 0; v < m.VertexCount; v++)
                {
                    double dx = m.Positions[3 * v] - wx, dz = m.Positions[3 * v + 2] - wz;
                    if (dx * dx + dz * dz > 0.5 * 0.5) continue;
                    lo = Math.Min(lo, m.Positions[3 * v + 1]);
                    hi = Math.Max(hi, m.Positions[3 * v + 1]);
                }
                Assert.That(lo, Is.LessThan(tr.Path.Y[k] + 0.1f), "wall foot at the road, side " + side);
                Assert.That(hi, Is.GreaterThan(1310.9f), "parapet above the ground, side " + side);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Seams
        // ---------------------------------------------------------------------------------------------------------

        [TestCase(true)]
        [TestCase(false)]
        public void DeckIsContinuousAcrossTileSeams(bool records)
        {
            TileData west, east;
            BridgeScenes.Seam(records, out west, out east);
            BridgeLayout lw = BridgeLayout.For(west), le = BridgeLayout.For(east);
            Assert.That(lw.Spans.Count, Is.EqualTo(1));
            Assert.That(le.Spans.Count, Is.EqualTo(1));
            Assert.That(lw.Derived, Is.EqualTo(!records));
            BridgeSpan a = lw.Spans[0], b = le.Spans[0];
            Assert.That(a.EndCut && b.StartCut, Is.True);
            int ka = a.Count - 1;
            Assert.That(a.Path.Y[ka], Is.EqualTo(b.Path.Y[0]).Within(1e-3), "deck height at the border");
            Assert.That(a.EdgeL(ka), Is.EqualTo(b.EdgeL(0)).Within(1e-3));
            Assert.That(a.EdgeR(ka), Is.EqualTo(b.EdgeR(0)).Within(1e-3));
            Assert.That(a.InnerL(ka), Is.EqualTo(b.InnerL(0)).Within(1e-3));
            Assert.That(a.Lerp(a.Depth, a.Path.End), Is.EqualTo(b.Lerp(b.Depth, b.Path.Start)).Within(1e-3));
            // Deck queries agree on both sides of the border.
            BridgeDeckIndex iw = BridgeDeckIndex.ForTile(west), ie = BridgeDeckIndex.ForTile(east);
            double bx = east.Tile.X0;
            for (double dz = -3; dz <= 3; dz += 1)
            {
                float yw, ye, nx, ny, nz;
                double z = west.Tile.Z0 + 516.2 + dz;
                Assert.That(iw.TryDeck(bx - 0.01, z, 1300f, out yw, out nx, out ny, out nz), Is.True, "west at " + dz);
                Assert.That(ie.TryDeck(bx + 0.01, z, 1300f, out ye, out nx, out ny, out nz), Is.True, "east at " + dz);
                // Records carry the context heights, so the surfaces meet exactly; derived decks only know their own
                // side of an oblique cut, which leaves a step of a centimetre or two off the centreline.
                Assert.That(ye, Is.EqualTo(yw).Within(records ? 0.005 : 0.03), "deck step at " + dz);
            }
            // The border cross-sections of the two meshes coincide (every border vertex of one has a twin).
            MeshData mw = Mesh(west), me = Mesh(east);
            AssertGoodMesh(mw, "west");
            AssertGoodMesh(me, "east");
            List<double[]> vw = BorderVertices(mw, 1024.0, 0), ve = BorderVertices(me, 0.0, 1024.0);
            Assert.That(vw.Count, Is.GreaterThan(20));
            Assert.That(ve.Count, Is.GreaterThan(20));
            foreach (double[] p in vw)
            {
                bool twin = false;
                foreach (double[] q in ve)
                    if (Math.Abs(p[0] - q[0]) < 0.01 && Math.Abs(p[1] - q[1]) < 0.01 && Math.Abs(p[2] - q[2]) < 0.01) twin = true;
                if (!twin) Assert.Fail("border vertex (" + p[0] + ", " + p[1] + ", " + p[2] + ") of the west tile has no twin in the east tile");
            }
            // Every pier is drawn by exactly one tile (world-anchored stations): no two closer than a span.
            var piers = new List<double>();
            foreach (double s in a.Piers) piers.Add(west.Tile.X0 + a.Path.X[a.StationAt(s)]);
            foreach (double s in b.Piers) piers.Add(east.Tile.X0 + b.Path.X[b.StationAt(s)]);
            piers.Sort();
            for (int i = 1; i < piers.Count; i++) Assert.That(piers[i] - piers[i - 1], Is.GreaterThan(10), "duplicate pier at the seam");
        }

        /// <summary>Vertices of the sweeps lying on the border plane x = <paramref name="localX"/>, as world-relative
        /// (x shifted by <paramref name="shift"/>, y, z).</summary>
        private static List<double[]> BorderVertices(MeshData m, double localX, double shift)
        {
            var list = new List<double[]>();
            for (int v = 0; v < m.VertexCount; v++)
            {
                if (Math.Abs(m.Positions[3 * v] - localX) > 1e-3) continue;
                list.Add(new double[] { m.Positions[3 * v] + shift, m.Positions[3 * v + 1], m.Positions[3 * v + 2] });
            }
            return list;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Sample region, determinism, budgets
        // ---------------------------------------------------------------------------------------------------------

        [Test]
        public void SampleBridgesBuildWithRailingsBothSides()
        {
            int spans = 0, tiles = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                bool any = false;
                foreach (RoadRecord r in t.Roads)
                    if ((r.Flags & RoadFlags.Bridge) != 0) any = true;
                if (!any) continue;
                BridgeLayout layout = BridgeLayout.For(t);
                Assert.That(layout.Derived, Is.True, "the sample pack has no structure chunk yet");
                if (layout.Spans.Count == 0) continue;
                tiles++;
                spans += layout.Spans.Count;
                MeshData m = Mesh(t);
                AssertGoodMesh(m, id.ToString());
                AssertRailingsBothSides(layout, m, id.ToString());
            }
            TestContext.WriteLine("sample: " + spans + " spans in " + tiles + " tiles");
            Assert.That(spans, Is.GreaterThan(50));
        }

        [Test]
        public void ThapathaliBridgeIsFoundAndBuilt()
        {
            TileData t = StreamingSampleRegion.Tile(new TileId(10, 517, 159));
            BridgeLayout layout = BridgeLayout.For(t);
            int found = 0;
            foreach (BridgeSpan sp in layout.Spans)
            {
                if (sp.Record.OsmWayId != 52916461UL && sp.Record.OsmWayId != 136448419UL) continue;
                found++;
                Assert.That(sp.Length, Is.GreaterThan(150));
                Assert.That(sp.Water, Is.True, "over the Bagmati");
            }
            Assert.That(found, Is.EqualTo(2), "both carriageways of the Bagmati bridge");
        }

        [Test]
        public void MeshesAreDeterministic()
        {
            MeshData a = Mesh(BridgeScenes.Flyover(out _, out _)), b = Mesh(BridgeScenes.Flyover(out _, out _));
            Assert.That(a.VertexCount, Is.EqualTo(b.VertexCount));
            Assert.That(a.IndexCount, Is.EqualTo(b.IndexCount));
            for (int i = 0; i < a.VertexCount * 3; i++) Assert.That(a.Positions[i], Is.EqualTo(b.Positions[i]));
            for (int i = 0; i < a.IndexCount; i++) Assert.That(a.Indices[i], Is.EqualTo(b.Indices[i]));
        }

        /// <summary>Triangles per 100 m of deck (plus an allowance per stair flight) at each LOD stay inside the bridge
        /// share of the roads slice (W2_DESIGN 10.4: a bridge in view at LOD0 near the camera, LOD1 in the mid band,
        /// LOD2 far), and each LOD is cheaper than the one before.</summary>
        [Test]
        public void BudgetsHoldPerLod()
        {
            int[] cap = { 9000, 4000, 1200 };
            int[] perStair = { 1000, 800, 20 };
            var scenes = new (string name, TileData t)[]
            {
                ("river", BridgeScenes.RiverBridge()), ("long", BridgeScenes.LongBridge()), ("flyover", BridgeScenes.Flyover(out _, out _)),
                ("foot", BridgeScenes.FootOverbridge()),
            };
            foreach (var (name, t) in scenes)
            {
                BridgeLayout layout = BridgeLayout.For(t);
                double len = 0;
                foreach (BridgeSpan sp in layout.Spans) len += sp.Length;
                int prev = int.MaxValue;
                for (int lod = 0; lod <= 2; lod++)
                {
                    MeshData m = Mesh(t, lod);
                    int stairs = 0;
                    foreach (BridgeSpan sp in layout.Spans) stairs += sp.Stairs.Count;
                    double per100 = (m.TriangleCount - stairs * perStair[lod]) * 100.0 / Math.Max(len, 40);
                    TestContext.WriteLine(name + " LOD" + lod + ": " + m.TriangleCount + " tris, " + per100.ToString("F0") + " per 100 m");
                    Assert.That(per100, Is.LessThanOrEqualTo(cap[lod]), name + " LOD" + lod);
                    Assert.That(m.TriangleCount, Is.LessThan(prev), name + " LOD" + lod + " is cheaper than the level before");
                    prev = m.TriangleCount;
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Previews (visual self-check; set GHUMANTE_BRIDGE_PREVIEW to an output directory)
        // ---------------------------------------------------------------------------------------------------------

        [Test]
        public void WritePreviews()
        {
            string dir = Environment.GetEnvironmentVariable("GHUMANTE_BRIDGE_PREVIEW");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("set GHUMANTE_BRIDGE_PREVIEW to write OBJ previews");
                return;
            }
            Directory.CreateDirectory(dir);
            int lod = 0;
            string lodEnv = Environment.GetEnvironmentVariable("GHUMANTE_BRIDGE_LOD");
            if (!string.IsNullOrEmpty(lodEnv)) lod = int.Parse(lodEnv);
            // Thapathali: the two carriageways of the Bagmati bridge from the sample (derived decks).
            TileData th = StreamingSampleRegion.Tile(new TileId(10, 517, 159));
            BridgeLayout lt = BridgeLayout.For(th);
            var m = new MeshData();
            float water = float.MaxValue;
            for (int i = 0; i < lt.Spans.Count; i++)
            {
                BridgeSpan sp = lt.Spans[i];
                if (sp.Record.OsmWayId != 52916461UL && sp.Record.OsmWayId != 136448419UL) continue;
                BridgeMesher.BuildSpan(lt, i, lt.Ground, new BridgeOptions { Lod = lod }, m);
                for (int k = 0; k < sp.Count; k++) water = Math.Min(water, sp.Ground[k]);
            }
            BridgeScenes.WriteObj(Path.Combine(dir, "thapathali.obj"), m, th, 25, water + 0.6, lt);
            Write(dir, "river", BridgeScenes.RiverBridge(), lod, 1279.8);
            Write(dir, "long", BridgeScenes.LongBridge(), lod, 1274.5);
            Write(dir, "flyover", BridgeScenes.Flyover(out _, out _), lod, 0);
            Write(dir, "foot_overbridge", BridgeScenes.FootOverbridge(), lod, 0);
            Write(dir, "underpass", BridgeScenes.Underpass(), lod, 0);
            // The river bridge on embankments (deck 2.5 m above the banks): abutments and wing walls show.
            TileData emb = BridgeScenes.RiverBridge();
            RoadStructureRecord rec = emb.RoadStructures[1];
            for (int i = 0; i < rec.DeckY.Length; i++) rec.DeckY[i] += 2.5f;
            emb.RoadStructures[1] = rec;
            Write(dir, "abutment", emb, lod, 1279.8);
        }

        private static void Write(string dir, string name, TileData t, int lod, double water)
        {
            MeshData m = Mesh(t, lod);
            BridgeScenes.WriteObj(Path.Combine(dir, name + ".obj"), m, t, 20, water, BridgeLayout.For(t));
        }
    }
}
