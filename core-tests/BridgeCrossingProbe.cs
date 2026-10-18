using System;
using System.Collections.Generic;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Streaming;
using Ghumante.World.Streaming;
using LodPiece = Ghumante.World.Streaming.LodPiece;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The drawn world around the sample's bridges, built as the game streams it (<see cref="TileBuild.Execute"/> with
    /// <see cref="MeshingSettings.ForTier"/>, the ground query with the bridge deck and corridor factories, the tiles'
    /// hero and dressing colliders registered like the streamer does), plus the drawn surfaces (terrain, areas, road
    /// ribbons and the bridge pieces at LOD0) to tell where a walker's feet are inside the geometry it sees.
    /// </summary>
    internal sealed class BridgeWorld
    {
        public readonly MeshingSettings Settings;
        public readonly TileGroundQuery Ground;
        public readonly Dictionary<TileId, TileBuild> Builds = new Dictionary<TileId, TileBuild>();
        private readonly DrawnSurface _drawn = new DrawnSurface();

        private static readonly Dictionary<string, BridgeWorld> Cache = new Dictionary<string, BridgeWorld>();
        private static readonly object Lock = new object();

        /// <summary>Register the tiles' dressing colliders (poles, trees, parked vehicles: other packages) as the streamer
        /// does; off (BRIDGE_NO_DRESSING=1) to look at the structures alone.</summary>
        public static bool WithDressing = Environment.GetEnvironmentVariable("BRIDGE_NO_DRESSING") != "1";

        private BridgeWorld(int tier)
        {
            Settings = StreamingScene.Settings(tier, 10);
            Settings.DrawBuildings = false; // house solids come from the footprints (TileSolids); the meshes are not needed
            Ground = StreamingScene.Ground(Settings);
        }

        /// <summary>A world of every level-10 tile within <paramref name="ring"/> tiles of the given ones (cached).</summary>
        public static BridgeWorld Around(IEnumerable<TileId> centres, int ring = 1, int tier = 2)
        {
            var want = new SortedSet<ulong>();
            var ids = new Dictionary<ulong, TileId>();
            foreach (TileId c in centres)
            for (int dy = -ring; dy <= ring; dy++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                var id = new TileId(c.Level, c.Tx + dx, c.Ty + dy);
                if (StreamingSampleRegion.Tile(id) == null) continue;
                want.Add(id.Key);
                ids[id.Key] = id;
            }
            var key = new StringBuilder().Append(tier).Append(WithDressing ? 'd' : 'n').Append(':');
            foreach (ulong k in want) key.Append(k).Append(',');
            lock (Lock)
            {
                BridgeWorld w;
                if (Cache.TryGetValue(key.ToString(), out w)) return w;
                w = new BridgeWorld(tier);
                foreach (ulong k in want) w.AddTile(ids[k]);
                Cache[key.ToString()] = w;
                return w;
            }
        }

        private readonly HashSet<long> _bridgeSolids = new HashSet<long>();

        private static long SolidKey(double x, double z)
        {
            return ((long)Math.Round(x * 20) << 32) ^ (long)Math.Round(z * 20);
        }

        /// <summary>True when a tile solid comes from the bridges package (<see cref="BridgeColliders"/>), by its start point.</summary>
        public bool IsBridgeSolid(in SolidPrim p)
        {
            return _bridgeSolids.Contains(SolidKey(p.Ax, p.Az));
        }

        private void AddTile(TileId id)
        {
            TileBuild b = StreamingScene.Build(id, Settings, Ground);
            Builds[id] = b;
            if (b.Source.HasDetail)
            {
                BridgeColliders bc = BridgeColliders.For(BridgeLayout.For(b.Source));
                foreach (BridgeColliders.Wall wl in bc.Walls) _bridgeSolids.Add(SolidKey(id.X0 + wl.X0, id.Z0 + wl.Z0));
                foreach (BridgeColliders.Column cl in bc.Columns) _bridgeSolids.Add(SolidKey(id.X0 + cl.CX, id.Z0 + cl.CZ));
            }
            Ground.Add(id, b.Sampler, b.Roads);
            if (b.HeroColliders != null && b.HeroColliders.Count > 0) Ground.Register(id.Key, b.HeroColliders);
            if (WithDressing && b.Colliders != null && b.Colliders.Count > 0) Ground.Register(id.Key | TileGroundQuery.StaticSetKeyBit, b.Colliders);
            double x0 = id.X0, z0 = id.Z0;
            // (The layers as uploaded: a layer's indices are relative to its upload chunk.)
            _drawn.Add(StreamingScene.Layer(b, TileLayers.Terrain), x0, z0, 0);
            _drawn.Add(StreamingScene.Layer(b, TileLayers.Areas), x0, z0, 1);
            _drawn.Add(StreamingScene.Layer(b, TileLayers.Roads), x0, z0, 2);
            // The bridge pieces at LOD0 (what a walker standing on them sees).
            var lod0 = new HashSet<int>();
            foreach (LodPiece p in b.Pieces)
                if (p.Layer == TileLayers.Bridges) lod0.Add(p.Index * 4);
            _drawn.Add(StreamingScene.Layer(b, TileLayers.Bridges, lod0.Contains), x0, z0, 3);
        }

        /// <summary>What blocks a body with feet at <paramref name="y"/> within <paramref name="r"/> of (x, z): tile solids
        /// (footprints, railings, retaining walls) and registered colliders, as text (diagnostics).</summary>
        public string Blockers(double x, double z, float y, double r = 1.0)
        {
            var sb = new StringBuilder();
            float low = y + TileGroundQuery.StepUpM, high = y + 1.6f;
            var prims = new List<SolidPrim>();
            foreach (KeyValuePair<TileId, TileBuild> kv in Builds)
            {
                prims.Clear();
                Ground.SolidsOf(kv.Key, prims);
                foreach (SolidPrim p in prims)
                {
                    double qx, qz;
                    SolidSet.Closest(p.Ax, p.Az, p.Bx, p.Bz, x, z, out qx, out qz);
                    double d = Math.Sqrt((qx - x) * (qx - x) + (qz - z) * (qz - z)) - p.Radius;
                    if (d > r || p.Top <= low || p.Bottom >= high) continue;
                    sb.Append("   solid ").Append(kv.Key).Append(" (").Append(p.Ax.ToString("F1")).Append(",").Append(p.Az.ToString("F1")).Append(")-(")
                      .Append(p.Bx.ToString("F1")).Append(",").Append(p.Bz.ToString("F1")).Append(") r ").Append(p.Radius.ToString("F2")).Append(" y ")
                      .Append(p.Bottom.ToString("F2")).Append("..").Append(p.Top.ToString("F2")).Append(" grp ").Append(p.Group).Append(" d ").Append(d.ToString("F2"))
                      .Append(IsBridgeSolid(in p) ? " [bridge]" : p.Group < 0 ? " " + LoweredNear(kv.Value.Source, Ground.RoadIndexOf(kv.Key), 0.5 * (p.Ax + p.Bx), 0.5 * (p.Az + p.Bz)) : "").Append('\n');
                }
                foreach (StructureColliders c in new[] { kv.Value.HeroColliders, kv.Value.Colliders })
                {
                    if (c == null) continue;
                    double x0 = kv.Key.X0, z0 = kv.Key.Z0;
                    foreach (ColliderWall wl in c.Walls)
                    {
                        double qx, qz;
                        SolidSet.Closest(x0 + wl.X0, z0 + wl.Z0, x0 + wl.X1, z0 + wl.Z1, x, z, out qx, out qz);
                        double d = Math.Sqrt((qx - x) * (qx - x) + (qz - z) * (qz - z)) - wl.HalfThickness;
                        if (d > r || wl.Top <= low || wl.Bottom >= high) continue;
                        sb.Append("   wall ").Append(d.ToString("F2")).Append(" y ").Append(wl.Bottom.ToString("F2")).Append("..").Append(wl.Top.ToString("F2")).Append('\n');
                    }
                    foreach (ColliderCylinder cy in c.Cylinders)
                    {
                        double d = Math.Sqrt((x0 + cy.CX - x) * (x0 + cy.CX - x) + (z0 + cy.CZ - z) * (z0 + cy.CZ - z)) - cy.Radius;
                        if (d > r || cy.Top <= low || cy.Bottom >= high) continue;
                        sb.Append("   cylinder r ").Append(cy.Radius.ToString("F2")).Append(" d ").Append(d.ToString("F2")).Append(" y ").Append(cy.Bottom.ToString("F2")).Append("..").Append(cy.Top.ToString("F2")).Append('\n');
                    }
                    foreach (OrientedBox b in c.Boxes)
                    {
                        double d = Math.Sqrt((x0 + b.CX - x) * (x0 + b.CX - x) + (z0 + b.CZ - z) * (z0 + b.CZ - z)) - Math.Max(b.HalfX, b.HalfZ);
                        if (d > r || b.Top <= low || b.Bottom >= high) continue;
                        sb.Append("   box ").Append(b.HalfX.ToString("F1")).Append("x").Append(b.HalfZ.ToString("F1")).Append(" d<= ").Append(d.ToString("F2")).Append(" y ").Append(b.Bottom.ToString("F2")).Append("..").Append(b.Top.ToString("F2")).Append(" ").Append(b.Flags).Append('\n');
                    }
                }
            }
            sb.Append("   IsBlocked here: ").Append(Ground.IsBlocked(x, z, y)).Append('\n');
            return sb.ToString();
        }

        /// <summary>What stopped a body with feet at <paramref name="y"/> heading (dx, dz) at (x, z), in one word: "bridge"
        /// (a solid of the bridges package), "dressing" (a registered pole, tree or parked vehicle), "house" (a footprint),
        /// "solid" (another tile solid: a road-based railing or wall), "overhead" (a deck or slab at body height),
        /// "step" (the ground ahead too high to climb), "drop" (no ground ahead) or "other".</summary>
        public string StopCause(double x, double z, float y, double dx, double dz, float climb)
        {
            float low = y + TileGroundQuery.StepUpM, high = y + 1.6f;
            // The nearest blocker within 0.6 m of the point 0.3 m ahead names the cause.
            double best = 0.6;
            string cause = null;
            var prims = new List<SolidPrim>();
            double ax = x + dx * 0.3, az = z + dz * 0.3;
            foreach (KeyValuePair<TileId, TileBuild> kv in Builds)
            {
                prims.Clear();
                Ground.SolidsOf(kv.Key, prims);
                foreach (SolidPrim p in prims)
                {
                    double qx, qz;
                    SolidSet.Closest(p.Ax, p.Az, p.Bx, p.Bz, ax, az, out qx, out qz);
                    double d = Math.Sqrt((qx - ax) * (qx - ax) + (qz - az) * (qz - az)) - p.Radius;
                    if (d > best || p.Top <= low || p.Bottom >= high) continue;
                    best = d;
                    cause = IsBridgeSolid(in p) ? "bridge" : p.Group >= 0 ? "house" : "solid";
                }
                StructureColliders c = kv.Value.Colliders;
                if (c == null) continue;
                double x0 = kv.Key.X0, z0 = kv.Key.Z0;
                foreach (ColliderCylinder cy in c.Cylinders)
                {
                    double d = Math.Sqrt((x0 + cy.CX - ax) * (x0 + cy.CX - ax) + (z0 + cy.CZ - az) * (z0 + cy.CZ - az)) - cy.Radius;
                    if (d > best || cy.Top <= low || cy.Bottom >= high) continue;
                    best = d;
                    cause = "dressing";
                }
                foreach (OrientedBox b in c.Boxes)
                {
                    double d = Math.Sqrt((x0 + b.CX - ax) * (x0 + b.CX - ax) + (z0 + b.CZ - az) * (z0 + b.CZ - az)) - Math.Max(b.HalfX, b.HalfZ);
                    if (d > best || b.Top <= low || b.Bottom >= high) continue;
                    best = d;
                    cause = "dressing";
                }
            }
            if (cause != null) return cause;
            if (Ground.InsideSolid(ax, az, y)) return "house";
            GroundSample g;
            if (!Ground.TrySample(ax, az, y, out g))
            {
                GroundSample g2;
                return Ground.TrySample(ax, az, y + 2f, out g2) ? "overhead" : "drop";
            }
            if (g.Height - y > climb) return "step";
            return "other";
        }

        /// <summary>The road with structure heights or an elevated deck whose centreline passes nearest (x, z) (the road a
        /// railing or retaining wall there most likely follows), as "way@distance".</summary>
        private static string LoweredNear(TileData t, RoadSpatialIndex idx, double x, double z)
        {
            if (idx == null) return "";
            double best = double.MaxValue;
            ulong way = 0;
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                if (idx.SurfaceHeights(ri) == null && !idx.IsElevated(ri)) continue;
                int[] pt = t.Roads[ri].Points;
                for (int k = 0; k + 3 < pt.Length; k += 2)
                {
                    double ax = t.Tile.X0 + pt[k] / 100.0, az = t.Tile.Z0 + pt[k + 1] / 100.0, bx = t.Tile.X0 + pt[k + 2] / 100.0, bz = t.Tile.Z0 + pt[k + 3] / 100.0;
                    double qx, qz;
                    SolidSet.Closest(ax, az, bx, bz, x, z, out qx, out qz);
                    double d = Math.Sqrt((qx - x) * (qx - x) + (qz - z) * (qz - z));
                    if (d < best)
                    {
                        best = d;
                        way = t.Roads[ri].OsmWayId;
                    }
                }
            }
            return way != 0 ? "road " + way + "@" + best.ToString("F1") : "";
        }

        /// <summary>Why the ground query answers what it does at (x, z) for feet at <paramref name="y"/>: every tile's deck
        /// index (the deck within reach, the deck seen from head height, the soffit over the step-up) and structure-height
        /// slabs, the way <c>TileGroundQuery.CeilingBlocks</c> reads them (diagnostics).</summary>
        public string Explain(double x, double z, float y, float bodyH = TileGroundQuery.BodyHeightM)
        {
            var sb = new StringBuilder();
            GroundSample g;
            bool got = Ground.TrySample(x, z, y, out g);
            sb.Append("   explain (").Append(x.ToString("F2")).Append(", ").Append(z.ToString("F2")).Append(") feet ").Append(y.ToString("F2")).Append(": ground ")
              .Append(got ? g.Height.ToString("F2") + (g.OnDeck ? " deck" : "") + (g.OnStructure ? " struct" : "") + (g.OnRoad ? " road" : "") : "none").Append('\n');
            RoadHit rh;
            if (Ground.TryNearestRoad(x, z, RoadSpatialIndex.MaxFootpathM, RoadFlags.None, RoadFlags.None, RoadLayer.Ground, out rh))
            {
                RoadSpatialIndex ri = Ground.RoadIndexOf(rh.Tile.Tile);
                float sy = 0f, sg;
                bool hs = ri != null && ri.TrySurfaceHeight(in rh, out sy, out sg);
                sb.Append("   nearest ground-layer road ").Append(rh.Road.OsmWayId).Append(' ').Append(rh.Road.RoadClass).Append(" #").Append(rh.RoadIndex).Append(" seg ").Append(rh.Segment)
                  .Append(" t ").Append(rh.T.ToString("F3")).Append(" edge ").Append(rh.EdgeDistanceM.ToString("F2")).Append(" lat ").Append(rh.LateralM.ToString("F2")).Append('/').Append(rh.HalfWidthM.ToString("F2"))
                  .Append(" surface ").Append(hs ? sy.ToString("F2") : "-").Append('\n');
            }
            float low = y + TileGroundQuery.StepUpM, high = y + bodyH;
            foreach (KeyValuePair<TileId, TileBuild> kv in Builds)
            {
                if (x < kv.Key.X0 - 40 || z < kv.Key.Z0 - 40 || x > kv.Key.X0 + kv.Key.Size + 40 || z > kv.Key.Z0 + kv.Key.Size + 40) continue;
                BridgeDeckIndex d = BridgeDeckIndex.ForTile(kv.Value.Source);
                float dy, nx, ny, nz, hy, under;
                bool near = d.TryDeck(x, z, y, out dy, out nx, out ny, out nz);
                bool head = d.TryDeck(x, z, high, out hy, out nx, out ny, out nz);
                bool ceil = d.TryCeiling(x, z, low, out under);
                if (near || head || ceil)
                    sb.Append("   tile ").Append(kv.Key).Append(" deck@feet ").Append(near ? dy.ToString("F2") : "-").Append(" deck@head ").Append(head ? hy.ToString("F2") : "-")
                      .Append(head && hy > low && hy - RoadSpatialIndex.DeckThicknessM < high ? " (BLOCKS)" : "").Append(" soffit>step ").Append(ceil ? under.ToString("F2") : "-")
                      .Append(ceil && under < high ? " (BLOCKS)" : "").Append('\n');
                RoadSpatialIndex idx = Ground.RoadIndexOf(kv.Key);
                if (idx != null && idx.HasDecks && idx.SlabOverlaps(x, z, low, high)) sb.Append("   tile ").Append(kv.Key).Append(" structure-height slab BLOCKS\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Dumps the drawn layers (terrain, areas, roads, bridges at LOD0) and the collision solids (tile solids red,
        /// registered colliders blue, deck index samples green dots) within <paramref name="half"/> metres of world point
        /// (cx, cz) to OBJ files under <c>GHUMANTE_PREVIEW_DIR/name</c>, in metres relative to (cx, ·, cz). No-op unless
        /// the preview dir is set.
        /// </summary>
        public void Dump(string name, double cx, double cz, double half, string eye = null)
        {
            if (!ObjDump.Enabled) return;
            var layers = new[] { "terrain", "areas", "roads", "bridges" };
            var ids = new[] { TileLayers.Terrain, TileLayers.Areas, TileLayers.Roads, TileLayers.Bridges };
            var comments = eye != null ? new[] { "meshpreview: " + eye } : null;
            for (int li = 0; li < layers.Length; li++)
            {
                var m = new MeshData { HasUv0 = true };
                foreach (KeyValuePair<TileId, TileBuild> kv in Builds)
                {
                    TileBuild b = kv.Value;
                    MeshData src = StreamingScene.Layer(b, ids[li]);
                    if (ids[li] == TileLayers.Bridges)
                    {
                        var lod0 = new HashSet<int>();
                        foreach (LodPiece pc in b.Pieces)
                            if (pc.Layer == TileLayers.Bridges) lod0.Add(pc.Index * 4);
                        src = StreamingScene.Layer(b, TileLayers.Bridges, lod0.Contains);
                    }
                    float lx = (float)(cx - kv.Key.X0), lz = (float)(cz - kv.Key.Z0);
                    MeshData crop = ObjDump.Crop(src, lx - (float)half, lz - (float)half, lx + (float)half, lz + (float)half, true);
                    if (crop.VertexCount == 0) continue;
                    if (!crop.HasUv0) m.HasUv0 = false;
                    ObjDump.Append(m, crop, (float)(kv.Key.X0 - cx), 0f, (float)(kv.Key.Z0 - cz));
                }
                ObjDump.Write(m, name + "/" + layers[li] + ".obj", comments);
            }
            var solids = new MeshData();
            var houses = new MeshData();
            var prims = new List<SolidPrim>();
            foreach (KeyValuePair<TileId, TileBuild> kv in Builds)
            {
                prims.Clear();
                Ground.SolidsOf(kv.Key, prims);
                foreach (SolidPrim p in prims)
                {
                    if (p.Group >= 0)
                    {
                        // Footprint walls (the drawn houses are off in this world): pale boxes.
                        double hx = 0.5 * (p.Ax + p.Bx) - cx, hz = 0.5 * (p.Az + p.Bz) - cz;
                        if (Math.Abs(hx) < half && Math.Abs(hz) < half)
                            Slab(houses, p.Ax - cx, p.Az - cz, p.Bx - cx, p.Bz - cz, Math.Max(0.1f, p.Radius), p.Bottom, Math.Min(p.Top, p.Bottom + 9f), 0xE8DCC8FFu);
                        continue;
                    }
                    double mx = 0.5 * (p.Ax + p.Bx) - cx, mz = 0.5 * (p.Az + p.Bz) - cz;
                    if (Math.Abs(mx) > half || Math.Abs(mz) > half) continue;
                    Slab(solids, p.Ax - cx, p.Az - cz, p.Bx - cx, p.Bz - cz, Math.Max(0.05f, p.Radius), p.Bottom, p.Top, 0xD03030FFu);
                }
                double x0 = kv.Key.X0 - cx, z0 = kv.Key.Z0 - cz;
                foreach (StructureColliders c in new[] { kv.Value.HeroColliders, kv.Value.Colliders })
                {
                    if (c == null) continue;
                    foreach (ColliderWall wl in c.Walls)
                        if (Math.Abs(x0 + wl.X0) < half && Math.Abs(z0 + wl.Z0) < half)
                            Slab(solids, x0 + wl.X0, z0 + wl.Z0, x0 + wl.X1, z0 + wl.Z1, wl.HalfThickness, wl.Bottom, wl.Top, 0x3050D0FFu);
                    foreach (ColliderCylinder cy in c.Cylinders)
                        if (Math.Abs(x0 + cy.CX) < half && Math.Abs(z0 + cy.CZ) < half)
                            Slab(solids, x0 + cy.CX, z0 + cy.CZ, x0 + cy.CX, z0 + cy.CZ, cy.Radius, cy.Bottom, cy.Top, 0x3050D0FFu);
                    foreach (OrientedBox bx in c.Boxes)
                    {
                        if (Math.Abs(x0 + bx.CX) > half || Math.Abs(z0 + bx.CZ) > half) continue;
                        double ux = Math.Cos(bx.YawRad), uz = Math.Sin(bx.YawRad);
                        Slab(solids, x0 + bx.CX - ux * bx.HalfX, z0 + bx.CZ - uz * bx.HalfX, x0 + bx.CX + ux * bx.HalfX, z0 + bx.CZ + uz * bx.HalfX,
                             bx.HalfZ, bx.Bottom, bx.Top, 0x30A0D0FFu);
                    }
                }
            }
            ObjDump.Write(solids, name + "/solids.obj", comments);
            ObjDump.Write(houses, name + "/houses.obj", comments);
        }

        /// <summary>A box from a to b, half width <paramref name="r"/>, from <paramref name="y0"/> to <paramref name="y1"/>.</summary>
        internal static void Slab(MeshData m, double ax, double az, double bx, double bz, float r, float y0, float y1, uint rgba)
        {
            double dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
            double ux = l > 1e-6 ? dx / l : 1, uz = l > 1e-6 ? dz / l : 0;
            double nx = -uz * r, nz = ux * r;
            ax -= ux * r;
            az -= uz * r;
            bx += ux * r;
            bz += uz * r;
            double[] px = { ax + nx, bx + nx, bx - nx, ax - nx }, pz = { az + nz, bz + nz, bz - nz, az - nz };
            int v0 = m.VertexCount;
            for (int k = 0; k < 4; k++) m.AddVertex((float)px[k], y0, (float)pz[k], 0, 1, 0, rgba);
            for (int k = 0; k < 4; k++) m.AddVertex((float)px[k], y1, (float)pz[k], 0, 1, 0, rgba);
            // top, bottom and four sides (both windings: the preview may cull back faces)
            int[] q = { 4, 5, 6, 7, 0, 1, 2, 3, 0, 1, 5, 4, 1, 2, 6, 5, 2, 3, 7, 6, 3, 0, 4, 7 };
            for (int f = 0; f < q.Length; f += 4)
            {
                m.AddTriangle(v0 + q[f], v0 + q[f + 1], v0 + q[f + 2]);
                m.AddTriangle(v0 + q[f], v0 + q[f + 2], v0 + q[f + 3]);
                m.AddTriangle(v0 + q[f], v0 + q[f + 2], v0 + q[f + 1]);
                m.AddTriangle(v0 + q[f], v0 + q[f + 3], v0 + q[f + 2]);
            }
        }

        /// <summary>The source tile the world built (a fresh decode, like the streamer's).</summary>
        public TileData Tile(TileId id)
        {
            TileBuild b;
            return Builds.TryGetValue(id, out b) ? b.Source : null;
        }

        /// <summary>The highest drawn surface at (x, z) at or below <paramref name="maxY"/> (false: none).</summary>
        public bool FirstAbove(double x, double z, float y, out float hitY, out bool upFacing, out int layer)
        {
            return _drawn.FirstAbove(x, z, y, out hitY, out upFacing, out layer);
        }

        /// <summary>Every drawn surface at (x, z), top down (diagnostics).</summary>
        public string Column(double x, double z)
        {
            return _drawn.Column(x, z);
        }

        /// <summary>The highest drawn surface of a layer (0 terrain, 1 areas, 2 roads, 3 bridges) at (x, z), NaN for none.</summary>
        public float LayerTop(double x, double z, int layer)
        {
            return _drawn.LayerTop(x, z, layer);
        }

        public bool DrawnBelow(double x, double z, float maxY, out float y)
        {
            int layer;
            return _drawn.HighestBelow(x, z, maxY, out y, out layer);
        }

        /// <summary>As <see cref="DrawnBelow(double, double, float, out float)"/> with the layer that drew it (0 terrain,
        /// 1 areas, 2 roads, 3 bridges).</summary>
        public bool DrawnBelow(double x, double z, float maxY, out float y, out int layer)
        {
            return _drawn.HighestBelow(x, z, maxY, out y, out layer);
        }
    }

    /// <summary>Triangles bucketed in a 4 m grid (world plan metres) for vertical ray queries.</summary>
    internal sealed class DrawnSurface
    {
        private const double Cell = 4.0;
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly List<double> _tri = new List<double>(); // x y z × 3
        private readonly List<int> _layer = new List<int>();
        private readonly List<float> _ny = new List<float>(); // mean vertex normal y (up-facing > 0)
        private readonly List<int> _channel = new List<int>(); // material channel of the first vertex (-1: no UV0)

        private static long Key(int i, int j)
        {
            return ((long)i << 32) ^ (uint)j;
        }

        public void Add(MeshData m, double x0, double z0, int layer)
        {
            for (int k = 0; k + 2 < m.IndexCount; k += 3)
            {
                int t = _tri.Count / 9;
                _layer.Add(layer);
                _ny.Add((m.Normals[3 * m.Indices[k] + 1] + m.Normals[3 * m.Indices[k + 1] + 1] + m.Normals[3 * m.Indices[k + 2] + 1]) / 3f);
                _channel.Add(m.HasUv0 ? (int)Math.Round(m.Uv0[2 * m.Indices[k]]) : -1);
                double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
                for (int c = 0; c < 3; c++)
                {
                    int v = m.Indices[k + c];
                    double x = x0 + m.Positions[3 * v], y = m.Positions[3 * v + 1], z = z0 + m.Positions[3 * v + 2];
                    _tri.Add(x);
                    _tri.Add(y);
                    _tri.Add(z);
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minZ = Math.Min(minZ, z);
                    maxZ = Math.Max(maxZ, z);
                }
                int i0 = (int)Math.Floor(minX / Cell), i1 = (int)Math.Floor(maxX / Cell);
                int j0 = (int)Math.Floor(minZ / Cell), j1 = (int)Math.Floor(maxZ / Cell);
                if ((i1 - i0 + 1) * (j1 - j0 + 1) > 4096) continue; // a skirt or a huge sliver: not a walking surface
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    List<int> l;
                    if (!_cells.TryGetValue(Key(i, j), out l)) _cells[Key(i, j)] = l = new List<int>();
                    l.Add(t);
                }
            }
        }

        /// <summary>The first drawn surface straight above (x, y, z): its height, whether it faces up (the point is inside
        /// the geometry under its top) and its layer.</summary>
        public bool FirstAbove(double x, double z, float y, out float hitY, out bool upFacing, out int layer)
        {
            hitY = float.PositiveInfinity;
            upFacing = false;
            layer = -1;
            List<int> l;
            if (!_cells.TryGetValue(Key((int)Math.Floor(x / Cell), (int)Math.Floor(z / Cell)), out l)) return false;
            foreach (int t in l)
            {
                double h;
                if (!At(t, x, z, out h) || h <= y || h >= hitY) continue;
                if (Math.Abs(_ny[t]) < 0.2f) continue; // a wall face: no cap above
                hitY = (float)h;
                upFacing = _ny[t] > 0;
                layer = _layer[t];
            }
            return !float.IsPositiveInfinity(hitY);
        }

        private bool At(int t, double x, double z, out double h)
        {
            h = 0;
            int o = 9 * t;
            double ax = _tri[o], ay = _tri[o + 1], az = _tri[o + 2];
            double bx = _tri[o + 3], by = _tri[o + 4], bz = _tri[o + 5];
            double cx = _tri[o + 6], cy = _tri[o + 7], cz = _tri[o + 8];
            double den = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
            if (Math.Abs(den) < 1e-12) return false;
            double w0 = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) / den;
            double w1 = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) / den;
            double w2 = 1 - w0 - w1;
            if (w0 < -1e-9 || w1 < -1e-9 || w2 < -1e-9) return false;
            h = w0 * ay + w1 * by + w2 * cy;
            return true;
        }

        /// <summary>Every drawn surface straight above or below (x, z), top down: height, layer (TARB) and the facing
        /// (diagnostics).</summary>
        public string Column(double x, double z)
        {
            var hits = new List<KeyValuePair<double, string>>();
            List<int> l;
            if (_cells.TryGetValue(Key((int)Math.Floor(x / Cell), (int)Math.Floor(z / Cell)), out l))
                foreach (int t in l)
                {
                    double h;
                    if (!At(t, x, z, out h)) continue;
                    hits.Add(new KeyValuePair<double, string>(h, h.ToString("F2") + "TARB"[_layer[t]] + (_ny[t] > 0.2f ? "^" : _ny[t] < -0.2f ? "v" : "|")
                                                                    + (Verbose ? "{" + (_channel[t] >= 0 ? ((Ghumante.Core.Meshing.MaterialChannel)_channel[t]).ToString() : "-") + " " + Tri(t) + "}" : "")));
                }
            hits.Sort((a, b) => b.Key.CompareTo(a.Key));
            var sb = new StringBuilder();
            foreach (KeyValuePair<double, string> kv in hits) sb.Append(' ').Append(kv.Value);
            return sb.ToString();
        }

        /// <summary>Column prints each triangle's material and corners (diagnostics).</summary>
        public static bool Verbose = Environment.GetEnvironmentVariable("BRIDGE_VERBOSE") == "1";

        private string Tri(int t)
        {
            int o = 9 * t;
            var sb = new StringBuilder();
            for (int c = 0; c < 3; c++)
                sb.Append('(').Append(_tri[o + 3 * c].ToString("F1")).Append(',').Append(_tri[o + 3 * c + 2].ToString("F1")).Append(' ').Append(_tri[o + 3 * c + 1].ToString("F2")).Append(')');
            return sb.ToString();
        }

        /// <summary>The highest surface of one layer at (x, z) (NaN: none).</summary>
        public float LayerTop(double x, double z, int layer)
        {
            float best = float.NaN;
            List<int> l;
            if (!_cells.TryGetValue(Key((int)Math.Floor(x / Cell), (int)Math.Floor(z / Cell)), out l)) return best;
            foreach (int t in l)
            {
                double h;
                if (_layer[t] != layer || !At(t, x, z, out h)) continue;
                if (float.IsNaN(best) || h > best) best = (float)h;
            }
            return best;
        }

        public bool HighestBelow(double x, double z, float maxY, out float y, out int layer)
        {
            y = float.NegativeInfinity;
            layer = -1;
            List<int> l;
            if (!_cells.TryGetValue(Key((int)Math.Floor(x / Cell), (int)Math.Floor(z / Cell)), out l)) return false;
            bool found = false;
            foreach (int t in l)
            {
                int o = 9 * t;
                double ax = _tri[o], ay = _tri[o + 1], az = _tri[o + 2];
                double bx = _tri[o + 3], by = _tri[o + 4], bz = _tri[o + 5];
                double cx = _tri[o + 6], cy = _tri[o + 7], cz = _tri[o + 8];
                double den = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
                if (Math.Abs(den) < 1e-12) continue; // vertical
                double w0 = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) / den;
                double w1 = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) / den;
                double w2 = 1 - w0 - w1;
                if (w0 < -1e-9 || w1 < -1e-9 || w2 < -1e-9) continue;
                double h = w0 * ay + w1 * by + w2 * cy;
                if (h > maxY || h <= y) continue;
                y = (float)h;
                layer = _layer[t];
                found = true;
            }
            return found;
        }
    }

    /// <summary>What happened on one walk or ride along a line of waypoints.</summary>
    internal struct ProbeRun
    {
        public bool Reached;
        public int Steps;

        /// <summary>Most the drawn surface stood above the feet (sunk into the geometry), and where.</summary>
        public float WorstSink;
        public double SinkX, SinkZ;
        public int SinkLayer;
        public bool SinkOnDeck;

        /// <summary>Worst sink where the deck carries the body or the bridge layer draws the surface.</summary>
        public float BridgeSink;

        /// <summary>Most the feet floated above the drawn surface under them.</summary>
        public float WorstFloat;

        /// <summary>Highest and lowest feet height.</summary>
        public float MaxY, MinY;

        public StepEvents Events;
        public double StopX, StopZ;
        public float StopY;

        /// <summary>Unit direction towards the waypoint the run was heading for when it stopped.</summary>
        public double StopDirX, StopDirZ;

        /// <summary>Body heading at the stop and the body that was driven (for sweeping it again).</summary>
        public float StopHeading;
        public CollisionBody Body;
        public bool EverOnDeck;

        /// <summary>What stopped the run (<see cref="BridgeWorld.StopCause"/>), null when it arrived.</summary>
        public string Cause;

        public override string ToString()
        {
            return (Reached ? "ok" : "STOPPED at (" + StopX.ToString("F1") + ", " + StopZ.ToString("F1") + ", y " + StopY.ToString("F2") + ")")
                   + " bsink " + BridgeSink.ToString("F2") + " sink " + WorstSink.ToString("F2") + (WorstSink > 0.05f ? "[" + "TARB"[SinkLayer < 0 ? 0 : SinkLayer] + (SinkOnDeck ? "d" : "g") + " " + SinkX.ToString("F0") + "," + SinkZ.ToString("F0") + "]" : "") + " float " + WorstFloat.ToString("F2") + " y " + MinY.ToString("F1") + ".." + MaxY.ToString("F1")
                   + (EverOnDeck ? " deck" : "") + ((Events & StepEvents.HitWall) != 0 ? " hitwall" : "") + ((Events & StepEvents.Landed) != 0 ? " landed" : "")
                   + ((Events & StepEvents.StuckRecovered) != 0 ? " stuck" : "") + (Cause != null ? " cause " + Cause : "");
        }
    }

    /// <summary>Drives an <see cref="ArcadeVehicle"/> (walker or vehicle) along waypoints over the probe world, the way
    /// the player steers: the stick towards the next waypoint.</summary>
    internal static class BridgeProbe
    {
        public const float Dt = 1f / 60f;

        /// <summary>Feet this far below the drawn surface count as sunk in (the owner's "sucked in").</summary>
        public const float SinkM = 0.12f;

        public static ProbeRun Follow(BridgeWorld w, VehicleSpec spec, IList<double> pts, float throttle, float startY = float.NaN,
                                      float maxSeconds = 120f, StringBuilder trace = null)
        {
            double tracedX = double.NaN, tracedZ = double.NaN;
            var run = new ProbeRun { MaxY = float.MinValue, MinY = float.MaxValue };
            var v = new ArcadeVehicle(spec);
            double x = pts[0], z = pts[1];
            float heading = (float)Math.Atan2(pts[2] - x, pts[3] - z);
            v.Teleport(x, z, heading, w.Ground);
            if (!float.IsNaN(startY))
            {
                // Start on the level asked (under a deck: the road below).
                GroundSample s;
                if (w.Ground.TrySample(x, z, startY, out s)) v.Teleport(x, z, heading, new Fixed(s));
                v.Y = s.Height;
            }
            if (!v.HasGround)
            {
                run.StopX = x;
                run.StopZ = z;
                return run;
            }
            int target = 1;
            int n = pts.Count / 2;
            double reach = spec.TurnInPlace ? 0.8 : 2.5;
            int steps = (int)(maxSeconds / Dt);
            double lastProgressX = v.X, lastProgressZ = v.Z;
            float sinceProgress = 0f;
            int stalls = 0;
            for (int i = 0; i < steps; i++)
            {
                double tx = pts[2 * target], tz = pts[2 * target + 1];
                double dx = tx - v.X, dz = tz - v.Z;
                // Next waypoint when close, or when it lies behind (passed beside it).
                while (target < n - 1)
                {
                    double ux = pts[2 * target] - pts[2 * target - 2], uz = pts[2 * target + 1] - pts[2 * target - 1];
                    bool passed = dx * ux + dz * uz < 0;
                    if (dx * dx + dz * dz > reach * reach && !passed) break;
                    target++;
                    tx = pts[2 * target];
                    tz = pts[2 * target + 1];
                    dx = tx - v.X;
                    dz = tz - v.Z;
                }
                if (target == n - 1 && dx * dx + dz * dz < reach * reach)
                {
                    run.Reached = true;
                    break;
                }
                float want = (float)Math.Atan2(dx, dz);
                double bx = v.X, bz = v.Z;
                float by = v.Y;
                StepEvents sev = v.Step(new DriveInput(throttle, 0f, v.SteerToward(want, Dt)), Dt, w.Ground, 0f);
                run.Events |= sev;
                run.Steps++;
                if (trace != null && stalls < 6 && (v.X - bx) * (v.X - bx) + (v.Z - bz) * (v.Z - bz) < 1e-8 && i > 30)
                {
                    stalls++;
                    trace.Append("STALL step ").Append(i).Append(" at (").Append(bx.ToString("F2")).Append(", ").Append(bz.ToString("F2")).Append(") y ").Append(by.ToString("F2"))
                         .Append(" heading ").Append(v.HeadingRad.ToString("F2")).Append(" want ").Append(want.ToString("F2")).Append(" v ").Append(v.SpeedMps.ToString("F2"))
                         .Append(" ev ").Append(sev).Append('\n');
                    double ax = bx + Math.Sin(v.HeadingRad) * 0.001, az = bz + Math.Cos(v.HeadingRad) * 0.001;
                    trace.Append(w.Explain(ax, az, by, v.Body.HeightM));
                    float t, nx, nz;
                    bool hit = w.Ground.SweepBody(v.Body, bx, bz, v.HeadingRad, by, Math.Sin(v.HeadingRad) * 0.1, Math.Cos(v.HeadingRad) * 0.1, out t, out nx, out nz);
                    GroundSample ga;
                    bool gotA = w.Ground.TrySample(ax, az, by, out ga);
                    trace.Append("   sweep ").Append(hit ? "hit t " + t.ToString("F2") + " n (" + nx.ToString("F2") + ", " + nz.ToString("F2") + ")" : "clear")
                         .Append(" ground ahead ").Append(gotA ? ga.Height.ToString("F2") + " n.y " + ga.Ny.ToString("F2") + (ga.OnDeck ? " deck" : "") : "none").Append('\n')
                         .Append(w.Blockers(ax, az, by, 0.5));
                }
                if (v.Ground.OnDeck) run.EverOnDeck = true;
                run.MaxY = Math.Max(run.MaxY, v.Y);
                run.MinY = Math.Min(run.MinY, v.Y);
                if (!v.Airborne)
                {
                    float top, above;
                    int layer, alayer;
                    bool up;
                    // Sunk: the first drawn surface straight above the feet faces up (the feet are inside the geometry under
                    // its top) within two metres (a whole body: the owner's "disappears"). Floating: the feet stand clear above everything drawn under them.
                    float sink = w.FirstAbove(v.X, v.Z, v.Y + 0.02f, out above, out up, out alayer) && up && above - v.Y < 2.0f ? above - v.Y : 0f;
                    bool below = w.DrawnBelow(v.X, v.Z, v.Y + 0.02f, out top, out layer);
                    if (sink > 0f) layer = alayer;
                    if ((layer == 3 || v.Ground.OnDeck) && sink > run.BridgeSink) run.BridgeSink = sink;
                    if (sink > run.WorstSink)
                    {
                        run.WorstSink = sink;
                        run.SinkX = v.X;
                        run.SinkZ = v.Z;
                        run.SinkLayer = layer;
                        run.SinkOnDeck = v.Ground.OnDeck;
                    }
                    if (below && v.Y - top > run.WorstFloat) run.WorstFloat = v.Y - top;
                }
                if (trace != null && (double.IsNaN(tracedX) || (v.X - tracedX) * (v.X - tracedX) + (v.Z - tracedZ) * (v.Z - tracedZ) > 0.25))
                {
                    tracedX = v.X;
                    tracedZ = v.Z;
                    GroundSample g = v.Ground;
                    float top;
                    int layer;
                    bool drawn = w.DrawnBelow(v.X, v.Z, v.Y + 1.0f, out top, out layer);
                    float th;
                    w.Ground.TryTerrainHeight(v.X, v.Z, out th);
                    trace.Append(i).Append(" (").Append(v.X.ToString("F1")).Append(", ").Append(v.Z.ToString("F1")).Append(") y ").Append(v.Y.ToString("F2"))
                         .Append(" gnd ").Append(g.Height.ToString("F2")).Append(g.OnDeck ? " DECK" : "").Append(g.OnRoad ? " road" : "")
                         .Append(g.OnFootpath ? " foot" : "").Append(g.OnStructure ? " struct" : "").Append(" ").Append(g.RoadClass)
                         .Append(" off ").Append(g.RoadOffsetM.ToString("F1")).Append("/").Append(g.RoadHalfWidthM.ToString("F1"))
                         .Append(" drawn ").Append(drawn ? top.ToString("F2") + "TARB"[layer] : "-").Append(" terr ").Append(th.ToString("F2"))
                         .Append(" v ").Append(v.SpeedMps.ToString("F1")).Append('\n');
                }
                double px = v.X - lastProgressX, pz = v.Z - lastProgressZ;
                if (px * px + pz * pz > 1.0)
                {
                    lastProgressX = v.X;
                    lastProgressZ = v.Z;
                    sinceProgress = 0f;
                }
                else if ((sinceProgress += Dt) > 4f)
                {
                    break; // stuck: no metre of progress in four seconds
                }
            }
            run.StopX = v.X;
            run.StopZ = v.Z;
            run.StopY = v.Y;
            run.StopHeading = v.HeadingRad;
            run.Body = v.Body;
            {
                double ddx = pts[2 * target] - v.X, ddz = pts[2 * target + 1] - v.Z, dl = Math.Sqrt(ddx * ddx + ddz * ddz);
                if (dl > 1e-9)
                {
                    run.StopDirX = ddx / dl;
                    run.StopDirZ = ddz / dl;
                }
            }
            if (!run.Reached) run.Cause = w.StopCause(run.StopX, run.StopZ, run.StopY, run.StopDirX, run.StopDirZ, spec.TurnInPlace ? ArcadeVehicle.WalkerMaxClimbM : ArcadeVehicle.MaxClimbM);
            if (trace != null && !run.Reached) trace.Append("blockers at the stop:\n").Append(w.Blockers(v.X, v.Z, v.Y));
            return run;
        }

        /// <summary>What stopped a run (the solids and colliders at the stop), or nothing when it arrived.</summary>
        public static string Why(BridgeWorld w, ProbeRun r)
        {
            if (r.Reached) return "";
            var sb = new StringBuilder();
            sb.Append("      stop (").Append(r.StopX.ToString("F1")).Append(", ").Append(r.StopZ.ToString("F1")).Append(") y ").Append(r.StopY.ToString("F2"))
              .Append(" heading (").Append(r.StopDirX.ToString("F2")).Append(", ").Append(r.StopDirZ.ToString("F2")).Append(") ground ahead:");
            double firstX = double.NaN;
            foreach (double a in new[] { 0.0, 0.3, 0.6, 1.0, 1.5 })
            {
                double x = r.StopX + r.StopDirX * a, z = r.StopZ + r.StopDirZ * a;
                GroundSample g;
                bool got = w.Ground.TrySample(x, z, r.StopY, out g);
                if (!got && double.IsNaN(firstX)) firstX = a;
                sb.Append(' ').Append(a.ToString("F1")).Append('=').Append(got ? g.Height.ToString("F2") + (g.OnDeck ? "d" : "") + (g.OnStructure ? "s" : "") + (g.OnFootpath ? "f" : "")
                                                                             + (g.Ny < 0.766f ? "!ny" + g.Ny.ToString("F2") : "") : "X");
            }
            sb.Append('\n').Append(w.Blockers(r.StopX + r.StopDirX * 0.3, r.StopZ + r.StopDirZ * 0.3, r.StopY, 0.6));
            double ea = double.IsNaN(firstX) ? 0.3 : firstX;
            sb.Append(w.Explain(r.StopX + r.StopDirX * ea, r.StopZ + r.StopDirZ * ea, r.StopY, r.Body.HeightM));
            float t, nx, nz;
            bool hit = w.Ground.SweepBody(r.Body, r.StopX, r.StopZ, r.StopHeading, r.StopY, r.StopDirX * 0.3, r.StopDirZ * 0.3, out t, out nx, out nz);
            sb.Append("   sweep 0.3 m ahead: ").Append(hit ? "hit t " + t.ToString("F2") + " n (" + nx.ToString("F2") + ", " + nz.ToString("F2") + ")" : "clear").Append('\n');
            return sb.ToString();
        }

        /// <summary>A ground query that answers one sample everywhere (to teleport onto a chosen level).</summary>
        private sealed class Fixed : IGroundQuery
        {
            private readonly GroundSample _s;

            public Fixed(GroundSample s)
            {
                _s = s;
            }

            public bool TrySample(double x, double z, out GroundSample s)
            {
                s = _s;
                return true;
            }
        }

        // ---- lines ----

        /// <summary>Where on the deck a line runs: the centreline, the middle of the left carriageway half (a lane,
        /// clear of a median) or the middle of the left walkway.</summary>
        public enum Lane
        {
            Centre,
            LeftLane,
            LeftWalk,
        }

        /// <summary>A span's line in world metres (<paramref name="lane"/>), extended <paramref name="before"/> /
        /// <paramref name="after"/> metres straight beyond its ends; reversed it runs the other way on its own left.</summary>
        public static List<double> Along(BridgeSpan sp, TileId tile, Lane lane, double before, double after, bool reverse)
        {
            BridgePath p = sp.Path;
            var pts = new List<double>();
            int n = p.Count;
            for (int i = 0; i < n; i++)
            {
                int k = reverse ? n - 1 - i : i;
                int a = Math.Max(0, k - 1), b = Math.Min(n - 1, k + 1);
                double dx = p.X[b] - p.X[a], dz = p.Z[b] - p.Z[a], l = Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-9) continue;
                double half = reverse ? sp.HalfR[k] : sp.HalfL[k], walk = reverse ? sp.WalkR[k] : sp.WalkL[k];
                double lateral = lane == Lane.Centre ? 0 : lane == Lane.LeftLane ? 0.5 * half : half + 0.5 * walk;
                if (reverse) lateral = -lateral;
                pts.Add(tile.X0 + p.X[k] - dz / l * lateral);
                pts.Add(tile.Z0 + p.Z[k] + dx / l * lateral);
            }
            if (pts.Count < 4) return pts;
            Extend(pts, reverse ? after : before, true);
            Extend(pts, reverse ? before : after, false);
            return pts;
        }

        /// <summary>A line up (or down) one stair flight in world metres: from its last tread (its first, going down) to
        /// 0.6 m past its top edge (its foot), whatever continues there: the deck, a landing, the next flight or the
        /// ground.</summary>
        public static List<double> Flight(BridgeStair st, TileId tile, bool up)
        {
            double a0 = st.Length - 0.25, a1 = -0.6;
            var pts = new List<double>();
            int n = 12;
            for (int i = 0; i <= n; i++)
            {
                double a = a0 + (a1 - a0) * i / n;
                pts.Add(tile.X0 + st.X + st.Dx * a);
                pts.Add(tile.Z0 + st.Z + st.Dz * a);
            }
            if (!up)
            {
                pts.Clear();
                a0 = 0.25;
                a1 = st.Length + 0.6;
                for (int i = 0; i <= n; i++)
                {
                    double a = a0 + (a1 - a0) * i / n;
                    pts.Add(tile.X0 + st.X + st.Dx * a);
                    pts.Add(tile.Z0 + st.Z + st.Dz * a);
                }
            }
            return pts;
        }

        /// <summary>A road's centreline (rendered range) in world metres around the point nearest (cx, cz) (tile-local),
        /// from <paramref name="half"/> metres before it to as far after it.</summary>
        public static List<double> RoadAround(TileData t, int road, double cx, double cz, double half, double lateral = 0)
        {
            RoadRecord r = t.Roads[road];
            int first, last;
            RoadSpatialIndex.RenderedRange(r, out first, out last);
            var xs = new List<double>();
            var zs = new List<double>();
            for (int k = first; k <= last; k++)
            {
                xs.Add(r.Points[2 * k] / 100.0);
                zs.Add(r.Points[2 * k + 1] / 100.0);
            }
            // Beyond the road's ends the route carries on along the drawn road that continues it there (the one sharing
            // the end point, most nearly straight on), as a player would follow the street, for as far as needed.
            Continue(t, road, xs, zs, false, half + 10);
            Continue(t, road, xs, zs, true, half + 10);
            // Along distance of the nearest point.
            double best = double.MaxValue, sBest = 0, acc = 0;
            var s = new double[xs.Count];
            for (int k = 0; k + 1 < xs.Count; k++)
            {
                double dx = xs[k + 1] - xs[k], dz = zs[k + 1] - zs[k], l2 = dx * dx + dz * dz;
                double f = l2 > 1e-12 ? Math.Max(0, Math.Min(1, ((cx - xs[k]) * dx + (cz - zs[k]) * dz) / l2)) : 0;
                double qx = xs[k] + dx * f, qz = zs[k] + dz * f, d = (qx - cx) * (qx - cx) + (qz - cz) * (qz - cz);
                if (d < best)
                {
                    best = d;
                    sBest = acc + f * Math.Sqrt(l2);
                }
                acc += Math.Sqrt(l2);
                s[k + 1] = acc;
            }
            var pts = new List<double>();
            double s0 = sBest - half, s1 = sBest + half;
            for (double q = s0; q <= s1 + 1e-6; q += 2.0)
            {
                double px, pz, ax, az, bx, bz;
                At(xs, zs, s, q, out px, out pz);
                At(xs, zs, s, q - 0.5, out ax, out az);
                At(xs, zs, s, q + 0.5, out bx, out bz);
                double dx = bx - ax, dz = bz - az, l = Math.Max(1e-9, Math.Sqrt(dx * dx + dz * dz));
                pts.Add(t.Tile.X0 + px - dz / l * lateral);
                pts.Add(t.Tile.Z0 + pz + dx / l * lateral);
            }
            return pts;
        }

        private static void At(List<double> xs, List<double> zs, double[] s, double q, out double x, out double z)
        {
            int n = xs.Count;
            if (q <= 0)
            {
                double dx = xs[1] - xs[0], dz = zs[1] - zs[0], l = Math.Max(1e-9, s[1]);
                x = xs[0] + dx / l * q;
                z = zs[0] + dz / l * q;
                return;
            }
            for (int k = 0; k + 1 < n; k++)
            {
                if (q > s[k + 1] && k + 2 < n) continue;
                double l = Math.Max(1e-9, s[k + 1] - s[k]), f = (q - s[k]) / l;
                x = xs[k] + (xs[k + 1] - xs[k]) * f;
                z = zs[k] + (zs[k + 1] - zs[k]) * f;
                return;
            }
            x = xs[n - 1];
            z = zs[n - 1];
        }

        /// <summary>Extends a centreline (tile-local metres) at its start or end along the roads of the tile that
        /// continue it at that point (sharing the point exactly, at one of their ends or passing through it), each time
        /// the one most nearly straight on (at a T, where none is within 60 degrees, the branch turning least, as a
        /// player turns into the street at the end of theirs), until <paramref name="need"/> metres are added or no road
        /// continues.</summary>
        private static void Continue(TileData t, int road, List<double> xs, List<double> zs, bool atStart, double need)
        {
            var used = new HashSet<int> { road };
            double added = 0;
            for (int hop = 0; hop < 6 && added < need && xs.Count >= 2; hop++)
            {
                int n = xs.Count;
                double ex = atStart ? xs[0] : xs[n - 1], ez = atStart ? zs[0] : zs[n - 1];
                double ix = atStart ? xs[0] - xs[1] : xs[n - 1] - xs[n - 2], iz = atStart ? zs[0] - zs[1] : zs[n - 1] - zs[n - 2];
                double il = Math.Sqrt(ix * ix + iz * iz);
                if (il < 1e-9) return;
                int bestRoad = -1, bestK = 0, bestDir = 0;
                double bestCos = -0.35; // at most 110 degrees off straight on
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    if (used.Contains(ri) || !RoadMesher.IsDrawn(t.Roads[ri], null)) continue;
                    RoadRecord o = t.Roads[ri];
                    int f, l;
                    RoadSpatialIndex.RenderedRange(o, out f, out l);
                    if (l <= f) continue;
                    for (int k = f; k <= l; k++)
                    {
                        double px = o.Points[2 * k] / 100.0, pz = o.Points[2 * k + 1] / 100.0;
                        if (Math.Abs(px - ex) > 0.02 || Math.Abs(pz - ez) > 0.02) continue;
                        for (int dir = -1; dir <= 1; dir += 2)
                        {
                            int k2 = k + dir;
                            if (k2 < f || k2 > l) continue;
                            double ox = o.Points[2 * k2] / 100.0 - px, oz = o.Points[2 * k2 + 1] / 100.0 - pz, ol = Math.Sqrt(ox * ox + oz * oz);
                            if (ol < 1e-9) continue;
                            double cos = (ix * ox + iz * oz) / (il * ol);
                            if (cos > bestCos)
                            {
                                bestCos = cos;
                                bestRoad = ri;
                                bestK = k;
                                bestDir = dir;
                            }
                        }
                    }
                }
                if (bestRoad < 0) return;
                used.Add(bestRoad);
                RoadRecord br = t.Roads[bestRoad];
                int bf, bl;
                RoadSpatialIndex.RenderedRange(br, out bf, out bl);
                var px2 = new List<double>();
                var pz2 = new List<double>();
                for (int k = bestK + bestDir; k >= bf && k <= bl; k += bestDir)
                {
                    px2.Add(br.Points[2 * k] / 100.0);
                    pz2.Add(br.Points[2 * k + 1] / 100.0);
                }
                double lx = ex, lz = ez;
                for (int i = 0; i < px2.Count; i++)
                {
                    added += Math.Sqrt((px2[i] - lx) * (px2[i] - lx) + (pz2[i] - lz) * (pz2[i] - lz));
                    lx = px2[i];
                    lz = pz2[i];
                    if (atStart)
                    {
                        xs.Insert(0, px2[i]);
                        zs.Insert(0, pz2[i]);
                    }
                    else
                    {
                        xs.Add(px2[i]);
                        zs.Add(pz2[i]);
                    }
                    if (added >= need) break;
                }
            }
        }

        public static void Extend(List<double> pts, double m, bool atStart)
        {
            if (m <= 0) return;
            int n = pts.Count / 2;
            int a = atStart ? 0 : n - 1, b = atStart ? 1 : n - 2;
            double dx = pts[2 * a] - pts[2 * b], dz = pts[2 * a + 1] - pts[2 * b + 1], l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-9) return;
            double x = pts[2 * a] + dx / l * m, z = pts[2 * a + 1] + dz / l * m;
            if (atStart)
            {
                pts.Insert(0, z);
                pts.Insert(0, x);
            }
            else
            {
                pts.Add(x);
                pts.Add(z);
            }
        }

        public static void Reverse(List<double> pts)
        {
            int n = pts.Count / 2;
            for (int i = 0; i < n / 2; i++)
            {
                int j = n - 1 - i;
                double x = pts[2 * i], z = pts[2 * i + 1];
                pts[2 * i] = pts[2 * j];
                pts[2 * i + 1] = pts[2 * j + 1];
                pts[2 * j] = x;
                pts[2 * j + 1] = z;
            }
        }
    }
}
