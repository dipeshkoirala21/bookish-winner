# Nature reference brief (detail pass, nature package)

What the Kathmandu Valley's plants, forests, fields, parks and gardens look like, as the nature kit models them
(`Core/Generators/Flora`, `Core/Generators/Placement`, `Core/Meshing/TerrainMesher` + `AreaMesher`). Each entry gives
the silhouette and proportions, the colours (sRGB hex, as in `FloraCatalog`), the details the kit models and the
photos it was checked against. Reference photos are **not** in the repository: they were fetched with
`refimg.py` into the shared reference folder (`refs/nature/<set>/`, with a `credits.txt` per set), and the
side-by-side sheets (photo | preview) were composed from them for review only. Tags: [S] sourced (W2_DESIGN 5.8,
research/street_life.md 9-12, the photos below), [E] estimated from the photos.

## 1. How to read the kit

* Every kind is a unit model (height 1, crown width 1), scaled per instance by (crown, height, crown); the catalogue
  model size (H × W, metres) sets the proportions and the placed height range.
* Four LODs: LOD0 (≤ 1 600 tris in every month, blooms included) and LOD1 (≤ 240) per species, then a family volume
  (≤ 112) and a family impostor (23 tris: an icosahedron fitted to the family's crown ellipsoid, its rings a little
  lumpy, on a three-sided trunk; no bipyramid "crystals") shared by the shape family (round, cone = mature chir pine,
  umbrella, column, fountain). `LodsKeepTheSilhouette` keeps the width, top and crown base of LOD1 and LOD2 within
  20 % / 0.1 / 0.12-0.22 of LOD0, so LOD switches do not pop.
* Draw budget (`FloraBudget`, shared by the game's `DressingRenderer` and the preview scenes, so a preview shows what
  the game draws): 18 k / 54 k / 100 k vegetation triangles on Low / Mid / High. Trees take LOD0, LOD1, the family
  volume or the impostor nearest first under each level's cap and triangle budget (`FloraLodPlan`), only inside the
  camera's view cone plus a margin (`FloraView`; everything within 18 m always). The volume budget holds its cap, so a
  forest inside the LOD1 radius spills to volumes, not impostors (Mid: volumes to ~150 m in a dense forest, impostors
  beyond). Small plants (grass, ferns, rocks, shrubs, pots) draw to 18 / 30 / 45 m; the big ones (straw stacks,
  boulders, hedges, marigold beds, bougainvillea, poinsettia, sunflowers, tulsi math) at LOD1 on to 45 / 80 / 120 m.
* Bloom, flush and harvest are baked into the near models per month; the far models are grey and tinted with the
  month's crown colour (`FloraCatalog.FoliageColour`: leaf mixed with bloom × bloom cover).
* Vertex alpha 255 marks foliage (wind sway and instance tint), 0 fixed parts; UV0 = (material channel, baked AO).

## 2. Trees

