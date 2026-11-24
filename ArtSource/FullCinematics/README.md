# Complete Blender story cinematics — authoring phase

This is the source work for the complete requested redesign. The game’s frozen 0.4 candidate and its verified
three-second inserts are unchanged. Full-source scenes are reviewed before final RTX rendering and runtime integration.

## Complete opening

`Opening/OpeningFull.blend` covers all 80 seconds at 1920×1080, 24 fps (1,920 frames), with 11 bound cameras.
The locked shot/caption plan is `Opening/opening-timeline.json`. It preserves the current narrative cue times,
four-tap rhythm and final gameplay handoff; captions remain live UI rather than being burned into video.

1. Living culture and a four-tap response.
2. Petrov physically taps the vessel.
3. Recognition and response.
4. Vale/Petrov disagreement.
5. Culture division into two synchronized bodies.
6. Contact filament reaches the worker’s actual wrist; recoil, collapse and Petrov’s response.
7. Mechanical restraint closes; Petrov returns to the rhythm.
8. Vale operates the interlock; real containment/floor apertures and hinged covers open.
9. The free culture crosses the lit service conduit.
10. It emerges through a street grate into a textured, inhabited alley.
11. An ordinary passerby crosses beyond it; the camera prepares the gameplay transition.

Actual character body/finger clips are retargeted from the existing animation library at film frame rate, with
hand IK, small head layers and source-measured speech jaw envelopes. Existing rigged characters, licensed textures and corrected native film sets
are reused read-only. All new geometry, animation and scripts live in these full-story source folders.

## Full lake sequences

`Lake/` contains separate packed sources for the 15-second neutral arrival, 6-second neutral choice hold,
7-second Peace resolution and 7-second Dominion resolution. The branch clips remain separate assets and must
only start after the existing explicit immutable choice. Neutral footage never chooses or starts a Dominion reveal.
See the lake manifest, audit and inspected CPU preview storyboard for exact coverage and branch isolation.

## Motion source

`Motion/` contains the compact 34-action source library, baked real-actor samples, numeric FK/proportion checks,
and CPU pose diagrams. The complete source scenes are packed and each is kept below GitHub’s 100 MB file limit.

## Build and review

```bash
Tools/blender/cinematics/full_story/build_opening.sh
```

This authors the opening and small CPU Cycles previews. The audit reopens the packed source in a fresh Blender
process and checks story duration, camera bindings, actual evaluated poses, physical contact, mechanism action,
and key-action framing. It does not claim a final GPU render, encoded film, audio mix or game integration.

GPU rendering remains a separate gated phase. `render_story.py` refuses a GPU render without the explicit
`--gpu-released` argument; that flag may only be used after root releases the RTX from the build/benchmark phase.
It writes working frames only under ignored `Logs/full-cinematic-renders`, binds resume to source/driver/Blender/device/sampling provenance, selects packed camera cuts per frame and supports a graceful stop file. See `Tools/blender/cinematics/full_story/RENDER_DRIVER.md` for chunk and resume commands.

## Preliminary cost and disk envelope

The combined opening plus lake footage is 115 seconds / 2,760 unique frames at 24 fps. The current quality target
is 1080p Cycles with 48 adaptive samples, denoising and physical glass/water. Calibration is still required on
representative actor/glass frames. The verified earlier 720p/32-sample inserts took about 4–8 seconds per frame;
linear pixel/sample scaling suggests roughly 10–27 seconds per 1080p/48 frame, or about 8–21 GPU hours overall.
This estimate is deliberately broad: skin, animation and glass can change the cost, and adaptive sampling can
reduce it. A measured RTX keyframe pass should replace it before a long render begins.

At approximately 2–3 MB per 1080p PNG, complete working frames need about 5.5–8.3 GB. Packed editable sources
are a few hundred MB total; final VP8 films at roughly 7 Mbps would be about 100 MB combined, split into files
well below GitHub’s individual 100 MB limit. There was 26 GB free when this phase started. Render one shot or
branch at a time, verify its encode, then archive/clear working chunks only after evidence is preserved.

Before replacing the shipped inserts, the full films still require final visual/motion review, measured RTX
rendering, complete encode/audio synchronization, real Linux-player decode, skip/release behavior, explicit-choice
isolation and the final gameplay handoff tests.
