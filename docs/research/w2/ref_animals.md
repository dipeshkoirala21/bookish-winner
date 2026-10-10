# Reference brief: animals and birds of the Kathmandu Valley (package `animals`)

What the fauna generators (`Core/Generators/Fauna`) model, from open-licensed photos taken in the valley where
possible (Openverse search, photos looked at as contact sheets; reference only, nothing is copied into the repo),
`docs/research/w2/street_life.md` §7–8 and W2_DESIGN §5.5–5.6. Sizes are real; the cartoon style only smooths
surfaces and slightly enlarges heads and eyes. Hex colours are what the mesh renders at the default instance tint
(sampled from the photos where marked *s*, else from street_life / W2_DESIGN).

## 0. What is actually on the streets now (2023–2026)

* **Cattle.** Kathmandu's ≈1,200 street cattle (KMC) are abandoned old cows, oxen and bull calves: a mix of humped
  hill zebu and dairy crossbreds (Jersey and Holstein crosses, the black-and-white and red-brown ones), lying on the
  Ring Road, in Durbar Squares among the pigeons and round Boudha. Most are thin, with hip bones showing.
* **Dogs.** 14.2 roaming dogs per km of street (ICAM): medium pariah-type dogs, often ginger-tan with thick winter
  coats and plumed curled tails, sleeping flat on their side or curled on plinths in the sun.
* **Monkeys.** Rhesus macaques at Swayambhu (≈450) and Pashupati: sitting on walls, chaityas and gilt roofs, holding
  food in both hands, mothers carrying babies on the belly or back.
* **Birds.** The valley's commonest urban birds (Bird Conservation Nepal counts): rock pigeon, house crow, house
  sparrow, barn swallow, common myna; black kites circle over the city; cattle egrets follow buffalo in the paddy.
* **Village yards.** Khari goats, local hens and roosters, water buffalo and pond ducks in Bungamati, Khokana,
  Kirtipur and the peri-urban fringe.

## 1. Street cow (`Cow`, `Calf`) and zebu ox (`Bull`)

Photos: Durbar Square cows lying among pigeons; red-brown cow in the Asan market; cream cows at Boudha; white zebu
lying on Boudha's brick paving; black cow grazing.

| Feature | Real | Model |
|---|---|---|
| Height at withers | 1.0–1.25 m (hill zebu smaller, crossbreds taller) | 1.20 m cow, 1.42 m ox, 0.83 m calf |
| Body | narrow deep barrel, straight back, sloping rump, hip (hook) bones visible on thin animals | superellipse loft, sloping rump knots |
| Hump | over the withers; low on crossbreds, 12–15 cm on cows, 25–30 cm and floppy on oxen | dome ellipsoid on the chest bone, ×0.35 on crossbreds, ×1.9 on the ox |
| Dewlap | loose fold from throat to brisket, large on zebu, small on crossbreds | flattened tube, depth ×1.45 ox, ×0.45 crossbred |
| Head | broad flat forehead with a tuft of hair at the poll, face 40–45 cm, held forward-down | 7-knot loft, length ×0.9, poll tuft |
| Horns | short (10–20 cm), rising up and out with the tips curving forward/in (lyre); thick at the base on oxen | 5-knot tapered tube, cream base `#D9CDB3` to dark tip `#3E3630` |
| Ears | medium, held out sideways and a little down, pale inside | leaf tube, inside `#E2BDB0` |
| Eyes | large, dark brown; white zebu have black skin round the eye | dark eyeball, black rim (not on crossbreds), half-closed lid |
| Muzzle | wide, dark grey-black on zebu (`#534940` *s*), pink on Holstein crosses | muzzle knots `#55504C` / `#CFA595` |
| Legs | thin and bony, knee and hock knobs, dusty lower legs; cloven dark hooves | 7-knot legs, hock point, two claws per hoof |
| Tail | long, past the hocks, dark switch | 6-knot tube + switch `#38322E` |
| Coats | white `#EDE7DA`, cream/fawn `#C2AC85` *s* / `#D8C9A8`, grey `#9D9890`, black-and-white `#2B2B2B`/`#EFEFEF`, red-brown `#7C4819` *s* / `#8B5A3C` | coat tint × pattern: Plain, Patched (crossbred), Socks (white face and feet), Saddle (dark neck and hump of a grey ox) |

Behaviour (street_life §7.1): lie chewing the cud on the sternum, front knees folded under, one hind leg out to the
side (60%); stand at vegetable waste chewing (30%); walk slowly (10%); tail swishing, ear flicks.

