# Reference brief: heritage correctness (generic sacred framing, building:part houses)

Package **heritage** (W2 detail pass, review fixes). What is really there in Kathmandu, the sizes the generic
generators may take, and how the B0 band draws the `building:part` volumes of the heritage squares. Photos are
reference only (Openverse, open licences, never copied into the repository); the contact sheets are under
`/home/user/wt/refs/heritage/` and the renders under `/home/user/wt/previews/heritage/`.

## 1. What went wrong (review findings, re-measured on base 3f71f0e)

* **Nag Pokhari** (Naxal, osm way 206874129, `building=temple`, one level, pipeline height 7 m): a U of narrow ranges
  round the pond (36 points, 1,105 m² of footprint in a 3,823 m² hull, fill 0.29). The generic framing took the
  convex hull's box: an 89 × 55 m plinth 1 m high and a 96 m pagoda over Primary osm 165226102. 65 of the 233 generic
  sacred structures of the sample put 2,317 m² of blocking collider below 4 m into the drawn road corridors.
* **Kathmandu Durbar Square** (osm way 1415152801, `building:part=yes`, `roof:shape=hipped`, 4 levels, 13.5 m; part of
  outline way 1029965548): drawn as a plain 13.5 m brick extrusion, the only wall of the building facing the square.
  186 `building:part` records of the sample are not drawn by a sacred host; every one was a styled extrusion at B0.

## 2. Real sizes (what a generic temple may be)

From `docs/research/w2/temples.md` §3 and §5 (OSM [O], sources [S], estimates [E]):

| Kind | Real examples (plan, height) | Generic limits used |
|---|---|---|
| One-tier pagoda | small Ganesh, Bhairav and Mahadev temples of the tols, 3-8 m plans, 4-9 m tall [E] | ≤ 9.5 m tall |
| Two-tier pagoda | Jagannath 14.7 × 14.3 m, ≈ 15 m; Vishwanath 12.6 × 11.4 m, ≈ 15 m; Changu Narayan 15.0 × 14.8 m, ≈ 15 m; Pashupatinath 19.5 m, 23.7 m (hero) | ≤ 16 m tall; plan ≤ 24 m (≤ 30 m when mapped with parts) |
| Largest pagodas (heroes) | Maju Dega 22.6 × 22.2 m, ≈ 25 m; Nyatapola 26.8 × 21.7 m with stair, 33.2 m; Taleju 35 m on a 12-stage plinth | heroes only |
| House-temple | Bhimsen (Patan) 11.5 × 11.4 m, ≈ 14 m; Shiva-Parvati 15.8 × 10.9 m, ≈ 10 m | ≤ 16 m; aspect ≤ 2.6 |
| Shikhara | Krishna Mandir 14.3 × 14.2 m, ≈ 20 m; Mahabouddha ≈ 9 × 9 m, ≈ 18 m; Vatsala Durga 7.3 × 6.1 m, ≈ 14 m | plan ≤ 16 m, aspect ≤ 1.6; ≤ 20 m |
| Stupa | Kathesimbhu ≈ 15 m across, ≈ 12 m; Boudha and Swayambhu are heroes | plan ≤ 30 m, aspect ≤ 1.4; ≤ 16 m |
| Shrine | niches, tin canopies, miniature pagodas 1-5 m | plan ≤ 8 m |

Pagodas are square or nearly so (Bhairavnath 16.9 × 14.6 = 1.16, Shiva-Parvati 1.45): a generic pagoda plan stays
within an aspect of 1.8. A tagged or `building:part` height is trusted up to 26 m (no generic temple is taller than
Pashupatinath). A height the generator derives from the plan is also capped by the record's (pipeline-estimated)
height: 1.5 × that height or that height + 3 m, whichever is more.

**Compounds.** An outline whose area is under 75% of its convex hull is a compound (a U or an L of ranges round a
court or a pond, a ring of rest houses), and an outline larger or more elongated than the limits above is a block of
temple buildings or a building used as a temple (Nawadurga, osm 389836684, is `building=mixed_use` with an RCC frame
and 5 levels; Pancha Devalaya, 64 × 59 m, is a complex of five shrines). These are drawn as **Newar compound
buildings** by the house grammar (brick, carved windows, doors, eaves, a jhingati roof where the plot is
rectangular), as tall as the record says, trimmed out of the road like any house.

