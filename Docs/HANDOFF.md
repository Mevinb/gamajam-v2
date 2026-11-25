# PARASITE — developer handoff

Read this before changing the project. The active request is a full realism/cinematic upgrade and continued mission expansion,
with frequent commits and pushes as **Mevinb**. This supersedes the old eight-mission scope freeze and local-only capture session.
The full requested upgrade is **in progress**; see [UPGRADE_TRACKER.md](UPGRADE_TRACKER.md). Evidence applies to its named source
or binary. Historical passing runs do not verify a newer player.

## Current checkpoint — 25 November 2026

- Linux/URP remains the target chosen by the user. Blender uses RTX/OptiX offline path tracing; the game's screen-space
  reflections and probes are separate features. Do not describe the Linux player as hardware ray traced.
- Source through `2f3fde9` is pushed to `origin/main` as Mevinb. The live source contains M18–M20, eight island voices, the
  Blender quarantine-wall kit, camera/reflection corrections, scoped native movie retirement and the physical rescue walker's
  bounded NPC sidestep/focused regression. The full opening and new continuous-playback proof extend that checkpoint.
- The 0.5.0 baseline passes 144 runtime / 23 editor / 144 Linux Mono API checks and all 122 EditMode rules.
  The independent graphics-settings work beyond that binary compiles 148/23/148; its new Unity tests are pending.
  These checks supplement actual Unity imports, rendering and player execution; they do not replace them.
- The current **0.5.0 candidate** is `Builds/Candidates/full-story-20261125T143348976819Z-ea740a95`, built from clean commit
  `2f3fde9` at 1,101 MB, zero errors/warnings and native build exit 0. Runtime SHA-256 is
  `2603c57202ec01363e80f156bd69ea117179fb598082ac1e2b63a75224472abc`. Source and the retained preview tree were unchanged
  during its build. All twenty missions, full five films, eight island voices and the wall/camera/movie fixes are included.
- Three normal rendered RTX4050/Vulkan gates pass on that exact runtime, each with native exit 0: strict full-five media
  (eight checks/seven real PNGs), queued shortcuts (14/14) and water case22 (1/1). Their logs have zero native ring-buffer
  warnings and the runtime hash is unchanged. This fast media stress is separate from continuous80s story playback.
  [Candidate proof](verification/0.5.0-candidate-20261125.json) pins build, source, reports, logs, images and native receipts.
  The user-controlled RTX4050/Vulkan playtest exited normally after 130.601 seconds, native code 0. The 23-case headless
  regression has no final exit receipt: five cases pass, Route A fails at duty-officer targeting, and Route B begins before
  the log stops. The machine has since restarted; both old processes and their tool session are gone. Do not treat its stale
  running JSON as a live process or a campaign pass. Current diagnostics preserve the targeting rules while inspecting
  camera angle, range and obstruction. The fixture used yaw-only aim and missed the unchanged15° cone. Yaw/pitch feedback
  now passes focused15 and all shared06/07/08/14/16 scenarios in a headless Editor run with native exit0; no targeting rule
  or production camera change was needed. [Exact aim proof](verification/hunter-camera-aim-20261125.json). All six current-binary
  desktop benchmarks pass; release archive remains pending. The old0.5.0 binary is unchanged and still needs a later rebuild.
- On the earlier `c35c66a7...` diagnostic player, native scenario 23 passes both full rescue branches in 516.231 seconds with native exit 0
  (`Logs/native-island-23-sidestep-retry`). The earlier failure was the test walker blocked by delivered Morgan.
  The focused fixture first proves real capsule blockage, then reaches a valid lateral route and the original target using
  normal simulated movement. Collision, navigation, support checks and original time limits remain intact.
- Native scenario 24 on that earlier diagnostic player exits 1 after 128.893 seconds: the exposure fixture fails to sustain actual sentry sight
  during its normal idle scan. A focused diagnostic also fails the displaced-follower visibility prerequisite. The fixture
  now faces its sentry between Holding and the westward lead, retaining ordinary scanning and unchanged vision/exposure
  rules. `island-exposure-facing.xml` passes 1/1 in 35.36 seconds; actual sight stays true as exposure reaches 2.1 and Regroup.
  The final rebuilt player must rerun 24. Exact source/artifact scopes are in
  [the physical-walker proof](verification/island-walker-20261125.json). Headless runs do not prove graphics or physical input.
