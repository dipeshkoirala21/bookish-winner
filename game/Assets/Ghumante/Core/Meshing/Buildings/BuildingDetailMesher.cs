using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// B0 "kit-lite" buildings per detail cell (W2_DESIGN 2.4, 10.3): every building whose outer-ring centroid lies in
    /// the cell gets the house grammar (<see cref="HouseBuilder"/>), the generic sacred generators
    /// (<see cref="SacredSelector"/>, LOD0) for temples, stupas and shrines (an outline the selector does not frame as a
    /// temple, <see cref="SacredOutcome.Fallback"/>, is drawn as its Newar compound building, <see cref="SacredSelector.FallbackHouse"/>),
    /// and the styled B1 extrusion for the other archetypes. A <c>building:part</c> that is a main volume of its building
    /// (from the ground, a house archetype) is a house of the grammar too, with its host outline's front and its own
    /// levels and height (<see cref="Part"/>): carved windows, doors, eaves and balconies on its street and square
    /// walls, never a blank slab; raised parts (upper volumes, roof caps, domes) keep the styled extrusion with its
    /// window rows. Houses are held to <see cref="BuildingOptions.B0CapTris"/> per plot from
    /// <see cref="BuildingOptions.B0BaseDrop"/> (the band table's near ring), or drawn at the tier's lite level (its outer
    /// ring: the flat level on Low; <see cref="BuildingBandTable.LiteOptions(int, BuildingOptions)"/>).
    /// Cells are 64 m, or 32 m for the near ring (<see cref="BuildingBandTable.NearSubdivision"/>). Colliders (pikha
    /// aprons, flat roofs, plinths, temple steps) go to the collider set when given. Deterministic; positions relative to
    /// the tile's south-west corner. Thread-safe for distinct meshes.
    /// </summary>
    public static class BuildingDetailMesher
    {
        /// <summary>Side of a detail cell.</summary>
        public const double CellM = 64.0;

        /// <summary>Detail cells per tile side.</summary>
        public static int CellsPerSide(TileData t)
        {
            return Math.Max(1, (int)Math.Round(t.Tile.Size / CellM));
        }

        /// <summary>Cells per tile side when every detail cell is split <paramref name="subdivision"/> times per side (the
        /// near B0 ring uses <see cref="BuildingBandTable.NearSubdivision"/>; 1 = the 64 m cells).</summary>
        public static int CellsPerSide(TileData t, int subdivision)
        {
            return CellsPerSide(t) * Math.Max(1, subdivision);
        }

        /// <summary>The detail cell (column, row) holding a building's outer-ring centroid.</summary>
        public static void CellOf(TileData t, int buildingIndex, out int cellX, out int cellZ)
        {
            CellOf(t, buildingIndex, 1, out cellX, out cellZ);
        }

        /// <summary>The cell (column, row) of a subdivided grid holding a building's outer-ring centroid.</summary>
        public static void CellOf(TileData t, int buildingIndex, int subdivision, out int cellX, out int cellZ)
        {
            int[] r = t.Buildings[buildingIndex].Rings[0];
            int n = r.Length / 2;
            double cx = 0, cz = 0;
            for (int k = 0; k < n; k++)
            {
                cx += r[2 * k];
                cz += r[2 * k + 1];
            }
            int cells = CellsPerSide(t, subdivision);
            double side = t.Tile.Size / cells;
            cellX = Math.Max(0, Math.Min(cells - 1, (int)Math.Floor(cx / n / 100.0 / side)));
            cellZ = Math.Max(0, Math.Min(cells - 1, (int)Math.Floor(cz / n / 100.0 / side)));
        }

        private sealed class CellLists
        {
            public int[][] One, Near;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TileData, CellLists> CellIndex =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TileData, CellLists>();

        /// <summary>The building indices of each detail cell (row-major, row 0 south), built once per tile.</summary>
        public static int[][] Cells(TileData t)
        {
            return Cells(t, 1);
        }

        /// <summary>The building indices of each cell of a subdivided grid (row-major, row 0 south), built once per tile
        /// and subdivision (1 and <see cref="BuildingBandTable.NearSubdivision"/> are cached).</summary>
        public static int[][] Cells(TileData t, int subdivision)
        {
            subdivision = Math.Max(1, subdivision);
            CellLists l = CellIndex.GetValue(t, k => new CellLists());
            lock (l)
            {
                if (subdivision == 1) return l.One ?? (l.One = Index(t, 1));
                if (subdivision == BuildingBandTable.NearSubdivision) return l.Near ?? (l.Near = Index(t, subdivision));
            }
            return Index(t, subdivision);
        }

        private static int[][] Index(TileData t, int subdivision)
        {
            int n = CellsPerSide(t, subdivision);
            var lists = new System.Collections.Generic.List<int>[n * n];
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                int cx, cz;
                CellOf(t, i, subdivision, out cx, out cz);
                int c = cz * n + cx;
                if (lists[c] == null) lists[c] = new System.Collections.Generic.List<int>();
                lists[c].Add(i);
            }
            var a = new int[n * n][];
            for (int c = 0; c < a.Length; c++) a[c] = lists[c] == null ? new int[0] : lists[c].ToArray();
            return a;
        }

        /// <summary>Build the B0 meshes of one detail cell; returns the number of buildings drawn.</summary>
        public static int BuildCell(TileData t, IHeightSampler h, int cellX, int cellZ, BuildingOptions o, MeshData m, GenColliders c)
        {
            return BuildCell(t, h, cellX, cellZ, 1, o, m, c);
        }

        /// <summary>Build the B0 meshes of one cell of a subdivided grid (the near ring's 32 m cells, or the 64 m cells for
        /// a subdivision of 1); returns the number of buildings drawn.</summary>
        public static int BuildCell(TileData t, IHeightSampler h, int cellX, int cellZ, int subdivision, BuildingOptions o, MeshData m, GenColliders c)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (o == null) o = new BuildingOptions();
            int n = CellsPerSide(t, subdivision);
            if (cellX < 0 || cellZ < 0 || cellX >= n || cellZ >= n) return 0;
            int drawn = 0;
            foreach (int i in Cells(t, subdivision)[cellZ * n + cellX])
                if (One(t, i, h, o, m, c)) drawn++;
            return drawn;
        }

        /// <summary>B0 for the whole tile (tests and previews; streaming builds cells).</summary>
        public static int BuildTile(TileData t, IHeightSampler h, BuildingOptions o, MeshData m, GenColliders c)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (o == null) o = new BuildingOptions();
            int drawn = 0;
            for (int i = 0; i < t.Buildings.Count; i++)
                if (One(t, i, h, o, m, c)) drawn++;
            return drawn;
        }

        /// <summary>One building at B0. Footprints in a road corridor are trimmed (or the building is skipped when it
        /// stands in the road, <see cref="BuildingFootprints"/>); houses are held to <see cref="BuildingOptions.B0CapTris"/>
        /// <b>per plot</b> (<see cref="HouseBudget"/>): each plot of a row starts at the drop level its size predicts and
        /// only a plot that still overflows is rebuilt lighter; the lightest level is always kept, so a house inside the
        /// B0 band is never a bare box. Archetypes without a facade grammar use the styled B1 extrusion.</summary>
        public static bool One(TileData t, int i, IHeightSampler h, BuildingOptions o, MeshData m, GenColliders c)
        {
            if (o == null) o = new BuildingOptions();
            IRoadCorridorQuery q = o.CorridorsFor(t);
            BuildingFootprints guard = BuildingFootprints.For(t, q);
            if (guard.Dropped(i)) return false;
            BuildingRecord b = guard.Record(i);
            if (o.SkipLandmarks && (b.Flags & BuildingFlags.Landmark) != 0) return false;
            if (o.HiddenRefs != null && o.HiddenRefs.Contains(b.OsmRef)) return false;
            if (SacredSelector.HostOf(t, i) >= 0) return false; // a part of a generic sacred outline: the host draws it
            if ((b.Flags & BuildingFlags.HasParts) != 0 && !SacredSelector.DrawsGeneric(b)) return false; // its parts are drawn instead
            if ((b.Flags & BuildingFlags.Part) != 0) return Part(t, i, b, h, o, q, guard, m, c);
            HousePlan plan = guard.Adjust(i, BuildingGrammar.Plan(t, i));
            if (plan.Sacred)
            {
                SacredOutcome how = SacredSelector.Classify(t, i, q);
                if (how == SacredOutcome.Fallback)
                {
                    // Not a temple-shaped outline (a compound round a court or a pond, a block of temple buildings) or
                    // one whose plinth cannot keep out of the road: its Newar compound building, with its outline walls as
                    // colliders (the footprint solids leave sacred outlines to the generators).
                    HousePlan fp;
                    BuildingRecord house = SacredSelector.FallbackHouse(t, i, guard, out fp);
                    int pith;
                    if (house == null) return SacredSelector.BuildGeneric(t, i, h, 0, m, c, q, out pith); // in the road: its stone pith
                    var fenv = new HouseEnv { Clear = new Clearance(t, q), Neighbours = guard.Neighbours, Index = i };
                    var fg = new BuildingGround(t, h);
                    int boxes0 = c == null ? 0 : c.Boxes.Count;
                    if (!HouseBuilder.Build(house, fp, ref fg, fenv, o.SinkM, new HouseBudget(o.B0CapTris, o.B0BaseDrop), m, c)) return false;
                    SacredSelector.DropBoxesInRoads(t, q, c, boxes0);
                    SacredSelector.AddOutlineWalls(t, house, fp, h, o.SinkM, c);
                    return true;
                }
                int vs = m.VertexCount, built;
                if (SacredSelector.BuildGeneric(t, i, h, 0, m, c, q, out built))
                {
                    KitPaint.FillUnset(m, vs);
                    return true;
                }
                return BuildingMesher.Styled(t, b, h, o, plan, m);
            }
            if (!BuildingGrammar.IsHouse(plan.Archetype)) return BuildingMesher.Styled(t, b, h, o, plan, m);
            var env = new HouseEnv { Clear = new Clearance(t, q), Neighbours = guard.Neighbours, Index = i };
            var g = new BuildingGround(t, h);
            return HouseBuilder.Build(b, plan, ref g, env, o.SinkM, new HouseBudget(o.B0CapTris, o.B0BaseDrop), m, c);
        }

        // ------------------------------------------------------------------------------------------------------------
        // building:part

        /// <summary>A part lower than this (m) above the ground is a main volume of its building (drawn by the house
        /// grammar); a higher one is an upper volume, a roof cap or a dome on top of another part.</summary>
        public const double PartGroundM = 0.5;

        /// <summary>Walls shorter than this (m) are not a storey: the part is a plinth, a canopy or a cap.</summary>
        public const double PartMinWallM = 2.5;

        /// <summary>
        /// One <c>building:part</c> at B0 (not one of a generic sacred outline, which draws its parts itself). A main
        /// volume (standing on the ground, <see cref="PartGroundM"/>, with at least one storey of wall, a house archetype)
        /// is built by the house grammar like any house: the plan keeps the part's own levels and height
        /// (<see cref="BuildingGrammar.Plan(TileData, int)"/>), its profile is its host outline's (BFNT), its front is the
        /// part's edge on its host's front (or corner) edge, else its outer edge facing the nearest road or square
        /// (<see cref="PartFronts"/>); its other outer walls are dressed as street or open sides by the grammar (the host
        /// outline is no neighbour: it is never drawn), its walls against a sibling part stay blank, and a part another
        /// volume stands on gets a flat terrace (<see cref="HouseEnv.FlatRoof"/>). Raised parts and parts of other
        /// archetypes keep the styled extrusion with its window rows (the B1 front detail).
        /// </summary>
        private static bool Part(TileData t, int i, BuildingRecord b, IHeightSampler h, BuildingOptions o, IRoadCorridorQuery q, BuildingFootprints guard,
                                 MeshData m, GenColliders c)
        {
            HousePlan plan = BuildingGrammar.Plan(t, i);
            bool body = b.MinHeightCm / 100.0 < PartGroundM && plan.WallTopM - plan.MinHeightM >= PartMinWallM && BuildingGrammar.IsHouse(plan.Archetype);
            PartIndex x = PartsOf(t);
            if (!body)
            {
                plan = guard.Adjust(i, plan);
                if (!BuildingMesher.Styled(t, b, h, o, plan, m)) return false;
                if (plan.WallTopM - plan.MinHeightM >= PartMinWallM) PartWindowRows(t, i, b, plan, h, x, guard, m);
                return true;
            }
            int front, second;
            PartFronts(t, i, b.Rings[0], x, guard, out front, out second);
            plan.FrontEdge = front;
            plan.SecondEdge = second;
            var env = new HouseEnv { Clear = new Clearance(t, q), Neighbours = guard.Neighbours, Index = i, FlatRoof = x.Covered[i] };
            var g = new BuildingGround(t, h);
            return HouseBuilder.Build(b, plan, ref g, env, o.SinkM, new HouseBudget(o.B0CapTris, o.B0BaseDrop), m, c);
        }

        /// <summary>
        /// Windows on every outer wall of a styled part (an upper volume, a part of another archetype) that faces a road
        /// or a square (<see cref="Facing"/>'s rule, every such wall, not only the front): per storey of the plan a row of
        /// framed, glazed openings about 1 m wide on a 2.2 m pitch, a little proud of the wall, so no wall of a part that
        /// looks onto a street is a blank slab.
        /// </summary>
        private static void PartWindowRows(TileData t, int i, BuildingRecord b, in HousePlan plan, IHeightSampler h, PartIndex x, BuildingFootprints guard,
                                           MeshData m)
        {
            int[] ring = b.Rings[0];
            int n = ring.Length / 2;
            if (n < 3) return;
            // Counter-clockwise (outward = right of travel), as the styled extrusion.
            double area = 0;
            for (int k = 0, j = n - 1; k < n; j = k++) area += (double)ring[2 * j] * ring[2 * k + 1] - (double)ring[2 * k] * ring[2 * j + 1];
            bool ccw = area > 0;
            var g = new BuildingGround(t, h);
            double cx, cz;
            Centroid(ring, out cx, out cz);
            double ground = g.Height(cx, cz);
            for (int k = 0; k < n; k++) ground = Math.Min(ground, g.Height(ring[2 * k] / 100.0, ring[2 * k + 1] / 100.0));
            uint frame = plan.Wood != 0 ? plan.Wood : BuildingGrammar.SalDark;
            uint glass = BuildingGrammar.Glass;
            for (int e = 0; e < n; e++)
            {
                int j = e + 1 == n ? 0 : e + 1;
                int ia = ccw ? e : j, ib = ccw ? j : e;
                double ax = ring[2 * ia] / 100.0, az = ring[2 * ia + 1] / 100.0, bx = ring[2 * ib] / 100.0, bz = ring[2 * ib + 1] / 100.0;
                double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 2.0) continue;
                if (!FacesStreet(t, x, guard, i, ax, az, bx, bz, plan.MinHeightM, plan.WallTopM)) continue;
                var f = new KitFrame(ax, ground, az, dx, dz);
                int windows = Math.Max(1, (int)Math.Floor((len - 0.6) / 2.2));
                double pitch = len / windows;
                for (int st = 0; st < plan.Storeys; st++)
                {
                    double v0 = plan.FloorBase(st), v1 = st + 1 < plan.Storeys ? plan.FloorBase(st + 1) : plan.WallTopM;
                    if (v1 - v0 < 1.6) continue;
                    double a = v0 + 0.75, c = Math.Min(v1 - 0.35, v0 + 2.0);
                    for (int w = 0; w < windows; w++)
                    {
                        double um = (w + 0.5) * pitch, hw = Math.Min(0.55, 0.35 * pitch);
                        int vf = m.VertexCount;
                        MeshKit.Panel(m, f, um - hw - 0.08, a - 0.1, um + hw + 0.08, c + 0.12, 0.02, frame);
                        KitPaint.Of(MaterialChannel.Wood, 0.9f).Apply(m, vf);
                        int vg = m.VertexCount;
                        MeshKit.Panel(m, f, um - hw, a, um + hw, c, 0.03, glass);
                        KitPaint.Of(MaterialChannel.Glass, 0.75f).Apply(m, vg);
                    }
                }
            }
        }

        /// <summary>True when the outer side of the wall a→b (a counter-clockwise ring) between heights
        /// <paramref name="y0"/> and <paramref name="y1"/> over the ground looks onto a road within
        /// <see cref="BuildingFronts.SearchM"/> or a square 3 m out, with no other drawn volume against it at that height (an
        /// upper volume's wall over the terrace of the part below it is open).</summary>
        private static bool FacesStreet(TileData t, PartIndex x, BuildingFootprints guard, int self, double ax, double az, double bx, double bz, double y0,
                                        double y1)
        {
            double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6) return false;
            double nx = dz / len, nz = -dx / len, mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
            if (y0 < PartGroundM ? guard.Neighbours.Inside(mx + 0.6 * nx, mz + 0.6 * nz, self) : AbutsAt(t, guard, self, mx + 0.6 * nx, mz + 0.6 * nz, y0, y1))
                return false;
            RoadClass cls;
            if (!double.IsPositiveInfinity(x.Roads.NearestInFront(mx, mz, nx, nz, BuildingFronts.SearchM, out cls))) return true;
            return OnSquare(t, mx + 3.0 * nx, mz + 3.0 * nz);
        }

        /// <summary>The part's host (the non-sacred outline with HAS_PARTS holding its centroid, DATA_FORMATS 1.6; -1 for
        /// none) and whether another part stands on it, per part of a tile.</summary>
        internal sealed class PartIndex
        {
            public int[] Host;
            public bool[] Covered;
            public BuildingFronts.SegmentGrid Roads;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TileData, PartIndex> PartCache =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TileData, PartIndex>();

        /// <summary>The part index of a tile (built once, cached; immutable afterwards).</summary>
        internal static PartIndex PartsOf(TileData t)
        {
            return PartCache.GetValue(t, k =>
            {
                int n = k.Buildings.Count;
                var x = new PartIndex { Host = new int[n], Covered = new bool[n], Roads = new BuildingFronts.SegmentGrid(k, 16.0) };
                var hosts = new System.Collections.Generic.List<int>();
                for (int i = 0; i < n; i++)
                {
                    BuildingRecord b = k.Buildings[i];
                    if ((b.Flags & BuildingFlags.HasParts) != 0 && (b.Flags & BuildingFlags.Part) == 0 && b.Rings[0].Length >= 6) hosts.Add(i);
                }
                for (int i = 0; i < n; i++)
                {
                    x.Host[i] = -1;
                    BuildingRecord p = k.Buildings[i];
                    if ((p.Flags & BuildingFlags.Part) == 0 || p.Rings[0].Length < 6) continue;
                    double px, pz;
                    Centroid(p.Rings[0], out px, out pz);
                    foreach (int hi in hosts)
                        if (BuildingBands.FootprintIndex.InRing(k.Buildings[hi].Rings[0], px, pz))
                        {
                            x.Host[i] = hi;
                            break;
                        }
                    // Covered: another part starts where this one stops (within 0.5 m) over its plan.
                    for (int j = 0; j < n && !x.Covered[i]; j++)
                    {
                        BuildingRecord o = k.Buildings[j];
                        if (j == i || (o.Flags & BuildingFlags.Part) == 0 || o.Rings[0].Length < 6) continue;
                        if (o.MinHeightCm <= p.MinHeightCm || o.MinHeightCm + 50 < p.HeightCm) continue;
                        double ox, oz;
                        Centroid(o.Rings[0], out ox, out oz);
                        x.Covered[i] = BuildingBands.FootprintIndex.InRing(p.Rings[0], ox, oz) || BuildingBands.FootprintIndex.InRing(o.Rings[0], px, pz);
                    }
                }
                return x;
            });
        }

        private static void Centroid(int[] r, out double x, out double z)
        {
            int n = r.Length / 2;
            x = z = 0;
            for (int k = 0; k < n; k++)
            {
                x += r[2 * k] / 100.0;
                z += r[2 * k + 1] / 100.0;
            }
            x /= n;
            z /= n;
        }

        /// <summary>
        /// The front and corner edges of a part's ring (indices of <paramref name="ring"/>; -1 for none): the part's
        /// longest edge on its host's BFNT front edge, else on its host's corner edge (which is then the front); otherwise
        /// the outer edge (not against another drawn footprint) that faces the nearest road, or a square or courtyard in
        /// front of it (DATA_FORMATS 1.13: parts have no front of their own). The corner edge: a part edge on the host's
        /// other street edge, else an outer edge turned at least 45° from the front with a road within 15 m.
        /// </summary>
        internal static void PartFronts(TileData t, int i, int[] ring, PartIndex x, BuildingFootprints guard, out int front, out int second)
        {
            front = second = -1;
            int n = ring.Length / 2;
            int host = x.Host[i];
            if (host >= 0 && t.HasBuildingFronts)
            {
                BuildingFrontRecord hf = BuildingFronts.For(t).Front(host);
                int[] hr = t.Buildings[host].Rings[0];
                int onFront = hf.HasFront ? EdgeOn(ring, hr, hf.FrontEdge) : -1;
                int onSecond = hf.SecondEdge != BuildingFrontRecord.NoEdge ? EdgeOn(ring, hr, hf.SecondEdge) : -1;
                if (onFront >= 0)
                {
                    front = onFront;
                    second = onSecond != onFront ? onSecond : -1;
                    return;
                }
                if (onSecond >= 0)
                {
                    front = onSecond;
                    // The corner edge: another outer edge on a street.
                    second = Facing(t, ring, x, guard, i, front, 15.0);
                    return;
                }
            }
            front = Facing(t, ring, x, guard, i, -1, BuildingFronts.SearchM);
            if (front >= 0) second = Facing(t, ring, x, guard, i, front, 15.0);
        }

        /// <summary>The longest edge of <paramref name="ring"/> lying on edge <paramref name="e"/> of
        /// <paramref name="host"/> (both ends within 0.2 m of its line, its middle within its extent), else -1.</summary>
        private static int EdgeOn(int[] ring, int[] host, int e)
        {
            int hn = host.Length / 2;
            if (e < 0 || e >= hn) return -1;
            int ej = e + 1 == hn ? 0 : e + 1;
            double ax = host[2 * e] / 100.0, az = host[2 * e + 1] / 100.0, bx = host[2 * ej] / 100.0, bz = host[2 * ej + 1] / 100.0;
            int n = ring.Length / 2, best = -1;
            double bl = 0.5;
            for (int k = 0; k < n; k++)
            {
                int j = k + 1 == n ? 0 : k + 1;
                double px = ring[2 * k] / 100.0, pz = ring[2 * k + 1] / 100.0, qx = ring[2 * j] / 100.0, qz = ring[2 * j + 1] / 100.0;
                double len = Math.Sqrt((qx - px) * (qx - px) + (qz - pz) * (qz - pz));
                if (len <= bl) continue;
                if (Plane2.PointSeg(px, pz, ax, az, bx, bz) > 0.2 || Plane2.PointSeg(qx, qz, ax, az, bx, bz) > 0.2) continue;
                // Same direction as the host edge (both rings counter-clockwise).
                if ((qx - px) * (bx - ax) + (qz - pz) * (bz - az) <= 0) continue;
                best = k;
                bl = len;
            }
            return best;
        }

        /// <summary>
        /// The outer edge of a part ring that faces the nearest road (the BFNT rule: the road segment nearest the edge
        /// middle within <paramref name="max"/> lies on its outer side; footways count a little less) or, failing that, a
        /// square or courtyard (a pedestrian, marketplace or courtyard area, or a heritage zone, 3 m in front of it, which
        /// counts as a road 4 m away). With <paramref name="turnedFrom"/> ≥ 0 only edges turned at least 45° from that edge
        /// count (the corner edge). Edges shorter than 2 m and edges against another drawn footprint are skipped.
        /// </summary>
        private static int Facing(TileData t, int[] ring, PartIndex x, BuildingFootprints guard, int self, int turnedFrom, double max)
        {
            int n = ring.Length / 2, best = -1;
            double bestScore = double.PositiveInfinity, fnx = 0, fnz = 0;
            if (turnedFrom >= 0)
            {
                int j = turnedFrom + 1 == n ? 0 : turnedFrom + 1;
                double dx = (ring[2 * j] - ring[2 * turnedFrom]) / 100.0, dz = (ring[2 * j + 1] - ring[2 * turnedFrom + 1]) / 100.0, l = Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-6) return -1;
                fnx = dz / l;
                fnz = -dx / l;
            }
            for (int k = 0; k < n; k++)
            {
                if (k == turnedFrom) continue;
                int j = k + 1 == n ? 0 : k + 1;
                double ax = ring[2 * k] / 100.0, az = ring[2 * k + 1] / 100.0, bx = ring[2 * j] / 100.0, bz = ring[2 * j + 1] / 100.0;
                double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 2.0) continue;
                double nx = dz / len, nz = -dx / len, mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
                if (turnedFrom >= 0 && nx * fnx + nz * fnz > 0.707) continue;
                if (guard.Neighbours.Inside(mx + 0.6 * nx, mz + 0.6 * nz, self)) continue; // a party wall
                RoadClass cls;
                double d = x.Roads.NearestInFront(mx, mz, nx, nz, max, out cls);
                if (double.IsPositiveInfinity(d) && OnSquare(t, mx + 3.0 * nx, mz + 3.0 * nz)) d = 4.0;
                if (double.IsPositiveInfinity(d)) continue;
                double score = d - 0.05 * len;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = k;
                }
            }
            return best;
        }

        /// <summary>True when a tile-local point lies inside another drawn footprint whose volume overlaps the heights
        /// [<paramref name="y0"/>, <paramref name="y1"/>] over the ground (outlines drawn by their parts left out).</summary>
        private static bool AbutsAt(TileData t, BuildingFootprints guard, int self, double x, double z, double y0, double y1)
        {
            for (int j = 0; j < t.Buildings.Count; j++)
            {
                if (j == self || guard.Dropped(j)) continue;
                BuildingRecord o = guard.Record(j);
                if ((o.Flags & BuildingFlags.HasParts) != 0 && !BuildingGrammar.IsSacred(o.Archetype)) continue;
                double lo = o.MinHeightCm / 100.0, hi = o.HeightCm > 0 ? o.HeightCm / 100.0 : 1e9;
                if (hi <= y0 + 0.3 || lo >= y1 - 0.3) continue;
                if (BuildingBands.FootprintIndex.InRing(o.Rings[0], x, z)) return true;
            }
            return false;
        }

        /// <summary>True when a tile-local point lies on a square: a pedestrian, marketplace or courtyard area, or any
        /// area of a heritage zone (Durbar squares, Basantapur).</summary>
        internal static bool OnSquare(TileData t, double x, double z)
        {
            double xc = x * 100.0, zc = z * 100.0;
            foreach (AreaRecord a in t.Areas)
            {
                bool square = a.Kind == AreaKind.Pedestrian || a.Kind == AreaKind.Marketplace || a.Kind == AreaKind.Courtyard ||
                              (a.Flags & AreaFlags.HeritageZone) != 0;
                if (!square || a.Indices == null || a.Vertices == null) continue;
                int[] v = a.Vertices, ix = a.Indices;
                for (int k = 0; k + 2 < ix.Length; k += 3)
                {
                    double x0 = v[2 * ix[k]], z0 = v[2 * ix[k] + 1], x1 = v[2 * ix[k + 1]], z1 = v[2 * ix[k + 1] + 1], x2 = v[2 * ix[k + 2]], z2 = v[2 * ix[k + 2] + 1];
                    double d0 = (x1 - x0) * (zc - z0) - (z1 - z0) * (xc - x0), d1 = (x2 - x1) * (zc - z1) - (z2 - z1) * (xc - x1), d2 = (x0 - x2) * (zc - z2) - (z0 - z2) * (xc - x2);
                    if (d0 >= 0 && d1 >= 0 && d2 >= 0 || d0 <= 0 && d1 <= 0 && d2 <= 0) return true;
                }
            }
            return false;
        }
    }
}
