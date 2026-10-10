using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

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

        /// <summary>Heritage footbridge (the sacred ghats and the old town cores): a solid parapet with a flat stone
        /// coping and end pillars with stone finials, grey stone at the Bagmati ghats (Pashupati, Teku, Sankhamul, as in
        /// photographs of the Pashupati crossings), Newar brick in the Durbar Square cores.</summary>
        Newar = 4,
    }

    /// <summary>The superstructure over the deck (OSM <c>bridge:structure</c>), when it is not a plain girder.</summary>
    public enum BridgeForm : byte
    {
        /// <summary>RCC or steel girders under the deck (the default).</summary>
        Girder = 0,

        /// <summary>A suspension footbridge (jhulunge pul): towers at both ends, main cables, hangers, no river piers.</summary>
        Suspension = 1,

        /// <summary>A through arch: two ribs over the deck with hangers, no intermediate piers.</summary>
        Arch = 2,
    }

    /// <summary>
    /// Dimensions and colours of the bridge structures (docs/research/w2/bridges_flyovers.md). Depths are the
    /// structure below the deck surface: the data package sets <c>DeckY</c> at least
    /// <c>lower road + RoadClearance.MinUnderpassClearanceM + </c><see cref="CrossingDepthM"/> (foot decks
    /// <see cref="FootCrossingDepthM"/>) over a road; over a road the mesher thins the structure to keep the
    /// clearance, never below <see cref="MinDepthM"/> (foot decks <see cref="MinFootDepthM"/>; a deck too low for that
    /// leaves the road open and is reported
    /// in <see cref="BridgeLayout.Issues"/>).
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

        /// <summary>A record's deck at most this much short of the clearance plus the thinnest structure keeps that
        /// structure (the underside then this much under the clearance, still well above
        /// <see cref="RoadClearance.MinOverheadClearanceM"/>) and is reported in <see cref="BridgeLayout.Issues"/>; a
        /// deck further short leaves the road below open.</summary>
        public const float IssueToleranceM = 0.5f;

        /// <summary>A foot deck (a steel plate on cross beams between the side trusses, which carry the load) never gets
        /// thinner than this.</summary>
        public const float MinFootDepthM = 0.25f;

        /// <summary>The thinnest structure of a deck (<see cref="MinFootDepthM"/> for foot decks, else
        /// <see cref="MinDepthM"/>).</summary>
        public static float MinDepthFor(bool foot)
        {
            return foot ? MinFootDepthM : MinDepthM;
        }

        /// <summary>Deck depth reserved over a road below (vehicle decks): the data contract's
        /// <c>RoadStructureRecord.DeckDepthM</c>.</summary>
        public const float CrossingDepthM = 1.2f;

        /// <summary>Deck depth reserved over a road below (foot decks): the data contract's
        /// <c>RoadStructureRecord.FootDeckDepthM</c>.</summary>
        public const float FootCrossingDepthM = 0.6f;

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

        /// <summary>Landing length of a stair flight.</summary>
        public const float StairLandingM = 1.5f;

        /// <summary>Clear width of a foot overbridge deck when the way has no plausible width.</summary>
        public const float FootOverbridgeWidthM = 2.6f;

        /// <summary>Headroom kept over a foot way (footway, path, steps, pedestrian street) passing under a deck or a
        /// stair: walkers need far less than <see cref="RoadClearance.MinUnderpassClearanceM"/>.</summary>
        public const float FootHeadroomM = 2.5f;

        /// <summary>Clearance a structure keeps over road <paramref name="c"/>: <see cref="FootHeadroomM"/> over a foot
        /// way, <see cref="RoadClearance.MinUnderpassClearanceM"/> over anything with traffic.</summary>
        public static float UnderClearanceM(RoadClass c)
        {
            return IsFoot(c) ? FootHeadroomM : RoadClearance.MinUnderpassClearanceM;
        }

        /// <summary>Height of the envelope over road <paramref name="c"/> that no structure may enter from the side
        /// (<see cref="FootHeadroomM"/> over a foot way, <see cref="RoadClearance.MinOverheadClearanceM"/> else).</summary>
        public static float EnvelopeM(RoadClass c)
        {
            return IsFoot(c) ? FootHeadroomM : RoadClearance.MinOverheadClearanceM;
        }

        /// <summary>A foot-class deck steeper than this is drawn as steps.</summary>
        public const float StairGrade = 0.2f;

        /// <summary>Kerb stripes are painted this far from a real end of a span (the approach warning); beyond it the
        /// kerb is plain yellow (stripes split every ring, so on a long deck they are the costliest sweep).</summary>
        public const double KerbStripeReachM = 15.0;

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
        public static readonly uint CableGrey = MeshColor.FromHex(0x55595E);
        public static readonly uint MeshPanel = MeshColor.FromHex(0x3B4448);

        /// <summary>The galvanised chain-link fence of a suspension footbridge (jhulunge pul): light grey from the deck
        /// to the handrail cable, as on the valley's and the hills' trail bridges.</summary>
        public static readonly uint MeshGalvanised = MeshColor.FromHex(0xA3ABB0);
        public static readonly uint PlankWood = MeshColor.FromHex(0x8A6A48);

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

        /// <summary>Steepest grade of a derived ramp for a road class (the data package's rule: 5% trunk and
        /// primary, 6% secondary and tertiary, 10% tracks, 8% other roads).</summary>
        public static float GradeFor(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk:
                case RoadClass.Primary:
                    return 0.05f;
                case RoadClass.Secondary:
                case RoadClass.Tertiary:
                    return 0.06f;
                case RoadClass.Track:
                    return 0.10f;
                default:
                    return 0.08f;
            }
        }

        /// <summary>The railing a span gets: crash barriers on flyovers and expressway bridges, steel trusses on foot
        /// overbridges, Newar heritage rails only on heritage footbridges (<paramref name="heritage"/>: the RATR
        /// heritage-pedestrian flag or a curated heritage zone, <see cref="InHeritageZone"/>; never from the surface,
        /// since <c>paving_stones</c> and interlock are modern city pavers), and on other bridges, paved city
        /// footbridges included, RCC post-and-rail or pipe rails by a hash of the way.</summary>
        public static RailingStyle RailingFor(RoadRecord r, in RoadStructureRecord s, bool heritage)
        {
            bool foot = IsFoot(r.RoadClass);
            if (s.Has(RoadStructureFlags.FootOverbridge)) return RailingStyle.SteelTruss;
            if (foot)
            {
                if (heritage) return RailingStyle.Newar;
                return (Hash(r.OsmWayId, 0x52414C46) & 1) == 0 ? RailingStyle.ConcreteRail : RailingStyle.PipeRail;
            }
            if (s.Kind == RoadStructureKind.Flyover) return RailingStyle.CrashBarrier;
            if (r.RoadClass == RoadClass.Motorway || r.RoadClass == RoadClass.Trunk && (r.Flags & RoadFlags.Oneway) != 0)
                return RailingStyle.CrashBarrier;
            uint h = Hash(r.OsmWayId, 0x5241494C);
            if (r.RoadClass == RoadClass.Trunk || r.RoadClass == RoadClass.Primary) return (h & 1) == 0 ? RailingStyle.PipeRail : RailingStyle.ConcreteRail;
            return h % 5 < 3 ? RailingStyle.ConcreteRail : RailingStyle.PipeRail;
        }

        /// <summary>Paint of the steel parts of a structure (pipe rails, trusses, stairs), chosen by a way id (the
        /// layout passes the lowest way id of a connected structure, so every piece of one bridge matches).</summary>
        public static uint SteelFor(ulong wayId)
        {
            switch (Hash(wayId, 0x5354454C) % 4)
            {
                case 0: return SteelGreen;
                case 1: return Galvanised;
                case 2: return SteelRed;
                default: return SteelBlue;
            }
        }

        /// <summary>Paint of the steel parts of a single way (<see cref="SteelFor(ulong)"/> of its id).</summary>
        public static uint SteelFor(RoadRecord r)
        {
            return SteelFor(r.OsmWayId);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Curated facts (docs/research/w2/bridges_flyovers.md §3 and §4)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Heritage zones where footbridges get the heritage parapet (<see cref="RailingStyle.Newar"/>): centre (game metres, NPL-TM84 minus the world
        /// origin, projected with pipeline/ghumante_pipeline/projection.py) and radius. Pashupati ghats with
        /// Guhyeshwari, Teku Dobhan ghats, Sankhamul ghat, and the Kathmandu, Patan and Bhaktapur Durbar Square
        /// cores.</summary>
        private static readonly double[] HeritageZones =
        {
            532967.4, 165850.4, 450, // Pashupati (27.7104, 85.3487)
            528609.2, 163953.1, 250, // Teku Dobhan (27.6937, 85.3043)
            530840.8, 162791.4, 220, // Sankhamul (27.6830, 85.3268)
            528862.8, 165152.4, 300, // Kathmandu Durbar Square (27.7045, 85.3070)
            530705.1, 161648.7, 300, // Patan Durbar Square (27.6727, 85.3253)
            540835.5, 161706.3, 350, // Bhaktapur Durbar Square (27.6722, 85.4280)
        };

        /// <summary>True when world point (x, z) (game metres) lies in a curated heritage or sacred zone.</summary>
        public static bool InHeritageZone(double x, double z)
        {
            return HeritageZoneAt(x, z) >= 0;
        }

        /// <summary>True when world point (x, z) lies at one of the Bagmati ghats (Pashupati, Teku, Sankhamul): the
        /// heritage parapet is grey stone there (brick elsewhere).</summary>
        public static bool AtGhat(double x, double z)
        {
            int zone = HeritageZoneAt(x, z);
            return zone >= 0 && zone < 3;
        }

        /// <summary>Index of the heritage zone holding (x, z) in <see cref="HeritageZones"/> order, −1 when none.</summary>
        private static int HeritageZoneAt(double x, double z)
        {
            for (int i = 0; i + 2 < HeritageZones.Length; i += 3)
            {
                double dx = x - HeritageZones[i], dz = z - HeritageZones[i + 1], r = HeritageZones[i + 2];
                if (dx * dx + dz * dz <= r * r) return i / 3;
            }
            return -1;
        }

        /// <summary>Valley ways tagged <c>bridge:structure=suspension</c> or <c>simple-suspension</c> in OSM
        /// (docs/research/w2/data_detail_pass.md §3 of the data package), until the structure record carries the tag
        /// (open issue for the data package). Sorted.</summary>
        private static readonly ulong[] SuspensionWays =
        {
            53097930, 179094375, 179242639, 184870766, 225466624, 230366186, 340533813, 341595612, 341653610, 341653611, 341812309,
            341876558, 341906416, 341906419, 342070227, 342153282, 344858571, 553570363, 553570368, 891351467, 891434928, 905354110,
            1056551607, 1526336291,
        };

        /// <summary>Valley ways tagged <c>bridge:structure=arch</c> (Dallu Arch Bridge, the two Pashupati arch
        /// footbridges). Sorted.</summary>
        private static readonly ulong[] ArchWays = { 37707651, 112664331, 651121137 };

        /// <summary>The superstructure of a way from the curated OSM tags (a girder bridge when untagged).</summary>
        public static BridgeForm FormOf(ulong wayId)
        {
            if (Array.BinarySearch(SuspensionWays, wayId) >= 0) return BridgeForm.Suspension;
            if (Array.BinarySearch(ArchWays, wayId) >= 0) return BridgeForm.Arch;
            return BridgeForm.Girder;
        }
    }
}