| Kind | Where in the valley | Silhouette and proportions (model H × W; placed H) | Colours | Details modelled | Photos |
|---|---|---|---|---|---|
| Pipal (Ficus religiosa) | Chautari squares, temple courts, OSM trees [S] | Broad dome on a short, pale, fluted trunk that forks low; crown as wide as tall (18 × 18; 15-25 m) | leaf `#4E8A3A`, bark `#9A948A`, copper flush `#C27C5E` in March-April | Buttress flutes, red-white puja band on the trunk, chautari platform under OSM trees (6 × 6 m, 0.8 m; placement turns its porter ledge toward the road and fits it outside the corridor) | pipal/ref_00 |
| Bar (banyan) | Chautari, old crossroads [S] | Very wide low umbrella (16 × 24; 15-20 m) on three stems with hanging aerial roots | leaf `#2F6B2F`, bark `#8A8274` | Aerial-root pillars, dense dark crown | bar/ref_01 |
| Jacaranda (Jacaranda mimosifolia) | Brought to Rana palace gardens (1920s or earlier), planted round Tundikhel in 1970; now Tundikhel, Ratna Park, Khula Manch, Durbar Marg, Singha Durbar, Kamaladi, Tripureshwor, Mahalaxmisthan, the Ring Road at Ekantakuna-Satdobato [S] | Broad open umbrella, wider than tall, on a short forked dark trunk (12 × 12; 8-15 m) | leaf `#5E8F45`, bloom `#A58AD8` from late March, peak late April and May, last flowers in June (far `#8E6CC8`), bark `#5A4A3E` | Violet clumps, petal carpet disc under the tree in bloom | jacaranda_np/ref_00, jacaranda_tree/ref_03 (the earlier jacaranda/ref_02 was an eastern redbud and is dropped) |
| Silky oak (Grevillea robusta) | Avenues, school and office grounds [S] | Ragged teardrop about half as wide as tall, widest 40 % up, thin top (22 × 10.5; 18-30 m); a straight furrowed trunk runs up through it; short branches ascending 25-50° | leaf `#557A3A`, bloom `#F2A33A` April-May, bark `#5E4A3A` | An open crown of branch sprays (a small lumpy core with seven hanging fern-frond cards each), sky between them, no stacked balls; in bloom the sprays turn gold and every other frond is a golden comb card; far LODs golden in April-May | silkyoak/ref_07, ref_09 |
| Bottlebrush (Callistemon) | Avenues, gardens [S] | Small weeping tree (6 × 5; 4-8 m), drooping outer leaves | leaf `#4F7D3A`, bloom `#D7263D` March-May (light second flush Oct-Nov) | Red brush tubes at the twig tips, drooping fringe | bottlebrush/ref_00 |
| Camphor | Parks, compounds | Dense round crown (15 × 13; 10-20 m) | leaf `#3F7F3A`, bronze flush in spring | Layered clumps | (W2_DESIGN 5.8) |
| Eucalyptus | Roadsides, institutional plantations [S] | Tall pale trunk, sparse high crown (28 × 11; 15-35 m) | leaf `#7FA08A` (blue-grey), bark `#D9D2C3` | Peeling pale bark patches, open shell crown | eucalyptus/ref_00 |
| Chilaune (Schima wallichii) | Valley-rim forests 1 300-1 800 m [S] | Dense rounded crown on a clean bole in forest (15 × 11; 10-22 m) | leaf `#4A7F36`, white flowers `#F4F1E6` May-June, red new leaves in spring | Many dark glossy clumps; in bloom 34 white flower blobs where the leaf fringe thins (LOD0 stays ≤ 1 600 tris) | schima/ref_08, ref_01 |
| Katus (Castanopsis indica) | Valley-rim forests with Schima [S] | Round dense crown (15 × 12) | leaf `#527F36`, catkins `#D9C46A` March-April | Pale catkin tassels | (S 11) |
| Utis (Alnus nepalensis) | Stream banks, landslide scars, plantations [S] | Straight pale trunk, narrow oval crown (16 × 10; 10-22 m), branches 30-55° | leaf `#4C8B3E`, bark `#8C8A80` | Light-green clumps on short ascending branches | alder/ref_02 |
| Chir pine (Pinus roxburghii) | Nagarjun, Chandragiri and Kirtipur plantations, dry south faces 1 400-1 800 m [S] | Tall straight plated trunk bare to ~40 % of its height; a broad, open, irregular crown widest a third of the way up with a domed top (20 × 12; 12-25 m) | leaf `#4F7A3A`, bark `#7A4A2E` with darker plates | Thirteen short near-horizontal branches spiralling up the trunk (golden-angle turns, jittered heights: no tiers), each with a needle tuft at the tip and a smaller one halfway (lumpy bristly cores with drooping needle-bundle cards), sky between the tufts; no stacked plates, no cone | chirpine2/ref_06, ref_07, ref_00 |
| Laligurans (Rhododendron arboreum) | Oak-rhododendron forest 1 800-2 400 m (Phulchoki, Shivapuri) [S] | Small gnarled tree on several stems (8 × 7; 6-12 m) | leaf `#3B6B34`, bloom `#C8102E` February-April | Spiky scarlet trusses studding the crown, red-brown bark | laligurans/ref_00 |
| Oak, brown oak | Upper rim forest 1 800-2 400 m+, lichen-hung [S] | Round crown (15 × 13 / 14 × 12) | leaf `#3B6B34`; brown oak `#6B7A4A` with lichen tints | Lichen cards (brown oak) | (S 11) |
| Sal (Shorea robusta) | Below 1 000 m (Terai, Chure; valley outskirts only) [S] | Tall clean bole, high rounded crown (25 × 12; 18-32 m) | leaf `#4F8A3A`, bark `#5A4636` | High crown | sal/ref_07 |
| Bamboo (bans) | Behind village houses, field edges [S] | Clump of a dozen culms arching over at the top (12 × 7; 8-15 m) | culm `#7DA83E`, leaves `#6FA03A` | Darker nodes, leaf-card sprays on the upper culms | (no usable photo, see 7) |
| Banana (kera) | House gardens of peri-urban and village houses [S] | Pseudostem with huge arching paddle leaves (4.5 × 5; 3-5 m) | leaf `#7DBA3A`, stem `#8DA04A` | Pale midribs, drooping torn tips, dry hanging leaves, purple bud and green bunch, suckers | (no usable photo) |
| Palm | Hotel and garden compounds | Slender banded trunk, feathery fronds (9 × 6) | leaf `#5C9A3A` | Serrated fronds, dead-frond skirt | (W2_DESIGN 5.8) |

