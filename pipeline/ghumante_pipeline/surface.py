"""Road surface inference (ARCHITECTURE.md section 6.2, ADR-005).

85% of Nepal's road length has no ``surface`` tag. ``assign_surfaces`` gives
every road a surface and records where it came from (``SurfaceSource``):

1. ``TAGGED``: ``surface=*`` that ``tags.normalize_surface`` understands.
2. ``DERIVED``: ``tracktype``, then ``smoothness``.
3. ``INFERRED``: the statistical model below, or the observed surfaces of the
   same road chain.
4. ``DEFAULT``: the model knows nothing about the class, so the hand prior
   alone decides.

The model
---------
``SurfaceModel`` is an empirical conditional distribution
P(surface | road class, settlement density, elevation, terrain slope). It is
fitted on the TAGGED and DERIVED roads of the same build, with counts measured
in kilometres. There are four nested conditioning levels:

    (class) -> (class, density) -> (class, density, elev) -> (class, density, elev, slope)

A level is used only when it has at least ``MIN_SUPPORT_KM`` of observed road.
Each level used is smoothed toward the level above it (hierarchical additive
smoothing): ``p = (counts_km + ALPHA * p_parent) / (total_km + ALPHA)``. The
parent of the class level is the hand prior ``HAND_PRIORS[class]``.

The decision
------------
When the most likely surface has probability >= 0.6, it is chosen (ties go to
the lowest enum value). Otherwise the surface is *sampled* deterministically:
``u = (fnv1a64(chain_key as 8 little-endian bytes) mod 2^53) / 2^53`` is
looked up in the cumulative distribution over surfaces in enum order. Every
road in a chain shares ``u``, so a road does not change surface every 100 m.
Only float64 arithmetic in a fixed order is involved, so results are identical
on every platform.

Road chains
-----------
``road_chains`` joins ways that share an end node and have the same class,
name and ref. Every way in a chain gets one surface. ``assign_surfaces``
decides once per chain, from the length-weighted mean of its members' model
distributions combined with any surfaces observed on other members of the chain.
Bridge deck materials (``BRIDGE_ONLY_SURFACES``: METAL, WOOD) are removed from
that distribution unless every road being decided is a bridge, so a footway in
town is never inferred to be steel grating because tagged footbridges are.
"""

from __future__ import annotations

import json
import math
from dataclasses import dataclass
from typing import Iterable, Sequence

import numpy as np

from .binio import fnv1a64
from .model import RoadClass, RoadFeature, Surface, SurfaceSource
from .tags import normalize_surface, surface_from_smoothness, surface_from_tracktype
from .trails import polyline_lengths_m

MODEL_VERSION = 1
MIN_SUPPORT_KM = 2.0  # a conditioning level needs this much observed road to be used
ALPHA = 1.0  # km of pseudo-count pulling each level toward its parent
ARGMAX_THRESHOLD = 0.6
CHAIN_ALPHA_KM = 1.0  # pseudo-km of model belief against surfaces observed on the same chain
# Deck materials: in Nepal's tagged data they occur (almost) only on bridges, so the
# model never infers them for a chain that has a non-bridge member to decide.
BRIDGE_ONLY_SURFACES = (Surface.METAL, Surface.WOOD)
N_SURFACES = len(Surface)
_U_SCALE = 1 << 53
_MASK64 = (1 << 64) - 1

# Bin edges. A value equal to an edge falls in the upper bin.
DENSITY_EDGES = (0.5, 3.0, 12.0)  # buildings per hectare: rural, village, town, urban core
ELEV_EDGES = (500.0, 1500.0, 2500.0, 3500.0)  # m: Terai, hills, upper hills, high hills, Himalaya
SLOPE_EDGES = (5.0, 15.0)  # degrees of terrain slope: flat, rolling, steep


def _bin(v: float, edges: tuple[float, ...]) -> int:
    if v is None or not math.isfinite(v):
        return 0
    for i, edge in enumerate(edges):
        if v < edge:
            return i
    return len(edges)


def density_bin(buildings_per_ha: float) -> int:
    """0 rural (< 0.5 buildings/ha), 1 village (< 3), 2 town (< 12), 3 urban core. NaN -> 0."""
    return _bin(buildings_per_ha, DENSITY_EDGES)


