# Reference brief: the people of the Kathmandu Valley as they look today (detail pass, package "characters")

> Status: written 2026-10-10 for the detail pass (docs/W2_DETAIL_CONTRACT.md). It turns photographs and sources into
> modelling rules for the character generator (`Core/Characters/HumanoidMesher*.cs`, `CharacterRecipe.cs`,
> `CharacterPalette.cs`, `StreetStyles.cs`) and the crowd (`CrowdAnimation.cs`, `CrowdBodies.cs`,
> `World/Instancing/PeopleRenderer.cs`, `Traffic/CrowdPresenter.cs`). `player_and_controls.md` §2 (the player's body
> and wardrobe) and `street_life.md` §1.3 and §3 (who walks where) still hold; this file adds what the photos show,
> with the sizes and colours used.
>
> Photos: Wikimedia Commons (open licences), fetched with a small Commons search script into
> `/home/user/wt/refs/characters/<topic>/` (Openverse was behind a bot check that day). **Reference only: nothing from
> them is copied into the repository or shipped.** Every mesh is procedural; colours are the cartoon palette in
> `CharacterPalette.cs`, checked against the photos. Credits are in §14. Side-by-side renders are in
> `/home/user/wt/previews/characters/compare_*.png`.
>
> Labels: **[photo]** seen in the cited photographs, **[src]** a cited source, **[est]** an estimate.

## 0. What was asked, and what was wrong before

The owner asked for detail that "looks exactly how it looks like in real life in that place". The stage-1 people were
capsules with a sphere head, dot eyes and no hands; every pedestrian in the crowd was the same rigid-part figure
recoloured. This pass rebuilds the person: a sculpted head and a full cartoon face, hands with a thumb and four
fingers, real footwear, garments with collars, plackets, pleats and drapes, the dhaka topi with its weave, and the
dress of each part of the valley. The crowd now uses the same generator (skinned bodies near, baked poses far).

## 1. Who walks where today [photo; street_life.md §1.3]

| Place | What people wear [photo] | Street style (`StreetStyles`) and what the generator does |
|---|---|---|
| **Asan, Indra Chowk, Basantapur** | Mixed: men in shirts, jackets, jeans, many elders in **daura suruwal with a dhaka topi and a dark coat**; women in **kurta suruwal** with a dupatta or in printed saris; porters with dokos and namlos; vendors [street ref_04, ref_06, ref_09] | `OldBazaar` (circle r 700 m round Asan): daura and topi more common, kurta and sari, porters, vendors |
| **Bhaktapur, Patan, Kirtipur, Thimi, Bungamati, Khokana** | Newar towns: elders in **daura with the black bhadgaunle topi**, women in **haku patasi** (black sari with a red border) or printed saris and kurtas, yellow or red headscarves on farmers, schoolgirls in light blue [bhaktapur_women ref_00, ref_02, ref_09; haku ref_01, ref_02; patan ref_03] | `NewarTown`: the kalo (bhadgaunle) topi more than twice as common as in the city, haku patasi, headscarves, chappals |
| **Thamel, Freak Street** | Tourists and trekkers (bucket hats, fleeces, trek trousers, daypacks, boots), shopkeepers, young locals in jackets and jeans [thamel ref_04] | `Tourist`: about half the people are trekkers (`Tourist` archetype), sunglasses, daypacks |
| **Boudhanath, Swayambhu** | Tibetan monks in maroon with the yellow dhonka, a few Theravada monks in orange, Tibetan women in the **chuba** with the striped **pangden** apron, prayer wheels and malas on the kora [monks ref_00, ref_03, ref_04, ref_06, ref_09] | `Buddhist`: monks, chubas, prayer wheels, malas |
| **Durbar Marg, Putalisadak, Singha Durbar, New Baneshwor, Pulchowk** | Office workers: shirts and trousers, blazers, leather shoes, kurtas for women; some topis on officials [est] | `Office`: shirts, blazers, plain trousers, leather shoes |
| **Rest of the metro, suburbs, villages** | Everyday clothes: T-shirts, hoodies, jackets, jeans, joggers; women in kurta suruwal; chappals everywhere; farmers with dokos and headscarves | `Urban`, `Suburb`, `Village` from the area type |

The district circles (lon, lat, radius) are in `StreetStyles.cs`; the crowd picks a body variant per archetype, style,
carry prop and one of four variants (`CrowdVariants.Key`).

