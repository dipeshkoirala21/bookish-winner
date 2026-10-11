# Reference brief: stable, solid buildings (package `stable`, playtest #4 and #12)

The owner, playtest 1 (11 Oct 2026, Mac M1, High): "Buildings constantly flickers and stutters... Make then stable and
solid." and "Make the buildings very solid." Screenshot at Hiti Pokhari, on foot between two houses: a house next to the
camera drawn dithered or see-through while the explorer is in plain view. This brief records how third-person games keep
walls solid and what the measurements on the `kathmandu_core` sample showed. It is about rendering technique, not an
object, so there are no photo sheets; the "reference" is the established practice below and the owner's screenshot.

## How third-person games handle walls between camera and player

* **Move the camera first.** The standard chase camera sweeps a sphere from the target back to the wanted camera position
  and pulls in to the first hit; the sweep radius should be close to the camera's near-plane extent, otherwise the near
  plane slices into walls the camera brushes past (Unity forum, "Camera object occlusion in 3rd person games").
* **Fade only what really occludes, and only the player's line.** Fading components (Needle Engine `SeeThrough`,
  Opsive `Object Fader`) fade objects that obscure a reference point on the character from the camera, with alpha-hash
  (dithered) fading so the objects stay in the opaque queue. A Bayer 4 × 4 screen door between an outer (opaque) and an
  inner (clear) radius around the line of sight is the common shader form (Godot shader "Camera occlusion dither").
* **Do not let it flicker.** A fade driven by a per-frame test flickers whenever the test flips (a pole or a corner
  crossing the line); the usual cure is hysteresis: different conditions (and a delay) to fade out and to fade back in,
  plus a time ramp of the fade amount. The searches did not turn up a canonical write-up; the gate here uses a 0.35 s
  engage delay, a 0.6 s release delay and 0.3 s / 0.45 s ramps.
* **Silhouette instead of fade.** Some games draw the hidden character as a silhouette (a second pass with a reversed
  depth test, or a separate camera on a character layer) rather than dissolving the world. Considered here; not built in
  this round (it needs the player's and the ride vehicle's materials to opt in, outside this package: open issue).

## Shadows

* URP supports only **stable fit** cascades (bounding sphere per cascade, snapped to texels): camera translation and
  rotation do not make the shadow edges crawl. What still moves them is the **light** turning: every rotation of the sun
  re-rasterises every shadow edge (a GameDev.net thread on stable CSM notes rotation still shimmers). The sky package
  turns the sun in 0.1° steps, one step every 0.8 s at the default 48-minute day: open issue (fewer, larger steps).
* Texel size near the camera is what the eye sees on the facades: shadow distance and the first cascade's split set it.
  Unity's cascade guidance: more cascades cost draw calls; the tiers keep their cascade counts and map sizes
  (ARCHITECTURE 10) and shorten the reach instead (≈ 9–15 cm per texel near the camera on every tier, from 13–18 cm).
* The **cascade border** fades the last 20% of the reach, so the edge of the shadowed area never sweeps over a facade as
  a hard line; the **conservative enclosing sphere** keeps casters at the cascade corners (no popping at the screen edge
  as the camera turns).

## What the sample showed (core-tests, kathmandu_core, High)

| Measurement | Before | After |
|---|---|---|
| House wall dissolved while the explorer is in plain view (worst frame), Hiti Pokhari between houses | 20.9 m² | 0 |
| ... Asan lanes (worst frame) | 13.0 m² | 0 |
| ... Kalopul residential walk (worst frame) | 1.7 m² | 0 |
| Frames with walls dissolved in plain view, all rides | > 1 400 | 0 |
| Wall patches (≤ 0.3 m) flipping solid/dissolved three times or more, worst ride | 1 249 | 0 |
| A house really hiding the explorer: line to the chest cleared after | at once (and all the time) | 0.35 s + 0.3 s ramp |

The chase camera's boom (a 0.3 m sphere pulled in to the first solid) kept the explorer in sight on every ride at the
three spots: the fade is needed only when something the boom cannot avoid hides the player.

Sources:
- [Camera object occlusion in 3rd person games (Unity Discussions)](https://discussions.unity.com/t/camera-object-occlusion-in-3rd-person-games/432872)
- [Needle Engine SeeThrough component](https://engine.needle.tools/docs/api/@needle-tools/engine/5.0.10/classes/Built-in_Components.SeeThrough.html)
- [Opsive Object Fader](https://opsive.com/support/documentation/ultimate-character-controller/camera/object-fader/)
- [Camera occlusion dither (Godot Shaders)](https://godotshaders.com/shader/camera-occlusion-dither/)
- [Stable cascaded shadow maps (GameDev.net)](https://gamedev.net/forums/topic/707159-stable-cascaded-shadow-maps)
- [Unity render pipelines feature comparison (stable fit only in URP)](https://docs.unity3d.com/cn/2023.2/Manual/render-pipelines-feature-comparison.html)
- [Unity 6.3 manual: shadow cascades performance](https://docs.unity3d.com/Manual/shadow-cascades-performance.html)
