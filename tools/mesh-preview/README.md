# Mesh preview (visual self-check)

Unity cannot run here, so every package that generates geometry checks it with this software renderer:
dump the `MeshData` to OBJ from a test, render PNGs, **look at them** (Read the PNG) and iterate until the result is
clearly detailed, rounded and correct (docs/W2_DETAIL_CONTRACT.md §6). numpy + Pillow only.

```
python3 tools/mesh-preview/render.py in.obj -o out.png                      # front, 3/4 and top views
```

While the preview package is in flight use `/home/user/wt/preview/tools/mesh-preview/render.py`; save your PNGs under
`/home/user/wt/previews/<package>/`.

## 1. Dump meshes from a test (`core-tests/Support/ObjDump.cs`)

```csharp
var m = new MeshData();
MyMesher.Build(..., m);
ObjDump.Write(m, "mypackage/thing.obj");                     // no-op unless GHUMANTE_PREVIEW_DIR is set
ObjDump.Write("mypackage/parts.obj", new ObjPart("body", body), new ObjPart("wheels", wheels)); // OBJ groups
ObjDump.Write(m, "mypackage/thing.obj", new[] { "meshpreview: views=4 sun=160,60" });          // render defaults
```

* Writes only when `GHUMANTE_PREVIEW_DIR` is set (ordinary test runs write nothing); relative paths go under it.
  `ObjDump.Write` returns the full path, or null when disabled.
* Format: `v x y z r g b` (albedo tint 0..1), `vn`, `vt u v` when the mesh has UV0 (u = `MaterialChannel`,
  v = baked AO, contract §5), one `o` group per part.
* **Coordinates**: Unity is left-handed (x east, y up, z north); OBJ is right-handed, so ObjDump negates z and reverses
  the winding. In the OBJ, north is **-Z**. Pass `--unity` to give `--eye/--look/--target/--clip` in Unity
  coordinates.
* Helpers: `ObjDump.Append(dst, src, dx, dy, dz)` (assemble pieces, e.g. a body and its wheels),
  `ObjDump.Crop(m, x0, z0, x1, z1, inside)` (keep or drop triangles by centroid), `ObjDump.Format(...)` (OBJ text, no
  disk).

Run the dumps:

```
cd /home/user/wt/<package>
GHUMANTE_PREVIEW_DIR=/home/user/wt/previews/<package>/obj \
  bash /home/user/wt/slot.sh /root/.dotnet/dotnet test core-tests --filter "FullyQualifiedName~MyDumpTest"
```

### Ready-made dumps (`core-tests/Support/PreviewDumps.cs`)

```
GHUMANTE_PREVIEW_DIR=/home/user/wt/previews/<package>/obj \
  bash /home/user/wt/slot.sh /root/.dotnet/dotnet test core-tests --filter "FullyQualifiedName~PreviewDumps"
```

* `scene/tile_<tx>_<ty>/`: one level-10 tile (1024 m) of `shared/sample-regions/kathmandu_core` as the player sees it:
  `terrain`, `roads`, `decals`, `areas`, `heroes`, `buildings` (B0 detail band within `GHUMANTE_PREVIEW_RADIUS`,
  default 150 m, of the eye) and `buildings_b1` (B1 band beyond), each a separate OBJ. Every file carries a
  street-level chase-camera eye point over the widest street near the tile centre
  (`# meshpreview: eye=... look=...`), and `render.sh` holds the exact render commands. Default tile: the densest
  (Asan, Indra Chowk); `GHUMANTE_PREVIEW_TILE=516,161` picks another level-10 tile (`tx,ty`).
* `vehicles/<asset>_<variant>.obj`: every catalogue vehicle at LOD0 with its wheels.
* `characters/*.obj`: player (topi, helmet) and pedestrian archetypes at LOD0.
* `heroes/<id>.obj`: every stage-one hero monument at LOD0.

Filter to one of them: `--filter "FullyQualifiedName~PreviewDumps.DumpSampleTileScene"` or `...DumpSampleObjects`.

## 2. Render

```
R=tools/mesh-preview/render.py
python3 $R thing.obj -o thing.png                       # 3 views: front, 3/4, top (default)
python3 $R thing.obj -o thing.png --views 4             # front, right, 3/4, top (1..6: + back, 3/4 back)
python3 $R thing.obj -o thing.png --views front,34,left,top,30:15   # presets and az:el pairs
python3 $R thing.obj -o thing.png --az 120 --el 20 --dist 8        # one orbit view at 8 m
python3 $R thing.obj -o thing.png --size 1200 --height 800          # panel size (px); --ssaa 2 default
python3 $R a.obj b.obj c.obj -o outdir/                 # one sheet per file
python3 $R --sheet out/*.obj -o contact.png --size 360  # contact sheet: a labelled cell per file
python3 $R --sheet a.obj b.obj --views front,34 -o c.png   # contact sheet: a row per file
```

Street level and scenes:

