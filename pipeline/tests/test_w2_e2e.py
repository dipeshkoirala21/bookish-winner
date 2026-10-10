"""Wave 2 end to end on the synthetic fixture: the F2 chunks, region side files, sacred routing (D14),
hide zones (D5), parts (D4) and determinism (W2_DESIGN 9.5)."""

from __future__ import annotations

import json

import numpy as np
import pytest

from fixtures.synth.make_synth import synth_build
from ghumante_pipeline import curated, routing, transit
from ghumante_pipeline.model import (BuildingFlags, HeritageFlags, JunctionFlags, JunctionKind, LiveryClass,
                                     ObjectKind, PropFlags, RoadAttrFlags, Sidewalk, StopFlags, StyleProfile,
                                     Travel, TreeClass, TurnRestriction)
from ghumante_pipeline.pack import PackReader, sha256_file
from ghumante_pipeline.tile_format import AreaFlags, decode_tile


@pytest.fixture(scope="session")
def synth(tmp_path_factory):
    return synth_build(tmp_path_factory.getbasetemp() / "synth_e2e")


@pytest.fixture(scope="session")
def synth_again(tmp_path_factory):
    return synth_build(tmp_path_factory.getbasetemp() / "synth_e2e_again", workers=1, qa=False, use_cache=False)


@pytest.fixture(scope="session")
def leaves(synth):
    out = []
    with PackReader(synth.pack) as pr:
        for k in pr.keys():
            td = decode_tile(pr.get(k))
            if td.has_detail:
                out.append(td)
    return out


def test_parallel_chunks_match_their_base(leaves):
    assert leaves
    for td in leaves:
        assert len(td.road_attrs) == len(td.roads)
        assert len(td.building_fronts) == len(td.buildings)


def test_road_attributes(synth, leaves):
    syn = synth.synth
    prim = [(r, a) for td in leaves for r, a in zip(td.roads, td.road_attrs) if r.osm_way_id == syn.primary_way]
    assert prim
    for r, a in prim:
        assert a.sidewalk == Sidewalk.BOTH and a.maxspeed_kmh == 40
        assert a.flags & RoadAttrFlags.LIT and a.flags & RoadAttrFlags.BUS_ROUTE and a.flags & RoadAttrFlags.PAINTABLE
    # The street block under the religious compound is walk-and-cycle only.
    blocked = [a for td in leaves for r, a in zip(td.roads, td.road_attrs)
               if r.osm_way_id == syn.compound_street and a.flags & RoadAttrFlags.HERITAGE_PEDESTRIAN]
    assert not blocked  # its length midpoint is far from the compound (the way is 2 km long)
    # Corridor samples exist on urban motor roads and are never negative.
    cors = [a.corridor_dm for td in leaves for a in td.road_attrs if len(a.corridor_dm)]
    assert cors and all(int(c.min()) >= 0 for c in cors)


def test_fronts_profiles_and_parts(synth, leaves):
    syn = synth.synth
    fronts = [(b, f) for td in leaves for b, f in zip(td.buildings, td.building_fronts)]
    house = [f for b, f in fronts if b.osm_ref == syn.front_house << 1]
    assert len(house) == 1
    h = house[0]
    assert h.front_edge == 0 and 30 <= h.front_dist_dm <= 150  # the south wall faces the (wiggling) primary
    assert h.shop_bays == (2 | 0x80)
    for b, f in fronts:
        if f.front_edge != 255:
            assert f.front_edge < len(b.rings[0]) and f.front_dist_dm <= 250
    profiles = {StyleProfile(f.style_profile) for _, f in fronts}
    assert StyleProfile.KATHMANDU_CORE in profiles  # the synth town overlaps the Kathmandu core rectangle
    part = [b for b, _ in fronts if b.osm_ref == syn.temple_part << 1]
    temple = [b for b, _ in fronts if b.osm_ref == syn.temple_way << 1]
    assert part and part[0].flags & BuildingFlags.PART
    assert temple and temple[0].flags & BuildingFlags.HAS_PARTS
    # Hidden by the hero hide zone (D5): the temple and its part.
    assert temple[0].flags & BuildingFlags.LANDMARK and part[0].flags & BuildingFlags.LANDMARK
    assert part[0].archetype == temple[0].archetype