## 3. Plants, flowers and ground cover

| Kind | Where | Form | Colours | Photos |
|---|---|---|---|---|
| Sayapatri (marigold) bed | Park flower rings, by village doors; Tihar (October-November) | 2.6 × 1.3 m bed, 0.45-0.65 m, edged | flowers `#F5821F` with yellow, leaf `#3E7A32` | marigoldfield/ref_03 |
| Rose | Park beds | 1.1 m bush, blooms spring and autumn | `#D81B3C` | (gardens) |
| Lalupate (poinsettia) | Beside houses, in winter red (November-February) | Small tree 1.5-3.5 m on two stems; red six-pointed bract whorls with a yellow eye at every branch tip | bracts `#D11F2A` | poinsettia/ref_00 |
| Bougainvillea | Compound walls and gates of urban houses | 2-3.4 m arching canes under a magenta mass | `#C2185B`, leaf `#4A8A3A` | bougainvillea/ref_00 |
| Sunflower | Village house sides, June-November | Clump of three, 1.6-2.6 m | `#F6C21B` | — |
| Potted plants | Doorsteps of 30-45 % of houses (old core 45 %) [S] | Group of three: terracotta pots, painted tins, cut jerry cans | terracotta `#B8643C`, tin `#2E7DBA` | (no usable photo) |
| Tulsi math | In front of 6-12 % of Hindu houses [S] | Stepped pedestal, 1.3-1.7 m, painted yellow / red / white, basil on top | yellow `#E8A62E`, red `#C8302A`, white `#F2ECDC` | tulsi/ref_06, ref_08 |
| Hedge | Park boundaries and both sides of park walks | 3 m clipped segment, 1.0-1.25 m high, 0.9 m deep | `#3F7F3A` | gardendreams/ref_01, ref_03 |
| Shrub, fern, grass tuft | Lawns, forest understorey, field edges, grassy hills | Mounds (0.8-2.2 m), fern fans (0.35-0.7 m), tufts (0.3-0.7 m) | grass greens in the monsoon `#7FB04A`, straw in winter `#B8A86A` | — |
| Rock, boulder | River banks and gravel beds, slopes over ~29° | Faceted stones 0.25-0.8 m and boulders 1-3 m, half buried | `#A39E96` | — |
| Kunyu (rice-straw stack) | Paddy fields after the October harvest until March [S] | Drum of sheaves round a pole widening to an eave, then a steep thatch cone with a top knot and the pole tip (2.4-3.6 m) | straw `#D9B65A` / `#B8924A` tiers | straw/ref_03, ref_01, ref_05 |