## 2. The dhaka topi and the bhadgaunle topi

| Feature | Real [photo / src] | Modelled (`HumanoidMesher.Headwear.cs`) |
|---|---|---|
| Shape | A soft brimless cap; the crown is pinched into a ridge from front to back so it reads as a trapezoid from the side, **higher at the front than at the back** (the "mountain") [topi ref_04, ref_06, ref_07; topi2 ref_02] | 60 × 13 grid (LOD0, `TopiColumns`): walls leaning in steadily to 60% of the base width (a trapezoid from the front and from the side), rounding over into the front-to-back ridge in the top 16% (`TopiPinchStart` 0.84), a trough along the ridge (`TopiFoldDeg` 8°) |
| Size | About 9–10 cm high at the front, 6–7 cm at the back over a head of about 23 cm; laid flat roughly 18 × 26 cm [src: museum catalogue entries; topi ref_07] | Scaled with the cartoon head (0.40 m, 1.74× real): front 0.20 m, back 0.14 m (`TopiFrontM`, `TopiBackM`), the rim 0.068 m above the head centre (about 4 cm over the brows). The 3 : 2 front-to-back ratio is kept |
| How it is worn | Square on the head, the peak above the forehead, **tilted a little toward the left**; never pulled down over the ears [topi ref_00, ref_04; topi2 ref_02] | Tilted 6° (`TopiTiltDeg`); the right side stands higher than the left |
| Dhaka weave | Hand-woven Palpali dhaka: a light ground (cream, white, pale pink, grey; sometimes maroon or black) with **diagonal bands of small stepped diamonds** in rose red, orange, black and leaf green; other weaves show red lattices, pink-black-grey checks with yellow, ikat-like blurs [topi ref_00, ref_08] | Per-quad vertex colours in the `Fabric` channel (`DhakaCell`), one quad ≈ 1.3 cm of real cloth: diagonal rows of single motif dots between dotted dark lines, lattices and checks, each repeating in a divisor of 60 so the cap closes without a seam. Six weaves — Palpali classic (muted, as on most topis worn today) `#B85A6E #E8956E #4A4048 #6E9A78`, red lattice `#C62838 #F07C8C #FFFFFF #2A2224`, ikat `#5A6A7A #D97A8A #3E8E8A #F2C1A0`, check `#D94A78 #2A2A2E #9A9EA6 #F2C230`, earth `#8A2E3A #E8B04A #2A2224 #EDE6D6`, jade `#2E8B57 #D9455B #F2C230 #2A2224`; grounds `#F1E6D6 #EBC9C0 #D9D6D0 #F4EFE6 #7A2433 #232226` |
| Rim | The cloth is folded in at the rim; worn, the edge reads as a slightly darker line [topi ref_07; topi2 ref_02] | A thin first row in the ground colour × 0.84 |
| Bhadgaunle (kalo) topi | The same cut in plain **black** cloth, worn by Newar elders in Bhaktapur and at ceremonies [photo: Bhaktapur squares; daura ref_00] | Same shape, `#1A1A1C`; recipes in `NewarTown` choose it more than twice as often |

## 3. Daura suruwal

| Feature | Real [photo / src] | Modelled |
|---|---|---|
| Daura | Knee-length tunic, **closed round neck**, left-over-right crossover fastened with **eight tie strings (four pairs)**; five pleats in the skirt; off-white, grey, light blue or beige cotton [daura ref_00, ref_01, ref_03] | Lofted torso and skirt to knee − 3 cm with 5 pleats (`DauraPleats`); 2 cm collar ring; the overlap edge from the neck to the wearer's right; tie knots with two strings each (`DauraTies` = 8). Colours `#EDE6D6 #C9C6BE #B8C8D8 #D9C7A3` |
| Suruwal | Trousers loose at the thigh, **tight from the calf to the ankle**, gathered [daura ref_00, ref_03] | Loose thigh curve, fitted ankle (`AnkleFit`), gathers at LOD0 |
| Outer layer | Most men wear a **dark coat** (blazer) or a **waistcoat (istakot)** over it, black leather shoes [daura ref_00, ref_03] | Half wear an open coat (charcoal, navy, brown, black `#3A3D42 #2B3550 #5A4636 #232427`), a quarter a buttoned waistcoat with a pointed V, a quarter the daura alone with its ties showing (`GarmentPlan.Layer`); only the parts in the V are drawn (no ties over the coat) |
| Head | Dhaka topi (city) or bhadgaunle topi (Newar towns) | Always a topi for this archetype |

