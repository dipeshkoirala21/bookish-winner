# Ghumante (घुमन्ते)

*"Wanderer": a bright, chunky, cartoon open-world explorer of the **real** Nepal, for iOS and Android.*

Ride a motorbike from Thamel to Boudhanath along real streets. Bus up the Prithvi Highway to Pokhara, paraglide off Sarangkot, or trek to Everest Base Camp. Everything is where it really is, built from OpenStreetMap, Copernicus elevation and ESA land-cover data, and drawn as a joyful, saturated cartoon.

> **Status:** M1, Kathmandu Valley vertical slice, in progress (Wave 2 detail pass). See [docs/PROGRESS.md](docs/PROGRESS.md).

## Screenshots

> These are **preview renders**, not captures from the running game. The offline mesh-preview renderer ([`tools/mesh-preview`](tools/mesh-preview/)) draws exactly the meshes the game's own generators build from the Kathmandu sample map. Captures from Unity in Play mode will replace them once they're taken on device.

| | |
|---|---|
| ![A lane in Asan with parked motorbikes](docs/images/asan_lane.jpg) Asan: a narrow old-core lane, widened to be rideable, with parked bikes | ![New Road roundabout with its statue](docs/images/new_road_roundabout.jpg) New Road: roundabout with its statue, traffic and kerbs |
| ![Maitighar Mandala](docs/images/maitighar_mandala.jpg) Maitighar Mandala: the island garden and mandala | ![Shahid Gate](docs/images/shahid_gate.jpg) Shahid Gate (Martyrs' Memorial) on its island |
| ![Driving onto a Bagmati bridge](docs/images/bagmati_bridge_deck.jpg) On a Bagmati bridge: railings on both sides, lamp posts | ![A Bagmati bridge from the bank](docs/images/bagmati_bridge.jpg) The same kind of bridge from the bank: deck, piers, railings |

**Landmarks and people**

![Nyatapola, Bhaktapur](docs/images/nyatapola.jpg)
*Nyatapola, Bhaktapur: five roofs on five plinths, struts under every eave, guardian pairs on the stair (33.7 m, real proportions).*

![Shahid Gate front](docs/images/shahid_gate_front.jpg) ![Dhaka topi](docs/images/topi.jpg)
*Shahid Gate front view; the player's dhaka topi from the front, three-quarter and side.*

**Vehicles of today's Kathmandu streets** (generic, unbranded)

![Vehicles](docs/images/vehicles.jpg)

**Animals and birds**

![Animals](docs/images/animals.jpg)

**Seasons on the valley rim** (January, April, July, October)

![Seasons](docs/images/seasons.jpg)

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
