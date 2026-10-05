# Ghumante data formats

These are the binary formats the offline pipeline (`/pipeline`, Python) writes and the game runtime (`/game`, C#) reads. The Python implementation in `pipeline/ghumante_pipeline/tile_format.py`, `pack.py`, `search_index.py` and `routing.py` is the reference. The C# readers live in `game/Assets/Ghumante/Core/Data/`, and golden-file tests in `core-tests/` keep the two in sync.

**Compatibility rule:** any change to a layout below bumps that format's `version` field. Readers reject versions they do not know. Enum values come from `pipeline/ghumante_pipeline/model.py` and are exported to `shared/enums.json`. They are append-only.

## 0. Conventions

* All multi-byte integers and floats are **little-endian**.
* `u8/u16/u32/u64`: unsigned integers. `i8/i16/i32`: two's-complement signed integers. `f32/f64`: IEEE-754.
* `varint` is unsigned LEB128: 7 bits per byte, least-significant group first, high bit set on every byte except the last, at most 10 bytes.
* `svarint` is a zigzag-encoded signed value (`(n << 1) ^ (n >> 63)`) written as a `varint`.
* `str` is a `varint` byte length followed by that many UTF-8 bytes (NFC-normalised, no BOM, no terminator).
* **Game coordinates** are metres: X east, Z north (Y up in Unity). They are defined in `projection.py`: NPL-TM84 (`+proj=tmerc +lat_0=0 +lon_0=84 +k=0.9996 +x_0=500000 +y_0=0 +ellps=WGS84`), minus the world origin (E 100 000, N 2 900 000), passed through the scale model (identity in M0/M1).
* **Quadtree:** a tile at level `L` has side `S = 2^(20-L)` m. Tile `(L, tx, ty)` covers `[tx·S, (tx+1)·S) × [ty·S, (ty+1)·S)`, with `ty` growing northward.
* **Tile key** (`u64`): `(L << 58) | morton(tx, ty)`. The Morton code puts `tx` bits on the even bit positions and `ty` bits on the odd ones (29 bits each).
* **Local coordinates** inside a tile are integer **centimetres** relative to the tile's south-west corner `(tx·S, ty·S)`. A point may lie outside `[0, S·100]`: context points, building footprints that cross the edge, and so on.

## 1. Tile container (`.ght`): magic `GHT1`

A tile is one self-contained blob holding a sequence of typed chunks.

```
Header (32 bytes)
  0  u8[4]  magic        "GHT1"
  4  u16    version      = 1
  6  u16    flags        bit0: has_detail_content (roads/buildings/...)
  8  u8     level
  9  u8[3]  reserved     = 0
 12  u32    tx
 16  u32    ty
 20  u32    data_version (pipeline PIPELINE_DATA_VERSION)
 24  u16    chunk_count
 26  u16    reserved     = 0
 28  u32    payload_crc32  CRC-32 (IEEE, as zlib.crc32) of every byte after the chunk table

Chunk table: chunk_count entries, 16 bytes each, sorted by fourcc
  0  u8[4]  fourcc
  4  u8     codec        0 = stored, 1 = raw DEFLATE (RFC 1951, no zlib header)
  5  u8[3]  reserved
  8  u32    offset       from start of tile blob to the chunk's (possibly compressed) bytes
 12  u32    stored_size  bytes in the blob
(the uncompressed size is the first u32 of every chunk payload: see below)

Chunk payload (after decompression): u32 raw_size, then chunk-specific bytes
```

A reader **must skip unknown fourccs**, which lets new chunk types be added without a version bump. Chunks a tile does not need are omitted. Horizon tiles only carry `HGHT` and `BIOM`.

The writer must be **deterministic**: identical inputs produce byte-identical tiles. That means no timestamps, stable sort orders everywhere, and a fixed DEFLATE level of 9. The CI seam/determinism tests depend on this.

### 1.1 `HGHT`: heightmap

```
u16  n              grid size, 2^k + 1 (129 by default)
u16  reserved = 0
f32  h_min_m        fixed: -100.0
f32  h_step_m       fixed: 0.15
u16[n*n] residuals  row-major, row j = 0 is the south edge, column i = 0 the west edge
```

Sample `(j, i)` sits at game `(x0 + i·S/(n−1), z0 + j·S/(n−1)`). Edge rows and columns lie exactly on the tile border and **must be bit-identical** to the neighbour's shared edge. This is the no-cracks invariant that `tests/test_seams.py` checks.

