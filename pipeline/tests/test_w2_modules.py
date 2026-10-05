"""Wave 2 pipeline modules (docs/W2_DESIGN.md section 9): area types, style profiles and fronts, road
attributes, junctions, sacred zones and D14 routing, transit, curated heroes, aviation, POI hints."""

from __future__ import annotations

import math

import numpy as np
import pytest
import shapely
from shapely.geometry import Polygon, box

from ghumante_pipeline import (areatype, aviation, curated, junctions, poi_hints, projection, roadattrs, routing,
                               sacred, style, transit)
from ghumante_pipeline import tags as T
from ghumante_pipeline.model import (AdminArea, AreaFeature, AreaKind, AreaType, BuildingArchetype, BuildingFeature,
                                     BuildingFlags, BuildingUse, Extract, HeritageFlags, JunctionFlags, JunctionKind,
                                     JunctionNode, LineFeature, LineKind, LiveryClass, NameRec, ObjectKind, PlaceFeature,
                                     PlaceKind, PoiFeature, PoiFlags, PoiKind, PropFeature, RoadAttrFlags, RoadClass,
                                     RoadFeature, RouteFeature, RouteFlags, RouteMember, Sidewalk, StopFlags,
                                     StyleProfile, Surface, Travel, TransitMode, TreeClass, TurnRestriction)
from ghumante_pipeline.tile_format import AreaFlags


# ---------------------------------------------------------------------------
# Tag parsers
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("raw, kmh", [("50", 50), ("40 km/h", 40), ("30 mph", 48), ("none", 0), (None, 0),
                                      ("NP:urban", 0), ("999", 0), ("५०", 50)])
def test_parse_maxspeed(raw, kmh):
    assert T.parse_maxspeed(raw) == kmh


@pytest.mark.parametrize("tags, expected", [
    ({"sidewalk": "both"}, Sidewalk.BOTH), ({"sidewalk": "no"}, Sidewalk.NONE), ({"sidewalk": "left"}, Sidewalk.LEFT),
    ({"sidewalk": "separate"}, Sidewalk.SEPARATE), ({"sidewalk:both": "yes"}, Sidewalk.BOTH),
    ({"sidewalk:left": "yes", "sidewalk:right": "no"}, Sidewalk.LEFT),
    ({"sidewalk:left": "no", "sidewalk:right": "no"}, Sidewalk.NONE), ({}, Sidewalk.UNKNOWN),
])
def test_parse_sidewalk(tags, expected):
    assert T.parse_sidewalk(tags) == expected


@pytest.mark.parametrize("tags, cls", [
    ({"species": "Ficus religiosa"}, TreeClass.PIPAL), ({"name": "Pipal Bot"}, TreeClass.PIPAL),
    ({"species": "Ficus benghalensis"}, TreeClass.BAR), ({"genus": "Pinus"}, TreeClass.CONIFER),
    ({"leaf_type": "needleleaved"}, TreeClass.CONIFER), ({"species": "Areca catechu"}, TreeClass.PALM),
    ({"leaf_type": "broadleaved"}, TreeClass.BROADLEAF), ({}, TreeClass.UNKNOWN),
])
def test_tree_class(tags, cls):
    assert T.tree_class(tags) == cls


@pytest.mark.parametrize("tags, kind", [
    ({"natural": "tree"}, ObjectKind.TREE), ({"highway": "street_lamp"}, ObjectKind.STREET_LAMP),
    ({"highway": "bus_stop"}, ObjectKind.BUS_STOP), ({"public_transport": "platform", "bus": "yes"}, ObjectKind.BUS_STOP),
    ({"highway": "traffic_signals"}, ObjectKind.TRAFFIC_SIGNALS),
    ({"highway": "crossing", "crossing": "zebra"}, ObjectKind.CROSSING_MARKED),
    ({"highway": "crossing", "crossing:markings": "yes"}, ObjectKind.CROSSING_MARKED),
    ({"highway": "crossing", "crossing": "uncontrolled"}, ObjectKind.CROSSING_UNMARKED),
    ({"aeroway": "gate", "ref": "7"}, ObjectKind.AEROWAY_GATE), ({"aeroway": "parking_position"}, ObjectKind.PARKING_POSITION),
    ({"aeroway": "windsock"}, ObjectKind.WINDSOCK), ({"aeroway": "helipad"}, ObjectKind.HELIPAD),
    ({"man_made": "storage_tank"}, ObjectKind.STORAGE_TANK), ({"barrier": "gate"}, ObjectKind.GATE),
    ({"amenity": "taxi"}, ObjectKind.TAXI_STAND), ({"amenity": "bench"}, ObjectKind.NONE),
])
def test_prop_kind(tags, kind):
    assert T.prop_kind(tags)[0] == kind


def test_chautari_flag():
    k, sub, flags = T.prop_kind({"natural": "tree", "name": "Pipal Chautari", "species": "Ficus religiosa"})
    assert k == ObjectKind.TREE and sub == TreeClass.PIPAL and flags & 4


