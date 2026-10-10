using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Ornaments
{
    /// <summary>
    /// Dresses roundabout and chowk islands (W2 detail pass, docs/research/w2/roundabouts.md, ref_ornaments.md): the
    /// painted kerb, apron and lawn, the real centrepiece where there is one (<see cref="RoundaboutCatalog"/>) and a
    /// garden everywhere else, flower beds, hedges, trees, paths, railing, solar street lights, the keep-left sign and
    /// the traffic police post. Output: tile-local positions with absolute Y (MeshData contract), vertex colour =
    /// albedo, UV0 = (MaterialChannel, baked AO) on every vertex.
    /// <para>
    /// The islands are the ones the road layout draws (<see cref="RoadLayout.Islands"/>: mapped rings clamped to the
    /// ring carriageway's inner edge, synthetic chowk islands shrunk clear of other carriageways), so the kerb always
    /// matches the ring hole and nothing stands on a lane. Integration calls
    /// <see cref="BuildTile(TileData, IHeightSampler, MeshData, int, DecorOptions)"/> per tile, or
    /// <see cref="Build(in RoadIsland, TileData, IHeightSampler, MeshData, int, OrnamentStats, DecorOptions)"/> per
    /// layout island; police chowks without an island get the post alone at the junction; standalone curated sites
    /// (mapped islands that are not junction records: Shahid Gate, Singha Durbar, Kalimati, Lagankhel's tree) are fitted
    /// between the road corridors when <see cref="DecorOptions.Corridors"/> is given (<see cref="StandaloneIslands"/>
    /// lists their mapped islands for the roads package to cut out). The decorator owns the island base and the police post of every island it dresses: the
    /// road mesher must skip its own island disc and podium there (open issue, roundabouts.md §7). Deterministic
    /// (FNV-1a seeds); allocation-free once the per-thread scratch has grown; thread-safe for distinct meshes.
    /// </para>
    /// <para>Budgets (triangles, LOD 0-3): hero designs 16,000 / 4,000 / 1,000 / 200, generic islands 8,000 / 2,000 /
    /// 500 / 100 (roundabouts.md §5).</para>
    /// </summary>
    public static class RoundaboutDecorator
    {
        /// <summary>Road surface above the terrain next to an island when the layout gives no road to measure
        /// (RoadMesher: LiftM 0.25 + primary class lift).</summary>
        public const float DefaultRoadLiftM = 0.274f;

        /// <summary>Clearance kept between a standalone island's kerb and the nearest road corridor.</summary>
        public const double CorridorMarginM = 0.3;

        /// <summary>A layout island belongs to a junction record when their centres are this close.</summary>
        public const double MatchEpsM = 0.5;

        /// <summary>Triangle budget of hero designs per LOD.</summary>
        public static readonly int[] HeroBudget = { 16000, 4000, 1000, 200 };

        /// <summary>Triangle budget of generic islands per LOD (the big ones are small parks with trees).</summary>
        public static readonly int[] GenericBudget = { 8000, 2000, 500, 100 };

        private static readonly RoadOptions s_roadDefaults = new RoadOptions();

        /// <summary>Switches for the build calls.</summary>
        public struct DecorOptions
        {
            /// <summary>Draw the kerb, apron and lawn (off only when another mesher draws exactly this island disc).</summary>
            public bool DrawBase;

            /// <summary>Draw the traffic police post (the road mesher's own podium must then be off).</summary>
            public bool DrawPolice;

            /// <summary>Optional collider sink: solid boxes for pedestals, the gate, basins, posts and poles
            /// (tile-local, absolute Y) so riders cannot pass through a centrepiece. Null = none.</summary>
            public GenColliders Colliders;

            /// <summary>Road corridors of the tile (<c>RoadCorridorIndex.ForTile</c>, game metres), null = off.
            /// Standalone sites shrink to the gap between corridors (or are left out), and furniture never stands on a
            /// corridor outside the layout island it belongs to.</summary>
            public IRoadCorridorQuery Corridors;

            /// <summary>The road mesher's options (lift of the road surface round the island); null = defaults.</summary>
            public RoadOptions Roads;

            public static DecorOptions Default
            {
                get { return new DecorOptions { DrawBase = true, DrawPolice = true }; }
            }
        }

        // ------------------------------------------------------------------ the road layout's islands

        /// <summary>The raised island (not a painted mini roundabout) the road layout draws centred within
        /// <see cref="MatchEpsM"/> of tile-local (x, z).</summary>
        public static bool TryLayoutIsland(TileData t, double x, double z, out RoadIsland island)
        {
            island = default(RoadIsland);
            if (t == null || t.Roads.Count == 0) return false;
            foreach (RoadIsland isl in RoadLayout.For(t).Islands)
            {
                if (isl.Kind == IslandKind.Mini) continue;
                double dx = isl.X - x, dz = isl.Z - z;
                if (dx * dx + dz * dz > MatchEpsM * MatchEpsM) continue;
                island = isl;
                return true;
            }
            return false;
        }

        /// <summary>The junction record at a layout island's centre (W1 rings found from closed ways have none).</summary>
        public static bool TryRecord(TileData t, in RoadIsland island, out JunctionRecord j)
        {
            j = default(JunctionRecord);
            if (t == null) return false;
            foreach (JunctionRecord r in t.Junctions)
            {
                double dx = r.XCm / 100.0 - island.X, dz = r.ZCm / 100.0 - island.Z;
                if (dx * dx + dz * dz > MatchEpsM * MatchEpsM) continue;
                j = r;
                return true;
            }
            return false;
        }

        /// <summary>Road surface lift round a layout island, exactly as the junction mesher lays it.</summary>
        public static float RoadLiftOf(TileData t, in RoadIsland island, RoadOptions roads)
        {
            RoadOptions o = roads ?? s_roadDefaults;
            return island.TopRoad >= 0 && island.TopRoad < t.Roads.Count ? RoadMesher.LiftOf(t.Roads[island.TopRoad], o) + JunctionMesher.CapExtraLiftM : o.LiftM;
        }

        // ------------------------------------------------------------------ design and site of a junction

        /// <summary>The design of a JNCT record: the curated entry when it matches, else the generic garden design
        /// for an island of <paramref name="islandRadiusM"/>. False for junctions that get no decoration (plain
        /// crossings, signals without police, mini roundabouts).</summary>
        public static bool TryDesign(in JunctionRecord j, TileData t, float islandRadiusM, out RoundaboutDesign d, out RoundaboutEntry entry)
        {
            entry = null;
            d = default(RoundaboutDesign);
            if (j.Kind == JunctionKind.Plain || j.Kind == JunctionKind.MiniRoundabout) return false;
            if (j.Kind == JunctionKind.Signals && !j.Has(JunctionFlags.HasPolice)) return false;
            if (RoundaboutCatalog.TryMatch(j, t, out entry))
            {
                d = entry.Design;
                return true;
            }
            d = RoundaboutCatalog.Generic(j, islandRadiusM);
            return true;
        }

        /// <summary>
        /// The island of a JNCT record. <paramref name="islandRadiusM"/>: 0 = the island the road layout draws there
        /// (none: radius 0, the police post alone); a positive radius from the caller's own layout (clamped to the
        /// road layout's island when there is one); negative = explicitly no island. The main arm bearing is
        /// <paramref name="mainArmDeg"/> when given, else measured from the tile's roads.
        /// </summary>
        public static bool TrySite(in JunctionRecord j, TileData t, float islandRadiusM, float mainArmDeg, out RoundaboutSite s)
        {
            return TrySite(j, t, islandRadiusM, mainArmDeg, null, out s);
        }

        /// <summary>As <see cref="TrySite(in JunctionRecord, TileData, float, float, out RoundaboutSite)"/> with the road
        /// mesher's options (for the road lift).</summary>
        public static bool TrySite(in JunctionRecord j, TileData t, float islandRadiusM, float mainArmDeg, RoadOptions roads, out RoundaboutSite s)
        {
            s = default(RoundaboutSite);
            if (t == null) return false;
            double x = j.XCm / 100.0, z = j.ZCm / 100.0;
            RoadIsland isl = default(RoadIsland);
            bool has = islandRadiusM >= 0 && TryLayoutIsland(t, x, z, out isl);
            bool ring = j.Kind == JunctionKind.Roundabout || j.Kind == JunctionKind.Circular;
            float r = 0f, apron = 0f;
            if (islandRadiusM > 0)
            {
                r = has ? Math.Min(islandRadiusM, isl.RadiusM) : islandRadiusM;
                apron = has ? isl.ApronM : ring ? 1.0f : 0f;
            }
            else if (islandRadiusM == 0 && has)
            {
                r = isl.RadiusM;
                apron = isl.ApronM;
            }
            s = new RoundaboutSite
            {
                X = x, Z = z, RadiusM = r, ApronM = Math.Min(apron, 0.3f * r), LayoutIsland = has && r > 0,
                RoadLiftM = has ? RoadLiftOf(t, isl, roads) : DefaultRoadLiftM,
                MainArmDeg = float.IsNaN(mainArmDeg) ? MainArmBearing(t, x, z, r) : mainArmDeg, TileX0 = t.Tile.X0, TileZ0 = t.Tile.Z0,
            };
            return true;
        }

        /// <summary>The site of a layout island: its exact radius, apron and road lift.</summary>
        public static RoundaboutSite SiteOf(in RoadIsland island, TileData t, float mainArmDeg = float.NaN, RoadOptions roads = null)
        {
            return new RoundaboutSite
            {
                X = island.X, Z = island.Z, RadiusM = island.RadiusM, ApronM = Math.Min(island.ApronM, 0.3f * island.RadiusM), LayoutIsland = true,
                RoadLiftM = RoadLiftOf(t, island, roads), MainArmDeg = float.IsNaN(mainArmDeg) ? MainArmBearing(t, island.X, island.Z, island.RadiusM) : mainArmDeg,
                TileX0 = t.Tile.X0, TileZ0 = t.Tile.Z0,
            };
        }

        /// <summary>The site of a standalone curated entry (no JNCT record) in tile <paramref name="t"/> at its mapped
        /// size; the build fits it between the road corridors (<see cref="FitToCorridors"/>).</summary>
        public static RoundaboutSite StandaloneSite(RoundaboutEntry e, TileData t)
        {
            double x = e.GameX - t.Tile.X0, z = e.GameZ - t.Tile.Z0;
            return new RoundaboutSite
            {
                X = x, Z = z, RadiusM = e.IslandRadiusM, ApronM = 0f, LayoutIsland = false, RoadLiftM = DefaultRoadLiftM,
                MainArmDeg = float.IsNaN(e.Design.FacingDeg) ? MainArmBearing(t, x, z, e.IslandRadiusM) : e.Design.FacingDeg, TileX0 = t.Tile.X0,
                TileZ0 = t.Tile.Z0,
            };
        }

        /// <summary>
        /// The mapped islands of the standalone curated sites of a tile (Shahid Gate, the Singha Durbar statue, Kalimati,
        /// Lagankhel's tree), at their mapped radius, appended to <paramref name="dst"/>: they are not junction records,
        /// so the road layout neither knows them nor routes lanes round them. The roads and integration packages cut
        /// these discs out of junction plates and the lane graph (open issue, roundabouts.md §7); until they do, the
        /// build fits each one into the gap between the road corridors and leaves out those with no room.
        /// </summary>
        public static void StandaloneIslands(TileData t, List<RoundaboutSite> dst)
        {
            if (t == null || dst == null) return;
            List<RoundaboutEntry> list = Scratch();
            RoundaboutCatalog.StandaloneIn(t, list);
            foreach (RoundaboutEntry e in list) dst.Add(StandaloneSite(e, t));
            list.Clear();
        }

        /// <summary>Shrink a site that is not a layout island to the gap between the road corridors round its centre
        /// (less <see cref="CorridorMarginM"/>). False when what is left cannot hold the design's centrepiece.</summary>
        public static bool FitToCorridors(ref RoundaboutSite s, in RoundaboutDesign d, IRoadCorridorQuery corridors)
        {
            if (s.LayoutIsland || corridors == null || s.RadiusM < 0.5f) return true;
            double clear = corridors.SignedDistance(s.TileX0 + s.X, s.TileZ0 + s.Z) - CorridorMarginM;
            if (clear < s.RadiusM) s.RadiusM = (float)Math.Max(0, clear);
            s.ApronM = Math.Min(s.ApronM, 0.3f * s.RadiusM);
            return s.RadiusM >= MinIslandRadius(d);
        }

        /// <summary>The smallest island that still holds the design's centrepiece (the statue's pedestal without its
        /// steps, the gate's platform, the mandala at 60 %).</summary>
        public static float MinIslandRadius(in RoundaboutDesign d)
        {
            const double kerb = 0.3 + 0.25;
            switch (d.Centre)
            {
                case Centrepiece.Statue:
                case Centrepiece.EquestrianStatue:
                    return (float)(0.5 * (Math.Max(0.6, d.Statue.PlinthW) + 0.4) + 0.2 + kerb);
                case Centrepiece.MemorialArch:
                    return (float)(MonumentMesher.GateReach + kerb);
                case Centrepiece.Mandala:
                    return (float)(0.6 * MonumentMesher.MandalaReach + kerb);
                case Centrepiece.Fountain:
                    return 2.4f;
                case Centrepiece.ShadeTree:
                    return 2.6f;
                case Centrepiece.PolicePodium:
                    return 0f;
                default:
                    return 1.2f;
            }
        }

        // ------------------------------------------------------------------ entry points

        /// <summary>
        /// Decorate the island of one JNCT record into <paramref name="m"/> at <paramref name="lod"/> (0-3).
        /// <paramref name="islandRadiusM"/> as in <see cref="TrySite(in JunctionRecord, TileData, float, float, out RoundaboutSite)"/>
        /// (0 = the road layout's island), the main arm bearing (NaN = measure it). Returns the triangles added (0 when
        /// the junction gets no decoration).
        /// </summary>
        public static int Build(in JunctionRecord j, TileData t, IHeightSampler h, MeshData m, int lod = 0, float islandRadiusM = 0f,
                                float mainArmDeg = float.NaN, OrnamentStats stats = null)
        {
            return Build(j, t, h, m, lod, islandRadiusM, mainArmDeg, stats, DecorOptions.Default);
        }

        /// <summary>As <see cref="Build(in JunctionRecord, TileData, IHeightSampler, MeshData, int, float, float, OrnamentStats)"/>
        /// with explicit options (base, police post, colliders, corridors).</summary>
        public static int Build(in JunctionRecord j, TileData t, IHeightSampler h, MeshData m, int lod, float islandRadiusM, float mainArmDeg,
                                OrnamentStats stats, DecorOptions options)
        {
            RoundaboutSite s;
            if (!TrySite(j, t, islandRadiusM, mainArmDeg, options.Roads, out s)) return 0;
            RoundaboutDesign d;
            RoundaboutEntry e;
            if (!TryDesign(j, t, s.RadiusM, out d, out e)) return 0;
            // Without an island only a police chowk gets anything: its post at the junction.
            if (s.RadiusM < 0.5f && d.Police == PoliceStyle.None) return 0;
            return Build(s, d, h, m, lod, stats, options);
        }

        /// <summary>Decorate one island of the tile's road layout (its junction record's design, or a generic garden for
        /// a W1 ring without a record). Mini roundabouts get nothing. Returns the triangles added.</summary>
        public static int Build(in RoadIsland island, TileData t, IHeightSampler h, MeshData m, int lod, OrnamentStats stats, DecorOptions options)
        {
            if (t == null || island.Kind == IslandKind.Mini || island.RadiusM < 0.5f) return 0;
            RoundaboutSite s = SiteOf(island, t, float.NaN, options.Roads);
            JunctionRecord j;
            if (!TryRecord(t, island, out j))
            {
                int xcm = (int)Math.Round(island.X * 100), zcm = (int)Math.Round(island.Z * 100);
                long key = ((long)t.Tile.Key << 20) ^ ((long)xcm << 32) ^ (uint)zcm;
                j = new JunctionRecord { OsmNodeId = key, Kind = JunctionKind.Roundabout, XCm = xcm, ZCm = zcm };
            }
            RoundaboutDesign d;
            RoundaboutEntry e;
            if (!TryDesign(j, t, s.RadiusM, out d, out e)) d = RoundaboutCatalog.Generic(j, s.RadiusM);
            return Build(s, d, h, m, lod, stats, options);
        }

        /// <summary>Decorate every island of the tile's road layout, the police posts of police chowks without one,
        /// and the standalone curated sites. Returns the triangles added.</summary>
        public static int BuildTile(TileData t, IHeightSampler h, MeshData m, int lod = 0)
        {
            return BuildTile(t, h, m, lod, DecorOptions.Default);
        }

        /// <summary>As <see cref="BuildTile(TileData, IHeightSampler, MeshData, int)"/> with explicit options.</summary>
        public static int BuildTile(TileData t, IHeightSampler h, MeshData m, int lod, DecorOptions options)
        {
            if (t == null) return 0;
            int tris = 0;
            if (t.Roads.Count > 0)
                foreach (RoadIsland isl in RoadLayout.For(t).Islands)
                    tris += Build(isl, t, h, m, lod, null, options);
            foreach (JunctionRecord j in t.Junctions)
            {
                RoadIsland isl;
                if (TryLayoutIsland(t, j.XCm / 100.0, j.ZCm / 100.0, out isl)) continue;
                tris += Build(j, t, h, m, lod, -1f, float.NaN, null, options);
            }
            List<RoundaboutEntry> list = Scratch();
            RoundaboutCatalog.StandaloneIn(t, list);
            foreach (RoundaboutEntry e in list) tris += Build(StandaloneSite(e, t), e.Design, h, m, lod, null, options);
            list.Clear();
            return tris;
        }

        [ThreadStatic] private static List<RoundaboutEntry> s_standalone;

        private static List<RoundaboutEntry> Scratch()
        {
            List<RoundaboutEntry> list = s_standalone ?? (s_standalone = new List<RoundaboutEntry>(4));
            list.Clear();
            return list;
        }

        /// <summary>Decorate an explicit site with an explicit design (previews, tests, custom islands).</summary>
        public static int Build(in RoundaboutSite site, in RoundaboutDesign design, IHeightSampler h, MeshData m, int lod, OrnamentStats stats = null)
        {
            return Build(site, design, h, m, lod, stats, DecorOptions.Default);
        }

        /// <summary>Decorate an explicit site with an explicit design and options. A site that is not a layout island
        /// is first fitted between the road corridors (<see cref="FitToCorridors"/>) and left out when no room is left.
        /// Returns the triangles added.</summary>
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
            RoundaboutSite s = site;
            if (!FitToCorridors(ref s, design, options.Corridors))
            {
                if (stats != null) stats.Skipped++;
                return 0;
            }
            if (stats != null) stats.RadiusM = s.RadiusM >= 0.5f ? s.RadiusM : 0f;
            OrnCtx c = OrnCtx.For();
            c.Begin(m, h, s, design, lod, stats, options.Corridors);
            if (stats != null) stats.IslandTopY = c.CentreTopY;
            if (s.RadiusM < 0.5f)
            {
                // A police chowk without an island: the post alone at the junction centre, on the road (W2_DESIGN 4.7).
                if (options.DrawPolice && design.Police != PoliceStyle.None && c.Lod < 3)
                {
                    float py = c.RoadY(s.X, s.Z) + 0.003f;
                    c.Take(OrnamentFootprint.Disc(FootprintKind.PolicePost, s.X, s.Z, IslandMesher.PostRadius(design.Police, false)));
                    IslandMesher.PolicePost(c, s.X, py, s.Z, s.MainArmDeg, design.Police);
                    if (options.Colliders != null)
                        options.Colliders.AddBox(s.X, s.Z, py, py + 1.05, 0.75, 0.75, 1, 0, GenColliderFlags.NoClimb, GenColliders.Concrete);
                }
                return Finish(c, m, t0, v0);
            }
            int mark = m.IndexCount;
            if (options.DrawBase) IslandMesher.Base(c);
            if (stats != null) stats.BaseTriangles = (m.IndexCount - mark) / 3;
            mark = m.IndexCount;
            double ri = IslandMesher.InnerRadius(s);
            double rc = Centre(c, design, ri);
            // Everything else keeps clear of the centrepiece: rcIsland is its reach from the island centre, rFree the
            // first ring round the island centre with room beside it (they differ for the gate and an offset mandala).
            double rcIsland = rc + Math.Sqrt((c.CX - s.X) * (c.CX - s.X) + (c.CZ - s.Z) * (c.CZ - s.Z));
            double rFree = c.Lod < 3 ? Math.Min(rcIsland, c.FreeRadius(ri, 0.6)) : rcIsland;
            if (stats != null) stats.CentreTriangles = (m.IndexCount - mark) / 3;
            if (options.Colliders != null) CentreColliders(c, options.Colliders, rc);
            // Street furniture before the planting, so beds, shrubs and trees make room for it (never the other way).
            mark = m.IndexCount;
            if (options.DrawPolice)
            {
                // The post first: it needs the most room by the kerb.
                IslandMesher.Police(c, design.Police);
                OrnamentFootprint post;
                if (options.Colliders != null && c.TryFind(FootprintKind.PolicePost, out post))
                {
                    float py = c.TopY(post.X, post.Z);
                    options.Colliders.AddBox(post.X, post.Z, py, py + 1.05, 0.75, 0.75, 1, 0, GenColliderFlags.NoClimb, GenColliders.Concrete);
                }
            }
            if (design.FlagPoleM > 0 && design.Centre != Centrepiece.FlagPole) IslandFlag(c, design.FlagPoleM, ri);
            if (c.Lod < 2 || (c.Lod == 2 && design.IsHero)) IslandMesher.Lamps(c, design.LampPosts, s.RadiusM > 12 ? 8.0 : 6.5);
            if (design.Signboard) IslandMesher.KeepLeftSign(c);
            IslandMesher.Railing(c);
            if (stats != null) stats.FurnitureTriangles = (m.IndexCount - mark) / 3;
            mark = m.IndexCount;
            double span = ri - rcIsland;
            if (design.Centre == Centrepiece.Mandala) MonumentMesher.MandalaFeatureBed(c, ri);
            // Trees by the kerb where the centrepiece leaves room (each finds a free spot or is left out).
            if (design.Trees > 0 && ri - rFree > 3.5) IslandMesher.TreeRing(c, Math.Max(rFree + 0.62 * (ri - rFree), rcIsland + 0.62 * span), design.Trees);
            IslandMesher.Garden(c, Math.Min(rFree, ri));
            IslandMesher.Paths(c, design.Paths, 1.5);
            if (stats != null) stats.GardenTriangles = (m.IndexCount - mark) / 3;
            return Finish(c, m, t0, v0);
        }

        /// <summary>Build the centrepiece (placed and scaled to fit inside the planting radius <paramref name="ri"/>),
        /// register its footprint and return its reach from its own centre.</summary>
        private static double Centre(OrnCtx c, in RoundaboutDesign design, double ri)
        {
            double rc;
            switch (design.Centre)
            {
                case Centrepiece.Mandala:
                {
                    double scale = Math.Max(0.6, Math.Min(1.0, (ri - 0.3) / MonumentMesher.MandalaReach));
                    c.PlaceCentre(MonumentMesher.MandalaReach * scale, ri, 2.5);
                    rc = MonumentMesher.Mandala(c, scale);
                    break;
                }
                case Centrepiece.MemorialArch:
                    rc = MonumentMesher.ShahidGate(c);
                    break;
                case Centrepiece.Statue:
                case Centrepiece.EquestrianStatue:
                {
                    StatueSpec st = FitStatue(design.Statue, ri);
                    if (design.Centre == Centrepiece.EquestrianStatue) st.Pose = StatuePose.Rider;
                    if (design.Fountain && st.Plinth == PlinthShape.Spire)
                    {
                        rc = MonumentMesher.SpireBasin(c, st, Math.Min(MonumentMesher.SpireBasinRadius, ri - 0.6));
                        c.Take(OrnamentFootprint.Disc(FootprintKind.Centrepiece, c.CX, c.CZ, rc));
                        break;
                    }
                    rc = StatueFootprint(st);
                    double crown = StatueMesher.Build(c, c.CentreFrame(), st, c.CentreTopY - 0.25);
                    if (c.Stats != null) c.Stats.TopM = (float)Math.Max(c.Stats.TopM, crown);
                    double half = st.Steps > 0 ? 0.5 * st.PlatformW : 0.5 * st.PlinthW + 0.2;
                    if (st.Steps > 0 && st.Platform == PlatformShape.Round) c.Take(OrnamentFootprint.Disc(FootprintKind.Centrepiece, c.CX, c.CZ, half + 0.05));
                    else c.Take(OrnamentFootprint.Box(FootprintKind.Centrepiece, c.CX, c.CZ, half, half, c.FacingDeg, 0.05));
                    break;
                }
                case Centrepiece.Fountain:
                    rc = MonumentMesher.Fountain(c, Math.Max(1.6, Math.Min(6.5, 0.38 * ri)));
                    c.Take(OrnamentFootprint.Disc(FootprintKind.Centrepiece, c.CX, c.CZ, rc));
                    break;
                case Centrepiece.FlagPole:
                {
                    double hgt = Math.Max(8.0, Math.Min(16.0, 1.4 * ri));
                    MonumentMesher.FlagPole(c, c.CX, c.CZ, hgt, c.Site.MainArmDeg + 90);
                    rc = MonumentMesher.FlagPoleBase(hgt) + 0.2;
                    c.Take(OrnamentFootprint.Disc(FootprintKind.Centrepiece, c.CX, c.CZ, rc));
                    break;
                }
                case Centrepiece.ShadeTree:
                    rc = MonumentMesher.Chautari(c, ri);
                    c.Take(OrnamentFootprint.Disc(FootprintKind.Centrepiece, c.CX, c.CZ, rc));
                    break;
                case Centrepiece.PolicePodium:
                    rc = 0;
                    break;
                default:
                    rc = CentralBed(c, ri);
                    if (rc > 0) c.Take(OrnamentFootprint.Disc(FootprintKind.Bed, c.CX, c.CZ, rc));
                    break;
            }
            return rc;
        }

        /// <summary>A statue spec that fits inside the planting radius <paramref name="ri"/>: the platform narrows and
        /// loses steps (and its pots) on small islands, down to the bare pedestal.</summary>
        public static StatueSpec FitStatue(in StatueSpec spec, double ri)
        {
            StatueSpec s = spec;
            double room = ri - 0.25;
            if (StatueFootprint(s) <= room) return s;
            s.Pots = false;
            double k = s.Platform == PlatformShape.Round ? 1.0 : Math.Sqrt(2);
            double maxW = 2 * (room - 0.2) / k;
            const double tread = 0.84;
            int steps = (int)Math.Floor((maxW - (Math.Max(0.6, s.PlinthW) + 0.3)) / tread) + 1;
            if (s.Steps > 0 && steps >= 1)
            {
                s.PlatformW = (float)Math.Min(s.PlatformW, maxW);
                s.Steps = (byte)Math.Min(s.Steps, steps);
            }
            else
            {
                s.Steps = 0;
            }
            return s;
        }

        /// <summary>The national flag pole standing on the island (Maitighar's giant flag): near the kerb, clear of
        /// the centrepiece and the post.</summary>
        private static void IslandFlag(OrnCtx c, double height, double ri)
        {
            double br = MonumentMesher.FlagPoleBase(height);
            double x, z, deg;
            if (!c.FindSpot(c.Site.MainArmDeg - 115, Math.Max(0, ri - br - 0.6), br, 0.4, ri, 10, 180, out x, out z, out deg))
            {
                c.Skip();
                return;
            }
            c.Take(OrnamentFootprint.Disc(FootprintKind.FlagPole, x, z, br));
            MonumentMesher.FlagPole(c, x, z, height, c.Site.MainArmDeg + 60);
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
            c.Corridors = null;
            return tris;
        }

        /// <summary>Solid boxes for the centrepiece (frame-aligned with the facing), so riders and walkers stop at it.</summary>
        private static void CentreColliders(OrnCtx c, GenColliders col, double rc)
        {
            RoundaboutDesign d = c.Design;
            double a = c.FacingDeg * Math.PI / 180.0, ux = Math.Cos(a), uz = -Math.Sin(a);
            double y = c.CentreTopY;
            switch (d.Centre)
            {
                case Centrepiece.Statue:
                case Centrepiece.EquestrianStatue:
                {
                    StatueSpec st = FitStatue(d.Statue, IslandMesher.InnerRadius(c.Site));
                    if (d.Fountain && st.Plinth == PlinthShape.Spire)
                    {
                        // Basin rim as a walkable ring of boxes, the spire solid.
                        for (int k = 0; k < 8; k++)
                        {
                            double b = 2 * Math.PI * k / 8, r = rc - 0.6;
                            col.AddBox(c.CX + r * Math.Sin(b), c.CZ + r * Math.Cos(b), y - 0.2, y + 0.48, 0.3, 0.5 * r * 0.78, Math.Cos(b), -Math.Sin(b),
                                       GenColliderFlags.Walkable, GenColliders.Stone);
                        }
                        col.AddBox(c.CX, c.CZ, y - 0.2, y + st.PlinthM, 0.3 * st.PlinthW, 0.3 * st.PlinthW, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                        break;
                    }
                    double py = st.Steps > 0 ? 0.28 * st.Steps : 0;
                    if (st.Steps > 0)
                        col.AddBox(c.CX, c.CZ, y - 0.25, y + py, 0.5 * st.PlatformW, 0.5 * st.PlatformW, ux, uz, GenColliderFlags.Walkable, GenColliders.Stone);
                    col.AddBox(c.CX, c.CZ, y + py, y + py + st.PlinthM + st.FigureM, 0.5 * st.PlinthW + 0.15, 0.5 * st.PlinthW + 0.15, ux, uz,
                               GenColliderFlags.NoClimb, GenColliders.Stone);
                    break;
                }
                case Centrepiece.MemorialArch:
                    MonumentMesher.GateColliders(c, col);
                    break;
                case Centrepiece.Mandala:
                    col.AddBox(c.CX, c.CZ, y - 0.2, y + 0.32, rc * 0.6, rc * 0.6, ux, uz, GenColliderFlags.Walkable, GenColliders.Stone);
                    break;
                case Centrepiece.Fountain:
                    col.AddBox(c.CX, c.CZ, y - 0.2, y + 0.5, 0.7 * rc, 0.7 * rc, ux, uz, GenColliderFlags.Walkable, GenColliders.Stone);
                    col.AddBox(c.CX, c.CZ, y, y + 1.5, 0.5, 0.5, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                    break;
                case Centrepiece.FlagPole:
                    col.AddBox(c.CX, c.CZ, y - 0.2, y + 1.0, 0.8, 0.8, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                    break;
                case Centrepiece.ShadeTree:
                    col.AddBox(c.CX, c.CZ, y - 0.2, y + MonumentMesher.ChautariHeightM, 0.7 * rc, 0.7 * rc, ux, uz, GenColliderFlags.Walkable, GenColliders.Brick);
                    col.AddBox(c.CX, c.CZ, y + MonumentMesher.ChautariHeightM, y + 4.0, 0.75, 0.75, ux, uz, GenColliderFlags.NoClimb, GenColliders.Wood);
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
            if (ri < 2.5)
            {
                float y = c.TopY(s.X, s.Z);
                OrnamentKit.Clump(c, s.X, y + 0.45, s.Z, 0.55, 0.45, 0.55, OrnamentPalette.ShrubGolden, MaterialChannel.Foliage, 12, 0.08, c.Seed);
                return 0.6;
            }
            return IslandMesher.RaisedBed(c, s.X, s.Z, Math.Min(2.2, 0.22 * ri));
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
