# Known issues and validation limits

The full realism/expansion request is active. See `UPGRADE_TRACKER.md`; do not interpret a passing focused test as proof that
all bugs are fixed or every new cinematic has been verified in the final player.

- Linux x86-64 is the target chosen for the current upgrade. URP reflections use screen-space tracing and reflection probes;
  built-in hardware ray tracing is not enabled in this Linux player. RTX path tracing is being used for Blender cinematic footage.
- Humans use skinned Blender/MakeHuman models and authored/retargeted humanoid animations. Skin, hair, facial acting and some
  environment geometry remain stylized; further realism work is still possible.
- Dialogue voices are synthesized. They are not recordings of human actors; captions remain available.
- The retained 0.5.0 preview has four captioned island arrival/refuge lines without voice clips. The newer working tree now
  contains eight offline-generated island clips and duration-aware arrival/departure timing. Technical decode/key/level gates
  pass. Five Editor cases now pass real source-tail progression, arrival/branch departure timing and skip/fallback behavior.
  Subjective listening, refuge/roster clip runtime playback and built-player checks remain pending. The preview is unchanged.
- The 0.5.0 preview includes four complete RTX-rendered lake films and Blender-designed live sets. All four films pass actual
  Editor decode/choice gates; built-player decode succeeds but normal shutdown remains unresolved. The complete 80-second Blender
  opening is still rendering. The retained 0.4.2 archive contains the earlier two three-second establishing inserts.
- M19/M20 source now passes both full branches and all 14 rescue fixtures, including physical custody, generation/loss,
  cover/checkpoint, boarding and all-free departure gates. A fresh built-player run is still required.
- The centred free-body camera passes 17,347 GPU samples; human framing and canonical markers remain. Physical input and
  extended human playthroughs remain separate from this automated camera proof.
- Reflection lifecycle and normal water pass six actual RTX/OpenGL cases; native capture owns its RT, so publication now uses
  an independent copied Cubemap. Null scheduling is guarded. Built-player/Vulkan checks remain pending.
- The immutable preview decodes all four lake films and two old clips and wraps the hold on Vulkan and OpenGL, but both strict
  test processes hang during native video cleanup and hit watchdog 137. Decode reports alone are not end-to-end passes.
  Exact Unity symbols locate the cleanup lock; its cause/repair remains under investigation.
- A new Blender precast-wall kit is staged with 2K PBR maps and unchanged physical openings/colliders. Unity import, appearance
  and performance are unverified; it is not yet in the live source/build.
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
