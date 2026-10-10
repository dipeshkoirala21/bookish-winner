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

Namespace `Ghumante.Core.Meshing.Shapes` (`game/Assets/Ghumante/Core/Meshing/Shapes/`), engine-free, deterministic, no allocation once its per-thread scratch has grown (safe on worker threads). Tests: `core-tests/MeshingShapesTests.cs`; showcase of every primitive: `core-tests/MeshingShapesShowcase.cs` (set `GHUMANTE_SHAPES_OBJ=<dir>` to dump OBJs; renders in `/home/user/wt/previews/shapes/`).

**Conventions.** Every emitter appends to a `MeshData` through an `Affine3` and a `ShapeBrush`, returns the index of its **first vertex** (its vertices run to `m.VertexCount`, handy for colour/AO/noise passes), writes **Unity winding** (front = `cross(b - a, c - a)`, decided against the analytic normals, so mirroring transforms and `KitFrame`s are safe), smooth normals unless a crease is asked for, and **always UV0** (`u = (float)brush.Channel`, `v = brush.Ao`, see §5). Boxes, spheres, superellipsoids and tori are **centred** on the origin; cylinders, cones, frusta, capsules, domes, lathes and extrudes **stand on y = 0 along +Y**. Closed solids are watertight (welded by position) and have positive volume. Every primitive takes its LOD0 segment counts plus `ShapeLod lod = default` and scales them itself.

| Type | What |
|---|---|
| `Affine3` | 3×4 affine transform. `Identity`, `Translation`, `Scaling(s)`/`Scaling(x,y,z)`, `RotationX/Y/Z(rad)`, `RotationAxis`, `Yaw(deg)` (compass: +Z → bearing), `FromBasis`, `FromKitFrame(KitFrame)`, `Along(a, b, out len)` (+Y from a to b), `a * b` (b first), `a.Then(b)` (a first), `ThenTranslate`, `Inverse`, `Determinant`, `Mirrors`, `Point`, `Vector`, `Normal`. |
| `ShapeBrush` | `new ShapeBrush(0xRRGGBBAA, MaterialChannel.Wood, ao: 1f)`, `ShapeBrush.Hex(0xRRGGBB, ch)`, `WithColor/WithChannel/WithAo`. |
| `ShapeLod` | `ShapeLod.Lod0/Lod1/Lod2` (or `new ShapeLod(level)`): `Radial(n)` (1, 1/2, 1/4; min 3, LOD2 keeps 6 for n ≥ 12), `Bevel(n)` (min 1 = chamfer), `Path(n)` (min 1). |
| `Profile2` | Reusable 2D polyline/polygon: `Clear(closed)`, `Add(x, y, crease)`, `Add(x, y, nx, ny, crease)` (explicit normal), `SetCircle`, `SetEllipse`, `SetRect`, `SetRoundedRect(w, h, r, seg)`, `SetRegular`, `SetPoints`, `FilletCorners(r, seg, all)`, `Smooth(segPerSpan)` (Catmull-Rom between creases), `Transform`, `Reverse`, `SignedArea`, `Length`. Outward = right of travel (closed: CCW; lathe: bottom → top with the outside at +x); clockwise closed profiles are detected. |
| `Path3` | Reusable 3D polyline: `Clear`, `Add`, `AddDistinct`, `CopyFrom`, `Length`. |
| `Curves` | `CatmullRom(ctrl, dst, segPerSpan, closed)` (centripetal), `Bezier(dst, p0..p3, seg)`, `ArcXZ(dst, cx, y, cz, r, startDeg, sweepDeg, seg)`, `Fillet(src, dst, r, seg, closed)` (round polyline corners), `FilletCorner(...)` (2D), `Catenary(dst, a, b, sag, seg)`, `Resample(src, dst, spacing)`. All append to `dst`. |
| `Shapes` | The primitives below. |
| `ShapeColor` | `VerticalGradient(m, first, count, y0, y1, bottom, top)`, `VerticalShade(..., bottomFactor, topFactor)`, `Tint`, `JitterByPosition(m, first, count, amount, seed, cell)` (seam-safe), `JitterFaces(m, firstIndex, indexCount, amount, seed, trisPerFace)` (flat-shaded tiles, planks). Alpha kept. |
| `ShapeAo` | `Bake(m, firstVertex, vertexCount, firstIndex, indexCount, in AoSettings)` multiplies into `Uv0.v` (-1 counts = to the end): ground contact, undersides, concavity, optional few-ray test (`Rays`, `RayDistance`, `RayBudget`) for small meshes. `ShapeAo.Defaults(groundY)`, `EnsureUv(m)`. |
| `ShapeNoise` | `Hash(x, y, z, seed)` (FNV-1a + avalanche), `Unit`, `Value3`, `Gradient3`, `Fbm3` (all in [-1, 1]), `Displace(m, first, count, amplitude, frequency, seed, octaves)` along normals, `RecomputeNormals(m, firstV, countV, firstI, countI, weld)`. |

