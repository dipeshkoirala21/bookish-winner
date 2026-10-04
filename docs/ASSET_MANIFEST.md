# Ghumante: asset manifest

> Status: **M0 draft, 2026-10-04.** This is a living document. Art works from it in parallel with engineering (ROADMAP, "Cross-cutting tracks"). Update the **Status** cell in the same PR that lands or changes an asset.
> Sources: [ARCHITECTURE](ARCHITECTURE.md) (§7.5 buildings, §7.9 UI, §8 rendering style, §10 budgets) · [ROADMAP](ROADMAP.md) (milestones) · [Landmark check](reports/landmarks.md) (OSM ids and coordinates) · enums in `pipeline/ghumante_pipeline/model.py`, mirrored in `shared/enums.json` · [LICENSES](LICENSES.md) (fonts, flag, attribution).

## 0. How to read this manifest

| Field | Meaning |
|---|---|
| **id** | File stem and Addressables key, `ghm_<category>_<name>_<variant>` (§1.1). `{a,b}` means one row covers several variants with the same spec. |
| **M** | The first milestone that needs the asset: M1, M2, M3a, M3b, M4, M5 or M6 (ROADMAP). Later milestones reuse it. |
| **P** | Priority *within that milestone*. **P0**: the milestone fails acceptance without it. **P1**: expected; a greybox may ship if it is late. **P2**: nice to have; may slip one milestone. |
| **Tris** | Triangle ceilings per LOD, written `LOD0/LOD1/LOD2`. `imp` = impostor or billboard. `proc` = generated at runtime from data; art supplies only pieces or parameters. These are ceilings, not targets. |
| **Status** | `needs-art`: must be authored. `placeholder`: a greybox or stand-in is acceptable for its milestone, and the note says when final art is due. `procedural`: produced by code or the pipeline; art supplies only inputs (palette rows, kit pieces, parameters). Nothing in this draft is final yet. |
| **(review)** | A cultural detail the consultant must confirm before final art (ARCHITECTURE §14 Q9, ROADMAP 1.15). **(verify)** marks a technical or factual detail to check against photos, device tests or a current source. |

**Rules for every asset.** Everything is generic and unbranded: no real manufacturer marks, shop brands, airline liveries or protected emblems (the Red Cross / Red Crescent emblem is never used; medical places use a generic green cross). Wildlife is observation-only. Sacred places follow their real access rules. The Nepal flag is generated from its constitutional construction (§12.7). Everything is depicted for a 9+ / PEGI 7 audience (ARCHITECTURE P11).

---

## 1. Conventions and budgets

### 1.1 Naming

Pattern: `ghm_<category>_<name>_<variant>`, lower snake case, ASCII only. LOD meshes add `_LOD0`, `_LOD1`, `_LOD2`, which lets Unity build the LODGroup on import. Collision meshes add `_COL`. Sockets (empty transforms) are named `sock_<name>`, IK targets `ik_<name>`. Animation clips are `ghm_anim_<rig>_<clip>`.

| Code | Category | Example |
|---|---|---|
| `ter` | Terrain biome entry (palette row and parameters) | `ghm_ter_hill_terraces` |
| `rd` / `dcl` | Road surfaces and road kit / decals | `ghm_rd_brick`, `ghm_dcl_pothole_a` |
| `bld` / `roof` | Building kit pieces / roof generator pieces | `ghm_bld_newar_window_sanjhya_a`, `ghm_roof_pagoda_tier` |
| `lmk` | Hero landmarks (one folder per landmark) | `ghm_lmk_boudhanath` |
| `veg` | Vegetation | `ghm_veg_rhododendron_tree_a` |
| `prp` | Props | `ghm_prp_prayerwheel_hand` |
| `veh` | Vehicles | `ghm_veh_scooter_a` |
| `chr` / `anim` | Characters and outfits / animation clips | `ghm_chr_npc_porter_a`, `ghm_anim_hum_walk` |
| `ani` | Animals and birds | `ghm_ani_rhino` |
| `sky` / `wat` / `vfx` | Sky and weather / water / particles and shader effects | `ghm_vfx_dust_dirt` |
| `ui` / `ico` / `map` | UI sprites / icons / map style | `ghm_ui_btn_pill_yellow`, `ghm_ico_poi_stupa` |
| `tex` / `mat` | Shared textures / materials | `ghm_tex_palette_main`, `ghm_mat_toon_lit` |
| `mus` / `amb` / `sfx` / `vo` | Music / ambience / sound effects / voice | `ghm_amb_ktm_street_day` |
| `fnt` | Fonts | `ghm_fnt_baloo2` |

### 1.2 Units, axes and pivots

| Rule | Spec |
|---|---|
| Units | 1 unit = 1 m. Import scale 1.0. Every DCC scene starts from `ghm_ref_mannequin` (1.70 m human, 1 m cube, 0.18 m step) (verify the export preset round-trips). |
| Axes | Y up. Assets face **+Z** in Unity (front of a vehicle, character, door side of a kit wall). +X is the asset's right. |
| Real size | Footprints, heights, wheelbases and door sizes are real-world size, because footprints come from OSM and the camera reads scale off doors and people. "Chunky" exaggeration applies to details only: frames, trims, railings and carvings at 1.2–1.5× real thickness. |
| Kit pieces | Pivot at the bottom-left corner of the exterior face. The exterior faces +Z and the width runs along +X. Widths are 1, 2 or 4 m on a 0.5 m snap grid. Storey heights are set per archetype (§4.1). |
| Props / trees | Bottom centre at ground contact. Trees and posts extend 0.2 m below ground so slopes don't show gaps. |
| Characters / animals | On the ground between the feet (animals: under the centre of mass), facing +Z. |
| Vehicles | On the ground at the midpoint between the axles, facing +Z. Wheels, steering and suspension are child transforms (§8). Aircraft: on the ground under the centre of gravity at rest. |
| Landmarks | Pivot at the footprint centroid used by `landmarks.resolved.json`, at the lowest ground contact. Model with +Z = grid north and +X = east (NPL-TM84) so the mesh overlays the OSM footprint. The runtime applies the resolved position and orientation (ARCHITECTURE §7.5). A footprint guide (OSM outline exported to FBX) is a pipeline request. |

### 1.3 File formats and export

| Asset | Delivery format | Import rules |
|---|---|---|
| Static meshes | FBX (binary) or glTF 2.0 `.glb` (static only, if engineering adopts glTFast (verify)) | Tangents **None** (the toon shaders use no normal maps), Read/Write **off** (ARCHITECTURE §10), mesh compression Medium, one mesh per LOD, smoothed outline normals in UV2 (§1.9) |
| Skinned meshes and animation | FBX only | 30 fps, locomotion in place (no root motion), Humanoid avatar for people and Generic for animals and vehicles. Clips per §1.12 |
| Textures | PNG or TGA from layered sources (PSD, Krita) | ASTC only (§1.6). sRGB for colour; linear for masks, noise and VAT. UI has no mipmaps |
| Vector art (icons, stamps, flag, UI shapes) | SVG plus PNG exports at 64, 128 and 256 px | Exports go into sprite atlases (ASTC 4×4, no mipmaps) |
| Audio | WAV 48 kHz / 24-bit masters | Unity compression per §13.1 |
| Fonts | Upstream TTF/OTF with `OFL.txt` | §14 |
| Sources | `.blend`, `.psd`, `.kra`, `.svg`, `.wav` masters | Kept outside `Assets/` in the art source store (Git LFS or external: **decision**). Never imported into the game project |

### 1.4 Frame triangle budget split per tier

Visible triangles per frame, per ARCHITECTURE §10 (Low ≤ 150 k, Mid ≤ 400 k, High ≤ 700 k). The LOD distances and per-tier caps in §1.11 keep each slice inside its column. NPC and vehicle counts in ARCHITECTURE §10 are simulation caps, not visible counts.

| Slice | Low (150 k) | Mid (400 k) | High (700 k) | What fills it |
|---|---|---|---|---|
| Terrain (CDLOD chunks + skirts) | 30 k | 70 k | 110 k | Procedural |
| Roads, trails, road kit, decals | 8 k | 20 k | 30 k | Procedural ribbons + §3 kit |
| Generated buildings (kit LOD0 near, merged LOD1/LOD2) | 32 k | 95 k | 170 k | §4 |
| Hero landmarks in view | 20 k (LOD1 max) | 40 k | 60 k | At most one class-A landmark at LOD0 (Mid/High); complex parts LOD independently |
| Vegetation (trees, impostors, crops, grass) | 20 k | 60 k | 110 k | §6 |
| Props | 8 k | 25 k | 45 k | §7 |
| Characters (player + NPCs + crowds) | 15 k | 40 k | 80 k | High: player 5 k + 6 NPCs at LOD0 + 20 at LOD1 + ~50 VAT |
| Vehicles (player + traffic) | 10 k | 30 k | 55 k | High: player 6 k + 4 at LOD0 + 10 at LOD1 + 8 at LOD2 |
| Animals and birds | 3 k | 10 k | 20 k | §10; flocks as VAT |
| Sky, skyline ring, water, VFX | 4 k | 10 k | 20 k | §11 |
| **Total** | **150 k** | **400 k** | **700 k** | — |

### 1.5 Per-asset-class budgets

| Class | LOD0 | LOD1 | LOD2 | Far | LOD switch (screen height) | Rig | Textures | Outline (§1.10) |
|---|---|---|---|---|---|---|---|---|
| Hero landmark, single (class A) | **25 000** | 8 000 | 2 000 | merged box or impostor ≤ 200 | 0.35 / 0.12 / 0.04 | — | palette + one detail atlas ≤ 1024² | all tiers |
| Hero complex part (class B: squares, walled towns) | 12 000 per part (one centrepiece part ≤ 25 000); ≤ 60 000 per complex | 4 000 | 1 000 | merged ≤ 300 per part | 0.30 / 0.10 / 0.03 | — | palette + shared detail atlas ≤ 1024² per complex | all tiers |
| Landmark-lite (class C: towers, caves, terminals) | 8 000 | 2 500 | 600 | merged | 0.25 / 0.08 / 0.02 | — | palette + ≤ 512² detail | Mid/High |
| Natural or area landmark (class D: peaks, lakes, rivers, parks) | no hero mesh: DEM terrain, skyline ring, water; dressing ≤ 3 000–12 000 | dressing ~30 % | — | skyline ring | as props | — | palette | — |
| Activity rig (class E: cable cars, bungee, zipline) | 10 000 | 3 000 | 800 | cables as lines | 0.25 / 0.08 / 0.02 | generic (cabins) | palette | Mid/High |
| Generated building (§4) | 3 000 (Newar 5 000; religious 9 000) | merged per tile, ~300 per building | blocks, ~24 per building | — | kit near ring only | — | palette + archetype detail atlas | none (landmarks only) |
| Vehicle | **6 000** (aircraft and helicopter 8 000) | 2 000 | 600 | cull | 0.20 / 0.08 / 0.02 | transform hierarchy | palette + paint mask + decal atlas | Mid/High |
| Player character | **5 000** + 600 accessories | 2 000 | — | — | 0.15 / — | Humanoid ≤ **40** bones, 4 weights | palette + face + pattern atlas | Mid/High |
| NPC | 3 500 | 1 500 | 500 (VAT crowd) | cull | 0.15 / 0.06 / 0.02 | Humanoid ≤ 40 bones, 2 weights from LOD1 | palette + face atlas | Mid/High |
| Large animal (rhino, elephant, yak) | **4 000** | 1 500 | 500 (VAT herds) | cull | 0.15 / 0.06 / 0.02 | Generic ≤ **25** bones | palette | Mid/High |
| Medium animal (deer, macaque, dog) | 2 500 | 1 000 | 300 | cull | same | Generic ≤ 25 bones | palette | High |
| Small animal (chicken, red panda) | 1 200 | 500 | 150 | cull | same | Generic ≤ 16 bones | palette | High |
| Bird (individual) | 800 | 300 | 80 (VAT) | cull | same | Generic ≤ 12 bones | palette | — |
| Flock bird (pigeons, crows) | 300 (VAT) | 80 | — | cull | 0.04 | baked VAT | palette | — |
| Prop, small / medium / large | 500 / 1 000 / **1 500** | ~35 % | ~10 % or cull | — | 0.10 / 0.04 / 0.01 | — | palette (+ detail only where listed) | High only |
| Tree | **1 500** | 500 | 150 | impostor quad (mid ring) | 0.20 / 0.08 / 0.03 | — | palette/gradient + impostor atlas | — |
| Shrub, crop patch, grass clump | 400 | 120 | — | cull or terrain colour | 0.06 / 0.02 | — | palette/gradient | — |

### 1.6 Textures: palette-first

The cartoon style needs very few unique textures. Colour comes from **shared 256×256 palette and gradient atlases**: faces are UV-collapsed onto swatch centres, so hundreds of meshes share one material and batch or instance together (SRP Batcher, GPU instancing, BatchRendererGroup per ARCHITECTURE §10). Unique textures exist only where the palette cannot carry the detail (carvings, painted motifs, faces, signage, truck art), and every one is listed below or in its section. ASTC only (ARCHITECTURE §10): 4×4 for UI and faces, 6×6 for terrain, building and landmark detail, 8×8 for far content. Low tier drops one mip through the mipmap limit groups `world_detail` and `hero`. The `ui` group is never reduced.

| id | Asset | M | P | Size / format | Use | Status |
|---|---|---|---|---|---|---|
| `ghm_tex_palette_main` | Main palette | M1 | P0 | 256², 16×16 swatches of 16 px, ASTC 4×4, no mips, point filter, sRGB | All static and skinned meshes. Swatch map in §1.9 tokens | needs-art |
| `ghm_tex_palette_gradient` | Gradient strips | M1 | P0 | 256², 32 vertical strips × 8 px, ASTC 4×4, bilinear, no mips | Foliage top-to-bottom shading, roof tiles, cloth, sky-facing gradients | needs-art |
| `ghm_tex_ramp_atlas` | Toon ramps | M1 | P0 | 256×64, 16 ramps × 4 px rows, RGBA32 uncompressed (64 KB) | Default, skin, foliage, snow, water, metal, cloth, stone, night, etc. | needs-art |
| `ghm_tex_biome_palette` | Biome palette | M1 | P0 | 256×32 RGBA32 (one row per `Biome`) | Terrain (§2) | needs-art |
| `ghm_tex_noise_world` | World noise | M1 | P0 | 256², linear, ASTC 6×6. R macro blotches, G grain, B brush strokes, A strata/cracks | Terrain, roads, walls (breaks up flat palette colour) | needs-art |
| `ghm_tex_road_trim_atlas` | Road trim atlas | M1 | P0 | 1024² (4×4 tiles of 256²), ASTC 6×6 | §3 | needs-art |
| `ghm_tex_decal_atlas` | Road decals | M1 | P0 | 1024², ASTC 6×6 | §3 | needs-art |
| `ghm_tex_detail_<archetype>` | Archetype detail atlases (one per §4 archetype) | M1 | P0 | 512², ASTC 6×6 | Carvings, lattices, painted motifs, Buddha eyes, signage slots | needs-art |
| `ghm_tex_facade_atlas` | LOD2 facade atlas | M1 | P1 | 1024², ASTC 8×8 | Merged block LOD2 (ARCHITECTURE §7.5). Baked from kit renders, then painted over | procedural |
| `ghm_tex_signage_atlas` | Generic shop signage | M1 | P0 | 1024², ASTC 6×6 | Nepali and English generic signs (§4, §7). Devanagari typeset with a shaping tool and checked by a native reader (review) | needs-art |
| `ghm_tex_lmk_<id>_detail` | Landmark detail | M1 | P0 | ≤ 1024² (A, B) or ≤ 512² (C), ASTC 6×6 | One per landmark (§5) | needs-art |
| `ghm_tex_veg_impostor_{albedo,normal}` | Tree impostors | M1 | P0 | 2048² albedo+alpha and 1024² normal, ASTC 8×8, 8 views per species | Mid-ring trees (ARCHITECTURE §7.2) | procedural |
| `ghm_tex_face_atlas` | Faces | M1 | P0 | 512², ASTC 4×4 | Eyes, brows and mouths swapped by UV offset (§9) | needs-art |
| `ghm_tex_pattern_atlas` | Textile patterns | M1 | P1 | 512², ASTC 6×6 | Dhaka weave, sari borders, hakupatasi border, Tharu and Sherpa textiles (review) | needs-art |
| `ghm_tex_truckart_atlas` | Truck and bus art | M2 | P0 | 1024², ASTC 6×6 | §8 | needs-art |
| `ghm_tex_vfx_atlas` | VFX flipbooks | M1 | P0 | 1024², ASTC 6×6 | Dust, mud, spray, sparkles, smoke (§11) | needs-art |
| `ghm_tex_vat_<set>` | Vertex animation textures | M1 | P1 | RGBAHalf positions + RGBA8 normals, uncompressed | Crowds, herds, flocks (§9, §10) | procedural |

### 1.7 Materials (artists use only these)

Shaders are owned by engineering. Artists choose one of these materials and set palette UVs, vertex colours and the listed parameters. They never add a custom shader.

| id | Material | M | P | Used by | Key parameters | Status |
|---|---|---|---|---|---|---|
| `ghm_mat_toon_lit` | Toon lit (palette) | M1 | P0 | Kit pieces, props, vehicles | ramp row, rim, wetness, snow cover, instanced tint | needs-art |
| `ghm_mat_toon_lit_detail` | Toon lit + detail atlas | M1 | P0 | Buildings, landmarks | detail atlas multiply/overlay, emissive mask (night windows) | needs-art |
| `ghm_mat_toon_skin` | Toon skinned | M1 | P0 | Characters, animals | face UV offset, tint masks, skin ramp | needs-art |
| `ghm_mat_toon_foliage` | Foliage | M1 | P0 | Trees, shrubs, crops, prayer flags on poles | wind (§1.9), season row, bloom mask | needs-art |
| `ghm_mat_toon_terrain` | Terrain | M1 | P0 | Terrain chunks | biome index, noise scale, slope-rock angles, triplanar (Mid/High, steep only) | procedural |
| `ghm_mat_toon_road` | Road | M1 | P0 | Road and trail ribbons | trim tile index, puddle mask, wetness | procedural |
| `ghm_mat_toon_water` | Water | M1 | P0 | Lakes, rivers, waterfalls | depth ramp, foam, flow, sparkle (§11) | needs-art |
| `ghm_mat_outline` | Inverted-hull outline pass | M1 | P0 | §1.10 | width (px, clamped), colour | needs-art |
| `ghm_mat_cloth_flutter` | Cloth flutter | M1 | P0 | Flags, prayer flags, laundry, banners | flutter amplitude, frequency | needs-art |
| `ghm_mat_impostor` | Impostor | M1 | P0 | Trees, far landmark cards | view count, alpha clip | procedural |
| `ghm_mat_vat_crowd` | VAT instanced | M1 | P1 | Crowds, herds, flocks | clip index, phase, tint | procedural |
| `ghm_mat_fx_{alpha,add}` | Particles | M1 | P0 | §11 | soft particles off on Low | needs-art |
| `ghm_mat_sky` | Sky gradient | M1 | P0 | Sky dome | gradient row, sun/moon disc | procedural |

### 1.8 Memory and install budgets for art

Runtime memory comes from ARCHITECTURE §10. The palette-first approach keeps authored textures far below the texture budget. The reserve absorbs tile-transition peaks, which are what trigger out-of-memory kills.

| Texture memory (MB) | Low (200) | Mid (350) | High (450) |
|---|---|---|---|
| UI atlases + font atlases | 30 | 40 | 45 |
| Map (paper, hatch, fog-of-discovery masks, pins) | 12 | 18 | 24 |
| Terrain, roads, decals | 10 | 16 | 20 |
| Buildings (palette, detail, facade, signage) | 12 | 20 | 26 |
| Landmarks resident | 10 | 18 | 26 |
| Vegetation impostors | 6 | 10 | 14 |
| Characters, animals, VAT | 14 | 24 | 32 |
| Vehicles | 6 | 10 | 14 |
| Sky, water, VFX | 8 | 14 | 18 |
| Loading screen, photo-mode frames and LUTs | 4 | 6 | 8 |
| Reserve (transient peaks, render-target growth) | 88 | 174 | 223 |

| Mesh memory (MB) | Low (100) | Mid (180) | High (250) |
|---|---|---|---|
| Procedural world (terrain, roads, merged buildings, skyline) | 55 | 100 | 140 |
| Building kit pieces | 8 | 12 | 15 |
| Landmarks resident | 10 | 20 | 30 |
| Vegetation + props | 8 | 14 | 18 |
| Characters + animals (incl. skinned) | 8 | 14 | 20 |
| Vehicles | 5 | 10 | 14 |
| VFX, sky, misc | 3 | 5 | 6 |
| Reserve | 3 | 5 | 7 |

| Install size (compressed, estimate) | Budget | Notes |
|---|---|---|
| Base install total | **≤ 150 MB** | ARCHITECTURE §9 |
| — Engine + code | ≤ 35 MB | estimate (verify on the first IL2CPP build) |
| — Kathmandu Valley data pack | ≤ 60 MB | ARCHITECTURE §9 |
| — Shared art kits (buildings, props, vegetation, M1 vehicles, characters, animals) | ≤ 20 MB | `base_shared` Addressables group |
| — M1 hero landmarks | ≤ 10 MB | `region_kathmandu_valley` group, shipped built-in |
| — UI, icons, fonts | ≤ 8 MB | fonts ≈ 2–3 MB of that |
| — Audio (M1) | ≤ 12 MB | §13 |
| — Slack | ≥ 5 MB | — |
| Each later region's art bundle | ≤ 25 MB art + ≤ 8 MB audio | downloaded with the region pack |

### 1.9 Vertex colours, UVs and palette tokens

| Channel | Static (kit, props, vehicles, landmarks) | Vegetation and flags | Characters and animals |
|---|---|---|---|
| UV0 | Palette or gradient UV (collapsed to swatch centres) | Palette or gradient UV | Palette UV; face region on the face atlas |
| UV1 | Detail atlas UV (only if the row lists a detail texture) | — | Pattern atlas UV |
| UV2 | Smoothed normals (xyz) for the outline pass | Wind pivot (xyz, object space) | Smoothed normals for the outline pass |
| UV3 | — | — | Vertex id for VAT bakes (crowd and herd LOD2 only) |
| Vertex colour R | Baked soft AO (0 = occluded) | AO | AO |
| G | Tint mask (1 = takes instanced paint/tint) | Trunk and branch sway weight | Clothing recolour mask |
| B | Wetness and porosity (how much rain darkens it) | Leaf or cloth flutter weight | Skin mask |
| A | Emissive mask (windows at night, lamps) | Season or bloom mask (1 = blossom or autumn colour) | — |

Colour tokens (provisional until the art director locks the palette, ARCHITECTURE §14 Q8). The direction is bright, saturated and warm, with soft cool shadows.

| Token | Hex | Use | — | Token | Hex | Use |
|---|---|---|---|---|---|---|
| `ui.cream` | #FFF3D6 | Panel fill | — | `w.brick.newar` | #B4543A | Newar brick |
| `ui.cream.edge` | #E9CC97 | Panel border | — | `w.tile.roof` | #9E4630 | Clay roof tiles |
| `ui.cream.shadow` | #D7B57A | Panel inner shadow | — | `w.wood.carved` | #6A3E22 | Carved wood |
| `ui.ink` | #4A2C1A | Text, outlines | — | `w.whitewash` | #F6F0E2 | Whitewash, stupa body |
| `ui.ribbon` / `.dark` | #E8483A / #A92F25 | Ribbon headers | — | `w.claywash.red` / `w.ochre` | #B3532E / #CF8A3A | Hill-house lower walls |
| `ui.pill.yellow` / edge | #FFC83D / #D3920E | Primary button | — | `w.mud` | #A97C52 | Mud plaster, rammed earth |
| `ui.pill.cyan` / edge | #3FC6EA / #1C8DB5 | Secondary/info | — | `w.stone` / `w.slate` | #9C968C / #5E646B | Stone walls / slate roofs |
| `ui.pill.green` / edge | #72CC47 / #3E962A | Go / confirm | — | `w.thatch` | #C9A35A | Thatch |
| `ui.pill.white` / edge | #FFFFFF / #C9D3DC | Neutral / cancel | — | `w.paint.*` | #F49AC1 #3CC9C0 #B6E05A #FFD95A #7FC8F8 #B79CE8 | Modern facade paints |
| `ui.pill.red` / edge | #F2584A / #B23224 | Destructive / close | — | `w.tank.black` / `.blue` | #2A2A2E / #2F6FD6 | Rooftop water tanks |
| `ui.heart` | #F0435B | Hearts | — | `w.metal.roof.*` | #3D7CC9 #3FA35C #C9433A #A2603A | Painted corrugated roofs |
| `ui.star` | #FFC21C | Stars | — | `w.gold` / `w.saffron` | #E9B23B / #F2A33A | Gilding / saffron |
| `ui.coin` | #F4B029 | Coins | — | `w.gompa.maroon` | #8E2F2A | Gompa walls and robes |
| `ui.energy` | #48C7F0 | Energy | — | `w.flag.*` | #2F6FD0 #FFFFFF #E2392C #2BA34F #FFD53D | Prayer flags (blue, white, red, green, yellow) |
| `map.paper` | #F5E9CF | Map base | — | `w.snow` / `.shadow` / `w.alpenglow` | #FFFFFF / #B7CFF0 / #FFB07A | Snow, snow shade, sunrise rim |
| `map.ink` | #5A3B26 | Map outlines | — | `w.green.sal` / `.pine` / `.paddy` | #4F9B3B / #2E7A4E / #9BE05A | Foliage |
| `map.water` | #8FCDEB | Map water | — | `w.paddy.gold` / `w.mustard` | #E7C34A / #F8D733 | Ripe paddy / mustard fields |
| `map.fog` | #E9DFC8 | Undiscovered | — | `w.rhodo.red` / `w.marigold` | #E0364C / #FF9E1B | Rhododendron / marigold |
| `flag.crimson` | #DC143C | Nepal flag field (review) | — | `w.water.lake` / `.glacial` / `.monsoon` | #3FA9D6 / #40D0C8 / #A58155 | Water (§11) |
| `flag.blue` | #003893 | Nepal flag border (review) | — | `w.water.seti` | #CFE3E0 | Milky Seti river |

### 1.10 Outlines

Outlines are an inverted-hull pass (`ghm_mat_outline`), per ARCHITECTURE §8 and §10: **Low** = landmarks only; **Mid** = characters, vehicles, landmarks; **High** = the same plus props. Buildings, terrain and vegetation never get outlines. Width is 1–3 px at 1080p, clamped by distance, and drawn only at LOD0 and LOD1. Colour is `ui.ink` at 85 %, or the material's darkened albedo. Any mesh that gets an outline ships smoothed normals in UV2, so hard edges don't split the hull. Thin cards and cloth strips are excluded. Whether to outline everything is open (ARCHITECTURE §14 Q8).

### 1.11 LOD and instancing rules

* Every mesh over 300 triangles ships LODs, except kit pieces: their LOD1 and LOD2 are merged procedurally per tile (ARCHITECTURE §7.5). LOD1 ≈ 30–35 % of LOD0 and LOD2 ≈ 8–10 %. Keep the silhouette and the palette UVs. LOD2 drops the detail atlas.
* Switch heights per class are in §1.5. High uses dithered cross-fade. Low and Mid hard-switch with hysteresis. Low sets `maximumLODLevel = 1` for world content; the player character and the current vehicle are exempt.
* Every repeated mesh shares one mesh and one material, so it can be GPU-instanced or drawn through BatchRendererGroup. Per-instance variety comes only from instanced properties (tint, season, wetness, paint), never from material copies.
* No alpha clipping on kit pieces, props, characters or vehicles: it is expensive on tile-based mobile GPUs. Lattices and fringes are painted on opaque insets. Alpha clip is allowed only on impostors and particles.
* Crowds, herds and flocks use vertex animation textures at LOD2 (§9.5, §10).

### 1.12 Animation conventions

* 30 fps. Locomotion is in place, and the controller drives movement. Loops have matching first and last frames and are marked **L** in tables. Frame counts are at 30 fps.
* Rig codes: `hum` (humanoid), `quadL` (large quadruped), `quadM`, `prim` (primate), `bird`, `croc`, `fish`, `veh`.
* **Squash and stretch** is part of the style. Every rig has a `squash` bone under the root that allows non-uniform, volume-preserving scale. The runtime springs it on landings, bumps and honks, and authored clips may key it. No other bone is scaled.
* Animation events: `fs_L` / `fs_R` (footsteps, §13), `sfx_<name>`, `vfx_<name>`.
* Faces are swapped through the face atlas (blink, smile, surprise, tired, two talk shapes), not blend shapes. Blend shapes are not used on mobile crowds.

### 1.13 Delivery folder structure

