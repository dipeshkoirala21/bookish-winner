# Sample region packs

`kathmandu_core/` is a compact, real-data region committed to the repo so the game's **Explore** works on a fresh checkout, with no need to run the Python pipeline. It covers Swayambhunath → Thamel → Kathmandu Durbar Square → Pashupatinath → Boudhanath (bbox 85.283–85.375 E, 27.690–27.735 N), plus a coarse horizon out to the Langtang/Jugal Himalaya (levels 5–6 only, to keep it about 11 MB).

* `Ghumante → Project Setup` (and `Ghumante → Import Region Pack`) copy it into `game/Assets/StreamingAssets/Regions/`, which is git-ignored.
* Rebuild it with `cd pipeline && python build.py --region kathmandu_core`, then copy the four files here. Refresh it rarely: every rebuild adds about 14 MB to git history.
* For the full valley, run `python build.py --region kathmandu_valley`, then **Ghumante → Import Region Pack**.

Map data © OpenStreetMap contributors (ODbL). Elevation: produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved. Land cover: © ESA WorldCover project 2021 / Contains modified Copernicus Sentinel data (2021) processed by ESA WorldCover consortium (CC BY 4.0). These packs are a Derivative Database under the ODbL; see docs/LICENSES.md.
