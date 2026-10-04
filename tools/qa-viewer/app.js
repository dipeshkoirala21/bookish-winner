/*
 * Ghumante QA viewer (developer-only). See README.md in this folder.
 *
 * A static Leaflet page that overlays a decoded region pack export
 * (docs/DATA_FORMATS.md section 5) on OpenStreetMap. No build step; the only
 * dependency is Leaflet 1.9.4 (global `L`, loaded by index.html).
 *
 * Layout of this file:
 *   1. enums + property normalisation (tolerant of ints, NAMES and lower_case)
 *   2. palettes and styles
 *   3. NPL-TM84 projection + quadtree tile maths (mirrors pipeline projection.py)
 *   4. GridIndex: uniform lon/lat grid over feature bboxes
 *   5. VectorLayer: lazy load, view-culled L.canvas rendering, hit testing
 *   6. RasterTiles: per-tile PNG overlays placed by affine transform
 *   7. TileGrid overlay
 *   8. UI: layer list, legend, stats, inspector, search, URL hash, status bar
 *   9. bootstrap; everything is exposed on window.QA for debugging and tests
 */
(function () {
  'use strict';

  if (!window.L) {
    const b = document.getElementById('banner');
    if (b) {
      b.hidden = false;
      b.textContent = 'Leaflet failed to load from cdnjs.cloudflare.com (offline, blocked, or SRI mismatch). ' +
        'The viewer needs network access to the CDN.';
    }
    console.error('[qa] Leaflet (window.L) is not available');
    return;
  }

  // ===========================================================================
  // 1. Enums (copy of shared/enums.json v1; refreshed from the repo at start-up
  //    when serve.py says where it is) and property normalisation
  // ===========================================================================
  const ENUMS = {
    RoadClass: { UNKNOWN: 0, MOTORWAY: 1, TRUNK: 2, PRIMARY: 3, SECONDARY: 4, TERTIARY: 5, UNCLASSIFIED: 6,
      RESIDENTIAL: 7, LIVING_STREET: 8, SERVICE: 9, TRACK: 10, ROAD: 11, PEDESTRIAN: 12, FOOTWAY: 13, PATH: 14,
      STEPS: 15, CYCLEWAY: 16, BRIDLEWAY: 17 },
    Surface: { UNKNOWN: 0, ASPHALT: 1, CONCRETE: 2, BRICK: 3, COBBLE: 4, GRAVEL: 5, COMPACTED: 6, DIRT: 7, MUD: 8,
      SAND: 9, GRASS: 10, ROCK: 11, SNOW_ICE: 12, WOOD: 13, METAL: 14 },
    SurfaceGroup: { PAVED: 0, GRAVEL: 1, DIRT: 2, MUD: 3 },
    SurfaceSource: { TAGGED: 0, DERIVED: 1, INFERRED: 2, DEFAULT: 3 },
    SacScale: { UNKNOWN: 0, HIKING: 1, MOUNTAIN_HIKING: 2, DEMANDING_MOUNTAIN_HIKING: 3, ALPINE_HIKING: 4,
      DEMANDING_ALPINE_HIKING: 5, DIFFICULT_ALPINE_HIKING: 6 },
    BuildingUse: { UNKNOWN: 0, HOUSE: 1, APARTMENTS: 2, COMMERCIAL: 3, MIXED_USE: 4, EDUCATION: 5, HEALTH: 6,
      PUBLIC: 7, INDUSTRIAL: 8, RELIGIOUS: 9, HOTEL: 10, HUT: 11, GREENHOUSE: 12, CONSTRUCTION: 13, ROOF: 14,
      GARAGE: 15, OFFICE: 16, FARM: 17 },
    BuildingArchetype: { GENERIC: 0, NEWAR: 1, MODERN_URBAN: 2, HILL_VILLAGE: 3, TERAI: 4, SHERPA_HIMALAYAN: 5,
      TRANS_HIMALAYAN: 6, TEMPLE_PAGODA: 7, TEMPLE_SHIKHARA: 8, STUPA: 9, GOMPA: 10, CHORTEN: 11, SHRINE: 12,
      MOSQUE: 13, CHURCH: 14, INDUSTRIAL: 15, INSTITUTIONAL: 16, HUT: 17, GREENHOUSE: 18, TEAHOUSE: 19 },
    RoofShape: { UNKNOWN: 0, FLAT: 1, GABLED: 2, HIPPED: 3, PYRAMIDAL: 4, SKILLION: 5, DOME: 6, ROUND: 7,
      PAGODA: 8, SHIKHARA: 9, GAMBREL: 10, MANSARD: 11, HALF_HIPPED: 12, ONION: 13, CONE: 14 },
    RoofMaterial: { UNKNOWN: 0, CONCRETE: 1, METAL: 2, TILES: 3, SLATE: 4, THATCH: 5, WOOD: 6, GLASS: 7, MUD: 8,
      STONE: 9 },
    WallMaterial: { UNKNOWN: 0, PLASTER: 1, BRICK: 2, STONE: 3, MUD: 4, WOOD: 5, BAMBOO: 6, METAL: 7, GLASS: 8 },
    PlaceKind: { NONE: 0, COUNTRY: 1, PROVINCE: 2, CITY: 3, TOWN: 4, VILLAGE: 5, HAMLET: 6, ISOLATED_DWELLING: 7,
      SUBURB: 8, NEIGHBOURHOOD: 9, QUARTER: 10, LOCALITY: 11, FARM: 12, SQUARE: 13, ISLAND: 14, DISTRICT: 15,
      LOCAL_LEVEL: 16, WARD: 17 },
    PoiKind: { NONE: 0, TEMPLE_HINDU: 100, STUPA: 101, GOMPA: 102, SHRINE: 103, MOSQUE: 104, CHURCH: 105,
      PLACE_OF_WORSHIP: 106, CHORTEN: 107, MANI_WALL: 108, HERITAGE_SQUARE: 120, PALACE: 121, MONUMENT: 122,
      RUINS: 123, STONE_TAP: 124, CITY_GATE: 125, MUSEUM: 126, PEAK: 200, VIEWPOINT: 201, WATERFALL: 202,
      CAVE: 203, LAKE: 204, HOT_SPRING: 205, GLACIER: 206, PASS: 207, SPRING: 208, RIVER: 209, PARK: 210,
      PROTECTED_AREA: 211, NOTABLE_TREE: 212, RIDGE: 213, AIRPORT: 300, HELIPAD: 301, BUS_STATION: 302, FUEL: 303,
      CABLE_CAR_STATION: 304, RAILWAY_STATION: 305, TAXI_STAND: 306, BRIDGE: 307, PARKING: 308, HOTEL: 400,
      GUEST_HOUSE: 401, HOSTEL: 402, CAMP_SITE: 403, TEAHOUSE: 404, RESTAURANT: 405, CAFE: 406, ATTRACTION: 407,
      SHOP: 408, MARKETPLACE: 409, INFORMATION: 410, PICNIC_SITE: 411, THEME_PARK: 412, ZOO: 413, BUNGEE: 500,
      PARAGLIDING: 501, ZIPLINE: 502, RAFTING: 503, BOATING: 504, SAFARI: 505, SCHOOL: 600, HOSPITAL: 601,
      GOVERNMENT: 602, BANK: 603 },
    AreaKind: { NONE: 0, WATER_LAKE: 1, WATER_RIVER: 2, WATER_POND: 3, GLACIER: 4, WETLAND: 5, FOREST: 6,
      FARMLAND: 7, ORCHARD: 8, MEADOW: 9, SCRUB: 10, PARK: 11, RESIDENTIAL: 12, COMMERCIAL: 13, INDUSTRIAL: 14,
      RELIGIOUS: 15, PEDESTRIAN: 16, SAND_SHINGLE: 17, BARE_ROCK: 18, AERODROME: 19, CEMETERY: 20, MILITARY: 21,
      PITCH: 22, PROTECTED: 23, TEA_GARDEN: 24, GRASSLAND: 25, SCREE: 26 },
    LineKind: { NONE: 0, RIVER: 1, STREAM: 2, CANAL: 3, DITCH: 4, RAILWAY: 5, CABLE_CAR: 6, CHAIR_LIFT: 7,
      ZIP_LINE: 8, RUNWAY: 9, TAXIWAY: 10, CITY_WALL: 11, MANI_WALL: 12, WATERFALL: 13 },
    Biome: { NONE: 0, WATER: 1, URBAN_DENSE: 2, URBAN_GREEN: 3, TERAI_PADDY: 4, TERAI_CROPLAND: 5,
      TERAI_SAL_FOREST: 6, TERAI_GRASSLAND: 7, RIVERBED_GRAVEL: 8, CHURE_FOREST: 9, HILL_TERRACES: 10,
      HILL_FOREST: 11, HILL_SCRUB: 12, HILL_GRASSLAND: 13, SUBALPINE_FOREST: 14, ALPINE_MEADOW: 15,
      ALPINE_SCRUB: 16, SCREE_ROCK: 17, MORAINE: 18, GLACIER: 19, SNOW: 20, TRANS_HIMALAYAN_STEPPE: 21,
      TRANS_HIMALAYAN_CROPLAND: 22, ORCHARD: 23, TEA_GARDEN: 24, WETLAND: 25, VALLEY_CROPLAND: 26, BARE_SOIL: 27 },
    Travel: { FOOT: 1, BICYCLE: 2, MOTORBIKE: 4, CAR: 8, JEEP: 16, BUS: 32, HORSE: 64 },
    RoadFlags: { ONEWAY: 1, BRIDGE: 2, TUNNEL: 4, HAS_PREV_CTX: 8, HAS_NEXT_CTX: 16, LINK: 32, FORD: 64,
      SAC_INFERRED: 128 },
    BuildingFlags: { LEVELS_INFERRED: 1, HEIGHT_TAGGED: 2, ROOF_TAGGED: 4, LANDMARK: 8, PART: 16 },
    PoiFlags: { DISCOVERABLE: 1, LANDMARK: 2, HAS_ELE: 4, SACRED: 8 },
    // Not in enums.json: bit layouts written inline in DATA_FORMATS.md 1.5 / 1.7
    LineFlags: { HAS_PREV_CTX: 1, HAS_NEXT_CTX: 2, INTERMITTENT: 4, TUNNEL: 8 },
    AreaFlags: { CLIPPED_BY_TILE: 1 },
  };
  const FLAG_ENUMS = new Set(['Travel', 'RoadFlags', 'BuildingFlags', 'PoiFlags', 'LineFlags', 'AreaFlags']);

  // Spellings that differ from the enum names (OSM raw values, short forms).
  const ALIASES = {
    Surface: { SNOW: 'SNOW_ICE', ICE: 'SNOW_ICE', PAVING_STONES: 'BRICK', BRICKS: 'BRICK', SETT: 'COBBLE',
      COBBLESTONE: 'COBBLE', UNHEWN_COBBLESTONE: 'COBBLE', UNPAVED: 'DIRT', EARTH: 'DIRT', GROUND: 'DIRT',
      FINE_GRAVEL: 'GRAVEL', PEBBLESTONE: 'GRAVEL', PAVED: 'ASPHALT', STONE: 'ROCK', TIMBER: 'WOOD', STEEL: 'METAL' },
    SacScale: { T0: 'UNKNOWN', T1: 'HIKING', T2: 'MOUNTAIN_HIKING', T3: 'DEMANDING_MOUNTAIN_HIKING',
      T4: 'ALPINE_HIKING', T5: 'DEMANDING_ALPINE_HIKING', T6: 'DIFFICULT_ALPINE_HIKING', NONE: 'UNKNOWN' },
    RoadClass: { MOTORWAY_LINK: 'MOTORWAY', TRUNK_LINK: 'TRUNK', PRIMARY_LINK: 'PRIMARY',
      SECONDARY_LINK: 'SECONDARY', TERTIARY_LINK: 'TERTIARY' },
  };
  const SAC_SHORT = { UNKNOWN: '?', HIKING: 'T1', MOUNTAIN_HIKING: 'T2', DEMANDING_MOUNTAIN_HIKING: 'T3',
    ALPINE_HIKING: 'T4', DEMANDING_ALPINE_HIKING: 'T5', DIFFICULT_ALPINE_HIKING: 'T6' };
  const TRAIL_CLASSES = new Set(['PEDESTRIAN', 'FOOTWAY', 'PATH', 'STEPS', 'CYCLEWAY', 'BRIDLEWAY']);

  const _byValue = {};
  function byValue(enumName) {
    if (!_byValue[enumName]) {
      const m = new Map();
      for (const [k, v] of Object.entries(ENUMS[enumName] || {})) m.set(v, k);
      _byValue[enumName] = m;
    }
    return _byValue[enumName];
  }
  function normKey(s) {
    return String(s).trim().toUpperCase().replace(/[\s\-:./]+/g, '_');
  }
  /** Any enum spelling (int, "7", "RESIDENTIAL", "residential", "living-street", {name}) -> NAME. */
  function normEnum(enumName, v) {
    if (v === null || v === undefined || v === '') return null;
    if (typeof v === 'object' && !Array.isArray(v)) return normEnum(enumName, v.name ?? v.value);
    if (typeof v === 'boolean') return null;
    if (typeof v === 'number' || /^-?\d+$/.test(String(v).trim())) {
      const n = Number(v);
      return byValue(enumName).get(n) ?? ('#' + n);
    }
    const k = normKey(v);
    const E = ENUMS[enumName];
    if (E && k in E) return k;
    const alias = ALIASES[enumName] && ALIASES[enumName][k];
    return alias || k;
  }
  function enumValue(enumName, name) {
    const E = ENUMS[enumName];
    return E && name in E ? E[name] : null;
  }
  /** Flags as int bitmask, list of names, "A|B" string or {NAME: bool} -> array of NAMES. */
  function flagNames(enumName, v) {
    if (v === null || v === undefined || v === '') return [];
    if (Array.isArray(v)) return v.map(normKey);
    if (typeof v === 'object') return Object.keys(v).filter((k) => v[k]).map(normKey);
    if (typeof v === 'string' && !/^\d+$/.test(v.trim())) return v.split(/[|,;\s]+/).filter(Boolean).map(normKey);
    const n = Number(v);
    const out = [];
    let known = 0;
    for (const [k, bit] of Object.entries(ENUMS[enumName] || {})) {
      if (n & bit) out.push(k);
      known |= bit;
    }
    if (n & ~known) out.push('0x' + (n & ~known).toString(16));
    return out;
  }
  /** First non-empty property among aliases. */
  function prop(p, ...keys) {
    for (const k of keys) {
      const v = p[k];
      if (v !== undefined && v !== null && v !== '') return v;
    }
    return undefined;
  }
  function num(v) {
    if (v === undefined || v === null || v === '') return null;
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
  }
  function truthy(v) {
    return v === true || v === 1 || v === '1' || v === 'yes' || v === 'true';
  }
  function featureNames(p) {
    let def = prop(p, 'name', 'name_default', 'default_name');
    let en = prop(p, 'name_en', 'name:en');
    let ne = prop(p, 'name_ne', 'name:ne');
    const names = p.names && typeof p.names === 'object' ? p.names : null;
    if (names) { def = def ?? names.default; en = en ?? names.en; ne = ne ?? names.ne; }
    if (def && typeof def === 'object') { en = en ?? def.en; ne = ne ?? def.ne; def = def.default ?? def.en ?? def.ne; }
    return { def: def || en || ne || '', en: en || '', ne: ne || '' };
  }
  function hasFlag(n, name) {
    return n.flags.includes(name);
  }

  // Layer-specific normalisers: compute the few fields styling needs, once per feature.
  function normRoad(p) {
    const flags = flagNames('RoadFlags', prop(p, 'flags', 'road_flags'));
    for (const k of ['oneway', 'bridge', 'tunnel', 'link', 'ford', 'sac_inferred']) {
      if (truthy(p[k]) && !flags.includes(k.toUpperCase())) flags.push(k.toUpperCase());
    }
    const cls = normEnum('RoadClass', prop(p, 'road_class', 'class', 'highway', 'cls')) || 'UNKNOWN';
    const sac = normEnum('SacScale', prop(p, 'sac_scale', 'sac')) || 'UNKNOWN';
    return {
      cls, sac, flags,
      surface: normEnum('Surface', prop(p, 'surface')) || 'UNKNOWN',
      source: normEnum('SurfaceSource', prop(p, 'surface_source', 'surfaceSource', 'source')) || 'DEFAULT',
      sacInferred: flags.includes('SAC_INFERRED'),
      trail: TRAIL_CLASSES.has(cls),
    };
  }
  function normBuilding(p) {
    const flags = flagNames('BuildingFlags', prop(p, 'flags', 'building_flags'));
    if (truthy(p.levels_inferred) && !flags.includes('LEVELS_INFERRED')) flags.push('LEVELS_INFERRED');
    if (truthy(p.landmark) && !flags.includes('LANDMARK')) flags.push('LANDMARK');
    const levels = num(prop(p, 'levels', 'building_levels', 'building:levels'));
    let inferred = flags.includes('LEVELS_INFERRED');
    const src = prop(p, 'levels_source');
    if (src !== undefined) inferred = normKey(src) !== 'TAGGED';
    return {
      arche: normEnum('BuildingArchetype', prop(p, 'archetype', 'building_archetype')) || 'GENERIC',
      use: normEnum('BuildingUse', prop(p, 'use', 'building_use')) || 'UNKNOWN',
      levels, levelsInferred: inferred, flags,
    };
  }
  function poiGroupOf(kindNum) {
    if (kindNum === null) return 'other';
    if (kindNum >= 1000) return 'place';
    if (kindNum >= 600) return 'civic';
    if (kindNum >= 500) return 'activity';
    if (kindNum >= 400) return 'tourism';
    if (kindNum >= 300) return 'transport';
    if (kindNum >= 200) return 'nature';
    if (kindNum >= 120) return 'heritage';
    if (kindNum >= 100) return 'religious';
    return 'other';
  }
  function normPoi(p) {
    const raw = prop(p, 'kind', 'poi_kind', 'type');
    let kind;
    let kindNum = null;
    if (typeof raw === 'number' || /^\d+$/.test(String(raw ?? ''))) {
      kindNum = Number(raw);
      kind = kindNum >= 1000 ? (normEnum('PlaceKind', kindNum - 1000)) : normEnum('PoiKind', kindNum);
    } else {
      kind = normEnum('PoiKind', raw) || 'NONE';
      kindNum = enumValue('PoiKind', kind);
      if (kindNum === null && enumValue('PlaceKind', kind) !== null) kindNum = 1000 + enumValue('PlaceKind', kind);
    }
    const flags = flagNames('PoiFlags', prop(p, 'flags', 'poi_flags'));
    if (truthy(p.landmark) && !flags.includes('LANDMARK')) flags.push('LANDMARK');
    let imp = num(prop(p, 'importance'));
    if (imp !== null && imp <= 1 && String(p.importance).includes('.')) imp = Math.round(imp * 255); // 0..1 float
    return { kind, kindNum, group: poiGroupOf(kindNum), importance: imp ?? 0, flags };
  }
  function normPlace(p) {
    const raw = prop(p, 'kind', 'place_kind', 'place');
    let kind = normEnum('PlaceKind', typeof raw === 'number' && raw >= 1000 ? raw - 1000 : raw) || 'NONE';
    if (kind.startsWith('#')) kind = 'NONE';
    let imp = num(prop(p, 'importance')) ?? 0;
    if (imp <= 1 && String(p.importance).includes('.')) imp = Math.round(imp * 255);
    return { kind, kindNum: 1000 + (enumValue('PlaceKind', kind) ?? 0), group: 'place', importance: imp, flags: [] };
  }
  function normArea(p) {
    return { kind: normEnum('AreaKind', prop(p, 'kind', 'area_kind')) || 'NONE',
      flags: flagNames('AreaFlags', prop(p, 'flags')) };
  }
  function normLine(p) {
    const flags = flagNames('LineFlags', prop(p, 'flags'));
    if (truthy(p.tunnel) && !flags.includes('TUNNEL')) flags.push('TUNNEL');
    if (truthy(p.intermittent) && !flags.includes('INTERMITTENT')) flags.push('INTERMITTENT');
    return { kind: normEnum('LineKind', prop(p, 'kind', 'line_kind')) || 'NONE', flags };
  }

  // ===========================================================================
  // 2. Palettes and styles
  // ===========================================================================
  const UNKNOWN_COLOR = '#ff00ff'; // anything the palette does not know: should stand out

  const SURFACE_COLORS = {
    ASPHALT: '#3a3a3a', CONCRETE: '#b8b8b8', BRICK: '#b5402f', COBBLE: '#7d6450', GRAVEL: '#c9ad7f',
    COMPACTED: '#bdb76b', DIRT: '#d2691e', MUD: '#4e2a12', SAND: '#ecd540', GRASS: '#4caf50', ROCK: '#5d6d7e',
    SNOW_ICE: '#ffffff', WOOD: '#a0522d', METAL: '#00a3a3', UNKNOWN: UNKNOWN_COLOR,
  };
  const SOURCE_COLORS = { TAGGED: '#1a9850', DERIVED: '#2c7fb8', INFERRED: '#fd8d3c', DEFAULT: '#d7191c' };
  const SOURCE_NOTES = { TAGGED: 'surface=* in OSM', DERIVED: 'from tracktype / smoothness',
    INFERRED: 'statistical model', DEFAULT: 'no evidence: class default' };
  const CLASS_ORDER = ['MOTORWAY', 'TRUNK', 'PRIMARY', 'SECONDARY', 'TERTIARY', 'UNCLASSIFIED', 'RESIDENTIAL',
    'ROAD', 'LIVING_STREET', 'SERVICE', 'TRACK', 'PEDESTRIAN', 'FOOTWAY', 'PATH', 'STEPS', 'CYCLEWAY', 'BRIDLEWAY',
    'UNKNOWN'];
  const CLASS_COLORS = {
    MOTORWAY: '#e8566e', TRUNK: '#f07e4a', PRIMARY: '#f5a623', SECONDARY: '#e3c91a', TERTIARY: '#9cc93a',
    UNCLASSIFIED: '#5fa8d3', RESIDENTIAL: '#7b8794', ROAD: '#ff66cc', LIVING_STREET: '#a984cf', SERVICE: '#a6a6a6',
    TRACK: '#a67c52', PEDESTRIAN: '#c39bd3', FOOTWAY: '#fa8072', PATH: '#e06666', STEPS: '#b30000',
    CYCLEWAY: '#3b8bd9', BRIDLEWAY: '#6b8e23', UNKNOWN: UNKNOWN_COLOR,
  };
  const CLASS_WIDTH = { MOTORWAY: 8, TRUNK: 7, PRIMARY: 6, SECONDARY: 5.5, TERTIARY: 5, UNCLASSIFIED: 4,
    RESIDENTIAL: 4, ROAD: 4, LIVING_STREET: 3.5, SERVICE: 3, TRACK: 3, PEDESTRIAN: 3.5, FOOTWAY: 2, PATH: 2,
    STEPS: 2.5, CYCLEWAY: 2, BRIDLEWAY: 2, UNKNOWN: 3 };
  // Classes drawn below z13 (QA: the network skeleton); everything is drawn from z13.
  const CLASS_MINZOOM = { MOTORWAY: 0, TRUNK: 0, PRIMARY: 0, SECONDARY: 9, TERTIARY: 11 };
  const SAC_ORDER = ['HIKING', 'MOUNTAIN_HIKING', 'DEMANDING_MOUNTAIN_HIKING', 'ALPINE_HIKING',
    'DEMANDING_ALPINE_HIKING', 'DIFFICULT_ALPINE_HIKING', 'UNKNOWN'];
  const SAC_COLORS = { HIKING: '#f2d600', MOUNTAIN_HIKING: '#f39c12', DEMANDING_MOUNTAIN_HIKING: '#e74c3c',
    ALPINE_HIKING: '#b0005a', DEMANDING_ALPINE_HIKING: '#6a1b9a', DIFFICULT_ALPINE_HIKING: '#111111',
    UNKNOWN: '#9e9e9e' };

  const ARCHETYPE_COLORS = {
    GENERIC: '#bdbdbd', NEWAR: '#c0392b', MODERN_URBAN: '#5dade2', HILL_VILLAGE: '#d4a373', TERAI: '#e9c46a',
    SHERPA_HIMALAYAN: '#6c5ce7', TRANS_HIMALAYAN: '#b08968', TEMPLE_PAGODA: '#d35400', TEMPLE_SHIKHARA: '#ff9f43',
    STUPA: '#f1c40f', GOMPA: '#8e44ad', CHORTEN: '#a29bfe', SHRINE: '#fd79a8', MOSQUE: '#16a085',
    CHURCH: '#2c3e50', INDUSTRIAL: '#7f8c8d', INSTITUTIONAL: '#2471a3', HUT: '#8d6e63', GREENHOUSE: '#a8e6cf',
    TEAHOUSE: '#00b894',
  };
  const LEVEL_BUCKETS = ['1', '2', '3', '4', '5', '6', '7', '8+', '?'];
  const LEVEL_COLORS = { 1: '#fde725', 2: '#b5de2b', 3: '#6ece58', 4: '#35b779', 5: '#1f9e89', 6: '#26828e',
    7: '#31688e', '8+': '#482878', '?': UNKNOWN_COLOR };
  const LEVELS_SOURCE_COLORS = { TAGGED: SOURCE_COLORS.TAGGED, INFERRED: SOURCE_COLORS.INFERRED };

  const POI_GROUPS = {
    religious: { color: '#8e44ad', label: 'religious (100-119)' },
    heritage: { color: '#a0522d', label: 'heritage (120-199)' },
    nature: { color: '#2e8b57', label: 'nature (200-299)' },
    transport: { color: '#1f78b4', label: 'transport (300-399)' },
    tourism: { color: '#ff7f00', label: 'tourism & services (400-499)' },
    activity: { color: '#e7298a', label: 'activities (500-599)' },
    civic: { color: '#636363', label: 'civic (600+)' },
    place: { color: '#111111', label: 'places' },
    other: { color: UNKNOWN_COLOR, label: 'other / unknown kind' },
  };
  const AREA_COLORS = {
    WATER_LAKE: '#4a90d9', WATER_RIVER: '#5b9bd5', WATER_POND: '#6aaee8', GLACIER: '#cfeeff', WETLAND: '#7fc6a4',
    FOREST: '#2d7a3a', FARMLAND: '#e3dc8f', ORCHARD: '#a8d08d', MEADOW: '#c5e8a5', SCRUB: '#9cbb6b', PARK: '#7bd17b',
    RESIDENTIAL: '#d9c7b8', COMMERCIAL: '#f2a5a5', INDUSTRIAL: '#c9b3d9', RELIGIOUS: '#d4a5d4', PEDESTRIAN: '#d6d6d6',
    SAND_SHINGLE: '#e8dcb0', BARE_ROCK: '#a9a9a9', AERODROME: '#c8c8e8', CEMETERY: '#9fbf9f', MILITARY: '#e6a0a0',
    PITCH: '#8fd18f', PROTECTED: '#6b8e23', TEA_GARDEN: '#4f9a4f', GRASSLAND: '#bfe3a0', SCREE: '#b8b0a8',
    NONE: UNKNOWN_COLOR,
  };
  const LINE_STYLES = {
    RIVER: { color: '#2b6cb0', weight: 4 }, STREAM: { color: '#4a90d9', weight: 2.5 },
    CANAL: { color: '#3fa7d6', weight: 2.5 }, DITCH: { color: '#7fb3d5', weight: 1.5 },
    RAILWAY: { color: '#444444', weight: 3, dashArray: '8 4' }, CABLE_CAR: { color: '#222222', weight: 2, dashArray: '2 5' },
    CHAIR_LIFT: { color: '#555555', weight: 2, dashArray: '2 5' }, ZIP_LINE: { color: '#e7298a', weight: 2, dashArray: '3 4' },
    RUNWAY: { color: '#666666', weight: 8 }, TAXIWAY: { color: '#888888', weight: 4 },
    CITY_WALL: { color: '#8b4513', weight: 3 }, MANI_WALL: { color: '#6a1b9a', weight: 3 },
    WATERFALL: { color: '#00bcd4', weight: 3 }, NONE: { color: UNKNOWN_COLOR, weight: 2 },
  };

  function zoomScale(z) {
    return z >= 18 ? 1.8 : z >= 17 ? 1.4 : z >= 16 ? 1.1 : z >= 15 ? 0.9 : z >= 14 ? 0.7 : z >= 13 ? 0.55 : 0.45;
  }
  function levelBucket(levels) {
    if (levels === null || levels === undefined || !(levels > 0)) return '?';
    return levels >= 8 ? '8+' : String(Math.round(levels));
  }

  // ===========================================================================
  // 3. NPL-TM84 (+proj=tmerc +lat_0=0 +lon_0=84 +k=0.9996 +x_0=500000 +ellps=WGS84)
  //    Krueger series (3rd order in n): sub-millimetre over Nepal; the smoke test
  //    checks it against pyproj. Game = TM84 - (100 000, 2 900 000), identity warp.
  // ===========================================================================
  const proj = (() => {
    const a = 6378137.0;
    const f = 1 / 298.257223563;
    const k0 = 0.9996;
    const lon0 = 84 * Math.PI / 180;
    const FE = 500000.0;
    const ORIGIN_E = 100000.0;
    const ORIGIN_N = 2900000.0;
    const n = f / (2 - f);
    const n2 = n * n;
    const n3 = n2 * n;
    const A = a / (1 + n) * (1 + n2 / 4 + n2 * n2 / 64);
    const alpha = [n / 2 - 2 * n2 / 3 + 5 * n3 / 16, 13 * n2 / 48 - 3 * n3 / 5, 61 * n3 / 240];
    const beta = [n / 2 - 2 * n2 / 3 + 37 * n3 / 96, n2 / 48 + n3 / 15, 17 * n3 / 480];
    const delta = [2 * n - 2 * n2 / 3 - 2 * n3, 7 * n2 / 3 - 8 * n3 / 5, 56 * n3 / 15];
    const c = 2 * Math.sqrt(n) / (1 + n);
    const D2R = Math.PI / 180;

    function lonLatToTM(lon, lat) {
      const phi = lat * D2R;
      const dl = lon * D2R - lon0;
      const s = Math.sin(phi);
      const t = Math.sinh(Math.atanh(s) - c * Math.atanh(c * s));
      const xi = Math.atan2(t, Math.cos(dl));
      const eta = Math.atanh(Math.sin(dl) / Math.sqrt(1 + t * t));
      let E = eta;
      let N = xi;
      for (let j = 1; j <= 3; j++) {
        E += alpha[j - 1] * Math.cos(2 * j * xi) * Math.sinh(2 * j * eta);
        N += alpha[j - 1] * Math.sin(2 * j * xi) * Math.cosh(2 * j * eta);
      }
      return [FE + k0 * A * E, k0 * A * N];
    }
    function tmToLonLat(E, N) {
      const xi = N / (k0 * A);
      const eta = (E - FE) / (k0 * A);
      let xp = xi;
      let ep = eta;
      for (let j = 1; j <= 3; j++) {
        xp -= beta[j - 1] * Math.sin(2 * j * xi) * Math.cosh(2 * j * eta);
        ep -= beta[j - 1] * Math.cos(2 * j * xi) * Math.sinh(2 * j * eta);
      }
      const chi = Math.asin(Math.sin(xp) / Math.cosh(ep));
      let phi = chi;
      for (let j = 1; j <= 3; j++) phi += delta[j - 1] * Math.sin(2 * j * chi);
      const lon = lon0 + Math.atan2(Math.sinh(ep), Math.cos(xp));
      return [lon / D2R, phi / D2R];
    }
    const lonLatToGame = (lon, lat) => { const [e, nn] = lonLatToTM(lon, lat); return [e - ORIGIN_E, nn - ORIGIN_N]; };
    const gameToLonLat = (x, z) => tmToLonLat(x + ORIGIN_E, z + ORIGIN_N);
    const tileSize = (level) => Math.pow(2, 20 - level);
    function tileAt(level, x, z) {
      const s = tileSize(level);
      return { level, tx: Math.floor(x / s), ty: Math.floor(z / s) };
    }
    /** Corners [sw, se, ne, nw] as [lon, lat]. */
    function tileCorners(level, tx, ty) {
      const s = tileSize(level);
      const x0 = tx * s;
      const z0 = ty * s;
      return [gameToLonLat(x0, z0), gameToLonLat(x0 + s, z0), gameToLonLat(x0 + s, z0 + s), gameToLonLat(x0, z0 + s)];
    }
    /** u64 tile key (L << 58 | morton(tx, ty)) as a decimal string (exceeds 2^53). */
    function tileKey(level, tx, ty) {
      let m = 0n;
      for (let i = 0n; i < 29n; i++) {
        m |= ((BigInt(tx) >> i) & 1n) << (2n * i);
        m |= ((BigInt(ty) >> i) & 1n) << (2n * i + 1n);
      }
      return ((BigInt(level) << 58n) | m).toString();
    }
    return { lonLatToTM, tmToLonLat, lonLatToGame, gameToLonLat, tileSize, tileAt, tileCorners, tileKey };
  })();

  // ===========================================================================
  // 4. GridIndex: features are bucketed into a uniform lon/lat grid by bbox;
  //    a query visits only the cells overlapping the view.
  // ===========================================================================
  class GridIndex {
    constructor(cellDeg) {
      this.cell = cellDeg;
      this.cells = new Map();
      this.bbox = new Float64Array(4 * 1024);
      this.size = 0;
      this.stamp = new Uint32Array(1024);
      this.qid = 0;
    }
    _ix(lon) { return Math.floor((lon + 180) / this.cell); }
    _iy(lat) { return Math.floor((lat + 90) / this.cell); }
    /** Insert feature i (dense ids 0..n-1) with bbox [w, s, e, n]. */
    insert(i, w, s, e, n) {
      if (4 * (i + 1) > this.bbox.length) {
        const nb = new Float64Array(Math.max(this.bbox.length * 2, 4 * (i + 1)));
        nb.set(this.bbox);
        this.bbox = nb;
        const ns = new Uint32Array(nb.length / 4);
        ns.set(this.stamp);
        this.stamp = ns;
      }
      this.bbox[4 * i] = w; this.bbox[4 * i + 1] = s; this.bbox[4 * i + 2] = e; this.bbox[4 * i + 3] = n;
      this.size = Math.max(this.size, i + 1);
      if (!(w <= e && s <= n)) return; // empty geometry
      const x0 = this._ix(w); const x1 = this._ix(e); const y0 = this._iy(s); const y1 = this._iy(n);
      for (let x = x0; x <= x1; x++) {
        for (let y = y0; y <= y1; y++) {
          const k = x * 1048576 + y;
          let arr = this.cells.get(k);
          if (!arr) { arr = []; this.cells.set(k, arr); }
          arr.push(i);
        }
      }
    }
    /** Feature ids whose bbox intersects [w, s, e, n]. */
    query(w, s, e, n) {
      const out = [];
      if (++this.qid === 0xffffffff) { this.stamp.fill(0); this.qid = 1; }
      const q = this.qid;
      const bb = this.bbox;
      const x0 = this._ix(w); const x1 = this._ix(e); const y0 = this._iy(s); const y1 = this._iy(n);
      if ((x1 - x0 + 1) * (y1 - y0 + 1) > 4 * this.cells.size) {
        // view larger than the data: scan everything once
        for (let i = 0; i < this.size; i++) {
          if (bb[4 * i] <= e && bb[4 * i + 2] >= w && bb[4 * i + 1] <= n && bb[4 * i + 3] >= s) out.push(i);
        }
        return out;
      }
      for (let x = x0; x <= x1; x++) {
        for (let y = y0; y <= y1; y++) {
          const arr = this.cells.get(x * 1048576 + y);
          if (!arr) continue;
          for (let j = 0; j < arr.length; j++) {
            const i = arr[j];
            if (this.stamp[i] === q) continue;
            this.stamp[i] = q;
            if (bb[4 * i] <= e && bb[4 * i + 2] >= w && bb[4 * i + 1] <= n && bb[4 * i + 3] >= s) out.push(i);
          }
        }
      }
      return out;
    }
  }

  function geomBBox(g) {
    let w = Infinity; let s = Infinity; let e = -Infinity; let n = -Infinity;
    function walk(c) {
      if (typeof c[0] === 'number') {
        if (c[0] < w) w = c[0];
        if (c[0] > e) e = c[0];
        if (c[1] < s) s = c[1];
        if (c[1] > n) n = c[1];
      } else {
        for (const cc of c) walk(cc);
      }
    }
    if (!g) return [w, s, e, n];
    if (g.type === 'GeometryCollection') {
      for (const gg of g.geometries || []) {
        const b = geomBBox(gg);
        w = Math.min(w, b[0]); s = Math.min(s, b[1]); e = Math.max(e, b[2]); n = Math.max(n, b[3]);
      }
    } else if (g.coordinates && g.coordinates.length) {
      walk(g.coordinates);
    }
    return [w, s, e, n];
  }
  const R_EARTH = 6371008.8;
  function haversine(a, b) {
    const p1 = a[1] * Math.PI / 180; const p2 = b[1] * Math.PI / 180;
    const dp = p2 - p1; const dl = (b[0] - a[0]) * Math.PI / 180;
    const h = Math.sin(dp / 2) ** 2 + Math.cos(p1) * Math.cos(p2) * Math.sin(dl / 2) ** 2;
    return 2 * R_EARTH * Math.asin(Math.min(1, Math.sqrt(h)));
  }
  function lineLength(g) {
    if (!g) return 0;
    const lines = g.type === 'LineString' ? [g.coordinates] : g.type === 'MultiLineString' ? g.coordinates : [];
    let t = 0;
    for (const l of lines) for (let i = 1; i < l.length; i++) t += haversine(l[i - 1], l[i]);
    return t;
  }
  function ringArea(r) {
    // spherical-excess-free planar approximation in local metres (fine for QA readouts)
    if (r.length < 3) return 0;
    const lat0 = r[0][1] * Math.PI / 180;
    const kx = Math.cos(lat0) * Math.PI / 180 * R_EARTH; const ky = Math.PI / 180 * R_EARTH;
    let s = 0;
    for (let i = 0, j = r.length - 1; i < r.length; j = i++) {
      s += (r[j][0] * kx) * (r[i][1] * ky) - (r[i][0] * kx) * (r[j][1] * ky);
    }
    return s / 2;
  }
  function polyArea(g) {
    if (!g) return 0;
    const polys = g.type === 'Polygon' ? [g.coordinates] : g.type === 'MultiPolygon' ? g.coordinates : [];
    let t = 0;
    for (const p of polys) p.forEach((r, k) => { t += (k === 0 ? 1 : -1) * Math.abs(ringArea(r)); });
    return t;
  }
  function pointInRing(x, y, r) {
    let inside = false;
    for (let i = 0, j = r.length - 1; i < r.length; j = i++) {
      const xi = r[i][0]; const yi = r[i][1]; const xj = r[j][0]; const yj = r[j][1];
      if (((yi > y) !== (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi)) inside = !inside;
    }
    return inside;
  }
  function pointInGeom(x, y, g) {
    const polys = g.type === 'Polygon' ? [g.coordinates] : g.type === 'MultiPolygon' ? g.coordinates : [];
    for (const p of polys) {
      let inside = false;
      for (const r of p) if (pointInRing(x, y, r)) inside = !inside; // even-odd: holes cancel
      if (inside) return true;
    }
    return false;
  }
  function vertexCount(g) {
    let k = 0;
    (function walk(c) { if (typeof c[0] === 'number') k++; else for (const cc of c) walk(cc); })(g.coordinates || []);
    return k;
  }

  // ===========================================================================
  // 5. VectorLayer: one GeoJSON layer (single file, or per-tile files), indexed
  //    once on load. On every view change the grid index yields the features
  //    under the (padded) canvas; they are drawn in style-batched Path2D calls
  //    by a FastCanvas (an L.Canvas subclass, so positioning, padding and zoom
  //    animation are Leaflet's). No Leaflet object is created per feature:
  //    clicks are hit-tested through the index instead.
  // ===========================================================================
  const LABEL_CAP = 250;
  const D2R = Math.PI / 180;
  const TAU = 2 * Math.PI;

  /** L.Canvas whose paths are drawn by an owner (VectorLayer or TileGrid) in one pass. */
  const FastCanvas = L.Canvas.extend({
    initialize(owner, options) {
      this._owner = owner;
      L.Canvas.prototype.initialize.call(this, options);
    },
    // Leaflet calls this after every canvas reset/move (the canvas was just resized and cleared).
    _updatePaths() {
      if (this._postponeUpdatePaths) return;
      this._owner.update(true);
    },
    _redraw() {
      this._redrawRequest = null;
      this._redrawBounds = null;
      if (!this._map || !this._ctx) return;
      this._clear();
      this._draw();
    },
    _draw() {
      if (!this._map || !this._ctx) return;
      this._ctx.save();
      try { this._owner.draw(this._ctx); } finally { this._ctx.restore(); }
    },
    /** Pixel bounds (layer points) the canvas covers, as lat/lng bounds. */
    latLngBounds() {
      const b = this._bounds;
      if (!b || !this._map) return null;
      return L.latLngBounds(this._map.layerPointToLatLng(b.min), this._map.layerPointToLatLng(b.max));
    },
  });

  /** Unrounded Web Mercator (EPSG:3857, as Leaflet) lon/lat -> layer point helpers for the current view. */
  function projector(map) {
    const S = 256 * Math.pow(2, map.getZoom());
    const o = map.getPixelOrigin();
    return {
      x: (lon) => (lon / 360 + 0.5) * S - o.x,
      y: (lat) => (0.5 - Math.atanh(Math.sin(Math.max(-85.0511287798, Math.min(85.0511287798, lat)) * D2R)) / TAU) * S - o.y,
    };
  }
  function parseDash(d) {
    if (!d) return [];
    if (Array.isArray(d)) return d;
    return String(d).split(/[ ,]+/).map(Number).filter((v) => Number.isFinite(v));
  }
  function styleKey(st) {
    return `${st.fill === false ? 0 : 1}|${st.fillColor}|${st.fillOpacity}|${st.stroke === false ? 0 : 1}|${st.color}|` +
      `${st.weight}|${st.opacity}|${st.dashArray || ''}|${st.lineCap || ''}`;
  }
  /** Append a GeoJSON geometry to a batch Path2D; polygons with holes go to `holed` (drawn even-odd). */
  function addGeometry(path, holed, g, P, radius) {
    switch (g.type) {
      case 'Point': {
        const x = P.x(g.coordinates[0]); const y = P.y(g.coordinates[1]);
        path.moveTo(x + radius, y);
        path.arc(x, y, radius, 0, TAU);
        break;
      }
      case 'MultiPoint':
        for (const c of g.coordinates) addGeometry(path, holed, { type: 'Point', coordinates: c }, P, radius);
        break;
      case 'LineString': case 'MultiLineString': {
        const parts = g.type === 'LineString' ? [g.coordinates] : g.coordinates;
        for (const part of parts) {
          for (let k = 0; k < part.length; k++) {
            const x = P.x(part[k][0]); const y = P.y(part[k][1]);
            if (k) path.lineTo(x, y); else path.moveTo(x, y);
          }
        }
        break;
      }
      case 'Polygon': case 'MultiPolygon': {
        const polys = g.type === 'Polygon' ? [g.coordinates] : g.coordinates;
        for (const rings of polys) {
          const target = rings.length > 1 ? new Path2D() : path;
          for (const ring of rings) {
            for (let k = 0; k < ring.length; k++) {
              const x = P.x(ring[k][0]); const y = P.y(ring[k][1]);
              if (k) target.lineTo(x, y); else target.moveTo(x, y);
            }
            target.closePath();
          }
          if (target !== path) holed.push(target);
        }
        break;
      }
      case 'GeometryCollection':
        for (const gg of g.geometries || []) addGeometry(path, holed, gg, P, radius);
        break;
      default: break;
    }
  }
  // Chromium rasterises one huge path super-linearly (40k polygons in one path: ~10 s), while
  // per-feature fills pay a call overhead each: small same-style chunks are fastest (measured
  // in headless Chromium 141: 64 polygons per path, ~0.3 s for 40k filled + stroked buildings).
  const CHUNK_POLY = 64;
  const CHUNK_OTHER = 512;
  function paintGroup(ctx, g) {
    const st = g.st;
    if (st.fill !== false && st.fill !== undefined) {
      ctx.globalAlpha = st.fillOpacity ?? 0.2;
      ctx.fillStyle = st.fillColor || st.color;
      ctx.fill(g.path, 'nonzero');
      for (const h of g.holed) ctx.fill(h, 'evenodd');
    }
    if (st.stroke !== false && (st.weight ?? 1) > 0) {
      ctx.globalAlpha = st.opacity ?? 1;
      ctx.strokeStyle = st.color;
      ctx.lineWidth = st.weight ?? 1;
      ctx.lineCap = st.lineCap || 'round';
      ctx.lineJoin = st.lineJoin || 'round';
      ctx.setLineDash(parseDash(st.dashArray));
      ctx.stroke(g.path);
      for (const h of g.holed) ctx.stroke(h);
    }
  }
  /** Draw features grouped by style, flushing each style's path every few dozen features. */
  function drawBatched(ctx, map, items, geomOf, styleOf) {
    const P = projector(map);
    const groups = new Map();
    for (const i of items) {
      const st = styleOf(i);
      const key = styleKey(st);
      let g = groups.get(key);
      if (!g) { g = { st, path: new Path2D(), holed: [], n: 0 }; groups.set(key, g); }
      const geom = geomOf(i);
      addGeometry(g.path, g.holed, geom, P, st.radius || 4);
      g.n++;
      if (g.n >= (geom.type === 'Polygon' || geom.type === 'MultiPolygon' ? CHUNK_POLY : CHUNK_OTHER)) {
        paintGroup(ctx, g);
        g.path = new Path2D();
        g.holed = [];
        g.n = 0;
      }
    }
    for (const g of groups.values()) if (g.n) paintGroup(ctx, g);
    ctx.globalAlpha = 1;
    ctx.setLineDash([]);
    return groups.size;
  }

  /**
   * ROAD/LINE pieces may carry context points (DATA_FORMATS 1.4): the original vertex just outside
   * the tile, flagged HAS_PREV_CTX / HAS_NEXT_CTX. They are never drawn. Exporters may or may not
   * strip them, so detect geometrically: a flagged end vertex outside the piece's tile square (in
   * NPL-TM84, 5 cm tolerance) is a context point; an already-stripped piece ends on the border.
   */
  function stripContextPoints(f, n, leafLevel) {
    const g = f.geometry;
    if (!g || g.type !== 'LineString' || g.coordinates.length < 3) return 0;
    const prev = n.flags.includes('HAS_PREV_CTX');
    const next = n.flags.includes('HAS_NEXT_CTX');
    if (!prev && !next) return 0;
    const c = g.coordinates;
    let tile = null;
    const m = typeof f.properties.tile === 'string' && f.properties.tile.match(/^(\d+)\/(\d+)\/(\d+)$/);
    if (m) tile = { level: +m[1], tx: +m[2], ty: +m[3] };
    else {
      const a = c[prev ? 1 : 0]; const b = c[prev ? 2 : 1];
      const [x, z] = proj.lonLatToGame((a[0] + b[0]) / 2, (a[1] + b[1]) / 2);
      tile = proj.tileAt(leafLevel, x, z);
    }
    const S = proj.tileSize(tile.level);
    const x0 = tile.tx * S; const z0 = tile.ty * S; const eps = 0.05;
    const outside = (pt) => {
      const [x, z] = proj.lonLatToGame(pt[0], pt[1]);
      return x < x0 - eps || x > x0 + S + eps || z < z0 - eps || z > z0 + S + eps;
    };
    const ctx = {};
    if (prev && outside(c[0])) ctx.prev = c[0];
    if (next && c.length - (ctx.prev ? 1 : 0) > 2 && outside(c[c.length - 1])) ctx.next = c[c.length - 1];
    const k = (ctx.prev ? 1 : 0) + (ctx.next ? 1 : 0);
    if (k) {
      g.coordinates = c.slice(ctx.prev ? 1 : 0, ctx.next ? c.length - 1 : c.length);
      Object.defineProperty(f, '_ctx', { value: ctx, enumerable: false }); // not part of "Copy GeoJSON"
    }
    return k;
  }

  class VectorLayer {
    constructor(app, def) {
      this.app = app;
      this.def = def;
      this.id = def.id;
      this.features = [];
      this.norm = [];
      this.lengths = [];
      this.index = null;
      this.drawList = [];
      this.rendered = new Set(); // feature ids currently drawn
      this.labels = new Map();
      this.state = 'idle'; // idle | loading | ready | missing | error | absent
      this.visible = !!def.defaultOn;
      this.source = null; // {path, count} | {tiles: template, level, count}
      this.message = '';
      this.inView = 0;
      this.capped = false;
      this.tilesLoaded = new Set();
      this.tilesPending = new Set();
      this.renderer = null;
      this.bytes = 0;
      this.loadMs = 0;
      this.epoch = 0; // bumped when styles, filters or data change
      this.ctxStripped = 0; // context points removed from ROAD/LINE pieces
      this._key = '';
      this.lastDraw = { ms: 0, groups: 0, n: 0 };
    }
    get map() { return this.app.map; }
    get count() { return this.features.length; }
    minZoom() { return this.def.minZoom ?? 0; }
    attach() {
      this.renderer = new FastCanvas(this, { pane: this.def.pane, padding: 0.15 });
      this.renderer.addTo(this.app.map);
    }
    setSource(src) {
      this.source = src;
      if (!src) this.state = 'absent';
    }

    /** Start loading once; returns the same promise to every caller (start() awaits in-flight loads). */
    ensureLoaded() {
      if (!this._loadPromise) {
        if (this.state !== 'idle' || !this.source) return Promise.resolve();
        this._loadPromise = this._load();
      }
      return this._loadPromise;
    }

    async _load() {
      if (this.source.tiles) {
        this.state = 'ready';
        this.index = new GridIndex(0.005);
        this.app.scheduleUpdate(); // the next update fetches the tile files under the view
        return;
      }
      this.state = 'loading';
      this.message = 'loading…';
      this.app.refreshLayerList();
      const t0 = performance.now();
      try {
        const fc = await this.app.fetchJSON(this.app.dataUrl(this.source.path), `${this.id}`, (n) => { this.bytes = n; });
        const feats = Array.isArray(fc) ? fc : (fc.features || []);
        const own = this.def.split ? this.def.split(this, feats) : feats;
        this.addFeatures(own, true);
        this.loadMs = performance.now() - t0;
        this.state = 'ready';
        this.message = '';
      } catch (err) {
        this.state = err && err.status === 404 ? 'missing' : 'error';
        this.message = err && err.status === 404 ? 'file not found' : String(err && err.message || err);
        console.warn(`[qa] layer ${this.id}: ${this.message}`);
      }
      this.app.onLayerLoaded(this);
    }

    /** Normalise and index features. bulk=true: the whole layer at once (sorted, cell size fitted). */
    addFeatures(feats, bulk) {
      const def = this.def;
      let items = [];
      for (const f of feats) {
        if (!f || !f.geometry) continue;
        const p = f.properties || (f.properties = {});
        const n = def.normalize(p, f);
        if (def.stripCtx) this.ctxStripped += stripContextPoints(f, n, this.app.leafLevel);
        items.push({ f, n });
      }
      if (bulk && def.sortKey) {
        items = items.map((it, k) => ({ ...it, k, s: def.sortKey(it.n) }))
          .sort((a, b) => (a.s - b.s) || (a.k - b.k));
      }
      const bbs = new Float64Array(items.length * 4);
      let W = Infinity; let S = Infinity; let E = -Infinity; let N = -Infinity;
      items.forEach((it, k) => {
        const b = geomBBox(it.f.geometry);
        bbs.set(b, 4 * k);
        if (b[0] <= b[2]) {
          W = Math.min(W, b[0]); S = Math.min(S, b[1]); E = Math.max(E, b[2]); N = Math.max(N, b[3]);
        }
      });
      if (!this.index) {
        let cell = 0.005;
        if (bulk && items.length && W <= E) {
          const area = Math.max((E - W) * (N - S), 1e-8);
          cell = Math.sqrt(area * 64 / items.length);
          cell = Math.min(0.05, Math.max(0.0005, cell));
        }
        this.index = new GridIndex(cell);
      }
      const isLine = def.lengths;
      items.forEach((it, k) => {
        const i = this.features.length;
        this.features.push(it.f);
        this.norm.push(it.n);
        if (isLine) this.lengths.push(lineLength(it.f.geometry));
        this.index.insert(i, bbs[4 * k], bbs[4 * k + 1], bbs[4 * k + 2], bbs[4 * k + 3]);
      });
      this.epoch++;
      this.app.invalidateStats();
    }

    /** Per-tile sources: fetch the tile files under the view that are not loaded yet. */
    loadTilesInView(bounds) {
      const level = this.source.level ?? this.app.leafLevel;
      const tiles = this.app.tilesInView(level, bounds, 400);
      for (const t of tiles) {
        const id = `${t.level}/${t.tx}/${t.ty}`;
        if (this.tilesLoaded.has(id) || this.tilesPending.has(id)) continue;
        if (this.tilesPending.size >= 8) break;
        this.tilesPending.add(id);
        const path = this.source.tiles.replace(/\{L\}|\{z\}|\{level\}/g, t.level)
          .replace(/\{tx\}|\{x\}/g, t.tx).replace(/\{ty\}|\{y\}/g, t.ty);
        this.app.fetchJSON(this.app.dataUrl(path), `${this.id} ${id}`)
          .then((fc) => this.addFeatures(fc.features || [], false))
          .catch((err) => {
            if (!(err && err.status === 404)) console.warn(`[qa] ${this.id} tile ${id}: ${err && err.message}`);
          })
          .finally(() => {
            this.tilesPending.delete(id);
            this.tilesLoaded.add(id);
            this.app.scheduleUpdate();
          });
      }
    }

    style(i) {
      return this.def.style(this.norm[i], this.features[i], this.app.styleCtx);
    }

    _empty(message, force) {
      this.message = message;
      this.inView = 0;
      if (this.drawList.length || this.labels.size || force) {
        this.drawList = [];
        this.rendered = new Set();
        this._key = '';
        for (const m of this.labels.values()) m.remove();
        this.labels.clear();
        if (this.renderer) this.renderer._redraw();
      }
    }

    /** Recompute what is under the canvas and redraw. force: the canvas was reset by Leaflet (always redraw). */
    update(force) {
      const map = this.map;
      if (!map || !this.renderer || !this.renderer._map) return;
      if (!this.visible) return this._empty('', force);
      const z = map.getZoom();
      if (z < this.minZoom()) {
        return this._empty(this.state === 'idle' && this.def.lazy ? `loads at zoom ≥ ${this.minZoom()}` : `zoom ≥ ${this.minZoom()}`, force);
      }
      if (this.state === 'idle') { this.ensureLoaded(); return this._empty('loading…', force); }
      if (this.state !== 'ready') return this._empty(this.message, force);
      const b = this.renderer.latLngBounds() || map.getBounds().pad(0.15);
      if (this.source && this.source.tiles) this.loadTilesInView(b);
      const key = `${z}|${b.toBBoxString()}|${this.epoch}|${this.count}|${this.app.filterEpoch}`;
      if (!force && key === this._key) return;
      this._key = key;

      const cand = this.index.query(b.getWest(), b.getSouth(), b.getEast(), b.getNorth());
      const hidden = this.app.hidden[this.app.legendKey(this.id)];
      const filterCats = hidden && hidden.size > 0;
      const mzf = this.def.minZoomFor;
      let keep = cand;
      if (filterCats || mzf) {
        keep = [];
        for (const i of cand) {
          const n = this.norm[i];
          if (mzf && z < mzf(n, this.app)) continue;
          if (filterCats && hidden.has(this.app.categoryOf(this.id, n))) continue;
          keep.push(i);
        }
      }
      this.inView = keep.length;
      const cap = this.def.cap ?? 50000;
      this.capped = keep.length > cap;
      if (this.def.sortKey) {
        // arrays are pre-sorted by importance: id order draws/truncates the important ones first
        keep = Uint32Array.from(keep).sort();
        if (this.capped) keep = keep.subarray(0, cap);
      } else if (this.capped) {
        // no importance order (buildings, areas): keep the features nearest the view centre
        const c = map.getCenter();
        const bb = this.index.bbox;
        const kx = Math.cos(c.lat * D2R);
        const dist = new Float64Array(keep.length);
        keep.forEach((i, k) => {
          const dx = ((bb[4 * i] + bb[4 * i + 2]) / 2 - c.lng) * kx; const dy = (bb[4 * i + 1] + bb[4 * i + 3]) / 2 - c.lat;
          dist[k] = dx * dx + dy * dy;
        });
        const order = Uint32Array.from(keep.keys()).sort((a, d) => dist[a] - dist[d]).subarray(0, cap);
        keep = Uint32Array.from(order, (k) => keep[k]).sort(); // id order is spatially coherent (tile order) for drawing
      }
      // otherwise keep the index's cell order: spatially compact chunks rasterise fastest
      this.drawList = keep;
      this.rendered = new Set(keep);
      this.updateLabels(keep, z);
      this.message = this.capped ? `${cap.toLocaleString()} of ${this.inView.toLocaleString()} ` +
        `(${this.def.sortKey ? 'most important' : 'nearest the centre'}; zoom in)` : '';
      this.renderer._redraw();
    }

    /** Called by FastCanvas with a context already translated to layer points. */
    draw(ctx) {
      const list = this.drawList;
      if (!list || !list.length) return;
      const t0 = performance.now();
      // Major roads / important POIs are first in the arrays: draw them last so they end up on top.
      const items = this.def.drawReverse ? Array.from(list).reverse() : list;
      const groups = drawBatched(ctx, this.map, items, (i) => this.features[i].geometry, (i) => this.style(i));
      this.lastDraw = { ms: performance.now() - t0, groups, n: list.length };
    }

    updateLabels(keep, z) {
      const def = this.def;
      if (!def.labelFn || z < (def.labelMinZoom ?? 99)) {
        for (const m of this.labels.values()) m.remove();
        this.labels.clear();
        return;
      }
      const want = new Map();
      for (const i of keep) {
        if (want.size >= LABEL_CAP) break;
        const text = def.labelFn(this.norm[i], this.features[i], z);
        if (text) want.set(i, text);
      }
      for (const [i, m] of this.labels) if (!want.has(i)) { m.remove(); this.labels.delete(i); }
      for (const [i, text] of want) {
        if (this.labels.has(i)) continue;
        const g = this.features[i].geometry;
        if (g.type !== 'Point') continue;
        const m = L.marker([g.coordinates[1], g.coordinates[0]], {
          pane: 'qa-labels', interactive: false, keyboard: false,
          icon: L.divIcon({ className: 'qa-label', html: `<span>${escapeHtml(text)}</span>`, iconSize: null }),
        }).addTo(this.map);
        this.labels.set(i, m);
      }
    }

    restyle() {
      this.epoch++;
      this.update();
    }

    clear() { this._empty('', true); }

    /** Nearest drawn feature to a click: {layer, i, d (px, 0 = inside polygon), area}. */
    hitTest(latlng, tolPx) {
      if (!this.visible || !this.rendered.size) return null;
      const map = this.map;
      const p = map.latLngToLayerPoint(latlng);
      const corner = map.layerPointToLatLng(p.add([tolPx, -tolPx]));
      const dLon = Math.abs(corner.lng - latlng.lng); const dLat = Math.abs(corner.lat - latlng.lat);
      const cand = this.index.query(latlng.lng - dLon, latlng.lat - dLat, latlng.lng + dLon, latlng.lat + dLat);
      let best = null;
      for (const i of cand) {
        if (!this.rendered.has(i)) continue;
        const g = this.features[i].geometry;
        const radius = (g.type === 'Point' || g.type === 'MultiPoint') ? (this.style(i).radius || 0) : 0;
        const d = geomDistancePx(map, g, latlng, p, radius);
        if (d > tolPx) continue;
        const bb = this.index.bbox;
        const area = (bb[4 * i + 2] - bb[4 * i]) * (bb[4 * i + 3] - bb[4 * i + 1]);
        if (!best || d < best.d - 1e-9 || (Math.abs(d - best.d) < 1e-9 && area < best.area)) best = { layer: this, i, d, area };
      }
      return best;
    }
  }

  function geomDistancePx(map, g, latlng, p, radius) {
    switch (g.type) {
      case 'Point': {
        const q = map.latLngToLayerPoint([g.coordinates[1], g.coordinates[0]]);
        return Math.max(0, q.distanceTo(p) - radius);
      }
      case 'MultiPoint':
        return Math.min(...g.coordinates.map((c) => Math.max(0, map.latLngToLayerPoint([c[1], c[0]]).distanceTo(p) - radius)));
      case 'LineString': case 'MultiLineString': {
        const parts = g.type === 'LineString' ? [g.coordinates] : g.coordinates;
        let best = Infinity;
        for (const part of parts) {
          let prev = null;
          for (const c of part) {
            const q = map.latLngToLayerPoint([c[1], c[0]]);
            if (prev) best = Math.min(best, L.LineUtil.pointToSegmentDistance(p, prev, q));
            prev = q;
          }
        }
        return best;
      }
      case 'Polygon': case 'MultiPolygon': {
        if (pointInGeom(latlng.lng, latlng.lat, g)) return 0;
        // near the outline counts too (thin slivers are hard to hit)
        const rings = g.type === 'Polygon' ? g.coordinates : g.coordinates.flat();
        return geomDistancePx(map, { type: 'MultiLineString', coordinates: rings }, latlng, p, 0) + 2;
      }
      case 'GeometryCollection':
        return Math.min(Infinity, ...(g.geometries || []).map((gg) => geomDistancePx(map, gg, latlng, p, radius)));
      default: return Infinity;
    }
  }

  // ===========================================================================
  // 6. RasterTiles: per-tile PNGs (hillshade, biome). A quadtree tile is a
  //    square in NPL-TM84, i.e. a slightly rotated quad in lon/lat (about 0.6 deg
  //    of grid convergence at Kathmandu), so each image is placed with a CSS
  //    affine matrix from its NW/NE/SW corners instead of an axis-aligned
  //    L.imageOverlay. Only tiles near the view are in the DOM.
  // ===========================================================================
  const RasterTiles = L.Layer.extend({
    options: { pane: 'qa-biome', opacity: 0.6, registration: 'auto', row0: 'north', pixelated: false },

    initialize(kind, tiles, options) {
      L.setOptions(this, options);
      this.kind = kind;
      this.tiles = tiles; // [{id, url, corners: [[lon,lat] sw, se, ne, nw], bounds: L.LatLngBounds}]
      this._els = new Map();
      this.stats = { loaded: 0, missing: 0 };
    },
    onAdd(map) {
      this._container = L.DomUtil.create('div', `qa-raster qa-raster-${this.kind}`);
      if (this._zoomAnimated) L.DomUtil.addClass(this._container, 'leaflet-zoom-animated');
      if (this.options.pixelated) L.DomUtil.addClass(this._container, 'qa-pixelated');
      this._container.style.opacity = this.options.opacity;
      this.getPane().appendChild(this._container);
      this._reset();
    },
    onRemove() {
      L.DomUtil.remove(this._container);
      for (const el of this._els.values()) el.remove();
    },
    getEvents() {
      const ev = { viewreset: this._reset, zoom: this._reset, moveend: this._update };
      if (this._zoomAnimated) ev.zoomanim = this._animateZoom;
      return ev;
    },
    setOpacity(o) {
      this.options.opacity = o;
      if (this._container) this._container.style.opacity = o;
    },
    _reset() {
      if (!this._map) return;
      this._origin = this._map.getPixelOrigin();
      this._zoom = this._map.getZoom();
      L.DomUtil.setTransform(this._container, L.point(0, 0), 1);
      for (const el of this._els.values()) if (el.isConnected && el.naturalWidth) this._position(el);
      this._update();
    },
    _animateZoom(e) {
      const scale = this._map.getZoomScale(e.zoom, this._zoom);
      const offset = this._origin.multiplyBy(scale).subtract(this._map._getNewPixelOrigin(e.center, e.zoom));
      L.DomUtil.setTransform(this._container, offset, scale);
    },
    _update() {
      if (!this._map) return;
      const view = this._map.getBounds().pad(0.3);
      for (const t of this.tiles) {
        let el = this._els.get(t.id);
        if (view.intersects(t.bounds)) {
          if (!el) {
            el = new Image();
            el.className = 'qa-raster-tile';
            el.alt = '';
            el.decoding = 'async';
            el._tile = t;
            el.onload = () => { this.stats.loaded++; this._position(el); };
            el.onerror = () => { this.stats.missing++; el._missing = true; el.style.display = 'none'; };
            el.src = t.url;
            this._els.set(t.id, el); // kept when off-screen so it is not fetched again
          }
          if (!el.isConnected && !el._missing) {
            this._container.appendChild(el);
            if (el.naturalWidth) this._position(el);
          }
        } else if (el && el.isConnected) {
          el.remove();
        }
      }
    },
    /** CSS matrix mapping image pixels to layer points (see README "Raster placement"). */
    _position(el) {
      const map = this._map;
      if (!map) return;
      const t = el._tile;
      const w = el.naturalWidth; const h = el.naturalHeight;
      const lp = (c) => map.project([c[1], c[0]]).subtract(map.getPixelOrigin()); // unrounded, like the vector canvas
      const [sw, se, ne, nw] = t.corners.map(lp);
      const north = this.options.row0 !== 'south';
      const o = north ? nw : sw; const u = north ? ne : se; const v = north ? sw : nw;
      let reg = this.options.registration;
      if (reg === 'auto') reg = (w % 2 === 1) ? 'vertex' : 'cell';
      const du = reg === 'vertex' ? Math.max(1, w - 1) : w;
      const dv = reg === 'vertex' ? Math.max(1, h - 1) : h;
      const off = reg === 'vertex' ? 0.5 : 0;
      const a = (u.x - o.x) / du; const b = (u.y - o.y) / du;
      const c = (v.x - o.x) / dv; const d = (v.y - o.y) / dv;
      const e = o.x - off * (a + c); const f = o.y - off * (b + d);
      el.style.width = w + 'px';
      el.style.height = h + 'px';
      el.style.transform = `matrix(${a},${b},${c},${d},${e},${f})`;
      el._m = [a, b, c, d, e, f];
      el._reg = reg;
    },
    /** Pixel under a lat/lng from the tiles in the DOM: {tile, px, py, rgba} or null. */
    sample(latlng) {
      if (!this._map) return null;
      const p = this._map.project(latlng).subtract(this._map.getPixelOrigin());
      for (const el of this._els.values()) {
        if (!el.isConnected || !el._m || el._missing) continue;
        const [a, b, c, d, e, f] = el._m;
        const det = a * d - b * c;
        if (!det) continue;
        const x = p.x - e; const y = p.y - f;
        const px = (d * x - c * y) / det; const py = (-b * x + a * y) / det;
        const w = el.naturalWidth; const h = el.naturalHeight;
        if (px < 0 || py < 0 || px >= w || py >= h) continue;
        if (!el._data) {
          const cv = document.createElement('canvas');
          cv.width = w; cv.height = h;
          const ctx = cv.getContext('2d', { willReadFrequently: true });
          ctx.drawImage(el, 0, 0);
          el._data = ctx.getImageData(0, 0, w, h).data;
        }
        const ix = Math.floor(px); const iy = Math.floor(py);
        const k = 4 * (iy * w + ix);
        const dd = el._data;
        return { tile: el._tile.id, px: ix, py: iy, w, h, rgba: [dd[k], dd[k + 1], dd[k + 2], dd[k + 3]], reg: el._reg };
      }
      return null;
    },
    countInDom() {
      let k = 0;
      for (const el of this._els.values()) if (el.isConnected && el.naturalWidth) k++;
      return k;
    },
  });

  // ===========================================================================
  // 7. Tile grid overlay (quads of one quadtree level, with L/tx/ty labels)
  // ===========================================================================
  class TileGrid {
    constructor(app) {
      this.app = app;
      this.visible = false;
      this.level = null;
      this.renderer = null;
      this.tiles = [];
      this.labels = new Map();
      this.inView = 0;
      this.message = '';
      this._key = '';
    }
    get rendered() { return { size: this.tiles.length }; }
    attach() {
      this.renderer = new FastCanvas(this, { pane: 'qa-grid', padding: 0.15 });
      this.renderer.addTo(this.app.map);
    }
    update(force) {
      const map = this.app.map;
      if (!this.renderer || !this.renderer._map) return;
      if (!this.visible || this.level === null) { this.clear(); return; }
      const b = this.renderer.latLngBounds() || map.getBounds().pad(0.15);
      const key = `${map.getZoom()}|${b.toBBoxString()}|${this.level}`;
      if (!force && key === this._key) return;
      this._key = key;
      const tiles = this.app.tilesInView(this.level, b, 3000);
      this.tiles = tiles;
      this.inView = tiles.length;
      this.message = !tiles.length && !this.app.tilesByLevel.has(this.level) ? 'too many tiles: zoom in' : '';
      this.renderer._redraw();
      // labels when a tile is at least ~110 px wide
      let showLabels = false;
      const t0 = tiles[0];
      if (t0) {
        const a = map.latLngToContainerPoint([t0.corners[0][1], t0.corners[0][0]]);
        const c = map.latLngToContainerPoint([t0.corners[1][1], t0.corners[1][0]]);
        showLabels = a.distanceTo(c) >= 110 && tiles.length <= 300;
      }
      const want = showLabels ? new Map(tiles.map((t) => [`${t.level}/${t.tx}/${t.ty}`, t])) : new Map();
      for (const [id, m] of this.labels) if (!want.has(id)) { m.remove(); this.labels.delete(id); }
      for (const [id, t] of want) {
        if (this.labels.has(id)) continue;
        const nw = t.corners[3];
        const m = L.marker([nw[1], nw[0]], { pane: 'qa-labels', interactive: false, keyboard: false,
          icon: L.divIcon({ className: 'qa-tile-label', html: `<span>${id}${t.listed ? '' : ' (not in index)'}</span>`,
            iconSize: null }) }).addTo(map);
        this.labels.set(id, m);
      }
    }
    draw(ctx) {
      if (!this.tiles.length) return;
      const quad = (i) => ({ type: 'Polygon', coordinates: [[...this.tiles[i].corners, this.tiles[i].corners[0]]] });
      const style = (i) => (this.tiles[i].listed
        ? { fill: false, color: '#0b57d0', weight: 1.6, opacity: 0.85 }
        : { fill: false, color: '#7f8c8d', weight: 1, opacity: 0.85, dashArray: '4 4' });
      drawBatched(ctx, this.app.map, this.tiles.map((t, i) => i), quad, style);
    }
    clear() {
      this.tiles = [];
      this.inView = 0;
      this._key = '';
      if (this.renderer && this.renderer._map) this.renderer._redraw();
      for (const m of this.labels.values()) m.remove();
      this.labels.clear();
    }
  }

  // ===========================================================================
  // 8. Layer definitions, styles and the App (UI, legend, stats, inspector,
  //    search, URL hash, status bar)
  // ===========================================================================
  const CLASS_RANK = Object.fromEntries(CLASS_ORDER.map((c, k) => [c, k]));

  function roadStyle(n, f, ctx) {
    const zs = ctx.zs;
    let weight = (CLASS_WIDTH[n.cls] ?? 3) * zs;
    let color;
    let opacity = 0.95;
    let dash = null;
    switch (ctx.roadMode) {
      case 'surface': color = SURFACE_COLORS[n.surface] || UNKNOWN_COLOR; break;
      case 'class': color = CLASS_COLORS[n.cls] || UNKNOWN_COLOR; break;
      case 'sac':
        if (n.trail || n.sac !== 'UNKNOWN') {
          color = SAC_COLORS[n.sac] || UNKNOWN_COLOR;
          weight = Math.max(weight, 3.5 * zs);
          if (n.sacInferred) dash = '7 5';
        } else {
          color = '#9aa0a6';
          opacity = 0.45;
        }
        break;
      default: color = SOURCE_COLORS[n.source] || UNKNOWN_COLOR;
    }
    if (n.trail && ctx.roadMode !== 'sac') dash = '5 3';
    if (hasFlag(n, 'TUNNEL')) { dash = '2 4'; opacity *= 0.6; }
    return { color, weight: Math.max(1, weight), opacity, dashArray: dash, lineCap: dash ? 'butt' : 'round',
      lineJoin: 'round', fill: false };
  }
  function buildingCategory(n, mode) {
    if (mode === 'levels') return levelBucket(n.levels);
    if (mode === 'levels_source') return n.levelsInferred ? 'INFERRED' : 'TAGGED';
    return n.arche;
  }
  function buildingStyle(n, f, ctx) {
    const cat = buildingCategory(n, ctx.buildingMode);
    const pal = ctx.buildingMode === 'levels' ? LEVEL_COLORS
      : ctx.buildingMode === 'levels_source' ? LEVELS_SOURCE_COLORS : ARCHETYPE_COLORS;
    const landmark = hasFlag(n, 'LANDMARK');
    // outlines below z16 would be sub-pixel: skip them (halves raster work in dense towns)
    return { fillColor: pal[cat] || UNKNOWN_COLOR, fillOpacity: 0.8, fill: true, color: landmark ? '#000' : '#2b2b2b',
      weight: landmark ? 2 : ctx.z >= 16 ? 0.6 : 0, opacity: landmark ? 1 : 0.55 };
  }
  function areaStyle(n) {
    const c = AREA_COLORS[n.kind] || UNKNOWN_COLOR;
    if (n.kind === 'PROTECTED') return { color: c, weight: 2, opacity: 0.8, dashArray: '6 4', fill: true, fillColor: c, fillOpacity: 0.05 };
    return { color: c, weight: 1, opacity: 0.75, fill: true, fillColor: c, fillOpacity: 0.35, dashArray: null };
  }
  function lineStyle(n, f, ctx) {
    const s = LINE_STYLES[n.kind] || LINE_STYLES.NONE;
    const st = { color: s.color, weight: s.weight * Math.max(0.7, ctx.zs), opacity: 0.9, dashArray: s.dashArray || null,
      fill: false, lineCap: 'round' };
    if (hasFlag(n, 'INTERMITTENT')) st.dashArray = '6 4';
    if (hasFlag(n, 'TUNNEL')) { st.dashArray = '2 4'; st.opacity = 0.5; }
    return st;
  }
  function poiStyle(n) {
    const landmark = hasFlag(n, 'LANDMARK');
    return { radius: 3.5 + Math.min(255, n.importance) / 255 * 4.5, fillColor: (POI_GROUPS[n.group] || POI_GROUPS.other).color,
      fillOpacity: 0.95, fill: true, color: landmark ? '#000' : '#fff', weight: landmark ? 2.5 : 1.5, opacity: 1 };
  }
  function placeStyle() {
    return { radius: 6, fillColor: POI_GROUPS.place.color, fillOpacity: 0.9, fill: true, color: '#fff', weight: 2, opacity: 1 };
  }
  function displayName(f) {
    const nm = featureNames(f.properties || {});
    return nm.def || nm.en || nm.ne || '';
  }

  function splitTrails(layer, feats) {
    // roads.geojson may carry the trail classes itself when no trails.geojson is exported.
    const trails = layer.app.layers.trails;
    if (!trails || trails.state !== 'absent') return feats;
    const own = []; const tr = [];
    for (const f of feats) {
      const cls = normEnum('RoadClass', prop((f && f.properties) || {}, 'road_class', 'class', 'highway', 'cls'));
      (TRAIL_CLASSES.has(cls) ? tr : own).push(f);
    }
    trails.source = { path: null, derived: 'roads' };
    trails.addFeatures(tr, true);
    trails.state = 'ready';
    trails.message = 'split from roads.geojson';
    return own;
  }

  const LAYER_DEFS = [
    { id: 'roads', label: 'Roads', pane: 'qa-roads', defaultOn: true, cap: 60000, lengths: true, drawReverse: true, stripCtx: true,
      normalize: normRoad, style: roadStyle, sortKey: (n) => CLASS_RANK[n.cls] ?? 99,
      minZoomFor: (n) => CLASS_MINZOOM[n.cls] ?? 13, split: splitTrails },
    { id: 'trails', label: 'Trails', pane: 'qa-trails', defaultOn: true, cap: 40000, minZoom: 12, lengths: true, drawReverse: true,
      stripCtx: true,
      normalize: normRoad, style: roadStyle, sortKey: (n) => CLASS_RANK[n.cls] ?? 99 },
    { id: 'buildings', label: 'Buildings', pane: 'qa-buildings', defaultOn: true, cap: 30000, minZoom: 15, lazy: true,
      normalize: normBuilding, style: buildingStyle },
    { id: 'areas', label: 'Areas', pane: 'qa-areas', defaultOn: true, cap: 20000, normalize: normArea, style: areaStyle },
    { id: 'lines', label: 'Lines (water, rail, walls)', pane: 'qa-lines', defaultOn: true, cap: 20000, lengths: true, stripCtx: true,
      normalize: normLine, style: lineStyle },
    { id: 'pois', label: 'POIs', pane: 'qa-pois', defaultOn: true, cap: 6000, drawReverse: true, normalize: normPoi, style: poiStyle,
      sortKey: (n) => -n.importance, labelMinZoom: 16,
      minZoomFor: (n) => (hasFlag(n, 'LANDMARK') || n.importance >= 180 ? 0 : n.importance >= 100 ? 13 : 14),
      labelFn: (n, f, z) => ((z >= 17 || n.importance >= 60 || hasFlag(n, 'LANDMARK')) ? displayName(f) : null) },
    { id: 'places', label: 'Places', pane: 'qa-places', defaultOn: true, optional: true, cap: 3000, drawReverse: true,
      normalize: normPlace, style: placeStyle, sortKey: (n) => -n.importance, labelMinZoom: 12,
      labelFn: (n, f) => displayName(f) },
  ];
  const SPEC_LAYERS = ['roads', 'trails', 'buildings', 'areas', 'lines', 'pois'];
  const RASTER_IDS = ['hillshade', 'biome'];
  const ALL_TOGGLES = ['roads', 'trails', 'buildings', 'areas', 'lines', 'pois', 'places', 'hillshade', 'biome', 'grid'];

  const PROP_ENUMS = {
    road_class: 'RoadClass', surface: 'Surface', surface_source: 'SurfaceSource', surface_group: 'SurfaceGroup',
    sac_scale: 'SacScale', archetype: 'BuildingArchetype', use: 'BuildingUse', roof_shape: 'RoofShape',
    roof_material: 'RoofMaterial', wall_material: 'WallMaterial', access: 'Travel',
  };
  const LAYER_KIND_ENUM = { pois: 'PoiKind', places: 'PlaceKind', areas: 'AreaKind', lines: 'LineKind' };
  const LAYER_FLAG_ENUM = { roads: 'RoadFlags', trails: 'RoadFlags', buildings: 'BuildingFlags', pois: 'PoiFlags',
    areas: 'AreaFlags', lines: 'LineFlags' };

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  }
  function fmtNum(v, d = 0) {
    return Number(v).toLocaleString('en-US', { maximumFractionDigits: d, minimumFractionDigits: d });
  }
  function fmtLen(m) { return m >= 1000 ? `${fmtNum(m / 1000, 2)} km` : `${fmtNum(m, 0)} m`; }
  function fmtBytes(b) { return b >= 1048576 ? `${fmtNum(b / 1048576, 1)} MB` : `${fmtNum(b / 1024, 0)} KB`; }
  function fold(s) { return String(s || '').normalize('NFKD').replace(/[̀-ͯ]/g, '').toLowerCase(); }
  function hexOf(rgb) { return '#' + rgb.slice(0, 3).map((v) => v.toString(16).padStart(2, '0')).join(''); }
  function parseColor(c) {
    if (Array.isArray(c)) return hexOf(c.map(Number));
    if (c && typeof c === 'object') return parseColor(c.color ?? c.colour ?? c.hex ?? c.rgb);
    if (typeof c !== 'string') return null;
    const s = c.trim();
    if (/^#?[0-9a-f]{6}$/i.test(s)) return (s[0] === '#' ? s : '#' + s).toLowerCase();
    if (/^#?[0-9a-f]{3}$/i.test(s)) { const h = s.replace('#', ''); return ('#' + h[0] + h[0] + h[1] + h[1] + h[2] + h[2]).toLowerCase(); }
    const m = s.match(/^rgba?\(([^)]+)\)$/i);
    if (m) return hexOf(m[1].split(',').map((x) => parseInt(x, 10)));
    return null;
  }
  function osmRefOf(layerId, p) {
    const typeNames = { n: 'node', w: 'way', r: 'relation', node: 'node', way: 'way', relation: 'relation' };
    const t = prop(p, 'osm_type');
    const id = num(prop(p, 'osm_id'));
    if (id !== null && t !== undefined && typeNames[String(t).toLowerCase()]) return { type: typeNames[String(t).toLowerCase()], id };
    if (layerId === 'roads' || layerId === 'trails' || layerId === 'lines') {
      const w = num(prop(p, 'osm_way_id', 'osm_id', 'way_id'));
      return w !== null ? { type: 'way', id: w } : null;
    }
    const ref = num(prop(p, 'osm_ref'));
    if (ref === null) return id !== null ? { type: layerId === 'pois' || layerId === 'places' ? 'node' : 'way', id } : null;
    if (layerId === 'pois' || layerId === 'places') return { type: ['node', 'way', 'relation', 'node'][ref % 4], id: Math.floor(ref / 4) };
    return { type: ref % 2 ? 'relation' : 'way', id: Math.floor(ref / 2) };
  }

  class App {
    constructor() {
      this.params = new URLSearchParams(location.search);
      this.layers = {};
      this.rasters = {};
      this.grid = new TileGrid(this);
      this.roadMode = 'source';
      this.buildingMode = 'archetype';
      this.base = 'osm';
      this.baseOpacity = 0.8;
      this.rasterOpacity = { hillshade: 0.6, biome: 0.55 };
      this.rasterVisible = { hillshade: false, biome: false };
      this.hidden = {};
      this.filterEpoch = 0;
      this.pending = 0;
      this.pendingLabels = new Map();
      this.idleWaiters = [];
      this.index = null;
      this.indexTiles = [];
      this.tilesByLevel = new Map();
      this.leafLevel = 10;
      this.biomePalette = null; // [{name, color}]
      this.styleCtx = { z: 0, zs: 1, roadMode: this.roadMode, buildingMode: this.buildingMode };
      this.errors = [];
      this._catStats = {};
      this._search = null;
      this._lastHash = '';
      this._updateQueued = false;
      this.selected = null;
      this.readyResolve = null;
      this.ready = new Promise((r) => { this.readyResolve = r; });
    }

    // ---------------------------------------------------------------- loading
    dataUrl(path) { return new URL(path, this.dataBase).href; }

    async fetchJSON(url, label, onBytes) {
      this.pending++;
      this.pendingLabels.set(url, { label, bytes: 0 });
      this.renderStatus();
      try {
        const res = await fetch(url, { cache: 'no-cache' });
        if (!res.ok) { const e = new Error(`${res.status} ${res.statusText} for ${url}`); e.status = res.status; throw e; }
        let text;
        if (res.body && res.body.getReader) {
          const reader = res.body.getReader();
          const dec = new TextDecoder();
          const parts = [];
          let n = 0;
          let last = 0;
          for (;;) {
            const { done, value } = await reader.read();
            if (done) break;
            n += value.length;
            parts.push(dec.decode(value, { stream: true }));
            if (n - last > 2e6) {
              last = n;
              this.pendingLabels.set(url, { label, bytes: n });
              this.renderStatus();
            }
          }
          parts.push(dec.decode());
          text = parts.join('');
          if (onBytes) onBytes(n);
        } else {
          text = await res.text();
          if (onBytes) onBytes(text.length);
        }
        this.pendingLabels.set(url, { label: label + ' (parsing)', bytes: text.length });
        this.renderStatus();
        await new Promise((r) => setTimeout(r, 0)); // let the status paint before a long parse
        return JSON.parse(text);
      } finally {
        this.pending--;
        this.pendingLabels.delete(url);
        this.renderStatus();
        if (this.pending === 0) setTimeout(() => this._checkIdle(), 0);
      }
    }
    _checkIdle() {
      if (this.pending !== 0) return;
      const w = this.idleWaiters;
      this.idleWaiters = [];
      w.forEach((fn) => fn());
    }
    /** Resolves once nothing is loading (after the next layer update). */
    idle() {
      return new Promise((resolve) => {
        const check = () => {
          if (this.pending === 0) requestAnimationFrame(() => (this.pending === 0 ? resolve() : this.idleWaiters.push(check)));
          else this.idleWaiters.push(check);
        };
        check();
      });
    }

    async start() {
      this.setupMap();
      this.bindUi();
      let listing = null;
      try {
        const r = await fetch('/__qa/datasets.json', { cache: 'no-cache' });
        if (r.ok) listing = await r.json();
      } catch { /* not served by serve.py: no listing */ }
      this.listing = listing;
      if (listing && listing.enums) await this.loadEnums(listing.enums);

      let data = this.params.get('data');
      const region = this.params.get('region');
      if (!data && region) data = `../../pipeline/build/regions/${region}/qa`;
      if (!data) {
        const real = listing && (listing.datasets || []).find((d) => !d.sample);
        data = real ? real.data : 'sample/qa';
      }
      this.dataParam = data;
      this.dataBase = new URL(data.replace(/\/?$/, '/'), location.href).href;
      this.indexName = this.params.get('index') || 'index.json';
      this.fillDatasetPicker();

      try {
        this.index = await this.fetchJSON(this.dataUrl(this.indexName), this.indexName);
      } catch (err) {
        this.fail(`Could not load ${this.dataUrl(this.indexName)} (${err.message || err}). ` +
          'Build a QA export (pipeline build.py --qa writes pipeline/build/regions/<region>/qa/) ' +
          'or try ?data=sample/qa.');
        this.applyHash(this.parseHash(location.hash), true); // still honour the view and base=… of the link
        if (!this.baseLayer && this.base !== 'none') this.setBase(this.base);
        this.readyResolve();
        return;
      }
      this.applyIndex();
      const hash = this.parseHash(location.hash);
      this.applyHash(hash, true);
      if (!hash) this.fitRegion();
      if (!this.baseLayer && this.base !== 'none') this.setBase(this.base);
      this.styleCtx.z = this.map.getZoom();
      this.styleCtx.zs = zoomScale(this.styleCtx.z);
      this.refreshLayerList();
      this.renderLegend();
      this.renderStats();
      const loads = [];
      for (const l of Object.values(this.layers)) if (!l.def.lazy) loads.push(l.ensureLoaded());
      await Promise.all(loads);
      this.updateAll();
      this.writeHash();
      window.addEventListener('hashchange', () => {
        if (location.hash !== this._lastHash) this.applyHash(this.parseHash(location.hash), false);
      });
      this.readyResolve();
    }

    async loadEnums(path) {
      try {
        const r = await fetch(path, { cache: 'no-cache' });
        if (!r.ok) return;
        const j = await r.json();
        for (const [name, vals] of Object.entries(j.enums || {})) {
          ENUMS[name] = { ...(ENUMS[name] || {}), ...vals };
          delete _byValue[name];
        }
        this.enumsVersion = j.version;
      } catch (e) { console.warn('[qa] enums.json not loaded:', e); }
    }

    fail(msg) {
      this.errors.push(msg);
      const b = document.getElementById('banner');
      b.hidden = false;
      b.textContent = msg;
      document.getElementById('dataset-name').textContent = 'no data';
      console.error('[qa] ' + msg);
    }

    applyIndex() {
      const idx = this.index;
      // --- tiles
      const rawTiles = Array.isArray(idx.tiles) ? idx.tiles
        : idx.tiles && typeof idx.tiles === 'object' ? Object.entries(idx.tiles).map(([k, v]) => ({ id: k, ...(v || {}) })) : [];
      const tiles = [];
      for (const t of rawTiles) {
        const tt = this.normTile(t);
        if (tt) tiles.push(tt);
      }
      this.indexTiles = tiles;
      this.tilesByLevel = new Map();
      for (const t of tiles) {
        if (!this.tilesByLevel.has(t.level)) this.tilesByLevel.set(t.level, []);
        this.tilesByLevel.get(t.level).push(t);
      }
      const det = Array.isArray(idx.detail_levels) ? idx.detail_levels.map(Number) : [];
      this.leafLevel = num(idx.leaf_level) ?? (det.length ? Math.max(...det) : null) ??
        (tiles.length ? Math.max(...tiles.map((t) => t.level)) : 10);

      // --- layers
      this.layerSources = this.normLayers(idx.layers);
      const map = this.map;
      for (const def of LAYER_DEFS) {
        const l = new VectorLayer(this, def);
        if (def.id === 'buildings') {
          l.def = { ...def, minZoom: num(this.params.get('bmin')) ?? def.minZoom, cap: num(this.params.get('bcap')) ?? def.cap };
        }
        l.attach();
        l.setSource(this.layerSources.get(def.id) || null);
        this.layers[def.id] = l;
      }
      // --- rasters
      const rasterCfg = idx.raster || idx.rasters || {};
      const explicit = tiles.some((t) => t.hillshade !== undefined || t.biome !== undefined);
      for (const kind of RASTER_IDS) {
        const cfg = rasterCfg[kind] || {};
        const list = [];
        for (const t of tiles) {
          let path = t[kind];
          if (!explicit && t.level === this.leafLevel) path = `${kind}/${t.level}/${t.tx}_${t.ty}.png`;
          if (!path || path === true) {
            if (path === true) path = `${kind}/${t.level}/${t.tx}_${t.ty}.png`; else continue;
          }
          list.push({ id: t.id, url: this.dataUrl(path), corners: t.corners, bounds: t.llBounds });
        }
        const layer = new RasterTiles(kind, list, {
          pane: kind === 'hillshade' ? 'qa-hillshade' : 'qa-biome',
          opacity: this.rasterOpacity[kind],
          registration: cfg.registration || 'auto',
          row0: cfg.row0 || 'north',
          pixelated: kind === 'biome',
        });
        this.rasters[kind] = layer;
      }
      this.biomePalette = this.normPalette(idx.biome_palette ?? (idx.palette && idx.palette.biome) ??
        (idx.palettes && idx.palettes.biome) ?? idx.biome_colors ?? idx.biomes);
      // --- grid
      this.grid.attach();
      const levels = [...new Set([...this.tilesByLevel.keys(), this.leafLevel])].sort((a, b) => a - b);
      const sel = document.getElementById('grid-level');
      sel.innerHTML = levels.map((L_) => `<option value="${L_}">level ${L_} (${fmtNum(proj.tileSize(L_))} m)` +
        `${this.tilesByLevel.has(L_) ? ` · ${this.tilesByLevel.get(L_).length} tiles` : ' · computed'}</option>`).join('');
      this.grid.level = this.leafLevel;
      sel.value = String(this.leafLevel);
      // --- header + attribution
      const nm = idx.name && typeof idx.name === 'object' ? (idx.name.en || idx.name.ne) : idx.name;
      const title = `${idx.region || 'region'}${nm ? ' · ' + nm : ''}`;
      document.getElementById('dataset-name').innerHTML = escapeHtml(title) +
        (idx.synthetic ? ' <span class="badge">synthetic</span>' : '');
      document.title = `QA · ${idx.region || 'Ghumante'}`;
      const attr = Array.isArray(idx.attribution) ? idx.attribution : idx.attribution ? [idx.attribution] : [];
      for (const a of attr) map.attributionControl.addAttribution(escapeHtml(a));
    }

    normTile(t) {
      if (typeof t === 'string') t = { id: t };
      let level = num(t.level ?? t.L ?? t.z);
      let tx = num(t.tx ?? t.x);
      let ty = num(t.ty ?? t.y);
      const idStr = t.id || t.tile || t.name;
      if ((level === null || tx === null || ty === null) && typeof idStr === 'string') {
        const m = idStr.match(/(\d+)\D+(\d+)\D+(\d+)/);
        if (m) { level = +m[1]; tx = +m[2]; ty = +m[3]; }
      }
      if (level === null || tx === null || ty === null) return null;
      let corners = t.corners_lonlat || t.corners;
      if (corners && !Array.isArray(corners)) corners = [corners.sw, corners.se, corners.ne, corners.nw];
      if (!(Array.isArray(corners) && corners.length === 4 && corners.every((c) => Array.isArray(c) && c.length >= 2))) {
        corners = null;
      }
      // Without explicit corners, the exact corners come from the projection (identity warp, M0/M1);
      // an axis-aligned bbox alone would misplace a rotated tile by ~10 m at the edges.
      if (!corners) {
        const sm = this.index && this.index.scale_model;
        const bb = this.normBBox(t.bounds_lonlat ?? t.bbox_lonlat ?? t.lonlat_bounds ?? t.bounds ?? t.bbox);
        corners = (!sm || sm === 'identity' || !bb) ? proj.tileCorners(level, tx, ty)
          : [[bb[0], bb[1]], [bb[2], bb[1]], [bb[2], bb[3]], [bb[0], bb[3]]];
      }
      corners = corners.map((c) => [Number(c[0]), Number(c[1])]);
      const lons = corners.map((c) => c[0]); const lats = corners.map((c) => c[1]);
      const llBounds = L.latLngBounds([Math.min(...lats), Math.min(...lons)], [Math.max(...lats), Math.max(...lons)]);
      const fileOf = (k) => {
        const v = t[k] ?? (t.files && t.files[k]) ?? (t.rasters && t.rasters[k]);
        return v === false || v === null ? null : v;
      };
      return { id: `${level}/${tx}/${ty}`, level, tx, ty, key: t.key !== undefined ? String(t.key) : null,
        corners, llBounds, listed: true, raw: t, hillshade: fileOf('hillshade'), biome: fileOf('biome') };
    }

    normBBox(b) {
      if (!b) return null;
      if (Array.isArray(b) && b.length >= 4) return b.slice(0, 4).map(Number);
      if (typeof b === 'object') {
        const w = b.west ?? b.min_lon ?? b.minLon ?? b.lon_min; const s = b.south ?? b.min_lat ?? b.minLat ?? b.lat_min;
        const e = b.east ?? b.max_lon ?? b.maxLon ?? b.lon_max; const n = b.north ?? b.max_lat ?? b.maxLat ?? b.lat_max;
        if ([w, s, e, n].every((v) => v !== undefined)) return [w, s, e, n].map(Number);
      }
      return null;
    }

    normLayers(spec) {
      const out = new Map();
      const add = (name, v) => {
        if (!name) return;
        name = String(name).replace(/\.geojson$/, '');
        if (v === false || v === null) return;
        let s;
        if (typeof v === 'string') s = { path: v };
        else if (typeof v === 'number') s = { path: `${name}.geojson`, count: v };
        else if (v && typeof v === 'object') {
          s = { path: v.path || v.file || v.url || null, count: num(v.count ?? v.features), tiles: v.tiles || v.tile_template || v.per_tile || null,
            level: num(v.level), bytes: num(v.bytes) };
          if (s.tiles === true) s.tiles = `${name}/{L}/{tx}_{ty}.geojson`;
          if (!s.path && !s.tiles) s.path = `${name}.geojson`;
        } else s = { path: `${name}.geojson` };
        out.set(name, s);
      };
      if (Array.isArray(spec)) {
        for (const v of spec) {
          if (typeof v === 'string') add(v, `${v.replace(/\.geojson$/, '')}.geojson`);
          else if (v && typeof v === 'object') add(v.name || v.id || v.layer, v);
        }
      } else if (spec && typeof spec === 'object') {
        for (const [k, v] of Object.entries(spec)) add(k, v === true ? `${k}.geojson` : v);
      } else {
        for (const k of SPEC_LAYERS) add(k, `${k}.geojson`); // no list: try the spec names
      }
      return out;
    }

    normPalette(p) {
      if (!p) return null;
      const out = [];
      const nameOf = (k) => (/^\d+$/.test(String(k)) ? (byValue('Biome').get(Number(k)) || `#${k}`) : normKey(k));
      if (Array.isArray(p)) {
        for (const e of p) {
          if (!e) continue;
          const name = e.name ? normKey(e.name) : nameOf(e.id ?? e.value);
          const color = parseColor(e.color ?? e.colour ?? e.rgb ?? e.hex);
          if (color) out.push({ name, color });
        }
      } else if (typeof p === 'object') {
        for (const [k, v] of Object.entries(p)) {
          const color = parseColor(v);
          if (color) out.push({ name: nameOf(k), color });
        }
      }
      out.sort((a, b) => (enumValue('Biome', a.name) ?? 999) - (enumValue('Biome', b.name) ?? 999));
      return out.length ? out : null;
    }

    fitRegion() {
      const bb = this.normBBox(this.index.bbox_lonlat ?? this.index.bbox ?? this.index.region_bbox ?? this.index.bounds);
      if (bb) this.map.fitBounds([[bb[1], bb[0]], [bb[3], bb[2]]]);
      else if (this.indexTiles.length) {
        const b = L.latLngBounds([]);
        this.indexTiles.forEach((t) => b.extend(t.llBounds));
        this.map.fitBounds(b);
      } else this.map.setView([27.7172, 85.324], 13); // Kathmandu
    }

    /** Quadtree tiles of `level` intersecting `bounds`: the index's own list, or computed when it has none. */
    tilesInView(level, bounds, cap) {
      const listed = this.tilesByLevel.get(level);
      if (listed && listed.length) return listed.filter((t) => bounds.intersects(t.llBounds)).slice(0, cap);
      const pts = [bounds.getSouthWest(), bounds.getSouthEast(), bounds.getNorthEast(), bounds.getNorthWest(), bounds.getCenter()]
        .map((ll) => proj.lonLatToGame(ll.lng, ll.lat));
      const s = proj.tileSize(level);
      const xs = pts.map((p) => p[0]); const zs = pts.map((p) => p[1]);
      const tx0 = Math.floor(Math.min(...xs) / s); const tx1 = Math.floor(Math.max(...xs) / s);
      const ty0 = Math.floor(Math.min(...zs) / s); const ty1 = Math.floor(Math.max(...zs) / s);
      const out = [];
      if ((tx1 - tx0 + 1) * (ty1 - ty0 + 1) > cap) return out;
      for (let tx = tx0; tx <= tx1; tx++) {
        for (let ty = ty0; ty <= ty1; ty++) {
          const corners = proj.tileCorners(level, tx, ty);
          const lons = corners.map((c) => c[0]); const lats = corners.map((c) => c[1]);
          out.push({ id: `${level}/${tx}/${ty}`, level, tx, ty, corners, listed: false,
            llBounds: L.latLngBounds([Math.min(...lats), Math.min(...lons)], [Math.max(...lats), Math.max(...lons)]) });
        }
      }
      return out;
    }

    // -------------------------------------------------------------------- map
    setupMap() {
      const map = L.map('map', { zoomControl: true, minZoom: 3, maxZoom: 22, worldCopyJump: false });
      this.map = map;
      const panes = [['qa-biome', 250], ['qa-hillshade', 260], ['qa-areas', 410], ['qa-lines', 420],
        ['qa-buildings', 430], ['qa-roads', 440], ['qa-trails', 450], ['qa-grid', 460], ['qa-places', 465],
        ['qa-pois', 470], ['qa-highlight', 480], ['qa-labels', 490]];
      for (const [name, z] of panes) {
        const p = map.createPane(name);
        p.style.zIndex = z;
      }
      map.getPane('qa-hillshade').classList.add('qa-multiply');
      map.getPane('qa-labels').style.pointerEvents = 'none';
      L.control.scale({ imperial: false }).addTo(map);
      map.setView([27.7172, 85.324], 13);

      const osmAttr = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';
      this.baseLayers = {
        // OSM tile usage policy: low volume, attribution shown, no prefetching beyond Leaflet defaults.
        osm: L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
          maxZoom: 22, maxNativeZoom: 19, attribution: osmAttr, className: 'qa-basemap' }),
      };
      const custom = this.params.get('basemap');
      if (custom) {
        this.baseLayers.custom = L.tileLayer(custom, { maxZoom: 22,
          maxNativeZoom: num(this.params.get('basemap_maxzoom')) ?? 19,
          attribution: this.params.get('basemap_attr') ? escapeHtml(this.params.get('basemap_attr')) : osmAttr,
          className: 'qa-basemap' });
        this.base = 'custom';
      }
      this.baseLayer = null;
      this.highlightRenderer = L.svg({ pane: 'qa-highlight' });

      map.on('moveend', () => { this.scheduleUpdate(); this.writeHash(); });
      // Styles read styleCtx at draw time; the canvases redraw on the 'moveend' that follows.
      map.on('zoomend', () => {
        this.styleCtx.z = map.getZoom();
        this.styleCtx.zs = zoomScale(this.styleCtx.z);
      });
      map.on('click', (e) => this.onMapClick(e));
      let raf = 0;
      map.on('mousemove', (e) => {
        if (raf) return;
        raf = requestAnimationFrame(() => { raf = 0; this.renderCursor(e.latlng); });
      });
    }

    setBase(name) {
      if (!this.baseLayers[name]) name = 'none';
      this.base = name;
      if (this.baseLayer) this.map.removeLayer(this.baseLayer);
      this.baseLayer = this.baseLayers[name] || null;
      if (this.baseLayer) { this.baseLayer.setOpacity(this.baseOpacity); this.baseLayer.addTo(this.map); }
      document.getElementById('map').classList.toggle('no-basemap', !this.baseLayer);
      for (const inp of document.querySelectorAll('#basemap-choices input')) inp.checked = inp.value === name;
      this.writeHash();
    }
    setBaseOpacity(o) {
      this.baseOpacity = o;
      if (this.baseLayer) this.baseLayer.setOpacity(o);
      document.getElementById('basemap-opacity').value = Math.round(o * 100);
      document.getElementById('basemap-opacity-out').textContent = `${Math.round(o * 100)}%`;
      this.writeHash();
    }
    setRasterOpacity(kind, o) {
      this.rasterOpacity[kind] = o;
      if (this.rasters[kind]) this.rasters[kind].setOpacity(o);
      document.getElementById(`${kind}-opacity`).value = Math.round(o * 100);
      document.getElementById(`${kind}-opacity-out`).textContent = `${Math.round(o * 100)}%`;
      this.writeHash();
    }

    scheduleUpdate() {
      if (this._updateQueued) return;
      this._updateQueued = true;
      requestAnimationFrame(() => { this._updateQueued = false; this.updateAll(); });
    }
    updateAll() {
      if (!this.index) return;
      for (const l of Object.values(this.layers)) l.update();
      this.grid.update();
      this.refreshLayerList();
    }

    onLayerLoaded(layer) {
      layer.update();
      if (layer.id === 'roads' && this.layers.trails && this.layers.trails.state === 'ready') this.layers.trails.update();
      this._search = null;
      this.invalidateStats();
      this.refreshLayerList();
      this.renderLegend();
      this.renderStats();
    }
    invalidateStats() { this._catStats = {}; this._search = null; this._statsDirty = true; }

    // --------------------------------------------------------- toggles & modes
    isVisible(id) {
      if (this.layers[id]) return this.layers[id].visible;
      if (RASTER_IDS.includes(id)) return this.rasterVisible[id];
      if (id === 'grid') return this.grid.visible;
      return false;
    }
    setVisible(id, on) {
      if (this.layers[id]) {
        this.layers[id].visible = on;
        this.layers[id].update();
      } else if (RASTER_IDS.includes(id)) {
        this.rasterVisible[id] = on;
        const r = this.rasters[id];
        if (r) { if (on) r.addTo(this.map); else this.map.removeLayer(r); }
      } else if (id === 'grid') {
        this.grid.visible = on;
        this.grid.update();
      }
      this.refreshLayerList();
      this.renderLegend();
      this.writeHash();
    }
    setRoadMode(m) {
      if (!['source', 'surface', 'class', 'sac'].includes(m)) m = 'source';
      this.roadMode = m;
      this.styleCtx.roadMode = m;
      document.getElementById('road-mode').value = m;
      for (const id of ['roads', 'trails']) if (this.layers[id]) this.layers[id].restyle();
      this.renderLegend();
      this.writeHash();
    }
    setBuildingMode(m) {
      if (!['archetype', 'levels', 'levels_source'].includes(m)) m = 'archetype';
      this.buildingMode = m;
      this.styleCtx.buildingMode = m;
      document.getElementById('building-mode').value = m;
      if (this.layers.buildings) this.layers.buildings.restyle();
      this.renderLegend();
      this.writeHash();
    }
    setGridLevel(level) {
      this.grid.level = level;
      document.getElementById('grid-level').value = String(level);
      this.grid.clear();
      this.grid.update();
      this.writeHash();
    }

    legendKey(layerId) {
      if (layerId === 'roads' || layerId === 'trails') return 'road:' + this.roadMode;
      if (layerId === 'buildings') return 'bldg:' + this.buildingMode;
      if (layerId === 'pois' || layerId === 'places') return 'poi';
      return layerId;
    }
    categoryOf(layerId, n) {
      switch (layerId) {
        case 'roads': case 'trails':
          return this.roadMode === 'surface' ? n.surface : this.roadMode === 'class' ? n.cls
            : this.roadMode === 'sac' ? n.sac : n.source;
        case 'buildings': return buildingCategory(n, this.buildingMode);
        case 'pois': case 'places': return n.group;
        case 'areas': case 'lines': return n.kind;
        default: return null;
      }
    }
    toggleCategory(key, cat) {
      const s = this.hidden[key] || (this.hidden[key] = new Set());
      if (s.has(cat)) s.delete(cat); else s.add(cat);
      this.filterEpoch++;
      for (const l of Object.values(this.layers)) if (this.legendKey(l.id) === key) l.update();
      this.renderLegend();
      this.refreshLayerList();
    }

    // ------------------------------------------------------------------ UI
    bindUi() {
      const $ = (id) => document.getElementById(id);
      // basemap choices
      const choices = [['osm', 'OSM standard'], ...(this.params.get('basemap') ? [['custom', 'Custom']] : []), ['none', 'None']];
      $('basemap-choices').innerHTML = choices.map(([v, t]) =>
        `<label><input type="radio" name="basemap" value="${v}"> ${t}</label>`).join('');
      $('basemap-choices').addEventListener('change', (e) => this.setBase(e.target.value));
      $('basemap-opacity').addEventListener('input', (e) => this.setBaseOpacity(e.target.value / 100));
      for (const kind of RASTER_IDS) {
        $(`${kind}-opacity`).addEventListener('input', (e) => this.setRasterOpacity(kind, e.target.value / 100));
      }
      $('road-mode').addEventListener('change', (e) => this.setRoadMode(e.target.value));
      $('building-mode').addEventListener('change', (e) => this.setBuildingMode(e.target.value));
      $('grid-level').addEventListener('change', (e) => this.setGridLevel(Number(e.target.value)));
      $('layer-list').addEventListener('change', (e) => {
        if (e.target.matches('input[data-layer]')) this.setVisible(e.target.dataset.layer, e.target.checked);
      });
      $('legend').addEventListener('click', (e) => {
        const row = e.target.closest('[data-cat]');
        if (row) this.toggleCategory(row.dataset.key, row.dataset.cat);
      });
      $('inspector-close').addEventListener('click', () => this.clearSelection());
      $('sidebar-collapse').addEventListener('click', () => this.setSidebar(false));
      $('sidebar-expand').addEventListener('click', () => this.setSidebar(true));
      $('dataset-select').addEventListener('change', (e) => {
        location.href = `${location.pathname}?data=${encodeURIComponent(e.target.value)}`;
      });
      // search
      const input = $('search');
      let t = 0;
      input.addEventListener('input', () => { clearTimeout(t); t = setTimeout(() => this.renderSearch(input.value), 120); });
      input.addEventListener('keydown', (e) => this.onSearchKey(e));
      $('search-results').addEventListener('click', (e) => {
        const li = e.target.closest('li[data-k]');
        if (li) this.pickResult(Number(li.dataset.k));
      });
      document.addEventListener('keydown', (e) => {
        if (e.key === '/' && document.activeElement !== input && !e.target.matches('input, select, textarea')) {
          e.preventDefault();
          input.focus();
        } else if (e.key === 'Escape' && document.activeElement !== input) {
          this.clearSelection();
        }
      });
      // The basemap is added by start() once the initial view is known (no wasted OSM tile requests).
    }

    setSidebar(open) {
      document.getElementById('app').classList.toggle('sidebar-hidden', !open);
      document.getElementById('sidebar-expand').hidden = open;
      setTimeout(() => this.map.invalidateSize(), 50);
    }

    fillDatasetPicker() {
      const sel = document.getElementById('dataset-select');
      const ds = (this.listing && this.listing.datasets) || [];
      if (!ds.length) return;
      const current = this.dataParam.replace(/\/$/, '');
      const opts = ds.map((d) => ({ v: d.data, t: `${d.id}${d.name && d.name !== d.id ? ' · ' + d.name : ''}` }));
      if (!opts.some((o) => o.v === current)) opts.unshift({ v: current, t: current });
      sel.innerHTML = opts.map((o) => `<option value="${escapeHtml(o.v)}">${escapeHtml(o.t)}</option>`).join('');
      sel.value = current;
      sel.hidden = false;
    }

    refreshLayerList() {
      const ul = document.getElementById('layer-list');
      if (!ul) return;
      const rows = [];
      for (const id of ALL_TOGGLES) {
        const l = this.layers[id];
        let label; let info = ''; let disabled = false;
        if (l) {
          if (l.def.optional && l.state === 'absent') continue;
          label = l.def.label;
          if (l.state === 'absent') { info = 'not in index'; disabled = true; } else if (l.state === 'missing') {
            info = 'file not found'; disabled = true;
          } else if (l.state === 'error') { info = 'load error'; disabled = true; } else {
            const total = l.state === 'ready' ? l.count : (l.source && l.source.count);
            const parts = [];
            if (total !== null && total !== undefined) parts.push(fmtNum(total));
            if (l.visible && l.rendered.size) parts.push(`${fmtNum(l.rendered.size)} drawn`);
            if (l.message) parts.push(l.message);
            else if (l.state === 'loading') parts.push('loading…');
            info = parts.join(' · ');
          }
        } else if (RASTER_IDS.includes(id)) {
          label = id === 'hillshade' ? 'Hillshade (HGHT)' : 'Biome (BIOM)';
          const r = this.rasters[id];
          const n = r ? r.tiles.length : 0;
          if (!n) { info = 'no tiles'; disabled = true; } else {
            info = `${n} tiles` + (r && r.stats.missing ? ` · ${r.stats.missing} missing` : '');
          }
        } else if (id === 'grid') {
          label = 'Tile grid';
          info = this.grid.visible ? `L${this.grid.level} · ${this.grid.inView} in view${this.grid.message ? ' · ' + this.grid.message : ''}`
            : `L${this.grid.level ?? '?'}`;
        }
        const on = this.isVisible(id);
        rows.push(`<li class="${disabled ? 'disabled' : ''}"><label><input type="checkbox" data-layer="${id}"` +
          `${on ? ' checked' : ''}${disabled ? ' disabled' : ''}> <span class="ll-name">${escapeHtml(label)}</span>` +
          `<span class="ll-info">${escapeHtml(info)}</span></label></li>`);
      }
      const html = rows.join('');
      if (html !== this._layerListHtml) {
        this._layerListHtml = html;
        ul.innerHTML = html;
      }
    }

    renderStatus() {
      const el = document.getElementById('status');
      if (!el) return;
      if (this.pending > 0) {
        const items = [...this.pendingLabels.values()].map((p) => p.label + (p.bytes ? ` ${fmtBytes(p.bytes)}` : ''));
        el.innerHTML = `<span class="spinner"></span> loading ${escapeHtml(items.slice(0, 3).join(', '))}${items.length > 3 ? '…' : ''}`;
      } else {
        el.textContent = this.errors.length ? 'error (see banner)' : 'ready';
      }
    }

    renderCursor(ll) {
      const [x, z] = proj.lonLatToGame(ll.lng, ll.lat);
      const t = proj.tileAt(this.leafLevel, x, z);
      document.getElementById('cursor').textContent =
        `${ll.lat.toFixed(6)}, ${ll.lng.toFixed(6)} · game ${fmtNum(x, 1)}, ${fmtNum(z, 1)} · tile ${t.level}/${t.tx}/${t.ty} · z${this.map.getZoom()}`;
    }

    // ------------------------------------------------------------- legend
    categoryStats(key) {
      if (this._catStats[key]) return this._catStats[key];
      const out = new Map();
      const ids = key.startsWith('road:') ? ['roads', 'trails'] : key.startsWith('bldg:') ? ['buildings']
        : key === 'poi' ? ['pois', 'places'] : [key];
      let total = 0;
      for (const id of ids) {
        const l = this.layers[id];
        if (!l || l.state !== 'ready') continue;
        for (let i = 0; i < l.count; i++) {
          const cat = this.categoryOf(id, l.norm[i]);
          const v = l.def.lengths ? l.lengths[i] : 1;
          out.set(cat, (out.get(cat) || 0) + v);
          total += v;
        }
      }
      const res = { by: out, total, unit: (key.startsWith('road:') || key === 'lines') ? 'm' : 'n' };
      this._catStats[key] = res;
      return res;
    }

    legendRow(key, cat, label, sw, stats, note) {
      const v = stats ? stats.by.get(cat) || 0 : null;
      const val = v === null ? '' : stats.unit === 'm' ? fmtLen(v) : fmtNum(v);
      const pct = stats && stats.total && v ? ` <span class="pct">${fmtNum(100 * v / stats.total, 1)}%</span>` : '';
      const hidden = this.hidden[key] && this.hidden[key].has(cat);
      let swatch;
      if (sw.type === 'line') {
        swatch = `<svg width="28" height="12" aria-hidden="true"><line x1="2" y1="6" x2="26" y2="6" stroke="${sw.color}" ` +
          `stroke-width="${Math.min(9, sw.width || 3)}" ${sw.dash ? `stroke-dasharray="${sw.dash}"` : ''} stroke-linecap="${sw.dash ? 'butt' : 'round'}"/></svg>`;
      } else if (sw.type === 'dot') {
        swatch = `<svg width="28" height="12" aria-hidden="true"><circle cx="14" cy="6" r="5" fill="${sw.color}" stroke="${sw.stroke || '#fff'}" stroke-width="1.5"/></svg>`;
      } else {
        swatch = `<svg width="28" height="12" aria-hidden="true"><rect x="3" y="1" width="22" height="10" rx="2" fill="${sw.color}" fill-opacity="${sw.opacity ?? 0.85}" stroke="#333" stroke-opacity="0.5"/></svg>`;
      }
      return `<div class="lg-row${hidden ? ' off' : ''}${v === 0 ? ' zero' : ''}" data-key="${escapeHtml(key)}" data-cat="${escapeHtml(cat)}" ` +
        `title="${escapeHtml(note || cat)}">${swatch}<span class="lg-label">${escapeHtml(label)}</span>` +
        `<span class="lg-val">${val}${pct}</span></div>`;
    }

    renderLegend() {
      const el = document.getElementById('legend');
      if (!el || !this.index) return;
      const html = [];
      const roadsOn = (this.layers.roads && this.layers.roads.visible) || (this.layers.trails && this.layers.trails.visible);
      if (roadsOn) {
        const key = 'road:' + this.roadMode;
        const st = this.categoryStats(key);
        const titles = { source: 'Roads & trails · surface_source (length)', surface: 'Roads & trails · surface (length)',
          class: 'Roads & trails · class (colour + width)', sac: 'Trails · sac_scale (dashed = inferred)' };
        html.push(`<h3>${titles[this.roadMode]}</h3>`);
        if (this.roadMode === 'source') {
          const bar = ['TAGGED', 'DERIVED', 'INFERRED', 'DEFAULT'].map((s) => {
            const v = st.by.get(s) || 0;
            return st.total ? `<span style="width:${(100 * v / st.total).toFixed(2)}%;background:${SOURCE_COLORS[s]}" title="${s}"></span>` : '';
          }).join('');
          html.push(`<div class="src-bar" aria-hidden="true">${bar}</div>`);
          for (const s of ['TAGGED', 'DERIVED', 'INFERRED', 'DEFAULT']) {
            html.push(this.legendRow(key, s, `${s.toLowerCase()} · ${SOURCE_NOTES[s]}`, { type: 'line', color: SOURCE_COLORS[s], width: 4 }, st));
          }
        } else if (this.roadMode === 'surface') {
          for (const s of Object.keys(SURFACE_COLORS)) {
            if (s === 'UNKNOWN' && !st.by.get(s)) continue;
            html.push(this.legendRow(key, s, s.toLowerCase().replace('_', '/'), { type: 'line', color: SURFACE_COLORS[s], width: 4 }, st));
          }
        } else if (this.roadMode === 'class') {
          for (const c of CLASS_ORDER) {
            if (c === 'UNKNOWN' && !st.by.get(c)) continue;
            html.push(this.legendRow(key, c, c.toLowerCase(), { type: 'line', color: CLASS_COLORS[c], width: CLASS_WIDTH[c],
              dash: TRAIL_CLASSES.has(c) ? '5 3' : null }, st));
          }
        } else {
          for (const s of SAC_ORDER) {
            html.push(this.legendRow(key, s, `${SAC_SHORT[s]} ${s.toLowerCase()}`, { type: 'line', color: SAC_COLORS[s], width: 4 }, st));
          }
        }
        for (const [cat] of st.by) {
          const known = this.roadMode === 'source' ? SOURCE_COLORS : this.roadMode === 'surface' ? SURFACE_COLORS
            : this.roadMode === 'class' ? CLASS_COLORS : SAC_COLORS;
          if (!(cat in known)) html.push(this.legendRow(key, cat, `${cat} (unknown value)`, { type: 'line', color: UNKNOWN_COLOR, width: 4 }, st));
        }
      }
      const b = this.layers.buildings;
      if (b && b.visible && b.state !== 'absent') {
        const key = 'bldg:' + this.buildingMode;
        const st = b.state === 'ready' ? this.categoryStats(key) : null;
        const titles = { archetype: 'Buildings · archetype', levels: 'Buildings · levels', levels_source: 'Buildings · levels source' };
        html.push(`<h3>${titles[this.buildingMode]}${b.state === 'ready' ? '' : ` <span class="hint">counts after load (zoom ≥ ${b.minZoom()})</span>`}</h3>`);
        const pal = this.buildingMode === 'levels' ? LEVEL_COLORS : this.buildingMode === 'levels_source' ? LEVELS_SOURCE_COLORS : ARCHETYPE_COLORS;
        const cats = this.buildingMode === 'levels' ? LEVEL_BUCKETS : Object.keys(pal);
        for (const c of cats) {
          if (c === '?' && !(st && st.by.get(c))) continue;
          html.push(this.legendRow(key, c, this.buildingMode === 'levels' ? `${c} level${c === '1' ? '' : 's'}` : c.toLowerCase(),
            { type: 'fill', color: pal[c] }, st));
        }
        if (st) for (const [cat] of st.by) if (!(cat in pal) && !cats.includes(cat)) html.push(this.legendRow(key, cat, `${cat} (unknown)`, { type: 'fill', color: UNKNOWN_COLOR }, st));
        html.push('<div class="lg-note">black outline = LANDMARK (hero asset replaces it)</div>');
      }
      if ((this.layers.pois && this.layers.pois.visible) || (this.layers.places && this.layers.places.visible && this.layers.places.state !== 'absent')) {
        const st = this.categoryStats('poi');
        html.push('<h3>POIs · kind group <span class="hint">size = importance</span></h3>');
        for (const [g, v] of Object.entries(POI_GROUPS)) {
          if ((g === 'other' || g === 'place') && !st.by.get(g)) continue;
          html.push(this.legendRow('poi', g, v.label, { type: 'dot', color: v.color }, st));
        }
      }
      for (const id of ['areas', 'lines']) {
        const l = this.layers[id];
        if (!l || !l.visible || l.state !== 'ready') continue;
        const st = this.categoryStats(id);
        if (!st.by.size) continue;
        html.push(`<h3>${id === 'areas' ? 'Areas · kind' : 'Lines · kind'} <span class="hint">present in data</span></h3>`);
        const cats = [...st.by.keys()].sort((a, c) => (enumValue(id === 'areas' ? 'AreaKind' : 'LineKind', a) ?? 999) -
          (enumValue(id === 'areas' ? 'AreaKind' : 'LineKind', c) ?? 999));
        for (const c of cats) {
          const sw = id === 'areas' ? { type: 'fill', color: AREA_COLORS[c] || UNKNOWN_COLOR, opacity: 0.45 }
            : { type: 'line', color: (LINE_STYLES[c] || LINE_STYLES.NONE).color, width: (LINE_STYLES[c] || LINE_STYLES.NONE).weight,
              dash: (LINE_STYLES[c] || {}).dashArray };
          html.push(this.legendRow(id, c, c.toLowerCase(), sw, st));
        }
      }
      if (this.rasterVisible.biome) {
        html.push('<h3>Biome overlay <span class="hint">palette from index.json</span></h3>');
        if (this.biomePalette) {
          html.push('<div class="palette">' + this.biomePalette.map((p) =>
            `<div class="pal-row" title="${escapeHtml(p.color)}"><span class="pal-sw" style="background:${escapeHtml(p.color)}"></span>${escapeHtml(p.name.toLowerCase())}</div>`).join('') + '</div>');
        } else {
          html.push('<div class="lg-note">index.json has no biome palette: click the map to read raw pixel colours.</div>');
        }
      }
      if (this.rasterVisible.hillshade) html.push('<h3>Hillshade</h3><div class="lg-note">decoded HGHT, multiplied over the basemap; click to read the value.</div>');
      el.innerHTML = html.join('') || '<div class="lg-note">Turn on a layer to see its legend.</div>';
    }

    // -------------------------------------------------------------- stats
    renderStats() {
      const el = document.getElementById('stats');
      if (!el || !this.index) return;
      const idx = this.index;
      const rows = [];
      const kv = (k, v) => rows.push(`<tr><th>${escapeHtml(k)}</th><td>${v}</td></tr>`);
      kv('region', escapeHtml(idx.region ?? '?') + (idx.synthetic ? ' <span class="badge">synthetic</span>' : ''));
      for (const k of ['data_version', 'pipeline_version', 'scale_model', 'built_at']) {
        if (idx[k] !== undefined) kv(k, escapeHtml(idx[k]));
      }
      if (this.enumsVersion !== undefined) kv('enums.json', `v${escapeHtml(this.enumsVersion)} (from repo)`);
      const lv = [...this.tilesByLevel.entries()].sort((a, b) => a[0] - b[0]).map(([L_, ts]) => `L${L_}×${ts.length}`).join(' · ');
      kv('tiles', lv || '<span class="muted">none listed</span>');
      kv('leaf level', `${this.leafLevel} (${fmtNum(proj.tileSize(this.leafLevel))} m)`);
      const loaded = [];
      for (const l of Object.values(this.layers)) {
        if (l.state === 'absent') continue;
        let s = `${escapeHtml(l.id)}: `;
        if (l.state === 'ready') {
          s += fmtNum(l.count);
          if (l.def.lengths && l.count) s += ` · ${fmtLen(l.lengths.reduce((a, c) => a + c, 0))}`;
          if (l.ctxStripped) s += ` · ${fmtNum(l.ctxStripped)} context pts hidden`;
          if (l.bytes) s += ` · ${fmtBytes(l.bytes)} in ${fmtNum(l.loadMs / 1000, 1)} s`;
          if (l.source && l.source.count !== null && l.source.count !== undefined && l.source.count !== l.count && !l.source.tiles) {
            s += ` <span class="warn">index says ${fmtNum(l.source.count)}</span>`;
          }
        } else s += `<span class="muted">${escapeHtml(l.state === 'idle' && l.def.lazy ? `lazy (zoom ≥ ${l.minZoom()})` : l.state)}</span>`;
        loaded.push(s);
      }
      kv('loaded', loaded.join('<br>'));
      const b = this.layers.buildings;
      if (b && b.state === 'ready' && b.count) {
        const inf = b.norm.reduce((a, n) => a + (n.levelsInferred ? 1 : 0), 0);
        kv('levels inferred', `${fmtNum(100 * inf / b.count, 1)}% of loaded buildings`);
      }
      const html = [`<table class="kv">${rows.join('')}</table>`];
      if (idx.stats && typeof idx.stats === 'object') {
        html.push('<h3>index.json stats</h3>' + this.statsTable(idx.stats));
      }
      el.innerHTML = html.join('');
    }
    statsTable(obj, depth = 0) {
      const rows = Object.entries(obj).map(([k, v]) => {
        let val;
        if (v && typeof v === 'object' && !Array.isArray(v)) val = depth < 3 ? this.statsTable(v, depth + 1) : escapeHtml(JSON.stringify(v));
        else if (Array.isArray(v)) val = escapeHtml(JSON.stringify(v));
        else if (typeof v === 'number') val = fmtNum(v, Number.isInteger(v) ? 0 : 2);
        else val = escapeHtml(v);
        return `<tr><th>${escapeHtml(k)}</th><td>${val}</td></tr>`;
      });
      return `<table class="kv${depth ? ' nested' : ''}">${rows.join('')}</table>`;
    }

    // ---------------------------------------------------------- inspector
    onMapClick(e) {
      const order = [['pois', 10], ['places', 10], ['trails', 6], ['roads', 6], ['lines', 6], ['buildings', 3], ['areas', 3]];
      let hit = null;
      const lineHits = [];
      for (const [id, tol] of order) {
        const l = this.layers[id];
        if (!l) continue;
        const h = l.hitTest(e.latlng, tol);
        if (!h) continue;
        if (id === 'pois' || id === 'places') { if (!hit || h.d < hit.d) hit = h; continue; }
        if (id === 'trails' || id === 'roads' || id === 'lines') { lineHits.push(h); continue; }
        if (!hit && !lineHits.length) { hit = h; break; }
      }
      if (!hit && lineHits.length) hit = lineHits.sort((a, c) => a.d - c.d)[0];
      if (hit) this.select(hit.layer.id, hit.i, e.latlng, true);
      else this.showLocation(e.latlng);
    }

    clearSelection() {
      this.selected = null;
      if (this.highlight) { this.highlight.remove(); this.highlight = null; }
      this.map.closePopup();
      document.getElementById('inspector').hidden = true;
    }

    select(layerId, i, latlng, popup) {
      const l = this.layers[layerId];
      const f = l.features[i];
      const n = l.norm[i];
      this.selected = { layerId, i };
      if (this.highlight) this.highlight.remove();
      this.highlight = L.geoJSON(f, {
        pane: 'qa-highlight', interactive: false, renderer: this.highlightRenderer,
        style: () => ({ color: '#00e5ff', weight: 7, opacity: 0.6, fillColor: '#00e5ff', fillOpacity: 0.12 }),
        pointToLayer: (ft, ll) => L.circleMarker(ll, { radius: 13, color: '#00e5ff', weight: 3, fill: false,
          pane: 'qa-highlight', renderer: this.highlightRenderer, interactive: false }),
      }).addTo(this.map);
      const at = latlng || this.featureCenter(f);
      const title = displayName(f) || this.kindLabel(layerId, n);
      if (popup) {
        L.popup({ autoPan: false, maxWidth: 280, className: 'qa-popup' }).setLatLng(at)
          .setContent(`<b>${escapeHtml(title)}</b><br>${this.popupSummary(layerId, n, f)}`).openOn(this.map);
      }
      this.renderInspector(layerId, i, at);
    }

    featureCenter(f) {
      const b = geomBBox(f.geometry);
      return L.latLng((b[1] + b[3]) / 2, (b[0] + b[2]) / 2);
    }

    kindLabel(layerId, n) {
      switch (layerId) {
        case 'roads': case 'trails': return `${n.cls.toLowerCase()} (${n.surface.toLowerCase()})`;
        case 'buildings': return `${n.arche.toLowerCase()} building`;
        default: return String(n.kind || layerId).toLowerCase();
      }
    }

    popupSummary(layerId, n, f) {
      const p = f.properties || {};
      switch (layerId) {
        case 'roads': case 'trails':
          return `${escapeHtml(n.cls.toLowerCase())} · ${escapeHtml(n.surface.toLowerCase())} · ` +
            `<span class="src src-${escapeHtml(n.source)}">${escapeHtml(n.source.toLowerCase())}</span>` +
            (n.sac !== 'UNKNOWN' ? ` · ${SAC_SHORT[n.sac] || escapeHtml(n.sac)}${n.sacInferred ? ' (inferred)' : ''}` : '');
        case 'buildings':
          return `${escapeHtml(n.arche.toLowerCase())} · ${escapeHtml(n.use.toLowerCase())}<br>` +
            `levels <b>${n.levels ?? '?'}</b> ${n.levelsInferred ? '<span class="src src-INFERRED">inferred</span>' : '<span class="src src-TAGGED">tagged</span>'}` +
            (num(p.height_cm) ? ` · ${fmtNum(p.height_cm / 100, 1)} m` : '');
        case 'pois': case 'places':
          return `${escapeHtml(String(n.kind).toLowerCase())} · ${escapeHtml(n.group)} · importance ${n.importance}`;
        default:
          return escapeHtml(String(n.kind || '').toLowerCase());
      }
    }

    keyFields(layerId, n, f) {
      const p = f.properties || {};
      const g = f.geometry;
      const out = [];
      const nm = featureNames(p);
      if (nm.def) out.push(['name', escapeHtml(nm.def)]);
      if (nm.en && nm.en !== nm.def) out.push(['name:en', escapeHtml(nm.en)]);
      if (nm.ne && nm.ne !== nm.def) out.push(['name:ne', `<span lang="ne">${escapeHtml(nm.ne)}</span>`]);
      const flagsStr = n.flags && n.flags.length ? escapeHtml(n.flags.join(' ')) : '<span class="muted">none</span>';
      switch (layerId) {
        case 'roads': case 'trails': {
          const grp = prop(p, 'surface_group') ? normEnum('SurfaceGroup', p.surface_group) : null;
          out.push(['class', escapeHtml(n.cls)]);
          out.push(['surface', `<span class="sw" style="background:${SURFACE_COLORS[n.surface] || UNKNOWN_COLOR}"></span>${escapeHtml(n.surface)}${grp ? ` <span class="muted">(${escapeHtml(grp)})</span>` : ''}`]);
          out.push(['surface_source', `<span class="src src-${escapeHtml(n.source)}">${escapeHtml(n.source)}</span> <span class="muted">${escapeHtml(SOURCE_NOTES[n.source] || '')}</span>`]);
          if (n.trail || n.sac !== 'UNKNOWN') out.push(['sac_scale', `${escapeHtml(n.sac)} ${SAC_SHORT[n.sac] ? `(${SAC_SHORT[n.sac]})` : ''} ${n.sacInferred ? '<span class="src src-INFERRED">inferred</span>' : (n.sac !== 'UNKNOWN' ? '<span class="src src-TAGGED">tagged</span>' : '')}`]);
          out.push(['flags', flagsStr]);
          const acc = prop(p, 'access');
          if (acc !== undefined) out.push(['access', escapeHtml(flagNames('Travel', acc).join(' ') || 'none')]);
          if (num(p.lanes)) out.push(['lanes', escapeHtml(p.lanes)]);
          if (num(p.width_cm)) out.push(['width', `${fmtNum(p.width_cm / 100, 1)} m`]);
          if (prop(p, 'ref')) out.push(['ref', escapeHtml(p.ref)]);
          out.push(['length', fmtLen(lineLength(g))]);
          break;
        }
        case 'buildings': {
          out.push(['archetype', `<span class="sw" style="background:${ARCHETYPE_COLORS[n.arche] || UNKNOWN_COLOR}"></span>${escapeHtml(n.arche)}`]);
          out.push(['use', escapeHtml(n.use)]);
          out.push(['levels', `<b>${n.levels ?? '?'}</b> ${n.levelsInferred ? '<span class="src src-INFERRED">inferred</span>' : '<span class="src src-TAGGED">tagged</span>'}`]);
          const h = num(p.height_cm);
          out.push(['height', h ? `${fmtNum(h / 100, 2)} m${n.flags.includes('HEIGHT_TAGGED') ? ' (tagged)' : ''}` : '<span class="muted">derived from levels</span>']);
          if (num(p.min_height_cm)) out.push(['min_height', `${fmtNum(p.min_height_cm / 100, 2)} m`]);
          for (const k of ['roof_shape', 'roof_material', 'wall_material']) if (p[k] !== undefined) out.push([k, escapeHtml(normEnum(PROP_ENUMS[k], p[k]))]);
          out.push(['flags', flagsStr]);
          out.push(['footprint', `${fmtNum(polyArea(g), 0)} m²`]);
          break;
        }
        case 'pois': case 'places':
          out.push(['kind', `${escapeHtml(n.kind)} <span class="muted">${n.kindNum ?? ''}</span>`]);
          out.push(['group', `<span class="sw round" style="background:${(POI_GROUPS[n.group] || POI_GROUPS.other).color}"></span>${escapeHtml(n.group)}`]);
          out.push(['importance', `${n.importance} / 255`]);
          if (n.flags.length) out.push(['flags', flagsStr]);
          if (num(p.ele_dm) && n.flags.includes('HAS_ELE')) out.push(['ele', `${fmtNum(p.ele_dm / 10, 1)} m`]);
          break;
        case 'areas':
          out.push(['kind', `<span class="sw" style="background:${AREA_COLORS[n.kind] || UNKNOWN_COLOR}"></span>${escapeHtml(n.kind)}`]);
          out.push(['flags', flagsStr]);
          out.push(['area', `${fmtNum(polyArea(g) / 1e4, 2)} ha`]);
          break;
        case 'lines':
          out.push(['kind', escapeHtml(n.kind)]);
          out.push(['flags', flagsStr]);
          if (num(p.width_cm)) out.push(['width', `${fmtNum(p.width_cm / 100, 1)} m`]);
          out.push(['length', fmtLen(lineLength(g))]);
          break;
        default: break;
      }
      if (p.tile) out.push(['tile', escapeHtml(p.tile)]);
      const ref = osmRefOf(layerId, p);
      if (ref) {
        out.push(['osm', this.index.synthetic ? `${ref.type} ${ref.id} <span class="muted">(synthetic id)</span>`
          : `<a href="https://www.openstreetmap.org/${ref.type}/${ref.id}" target="_blank" rel="noopener">${ref.type}/${ref.id}</a>`]);
      }
      return out;
    }

    rawRows(layerId, p) {
      return Object.entries(p).map(([k, v]) => {
        let val = v !== null && typeof v === 'object' ? JSON.stringify(v) : String(v);
        val = escapeHtml(val);
        let enumName = PROP_ENUMS[k];
        if (k === 'kind') enumName = LAYER_KIND_ENUM[layerId];
        if (k === 'flags') enumName = LAYER_FLAG_ENUM[layerId];
        if (enumName && (typeof v === 'number' || /^\d+$/.test(String(v)))) {
          const dec = FLAG_ENUMS.has(enumName) ? flagNames(enumName, v).join(' ') : normEnum(enumName, v);
          if (dec) val += ` <span class="muted">${escapeHtml(dec)}</span>`;
        }
        return `<tr><th>${escapeHtml(k)}</th><td>${val}</td></tr>`;
      }).join('');
    }

    locationRows(latlng) {
      const rows = [];
      const [x, z] = proj.lonLatToGame(latlng.lng, latlng.lat);
      const t = proj.tileAt(this.leafLevel, x, z);
      const tid = `${t.level}/${t.tx}/${t.ty}`;
      const listed = (this.tilesByLevel.get(this.leafLevel) || []).find((tt) => tt.id === tid);
      rows.push(['lat, lon', `${latlng.lat.toFixed(7)}, ${latlng.lng.toFixed(7)}`]);
      rows.push(['game x, z', `${fmtNum(x, 2)}, ${fmtNum(z, 2)} m`]);
      rows.push(['leaf tile', `${tid} ${listed ? '' : '<span class="warn">not in index</span>'}`]);
      rows.push(['tile key', `<code>${proj.tileKey(t.level, t.tx, t.ty)}</code>`]);
      const s = proj.tileSize(t.level);
      rows.push(['in tile', `${fmtNum(x - t.tx * s, 2)}, ${fmtNum(z - t.ty * s, 2)} m from SW corner`]);
      for (const kind of RASTER_IDS) {
        const r = this.rasters[kind];
        if (!r || !r.tiles.length) continue;
        if (!this.rasterVisible[kind]) { rows.push([kind, '<span class="muted">turn the overlay on to sample</span>']); continue; }
        const smp = r.sample(latlng);
        if (!smp) { rows.push([kind, '<span class="muted">no tile here</span>']); continue; }
        const hex = hexOf(smp.rgba);
        let txt;
        if (kind === 'biome') {
          const hit = this.biomePalette && this.biomePalette.find((p) => p.color === hex);
          txt = `<span class="sw" style="background:${hex}"></span>${hit ? `<b>${escapeHtml(hit.name)}</b>` : '<span class="warn">no palette match</span>'} <span class="muted">${hex}</span>`;
        } else {
          txt = `${smp.rgba[0]} / 255`;
        }
        rows.push([kind, `${txt} <span class="muted">px ${smp.px},${smp.py} of ${smp.w}×${smp.h} (${smp.reg})</span>`]);
      }
      return rows;
    }

    renderInspector(layerId, i, latlng) {
      const box = document.getElementById('inspector');
      const body = document.getElementById('inspector-body');
      const tr = (rows) => rows.map(([k, v]) => `<tr><th>${escapeHtml(k)}</th><td>${v}</td></tr>`).join('');
      let html = '';
      if (layerId) {
        const l = this.layers[layerId];
        const f = l.features[i];
        const n = l.norm[i];
        const p = f.properties || {};
        document.getElementById('inspector-title').textContent = displayName(f) || this.kindLabel(layerId, n);
        html += `<div class="insp-sub">${escapeHtml(l.def.label)} · #${i}</div>`;
        html += `<table class="kv">${tr(this.keyFields(layerId, n, f))}</table>`;
        html += `<details open><summary>All properties (${Object.keys(p).length})</summary><table class="kv raw">${this.rawRows(layerId, p)}</table></details>`;
        const g = f.geometry;
        const geoRows = [['type', escapeHtml(g.type)], ['vertices', fmtNum(vertexCount(g))],
          ['bbox', geomBBox(g).map((v) => v.toFixed(7)).join(', ')]];
        if (f._ctx) {
          geoRows.push(['context pts', ['prev', 'next'].filter((k) => f._ctx[k])
            .map((k) => `${k}: ${f._ctx[k][1].toFixed(7)}, ${f._ctx[k][0].toFixed(7)}`).join('<br>') +
            ' <span class="muted">(outside the tile, not drawn)</span>']);
        }
        html += `<details${f._ctx ? ' open' : ''}><summary>Geometry</summary><table class="kv">${tr(geoRows)}</table></details>`;
        html += '<div class="insp-actions"><button type="button" data-act="zoom">Zoom to</button>' +
          '<button type="button" data-act="copy">Copy GeoJSON</button></div>';
      } else {
        document.getElementById('inspector-title').textContent = 'Location';
      }
      html += `<details open><summary>Location</summary><table class="kv">${tr(this.locationRows(latlng))}</table></details>`;
      body.innerHTML = html;
      box.hidden = false;
      body.querySelectorAll('button[data-act]').forEach((btn) => btn.addEventListener('click', () => {
        const l = this.layers[layerId];
        const f = l.features[i];
        if (btn.dataset.act === 'zoom') this.zoomToFeature(f);
        else if (navigator.clipboard) navigator.clipboard.writeText(JSON.stringify(f)).then(() => { btn.textContent = 'Copied'; }, () => {});
      }));
    }

    showLocation(latlng) {
      this.selected = null;
      if (this.highlight) this.highlight.remove();
      this.highlight = L.circleMarker(latlng, { radius: 5, color: '#00e5ff', weight: 2, fill: false,
        pane: 'qa-highlight', renderer: this.highlightRenderer, interactive: false }).addTo(this.map);
      this.map.closePopup();
      this.renderInspector(null, null, latlng);
    }

    zoomToFeature(f) {
      const b = geomBBox(f.geometry);
      if (b[0] === b[2] && b[1] === b[3]) this.map.setView([b[1], b[0]], Math.max(this.map.getZoom(), 18));
      else this.map.fitBounds([[b[1], b[0]], [b[3], b[2]]], { maxZoom: 18, padding: [40, 40] });
    }

    // -------------------------------------------------------------- search
    buildSearch() {
      const entries = [];
      const add = (layerId, i, nm, kind, imp) => {
        const keys = [nm.def, nm.en, nm.ne].filter(Boolean);
        if (!keys.length) return null;
        const e = { layerId, idx: [i], name: nm.def || nm.en || nm.ne, ne: nm.ne && nm.ne !== nm.def ? nm.ne : '',
          kind, imp, keys: keys.map(fold) };
        entries.push(e);
        return e;
      };
      for (const id of ['places', 'pois', 'areas', 'buildings', 'lines', 'roads', 'trails']) {
        const l = this.layers[id];
        if (!l || l.state !== 'ready') continue;
        const byName = new Map(); // roads/lines are clipped per tile: one entry per (layer, name)
        for (let i = 0; i < l.count; i++) {
          const nm = featureNames(l.features[i].properties || {});
          if (!nm.def && !nm.en && !nm.ne) continue;
          const n = l.norm[i];
          if (l.def.lengths) {
            const k = nm.def || nm.en || nm.ne;
            const prev = byName.get(k);
            if (prev) { prev.idx.push(i); continue; }
            byName.set(k, add(id, i, nm, (n.cls || n.kind || id).toLowerCase(), 0));
          } else {
            add(id, i, nm, String(n.kind || n.arche || id).toLowerCase(), n.importance || (id === 'places' ? 200 : 0));
          }
        }
      }
      this._search = entries;
      return entries;
    }

    search(q) {
      q = q.trim();
      const out = [];
      if (!q) return out;
      let m = q.match(/^(\d{1,2})\s*\/\s*(\d+)\s*\/\s*(\d+)$/);
      if (m) {
        const level = +m[1]; const tx = +m[2]; const ty = +m[3];
        const c = proj.tileCorners(level, tx, ty);
        out.push({ special: 'tile', name: `tile ${level}/${tx}/${ty}`, kind: `${fmtNum(proj.tileSize(level))} m quadtree tile`,
          bounds: [[Math.min(...c.map((p) => p[1])), Math.min(...c.map((p) => p[0]))], [Math.max(...c.map((p) => p[1])), Math.max(...c.map((p) => p[0]))]] });
        return out;
      }
      m = q.match(/^(-?\d+(?:\.\d+)?)\s*[,\s]\s*(-?\d+(?:\.\d+)?)$/);
      if (m) {
        let a = +m[1]; let b = +m[2];
        if (Math.abs(a) > 60 && Math.abs(b) <= 60) [a, b] = [b, a]; // "85.31, 27.71" (lon, lat) in Nepal
        if (Math.abs(a) <= 90 && Math.abs(b) <= 180) out.push({ special: 'point', name: `${a.toFixed(6)}, ${b.toFixed(6)}`, kind: 'lat, lon', latlng: [a, b] });
        return out;
      }
      m = q.match(/^([nwr]|node\/|way\/|relation\/)?(\d{3,})$/i);
      if (m) {
        const want = Number(m[2]);
        const t = m[1] ? ({ n: 'node', w: 'way', r: 'relation' }[m[1][0].toLowerCase()]) : null;
        for (const [id, l] of Object.entries(this.layers)) {
          if (l.state !== 'ready') continue;
          for (let i = 0; i < l.count && out.length < 30; i++) {
            const ref = osmRefOf(id, l.features[i].properties || {});
            if (ref && ref.id === want && (!t || ref.type === t)) {
              out.push({ layerId: id, idx: [i], name: displayName(l.features[i]) || this.kindLabel(id, l.norm[i]), kind: `${id} · ${ref.type} ${ref.id}` });
            }
          }
        }
        if (out.length) return out;
      }
      const fq = fold(q);
      const entries = this._search || this.buildSearch();
      const scored = [];
      for (const e of entries) {
        let best = -1;
        for (const k of e.keys) {
          const p = k.indexOf(fq);
          if (p < 0) continue;
          const s = k === fq ? 3 : p === 0 ? 2 : /[\s\-(]/.test(k[p - 1]) ? 1.5 : 1;
          if (s > best) best = s;
        }
        if (best > 0) scored.push({ e, s: best });
      }
      scored.sort((a, b) => (b.s - a.s) || (b.e.imp - a.e.imp) || a.e.name.localeCompare(b.e.name));
      return scored.slice(0, 30).map((x) => x.e);
    }

    renderSearch(q) {
      const ul = document.getElementById('search-results');
      const res = this.search(q);
      this._results = res;
      this._resultSel = res.length ? 0 : -1;
      if (!q.trim()) { ul.hidden = true; ul.innerHTML = ''; return; }
      ul.innerHTML = res.length ? res.map((r, k) => `<li data-k="${k}" role="option"${k === 0 ? ' class="sel"' : ''}>` +
        `<span class="sr-name">${escapeHtml(r.name)}</span>${r.ne ? ` <span class="sr-ne" lang="ne">${escapeHtml(r.ne)}</span>` : ''}` +
        `<span class="sr-kind">${escapeHtml(r.kind || '')}${r.layerId ? ' · ' + escapeHtml(r.layerId) : ''}${r.idx && r.idx.length > 1 ? ` · ${r.idx.length} pieces` : ''}</span></li>`).join('')
        : '<li class="empty">no match in loaded layers</li>';
      ul.hidden = false;
    }

    onSearchKey(e) {
      const ul = document.getElementById('search-results');
      const n = (this._results || []).length;
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        if (!n) return;
        e.preventDefault();
        this._resultSel = (this._resultSel + (e.key === 'ArrowDown' ? 1 : n - 1)) % n;
        ul.querySelectorAll('li[data-k]').forEach((li) => li.classList.toggle('sel', Number(li.dataset.k) === this._resultSel));
      } else if (e.key === 'Enter') {
        e.preventDefault();
        this.renderSearch(e.target.value);
        if (this._results && this._results.length) this.pickResult(Math.max(0, this._resultSel));
      } else if (e.key === 'Escape') {
        ul.hidden = true;
        e.target.blur();
      }
    }

    pickResult(k) {
      const r = this._results && this._results[k];
      if (!r) return;
      document.getElementById('search-results').hidden = true;
      if (r.special === 'tile') { this.map.fitBounds(r.bounds); return; }
      if (r.special === 'point') { this.map.setView(r.latlng, Math.max(this.map.getZoom(), 17)); this.showLocation(L.latLng(r.latlng)); return; }
      const l = this.layers[r.layerId];
      const b = L.latLngBounds([]);
      for (const i of r.idx) { const bb = geomBBox(l.features[i].geometry); b.extend([[bb[1], bb[0]], [bb[3], bb[2]]]); }
      const minZ = Math.max(l.minZoom(), l.def.lazy ? l.minZoom() : 0);
      if (b.getNorth() === b.getSouth() && b.getEast() === b.getWest()) this.map.setView(b.getCenter(), Math.max(17, minZ));
      else this.map.fitBounds(b, { maxZoom: 18, padding: [40, 40] });
      if (this.map.getZoom() < minZ) this.map.setZoom(minZ);
      if (!l.visible) this.setVisible(r.layerId, true);
      const done = () => { this.select(r.layerId, r.idx[0], this.featureCenter(l.features[r.idx[0]]), true); };
      this.map.once('moveend', done);
      setTimeout(() => { this.map.off('moveend', done); if (!this.selected || this.selected.i !== r.idx[0]) done(); }, 400);
    }

    // ---------------------------------------------------------------- hash
    parseHash(h) {
      const parts = (h || '').replace(/^#/, '').split('/');
      if (parts.length < 3) return null;
      const z = Number(parts[0]); const lat = Number(parts[1]); const lon = Number(parts[2]);
      if (![z, lat, lon].every(Number.isFinite)) return null;
      const layers = parts[3] !== undefined ? new Set(parts[3].split(',').filter(Boolean)) : null;
      const opts = new URLSearchParams(parts.slice(4).join('/'));
      return { z, lat, lon, layers, opts };
    }

    applyHash(h, initial) {
      this._applyingHash = true;
      try {
        this._applyHash(h, initial);
      } finally {
        this._applyingHash = false;
      }
      if (!initial) this.writeHash();
    }

    _applyHash(h, initial) {
      if (h && h.layers) {
        for (const id of ALL_TOGGLES) {
          const on = h.layers.has(id);
          if (initial) {
            if (this.layers[id]) this.layers[id].visible = on;
            else if (RASTER_IDS.includes(id)) { this.rasterVisible[id] = on; if (on && this.rasters[id]) this.rasters[id].addTo(this.map); }
            else if (id === 'grid') this.grid.visible = on;
          } else if (this.isVisible(id) !== on) this.setVisible(id, on);
        }
      } else if (initial) {
        this.grid.visible = true;
      }
      const o = h ? h.opts : new URLSearchParams();
      if (o.get('road')) this.setRoadMode(o.get('road'));
      if (o.get('bldg')) this.setBuildingMode(o.get('bldg'));
      if (o.get('base')) this.setBase(o.get('base'));
      if (o.get('base_op') !== null) this.setBaseOpacity(Math.min(100, Math.max(0, Number(o.get('base_op')) || 0)) / 100);
      if (o.get('hs_op') !== null) this.setRasterOpacity('hillshade', Math.min(100, Math.max(0, Number(o.get('hs_op')) || 0)) / 100);
      if (o.get('bio_op') !== null) this.setRasterOpacity('biome', Math.min(100, Math.max(0, Number(o.get('bio_op')) || 0)) / 100);
      if (o.get('grid') !== null && Number.isFinite(Number(o.get('grid')))) {
        const lv = Number(o.get('grid'));
        if (![...document.getElementById('grid-level').options].some((op) => op.value === String(lv))) {
          document.getElementById('grid-level').insertAdjacentHTML('beforeend', `<option value="${lv}">level ${lv} · computed</option>`);
        }
        this.setGridLevel(lv);
      }
      if (h) this.map.setView([h.lat, h.lon], h.z, { animate: false });
      this.refreshLayerList();
      this.renderLegend();
      if (!initial) this.scheduleUpdate();
    }

    writeHash() {
      if (!this.index || !this.map || this._applyingHash) return;
      const c = this.map.getCenter();
      const z = this.map.getZoom();
      const d = z >= 16 ? 6 : z >= 12 ? 5 : 4;
      const layers = ALL_TOGGLES.filter((id) => this.isVisible(id));
      const o = new URLSearchParams();
      o.set('road', this.roadMode);
      o.set('bldg', this.buildingMode);
      o.set('base', this.base);
      o.set('base_op', String(Math.round(this.baseOpacity * 100)));
      o.set('hs_op', String(Math.round(this.rasterOpacity.hillshade * 100)));
      o.set('bio_op', String(Math.round(this.rasterOpacity.biome * 100)));
      if (this.grid.level !== null) o.set('grid', String(this.grid.level));
      const h = `#${z}/${c.lat.toFixed(d)}/${c.lng.toFixed(d)}/${layers.join(',')}/${o.toString()}`;
      if (h !== this._lastHash) {
        this._lastHash = h;
        history.replaceState(null, '', h);
      }
    }
  }

  // ===========================================================================
  // 9. Bootstrap
  // ===========================================================================
  const app = new App();
  window.QA = {
    app,
    get map() { return app.map; },
    get layers() { return app.layers; },
    get rasters() { return app.rasters; },
    get grid() { return app.grid; },
    get index() { return app.index; },
    get errors() { return app.errors; },
    ready: app.ready,
    idle: () => app.idle(),
    setLayerVisible: (id, on) => app.setVisible(id, on),
    setRoadMode: (m) => app.setRoadMode(m),
    setBuildingMode: (m) => app.setBuildingMode(m),
    setBase: (b) => app.setBase(b),
    search: (q) => app.search(q),
    select: (id, i) => app.select(id, i, null, true),
    styleOf: (id, i) => app.layers[id].style(i),
    proj,
    util: { normEnum, flagNames, featureNames, normRoad, normBuilding, normPoi, normArea, normLine, normPlace,
      parseColor, osmRefOf, GridIndex, ENUMS },
    palettes: { SURFACE_COLORS, SOURCE_COLORS, CLASS_COLORS, CLASS_WIDTH, SAC_COLORS, ARCHETYPE_COLORS, LEVEL_COLORS,
      POI_GROUPS, AREA_COLORS, LINE_STYLES },
  };
  app.start().catch((err) => {
    app.fail(`Viewer failed to start: ${err && err.message ? err.message : err}`);
    console.error(err);
    app.readyResolve();
  });
})();