- All 2,760 planned RTX frames are rendered: four lake sequences (840 frames) and the 80-second opening (1,920 frames),
  at 1920×1080, 24 fps, 48 samples, OptiX and GPU OIDN. Every production job exits 0. All five films pass exact source/frame
  provenance and complete FFmpeg decoding and are published in live Resources. The opening is 79,993,655 bytes, silent VP8,
  SHA-256 `e76b223295625233e32c30930951bd8718b40ed45e032050b24b923016d1ea27`.
  Actual Editor RTX/Vulkan story verification passes across the original collection and focused rerun: the original collection
  records 12/13 with one score-pause cursor baseline failure; the corrected pause fixture and normal80s opening rerun pass 2/2,
  zero skips, native exit 0. Full five-film decode, all eleven normal-speed shots, selected real voice progress, skip/settings,
  natural resolutions and gameplay/camera/HUD/media cleanup pass their named methods. The current built player now passes
  strict full-five decode/terminal-seek cleanup; continuous80s natural story proof remains Editor scope.
  [Runtime proof](verification/full-opening-runtime-20261125.json) preserves both suite outcomes and 21/2 native graphics warnings.
  [Portable opening evidence](../ArtSource/FullOpeningFilm/README.md) and tracked publishing tools retain the production.

## Current user playtest and retained preview

The new0.5.0 candidate was launched for the user at14:39:37 UTC (20:09 local) on RTX4050/Vulkan and exited normally at
14:41:48 UTC, native code 0. `Logs/user-playtest-0.5.0/session.json` records the 130.601-second session. This proves launch
and normal exit, not a completed human playthrough or physical-input test. Do not relaunch it merely because an older note
calls the process running.

The requested build-and-launch was completed with `0.5.0-preview` under `Builds/Linux`. It contains M18 and the four full lake
films; it does not contain M19/M20 or the subsequent fixes. It launched on native `DISPLAY=:0` / RTX 4050 / Vulkan and later
exited normally with code 0. This records launch and exit, not a claim that the user completed a playthrough.

`Logs/user-playtest/build-info.json`, `source-snapshot.json`, `session.json` and `player.log` retain its exact provenance.
Its build was 1,007 MB with zero errors/warnings. The runtime SHA-256 is
`8ce33854dd55496442655cef1c61ce404b9d5d9dc077d5606c89a28c88a9fabd`.
Keep this preview intact. New candidates use fresh `-parasiteBuildDirectory` paths; do not relabel the preview as current.
The retained 0.4.2 archive is an earlier release containing two short film inserts.

## Verified source and private-player behavior

- **Campaign:** M15–M17 continue the original explicit Peace/Dominion lake choice. M18–M20 add island access, branch-sensitive
  rescue/courier work, physical signal custody, boarding and protected departure. All original 14 rescue fixtures pass actual
  Editor execution across focused suites, including both complete branches, visited-cover recovery, ownership generation,
  checkpoints and physical boarding clearance. The additional sidestep fixture passes separately. Native 23 passes; the later
  native 24 exposed a sight-cone setup defect, whose sentry-facing correction passes the focused Editor case. Native 24 and
  a complete campaign regression remain required on the final rebuilt player.
- **Island art and camera:** the editable Blender wall source, two exported meshes and shared 2K PBR maps are integrated.
  Actual import passes 140 checks; maps are Texture2D/2048/BC7 (about 16 MiB total), existing 18 maps remain 512, and the new
  model adds no colliders. All four entry-route/outcome traversals and the full camera case pass with the kit. Raster/visual
  checks pass 2/2 after isolating and exactly restoring Atmosphere for structural proof; HDR staged art review passes 1/1.
  The free-body camera passes more than 17,000 samples without changing human framing or mission markers. Portable sources,
  before/after PNGs and exact proof are in [QuarantineWalls.README.md](../ArtSource/Island/QuarantineWalls.README.md).
- **Reflections:** completed native capture is copied into an independent owned Cubemap before the native probe is retired.
  Editor RTX/OpenGL verification passes six cases with one Null-only skip. Rendered standalone Vulkan water case 22 passes
  1/1 with native exit 0. Standalone batch mode does not schedule that probe correctly; use normal rendered mode for this
  graphics check. High/Ultra enable probes only on actual graphics devices; preserve the Null guard.
