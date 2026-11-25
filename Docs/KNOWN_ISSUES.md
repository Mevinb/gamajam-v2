# Known issues and validation limits

The full realism/expansion request remains active. [HANDOFF.md](HANDOFF.md) and [UPGRADE_TRACKER.md](UPGRADE_TRACKER.md) name
current work; every pass applies to its recorded source or binary.

- Source 0.5.2 adds close-range aiming guidance and a live-host/physical-spacing test driver. Full Route 08 passes in the Null
  Editor on the pinned pre-hint snapshot; two post-hint focused cases pass priority, occlusion, turn-away and actual E controls.
  A rebuilt native binary remains required; the running user player is still 0.5.1.
  [Moving-host proof](verification/moving-host-aim-20261125.json).
- The new security-booth kit passes Blender source/export and eleven Null Unity import checks, but its CityBuilder hook,
  generated scene, native appearance and performance remain unverified. It is outside the running user build.
  [Booth proof](verification/city-security-booth-20261125.json).
- Gate-capture schema 2 now records raw authored-light and camera contracts and restores staged dynamic lights; only CPU
  compilation is verified. Fresh old/new scene renders remain required. [Fixture proof](verification/gate-capture-fixture-20261125.json).
- Linux/URP uses screen-space reflections and probes. It has no built-in hardware ray tracing; Blender films use offline
  RTX/OptiX path tracing. SSR cannot reflect geometry outside the camera buffers; puddles are not a full fluid simulation.
- The user-requested **gate WIP** is`Builds/Candidates/full-story-20261125T173043238618Z-0f595731`,0.5.1-preview,
  1,117MB/nativebuild0/zeroerrorswarnings/source+retainedpreview unchanged. Corrected absolute-path media8checks/7images,
  shortcuts14/14 and renderedwater22 all pass native0/zero ring warnings; user playtest is launched, not a complete playthrough.
  This has the same runtimeDLL as the earlier graphics build but a different scene/art/source snapshot; older campaign results
  are not transferred. [WIP proof](verification/0.5.1-gate-playtest-20261125.json).
  Gate import21checks/GPUregeneration/exact329collider-4door-144light inventory and actual99-ray door/aperture/navigation pass.
  Before8captures/fullrestore pass; AFTERcapture fails raw static matching despite8/8camera/settings/rain agreement and full
  restoration. DynamicAmoebaLight variation is a hypothesis; raw light rows were not persisted, so sole cause is unproved.
  Native gate access/fullroutes/performance and accepted matched visual comparison remain open.
- The earlier pre-gate0.5.1-preview candidate at`Builds/Candidates/full-story-20261125T170047223995Z-63921b41`, runtime`80e65c6b...`, passes
  native strictfull-five media(8checks/7images),14queuedshortcuts and renderedwater22, all exit0/zero ring warnings. These do
  not prove native Custom-control persistence/frame pacing, continuous80s story playback or a full human campaign. Native 23/24
  now pass 2/2, exit 0, on this same runtime using prepared Null-graphics fixtures. The other run is terminal 19/21, exit 1:
  case 08 failed when a moving officer invalidated its snapshot aim, and case 21 requires a rendered GPU for its camera check.
  Live-host/physical-spacing source passes full Route 08 in the Null Editor; native Route 08 and rendered 21 remain required. No complete 24-case result
  is claimed. [Original/riverside result](verification/0.5.1-native-original-river-20261125.json).
- The Editor Vulkan warning gate remains open after serializedIndividualFaces:13pass/1PendingDowngrade warning failure/
  1expectedNullskip/native2,9raw Graphics Ring Buffer warnings. Diagnostic controls with14passes still have warnings; no
  production all-green result or allocator cause is established. Narrow native gates currently have none. No allocator flag,
  quality reduction or warning suppression is a verified fix. [Settings proof](verification/independent-graphics-settings-20261125.json).
- New independent settings have135passing EditMode rules and passing named GPU ownership/Custom/UI/Bloom cases. The initial
  Bloom metric used a stale SingleCameraRequest volume stack; the corrected StandardRequest fixture passes with unchanged
  emitter/ROI/threshold and actual On1.178656/Off0.1345335/restored1.178656 HDR halo values. This is a rendered effect proof,
  not a final-player performance result. Queued keyboard/gamepad navigation is separate from physical device testing.
- Gate WIP checks above apply to the regenerated scene and173043build. The earlier pre-gate semantic comparison verifies
  its own snapshots only; later gate validation is separately pinned. No final visual/performance release is implied.
- The older0.5.0 candidate(runtime2603...) was launched for the user and exited normally0 after130.601s. Its headless campaign
  stopped after01–05pass/06duty-officer aim failure with no final receipt before reboot. Yaw/pitch test feedback passes focused15
  and shared06/07/08/14/16 in the Editor with unchanged targeting rules. The new binary still needs the remaining campaign.
- Six desktop0.5.0 baseline routes pass; riverHigh has one217.1ms worst frame with unproved cause. GPU timers/per-gameVRAM,
  dedicated lab coverage and a final archive/extracted player remain pending. Older FPS values are not new-binary measurements.
- Immutable`Builds/Linux`0.5.0-preview contains M18/four full lake films, not M19/M20 or subsequent fixes. The retained0.4.2archive
  has two short inserts. Neither is relabelled by new source docs.
- All five silent full films have complete frame/decode provenance. Continuous80s opening, selected actual voice progression,
  skip/choice and gameplay/media restoration pass named Editor methods. New native media acceptance is initial-image/advancement/
  terminal-seek cleanup, not a complete80s natural watch. Physical speakers/listening and native full story remain separate.
- Scoped native movie retirement fixes the reproduced old-preview cleanup hang in newer pinned candidates. Actual pending-seek
  Quit0/1 preserves requested codes after ACK/zeroowned resources. OS/window-close with media pending and unexpected graphics
  owner destruction remain unexercised paths. [Movie proof](verification/cinematic-retirement-20261125.json).
- M19/M20 both branches and all twelve recovery helpers now pass native scenarios 23/24 on runtime `80e65c6b...`, with normal
  exit 0 and unchanged binary. This includes the corrected actual sentry exposure/Holding retreat/fresh E case, retaining the
  original vision/exposure/time rules. Setup grants prerequisites, forces initial role possession and parks unrelated guards;
  this is not a complete human or stealth playthrough. [Native rescue proof](verification/0.5.1-native-rescue-20261125.json)
  and [physical walker proof](verification/island-walker-20261125.json) retain precise limits and earlier failures.
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