Primitives (`m, xf, brush` first, then):

| Call | Notes |
|---|---|
| `RoundedBox(sx, sy, sz, radius, segments, lod)` / `RoundedBox(sx, sy, sz, in BoxRadii, segments, lod)` | Flat faces are single quads; `segments` per 90° edge arc. `BoxRadii(xNeg, xPos, yNeg, yPos, zNeg, zPos)`, `BoxRadii.All(r)`, `BoxRadii.Vertical(side, top, bottom)`: unequal radii give elliptical edges, ~0 keeps a side crisp (flat-bottomed seat). |
| `Superellipsoid(rx, ry, rz, eVertical, eHorizontal, segments, lod)` | Cube-sphere grid with signed-power mapping (no poles). e = 1 round, 0.2-0.4 boxy cartoon car body/cushion/head, 2 diamond. |
| `CubeSphere(r, segments, lod)`, `Sphere(r, segments, lod)` (UV), `Ellipsoid(rx, ry, rz, segments, cubeSphere, lod)`, `Dome(r, height, segments, capBottom, lod)` | |
| `Lathe(profile, radialSegments, lod, startDeg, sweepDeg, endCaps)` | Revolve about +Y; creased points make hard rings; partial sweeps can cap their cut faces. |
| `Cylinder(r, h, radial, rimRadius, rimSegments, capBottom, capTop, lod)`, `Cone(...)`, `Frustum(rBottom, rTop, h, ...)` | Rounded rims where caps meet the side. |
| `Capsule(r, height, radial, lod)` | Use `Affine3.Along` to span two points. |
| `Torus(R, r, majorSeg, minorSeg, lod, startDeg, sweepDeg, endCaps)` | Partial torus with caps: arches, mudguards, garlands. |
| `BevelExtrude(x[], z[], n, height, bevel, bevelSegments, style, capTop, capBottom, bottomBevel, creaseDeg, lod)` | Any simple polygon (either orientation); `BevelStyle.Round` or `Chamfer`; polygon corners sharper than `creaseDeg` stay hard. No holes (open issue). |
| `RoundedSlab(sx, sz, height, cornerRadius, bevel, segments, lod)` | Rounded-rect plan + bevelled top: table tops, steps, plinths. |
| `Sweep(path, section, closedPath, caps, frames, scaleStart, scaleEnd, twistDeg)` | Any 2D section along a 3D path; `SweepFrames.ParallelTransport` (pipes, tails) or `Upright` (railings, kerbs); sharp path corners are mitred; tapers and twist; capped ends for closed sections. |
| `Tube(path, r, radial, caps, closedPath, lod, radiusEnd)`, `Bar(a, b, r, radial, lod)`, `Wire(a, b, sag, r, segments, radial, lod)` | Round tubes; `Wire` hangs a catenary. |
| `Loft(profileA, frameA, profileB, frameB, segments, capA, capB, lod)` | Each profile in the XY plane of its frame; resampled by arc length if counts differ; start points auto-aligned (no twist). |
| `CopyTransformed(m, src, xf, tint, firstVertex, vertexCount, firstIndex, indexCount)` | Instance a part (even from `m` itself) with a transform and RGB tint; mirrors flip winding. |

Examples:

