# W2 detail pass: contract for parallel packages

> The owner played Wave 2 Stage 1 and asked for a large detail and rideability pass (quoted in §0). The work is split into **packages** that run in parallel, each on its own git worktree and branch (`w2d/<package>`), and are merged by the lead. This file is the contract between them: decisions, ownership, and the shared interfaces. Interfaces marked *stub* already exist in the base commit so every package compiles against them from the start.

## 0. Owner feedback (verbatim)

"Fix the design.. Everything looks so boxy. Add more details to everything. Trees, flowers, animals, bikes, cycles, cars, trucks, tractors, Hills, Cities.. Make temples more Detailed and 3D. Camera view gets blocked if i Try to back up my motorbike in the narrowers streets. Houses blocks the view, and sometimes the motorbikes can pass through the houses. Add multiple camera angles, for Bike, Cycles, Cars, Bus. Add busses.. I know i said Cartoonish, but I did not mean this level of boxy. Roads have sharp corners.. Some of the roads are too narrow, the characters cannot even pass through. Some of the houses have way too low balcony.. I cannot even ride the scooter pass through it. It blocks the way.. Some of the houses are in the roads.. Add details to characters.. Make proper face, hand, feet.. Give detail to Nepali Topi. .. Add details to motorcycle. It looks soo boxy right now. Add details other person walking by.. Make roads smoother. Make it wide.. Even On the narrower streets on kathmandu, atleast 3 bikes can pass through easily. Fix that. If there is a road passing from the river, make sure to design it as a Proper bridge with Railings. If there is a road going from the underneath the bridge, add more clearance so the the user can pass through.. Right now, it gets stuck. Remove the crowd noise in the background. Its annoying. Find wherever there is an overpass in the city and design that. Make roundabouts more detailed. Add statues in the middle of the roundabout wherever application. Do an indepth research on how it looks and try to design it. Basically, Make everything detailed. Make roads verymuch rideable. If i am on a car, avoid the roads that has narrowers streets through which cars cannot pass through. Place direction info somewhere else where its convenience to look. Right now it blocks the view."

## 1. Decisions (binding)

1. **Rideability beats exact footprints.** Every drawn road has a clear corridor of at least `RoadClearance.MinCorridorM` = 4.8 m (three motorbikes side by side plus margins), widened from the real width. A building that intrudes into a corridor is **trimmed** back to the corridor edge (pipeline does it so every consumer agrees; runtime guards too). Roads never run through houses; houses never stand in roads.
2. **Overhead clearance.** Nothing overhangs a road corridor below `RoadClearance.MinOverheadClearanceM` = 4.5 m (balconies, eaves, struts, signs, wires). Projections are raised or clipped.
3. **Bridges.** Every road over water is a real bridge: deck, kerbs, railings on both sides, abutments, piers on long spans. Every road under a bridge or flyover has at least `RoadClearance.MinUnderpassClearanceM` = 5.5 m; ground queries pick the right level, so nothing gets stuck.
4. **Flyovers.** Every flyover, overpass, underpass and pedestrian overbridge in the valley is modelled (decks, ramps, piers, railings, lamp posts).
5. **Car routing.** Car routes and AI cars avoid streets a car cannot use (galli, real width under about 3 m, footway, path, steps, pedestrian, `motorcar=no`). Motorbikes and bicycles may use them.
6. **Look.** Cartoon, but rounded and rich: bevelled and rounded edges, curved surfaces (cylinders, spheres, lathes, sweeps) instead of boxes, smooth shading, procedural textures generated at runtime, baked vertex AO, cartoon outlines, many small details. Budgets hold through LOD (raise per-object caps where needed).
7. **Audio.** The crowd walla bed is removed (off by default). Footsteps, engines, horns, bells and aircraft stay.

## 2. Packages and ownership

A file belongs to exactly one package. Need a change elsewhere? Record it as an open issue for the lead or the integration package. Localisation JSONs: append keys only (the lead merges). Every package adds its own tests (`core-tests/` for Core, `game/Assets/Ghumante/Tests/EditMode/` for Unity).

