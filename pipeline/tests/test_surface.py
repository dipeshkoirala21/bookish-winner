"""Tests for ghumante_pipeline.surface: the road surface inference model."""

from __future__ import annotations

import copy
import json
import math
from collections import Counter

import numpy as np
import pytest

from ghumante_pipeline import surface as S
from ghumante_pipeline import tags as T
from ghumante_pipeline.binio import fnv1a64
from ghumante_pipeline.model import NameRec, RoadClass, RoadFeature, Surface, SurfaceSource
from ghumante_pipeline.surface import (
    HAND_PRIORS,
    RoadContext,
    SurfaceModel,
    assign_surfaces,
    chain_uniform,
    decide,
    density_bin,
    elev_bin,
    road_chains,
    slope_bin,
)
from ghumante_pipeline.trails import polyline_length_m

CTX = RoadContext(1, 1, 1)
LAT = 27.7


@pytest.fixture(autouse=True)
def _clean_unknown():
    T.reset_unknown()
    yield
    T.reset_unknown()


def make_road(osm_id: int, cls: RoadClass, nodes: list[int], *, km: float = 0.5, lon0: float = 85.3,
              raw: str | None = None, tracktype: int = 0, smoothness: str | None = None, name: str = "",
              ref: str | None = None) -> RoadFeature:
    """A straight east-west road of ``km`` kilometres with the given node ids."""
    dlon = km / (111.32 * math.cos(math.radians(LAT)))
    lons = np.linspace(lon0, lon0 + dlon, len(nodes))
    return RoadFeature(
        osm_id=osm_id, cls=cls, lonlat=np.column_stack((lons, np.full(len(nodes), LAT))),
        node_ids=np.asarray(nodes, dtype=np.int64), surface_raw=raw, tracktype=tracktype,
        smoothness=smoothness, name=NameRec(default=name) if name else None, ref=ref)


# ---------------------------------------------------------------------------
# Bins and context
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("v, expected", [(0.0, 0), (0.49, 0), (0.5, 1), (2.99, 1), (3.0, 2), (11.99, 2),
                                         (12.0, 3), (500.0, 3), (float("nan"), 0), (-1.0, 0)])
def test_density_bin(v, expected):
    assert density_bin(v) == expected


@pytest.mark.parametrize("v, expected", [(60.0, 0), (499.9, 0), (500.0, 1), (1349.0, 1), (1500.0, 2),
                                         (2499.0, 2), (2500.0, 3), (3499.0, 3), (3500.0, 4), (8848.0, 4),
                                         (float("nan"), 0)])
def test_elev_bin(v, expected):
    assert elev_bin(v) == expected


@pytest.mark.parametrize("v, expected", [(0.0, 0), (4.99, 0), (5.0, 1), (14.99, 1), (15.0, 2), (60.0, 2),
                                         (float("inf"), 0)])
def test_slope_bin(v, expected):
    assert slope_bin(v) == expected


def test_road_context_is_frozen_and_hashable():
    ctx = RoadContext.from_values(4.0, 1400.0, 20.0)
    assert ctx == RoadContext(2, 1, 2)
    assert {ctx: 1}[RoadContext(2, 1, 2)] == 1
    with pytest.raises(Exception):
        ctx.density_bin = 0  # type: ignore[misc]


# ---------------------------------------------------------------------------
# Hand priors
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("cls", list(RoadClass))
def test_hand_priors_cover_every_class_and_sum_to_one(cls):
    row = HAND_PRIORS[cls]
    assert math.isclose(sum(row.values()), 1.0, abs_tol=1e-9)
    assert Surface.UNKNOWN not in row
    assert all(p > 0 for p in row.values())


@pytest.mark.parametrize("cls, expected", [
    (RoadClass.TRUNK, Surface.ASPHALT), (RoadClass.PRIMARY, Surface.ASPHALT), (RoadClass.MOTORWAY, Surface.ASPHALT),
    (RoadClass.TRACK, Surface.DIRT), (RoadClass.PATH, Surface.DIRT), (RoadClass.UNCLASSIFIED, Surface.DIRT),
])
def test_hand_prior_argmax(cls, expected):
    surface, p = SurfaceModel().predict(cls, CTX, chain_key=1)
    assert surface == expected and p >= S.ARGMAX_THRESHOLD