def test_junctions_and_props(synth, leaves):
    syn = synth.synth
    js = [j for td in leaves for j in td.junctions]
    sig = [j for j in js if j.osm_node_id == syn.signal_node]
    assert len(sig) == 1 and sig[0].kind == JunctionKind.SIGNALS
    assert sig[0].flags & JunctionFlags.HAS_SIGNALS and sig[0].flags & JunctionFlags.HAS_POLICE
    assert sig[0].flags & JunctionFlags.OFFICERS_2_4 and sig[0].flags & JunctionFlags.CROSSINGS_MARKED
    names = {td.name(j.name_ref).en for td in leaves for j in td.junctions if j.name_ref}
    assert "Synth Chowk" in names
    props = [(td, p) for td in leaves for p in td.props]
    kinds = {p.kind for _, p in props}
    assert {ObjectKind.TREE, ObjectKind.BUS_STOP, ObjectKind.TRAFFIC_SIGNALS, ObjectKind.CROSSING_MARKED,
            ObjectKind.STORAGE_TANK} <= kinds
    tree = next(p for _, p in props if p.kind == ObjectKind.TREE)
    assert tree.subtype == TreeClass.PIPAL and tree.flags & PropFlags.CHAUTARI and tree.height_dm == 180
    on_road = {p.kind for _, p in props if p.flags & PropFlags.ON_ROAD}
    assert {ObjectKind.TRAFFIC_SIGNALS, ObjectKind.CROSSING_MARKED} <= on_road
    assert ObjectKind.BUS_STOP not in on_road


def test_area_flags(synth, leaves):
    syn = synth.synth
    comp = [a for td in leaves for a in td.areas if a.osm_ref == syn.compound_way << 1]
    assert comp and all(a.flags & AreaFlags.SACRED_NO_VEHICLE and a.flags & AreaFlags.HERITAGE_ZONE for a in comp)
    court = [a for td in leaves for a in td.areas if a.osm_ref == (syn.building_relation << 1) | 1]
    assert court and all(a.flags & AreaFlags.SACRED_NO_VEHICLE for a in court)


def test_routing_keeps_cars_out_of_the_compound(synth):
    g = routing.read_graph(synth.graph)
    # The street block under the compound lost its motor modes in both directions (D14).
    assert synth.manifest["stats"]["graph_sacred_pieces"] >= 1
    motor = int(Travel.CAR | Travel.BUS | Travel.JEEP | Travel.MOTORBIKE)
    blocked = int(np.sum((g.edge_access & motor) == 0))
    assert blocked > 0


def test_transit_file(synth):
    syn = synth.synth
    rs, _names = transit.decode_ghrt((synth.reg_dir / "synth_test.transit.ghrt").read_bytes())
    bus = [r for r in rs.routes if r.osm_id == syn.bus_route]
    assert len(bus) == 1
    r = bus[0]
    assert r.livery == LiveryClass.CITY_GREEN and r.ref == "S1" and r.ways == [(syn.primary_way, True)]
    assert len(r.stops) == 1 and r.stops[0].flags & StopFlags.FROM_MEMBER and r.stops[0].flags & StopFlags.TERMINAL
    assert [(x.osm_id, x.kind) for x in rs.restrictions] == [(syn.restriction, TurnRestriction.NO_LEFT_TURN)]


def test_curated_files(synth):
    syn = synth.synth
    recs = curated.decode_ghcd((synth.reg_dir / "synth_test.curated.ghcd").read_bytes())
    ids = [h.id for h in recs]
    assert ids == ["her.synth.temple", "her.synth.window"]  # far one outside, stage 2 skipped
    t = recs[0]
    assert t.footprint_ref == syn.temple_way << 1 and t.compound_ref == syn.compound_way << 1 and t.tiers == 2
    assert t.flags & HeritageFlags.HAS_COMPOUND and t.flags & HeritageFlags.SANCTUM_CLOSED
    assert set(t.hidden) == {syn.temple_way << 1, syn.temple_part << 1} and t.yaw_cdeg == 9000
    assert t.name_ne  # from the OSM name (Devanagari never typed into the config)
    assert recs[1].flags & HeritageFlags.MANUAL_POSITION and recs[1].flags & HeritageFlags.VERIFY
    hero = json.loads((synth.reg_dir / "hero_recipes.json").read_text(encoding="utf-8"))
    assert [r["id"] for r in hero["records"]] == ids


def test_manifest_lists_w2_files_and_stats(synth):
    m = synth.manifest
    files = {f["path"]: f for f in m["files"]}
    for name in ("synth_test.transit.ghrt", "synth_test.curated.ghcd", "hero_recipes.json"):
        assert files[name]["sha256"] == sha256_file(synth.reg_dir / name)
    w2 = m["stats"]["w2"]
    assert w2["transit"]["routes"] == 1 and w2["heritage"]["resolved"] == 2
    assert w2["parts"]["parts_with_host"] == 1 and w2["junctions"]["police"] == 1
    ch = w2["chunks"]
    assert set(ch["chunk_bytes"]) == {"BFNT", "JNCT", "PROP", "RATR", "RSTR"} and all(v > 0 for v in ch["chunk_bytes"].values())
    assert ch["corridor"]["narrower_than_tagged"] == 0 and ch["fronts"]["with_front"] >= 1
    assert ch["props"]["TREE"] >= 1 and ch["junction_kinds"]["SIGNALS"] == 1
    assert "Wave 2 data" in (synth.reg_dir / "BUILD_REPORT.md").read_text(encoding="utf-8")