```
D=/home/user/wt/previews/<package>/obj/scene/tile_516_161
python3 $R --combine $D/terrain.obj $D/roads.obj $D/decals.obj $D/areas.obj $D/buildings.obj \
    $D/buildings_b1.obj $D/heroes.obj -o street.png --size 1200 --height 700 --fog 350   # eye from the files
python3 $R --combine $D/*.obj -o mine.png --eye 470 1308 -500 --look 480 1307 -540   # your own eye (OBJ coords)
python3 $R --combine $D/*.obj -o mine.png --unity --eye 470,1308,500 --look 480,1307,540   # same, Unity coords
python3 $R --combine $D/*.obj -o over.png --no-eye --views bird --size 1200    # orbit view of the whole tile
python3 $R --combine $D/*.obj -o near.png --radius 120 --fog 300    # only triangles within 120 m of the eye
```

Views, camera and look:

| Option | Meaning |
|---|---|
| `--views N` or list | layouts 1..6 or presets `front back right left 34 34back 34left top street bird` and `az:el` |
| `--az --el --dist --target x,y,z --zoom` | orbit camera; az = bearing of the camera seen from the target, clockwise from north (az 0 looks at the front of a Unity +Z-forward object); el above the horizon; auto-framed unless `--dist` |
| `--eye x y z --look x y z` | free perspective camera (also `x,y,z`); an `eye=`/`look=` directive in the OBJ does the same; `--no-eye` ignores it |
| `--unity` | `--eye/--look/--target/--clip` are Unity coordinates (z north) |
| `--fov 30`, `--ortho` | vertical field of view; orthographic projection |
| `--shading toon` | default: the ToonLit 3-band ramp, cool shadow tint, trilight ambient, rim light; `lambert`, `unlit`, `normals`, `channels` (false colour per material channel with a legend), `ao` (baked AO in grey) |
| `--no-materials` | ignore UV0 (no channel patterns, no AO) |
| `--sun az,el` | sun bearing and elevation (default 330,50; scene dumps use 160,62) |
| `--no-shadows --shadow-res 2048` | shadow map (street views fit it around the eye for sharp shadows) |
| `--fog M` | distance fog reaching 63 % at M metres |
| `--no-outline --outline-width 1` | black cartoon outlines from depth and normal edges |
| `--no-grid` | ground grid at the lowest point (`grid=0` directive for scenes) |
| `--wire` | hidden-line wireframe overlay |
| `--backfaces cull/show/highlight` | `highlight` paints back faces magenta: wrong winding shows at once |
| `--exploded 0.6` | push groups (or connected pieces) apart |
| `--group text` / `--clip x0,y0,z0,x1,y1,z1` / `--radius R` | render a subset |
| `--pull 1e-5` | depth bias per group index (later groups win near-coplanar ties) |
| `--stats` | JSON: counts, size, groups, smooth-shaded area %, inverted normals %, facet directions, components |
| `--swatch out.obj` | write and render the material swatch board (every channel's preview pattern with AO) |

Directives: a comment line `# meshpreview: key=value ...` in the OBJ sets defaults the command line overrides
(`views`, `shading`, `flat`, `backfaces`, `grid`, `sun`, `pull`, `ortho`, `fog`, `eye`, `look`).

## 3. Material channels and AO

Meshes with UV0 (contract §5) render each `MaterialChannel` with a simple procedural pattern in world space (box
projection, metres): plaster blotches, brick running bond with mortar, glazed brick, wood grain and planks, carved
rosettes, jhingati roof tiles (scalloped rows down the slope), brushed metal, gilt sparkle, ashlar stone, asphalt
speckle, concrete joints, grass, foliage clumps, bark furrows, fabric weave, glass reflection, water ripples, dirt,
flagstones, hair strands, leather, worn markings. Patterns fade to their average where they would be sub-pixel. AO
(v) darkens the colour (`0.3 + 0.7 * ao`). Use `--shading channels` to check the channel assignment (legend under the
panels) and `--shading ao` to check the bake. `python3 tools/mesh-preview/render.py --swatch /tmp/swatch.obj -o
swatch.png --views 3/4` shows every channel.

## 4. What to look for

* Boxiness: silhouettes should be curved (`--stats`: high `smooth_shaded_area_pct`, many `facet_directions_5deg`).
* Holes, flipped faces (`--backfaces highlight`), floating parts (`--stats` components, `--exploded`).
* Scale against the grid (label says the step) and against a character (`--combine thing.obj characters/porter.obj`).
* From street level: clearances, balconies over the road, corridor widths, what blocks the view.

## 5. Speed

Fully vectorised: a 200k-triangle mesh renders three 800 px views (2x supersampled, shadows, outlines) in about
10 s; a 500k-triangle street scene in about 12 s. Lower `--size` or `--ssaa 1` for quick iterations, or use
`--radius`/`--group` for big scenes.

## 6. Layout

`render.py` (entry), `meshpreview/objfile.py` (OBJ load and merge), `camera.py` (orbit framing, look-at, near
clipping), `raster.py` (bucketed z-buffer rasteriser), `renderer.py` (scene, shading, shadows, ground, outlines,
fog), `materials.py` (channel patterns and palette), `compose.py` (sheets, contact sheets, legends), `stats.py`,
`swatch.py`, `cli.py`. Tests: `python3 -m pytest -q tools/mesh-preview/tests`.