## 2. Water buffalo (`Buffalo`)

Photos: buffalo herds walking on a Kathmandu road ("holding up traffic"), wallowing in village ponds, Changu Narayan.
Height 1.25–1.35 m; broad, deep, almost hairless slate-grey to black barrel (`#3C3C3E`, `#2E2C2C`); short thick
neck carrying the head low and level; long face with a wide wet black muzzle; flat ridged horns sweeping out and back
in a crescent (`#4A4643`); ears sticking out sideways under the horns; pale chevrons on the throat and brisket
(`#B9B1A6`); pale lower legs on some animals; big splayed hooves; tail to the hocks with a small tuft.

## 3. Street dog (`Dog`)

Photos: ginger dogs lying and sitting in a Kirtipur lane, a tricolour dog asleep on its side on Patan paving, a dog
under a traffic policeman's motorbike, Boudha monastery dogs.
Height 45–55 cm (model 0.52 m at the shoulder), body length about the height; wedge head, black nose, amber eyes;
pricked or semi-pricked triangular ears (one often tipped over); deep chest, tucked belly; medium coat with a fuller
neck ruff in winter; sickle tail curled over the back, plumed. Coats (street_life §7.2): tan `#C49A6C` 40% (often
ginger `#B2672E` *s*, black-muzzled), black `#2A2A2A` 20%, black-and-tan 15%, cream `#EFE3C8` 10%, patched and
brindle 15%; white socks and chest blaze common. Poses: asleep curled or flat in the sun (day 60–70%), sphinx lying,
sitting, scratching an ear, trotting, barking at night.

## 4. Khari goat (`Goat`)

Photos: goats with cows in village sheds, goats in Bandipur and Kathmandu lanes, a girl with a kid.
Height 50–60 cm; compact, deep rumen barrel on thin legs; straight face, small muzzle, chin beard; amber eyes with a
horizontal slot pupil; medium leaf ears held out and down; short horns sweeping back; short tail flicked up. Coats:
black (most common) `#2A2624`, brown `#7A4A2C`, white `#EDE8DE`, pied, tan with dark face stripes and legs.

## 5. Rhesus macaque (`Macaque`, `MacaqueBaby`)

Photos: Swayambhu (sitting on chaityas and gilt roofs, mothers with babies, grooming pairs) and Pashupati (sitting on
the riverside terraces holding food).
Sitting height 40–50 cm, all-fours shoulder about 42 cm; brown-grey shoulders and arms (`#8C7A64`, `#927963` *s*),
golden-orange lower back, thighs and rump (`#B07E4E`); paler belly; bare pink face (`#D8907E`) ringed with pale fur;
close-set amber-brown eyes under a heavy brow; short muzzle; small rounded ears; grey-pink hands and feet with
fingers; tail 20–25 cm hanging in a curve. Babies: half size, big head and ears, darker fur, pinker face; ride on
the mother. Behaviour: sit (most of the time), groom, climb walls and trees, walk on all fours, bound when they
move fast. Never aggressive, never fed by the player.

## 6. Village fowl (`Hen`, `Rooster`, `Duck`)

Photos: roosters in Nepali backyards and on Bhaktapur steps, hens scratching in Kathmandu lanes.
Hen 35–40 cm: red-brown (`#9C5A2E`), buff, black or white, small red single comb and wattles, tail held up, yellow
legs. Rooster 45–55 cm: tall serrated red comb (`#D82828`), long wattles, golden-orange hackle cape (`#E0A030`) and
saddle, red-brown back, black breast, arching green-black sickle tail (`#1E3A2E`), or all white. Duck: white or
mottled brown, upright, flat orange bill and webbed orange feet (`#F09A2A`).

## 7. Birds