def elev_bin(m: float) -> int:
    """0 < 500 m, 1 < 1500, 2 < 2500, 3 < 3500, 4 >= 3500. NaN -> 0."""
    return _bin(m, ELEV_EDGES)


def slope_bin(deg: float) -> int:
    """0 < 5 deg, 1 < 15, 2 >= 15. NaN -> 0."""
    return _bin(deg, SLOPE_EDGES)


@dataclass(frozen=True)
class RoadContext:
    """Where a road is: the conditioning variables of the surface model."""

    density_bin: int
    elev_bin: int
    slope_bin: int

    @classmethod
    def from_values(cls, buildings_per_ha: float, elev_m: float, slope_deg: float) -> "RoadContext":
        return cls(density_bin(buildings_per_ha), elev_bin(elev_m), slope_bin(slope_deg))


# ---------------------------------------------------------------------------
# Hand priors: what a Nepali road of each class is usually made of. Shaped by
# the tagged shares in docs/reports/tag_coverage.md. Each row sums to 1.
# ---------------------------------------------------------------------------
_S = Surface
HAND_PRIORS: dict[RoadClass, dict[Surface, float]] = {
    RoadClass.UNKNOWN: {_S.DIRT: 0.60, _S.GRAVEL: 0.20, _S.ASPHALT: 0.20},
    RoadClass.MOTORWAY: {_S.ASPHALT: 0.95, _S.CONCRETE: 0.05},
    RoadClass.TRUNK: {_S.ASPHALT: 0.85, _S.CONCRETE: 0.03, _S.GRAVEL: 0.05, _S.DIRT: 0.05, _S.COMPACTED: 0.02},
    RoadClass.PRIMARY: {_S.ASPHALT: 0.80, _S.CONCRETE: 0.03, _S.GRAVEL: 0.07, _S.DIRT: 0.07, _S.COMPACTED: 0.03},
    RoadClass.SECONDARY: {_S.ASPHALT: 0.55, _S.DIRT: 0.22, _S.GRAVEL: 0.15, _S.COMPACTED: 0.05,
                          _S.CONCRETE: 0.03},
    RoadClass.TERTIARY: {_S.DIRT: 0.40, _S.ASPHALT: 0.35, _S.GRAVEL: 0.17, _S.COMPACTED: 0.05,
                         _S.CONCRETE: 0.03},
    RoadClass.UNCLASSIFIED: {_S.DIRT: 0.65, _S.GRAVEL: 0.15, _S.ASPHALT: 0.10, _S.COMPACTED: 0.05,
                             _S.CONCRETE: 0.03, _S.MUD: 0.02},
    RoadClass.RESIDENTIAL: {_S.DIRT: 0.40, _S.ASPHALT: 0.30, _S.CONCRETE: 0.12, _S.GRAVEL: 0.10,
                            _S.BRICK: 0.05, _S.COMPACTED: 0.03},
    RoadClass.LIVING_STREET: {_S.ASPHALT: 0.35, _S.CONCRETE: 0.25, _S.BRICK: 0.20, _S.DIRT: 0.15,
                              _S.COBBLE: 0.05},
    RoadClass.SERVICE: {_S.DIRT: 0.35, _S.ASPHALT: 0.30, _S.CONCRETE: 0.15, _S.GRAVEL: 0.15, _S.BRICK: 0.05},
    RoadClass.TRACK: {_S.DIRT: 0.60, _S.GRAVEL: 0.20, _S.COMPACTED: 0.10, _S.MUD: 0.05, _S.GRASS: 0.05},
    RoadClass.ROAD: {_S.DIRT: 0.60, _S.GRAVEL: 0.20, _S.ASPHALT: 0.15, _S.COMPACTED: 0.05},
    RoadClass.PEDESTRIAN: {_S.BRICK: 0.45, _S.CONCRETE: 0.20, _S.COBBLE: 0.20, _S.ASPHALT: 0.10, _S.DIRT: 0.05},
    RoadClass.FOOTWAY: {_S.DIRT: 0.30, _S.COBBLE: 0.25, _S.CONCRETE: 0.25, _S.BRICK: 0.15, _S.ASPHALT: 0.05},
    RoadClass.PATH: {_S.DIRT: 0.75, _S.ROCK: 0.10, _S.GRASS: 0.10, _S.GRAVEL: 0.05},
    RoadClass.STEPS: {_S.COBBLE: 0.45, _S.ROCK: 0.25, _S.CONCRETE: 0.20, _S.BRICK: 0.10},
    RoadClass.CYCLEWAY: {_S.ASPHALT: 0.50, _S.CONCRETE: 0.30, _S.DIRT: 0.20},
    RoadClass.BRIDLEWAY: {_S.DIRT: 0.80, _S.GRAVEL: 0.10, _S.GRASS: 0.10},
}
del _S


