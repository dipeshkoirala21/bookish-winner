"""Sacred and heritage zones (docs/W2_DESIGN.md 3.3 and 4.4; work items D14 and D19).

One rule set feeds the ``AREA`` flags, the routing graph (D14) and the
``RATR`` / ``JNCT`` flags, so the runtime ``SacredZoneIndex`` (built from the
same AREA records) and the routes agree:

* **SACRED_NO_VEHICLE** (``tile_format.AreaFlags``): RELIGIOUS areas (temple,
  stupa and gompa compounds), COURTYARD areas (bahals, bahis, palace chowks),
  heritage squares, and every curated compound (``heritage_sites.yaml``
  ``compound``). No motor vehicle may enter (W2_DESIGN L16).
* **HERITAGE_ZONE**: heritage squares (a PEDESTRIAN area with a ``heritage``
  tag, a Durbar-square name, or ``place=square`` with a wikidata id) and the
  curated compounds.

``prepare_courtyards`` fixes the COURTYARD kind before tiling: a
*chowk*-named courtyard outside every curated old core (``style_zones.yaml``
``old_core`` zones) is a traffic junction, not a courtyard, and goes back to
its plain kind (PEDESTRIAN for a square, else dropped); the holes of building
footprints named like a bahal or chowk become COURTYARD areas of their own
(the relation id is the AREA's ``osm_ref``).

``SacredZones.motor_block`` answers "is this point inside a no-vehicle zone"
for ``routing.build_graph``: an edge whose length is at least half inside
loses every motor mode (CAR, JEEP, BUS, MOTORBIKE); FOOT, BICYCLE and HORSE
stay.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Iterable, Sequence

import numpy as np
import shapely
from shapely.geometry import Polygon

from . import tags as T
from .model import AreaFeature, AreaKind, BuildingFeature, SacredZoneKind
from .tile_format import AreaFlags

_DURBAR_RE = re.compile(r"durbar|darbar|दरबार")
_CHOWK_RE = re.compile(r"chowk|\bchok\b|चोक")


def is_heritage_square(a: AreaFeature) -> bool:
    if int(a.kind) != int(AreaKind.PEDESTRIAN):
        return False
    t = a.tags or {}
    names = " ".join(s for s in ((a.name.default, a.name.en, a.name.ne) if a.name else ()) if s).casefold()
    return bool(t.get("heritage")) or bool(_DURBAR_RE.search(names)) or (t.get("place") == "square"
                                                                         and bool(t.get("wikidata")))


def zone_kind(a: AreaFeature, compound_refs: set[tuple[str, int]], kora_refs: set[tuple[str, int]]
              ) -> SacredZoneKind:
    ref = (a.osm_type, int(a.osm_id))
    if ref in kora_refs:
        return SacredZoneKind.STUPA_KORA
    if int(a.kind) == int(AreaKind.COURTYARD):
        return SacredZoneKind.COURTYARD
    if int(a.kind) == int(AreaKind.RELIGIOUS) or ref in compound_refs:
        return SacredZoneKind.COMPOUND
    if is_heritage_square(a):
        return SacredZoneKind.HERITAGE_SQUARE
    return SacredZoneKind.NONE


def area_flags(a: AreaFeature, compound_refs: set[tuple[str, int]], kora_refs: set[tuple[str, int]]) -> int:
    k = zone_kind(a, compound_refs, kora_refs)
    if k == SacredZoneKind.NONE:
        return 0
    f = int(AreaFlags.SACRED_NO_VEHICLE)
    ref = (a.osm_type, int(a.osm_id))
    if k == SacredZoneKind.HERITAGE_SQUARE or ref in compound_refs:
        f |= int(AreaFlags.HERITAGE_ZONE)
    return f


def prepare_courtyards(areas: list[AreaFeature], buildings: Sequence[BuildingFeature], core_geoms_lonlat
                       ) -> dict:
    """Demote chowk-named courtyards outside the cores and add bahal/chowk building holes (in place)."""
    stats = {"courtyards_demoted": 0, "courtyards_dropped": 0, "courtyard_holes": 0}
    cores = shapely.union_all(list(core_geoms_lonlat)) if core_geoms_lonlat else None
    keep = []
    for a in areas:
        if int(a.kind) == int(AreaKind.COURTYARD):
            names = " ".join(s for s in ((a.name.default, a.name.en, a.name.ne) if a.name else ()) if s).casefold()
            bahal = T.COURTYARD_NAME_RE.search(_CHOWK_RE.sub("", names))
            if not bahal and (cores is None or not cores.intersects(a.polygon.representative_point())):
                base = T.area_kind({**(a.tags or {}), **({"name": a.name.default} if a.name else {})},
                                   courtyard=False)
                if base == AreaKind.NONE:
                    stats["courtyards_dropped"] += 1
                    continue
                a.kind = base
                stats["courtyards_demoted"] += 1
        keep.append(a)
    areas[:] = keep
    have = {(a.osm_type, int(a.osm_id)) for a in areas}
    for b in buildings:
        if not b.holes or b.name is None:
            continue
        names = " ".join(s for s in (b.name.default, b.name.en, b.name.ne) if s).casefold()
        if not T.COURTYARD_NAME_RE.search(names) or (b.osm_type, int(b.osm_id)) in have:
            continue
        for h in b.holes:
            if len(h) < 3:
                continue
            poly = shapely.make_valid(Polygon(h))
            if poly.is_empty or poly.area <= 0:
                continue
            areas.append(AreaFeature(b.osm_type, int(b.osm_id), AreaKind.COURTYARD, poly, name=b.name,
                                     tags={"courtyard": "building_hole"}))
            stats["courtyard_holes"] += 1
        have.add((b.osm_type, int(b.osm_id)))
    return stats


@dataclass
class SacredZones:
    """Game-space union of the SACRED_NO_VEHICLE areas."""

    geom: object = None
    count: int = 0
    by_kind: dict[str, int] = field(default_factory=dict)

    @classmethod
    def build(cls, areas: Sequence[AreaFeature], area_geoms_game: Sequence[object], compound_refs, kora_refs
              ) -> "SacredZones":
        polys, kinds = [], {}
        for a, g in zip(areas, area_geoms_game):
            k = zone_kind(a, compound_refs, kora_refs)
            if k == SacredZoneKind.NONE or g is None or g.is_empty:
                continue
            polys.append(g)
            kinds[k.name] = kinds.get(k.name, 0) + 1
        if not polys:
            return cls(None, 0, kinds)
        geom = shapely.union_all(polys)
        shapely.prepare(geom)
        return cls(geom, len(polys), dict(sorted(kinds.items())))

    def contains(self, x, z) -> np.ndarray:
        if self.geom is None:
            return np.zeros(np.shape(x), dtype=bool)
        return shapely.contains_xy(self.geom, np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64))

    def motor_block(self, x: np.ndarray, z: np.ndarray) -> np.ndarray:
        """Per point (segment midpoints, game metres): inside a no-vehicle zone."""
        return self.contains(x, z)


def compound_sets(records: Iterable[dict]) -> tuple[set[tuple[str, int]], set[tuple[str, int]]]:
    """``(compound refs, kora refs)`` from heritage records (``curated.load_heritage``)."""
    comp, kora = set(), set()
    for r in records:
        c = r.get("compound")
        if not c:
            continue
        ref = (str(c)[0], int(str(c)[1:]))
        comp.add(ref)
        if str(r.get("kind", "")).upper() == "STUPA" and str(r.get("kora", "")).upper() in ("CLOCKWISE",
                                                                                              "ANTICLOCKWISE"):
            kora.add(ref)
    return comp, kora
