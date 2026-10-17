# The Kathmandu skyline: reference brief (package `sky`)

Owner playtest #12: "Add mountainous peaks in the far background". This brief records what is really on the skyline of
the Kathmandu Valley, how big it really looks from the valley floor, what the game drew at the playtest, and the model
the distant-mountains layer (`Core/Horizon`, `World/Sky/SkylineRing`, `Ghumante/Skyline`) follows. Photos are
references only (Wikimedia Commons, open licences; nothing is copied into the repository), kept under
`/home/user/wt/refs/sky/`; previews and side-by-side comparisons are in `/home/user/wt/previews/sky/`.

## 1. What is on the skyline

Two layers, as every photo from the valley shows:

* **The green rim** (10-20 km): forested ridges of 2 100-2 800 m, dark blue-green, softened by the valley haze.
  Summits located on the 30 m GLO-30 DEM, heights as published:

  | Hill | Lat, lon | Height | From Basantapur |
  |---|---|---|---|
  | Shivapuri | 27.8124, 85.3846 | 2 732 m | bearing 32°, 14.2 km |
  | Phulchoki | 27.5710, 85.4068 | 2 782 m | 146°, 17.7 km |
  | Chandragiri | 27.6663, 85.2053 | 2 551 m | 247°, 10.9 km |
  | Nagarjun (Jamacho) | 27.7453, 85.2593 | 2 128 m | 313°, 6.6 km |

* **The Himalaya** (60-125 km, north and north-east): snow peaks of 6 600-7 400 m that rise just above the rim.
  Coordinates from Wikipedia unless noted; all in `HorizonPeaks`:

  | Group | Peaks (height) |
  |---|---|
  | Ganesh Himal (NNW) | Yangra / Ganesh I 7 422, Ganesh II 7 118, Salasungo / Ganesh III 7 043, Pabil / Ganesh IV 7 104 |
  | Langtang (NNE) | Langtang Lirung 7 234, Langtang Ri 7 205, Shishapangma 8 027 (Tibet) |
  | Jugal (NE) | Dorje Lakpa 6 966, Phurbi Chyachu 6 637 |
  | Rolwaling (ENE) | Gauri Shankar 7 134, Melungtse 7 181, Kang Nachugo 6 735 (27.905, 86.4425), Chobutse 6 685 (27.882, 86.492; Nepal Himal Peak Profile), Tengi Ragi Tau 6 938, Numbur 6 958 |
  | Khumbu (E, 140-165 km) | Cho Oyu 8 188, Everest 8 849 |
  | Manaslu group (WNW) | Manaslu 8 163, Himalchuli 7 893 |

### Which peaks a player can really see (line of sight on GLO-30)

Ray-marched on the full 30 m DEM (six tiles N27-N28 E084-E086), curvature with R_eff = 7/6 R, eye 2 m above the
ground, foreground from 500 m:

| From | Visible above the rim (rise over the foreground) | Hidden |
|---|---|---|
| Basantapur (1 302 m) | Yangra 4.17° (+0.38°), Pabil 4.15° (+0.85°), Salasungo 4.25° (+0.79°), Langtang Lirung 4.98° (+0.31°), Phurbi Chyachu 3.90° (+0.56°), Gauri Shankar 2.78° (+0.77°), the Rolwaling row 2.1-2.3° | Dorje Lakpa (-0.13° behind Shivapuri's east shoulder), Langtang Ri, Shishapangma |
| Boudha (1 336 m) | Yangra, Pabil, Salasungo, Phurbi Chyachu 4.22° (+0.54°), Gauri Shankar 2.96° (+0.74°) | Langtang Lirung (Shivapuri at 10 km stands at 7.3°), Dorje Lakpa |
| Swayambhu (1 401 m) | Langtang Lirung 4.94° (+0.26°), Gauri Shankar 2.68° (+0.76°) | the Ganesh group (Nagarjun at 3.7 km stands at 5.2-5.8°) |

**True scale.** From the valley floor the snow peaks rise only 0.3-1.0° above the rim: 10-30 pixels at the game's 62°
landscape view. That is what the photos show too (see "Ganesh Himal from Kathmandu", a rooftop shot: a thin pink row of
peaks over dark blue hills). The world is true 1:1 vertically (ADR-004), so the skyline is too; the peaks grow as the
player climbs (rooftops, Swayambhu, Chandragiri, Nagarkot, flying), exactly as they do in Kathmandu.

## 2. Reference photos and what they show

| Photo (Commons) | Credit, licence | What to take from it |
|---|---|---|
| Swayambhunath Stupa and Langtang Mountain Range, Nepal | Safalphotos, CC BY-SA 4.0 | telephoto from the south: Langtang Lirung's broad snow pyramid; snow lit warm white `#E6D7D0`, shaded snow and rock ribs blue-grey `#7B9DBE`/`#7497BC`; a pale haze band `#A1A7B5` at the foot of the range; nearer ridges dark blue-green `#517899` |
| Swyambhu and langtang | Mbiken, CC BY-SA 4.0 | the whole Langtang-Jugal row as a jagged white crest over a white haze band, sky clear blue above |
| Ganesh Himāl seen from Lalitpur, Nepal | Xiaojun Deng, CC BY 2.0 | layered ridges getting bluer with distance (`#075979` near, `#1A81AB`, `#2F88B4` farther), the snow wall above them |
| Ganesh Mountain Range seen from Chandragiri Hill | Saroj Pandey, CC BY-SA 4.0 | the Ganesh silhouette (Pabil's point left, Yangra and Salasungo centre, a long crest east); snow faces ribbed with blue-grey rock, rocky blue foothills with snow streaks below |
| Ganesh Himal from Kathmandu | Zarniwoop und Zarquon, CC BY-SA 4.0 | true scale from a rooftop at dusk: alpenglow pink on the peaks, the rim hills dark navy `#394959`, the valley in shadow |
| Kathmandu Valley panoramic view from Shivapuri hills under haze | Krishna k. sahh, CC0 | the valley haze: everything beyond 15-20 km dissolves into a grey-beige band |
| Kathmandu view from Swayambhunath steps | - MattW -, CC BY-NC-ND 2.0 (shots package) | the rim as soft blue-grey silhouettes in morning haze |
| Le Gan Chenpo, Le Langtang Lirung (Himalaya, Népal) | Jean-Pierre Dalbéra, CC BY 2.0 | close-ups: snow flutings, rock ribs, the snow line around 5 000 m in autumn |

Light and air in the photos:

* Sunlit snow is the brightest thing in the scene; shaded faces are blue (sky light), not grey.
* The foot of the range sits in haze; the sight lines to the summits pass above it, so the peaks stay crisp.
* Each farther ridge is bluer and paler (aerial perspective), so the ranges read as layers.
* Snow line in October: about 5 000 m on the south faces, lower in gullies, higher on steep rock.
* At sunrise and sunset the peaks catch alpenglow (gold to pink) while the valley is already in shadow.

## 3. What the game drew at the playtest (the reproduction)

`core-tests/HorizonReproTests` rebuilds the skyline headlessly from the committed `kathmandu_core` sample exactly as
the game streams it (`TileSelector` with the tier's rings, `StreamingConfig.TerrainStep`, the rendered terrain surface,
earth curvature, the far clip of `WorldRoot.ConfigureCamera`, `WorldSky`'s fog) and checks each of the 13 peak
sightings above. Before the skyline ring (`ReportStreamedTerrainAlone`, log in `previews/sky/before_repro.log`):

| Tier | Result | Why |
|---|---|---|
| Low | 13 of 13 wrong or invisible | the far clip (67.5 km) cuts every peak beyond 65 km, and the fog floor that hides the end of the 50 km world leaves 0-1 % of a peak |
| Mid | 13 of 13 | peaks drawn 0.13-0.57° too low (the level-5 ring's 2 km terrain quads flatten the summits) and fogged to 6-13 %; Gauri Shankar beyond the far clip |
| High | 13 of 13 | the same flattening (Ganesh 0.37-0.57° low, i.e. 500-1 000 m), 29-40 % left by the fog, Gauri Shankar hidden behind a flattened nearer ridge |

On top of that the horizon tiles themselves hold the summits 40-260 m low (GLO-30 infill: Gauri Shankar 6 874 m on the
level-6 grid instead of 7 134 m).

## 4. The model

* **Heights.** `HorizonHeightField`: one grid from the pack's horizon tiles (level 6, 128 m; Low 256 m keeping the highest
  of each four), plus a summit cone to the published height of every curated peak on the grid, plus a synthetic massif
  (cone with three ridges on a broad shoulder) for curated peaks beyond the region's horizon tiles (Rolwaling east of
  86.4° E, Cho Oyu, Everest).
* **Panorama.** `HorizonPanorama`: rays every 0.25° (Mid, High) / 0.4° (Low) from 4 km to 250 km, curvature
  `d²/(2 R_eff)` as the world shaders, the highest point per distance band (4-30, 30-60, 60-100, 100-250 km on Mid/High;
  4-30, 30-75, 75-250 km on Low), each curated summit sampled exactly where the ray passes it, and a summit column of its
  own when it stands on its band's silhouette (so the peak is true from wherever the camera is).
* **Mesh.** `HorizonMesher`: Douglas-Peucker in degrees per band, a curtain column per kept point from the true
  silhouette point down to 6° below the eye's horizon; triangles capped per tier (Low 2 500, Mid 5 000, High 9 000,
  inside the sky + water slice of 4 k / 10 k / 20 k).
* **Drawing.** `Ghumante/Skyline` moves each vertex along its view ray to the handover distance (30 km on every tier;
  Mid 45 km and High 55 km before the playtest round) and draws after the opaque world: streamed terrain nearer than that
  stays in front; everything beyond it is the ring's true silhouettes (the flattened horizon terrain, and the high
  ground 30-60 km out that the streamed terrain paints in khaki alpine and bare-ground colours, while every photo shows
  it blue-grey with snow streaks over a hazy foot); the sky fills the rest. The far clip stays as it was.