```csharp
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

var m = new MeshData();
var red = ShapeBrush.Hex(0xD7263D, MaterialChannel.Paint);
var lod = new ShapeLod(lodLevel);                       // 0, 1, 2
Affine3 car = Affine3.Translation(x, y, z) * Affine3.Yaw(headingDeg);

// Cartoon car body + glasshouse (superquadrics), four tyres (tori) with hubs.
int body = Shapes.Superellipsoid(m, car * Affine3.Translation(0, 0.62, 0), red, 0.85, 0.32, 1.6, 0.25, 0.3, 40, lod);
Shapes.Superellipsoid(m, car * Affine3.Translation(0, 1.0, -0.15), ShapeBrush.Hex(0xBFE3F2, MaterialChannel.Glass), 0.72, 0.3, 0.95, 0.35, 0.35, 32, lod);
Affine3 wheel = car * Affine3.Translation(-0.78, 0.34, 1.0) * Affine3.RotationZ(Math.PI / 2);
Shapes.Torus(m, wheel, ShapeBrush.Hex(0x23262B, MaterialChannel.Rubber), 0.24, 0.11, 24, 10, lod);

// Railing: mitred, upright handrail along a filleted polyline, posts instanced.
Path3 rail = Curves.Fillet(new Path3().Add(0, 1, 0).Add(4, 1, 0).Add(6, 1, 2), new Path3(), 0.3, lod.Bevel(4));
Shapes.Sweep(m, Affine3.Identity, metal, rail, new Profile2().SetRoundedRect(0.08, 0.06, 0.02, lod.Bevel(3)), false, true, SweepFrames.Upright);
int f = m.VertexCount, fi = m.IndexCount;
Shapes.Cylinder(m, Affine3.Identity, metal, 0.03, 0.97, 10, 0.01, 2, true, true, lod);
int nv = m.VertexCount - f, ni = m.IndexCount - fi;
for (int i = 1; i < 8; i++) Shapes.CopyTransformed(m, m, Affine3.Translation(0.5 * i, 0, 0), 0xFFFFFFFFu, f, nv, fi, ni);

// Kalash / vase: a few control points, smoothed, revolved.
var vase = new Profile2().Add(0, 0, true).Add(0.35, 0, true).Add(0.5, 0.3).Add(0.45, 0.6).Add(0.22, 0.9).Add(0.3, 1.15, true).Add(0, 1.15, true);
Shapes.Lathe(m, xf, ShapeBrush.Hex(0xB5651D, MaterialChannel.Dirt), vase.Smooth(lod.Path(6)), 32, lod);

// Rock / canopy: displaced cube sphere, seam-safe normals, patchy colour.
int r0 = m.VertexCount, ri = m.IndexCount;
Shapes.CubeSphere(m, xf * Affine3.Scaling(1.1, 0.7, 0.9), ShapeBrush.Hex(0x8A8378, MaterialChannel.Stone), 1, 32, lod);
ShapeNoise.Displace(m, r0, m.VertexCount - r0, 0.22, 1.3, seed, 3);
ShapeNoise.RecomputeNormals(m, r0, m.VertexCount - r0, ri, m.IndexCount - ri);
ShapeColor.JitterByPosition(m, r0, m.VertexCount - r0, 0.12f, seed, 0.3);

// Finally bake AO for the whole object standing on groundY.
ShapeAo.Bake(m, 0, -1, 0, -1, ShapeAo.Defaults(groundY));
```

Rules of thumb: prefer `RoundedBox`/`Superellipsoid` over `MeshKit.Box` for anything the camera sees up close; use creases (profile `crease: true`, `BevelStyle.Chamfer`) only where a real hard edge exists; build one `Profile2`/`Path3` per mesher and reuse it; call `ShapeAo.Bake` once per object after all parts are in (the ray term only for small props: `Rays = 8`, it is skipped above `RayBudget`).

## 5. Material channels and AO (for the look package's shader)

`Core/Meshing/MaterialChannel.cs` (*stub*). Every mesher in this pass writes `MeshData.Uv0` (set `HasUv0`): **u = `(float)MaterialChannel`**, **v = baked ambient occlusion** (1 = open, 0 = fully occluded; bake cheaply: concave corners, undersides, ground contact, inner faces of deep openings). The vertex colour stays the albedo tint; its alpha keeps its current meaning (instance tint weight). Meshes without UV0 render as `Plain` with no AO. The **look** package generates a procedural texture per channel at runtime (brick, wood grain, jhingati tiles, asphalt wear, stone, plaster, grass, foliage, bark, fabric weave...), samples it triplanar in world or object space, multiplies by the tint and AO, and adds rim light and outlines.

## 6. Working rules for every package

* Work only in your worktree `/home/user/wt/<package>` (create it with `bash /home/user/wt/new.sh <package> <base-sha>`); never edit `/home/user/bookish-winner` directly.
* Wrap every heavy command in the shared build-slot limiter: `bash /home/user/wt/slot.sh /root/.dotnet/dotnet test core-tests --filter ...`, `bash /home/user/wt/slot.sh bash tools/unity-compile-check/run.sh --audit`, `bash /home/user/wt/slot.sh python3 -m pytest ...`. Up to 15 agents share 4 CPUs. Iterate with filtered tests and run the full gates once at the end.
* Visual self-check is mandatory for anything you generate: dump the mesh (`ObjDump`) and render it with the preview tool (`/home/user/wt/preview/tools/mesh-preview/render.py` while the preview package is in flight, `tools/mesh-preview/render.py` after it merges; see its README), look at the PNGs and iterate until the result is clearly detailed and not boxy.
* Finish with every gate green in your worktree and one commit on `w2d/<package>` ending with the attribution lines the lead gives you.
