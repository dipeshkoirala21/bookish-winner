using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>Which generator draws a sacred structure.</summary>
    public enum SacredKind : byte
    {
        None = 0,
        Pagoda = 1,
        HouseTemple = 2,
        ShikharaStone = 3,
        ShikharaPlaster = 4,
        Stupa = 5,
        Chaitya = 6,
        Shrine = 7,
        Hiti = 8,
    }

    /// <summary>The selected generator's parameters and where it stands (only the member of <see cref="Kind"/> is set).</summary>
    public struct SacredParams
    {
        public SacredKind Kind;
        public GenFrame Frame;
        public PagodaParams Pagoda;
        public HouseTempleParams House;
        public ShikharaParams Shikhara;
        public StupaParams Stupa;
        public ChaityaParams Chaitya;
        public ShrineParams Shrine;
        public float HitiW, HitiD;
        public int HitiSpouts;
    }

    /// <summary>
    /// Generic (non-hero) sacred structures from OSM (W2_DESIGN 3.2 selection table): pagodas get 1 tier under 6 m, 2
    /// tiers from 14 m or for named Hindu mandirs and degas of 6-14 m, never 3 or more without a curated record; a
    /// Bhimsen name means a house-temple; shikharas are stone when the wall material says so, else plastered; stupas are
    /// S (under 8 m) or M (8-20 m); shrines take their kind from the name (Ganesh, Bhairav, linga, nag) and a form from
    /// their size; POIs without a footprint become shrines, chaityas (Buddhist stupa POIs) or hitis (stone taps), facing
    /// the nearest road. Procedural dressing never invents one where OSM has nothing (O2). Gilt and extra tiers come only
    /// from the curated DB, or (tiers) from the <c>building:part</c> records of an outline, which then give the heights
    /// and are not drawn themselves (<see cref="HostOf"/>). Deterministic.
    /// </summary>
    public static class SacredSelector
    {
        /// <summary>Select the generator for building <paramref name="buildingIndex"/>; false when it is not sacred.
        /// The frame's ground height is 0 (callers add the terrain).</summary>
        public static bool TrySelect(TileData t, int buildingIndex, out SacredKind kind, out SacredParams p)
        {
            p = default(SacredParams);
            kind = SacredKind.None;
            BuildingRecord b = t.Buildings[buildingIndex];
            if (!BuildingGrammar.IsSacred(b.Archetype)) return false;
            if ((b.Flags & BuildingFlags.Part) != 0) return false; // parts describe a temple drawn from its outline
            BuildingFronts fronts = BuildingFronts.For(t);
            BuildingFrontRecord front = fronts.Front(buildingIndex);
            int[] ring = b.Rings[0];
            double cx, cz, yaw, w, d;
            Frame(ring, front.HasFront ? front.FrontEdge : -1, out cx, out cz, out yaw, out w, out d);
            double longSide = Math.Max(w, d);
            string name = NameOf(t, b.NameRef);
            float height = (b.Flags & BuildingFlags.HeightTagged) != 0 ? (float)(b.HeightCm / 100.0) : 0f;
            if (height > 0 && height < 3) height = 0; // a tagged height under 3 m on a temple outline is its plinth (T 1.3)
            // building:part present (3.2, first row): the heights come from the parts, which are not drawn themselves.
            int[] parts = (b.Flags & BuildingFlags.HasParts) != 0 ? PartsOf(t, buildingIndex) : null;
            if (parts != null)
            {
                double partTop = 0;
                foreach (int j in parts) partTop = Math.Max(partTop, t.Buildings[j].HeightCm / 100.0);
                if (partTop >= 3) height = (float)partTop;
            }
            p.Frame = new GenFrame(cx, cz, 0f, (float)yaw);
            switch (b.Archetype)
            {
                case BuildingArchetype.TemplePagoda:
                {
                    if (Matches(name, "bhimsen"))
                    {
                        kind = SacredKind.HouseTemple;
                        p.House = new HouseTempleParams
                        {
                            W = (float)w, D = (float)d, Storeys = longSide >= 8 ? 3 : 2, PlinthLevels = 1, PlinthRiseM = 0.5f,
                            TotalHeightM = height > 0 ? height : (float)Math.Max(7, 1.1 * longSide), Lions = true,
                        };
                        break;
                    }
                    int tiers = longSide < 6 ? 1 : longSide < 14 ? (Matches(name, "mandir", "temple", "dega", "deval", "मन्दिर", "देगः") ? 2 : 1) : 2;
                    kind = SacredKind.Pagoda;
                    PagodaParams pp = PagodaParams.Defaults((float)w, (float)d, tiers);
                    pp.PlinthLevels = longSide < 6 ? 1 : 2;
                    pp.StepRiseM = longSide < 6 ? 0.3f : 0.45f;
                    pp.TotalHeightM = height > 0 ? height : (float)(tiers == 1 ? Math.Max(4.0, 0.85 * longSide) : Math.Max(7.0, 1.05 * longSide + 2));
                    pp.Doors = (byte)(Matches(name, "shiva", "mahadev", "shiv", "महादेव") ? 4 : 1);
                    pp.BellSpacingM = 0.5f;
                    if (parts != null) PagodaFromParts(t, parts, cx, cz, yaw, ref pp);
                    p.Pagoda = pp;
                    break;
                }
                case BuildingArchetype.TempleShikhara:
                {
                    bool stone = b.WallMaterial == WallMaterial.Stone;
                    kind = stone ? SacredKind.ShikharaStone : SacredKind.ShikharaPlaster;
                    ShikharaParams sp = stone ? ShikharaParams.StoneDefaults((float)w, (float)d) : ShikharaParams.PlasterDefaults((float)w, (float)d);
                    sp.TotalHeightM = height > 0 ? height : (float)Math.Max(5.0, (stone ? 1.4 : 1.9) * longSide);
                    if (longSide < 6)
                    {
                        sp.PavilionsStorey1 = 4;
                        sp.PavilionsStorey2 = 0;
                    }
                    p.Shikhara = sp;
                    break;
                }
                case BuildingArchetype.Stupa:
                case BuildingArchetype.Chorten:
                {
                    if (longSide < 3)
                    {
                        kind = SacredKind.Chaitya;
                        p.Chaitya = ChaityaParams.Defaults((float)Math.Max(0.8, Math.Min(3.0, 1.2 * longSide)));
                        break;
                    }
                    kind = SacredKind.Stupa;
                    StupaParams st = StupaParams.ForSize((float)longSide);
                    if (longSide > 20) st = StupaParams.Defaults((float)(0.5 * longSide), 1); // L is curated; keep generic ones medium
                    st.TotalHeightM = height > 0 ? height : (float)Math.Max(2.5, longSide < 8 ? 1.1 * longSide : 1.0 * longSide);
                    p.Stupa = st;
                    break;
                }
                case BuildingArchetype.Shrine:
                {
                    kind = SacredKind.Shrine;
                    p.Shrine = new ShrineParams
                    {
                        Kind = ShrineKindOf(name), LongSideM = (float)longSide, ShortSideM = (float)Math.Min(w, d),
                        Form = b.RoofMaterial == RoofMaterial.Metal ? ShrineForm.TinCanopy : longSide < 2.5 ? ShrineForm.Niche : longSide < 5.5 ? ShrineForm.MiniPagoda : ShrineForm.Niche,
                    };
                    break;
                }
            }
            p.Kind = kind;
            return kind != SacredKind.None;
        }

        /// <summary>Select the generator for POI <paramref name="poiIndex"/> without a footprint (W2_DESIGN 1.18): shrines by
        /// name, chaityas for Buddhist stupa points, hitis at stone taps; false otherwise.</summary>
        public static bool TrySelectPoi(TileData t, int poiIndex, out SacredKind kind, out SacredParams p)
        {
            p = default(SacredParams);
            kind = SacredKind.None;
            PoiRecord poi = t.Pois[poiIndex];
            string name = NameOf(t, poi.NameRef);
            double x = poi.XCm / 100.0, z = poi.ZCm / 100.0;
            double yaw = YawToNearestRoad(t, x, z);
            p.Frame = new GenFrame(x, z, 0f, (float)yaw);
            switch (poi.Kind)
            {
                case PoiKind.Shrine:
                case PoiKind.TempleHindu:
                    kind = SacredKind.Shrine;
                    p.Shrine = new ShrineParams { Kind = ShrineKindOf(name), LongSideM = 1.6f, ShortSideM = 1.3f, Form = ShrineForm.Niche };
                    break;
                case PoiKind.Stupa:
                case PoiKind.Chorten:
                    kind = SacredKind.Chaitya;
                    p.Chaitya = ChaityaParams.Defaults(1.8f);
                    break;
                case PoiKind.StoneTap:
                    kind = SacredKind.Hiti;
                    p.HitiW = 6f;
                    p.HitiD = 4f;
                    p.HitiSpouts = 1;
                    break;
            }
            p.Kind = kind;
            return kind != SacredKind.None;
        }

        /// <summary>Build the generic structure for a building at a LOD (ground from <paramref name="h"/>); false when the
        /// building is not sacred.</summary>
        public static bool BuildGeneric(TileData t, int buildingIndex, IHeightSampler h, int lod, MeshData m, GenColliders c)
        {
            SacredKind kind;
            SacredParams p;
            if (!TrySelect(t, buildingIndex, out kind, out p)) return false;
            var g = new RoadSurface(t, h);
            int[] ring = t.Buildings[buildingIndex].Rings[0];
            double ground = double.MaxValue;
            for (int k = 0; k < ring.Length / 2; k++) ground = Math.Min(ground, g.Height(ring[2 * k] / 100.0, ring[2 * k + 1] / 100.0));
            p.Frame.GroundY = (float)ground;
            // Hold the structure to its LOD ceiling (3.2 LOD table): a large or richly mapped one drops a LOD instead.
            bool small = kind == SacredKind.Shrine || kind == SacredKind.Chaitya || kind == SacredKind.Hiti;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            int boxes0 = c == null ? 0 : c.Boxes.Count, ramps0 = c == null ? 0 : c.Ramps.Count;
            for (int l = Math.Max(0, lod); ; l++)
            {
                Build(p, l, m, c);
                if (l >= 3 || (m.IndexCount - i0) / 3 <= MaxTris(small, l)) return true;
                m.VertexCount = v0;
                m.IndexCount = i0;
                if (c != null)
                {
                    c.Boxes.RemoveRange(boxes0, c.Boxes.Count - boxes0);
                    c.Ramps.RemoveRange(ramps0, c.Ramps.Count - ramps0);
                }
            }
        }

        /// <summary>Triangle ceiling of a generic sacred structure at a LOD (W2_DESIGN 3.2): temples, stupas and
        /// shikharas 6,000 / 3,000 / 800 / 200; shrines, chaityas and hitis 1,500 / 600 / 120 / 120.</summary>
        public static int MaxTris(bool small, int lod)
        {
            switch (lod)
            {
                case 0: return small ? 1500 : 6000;
                case 1: return small ? 600 : 3000;
                case 2: return small ? 120 : 800;
                default: return small ? 120 : 200;
            }
        }

        /// <summary>Run the selected generator.</summary>
        public static int Build(in SacredParams p, int lod, MeshData m, GenColliders c)
        {
            switch (p.Kind)
            {
                case SacredKind.Pagoda: return PagodaGenerator.Build(p.Pagoda, p.Frame, lod, m, c);
                case SacredKind.HouseTemple: return HeroForms.HouseTemple(p.House, p.Frame, lod, m, c, null);
                case SacredKind.ShikharaStone:
                case SacredKind.ShikharaPlaster: return ShikharaGenerator.Build(p.Shikhara, p.Frame, lod, m, c);
                case SacredKind.Stupa: return StupaGenerator.Build(p.Stupa, p.Frame, lod, m, c);
                case SacredKind.Chaitya: return ChaityaGenerator.Build(p.Chaitya, p.Frame, lod, m, c);
                case SacredKind.Shrine: return ShrineGenerator.Build(p.Shrine, p.Frame, lod, m, c);
                case SacredKind.Hiti: return HeroForms.Hiti(p.HitiW, p.HitiD, p.HitiSpouts, true, p.Frame, lod, m, c);
                default: return 0;
            }
        }

        /// <summary>True when the generic generators draw this record from its outline (a sacred archetype that is not a
        /// <c>building:part</c>); its parts, if any, are then not drawn (<see cref="HostOf"/>).</summary>
        public static bool DrawsGeneric(BuildingRecord b)
        {
            return BuildingGrammar.IsSacred(b.Archetype) && (b.Flags & BuildingFlags.Part) == 0;
        }

        /// <summary>For a <c>building:part</c> record, the index of the generic sacred outline (<see cref="DrawsGeneric"/>,
        /// HAS_PARTS) whose outer ring holds the part's centroid (DATA_FORMATS 1.6); -1 otherwise. Such parts are drawn by
        /// their host's generator and must not be drawn again. Cached per tile.</summary>
        public static int HostOf(TileData t, int buildingIndex)
        {
            if ((t.Buildings[buildingIndex].Flags & BuildingFlags.Part) == 0) return -1;
            PartIndex x = Index(t);
            return x.Host == null || buildingIndex >= x.Host.Length ? -1 : x.Host[buildingIndex];
        }

        /// <summary>The part indices of a generic sacred host (null when it has none).</summary>
        internal static int[] PartsOf(TileData t, int hostIndex)
        {
            PartIndex x = Index(t);
            return x.Parts == null || hostIndex >= x.Parts.Length ? null : x.Parts[hostIndex];
        }

        private sealed class PartIndex
        {
            public int[] Host;
            public int[][] Parts;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TileData, PartIndex> PartCache =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TileData, PartIndex>();

        private static PartIndex Index(TileData t)
        {
            return PartCache.GetValue(t, k =>
            {
                var x = new PartIndex();
                var hosts = new System.Collections.Generic.List<int>();
                for (int i = 0; i < k.Buildings.Count; i++)
                {
                    BuildingRecord b = k.Buildings[i];
                    if ((b.Flags & BuildingFlags.HasParts) != 0 && DrawsGeneric(b) && b.Rings[0].Length >= 6) hosts.Add(i);
                }
                if (hosts.Count == 0) return x;
                x.Host = new int[k.Buildings.Count];
                var lists = new System.Collections.Generic.List<int>[k.Buildings.Count];
                for (int j = 0; j < k.Buildings.Count; j++)
                {
                    x.Host[j] = -1;
                    BuildingRecord p = k.Buildings[j];
                    if ((p.Flags & BuildingFlags.Part) == 0 || p.Rings[0].Length < 2) continue;
                    int[] r = p.Rings[0];
                    double px = 0, pz = 0;
                    for (int q = 0; q < r.Length / 2; q++)
                    {
                        px += r[2 * q];
                        pz += r[2 * q + 1];
                    }
                    px /= r.Length / 2;
                    pz /= r.Length / 2;
                    foreach (int hi in hosts)
                    {
                        if (!Inside(k.Buildings[hi].Rings[0], px, pz)) continue;
                        x.Host[j] = hi;
                        if (lists[hi] == null) lists[hi] = new System.Collections.Generic.List<int>();
                        lists[hi].Add(j);
                        break;
                    }
                }
                x.Parts = new int[k.Buildings.Count][];
                foreach (int hi in hosts)
                    if (lists[hi] != null) x.Parts[hi] = lists[hi].ToArray();
                return x;
            });
        }

        /// <summary>Even-odd point-in-ring test in centimetres.</summary>
        private static bool Inside(int[] ring, double x, double z)
        {
            int n = ring.Length / 2;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = ring[2 * i], zi = ring[2 * i + 1], xj = ring[2 * j], zj = ring[2 * j + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// Tiers and heights of a pagoda mapped with <c>building:part</c> (W2_DESIGN 3.2, first selection row). Only parts
        /// at least 0.3 × the outline wide and deep, centred within 0.2 × its long side of its centre, count (posts,
        /// struts, porches and side shrines do not). Of those, parts standing on the ground (min_height under 0.3 m) or
        /// tagged flat that top out at most 0.45 × the tallest part are the plinth levels (one alone only up to 3 m): their count, widths
        /// and rise. Every other part above the plinth that is not tagged flat is a roof tier whose eave sits at its
        /// min_height (or, for a part holding its storey and roof, below its top by a 33° roof over its short side),
        /// merged when two eaves are within 0.5 m; from the top down a part no wider than the roof above is a storey body,
        /// not a roof; at most 5 tiers; the eave widths are the parts' widths across the
        /// door axis and the roof apex is the tallest part's top (the gajur goes above). The generator then adds struts,
        /// bells, gajur and plinth. The finish stays tile (gilt only from a curated record).
        /// </summary>
        private static void PagodaFromParts(TileData t, int[] parts, double cx, double cz, double yaw, ref PagodaParams pp)
        {
            int n = parts.Length;
            var top = new double[n];
            var min = new double[n];
            var pw = new double[n];
            var pd = new double[n];
            var use = new bool[n];
            double total = 0, hostLong = Math.Max(pp.PlinthW, pp.PlinthD);
            var plinth = new bool[n];
            for (int q = 0; q < n; q++)
            {
                BuildingRecord p = t.Buildings[parts[q]];
                top[q] = p.HeightCm / 100.0;
                min[q] = p.MinHeightCm / 100.0;
                if (!(top[q] > min[q]) || p.Rings[0].Length < 6) continue;
                int[] r = p.Rings[0];
                double px = 0, pz = 0;
                for (int v = 0; v < r.Length / 2; v++)
                {
                    px += r[2 * v] / 100.0;
                    pz += r[2 * v + 1] / 100.0;
                }
                px = px / (r.Length / 2) - cx;
                pz = pz / (r.Length / 2) - cz;
                Extents(r, cx, cz, yaw, out pw[q], out pd[q]);
                double off = Math.Sqrt(px * px + pz * pz);
                use[q] = pw[q] >= 0.3 * pp.PlinthW && pd[q] >= 0.3 * pp.PlinthD && off <= 0.2 * hostLong;
                if (use[q]) total = Math.Max(total, top[q]);
            }
            if (total < 3) return;
            for (int q = 0; q < n; q++)
                plinth[q] = use[q] && top[q] <= 0.45 * total && (min[q] < 0.3 || t.Buildings[parts[q]].RoofShape == RoofShape.Flat);
            pp.TotalHeightM = (float)(total / (1 - pp.GajurFrac)); // OSM parts stop at the roof; the gajur sits on top

            // Plinth levels.
            var lvTop = new double[n];
            var lvW = new double[n];
            int levels = 0;
            for (int q = 0; q < n; q++)
            {
                if (!plinth[q]) continue;
                lvTop[levels] = top[q];
                lvW[levels++] = pw[q];
            }
            if (levels > 0)
            {
                Array.Sort(lvTop, lvW, 0, levels);
                int m = 0;
                for (int q = 0; q < levels; q++)
                {
                    if (m > 0 && lvTop[q] - lvTop[m - 1] < 0.15)
                    {
                        lvW[m - 1] = Math.Max(lvW[m - 1], lvW[q]);
                        continue;
                    }
                    lvTop[m] = lvTop[q];
                    lvW[m++] = lvW[q];
                }
                levels = Math.Min(m, 12);
                if (levels == 1 && lvTop[0] > 3.0) levels = 0; // one tall ground part is the sanctum, not a plinth
            }
            double plinthTop = 0;
            if (levels > 0)
            {
                plinthTop = lvTop[levels - 1];
                pp.PlinthLevels = levels;
                pp.StepRiseM = (float)Math.Max(0.2, Math.Min(1.5, plinthTop / levels));
                pp.PlinthWidths = new float[levels];
                for (int q = 0; q < levels; q++) pp.PlinthWidths[q] = (float)Math.Max(lvW[q], q + 1 < levels ? lvW[q + 1] : 0);
            }

            // Roof tiers.
            var eave = new double[n];
            var ew = new double[n];
            int tiers = 0;
            for (int q = 0; q < n; q++)
            {
                BuildingRecord p = t.Buildings[parts[q]];
                if (!use[q] || plinth[q] || p.RoofShape == RoofShape.Flat || top[q] <= plinthTop + 1.5) continue;
                eave[tiers] = Math.Max(Math.Max(min[q], plinthTop + 1.0), top[q] - 0.5 * Math.Min(pw[q], pd[q]) * 0.65);
                ew[tiers++] = pw[q];
            }
            if (tiers == 0) return; // no roof parts: the footprint rule keeps the tier count, the parts give the height
            Array.Sort(eave, ew, 0, tiers);
            int c = 0;
            for (int q = 0; q < tiers; q++)
            {
                if (c > 0 && eave[q] - eave[c - 1] < 0.5)
                {
                    ew[c - 1] = Math.Max(ew[c - 1], ew[q]);
                    continue;
                }
                eave[c] = eave[q];
                ew[c++] = ew[q];
            }
            // A pagoda's roofs shrink upward: from the top down, a part no wider than the roof above it is a storey
            // body (or a porch), not a roof.
            int kept = 0;
            for (int q = c - 1; q >= 0; q--)
            {
                if (kept > 0 && ew[q] <= 1.05 * ew[c - kept]) continue;
                kept++;
                eave[c - kept] = eave[q];
                ew[c - kept] = ew[q];
            }
            Array.Copy(eave, c - kept, eave, 0, kept);
            Array.Copy(ew, c - kept, ew, 0, kept);
            tiers = Math.Min(kept, 5);
            if (kept > 5)
            {
                Array.Copy(eave, kept - 5, eave, 0, 5); // keep the top five
                Array.Copy(ew, kept - 5, ew, 0, 5);
            }
            pp.Tiers = tiers;
            pp.EaveWidths = new float[tiers];
            for (int q = 0; q < tiers; q++) pp.EaveWidths[q] = (float)ew[q];
            double rise = pp.PlinthLevels > 0 ? pp.PlinthLevels * pp.StepRiseM : 0;
            if (eave[0] >= rise + 1.5 && eave[tiers - 1] + 0.5 < total)
            {
                pp.EaveHeights = new float[tiers];
                for (int q = 0; q < tiers; q++) pp.EaveHeights[q] = (float)eave[q];
            }
        }

        // ------------------------------------------------------------------------------------------------------------

        internal static string NameOf(TileData t, int nameRef)
        {
            if (nameRef <= 0 || nameRef > t.Names.Count) return "";
            NameRecord n = t.Name(nameRef);
            return ((n.Default ?? "") + " " + (n.En ?? "") + " " + (n.Ne ?? "")).ToLowerInvariant();
        }

        private static bool Matches(string name, params string[] words)
        {
            foreach (string w in words)
                if (name.Contains(w)) return true;
            return false;
        }

        internal static ShrineKind ShrineKindOf(string name)
        {
            if (Matches(name, "ganesh", "ganesha", "binayak", "vinayak", "गणेश")) return ShrineKind.Ganesh;
            if (Matches(name, "bhairab", "bhairav", "भैरव")) return ShrineKind.Bhairav;
            if (Matches(name, "shiva", "mahadev", "linga", "lingam", "shivalaya", "शिव")) return ShrineKind.Linga;
            if (Matches(name, "nag", "naga", "नाग")) return ShrineKind.Nag;
            return ShrineKind.Generic;
        }

        /// <summary>The footprint's centroid, its door bearing (the outward normal of the oriented-box side nearest to the
        /// front edge's normal; south when there is no front) and its extents across (w) and along (d) the door axis.</summary>
        internal static void Frame(int[] ring, int frontEdge, out double cx, out double cz, out double yaw, out double w, out double d)
        {
            int n = ring.Length / 2;
            var hx = new double[2 * n + 2];
            var hz = new double[2 * n + 2];
            var bx = new double[8];
            var bz = new double[8];
            int hn = BuildingBands.Hull(ring, hx, hz);
            cx = cz = 0;
            for (int i = 0; i < n; i++)
            {
                cx += ring[2 * i] / 100.0 / n;
                cz += ring[2 * i + 1] / 100.0 / n;
            }
            double ax = 1, az = 0;
            if (hn >= 3)
            {
                BuildingBands.MinAreaBox(hx, hz, hn, bx, bz);
                double ex = bx[1] - bx[0], ez = bz[1] - bz[0], l = Math.Sqrt(ex * ex + ez * ez);
                if (l > 1e-6)
                {
                    ax = ex / l;
                    az = ez / l;
                }
                cx = 0.25 * (bx[0] + bx[1] + bx[2] + bx[3]);
                cz = 0.25 * (bz[0] + bz[1] + bz[2] + bz[3]);
            }
            // Candidate door normals: ±a and ±a⊥ (box axes); pick the one closest to the front edge's outward normal.
            double fnx = 0, fnz = -1;
            if (frontEdge >= 0 && frontEdge < n)
            {
                int j = frontEdge + 1 == n ? 0 : frontEdge + 1;
                double dx = (ring[2 * j] - ring[2 * frontEdge]) / 100.0, dz = (ring[2 * j + 1] - ring[2 * frontEdge + 1]) / 100.0, l = Math.Sqrt(dx * dx + dz * dz);
                if (l > 1e-6)
                {
                    fnx = dz / l;
                    fnz = -dx / l;
                }
            }
            double[] cxs = { ax, -ax, -az, az }, czs = { az, -az, ax, -ax };
            int best = 0;
            double bestDot = double.MinValue;
            for (int q = 0; q < 4; q++)
            {
                double dot = cxs[q] * fnx + czs[q] * fnz;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = q;
                }
            }
            double nx = cxs[best], nz = czs[best];
            yaw = Math.Atan2(nx, nz) * 180 / Math.PI;
            if (yaw < 0) yaw += 360;
            Extents(ring, cx, cz, yaw, out w, out d);
        }

        /// <summary>Extents of a ring across (u) and along (w) a door bearing.</summary>
        internal static void Extents(int[] ring, double cx, double cz, double yawDeg, out double w, out double d)
        {
            KitFrame k = KitFrame.FromYaw(cx, 0, cz, yawDeg);
            double u0 = double.MaxValue, u1 = double.MinValue, w0 = double.MaxValue, w1 = double.MinValue;
            int n = ring.Length / 2;
            for (int i = 0; i < n; i++)
            {
                double x = ring[2 * i] / 100.0 - cx, z = ring[2 * i + 1] / 100.0 - cz;
                double u = x * k.UX + z * k.UZ, ww = x * k.WX + z * k.WZ;
                u0 = Math.Min(u0, u);
                u1 = Math.Max(u1, u);
                w0 = Math.Min(w0, ww);
                w1 = Math.Max(w1, ww);
            }
            w = Math.Max(0.5, u1 - u0);
            d = Math.Max(0.5, w1 - w0);
        }

        private static double YawToNearestRoad(TileData t, double x, double z)
        {
            double best = double.MaxValue, yaw = 180;
            foreach (RoadRecord r in t.Roads)
            {
                int[] p = r.Points;
                for (int k = 0; k + 1 < p.Length / 2; k++)
                {
                    double ox, oz;
                    double d = RoadCorridor.PointSeg(x, z, p[2 * k] / 100.0, p[2 * k + 1] / 100.0, p[2 * k + 2] / 100.0, p[2 * k + 3] / 100.0, out ox, out oz);
                    if (d < best && d > 0.2 && d < 40)
                    {
                        best = d;
                        yaw = Math.Atan2(ox - x, oz - z) * 180 / Math.PI;
                    }
                }
            }
            return yaw < 0 ? yaw + 360 : yaw;
        }
    }
}
