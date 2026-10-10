"""W2 detail pass (docs/W2_DETAIL_CONTRACT.md decisions 1, 3-5): corridors and building trimming
(``corridors.py``), road structures and deck heights (``structures.py``), the way-profile helpers
(``wayprofile.py``) and the routing car rule, on small hand-made scenes in game metres."""

from __future__ import annotations

import math

import numpy as np
import pytest
import shapely

from ghumante_pipeline import corridors as cor
from ghumante_pipeline import routing, structures as sts, wayprofile as wp
from ghumante_pipeline.model import (AreaFeature, AreaKind, AreaType, BuildingArchetype, BuildingFlags, LineFeature,
                                     LineKind, NameRec, RoadClass, RoadFeature, RoadStructureFlags as SF,
                                     RoadStructureKind as SK, Travel)
from ghumante_pipeline.tile_format import DECK_DECK, DECK_DRAPED, DECK_RAMP

URBAN = int(AreaType.URBAN)
OLD = int(AreaType.OLD_CORE)


def road(oid, cls=RoadClass.RESIDENTIAL, pts=((0, 0), (200, 0)), ids=None, **kw) -> tuple[RoadFeature, np.ndarray,
                                                                                          np.ndarray]:
    p = np.asarray(pts, dtype=np.float64)
    nid = np.asarray(ids if ids is not None else [oid * 100 + k for k in range(len(p))], dtype=np.int64)
    access = kw.pop("access", Travel(0x7F) if int(cls) not in sts.FOOT_CLASSES else Travel.FOOT | Travel.BICYCLE)
    r = RoadFeature(osm_id=oid, cls=cls, lonlat=p.copy(), node_ids=nid, access=access, **kw)
    return r, p, nid


def rect(x0, z0, x1, z1) -> np.ndarray:
    return np.array([[x0, z0], [x1, z0], [x1, z1], [x0, z1]], dtype=np.float64)


class Bld:
    def __init__(self, archetype=BuildingArchetype.MODERN_URBAN, flags=0):
        self.archetype = archetype
        self.flags = BuildingFlags(flags)


def flat(h=1300.0):
    return lambda x, z: np.full(len(np.atleast_1d(x)), h)


# ---------------------------------------------------------------------------------------------------------------
# wayprofile
# ---------------------------------------------------------------------------------------------------------------
def test_piece_arcs_open_and_closed():
    from ghumante_pipeline import geom

    way = np.array([[0.0, 0.0], [10.0, 0.0], [10.0, 10.0], [0.0, 10.0]])
    cum = wp.cumulative(way)
    for p in geom.clip_polyline(way, (5.0, -1.0, 20.0, 20.0)):
        a = wp.piece_arcs(way, cum, p.points)
        assert a is not None and np.all(np.diff(a) > 0)
        pos, _ = wp.at_arcs(way, cum, a)
        assert np.allclose(pos, p.points)
    ring = np.array([[0.0, 0.0], [10.0, 0.0], [10.0, 10.0], [0.0, 10.0], [0.0, 0.0]])
    cum = wp.cumulative(ring)
    pieces = geom.clip_polyline(ring, (-1.0, -1.0, 5.0, 11.0))
    assert pieces
    for p in pieces:
        a = wp.piece_arcs(ring, cum, p.points)
        assert a is not None and np.all(np.diff(a) > 0)  # wraps past the closing vertex
        pos, _ = wp.at_arcs(ring, cum, a)
        assert np.allclose(pos, p.points)


def test_profile_window_min_bounds_interpolation():
    prof = wp.Profile(np.arange(0.0, 101.0, 10.0), np.array([5, 9, 4.8, 9, 9, 9, 7, 9, 9, 9, 9], float), 100.0)
    a = np.arange(0.0, 101.0, 20.0)
    m = prof.window_min(a, 20.0)
    # Linear interpolation between two window minima never exceeds the profile.
    x = np.linspace(0.0, 100.0, 401)
    interp = np.interp(x, a, m)
    assert np.all(interp <= prof.at(x) + 1e-9)


# ---------------------------------------------------------------------------------------------------------------
# corridors
# ---------------------------------------------------------------------------------------------------------------
def test_real_width_tag_plausibility():
    r, _, _ = road(1, width_m=1.0)  # under half the residential floor (2.5 m): ignored
    assert cor.real_width(r, URBAN) == 5.0
    r, _, _ = road(1, width_m=2.6)
    assert cor.tagged_width(r) == 2.6 and cor.real_width(r, URBAN) == 2.6
    r, _, _ = road(2, cls=RoadClass.FOOTWAY)
    assert cor.real_width(r, OLD) == 1.75


