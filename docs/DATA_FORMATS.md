# Ghumante data formats

These are the binary formats the offline pipeline (`/pipeline`, Python) writes and the game runtime (`/game`, C#) reads. The Python implementation in `pipeline/ghumante_pipeline/tile_format.py`, `pack.py`, `search_index.py` and `routing.py` is the reference. The C# readers live in `game/Assets/Ghumante/Core/Data/`, and golden-file tests in `core-tests/` keep the two in sync.

**Compatibility rule:** any change to a layout below bumps that format's `version` field. Readers reject versions they do not know. Enum values come from `pipeline/ghumante_pipeline/model.py` and are exported to `shared/enums.json` (with a `flag_enums` list naming the `[Flags]` ones); `python3 shared/golden/make_golden.py --enums-cs` regenerates `game/Assets/Ghumante/Core/Data/Enums.cs` from it. They are append-only. `ENUMS_VERSION` 3 appended EntryRule NO_LEATHER 6. `ENUMS_VERSION` 2 (W2 batch F1, docs/W2_DESIGN.md 9.3) appended BuildingArchetype RANA_PALACE 20 and NEWAR_HYBRID 21, PoiKind STATUE 127, GHAT 128 and PARAGLIDING_LANDING 506, AreaKind 27–39 (APRON 36, COURTYARD 38, TRAFFIC_ISLAND 39 are emitted; the rest are reserved for D3), LineKind 14–26 (reserved), the BuildingFlags HAS_PARTS, TAG_SUSPECT, OPEN_CANOPY and the PoiFlags HAS_FOOTPRINT, LANDMARK_LITE, INFERRED, and the new enums StyleProfile, AreaType, JunctionKind, Sidewalk, ObjectKind, TreeClass, TransitMode, LiveryClass, TurnRestriction, HeritageKind, HeritageFinish, EntryRule, KoraDirection, SacredZoneKind and the flag sets RoadAttrFlags, JunctionFlags, BuildingFrontFlags, PropFlags, RouteFlags, StopFlags, HeritageFlags.

**W2 additions are additive** (batch F2): the new tile chunks `RATR`, `JNCT`, `BFNT` and `PROP` (sections 1.11–1.14) are skipped by readers that do not know them, so `GHT1` stays version 1; the new region files `.ghrt`, `.ghcd` and `<region>.aviation.json` (sections 6–8) are listed in the manifest's `files`. `PIPELINE_DATA_VERSION` is 2 for packs that carry them.

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

Since W2 (D4) `building:part` ways are records too, with the `PART` flag (height, `min_height` or `building:min_level` × 3 m, roof shape from their own tags; a part of a religious host takes the host's archetype). A host footprint that contains a part's centroid has `HAS_PARTS`; the runtime draws the parts instead of the host. `TAG_SUSPECT`: a tagged height under 2.5 m without parts, more than 20 tagged levels, a tagged height per level outside 1.8–6 m, or more than 6 tagged levels in the Bhaktapur, Kirtipur and Panauti profiles. Religious archetypes keep the suspect tagged height (a temple height under 3 m is its plinth, W2_DESIGN 3.1); other buildings have it re-inferred. `LANDMARK` marks every building and part inside a hero's hide zone (section 7): the hero replica stands there and the record is not drawn. `OPEN_CANOPY` is set on `building=roof`. Closed `man_made=stupa` / `tower:type=stupa` ways without a building tag are STUPA footprints; `aeroway=apron` is never a building (it is an APRON area).

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

Flags (since W2, `tile_format.AreaFlags`): bit0 `CLIPPED_BY_TILE`, bit1 `HERITAGE_ZONE` (a heritage square: a PEDESTRIAN area with a `heritage` tag, a Durbar-square name, or `place=square` with a wikidata id; or a curated hero compound), bit2 `SACRED_NO_VEHICLE` (RELIGIOUS and COURTYARD areas, heritage squares, curated compounds: no motor vehicle may enter; the routing graph agrees, section 4). New kinds: APRON (`aeroway=apron`), TRAFFIC_ISLAND (`area:highway=traffic_island`) and COURTYARD (named bahal, bahi, chowk, dabali or vihar squares and pedestrian areas, and the holes of building footprints with such a name; a *chowk*-named one outside every curated historic core is a traffic junction and keeps its plain kind). A courtyard made from a building hole carries the building's `osm_ref`.

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
u64 tile_seed   = FNV-1a 64 over the bytes of (u64 tile_key, u32 ruleset)    (D10, stable seed)
u16 ruleset     scatter ruleset id (1)
```

Since W2 (D10) the seed no longer hashes `data_version`, so trees and props stay put when the pipeline is rebuilt; only a new scatter ruleset re-rolls them. The byte layout is unchanged: for ruleset 1 the seed equals the M0 seed of a data_version-1 tile.

The runtime generates vegetation and props from `tile_seed`, the `BIOM` map and the masks it rasterises from `ROAD`, `BLDG` and `AREA`. Procedural instances are never stored. Real OSM point objects (single trees, lamps, bus stops, signals, crossings and so on) are stored in the `PROP` chunk (section 1.14; the W2 subset of [CONTENT_COVERAGE](CONTENT_COVERAGE.md) D3), because they are real features at real positions, not decoration.

### 1.10 `META`

```
str json   small UTF-8 JSON object: {"region": "...", "sources": {"osm": "<md5>", "dem": "...", "landcover": "..."}}
```

`META` must not contain timestamps, so that builds stay deterministic.

### 1.11 `RATR`: road attributes (W2, D17)

One record per `ROAD` record, in the same order (so the chunk is absent, or its count equals the ROAD count).

```
varint count                       == ROAD count
count × RoadAttr:
  u8     area_type                 model.AreaType of the 250 m cell under the piece's length midpoint
  u8     sidewalk                  model.Sidewalk (left/right relative to the ROAD point order)
  u8     lanes_fwd, lanes_bwd      lanes:forward / lanes:backward along the point order; 0 = unknown
  u8     maxspeed_kmh              0 = unknown
  u8     flags                     model.RoadAttrFlags: bit0 DUAL (has partner), bit1 SERVICE_ROAD, bit2 HERITAGE_PEDESTRIAN,
                                   bit3 NO_MOTOR (access/motor_vehicle=no), bit4 LIT, bit5 BUS_ROUTE,
                                   bit6 RING_MEMBER (junction=roundabout|circular), bit7 PAINTABLE (real >= 5.5 m, sealed)
  varint partner_way_id            0 = none
  varint median_cm                 0 = none
  varint corridor_count            0 = unknown; else samples every 20 m from the piece's first rendered point
  corridor_count × varint corridor_dm   2 × min(dLeft, dRight) at the sample; 0 = open on one side (unbounded)
```

* **Area type**: the 250 m grid of `areatype.py` (street_life 1.1 + roads 1.2): OLD_CORE (a curated core, or building coverage ≥ 0.45 with ≥ 180 buildings), URBAN (coverage ≥ 0.22), FOREST (OSM forest or WorldCover tree cover ≥ 50 %, coverage < 0.05), PERI_URBAN (≥ 0.06), RURAL; PERI_URBAN, RURAL and FOREST cells above 1,650 m or steeper than 12 % to a neighbouring cell centre are HILL. Cells are aligned to multiples of 250 m in game space.
* For `oneway=-1` ways the ROAD record is reversed, so `sidewalk` left/right and `lanes_fwd`/`lanes_bwd` are swapped from the OSM tags.
* **Dual carriageways** (`DUAL`, `partner_way_id`): two one-way trunk to tertiary ways (not links) with the same `ref` or name, running in opposite directions with centrelines at most 15 m apart on at least half of the 10 m samples. `median_cm` = median centreline spacing − the two real half-widths, at least 100 cm. A one-way primary or secondary that runs alongside a dual trunk carriageway in the same direction, 6–15 m away, is a `SERVICE_ROAD` (Ring Road south).
* **Real width** (for `PAINTABLE` and the median) is the W2_DESIGN 4.1 table, the same rule as `RoadWidthModel.RealWidthM`.
* **Corridor samples** (roads 1.3) exist only for OLD_CORE and URBAN pieces of motor classes and pedestrian streets that are not bridges, tunnels or on a non-zero layer. At each sample two rays, 40 m each side, perpendicular to the centreline, stop at the first building outline (parts excluded). A sample whose point lies inside a footprint (mapping offset) repeats the previous valid sample (else the next, else 0). **Mapping-offset guard**: on a way with a tagged `width`, a bounded sample is never narrower than that width (rounded up to the decimetre): OSM building outlines and centrelines are often a few metres apart from each other, and the surveyed width wins (W2_DESIGN 9.5, at most 1 % of samples narrower than the tagged width).
* `HERITAGE_PEDESTRIAN`: the piece's length midpoint lies in a `SACRED_NO_VEHICLE` area.

### 1.12 `JNCT`: junctions (W2, D18)

```
varint count
count × Junction:
  varint osm_node_id               a ring: its smallest OSM node id
  u8     kind                      model.JunctionKind
  u8     arms
  u8     flags                     model.JunctionFlags: bit0 HAS_ISLAND_AREA, bit1 OFFICERS_2_4, bit2 HERITAGE_NO_MOTOR,
                                   bit3 CROSSINGS_MARKED, bit4 HAS_POLICE, bit5 HAS_SIGNALS
  svarint x_cm, z_cm               centre, absolute tile-local
  varint ring_diameter_cm          0 = no ring
  varint island_diameter_cm        0 = none or synthetic (runtime computes W2_DESIGN 4.7)
  varint island_area_osm_ref       (osm_id << 1) | is_relation of the AREA island, 0 = none
  varint name_ref
```

Records sorted by `(osm_node_id, kind, x_cm, z_cm)`, each in the leaf tile holding its centre. Only junctions that need more than ROAD are written:

* **ROUNDABOUT / CIRCULAR**: connected `junction=roundabout|circular` ways form one ring. Centre = mean of its distinct vertices; diameter = perimeter / π for a closed ring, else the largest chord (split arcs). `arms` = distinct motor ways touching the ring. Island: the largest TRAFFIC_ISLAND, park, meadow or grassland AREA whose representative point lies inside the ring circle (equal-area diameter).
* **MINI_ROUNDABOUT** and **SIGNALS**: `highway=mini_roundabout` / `traffic_signals` nodes on a motor road (`HAS_SIGNALS`).
* **Police chowks** (`config/curated/chowks.yaml`, 33 entries): snapped to a ring whose centre is within 120 m (flags added to the ring), else to the motor junction node within 120 m with the most arms (ties: higher class, nearer, lower id). A signal node keeps kind SIGNALS; otherwise SYNTHETIC_ISLAND when the node has ≥ 4 arms, one of them trunk or primary, and no mapped island within 45 m, else POLICE. All get `HAS_POLICE`, and `OFFICERS_2_4` for the big chowks. Name: the curated English name (the NE name only ever comes from OSM, ADR-005).
* Any record: `HERITAGE_NO_MOTOR` when the centre lies in a SACRED_NO_VEHICLE area; `CROSSINGS_MARKED` when a marked-crossing prop lies within 30 m (rings: radius + 15 m).
* `arms` counts a motor way twice when the node is inside it and once at its end.

### 1.13 `BFNT`: building fronts (W2, D4 + D19)

One 7-byte record per `BLDG` record, in the same order.

```
varint count                       == BLDG count
count × BuildingFront:
  u8     style_profile             model.StyleProfile (W2_DESIGN 2.1)
  u8     area_type                 model.AreaType of the cell under the outer-ring centroid
  u8     front_edge                ring-0 edge index facing the nearest motor road or pedestrian street; 255 = none
  u8     front_dist_dm             front edge midpoint to the road centreline, decimetres (0..255)
  u8     shop_bays                 bits 0-3 shop count from shop POIs inside (0..15); bit7 FROM_POI
  u8     flags                     model.BuildingFrontFlags: bit0 COURTYARD_HOST, bit1 CORNER (second road-facing edge),
                                   bit2 FACES_HERITAGE_SQUARE, bit3 RANA_HINT, bit4 STRUCTURE_RCC, bit5 STRUCTURE_MUD,
                                   bit6 ROOF_FLAT_TAGGED
  u8     second_edge               255 = none (corner houses)
```

* Edge `i` of ring 0 runs from vertex `i` to vertex `i + 1` of the **stored** (canonical, counter-clockwise) ring; `canonicalize` remaps the index when it re-orients a ring.
* An edge *faces* a road when the nearest segment of a motor road or pedestrian street (not footway, path, steps, cycleway, bridleway) within 25 m of the edge midpoint lies on the outer side of the edge (outward normal) and the edge is longer than 0.5 m. Among facing edges the nearest wins (ties: lower index). `second_edge` is the nearest facing edge whose road is a different way, within 15 m (`CORNER`).
* **Style profile**, first match: `config/style_zones.yaml` zones in list order (Thamel, the Boudha kora circle, then the historic cores), then Newar-town circles around OSM place nodes (400 m towns, 250 m villages; KHOKANA for villages under 1,500 buildings, else KIRTIPUR), then the level-7 municipality relations **by relation id** (METRO, or RIM outside URBAN/OLD_CORE cells for the rim municipalities), then the area type (URBAN and OLD_CORE METRO, everything else RIM). Parts take their host's profile and have no front (255).
* `shop_bays`: shop, restaurant, cafe, bank and market POIs inside the footprint (`poi_hints.py`).
* `FACES_HERITAGE_SQUARE`: the front edge midpoint lies within 8 m of a heritage-square AREA. `RANA_HINT`: `start_date` before 1960, or a non-religious building named or tagged durbar, palace or mahal. `STRUCTURE_*` from `building:structure`.

### 1.14 `PROP`: real point objects (W2 subset of D3)

```
varint count
count × Prop:
  varint  osm_ref      (osm_id << 2) | type (0 node, 1 way, 2 relation)
  u8      kind         model.ObjectKind
  u8      subtype      TREE: model.TreeClass; else 0
  u8      flags        model.PropFlags: bit0 YAW, bit1 HEIGHT_TAGGED, bit2 CHAUTARI, bit3 FROM_WAY, bit4 ON_ROAD
  svarint x_cm, z_cm   absolute tile-local
  u16     yaw_cdeg     bearing clockwise from north (+Z), 1/100 degree, 0..35999; 0 without the YAW flag
  varint  height_dm    0 = unknown
  varint  name_ref
  varint  ref_ref      NAME record holding the object's `ref` (gate "7", stand "D7"); 0 = none
```

Records sorted by `(osm_ref, kind)`, each in the leaf tile holding it. Stage 1 kinds: TREE (every `natural=tree`, species class from species/genus/taxon/leaf_type/name, CHAUTARI from the name), STREET_LAMP, BUS_STOP (`highway=bus_stop` or a bus platform), TRAFFIC_SIGNALS, CROSSING_MARKED (`crossing=zebra|marked|traffic_signals` or `crossing:markings` other than `no`) and CROSSING_UNMARKED, STORAGE_TANK, GATE (`barrier=gate`), AEROWAY_GATE, PARKING_POSITION (a line: the record sits on its last vertex with the yaw of its last segment), WINDSOCK, HELIPAD, TAXI_STAND. Ways become one record at their label point (`FROM_WAY`). `ON_ROAD` marks nodes that are vertices of a ROAD way. `YAW` also comes from a `direction` tag. Procedural dressing is never stored (section 1.9); everything here is a real OSM object at its real position.

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

Since W2 `files` also lists the region side files when the region has them: `<region>.transit.ghrt` (section 6), `<region>.curated.ghcd` and `hero_recipes.json` (section 7), and `<region>.aviation.json` (section 8, regions whose `regions.yaml` entry lists `aviation`). `stats.w2` summarises them (area types, style profiles, sacred zones, dual carriageways, junctions, transit, heritage, parts).

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

**Sacred zones (D14).** A piece of road whose length (by segment midpoints) lies at least half inside a `SACRED_NO_VEHICLE` area (section 1.7) loses every motor mode (MOTORBIKE, CAR, JEEP, BUS) in both directions; FOOT, BICYCLE and HORSE stay. A forward edge whose mask becomes empty is omitted like a reverse one.

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

## 6. Routes and turn restrictions (`.ghrt`): magic `GHRT` (W2, D2 + D22)

One file per region, `<region>.transit.ghrt`, written by `transit.py`.

```
Header (32 bytes)
  0  u8[4] magic "GHRT"
  4  u16   version = 1
  6  u16   flags = 0
  8  u32   route_count
 12  u32   restriction_count
 16  u32   name_count
 20  u32   payload_bytes        bytes after the header
 24  u32   payload_crc32        CRC-32 of those bytes
 28  u8[4] reserved = 0

Payload:
  varint name_count, then name_count × { str default, str en, str ne }       (name_ref k = record k-1; 0 = none)
  route_count × Route:
    varint osm_relation_id
    str    id                   "<mode>.r<relation id>", e.g. "bus.r3100600"
    u8     mode                 model.TransitMode
    u8     livery               model.LiveryClass (generic presets; never an operator's livery)
    u8     flags                model.RouteFlags: bit0 ROUNDTRIP, bit1 STOPS_FROM_MEMBERS, bit2 STOPS_INFERRED,
                                bit3 HAS_GAPS, bit4 MISSING_WAYS
    varint name_ref, ref_ref, from_ref, to_ref     route name, `ref`, `from`, `to` (the operator is never stored)
    u16    headway_peak_s, headway_off_s           0 for walking and cycling routes
    varint length_m             summed length of the chained ways
    varint way_count
    way_count × varint          (way_id << 1) | forward     forward = travelled in the OSM way's node order
    varint stop_count
    stop_count × Stop: varint osm_ref ((id << 2) | type, 0 = none), u8 flags (model.StopFlags: bit0 FROM_MEMBER,
                       bit1 INFERRED, bit2 TERMINAL), svarint x_dm, svarint z_dm (absolute game decimetres),
                       varint along_dm (distance along the route), varint name_ref
  restriction_count × Restriction:
    varint osm_relation_id, u8 kind (model.TurnRestriction), varint from_way, varint via_node (0 = via a way),
    varint via_way (0 = via a node), varint to_way
```

* Routes: `type=route` relations with `route=bus|trolleybus|coach` (BUS), `minibus|microbus` (MICROBUS), `tempo` or `network=safatempo` (TEMPO), `share_taxi`, `hiking`, `foot`, `bicycle`, `mtb` that use at least one road of the region; sorted by relation id.
* Ways: the relation's way members with an empty, `forward`, `backward`, `route` or `main` role, in member order, that are roads of the extract (else `MISSING_WAYS`). Each way's direction comes from its shared end node with the next way (the last way: with the previous); a way that does not connect sets `HAS_GAPS`; `backward` flips it.
* Stops: node members with a `stop*`/`platform*` role when there are any, else every BUS_STOP prop within 25 m of the route (bus, micro, tempo, share taxi only); ordered by distance along the route; a stop less than 60 m (members: 15 m) along the route after the previous kept stop is dropped. The first and last are TERMINAL.
* Livery: CITY_GREEN for a BUS route whose OSM `operator` contains a configured public operator word (`config/curated/transit.yaml`), COACH for a bus route reaching more than 15 km outside the region box, else the mode default (MINIBUS, MICROBUS, SAFA_TEMPO). Headways from the same file.
* Restrictions: `type=restriction` relations (`restriction`, else `restriction:motorcar` / `motor_vehicle`) with one `from` way, one `to` way and a `via` node or way, whose `from` or `to` way is a road of the region.
* The file is deterministic: names are numbered in first-use order (route name, ref, from, to, then stop names).

## 7. Curated DB (`.ghcd`): magic `GHCD` (W2, D5 + D12)

One file per region, `<region>.curated.ghcd`, compiled by `curated.py` from `pipeline/config/curated/heritage_sites.yaml` (stage 1 records; CONTENT_COVERAGE 4.2). The same records are also written as `hero_recipes.json` (format `ghumante-hero-recipes`, the JSON of `curated.records_json`), a temporary bridge for Track C (W2_DESIGN 9.4).

```
Header (32 bytes)
  0  u8[4]  magic "GHCD"
  4  u16    version = 1
  6  u16    flags = 0
  8  u32    record_count
 12  u32    payload_bytes
 16  u32    payload_crc32
 20  u8[12] reserved = 0

Payload: record_count × Heritage, sorted by id:
  str     id              our id, "her.<town>.<name>"
  u8      kind            model.HeritageKind
  u8      stage           1 or 2
  u8      flags           model.HeritageFlags: bit0 MANUAL_POSITION, bit1 HAS_COMPOUND, bit2 WALKABLE_COMPOUND,
                          bit3 NO_VEHICLES, bit4 SANCTUM_CLOSED (always), bit5 VERIFY
  u8      finish          model.HeritageFinish
  u8      tiers           0 = not a tiered building
  u8      plinth_levels
  u8      doors           0 = unknown
  u8      entry_rule      model.EntryRule: a non-blocking info card (W2-O1)
  u8      kora            model.KoraDirection
  u16     yaw_cdeg        main door bearing, 1/100 degree clockwise from north; 65535 = unknown
  varint  height_cm       total height to the finial; 0 = unknown
  str     name_en
  str     name_ne         from the OSM anchor (name:ne, else a Devanagari name); never typed (ADR-005)
  str     deity
  varint  anchor_ref      (osm_id << 2) | type of the OSM anchor; 0 = manual position
  varint  footprint_ref   (osm_id << 1) | is_relation of the BLDG record that is the hero's plan; 0 = none
  varint  compound_ref    (osm_id << 1) | is_relation of the compound AREA; 0 = none
  u64     anchor_tile     key of the leaf tile holding the anchor
  svarint x_cm, z_cm      anchor position, absolute game centimetres
  varint  hidden_count, hidden_count × varint   osm_ref of every BLDG record hidden by this hero (LANDMARK)
  varint  attr_count, attr_count × { str key, str value }   other generator parameters (sorted by key)
  str     provenance
  str     review          cultural sign-off ("pending" until signed)
```

* **Anchor** (D5): the OSM object, or the curated `manual` lon/lat (MANUAL_POSITION). A way or relation anchor that is a building footprint is the plan. A node anchor inside a footprint takes it as the plan when the footprint is at most 4 × the curated `plan_m` area (900 m² without one), so a gate node in a palace block never hides the palace.
* **Hide zone**: the plan footprint plus the footprints of the record's `hide` refs, grown by 0.3 m. Every building or part whose centroid (or representative point) lies inside, or that overlaps the zone by at least 30 % of its own area, gets `LANDMARK` and is listed in `hidden`. Remaining overlaps over 0.5 m² are counted in the build report (`unhidden_overlaps`, check G7).
* The plan footprint also gets the record's archetype (stupa STUPA, pagoda and mandapa TEMPLE_PAGODA, shikhara TEMPLE_SHIKHARA, gompa GOMPA, palace RANA_PALACE, house-temple and bahal NEWAR).
* Records whose anchor is not in the region's extract, or lies outside the region box, are left out of that region's file.

## 8. Aviation sidecar (`<region>.aviation.json`) (W2, D20)

JSON (UTF-8, sorted keys, `format` `ghumante-aviation`, `version` 1), compiled by `aviation.py` from `pipeline/config/aviation/tia.yaml` (the AV 4–5 tables of docs/research/w2/aviation.md copied unchanged; not for navigation) and the OSM runway:

* `airport.runway.thresholds."02"` / `"20"`: the south and north end nodes of the main runway way (`w340948564`), with `lon`, `lat`, game `x`/`z` (metres, 3 decimals; equal to the OSM nodes to the centimetre), `elev_m` (charted) and `dsm_m`; `pavement_ends.south` / `north` from the displaced-threshold ways; `heading_game_deg` and `length_between_thresholds_m` measured in game space.
* `airport.vor`, `airport.ndb`, `holds[]`, and every `procedures.<id>.points[]` point carry `lon`, `lat`, `x`, `z`; procedure points also `alt_m` (metres MSL, the game's Y), `ground_m` and `note`. Procedures: A1–A5, D1–D4, H-E, H-N, H-W.
* `schedule.movements_per_real_hour["0".."23"]`: movements per **real** hour by class (DOM_TP, HELI, INTL_NB, INTL_WB), selected by the game hour (AV 4.4); month factors, fog and monsoon modifiers, runway separation, runway-in-use and go-around rules; `procedure_weights`; generic `liveries` and model `classes`.