## 4. Women: sari, kurta suruwal and haku patasi

| Garment | Real [photo] | Modelled |
|---|---|---|
| Sari | Printed or plain sari (red, magenta, orange, green, blue, yellow, maroon, purple) with a contrasting border; the **pallu** crosses the chest from the right hip over the left shoulder; a short blouse; chappals [sari ref_01, ref_03] | Ankle-length skirt with a border band and rolled hem; the pallu as a draped band front and back with its border at the hanging end; blouse sleeves to mid-upper arm |
| Kurta suruwal | Knee-length kurta with **side slits**, matching or contrasting suruwal, a dupatta over the shoulders; pastel and bright colours [street ref_06; bhaktapur_women ref_02] | Kurta to the knee with slits, suruwal, shawl/dupatta accent; ten kurta colours from pink `#F4B6C2` to navy `#2B3A6B` |
| Haku patasi | The Newar **black sari with a red border** (`#1C1C1C` / `#B3141E`). Two ways seen: (a) draped like a sari, the **red-bordered pallu across the chest**, a dark printed blouse with three-quarter sleeves, **a red glass-bead necklace with a flat gold pendant**, gold earrings [haku ref_01, ref_02]; (b) the elder Jyapu way, wrapped at the waist with a white **patuka** sash, a red or coloured blouse and a shawl [bhaktapur_women ref_00, ref_09] | Both: half are draped (pallu with a red front border, dark blouse `#33282E`, red bead strand `#B3121B` with a flat gold pendant, no shawl), half tied with the patuka, a red/maroon/blue blouse and often a shawl. Hem a little above the ankle (the patasi is worn shorter) |
| Pote | Married women's **green glass-bead** necklace with the gold **tilhari** cylinder | Beaded strand `#2E8B3E` with a gold cylinder; on the haku patasi the red strand with a flat pendant |
| Small accents | Red tika, bindi, sindoor in the parting, nose stud, gold earrings | Accent flags: bindi `#C8102E`, tika, sindoor `#D2001F` along the parting, nose stud and earrings in gilt |

## 5. Monks and sadhus

| Who | Real [photo] | Modelled |
|---|---|---|
| Tibetan Buddhist monk (Boudha, Swayambhu) | Maroon robe and **zen** shawl over the left shoulder, the **yellow dhonka** vest with blue piping showing on the right, shaved head, maroon or plain chappals or sneakers, mala, sometimes a maroon knit cap [monks ref_00, ref_03, ref_06] | Robe `#7A1F2B`, dhonka `#E3A21A` with piping `#2E5DA8`, zen drape, shaved head, mala, prayer wheel on the kora; sneakers in maroon or chappals |
| Theravada monk | Orange robe over one shoulder [monks ref_04, ref_09] | Robe colour 1 `#E07B1A` |
| Sadhu (Pashupati) | Saffron and yellow cloth, bare chest with ash, **jata** (dreadlocks piled on the crown), long grey beard, painted forehead (tripundra), rudraksha mala [sadhu ref_00, ref_03, ref_05] | Saffron robe, dreadlocks with a crown bun and ropes, long beard, tilak (three ash lines and a red dot), brown mala |

## 6. Traffic police (Kathmandu Valley Traffic Police)

| Feature | Real [photo] | Modelled |
|---|---|---|
| Uniform | **Light blue shirt**, **navy trousers**, black shoes, a **navy peaked cap** (white-topped in some seasons), **white gloves**, often a face mask; in winter a navy jacket [police ref_00, ref_02] | Shirt `#6E9FD6` with epaulettes, trousers and cap `#1F2D4F`, black visor, white gloves, mask on 30% |
| Vest | Some officers wear a high-visibility vest | About half (`HumanoidMesher.WearsPoliceVest`): lime `#C6E83A` with reflective tapes. Uniforms are never tinted in the far crowd |
| Poses | Five hand signals at junctions | `CrowdAnimation.Officer` (stop front, stop behind, go, slow, right turn) |

## 7. Porters and carried loads