| Species | Size | Look (photos) | Behaviour |
|---|---|---|---|
| Rock pigeon (`Pigeon`) | 32 cm, span 66 cm | pale blue-grey back and wings `#8F99A5` *s*, darker head, iridescent green/purple neck `#4E7A6A`/`#7B5C8E`, two black wing bars `#2F2F35`, dark tail band, orange eye, dark bill with a white cere, red feet; feral variants darker chequer, pale, brownish | flocks of 30–200 on Basantapur and Patan squares and temple roofs; peck in a 3–10 m disc, burst up together, circle 30–60 m, re-land in 20–40 s |
| House crow (`Crow`) | 42 cm, span 80 cm | glossy black, grey nape/neck/breast collar `#6E6E70`, heavy black bill | groups on wires and roofs, hop, fly 8–12 m/s |
| Black kite (`BlackKite`) | 58 cm, span 1.5 m | brown `#5C4632`, paler streaked head `#8F7558`, shallow forked tail, long angled wings with fingered primaries and a pale underwing patch, yellow cere and feet | soars and circles 20–50 m radius, 40–300 m up, banking 15–30°, twisting the tail |
| Common myna (`Myna`) | 24 cm | vinous brown `#5A3E2B`, black head, yellow bill, bare yellow eye patch and legs, white wing patch and vent | struts on lawns, short flights |
| House sparrow (`Sparrow`) | 15 cm | male: grey crown, chestnut nape, pale cheeks, black bib, streaked brown back `#8B6B4A`, white wing bar | groups of 5–30 at eaves and shopfronts, hop, flush together |
| Cattle egret (`Egret`) | 52 cm | white, yellow bill, dark legs; buff head, breast and back in breeding (Apr–Jul) | walks behind buffalo and tractors in the paddy |
| Barn swallow (`Swallow`) | 18 cm with streamers | blue-black back `#1C2A44`, rufous forehead and throat `#A8492E`, dark breast band, white belly, tail streamers | low swoops over fields and rivers, Mar–Oct |

Far flocks are 8-triangle paper birds (two wing quads in a V, both faces) in the species' wing colour.

## 8. Photo credits (Openverse; all CC licences as listed, reference only)

* "Kathmandu - Cows and pigeons in Durbar Square", Phil @ Delfryn Design, CC BY-NC-SA 2.0, flickr 5240497381
* "Cow looking for food in the market", Francisco Anzola, CC BY 2.0, flickr 12653499773
* "Cow Train", SamHawleywood, CC BY 2.0, flickr 6619647411
* "White cow back fur, green flag stones, Boudha", Wonderlane, CC BY 2.0, flickr 13322051034
* "Cows and people carrying umbrellas ... Boudha" and "Like shopping at Home Depot, Cow ... Boudha", Wonderlane, CC BY 2.0
* "A white cow poses for a photographer" (Boudha), via Openverse (cow_nepal/ref_09)
* "Holding up traffic, sweet black water buffalo", via Openverse; "Water buffalo, Nepal", पानी परयोजना, CC BY-ND 2.0
* "Kirtipur, Nepal" (street dogs), via Openverse (dog_nepal/ref_06); "A tired dog will sleep anywhere", via Openverse
* "Sleeping dog, Nepal 2010", JasSpace, CC BY-NC-ND 2.0, flickr 11008397036
* "Rhesus macaque at Swayambhunath", Photoman Phil, CC BY-ND 2.0, flickr 12679801605; "DSC_0301"…"DSC_0541"
  (Swayambhu), RachidH, CC BY-NC 2.0
* "Monkey, Pashupatinath (1)–(8)", Prof. Mortel, CC BY-NC-SA 2.0
* "Blue Rock Pigeon (Columba livia) in Kolkata", J.M.Garg, CC BY-SA 3.0; "Plinth with Pigeons - Durbar Square -
  Patan", Adam Jones, CC BY-SA 2.0; "Basantapur Durbar, King Malla's Column", Jorge Lascar, CC BY 2.0
* "House Crow (Corvus splendens)", Lip Kee, CC BY-SA 2.0; "House Crow I IMG 6211", J.M.Garg, CC BY-SA 3.0
* "Black Kite (Milvus migrans) Goes Fish-Hunting", See-ming Lee, CC BY-NC 2.0; "Black Kite in Flight", via Openverse
* "Common Myna", Koshyk, CC BY 2.0; "House Sparrow Male", Kurayba, CC BY-SA 2.0
* "Cattle Egret in Breeding Plumage", Charles Patrick Ewing, CC BY 2.0; "Cattle Egret with Cow", rebeccakoconnor,
  CC BY 2.0
* "Rooster in backyard", World Bank Photo Collection, CC BY-NC-ND 2.0; "Domestic Chicken (Rooster)", Fryderyk
  Supinski, CC BY 2.0
* "Cows and goats, Nepal", Happy Sleepy, CC BY-NC-SA 2.0; "Cow, goat, Nepal", Eufmd, CC BY-NC-SA 2.0
* "Barn Swallow", Martin_Heigan, CC BY-NC-ND 2.0; "Macaque baby with mom", Tambako the Jaguar, CC BY-ND 2.0

Facts: KMC street cattle and ICAM dog density (street_life §7, sources there); NPR "Kathmandu Is Cowed By Abandoned
Cattle" (2015) and KAT Centre on why the cattle are on the street; Bird Conservation Nepal urban bird count (2022).
