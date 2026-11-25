# Known issues and validation limits

The full realism/expansion request is active. See [UPGRADE_TRACKER.md](UPGRADE_TRACKER.md) and the current
[handoff](HANDOFF.md). A focused pass applies to its recorded source/binary; it does not establish that all bugs are fixed.

- Linux x86-64 remains the user's selected target. URP uses screen-space reflections and probes; built-in hardware ray tracing
  is not enabled in this player. Blender cinematic footage uses RTX/OptiX offline path tracing. Screen-space reflections cannot
  reflect objects outside the camera buffers, and puddles are not a complete fluid simulation.
- The immutable `Builds/Linux` 0.5.0 preview was launched for the user and exited normally. It contains M18, four full lake films
  and the earlier island presentation. It does not contain M19/M20, eight island voices or the subsequent source fixes. Do not
  relabel it as the latest private candidate or a final release. The retained 0.4.2 archive is older and has two short film inserts.
- The current0.5.0 candidate at `Builds/Candidates/full-story-20261125T143348976819Z-ea740a95` is built from clean commit
  `2f3fde9`,1,101MB with zero build errors/warnings. Its strict full-five media stress passes eight checks/seven PNGs, queued
  shortcuts pass14/14, and rendered Vulkan water passes1/1. All three exit natively0 with unchanged runtime hash and zero
  ring-buffer warnings. The user-controlled desktop player exited normally0 after130.601s. The23-case headless campaign
  stopped without a final receipt:01–05 pass,06 fails at duty-officer targeting,07 begins. The machine has since restarted;
  both old processes are absent. The stale running receipt is not a campaign pass. Diagnostics prove a yaw-only test aim
  missed the unchanged15° cone. Test-camera yaw/pitch feedback passes focused15 and shared06/07/08/14/16 in the headless
  Editor, including all three original infiltration paths through extraction. A new native rebuild, corrected24 and the
  remaining current-player cases are still required. [Aim proof](verification/hunter-camera-aim-20261125.json).
  Six focused current-binary High/Ultra baseline routes now pass; riverside High retains one217.1ms worst frame whose cause
  is unproven. Dedicated lab, GPU timer/per-game VRAM and archive/extracted-player checks remain pending.
  See [candidate proof](verification/0.5.0-candidate-20261125.json) and [baseline details](verification/graphics-baseline-0.5.0-20261125.json).
- All 1,920 opening frames and all 840 lake frames pass source/frame provenance, visual sample review and complete FFmpeg decode.
  All five silent 1080p24 films are published in live Resources. Editor full-five decode, continuous normal80s opening,
  eleven shot checkpoints, selected actual voice progression, skip/settings/choice and gameplay/media restoration pass.
  The original suite records 12/13 after a score-pause cursor baseline failure; the bounded actual-DSP baseline correction
  and normal80s rerun pass 2/2/native0 with unchanged hold tolerance. Native graphics ring-buffer warnings remain recorded
  (21 in the first collection, two in the focused run); their allocator cause and player impact are unproven.
  The new candidate's strict decoder/terminal-seek check now passes; continuous80s native story observation remains separate.
  See [Editor runtime proof](verification/full-opening-runtime-20261125.json)
  and [opening production evidence](../ArtSource/FullOpeningFilm/README.md).
- The preview's strict movie tests decoded the footage but hung during native cleanup. The newer scoped lifetime fix passes
  rendered, batch and untraced default terminal-seek stress with normal native exit 0. Actual pending-seek Quit(0)/Quit(1) probes
  also pass after real acknowledgement and zero owned/pending/quarantined resources, preserving requested native codes. These
  results apply to pinned private candidates, not the immutable preview. OS/window-close while media is pending remains unexercised.
  See [cinematic retirement proof](verification/cinematic-retirement-20261125.json).
- M19/M20 pass both complete branches and all original 14 Editor rescue fixtures, including custody, generation/loss,
  visited-cover/checkpoint, boarding and all-free departure. The focused physical sidestep fixture and native scenario 23 now
  pass on the earlier `c35c66a7...` diagnostic player. Its native scenario 24 later exits 1 at the exposure fixture: ordinary sentry idle scanning loses
  the follower's sight cone. A fixture-only sentry-facing correction passes the focused Editor case with actual sight and
  exposure 2.1 before Regroup; vision rules and time limits are unchanged. Native 24 and the full campaign regression remain
  required on the final rebuilt player. See [the physical-walker proof](verification/island-walker-20261125.json).
- The centred free-body camera passes more than 17,000 actual GPU samples; human framing and canonical markers remain. This
  isolated proof is separate from physical input and an extended human playthrough.
- Reflection lifecycle, fallback and normal water pass six RTX/OpenGL cases. The rendered standalone Vulkan water case passes
  with native exit 0. Standalone batch mode does not finish that native probe; use normal rendered mode for this graphics check.
  Null devices intentionally keep native probes disabled.
- The integrated Blender wall kit passes 140 import checks, all four entry-route/outcome traversals, camera and 2/2 raster/visual
  cases, plus an optional HDR staged art review. Three true 2D/2K BC7 maps use about 16 MiB; existing maps remain 512. The model
  adds no colliders. Staged scene-only art images are not a substitute for final-player UI/gameplay captures.
- Desktop island measurements on a profiled private candidate reach all 20 waypoints with AI active and native exit 0 at
  1920×1080/Vulkan: High 265.6 FPS / p99 9.44 ms; Ultra 179.0 FPS / p99 11.28 ms. All sampled frames were focused. Earlier
  private Xvfb `:97` results around 25 FPS were presentation-confounded and did not justify quality/AI cuts. These are bounded
  route measurements, not constant-frame-rate claims or measurements of a future release binary.
- Dialogue is synthesized rather than recorded by human actors. Eight new island clips pass technical format/key/level gates;
  seven Editor cases exercise actual source tails for all eight clips plus timing, skip and missing/disabled fallback. Subjective
  listening, physical speaker output and the final rebuilt-player check remain separate. Captions remain available.
- Humans use skinned Blender/MakeHuman models and authored/retargeted animation. Skin, hair, facial acting and some environment
  geometry remain stylized; further realism work is still possible.
- Traffic has kinematic lanes, right-of-way, bumper clearance and pedestrian braking. Unexpected overlaps, complex merges and
  prolonged congestion still need extended playtesting. It is not a full road simulator.
- Some city prop detail is selected during scene generation and static batching rather than changing at camera distance.
  Preset LOD bias affects only assets that support runtime LODs.
- Automated tests use simulated movement or queued Input System events. They do not establish physical keyboard/gamepad behavior,
  a complete human playthrough, or every graphical configuration. Final story/settings/campaign and package checks remain required;
  current-candidate media stress and queued shortcuts are already recorded separately.
- Two historical long Editor attempts ended in native SIGSEGV without final XML; a subsequent complete run on that earlier
  source passed. Their initiating cause remains unproven. Do not present later narrow native passes as a universal engine-crash repair.
- Campaign checkpoints survive scene reloads within the running process. Persistent on-disk campaign saving is not implemented.

Caches, render frames, captures and builds consume substantial disk space. Check live free space before large jobs, retain
source/provenance and the user's immutable preview, and keep generated output out of Git. The existing package shell script
modifies `Builds/Linux`; use a reviewed explicit-candidate packaging path before shipping the newer player.
