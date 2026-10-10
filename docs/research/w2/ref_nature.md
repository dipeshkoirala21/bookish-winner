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
* Four LODs: LOD0 (≤ 1 600 tris) and LOD1 (≤ 240) per species, then a family volume (≤ 112) and a family impostor
  (≤ 8) shared by the shape family (round, cone, umbrella, column, fountain). `LodsKeepTheSilhouette` keeps the width,
  top and crown base of LOD1 and LOD2 within 20 % / 0.1 / 0.12-0.22 of LOD0, so LOD switches do not pop.
* Bloom, flush and harvest are baked into the near models per month; the far models are grey and tinted with the
  month's crown colour (`FloraCatalog.FoliageColour`: leaf mixed with bloom × bloom cover).
* Vertex alpha 255 marks foliage (wind sway and instance tint), 0 fixed parts; UV0 = (material channel, baked AO).

## 2. Trees

| Kind | Where in the valley | Silhouette and proportions (model H × W; placed H) | Colours | Details modelled | Photos |
|---|---|---|---|---|---|
| Pipal (Ficus religiosa) | Chautari squares, temple courts, OSM trees [S] | Broad dome on a short, pale, fluted trunk that forks low; crown as wide as tall (18 × 18; 15-25 m) | leaf `#4E8A3A`, bark `#9A948A`, copper flush `#C27C5E` in March-April | Buttress flutes, red-white puja band on the trunk, chautari platform under OSM trees (6 × 6 m, 0.8 m, porter ledge on the south) | pipal/ref_00 |
| Bar (banyan) | Chautari, old crossroads [S] | Very wide low umbrella (16 × 24; 15-20 m) on three stems with hanging aerial roots | leaf `#2F6B2F`, bark `#8A8274` | Aerial-root pillars, dense dark crown | bar/ref_01 |
| Jacaranda | Avenues (Kamaladi, Tripureshwor, Durbar Marg) [S] | Open umbrella on a forked dark trunk (12 × 12; 8-15 m) | leaf `#5E8F45`, bloom `#A58AD8` March-May (far `#8E6CC8`), bark `#5A4A3E` | Violet clumps, petal carpet disc under the tree in bloom | jacaranda/ref_00, ref_02 |
| Silky oak (Grevillea robusta) | Avenues, school and office grounds [S] | Tall teardrop / narrow pyramid (22 × 8; 18-30 m), branches ascending 25-50° | leaf `#557A3A`, bloom `#F2A33A` April-May, bark `#5E4A3A` | Fern-like frond cards, golden comb cards and spikes over the crown in bloom; far LODs golden in April-May | silkyoak/ref_07, ref_09 |
| Bottlebrush (Callistemon) | Avenues, gardens [S] | Small weeping tree (6 × 5; 4-8 m), drooping outer leaves | leaf `#4F7D3A`, bloom `#D7263D` March-May (light second flush Oct-Nov) | Red brush tubes at the twig tips, drooping fringe | bottlebrush/ref_00 |
| Camphor | Parks, compounds | Dense round crown (15 × 13; 10-20 m) | leaf `#3F7F3A`, bronze flush in spring | Layered clumps | (W2_DESIGN 5.8) |
| Eucalyptus | Roadsides, institutional plantations [S] | Tall pale trunk, sparse high crown (28 × 11; 15-35 m) | leaf `#7FA08A` (blue-grey), bark `#D9D2C3` | Peeling pale bark patches, open shell crown | eucalyptus/ref_00 |
| Chilaune (Schima wallichii) | Valley-rim forests 1 300-1 800 m [S] | Dense rounded crown on a clean bole in forest (15 × 11; 10-22 m) | leaf `#4A7F36`, white flowers `#F4F1E6` May-June, red new leaves in spring | Many dark glossy clumps, flower dots | schima/ref_08, ref_01 |
| Katus (Castanopsis indica) | Valley-rim forests with Schima [S] | Round dense crown (15 × 12) | leaf `#527F36`, catkins `#D9C46A` March-April | Pale catkin tassels | (S 11) |
| Utis (Alnus nepalensis) | Stream banks, landslide scars, plantations [S] | Straight pale trunk, narrow oval crown (16 × 10; 10-22 m), branches 30-55° | leaf `#4C8B3E`, bark `#8C8A80` | Light-green clumps on short ascending branches | alder/ref_02 |
| Chir pine (Pinus roxburghii) | Nagarjun, Chandragiri and Kirtipur plantations, dry south faces 1 400-1 800 m [S] | Tall straight plated trunk bare to half height; open crown of tiers of near-horizontal branches ending in needle tufts (20 × 8; 12-25 m), rounding off with age | leaf `#4F7A3A`, bark `#7A4A2E` with darker plates | Drooping needle cards round every branch tip; three branches per tier at staggered heights (no stacked plates) | chirpine2/ref_06, ref_00 |
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