# ---------------------------------------------------------------------------
# Fitting, backoff and smoothing
# ---------------------------------------------------------------------------
def test_fit_accumulates_every_level_and_skips_bad_samples():
    m = SurfaceModel.fit([
        (RoadClass.PRIMARY, RoadContext(1, 2, 0), Surface.ASPHALT, 1.5),
        (RoadClass.PRIMARY, RoadContext(1, 2, 1), Surface.GRAVEL, 0.5),
        (RoadClass.PRIMARY, RoadContext(1, 2, 1), Surface.UNKNOWN, 9.0),
        (RoadClass.PRIMARY, RoadContext(1, 2, 1), Surface.DIRT, 0.0),
        (RoadClass.PRIMARY, RoadContext(1, 2, 1), Surface.DIRT, float("nan")),
        (RoadClass.PRIMARY, RoadContext(1, 2, 1), None, 1.0),
    ])
    p = int(RoadClass.PRIMARY)
    assert m.counts[(p,)][Surface.ASPHALT] == 1.5 and m.counts[(p,)][Surface.GRAVEL] == 0.5
    assert m.counts[(p,)].sum() == 2.0
    assert m.counts[(p, 1)].sum() == 2.0
    assert m.counts[(p, 1, 2)].sum() == 2.0
    assert m.counts[(p, 1, 2, 0)][Surface.ASPHALT] == 1.5
    assert m.counts[(p, 1, 2, 1)][Surface.GRAVEL] == 0.5
    assert len(m.counts) == 5


def _onehot(surface: Surface, km: float) -> np.ndarray:
    a = np.zeros(S.N_SURFACES)
    a[int(surface)] = km
    return a


def test_hierarchical_smoothing_formula():
    cls = RoadClass.TERTIARY
    c = int(cls)
    counts = {
        (c,): _onehot(Surface.ASPHALT, 6.0) + _onehot(Surface.DIRT, 4.0),
        (c, 1): _onehot(Surface.ASPHALT, 2.0) + _onehot(Surface.DIRT, 3.0),
        (c, 1, 1): _onehot(Surface.DIRT, 2.5),
        (c, 1, 1, 1): _onehot(Surface.DIRT, 1.0),  # below MIN_SUPPORT_KM: not used
    }
    m = SurfaceModel(counts)
    p, depth = m.distribution_array(cls, RoadContext(1, 1, 1))
    assert depth == 3
    prior = S._PRIORS[c]
    expected = prior
    for key in [(c,), (c, 1), (c, 1, 1)]:
        expected = (counts[key] + S.ALPHA * expected) / (counts[key].sum() + S.ALPHA)
    np.testing.assert_allclose(p, expected, rtol=0, atol=1e-15)
    assert math.isclose(p.sum(), 1.0)
    dist = m.distribution(cls, RoadContext(1, 1, 1))
    assert set(dist) == {s for s in Surface if expected[int(s)] > 0}
    assert m.support_depth(cls, RoadContext(1, 1, 1)) == 3


def test_backoff_stops_at_first_thin_level():
    cls = RoadClass.SECONDARY
    c = int(cls)
    m = SurfaceModel({(c,): _onehot(Surface.GRAVEL, 10.0), (c, 2): _onehot(Surface.GRAVEL, 1.0),
                      (c, 2, 0): _onehot(Surface.GRAVEL, 1.0), (c, 2, 0, 0): _onehot(Surface.GRAVEL, 1.0)})
    _, depth = m.distribution_array(cls, RoadContext(2, 0, 0))
    assert depth == 1
    p_class_only, _ = SurfaceModel({(c,): _onehot(Surface.GRAVEL, 10.0)}).distribution_array(cls, RoadContext(2, 0, 0))
    np.testing.assert_array_equal(m.distribution_array(cls, RoadContext(2, 0, 0))[0], p_class_only)


def test_min_support_boundary():
    cls = RoadClass.RESIDENTIAL
    c = int(cls)
    at = SurfaceModel({(c,): _onehot(Surface.CONCRETE, S.MIN_SUPPORT_KM)})
    below = SurfaceModel({(c,): _onehot(Surface.CONCRETE, S.MIN_SUPPORT_KM - 1e-9)})
    assert at.support_depth(cls, CTX) == 1
    assert below.support_depth(cls, CTX) == 0
    np.testing.assert_array_equal(below.distribution_array(cls, CTX)[0], S._PRIORS[c])