Quantisation is global, with no per-tile ranges: `q = clamp(round((h − h_min)/h_step), 0, 65535)`, giving a range of −100 m to 9 730.25 m at 15 cm steps. Because it is global, a parent tile's samples and the child samples at the same position decode to comparable values.

Prediction filter: `pred = q[j][i−1]` when `i > 0`, else `q[j−1][0]` when `j > 0`, else `0`. Each value is stored as `residual = (q − pred) mod 65536`. Decoding reverses this in the same scan order.

Heights are the terrain surface **before** runtime road and building flattening. The runtime conforms the terrain to roads in the near field (ARCHITECTURE.md §6.3).

### 1.2 `BIOM`: biome map

```
u16  n              2^k + 1 (65 by default)
u16  reserved
u8[n*n] biome       row-major like HGHT, values = model.Biome
```

Samples are vertex-aligned, so edges are shared exactly like `HGHT`.

### 1.3 `NAME`: name records (tile-local)

```
varint count
count × { str default, str en, str ne }
```

Other chunks refer to a name with `name_ref`: `0` = no name, `k` = record `k−1`.

### 1.4 `ROAD`: roads and trails

```
varint count
count × Road:
  varint  osm_way_id
  u8      road_class        model.RoadClass
  u8      surface           model.Surface
  u8      surface_source    model.SurfaceSource
  u8      flags             model.RoadFlags (ONEWAY, BRIDGE, TUNNEL, HAS_PREV_CTX, HAS_NEXT_CTX, LINK, FORD, SAC_INFERRED)
  u8      lanes             0 = unknown
  u8      sac_scale         model.SacScale (trails), 0 otherwise
  u8      trail_visibility  0 = unknown, 1..6
  i8      layer
  varint  width_cm          0 = unknown (runtime uses class default)
  u8      access            model.Travel bit mask
  varint  name_ref
  varint  ref_ref           name_ref-style index of a NAME record holding `ref` (e.g. "NH04")
  varint  point_count       >= 2, includes context points
  point_count × { svarint dx_cm, svarint dz_cm }   first point relative to tile origin, then deltas
```

A way is clipped to the tile's square. Each clipped piece stores its in-tile vertices plus the exact intersection points with the tile border. If the piece was cut at its start (or end), the original polyline vertex just outside the tile is stored as a **context point** and the `HAS_PREV_CTX` (or `HAS_NEXT_CTX`) flag is set. Context points are never rendered. They exist so the mesher on each side of a tile edge computes the same tangent at the shared cut point, which makes the meshes meet without seams. After rounding to whole centimetres, a piece has no two equal consecutive points, and a context point never equals its cut point: when the vertex just outside rounds onto the cut (it lies within 0.5 cm of the border), the next original vertex further out that does not is stored instead, which is the first in-tile point of the neighbour's piece. If there is no such vertex the flag is cleared. Pieces are sorted by `(osm_way_id, first point)`.

`ONEWAY` means traffic flows in point order: the writer reverses `oneway=-1` ways.

### 1.5 `LINE`: other linear features (waterways, rail, aerialways, runways, walls)

```
varint count
count × Line:
  varint osm_way_id
  u8     kind        model.LineKind
  u8     flags       bit0 HAS_PREV_CTX, bit1 HAS_NEXT_CTX, bit2 intermittent, bit3 tunnel/culvert
  varint width_cm    0 = unknown
  varint name_ref
  varint point_count
  point_count × { svarint dx_cm, svarint dz_cm }
```

Clipping and context points work the same way as in `ROAD`.

### 1.6 `BLDG`: buildings

```
varint count
count × Building:
  varint osm_ref        (osm_id << 1) | is_relation
  u8     archetype      model.BuildingArchetype
  u8     use            model.BuildingUse
  u8     levels         1..255, already inferred when the BuildingFlags.LEVELS_INFERRED flag is set
  u8     flags          model.BuildingFlags
  varint height_cm      0 = derive from levels
  varint min_height_cm  building:min_height, 0 normally
  u8     roof_shape     model.RoofShape (inferred when not tagged)
  u8     roof_material  model.RoofMaterial
  u8     wall_material  model.WallMaterial
  u32    seed           deterministic variation seed = FNV-1a 32 of the osm_ref varint bytes
  varint name_ref
  varint ring_count     >= 1; ring 0 is the outer ring
  ring_count × Ring:
    varint n            >= 3, the closing point is not repeated
    n × { svarint dx_cm, svarint dz_cm }  ring's first point relative to tile origin, then deltas
```

