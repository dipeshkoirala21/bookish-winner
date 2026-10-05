"""Wave 2 build steps (docs/W2_DESIGN.md section 9), called by ``build.build_region``.

``prepare_buildings`` runs before building inference: POI and part hints
(``poi_hints``), curated hero records (``curated.resolve``: hide zones and
archetype overrides) and courtyard fixes (``sacred.prepare_courtyards``).
``prepare_inputs`` runs after it and computes everything the tiler and the
router need: the area-type grid, style profiles, front hints, sacred zones and
AREA flags, dual carriageways, bus routes, junctions (``tiling.W2Inputs``).
``write_files`` writes the region files: ``<region>.transit.ghrt``,
``<region>.curated.ghcd``, ``hero_recipes.json`` and, for regions that list an
airport (``regions.yaml`` ``aviation``), ``<region>.aviation.json``.
"""

from __future__ import annotations

import logging
from dataclasses import dataclass, field
from pathlib import Path
from typing import Sequence

import numpy as np
import shapely

from . import aviation, curated, junctions, poi_hints, roadattrs, sacred, style, transit
from .areatype import AreaTypeGrid, build_grid, ring_area_centroid
from .buildings import BuildingContext
from .config import CONFIG_DIR, Region
from .contexts import project_lonlat_arrays
from .model import AreaKind, BuildingFlags, Extract, StyleProfile
from .tiling import W2Inputs, _to_game_geoms

log = logging.getLogger(__name__)
TREE_COVER = 10  # WorldCover class
SMALL_TOWN_PROFILES = frozenset((int(StyleProfile.BHAKTAPUR), int(StyleProfile.KIRTIPUR), int(StyleProfile.PANAUTI)))
SMALL_TOWN_MAX_LEVELS = 6


@dataclass
class W2Config:
    style_zones: Path = CONFIG_DIR / "style_zones.yaml"
    chowks: Path = CONFIG_DIR / "curated" / "chowks.yaml"
    heritage: Path = CONFIG_DIR / "curated" / "heritage_sites.yaml"
    transit: Path = CONFIG_DIR / "curated" / "transit.yaml"
    corridors: bool = True


@dataclass
class W2State:
    cfg: W2Config
    zones: style.StyleZones
    heritage: list[dict]
    shops: np.ndarray | None = None
    part_hosts: dict[int, int] = field(default_factory=dict)
    resolved: list = field(default_factory=list)
    overrides: dict = field(default_factory=dict)
    stats: dict = field(default_factory=dict)


def load(cfg: W2Config | None = None) -> W2State:
    cfg = cfg or W2Config()
    return W2State(cfg=cfg, zones=style.load_style_zones(cfg.style_zones),
                   heritage=curated.load_heritage(cfg.heritage))


def anchor_nodes(state: W2State) -> set[int]:
    return curated.anchor_node_ids(state.heritage)


def prepare_buildings(state: W2State, extract: Extract, region: Region) -> None:
    """POI/part hints, curated heroes and courtyards, in place, before ``infer_buildings``."""
    state.shops, s1 = poi_hints.apply_poi_footprints(extract.buildings, extract.pois)
    state.part_hosts, s2 = poi_hints.link_parts(extract.buildings)
    s3 = sacred.prepare_courtyards(extract.areas, extract.buildings,
                                   [z.geom_lonlat for z in state.zones.zones if z.old_core])
    # Heroes are placed from the leaf tiles, so only records inside the region box (not its buffer) count.
    state.resolved, state.overrides, s4 = curated.resolve(state.heritage, extract, extract.anchor_nodes,
                                                          bbox_lonlat=tuple(region.bbox))
    state.stats.update({"poi_hints": s1, "parts": s2, "courtyards": s3, "heritage": s4})


def infer_kwargs(state: W2State) -> dict:
    return {"overrides": state.overrides, "part_hosts": state.part_hosts}


