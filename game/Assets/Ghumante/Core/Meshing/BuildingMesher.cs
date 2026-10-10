using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>Options for <see cref="BuildingMesher"/>.</summary>
    public sealed class BuildingOptions
    {
        /// <summary>Walls start this far below the lowest ground point under the footprint, so slopes never show
        /// a gap under a building.</summary>
        public float SinkM = 1f;

        /// <summary>Skip LANDMARK buildings (their hero prefab replaces them, ARCHITECTURE 7.5; W2).</summary>
        public bool SkipLandmarks = false;

        /// <summary>Outer rings smaller than this (m²) are skipped as degenerate.</summary>
        public float MinAreaM2 = 1f;

        /// <summary>Draw the parapet lip of flat roofs (inner faces); off saves 8 triangles per building.</summary>
        public bool Parapets = true;

        /// <summary>LOD band (W2_DESIGN 2.4). B1 (default) is the W1 extrusion styled from the building's
        /// <see cref="HousePlan"/>; B0 draws the full grammar for the whole tile (the streaming path builds B0 per 64 m
        /// cell with <see cref="BuildingDetailMesher.BuildCell"/>); B2 and B3 are the far bands.</summary>
        public BuildingBand Band = BuildingBand.B1Styled;

        /// <summary>Take heights, roofs and colours from the grammar plan (<see cref="BuildingGrammar.Plan(TileData, int)"/>:
        /// W2 storey stacks and the profile palettes), so every band agrees with B0. Off: the plain W1 extrusion.</summary>
        public bool Styled = true;

        /// <summary>B1 street-front detail (W2_DESIGN 2.4): the front paint, a floor band and one window-row quad per
        /// storey on the front edge (about 12 triangles more per building at Asan). The detail pass's band table
        /// (BuildingBandTable) budgets B1 with it on, since B1 now takes over from the richer B0 nearer the camera; it
        /// stays off by default so whole-tile builds (the streaming budget tests) keep the W1 cost until the B1 layer's
        /// options turn it on.</summary>
        public bool FrontDetail = false;

        /// <summary>Buildings (BLDG osm_ref) hidden under a hero replica (D5 hide zones); null = none.</summary>
        public System.Collections.Generic.ISet<ulong> HiddenRefs;

        /// <summary>B0 triangle cap per house (W2_DESIGN 2.4, raised for the detail pass): over it the grammar drops
        /// detail (lattice relief and small props, struts and tile courses, floor bands and railings, roof props) until
        /// it fits.</summary>
        public int B0CapTris = BuildingBandTable.B0CapTris;

        /// <summary>Keep buildings out of the roads (docs/W2_DETAIL_CONTRACT.md decisions 1 and 2): footprints that
        /// intrude into a road corridor are trimmed back to it (or dropped when the house stands in the road) in every
        /// band, and nothing below <see cref="RoadClearance.MinOverheadClearanceM"/> projects over a corridor in B0. On by
        /// default.</summary>
        public bool RoadGuard = true;

        /// <summary>The road corridors of a tile (the roads package's <c>RoadCorridorIndex.ForTile</c>); null uses the
        /// building package's stand-in built from the tile's roads at their game widths. Must return the same
        /// instance for the same tile (the guard is cached per tile and corridor source).</summary>
        public Func<TileData, IRoadCorridorQuery> Corridors;

        /// <summary>The corridor query for a tile under these options (null when <see cref="RoadGuard"/> is off).</summary>
        public IRoadCorridorQuery CorridorsFor(TileData t)
        {
            if (!RoadGuard || t == null) return null;
            IRoadCorridorQuery q = Corridors != null ? Corridors(t) : null;
            return q ?? RoadCorridorStandIn.For(t);
        }
    }

    /// <summary>
    /// Buildings from BLDG as extruded blocks with roof shapes (ARCHITECTURE.md 7.5 LOD1, W1). Walls rise from the
    /// lowest sampled ground under the outer ring minus <see cref="BuildingOptions.SinkM"/> (from ground +
    /// min_height for raised parts) to the eaves; the total height (the record's, or storeys × 3 m plus the roof
    /// allowance, <see cref="BuildingStyle.HeightM"/>) includes the roof. Hole rings get inward-facing walls; roofs
    /// ignore holes in W1.
    /// <list type="bullet">
    /// <item>FLAT: a deck 0.5 m below the wall tops behind a parapet lip (ear-clipped, so concave footprints work).</item>
    /// <item>GABLED / HIPPED / SKILLION on quads: the ridge runs along the longer pair of opposite edges (the long
    /// side of the oriented box); on other convex footprints a pyramid to the centroid; on concave ones a flat roof.</item>
    /// <item>PYRAMIDAL, CONE: a pyramid to the centroid. DOME, ONION: a faceted dome. PAGODA: stacked tiers.
    /// SHIKHARA: a tall two-stage spire. STUPA and CHORTEN archetypes: plinth, white dome and a gilt spire. All need
    /// a convex footprint, else flat.</item>
    /// </list>
    /// Normals are flat (every face has its own vertices); colours come from <see cref="BuildingStyle"/>, varied by
    /// seed. Positions are relative to the tile's south-west corner (draw buildings only for exact nodes). Skips,
    /// documented by <see cref="IsDrawn"/>: landmarks when <see cref="BuildingOptions.SkipLandmarks"/>, outer rings
    /// with fewer than 3 distinct points or under <see cref="BuildingOptions.MinAreaM2"/>. Appends to
    /// <see cref="MeshData"/>; returns the number of buildings drawn. Thread-safe for distinct meshes.
    /// </summary>
    public static class BuildingMesher
    {
        private enum Gen : byte
        {
            Flat,
            Gabled,
            Hipped,
            Skillion,
            Pyramid,
            Dome,
            Pagoda,
            Shikhara,
            Stupa,
        }

        private struct P3
        {
            public double X, Y, Z;

            public P3(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        private sealed class Scratch
        {
            public double[] X = new double[64], Z = new double[64];
            public int[] Tris = new int[192], Next = new int[64], Prev = new int[64];

            public void Ensure(int n)
            {
                if (X.Length >= n) return;
                int cap = Math.Max(n, X.Length * 2);
                X = new double[cap];
                Z = new double[cap];
                Tris = new int[cap * 3];
                Next = new int[cap];
                Prev = new int[cap];
            }
        }

        [ThreadStatic] private static Scratch _scratch;

        public static int Build(TileData t, IHeightSampler h, BuildingOptions o, MeshData m)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (o == null) o = new BuildingOptions();
            switch (o.Band)
            {
                case BuildingBand.B0KitLite: return BuildingDetailMesher.BuildTile(t, h, o, m, null);
                case BuildingBand.B2Prism: return BuildingBands.Prisms(t, h, o, m);
                case BuildingBand.B3Block: return BuildingBands.Blocks(t, h, o, m);
            }
            Scratch s = _scratch ?? (_scratch = new Scratch());
            var ground = new Ground(t, h);
            BuildingFootprints guard = BuildingFootprints.For(t, o.CorridorsFor(t));
            int drawn = 0;
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                if (guard.Dropped(i)) continue;
                BuildingRecord b = guard.Record(i);
                if (o.HiddenRefs != null && o.HiddenRefs.Contains(b.OsmRef)) continue;
                if (o.Styled)
                {
                    // Generic temples, stupas and shrines use their generator's LOD1 in B1 (W2_DESIGN 3.2 LOD table), so
                    // tiers and finish agree with B0; their building:parts only give the heights.
                    if (SacredSelector.HostOf(t, i) >= 0) continue;
                    if (SacredSelector.DrawsGeneric(b))
                    {
                        int vs = m.VertexCount;
                        if (Ring(b, o, s) && SacredSelector.BuildGeneric(t, i, h, 1, m, null))
                        {
                            KitPaint.FillUnset(m, vs); // a generator without UV0 on a painted mesh: Plain and open
                            drawn++;
                        }
                        continue;
                    }
                    if ((b.Flags & BuildingFlags.HasParts) != 0) continue; // its parts are drawn instead (DATA_FORMATS 1.6)
                }
                HousePlan plan = o.Styled ? guard.Adjust(i, BuildingGrammar.Plan(t, i)) : default(HousePlan);
                int v0 = m.VertexCount;
                if (One(b, ref ground, o, s, m, o.Styled, plan))
                {
                    drawn++;
                    if (o.Styled) BuildingBandTable.PaintFar(m, v0, plan, o.SinkM);
                }
            }
            return drawn;
        }

        /// <summary>One building as a B1 styled extrusion (the B0 fallback for archetypes without a facade grammar).
        /// Returns false when skipped.</summary>
        internal static bool Styled(TileData t, int index, IHeightSampler h, BuildingOptions o, in HousePlan plan, MeshData m)
        {
            return Styled(t, t.Buildings[index], h, o, plan, m);
        }

        /// <summary>One record (possibly trimmed out of a road) as a B1 styled extrusion, painted with channels and AO.</summary>
        internal static bool Styled(TileData t, BuildingRecord b, IHeightSampler h, BuildingOptions o, in HousePlan plan, MeshData m)
        {
            Scratch s = _scratch ?? (_scratch = new Scratch());
            var ground = new Ground(t, h);
            int v0 = m.VertexCount;
            if (!One(b, ref ground, o ?? new BuildingOptions(), s, m, true, plan)) return false;
            BuildingBandTable.PaintFar(m, v0, plan, (o ?? new BuildingOptions()).SinkM);
            return true;
        }

        private static RoofShape ShapeOf(PlanRoof r)
        {
            switch (r)
            {
                case PlanRoof.Gable: return RoofShape.Gabled;
                case PlanRoof.Hip: return RoofShape.Hipped;
                case PlanRoof.Skillion: return RoofShape.Skillion;
                case PlanRoof.Pyramid: return RoofShape.Pyramidal;
                case PlanRoof.Dome: return RoofShape.Dome;
                case PlanRoof.Pagoda: return RoofShape.Pagoda;
                case PlanRoof.Shikhara: return RoofShape.Shikhara;
                default: return RoofShape.Flat;
            }
        }

        /// <summary>True when <see cref="Build"/> draws the building judged from its record alone (see the class
        /// remarks for the skips); the styled band also draws a generic sacred outline with parts and skips its parts,
        /// which <see cref="IsDrawn(TileData, int, BuildingOptions)"/> accounts for.</summary>
        public static bool IsDrawn(BuildingRecord b, BuildingOptions o)
        {
            if (o == null) o = new BuildingOptions();
            if (o.Styled && (b.Flags & BuildingFlags.HasParts) != 0) return false;
            return Ring(b, o, _scratch ?? (_scratch = new Scratch()));
        }

        /// <summary>True when <see cref="Build"/> draws building <paramref name="index"/> of a tile (hidden refs, generic
        /// sacred outlines with parts and the parts they draw, and houses dropped by the road guard included).</summary>
        public static bool IsDrawn(TileData t, int index, BuildingOptions o)
        {
            if (o == null) o = new BuildingOptions();
            BuildingFootprints guard = BuildingFootprints.For(t, o.CorridorsFor(t));
            if (guard.Dropped(index)) return false;
            BuildingRecord b = guard.Record(index);
            if (o.HiddenRefs != null && o.HiddenRefs.Contains(b.OsmRef)) return false;
            if (o.Styled && SacredSelector.HostOf(t, index) >= 0) return false;
            if (o.Styled && SacredSelector.DrawsGeneric(b)) return Ring(b, o, _scratch ?? (_scratch = new Scratch()));
            return IsDrawn(b, o);
        }

        /// <summary>Landmark skip and outer-ring validity (3 distinct points, at least <see cref="BuildingOptions.MinAreaM2"/>).</summary>
        private static bool Ring(BuildingRecord b, BuildingOptions o, Scratch s)
        {
            if (o.SkipLandmarks && (b.Flags & BuildingFlags.Landmark) != 0) return false;
            int n = LoadRing(b.Rings[0], s, 0, 0);
            if (n < 3) return false;
            return Math.Abs(Polygon.SignedArea(s.X, s.Z, n)) >= o.MinAreaM2;
        }

        /// <summary>Copy a ring into the scratch arrays in metres relative to (ox, oz) (tile-local metres), dropping
        /// repeated consecutive points and a closing duplicate. Returns the distinct point count.</summary>
        private static int LoadRing(int[] ring, Scratch s, double ox, double oz)
        {
            int count = ring.Length / 2;
            s.Ensure(count + 1);
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                int xc = ring[2 * i], zc = ring[2 * i + 1];
                if (n > 0 && s.X[n - 1] == xc / 100.0 - ox && s.Z[n - 1] == zc / 100.0 - oz) continue;
                s.X[n] = xc / 100.0 - ox;
                s.Z[n] = zc / 100.0 - oz;
                n++;
            }
            while (n > 1 && s.X[n - 1] == s.X[0] && s.Z[n - 1] == s.Z[0]) n--;
            return n;
        }

        private static void Reverse(double[] x, double[] z, int n)
        {
            for (int i = 0, j = n - 1; i < j; i++, j--)
            {
                double tx = x[i], tz = z[i];
                x[i] = x[j];
                z[i] = z[j];
                x[j] = tx;
                z[j] = tz;
            }
        }

        private static bool One(BuildingRecord b, ref Ground g, BuildingOptions o, Scratch s, MeshData m, bool styled, in HousePlan plan)
        {
            if (o.SkipLandmarks && (b.Flags & BuildingFlags.Landmark) != 0) return false;
            int n = LoadRing(b.Rings[0], s, 0, 0);
            if (n < 3) return false;
            double area = Polygon.SignedArea(s.X, s.Z, n);
            if (Math.Abs(area) < o.MinAreaM2) return false;
            if (area < 0)
            {
                Reverse(s.X, s.Z, n);
                area = -area;
            }
            double[] x = s.X, z = s.Z;

            double groundY = double.MaxValue;
            for (int i = 0; i < n; i++) groundY = Math.Min(groundY, g.At(x[i], z[i]));
            double cx, cz;
            Polygon.Centroid(x, z, n, out cx, out cz);
            groundY = Math.Min(groundY, g.At(cx, cz));

            float total = styled ? plan.TotalM : BuildingStyle.HeightM(b);
            double minH = b.MinHeightCm / 100.0;
            if (minH > total - 1.0) minH = Math.Max(0.0, total - 1.0);
            double bottom = minH > 0 ? groundY + minH : groundY - o.SinkM;
            double top = groundY + total;

            RoofShape shape = styled && BuildingGrammar.IsHouse(plan.Archetype) ? ShapeOf(plan.Roof) : BuildingStyle.ResolvedShape(b);
            bool convex = Polygon.IsConvex(x, z, n);
            Gen gen = Resolve(b, shape, n, convex);
            uint wall = styled ? plan.Wall : BuildingStyle.WallRgba(b);
            uint roof = styled ? plan.RoofColour : BuildingStyle.RoofRgba(b, gen == Gen.Flat ? RoofShape.Flat : shape);
            double meanR = 0;
            for (int i = 0; i < n; i++) meanR += Math.Sqrt((x[i] - cx) * (x[i] - cx) + (z[i] - cz) * (z[i] - cz));
            meanR /= n;
            double floorTop = groundY + minH + 1.0; // eaves never go below one metre of wall

            switch (gen)
            {
                case Gen.Flat:
                {
                    // Styled B1 is a far band: a flat deck on the wall tops, without the inner parapet faces (B0 draws them).
                    double parapet = o.Parapets && !styled ? Math.Min(BuildingStyle.RoofAllowanceM(RoofShape.Flat), 0.2 * (top - groundY)) : 0;
                    Walls(m, x, z, n, bottom, top, wall, false);
                    double deck = top - parapet;
                    if (parapet > 0) Walls(m, x, z, n, deck, top, wall, true);
                    Deck(m, s, n, convex, deck, roof);
                    break;
                }
                case Gen.Gabled:
                case Gen.Hipped:
                case Gen.Skillion:
                {
                    double allowance = BuildingStyle.RoofAllowanceM(gen == Gen.Skillion ? RoofShape.Skillion : RoofShape.Gabled);
                    double eaves = Math.Max(top - allowance, floorTop);
                    Walls(m, x, z, n, bottom, eaves, wall, false);
                    QuadRoof(m, x, z, gen, eaves, allowance, wall, roof);
                    break;
                }
                case Gen.Pyramid:
                {
                    double rh = Math.Min(BuildingStyle.RoofAllowanceM(RoofShape.Pyramidal), Math.Max(0.3, meanR));
                    double eaves = Math.Max(top - BuildingStyle.RoofAllowanceM(RoofShape.Pyramidal), floorTop);
                    Walls(m, x, z, n, bottom, eaves, wall, false);
                    Loft(m, x, z, n, cx, cz, eaves, 1.0, eaves + rh, 0.0, roof);
                    break;
                }
                case Gen.Dome:
                {
                    double dh = Math.Min(Math.Max(BuildingStyle.RoofAllowanceM(RoofShape.Dome), 0.6 * meanR), 0.5 * (top - groundY));
                    double eaves = Math.Max(top - dh, floorTop);
                    Walls(m, x, z, n, bottom, eaves, wall, false);
                    Dome(m, x, z, n, cx, cz, eaves, 1.0, dh, roof);
                    break;
                }
                case Gen.Pagoda:
                    Pagoda(m, s, n, convex, cx, cz, bottom, groundY, top, floorTop, wall, roof);
                    break;
                case Gen.Shikhara:
                {
                    double eaves = Math.Max(groundY + 0.3 * (top - groundY), floorTop);
                    Walls(m, x, z, n, bottom, eaves, wall, false);
                    double mid = eaves + 0.6 * (top - eaves);
                    Loft(m, x, z, n, cx, cz, eaves, 1.0, mid, 0.62, roof);
                    Loft(m, x, z, n, cx, cz, mid, 0.62, top, 0.0, roof);
                    break;
                }
                case Gen.Stupa:
                    Stupa(m, s, n, convex, cx, cz, meanR, bottom, groundY, top, wall, roof);
                    break;
            }

            if (styled && o.FrontDetail && BuildingGrammar.IsHouse(plan.Archetype)) StyledFront(b, plan, s, n, groundY, m);

            // Courtyard walls of the holes, facing into the courtyard; W1 roofs do not cut the holes out.
            double holeTop = gen == Gen.Flat ? top : Math.Max(top - BuildingStyle.RoofAllowanceM(shape), floorTop);
            for (int r = 1; r < b.Rings.Length; r++)
            {
                int hn = LoadRing(b.Rings[r], s, 0, 0);
                if (hn < 3) continue;
                double ha = Polygon.SignedArea(s.X, s.Z, hn);
                if (Math.Abs(ha) < 0.25) continue;
                if (ha > 0) Reverse(s.X, s.Z, hn); // holes are clockwise, so the walls face into the courtyard
                Walls(m, s.X, s.Z, hn, bottom, holeTop, wall, false);
            }
            return true;
        }

        /// <summary>
        /// B1 styling of the street front (W2_DESIGN 2.4): the front paint over the front edge (and a corner house's
        /// second edge), a floor band at the G/1 line and one darker window-row quad per storey (2 triangles each).
        /// The scratch ring must still hold the outer ring (counter-clockwise).
        /// </summary>
        private static void StyledFront(BuildingRecord b, in HousePlan plan, Scratch s, int n, double groundY, MeshData m)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                int e = pass == 0 ? plan.FrontEdge : plan.SecondEdge;
                if (e < 0 && pass == 1) continue;
                int i = EdgeIndex(b.Rings[0], e, s, n);
                if (i < 0) continue;
                int j = i + 1 == n ? 0 : i + 1;
                double dx = s.X[j] - s.X[i], dz = s.Z[j] - s.Z[i], len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 2.0) continue;
                var f = new KitFrame(s.X[i], groundY, s.Z[i], dx, dz);
                double top = plan.WallTopM;
                if (plan.Front != plan.Wall) MeshKit.Panel(m, f, 0, 0, len, top, 0.01, plan.Front);
                uint band = MeshColor.Scale(plan.Front, 0.75f), window = MeshColor.Scale(plan.Front, 0.42f);
                if (plan.Storeys >= 2) MeshKit.Panel(m, f, 0, plan.FloorBase(1) - 0.12, len, plan.FloorBase(1) + 0.12, 0.02, band);
                for (int k = 0; k < plan.Storeys; k++)
                {
                    double v0 = plan.FloorBase(k), v1 = k + 1 < plan.Storeys ? plan.FloorBase(k + 1) : top;
                    if (v1 - v0 < 1.6) continue;
                    bool shop = k == 0 && plan.ShopGround;
                    double a = shop ? v0 + 0.05 : v0 + 0.75, c = shop ? v0 + Math.Min(2.5, v1 - v0 - 0.3) : Math.Min(v1 - 0.35, v0 + 2.0);
                    MeshKit.Panel(m, f, 0.1 * len, a, 0.9 * len, c, 0.03, shop ? MeshColor.FromHex(0x8C949C) : window);
                }
            }
        }

        /// <summary>The scratch-ring index of ring-0 edge <paramref name="e"/> (matched by its start point), else the
        /// longest edge (for e = -1, the front fallback).</summary>
        private static int EdgeIndex(int[] ring, int e, Scratch s, int n)
        {
            if (e >= 0 && e < ring.Length / 2)
            {
                double x = ring[2 * e] / 100.0, z = ring[2 * e + 1] / 100.0;
                for (int i = 0; i < n; i++)
                    if (s.X[i] == x && s.Z[i] == z) return i;
                return -1;
            }
            int best = -1;
            double bl = -1;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                double l = (s.X[j] - s.X[i]) * (s.X[j] - s.X[i]) + (s.Z[j] - s.Z[i]) * (s.Z[j] - s.Z[i]);
                if (l > bl)
                {
                    bl = l;
                    best = i;
                }
            }
            return best;
        }

        private static Gen Resolve(BuildingRecord b, RoofShape shape, int n, bool convex)
        {
            if (b.Archetype == BuildingArchetype.Stupa || b.Archetype == BuildingArchetype.Chorten)
                return convex ? Gen.Stupa : Gen.Flat;
            switch (shape)
            {
                case RoofShape.Gabled:
                case RoofShape.Round:
                    return n == 4 && convex ? Gen.Gabled : convex ? Gen.Pyramid : Gen.Flat;
                case RoofShape.Hipped:
                case RoofShape.HalfHipped:
                case RoofShape.Gambrel:
                case RoofShape.Mansard:
                    return n == 4 && convex ? Gen.Hipped : convex ? Gen.Pyramid : Gen.Flat;
                case RoofShape.Skillion:
                    return n == 4 && convex ? Gen.Skillion : Gen.Flat;
                case RoofShape.Pyramidal:
                case RoofShape.Cone:
                    return convex ? Gen.Pyramid : Gen.Flat;
                case RoofShape.Dome:
                case RoofShape.Onion:
                    return convex ? Gen.Dome : Gen.Flat;
                case RoofShape.Pagoda:
                    return convex ? Gen.Pagoda : Gen.Flat;
                case RoofShape.Shikhara:
                    return convex ? Gen.Shikhara : Gen.Flat;
                default:
                    return Gen.Flat;
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Faces
        // ---------------------------------------------------------------------------------------------------

        /// <summary>Vertical walls along a ring from y0 to y1, one flat quad per edge, facing to the right of the
        /// ring's direction (outward for a counter-clockwise ring); <paramref name="inward"/> faces the other way.</summary>
        private static void Walls(MeshData m, double[] x, double[] z, int n, double y0, double y1, uint c, bool inward)
        {
            if (!(y1 > y0)) return;
            m.Reserve(4 * n, 6 * n);
            float fy0 = (float)y0, fy1 = (float)y1;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                double ax = x[i], az = z[i], bx = x[j], bz = z[j];
                if (inward)
                {
                    ax = x[j];
                    az = z[j];
                    bx = x[i];
                    bz = z[i];
                }
                double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-4) continue;
                float nx = (float)(dz / len), nz = (float)(-dx / len);
                int v = m.AddVertex((float)ax, fy0, (float)az, nx, 0f, nz, c);
                m.AddVertex((float)ax, fy1, (float)az, nx, 0f, nz, c);
                m.AddVertex((float)bx, fy1, (float)bz, nx, 0f, nz, c);
                m.AddVertex((float)bx, fy0, (float)bz, nx, 0f, nz, c);
                m.AddTriangle(v, v + 1, v + 2);
                m.AddTriangle(v, v + 2, v + 3);
            }
        }

        /// <summary>A horizontal, upward-facing deck over the scratch ring at height y. A fan is used only for
        /// strictly convex rings (the 3 degree tolerance that picks roof shapes would let a fan fold); otherwise the
        /// ring is ear-clipped.</summary>
        private static void Deck(MeshData m, Scratch s, int n, bool convex, double y, uint c)
        {
            bool fan = convex && Polygon.IsConvex(s.X, s.Z, n, 0.0);
            int tris = Polygon.Triangulate(s.X, s.Z, n, s.Tris, s.Next, s.Prev, fan);
            if (tris == 0) return;
            m.Reserve(n, tris * 3);
            int v0 = m.VertexCount;
            float fy = (float)y;
            for (int i = 0; i < n; i++) m.AddVertex((float)s.X[i], fy, (float)s.Z[i], 0f, 1f, 0f, c);
            for (int t = 0; t < tris; t++)
            {
                int ia = s.Tris[3 * t], ib = s.Tris[3 * t + 1], ic = s.Tris[3 * t + 2];
                // Collinear ring points give slivers thinner than a millimetre (zero-area fan triangles, or ones
                // whose winding float rounding could flip): drop them, they cover nothing visible.
                double cr = (s.X[ib] - s.X[ia]) * (s.Z[ic] - s.Z[ia]) - (s.Z[ib] - s.Z[ia]) * (s.X[ic] - s.X[ia]);
                double e = Math.Max(Dist2(s, ia, ib), Math.Max(Dist2(s, ib, ic), Dist2(s, ic, ia)));
                if (cr <= 0 || cr * cr < 1e-6 * e) continue;
                // Counter-clockwise from above -> reversed for Unity's front face pointing up.
                m.AddTriangle(v0 + ia, v0 + ic, v0 + ib);
            }
        }

        private static void Tri(MeshData m, P3 a, P3 b, P3 c, double hx, double hy, double hz, uint col)
        {
            double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 1e-9) return;
            if (nx * hx + ny * hy + nz * hz < 0)
            {
                P3 t = b;
                b = c;
                c = t;
                nx = -nx;
                ny = -ny;
                nz = -nz;
            }
            float fx = (float)(nx / len), fy = (float)(ny / len), fz = (float)(nz / len);
            int v = m.AddVertex((float)a.X, (float)a.Y, (float)a.Z, fx, fy, fz, col);
            m.AddVertex((float)b.X, (float)b.Y, (float)b.Z, fx, fy, fz, col);
            m.AddVertex((float)c.X, (float)c.Y, (float)c.Z, fx, fy, fz, col);
            m.AddTriangle(v, v + 1, v + 2);
        }

        /// <summary>A flat-shaded quad (a, b, c, d around its perimeter), facing the side of the hint vector. A
        /// twisted quad (its halves more than about 6 degrees apart, e.g. a gable roof over a skewed footprint) is
        /// emitted as two triangles with their own normals.</summary>
        private static void Quad(MeshData m, P3 a, P3 b, P3 c, P3 d, double hx, double hy, double hz, uint col)
        {
            double n1x, n1y, n1z, n2x, n2y, n2z;
            Cross(a, b, c, out n1x, out n1y, out n1z);
            Cross(a, c, d, out n2x, out n2y, out n2z);
            double l1 = Math.Sqrt(n1x * n1x + n1y * n1y + n1z * n1z), l2 = Math.Sqrt(n2x * n2x + n2y * n2y + n2z * n2z);
            if (l1 > 1e-9 && l2 > 1e-9 && (n1x * n2x + n1y * n2y + n1z * n2z) / (l1 * l2) < 0.995)
            {
                Tri(m, a, b, c, hx, hy, hz, col);
                Tri(m, a, c, d, hx, hy, hz, col);
                return;
            }
            // Newell normal of a -> b -> c -> d.
            double nx = (a.Y - b.Y) * (a.Z + b.Z) + (b.Y - c.Y) * (b.Z + c.Z) + (c.Y - d.Y) * (c.Z + d.Z) + (d.Y - a.Y) * (d.Z + a.Z);
            double ny = (a.Z - b.Z) * (a.X + b.X) + (b.Z - c.Z) * (b.X + c.X) + (c.Z - d.Z) * (c.X + d.X) + (d.Z - a.Z) * (d.X + a.X);
            double nz = (a.X - b.X) * (a.Y + b.Y) + (b.X - c.X) * (b.Y + c.Y) + (c.X - d.X) * (c.Y + d.Y) + (d.X - a.X) * (d.Y + a.Y);
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 1e-9)
            {
                Tri(m, a, b, c, hx, hy, hz, col);
                Tri(m, a, c, d, hx, hy, hz, col);
                return;
            }
            if (nx * hx + ny * hy + nz * hz < 0)
            {
                P3 t = b;
                b = d;
                d = t;
                nx = -nx;
                ny = -ny;
                nz = -nz;
            }
            float fx = (float)(nx / len), fy = (float)(ny / len), fz = (float)(nz / len);
            int v = m.AddVertex((float)a.X, (float)a.Y, (float)a.Z, fx, fy, fz, col);
            m.AddVertex((float)b.X, (float)b.Y, (float)b.Z, fx, fy, fz, col);
            m.AddVertex((float)c.X, (float)c.Y, (float)c.Z, fx, fy, fz, col);
            m.AddVertex((float)d.X, (float)d.Y, (float)d.Z, fx, fy, fz, col);
            m.AddTriangle(v, v + 1, v + 2);
            m.AddTriangle(v, v + 2, v + 3);
        }

        private static double Dist2(Scratch s, int i, int j)
        {
            double dx = s.X[j] - s.X[i], dz = s.Z[j] - s.Z[i];
            return dx * dx + dz * dz;
        }

        private static void Cross(P3 a, P3 b, P3 c, out double nx, out double ny, out double nz)
        {
            double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
            nx = uy * vz - uz * vy;
            ny = uz * vx - ux * vz;
            nz = ux * vy - uy * vx;
        }

        // ---------------------------------------------------------------------------------------------------
        // Roofs
        // ---------------------------------------------------------------------------------------------------

        /// <summary>Gabled, hipped or skillion roof over a convex quad. The eave edges are the longer pair of opposite
        /// edges; the ridge joins the midpoints of the other two (the gable ends).</summary>
        private static void QuadRoof(MeshData m, double[] x, double[] z, Gen gen, double eaves, double allowance, uint wall, uint roof)
        {
            double e0 = Dist(x, z, 0, 1), e1 = Dist(x, z, 1, 2), e2 = Dist(x, z, 2, 3), e3 = Dist(x, z, 3, 0);
            int o = e0 + e2 >= e1 + e3 ? 0 : 1;
            int ia = o, ib = (o + 1) & 3, ic = (o + 2) & 3, id = (o + 3) & 3;
            // Eaves along a-b and c-d; gable ends b-c and d-a.
            double span = 0.5 * (Dist(x, z, ib, ic) + Dist(x, z, id, ia));
            double rh = Math.Max(0.3, Math.Min(allowance, gen == Gen.Skillion ? span : 0.5 * span));
            var a = new P3(x[ia], eaves, z[ia]);
            var b = new P3(x[ib], eaves, z[ib]);
            var c = new P3(x[ic], eaves, z[ic]);
            var d = new P3(x[id], eaves, z[id]);
            double cx = 0.25 * (a.X + b.X + c.X + d.X), cz = 0.25 * (a.Z + b.Z + c.Z + d.Z);

            if (gen == Gen.Skillion)
            {
                // a-b low, c-d high.
                var c2 = new P3(c.X, eaves + rh, c.Z);
                var d2 = new P3(d.X, eaves + rh, d.Z);
                Quad(m, a, b, c2, d2, 0, 1, 0, roof);
                Quad(m, c, d, d2, c2, c.X + d.X - 2 * cx, 0, c.Z + d.Z - 2 * cz, wall);
                Tri(m, b, c, c2, b.X + c.X - 2 * cx, 0, b.Z + c.Z - 2 * cz, wall);
                Tri(m, d, a, d2, d.X + a.X - 2 * cx, 0, d.Z + a.Z - 2 * cz, wall);
                return;
            }

            var r1 = new P3(0.5 * (b.X + c.X), eaves + rh, 0.5 * (b.Z + c.Z)); // over gable end b-c
            var r0 = new P3(0.5 * (d.X + a.X), eaves + rh, 0.5 * (d.Z + a.Z)); // over gable end d-a
            if (gen == Gen.Hipped)
            {
                double rx = r1.X - r0.X, rz = r1.Z - r0.Z, rl = Math.Sqrt(rx * rx + rz * rz);
                double inset = Math.Min(0.5 * span, 0.5 * rl);
                if (rl > 1e-6)
                {
                    r0 = new P3(r0.X + rx / rl * inset, r0.Y, r0.Z + rz / rl * inset);
                    r1 = new P3(r1.X - rx / rl * inset, r1.Y, r1.Z - rz / rl * inset);
                }
            }
            Quad(m, a, b, r1, r0, (a.X + b.X) * 0.5 - cx, 1, (a.Z + b.Z) * 0.5 - cz, roof);
            Quad(m, c, d, r0, r1, (c.X + d.X) * 0.5 - cx, 1, (c.Z + d.Z) * 0.5 - cz, roof);
            uint endColour = gen == Gen.Hipped ? roof : wall;
            double ehy = gen == Gen.Hipped ? 1 : 0;
            Tri(m, b, c, r1, (b.X + c.X) * 0.5 - cx, ehy, (b.Z + c.Z) * 0.5 - cz, endColour);
            Tri(m, d, a, r0, (d.X + a.X) * 0.5 - cx, ehy, (d.Z + a.Z) * 0.5 - cz, endColour);
        }

        private static double Dist(double[] x, double[] z, int i, int j)
        {
            double dx = x[j] - x[i], dz = z[j] - z[i];
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>One frustum band of a convex ring scaled about (cx, cz): from scale s0 at y0 up to scale s1 at
        /// y1 (s1 = 0 closes it to an apex). Faces point away from the axis.</summary>
        private static void Loft(MeshData m, double[] x, double[] z, int n, double cx, double cz,
                                 double y0, double s0, double y1, double s1, uint col)
        {
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                var a0 = new P3(cx + (x[i] - cx) * s0, y0, cz + (z[i] - cz) * s0);
                var b0 = new P3(cx + (x[j] - cx) * s0, y0, cz + (z[j] - cz) * s0);
                double hx = 0.5 * (x[i] + x[j]) - cx, hz = 0.5 * (z[i] + z[j]) - cz;
                double hy = s1 < s0 ? 1e-3 : 0; // tie-break toward up for sloped bands
                if (s1 <= 0)
                {
                    Tri(m, a0, b0, new P3(cx, y1, cz), hx, hy, hz, col);
                    continue;
                }
                var a1 = new P3(cx + (x[i] - cx) * s1, y1, cz + (z[i] - cz) * s1);
                var b1 = new P3(cx + (x[j] - cx) * s1, y1, cz + (z[j] - cz) * s1);
                Quad(m, a0, b0, b1, a1, hx, hy, hz, col);
            }
        }

        /// <summary>A faceted dome of height dh on the ring scaled by s about (cx, cz), from y0.</summary>
        private static void Dome(MeshData m, double[] x, double[] z, int n, double cx, double cz, double y0, double s, double dh, uint col)
        {
            const int Bands = 4;
            for (int k = 0; k < Bands; k++)
            {
                double a0 = k * Math.PI / (2 * Bands), a1 = (k + 1) * Math.PI / (2 * Bands);
                double sc1 = k + 1 == Bands ? 0 : Math.Cos(a1);
                Loft(m, x, z, n, cx, cz, y0 + dh * Math.Sin(a0), s * Math.Cos(a0), y0 + dh * Math.Sin(a1), s * sc1, col);
            }
        }

        /// <summary>Pagoda placeholder (unstyled W1 extrusion and stray parts; styled bands draw temples with the sacred
        /// generators): sanctum walls to 35 % of the height, then 1 or 2 tile tiers (by height; 3 tiers and gilt come only
        /// from a curated record, W2_DESIGN 3.2), each an overhanging sloped roof band with a short wall above it, the
        /// last closing to the apex.</summary>
        private static void Pagoda(MeshData m, Scratch s, int n, bool convex, double cx, double cz, double bottom, double groundY,
                                   double top, double floorTop, uint wall, uint roof)
        {
            double[] x = s.X, z = s.Z;
            double h = top - groundY;
            double y = Math.Max(groundY + 0.35 * h, floorTop);
            Walls(m, x, z, n, bottom, y, wall, false);
            int tiers = h < 9 ? 1 : 2;
            double th = (top - y) / tiers;
            double scale = 1.0;
            for (int k = 0; k < tiers; k++)
            {
                double eave = scale * 1.25, next = scale * 0.72;
                // Underside of the overhang, facing down.
                for (int i = 0; i < n; i++)
                {
                    int j = i + 1 == n ? 0 : i + 1;
                    Quad(m, new P3(cx + (x[i] - cx) * scale, y, cz + (z[i] - cz) * scale),
                         new P3(cx + (x[j] - cx) * scale, y, cz + (z[j] - cz) * scale),
                         new P3(cx + (x[j] - cx) * eave, y, cz + (z[j] - cz) * eave),
                         new P3(cx + (x[i] - cx) * eave, y, cz + (z[i] - cz) * eave), 0, -1, 0, roof);
                }
                if (k == tiers - 1)
                {
                    Loft(m, x, z, n, cx, cz, y, eave, top, 0.0, roof);
                    break;
                }
                double yRoof = y + 0.6 * th;
                Loft(m, x, z, n, cx, cz, y, eave, yRoof, next, roof);
                Loft(m, x, z, n, cx, cz, yRoof, next, y + th, next, wall);
                y += th;
                scale = next;
            }
        }

        /// <summary>Stupa placeholder: a stepped white plinth (15 % of the height), a white dome (45 %) and a gilt
        /// spire to the top.</summary>
        private static void Stupa(MeshData m, Scratch s, int n, bool convex, double cx, double cz, double meanR, double bottom,
                                  double groundY, double top, uint wall, uint roof)
        {
            double[] x = s.X, z = s.Z;
            double h = top - groundY;
            double plinth = groundY + Math.Max(0.15 * h, 0.5);
            Walls(m, x, z, n, bottom, plinth, wall, false);
            Deck(m, s, n, convex, plinth, wall);
            double domeH = Math.Min(0.45 * h, 1.2 * meanR);
            Dome(m, x, z, n, cx, cz, plinth, 0.85, domeH, BuildingStyle.Whitewash);
            double spireBase = plinth + domeH * 0.92;
            double r = Math.Max(0.12 * meanR, 0.3);
            // A square gilt spire on the dome's crown.
            double[] sx = { cx - r, cx + r, cx + r, cx - r }, sz = { cz - r, cz - r, cz + r, cz + r };
            Loft(m, sx, sz, 4, cx, cz, spireBase, 1.0, top, 0.0, BuildingStyle.Gold);
        }

        // ---------------------------------------------------------------------------------------------------
        // Ground
        // ---------------------------------------------------------------------------------------------------

        /// <summary>Terrain height lookups in tile-local metres, clamped onto the tile for overhanging footprints.</summary>
        private struct Ground
        {
            private readonly IHeightSampler _h;
            private readonly TileHeightSampler _ths;
            private readonly double _x0, _z0, _size;
            private double _last;

            public Ground(TileData t, IHeightSampler h)
            {
                _h = h;
                _ths = h as TileHeightSampler;
                _x0 = t.Tile.X0;
                _z0 = t.Tile.Z0;
                _size = t.Tile.Size;
                _last = 0;
            }

            public double At(double lx, double lz)
            {
                float v;
                if (_h.TryHeight(_x0 + lx, _z0 + lz, out v)) return _last = v;
                if (_ths != null)
                {
                    if (_ths.TryHeightClamped(_x0 + lx, _z0 + lz, out v)) return _last = v;
                }
                else
                {
                    double cx = lx < 0 ? 0 : lx > _size ? _size : lx, cz = lz < 0 ? 0 : lz > _size ? _size : lz;
                    if (_h.TryHeight(_x0 + cx, _z0 + cz, out v)) return _last = v;
                }
                return _last;
            }
        }
    }
}
