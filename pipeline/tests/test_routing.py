"""GHRG routing graph: travel profiles, graph building, binary format and the A* reference router."""

from __future__ import annotations

import json
import math
import struct

import numpy as np
import pytest
from scipy.sparse import csr_matrix
from scipy.sparse.csgraph import dijkstra

from ghumante_pipeline.binio import Reader
from ghumante_pipeline.model import (ALL_TRAVEL, MOTOR_TRAVEL, NameRec, RoadClass, RoadFeature, SacScale, Surface,
                                     Travel)
from ghumante_pipeline.projection import lonlat_to_game
from ghumante_pipeline.routing import (EDGE_BRIDGE, EDGE_FORD, EDGE_LINK, EDGE_SIZE, EDGE_TUNNEL, ELEV_UNKNOWN,
                                       HEADER_SIZE, MAGIC, MAX_GEOM_POINTS, NODE_SIZE, PROFILES, TRAVEL_PROFILES,
                                       VERSION, RoutingGraph, build_graph, climb_factor, decode_graph, edge_time_s,
                                       edge_times, encode_graph, encode_svarints, eta_by_profile, get_profile,
                                       nearest_node, read_graph, route, route_geometry, travel_profiles_table,
                                       usable_mask, write_graph)

F, B, M, C, J, BUS, H = (Travel.FOOT, Travel.BICYCLE, Travel.MOTORBIKE, Travel.CAR, Travel.JEEP, Travel.BUS,
                         Travel.HORSE)
KTM = (85.3240, 27.7172)  # Kathmandu Durbar Square


