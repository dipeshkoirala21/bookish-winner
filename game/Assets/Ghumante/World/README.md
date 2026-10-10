# Ghumante.World: the streamed 1:1 world (M1 track C)

The runtime that turns a region pack into the cartoon world around the player (ARCHITECTURE.md 5, 7.2-7.5, 8).
Decoding, meshing, selection and ground queries are engine-free `Ghumante.Core` (tracks A and B); this assembly
is the Unity glue, plus a small engine-free streaming core (`Streaming/StreamingScheduler`, `TileResidency`,
`TileBuild`) that the EditMode tests drive with a fake clock against the real sample region.

## Using it (track D and later)

```csharp
WorldRoot world = gameObject.AddComponent<WorldRoot>();   // transform at the identity
world.Tier = Bootstrap.Instance.Tier;                     // optional: defaults to the active quality level
world.Focus = spawn;                                      // optional: defaults to the region centre
world.OriginShifted += delta => myRoot.position -= new Vector3((float)delta.X, delta.Y, (float)delta.Z);
await world.OpenRegionAsync("kathmandu_core");            // unpacks on Android, loads search + routes
// each frame: world.Focus = vehicle.Position;            // streaming and the floating origin follow it
// spawn once world.HasGroundAt(x, z), then vehicle.Teleport(x, z, heading, world.Ground)
Vector3 p = world.ToScene(vehicle.Position);              // Unity position = world - Origin
world.ShowRoute(astar.RouteGeometry(route));              // bright animated ribbon on the ground
world.TimeOfDayHours = 6f;                                // sunrise
world.Close();                                            // back to the menu: unloads everything
```

| Member | What |
|---|---|
| `OpenRegionAsync(id)`, `IsOpening`, `IsOpen`/`IsReady`, `Ready`, `Progress`, `Close()`, `Closed` | Region lifecycle. `RegionSource` defaults to `Platform.Regions.BuiltInRegionSource.CreateDefault()`. |
| `Focus`, `Teleport(pos)`, `FocusOverride` | Streaming focus (game metres). `Teleport` rebases at once; `FocusOverride` is for the debug free-fly camera. |
| `Origin`, `OriginShifted(delta)`, `ToScene`, `ToWorld` | Floating origin (ADR-003): rebased when the focus is 2 km away, at the start of the frame (execution order -500). |
| `Ground` (`IGroundQuery`), `GroundQuery` (`TileGroundQuery`: decks, roads), `HasGroundAt`, `GroundVersion` | Exactly what is drawn: an area enters the ground query when its mesh becomes visible and leaves when hidden. |
| `Search`, `Routes`, `Manifest` | Region search engine, routing graph, manifest (null when the region has none). |
| `TimeOfDayHours`, `Sky` | Day/night (48 real minutes per day), sun/moon light, sky, valley haze, ambient. |
| `ShowRoute(xz)`, `ClearRoute()`, `Route` | Route ribbon. |
| `Stats`, `IsSettled`, `DebugSummary()`, `Active` | Streaming counters (the debug HUD shows them). |
| `ConfigureCamera(camera, config)` | Clip planes (far = view radius) and skybox clear for any world camera rig. |
| `Life` (`LifeHost`), `Zones` (`SacredZoneIndex`), `AreaTypes` (`AreaTypeGrid`) | W2: the life sims and the shared zone indexes of the visible detail tiles. Gameplay calls the sims only through `Life.Post(...)`. |
| `Curated`, `Transit`, `AviationConfig`, `Heroes` | W2 region sidecars (`.curated.ghcd` or `hero_recipes.json`, `.transit.ghrt`, `.aviation.json`) and the hero set with its hide zones. |
| `Sound`, `Month`, `ViewCamera`, `Materials` | The `ISoundService` presenters use (App may set it; else `AudioDirector` when it exists), the month for palettes and the airport, the camera bands are measured from, the material set. |
| `RenderStats`, `ReportLife(...)` | Per-frame triangle and instance counters (W2_DESIGN 10.4 slices); presenters add theirs. `DebugSummary()` prints them. |
| `AnyReady` (static), `DetailTileShown`, `DetailTileHidden` | Hooks: presenter assemblies World cannot reference attach on `AnyReady`; detail tiles come and go with their decoded data and extras. |