def test_unseen_class_uses_hand_prior():
    m = SurfaceModel.fit([(RoadClass.PRIMARY, CTX, Surface.ASPHALT, 50.0)])
    p, depth = m.distribution_array(RoadClass.STEPS, CTX)
    assert depth == 0
    np.testing.assert_array_equal(p, S._PRIORS[int(RoadClass.STEPS)])


def test_finest_level_dominates_with_lots_of_data():
    samples = [(RoadClass.TRACK, RoadContext(0, 4, 2), Surface.ROCK, 1.0)] * 40
    samples += [(RoadClass.TRACK, RoadContext(0, 1, 0), Surface.DIRT, 1.0)] * 200
    m = SurfaceModel.fit(samples)
    assert m.predict(RoadClass.TRACK, RoadContext(0, 4, 2), 1)[0] == Surface.ROCK
    assert m.predict(RoadClass.TRACK, RoadContext(0, 1, 0), 1)[0] == Surface.DIRT
    # an unseen slope bin backs off to (track, rural, Himalaya): still rock
    assert m.predict(RoadClass.TRACK, RoadContext(0, 4, 0), 1)[0] == Surface.ROCK


def test_distribution_array_returns_copies():
    m = SurfaceModel()
    p, _ = m.distribution_array(RoadClass.PATH, CTX)
    p[:] = 0
    assert m.distribution_array(RoadClass.PATH, CTX)[0].sum() == pytest.approx(1.0)


# ---------------------------------------------------------------------------
# Decision
# ---------------------------------------------------------------------------
def test_chain_uniform_matches_spec():
    for key in (0, 1, 123456789, 2**40 + 7, -5):
        h = fnv1a64((key & (2**64 - 1)).to_bytes(8, "little"))
        assert chain_uniform(key) == (h % 2**53) / 2**53
        assert 0.0 <= chain_uniform(key) < 1.0


def test_chain_uniform_known_value():
    # fnv1a64 of eight zero bytes; pinned so a C# port can be checked against it
    assert fnv1a64(bytes(8)) == 0xA8C7F832281A39C5
    assert chain_uniform(0) == (0xA8C7F832281A39C5 % 2**53) / 2**53


def test_decide_argmax_at_threshold_and_ties():
    p = np.zeros(S.N_SURFACES)
    p[Surface.GRAVEL], p[Surface.DIRT] = 0.6, 0.4
    for key in range(50):
        assert decide(p, key) == (Surface.GRAVEL, pytest.approx(0.6))
    q = np.zeros(S.N_SURFACES)
    q[Surface.ASPHALT], q[Surface.DIRT] = 0.5, 0.5
    picks = {decide(q, key)[0] for key in range(200)}
    assert picks == {Surface.ASPHALT, Surface.DIRT}  # below threshold: sampled


def test_decide_normalises_and_ignores_unknown_mass():
    p = np.zeros(S.N_SURFACES)
    p[Surface.MUD] = p[Surface.CONCRETE] = 0.7  # unnormalised: 0.5 / 0.5, so sampled
    p[Surface.UNKNOWN] = 5.0  # never chosen, and not part of the normalisation
    for key in range(100):
        u = chain_uniform(key)
        assert decide(p, key) == (Surface.CONCRETE if u < 0.5 else Surface.MUD, pytest.approx(0.5))
    q = np.zeros(S.N_SURFACES)
    q[Surface.SAND], q[Surface.UNKNOWN] = 3.0, 100.0
    assert decide(q, 1) == (Surface.SAND, 1.0)


def test_decide_inverse_cdf_in_enum_order():
    p = np.zeros(S.N_SURFACES)
    p[Surface.ASPHALT], p[Surface.GRAVEL], p[Surface.DIRT] = 0.3, 0.3, 0.4
    for key in range(500):
        u = chain_uniform(key)
        expected = Surface.ASPHALT if u < 0.3 else Surface.GRAVEL if u < 0.6 else Surface.DIRT
        assert decide(p, key)[0] == expected


def test_decide_sampling_frequencies_follow_distribution():
    p = np.zeros(S.N_SURFACES)
    p[Surface.ASPHALT], p[Surface.GRAVEL], p[Surface.DIRT] = 0.2, 0.3, 0.5
    n = 20000
    freq = Counter(decide(p, key)[0] for key in range(1, n + 1))
    for s in (Surface.ASPHALT, Surface.GRAVEL, Surface.DIRT):
        assert abs(freq[s] / n - p[int(s)]) < 0.02


