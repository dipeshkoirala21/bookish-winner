# Reference brief: Kathmandu Durbar Square, the temples and the square (bespoke models)

Package **mon-kds-temples** (W2 monuments, modeller stage). The research brief is
`docs/research/heritage/kds-temples.md` (structure inventory T1-T31, positions, DANAM measurements, photos); this note
records how each structure is modelled from it, what had to give way to the road corridors and the budgets, and what
the render-vs-photo rounds found. Photos are reference only (DANAM, Wikimedia Commons, Openverse; never copied into the
repository): `/home/user/wt/refs/mon-kds-temples/`. Renders and side-by-side sheets:
`/home/user/wt/previews/mon-kds-temples/`.

## 1. Where the code is

`game/Assets/Ghumante/Core/Generators/Sacred/Heroes/KdsTemples/`:

| File | Structures |
|---|---|
| `KdsTemplesSite.cs` | the site table (id, OSM way, brief position, door bearing, top, class), catalogue patch (`Apply`), placement (`Locate`), dispatch |
| `KdsKit.cs` | the shared kit: looks (brief hex colours), stages and stairs, posts, walls, windows, doors, roofs on measured eaves with the crimson valance, eave fitting to corridors, struts, bells, octagonal roofs, finials (gilt gajur, white bell dome, dark bronze, white stepped pinnacle, flame-arch pinnacle), lions, fences, flame arches, bells |
| `KdsPagoda.cs` | measured roof tiers, window bands, ambulatories, stage stacks |
| `Kasthamandap.cs` | T1 |
| `MajuDega.cs` | T4 and the 0.5 m stone platform w908861401 |
| `TrailokyaMohan.cs` | T8 and the Garuda T9 |
| `Jagannath.cs` | T18 with its four corner shrines, T19 Pratap Malla's column |
| `ChyasinDega.cs` | T14 (octagonal) |
| `ShivaParvati.cs` | T7 |
| `ForecourtTemples.cs` | T16 Lakshmi Narayan, T20 Indrapur, T21 Gopinath, T23 Kageshwor, T24 Mahavishnu |
| `StoneShrines.cs` | T5 white shikhara, T22 Kotilingeshwar, T30/T31 the two Shiva temples by Kasthamandap |
| `KalBhairav.cs` | T17 Kal Bhairav (moved to w908861416, facing north) and T27 the Sweta Bhairav screen |
| `Pavilions.cs` | T2 Silyan Sattal, T3 Ashok Binayak, T11 Laxmi Narayan Sattal, T12 Taleju Bell, T13 Saraswati phalcha, T15 Big Drums |
| `Mahendreshwar.cs` | T25 with its compound, gate and the small shrine w907048005 |
| `Bhagwati.cs` | T26 |
| `Basantapur.cs` | T6 Narayan ruin, T10 Dabali, T28 Basantapur square |

The six heroes that existed keep their ids (`her.ktm.kasthamandap`, `maju_dega`, `trailokya_mohan`, `jagannath`,
`shiva_parvati`, `kal_bhairav`); the 23 new structures are `her.ktm.kds_*` parts of `her.ktm.kasthamandap`
(`HeroRecipe.SiteOf`), added by the hero set with the curated record, each its own hero with its own LODs.

## 2. Placement

* A structure with an OSM outline in the anchor tile (10/516/161) is built in the outline's oriented box; the door
  bearing of the brief snaps onto the nearest box axis (45° steps for the octagonal Chyasin Dega). The ground is the
  lowest terrain under the footprint.
* Unmapped structures use the brief's local frame (origin 27.7045 N, 85.3075 E): Ashok Binayak relative to
  Kasthamandap's outline, Pratap Malla's column relative to Jagannath's, Kotilingeshwar relative to Mahavishnu's, the
  Basantapur square relative to the Dabali's (the brief's positions are 0.5-2 m off the game's outlines; a relative
  offset keeps the neighbours right). The Sweta Bhairav screen finds the west wall of w1415152806 and hangs on it.
* Ashok Binayak stands 1.4 m from the corridor of path w1415316909 at the brief's point; it is nudged (up to 3 m) until
  its cage is clear.

## 3. Roads (contract §1)

The drawn corridors (at least 4.8 m) of the square's mapped paths cross or touch 12 of the outlines (Kasthamandap,
Maju Dega, Trailokya, Laxmi Narayan Sattal, Silyan, Bhagwati, Narayan ruin, Kageshwor, Mahavishnu, both Shiva temples,
the bell). The temples keep their OSM plans; everything else gives way:

* eaves: an eave that would hang under the overhead clearance (4.5 m + margin) over a corridor is first shortened (the
  overhang pulled back toward the wall in 15 % steps); only when even a short overhang would hang into the corridor
  (the wall itself stands at it) is the tier raised as a whole; valances stop at the clearance;
* stairs shorten their run out of a corridor (Trailokya's west stair, Chyasin's stair, Saraswati); the platform at
  Maju Dega's stair foot is dropped if a corridor runs over it;
* fences, railings and compound walls are trimmed at both ends where they enter a corridor (Kasthamandap's picket
  fence, the Narayan ruin's wire fence, Mahendreshwar's compound, Kotilingeshwar's rail);
* the Garuda moves along Trailokya's stair axis to the nearest clear spot (path w1550144348 runs over its real place);
* the Basantapur paving drops every 2 m cell that touches a corridor (kerbs are drawn where it ends) and no stall
  stands within 1.6 m of one.

## 4. Budgets

Per-structure ceilings by class (LOD0 / LOD1 / LOD2 / LOD3): major temples 64 k / 20 k / 6 k / 220, temples
34 k / 11 k / 3.2 k / 220, small structures 16 k / 5.2 k / 1.6 k / 220, the square 30 k / 9 k / 2.4 k / 220; the whole
site at LOD0 at most 150 k (`GeneratorsSacredKdsTemplesTests.SiteFitsItsBudget`). Minor parts (Jagannath's corner
shrines, the small shrine at Mahendreshwar, the side domes of Kal Bhairav, small finials, the Nandis) are drawn one
LOD coarser than their structure so the site's LOD0 fits.
