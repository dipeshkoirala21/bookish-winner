namespace Ghumante.Core.Data
{
    // W2 tile records (W2_DESIGN.md 9.3 and 10.3; byte layouts as pipeline tile_format.py writes them): RATR road
    // attributes, JNCT junctions, BFNT building fronts and PROP real point objects. Every chunk is optional: a W1
    // pack has none of them and the lists stay empty. The flag fields keep the contract's byte type; use Has() with
    // the generated [Flags] enums.

    /// <summary>
    /// Attributes of one ROAD record (RATR, same order as ROAD). <see cref="CorridorDm"/> holds the corridor samples
    /// every 20 m from the piece's first rendered point, in decimetres. In a W2 detail-pass tile
    /// (<see cref="TileData.FinalCorridors"/>, META <c>"ratr_corridor": "final"</c>) each sample is the road's final
    /// game corridor (DATA_FORMATS.md 1.11): the full width of the clear band the carriageway, shoulders and
    /// footpaths are drawn in, at least <c>RoadClearance.MinCorridorM</c> 4.8 m (narrower only where SQUEEZED between
    /// protected footprints), the minimum over ±20 m around the sample, never 0, with no further clearance to
    /// subtract; buildings were trimmed back to it. In older tiles it is the stage-1 measurement
    /// <c>2 × min(dLeft, dRight)</c> to the first building, 0 = open. Empty when unknown; never null in decoded data.
    /// </summary>
    public struct RoadAttrRecord
    {
        public AreaType Area;
        public byte Sidewalk;
        public byte LanesFwd;
        public byte LanesBwd;
        public byte MaxspeedKmh;
        public byte Flags;
        public long PartnerWayId;
        public int MedianCm;
        public int[] CorridorDm;

        /// <summary>Spacing of <see cref="CorridorDm"/> samples along the piece.</summary>
        public const float CorridorSpacingM = 20f;

        public bool Has(RoadAttrFlags flag)
        {
            return (Flags & (byte)flag) != 0;
        }

        public global::Ghumante.Core.Data.Sidewalk SidewalkKind
        {
            get { return (global::Ghumante.Core.Data.Sidewalk)Sidewalk; }
        }

        public int CorridorCount
        {
            get { return CorridorDm == null ? 0 : CorridorDm.Length; }
        }
    }

    /// <summary>One junction (JNCT): roundabouts, signals, police chowks and synthetic islands. Centre in local
    /// centimetres from the tile's south-west corner; diameters 0 = none (or computed at runtime, W2_DESIGN 4.7).</summary>
    public struct JunctionRecord
    {
        public long OsmNodeId;
        public JunctionKind Kind;
        public byte Arms;
        public byte Flags;
        public int XCm;
        public int ZCm;
        public int RingDiameterCm;
        public int IslandDiameterCm;

        /// <summary><c>(osm_id &lt;&lt; 1) | is_relation</c> of the AREA island, 0 = none.</summary>
        public long IslandAreaRef;

        public int NameRef;

        public bool Has(JunctionFlags flag)
        {
            return (Flags & (byte)flag) != 0;
        }
    }

    /// <summary>The street front of one BLDG record (BFNT, same order as BLDG). <see cref="FrontEdge"/> indexes ring-0
    /// edges (edge i runs from point i to point i + 1); <see cref="NoEdge"/> = none.</summary>
    public struct BuildingFrontRecord
    {
        /// <summary><see cref="FrontEdge"/> / <see cref="SecondEdge"/> value for "no edge".</summary>
        public const byte NoEdge = 255;

        /// <summary><see cref="ShopBays"/> bit 7: the shop count comes from shop POIs inside the footprint.</summary>
        public const byte ShopFromPoi = 0x80;

        public StyleProfile Profile;
        public AreaType Area;
        public byte FrontEdge;
        public byte FrontDistDm;
        public byte ShopBays;
        public byte Flags;
        public byte SecondEdge;

        /// <summary>The record used when a tile has no BFNT: no profile, no front edge.</summary>
        public static BuildingFrontRecord Absent
        {
            get { return new BuildingFrontRecord { FrontEdge = NoEdge, SecondEdge = NoEdge }; }
        }

        public bool HasFront
        {
            get { return FrontEdge != NoEdge; }
        }

        /// <summary>Shop count from <see cref="ShopBays"/> bits 0-3.</summary>
        public int ShopCount
        {
            get { return ShopBays & 0x0F; }
        }

        public bool Has(BuildingFrontFlags flag)
        {
            return (Flags & (byte)flag) != 0;
        }
    }

    /// <summary>
    /// One real OSM point object from PROP (CONTENT_COVERAGE D3). <see cref="OsmRef"/> is <c>(osm_id &lt;&lt; 2) |
    /// type</c>; <see cref="Subtype"/> is the <see cref="TreeClass"/> of a tree; <see cref="YawCdeg"/> is the bearing
    /// clockwise from north in 1/100 degree, valid with <see cref="PropFlags.Yaw"/>.
    /// </summary>
    public struct PropRecord
    {
        public ulong OsmRef;
        public ObjectKind Kind;
        public byte Subtype;
        public PropFlags Flags;
        public int XCm;
        public int ZCm;
        public ushort YawCdeg;
        public int HeightDm;
        public int NameRef;
        public int RefRef;

        public bool Has(PropFlags flag)
        {
            return (Flags & flag) != 0;
        }

        public TreeClass Tree
        {
            get { return Kind == ObjectKind.Tree ? (TreeClass)Subtype : TreeClass.Unknown; }
        }

        /// <summary>Yaw in degrees clockwise from north (0 without <see cref="PropFlags.Yaw"/>).</summary>
        public float YawDeg
        {
            get { return Has(PropFlags.Yaw) ? YawCdeg / 100f : 0f; }
        }
    }
}
