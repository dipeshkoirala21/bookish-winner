"""POIs and parts that tell a footprint what it is (D1, D4).

Run once per extract, before ``buildings.infer_buildings``:

* ``apply_poi_footprints``: a religious POI node (temple, stupa, gompa, shrine,
  generic place of worship) inside a building footprint that carries no
  religion of its own passes its religion, use and structure word to it, so
  the archetype rules see a temple (213 such cases in the valley). The POI
  gets ``PoiFlags.HAS_FOOTPRINT``. Shop-like POIs (shops, restaurants, cafes,
  banks, markets) inside a footprint are counted for ``BFNT.shop_bays``.
  Only footprints up to ``MAX_TEMPLE_FOOTPRINT_M2`` take a religion from a POI
  (a shrine room inside a big apartment block does not make it a temple).
* ``link_parts``: every ``building:part`` whose centroid lies inside a
  non-part footprint marks that host ``HAS_PARTS``; returns part -> host.
"""

from __future__ import annotations

from collections import Counter
from typing import Sequence

import numpy as np
import shapely
from shapely.geometry import Polygon

from .buildings import footprint_area_m2
from .model import BuildingFeature, BuildingFlags, BuildingUse, PoiFeature, PoiFlags, PoiKind

MAX_TEMPLE_FOOTPRINT_M2 = 900.0
MAX_SHOP_BAYS = 15
RELIGIOUS_KINDS = {PoiKind.TEMPLE_HINDU: ("hindu", "temple"), PoiKind.STUPA: ("buddhist", "stupa"),
                   PoiKind.GOMPA: ("buddhist", "temple"), PoiKind.SHRINE: ("", "shrine"),
                   PoiKind.PLACE_OF_WORSHIP: ("", "temple"), PoiKind.CHORTEN: ("buddhist", "chorten")}
SHOP_KINDS = frozenset({PoiKind.SHOP, PoiKind.RESTAURANT, PoiKind.CAFE, PoiKind.BANK, PoiKind.MARKETPLACE})
_TAKES_RELIGION = frozenset({BuildingUse.UNKNOWN, BuildingUse.HOUSE, BuildingUse.RELIGIOUS})


def _polygons(buildings: Sequence[BuildingFeature]) -> np.ndarray:
    arr = np.empty(len(buildings), dtype=object)
    arr[:] = [Polygon(b.outer, [h for h in b.holes if len(h) >= 3]) if len(b.outer) >= 3 else Polygon()
              for b in buildings]
    return arr


def _point_in_footprint(polys: np.ndarray, lon: np.ndarray, lat: np.ndarray, candidates: np.ndarray
                        ) -> np.ndarray:
    """Index of the smallest non-part footprint containing each point (-1 if none)."""
    out = np.full(len(lon), -1, dtype=np.int64)
    if not len(lon) or not len(polys):
        return out
    tree = shapely.STRtree(polys)
    idx = tree.query(shapely.points(lon, lat), predicate="within")
    if not idx.shape[1]:
        return out
    pt, bi = idx[0], idx[1]
    ok = candidates[bi]
    pt, bi = pt[ok], bi[ok]
    area = shapely.area(polys[bi])
    order = np.lexsort((bi, area, pt))  # per point: smallest footprint first
    pt, bi = pt[order], bi[order]
    first = np.ones(len(pt), dtype=bool)
    first[1:] = pt[1:] != pt[:-1]
    out[pt[first]] = bi[first]
    return out


def apply_poi_footprints(buildings: list[BuildingFeature], pois: list[PoiFeature]) -> tuple[np.ndarray, dict]:
    """Pass religious POI hints to their footprints (in place); return shop counts per building."""
    shops = np.zeros(len(buildings), dtype=np.int64)
    stats: Counter = Counter()
    if not buildings or not pois:
        return shops, dict(stats)
    polys = _polygons(buildings)
    host_ok = np.array([not (b.flags & BuildingFlags.PART) for b in buildings], dtype=bool)
    lon = np.array([p.lon for p in pois], dtype=np.float64)
    lat = np.array([p.lat for p in pois], dtype=np.float64)
    hit = _point_in_footprint(polys, lon, lat, host_ok)
    for k, p in enumerate(pois):
        b = int(hit[k])
        if b < 0:
            continue
        if p.kind in SHOP_KINDS:
            shops[b] = min(MAX_SHOP_BAYS, shops[b] + 1)
            stats["shop_pois_in_footprints"] += 1
            continue
        rk = RELIGIOUS_KINDS.get(p.kind)
        if rk is None or p.osm_type != "n":
            continue
        bf = buildings[b]
        p.flags = PoiFlags(p.flags) | PoiFlags.HAS_FOOTPRINT
        if bf.religion or bf.use not in _TAKES_RELIGION or footprint_area_m2(bf) > MAX_TEMPLE_FOOTPRINT_M2:
            stats["religious_pois_footprint_kept"] += 1
            continue
        religion = (p.tags.get("religion") or rk[0] or "").strip()
        if religion:
            bf.religion = religion
        bf.use = BuildingUse.RELIGIOUS
        if bf.building_raw in ("yes", "house", "") or p.kind == PoiKind.STUPA:
            bf.building_raw = rk[1]
        stats["religious_pois_to_footprints"] += 1
    stats["buildings_with_shops"] = int((shops > 0).sum())
    return shops, dict(stats)


def link_parts(buildings: list[BuildingFeature]) -> tuple[dict[int, int], dict]:
    """``{part index: host index}``; hosts get ``HAS_PARTS`` (in place)."""
    parts = [i for i, b in enumerate(buildings) if b.flags & BuildingFlags.PART]
    if not parts:
        return {}, {"parts": 0, "parts_with_host": 0, "hosts": 0}
    polys = _polygons(buildings)
    host_ok = np.array([not (b.flags & BuildingFlags.PART) for b in buildings], dtype=bool)
    cen = shapely.centroid(polys[parts])
    hit = _point_in_footprint(polys, shapely.get_x(cen), shapely.get_y(cen), host_ok)
    out: dict[int, int] = {}
    for p, h in zip(parts, hit.tolist()):
        if h >= 0:
            out[p] = int(h)
            buildings[h].flags = BuildingFlags(buildings[h].flags) | BuildingFlags.HAS_PARTS
    return out, {"parts": len(parts), "parts_with_host": len(out), "hosts": len(set(out.values()))}
