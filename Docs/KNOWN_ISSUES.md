# Known issues and validation limits

The full realism/expansion request is active. See `UPGRADE_TRACKER.md`; do not interpret a passing focused test as proof that
all bugs are fixed or every new cinematic has been verified in the final player.

- Linux x86-64 is the target chosen for the current upgrade. URP reflections use screen-space tracing and reflection probes;
  built-in hardware ray tracing is not enabled in this Linux player. RTX path tracing is being used for Blender cinematic footage.
- Humans use skinned Blender/MakeHuman models and authored/retargeted humanoid animations. Skin, hair, facial acting and some
  environment geometry remain stylized; further realism work is still possible.
- Dialogue voices are synthesized. They are not recordings of human actors; captions remain available.
- The player includes two three-second RTX-rendered establishing inserts and Blender-designed live sets. The complete 80-second
  Blender opening and branching lake films are authored separately and still require production rendering and runtime integration.
  `TEST_REPORT.md` records actual-player checks for the current inserts and riverside chapter.
- The old skyline slab hid the river/berth water in 0.4.1. Version 0.4.2 cuts only that backdrop's rendering; before/after
  GPU views and the built Vulkan player confirm visible water with unchanged physical footing. The historical 0.4.1 archive
  retains the earlier rendering behavior.
- Screen-space reflections cannot reflect objects outside the camera's depth/color buffers. Reflection probes provide fallback;
  puddles are not a complete fluid simulation.
- Traffic follows kinematic lanes with right-of-way, bumper clearance and pedestrian braking. It is not a full road simulator;
  unexpected overlaps, complicated merges and prolonged congestion still need extended playtesting.
- Some imported city prop detail is selected at scene generation and merged into static batches, rather than switching LOD at
  camera distance. Preset LOD bias only affects assets supporting runtime LODs.
- Automated tests use queued keyboard/simulated movement. They do not establish physical keyboard/gamepad behavior, an extended
  human playthrough, or constant frame rate in all camera views.
- Two long Editor verification attempts exited natively with SIGSEGV and no final XML. The subsequent complete run passed all seventeen PlayMode tests
  and eighteen campaign scenarios with source/art writes frozen on a private NVIDIA-backed display. The initiating cause
  of the earlier exits remains unproven; no claim is made that a specific native engine defect was repaired.
- Campaign checkpoints survive scene reloads within the current process. There is no persistent on-disk campaign-save claim.
- Historical benchmark numbers describe their original build and route. A fresh measured result must include resolution, preset,
  render scale, route coverage and frame-time percentiles; a successful build alone is not a performance result.

Caches, footage intermediates, verification captures and builds can grow quickly on this workstation. Check free space before
large jobs. Keep generated builds/logs out of Git; compact editable art sources and the actual shipped cinematic clips belong in it.