def prepare_inputs(state: W2State, extract: Extract, region: Region, leaf_box: Sequence[float] | None, dem,
                   landcover, routes: transit.RouteSet) -> W2Inputs:
    ex = extract
    st = state.stats
    # Buildings: centroids and areas in game metres (parts excluded from coverage).
    outers = project_lonlat_arrays([b.outer for b in ex.buildings])
    n = len(ex.buildings)
    areas = np.zeros(n)
    cxz = np.zeros((n, 2))
    for i, r in enumerate(outers):
        a, cx, cz = ring_area_centroid(r) if len(r) else (0.0, 0.0, 0.0)
        areas[i], cxz[i] = a, (cx, cz)
    is_part = np.array([bool(b.flags & BuildingFlags.PART) for b in ex.buildings], dtype=bool)

    # Areas in game space, flags, sacred zones.
    comp, kora = sacred.compound_sets(state.heritage)
    area_geoms = _to_game_geoms([a.polygon for a in ex.areas]) if ex.areas else np.zeros(0, dtype=object)
    area_flags = np.array([sacred.area_flags(a, comp, kora) for a in ex.areas], dtype=np.int64)
    zones = sacred.SacredZones.build(ex.areas, area_geoms, comp, kora)
    heritage_polys = [g for a, g in zip(ex.areas, area_geoms) if sacred.is_heritage_square(a) and g is not None]
    heritage_front = None
    if heritage_polys:
        heritage_front = shapely.buffer(shapely.union_all(heritage_polys), style.HERITAGE_FRONT_M)
        shapely.prepare(heritage_front)
    st["sacred_zones"] = {"areas": zones.count, "by_kind": zones.by_kind,
                          "flagged_areas": int((area_flags != 0).sum())}

    # Area-type grid over the leaf tiles.
    box = leaf_box
    if box is None:
        from .projection import bbox_lonlat_to_game

        box = bbox_lonlat_to_game(*region.bbox)
    forest = [g for a, g in zip(ex.areas, area_geoms) if int(a.kind) == int(AreaKind.FOREST) and g is not None]
    forest_geom = shapely.union_all(forest) if forest else None

    def tree_share(x, z):
        if landcover is None:
            return np.zeros(len(x))
        return (np.asarray(landcover.sample_game(x, z)) == TREE_COVER).astype(np.float64)

    def elevation(x, z):
        return dem.sample_game(x, z) if dem is not None else np.full(len(x), np.nan)

    grid = build_grid(box, areas[~is_part], cxz[~is_part], core_geoms=state.zones.core_geoms_game(),
                      forest_geom=forest_geom, tree_share=tree_share, elevation=elevation)
    st["area_types"] = grid.counts_by_type()

    # Profiles (parts take their host's), small-town storey sanity, front hints.
    from .projection import game_to_lonlat

    lon, lat = game_to_lonlat(cxz[:, 0], cxz[:, 1])
    at = np.asarray(grid.at(cxz[:, 0], cxz[:, 1]), dtype=np.uint8).reshape(-1) if n else np.zeros(0, np.uint8)
    profiles, pst = style.assign_profiles(state.zones, np.asarray(lon), np.asarray(lat), at, ex.places, ex.admin)
    for p, h in state.part_hosts.items():
        profiles[p] = profiles[h]
    suspect = 0
    for i, b in enumerate(ex.buildings):
        if int(profiles[i]) in SMALL_TOWN_PROFILES and b.levels is not None and b.levels > SMALL_TOWN_MAX_LEVELS \
                and not (b.flags & BuildingFlags.LEVELS_INFERRED):
            b.flags = BuildingFlags(b.flags) | BuildingFlags.TAG_SUSPECT
            suspect += 1
    hints = np.array([style.hint_flags(b) for b in ex.buildings], dtype=np.int64)
    vals, cnt = np.unique(profiles, return_counts=True)
    st["style_profiles"] = {StyleProfile(int(v)).name: int(c) for v, c in zip(vals, cnt)}
    st["style_rules"] = pst
    st["small_town_tag_suspect"] = suspect

    # Roads: dual carriageways and bus routes.
    roads_game = project_lonlat_arrays([r.lonlat[::-1] if r.oneway == -1 else r.lonlat for r in ex.roads])
    dual = roadattrs.pair_dual_carriageways(ex.roads, roads_game)
    bus = frozenset(transit.bus_way_ids(routes))
    st["dual"] = dual.stats()
    st["bus_route_ways"] = len(bus)

    # Junctions.
    jl, jst = junctions.find_junctions(ex.roads, ex.junction_nodes, ex.areas, ex.props,
                                       junctions.load_chowks(state.cfg.chowks), sacred=zones.geom,
                                       area_geoms_game=area_geoms)
    st["junctions"] = jst
    return W2Inputs(grid=grid, profiles=profiles, shops=state.shops, front_hints=hints, area_flags=area_flags,
                    sacred=zones, heritage_front=heritage_front, dual=dual, bus_ways=bus, junctions=jl,
                    corridors=state.cfg.corridors)