def _prior_array(cls: RoadClass) -> np.ndarray:
    row = HAND_PRIORS.get(cls) or HAND_PRIORS[RoadClass.UNKNOWN]
    arr = np.zeros(N_SURFACES, dtype=np.float64)
    for s, p in row.items():
        arr[int(s)] = p
    return arr / arr.sum()


_PRIORS: dict[int, np.ndarray] = {int(c): _prior_array(c) for c in RoadClass}


# ---------------------------------------------------------------------------
# Deterministic decision
# ---------------------------------------------------------------------------
def chain_uniform(chain_key: int) -> float:
    """u in [0, 1) from the chain key: (fnv1a64(8-byte little-endian two's complement) mod 2^53) / 2^53."""
    h = fnv1a64((int(chain_key) & _MASK64).to_bytes(8, "little"))
    return (h % _U_SCALE) / float(_U_SCALE)


def decide(p: Sequence[float], chain_key: int) -> tuple[Surface, float]:
    """Pick a surface from a distribution over ``Surface`` values (index = enum value).

    Returns ``(surface, probability of that surface)``: the arg-max when its
    probability is >= ``ARGMAX_THRESHOLD``, otherwise an inverse-CDF sample at
    ``chain_uniform(chain_key)`` over surfaces in enum order. ``UNKNOWN`` (index
    0) is never chosen.
    """
    probs = [float(x) for x in p]
    probs[0] = 0.0
    total = sum(probs)
    if not total > 0.0:
        return Surface.UNKNOWN, 0.0
    probs = [x / total for x in probs]
    best = 1
    for i in range(2, len(probs)):
        if probs[i] > probs[best]:
            best = i
    if probs[best] >= ARGMAX_THRESHOLD:
        return Surface(best), probs[best]
    u = chain_uniform(chain_key)
    acc = 0.0
    last = best
    for i in range(1, len(probs)):
        if probs[i] <= 0.0:
            continue
        acc += probs[i]
        last = i
        if u < acc:
            return Surface(i), probs[i]
    return Surface(last), probs[last]  # only reachable through rounding when u is ~1


# ---------------------------------------------------------------------------
# Model
# ---------------------------------------------------------------------------
_LEVEL_NAMES = ("class", "density", "density_elev", "density_elev_slope")