Buildings are **not clipped**. Each is stored once, in the leaf-level tile that contains its outer-ring centroid. Outer rings are counter-clockwise and holes clockwise, both in the X-east/Z-north plane. Records are sorted by `osm_ref`.

### 1.7 `AREA`: polygons (water, land use, compounds), pre-triangulated

```
varint count
count × Area:
  varint osm_ref        (osm_id << 1) | is_relation
  u8     kind           model.AreaKind
  u8     flags          bit0 clipped_by_tile
  varint name_ref
  varint vertex_count
  vertex_count × { svarint dx_cm, svarint dz_cm }   delta-coded like rings
  varint index_count    multiple of 3
  index_count × varint  triangle vertex indices, counter-clockwise
  varint ring_count
  ring_count × { varint start, varint n }  boundary rings as vertex index ranges (for shorelines/outlines)
```

Areas **are clipped** to the tile square. Each clipped polygon is triangulated with constrained Delaunay triangulation. Edges created by the clip lie on the tile border. The runtime does not draw shoreline effects on border edges. It finds them by testing whether both endpoints lie on the same border line.

### 1.8 `POIS`: points of interest

```
varint count
count × Poi:
  varint osm_ref        (osm_id << 2) | type (0 node, 1 way, 2 relation)
  u16    kind           model.PoiKind
  u8     flags          model.PoiFlags
  u8     importance     0..255
  svarint x_cm          absolute local coordinates (not delta-coded)
  svarint z_cm
  svarint ele_dm        tagged elevation in decimetres, 0 when no HAS_ELE flag
  varint name_ref
  varint search_id      index into the global search index + 1, 0 = not searchable
```

`search_id` is the record's own entry: the region index entry with the same OSM object **and** the same kind (`PlaceKind + 1000` for places), so `entries[search_id - 1].kind == kind`. One OSM object can be both a POI and a place, and each record links to its own entry. Records whose entry was deduplicated away or filtered out get 0.

### 1.9 `SEED`: deterministic scatter

```
u64 tile_seed   = FNV-1a 64 over the bytes of (u64 tile_key, u32 data_version)
u16 ruleset     scatter ruleset id (1 in M0)
```

The runtime generates vegetation and props from `tile_seed`, the `BIOM` map and the masks it rasterises from `ROAD`, `BLDG` and `AREA`. Individual prop instances are never stored.

### 1.10 `META`

```
str json   small UTF-8 JSON object: {"region": "...", "sources": {"osm": "<md5>", "dem": "...", "landcover": "..."}}
```

`META` must not contain timestamps, so that builds stay deterministic.

## 2. Region pack (`.ghpk`): magic `GHPK`

A region pack holds all the tiles of one region, and it is the unit of download.

```
Header (64 bytes)
  0  u8[4]  magic            "GHPK"
  4  u16    version          = 1
  6  u16    flags            = 0
  8  u32    tile_count
 12  u32    data_version
 16  u64    directory_offset
 24  u64    directory_size   = tile_count * 24
 32  u8[16] region_id        ASCII, zero-padded (e.g. "kathmandu_valley" truncated to 16)
 48  u32    directory_crc32
 52  u8[12] reserved = 0

Tile data: tile blobs back to back (each a complete GHT1 blob)

Directory: tile_count entries, sorted ascending by key (binary-searchable)
  0  u64  key
  8  u64  offset   from start of file
 16  u32  size
 20  u32  crc32    of the tile blob
```

Packs are read with random access, either memory-mapped or by seeking. A region's manifest (`<region>.manifest.json`) is a sibling file:

