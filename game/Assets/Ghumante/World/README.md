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

## Rendering

| Shader | Use |
|---|---|
| `Ghumante/ToonLit` | Terrain, buildings, roads, areas: vertex-colour albedo (sRGB palette, linearised), 3-band toon ramp, cool shadow tint, soft rim in the light colour, SH ambient, main-light shadows, URP fog, earth curvature `y -= d²/(2·R_eff)`, `_ViewPull` + polygon offset for overlays. ForwardLit, ShadowCaster, DepthOnly, DepthNormals. |
| `Ghumante/SkyGradient` | Skybox from `WorldSky` globals: zenith/horizon/ground, sun and moon discs, sunrise glow, haze blend at the horizon. |
| `Ghumante/RouteRibbon` | Transparent animated chevron ribbon with the same curvature. |

All are SRP Batcher compatible (per-material properties in `UnityPerMaterial`). Project Setup creates the materials in
`Assets/Ghumante/Settings/Materials/` and `Assets/Ghumante/Settings/Resources/GhumanteWorldMaterials.asset`, which keeps
them in builds; tune them there (Project Setup does not overwrite existing values).

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
| `Rendering/` | `WorldMaterialSet`, `WorldShaders`, `EarthCurvature`. |
| `Shaders/` | The three shaders and their includes. |
| `Debug/` | Development-only: `FreeFlyCamera`, `WorldDebugHotkeys`, `WorldPreview`. |
| `Editor/` | `WorldSetup`: materials for Project Setup, the World Preview menu. |
| `Cameras/CameraFov.cs` | Orientation-aware FOV maths for camera rigs (M0). |