@pytest.mark.parametrize("tags, mode", [
    ({"route": "bus"}, TransitMode.BUS), ({"route": "minibus"}, TransitMode.MICROBUS),
    ({"route": "microbus"}, TransitMode.MICROBUS), ({"route": "share_taxi", "network": "safatempo"}, TransitMode.TEMPO),
    ({"route": "hiking"}, TransitMode.HIKING), ({"route": "road"}, TransitMode.NONE),
])
def test_route_mode(tags, mode):
    assert T.route_mode(tags) == mode


def test_restriction_kind():
    assert T.restriction_kind({"restriction": "no_left_turn"}) == TurnRestriction.NO_LEFT_TURN
    assert T.restriction_kind({"restriction:motorcar": "only_straight_on"}) == TurnRestriction.ONLY_STRAIGHT_ON
    assert T.restriction_kind({"restriction": "maybe"}) == TurnRestriction.NONE


@pytest.mark.parametrize("tags, kind", [
    ({"place": "square", "name": "Itum Bahal"}, AreaKind.COURTYARD),
    ({"highway": "pedestrian", "area": "yes", "name": "Nasal Chowk"}, AreaKind.COURTYARD),
    ({"name": "Kwa Bahal"}, AreaKind.COURTYARD),
    ({"highway": "residential", "name": "Bahal Marg"}, AreaKind.NONE),  # a closed road is not a courtyard
    ({"building": "yes", "name": "Bahal"}, AreaKind.NONE),
    ({"amenity": "place_of_worship", "name": "Bu Bahal"}, AreaKind.RELIGIOUS),
    ({"aeroway": "apron"}, AreaKind.APRON), ({"aeroway": "apron", "building": "yes"}, AreaKind.APRON),
    ({"area:highway": "traffic_island"}, AreaKind.TRAFFIC_ISLAND),
])
def test_area_kind_w2(tags, kind):
    assert T.area_kind(tags) == kind