```
game/Assets/Ghumante/Art/
├── _Shared/        Palettes/ Ramps/ Noise/ Materials/ Reference/ (mannequin, scale cube; not shipped)
├── Terrain/        BiomePalette/ GroundScatter/
├── Roads/          TrimAtlas/ Decals/ Kit/ (kerbs, steps, bridges, gabions)
├── Buildings/<Archetype>/   Meshes/ Textures/ Prefabs/   (grammar ScriptableObjects are owned by engineering)
├── Landmarks/<region>/<landmark_id>/   Meshes/ Textures/ Prefabs/
├── Vegetation/<species>/
├── Props/<group>/  Religious/ Street/ Utility/ Transport/ Trekking/ Agriculture/ Water/ Festival/
├── Vehicles/<class>/   Meshes/ Textures/ Prefabs/ Paint/
├── Characters/     Rigs/ Player/ Outfits/ NPC/<archetype>/ Animations/<rig>/ VAT/
├── Animals/        Wildlife/<species>/ Domestic/<species>/ Birds/<species>/ Animations/ VAT/
├── Sky/  Water/  VFX/
├── UI/             Sprites/ Icons/<group>/ Stamps/ Badges/ Cards/ PhotoMode/ Map/ Loading/ AppIcon/ Flag/
├── Audio/          Music/<region>/ Ambience/<biome>/ SFX/<group>/ UI/ VO/
├── Fonts/          Baloo2/ Mukta/ NotoSansDevanagari/   (each with OFL.txt)
└── _Placeholders/  greybox stand-ins; delete each one when its final art lands
```

Addressables groups: `base_shared` (everything not region-specific; ships in the base install) and `region_<id>`, which holds landmarks, region-only kits and region audio, downloaded with the region pack. Region ids follow `pipeline/config/landmarks.yaml`: kathmandu_valley, prithvi_corridor, pokhara, annapurna, mustang, khumbu, langtang, manaslu, far_west, lumbini, chitwan, bardiya, janakpur, east, kalinchowk, bhote_koshi.

**Import validator (CI, engineering).** It checks naming, scale, triangle ceilings per LOD, LOD presence, Read/Write off, tangents off, palette UVs inside swatches, outline normals where required, required sockets, and that every unique texture is listed in this manifest.

---
## 2. Terrain and biome materials

Terrain meshes are procedural (CDLOD, ARCHITECTURE §7.3). One terrain material (`ghm_mat_toon_terrain`) reads the per-vertex `Biome` and looks up its row in `ghm_tex_biome_palette`. Each row below is one "toon ground material": a palette row plus parameters. Art authors the row and its parameters.

**Palette row layout** (256×32, RGBA32, one row per `Biome` value, rows 28–31 spare): eight 32 px swatches, in order **spring** (Mar–May, pre-monsoon), **monsoon** (Jun–Sep, lush and wet), **autumn** (Oct–Nov, harvest and festivals), **winter** (Dec–Feb), **slope** (steep soil), **rock**, **wet tint** (multiplied in by rain wetness) and **map colour** (§12.5). The calendar is approximate and blended over two weeks. **Detail:** `ghm_tex_noise_world`, planar XZ, with triplanar only inside the slope-rock blend band on Mid/High. **Snow:** a global snowline per region and season plus an up-facing mask, using `w.snow` / `w.snow.shadow`. **Monsoon:** darken via the wet tint, add a highlight band to the ramp, and draw puddle masks on flat ground. The slope-rock blend columns give start–end angles.

| id | Biome (value) | M | P | Seasonal flat colours (spring / monsoon / autumn / winter) | Slope / rock | Detail noise and strokes | Slope rock blend | Weather variants and notes | Status |
|---|---|---|---|---|---|---|---|---|---|
| `ghm_ter_none` | NONE (0) | M1 | P0 | copies HILL_GRASSLAND; magenta in dev builds | — | — | — | A visible error colour helps QA | needs-art |
| `ghm_ter_water` | WATER (1) | M1 | P0 | bed #6E8C6A, bank band #8A7A5C | #7A6A52 / #8C8478 | silt blotches | 25–40° | Lake and river beds under the water shader (§11). Monsoon: high-water band on banks | needs-art |
| `ghm_ter_urban_dense` | URBAN_DENSE (2) | M1 | P0 | #C9B9A0 all year; dusty | #B49C7C / #9C968C | grit, tyre-worn patches | 35–50° | Monsoon: dark wet + puddles. Dry season: dust haze tint | needs-art |
| `ghm_ter_urban_green` | URBAN_GREEN (3) | M1 | P0 | #9CCB63 / #7FC456 / #A9C463 / #B9B56E | #8C7A5A / #9C968C | lawn strokes, worn dirt paths | 35–50° | Parks, Garden of Dreams lawns | needs-art |
| `ghm_ter_terai_paddy` | TERAI_PADDY (4) | M4 | P0 | dry stubble #CDB27A / flooded #7FD0C0 + shoots #8BD65A / ripe #E7C34A / fallow #C9A877 | #9A8460 / #A08E74 | bund lines (field edges), furrows | 15–30° | Monsoon flooded paddies use the puddle mask at full strength plus a shoot-card scatter (§6). Sky reflection on High | needs-art |
| `ghm_ter_terai_cropland` | TERAI_CROPLAND (5) | M4 | P0 | wheat gold #DDBE5A / maize green #7CC24E / fallow #C8A774 / mustard #F8D733 | #9A8460 / #A08E74 | furrow stripes | 15–30° | Winter mustard is a signature look of the Terai | needs-art |
| `ghm_ter_terai_sal_forest` | TERAI_SAL_FOREST (6) | M4 | P0 | dry-leaf litter #C98B3A / lush #557F33 / #6E7A3A / #8E7A3E | #7D6A3A / #A08E74 | leaf-litter strokes | 25–40° | Sal sheds leaves around spring, so the floor goes orange (verify timing) | needs-art |
| `ghm_ter_terai_grassland` | TERAI_GRASSLAND (7) | M4 | P0 | #B8B35A / #7FB24A / #A6B04E / #C2A86A | #9A8460 / #A08E74 | tall-grass strokes | 20–35° | Phanta grasslands (Chitwan, Bardiya, Shuklaphanta). Optional burnt-patch variant in winter (review the controlled-burn depiction) | needs-art |
| `ghm_ter_riverbed_gravel` | RIVERBED_GRAVEL (8) | M2 | P0 | #CFC7B8 shingle, #E0D3B0 sand bars (all seasons) | #B8AE9C / #A39E96 | pebble speckle, braided channel strokes | 30–45° | Monsoon: wider wet band and a higher water mesh. Dry: pale and bright | needs-art |
| `ghm_ter_chure_forest` | CHURE_FOREST (9) | M2 | P1 | #6E8A3E / #4F8A3A / #7A8A44 / #8A7E4A | #A8653F / #B08A6A | crumbly soil strokes | 25–40° | Siwalik hills: red-brown soil, soft sandstone gullies | needs-art |
| `ghm_ter_hill_terraces` | HILL_TERRACES (10) | M2 | P0 | dry #C9A877 / rice #7FD457 / ripe #E2C04C / wheat #9CCB55 | riser #8C7A5A / #8E877C | crop rows following the contour | 35–50° | Terrace risers are procedural contour strips (§7 `ghm_prp_terrace_wall_*`). Monsoon water in the rice steps | needs-art |
| `ghm_ter_hill_forest` | HILL_FOREST (11) | M1 | P0 | #5E8A3E / #4E8A3A / #5A7E3A / #6A7A42 | #7A6A3E / #8E877C | litter + moss blotches | 30–45° | Valley rim forests (Shivapuri, Chandragiri) | needs-art |
| `ghm_ter_hill_scrub` | HILL_SCRUB (12) | M1 | P1 | #A39A55 / #8FA049 / #9C9A50 / #B59A5A | #9A7A50 / #8E877C | sparse-bush blotches | 30–45° | Spiny babbler habitat (§10) | needs-art |
| `ghm_ter_hill_grassland` | HILL_GRASSLAND (13) | M1 | P0 | #B4C25E / #9CC75A / #B8B85E / #C8B56A | #9A7A50 / #8E877C | grass strokes | 30–45° | Also the fallback colours for NONE | needs-art |
| `ghm_ter_subalpine_forest` | SUBALPINE_FOREST (14) | M3a | P0 | #4E7A46 / #3F6E46 / #5A6E40 / snow-dusted | #6A5E44 / #8C877F | needle litter + moss | 28–42° | Rhododendron bloom tint in spring via §6 scatter. Winter snow cover | needs-art |
| `ghm_ter_alpine_meadow` | ALPINE_MEADOW (15) | M3a | P0 | #A6C46A / #8DC65E + flower specks / #C89A5A / snow | #8E7A5A / #8C877F | grass strokes + flower dots (pink, yellow, purple) | 28–40° | Kharka pastures with yak and goat grazing | needs-art |
| `ghm_ter_alpine_scrub` | ALPINE_SCRUB (16) | M3a | P0 | #8A8E52 / #7E8A4E / rust #A0603A / snow | #7A6A50 / #8C877F | low-bush blotches | 28–40° | Dwarf juniper and rhododendron | needs-art |
| `ghm_ter_scree_rock` | SCREE_ROCK (17) | M3a | P0 | #A39E96 all seasons; snow patches in winter | #8C877F / #77726B | strata + crack strokes | 20–35° | Strong rim light at sunrise and sunset | needs-art |
| `ghm_ter_moraine` | MORAINE (18) | M3b | P0 | #8F877C | #7E766B / #6E675E | boulder speckle, rubble strokes | 20–35° | Boulder scatter (§7 `ghm_prp_rock_*`). Lateral moraine ridges come from the DEM | needs-art |
| `ghm_ter_glacier` | GLACIER (19) | M3b | P0 | ice #DCEFF7, crevasse lines #9CCBE6; debris-covered variant #8F877C | #BFDDEB / #8C877F | crevasse strokes, meltpond spots #40D0C8 | 15–30° | Khumbu and Ngozumpa are debris-covered glaciers: use the debris variant there (verify extents) | needs-art |
| `ghm_ter_snow` | SNOW (20) | M3a | P0 | #FFFFFF, shadow #B7CFF0 | #E6EEF8 / #8C877F | wind-ripple strokes, sparkle mask | 40–60° | Alpenglow comes from lighting, not the palette | needs-art |
| `ghm_ter_trans_himalayan_steppe` | TRANS_HIMALAYAN_STEPPE (21) | M3a | P0 | #CDA875 / #C9AA70 / #C99E68 / snow-dusted | eroded cliff #B5683F / #A58A6C | dust blotches, strata stripes | 25–40° | Mustang's red and ochre eroded cliffs. Wind-blown dust (§11) | needs-art |
| `ghm_ter_trans_himalayan_cropland` | TRANS_HIMALAYAN_CROPLAND (22) | M3a | P1 | young barley #A5D45A / #8FCB52 / buckwheat pink #E7A0B8 or gold #DCC05A / bare #B8A27A | #A58A6C / #A39E96 | field-strip strokes | 20–35° | Irrigated green fields inside an arid landscape, edged by stone walls and poplar lines (§6). Crop calendar (review) | needs-art |
| `ghm_ter_orchard` | ORCHARD (23) | M3a | P1 | #9CCB63 / #7FC456 / #A9C463 / #B9B56E | #8C7A5A / #9C968C | grass strokes in tree rows | 30–45° | Apple orchards (Mustang, Jumla) | needs-art |
| `ghm_ter_tea_garden` | TEA_GARDEN (24) | M5 | P0 | flush #8FD45A / #5DAE4A / #5A9E46 / #4E8A42 | #8C7A5A / #9C968C | contour row stripes | 30–45° | Ilam. Bushes are instanced along contour rows (§6) | needs-art |
| `ghm_ter_wetland` | WETLAND (25) | M4 | P1 | #8E9C5E / #7C9C58 + water patches #6FA9B8 / #9A9A5E / #A49A6A | #7A6A50 / #8C877F | reed strokes, water-patch mask | 15–30° | Koshi Tappu, Chitwan oxbow lakes | needs-art |
| `ghm_ter_valley_cropland` | VALLEY_CROPLAND (26) | M1 | P0 | fallow #C9A877 / rice #7FD457 / ripe #E2C04C / mustard #F3D23A or wheat #9CCB55 | #9A8460 / #9C968C | plot strokes, bund lines | 25–40° | Kathmandu Valley fields between brick kilns and suburbs | needs-art |
| `ghm_ter_bare_soil` | BARE_SOIL (27) | M1 | P1 | #B07A4E all seasons | #9A6440 / #9C8A74 | gully strokes, landslide scars | 25–40° | Monsoon landslide-scar variant on hill roads | needs-art |

**Ground scatter per biome** (pebbles, tufts, flowers, leaf piles) uses the §6 grasses and the §7 rocks. Engineering owns the density tables. **Region tint:** soil hue may shift slightly per region (red Kathmandu clay, grey Khumbu, ochre Mustang) through a per-region colour offset, not new rows.

---

## 3. Road and trail surfaces

Ribbons are procedural (ARCHITECTURE §7.4). There is one road material. Each `Surface` value selects a 256² tile in `ghm_tex_road_trim_atlas`: the tile's middle 60 % is the running surface, and its outer bands are the edge treatment. The runtime maps U across the width and V along the length at 1 tile per 4 m. Wetness and the monsoon switch DIRT visuals to MUD (ARCHITECTURE §7.6).

### 3.1 Surfaces

| id | Surface (value → group) | M | P | Trim tile (running surface) | Edge / shoulder | Decals | Particles | Wet / snow | Status |
|---|---|---|---|---|---|---|---|---|---|
| `ghm_rd_asphalt` | ASPHALT (1 → PAVED) | M1 | P0 | blue-grey #5C5F66, lighter wheel tracks, crack strokes | crumbling edge into dirt (rural); concrete kerb + drain (urban) | potholes, patches, lane paint, zebra, tyre and skid marks, manholes, oil | water spray (wet); skid puff | dark + highlight band; puddles in potholes | needs-art |
| `ghm_rd_concrete` | CONCRETE (2 → PAVED) | M1 | P0 | light grey slabs, expansion joints every 4 m | kerb or broken edge | cracks, patches, lane paint | water spray (wet) | darkens; joint puddles | needs-art |
| `ghm_rd_brick` | BRICK (3 → PAVED) | M1 | P0 | Newar old-town brick paving: herringbone and stretcher bonds, stone-slab borders and gutters (verify against Patan and Bhaktapur photos) | stone channel edge, step-up to house plinths | worn centre, puddles, marigold petals (festival) | light dust | deep red when wet; puddles in hollows | needs-art |
| `ghm_rd_cobble` | COBBLE (4 → PAVED) | M1 | P1 | stone flags and setts; irregular flagstones for mountain villages | rough stone edge | moss in joints, puddles | — | darkens; snow in joints at altitude | needs-art |
| `ghm_rd_gravel` | GRAVEL (5 → GRAVEL) | M1 | P1 | pale grey loose stones, two wheel ruts | gravel spill into grass | tyre ruts, puddles | gravel kick, dust trail | dark stones, puddles in ruts | needs-art |
| `ghm_rd_compacted` | COMPACTED (6 → GRAVEL) | M1 | P1 | hard-packed earth with fine gravel | soft verge | ruts, puddles | dust puff, light gravel kick | turns to mud colour at high wetness | needs-art |
| `ghm_rd_dirt` | DIRT (7 → DIRT) | M1 | P0 | red-brown or ochre (region tint), ruts, grass strip in the centre on tracks | grassy verge | tyre marks, puddles, hoof prints | dust trail, dust puff | **Monsoon → MUD visuals**; puddles everywhere | needs-art |
| `ghm_rd_mud` | MUD (8 → MUD) | M2 | P0 | dark glossy brown, deep water-filled ruts | churned verge | deep tyre ruts, puddles | mud splash, mud clods | always wet; glossy ramp | needs-art |
| `ghm_rd_sand` | SAND (9 → DIRT) | M4 | P1 | pale beige riverbed track, soft ripples | blends to RIVERBED_GRAVEL | tyre marks, footprints | sand puff | darker, firm look | needs-art |
| `ghm_rd_grass` | GRASS (10 → DIRT) | M2 | P1 | two-track worn grass | meadow | hoof prints | grass flecks | dark green, muddy tracks | needs-art |
| `ghm_rd_rock` | ROCK (11 → GRAVEL) | M3a | P0 | bedrock slabs and rough stone trail | boulders | — | gravel kick, stone chips | dark wet stone; snow patches | needs-art |
| `ghm_rd_snow_ice` | SNOW_ICE (12 → MUD) | M3a | P1 | packed snow with footprint and track texture; blue icy patches | soft snow berm | footprints, tracks (dynamic) | snow powder | glitter mask; ice highlight | needs-art |
| `ghm_rd_wood` | WOOD (13 → PAVED) | M1 | P1 | plank deck, 0.2 m boards, nail dots | timber edge beam | — | — | darkens; wet sheen | needs-art |
| `ghm_rd_metal` | METAL (14 → PAVED) | M2 | P0 | steel chequer plate and grating (Bailey and suspension bridge decks) | steel kerb angle | rust streaks | — | wet sheen | needs-art |
| `ghm_rd_unknown` | UNKNOWN (0 → DIRT) | M1 | P0 | uses DIRT | — | — | — | — | procedural |

### 3.2 Decals (cells in `ghm_tex_decal_atlas`)

Decals are mesh decals projected onto ribbons (not URP screen-space decals). They are placed procedurally by surface, class, seed and weather.

| id | Decal | M | P | Surfaces | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_dcl_pothole_{a,b,c}` | Potholes | M1 | P0 | ASPHALT, CONCRETE | Cartoon rounded rims; small, medium and large | needs-art |
| `ghm_dcl_pothole_puddle_{a,b}` | Pothole with water | M1 | P0 | ASPHALT, CONCRETE | Monsoon and rain variant; ripple animation from the shader | needs-art |
| `ghm_dcl_puddle_{a,b,c}` | Puddles | M1 | P0 | DIRT, GRAVEL, COMPACTED, BRICK, MUD | Sky-reflection tint; rain ripples | needs-art |
| `ghm_dcl_patch_{a,b}` | Asphalt patch repairs | M1 | P1 | ASPHALT | Darker squares | needs-art |
| `ghm_dcl_cracks_{a,b}` | Crack networks | M1 | P1 | ASPHALT, CONCRETE | — | needs-art |
| `ghm_dcl_tyre_marks_{a,b}` | Tyre marks and ruts | M1 | P0 | DIRT, MUD, SAND, SNOW_ICE, GRAVEL | Dynamic trails behind vehicles reuse these cells | needs-art |
| `ghm_dcl_skid_marks` | Skid marks | M1 | P1 | ASPHALT, CONCRETE | Fade after 30 s | needs-art |
| `ghm_dcl_lane_{dash,edge,centre}` | Lane paint | M1 | P0 | ASPHALT, CONCRETE | White dashed centre and edge lines; yellow variant (verify current Nepali road-marking practice) | needs-art |
| `ghm_dcl_zebra` | Zebra crossing | M1 | P0 | ASPHALT, CONCRETE | Chunky stripes, slightly worn | needs-art |
| `ghm_dcl_stop_line`, `ghm_dcl_arrow_{left,right,straight}` | Junction markings | M1 | P2 | ASPHALT | — | needs-art |
| `ghm_dcl_speed_breaker_paint` | Speed-breaker stripes | M1 | P1 | ASPHALT, CONCRETE | Goes with the `ghm_rd_speed_breaker` mesh | needs-art |
| `ghm_dcl_manhole`, `ghm_dcl_drain_grate` | Covers | M1 | P1 | urban | Generic: no city crests or text | needs-art |
| `ghm_dcl_oil_stain` | Oil stains | M1 | P2 | ASPHALT, CONCRETE | Bus parks, junctions | needs-art |
| `ghm_dcl_hoof_prints` | Hoof prints | M3a | P2 | DIRT, GRASS, SNOW_ICE | Horse, mule and yak trails | needs-art |
| `ghm_dcl_footprints` | Footprints | M3a | P1 | SNOW_ICE, SAND, MUD | Dynamic trail behind the player | needs-art |
| `ghm_dcl_leaf_litter` | Leaf litter | M2 | P2 | all, forest roads | Season-driven | needs-art |
| `ghm_dcl_petals_marigold` | Marigold petals | M1 | P2 | BRICK, COBBLE (temple approaches) | Festival and temple variant | needs-art |
| `ghm_dcl_rangoli_{a,b}` | Rangoli / mandala | M5 | P2 | BRICK, CONCRETE (doorsteps) | Tihar. Patterns (review) | needs-art |

### 3.3 Surface particles

Budgets are the maximum live particles per emitter (Low / Mid / High). All use `ghm_tex_vfx_atlas`. Soft particles are off on Low.

| id | Effect | M | P | Trigger | Budget | Notes | Status |
|---|---|---|---|---|---|---|---|
| `ghm_vfx_dust_puff` | Dust puff | M1 | P0 | Footstep, landing, braking on DIRT, COMPACTED, SAND (dry) | 8 / 16 / 32 | Round cartoon puffs, tinted from the biome soil colour | needs-art |
| `ghm_vfx_dust_trail` | Dust trail | M1 | P0 | Vehicle above 20 km/h on dry unpaved roads | 16 / 32 / 64 | Off when wetness > 0.3 | needs-art |
| `ghm_vfx_gravel_kick` | Gravel kick | M1 | P1 | GRAVEL, ROCK, COMPACTED; wheel spin | 8 / 16 / 32 | Mesh particles (small stones), 12 tris each | needs-art |
| `ghm_vfx_mud_splash` | Mud splash | M2 | P0 | MUD, or DIRT when wet; stuck recovery | 12 / 24 / 48 | Also drives the vehicle mud-level tint (§8) | needs-art |
| `ghm_vfx_water_spray` | Water spray | M1 | P0 | Puddles, wet asphalt, fords (`RoadFlags.FORD`) | 12 / 24 / 48 | Fords use a bigger burst plus a ring | needs-art |
| `ghm_vfx_snow_powder` | Snow powder | M3a | P1 | SNOW_ICE, SNOW biome | 8 / 16 / 32 | Sparkly | needs-art |
| `ghm_vfx_sand_puff` | Sand puff | M4 | P2 | SAND | 8 / 16 / 32 | — | needs-art |
| `ghm_vfx_leaf_swirl` | Leaf swirl | M2 | P2 | Forest roads at speed, by season | 6 / 12 / 24 | Sal leaves in spring, rhododendron petals in bloom | needs-art |
| `ghm_vfx_skid_puff` | Skid puff | M1 | P1 | Hard braking on PAVED | 6 / 12 / 24 | Cartoon white puff, never black smoke | needs-art |

### 3.4 Road kit meshes

| id | Piece | M | P | Tris (LOD0/LOD1) | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_rd_kerb_{1m,2m,4m}` | Concrete kerb | M1 | P0 | 24/40/60, merged | Plain, and a painted variant with black and yellow stripes (verify local practice) | needs-art |
| `ghm_rd_footpath_tile_{2m,4m}` | Footpath with tiles | M1 | P1 | 40/60 | Urban arterials | needs-art |
| `ghm_rd_drain_open_{2m,4m}` | Open roadside drain | M1 | P1 | 40/60 | Stone or concrete channel | needs-art |
| `ghm_rd_guardrail_{2m,4m}` | Steel W-beam guardrail | M2 | P0 | 80/120 | Hill-road curves | needs-art |
| `ghm_rd_parapet_stone_{2m,4m}` | Stone parapet | M2 | P1 | 60/100 | Painted white blocks on curves (verify) | needs-art |
| `ghm_rd_gabion_{1m,2m}` | Gabion wall (wire-mesh stone boxes) | M2 | P0 | 120/200 | An iconic hill-road retaining wall. Mesh pattern lives in the detail atlas | needs-art |
| `ghm_rd_retaining_stone_{2m,4m}` | Stone retaining wall | M2 | P0 | 60/100 | Also used for road cuts | needs-art |
| `ghm_rd_landslide_debris_{a,b}` | Landslide debris pile | M2 | P2 | 600/200 | Decorative road-edge debris that never fully blocks a road | needs-art |
| `ghm_rd_culvert` | Culvert mouth | M2 | P2 | 200/80 | — | needs-art |
| `ghm_rd_speed_breaker` | Speed breaker | M1 | P1 | 60/20 | Plus the paint decal | needs-art |
| `ghm_rd_steps_stone_{1m,2m}` | Trail steps (0.18 m rise) | M1 | P0 | 60/100 | `RoadClass.STEPS` and steep trails; Swayambhu stairway style | needs-art |
| `ghm_rd_steps_concrete_{1m,2m}` | City steps | M1 | P1 | 40/60 | — | needs-art |
| `ghm_rd_bridge_deck_concrete_{4m,8m}` | Concrete bridge deck + railing | M1 | P0 | 150/250 | `RoadFlags.BRIDGE` | needs-art |
| `ghm_rd_bridge_bailey_{3m}` | Steel truss (Bailey) bridge module | M2 | P0 | 400 per module | Common on highways and rural roads. Painted grey or green | needs-art |
| `ghm_rd_bridge_pier_{a,b}` | Piers and abutments | M1 | P1 | 150 | — | needs-art |
| `ghm_rd_tunnel_portal` | Tunnel portal | M2 | P2 | 800/250 | `RoadFlags.TUNNEL`; generic | needs-art |
| `ghm_rd_ford_stones` | Stepping stones | M2 | P2 | 200/60 | Fords on trails | needs-art |
| `ghm_rd_barrier_pole` | Checkpost barrier pole | M2 | P1 | 150/50 | Police and permit checkposts; red-white striped | needs-art |
| `ghm_rd_edge_crumble_{a,b}` | Broken asphalt edge chunks | M1 | P2 | 80 | Rural road edges | needs-art |

---
## 4. Building modular kits

Each `BuildingArchetype` is a grammar ScriptableObject (ARCHITECTURE §7.5). It has a facade grammar, a roof generator per `RoofShape`, a palette, a detail atlas `ghm_tex_detail_<archetype>` and the kit pieces below. Footprints, levels, roof shape and materials come from OSM or inference. The grammar chooses pieces by seed, levels and `BuildingUse`. LOD0 is kit pieces, GPU-instanced per piece. LOD1 and LOD2 are merged procedurally. Material: `ghm_mat_toon_lit_detail` for walls with carvings or motifs, `ghm_mat_toon_lit` for everything else.

### 4.1 Standard kit piece budgets (all archetypes)

| Piece | Variants | Tris LOD0 | Rules |
|---|---|---|---|
| Wall segment | 1 m / 2 m / 4 m; ground floor, upper floor, top floor | 40 / 60 / 100 | Exterior face at +Z. Wall thickness is shown only at openings and edges (0.25–0.45 m by archetype) |
| Corner | outer, inner, per storey | 60 / 40 | Bevelled, chunky |
| Window | small, medium, large | 150 / 250 / 400 (carved ≤ 900) | No alpha: lattices are painted on opaque insets |
| Door | single, double, rolling shutter | 200 / 300 / 120 (carved ≤ 500) | — |
| Shopfront bay | 2 m / 4 m | 250 / 400 | Signboard slot 2 m × 0.6 m (signage atlas) |
| Balcony | 2 m / 4 m | 300 / 500 | — |
| Trim (string course, cornice, plinth, eave) | per metre | 20–24 | Instanced along edges |
| Roof (generator output, §4.4) | per building | ≤ 400 for houses, ≤ 600 per pagoda tier | Eave overhang per archetype |
| Roof prop | per item | 100–400 | §4.3 per archetype |
| Generated building cap | per building | **3 000** (Newar 5 000; temples, stupas and gompas 9 000) | The grammar drops ornament before it exceeds the cap |

Storey heights are grammar parameters (verify against references): Newar 2.1–2.4 m (low ceilings), modern urban 3.0 m, hill village 2.3 m, Terai 2.6 m, Sherpa 2.4 m, trans-Himalayan 2.4 m, institutional 3.3 m.

### 4.2 Archetypes