## How streaming works

Every frame (`LateUpdate`): `TileSelector.Select(focus)` when the focus moved 4 m, `TileResidency` diffs it against
what is resident (`TileLoadPlanner`), the nearest queued nodes are decoded and meshed on at most two worker threads
(`TileBuild.Execute`, Core only, no Unity API), finished builds are uploaded in chunks of at most 32 768 vertices
(`UploadChunk`, one mesh each: a dense city buildings layer of 290 000 vertices spreads over several frames) within
2 ms (1.5 ms at 60 fps) and 1 MB per frame through the advanced Mesh API (multi-stream, 16-bit indices, CPU copy
released), and the swap rule shows them: a node that leaves the selection stays visible until every node replacing
it is ready, so there are no holes and never two overlapping LODs on screen. Decoded tiles sit in a byte-budgeted LRU
(30/60/90 MB); resident nodes are capped per tier (`StreamingConfig.MaxResidentTiles`).

Per node: terrain from the source tile cropped to the area (`StreamingConfig.TerrainStep`, skirts), and for exact
level-10 nodes roads, buildings and water/land-use areas draped on the same `TileHeightSampler` the ground query uses.

## W2: the city reads right and moves (W2_DESIGN 10.5 stage 1, track C1)

Each exact level-10 detail node now builds, on its worker (`TileBuild.Execute`), besides terrain, roads and areas:

| Layer / result | What | Parts |
|---|---|---|
| `RoadDecals` | Track A's `MarkingMesher`: centre, lane and edge lines, zebras, stop lines (drawn with the `decals` material: a stronger depth pull and polygon offset). | one |
| `Buildings` (B1) | Styled extrusions (`BuildingBand.B1Styled`), regrouped per 128 m block (`MeshParts.ByBlocks`). | block index |
| `BuildingsFar` (B2) | Prisms, per 128 m block. | block index |
| `BuildingsBlock` (B3) | 16 m city blocks, per 256 m block. | block index |
| `Heroes` | Every hero of the region anchored in the tile (`HeroSet`, from the curated DB) at LOD 0-3 (`HeroBuilder`), with their structure colliders. Their D5 hide zones are applied to every band (`MeshingSettings.SetHiddenRefs`). | hero × 4 + lod |
| `TileExtras` | Decoded tile and sampler (for B0 cells and the sims), `TileInstances` (OSM, avenue and forest trees; street props; parked vehicles from `ParkedPlacement`), heroes, hero colliders. Handed to the view by `ITileSink.Ready`. | |

Per frame (`WorldStreamer.UpdateView`, after `Tick`): `BandConfig` gives the tier's radii (Low 35 / 120 / 350 / 750 m,
Mid 60 / 200 / 500 / 1,250, High 80 / 250 / 700 / 1,750). A block renderer is on when its bounds touch its band's
distance range around the camera (`BandConfig.Touches`, conservative), and the band materials (`_BAND_FADE`) dither the
exact 4 m cross-fade per fragment around `_GhBandCentre`, so blocks may cut a building without it showing.
`DetailCells` builds B0 (`BuildingDetailMesher.BuildCell`) per 64 m cell near the camera on a worker (one at a time,
at most one started per 100 ms, nearest first), uploads it when the tile uploads leave room, keeps an LRU of 24 / 48 / 96
cells; a B1 block keeps its "full" material (and hides its cells) until every cell of it near the camera is up, so there
is never a hole. `HeroLodBudget` (W2_DESIGN 3.2) picks each visible hero's LOD by screen height under the hero slice
(20 k / 40 k / 60 k, Low never LOD0, one LOD step per 0.5 s with 10% hysteresis). Hero and shown-cell colliders are
merged per tile and registered with the ground query (`TileGroundQuery.Register`) while the tile is visible.

