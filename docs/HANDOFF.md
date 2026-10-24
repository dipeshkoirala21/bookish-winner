# Hand-off (24 Oct 2026)

`main` plus this branch hold every package that passed all gates (full core suite, Unity compile audit for Android,
iOS and macOS Editor, UI, localization, text spike). Pashupatinath (fixer round) and Boudhanath are the last two in.

The packages below were not merged because each needs real work. Each is pushed as its own branch.

| Branch | Owner item | What it does | Why it is not in main | Where to start |
|---|---|---|---|---|
| `w2d/ride` | #1, #8, #9, #14 | Feet and wheels on the drawn road surface, full drawn width drivable, house solids on the drawn outlines, Kalanki underpass and Asan passages | Its junction-cap clamp keeps corner houses whole, so the Mid B0 building budget breaks: Asan keeps 5 near houses (test wants more than 5), and the Thamel-Asan ride peaks at 108,606 of 105,750 triangles | Make near/lite houses cheaper or improve B0 shedding (`B0CellSet.RaiseHeadroomTris`). Re-merging needs commit ce8d87f's fix: the road index reads only the full kit, not the far copy |
| `w2d/cutfill` (merged attempt: `w2d/integ-roads-cutfill-attempt`) | #16 | Hill roads level across, terrain cut and filled, gabion and stone walls, guard rails, bikes and animals tilted to the slope | Carved terrain in view goes over the terrain triangle budget (Mid 70k, High 110k); Boudha reaches Mid 127,687 and High 176,069 | A cheaper, tier-aware carve; then recheck the float/sink tests. On the attempt branch only `TerrainInViewFitsTheTierSlice` fails |
| `w2d/bridges2` | #2, #15 | Walking and riding on bridge decks, underpasses not blocked, climbable overbridge stairs | Unfinished. Rewrites the same `TileGroundQuery` ground lookup as ride, in a different way | Rebuild the deck-versus-ground choice on top of ride's drawn road surface, after ride lands |
| `w2d/mon-kds-palace` | #17 | Hanuman Dhoka palace, 16 bespoke parts, photo-judged fixes F1-F17 | High resident meshes at Basantapur go over 250 MB (adds about 15 MB against 2.2 MB of headroom). No hero colliders on Low (colliders only at LOD0) | Cut hero triangles about two thirds, or keep LOD0 resident only near the player. Build colliders at any LOD (see `PashupatiSite`) |
| `w2d/mon-kds-temples` | #17 | Kathmandu Durbar Square temples and square | Same budget at Basantapur (adds about 8 MB). Same Low-collider gap | As for the palace. The palace and temples must share one level ground at the square |
| `w2d/mon-swayambhu`, `w2d/mon-pashupati-river` | #17 | Research briefs only (`docs/research/heritage/`) | Not modelled yet | Model them the way Pashupatinath and Boudhanath were built |

Other open notes: `/home/user/wt/open_issues.md` was session-local. Its live items are covered by the table above and
by the open-issues sections of each package's `docs/research/w2/ref_*.md`.
