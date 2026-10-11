# Reference brief: heritage correctness (generic sacred framing, building:part houses)

Package **heritage** (W2 detail pass, review fixes). What is really there in Kathmandu, the sizes the generic
generators may take, and how the B0 band draws the `building:part` volumes of the heritage squares. Photos are
reference only (Openverse, open licences, never copied into the repository); the contact sheets are under
`/home/user/wt/refs/heritage/` and the renders under `/home/user/wt/previews/heritage/`.

## 1. What went wrong (review findings, re-measured on base 3f71f0e; the owner's playtest #1)

* **Hiti Pokhari** (Naxal, osm way 206874129, `building=temple`, one level, pipeline height 7 m): a U of narrow ranges
  round the court of the Narayan temple (POI "Narayansthan"; the round temple is way 909138359)
  across Narayanhiti Path from the Hiti Pokhari pond, opposite the palace whose name it gave (36 points, 1,105 m² of
  footprint in a 3,823 m² hull, fill 0.29). The generic framing took the convex hull's box: an 89 × 55 m plinth 1 m
  high and a 96 m pagoda over Primary osm 165226102. This is the owner's playtest #1 ("riding on a raised brick
  platform with brick retaining edges that spans the whole road"). It is **not** Nag Pokhari, the walled pond with the
  golden naga 500 m east (27.7134 N, 85.3241 E), which the first draft of this brief named by mistake. 65 of the 233
  generic sacred structures of the sample put 2,317 m² of blocking collider below 4 m into the drawn road corridors.
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

**Hiti Pokhari compound.** Narayanhiti takes its name from the Narayan temple opposite the palace and the hiti (stone
spout) east of its main gate ([Wikipedia: Narayanhiti Palace Museum](https://en.wikipedia.org/wiki/Narayanhiti_Palace_Museum);
an 1844 drawing, "Temple of Narayan at Narayanhitti near Kathmandu", [Global Nepali Museum](https://globalnepalimuseum.com/?p=1755)).
OSM maps the temple court's ranges as one U-shaped `building=temple`; it is drawn as a two-storey brick range round the
court (the compound building of the house grammar, trimmed out of the road like a house), with the small round temple
(way 909138359) framed on its own outline in the middle. Openverse holds no open-licensed photo of the court
(searches for Narayanhiti and Hiti Pokhari return the Durbar Square's Narayan temples), so the range keeps the generic
Kathmandu brick and window rhythm.

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

## 5. Heroes beside their kora (playtest #1, whole-sample scan)

**Boudhanath.** "The dome is elevated on three 20-cornered terraces of decreasing sizes" (a giant three-dimensional
mandala). The sample's OSM terraces (ways 56688295 and 235084185) give the real shape: each arm runs out to the half
size a over the middle half of a side, then every corner steps back twice, to 0.82 a and to 0.5 a (five corners a
quadrant: (a, 0.5a), (0.82a, 0.5a), (0.82a, 0.82a), (0.5a, 0.82a), (0.5a, a)). The replica's terraces were a square
with small mid-side projections and square collider boxes, whose corners stood up to 3.6 m into the kora street
(Pedestrian osm 1196490820, 215.8 m²). They are now that mandala (`StupaGenerator.MandalaLocal`): visual rings, caps and
parapets on the 20-cornered plan, the colliders its two arms and inner square, the chortens on the stepped corners and
the 147 prayer-wheel niches spread along its walls. The plan keeps off the drawn carriageways round it
(`StupaParams.Clearance`, `SacredClearance.DrawnDistance`), shrinking by at most 4% (Boudha: 2.3%, 81.5 → 79.6 m).

**Swayambhunath.** The drum and dome collided as one square box (half 14.2 m) whose corners reached 20 m out, over
the kora path round the dome (245 m²). They are round solids now (drum and lower dome, upper dome, harmika and
spire; a square inscribed in the drum keeps a box in every collider set). The kora path (osm 302394468, `SQUEEZED`
corridor 1.1-2.5 m) is drawn 2.5 m wide and overlaps the protected dome outline; the drum gives way by the 4% cap
and the drawn path still touches it by up to 0.12 m at one bearing (open issue for the roads package).

## 6. Checks

* `SacredRideTests.NarayanhitiPathPastHitiPokhariIsRideableEndToEnd`: the tile built as the streamer builds it (High
  tier), the heroes' and the B0 near-ring cells' colliders registered in the layered ground query, a motorbike ridden
  down five lanes of both Narayanhiti Path pieces past the compound, both ways. Base 5a7668a: 9 of 20 rides stuck on
  osm 165226102, the bike lifted up to 2.12 m onto the plinth. Now 20 of 20 through, never above the road, no wall hit.
* `SacredFramingTests.NoSacredPlinthPlatformStepOrWallStandsOnADrawnRoad`: every generic structure and every hero
  replica of the sample against the carriageways as drawn (plinths, platforms, steps, walls, posts and solids with a
  bottom under 4 m and a top over 10 cm; stairs along a steps or foot way are that way). Base 5a7668a: 60 generic
  structures (1,069 m²) and 2 heroes (338 m²); now 0 and 0.
* `SacredFramingTests.NoGenericSacredStructurePutsABlockingColliderIntoADrawnCorridor`: against the roads package's
  corridors (at least 4.8 m): 65 → 0 generic structures; no structure taller than 27 m over its ground.
* `SacredFramingTests.HitiPokhariCompoundIsABuildingNotA96MetrePagodaOverTheRoad`: under 10 m in B0 and B1, nothing
  over the road. `BoudhanathTerracesAreTheTwentyCorneredMandalaOffItsKoraStreet`: 147 niches, arm and square boxes,
  round drum, nothing on the kora street.
* `MeshingBuildingPartTests.NoB0PartWallFacingACorridorOrASquareIsWindowFree`: every B0 part wall of 3 m or more that
  looks onto a drawn corridor or a square carries openings above the ground floor.
* Previews: `/home/user/wt/previews/heritage/pt/` (`sbs_hiti_street.jpg`, `sbs_hiti_air.jpg`, `sbs_boudha_top.jpg`,
  `sbs_kds_part.jpg`: before on base 5a7668a, after, and a photo).