| Feature | Real [photo] | Modelled |
|---|---|---|
| Doko | A conical bamboo basket, straw to light brown, about 50–60 cm tall, carried on the back with a **namlo** strap across the forehead [porter ref_00, ref_01] | Woven grid (alternating strands) `#B8935A`, a rim, a load (greens, fodder or firewood by pattern), the namlo over the forehead (hats are removed when the namlo is worn) |
| Other loads | Sacks, red LPG cylinders on porters' backs, babies in a shawl sling | Sack `#C9B48A`, gas cylinder `#C0392B` with valve guard, baby sling with the baby's head in a knitted cap; 8–12% of pedestrians carry something (W2_DESIGN 5.4) |
| Gait | Porters lean forward and walk at 0.8× | `PedClip.Carry`: forward lean, shorter steps |

## 8. Schoolchildren

| Feature | Real [photo] | Modelled |
|---|---|---|
| Uniforms | **White or light blue shirts with striped ties**, navy or grey trousers or skirts (some schools maroon), grey or navy sweaters, black shoes, white socks; girls with **two plaits tied with ribbons** [school ref_00, ref_02, ref_05] | Shirt `#F5F5F2` / `#9EC9EA`, tie with stripes (maroon, navy, green), bottoms navy `#1F2D4F`, grey `#6B6E73` or maroon `#6B1F2A`, belt, two plaits with ribbons; children are 1.22 m and have bigger heads |

## 9. Tourists and trekkers (Thamel)

Bucket hats, sunglasses, bright fleeces (orange, blue, yellow, green, purple), zip-off trek trousers (khaki, olive,
grey), boots, daypacks [thamel ref_04]. Modelled with the sun hat, fleece (`#E2552D #2F7FC1 #F2B705 #2E9E4F #6B4E9A`),
cargo pockets on trek trousers, trek boots with lace hooks, daypack with straps and buckle. Hair colours include blonde
`#C9A060` and auburn.

## 10. Everyday youth and office workers

Jackets, hoodies, T-shirts, jeans and joggers, sneakers (mostly white); office men in shirts with collars and
trousers with a crease, blazers, leather shoes; women in kurtas [street ref_06, ref_09]. No logos or brand marks on
anything (the sneakers carry a plain stitched side curve).

## 11. The cartoon body and face (player and NPCs)

| Part | Rule (player_and_controls.md §2 and this pass) |
|---|---|
| Proportions | 1.55 m barefoot (build B), head 0.40 m (3.9 heads), shoulders 0.42 m, hips 0.34 m, legs 0.63 m; builds A 1.52, C 1.56, D 1.66 m; children 1.22 m, teens and elders in between; height ±6% per recipe; a soft figure (shoulders ×0.94, hips ×1.06) |
| Head | Superellipsoid e = 2.4, sculpted: jaw taper, cheeks, eye sockets, brow ridge, muzzle, chin; stubble shading for beards |
| Eyes | 14% of the head tall: eye white, iris with pupil and two rings, a limbal line, two highlights (upper left), upper lid with a dark lash line and a lower lid, both turning about the eye's axis so every face state shares one topology; a lash flick on the soft figure |
| Brows, nose, mouth | Swept brows; a rounded nose bulb with wings and nostril shadows; a mouth with lips, teeth, tongue and interior whose shape follows the expression; cheek blush |
| Face states | Smile (default), Neutral, Joy, WinceLaugh, Puff (after running), Calm (temples) and Blink: built as blend-shape targets of the player (`PlayerAvatar`) |
| Ears | Ellipsoid with an inner fold (concha) and a lobe |
| Hands | LOD0: a rounded palm, four two-joint fingers with a rest curl and nails, a two-joint thumb (`FingersLod0` = 5), skinned to the finger and thumb bones; LOD1 a mitten with a thumb; LOD2 one rounded shape |
| Feet | Sneakers (white midsole, toe cap, collar, tongue, three laces, side curve), chappals (two-layer sole, Y strap, the bare foot with five toes), leather shoes, trek boots (lugged sole, shaft, hooks), bare feet |
| Skin | Ten numbered swatches #1 `#F3D3B5`, #2 `#EBC39E`, #3 `#E0B48C`, #4 `#D4A276`, #5 `#C69064`, #6 `#B67F55`, #7 `#A26D47`, #8 `#8C5A3A`, #9 `#744830`, #10 `#5C3826`, each with a shadow, blush and lip tone |
| Hair | 17 styles (side fringe, curls, spikes, bob, long, braid with red tassel, ponytail, bun with pin, top knot, wavy, puffs, dreadlocks, two plaits, quiff with faded sides, shaved, receding) and facial hair (stubble, moustaches, short beard, goatee, long beard); ten colours from black `#1B1A1C` to white `#E6E2DA` |
| Material channels | UV0 u = channel (Skin, Hair, Fabric, Leather, Rubber, Metal, Gilt, Plain ...), v = baked AO (capsule occluders, floor 0.42) per docs/W2_DETAIL_CONTRACT.md §5 |