def _compute(roads, outlines, protected=None, area=URBAN):
    rs = [r for r, _, _ in roads]
    pts = [p for _, p, _ in roads]
    prot = np.zeros(len(outlines), dtype=bool) if protected is None else np.asarray(protected, dtype=bool)
    return cor.compute(rs, pts, lambda x, z: np.full(len(np.atleast_1d(x)), area), outlines, prot), pts


def test_corridor_floor_trims_a_galli():
    # A 60 m old-core galli with houses 1.5 m either side of the centreline.
    g = road(1, pts=((0, 0), (60, 0)))
    blds = [rect(5, 1.5, 55, 10), rect(5, -10, 55, -1.5), rect(10, 20, 20, 30)]
    C, pts = _compute([g], blds, area=OLD)
    w = C.ways[0]
    assert np.all(w.c >= cor.MIN_CORRIDOR_M - 1e-9) and w.c.min() == pytest.approx(cor.MIN_CORRIDOR_M)
    assert C.galli(0)  # untagged residential 3 m wall to wall
    bands, owners = cor.corridor_bands(C, pts, set())
    rings = [[b] for b in blds]
    T = cor.trim_buildings(rings, np.zeros(3, dtype=bool), bands, owners)
    assert T.trimmed == {0, 1} and not T.removed
    for k in (0, 1):
        poly = shapely.Polygon(T.rings[k][0])
        assert poly.is_valid and poly.exterior.is_ccw
        assert shapely.distance(shapely.LineString(pts[0]), poly) >= 0.5 * cor.MIN_CORRIDOR_M - 1e-3
    assert 2 not in T.trimmed  # far away
    # Window minima: the stored samples never exceed the corridor.
    dm, sh = w.samples(0.0, 60.0)
    assert len(dm) == 4 and np.all(dm >= 48) and len(sh) == 0


def test_corridor_open_country_and_tagged_width():
    tr = road(1, cls=RoadClass.TRUNK, pts=((0, 0), (300, 0)))
    C, _ = _compute([tr], [], area=int(AreaType.RURAL))
    # Trunk, rural, untagged two-way: real 9 m, nominal min(max(11.25, 6.5), 15) = 11.25, + 2 x 1.5 m shoulders.
    assert C.ways[0].c == pytest.approx(np.full(len(C.ways[0].c), 11.25 + 3.0))
    ft = road(2, cls=RoadClass.FOOTWAY, pts=((0, 50), (100, 50)))
    tagged = road(3, pts=((0, 100), (100, 100)), width_m=8.0)
    C, _ = _compute([ft, tagged], [rect(20, 101, 40, 110)])
    assert np.allclose(C.ways[0].c, cor.MIN_CORRIDOR_M)  # a footway still gets 4.8 m
    assert C.ways[1].c.min() >= 8.0 - 1e-9  # the surveyed width wins over the 1 m building gap
    assert not C.galli(1)


def test_dead_end_does_not_cut_the_house_it_stops_at():
    r = road(1, pts=((0, 0), (20, 0)))
    house = rect(20.05, -5, 30, 5)
    C, pts = _compute([r], [house])
    bands, owners = cor.corridor_bands(C, pts, set())
    T = cor.trim_buildings([[house]], np.zeros(1, dtype=bool), bands, owners)
    assert not T.trimmed


def test_protected_footprints_shift_or_squeeze():
    fw = road(1, cls=RoadClass.FOOTWAY, pts=((0, 0), (80, 0)))
    temple = rect(30, 1.0, 40, 9)  # 1.0 m left of the footway
    house = rect(25, -9, 45, -1.5)  # 1.5 m right
    C, pts = _compute([fw], [temple, house], protected=[True, False])
    w = C.ways[0]
    k = np.argmin(np.abs(w.arcs - 35.0))
    assert w.shift[k] <= -(0.5 * cor.MIN_CORRIDOR_M + cor.SHIFT_MARGIN_M - 1.0) + 1e-6  # moved right, off the temple
    assert np.all(np.abs(np.diff(w.shift)) <= np.diff(w.arcs) / cor.TAPER + 1e-9)  # tapered
    bands, owners = cor.corridor_bands(C, pts, set())
    T = cor.trim_buildings([[temple], [house]], np.array([True, False]), bands, owners)
    assert 0 not in T.trimmed and 1 in T.trimmed  # the temple is never trimmed; the house gives way
    dm, sh = w.samples(0.0, 80.0)
    assert len(sh) == len(dm) and sh.min() < 0
    # Temples on both sides: the corridor narrows to the gap between them.
    C, _ = _compute([fw], [temple, rect(25, -9, 45, -1.0)], protected=[True, True])
    w = C.ways[0]
    assert w.squeezed and w.c.min() == pytest.approx(2.0 - 2 * cor.SHIFT_MARGIN_M)


