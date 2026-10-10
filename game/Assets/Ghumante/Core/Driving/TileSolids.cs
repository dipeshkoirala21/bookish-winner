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
    /// hidden hero footprints and outlines whose parts are drawn instead are left out. As a runtime guard
    /// (<see cref="FootprintGuard"/>) an outline never reaches into a road: the part inside is trimmed back to the edge
    /// of the road it intrudes into, on the building's side (decision 1).</item>
    /// <item><b>Point objects</b> (PROP): tree trunks, poles, lamps, masts, towers, chimneys, tanks, wells, taps, benches
    /// and statues (artwork), each a cylinder sized by kind; never one standing on a drawn carriageway.</item>
    /// <item><b>Railings and parapets</b> along both edges of every bridge and flyover, walked every
    /// <see cref="RailingStepM"/>: wherever the deck (structure heights, or the straight deck of a bridge without them)
    /// stands at least <see cref="RailingMinRiseM"/> above the terrain beside it, and along the whole span of a bridge
    /// over water, except where another road meets the deck at grade (<see cref="AtGradeM"/>). Each piece follows the
    /// local deck height (its bottom and top vary by at most <see cref="RailingMaxRiseM"/>), so a railing never reaches
    /// down over a road passing under a ramp.</item>
    /// <item><b>Retaining walls</b> along a lowered road (an underpass trench) wherever it runs more than
    /// <see cref="TileGroundQuery.HardEdgeM"/> below the ground beside it, from under the road to a parapet
    /// <see cref="RetainingParapetM"/> above that ground.</item>
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

        /// <summary>Railings and retaining walls are walked in steps of at most this many metres along the road.</summary>
        public const double RailingStepM = 1.5;

        /// <summary>Most a railing piece's bottom or top changes along it: on a ramp the pieces are short, so each one
        /// hugs the local deck height.</summary>
        public const float RailingMaxRiseM = 0.25f;

        /// <summary>Longest railing piece (straight, level runs merge into one).</summary>
        public const double RailingMaxPieceM = 24.0;

        /// <summary>A railing reaches this far below the deck surface (into the slab, never under it).</summary>
        public const float RailingBelowDeckM = 0.4f;

        /// <summary>A railing station on another road's carriageway whose surface is less than this from the deck is
        /// left open (a street joining at grade at the end of a bridge).</summary>
        public const float AtGradeM = 1.5f;

        /// <summary>Height of the parapet on a retaining wall above the ground beside a lowered road.</summary>
        public const float RetainingParapetM = 1.0f;

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

        private static void Buildings(TileData t, TileHeightSampler h, RoadSpatialIndex roads, IRoadCorridorQuery corridors, ISet<ulong> hidden,
                                      SolidBuilder b)
        {
            if (t.Buildings.Count == 0) return;
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
            FootprintGuard guard = roads != null && roads.SegmentCount > 0 ? new FootprintGuard(roads, corridors) : null;
            double[] xs = new double[64], zs = new double[64], hx = new double[16], hz = new double[16];
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
                int n = Ring(t, outer, ref xs, ref zs);
                bool trimmed = guard != null && guard.Trim(xs, zs, n);
                int pieces = trimmed ? guard.PieceCount : 1;
                for (int piece = 0; piece < pieces; piece++)
                {
                    if (trimmed) n = guard.CopyPiece(piece, ref xs, ref zs);
                    if (n < 3) continue;
                    b.BeginGroup();
                    if (canopy)
                    {
                        // Open canopy (petrol stations, pavilions): the roof slab above head height, posts at the corners
                        // that stand clear of the road.
                        float slabBottom = Math.Max(bottom, top - 0.6f);
                        b.AddRing(xs, zs, n, WallHalfM, slabBottom, top, SolidFlags.None);
                        if (piece > 0) continue;
                        for (int k = 0; k + 1 < outer.Length; k += 2)
                        {
                            double px = x0 + outer[k] / 100.0, pz = z0 + outer[k + 1] / 100.0;
                            if (guard != null && guard.InRoad(px, pz)) continue;
                            b.AddCylinder(px, pz, 0.2f, bottom, slabBottom, SolidFlags.NoCamera);
                        }
                        continue;
                    }
                    b.AddRing(xs, zs, n, WallHalfM, bottom, top, SolidFlags.None);
                    if (trimmed && pieces > 1) continue; // holes of a footprint cut in two are dropped
                    for (int ring = 1; ring < r.Rings.Length; ring++)
                    {
                        // Courtyards stay as drawn, except one that opens onto a road: filled, so no wall of it stands in
                        // the road.
                        int[] pts = r.Rings[ring];
                        if (pts == null || pts.Length < 6) continue;
                        int hn = Ring(t, pts, ref hx, ref hz);
                        if (guard != null && guard.Intrudes(hx, hz, hn)) continue;
                        b.AddRing(hx, hz, hn, WallHalfM, bottom, top, SolidFlags.None);
                    }
                }
            }
        }

        /// <summary>A ring of tile-local centimetre pairs in game metres, into (x, z) (grown as needed); returns its
        /// point count.</summary>
        private static int Ring(TileData t, int[] pts, ref double[] x, ref double[] z)
        {
            int n = pts.Length / 2;
            if (x.Length < n) x = new double[Math.Max(n, 2 * x.Length)];
            if (z.Length < x.Length) z = new double[x.Length];
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            for (int k = 0; k < n; k++)
            {
                x[k] = x0 + pts[2 * k] / 100.0;
                z[k] = z0 + pts[2 * k + 1] / 100.0;
            }
            return n;
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
        // Railings and retaining walls

        /// <summary>Merges consecutive railing stations into straight pieces whose bottom and top each stay within
        /// <see cref="RailingMaxRiseM"/>, and adds them as walls.</summary>
        private sealed class RailRun
        {
            private readonly SolidBuilder _b;
            private bool _open, _havePrev;
            private double _sx, _sz, _ex, _ez, _dirX, _dirZ, _len;
            private float _loB, _hiB, _loT, _hiT;
            private double _px, _pz;
            private float _pb, _pt;
            private bool _pRaised;

            public RailRun(SolidBuilder b)
            {
                _b = b;
            }

            public void Begin()
            {
                _open = false;
                _havePrev = false;
            }

            /// <summary>The next station (x, z) with the wall's bottom and top there; a piece joins it to the previous
            /// station when either of them needs a wall.</summary>
            public void Add(double x, double z, float bottom, float top, bool raised)
            {
                if (_havePrev && (raised || _pRaised)) Piece(_px, _pz, _pb, _pt, x, z, bottom, top);
                else Flush();
                _px = x;
                _pz = z;
                _pb = bottom;
                _pt = top;
                _pRaised = raised;
                _havePrev = true;
            }

            public void End()
            {
                Flush();
                _havePrev = false;
            }

            private void Piece(double ax, double az, float ab, float at, double bx, double bz, float bb, float bt)
            {
                double dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-6) return;
                dx /= l;
                dz /= l;
                if (_open)
                {
                    float loB = Math.Min(_loB, Math.Min(ab, bb)), hiB = Math.Max(_hiB, Math.Max(ab, bb));
                    float loT = Math.Min(_loT, Math.Min(at, bt)), hiT = Math.Max(_hiT, Math.Max(at, bt));
                    bool straight = dx * _dirX + dz * _dirZ > 0.99995 && Math.Abs(ax - _ex) < 1e-6 && Math.Abs(az - _ez) < 1e-6;
                    if (straight && hiB - loB <= RailingMaxRiseM && hiT - loT <= RailingMaxRiseM && _len + l <= RailingMaxPieceM)
                    {
                        _ex = bx;
                        _ez = bz;
                        _len += l;
                        _loB = loB;
                        _hiB = hiB;
                        _loT = loT;
                        _hiT = hiT;
                        return;
                    }
                    Flush();
                }
                _open = true;
                _sx = ax;
                _sz = az;
                _ex = bx;
                _ez = bz;
                _dirX = dx;
                _dirZ = dz;
                _len = l;
                _loB = Math.Min(ab, bb);
                _hiB = Math.Max(ab, bb);
                _loT = Math.Min(at, bt);
                _hiT = Math.Max(at, bt);
            }

            private void Flush()
            {
                if (!_open) return;
                _open = false;
                _b.AddWall(_sx, _sz, _ex, _ez, RailingHalfM, _loB, _hiT, SolidFlags.None);
            }
        }

        /// <summary>True when a bridge's railings run its whole span: a bridge over water (structure kind or flag), or a
        /// bridge-flagged road without structure data.</summary>
        private static bool WholeSpan(RoadRecord r, RoadStructureRecord st)
        {
            return st.Kind == RoadStructureKind.Bridge || st.Has(RoadStructureFlags.WaterCrossing) ||
                   st.Kind == RoadStructureKind.None && (r.Flags & RoadFlags.Bridge) != 0;
        }

        private static void Railings(TileData t, TileHeightSampler h, RoadSpatialIndex roads, RoadOptions o, SolidBuilder b)
        {
            var run = new RailRun(b);
            double[] xs = new double[16], zs = new double[16], along = new double[16];
            float[] ys = new float[16];
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                bool elevated = roads.IsElevated(ri);
                float[] deck = roads.SurfaceHeights(ri);
                if (!elevated && deck == null) continue;
                RoadRecord r = t.Roads[ri];
                if (!RoadMesher.IsDrawn(r, o)) continue;
                int first, last;
                RoadSpatialIndex.RenderedRange(r, out first, out last);
                if (last <= first) continue;
                RoadStructureRecord st = t.RoadStructureOf(ri);
                float rail = st.RailingHeightM > 0f ? st.RailingHeightM : DefaultRailingM;
                float lift = RoadMesher.LiftOf(r, o);
                bool whole = elevated && WholeSpan(r, st);
                int[] p = r.Points;
                int n = last - first + 1;
                if (xs.Length < n)
                {
                    xs = new double[2 * n];
                    zs = new double[2 * n];
                    along = new double[2 * n];
                    ys = new float[2 * n];
                }
                for (int k = 0; k < n; k++)
                {
                    int i = first + k;
                    t.LocalToGame(p[2 * i], p[2 * i + 1], out xs[k], out zs[k]);
                    along[k] = k > 0 ? along[k - 1] + Math.Sqrt((xs[k] - xs[k - 1]) * (xs[k] - xs[k - 1]) + (zs[k] - zs[k - 1]) * (zs[k] - zs[k - 1])) : 0.0;
                }
                if (deck != null)
                {
                    for (int k = 0; k < n; k++) ys[k] = deck[first + k];
                }
                else
                {
                    // The straight deck between the lifted ends (as the ground query's bridge without heights).
                    float ha, hb;
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
                    run.Begin();
                    for (int k = 0; k + 1 < n; k++)
                    {
                        double dx = xs[k + 1] - xs[k], dz = zs[k + 1] - zs[k];
                        double len = along[k + 1] - along[k];
                        if (len < 1e-6) continue;
                        double nx = dz / len, nz = -dx / len; // right normal of the segment
                        int pieces = Math.Max(1, (int)Math.Ceiling(len / RailingStepM));
                        for (int q = k == 0 ? 0 : 1; q <= pieces; q++)
                        {
                            double f = q / (double)pieces;
                            double cx = xs[k] + dx * f, cz = zs[k] + dz * f;
                            float y = ys[k] + (ys[k + 1] - ys[k]) * (float)f;
                            // At a vertex the mitred normal of both segments, so the pieces of a bend meet.
                            double mx = nx, mz = nz, scale = 1.0;
                            int v = q == 0 ? k : q == pieces ? k + 1 : -1;
                            if (v >= 0) Mitre(xs, zs, n, v, out mx, out mz, out scale);
                            float half, shift, fl, fr;
                            roads.SectionAt(ri, along[k] + len * f, out half, out shift, out fl, out fr);
                            double reach = elevated ? half + RoadSpatialIndex.DeckKerbM : half + (side < 0 ? fl : fr);
                            double off = (shift + side * (reach + RailingHalfM)) * scale;
                            double qx = cx + mx * off, qz = cz + mz * off;
                            float ground;
                            if (!h.TryHeightClamped(qx, qz, out ground)) ground = y;
                            if (elevated)
                            {
                                bool raised = (whole || y - ground >= RailingMinRiseM) && !AtGradeRoad(t, h, roads, o, ri, qx, qz, y);
                                run.Add(qx, qz, y - RailingBelowDeckM, y + rail, raised);
                            }
                            else
                            {
                                // A lowered road: a retaining wall where it runs below the hard edge.
                                bool cut = ground + lift - y > TileGroundQuery.HardEdgeM;
                                run.Add(qx, qz, y - RailingBelowDeckM, Math.Max(y + rail, ground + RetainingParapetM), cut);
                            }
                        }
                    }
                    run.End();
                }
            }
        }

        /// <summary>True when (x, z) lies on the carriageway of another road whose surface is within
        /// <see cref="AtGradeM"/> of the deck height <paramref name="y"/> there: a street meeting the bridge's end at grade,
        /// where a railing would stand in its way (a road passing under the deck lies far lower and keeps it).</summary>
        private static bool AtGradeRoad(TileData t, TileHeightSampler h, RoadSpatialIndex roads, RoadOptions o, int self, double x, double z, float y)
        {
            RoadHit hit;
            if (!roads.TryNearest(x, z, 0.0, RoadFlags.None, RoadFlags.None, RoadLayer.Any, out hit) || hit.RoadIndex == self) return false;
            float surface, grade;
            if (!roads.TrySurfaceHeight(in hit, out surface, out grade))
            {
                float terrain;
                if (!h.TryHeightClamped(x, z, out terrain)) return false;
                surface = terrain + RoadMesher.LiftOf(hit.Road, o);
            }
            return Math.Abs(y - surface) < AtGradeM;
        }

        /// <summary>The mitred unit normal (right of the point order) at vertex <paramref name="v"/>, and the offset
        /// scale that keeps a parallel line at its distance across the bend (at most 2).</summary>
        private static void Mitre(double[] xs, double[] zs, int n, int v, out double mx, out double mz, out double scale)
        {
            double nx = 0, nz = 0;
            if (v > 0) AddNormal(xs[v - 1], zs[v - 1], xs[v], zs[v], ref nx, ref nz);
            if (v < n - 1) AddNormal(xs[v], zs[v], xs[v + 1], zs[v + 1], ref nx, ref nz);
            double l = Math.Sqrt(nx * nx + nz * nz);
            if (l < 1e-9)
            {
                // A U-turn: the normal of the incoming segment.
                nx = nz = 0;
                AddNormal(xs[Math.Max(0, v - 1)], zs[Math.Max(0, v - 1)], xs[Math.Min(n - 1, Math.Max(1, v))], zs[Math.Min(n - 1, Math.Max(1, v))], ref nx, ref nz);
                l = Math.Max(1e-9, Math.Sqrt(nx * nx + nz * nz));
            }
            mx = nx / l;
            mz = nz / l;
            scale = 1.0;
            if (v > 0 && v < n - 1)
            {
                double dx = xs[v] - xs[v - 1], dz = zs[v] - zs[v - 1], dl = Math.Sqrt(dx * dx + dz * dz);
                if (dl > 1e-9) scale = 1.0 / Math.Max(0.5, Math.Abs(mx * (dz / dl) - mz * (dx / dl)));
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