## 12. Budgets and the crowd

| Level | Cap (triangles) | Typical | Used for |
|---|---|---|---|
| LOD0 | 9,600 body + 2,400 accessories = **12,000** | 9.8–11.4 k | The player (every tier) and the nearest NPC on High |
| LOD1 | **3,000** | 2.0–2.9 k | Near NPCs on Mid and High, the nearest of the mid band |
| LOD2 | **500** | 430–495 | The rest of the mid band (skinned) and the far band (baked poses, instanced with a tint) |

A recipe that would go over its cap is rebuilt with its accessories one step plainer (accents, then hair and back
items, then headwear and footwear; `HumanoidMesher.DetailDrop`), so the caps hold for any combination; 98% of the
crowd's everyday bodies need no drop. With the people caps of W2_DESIGN 10.4 the worst cases are Low 21.0 k, Mid
48.0 k and High 93.0 k against the 21 / 50 / 95 k character slices (`CrowdLodPlan`).

## 13. Deviations and open points

* **Topi size**: W2_DESIGN 6.1 gives 0.11 / 0.075 m, which does not clear the 0.40 m cartoon head; the cap scales with
  the head (0.20 / 0.14 m) and keeps the ratio and the tilt.
* **LOD0 budget**: raised from 5,000 + 600 to 12,000 for the requested detail (fingers, faces, garments); the crowd
  keeps within the slices by giving LOD0 to the player and one NPC only.
* **Far crowd**: the design's VAT crowd is replaced by baked LOD2 frames (six walk phases, stand, sit, pray, arm up)
  drawn with GPU instancing and a per-person tint; no textures.
* Saris and kurtas are plain colours with borders; printed motifs (other than the dhaka weave) are not modelled.
* Hats are removed under the namlo strap; helmets replace all headwear on two-wheelers.

## 14. Photo credits (reference only, not in the repository)

All from Wikimedia Commons; the local cache path is under `/home/user/wt/refs/characters/`.