* **Facets.** Every quad between two kept silhouette points has its own vertices and one flat normal: the face that
  falls at 31-52 degrees (from the DEM slope) from that piece of silhouette towards the eye. A piece that rises to the
  right belongs to a face turned left and the other way round, so each peak has a lit and a shaded face meeting in a
  crisp ridge line under the summit, gullies under the notches, as in the Chandragiri and Lalitpur photos; the ground's
  own aspect at the silhouette turns it further (0.35 per unit slope).
* **Look** (`HorizonLook`, the shader's C# twin, same numbers):
  * ground colour by altitude: hill forest `#3B5636` below 2 600-2 900 m, conifer `#2F4438`, dark alpine scrub `#55544A`
    from the tree line (3 600-4 100 m), blue-grey rock `#4C5262` up to the snow line (5 000 m in October, ±300 m from
    face to face, up to 400 m higher on steep rock), snow `#F4F6F8` above;
  * the snow reaches ±350 m down the gullies and leaves the ribs bare (the white streaks on the dark foothills of the
    Chandragiri photo); bare rock breaks it on the lower faces, more on steep ground, and thins out over the top
    1.2 km so the summits read white, as in every photo;
  * rib noise slants down the faces (150 m ribs over 520 m patches, no longer 2.5 km tall streaks), bends the normal
    (strength 0.6) and fades out below three pixels per rib, so the wide view does not shimmer;
  * light: a three-band toon ramp on N·L (edges 0.02 and 0.30, mid band 0.78 of the sun, dark band only the sky light,
    ambient 0.55, sun 0.85): sunlit snow is the brightest thing in the scene, shaded snow a clear blue about 0.7 times
    as bright as the sky behind it (photo: `#7B9DBE` against `#9DB5D5`), and grazing faces stay clearly above the sky;
  * alpenglow: while the sun is the light, a point takes the sun at the elevation it sees over the curve of the earth
    (a 7 000 m summit sees 1.45 degrees further down than the valley), so the peaks turn gold and pink before sunrise
    and after sunset while the valley is in shadow (the dusk rooftop photo); by day nothing changes;
  * air: the valley haze (the scene fog colour and density, only over the part of the sight line below 3 000 m; the
    scene fog's floor weighs in only by the square of that part, so it never hazes the sight lines to the peaks), then
    clear-air aerial perspective over 90 km at sea level (October air, near pure Rayleigh scattering; 8 km scale height:
    about 150 km to a 7 000 m summit seen from the valley) that
    fades the ranges into the sky right behind them (`Ghumante/SkyGradient`'s own gradient, glow and fog band), never
    into a brighter horizon colour: a peak can no longer melt into the sky by being lifted to its brightness.
* **Valley haze** (`SkyPalette.ValleyHazeDensity` = 5.0e-5, the scene fog's least density, the same on every tier;
  2.1e-5 before this round): in the photos over the city (Swayambhu looking east, Basantapur and rooftops looking north)
  the forested rim 12-20 km away is a blue-grey silhouette with about half of its own colour, the ridges beyond 30 km a
  pale band, and the city a few kilometres away is already a little hazy. Exp² at 5e-5 leaves 99.5 % at 1.4 km (the
  streets the game is played in), 94 % at 5 km, 57 % at 15 km, 37 % at 20 km and 10 % at the 30 km handover (Low's
  end-of-world floor, 3.6e-5, is now below it, so all tiers share one valley). The skyline hazes the sight lines that
  rise above the 3 km haze top with the palette's own 1.2e-5 (`_GhSkyAir`), so the snow peaks stay crisp over a hazy
  foot, the look of every photo of the Langtang and Ganesh ranges from the valley.

## 5. After: renders beside the photos

`core-tests/HorizonPreviews` (set `GHUMANTE_PREVIEW_DIR`; `GHUMANTE_SKY_BEFORE=1` adds the playtest state) dumps the
streamed terrain nearer than the handover, the ring and the game's sky from each place, coloured on the CPU with the
shaders' own models (`HorizonLook` for the ring, a ToonLit-like copy for the terrain, SkyGradient for the dome), and
`render.sh` renders them with `tools/mesh-preview`. The comparisons are in `/home/user/wt/previews/sky/compare/`
(render left, photo right; photos are references only and never enter the repository):

Each comparison shows the playtest build ("PLAYTEST": the streamed terrain alone under that build's fog) or the ring as
it stood before this round's haze and handover change, then the game now, then the photo:

| Comparison | Render | Photo |
|---|---|---|
| `compare_basantapur_north.png` | Basantapur rooftop looking north at 9:00, game zoom (37° tall, 62° wide) | - |
| `compare_boudha_north.png` | Boudha looking north-north-east at 9:00, game zoom | "Himalayas @ Golf Club, Katmandu", SouthAsiaGolf, CC BY 2.0 (Gokarna, 4 km north-east of Boudha; no open photo from the kora itself was found) |
| `compare_swayambhu_north.png` | Swayambhu platform looking north-north-east at 9:00, game zoom | - |
| `compare_swayambhu_east.png` | Swayambhu over the city to the eastern rim at 10:30 | "The ancient settlement of Kathmandu as viewed from Swayambhunath Hill", Wonderlane, CC BY 2.0 |
| `compare_kirtipur_langtang.png` | Kirtipur, telephoto on Langtang Lirung right behind Swayambhu hill (17-19°) | "Swayambhunath Stupa and Langtang Mountain Range, Nepal", Safalphotos, CC BY-SA 4.0 |
| `compare_kirtipur_langtang_mid.png` | Kirtipur, the Langtang row at mid zoom | "Swyambhu and langtang", Mbiken, CC BY-SA 4.0 |
| `compare_basantapur_ganesh.png` | Basantapur, telephoto on Ganesh (Pabil, Salasungo, Yangra) over the rim | "Ganesh Himāl seen from Lalitpur, Nepal", Xiaojun Deng, CC BY 2.0 |
| `compare_chandragiri_ganesh.png` | the top of Chandragiri, telephoto on Ganesh | "Ganesh Mountain Range seen from Chandragiri Hill", Saroj Pandey, CC BY-SA 4.0 |
| `compare_basantapur_dusk.png` | Basantapur rooftop at 17:51 towards Ganesh Himal: alpenglow on the peaks, the valley in shadow | "Ganesh Himal from Kathmandu", Zarniwoop und Zarquon, CC BY-SA 4.0 |
| `compare_swayambhu_langtang.png` | Swayambhu, telephoto on Langtang Lirung | - |
| `compare_boudha_tele.png` | Boudha, telephotos on Phurbi Chyachu (Jugal) and Gauri Shankar | - |
| `compare_basantapur_sunrise.png` | Basantapur looking north at 6:00 | - |

What matches the photos now: the snow peaks at true scale, thin above the rim; sunlit snow the brightest thing in the
view and shaded faces a clear blue darker than the sky; lit and shaded faces meeting in ridge lines; rock breaking the
lower faces and white summits; the forested rim blue-grey in the valley haze (it was a saturated green); the foothills
30-60 km out as blue-grey rock faces with snow streaks over a pale haze band (they were khaki streamed terrain); the
peaks at dusk standing out over a dark valley (though less pink than in the photo, see section 8). What is still the
cartoon: the faces are flat facets with procedural ribs,
not the real snow and rock pattern of each face, and the snow streaks of foothills near the snow line hang as regular
drips.

## 6. Numbers (core-tests, committed `kathmandu_core` sample)

**Bearings and elevations** (`HorizonReproTests.PeaksDrawnAtTrueBearingAndElevation`, every tier). The drawn summit is
the local maximum of the drawn skyline on the peak's range nearest to its true bearing; the true values come from the
published coordinates and heights only. Same on Low, Mid and High:

| From | Peak | True bearing | Drawn | Δ | True elevation | Drawn | Δ | km |
|---|---|---|---|---|---|---|---|---|
| Basantapur | Ganesh I (Yangra) | 346.54° | 346.54° | 0.00 | 4.17° | 4.17° | 0.00 | 78.2 |
| Basantapur | Ganesh IV (Pabil) | 341.98° | 341.98° | 0.00 | 4.16° | 4.16° | 0.00 | 74.5 |
| Basantapur | Ganesh III (Salasungo) | 344.78° | 344.78° | 0.00 | 4.27° | 4.27° | 0.00 | 72.2 |
| Basantapur | Langtang Lirung | 17.82° | 17.82° | 0.00 | 4.99° | 4.99° | 0.00 | 64.7 |
| Basantapur | Phurbi Chyachu (Jugal) | 48.87° | 48.87° | 0.00 | 3.90° | 3.90° | 0.00 | 72.9 |
| Basantapur | Gauri Shankar | 73.90° | 73.90° | 0.00 | 2.77° | 2.77° | 0.00 | 105.0 |
| Boudha | Ganesh I (Yangra) | 342.36° | 342.36° | 0.00 | 4.18° | 4.18° | 0.00 | 77.8 |
| Boudha | Ganesh IV (Pabil) | 337.59° | 337.59° | 0.00 | 4.14° | 4.14° | 0.00 | 74.5 |
| Boudha | Ganesh III (Salasungo) | 340.25° | 340.25° | 0.00 | 4.26° | 4.26° | 0.00 | 72.0 |
| Boudha | Phurbi Chyachu (Jugal) | 47.16° | 47.16° | 0.00 | 4.22° | 4.22° | 0.00 | 67.6 |
| Boudha | Gauri Shankar | 74.14° | 74.14° | 0.00 | 2.96° | 2.96° | 0.00 | 99.3 |
| Swayambhu | Langtang Lirung | 19.59° | 19.59° | 0.00 | 4.95° | 4.95° | 0.00 | 64.1 |
| Swayambhu | Gauri Shankar | 74.77° | 74.77° | 0.00 | 2.67° | 2.67° | 0.00 | 106.3 |

(Allowed: 2° of bearing, 0.1° of elevation. The summits are exact because each curated summit on its band's silhouette
gets a column of its own at its published position and height. `HorizonTests.PeaksStayTrueUntilTheNextRebuild` holds
the same with the ring built the rebuild distance away in four directions.)

**Clear enough to read**: mean absolute luminance contrast of the drawn summit area (top 0.1°, ±0.5° round the summit)
against the game's sky behind it, shaded exactly as the skyline shader shades it, worst of 9:00, 12:00 and 15:00. At the
playtest none of the 13 sightings was drawn at all on Low, and none true on Mid and High (section 3). Now (bar 0.15;
Low 0.14; with this round's valley haze and 30 km handover):

| Tier | Worst sighting | Range over the 13 |
|---|---|---|
| Low | Pabil from Boudha at 15:00, 0.15 | 0.15-0.40 |
| Mid | Gauri Shankar from Boudha at 9:00, 0.17 | 0.17-0.34 |
| High | Gauri Shankar from Boudha at 9:00, 0.17 | 0.17-0.33 |

Before the look pass of this round the eastern peaks faded to the sky's own brightness in the morning (Phurbi Chyachu
from Basantapur 0.04-0.05, Gauri Shankar 0.08-0.11 with the old metric): the clear air faded them into a horizon colour
brighter than the sky behind them.

**The rim** (`HorizonTests.RimHillsAtTheirTrueBearingsAndHeights`): Shivapuri, Phulchoki, Chandragiri and Nagarjun at
their true bearings and summit elevations (within 0.1°) from Basantapur and Boudha on every tier.
`RingTopsUpFlattenedHillsInTheirOwnColours`: the streamed terrain 4-12 km out is decimated (Nagarjun from Swayambhu:
the field holds 2 067 m where the streamed surface draws 1 845 m), so on 60-80 % of the bearings the ring stands above
the streamed hill on the same hill (median 0.11-0.19°, p90 0.40-0.92°, worst 2.0° at Nagarjun from Swayambhu). That is
the true crest; it is drawn in the terrain's own October hill-forest colours, and nowhere higher than the field's own
ground on the panorama's rays.

**Air**: `ValleyHazeLeavesTheCityClearAndHazesFarRidges` (on every tier: the streets within 1.4 km above 99 %, the city
5 km away above 90 %, the rim 15 km away 45-70 %, the ground beyond the 30 km handover below 15 %), `HandoverIsSeamless` (sight lines that stay in the haze get exactly the scene fog at the handover),
`SummitsCatchTheSunBeforeTheValley` (at dawn, sun 0.5° below the valley's horizon: valley 0.03, 7 000 m summits 0.25 of
full sun, by day equal).

## 7. Cost per tier

| | Low | Mid | High |
|---|---|---|---|
| Triangles (cap) | 1 846 (2 500) | 3 706 (5 000) | 4 564 (9 000) |
| Vertices (4 per quad, flat facets) | 3 692 | 7 412 | 9 128 |
| Mesh | 140 KB | 282 KB | 347 KB |
| Sky + water slice (W2_DESIGN 10.4) | 4 k | 10 k | 20 k |
| Draw calls | 1 (no shadows, no outline, never frustum-culled) | 1 | 1 |
| Rebuild (worker, after moving) | 40-80 ms every 400 m | 90-160 ms every 300 m | 100-140 ms every 200 m |
| Main thread per rebuild | one mesh upload | one mesh upload | one mesh upload |
| Height samples per rebuild | 0.71 M | 1.38 M | 1.38 M |
| Height field (once, worker) | 1.3 MB, 240-320 ms | 5.3 MB, 380-550 ms | 5.3 MB, 380-520 ms |
| Allocation per warm rebuild | 40 bytes | 40 bytes | 40 bytes |

GPU: the ring is drawn after the opaque world with the depth test, so only the band of screen near the horizon that no
nearer terrain covers runs its fragment shader (a few noise lookups, the sky gradient and two exponentials).

## 8. Open issues (outside this package)

* **Streamed terrain flattens near hills** (streaming / perf-roads): Nagarjun 4.5 km from Swayambhu is drawn about
  220 m low on High (terrain-quad budgets per tier, and the sample pack's coarse tiles there). The ring now tops it up
  with the true crest in the terrain's colours; finer near terrain would remove the step.
* **Streamed alpine and bare ground reads khaki** (nature, `BiomePalette`): beyond 30 km the ring now draws that ground
  in blue-grey rock, scree and snow; inside 30 km (the rim hills are forest, so this is rare) the khaki classes remain.
  Photos show such slopes dark blue-green through the haze.
* **The whole world's far look changed** (look, lead): the valley haze is 5e-5 on every tier (2.1e-5 before), so the
  forested rim reads blue-grey and the city 5 km away keeps 94 % of its colour; within 1.4 km nothing visible changes.
  Other packages' far previews will look hazier.
* **Streamed terrain beyond 30 km is now hidden** (streaming / perf-roads): Mid and High stream terrain out to 80 and
  120 km, but beyond the 30 km handover the ring covers it (and the haze leaves a tenth of it). Shrinking those view
  radii (or their coarsest rings) would save memory, uploads and triangles with no visible change; the far clip
  (`WorldRoot.ConfigureCamera`, view radius x 1.25 + 5 km) only has to stay beyond 30.6 km.
* **High viewpoints get the valley's haze too** (sky with lead, `WorldSky` + `WorldRoot`): Unity's fog is by distance
  only, so from Chandragiri, Nagarkot or an aircraft the ridges 10-30 km away are as hazy as from the valley floor
  (`compare_chandragiri_ganesh.png`: the snow range is crisp, the foothills below it a pale band), while the photo from
  Chandragiri shows them crisp above the haze. The skyline itself is altitude-aware; the scene fog would need the
  camera's altitude in `WorldSky` (thin the valley share above about 2 000 m), which `WorldRoot` would have to pass.
* **Dusk palette** (sky): at 17:30-18:00 the evening tint turns the whole sky and the haze salmon pink
  (`compare_basantapur_dusk.png`), so the alpenglow on the peaks reads pale against it; the photo has a pale blue-grey
  sky, navy hazy hills and pink only on the snow and the horizon. A follow-up should tint only the horizon band towards
  the sun.
* **Season**: the ring's forest colours are the October palette (the world's default season); a world opened in
  another season keeps them. A global for the season's colours would follow `BiomePalette`.
* **Field build at world open** allocates about 50 MB transiently on the worker (decoding the horizon tiles), once.
* **Shader on device**: `Ghumante/Skyline` is checked by the EditMode test (`isSupported`, queue) and by its C# twin
  here; it still needs a look on the Mac and on a phone.
* **Files outside the package touched minimally**: `World/WorldRoot.cs` (create, tick and dispose the ring, three
  places), `World/README.md` (the shader row and the `Sky/` row).