- **Voices:** eight offline-generated island clips pass format, key and level checks. Seven Editor cases exercise actual
  source tails for all eight arrival/refuge/roster/departure clips plus skip/fallback/timing behavior. Subjective listening
  and verification in the final rebuilt player remain separate work.
- **Movie lifetime:** the immutable preview's native cleanup hang was reproduced and symbolicated. The newer scoped lifetime
  owner keeps real pending prepare/seek operations alive until acknowledgement, then retires native objects before releasing
  targets. Default forced-terminal stress remains enabled. Editor verification passes 12 cases with one expected missing-opening
  skip; rendered, batch and untraced native stress all pass seven checks with normal exit 0. Strict missing-opening mode
  correctly exits 1. Optional pending-seek Quit(0)/Quit(1) probes each pass 12 checks and seven PNGs, with actual seek ACK,
  zero owned/pending/quarantined resources and the requested native code. OS/window-close pending-media behavior is unexercised.
  Exact proof: [cinematic-retirement-20261125.json](verification/cinematic-retirement-20261125.json).
- **Performance:** the exact 0.5.0 binary on desktop `:0`, 1920×1080/Vulkan, AC power and balanced profile passes all six
  High/Ultra routes with every sample focused, AI active and native exits 0. City exterior averages169.2/111.7FPS,
  riverside212.9/140.5 and island252.2/169.5. All p99 times are8.68–12.84ms, but riverside High has a217.1ms worst frame;
  its cause is unproven. GPU timing/per-game VRAM are unavailable, shared PSO cache makes these mixed-cache runs, and no
  dedicated laboratory-interior route is included. [Exact baseline proof](verification/graphics-baseline-0.5.0-20261125.json)
  retains every report/profile/image hash, power/focus observation and limitation. Older Xvfb results were presentation-confounded.

## Remaining release work

1. The duty-officer fixture correction passes focused15 and all five shared route/recovery cases in the Editor. Rebuild and
   run the remaining current-player campaign cases, including corrected native24. Rendered22 already passes separately. The prior run under
   `Logs/full-story-candidate/campaign-headless` is interrupted with no native exit receipt; its recovery observation records
   five passes, one failure and the missing processes after reboot.
   Preserve real terminal exits and hashes, and distinguish prepared/simulated scenarios
   from a continuous human playthrough.
2. Complete final-player story/audio/settings observations. The user-controlled playtest has exited. Strict full-five media,14queued shortcuts and
   rendered water already pass on this binary; continuous80s natural opening and selected live voice proof remain Editor scope.
3. Six desktop baseline routes on this binary are complete; retain the single217.1ms spike for investigation. Verify the new
   independent graphics controls with real pixels/ownership, then a fresh release package and isolated extracted player.
   `Tools/package_build.sh` targets and modifies `Builds/Linux`;
   use an explicit-candidate packaging path while retaining the preview. No release archive or current-player FPS result is claimed.
4. Continue the active realism/gameplay expansion from verified playtest findings. Do not mark the broad goal complete from
   compilation, render completion, a single route or these focused fixtures.

## Cinematic production and GPU scheduling

Production evidence is under `Logs/full-cinematic-renders/full-production-20261125T003711Z`.
`production-summary.json` records complete jobs for all five sequences. Verify actual processes before starting any GPU job;
old PID files and lock files are not evidence of a live renderer. Do not rerender completed frames after an observation timeout.
The packed blends, render driver and render policy are frozen and hash pinned; preserve them and all complete frame records.

For a future render resume only, request a safe stop using that production directory's `STOP`, then wait for the active frame,
Blender and supervisor to become terminal before another Unity graphics/player/benchmark job. Headless logic work may coexist.
Resume the same production directory via `production_plan.py`; it verifies recorded frame/input hashes. A 4 GiB disk reserve
stops rendering between complete frames. Check free space before encoding, imports, builds or packages; about 4 GiB was
free after the candidate build. Never infer a completed asset or passing runtime decode solely from a status file.

## Environment and commands