class SurfaceModel:
    """Hierarchically smoothed P(surface | class, density, elevation, slope).

    ``counts`` maps a conditioning key to kilometres per surface (an array
    indexed by ``Surface`` value). The keys are ``(cls,)``, ``(cls, d)``,
    ``(cls, d, e)`` and ``(cls, d, e, s)``, all ints.
    """

    def __init__(self, counts: dict[tuple[int, ...], np.ndarray] | None = None, *,
                 alpha: float = ALPHA, min_support_km: float = MIN_SUPPORT_KM) -> None:
        self.counts: dict[tuple[int, ...], np.ndarray] = {
            tuple(int(x) for x in k): np.asarray(v, dtype=np.float64) for k, v in (counts or {}).items()}
        self.alpha = float(alpha)
        self.min_support_km = float(min_support_km)
        self._cache: dict[tuple[int, int, int, int], tuple[np.ndarray, int]] = {}

    @classmethod
    def fit(cls, samples: Iterable[tuple[RoadClass, RoadContext, Surface, float]], *,
            alpha: float = ALPHA, min_support_km: float = MIN_SUPPORT_KM) -> "SurfaceModel":
        """Fit from ``(class, context, observed surface, length_km)`` samples.

        Samples with an UNKNOWN surface or a non-positive or non-finite length
        are ignored.
        """
        counts: dict[tuple[int, ...], np.ndarray] = {}
        for rc, ctx, surf, km in samples:
            km = float(km)
            if surf is None or int(surf) == int(Surface.UNKNOWN) or not (math.isfinite(km) and km > 0.0):
                continue
            for key in cls._keys(rc, ctx):
                arr = counts.get(key)
                if arr is None:
                    arr = counts[key] = np.zeros(N_SURFACES, dtype=np.float64)
                arr[int(surf)] += km
        return cls(counts, alpha=alpha, min_support_km=min_support_km)

    @staticmethod
    def _keys(rc: RoadClass, ctx: RoadContext) -> tuple[tuple[int, ...], ...]:
        c, d, e, s = int(rc), int(ctx.density_bin), int(ctx.elev_bin), int(ctx.slope_bin)
        return (c,), (c, d), (c, d, e), (c, d, e, s)

    def distribution_array(self, rc: RoadClass, ctx: RoadContext) -> tuple[np.ndarray, int]:
        """``(p, depth)``: p indexed by ``Surface`` value; depth = number of levels used
        (0 means the hand prior alone)."""
        ck = (int(rc), int(ctx.density_bin), int(ctx.elev_bin), int(ctx.slope_bin))
        hit = self._cache.get(ck)
        if hit is not None:
            return hit[0].copy(), hit[1]
        p = _PRIORS.get(int(rc), _PRIORS[int(RoadClass.UNKNOWN)]).copy()
        depth = 0
        for key in self._keys(rc, ctx):
            c = self.counts.get(key)
            if c is None:
                break
            total = float(c.sum())
            if total < self.min_support_km:
                break
            p = (c + self.alpha * p) / (total + self.alpha)
            depth += 1
        self._cache[ck] = (p, depth)
        return p.copy(), depth

    def distribution(self, rc: RoadClass, ctx: RoadContext) -> dict[Surface, float]:
        """The smoothed distribution as ``{surface: p}`` (zero entries left out)."""
        p, _ = self.distribution_array(rc, ctx)
        return {Surface(i): float(p[i]) for i in range(1, N_SURFACES) if p[i] > 0.0}

    def support_depth(self, rc: RoadClass, ctx: RoadContext) -> int:
        """How many conditioning levels had enough data (0 = hand prior only)."""
        return self.distribution_array(rc, ctx)[1]

    def predict(self, rc: RoadClass, ctx: RoadContext, chain_key: int) -> tuple[Surface, float]:
        """``(surface, probability)`` for one road; see ``decide``."""
        p, _ = self.distribution_array(rc, ctx)
        return decide(p, chain_key)

    # --- serialisation -------------------------------------------------------
    def to_json(self) -> dict:
        """JSON-serialisable dict (sparse; surfaces by name). Keys are sorted."""
        cells = []
        for key in sorted(self.counts):
            arr = self.counts[key]
            cells.append({"key": list(key),
                          "km": {Surface(i).name: float(arr[i]) for i in range(N_SURFACES) if arr[i] != 0.0}})
        return {"version": MODEL_VERSION, "alpha_km": self.alpha, "min_support_km": self.min_support_km,
                "cells": cells}

    @classmethod
    def from_json(cls, data: dict | str) -> "SurfaceModel":
        if isinstance(data, str):
            data = json.loads(data)
        if int(data.get("version", 0)) != MODEL_VERSION:
            raise ValueError(f"unsupported surface model version {data.get('version')}")
        counts: dict[tuple[int, ...], np.ndarray] = {}
        for cell in data["cells"]:
            arr = np.zeros(N_SURFACES, dtype=np.float64)
            for name, km in cell["km"].items():
                arr[int(Surface[name])] = float(km)
            counts[tuple(int(x) for x in cell["key"])] = arr
        return cls(counts, alpha=data["alpha_km"], min_support_km=data["min_support_km"])

    def report(self) -> dict:
        """Per-class fitted kilometres, surface shares and supported cells per level."""
        supported = {name: {} for name in _LEVEL_NAMES}
        for key, arr in self.counts.items():
            if float(arr.sum()) >= self.min_support_km:
                level = supported[_LEVEL_NAMES[len(key) - 1]]
                level[key[0]] = level.get(key[0], 0) + 1
        classes = {}
        fitted = 0.0
        for key in sorted(k for k in self.counts if len(k) == 1):
            arr = self.counts[key]
            total = float(arr.sum())
            fitted += total
            name = _class_name(key[0])
            classes[name] = {
                "fitted_km": round(total, 3),
                "surface_pct": {Surface(i).name: round(100.0 * arr[i] / total, 2)
                                for i in range(N_SURFACES) if arr[i] > 0.0} if total > 0 else {},
                "supported_cells": {lvl: supported[lvl].get(key[0], 0) for lvl in _LEVEL_NAMES},
            }
        return {"version": MODEL_VERSION, "alpha_km": self.alpha, "min_support_km": self.min_support_km,
                "argmax_threshold": ARGMAX_THRESHOLD, "fitted_km": round(fitted, 3), "classes": classes}