def test_trim_removes_slivers_and_keeps_the_largest_part():
    r = road(1, pts=((0, 0), (100, 0)))
    small = rect(10, -1.0, 11.5, 1.0)  # inside the corridor
    cut = rect(40, -10, 50, 10)  # the road runs through it: two halves, the larger kept
    cut2 = rect(60, -3, 70, 10)
    C, pts = _compute([r], [small, cut, cut2])
    bands, owners = cor.corridor_bands(C, pts, set())
    T = cor.trim_buildings([[small], [cut], [cut2]], np.zeros(3, dtype=bool), bands, owners)
    assert 0 in T.removed
    assert 1 in T.trimmed and len(T.parts[1]) == 1  # cut in two: both halves stay, the larger first
    a0 = shapely.Polygon(T.rings[1][0]).area
    a1 = shapely.Polygon(T.parts[1][0][0]).area
    assert a0 >= a1 >= cor.SLIVER_M2
    assert 2 in T.trimmed and shapely.Polygon(T.rings[2][0]).bounds[1] >= 2.4 - 1e-3


# ---------------------------------------------------------------------------------------------------------------
# structures
# ---------------------------------------------------------------------------------------------------------------
def _analyse(roads, lines=(), areas=(), terrain=None):
    rs = [r for r, _, _ in roads]
    pts = [p for _, p, _ in roads]
    ids = [i for _, _, i in roads]
    lines = list(lines)
    lg = [np.asarray(ln.lonlat, dtype=np.float64) for ln in lines]
    areas = list(areas)
    ag = [a.polygon for a in areas]
    return sts.analyse(rs, pts, ids, lines, lg, areas, ag, terrain or flat(),
                       real_width=lambda i: cor.real_width(rs[i], URBAN))


def _river(oid=900, x=100.0, width=None):
    return LineFeature(osm_id=oid, kind=LineKind.RIVER, lonlat=np.array([[x, -200.0], [x, 200.0]]), width_m=width,
                       name=NameRec("Bagmati", "Bagmati", "बागमती"), tags={"waterway": "river"})


def _piece(S, i, pts, car=True):
    p = np.asarray(S.polyline(i) if S.polyline(i) is not None else pts, dtype=np.float64)
    return S.piece_record(i, p, p, wp.cumulative(p), False, False, car), p


def test_untagged_river_crossing_gets_an_inferred_bridge():
    r = road(1, pts=((0, 0), (200, 0)))
    S = _analyse([r], lines=[_river()])
    assert int(S.kinds[0]) == int(SK.BRIDGE)
    rec, p = _piece(S, 0, r[1])
    assert rec.kind == int(SK.BRIDGE) and rec.flags & int(SF.WATER_CROSSING)
    assert not rec.flags & int(SF.DECK_FROM_TAGS) and rec.railing_dm == 11
    role, h = rec.deck_role, rec.deck_cm / 100.0
    deck = role == DECK_DECK
    assert deck.any() and (role == DECK_RAMP).any() and role[0] == DECK_DRAPED and role[-1] == DECK_DRAPED
    # Flat terrain at 1300: surface 1300 - 1.5, clearance 3 m over a 15 m river (+2 m each side).
    assert h[deck].min() >= 1300.0 - 1.5 + 3.0 - 1e-6
    xs = p[:, 0]
    assert xs[deck].min() <= 100 - 7.5 - 2 + 1e-6 and xs[deck].max() >= 100 + 7.5 + 2 - 1e-6
    # Ramps at most 8 % (residential).
    hh = np.where(role != DECK_DRAPED, h, 1300.0)
    grade = np.abs(np.diff(hh)) / np.maximum(np.hypot(*np.diff(p, axis=0).T), 1e-9)
    assert grade.max() <= sts.GRADE.get(int(RoadClass.RESIDENTIAL), sts.DEFAULT_GRADE) + 1e-6