- Project: `~/Data/Parasite`; remote `https://github.com/Mevinb/gamajam-v2`, branch `main`.
- Unity 6000.3.29f1: `~/Data/Unity/Editor/6000.3.29f1/Editor/Unity`; URP 17.3, Cinemachine 3, Input System.
- Blender: `~/Data/Blender/current/blender` (not on PATH). Existing pipelines live in `Tools/blender/`.
- Linux x86-64 remains the target. The user selected RTX-rendered Blender cinematics plus improved real-time shaders.
- GPU: RTX 4050 Laptop GPU, 6 GB; RAM 16 GB. Check `df -h .` before large imports/renders. About 4 GiB remained after the current candidate build; check live free space.
- Launch the built Linux player using `./play.sh`; it selects NVIDIA offload and prefers Vulkan on NVIDIA systems.
  Use `PARASITE_GRAPHICS_API=glcore ./play.sh` for OpenGL; explicit Unity API flags take priority.

```bash
Tools/compile_check.sh all
Tools/unity.sh buildscenes -executeMethod Parasite.EditorTools.CityBuilder.BuildAllBatch
Tools/unity.sh editmode -nographics -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml
Tools/unity.sh playmode -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml
grep -E '\[AutoTest\] (PASS|FAIL)' Logs/playmode.log
Tools/unity.sh photos -executeMethod Parasite.EditorTools.PhotoTool.CaptureBatch
Tools/unity.sh build-linux -executeMethod Parasite.EditorTools.BuildAutomation.BuildLinux
./play.sh -autotest -autotestOut /tmp/parasite-acceptance.txt
./play.sh -benchmark -benchmarkPreset High -benchmarkDistrict city -autotestOut /tmp/parasite-benchmark.txt
./play.sh -benchmark -benchmarkPreset Ultra -benchmarkDistrict riverside -autotestOut /tmp/parasite-riverside-benchmark.txt
Tools/package_build.sh linux
```

`Tools/unity.sh <log-name> <args>` serializes Unity with flock and writes `Logs/<log-name>.log`. Never open the editor GUI or
start another editor process on this project during a batch job. Poll the returned process/session; an old lock file is not
proof a job is alive. Coordinate Blender GPU renders with Unity graphics jobs, especially benchmarks on this 6 GB card.
Explicit `DISPLAY`/`XAUTHORITY`, including an Xvfb display, are preserved by `Tools/display.sh`.

`Tools/compile_check.sh all` now includes a Linux-Mono player API check, using only references/defines from the last valid
Bee player cache and enumerating current source anew. `runtime`, `editor` and `player` modes are explicit; unknown modes fail.
A successful Linux build seeds this cache. Unity-version, package lock or assembly-definition changes require a fresh build;
the player check fails clearly rather than silently using Editor assemblies. It does not replace shader/build/player tests.

After edits to editor builders, `MissionLayout`, or `CityLayout`: **compile → regenerate scenes → PlayMode → Linux rebuild**.
The generated Bootstrap and City scenes must never be hand-edited. Test source changes with the relevant focused suite,
then run campaign regressions when shared behavior or mission progression changes. Package only after the build and its
verification report agree. `Logs/`, `Library/`, and `Builds/` are ignored; they must not be committed.

## Current game architecture

All runtime source is under `Assets/Parasite/Scripts`, assembly `Parasite.Runtime`; editor tooling is in `Assets/Parasite/Editor`.
Use one MonoBehaviour per file, with the file matching the class name. Unity scene serialization depends on this.

- **Boot:** City scene's `World/CityDirector` creates GameManager, SuspicionSystem, MissionManager, PossessionManager,
  AbilitySystem, bakes the collider NavMesh under `World`, spawns `NpcSpawn` markers through `HostFactory`, then creates camera,
  traffic, HUD, ambience and weather. `GameManager.Awake` builds the riverside annex before the bake when the later chapter is active.
- **State:** `Core/GameManager` owns Playing/Paused/Ending/Ended/Resetting. Static `RunState` survives checkpoint scene reloads,
  including mission flags, index, statistics and immutable lakeside outcome; New Game clears it. There is no disk campaign-save claim.
- **Input/possession:** `GameInput` defines Input System actions and acceptance-only simulated controls. `PossessionManager`
  supports two amoebas, aimed E possession, Q release, V split and Tab switching. `PlayerCamera` uses Cinemachine third-person follow.
