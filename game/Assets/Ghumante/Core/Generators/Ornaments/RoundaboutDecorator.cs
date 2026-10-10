using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Ornaments
{
    /// <summary>
    /// Dresses roundabout and chowk islands (W2 detail pass, docs/research/w2/roundabouts.md, ref_ornaments.md): the
    /// painted kerb, apron and lawn, the real centrepiece where there is one (<see cref="RoundaboutCatalog"/>) and a
    /// generic garden everywhere else, flower beds, hedges, trees, paths, railing, solar street lights, the keep-left
    /// sign and the traffic police post. Output: tile-local positions with absolute Y (MeshData contract), vertex
    /// colour = albedo, UV0 = (MaterialChannel, baked AO) on every vertex.
    /// <para>
    /// Integration: call <see cref="Build(in JunctionRecord, TileData, IHeightSampler, MeshData, int, float, float, OrnamentStats)"/>
    /// per JNCT record, passing the island radius the road layout drew (<c>RoadLayout.Islands</c>) so the kerb matches
    /// the ring hole exactly, or <see cref="BuildTile"/> for a whole tile. With <see cref="DecorOptions.DrawBase"/> off
    /// the kerb and lawn are left to the road mesher and only what stands on the island is built. Deterministic
    /// (FNV-1a seeds); allocation-free once the per-thread scratch has grown; thread-safe for distinct meshes.
    /// </para>
    /// <para>Budgets (triangles, LOD 0-3): hero designs 16,000 / 4,000 / 1,000 / 200, generic islands 8,000 / 2,000 /
    /// 500 / 100 (roundabouts.md §5).</para>
    /// </summary>
    public static class RoundaboutDecorator
    {
        /// <summary>Road surface above the terrain next to an island (RoadMesher: LiftM 0.25 + primary class lift).</summary>
        public const float DefaultRoadLiftM = 0.274f;

        /// <summary>Ring carriageway width assumed when a JNCT ring has no mapped island (W2_DESIGN 4.7: widest
        /// approach + 1 m, at least 7 m).</summary>
        public const float DefaultRingWidthM = 8.5f;

        /// <summary>Triangle budget of hero designs per LOD.</summary>
        public static readonly int[] HeroBudget = { 16000, 4000, 1000, 200 };

        /// <summary>Triangle budget of generic islands per LOD (the big ones are small parks with trees).</summary>
        public static readonly int[] GenericBudget = { 8000, 2000, 500, 100 };

        /// <summary>Switches for <see cref="Build(in RoundaboutSite, in RoundaboutDesign, IHeightSampler, MeshData, int, OrnamentStats, DecorOptions)"/>.</summary>
        public struct DecorOptions
        {
            /// <summary>Draw the kerb, apron and lawn (off when the road mesher already draws the island disc).</summary>
            public bool DrawBase;

            /// <summary>Draw the traffic police post (off when the road mesher draws its own podium).</summary>
            public bool DrawPolice;

            /// <summary>Optional collider sink: solid boxes for pedestals, the gate, towers, basins, posts and poles
            /// (tile-local, absolute Y) so riders cannot pass through a centrepiece. Null = none.</summary>
            public GenColliders Colliders;

            public static DecorOptions Default
            {
                get { return new DecorOptions { DrawBase = true, DrawPolice = true }; }
            }
        }

        // ------------------------------------------------------------------ design and site of a junction

        /// <summary>The design of a JNCT record: the curated entry when it matches, else the generic design for an
        /// island of <paramref name="islandRadiusM"/>. False for junctions that get no decoration (plain crossings,
        /// signals, mini roundabouts).</summary>
        public static bool TryDesign(in JunctionRecord j, TileData t, float islandRadiusM, out RoundaboutDesign d, out RoundaboutEntry entry)
        {
            entry = null;
            d = default(RoundaboutDesign);
            if (j.Kind == JunctionKind.Plain || j.Kind == JunctionKind.Signals || j.Kind == JunctionKind.MiniRoundabout)
            {
                if (!(j.Kind == JunctionKind.Signals && j.Has(JunctionFlags.HasPolice))) return false;
            }
            if (RoundaboutCatalog.TryMatch(j, t, out entry))
            {
                d = entry.Design;
                return true;
            }
            d = RoundaboutCatalog.Generic(j, islandRadiusM);
            return true;
        }

        /// <summary>
        /// The island of a JNCT record: centre from the record, radius <paramref name="islandRadiusM"/> when given (the
        /// road layout's island), else the record's island diameter, else the ring diameter minus
        /// <see cref="DefaultRingWidthM"/> on rings, the curated radius, or the W2_DESIGN 4.7 synthetic island
        /// (0.7 × the widest arm, clamped to 6-16 m). Police chowks without an island get radius 0 (post only).
        /// The main arm bearing is <paramref name="mainArmDeg"/> when given, else measured from the tile's roads.
        /// </summary>
        public static bool TrySite(in JunctionRecord j, TileData t, float islandRadiusM, float mainArmDeg, out RoundaboutSite s)
        {
            s = default(RoundaboutSite);
            if (t == null) return false;
            double x = j.XCm / 100.0, z = j.ZCm / 100.0;
            RoundaboutEntry e;
            RoundaboutCatalog.TryMatch(j, t, out e);
            float r = islandRadiusM;
            float apron = 0f;
            bool ring = j.Kind == JunctionKind.Roundabout || j.Kind == JunctionKind.Circular;
            if (ring) apron = 1.0f;
            if (r <= 0f)
            {
                if (j.IslandDiameterCm > 0) r = j.IslandDiameterCm / 200f;
                else if (e != null && e.IslandRadiusM > 0) r = e.IslandRadiusM;
                else if (ring && j.RingDiameterCm > 0) r = Math.Max(1.5f, j.RingDiameterCm / 200f - DefaultRingWidthM);
                else if (j.Kind == JunctionKind.SyntheticIsland)
                {
                    float widest = WidestArm(t, x, z, 15.0);
                    r = 0.5f * Math.Max(6f, Math.Min(16f, 0.7f * (widest > 0 ? widest : 12f)));
                }
                else r = 0f;
            }
            if (j.Kind == JunctionKind.SyntheticIsland) apron = 0f;
            s = new RoundaboutSite
            {
                X = x, Z = z, RadiusM = r, ApronM = Math.Min(apron, 0.3f * r), Shape = IslandShape.Round, RoadLiftM = DefaultRoadLiftM,
                MainArmDeg = float.IsNaN(mainArmDeg) ? MainArmBearing(t, x, z, r) : mainArmDeg, TileX0 = t.Tile.X0, TileZ0 = t.Tile.Z0,
            };
            return true;
        }

        /// <summary>The site of a standalone curated entry (no JNCT record) in tile <paramref name="t"/>.</summary>
        public static RoundaboutSite StandaloneSite(RoundaboutEntry e, TileData t)
        {
            double x = e.GameX - t.Tile.X0, z = e.GameZ - t.Tile.Z0;
            return new RoundaboutSite
            {
                X = x, Z = z, RadiusM = e.IslandRadiusM, HalfLengthM = e.HalfLengthM, Shape = e.Shape, ApronM = 0f, RoadLiftM = DefaultRoadLiftM,
                MainArmDeg = float.IsNaN(e.Design.FacingDeg) ? MainArmBearing(t, x, z, e.IslandRadiusM) : e.Design.FacingDeg, TileX0 = t.Tile.X0,
                TileZ0 = t.Tile.Z0,
            };
        }

        // ------------------------------------------------------------------ entry points

        /// <summary>
        /// Decorate the island of one JNCT record into <paramref name="m"/> at <paramref name="lod"/> (0-3). Pass the
        /// island radius the road layout drew (0 = derive it from the record) and the main arm bearing (NaN = measure
        /// it). Returns the triangles added (0 when the junction gets no decoration).
        /// </summary>
        public static int Build(in JunctionRecord j, TileData t, IHeightSampler h, MeshData m, int lod = 0, float islandRadiusM = 0f,
                                float mainArmDeg = float.NaN, OrnamentStats stats = null)
        {
            return Build(j, t, h, m, lod, islandRadiusM, mainArmDeg, stats, DecorOptions.Default);
        }

        /// <summary>As <see cref="Build(in JunctionRecord, TileData, IHeightSampler, MeshData, int, float, float, OrnamentStats)"/>
        /// with explicit options (base, police post, colliders).</summary>
        public static int Build(in JunctionRecord j, TileData t, IHeightSampler h, MeshData m, int lod, float islandRadiusM, float mainArmDeg,
                                OrnamentStats stats, DecorOptions options)
        {
            RoundaboutSite s;
            if (!TrySite(j, t, islandRadiusM, mainArmDeg, out s)) return 0;
            RoundaboutDesign d;
            RoundaboutEntry e;
            if (!TryDesign(j, t, s.RadiusM, out d, out e)) return 0;
            return Build(s, d, h, m, lod, stats, options);
        }

        /// <summary>Decorate every JNCT island and every standalone curated site of a tile. Returns the triangles added.</summary>
        public static int BuildTile(TileData t, IHeightSampler h, MeshData m, int lod = 0)
        {
            return BuildTile(t, h, m, lod, DecorOptions.Default);
        }

        /// <summary>As <see cref="BuildTile(TileData, IHeightSampler, MeshData, int)"/> with explicit options.</summary>
        public static int BuildTile(TileData t, IHeightSampler h, MeshData m, int lod, DecorOptions options)
        {
            if (t == null) return 0;
            int tris = 0;
            foreach (JunctionRecord j in t.Junctions) tris += Build(j, t, h, m, lod, 0f, float.NaN, null, options);
            // Each curated entry dresses one island only (TryMatch picks the nearest record), so no duplicates here.
            List<RoundaboutEntry> list = s_standalone ?? (s_standalone = new List<RoundaboutEntry>(4));
            list.Clear();
            RoundaboutCatalog.StandaloneIn(t, list);
            foreach (RoundaboutEntry e in list) tris += Build(StandaloneSite(e, t), e.Design, h, m, lod, null, options);
            list.Clear();
            return tris;
        }

        [ThreadStatic] private static List<RoundaboutEntry> s_standalone;

        /// <summary>Decorate an explicit site with an explicit design (previews, tests, custom islands).</summary>
        public static int Build(in RoundaboutSite site, in RoundaboutDesign design, IHeightSampler h, MeshData m, int lod, OrnamentStats stats = null)
        {
            return Build(site, design, h, m, lod, stats, DecorOptions.Default);
        }

        /// <summary>Decorate an explicit site with an explicit design and options. Returns the triangles added.</summary>
        public static int Build(in RoundaboutSite site, in RoundaboutDesign design, IHeightSampler h, MeshData m, int lod, OrnamentStats stats,
                                DecorOptions options)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int t0 = m.IndexCount, v0 = m.VertexCount;
            if (stats != null)
            {
                stats.Clear();
                stats.DesignId = design.Id;
                stats.Centre = design.Centre;
            }
            OrnCtx c = OrnCtx.For();
            c.Begin(m, h, site, design, lod, stats);
            if (stats != null) stats.IslandTopY = c.CentreTopY;
            if (site.RadiusM < 0.5f)
            {
                // A police chowk without an island: the post alone at the junction centre, on the road.
                if (options.DrawPolice && design.Police != PoliceStyle.None)
                {
                    float py = c.RoadY(site.X, site.Z) + 0.003f;
                    IslandMesher.PolicePost(c, site.X, py, site.Z, site.MainArmDeg, design.Police);
                    if (options.Colliders != null)
                        options.Colliders.AddBox(site.X, site.Z, py, py + 1.05, 0.75, 0.75, 1, 0, GenColliderFlags.NoClimb, GenColliders.Concrete);
                }
                return Finish(c, m, t0, v0);
            }
            int mark = m.IndexCount;
            if (options.DrawBase) IslandMesher.Base(c);
            if (stats != null) stats.BaseTriangles = (m.IndexCount - mark) / 3;
            mark = m.IndexCount;
            double ri = IslandMesher.InnerRadius(site);
            double rc = 0;
            switch (design.Centre)
            {
                case Centrepiece.Mandala:
                    rc = MonumentMesher.Mandala(c);
                    break;
                case Centrepiece.MemorialArch:
                    rc = MonumentMesher.ShahidGate(c);
                    break;
                case Centrepiece.Statue:
                case Centrepiece.EquestrianStatue:
                {
                    StatueSpec s = design.Statue;
                    if (design.Centre == Centrepiece.EquestrianStatue) s.Pose = StatuePose.Rider;
                    if (design.Fountain && s.Plinth == PlinthShape.Spire)
                    {
                        rc = MonumentMesher.SpireBasin(c, s);
                    }
                    else
                    {
                        double crown = StatueMesher.Build(c, c.CentreFrame(), s, c.CentreTopY - 0.25);
                        if (stats != null) stats.TopM = (float)Math.Max(stats.TopM, crown);
                        rc = StatueFootprint(s);
                    }
                    break;
                }
                case Centrepiece.Fountain:
                    rc = MonumentMesher.Fountain(c, Math.Max(1.6, Math.Min(6.5, 0.38 * ri)));
                    break;
                case Centrepiece.ClockTower:
                    rc = MonumentMesher.ClockTower(c, Math.Max(6.0, Math.Min(11.0, 0.9 * ri)));
                    break;
                case Centrepiece.FlagPole:
                    MonumentMesher.FlagPole(c, site.X, site.Z, Math.Max(8.0, Math.Min(16.0, 1.4 * ri)), site.MainArmDeg + 90);
                    rc = 1.3;
                    break;
                default:
                    rc = CentralBed(c, ri);
                    break;
            }
            if (stats != null) stats.CentreTriangles = (m.IndexCount - mark) / 3;
            if (options.Colliders != null) CentreColliders(c, options.Colliders, rc);
            mark = m.IndexCount;
            if (site.Shape == IslandShape.Stadium) IslandMesher.EdgeBeds(c);
            else IslandMesher.Garden(c, rc);
            double span = ri - rc;
            if (design.Trees > 0 && span > 3.5 && site.Shape == IslandShape.Round) IslandMesher.TreeRing(c, rc + 0.62 * span, design.Trees);
            if (site.Shape == IslandShape.Round) IslandMesher.Paths(c, design.Paths, rc, 1.5);
            if (stats != null) stats.GardenTriangles = (m.IndexCount - mark) / 3;
            mark = m.IndexCount;
            IslandMesher.Railing(c);
            if (c.Lod < 2 || (c.Lod == 2 && design.IsHero)) IslandMesher.Lamps(c, design.LampPosts, site.RadiusM > 12 ? 8.0 : 6.5);
            if (design.FlagPoleM > 0 && design.Centre != Centrepiece.FlagPole)
            {
                double a = (site.MainArmDeg - 115) * Math.PI / 180.0, r = Math.Max(rc + 1.5, ri - 2.4);
                MonumentMesher.FlagPole(c, site.X + r * Math.Sin(a), site.Z + r * Math.Cos(a), design.FlagPoleM, site.MainArmDeg + 60);
            }
            if (options.DrawPolice)
            {
                IslandMesher.Police(c, design.Police);
                if (options.Colliders != null && design.Police != PoliceStyle.None && c.Lod < 3)
                {
                    double ri2 = IslandMesher.InnerRadius(site), pd = (site.MainArmDeg + (ri2 > 3 ? 55 : 0)) * Math.PI / 180.0, pr = Math.Max(0, ri2 - 1.1);
                    double px = site.X + pr * Math.Sin(pd), pz = site.Z + pr * Math.Cos(pd);
                    float py = c.TopY(px, pz);
                    options.Colliders.AddBox(px, pz, py, py + 1.05, 0.75, 0.75, 1, 0, GenColliderFlags.NoClimb, GenColliders.Concrete);
                }
            }
            if (design.Signboard) IslandMesher.KeepLeftSign(c);
            if (stats != null) stats.FurnitureTriangles = (m.IndexCount - mark) / 3;
            return Finish(c, m, t0, v0);
        }

        private static int Finish(OrnCtx c, MeshData m, int t0, int v0)
        {
            int tris = (m.IndexCount - t0) / 3;
            if (c.Stats != null)
            {
                c.Stats.Triangles = tris;
                c.Stats.Vertices = m.VertexCount - v0;
            }
            c.M = null;
            c.H = null;
            c.Stats = null;
            return tris;
        }

        /// <summary>Solid boxes for the centrepiece (frame-aligned with the facing), so riders and walkers stop at it.</summary>
        private static void CentreColliders(OrnCtx c, GenColliders col, double rc)
        {
            RoundaboutSite s = c.Site;
            RoundaboutDesign d = c.Design;
            double a = c.FacingDeg * Math.PI / 180.0, ux = Math.Cos(a), uz = -Math.Sin(a);
            double y = c.CentreTopY;
            switch (d.Centre)
            {
                case Centrepiece.Statue:
                case Centrepiece.EquestrianStatue:
                {
                    StatueSpec st = d.Statue;
                    if (d.Fountain && st.Plinth == PlinthShape.Spire)
                    {
                        // Basin rim as a walkable ring of boxes, the spire solid.
                        for (int k = 0; k < 8; k++)
                        {
                            double b = 2 * Math.PI * k / 8, r = rc - 0.6;
                            col.AddBox(s.X + r * Math.Sin(b), s.Z + r * Math.Cos(b), y - 0.2, y + 0.48, 0.3, 0.5 * r * 0.78, Math.Cos(b), -Math.Sin(b),
                                       GenColliderFlags.Walkable, GenColliders.Stone);
                        }
                        col.AddBox(s.X, s.Z, y - 0.2, y + st.PlinthM, 0.3 * st.PlinthW, 0.3 * st.PlinthW, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                        break;
                    }
                    double py = st.Steps > 0 ? 0.28 * st.Steps : 0;
                    if (st.Steps > 0)
                        col.AddBox(s.X, s.Z, y - 0.25, y + py, 0.5 * st.PlatformW, 0.5 * st.PlatformW, ux, uz, GenColliderFlags.Walkable, GenColliders.Stone);
                    col.AddBox(s.X, s.Z, y + py, y + py + st.PlinthM + st.FigureM, 0.5 * st.PlinthW + 0.15, 0.5 * st.PlinthW + 0.15, ux, uz,
                               GenColliderFlags.NoClimb, GenColliders.Stone);
                    break;
                }
                case Centrepiece.MemorialArch:
                {
                    col.AddBox(s.X, s.Z, y - 0.3, y + 0.9, 13.2, 3.6, ux, uz, GenColliderFlags.Walkable, GenColliders.Stone);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double x, z;
                        c.Local(side * 10.3, 0, out x, out z);
                        col.AddBox(x, z, y + 0.9, y + 4.6, 2.3, 2.0, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                        c.Local(side * 7.0, 0, out x, out z);
                        col.AddBox(x, z, y + 0.9, y + 4.0, 1.6, 0.75, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                    }
                    break;
                }
                case Centrepiece.Mandala:
                    col.AddBox(s.X, s.Z, y - 0.2, y + 0.32, 12, 12, ux, uz, GenColliderFlags.Walkable, GenColliders.Stone);
                    break;
                case Centrepiece.ClockTower:
                    col.AddBox(s.X, s.Z, y - 0.2, y + 11, 0.7 * rc, 0.7 * rc, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                    break;
                case Centrepiece.Fountain:
                    col.AddBox(s.X, s.Z, y - 0.2, y + 0.5, 0.7 * rc, 0.7 * rc, ux, uz, GenColliderFlags.Walkable, GenColliders.Stone);
                    col.AddBox(s.X, s.Z, y, y + 1.5, 0.5, 0.5, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                    break;
                case Centrepiece.FlagPole:
                    col.AddBox(s.X, s.Z, y - 0.2, y + 1.0, 0.8, 0.8, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                    break;
            }
        }

        /// <summary>Radius the statue's platform (or pedestal) occupies.</summary>
        public static double StatueFootprint(in StatueSpec s)
        {
            double w = s.Steps > 0 ? s.PlatformW : s.PlinthW + 0.4;
            return s.Platform == PlatformShape.Round || s.Steps == 0 ? 0.5 * w + 0.2 : 0.5 * w * 1.414 + 0.2;
        }

        /// <summary>A generic island's centre: a raised round marigold bed with a clipped shrub (radius ≥ 2.5 m) or a
        /// single shrub. Returns its footprint.</summary>
        private static double CentralBed(OrnCtx c, double ri)
        {
            if (c.Lod >= 3 || ri < 1.2) return 0;
            RoundaboutSite s = c.Site;
            float y = c.TopY(s.X, s.Z);
            if (ri < 2.5)
            {
                OrnamentKit.Clump(c, s.X, y + 0.45, s.Z, 0.55, 0.45, 0.55, OrnamentPalette.ShrubGolden, MaterialChannel.Foliage, 12, 0.08, c.Seed);
                return 0.6;
            }
            double r = Math.Min(2.2, 0.22 * ri);
            int v0 = c.M.VertexCount, i0 = c.M.IndexCount;
            if (c.Lod >= 2)
            {
                Shapes.Cylinder(c.M, Affine3.Translation(s.X, y - 0.1, s.Z), OrnamentKit.B(OrnamentPalette.BrickPlanter, MaterialChannel.Brick), r + 0.25, 0.45, 8, 0, 0, false, false, c.L);
                Shapes.Dome(c.M, Affine3.Translation(s.X, y + 0.33, s.Z), OrnamentKit.B(OrnamentPalette.MarigoldOrange, MaterialChannel.Foliage), r + 0.2, 0.5, 8, false, c.L);
                return r + 0.4;
            }
            Shapes.Cylinder(c.M, Affine3.Translation(s.X, y - 0.1, s.Z), OrnamentKit.B(OrnamentPalette.BrickPlanter, MaterialChannel.Brick),
                                           r + 0.25, 0.45, 28, 0.04, 1, false, true, c.L);
            Shapes.Dome(c.M, Affine3.Translation(s.X, y + 0.33, s.Z), OrnamentKit.B(OrnamentPalette.Soil, MaterialChannel.Dirt), r, 0.12, 20, false, c.L);
            OrnamentKit.Mound(c, OrnamentKit.Ring(c, c.Path, s.X, s.Z, 0.72 * r, 0.35, 0, 360, c.Lod == 0 ? 28 : 14, 0, 0, false), true, 0.5 * r, 0.3,
                              OrnamentPalette.MarigoldOrange, OrnamentPalette.MarigoldYellow, MaterialChannel.Foliage, 0.05, c.Seed + 3, 0.25);
            OrnamentKit.Clump(c, s.X, y + 0.35 + 0.55 * r, s.Z, 0.45 * r, 0.55 * r, 0.45 * r, OrnamentPalette.Hedge, MaterialChannel.Foliage, 12, 0.12, c.Seed + 5);
            c.Ao(v0, i0, y - 0.1, 0.4f);
            if (c.Stats != null) c.Stats.Shrubs++;
            return r + 0.4;
        }

        // ------------------------------------------------------------------ arms

        /// <summary>Bearing (degrees clockwise from north) from (x, z) to the outer end of the most important road
        /// arm leaving the junction (highest class, then widest), ring members excluded; 0 when there is none.</summary>
        public static float MainArmBearing(TileData t, double x, double z, double radius)
        {
            if (t == null) return 0f;
            double reach = radius + 14.0, best = double.MaxValue, bearing = 0;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (r.RoadClass == RoadClass.Unknown || r.RoadClass >= RoadClass.Pedestrian) continue;
                if (t.HasRoadAttrs && t.RoadAttrs[i].Has(RoadAttrFlags.RingMember)) continue;
                int[] p = r.Points;
                int n = r.PointCount;
                for (int k = 0; k < n; k++)
                {
                    double px = p[2 * k] / 100.0 - x, pz = p[2 * k + 1] / 100.0 - z;
                    double d = Math.Sqrt(px * px + pz * pz);
                    if (d > reach) continue;
                    // The arm runs from this point away from the centre: take the neighbour further out.
                    for (int q = -1; q <= 1; q += 2)
                    {
                        int kk = k + q;
                        if (kk < 0 || kk >= n) continue;
                        double qx = p[2 * kk] / 100.0 - x, qz = p[2 * kk + 1] / 100.0 - z;
                        double dq = Math.Sqrt(qx * qx + qz * qz);
                        if (dq <= d + 1.0) continue;
                        double score = (int)r.RoadClass * 1000.0 - Math.Min(999.0, r.WidthCm / 10.0) + d * 0.01;
                        if (score < best)
                        {
                            best = score;
                            bearing = Math.Atan2(qx, qz) * 180.0 / Math.PI;
                        }
                    }
                }
            }
            if (best == double.MaxValue) return 0f;
            if (bearing < 0) bearing += 360;
            return (float)bearing;
        }

        /// <summary>Widest drawn width (OSM width, else lanes × 3.5 m) of the motor roads with a point within
        /// <paramref name="reach"/> of (x, z); 0 when none.</summary>
        public static float WidestArm(TileData t, double x, double z, double reach)
        {
            float best = 0f;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (r.RoadClass == RoadClass.Unknown || r.RoadClass >= RoadClass.Pedestrian) continue;
                int[] p = r.Points;
                for (int k = 0; k < r.PointCount; k++)
                {
                    double px = p[2 * k] / 100.0 - x, pz = p[2 * k + 1] / 100.0 - z;
                    if (px * px + pz * pz > reach * reach) continue;
                    float w = r.WidthCm > 0 ? r.WidthCm / 100f : Math.Max(1, (int)r.Lanes) * 3.5f;
                    if (w > best) best = w;
                    break;
                }
            }
            return best;
        }
    }
}