def test_tagged_bridge_snaps_a_nearby_river_and_keeps_its_abutments_on_the_banks():
    app_a = road(1, pts=((0, 0), (90, 0)), ids=[10, 11])
    br = road(2, pts=((90, 0), (130, 0)), ids=[11, 12], bridge=True)
    app_b = road(3, pts=((130, 0), (220, 0)), ids=[12, 13])
    river = _river(x=140.0)  # 10 m past the bridge end: a mapping offset
    S = _analyse([app_a, br, app_b], lines=[river])
    assert int(S.kinds[1]) == int(SK.BRIDGE) and int(S.kinds[2]) == int(SK.NONE)
    rec, _ = _piece(S, 1, br[1])
    assert rec.flags & int(SF.WATER_CROSSING) and rec.flags & int(SF.DECK_FROM_TAGS)
    assert np.all(rec.deck_role == DECK_DECK)
    assert rec.deck_cm.min() / 100.0 >= 1300.0 - 1.5 + 3.0 - 1e-6
    # The deck is raised 1.5 m above the banks, so both approaches ramp up to it.
    for k, pts in ((0, app_a[1]), (2, app_b[1])):
        a, _ = _piece(S, k, pts)
        assert a.kind == int(SK.NONE) and a.flags & int(SF.APPROACH) and (a.deck_role == DECK_RAMP).any()


def test_flyover_foot_overbridge_and_underpass_clearance():
    primary = road(1, cls=RoadClass.PRIMARY, pts=((0, 0), (300, 0)), width_m=10.0)
    foot = road(2, cls=RoadClass.FOOTWAY, pts=((150, -15), (150, 15)), bridge=True, layer=1)
    stairs = road(3, cls=RoadClass.STEPS, pts=((150, 15), (150, 30)), ids=[201, 301])
    S = _analyse([primary, foot, stairs])
    assert int(S.kinds[1]) == int(SK.FLYOVER) and int(S.kinds[0]) == int(SK.UNDERPASS)
    rec, _ = _piece(S, 1, foot[1])
    assert rec.flags & int(SF.FOOT_OVERBRIDGE) and rec.flags & int(SF.OVER_ROAD) and rec.railing_dm == 13
    need = 1300.0 + sts.MIN_UNDERPASS_CLEARANCE_M + sts.FOOT_DECK_DEPTH_M
    assert rec.deck_cm.max() / 100.0 >= need - 1e-6
    under, _ = _piece(S, 0, primary[1])
    assert under.kind == int(SK.UNDERPASS) and under.clearance_cm >= 550
    assert len(under.deck_role) == 0  # the road below stays on the terrain
    st, _ = _piece(S, 2, stairs[1])
    assert st.flags & int(SF.APPROACH) and (st.deck_role == DECK_RAMP).any()  # stairs down at 50 %


def test_tunnel_under_a_ground_road_is_lowered():
    ground = road(1, cls=RoadClass.PRIMARY, pts=((0, 0), (400, 0)))
    tunnel = road(2, cls=RoadClass.TRUNK, pts=((200, -40), (200, 40)), ids=[21, 22], tunnel=True, layer=-1)
    # The south approach is tunnel-tagged too (Kalanki): it belongs to the underpass, not a tunnel.
    ramp_s = road(3, cls=RoadClass.TRUNK, pts=((200, -300), (200, -40)), ids=[31, 21], tunnel=True, layer=-1)
    ramp_n = road(4, cls=RoadClass.TRUNK, pts=((200, 40), (200, 300)), ids=[22, 41])
    S = _analyse([ground, tunnel, ramp_s, ramp_n])
    assert int(S.kinds[1]) == int(SK.UNDERPASS)
    rec, p = _piece(S, 1, tunnel[1])
    assert rec.flags & int(SF.LOWERED) and rec.clearance_cm >= 550
    h = rec.deck_cm / 100.0
    k = np.argmin(np.abs(p[:, 1]))
    assert h[k] <= 1300.0 - sts.MIN_UNDERPASS_CLEARANCE_M - sts.DECK_DEPTH_M + 1e-6
    assert int(S.kinds[2]) == int(SK.UNDERPASS)  # chained tunnel way: part of the underpass
    a, _ = _piece(S, 2, ramp_s[1])
    assert a.kind == int(SK.UNDERPASS) and a.flags & int(SF.LOWERED)
    assert sts.car_accessible(ramp_s[0], 7.0, False, int(S.kinds[2]))
    a, _ = _piece(S, 3, ramp_n[1])
    assert a.flags & int(SF.APPROACH) and a.flags & int(SF.LOWERED)
    g, _ = _piece(S, 0, ground[1])
    assert len(g.deck_role) == 0  # the upper road never sinks