# ---------------------------------------------------------------------------
# Chains
# ---------------------------------------------------------------------------
def _chain_ident(road: RoadFeature) -> tuple[int, str, str]:
    name = road.name.default.strip() if road.name is not None else ""
    return int(road.cls), name, (road.ref or "").strip()


def road_chains(roads: Sequence[RoadFeature]) -> list[int]:
    """Chain key per road: the smallest ``osm_id`` of the chain it belongs to.

    Two ways are joined when they share an *end* node and have the same class,
    the same ``name.default`` and the same ``ref`` (either may be empty).
    Named or numbered ways (name or ref not empty) join through any number of
    same-identity ways at a node. Unnamed, unnumbered ways join only where
    exactly two of them meet, which is where a mapper split one street. At a
    junction of three or more, each unnamed street stays its own chain;
    otherwise a town's whole unnamed street network would become one chain.
    """
    n = len(roads)
    parent = list(range(n))

    def find(i: int) -> int:
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    ends: dict[tuple[int, tuple[int, str, str]], list[int]] = {}
    for i, road in enumerate(roads):
        ids = road.node_ids
        if ids is None or len(ids) == 0:
            continue
        ident = _chain_ident(road)
        ends.setdefault((int(ids[0]), ident), []).append(i)
        ends.setdefault((int(ids[-1]), ident), []).append(i)

    for (_, ident), members in ends.items():
        if len(members) < 2 or (len(members) > 2 and not (ident[1] or ident[2])):
            continue
        root = find(members[0])
        for m in members[1:]:
            r = find(m)
            if r != root:
                if r < root:
                    parent[root] = r
                    root = r
                else:
                    parent[r] = root

    min_id: dict[int, int] = {}
    for i, road in enumerate(roads):
        r = find(i)
        oid = int(road.osm_id)
        if r not in min_id or oid < min_id[r]:
            min_id[r] = oid
    return [min_id[find(i)] for i in range(n)]


# ---------------------------------------------------------------------------
# Assignment
# ---------------------------------------------------------------------------
def _observed_surface(road: RoadFeature) -> tuple[Surface | None, SurfaceSource | None, bool]:
    """(surface, source, raw-present-but-unrecognised)."""
    raw = road.surface_raw
    raw_present = raw is not None and bool(str(raw).strip())
    if raw_present:
        s = normalize_surface(raw)
        if s is not None:
            return s, SurfaceSource.TAGGED, False
    s = surface_from_tracktype(road.tracktype) if road.tracktype else None
    if s is None and road.smoothness:
        s = surface_from_smoothness(road.smoothness)
    if s is not None:
        return s, SurfaceSource.DERIVED, raw_present
    return None, None, raw_present


def _without_bridge_only(p: np.ndarray, cls: RoadClass) -> np.ndarray:
    """``p`` with ``BRIDGE_ONLY_SURFACES`` removed (``decide`` renormalises); the
    class prior, likewise filtered, if nothing else is left."""
    q = np.array(p, dtype=np.float64)
    for s in BRIDGE_ONLY_SURFACES:
        q[int(s)] = 0.0
    if not q[1:].sum() > 0.0:
        q = _PRIORS.get(int(cls), _PRIORS[int(RoadClass.UNKNOWN)]).copy()
        for s in BRIDGE_ONLY_SURFACES:
            q[int(s)] = 0.0
    return q


def _class_name(value: int) -> str:
    try:
        return RoadClass(value).name
    except ValueError:
        return str(value)


def _pct(part: float, whole: float) -> float:
    return round(100.0 * part / whole, 2) if whole > 0 else 0.0