def test_decide_degenerate():
    assert decide(np.zeros(S.N_SURFACES), 1) == (Surface.UNKNOWN, 0.0)


def test_predict_is_deterministic():
    m = SurfaceModel.fit([(RoadClass.TERTIARY, CTX, Surface.ASPHALT, 3.0), (RoadClass.TERTIARY, CTX, Surface.DIRT, 3.0)])
    a = [m.predict(RoadClass.TERTIARY, CTX, k) for k in range(100)]
    b = [SurfaceModel.from_json(json.dumps(m.to_json())).predict(RoadClass.TERTIARY, CTX, k) for k in range(100)]
    assert a == b
    assert len({s for s, _ in a}) > 1


# ---------------------------------------------------------------------------
# Serialisation and report
# ---------------------------------------------------------------------------
def _fitted_model() -> SurfaceModel:
    samples = []
    for i in range(60):
        cls = [RoadClass.PRIMARY, RoadClass.TRACK, RoadClass.RESIDENTIAL][i % 3]
        ctx = RoadContext(i % 4, i % 5, i % 3)
        surf = [Surface.ASPHALT, Surface.DIRT, Surface.GRAVEL, Surface.CONCRETE][i % 4]
        samples.append((cls, ctx, surf, 0.25 + (i % 7) * 0.5))
    return SurfaceModel.fit(samples)


def test_json_round_trip():
    m = _fitted_model()
    blob = json.dumps(m.to_json(), sort_keys=True)
    m2 = SurfaceModel.from_json(blob)
    assert json.dumps(m2.to_json(), sort_keys=True) == blob
    for cls in RoadClass:
        for d in range(4):
            for e in range(5):
                for s in range(3):
                    ctx = RoadContext(d, e, s)
                    p1, d1 = m.distribution_array(cls, ctx)
                    p2, d2 = m2.distribution_array(cls, ctx)
                    np.testing.assert_array_equal(p1, p2)
                    assert d1 == d2


def test_to_json_is_sorted_sparse_and_versioned():
    data = _fitted_model().to_json()
    keys = [tuple(c["key"]) for c in data["cells"]]
    assert keys == sorted(keys)
    assert all(all(km != 0 for km in c["km"].values()) for c in data["cells"])
    assert data["version"] == S.MODEL_VERSION
    with pytest.raises(ValueError):
        SurfaceModel.from_json({**data, "version": 99})


def test_report():
    m = _fitted_model()
    rep = m.report()
    assert set(rep["classes"]) == {"PRIMARY", "TRACK", "RESIDENTIAL"}
    total = sum(c["fitted_km"] for c in rep["classes"].values())
    assert rep["fitted_km"] == pytest.approx(total, abs=1e-6)
    for row in rep["classes"].values():
        assert sum(row["surface_pct"].values()) == pytest.approx(100.0, abs=0.05)
        assert row["supported_cells"]["class"] == 1
        assert set(row["supported_cells"]) == {"class", "density", "density_elev", "density_elev_slope"}
    assert rep["min_support_km"] == S.MIN_SUPPORT_KM and rep["alpha_km"] == S.ALPHA
    assert json.dumps(rep) == json.dumps(_fitted_model().report())


# ---------------------------------------------------------------------------
# Chains
# ---------------------------------------------------------------------------
def test_chains_join_named_ways_at_shared_endpoints():
    roads = [
        make_road(30, RoadClass.PRIMARY, [1, 2, 3], name="Araniko Highway", ref="NH03"),
        make_road(10, RoadClass.PRIMARY, [3, 4], name="Araniko Highway", ref="NH03"),
        make_road(20, RoadClass.PRIMARY, [4, 5, 6], name="Araniko Highway", ref="NH03"),
        make_road(40, RoadClass.PRIMARY, [6, 7], name="Other Road"),  # different name
        make_road(50, RoadClass.SECONDARY, [7, 8], name="Other Road"),  # different class
        make_road(60, RoadClass.PRIMARY, [9, 2], name="Araniko Highway", ref="NH03"),  # touches an interior node only
    ]
    assert road_chains(roads) == [10, 10, 10, 40, 50, 60]


def test_chains_named_ways_join_through_junctions():
    roads = [make_road(i, RoadClass.TERTIARY, [100, i], name="Ring Road") for i in (5, 3, 9)]
    assert road_chains(roads) == [3, 3, 3]


