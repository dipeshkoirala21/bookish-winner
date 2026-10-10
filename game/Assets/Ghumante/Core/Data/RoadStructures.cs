namespace Ghumante.Core.Data
{
    // RoadStructureKind and RoadStructureFlags are generated into Enums.cs from shared/enums.json (pipeline
    // model.py, ENUMS_VERSION 4): Kind None, Bridge, Flyover, Underpass, Tunnel, Ford, Passage (a way under a
    // building that stays intact: mesh a gateway, never trim the house); Flags CarAccessible, WaterCrossing,
    // DeckFromTags, FootOverbridge, Lowered, Approach, Squeezed, OverRoad.

    /// <summary>What one road point stands on (RSTR deck codes, DATA_FORMATS.md 1.15).</summary>
    public enum DeckPointRole : byte
    {
        /// <summary>On the terrain (<see cref="RoadStructureRecord.DeckY"/> is NaN there).</summary>
        Draped = 0,

        /// <summary>On a structure deck: a bridge or flyover span (piers, railings, kerbs belong here).</summary>
        Deck = 1,

        /// <summary>On an approach ramp: an embankment rising to a deck, or a cutting down to a lowered underpass
        /// (retaining walls, no piers). Foot structures climb these by stairs.</summary>
        Ramp = 2,
    }

    /// <summary>
    /// How one road piece is built (RSTR chunk, docs/DATA_FORMATS.md 1.15; W2 detail pass, docs/W2_DETAIL_CONTRACT.md
    /// decisions 3-5). One record per <see cref="TileData.Roads"/> entry, same order (like RATR); a tile without the
    /// chunk reads <see cref="Absent"/> for every road. <see cref="DeckY"/> gives the absolute surface height in game
    /// metres at every road point (context points included) where the road leaves the terrain, NaN where it is draped,
    /// and is null when the whole piece is draped. Pipeline rules: decks clear water by the kind's clearance (rivers
    /// 3 m above the surface) and every road below by 5.5 m (RoadClearance.MinUnderpassClearanceM) plus
    /// <see cref="DeckDepthM"/> (<see cref="FootDeckDepthM"/> for foot decks, <see cref="DeckDepthFor"/>) under the
    /// deck's whole width; heights never step: along the ground the offset from the terrain changes by at most the
    /// class's grade per metre (5 % trunk and primary, 6 % secondary and tertiary, 8 % minor roads, 10 % tracks, 50 %
    /// stairs on foot ways; up to <see cref="SteepestRampGrade"/> on the few connectors between a riverside cutting
    /// and a bridge approach), on decks the height does, and roads meeting at a node agree there.
    /// </summary>
    public struct RoadStructureRecord
    {
        /// <summary>Depth from a vehicle deck's surface to its underside assumed for clearances (metres). The bridges
        /// package keeps the deck structure within it so every underpass keeps its clearance.</summary>
        public const float DeckDepthM = 1.2f;

        /// <summary>Deck depth of a foot deck (foot overbridges, foot bridges over water), metres.</summary>
        public const float FootDeckDepthM = 0.6f;

        /// <summary>The steepest ramp grade (rise / run) the pipeline writes on a motor road (pipeline
        /// <c>structures.STEEP_GRADE</c>): connectors between a riverside cutting and a bridge approach that the class
        /// grade cannot close.</summary>
        public const float SteepestRampGrade = 0.15f;

        /// <summary>What the piece is built as (the dominant structure along its rendered length).</summary>
        public RoadStructureKind Kind;

        /// <summary>Effective layer: the OSM layer, else +1 for a bridge and -1 for a tunnel.</summary>
        public sbyte Layer;

        /// <summary>Access and construction flags (car access, water, tags, foot overbridge, lowered, approach...).</summary>
        public RoadStructureFlags Flags;

        /// <summary>Free height above this road's surface (metres): for an underpass the lowest deck underside above
        /// it (at least RoadClearance.MinUnderpassClearanceM over the deck's whole width); for a
        /// <see cref="RoadStructureKind.Passage"/> the free height of the gateway under the building that stays
        /// intact (at least RoadClearance.MinOverheadClearanceM, or the building's tagged min_height); 0 = unlimited /
        /// unknown.</summary>
        public float ClearanceM;

        /// <summary>Railing height for bridges and flyovers (metres); 0 = none.</summary>
        public float RailingHeightM;

        /// <summary>Absolute surface height per road point (game metres), NaN where draped; null when the piece is
        /// draped everywhere.</summary>
        public float[] DeckY;

        /// <summary>What each road point stands on (same length as <see cref="DeckY"/>); null with it.</summary>
        public DeckPointRole[] DeckRole;

        /// <summary>Lateral corridor shift every <see cref="ShiftSpacingM"/> metres from the piece's first rendered
        /// point (centimetres, positive = left of the point order): the corridor moves away from a protected (hero
        /// or temple) footprint there. Each sample is the largest shift within one spacing, so reading it with
        /// <see cref="ShiftAtM"/> never shifts less than the pipeline's band. Null = none.</summary>
        public int[] CorridorShiftCm;

        /// <summary>Spacing of <see cref="CorridorShiftCm"/> (metres).</summary>
        public const double ShiftSpacingM = 5.0;

        /// <summary>True when <paramref name="shifts"/> samples every <see cref="ShiftSpacingM"/> fit a piece with
        /// <paramref name="corridorSamples"/> RATR samples (both start at the first rendered point and cover the
        /// rendered length): 0, or 4 (corridorSamples - 1) + 1 to 4 corridorSamples.</summary>
        public static bool ShiftCountFits(int shifts, int corridorSamples)
        {
            return shifts == 0 || (corridorSamples >= 1 && shifts >= 4 * (corridorSamples - 1) + 1 && shifts <= 4 * corridorSamples);
        }

        /// <summary>The record of a road in a tile without RSTR: draped, car-accessible, no shift.</summary>
        public static RoadStructureRecord Absent
        {
            get { return new RoadStructureRecord { Flags = RoadStructureFlags.CarAccessible }; }
        }

        /// <summary>True when <paramref name="flag"/> is set.</summary>
        public bool Has(RoadStructureFlags flag)
        {
            return (Flags & flag) != 0;
        }

        /// <summary>A bridge or flyover deck with heights.</summary>
        public bool IsElevated
        {
            get { return DeckY != null && (Kind == RoadStructureKind.Bridge || Kind == RoadStructureKind.Flyover); }
        }

        /// <summary>True when any point of the piece leaves the terrain (deck, ramp or lowered cutting).</summary>
        public bool HasHeights
        {
            get { return DeckY != null; }
        }

        /// <summary>True when the corridor is shifted somewhere along the piece.</summary>
        public bool HasShift
        {
            get { return CorridorShiftCm != null && CorridorShiftCm.Length > 0; }
        }

        /// <summary>The deck depth that applies to this record when the road class is unknown (foot overbridges are
        /// thinner); prefer <see cref="DeckDepthFor"/>.</summary>
        public float DeckDepth
        {
            get { return Has(RoadStructureFlags.FootOverbridge) ? FootDeckDepthM : DeckDepthM; }
        }

        /// <summary>The deck depth the pipeline reserved under this road's deck: <see cref="FootDeckDepthM"/> for foot
        /// decks (footway, path, steps, cycleway, bridleway and pedestrian-street classes, over water too, and every
        /// <see cref="RoadStructureFlags.FootOverbridge"/>), else <see cref="DeckDepthM"/>.</summary>
        public float DeckDepthFor(RoadClass roadClass)
        {
            return Has(RoadStructureFlags.FootOverbridge) || IsFootDeckClass(roadClass) ? FootDeckDepthM : DeckDepthM;
        }

        /// <summary>True for the classes whose decks are foot decks (thinner deck, 1.3 m railing; pipeline
        /// <c>structures.FOOT_DECK_CLASSES</c>).</summary>
        public static bool IsFootDeckClass(RoadClass c)
        {
            return c == RoadClass.Footway || c == RoadClass.Path || c == RoadClass.Steps || c == RoadClass.Cycleway ||
                   c == RoadClass.Bridleway || c == RoadClass.Pedestrian;
        }

        /// <summary>The role of road point <paramref name="point"/> (Draped when the piece has no heights).</summary>
        public DeckPointRole RoleAt(int point)
        {
            return DeckRole == null || (uint)point >= (uint)DeckRole.Length ? DeckPointRole.Draped : DeckRole[point];
        }

        /// <summary>The absolute surface height at road point <paramref name="point"/>; false where it is draped.</summary>
        public bool TryHeightAt(int point, out float y)
        {
            if (DeckY == null || (uint)point >= (uint)DeckY.Length || float.IsNaN(DeckY[point]))
            {
                y = 0f;
                return false;
            }
            y = DeckY[point];
            return true;
        }

        /// <summary>Corridor shift (metres, positive = left) at <paramref name="alongM"/> metres from the piece's first
        /// rendered point, interpolated between the <see cref="ShiftSpacingM"/> samples (0 without a shift).</summary>
        public float ShiftAtM(double alongM)
        {
            int[] s = CorridorShiftCm;
            if (s == null || s.Length == 0) return 0f;
            double f = alongM / ShiftSpacingM;
            if (f <= 0) return s[0] / 100f;
            int i = (int)f;
            if (i >= s.Length - 1) return s[s.Length - 1] / 100f;
            double t = f - i;
            return (float)((s[i] + (s[i + 1] - s[i]) * t) / 100.0);
        }
    }
}
