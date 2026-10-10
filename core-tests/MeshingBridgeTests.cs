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

        /// <summary>The span's mesh at LOD0 and the vertex range [stairs0, stairs1) of its stair flights.</summary>
        private static MeshData SpanMesh(BridgeLayout layout, int span, out int stairs0, out int stairs1)
        {
            var m = new MeshData();
            int s0 = 0, s1 = 0, prev = 0;
            BridgeMesher.Trace = (part, tris) =>
            {
                if (part == "stairs")
                {
                    s0 = prev;
                    s1 = m.VertexCount;
                }
                prev = m.VertexCount;
            };
            try
            {
                BridgeMesher.BuildSpan(layout, span, layout.Ground, new BridgeOptions { Lod = 0 }, m);
            }
            finally
            {
                BridgeMesher.Trace = null;
            }
            stairs0 = s0;
            stairs1 = s1;
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

        /// <summary>The highest point of a triangle of <paramref name="m"/> straight above or below plan point (x, z)
        /// between y0 and y1 (only triangles facing up when <paramref name="upOnly"/>); NaN when none.</summary>
        private static float HitY(MeshData m, double x, double z, float y0, float y1, bool upOnly)
        {
            float best = float.NaN;
            float[] p = m.Positions, n = m.Normals;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                int a = m.Indices[3 * t], b = m.Indices[3 * t + 1], c = m.Indices[3 * t + 2];
                double ax = p[3 * a], az = p[3 * a + 2], bx = p[3 * b], bz = p[3 * b + 2], cx = p[3 * c], cz = p[3 * c + 2];
                if (x < Math.Min(ax, Math.Min(bx, cx)) - 1e-6 || x > Math.Max(ax, Math.Max(bx, cx)) + 1e-6) continue;
                if (z < Math.Min(az, Math.Min(bz, cz)) - 1e-6 || z > Math.Max(az, Math.Max(bz, cz)) + 1e-6) continue;
                if (upOnly && n[3 * a + 1] + n[3 * b + 1] + n[3 * c + 1] < 2.7f) continue;
                double den = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
                if (Math.Abs(den) < 1e-12) continue;
                double l1 = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) / den, l2 = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) / den, l3 = 1 - l1 - l2;
                if (l1 < -1e-6 || l2 < -1e-6 || l3 < -1e-6) continue;
                float y = (float)(l1 * p[3 * a + 1] + l2 * p[3 * b + 1] + l3 * p[3 * c + 1]);
                if (y < y0 || y > y1) continue;
                if (float.IsNaN(best) || y > best) best = y;
            }
            return best;
        }

        /// <summary>Wherever a span draws its railing (or median barrier) on a side, the railing stands there: some
        /// triangle at least 0.8 m above the walkway straight above the railing base. Railings are drawn on both sides
        /// along every deck run, except where a road, a branch deck or a stair passes through the side
        /// (<see cref="BridgeSpan.GapsL"/>), which must stay short.</summary>
        private static void AssertRailingsBothSides(BridgeLayout layout, string what)
        {
            for (int si = 0; si < layout.Spans.Count; si++)
            {
                BridgeSpan sp = layout.Spans[si];
                if (sp.Stair) continue;
                MeshData sm = SpanMesh(layout, si);
                float top = sp.RaisedWalk ? BridgeStyle.KerbHeightM : 0f;
                for (int r = 0; r + 1 < sp.DeckRuns.Count; r += 2)
                {
                    for (double s = sp.DeckRuns[r] + 0.4; s < sp.DeckRuns[r + 1] - 0.4; s += 1.0)
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            if (!sp.HasRail(side, s)) continue;
                            double x, z, ux, uz, nx, nz, tx, tz;
                            float y;
                            sp.Path.Frame(s, out x, out z, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                            int k = sp.StationAt(s);
                            double inner = side > 0 ? sp.Lerp(sp.HalfL, s) + sp.Lerp(sp.WalkL, s) : sp.Lerp(sp.HalfR, s) + sp.Lerp(sp.WalkR, s);
                            // (Where a tile border cuts the span on the skew, the railing ends at the border.)
                            double size = layout.Tile.Tile.Size, bx = x + nx * side * inner, bz = z + nz * side * inner;
                            if (bx < 0.3 || bz < 0.3 || bx > size - 0.3 || bz > size - 0.3) continue;
                            double clamp = side > 0 ? sp.Lerp(sp.ClampL, s) : sp.Lerp(sp.ClampR, s);
                            inner = Math.Min(inner, clamp - BridgeStyle.RailBaseM);
                            bool found = false;
                            bool shared = side > 0 ? sp.SharedL : sp.SharedR;
                            if (shared)
                            {
                                // A median barrier where twin decks meet: somewhere between this carriageway's edge and
                                // just past the meeting line, at least 0.6 m tall.
                                double c = side > 0 ? sp.Lerp(sp.HalfL, s) : sp.Lerp(sp.HalfR, s);
                                for (double uu = c - 0.3; uu <= Math.Min(clamp, c + 3.0) + 0.5 && !found; uu += 0.1)
                                    found = !float.IsNaN(HitY(sm, x + nx * side * uu, z + nz * side * uu, y + 0.6f, y + 2.5f, false));
                            }
                            // Across the railing base and just inside it (a steel truss hangs its rails 8 cm in), on the
                            // line the rail sweeps follow: straight between the run ends and the key stations, offset along
                            // each station's miter (at a sharp bend the inner corner lies off the segment normal).
                            double lx, lz, ox, oz;
                            RailLine(sp, s, side, out lx, out lz, out ox, out oz);
                            for (int i = -4; i <= 8 && !found; i++)
                            {
                                double u = side * (BridgeStyle.RailBaseM * i / 8.0);
                                found = !float.IsNaN(HitY(sm, lx + ox * u, lz + oz * u, y + top + 0.8f, y + top + 2.5f, false));
                            }
                            if (!found)
                                Assert.Fail(what + ": span " + si + " (way " + sp.Record.OsmWayId + ", " + sp.Railing + ") has no railing on side " + side + " at s " +
                                            s.ToString("F1") + " of " + sp.Length.ToString("F1") + " (station " + k + ")");
                        }
                    }
                }
                // A gap for a branch or a stair stays short; a road running beside the deck at grade opens the side as
                // far as it runs there.
                bool[] own = OwnRoads(layout, sp);
                foreach (BridgeGap g in sp.GapsL)
                    if (g.Road < 0 || own[g.Road]) Assert.That(g.S1 - g.S0, Is.LessThan(Math.Max(20.0, 0.5 * sp.Length)), what + ": left gap");
                foreach (BridgeGap g in sp.GapsR)
                    if (g.Road < 0 || own[g.Road]) Assert.That(g.S1 - g.S0, Is.LessThan(Math.Max(20.0, 0.5 * sp.Length)), what + ": right gap");
            }
        }

        /// <summary>The railing inner face line of <paramref name="side"/> at along <paramref name="s"/> as the rail sweeps
        /// draw it (<c>BridgeMesher.LineSweep</c>): straight between the ends of the railing run holding s and the key
        /// stations inside it, each offset along its own frame (a station's miter); (ox, oz) the segment's left normal.</summary>
        private static void RailLine(BridgeSpan sp, double s, int side, out double lx, out double lz, out double ox, out double oz)
        {
            List<double> runs = side > 0 ? sp.RailRunsL : sp.RailRunsR;
            double a = sp.Path.Start, b = sp.Path.End;
            for (int i = 0; i + 1 < runs.Count; i += 2)
                if (s >= runs[i] - 1e-6 && s <= runs[i + 1] + 1e-6)
                {
                    a = runs[i];
                    b = runs[i + 1];
                }
            double s0 = a, s1 = b;
            for (int k = 0; k < sp.Count; k++)
            {
                double sk = sp.Path.S[k];
                if (!sp.Path.Key[k] || sk <= a + 1e-4 || sk >= b - 1e-4) continue;
                if (sk <= s) s0 = Math.Max(s0, sk);
                else s1 = Math.Min(s1, sk);
            }
            double ax, az, bx, bz, ux, uz, nx, nz, tx, tz;
            float y;
            sp.Path.Frame(s0, out ax, out az, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            double wa = InnerFace(sp, s0, side);
            ax += ux * side * wa;
            az += uz * side * wa;
            sp.Path.Frame(s1, out bx, out bz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            double wb = InnerFace(sp, s1, side);
            bx += ux * side * wb;
            bz += uz * side * wb;
            double f = s1 - s0 > 1e-9 ? (s - s0) / (s1 - s0) : 0;
            lx = ax + (bx - ax) * f;
            lz = az + (bz - az) * f;
            double cx, cz;
            sp.Path.Frame(s, out cx, out cz, out y, out ux, out uz, out ox, out oz, out tx, out tz);
        }

        /// <summary>Offset of the railing inner face of a side at along s (as <c>BridgeMesher.Side</c>).</summary>
        private static double InnerFace(BridgeSpan sp, double s, int side)
        {
            double half = side > 0 ? sp.Lerp(sp.HalfL, s) : sp.Lerp(sp.HalfR, s);
            double walk = side > 0 ? sp.Lerp(sp.WalkL, s) : sp.Lerp(sp.WalkR, s);
            double clamp = side > 0 ? sp.Lerp(sp.ClampL, s) : sp.Lerp(sp.ClampR, s);
            return Math.Min(half + walk, clamp - BridgeStyle.RailBaseM);
        }

        /// <summary>The roads a span's structure may legitimately touch: its own, its approaches (roads meeting it at
        /// an end), the other pieces of its structure and a twin deck sharing a side.</summary>
        private static bool[] OwnRoads(BridgeLayout layout, BridgeSpan sp)
        {
            TileData t = layout.Tile;
            var own = new bool[t.Roads.Count];
            own[sp.Road] = true;
            foreach (BridgeSpan o in layout.Spans)
                if (o.GroupWay == sp.GroupWay) own[o.Road] = true;
            if (sp.PartnerL >= 0) own[layout.Spans[sp.PartnerL].Road] = true;
            if (sp.PartnerR >= 0) own[layout.Spans[sp.PartnerR].Road] = true;
            int[] p = sp.Record.Points;
            for (int j = 0; j < t.Roads.Count; j++)
            {
                int[] q = t.Roads[j].Points;
                for (int i = 0; i < q.Length; i += 2)
                    foreach (int e in new[] { sp.FirstPoint, sp.LastPoint })
                        if (q[i] == p[2 * e] && q[i + 1] == p[2 * e + 1]) own[j] = true;
            }
            return own;
        }

        /// <summary>Vertices of <paramref name="m"/> inside the corridor of road j (carriageway, footpaths and shoulders,
        /// at least the minimum corridor) more than 0.3 m (an at-grade meeting) and less than its envelope
        /// (<see cref="BridgeStyle.EnvelopeM"/>: the overhead clearance, or a walker's headroom over a foot way) above its
        /// surface. Stair vertices (<paramref name="stairs0"/>..<paramref name="stairs1"/>) only count inside the
        /// carriageway and shoulders of a road with traffic: overbridge stairs land on its footpath.</summary>
        private static int Intrusions(BridgeLayout layout, MeshData m, int j, int stairs0, int stairs1, out float minRel)
        {
            TileData t = layout.Tile;
            RoadRecord o = t.Roads[j];
            RoadStructureRecord os = layout.Structures[j];
            RoadLayout rl = RoadLayout.For(t);
            int[] q = o.Points;
            int n = q.Length / 2;
            int hits = 0;
            float env = BridgeStyle.EnvelopeM(o.RoadClass);
            minRel = float.MaxValue;
            float minX, minY, minZ, maxX, maxY, maxZ;
            m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            for (int b = 0; b + 1 < n; b++)
            {
                double ax = q[2 * b] / 100.0, az = q[2 * b + 1] / 100.0, bx = q[2 * b + 2] / 100.0, bz = q[2 * b + 3] / 100.0;
                double dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-6) continue;
                double mid = rl.AlongAt(j, b) + 0.5 * l; // the road layout's own along
                double half = 0.5 * BridgeStructures.CorridorWidth(rl, o, j, mid);
                double carriage = 0.5 * BridgeStructures.CarriageWidth(rl, o, j, mid);
                if (Math.Max(ax, bx) + half < minX || Math.Min(ax, bx) - half > maxX || Math.Max(az, bz) + half < minZ || Math.Min(az, bz) - half > maxZ) continue;
                for (int v = 0; v < m.VertexCount; v++)
                {
                    double px = m.Positions[3 * v], py = m.Positions[3 * v + 1], pz = m.Positions[3 * v + 2];
                    double f = ((px - ax) * dx + (pz - az) * dz) / (l * l);
                    if (f < 0 || f > 1) continue;
                    double qx = ax + dx * f, qz = az + dz * f;
                    double r = v >= stairs0 && v < stairs1 ? carriage : half;
                    if ((px - qx) * (px - qx) + (pz - qz) * (pz - qz) > r * r) continue;
                    // (A record's NaN heights are draped points: the ground there.)
                    bool deck = os.DeckY != null && os.DeckY.Length == n && !float.IsNaN(os.DeckY[b]) && !float.IsNaN(os.DeckY[b + 1]);
                    float low = deck ? (float)(os.DeckY[b] + (os.DeckY[b + 1] - os.DeckY[b]) * f)
                        : BridgeStructures.Ground(t, layout.Ground, qx, qz) + RoadMesher.LiftOf(o, new RoadOptions());
                    float rel = (float)(py - low);
                    if (rel > 0.3f && rel < env)
                    {
                        hits++;
                        minRel = Math.Min(minRel, rel);
                    }
                }
            }
            return hits;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Kit
        // ---------------------------------------------------------------------------------------------------------

        [Test]
        public void KitPrimitivesFaceOutward()
        {
            var m = new MeshData();
            BridgeKit.RoundedBox(m, 0, 0, 0.3, 1, 0.5, 0, 2, 0.1, 2, 0xFFFFFFFF, MaterialChannel.Concrete, 0.5f, 1f);
            int boxVerts = m.VertexCount;
            BridgeKit.Column(m, 5, 0, 0.5, 0, 3, 12, 0xFFFFFFFF, MaterialChannel.Concrete, 0.5f, 1f);
            BridgeKit.Box(m, 10, 0, 0.7, 0.4, 0.2, 0, 1, 0xFFFFFFFF, MaterialChannel.Concrete, 0.5f, 1f, true);
            BridgeKit.CappedPost(m, 12, 0, 0.2, 0.1, 0, 1, 0.08, 0.1, 0xFFFFFFFF, MaterialChannel.Paint, 0.7f, 1f);
            BridgeKit.CappedBlock(m, 14, 0, -0.4, 0.6, 0.3, 0, 1, 0.1, 0.05, 0xFFFFFFFF, MaterialChannel.Concrete, 0.7f, 1f);
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
            // Outward: every vertex normal of the rounded box points away from its centre.
            for (int v = 0; v < boxVerts; v++)
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
        // Layout and meshes (structure records)
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
            Assert.That(sp.StartEnd, Is.EqualTo(SpanEnd.Ground));
            Assert.That(sp.Abutments.Count, Is.EqualTo(2), "an abutment at each real end");
            Assert.That(sp.GapsL.Count + sp.GapsR.Count + sp.Openings.Count, Is.EqualTo(0), "nothing crosses it");
            // Piers every ~21 m where the deck stands clear of the ground (the banks carry the rest).
            Assert.That(sp.Piers.Count, Is.GreaterThanOrEqualTo(2));
            for (int i = 1; i < sp.Piers.Count; i++) Assert.That(sp.Piers[i] - sp.Piers[i - 1], Is.LessThanOrEqualTo(25));
            // Clear width between the railings holds the rideable corridor.
            for (int k = 0; k < sp.Count; k++) Assert.That(sp.InnerL(k) + sp.InnerR(k), Is.GreaterThanOrEqualTo(RoadClearance.MinCorridorM));
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "river bridge");
            AssertRailingsBothSides(layout, "river bridge");
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
            Assert.That(sp.Openings.Count, Is.EqualTo(0));
            Assert.That(layout.Issues.Count, Is.EqualTo(0));
            Assert.That(sp.Railing, Is.EqualTo(RailingStyle.CrashBarrier));
            Assert.That(sp.Fill[0], Is.True, "ramps are fill between retaining walls");
            Assert.That(sp.Piers.Count, Is.GreaterThan(2));
            Assert.That(sp.Lamps.Count, Is.GreaterThan(4));
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "flyover");
            AssertRailingsBothSides(layout, "flyover");
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
            Assert.That(layout.Issues.Count, Is.EqualTo(0), "thinning within the minimum depth is not an issue");
        }

        [Test]
        public void DeckTooLowLeavesTheRoadOpenAndIsReported()
        {
            int lower;
            float lowY;
            TileData t = BridgeScenes.Flyover(out lower, out lowY);
            // A record whose deck stays 3 m over the road: it cannot keep 5.5 m even with the thinnest structure.
            RoadStructureRecord rec = t.RoadStructures[0];
            for (int i = 0; i < rec.DeckY.Length; i++) rec.DeckY[i] = Math.Min(rec.DeckY[i], lowY + 3.0f);
            t.RoadStructures[0] = rec;
            BridgeLayout layout = BridgeLayout.For(t);
            BridgeSpan sp = layout.Spans[0];
            Assert.That(sp.Crossings.Count, Is.EqualTo(0));
            Assert.That(sp.Openings.Count, Is.EqualTo(1), "the structure leaves the road open");
            Assert.That(layout.Issues.Count, Is.GreaterThanOrEqualTo(1), "reported for the data package");
            Assert.That(layout.Issues[0].LowerWay, Is.EqualTo(302UL));
            Assert.That(layout.Issues[0].DeckAboveM, Is.LessThan(3.1f));
            // Nothing over the lower road's corridor below the overhead clearance, and no ceiling there.
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "low deck");
            float minRel;
            Assert.That(Intrusions(layout, m, lower, 0, 0, out minRel), Is.EqualTo(0), "structure in the open road (min " + minRel + ")");
            float under;
            Assert.That(BridgeDeckIndex.ForTile(t).TryCeiling(t.Tile.X0 + 500, t.Tile.Z0 + 500, lowY + 0.3f, out under), Is.False);
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
            Assert.That(sp.Stairs.Count, Is.EqualTo(2), "a stair at each end (nothing mapped)");
            foreach (BridgeStair st in sp.Stairs)
            {
                Assert.That(st.Steps, Is.GreaterThan(30));
                Assert.That(st.Landings, Is.GreaterThanOrEqualTo(2));
                Assert.That((st.TopY - st.BottomY) / st.Steps, Is.EqualTo(BridgeStyle.StairRiseM).Within(0.02));
                Assert.That(st.Length, Is.EqualTo((st.Steps - st.Landings) * st.Tread + st.Landings * st.LandingM + st.BottomRun).Within(1e-6));
            }
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "foot overbridge");
            AssertRailingsBothSides(layout, "foot overbridge");
            // The stairs are walkable through the deck query, down to the ground.
            BridgeDeckIndex idx = BridgeDeckIndex.ForTile(t);
            BridgeStair s0 = sp.Stairs[0];
            float y, nx, ny, nz;
            Assert.That(idx.TryDeck(t.Tile.X0 + s0.X + s0.Dx * (s0.Length - 0.1), t.Tile.Z0 + s0.Z + s0.Dz * (s0.Length - 0.1), 1301f, out y, out nx, out ny,
                                    out nz), Is.True);
            Assert.That(y, Is.LessThan(1300.4f));
        }

        /// <summary>The deck query on a stair stays on the drawn treads: never under the tread drawn at that point and at
        /// most one riser above it (a ramp through the nosings), flat on the landings, and the flight drawn as long as
        /// the query says (finding: the query was 0.6 m off the drawn flight).</summary>
        [Test]
        public void StairQueryFollowsTheDrawnTreads()
        {
            TileData t = BridgeScenes.FootOverbridge();
            BridgeLayout layout = BridgeLayout.For(t);
            BridgeDeckIndex idx = BridgeDeckIndex.ForTile(t);
            BridgeSpan sp = layout.Spans[0];
            int checkedPoints = 0;
            foreach (BridgeStair st in sp.Stairs)
            {
                var m = new MeshData();
                BridgeMesher.BuildSpan(layout, 0, layout.Ground, new BridgeOptions { Lod = 0, Lamps = false, Roofs = false }, m);
                float rise = st.Rise;
                for (double a = 0.02; a < st.Length - 0.02; a += 0.07)
                {
                    double x = st.X + st.Dx * a, z = st.Z + st.Dz * a;
                    float y, nx, ny, nz;
                    Assert.That(idx.TryDeck(t.Tile.X0 + x, t.Tile.Z0 + z, st.TopY + 0.1f, out y, out nx, out ny, out nz), Is.True, "query at " + a);
                    float drawn = HitY(m, x, z, y - rise - 0.05f, y + 0.05f, true);
                    Assert.That(float.IsNaN(drawn), Is.False, "a tread under the query at " + a.ToString("F2"));
                    Assert.That(y - drawn, Is.GreaterThanOrEqualTo(-0.01f), "query under the tread at " + a.ToString("F2"));
                    Assert.That(y - drawn, Is.LessThanOrEqualTo(rise + 0.01f), "query above the tread at " + a.ToString("F2"));
                    checkedPoints++;
                }
                // Landings are flat in the query.
                double at = 0;
                for (int i = 0; i < st.Steps; i++)
                {
                    double len = st.TreadLength(i);
                    if (st.IsLanding(i))
                    {
                        Assert.That(st.SurfaceAt(at + st.Tread + 0.1), Is.EqualTo(st.TreadY(i)).Within(1e-4));
                        Assert.That(st.SurfaceAt(at + len - 0.05), Is.EqualTo(st.TreadY(i)).Within(1e-4));
                    }
                    at += len;
                }
                Assert.That(at, Is.EqualTo(st.Length).Within(1e-6), "the drawn flight is as long as the query's");
            }
            Assert.That(checkedPoints, Is.GreaterThan(200));
        }

        /// <summary>Every flight in the sample region (mapped, extended, dog-legged or generated) is drawn exactly as
        /// long as the deck query walks it: its treads and landings add up to its length (finding: a flight fitted
        /// without landings still drew them, 4 m past its foot).</summary>
        [Test]
        public void SampleStairsDrawnAsLongAsQueried()
        {
            int flights = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                BridgeLayout layout = BridgeLayout.For(StreamingSampleRegion.Tile(id));
                foreach (BridgeSpan sp in layout.Spans)
                foreach (BridgeStair st in sp.Stairs)
                {
                    if (st.Steps <= 0) continue;
                    double at = 0;
                    int landings = 0;
                    for (int i = 0; i < st.Steps; i++)
                    {
                        at += st.TreadLength(i);
                        if (st.IsLanding(i)) landings++;
                    }
                    Assert.That(at, Is.EqualTo(st.Length).Within(1e-6), id + " " + sp.Record.OsmWayId);
                    Assert.That(landings, Is.EqualTo(st.Landings), id + " " + sp.Record.OsmWayId);
                    Assert.That(st.SurfaceAt(st.Length), Is.EqualTo(st.BottomY).Within(0.01f), id + " " + sp.Record.OsmWayId);
                    flights++;
                }
            }
            Assert.That(flights, Is.GreaterThan(20));
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
            AssertRailingsBothSides(layout, "long bridge");
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

        /// <summary>A foot deck whose record steps between two levels (the data package's decks over two roads of
        /// different heights) gets a flight across the step: the walk along the deck never breaks off, and the query
        /// follows the drawn flight (finding: steep stretches inside a deck were left as holes).</summary>
        [Test]
        public void SteppedFootDeckGetsAFlight()
        {
            TileData t = BridgeScenes.FootOverbridge();
            RoadStructureRecord rec = t.RoadStructures[0];
            float d = rec.DeckY[0];
            rec.DeckY = new[] { d, d, d, d - 0.66f, d - 0.66f };
            // (A 0.66 m step over the 11 m from 500 to 511: steeper than a ramp only within a short stretch, so make it
            // a 1.4 m stretch by inserting the points.)
            t.Roads[0] = BridgeScenes.Road(406, RoadClass.Footway, Surface.Concrete, 2.5, RoadFlags.Bridge, 1, 500, 478, 500, 489, 500, 509.6, 500, 511, 500, 522);
            t.RoadStructures[0] = rec;
            BridgeLayout layout = BridgeLayout.For(t);
            BridgeSpan sp = layout.Spans[0];
            Assert.That(sp.StairZones.Count, Is.EqualTo(1));
            int inDeck = 0;
            foreach (BridgeStair st in sp.Stairs)
                if (Math.Abs(st.TopY - d) < 0.01f && Math.Abs(st.BottomY - (d - 0.66f)) < 0.01f) inDeck++;
            Assert.That(inDeck, Is.EqualTo(1), "a flight across the step");
            BridgeDeckIndex idx = BridgeDeckIndex.ForTile(t);
            float prev = d;
            for (double z = 480; z <= 520; z += 0.1)
            {
                float y, nx, ny, nz;
                Assert.That(idx.TryDeck(t.Tile.X0 + 500, t.Tile.Z0 + z, prev + 0.3f, out y, out nx, out ny, out nz), Is.True, "walk breaks off at z " + z.ToString("F1"));
                Assert.That(prev - y, Is.LessThan(0.25f), "drop at z " + z.ToString("F1"));
                prev = y;
            }
            AssertGoodMesh(Mesh(t), "stepped foot deck");
        }

        /// <summary>The thinned key stations of each LOD (<see cref="BridgePath.KeysFor"/>) keep the deck surface: the
        /// line through them never rises over the full deck by more than the LOD's allowance (the slab top stays under
        /// the road surfacing) and never falls under it by more than a few decimetres, and they keep the span's ends.</summary>
        [Test]
        public void ThinnedStationsKeepTheDeck()
        {
            float[] up = { 0.005f, 0.021f, BridgeMesher.FarTopDropM + 0.001f }, down = { 0.011f, 0.051f, 0.251f };
            int spans = 0, dropped = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                BridgeLayout layout = BridgeLayout.For(StreamingSampleRegion.Tile(id));
                foreach (BridgeSpan sp in layout.Spans)
                {
                    spans++;
                    BridgePath p = sp.Path;
                    for (int lod = 0; lod < 3; lod++)
                    {
                        bool[] keys = p.KeysFor(lod);
                        Assert.That(keys[0] && keys[p.Count - 1], Is.True, "span ends kept");
                        int a = 0;
                        for (int k = 1; k < p.Count; k++)
                        {
                            if (!keys[k]) continue;
                            for (int i = a + 1; i < k; i++)
                            {
                                if (!p.Key[i]) continue;
                                dropped++;
                                double f = (p.S[i] - p.S[a]) / (p.S[k] - p.S[a]);
                                float line = (float)(p.Y[a] + (p.Y[k] - p.Y[a]) * f);
                                Assert.That(line - p.Y[i], Is.LessThanOrEqualTo(up[lod]), id + " " + sp.Record.OsmWayId + " LOD" + lod + " above the deck");
                                Assert.That(p.Y[i] - line, Is.LessThanOrEqualTo(down[lod]), id + " " + sp.Record.OsmWayId + " LOD" + lod + " under the deck");
                            }
                            a = k;
                        }
                    }
                }
            }
            Assert.That(spans, Is.GreaterThan(50));
            Assert.That(dropped, Is.GreaterThan(100), "the far LODs thin the stations");
        }

        // ---------------------------------------------------------------------------------------------------------
        // Derived structures (packs without the structure chunk)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A foot overbridge as OSM maps it, without records: its deck clears the road below by the underpass
        /// clearance, its ends move out of the widened road, and the mapped steps become the stairs (no generated axis
        /// stair): each end is a landing closed by an end railing, with a railing gap where each stair leaves.</summary>
        [Test]
        public void DerivedOverbridgeUsesTheMappedSteps()
        {
            TileData t = BridgeScenes.MappedOverbridge(false);
            BridgeLayout layout = BridgeLayout.For(t);
            Assert.That(layout.Derived, Is.True);
            BridgeSpan deck = layout.Spans.Find(s => s.Record.OsmWayId == 701UL);
            Assert.That(deck, Is.Not.Null);
            Assert.That(deck.Overbridge, Is.True);
            Assert.That(deck.Crossings.Count, Is.EqualTo(1));
            BridgeCrossing c = deck.Crossings[0];
            Assert.That(deck.Path.YAt(c.S) - deck.Lerp(deck.Depth, c.S) - c.LowY, Is.GreaterThanOrEqualTo(RoadClearance.MinUnderpassClearanceM - 1e-3));
            Assert.That(deck.ExtStart, Is.GreaterThan(0.5), "the south end moved out of the widened primary road");
            Assert.That(deck.ExtEnd, Is.GreaterThan(0.5), "the north end moved out of the widened primary road");
            Assert.That(deck.Stairs.Count, Is.EqualTo(0), "no generated stair where steps are mapped");
            Assert.That(deck.StartEnd, Is.EqualTo(SpanEnd.Landing));
            Assert.That(deck.EndEnd, Is.EqualTo(SpanEnd.Landing));
            Assert.That(deck.GapsL.Count, Is.GreaterThanOrEqualTo(2), "a railing gap for each stair on the west side");
            Assert.That(deck.GapsR.Count, Is.GreaterThanOrEqualTo(2), "a railing gap for each stair on the east side");
            int stairs = 0;
            foreach (BridgeSpan sp in layout.Spans)
            {
                if (!sp.Stair) continue;
                stairs++;
                Assert.That(sp.Record.RoadClass, Is.EqualTo(RoadClass.Steps));
                Assert.That(sp.Stairs.Count, Is.GreaterThanOrEqualTo(1));
                Assert.That(sp.Steel, Is.EqualTo(deck.Steel), "one paint for the whole structure");
                foreach (BridgeStair st in sp.Stairs)
                {
                    Assert.That(Math.Abs(st.TopY - deck.Path.Y[0]), Is.LessThan(0.01f), "the flight starts at the deck");
                    Assert.That((st.TopY - st.BottomY) / Math.Max(0.1, st.Length), Is.LessThan(0.8), "a walkable grade");
                }
            }
            Assert.That(stairs, Is.EqualTo(4), "the four mapped steps ways");
            // The structure stands in no vehicle road: the primary and its pavements stay clear.
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "mapped overbridge");
            AssertRailingsBothSides(layout, "mapped overbridge");
            float minRel;
            Assert.That(Intrusions(layout, m, 1, 0, 0, out minRel), Is.EqualTo(0), "structure in the primary road (min " + minRel + ")");
            // The derived records are published for the roads and collide packages.
            IReadOnlyList<RoadStructureRecord> recs = BridgeStructures.For(t);
            Assert.That(recs[0].Has(RoadStructureFlags.FootOverbridge), Is.True);
            Assert.That(recs[2].DeckY, Is.Not.Null, "the steps carry stair heights");
            Assert.That(recs[2].DeckY[0], Is.EqualTo(recs[0].DeckY[0]).Within(1e-3));
        }

        /// <summary>Two decks of one overbridge joined in a T: the branch starts at the main deck's edge (no stair,
        /// pillars, cap or columns at the junction), the main deck's railing opens where the branch leaves, the walk
        /// across the junction is continuous, and both pieces share one steel paint.</summary>
        [Test]
        public void TeeOverbridgeJoinsWithoutWalls()
        {
            TileData t = BridgeScenes.MappedOverbridge(true);
            BridgeLayout layout = BridgeLayout.For(t);
            BridgeSpan main = layout.Spans.Find(s => s.Record.OsmWayId == 701UL), branch = layout.Spans.Find(s => s.Record.OsmWayId == 709UL);
            Assert.That(main, Is.Not.Null);
            Assert.That(branch, Is.Not.Null);
            Assert.That(branch.Overbridge, Is.True);
            Assert.That(branch.StartEnd, Is.EqualTo(SpanEnd.Junction));
            Assert.That(branch.TrimStart, Is.GreaterThan(0.5), "the branch starts at the main deck's edge");
            Assert.That(main.Steel, Is.EqualTo(branch.Steel));
            Assert.That(Math.Abs(main.Path.Y[main.PointStation[1]] - branch.Path.Y[0]), Is.LessThan(0.01f), "one deck height");
            // The main deck's east (right, travelling north) railing opens around the junction at its middle point.
            double sj = main.Path.S[main.PointStation[1]];
            Assert.That(main.InGap(-1, sj), Is.True, "railing gap where the branch leaves");
            Assert.That(main.InGap(1, sj), Is.False, "the other side stays closed");
            foreach (BridgeStair st in branch.Stairs)
            {
                double d = Math.Sqrt(Math.Pow(st.X - 500, 2) + Math.Pow(st.Z - 500, 2));
                Assert.That(d, Is.GreaterThan(5), "no stair at the junction");
            }
            // Walk east from the main deck onto the branch: the surface never drops.
            BridgeDeckIndex idx = BridgeDeckIndex.ForTile(t);
            float y0 = main.Path.Y[main.PointStation[1]];
            for (double x = 500; x <= 512; x += 0.25)
            {
                float y, nx, ny, nz;
                Assert.That(idx.TryDeck(t.Tile.X0 + x, t.Tile.Z0 + 500, y0 + 0.2f, out y, out nx, out ny, out nz), Is.True, "deck at x " + x);
                Assert.That(y, Is.EqualTo(y0).Within(0.05f), "deck height at x " + x);
            }
            // No railing vertex across the branch entrance (the main deck's east edge, 1 m either side of the branch axis).
            MeshData m = Mesh(t);
            AssertGoodMesh(m, "tee overbridge");
            double edgeX = 500 + main.EdgeR(main.PointStation[1]);
            int blocking = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                double px = m.Positions[3 * v], py = m.Positions[3 * v + 1], pz = m.Positions[3 * v + 2];
                if (Math.Abs(px - (edgeX - 0.2)) < 0.25 && Math.Abs(pz - 500) < 0.9 && py > y0 + 0.3 && py < y0 + 1.3) blocking++;
            }
            Assert.That(blocking, Is.EqualTo(0), "railing across the branch entrance");
        }

        /// <summary>Every span of the sample region builds (from the pack's structure records, or derived on a pack
        /// without the chunk), with railings wherever its railing runs, and foot overbridges clear the roads they
        /// cross.</summary>
        [Test]
        public void SampleBridgesBuildWithRailingsBothSides()
        {
            int spans = 0, tiles = 0, overbridges = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                bool any = false;
                foreach (RoadRecord r in t.Roads)
                    if ((r.Flags & RoadFlags.Bridge) != 0) any = true;
                if (!any) continue;
                BridgeLayout layout = BridgeLayout.For(t);
                Assert.That(layout.Derived, Is.EqualTo(!BridgeStructures.HasRecords(t)), "derived only without the structure chunk");
                if (layout.Spans.Count == 0) continue;
                tiles++;
                spans += layout.Spans.Count;
                MeshData m = Mesh(t);
                AssertGoodMesh(m, id.ToString());
                AssertRailingsBothSides(layout, id.ToString());
                foreach (BridgeSpan sp in layout.Spans)
                {
                    if (!sp.Overbridge) continue;
                    overbridges++;
                    bool reason = sp.Crossings.Count > 0 || sp.StartEnd == SpanEnd.Junction || sp.EndEnd == SpanEnd.Junction || sp.StartCut || sp.EndCut;
                    foreach (BridgeIssue i in layout.Issues)
                        if (i.Way == sp.Record.OsmWayId) reason = true; // a record too low over its road: left open, reported
                    Assert.That(reason, Is.True, "foot overbridge deck with nothing under it: " + id + " " + sp.Record.OsmWayId);
                }
            }
            TestContext.WriteLine("sample: " + spans + " spans in " + tiles + " tiles, " + overbridges + " foot overbridge decks");
            Assert.That(spans, Is.GreaterThan(50));
            Assert.That(overbridges, Is.GreaterThanOrEqualTo(15));
        }

        /// <summary>
        /// Over the whole sample region (derived decks): every road kept under a deck has at least
        /// <see cref="RoadClearance.MinUnderpassClearanceM"/> (a foot way <see cref="BridgeStyle.FootHeadroomM"/>) under
        /// the soffit, and no vertex of any structure stands in another road's corridor between 0.3 m (an at-grade
        /// meeting) and the clearance envelope above it: the span's own approaches, its structure's other pieces and a
        /// twin deck beside it aside, stairs allowed on a road's footpath. Every plan crossing of a lower road is either
        /// kept clear or left open (none is ignored).
        /// </summary>
        [Test]
        public void SampleRoadsUnderAndBesideBridgesStayClear()
        {
            int crossings = 0, openings = 0, intruding = 0;
            var report = new List<string>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                BridgeLayout layout = BridgeLayout.For(t);
                for (int si = 0; si < layout.Spans.Count; si++)
                {
                    BridgeSpan sp = layout.Spans[si];
                    foreach (BridgeCrossing c in sp.Crossings)
                    {
                        crossings++;
                        // (A record reported as a little short keeps its thinnest structure: BridgeStyle.IssueToleranceM.)
                        float slack = 0.01f;
                        foreach (BridgeIssue i in layout.Issues)
                            if (i.Way == sp.Record.OsmWayId && i.LowerWay == t.Roads[c.Road].OsmWayId) slack = BridgeStyle.IssueToleranceM + 0.01f;
                        for (double s = c.S - c.HalfAlong; s <= c.S + c.HalfAlong; s += 0.5)
                        {
                            if (s < sp.Path.Start || s > sp.Path.End) continue;
                            float clear = sp.Path.YAt(s) - sp.Lerp(sp.Depth, s) - c.LowY;
                            Assert.That(clear, Is.GreaterThanOrEqualTo(BridgeStyle.UnderClearanceM(t.Roads[c.Road].RoadClass) - slack),
                                        id + " " + sp.Record.OsmWayId + " over " + t.Roads[c.Road].OsmWayId);
                        }
                    }
                    openings += sp.Openings.Count;
                    if (sp.Stair && sp.Stairs.Count == 0) continue;
                    int st0, st1;
                    MeshData m = SpanMesh(layout, si, out st0, out st1);
                    bool[] own = OwnRoads(layout, sp);
                    for (int j = 0; j < t.Roads.Count; j++)
                    {
                        if (own[j] || !RoadMesher.IsDrawn(t.Roads[j], null) || (t.Roads[j].Flags & RoadFlags.Tunnel) != 0) continue;
                        float minRel;
                        int hits = Intrusions(layout, m, j, st0, st1, out minRel);
                        if (hits == 0) continue;
                        intruding++;
                        report.Add(id + " " + sp.Record.OsmWayId + " (" + sp.Record.RoadClass + ") in " + t.Roads[j].OsmWayId + " (" + t.Roads[j].RoadClass + "): " +
                                   hits + " vertices, " + minRel.ToString("F2") + " m above it");
                    }
                }
            }
            TestContext.WriteLine("crossings kept clear " + crossings + ", openings " + openings);
            foreach (string r in report) TestContext.WriteLine(r);
            Assert.That(intruding, Is.EqualTo(0), "structures standing in other roads:\n" + string.Join("\n", report));
            Assert.That(crossings, Is.GreaterThan(20));
        }

        [Test]
        public void HeritageRailingsOnlyInHeritagePlaces()
        {
            int newar = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                BridgeLayout layout = BridgeLayout.For(t);
                RoadLayout rl = t.Roads.Count > 0 ? RoadLayout.For(t) : null;
                foreach (BridgeSpan sp in layout.Spans)
                {
                    if (sp.Railing != RailingStyle.Newar) continue;
                    newar++;
                    int k = sp.Count / 2;
                    bool zone = BridgeStyle.InHeritageZone(t.Tile.X0 + sp.Path.X[k], t.Tile.Z0 + sp.Path.Z[k]) ||
                                BridgeStyle.InHeritageZone(t.Tile.X0 + sp.Path.X[0], t.Tile.Z0 + sp.Path.Z[0]);
                    bool flag = rl != null && rl.Attrs[sp.Road].Has(RoadAttrFlags.HeritagePedestrian);
                    Assert.That(zone || flag, Is.True, id + " " + sp.Record.OsmWayId + " is Newar outside the heritage places");
                }
            }
            TestContext.WriteLine("Newar spans: " + newar);
            // The 2018 Ring Road sidewalk bridge at Balkhu (paving stones) shares a side with the trunk deck and takes its
            // crash barrier, not carved wood.
            TileData balkhu = StreamingSampleRegion.Tile(new TileId(10, 520, 159));
            BridgeSpan walk = BridgeLayout.For(balkhu).Spans.Find(s => s.Record.OsmWayId == 1091006164UL);
            Assert.That(walk, Is.Not.Null);
            Assert.That(walk.Railing, Is.Not.EqualTo(RailingStyle.Newar));
            Assert.That(BridgeStyle.InHeritageZone(532967.4, 165850.4), Is.True, "Pashupati");
            Assert.That(BridgeStyle.InHeritageZone(530109.9, 163802.9), Is.False, "Thapathali");
        }

        /// <summary>The Teku suspension footbridge (bridge:structure=suspension) has no river piers but towers and
        /// cables; the Dallu Arch Bridge gets its ribs over the deck and no piers.</summary>
        [Test]
        public void SuspensionAndArchBridgesKeepTheirForm()
        {
            TileData t = StreamingSampleRegion.Tile(new TileId(10, 516, 159));
            BridgeLayout layout = BridgeLayout.For(t);
            BridgeSpan teku = layout.Spans.Find(s => s.Record.OsmWayId == 225466624UL);
            Assert.That(teku, Is.Not.Null);
            Assert.That(teku.Form, Is.EqualTo(BridgeForm.Suspension));
            Assert.That(teku.Piers.Count, Is.EqualTo(0), "no piers in the river");
            MeshData m = SpanMesh(layout, layout.Spans.IndexOf(teku));
            AssertGoodMesh(m, "teku");
            float minX, minY, minZ, maxX, maxY, maxZ;
            m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            Assert.That(teku.TowerA || teku.TowerB, Is.True, "a tower pair stands");
            float deckAtTower = teku.Path.YAt(teku.TowerA ? teku.SuspA : teku.SuspB);
            Assert.That(maxY - deckAtTower, Is.GreaterThan(4.0f), "towers rise over the deck");
            TileData d = StreamingSampleRegion.Tile(new TileId(10, 516, 161));
            BridgeLayout ld = BridgeLayout.For(d);
            BridgeSpan dallu = ld.Spans.Find(s => s.Record.OsmWayId == 651121137UL);
            Assert.That(dallu, Is.Not.Null);
            Assert.That(dallu.Form, Is.EqualTo(BridgeForm.Arch));
            Assert.That(dallu.Piers.Count, Is.EqualTo(0));
            MeshData ma = SpanMesh(ld, ld.Spans.IndexOf(dallu));
            AssertGoodMesh(ma, "dallu");
            ma.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            float dMax = float.MinValue;
            for (int k = 0; k < dallu.Count; k++) dMax = Math.Max(dMax, dallu.Path.Y[k]);
            Assert.That(maxY - dMax, Is.GreaterThan(5.0f), "arch ribs over the deck");
        }

        /// <summary>At every LOD that draws lamps, the pole stands on something: geometry runs down from the pole foot to
        /// the walkway (finding: at LOD1 the pole floated at rail height).</summary>
        [Test]
        public void LampPolesStandOnPilasters()
        {
            TileData t = BridgeScenes.RiverBridge();
            BridgeLayout layout = BridgeLayout.For(t);
            BridgeSpan sp = layout.Spans[0];
            Assert.That(sp.Lamps.Count, Is.GreaterThan(0));
            for (int lod = 0; lod <= 1; lod++)
            {
                MeshData m = SpanMesh(layout, 0, lod);
                foreach (BridgeLamp lamp in sp.Lamps)
                {
                    double x, z, ux, uz, nx, nz, tx, tz;
                    float y;
                    sp.Path.Frame(lamp.S, out x, out z, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                    double w = lamp.Side > 0 ? sp.Lerp(sp.HalfL, lamp.S) + sp.Lerp(sp.WalkL, lamp.S) : sp.Lerp(sp.HalfR, lamp.S) + sp.Lerp(sp.WalkR, lamp.S);
                    double u = lamp.Side * (w + 0.5 * BridgeStyle.RailBaseM);
                    double px = x + nx * u, pz = z + nz * u;
                    // A solid column under the lamp: at every height from the walkway up to 2 m, some triangle lying
                    // wholly within the pilaster's footprint spans that height (nothing floats).
                    float walk = y + (sp.RaisedWalk ? BridgeStyle.KerbHeightM : 0f);
                    for (float band = 0.1f; band < 2.0f; band += 0.2f)
                    {
                        bool any = false;
                        int[] idx = m.Indices;
                        for (int i = 0; i + 2 < m.IndexCount && !any; i += 3)
                        {
                            float lo = float.MaxValue, hi = float.MinValue;
                            bool inside = true;
                            for (int c = 0; c < 3 && inside; c++)
                            {
                                int v = idx[i + c];
                                double dx = m.Positions[3 * v] - px, dz = m.Positions[3 * v + 2] - pz;
                                inside = dx * dx + dz * dz < 0.42 * 0.42;
                                lo = Math.Min(lo, m.Positions[3 * v + 1] - walk);
                                hi = Math.Max(hi, m.Positions[3 * v + 1] - walk);
                            }
                            any = inside && lo <= band && hi >= band;
                        }
                        Assert.That(any, Is.True, "LOD" + lod + " lamp at " + lamp.S.ToString("F1") + ": nothing at " + band.ToString("F1") + " m");
                    }
                }
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
                // The riverside roads at both banks (secondary 751465485, residential 754991649) pass under the deck:
                // with the data package's records (decks over them, the roads lowered) they keep the underpass
                // clearance; on derived decks (at grade, DEM heights) the structure leaves them open.
                foreach (ulong bank in new[] { 751465485UL, 754991649UL })
                {
                    bool kept = false, open = false;
                    foreach (BridgeCrossing c in sp.Crossings)
                    {
                        if (t.Roads[c.Road].OsmWayId != bank) continue;
                        kept = true;
                        for (double s = c.S - c.HalfAlong; s <= c.S + c.HalfAlong; s += 0.5)
                            if (s >= sp.Path.Start && s <= sp.Path.End)
                                Assert.That(sp.Path.YAt(s) - sp.Lerp(sp.Depth, s) - c.LowY, Is.GreaterThanOrEqualTo(RoadClearance.MinUnderpassClearanceM - 0.01f));
                    }
                    foreach (BridgeGap g in sp.Openings)
                        if (g.Road >= 0 && t.Roads[g.Road].OsmWayId == bank) open = true;
                    Assert.That(kept || open, Is.True, sp.Record.OsmWayId + " over the bank road " + bank);
                    if (!layout.Derived) Assert.That(kept, Is.True, sp.Record.OsmWayId + " keeps the clearance over " + bank);
                }
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

        /// <summary>
        /// Bridges take at most half of the W2_DESIGN §10.4 roads + decals slice (8 k / 20 k / 30 k visible triangles on
        /// Low / Mid / High): 4 k / 10 k / 15 k (docs/research/w2/bridges_flyovers.md §5; the share is an open issue for
        /// the lead). Visible triangles are counted as the slice means them: from a camera on every span in turn (its
        /// middle, on the deck), looking in the worst direction, every span within the tier's level-10 ring at the LOD
        /// <see cref="BridgeOptions.LodFor"/> gives for its distance, counting the triangles whose centroid lies within
        /// a 90° horizontal view (wider than the landscape view) or within 3 m of the camera. Per tile: LOD2 within the
        /// Low share; per span each LOD cheaper than the one before; Low never builds LOD0.
        /// </summary>
        [Test]
        public void BudgetsHoldPerTier()
        {
            int[] share = { 4000, 10000, 15000 };
            double[] ring = { 750, 1250, 1750 };
            const int Bins = 36, View = 9; // 10° bins, a 90° view
            var spans = new List<(double[] line, double cx, double cz, float[][] cent, string name)>();
            int worstTile2 = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                BridgeLayout l = BridgeLayout.For(t);
                if (l.Spans.Count == 0) continue;
                int tile2 = 0;
                for (int i = 0; i < l.Spans.Count; i++)
                {
                    BridgeSpan sp = l.Spans[i];
                    var cent = new float[3][];
                    var tris = new int[3];
                    for (int lod = 0; lod < 3; lod++)
                    {
                        MeshData m = SpanMesh(l, i, lod);
                        tris[lod] = m.TriangleCount;
                        cent[lod] = new float[2 * m.TriangleCount];
                        for (int tri = 0; tri < m.TriangleCount; tri++)
                        {
                            int a = m.Indices[3 * tri], b = m.Indices[3 * tri + 1], c = m.Indices[3 * tri + 2];
                            cent[lod][2 * tri] = (m.Positions[3 * a] + m.Positions[3 * b] + m.Positions[3 * c]) / 3f;
                            cent[lod][2 * tri + 1] = (m.Positions[3 * a + 2] + m.Positions[3 * b + 2] + m.Positions[3 * c + 2]) / 3f;
                        }
                    }
                    Assert.That(tris[1], Is.LessThanOrEqualTo(tris[0]), id + " span " + i + " LOD1");
                    Assert.That(tris[2], Is.LessThanOrEqualTo(tris[1]), id + " span " + i + " LOD2");
                    tile2 += tris[2];
                    var line = new double[2 * sp.Count];
                    for (int k = 0; k < sp.Count; k++)
                    {
                        line[2 * k] = t.Tile.X0 + sp.Path.X[k];
                        line[2 * k + 1] = t.Tile.Z0 + sp.Path.Z[k];
                    }
                    // (Centroids to world coordinates.)
                    for (int lod = 0; lod < 3; lod++)
                        for (int q = 0; q < cent[lod].Length; q += 2)
                        {
                            cent[lod][q] = (float)(cent[lod][q] + t.Tile.X0 - 500000);
                            cent[lod][q + 1] = (float)(cent[lod][q + 1] + t.Tile.Z0 - 160000);
                        }
                    int mid = sp.Count / 2;
                    spans.Add((line, line[2 * mid], line[2 * mid + 1], cent, id + " " + sp.Record.OsmWayId));
                }
                worstTile2 = Math.Max(worstTile2, tile2);
            }
            Assert.That(worstTile2, Is.LessThanOrEqualTo(share[0]), "per-tile LOD2");
            var bins = new int[Bins];
            for (int tier = 0; tier < 3; tier++)
            {
                int worst = 0;
                string where = "";
                foreach (var cam in spans)
                {
                    Array.Clear(bins, 0, Bins);
                    int around = 0;
                    foreach (var o in spans)
                    {
                        double d = PolylineDistance(o.line, cam.cx, cam.cz);
                        if (d > ring[tier]) continue;
                        float[] c = o.cent[BridgeOptions.LodFor(tier, d)];
                        for (int q = 0; q < c.Length; q += 2)
                        {
                            double dx = c[q] + 500000 - cam.cx, dz = c[q + 1] + 160000 - cam.cz;
                            if (dx * dx + dz * dz < 9.0)
                            {
                                around++;
                                continue;
                            }
                            double ang = Math.Atan2(dz, dx) + Math.PI;
                            bins[Math.Min(Bins - 1, (int)(ang / (2 * Math.PI) * Bins))]++;
                        }
                    }
                    for (int b0 = 0; b0 < Bins; b0++)
                    {
                        int sum = around;
                        for (int k = 0; k < View; k++) sum += bins[(b0 + k) % Bins];
                        if (sum > worst)
                        {
                            worst = sum;
                            where = cam.name;
                        }
                    }
                }
                TestContext.WriteLine("tier " + tier + ": worst visible " + worst + " of " + share[tier] + " (camera on " + where + ")");
                Assert.That(worst, Is.LessThanOrEqualTo(share[tier]), "tier " + tier + " at " + where);
            }
            Assert.That(BridgeOptions.LodFor(0, 0), Is.EqualTo(1), "Low never builds LOD0");
        }

        /// <summary>Plan distance from (x, z) to a polyline of (x, z) pairs.</summary>
        private static double PolylineDistance(double[] l, double x, double z)
        {
            double best = double.MaxValue;
            for (int i = 0; i + 3 < l.Length; i += 2)
            {
                double ax = l[i], az = l[i + 1], dx = l[i + 2] - ax, dz = l[i + 3] - az, l2 = dx * dx + dz * dz;
                double f = l2 > 1e-12 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
                f = f < 0 ? 0 : f > 1 ? 1 : f;
                double qx = ax + dx * f - x, qz = az + dz * f - z;
                best = Math.Min(best, qx * qx + qz * qz);
            }
            return Math.Sqrt(best);
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
            SampleSpans(dir, "thapathali", 517, 159, lod, 25, true, 52916461UL, 136448419UL);
            // A long multi-span river bridge: the four Ring Road decks over the Bagmati at Balkhu.
            SampleSpans(dir, "balkhu", 515, 159, lod, 20, true, 185470054UL, 670112952UL, 670112954UL, 1091877749UL);
            // Foot overbridges of Kanti Path with their mapped stairs (the structure joined to 112448812).
            SampleGroup(dir, "kantipath_overbridge", 517, 161, lod, 112448812UL);
            SampleGroup(dir, "overbridge_301644010", 517, 160, lod, 301644010UL);
            SampleSpans(dir, "teku_suspension", 516, 159, lod, 15, true, 225466624UL);
            SampleSpans(dir, "dallu_arch", 516, 161, lod, 15, true, 651121137UL);
            Write(dir, "river", BridgeScenes.RiverBridge(), lod, 1279.8);
            Write(dir, "long", BridgeScenes.LongBridge(), lod, 1274.5);
            Write(dir, "flyover", BridgeScenes.Flyover(out _, out _), lod, 0);
            Write(dir, "foot_overbridge", BridgeScenes.FootOverbridge(), lod, 0);
            Write(dir, "mapped_overbridge", BridgeScenes.MappedOverbridge(false), lod, 0);
            Write(dir, "tee_overbridge", BridgeScenes.MappedOverbridge(true), lod, 0);
            Write(dir, "underpass", BridgeScenes.Underpass(), lod, 0);
        }

        private static void SampleSpans(string dir, string name, int tx, int ty, int lod, double margin, bool water, params ulong[] ways)
        {
            TileData t = StreamingSampleRegion.Tile(new TileId(10, tx, ty));
            BridgeLayout l = BridgeLayout.For(t);
            var m = new MeshData();
            float low = float.MaxValue;
            for (int i = 0; i < l.Spans.Count; i++)
            {
                if (Array.IndexOf(ways, l.Spans[i].Record.OsmWayId) < 0) continue;
                BridgeMesher.BuildSpan(l, i, l.Ground, new BridgeOptions { Lod = lod }, m);
                for (int k = 0; k < l.Spans[i].Count; k++) low = Math.Min(low, l.Spans[i].Ground[k]);
            }
            if (m.VertexCount > 0) BridgeScenes.WriteObj(Path.Combine(dir, name + ".obj"), m, t, margin, water ? low + 0.6 : -1e9, l);
        }

        private static void SampleGroup(string dir, string name, int tx, int ty, int lod, ulong way)
        {
            TileData t = StreamingSampleRegion.Tile(new TileId(10, tx, ty));
            BridgeLayout l = BridgeLayout.For(t);
            BridgeSpan seed = l.Spans.Find(s => s.Record.OsmWayId == way);
            if (seed == null) return;
            var m = new MeshData();
            for (int i = 0; i < l.Spans.Count; i++)
                if (l.Spans[i].GroupWay == seed.GroupWay) BridgeMesher.BuildSpan(l, i, l.Ground, new BridgeOptions { Lod = lod }, m);
            BridgeScenes.WriteObj(Path.Combine(dir, name + ".obj"), m, t, 15, -1e9, l);
        }

        private static void Write(string dir, string name, TileData t, int lod, double water)
        {
            MeshData m = Mesh(t, lod);
            BridgeScenes.WriteObj(Path.Combine(dir, name + ".obj"), m, t, 20, water, BridgeLayout.For(t));
        }
    }
}
