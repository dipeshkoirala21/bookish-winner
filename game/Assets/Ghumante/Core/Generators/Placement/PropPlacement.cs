using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Props;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Generators.Placement
{
    /// <summary>A street prop kind for the instancing layer (append only: the instanced kit draws one mesh per kind,
    /// <see cref="StreetPropMesher"/>).</summary>
    public enum StreetPropKind : byte
    {
        StreetLamp = 0,
        BusStop = 1,
        BusShelter = 2,
        TrafficSignal = 3,
        Gate = 4,
        StorageTank = 5,
        Bench = 6,
        WaterTap = 7,
        Windsock = 8,
        Helipad = 9,
        TaxiStand = 10,
        AerowayGate = 11,
        ParkingPosition = 12,
        Mast = 13,
        Artwork = 14,
        PowerTower = 15,
        PowerPole = 16,
        Well = 17,
        SolarPanel = 18,
        Chimney = 19,

        /// <summary>A solar street light (tilted panel on top, battery box): the KMC and SAARC-summit lamps.</summary>
        StreetLampSolar = 20,

        /// <summary>The black-and-gold lantern lamp of New Road, Shukrapath and Durbar Marg (2026).</summary>
        StreetLampHeritage = 21,

        /// <summary>A two-pole H-frame carrying a distribution transformer.</summary>
        TransformerPole = 22,

        /// <summary>The long mast-arm signal (<see cref="TrafficSignal"/> with a 5.6 m arm instead of 2.8 m) for wide
        /// approaches.</summary>
        TrafficSignalLong = 23,

        /// <summary><see cref="TrafficSignal"/> mirrored: the head faces -X, for a pole on the traffic's right (a median
        /// or the far kerb) with the arm reaching left over the lanes.</summary>
        TrafficSignalMirrored = 24,

        /// <summary><see cref="TrafficSignalLong"/> mirrored (see <see cref="TrafficSignalMirrored"/>).</summary>
        TrafficSignalLongMirrored = 25,
    }

    /// <summary>One placed street prop (tile-local metres, absolute Y; yaw clockwise from north).</summary>
    public struct StreetProp
    {
        public float X, Y, Z, YawDeg, HeightM;
        public StreetPropKind Kind;

        /// <summary>True for real OSM objects (PROP); false for generated ones (labelled procedural).</summary>
        public bool Osm;

        public ulong OsmRef;
    }

    /// <summary>
    /// Street props (W2_DESIGN 4.5, 4.7, 2.6; detail pass: docs/research/w2/ref_props.md): every real PROP object, then
    /// generated street lamps where OSM has none within 15 m, then the mast-arm traffic signals of every signalised
    /// junction. Everything stands on what is drawn (<see cref="RoadSurfaceQuery"/>): the footpath or kerb next to a
    /// road, a bridge or flyover deck at its parapet line (never the river bed or the ground under the deck), the
    /// terrain elsewhere; and nothing stands in what is drawn for traffic (owner playtest #7, docs/W2_PLAYTEST_1.md):
    /// no prop's footprint (every collider of <see cref="StreetPropColliders"/>, so also what makes it solid) lies on a
    /// drawn motor carriageway, junction cap or roundabout ring.
    /// <para>
    /// OSM objects keep their position unless they stand on a drawn carriageway (stops, lamps and poles are mapped on
    /// road nodes): road furniture then moves out of every carriageway, a pole or lamp on a divided road onto the median
    /// when it was mapped on the median side, anything else to the nearest kerb (on the footpath when there is one), and
    /// turns to face the road when the object has no mapped direction; anything still in the road moves to the nearest
    /// clear spot within 10 m (or is not drawn). Gates on a road turn so traffic passes through them and widen until
    /// their pillars stand outside the carriageway (a road too wide for a gate gets none). About one urban OSM power
    /// pole in six becomes an H-frame transformer. Mapped signals do not stand where mapped: each signalised junction
    /// gets one cantilever per approach (Ctx.PlaceSignals), its head hanging over the approach lanes.
    /// </para>
    /// <para>
    /// Generated lamps: arterial 10-12 m every 30-35 m, local 9-10 m every 25-30 m, alternating sides at the kerb (on
    /// the footpath when there is one; on a divided road's median on its median side), never in old cores (shared
    /// lanes) and never on a road or building; about a third of the roads (by way id) carry solar lamps; the named
    /// heritage streets (<see cref="HeritageStreets"/>) carry the black-and-gold lantern lamps about every 20 m on both
    /// sides. On a deck a lamp stands at the parapet line, and a lit bridge (which draws its own lamp posts) gets none; a
    /// generated prop whose base would be more than <see cref="MaxOffSurfaceM"/> off the drawn road surface beside it
    /// (an embankment, a cutting, a river bank under a deck) or whose pole would reach a deck overhead is skipped.
    /// Deterministic. Returns the props added.
    /// </para>
    /// </summary>
    public static class PropPlacement
    {
        private const uint PurposeLamp = 0x4C414D50, PurposeSolar = 0x534F4C52, PurposeXfmr = 0x58464D52;

        /// <summary>A generated prop's base may sit at most this far above or below the drawn road surface beside it.</summary>
        public const float MaxOffSurfaceM = 1.0f;

        /// <summary>Spacing of the heritage lantern lamps along a heritage street (alternating sides).</summary>
        public const double HeritageSpacingM = 10.5;

        /// <summary>Street names (English or default, case-insensitive) that carry the heritage lantern lamps.</summary>
        public static readonly string[] HeritageStreets = { "New Road", "Juddha Sadak", "Shukrapath", "Shukra Path", "Durbar Marg", "Darbar Marg" };

        /// <summary>Places the tile's props with the default road options (the drawn surface of
        /// <see cref="RoadOptions"/> defaults; the tier options change detail, not heights).</summary>
        public static int Place(TileData t, IHeightSampler h, List<StreetProp> output)
        {
            return Place(t, h, output, null);
        }

        /// <summary>Places the tile's props standing on the road surface drawn with <paramref name="roads"/>.</summary>
        public static int Place(TileData t, IHeightSampler h, List<StreetProp> output, RoadOptions roads)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (output == null) throw new ArgumentNullException(nameof(output));
            int before = output.Count;
            var ctx = new Ctx(t, h, roads);
            var osmLamps = new List<double>();
            var osmSignals = new List<PropRecord>();
            foreach (PropRecord p in t.Props)
            {
                StreetPropKind kind;
                if (!KindOf(p.Kind, out kind)) continue;
                if (kind == StreetPropKind.TrafficSignal && ctx.Layout != null)
                {
                    // Signals hang over their approaches from the junction's corners (PlaceSignals below).
                    osmSignals.Add(p);
                    continue;
                }
                double x = p.XCm / 100.0, z = p.ZCm / 100.0;
                float hgt = p.HeightDm > 0 ? p.HeightDm / 10f : DefaultHeight(kind);
                float yaw = p.YawDeg;
                if (kind == StreetPropKind.PowerPole && GrammarRng.Mix(GrammarRng.Mix((uint)p.OsmRef, (uint)(p.OsmRef >> 32)), PurposeXfmr) % 6 == 0 && ctx.Urban(x, z))
                {
                    kind = StreetPropKind.TransformerPole;
                    hgt = Math.Max(hgt, 8.5f);
                }
                if (!ctx.FitOsm(kind, p.Has(PropFlags.Yaw), p.Has(PropFlags.OnRoad), p.OsmRef, ref x, ref z, ref yaw, ref hgt)) continue;
                var prop = new StreetProp { X = (float)x, Y = ctx.BaseY(x, z, hgt), Z = (float)z, YawDeg = yaw, HeightM = hgt, Kind = kind, Osm = true, OsmRef = p.OsmRef };
                // Its whole footprint (every collider) off the drawn carriageways, caps and rings, or moved to the
                // nearest spot where it is; an object with no free spot within 10 m is not drawn.
                if (!ctx.FootprintClear(prop) && (kind == StreetPropKind.Gate || !ctx.Relocate(ref prop))) continue;
                x = prop.X;
                z = prop.Z;
                output.Add(prop);
                if (IsLampPost(kind))
                {
                    osmLamps.Add(x);
                    osmLamps.Add(z);
                }
            }
            if (t.Roads.Count == 0) return output.Count - before;
            RoadLayout layout = ctx.Layout;
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                RoadRecord r = t.Roads[ri];
                if (!RoadWidthModel.IsMotor(r.RoadClass) || r.RoadClass == RoadClass.Track || (r.Flags & RoadFlags.Tunnel) != 0) continue;
                bool heritage = IsHeritageStreet(t, r);
                AreaType area = RoadWidthModel.AreaOf(layout.Attrs[ri]);
                if (!heritage && (area == AreaType.OldCore || area == AreaType.Rural || area == AreaType.Forest || area == AreaType.Hill)) continue;
                bool arterial = RoadWidthModel.IsMajor(r.RoadClass);
                uint wayKey = GrammarRng.Mix((uint)r.OsmWayId, (uint)(r.OsmWayId >> 32));
                var rng = new GrammarRng(wayKey, PurposeLamp);
                double spacing = arterial ? rng.Range(30f, 35f) : rng.Range(25f, 30f);
                float pole = arterial ? rng.Range(10f, 12f) : rng.Range(9f, 10f);
                StreetPropKind kind = StreetPropKind.StreetLamp;
                var solar = new GrammarRng(wayKey, PurposeSolar);
                if (heritage)
                {
                    kind = StreetPropKind.StreetLampHeritage;
                    spacing = HeritageSpacingM;
                    pole = rng.Range(5.7f, 6.2f);
                }
                else if (solar.Range(0f, 1f) < (arterial ? 0.25f : 0.4f))
                {
                    kind = StreetPropKind.StreetLampSolar;
                    pole = rng.Range(7.2f, 8.4f);
                }
                double length = layout.Profiles[ri].LengthM;
                int side = (r.OsmWayId & 1) == 0 ? 1 : -1;
                for (double s = 0.5 * spacing; s < length; s += spacing, side = -side)
                {
                    if (layout.InGap(ri, s)) continue;
                    double x, z;
                    float y;
                    if (!ctx.LampSpot(ri, s, side, pole, heritage, out x, out z, out y)) continue;
                    if (NearAny(osmLamps, x, z, 15.0)) continue;
                    RoadCut c = layout.CutAt(t, ri, s);
                    double yaw = Math.Atan2(-side * c.UX, -side * c.UZ) * 180 / Math.PI; // the arm reaches over the road
                    var lamp = new StreetProp { X = (float)x, Y = y, Z = (float)z, YawDeg = (float)(yaw < 0 ? yaw + 360 : yaw), HeightM = pole, Kind = kind };
                    if (!ctx.FootprintClear(lamp)) continue;
                    output.Add(lamp);
                }
            }
            ctx.PlaceSignals(osmSignals, output);
            return output.Count - before;
        }

        /// <summary>True for the kinds that are a lamp post (a pole collider, the 15 m OSM-lamp exclusion).</summary>
        public static bool IsLampPost(StreetPropKind k)
        {
            return k == StreetPropKind.StreetLamp || k == StreetPropKind.StreetLampSolar || k == StreetPropKind.StreetLampHeritage;
        }

        /// <summary>True when road <paramref name="r"/> is one of the <see cref="HeritageStreets"/>.</summary>
        public static bool IsHeritageStreet(TileData t, RoadRecord r)
        {
            NameRecord n = t.Name(r.NameRef);
            if (n == null) return false;
            foreach (string s in HeritageStreets)
                if (string.Equals(n.En, s, StringComparison.OrdinalIgnoreCase) || string.Equals(n.Default, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool NearAny(List<double> pts, double x, double z, double r)
        {
            for (int i = 0; i + 1 < pts.Count; i += 2)
                if ((pts[i] - x) * (pts[i] - x) + (pts[i + 1] - z) * (pts[i + 1] - z) < r * r) return true;
            return false;
        }

        /// <summary>Typical height of an OSM object without a height tag: the kit's own height (so an untagged
        /// object is drawn at its natural size) except where real objects of the kind are usually taller or shorter.</summary>
        private static float DefaultHeight(StreetPropKind k)
        {
            switch (k)
            {
                case StreetPropKind.StreetLamp: return 9.5f;
                case StreetPropKind.StorageTank: return 2.2f;
                case StreetPropKind.Chimney: return 20f;
                default: return StreetPropMesher.DefaultHeight(k);
            }
        }

        /// <summary>The street-prop kind of a PROP object kind; false for kinds this layer does not place (trees).</summary>
        public static bool KindOf(ObjectKind k, out StreetPropKind kind)
        {
            switch (k)
            {
                case ObjectKind.StreetLamp: kind = StreetPropKind.StreetLamp; return true;
                case ObjectKind.BusStop: kind = StreetPropKind.BusStop; return true;
                case ObjectKind.Shelter: kind = StreetPropKind.BusShelter; return true;
                case ObjectKind.TrafficSignals: kind = StreetPropKind.TrafficSignal; return true;
                case ObjectKind.Gate: kind = StreetPropKind.Gate; return true;
                case ObjectKind.StorageTank: kind = StreetPropKind.StorageTank; return true;
                case ObjectKind.Bench: kind = StreetPropKind.Bench; return true;
                case ObjectKind.WaterTap: kind = StreetPropKind.WaterTap; return true;
                case ObjectKind.Well: kind = StreetPropKind.Well; return true;
                case ObjectKind.Windsock: kind = StreetPropKind.Windsock; return true;
                case ObjectKind.Helipad: kind = StreetPropKind.Helipad; return true;
                case ObjectKind.TaxiStand: kind = StreetPropKind.TaxiStand; return true;
                case ObjectKind.AerowayGate: kind = StreetPropKind.AerowayGate; return true;
                case ObjectKind.ParkingPosition: kind = StreetPropKind.ParkingPosition; return true;
                case ObjectKind.Mast:
                case ObjectKind.Tower: kind = StreetPropKind.Mast; return true;
                case ObjectKind.Artwork: kind = StreetPropKind.Artwork; return true;
                case ObjectKind.PowerTower: kind = StreetPropKind.PowerTower; return true;
                case ObjectKind.PowerPole: kind = StreetPropKind.PowerPole; return true;
                case ObjectKind.SolarPanel: kind = StreetPropKind.SolarPanel; return true;
                case ObjectKind.Chimney: kind = StreetPropKind.Chimney; return true;
                default: kind = StreetPropKind.StreetLamp; return false;
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Placement context: the terrain, the drawn road surface, the decks and the building mask of one tile.

        private sealed class Ctx
        {
            private readonly TileData _t;
            private readonly RoadSurface _g;
            private readonly RoadSurfaceQuery _q;
            private readonly IBridgeDeckQuery _decks;
            private readonly BridgeLayout _bridges;
            private readonly PlacementMask _mask;
            private readonly double _x0, _z0;
            public readonly RoadLayout Layout;

            public Ctx(TileData t, IHeightSampler h, RoadOptions roads)
            {
                _t = t;
                _g = new RoadSurface(t, h);
                _x0 = t.Tile.X0;
                _z0 = t.Tile.Z0;
                if (t.Roads.Count == 0) return;
                Layout = RoadLayout.For(t);
                _q = RoadSurfaceQuery.For(t, h, roads ?? new RoadOptions());
                BridgeDeckIndex decks = BridgeDeckIndex.ForTile(t);
                _decks = decks;
                _bridges = decks.Layout;
                _mask = new PlacementMask(t);
            }

            private bool Inside(double x, double z)
            {
                return x >= 0 && z >= 0 && x < _t.Tile.Size && z < _t.Tile.Size;
            }

            private bool InBuilding(double x, double z)
            {
                return _mask != null && (_mask.At(x, z) & PlacementMask.Building) != 0;
            }

            /// <summary>True in urban, peri-urban and old-core areas (by the nearest road's area type).</summary>
            public bool Urban(double x, double z)
            {
                int ri;
                double s, lat, d;
                if (!NearestRoad(x, z, 80, out ri, out s, out lat, out d)) return false;
                AreaType a = RoadWidthModel.AreaOf(Layout.Attrs[ri]);
                return a == AreaType.Urban || a == AreaType.OldCore || a == AreaType.PeriUrban;
            }

            /// <summary>The ground road surface (not a deck) under (x, z) within a step of the terrain, if any.</summary>
            private bool GroundSurface(double x, double z, float terrain, out RoadSurfaceHit hit)
            {
                hit = default(RoadSurfaceHit);
                if (_q == null || !_q.TrySample(_x0 + x, _z0 + z, terrain + 0.5f, out hit)) return false;
                return !hit.OnDeck && Math.Abs(hit.Y - terrain) < 2.5f;
            }

            /// <summary>
            /// The base height of an OSM object of height <paramref name="height"/> at (x, z): a deck covering the point
            /// when nothing drawn on the ground is there (a lamp mapped on a bridge stands on it, not in the river) or
            /// when the object would not fit under the deck; else the drawn ground road surface, else the terrain.
            /// </summary>
            public float BaseY(double x, double z, float height)
            {
                float terrain = _g.Height(x, z);
                RoadSurfaceHit ground;
                bool onGround = GroundSurface(x, z, terrain, out ground);
                float gy = onGround ? ground.Y : terrain;
                if (_decks == null) return gy;
                float deckY, nx, ny, nz;
                if (!_decks.TryDeck(_x0 + x, _z0 + z, terrain + 80f, out deckY, out nx, out ny, out nz) || deckY < gy + 1.0f) return gy;
                if (!onGround) return deckY;
                float under;
                if (_decks.TryCeiling(_x0 + x, _z0 + z, gy + 0.1f, out under) && under < gy + height + 0.3f) return deckY;
                return gy;
            }

            /// <summary>
            /// Base height of a generated prop of height <paramref name="height"/> at (x, z) on the ground: the drawn
            /// surface there (footpath, shoulder) or the terrain; false when that base is more than
            /// <see cref="MaxOffSurfaceM"/> off the drawn road surface at the nearest carriageway edge, or a deck
            /// overhead would cut the pole.
            /// </summary>
            public bool GeneratedBase(double x, double z, float height, out float y)
            {
                return GeneratedBase(x, z, height, -1, 0, out y);
            }

            /// <summary>As <see cref="GeneratedBase(double, double, float, out float)"/>, measured against the edge of
            /// road <paramref name="road"/> at along <paramref name="along"/> (−1: the nearest road).</summary>
            public bool GeneratedBase(double x, double z, float height, int road, double along, out float y)
            {
                float terrain = _g.Height(x, z);
                RoadSurfaceHit hit;
                y = GroundSurface(x, z, terrain, out hit) ? hit.Y : terrain;
                if (_q == null) return true;
                // A drawn surface right here at another height (a road in a cutting, a lowered underpass): no base.
                RoadSurfaceHit here;
                if (_q.TrySample(_x0 + x, _z0 + z, y + 0.5f, out here) && Math.Abs(here.Y - y) > MaxOffSurfaceM) return false;
                int ri = road;
                double s = along, lat = 0, d;
                if (ri < 0 && !NearestRoad(x, z, 15, out ri, out s, out lat, out d)) ri = -1;
                else if (road >= 0)
                {
                    int seg;
                    double a;
                    if (!Layout.Centres[road].Nearest(x, z, out seg, out a, out lat, out d)) lat = 0;
                }
                if (ri >= 0 && _q.Grade.Has(ri))
                {
                    RoadCut c = Layout.DrawnCutAt(_t, ri, s);
                    double off = c.Shift + (lat >= c.Shift ? c.Half : -c.Half);
                    double ex = c.CX + c.UX * off, ez = c.CZ + c.UZ * off;
                    float edge = _q.Grade.SurfaceY(ri, s, off, ex, ez);
                    if (Math.Abs(y - edge) > MaxOffSurfaceM) return false;
                }
                float under;
                if (_decks != null && _decks.TryCeiling(_x0 + x, _z0 + z, y + 0.1f, out under) && under < y + height + 0.5f) return false;
                return true;
            }

            /// <summary>
            /// The spot of a generated lamp at along <paramref name="s"/> of road <paramref name="ri"/> on side
            /// <paramref name="side"/>: at the kerb on the ground; on a deck at the parapet line (none on a bridge that
            /// draws its own lamps). False when there is no good spot.
            /// </summary>
            public bool LampSpot(int ri, double s, int side, float pole, bool heritage, out double x, out double z, out float y)
            {
                x = z = 0;
                y = 0;
                RoadCut c = Layout.DrawnCutAt(_t, ri, s);
                RoadProfile p = Layout.ProfileAt(ri, s);
                bool deck = _q.Grade.Has(ri) && (_q.Grade.IsAbsolute(ri) || _q.Grade.OnDeck(ri, s));
                if (deck) return !BridgeDrawsLamps(ri) && DeckSpot(ri, s, c, side, pole, out x, out z, out y);
                double foot = side > 0 ? p.FootpathLeftM : p.FootpathRightM;
                double off = c.Shift + side * (c.Half + (foot > 0 ? (heritage ? 0.45 : 0.6) : 0.8 + p.ShoulderM));
                // The right side of a divided road's piece is its half of the median: the lamp stands on the median's
                // centre line (Kantipath, the Ring Road), never in the other carriageway.
                if (side < 0 && Layout.Profiles[ri].Dual) off = c.Shift - (c.Half + 0.5 * Math.Max(RoadWidthModel.MinMedianM, p.MedianM));
                x = c.CX + c.UX * off;
                z = c.CZ + c.UZ * off;
                if (!Inside(x, z) || InBuilding(x, z) || !Clear(x, z, 0.35)) return false;
                return GeneratedBase(x, z, pole, ri, s, out y);
            }

            /// <summary>A lamp on a deck: walking out from the carriageway edge across the walkway, the outermost point
            /// that is still this deck (at the road's height, on no carriageway: a twin deck beside a shared side does
            /// not count), less a margin for the parapet; false when the deck has no walkway there or something is
            /// overhead.</summary>
            private bool DeckSpot(int ri, double s, in RoadCut c, int side, float pole, out double x, out double z, out float y)
            {
                x = z = 0;
                y = 0;
                float road = _q.Grade.SurfaceY(ri, s, c.Shift, c.CX, c.CZ);
                double last = double.NaN;
                float lastY = 0;
                for (double off = c.Half + 0.1; off <= c.Half + 4.0; off += 0.1)
                {
                    double px = c.CX + c.UX * (c.Shift + side * off), pz = c.CZ + c.UZ * (c.Shift + side * off);
                    float dy, nx, ny, nz;
                    if (!_decks.TryDeck(_x0 + px, _z0 + pz, road + 1.0f, out dy, out nx, out ny, out nz) || Math.Abs(dy - road) > 0.6f) break;
                    RoadSurfaceHit hit;
                    if (_q.TrySample(_x0 + px, _z0 + pz, dy + 0.3f, out hit) && hit.OnCarriageway && Math.Abs(hit.Y - dy) < 0.4f) break;
                    last = off;
                    lastY = dy;
                }
                if (double.IsNaN(last) || last < c.Half + 0.45) return false;
                double o = last - 0.25;
                x = c.CX + c.UX * (c.Shift + side * o);
                z = c.CZ + c.UZ * (c.Shift + side * o);
                if (!Inside(x, z) || InBuilding(x, z)) return false;
                float d2, n1, n2, n3;
                y = _decks.TryDeck(_x0 + x, _z0 + z, road + 1.0f, out d2, out n1, out n2, out n3) && Math.Abs(d2 - road) <= 0.6f ? d2 : lastY;
                float under;
                return !(_decks.TryCeiling(_x0 + x, _z0 + z, y + 0.1f, out under) && under < y + pole + 0.5f);
            }

            /// <summary>True when the bridge layout draws its own lamp posts on road <paramref name="ri"/>.</summary>
            private bool BridgeDrawsLamps(int ri)
            {
                foreach (BridgeSpan sp in _bridges.Spans)
                    if (sp.Road == ri && sp.Lit && !sp.Overbridge && sp.Lamps.Count > 0) return true;
                return false;
            }

            /// <summary>True on the drawn carriageway of a motor road or a junction (footways, paths and pedestrian
            /// streets do not count: furniture stands on them).</summary>
            private bool OnCarriageway(double x, double z)
            {
                if (_q == null) return false;
                RoadSurfaceHit hit;
                float terrain = _g.Height(x, z);
                return _q.TrySample(_x0 + x, _z0 + z, terrain + 0.5f, out hit) && MotorCarriageway(hit) && Math.Abs(hit.Y - terrain) < 2.5f;
            }

            private bool MotorCarriageway(in RoadSurfaceHit hit)
            {
                return hit.OnCarriageway && (hit.Road < 0 || RoadWidthModel.IsMotor(_t.Roads[hit.Road].RoadClass));
            }

            /// <summary>The nearest free spot round (x, z): off every motor carriageway, out of buildings, on the tile;
            /// rings of 16 directions every 0.75 m out to 10 m.</summary>
            private bool NearestFreeSpot(ref double x, ref double z)
            {
                for (double r = 1.0; r <= 10.0; r += 0.75)
                    for (int k = 0; k < 16; k++)
                    {
                        double a = k * Math.PI / 8, nx = x + r * Math.Sin(a), nz = z + r * Math.Cos(a);
                        if (!Inside(nx, nz) || InBuilding(nx, nz) || OnCarriageway(nx, nz)) continue;
                        x = nx;
                        z = nz;
                        return true;
                    }
                return false;
            }

            /// <summary>The drawn road whose centreline is nearest (x, z) within <paramref name="maxDist"/>.</summary>
            private bool NearestRoad(double x, double z, double maxDist, out int road, out double along, out double lateral, out double dist)
            {
                road = -1;
                along = lateral = 0;
                dist = maxDist;
                if (Layout == null) return false;
                for (int i = 0; i < _t.Roads.Count; i++)
                {
                    RoadCentreline c = Layout.Centres[i];
                    if (c == null || c.Count < 2 || !Layout.DrawsRibbon(i)) continue;
                    int seg;
                    double a, l, d;
                    if (!c.Nearest(x, z, out seg, out a, out l, out d) || d >= dist) continue;
                    road = i;
                    along = a;
                    lateral = l;
                    dist = d;
                }
                return road >= 0;
            }

            /// <summary>
            /// Road furniture mapped on or right next to a carriageway (a stop, a lamp or a pole on a road node) moves out
            /// of every drawn carriageway, junction cap and ring: a utility pole or lamp on a divided road onto the median
            /// when it was mapped on the median side, anything else to the nearest kerb (on the footpath when there is
            /// one); objects without a mapped direction turn to the road (signs face the oncoming traffic, lamps reach
            /// over the road, shelters and benches open to it). A gate on a road turns so traffic passes through it and
            /// is centred on the drawn carriageway and widened until its pillars stand outside it (false: the road is too
            /// wide for a gate, which is then not drawn). Signals are placed by <see cref="PlaceSignals"/>.
            /// </summary>
            public bool FitOsm(StreetPropKind kind, bool hasYaw, bool onRoad, ulong osmRef, ref double x, ref double z, ref float yaw, ref float height)
            {
                if (Layout == null || !RoadFurniture(kind)) return true;
                // The road the object stands on (or the nearest one), its along and lateral position.
                int ri;
                double s, rel;
                RoadSurfaceHit hit;
                float terrain = _g.Height(x, z);
                bool onCarriageway = _q.TrySample(_x0 + x, _z0 + z, terrain + 0.5f, out hit) && MotorCarriageway(hit) && Math.Abs(hit.Y - terrain) < 2.5f;
                if (onCarriageway && hit.Road >= 0)
                {
                    ri = hit.Road;
                    s = hit.AlongM;
                    rel = hit.LateralM;
                }
                else
                {
                    double lat, d;
                    if (!NearestRoad(x, z, 25, out ri, out s, out lat, out d))
                    {
                        // On a junction cap or ring with no ribbon near: out to the nearest free spot.
                        if (onCarriageway && kind != StreetPropKind.Gate) NearestFreeSpot(ref x, ref z);
                        return true;
                    }
                    rel = lat - Layout.DrawnCutAt(_t, ri, s).Shift;
                }
                RoadCut c = Layout.DrawnCutAt(_t, ri, s);
                double tx = c.UZ, tz = -c.UX; // travel direction: the left normal U turned clockwise
                int side = rel > 0.05 ? 1 : rel < -0.05 ? -1 : ((osmRef >> 2) & 1) == 0 ? 1 : -1;
                if (kind == StreetPropKind.Gate) return FitGate(onRoad, onCarriageway, c, rel, tx, tz, ref x, ref z, ref yaw, ref height);
                bool moved = false;
                double depth = kind == StreetPropKind.BusShelter ? 2.3 : kind == StreetPropKind.Bench ? 0.5 : kind == StreetPropKind.TransformerPole ? 0.6 : 0.45;
                if (onCarriageway || Math.Abs(rel) < c.Half + depth - 0.2)
                {
                    RoadProfile p = Layout.ProfileAt(ri, s);
                    // A pole or lamp mapped on the median side of a divided road stands on the median (Ring Road,
                    // Kantipath): its centre line is half the median beyond the right edge of the piece.
                    bool pole = kind == StreetPropKind.PowerPole || kind == StreetPropKind.TransformerPole || kind == StreetPropKind.StreetLamp;
                    if (pole && side < 0 && Layout.Profiles[ri].Dual)
                    {
                        double mid = c.Half + 0.5 * Math.Max(RoadWidthModel.MinMedianM, p.MedianM);
                        double nx = c.CX + c.UX * (c.Shift - mid), nz = c.CZ + c.UZ * (c.Shift - mid);
                        if (Inside(nx, nz) && !InBuilding(nx, nz) && Clear(nx, nz, 0.2))
                        {
                            x = nx;
                            z = nz;
                            moved = true;
                        }
                    }
                    // Out to the kerb on the object's side, further out when that is still a carriageway (a junction cap,
                    // a road alongside), then the other side.
                    for (int attempt = 0; attempt < 12 && !moved; attempt++)
                    {
                        int sd = attempt < 6 ? side : -side;
                        if (sd < 0 && Layout.Profiles[ri].Dual) continue; // the median side: handled above
                        double foot = sd > 0 ? p.FootpathLeftM : p.FootpathRightM;
                        double want = c.Half + Math.Max(depth, foot > 0 ? Math.Min(foot * 0.5, 1.2) : 0.6 + p.ShoulderM) + 0.9 * (attempt % 6);
                        double nx = c.CX + c.UX * (c.Shift + sd * want), nz = c.CZ + c.UZ * (c.Shift + sd * want);
                        if (!Inside(nx, nz) || InBuilding(nx, nz) || !Clear(nx, nz, 0.25)) continue;
                        x = nx;
                        z = nz;
                        side = sd;
                        moved = true;
                    }
                    if (!moved && onCarriageway && NearestFreeSpot(ref x, ref z))
                    {
                        int seg;
                        double a, lat, d;
                        if (Layout.Centres[ri].Nearest(x, z, out seg, out a, out lat, out d)) side = lat - c.Shift >= 0 ? 1 : -1;
                    }
                }
                if (hasYaw) return true;
                switch (kind)
                {
                    case StreetPropKind.BusStop:
                    case StreetPropKind.TaxiStand:
                        // Left-hand traffic: the near lane runs along the travel direction when the sign is on its left
                        // (side > 0); the sign faces the traffic coming towards it.
                        yaw = side > 0 ? Deg(-tx, -tz) : Deg(tx, tz);
                        break;
                    default:
                        yaw = Deg(-side * c.UX, -side * c.UZ); // towards the road
                        break;
                }
                return true;
            }

            /// <summary>Half the gate's clear opening at its default height (pillar inner faces at ±2.65 m).</summary>
            private const double GateHalfOpeningM = 2.65;

            /// <summary>The most a gate is widened (with its height) to clear a wide carriageway.</summary>
            private const double GateMaxScale = 1.7;

            private bool FitGate(bool onRoad, bool onCarriageway, in RoadCut c, double rel, double tx, double tz, ref double x, ref double z, ref float yaw,
                                 ref float height)
            {
                if (!onRoad && !onCarriageway && Math.Abs(rel) >= c.Half) return true;
                yaw = Deg(tx, tz);
                if (!onCarriageway) return true;
                // Centred on the drawn carriageway, its pillars outside both edges with a hand's breadth to spare.
                double scale = Math.Max(height / StreetPropMesher.DefaultHeight(StreetPropKind.Gate), (c.Half + 0.15) / GateHalfOpeningM);
                if (scale > GateMaxScale) return false;
                x = c.CX + c.UX * c.Shift;
                z = c.CZ + c.UZ * c.Shift;
                height = (float)(scale * StreetPropMesher.DefaultHeight(StreetPropKind.Gate));
                return true;
            }

            private readonly StructureColliders _footprint = new StructureColliders();

            /// <summary>
            /// True when no collider of <paramref name="p"/> (<see cref="StreetPropColliders"/>: what makes it solid,
            /// so also where it stands) blocks a body on a drawn motor carriageway, junction cap or ring at the prop's
            /// level: every cylinder's centre and rim, every box's corners, edges and centre.
            /// </summary>
            public bool FootprintClear(in StreetProp p)
            {
                if (_q == null) return true;
                _footprint.Clear();
                StreetPropColliders.Append(p, _footprint);
                foreach (ColliderCylinder y in _footprint.Cylinders)
                    for (int k = -1; k < 8; k++)
                    {
                        double a = k * Math.PI / 4, r = k < 0 ? 0 : y.Radius + 0.05;
                        if (BlocksRoad(y.CX + r * Math.Sin(a), y.CZ + r * Math.Cos(a), y.Bottom, y.Top)) return false;
                    }
                foreach (OrientedBox b in _footprint.Boxes)
                {
                    double c = Math.Cos(b.YawRad), sn = Math.Sin(b.YawRad);
                    for (int k = 0; k < 9; k++)
                    {
                        double u = (k % 3 - 1) * (b.HalfX + 0.05), w = (k / 3 - 1) * (b.HalfZ + 0.05);
                        if (BlocksRoad(b.CX + u * c - w * sn, b.CZ + u * sn + w * c, b.Bottom, b.Top)) return false;
                    }
                }
                return true;
            }

            /// <summary>A drawn motor carriageway, cap or ring at tile-local (x, z) whose surface lies within the span
            /// <paramref name="bottom"/>..<paramref name="top"/> of a solid there (a body on it would hit the solid).</summary>
            private bool BlocksRoad(double x, double z, float bottom, float top)
            {
                RoadSurfaceHit hit;
                if (!_q.TrySample(_x0 + x, _z0 + z, bottom + 1.0f, out hit) || !MotorCarriageway(hit)) return false;
                float feet = hit.Y + 0.25f;
                return feet > bottom - 0.25f && feet < top;
            }

            /// <summary>Moves <paramref name="p"/> to the nearest spot round it (rings of 16 directions every 0.75 m
            /// out to 10 m) on the tile, out of buildings, where its footprint is clear; false when there is none.</summary>
            public bool Relocate(ref StreetProp p)
            {
                for (double r = 0.75; r <= 10.0; r += 0.75)
                    for (int k = 0; k < 16; k++)
                    {
                        double a = k * Math.PI / 8, nx = p.X + r * Math.Sin(a), nz = p.Z + r * Math.Cos(a);
                        if (!Inside(nx, nz) || InBuilding(nx, nz)) continue;
                        StreetProp q = p;
                        q.X = (float)nx;
                        q.Z = (float)nz;
                        q.Y = BaseY(nx, nz, p.HeightM);
                        if (!FootprintClear(q)) continue;
                        p = q;
                        return true;
                    }
                return false;
            }

            /// <summary>No drawn motor carriageway, cap or ring at (x, z) nor within <paramref name="r"/> of it.</summary>
            private bool Clear(double x, double z, double r)
            {
                if (OnCarriageway(x, z)) return false;
                for (int k = 0; k < 8; k++)
                {
                    double a = k * Math.PI / 4;
                    if (OnCarriageway(x + r * Math.Sin(a), z + r * Math.Cos(a))) return false;
                }
                return true;
            }

            private static bool RoadFurniture(StreetPropKind k)
            {
                switch (k)
                {
                    case StreetPropKind.StreetLamp:
                    case StreetPropKind.BusStop:
                    case StreetPropKind.BusShelter:
                    case StreetPropKind.Gate:
                    case StreetPropKind.Bench:
                    case StreetPropKind.WaterTap:
                    case StreetPropKind.TaxiStand:
                    case StreetPropKind.PowerPole:
                    case StreetPropKind.TransformerPole:
                        return true;
                    default:
                        return false;
                }
            }

            private static float Deg(double dx, double dz)
            {
                double a = Math.Atan2(dx, dz) * 180 / Math.PI;
                return (float)(a < 0 ? a + 360 : a);
            }

            // ---------------------------------------------------------------------------------------------------------
            // Traffic signals (owner playtest #7: "traffic lights should be hanging on top").

            /// <summary>One approach of a signalised junction or crossing: road <see cref="Road"/>, the along value
            /// <see cref="CutS"/> where its ribbon meets the junction (or the crossing), and <see cref="Out"/> = +1 when
            /// the road leaves the junction towards increasing along (traffic approaching runs the other way).</summary>
            private struct Approach
            {
                public int Road, Out;
                public double CutS;
            }

            /// <summary>Most a mapped signal lies from a junction cap or ring (it is often mapped on an approach before
            /// the stop line) to belong to it.</summary>
            private const double SignalSiteM = 22.0;

            /// <summary>Pole offsets behind the kerb tried for a signal (along the approach from the junction edge).</summary>
            private static readonly double[] SignalBack = { 1.2, 2.4, 3.6, 5.0, 6.5, 8.0, 10.0, 12.5, 15.0 };

            private static readonly StreetPropKind[] SignalKinds = { StreetPropKind.TrafficSignal, StreetPropKind.TrafficSignalLong };

            private static readonly StreetPropKind[] MirroredKinds = { StreetPropKind.TrafficSignalMirrored, StreetPropKind.TrafficSignalLongMirrored };

            /// <summary>
            /// The signals of the tile's signalised junctions (caps and rings within <see cref="SignalSiteM"/> of a mapped
            /// signal, or matched by a JNCT record of kind Signals) and of mapped signals elsewhere (a signalised
            /// crossing on a straight road): one mast-arm signal per approach, its pole on the footpath (or verge) at the
            /// approaching traffic's LEFT kerb just before the junction (Nepal drives on the left), the arm reaching over
            /// the approach lanes so the head hangs over their middle (two-way roads: never over the opposing lanes;
            /// one-way pieces: over the carriageway), facing the traffic, its bottom more than the 4.5 m overhead
            /// clearance above the road. Out of every carriageway, cap, ring and building. The short (2.8 m) or long
            /// (5.6 m) arm is picked for the road's width. A mapped signal gives its OSM id to the approach nearest to it
            /// (several on one approach are one signal); approaches without one are generated.
            /// </summary>
            public void PlaceSignals(List<PropRecord> osm, List<StreetProp> output)
            {
                if (Layout == null) return;
                int caps = Layout.Caps.Count, sites = caps + Layout.Rings.Count;
                var signalised = new bool[sites];
                var siteOf = new int[osm.Count];
                for (int i = 0; i < osm.Count; i++)
                {
                    siteOf[i] = NearestSite(osm[i].XCm / 100.0, osm[i].ZCm / 100.0, SignalSiteM);
                    if (siteOf[i] >= 0) signalised[siteOf[i]] = true;
                }
                foreach (JunctionRecord j in _t.Junctions)
                {
                    if (j.Kind != JunctionKind.Signals) continue;
                    int site = NearestSite(j.XCm / 100.0, j.ZCm / 100.0, 8.0);
                    if (site >= 0) signalised[site] = true;
                }
                var approaches = new List<Approach>();
                var poles = new List<double>();
                for (int si = 0; si < sites; si++)
                {
                    if (!signalised[si]) continue;
                    approaches.Clear();
                    if (si < caps) CapApproaches(Layout.Caps[si], approaches);
                    else RingApproaches(Layout.Rings[si - caps], approaches);
                    PlaceApproaches(approaches, osm, siteOf, si, poles, output);
                }
                // Mapped signals away from every junction: a signalised crossing on the nearest motor road, one signal per
                // direction of travel just before it.
                for (int i = 0; i < osm.Count; i++)
                {
                    if (siteOf[i] >= 0) continue;
                    double x = osm[i].XCm / 100.0, z = osm[i].ZCm / 100.0;
                    int ri;
                    double s, lat, d;
                    if (!NearestSignalRoad(x, z, 15.0, out ri, out s, out lat, out d)) continue;
                    approaches.Clear();
                    bool oneway = (_t.Roads[ri].Flags & RoadFlags.Oneway) != 0;
                    // Traffic along +s meets the crossing from smaller s: that approach leaves the crossing towards -s.
                    approaches.Add(new Approach { Road = ri, CutS = s - 1.5, Out = -1 });
                    if (!oneway) approaches.Add(new Approach { Road = ri, CutS = s + 1.5, Out = 1 });
                    siteOf[i] = -2 - i; // a site of its own
                    PlaceApproaches(approaches, osm, siteOf, -2 - i, poles, output);
                }
            }

            /// <summary>Places one signal per approach and hands the OSM ids of the site's mapped signals to the nearest.</summary>
            private void PlaceApproaches(List<Approach> approaches, List<PropRecord> osm, int[] siteOf, int site, List<double> poles, List<StreetProp> output)
            {
                for (int a = 0; a < approaches.Count; a++)
                {
                    StreetProp prop;
                    if (!SignalFor(approaches[a], poles, out prop)) continue;
                    // The OSM id of the site's mapped signal nearest to this approach's junction edge (each used once).
                    RoadCut cut = Layout.DrawnCutAt(_t, approaches[a].Road, approaches[a].CutS);
                    double best = double.MaxValue;
                    int pick = -1;
                    for (int i = 0; i < osm.Count; i++)
                    {
                        if (siteOf[i] != site) continue;
                        double dx = osm[i].XCm / 100.0 - cut.CX, dz = osm[i].ZCm / 100.0 - cut.CZ, d2 = dx * dx + dz * dz;
                        if (d2 < best)
                        {
                            best = d2;
                            pick = i;
                        }
                    }
                    if (pick >= 0)
                    {
                        prop.Osm = true;
                        prop.OsmRef = osm[pick].OsmRef;
                        siteOf[pick] = int.MinValue; // used
                    }
                    output.Add(prop);
                    poles.Add(prop.X);
                    poles.Add(prop.Z);
                }
            }

            /// <summary>The cap or ring (index: caps first, then rings) nearest (x, z) within <paramref name="maxDist"/>
            /// of its outline, or -1.</summary>
            private int NearestSite(double x, double z, double maxDist)
            {
                int best = -1;
                double bestD = maxDist;
                for (int i = 0; i < Layout.Caps.Count; i++)
                {
                    JunctionCap cap = Layout.Caps[i];
                    double d = PolygonDistance(cap.PolyX, cap.PolyZ, cap.Count, x, z);
                    if (d <= bestD)
                    {
                        bestD = d;
                        best = i;
                    }
                }
                for (int i = 0; i < Layout.Rings.Count; i++)
                {
                    RoadRing ring = Layout.Rings[i];
                    double dx = x - ring.X, dz = z - ring.Z, d = Math.Max(0, Math.Sqrt(dx * dx + dz * dz) - ring.OuterRadiusM);
                    if (d <= bestD)
                    {
                        bestD = d;
                        best = Layout.Caps.Count + i;
                    }
                }
                return best;
            }

            private static double PolygonDistance(double[] px, double[] pz, int n, double x, double z)
            {
                bool inside = false;
                double best = double.MaxValue;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    if ((pz[i] > z) != (pz[j] > z) && x < (px[j] - px[i]) * (z - pz[i]) / (pz[j] - pz[i]) + px[i]) inside = !inside;
                    double t;
                    best = Math.Min(best, RoadCorridorIndex.SegDistance(x, z, px[j], pz[j], px[i], pz[i], out t));
                }
                return inside ? 0 : best;
            }

            /// <summary>True for the roads that get signals: drawn motor roads, not service roads or tracks.</summary>
            private bool SignalRoad(int ri)
            {
                RoadRecord r = _t.Roads[ri];
                return Layout.DrawsRibbon(ri) && RoadWidthModel.IsMotor(r.RoadClass) && r.RoadClass != RoadClass.Track && r.RoadClass != RoadClass.Service &&
                       (r.Flags & RoadFlags.Tunnel) == 0;
            }

            private void CapApproaches(JunctionCap cap, List<Approach> into)
            {
                for (int k = 0; k < cap.ArmRoads.Length; k++)
                {
                    int ri = cap.ArmRoads[k];
                    if (!SignalRoad(ri)) continue;
                    int o = cap.ArmForward[k] ? 1 : -1;
                    // A one-way arm carrying traffic away from the junction has no approach (one-way runs with the points).
                    if ((_t.Roads[ri].Flags & RoadFlags.Oneway) != 0 && o > 0) continue;
                    into.Add(new Approach { Road = ri, CutS = cap.ArmCutS[k], Out = o });
                }
            }

            private void RingApproaches(RoadRing ring, List<Approach> into)
            {
                foreach (RingArm arm in ring.Arms)
                {
                    if (!arm.Entry || !SignalRoad(arm.Road)) continue;
                    into.Add(new Approach { Road = arm.Road, CutS = arm.Cut.S, Out = arm.AtStart ? 1 : -1 });
                }
            }

            /// <summary>The motor road (signal roads only) whose drawn centreline is nearest (x, z).</summary>
            private bool NearestSignalRoad(double x, double z, double maxDist, out int road, out double along, out double lateral, out double dist)
            {
                road = -1;
                along = lateral = 0;
                dist = maxDist;
                for (int i = 0; i < _t.Roads.Count; i++)
                {
                    RoadCentreline c = Layout.Centres[i];
                    if (c == null || c.Count < 2 || !SignalRoad(i)) continue;
                    int seg;
                    double a, l, d;
                    if (!c.Nearest(x, z, out seg, out a, out l, out d) || d >= dist) continue;
                    road = i;
                    along = a;
                    lateral = l;
                    dist = d;
                }
                return road >= 0;
            }

            /// <summary>Farthest a signal pole stands behind the kerb (on a wide footpath or the verge).</summary>
            private const double SignalPoleMaxM = 3.5;

            private readonly List<double> _cand = new List<double>();

            /// <summary>
            /// The mast-arm signal of one approach: walking back from the junction edge, the first section where an arm
            /// (short or long) and a pole offset put the head over the approach lanes of this road (not the junction box,
            /// never over the opposing lanes of a two-way road), as near their middle as the pole's free spots allow,
            /// with the pole behind the traffic's LEFT kerb (on the footpath, else the verge) and the arm reaching right;
            /// only when no section up to 15 m back has room on that kerb (another carriageway alongside, a wall, houses
            /// at the carriageway edge in the old core), with the pole on the traffic's right (the median of a divided
            /// road, or the far kerb) and a mirrored arm reaching left. The
            /// pole is clear of every carriageway, cap, ring, building and the other signal poles, and the head clears
            /// the road by the overhead clearance.
            /// </summary>
            private bool SignalFor(in Approach a, List<double> poles, out StreetProp prop)
            {
                prop = default(StreetProp);
                RoadRecord r = _t.Roads[a.Road];
                bool oneway = (r.Flags & RoadFlags.Oneway) != 0;
                bool dual = Layout.Profiles[a.Road].Dual;
                double len = Layout.Profiles[a.Road].LengthM;
                // Pass 0 walks back along the approach with the pole at the traffic's left kerb only; pass 1, only when
                // no section had room there, with the pole on the traffic's right (the median or the far kerb).
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (double back in SignalBack)
                    {
                        double s = a.CutS + a.Out * back;
                        if (s < 0 || s > len) break;
                        if (Layout.InGap(a.Road, s)) continue;
                        RoadCut c = Layout.DrawnCutAt(_t, a.Road, s);
                        RoadProfile p = Layout.ProfileAt(a.Road, s);
                        // Traffic approaching runs against Out; its left (left-hand traffic: the kerb it keeps to). w is the
                        // lateral position towards the traffic's left from the carriageway centre: the left kerb at +Half.
                        double tx = -a.Out * c.UZ, tz = a.Out * c.UX;
                        double lx = -tz, lz = tx;
                        int side = lx * c.UX + lz * c.UZ > 0 ? 1 : -1;
                        if (side < 0 && dual) return false; // a median on the traffic's left: a wrong-way piece
                        // Head positions allowed and wanted, as w.
                        double wMax = c.Half - 0.6, wMin = oneway ? -c.Half + 0.6 : 0.4, wWant = oneway ? c.Half - Math.Min(c.Half, 4.5) : 0.5 * c.Half - 0.2;
                        if (wMax < wMin) return false;
                        _cand.Clear();
                        for (int right = pass; right == pass; right++)
                        {
                            double foot = (side > 0) == (right == 0) ? p.FootpathLeftM : p.FootpathRightM;
                            double pFoot = right == 1 && dual ? 0.55 : foot > 0 ? Math.Max(0.55, foot - 0.35) : 0.55;
                            for (int ki = 0; ki < SignalKinds.Length; ki++)
                            {
                                StreetPropKind k = right == 0 ? SignalKinds[ki] : MirroredKinds[ki];
                                StreetPropMesher.SignalHead head;
                                StreetPropMesher.TryHangingHead(k, out head);
                                for (double po = 0.55; po <= SignalPoleMaxM + 1e-9; po += 0.25)
                                {
                                    // Left: pole at w = Half + po, head at pole - F; right: pole at w = -Half - po, head at pole + F.
                                    double w = right == 0 ? c.Half + po - head.ForwardM : -c.Half - po + head.ForwardM;
                                    if (w < wMin - 1e-6 || w > wMax + 1e-6) continue;
                                    _cand.Add(Math.Abs(w - wWant) + 0.6 * Math.Max(0, po - pFoot) + 0.3 * ki + 1.5 * right);
                                    _cand.Add(right * SignalKinds.Length + ki);
                                    _cand.Add(po);
                                }
                            }
                        }
                        if (_cand.Count == 0) break; // no arm and pole offset of this side fits the road: the other side
                        while (_cand.Count > 0)
                        {
                            int best = 0;
                            for (int i = 3; i < _cand.Count; i += 3)
                                if (_cand[i] < _cand[best]) best = i;
                            int code = (int)_cand[best + 1];
                            double off = _cand[best + 2];
                            _cand.RemoveRange(best, 3);
                            bool right = code >= SignalKinds.Length;
                            StreetPropKind kind = right ? MirroredKinds[code - SignalKinds.Length] : SignalKinds[code];
                            StreetPropMesher.SignalHead hd;
                            StreetPropMesher.TryHangingHead(kind, out hd);
                            double wPole = right ? -c.Half - off : c.Half + off, wHead = right ? wPole + hd.ForwardM : wPole - hd.ForwardM;
                            double px = c.CX + c.UX * (c.Shift + side * wPole), pz = c.CZ + c.UZ * (c.Shift + side * wPole);
                            double hx = c.CX + c.UX * (c.Shift + side * wHead), hz = c.CZ + c.UZ * (c.Shift + side * wHead);
                            if (!Inside(px, pz) || InBuilding(px, pz) || !Clear(px, pz, 0.35) || NearAny(poles, px, pz, 1.5)) continue;
                            float height = StreetPropMesher.DefaultHeight(kind), y;
                            if (!GeneratedBase(px, pz, height, a.Road, s, out y)) continue;
                            RoadSurfaceHit hit;
                            if (!_q.TrySample(_x0 + hx, _z0 + hz, y + 0.5f, out hit) || !hit.OnCarriageway || hit.Road != a.Road || hit.OnJunction) continue;
                            if (y + hd.BottomM - hit.Y < RoadClearance.MinOverheadClearanceM + 0.1) continue;
                            // The arm reaches from the pole towards the head: right of the traffic from the left kerb, left of it
                            // from the right side.
                            float yaw = right ? Deg(lx, lz) : Deg(-lx, -lz);
                            prop = new StreetProp { X = (float)px, Y = y, Z = (float)pz, YawDeg = yaw, HeightM = height, Kind = kind };
                            if (!FootprintClear(prop)) continue;
                            return true;
                        }
                    }
                }
                return false;
            }
        }
    }
}