| Package | Wave | Owns |
|---|---|---|
| **shapes** | A | `Core/Meshing/Shapes/**` (new), `core-tests/MeshingShapes*` |
| **preview** | A | `tools/mesh-preview/**`, `core-tests/Support/ObjDump.cs` (and the csproj include for `Support/*.cs` if missing) |
| **data** | A | `pipeline/**`, `shared/**`, `docs/DATA_FORMATS.md`, `Core/Data/**`, `Core/Routing/**`, `core-tests/{Data,Tile,Golden,Enums,Routing,PackReader,BinReader}*` |
| **roads** | A | `Core/Meshing/Roads/**`, `Core/Meshing/RoadMesher.cs`, `core-tests/MeshingRoad*` |
| **bridges** | A | `Core/Meshing/Bridges/**`, `core-tests/MeshingBridge*` |
| **collide** | A | `Core/Driving/**` except the vehicle mesh files, `Core/Generators/GenColliders.cs`, `Core/Traffic/**`, `core-tests/{Driving,Traffic}*` except vehicle mesh tests |
| **camera** | A | `Characters/Cameras/**`, `Characters/Control/**`, `Characters/ExplorerController.cs`, `Characters/Rides/**`, `Core/Characters/CameraRigTable.cs`, `UI/**`, `App/**`, `game/README.md`, `core-tests/CharactersCamera*` |
| **look** | A | `World/Shaders/**`, `World/Rendering/**`, `World/Editor/**`, `Editor/ProjectSetup.cs`, `Audio/**`, `Core/Synth/**`, `World/README.md`, `Tests/EditMode/WorldRendering*`, `core-tests/Synth*` |
| **vehicles** | B | `Core/Driving/{VehicleMesher,VehiclePlates,PlateGlyphs}.cs`, `Vehicles/**`, `Traffic/TrafficPresenter.cs`, `core-tests/VehicleMesh*` |
| **characters** | B | `Core/Characters/**` except `CameraRigTable.cs`, `Characters/Avatar/**`, `Traffic/CrowdPresenter.cs`, `World/Instancing/{PeopleRenderer,PersonAnimation}.cs`, `core-tests/Characters{Mesh,Motion,Player}*` |
| **temples** | B | `Core/Generators/Sacred/**`, `World/Sacred/**`, `core-tests/GeneratorsSacred*` |
| **buildings** | B | `Core/Meshing/Buildings/**`, `Core/Meshing/{BuildingMesher,BuildingStyle}.cs`, `Core/Meshing/Kit/**`, `World/Buildings/**`, `core-tests/MeshingBuilding*` |
| **ornaments** | B | `Core/Generators/Ornaments/**` (new), `core-tests/GeneratorsOrnament*` |
| **nature** | B | `Core/Generators/Placement/**`, `Core/Generators/Flora/**` (new), `World/Instancing/{KitMeshes,DressingRenderer,DressingConfig}.cs`, `Core/Meshing/{TerrainMesher,TerrainGrid,BiomePalette,AreaMesher}.cs`, `core-tests/{GeneratorsPlacement,MeshingTerrain,MeshingArea,GeneratorsFlora}*` |
| **animals** | B | `Core/Generators/Fauna/**` (new), `Wildlife/**`, `core-tests/GeneratorsFauna*` |
| **integration** | C | `World/Streaming/**`, `World/Life/**`, `World/WorldRoot.cs`, `World/Instancing/{TileInstances,ParkedPlacement,InstanceBatch}.cs`, wiring across packages after the merges |

Wave B packages start from a base that already contains **shapes** and **preview**.

## 3. Shared interfaces

| Interface | Where | Implemented by | Used by |
|---|---|---|---|
| `RoadClearance` constants (*stub*) | `Core/Meshing/Roads/IRoadCorridorQuery.cs` | — | everyone |
| `IRoadCorridorQuery` (*stub*): `SignedDistance(x, z)`, `Overlaps(x[], z[], n, out depth)` | same file | **roads**: `RoadCorridorIndex.ForTile(TileData t)` (game metres, the same widened corridor the road mesher draws) | buildings (overhang clamp, footprint guard), nature/ornaments/props (keep out of roads), collide |
| `RoadStructureRecord`, `RoadStructureKind`, `RoadStructureFlags`, `TileData.RoadStructures`, `TileData.RoadStructureOf(i)` (*stub*) | `Core/Data/RoadStructures.cs`, `Core/Data/TileData.cs` | **data**: new tile chunk (name it, document it in DATA_FORMATS.md, reader in `Core/Data`), filled for every road: kind, layer, flags (CarAccessible, WaterCrossing...), clearance, railing height, absolute deck heights per point | roads (lowered underpass profile, skip draping on decks), bridges (deck geometry), collide (layered ground, car access for traffic), camera (route planner car profile), data (GHRG car mask) |
| `IBridgeDeckQuery` (*stub*): `TryDeck(x, z, nearY, out deckY, out normal)`, `TryCeiling(x, z, fromY, out undersideY)` | `Core/Meshing/Bridges/IBridgeDeckQuery.cs` | **bridges**: `BridgeDeckIndex.ForTile(TileData t)` | collide (layered ground, clearance), camera |
| `IViewObstacleQuery` (*stub*): `SphereCast(...)` | `Core/Driving/IViewObstacleQuery.cs` | **collide** (over structure colliders, decks and terrain) | camera (collision-aware chase camera) |
| `Shapes` library | `Core/Meshing/Shapes/**` | **shapes** (API documented in its README section of `docs/W2_DETAIL_CONTRACT.md` §4, appended by the shapes package) | every wave B package |
| `ObjDump.Write(MeshData, path)` and `tools/mesh-preview/render.py` | `core-tests/Support/ObjDump.cs` | **preview** | everyone (visual self-check) |

Until a stub's implementation lands, code against the interface and test with a small fake.

## 4. Shapes API

(Appended by the **shapes** package.)
