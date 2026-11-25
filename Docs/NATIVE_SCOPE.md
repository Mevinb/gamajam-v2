# 0.6.0 preview — native verification scope

**Overall status: PENDING.** The selected native run has not produced its final process exit/receipt as this page is prepared.
Do not treat the preview as a complete 27-scenario pass, a graphics/performance acceptance or a human playthrough.

## Which player this describes

- Version: **0.6.0-preview**, with 23 implemented missions.
- Build identifier: `full-story-20261125T191303012539Z-a6d70d5c`.
- Source identifier: `aa9ec70`; the adjacent `BUILD_INFO.json` records the build's exact source snapshot.
- Runtime SHA-256: `0435dd5e00913a4e341492690547536778c33f5fd522009d15a9ea6dd92a375b`.

The separately retained/running 0.5.1 preview is a different twenty-mission player. Neither its results nor this document
change that older executable. Use the `BUILD_INFO.json` supplied with your copy to confirm its identity.

## Selected native campaign checks

| Case | Scope | Current status |
|---|---|---|
| 08 | Original campaign through the maintenance/service-tunnel route | Individual PASS record, 231.1 seconds; enclosing run still pending |
| 25 | Peace Harbor pressure, moving antidote lots, relay and checkpoint recovery | Pending final result |
| 26 | Dominion Harbor pressure, moving relay lots, relay and checkpoint recovery | Pending final result |
| 27 | Two earned in-flight cargo reroutes, separate checkpoint restore and physical arrivals | Pending final result |

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