## 4. Forests (placement)

* Real forest polygons and forest biome cells hold canopy trees on a world-fixed jittered lattice of 10.5 m (about
  90 trees per hectare, the same tree at the same spot from every tile, so forests continue across tile edges), each
  crown about 1.45 lattice steps wide and its height from the species' proportions, so neighbouring crowns overlap:
  measured canopy cover 0.95 on the sample's Pashupati woods, ≥ 0.9 on broadleaf hills, ~0.8 on the more open chir
  pine faces (`ForestsCloseTheirCanopy`). Over 10 000 trees a tile widens the lattice evenly. Species come in stands
  ~70 m across. Under them a dark, shaded floor with leaf-litter patches (forest areas `#4E8A3A` × 0.62 mixed with
  litter `#5C5634`, AO 0.72).
* Elevation bands (W2_DESIGN 5.8, S 11): fringe 1 300-1 400 m (Schima, Castanopsis, utis, broadleaf);
  Schima-Castanopsis to 1 800 m, with 60 % of the weight swapped to chir pine on south faces; oak-laurel with
  rhododendron and some bamboo to 2 400 m; brown oak and rhododendron above; sal below 1 000 m.
* Understorey (under one tree in three): ferns and shrubs in the broadleaf bands, grass and rocks on the open pine
  floor, ferns and rocks in the oak forest.
