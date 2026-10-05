# Content census (2026-10-05)

Evidence for [CONTENT_COVERAGE.md](../../CONTENT_COVERAGE.md): a single pass over the OSM Nepal extract (2026-10-02 snapshot) and the built `kathmandu_valley` / `kathmandu_core` packs.

| File | What |
|---|---|
| `findings.md` | Hand-written findings: the numbers that matter and the surprising zeros |
| `census.md` | Generated tables per category and scope (Nepal extract, valley, pack box, core) |
| `census.py` | The script (pyosmium; about 4 min for the full extract). `python3 docs/reports/content_census/census.py` writes `census.json` / `census.md` to `pipeline/build/content_census/` (override with `GHUMANTE_CENSUS_OUT`). |

"Nepal" in these tables is the whole extract, which includes Indian and Tibetan border strips (see CONTENT_COVERAGE, "Where the numbers come from"). Work item D13 ports this into the pipeline as a per-build coverage report.