def test_w2_outputs_are_deterministic(synth, synth_again):
    for name in ("synth_test.ghpk", "synth_test.route.ghrg", "synth_test.transit.ghrt", "synth_test.curated.ghcd",
                 "hero_recipes.json"):
        assert sha256_file(synth.reg_dir / name) == sha256_file(synth_again.reg_dir / name), name


# ---------------------------------------------------------------------------------------------------------------
# W2 detail pass: corridors, trimming, structures, car access (docs/W2_DETAIL_CONTRACT.md decisions 1, 3-5)
# ---------------------------------------------------------------------------------------------------------------
def test_every_road_has_a_structure_record_and_a_corridor(leaves):
    from ghumante_pipeline.model import RoadStructureFlags as SF
    from ghumante_pipeline.tile_format import shift_count_ok

    assert all(td.meta and td.meta.get("ratr_corridor") == "final" for td in leaves)  # the final-corridor marker

    n = 0
    for td in leaves:
        assert len(td.road_structures) == len(td.roads)
        for r, a, s in zip(td.roads, td.road_attrs, td.road_structures):
            assert len(a.corridor_dm) >= 1, r.osm_way_id
            if not s.flags & int(SF.SQUEEZED):
                assert int(a.corridor_dm.min()) >= 48  # RoadClearance.MinCorridorM
            assert len(s.deck_role) in (0, len(r.points))
            assert shift_count_ok(len(s.shift_cm), len(a.corridor_dm))
            n += 1
    assert n > 40


def test_inferred_bridges_over_the_river(synth, leaves):
    from ghumante_pipeline.model import RoadStructureFlags as SF, RoadStructureKind as SK

    br = [(r, s) for td in leaves for r, s in zip(td.roads, td.road_structures)
          if s.kind == int(SK.BRIDGE) and s.flags & int(SF.WATER_CROSSING)]
    assert len({r.osm_way_id for r, _ in br}) >= 6  # every north-south street crosses Synth Khola
    for r, s in br:
        assert not s.flags & int(SF.DECK_FROM_TAGS) and s.railing_dm == 11
        assert (s.deck_role == 1).any()


def test_foot_overbridge_and_underpass(synth, leaves):
    from ghumante_pipeline.model import RoadStructureFlags as SF, RoadStructureKind as SK

    fb = [s for td in leaves for r, s in zip(td.roads, td.road_structures) if r.osm_way_id == synth.synth.footbridge]
    assert fb and all(s.kind == int(SK.FLYOVER) and s.flags & int(SF.FOOT_OVERBRIDGE) for s in fb)
    under = [s for td in leaves for r, s in zip(td.roads, td.road_structures)
             if r.osm_way_id == synth.synth.primary_way and s.kind == int(SK.UNDERPASS)]
    assert under and min(s.clearance_cm for s in under) >= 550


def test_galli_and_house_in_the_road_are_trimmed(synth, leaves):
    from ghumante_pipeline.model import BuildingFrontFlags, RoadStructureFlags as SF

    syn = synth.synth
    trimmed = {b.osm_ref >> 1 for td in leaves for b, f in zip(td.buildings, td.building_fronts)
               if f.flags & int(BuildingFrontFlags.TRIMMED_FOR_ROAD)}
    assert set(syn.galli_houses) <= trimmed and syn.road_house in trimmed
    assert syn.temple_way not in trimmed  # hero footprints are never trimmed
    g = [s for td in leaves for r, s in zip(td.roads, td.road_structures) if r.osm_way_id == syn.galli]
    assert g and not any(s.flags & int(SF.CAR_ACCESSIBLE) for s in g)
    prim = [s for td in leaves for r, s in zip(td.roads, td.road_structures) if r.osm_way_id == syn.primary_way]
    assert prim and all(s.flags & int(SF.CAR_ACCESSIBLE) for s in prim)
    stats = synth.manifest["stats"]["w2"]["detail"]
    assert stats["trim"]["trimmed"] >= 3 and sum(stats["trims_by_tile"].values()) >= 3


def test_routing_keeps_cars_out_of_the_galli(synth):
    g = routing.read_graph(synth.graph)
    assert synth.manifest["stats"].get("graph_no_car_ways", 0) >= 1
    # The galli's edges keep two-wheelers and walkers but no car, jeep or bus.
    names = {k: nm for k, nm in enumerate(g.names, start=1)}
    gid = [k for k, nm in names.items() if nm.default == "Synth Galli"]
    assert gid
    for e in np.flatnonzero(g.edge_name == gid[0]).tolist():
        m = int(g.edge_access[e])
        assert m & int(Travel.MOTORBIKE) and m & int(Travel.BICYCLE) and m & int(Travel.FOOT)
        assert not m & int(Travel.CAR | Travel.JEEP | Travel.BUS)