* Checked against: terraces_kv/ref_09 (the valley rim), hills/ref_07 (Swayambhu's wooded hill): from afar the rim
  forests read as a continuous dark green canopy with rounded crowns (compare/cmp_forest.jpg in the previews).

## 5. Fields, terraces and banks (terrain and area meshers)

* **Which land is farmed** (`CropLand`, shared by the terrain colours, the field lines and the placement mask):
  farmland, orchard and tea-garden areas, and cropland biome cells that no other mapped land use covers. The coarse
  biome raster calls Tundikhel, pitches, parks and whole residential blocks "valley cropland"; those get urban-green
  grass instead, no bunds, no terraces and no straw stacks (`NoStrawStacksOnTundikhelOrOtherNonFarmLand`). Sampled
  exactly on the 8 m lattice of the terrain's vertices, so tiles agree on their edges.
* **Patchwork:** cropland (valley cropland, hill terraces, farmland areas) is split into world-fixed plots
  (26 × 17 m on a district-wide axis) coloured by season: monsoon greens `#5DAA3A` / `#8CC84A`; October-November
  gold `#D9B44A` (55 %), green `#8FB848`, bare `#C9A86A` (ploughed, Dirt channel); winter mustard and wheat; spring
  mixed. Crops cover the flats and terraced slopes up to ~38°.
* **Terraces:** where the ground slope smoothed over ~40 m is 0.18-0.85 rise over run (so a terraced hillside stays
  terraced across small bumps and the lines never stop at single triangles), a thin riser line below every
  world-fixed level (1.9 m apart): at most 0.8 m wide in plan (0.55 m tall at most), grassy `#6E8C40` with the Grass
  channel in the monsoon and autumn, dry `#9A8A5A` (Dirt) in winter and spring, lifted 0.13 m on the exact terrain
  triangles. Gentler cropland gets grassy bunds (`#8FB060`, winter `#A8B070`) along the plot lines instead. Budget per
  tile at terrain step 1: 3 000 / 8 000 / 16 000 triangles on Low / Mid / High (`AreaOptions.FieldLineCap`), divided
  by the step on coarser rings; over it the bunds thin first, then every second terrace level is drawn (3.8 m steps),
  then whole 48 m blocks thin evenly. Checked against terraces_kv/ref_04, ref_05, ref_07 and mustard/ref_01: golden or
  green narrow benches separated by thin green risers following the contours (compare/cmp_terraces.jpg).
* **River banks:** a 2.5 m gravel strip (`#B8AE94`, Stone channel) outside river edges, mud (`#9A8664`) round ponds
  and lakes, sedge green round wetlands; boulders, cobbles and grass tufts on banks and gravel beds; a lighter
  shallow rim (`#8FD0DE`) inside water.
* **Ground relief:** micro-relief normals (~34 m wavelength, fading on coarse LODs), rock outcrops breaking through
  steep ground in noisy patches before the slope band, dry and lush patches on grass, a mottled canopy shell on
  forest biome cells. Everything is a function of world position, so tile edges agree.

## 6. Parks and gardens

* **Parks** (Ratna Park, Shankha Park, Tundikhel edges, Garden of Dreams [S]): ornamental trees (jacaranda 25 %,
  bottlebrush 20 %, camphor 15 %, silky oak 10 %, palm 10 %, pipal 5 %, other 15 %) about one per 180 m²; clipped
  hedges lining both sides of most walks (as the Garden of Dreams walks are), a ring of marigold beds with roses,
  shrubs on the lawns, hedges along half the boundary. Checked against gardendreams/ref_01, ref_03, park/ref_06.
* **House gardens:** pots at the front wall of 30-45 % of houses, a tulsi math in front of 6-12 %; by peri-urban and
  village houses banana (30 %), bamboo behind one in four, poinsettia by a side wall, a marigold bed or sunflowers by
  the door; bougainvillea at 8 % of urban compound corners; old cores get pots only.
* **Clearances** (docs/W2_DETAIL_CONTRACT.md §1.1-1.2): everything keeps out of road corridors, buildings and water.
  A tree keeps everything it has below 4.5 m out of the corridor plus 0.3 m (at least 1.2 m): its "low reach" is
  measured on its own LOD0 and LOD1 meshes (`FloraReach`: a bar's prop roots reach 5.7-7.6 m, a pipal's buttresses
  1.4-2.4 m, a tall eucalyptus only its trunk), so a crown that starts above 4.5 m may still overhang. OSM trees in a
  corridor are moved out (at most 6 m beyond what a bare trunk would need) or dropped; a chautari platform is turned
  with its porter ledge toward the road and placed (or shrunk to 3 m) so the whole turned outline, ledge and step
  included, stays 0.3 m outside. Park path hedges stand with their near face 0.3 m outside the path's 4.8 m corridor
  (or its drawn width when wider) and both ends clear of every other way's corridor, so crossings stay open.

## 7. Gaps and estimates

* No usable photos were found for banana, bamboo clumps, potted plants on Kathmandu doorsteps, ferns, oak forest or
  river rocks (searches returned unrelated subjects); those kinds follow the written sources and general knowledge
  [E]. Poinsettia, bougainvillea and marigold photos are from outside Nepal but show the same cultivars.
* Season dates for second flushes (bottlebrush in autumn, a light autumn jacaranda sprinkle) are [E].
* No openly licensed photo of a Kathmandu jacaranda was found (Openverse has only a 1986 Kathmandu Valley photo that
  lists jacaranda among the common trees; the Kathmandu Post, Onlinekhabar and Ekantipur photo features of the April-
  May bloom are copyrighted and were read for facts only: where the trees stand and when they flower). The shape
  references are real Jacaranda mimosifolia trees elsewhere (jacaranda_np/ref_00, jacaranda_tree/ref_03).
* Terrace step and riser heights (1.9 m, 0.55 m) are averages read from the photos [E]; real terraces range from
  1 to 3 m.

## 8. Photo credits (sheets only; not in the repository)

| Set / file | Title | Author | Licence | Source |
|---|---|---|---|---|
| pipal/ref_00 | A grand Ficus Religiosa tree and its trunk is colourful | Bigul Malayi | CC0 1.0 | https://wordpress.org/photos/photo/36865d5027/ |
| bar/ref_01 | Banyan Tree Root Shrine | Mabacam | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/59275783@N04/6679434051 |
| jacaranda_np/ref_00 | Jacaranda Tree HDR, Johannesburg | Paul Saad | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/114138418@N08/15078277874 |
| jacaranda_tree/ref_03 | Jacaranda tree | wallygrom | CC BY-SA 2.0 | https://www.flickr.com/photos/33037982@N04/3637356200 |
| silkyoak/ref_07 | Grevillea-robusta in full blossom | expom2uk | CC BY 2.0 | https://www.flickr.com/photos/57768042@N00/280379631 |
| silkyoak/ref_09 | Grevillea robusta (Silky Oak) as a street tree | Tatters | CC BY-SA 2.0 | https://www.flickr.com/photos/62938898@N00/24966962842 |
| bottlebrush/ref_00 | Roadside Callistemon | coofdy | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/57754952@N06/15277538995 |
| eucalyptus/ref_00 | Five Eucalyptus Trees. Aged Nine. 2010. | amandabhslater | CC BY-SA 2.0 | https://www.flickr.com/photos/15181848@N02/5114989034 |
| schima/ref_08 | Schima wallichii (3) | siddarth.machado | CC BY-NC 2.0 | https://www.flickr.com/photos/127280380@N06/27672864730 |
| schima/ref_01 | Schima wallichii | FarOutFlora | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/46821817@N08/6415032041 |
| alder/ref_02 | Alnus nepalensis 100515-0834 | Tony Rodd | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/8108294@N05/4887841770 |
| chirpine2/ref_06 | Pinus roxburghii | Soumil | CC BY-NC 4.0 | https://www.inaturalist.org/photos/209815770 |
| chirpine2/ref_07 | Pinus roxburghii | Soumil | CC BY-NC 4.0 | https://www.inaturalist.org/photos/209815797 |
| chirpine2/ref_00 | Pinus roxburghii Sarg. Between Lete and Ghasa | tireboushtroumpf | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/67698375@N04/16948500000 |
| laligurans/ref_00 | Laligurans (Rhododendron) | Barsha Paudel | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/48273796@N07/8626035981 |
| sal/ref_07 | Sal Tree (Shorea robusta) from my town Jhargram | Soumitra Giri | CC BY-NC 2.0 | https://www.flickr.com/photos/58250789@N05/6975814080 |
| poinsettia/ref_00 | Birmingham Botanical Gardens Poinsettia Tree | Southernpixel - Alby Headrick | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/99067767@N00/2129290893 |
| bougainvillea/ref_00 | Bougainvillea wall | @bastique | CC BY-SA 2.0 | https://www.flickr.com/photos/14429081@N00/2638005972 |
| marigoldfield/ref_03 | Red Salvias and African Marigolds in flower bed | ukgardenphotos | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/26183800@N07/8474799661 |
| straw/ref_03 | On the roads of Kathmandu - the old and the new? | Carsten ten Brink | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/77334245@N00/8229602212 |
| straw/ref_01 | Haystacks | Dey | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/45966355@N00/69454459 |
| straw/ref_05 | Scenes outside the city | Carsten ten Brink | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/77334245@N00/8241250036 |
| tulsi/ref_06 | Tulsi plant in temple premises | Samuelraj | CC BY-NC 2.0 | https://www.flickr.com/photos/28691998@N06/3064605483 |
| tulsi/ref_08 | tulsi plant | Veena Mitra | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/33169660@N07/3356089920 |
| terraces_kv/ref_09 | The rim of the Kathmandu valley | Francisco Anzola | CC BY 2.0 | https://www.flickr.com/photos/10345599@N03/12679358653 |
| terraces_kv/ref_04 | Terraced Fields | John Pavelka | CC BY 2.0 | https://www.flickr.com/photos/28705377@N04/7476657730 |
| terraces_kv/ref_05 | Houses and stepped terraces, rural Nepal, on the road to Pharping | Wonderlane | CC BY 2.0 | https://www.flickr.com/photos/71401718@N00/4536532716 |
| terraces_kv/ref_07 | Rural green terraced countryside, on the road to Pharping | Wonderlane | CC BY 2.0 | https://www.flickr.com/photos/71401718@N00/6443988157 |
| hills/ref_07 | Swayambhunath Stupa, Kathmandu Nepal | eriktorner | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/39267804@N05/13291107743 |
| hills/ref_01 | Kathmandu Valley Sunset | DeeMakMak | CC BY-ND 2.0 | https://www.flickr.com/photos/45469294@N07/5136942634 |
| paddy_gold/ref_00 | Rice Harvest, Nepal | Phil @ Delfryn Design | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/43555170@N00/5342953566 |
| mustard/ref_01 | Drive to Bhaktapur, Nepal: terraced mustard fields | Cheryl Marland | CC BY 2.0 | https://www.flickr.com/photos/13654353@N08/3014058990 |
| park/ref_06 | Morning at Ratna Park | georges | CC BY-NC 2.0 | https://www.flickr.com/photos/35237103804@N01/326667309 |
| park/ref_00 | Ratna Park, Kathmandu, Nepal | theglobalpanorama | CC BY-SA 2.0 | https://www.flickr.com/photos/121483302@N02/14986627545 |
| gardendreams/ref_01 | Garden of Dreams, Kathmandu, Nepal | Matt-Zimmerman | CC BY 2.0 | https://www.flickr.com/photos/16725630@N00/24774932509 |
| gardendreams/ref_03 | Garden of Dreams, Kathmandu, Nepal | Matt-Zimmerman | CC BY 2.0 | https://www.flickr.com/photos/16725630@N00/24774944299 |

Kathmandu facts on jacaranda (text only, no images used): Onlinekhabar, "The beauty of jacaranda in Kathmandu and the
facts" (May 2025); The Kathmandu Post visual stories (2020, 2026).

## 9. Open issues for the integration package (files the nature package does not own)

* **Season from the month.** `WorldRoot` opens in month 10 and feeds `DressingRenderer.Month`, but `TileBuild` meshes
  the terrain and areas with `MeshingSettings.Season` and passes no season to `settings.Areas`. The nature package
  made the defaults agree (`BiomePalette.DefaultSeason` is now the season of `BiomePalette.DefaultMonth` = October,
  so `TerrainOptions.Season`, `AreaOptions.Season` and `MeshingSettings.Season` all default to Autumn) and added
  `BiomePalette.SeasonOf(month)`. Integration: set `settings.Season` and `settings.Areas.Season` from
  `BiomePalette.SeasonOf(WorldRoot.Month)` and rebuild the terrain and area layers when the season changes.
* **Field-line budget per tier.** `AreaOptions.ForTier(tier)` (or `MaxFieldLineTris = AreaOptions.FieldLineCap(tier)`)
  into `MeshingSettings.Areas`; the mesher divides it by the terrain step itself. The default is the Mid tier's.
* **Road corridors.** Once the roads package's `RoadCorridorIndex` is merged, set
  `TreePlacementOptions.Corridors = RoadCorridorIndex.ForTile` in `TileBuild` (OSM tree nudges, chautari fitting and
  park path hedges then follow the drawn corridors; without it they use the ways' widths, at least 4.8 m).
* **View camera.** `DressingRenderer.ViewCamera = WorldRoot.ViewCamera` (it falls back to `Camera.main`); the view cone
  needs the camera that the frame's position comes from.

How to see the fixes in the editor: October terrain is harvest gold out of the box; to see another season, change
`WorldRoot.Month` and (until the season wiring above lands) `MeshingSettings.Season` to `BiomePalette.SeasonOf(month)`,
then reopen the world.