* Real forest polygons and forest biome cells hold clumps of 3-7 trees (2-4 m apart) at ~30 clumps per hectare,
  ≤ 10 000 trees per tile, crowns interlocking into a closed canopy; under them a dark, shaded floor with leaf-litter
  patches (forest areas `#4E8A3A` × 0.62 mixed with litter `#5C5634`, AO 0.72).
* Elevation bands (W2_DESIGN 5.8, S 11): fringe 1 300-1 400 m (Schima, Castanopsis, utis, broadleaf);
  Schima-Castanopsis to 1 800 m, with 60 % of the weight swapped to chir pine on south faces; oak-laurel with
  rhododendron and some bamboo to 2 400 m; brown oak and rhododendron above; sal below 1 000 m.
* Understorey: ferns and shrubs in the broadleaf bands, grass and rocks on the open pine floor, ferns and rocks in the
  oak forest.
* Checked against: terraces_kv/ref_09 (the valley rim), hills/ref_07 (Swayambhu's wooded hill): from afar the rim
  forests read as a continuous dark green canopy with rounded crowns.

## 5. Fields, terraces and banks (terrain and area meshers)

* **Patchwork:** cropland (valley cropland, hill terraces, farmland areas) is split into world-fixed plots
  (26 × 17 m on a district-wide axis) coloured by season: monsoon greens `#5DAA3A` / `#8CC84A`; October-November
  gold `#D9B44A` (55 %), green `#8FB848`, bare `#C9A86A` (ploughed, Dirt channel); winter mustard and wheat; spring
  mixed. Crops cover the flats and terraced slopes up to ~38°.
* **Terraces:** on cropland slopes of 0.1-0.85 rise over run, a riser band below every world-fixed level
  (1.9 m apart, riser 0.55 m; grassy `#6E8C40` in the monsoon and autumn, dry `#9A8A5A` in winter and spring), lifted
  0.13 m on the exact terrain triangles; on the flat, grassy bunds (`#8FB060`, winter `#A8B070`) along the plot lines.
  Over the triangle budget (24 k per tile) every second level is drawn first (3.8 m steps, taller risers), then whole
  48 m blocks thin evenly. Checked against terraces_kv/ref_04, ref_05, ref_07 and mustard/ref_01: golden or green
  terraces separated by green grassy risers following the contours.
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
* **Clearances:** everything keeps out of road corridors (big trees whose crown starts above 4.5 m may overhang a
  road and only keep the trunk 1.2 m clear), buildings and water; path hedges keep clear of every other way so
  crossings stay open.

## 7. Gaps and estimates

* No usable photos were found for banana, bamboo clumps, potted plants on Kathmandu doorsteps, ferns, oak forest or
  river rocks (searches returned unrelated subjects); those kinds follow the written sources and general knowledge
  [E]. Poinsettia, bougainvillea and marigold photos are from outside Nepal but show the same cultivars.
* Season dates for second flushes (bottlebrush in autumn, a light autumn jacaranda sprinkle) are [E].
* Terrace step and riser heights (1.9 m, 0.55 m) are averages read from the photos [E]; real terraces range from
  1 to 3 m.

## 8. Photo credits (sheets only; not in the repository)

| Set / file | Title | Author | Licence | Source |
|---|---|---|---|---|
| pipal/ref_00 | A grand Ficus Religiosa tree and its trunk is colourful | Bigul Malayi | CC0 1.0 | https://wordpress.org/photos/photo/36865d5027/ |
| bar/ref_01 | Banyan Tree Root Shrine | Mabacam | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/59275783@N04/6679434051 |
| jacaranda/ref_00 | Street lined with Jacarandas | martie1swart | CC BY 2.0 | https://www.flickr.com/photos/65724937@N02/6315327499 |
| jacaranda/ref_02 | Jacaranda Tree in Bloom | Michael J. Linden | CC BY-NC 2.0 | https://www.flickr.com/photos/57514457@N04/5496083866 |
| silkyoak/ref_07 | Grevillea-robusta in full blossom | expom2uk | CC BY 2.0 | https://www.flickr.com/photos/57768042@N00/280379631 |
| silkyoak/ref_09 | Grevillea robusta (Silky Oak) as a street tree | Tatters | CC BY-SA 2.0 | https://www.flickr.com/photos/62938898@N00/24966962842 |
| bottlebrush/ref_00 | Roadside Callistemon | coofdy | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/57754952@N06/15277538995 |
| eucalyptus/ref_00 | Five Eucalyptus Trees. Aged Nine. 2010. | amandabhslater | CC BY-SA 2.0 | https://www.flickr.com/photos/15181848@N02/5114989034 |
| schima/ref_08 | Schima wallichii (3) | siddarth.machado | CC BY-NC 2.0 | https://www.flickr.com/photos/127280380@N06/27672864730 |
| schima/ref_01 | Schima wallichii | FarOutFlora | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/46821817@N08/6415032041 |
| alder/ref_02 | Alnus nepalensis 100515-0834 | Tony Rodd | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/8108294@N05/4887841770 |
| chirpine2/ref_06 | Pinus roxburghii | Soumil | CC BY-NC 4.0 | https://www.inaturalist.org/photos/209815770 |
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
