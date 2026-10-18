# Reference brief: street life the player meets (package play)

Scope: what stands at the kerb and what moves on the road around the player in the Kathmandu Valley, the playtest items
the play package owns (docs/W2_PLAYTEST_1.md #3 parked vehicles, #6 lane-following traffic, #10 sprint lock, #11 drive
the parked vehicles), and where the heavy vehicles a player may borrow really stand. The vehicle models themselves are the
vehicles package's (`ref_vehicles.md`); this brief is about **where** and **how many**.

Photos were collected with `refimg.py` (Openverse, open licences) into `/home/user/wt/refs/play/` for reference only;
none is copied into the repository. Credits are in section 6.

## 1. Parked two-wheelers: what a Kathmandu street really looks like

What the photos show (contact sheets `street_bikes`, `parked_bikes`, `traffic`):

* **Residential and side streets** (World Bank "SDM-NP-124", a Kathmandu side street with cars and a taxi): motorbikes
  stand **nose-in, perpendicular to the kerb**, in a short row against the house fronts and shop shutters, rear wheel at
  the carriageway edge. The carriageway between them stays open for cars, taxis and bikes. Nobody parks a bike
  crosswise in the middle of a lane.
* **Old-core courtyards and lanes** ("Typical Kathmandu, old meets modern"): two or three bikes tucked beside a shrine
  plinth or a doorway, off the walking line, never across the lane.
* **Bazaar streets** (Thamel, Asan, New Road; "Street Scene in Kathmandu", "Kathmandu Traffic" by C. Guillebeau): the
  street is full of **people and moving scooters**, not rows of parked bikes; parking there is a traffic-police offence
  and the kerb is taken by shop goods and pedestrians.
* **Main roads** (Ring Road, Kantipath, New Baneshwor; "Kathmandu traffic" by RobiNZ): moving traffic in two or more
  lanes per direction, buses at the kerb at stops, no parked two-wheelers along the carriageway.

Rules (facts): the Ring Road's 25 m greenbelt is a declared no-parking zone and the Metropolitan Traffic Police are told
to enforce it across their 35 units in the valley; parking in a restricted place is fined Rs 1,500 under the Motor
Vehicle and Transport Management Act (Himalayan Times). The Kathmandu Valley Traffic Police book about a hundred
roadside or improper parking cases a day in 2026 (ShareSansar daily tallies), so the main roads and bazaars are watched.

What the game does with it (`ParkedPlacement`, `ParkedPlacementTests`):

| Rule | Value | Source |
|---|---|---|
| Two-wheelers only beside home streets (residential, living street, unclassified, service, road) | never trunk/primary/secondary/tertiary, never market streets | photos above; owner #3 |
| Orientation | perpendicular to the road edge within 12°, nose to a house within 8 m, rear wheel at the road | SDM-NP-124 |
| Density | one slot per 26-44 m of kerb in the city (measured: one bike per 30-250 m of city residential kerb), fewer in suburbs, none in forests | owner: "only some" |
| Clearance | the whole drawn carriageway stays free (every vehicle stands beside the road); a lane under 6 m keeps all of it; at least 2.7 m always | owner: "3 bikes side by side" |
| Cars | parallel at the kerb of urban residential streets, about one per 100 m, facing the flow of their side (left-hand traffic) | photos |

## 2. Moving traffic near the player

* **Mix.** Two-wheelers dominate the valley's traffic: 27.3 % of Nepali households own a two-wheeler against 3.1 % a
  car (Census 2021, via nepaldrives.com), Bagmati holds the largest share of them (30.87 %), the Gurjudhara office
  alone has registered 1.58 million two-wheelers in Kathmandu (2025), and two-wheelers are in about 78 % of the valley's
  road crashes (2021-22). On any street the player stands in, most moving vehicles are scooters and motorbikes, with
  white taxis, micro vans, buses and the odd lorry on the main roads.
* **Left-hand traffic.** Nepal drives on the left; every vehicle keeps to the left lane of its direction and overtakes on
  the right. Buses and micros stop at the left kerb.
* **Busy bazaars.** The owner: "On busy area, instead of parked motorcycles, bring up the actual person driving it
  instead. But not too many.. 2-3 motorcycles driving, so that it does not look abandoned." The traffic keeps
  `TrafficSettings.NearRiders` (2 on Low, 3 on Mid and High) moving two-wheelers within 70 m of the player, started out
  of view on a lane 35-70 m away with 30 m clear behind them, so a busy street is never empty; the ambient mix adds more
  on the main roads.

Measured on the sample (`TrafficNearPlayerTests`, 8 places, 3 tiers, 2 minutes each after a minute's warm-up;
`/home/user/wt/previews/play/traffic_near_player_{before_main64155f3,after}.txt`):

| Place, tier | Before (main 64155f3): within 60 m / 120 m, riders within 70 m, longest stop | After |
|---|---|---|
| New Baneshwor, Low | 0.0 / 6.1, 0.0 riders, 119 s (a gridlock at the junction cluster) | 2.0 / 7.6, 1.7 riders, 66 s |
| Baluwatar lanes, Low | 0.0 / 2.3, 0.0 riders | 2.0 / 8.0, 2.4 riders |
| Thamel, Low | 0.5 / 3.9, 0.0 riders (0 % of the time) | 1.7 / 7.6, 1.9 riders (100 %) |
| Thamel, Mid | 0.9 / 8.0, 0.6 riders | 3.1 / 16.3, 4.1 riders |
| Bakhundol lanes, High | 0.0 / 14.2 (queued), 119 s | 1.4 / 13.8, 15 s |
| every place and tier | wrong-side samples 0 | wrong-side samples 0, mean lane offset 0.20-0.46 m |

