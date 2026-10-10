namespace Ghumante.Core.Data
{
    // RoadStructureKind and RoadStructureFlags are generated into Enums.cs from shared/enums.json (pipeline
    // model.py, ENUMS_VERSION 4): Kind None, Bridge, Flyover, Underpass, Tunnel, Ford; Flags CarAccessible,
    // WaterCrossing, DeckFromTags, FootOverbridge, Lowered, Approach, Squeezed, OverRoad.

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
    /// <see cref="DeckDepthM"/> (<see cref="FootDeckDepthM"/> for foot decks); ramps never exceed the class's grade
    /// (5 % trunk and primary, 6 % secondary and tertiary, 8 % minor roads, 10 % tracks, 50 % stairs on foot ways).
    /// </summary>
    public struct RoadStructureRecord
    {
        /// <summary>Depth from a vehicle deck's surface to its underside assumed for clearances (metres). The bridges
        /// package keeps the deck structure within it so every underpass keeps its clearance.</summary>
        public const float DeckDepthM = 1.2f;

        /// <summary>Deck depth of a foot deck (foot overbridges, foot bridges over water), metres.</summary>
        public const float FootDeckDepthM = 0.6f;

        /// <summary>What the piece is built as (the dominant structure along its rendered length).</summary>
        public RoadStructureKind Kind;

        /// <summary>Effective layer: the OSM layer, else +1 for a bridge and -1 for a tunnel.</summary>
        public sbyte Layer;

        /// <summary>Access and construction flags (car access, water, tags, foot overbridge, lowered, approach...).</summary>
        public RoadStructureFlags Flags;

        /// <summary>Free height above this road's surface (metres): for an underpass the lowest deck underside above
        /// it; 0 = unlimited / unknown.</summary>
        public float ClearanceM;

        /// <summary>Railing height for bridges and flyovers (metres); 0 = none.</summary>
        public float RailingHeightM;

        /// <summary>Absolute surface height per road point (game metres), NaN where draped; null when the piece is
        /// draped everywhere.</summary>
        public float[] DeckY;

        /// <summary>What each road point stands on (same length as <see cref="DeckY"/>); null with it.</summary>
        public DeckPointRole[] DeckRole;

        /// <summary>Lateral corridor shift at every RATR corridor sample (centimetres, positive = left of the point
        /// order): the corridor moves away from a protected (hero or temple) footprint there. Null = none.</summary>
        public int[] CorridorShiftCm;

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

        /// <summary>The deck depth that applies to this record (foot decks are thinner).</summary>
        public float DeckDepth
        {
            get { return Has(RoadStructureFlags.FootOverbridge) ? FootDeckDepthM : DeckDepthM; }
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
        /// rendered point, interpolated between the 20 m RATR samples (0 without a shift).</summary>
        public float ShiftAtM(double alongM)
        {
            int[] s = CorridorShiftCm;
            if (s == null || s.Length == 0) return 0f;
            double f = alongM / RoadAttrRecord.CorridorSpacingM;
            if (f <= 0) return s[0] / 100f;
            int i = (int)f;
            if (i >= s.Length - 1) return s[s.Length - 1] / 100f;
            double t = f - i;
            return (float)((s[i] + (s[i + 1] - s[i]) * t) / 100.0);
        }
    }
}
