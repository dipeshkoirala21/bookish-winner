using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
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
    /// generated street lamps where OSM has none within 15 m, and signal heads at JNCT SIGNALS junctions without OSM
    /// signals. Everything stands on what is drawn (<see cref="RoadSurfaceQuery"/>): the footpath or kerb next to a
    /// road, a bridge or flyover deck at its parapet line (never the river bed or the ground under the deck), the
    /// terrain elsewhere.
    /// <para>
    /// OSM objects keep their position unless they stand on a drawn carriageway (signals and stops are mapped on road
    /// nodes): road furniture then moves to the nearest kerb, out of every carriageway (a signal to the corner of its
    /// junction), and turns to face the road when the object has no mapped direction; gates on a road turn so traffic
    /// passes through them. About one urban OSM power pole in six becomes an H-frame transformer.
    /// </para>
    /// <para>
    /// Generated lamps: arterial 10-12 m every 30-35 m, local 9-10 m every 25-30 m, alternating sides at the kerb (on
    /// the footpath when there is one), never in old cores (shared lanes) and never on a road or building; about a
    /// third of the roads (by way id) carry solar lamps; the named heritage streets (<see cref="HeritageStreets"/>)
    /// carry the black-and-gold lantern lamps about every 20 m on both sides. On a deck a lamp stands at the parapet
    /// line, and a lit bridge (which draws its own lamp posts) gets none; a generated prop whose base would be more than
    /// <see cref="MaxOffSurfaceM"/> off the drawn road surface beside it (an embankment, a cutting, a river bank under a
    /// deck) or whose pole would reach a deck overhead is skipped. Deterministic. Returns the props added.
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
            bool anySignals = false;
            foreach (PropRecord p in t.Props)
            {
                StreetPropKind kind;
                if (!KindOf(p.Kind, out kind)) continue;
                double x = p.XCm / 100.0, z = p.ZCm / 100.0;
                float hgt = p.HeightDm > 0 ? p.HeightDm / 10f : DefaultHeight(kind);
                float yaw = p.YawDeg;
                if (kind == StreetPropKind.PowerPole && GrammarRng.Mix(GrammarRng.Mix((uint)p.OsmRef, (uint)(p.OsmRef >> 32)), PurposeXfmr) % 6 == 0 && ctx.Urban(x, z))
                {
                    kind = StreetPropKind.TransformerPole;
                    hgt = Math.Max(hgt, 8.5f);
                }
                ctx.FitOsm(kind, p.Has(PropFlags.Yaw), p.Has(PropFlags.OnRoad), p.OsmRef, ref x, ref z, ref yaw);
                float y = ctx.BaseY(x, z, hgt);
                output.Add(new StreetProp { X = (float)x, Y = y, Z = (float)z, YawDeg = yaw, HeightM = hgt, Kind = kind, Osm = true, OsmRef = p.OsmRef });
                if (IsLampPost(kind))
                {
                    osmLamps.Add(x);
                    osmLamps.Add(z);
                }
                if (kind == StreetPropKind.TrafficSignal) anySignals = true;
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
                    output.Add(new StreetProp
                    {
                        X = (float)x, Y = y, Z = (float)z, YawDeg = (float)(yaw < 0 ? yaw + 360 : yaw), HeightM = pole, Kind = kind,
                    });
                }
            }
            if (!anySignals)
            {
                foreach (JunctionRecord j in t.Junctions)
                {
                    if (j.Kind != JunctionKind.Signals) continue;
                    double x, z, yaw;
                    if (!ctx.Corner(j.XCm / 100.0, j.ZCm / 100.0, (uint)(j.OsmNodeId ^ (j.OsmNodeId >> 32)), out x, out z, out yaw)) continue;
                    float y;
                    if (!ctx.GeneratedBase(x, z, 4.5f, out y)) continue;
                    output.Add(new StreetProp { X = (float)x, Y = y, Z = (float)z, YawDeg = (float)yaw, HeightM = 4.5f, Kind = StreetPropKind.TrafficSignal });
                }
            }
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
                x = c.CX + c.UX * off;
                z = c.CZ + c.UZ * off;
                if (!Inside(x, z) || InBuilding(x, z) || OnCarriageway(x, z)) return false;
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
            /// Road furniture mapped on or right next to a carriageway (a signal or stop on a road node) moves to the
            /// nearest kerb, out of every carriageway (a signal to a corner of its junction); objects without a mapped
            /// direction turn to the road (signs face the oncoming traffic, lamps reach over the road, shelters and
            /// benches open to it); gates on a road turn so traffic passes through them.
            /// </summary>
            public void FitOsm(StreetPropKind kind, bool hasYaw, bool onRoad, ulong osmRef, ref double x, ref double z, ref float yaw)
            {
                if (Layout == null || !RoadFurniture(kind)) return;
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
                    if (!NearestRoad(x, z, 25, out ri, out s, out lat, out d)) return;
                    rel = lat - Layout.DrawnCutAt(_t, ri, s).Shift;
                }
                RoadCut c = Layout.DrawnCutAt(_t, ri, s);
                double tx = c.UZ, tz = -c.UX; // travel direction: the left normal U turned clockwise
                int side = rel > 0.05 ? 1 : rel < -0.05 ? -1 : ((osmRef >> 2) & 1) == 0 ? 1 : -1;
                if (kind == StreetPropKind.Gate)
                {
                    if (onRoad || Math.Abs(rel) < c.Half) yaw = Deg(tx, tz);
                    return;
                }
                bool moved = false;
                if (kind == StreetPropKind.TrafficSignal && onCarriageway)
                {
                    double cx, cz, cy;
                    if (Corner(x, z, (uint)(osmRef ^ (osmRef >> 32)), out cx, out cz, out cy))
                    {
                        x = cx;
                        z = cz;
                        if (!hasYaw) yaw = (float)cy;
                        return;
                    }
                }
                double depth = kind == StreetPropKind.BusShelter ? 2.3 : kind == StreetPropKind.Bench ? 0.5 : 0.45;
                if (onCarriageway || Math.Abs(rel) < c.Half + depth - 0.2)
                {
                    // Out to the kerb on the object's side, further out when that is still a carriageway (a junction cap,
                    // a road alongside), then the other side.
                    RoadProfile p = Layout.ProfileAt(ri, s);
                    for (int attempt = 0; attempt < 12 && !moved; attempt++)
                    {
                        int sd = attempt < 6 ? side : -side;
                        double foot = sd > 0 ? p.FootpathLeftM : p.FootpathRightM;
                        double want = c.Half + Math.Max(depth, foot > 0 ? Math.Min(foot * 0.5, 1.2) : 0.6 + p.ShoulderM) + 0.9 * (attempt % 6);
                        double nx = c.CX + c.UX * (c.Shift + sd * want), nz = c.CZ + c.UZ * (c.Shift + sd * want);
                        if (!Inside(nx, nz) || InBuilding(nx, nz) || OnCarriageway(nx, nz)) continue;
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
                if (hasYaw) return;
                switch (kind)
                {
                    case StreetPropKind.BusStop:
                    case StreetPropKind.TaxiStand:
                    case StreetPropKind.TrafficSignal:
                        // Left-hand traffic: the near lane runs along the travel direction when the sign is on its left
                        // (side > 0); the sign faces the traffic coming towards it.
                        yaw = side > 0 ? Deg(-tx, -tz) : Deg(tx, tz);
                        break;
                    default:
                        yaw = Deg(-side * c.UX, -side * c.UZ); // towards the road
                        break;
                }
            }

            private static bool RoadFurniture(StreetPropKind k)
            {
                switch (k)
                {
                    case StreetPropKind.StreetLamp:
                    case StreetPropKind.BusStop:
                    case StreetPropKind.BusShelter:
                    case StreetPropKind.TrafficSignal:
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

            /// <summary>
            /// A signal head's spot at the junction near (jx, jz): on the near-left corner (left-hand traffic) of the
            /// widest arm's approach, set back past the crossing roads, outside every carriageway and building, facing
            /// the approaching traffic. False when no arm is near or no corner is free.
            /// </summary>
            public bool Corner(double jx, double jz, uint seed, out double x, out double z, out double yaw)
            {
                x = jx;
                z = jz;
                yaw = 0;
                if (Layout == null) return false;
                int best = -1;
                double bestHalf = 0, bestS = 0, otherHalf = 0;
                for (int i = 0; i < _t.Roads.Count; i++)
                {
                    RoadCentreline cl = Layout.Centres[i];
                    if (cl == null || cl.Count < 2 || !Layout.DrawsRibbon(i) || !RoadWidthModel.IsMotor(_t.Roads[i].RoadClass)) continue;
                    int seg;
                    double a, l, d;
                    if (!cl.Nearest(jx, jz, out seg, out a, out l, out d) || d > 3.0) continue;
                    double half = Layout.DrawnCutAt(_t, i, a).Half;
                    if (half > bestHalf)
                    {
                        otherHalf = Math.Max(otherHalf, bestHalf);
                        bestHalf = half;
                        best = i;
                        bestS = a;
                    }
                    else otherHalf = Math.Max(otherHalf, half);
                }
                if (best < 0) return false;
                double len = Layout.Profiles[best].LengthM;
                // The approach comes from the longer side of the widest arm (either side when both are long).
                int dir = bestS < 0.5 * len ? 1 : -1;
                if ((seed & 1) == 1 && bestS > 12 && len - bestS > 12) dir = -dir;
                double back = Math.Max(4.0, otherHalf + 2.5);
                for (int tries = 0; tries < 5; tries++, back += 1.5)
                {
                    double s = bestS + dir * back;
                    if (s < 0 || s > len || Layout.InGap(best, s) && tries < 4) continue;
                    RoadCut c = Layout.DrawnCutAt(_t, best, s);
                    double tx = c.UZ, tz = -c.UX;
                    // Traffic towards the node runs along -dir; its left side is the kerb (left-hand traffic).
                    double hx = -dir * tx, hz = -dir * tz;
                    double lx = -hz, lz = hx;
                    double off = c.Half + 0.7;
                    double px = c.CX + c.UX * c.Shift + lx * off, pz = c.CZ + c.UZ * c.Shift + lz * off;
                    if (!Inside(px, pz) || InBuilding(px, pz) || OnCarriageway(px, pz)) continue;
                    x = px;
                    z = pz;
                    yaw = Deg(-hx, -hz); // face the approaching traffic
                    return true;
                }
                return false;
            }
        }
    }
}