## 3. Heavy vehicles: where they really stand (playtest #11)

* **Bus parks.** Ratna Park (the Old Bus Park by Tundikhel, city buses and micros to every part of the valley; photo
  "A Tempo at Ratna Park"), Naya Bus Park at Gongabu on the Ring Road (long-distance buses), Chabahil and the mapped
  micro stations (Old Baneshwor, Tinkune, Kalanki, Sinamangal). The green Sajha city bus, cream minibuses, white micro
  vans and Safa (electric) tempos wait there nose to the exit.
* **Depots and works yards.** Balaju industrial area (dairy, flour mill, feed factory), the Pepsi plant south-east of
  the airport, smaller yards to the east: tippers, lorries, a tanker, pickups, off the road in the yard.
* **Farms.** Two-wheel power tillers with a trailer and blue four-wheel tractors are everyday traffic in the valley's
  farm belts (photos "Daily Commute, typical Boudha street scene" and "Plowing tractor traffic, Kathmandu Valley"):
  the game parks tractors by the lanes of Nagdesh and Thimi east of the airport and Tinchuli north of Boudha.

The game marks every one of these as a community-fleet vehicle (green key tag): the player walks to the driver's door
(right side: right-hand drive) and drives it with its own handling and camera rig (bus, truck, tractor, van, car).
`game/README.md` lists the places.

## 4. Sprint lock (playtest #10)

Mobile open-world and shooter games lock the run by **dragging the stick past its rim** (the "sprint lock" of PUBG
Mobile and Call of Duty Mobile: a lock icon appears above the stick) and PC games by **double-tapping sprint**
(auto-run). The game does both: double-tap Shift (gamepad X) or push the touch stick up past 1.6 x its radius within
30° of straight up for 0.25 s; the stick's rim lights up. Pulling back, a single sprint tap, the Action button, a wall
for 0.8 s, a vehicle, an emote, a calm zone or the pause menu stop it.

## 5. Previews

`core-tests/ParkedScenePreview.cs` dumps street-level scenes of the sample as the streamer builds them on High, with
every parked vehicle where `ParkedPlacement` puts it and the High-tier traffic after 90 s; rendered in
`/home/user/wt/previews/play/scene_*.png` (Ganesh Galli and Kalopul lanes, Baluwatar, Thamel Marg, New Road,
New Baneshwor, Ring Road at Chabahil, Ratna Park and Gongabu bus parks; `*_bird.png` from above). Photo against render:
`compare_parked_bikes_photo_vs_ganesh_galli.png` (bikes nose-in and perpendicular at the house fronts, as in the photo,
but only a few and never in a bazaar, by the owner's rule), `compare_traffic_photo_vs_new_road.png` (a rider keeping
left on New Road, no parked rows), `compare_ratna_park_photo_vs_game.png` (white micros with the green band and the green
city bus in the yard). Top-down before/after maps of the parked vehicles: `/home/user/wt/previews/play/parked_*.png`.

What the renders showed and what stays open: the bazaar shots show no parked two-wheelers and moving riders; on the main
roads (New Baneshwor, the Ring Road near Chabahil) oncoming micros keep to the far side of the centre line or median.
At Ratna Park the green bus stands on the grassy edge beside the yard rather than on its compacted middle (open: a
bus-sized spot on the yard surface would be closer to the photos); at Gongabu it stands beside the terminal building with three micros
nose-in in a row.

## 6. Photo credits (reference only)

* World Bank Photo Collection, "Street traffic in Kathmandu" (x3), "Street parking for bicycles in Kathmandu",
  "SDM-NP-124 World Bank", "Street Scene in Kathmandu" (CC BY-NC-ND 2.0, flickr.com/photos/10816734@N03).
* Wonderlane, "Child at play ... Bodha", "The passenger on the random motorcycle ...", "Street view: motorcycle helmets
  and shrine buildings", "Two Nepalese men riding the bags, on a tractor to market", "'Daily Commute' Typical Boudha
  street scene" (CC BY 2.0, flickr.com/photos/71401718@N00).
* suzienewshoes, "Typical Kathmandu - old meets modern" (CC BY-NC-ND 2.0).
* jwalanta, jackol, Chris Guillebeau, SqueakyMarmot, stewie811, ArtisteInconnu, ironybored, RobiNZ: "Kathmandu traffic"
  photos (CC BY / BY-SA / BY-NC / BY-NC-SA 2.0, Flickr).
* Happy Sleepy, "Plowing tractor traffic, Kathmandu Valley" (CC BY-NC-SA 2.0); CIMMYT farm tractor photos
  (CC BY-NC / BY-NC-SA 2.0).
* CS Sharada Prasad, "Kathmandu - A Tempo at Ratna Park" (CC BY 2.0).

Sources: Himalayan Times, "Ring Road greenbelt declared no-parking zone" and "Vehicle parking banned along Ring Road";
ShareSansar, Kathmandu Valley Traffic Police daily tallies (June 2026); nepaldrives.com, "Only 3.1 % Nepalese families
own cars, 27.3 % own two-wheelers, census finds"; Himalayan Times, "Casualties among bikers alarmingly high in valley".
