using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// The solids a tile's own data implies (docs/W2_DETAIL_CONTRACT.md §1), built once per exact tile for the ground
    /// query, on a worker when it comes through <see cref="TileGroundQuery.BuildRoadIndex"/>:
    /// <list type="bullet">
    /// <item><b>Building footprints</b> (the rings as the data package trimmed them): every outline wall from below its
    /// base to its top, an elevated part (<c>min_height</c>) only from that height, open canopies as corner posts and a
    /// roof slab. Sacred outlines and their parts (the temple generators emit their own walkable plinths and walls),
    /// hidden hero footprints and outlines whose parts are drawn instead are left out. As a runtime guard a footprint
    /// never reaches into a road corridor: points inside it are pushed out to its edge (decision 1).</item>
    /// <item><b>Point objects</b> (PROP): tree trunks, poles, lamps, masts, towers, chimneys, tanks, wells, taps, benches
    /// and statues (artwork), each a cylinder sized by kind; never one standing on a drawn carriageway.</item>
    /// <item><b>Railings and parapets</b> along both edges of every bridge and flyover where its deck stands at least
    /// <see cref="RailingMinRiseM"/> above the terrain (structure heights, or the straight deck of a bridge without
    /// them).</item>
    /// </list>
    /// Deterministic: the same tile and options give the same solids in the same order.
    /// </summary>
    public static class TileSolids
    {
        /// <summary>Half thickness of a footprint wall (the outline is the wall's outer face).</summary>
        public const float WallHalfM = 0.05f;

        /// <summary>A deck lower than this above the terrain needs no railing (ramps start at grade); it matches the
        /// step onto a deck from its side (<see cref="TileGroundQuery.StructureDeckStepUpM"/>), so no side is open.</summary>
        public const float RailingMinRiseM = TileGroundQuery.StructureDeckStepUpM;

        /// <summary>Railing height where the structure record gives none.</summary>
        public const float DefaultRailingM = 1.1f;

        /// <summary>Half thickness of a railing or parapet.</summary>
        public const float RailingHalfM = 0.08f;

        /// <summary>Longest footprint edge piece checked against the corridors (finer pieces follow a corridor edge).</summary>
        public const double CorridorStepM = 2.0;

        /// <summary>Outlines of sacred archetypes are left to the temple generators (as BuildingGrammar.IsSacred).</summary>
        public static bool IsSacred(BuildingArchetype a)
        {
            return a == BuildingArchetype.TemplePagoda || a == BuildingArchetype.TempleShikhara || a == BuildingArchetype.Stupa ||
                   a == BuildingArchetype.Chorten || a == BuildingArchetype.Shrine;
        }

        /// <summary>Builds the solids of <paramref name="t"/>. <paramref name="roads"/> (the tile's road index, may be null)
        /// keeps props off carriageways and bounds railings; <paramref name="corridors"/> (may be null: the drawn
        /// carriageways of <paramref name="roads"/> stand in) trims footprints; <paramref name="hidden"/> lists building
        /// refs drawn by hero replicas (may be null).</summary>
        internal static SolidSet Build(TileData t, RoadSpatialIndex roads, IRoadCorridorQuery corridors, ISet<ulong> hidden,
                                       RoadOptions roadOptions)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (t.HeightsQ == null || t.HeightsN < 2) return SolidSet.Empty;
            if (t.Buildings.Count == 0 && t.Props.Count == 0 && t.Roads.Count == 0) return SolidSet.Empty;
            var h = new TileHeightSampler(t, 1);
            var b = new SolidBuilder();
            Buildings(t, h, roads, corridors, hidden, b);
            Props(t, h, roads, b);
            if (roads != null) Railings(t, h, roads, roadOptions ?? new RoadOptions(), b);
            return b.Build();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Buildings

        private sealed class Scratch
        {
            public double[] X = new double[64], Z = new double[64];
            public double[] X2 = new double[64], Z2 = new double[64];
            public double[] TX = new double[64], TZ = new double[64];
            public bool[] Inside = new bool[64];

            /// <summary>Grows the densified buffers to hold n points, keeping their contents.</summary>
            public void EnsureDense(int n)
            {
                if (TX.Length >= n) return;
                int m = Math.Max(n, 2 * TX.Length);
                Array.Resize(ref TX, m);
                Array.Resize(ref TZ, m);
                Array.Resize(ref Inside, m);
            }

            /// <summary>Grows the outline buffers to hold n points (the densified ones too, so a guarded ring fits).</summary>
            public void Ensure(int n)
            {
                EnsureDense(n);
                if (X.Length >= TX.Length) return;
                int m = TX.Length;
                X = new double[m];
                Z = new double[m];
                X2 = new double[m];
                Z2 = new double[m];
            }
        }

        private static void Buildings(TileData t, TileHeightSampler h, RoadSpatialIndex roads, IRoadCorridorQuery corridors, ISet<ulong> hidden,
                                      SolidBuilder b)
        {
            if (t.Buildings.Count == 0) return;
            var s = new Scratch();
            List<int> sacredHosts = null;
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                BuildingRecord r = t.Buildings[i];
                if (IsSacred(r.Archetype) && (r.Flags & BuildingFlags.Part) == 0 && r.Rings != null && r.Rings.Length > 0)
                {
                    if (sacredHosts == null) sacredHosts = new List<int>();
                    sacredHosts.Add(i);
                }
            }
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                BuildingRecord r = t.Buildings[i];
                if (r.Rings == null || r.Rings.Length == 0 || r.Rings[0] == null || r.Rings[0].Length < 6) continue;
                if (hidden != null && hidden.Contains(r.OsmRef)) continue;
                if (IsSacred(r.Archetype)) continue;
                bool part = (r.Flags & BuildingFlags.Part) != 0;
                if ((r.Flags & BuildingFlags.HasParts) != 0 && !part) continue; // its parts are the solids
                if (part && sacredHosts != null && InsideHost(t, r, sacredHosts)) continue;

                // Heights: from below the lowest corner to the top over the highest.
                float minG = float.PositiveInfinity, maxG = float.NegativeInfinity;
                int[] outer = r.Rings[0];
                for (int k = 0; k + 1 < outer.Length; k += 2)
                {
                    float g;
                    if (!h.TryHeightClamped(x0 + outer[k] / 100.0, z0 + outer[k + 1] / 100.0, out g)) continue;
                    minG = Math.Min(minG, g);
                    maxG = Math.Max(maxG, g);
                }
                if (float.IsInfinity(minG)) continue;
                float height = r.HeightCm > 0 ? r.HeightCm / 100f : r.Levels > 0 ? r.Levels * 3f + 1.5f : 7f;
                if (height < 2.5f) height = 2.5f;
                float top = maxG + height;
                float minH = r.MinHeightCm > 0 ? r.MinHeightCm / 100f : 0f;
                float bottom = minH > 0.5f ? minG + minH : minG - 1.5f;
                if (!(top > bottom + 0.2f)) continue;

                bool canopy = (r.Flags & BuildingFlags.OpenCanopy) != 0 || r.Use == BuildingUse.Roof;
                b.BeginGroup();
                for (int ring = 0; ring < r.Rings.Length; ring++)
                {
                    int[] pts = r.Rings[ring];
                    if (pts == null || pts.Length < 6) continue;
                    if (ring > 0 && canopy) break;
                    int second;
                    int n = Outline(t, pts, roads, corridors, s, out second);
                    if (n < 3) continue;
                    if (canopy)
                    {
                        // Open canopy (petrol stations, pavilions): posts at the corners, the roof slab above head height.
                        float slabBottom = Math.Max(bottom, top - 0.6f);
                        b.AddRing(s.X, s.Z, n, WallHalfM, slabBottom, top, SolidFlags.None);
                        for (int k = 0; k + 1 < pts.Length; k += 2)
                            b.AddCylinder(x0 + pts[k] / 100.0, z0 + pts[k + 1] / 100.0, 0.2f, bottom, slabBottom, SolidFlags.NoCamera);
                        continue;
                    }
                    b.AddRing(s.X, s.Z, n, WallHalfM, bottom, top, SolidFlags.None);
                    if (second >= 3)
                    {
                        // The other side of a road that ran through the footprint: an outline of its own.
                        b.BeginGroup();
                        b.AddRing(s.X2, s.Z2, second, WallHalfM, bottom, top, SolidFlags.None);
                        break; // holes of a cut footprint are dropped
                    }
                }
            }
        }

        /// <summary>True when the part's first point lies inside the outer ring of a sacred host outline.</summary>
        private static bool InsideHost(TileData t, BuildingRecord part, List<int> hosts)
        {
            int[] p = part.Rings[0];
            double cx = 0, cz = 0;
            int n = p.Length / 2;
            for (int k = 0; k + 1 < p.Length; k += 2)
            {
                cx += p[k];
                cz += p[k + 1];
            }
            cx /= n;
            cz /= n;
            foreach (int hi in hosts)
            {
                int[] ring = t.Buildings[hi].Rings[0];
                if (ring == null || ring.Length < 6) continue;
                bool inside = false;
                int m = ring.Length / 2;
                for (int i = 0, j = m - 1; i < m; j = i++)
                {
                    double xi = ring[2 * i], zi = ring[2 * i + 1], xj = ring[2 * j], zj = ring[2 * j + 1];
                    if (zi > cz != zj > cz && cx < (xj - xi) * (cz - zi) / (zj - zi) + xi) inside = !inside;
                }
                if (inside) return true;
            }
            return false;
        }

        /// <summary>
        /// The ring in game metres, in <see cref="Scratch.X"/>/<see cref="Scratch.Z"/>; returns its point count. The
        /// runtime guard of decision 1: where the ring reaches into a road (the corridor of <paramref name="corridors"/>
        /// when given, else the drawn carriageway), its edges are cut into pieces of at most <see cref="CorridorStepM"/>
        /// and every point inside is moved to the corridor edge on the footprint's side, so the outline is sealed along
        /// the corridor edge. A footprint the road runs right through becomes two outlines, one each side: the second
        /// one goes to <see cref="Scratch.X2"/>/<see cref="Scratch.Z2"/> with its count in <paramref name="second"/>.
        /// </summary>
        private static int Outline(TileData t, int[] pts, RoadSpatialIndex roads, IRoadCorridorQuery corridors, Scratch s, out int second)
        {
            second = 0;
            int n = pts.Length / 2;
            s.Ensure(n);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int k = 0; k < n; k++)
            {
                s.X[k] = x0 + pts[2 * k] / 100.0;
                s.Z[k] = z0 + pts[2 * k + 1] / 100.0;
                minX = Math.Min(minX, s.X[k]);
                maxX = Math.Max(maxX, s.X[k]);
                minZ = Math.Min(minZ, s.Z[k]);
                maxZ = Math.Max(maxZ, s.Z[k]);
            }
            if (roads == null) return n;
            // Quick reject: no road within the outline's reach.
            double cx = 0.5 * (minX + maxX), cz = 0.5 * (minZ + maxZ);
            double rad = 0.5 * Math.Sqrt((maxX - minX) * (maxX - minX) + (maxZ - minZ) * (maxZ - minZ));
            RoadHit hit;
            bool near = corridors != null
                ? corridors.SignedDistance(cx, cz) < rad
                : roads.TryNearest(cx, cz, rad, RoadFlags.None, RoadFlags.None, RoadLayer.Ground, out hit);
            if (!near) return n;

            // Densify and classify: inside a corridor, or on the left (-1) / right (+1) of the nearest road.
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                int j = k + 1 == n ? 0 : k + 1;
                double dx = s.X[j] - s.X[k], dz = s.Z[j] - s.Z[k];
                int pieces = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / CorridorStepM));
                for (int q = 0; q < pieces; q++)
                {
                    s.EnsureDense(m + 1);
                    double f = q / (double)pieces;
                    s.TX[m] = s.X[k] + dx * f;
                    s.TZ[m] = s.Z[k] + dz * f;
                    m++;
                }
            }
            s.Ensure(m);
            bool anyInside = false, left = false, right = false;
            for (int k = 0; k < m; k++)
            {
                s.Inside[k] = InsideCorridor(s.TX[k], s.TZ[k], roads, corridors);
                anyInside |= s.Inside[k];
                if (s.Inside[k]) continue;
                int side = SideOf(s.TX[k], s.TZ[k], roads);
                left |= side < 0;
                right |= side > 0;
            }
            if (!anyInside) return n;
            if (left && right && Straddles(s, m, roads))
            {
                // The road runs through the footprint: one outline on each side, each sealed along its corridor edge.
                int na = Sided(s, m, -1, roads, corridors, s.X, s.Z);
                second = Sided(s, m, +1, roads, corridors, s.X2, s.Z2);
                return na;
            }
            int keep = right && !left ? 1 : -1;
            return Sided(s, m, keep, roads, corridors, s.X, s.Z);
        }

        private static bool InsideCorridor(double x, double z, RoadSpatialIndex roads, IRoadCorridorQuery corridors)
        {
            if (corridors != null) return corridors.SignedDistance(x, z) < 0;
            RoadHit hit;
            return roads.TryNearest(x, z, 0.0, RoadFlags.None, RoadFlags.None, RoadLayer.Ground, out hit) && hit.EdgeDistanceM < 0f;
        }

        /// <summary>Side of a point relative to the nearest road's carriageway centre: -1 left, +1 right of its point order.</summary>
        private static int SideOf(double x, double z, RoadSpatialIndex roads)
        {
            RoadHit hit;
            if (!roads.TryNearest(x, z, 60.0, RoadFlags.None, RoadFlags.None, RoadLayer.Ground, out hit)) return 0;
            return hit.LateralM - hit.CentreShiftM >= 0f ? 1 : -1;
        }

        /// <summary>True when some run of corridor points has outside neighbours on both sides of the road.</summary>
        private static bool Straddles(Scratch s, int m, RoadSpatialIndex roads)
        {
            for (int k = 0; k < m; k++)
            {
                if (!s.Inside[k] || s.Inside[(k + m - 1) % m]) continue; // the start of a run
                int end = k;
                while (s.Inside[(end + 1) % m] && (end + 1) % m != k) end = (end + 1) % m;
                int a = (k + m - 1) % m, b = (end + 1) % m;
                if (s.Inside[a] || s.Inside[b]) continue;
                if (SideOf(s.TX[a], s.TZ[a], roads) * SideOf(s.TX[b], s.TZ[b], roads) < 0) return true;
            }
            return false;
        }

        /// <summary>The densified ring with every point inside a corridor or across the road from <paramref name="side"/>
        /// moved to the corridor edge on that side; returns the point count written to (ox, oz).</summary>
        private static int Sided(Scratch s, int m, int side, RoadSpatialIndex roads, IRoadCorridorQuery corridors, double[] ox, double[] oz)
        {
            for (int k = 0; k < m; k++)
            {
                double x = s.TX[k], z = s.TZ[k];
                if (s.Inside[k] || SideOf(x, z, roads) != side) ToEdge(ref x, ref z, side, roads, corridors);
                ox[k] = x;
                oz[k] = z;
            }
            return m;
        }

        /// <summary>Moves a point across to just beyond the corridor edge on <paramref name="side"/> of its nearest road.</summary>
        private static void ToEdge(ref double x, ref double z, int side, RoadSpatialIndex roads, IRoadCorridorQuery corridors)
        {
            RoadHit hit;
            if (!roads.TryNearest(x, z, 60.0, RoadFlags.None, RoadFlags.None, RoadLayer.Ground, out hit)) return;
            double rx = hit.DirZ, rz = -hit.DirX; // right normal
            double off = hit.CentreShiftM + side * (hit.HalfWidthM + 0.05);
            double tx = hit.X + rx * off, tz = hit.Z + rz * off;
            if (corridors != null)
            {
                // The corridor may reach beyond the carriageway (footpaths): step on outward until clear of it.
                for (int it = 0; it < 40 && corridors.SignedDistance(tx, tz) < 0; it++)
                {
                    tx += rx * side * 0.25;
                    tz += rz * side * 0.25;
                }
            }
            x = tx;
            z = tz;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Point objects

        /// <summary>Cylinder radius and height of a PROP kind (0 radius: not solid).</summary>
        public static void PropSize(ObjectKind k, out float radius, out float height, out bool cameraPasses)
        {
            cameraPasses = false;
            switch (k)
            {
                case ObjectKind.Tree: radius = 0.35f; height = 8f; cameraPasses = true; return;
                case ObjectKind.PowerTower: radius = 2.0f; height = 30f; return;
                case ObjectKind.PowerPole: radius = 0.15f; height = 9f; cameraPasses = true; return;
                case ObjectKind.StreetLamp: radius = 0.15f; height = 9f; cameraPasses = true; return;
                case ObjectKind.BusStop: radius = 0.08f; height = 2.6f; cameraPasses = true; return;
                case ObjectKind.Bench: radius = 0.45f; height = 0.5f; cameraPasses = true; return;
                case ObjectKind.WaterTap: radius = 0.25f; height = 1.0f; cameraPasses = true; return;
                case ObjectKind.Well: radius = 0.9f; height = 0.9f; return;
                case ObjectKind.Chimney: radius = 1.2f; height = 20f; return;
                case ObjectKind.Mast: radius = 0.4f; height = 20f; cameraPasses = true; return;
                case ObjectKind.Tower: radius = 2.5f; height = 20f; return;
                case ObjectKind.StorageTank: radius = 2.5f; height = 6f; return;
                case ObjectKind.Artwork: radius = 0.8f; height = 3f; return;
                default: radius = 0f; height = 0f; return;
            }
        }

        private static void Props(TileData t, TileHeightSampler h, RoadSpatialIndex roads, SolidBuilder b)
        {
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            foreach (PropRecord p in t.Props)
            {
                if (p.Has(PropFlags.OnRoad)) continue;
                float r, height;
                bool cam;
                PropSize(p.Kind, out r, out height, out cam);
                if (!(r > 0f)) continue;
                double x = x0 + p.XCm / 100.0, z = z0 + p.ZCm / 100.0;
                if (roads != null)
                {
                    RoadHit hit;
                    if (roads.TryNearest(x, z, 0.0, RoadFlags.None, RoadFlags.None, RoadLayer.Ground, out hit) && hit.EdgeDistanceM < 0f) continue;
                }
                float g;
                if (!h.TryHeightClamped(x, z, out g)) continue;
                if (p.HeightDm > 0) height = p.HeightDm / 10f;
                if (p.Kind == ObjectKind.Tree)
                {
                    float th = height > 0f ? height : 8f;
                    r = Math.Min(StructureColliders.MaxTrunkRadiusM, Math.Max(StructureColliders.MinTrunkRadiusM, th * StructureColliders.TrunkRadiusPerM));
                    height = Math.Max(2.5f, 0.5f * th);
                }
                b.AddCylinder(x, z, r, g - 0.5f, g + Math.Max(0.4f, height), cam ? SolidFlags.NoCamera : SolidFlags.None);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Railings

        private static void Railings(TileData t, TileHeightSampler h, RoadSpatialIndex roads, RoadOptions o, SolidBuilder b)
        {
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                if (!roads.IsElevated(ri)) continue;
                RoadRecord r = t.Roads[ri];
                if (!RoadMesher.IsDrawn(r, o)) continue;
                int first, last;
                RoadSpatialIndex.RenderedRange(r, out first, out last);
                if (last <= first) continue;
                RoadStructureRecord st = t.RoadStructureOf(ri);
                float rail = st.RailingHeightM > 0f ? st.RailingHeightM : DefaultRailingM;
                float[] deck = roads.SurfaceHeights(ri);
                int[] p = r.Points;
                int n = last - first + 1;
                var xs = new double[n];
                var zs = new double[n];
                var ys = new float[n];
                var along = new double[n];
                for (int k = 0; k < n; k++)
                {
                    int i = first + k;
                    t.LocalToGame(p[2 * i], p[2 * i + 1], out xs[k], out zs[k]);
                    if (k > 0) along[k] = along[k - 1] + Math.Sqrt((xs[k] - xs[k - 1]) * (xs[k] - xs[k - 1]) + (zs[k] - zs[k - 1]) * (zs[k] - zs[k - 1]));
                }
                if (deck != null)
                {
                    for (int k = 0; k < n; k++) ys[k] = deck[first + k];
                }
                else
                {
                    // The straight deck between the lifted ends (as the ground query's legacy bridge).
                    float lift = RoadMesher.LiftOf(r, o), ha, hb;
                    if (!h.TryHeightClamped(xs[0], zs[0], out ha) || !h.TryHeightClamped(xs[n - 1], zs[n - 1], out hb)) continue;
                    double total = along[n - 1];
                    for (int k = 0; k < n; k++)
                    {
                        float terrain;
                        h.TryHeightClamped(xs[k], zs[k], out terrain);
                        float line = ha + (hb - ha) * (float)(total > 0 ? along[k] / total : 0) + lift;
                        ys[k] = Math.Max(line, terrain + lift);
                    }
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    double px = 0, pz = 0;
                    float py = 0f;
                    bool havePrev = false;
                    for (int k = 0; k < n; k++)
                    {
                        // Mitred normal at the point (right of the point order), scaled so the offset holds at bends.
                        double nx = 0, nz = 0;
                        if (k > 0) AddNormal(xs[k - 1], zs[k - 1], xs[k], zs[k], ref nx, ref nz);
                        if (k < n - 1) AddNormal(xs[k], zs[k], xs[k + 1], zs[k + 1], ref nx, ref nz);
                        double l = Math.Sqrt(nx * nx + nz * nz);
                        if (l < 1e-9) continue;
                        nx /= l;
                        nz /= l;
                        double c = 1.0;
                        if (k > 0 && k < n - 1)
                        {
                            double dx = xs[k] - xs[k - 1], dz = zs[k] - zs[k - 1], dl = Math.Sqrt(dx * dx + dz * dz);
                            if (dl > 1e-9) c = Math.Max(0.5, Math.Abs(nx * (dz / dl) - nz * (dx / dl)));
                        }
                        float half, shift, fl, fr;
                        roads.SectionAt(ri, along[k], out half, out shift, out fl, out fr);
                        double off = shift + side * (half + RoadSpatialIndex.DeckKerbM + RailingHalfM);
                        double qx = xs[k] + nx / c * off, qz = zs[k] + nz / c * off;
                        float terrainHere;
                        if (!h.TryHeightClamped(qx, qz, out terrainHere)) terrainHere = ys[k];
                        bool raised = ys[k] - terrainHere >= RailingMinRiseM;
                        if (havePrev && raised)
                        {
                            float lo = Math.Min(py, ys[k]), hi = Math.Max(py, ys[k]);
                            b.AddWall(px, pz, qx, qz, RailingHalfM, lo - 0.4f, hi + rail, SolidFlags.None);
                        }
                        px = qx;
                        pz = qz;
                        py = ys[k];
                        havePrev = raised;
                    }
                }
            }
        }

        private static void AddNormal(double ax, double az, double bx, double bz, ref double nx, ref double nz)
        {
            double dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-9) return;
            nx += dz / l;
            nz += -dx / l;
        }
    }
}