def test_chains_unnamed_join_only_where_two_meet():
    two = [make_road(7, RoadClass.RESIDENTIAL, [1, 2]), make_road(4, RoadClass.RESIDENTIAL, [2, 3])]
    assert road_chains(two) == [4, 4]
    three = two + [make_road(2, RoadClass.RESIDENTIAL, [2, 9])]
    assert road_chains(three) == [7, 4, 2]
    # a different-class way at the node does not break the unnamed chain
    side = two + [make_road(1, RoadClass.SERVICE, [2, 5])]
    assert road_chains(side) == [4, 4, 1]


def test_chains_ref_only_counts_as_named():
    roads = [make_road(i, RoadClass.UNCLASSIFIED, [50, 1000 + i], ref="01DR001") for i in (8, 6, 7)]
    assert road_chains(roads) == [6, 6, 6]


def test_chains_loop_and_empty_nodes():
    loop = make_road(5, RoadClass.RESIDENTIAL, [1, 2, 3, 1])
    tail = make_road(3, RoadClass.RESIDENTIAL, [1, 4])
    empty = RoadFeature(osm_id=1, cls=RoadClass.RESIDENTIAL, lonlat=np.zeros((0, 2)),
                        node_ids=np.zeros(0, dtype=np.int64))
    # the loop's two ends plus the tail make three ends at node 1: an unnamed junction
    assert road_chains([loop, tail, empty]) == [5, 3, 1]
    assert road_chains([]) == []


def test_chains_are_order_independent():
    rng = np.random.default_rng(3)
    roads = []
    for i in range(300):
        a, b = (int(x) for x in rng.integers(0, 120, 2))
        cls = [RoadClass.RESIDENTIAL, RoadClass.TRACK][int(rng.integers(0, 2))]
        name = ["", "", "Main St"][int(rng.integers(0, 3))]
        roads.append(make_road(1000 + i, cls, [a, 500 + i, b], name=name))
    keys = dict(zip((r.osm_id for r in roads), road_chains(roads)))
    perm = [roads[i] for i in rng.permutation(len(roads))]
    assert dict(zip((r.osm_id for r in perm), road_chains(perm))) == keys
    assert all(keys[k] <= k for k in keys)


# ---------------------------------------------------------------------------
# assign_surfaces
# ---------------------------------------------------------------------------
def test_assign_sources_tagged_derived_and_fallbacks():
    roads = [
        make_road(1, RoadClass.RESIDENTIAL, [1, 2], raw="Blacktopped"),
        make_road(2, RoadClass.TRACK, [3, 4], tracktype=2),
        make_road(3, RoadClass.TRACK, [5, 6], smoothness="very_horrible"),
        make_road(4, RoadClass.TRACK, [7, 8], tracktype=4, smoothness="excellent"),  # tracktype first
        make_road(5, RoadClass.TRACK, [9, 10], raw="G", tracktype=1),  # junk surface: derived instead
        make_road(6, RoadClass.STEPS, [11, 12], raw="3.5"),  # junk, nothing else: prior only
    ]
    stats = assign_surfaces(roads, [CTX] * len(roads))
    got = [(r.surface, r.surface_source) for r in roads]
    assert got[:5] == [
        (Surface.ASPHALT, SurfaceSource.TAGGED),
        (Surface.GRAVEL, SurfaceSource.DERIVED),
        (Surface.MUD, SurfaceSource.DERIVED),
        (Surface.DIRT, SurfaceSource.DERIVED),
        (Surface.CONCRETE, SurfaceSource.DERIVED),
    ]
    assert got[5][1] == SurfaceSource.DEFAULT and got[5][0] in HAND_PRIORS[RoadClass.STEPS]
    assert roads[0].surface_raw == "Blacktopped" and roads[4].surface_raw == "G"
    assert stats["surface_raw_unrecognised"]["roads"] == 2
    assert dict(T.unknown_values["surface"]) == {"G": 1, "3.5": 1}


