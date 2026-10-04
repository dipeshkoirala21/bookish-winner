#!/usr/bin/env python3
"""Reference NPL-TM84 values from pyproj for the viewer's JS projection test.

Prints JSON: {"points": [[lon, lat, x_game, z_game], ...]} over a grid that
covers Nepal (80.0-88.25 E, 26.3-30.5 N), so the smoke test can check the
Krueger-series port in app.js against PROJ. Exits 3 when pyproj is missing
(the test then skips this check).
"""

import json
import sys

try:
    from pyproj import Transformer
except ImportError:  # pragma: no cover
    sys.exit(3)

TM84 = "+proj=tmerc +lat_0=0 +lon_0=84 +k=0.9996 +x_0=500000 +y_0=0 +ellps=WGS84 +units=m +no_defs"
fwd = Transformer.from_crs("EPSG:4326", TM84, always_xy=True)

pts = []
for i in range(12):
    lon = 80.0 + i * (88.25 - 80.0) / 11
    for j in range(6):
        lat = 26.3 + j * (30.5 - 26.3) / 5
        e, n = fwd.transform(lon, lat)
        pts.append([lon, lat, e - 100_000.0, n - 2_900_000.0])
pts.append([85.31, 27.715, *(v - o for v, o in zip(fwd.transform(85.31, 27.715), (100_000.0, 2_900_000.0)))])
print(json.dumps({"points": pts}))