- **Humans:** `HostCharacter` switches between NavMeshAgent and CharacterController; do not enable both CC and HitBox.
  `HumanoidRig` loads Blender/MakeHuman skinned variants and humanoid animation. `HostBrain` and `GuardAI` handle routines/pursuit.
- **Stealth:** `SuspicionSystem` tracks witnessed violations, cameras report directly, hunters track burned bodies, and guards capture hosts.
- **Missions:** `MissionManager.BuildCampaign` supplies 14 initial missions. Flags drive progression/objectives/waypoints; completed
  evidence and sabotage flags restore world state. `MissionProp`, `CampaignOperation`, `CooperativeConsole`, `DualAuthReader`,
  `AccessDoor`, and `StaffTerminal` implement role and prerequisite gates. A cooperative console remains held by that same host;
  its paired operation needs a different possessed host, and cancellation does not preserve transient work.
- **Later chapter:** `AfterlakeCampaign` appends three missions only after the original summary's deliberate Continue action.
  `AfterlakeDistrict` builds a bounded riverside annex and NPC markers before NavMesh baking. Peace/Dominion changes the story,
  records objective, and radio role choices. Completion requires the free amoeba at the ferry. The first lakeside outcome persists.
- **Presentation:** Deliberate New Game requests the 80-second `OpeningCinematic`; reload/restart/direct City loads do not replay it.
  `LakesideEpilogue` requires explicit Peace/Dominion before publishing the original summary. Waiting or skipping never chooses.
  Both use unscaled time while live gameplay is frozen. Blender set assets and PBR surfaces are loaded through `CinematicArt`.
- **UI:** code-built uGUI in `HUDController` partial files; `MainMenuController`, `SettingsOverlay` and `UIKit` share input/navigation.
  Escape closes a pause submenu before resuming. Journal entries are built from the current campaign rather than hard-coded counts.
- **Graphics:** `GraphicsPreset` applies saved Low/Medium/High/Ultra settings to pipeline and newly created cameras. GroundWater's
  reflections are screen-space depth-buffer ray marches; they are not hardware ray tracing. `LampLights` budgets local lights.
  Custom atmosphere/foliage/facade/amoeba shaders and packed PBR textures provide the real-time look.
- **Audio/FX:** `AudioPreferences`, voice/narration/ambience/music systems; `FxApi`, `SfxBackend`, `AmoebaVisual`, `HostGlow`;
  weather and slime trail. Temporary cinematic gain must not overwrite saved preferences.
- **Builders:** `CityBuilder` + `Editor/World/*`, `MissionPlacement`, `ProjectSetup`, `FxSetup`; `PhotoTool` captures environments,
  while PlayMode UI capture renders actual overlay canvases through the camera. `BuildAutomation` creates the Linux player.

## Campaign

| Mission | Core objective |
|---|---|
| 1 OUT OF THE DRAIN | Leave the alley and borrow the first host. |
| 2 WORD ON THE STREET | Read the plaza newsstand and eavesdrop at the café. |
| 3 THE TAIL | Follow Petrov without crowding or losing her, then recover her dead drop. |
| 4 CREDENTIALS | Guard/main gate, scientist/keycard/staff terminal, or maintenance/power/tunnel. |
| 5 VALE'S RESEARCH | Copy two terminals and use two separate hosts at the office readers. |
| 6 CREATOR | Confront Vale; protected dialogue returns control afterward. |
| 7 HUNTER PROTOCOL | Evade bio-scans, bypass the arch as free amoeba, trip the duty-officer breaker. |
| 8 THE PART HE HID | Maintenance holds coolant while another scientist copies the signed archive. |
| 9 BREAK THE LEASH | Guard holds dispatch while another maintenance host overloads the relay. |
| 10 THE SHADOW UPLINK | Broadcast preliminary evidence without pursuit. |
| 11 THE BLACK SITE ARCHIVE | Trigger a diversion, then download the black-site archive as a scientist. |
| 12 CONTAINMENT PROTOCOL EPSILON | Overload backup power and override the forecourt security grid. |
| 13 BLACKOUT | Cut three transformers in 35 seconds using three distinct human palm prints. |
| 14 THE FINAL CONVERGENCE | Broadcast the unredacted archive and extract as free amoeba through the original drain. |
| Lake | Watch the arrival, explicitly choose Peace or Dominion, then reach the original summary. |
| 15 AFTER THE STILL WATER | Answer Petrov's distress signal and isolate a poisoned floodgate. |
| 16 BORROWED HEARTS / A WILLING NETWORK | Analyse runoff, coordinate guard/scientist hosts at the shelter records. |
| 17 THE OPEN CHANNEL / THE FIRST MANY | Hold mast power with maintenance, transmit with another host, leave by ferry as free amoeba. |