def build_transit(state: W2State, extract: Extract, region: Region) -> transit.RouteSet:
    rs, s = transit.build_routes(extract, transit.load_transit(state.cfg.transit), region.bbox)
    state.stats["transit"] = s
    return rs


def chunk_stats(tiles) -> dict:
    """Sizes and contents of the W2 chunks in the encoded tiles (``{key: GHT1 blob}``), for the build report:
    stored bytes per chunk, corridor samples and the mapping-offset check (W2_DESIGN 9.5), fronts, props and
    junctions by kind."""
    from collections import Counter

    from .model import JunctionKind, ObjectKind
    from .tile_format import FOURCC_BFNT, FOURCC_JNCT, FOURCC_PROP, FOURCC_RATR, decode_tile, read_header

    names = {FOURCC_RATR: "RATR", FOURCC_JNCT: "JNCT", FOURCC_BFNT: "BFNT", FOURCC_PROP: "PROP"}
    chunk_bytes = Counter()
    samples = open_ = tagged = narrower = 0
    vals: list[np.ndarray] = []
    nb = fronts = corners = 0
    props: Counter = Counter()
    jk: Counter = Counter()
    for key in sorted(tiles):
        blob = tiles[key]
        h = read_header(blob)
        found = [c for c in h.chunks if c.fourcc in names]
        if not found:
            continue
        for c in found:
            chunk_bytes[names[c.fourcc]] += int(c.stored_size)
        td = decode_tile(blob)
        for r, a in zip(td.roads, td.road_attrs):
            c = np.asarray(a.corridor_dm, dtype=np.int64)
            if not len(c):
                continue
            samples += len(c)
            open_ += int((c == 0).sum())
            vals.append(c[c > 0])
            if r.width_cm:
                tagged += int((c > 0).sum())
                narrower += int(((c > 0) & (c * 10 < int(r.width_cm))).sum())
        for f in td.building_fronts:
            nb += 1
            fronts += f.front_edge != 255
            corners += f.second_edge != 255
        props.update(ObjectKind(p.kind).name for p in td.props)
        jk.update(JunctionKind(j.kind).name for j in td.junctions)
    v = np.concatenate(vals) / 10.0 if vals else np.zeros(0)
    pct = [round(float(x), 1) for x in np.percentile(v, [25, 50, 75])] if len(v) else []
    return {"chunk_bytes": dict(sorted(chunk_bytes.items())),
            "corridor": {"samples": samples, "open": open_, "p25_p50_p75_m": pct, "tagged_width_samples": tagged,
                         "narrower_than_tagged": narrower,
                         "narrower_pct": round(100.0 * narrower / tagged, 2) if tagged else 0.0},
            "fronts": {"buildings": nb, "with_front": fronts, "corners": corners},
            "props": dict(sorted(props.items())), "junction_kinds": dict(sorted(jk.items()))}


def write_files(state: W2State, extract: Extract, region: Region, reg_dir: Path, routes: transit.RouteSet,
                dem=None) -> list[Path]:
    """Region side files; returns the paths to list in the manifest."""
    rid = region.id
    out: list[Path] = []
    p = reg_dir / f"{rid}.transit.ghrt"
    state.stats["transit_file"] = transit.write_ghrt(p, routes)
    out.append(p)
    recs = curated.to_records(state.resolved, extract, region.leaf_level)
    p = reg_dir / f"{rid}.curated.ghcd"
    state.stats["curated_file"] = curated.write_ghcd(p, recs)
    out.append(p)
    p = reg_dir / "hero_recipes.json"
    p.write_text(curated.hero_recipes_json(recs, rid), encoding="utf-8")
    out.append(p)
    for airport in list(region.extra.get("aviation", []) or []):
        av, s = aviation.build_aviation(extract, str(airport),
                                        elevation=(lambda x, z: dem.sample_game(x, z)) if dem is not None else None)
        state.stats.setdefault("aviation", {})[str(airport)] = s
        if av is None:
            continue
        p = reg_dir / f"{rid}.aviation.json"
        aviation.write_aviation(p, av)
        out.append(p)
    return out