```json
{
  "format": "ghumante-region-manifest", "version": 1,
  "region": "kathmandu_valley", "name": {"en": "Kathmandu Valley", "ne": "काठमाडौं उपत्यका"},
  "data_version": 1, "pipeline_version": "0.1.0", "built_at": "2026-10-04T19:00:00Z",
  "bbox_lonlat": [85.18, 27.55, 85.58, 27.83], "bbox_game": [x0, z0, x1, z1],
  "detail_levels": [8, 9, 10], "horizon_levels": [5, 6, 7],
  "scale_model": "identity",
  "files": [{"path": "kathmandu_valley.ghpk", "bytes": 123, "sha256": "..."},
            {"path": "kathmandu_valley.search.ghsi", "bytes": 123, "sha256": "..."},
            {"path": "kathmandu_valley.route.ghrg", "bytes": 123, "sha256": "..."}],
  "tile_counts": {"5": 4, "10": 1350},
  "sources": {"osm": {"md5": "...", "timestamp": "..."}, "dem": [...], "landcover": [...]},
  "attribution": ["© OpenStreetMap contributors", "..."],
  "stats": {"roads": 0, "buildings": 0, "surface_tagged_pct": 0.0}
}
```

`sources.osm.timestamp` is the PBF header's replication timestamp, or, when the header has none (the geo2day mirror), the `Last-Modified` time recorded for that file in `SOURCES.lock.json`, as ISO 8601 UTC; `null` only if neither exists. Paths inside the manifest (`files[].path`, `stats.qa.out_dir`) are relative to the region directory.

## 3. Search index (`.ghsi`): magic `GHSI`

There is one search index per region. The game merges the indexes of every installed region at load time.

```
Header (32 bytes)
  0  u8[4] magic "GHSI"
  4  u16   version = 1
  6  u16   flags
  8  u32   entry_count
 12  u32   key_count
 16  u32   names_offset
 20  u32   entries_offset
 24  u32   keys_offset
 28  u32   reserved

Names section: varint count, then count × { str default, str en, str ne }

Entries section: entry_count × 32 bytes
  0  u32  name_index      into names section
  4  u16  kind            PoiKind, or PlaceKind + 1000 for places
  6  u8   importance      0..255
  7  u8   flags           bit0 discoverable, bit1 landmark, bit2 transport_hub (fast travel target)
  8  i32  x_dm            game X in decimetres
 12  i32  z_dm            game Z in decimetres
 16  i32  lon_e7          longitude × 1e7
 20  i32  lat_e7          latitude × 1e7
 24  u32  osm_ref         (osm_id << 2) | type, truncated to 32 bits (diagnostics only)
 28  u16  district_index  into names section + 1, 0 = unknown
 30  u16  province_index  into names section + 1, 0 = unknown

Keys section: key_count × { str key, varint entry_index }, sorted by (key bytes, entry_index)
```

**Keys** are folded search keys (`translit.fold`). Each entry gets keys for:

* every name variant (`default`, `en`, `ne`, `alt`), folded. `alt` holds OSM `alt_name`, `old_name`, `official_name`, `short_name`, `loc_name` and their language variants, the curated `aliases` of `config/landmarks.yaml` (for example "Kathmandu Airport" and "TIA" for Tribhuvan International Airport), and the names of duplicates merged into the entry;
* the romanisation of every Devanagari variant (`translit.romanize`), folded;
* every word suffix of a multi-word name whose first word is *significant*: at least 3 characters and not in the folded stop list `search_index.SUFFIX_STOP_WORDS` (generic words such as temple, mandir, lake, tal, road, chowk, the). So "Patan Durbar Square" gets "durbar square" and "square", but "Pashupatinath Temple" gets no "temple" key.

**Entries.** Places, admin areas (province, district, local level) and named POIs except businesses and lodging. Place and POI candidates outside the region's leaf-tile coverage (the extract's buffer zone) are dropped; admin areas are kept. Two candidates of the same kind group within 300 m are duplicates when **any** of their folded `default`/`en`/`ne` names match; the more important one is kept and inherits the other's names as `alt`. A bus stop named after its destination ("Bus to Nagarkot") gets the default importance and no transport-hub flag. See the `search_index` module docstring for the importance formula.