def test_ford_tunnel_and_car_access():
    ford = road(1, cls=RoadClass.TRACK, pts=((0, 0), (200, 0)), ford=True)
    long_tunnel = road(2, pts=((0, 50), (300, 50)), tunnel=True, extra={"tunnel": "yes"})
    passage = road(3, cls=RoadClass.FOOTWAY, pts=((0, 80), (20, 80)), tunnel=True,
                   extra={"tunnel": "building_passage"})
    S = _analyse([ford, long_tunnel, passage], lines=[_river()])
    assert int(S.kinds[0]) == int(SK.FORD) and int(S.kinds[1]) == int(SK.TUNNEL) and int(S.kinds[2]) == int(SK.NONE)
    assert int(S.layers[1]) == -1 and int(S.layers[2]) == 0
    r_ok, _, _ = road(5)
    assert sts.car_accessible(r_ok, 5.0, False, int(SK.NONE))
    assert not sts.car_accessible(r_ok, 2.9, False, int(SK.NONE))  # under 3 m real
    assert not sts.car_accessible(r_ok, 5.0, True, int(SK.NONE))  # a galli
    assert not sts.car_accessible(r_ok, 5.0, False, int(SK.TUNNEL))
    fw, _, _ = road(6, cls=RoadClass.FOOTWAY)
    assert not sts.car_accessible(fw, 2.0, False, int(SK.NONE))
    nocar, _, _ = road(7, access=Travel.FOOT | Travel.BICYCLE | Travel.MOTORBIKE)  # motorcar=no
    assert not sts.car_accessible(nocar, 5.0, False, int(SK.NONE))


def test_pond_causeway_and_water_area_span():
    pond = AreaFeature(osm_type="w", osm_id=77, kind=AreaKind.WATER_POND,
                       polygon=shapely.Polygon(rect(50, -30, 90, 30)), name=NameRec("Rani Pokhari", "", ""))
    r = road(1, cls=RoadClass.FOOTWAY, pts=((0, 0), (140, 0)))
    S = _analyse([r], areas=[pond])
    assert int(S.kinds[0]) == int(SK.BRIDGE)
    rec, p = _piece(S, 0, r[1])
    deck = rec.deck_role == DECK_DECK
    assert p[deck, 0].min() <= 48.0 + 1e-6 and p[deck, 0].max() >= 92.0 - 1e-6
    assert (rec.deck_cm[deck] / 100.0).min() >= 1300.0 - 0.5 + 1.0 - 1e-6
    assert any(e["water"] == ["Rani Pokhari"] for e in S.report)


def test_leaf_terrain_matches_the_tile_grid():
    from ghumante_pipeline import projection
    from ghumante_pipeline.tile_format import dequantize_heights, quantize_heights

    def sample(x, z, spacing_m=None):
        return 1300.0 + 0.01 * np.asarray(x) - 0.02 * np.asarray(z)

    lt = sts.LeafTerrain(sample, projection.tile_size(10), 129)
    t = projection.TileId(10, 516, 161)
    xs, zs = projection.grid_coords(t, 129)
    q = dequantize_heights(quantize_heights(sample(xs, zs))).astype(np.float64)
    assert np.allclose(lt(xs[5:8, 5:8].ravel(), zs[5:8, 5:8].ravel()), q[5:8, 5:8].ravel())
    mid = lt([0.5 * (xs[3, 3] + xs[3, 4])], [zs[3, 3]])[0]
    assert mid == pytest.approx(0.5 * (q[3, 3] + q[3, 4]))


# ---------------------------------------------------------------------------------------------------------------
# routing
# ---------------------------------------------------------------------------------------------------------------
def test_routing_drops_car_modes_from_no_car_ways():
    def rf(oid, ids, cls=RoadClass.RESIDENTIAL):
        pts = np.array([[85.3 + 0.001 * k, 27.7] for k in range(len(ids))])
        return RoadFeature(osm_id=oid, cls=cls, lonlat=pts, node_ids=np.asarray(ids, dtype=np.int64))

    g = routing.build_graph([rf(1, [1, 2, 3]), rf(2, [3, 4])], no_car=[2])
    assert g.build_info["no_car_ways"] == 1
    masks = {int(m) for m in g.edge_access}
    assert any(m & int(Travel.CAR) for m in masks)
    gone = [int(m) for m in g.edge_access if not m & int(Travel.CAR)]
    assert gone and all(m & int(Travel.MOTORBIKE) and m & int(Travel.BICYCLE) and not m & int(Travel.BUS)
                        and not m & int(Travel.JEEP) for m in gone)