`DressingRenderer` draws the trees (three shape families × three LODs, LOD0 / LOD1 nearest first under the tier caps,
crown colour by species and month, vertex wind), chautari platforms and every street prop kind with
`Graphics.RenderMeshInstanced` (`InstanceBatch`: GLES3-safe GPU instancing, per-instance tint arrays). Matrices and tints
are built per tile progressively (`BuildUnitsPerFrame` instances a frame, nearest tile first; a tile draws once complete),
shifted on origin rebases, re-tinted progressively on a month change and kept `KeepFrames` after the tile is hidden. The
far tree LOD is nearest first too: the LOD1 overflow, then whole 64 m cells sorted by distance, so the far cap never
leaves a clearing around the camera.

`LifeHost` runs Track B's `LaneGraph`, `TrafficSim` (+ `BusRouteRunner` on the `.ghrt` routes), `PedestrianSim`,
`AnimalSim` and `AirTrafficSim` on one worker step per frame (tile adds and gameplay `Post`s are applied between steps)
and exposes the snapshots. Each step's horns are consumed once (`TakeHorns`); a frame whose step is still running keeps
the snapshot and reports its lag (`SnapshotAgeS`, ≤ 0.25 s) so presenters extrapolate poses along their heading; a long
hitch's time carries over instead of being lost; after a failed step the host goes quiet (no steps, horns or queued
posts). The presenters only read them:

| Presenter | Assembly | Draws and plays |
|---|---|---|
| `TrafficPresenter` | Traffic | Moving vehicles nearest first (LOD0 / LOD1 / LOD2 caps 0 / 1 / 6, 1 / 6 / 16, 3 / 10 / 20; LOD0 with its own plate), spinning wheels, parked vehicles (LOD2 ≤ 20 m, block-out ≤ 80 m, box ≤ 150 m, caps 80 / 200 / 400); engine voices for the nearest agents, air brakes, the sim's horns. |
| `CrowdPresenter` | Traffic | People as animated rigid parts (`PeopleRenderer`, `PersonAnimation`) under the crowd caps, with their carry props (doko, sack, gas cylinder, baby, umbrella); traffic officers on the podiums of the police chowks (`OfficerPosts`) cycling the five hand signals; NPC footsteps on the surface under the foot. |
| `AnimalPresenter` | Wildlife | Cows (lying, standing) and dogs (asleep, standing) under the animal caps, coat tints; moos and barks. |
| `AviationPresenter` | World | The TIA runway corridor with markings and lights (`RunwayMesher`, built at once from the sidecar over any resident ground and rebuilt when finer ground arrives), aircraft (`AircraftMesher`, generic liveries as tail tints) nearest first under the 4 / 6 / 10 cap with one LOD0, nav lights and beacons, two live aircraft voices. |

### Open issues (W2 stage 1)

- **Crowd bodies vs. the 10.3 contract**: the crowd is still rigid `KitMeshes` parts, not `HumanoidMesher` bodies from
  `CharacterRecipe.ForPedestrian` (LOD0/1) with a VAT body baked from its LOD2, so the crowd and the player do not
  share one style yet. Needs a part split (or VAT bake) of the skinned humanoid within the crowd draw budget.
- **Life step scheduling**: tile adds and removes (lane and walk graph building) still run inside the timed life step,
  which shares the thread pool with tile builds; presenters hide held snapshots by extrapolation, but a dedicated
  worker and a separate low-priority graph job are still to do.
- **Seam widths** (Track A / E): within ~30 m of a tile edge every piece is floored at `RoadWidthModel.BorderWidthM`
  (URBAN column for untagged ways, so both tiles agree), so old-core and hill lanes along seams can still overrun
  buildings (V2 seam bands ≈ 0.9% in OLD_CORE and HILL against ≤ 0.02% inside tiles; asserted < 1% per area type in
  `RibbonsNeverCoverBuildingsOnTheSamplePack`). Flooring only the stretches next to real cut ends was tried and
  reverted: it leaves width steps at piece joints near the seam whose lane connectors let two buses overlap in
  `RingRoadRunsHalfAnHourWithoutContact`. Needs a per-cut width written by the pipeline (RATR) plus continuous
  widths across joints.
