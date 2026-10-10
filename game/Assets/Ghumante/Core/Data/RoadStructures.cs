using System;

namespace Ghumante.Core.Data
{
    /// <summary>
    /// What a road piece is built as (W2 detail pass, docs/W2_DETAIL_CONTRACT.md §3). Written by the pipeline
    /// (Track DATA, chunk documented in DATA_FORMATS.md); absent chunks mean <see cref="None"/> for every road.
    /// </summary>
    public enum RoadStructureKind : byte
    {
        /// <summary>Draped on the terrain (the default).</summary>
        None = 0,
        /// <summary>Crosses water: deck, kerbs, railings on both sides, abutments, piers on long spans.</summary>
        Bridge = 1,
        /// <summary>Crosses another road or rail: deck, ramps, piers, railings (flyover / overpass).</summary>
        Flyover = 2,
        /// <summary>Passes under a bridge or flyover: lowered profile so the clearance holds.</summary>
        Underpass = 3,
        /// <summary>Tunnel (not drawn in W2; kept out of car routes).</summary>
        Tunnel = 4,
        /// <summary>Ford through shallow water.</summary>
        Ford = 5,
    }

    /// <summary>Per-road access and construction flags (W2 detail pass).</summary>
    [Flags]
    public enum RoadStructureFlags : byte
    {
        None = 0,
        /// <summary>A car fits (real width or lanes allow it, not footway/path/steps/pedestrian, not motorcar=no).</summary>
        CarAccessible = 1,
        /// <summary>The piece crosses a waterway line or water area.</summary>
        WaterCrossing = 2,
        /// <summary>The deck heights come from OSM layer/bridge tags (otherwise inferred from terrain and water).</summary>
        DeckFromTags = 4,
        /// <summary>Pedestrian overhead bridge (footway on a deck over a road).</summary>
        FootOverbridge = 8,
    }

    /// <summary>
    /// One record per <see cref="TileData.Roads"/> entry, same order (like RATR). <see cref="DeckY"/> gives the
    /// absolute deck surface height in game metres at every road point (null when the road is draped).
    /// </summary>
    public struct RoadStructureRecord
    {
        public RoadStructureKind Kind;
        public sbyte Layer;
        public RoadStructureFlags Flags;
        /// <summary>Free height above this road's surface (metres); 0 = unlimited / unknown.</summary>
        public float ClearanceM;
        /// <summary>Railing height for bridges and flyovers (metres); 0 = none.</summary>
        public float RailingHeightM;
        /// <summary>Absolute deck height per road point (game metres), or null when draped on the terrain.</summary>
        public float[] DeckY;

        public static RoadStructureRecord Absent
        {
            get { return new RoadStructureRecord { Flags = RoadStructureFlags.CarAccessible }; }
        }

        public bool Has(RoadStructureFlags flag)
        {
            return (Flags & flag) != 0;
        }

        public bool IsElevated
        {
            get { return DeckY != null && (Kind == RoadStructureKind.Bridge || Kind == RoadStructureKind.Flyover); }
        }
    }
}