def assign_surfaces(roads: Sequence[RoadFeature], contexts: Sequence[RoadContext],
                    model: SurfaceModel | None = None) -> dict:
    """Set ``surface`` and ``surface_source`` on every road, in place.

    ``contexts[i]`` is the context of ``roads[i]``. The order is TAGGED
    (``surface_raw``), then DERIVED (``tracktype``, then ``smoothness``), then
    the model. When ``model`` is None it is fitted on this call's TAGGED and
    DERIVED roads. Untagged roads are decided once per chain (``road_chains``)
    from the length-weighted mean of their model distributions. When other
    members of the chain have an observed surface, that evidence is mixed in
    (``CHAIN_ALPHA_KM``) and the source is INFERRED. A chain whose members
    all have only the hand prior gets DEFAULT. ``surface_raw`` is left as the
    extractor set it.

    Returns statistics: kilometres and length-weighted percentages per source,
    overall and per class; the final surface mix; unrecognised raw values; and
    ``model.report()``.
    """
    if len(contexts) != len(roads):
        raise ValueError(f"{len(contexts)} contexts for {len(roads)} roads")
    n = len(roads)
    length_km = (polyline_lengths_m([r.lonlat for r in roads]) / 1000.0).tolist()
    observed: list[Surface | None] = [None] * n
    unrecognised_roads, unrecognised_km = 0, 0.0
    for i, road in enumerate(roads):
        s, src, unrecognised = _observed_surface(road)
        if unrecognised:
            unrecognised_roads += 1
            unrecognised_km += length_km[i]
        if s is not None:
            road.surface, road.surface_source = s, src
            observed[i] = s

    if model is None:
        model = SurfaceModel.fit((roads[i].cls, contexts[i], observed[i], length_km[i])
                                 for i in range(n) if observed[i] is not None)

    chains = road_chains(roads)
    members: dict[int, list[int]] = {}
    for i, key in enumerate(chains):
        members.setdefault(key, []).append(i)

    for key, idx in members.items():
        pending = [i for i in idx if observed[i] is None]
        if not pending:
            continue
        acc = np.zeros(N_SURFACES, dtype=np.float64)
        weight = 0.0
        depth = 0
        for i in pending:
            p, d = model.distribution_array(roads[i].cls, contexts[i])
            w = max(length_km[i], 1e-6)
            acc += w * p
            weight += w
            depth = max(depth, d)
        p = acc / weight
        seen = np.zeros(N_SURFACES, dtype=np.float64)
        for i in idx:
            if observed[i] is not None:
                seen[int(observed[i])] += length_km[i]
        seen_km = float(seen.sum())
        if seen_km > 0.0:
            p = (seen + CHAIN_ALPHA_KM * p) / (seen_km + CHAIN_ALPHA_KM)
        source = SurfaceSource.INFERRED if (depth > 0 or seen_km > 0.0) else SurfaceSource.DEFAULT
        if not all(roads[i].bridge for i in pending):
            p = _without_bridge_only(p, roads[pending[0]].cls)
        surface, _ = decide(p, key)
        for i in pending:
            roads[i].surface = surface
            roads[i].surface_source = source

    km_by_source = {s.name: 0.0 for s in SurfaceSource}
    km_by_surface = {s.name: 0.0 for s in Surface}
    by_class: dict[int, dict[str, float]] = {}
    for i, road in enumerate(roads):
        src = SurfaceSource(road.surface_source).name
        km_by_source[src] += length_km[i]
        km_by_surface[Surface(road.surface).name] += length_km[i]
        row = by_class.setdefault(int(road.cls), {s.name: 0.0 for s in SurfaceSource})
        row[src] += length_km[i]
    total_km = sum(length_km)
    return {
        "roads": n,
        "chains": len(members),
        "total_km": round(total_km, 3),
        "km_by_source": {k: round(v, 3) for k, v in km_by_source.items()},
        "pct_by_source": {k: _pct(v, total_km) for k, v in km_by_source.items()},
        "by_class": {
            RoadClass(c).name: {"km": round(sum(row.values()), 3),
                                "pct_by_source": {k: _pct(v, sum(row.values())) for k, v in row.items()}}
            for c, row in sorted(by_class.items())
        },
        "surface_pct": {k: _pct(v, total_km) for k, v in km_by_surface.items() if v > 0.0},
        "surface_raw_unrecognised": {"roads": unrecognised_roads, "km": round(unrecognised_km, 3)},
        "model": model.report(),
    }
