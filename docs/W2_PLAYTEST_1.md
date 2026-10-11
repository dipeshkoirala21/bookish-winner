# First Unity playtest (owner, Mac M1, Unity 6000.3.25f1, 11 Oct 2026)

The first time the game ran in Unity. Everything below was seen in Play mode on the `kathmandu_core` sample, High tier.
Each item is binding for the playtest-fix round. Fixed before this round: the Unity compile error (CS0012), the blank
screen (Panel Settings), the traffic crash (`TrafficSim.BuildOccupancy`), the "Discovered: ..." toasts (#13), and the
world material set with a broken script link.

## Owner feedback (verbatim)

1. Road is being blocked by some structure and i cannot drive through this road.
2. Bridges is not walkable or drivable at all. Characters gets sucked in. Character basically disappears when trying to cross the bridge.. Also, the same bridge acts as a overpass in areas like ratopul, kalopul, setopul, pahenlo pul.. but none of those underpass works.. The bridge wont let us pass through.
3. There are way too many motocycles or scooter parked in the roads, narrow streets.. User cannot properly drive in these area.. Do not show a parked motorcycles or scooters in main streel like ring roads, Baneshwor roads...or in busy market areas. Only some motorcycles will be parked infront of the houses in residential area, that too in vertical position alongside the road and footpath.. Right now, there are way too many motorcycles parked horizontally in roads which takes up too many space. On busy area, instead of parked motorcycles, bring up the actual person driving it instead. But not too many.. 2-3 motorcycles driving, so that it does not look abandoned area.
4. Buildings constantly flickers and stutters... Make then stable and solid.
5. Camera angle is shitty.. It constantly keeps moving which make the driving experience really bad. reach about the camera position while riding on a different vehicles like cars, motorcycle, scooter, bus etc and try to implement that camera angle.
6. There are absolutely no traffics on the road. Try to show few traffics on the roads that follow the lanes properly.
7. Some of the traffic poles, roads signs and  electric poles are in the middle of the roads/intersection. Fix that. Traffic lights should be hanging on top. Electric poles should either be in the middle of the roads where dividers are there in actual roads in kathmandu, or they should be in the footpath and road corner.
8. Also, on some roads, character feet looks like they are being sucked in. Fix that.
9. Roads have been widen, but the actual driving space is same.. Try to widen the roads as well.
10. Add locking mechanism while user is sprinting. so that the player do not have to keep pushing it forward.
11. Add Car, Bus, and truck driving, Airplane flying as well for the character. Allow the user to grab and start riding the parked vehicles around, including planes.
12. Add mountainous peaks in the far background and add more detail to the gameplay. Make the buildings very solid. Game should follow proper physics.
13. Discovered "Kalopul", "Kathmandu" texts are annoying.. Remove that. (DONE: only the face reacts now)
14. Cannot drive between the houses even if there is a space. That should be allowed.
15. Overhead Sidewalks do not work. I tried crossing the road using that, and i could not even reach the top. Fix it.

## What the owner's screenshots show

* **Hiti Pokhari (Naxal):** riding on a raised brick platform with brick retaining edges that spans the whole road, so
  the yellow route runs over it. This is almost certainly the generic temple framed from the U-shaped Nag Pokhari
  compound outline (OSM way 413748258): a 55 x 89 m plinth over Primary osm 165226102 (final review finding).
* **On a bridge deck** ("shree tripura sundari grill udhyog"): a wide divided road with railings. Walking onto it, the
  explorer sinks and disappears (#2).
* **Ganesh Mandir lane (old core):** parked motorbikes drawn as red LOD boxes stand across the middle of a lane about
  4-5 m wide, turned crosswise, so nothing can pass (#3).
* **Kalopul:** scooters parked at angles stand in the carriageway.
* **Hiti Pokhari, between buildings:** HUD says "Paved, off road" and a building near the camera is drawn dithered or
  see-through (occluder fade). This is likely part of the "flicker" (#4).

## Triage (who fixes what in this round)

| # | Area | Package |
|---|------|---------|
| 1, 14 | Plinths or solids blocking roads; house solids wider than the drawn houses | heritage, ride2 |
| 2, 15 | Bridge decks and foot overbridges not walkable or rideable; underpasses blocked by bridge solids | ride2 |
| 3, 6 | Parked vehicles: none on main roads or market streets; few, perpendicular, at the kerb in residential streets; 2-3 moving riders instead; visible lane-following traffic | play |
| 4, 12 (solid) | Flicker: B0 cell swaps, ring double-draw, occluder fade, z-fighting; solid houses that match what is drawn | stable, perf-cells |
| 5 | Chase cameras per vehicle class, steady and researched | camera |
| 7 | Poles, signs and signals in carriageways; signals hang over the road on gantries or mast arms; electric poles on dividers or footpath edges and corners | props |
| 8 | Feet sink into some roads (drawn surface vs ground query height) | ride2 |
| 9 | Drivable width equals the drawn road (no invisible walls or kerbs narrowing it) | ride2 |
| 10 | Sprint lock (cruise) | controls |
| 11 | Drive cars, buses and trucks from parked ones; fly planes from the airport apron | play (ground vehicles), controls (aircraft) |
| 12 | Himalayan peaks on the horizon, more life and detail, solid physics | sky, stable |