Lookup (`search_index.search`, mirrored exactly in C# `SearchEngine`; all integer arithmetic, `m` in thousandths):

1. Fold the query. If it contains Devanagari, also romanise it and fold that.
2. Collect candidates in four ways:
   * exact key matches: `m = 1000`;
   * prefix matches found by binary search over the sorted keys: `m = max(100, 900 − 10 · extra characters)`, so 0.9 − 0.01 · extra with a floor of 0.1;
   * token matches: when the query has at least two distinct significant words, an entry for which every one of them is a prefix of some key of the entry gets `m = 600`. "Tribhuvan airport" finds "Tribhuvan International Airport". Phrase matches score higher;
   * fuzzy matches, for queries longer than 3 characters: keys that share the first character and lie within optimal-string-alignment (restricted Damerau–Levenshtein) distance `d ≤ max(1, len/4)`: `m = 750 − 100 · d`.
3. Each entry keeps its best `m`. Landmarks (flag bit1) add a rank bonus of 150, and other heritage POIs (kinds 120–129) add 50, capped at 1000: `m' = min(1000, m + bonus)`. This makes "Boudha" return Boudhanath Stupa before the Baudha neighbourhood.
4. Rank by `score = 1785 · m' + 3000 · importance`, which is `2 550 000 · (m'/1000 × 0.7 + importance/255 × 0.3)`. Break ties by kind priority (places before POIs, then ascending kind), then entry index (folded name, then osm_ref).

## 4. Routing graph (`.ghrg`): magic `GHRG`

```
Header (32 bytes)
  0  u8[4] magic "GHRG"
  4  u16   version = 1
  6  u16   flags
  8  u32   node_count
 12  u32   edge_count     directed edges
 16  u32   geom_bytes
 20  u32   name_count
 24  u64   reserved

Nodes: node_count × { i32 x_dm, i32 z_dm, i16 elev_m, u16 reserved }   (12 bytes)
Offsets: (node_count + 1) × u32         CSR: edges of node v are [off[v], off[v+1])
Edges: edge_count × 24 bytes
  0  u32 target
  4  u32 length_dm       along the polyline, game metres × 10
  8  u8  road_class      model.RoadClass
  9  u8  surface         model.Surface
 10  u8  access          model.Travel mask valid in THIS direction
 11  u8  flags           bit0 bridge, bit1 tunnel, bit2 ford, bit3 link
 12  u8  sac_scale
 13  u8  reserved
 14  i16 climb_m         elevation gain from source to target (may be negative)
 16  u32 geom_offset     byte offset into geometry blob
 20  u16 name_index      + 1 into names (0 = unnamed)
 22  u16 geom_count      number of points, including both endpoints
Geometry blob: per edge, geom_count × { svarint dx_dm, svarint dz_dm }, the first point relative to the source node
Names: name_count × { str default, str en, str ne }
```

The graph holds the region's roads clipped to its leaf-tile coverage; cut points become synthetic end nodes on the coverage edge. Nodes are OSM nodes that are an endpoint or that are shared by two or more routable ways. Ways are split at those nodes. Each edge is stored once per direction it can be travelled in. For a `oneway` road, the reverse edge keeps only `FOOT`, and it is omitted when its mask would be empty. `oneway:bicycle=no` (contraflow cycling) is not modelled yet, because the extract does not carry the tag (future work).

Travel cost for profile `p` is `length / speed(p, class, surface) × climb_factor`, with the tables in `routing.py` / `Routing/TravelProfiles.cs`. The reference query is A* with the straight-line distance divided by the profile's maximum speed as the heuristic.

Snapping a position to the graph (`routing.nearest_node` / C# `NearestNode`) picks the closest node with a usable outgoing edge for the profile (a destination: a usable incoming edge). By default it only considers nodes connected to the profile's *main component*, the largest strongly connected component of its usable edges (ties go to the component holding the smallest node index): starts must be able to reach it and destinations must be reachable from it. A landmark therefore never snaps into a small disconnected island, and every start can reach every destination. Exact distance ties go to the lowest node index.

## 5. QA export (`qa/`)

`build.py --qa` writes developer-only files for the QA viewer (`tools/qa-viewer/`) next to the pack, in `build/regions/<region>/qa/`:

```
qa/index.json                         region bbox, tile list, layer list, stats
qa/<layer>.geojson                    roads | trails | buildings | areas | lines | pois (lon/lat, decoded FROM THE PACK)
qa/buildings/<L>/<tx>_<ty>.geojson    buildings again, one file per leaf tile (the viewer loads them on demand)
qa/hillshade/<L>/<tx>_<ty>.png        one per detail tile at the leaf level (decoded from HGHT)
qa/biome/<L>/<tx>_<ty>.png            colour-coded BIOM
```

The GeoJSON layers are produced by **decoding the region pack**, not from intermediate data. Overlaying them on OpenStreetMap therefore checks the whole chain: extract, project, clip, encode, decode, unproject.
