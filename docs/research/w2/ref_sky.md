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
* **Drawing.** `Ghumante/Skyline` moves each vertex along its view ray to the tier's handover distance (Low 30 km, Mid
  45 km, High 55 km) and draws after the opaque world: streamed terrain nearer than that stays in front; the flattened
  horizon terrain beyond it (already 50-70 % fog there) is replaced by the true silhouettes; the sky fills the rest.
  The far clip and the scene fog stay as they were (the ring needs neither).
* **Look** (`HorizonLook`, the shader's C# twin): hill forest `#3B5636` below 2 600-2 900 m, conifer `#2F4438`,
  alpine `#6C6A5A` from 3 500-4 000 m, rock `#545A68` up to the snow line (5 000 m, ±300 m from face to face, up to
  400 m higher on rock-steep ground), snow `#F4F6F8` broken by rock ribs and gullies (more on steep ground); normals
  from the DEM gradient at the silhouette, bent across the curtain by 150 m rib noise (snow flutings and ribs that
  catch the light on one side), faded out where a rib would be narrower than three pixels so the wide view does not
  shimmer; the rib coordinate is the bearing from a fixed anchor, so the detail stays on the ground between rebuilds;
  steep face normals (a range seen from afar shows its faces, not its tops), two-tone light from the main light (sun,
  or moon) plus sky ambient (sunlit snow just white and warm, shaded snow sky blue, as in the photos); the valley haze
  only over the part of the sight line below 3 000 m, with the scene fog's density for lines that stay in it (so a
  ridge crossing the handover keeps its colour) and the air's own density (`WorldSky` publishes the palette's haze,
  without the floor that hides the end of the Low and Mid worlds) for lines that rise above it; clear-air aerial
  perspective towards `WorldSky`'s horizon colour over 75 km at sea level, longer through the thinner air of high
  sight lines (8 km scale height: about 125 km to a 7 000 m summit seen from the valley).

## 5. After

See `previews/sky/` for the renders from Basantapur, Boudha, Swayambhu and Chandragiri, each set beside the photos,
and section 6 for the test numbers.