- **Split chowks**: a synthetic police island whose disc would cover a second junction node or another carriageway
  (split dual-carriageway chowks) shrinks clear of it, and below 6 m the chowk keeps only its podium.

## Rendering

| Shader | Use |
|---|---|
| `Ghumante/ToonLit` | Terrain, buildings, roads, areas: vertex-colour albedo (sRGB palette, linearised) × procedural material texture (see "Look"), 3-band toon ramp, cool shadow tint, soft rim in the light colour, SH ambient, main-light shadows, URP fog, earth curvature `y -= d²/(2·R_eff)`, `_ViewPull` + polygon offset for overlays. ForwardLit, Outline (LOD 300 only), ShadowCaster, DepthOnly, DepthNormals. W2: shader model 3.5, GPU instancing, `_BAND_FADE` (dithered band cross-fade, `_BandRange`), `_WIND` (vertex sway, `_Wind`), `_INSTANCE_TINT` (per-instance colour masked by vertex alpha). |
| `Ghumante/InstancedLights` | Airport and aircraft lights: unlit instanced studs, per-instance colour, brighter after dusk (`_GhNightLights`). |
| `Ghumante/SkyGradient` | Skybox from `WorldSky` globals: zenith/horizon/ground, sun and moon discs, sunrise glow, haze blend at the horizon. |
| `Ghumante/RouteRibbon` | Transparent animated chevron ribbon with the same curvature. |

All are SRP Batcher compatible (per-material properties in `UnityPerMaterial`). The W2 materials (decals, five band
materials, heroes, instanced, instanced tint, trees, lights) are created by Project Setup too; a set made before W2 gets
them at runtime (`WorldMaterialSet.EnsureExtras`). Project Setup creates the materials in
`Assets/Ghumante/Settings/Materials/` and `Assets/Ghumante/Settings/Resources/GhumanteWorldMaterials.asset`, which keeps
them in builds; tune them there (Project Setup does not overwrite existing values, except the look roles above).

## Look (W2 detail pass: cartoon, rounded and rich)

Owner feedback: "not this level of boxy", "houses block the view". docs/W2_DETAIL_CONTRACT.md decisions 6 and 7.

**Material channels and procedural textures.** Every mesher writes `MeshData.Uv0` = (`MaterialChannel`, baked AO).
`Core/Synth/Textures/MaterialTextures` generates one tileable texture per channel at startup from code and a seed
(FNV-1a of the channel name; nothing is stored or downloaded): brick with lime mortar, glazed dachi apa brick, wood grain
with knots, carved wood (lotus rosettes and tiki jhya lattice), jhingati roof tiles, galvanised metal, hammered gilt with
glints, ashlar stone, asphalt (aggregate, wear, repair patches, cracks, oil), concrete (pores, formwork seams), grass with
tiny wild flowers, foliage, bark, fabric weave, skin, glass with cartoon shine bands, water ripples, paint with chips,
rubber, dirt with pebbles, flagstones with grout, hair, leather, worn road paint; slice 0 is a macro-variation layer.
Encoding: sRGB rgb + alpha = tint weight; the shader computes `albedo = lerp(rgb, tint × rgb × 2, a)`, so a texture
enriches the mesher's palette colour (rgb 0.5 = unchanged) and can carry absolute colours (mortar, grout, worn-through
asphalt). Every pattern is periodic over the tile with integer noise periods (tested by rendering a shifted window), mips
are box-filtered in linear space. `MaterialLooks` holds the per-channel shading table (tile size in metres, toon highlight
strength and gloss, glints, metallic tint, sky reflection, macro strength, water drift). Every tile size and the 32 m
macro period are powers of two that divide 64 m (tested): tile-local meshes (terrain, roads, areas, water) are sampled in
object space, and tile sides are 2^(20-L) m, so the patterns run on across tile borders without a seam (brick 0.5 m per
repeat: 0.25 m bricks in 62.5 mm courses; jhingati 0.5 m; stone and glass 1 m; grass 2 m; asphalt and water 4 m; fabric
0.125 m; skin, hair, leather, rubber 0.25 m). Run the core tests with `GHUMANTE_SWATCH_DIR=<dir>` to write every texture
as PNG.

