# 0.6.0 preview — native verification scope

**Status: SELECTED LOGIC PASS — 4/4 selected cases, native exit 0.** The completed run used Null graphics, simulated input
and prepared fixtures. This is a pass for the four named logic scenarios, not the full 27-case suite, native graphical
acceptance, a performance result or a human playthrough.

The final run receipt is **2026-11-25 19:27:28.329808 UTC**. Post-run integrity checks confirm the source, candidate and both
retained preview trees were unchanged.

## Which player this describes

- Version: **0.6.0-preview**, with 23 implemented missions.
- Build identifier: `full-story-20261125T191303012539Z-a6d70d5c`.
- Source identifier: `aa9ec701fe46af2dfec23e1ad7508f0f5b38dbd4`; the adjacent `BUILD_INFO.json` records the exact source snapshot.
- Runtime SHA-256: `0435dd5e00913a4e341492690547536778c33f5fd522009d15a9ea6dd92a375b`.
- `BUILD_INFO.json` SHA-256: `1009a8afed2434112b5ac23f5acc02b393d4b8a30f06605f354c06bfdeb62117`.

The separately retained/running 0.5.1 preview is a different twenty-mission player. Neither its results nor this document
change that older executable. Use the `BUILD_INFO.json` supplied with your copy to confirm its identity.

## Selected native campaign checks

| Case | Scope | Current status |
|---|---|---|
| 08 | Original campaign through the maintenance/service-tunnel route | **PASS — 231.1 seconds** |
| 25 | Peace Harbor pressure, moving antidote lots, relay and checkpoint recovery | **PASS — 124.3 seconds** |
| 26 | Dominion Harbor pressure, moving relay lots, relay and checkpoint recovery | **PASS — 136.9 seconds** |
| 27 | Two earned in-flight cargo reroutes, separate checkpoint restore and physical arrivals | **PASS — 51.0 seconds** |

These are automated scenarios. The Harbor scenarios prepare earlier chapter prerequisites and initial role ownership, then
exercise the chapter's movement, E interactions, timing and physical conditions. They are not an end-to-end human run through
all 23 missions. Editor/source/import checks are separate evidence and are not standalone-player graphical passes.

## Still outside this scope

- A completed full 27-case native suite on this exact player.
- Rendered appearance, graphics settings, frame pacing and performance acceptance for this exact player.
- Physical keyboard/gamepad verification, physical speaker listening and a complete human playthrough.
- Persistent campaign saving: checkpoints are held only within the running process. Settings preferences are separate.

Linux gameplay uses real-time URP shaders, screen-space reflections and probes. Offline RTX/OptiX Blender films do not make
that gameplay hardware ray traced. The new Harbor lines use captions without newly recorded narration.
