# Reference brief: the streamed world at street level (integ-world)

The integration package builds no new models; it decides what the streamer puts on screen together (bridges, roundabout
dressing, houses, trees, parked vehicles, heroes) and where riders can go. This brief lists what a Kathmandu local must
recognise at street level in the places the integration scenes check (`core-tests/StreamingSceneTests.DumpStreetScenes`,
renders in `/home/user/wt/previews/integ-world/`, render against photo in `fix/street_level_vs_photos.jpg`), what the
current build shows, and what is left for the owning packages. Photos are reference only (Openverse/Flickr, open
licences, never copied into the repo).

## Camera rule for the scenes

A rider's eye: 1.8-2.2 m over the drawn surface, on a drawn carriageway (a motor road over a footway beside it), well
inside the road corridor (houses are trimmed to it, so the eye is outside every footprint), off every hero plinth,
looking down the street with a clear line of sight (sampled every metre: no house footprint or hero structure between
eye and target). Hero shots look at a named hero (`HeroId`) from the point on a 60 m circle that crosses the fewest
house walls and hero structures.

## Places

| Place | Real (photos) | Build now | Left |
|---|---|---|---|
| **Thamel street** (Thamel Marg area) | 4-7 storey concrete houses wall to wall, 4-6 m paved street, shop signs and banners on every floor, motorbikes parked nose-in along both kerbs, wires overhead (refs/roads/thamel_street_kathmandu ref_00, ref_02) | Paved street between tight colourful houses, signs and awnings, parked bikes along both kerbs, a cow; eye on the carriageway | Overhead wires and dense hanging signs (buildings) |
| **Asan lane** | Narrow brick-and-concrete lane, shops open on the ground floor with goods hung out, crowds, bikes squeezed along the walls (refs/roads/asan_tole_kathmandu ref_00) | Lane between brick and plaster houses with carved windows, bikes parked along one kerb with gaps, no overlaps | Hanging goods at shopfronts (buildings); people come from the crowd presenter at runtime |
| **Basantapur / Taleju** | Taleju on a twelve-stage stepped plinth of brick and stone with small corner shrines on the stages; no trees on the plinth; the paved square around it (refs/temples/taleju ref_02, ref_08) | Taleju hero on its stepped plinth with guardian lions at the stair; no tree stands on the plinth any more: a mapped tree there is moved beside it, or dropped when it cannot be (`TileBuild.KeepTreesOffHeroes`) | Corner shrines on the plinth stages and a weathered stone/brick plinth colour (temples) |
| **Basantapur square** | Open square fully paved in brick, Gaddi Baithak (white, neo-classical) on its east side, the Basantapur palace wing and Nasal Chowk to the north, the temples of Durbar Square beyond (refs/temples/basantapur2 ref_00, ref_02) | Gaddi Baithak white on the right, the long palace wing on the left, the pagodas beyond; a brick lane across the square | The square reads as bare sandy terrain around the lane, not brick paving (areas/terrain: pedestrian squares need the paving); a small windowless white box stands in the square (buildings: a tiny mapped structure drawn as a plain box) |

## Photo credits

- Taleju Temple, Durbar Square, Kathmandu, Nepal: eriktorner, CC BY-NC-SA 2.0, https://www.flickr.com/photos/39267804@N05/13255615385
- Taleju Temple, Kathmandu Durbar Square (3): Prof. Mortel, CC BY-NC-SA 2.0, https://www.flickr.com/photos/43714545@N06/6848315161
- Market Street, Asan Tole, Kathmandu Old Town: eriktorner, CC BY-NC-SA 2.0, https://www.flickr.com/photos/39267804@N05/13255232605
- Nepal - Kathmandu - 001 - streets of Thamel: mckaysavage, CC BY 2.0, https://www.flickr.com/photos/56796376@N00/492181084
- Thamel street, Kathmandu: andreweland, CC BY-NC-ND 2.0, https://www.flickr.com/photos/46267632@N00/2003052300
- Looking southwest from Basantapur Tower, Kathmandu Durbar Square: MATomlinson, CC BY-NC-SA 2.0, https://www.flickr.com/photos/73111045@N05/16769915114

## Open issues for other packages

- **Nature / data:** the OSM tree near Taleju is classed *Bottlebrush* and drawn 24 m tall; bottlebrush (Callistemon)
  grows 5-8 m in the valley. Clamp a tagged height to the species' range (or ignore an implausible tag).
- **Buildings:** small mapped structures in Basantapur square and the B1 band near the Taleju plinth draw as plain
  windowless flat-roofed boxes; at street level they read as wrong in a heritage square.
- **Areas / terrain:** pedestrian squares (Basantapur, Hanuman Dhoka) and the ground between houses in the old core
  draw as bare sandy terrain; the real ground is brick or stone paving.
- **Temples:** Taleju's plinth stages carry small corner shrines in the photos; the plinth colour should be weathered
  brick and stone, not uniform red.