def test_assign_inferred_vs_default():
    tagged = [make_road(100 + i, RoadClass.UNCLASSIFIED, [1000 + 2 * i, 1001 + 2 * i], km=1.0, raw="gravel")
              for i in range(5)]
    untagged = [make_road(1, RoadClass.UNCLASSIFIED, [1, 2]), make_road(2, RoadClass.PATH, [3, 4])]
    roads = tagged + untagged
    stats = assign_surfaces(roads, [CTX] * len(roads))
    assert untagged[0].surface == Surface.GRAVEL and untagged[0].surface_source == SurfaceSource.INFERRED
    assert untagged[1].surface_source == SurfaceSource.DEFAULT
    assert stats["by_class"]["UNCLASSIFIED"]["pct_by_source"]["TAGGED"] == pytest.approx(100 * 5 / 5.5, abs=0.01)
    assert stats["model"]["classes"]["UNCLASSIFIED"]["fitted_km"] == pytest.approx(5.0, abs=0.01)


def test_assign_uses_given_model():
    model = SurfaceModel.fit([(RoadClass.SERVICE, CTX, Surface.COBBLE, 10.0)])
    roads = [make_road(1, RoadClass.SERVICE, [1, 2]), make_road(2, RoadClass.SERVICE, [3, 4], raw="asphalt")]
    stats = assign_surfaces(roads, [CTX, CTX], model)
    assert roads[0].surface == Surface.COBBLE and roads[0].surface_source == SurfaceSource.INFERRED
    assert stats["model"]["classes"]["SERVICE"]["surface_pct"] == {"COBBLE": 100.0}


def test_inferred_metal_and_wood_only_on_bridges():
    """Tagged METAL/WOOD come from footbridges; inference must not spread them onto ordinary footways."""
    model = SurfaceModel.fit([(RoadClass.FOOTWAY, CTX, Surface.METAL, 5.0), (RoadClass.FOOTWAY, CTX, Surface.WOOD, 3.0),
                              (RoadClass.FOOTWAY, CTX, Surface.CONCRETE, 1.0)])
    plain = [make_road(10 + i, RoadClass.FOOTWAY, [100 + 2 * i, 101 + 2 * i]) for i in range(30)]
    bridges = [make_road(200 + i, RoadClass.FOOTWAY, [300 + 2 * i, 301 + 2 * i]) for i in range(30)]
    for b in bridges:
        b.bridge = True
    assign_surfaces(plain + bridges, [CTX] * 60, model)
    assert all(r.surface not in S.BRIDGE_ONLY_SURFACES for r in plain)
    assert all(r.surface_source == SurfaceSource.INFERRED for r in plain)
    assert {r.surface for r in bridges} & set(S.BRIDGE_ONLY_SURFACES)
    # only bridge-only evidence: falls back to the class prior without them
    only_metal = SurfaceModel.fit([(RoadClass.FOOTWAY, CTX, Surface.METAL, 50.0)])
    r = make_road(1, RoadClass.FOOTWAY, [1, 2])
    assign_surfaces([r], [CTX], only_metal)
    assert r.surface in HAND_PRIORS[RoadClass.FOOTWAY] and r.surface != Surface.METAL


def test_assign_chain_consistency_across_contexts():
    # training data: town tertiary roads are asphalt-ish, rural ones dirt-ish, neither above 0.6
    samples = []
    for i in range(40):
        samples.append((RoadClass.TERTIARY, RoadContext(2, 1, 0), Surface.ASPHALT if i % 2 else Surface.GRAVEL, 1.0))
        samples.append((RoadClass.TERTIARY, RoadContext(0, 2, 2), Surface.DIRT if i % 2 else Surface.GRAVEL, 1.0))
    model = SurfaceModel.fit(samples)
    for name in ["Siddhartha Rajmarg", ""]:
        nodes = list(range(1, 13))
        roads = [make_road(500 + k, RoadClass.TERTIARY, [nodes[k], nodes[k + 1]], name=name) for k in range(11)]
        ctxs = [RoadContext(2, 1, 0) if k < 6 else RoadContext(0, 2, 2) for k in range(11)]
        assign_surfaces(roads, ctxs, model)
        assert len({r.surface for r in roads}) == 1
        assert all(r.surface_source == SurfaceSource.INFERRED for r in roads)


def test_assign_chain_follows_observed_member():
    # a 3 km tagged stretch and an untagged bridge way of the same named road
    roads = [make_road(1, RoadClass.SECONDARY, [1, 2], km=3.0, raw="concrete", name="Bridge Rd"),
             make_road(2, RoadClass.SECONDARY, [2, 3], km=0.1, name="Bridge Rd")]
    assign_surfaces(roads, [CTX, CTX], SurfaceModel())
    assert roads[1].surface == Surface.CONCRETE and roads[1].surface_source == SurfaceSource.INFERRED