**`ToonLook`** (Rendering/, self-starting: `RuntimeInitializeOnLoadMethod` + `RenderPipelineManager.beginCameraRendering`,
no scene object, no per-frame allocation) bakes the textures on worker threads (`ToonTextureBank`, ≈ 9 MB at 256², 2.3 MB
at 128² on Low; a neutral 4 × 4 array is bound until then, which renders the flat colours), uploads one mipmapped sRGB
`Texture2DArray`, binds the channel table as `_GhChannelA/_GhChannelB[32]`, applies the device tier (`ToonLookTier`),
applies the tier's look roles to the active world's materials, and sets the occluder capsule before the world camera
renders. A tier change with another texture size (Low 128², Mid/High 256²: a settings change, a thermal step-down)
re-bakes in the background and swaps the arrays when the new one is uploaded; the old one stays bound meanwhile.
`ToonLook.Refresh()` runs the same per-camera step for tools and tests. In the editor `ToonLookEditor` starts it on every
domain load (Scene view shows the look) and **Ghumante > Look > Refresh Material Textures** re-bakes.

**`Ghumante/ToonLit`** samples the channel's texture triplanar in object space (stable under the floating origin; moving
vehicles keep their texture), adds the macro layer, fades to the flat colour with distance, multiplies by the vertex tint
and the baked AO (full on ambient, half on direct light), then the 3-band toon ramp, a toon highlight per channel (white
on paint and glass, tinted on metal and gilt), glints on gilt and water, sky reflection at grazing angles on glass and
water, the warm rim and fog. Meshes without UV0 read (0, 0) and render exactly as before (Plain, no AO); channel values
without a texture slice (`GH_SLICE_COUNT` = `MaterialTextures.SliceCount` = 26, tested) render as Plain too. All texture
gradients are taken before the band and occluder `clip()`s (derivatives after a discard are undefined on Vulkan and GLES).
Glints use cells at least two pixels wide (1/64 m up close, the next power of two as the pixel footprint grows, an integer
hash), fade out between ≈ 58 and 117 m at 1080 p and with the texture reach, and reshuffle over ≈ 3° of view change, so
gilt roofs and rivers sparkle instead of fizzing per pixel. Water drifts by whole wrapped tiles (no precision loss).

**Outlines** are an inverted hull: a second pass (`LightMode` `SRPDefaultUnlit`, which URP's opaque pass draws right after
`UniversalForward`) pushes back faces out along the screen-space normal by a constant pixel width, fading out with
distance; the colour is a dark shade of the surface colour, lit like the scene. Where the width reaches zero the hull
stays in place behind the surface (hidden by the depth test) and the fragment discards what is left below 0.05 px;
vertices are never moved one by one (a triangle crossing the fade-out radius would become a screen-wide sliver:
`previews/look/fix/outline_collapse_fixed.png`). Instanced draws drop whole instances whose origin lies more than 32 m
beyond the fade-out (all vertices of an instance agree). The tier toggle is the shader LOD (the LOD 300 SubShader has the
outline pass, the LOD 200 SubShader does not; `ToonLook` sets `Shader.maximumLOD`), and per material the pass is enabled
by the tier's role (`WorldMaterialDefaults.RoleOf`, ARCHITECTURE 10 "Outlines"). No URP renderer feature, no depth texture.

| Tier | Textures | Triplanar | Texture reach | Glints | Outline | Outlined materials |
|---|---|---|---|---|---|---|
| Low | 128² | dominant axis (2 samples with macro) | 80 m | off | none (LOD 200) | none (deviation from "landmarks only": a second draw per hero on the tier with the tightest batch budget) |
| Mid | 256², aniso 2 | blended (4 samples) | 160 m | on | 1.6 px at 1080 p, fades 30–80 m | explorer + their vehicle (`buildings`), traffic (`instanced`), people, animals, aircraft (`instancedTint`), heroes |
| High | 256², aniso 4 | blended (4 samples) | 260 m | on | 2 px, fades 40–110 m | Mid + street props (`props`) + the near building band (`bandB0`, 80 m) |