| id | Archetype (value) | M | P | Palette and materials | Roofs (`RoofShape`) | Reference and cultural notes | Status |
|---|---|---|---|---|---|---|---|
| `ghm_bld_generic` | GENERIC (0) | M1 | P0 | Muted plaster, concrete, corrugated metal | FLAT, GABLED (metal) | Fallback. Reuses the MODERN_URBAN pieces with a muted palette | needs-art |
| `ghm_bld_newar` | NEWAR (1) | M1 | P0 | Red brick `w.brick.newar`, dark carved wood, clay tiles; dachi apa (wedge-faced brick) string courses (review) | GABLED (main), HIPPED; FLAT for later concrete additions | Kathmandu, Patan, Bhaktapur, Kirtipur, Bandipur, Panauti. Ground-floor shops and storage, carved windows above, deep tiled eaves on struts. Many houses carry a newer concrete top floor: a hybrid variant is required for realism | needs-art |
| `ghm_bld_modern_urban` | MODERN_URBAN (2) | M1 | P0 | Painted RCC: pink, turquoise, lime, yellow, sky, lavender (`w.paint.*`); glazed-tile fronts; steel railings | FLAT (main), corrugated SKILLION sheds on roofs | 3–5 storeys; black or blue rooftop water tanks; rebar sticking up from the columns for the next floor; rolling shutters with generic signage; tangled wires on poles in front (§7) | needs-art |
| `ghm_bld_hill_village` | HILL_VILLAGE (3) | M1 | P2 | Stone or mud walls; red-ochre clay lower walls with white upper walls (`w.claywash.red`, `w.whitewash`); slate, thatch or corrugated metal | GABLED, HIPPED, ROUND (oval plans, review) | Middle hills. Verandah (pidi), small timber windows, maize cobs hung under the eaves to dry, stone courtyard. Gurung slate-roof variant for Ghandruk (M3a). Becomes P0 in M2 | needs-art |
| `ghm_bld_terai` | TERAI (4) | M4 | P0 | Mud plaster on bamboo and wattle, painted motifs, thatch or clay tiles; flat-roof brick houses for towns | GABLED, HIPPED (thatch), FLAT (brick) | Tharu houses with mud-relief decoration; Mithila wall paintings around Janakpur (fish, peacocks, elephants), commissioned or reviewed by Mithila artists (review); raised granaries and decorated mud grain bins | needs-art |
| `ghm_bld_sherpa` | SHERPA_HIMALAYAN (5) | M3b | P0 | Stone, partly whitewashed; window frames painted green, blue or red; corrugated roofs painted blue, green or red | GABLED (low pitch) | Khumbu: Namche, Khumjung, Pangboche. Livestock and storage on the ground floor, living space above. Firewood and yak-dung stacks. Traditional variant: wooden shingles weighted with stones (review) | needs-art |
| `ghm_bld_trans_himalayan` | TRANS_HIMALAYAN (6) | M3a | P0 | Rammed earth and mud brick, whitewashed or natural; black trapezoid window frames | FLAT with parapets | Mustang, Manang, Dolpo. Firewood stacked along the roof parapets; prayer-flag poles at the roof corners; notched-log ladders; red, white and grey-blue bands on religious buildings (review) | needs-art |
| `ghm_bld_temple_pagoda` | TEMPLE_PAGODA (7) | M1 | P0 | Brick, carved wood, clay tiles or gilt copper roofs, gilded finials | PAGODA (1–5 tiers) | Stepped plinth, square sanctum, 1–5 tiered roofs on carved struts, gajur finial, torana over the door, eave bells, guardian lions. Strut carvings are generic and stylised: no explicit imagery (some real struts are erotic) (review) | needs-art |
| `ghm_bld_temple_shikhara` | TEMPLE_SHIKHARA (8) | M1 | P1 | Stone (Patan's Krishna Mandir style) or white and orange plaster (Terai and hills) | SHIKHARA | Curvilinear tower, amalaka disc, kalasha finial, colonnaded pavilions. Red or saffron pennant on top of plastered temples (review) | needs-art |
| `ghm_bld_stupa` | STUPA (9) | M1 | P0 | `w.stupa.white` dome, gilt spire, painted harmika | DOME (own generator) | Base terraces, white dome, harmika with painted Buddha eyes (nose drawn as the Nepali numeral १, with the urna dot), **13-step spire**, parasol and finial, prayer-flag lines from spire to base. Walking routes run clockwise | needs-art |
| `ghm_bld_gompa` | GOMPA (10) | M1 | P1 | Whitewash or ochre with a maroon roof band; black trapezoid windows; painted columns; gilt roof ornaments | FLAT, HIPPED (gilt) | Assembly hall, courtyard, prayer-wheel gallery, dharma wheel flanked by two deer over the entrance, cylindrical victory banners at the roof corners (review) | needs-art |
| `ghm_bld_chorten` | CHORTEN (11) | M3a | P0 | White; regional colour bands | — (own generator) | Stepped base, bell dome, spire with sun-and-moon finial. Kani (walk-through gate chorten) with a painted ceiling (review). Pass on the left, keeping the chorten on your right | needs-art |
| `ghm_bld_shrine` | SHRINE (12) | M1 | P0 | Brick, stone, sindoor-red painted images, bells, tin canopies | PYRAMIDAL, SKILLION, none | Roadside Ganesh and Bhairab shrines, Shiva shrines with tridents, nag stones, Newar stone chaityas. Offerings: flowers, rice, red powder (review) | needs-art |
| `ghm_bld_mosque` | MOSQUE (13) | M1 | P1 | White and green plaster | DOME, ONION + minarets | Generic South Asian style. No Arabic script unless verified (review) | needs-art |
| `ghm_bld_church` | CHURCH (14) | M1 | P2 | Plaster, a simple cross | GABLED | Nepali churches are modest: often a plain hall with a cross (review) | needs-art |
| `ghm_bld_industrial` | INDUSTRIAL (15) | M1 | P1 | Corrugated metal, brick, concrete | GABLED, sawtooth SKILLION | Factory sheds and warehouses. **Brick kilns** with tall brick chimneys, rings of stacked bricks and drying yards (Kathmandu Valley, dry season); smoke in season (§11) | needs-art |
| `ghm_bld_institutional` | INSTITUTIONAL (16) | M1 | P1 | Cream, blue or white plaster; compound walls | FLAT, HIPPED | Schools (open corridors with railings, compound wall and gate, flagpole), health posts and hospitals (generic **green** cross only), government offices (no national emblem: use the flag), police posts (generic) | needs-art |
| `ghm_bld_hut` | HUT (17) | M1 | P1 | Bamboo, thatch, corrugated metal, stone | GABLED, SKILLION | Sheds, cowsheds (goth), field huts; Terai crop-watch huts on stilts (machan) | needs-art |
| `ghm_bld_greenhouse` | GREENHOUSE (18) | M1 | P2 | Bamboo hoops and milky plastic (opaque, faked translucency in the ramp) | ROUND | Plastic tunnel greenhouses for vegetables (common in the hills) | needs-art |
| `ghm_bld_teahouse` | TEAHOUSE (19) | M3a | P0 | Regional: Gurung stone with slate or blue metal (Annapurna); Sherpa stone with painted frames (Khumbu) | GABLED | Two-storey lodges, a dining room with big windows and a stove chimney, solar panels, prayer flags, a terrace with tables. Generic lodge names only (e.g. "Mountain View Lodge" style) | needs-art |

### 4.3 Archetype-specific pieces

Pieces not listed here use the standard pieces in §4.1, with the archetype's palette.

| id | Piece | M | P | Tris LOD0 | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_bld_newar_wall_{1m,2m,4m}` | Brick wall + dachi apa course | M1 | P0 | 40/60/100 | Brick pattern from the detail atlas | needs-art |
| `ghm_bld_newar_window_tikijhya_{a,b}` | Tikijhya lattice window | M1 | P0 | 400 | Fine lattice painted on an opaque inset (review pattern) | needs-art |
| `ghm_bld_newar_window_sanjhya_{a,b}` | San jhya projecting window | M1 | P0 | 900 | Cantilevered carved bay window (review) | needs-art |
| `ghm_bld_newar_window_plain_{a,b}` | Carved-frame window | M1 | P0 | 250 | Most common type | needs-art |
| `ghm_bld_newar_door_{a,b}` | Carved door with lintel | M1 | P0 | 500 | Low doorway (about 1.6 m) | needs-art |
| `ghm_bld_newar_shopfront_{2m,4m}` | Ground-floor shop with wooden fold-out shutters | M1 | P0 | 400 | Generic goods (§7) | needs-art |
| `ghm_bld_newar_strut` | Eave strut (tunala), house type | M1 | P0 | 120 | Instanced along the eave | needs-art |
| `ghm_bld_newar_upper_concrete_{a,b}` | Concrete top-floor addition | M1 | P1 | 400 | Hybrid realism | needs-art |
| `ghm_bld_newar_bahal_{bay,pillar,steps,gate}` | Courtyard (bahal/bahi) pieces | M1 | P1 | 600/200/150/500 | Arcaded courtyards, a gate with a torana; a chaitya in the centre (§7) (review) | needs-art |
| `ghm_bld_modern_wall_{1m,2m,4m}` | Painted plaster wall | M1 | P0 | 40/60/100 | Paint via tint mask; slight stains | needs-art |
| `ghm_bld_modern_rebar_{a,b}` | Rebar sticking up from columns | M1 | P0 | 120 | Iconic. Bent or straight, wrapped in a plastic bag on top (verify) | needs-art |
| `ghm_bld_modern_window_{a,b,c}` | Aluminium sliding and grille windows | M1 | P0 | 200 | Some with steel safety grilles | needs-art |
| `ghm_bld_modern_balcony_{2m,4m}` | Steel-railing balcony | M1 | P0 | 300/500 | Laundry line variant | needs-art |
| `ghm_bld_modern_shutter_{2m,4m}_{open,half,closed}` | Rolling shutter shopfront | M1 | P0 | 200 | Signboard slot above | needs-art |
| `ghm_bld_modern_signboard_{s,m,l}` | Generic signboard | M1 | P0 | 50 | Signage atlas cells: generic categories such as "Kirana Pasal / किराना पसल", "Tea / चिया", "Momo", "Trekking Gear", "Hardware" (review wording) | needs-art |
| `ghm_bld_modern_cantilever_trim` | Floor slab edge | M1 | P1 | 60/m | Upper floors projecting over the street | needs-art |
| `ghm_bld_roofprop_tank_{black,blue}` | Rooftop water tank | M1 | P0 | 200 | Cylindrical plastic tanks; generic, no brand | needs-art |
| `ghm_bld_roofprop_solar_heater` | Solar water heater | M1 | P1 | 300 | — | needs-art |
| `ghm_bld_roofprop_dish` | Satellite dish | M1 | P1 | 120 | — | needs-art |
| `ghm_bld_roofprop_stair_hut` | Roof stair hut | M1 | P1 | 300 | — | needs-art |
| `ghm_bld_roofprop_pots` | Plant pots, rooftop garden | M1 | P2 | 150 | — | needs-art |
| `ghm_bld_hill_wall_{stone,mud}_{1m,2m,4m}` | Stone or mud wall, two-tone | M2 | P0 | 40/60/100 | Red-ochre and white split from vertex-colour tint | needs-art |
| `ghm_bld_hill_verandah_{2m,4m}` | Verandah with wooden posts | M2 | P0 | 300/500 | — | needs-art |
| `ghm_bld_hill_maize_hang` | Drying maize bundles | M2 | P1 | 200 | Under the eaves in autumn | needs-art |
| `ghm_bld_hill_roof_{slate,thatch}_trim` | Slate and thatch roof trims | M2 | P0 | 24/m | Thatch is a thick rounded mesh with no cards | needs-art |
| `ghm_bld_hill_window_small`, `ghm_bld_hill_door_low` | Small timber window and door | M2 | P0 | 150 / 200 | — | needs-art |
| `ghm_bld_terai_wall_mud_{1m,2m,4m}` | Mud-plaster wall with a motif slot | M4 | P0 | 40/60/100 | Motifs as detail-atlas decals | needs-art |
| `ghm_bld_terai_motif_{mithila,tharu}_{a..f}` | Painted and relief motifs | M4 | P0 | decal | Mithila: fish, peacock, elephant, sun, lotus. Tharu: relief patterns (review all) | needs-art |
| `ghm_bld_terai_granary_raised` | Raised bamboo granary | M4 | P1 | 600 | — | needs-art |
| `ghm_bld_terai_grain_bin` | Decorated mud grain bin | M4 | P2 | 400 | Tharu (review) | needs-art |
| `ghm_bld_terai_roof_thatch_trim` | Thick thatch eave | M4 | P0 | 24/m | — | needs-art |
| `ghm_bld_sherpa_wall_{1m,2m,4m}` | Stone wall, whitewashed upper | M3b | P0 | 40/60/100 | — | needs-art |
| `ghm_bld_sherpa_window_{a,b}` | Painted-frame window | M3b | P0 | 250 | Small cloth valance above the window (review) | needs-art |
| `ghm_bld_sherpa_roof_metal_trim`, `_shingle_stones` | Painted metal roof; stone-weighted shingle roof | M3b | P0 | 24/m; 400 | — | needs-art |
| `ghm_bld_sherpa_firewood_stack`, `_dung_wall` | Firewood stack; dung cakes drying on a wall | M3b | P1 | 300; 100 | (review) | needs-art |
| `ghm_bld_trans_wall_{1m,2m,4m}` | Rammed-earth wall, battered | M3a | P0 | 40/60/100 | — | needs-art |
| `ghm_bld_trans_window_{a,b}` | Black trapezoid window | M3a | P0 | 200 | — | needs-art |
| `ghm_bld_trans_parapet_firewood_{2m,4m}` | Roof-edge firewood stack | M3a | P0 | 200/400 | A signature look of Mustang | needs-art |
| `ghm_bld_trans_roof_corner_flags` | Roof-corner prayer-flag bundle | M3a | P0 | 150 | Cloth flutter | needs-art |
| `ghm_bld_trans_ladder_log` | Notched-log ladder | M3a | P1 | 100 | — | needs-art |
| `ghm_bld_pagoda_plinth_step` | Plinth level (stack 1–5) | M1 | P0 | 300 | Brick or stone, with stairs on 1 or 4 sides | needs-art |
| `ghm_bld_pagoda_sanctum_{1bay,3bay,5bay}` | Sanctum body | M1 | P0 | 1 200 | Carved door frames | needs-art |
| `ghm_bld_pagoda_strut_deity_{a..d}` | Temple struts | M1 | P0 | 100 | Generic stylised deities; non-explicit (review) | needs-art |
| `ghm_bld_pagoda_torana` | Torana arch | M1 | P0 | 400 | Detail atlas (review iconography) | needs-art |
| `ghm_bld_pagoda_gajur` | Gilded finial | M1 | P0 | 300 | Gold ramp | needs-art |
| `ghm_bld_pagoda_banner` | Hanging metal banner from the top tier | M1 | P1 | 100 | Pataka (review name and form) | needs-art |
| `ghm_bld_pagoda_eave_bell` | Eave bell | M1 | P1 | 40 | Instanced; sways in the wind; tinkle SFX | needs-art |
| `ghm_bld_pagoda_lion_{a,b}` | Guardian lion pair | M1 | P0 | 600 | Stone, cartoon-friendly | needs-art |
| `ghm_bld_shikhara_{tower_s,tower_m,tower_l}` | Shikhara tower segments | M1 | P1 | 800/1 200/1 500 | With amalaka and kalasha | needs-art |
| `ghm_bld_shikhara_pavilion_{2m,4m}` | Colonnaded pavilion | M1 | P1 | 500/800 | — | needs-art |
| `ghm_bld_stupa_{s,m,l}` | Parametric stupa | M1 | P0 | 600/1 500/3 000 | 2 m, 6 m and 15 m classes. Eyes from the detail atlas (review proportions) | needs-art |
| `ghm_bld_stupa_flaglines` | Prayer-flag catenary lines | M1 | P0 | proc | Generated strips (§7 lungta). Flags in correct colour order | procedural |
| `ghm_bld_gompa_wall_{1m,2m,4m}` | Battered wall with maroon band | M1 | P1 | 40/60/100 | — | needs-art |
| `ghm_bld_gompa_dharma_wheel` | Dharma wheel with two deer | M1 | P1 | 500 | Gilt (review) | needs-art |
| `ghm_bld_gompa_victory_banner` | Roof-corner victory banner | M1 | P1 | 200 | (review) | needs-art |
| `ghm_bld_gompa_portico_{2col,4col}` | Painted portico | M1 | P1 | 600/1 000 | Painted columns and lintels | needs-art |
| `ghm_bld_chorten_{s,m,l}` | Chorten | M3a | P0 | 400/900/1 500 | Paint variants: Khumbu white; Mustang red, white and grey bands (review) | needs-art |
| `ghm_bld_chorten_kani` | Gate chorten | M3a | P1 | 1 200 | Painted ceiling mandala (review) | needs-art |
| `ghm_bld_shrine_{ganesh,bhairab,trishul,nag}` | Small shrines | M1 | P0 | 300–400 | (review all iconography) | needs-art |
| `ghm_bld_shrine_tin_canopy` | Corrugated mini canopy | M1 | P1 | 300 | — | needs-art |
| `ghm_bld_mosque_{dome,minaret,arch_bay}` | Mosque pieces | M1 | P1 | 600/800/300 | Crescent finial (review) | needs-art |
| `ghm_bld_church_{bell_tower,cross}` | Church pieces | M1 | P2 | 600/60 | (review) | needs-art |
| `ghm_bld_ind_shed_{4m,8m}` | Sawtooth corrugated shed | M1 | P1 | 200/300 | — | needs-art |
| `ghm_bld_ind_kiln_chimney` | Brick-kiln chimney | M1 | P1 | 800 | Tapered brick, 25–35 m (verify); smoke VFX | needs-art |
| `ghm_bld_ind_kiln_ring` | Kiln firing ring | M1 | P1 | 1 500 | Oval trench enclosure (verify against reference) | needs-art |
| `ghm_bld_ind_brick_stack_{a,b}` | Green and fired brick stacks | M1 | P1 | 200 | Instanced in drying yards | needs-art |
| `ghm_bld_inst_corridor_rail_{2m,4m}` | School corridor railing | M1 | P1 | 150/250 | — | needs-art |
| `ghm_bld_inst_gate_{a,b}` | Compound gate with generic name plate | M1 | P1 | 500 | "School / विद्यालय", "Health Post / स्वास्थ्य चौकी" (review wording) | needs-art |
| `ghm_bld_hut_{shed,cowshed,machan}` | Hut variants | M1 | P1 | 400/600/500 | Machan (stilt watch hut) is M4 | needs-art |
| `ghm_bld_greenhouse_{4m}` | Plastic tunnel segment | M1 | P2 | 300 | — | needs-art |
| `ghm_bld_teahouse_{sign,dining_window,stovepipe,terrace}` | Teahouse pieces | M3a | P0 | 50/300/100/400 | Generic lodge names; bilingual menu board (§7) | needs-art |

### 4.4 Roofs per `RoofShape`

Roofs are generated from the footprint by the roof generator, with kit trims (eave, ridge, gable end) and archetype materials. `UNKNOWN` resolves to the archetype default.

| id | RoofShape (value) | M | P | Generator notes | Typical archetypes | Tris LOD0 (house) | Status |
|---|---|---|---|---|---|---|---|
| `ghm_roof_default` | UNKNOWN (0) | M1 | P0 | Uses the archetype default: Newar GABLED, modern FLAT, hill GABLED, Terai HIPPED, Sherpa GABLED, trans-Himalayan FLAT | all | — | procedural |
| `ghm_roof_flat` | FLAT (1) | M1 | P0 | Parapet ring 0.9 m; roof-prop slots (tanks, rebar, dish, flags) | modern, trans-Himalayan, institutional | 150 | procedural |
| `ghm_roof_gabled` | GABLED (2) | M1 | P0 | Pitch 20–35°; eave overhang 0.4 m (modern) to 1.2 m (Newar, with struts) | Newar, hill, Sherpa, teahouse | 250 | procedural |
| `ghm_roof_hipped` | HIPPED (3) | M1 | P0 | — | Newar, hill, Terai, gompa | 300 | procedural |
| `ghm_roof_pyramidal` | PYRAMIDAL (4) | M1 | P1 | Square footprints | shrines, small temples | 200 | procedural |
| `ghm_roof_skillion` | SKILLION (5) | M1 | P0 | Single slope | sheds, extensions, rooftop sheds | 120 | procedural |
| `ghm_roof_dome` | DOME (6) | M1 | P1 | Hemisphere or segment, 16–24 segments | mosques, stupas (own generator) | 600 | procedural |
| `ghm_roof_round` | ROUND (7) | M2 | P2 | Barrel or rounded thatch | greenhouses, round houses | 400 | procedural |
| `ghm_roof_pagoda_tier` | PAGODA (8) | M1 | P0 | Stack of 1–5 tiers; each tier ~70 % of the one below (verify proportions per landmark); upturned corners; tiles or gilt copper | TEMPLE_PAGODA | 600 per tier | needs-art |
| `ghm_roof_shikhara` | SHIKHARA (9) | M1 | P1 | Curvilinear tower from the kit segments | TEMPLE_SHIKHARA | 1 500 | needs-art |
| `ghm_roof_gambrel` | GAMBREL (10) | M2 | P2 | Rare | — | 300 | procedural |
| `ghm_roof_mansard` | MANSARD (11) | M2 | P2 | Rare; some Rana-era buildings (verify) | institutional | 350 | procedural |
| `ghm_roof_half_hipped` | HALF_HIPPED (12) | M2 | P2 | — | hill, institutional | 300 | procedural |
| `ghm_roof_onion` | ONION (13) | M1 | P2 | Onion domes and chhatri kiosks | mosques, Janaki Mandir style (M4) | 700 | needs-art |
| `ghm_roof_cone` | CONE (14) | M2 | P2 | — | round huts, towers | 200 | procedural |

### 4.5 Wall and roof materials (`WallMaterial`, `RoofMaterial`)

Each inferred material maps to a palette swatch, a tile in the detail atlas, and a wetness response.

| id | Material (enum value) | M | P | Palette / pattern | Where | Status |
|---|---|---|---|---|---|---|
| `ghm_mat_wall_plaster` | WallMaterial.PLASTER (1) | M1 | P0 | `w.paint.*` tints, stain strokes | modern, institutional | needs-art |
| `ghm_mat_wall_brick` | WallMaterial.BRICK (2) | M1 | P0 | `w.brick.newar`, brick bond pattern | Newar, industrial | needs-art |
| `ghm_mat_wall_stone` | WallMaterial.STONE (3) | M2 | P0 | `w.stone`, coursed or random rubble | hill, Sherpa | needs-art |
| `ghm_mat_wall_mud` | WallMaterial.MUD (4) | M2 | P0 | `w.mud`, `w.claywash.red`, `w.whitewash` | hill, Terai, trans-Himalayan | needs-art |
| `ghm_mat_wall_wood` | WallMaterial.WOOD (5) | M2 | P1 | `w.wood.carved`, plank strokes | huts, upper storeys | needs-art |
| `ghm_mat_wall_bamboo` | WallMaterial.BAMBOO (6) | M4 | P1 | woven-bamboo pattern | Terai, huts | needs-art |
| `ghm_mat_wall_metal` | WallMaterial.METAL (7) | M1 | P1 | corrugated stripes, `w.metal.roof.*` | sheds, industrial | needs-art |
| `ghm_mat_wall_glass` | WallMaterial.GLASS (8) | M1 | P2 | Opaque toon glass (sky-tinted gradient, no transparency) | commercial, airport | needs-art |
| `ghm_mat_roof_concrete` | RoofMaterial.CONCRETE (1) | M1 | P0 | `w.concrete`, stain patches | flat roofs | needs-art |
| `ghm_mat_roof_metal` | RoofMaterial.METAL (2) | M1 | P0 | Corrugated: bare, rusty, or painted blue, green or red | very common everywhere | needs-art |
| `ghm_mat_roof_tiles` | RoofMaterial.TILES (3) | M1 | P0 | `w.tile.roof`, Newar clay tile rows | Newar, temples | needs-art |
| `ghm_mat_roof_slate` | RoofMaterial.SLATE (4) | M2 | P0 | `w.slate`, irregular slab rows | Gurung villages | needs-art |
| `ghm_mat_roof_thatch` | RoofMaterial.THATCH (5) | M2 | P0 | `w.thatch`, straw strokes | hill, Terai | needs-art |
| `ghm_mat_roof_wood` | RoofMaterial.WOOD (6) | M3b | P1 | Shingles | Sherpa traditional | needs-art |
| `ghm_mat_roof_glass` | RoofMaterial.GLASS (7) | M2 | P2 | Opaque toon glass | greenhouses (glass type), malls | needs-art |
| `ghm_mat_roof_mud` | RoofMaterial.MUD (8) | M3a | P0 | `w.mud`, packed-earth strokes | trans-Himalayan | needs-art |
| `ghm_mat_roof_stone` | RoofMaterial.STONE (9) | M3a | P1 | Stone slabs | high hills | needs-art |

---
## 5. Hero landmarks

One row per entry in [reports/landmarks.md](reports/landmarks.md) (66 rows: 60 found, 5 weak, 1 missing). Placement uses `landmarks.resolved.json`. The OSM building is hidden and replaced by the hero prefab (ARCHITECTURE §7.5). Budget classes come from §1.5: **A** single hero (25 k/8 k/2 k), **B** complex (parts ≤ 12 k, one centrepiece ≤ 25 k, ≤ 60 k in total), **C** landmark-lite (8 k/2.5 k/0.6 k), **D** natural or area landmark (no hero mesh: DEM, skyline, water, discovery marker, plus dressing), **E** activity rig (10 k/3 k/0.8 k). "Dressing" means the area is built from kits and props with a density preset, not a unique mesh. Every landmark also gets a discovery marker, a map pin (§12.3) and a passport or collection entry. **Weak and missing OSM matches must be fixed in `pipeline/config/landmarks.yaml` before art is placed.** Each landmark folder gets a reference board (photos, plans, the OSM footprint export) before modelling starts. During M1, P1 and P2 landmarks ship as greybox placeholders until their art lands (ROADMAP 1.6).

**General cultural rules** for all sacred sites (review): walk around stupas, chortens and mani walls **clockwise**, keeping them on your right (Bon sites are the exception and go counter-clockwise); **no vehicles** inside temple compounds or on stupa koras (`AreaKind.RELIGIOUS`, `PoiFlags.SACRED`); no climbing on monuments; prayer wheels spin clockwise; shoes-off zones are shown with shoe racks, not by forcing animations; no depiction of animal sacrifice, cremation fires or bodies.

### 5.1 Kathmandu Valley (M1)

| id | Landmark | OSM | lon, lat | M | P | Class and budget | Reference and cultural notes | Status |
|---|---|---|---|---|---|---|---|---|
| `ghm_lmk_boudhanath` | Boudhanath Stupa (बौद्धनाथ स्तूप) | [w56688296](https://www.openstreetmap.org/way/56688296) | 85.362039, 27.721493 | M1 | **P0** | A: 25 k/8 k/2 k + kora ring from kits | Mandala plan with three terraces, white dome, harmika with painted eyes, 13-step gilt spire, prayer-flag lines; prayer wheels in base niches (count (review)); kora ring of houses, shops and gompas (procedural kits); pigeons and grain sellers; butter lamps. **Clockwise kora; no vehicles on the kora plaza.** Optional festival dressing: saffron lotus arcs on the dome, Losar (review) | needs-art |
| `ghm_lmk_swayambhunath` | Swayambhunath (स्वयंभुनाथ) | [w201223707](https://www.openstreetmap.org/way/201223707) | 85.290396, 27.714929 | M1 | **P0** | B: stupa centrepiece ≤ 25 k; two shikharas ≤ 6 k each; stairway + vajra ≤ 8 k; wheels and shrines ≤ 10 k | Hilltop stupa with eyes and 13-step spire; long eastern stairway (step count (review)); giant vajra at the top of the stairs; two shikhara temples flanking the stupa (names (verify)); Harati shrine (review); prayer wheels; **rhesus macaques everywhere** (§10): playful, never aggressive, never fed by the player (review). Buddha Park statues at the western foot are out of scope (P2 later) | needs-art |
| `ghm_lmk_kathmandu_durbar_square` | Kathmandu (Basantapur) Durbar Square | [r21291455](https://www.openstreetmap.org/relation/21291455) | 85.307315, 27.704362 | M1 | **P0** | B: nine-storey Basantapur tower centrepiece ≤ 25 k; other parts ≤ 12 k each | Hanuman Dhoka gate and palace frontage (Hanuman statue under an umbrella, red-orange paste (review)); Kasthamandap (rebuilt; **model the current state** (verify)); Taleju temple exterior (not enterable); Maju Dega on its stepped plinth; Shiva-Parvati temple with figures at the upper window; Jagannath temple (neutral struts, review); Kal Bhairav relief; **Kumari Ghar: the Kumari is never shown or photographed; windows stay closed** (review); pedestrian square with vendors and pigeons; ticket booth (OSM fee=yes). Check each building's post-2015 reconstruction state as of 2026 (verify) | needs-art |
| `ghm_lmk_pashupatinath` | Pashupatinath Temple | [w913170315](https://www.openstreetmap.org/way/913170315) | 85.34863, 27.710471 | M1 | P1 | B: main temple exterior ≤ 25 k; ghats ≤ 12 k; east-bank terraces and shrines ≤ 12 k; footbridges ≤ 4 k | Two-tier gilt-roofed pagoda, silver doors, large gilded Nandi seen from behind (review). **Non-Hindus may not enter the main temple compound: the player cannot enter (a respectful prompt explains why) and views from the east bank of the Bagmati** (ARCHITECTURE P10). Ghats are shown with no cremations, bodies or fires (review). Evening aarti on the ghats as an ambient event with lamps and bells (review). Sadhus are respectful, with no pay-for-photo mechanic. Monkeys | placeholder (M1 greybox) |
| `ghm_lmk_patan_durbar_square` | Patan Durbar Square | [n2066253623](https://www.openstreetmap.org/node/2066253623) | 85.324795, 27.673148 | M1 | P1 | B: Krishna Mandir centrepiece ≤ 25 k; palace frontage, Bhimsen temple, pillar statue, hiti ≤ 12 k each | Stone shikhara Krishna Mandir; palace courtyards (Mul Chowk, Sundari Chowk) as frontage; pillar with King Yoganarendra Malla; Taleju bell; Manga Hiti sunken stone spouts (names and positions (review)). OSM is a node only: derive the anchor from the square's footprint (verify) | placeholder (M1 greybox) |
| `ghm_lmk_bhaktapur_durbar_square` | Bhaktapur Durbar Square | [w1192827651](https://www.openstreetmap.org/way/1192827651) | 85.428086, 27.6721 | M1 | P1 | B: 55-window palace centrepiece ≤ 25 k; Golden Gate, Vatsala Durga, bell, Pashupati temple ≤ 12 k each | Gilt Golden Gate torana (review iconography); stone shikhara of Vatsala Durga; brick paving; car-free heritage zone (fee). Nearby props: Pottery Square wheels and drying pots, juju dhau curd in clay bowls (§7) | placeholder (M1 greybox) |
| `ghm_lmk_nyatapola` | Nyatapola Temple (Taumadhi) | [w85470341](https://www.openstreetmap.org/way/85470341) | 85.429361, 27.671439 | M1 | P1 | A: 25 k/8 k/2 k; Bhairabnath temple as a C part ≤ 8 k | Five-tier pagoda (about 30 m; Nepal's tallest pagoda (verify)) on a five-level plinth. Stairway guardians in pairs, bottom to top: wrestlers, elephants, lions, griffins, goddesses (review). Taumadhi square with the three-tier Bhairabnath temple. OSM `height=2` is wrong (data note) | placeholder (M1 greybox) |
| `ghm_lmk_changu_narayan` | Changu Narayan Temple (चाँगुनारायण मन्दिर) | [w186264410](https://www.openstreetmap.org/way/186264410) | 85.427886, 27.716363 | M1 | P2 | A | Two-tier pagoda to Vishnu on a ridge-top; kneeling Garuda facing the temple; Licchavi-era inscribed stone pillar (oldest in Nepal (verify)); densely carved struts (review); UNESCO site | placeholder (M1 greybox) |
| `ghm_lmk_dharahara` | Dharahara (भीमसेन स्तम्भ) | [n11622074774](https://www.openstreetmap.org/node/11622074774) | 85.312169, 27.700545 | M1 | P1 | A (≤ 15 k expected) | **Model the current rebuilt tower.** The 1832 tower collapsed in the 2015 earthquake; the new tower opened in 2021 (date and height (verify)). Viewing deck; whatever remains of the old base is shown as a memorial, respectfully (verify, review). OSM `height=0` (data note) | placeholder (M1 greybox) |
| `ghm_lmk_garden_of_dreams` | Garden of Dreams (Swapna Bagaicha) | [w85650618](https://www.openstreetmap.org/way/85650618) | 85.314802, 27.714068 | M1 | P2 | C × 3: pavilions ≤ 8 k; pond and fountains ≤ 4 k; walls and gate ≤ 4 k | Rana-era neoclassical garden (1920s), restored in the 2000s (verify): pavilions, pergolas, pond, urns, lawns. A quiet zone beside Thamel; entry fee (OSM charge=400 NPR) | placeholder (M1 greybox) |
| `ghm_lmk_thamel` | Thamel (dressing) | [n1349697740](https://www.openstreetmap.org/node/1349697740) | 85.312702, 27.716658 | M1 | P1 | Dressing preset (kits + §7 props); no hero mesh | Narrow lanes; stacked **generic** signboards (no real business names, ADR-010); tangled wires; prayer-flag garlands across streets; trekking-gear and handicraft shopfronts; cycle rickshaws; tourists | needs-art |
| `ghm_lmk_chandragiri_cable_car` | Chandragiri Cable Car (चन्द्रागिरि केबलकार) | [w638657930](https://www.openstreetmap.org/way/638657930) | 85.210633, 27.677647 | M1 | P2 | E: stations 2 × 4 k; towers 600 each (instanced); cabin §8 | Gondola from Thankot up Chandragiri hill; summit viewing deck with a Himalaya panorama; Bhaleshwor Mahadev temple at the top (C ≤ 8 k, review). No operator branding | placeholder (M1 greybox) |
| `ghm_lmk_nagarkot_viewpoint` | Nagarkot viewpoint tower | [n4430216290](https://www.openstreetmap.org/node/4430216290) | 85.528521, 27.720198 | M1 | P2 | C ≤ 8 k | OSM resolves to a trail-end viewpoint, so confirm which view tower to model (verify anchor). Sunrise panorama; cloud sea over the valley (§11) | placeholder (M1 greybox) |
| `ghm_lmk_tribhuvan_airport` | Tribhuvan International Airport (त्रिभुवन अन्तर्राष्ट्रिय विमानस्थल) | [w118505122](https://www.openstreetmap.org/way/118505122) | 85.359718, 27.696551 | M1 | P2 | A ≤ 25 k: international terminal, domestic terminal, control tower | Brick-clad terminals with traditional-style elements (verify current facades). The domestic terminal is the start of the Lukla and mountain flights (M3b). No airline liveries; generic aircraft (§8). Airside is not drivable | placeholder (M1 greybox) |

### 5.2 Prithvi corridor and Pokhara (M2)

| id | Landmark | OSM | lon, lat | M | P | Class and budget | Reference and cultural notes | Status |
|---|---|---|---|---|---|---|---|---|
| `ghm_lmk_phewa_lake` | Phewa Lake (फेवा ताल) | [r8202581](https://www.openstreetmap.org/relation/8202581) | 83.942473, 28.216102 | M2 | **P0** | D + shore dressing ≤ 12 k (docks, Lakeside promenade, ghats) | Colourful wooden doonga rowboats and swan paddle boats (§8); Annapurna reflection on calm mornings; boat hire hours from OSM (08:00–18:00) drive availability | procedural |
| `ghm_lmk_tal_barahi` | Tal Barahi Temple (ताल बराही मन्दिर) | [w44843317](https://www.openstreetmap.org/way/44843317) | 83.953601, 28.207397 | M2 | **P0** | A (≤ 12 k expected) | Small pagoda on an island in Phewa (tier count (verify)), reached only by boat; Hindu temple to Barahi (review); pigeons; Saturday crowds. OSM `place=islet` | needs-art |
| `ghm_lmk_sarangkot` | Sarangkot Viewpoint (सराङ्गकोट) | [n520698757](https://www.openstreetmap.org/node/520698757) | 83.957498, 28.243695 | M2 | **P0** | C ≤ 8 k | Viewing tower and terraces; sunrise over Machhapuchhre and the Annapurnas; the paragliding launch is nearby (`ghm_lmk_sarangkot_paragliding`) | needs-art |
| `ghm_lmk_world_peace_pagoda` | World Peace Pagoda (विश्व शान्ति स्तुपा) | [w596603713](https://www.openstreetmap.org/way/596603713) | 83.944742, 28.20103 | M2 | P1 | A (≤ 15 k expected) | White shanti stupa built by the Japanese Nipponzan Myohoji order, with gilt Buddha statues in niches (count and origins (review)); on the ridge above Phewa (OSM ele=1115). A quiet zone at the top | needs-art |
| `ghm_lmk_davis_falls` | Devi's (Davis) Falls (पाताले छाँगो) | [n713296203](https://www.openstreetmap.org/node/713296203) | 83.959439, 28.190359 | M2 | P1 | C ≤ 8 k + waterfall VFX | A stream drops into a sinkhole and an underground tunnel; fenced viewing park; the monsoon flow is far bigger (VFX variant). Leave the drowning story behind the name out of fun facts (review) | needs-art |
| `ghm_lmk_gupteshwor_cave` | Gupteshwor Mahadev Cave (गुप्तेस्वर माहादेव) | [n4213348390](https://www.openstreetmap.org/node/4213348390) | 83.957726, 28.189194 | M2 | P2 | C ≤ 8 k (entrance + one chamber) | Hindu cave shrine with a Shiva lingam, entered by a spiral stair opposite Davis Falls; photography rules inside the shrine (verify, review); a lower chamber looks out at the falls (verify) | placeholder (greybox) |
| `ghm_lmk_bat_cave` | Bat Cave (Chamero Gufa) | [n3678974492](https://www.openstreetmap.org/node/3678974492) | 83.975702, 28.267581 | M2 | P2 | C ≤ 8 k | Cave interior with roosting bats (VAT, §10); narrow squeeze exit (verify); OSM fee and opening hours | placeholder (greybox) |
| `ghm_lmk_begnas_lake` | Begnas Lake (बेगनास ताल) | [w102128260](https://www.openstreetmap.org/way/102128260) | 84.097689, 28.17568 | M2 | P2 | D + dressing ≤ 4 k | Quieter lake; rowboats; fish-farming cages (verify) | procedural |
| `ghm_lmk_pokhara_airport` | Pokhara International Airport (पोखरा अन्तराष्ट्रिय विमानस्थल) | [w364334224](https://www.openstreetmap.org/way/364334224) | 84.011102, 28.187874 | M2 | P1 | C ≤ 8 k (terminal) | Opened in 2023 (verify); fast-travel hub; generic aircraft only. OSM still carries `construction:aeroway` (data note) | needs-art |
| `ghm_lmk_machhapuchhre` | Machhapuchhre (माछापुच्छ्रे) | [n727108156](https://www.openstreetmap.org/node/727108156) | 83.945876, 28.49798 | M2 | **P0** | D: DEM + skyline; silhouette check | Sacred mountain, closed to climbing: **no summit content, no landings** (review). The fishtail double summit must read from Pokhara. If the 30 m DEM silhouette is too soft, the pipeline applies an art-painted height delta to the silhouette only (verify approach) | procedural |
| `ghm_lmk_bandipur` | Bandipur | [n316985037](https://www.openstreetmap.org/node/316985037) | 84.406696, 27.938022 | M2 | P1 | Dressing: NEWAR kit + bazaar props | Newar hill-trading town on a ridge; car-free main bazaar (verify); Tundikhel viewpoint; temples (names (verify)) | needs-art |
| `ghm_lmk_manakamana` | Manakamana Temple | [n1727432206](https://www.openstreetmap.org/node/1727432206) | 84.583992, 27.904224 | M2 | P1 | A (temple ≤ 12 k) + E cable car ≤ 10 k | Pagoda temple to the wish-fulfilling goddess (tier count (verify)); cable car up from Kurintar on the Prithvi Highway; queues and pigeons. **Animal sacrifice is not depicted** (review) | needs-art |
| `ghm_lmk_gorkha_durbar` | Gorkha Durbar | [n9796531519](https://www.openstreetmap.org/node/9796531519) (**weak: matched a hotel**) | 84.62728, 27.992201 | M2 | P2 | A ≤ 25 k | **Fix the OSM match before placement.** Hilltop palace-temple where the Shah dynasty began; stone steps up from Gorkha bazaar (review) | placeholder (greybox) |
| `ghm_lmk_trishuli_river` | Trishuli River (त्रिशुली नदी) | [r4839538](https://www.openstreetmap.org/relation/4839538) | 84.928404, 27.920086 | M2 | P1 | D: river mesh + rapids VFX + suspension bridges (§7) | Runs beside the Prithvi Highway; rafting put-ins (rafting comes later, §8); brown water in the monsoon | procedural |
| `ghm_lmk_seti_river` | Seti Gandaki | [r12455804](https://www.openstreetmap.org/relation/12455804) | 84.087464, 28.156976 | M2 | P2 | D | Deep, narrow gorge through Pokhara; milky white water (`w.water.seti`; "seti" means white); viewpoints from the bridges | procedural |
| `ghm_lmk_sarangkot_paragliding` | Sarangkot paragliding takeoff | [w514455075](https://www.openstreetmap.org/way/514455075) | 83.951163, 28.241685 | M2 | **P0** | E ≤ 10 k | Grass launch slope, windsocks, staging area; landing field by Lakeside; thermal VFX (§11) and circling eagles (§10). Generic wings, no operator brands | needs-art |
| `ghm_lmk_pokhara_zipline` | Pokhara zipline | [w119054966](https://www.openstreetmap.org/way/119054966) (**weak: matched a road**) | 83.943929, 28.248079 | M2 | P2 | E ≤ 10 k | **Hand-place.** A long, steep zipline down from the Sarangkot ridge (length and drop (verify)) | placeholder (greybox) |

### 5.3 Himalaya (M3a Annapurna and Mustang approach; M3b Everest and air)

| id | Landmark | OSM | lon, lat | M | P | Class and budget | Reference and cultural notes | Status |
|---|---|---|---|---|---|---|---|---|
| `ghm_lmk_annapurna_i` | Annapurna I (अन्नपूर्णा I) | [n289923681](https://www.openstreetmap.org/node/289923681) | 83.81992, 28.595806 | M3a | **P0** | D | Skyline from Pokhara (procedural from M2); close view from Annapurna Base Camp | procedural |
| `ghm_lmk_poon_hill` | Poon Hill tower | [w366056404](https://www.openstreetmap.org/way/366056404) | 83.689503, 28.400108 | M3a | **P0** | C ≤ 8 k | View tower at about 3 200 m (OSM ele=3217) above Ghorepani; sunrise crowds; rhododendron forest blooming in spring | needs-art |
| `ghm_lmk_ghandruk` | Ghandruk (घान्द्रुक) | [n388572114](https://www.openstreetmap.org/node/388572114) | 83.807793, 28.376865 | M3a | P1 | Dressing: HILL_VILLAGE Gurung variant + museum (C) | Gurung village with slate roofs and stone-paved lanes; views of Annapurna South and Machhapuchhre; Gurung museum (verify); dress and customs (review) | needs-art |
| `ghm_lmk_dhaulagiri` | Dhaulagiri (धौलागिरी) | [n530579647](https://www.openstreetmap.org/node/530579647) | 83.489488, 28.697609 | M3a | P1 | D | Skyline from Poon Hill and the Kali Gandaki valley | procedural |
| `ghm_lmk_tilicho_lake` | Tilicho Lake (तिलिचो ताल) | [w26772994](https://www.openstreetmap.org/way/26772994) | 83.853029, 28.690126 | M3a | P2 | D | Very high lake (OSM elevation=4910); turquoise water; snow cliffs; side trip from Manang | procedural |
| `ghm_lmk_thorong_la` | Thorong La | [n1345507422](https://www.openstreetmap.org/node/1345507422) | 83.938758, 28.793529 | M3a | **P0** | C dressing ≤ 8 k | 5 416 m pass: generic pass sign, masses of prayer flags, cairns, a small teahouse (verify). The **pass celebration** (ROADMAP M3a) with a flag VFX burst | needs-art |
| `ghm_lmk_muktinath` | Muktinath | [n10173284131](https://www.openstreetmap.org/node/10173284131) | 83.863829, 28.820796 | M3a | P1 | B: pagoda temple ≤ 12 k; wall of 108 spouts ≤ 12 k; two ponds ≤ 4 k; flame shrine gompa ≤ 8 k | Sacred to both Hindus and Buddhists; 108 water spouts (review form); pilgrims bathing under the spouts (ambient); natural-gas flame shrine (review). The OSM anchor is the pond (`religion=buddhist`): verify the temple placement | needs-art |
| `ghm_lmk_everest` | Mount Everest (सगरमाथा) | [n164979149](https://www.openstreetmap.org/node/164979149) | 86.92521, 27.988061 | M3b | **P0** | D + summit plume VFX | Skyline hero; summit cloud plume (§11); seen from Kala Patthar, EBC and the mountain flight; no summit gameplay. 8 848.86 m. Silhouette check as for Machhapuchhre | procedural |
| `ghm_lmk_lhotse` | Lhotse (ल्होत्से) | [n335376854](https://www.openstreetmap.org/node/335376854) | 86.932504, 27.961986 | M3b | P1 | D | Skyline; the Nuptse–Lhotse wall frames Everest from Tengboche | procedural |
| `ghm_lmk_ama_dablam` | Ama Dablam | [n477904058](https://www.openstreetmap.org/node/477904058) | 86.860429, 27.8621 | M3b | **P0** | D + silhouette check | Iconic profile with a hanging glacier, seen from Tengboche and Pangboche; DEM silhouette correction is likely (verify) | procedural |
| `ghm_lmk_lukla_airport` | Tenzing-Hillary Airport, Lukla (तेन्जिङ हिलारी विमानस्थल) | [w377662838](https://www.openstreetmap.org/way/377662838) | 86.730296, 27.68707 | M3b | **P0** | B: runway + end wall ≤ 12 k; terminal + tower ≤ 12 k; apron + town edge ≤ 12 k | Short uphill runway ending at a terrace wall (length and gradient (verify)); the hero STOL landing sequence; small terminal and apron; porter crowds; generic aircraft | needs-art |
| `ghm_lmk_namche_bazaar` | Namche Bazaar (नाम्चे बजार) | [n274016404](https://www.openstreetmap.org/node/274016404) | 86.709796, 27.804166 | M3b | **P0** | Dressing: SHERPA kit + teahouses; entrance stupa C ≤ 8 k | Horseshoe amphitheatre village with blue and green roofs; stupa and prayer wheels at the village entrance (verify); weekly market (verify) | needs-art |
| `ghm_lmk_tengboche_monastery` | Tengboche Monastery | [r13050800](https://www.openstreetmap.org/relation/13050800) | 86.763844, 27.836126 | M3b | **P0** | A ≤ 25 k | The largest gompa in the Khumbu, rebuilt after a fire in 1989; courtyard, prayer-wheel house, entrance gate chorten, Ama Dablam backdrop; Mani Rimdu dances as a P2 event (review). A quiet zone; walk clockwise | needs-art |
| `ghm_lmk_everest_base_camp` | Everest Base Camp | [n5225912921](https://www.openstreetmap.org/node/5225912921) | 86.848795, 27.999665 | M3b | **P0** | C ≤ 8 k + instanced tents | Rocky moraine below the Khumbu Icefall; painted marker boulder with prayer flags (generic text; current form (verify)); expedition tents in the spring season only; puja altar with flag lines (review); no climbing beyond | needs-art |
| `ghm_lmk_kala_patthar` | Kala Patthar | [n564117511](https://www.openstreetmap.org/node/564117511) | 86.828441, 27.9958 | M3b | P1 | D + dressing ≤ 3 k | Summit cairns and prayer flags; the best close view of Everest; pass-celebration flow | procedural |
| `ghm_lmk_gokyo_lakes` | Gokyo Lakes (Dudh Pokhari) | [w40200994](https://www.openstreetmap.org/way/40200994) | 86.69083, 27.950679 | M3b | P1 | D + Gokyo village dressing | Turquoise glacial lakes (`w.water.glacial`); Gokyo village teahouses; the debris-covered Ngozumpa glacier beside them; Gokyo Ri. Sacred lakes: no boating or swimming (review) | procedural |
| `ghm_lmk_kushma_bungee` | Kushma bungee (Kushma–Gyadi bridge) | [w230721093](https://www.openstreetmap.org/way/230721093) (**weak: bridge way**) | 83.678481, 28.209531 | M3b | P2 | E ≤ 10 k | Long suspension footbridge high over the Kali Gandaki gorge with a jump platform (heights (verify)). It ships with the bungee system in M3b; generic operator | placeholder (greybox) |
| `ghm_lmk_bhote_koshi_bungee` | Bhote Koshi bungee bridge | **missing in OSM** | hand-placed | M3b | P1 | E ≤ 10 k | Steel suspension bridge high over the Bhote Koshi gorge near the Tibet border (site and height (verify)). Hand-place it in `landmarks.yaml`. Generic operator; jump rig in §8 | needs-art |

### 5.4 Terai (M4)

| id | Landmark | OSM | lon, lat | M | P | Class and budget | Reference and cultural notes | Status |
|---|---|---|---|---|---|---|---|---|
| `ghm_lmk_maya_devi_temple` | Maya Devi Temple, Lumbini | [w343151593](https://www.openstreetmap.org/way/343151593) | 83.27581, 27.469596 | M4 | **P0** | A ≤ 25 k | The Buddha's birthplace: a white protective temple over excavated ruins and the marker stone (interior walkway), the Ashoka pillar, the Puskarini pond, and a bodhi tree with prayer flags; monks and pilgrims. Quiet, shoes-off zone (review). Photography inside the temple is not allowed (verify), so photo mode is disabled inside | needs-art |
| `ghm_lmk_lumbini_monastic_zone` | Lumbini Monastic Zone | [w456385566](https://www.openstreetmap.org/way/456385566) (**weak**) | 83.275261, 27.489659 | M4 | P1 | B ≤ 60 k | Central canal, Eternal Peace Flame, international monasteries in national styles. Use generic, respectful stylisations, not copies of specific monasteries (review). Sarus cranes nearby (§10) | needs-art |
| `ghm_lmk_chitwan_np` | Chitwan National Park (चितवन राष्ट्रिय निकुञ्ज) | [r6083166](https://www.openstreetmap.org/relation/6083166) | 84.290877, 27.520185 | M4 | **P0** | D + Sauraha and park-gate dressing ≤ 12 k | Rapti riverside; dugout canoes; jeep safari; wooden watchtowers; Tharu villages. **No elephant-back safari** (animal welfare). Rhino, gharial and birds (§10) | needs-art |
| `ghm_lmk_bardiya_np` | Bardiya National Park (बर्दिया राष्ट्रिय निकुञ्ज) | [w294497796](https://www.openstreetmap.org/way/294497796) | 81.51154, 28.409808 | M4 | P1 | D + gate dressing | Karnali river (dolphins); tigers as a rare sighting; Tharu villages; quieter than Chitwan | procedural |
| `ghm_lmk_koshi_tappu` | Koshi Tappu Wildlife Reserve (कोशी टप्पु वन्यजन्तु आरक्ष) | [r7569314](https://www.openstreetmap.org/relation/7569314) | 86.985259, 26.66114 | M4 | P1 | D + bird hides ≤ 4 k | Wetlands and water birds; Nepal's last wild water buffalo (arna) population (§10); Koshi barrage nearby (verify) | procedural |
| `ghm_lmk_janaki_mandir` | Janaki Mandir, Janakpur (जनकपुरधाम) | [n3595105449](https://www.openstreetmap.org/node/3595105449) | 85.92566, 26.730411 | M4 | **P0** | A ≤ 25 k | White palace-temple in Hindu-Rajput style with domes and chhatris, early 20th century (verify); dedicated to Sita (Janaki); Vivah Panchami festival (review); Mithila art in the town (§4.3 Terai motifs). OSM name is in capitals (data note) | needs-art |

### 5.5 Far west, Mustang, Langtang and east (M5)

| id | Landmark | OSM | lon, lat | M | P | Class and budget | Reference and cultural notes | Status |
|---|---|---|---|---|---|---|---|---|
| `ghm_lmk_lo_manthang` | Lo Manthang (लो मन्थाङ) | [n973481984](https://www.openstreetmap.org/node/973481984) | 83.956304, 29.182835 | M5 | **P0** | B: city wall and gate, royal palace exterior, red gompas (Jampa, Thubchen) ≤ 12 k each; houses from the TRANS_HIMALAYAN kit | Walled trans-Himalayan town; red gompas; striped chortens; firewood on the roofs; Tiji festival (review, P2). Interiors and murals are out of scope (review) | needs-art |
| `ghm_lmk_rara_lake` | Rara Lake (रारा ताल) | [w329747447](https://www.openstreetmap.org/way/329747447) | 82.089672, 29.529937 | M5 | P1 | D + shore dressing | Nepal's largest lake, ringed by blue pine (§6); national park, quiet, no motor boats | procedural |
| `ghm_lmk_shey_phoksundo` | Shey Phoksundo Lake (शे-फोक्सुन्डो ताल) | [w189103271](https://www.openstreetmap.org/way/189103271) | 82.948881, 29.196979 | M5 | P1 | D + Ringmo dressing ≤ 12 k | Turquoise and very deep (OSM depth=145); Ringmo village and a Bon gompa, where **circumambulation is counter-clockwise** (review); waterfall below the lake; no boating (review) | needs-art |
| `ghm_lmk_khaptad` | Khaptad National Park (खप्तड राष्ट्रिय निकुञ्ज) | [w294497219](https://www.openstreetmap.org/way/294497219) | 81.114026, 29.377994 | M5 | P2 | D + ashram dressing (C) | High plateau grasslands and forest; the ashram area has its own rules (review); the OSM name is Devanagari only | procedural |
| `ghm_lmk_langtang_valley` | Langtang (लाङटाङ) | [n976471702](https://www.openstreetmap.org/node/976471702) | 85.508976, 28.215756 | M5 | P1 | Dressing + Kyanjin Gompa (C) | Tamang villages. The old Langtang village was destroyed by the 2015 avalanche: **model the rebuilt village** and treat any memorial with respect (review); yak-cheese factory (verify) | needs-art |
| `ghm_lmk_gosaikunda` | Gosaikunda (गोसाइँकुण्ड) | [w24719058](https://www.openstreetmap.org/way/24719058) | 85.413292, 28.082764 | M5 | P1 | D + shrine dressing ≤ 3 k | Sacred alpine lake for Hindu and Buddhist pilgrims (Janai Purnima) (review); frozen in winter; no boating or swimming | procedural |
| `ghm_lmk_manaslu` | Manaslu (मनास्लु) | [n565159949](https://www.openstreetmap.org/node/565159949) | 84.559728, 28.549983 | M5 | P2 | D | Skyline of the Manaslu region | procedural |
| `ghm_lmk_kangchenjunga` | Kangchenjunga (कञ्चनजङ्घा) | [n4462458037](https://www.openstreetmap.org/node/4462458037) | 88.147477, 27.703011 | M5 | P1 | D | Far-east skyline; sacred to local communities (review). OSM has `tourism=viewpoint` on the peak (data note) | procedural |
| `ghm_lmk_ilam_tea_gardens` | Ilam tea gardens | [w503694961](https://www.openstreetmap.org/way/503694961) | 87.921372, 26.909969 | M5 | P1 | D + tea factory C ≤ 8 k | Rolling tea rows along the contours (§6); pickers with baskets (§9); Kanyam viewpoint (verify) | needs-art |
| `ghm_lmk_kalinchowk` | Kalinchowk Bhagwati | [r6742520](https://www.openstreetmap.org/relation/6742520) (**weak: ward boundary**) | 86.14954, 27.739186 | M5 | P2 | C ≤ 8 k | **Fix the OSM match before placement.** Hilltop shrine with clusters of tridents and bells; winter snow destination; cable car (verify) (review) | placeholder (greybox) |
| `ghm_lmk_hyatung_falls` | Hyatung Waterfall | [n9502459570](https://www.openstreetmap.org/node/9502459570) | 87.593916, 27.222445 | M5 | P2 | D + waterfall VFX | Very tall, multi-step waterfall (OSM height=365 (verify)) | procedural |

---
## 6. Vegetation

All vegetation uses `ghm_mat_toon_foliage`: chunky, rounded "puffball" leaf clumps modelled as opaque geometry (no alpha cards, which avoids overdraw), coloured from the gradient atlas, with vertex-colour wind (§1.9). The season changes colour through the biome season row and the bloom/autumn mask in vertex alpha, not separate meshes. Every tree species ships LOD0–LOD2 plus an **8-view impostor** baked into `ghm_tex_veg_impostor_*` for the mid ring (ARCHITECTURE §7.2). Scatter is GPU-instanced or drawn through BatchRendererGroup. Engineering owns the density tables per biome. Each species ships at least 2 shape variants (`_a`, `_b`); hero species ship 3. Wind: **T** = trunk sway (vertex G), **F** = leaf flutter (vertex B), **G** = grass bend.

| id | Species (Nepali name) | Biomes | M | P | Tris LOD0/LOD1/LOD2 + imp | Wind | Seasonal variants | Reference and notes | Status |
|---|---|---|---|---|---|---|---|---|---|
| `ghm_veg_rhododendron_tree_{a,b,c}` | Rhododendron tree (lali gurans) | HILL_FOREST, SUBALPINE_FOREST | M1 | **P0** | 1 500/500/150 + imp | T, F | **Spring bloom red, pink or white** (bloom mask); evergreen | National flower. Gnarled trunk, glossy leaf clusters. Bloom density peaks in Mar–Apr (verify per altitude) | needs-art |
| `ghm_veg_rhododendron_shrub_{a,b}` | Dwarf rhododendron | ALPINE_SCRUB | M3a | P0 | 400/120/— | F | Bloom (pink/white), autumn rust | Above the treeline | needs-art |
| `ghm_veg_sal_{a,b,c}` | Sal (sal / sakhuwa) | TERAI_SAL_FOREST, CHURE_FOREST | M2 | **P0** | 1 500/500/150 + imp | T, F | Spring leaf flush, light green; leaf-litter floor | Tall straight trunk, high crown; the dominant Terai forest | needs-art |
| `ghm_veg_chir_pine_{a,b}` | Chir pine (khote salla) | HILL_FOREST, CHURE_FOREST | M1 | P0 | 1 500/500/150 + imp | T | Evergreen | Long-needle tufts, open crown, reddish bark; dry south slopes about 500–2 000 m | needs-art |
| `ghm_veg_blue_pine_{a,b}` | Blue pine (gobre salla) | SUBALPINE_FOREST, TRANS_HIMALAYAN_* | M3a | P0 | 1 500/500/150 + imp | T | Evergreen | Drooping blue-green needles; Rara, Jomsom, Langtang | needs-art |
| `ghm_veg_fir_{a,b}` | Himalayan fir (talis patra (verify)) | SUBALPINE_FOREST | M3a | P0 | 1 200/400/120 + imp | T | Snow-laden winter variant (mask) | Dark conical crowns, about 3 000–4 000 m | needs-art |
| `ghm_veg_birch_{a,b}` | Himalayan birch (bhojpatra) | SUBALPINE_FOREST (treeline) | M3a | P1 | 1 200/400/120 + imp | T, F | **Autumn yellow**; bare in winter | White peeling bark; its bark was historically used for manuscripts | needs-art |
| `ghm_veg_oak_{a,b}` | Oak (banjh, khasru) | HILL_FOREST | M1 | P1 | 1 500/500/150 + imp | T, F | Evergreen; new flush in spring | Dense rounded crowns; mossy trunks at higher altitude | needs-art |
| `ghm_veg_alder_{a}` | Nepal alder (utis) | HILL_FOREST, landslide scars | M2 | P2 | 1 200/400/120 + imp | T, F | Evergreen | Pioneer tree on landslides and stream banks | needs-art |
| `ghm_veg_bamboo_{a,b}` | Bamboo clump (bans) | HILL_TERRACES, TERAI_*, villages | M1 | P0 | 1 200/400/120 + imp | T, F | Evergreen | Arching clumps beside houses; source of doko and fences | needs-art |
| `ghm_veg_peepal_{a,b}` | Peepal (pipal), sacred fig | Chautari, temples, villages | M1 | **P0** | 1 500/500/150 + imp | T, F (strong) | Brief spring leaf change | Heart-shaped leaves with drip tips; red thread and cloth wrapped round sacred trunks; often planted beside a banyan at a chautari (review) | needs-art |
| `ghm_veg_banyan_{a,b}` | Banyan (bar) | Chautari, Terai, valley | M1 | P0 | 1 500/500/150 + imp | T | Evergreen | Aerial roots modelled as a few chunky strands | needs-art |
| `ghm_veg_marigold_bed` | Marigold (sayapatri) bed | Gardens, temple stalls | M1 | P1 | 300/100/— | F | Autumn peak (Tihar) | Orange and yellow pompoms; garlands are props (§7) | needs-art |
| `ghm_veg_mustard_patch` | Mustard (tori) field patch, 1 m² | TERAI_CROPLAND, VALLEY_CROPLAND | M1 | P1 | 60/20/— (terrain colour beyond) | G | **Winter yellow bloom**; otherwise hidden | A signature winter look | needs-art |
| `ghm_veg_paddy_patch_{seedling,growing,ripe,stubble}` | Rice paddy patch, 1 m² | TERAI_PADDY, HILL_TERRACES, VALLEY_CROPLAND | M1 | **P0** | 60/20/— | G | Four stages by season: transplanted seedlings in water (early monsoon) → green → gold (autumn) → stubble and drying stacks | Patch cards fade into the terrain colour beyond 60 m | needs-art |
| `ghm_veg_rice_stack` | Harvested rice stacks | Fields in autumn | M2 | P2 | 200/60/— | — | Autumn only | Conical straw stacks (verify local form) | needs-art |
| `ghm_veg_millet_patch` | Finger millet (kodo) patch | HILL_TERRACES | M2 | P1 | 60/20/— | G | Green → brown heads (autumn) | Dry terrace crop | needs-art |
| `ghm_veg_maize_patch` | Maize (makai) patch | HILL_TERRACES, VALLEY_CROPLAND | M1 | P1 | 120/40/— | G, F | Summer tall green → dry tan | Dried cobs hang on houses (§4.3) | needs-art |
| `ghm_veg_wheat_patch` | Wheat patch | VALLEY_CROPLAND, TERAI_CROPLAND | M1 | P2 | 60/20/— | G | Winter green → spring gold | — | needs-art |
| `ghm_veg_barley_buckwheat_patch` | Barley and buckwheat patches | TRANS_HIMALAYAN_CROPLAND | M3a | P1 | 60/20/— | G | Green → gold; buckwheat pink bloom (review timing) | — | needs-art |
| `ghm_veg_potato_patch` | Potato field rows | ALPINE / Khumbu fields | M3b | P2 | 60/20/— | — | Summer green | Walled stone fields around Sherpa villages | needs-art |
| `ghm_veg_tea_bush_{a,b}` | Tea bush (chiya) | TEA_GARDEN | M5 | **P0** | 300/100/— | F (light) | Spring flush lighter green | Clipped, rounded, waist-high bushes instanced along contour rows; occasional shade trees | needs-art |
| `ghm_veg_cardamom_clump` | Large cardamom (alainchi) | HILL_FOREST (east) | M5 | P1 | 400/120/— | F | Evergreen | Broad leaf clumps under shade trees (Ilam, Taplejung) | needs-art |
| `ghm_veg_banana_{a,b}` | Banana (kera) | Terai, low hills, villages | M2 | P1 | 600/200/60 | F (strong) | Evergreen | Big paddle leaves; near houses | needs-art |
| `ghm_veg_mango_{a}` | Mango (aanp) | Terai | M4 | P2 | 1 500/500/150 + imp | T, F | Spring blossom | Dense dark crowns around villages | needs-art |
| `ghm_veg_simal_{a}` | Silk-cotton tree (simal) | Terai, Chure | M4 | P2 | 1 500/500/150 + imp | T | **Red bloom on bare branches** (late winter) | (verify bloom timing) | needs-art |
| `ghm_veg_poplar_willow_{a,b}` | Poplar and willow | TRANS_HIMALAYAN_CROPLAND | M3a | P1 | 1 200/400/120 + imp | T, F | Autumn gold | Lines along irrigation channels (Mustang, Manang) | needs-art |
| `ghm_veg_apple_{a,b}` | Apple tree | ORCHARD | M3a | P1 | 1 000/350/100 + imp | T, F | Spring blossom white-pink; autumn fruit | Marpha and Jumla orchards | needs-art |
| `ghm_veg_orchid_cluster` | Epiphytic orchid cluster | HILL_FOREST, SUBALPINE_FOREST | M2 | P2 | 150/—/— | F | Spring and monsoon bloom | Attached to tree trunks; a photo-collectible (§12) | needs-art |
| `ghm_veg_juniper_{a,b}` | Juniper (dhupi) | ALPINE_SCRUB, TRANS_HIMALAYAN_STEPPE | M3a | P0 | 800/250/80 | T | Evergreen | Burned as incense at gompas and passes (smoke VFX, §11) | needs-art |
| `ghm_veg_grass_{lawn,meadow,tussock,tall}` | Grass clumps | per biome | M1 | P0 | 120/40/— | G | Biome season row | `tall` is elephant grass for TERAI_GRASSLAND (instanced bands, 400/120) | needs-art |
| `ghm_veg_fern_{a}` | Fern clump | HILL_FOREST | M2 | P2 | 200/60/— | F | Monsoon lush | — | needs-art |
| `ghm_veg_reeds_{a}` | Reeds | WETLAND, lakeshores | M2 | P1 | 200/60/— | G | Autumn tan | Phewa and Koshi Tappu shores | needs-art |
| `ghm_veg_wildflowers_{alpine,hill}` | Wildflower specks | ALPINE_MEADOW, HILL_GRASSLAND | M3a | P2 | 60/—/— | G | Summer bloom | Primula-like pinks, yellows and purples | needs-art |
| `ghm_veg_shrub_generic_{a,b,c}` | Generic shrubs | HILL_SCRUB, URBAN_GREEN, CHURE | M1 | P0 | 400/120/— | F | Season row | Filler | needs-art |
| `ghm_veg_hedge_{a}` | Hedge segment, 2 m | Urban gardens, compounds | M1 | P2 | 200/60/— | — | — | — | needs-art |
| `ghm_veg_moss_lichen_decal` | Moss and lichen patches | Rocks, walls (altitude) | M3a | P2 | decal | — | — | Shared decal atlas | needs-art |
| `ghm_veg_snag_{a}` | Dead tree / snag | SUBALPINE, BARE_SOIL | M3a | P2 | 600/200/60 | T | — | — | needs-art |

---

## 7. Props

Props use `ghm_mat_toon_lit` (palette) unless the row says otherwise. Cloth uses `ghm_mat_cloth_flutter`. Small props are instanced. **Inst** = GPU instancing or BatchRendererGroup; **Proc** = generated geometry (catenaries, strips) from kit pieces. Tris are LOD0/LOD1. **Not authored:** the khukuri or any other weapon as a prop, pickup or inventory item (no weapon emphasis); tools such as sickles appear only in work animations. Revisit ceremonial depictions only if the consultant asks (review).

### 7.1 Religious and cultural

| id | Prop | M | P | Tris | Anim / instancing | Reference and cultural notes | Status |
|---|---|---|---|---|---|---|---|
| `ghm_prp_lungta_string` | Prayer-flag string (lungta) | M1 | **P0** | proc, 2 tris per flag | Cloth flutter; Proc catenary between anchors | Repeating colour order **blue, white, red, green, yellow** (`w.flag.*`). Printed text is abstracted to block lines, never fake script (review) | needs-art |
| `ghm_prp_darchor_pole_{a,b}` | Vertical prayer-flag pole (darchor) | M1 | P0 | 300/100 | Flutter | Tall vertical flags on poles at gompas, passes and houses (review) | needs-art |
| `ghm_prp_prayerwheel_hand` | Hand prayer wheel | M1 | **P0** | 300/100 | Spins clockwise in the hand (§9) | Player and NPC hand prop | needs-art |
| `ghm_prp_prayerwheel_row_{4,8}` | Wall-mounted prayer-wheel row | M1 | **P0** | 800/250 | Wheels spin when touched while walking clockwise; Inst | Boudha and Swayambhu bases; mantra relief abstracted (review) | needs-art |
| `ghm_prp_prayerwheel_large` | Large prayer wheel in its house | M1 | P1 | 1 500/500 | Spins slowly; bell ding per rotation | Bell strikes once per turn (verify) | needs-art |
| `ghm_prp_mani_wall_{2m,4m}` | Mani wall segment | M3a | **P0** | 300/100 | Inst along `LineKind.MANI_WALL` | Carved stones on top. Pass with the wall on your right; NPC paths respect this (review). Carved script is stylised unless verified by a reader (review) | needs-art |
| `ghm_prp_mani_stone_{a,b,c}` | Carved mani stones (piles) | M3a | P0 | 150/50 | Inst | Painted carved lettering (review) | needs-art |
| `ghm_prp_chautari_{s,m}` | Chautari resting platform | M2 | **P0** | 800/250 | — | Stone platform around a peepal or banyan, with a ledge at porter-load height (§6) | needs-art |
| `ghm_prp_dhunge_dhara_{a,b}` | Stone water spout (dhunge dhara / hiti) | M1 | **P0** | 1 200/400 | Water stream VFX | Sunken stepped pit with carved spouts (makara heads (review)); people fetch water and wash | needs-art |
| `ghm_prp_temple_bell_{s,m,l}` | Temple bells | M1 | **P0** | 300/600/1 200 | Swing on ring (§9); SFX | Small hanging bells to large bells on pillared frames | needs-art |
| `ghm_prp_butter_lamp_{single,tray}` | Butter lamps | M1 | P0 | 60/400 | Flame flicker sprite (no realtime light on Low) | Buddhist lamp houses and trays (review) | needs-art |
| `ghm_prp_diyo_{single,row}` | Clay oil lamps (diyo) | M1 | P1 | 40/300 | Flicker | Hindu shrines; Tihar rows | needs-art |
| `ghm_prp_offering_plate` | Puja offering plate | M1 | P1 | 150 | — | Flowers, rice, red tika powder, incense (review) | needs-art |
| `ghm_prp_incense_burner_{stick,sang}` | Incense / juniper burner | M1 | P1 | 200 | Smoke VFX | Sang burners (whitewashed chimney ovens) at gompas and passes (review) | needs-art |
| `ghm_prp_khata_scarf` | Khata (ceremonial white scarf) | M3a | P1 | 60 | Flutter | Tied on bridges and at passes; also a greeting prop (review) | needs-art |
| `ghm_prp_chaitya_stone_{s,m}` | Newar stone chaitya | M1 | P1 | 500/150 | Inst | Courtyard centrepiece (review) | needs-art |
| `ghm_prp_deity_stone_{a,b}` | Painted deity stone / relief | M1 | P1 | 200/60 | Inst | Red-orange painted, flowers; generic, never a recognisable specific image without review (review) | needs-art |
| `ghm_prp_trishul_cluster` | Tridents with bells | M1 | P1 | 300/100 | — | Shiva shrines; Kalinchowk | needs-art |
| `ghm_prp_cairn_{s,m}` | Stone cairns | M3a | P0 | 200/60 | Inst | Passes and summits, often with flags | needs-art |
| `ghm_prp_monastery_horn_stand` | Long horn (dungchen) on stand | M3b | P2 | 400/120 | — | Festival dressing (review) | needs-art |

### 7.2 Street, market and household

| id | Prop | M | P | Tris | Anim / instancing | Reference and notes | Status |
|---|---|---|---|---|---|---|---|
| `ghm_prp_power_pole_{concrete,steel}` | Power / telecom pole | M1 | **P0** | 300/100 | Inst | Iconic Kathmandu look; transformer and junction-box variants | needs-art |
| `ghm_prp_wire_tangle_{a,b,c}` | Tangled wire bundles | M1 | **P0** | proc + 400 coils | Proc catenaries between poles + coil clusters | **Iconic.** Dense in urban cores, sparse in villages. Generated along roads; thickness ≥ 3 cm for readability | needs-art |
| `ghm_prp_transformer` | Pole-mounted transformer | M1 | P1 | 500/150 | — | — | needs-art |
| `ghm_prp_streetlight_{a,b}` | Streetlights | M1 | P1 | 200/60 | Emissive at night | — | needs-art |
| `ghm_prp_road_sign_{warning,info,direction}` | Generic road signs (Nepali + English) | M1 | P0 | 100/30 | Inst; text from the signage atlas | Generic designs; place names from data via runtime-shaped text where possible (verify world-space text approach, §14) | needs-art |
| `ghm_prp_km_stone` | Kilometre milestone | M2 | P1 | 120/40 | Inst | Generic painted stone (verify local style) | needs-art |
| `ghm_prp_bus_stop_{a,b}` | Bus stop shelter / sign | M1 | P1 | 600/200 | — | Generic, bilingual | needs-art |
| `ghm_prp_vendor_cart_{fruit,snack,veg}` | Street vendor carts | M1 | **P0** | 1 000/300 | — | Bicycle carts and pushcarts; generic goods | needs-art |
| `ghm_prp_tea_stall` | Tea stall (chiya pasal) | M1 | **P0** | 1 500/500 | Steam VFX | Bench, kettle, glass cups, biscuit jars (unbranded) | needs-art |
| `ghm_prp_momo_steamer` | Momo steamer stack | M1 | P1 | 300/100 | Steam VFX | — | needs-art |
| `ghm_prp_market_stall_{a,b}` | Market stall with tarp roof | M1 | P0 | 1 200/400 | Tarp flutter | Vegetables, fabrics, pots | needs-art |
| `ghm_prp_goods_{fruit,veg,pots,fabric,sacks}` | Goods piles | M1 | P0 | 200–400 | Inst | Generic, unbranded | needs-art |
| `ghm_prp_pottery_{wheel,pots_drying}` | Pottery wheel and drying pots | M1 | P2 | 400/800 | Wheel spin | Bhaktapur Pottery Square | needs-art |
| `ghm_prp_juju_dhau_bowl` | Curd in a clay bowl | M1 | P2 | 60 | — | Bhaktapur food prop | needs-art |
| `ghm_prp_doko` | Doko basket | M1 | **P0** | 300/100 | Carried (§9) | Conical bamboo basket carried with a namlo head strap | needs-art |
| `ghm_prp_namlo` | Namlo head strap | M1 | P0 | 60 | Part of the porter rig | — | needs-art |
| `ghm_prp_tokma` | Porter's T-stick | M3b | P2 | 60 | Rest pose (§9) | Supports the load during rests (review) | needs-art |
| `ghm_prp_mudha_stool` | Bamboo/cane stool (mudha) | M1 | P1 | 200/60 | Inst | — | needs-art |
| `ghm_prp_plastic_chair_{a}` | Plastic chair and table | M1 | P1 | 200 | Inst | Generic, unbranded | needs-art |
| `ghm_prp_bench_{wood,stone}` | Benches | M1 | P1 | 200/60 | Inst | — | needs-art |
| `ghm_prp_gas_cylinder` | LPG cylinder | M1 | P2 | 150 | Inst | Generic colours, no company marks | needs-art |
| `ghm_prp_laundry_line` | Laundry line | M1 | P1 | proc | Flutter | Saris and shirts, pattern atlas | needs-art |
| `ghm_prp_water_tank_ground` | Ground water tank / tap stand | M1 | P1 | 300/100 | — | Public tap stands (verify) | needs-art |
| `ghm_prp_fuel_pump` | Generic fuel pump | M1 | P1 | 600/200 | — | `PoiKind.FUEL`. **No company logos** | needs-art |
| `ghm_prp_bin_{a}` | Bins | M1 | P2 | 150 | Inst | — | needs-art |
| `ghm_prp_welcome_gate_{a,b}` | Village / municipality welcome gate | M2 | P1 | 1 200/400 | — | Concrete arch with a generic bilingual welcome text | needs-art |
| `ghm_prp_flagpole_nepal` | Flagpole with the Nepal flag | M1 | P0 | 200 + flag | Cloth flutter on a non-rectangular mesh | Flag geometry from §12.7. Schools, offices, airports | needs-art |

### 7.3 Transport, bridges and water

| id | Prop | M | P | Tris | Anim / instancing | Reference and notes | Status |
|---|---|---|---|---|---|---|---|
| `ghm_prp_suspension_bridge_{tower,deck_2m,cable}` | Trekking suspension bridge kit | M2 | **P0** | tower 800; deck 120 per 2 m; cables proc | Gentle sway; flags and khatas flutter | **Iconic.** Steel cables, mesh sides, prayer flags and khatas tied on. Span comes from the OSM bridge length | needs-art |
| `ghm_prp_wooden_bridge_{4m,8m}` | Wooden footbridge (cantilever / log) | M2 | P0 | 400/800 | — | Village and trail crossings | needs-art |
| `ghm_prp_boat_dock_{a,b}` | Boat dock / jetty | M2 | P0 | 600/200 | — | Phewa ghats and docks | needs-art |
| `ghm_prp_ghat_steps_{2m,4m}` | River ghat steps | M1 | P1 | 200/400 | — | Bagmati ghats (Pashupatinath), lakes | needs-art |
| `ghm_prp_windsock` | Windsock | M2 | P0 | 200 | Wind-driven | Airports, paragliding launch | needs-art |
| `ghm_prp_helipad_{h}` | Helipad marking + light ring | M3b | P1 | decal + 100 | — | `PoiKind.HELIPAD` | needs-art |
| `ghm_prp_runway_lights` | Runway lights | M1 | P2 | 40 | Inst, emissive | — | needs-art |
| `ghm_prp_airstairs` | Generic boarding stairs | M3b | P1 | 400 | — | STOL flights | needs-art |
| `ghm_prp_cablecar_{tower,station_s}` | Cable-car tower and small station | M1 | P2 | 600 / 4 000 | Cabin moves (§8) | Shared by Chandragiri, Manakamana, Kalinchowk | needs-art |
| `ghm_prp_water_mill` | Water mill (ghatta) hut | M2 | P2 | 800/250 | Paddle spin, stream VFX | Traditional streamside mill (review) | needs-art |
| `ghm_prp_fishing_net_{a}` | Fishing nets / cages | M2 | P2 | 300 | — | Begnas, rivers | needs-art |
| `ghm_prp_raft_pile` | Stacked rafts at put-ins | M5 | P2 | 400 | — | — | needs-art |

### 7.4 Trekking, agriculture and landscape

| id | Prop | M | P | Tris | Anim / instancing | Reference and notes | Status |
|---|---|---|---|---|---|---|---|
| `ghm_prp_trail_sign_{wood,metal}` | Trail signposts | M3a | **P0** | 150/50 | — | Bilingual arrows, altitudes and times (generic design) | needs-art |
| `ghm_prp_checkpost_hut` | Permit checkpost hut | M3a | P1 | 1 000/300 | — | Generic; no agency logos | needs-art |
| `ghm_prp_teahouse_menu_board` | Teahouse menu board | M3a | P1 | 100 | — | Bilingual generic menu (dal bhat, tea, noodles) | needs-art |
| `ghm_prp_solar_panel_{a}` | Solar panel | M3a | P1 | 100 | Inst | Teahouses, villages | needs-art |
| `ghm_prp_tent_{dome_a,dome_b,mess}` | Tents | M3b | **P0** | 300/600/800 | Flutter (High) | EBC (spring season) and campsites; generic yellow and orange | needs-art |
| `ghm_prp_terrace_wall_{stone,earth}_{2m,4m}` | Terrace retaining walls | M2 | **P0** | 60/100 per piece | Proc along contours | Defines the HILL_TERRACES look | needs-art |
| `ghm_prp_fence_{bamboo,stone,wood,wire}_{2m}` | Fences and field walls | M1 | P0 | 60–150 | Inst along lines | Stone field walls at altitude; bamboo in villages | needs-art |
| `ghm_prp_haystack_pole` | Haystack round a pole (pira) | M2 | P1 | 400/120 | Inst | Hill farms | needs-art |
| `ghm_prp_firewood_stack_{a,b}` | Firewood stacks | M2 | P1 | 300/100 | Inst | — | needs-art |
| `ghm_prp_drying_mat_{grain,chilli}` | Grain / chilli drying mats | M2 | P2 | 60 | Inst | Courtyards and rooftops in autumn | needs-art |
| `ghm_prp_plough_wood` | Wooden plough | M2 | P2 | 200 | — | Used with buffalo or oxen | needs-art |
| `ghm_prp_beehive_log` | Log beehive | M2 | P2 | 200 | Bee VFX | Hung under the eaves (verify) | needs-art |
| `ghm_prp_trough_{stone,wood}` | Animal trough | M2 | P2 | 150 | Inst | — | needs-art |
| `ghm_prp_rock_{s,m,l,xl}` | Rocks and boulders | M1 | **P0** | 60/200/600/1 500 | Inst | Palette rock + noise; river-rounded variants | needs-art |
| `ghm_prp_river_stones` | River stone scatter | M1 | P1 | 60 | Inst | — | needs-art |
| `ghm_prp_signboard_photo_spot` | Photo-spot marker | M2 | P2 | 200 | — | Generic | needs-art |

### 7.5 Festival props (Dashain, Tihar and others)

| id | Prop | M | P | Tris | Anim / instancing | Reference and notes | Status |
|---|---|---|---|---|---|---|---|
| `ghm_prp_kite_{a,b,c}` | Kites (changa) | M2 | P1 | 60 | Flutter, string catenary | Dashain sky full of kites; pattern variants | needs-art |
| `ghm_prp_linge_ping` | Bamboo swing (linge ping) | M2 | P1 | 800/250 | Swing animation (§9) | Tall four-pole bamboo swing for Dashain (review construction) | needs-art |
| `ghm_prp_rote_ping` | Wooden ferris swing (rote ping) | M5 | P2 | 1 200/400 | Rotates | Dashain (review) | needs-art |
| `ghm_prp_garland_marigold_{s,l}` | Marigold garlands | M1 | P1 | 200 | Gentle sway | Doorways, vehicles, people, animals (Tihar) | needs-art |
| `ghm_prp_jamara_tika` | Jamara (barley shoots) and tika plate | M2 | P2 | 200 | — | Dashain tika (review) | needs-art |
| `ghm_prp_tihar_lights` | String lights and lamps | M5 | P2 | proc | Emissive twinkle | Tihar | needs-art |
| `ghm_prp_losar_flags` | New prayer flags for Losar | M5 | P2 | reuse lungta | — | Fresh-flag variant (review) | needs-art |

---
## 8. Vehicles

**All vehicles are generic.** Proportions are allowed to evoke a class or an era ("1990s small taxi", "Twin-Otter-like STOL"), but there are no real manufacturer badges, grilles, logos, model names or airline liveries. Number plates use fictional numbers in a generic Nepali style, with colour coded by category (private, public, government; verify the current scheme). **Rig:** a transform hierarchy, not skinned. Standard nodes are `body` (the squash pivot), `steer`, `wheel_{FL,FR,RL,RR}` (plus `wheel_M*` for multi-axle), `susp_*` visual pivots, `sock_driver`, `sock_pass_NN`, `sock_cargo`, `sock_exhaust`, `sock_light_{L,R}` and `sock_horn_fx`. Two-wheelers add `fork`, `swingarm`, `ik_hand_{L,R}`, `ik_foot_{L,R}` and `sock_pillion`. **Squash animations** are procedural springs on `body`: start shake, honk bounce (body squash + horn puff), landing squash, bump wobble, stuck wiggle with recovery hop (ARCHITECTURE §7.6), and a parked idle sway. No damage states, ever. **Paint:** body colour comes from the instanced tint (vertex G mask), with 6–8 presets per vehicle. A mud-level mask darkens the lower body after MUD and washes off in rain or water (`ghm_vfx_mud_splash`). **Riders always wear helmets** on motorbikes and bicycles. Travel profiles follow `model.Travel`.

| id | Vehicle | Travel | M | P | Tris LOD0/LOD1/LOD2 | Rig, wheels and sockets | Paint variants | Reference and notes | Status |
|---|---|---|---|---|---|---|---|---|---|
| `ghm_veh_scooter_a` | Scooter | MOTORBIKE | M1 | **P0** | 4 000/1 300/400 | 2 wheels, fork, ik hands/feet, pillion | 8 pastel and bright | The urban step-through scooter | needs-art |
| `ghm_veh_motorbike_commuter_a` | Commuter motorbike (100–150 cc) | MOTORBIKE | M1 | **P0** | 4 500/1 500/450 | 2 wheels, fork, swingarm, pillion, side box | 8 | The M1 hero ride ("ride a motorbike to Boudha") | needs-art |
| `ghm_veh_motorbike_trail_a` | Trail / enduro bike | MOTORBIKE | M1 | P2 | 4 500/1 500/450 | Long-travel suspension (visual) | 6 | Dirt and mountain roads | needs-art |
| `ghm_veh_motorbike_cruiser_a` | Cruiser (generic retro "Royal-style") | MOTORBIKE | M1 | P2 | 5 000/1 600/500 | Teardrop tank, round lamp, pillion | 6 incl. chrome-and-black | Highway touring; no real brand cues | needs-art |
| `ghm_veh_bicycle_a` | Bicycle (city roadster) | BICYCLE | M1 | P1 | 2 000/700/200 | Pedal crank pivot, ik feet on pedals | 6 | Bell SFX | needs-art |
| `ghm_veh_mtb_a` | Mountain bike | BICYCLE | M1 | P2 | 2 500/800/250 | Front fork travel (visual) | 6 | — | needs-art |
| `ghm_veh_taxi_small_a` | Small taxi (1990s-era city car) | CAR | M1 | **P0** | 5 000/1 600/500 | 4 wheels, 4 seats, roof sign socket | Taxi colour scheme (verify) + roof sign "TAXI / ट्याक्सी" | Tiny boxy hatchback silhouette | needs-art |
| `ghm_veh_hatchback_a` | Hatchback | CAR | M1 | **P0** | 5 000/1 600/500 | 4 wheels, 5 seats | 8 | Most common private car | needs-art |
| `ghm_veh_ev_compact_a` | Compact electric car | CAR | M1 | P2 | 5 000/1 600/500 | 4 wheels | 8 | EVs are common in Kathmandu (verify share) | needs-art |
| `ghm_veh_suv_a` | SUV | JEEP | M1 | P1 | 5 500/1 800/550 | 4 wheels, 7 seats, roof rails | 8 | — | needs-art |
| `ghm_veh_jeep_mountain_a` | Mountain / shared jeep | JEEP | M2 | **P0** | 5 500/1 800/550 | High clearance, roof rack + cargo socket, rear bench seats | 6 (incl. painted panels) | Boxy 4×4 used on rough hill roads; roof luggage | needs-art |
| `ghm_veh_jeep_safari_a` | Open safari jeep | JEEP | M4 | **P0** | 5 500/1 800/550 | Open bed with bench seats, photo stand | 3 (green, sand, white) | Chitwan and Bardiya | needs-art |
| `ghm_veh_microvan_a` | Microbus ("micro") | CAR | M1 | P1 | 5 500/1 800/550 | Sliding door, 12+ seats | White + stripe variants | Ubiquitous urban minibus | needs-art |
| `ghm_veh_bus_city_a` | City bus | BUS | M1 | **P0** | 6 000/2 000/600 | 2 axles, standing passenger sockets | 4 liveries (generic) | Real bus routes (ROADMAP 1.8) | needs-art |
| `ghm_veh_bus_tourist_a` | Long-distance tourist bus | BUS | M2 | **P0** | 6 000/2 000/600 | Roof luggage rack + cargo, 40 seats | Truck-art panels (§8.1) | The Kathmandu–Pokhara bus experience. Riders on the roof only as P2 ambience (review child-safety messaging) | needs-art |
| `ghm_veh_tempo_safa_a` | Electric three-wheeler (safa tempo) | CAR | M1 | P1 | 3 500/1 200/350 | 3 wheels, rear bench | Colours (verify) | Electric shared tempo, quiet whine SFX | needs-art |
| `ghm_veh_rickshaw_cycle_a` | Cycle rickshaw | BICYCLE | M4 | **P0** | 2 500/800/250 | Puller IK + 2 passenger sockets, canopy | 6 painted, tassels | Terai towns; optional early use in Thamel | needs-art |
| `ghm_veh_tractor_a` | Tractor + trolley | BUS | M1 | P2 | 5 000/1 600/500 | Trolley as a linked child | 3 (red, blue, green) | Farm transport; trolley with sand or bricks | needs-art |
| `ghm_veh_truck_plain_a` | Truck (plain) | BUS | M1 | P1 | 6 000/2 000/600 | 2 axles, cargo socket | 6 solid colours | Valley traffic before the hero art lands | needs-art |
| `ghm_veh_truck_painted_a` | **Decorated painted truck** (hero art) | BUS | M2 | **P0** | 6 000/2 000/600 | Crown headboard over the cab, hanging tassels and chains (vertex sway), mudflaps | 6+ truck-art schemes (§8.1) | Nepali truck-art motifs: floral borders, peacocks, mountain landscapes, deities (review), slogans in Devanagari and English (wording (review)) | needs-art |
| `ghm_veh_tanker_a` | Fuel / water tanker | BUS | M1 | P2 | 6 000/2 000/600 | Tank as a separate mesh | 3; **no company marks** | — | needs-art |
| `ghm_veh_horse_a` / `ghm_veh_mule_a` | Rideable horse / mule | HORSE | M3a | **P0** | 4 000/1 500/500 | Animal rig (§10), saddle socket, rider IK; pack-load variant | Coat colours ×4; saddle blankets | Mustang and Annapurna pack trains; bells | needs-art |
| `ghm_veh_yak_a` | Rideable / pack yak | HORSE | M3b | **P0** | 4 000/1 500/500 | Animal rig (§10), saddle and pack sockets | Black, white-patched, brown | Pack trains have **right of way**: stand on the hillside, never the cliff side (gameplay tip) | needs-art |
| `ghm_veh_paddleboat_swan_a` | Swan paddle boat | — (water) | M2 | **P0** | 2 500/800/250 | Pedal pivots, 2 seats | White, pink, yellow | Phewa Lake | needs-art |
| `ghm_veh_doonga_a` | Rowboat (doonga) | — (water) | M2 | **P0** | 1 500/500/150 | Oar pivots, 4 seats + rower | **Colourful painted** (red, blue, yellow, green) | Phewa's iconic wooden boats | needs-art |
| `ghm_veh_canoe_dugout_a` | Dugout canoe | — (water) | M4 | **P0** | 1 500/500/150 | Pole / paddle pivot, 6 seats | Wood only | Chitwan Rapti river safari | needs-art |
| `ghm_veh_raft_a` | Raft (8-person) | — (water) | M5 | P1 | 2 000/700/200 | 8 paddler sockets, guide socket | 4 bright | Trishuli, Seti, Bhote Koshi. Rafting arrives late (scope) | needs-art |
| `ghm_veh_kayak_a` | Kayak | — (water) | M5 | P2 | 1 000/350/100 | Paddle IK | 6 | — | needs-art |
| `ghm_veh_stol_turboprop_a` | STOL twin turboprop (Twin-Otter-like) | — (air) | M3b | **P0** | 8 000/2 500/800 | Props spin (blurred disc at speed), flaps, gear fixed, doors; 18 passenger sockets | 3 generic liveries | The Lukla hero sequence. Generic: no airline marks | needs-art |
| `ghm_veh_helicopter_a` | Mountain helicopter (single engine) | — (air) | M3b | **P0** | 8 000/2 500/800 | Main and tail rotor spin + blur discs, skids, 5 seats | 3 generic | Rescue/scenic look **without** medical crosses | needs-art |
| `ghm_veh_ultralight_a` | Ultralight trike | — (air) | M2 | P2 | 4 000/1 300/400 | Wing pivot, prop | 4 | Pokhara scenic flights | needs-art |
| `ghm_veh_paraglider_a` | Paraglider (tandem) | — (air) | M2 | **P0** | wing 2 500 + harness 800 / 1 000 / 300 | Wing shape keys via bone chain (8 bones), lines as procedural lines, pilot + passenger IK | 8 wing patterns | Vertex flutter at the wing trailing edge; Sarangkot | needs-art |
| `ghm_veh_balloon_a` | Hot-air balloon | — (air) | M2 | P2 | 3 000/1 000/300 | Burner flame FX socket, basket sockets | 4 | Pokhara balloon rides (verify still operating) | needs-art |
| `ghm_veh_cablecar_cabin_a` | Cable car cabin | — (cable) | M1 | P2 | 2 000/700/200 | Hanger arm, 6–8 seats, door | 4 colours | Chandragiri, Manakamana, Kalinchowk; no operator marks | needs-art |
| `ghm_veh_zipline_trolley_a` | Zipline trolley + harness | — (cable) | M2 | P2 | 1 000/350/— | Trolley on cable, rider socket | 2 | Pokhara zipline | needs-art |
| `ghm_veh_bungee_rig_a` | Bungee harness + cord | — (rig) | M3b | P1 | 1 000/350/— | Cord as a procedural spring spline; ankle and body harness sockets | 2 | Bhote Koshi and Kushma; never a frightening outcome | needs-art |

### 8.1 Truck-art and livery atlas

| id | Asset | M | P | Spec | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_veh_truckart_set` | Truck and bus art decal set (fills `ghm_tex_truckart_atlas`) | M2 | **P0** | ~24 cells in the 1024² atlas (§1.6) | Floral borders, peacocks, Himalaya scenes, lotus, birds, eye-motif headboards (review), deity panels (review), generic slogans in Devanagari and English (review wording); no real names | needs-art |
| `ghm_veh_tassel_chain_set` | Tassels and chains | M2 | P1 | 300 tris, vertex sway | Hung from bumpers and mirrors | needs-art |
| `ghm_veh_plate_set` | Generic number plates | M1 | P0 | Atlas cells, ASTC 4×4 | Fictional numbers; private, public and government colour schemes (verify) | needs-art |
| `ghm_veh_marigold_garland` | Vehicle garland (Tihar / puja) | M5 | P2 | 200 tris | Seasonal variant on vehicles | needs-art |

---

## 9. Characters

### 9.1 Humanoid rig (`hum`)

The rig is ≤ **40 bones**, Unity Humanoid, shared by the player and all NPCs so every clip retargets. Bones: root, `squash`, hips, spine, chest, neck, head (7). Shoulder, upper arm, forearm and hand ×2 (8). Thumb 2 + mitten fingers 2, ×2 (8). Thigh, shin, foot and toe ×2 (8). Props: `prop_R`, `prop_L`, `back_attach` (doko/backpack) and `head_attach` (hats, helmets) (4). Cloth: `skirt_F`, `skirt_B` (sari, kurta and robe hems; driven by a spring) (2). **Total 37**, with 3 spare. Skin weights: 4 per vertex on the player, 2 per vertex on NPCs from LOD1. Faces use the face atlas (§1.12). Body types: 4 adult builds, 2 child builds and an elder posture layer, all on the same skeleton. Skin tones and hair come from palette swatches covering the real diversity of Nepal's peoples (review).

### 9.2 Player customisation

| id | Item | Slot | M | P | Tris (adds to body) | Reference and cultural notes | Status |
|---|---|---|---|---|---|---|---|
| `ghm_chr_player_base_{adult_a..d}` | Player base bodies (4 builds) | body | M1 | **P0** | 3 200 | Chunky, rounded proportions; 4 skin-tone sets; 12 hairstyles | needs-art |
| `ghm_chr_player_face_set` | Expression set | face | M1 | **P0** | atlas | Blink, smile, surprise, tired, two talk shapes | needs-art |
| `ghm_chr_outfit_casual_{a,b,c}` | T-shirt, jeans, hoodie | torso, legs | M1 | **P0** | 800 | Default outfits | needs-art |
| `ghm_chr_outfit_dhaka_topi` | Dhaka topi | head | M1 | **P0** | 200 | Traditional cap of woven dhaka cloth; pattern from the pattern atlas (review pattern) | needs-art |
| `ghm_chr_outfit_daura_suruwal` | Daura suruwal + waistcoat | torso, legs | M1 | P1 | 1 000 | National dress for men: tied tunic and fitted trousers (review cut and ties) | needs-art |
| `ghm_chr_outfit_kurta_suruwal_{a,b}` | Kurta suruwal | torso, legs | M1 | P1 | 1 000 | Common everyday wear, with a shawl option | needs-art |
| `ghm_chr_outfit_sari_{a,b}` | Sari + blouse | full | M1 | P1 | 1 200 | Skirt bones animate the hem (review drape) | needs-art |
| `ghm_chr_outfit_gunyu_cholo` | Gunyu cholo | full | M2 | P2 | 1 200 | Traditional dress for girls and young women (review) | needs-art |
| `ghm_chr_outfit_hakupatasi` | Hakupatasi (Newar black sari with a red border) | full | M1 | P2 | 1 200 | Newar festival dress (review) | needs-art |
| `ghm_chr_outfit_dhaka_{shawl,jacket,scarf}` | Dhaka-pattern unlocks | torso, accessory | M2 | P1 | 400–800 | Collection rewards; regional pattern colourways (review) | needs-art |
| `ghm_chr_outfit_trek_{down,fleece,pants,boots}` | Trek gear | torso, legs, feet | M3a | **P0** | 800–1 000 | Down jacket, fleece, trekking trousers, boots; generic, **no outdoor brands** | needs-art |
| `ghm_chr_acc_backpack_{day,trek}` | Backpacks | back_attach | M1 | P0 | 400/600 | Trek pack with a sleeping-mat roll | needs-art |
| `ghm_chr_acc_trek_poles` | Trekking poles | prop_L/R | M3a | P1 | 100 | Pole-plant locomotion (§9.4) | needs-art |
| `ghm_chr_acc_helmet_{open,full,bicycle}` | Helmets | head_attach | M1 | **P0** | 400 | Mandatory on two-wheelers; 8 colour presets | needs-art |
| `ghm_chr_acc_sunhat_{a}`, `_beanie_{a}`, `_sunglasses` | Hats and sunglasses | head_attach | M1 | P1 | 150–300 | Beanie for altitude, with a knitted pattern | needs-art |
| `ghm_chr_acc_camera` | Camera | prop_R | M1 | **P0** | 200 | Photo mode and the photograph emote | needs-art |
| `ghm_chr_acc_khata` | Khata around the neck | accessory | M3a | P2 | 150 | Received at the pass celebration and as a welcome (review) | needs-art |
| `ghm_chr_acc_marigold_mala` | Marigold garland (worn) | accessory | M2 | P2 | 200 | Tihar (review) | needs-art |
| `ghm_chr_acc_tika` | Tika on the forehead | face decal | M2 | P2 | decal | Red Dashain tika with jamara (review) | needs-art |

### 9.3 NPC archetypes

| id | Archetype | M | P | Tris LOD0/LOD1/LOD2 | Outfit and props | Animation needs (beyond the core set) | Reference and cultural notes | Status |
|---|---|---|---|---|---|---|---|---|
| `ghm_chr_npc_urban_{m,f}_{a,b,c}` | Urban pedestrians | M1 | **P0** | 3 500/1 500/500 | Casual, kurta, sari, jackets | Phone glance, chat pair, carry bag | Everyday mixed dress | needs-art |
| `ghm_chr_npc_vendor_{a,b}` | Street vendors | M1 | **P0** | 3 500/1 500/500 | Apron, cart, scale | Call out (non-verbal), weigh, hand over | Generic goods | needs-art |
| `ghm_chr_npc_monk_{tibetan,theravada}` | Monks and nuns | M1 | **P0** | 3 500/1 500/500 | Maroon and saffron robes (Tibetan); saffron/orange (Theravada, Lumbini) | Walk with prayer beads, spin wheels, chant sit | Respectful depiction; no comedic gags (review) | needs-art |
| `ghm_chr_npc_porter_{a,b}` | Porters | M1 | P1 | 3 500/1 500/500 | Doko + namlo (head strap), loads (crates, gas cylinders, sacks); trek porters with duffels | Heavy-load walk, rest on tokma or chautari ledge | Dignified depiction, not comedic (review) | needs-art |
| `ghm_chr_npc_sadhu_a` | Sadhu | M1 | P1 | 3 500/1 500/500 | Saffron cloth, beads, painted forehead marks (review) | Sit cross-legged, bless gesture | Respectful; no pay-for-photo gag (review) | needs-art |
| `ghm_chr_npc_kid_{a,b}` | Kids | M1 | P1 | 3 000/1 200/450 | Casual; school uniform variant | Play chase, wave, fly kite (Dashain) | School uniforms are generic blue/grey with a tie (verify) | needs-art |
| `ghm_chr_npc_kid_kite` | Kid flying a kite | M2 | P1 | 3 000/1 200/450 | Kite + spool (lattai) | Kite tug loop, run, cheer | Dashain rooftops (review) | needs-art |
| `ghm_chr_npc_traffic_police_a` | Traffic police | M1 | P1 | 3 500/1 500/500 | Generic uniform, white gloves, whistle | Hand signals loop (stop, go, turn) | **No real insignia**; uniform colours (verify) | needs-art |
| `ghm_chr_npc_tourist_{a,b,c}` | Tourists and trekkers | M1 | **P0** | 3 500/1 500/500 | Daypacks, sunhats, cameras, maps | Photograph, look around, consult map | Diverse international visitors | needs-art |
| `ghm_chr_npc_farmer_{m,f}` | Farmers | M1 | P1 | 3 500/1 500/500 | Hoe, sickle (tool, not weapon), basket | Hoe loop, plant rice, harvest, carry sheaf | Seasonal field work | needs-art |
| `ghm_chr_npc_sherpa_guide_a` | Sherpa guide | M3b | **P0** | 3 500/1 500/500 | Down jacket, traditional bakhu variant (review) | Point to peaks, lead walk, welcome with khata | Sherpa culture (review) | needs-art |
| `ghm_chr_npc_gurung_villager_{m,f}` | Gurung / Magar villagers | M3a | P1 | 3 500/1 500/500 | Regional dress variants (review) | Weave, carry, greet | Ghandruk | needs-art |
| `ghm_chr_npc_tharu_villager_{m,f}` | Tharu villagers | M4 | **P0** | 3 500/1 500/500 | Tharu dress and jewellery (review) | Fish with a net, carry, stick dance (P2, review) | Chitwan, Bardiya | needs-art |
| `ghm_chr_npc_madhesi_{m,f}` | Terai town folk | M4 | P1 | 3 500/1 500/500 | Dhoti, kurta, sari variants (review) | Market, rickshaw puller | Janakpur | needs-art |
| `ghm_chr_npc_woman_{sari,kurta,gunyu}` | Women in sari, kurta and gunyu cholo | M1 | **P0** | 3 500/1 500/500 | Pote (glass-bead necklace) and red sari variants for festivals (review) | Carry water vessel, puja offering | — | needs-art |
| `ghm_chr_npc_tea_picker_f` | Tea picker | M5 | P1 | 3 500/1 500/500 | Back basket, head cloth | Pick-and-toss loop | Ilam | needs-art |
| `ghm_chr_npc_yak_herder` | Yak herder | M3b | P1 | 3 500/1 500/500 | Warm layers, whistle | Herd walk, whistle | Khumbu, Mustang | needs-art |
| `ghm_chr_npc_pilot`, `_ranger`, `_boatman`, `_rafting_guide` | Service roles | M2 | P1 | 3 500/1 500/500 | Generic uniforms, no company marks | Role idles | Pilots M3b; rangers M4; boatmen M2 | needs-art |
| `ghm_chr_crowd_body` | Shared crowd LOD2 body | M1 | **P0** | — / — / 500 (≤ 512 verts) | Palette tint per instance | All VAT clips (§9.5) | One topology for every NPC LOD2 | procedural |

### 9.4 Animation sets (30 fps; **L** = loop)

| id | Clip set | M | P | Clips (frames) | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_anim_hum_locomotion` | Core locomotion | M1 | **P0** | idle 90L, walk 32L, jog 24L, run 20L, sprint 16L, start 12, stop 14, turn90 L/R 16, turn180 20, walk_uphill 32L, walk_downhill 32L, stairs_up 32L, stairs_down 32L | In place; bouncy squash on footfalls | needs-art |
| `ghm_anim_hum_idle_variety` | Idle variety | M1 | P0 | look_around 120, stretch 90, check_phone 120, shift_weight 90, yawn 90 | Random picks after 6 s | needs-art |
| `ghm_anim_hum_jump` | Jump and land | M1 | P0 | jump_start 8, air 12L, land 10, land_squash 12, stumble_recover 24 | Squash on land | needs-art |
| `ghm_anim_hum_trek` | Trekking | M3a | **P0** | trek_walk 36L, pole_walk 36L, tired_walk 40L, catch_breath 90L (hands on knees), altitude_breath additive 60L | Stamina-driven (Heart/Energy) | needs-art |
| `ghm_anim_hum_sit` | Sitting | M1 | P1 | sit_bench 60L, sit_ground_crosslegged 90L, sit_chautari 90L, sit_to_stand 20 | — | needs-art |
| `ghm_anim_hum_ride_twowheel` | Two-wheeler poses | M1 | **P0** | scooter_idle L, moto_idle L, lean L/R (additive), brake, honk_bounce 12, wheelie_squash 16, pillion_idle L, bicycle_pedal 24L | Hands and feet via IK sockets | needs-art |
| `ghm_anim_hum_ride_vehicle` | Seated poses | M1 | **P0** | car_driver L, car_passenger L, bus_seated L, bus_standing_sway L, tempo_passenger L, rickshaw_passenger L, rickshaw_puller 24L, tractor_driver L | — | needs-art |
| `ghm_anim_hum_ride_animal` | Animal riding | M3a | **P0** | horse_walk 32L, horse_trot 20L, horse_canter 18L, yak_walk 40L (synced to §10 clips) | Bounce from the animal's root | needs-art |
| `ghm_anim_hum_water` | Boats | M2 | **P0** | pedal_boat 24L, row_doonga 40L, canoe_pole 48L, raft_paddle 30L, kayak_paddle 30L, splash_recover 30 | No swimming: falling in water gives a comic bounce back to shore | needs-art |
| `ghm_anim_hum_air` | Air and cable | M2 | **P0** | para_launch_run 20L, para_fly L, para_steer L/R (additive), para_flare_land 30, plane_passenger L, heli_passenger L, balloon_basket L, cablecar_seated L, zipline_hang 24L, bungee_jump 30, bungee_freefall L, bungee_bounce 40, bungee_dangle 60L | Bungee arrives in M3b; thrill faces, never fear | needs-art |
| `ghm_anim_hum_emotes` | Emotes | M1 | **P0** | **namaste 40**, wave 30, cheer 36, clap 30L, laugh 40, bow 36, thumbs_up 24, point 24, dance_folk 96L (review style), selfie 48, photograph 40 | Namaste is the default greeting (palms together, slight bow) | needs-art |
| `ghm_anim_hum_interact_sacred` | Sacred interactions | M1 | **P0** | spin_prayer_wheel_walk 32L (right hand, clockwise), spin_hand_wheel 30L, ring_bell 30, light_butter_lamp 60, offer_flowers 60, tie_prayer_flag 90, receive_tika 60 (M2) | Correct handedness and direction (review all) | needs-art |
| `ghm_anim_hum_interact_world` | World interactions | M1 | P1 | fetch_water_tap 60, drink_tea 60, eat_momo 60, buy_handover 40, feed_pigeons 60 (no player feeding of monkeys), fly_kite_tug 40L, swing_linge_ping 48L, carry_doko_walk 40L, carry_doko_rest 90L | — | needs-art |
| `ghm_anim_hum_npc_work` | NPC work loops | M1 | P1 | hoe 40L, plant_rice 48L, harvest 48L, weave 60L, sweep 40L, vendor_weigh 48L, traffic_signals 120L, pick_tea 40L, herd_whistle 60 | NPCs only | needs-art |

### 9.5 Crowd strategy

| Distance (High; Low and Mid scale by the ARCHITECTURE §10 rings) | Representation | Notes |
|---|---|---|
| ≤ 15 m | GPU-skinned LOD0, 4 weights, outline (Mid/High) | Up to 6 on High, 4 on Mid, 2 on Low |
| 15–40 m | GPU-skinned LOD1, 2 weights | Up to 20 on High |
| 40–90 m | **VAT** LOD2 (`ghm_chr_crowd_body`), instanced, tinted per instance | One shared VAT holds ~12 clips (idle, walk, carry, sit, chat, cheer, pray, pick, vendor, kite, run, namaste) at 15 fps |
| > 90 m | Culled (dense squares: VAT out to 120 m on High) | Festival crowds raise the VAT counts, never the skinned counts |

VAT texture: ≤ 512 vertices × ~720 frames total, RGBAHalf positions (~2.9 MB) + RGBA8 normals (~1.5 MB), shared by every NPC archetype (`ghm_tex_vat_crowd`). Herds and flocks use the same technique (§10).

---
## 10. Wildlife and domestic animals

Spawn tables are keyed by `Biome` and region (ARCHITECTURE §7.7: rhino only in Chitwan and Bardiya). Behaviour is a state machine: **W**ander, **G**raze, **F**lee, **Fl**ock, **P**erch, **S**oar, plus species extras. **Interaction is observation only.** The player can watch, photograph and give way, never feed (except pigeons, where people really do), chase, ride (except the domestic riding animals in §8) or harm. Animals never attack. Predators keep their distance and slip away, and no predation is shown. Vehicles stop for animals with a damage-free bump (ARCHITECTURE §7.6–7.7). **Rarity** drives the photo-collection score: common, uncommon, rare, legendary. Rigs: `quadL` and `quadM` ≤ 25 bones (tails 3, trunk 5, neck 3), `prim` ≤ 25, `croc` ≤ 20, `bird` ≤ 12, `fish` ≤ 8. Herds beyond 40 m and flocks always use VAT (`ghm_tex_vat_<species>`).

### 10.1 Wildlife

| id | Species (Nepali) | Biome / region | M | P | Tris LOD0/LOD1/LOD2 | Bones | Behaviours | Animations (frames; L = loop) | Rarity | Photo and cultural notes | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `ghm_ani_rhino` | Greater one-horned rhinoceros (gainda) | TERAI_GRASSLAND, WETLAND, riverbanks; **Chitwan, Bardiya only** | M4 | **P0** | 4 000/1 500/500 | 22 | W, G, wallow, turn-away (never charges) | idle 90L, walk 40L, trot 24L, graze 60L, wallow_enter 40, wallow 120L, ear_flick additive, look 40 | uncommon | Armour-plated skin folds in chunky shapes; mud-wallow sheen. Best shot: side profile in the river | needs-art |
| `ghm_ani_tiger` | Bengal tiger (bagh) | TERAI_SAL_FOREST, TERAI_GRASSLAND; Chitwan, Bardiya, Banke, Parsa, Shuklaphanta | M4 | P1 | 4 000/1 500/500 | 24 | W, rest, drink, slip away at the player's approach | walk 32L, trot 22L, sit 90L, lie 120L, drink 60L, yawn 50, look 40 | **legendary** | Spawns at 60 m or more, dusk and dawn. Never stalks the player; a yawn instead of a roar (age rating) | needs-art |
| `ghm_ani_elephant` | Asian elephant (hatti) | TERAI_SAL_FOREST, rivers; Bardiya, Chitwan, Koshi | M4 | **P0** | 4 000/1 500/500 | 25 (trunk 5) | W, G, bathe, spray | walk 48L, idle_trunk_sway 90L, eat_branch 60L, bathe_spray 80L, ear_flap additive, trumpet 50 | uncommon | **No elephant riding or elephant-back safari** (welfare). Domestic breeding-centre variant is shown only as context (review) | needs-art |
| `ghm_ani_red_panda` | Red panda (habre) | SUBALPINE_FOREST with bamboo; e.g. Langtang, the east (verify range) | M5 | P1 | 1 200/500/150 | 16 | P (on branch), W, eat | walk 24L, climb 32L, sit_eat_bamboo 60L, sleep_branch 120L, tail_flick additive | rare | Fog and moss setting; curled-up sleep shot | needs-art |
| `ghm_ani_snow_leopard` | Snow leopard (hiun chituwa) | SCREE_ROCK, ALPINE_SCRUB, TRANS_HIMALAYAN_STEPPE; Mustang, Dolpo, Manang, Khumbu | M3b | P2 | 3 000/1 200/400 | 24 | W along ridges, rest, vanish | walk 32L, sit 90L, lie 120L, look 40, leap 30 | **legendary** | Distance-only (≥ 100 m), dawn and dusk; a camouflage palette | needs-art |
| `ghm_ani_tahr` | Himalayan tahr (jharal) | SUBALPINE_FOREST edges, cliffs; Khumbu, Annapurna | M3b | **P0** | 2 500/1 000/300 | 20 | G, cliff-walk, F | idle 60L, walk 32L, graze 60L, cliff_hop 24, flee_run 18L | common (Khumbu) | Shaggy ruff on males; groups near Namche (verify) | needs-art |
| `ghm_ani_bharal` | Blue sheep / bharal (naur) | TRANS_HIMALAYAN_STEPPE, ALPINE_MEADOW; Mustang, Dolpo, Manang | M3a | P1 | 2 500/1 000/300 | 20 | G, herd, F (VAT herd) | idle 60L, walk 32L, graze 60L, run 18L, cliff_leap 24 | common | Herds of 5–30 on slopes | needs-art |
| `ghm_ani_musk_deer` | Himalayan musk deer (kasturi mriga) | SUBALPINE_FOREST (birch, rhododendron) | M3b | P1 | 2 000/800/250 | 18 | W, browse, bound away | idle 60L, walk 32L, browse 60L, bound 20L | rare | Small, with tusks on males; shy | needs-art |
| `ghm_ani_chital` | Spotted deer (chital) | TERAI_SAL_FOREST, TERAI_GRASSLAND | M4 | **P0** | 2 500/1 000/300 | 20 | G, herd, alarm, F (VAT herd) | idle 60L, walk 32L, graze 60L, alarm_stamp 30, flee_run 18L | common | Herds at the forest edge | needs-art |
| `ghm_ani_macaque` | Rhesus macaque (bandar) | Temples (Swayambhunath, Pashupatinath), HILL_FOREST, towns | M1 | **P0** | 2 000/800/250 | 22 (tail 3) | W, sit, groom, climb, hop walls, P on roofs | idle_sit 90L, walk 24L, run 16L, climb 24L, groom_pair 120L, eat 60L, jump 20, baby_carry variant | common | **Swayambhu's monkeys.** Cheeky but never aggressive; they don't steal from the player (review); VAT troops on roofs | needs-art |
| `ghm_ani_langur` | Grey langur (dedhre bandar (verify)) | HILL_FOREST, TERAI, up to about 4 000 m | M4 | P1 | 2 000/800/250 | 22 (tail 3) | P, leap between trees, sit | sit 90L, walk 24L, leap 24, climb 24L | uncommon | Black face and long tail; graceful leaps | needs-art |
| `ghm_ani_gharial` | Gharial (ghadiyal gohi) | Rivers: Narayani, Rapti (Chitwan), Karnali (Bardiya) | M4 | **P0** | 2 000/800/250 | 18 | Bask on sandbanks, slide into water | bask 120L, crawl 40L, slide_in 40, swim 40L | uncommon | Long narrow snout; critically endangered (a conservation fun fact) | needs-art |
| `ghm_ani_mugger` | Mugger crocodile (magar gohi) | WETLAND, oxbow lakes, rivers (Terai) | M4 | P1 | 2 000/800/250 | 18 | Bask with mouth open, slide in, float | bask_gape 120L, crawl 40L, float 90L | uncommon | Never lunges at anything | needs-art |
| `ghm_ani_river_dolphin` | Gangetic river dolphin (souns) | Karnali (Bardiya), Koshi (verify) | M4 | P1 | 1 500/500/— | 8 | Surface arc, dive | surface_arc 40, dive 30 | rare | Seen only as surfacing arcs plus a blow-puff VFX | needs-art |
| `ghm_ani_wild_buffalo` | Wild water buffalo (arna) | WETLAND, Koshi Tappu | M4 | P2 | 4 000/1 500/500 | 22 | G, wallow, herd | reuse buffalo clips + alert 40 | rare | Koshi Tappu's last population; wide swept-back horns | needs-art |
| `ghm_ani_bats` | Bats (flock) | Bat Cave, dusk over towns | M2 | P2 | 150 (VAT) | baked | Fl (roost, swirl) | roost_hang L, fly_swirl L | common | — | needs-art |
| `ghm_ani_fish_school` | Fish schools | Lakes, rivers | M2 | P2 | 60 (VAT) | baked | school swim, flee ripple | swim L | common | Visible from boats in clear water | needs-art |

### 10.2 Domestic animals

| id | Species (Nepali) | Where | M | P | Tris LOD0/LOD1/LOD2 | Bones | Behaviours | Animations (frames; L = loop) | Notes | Status |
|---|---|---|---|---|---|---|---|---|---|---|
| `ghm_ani_cow` | Cow (gai) and bull | Everywhere, **lounging on roads** | M1 | **P0** | 4 000/1 500/500 | 22 | W, G, lie on road, chew; vehicles always stop and steer round | idle 90L, walk 40L, lie_down 40, lie_chew 120L, stand_up 40, tail_swish additive, moo 40 | **Sacred and the national animal: never harmed** (no damage, no collisions, a soft avoidance radius). Tihar variant with marigold garland and tika (review) | needs-art |
| `ghm_ani_dog` | Dog (kukur) | Towns, villages, trails | M1 | **P0** | 2 500/1 000/300 | 22 | Sleep in the sun, trot, follow the player briefly, bark softly, wag | sleep 120L, trot 20L, run 16L, sit 90L, wag additive, bark 20, scratch 40 | **Kukur Tihar** variant with garland and red tika (review). Friendly street dogs; mountain dogs at altitude | needs-art |
| `ghm_ani_buffalo` | Water buffalo (bhainsi / rango) | Terai, hills, valley | M1 | P1 | 4 000/1 500/500 | 22 | G, wallow, plough (with farmer) | idle 90L, walk 44L, graze 60L, wallow 120L, plough_pull 48L | Egrets often ride on their backs (§10.3) | needs-art |
| `ghm_ani_goat` | Goat (bakhra) | Hills, villages, trails | M1 | P1 | 1 500/600/200 | 18 | Herd, G, climb walls (VAT herds) | idle 60L, walk 24L, graze 60L, hop 20, bleat 30 | Dashain context: **no sacrifice depicted** (review) | needs-art |
| `ghm_ani_chicken` | Chicken and rooster (kukhura) | Villages | M1 | P1 | 800/300/80 | 12 | Peck, scatter with squash and flap | peck 30L, walk 20L, scatter_flap 20, crow 40 (rooster) | Comic scatter on approach | needs-art |
| `ghm_ani_yak` | Yak (chauri / nak) | Khumbu, Mustang, Langtang | M3b | **P0** | 4 000/1 500/500 | 22 | G, pack-train walk, rest | idle 90L, walk 48L, graze 60L, lie 120L, bell additive | Rideable and pack variants in §8. Pack trains have right of way | needs-art |
| `ghm_ani_horse_mule` | Horse, pony and mule | Mustang, Annapurna trails | M3a | **P0** | 4 000/1 500/500 | 22 | Pack-train walk, G | idle 90L, walk 36L, trot 20L, canter 18L, graze 60L, head_toss 30 | Coloured head plumes and bells on pack mules (verify) | needs-art |
| `ghm_ani_sheep` | Sheep | High pastures | M3a | P2 | 1 500/600/200 | 18 | Herd, G | reuse goat clips | — | needs-art |

### 10.3 Birds

| id | Species (Nepali) | Biome / region | M | P | Tris LOD0/LOD1/LOD2 | Bones | Behaviours | Animations | Rarity | Notes | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `ghm_ani_monal` | Himalayan monal (danphe) | SUBALPINE_FOREST, ALPINE_MEADOW (about 2 500–5 000 m) | M3b | **P0** | 800/300/80 | 12 | Ground forage, flush-glide downhill | walk 24L, peck 30L, flush_takeoff 20, glide L | uncommon | **National bird.** Iridescent male (gradient-atlas rainbow sheen); duller female | needs-art |
| `ghm_ani_spiny_babbler` | Spiny babbler (kande bhyakur) | HILL_SCRUB (central and western hills) | M2 | P2 | 600/200/60 | 10 | Skulk in scrub, sing | hop 20L, sing 40L, dart 16 | rare | **Nepal's only endemic bird.** Heard more than seen: song cue (§13) | needs-art |
| `ghm_ani_hornbill` | Great hornbill | TERAI_SAL_FOREST (Chitwan) | M4 | P1 | 800/300/80 | 12 | P, fly between tall trees | perch 90L, fly 24L, takeoff 20 | rare | Big casque; loud wing whoosh | needs-art |
| `ghm_ani_sarus_crane` | Sarus crane | Terai wetlands and fields (Lumbini area) | M4 | P1 | 800/300/80 | 12 | Pairs, wade, dance | walk 32L, forage 40L, pair_dance 96, call 40 | uncommon | The tallest flying bird; a pair-dance photo moment | needs-art |
| `ghm_ani_kingfisher` | Kingfisher (pied, white-throated) | Rivers, Phewa | M2 | P1 | 500/200/60 | 10 | P, hover (pied), dive | perch 60L, hover 16L, dive 20, flyoff 16 | common | Bright blue and orange gradients | needs-art |
| `ghm_ani_vulture` | Himalayan griffon | Hills, cliffs; Pokhara skies | M2 | P1 | 800/300/80 | 12 | **S** in thermals, perch on cliffs | soar L, glide_bank L/R, perch 90L, land 30 | common | Circling birds mark thermals for paragliding (§11) | needs-art |
| `ghm_ani_eagle` | Steppe eagle | Migrating along the Himalaya in autumn | M2 | **P0** | 800/300/80 | 12 | **S**, glide | soar L, glide L, flap 20L | uncommon (autumn) | Sarangkot paragliding companions (ROADMAP M2) | needs-art |
| `ghm_ani_pigeon` | Rock pigeon (parewa) | Durbar Squares, Boudha, Swayambhu, Tal Barahi | M1 | **P0** | 300 (VAT) | baked | **Fl**: peck, burst take-off at the player, circle, re-land | peck L, walk L, takeoff, fly_circle L, land | common | Flocks of 30–200 with VAT; grain-feeding moment | needs-art |
| `ghm_ani_crow` | House and jungle crows (kag) | Towns | M1 | P1 | 300 (VAT) / 600 single | 10 | Hop, perch on wires, Fl | hop L, perch L, caw, fly L | common | **Kag Tihar** offering moment (review) | needs-art |
| `ghm_ani_egret` | Cattle egret | Fields, on buffalo | M2 | P2 | 400 (VAT) | 10 | Follow buffalo, Fl | walk L, perch_on_buffalo L, fly L | common | — | needs-art |
| `ghm_ani_peafowl` | Indian peafowl (mayur) | Terai forest edges | M4 | P2 | 800/300/80 | 12 | W, display | walk L, display 90, call 40 | uncommon | — | needs-art |
| `ghm_ani_chough` | Yellow-billed chough | High Khumbu, EBC, Gokyo | M3b | P2 | 300 (VAT) | baked | Fl, tumble in wind | fly L, hop L | common | Playful aerobatics at altitude | needs-art |
| `ghm_ani_lammergeier` | Bearded vulture | High Himalaya | M3b | P2 | 800/300/80 | 12 | S along ridges | soar L, glide L | rare | — | needs-art |
| `ghm_ani_butterflies` | Butterflies | Hills and Terai, spring and monsoon | M2 | P2 | VFX quads | — | Flutter swarm | — | common | VFX (§11) | needs-art |

---

## 11. Sky, weather and VFX

### 11.1 Sky and skyline

| id | Asset | M | P | Spec | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_sky_gradient_atlas` | Sky gradients per time of day | M1 | **P0** | 256×64 RGBA32: 8 time keys (pre-dawn, sunrise alpenglow, morning, midday, golden hour, sunset, dusk, night) × region presets | Region presets: valley haze, Terai haze, hill clear, high-altitude deep blue, trans-Himalayan crisp | needs-art |
| `ghm_sky_sun_moon` | Sun and moon discs + glow | M1 | P0 | 256² ASTC 4×4 | Cartoon sun with a soft halo; moon phases from the atlas | needs-art |
| `ghm_sky_stars` | Star field | M1 | P1 | 512² ASTC 6×6 + twinkle | Milky Way band visible at altitude | needs-art |
| `ghm_sky_skyline_ring` | Himalayan skyline impostor ring | M1 | **P0** | Per region, generated from the coarse DEM: a 360° ring, 3 depth bands, ≤ 6 000 tris, vertex colours only | **Procedural** (ARCHITECTURE §8). Art supplies the snowline ramp, rock ramp, atmospheric fade and the warm rim at sunrise and sunset (the hero moment) | procedural |
| `ghm_sky_cloud_{puff_a..d}` | Puffy cartoon clouds | M1 | P1 | 300 tris each, instanced | Day drift; monsoon stacks | needs-art |
| `ghm_sky_cloud_sea` | Cloud sea / valley fog layer | M1 | **P0** | Soft layer mesh + height fog | Seen from Nagarkot, Sarangkot and Chandragiri at dawn | needs-art |
| `ghm_sky_summit_plume` | Summit cloud banner | M3b | P1 | Scrolling card on the lee side of peaks | Everest, Lhotse, Ama Dablam | needs-art |
| `ghm_sky_monsoon_stack` | Monsoon cumulonimbus | M2 | P2 | Large impostor | Distant lightning flashes | needs-art |
| `ghm_sky_rainbow` | Rainbow | M2 | P2 | Arc mesh + gradient | After rain (Pokhara) | needs-art |

### 11.2 Weather

| id | Asset | M | P | Spec | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_vfx_valley_fog` | Valley fog | M1 | **P0** | Height fog params + soft fog cards (Mid/High) | Winter mornings in the valley; Terai winter fog | needs-art |
| `ghm_vfx_rain_{light,heavy}` | Monsoon rain | M1 | P1 | Camera-attached streak cylinder; 400 / 1 000 / 1 500 particles (Low / Mid / High) | Drives wetness (road physics), ripples in puddles, splashes on roads | needs-art |
| `ghm_vfx_rain_splash` | Rain splashes and ripples | M1 | P1 | Flipbook decals | Puddles, lake surfaces | needs-art |
| `ghm_vfx_snowfall` | Snow | M3a | **P0** | Camera-attached flakes; 300 / 800 / 1 200 | Above the snowline; light flurries at passes | needs-art |
| `ghm_vfx_wind_gust` | Wind gusts | M3a | P1 | Streak sprites + global wind parameter | Drives flags, trees and snow spindrift | needs-art |
| `ghm_vfx_dust_haze` | Dust and haze | M1 | P1 | Fog colour and density preset + drifting motes | Dry season (Terai, valley, Mustang afternoon wind) | needs-art |
| `ghm_vfx_dust_devil` | Dust devil | M3a | P2 | Swirl particle | Mustang, Terai fields | needs-art |
| `ghm_vfx_lightning` | Distant lightning | M2 | P2 | Flash + bolt sprite | Monsoon evenings; not near the player | needs-art |
| `ghm_vfx_kiln_smoke` | Brick-kiln chimney smoke | M1 | P1 | Soft plume, 12 / 24 / 40 | Dry season only | needs-art |

### 11.3 Water

| id | Asset | M | P | Spec | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_wat_lake` | Lake water | M1 | **P0** | `ghm_mat_toon_water`: depth-ramp colour, shoreline foam band, sun sparkle, sky-colour reflection; planar reflection on High only (Phewa) (verify cost) | Variants: clear (`w.water.lake`), glacial turquoise (`w.water.glacial`: Gokyo, Tilicho, Phoksundo), monsoon brown (`w.water.monsoon`) | needs-art |
| `ghm_wat_river` | River flow | M1 | **P0** | Flow along the OSM river direction (UV from polyline), white-water bands where the slope is steep | Seti milky variant; monsoon high and brown | needs-art |
| `ghm_wat_rapids` | Rapids and foam | M2 | P1 | Foam flipbook + spray | Trishuli, Bhote Koshi | needs-art |
| `ghm_wat_waterfall_{s,l}` | Waterfalls | M2 | P1 | Scrolling mesh + mist particles + spray | `LineKind.WATERFALL`; Davis Falls, Hyatung | needs-art |
| `ghm_wat_shimmer` | Water shimmer and caustics | M2 | P2 | Shader term on shallow beds | — | needs-art |
| `ghm_wat_wake` | Boat wakes and ripples | M2 | P0 | Trail decal + particles | Doongas, paddle boats, canoes | needs-art |
| `ghm_wat_ice` | Frozen lake surface | M5 | P2 | Ice ramp + cracks | Gosaikunda in winter | needs-art |

### 11.4 Gameplay and transition VFX

| id | Asset | M | P | Spec | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_vfx_discovery_sparkle` | Discovery sparkle | M1 | **P0** | Star burst + rising ribbon + UI hand-off | Plays when a place is discovered | needs-art |
| `ghm_vfx_thermal_column` | Thermal columns | M2 | **P0** | Soft rising spirals, drifting seeds and leaves, plus circling birds | Paragliding: readable at a glance, strength shown by spiral speed | needs-art |
| `ghm_vfx_pass_celebration` | Pass celebration | M3a | **P0** | Prayer-flag burst + confetti in the five flag colours | Thorong La, Kala Patthar | needs-art |
| `ghm_vfx_stamp_burst` | Passport stamp burst | M2 | P0 | UI-space ink splat + stars | §12.4 | needs-art |
| `ghm_vfx_speed_lines` | Cartoon speed lines | M1 | P1 | Screen-edge streaks | Vehicles at speed, freefall; respects reduced motion (M6) | needs-art |
| `ghm_vfx_honk_puff` | Honk puff | M1 | P1 | Comic puff at `sock_horn_fx` | Paired with the honk-bounce squash | needs-art |
| `ghm_vfx_exhaust_puff` | Exhaust puffs | M1 | P1 | Small grey cartoon puffs | Electric vehicles have none | needs-art |
| `ghm_vfx_stuck_bounce` | Stuck-recovery hop | M1 | P0 | Dust and mud puff ring | ARCHITECTURE §7.6 | needs-art |
| `ghm_vfx_lamp_flicker` | Butter-lamp and diyo flames | M1 | P0 | Flame sprite + glow card; no realtime lights on Low | — | needs-art |
| `ghm_vfx_incense_smoke` | Incense and juniper smoke | M1 | P1 | Soft curling plume | Gompas, passes, shrines | needs-art |
| `ghm_vfx_steam` | Steam | M1 | P2 | Tea stalls, momo steamers, hot springs | — | needs-art |
| `ghm_vfx_burner_flame` | Balloon burner | M2 | P2 | Flame burst | — | needs-art |
| `ghm_vfx_dolphin_blow` | Dolphin surfacing puff | M4 | P2 | Tiny spray | — | needs-art |
| `ghm_vfx_fireflies` | Fireflies | M4 | P2 | Glow motes, Terai nights | — | needs-art |
| `ghm_vfx_fireworks_soft` | Soft festival fireworks | M5 | P2 | Bursts, Tihar | Low volume, no loud scare | needs-art |
| `ghm_vfx_photo_flash` | Photo flash and shutter | M1 | P1 | Screen flash + vignette | Photo mode | needs-art |
| `ghm_vfx_ft_bus` | Fast travel: bus bouncing along a map road | M2 | **P0** | 3–5 s vignette on the paper map: a bouncing bus, dust puffs, hairpin wiggle | ROADMAP M2 cartoon transitions | needs-art |
| `ghm_vfx_ft_plane` | Fast travel: plane over the mountains | M3b | P1 | 3–5 s vignette: a STOL plane over cartoon peaks with a cloud parting | — | needs-art |
| `ghm_vfx_ft_scooter` | Fast travel: scooter whoosh | M1 | P1 | 2–3 s vignette | M1 fast travel to discovered places | needs-art |
| `ghm_vfx_ft_jeep`, `_heli` | Fast travel: jeep and helicopter | M3a | P2 | 3–5 s vignettes | — | needs-art |

---
## 12. UI (UI Toolkit)

> **Orientation (ADR-017): the game is fully playable in portrait and in landscape**, and it rotates live. Every screen, HUD and panel needs **both compositions**. Reference resolutions are 1170×2532 (portrait) and 2532×1170 (landscape), checked at 1080×2400, an iPad at 1640×2360 and a foldable at 2208×1768. In portrait, primary controls sit in the bottom third (the thumb zone) and counters along the top. All full-bleed art (loading screens, splash, backgrounds) is composed so that a centre-safe crop works at both 9:19.5 and 19.5:9, or it ships as two crops.

All UI is UI Toolkit with the Advanced Text Generator (ARCHITECTURE §7.9, ADR-007). Shapes, borders, radii and colours are **USS first**, with the colour tokens from §1.9. Sprites are used only for what USS cannot draw: glossy highlights, ribbon tails, paper textures and icons. Gradient fills need a sprite or an SVG VectorImage (verify what UI Toolkit in 6.3 can do natively). Reference layout is landscape 1920×1080 with safe areas (verify against the reference image). Touch targets are ≥ 44 pt / 48 dp. UI textures are ASTC 4×4 with no mipmaps, in sprite atlases. Motion: bouncy tweens (overshoot 1.1, 180–250 ms) and squash on press, with a **reduced-motion** setting (M6). Colour is never the only signal: every pill colour also has an icon or label (colour-blind safety, M6).

### 12.1 Components

| id | Component | M | P | Variants and states | Spec | Status |
|---|---|---|---|---|---|---|
| `ghm_ui_panel_cream` | Cream panel | M1 | **P0** | S, M, L; with or without inner scroll | `ui.cream` fill, 4 px `ui.cream.edge` border, 6 px `ui.cream.shadow` inner bottom shadow, 28 px radius, 9-slice paper grain overlay (optional) | needs-art |
| `ghm_ui_ribbon_header` | Ribbon header | M1 | **P0** | red (default), teal, green; short and long | Ribbon body (9-slice) + folded tails sprite; Baloo 2 ExtraBold title with an ink outline | needs-art |
| `ghm_ui_btn_pill_{yellow,cyan,green,white,red}` | Glossy pill buttons | M1 | **P0** | normal, pressed, disabled, focused (gamepad and keyboard ring) | Fill + darker 6 px bottom edge (§1.9 edge tokens) + glossy top-40 % highlight sprite. **Pressed:** edge shrinks to 2 px, content moves down 4 px, squash tween 0.95 | needs-art |
| `ghm_ui_btn_round_{s,m,l}` | Round icon buttons | M1 | **P0** | 96, 128, 160 px; all five colours; badge-dot slot | Same edge and gloss rules as the pills | needs-art |
| `ghm_ui_counter_pill_{heart,star,coin,energy}` | Counter pills with "+" | M1 | **P0** | value, animating count-up, "+" pressed | Icon left (overlapping the pill edge), number in Baloo 2, round green "+" on the right | needs-art |
| `ghm_ui_toggle` | Toggle | M1 | P0 | off, on, disabled | Pill track with a knob that squashes on switch | needs-art |
| `ghm_ui_slider` | Slider | M1 | P0 | normal, dragging | Rounded track, glossy knob, value bubble | needs-art |
| `ghm_ui_tabs` | Tabs | M1 | P0 | 2–5 tabs; selected, unselected | Folder tabs on top of a cream panel | needs-art |
| `ghm_ui_modal` | Modal dialog | M1 | **P0** | info, confirm, destructive, download size | Ribbon + panel + 1–2 pills; dimmed backdrop | needs-art |
| `ghm_ui_toast` | Toast | M1 | P0 | info, success, warning | Slides in from the top; icon + one line | needs-art |
| `ghm_ui_progress_{xp,download,stamina}` | Progress bars | M1 | P0 | fill, empty, pulsing | Rounded, striped fill animation | needs-art |
| `ghm_ui_checkbox`, `ghm_ui_dropdown` | Checkbox, dropdown | M1 | P1 | standard states | — | needs-art |
| `ghm_ui_search_field` | Search field | M1 | **P0** | empty, typing (EN/NE IME), results | Magnifier icon, clear "×"; Devanagari input must render shaped while typing (spike, ROADMAP 1.1) | needs-art |
| `ghm_ui_result_row` | Search result row | M1 | **P0** | place, POI, landmark; discovered or not | PoiKind icon, bilingual name, distance, ETA per mode | needs-art |
| `ghm_ui_tooltip`, `ghm_ui_badge_dot` | Tooltip, notification dot | M1 | P1 | — | — | needs-art |
| `ghm_ui_spinner_prayerwheel` | Loading spinner | M1 | P1 | loop | A small spinning prayer wheel (clockwise) or wheel (review the prayer-wheel use) | needs-art |
| `ghm_ui_hud_joystick` | Virtual joystick | M1 | **P0** | idle, active | Translucent cream ring + knob | needs-art |
| `ghm_ui_hud_actions` | HUD action buttons | M1 | **P0** | accelerate, brake, horn, jump, interact, camera, exit vehicle | Round buttons with icons; the horn has a bounce | needs-art |
| `ghm_ui_hud_speedo` | Speedometer | M1 | P0 | per vehicle class | Cartoon dial | needs-art |
| `ghm_ui_hud_minimap_frame` | Minimap frame + compass | M1 | **P0** | north-up, heading-up | Round frame with N marker; attribution chip (§12.5) | needs-art |
| `ghm_ui_hud_altimeter` | Altitude and stamina widget | M3a | **P0** | normal, high-altitude warning | Hearts and energy as in-world stamina (ARCHITECTURE §14 Q6) | needs-art |
| `ghm_ui_hud_vario` | Paragliding vario | M2 | **P0** | climb, sink | Arrow + beeps (§13) | needs-art |
| `ghm_ui_region_download_card` | Region download card | M2 | **P0** | not downloaded, downloading, paused, ready, update, delete | Shows size, Wi-Fi recommendation and the cellular opt-in (ARCHITECTURE §9) | needs-art |
| `ghm_ui_photo_mode` | Photo mode UI | M4 | **P0** | free camera, frame, sticker, filter, score | Score stars for framing and distance (§10) | needs-art |
| `ghm_ui_prompt_sacred` | Respectful-access prompt | M1 | **P0** | info | "This temple is open to Hindu devotees only. You can watch from the east bank." Bilingual wording (review) | needs-art |

### 12.2 Icons

Vector source (SVG) plus exports at **64, 128 and 256 px**. Style: chunky, rounded shapes with 2.5 px ink outlines at 64 px and a soft top highlight. All icons are generic, with no real brand marks.

| id | Icon set | M | P | Count | Contents | Status |
|---|---|---|---|---|---|---|
| `ghm_ico_currency_*` | Currencies | M1 | **P0** | 4 | heart, star, coin, energy | needs-art |
| `ghm_ico_nav_*` | Navigation | M1 | **P0** | 16 | map, search, settings, back, close, home, menu, camera, passport, collection, achievements, wardrobe, garage, pin, route, fast travel | needs-art |
| `ghm_ico_hud_*` | HUD | M1 | **P0** | 14 | accelerate, brake, horn, jump, interact, sprint, camera toggle, exit vehicle, recentre, honk, namaste, photo, zoom in, zoom out | needs-art |
| `ghm_ico_vehicle_*` | Vehicles | M1 | P0 | 36 | One per §8 row (silhouette style) | needs-art |
| `ghm_ico_activity_*` | Activities | M2 | P0 | 9 | bungee, paragliding, zipline, rafting, boating, safari, trekking, cable car, photo | needs-art |
| `ghm_ico_weather_*` | Weather and time | M1 | P1 | 10 | clear, cloudy, fog, rain, storm, snow, wind, day, dusk, night | needs-art |
| `ghm_ico_settings_*` | Settings | M1 | P0 | 14 | graphics, audio, language, accessibility, controls, notifications, data licences, credits, privacy, download region, delete region, Wi-Fi, cellular, storage | needs-art |
| `ghm_ico_wildlife_*` | Wildlife and birds (collection) | M2 | P1 | 40 | One per §10 species; locked silhouette + revealed | needs-art |
| `ghm_ico_poi_*` | POI icons | M1 | **P0** | 63 | One per `PoiKind` value except NONE (§12.3) | needs-art |

### 12.3 Map pins per PoiKind group

Pin = group-coloured teardrop (ink outline, glossy top) + the white POI icon. States: undiscovered (sketch, grey), discovered, landmark (gold rim, bigger), selected (bounce). Sizes 48/64/96 px.

| id | Group | M | P | Pin colour | PoiKind values (each gets `ghm_ico_poi_<kind>`) | Status |
|---|---|---|---|---|---|---|
| `ghm_ui_pin_religious` | Religious | M1 | **P0** | saffron #F2A33A | TEMPLE_HINDU, STUPA, GOMPA, SHRINE, MOSQUE, CHURCH, PLACE_OF_WORSHIP, CHORTEN, MANI_WALL | needs-art |
| `ghm_ui_pin_heritage` | Heritage | M1 | **P0** | brick #B4543A | HERITAGE_SQUARE, PALACE, MONUMENT, RUINS, STONE_TAP, CITY_GATE, MUSEUM | needs-art |
| `ghm_ui_pin_nature` | Nature | M1 | **P0** | green #4F9B3B | PEAK, VIEWPOINT, WATERFALL, CAVE, LAKE, HOT_SPRING, GLACIER, PASS, SPRING, RIVER, PARK, PROTECTED_AREA, NOTABLE_TREE, RIDGE | needs-art |
| `ghm_ui_pin_transport` | Transport | M1 | **P0** | cyan #1C8DB5 | AIRPORT, HELIPAD, BUS_STATION, FUEL, CABLE_CAR_STATION, RAILWAY_STATION, TAXI_STAND, BRIDGE, PARKING | needs-art |
| `ghm_ui_pin_tourism` | Tourism and services | M1 | P0 | purple #8E6AD8 | HOTEL, GUEST_HOUSE, HOSTEL, CAMP_SITE, TEAHOUSE, RESTAURANT, CAFE, ATTRACTION, SHOP, MARKETPLACE, INFORMATION, PICNIC_SITE, THEME_PARK, ZOO | needs-art |
| `ghm_ui_pin_activity` | Activities | M2 | **P0** | red #F2584A | BUNGEE, PARAGLIDING, ZIPLINE, RAFTING, BOATING, SAFARI | needs-art |
| `ghm_ui_pin_civic` | Civic | M1 | P2 | grey #7A8A99 | SCHOOL, HOSPITAL (generic green cross, never a red cross), GOVERNMENT, BANK | needs-art |
| `ghm_ui_pin_player`, `_vehicle`, `_custom` | Player, vehicle and custom pins | M1 | **P0** | — | Player head marker with a heading wedge; parked-vehicle marker; 6 custom pin colours | needs-art |

### 12.4 Passport, collections, achievements, photo mode

| id | Asset | M | P | Spec | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_ui_passport_book` | Explorer's Passport book | M2 | **P0** | Cover (with the generated flag, §12.7), page spreads, page-turn animation | ROADMAP M2 | needs-art |
| `ghm_ui_stamp_province_{koshi,madhesh,bagmati,gandaki,lumbini,karnali,sudurpashchim}` | Province stamps (7) | M2 | **P0** | Unique frame shape per province + motif; SVG + 256 px | Province names from OSM admin level 4. Bilingual text rendered at runtime (shaped), not baked | needs-art |
| `ghm_ui_stamp_district_template` | District stamp template (77 districts) | M2 | **P0** | Province frame family + motif slot + runtime bilingual district name + date | Generated from data (OSM admin level 6). Art supplies the templates | procedural |
| `ghm_ui_stamp_motif_*` | District motif library | M2 | **P0** | ~40 motifs (temple, stupa, peak, lake, rhino, tea leaf, rhododendron, kite, boat, yak, etc.) | Assigned per district by curation; districts in released regions first, all 77 by M5 (review per district) | needs-art |
| `ghm_ui_card_frame_{common,uncommon,rare,legendary}` | Collection card frames | M2 | P1 | 4 rarity frames + category tabs (landmarks, wildlife, vehicles, festivals, peaks, food) | Card art: landmark renders and wildlife photos taken in-game | needs-art |
| `ghm_ui_badge_{bronze,silver,gold,special}` | Achievement badge shapes | M5 | **P0** | 4 shapes + ~60 badge icons | Complete collections and achievements (ROADMAP M5); some badges are needed from M2 | needs-art |
| `ghm_ui_photo_frame_*` | Photo-mode frames | M4 | P1 | 8: polaroid, postcard ("Greetings from Nepal", generic), passport page, prayer-flag border, dhaka border, snow-peak, film strip, festival | — | needs-art |
| `ghm_ui_photo_sticker_*` | Stickers | M4 | P1 | ~24: namaste hands, momo, chiya cup, yak, rhino, danphe, rhododendron, kite, peak, bus, "Ghumante!", speech bubbles | **No Buddha eyes or deity images as stickers** (review) | needs-art |
| `ghm_ui_photo_lut_*` | Filters | M4 | P1 | 6 LUT strips 1024×32 (warm, cool morning, vintage film, monsoon, mono, sepia) | — | needs-art |

### 12.5 Map style (ARCHITECTURE §7.8)

| id | Asset | M | P | Spec | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_map_paper` | Paper texture | M1 | **P0** | 512² tileable, ASTC 6×6 | `map.paper` base with fibres and stains | needs-art |
| `ghm_map_sketch_hatch` | Fog-of-discovery sketch style | M1 | **P0** | 512² pencil hatch + paper | Undiscovered areas are drawn in pencil sketch, revealed by the fog mask | needs-art |
| `ghm_map_fog_mask` | Fog-of-discovery mask | M1 | **P0** | R8 per region, saved compressed (ARCHITECTURE §7.10) | Runtime-generated; soft brush edge sprite from art | procedural |
| `ghm_map_style_sheet` | Line and area styles | M1 | **P0** | Width, colour and outline per `RoadClass`, `LineKind`, `AreaKind` | Thick ink outlines; trails dashed; rivers wavy | needs-art |
| `ghm_map_patterns_{forest,water,farm,glacier,sand}` | Area fill patterns | M1 | P1 | 256² tileable, ASTC 6×6 | Cartoon tree stamps, water ripples, field stripes, ice cracks | needs-art |
| `ghm_map_peak_glyph`, `ghm_map_hachure` | Peak glyphs and relief | M1 | P1 | Vector | Snowcap glyph by elevation band | needs-art |
| `ghm_map_route_ribbon` | Route ribbon | M1 | **P0** | Dashed glossy line, per-mode colour | Also drawn in the world (ARCHITECTURE §7.8) | needs-art |
| `ghm_map_label_shield_*` | Road and route shields | M1 | P2 | Highway number shields (verify the current numbering scheme) | — | needs-art |
| `ghm_map_compass`, `ghm_map_scale_bar` | Compass rose, scale bar | M1 | P1 | Vector | — | needs-art |
| `ghm_map_borders` | Province, district and national borders | M2 | P1 | Dashed styles | **The national border depiction is a pending decision** (ARCHITECTURE P15, LICENSES §4) | needs-art |
| `ghm_map_attribution_chip` | "© OpenStreetMap" chip | M1 | **P0** | Corner chip, tappable through to openstreetmap.org/copyright, may collapse to (i) after 5 s | Required on every map screen (LICENSES §1) | needs-art |

### 12.6 Loading screens, app icon, splash

| id | Asset | M | P | Spec | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_ui_loading_kathmandu_valley_{land,port}` | Loading illustration: Kathmandu Valley | M1 | **P0** | landscape 2048×1024 and portrait 1024×2048, ASTC 6×6; tip text area; safe-area aware | Key art: stupa, scooter, pigeons, Himalaya behind | needs-art |
| `ghm_ui_loading_{pokhara,annapurna,khumbu,terai,mustang_farwest,east}` | Regional loading illustrations | M2 | P1 | as above | One per milestone region: M2 Pokhara, M3a Annapurna, M3b Khumbu, M4 Terai, M5 the others | needs-art |
| `ghm_ui_app_icon` | App icon | M1 | **P0** | iOS 1024² master; Android adaptive foreground and background layers (432² each) + monochrome layer | Generic character or scooter with a peak; no flag misuse | needs-art |
| `ghm_ui_splash` | Splash / startup credit | M1 | **P0** | Logo + **"Map data © OpenStreetMap contributors"**, legible for ≥ 5 s or until dismissed | LICENSES §1 (ODbL 4.3 notice) | needs-art |
| `ghm_ui_logo_ghumante_{en,ne}` | Game logo, Latin and Devanagari | M1 | **P0** | Vector, Baloo 2-based custom lettering | Devanagari lettering checked by a native designer (review) | needs-art |
| `ghm_ui_store_assets` | Store screenshots and feature graphic (EN/NE) | M6 | **P0** | Per store spec, **portrait and landscape sets** | Localised UI captures | needs-art |

### 12.7 Nepal flag (generated, never hand-drawn)

| id | Asset | M | P | Spec | Notes | Status |
|---|---|---|---|---|---|---|
| `ghm_ui_flag_nepal` | Nepal flag master | M1 | **P0** | An editor script builds the SVG master from the step-by-step geometric construction in **Schedule 1 of the Constitution of Nepal** (outline, moon, sun, border). Outputs: SVG, UI sprites 64/128/256, a non-rectangular cloth mesh for flagpoles (§7.2), and a map icon | The **double pennant**: crimson field, deep blue border; white moon emblem in the upper pennant (crescent with rays, 8 of its 16 rays visible) and a white 12-ray sun in the lower one (verify against the Schedule). Taller than wide (height:width ≈ 1.219, which comes out of the construction and is never typed in). Colours: `flag.crimson` / `flag.blue` (the Constitution names the colours; hex values (review)). Never stretched non-uniformly, never drawn as a rectangle, no text on it | needs-art |

---

## 13. Audio

Everything is a placeholder until composed or recorded, but the file names and loop points are fixed now so engineering can integrate. Library and field recordings must be licensed for commercial games and recorded in LICENSES.md. Sacred chants and ceremonies are recorded only with permission, and never used as a music bed (review).

### 13.1 Formats and import

| Class | Unity load type | Format | Channels | Sample rate | Loops | Notes |
|---|---|---|---|---|---|---|
| Music stems | Streaming | Vorbis (quality ~0.5) | stereo | 44.1 kHz | Bar-aligned loop points, sample-accurate | ≤ 4 stems streaming at once |
| Ambience beds | Compressed in memory | Vorbis (~0.4) | stereo | 32 kHz | Seamless 30–60 s loops | 2 beds crossfaded + spots |
| Ambience spots, longer SFX, animal calls, horns | Compressed in memory | Vorbis | mono | 32 kHz | — | 3D positioned |
| Engine and tyre loops | Compressed in memory | ADPCM | mono | 32 kHz | Short loops (1–3 s) per RPM layer, zero-crossing points | 4 RPM layers, crossfaded |
| Short SFX (UI, foley, footsteps) | Decompress on load | ADPCM | mono | 22.05 kHz | — | ≤ 1.5 s each |
| Voice barks | Compressed in memory | Vorbis | mono | 22.05 kHz | — | EN and NE |

Mix target: about −16 LUFS integrated, −1 dBTP peak (verify with the audio lead). Masters are delivered as 48 kHz / 24-bit WAV.

| Audio memory (MB) | Low (25) | Mid (40) | High (50) |
|---|---|---|---|
| Music streaming buffers | 2 | 3 | 4 |
| Ambience (current region) | 8 | 12 | 15 |
| Vehicles (player + nearby classes) | 5 | 8 | 10 |
| Foley and footsteps | 3 | 5 | 6 |
| Animals | 2 | 4 | 5 |
| UI | 2 | 3 | 3 |
| Voice and festival | 1 | 2 | 3 |
| Reserve | 2 | 3 | 4 |

### 13.2 Adaptive music

Stems: **rhythm** (madal, dhime, damphu, dholak), **melody** (bansuri, sarangi), **harmony / drone** (harmonium, strings, pads) and **texture** (bells, bowls, field ambience). Layers switch by activity: explore, ride, fly, festival, night. All pieces are original compositions in folk idioms. Recorded traditional songs are not used without clearance (review).

| id | Piece | M | P | Instrumentation and mood | Status |
|---|---|---|---|---|---|
| `ghm_mus_main_theme` | Main theme (menu) | M1 | **P0** | Madal groove, bansuri leitmotif, sarangi answer; warm and adventurous | placeholder |
| `ghm_mus_kathmandu_valley` | Kathmandu Valley | M1 | P1 | Newar flavour: dhime drum, cymbals, bansuri; bustling and festive | placeholder |
| `ghm_mus_sting_{discovery,stamp,achievement,pass,photo}` | Stings (2–4 s) | M1 | **P0** | Madal flourish + bansuri rise; the pass sting is bigger and brassy | placeholder |
| `ghm_mus_ride` | Road-trip layer | M1 | P1 | Driving madal + murchunga (jaw harp) groove; sits on top of the regional stem | placeholder |
| `ghm_mus_hills_corridor` | Middle hills / Prithvi corridor | M2 | P1 | Madal, sarangi, bansuri, murchunga; road-trip | placeholder |
| `ghm_mus_pokhara` | Pokhara lakeside | M2 | P1 | Relaxed bansuri lead, soft guitar-like strings | placeholder |
| `ghm_mus_air` | Flight and paragliding | M2 | P1 | Soaring strings + bansuri; intensity follows climb rate | placeholder |
| `ghm_mus_himalaya` | Annapurna and Khumbu | M3a | P1 | Damphu, Tibetan lute (review), sparse bansuri, singing-bowl textures; awe | placeholder |
| `ghm_mus_terai` | Terai | M4 | P1 | Dholak, harmonium, cymbals, flute; Tharu and Maithili inflections (review) | placeholder |
| `ghm_mus_far_west_mustang` | Far west and Mustang | M5 | P2 | Deuda-inspired rhythms (far west) and Mustang lute textures (review) | placeholder |
| `ghm_mus_east` | East | M5 | P2 | Chyabrung-drum-inspired rhythms (Limbu) (review) | placeholder |
| `ghm_mus_festival_dashain` | Dashain festive | M2 | P2 | Original festive arrangement in the Dashain tradition (review melody clearance) | placeholder |
| `ghm_mus_night_*` | Night variants | M1 | P2 | Sparse versions of each regional piece | placeholder |

### 13.3 Biome and place ambiences

| id | Ambience | M | P | Content | Status |
|---|---|---|---|---|---|
| `ghm_amb_ktm_street_{day,night}` | Kathmandu streets | M1 | **P0** | Constant playful honking, motorbikes, vendors' non-verbal calls, distant temple bells; at night, dogs and crickets | needs-art |
| `ghm_amb_temple_courtyard` | Temple courtyards | M1 | **P0** | Bells, pigeons, low murmurs, footsteps on brick | needs-art |
| `ghm_amb_stupa_kora` | Stupa kora | M1 | **P0** | Prayer-wheel creaks, distant chanting at low level (recorded with permission) (review), pigeons, shop murmur | needs-art |
| `ghm_amb_ghats_aarti` | Evening aarti at the ghats | M1 | P2 | Bells, conch, devotional singing (review) | needs-art |
| `ghm_amb_valley_fields` | Valley fields | M1 | P1 | Birds, distant traffic, tractors, wind in crops | needs-art |
| `ghm_amb_forest_{hill,subalpine}` | Forests | M1 | P1 | Birdsong by altitude band, cicadas in the hills | needs-art |
| `ghm_amb_water_{stream,river,roar}` | Streams and rivers | M1 | **P0** | Chosen by `LineKind` and slope; roar for gorges (M2) | needs-art |
| `ghm_amb_waterfall` | Waterfalls | M2 | P1 | Layered roar + mist hiss | needs-art |
| `ghm_amb_lakeside` | Lakeside | M2 | **P0** | Lapping water, oars, distant boat chatter, kingfisher calls | needs-art |
| `ghm_amb_hill_village` | Hill villages | M2 | **P0** | Roosters, goat bells, distant children, tap water, wood chopping | needs-art |
| `ghm_amb_altitude_wind` | High-altitude wind | M3a | **P0** | Gusts, flag flapping, whistling over ridges | needs-art |
| `ghm_amb_teahouse_interior` | Teahouse interior | M3a | P1 | Stove crackle, cutlery, murmur | needs-art |
| `ghm_amb_trans_himalaya` | Trans-Himalaya | M3a | P1 | Dry wind, mule bells, distant monastery horns | needs-art |
| `ghm_amb_glacier` | Glacier | M3b | P1 | Ice creaks, distant icefall rumbles (never a threat) | needs-art |
| `ghm_amb_airport_apron` | Mountain airport | M3b | P1 | Turboprops spooling, radio chatter (unintelligible) | needs-art |
| `ghm_amb_monsoon_rain_{light,heavy}` | Monsoon rain | M1 | P1 | Rain beds + **rain on corrugated tin roofs** (iconic) + gutters | needs-art |
| `ghm_amb_thunder_distant` | Distant thunder | M2 | P2 | Soft rolls | needs-art |
| `ghm_amb_terai_jungle_{day,night}` | Terai jungle | M4 | **P0** | Day: peafowl, langur alarm calls, birds, cicadas. Night: crickets, frogs, distant jackals, owls | needs-art |
| `ghm_amb_wetland` | Wetlands | M4 | P1 | Frogs, waterfowl | needs-art |
| `ghm_amb_cablecar` | Cable car ride | M1 | P2 | Cable hum, tower wheel clicks | needs-art |
| `ghm_amb_festival_{dashain,tihar}` | Festival crowds | M2 | P2 | Kite-flying shouts (generic), swings, deusi-bhailo singing groups (new recordings with permission) (review) | needs-art |

### 13.4 Vehicle SFX (per class and surface)

| id | Class | M | P | Assets | Status |
|---|---|---|---|---|---|
| `ghm_sfx_veh_scooter` | Scooter | M1 | **P0** | 4 RPM loops, start, stop, horn beep, brake squeak | needs-art |
| `ghm_sfx_veh_moto_{commuter,trail,cruiser}` | Motorbikes | M1 | **P0** | 4 RPM loops each, kick and electric start, gear clunk, horn; cruiser thump (P2) | needs-art |
| `ghm_sfx_veh_bicycle` | Bicycles | M1 | P1 | Freewheel loop, pedal loop, bell, brake | needs-art |
| `ghm_sfx_veh_car_small` | Taxi, hatchback, EV | M1 | **P0** | Petrol loops, EV whine, horn, door, indicator tick | needs-art |
| `ghm_sfx_veh_jeep_suv` | SUV, mountain and safari jeep | M1 | P1 | Diesel loops, horn, gear grind (comic), rattle on rough roads | needs-art |
| `ghm_sfx_veh_microvan` | Microbus | M1 | P1 | Loops, sliding door, horn | needs-art |
| `ghm_sfx_veh_bus` | City and tourist buses | M1 | **P0** | Diesel loops, air-brake hiss, **multi-tone musical horn**, door, body taps (verify the conductor's signal) | needs-art |
| `ghm_sfx_veh_truck` | Trucks and tanker | M1 | P1 | Diesel loops, musical horn, air brake, chain jingle (painted truck) | needs-art |
| `ghm_sfx_veh_tempo` | Electric tempo | M1 | P1 | Electric whine, horn | needs-art |
| `ghm_sfx_veh_rickshaw` | Cycle rickshaw | M4 | **P0** | Chain, creaks, bell | needs-art |
| `ghm_sfx_veh_tractor` | Tractor | M1 | P2 | Putt-putt loop, trolley rattle | needs-art |
| `ghm_sfx_veh_boat_{pedal,row,canoe}` | Boats | M2 | **P0** | Pedal paddles, oar creak and splash, canoe pole | needs-art |
| `ghm_sfx_veh_{raft,kayak}` | Raft and kayak | M5 | P1 | Paddle splashes, rapids hits | needs-art |
| `ghm_sfx_veh_stol` | STOL turboprop | M3b | **P0** | Idle, taxi, take-off roar, cruise cabin, landing thump, reverse thrust | needs-art |
| `ghm_sfx_veh_heli` | Helicopter | M3b | **P0** | Rotor loop with Doppler, start-up and wind-down | needs-art |
| `ghm_sfx_veh_ultralight` | Ultralight | M2 | P2 | Two-stroke buzz | needs-art |
| `ghm_sfx_veh_paraglider` | Paraglider | M2 | **P0** | Wind by airspeed, wing rustle, **vario beeps** (rising pitch in thermals) | needs-art |
| `ghm_sfx_veh_balloon` | Balloon | M2 | P2 | Burner roar | needs-art |
| `ghm_sfx_veh_cable_zip_bungee` | Cable car, zipline, bungee | M1 | P2 | Station clunk, trolley whizz, soft cord twang, wind rush (bungee M3b P1) | needs-art |
| `ghm_sfx_tyre_paved_{asphalt,brick,cobble,wood,metal}` | Tyres on PAVED | M1 | **P0** | Roll loops: asphalt hum, brick rumble, cobble rattle, wooden plank clatter, steel-grate rattle (Bailey and suspension bridges) + skid | needs-art |
| `ghm_sfx_tyre_{gravel,dirt,sand}` | Tyres on GRAVEL and DIRT | M1 | **P0** | Crunch, scrape, sand hiss (M4) | needs-art |
| `ghm_sfx_tyre_mud`, `_snow` | Tyres on MUD and SNOW_ICE | M2 | **P0** | Squelch, suck-out; snow crunch (M3a) | needs-art |
| `ghm_sfx_splash_{puddle,ford}`, `ghm_sfx_bump_*` | Splashes and suspension bumps | M1 | P0 | — | needs-art |
| `ghm_sfx_hooves_{horse,yak}_*` | Hooves per surface + bells | M3a | **P0** | Dirt, rock, snow, wood (bridges) | needs-art |

### 13.5 Character foley, animals, UI, festivals

| id | Set | M | P | Assets | Status |
|---|---|---|---|---|---|
| `ghm_sfx_fs_<surface>_{walk,run}_01..06` | Footsteps per `Surface` | M1 | **P0** | M1: asphalt, concrete, brick, cobble, gravel, dirt, grass, wood. Later: mud (M2), rock and snow (M3a), sand and metal (M2/M4). Plus stairs | needs-art |
| `ghm_sfx_foley_*` | Body foley | M1 | P1 | Cloth (light, heavy, down jacket), backpack jingle, doko creak, jump, land, soft cartoon "boing" squash accent | needs-art |
| `ghm_sfx_breath_*` | Breathing | M3a | P1 | Normal, tired, altitude | needs-art |
| `ghm_vo_bark_{en,ne}_*` | Voice barks | M1 | P1 | "Namaste!" (shared), greetings, thanks, cheers, laugh, short tourist and vendor lines; native NE voice actors (review lines) | needs-art |
| `ghm_sfx_ani_*` | Animal calls | M1 | **P0** | One set per §10 species: cow moo and bell, dog bark and whine, pigeon coo and **flock take-off**, macaque chatter, crow caw, chicken and rooster, goat bleat, buffalo low (M1); eagle cry, vulture wing whoosh, kingfisher, spiny-babbler song (M2); mule and horse, yak grunt + bells, bharal and tahr alarm whistles, monal call (M3a/M3b); rhino snort, elephant trumpet, tiger chuff and distant call, chital alarm bark, langur whoop, hornbill wingbeat, sarus duet, peafowl, dolphin blow (M4); red panda calls (M5) | needs-art |
| `ghm_sfx_sacred_*` | Sacred objects | M1 | **P0** | Bell rings (small, medium, large), prayer-wheel creak and ding, butter-lamp match strike, cymbal, conch (review) | needs-art |
| `ghm_sfx_ui_*` | UI | M1 | **P0** | Tap, pill press (pitch per colour), back, panel open and close, ribbon swoosh, coin collect, star gain, heart fill, energy refill, **stamp thump**, toggle, slider tick, toast, error, shutter, map zoom, pin drop, fast-travel whoosh, download complete | needs-art |
| `ghm_sfx_festival_*` | Festivals | M2 | P2 | Dashain kites (string zip, cheers), swing creak, tika bells; Tihar deusi-bhailo (recorded with permission), soft firecrackers, lamp lighting; Losar cymbals and horns (M5) (review) | needs-art |

---

## 14. Fonts and localisation assets

| id | Asset | M | P | Spec | Licence and notes | Status |
|---|---|---|---|---|---|---|
| `ghm_fnt_baloo2` | Baloo 2 (Ek Type), display | M1 | **P0** | Latin + Devanagari; Medium, SemiBold, Bold, ExtraBold (verify the weights in the Google Fonts build) | SIL OFL 1.1, no Reserved Font Name; ship `OFL.txt` + copyright line (LICENSES §2). Titles, buttons, counters, stamps | needs-art |
| `ghm_fnt_mukta` | Mukta (Ek Type), body | M1 | **P0** | Latin + Devanagari; Regular, Medium, SemiBold, Bold | SIL OFL 1.1. Body text, lists, descriptions | needs-art |
| `ghm_fnt_noto_sans_devanagari` | Noto Sans Devanagari, fallback | M1 | P1 | Regular, Bold | SIL OFL 1.1. Fallback for rare conjuncts or glyphs | needs-art |
| `ghm_fnt_assets_sdf` | Dynamic font assets | M1 | **P0** | SDF atlases 1024² per face, filled at runtime and pre-warmed from the EN and NE string tables; fallback chain Baloo 2 / Mukta → Noto Sans Devanagari | Shaping through UI Toolkit's Advanced Text Generator (HarfBuzz + ICU); verify the font-asset workflow on 6.3 in the device spike (ROADMAP 1.1) | procedural |
| `ghm_loc_charset` | Character coverage | M1 | **P0** | Basic Latin, Latin-1, **Latin Extended-A** (macrons in romanised names such as "Boudhanāth"), Devanagari U+0900–U+097F including digits ०–९, **ZWJ / ZWNJ** (U+200D / U+200C; OSM names contain ZWJ, e.g. "निकुञ्‍ज"), "रु" for rupees, arrows | If fonts are subset, the subsets stay under OFL | needs-art |
| `ghm_loc_devanagari_testset` | Shaping test strings | M1 | **P0** | Conjuncts (क्ष, त्र, ज्ञ, श्र), reph (र्क), pre-base vowel sign (कि), nukta, half forms, ZWJ variants, Nepali numerals, long names ("त्रिभुवन अन्तर्राष्ट्रिय विमानस्थल") | Drives the TextSpike screen and CI screenshot tests | needs-art |
| `ghm_loc_string_tables` | EN and NE string tables | M1 | **P0** | Unity Localization; every string keyed; Nepali numerals as a per-locale option | CI fails on missing keys (ARCHITECTURE §7.9); NE reviewed by native speakers (M6) | needs-art |
| `ghm_loc_world_text` | World-space text (signs, map labels) | M1 | P1 | Runtime place names must be shaped: UI Toolkit world-space panels, or glyph meshes pre-shaped by an editor tool using HarfBuzz (verify which on 6.3) | Avoid baking runtime names into textures | needs-art |
| `ghm_loc_baked_text_review` | Baked-text review | M1 | **P0** | Every texture with baked Devanagari (signage, truck art, logos) is typeset in a shaping-aware tool and signed off by a native reader | (review) | needs-art |

---

## 15. Milestone rollup and production summary

Counts are **manifest rows** in this document as of 2026-10-04. A row can cover several variants (`{a,b,c}`) or a family (`*`), so the number of delivered files is higher. Recount when rows change.

### 15.1 Rows per category and milestone

| § | Category | M1 | M2 | M3a | M3b | M4 | M5 | M6 | Rows |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Shared textures and materials | 29 | 1 | — | — | — | — | — | 30 |
| 2 | Terrain biomes | 9 | 3 | 8 | 2 | 5 | 1 | — | 28 |
| 3 | Roads, decals, surface VFX, road kit | 37 | 16 | 5 | — | 2 | 1 | — | 61 |
| 4 | Building kits, roofs, materials | 81 | 16 | 13 | 6 | 7 | — | — | 123 |
| 5 | Hero landmarks | 14 | 17 | 7 | 11 | 6 | 11 | — | 66 |
| 6 | Vegetation | 14 | 8 | 11 | 1 | 2 | 2 | — | 38 |
| 7 | Props | 45 | 20 | 8 | 5 | — | 4 | — | 82 |
| 8 | Vehicles | 18 | 11 | 1 | 4 | 3 | 3 | — | 40 |
| 9 | Characters and animation | 33 | 8 | 6 | 2 | 2 | 1 | — | 52 |
| 10 | Animals and birds | 8 | 8 | 3 | 7 | 12 | 1 | — | 39 |
| 11 | Sky, weather, water, VFX | 23 | 11 | 5 | 2 | 2 | 2 | — | 45 |
| 12 | UI, icons, map, flag | 50 | 12 | 1 | — | 4 | 1 | 1 | 69 |
| 13 | Audio | 34 | 15 | 6 | 4 | 4 | 3 | — | 66 |
| 14 | Fonts and localisation | 9 | — | — | — | — | — | — | 9 |
| — | **Total** | **404** | **146** | **74** | **44** | **49** | **30** | **1** | **748** |

### 15.2 Rows per milestone and priority

| Milestone | P0 | P1 | P2 | Total | Reading |
|---|---|---|---|---|---|
| M1 | 224 | 139 | 41 | 404 | Front-loaded on purpose: M1 builds the shared kits, the materials, the UI system and the valley. **Start P0 rows in week 1**, beginning with §1 shared textures, §4 Newar and modern kits, §5 Boudhanath, Swayambhunath and Kathmandu Durbar Square, §8 scooter, motorbike and taxi, and §12 core UI |
| M2 | 57 | 37 | 52 | 146 | Pokhara, painted trucks and buses, boats, paragliding, passport |
| M3a | 38 | 27 | 9 | 74 | Annapurna and Mustang approach: trans-Himalayan kit, chortens, teahouses, horses |
| M3b | 22 | 15 | 7 | 44 | Khumbu, STOL and helicopter, yaks, bungee |
| M4 | 22 | 19 | 8 | 49 | Terai kit, wildlife, Lumbini, Janakpur, photo mode |
| M5 | 4 | 11 | 15 | 30 | Remaining regions, achievements |
| M6 | 1 | 0 | 0 | 1 | Store assets; M6 is mostly fixes from cultural and localisation review |
| **Total** | **368** | **248** | **132** | **748** | — |

Status today: 671 rows `needs-art`, 48 `procedural`, 29 `placeholder` (M1 secondary landmarks as greyboxes; all music until composed).

### 15.3 Procedural vs authored

| System | Procedural (code or pipeline) | Authored (art) |
|---|---|---|
| Terrain (§2) | CDLOD meshes, morphing, road conformance, biome assignment, snowline, wetness, seasonal blend | 28 biome palette rows, `ghm_tex_noise_world`, slope and rock parameters, region tints |
| Roads (§3) | Ribbons, junction caps, bridge and tunnel placement, decal placement, dynamic tyre tracks and footprints | Road trim atlas, decal atlas, surface particles, road kit (kerbs, gabions, Bailey modules, steps) |
| Buildings (§4) | Facade grammars from OSM footprints, levels, use and seed; roofs per `RoofShape`; LOD1/LOD2 merges; facade atlas bake | Kit pieces per archetype, detail atlases, palettes, roof trims and props |
| Landmarks (§5) | Placement from `landmarks.resolved.json`; class D peaks, lakes and rivers from DEM, skyline and water | Hero meshes (A, B, C, E), dressing presets, detail atlases, reference boards, peak silhouette deltas where needed |
| Vegetation (§6) | Scatter and density per biome, impostor baking, season colour, wind | Species meshes with wind and bloom masks |
| Props (§7) | Wire tangles, prayer-flag lines, terrace walls along contours, suspension-bridge spans, fences along lines | All prop meshes |
| Characters and animals (§9–10) | LOD selection, crowd, herd and flock VAT baking, outfit recolouring, spawning, behaviour | Bodies, outfits, faces, rigs, animation clips |
| Sky and VFX (§11) | Skyline impostor ring per region, sky blending, weather state | Gradients, clouds, particles, water look, transition vignettes |
| UI (§12) | 77 district stamps from templates and data, fog-of-discovery mask, vector map rendering, flag geometry from the construction | Components, icons, pins, stamp templates and motifs, cards, badges, loading art, app icon |
| Audio (§13) | Adaptive layering, surface and RPM selection | All stems, ambiences and SFX |
| Text (§14) | Runtime shaping (Advanced Text Generator) and dynamic font atlases | Font selection (upstream OFL fonts), string tables, baked-text review |

### 15.4 Cultural review index (for the consultant, ROADMAP 1.15)

Every `(review)` tag above falls into one of these groups. Sign-off is needed per group before final art in the milestone that first uses it.

1. **Buddhist iconography:** stupa eyes and proportions, the 13-step spire, prayer-flag colour order, dharma wheel with deer, victory banners, kani ceilings, mani-stone script, prayer-wheel handedness and direction, Bon counter-clockwise practice (§4, §5, §7, §9.4).
2. **Hindu iconography and practice:** temple struts and toranas (non-explicit), banners, deity stones and offerings, tika and jamara, aarti, the non-Hindu access rule at Pashupatinath, cremation ghats without cremations, no animal sacrifice (Manakamana, Dashain) (§4, §5, §7, §10).
3. **Living traditions:** the Kumari never shown; photography rules at Maya Devi and Gupteshwor; festival depictions (Dashain kites and swings, Tihar animal worship, Kag Tihar, Losar, Mani Rimdu, Tiji, Vivah Panchami) (§5, §7, §10, §13).
4. **People and dress:** daura suruwal, dhaka topi, sari, kurta, gunyu cholo, hakupatasi; Gurung, Sherpa, Tharu and Madhesi dress; monks, sadhus and porters shown with dignity; uniforms without real insignia (§9).
5. **Language:** signage wording, truck-art slogans, the sacred-access prompt, district names in Nepali, logo lettering, voice lines (§4, §8, §12, §14).
6. **Art traditions:** Mithila and Tharu motifs commissioned from or reviewed by practitioners; Nepali truck art (§4, §8).
7. **Music:** folk idioms, the Dashain melody, recordings of chants (§13).
8. **Memory and loss:** the rebuilt Dharahara and its old base; the rebuilt Langtang village; post-2015 reconstruction states in the Durbar Squares (§5).
9. **National symbols:** flag construction and colour values; the national border depiction (a pending decision, ARCHITECTURE P15) (§12).

### 15.5 Open art-direction decisions

1. Outlines on hero objects only, or on everything (ARCHITECTURE §14 Q8)? This changes §1.10 and the outline column in §1.5.
2. Palette: the saturated tokens in §1.9 or a more pastel lean (Q8)?
3. Where the art source store lives (Git LFS or external) (§1.3).
4. Whether glTF is accepted for static meshes (glTFast) or FBX only (§1.3).
5. ~~UI orientation~~: **decided**, portrait and landscape both supported (ADR-017). Confirm the reference resolutions in §12 against the reference image.