# ---------------------------------------------------------------------------
# Area types
# ---------------------------------------------------------------------------
def test_area_type_grid_rules():
    cell = areatype.CELL_M
    box_ = (0.0, 0.0, 6 * cell, cell)
    # Six cells along x: old core by rule, urban, peri-urban, rural, forest, hill (by height).
    xs, areas_ = [], []

    def fill(i, n, a):
        for k in range(n):
            xs.append((i * cell + 5 + (k % 50) * 4.0, 5 + (k // 50) * 4.0))
            areas_.append(a)

    fill(0, 200, 0.46 * cell * cell / 200)
    fill(1, 50, 0.25 * cell * cell / 50)
    fill(2, 20, 0.07 * cell * cell / 20)
    forest = box(4 * cell, 0, 5 * cell, cell)

    def elevation(x, z):  # flat enough everywhere (< 12 %), above 1,650 m only in the last cell
        return np.where(np.asarray(x) >= 5 * cell, 1660.0, 1640.0)

    g = areatype.build_grid(box_, np.array(areas_), np.array(xs), forest_geom=forest, elevation=elevation)
    got = [AreaType(g.at(i * cell + 125.0, 125.0)) for i in range(6)]
    assert got == [AreaType.OLD_CORE, AreaType.URBAN, AreaType.PERI_URBAN, AreaType.RURAL, AreaType.FOREST,
                   AreaType.HILL]
    assert g.at(-1e6, 0.0) == AreaType.UNKNOWN
    # A curated core makes an empty cell OLD_CORE.
    g2 = areatype.build_grid(box_, np.zeros(0), np.zeros((0, 2)), core_geoms=[box(3 * cell, 0, 4 * cell, cell)])
    assert g2.at(3.5 * cell, 125.0) == AreaType.OLD_CORE


def test_grid_cells_are_globally_aligned():
    a = areatype.grid_bounds((1234.0, 5678.0, 3000.0, 6000.0))
    assert a[0] % areatype.CELL_M == 0 and a[1] % areatype.CELL_M == 0


# ---------------------------------------------------------------------------
# Style profiles and fronts
# ---------------------------------------------------------------------------
def test_style_zones_config_loads():
    z = style.load_style_zones()
    ids = [x.id for x in z.zones]
    assert ids[0] == "thamel" and "kathmandu_core" in ids and "boudha_kora" in ids
    assert {x.id for x in z.zones if x.old_core} >= {"kathmandu_core", "patan_core", "bhaktapur_core"}
    assert z.municipalities[12394677][0] == StyleProfile.METRO
    assert z.municipalities[10535035] == (StyleProfile.RIM, StyleProfile.METRO)


def test_assign_profiles_first_match():
    z = style.load_style_zones()
    lon = np.array([85.3110, 85.3080, 85.3620, 85.4300, 85.3300, 85.2000, 85.3290])
    lat = np.array([27.7150, 27.7050, 27.7215, 27.6720, 27.7400, 27.6000, 27.5970])
    at = np.array([AreaType.URBAN] * 5 + [AreaType.RURAL, AreaType.RURAL], dtype=np.uint8)
    places = [PlaceFeature("n", 1, PlaceKind.VILLAGE, 85.3290, 27.5971, NameRec("Chapagaun", "Chapagaun", ""))]
    # A municipality polygon (by relation id) around the Maharajgunj point; a same-named fake relation is ignored.
    admin = [AdminArea(12394677, PlaceKind.LOCAL_LEVEL, 7, NameRec("Wrong name"), box(85.32, 27.73, 85.34, 27.75)),
             AdminArea(999, PlaceKind.LOCAL_LEVEL, 7, NameRec("Kathmandu Metropolitan City"), box(85.1, 27.5, 85.3, 27.7))]
    prof, stats = style.assign_profiles(z, lon, lat, at, places, admin)
    assert [StyleProfile(int(p)) for p in prof] == [StyleProfile.THAMEL, StyleProfile.KATHMANDU_CORE,
                                                    StyleProfile.BOUDHA_KORA, StyleProfile.BHAKTAPUR,
                                                    StyleProfile.METRO, StyleProfile.RIM, StyleProfile.KHOKANA]
    assert "Chapagaun" in stats["towns_found"]


def _segments(lines, ids, classes):
    return style.RoadSegments.build([np.asarray(l, dtype=float) for l in lines], ids, classes)


def test_front_edges_face_the_road_and_corner():
    # A 10 x 8 m house at (0..10, 0..8); a road along z = -4 (south) and another along x = 14 (east).
    ring = np.array([[0, 0], [10, 0], [10, 8], [0, 8]], dtype=float)
    roads = _segments([[[-50, -4], [50, -4]], [[14, -50], [14, 50]]], [1, 2],
                      [RoadClass.RESIDENTIAL, RoadClass.TERTIARY])
    fe, fdm, se = style.front_edges(ring, roads)
    assert fe == 0 and fdm == 40 and se == 1
    # A footway does not count as a front road.
    roads2 = _segments([[[-50, -4], [50, -4]]], [3], [RoadClass.FOOTWAY])
    assert style.front_edges(ring, roads2) == (style.EDGE_NONE, 0, style.EDGE_NONE)
    # The road behind the north wall faces edge 2 only.
    roads3 = _segments([[[-50, 12], [50, 12]]], [4], [RoadClass.PRIMARY])
    assert style.front_edges(ring, roads3)[0] == 2


def test_hint_flags():
    b = BuildingFeature("w", 1, np.array([[0, 0], [1, 0], [1, 1]], dtype=float), holes=[np.zeros((3, 2))],
                        extra={"building:structure": "reinforced_concrete", "start_date": "1935"},
                        name=NameRec("Kaiser Mahal"))
    f = style.hint_flags(b)
    assert f & 1 and f & 16 and f & 8  # courtyard host, RCC, Rana hint
    t = BuildingFeature("w", 2, np.zeros((3, 2)), religion="hindu", building_raw="temple",
                        extra={"start_date": "1700"})
    assert not style.hint_flags(t) & 8  # temples are never RANA


# ---------------------------------------------------------------------------
# Road attributes
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("cls, area, width, lanes, oneway, expected", [
    (RoadClass.TRUNK, AreaType.URBAN, None, 4, False, 14.0), (RoadClass.TRUNK, AreaType.URBAN, None, 2, True, 7.0),
    (RoadClass.TRUNK, AreaType.HILL, None, 2, False, 7.5), (RoadClass.RESIDENTIAL, AreaType.OLD_CORE, None, 0, False, 4.0),
    (RoadClass.TERTIARY, AreaType.FOREST, None, 0, False, 4.5), (RoadClass.PRIMARY, AreaType.URBAN, None, 4, False, 14.0),
    (RoadClass.RESIDENTIAL, AreaType.URBAN, 1.0, 0, False, 2.5), (RoadClass.SERVICE, AreaType.URBAN, 99.0, 0, False, 40.0),
])
def test_real_width(cls, area, width, lanes, oneway, expected):
    assert roadattrs.real_width_m(int(cls), int(area), width, lanes, oneway) == expected


def _road(oid, pts, cls=RoadClass.TRUNK, **kw):
    pts = np.asarray(pts, dtype=float)
    return RoadFeature(osm_id=oid, cls=cls, lonlat=pts, node_ids=np.arange(len(pts)) + oid * 100, **kw)


def test_dual_carriageway_pairing_and_service_road():
    a = _road(1, [[0, 0], [500, 0]], oneway=1, ref="NH39")
    b = _road(2, [[500, 7.4], [0, 7.4]], oneway=1, ref="NH39")
    svc = _road(3, [[0, -9.3], [500, -9.3]], RoadClass.PRIMARY, oneway=1)
    other = _road(4, [[500, 30], [0, 30]], oneway=1, ref="F21")
    games = [r.lonlat for r in (a, b, svc, other)]
    info = roadattrs.pair_dual_carriageways([a, b, svc, other], games)
    assert info.partner == {1: 2, 2: 1}
    assert info.spacing_m[1] == pytest.approx(7.4, abs=0.01)
    assert info.service == {3}
    rec = roadattrs.road_attr(a, games[0], int(AreaType.URBAN), False, info, set(), b, None)
    assert rec.flags & RoadAttrFlags.DUAL and rec.partner_way_id == 2 and rec.median_cm == 100  # 7.4 - 7 -> 1 m min


def test_corridor_samples():
    # Buildings 6 m left and 4 m right of a 60 m road: corridor = 2 * 4 m = 8 m = 80 dm.
    blds = [np.array([[-10, 6], [80, 6], [80, 12], [-10, 12]], dtype=float),
            np.array([[-10, -10], [30, -10], [30, -4], [-10, -4]], dtype=float)]
    idx = roadattrs.BuildingIndex.build(blds)
    pts = np.array([[0, 0], [60, 0]], dtype=float)
    cor = roadattrs.corridor_samples(pts, idx)
    assert len(cor) == 4  # 0, 20, 40, 60 m
    assert cor[0] == 80 and cor[1] == 80
    assert cor[2] == 0 and cor[3] == 0  # the right side is open past x = 30
    # A sample inside a footprint takes the previous valid value.
    blds.append(np.array([[38, -1], [42, -1], [42, 1], [38, 1]], dtype=float))
    cor2 = roadattrs.corridor_samples(pts, roadattrs.BuildingIndex.build(blds))
    assert cor2[2] == cor2[1]
    # Mapping-offset guard: with a tagged 10 m width a bounded sample is never narrower; open sides stay 0.
    cor3 = roadattrs.corridor_samples(pts, idx, min_dm=100)
    assert list(cor3) == [100, 100, 0, 0]


def test_corridor_guard_uses_the_tagged_width():
    blds = [np.array([[-10, 3], [80, 3], [80, 12], [-10, 12]], dtype=float),
            np.array([[-10, -12], [80, -12], [80, -3], [-10, -3]], dtype=float)]
    idx = roadattrs.BuildingIndex.build(blds)
    r = _road(13, [[0, 0], [60, 0]], RoadClass.PRIMARY, surface=Surface.ASPHALT)
    plain = roadattrs.road_attr(r, r.lonlat, int(AreaType.URBAN), False, roadattrs.DualInfo(), set(), None, idx)
    assert list(plain.corridor_dm) == [60] * 4
    r.width_m = 7.25
    tagged = roadattrs.road_attr(r, r.lonlat, int(AreaType.URBAN), False, roadattrs.DualInfo(), set(), None, idx)
    assert list(tagged.corridor_dm) == [73] * 4


def test_static_attrs_reverse_for_backward_oneways():
    r = _road(9, [[0, 0], [10, 0]], RoadClass.SECONDARY, oneway=-1,
              extra={"sidewalk": "left", "lanes:forward": "2", "lanes:backward": "1", "lit": "yes",
                     "junction": "roundabout", "maxspeed": "40"})
    sw, fwd, bwd, ms, flags = roadattrs.static_attrs(r, roadattrs.DualInfo(), {9})
    assert sw == Sidewalk.RIGHT and (fwd, bwd) == (1, 2) and ms == 40
    assert flags & RoadAttrFlags.LIT and flags & RoadAttrFlags.RING_MEMBER and flags & RoadAttrFlags.BUS_ROUTE
    foot = _road(10, [[0, 0], [10, 0]], RoadClass.FOOTWAY, access=Travel.FOOT)
    assert roadattrs.static_attrs(foot, roadattrs.DualInfo(), set())[4] & RoadAttrFlags.NO_MOTOR


def test_paintable_flag():
    r = _road(11, [[0, 0], [50, 0]], RoadClass.PRIMARY, surface=Surface.ASPHALT)
    rec = roadattrs.road_attr(r, r.lonlat, int(AreaType.URBAN), True, roadattrs.DualInfo(), set(), None, None)
    assert rec.flags & RoadAttrFlags.PAINTABLE and rec.flags & RoadAttrFlags.HERITAGE_PEDESTRIAN
    lane = _road(12, [[0, 0], [50, 0]], RoadClass.RESIDENTIAL, surface=Surface.ASPHALT)
    rec2 = roadattrs.road_attr(lane, lane.lonlat, int(AreaType.OLD_CORE), False, roadattrs.DualInfo(), set(), None,
                               None)
    assert not rec2.flags & RoadAttrFlags.PAINTABLE  # 4 m real


# ---------------------------------------------------------------------------
# Junctions
# ---------------------------------------------------------------------------
def _planar(lon, lat):
    return np.asarray(lon, dtype=float), np.asarray(lat, dtype=float)


def _ring_road(oid, cx, cz, r, n=24, tag="roundabout"):
    t = np.linspace(0, 2 * math.pi, n + 1)
    pts = np.stack([cx + r * np.cos(t), cz + r * np.sin(t)], axis=1)
    ids = np.arange(n + 1) + oid * 1000
    ids[-1] = ids[0]
    return RoadFeature(osm_id=oid, cls=RoadClass.PRIMARY, lonlat=pts, node_ids=ids, extra={"junction": tag})


def test_junctions_ring_signals_police():
    ring = _ring_road(7, 0.0, 0.0, 20.0)
    arm = RoadFeature(osm_id=8, cls=RoadClass.PRIMARY, lonlat=np.array([[20.0, 0.0], [120.0, 0.0]]),
                      node_ids=np.array([7000, 9001]))
    # A 4-arm trunk crossing at (500, 0): node 5000 shared by two through ways.
    ew = RoadFeature(osm_id=20, cls=RoadClass.TRUNK, lonlat=np.array([[400.0, 0.0], [500.0, 0.0], [600.0, 0.0]]),
                     node_ids=np.array([4001, 5000, 4002]))
    ns = RoadFeature(osm_id=21, cls=RoadClass.SECONDARY, lonlat=np.array([[500.0, -100.0], [500.0, 0.0], [500.0, 100.0]]),
                     node_ids=np.array([4003, 5000, 4004]))
    sig = RoadFeature(osm_id=22, cls=RoadClass.RESIDENTIAL,
                      lonlat=np.array([[1000.0, 0.0], [1000.0, 50.0], [1000.0, 100.0]]), node_ids=np.array([1, 2, 3]))
    side = RoadFeature(osm_id=23, cls=RoadClass.RESIDENTIAL, lonlat=np.array([[950.0, 50.0], [1000.0, 50.0]]),
                       node_ids=np.array([4, 2]))
    island = AreaFeature("w", 55, AreaKind.PARK, Polygon([(-8, -8), (8, -8), (8, 8), (-8, 8)]))
    nodes = [JunctionNode(2, 1000.0, 50.0, "traffic_signals"), JunctionNode(999, 3000.0, 0.0, "mini_roundabout")]
    cross = [PropFeature("n", 77, ObjectKind.CROSSING_MARKED, 500.0, 10.0)]
    cfg = junctions.ChowkConfig(snap_m=120.0, chowks=[
        junctions.Chowk("ring", "Ring Chowk", 5.0, 5.0, "2-4"), junctions.Chowk("big", "Big Chowk", 520.0, 10.0),
        junctions.Chowk("sig", "Signal Chowk", 1010.0, 40.0), junctions.Chowk("lost", "Lost", 9000.0, 9000.0)])
    out, st = junctions.find_junctions([ring, arm, ew, ns, sig, side], nodes, [island], cross, cfg,
                                       to_game=_planar)
    by_kind = {j.kind: j for j in out}
    r = by_kind[JunctionKind.ROUNDABOUT]
    assert r.ring_diameter_m == pytest.approx(40.0, rel=0.02) and r.arms == 1
    assert r.flags & JunctionFlags.HAS_ISLAND_AREA and r.island_area_osm_ref == (55 << 1)
    assert r.island_diameter_m == pytest.approx(2 * math.sqrt(256 / math.pi), rel=1e-6)
    assert r.flags & JunctionFlags.HAS_POLICE and r.flags & JunctionFlags.OFFICERS_2_4 and r.name.en == "Ring Chowk"
    big = by_kind[JunctionKind.SYNTHETIC_ISLAND]
    assert big.osm_node_id == 5000 and big.arms == 4 and big.flags & JunctionFlags.CROSSINGS_MARKED
    s = by_kind[JunctionKind.SIGNALS]
    assert s.flags & JunctionFlags.HAS_SIGNALS and s.flags & JunctionFlags.HAS_POLICE  # police at a signal keeps SIGNALS
    assert JunctionKind.MINI_ROUNDABOUT not in by_kind  # its node is not on a road of the extract
    assert st["chowks_unmatched"] == ["lost"]


def test_chowks_config():
    cfg = junctions.load_chowks()
    assert len(cfg.chowks) == 33 and len({c.id for c in cfg.chowks}) == 33
    big = {c.id for c in cfg.chowks if c.officers == "2-4"}
    assert big == {"kalanki", "koteshwor", "chabahil", "narayan_gopal", "satdobato", "maitighar", "thapathali",
                   "tripureshwor", "jamal", "lainchaur"}
    for c in cfg.chowks:
        assert 85.25 < c.lon < 85.40 and 27.64 < c.lat < 27.75


# ---------------------------------------------------------------------------
# Sacred zones and D14 routing
# ---------------------------------------------------------------------------
def test_area_flags_and_zones():
    rel = AreaFeature("w", 1, AreaKind.RELIGIOUS, box(0, 0, 10, 10))
    sq = AreaFeature("r", 2, AreaKind.PEDESTRIAN, box(20, 0, 30, 10), tags={"heritage": "1"})
    plain = AreaFeature("w", 3, AreaKind.PEDESTRIAN, box(40, 0, 50, 10))
    comp = AreaFeature("w", 4, AreaKind.PARK, box(60, 0, 70, 10))
    cs = {("w", 4)}
    assert sacred.area_flags(rel, cs, set()) == int(AreaFlags.SACRED_NO_VEHICLE)
    assert sacred.area_flags(sq, cs, set()) == int(AreaFlags.SACRED_NO_VEHICLE | AreaFlags.HERITAGE_ZONE)
    assert sacred.area_flags(plain, cs, set()) == 0
    assert sacred.area_flags(comp, cs, set()) == int(AreaFlags.SACRED_NO_VEHICLE | AreaFlags.HERITAGE_ZONE)
    z = sacred.SacredZones.build([rel, sq, plain, comp], [a.polygon for a in (rel, sq, plain, comp)], cs, set())
    assert z.count == 3 and list(z.contains(np.array([5.0, 45.0, 65.0]), np.array([5.0, 5.0, 5.0]))) == [True, False, True]


def test_routing_drops_motor_modes_inside_zones():
    # A straight primary road through a compound (x 100..300), split at shared nodes.
    roads = [RoadFeature(osm_id=1, cls=RoadClass.PRIMARY, lonlat=np.array([[0.0, 0.0], [100.0, 0.0]]),
                         node_ids=np.array([1, 2])),
             RoadFeature(osm_id=2, cls=RoadClass.PRIMARY, lonlat=np.array([[100.0, 0.0], [200.0, 0.0], [300.0, 0.0]]),
                         node_ids=np.array([2, 3, 4])),
             RoadFeature(osm_id=3, cls=RoadClass.PRIMARY, lonlat=np.array([[300.0, 0.0], [400.0, 0.0]]),
                         node_ids=np.array([4, 5]))]
    zone = box(90, -20, 310, 20)

    def block(x, z):
        return shapely.contains_xy(zone, x, z)

    def planar(ll):
        return ll[:, 0].copy(), ll[:, 1].copy()

    g = routing.build_graph(roads, to_game=planar, motor_block=block)
    assert g.build_info["sacred_pieces"] == 1
    a, b = routing.nearest_node(g, 0.0, 0.0, Travel.FOOT), routing.nearest_node(g, 400.0, 0.0, Travel.FOOT)
    assert routing.route(g, a, b, Travel.FOOT) is not None
    assert routing.route(g, a, b, Travel.CAR) is None
    assert routing.route(g, a, b, Travel.BICYCLE) is not None
    g0 = routing.build_graph(roads, to_game=planar)
    assert routing.route(g0, a, b, Travel.CAR) is not None


def test_prepare_courtyards():
    ktm_core = Polygon([(85.30, 27.69), (85.32, 27.69), (85.32, 27.72), (85.30, 27.72)])
    inside = AreaFeature("w", 1, AreaKind.COURTYARD, box(85.305, 27.70, 85.3051, 27.7001), name=NameRec("Nasal Chowk"),
                         tags={"place": "square"})
    outside = AreaFeature("w", 2, AreaKind.COURTYARD, box(85.40, 27.70, 85.4001, 27.7001),
                          name=NameRec("Koteshwor Chowk"), tags={"place": "square"})
    dropped = AreaFeature("w", 3, AreaKind.COURTYARD, box(85.41, 27.70, 85.4101, 27.7001), name=NameRec("Milan Chowk"))
    bahal = AreaFeature("w", 4, AreaKind.COURTYARD, box(85.42, 27.70, 85.4201, 27.7001), name=NameRec("Bu Bahal"))
    host = BuildingFeature("r", 9, np.array([[0, 0], [3, 0], [3, 3], [0, 3]], dtype=float),
                           holes=[np.array([[1, 1], [1, 2], [2, 2], [2, 1]], dtype=float)], name=NameRec("Itum Bahal"))
    areas = [inside, outside, dropped, bahal]
    st = sacred.prepare_courtyards(areas, [host], [ktm_core])
    kinds = {(a.osm_type, a.osm_id): a.kind for a in areas}
    assert kinds[("w", 1)] == AreaKind.COURTYARD and kinds[("w", 2)] == AreaKind.PEDESTRIAN
    assert ("w", 3) not in kinds and kinds[("w", 4)] == AreaKind.COURTYARD
    assert kinds[("r", 9)] == AreaKind.COURTYARD and st["courtyard_holes"] == 1


# ---------------------------------------------------------------------------
# POI hints and parts
# ---------------------------------------------------------------------------
def _sq(oid, x, y, s, **kw):
    pts = np.array([[x, y], [x + s, y], [x + s, y + s], [x, y + s]], dtype=float)
    return BuildingFeature("w", oid, pts, **kw)


def test_poi_footprints_and_parts():
    d = 0.0001
    temple = _sq(1, 85.0, 27.0, d)
    shopblock = _sq(2, 85.01, 27.0, d)
    big = _sq(3, 85.02, 27.0, 0.01)  # ~1 km: too big to become a temple
    part = _sq(4, 85.0 + 0.2 * d, 27.0 + 0.2 * d, 0.5 * d, flags=BuildingFlags.PART)
    pois = [PoiFeature("n", 10, PoiKind.TEMPLE_HINDU, 85.0 + 0.5 * d, 27.0 + 0.5 * d, tags={"religion": "hindu"}),
            PoiFeature("n", 11, PoiKind.SHOP, 85.01 + 0.5 * d, 27.0 + 0.5 * d),
            PoiFeature("n", 12, PoiKind.CAFE, 85.01 + 0.4 * d, 27.0 + 0.5 * d),
            PoiFeature("n", 13, PoiKind.TEMPLE_HINDU, 85.025, 27.005)]
    bl = [temple, shopblock, big, part]
    shops, st = poi_hints.apply_poi_footprints(bl, pois)
    assert temple.use == BuildingUse.RELIGIOUS and temple.religion == "hindu" and temple.building_raw == "temple"
    assert pois[0].flags & PoiFlags.HAS_FOOTPRINT
    assert big.use == BuildingUse.UNKNOWN
    assert list(shops) == [0, 2, 0, 0]
    hosts, pst = poi_hints.link_parts(bl)
    assert hosts == {3: 0} and temple.flags & BuildingFlags.HAS_PARTS and pst["hosts"] == 1


# ---------------------------------------------------------------------------
# Transit
# ---------------------------------------------------------------------------
def _ll_road(oid, nodes, **kw):
    ll = np.array([[85.30 + 0.001 * n, 27.70] for n in nodes], dtype=float)
    return RoadFeature(osm_id=oid, cls=RoadClass.PRIMARY, lonlat=ll, node_ids=np.array([1000 + n for n in nodes]), **kw)


def test_build_routes_chain_and_stops():
    ex = Extract(region="t")
    ex.roads = [_ll_road(1, [0, 1, 2]), _ll_road(2, [4, 3, 2]), _ll_road(3, [4, 5])]
    ex.props = [PropFeature("n", 50, ObjectKind.BUS_STOP, 85.3005, 27.70005),
                PropFeature("n", 51, ObjectKind.BUS_STOP, 85.3006, 27.70005),  # merged with 50 (10 m)
                PropFeature("n", 52, ObjectKind.BUS_STOP, 85.3045, 27.7001)]
    ex.routes = [RouteFeature(1, "bus", {"route": "bus", "operator": "Sajha Yatayat", "ref": "1"},
                              [RouteMember("w", 1, ""), RouteMember("w", 2, ""), RouteMember("w", 3, "")]),
                 RouteFeature(2, "minibus", {"route": "minibus"}, [RouteMember("w", 3, ""), RouteMember("w", 1, "")])]
    rs, st = transit.build_routes(ex, transit.load_transit(), (85.29, 27.69, 85.31, 27.71))
    bus, micro = rs.routes
    assert bus.ways == [(1, True), (2, False), (3, True)] and bus.livery == LiveryClass.CITY_GREEN
    assert bus.flags & RouteFlags.STOPS_INFERRED and not bus.flags & RouteFlags.HAS_GAPS
    assert [s.osm_ref >> 2 for s in bus.stops] == [50, 52]
    assert bus.stops[0].flags & StopFlags.TERMINAL and bus.stops[-1].flags & StopFlags.TERMINAL
    assert bus.headway_peak_s == 300 and bus.length_m == pytest.approx(5 * 98.8, rel=0.02)
    assert micro.mode == TransitMode.MICROBUS and micro.flags & RouteFlags.HAS_GAPS
    assert transit.bus_way_ids(rs) == {1, 2, 3}
    data = transit.encode_ghrt(rs)
    back, names = transit.decode_ghrt(data)
    assert [r.ways for r in back.routes] == [r.ways for r in rs.routes]
    assert transit.encode_ghrt(back) == data
    with pytest.raises(ValueError):
        transit.decode_ghrt(data[:-1])


# ---------------------------------------------------------------------------
# Curated heroes
# ---------------------------------------------------------------------------
def test_heritage_config_stage1():
    recs = curated.load_heritage()
    ids = [r["id"] for r in recs]
    assert len(ids) == len(set(ids)) >= 35
    for must in ("her.ktm.boudhanath", "her.ktm.swayambhunath", "her.ktm.pashupatinath", "her.ktm.taleju",
                 "her.ptn.krishna_mandir", "her.bkt.nyatapola", "her.ktm.annapurna_asan", "her.ktm.dharahara"):
        assert must in ids
    by = {r["id"]: r for r in recs}
    assert by["her.bkt.nyatapola"]["tiers"] == 5 and by["her.bkt.nyatapola"]["plinth_levels"] == 5
    assert by["her.ktm.boudhanath"]["osm"] == "w56688295" and by["her.ktm.boudhanath"]["compound"] == "w56688296"
    assert by["her.ktm.boudhanath"]["kora"] == "CLOCKWISE"
    for r in recs:  # Devanagari is never typed into the curated file (ADR-005)
        assert not any("ऀ" <= ch <= "ॿ" for ch in str(r))


def test_resolve_hides_and_overrides():
    d = 0.0001
    hero = _sq(100, 85.30, 27.70, 2 * d, name=NameRec("Nyatapola", "", "न्यातपोल"))
    part = _sq(101, 85.30 + 0.5 * d, 27.70 + 0.5 * d, d, flags=BuildingFlags.PART)
    overlap = _sq(102, 85.30 + 1.9 * d, 27.70, 2 * d)  # 5% overlap: stays
    inside = _sq(103, 85.30 + 0.2 * d, 27.70 + 0.2 * d, 0.3 * d)
    palace = _sq(104, 85.31, 27.70, 0.01)  # a huge block holding a gate node
    ex = Extract(region="t", buildings=[hero, part, overlap, inside, palace])
    recs = [{"id": "her.a", "kind": "PAGODA", "osm": "w100", "tiers": 5, "yaw_deg": 180},
            {"id": "her.b", "kind": "GATE", "osm": "n5", "attrs": {"plan_m": "4,2"}},
            {"id": "her.c", "kind": "RELIEF", "manual": {"lon": 85.302, "lat": 27.702, "source": "x"}},
            {"id": "her.d", "kind": "STUPA", "osm": "n6"}]
    res, ov, st = curated.resolve(recs, ex, {5: (85.312, 27.702, None)}, bbox_lonlat=(85.2, 27.6, 85.4, 27.8))
    assert [r.rec["id"] for r in res] == ["her.a", "her.b", "her.c"] and st["not_in_extract"] == ["her.d"]
    assert sorted(res[0].hidden) == [0, 1, 3]
    assert hero.flags & BuildingFlags.LANDMARK and part.flags & BuildingFlags.LANDMARK
    assert not overlap.flags & BuildingFlags.LANDMARK and not palace.flags & BuildingFlags.LANDMARK
    assert ov == {0: BuildingArchetype.TEMPLE_PAGODA}
    assert res[1].footprint == -1  # the palace block is far bigger than the gate's plan
    assert res[2].flags & HeritageFlags.MANUAL_POSITION and res[0].name_ne == "न्यातपोल"
    hr = curated.to_records(res, ex, 10)
    data = curated.encode_ghcd(hr)
    back = curated.decode_ghcd(data)
    assert [h.id for h in back] == ["her.a", "her.b", "her.c"] and back[0].tiers == 5 and back[0].yaw_cdeg == 18000
    assert back[1].yaw_cdeg == curated.YAW_UNKNOWN
    assert curated.encode_ghcd(back) == data
    with pytest.raises(ValueError):
        curated.decode_ghcd(data[:-2] + b"xx")


def test_resolve_plan_rect_cuts_a_wing_but_never_hides_it():
    # A tower node in the courtyard of a ring-shaped palace wing (Lohan Chowk): its plan rectangle clips the wing.
    d = 0.0001
    lon, lat = 85.30, 27.70
    outer = np.array([[lon - 2 * d, lat - 2 * d], [lon + 2 * d, lat - 2 * d], [lon + 2 * d, lat + 2 * d],
                      [lon - 2 * d, lat + 2 * d]])
    hole = np.array([[lon - d, lat - d], [lon - d, lat + d], [lon + d, lat + d], [lon + d, lat - d]])
    wing = BuildingFeature("r", 200, outer, [hole])
    before = curated._area_m2(Polygon(outer, [hole]))
    ex = Extract(region="t", buildings=[wing])
    node = (lon + 0.8 * d, lat, None)  # the 10 m plan reaches into the wing's east range
    recs = [{"id": "her.t", "kind": "TOWER", "osm": "n7", "yaw_deg": 180, "attrs": {"plan_m": "10,10"}},
            {"id": "her.g", "kind": "GATE", "osm": "n8"}]
    res, ov, st = curated.resolve(recs, ex, {7: node, 8: (lon + 5 * d, lat, None)})
    assert res[0].hidden == [] and not wing.flags & BuildingFlags.LANDMARK  # the centroid is in the courtyard
    assert st["cut_buildings"] == 1 and st["unhidden_overlaps"] == 0
    rect = curated._plan_rect(node[0], node[1], recs[0])
    assert abs(curated._area_m2(rect) - 100.0) < 0.5
    cut = Polygon(wing.outer, wing.holes)
    assert cut.is_valid and len(wing.holes) == 1 and curated._area_m2(cut) < before - 1.0
    assert curated._area_m2(cut.intersection(rect)) < 0.01
    assert st["no_hide_zone"] == ["her.g"]
    # A plan wholly inside a block is reported, not punched as a hole.
    block = _sq(300, 85.31, 27.70, 0.01)
    ex2 = Extract(region="t", buildings=[block])
    _, _, st2 = curated.resolve(recs[:1], ex2, {7: (85.315, 27.705, None)})
    assert st2["cut_buildings"] == 0 and st2["unhidden_overlaps"] == 1 and len(block.holes) == 0


# ---------------------------------------------------------------------------
# Aviation
# ---------------------------------------------------------------------------
def test_aviation_thresholds_are_the_osm_runway_ends():
    ex = Extract(region="t")
    main = np.array([[85.3639103, 27.7070042], [85.3534132, 27.6839528]])
    ex.lines = [LineFeature(340948564, LineKind.RUNWAY, main, width_m=45.0),
                LineFeature(1453500827, LineKind.RUNWAY, np.array([[85.3534132, 27.6839528], [85.3523298, 27.6815732]]))]
    av, st = aviation.build_aviation(ex, "tia")
    thr = av["airport"]["runway"]["thresholds"]
    for key, (lon, lat) in (("02", main[1]), ("20", main[0])):
        x, z = projection.lonlat_to_game(lon, lat)
        assert abs(thr[key]["x"] - float(x)) < 0.01 and abs(thr[key]["z"] - float(z)) < 0.01
    assert 2700 < av["airport"]["runway"]["length_between_thresholds_m"] < 2800
    assert 20.0 < av["airport"]["runway"]["heading_game_deg"] < 24.0
    assert av["airport"]["runway"]["pavement_ends"]["south"]["lat"] == pytest.approx(27.6815732)
    assert len(av["procedures"]["A1"]["points"]) == 17 and av["procedures"]["D1"]["points"][-1]["alt_m"] == 4115
    hours = av["schedule"]["movements_per_real_hour"]
    day = {c: sum(hours[str(h)][c] for h in range(24)) for c in ("DOM_TP", "HELI", "INTL_NB", "INTL_WB")}
    # The hourly rows of AV 4.2 / W2_DESIGN 8.2 copied unchanged (their printed "Day total" row says 240 and 7).
    assert day == {"DOM_TP": 220, "HELI": 50, "INTL_NB": 87, "INTL_WB": 8}
    assert aviation.dumps(av) == aviation.dumps(aviation.build_aviation(ex, "tia")[0])
    assert aviation.build_aviation(Extract(region="none"), "tia")[0] is None