**Nag Pokhari** ([Wikipedia](https://en.wikipedia.org/wiki/Naag_Pokhari)): a historic artificial pond east of the
royal palace, about 38 × 20 m (125 × 65 ft), with a golden naga on a pillar in the middle, railings and walls round
it and the city's houses beyond (photos: Biraha Prakash Dangol, Naag Panchami 2012, CC BY-NC-SA 2.0; limbun, CC BY-NC
2.0). The mapped U of ranges is drawn as a two-storey brick range round the pond, not a temple.

## 3. Temples beside roads (decision 1)

The drawn road corridor (at least 4.8 m, `RoadClearance.MinCorridorM`) is wider than many real lanes of Asan and
the Durbar Square, so it runs 1-3 m into 33 protected temple outlines of the sample. Rideability wins: the temple
gives way. Its plan shrinks from the side that meets the road (the largest clear rectangle of its outline's box, then
the stair, guardians and posts push back further), keeping at least 35% of each side and a plan no more elongated
than its kind; a pagoda whose door side meets the road drops its stair first (as where real temple steps come down
onto the street); a plan left too small becomes a wayside shrine (a niche or a miniature pagoda; a chaitya for a
stupa) on the clear part. A temple whose plan lies in the road itself (1 in the sample) is marked by a low stone set
into the paving, daubed with sindoor and strewn with marigolds, like the **pikhālākhu** stones set into the street
before Newar doors ([askmeaboutnepal: Palswaan butta](https://www.askmeaboutnepal.com/post/palswaan-butta-the-stone-lotus)):
4 cm proud, no collider, traffic passes over it.

## 4. Durbar Square houses (`building:part` volumes)

Photos: Kumari Ghar and the houses of Kathmandu Durbar Square (Cheryl Marland, CC BY 2.0, Flickr 3016548184,
3016546488, 3015683783; Phil @ Delfryn Design, CC BY-NC-SA 2.0, 5241081454; wonker, CC BY 2.0, 2384239427);
Basantapur Square (hiphopmilk, CC BY-NC-ND 2.0, 29683544476; S Pakhrin, CC BY 2.0, Commons 33970246).

* **Silhouette**: three to five storeys of exposed brick on a stone or brick plinth; storey heights 2.4 / 2.25 /
  2.25 / 2.1 m (ref_buildings.md); a jhingati gable or hipped roof with a deep eave on carved struts; flat terraces on
  the rebuilt hybrids.
* **Square-facing walls**: dense rows of carved timber windows (tiki jhyal lattice, painted black or dark sal), a
  sanjhya bay window projecting on the second floor, carved door frames with toranas on the ground floor, often a
  shop dalan; plain brick only on party walls against a neighbour.
* **Colours**: brick `#B4543A`-`#C25A3C` with darker joints (the grammar's Kathmandu-core palette), timber `#4A2C1C`
  or painted black `#2B221D`, teal and green painted shutters on some (`#3FA39A`, `#2F6E4E`), jhingati `#8A5A44`-`#9C6450`.
* **How it is drawn**: a `building:part` that stands on the ground (min_height under 0.5 m) with at least one storey
  of wall is a house of the grammar: its host outline's profile (BFNT; parts have no front of their own), its own
  levels and height (never a re-drawn row-house storey count), its front on the part edge that lies on the host's
  front or corner edge (the square at osm 1415152801), else its outer edge facing the nearest road or square; its
  other outer walls are street or open sides with windows on every floor (the host outline is never drawn and is no
  neighbour); walls against a sibling part stay blank; a part another volume stands on gets a flat terrace. An upper
  volume (a part from a min_height) keeps the styled extrusion and gets a row of framed, glazed windows per storey on
  every wall that looks onto a road or a square.

## 5. Checks

* `SacredFramingTests.NoGenericSacredStructurePutsABlockingColliderIntoADrawnCorridor`: every generic structure of
  the sample (temples, stupas, shrines, compound buildings), with the roads package's drawn corridors: no blocking
  collider point below 4 m deeper than 5 cm in a corridor; no structure taller than 27 m over its ground.
* `SacredFramingTests.NagPokhariIsACompoundBuildingNotA96MetrePagoda`: under 10 m in B0 and B1, nothing over the road.
* `MeshingBuildingPartTests.NoB0PartWallFacingACorridorOrASquareIsWindowFree`: every B0 part wall of 3 m or more that
  looks onto a drawn corridor or a square carries openings above the ground floor.
* Previews: `/home/user/wt/previews/heritage/` (before/after street views at Basantapur and the Durbar Square part,
  Nag Pokhari, temples beside Asan lanes; side-by-side sheets with the photos).
