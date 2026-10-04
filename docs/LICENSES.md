# Licences, attribution and legal review items

> Status 2026-10-04. Sources were checked on that date (URLs inline). **This is engineering due diligence, not legal advice.** Items marked ⚖️ need review by a lawyer before any public release.
> Machine-readable source list: `pipeline/config/sources.yaml`. CI should fail when a new data source or asset has no entry here (TODO M1).

## 1. Data sources

### 1.1 OpenStreetMap (ODbL 1.0)

* **Licence:** [Open Database License 1.0](https://opendatacommons.org/licenses/odbl/1-0/). Commercial use is allowed ([OSMF Licence FAQ](https://osmfoundation.org/wiki/Licence/Licence_and_Legal_FAQ)).
* **What we make from it:**
  * *Derivative Databases*: our region packs (`.ghpk`), search indexes (`.ghsi`) and routing graphs (`.ghrg`). These are OSM data that has been extracted, reprojected, clipped, re-encoded, and enriched with inferred attributes.
  * *Produced Works*: the rendered game world, the in-game map and screenshots.
* **Obligations, and how we meet them:**

| ODbL clause | Requirement | Our implementation |
|---|---|---|
| 4.3 Notice on Produced Works | A notice that makes users aware the content comes from OSM under ODbL | Startup splash credit "Map data © OpenStreetMap contributors", legible for ≥ 5 s or until dismissed. A persistent corner credit **"© OpenStreetMap"** on the full map and minimap, tappable through to openstreetmap.org/copyright, and allowed to collapse to an (i) after 5 s, as the interactive-maps section of the [OSMF Attribution Guidelines](https://osmfoundation.org/wiki/Licence/Attribution_Guidelines) permits. A **Data licences** screen in Settings and Credits. The guidelines' "Computer games and simulations" section explicitly allows splash, in-game, credits or menu placement, provided detailed information is available somewhere suitable. |
| 4.4 Share-alike | A Derivative Database that we Publicly Use (including through a Produced Work made from it, 4.4c) must be offered under ODbL | Our OSM-derived databases are licensed under ODbL. |
| 4.6 Offer of the database or the method | Recipients of the Produced Work must be offered, free of charge, either the entire Derivative Database or the method used to make it | **Recommended:** publish the pipeline source (`/pipeline`) and the pinned OSM snapshot ID (`SOURCES.lock.json`) at a public URL, and optionally the built packs too. The Data licences screen links to them. ⚖️ |
| 4.2 / 4.7 Notices and DRM | When conveying a DB, include the licence notice in it; no technical measures that restrict ODbL rights unless an unrestricted copy is also available | Each region manifest carries the ODbL notice. Because app-store distribution is effectively DRM'd, we make an unrestricted copy (or the method) available publicly (4.7b). ⚖️ |
| 4.4d Incompatible content | No content incompatible with ODbL may be added into the Derivative Database | **Layer separation (ADR-013):** DEM heights and WorldCover classes are stored in *separate chunks* (`HGHT`, `BIOM`) and never written into OSM feature records. Hand-curated content (fun facts, curated gems) lives in a separate database keyed by our own IDs. This follows the [Collective Database](https://osmfoundation.org/wiki/Licence/Community_Guidelines/Collective_Database_Guideline_Guideline) and [Horizontal Map Layers](https://osmfoundation.org/wiki/Licence/Community_Guidelines/Horizontal_Map_Layers_-_Guideline) guidelines. ⚖️ |

* **Inferred attributes.** These include road surface, building levels and trail difficulty, and they are derived partly from DEM and WorldCover. That puts them outside the [Trivial Transformations](https://osmfoundation.org/wiki/Licence/Community_Guidelines/Trivial_Transformations_-_Guideline) safe harbour, which requires that no other data source be involved. They count as alterations, and the published method (4.6b) must describe them. The pipeline source code does exactly that. ⚖️
* **Personal data.** Our OSM mirror (geo2day/OSMToday) ships full object metadata: user names, uids and changesets. Geofabrik's public extract strips these fields. **We drop all metadata at ingest.** The pipeline reads only tags, ids and geometry, never writes metadata to any output, and the coverage scan reads only timestamps. A `strip_metadata` step in `fetch.py` is planned so the raw file at rest is clean too (TODO, tracked in PROGRESS).
* **Mirror provenance.** geo2day.com has no OSMF affiliation and no published terms. Geofabrik stays the primary source; the mirror is a fallback (see `sources.yaml`). Both are verified by MD5.
* **Do not** use `tile.openstreetmap.org` from the shipped game. The QA viewer uses it only for interactive developer viewing, under the [Tile Usage Policy](https://operations.osmfoundation.org/policies/tiles/): attribution, browser caching, no bulk or automated prefetch, no offline use. Automated screenshot tests run with the basemap disabled.

### 1.2 Copernicus DEM GLO-30

* **Licence:** "Licence for Copernicus DEM instance COP-DEM-GLO-30-F Global 30m Full, Free & Open" ([PDF](https://documentation.dataspace.copernicus.eu/APIs/SentinelHub/Data/DEM/resources/license/License-COPDEM-30.pdf)). It grants worldwide, unlimited-time, free rights of reproduction, distribution, communication to the public, adaptation and combination, to "any natural or legal person". Commercial use is **inferred**: the licence does not use the word. ⚖️
* **Required notice (Art. 6b, for modified data). We use this verbatim:**
  > produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved
* **Required liability sentence (Art. 6c), placed in the EULA / legal notice:**
  > The organisations in charge of the Copernicus programme by law or by delegation do not incur any liability for any use of the Copernicus WorldDEM-30.
* No endorsement may be implied, and no Copernicus or ESA logos may be used. Art. 6e: if we let others redistribute terrain data (for example in a published database), we must bind them to the same obligations. ⚖️
* Access: AWS Open Data `copernicus-dem-30m`. The registry asks for the citation "Copernicus Digital Elevation Model (DEM) was accessed on DATE from https://registry.opendata.aws/copernicus-dem."

### 1.3 ESA WorldCover 10 m 2021 v200

* **Licence:** [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/legalcode.en) ([ESA WorldCover data access](https://esa-worldcover.org/en/data-access)).
* **Required attribution. We use this verbatim, plus the modification note:**
  > © ESA WorldCover project 2021 / Contains modified Copernicus Sentinel data (2021) processed by ESA WorldCover consortium. Licensed under CC BY 4.0 (creativecommons.org/licenses/by/4.0/). Resampled and stylised for this game.
* Citation: Zanaga, D. et al. (2022). ESA WorldCover 10 m 2021 v200. doi:10.5281/zenodo.7254221.
* CC BY 4.0 §2(a)(5)(C) forbids technological measures that restrict recipients' rights. App-store distribution is standard practice, and the derived biome data is a stylised adaptation. ⚖️ (low risk)

### 1.4 HydroSHEDS / HydroRIVERS: **not used** (changed from the brief)

The brief lists HydroRIVERS as optional. HydroSHEDS v1 is free for commercial use only under the [WWF licence agreement](https://data.hydrosheds.org/file/technical-documentation/HydroSHEDS_TechDoc_v1_4.pdf). That agreement includes an EULA flow-down, an anti-decompile clause, an indemnity and termination at WWF's discretion, all of which conflict with publishing our database under ODbL. HydroSHEDS v2 (CC BY 4.0) covers only the Americas so far. **Decision:** rivers come from OSM waterways (49 966 streams and 5 489 rivers in Nepal), plus our own flow accumulation on the DEM if needed. HydroRIVERS may be used only for internal QA comparison, which is not public use.

### 1.5 NASADEM / SRTM (fallback only)

These are NASA/USGS data, CC0 or public domain ([Earthdata guidance](https://www.earthdata.nasa.gov/engage/open-data-services-software-policies/data-use-guidance)). They are used only for void-filling. Courtesy credit: "NASADEM_HGT v001 courtesy of NASA LP DAAC, doi:10.5067/MEASURES/NASADEM/NASADEM_HGT.001". If they are mixed into Copernicus-derived terrain, the Copernicus 6b notice still applies.

## 2. Fonts (SIL Open Font License 1.1)

| Font | Use | Licence | Copyright line to ship (must match the binary's source) |
|---|---|---|---|
| **Baloo 2** (Ek Type) | Display, Latin + Devanagari | OFL 1.1, no Reserved Font Name | Google Fonts build: "Copyright 2019 The Baloo 2 Project Authors (https://github.com/EkType/Baloo2)" |
| **Mukta** (Ek Type) | Body, Latin + Devanagari | OFL 1.1, no RFN | Google Fonts build: "Copyright (c) 2014, Girish Dalvi, Ek Type" |
| Noto Sans Devanagari (optional fallback) | Fallback | OFL 1.1 | "Copyright 2022 The Noto Project Authors" |

OFL allows bundling in commercial games and mobile apps ([OFL FAQ 1.4](https://openfontlicense.org/ofl-faq/)). Text rendered with these fonts is not covered by the OFL. Our obligations are: ship `OFL.txt` and the copyright lines (on the Data licences screen and under `StreamingAssets/licenses/`), never sell the fonts on their own (for example as a DLC item), and keep any modified or subsetted font under the OFL.

## 3. Engine and libraries

| Component | Licence | Notes |
|---|---|---|
| Unity 6.3 LTS Editor / runtime | Unity Terms of Service | **Unity Personal:** free while revenue plus funding over the last 12 months is ≤ US$200 000. Above that, **Unity Pro** is needed, at $2 310 per seat per year since 2026-01-12. The Runtime Fee was cancelled on 2024-09-12. Personal can disable the splash screen in Unity 6. ([pricing updates](https://unity.com/en/products/pricing-updates)) |
| Unity packages (URP, Addressables, Input System, Burst, Collections, Mathematics, Localization) | Unity Companion License | Use with the Unity engine only |
| pyosmium / libosmium | BSD-2 / BSL-1.0 | Pipeline only (not shipped) |
| GDAL/rasterio, PROJ/pyproj, shapely/GEOS, numpy, scipy, PyYAML | MIT / BSD / LGPL (GEOS, linked dynamically) | Pipeline only |
| Leaflet 1.9.4 | BSD-2 | QA viewer (developer tool only) |
| Any Asset Store asset | Unity Asset Store EULA | Record each one here before use |

## 4. Content and trademarks

* **Vehicles, shops and brands:** all designs are generic, with no real manufacturer names or logos (brief §5.1). OSM business names are not painted on signs and are not searchable (ADR-010).
* **Landmarks:** buildings and monuments are depicted as stylised cartoons. A few sites have photography or depiction sensitivities, and these are listed for the cultural consultant. ⚖️ (low)
* **Nepal's flag** is rendered to the constitutional construction (Schedule 1 of the Constitution of Nepal).
* ⚖️ **Border depiction.** Nepal's official map since 20 May 2020 includes Limpiyadhura–Lipulekh–Kalapani (147 516 km²), which India disputes. How the in-game map draws this area affects store approval and reputation in Nepal and India. **This is your decision, with legal and market input, before any map UI ships publicly.** We will also check which claim OSM relation 184633 follows.

## 5. Attribution text (Data licences screen, EN; NE translation pending review)

```
Map data
  Contains information from OpenStreetMap (openstreetmap.org/copyright), which is made
  available here under the Open Database License (ODbL) (opendatacommons.org/licenses/odbl/1-0/).
  The map data in this game was derived from OpenStreetMap by the Ghumante data pipeline.
  The derived database and the method used to create it are available at <PUBLIC URL TBD>.

Elevation
  Produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space
  GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved.

Land cover
  © ESA WorldCover project 2021 / Contains modified Copernicus Sentinel data (2021) processed by
  ESA WorldCover consortium. Licensed under CC BY 4.0. Resampled and stylised for this game.

Fonts
  Baloo 2 and Mukta by Ek Type, licensed under the SIL Open Font License 1.1.
```

## 6. ⚖️ Lawyer review checklist

1. Is OSM-derived data inside the app bundle "Conveyed" as a Derivative Database (4.2 notices, 4.4 licence, 4.7 DRM), or is it only a component of a Produced Work? Is our plan (publish the method and the DB, notices in the manifests) sufficient?
2. Is layer separation (OSM features vs DEM/WorldCover chunks vs curated content) enough to keep the Copernicus "all rights reserved" terms and our proprietary curated data out of ODbL share-alike?
3. Copernicus DEM: is commercial use confirmed? Do we need the Art. 6e flow-down in our EULA?
4. The disputed-border depiction and app-store implications (India, Nepal, China).
5. Privacy: the full EULA and privacy policy, the children's-audience obligations (COPPA, GDPR-K, Play Families), and a privacy-safe crash reporter.
6. Trademarks: landmark names in marketing; "Made with Unity" splash choices; any lookalike issues with vehicle designs.
