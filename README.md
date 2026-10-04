# Ghumante (घुमन्ते)

*"Wanderer": a bright, chunky, cartoon open-world explorer of the **real** Nepal, for iOS and Android.*

Ride a motorbike from Thamel to Boudhanath along real streets. Bus up the Prithvi Highway to Pokhara, paraglide off Sarangkot, or trek to Everest Base Camp. Everything is where it really is, built from OpenStreetMap, Copernicus elevation and ESA land-cover data, and drawn as a joyful, saturated cartoon.

> **Status:** M0, Foundations. See [docs/PROGRESS.md](docs/PROGRESS.md).

## Repository map

| Path | What |
|---|---|
| [`docs/`](docs/) | [Architecture](docs/ARCHITECTURE.md) · [Roadmap](docs/ROADMAP.md) · [Progress](docs/PROGRESS.md) · [Data formats](docs/DATA_FORMATS.md) · [Licences](docs/LICENSES.md) · [Asset manifest](docs/ASSET_MANIFEST.md) · [Reports](docs/reports/) |
| [`pipeline/`](pipeline/) | Offline Python data pipeline: fetch → extract → infer → tile → pack → search index → routing graph |
| [`game/`](game/) | Unity 6.3 LTS (URP) project |
| `game/Assets/Ghumante/Core/` | Engine-agnostic C# core (data readers, search, routing, save). No `UnityEngine` references |
| [`core-tests/`](core-tests/) | `dotnet test` project for the core, cross-checked against golden files from the Python pipeline |
| [`shared/`](shared/) | Cross-language contracts (`enums.json`, golden files) |
| [`tools/qa-viewer/`](tools/qa-viewer/) | Web viewer that overlays generated tiles on OpenStreetMap |
| `.github/workflows/` | CI: pipeline tests, core tests, localisation check, Unity Android/iOS builds, nightly data refresh |

## Quick start (data pipeline)

```bash
cd pipeline
pip install -e ".[dev]"
python fetch.py --region kathmandu_valley      # downloads OSM + DEM + land cover, writes SOURCES.lock.json
python build.py --region kathmandu_valley --qa # builds tiles, search index, routing graph, QA export
python -m pytest -q                            # unit + end-to-end tests
python ../tools/qa-viewer/serve.py             # open the printed URL to inspect the build over OSM
```

Details: [pipeline/README.md](pipeline/README.md).

## Attribution

Map data © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors (ODbL). Elevation: produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved. Land cover: © ESA WorldCover project 2021 / Contains modified Copernicus Sentinel data (2021) processed by ESA WorldCover consortium (CC BY 4.0). Full details in [docs/LICENSES.md](docs/LICENSES.md).
