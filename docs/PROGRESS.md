# Ghumante: progress log

> The running log: what's done, what's next, known issues, and decisions with the reasons for them. The newest entry is at the top.

## Current status (2026-10-04)

**Milestone:** M0 Foundations, in progress (see the [ROADMAP](ROADMAP.md#m0-foundations-planning-and-pipeline-proof)).

| Area | State |
|---|---|
| Planning docs | ARCHITECTURE, ROADMAP, LICENSES, DATA_FORMATS, ASSET_MANIFEST written; open questions in ARCHITECTURE §14 |
| Source data | Nepal OSM (2026-10-02), Copernicus GLO-30 tiles, WorldCover N27E084 downloaded and verified |
| Tag coverage | [report](reports/tag_coverage.md) + [findings](reports/TAG_COVERAGE_FINDINGS.md) |
| Landmarks | [60/66 resolved](reports/landmarks.md) against OSM |
| Pipeline | *in progress* (filled in below at the end of the session) |
| C# core | *in progress* |
| Unity project | *in progress* (can't be opened or run in this cloud environment, which has no Unity licence) |
| CI | *in progress* |

---

## 2026-10-04: session 1 (M0 kickoff)

### Done
* Read the brief and wrote down the pushback: [ARCHITECTURE §2](ARCHITECTURE.md#2-where-this-brief-needs-pushback), 15 items.
* Ran a parallel research pass with adversarial fact-checking over engine, delivery, licensing, performance, scale precedents and data tooling. Results are in [ARCHITECTURE Appendix A](ARCHITECTURE.md#appendix-a-verified-platform-facts-research-pass-2026-10-04) and [reports/research_2026-10-04.md](reports/research_2026-10-04.md).
* Downloaded the sources. **Geofabrik is unreachable from this cloud environment** (TLS connection reset at the proxy, consistent with Geofabrik blocking cloud egress), so the same daily Nepal extract came from the OSMToday/geo2day mirror, MD5-verified. `sources.yaml` lists Geofabrik first and the mirror as fallback.
* **Tag coverage report.** It scans 9.5 M tagged ways in about 5 min. Headline numbers: 85% of road length has no surface tag, 99.6% of buildings have no levels, sac_scale is on 2% of trails, and name:ne is on 14–29% of villages. The consequences became ADR-005 (inference-first).
* **Landmark verification.** 60 of the 66 brief landmarks resolved to OSM objects with the expected tags. Weak or missing: Gorkha Durbar, the Bhote Koshi bungee, the Kalinchowk temple, the Pokhara zipline, and the Muktinath temple (it matched "Muktinath Pond", which needs a manual check).
* Wrote the data contract: `model.py` enums (exported to `shared/enums.json`), [DATA_FORMATS.md](DATA_FORMATS.md) (GHT1 tiles, GHPK packs, GHSI search, GHRG routing), and the NPL-TM84 projection plus quadtree with tests.

### Decisions (see ARCHITECTURE §13)
* **ADR-004 / ADR-016, decided by the product owner: Option 3.** Towns at 1:1, country between them compressed (~1:6, tuned at gate G1). **The full-screen Nepal map is drawn at true 1:1 geography.** Nothing changes for M0/M1, which already use the identity warp.
* ADR-001 **Unity 6.3 LTS**, not 6.0 LTS. 6.0 support ends this month; 6.3 is supported to Dec 2027.
* ADR-007 all UI in UI Toolkit, for Devanagari shaping. A device spike is the first task in M1.
* ADR-013 / 014 / 015: layer separation for ODbL; no HydroSHEDS; base install ≤ 150 MB with the CDN as the baseline (ODR is deprecated in iOS 27).

### Known issues / risks
* Unity can't run in this cloud environment, so engine code is compile-checked against reference assemblies only. The first real Unity open happens on your machine or in CI once secrets are added (`docs/CI_SECRETS.md`).
* The mirror's OSM file contains contributor metadata. Our outputs never include it, but stripping it at ingest is still TODO.
* The scale warp (G1 gate) is the biggest technical risk after M1.

### Next
* See the end-of-session summary below.
