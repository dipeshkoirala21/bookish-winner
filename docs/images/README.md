# README images

Two kinds of images live here:

* `docs/images/*.jpg`: renders of the game's own generators from the offline mesh-preview tool
  (`tools/mesh-preview`), made where Unity cannot run. They show the geometry, not the game.
* `docs/images/game/`: **real gameplay screenshots**, captured in the Unity editor by the screenshot tour below. Every
  image there is a frame of the running game: the streamed 1:1 world, traffic, people, animals and the explorer, without
  the HUD. `docs/images/game/README.md` (written by the tour) lists them.

## Capture the gameplay screenshots (on a Mac with Unity)

You need Unity **6000.3.25f1** (Unity Hub) and this repository checked out. One run takes about two minutes and
writes 24 JPEGs (12 places, landscape 1920 x 1080 and portrait 1080 x 1920).

1. **Open the project.** In Unity Hub: *Add* > *Add project from disk* > select the `game` folder of the repository >
   open it with 6000.3.25f1. Wait for the first import to finish.
2. **Project Setup.** Menu **Ghumante > Project Setup** (once per checkout, and again after pulling changes that touch
   the settings). It builds the render pipeline assets, the materials and `Assets/Ghumante/Scenes/Bootstrap.unity`.
3. **Bootstrap scene.** Open `Assets/Ghumante/Scenes/Bootstrap.unity` (Project window, double-click). The tour opens
   it for you if another scene is open, after asking to save that scene.
4. **Run the tour.** Menu **Ghumante > Capture Screenshot Tour**. It enters Play mode, opens Explore, and for each place
   teleports the explorer there (the region's search confirms the place, `WorldRoot` streams it), waits until the
   streaming is settled and the traffic, people and animals have spawned, sets the place's time of day, frames the
   camera and saves both orientations. A line at the top left of the Game view shows the progress.
5. **Wait about two minutes.** Keep the Game view visible and Unity in the foreground; do not touch the keyboard or
   mouse in the Game view meanwhile. When it is done the Console says
   `ScreenshotTour: 24 images in .../docs/images/game` and Finder opens the folder. Stop Play mode.
6. **Commit and push** from the repository root:

   ```bash
   git add docs/images/game && git commit -m "Gameplay screenshots from the screenshot tour" && git push
   ```

### The places

| File stem | Place | Time |
|---|---|---|
| `thamel_lane` | Thamel Marg at street level, chase camera behind the explorer | 15:30 |
| `asan_tole` | Asan Tole: the gilded Annapurna temple between the market houses | 11:30 |
| `basantapur_durbar` | Kathmandu Durbar Square from the Basantapur side: Maju Dega, Kasthamandap down the square | 10:00 |
| `patan_durbar` | Patan Durbar Square from the south: the Krishna Mandir and Yoganarendra Malla's column | 10:00 |
| `bhaktapur_nyatapola` | Taumadhi Square, Bhaktapur: Nyatapola above its stair guardians | 10:00 |
| `boudhanath` | Boudhanath from the gate lane on the south | 09:30 |
| `swayambhu_stair` | The last flight of the eastern stairway up to Swayambhunath | 09:00 |
| `pashupati_east_bank` | Pashupatinath across the Bagmati from the east-bank shrine terraces | 09:00 |
| `maitighar_mandala` | Maitighar Mandala from above the junction | 10:30 |
| `shahid_gate` | Shahid Gate from the south, across its roundabout | 11:00 |
| `thapathali_bridge` | The Bagmati bridge at Thapathali from the riverside road | 09:30 |
| `valley_rim_golden_hour` | Evening over the city from Swayambhu hill, the valley rim beyond | 17:18 |

The exact eye and look points, times and fields of view are in
`game/Assets/Ghumante/World/Debug/ScreenshotTourAnchors.cs`; the photo references they were matched to are in
`docs/research/w2/ref_shots.md`. EditMode tests (`ScreenshotTourTests`) check every place against the bundled sample
region, the hero catalogue and the sun.

**Patan and Bhaktapur** are outside the bundled `kathmandu_core` sample. To capture them, import the full
`kathmandu_valley` region first (**Ghumante > Import Region Pack...**, a pack built by `pipeline/`); Explore then opens
the valley. With only the sample the tour skips those two and says so in `docs/images/game/README.md`.

### Single shots (F12)

In the editor and in development builds, while exploring:

* **F12** saves the camera view as the player sees it, without UI, at 1920 x 1080 (1080 x 1920 when the Game view is
  portrait).
* **Shift+F12** saves the screen with the HUD (the debug overlays hidden).

Files go to `docs/images/game/shots/shot_<date>_<time>_<orientation>.jpg` in the editor, and to
`<persistentDataPath>/Screenshots/shots/` on a device. Release builds contain neither the tour nor the hotkey (the code
compiles only for the editor and development builds).

### If something goes wrong

* *"There is no Bootstrap scene yet"*: run **Ghumante > Project Setup** first.
* *"Explore did not open"*: no region is installed. Project Setup copies the bundled sample into StreamingAssets; or
  import a region pack.
* *"streaming not settled ... shooting anyway"*: a slow first import; run the tour again (tiles are cached by then).
* A place is missing from the README: the Console line `ScreenshotTour: skipped <place>` says why.

Screenshots are Produced Works of OpenStreetMap data (docs/LICENSES.md 1.1): keep the map-data notice that the tour
writes into `docs/images/game/README.md` wherever the images are shown.
