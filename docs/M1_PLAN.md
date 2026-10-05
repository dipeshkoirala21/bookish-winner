# M1 plan: Kathmandu Valley vertical slice

> Working plan for [ROADMAP M1](ROADMAP.md#m1-vertical-slice-kathmandu-valley-size-l-about-8-weeks). It complements ARCHITECTURE.md §5–§10, which says *what* the runtime is; this file says *in which order we build it*, *who owns which files*, and the *code contracts* parallel work is built against.
> **M1 acceptance (unchanged):** on a mid-range phone, search "Boudha", ride a motorbike there along real roads, in portrait and landscape; frame rate and memory within ARCHITECTURE §10.

## Principles

1. **Engine-agnostic first.** Everything that can be pure C# lives in `Ghumante.Core` and is unit-tested with `dotnet test`, including against a *real* region pack. This covers streaming selection, meshing (terrain, roads, buildings, areas), ground queries and the arcade vehicle model. Unity code is thin glue: uploading meshes, MonoBehaviours, input, rendering. This is how we keep moving without Unity in CI. The glue is compile-checked and API-audited against Unity 6000.3, and the product owner tests it in the editor.
2. **Playable early.** Wave 1 ends with Explore working on a Mac: real terrain, roads and buildings, a motorbike and walking, search and teleport, and a route line. Fidelity comes after it is playable.
3. **Works out of the box.** A compact sample region, `kathmandu_core`, is committed under `shared/sample-regions/` and copied into `StreamingAssets` by Project Setup. It covers Swayambhunath, Thamel, Durbar Square, Pashupatinath and Boudhanath, with the valley horizon. The full valley comes from the pipeline: run `python build.py --region kathmandu_valley`, then use **Ghumante → Import Region Pack**.

## Waves

| Wave | Goal | Work items (ROADMAP ids) | Status |
|---|---|---|---|
| **W1: Explore works** | Real 1:1 Kathmandu streamed around a motorbike or a walker. Search places, teleport, see a route line, back to menu. Both orientations, keyboard/gamepad on Mac, touch on phones. | 1.2 pack loading + LOD rings + floating origin; 1.3 terrain (skirts, biome toon colours); 1.4 roads (ribbons, surfaces, bridges); 1.5 buildings as extruded blocks with roof shapes; 1.7 walk + motorbike, arcade surface grip, stuck recovery, orientation-aware camera, touch controls; 1.9 sky gradient + day/night + valley haze + earth curvature; 1.10 search → teleport + route line; 1.13 debug HUD + teleport | **next** |
| **W2: Kathmandu comes alive** | Traffic and cows, landmarks, a real map. | 1.8 lane graph, cars, bikes, buses on OSM bus routes, cows (stop and steer, never harm); 1.6 hero landmark placeholders (stupa, pagoda, square), placed from `landmarks.resolved.json`; 1.5 Newar and Modern Urban facade details (LOD0 kit-lite); 1.10 full-screen map (true 1:1), minimap, fast travel to discovered places; 1.12 save of position and discoveries; taxi | later |
| **W3: Ship quality** | Performance and polish. | 1.14 instancing and batching, memory caps per tier, Adaptive Performance; 1.11 HUD and settings polish; 1.15 cultural review list; device test pass (iPhone 12, SD 7-series, 3 GB Android) | later |

## W1 work split

Agents never edit the same files at the same time. Arrows show dependencies.

| Track | Owns | Depends on |
|---|---|---|
| **A: Core streaming + meshing** | `Core/Streaming/**`, `Core/Meshing/**`, `core-tests/Streaming*.cs`, `core-tests/Meshing*.cs` | — |
| **B: Core driving** | `Core/Driving/**`, `core-tests/Driving*.cs` | — |
| **C: World runtime** | `World/**` (streamer, tile views, floating origin, shaders, materials, sky/time), `Platform/Regions/**` (built-in region source), `Editor/RegionImport*.cs` (+ the Project Setup hook), `shared/sample-regions/**` | A |
| **D: Explore gameplay** | `Characters/**`, `Vehicles/**`, `UI/Screens/Explore*`, `UI/Hud/**`, `App/**` (Explore flow), input | A, B, C |

## Contracts (namespaces, names, semantics)

Frames: all positions are **game metres** (ARCHITECTURE §5.1: X east, Z north, Y up, 1:1). Tile-relative mesh positions are metres from the tile's south-west corner (`TileId.X0/Z0`), with **absolute** Y.

```csharp
// ---- Ghumante.Core.Streaming --------------------------------------------------------------
public struct LodRing { public int Level; public double RadiusM; }          // finest level first
public sealed class StreamingConfig {
    public LodRing[] Rings;            // e.g. {10,1250},{9,4000},{8,10000},{7,30000},{6,80000},{5,200000}
    public int MaxResidentTiles; public long CacheBudgetBytes;
    public static StreamingConfig ForTier(int tier);   // 0 Low, 1 Mid, 2 High (ARCHITECTURE §7.2)
}
public sealed class TileSelector {
    public TileSelector(StreamingConfig config, Func<TileId, bool> exists);
    // CDLOD-style descent from the coarsest existing level. A tile is split when its closest point
    // to (x,z) lies within the radius of the next finer ring AND all four children exist.
    // Output covers the area exactly once (no overlaps or holes where data exists). Neighbours
    // differ by at most one level wherever data allows. Deterministic order: by distance, then key.
    public void Select(double x, double z, List<TileId> result);
}
public sealed class TileLoadPlanner {             // diff desired vs resident, prioritised
    public void Plan(IReadOnlyList<TileId> desired, ICollection<TileId> resident,
                     double x, double z, List<TileId> toLoad, List<TileId> toUnload);
}
public sealed class LruByteCache<TKey, TValue> { /* Get/Put/Remove; evicts to a byte budget */ }

// ---- Ghumante.Core.Meshing ---------------------------------------------------------------------
public sealed class MeshData {                    // reusable, grow-only buffers
    public float[] Positions; public float[] Normals; public byte[] Colors; /* RGBA32 */ public float[] Uv0;
    public int[] Indices; public int VertexCount; public int IndexCount; public void Clear();
}
public interface IHeightSampler { bool TryHeight(double x, double z, out float h); }   // game metres
public sealed class TileHeightSampler : IHeightSampler { public TileHeightSampler(TileData t); } // bilinear
public static class BiomePalette { public static uint Rgba(Biome b); }       // toon palette, ASSET_MANIFEST §2
public static class TerrainMesher {                 // grid from HGHT; skirts hide LOD cracks
    public static void Build(TileData t, TerrainOptions o, MeshData m); }   // o: Step (decimation), SkirtDepthM
public static class RoadMesher {                    // ribbons; widths by class; surface colours; bridges
    public static void Build(TileData t, IHeightSampler h, RoadOptions o, MeshData m); }
public static class BuildingMesher {                // extrusion; flat/gabled/hipped/pyramidal roofs; archetype colours
    public static void Build(TileData t, IHeightSampler h, BuildingOptions o, MeshData m); }
public static class AreaMesher {                    // water and land-use surfaces from pre-triangulated AREA
    public static void Build(TileData t, IHeightSampler h, AreaOptions o, MeshData m); }

// ---- Ghumante.Core.Driving -----------------------------------------------------------------------
public struct GroundSample { public float Height; public float Nx, Ny, Nz; public SurfaceGroup Surface;
                             public bool OnRoad; public RoadClass RoadClass; public Surface RoadSurface; }
public interface IGroundQuery { bool TrySample(double x, double z, out GroundSample s); }
public sealed class RoadSpatialIndex { public RoadSpatialIndex(TileData t);   // per-tile segment grid
    public bool TryNearest(double x, double z, double maxDistM, out RoadHit hit); }
public struct DriveInput { public float Throttle, Brake, Steer; public bool Boost; }   // [-1,1] / [0,1]
[Flags] public enum StepEvents { None = 0, SurfaceChanged = 1, Bump = 2, Landed = 4, StuckRecovered = 8, OffRoad = 16 }
public sealed class VehicleSpec { /* MaxSpeedKmh, AccelMps2, BrakeMps2, SteerDegPerSec, WheelbaseM,
    Grip[SurfaceGroup], TopSpeedFactor[SurfaceGroup], MaxSlopeDeg ... */
    public static VehicleSpec Motorbike(); public static VehicleSpec Walker(); public static VehicleSpec Taxi(); }
public sealed class ArcadeVehicle {                 // kinematic bicycle model, terrain-following
    public ArcadeVehicle(VehicleSpec spec);
    public double X, Z; public float Y, HeadingRad, SpeedMps, Pitch, Roll; public SurfaceGroup Surface;
    public void Teleport(double x, double z, float headingRad, IGroundQuery g);
    public StepEvents Step(in DriveInput input, float dt, IGroundQuery g, float wetness01);
}

// ---- Ghumante.World (Unity) -------------------------------------------------------------------
// WorldRoot (MonoBehaviour): Task OpenRegionAsync(string regionId); WorldPos Focus { get; set; };
//   IGroundQuery Ground { get; }; float TimeOfDayHours { get; set; }; event Action<WorldPos> OriginShifted;
//   SearchEngine Search { get; }; RouteGraph Routes { get; }; bool IsReady { get; }
// WorldStreamer: decode + mesh on worker threads (no Unity API off the main thread), upload on the main
//   thread within a per-frame budget (2 ms at 30 fps, 1.5 ms at 60 fps); pooled TileView objects.
// Shaders: "Ghumante/ToonLit" (vertex colour, ramp, main light + SH, fog, earth-curvature vertex drop),
//   "Ghumante/SkyGradient". SRP-Batcher compatible. Materials are created by ProjectSetup.
```

## Verification for every wave

* `dotnet test core-tests` passes, including the real-pack tests on `shared/sample-regions/kathmandu_core`: meshing every tile without exceptions, seam checks on terrain edges, selection covering exactly once.
* `tools/unity-compile-check/run.sh --audit` and `check_ui.py` pass, as do the localisation check and the pipeline tests.
* An adversarial review pass covers runtime correctness, shaders, UX in both orientations and data correctness, followed by fixes.
* The product owner plays it in the Mac editor (PROGRESS.md lists exactly what to try).