def test_assign_sampling_spreads_over_independent_chains():
    # model says 50/50 asphalt/dirt for residential: independent chains sample both
    model = SurfaceModel.fit([(RoadClass.RESIDENTIAL, CTX, Surface.ASPHALT, 50.0),
                              (RoadClass.RESIDENTIAL, CTX, Surface.DIRT, 50.0)])
    roads = [make_road(10 + i, RoadClass.RESIDENTIAL, [2 * i, 2 * i + 1]) for i in range(400)]
    assign_surfaces(roads, [CTX] * len(roads), model)
    freq = Counter(r.surface for r in roads)
    assert set(freq) == {Surface.ASPHALT, Surface.DIRT}
    assert 0.4 < freq[Surface.ASPHALT] / 400 < 0.6


def test_assign_is_deterministic_and_order_independent():
    rng = np.random.default_rng(11)
    roads, ctxs = [], []
    raws = [None, None, None, "asphalt", "unpaved", "gravel", "paving_stones", "G"]
    for i in range(400):
        a, b = (int(x) for x in rng.integers(0, 150, 2))
        cls = [RoadClass.RESIDENTIAL, RoadClass.UNCLASSIFIED, RoadClass.TRACK][int(rng.integers(0, 3))]
        roads.append(make_road(10_000 + i, cls, [a, 900 + i, b], km=float(rng.uniform(0.05, 2.0)),
                               raw=raws[int(rng.integers(0, len(raws)))], name=["", "X"][int(rng.integers(0, 2))]))
        ctxs.append(RoadContext(int(rng.integers(0, 4)), int(rng.integers(0, 5)), int(rng.integers(0, 3))))
    r1 = copy.deepcopy(roads)
    s1 = assign_surfaces(r1, ctxs)
    r2 = copy.deepcopy(roads)
    s2 = assign_surfaces(r2, ctxs)
    assert json.dumps(s1) == json.dumps(s2)
    out1 = {r.osm_id: (r.surface, r.surface_source) for r in r1}
    assert out1 == {r.osm_id: (r.surface, r.surface_source) for r in r2}
    perm = rng.permutation(len(roads))
    r3 = [copy.deepcopy(roads[i]) for i in perm]
    assign_surfaces(r3, [ctxs[i] for i in perm])
    assert {r.osm_id: (r.surface, r.surface_source) for r in r3} == out1
    assert all(r.surface != Surface.UNKNOWN for r in r1)


def test_assign_stats_shape():
    roads = [make_road(1, RoadClass.PRIMARY, [1, 2], km=2.0, raw="asphalt"),
             make_road(2, RoadClass.PRIMARY, [5, 6], km=1.0, smoothness="bad"),
             make_road(3, RoadClass.PATH, [7, 8], km=1.0)]
    stats = assign_surfaces(roads, [CTX] * 3)
    assert stats["roads"] == 3 and stats["chains"] == 3
    assert stats["total_km"] == pytest.approx(sum(polyline_length_m(r.lonlat) for r in roads) / 1000, abs=1e-3)
    assert stats["pct_by_source"] == pytest.approx({"TAGGED": 50.0, "DERIVED": 25.0, "INFERRED": 0.0,
                                                   "DEFAULT": 25.0}, abs=0.05)
    assert list(stats["by_class"]) == ["PRIMARY", "PATH"]
    assert sum(stats["surface_pct"].values()) == pytest.approx(100.0, abs=0.05)
    assert "classes" in stats["model"]


def test_assign_rejects_mismatched_contexts():
    with pytest.raises(ValueError):
        assign_surfaces([make_road(1, RoadClass.PATH, [1, 2])], [])


def test_assign_handles_degenerate_geometry():
    roads = [RoadFeature(osm_id=1, cls=RoadClass.TRACK, lonlat=np.array([[85.0, 27.0]]),
                         node_ids=np.array([1], dtype=np.int64)),
             RoadFeature(osm_id=2, cls=RoadClass.TRACK, lonlat=np.zeros((0, 2)), node_ids=np.zeros(0, np.int64))]
    stats = assign_surfaces(roads, [CTX, CTX])
    assert all(r.surface != Surface.UNKNOWN for r in roads)
    assert stats["total_km"] == 0.0
