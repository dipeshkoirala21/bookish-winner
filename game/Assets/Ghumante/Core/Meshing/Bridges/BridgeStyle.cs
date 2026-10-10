using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>How the two edges of a deck are guarded (docs/research/w2/bridges_flyovers.md §4).</summary>
    public enum RailingStyle : byte
    {
        /// <summary>RCC post-and-rail balustrade: a plinth, square posts with caps every 2.4 m and three rounded
        /// rails, whitewashed with black-and-yellow posts (most older valley river bridges).</summary>
        ConcreteRail = 0,

        /// <summary>RCC posts with two or three painted steel pipe rails (newer district and city bridges).</summary>
        PipeRail = 1,

        /// <summary>A rounded concrete crash parapet with a steel pipe handrail on top (flyovers, Ring Road and
        /// Araniko Highway bridges).</summary>
        CrashBarrier = 2,

        /// <summary>A painted steel truss or post frame with mesh panels (foot overbridges and steel footbridges).</summary>
        SteelTruss = 3,

        /// <summary>Heritage footbridge: brick parapet with a carved wooden top rail, turned balusters and stone
        /// finials on the posts.</summary>
        Newar = 4,
    }

    /// <summary>
    /// Dimensions and colours of the bridge structures (docs/research/w2/bridges_flyovers.md). Depths are the
    /// structure below the deck surface: the data package sets <c>DeckY</c> at least
    /// <c>lower road + RoadClearance.MinUnderpassClearanceM + depth</c> over a road; where it does not, the mesher
    /// thins the structure down to <see cref="MinDepthM"/> to keep the clearance.
    /// </summary>
    public static class BridgeStyle
    {
        /// <summary>Deck surface to soffit of a road bridge (RCC T-girders or slab).</summary>
        public const float RoadDepthM = 1.3f;

        /// <summary>Deck surface to soffit of a flyover (box girder).</summary>
        public const float FlyoverDepthM = 1.5f;

        /// <summary>Deck surface to soffit of a footbridge or foot overbridge (steel box).</summary>
        public const float FootDepthM = 0.55f;

        /// <summary>The structure never gets thinner than this where the clearance squeezes it.</summary>
        public const float MinDepthM = 0.45f;

        /// <summary>Height of the raised walkway (safety kerb) above the carriageway.</summary>
        public const float KerbHeightM = 0.22f;

        /// <summary>Width of a safety kerb where the road has no footpath on the bridge.</summary>
        public const float SafetyKerbM = 0.6f;

        /// <summary>Railing base (plinth or parapet) width at the deck edge.</summary>
        public const float RailBaseM = 0.32f;

        /// <summary>Deck slab overhang beyond the railing base.</summary>
        public const float OverhangM = 0.08f;

        /// <summary>Default railing height above the walkway.</summary>
        public const float RailHeightM = 1.1f;

        /// <summary>A step up a body may take onto a deck (for <see cref="IBridgeDeckQuery.TryDeck"/>).</summary>
        public const float StepUpM = 0.6f;

        /// <summary>Open spans longer than this get piers.</summary>
        public const float PierFreeSpanM = 15f;

        /// <summary>Target span between piers of a river bridge (RCC girders).</summary>
        public const float RiverSpanM = 21f;

        /// <summary>Target span between piers of a flyover.</summary>
        public const float FlyoverSpanM = 25f;

        /// <summary>A flyover ramp is solid fill between retaining walls up to this height above the ground.</summary>
        public const float FillMaxM = 4.2f;

        /// <summary>Spacing of the lamp posts along one side (they alternate sides on narrow decks).</summary>
        public const float LampSpacingM = 30f;

        /// <summary>Foot overbridge stair geometry (NRS / IRC pedestrian stairs).</summary>
        public const float StairRiseM = 0.165f, StairTreadM = 0.30f;

        /// <summary>Steps per flight before a landing.</summary>
        public const int StairsPerFlight = 14;

        /// <summary>Clear width of a foot overbridge deck when the way has no plausible width.</summary>
        public const float FootOverbridgeWidthM = 2.6f;

        /// <summary>A foot-class deck steeper than this is drawn as steps.</summary>
        public const float StairGrade = 0.2f;

        // Colours (0xRRGGBBAA). Concrete weathers to a warm grey in the valley; railings are whitewashed with the
        // black-and-yellow traffic bands; steel is painted municipal blue or galvanised.
        public static readonly uint Concrete = MeshColor.FromHex(0xB9B4A8);
        public static readonly uint ConcreteDark = MeshColor.FromHex(0x8F8A80);
        public static readonly uint ConcreteSoffit = MeshColor.FromHex(0x9C978C);
        public static readonly uint Whitewash = MeshColor.FromHex(0xEDEBE2);
        public static readonly uint TrafficYellow = MeshColor.FromHex(0xF2C230);
        public static readonly uint TrafficBlack = MeshColor.FromHex(0x2B2B2D);
        public static readonly uint SteelBlue = MeshColor.FromHex(0x3F72A8);
        public static readonly uint SteelGreen = MeshColor.FromHex(0x3E7F5B);
        public static readonly uint SteelRed = MeshColor.FromHex(0xB0413E);
        public static readonly uint Galvanised = MeshColor.FromHex(0xA9B1B6);
        public static readonly uint LampGrey = MeshColor.FromHex(0x6E747A);
        public static readonly uint LampHead = MeshColor.FromHex(0xF4F1E2);
        public static readonly uint Stone = MeshColor.FromHex(0x8E8A80);
        public static readonly uint StoneLight = MeshColor.FromHex(0xA8A296);
        public static readonly uint Brick = MeshColor.FromHex(0xA4553A);
        public static readonly uint Wood = MeshColor.FromHex(0x7A4A2A);
        public static readonly uint Asphalt = MeshColor.FromHex(0x4A4D52);
        public static readonly uint DeckPaver = MeshColor.FromHex(0x9C9A94);
        public static readonly uint RoofBlue = MeshColor.FromHex(0x4C86C6);
        public static readonly uint RoofGreen = MeshColor.FromHex(0x5E9E6E);

        /// <summary>Deterministic hash of a way and a purpose (FNV-1a 32 over the little-endian bytes, as
        /// <see cref="Hashes.Fnv1a32"/>), without allocating.</summary>
        public static uint Hash(ulong wayId, uint purpose)
        {
            uint h = Hashes.Fnv32Offset;
            unchecked
            {
                for (int i = 0; i < 8; i++) h = (h ^ (byte)(wayId >> (8 * i))) * Hashes.Fnv32Prime;
                for (int i = 0; i < 4; i++) h = (h ^ (byte)(purpose >> (8 * i))) * Hashes.Fnv32Prime;
            }
            return h;
        }

        /// <summary>Foot-class roads (footway, path, steps, cycleway, bridleway, pedestrian street).</summary>
        public static bool IsFoot(RoadClass c)
        {
            return RoadWidthModel.IsFootClass(c) || c == RoadClass.Pedestrian;
        }

        /// <summary>The railing a span gets: crash barriers on flyovers and expressway bridges, steel trusses on foot
        /// overbridges, Newar heritage rails on footbridges in heritage zones or with brick or stone decks, and on
        /// other bridges RCC post-and-rail or pipe rails by a hash of the way.</summary>
        public static RailingStyle RailingFor(RoadRecord r, in RoadStructureRecord s, bool heritage)
        {
            bool foot = IsFoot(r.RoadClass);
            if (s.Has(RoadStructureFlags.FootOverbridge)) return RailingStyle.SteelTruss;
            if (foot)
            {
                if (heritage || r.Surface == Surface.Brick || r.Surface == Surface.Cobble) return RailingStyle.Newar;
                return (Hash(r.OsmWayId, 0x52414C46) & 3) == 0 ? RailingStyle.ConcreteRail : RailingStyle.SteelTruss;
            }
            if (s.Kind == RoadStructureKind.Flyover) return RailingStyle.CrashBarrier;
            if (r.RoadClass == RoadClass.Motorway || r.RoadClass == RoadClass.Trunk && (r.Flags & RoadFlags.Oneway) != 0)
                return RailingStyle.CrashBarrier;
            uint h = Hash(r.OsmWayId, 0x5241494C);
            if (r.RoadClass == RoadClass.Trunk || r.RoadClass == RoadClass.Primary) return (h & 1) == 0 ? RailingStyle.PipeRail : RailingStyle.ConcreteRail;
            return h % 5 < 3 ? RailingStyle.ConcreteRail : RailingStyle.PipeRail;
        }

        /// <summary>Paint of the steel parts of a span (pipe rails, trusses, lamp posts on footbridges).</summary>
        public static uint SteelFor(RoadRecord r)
        {
            switch (Hash(r.OsmWayId, 0x5354454C) % 4)
            {
                case 0: return SteelGreen;
                case 1: return Galvanised;
                case 2: return SteelRed;
                default: return SteelBlue;
            }
        }
    }
}
