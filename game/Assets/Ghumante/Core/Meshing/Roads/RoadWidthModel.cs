using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// One road cross-section in game metres (W2_DESIGN 4.5, 10.3). Left and right are relative to the piece's point
    /// order. <see cref="FootpathLeftM"/> and <see cref="FootpathRightM"/> include the kerb top
    /// (<see cref="KerbLeftM"/>, <see cref="KerbRightM"/>); 0 = no footpath on that side. <see cref="MedianM"/> is the
    /// full median width of a paired dual road (each carriageway draws half of it on its right, the offside in
    /// left-hand traffic). <see cref="CentreShiftM"/> moves the carriageway centre to the left of the mapped
    /// centreline so a dual carriageway widens away from its median and a shifted corridor clears a protected footprint
    /// (positive = left).
    /// </summary>
    public struct RoadProfile
    {
        /// <summary>The §4.2 game carriageway width (lanes, access masks and the driving index of stage one follow it; the
        /// surface as drawn is <see cref="DrawnM"/> at <see cref="DrawnShiftM"/>, <see cref="Roads.RoadSurfaceQuery"/>).</summary>
        public float CarriagewayM;

        /// <summary>The drawn carriageway width: the §4.2 access width raised to the rideability floor (decision 1: every
        /// street at least <see cref="Roads.RoadClearance.MinCorridorM"/>), as the road mesher draws it.</summary>
        public float DrawnM;

        public float MedianM, FootpathLeftM, FootpathRightM, KerbLeftM, KerbRightM, ShoulderM;
        public byte Lanes, LanesFwd, LanesBwd;
        public Travel Access;
        public bool CentreLine, LaneLines, EdgeLines;

        /// <summary>Real (unscaled) width of the carriageway at this point.</summary>
        public float RealM;

        /// <summary>Lateral shift of the carriageway centre from the centreline (<see cref="RoadWidthProfile.ShiftAt"/>).</summary>
        public float CentreShiftM;

        /// <summary>The centre shift of the drawn carriageway (= <see cref="CentreShiftM"/>).</summary>
        public float DrawnShiftM;

        /// <summary>Kerb to kerb plus shoulders, footpaths and the median half: the whole width this piece occupies
        /// across its centreline.</summary>
        public float TotalM
        {
            get { return Math.Max(CarriagewayM, DrawnM) + 2f * ShoulderM + FootpathLeftM + FootpathRightM + 0.5f * MedianM; }
        }
    }

    /// <summary>
    /// A piece's game width sampled along its rendered length (<see cref="RoadWidthModel.BuildProfile"/>) every
    /// <see cref="StepM"/>: the §4.2 width (<see cref="Width"/>: clamped by the corridor with the footpaths reserved
    /// first, smoothed and tapered, widened at roundabout arms; lanes, access masks and the driving index of stage one
    /// follow it, so a galli drawn 4.8 m wide for three motorbikes still keeps cars out), the carriageway as drawn
    /// (<see cref="Drawn"/>: the §4.2 width raised to the rideability floor <see cref="RoadWidthModel.RideableMinM"/>,
    /// decision 1, joined at knees and clamped against unconnected parallel pieces: what the road mesher draws and the
    /// corridor holds), the footpath widths that fit beside it, and the lateral shift of the drawn carriageway
    /// (<see cref="ShiftAt"/>: a dual carriageway widening away from its median, a push away from a parallel piece and
    /// the corridor shift away from a protected footprint). The surface as drawn, junction caps and rings included, is
    /// answered by <see cref="Roads.RoadSurfaceQuery"/>. Reusable: <see cref="RoadWidthModel.BuildProfile"/> overwrites
    /// it without allocating once it has grown.
    /// </summary>
    public sealed class RoadWidthProfile
    {
        public float RealM;
        public float NominalM;
        public float LengthM;
        public float StepM;
        public int Count;

        /// <summary>The §4.2 carriageway width per sample (lanes, access masks and the driving index of stage one).</summary>
        public float[] Width = new float[64];

        public float[] FootLeft = new float[64];
        public float[] FootRight = new float[64];
        public float[] Limit = new float[64];

        /// <summary>The drawn carriageway width per sample (at least the rideability floor).</summary>
        public float[] Drawn = new float[64];

        /// <summary>Lateral corridor shift of the carriageway (metres, positive = left of the point order) per sample, or
        /// null when the piece is not shifted (RSTR corridor shift away from a protected footprint).</summary>
        public float[] Shift;

        /// <summary>A dual carriageway (RATR partner): the carriageway widens away from its median on its right.</summary>
        public bool Dual;

        /// <summary>The structure record says no car fits (RoadStructureFlags.CarAccessible clear; decision 5).</summary>
        public bool NoCars;

        internal void Ensure(int n)
        {
            if (Width.Length >= n) return;
            int cap = Math.Max(n, Width.Length * 2);
            Width = new float[cap];
            Drawn = new float[cap];
            FootLeft = new float[cap];
            FootRight = new float[cap];
            Limit = new float[cap];
        }

        /// <summary>The access width at <paramref name="along"/> metres: the §4.2 width (a galli drawn 4.8 m wide for three
        /// motorbikes still keeps cars out).</summary>
        public float AccessWidthAt(double along)
        {
            return Sample(Width, along);
        }

        /// <summary>The narrowest access width along the piece.</summary>
        public float MinAccessWidth
        {
            get { return MinWidth; }
        }

        /// <summary>The drawn carriageway width at <paramref name="along"/> metres.</summary>
        public float DrawnAt(double along)
        {
            return Sample(Drawn, along);
        }

        /// <summary>
        /// Lateral shift of the drawn carriageway centre from the centreline at <paramref name="along"/> metres (positive =
        /// left of the point order): half the widening of a dual carriageway (its median-side edge stays at real / 2) plus
        /// the corridor shift (<see cref="Shift"/>). Every consumer (ribbon, grade, caps, markings, corridor, surface
        /// query) offsets the carriageway by exactly this.
        /// </summary>
        public float ShiftAt(double along)
        {
            float s = Dual ? 0.5f * (DrawnAt(along) - RealM) : 0f;
            if (Shift != null) s += Sample(Shift, along);
            return s;
        }

        /// <summary>The corridor shift alone at <paramref name="along"/> metres (0 without one).</summary>
        public float CorridorShiftAt(double along)
        {
            return Shift == null ? 0f : Sample(Shift, along);
        }

        /// <summary>The widest drawn point of the piece.</summary>
        public float MaxDrawn
        {
            get
            {
                float m = 0f;
                for (int i = 0; i < Count; i++) m = Math.Max(m, Drawn[i]);
                return m;
            }
        }

        /// <summary>The narrowest drawn point of the piece.</summary>
        public float MinDrawn
        {
            get
            {
                float m = float.MaxValue;
                for (int i = 0; i < Count; i++) m = Math.Min(m, Drawn[i]);
                return Count == 0 ? 0f : m;
            }
        }

        /// <summary>Linear interpolation of <paramref name="values"/> at <paramref name="along"/> metres (clamped).</summary>
        public float Sample(float[] values, double along)
        {
            if (Count <= 1) return Count == 1 ? values[0] : 0f;
            double f = along / StepM;
            if (f <= 0) return values[0];
            int i = (int)f;
            if (i >= Count - 1) return values[Count - 1];
            float t = (float)(f - i);
            return values[i] + (values[i + 1] - values[i]) * t;
        }

        /// <summary>The §4.2 carriageway width at <paramref name="along"/> metres (the drawn width is
        /// <see cref="DrawnAt"/>).</summary>
        public float WidthAt(double along)
        {
            return Sample(Width, along);
        }

        /// <summary>The narrowest §4.2 width along the piece.</summary>
        public float MinWidth
        {
            get
            {
                float m = float.MaxValue;
                for (int i = 0; i < Count; i++) m = Math.Min(m, Width[i]);
                return Count == 0 ? 0f : m;
            }
        }

        /// <summary>The widest §4.2 width along the piece.</summary>
        public float MaxWidth
        {
            get
            {
                float m = 0f;
                for (int i = 0; i < Count; i++) m = Math.Max(m, Width[i]);
                return m;
            }
        }
    }

    /// <summary>
    /// The only place that decides road widths (W2_DESIGN 4.1-4.4, owner decision W2-O3, detail-pass decision 1): real
    /// widths from the tag or the class × area-type table, the 1.25× game rule with its minimums, the corridor clamp
    /// (never narrower than real, at most real + 6 m), 1 : 20 tapers, then the rideability floor (every street at least
    /// <see cref="Ghumante.Core.Meshing.Roads.RoadClearance.MinCorridorM"/> = 4.8 m: three motorbikes side by side;
    /// buildings in the way are trimmed to the corridor), footpaths that fit, lane counts, paint rules and the access
    /// mask by the width before the floor. <see cref="RoadMesher"/>, <see cref="JunctionMesher"/>,
    /// <see cref="MarkingMesher"/> and (by contract) the road spatial index, lane graph and routing masks call it, so
    /// mesh, physics, AI and routes agree. Pure and deterministic; thread-safe (thread-static scratch only).
    /// </summary>
    public static class RoadWidthModel
    {
        /// <summary>Game / real width for motor classes.</summary>
        public const float Scale = 1.25f;

        /// <summary>Width change per length: 1 m of width over 20 m (a 0.5 m step takes the minimum 10 m).</summary>
        public const float TaperRatio = 20f;

        /// <summary>Game width never exceeds real + this (proportions survive).</summary>
        public const float MaxGainM = 6f;

        /// <summary>Game / real width for pedestrian streets (footways, paths and steps stay 1.0).</summary>
        public const float PedestrianScale = 1.15f;

        public const float MaxRealM = 40f;

        /// <summary>Clearance subtracted from the corridor (0.5 m to the building on each side).</summary>
        public const float CorridorClearanceM = 1.0f;

        /// <summary>Footpath width change per length: 1 m over this many metres (a kerb-line flare).</summary>
        public const float FootTaperRatio = 4f;

        /// <summary>Corridor jitter is smoothed with a moving minimum over this window.</summary>
        public const float SmoothWindowM = 30f;

        /// <summary>Sample spacing of <see cref="RoadWidthProfile"/>.</summary>
        public const float ProfileStepM = 5f;

        /// <summary>A footpath narrower than this is dropped.</summary>
        public const float MinFootpathM = 1.0f;

        /// <summary>Kerb top width (barrier kerb next to a footpath, NRS 13.6).</summary>
        public const float KerbTopM = 0.15f;

        /// <summary>A dual carriageway's median is never narrower than this.</summary>
        public const float MinMedianM = 1.0f;

        /// <summary>Drawn width floor of footways, paths, cycleways, bridleways and steps: two walkers and a bicycle
        /// pass, and the chase camera has room. Their clear corridor is still <see cref="Roads.RoadClearance.MinCorridorM"/>.</summary>
        public const float FootRideableM = 2.5f;

        [ThreadStatic] private static RoadWidthProfile _scratch;
        [ThreadStatic] private static RoadRecord _scratchRoad;
        [ThreadStatic] private static int[] _scratchCorridor;
        [ThreadStatic] private static AreaType _scratchArea;

        // -------------------------------------------------------------------------------------------------------
        // Classes
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Roads cars may use by class (before width and access rules).</summary>
        public static bool IsMotor(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk:
                case RoadClass.Primary:
                case RoadClass.Secondary:
                case RoadClass.Tertiary:
                case RoadClass.Unclassified:
                case RoadClass.Residential:
                case RoadClass.LivingStreet:
                case RoadClass.Service:
                case RoadClass.Track:
                case RoadClass.Road:
                case RoadClass.Unknown:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Footway, path, steps, cycleway and bridleway: never scaled.</summary>
        public static bool IsFootClass(RoadClass c)
        {
            return c == RoadClass.Footway || c == RoadClass.Path || c == RoadClass.Steps || c == RoadClass.Cycleway ||
                   c == RoadClass.Bridleway;
        }

        /// <summary>Motorway, trunk, primary and secondary: the arterials. Those skirting or crossing an old core (Kanti Path,
        /// Durbar Marg, Tripureshwor, the Bhaktapur bypass) keep footpaths and paint there; only the lanes are shared
        /// streets.</summary>
        public static bool IsArterial(RoadClass c)
        {
            return c == RoadClass.Motorway || c == RoadClass.Trunk || c == RoadClass.Primary || c == RoadClass.Secondary;
        }

        /// <summary>Trunk to tertiary (and motorway): the "major" two-way classes.</summary>
        public static bool IsMajor(RoadClass c)
        {
            return c == RoadClass.Motorway || c == RoadClass.Trunk || c == RoadClass.Primary || c == RoadClass.Secondary ||
                   c == RoadClass.Tertiary;
        }

        /// <summary>Scale applied to the real width: 1.25 motor, 1.15 pedestrian street, 1.0 footway, path, steps.</summary>
        public static float ScaleFor(RoadClass c)
        {
            if (c == RoadClass.Pedestrian) return PedestrianScale;
            return IsFootClass(c) ? 1f : Scale;
        }

        private static int Column(AreaType a)
        {
            switch (a)
            {
                case AreaType.OldCore: return 0;
                case AreaType.PeriUrban: return 2;
                case AreaType.Rural:
                case AreaType.Forest: return 3;
                case AreaType.Hill: return 4;
                default: return 1; // Urban and Unknown
            }
        }

        private static float Pick(AreaType a, float oldCore, float urban, float peri, float rural, float hill)
        {
            switch (Column(a))
            {
                case 0: return oldCore;
                case 2: return peri;
                case 3: return rural;
                case 4: return hill;
                default: return urban;
            }
        }

        /// <summary>Real carriageway width when the way has no plausible width tag (W2_DESIGN 4.1, roads.md 8.1).
        /// One-way ways get the width per carriageway.</summary>
        public static float DefaultRealWidthM(RoadClass c, AreaType a, bool oneway, byte lanes)
        {
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk:
                    if (oneway) return 7.0f;
                    return Pick(a, 14f, 14f, 14f, 9f, 7.5f);
                case RoadClass.Primary:
                    if (!oneway && lanes >= 4 && Column(a) <= 2) return 14f;
                    return Pick(a, 7f, 7f, 7f, 6.5f, 7f);
                case RoadClass.Secondary: return Pick(a, 6f, 7f, 7f, 5.5f, 5.75f);
                case RoadClass.Tertiary: return Pick(a, 5f, 7f, 6f, 4.5f, 4.5f);
                case RoadClass.Unclassified:
                case RoadClass.Road:
                case RoadClass.Unknown: return Pick(a, 3.5f, 4f, 5f, 3.5f, 3.5f);
                case RoadClass.Residential: return Pick(a, 4f, 5f, 5f, 3.5f, 3.5f);
                case RoadClass.LivingStreet: return Pick(a, 3f, 3f, 3.5f, 3f, 3f);
                case RoadClass.Service: return Pick(a, 3.5f, 4f, 4.5f, 3.5f, 3f);
                case RoadClass.Track: return 3f;
                case RoadClass.Pedestrian: return Pick(a, 4f, 4f, 4f, 4f, 3f);
                case RoadClass.Footway:
                case RoadClass.Cycleway: return Pick(a, 1.75f, 2f, 2f, 1.5f, 1.2f);
                case RoadClass.Path:
                case RoadClass.Bridleway: return Pick(a, 1.5f, 1.5f, 1.5f, 1.2f, 1f);
                case RoadClass.Steps: return Pick(a, 1.5f, 2f, 1.5f, 1.2f, 1.2f);
                default: return 4f;
            }
        }

        /// <summary>Unpaved shoulder per side (W2_DESIGN 4.1: rural trunk 1.5, hill trunk and primary 1.0, hill
        /// secondary 0.75).</summary>
        public static float ShoulderM(RoadClass c, AreaType a)
        {
            int col = Column(a);
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk: return col == 3 ? 1.5f : col == 4 ? 1.0f : 0f;
                case RoadClass.Primary: return col == 4 ? 1.0f : 0f;
                case RoadClass.Secondary: return col == 4 ? 0.75f : 0f;
                default: return 0f;
            }
        }

        /// <summary>Smallest real width a tag or default may give (W2_DESIGN 4.1 floors).</summary>
        public static float FloorM(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk: return 6f;
                case RoadClass.Primary: return 5f;
                case RoadClass.Secondary: return 4f;
                case RoadClass.Tertiary: return 3f;
                case RoadClass.Residential:
                case RoadClass.Unclassified:
                case RoadClass.Service:
                case RoadClass.LivingStreet:
                case RoadClass.Road:
                case RoadClass.Unknown: return 2.5f;
                case RoadClass.Track:
                case RoadClass.Pedestrian: return 2f;
                case RoadClass.Footway:
                case RoadClass.Steps:
                case RoadClass.Cycleway: return 0.9f;
                default: return 0.6f; // path, bridleway
            }
        }

        /// <summary>Minimum game width (W2_DESIGN 4.3). The corridor clamp still wins over it.</summary>
        public static float MinGameWidthM(RoadClass c, bool oneway, bool dual, AreaType a)
        {
            if (IsFootClass(c))
            {
                if (c == RoadClass.Steps) return 1.2f;
                return a == AreaType.OldCore ? 1.2f : 1.5f;
            }
            if (c == RoadClass.Pedestrian || c == RoadClass.Track || c == RoadClass.LivingStreet) return 3f;
            if (dual) return 7f;
            if (oneway) return 4f;
            return IsMajor(c) ? 6.5f : 4f;
        }

        /// <summary>
        /// The drawn carriageway floor (detail-pass decision 1): every street (motor classes and pedestrian streets) at
        /// least <see cref="Roads.RoadClearance.MinCorridorM"/>, footways, paths and steps at least
        /// <see cref="FootRideableM"/>. It depends on the class only, so both tiles of a cut way agree.
        /// </summary>
        public static float RideableMinM(RoadClass c)
        {
            return IsFootClass(c) ? FootRideableM : Roads.RoadClearance.MinCorridorM;
        }

        /// <summary>
        /// True when the tile's RATR corridors are the detail-pass final game corridors: the pipeline writes RSTR (one
        /// structure record per road) together with them, and trims every building to them (data package,
        /// docs/W2_DETAIL_CONTRACT.md decision 1). Stage-one packs (RATR without RSTR) carry building-to-building
        /// corridors, from which <see cref="CorridorClearanceM"/> is still subtracted.
        /// </summary>
        public static bool UsesFinalCorridor(TileData t)
        {
            return t != null && t.HasRoadAttrs && t.Roads.Count > 0 && t.RoadStructures.Count == t.Roads.Count;
        }

        /// <summary>Area type used for a road: the RATR value, else Urban (the valley default column).</summary>
        public static AreaType AreaOf(in RoadAttrRecord a)
        {
            return a.Area == AreaType.Unknown ? AreaType.Urban : a.Area;
        }

        private static bool Oneway(RoadRecord r)
        {
            return (r.Flags & RoadFlags.Oneway) != 0;
        }

        /// <summary>True when a width tag is usable: at least half the class floor and at most 40 m.</summary>
        public static bool TagPlausible(RoadRecord r)
        {
            if (r.WidthCm == 0) return false;
            double w = r.WidthCm / 100.0;
            return w >= 0.5 * FloorM(r.RoadClass) && w <= MaxRealM;
        }

        /// <summary>The real width (§4.1): the plausible tag, else the class × area default, clamped to
        /// [floor, 40 m]. One-way ways: per carriageway.</summary>
        public static float RealWidthM(in RoadRecord r, in RoadAttrRecord a)
        {
            float w = TagPlausible(r) ? (float)(r.WidthCm / 100.0) : DefaultRealWidthM(r.RoadClass, AreaOf(a), Oneway(r), r.Lanes);
            float floor = FloorM(r.RoadClass);
            return w < floor ? floor : w > MaxRealM ? MaxRealM : w;
        }

        /// <summary>Game width before the corridor clamp: <c>min(max(real × S, minimum), real + 6)</c>.</summary>
        public static float NominalGameWidthM(in RoadRecord r, in RoadAttrRecord a)
        {
            float real = RealWidthM(r, a);
            float g = Math.Max(real * ScaleFor(r.RoadClass), MinGameWidthM(r.RoadClass, Oneway(r), a.Has(RoadAttrFlags.Dual), AreaOf(a)));
            return Math.Min(g, real + MaxGainM);
        }

        /// <summary>
        /// The §4.2 width for a given corridor limit (already reduced by the clearance; <c>+∞</c> = open):
        /// <c>max(min(nominal, limit), realEff)</c>, where an untagged real width never exceeds the measured limit
        /// (the class default is an estimate) nor goes under the class floor.
        /// </summary>
        public static float ClampToLimit(float real, float nominal, float floor, bool tagged, float limit)
        {
            float realEff = real;
            if (!tagged && limit < realEff) realEff = Math.Max(floor, limit);
            float g = Math.Min(nominal, limit);
            if (g < realEff) g = realEff;
            return Math.Min(g, realEff + MaxGainM);
        }

        /// <summary>True in built-up area types (old core, urban, peri-urban), where buildings can bind the corridor:
        /// such pieces meet a tile border at their real width so both tiles agree without seeing each other's
        /// buildings. Elsewhere a border crossing keeps the nominal game width.</summary>
        public static bool CorridorBound(in RoadAttrRecord a)
        {
            AreaType t = AreaOf(a);
            return t == AreaType.OldCore || t == AreaType.Urban || t == AreaType.PeriUrban;
        }

        /// <summary>
        /// The width a piece has where it is cut at a tile border. Both tiles must agree without seeing each other's
        /// buildings or area cells (the pipeline may class the two pieces of one way differently), so it depends
        /// only on the way itself: the plausible tag, else the class default in the valley (URBAN) column, clamped
        /// to [floor, 40] — the real width, which is never over a building where the corridor binds.
        /// </summary>
        public static float BorderWidthM(RoadRecord r)
        {
            float w = TagPlausible(r) ? (float)(r.WidthCm / 100.0) : DefaultRealWidthM(r.RoadClass, AreaType.Urban, Oneway(r), r.Lanes);
            float floor = FloorM(r.RoadClass);
            w = w < floor ? floor : w > MaxRealM ? MaxRealM : w;
            return Math.Max(w, RideableMinM(r.RoadClass));
        }

        /// <summary>
        /// The area class both tiles assume for the cross-section where a piece is cut at a tile border (footpaths,
        /// shoulders): the pipeline may class the two pieces of one way differently, so it depends only on where the cut
        /// lies: URBAN on the Kathmandu valley floor (85.25..85.46 E, 27.62..27.78 N: Kathmandu, Lalitpur, Bhaktapur and
        /// the Ring Road towns), RURAL elsewhere.
        /// </summary>
        public static AreaType BorderArea(double lonDeg, double latDeg)
        {
            return lonDeg >= 85.25 && lonDeg <= 85.46 && latDeg >= 27.62 && latDeg <= 27.78 ? AreaType.Urban : AreaType.Rural;
        }

        /// <summary>The footpaths a piece has where it is cut at a tile border (<see cref="NominalFootpaths"/> in the
        /// <paramref name="borderArea"/> column at <see cref="BorderAccessWidthM"/>, none under <see cref="MinFootpathM"/>):
        /// tile-independent, so both pieces of a cut way meet with the same kerbs.</summary>
        public static void BorderFootpaths(RoadRecord r, in RoadAttrRecord a, AreaType borderArea, out float left, out float right)
        {
            RoadAttrRecord c = a;
            c.Area = borderArea;
            NominalFootpaths(r, c, BorderAccessWidthM(r), out left, out right);
            if (left < MinFootpathM) left = 0f;
            if (right < MinFootpathM) right = 0f;
        }

        /// <summary>The access width a cut end meets the border at (<see cref="BorderWidthM"/> before the floor).</summary>
        public static float BorderAccessWidthM(RoadRecord r)
        {
            float w = TagPlausible(r) ? (float)(r.WidthCm / 100.0) : DefaultRealWidthM(r.RoadClass, AreaType.Urban, Oneway(r), r.Lanes);
            float floor = FloorM(r.RoadClass);
            return w < floor ? floor : w > MaxRealM ? MaxRealM : w;
        }

        /// <summary>Rendered length of a piece (context points excluded), in metres.</summary>
        public static double RenderedLengthM(RoadRecord r)
        {
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            double s = 0;
            for (int i = first; i < last; i++)
            {
                double dx = (p[2 * i + 2] - p[2 * i]) / 100.0, dz = (p[2 * i + 3] - p[2 * i + 1]) / 100.0;
                s += Math.Sqrt(dx * dx + dz * dz);
            }
            return s;
        }

        /// <summary>The corridor limit (corridor − clearance, smoothed by a 30 m moving minimum) at
        /// <paramref name="alongM"/>; +∞ when open or unknown outside old cores; the real width when unknown inside an
        /// old core.</summary>
        public static float LimitAt(in RoadAttrRecord a, float real, double alongM)
        {
            return LimitAt(a, real, alongM, false);
        }

        /// <summary>
        /// As <see cref="LimitAt(in RoadAttrRecord, float, double)"/>; with <paramref name="finalCorridor"/> the samples
        /// are the detail-pass final game corridor (RATR written together with RSTR: the pipeline already kept 0.5 m to
        /// the buildings and trimmed every building to it), so no clearance is subtracted and every bounded sample is at
        /// least <see cref="Roads.RoadClearance.MinCorridorM"/> (the runtime floor of decision 1).
        /// </summary>
        public static float LimitAt(in RoadAttrRecord a, float real, double alongM, bool finalCorridor)
        {
            int n = a.CorridorCount;
            if (n == 0) return AreaOf(a) == AreaType.OldCore ? real : float.PositiveInfinity;
            double half = 0.5 * SmoothWindowM;
            int i0 = (int)Math.Ceiling((alongM - half) / RoadAttrRecord.CorridorSpacingM);
            int i1 = (int)Math.Floor((alongM + half) / RoadAttrRecord.CorridorSpacingM);
            if (i0 < 0) i0 = 0;
            if (i1 > n - 1) i1 = n - 1;
            if (i1 < i0)
            {
                int k = (int)Math.Round(alongM / RoadAttrRecord.CorridorSpacingM);
                i0 = i1 = k < 0 ? 0 : k > n - 1 ? n - 1 : k;
            }
            float best = float.PositiveInfinity;
            for (int i = i0; i <= i1; i++)
            {
                int dm = a.CorridorDm[i];
                if (dm <= 0) continue; // open
                float lim = finalCorridor ? Math.Max(dm / 10f, Roads.RoadClearance.MinCorridorM) : dm / 10f - CorridorClearanceM;
                if (lim < best) best = lim;
            }
            return best;
        }

        /// <summary>
        /// Fill <paramref name="p"/> with the piece's widths every <see cref="ProfileStepM"/>: §4.2 per sample from the
        /// corridor limit, with the class × area footpaths and shoulders reserved first (the carriageway widens beyond its
        /// real width only into what the corridor leaves beside them); cut ends (tile borders) pinned to
        /// <see cref="BorderAccessWidthM"/> so both tiles meet exactly; then the 1 : 20 taper as a lower envelope (a width
        /// only ever narrows to meet a constraint), and a 1 : 20 ramp down from each pinned cut end so the end width holds
        /// even where this tile's corridor is tighter than the neighbour's. That is the §4.2 width lanes and access follow
        /// (§4.4, <see cref="RoadWidthProfile.Width"/>). The drawn width (<see cref="RoadWidthProfile.Drawn"/>) is it raised
        /// to the rideability floor (<see cref="RideableMinM"/>, a class constant, so cut ends still agree). Footpaths: the
        /// class × area rule, shrunk to the space the corridor leaves beside the drawn carriageway, tapered at
        /// 1 : <see cref="FootTaperRatio"/> and dropped under 1 m. A dual carriageway keeps its median-side edge at real/2
        /// and widens outwards (<see cref="RoadWidthProfile.ShiftAt"/>), so its width and outer footpath are checked
        /// one-sided against the symmetric corridor: shift + w/2 + footpath ≤ limit/2.
        /// </summary>
        public static void BuildProfile(RoadRecord r, in RoadAttrRecord a, RoadWidthProfile p)
        {
            BuildProfile(r, a, p, false);
        }

        /// <summary>
        /// As <see cref="BuildProfile(RoadRecord, in RoadAttrRecord, RoadWidthProfile)"/>; with
        /// <paramref name="finalCorridor"/> the RATR samples are the detail-pass final game corridor (see
        /// <see cref="LimitAt(in RoadAttrRecord, float, double, bool)"/>): carriageway, footpaths and shoulders share it
        /// without a further clearance (<see cref="UsesFinalCorridor"/> says which a tile carries).
        /// </summary>
        public static void BuildProfile(RoadRecord r, in RoadAttrRecord a, RoadWidthProfile p, bool finalCorridor)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (p == null) throw new ArgumentNullException(nameof(p));
            float real = RealWidthM(r, a);
            float nominal = NominalGameWidthM(r, a);
            float floor = FloorM(r.RoadClass);
            bool tagged = TagPlausible(r);
            double length = RenderedLengthM(r);
            int n = Math.Max(2, (int)Math.Ceiling(length / ProfileStepM) + 1);
            p.Ensure(n);
            p.RealM = real;
            p.NominalM = nominal;
            p.LengthM = (float)length;
            p.StepM = length > 0 ? (float)(length / (n - 1)) : ProfileStepM;
            p.Count = n;
            p.NoCars = false;
            float sh = ShoulderM(r.RoadClass, AreaOf(a));
            float shoulders = 2f * sh;
            float footL, footR;
            NominalFootpaths(r, a, real, out footL, out footR);
            bool dual = a.Has(RoadAttrFlags.Dual);
            p.Dual = dual;
            p.Shift = null;
            float[] w = p.Width;
            for (int i = 0; i < n; i++)
            {
                double s = i * (double)p.StepM;
                float lim = LimitAt(a, real, s, finalCorridor);
                p.Limit[i] = lim;
                // Dual: the outer edge sits at w − real/2 (all the widening goes outwards), so w ≤ (limit + real) / 2.
                bool bounded = !float.IsPositiveInfinity(lim);
                float wl = dual && bounded ? Math.Min(lim, 0.5f * (lim + real)) : lim;
                float g = ClampToLimit(real, nominal, floor, tagged, wl);
                if (bounded)
                {
                    // The footpaths (and shoulders) the class and area give are reserved before the carriageway widens
                    // beyond its real width (Kanti Path, Durbar Marg keep their kerbed pavements): the widening only takes
                    // what the corridor has left; the real width itself is never given up.
                    float room = dual ? 0.5f * (lim + real) - footL - sh : lim - 2f * (Math.Max(footL, footR) + sh);
                    if (g > room) g = Math.Max(room, ClampToLimit(real, real, floor, tagged, wl));
                }
                w[i] = g;
            }
            float border = BorderAccessWidthM(r);
            if (r.HasPrevContext) w[0] = Math.Min(w[0], border);
            if (r.HasNextContext) w[n - 1] = Math.Min(w[n - 1], border);
            Envelope(w, n, p.StepM / TaperRatio);
            // Cut ends meet the border width exactly, whatever this tile's corridor says next to them (the
            // neighbour's samples may differ): raise the end and ramp down from it at 1 : 20.
            float k = p.StepM / TaperRatio;
            for (int i = 0; i < n; i++)
            {
                if (r.HasPrevContext) w[i] = Math.Max(w[i], border - k * i);
                if (r.HasNextContext) w[i] = Math.Max(w[i], border - k * (n - 1 - i));
            }
            float rideable = RideableMinM(r.RoadClass);
            for (int i = 0; i < n; i++) p.Drawn[i] = Math.Max(w[i], rideable);
            for (int i = 0; i < n; i++)
            {
                float lim = p.Limit[i];
                float spaceL, spaceR;
                if (float.IsPositiveInfinity(lim)) spaceL = spaceR = float.PositiveInfinity;
                else if (dual)
                {
                    // Outer (left) edge at w − real/2, median (right) edge at real/2 from the centreline.
                    spaceL = 0.5f * (lim - shoulders) - (p.Drawn[i] - 0.5f * real);
                    spaceR = 0.5f * (lim - shoulders - real);
                }
                else spaceL = spaceR = 0.5f * (lim - p.Drawn[i] - shoulders);
                p.FootLeft[i] = Math.Max(0f, Math.Min(footL, spaceL));
                p.FootRight[i] = Math.Max(0f, Math.Min(footR, spaceR));
            }
            // Footpaths change width along a kerb flare (1 : FootTaperRatio), not the carriageway's 1 : 20 taper, so a
            // pavement is not lost over 20 m either side of every narrow spot.
            float kf = p.StepM / FootTaperRatio;
            Envelope(p.FootLeft, n, kf);
            Envelope(p.FootRight, n, kf);
            for (int i = 0; i < n; i++)
            {
                if (p.FootLeft[i] < MinFootpathM) p.FootLeft[i] = 0f;
                if (p.FootRight[i] < MinFootpathM) p.FootRight[i] = 0f;
            }
        }

        /// <summary>Lower envelope with a maximum slope: v[i] = min_j (v[j] + k·|i − j|).</summary>
        private static void Envelope(float[] v, int n, float k)
        {
            for (int i = 1; i < n; i++)
                if (v[i] > v[i - 1] + k) v[i] = v[i - 1] + k;
            for (int i = n - 2; i >= 0; i--)
                if (v[i] > v[i + 1] + k) v[i] = v[i + 1] + k;
        }

        private static RoadWidthProfile Cached(RoadRecord r, in RoadAttrRecord a)
        {
            RoadWidthProfile p = _scratch ?? (_scratch = new RoadWidthProfile());
            if (!ReferenceEquals(_scratchRoad, r) || !ReferenceEquals(_scratchCorridor, a.CorridorDm) || _scratchArea != a.Area)
            {
                BuildProfile(r, a, p);
                _scratchRoad = r;
                _scratchCorridor = a.CorridorDm;
                _scratchArea = a.Area;
            }
            return p;
        }

        /// <summary>The game carriageway width (as drawn, before any tile-level knee, ring or parallel-piece adjustment of
        /// <see cref="RoadLayout"/>) at <paramref name="alongM"/> metres from the piece's first rendered point. Prefer
        /// <see cref="RoadLayout.Profiles"/> (the tile's final widths) or <see cref="BuildProfile"/> when sampling a piece
        /// many times.</summary>
        public static float GameWidthM(in RoadRecord r, in RoadAttrRecord a, float alongM)
        {
            return Cached(r, a).WidthAt(alongM);
        }

        /// <summary>The widest point of the piece (for spatial-index reach).</summary>
        public static float MaxGameWidthM(RoadRecord r, in RoadAttrRecord a)
        {
            return Cached(r, a).MaxWidth;
        }

        // -------------------------------------------------------------------------------------------------------
        // Footpaths, lanes, paint
        // -------------------------------------------------------------------------------------------------------

        private static float Range(ulong wayId, uint purpose, float lo, float hi)
        {
            uint h = Hash(wayId, purpose);
            return lo + (hi - lo) * (h & 0xFFFF) / 65535f;
        }

        internal static uint Hash(ulong wayId, uint purpose)
        {
            unchecked
            {
                uint h = Hashes.Fnv32Offset;
                for (int i = 0; i < 8; i++) h = (h ^ (byte)(wayId >> (8 * i))) * Hashes.Fnv32Prime;
                for (int i = 0; i < 4; i++) h = (h ^ (byte)(purpose >> (8 * i))) * Hashes.Fnv32Prime;
                return h;
            }
        }

        private const uint PurposeFootpath = 0x46505448; // "FPTH"
        private const uint PurposeFootSides = 0x46534944;

        /// <summary>
        /// Footpath widths in game metres before the corridor fit (roads.md 8.2): a tagged sidewalk wins (SEPARATE and
        /// NONE give none); else trunk 2.5-3.0, primary 2.0-3.5 and secondary or tertiary 1.5-2.0 (60% both sides, 20%
        /// left only, 20% none) in URBAN areas and on the arterials of old cores (Kanti Path); none on old-core lanes
        /// (shared surface) and outside towns. A dual carriageway gets a footpath on its outer (left) side only. Widths are
        /// real × 1.15 (pedestrian scale).
        /// </summary>
        public static void NominalFootpaths(RoadRecord r, in RoadAttrRecord a, float real, out float left, out float right)
        {
            left = right = 0f;
            if (!IsMotor(r.RoadClass) || r.RoadClass == RoadClass.Track || (r.Flags & RoadFlags.Bridge) != 0 && real < 5f) return;
            if (a.Has(RoadAttrFlags.HeritagePedestrian)) return;
            AreaType area = AreaOf(a);
            bool dual = a.Has(RoadAttrFlags.Dual);
            float w;
            switch (r.RoadClass)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk: w = Range(r.OsmWayId, PurposeFootpath, 2.5f, 3.0f); break;
                case RoadClass.Primary: w = Range(r.OsmWayId, PurposeFootpath, 2.0f, 3.5f); break;
                case RoadClass.Secondary:
                case RoadClass.Tertiary: w = Range(r.OsmWayId, PurposeFootpath, 1.5f, 2.0f); break;
                default: w = 1.5f; break;
            }
            w *= PedestrianScale;
            switch (a.SidewalkKind)
            {
                case Data.Sidewalk.None:
                case Data.Sidewalk.Separate: return;
                case Data.Sidewalk.Left:
                    left = w;
                    return;
                case Data.Sidewalk.Right:
                    right = w;
                    break;
                case Data.Sidewalk.Both:
                    left = right = w;
                    break;
                default:
                {
                    if (!(area == AreaType.Urban && IsMajor(r.RoadClass) || area == AreaType.OldCore && IsArterial(r.RoadClass))) return;
                    if (r.RoadClass == RoadClass.Secondary || r.RoadClass == RoadClass.Tertiary)
                    {
                        uint pick = Hash(r.OsmWayId, PurposeFootSides) % 100;
                        if (pick < 60) left = right = w;
                        else if (pick < 80) left = w;
                    }
                    else
                    {
                        left = right = w;
                    }
                    break;
                }
            }
            if (dual) right = 0f; // the median side
        }

        /// <summary>Lane count: the tag (or RATR forward + backward), else <c>clamp(round(w / 3.5), 1, 6)</c> for trunk
        /// to secondary and <c>round(w / 3.0)</c> below; a two-way road under 5.5 m real is one shared lane.</summary>
        public static byte LanesFor(RoadRecord r, in RoadAttrRecord a, float real)
        {
            int lanes = a.LanesFwd + a.LanesBwd;
            if (lanes == 0) lanes = r.Lanes;
            if (lanes == 0)
            {
                bool big = r.RoadClass == RoadClass.Motorway || r.RoadClass == RoadClass.Trunk || r.RoadClass == RoadClass.Primary ||
                           r.RoadClass == RoadClass.Secondary;
                lanes = big ? (int)Math.Round(real / 3.5, MidpointRounding.AwayFromZero) : (int)Math.Round(real / 3.0, MidpointRounding.AwayFromZero);
                lanes = lanes < 1 ? 1 : lanes > 6 ? 6 : lanes;
            }
            if (!Oneway(r) && real < 5.5f) lanes = 1;
            return (byte)lanes;
        }

        private static bool Sealed(Surface s)
        {
            return s == Surface.Asphalt || s == Surface.Concrete;
        }

        /// <summary>The cross-section at <paramref name="alongM"/> (W2_DESIGN 4.5).</summary>
        public static RoadProfile ProfileAt(in RoadRecord r, in RoadAttrRecord a, float alongM)
        {
            RoadWidthProfile wp = Cached(r, a);
            return ProfileFrom(r, a, wp, alongM);
        }

        /// <summary><see cref="ProfileAt"/> from an already built width profile.</summary>
        public static RoadProfile ProfileFrom(RoadRecord r, in RoadAttrRecord a, RoadWidthProfile wp, double alongM)
        {
            float real = wp.RealM;
            float w = wp.WidthAt(alongM);
            AreaType area = AreaOf(a);
            bool oneway = Oneway(r), dual = a.Has(RoadAttrFlags.Dual);
            var p = new RoadProfile
            {
                CarriagewayM = w, RealM = real, ShoulderM = ShoulderM(r.RoadClass, area),
                FootpathLeftM = wp.Sample(wp.FootLeft, alongM), FootpathRightM = wp.Sample(wp.FootRight, alongM),
            };
            p.KerbLeftM = p.FootpathLeftM > 0 ? KerbTopM : 0f;
            p.KerbRightM = p.FootpathRightM > 0 ? KerbTopM : 0f;
            if (dual) p.MedianM = Math.Max(MinMedianM, a.MedianCm / 100f);
            p.CentreShiftM = wp.ShiftAt(alongM);
            byte lanes = LanesFor(r, a, real);
            p.Lanes = lanes;
            if (oneway)
            {
                p.LanesFwd = lanes;
                p.LanesBwd = 0;
            }
            else if (lanes == 1)
            {
                p.LanesFwd = p.LanesBwd = 1; // one shared lane
            }
            else
            {
                p.LanesFwd = a.LanesFwd > 0 ? a.LanesFwd : (byte)((lanes + 1) / 2);
                p.LanesBwd = a.LanesBwd > 0 ? a.LanesBwd : (byte)(lanes / 2);
            }
            bool paintable = a.Has(RoadAttrFlags.Paintable) || real >= 5.5f && Sealed(r.Surface);
            bool motor = IsMotor(r.RoadClass) && r.RoadClass != RoadClass.Track;
            bool sharedStreet = area == AreaType.OldCore && !IsArterial(r.RoadClass);
            p.CentreLine = motor && paintable && !oneway && lanes >= 2 && !sharedStreet;
            p.LaneLines = motor && paintable && (dual || oneway && lanes >= 2 || lanes >= 3);
            p.EdgeLines = motor && paintable && !sharedStreet &&
                          (r.RoadClass == RoadClass.Motorway || r.RoadClass == RoadClass.Trunk || r.RoadClass == RoadClass.Primary ||
                           r.RoadClass == RoadClass.Secondary);
            p.DrawnM = wp.DrawnAt(alongM);
            p.DrawnShiftM = p.CentreShiftM;
            p.Access = AccessForWidth(wp.AccessWidthAt(alongM), r, a);
            if (wp.NoCars) p.Access &= ~(Travel.Car | Travel.Jeep | Travel.Bus);
            return p;
        }

        // -------------------------------------------------------------------------------------------------------
        // Access (§4.4)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Travel modes a game width allows (§4.4): &lt; 1.8 m walk; to 3.5 m + bicycle and two-wheelers; to
        /// 4.5 m + car; to 6.0 m + jeep, micro, tempo and small trucks; from 6.0 m everything.</summary>
        public static Travel WidthMask(float gameWidthM)
        {
            Travel t = Travel.Foot | Travel.Horse;
            if (gameWidthM >= 1.8f) t |= Travel.Bicycle | Travel.Motorbike;
            if (gameWidthM >= 3.5f) t |= Travel.Car;
            if (gameWidthM >= 4.5f) t |= Travel.Jeep;
            if (gameWidthM >= 6.0f) t |= Travel.Bus;
            return t;
        }

        /// <summary>Smallest turning radius along the piece (circumradius of consecutive point triples, smoothed by
        /// skipping points closer than 2 m); +∞ for straight pieces.</summary>
        public static float MinRadiusM(RoadRecord r)
        {
            int[] p = r.Points;
            int n = p.Length / 2;
            double best = double.PositiveInfinity;
            int a = 0;
            for (int b = 1; b < n - 1; b++)
            {
                if (Dist(p, a, b) < 2.0) continue;
                int c = b + 1;
                while (c < n - 1 && Dist(p, b, c) < 2.0) c++;
                double ab = Dist(p, a, b), bc = Dist(p, b, c), ca = Dist(p, c, a);
                double cr = ((p[2 * b] - p[2 * a]) / 100.0) * ((p[2 * c + 1] - p[2 * a + 1]) / 100.0) -
                            ((p[2 * b + 1] - p[2 * a + 1]) / 100.0) * ((p[2 * c] - p[2 * a]) / 100.0);
                if (Math.Abs(cr) > 1e-6)
                {
                    double rad = ab * bc * ca / (2 * Math.Abs(cr));
                    if (rad < best) best = rad;
                }
                a = b;
            }
            return (float)best;
        }

        private static double Dist(int[] p, int i, int j)
        {
            double dx = (p[2 * j] - p[2 * i]) / 100.0, dz = (p[2 * j + 1] - p[2 * i + 1]) / 100.0;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>No bus on a way whose smoothed hairpin apex radius is under this.</summary>
        public const float MinBusRadiusM = 12f;

        /// <summary>
        /// Who may use the piece (§4.4): the width mask of the access width (the §4.2 width before the rideability floor,
        /// at most <paramref name="gameWidthM"/>; a galli drawn 4.8 m wide for three motorbikes still keeps cars out),
        /// intersected with the record's OSM access; heritage squares, pedestrian streets and
        /// <c>access=no</c>/<c>motor_vehicle=no</c> are walk-and-cycle only; no bus on hairpins under 12 m. Compounds are
        /// excluded separately by the sacred-zone index. Pass the structure record too where the tile has one
        /// (<see cref="AccessFor(float, in RoadRecord, in RoadAttrRecord, in RoadStructureRecord)"/>).
        /// </summary>
        public static Travel AccessFor(float gameWidthM, in RoadRecord r, in RoadAttrRecord a)
        {
            float access = Math.Min(gameWidthM, Cached(r, a).MinAccessWidth);
            return AccessForWidth(access, r, a);
        }

        /// <summary>As <see cref="AccessFor(float, in RoadRecord, in RoadAttrRecord)"/>, and no car, jeep or bus where the
        /// structure record says a car does not fit (decision 5: gallis, real width under about 3 m, motorcar=no).</summary>
        public static Travel AccessFor(float gameWidthM, in RoadRecord r, in RoadAttrRecord a, in RoadStructureRecord s)
        {
            Travel t = AccessFor(gameWidthM, r, a);
            if (!s.Has(RoadStructureFlags.CarAccessible)) t &= ~(Travel.Car | Travel.Jeep | Travel.Bus);
            return t;
        }

        /// <summary>The §4.4 rules for a known access width.</summary>
        internal static Travel AccessForWidth(float accessWidthM, in RoadRecord r, in RoadAttrRecord a)
        {
            Travel t = WidthMask(accessWidthM);
            if (a.Has(RoadAttrFlags.HeritagePedestrian) || a.Has(RoadAttrFlags.NoMotor) || r.RoadClass == RoadClass.Pedestrian)
                t &= Travel.Foot | Travel.Bicycle | Travel.Horse;
            if ((t & Travel.Bus) != 0 && MinRadiusM(r) < MinBusRadiusM) t &= ~Travel.Bus;
            if (r.Access != Travel.None) t &= r.Access;
            return t;
        }
    }
}