| Photo (local cache) | Title | Author | Licence |
|---|---|---|---|
| `topi/ref_00` | [Old Nepali Newar Peoples wearing traditional hat- Dhaka Topi-Patan Durbar Square-2061.jpg](https://commons.wikimedia.org/wiki/File:Old_Nepali_Newar_Peoples_wearing_traditional_hat-_Dhaka_Topi-Patan_Durbar_Square-2061.jpg) | Bijay Chaurasia | CC BY-SA 4.0 |
| `topi/ref_04` | [Nepali Topi.JPG](https://commons.wikimedia.org/wiki/File:Nepali_Topi.JPG) | Krish Dulal | CC BY-SA 3.0 |
| `topi/ref_06` | [Nepali Dhaka Topi in folded position.jpg](https://commons.wikimedia.org/wiki/File:Nepali_Dhaka_Topi_in_folded_position.jpg) | Ramnam | CC BY-SA 3.0 |
| `topi/ref_07` | [A Typical Nepali Dhaka Topi laid on a level surface.jpg](https://commons.wikimedia.org/wiki/File:A_Typical_Nepali_Dhaka_Topi_laid_on_a_level_surface.jpg) | Ramnam | CC BY-SA 3.0 |
| `topi/ref_08` | [Dhaka Topi on Display at Patan Durbar Square.jpg](https://commons.wikimedia.org/wiki/File:Dhaka_Topi_on_Display_at_Patan_Durbar_Square.jpg) | Sushan116 | CC BY-SA 4.0 |
| `topi2/ref_02` | [Portrait of an elder in a Dhaka topi - 2026.jpg](https://commons.wikimedia.org/wiki/File:Portrait_of_an_elder_in_a_Dhaka_topi_-_2026.jpg) | Jeena Maharjan | CC BY-SA 4.0 |
| `daura/ref_00` | [Daura Suruwal by Mahalaxmi Silwal.jpg](https://commons.wikimedia.org/wiki/File:Daura_Suruwal_by_Mahalaxmi_Silwal.jpg) | Mahalaxmi silwal | CC BY-SA 3.0 |
| `daura/ref_01` | [Dhakatopinepalidress (cropped).jpg](https://commons.wikimedia.org/wiki/File:Dhakatopinepalidress_(cropped).jpg) | Ak479726 | CC BY-SA 4.0 |
| `daura/ref_03` | [Nepalese people walking in traditional clothing Thamel Kathmandu Nepal - 070A8290.jpg](https://commons.wikimedia.org/wiki/File:Nepalese_people_walking_in_traditional_clothing_Thamel_Kathmandu_Nepal_-_070A8290.jpg) | Bijay Chaurasia | CC BY-SA 4.0 |
| `daura/ref_04` | [Nepali dress boy.jpg](https://commons.wikimedia.org/wiki/File:Nepali_dress_boy.jpg) | see file page (Flickr upload) | CC BY-SA 2.0 |
| `bhaktapur_women/ref_00` | [Old Woman of Bhaktapur - Kathmandu.jpg](https://commons.wikimedia.org/wiki/File:Old_Woman_of_Bhaktapur_-_Kathmandu.jpg) | see file page (Flickr upload) | CC BY-SA 2.0 |
| `bhaktapur_women/ref_02` | [Young Women and Ice Cream - Bhaktapur - Nepal.jpg](https://commons.wikimedia.org/wiki/File:Young_Women_and_Ice_Cream_-_Bhaktapur_-_Nepal.jpg) | Adam Jones | CC BY-SA 2.0 |
| `bhaktapur_women/ref_09` | [Woman in Courtyard - Bhaktapur - Nepal (13486423423).jpg](https://commons.wikimedia.org/wiki/File:Woman_in_Courtyard_-_Bhaktapur_-_Nepal_(13486423423).jpg) | see file page (Flickr upload) | CC BY-SA 2.0 |
| `haku/ref_01` | [Women wearinhg tradtional Newari dress - Haku Patasi 01.jpg](https://commons.wikimedia.org/wiki/File:Women_wearinhg_tradtional_Newari_dress_-_Haku_Patasi_01.jpg) | Nabin K. Sapkota | CC BY-SA 4.0 |
| `haku/ref_02` | [Women wearinhg tradtional Newari dress - Haku Patasi 03.jpg](https://commons.wikimedia.org/wiki/File:Women_wearinhg_tradtional_Newari_dress_-_Haku_Patasi_03.jpg) | Nabin K. Sapkota | CC BY-SA 4.0 |
| `sari/ref_01` | [Women in Sari- Morang District Nepal-1598.jpg](https://commons.wikimedia.org/wiki/File:Women_in_Sari-_Morang_District_Nepal-1598.jpg) | Bijay Chaurasia | CC BY-SA 4.0 |
| `sari/ref_03` | [A Nepali Woman's at Sari.jpg](https://commons.wikimedia.org/wiki/File:A_Nepali_Woman%27s_at_Sari.jpg) | Bhupendra Shrestha | CC BY-SA 4.0 |
| `patan/ref_03` | [Street scene in Patan, Nepal.jpg](https://commons.wikimedia.org/wiki/File:Street_scene_in_Patan,_Nepal.jpg) | Radosław Botev | CC BY 3.0 pl |
| `street/ref_04` | [Kathmandu, Basantapur, People, Nepal.jpg](https://commons.wikimedia.org/wiki/File:Kathmandu,_Basantapur,_People,_Nepal.jpg) | Argenberg | CC BY 4.0 |
| `street/ref_06` | [Kathmandu, Nepal, Life on the streets.jpg](https://commons.wikimedia.org/wiki/File:Kathmandu,_Nepal,_Life_on_the_streets.jpg) | Argenberg | CC BY 4.0 |
| `street/ref_09` | [Streets of Kathmandu 05.jpg](https://commons.wikimedia.org/wiki/File:Streets_of_Kathmandu_05.jpg) | Nabin K. Sapkota | CC BY-SA 4.0 |
| `thamel/ref_04` | [Kathmandu, Nepal, Life on the streets of Thamel.jpg](https://commons.wikimedia.org/wiki/File:Kathmandu,_Nepal,_Life_on_the_streets_of_Thamel.jpg) | Argenberg | CC BY 4.0 |
| `school/ref_00` | [Nepali-School-Uniform.JPG](https://commons.wikimedia.org/wiki/File:Nepali-School-Uniform.JPG) | Seeteufel | CC BY-SA 3.0 |
| `school/ref_02` | [School girls in Bhaktapur.jpg](https://commons.wikimedia.org/wiki/File:School_girls_in_Bhaktapur.jpg) | see file page | CC BY-SA 2.0 |
| `school/ref_05` | [Band of Brothers.jpg](https://commons.wikimedia.org/wiki/File:Band_of_Brothers.jpg) | Gaurav Dhwaj Khadka | CC BY-SA 4.0 |
| `porter/ref_00` | [Doko 001.jpg](https://commons.wikimedia.org/wiki/File:Doko_001.jpg) | Nirmal Dulal | CC BY-SA 3.0 |
| `porter/ref_01` | [Doko carried by porters to carry goods in Nepal-3069.jpg](https://commons.wikimedia.org/wiki/File:Doko_carried_by_porters_to_carry_goods_in_Nepal-3069.jpg) | Bijay Chaurasia | CC BY-SA 4.0 |
| `police/ref_00` | [Traffic Police officer during Maha Shivaratri Celebrations near Jaya Bageshwori Road, Kathmandu, Nepal-070A6986.jpg](https://commons.wikimedia.org/wiki/File:Traffic_Police_officer_during_Maha_Shivaratri_Celebrations_near_Jaya_Bageshwori_Road,_Kathmandu,_Nepal-070A6986.jpg) | Bijay Chaurasia | CC BY-SA 4.0 |
| `police/ref_02` | [Traffic-controllers - Kathmandu, Nepal - panoramio.jpg](https://commons.wikimedia.org/wiki/File:Traffic-controllers_-_Kathmandu,_Nepal_-_panoramio.jpg) | see file page (Flickr upload) | CC BY-SA 3.0 |
| `monks/ref_00` | [Monks at Boudhanath.jpg](https://commons.wikimedia.org/wiki/File:Monks_at_Boudhanath.jpg) | Bgag | CC BY-SA 4.0 |
| `monks/ref_03` | [Tibetan Buddhist monks at Boudhanath Stupa.jpg](https://commons.wikimedia.org/wiki/File:Tibetan_Buddhist_monks_at_Boudhanath_Stupa.jpg) | Chris Shervey | CC BY 2.0 |
| `monks/ref_04` | [Theravada monk at Boudhanath bell.jpg](https://commons.wikimedia.org/wiki/File:Theravada_monk_at_Boudhanath_bell.jpg) | CFynn | CC BY-SA 4.0 |
| `monks/ref_06` | [Two monks at Boudhanath.jpg](https://commons.wikimedia.org/wiki/File:Two_monks_at_Boudhanath.jpg) | CFynn | CC BY-SA 4.0 |
| `monks/ref_09` | [Theravada monks at Boudha.jpg](https://commons.wikimedia.org/wiki/File:Theravada_monks_at_Boudha.jpg) | CFynn | CC BY-SA 4.0 |
| `sadhu/ref_00` | [Sadu Kathmandu Pashupatinath 2006 Luca Galuzzi.jpg](https://commons.wikimedia.org/wiki/File:Sadu_Kathmandu_Pashupatinath_2006_Luca_Galuzzi.jpg) | Lucag | CC BY-SA 2.5 |
| `sadhu/ref_03` | [PAS - Sadhu at Pashupatinath Temple, Kathmandu, Nepal, 2016.jpg](https://commons.wikimedia.org/wiki/File:PAS_-_Sadhu_at_Pashupatinath_Temple,_Kathmandu,_Nepal,_2016.jpg) | Josep M. Gracia | CC BY-SA 4.0 |
| `sadhu/ref_05` | [PAS - Sadhus at Pashupatinath Temple, Kathmandu, Nepal, 2016.jpg](https://commons.wikimedia.org/wiki/File:PAS_-_Sadhus_at_Pashupatinath_Temple,_Kathmandu,_Nepal,_2016.jpg) | Josep M. Gracia | CC BY-SA 4.0 |

Sources for the topi measurements: museum catalogue entries for Nepali dhaka topis (laid flat about 18 × 26 cm, height
3–4 in) and published descriptions of how the topi is worn (tilted, the crown pinched into a ridge); see the folded and
laid-flat photos above (topi ref_06, ref_07).