def planar(lonlat: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Test ``to_game``: the 'lon/lat' columns already are game metres."""
    a = np.asarray(lonlat, dtype=np.float64)
    return a[:, 0].copy(), a[:, 1].copy()


def road(osm_id: int, cls: RoadClass, pts, ids, **kw) -> RoadFeature:
    return RoadFeature(osm_id=osm_id, cls=cls, lonlat=np.asarray(pts, dtype=np.float64),
                       node_ids=np.asarray(ids, dtype=np.int64), **kw)


def node_of(g: RoutingGraph, osm: int) -> int:
    hit = np.flatnonzero(g.node_osm_id == osm)
    assert hit.size == 1, f"OSM node {osm} is not a graph node"
    return int(hit[0])


def edges_between(g: RoutingGraph, u: int, v: int) -> list[int]:
    return [e for e in g.edges_from(u) if int(g.edge_target[e]) == v]


# ---------------------------------------------------------------------------
# Travel profiles
# ---------------------------------------------------------------------------
def test_profile_tables_shape_and_spec_values():
    assert set(TRAVEL_PROFILES) == set(PROFILES) and len(PROFILES) == 7
    for t, p in TRAVEL_PROFILES.items():
        assert p.travel == t and p.name == t.name
        assert len(p.speed_kmh) == len(RoadClass) and len(p.surface_factor) == 4
        assert p.speed_kmh[RoadClass.UNKNOWN] == 0.0
        assert p.surface_factor[0] == 1.0 and max(p.surface_factor) == 1.0
    mb = TRAVEL_PROFILES[M].speed_kmh
    assert [mb[c] for c in (RoadClass.TRUNK, RoadClass.PRIMARY, RoadClass.SECONDARY, RoadClass.TERTIARY,
                            RoadClass.UNCLASSIFIED, RoadClass.RESIDENTIAL, RoadClass.TRACK, RoadClass.PATH)] == \
        [80, 70, 60, 50, 40, 30, 25, 12]
    foot = TRAVEL_PROFILES[F]
    assert {foot.speed_kmh[c] for c in RoadClass if c not in (RoadClass.UNKNOWN, RoadClass.STEPS)} == {5.0}
    assert foot.speed_kmh[RoadClass.STEPS] == 4.0 and foot.alpine_speed_kmh == 3.5
    assert TRAVEL_PROFILES[J].surface_factor == (1.0, 0.9, 0.85, 0.6)
    for t in (B, M, C, BUS):
        assert TRAVEL_PROFILES[t].surface_factor == (1.0, 0.75, 0.6, 0.35)
    assert [TRAVEL_PROFILES[t].max_sac for t in PROFILES] == [6, 2, 1, 0, 0, 0, 3]
    assert [TRAVEL_PROFILES[t].climb_k for t in (F, B, M, C, J, BUS)] == [6, 8, 1, 1, 1, 1]
    # BUS slower than CAR everywhere a car can go
    car, bus = TRAVEL_PROFILES[C].speed_kmh, TRAVEL_PROFILES[BUS].speed_kmh
    assert all(b <= c for b, c in zip(bus, car)) and bus[RoadClass.PRIMARY] < car[RoadClass.PRIMARY]
    assert TRAVEL_PROFILES[C].max_speed_kmh == 90.0 and TRAVEL_PROFILES[F].max_speed_kmh == 5.0


def test_profile_speed_rules():
    car, jeep, foot = TRAVEL_PROFILES[C], TRAVEL_PROFILES[J], TRAVEL_PROFILES[F]
    assert car.speed(RoadClass.TERTIARY, Surface.ASPHALT, 0) == 50.0
    assert car.speed(RoadClass.TERTIARY, Surface.MUD, 0) == pytest.approx(17.5)
    assert jeep.speed(RoadClass.TERTIARY, Surface.MUD, 0) == pytest.approx(30.0)
    assert jeep.speed(RoadClass.TRACK, Surface.GRAVEL, 0) == pytest.approx(27.0)
    assert car.speed(RoadClass.TERTIARY, Surface.UNKNOWN, 0) == pytest.approx(30.0)  # UNKNOWN -> DIRT group
    assert car.speed(RoadClass.TERTIARY, 200, 0) == pytest.approx(30.0)  # unknown surface value -> DIRT
    assert car.speed(RoadClass.PATH, Surface.ASPHALT, 0) == 0.0
    assert car.speed(99, Surface.ASPHALT, 0) == 0.0
    assert foot.speed(RoadClass.PATH, Surface.DIRT, SacScale.DEMANDING_ALPINE_HIKING) == 3.5
    assert foot.speed(RoadClass.TRACK, Surface.DIRT, SacScale.ALPINE_HIKING) == 3.5
    assert foot.speed(RoadClass.STEPS, Surface.ROCK, 0) == 4.0
    assert foot.speed(RoadClass.PATH, Surface.MUD, 0) == pytest.approx(4.0)
    assert TRAVEL_PROFILES[M].speed(RoadClass.PATH, Surface.ASPHALT, SacScale.MOUNTAIN_HIKING) == 0.0


def test_usable_mask():
    assert usable_mask(RoadClass.UNKNOWN, 0) == 0
    assert usable_mask(RoadClass.PRIMARY, 0) == int(ALL_TRAVEL)
    assert usable_mask(RoadClass.PATH, 0) == int(F | B | M | H)
    assert usable_mask(RoadClass.PATH, SacScale.HIKING) == int(F | B | M | H)
    assert usable_mask(RoadClass.PATH, SacScale.MOUNTAIN_HIKING) == int(F | B | H)
    assert usable_mask(RoadClass.PATH, SacScale.DEMANDING_MOUNTAIN_HIKING) == int(F | H)
    assert usable_mask(RoadClass.PATH, SacScale.DIFFICULT_ALPINE_HIKING) == int(F)
    assert usable_mask(RoadClass.STEPS, 0) == int(F | B)
    # sac_scale does not gate non-trail classes: an inferred T4 jeep track stays drivable
    assert usable_mask(RoadClass.TRACK, SacScale.ALPINE_HIKING) == int(ALL_TRAVEL)


def test_get_profile_and_climb_factor():
    assert get_profile(F).travel == F and get_profile("jeep").travel == J and get_profile(int(BUS)).travel == BUS
    for bad in (F | C, 0, 128, "nope"):
        with pytest.raises(ValueError):
            get_profile(bad)
    assert climb_factor(F, 100, 1000.0) == pytest.approx(1.6)
    assert climb_factor(B, 50, 1000.0) == pytest.approx(1.4)
    assert climb_factor(C, 100, 1000.0) == pytest.approx(1.1)
    assert climb_factor(F, -100, 1000.0) == 1.0
    assert climb_factor(F, 10, 0.0) == 1.0


def test_travel_profiles_table_is_json():
    t = travel_profiles_table()
    s = json.dumps(t, sort_keys=True)
    assert json.loads(s) == t
    assert list(t["profiles"]) == [p.name for p in PROFILES]
    assert t["profiles"]["FOOT"]["alpine_speed_kmh"] == 3.5 and t["profiles"]["CAR"]["alpine_speed_kmh"] is None
    assert t["profiles"]["MOTORBIKE"]["speed_kmh"]["PATH"] == 12.0
    assert "PATH" in t["trail_classes"] and "TRACK" not in t["trail_classes"]


# ---------------------------------------------------------------------------
# Graph building
# ---------------------------------------------------------------------------
def plus_network() -> list[RoadFeature]:
    """Two ways crossing at OSM node 2; nodes 10 and 11 are plain shape points."""
    return [
        road(100, RoadClass.RESIDENTIAL, [(0, 0), (50, 0), (100, 0), (150, 0), (200, 0)], [1, 10, 2, 11, 3],
             surface=Surface.ASPHALT),
        road(101, RoadClass.RESIDENTIAL, [(100, -100), (100, 0), (100, 100)], [4, 2, 5], surface=Surface.ASPHALT),
    ]


def test_shared_node_splitting():
    g = build_graph(plus_network(), to_game=planar)
    assert g.node_osm_id.tolist() == [1, 2, 3, 4, 5]
    assert g.node_count == 5 and g.edge_count == 8
    hub = node_of(g, 2)
    assert {int(g.edge_target[e]) for e in g.edges_from(hub)} == {node_of(g, o) for o in (1, 3, 4, 5)}
    (e,) = edges_between(g, node_of(g, 1), hub)
    assert g.edge_geom_count[e] == 3 and g.edge_length_dm[e] == 1000
    np.testing.assert_allclose(g.edge_geometry(e), [(0, 0), (50, 0), (100, 0)])
    (e,) = edges_between(g, hub, node_of(g, 1))
    np.testing.assert_allclose(g.edge_geometry(e), [(100, 0), (50, 0), (0, 0)])
    # CSR invariants: edge_source matches offsets; targets sorted within each node
    for v in range(g.node_count):
        tg = [int(g.edge_target[e]) for e in g.edges_from(v)]
        assert tg == sorted(tg)
        assert all(int(g.edge_source[e]) == v for e in g.edges_from(v))
    assert g.build_info["roads_used"] == 2


def test_node_numbering_sorted_by_osm_id():
    roads = [road(7, RoadClass.SERVICE, [(0, 0), (10, 0)], [900, 5]),
             road(3, RoadClass.SERVICE, [(10, 0), (10, 10), (20, 20)], [5, 77, 31])]
    g = build_graph(roads, to_game=planar)
    assert g.node_osm_id.tolist() == [5, 31, 900]
    assert g.node_x_dm.tolist() == [100, 200, 0] and g.node_z_dm.tolist() == [0, 200, 0]


def test_closed_loop_way_is_split_into_a_cycle():
    ring = road(1, RoadClass.FOOTWAY, [(0, 0), (100, 0), (100, 100), (0, 100), (0, 0)], [1, 2, 3, 4, 1],
                access=F)
    stem = road(2, RoadClass.FOOTWAY, [(0, 0), (-100, 0)], [1, 9], access=F)
    g = build_graph([ring, stem], to_game=planar)
    assert g.node_osm_id.tolist() == [1, 3, 9]  # 3 is the ring's middle vertex
    assert g.build_info["loops_split"] == 1
    assert not np.any(g.edge_source == g.edge_target)
    r = route(g, node_of(g, 9), node_of(g, 3), F)
    assert r is not None and r.length_m == pytest.approx(300.0)
    assert len(r.edges) == 2


def test_node_visited_twice_by_one_way():
    # lollipop: 9 -> 1 -> 2 -> 3 -> 1; node 1 occurs twice and must become a graph node
    lolly = road(5, RoadClass.PATH, [(-50, 0), (0, 0), (50, 0), (25, 40), (0, 0)], [9, 1, 2, 3, 1])
    g = build_graph([lolly], to_game=planar)
    assert g.node_osm_id.tolist() == [1, 2, 9]
    assert g.edge_count == 6 and not np.any(g.edge_source == g.edge_target)
    r = route(g, node_of(g, 9), node_of(g, 2), F)
    assert r.length_m == pytest.approx(100.0)


def test_consecutive_duplicate_nodes_and_degenerate_ways():
    roads = [road(1, RoadClass.SERVICE, [(0, 0), (10, 0), (10, 0), (20, 0)], [1, 2, 2, 3]),
             road(2, RoadClass.SERVICE, [(5, 5), (5, 5)], [7, 7]),
             road(3, RoadClass.SERVICE, [(5, 5)], [8])]
    g = build_graph(roads, to_game=planar)
    assert g.node_osm_id.tolist() == [1, 3]
    assert g.edge_geom_count.tolist() == [3, 3]
    assert g.build_info["skipped_degenerate"] == 2


def test_synthetic_ids_never_connect():
    # Two clipped pieces ending at a synthetic vertex (id 0) at the same place: not connected.
    roads = [road(1, RoadClass.TERTIARY, [(0, 0), (100, 0)], [11, 0]),
             road(2, RoadClass.TERTIARY, [(100, 0), (200, 0)], [0, 12]),
             road(3, RoadClass.TERTIARY, [(0, 50), (50, 50), (50, 50), (100, 50)], [21, -1, -1, 22])]
    g = build_graph(roads, to_game=planar)
    assert g.node_count == 6
    assert (g.node_osm_id == 0).sum() == 2 and g.node_osm_id.tolist()[:4] == [11, 12, 21, 22]
    assert route(g, node_of(g, 11), node_of(g, 12), C) is None
    assert g.stats()["profiles"]["CAR"]["components"] == 3
    (e,) = edges_between(g, node_of(g, 21), node_of(g, 22))
    assert g.edge_geom_count[e] == 4  # distinct synthetic vertices are not deduplicated


def test_input_validation():
    with pytest.raises(ValueError):
        build_graph([road(1, RoadClass.SERVICE, [(0, 0), (1, 1)], [1, 2, 3])], to_game=planar)
    with pytest.raises(ValueError):
        build_graph([road(1, RoadClass.SERVICE, [(0, 0), (np.nan, 1)], [1, 2])], to_game=planar)
    with pytest.raises(ValueError):
        build_graph([road(1, RoadClass.SERVICE, [(0, 0), (1, 1)], [1, 2])], to_game=lambda ll: (ll[:1, 0], ll[:1, 1]))


def test_oneway_forward_and_reverse():
    roads = [road(1, RoadClass.RESIDENTIAL, [(0, 0), (100, 0)], [1, 2], oneway=1),
             road(2, RoadClass.RESIDENTIAL, [(0, 100), (100, 100)], [3, 4], oneway=-1)]
    g = build_graph(roads, to_game=planar)
    n1, n2, n3, n4 = (node_of(g, o) for o in (1, 2, 3, 4))
    assert route(g, n1, n2, C) is not None and route(g, n2, n1, C) is None
    assert route(g, n2, n1, F).length_m == pytest.approx(100.0)
    (rev,) = edges_between(g, n2, n1)
    assert g.edge_access[rev] == int(F)
    (fwd,) = edges_between(g, n1, n2)
    assert g.edge_access[fwd] == int(ALL_TRAVEL)
    # oneway=-1: traffic flows 4 -> 3, against the digitised order
    assert route(g, n4, n3, C) is not None and route(g, n3, n4, C) is None
    (e,) = edges_between(g, n4, n3)
    np.testing.assert_allclose(g.edge_geometry(e), [(100, 100), (0, 100)])
    assert edge_time_s(g, edges_between(g, n3, n4)[0], C) == math.inf
    assert edge_time_s(g, edges_between(g, n3, n4)[0], F) < math.inf


def test_oneway_without_foot_has_no_reverse_edge():
    g = build_graph([road(1, RoadClass.MOTORWAY, [(0, 0), (1000, 0)], [1, 2], oneway=1, access=MOTOR_TRAVEL)],
                    to_game=planar)
    assert g.edge_count == 1 and g.build_info["oneway_reverse_omitted"] == 1
    assert route(g, 1, 0, F) is None and route(g, 0, 1, BUS) is not None


def test_access_class_and_sac_limits():
    # PATH tagged (wrongly) with every mode: the stored mask is the effective one, no BUS/CAR/JEEP.
    roads = [road(1, RoadClass.PATH, [(0, 0), (100, 0)], [1, 2], access=ALL_TRAVEL),
             road(2, RoadClass.PATH, [(0, 100), (100, 100)], [3, 4], access=F | B | H | M,
                  sac_scale=SacScale.DEMANDING_MOUNTAIN_HIKING),
             road(3, RoadClass.PATH, [(0, 200), (100, 200)], [5, 6], access=F | B | H | M, sac_scale=SacScale.HIKING),
             road(4, RoadClass.TRACK, [(0, 300), (100, 300)], [7, 8], access=F | J | H | M,
                  sac_scale=SacScale.ALPINE_HIKING, surface=Surface.GRAVEL)]
    g = build_graph(roads, to_game=planar)
    e1 = edges_between(g, node_of(g, 1), node_of(g, 2))[0]
    assert g.edge_access[e1] == int(F | B | M | H)
    for t in (C, J, BUS):
        assert edge_time_s(g, e1, t) == math.inf
        assert route(g, node_of(g, 1), node_of(g, 2), t) is None
    # T3 path: MOTORBIKE and BICYCLE out, HORSE and FOOT in
    e2 = edges_between(g, node_of(g, 3), node_of(g, 4))[0]
    assert g.edge_access[e2] == int(F | H)
    assert route(g, node_of(g, 3), node_of(g, 4), M) is None
    assert route(g, node_of(g, 3), node_of(g, 4), B) is None
    assert route(g, node_of(g, 3), node_of(g, 4), H) is not None and route(g, node_of(g, 3), node_of(g, 4), F)
    # T1 path with motorcycle access: MOTORBIKE ok at 12 km/h * DIRT 0.6
    e3 = edges_between(g, node_of(g, 5), node_of(g, 6))[0]
    assert edge_time_s(g, e3, M) == pytest.approx(100.0 * 3.6 / (12.0 * 0.6))
    # T4 track: JEEP still drives, FOOT capped at 3.5 km/h
    e4 = edges_between(g, node_of(g, 7), node_of(g, 8))[0]
    assert edge_time_s(g, e4, J) == pytest.approx(100.0 * 3.6 / (30.0 * 0.9))
    assert edge_time_s(g, e4, F) == pytest.approx(100.0 * 3.6 / 3.5)
    assert edge_time_s(g, e4, C) == math.inf  # access, not physics


def test_bus_routes_around_a_path_shortcut():
    roads = [road(1, RoadClass.PRIMARY, [(0, 0), (500, 500), (1000, 0)], [1, 50, 2], surface=Surface.ASPHALT),
             road(2, RoadClass.PATH, [(0, 0), (1000, 0)], [1, 2], access=F | B | H)]
    g = build_graph(roads, to_game=planar)
    a, b = node_of(g, 1), node_of(g, 2)
    rb, rf = route(g, a, b, BUS), route(g, a, b, F)
    assert rb.length_m == pytest.approx(2 * math.hypot(500, 500), abs=0.1)
    assert g.edge_class[rb.edges[0]] == RoadClass.PRIMARY
    assert rf.length_m == pytest.approx(1000.0) and g.edge_class[rf.edges[0]] == RoadClass.PATH


def test_skipped_roads():
    roads = [road(1, RoadClass.UNKNOWN, [(0, 0), (100, 0)], [1, 2]),
             road(2, RoadClass.RESIDENTIAL, [(0, 0), (0, 100)], [1, 3], access=Travel(0)),
             road(3, RoadClass.STEPS, [(0, 0), (0, -100)], [1, 4], access=C | BUS),
             road(4, RoadClass.RESIDENTIAL, [(0, 0), (-100, 0)], [1, 5])]
    g = build_graph(roads, to_game=planar)
    assert g.node_osm_id.tolist() == [1, 5]
    bi = g.build_info
    assert bi["skipped_unknown_class"] == 1 and bi["skipped_no_access"] == 2 and bi["roads_used"] == 1


def test_surface_mud_shortcut_car_avoids_jeep_takes():
    h = math.sqrt(1500.0 ** 2 - 750.0 ** 2)
    roads = [road(1, RoadClass.TERTIARY, [(0, 0), (750, h), (1500, 0)], [1, 50, 2], surface=Surface.ASPHALT),
             road(2, RoadClass.TERTIARY, [(0, 0), (1500, 0)], [1, 2], surface=Surface.MUD)]
    g = build_graph(roads, to_game=planar)
    a, b = node_of(g, 1), node_of(g, 2)
    rc, rj = route(g, a, b, C), route(g, a, b, J)
    assert g.edge_surface[rc.edges[0]] == Surface.ASPHALT and rc.length_m == pytest.approx(3000.0, abs=0.2)
    assert rc.time_s == pytest.approx(3000.0 * 3.6 / 50.0, rel=1e-4)
    assert g.edge_surface[rj.edges[0]] == Surface.MUD and rj.length_m == pytest.approx(1500.0)
    assert rj.time_s == pytest.approx(1500.0 * 3.6 / 30.0)
    # the mud edge costs a car more than the detour, the jeep less
    mud = [e for e in edges_between(g, a, b) if g.edge_surface[e] == Surface.MUD][0]
    assert edge_time_s(g, mud, C) > rc.time_s and edge_time_s(g, mud, J) < rc.time_s


def test_climb_factor_foot_bicycle_car():
    g = build_graph([road(1, RoadClass.RESIDENTIAL, [(0, 0), (1000, 0)], [1, 2], surface=Surface.ASPHALT)],
                    to_game=planar, elev=lambda x, z: 1300.0 + 0.1 * x)
    lo, hi = node_of(g, 1), node_of(g, 2)
    assert g.node_elev.tolist() == [1300, 1400]
    (up,) = edges_between(g, lo, hi)
    (down,) = edges_between(g, hi, lo)
    assert g.edge_climb[up] == 100 and g.edge_climb[down] == -100
    assert edge_time_s(g, up, F) == pytest.approx(720.0 * 1.6)
    assert edge_time_s(g, down, F) == pytest.approx(720.0)
    assert edge_time_s(g, up, B) == pytest.approx(240.0 * 1.8)
    assert edge_time_s(g, up, C) == pytest.approx(120.0 * 1.1)
    assert route(g, lo, hi, F).time_s > route(g, hi, lo, F).time_s


def test_unknown_elevation_sentinel():
    g = build_graph([road(1, RoadClass.RESIDENTIAL, [(0, 0), (1000, 0), (2000, 0)], [1, 2, 3]),
                     road(2, RoadClass.RESIDENTIAL, [(1000, 0), (1000, 500)], [2, 4])],
                    to_game=planar, elev=lambda x, z: np.where(x > 1500, np.nan, 50000.0 + 0.0 * x))
    assert g.node_elev[node_of(g, 3)] == ELEV_UNKNOWN
    assert g.node_elev[node_of(g, 1)] == 32767  # clamped to i16
    for e in edges_between(g, node_of(g, 2), node_of(g, 3)) + edges_between(g, node_of(g, 3), node_of(g, 2)):
        assert g.edge_climb[e] == 0
    with pytest.raises(ValueError):
        build_graph([road(1, RoadClass.SERVICE, [(0, 0), (1, 0)], [1, 2])], to_game=planar,
                    elev=lambda x, z: np.zeros(5))


def test_names_and_flags():
    nm = NameRec("Ring Road", "Ring Road", "चक्रपथ", alt=("Chakrapath",))
    roads = [road(1, RoadClass.TRUNK, [(0, 0), (100, 0)], [1, 2], name=nm, bridge=True),
             road(2, RoadClass.TRUNK, [(100, 0), (200, 0)], [2, 3], name=NameRec("Ring Road", "Ring Road", "चक्रपथ"),
                  tunnel=True, ford=True),
             road(3, RoadClass.PRIMARY, [(100, 0), (100, 100)], [2, 4], name=NameRec("Arniko Highway"), is_link=True),
             road(4, RoadClass.PRIMARY, [(0, 0), (0, 100)], [1, 5], name=NameRec())]
    g = build_graph(roads, to_game=planar)
    assert g.names == [NameRec("Arniko Highway"), NameRec("Ring Road", "Ring Road", "चक्रपथ")]
    e = edges_between(g, node_of(g, 1), node_of(g, 2))[0]
    assert g.edge_name_rec(e) == g.names[1] and g.edge_flags[e] == EDGE_BRIDGE
    e = edges_between(g, node_of(g, 3), node_of(g, 2))[0]
    assert g.edge_name[e] == 2 and g.edge_flags[e] == EDGE_TUNNEL | EDGE_FORD
    e = edges_between(g, node_of(g, 2), node_of(g, 4))[0]
    assert g.edge_name[e] == 1 and g.edge_flags[e] == EDGE_LINK
    e = edges_between(g, node_of(g, 1), node_of(g, 5))[0]
    assert g.edge_name[e] == 0 and g.edge_name_rec(e) is None
    assert decode_graph(encode_graph(g)).names == g.names


def test_long_way_split_at_geom_count_limit():
    n = 70_000
    pts = np.column_stack((np.arange(n, dtype=np.float64), np.zeros(n)))
    g = build_graph([road(1, RoadClass.TRACK, pts, np.arange(1, n + 1))], to_game=planar)
    assert g.build_info["long_split"] == 1
    assert g.node_osm_id.tolist() == [1, MAX_GEOM_POINTS, n]
    assert sorted(g.edge_geom_count.tolist()) == [n - MAX_GEOM_POINTS + 1] * 2 + [MAX_GEOM_POINTS] * 2
    r = route(g, 0, 2, F)
    assert r.length_m == pytest.approx(n - 1)
    np.testing.assert_allclose(route_geometry(g, r), pts)


def test_empty_graph():
    g = build_graph([], to_game=planar)
    assert g.node_count == 0 and g.edge_count == 0 and g.offsets.tolist() == [0]
    g2 = decode_graph(encode_graph(g))
    assert g2 == g and len(encode_graph(g)) == HEADER_SIZE + 4
    assert nearest_node(g, 0, 0, F) is None
    assert eta_by_profile(g, (0, 0), (1, 1)) == {p.name: None for p in PROFILES}
    assert g.stats()["total_km"] == 0.0


# ---------------------------------------------------------------------------
# Random networks near Kathmandu (default projection)
# ---------------------------------------------------------------------------
_RAND_CLASSES = (RoadClass.MOTORWAY, RoadClass.TRUNK, RoadClass.PRIMARY, RoadClass.SECONDARY, RoadClass.TERTIARY,
                 RoadClass.UNCLASSIFIED, RoadClass.RESIDENTIAL, RoadClass.SERVICE, RoadClass.TRACK, RoadClass.ROAD,
                 RoadClass.PATH, RoadClass.FOOTWAY, RoadClass.STEPS, RoadClass.CYCLEWAY, RoadClass.BRIDLEWAY,
                 RoadClass.LIVING_STREET, RoadClass.PEDESTRIAN)
_RAND_MASKS = (ALL_TRAVEL, ALL_TRAVEL, F | B | H, MOTOR_TRAVEL, F, F | B | M | H | J, F | M, B | C | BUS)


def random_roads(seed: int) -> list[RoadFeature]:
    rng = np.random.default_rng(seed)
    n = int(rng.integers(12, 40))
    pts = rng.uniform(0.0, 1.0, (n, 2)) * (0.03, 0.027) + (85.30, 27.68)
    jid = rng.permutation(n) * 7 + 100
    next_id = 100_000
    roads: list[RoadFeature] = []
    for w in range(int(rng.integers(n, 2 * n))):
        seq = [int(rng.integers(n))]
        while len(seq) < int(rng.integers(2, 5)) or len(seq) < 2:
            c = int(rng.integers(n))
            if c != seq[-1]:
                seq.append(c)
        ll, ids = [pts[seq[0]]], [int(jid[seq[0]])]
        for u, v in zip(seq[:-1], seq[1:]):
            for _ in range(int(rng.integers(0, 3))):
                t = rng.uniform(0.2, 0.8)
                ll.append(pts[u] + (pts[v] - pts[u]) * t + rng.normal(0.0, 0.0004, 2))
                ids.append(next_id)
                next_id += 1
            ll.append(pts[v])
            ids.append(int(jid[v]))
        roads.append(RoadFeature(
            osm_id=50_000 + w, cls=_RAND_CLASSES[int(rng.integers(len(_RAND_CLASSES)))],
            lonlat=np.array(ll), node_ids=np.array(ids, dtype=np.int64),
            surface=Surface(int(rng.integers(len(Surface)))),
            oneway=int(rng.choice([0, 0, 0, 1, -1])),
            access=_RAND_MASKS[int(rng.integers(len(_RAND_MASKS)))],
            sac_scale=SacScale(int(rng.choice([0, 0, 0, 1, 2, 3, 4, 5, 6]))),
            bridge=bool(rng.random() < 0.1), tunnel=bool(rng.random() < 0.05), ford=bool(rng.random() < 0.05),
            is_link=bool(rng.random() < 0.1),
            name=NameRec(f"Marga {int(rng.integers(5))}", "", "") if rng.random() < 0.5 else None,
        ))
    return roads


def _elev(x: np.ndarray, z: np.ndarray) -> np.ndarray:
    return 1300.0 + 120.0 * np.sin(x / 300.0) + 90.0 * np.cos(z / 450.0)


def _random_graph(seed: int) -> RoutingGraph:
    return build_graph(random_roads(seed), elev=_elev)


def _reference_times(g: RoutingGraph, t: Travel) -> np.ndarray:
    """All-pairs fastest times with scipy (parallel edges reduced to the fastest)."""
    times = edge_times(g, t)
    use = np.isfinite(times)
    s, d, w = g.edge_source[use], g.edge_target[use].astype(np.int64), times[use]
    o = np.lexsort((w, d, s))
    s, d, w = s[o], d[o], w[o]
    first = np.ones(s.shape[0], dtype=bool)
    first[1:] = (s[1:] != s[:-1]) | (d[1:] != d[:-1])
    assert np.all(w > 0)  # scipy would drop explicit zeros
    m = csr_matrix((w[first], (s[first], d[first])), shape=(g.node_count, g.node_count))
    return dijkstra(m, directed=True)


@pytest.mark.parametrize("seed", range(50))
def test_astar_equals_dijkstra_on_random_graphs(seed):
    g = _random_graph(seed)
    rng = np.random.default_rng(1000 + seed)
    xy = np.column_stack((g.node_x_dm, g.node_z_dm)).astype(np.float64)
    chord_m = np.hypot(xy[:, None, 0] - xy[None, :, 0], xy[:, None, 1] - xy[None, :, 1]) / 10.0
    for t in PROFILES:
        ref = _reference_times(g, t)
        # admissibility: h(v -> d) never exceeds the true fastest time
        h = chord_m * 3.6 / TRAVEL_PROFILES[t].max_speed_kmh
        fin = np.isfinite(ref)
        assert np.all(h[fin] <= ref[fin] * (1 + 1e-12) + 1e-9)
        for _ in range(6):
            s, d = (int(v) for v in rng.integers(0, g.node_count, 2))
            ra, rd = route(g, s, d, t), route(g, s, d, t, heuristic=False)
            if not np.isfinite(ref[s, d]):
                assert ra is None and rd is None
                continue
            assert ra.time_s == pytest.approx(ref[s, d], rel=1e-9, abs=1e-9)
            assert rd.time_s == pytest.approx(ref[s, d], rel=1e-9, abs=1e-9)
            # the reported route is a real path with the reported totals
            assert ra.nodes[0] == s and ra.nodes[-1] == d and len(ra.edges) == len(ra.nodes) - 1
            for i, e in enumerate(ra.edges):
                assert int(g.edge_source[e]) == ra.nodes[i] and int(g.edge_target[e]) == ra.nodes[i + 1]
            assert sum(edge_time_s(g, e, t) for e in ra.edges) == pytest.approx(ra.time_s, rel=1e-12)
            assert ra.length_m == pytest.approx(sum(int(g.edge_length_dm[e]) for e in ra.edges) / 10.0)


def test_edge_length_never_shorter_than_chord():
    for seed in range(10):
        g = _random_graph(seed)
        src, tgt = g.edge_source, g.edge_target.astype(np.int64)
        chord = np.hypot((g.node_x_dm[tgt] - g.node_x_dm[src]).astype(np.float64),
                         (g.node_z_dm[tgt] - g.node_z_dm[src]).astype(np.float64))
        assert np.all(g.edge_length_dm >= chord)


def test_edge_times_vector_matches_scalar():
    g = _random_graph(3)
    for t in PROFILES:
        vec = edge_times(g, t)
        scal = np.array([edge_time_s(g, e, t) for e in range(g.edge_count)])
        assert np.array_equal(vec, scal)
        assert edge_times(g, t) is vec  # cached


def test_edges_and_mask_rules_on_random_graph():
    roads = random_roads(11)
    g = build_graph(roads, elev=_elev)
    assert np.all(g.edge_access != 0)
    assert np.all(g.edge_access & ~np.uint8(int(ALL_TRAVEL)) == 0)
    for v in range(g.node_count):
        tg = [int(g.edge_target[e]) for e in g.edges_from(v)]
        assert tg == sorted(tg)
    # every edge's geometry starts and ends exactly on its nodes
    for e in range(g.edge_count):
        geo = g.edge_geometry_dm(e)
        s, d = int(g.edge_source[e]), int(g.edge_target[e])
        assert tuple(geo[0]) == (g.node_x_dm[s], g.node_z_dm[s])
        assert tuple(geo[-1]) == (g.node_x_dm[d], g.node_z_dm[d])
        assert g.edge_geom_count[e] == geo.shape[0] >= 2


# ---------------------------------------------------------------------------
# Binary format
# ---------------------------------------------------------------------------
def test_binary_round_trip_and_determinism(tmp_path):
    roads = random_roads(7)
    g = build_graph(roads, elev=_elev)
    p1, p2 = tmp_path / "a.ghrg", tmp_path / "b.ghrg"
    s1 = write_graph(p1, g)
    write_graph(p2, build_graph(roads, elev=_elev))
    data = p1.read_bytes()
    assert data == p2.read_bytes()
    rng = np.random.default_rng(0)
    shuffled = [roads[i] for i in rng.permutation(len(roads))]
    assert encode_graph(build_graph(shuffled, elev=_elev)) == data
    assert s1["bytes"] == len(data) and s1["nodes"] == g.node_count and s1["edges"] == g.edge_count
    assert len(s1["sha256"]) == 64 and not (tmp_path / "a.ghrg.tmp").exists()

    g2 = read_graph(p1)
    assert g2 == g
    assert encode_graph(g2) == data
    for e in range(0, g.edge_count, 7):
        np.testing.assert_array_equal(g2.edge_geometry(e), g.edge_geometry(e))
    for t in PROFILES:
        assert np.array_equal(edge_times(g2, t), edge_times(g, t))
    assert g2.node_osm_id is None and g2.stats() == g.stats()

    # header and section sizes exactly as DATA_FORMATS.md section 4
    magic, ver, flags, n, e, gb, nc, reserved = struct.unpack_from("<4sHHIIIIQ", data, 0)
    assert (magic, ver, flags, reserved) == (MAGIC, VERSION, 0, 0)
    assert (n, e, gb, nc) == (g.node_count, g.edge_count, len(g.geometry), len(g.names))
    names_off = HEADER_SIZE + n * NODE_SIZE + (n + 1) * 4 + e * EDGE_SIZE + gb
    r = Reader(data, names_off)
    for nm in g.names:
        assert (r.str(), r.str(), r.str()) == (nm.default, nm.en, nm.ne)
    assert r.remaining() == 0
    # spot-check raw node and edge records
    v = n // 2
    x, z, el, res = struct.unpack_from("<iihH", data, HEADER_SIZE + v * NODE_SIZE)
    assert (x, z, el, res) == (g.node_x_dm[v], g.node_z_dm[v], g.node_elev[v], 0)
    k = e // 3
    rec = struct.unpack_from("<IIBBBBBBhIHH", data, HEADER_SIZE + n * NODE_SIZE + (n + 1) * 4 + k * EDGE_SIZE)
    assert rec == (g.edge_target[k], g.edge_length_dm[k], g.edge_class[k], g.edge_surface[k], g.edge_access[k],
                   g.edge_flags[k], g.edge_sac[k], 0, g.edge_climb[k], g.edge_geom_offset[k], g.edge_name[k],
                   g.edge_geom_count[k])
    # geometry: first point relative to the source node is (0, 0), then deltas
    geo = Reader(data, HEADER_SIZE + n * NODE_SIZE + (n + 1) * 4 + e * EDGE_SIZE + int(g.edge_geom_offset[k]))
    assert (geo.svarint(), geo.svarint()) == (0, 0)


def test_decoder_rejects_bad_input():
    data = encode_graph(_random_graph(1))
    with pytest.raises(ValueError):
        decode_graph(b"XXRG" + data[4:])
    with pytest.raises(ValueError):
        decode_graph(data[:4] + struct.pack("<H", 2) + data[6:])
    with pytest.raises(ValueError):
        decode_graph(data[:-1])
    with pytest.raises(ValueError):
        decode_graph(data + b"\0")
    with pytest.raises(ValueError):
        decode_graph(data[:20])


def test_svarint_encoder_matches_binio():
    from ghumante_pipeline.binio import Writer

    rng = np.random.default_rng(5)
    vals = np.concatenate((np.array([0, 1, -1, 63, -64, 64, -65, 8191, -8192, 2 ** 31 - 1, -(2 ** 31),
                                     2 ** 62, -(2 ** 62)]),
                           rng.integers(-(1 << 40), 1 << 40, 500)))
    blob, nb = encode_svarints(vals)
    w = Writer()
    for v in vals.tolist():
        w.svarint(v)
    assert blob.tobytes() == w.bytes() and int(nb.sum()) == len(w)
    assert encode_svarints(np.zeros(0, dtype=np.int64))[0].size == 0


def test_geometry_round_trip_within_quantisation():
    rng = np.random.default_rng(9)
    t = np.linspace(0.0, 1.0, 40)
    ll = np.column_stack((KTM[0] + 0.01 * t, KTM[1] + 0.002 * np.sin(t * 9.0))) + rng.normal(0, 2e-6, (40, 2))
    ids = np.arange(1, 41)
    cross = road(2, RoadClass.RESIDENTIAL, [ll[13] + (0, 0.001), ll[13], ll[13] - (0, 0.001)], [900, 14, 901])
    g = build_graph([road(1, RoadClass.SECONDARY, ll, ids), cross])
    x, z = lonlat_to_game(ll[:, 0], ll[:, 1])
    expect = np.column_stack((x, z))
    # nodes are rint(game * 10) of the projected OSM node
    a = node_of(g, 1)
    assert (g.node_x_dm[a], g.node_z_dm[a]) == (int(np.rint(x[0] * 10)), int(np.rint(z[0] * 10)))
    for start, end in ((1, 40), (40, 1)):
        r = route(g, node_of(g, start), node_of(g, end), F)
        assert len(r.edges) == 2
        geo = route_geometry(g, r)
        ref = expect if start == 1 else expect[::-1]
        assert geo.shape == ref.shape
        assert np.abs(geo - ref).max() <= 0.05 + 1e-9  # half a decimetre per axis
        assert np.hypot(*(geo - ref).T).max() < 0.1
    true_len = float(np.hypot(np.diff(x), np.diff(z)).sum())
    assert r.length_m == pytest.approx(true_len, abs=0.25)


# ---------------------------------------------------------------------------
# Queries
# ---------------------------------------------------------------------------
def query_network() -> RoutingGraph:
    """Road 1-2 (oneway for vehicles), footpath 2-3 north, a bus-free road 2-5 east."""
    return build_graph([
        road(1, RoadClass.RESIDENTIAL, [(0, 0), (1000, 0)], [1, 2], oneway=1, access=ALL_TRAVEL & ~BUS),
        road(2, RoadClass.PATH, [(1000, 0), (1000, 800)], [2, 3], access=F | B | H),
        road(3, RoadClass.SERVICE, [(1000, 0), (1600, 0)], [2, 5], access=ALL_TRAVEL & ~BUS),
    ], to_game=planar)


def test_nearest_node_per_profile():
    g = query_network()
    n1, n2, n3, n5 = (node_of(g, o) for o in (1, 2, 3, 5))
    assert nearest_node(g, 1000, 790, F) == n3
    assert nearest_node(g, 1000, 790, H) == n3
    assert nearest_node(g, 1000, 790, C) == n2
    assert nearest_node(g, 1000, 790, C, max_dist_m=500.0) is None
    assert nearest_node(g, 1000, 790, C, max_dist_m=800.0) == n2
    assert nearest_node(g, 1000, 790, BUS) is None
    assert nearest_node(g, -50, 0, C) == n1
    # node 3 is a dead end of the path: FOOT can leave it, CAR has no edge there at all
    assert nearest_node(g, 1590, 0, C) == n5
    # oneway: node 1 has no incoming CAR edge, so a destination snaps to node 2
    assert nearest_node(g, -50, 0, C, incoming=True) == n2
    assert nearest_node(g, -50, 0, F, incoming=True) == n1


def test_eta_by_profile():
    g = query_network()
    eta = eta_by_profile(g, (-10.0, 0.0), (1000.0, 790.0))
    assert list(eta) == [p.name for p in PROFILES]
    assert eta["FOOT"] == pytest.approx((1800.0, 1800.0 * 3.6 / 5.0))
    assert eta["HORSE"][0] == pytest.approx(1800.0)
    # car snaps the destination to node 2 (the path is not drivable)
    assert eta["CAR"] == pytest.approx((1000.0, 1000.0 * 3.6 / (30.0 * 0.6)))
    assert eta["BUS"] is None
    back = eta_by_profile(g, (1000.0, 790.0), (-10.0, 0.0))
    assert back["FOOT"][0] == pytest.approx(1800.0)
    assert back["CAR"] is None or back["CAR"][0] == 0.0  # node 2 -> node 2: oneway forbids reaching node 1
    assert eta_by_profile(g, (0.0, 0.0), (0.0, 0.0))["FOOT"] == (0.0, 0.0)


def test_route_edge_cases():
    g = query_network()
    assert route(g, 0, 0, C) == route(g, 0, 0, F)
    assert route(g, 0, 0, C).nodes == [0] and route(g, 0, 0, C).time_s == 0.0
    assert route(g, node_of(g, 2), node_of(g, 1), C) is None
    with pytest.raises(IndexError):
        route(g, 0, g.node_count, F)
    with pytest.raises(ValueError):
        route(g, 0, 1, F | C)


def test_stats_components_and_km():
    roads = [road(1, RoadClass.RESIDENTIAL, [(0, 0), (1000, 0)], [1, 2]),
             road(2, RoadClass.RESIDENTIAL, [(5000, 0), (5000, 500)], [3, 4], oneway=1),
             road(3, RoadClass.PATH, [(1000, 0), (5000, 0)], [2, 3], access=F)]
    g = build_graph(roads, to_game=planar)
    st = g.stats()
    assert st["nodes"] == 4 and st["edges"] == 6
    assert st["total_km"] == pytest.approx(5.5) and st["directed_km"] == pytest.approx(11.0)
    assert st["profiles"]["CAR"]["components"] == 2 and st["profiles"]["CAR"]["km"] == pytest.approx(1.5)
    assert st["profiles"]["CAR"]["largest_component_share"] == pytest.approx(0.5)
    assert st["profiles"]["FOOT"]["components"] == 1 and st["profiles"]["FOOT"]["km"] == pytest.approx(5.5)
    assert st["profiles"]["CAR"]["edges"] == 3 and st["profiles"]["FOOT"]["edges"] == 6
    json.dumps(st)


def test_graph_equality_ignores_build_only_fields():
    g = build_graph(plus_network(), to_game=planar)
    g2 = decode_graph(encode_graph(g))
    assert g == g2 and g2.node_osm_id is None and g2.build_info == {}
    g3 = decode_graph(encode_graph(g))
    g3.edge_access = g3.edge_access.copy()
    g3.edge_access[0] ^= 1
    assert g3 != g
    with pytest.raises(TypeError):
        hash(g)