The presentation shortcut remains Ctrl+M (either Ctrl, either press order) through initial title/opening/pause/dialogue/gameplay.
It does not choose a lakeside outcome. It must not restart the old finale during the riverside chapter. Existing queued-input
checks do not establish a native physical keyboard or physical gamepad pass. Do not guess a replacement shortcut.

## Blender/art pipeline

- `Tools/blender/characters/`: MakeHuman/MPFB models, authored garments and textures; installed under `Art/Characters`.
- `Tools/blender/animation/`: retargeted/authored clips and previews.
- `Tools/blender/environment/`: tree meshes, bark and leaf atlases, HDR environment assets.
- `Tools/blender/cinematics/build_sets.sh`: deterministic original laboratory, conduit, bench and shoreline sets.
  Editable sources: `ArtSource/Cinematics`; runtime exports/textures: `Resources/Cinematics`; importer: `CinematicArtImporter`.
- `Tools/blender/cinematics/build_films.sh`: two RTX/OptiX establishing inserts, three seconds each; packed film scenes and
  derived device/codec/SHA evidence in `ArtSource/Cinematics`. Verify built-player decode with `-cinematicTestOut <absolute-path>`.
- `Tools/blender/riverside/build_props.sh`: eight original ferry/clinic/radio/floodgate/bank assemblies; editable source and
  CPU export verification in `ArtSource/Riverside`, game assets in `Resources/Riverside`.
- Asset generation must finish before a frozen Unity test run. Publish verified FBX/PNG/video files atomically from staging.
  Blender-to-Unity export needs the verified handedness conversion; size-only bounds checks miss front/back reflection.
- Consult `Docs/THIRD_PARTY_ASSETS.md` for external licences; keep source provenance alongside new assets.

## Verification and remaining work

`Docs/TEST_REPORT.md` owns artifact-specific evidence. `Docs/KNOWN_ISSUES.md` lists limits. `Docs/UPGRADE_TRACKER.md` retains the
full active request and the evidence needed for completion. An implementation or an old passing XML is not proof the new
feature works in the final player. Report unverified player video decode, physical controls, long playthroughs and performance honestly.

## Gotchas

- Unity 6 licensing must be active. Use Unity Hub sign-in only if logs show a licensing problem.
- Story freezes simulation at timeScale=0; use realtime waits/unscaled camera/audio/skip clocks, never speed acceptance by unfreezing AI.
- Commit the original Escaped flag only after an explicit resolved lake outcome. Keep continuation separate from replaying that choice.
- `Physics.IgnoreCollision` resets when colliders disable. Preserve HostCharacter's CC/HitBox switching.
- Unity object fake-null makes `?.`/`??` unsafe for destroyed objects; prefer `if (obj)` checks.
- Disabling a runtime-mutated quality feature must restore its lower-quality setting as well as enabling its higher-quality setting.
- The city atmosphere's height fog must not be applied to isolated cinematic stages far below the city.
- Keep ContributeGI off generated geometry unless deliberately introducing baked GI. Do not skip environment/reflection generation.
- World text needs `WorldTextFont` and the depth-tested shader, including atlas-rebuild refresh; do not mutate the shared UI font material.
- Use deterministic `ShopSeed`/local RNG for generated decoration; preserve unrelated shared RNG draw counts.
- Tests have no focused GameView: route queued keyboard events through DynamicUpdate/IgnoreFocus and restore settings after tests.
- Editor batch ScreenCapture/WaitForEndOfFrame is unreliable for overlay UI. Use the existing URP SingleCameraRequest capture helper.
- Cold shader builds may take many minutes. Poll the live process; an observation timeout is not a failed job.
- `pkill -f` can kill its own shell. Identify the exact PID and only terminate the intended task.
- Git identity is Mevinb only. Commit/push working features often, without Devin or generated-by trailers.