Never outlined: terrain, roads, areas, markings, trees, the B1/B1-full/B2/B3 bands. **Cost**: the outline adds one draw
per outlined renderer or instanced batch and runs its vertices again; `RenderStats.OutlinedTris(tier)` gives the
per-frame upper bound from the streamer and presenter counters (vehicles + parked + people + animals + aircraft + heroes +
props, + B0 on High). Estimate against ARCHITECTURE 10 (only non-empty batches draw): Mid ≈ +30–60 draws (traffic
≈ 12–25 batches, people ≤ 30 and animals ≤ 12 batches, the hero renderers, the explorer) and at most the hero budget
(40 k triangles) plus the people and vehicle triangles of the 60 / 30 caps; High adds the B0 cells in 80 m and ≤ 20 prop
batches, with a 60 k hero budget. Before this fix every B0 and B1 chunk to 200–250 m, every tree batch and every prop batch
drew it on Mid too. To validate on device (Wave 3).

**Occluder fade** (`_OCCLUDER_FADE`; the engine-free twin is `Core/Synth/Look/OccluderCapsule`, tested in
`core-tests/SynthOccluderFadeTests`): surfaces between the camera and the player dissolve into a 4 × 4 Bayer screen door
(18% left), so houses, walls, balconies, temples and bridge parts never block the view, also when the collision-aware
camera is squeezed 1.5 m behind a motorbike backing down a galli. The capsule is four global vectors set once per camera;
there is no per-object CPU work. It is fully faded within a radius growing from the camera end (at least 1 m, more when
the view frustum 0.8 m in front of the lens is wider: landscape, wide FOV) to 1.3 m at the player, with no fade-in at the
camera (a wall 0.1 m in front of the lens is gone), soft edges of 12% of the capsule (0.15–0.6 m) outside that radius,
nothing at or beyond the plane through the player (the background stays), and a solid sphere of 0.5 m (at most a quarter
of the capsule) around the player end. The player end is the world focus raised 1 m, or `ToonLook.SetOccluderTarget` when a
camera rig sets a better point that frame. The road material fades as a **ground layer** (`_OccluderGround`): only above
the player's knees, and upward-facing surfaces only from 0.8 m above the player end, so bridge railings, piers, parapets,
flyover undersides and decks overhead dissolve while the road surface, kerbs and footpaths never do. The plain building
material (the explorer's own material is copied from it) and traffic never fade; shadows of faded houses stay.

| Material | Outline (tier) | Occluder fade |
|---|---|---|
| terrain, areas, decals | — | — |
| roads (also bridge and flyover parts) | — | ground layer |
| buildings (explorer source), instanced (traffic) | Mid, High | — |
| instancedTint (people, animals, aircraft), heroes | Mid, High | yes |
| props | High | yes |
| bandB0 | High | yes |
| bandB1, bandB1Full, trees | — | yes |
| bandB2, bandB3 | — | — |

Project Setup stores the High roles (the superset) in the material assets; `WorldMaterialSet.Load` and `ToonLook` apply
the tier in use at runtime (before the streamer clones the band materials when a world opens).

**Audio** (decision 7): the crowd walla beds are off by default (`AudioDirector.CrowdWallaEnabled`, `AmbienceInputs
.CrowdWalla`); the bank does not even bake them, and the temple-courtyard bed is now quiet air and pigeons without
walla. Footsteps, engines, horns, bells, birds and aircraft keep their W2_DESIGN 7.1 level targets.

### Open issues (look)

- **Props material** (nature): `DressingRenderer` draws street props and the chautari with `WorldMaterialSet.instancedTint`
  (the people's material), so props are outlined on Mid too. Switch those two batches to `WorldMaterialSet.props` (same
  shader setup) to get the ARCHITECTURE 10 split (props outlined on High only).
- **Bridge and flyover layer** (integration, bridges): structure geometry drawn with the road material fades as a ground
  layer (above the player's knees). That puts a `clip()` in every road fragment, which costs early depth writes and
  hidden-surface removal on mobile GPUs over a large share of the screen. Once the bridge meshes get a layer of their
  own, draw it with a material that fades whole (like `heroes`), keep the deck surface on the road layer and turn the
  road material's fade off again (`WorldMaterialDefaults.RoleOf`).
- **Outline cost in the HUD** (integration): show `RenderStats.OutlinedTris(ToonLook.Tier)` next to the triangle counters
  in the debug HUD for the Wave 3 device pass.
- **Inspector-assigned material sets**: a `WorldRoot` with a set assigned in the inspector skips `WorldMaterialSet.Load`, so
  its band clones keep the asset's High roles (B0 outlined on Mid) until the next open after `ToonLook` applied the tier.
- `ToonLook.Occluder` now takes `Ghumante.Core.Synth.Look.OccluderFadeSettings` (moved to Core so the capsule is tested
  by the core tests); `ToonLook.SetOccluderTarget` is unchanged.

## Trying it in the editor

**Ghumante > World Preview** builds a throw-away scene (camera, sun, `WorldRoot` + `WorldPreview`) and presses Play.
Fly with WASD, Q/E, Shift, the mouse wheel and a mouse drag (touch: drag, pinch, two-finger slide). T: fast time,
[ and ]: one hour back/forward, P: pause the clock, R: route on/off, F3 (touch: hold four fingers for a second): free-fly
overlay over gameplay (in a game scene; it closes with the world). The hotkeys ignore keys typed into a text field. The corner box shows the streaming statistics.

## Files

| Path | What |
|---|---|
| `WorldRoot.cs` | The component above. |
| `Streaming/` | `StreamingScheduler` (engine-free orchestration), `TileResidency` (swap rule), `TileBuild` (worker job), `WorldStreamer` + `TileView` + `MeshUpload` (Unity side). |
| `Sky/` | `SkyPalette` (engine-free sun path and time-of-day palette), `WorldSky` (light, sky, fog, ambient). |
| `Navigation/` | `RibbonBuilder` (engine-free strip geometry), `RouteRibbon`. |
| `Rendering/` | `WorldMaterialSet`, `WorldShaders`, `EarthCurvature`, `RenderStats`; the look: `ToonLook`, `ToonLookTier`, `ToonTextureBank`, `ToonLitLayout` (C# twin of the SRP Batcher layout); the occluder capsule's twin is `Core/Synth/Look/OccluderCapsule`. |
| `Shaders/` | The shaders and their includes (`ToonLitInput`, `ToonLitForwardPass`, `ToonLitOutlinePass`, `ToonLitDepthPasses`, `GhumanteCommon`). |
| `Debug/` | Development-only: `FreeFlyCamera`, `WorldDebugHotkeys`, `WorldPreview`. |
| `Editor/` | `WorldSetup`: materials for Project Setup, the World Preview menu; `ToonLookEditor`: the look in edit mode. |
| `Cameras/CameraFov.cs` | Orientation-aware FOV maths for camera rigs (M0). |
| `Streaming/MeshParts.cs` | Spatial regrouping of band layers into blocks; part ranges. |
| `Buildings/` | `BandConfig` (radii, fades, block sizes; engine-free), `DetailCells` (B0 cells). |
| `Sacred/` | `HeroSet` (heroes per anchor tile, hide zones), `HeroLodBudget` (engine-free). |
| `Instancing/` | `InstanceBatch`, `KitMeshes` (trees, props, people, cows, dogs), `DressingConfig`, `DressingRenderer`, `TileInstances`, `ParkedPlacement` + `FootprintGrid`, `PersonAnimation`, `PeopleRenderer`, `WorldHash`. |
| `Life/` | `LifeHost` (sims on a worker), `LifeLod` (caps, nearest-first), `OfficerPosts`. |
| `Aviation/` | `AircraftMesher`, `RunwayMesher`, `AircraftAudio` (engine-free), `AviationPresenter`. |
| `Rendering/RenderStats.cs` | The HUD counters. |
