using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Meshing.Shapes;

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

    /// <summary>How the generic generators treat a sacred outline (<see cref="SacredSelector.Classify"/>).</summary>
    public enum SacredOutcome : byte
    {
        /// <summary>Not a sacred outline the generic generators draw (another archetype, a <c>building:part</c>).</summary>
        NotSacred = 0,

        /// <summary>Framed and drawn by a generic generator (<see cref="SacredSelector.TrySelect(TileData, int, out SacredKind, out SacredParams)"/>).</summary>
        Generic = 1,

        /// <summary>
        /// Not a temple-shaped outline (concave, oversized for its kind, elongated): drawn as a Newar compound building by
        /// the house grammar instead (<see cref="SacredSelector.FallbackHouse"/>), never as a giant pagoda.
        /// </summary>
        Fallback = 2,

        /// <summary>A temple whose plan lies mostly in a drawn road corridor and where not even a wayside shrine fits the
        /// part still clear: the road keeps its corridor (decision 1) and only a low sacred stone set into the paving,
        /// daubed with sindoor and strewn with marigolds, marks the place (<see cref="SacredSelector.RoadPith"/>), like the
        /// pikhālākhu stones set into the street before Newar doors that traffic passes over.</summary>
        Omitted = 3,
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
    /// <para>
    /// Framing (docs/research/w2/ref_heritage.md): a generic structure is framed on the oriented box of its outline only
    /// when the outline is temple-shaped and of a real Kathmandu temple's size (<see cref="Plausible"/>): concave
    /// compounds (fill of the convex hull under <see cref="MinHullFill"/>), outlines longer than the largest real temples
    /// of the kind and elongated ones are drawn as a Newar compound building instead (<see cref="SacredOutcome.Fallback"/>).
    /// Heights derived from the plan are capped by the record's height and by the tallest real temples of the kind
    /// (<see cref="HeightCap"/>); tagged heights and <c>building:part</c> heights are trusted up to the generic maximum.
    /// The structure is then fitted to the drawn road corridors (decision 1, rideability beats exact footprints): its
    /// blocking colliders below <see cref="BlockingBelowM"/> (plinth levels, steps, walls, guardians, posts) keep
    /// <see cref="CorridorMarginM"/> from every corridor; the frame gives way side by side (it shrinks from the side that
    /// meets the road and its centre moves back) and a structure that would lose more than half its plan falls back.
    /// </para>
    /// </summary>
    public static class SacredSelector
    {
        /// <summary>An outline whose area is less than this share of its convex hull is a compound (a U, an L or a ring of
        /// buildings round a court or a pond), never one temple.</summary>
        public const double MinHullFill = 0.75;

        /// <summary>Outlines shorter than this (m) are small structures the shape rules leave to their generator.</summary>
        public const double SmallOutlineM = 4.0;

        /// <summary>Longest generic pagoda or house-temple plan (m): the largest real ones in the valley are Maju Dega
        /// 22.6 m and Nyatapola 26.8 m with its stair (both heroes); generic ones are 3-16 m (Jagannath 14.7, Changu Narayan
        /// 15.0, Vishwanath 12.6). Mapped with <c>building:part</c>s the parts are trusted up to <see cref="MaxPagodaPartsM"/>.</summary>
        public const double MaxPagodaM = 24.0, MaxPagodaPartsM = 30.0;

        /// <summary>Longest generic shikhara plan (m): Krishna Mandir 14.3, Rato Machhindranath 11.2, Mahabouddha 9.</summary>
        public const double MaxShikharaM = 16.0;

        /// <summary>Longest generic stupa plan (m) (Boudhanath and Swayambhu are heroes; Chabahil and Kathesimbhu are
        /// 15-25 m across their platforms).</summary>
        public const double MaxStupaM = 30.0;

        /// <summary>Longest generic shrine plan (m): a niche, a tin canopy or a miniature pagoda; a larger "shrine" outline
        /// is a temple building.</summary>
        public const double MaxShrineM = 8.0;

        /// <summary>Most elongated plans (long / short side): pagodas and shikharas are square or nearly so (Bhairavnath
        /// 1.16, Shiva-Parvati 1.45), stupas round; a Bhimsen house-temple may be longer.</summary>
        public const double MaxPagodaAspect = 1.8, MaxHouseTempleAspect = 2.6, MaxShikharaAspect = 1.6, MaxStupaAspect = 1.4;

        /// <summary>Tallest generic structures (m), to the finial: one-tier pagodas (small Ganesh and Bhairav temples)
        /// 9.5, two-tier pagodas 16 (Jagannath and Vishwanath 15, Changu Narayan 15), house-temples 16 (Bhimsen Patan 14),
        /// shikharas 20 (Krishna Mandir 20, Mahabouddha 18), stupas 16; tagged and <c>building:part</c> heights up to
        /// <see cref="MaxTaggedM"/> (Pashupatinath 23.7 m, the tallest two-tier temple, is a hero).</summary>
        public const double MaxOneTierM = 9.5, MaxTwoTierM = 16.0, MaxHouseTempleM = 16.0, MaxShikharaHeightM = 20.0, MaxStupaHeightM = 16.0, MaxTaggedM = 26.0;

        /// <summary>Colliders whose bottom is below this height over the frame ground block a rider (the reviewer's and
        /// the sample-pack test's criterion).</summary>
        public const double BlockingBelowM = 4.0;

        /// <summary>Clear distance a blocking collider keeps from a corridor edge (m).</summary>
        public const double CorridorMarginM = 0.1;

        /// <summary>A fitted plan keeps at least this share of each side of its outline's box; less falls back.</summary>
        public const double MinFitShare = 0.35;

        /// <summary>Fitting rounds (each builds the structure's LOD2 colliders once).</summary>
        private const int FitRounds = 12;

        /// <summary>Select the generator for building <paramref name="buildingIndex"/>; false when it is not sacred or
        /// falls back to the house grammar (<see cref="SacredOutcome.Fallback"/>). The frame's ground height is 0 (callers
        /// add the terrain). Fitted to the corridors the world set (<see cref="SacredClearance.Corridors"/>, else the
        /// sacred generators' stand-in).</summary>
        public static bool TrySelect(TileData t, int buildingIndex, out SacredKind kind, out SacredParams p)
        {
            return TrySelect(t, buildingIndex, null, out kind, out p);
        }

        /// <summary>As <see cref="TrySelect(TileData, int, out SacredKind, out SacredParams)"/>, fitted to
        /// <paramref name="corridors"/> (game metres; null: the corridors the world set).</summary>
        public static bool TrySelect(TileData t, int buildingIndex, IRoadCorridorQuery corridors, out SacredKind kind, out SacredParams p)
        {
            Choice c = Choose(t, buildingIndex, corridors);
            kind = c.Outcome == SacredOutcome.Generic ? c.Kind : SacredKind.None;
            p = c.Outcome == SacredOutcome.Generic ? c.Params : default(SacredParams);
            return c.Outcome == SacredOutcome.Generic;
        }

        /// <summary>How building <paramref name="buildingIndex"/> is drawn: not sacred, by a generic generator, or as a
        /// Newar compound building (<see cref="FallbackHouse"/>). Fitted to <paramref name="corridors"/> (null: the corridors
        /// the world set). Cached per tile and corridor source.</summary>
        public static SacredOutcome Classify(TileData t, int buildingIndex, IRoadCorridorQuery corridors = null)
        {
            return Choose(t, buildingIndex, corridors).Outcome;
        }

        /// <summary>The selection before the corridor fit (tests and tools: the outline's own frame).</summary>
        internal static bool TrySelectUnfitted(TileData t, int buildingIndex, out SacredKind kind, out SacredParams p)
        {
            Choice c = Compute(t, buildingIndex, null);
            kind = c.Outcome == SacredOutcome.Generic ? c.Kind : SacredKind.None;
            p = c.Outcome == SacredOutcome.Generic ? c.Params : default(SacredParams);
            return c.Outcome == SacredOutcome.Generic;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Choice: shape rules, selection, height caps, corridor fit (cached per tile and corridor source)

        private struct Choice
        {
            public SacredOutcome Outcome;
            public SacredKind Kind;
            public SacredParams Params;
        }

        private sealed class ChoiceSet
        {
            public IRoadCorridorQuery Source;
            public bool NoRoads;
            public Choice[] Items;
            public bool[] Done;
        }

        private sealed class ChoiceHolder
        {
            public readonly ChoiceSet[] Sets = new ChoiceSet[3];
            public int Next;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TileData, ChoiceHolder> Choices =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TileData, ChoiceHolder>();

        /// <summary>The corridor query the fit uses: <paramref name="corridors"/>, else the world's
        /// (<see cref="SacredClearance.Corridors"/>), else the sacred generators' stand-in; null for a tile without roads.</summary>
        private static IRoadCorridorQuery SourceFor(TileData t, IRoadCorridorQuery corridors)
        {
            if (t.Roads.Count == 0) return null;
            if (corridors != null) return corridors;
            Func<TileData, IRoadCorridorQuery> f = SacredClearance.Corridors;
            IRoadCorridorQuery q = f != null ? f(t) : null;
            return q ?? SacredRoads.For(t);
        }

        private static Choice Choose(TileData t, int i, IRoadCorridorQuery corridors)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            BuildingRecord b = t.Buildings[i];
            if (!DrawsGeneric(b)) return default(Choice);
            IRoadCorridorQuery q = SourceFor(t, corridors);
            ChoiceHolder holder = Choices.GetValue(t, k => new ChoiceHolder());
            ChoiceSet set = null;
            lock (holder)
            {
                foreach (ChoiceSet s in holder.Sets)
                    if (s != null && (q == null ? s.NoRoads : ReferenceEquals(s.Source, q))) set = s;
                if (set == null)
                {
                    set = new ChoiceSet { Source = q, NoRoads = q == null, Items = new Choice[t.Buildings.Count], Done = new bool[t.Buildings.Count] };
                    holder.Sets[holder.Next] = set;
                    holder.Next = (holder.Next + 1) % holder.Sets.Length;
                }
                if (set.Items.Length < t.Buildings.Count)
                {
                    // A tile built up record by record (tools and tests): grow the set.
                    Array.Resize(ref set.Items, t.Buildings.Count);
                    Array.Resize(ref set.Done, t.Buildings.Count);
                }
                if (set.Done[i]) return set.Items[i];
            }
            Choice c = Compute(t, i, q);
            lock (holder)
            {
                set.Items[i] = c;
                set.Done[i] = true;
            }
            return c;
        }

        private static Choice Compute(TileData t, int buildingIndex, IRoadCorridorQuery q)
        {
            var c = default(Choice);
            BuildingRecord b = t.Buildings[buildingIndex];
            if (!DrawsGeneric(b)) return c;
            BuildingFronts fronts = BuildingFronts.For(t);
            BuildingFrontRecord front = fronts.Front(buildingIndex);
            int[] ring = b.Rings[0];
            double cx, cz, yaw, w, d;
            Frame(ring, front.HasFront ? front.FrontEdge : -1, out cx, out cz, out yaw, out w, out d);
            string name = NameOf(t, b.NameRef);
            int[] parts = (b.Flags & BuildingFlags.HasParts) != 0 ? PartsOf(t, buildingIndex) : null;
            if (!Plausible(b.Archetype, name, ring, w, d, parts != null))
            {
                c.Outcome = SacredOutcome.Fallback;
                return c;
            }
            bool derived;
            Select(t, b, parts, name, cx, cz, yaw, w, d, out c.Kind, out c.Params, out derived);
            if (c.Kind == SacredKind.None) return c;
            c.Outcome = SacredOutcome.Generic;
            if (q == null) return c;
            SacredParams sel = c.Params;
            double[] rect;
            if (Fit(t, q, ref c.Params, cx, cz, yaw, w, d, derived, MinFitShare, out rect)) return c;
            // The door side meets the road: the stair (and the guardians on it) give way first, as where a real temple's
            // steps come down onto the street.
            if (c.Kind == SacredKind.Pagoda && sel.Pagoda.PlinthLevels > 0)
            {
                SacredParams ns = sel;
                ns.Pagoda.NoStair = true;
                if (Fit(t, q, ref ns, cx, cz, yaw, w, d, derived, MinFitShare, out rect))
                {
                    c.Params = ns;
                    return c;
                }
            }
            // Most of the plan lies in the road: a wayside shrine (a niche or a miniature pagoda) on the part of the plan
            // still clear, else nothing (the road keeps its corridor; a house there would stand in the road too).
            c.Kind = SacredKind.None;
            c.Params = default(SacredParams);
            c.Outcome = SacredOutcome.Omitted;
            SacredParams small;
            if (SmallIn(t, q, b, name, cx, cz, yaw, rect, out small))
            {
                c.Kind = small.Kind;
                c.Params = small;
                c.Outcome = SacredOutcome.Generic;
            }
            return c;
        }

        /// <summary>A wayside shrine (a chaitya for a stupa outline) on the clear part [u0, u1, w0, w1] of an outline's
        /// frame (at least 1.4 m each way), fitted to the corridors; false when none fits.</summary>
        private static bool SmallIn(TileData t, IRoadCorridorQuery q, BuildingRecord b, string name, double cx, double cz, double yaw, double[] rect,
                                    out SacredParams p)
        {
            p = default(SacredParams);
            double rw = rect[1] - rect[0], rd = rect[3] - rect[2];
            if (rw < 1.4 || rd < 1.4) return false;
            double l = Math.Min(5.4, Math.Max(rw, rd)), sh = Math.Min(l, Math.Min(rw, rd));
            KitFrame k = KitFrame.FromYaw(cx, 0, cz, yaw);
            double du = 0.5 * (rect[0] + rect[1]), dw = 0.5 * (rect[2] + rect[3]);
            double sx = k.OX + du * k.UX + dw * k.WX, sz = k.OZ + du * k.UZ + dw * k.WZ;
            p.Frame = new GenFrame(sx, sz, 0f, (float)yaw);
            if (b.Archetype == BuildingArchetype.Stupa || b.Archetype == BuildingArchetype.Chorten)
            {
                p.Kind = SacredKind.Chaitya;
                p.Chaitya = ChaityaParams.Defaults((float)Math.Max(0.8, Math.Min(3.0, 1.2 * sh)));
            }
            else
            {
                p.Kind = SacredKind.Shrine;
                p.Shrine = new ShrineParams
                {
                    Kind = ShrineKindOf(name), LongSideM = (float)l, ShortSideM = (float)sh,
                    Form = b.RoofMaterial == RoofMaterial.Metal ? ShrineForm.TinCanopy : l < 2.5 ? ShrineForm.Niche : ShrineForm.MiniPagoda,
                };
            }
            double[] r2;
            return Fit(t, q, ref p, sx, sz, yaw, rw, rd, false, MinFitShare, out r2);
        }

        /// <summary>
        /// True when an outline can be one generic structure of its archetype: a small outline (under
        /// <see cref="SmallOutlineM"/>) always; otherwise the outline fills at least <see cref="MinHullFill"/> of its convex
        /// hull, and its plan is no longer and no more elongated than the real temples of the kind
        /// (<see cref="MaxPagodaM"/>, <see cref="MaxShikharaM"/>, <see cref="MaxStupaM"/>, <see cref="MaxShrineM"/> and the
        /// aspect limits).
        /// </summary>
        public static bool Plausible(BuildingArchetype a, string name, int[] ring, double w, double d, bool hasParts)
        {
            if (ring == null || ring.Length < 6) return false;
            double l = Math.Max(w, d), s = Math.Max(0.5, Math.Min(w, d)), aspect = l / s;
            // A small outline (a shrine, a chaitya, a little temple; under SmallOutlineM) is drawn by its generator
            // whatever its shape: there is no compound or block hiding in it.
            if (l < SmallOutlineM) return true;
            double area = Math.Abs(RingAreaM2(ring)), hull = HullAreaM2(ring);
            if (hull > 1e-6 && area < MinHullFill * hull) return false;
            switch (a)
            {
                case BuildingArchetype.TemplePagoda:
                    if (l > (hasParts ? MaxPagodaPartsM : MaxPagodaM)) return false;
                    return aspect <= (Matches(name ?? "", "bhimsen") ? MaxHouseTempleAspect : MaxPagodaAspect);
                case BuildingArchetype.TempleShikhara:
                    return l <= MaxShikharaM && aspect <= MaxShikharaAspect;
                case BuildingArchetype.Stupa:
                case BuildingArchetype.Chorten:
                    return l < 3 || l <= MaxStupaM && aspect <= MaxStupaAspect;
                case BuildingArchetype.Shrine:
                    return l <= MaxShrineM;
                default:
                    return false;
            }
        }

        /// <summary>The height cap of a generic structure whose height comes from its plan (not tagged, not from
        /// parts): the tallest real temples of its kind (<see cref="MaxOneTierM"/> ...) and, when the record carries a
        /// (pipeline-estimated) height, 1.5 × that height or that height + 3 m, whichever is more.</summary>
        public static double HeightCap(SacredKind kind, int tiers, BuildingRecord b)
        {
            double cap;
            switch (kind)
            {
                case SacredKind.Pagoda: cap = tiers <= 1 ? MaxOneTierM : MaxTwoTierM; break;
                case SacredKind.HouseTemple: cap = MaxHouseTempleM; break;
                case SacredKind.ShikharaStone:
                case SacredKind.ShikharaPlaster: cap = MaxShikharaHeightM; break;
                case SacredKind.Stupa: cap = MaxStupaHeightM; break;
                default: cap = MaxTaggedM; break;
            }
            double rec = b != null ? b.HeightCm / 100.0 : 0;
            if (rec >= 3) cap = Math.Min(cap, Math.Max(1.5 * rec, rec + 3.0));
            return cap;
        }

        private static double RingAreaM2(int[] r)
        {
            double a = 0;
            int n = r.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++) a += (double)r[2 * j] * r[2 * i + 1] - (double)r[2 * i] * r[2 * j + 1];
            return 0.5 * a / 1e4;
        }

        private static double HullAreaM2(int[] r)
        {
            int n = r.Length / 2;
            var hx = new double[2 * n + 2];
            var hz = new double[2 * n + 2];
            int hn = BuildingBands.Hull(r, hx, hz);
            double a = 0;
            for (int i = 0, j = hn - 1; i < hn; j = i++) a += hx[j] * hz[i] - hx[i] * hz[j];
            return Math.Abs(0.5 * a);
        }

        /// <summary>The generator and its parameters for a plausible outline framed at (cx, cz, yaw, w × d);
        /// <paramref name="derived"/> says the total height came from the plan (capped) rather than a tag or the parts.</summary>
        private static void Select(TileData t, BuildingRecord b, int[] parts, string name, double cx, double cz, double yaw, double w, double d,
                                   out SacredKind kind, out SacredParams p, out bool derived)
        {
            p = default(SacredParams);
            kind = SacredKind.None;
            double longSide = Math.Max(w, d);
            float height = (b.Flags & BuildingFlags.HeightTagged) != 0 ? (float)(b.HeightCm / 100.0) : 0f;
            if (height > 0 && height < 3) height = 0; // a tagged height under 3 m on a temple outline is its plinth (T 1.3)
            // building:part present (3.2, first row): the heights come from the parts, which are not drawn themselves.
            if (parts != null)
            {
                double partTop = 0;
                foreach (int j in parts) partTop = Math.Max(partTop, t.Buildings[j].HeightCm / 100.0);
                if (partTop >= 3) height = (float)partTop;
            }
            derived = height <= 0;
            if (height > MaxTaggedM) height = (float)MaxTaggedM;
            p.Frame = new GenFrame(cx, cz, 0f, (float)yaw);
            switch (b.Archetype)
            {
                case BuildingArchetype.TemplePagoda:
                {
                    if (Matches(name, "bhimsen"))
                    {
                        kind = SacredKind.HouseTemple;
                        float total = height > 0 ? height : (float)Math.Min(HeightCap(SacredKind.HouseTemple, 0, b), Math.Max(7, 1.1 * longSide));
                        p.House = new HouseTempleParams
                        {
                            W = (float)w, D = (float)d, Storeys = longSide >= 8 ? 3 : 2, PlinthLevels = 1, PlinthRiseM = 0.5f, TotalHeightM = total, Lions = true,
                        };
                        break;
                    }
                    int tiers = longSide < 6 ? 1 : longSide < 14 ? (Matches(name, "mandir", "temple", "dega", "deval", "मन्दिर", "देगः") ? 2 : 1) : 2;
                    kind = SacredKind.Pagoda;
                    PagodaParams pp = PagodaParams.Defaults((float)w, (float)d, tiers);
                    pp.PlinthLevels = longSide < 6 ? 1 : 2;
                    pp.StepRiseM = longSide < 6 ? 0.3f : 0.45f;
                    pp.TotalHeightM = height > 0 ? height
                        : (float)Math.Min(HeightCap(SacredKind.Pagoda, tiers, b), tiers == 1 ? Math.Max(4.0, 0.85 * longSide) : Math.Max(7.0, 1.05 * longSide + 2));
                    pp.Doors = (byte)(Matches(name, "shiva", "mahadev", "shiv", "महादेव") ? 4 : 1);
                    pp.BellSpacingM = 0.5f;
                    if (parts != null) PagodaFromParts(t, parts, cx, cz, yaw, ref pp);
                    if (pp.TotalHeightM > MaxTaggedM) pp.TotalHeightM = (float)MaxTaggedM;
                    // Generic detail spreads out with size (a hero keeps its real counts): bells, struts, posts and tile
                    // courses keep their LOD0 form on a large outline at about the density of an 11 m temple.
                    pp.DetailScale = GenericDetailScale(pp.PlinthW, pp.PlinthD, pp.Tiers);
                    p.Pagoda = pp;
                    break;
                }
                case BuildingArchetype.TempleShikhara:
                {
                    bool stone = b.WallMaterial == WallMaterial.Stone;
                    kind = stone ? SacredKind.ShikharaStone : SacredKind.ShikharaPlaster;
                    ShikharaParams sp = stone ? ShikharaParams.StoneDefaults((float)w, (float)d) : ShikharaParams.PlasterDefaults((float)w, (float)d);
                    sp.TotalHeightM = height > 0 ? height : (float)Math.Min(HeightCap(kind, 0, b), Math.Max(5.0, (stone ? 1.4 : 1.9) * longSide));
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
                    st.TotalHeightM = height > 0 ? height
                        : (float)Math.Min(HeightCap(SacredKind.Stupa, 0, b), Math.Max(2.5, longSide < 8 ? 1.1 * longSide : 1.0 * longSide));
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
        }

        /// <summary>
        /// Fit a selected structure to the corridors <paramref name="q"/> (game metres): build its colliders (LOD2, the
        /// same plinths, steps, walls and props as every LOD), find every blocking collider point (bottom under
        /// <see cref="BlockingBelowM"/>, not a walkable step under 0.45 m) closer than <see cref="CorridorMarginM"/> to a
        /// corridor, and pull the side of the frame that point belongs to back by its depth (the centre moves with it),
        /// then rebuild, up to <see cref="FitRounds"/> times. False when the plan would keep less than
        /// <see cref="MinFitShare"/> of a side or the structure still meets a corridor.
        /// </summary>
        private static bool Fit(TileData t, IRoadCorridorQuery q, ref SacredParams p, double cx0, double cz0, double yaw, double w0, double d0, bool derived)
        {
            double[] rect;
            return Fit(t, q, ref p, cx0, cz0, yaw, w0, d0, derived, MinFitShare, out rect);
        }

        /// <summary>As <see cref="Fit(TileData, IRoadCorridorQuery, ref SacredParams, double, double, double, double, double, bool)"/>
        /// with a minimum share per side; <paramref name="rect"/> is the frame's last plan [u0, u1, w0, w1] (the part of
        /// the outline's box still clear when it fails).</summary>
        private static bool Fit(TileData t, IRoadCorridorQuery q, ref SacredParams p, double cx0, double cz0, double yaw, double w0, double d0, bool derived,
                                double minShare, out double[] rect)
        {
            KitFrame k = KitFrame.FromYaw(cx0, 0, cz0, yaw);
            double maxAspect, minLong, minShort;
            Limits(p.Kind, out maxAspect, out minLong, out minShort);
            var b = new double[] { -0.5 * w0, 0.5 * w0, -0.5 * d0, 0.5 * d0 };
            rect = b;
            SacredParams baseP = p;
            var r = new double[4];
            // Start from the largest part of the outline's box that is clear of the corridors (the plan itself), then let
            // the stair, guardians and posts push the sides back further round by round.
            ClearRect(t, q, k, ref b[0], ref b[1], ref b[2], ref b[3]);
            for (int round = 0; round <= FitRounds; round++)
            {
                KeepAspect(b, maxAspect);
                double nw = b[1] - b[0], nd = b[3] - b[2];
                if (nw < minShare * w0 || nd < minShare * d0 || Math.Max(nw, nd) < minLong || Math.Min(nw, nd) < minShort) return false;
                if (nw < w0 - 1e-6 || nd < d0 - 1e-6)
                {
                    double du = 0.5 * (b[0] + b[1]), dw = 0.5 * (b[2] + b[3]);
                    p = Scaled(baseP, k.OX + du * k.UX + dw * k.WX, k.OZ + du * k.UZ + dw * k.WZ, w0, d0, nw, nd, derived, t);
                }
                Array.Clear(r, 0, 4);
                bool hit = Intrudes(t, q, p, r);
                if (FitLog != null)
                    FitLog(string.Format("round {0}: u {1:0.00}..{2:0.00} w {3:0.00}..{4:0.00} retreat {5:0.00} {6:0.00} {7:0.00} {8:0.00}", round, b[0], b[1], b[2], b[3], r[0], r[1], r[2], r[3]));
                if (!hit) return true;
                if (round == FitRounds) return false;
                // Each side gives way by its deepest point (a little more each round so it converges).
                double slack = 0.03 * (round + 1);
                if (r[0] > 0) b[0] += r[0] + slack;
                if (r[1] > 0) b[1] -= r[1] + slack;
                if (r[2] > 0) b[2] += r[2] + slack;
                if (r[3] > 0) b[3] -= r[3] + slack;
            }
            return false;
        }

        /// <summary>A fitted plan's shape limits per kind: most elongated plan (a narrow strip left beside a road is no
        /// temple), and the smallest long and short sides a structure of the kind keeps (below them a wayside shrine
        /// stands in its place).</summary>
        private static void Limits(SacredKind kind, out double maxAspect, out double minLong, out double minShort)
        {
            switch (kind)
            {
                case SacredKind.Pagoda: maxAspect = MaxPagodaAspect; minLong = 3.2; minShort = 2.6; break;
                case SacredKind.HouseTemple: maxAspect = MaxHouseTempleAspect; minLong = 4.0; minShort = 2.6; break;
                case SacredKind.ShikharaStone:
                case SacredKind.ShikharaPlaster: maxAspect = MaxShikharaAspect; minLong = 3.0; minShort = 2.4; break;
                case SacredKind.Stupa: maxAspect = MaxStupaAspect; minLong = 3.0; minShort = 2.4; break;
                default: maxAspect = 2.5; minLong = 0.8; minShort = 0.8; break;
            }
        }

        /// <summary>Trim the longer side of a plan [u0, u1, w0, w1] about its middle to at most
        /// <paramref name="maxAspect"/> × its shorter side.</summary>
        private static void KeepAspect(double[] b, double maxAspect)
        {
            double nw = b[1] - b[0], nd = b[3] - b[2];
            if (nw > maxAspect * nd)
            {
                double c = 0.5 * (b[0] + b[1]), h = 0.5 * maxAspect * nd;
                b[0] = c - h;
                b[1] = c + h;
            }
            else if (nd > maxAspect * nw)
            {
                double c = 0.5 * (b[2] + b[3]), h = 0.5 * maxAspect * nw;
                b[2] = c - h;
                b[3] = c + h;
            }
        }

        [ThreadStatic] private static Action<string> _fitLog;

        /// <summary>Tools: receives one line per corridor-fit round on this thread (null: off).</summary>
        internal static Action<string> FitLog
        {
            get { return _fitLog; }
            set { _fitLog = value; }
        }

        /// <summary>Cell of the clear-rectangle search (m).</summary>
        private const double ClearCellM = 0.25;

        /// <summary>
        /// Shrink the frame box [uLo, uHi] × [wLo, wHi] to its largest axis-aligned sub-rectangle whose cells all keep
        /// <see cref="CorridorMarginM"/> (plus half a cell) from every corridor: a grid of <see cref="ClearCellM"/> cells and
        /// the largest rectangle under a histogram, row by row. Unchanged when the whole box is clear; an empty box when
        /// nothing is.
        /// </summary>
        private static void ClearRect(TileData t, IRoadCorridorQuery q, in KitFrame k, ref double uLo, ref double uHi, ref double wLo, ref double wHi)
        {
            int nu = Math.Max(1, (int)Math.Round((uHi - uLo) / ClearCellM)), nw = Math.Max(1, (int)Math.Round((wHi - wLo) / ClearCellM));
            double cu = (uHi - uLo) / nu, cw = (wHi - wLo) / nw, need = CorridorMarginM + 0.5 * Math.Sqrt(cu * cu + cw * cw);
            // The distance is 1-Lipschitz: a box whose middle is further from every corridor than its half diagonal is clear.
            double mu = 0.5 * (uLo + uHi), mw = 0.5 * (wLo + wHi), half = 0.5 * Math.Sqrt((uHi - uLo) * (uHi - uLo) + (wHi - wLo) * (wHi - wLo));
            if (q.SignedDistance(t.Tile.X0 + k.OX + mu * k.UX + mw * k.WX, t.Tile.Z0 + k.OZ + mu * k.UZ + mw * k.WZ) >= half + need) return;
            var height = new int[nu];
            bool all = true;
            int bestArea = 0, bu0 = 0, bu1 = -1, bw0 = 0, bw1 = -1;
            var stack = new int[nu + 1];
            for (int j = 0; j < nw; j++)
            {
                double w = wLo + (j + 0.5) * cw;
                for (int i = 0; i < nu; i++)
                {
                    double u = uLo + (i + 0.5) * cu;
                    double x = k.OX + u * k.UX + w * k.WX, z = k.OZ + u * k.UZ + w * k.WZ;
                    bool clear = q.SignedDistance(t.Tile.X0 + x, t.Tile.Z0 + z) >= need;
                    all &= clear;
                    height[i] = clear ? height[i] + 1 : 0;
                }
                // Largest rectangle in the histogram of row j (heights reach down to row j - height + 1).
                int top = 0;
                for (int i = 0; i <= nu; i++)
                {
                    int hcur = i < nu ? height[i] : 0;
                    while (top > 0 && height[stack[top - 1]] >= hcur)
                    {
                        int hgt = height[stack[--top]];
                        int left = top > 0 ? stack[top - 1] + 1 : 0, area = hgt * (i - left);
                        if (area > bestArea)
                        {
                            bestArea = area;
                            bu0 = left;
                            bu1 = i - 1;
                            bw0 = j - hgt + 1;
                            bw1 = j;
                        }
                    }
                    stack[top++] = i;
                }
            }
            if (all) return;
            if (bestArea == 0)
            {
                uHi = uLo;
                wHi = wLo;
                return;
            }
            double u0 = uLo + bu0 * cu, u1 = uLo + (bu1 + 1) * cu, w0 = wLo + bw0 * cw, w1 = wLo + (bw1 + 1) * cw;
            uLo = u0;
            uHi = u1;
            wLo = w0;
            wHi = w1;
        }

        /// <summary>The selected parameters moved to a new centre and scaled from the outline's box (w0 × d0) to
        /// w × d: the plan and every explicit width (plinth levels and eaves from parts) scale with it; a height from the
        /// plan follows the smaller plan (still capped), a tagged or parts height stays.</summary>
        private static SacredParams Scaled(in SacredParams src, double cx, double cz, double w0, double d0, double w, double d, bool derived, TileData t)
        {
            SacredParams p = src;
            double su = w / w0, sd = d / d0, s = Math.Min(su, sd);
            p.Frame = new GenFrame(cx, cz, src.Frame.GroundY, src.Frame.YawDeg);
            switch (p.Kind)
            {
                case SacredKind.Pagoda:
                {
                    PagodaParams pp = src.Pagoda;
                    pp.PlinthW = (float)(pp.PlinthW * su);
                    pp.PlinthD = (float)(pp.PlinthD * sd);
                    pp.PlinthWidths = ScaledArray(pp.PlinthWidths, su);
                    pp.EaveWidths = ScaledArray(pp.EaveWidths, su);
                    if (derived) pp.TotalHeightM = (float)Math.Max(4.0, pp.TotalHeightM * Math.Max(0.75, s));
                    // A tagged height on a plan the road left small: no needle (real pagodas stand about as tall as
                    // their plinth is wide, two-tier ones up to 1.6 ×).
                    pp.TotalHeightM = (float)Math.Min(pp.TotalHeightM, Math.Max(4.0, 1.6 * Math.Max(w, d) + 2.0));
                    p.Pagoda = pp;
                    break;
                }
                case SacredKind.HouseTemple:
                    p.House.W = (float)(src.House.W * su);
                    p.House.D = (float)(src.House.D * sd);
                    if (derived) p.House.TotalHeightM = (float)Math.Max(6.0, src.House.TotalHeightM * Math.Max(0.75, s));
                    p.House.TotalHeightM = (float)Math.Min(p.House.TotalHeightM, Math.Max(6.0, 1.4 * Math.Max(w, d) + 2.0));
                    break;
                case SacredKind.ShikharaStone:
                case SacredKind.ShikharaPlaster:
                    p.Shikhara.PlinthW = (float)(src.Shikhara.PlinthW * su);
                    p.Shikhara.PlinthD = (float)(src.Shikhara.PlinthD * sd);
                    if (derived) p.Shikhara.TotalHeightM = (float)Math.Max(5.0, src.Shikhara.TotalHeightM * Math.Max(0.75, s));
                    p.Shikhara.TotalHeightM = (float)Math.Min(p.Shikhara.TotalHeightM, Math.Max(5.0, 2.5 * Math.Max(w, d)));
                    break;
                case SacredKind.Stupa:
                    p.Stupa.DomeDiameterM = (float)(src.Stupa.DomeDiameterM * s);
                    p.Stupa.DrumDiameterM = (float)(src.Stupa.DrumDiameterM * s);
                    p.Stupa.DrumHeightM = (float)Math.Max(0.3, src.Stupa.DrumHeightM * s);
                    if (derived) p.Stupa.TotalHeightM = (float)Math.Max(2.5, src.Stupa.TotalHeightM * s);
                    break;
                case SacredKind.Chaitya:
                    p.Chaitya = ChaityaParams.Defaults((float)Math.Max(0.8, src.Chaitya.HeightM * s));
                    break;
                case SacredKind.Shrine:
                    p.Shrine.LongSideM = (float)(src.Shrine.LongSideM * s);
                    p.Shrine.ShortSideM = (float)(src.Shrine.ShortSideM * s);
                    break;
                case SacredKind.Hiti:
                    p.HitiW = (float)(src.HitiW * su);
                    p.HitiD = (float)(src.HitiD * sd);
                    break;
            }
            return p;
        }

        private static float[] ScaledArray(float[] a, double f)
        {
            if (a == null) return null;
            var r = new float[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = (float)(a[i] * f);
            return r;
        }

        [ThreadStatic] private static MeshData _fitMesh;
        [ThreadStatic] private static GenColliders _fitColliders;

        /// <summary>
        /// Build the structure's colliders (LOD2) and measure how far each side of its frame must give way so no blocking
        /// collider comes within <see cref="CorridorMarginM"/> of a corridor: <paramref name="retreat"/> [−u, +u, back,
        /// door side]. Each intruding sample point counts for the frame side it lies towards (its larger normalised
        /// coordinate) with its depth, divided by how squarely that side faces the road (the corridor's gradient).
        /// False when nothing intrudes.
        /// </summary>
        private static bool Intrudes(TileData t, IRoadCorridorQuery q, in SacredParams src, double[] retreat)
        {
            // Far from every corridor (the plan's half diagonal plus a generous allowance for the stair, guardians and
            // props in front): nothing to measure.
            double reach = 0.5 * Math.Sqrt(Extent(src, true) * Extent(src, true) + Extent(src, false) * Extent(src, false)) + 4.0;
            if (q.SignedDistance(t.Tile.X0 + src.Frame.X, t.Tile.Z0 + src.Frame.Z) >= reach + CorridorMarginM) return false;
            MeshData m = _fitMesh ?? (_fitMesh = new MeshData());
            GenColliders c = _fitColliders ?? (_fitColliders = new GenColliders());
            m.Clear();
            c.Clear();
            SacredParams p = src;
            p.Frame.GroundY = 0f;
            if (p.Kind == SacredKind.Pagoda) p.Pagoda.Clearance = new SacredClearance(q, t, null);
            Build(p, 2, m, c);
            KitFrame k = p.Frame.Kit;
            double hu = 0.5 * Math.Max(0.5, Extent(p, true)), hw = 0.5 * Math.Max(0.5, Extent(p, false));
            bool any = false;
            var probe = new Probe { T = t, Q = q, K = k, HU = hu, HW = hw, Retreat = retreat };
            foreach (GenBox b in c.Boxes)
            {
                if (b.HalfY * 2 < 0.45f && (b.Flags & GenColliderFlags.Walkable) != 0) continue;
                if (b.CY - b.HalfY > BlockingBelowM) continue;
                double c0 = Math.Cos(b.YawRad), s0 = Math.Sin(b.YawRad);
                int nu = Math.Max(1, (int)Math.Ceiling(2 * b.HalfX / 0.5)), nw = Math.Max(1, (int)Math.Ceiling(2 * b.HalfZ / 0.5));
                for (int iu = 0; iu <= nu; iu++)
                for (int iw = 0; iw <= nw; iw++)
                {
                    double u = -b.HalfX + 2 * b.HalfX * iu / nu, w = -b.HalfZ + 2 * b.HalfZ * iw / nw;
                    any |= probe.At(b.CX + u * c0 - w * s0, b.CZ + u * s0 + w * c0);
                }
            }
            foreach (GenRamp r in c.Ramps)
            {
                double dx = r.X1 - r.X0, dz = r.Z1 - r.Z0, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-6 || Math.Min(r.Y0, r.Y1) > BlockingBelowM) continue;
                double nx = -dz / len * r.HalfWidth, nz = dx / len * r.HalfWidth;
                int n = Math.Max(1, (int)Math.Ceiling(len / 0.5));
                for (int i = 0; i <= n; i++)
                {
                    double x = r.X0 + dx * i / n, z = r.Z0 + dz * i / n;
                    any |= probe.At(x, z);
                    any |= probe.At(x + nx, z + nz);
                    any |= probe.At(x - nx, z - nz);
                }
            }
            foreach (GenCylinder cy in c.Cylinders)
            {
                if (cy.Y0 > BlockingBelowM) continue;
                any |= probe.At(cy.CX, cy.CZ);
                for (int a = 0; a < 8; a++)
                    any |= probe.At(cy.CX + cy.Radius * Math.Cos(a * Math.PI / 4), cy.CZ + cy.Radius * Math.Sin(a * Math.PI / 4));
            }
            foreach (GenWall wl in c.Walls)
            {
                double dx = wl.X1 - wl.X0, dz = wl.Z1 - wl.Z0, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-6 || wl.Y0 > BlockingBelowM) continue;
                int n = Math.Max(1, (int)Math.Ceiling(len / 0.5));
                for (int i = 0; i <= n; i++) any |= probe.At(wl.X0 + dx * i / n, wl.Z0 + dz * i / n);
            }
            return any;
        }

        /// <summary>One structure's corridor probe: frame side and depth of each intruding point.</summary>
        private struct Probe
        {
            public TileData T;
            public IRoadCorridorQuery Q;
            public KitFrame K;
            public double HU, HW;
            public double[] Retreat;

            public bool At(double x, double z)
            {
                double gx = T.Tile.X0 + x, gz = T.Tile.Z0 + z;
                double sd = Q.SignedDistance(gx, gz);
                if (sd >= CorridorMarginM) return false;
                double depth = CorridorMarginM - sd;
                double dx = x - K.OX, dz = z - K.OZ;
                double u = dx * K.UX + dz * K.UZ, w = dx * K.WX + dz * K.WZ;
                int side = Math.Abs(u) / HU >= Math.Abs(w) / HW ? (u < 0 ? 0 : 1) : (w < 0 ? 2 : 3);
                // How squarely the side faces the road: the corridor lies down the distance gradient.
                double gu = Q.SignedDistance(gx + 0.3, gz) - Q.SignedDistance(gx - 0.3, gz), gw = Q.SignedDistance(gx, gz + 0.3) - Q.SignedDistance(gx, gz - 0.3);
                double gl = Math.Sqrt(gu * gu + gw * gw), face = 1;
                if (gl > 1e-6)
                {
                    double ox = side == 0 ? -K.UX : side == 1 ? K.UX : side == 2 ? -K.WX : K.WX;
                    double oz = side == 0 ? -K.UZ : side == 1 ? K.UZ : side == 2 ? -K.WZ : K.WZ;
                    face = Math.Max(0.7, -(ox * gu + oz * gw) / gl);
                }
                double need = depth / face;
                if (need > Retreat[side]) Retreat[side] = need;
                return true;
            }
        }

        /// <summary>The plan extent of a selected structure across (true) or along its door axis.</summary>
        private static double Extent(in SacredParams p, bool across)
        {
            switch (p.Kind)
            {
                case SacredKind.Pagoda: return across ? p.Pagoda.PlinthW : p.Pagoda.PlinthD > 0 ? p.Pagoda.PlinthD : p.Pagoda.PlinthW;
                case SacredKind.HouseTemple: return across ? p.House.W : p.House.D;
                case SacredKind.ShikharaStone:
                case SacredKind.ShikharaPlaster: return across ? p.Shikhara.PlinthW : p.Shikhara.PlinthD;
                case SacredKind.Stupa: return 1.3 * p.Stupa.DomeDiameterM;
                case SacredKind.Chaitya: return p.Chaitya.BaseW;
                case SacredKind.Shrine: return across ? p.Shrine.LongSideM : Math.Max(0.8, p.Shrine.ShortSideM);
                case SacredKind.Hiti: return across ? p.HitiW : p.HitiD;
                default: return 1;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Fallback: a Newar compound building drawn by the house grammar

        /// <summary>
        /// The house record and plan the house grammar draws for a sacred outline that falls back
        /// (<see cref="SacredOutcome.Fallback"/>): the same rings trimmed out of the road corridors by
        /// <paramref name="guard"/> as a house's would be (protected outlines are otherwise never trimmed; null when it
        /// stands in the road), a Newar building in the Newar towns and the old cores (a Newar hybrid elsewhere: brick below,
        /// plaster above), as tall as the record says (a tagged height, else the pipeline's estimate, at most 20 m) on
        /// storeys of the Newar stack, so B0, B1 and the far bands keep one height.
        /// </summary>
        public static BuildingRecord FallbackHouse(TileData t, int buildingIndex, BuildingFootprints guard, out HousePlan plan)
        {
            plan = default(HousePlan);
            BuildingRecord b = t.Buildings[buildingIndex];
            BuildingFronts fronts = BuildingFronts.For(t);
            BuildingFrontRecord f = fronts.Front(buildingIndex);
            double height = b.HeightCm >= 300 ? b.HeightCm / 100.0 : 3.0 + 3.0 * Math.Max(1, (int)b.Levels);
            height = Math.Max(4.0, Math.Min(20.0, height));
            BuildingArchetype arch = BuildingGrammar.IsNewarProfile(f.Profile) ? BuildingArchetype.Newar : BuildingArchetype.NewarHybrid;
            int storeys = Math.Max(1, Math.Min(arch == BuildingArchetype.Newar ? 5 : 6, (int)Math.Round((height - 1.8) / 2.5)));
            var r = new BuildingRecord
            {
                OsmRef = b.OsmRef, Archetype = arch, Use = b.Use, Levels = (byte)storeys,
                Flags = (b.Flags & ~(BuildingFlags.LevelsInferred | BuildingFlags.RoofTagged | BuildingFlags.HasParts)) | BuildingFlags.HeightTagged,
                HeightCm = (ulong)Math.Round(height * 100), MinHeightCm = 0, RoofShape = RoofShape.Gabled, RoofMaterial = b.RoofMaterial,
                WallMaterial = b.WallMaterial, Seed = b.Seed, NameRef = b.NameRef, Rings = b.Rings,
            };
            int front = -1, second = -1;
            if (guard != null)
            {
                BuildingRecord kept = guard.TrimLikeHouse(r, out front, out second);
                if (kept == null) return null;
                r = kept;
            }
            plan = BuildingGrammar.Plan(r, f, fronts.MainLane(buildingIndex));
            if (front >= 0)
            {
                plan.FrontEdge = front;
                plan.SecondEdge = second;
            }
            return r;
        }

        /// <summary>
        /// Drop the boxes from <paramref name="first"/> on that come within <see cref="CorridorMarginM"/> of a corridor
        /// (<paramref name="q"/>, game metres; null: none): a fallback house's flat-roof deck and apron boxes span its
        /// plots' frame boxes, which on an irregular or trimmed outline reach past its walls into the road; its outline
        /// walls (<see cref="AddOutlineWalls"/>) stay its solids.
        /// </summary>
        internal static void DropBoxesInRoads(TileData t, IRoadCorridorQuery q, GenColliders c, int first)
        {
            if (q == null || c == null) return;
            for (int k = c.Boxes.Count - 1; k >= first; k--)
            {
                GenBox b = c.Boxes[k];
                double c0 = Math.Cos(b.YawRad), s0 = Math.Sin(b.YawRad);
                int nu = Math.Max(1, (int)Math.Ceiling(2 * b.HalfX / 0.25)), nw = Math.Max(1, (int)Math.Ceiling(2 * b.HalfZ / 0.25));
                bool hit = false;
                for (int iu = 0; iu <= nu && !hit; iu++)
                for (int iw = 0; iw <= nw && !hit; iw++)
                {
                    double u = -b.HalfX + 2 * b.HalfX * iu / nu, w = -b.HalfZ + 2 * b.HalfZ * iw / nw;
                    hit = q.SignedDistance(t.Tile.X0 + b.CX + u * c0 - w * s0, t.Tile.Z0 + b.CZ + u * s0 + w * c0) < CorridorMarginM;
                }
                if (hit) c.Boxes.RemoveAt(k);
            }
        }

        /// <summary>
        /// A sacred stone set flush into a street (<see cref="SacredOutcome.Omitted"/>): a rounded, slightly domed slab of
        /// grey stone <paramref name="size"/> across and 4 cm proud of the surface (vehicles and riders pass over it; no
        /// collider), a sindoor-red daub at its centre and a few marigold heads round it. LOD 2 and beyond: the slab and
        /// the daub only.
        /// </summary>
        public static int RoadPith(in GenFrame f, double size, int lod, MeshData m)
        {
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            size = Math.Max(0.8, Math.Min(2.4, size));
            var shapeLod = new ShapeLod(Math.Min(2, Math.Max(0, lod)));
            Affine3 k = SacredDraw.Xf(f.Kit);
            var stone = ShapeBrush.Hex(0x7E786F, MaterialChannel.Stone);
            Shapes.RoundedSlab(m, k, stone, size, 0.8 * size, 0.04, 0.3 * size, 0.015, 2, shapeLod);
            Shapes.Cylinder(m, k * Affine3.Translation(0, 0.04, 0), ShapeBrush.Hex(0xE23B2A, MaterialChannel.Plain),
                                           0.12 * size, 0.006, 16, 0, 0, false, true, shapeLod);
            if (lod <= 1)
            {
                var marigold = ShapeBrush.Hex(0xF6A21B, MaterialChannel.Fabric);
                for (int q = 0; q < 6; q++)
                {
                    double a = q * Math.PI / 3 + 0.4, rr = 0.25 * size;
                    Shapes.Sphere(m, k * Affine3.Translation(rr * Math.Cos(a), 0.06, 0.8 * rr * Math.Sin(a)), marigold, 0.035, 8, shapeLod);
                }
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>The outline walls of a fallback house as colliders (the footprint solids leave sacred outlines to the
        /// generators): every ring edge from the house's ground minus <paramref name="sinkM"/> to its top.</summary>
        internal static void AddOutlineWalls(TileData t, BuildingRecord house, in HousePlan plan, IHeightSampler h, float sinkM, GenColliders c)
        {
            if (c == null) return;
            var g = new BuildingGround(t, h);
            double ground = double.MaxValue;
            int[] outer = house.Rings[0];
            for (int k = 0; k < outer.Length / 2; k++) ground = Math.Min(ground, g.Height(outer[2 * k] / 100.0, outer[2 * k + 1] / 100.0));
            foreach (int[] ring in house.Rings)
            {
                int n = ring.Length / 2;
                for (int k = 0; k < n; k++)
                {
                    int j = k + 1 == n ? 0 : k + 1;
                    double x0 = ring[2 * k] / 100.0, z0 = ring[2 * k + 1] / 100.0, x1 = ring[2 * j] / 100.0, z1 = ring[2 * j + 1] / 100.0;
                    if ((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0) < 1e-4) continue;
                    c.AddWall(x0, z0, x1, z1, ground - sinkM, ground + plan.WallTopM, 0.05);
                }
            }
        }

        private static readonly BuildingOptions FallbackFarOptions = BuildingBandTable.B1Options(new BuildingOptions());

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
            int built;
            return BuildGeneric(t, buildingIndex, h, lod, m, c, out built);
        }

        /// <summary>
        /// Build the generic structure for a building at a LOD, held to its LOD ceiling (<see cref="MaxTris"/>): when the
        /// requested LOD is over, the repeated detail is spread out (<see cref="Thin"/>: bells, struts, posts, tile courses,
        /// flag lines) up to three times before the structure drops to the next LOD, so a large temple near the camera
        /// keeps its LOD0 form. <paramref name="builtLod"/> is the LOD actually built.
        /// </summary>
        public static bool BuildGeneric(TileData t, int buildingIndex, IHeightSampler h, int lod, MeshData m, GenColliders c, out int builtLod)
        {
            return BuildGeneric(t, buildingIndex, h, lod, m, c, null, out builtLod);
        }

        /// <summary>
        /// As <see cref="BuildGeneric(TileData, int, IHeightSampler, int, MeshData, GenColliders, out int)"/>, fitted to
        /// <paramref name="corridors"/> (game metres; null: the corridors the world set, <see cref="SacredClearance.Corridors"/>).
        /// An outline that falls back (<see cref="SacredOutcome.Fallback"/>) is drawn as the styled extrusion of its
        /// <see cref="FallbackHouse"/> (B1 and beyond; the B0 cells draw it with the house grammar) with its outline walls as
        /// colliders.
        /// </summary>
        public static bool BuildGeneric(TileData t, int buildingIndex, IHeightSampler h, int lod, MeshData m, GenColliders c, IRoadCorridorQuery corridors,
                                        out int builtLod)
        {
            builtLod = -1;
            Choice choice = Choose(t, buildingIndex, corridors);
            if (choice.Outcome == SacredOutcome.NotSacred) return false;
            if (choice.Outcome == SacredOutcome.Fallback)
            {
                HousePlan plan;
                BuildingRecord house = FallbackHouse(t, buildingIndex, BuildingFootprints.For(t, SourceFor(t, corridors)), out plan);
                if (house != null)
                {
                    if (!BuildingMesher.Styled(t, house, h, FallbackFarOptions, plan, m)) return false;
                    AddOutlineWalls(t, house, plan, h, FallbackFarOptions.SinkM, c);
                    builtLod = Math.Max(0, lod);
                    return true;
                }
                // The compound stands in the road (the guard keeps too little of it): its stone pith, as a temple there.
            }
            if (choice.Outcome != SacredOutcome.Generic)
            {
                int[] r = t.Buildings[buildingIndex].Rings[0];
                double cx, cz, yaw, w, d;
                Frame(r, -1, out cx, out cz, out yaw, out w, out d);
                var surface = new RoadSurface(t, h);
                RoadPith(new GenFrame(cx, cz, (float)surface.Height(cx, cz), (float)yaw), Math.Min(Math.Min(w, d), 2.4), lod, m);
                builtLod = Math.Max(0, lod);
                return true;
            }
            SacredKind kind = choice.Kind;
            SacredParams p = choice.Params;
            var g = new RoadSurface(t, h);
            int[] ring = t.Buildings[buildingIndex].Rings[0];
            double ground = double.MaxValue;
            for (int k = 0; k < ring.Length / 2; k++) ground = Math.Min(ground, g.Height(ring[2 * k] / 100.0, ring[2 * k + 1] / 100.0));
            p.Frame.GroundY = (float)ground;
            if (kind == SacredKind.Pagoda && t.Roads.Count > 0) p.Pagoda.Clearance = new SacredClearance(SourceFor(t, corridors), t, h);
            // Hold the structure to its LOD ceiling (3.2 LOD table): a large or richly mapped one drops a LOD instead.
            bool small = kind == SacredKind.Shrine || kind == SacredKind.Chaitya || kind == SacredKind.Hiti;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            int boxes0 = c == null ? 0 : c.Boxes.Count, ramps0 = c == null ? 0 : c.Ramps.Count;
            for (int l = Math.Max(0, lod); ; l++)
            {
                SacredParams q = p;
                for (int attempt = 0; ; attempt++)
                {
                    Build(q, l, m, c);
                    int tris = (m.IndexCount - i0) / 3, cap = MaxTris(small, l);
                    if (l >= 3 || tris <= cap)
                    {
                        builtLod = l;
                        return true;
                    }
                    m.VertexCount = v0;
                    m.IndexCount = i0;
                    if (c != null)
                    {
                        c.Boxes.RemoveRange(boxes0, c.Boxes.Count - boxes0);
                        c.Ramps.RemoveRange(ramps0, c.Ramps.Count - ramps0);
                    }
                    if (l >= 2 || attempt >= 3 || !Thin(ref q, (double)tris / cap)) break;
                }
            }
        }

        /// <summary>The detail spacing of a generic pagoda of a plan and tier count: 1 up to a 9 m plan, then growing with
        /// the plan (a third more for 3 and more tiers), so its bells, struts, posts and tile courses keep the density of
        /// a 9 m temple.</summary>
        public static float GenericDetailScale(float w, float d, int tiers)
        {
            return (float)Math.Max(1.0, Math.Max(w, d) / 9.0 * (tiers >= 3 ? 1.3 : 1.0));
        }

        /// <summary>Spread a structure's repeated detail out for an overshoot of <paramref name="over"/> × its ceiling;
        /// false when its kind has nothing left to thin.</summary>
        internal static bool Thin(ref SacredParams p, double over)
        {
            float f = (float)Math.Max(1.3, 1.25 * over);
            switch (p.Kind)
            {
                case SacredKind.Pagoda:
                    if (p.Pagoda.DetailScale >= 12f) return false;
                    p.Pagoda.DetailScale = Math.Max(1f, p.Pagoda.DetailScale) * f;
                    return true;
                case SacredKind.HouseTemple:
                    if (p.House.DetailScale >= 12f) return false;
                    p.House.DetailScale = Math.Max(1f, p.House.DetailScale) * f;
                    return true;
                case SacredKind.Stupa:
                    if (p.Stupa.DetailScale >= 12f) return false;
                    p.Stupa.DetailScale = Math.Max(1f, p.Stupa.DetailScale) * f;
                    return true;
                case SacredKind.ShikharaStone:
                case SacredKind.ShikharaPlaster:
                    if (p.Shikhara.DetailScale >= 12f) return false;
                    p.Shikhara.DetailScale = Math.Max(1f, p.Shikhara.DetailScale) * f;
                    return true;
                default:
                    return false;
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
